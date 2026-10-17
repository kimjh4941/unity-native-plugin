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

### Android（native-toolkit の C ABI）

Android は 2.0.0 から native-toolkit の C ABI（`libntk.so`。ヘッダーは `native-toolkit/dist/<版>/android/include/NativeToolkitC/`、
AAR は `android-native-toolkit-<版>.aar` と `android-native-toolkit-capi-<版>.aar`）を P/Invoke で呼ぶ。
以下は Dialog を移す設計（`artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`）で決めた約束ごとで、ほかの Android の機能も従う。
**Windows の C ABI の形をそのまま写さない**（`artifact/topics/android-c-abi-2/README.md` 3 章）。違うところは下の各項に書く。
Share も移した（`artifact/android/share/designs/2026-10-11-android-share-design-v6.md`。2026-10-12）。Share が従わない約束ごとは、下の「Share の例外」に書く。
Clipboard も移した（`artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md`。2026-10-12）。Clipboard の形は、下の「Clipboard の形」に書く。
Notification も移した（`artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md`。2026-10-12）。これで 4 機能すべてが C ABI を呼ぶ。Notification の形は、下の「Notification の形」に書く。1.x のブリッジ（`AndroidJavaObject` / `AndroidJavaProxy`、`android.unity.*`）を呼ぶコードはもう無い。旧ブリッジの AAR（`android-native-toolkit-1.3.0.aar`、`unity-android-native-toolkit-1.3.0.aar`）は段 4 で外した（2026-10-17）。

**ファイルの置き場所**

| ファイル | 持つもの |
|---|---|
| `Runtime/Android/Common/AndroidNativeToolkitCApi.cs` | ライブラリ名の定数（`"ntk"`）、版と初期化の確認、`Common.h` の関数と `ntk_android_is_initialized` の `extern`、UTF-8 の読み書き（NULL と空を分ける `ReadUtf8OrNull` を含む）、文字列の配列（`AllocStringArray`。`const char* const*`） |
| `Runtime/Android/Common/AndroidNativeCallbackQueue.cs` | C ABI の完了とイベントを UnityMain に運ぶ取り出し口（下の「コールバック」） |
| `Runtime/Android/<Feature>/Android<Feature>CApi.cs`（Bridge） | その機能の構造体・`extern`・delegate の型と、`#if` の外の純粋な変換の関数 |
| `Runtime/Android/<Feature>/Android<Feature>Manager.cs` | 公開 API、検査、完了の受け口（`[MonoPInvokeCallback]`）、イベント |

**`DllImport`**: `[DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern`。
型は Windows と同じ（ポインタは `IntPtr`、`size_t` は `UIntPtr`、`int32_t` は `int`）。ただし**要求の ID（`uint64_t`）は `ulong`**（Windows は 32 ビット）。
構造体は `[StructLayout(LayoutKind.Sequential, Pack = 8)]`、大きさと全フィールドの位置を層 1 で `StructLayoutTest.cpp` の値に固定する。
同名でも Windows と引数・値・配置が違うものがあるので、Windows の宣言や列挙を流用しない。

**コールバック**（Windows と違う）

- 完了・イベント・受け付けた後の `release` は **Android の main（UI スレッド）**で来る。UnityMain ではない。UnityMain から呼ぶと、関数が戻る前に完了が来うる
- 完了と呼び出しの対応は `user_data` で付ける（出力の要求の ID は、書かれる前に完了が来うるので使わない）。呼び出しごとの状態が無い機能（Dialog）は種類の番号を、ある機能は登録ごとの値を渡す
- **コールバックが受け取ったハンドルは所有権ごと渡される**。受け口の中で managed の値に写し、対応する `_free` を `finally` で呼ぶ（Windows は借りるだけ）
- 受け口は Manager の static メソッドで、delegate は `static readonly` に保つ。中では写して `_free` し、`AndroidNativeCallbackQueue.Post(() => Instance.Raise(...))` に入れるだけにする。Manager の状態にも Unity のオブジェクトにも触らない（`Debug.Log*` は可）。全体を `try/catch` で囲む（C に例外が抜けるとプロセスが終わる）
- **`UnityMainThreadDispatcher` は使わない**（ロックを持ったまま購読者を走らせ、Android の main を待たせる。`artifact/topics/dispatcher-runs-under-lock/README.md`）。取り出し口は Manager の `Awake` と、ライブラリを呼ぶたび（どちらも UnityMain）に `AndroidNativeCallbackQueue.EnsureCreated()` で確かめる（利用者のコードが壊したり無効にしたりしても、次の呼び出しで戻る。戻るまでの完了は列に残り、`LogError` が出る）
- **どのスレッドでも、完了も `release` も待たない**（UnityMain と Android の main が互いに待って止まる）

