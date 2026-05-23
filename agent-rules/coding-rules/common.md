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

---

## スレッド安全性

- ネイティブコールバックは Unity のメインスレッド以外から来る場合がある
- コールバック内での Unity API 呼び出しは必ず `UnityMainThreadDispatcher.Instance.Enqueue(action)` 経由で行う
- `UnityMainThreadDispatcher` は Manager の `Awake` でメインスレッド上にインスタンスを生成しておく

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
