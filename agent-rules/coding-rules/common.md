# 共通実装方針

このファイルはプラットフォーム横断の実装方針を定義する。
ここに書かれた原則はすべての C# 実装（Dialog / Notification / 共通ユーティリティ）に適用する。
プラットフォーム固有の Bridge パターンの詳細は各ルールファイルを参照すること。

---

## アーキテクチャ（Manager + Bridge パターン）

### 層の定義と依存方向

```
native-toolkit（ネイティブ側）
        ↓  AAR / XCFramework / DLL
  Unity Bridge（P/Invoke / AndroidJavaObject）
        ↓
    Manager（MonoBehaviour Singleton）
        ↓
  ExampleController（UI 操作・結果表示）
```

- **Unity Bridge**: ネイティブ API と C# の境界。`[DllImport]` / `AndroidJavaObject` を用いてネイティブ関数を呼び出す
- **Manager**: `MonoBehaviour` Singleton。Bridge 呼び出しを集約し、コールバックをイベントで公開する
- **ExampleController**: Manager の公開 API を呼び出すサンプル UI コントローラ

### 依存のルール

- Manager はネイティブ呼び出しの詳細（`DllImport` / `AndroidJavaObject`）を隠蔽する
- ExampleController は Manager のイベント（`event Action<...>`）にのみ依存する
- プラットフォーム固有のコードはコンパイルガード（`#if UNITY_ANDROID` 等）で分離する

---

## Unity Bridge パターン

### Android

- `AndroidJavaObject` / `AndroidJavaClass` でネイティブ Singleton を取得する
- コールバックは `AndroidJavaProxy` サブクラスで受け取り、`UnityMainThreadDispatcher` 経由でメインスレッドに転送する

```csharp
using (AndroidJavaClass cls = new AndroidJavaClass("com.example.Plugin"))
{
    AndroidJavaObject instance = cls.CallStatic<AndroidJavaObject>("getInstance");
    instance.Call("showDialog", activity, title, message, listener);
}
```

### iOS / macOS

- `[DllImport("__Internal")]` でネイティブ C 関数をインポートする
- コールバックは `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` delegate + `[MonoPInvokeCallback]` static メソッドで受け取る
- IL2CPP で動作させるため、コールバックメソッドは必ず `static` にする

```csharp
[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void showDialog(string title, string message, DialogCallback callback);

[MonoPInvokeCallback(typeof(DialogCallback))]
private static void OnDialogCallback(string? buttonText, bool isSuccess, string? errorMessage)
{
    UnityMainThreadDispatcher.Instance.Enqueue(() =>
    {
        Instance.DialogResult?.Invoke(buttonText, isSuccess, errorMessage);
    });
}
```

### Windows

- `[DllImport("NativeToolkit")]` でネイティブ DLL 関数をインポートする
- コールバックパターンは iOS / macOS に準ずる

### ポインタが必要な配列渡し（共通）

- string[] を native に渡す場合は `Marshal.AllocHGlobal` でアンマネージドバッファを確保し、コールバック完了後に `Marshal.FreeHGlobal` で解放する
- `try / catch / finally` で確実に解放する

---

## Manager 設計ルール

### Singleton

```csharp
private static XxxManager? _instance;

public static XxxManager Instance
{
    get
    {
        if (_instance == null)
        {
            var go = new GameObject("XxxManager");
            _instance = go.AddComponent<XxxManager>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }
}

private void Awake()
{
    if (_instance == null) { _instance = this; DontDestroyOnLoad(gameObject); }
    else if (_instance != this) { Destroy(gameObject); }
    _ = UnityMainThreadDispatcher.Instance;
}
```

### イベント公開

- 結果は `public event Action<...>?` で公開する
- シグネチャは `(buttonText, isSuccess, errorMessage)` を基本とし、追加データを先頭に並べる
- `isSuccess == true` のとき `errorMessage == null` を保証する

### プラットフォームガード