**入力**: 文字列は NUL 終端の UTF-8（共通部の `AllocUtf8`）で、確保したものは 1 つの一覧で追い `finally` で解放する。ライブラリは呼び出しの間に写すので、戻ったらすぐ解放してよい。
途中の U+0000 で切れる。NULL の要素を含む配列は C ABI が入口で拒むので、Manager が先に検査する。

**版と対応の範囲**: 最初の呼び出しの前に `AndroidNativeToolkitCApi.EnsureNativeAvailable()`（major が 2、`DllNotFoundException` などは「使えない」）を確かめる。
対応するのは **IL2CPP と ARM64（と x86_64）だけ**。32 ビットを含むビルドは `Editor/Android/AndroidArchitectureCheck.cs` が Gradle の前に止める。

**Editor**: `DllImport` と受け口は `UNITY_ANDROID && !UNITY_EDITOR`。Editor では、その機能の 1.x の契約（Dialog なら検査の失敗だけをイベントで返し、通れば `LogWarning`）を保つ。

**Share の例外**（Share 設計 v6 の J-S1〜J-S16。1.x の per-call の時点を保つため）

- **呼び出しごとの `user_data`**（J-S1）: 完了の文言にパスや ID を入れるので、呼び出しの記録（`ShareCall`）を static の表に連番のキーで入れ、キーを `user_data` にする。表に入れるのは `extern` を呼ぶ直前、消すのは `release` の受け口と、`extern` に入れなかった例外のときだけ。完了の受け口は表を読むだけ（完了の直後に同じ main のメッセージで `release` が来る）
- **受け口が per-call の枠を取り出す**（J-S5、J-S14）: 「受け口は写して積むだけ」の例外。完了の受け口（Android の main）が、ロックの中で操作ごとの per-call の枠を取り出し（`shareWithCallback` の失敗なら `onSelected` も消し）、取り出した callback を閉じ込めて `Post` する。ロックの中では辞書の読み書きだけ
- **選ばれたアプリは照らさない**（J-S12）: ネイティブが今の待ちの選択だけを届けるので、C# は要求の ID と照らさない
- **結果は、取り出した時点で生きている Manager に出し、いなければ捨てる**（J-S4）: Dialog の「`Instance` を作って出す」と違う（終了の間に GameObject を作り直さない）。例外の例外として、Chooser Action は受け口の時点の Manager の coordinator に閉じ込める。受けた後に Manager を壊しても、その購読者に 1 回出る（1.x の proxy と同じ。持ち越し K-1）
- **結果はいつも呼び出しの後**（J-S2）: 検査の失敗も Editor の失敗も取り出し口を通す（Dialog は呼び出しの中で出す）
- **どのスレッドから呼んでもよい**（J-S16）: 1.x は別のスレッドからの呼び出しを受け付けなかった（UI テスト計画 v6。既知の差分）。ただし `Instance` の最初の取得は GameObject を作るので、UnityMain で行う（公開の XML コメントに書く）
- **ログ**（J-S10）: 本文と `subject` は長さ、アイコンはバイト数だけを出す。題名・MIME・パス・ID・ラベル・カテゴリ・選ばれたアプリのパッケージ名・押されたアクションの ID はそのまま出す（1.x もライブラリも出していた）

**Clipboard の形**（Clipboard 設計 v4 の J-C1〜J-C17）

