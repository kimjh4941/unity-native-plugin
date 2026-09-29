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

### Windows（native-toolkit の C ABI）

Windows は native-toolkit の C ABI（ヘッダーは `native-toolkit/dist/<版>/windows/include/NativeToolkitC/`、
DLL は dist の `windows-native-toolkit-capi-<版>.dll`）を P/Invoke で呼ぶ。
以下は Dialog / Notification / Clipboard を 2.0.0 に移す設計（`artifact/windows/{dialog,notification,clipboard}/designs/`
の 2026-09-27 版）で決めた約束ごとで、新しい Windows の機能も従う。
3 機能とも 2026-09-27 に移行を終え、1.x の C ABI（`unity-windows-native-toolkit.dll`、UTF-16 のバッファと JSON）を呼ぶコードは残っていない。

**ファイルの置き場所**

| ファイル | 持つもの |
|---|---|
| `Runtime/Windows/Common/WindowsNativeToolkitCApi.cs`（1 つだけ。名前空間 `JonghyunKim.NativeToolkit.Runtime.Windows.Common`） | DLL 名の定数、版の確認、`Common.h` の関数（`ntk_version`、`ntk_last_system_code`、`ntk_string_*`、`ntk_bytes_*`、`ntk_string_list_*`）の `extern`、UTF-8 の読み書き。**各機能は DLL 名を自分で書かず、ここの定数を使う**（native-toolkit の版を上げるときの書き換えを 1 か所にするため） |
| `Runtime/Windows/<Feature>/Windows<Feature>CApi.cs`（Bridge） | その機能の `ntk_<feature>_*` の `extern`、入れ子の構造体、コールバックの delegate と受け口、ネイティブの値と C# の値の変換 |
| `Runtime/Windows/<Feature>/Windows<Feature>Manager.cs` | 公開 API、状態、イベント（Manager 設計ルール） |

名前空間は `JonghyunKim.NativeToolkit.Runtime.Windows.<Feature>`（「命名」の節）。

層は Bridge と Manager の 2 つだけにする。変換（引数の組み立て、結果とエラーの変換、版の判定）は Bridge の中の
`internal static` の純粋な関数にし、`DllImport` を囲む `#if` の**外**に置く（Editor でもコンパイルされ、EditMode でテストできる）。
変換のためだけのファイルや層は作らない。

**`DllImport`**

- `[DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)] internal static extern`
  - DLL 名は `.dll` まで書く（名前に `.` を含むので、拡張子を補ってもらえる前提に立たない）
  - `DEVELOPMENT_BUILD` で DLL 名を切り替えない
  - `internal` にする（層 2b のテストが結び付けを直接確かめるため。`private` では `InternalsVisibleTo` でも呼べない）
- 型: ハンドル・ポインタ・関数ポインタは `IntPtr`、`size_t` は `UIntPtr`、`int32_t`（列挙・真偽を含む）は `int`、
  `uint32_t` は `uint`、`int64_t` は `long`、エラーの戻り値は `int`。`const uint8_t*` の入力は `byte[]` で渡してよい
- 構造体は入れ子の `internal struct`、`[StructLayout(LayoutKind.Sequential, Pack = 8)]`。`struct_size = (uint)Marshal.SizeOf<T>()`、
  `reserved*` は 0。大きさと全フィールドの位置を `Marshal.OffsetOf` で層 1 に固定する（期待値は native-toolkit の `CApiLayoutTest.cpp`）

**文字列とハンドル**

- 入力の文字列は `Encoding.UTF8` の NUL 終端のバイト列を `Marshal.AllocHGlobal` に置く。確保したものは 1 つの一覧で追い、`finally` で全部解放する
- 出力の文字列は `*_data` と `*_size` でバイト列を写し、UTF-8 で読む。
  **`[return: MarshalAs(UnmanagedType.LPUTF8Str)]` は使わない**（マーシャラーが戻りのポインタを解放し、ヒープを壊す）
- 出力のハンドルは、呼ぶ前に `IntPtr.Zero` で初期化した変数に受け、`finally` で `!= IntPtr.Zero` のときだけ対応する `_free` に渡す
- 借りたポインタ（一覧の要素、コールバックの引数）は、持ち主の寿命のうちに managed の値へ写す。
  同期で受け取ってその場で解放するハンドルは `SafeHandle` にしない

**try の形**

```csharp
try
{
    try { /* ネイティブ呼び出しと変換 */ }
    finally { /* 解放 */ }
}
catch (Exception e) { code = /* 例外 → エラーコード（純粋な関数） */; }
// catch の外で、結果を 1 回だけ出す
```

finally を内側に置くのは、C# の catch が同じ try の finally から出た例外を捕まえないため（解放の失敗も結果にする）。
購読者の例外を catch の中に入れない（結果のイベントは catch の外で出す）。