- Manager クラス全体を `#if UNITY_ANDROID` 等で囲む
- Editor での動作確認は `!UNITY_EDITOR` と組み合わせて制御する

### 公開 API 方式（同期・非同期の判断）

**基本原則: ネイティブ API が同期なら C# も同期、非同期なら C# も非同期にする。**
Bridge 層で同期・非同期を変換しない。

| ネイティブ API | C# Manager の公開 API |
|---|---|
| 同期（即座に値を返す） | 同期メソッド（戻り値をそのまま返す） |
| 非同期（callback / delegate） | callback 版（event + `Action<TResult>?`、必須）+ `Awaitable<TResult>` 版（下記「多重呼び出しガード」の前提条件を満たす場合のみ） |

**禁止:**

- 同期ネイティブ API を `Awaitable` / `Task` で包んで非同期に見せること
  （呼び出し側に不要な `await` と1フレーム遅延を強いる）
- 非同期ネイティブ API を `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` で
  同期化すること。ネイティブ callback は `UnityMainThreadDispatcher` 経由で
  メインスレッドに戻るため、メインスレッドで待つと **必ずデッドロックする**
- 同期ネイティブ API を別スレッドへ逃がして非同期化すること
  （ネイティブ側がメインスレッド前提の場合に破綻する）

### 非同期版の併設ルール（callback 版 + Awaitable 版）

非同期ネイティブ API に対しては、callback 版を必ず用意する。
`Awaitable` 版は、下記「多重呼び出しガード」の前提条件（in-flight ガードが実装されていること、
または同時実行しても安全であることが確認されていること）を満たす操作にのみ併設する。

前提条件をまだ満たさない操作は、ガード実装が完了するまで **callback 版のみでよい**。
これは設計・実装の欠落ではなく、正しい初期実装として扱う（後から `Awaitable` 版を追加するのは
非破壊的な変更のため、ガードが未整備な段階で無理に `Awaitable` 版を作らないこと自体がルール）。

- **callback 版**: `event Action<TResult>?` + 任意の per-call `Action<TResult>?` の形。
  ネイティブコールバックが任意スレッドから来ること、IL2CPP で
  `[MonoPInvokeCallback]` が static 制約を持つことから、Bridge 直結の受け口として必須。
- **非同期版**: `Awaitable<TResult>` を返す `XxxAsync` メソッド。
  呼び出し元（アプリコード / ExampleController）が `await` で直線的に書けるようにするためのもの。

**方針:**

- callback 版は必ず維持する（既存 API を壊さない）。event は非同期版の
  呼び出し時にも必ず先に発火させる（購読者の挙動を変えない）
- 非同期版は `AwaitableCompletionSource<TResult>` で callback 版を包む
  **薄いラッパー**とし、Manager 内でネイティブ呼び出しロジックを重複させない
- 戻り値は既存の結果型（`IosShareResult` 等）をそのまま返し、
  `isSuccess == false` を例外に変換しない（Bridge 側のエラー表現と一貫させる）。
  引数不正など呼び出し側のバグは従来どおり例外でよい
- 命名は `XxxAsync`。`CancellationToken` を受ける場合は最終引数に置き、
  MonoBehaviour からは `destroyCancellationToken` を渡す
- `Awaitable` は Unity 6 標準型（`UnityEngine.Awaitable`）を使う。
  UniTask など外部パッケージへは依存しない
- `Awaitable<T>` は `Task<T>` と異なり **await できるのは1回だけ**。
  戻り値をフィールドに保持して複数箇所で await せず、呼び出しごとに `XxxAsync` を呼ぶ
- `await` 後は Unity メインスレッドが保証されるため、
  非同期版の呼び出し元では `UnityMainThreadDispatcher` を挟む必要はない
- 複数操作の並行待ち（`Task.WhenAll` 相当）が必要になった場合のみ、
  その API に限り `Task<TResult>` 版を追加する