- **同期の C を呼び、結果は後で出す**（J-C1、J-C2）: copy・clear・stopObserving の C の関数は呼び出しスレッドで同期に動く。公開 API は非同期の形のまま、戻り値から作った結果を、その呼び出しの per-call を閉じ込めて取り出し口に `Post` する（呼び出しの中ではイベントを出さない）。per-call の枠（辞書）は持たない
- **結果と変更は、取り出した時点で生きている Manager に出し、いなければ捨てる**（J-C3。Share の J-S4 と同じ）
- **C# は入力を検査しない**（J-C8）: 空の HTML や URI のスキームは、1.x と同じ Kotlin のコードが判定する。C# はエラー値を 1.x の文言とコードに写すだけ。**文字列の null は検査の失敗ではなく `""` として渡す**（J-C7。文字列の配列の null の要素も。上の「NULL の要素を含む配列は Manager が先に検査する」の Clipboard 版）。`payload` と配列そのものの null は `ArgumentNullException`
- **変更のリスナーはプロセスで 1 回だけ登録し、外さない**（J-C9）。監視はプロセスで共有されるので、C# の印（Unity が `StartObserving` を呼んだか）が偽の間の変更は受け口で捨てる（J-C10）。Manager の破棄で監視を止めるのは、その Manager が生きていて、印が真のときだけ（J-C12）
- **開始と停止は監視専用の `lock` で直列にする**（実装のレビュー v2）: 印と F-9 / F-10 を 1 つのまとまりにする（F-9 / F-10 は積むだけで待たないので `lock` の中で呼べる）。止めるたびに世代を数え、止める前に積まれた変更は配送の前に再開しても出さない。受け口は `lock` を取らない
- **監視の開始は後で効く**（J-C11）: `StartObserving` の直後のコピーの変更は届かないことがある（親課題 J-11 の例外。利用者の判断）
- **どのスレッドから呼んでもよい**（J-C13）。`Instance` の最初の取得は UnityMain で行う

**Notification の形**（Notification 設計 v4 の J-N1〜J-N21）

- **公開 API の JSON は C# で読む**（J-N4、J-N17）: 汎用のリーダー（`AndroidNotificationJsonReader`）と、1.x の Kotlin のパーサーの規則（必須のキーとその順、既定値、空白は無し、捨てる要素）で中間の値を作るマッピング（`AndroidNotificationJsonMapping`）。値の型が違うものは「無い」。中間の値からビルダー（`ntk_notification_content_*` / `_channel_*`）を呼ぶのは Bridge の `BuildContent` / `BuildChannel`。チャンネルのハンドルは `set_channel` の直後に解放し、内容のハンドルは呼ぶ側の `finally` で解放する
- **1.x の補正のうち、JSON の段で決まるものを保つ**（J-N5）: importance・visibility の丸め、priority と progress の丸め、groupName の既定、messaging の `"You"` と今の時刻など。保たないもの（引けないリソースの名前、範囲の外の値）は失敗として返す（J-N6。利用者の判断）
- **同期の C を呼び、結果は後で出す**（J-N1）: Clipboard と同じ。設定の画面だけは非同期で、完了の受け口から `Post` する（J-N9）。per-call は無い
- **権限が無い・アプリの通知がオフの表示は失敗**（J-N7。親課題 J-11 の例外。利用者の判断）
- **正確な予約が許されなければ、1 回だけ不正確で呼び直す**（J-N8）。判断は純粋な `ScheduleWithFallback`
- **リスナーは 2 種類（操作のイベントと shown）を、種類ごとの印でプロセスで 1 回だけ登録し、外さない**（J-N12）。失敗した側だけをやり直す。最初の登録で、それまで保たれていたタップ（アプリが起動する前を含む）が届くので、`Instance` を最初に取るフレームで購読する
- **`RequestPermission` は Unity の権限 API のまま**（J-N10）。ほかのメソッドはどのスレッドから呼んでもよい（J-N15）
- **ログ**: JSON をそのまま出さず、ID、題名と本文の長さ、style の種類、アクションと data の件数、tag の有無だけ。ビルダーが拒んだときは、拒んだ setter の名前を出す。**`INVALID_PARAMETER` は `LogWarning`**（設計 4.5）: ビルダーの範囲の検査が、利用者が JSON に書いた値に返すため。下の一般則（入口の `INVALID_PARAMETER` は `LogError`）の例外。正しい実装で起きない `NOT_SUPPORTED` だけを `LogError` にする