**コールバック**

- delegate は `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`。static readonly のフィールドで保持し、
  `Marshal.GetFunctionPointerForDelegate` で渡す。受け口は `[MonoPInvokeCallback]` の static メソッド
- 登録ごとの状態を持たないなら、`user_data` と `release` は `IntPtr.Zero`（ネイティブは `release` が NULL でよい）
- 受け口は全体を try/catch で囲み、例外をネイティブに返さない
- OS のスレッドで来るコールバックは、`Awake` でメインスレッドから取っておいた dispatcher に積むだけにする。
  **ネイティブのスレッドで `UnityMainThreadDispatcher.Instance` の getter に触らない**（無ければメインスレッド以外で GameObject を作る）
- 受け口の中から、その持ち主（セッション・マネージャー）の close / free を呼ばない

**版の確認**

最初のネイティブ呼び出しの前に、`WindowsNativeToolkitCApi` で `(ntk_version() >> 16)` が C# の想定する major と一致するかを確かめる。
一致しないとき、または `DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` のときは、
「ネイティブが使えない」エラーにする（コードの値は、その機能の既存のエラーコードの体系に合わせる）。

**Editor**

DLL は importer で Editor 無効にしてあり、Editor ではネイティブを呼ばない（「プラットフォームガード」）。
こうするとドメインリロードで DLL が下ろされないことへの後片付けが要らない。
Editor でネイティブを呼ぶ機能を作る場合は、close → free → release を待つ後片付けを別に設計する
（`artifact/topics/windows-c-abi-2/README.md`「ドメインリロード」）。

**ログ**

結果（エラーコード）として呼び出し側に返す失敗は `Debug.Log` か `Debug.LogWarning` で書き、`Debug.LogError` にしない。
`csharp.md` の「エラーは `Debug.LogError`」は、想定外の失敗（捕まえた例外、壊れた状態）に当てはめる。
Unity Test Framework は想定していない `LogError` でテストを落とすので、エラーの結果を期待するテストが書けなくなる。

**COM のアパートメント**

Clipboard のセッションは STA のスレッドで作る（Unity のメインスレッドは MAINSTA）。
Notification の `ntk_notification_manager_create` は呼び出したスレッドで MTA を試みるが、既に STA ならそのまま STA で動く。
どちらもメインスレッドから使う。

**振る舞いの差分**

1.x から移すとき、または native-toolkit の版を上げるときは、公開 API で変わる振る舞いを設計書に「既知の差分」の表（差分 / 前 / 後）で書き、
XML コメントと実装結果ファイルを通して次の版のマニュアルへ引き継ぐ。

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

新しく書くコードは二重のガードにする（`review-document` の P5）。

- 型（Manager・Bridge・結果型・エラーコード）は `#if <PLATFORM> || UNITY_EDITOR` で囲む。Editor でも型が見え、層 1 / 2a でテストできる
- `DllImport` / `AndroidJavaObject` と、ネイティブを実際に呼ぶ箇所は `#if <PLATFORM> && !UNITY_EDITOR` で囲む。
  `#else` の側は「このプラットフォームでは使えない」結果を返すか、何もせずに戻る（機能ごとの既存の契約に合わせる）
- 前例: `Runtime/Windows/Clipboard/WindowsClipboardManager.cs`
- 片方だけのガード（`#if UNITY_ANDROID` だけ、など）の既存の型は `testing.md`「3. Manager ごとのコンパイルガード差異」の B 群を参照。新しく真似しない

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

### 命名: OS 接頭辞と、共通ファイルを作らない方針

**`Runtime/` と `Tests/` 配下は、プラットフォーム単位で管理する。共通ファイルを作らない。**

**Runtime のディレクトリと名前空間（2026-09-27 決定）**

| 対象 | ディレクトリ | 名前空間 |
|---|---|---|
| 正しい形 | `Runtime/<Platform>/<Feature>/` | `JonghyunKim.NativeToolkit.Runtime.<Platform>.<Feature>` |
| そのプラットフォームの全機能が使う共通部 | `Runtime/<Platform>/Common/` | `JonghyunKim.NativeToolkit.Runtime.<Platform>.Common` |
| プラットフォームをまたぐ共通（下の「例外」） | `Runtime/Common/` | `JonghyunKim.NativeToolkit.Runtime.Common` |