```csharp
// Bridge 向け（既存維持）
public void Share(IosShareContentPayload? payload, Action<IosShareResult>? onResult = null)

// 呼び出し元向け（callback 版を包む薄いラッパー）
public Awaitable<IosShareResult> ShareAsync(IosShareContentPayload? payload)
{
    Debug.Log($"[{LogTag}][{nameof(ShareAsync)}] payload: {payload}");
    var source = new AwaitableCompletionSource<IosShareResult>();
    Share(payload, result => source.TrySetResult(result));
    return source.Awaitable;
}
```

### 多重呼び出しガード（非同期版を追加する前提条件）

現行の Manager は per-call callback を **static スロット1つ**に保持し、
「last-registered wins」（後勝ち）で上書きする実装になっている
（`IosShareManager` / `MacShareManager` / `AndroidShareManager`）。

この方式は callback 版では「1回目の callback が呼ばれない」で済むが、
**`Awaitable` 版では1回目の `await` が永久に完了しない（ハングする）**。
`AwaitableCompletionSource` が `TrySetResult` されないまま破棄されるため。

**ルール:**

- 非同期版を追加する前に、その操作が **OS 上で同時実行可能か**を判定する
- 同時に1つしか提示できない操作（ダイアログ、共有シート、権限要求など）は
  **in-flight ガード**を持たせる。実行中の再呼び出しは、進行中の操作をキャンセルせず、
  **新しい呼び出し側に失敗結果を即座に返す**（既存の呼び出しの結果は必ず届ける）
- ガードは **callback 版に実装する**。非同期版は薄いラッパーのままに保つ
- 後勝ち方式のまま `Awaitable` 版を作らない。
  捨てられた callback は `await` の永久未完了に直結する
- per-call callback を持たず **event のみ**で結果を返す API
  （`IosDialogManager` の `ShowDialog` 系など）には、そのままでは `Awaitable` 版を作れない。
  結果と呼び出しを対応付ける手段が無いため、先に per-call callback を追加すること

---

## スレッド安全性

- ネイティブコールバックは Unity のメインスレッド以外から来る場合がある
- コールバック内での Unity API 呼び出しは必ず `UnityMainThreadDispatcher.Instance.Enqueue(action)` 経由で行う
- `UnityMainThreadDispatcher` は Manager の `Awake` でメインスレッド上にインスタンスを生成しておく

---

## ファイル作成ルール

- `.meta` ファイルは Unity が自動生成するため、AI エージェントは作成しない

---

## JSON によるデータ転送

- 複雑な構造（通知コンテンツ等）はネイティブとの境界を JSON 文字列で渡す
- `XxxJsonBuilder` クラスで JSON 組み立てを担い、Manager から呼び出す
- JSON のスキーマ変更はネイティブ側との整合を必ず確認する

---

## テスト（TDD）

### 基本方針

- 新機能は ExampleController 単位で手動確認項目を定義する
- Manager の初期化・イベント購読・コールバック転送は EditMode テストで検証する
- プラットフォーム依存の動作（実機での表示確認等）は手動確認項目として明記する

### テストフレームワーク

| 種別 | フレームワーク | 備考 |
|------|--------------|------|
| EditMode | NUnit (Unity Test Runner) | ネイティブ呼び出しなし |
| PlayMode | NUnit (Unity Test Runner) | シーン上での動作確認 |
| 手動確認 | 実機 / Unity Editor | ネイティブ Bridge 依存の項目 |

### テストの確認タイミング

- コードを修正・追加した後は、**必ず既存のテストコードを確認する**
  - 修正内容によって既存テストが壊れていないか確認する
  - 新機能・新フィールドに対してテストが不足していれば追加する
- テスト確認後、Unity Test Runner でテストを実行してすべて passed であることを確認する

---

## Minimum Versions

設計・実装時は、以下の最小バージョン以上で動作確認が必須。

- **Unity**: 6 以降
- **Android**: 12 以降
- **iOS**: 18 以降
- **Windows**: 11 以降
- **macOS**: 15 以降