**ログ**: 結果として返す失敗は `LogWarning`（Windows と同じ）。`LogError` は、正しい実装では起きないもの（入口の `INVALID_PARAMETER` / `NOT_SUPPORTED`、捕まえた例外）だけ。入力・利用者名・パスワードなどの値は長さだけを出す。

**振る舞いの差分**: 1.x から移すときは、公開 API で変わる振る舞いを設計書の「既知の差分」の表に書き、XML コメントと実装結果ファイルを通して移行ガイドへ引き継ぐ。

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
  - 例外: Android の Dialog は、Back や外側のタップで閉じたとき `("Cancel", …, true, "")` を返す（1.x から続く公開の振る舞い。C ABI 2.0.0 への移行でも保った。`AndroidDialogManager` の XML コメントに書いてある）

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
- **Windows と Android は移行済み**（`Runtime/Windows/Clipboard/`、`Runtime/Android/Dialog/` など。Android は 2026-10-11 に 4 機能の型をまとめて移した）。**iOS / macOS は `Runtime/<Feature>/`（名前空間 `JonghyunKim.NativeToolkit.Runtime.<Feature>`）のまま**で、それぞれの OS の対応のときに移す。移すまでは、その OS の新しいファイルも今の場所に置く（1 つの OS の中で形を混ぜない）
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

そのプラットフォームの**全機能が使うネイティブライブラリの共通部**だけを置ける。ネイティブの完了を UnityMain に運ぶ仕組み（Android の `AndroidNativeCallbackQueue`）もこれに含める。ファイル名には接頭辞を付ける。機能の中身は置かない。

| ファイル | 位置づけ |
|---|---|
| `Runtime/Windows/Common/WindowsNativeToolkitCApi.cs` | Windows の全機能が使う native-toolkit の C ABI の共通部（DLL 名、版の確認、`Common.h` の関数、UTF-8 の読み書き。「Unity Bridge パターン」の Windows） |
| `Runtime/Android/Common/AndroidNativeToolkitCApi.cs` | Android の全機能が使う native-toolkit の C ABI の共通部（ライブラリ名、版と初期化の確認、`Common.h` の関数、UTF-8 の読み書き。「Unity Bridge パターン」の Android） |
| `Runtime/Android/Common/AndroidNativeCallbackQueue.cs` | Android の全機能が、C ABI の完了とイベントを UnityMain に運ぶのに使う取り出し口 |

**共通化しない理由との関係:** 共通化を避けるのは、片方のプラットフォームの都合がもう片方に及ぶのを防ぐため（上）。
同じプラットフォームの機能どうしは**同じネイティブライブラリの同じ版を必ず一緒に使う**ので、この理由が当てはまらない。
逆に機能ごとに持つと、ライブラリの版を上げるたびに機能の数だけ DLL 名などの書き換えが要り、1 か所でも漏れるとその機能だけ読み込みに失敗する。
置いてよいのは「そのライブラリを使うどの機能にも同じ形で要るもの」に限る。1 つの機能だけが使うものは、その機能の Bridge に置く。

**`UI/` のディレクトリ構成**

`UI/` は `UI/<Platform>/<Feature>/` の構造を採る（Runtime の正しい形と同じ順）。ファイル名の接頭辞は同じ規則に従う。

**既知の逸脱（新規実装で真似しない）: Runtime 11 件**

| 逸脱している型 | 実際の所属 |
|---|---|
| `Android/Clipboard/ClipboardOperationResult` / `ClipboardReadResult`（`ClipItem` / `ClipContents` を含む） / `ClipboardDescriptionResult` | Android |
| `Android/Notification/NotificationResult` / `NotificationActionResult` / `NotificationReceivedResult` | Android |
| `Android/Share/ShareOperationResult` / `ShareCallbackResult` / `ShareChooserActionResult` / `ShareChooserActionCallbackCoordinator` | Android |
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
  - Android の例外: Share・Clipboard の Manager は Payload を C ABI の構造体に移して渡し、JSON ビルダーを持たない（1.x の Share・Clipboard の JSON ビルダーは段 4 で消した。2026-10-17）。`AndroidNotificationJsonBuilder` は、利用者が Manager に渡す JSON を組むための道具で、Manager からは呼ばない
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