- `<Platform>` のディレクトリ名は `Windows` / `Android` / `iOS` / `macOS`（`UI/<Platform>/` と同じ綴り）
- **Windows は移行済み**（`Runtime/Windows/Clipboard/` など）。**Android / iOS / macOS は `Runtime/<Feature>/`（名前空間 `JonghyunKim.NativeToolkit.Runtime.<Feature>`）のまま**で、それぞれの OS の対応のときに移す。移すまでは、その OS の新しいファイルも今の場所に置く（1 つの OS の中で形を混ぜない）
- 移すときは `.cs` と `.meta` をいっしょに `git mv` する（GUID を保つ）。名前空間の変更は公開 API の破壊的変更になるので、その版の既知の差分とマニュアルに書く
- `Tests/Runtime/` と `Tests/PlayMode/` はプラットフォームで分けない（ファイル名の接頭辞で分かる）

ファイル名と、その中の public / internal な型名には、対象プラットフォームを接頭辞で表す。**テストファイルも同じ規則に従う。**

| 対象 | 接頭辞 | 例 |
|---|---|---|
| Android | `Android` | `AndroidClipboardManager.cs` |
| iOS | `Ios` | `IosClipboardManager.cs` |
| macOS | `Mac` | `MacClipboardManager.cs` |
| Windows | `Windows` | `WindowsNotificationManager.cs` |

- **機能ディレクトリに接頭辞なしのファイルを作らない。** 2 つのプラットフォームが同じロジックを必要とする場合も、**共通化せずそれぞれに持たせる**
- ファイル名と、そのファイルが定義する主たる型の名前を一致させる
- ディレクトリの形は上の表（`Runtime/<Platform>/<Feature>/`）と、後述の `UI/<Platform>/<Feature>/`
- **`Tests/Runtime/` と `Tests/PlayMode/` のファイル名・クラス名にも接頭辞を付ける。** テスト対象のプラットフォームがファイル名から分かることが目的
  - 例: `MacClipboardJsonParserTests.cs` / `IosClipboardManagerDispatchTests.cs`
  - **複数プラットフォームの型を 1 つのテストファイルで扱わない。** 対象ごとにファイルを分ける
  - テストのコンパイルガードは対象型に合わせる（対象が `#if UNITY_STANDALONE_OSX \|\| UNITY_EDITOR` ならテストも同じ）

**共通化しない理由:** プラットフォーム単位で管理できることを優先する。共有すると、片方のプラットフォームの都合で変更したときに、もう片方へ意図しない影響が及ぶ。実際に macOS Clipboard の設計時、iOS の JSON リーダーを共有化する案を検討したが、この方針により **`MacClipboardJsonReader` として複製する**ことにした。重複のコストより、プラットフォームごとに独立して変更できることを取る。

**例外: `Runtime/Common/`**

`Runtime/Common/` は**意図的に共通とした横断インフラの置き場所**であり、この方針の対象外とする。接頭辞を付けない。

| ファイル | 位置づけ |
|---|---|
| `UnityMainThreadDispatcher.cs` | 全プラットフォームの Manager が使う。意図的な共通 |

新しく `Common/` へ置く場合は、**機能ロジックではなく横断インフラであること**を条件とする。特定プラットフォームの機能に属するものは機能ディレクトリへ置き、接頭辞を付ける。

**例外: `Runtime/<Platform>/Common/`**

そのプラットフォームの**全機能が使うネイティブライブラリの共通部**だけを置ける。ファイル名には接頭辞を付ける。機能の中身は置かない。

| ファイル | 位置づけ |
|---|---|
| `Runtime/Windows/Common/WindowsNativeToolkitCApi.cs` | Windows の全機能が使う native-toolkit の C ABI の共通部（DLL 名、版の確認、`Common.h` の関数、UTF-8 の読み書き。「Unity Bridge パターン」の Windows） |

**共通化しない理由との関係:** 共通化を避けるのは、片方のプラットフォームの都合がもう片方に及ぶのを防ぐため（上）。
同じプラットフォームの機能どうしは**同じネイティブライブラリの同じ版を必ず一緒に使う**ので、この理由が当てはまらない。
逆に機能ごとに持つと、ライブラリの版を上げるたびに機能の数だけ DLL 名などの書き換えが要り、1 か所でも漏れるとその機能だけ読み込みに失敗する。
置いてよいのは「そのライブラリを使うどの機能にも同じ形で要るもの」に限る。1 つの機能だけが使うものは、その機能の Bridge に置く。

**`UI/` のディレクトリ構成**

`UI/` は `UI/<Platform>/<Feature>/` の構造を採る（Runtime の正しい形と同じ順）。ファイル名の接頭辞は同じ規則に従う。

**既知の逸脱（新規実装で真似しない）: Runtime 11 件**

| 逸脱している型 | 実際の所属 |
|---|---|
| `Clipboard/ClipboardOperationResult` / `ClipboardReadResult`（`ClipItem` / `ClipContents` を含む） / `ClipboardDescriptionResult` | Android |
| `Notification/NotificationResult` / `NotificationActionResult` / `NotificationReceivedResult` | Android |
| `Share/ShareOperationResult` / `ShareCallbackResult` / `ShareChooserActionResult` / `ShareChooserActionCallbackCoordinator` | Android |
| `Common/IconConfiguration` | **macOS**（`MacDialogManager` 専用。`Common/` にあるが横断インフラではない） |

**`Tests/` の逸脱 2 件は 2026-09-03 に解消済み。** テストファイルは公開 API ではないため、Runtime の改名方針を待たずに直せた。

| 旧 | 新 |
|---|---|
| `Tests/Runtime/ShareChooserActionCallbackCoordinatorTests` | `AndroidShareChooserActionCallbackCoordinatorTests` |
| `Tests/Runtime/ShareResultTests` | `AndroidShareResultTests` ＋ `IosShareResultTests`（`IosShareResult` の 6 件を分離） |

原因は「その機能を最初に実装したプラットフォームが接頭辞を付けなかった」ことで、2 番目のプラットフォームを追加する際に付け直す手順が無かったためである。

- **これらを他プラットフォームから使わないこと**
- **これらを見て「接頭辞なしは共通の意味」と読まないこと。** `Common/` 配下だけが共通である
- 改名は破壊的変更（すべて `public`）になるため別課題として扱う。詳細と対応案: `artifact/topics/os-prefix-violations/README.md`

---

## サンプルシーン（ExampleController + UXML）

### 入力欄を設けない

**既定は「入力欄（`TextField`）を置かない」。** 値はコード内の固定 fixture を使い、操作はボタンで完結させる。

- 実行時にしか値が存在しないもの（クリップボード履歴の item id 等）は Controller の内部状態として保持し、
  画面には保持の有無と操作対象を出す
- 任意文字列が API の分岐条件になる場合（未登録のフォーマット名など）は、
  欄で入力させるのではなく**代表値ごとのボタン**にする

**理由は自動化の難易度ではない。** 層 2b はテストが Unity プロセス内で動くため、
`TextField.value` の設定は容易であり、むしろ `Button.clicked` の方が
`Clickable` のポインタイベント列を要する。既定を「置かない」にしているのは次の 3 点による。

- 既存サンプル画面が例外なく入力欄を持たない。1 画面だけ例外を作ると、
  操作方法とテストの書き方が画面ごとに分かれる
- 手動確認で操作者の打ち間違いが**偽の不具合に見える**。
  履歴 item id を打ち損ねた `InvalidArgument` は、ライブラリの欠陥と区別が付かない
- 実行時生成 ID は内部保持の方が手数が少ない（`Get History` → `Restore Last` の 2 操作で済む）

### 例外を採る場合

**機能の性質上どうしても入力欄が要る場合は置いてよい。ただしサンプルシーン計画書に
「なぜこの機能では固定 fixture で足りないのか」を注意書きとして明示する。**

理由を書かずに例外を作らないこと。既定から外れていること自体は計画書を読んでも分からず、
既存画面との差分としてしか現れないため、レビューを素通りする。

> 実例: Windows Clipboard のサンプルシーン計画 v1 が、Android / iOS の計画に明記されていた
> 「入力欄は設けない」を理由なく破って入力欄を 2 つ置いた。計画レビュー 2 巡・実装レビュー 2 巡の
> いずれも検出できなかった（`artifact/<os>/<feature>/designs/` の先行計画を参照に含めていなかったため）。
> **方針をこのファイルに置いたのはそのため。**

---

## JSON によるデータ転送

- 複雑な構造（通知コンテンツ等）はネイティブとの境界を JSON 文字列で渡す
- `XxxJsonBuilder` クラスで JSON 組み立てを担い、Manager から呼び出す
- JSON のスキーマ変更はネイティブ側との整合を必ず確認する

---

## テスト（TDD）

### 基本方針

- 新機能は ExampleController 単位で手動確認項目を定義する
- コールバックの dispatch 順序・結果型の不変条件は、**ネイティブ呼び出しから分離した
  `internal static` の純粋関数**として切り出し、EditMode テストで検証する
  - Manager の `Awake` / `Initialize` は `AndroidJavaObject` / `DllImport` に触れるため、
    **Manager インスタンスを生成するテストは EditMode では書かない**
  - Manager 全体の初期化・イベント購読を通す検証は PlayMode 以降で行う（詳細は `./testing.md`）
- プラットフォーム依存の動作（実機での表示確認等）は手動確認項目として明記する

### テスト層とツール選定

テストの層モデル（EditMode / PlayMode / OS 境界）とプラットフォーム別のツール選定は
**`./testing.md` を正本とする**。この節では重複して定義しない。

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
