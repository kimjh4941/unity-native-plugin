# Android Dialog 実装計画 v2（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Dialog（Android） |
| 種別 | **移行。機能追加ではない。** 1.x の Unity 向けブリッジ（`android.unity.dialog.UnityAndroidDialogManager`、`AndroidJavaObject` / `AndroidJavaProxy`）から、C ABI 2.0.0（`libntk.so`、`Dialog.h`）の P/Invoke に置き換える |
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（4 機能の目次。決定 J-1〜J-7、Windows と違うところ A-1〜A-6） |
| 移行前の基準 | `48d7927`（親課題の「移行前の基準」）。Dialog の自動テストは無い。旧の振る舞いは 2 章の表がすべて |
| 方針 | **公開 API（`Instance`、`Initialize()`、メソッド 6 本、イベント 6 本、引数、既定値、値の意味、エラーの文言）を保つ。** 変わるのは名前空間（J-4、J-D11）と、5.3「既知の差分」に列挙したものだけ |
| AAR | `android-native-toolkit-2.0.0.aar` と `android-native-toolkit-capi-2.0.0.aar`（dist 1.13.0）。親課題のスパイクで `Plugins/Android/VERSION.txt` の主のピンに入れた。旧ブリッジは移行の間 `extra_*` に残す |
| 出典 | native-toolkit `dist/1.13.0/android/include/NativeToolkitC/Dialog.h`、`android/android_library_capi/src/main/cpp/src/Dialog/Dialog.cpp`（以下 `D.cpp`）、`.../capi/jni/DialogBridge.kt`（以下 `Bridge`）、`android/android_library/.../dialog/AndroidDialogFragment.kt`（以下 `Frag`）。旧ブリッジは native-toolkit のタグ `1.12.0` の `android/unity_android_plugin/.../dialog/UnityAndroidDialogManager.kt`（以下 `OldBridge`）と `android/android_library/.../dialog/AndroidDialogFragment.kt`（以下 `OldFrag`）。C ABI の約束は native-toolkit の `artifact/topics/android-c-abi/designs/` の第 1 部（`2026-10-04-...-c-abi-design.md`）と第 2 部（`2026-10-05-...-part2.md`） |
| UI の補助 | `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v2.md` |
| レビュー | `artifact/android/dialog/reviews/2026-10-11-android-dialog-design-review-v1.md`（サブエージェントと Codex） |

## v1 からの主な変更

レビュー v1 の A1 5 件（H-1〜H-5）と、A2・B・C、Codex の 9 件を反映した。

- H-1: Android の 4 機能の型（21 ファイル）を、実装の前の 1 コミットでまとめて `Runtime/Android/<Feature>/` に移す（J-D11）。1 つの OS の中で形を混ぜない。`common.md` の `Runtime/<Platform>/Common/` の表に Android の行を足す
- H-2: 公開の `Instance` と `Initialize()` を公開 API に数え、`Initialize()` を残す（J-D10）
- H-3: 項目の `null` の要素を C# で同期の失敗にする。U+0000 で切れることを既知の差分に（J-D12）
- H-4: 完了の受け口は、Manager のロックの無い列（`ConcurrentQueue`）に入れるだけにし、`UnityMainThreadDispatcher` を使わない（J-D2）。dispatcher がロックを持ったまま処理を走らせる件は、別の課題に切り出した（`artifact/topics/dispatcher-runs-under-lock/README.md`）
- H-5: 5.3 に `CANCELED_BY_SYSTEM` の行。旧の失敗の時点を「`Show*` が戻った後、dispatcher 経由」に直した。サロゲートの行を全ての文字列に広げた
- M-1 / Codex 1: 文字列の読み取りは NULL を `null` にする（4.1、4.3）
- M-2: ログの段階を表にした（J-D13）
- M-3: テストのガード（5.1）
- M-6〜M-12、Codex 2: テストの書き直し（7 章）。層 2a を足し、別のスレッドからの呼び出しのテストをやめ、公開 API は UnityMain から呼ぶ約束にした（J-D14）
- M-11: 入力・利用者名・パスワードは長さだけをログに出す
- L-1〜L-7: 変更ファイルの一覧、受け口の置き場所（Manager）、OP-19、種類の番号（1〜6）、`isSuccessful == true` で `errorMessage` が `""` になる例外の明記
- UI の補助（M-4、M-5、Codex 3〜9）は補助の設計書 v2 で直した

## 1. native-toolkit 側（2.0.0 の C ABI）

### 1.1 関数

| # | 関数 | 旧（`OldBridge`） | 備考 |
|---|---|---|---|
| OP-13 | `ntk_dialog_show_alert_async(const ntk_dialog_alert_request*, ntk_dialog_result_fn, void* user_data, ntk_release_fn, uint64_t* out_request_id)` | `showDialog` | 第 2 部の設計 8.3 の DL-08 |
| OP-14 | `ntk_dialog_show_confirm_async(...)` | `showConfirmDialog` | DL-09 |
| OP-15 | `ntk_dialog_show_single_choice_async(...)` | `showSingleChoiceItemDialog` | DL-10 |
| OP-16 | `ntk_dialog_show_multi_choice_async(...)` | `showMultiChoiceItemDialog` | DL-11 |
| OP-17 | `ntk_dialog_show_text_input_async(...)` | `showTextInputDialog` | DL-12 |
| OP-18 | `ntk_dialog_show_login_async(...)` | `showLoginDialog` | DL-13 |
| OP-19 | `ntk_dialog_cancel(uint64_t request_id)` | 無い | 公開 API には出さない。層 2b のテストが、UI を操作せずに完了を起こすために使う（7.3） |
| - | 結果の読み取り: `ntk_dialog_result_answer` / `_button` / `_button_text` / `_checked_index` / `_checked_count` / `_checked_at` / `_text` / `_username` / `_password` / `_free` | - | 文字列は `const char*` と `size_t* out_size`。`_free` するまで有効。値が無いときは NULL、空の文字列は長さ 0 の非 NULL（`D.cpp`:471-474） |

旧ブリッジの `getInstance` と `set*Listener` 6 本（DL-01〜DL-07）は C ABI に対応が無い（要求ごとの完了のコールバックに変わる）。
`Common.h` と `ntk_android_is_initialized` は共通部 `Runtime/Android/Common/AndroidNativeToolkitCApi.cs`（親課題の「スパイク」）を使い、Dialog の Bridge では宣言しない。

### 1.2 型と値

- `ntk_dialog_error`: `NONE`(0) / `INVALID_PARAMETER`(1) / `NOT_INITIALIZED`(2) / `NOT_SUPPORTED`(3) / `UNKNOWN`(4) / `OUT_OF_MEMORY`(5) / `CANCELED`(6) / `CANCELED_BY_SYSTEM`(7) / `NOT_FOREGROUND`(8) / `HOST_START_FAILED`(9) / `SHOW_FAILED`(10)。**Windows の `ntk_dialog_error` と値が違う**（Windows は `CANCELED` が 2。親課題 A-6）
- `ntk_dialog_answer`: `BUTTON`(0) / `DISMISSED`(1)。`ntk_dialog_button`: `POSITIVE`(0) / `NEGATIVE`(1)
- **`DISMISSED` のとき `ntk_dialog_result_button` は 0（`POSITIVE`）を返す**（`Bridge`:199）。`answer` を先に読み、`DISMISSED` なら `button` を読まない
- 完了のコールバック: `void (*)(void* user_data, uint64_t request_id, int32_t error, uint32_t system_code, ntk_dialog_result* result)`。`result` は `error == NONE` のときだけ非 NULL で、**所有権ごと渡される**（受け手が `ntk_dialog_result_free`。第 1 部の設計 5.9）。Windows の「コールバックの間だけ借りる」とは逆（親課題 A-3）
- `NTK_CALL` は Android では空（呼び出し規約は既定の cdecl）

### 1.3 入口で拒まれるもの（同期）と、完了で来るもの（非同期）

入口（`D.cpp`:239-254 ほか）。この順で調べ、拒んだら完了は来ず、`release` が呼び出しスレッドで戻る前に呼ばれる（`D.cpp`:124-129、第 1 部の設計 1.3）:

1. `*out_request_id = 0`
2. `callback == NULL` → `INVALID_PARAMETER`
3. `request == NULL`、`struct_size < sizeof`、`> 4096`、`reserved0 != 0` → `INVALID_PARAMETER`。`sizeof` より後ろに 0 でないバイト → `NOT_SUPPORTED`
4. 文字列: NULL か厳密な UTF-8。**空の題名・本文は拒まない**
5. 機能ごと: 単一選択は `reserved1 != 0`、`items` が NULL、`item_count == 0`、要素が NULL、`checked_index < -1` か `>= item_count`。複数選択は `items` の同じ検査（`checked` の長さは検査できない。NULL か `item_count` 個の `int32_t`）。入力とログインは `reserved1 != 0`
6. 未初期化 → `NOT_INITIALIZED`（引数の誤りが先に効く。第 1 部の設計 1.4）
7. 受け付けの失敗 → `OUT_OF_MEMORY` / `UNKNOWN`

完了で来る失敗（`result` は NULL、`system_code` は 0）:

| エラー | 起きる場面 |
|---|---|
| `NOT_FOREGROUND` | main に積んだ処理の時点でアプリが前面にない。前面の Activity が無い、状態を保存済み。透明な宿主が 5 秒で作られない。`showNow` が `IllegalStateException` |
| `HOST_START_FAILED` | 前面の Activity が `FragmentActivity` でなく、透明な宿主 `NtkHostActivity` を起動できない |
| `SHOW_FAILED` | `showNow` がそのほかの例外 |
| `CANCELED_BY_SYSTEM` | 構成の変更以外で fragment が壊れた（宿主や Activity が終わった）（`Frag`:441-452） |
| `CANCELED` | `ntk_dialog_cancel` |
| `NOT_INITIALIZED` | Kotlin の側が未初期化と答えた（`Bridge`:231）。第 2 部の設計 11.1 は「起きない」とする |
| `INVALID_PARAMETER` / `OUT_OF_MEMORY` / `UNKNOWN` | Kotlin の側の例外、結果を組めない。入口の検査と重なるので、ふつうは起きない |

### 1.4 スレッド、寿命、入力

- どの関数もどのスレッドから呼んでよく、待たない。**完了と `release` は Android の main（UI スレッド）で、この順に来る**。UnityMain ではない（親課題 A-5）
- main 以外から呼ぶと、**関数が戻る前に、`*out_request_id` が書かれる前に完了が来うる**（第 1 部の設計 1.3）。UnityMain は main ではないので、ふつうの呼び方でこれが起きる
- 受け付けた呼び出しにつき完了はちょうど 1 回。プロセスが終わるときは完了も `release` も来ない
- `release` は NULL でよい。`user_data` はライブラリが参照しない（コールバックに渡すだけ）ので、ただの整数でよい
- 入力の文字列・配列・構造体は**呼び出しの間だけ**有効であればよい。ライブラリは戻る前に Java の配列へ写す（`D.cpp`:176-196）
- NULL の文字列はライブラリの既定値になる（`title` / `message` / `hint` は `""`、ボタンは "OK" など）。`""` は `""` のまま（ボタンの文字は空になる）
- 文字列は NUL 終端なので、途中の U+0000 で切れる。Kotlin から C へ戻す文字列は、U+0000 と対になっていないサロゲートを U+FFFD にする（第 1 部の設計 5.10）
- コールバックから例外が C に抜けるとプロセスが終わる（第 1 部の設計 5.11）。main を長く塞がない（第 1 部の設計 1.3.2）

### 1.5 画面

前面が `FragmentActivity`（Unity の GameActivity はこれ）なら、そこに androidx の `DialogFragment`（`com.jonghyunkim.nativetoolkit.dialog.AndroidDialogFragment`）を出す。違えば透明な宿主 `NtkHostActivity` を起動して出す。
ダイアログは**フレームワークの `android.app.AlertDialog`**。ボタンは `android:id/button1`（肯定）と `android:id/button2`（否定）、題名は `android:id/alertTitle`、本文は `android:id/message`。
入力欄（`EditText`）には ID が無い（`View.generateViewId()`）。テキスト入力は 1 つ、ログインは上から利用者名・パスワード。肯定ボタンは、空を許さない設定では文字を入れるまで無効（`Frag`:354-398）。
Unity の Activity と透明な宿主は `configChanges` に `orientation|screenSize` を持つので、回転で作り直されない。

## 2. 既存の C# 実装

- `Runtime/Dialog/AndroidDialogManager.cs`（613 行）: `MonoBehaviour` Singleton（`DontDestroyOnLoad`）。ガードは `#if UNITY_ANDROID` だけ。`Awake` で `UnityMainThreadDispatcher.Instance` を作り、公開の `Initialize()` でブリッジを取って 6 つの `AndroidJavaProxy` を登録する
- 公開: `static Instance`、`Initialize()`、メソッド 6 本（戻り値なし）、種類ごとのイベント 6 本。per-call callback は無い
- 公開イベント（**移行後もこのまま**）:

| イベント | 引数 |
|---|---|
| `DialogResult` / `ConfirmDialogResult` | `(string? buttonText, bool isSuccessful, string? errorMessage)` |
| `SingleChoiceItemDialogResult` | `(string? buttonText, int? checkedItem, bool isSuccessful, string? errorMessage)`。旧の -1 は `null` |
| `MultiChoiceItemDialogResult` | `(string? buttonText, bool[]? checkedItems, bool isSuccessful, string? errorMessage)` |
| `TextInputDialogResult` | `(string? buttonText, string? inputText, bool isSuccessful, string? errorMessage)` |
| `LoginDialogResult` | `(string? buttonText, string? username, string? password, bool isSuccessful, string? errorMessage)` |

旧の値（`OldFrag` と `OldBridge`。C# は単一選択の -1 → `null` のほかはそのまま渡す）:

| ダイアログ | 肯定 | 否定 | Back・外側のタップ（`onCancel`） |
|---|---|---|---|
| Dialog | `(buttonText, true, null)`（`OldFrag`:217-219） | 無い | `("Cancel", true, "")`（`OldFrag`:376-377） |
| Confirm | `(positive, true, null)` | `(negative, true, null)` | `("Cancel", true, "")` |
| SingleChoice | `(positive, 最後に押した添字か初期値, true, null)` | `(negative, null, true, null)` | `("Cancel", 今の選択, true, "")`（`OldFrag`:383） |
| MultiChoice | `(positive, 今の状態の配列, true, null)` | `(negative, null, true, null)` | `("Cancel", 今の状態の配列, true, "")`（`OldFrag`:386） |
| TextInput | `(positive, 入力, true, null)` | `(negative, null, true, null)` | `("Cancel", "", true, "")` |
| Login | `(positive, 利用者名, パスワード, true, null)` | `(negative, null, null, true, null)` | `("Cancel", "", "", true, "")` |

`buttonText` は渡したラベル（`null` なら C# の既定値）。閉じたときの `"Cancel"` は否定のラベルではなく固定の文字列。
閉じたときの `errorMessage = ""` は、`common.md` の「`isSuccess == true` なら `errorMessage == null`」の例外で、旧から続く互換の値として保つ。

旧の失敗（付随の値はすべて `null`）:

| 場面 | `errorMessage` | 出る時点 |
|---|---|---|
| 題名が空（6 本） | `"Title cannot be null or empty."` | C# の検査。`Show*` の中で同期に。`LogError` |
| 本文が空（Dialog / Confirm / TextInput / Login） | `"Message cannot be null or empty."` | 同上 |
| 項目が空 | `"singleChoiceItems cannot be null or empty."` / `"multiChoiceItems cannot be null or empty."` | 同上 |
| ブリッジが取れていない | `"pluginInstance is null"` | 同上 |
| `AndroidJavaObject.Call` の例外 | `$"Internal error: {ex.Message}"` | 同上 |
| Activity が `FragmentActivity` でない | `"Failed to cast context to FragmentActivity"` | ブリッジ。リスナー → `AndroidJavaProxy` → dispatcher なので、`Show*` が戻った後（同じか次のフレームの `Update`） |
| 表示の例外（後ろからの呼び出しの `IllegalStateException` を含む） | `"Failed to showDialog: <例外の文言>"`（`showConfirmDialog` など、ブリッジのメソッドの名前） | 同上 |
| 宿主や Activity が壊れた | **イベントは出ない**（`OldFrag` に `onDestroy` の報告が無い） | - |

- Editor など Android 以外: 検査に通れば `LogWarning("ShowDialog can only be called on an Android device.")` を出し、イベントは出さない
- 誰も調べていなかったもの: 単一選択の `checkedItem` の範囲、複数選択の `checkedItems` の長さ（`null` か空なら項目数の `false` に置き換えるだけ）、項目の `null` の要素。短い `checkedItems` は `OldFrag`:252/260 の `checkedItems!![which]` で範囲外になり、main で例外になったと推定する（実機で確かめていない）
- 既定値: `ShowDialog` の `buttonText = "OK"`、Confirm の `"No"` / `"Yes"`、SingleChoice の `checkedItem = 0` と `"Cancel"` / `"OK"`、MultiChoice の `"Cancel"` / `"OK"`、TextInput の `hint = ""` と `"Cancel"` / `"OK"`、Login の `"Username"` / `"Password"` / `"Cancel"` / `"Login"`。`enablePositiveButtonWhenEmpty = false`、`cancelableOnTouchOutside = true`、`cancelable = true`。**`null` を明示しても同じ値に置き換える**（`??=`）
- ログ: 呼び出しの引数と結果をすべて `Debug.Log` に出す。**パスワードと入力も出していた**（`AndroidDialogManager.cs`:216）
- 利用者: サンプル `Runtime/UI/Android/Dialog/AndroidDialogManagerExampleController.cs`（呼び出しは `#if UNITY_ANDROID && !UNITY_EDITOR`、`using` は同じガードの中）、`Assets/Scripts/Runtime/UI/Android/Dialog/AndroidDialogManagerTest.cs`（`using` だけ）、`Documentation~/index*.md` の例

## 3. 実装制約

- `agent-rules/coding-rules/common.md`: Manager + Bridge の 2 層、変換は Bridge の `internal static` の純粋な関数で `DllImport` の `#if` の外、`Android` 接頭辞、共通化しない、二重のガード
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、ログのタグ、XML コメント。ログの段階は J-D13
- 親課題 3 章（A-1〜A-6）: Windows の P/Invoke の形を写さない
- ネイティブが非同期なので C# も非同期のまま（今と同じく、イベントで返す）。per-call callback と `Awaitable` 版は足さない（公開 API を変えない）

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

### 4.1 `DllImport` の署名と構造体

- `[DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern`
- 型: ポインタ・ハンドル・関数ポインタは `IntPtr`、`size_t` は `UIntPtr`、`int32_t`（列挙・真偽）は `int`、`uint32_t` は `uint`、**`uint64_t` は `ulong`**（親課題 A-2）
- 構造体は Bridge の入れ子の `internal struct`、`[StructLayout(LayoutKind.Sequential, Pack = 8)]`。`struct_size = (uint)Marshal.SizeOf<T>()`、`reserved*` は 0

| 構造体（C# 名） | 大きさ | フィールドの位置（native-toolkit `android/android_library_capi_test/src/main/cpp/tests/StructLayoutTest.cpp`:25-94） |
|---|---|---|
| `ntk_dialog_alert_request`（`AlertRequest`） | 40 | struct_size 0、reserved0 4、title 8、message 16、button_text 24、not_cancelable 32、not_cancelable_on_touch_outside 36 |
| `ntk_dialog_confirm_request`（`ConfirmRequest`） | 48 | 0、4、title 8、message 16、negative_text 24、positive_text 32、not_cancelable 40、not_cancelable_on_touch_outside 44 |
| `ntk_dialog_single_choice_request`（`SingleChoiceRequest`） | 64 | 0、4、title 8、items 16、item_count 24、checked_index 32、not_cancelable 36、not_cancelable_on_touch_outside 40、reserved1 44、negative_text 48、positive_text 56 |
| `ntk_dialog_multi_choice_request`（`MultiChoiceRequest`） | 64 | 0、4、title 8、items 16、item_count 24、checked 32、negative_text 40、positive_text 48、not_cancelable 56、not_cancelable_on_touch_outside 60 |
| `ntk_dialog_text_input_request`（`TextInputRequest`） | 64 | 0、4、title 8、message 16、hint 24、negative_text 32、positive_text 40、enable_positive_when_empty 48、not_cancelable 52、not_cancelable_on_touch_outside 56、reserved1 60 |
| `ntk_dialog_login_request`（`LoginRequest`） | 72 | 0、4、title 8、message 16、username_hint 24、password_hint 32、negative_text 40、positive_text 48、enable_positive_when_empty 56、not_cancelable 60、not_cancelable_on_touch_outside 64、reserved1 68 |

`extern`（17 本）:

| C | C# |
|---|---|
| `ntk_dialog_show_alert_async` | `int (ref AlertRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId)` |
| `ntk_dialog_show_confirm_async` ほか 4 本 | 同じ形（`ref` の型だけが違う） |
| `ntk_dialog_cancel` | `int (ulong requestId)` |
| `ntk_dialog_result_answer` / `_button` / `_checked_index` | `int (IntPtr result)` |
| `ntk_dialog_result_checked_count` | `UIntPtr (IntPtr result)` |
| `ntk_dialog_result_checked_at` | `int (IntPtr result, UIntPtr index)` |
| `ntk_dialog_result_button_text` / `_text` / `_username` / `_password` | `IntPtr (IntPtr result, out UIntPtr outSize)` |
| `ntk_dialog_result_free` | `void (IntPtr result)` |

- `out_request_id` は公開 API では `IntPtr.Zero`（NULL）で渡す。要求の ID は使わない（J-D1）。層 2b のテストは、取り消しのために ID が要るので、`Marshal.AllocHGlobal(8)` を渡して `Marshal.ReadInt64` で読む（同じ `extern` を使い回す）
- コールバックの delegate の型 `ResultCallback`: `[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ResultCallback(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr result)` は Bridge に置く。**受け口（`[MonoPInvokeCallback(typeof(AndroidDialogCApi.ResultCallback))]` の static メソッド）と、delegate を保つ `static readonly` のフィールド、`Marshal.GetFunctionPointerForDelegate` で 1 回だけ作る関数ポインタは Manager に置く**（受け口が Manager の列に入れるため。Bridge が Manager に依存しない。Windows の前例と同じ）
- `release` は `IntPtr.Zero`（J-D1）
- 文字列を返す読み取りは `IntPtr` で受け、**NULL なら `null`、そうでなければ `AndroidNativeToolkitCApi.ReadUtf8(ptr, ToByteCount(size))`**。共通部に `ReadUtf8OrNull(IntPtr data, UIntPtr size)` を足す（Notification・Share・Clipboard も同じ形で読むので共通部に置く）。`[return: MarshalAs(UnmanagedType.LPUTF8Str)]` は使わない

### 4.2 引数の変換（公開 API は変えない）

1. `null` の置き換えは今のまま（2 章の既定値。C# で行うので、ライブラリの既定値には頼らない）
2. C# の検査は今のまま、今の順で、今の文言で（題名、本文、項目）
3. 加える検査（J-D3、J-D12）: 項目の配列が空でないことの検査の直後に、要素の `null`、単一選択の `checkedItem` の範囲
4. 長さの合わせ方（J-D4）: 複数選択の `checkedItems` の長さが項目数と違うとき、足りない分を `false` で埋め、余る分を捨てて渡す
5. 構造体の組み立て（純粋な関数）:

| C# の引数 | 構造体へ |
|---|---|
| 文字列 | NUL 終端の UTF-8（`AndroidNativeToolkitCApi.AllocUtf8`）。対になっていないサロゲートは U+FFFD。途中の U+0000 で切れる（J-D12） |
| `cancelable` | `not_cancelable = cancelable ? 0 : 1`（**名前の向きが逆**） |
| `cancelableOnTouchOutside` | `not_cancelable_on_touch_outside = cancelableOnTouchOutside ? 0 : 1`（同上） |
| `enablePositiveButtonWhenEmpty` | `enable_positive_when_empty = 1 / 0` |
| 単一選択の `singleChoiceItems` / `checkedItem` | `items`（`const char*` の配列を `AllocHGlobal` に並べる）、`item_count`、`checked_index` |
| 複数選択の `checkedItems` | `checked`（`int32_t` の配列。`true` は 1） |

確保したものは 1 つの一覧で追い、`finally` で全部解放する。ライブラリは戻る前に写すので（1.4）、呼び出しの直後に解放してよい。

### 4.3 結果の変換

完了の受け口（Android の main）で、結果のハンドルから managed の値（`AndroidDialogCApi.Answer`。種類、エラー、答え、ボタンの文字、添字、真偽の配列、文字列 3 つ）を写し、ハンドルを `_free` してから Manager の列に入れる（J-D2）。イベントは UnityMain で出す。

| 完了 | イベントへ |
|---|---|
| `NONE`、`BUTTON` | `buttonText = button_text`（表示したラベル。C# が置き換えた既定値を含む）。単一選択は `checked_index`（-1 は `null`）、複数選択は `checked_at` を `checked_count` 個読んだ `bool[]`（否定のときは `count == 0` なので `null`）、入力とログインは文字列（否定のときは NULL なので **`null`**。長さ 0 の非 NULL は `""`）。`(…, true, null)` |
| `NONE`、`DISMISSED` | 今と同じ `("Cancel", …, true, "")`。入力は `""`、ログインは `""` と `""`。**単一選択は `null`、複数選択は `null`**（選択の状態は来ない。J-D5） |
| 失敗（`NONE` 以外） | `(null, …null, false, 文言)`。文言は 4.4 |

`Answer` から各イベントの引数への変換は純粋な関数にし、層 1 で 6 種類 × 4 通り（肯定・否定・閉じた・失敗）を固める。

### 4.4 エラーの文言とログ

旧のブリッジの形 `"Failed to <method>: <理由>"`（`<method>` は `showDialog` などのブリッジのメソッド名）を保ち、理由を Manager が持つ。

| 原因 | 時点 | `errorMessage` | ログ（J-D13） |
|---|---|---|---|
| C# の検査（題名・本文・項目が空） | 同期 | 今の文言のまま | `LogWarning` |
| 項目の `null` の要素（J-D12） | 同期 | `"singleChoiceItems cannot contain null."` / `"multiChoiceItems cannot contain null."` | `LogWarning` |
| `checkedItem` の範囲外（J-D3） | 同期 | `"checkedItem is out of range."` | `LogWarning` |
| ネイティブが使えない（`DllNotFoundException` などと版。共通部の `NativeUnavailable`） | 同期 | `"Failed to <method>: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only)."` | `LogWarning` |
| 入口の `NOT_INITIALIZED` | 同期 | `"Failed to <method>: the native library is not initialized yet."` | `LogWarning` |
| 入口の `INVALID_PARAMETER` / `NOT_SUPPORTED` | 同期 | `"Failed to <method>: the request was rejected (<名前>)."` | `LogError`（C# の構造体の誤り。正しい実装では起きない） |
| 入口の `OUT_OF_MEMORY` / `UNKNOWN` / 想定外の値 | 同期 | `"Failed to <method>: <名前>."` | `LogWarning` |
| 完了の `NOT_FOREGROUND` | 非同期 | `"Failed to <method>: the app is not in the foreground."` | `LogWarning` |
| 完了の `HOST_START_FAILED` | 非同期 | `"Failed to <method>: the dialog host could not be started."` | `LogWarning` |
| 完了の `SHOW_FAILED` | 非同期 | `"Failed to <method>: the dialog could not be shown."` | `LogWarning` |
| 完了の `CANCELED_BY_SYSTEM` | 非同期 | `"Failed to <method>: the dialog was closed by the system."` | `LogWarning` |
| 完了の `CANCELED` | 非同期 | `"Failed to <method>: the dialog was canceled."`（公開 API では起きない。テストだけ） | `LogWarning` |
| 完了のそのほか | 非同期 | `"Failed to <method>: <名前>."` | `LogWarning` |
| C# の例外（変換・確保の失敗など） | 同期 | `$"Internal error: {ex.Message}"`（今と同じ） | `LogError` |
| 受け口の中の例外 | - | イベントは出ない（その完了は失われる） | `LogError` |

`<名前>` はエラーの列挙の名前（`OUT_OF_MEMORY` など）。名前への変換は純粋な関数にする。
結果のログは、ボタンの文字・添字・真偽の配列は出し、**入力・利用者名・パスワードは長さだけを出す**（`DialogBridge.kt`:208 と同じ考え方。UI の補助が logcat を読むので、値が成果物に残りうる）。

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

**移動（J-D11）。実装の前の 1 コミット。** Android の 4 機能の型を `git mv`（`.meta` ごと。GUID を保つ）で `Runtime/Android/<Feature>/` に移し、名前空間を `JonghyunKim.NativeToolkit.Runtime.Android.<Feature>` にする。名前は変えない（接頭辞の無い 10 個の改名は `artifact/topics/os-prefix-violations/README.md` の範囲で、ここでは行わない）。振る舞いは変えない。

| 機能 | 移すファイル（`Runtime/<Feature>/` から） |
|---|---|
| Clipboard | `AndroidClipboardManager.cs`、`AndroidClipboardPayloads.cs`、`AndroidClipboardJsonBuilder.cs`、`AndroidClipboardJsonParser.cs`、`ClipboardOperationResult.cs`、`ClipboardReadResult.cs`、`ClipboardDescriptionResult.cs` |
| Dialog | `AndroidDialogManager.cs` |
| Notification | `AndroidNotificationManager.cs`、`AndroidNotificationPayloads.cs`、`AndroidNotificationJsonBuilder.cs`、`NotificationResult.cs`、`NotificationActionResult.cs`、`NotificationReceivedResult.cs` |
| Share | `AndroidShareManager.cs`、`AndroidSharePayloads.cs`、`AndroidShareJsonBuilder.cs`、`ShareOperationResult.cs`、`ShareCallbackResult.cs`、`ShareChooserActionResult.cs`、`ShareChooserActionCallbackCoordinator.cs` |

あわせて、これらを使う `using` を直す（Android のサンプルのコントローラー 4 本、`Assets/Scripts/Runtime/UI/Android/*/` のテスト用スクリプト、`Tests/Runtime/Android*`、`Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs`）。移したあと、EditMode と実機の PlayMode が基準と同じに通ることを確かめる。`Documentation~/index*.md` の `using` は段 4（マニュアル）で直す。

**Dialog の実装:**

| 区分 | ファイル | 置き場所 | ガード | 内容 |
|---|---|---|---|---|
| 既存変更 | `AndroidDialogManager.cs` | `Runtime/Android/Dialog/` | `UNITY_ANDROID \|\| UNITY_EDITOR`（今は `UNITY_ANDROID`） | `AndroidJavaObject` / `AndroidJavaProxy` を消し、`AndroidDialogCApi` を呼ぶ。受け口と列（J-D2）、`Update` での取り出し。公開 API・既定値・検査・文言はそのまま。`Initialize()` は J-D10 |
| 新規作成 | `AndroidDialogCApi.cs` | `Runtime/Android/Dialog/` | 型と変換は `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport` は `UNITY_ANDROID && !UNITY_EDITOR` | Bridge。4.1 の構造体・`extern`・delegate の型、入れ子の `Kind`（1〜6）と `Answer`、4.2〜4.4 の純粋な関数（検査、構造体の組み立て、`checked` の長さの合わせ方、`Answer` からイベントの引数、エラーの名前と文言） |
| 既存変更 | `AndroidNativeToolkitCApi.cs` | `Runtime/Android/Common/` | 今のまま | `ReadUtf8OrNull` を足す |
| 新規作成 | `AndroidDialogCApiTests.cs` | `Tests/Runtime/` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（7.1） |
| 新規作成 | `AndroidNativeToolkitCApiTests.cs` | `Tests/Runtime/` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（共通部） |
| 新規作成 | `AndroidDialogManagerEditorTests.cs` | `Tests/PlayMode/` | `UNITY_EDITOR`（Player に入れない） | 層 2a（7.2） |
| 新規作成 | `AndroidDialogPlayerTests.cs` | `Tests/PlayMode/` | 外 `UNITY_ANDROID`、中 `!UNITY_EDITOR`（`AndroidNativeToolkitCApiPlayerTests.cs` と同じ 2 段） | 層 2b（7.3） |
| 新規作成 | `AndroidUiDriver.cs` | `Tests/PlayMode/` | 同上 | UI の補助のテストの側（補助の設計書 v2 の 5 章） |
| 新規作成 | `android_ui_driver.py` | `scripts/` | - | UI の補助（補助の設計書 v2） |
| 既存変更 | `verify_unity_android.sh` | `scripts/` | - | 補助の起動と停止、判定（補助の設計書 v2 の 7 章） |
| 既存変更 | `common.md` | `agent-rules/coding-rules/` | - | 「Unity Bridge パターン > Android」を C ABI の形に書き直す（Windows の節に倣い、親課題 A-1〜A-6 と J-D1・J-D2 の形）。「Runtime のディレクトリと名前空間」の Android を移行済みに。`Runtime/<Platform>/Common/` の表に `AndroidNativeToolkitCApi.cs` の行。既知の逸脱の表の場所の列 |
| 既存変更 | `csharp.md` | `agent-rules/coding-rules/` | - | 結果として返す失敗は `LogWarning` の規則（今は Windows だけ）を Android にも（J-D13） |
| 既存変更 | `testing.md` | `agent-rules/coding-rules/` | - | 2 節の Android の行に adb の方式、3 節のコンパイルガードの群、7 節の適用状況（Dialog の Android の層 1・2a・2b） |
| 非変更 | `Runtime/Resources/UI/Android/Dialog/*`、`NativeToolkitSampleNavigator.cs`、`NativeToolkitEditorWindow.cs` | - | - | コントローラーの型名で参照するだけ。コントローラーの名前空間は変えない |

Manager を `|| UNITY_EDITOR` にするのは、層 2a で Editor の契約（J-D6）を確かめるため。

### 5.2 判断

| # | 内容 | 決定（案） |
|---|---|---|
| J-D1 | 完了と呼び出しの対応付け | `user_data` に**ダイアログの種類**（`Kind`。Alert = 1 〜 Login = 6。0 は使わない）を入れ、`release` は NULL。親課題 A-1 の「登録ごとに別の `user_data`」から外れるのは、Dialog のイベントが種類ごとで呼び出しごとの状態が無く、取り消しも公開しないため（要求ごとの表が要らない）。完了が関数の戻る前に来ても、種類だけで正しいイベントに届く（1.4）。要求の ID は使わない |
| J-D2 | 完了の受け口ですること | 全体を `try/catch` で囲み、結果を `Answer` に写し、`_free` し、**Manager の `static readonly ConcurrentQueue<Answer>` に入れるだけ**。Manager の `Update`（UnityMain）が取り出してイベントを出す。受け口は Unity の API にも Manager のインスタンスにも触らず、待つのは `ConcurrentQueue` の中だけ（ロックを取らない）。`UnityMainThreadDispatcher` は使わない（ロックを持ったまま購読者を走らせるので、Android の main が購読者の終わりまで待たされる。`artifact/topics/dispatcher-runs-under-lock/README.md`）。受け口の例外は `LogError` にして飲む（C に抜けるとプロセスが終わる） |
| J-D3 | 単一選択の `checkedItem` が `-1` 未満か項目数以上 | ネイティブを呼ばず、同期に `(null, null, false, "checkedItem is out of range.")`。C ABI は入口で拒む。旧は検査せずに渡しており、結果は記録が無い（2 章） |
| J-D4 | 複数選択の `checkedItems` の長さが項目数と違う | 足りない分は `false`、余る分は捨てる。C ABI は長さを知らないので、短い配列をそのまま渡すと範囲外を読む |
| J-D5 | 閉じたとき（`DISMISSED`）の選択の状態 | 単一選択も複数選択も `null`。C ABI が返さないので保てない（第 2 部の設計 8.3 の DL-10 / DL-11）。5.3 と移行ガイドに書く |
| J-D6 | Editor（Android 以外） | 今と同じ: 検査に失敗すればイベント、通れば `LogWarning` だけでイベントは出さない。Windows の Dialog（`-5` のイベント）とは揃えない（公開 API の振る舞いを変えない） |
| J-D7 | 失敗の時点 | ネイティブを呼ぶ前と入口で拒まれた失敗は、`Show*` の中で同期にイベントを出す（今の C# の検査と同じ）。受け付けた後の失敗は、完了から Manager の `Update` で出す（`Show*` が戻った後。同じか後のフレーム） |
| J-D8 | 後ろから呼んだとき | `NOT_FOREGROUND` の完了を `"Failed to <method>: the app is not in the foreground."` にする。旧は `"Failed to <method>: <IllegalStateException の文言>"` で、時点はどちらも `Show*` が戻った後。文言だけが変わる（5.3） |
| J-D9 | ネイティブが使えないとき（32 ビット、Mono） | 同期の失敗（4.4）。旧の `"pluginInstance is null"` に当たる。Mono の Player は ARMv7 だけなので、**Mono では Dialog は常にこの失敗になる**（親課題の「スパイク」）。移行ガイドに「IL2CPP と ARM64 が要る」と書く |
| J-D10 | 公開の `Initialize()` | 残す。中身は `AndroidNativeToolkitCApi.EnsureNativeAvailable()` を呼び、使えるか（と初期化済みか）を `Debug.Log` するだけ。`Awake` から今と同じく呼ぶ。何度呼んでもよい。XML コメントに「2.0.0 ではネイティブの準備は要らない。互換のために残す」と書く |
| J-D11 | フォルダと名前空間 | Android の 4 機能の型をまとめて 1 コミットで移す（5.1）。Dialog だけを先に移すと、1 つの OS の中で形が混ざる（`common.md`「Runtime のディレクトリと名前空間」） |
| J-D12 | 項目の `null` の要素と、文字列の U+0000 | 要素の `null` は同期の失敗（4.4）。C ABI が入口で拒むので、呼び出し側の誤りとして C# が先に言う。U+0000 は NUL 終端で切れる（C ABI の形。C# では直さない）。5.3 に書く |
| J-D13 | ログの段階 | 結果として返す失敗は `LogWarning`（`common.md` の Windows の規則を Android にも。今の検査の `LogError` を下げる）。`LogError` は、正しい実装では起きないもの（入口の `INVALID_PARAMETER` / `NOT_SUPPORTED`、C# の例外、受け口の例外）だけ。Unity Test Framework は想定外の `LogError` でテストを落とすので、失敗を期待するテストが書ける |
| J-D14 | 呼ぶスレッド | 公開 API は UnityMain から呼ぶ（XML コメントに書く。今も同じ前提）。同期の失敗のイベントは呼んだスレッドで出るため。UnityMain から呼べば、`Update` が同じスレッドなので、`Show*` が戻る前に完了のイベントが出ることはない |

### 5.3 既知の差分（意図したもの）

XML コメントに書き、実装結果ファイル経由で次の版のマニュアル（移行ガイド）へ引き継ぐ。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 名前空間とフォルダ（J-4、J-D11） | `JonghyunKim.NativeToolkit.Runtime.Dialog`、`Runtime/Dialog/` | `JonghyunKim.NativeToolkit.Runtime.Android.Dialog`、`Runtime/Android/Dialog/`。利用者は `using` を書き換える。Clipboard・Notification・Share の Android の型も同じ |
| 閉じたときの選択（J-D5） | 単一選択は今の選択、複数選択は今の状態 | どちらも `null` |
| 範囲外の `checkedItem`（J-D3） | フレームワークに渡した | 同期の失敗 `"checkedItem is out of range."` |
| 長さの違う `checkedItems`（J-D4） | フレームワークに渡した（短いと main で例外になったと推定） | 項目数に合わせる |
| 項目の `null` の要素（J-D12） | フレームワークに渡した | 同期の失敗 `"... cannot contain null."` |
| 文字列の途中の U+0000（J-D12） | そのまま運んだ | そこで切れる |
| 対になっていないサロゲート | そのまま | 入力（題名・本文・項目・ヒント・ボタン）は C# の UTF-8 の変換で、出力（ボタンの文字・入力・利用者名・パスワード）はライブラリで U+FFFD |
| 後ろから呼ぶ（J-D8） | `"Failed to <method>: <例外の文言>"` | `"Failed to <method>: the app is not in the foreground."` |
| 宿主や Activity が壊れた | イベントは出ない | `(null, …, false, "Failed to <method>: the dialog was closed by the system.")` |
| 前面が `FragmentActivity` でない | `"Failed to cast context to FragmentActivity"` | 透明な宿主に出る（第 2 部の設計 8.3 の DL-08）。Unity の GameActivity は `FragmentActivity` なので、ふつうは起きない |
| ネイティブが使えない（J-D9） | 無い（ブリッジは Java なので 32 ビットでも動いた） | 同期の `"Failed to <method>: the native library is not available ..."`。**Mono（ARMv7）と 32 ビットの ARM では Dialog は使えない** |
| エラーの文言 | 例外の文言を含む | 4.4 の固定の文言 |
| ログ（J-D13） | 検査の失敗は `LogError`、入力とパスワードを出した | 検査の失敗は `LogWarning`、入力とパスワードは長さだけ |
| `Initialize()`（J-D10） | ブリッジを取り、リスナーを登録した | 使えるかをログに出すだけ |

### 5.4 呼び出しの流れと契約（`ShowConfirmDialog` の例）

1. `null` を既定値に置き換え、C# の検査（4.2 の順）。失敗なら `LogWarning` を出し、イベントを同期に出して終わる
2. Android 以外（Editor）は `LogWarning` で終わる（J-D6）
3. `AndroidNativeToolkitCApi.EnsureNativeAvailable()` が `Available` でなければ、4.4 の同期の失敗
4. 確保の一覧を try の外で空に作る。UTF-8 に変換して構造体を組み立てる
5. `ntk_dialog_show_confirm_async(ref request, s_callbackPointer, (IntPtr)Kind.Confirm, IntPtr.Zero, IntPtr.Zero)`。完了はこの呼び出しが戻る前に main で来うるが、受け口は列に入れるだけで、イベントは UnityMain の `Update` で出す（J-D14）
6. `finally` で確保したものを全部解放する
7. 戻り値が `NONE` 以外なら 4.4 の同期の失敗をイベントで出す（catch の外で 1 回）。`NONE` なら何もしない（完了を待たない）

```csharp
string? error = null;
var allocated = new List<IntPtr>();
try
{
    try { /* 4〜5 */ }
    finally { AndroidNativeToolkitCApi.FreeAll(allocated); }  // 6
}
catch (Exception e) { error = $"Internal error: {e.Message}"; }
if (error != null) ConfirmDialogResult?.Invoke(null, false, error);   // 7
```

- 完了の受け口（main、Manager の static メソッド）: `try { answer = AndroidDialogCApi.Read(kind, error, result); } finally { if (result != IntPtr.Zero) ntk_dialog_result_free(result); }` を外側の `try/catch` で囲み、`s_completions.Enqueue(answer)`
- `Update`（UnityMain）: `while (s_completions.TryDequeue(out var answer))` で種類に応じてイベントを 1 回ずつ出す。購読者の例外は捕まえない（今の dispatcher と同じく `Update` から上に出る。残りは次のフレームで出る）
- Manager が壊されているあいだに来た完了は列に残り、次に Manager が作られた（`Instance`）あとの `Update` で出る。旧は dispatcher から `Instance` を呼んで作り直していた。どちらも、完了が捨てられないことは同じ
- **どのスレッドでも完了を待たない**（親課題 A-4）。`Show*` は受け付けたら戻る

### 5.5 実装順

各段階の終わりに `scripts/verify_unity_android.sh`（EditMode と実機の IL2CPP）を流す。`!UNITY_EDITOR` 側は Player のビルドでしかコンパイルされない。

1. 移動のコミット（5.1。21 ファイルと `using`）。振る舞いは変えない。EditMode と実機の PlayMode が基準と同じに通ること
2. `AndroidDialogCApi` の構造体と純粋な関数、共通部の `ReadUtf8OrNull`、層 1 のテスト
3. `AndroidDialogCApi` の `extern`
4. `AndroidDialogManager` の置き換え（J-D1〜J-D14）と層 2a
5. UI の補助と層 2b（補助の設計書 v2 の要検証を最初に確かめる）
6. `--mono`（ARMv7）で J-D9 を確かめる
7. `common.md`、`csharp.md`、`testing.md`
8. 実装結果ファイル（`artifact/android/dialog/results/`）に 5.3 と 4.4 を書き写す

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | 題名・本文・項目が空 | C# | 同期、今の文言、`LogWarning` | 1（検査の純粋な関数）、2a、2b |
| E-2 | `checkedItem` の範囲外（J-D3） | C# | 同期 | 1、2a、2b |
| E-3 | `checkedItems` の長さ違い（J-D4） | C# | 合わせて渡す（失敗にしない） | 1（組み立て）、2b（項目数と同じ長さが返る） |
| E-4 | 項目の `null` の要素（J-D12） | C# | 同期 | 1、2a |
| E-5 | ネイティブが使えない（32 ビット・Mono・版違い） | C# | 同期、4.4 | 1（例外の分類と版）、2b（`--mono`） |
| E-6 | 未初期化 | ネイティブ（入口） | 同期、4.4 | 1（文言）。起こせない（androidx.startup が起動時に済ませる） |
| E-7 | 入口の `INVALID_PARAMETER` / `NOT_SUPPORTED` | ネイティブ（入口） | 同期、4.4 と `LogError` | 1（文言）。正しい実装では起きない。構造体の位置は 1 で固定し、C# の検査で先に拒む |
| E-8 | `NOT_FOREGROUND` | ネイティブ（完了） | 非同期、4.4 | 1（文言）、2b（試す。7.3） |
| E-9 | `HOST_START_FAILED` / `SHOW_FAILED` / `CANCELED_BY_SYSTEM` / 完了の `NOT_INITIALIZED` | ネイティブ（完了） | 非同期、4.4 | 1（文言）。起こせない（`CANCELED_BY_SYSTEM` は Activity を終わらせれば起きるが、Unity の Activity なのでテストが終わる） |
| E-10 | `CANCELED`（取り消し） | ネイティブ（完了） | 非同期、4.4 | 2b（`ntk_dialog_cancel` で完了の経路を通す） |
| E-11 | 閉じた（Back・外側） | ネイティブ（完了） | `("Cancel", true, "")` ほか 4.3 | 1（写し）、2b（Back、外側のタップ） |
| E-12 | C# の例外 | C# | 同期、`"Internal error: ..."`、`LogError` | 1（形はコードレビュー） |
| E-13 | 受け口の中の例外 | C# | イベントは出ない、`LogError` | コードレビュー。起こせない |

## 7. テスト方針

### 7.1 層 1（EditMode）

`AndroidDialogCApiTests`:

- 6 つの構造体の大きさと全フィールドの位置（`Marshal.SizeOf`、`Marshal.OffsetOf`。4.1 の表）
- 要求の ID が 64 ビットであること: `ResultCallback` の 2 番目の引数の型が `ulong`（反射。`extern` は Editor ではコンパイルされないので、delegate の型で見る）
- 構造体の組み立て: `struct_size`、`reserved* = 0`、`not_cancelable` の向き（`cancelable = true` で 0）、`enable_positive_when_empty`、文字列のポインタが NUL 終端の UTF-8 であること（読み戻す）、項目の配列と件数、`checked` の値（J-D4 の短い・長い・`null`・空）
- 検査（4.2）: 題名・本文・項目の空、要素の `null`（J-D12）、`checkedItem` の `-1`・`0`・項目数 - 1 は通り、`-2` と項目数は失敗（J-D3）。文言が今と同じであること
- 結果の写し（4.3）: `Answer` からイベントの引数へ、6 種類 × 肯定・否定・閉じた・失敗。否定の入力が `null`、閉じたときの入力が `""`
- エラーの名前と文言（4.4）: 全値と想定外の値

`AndroidNativeToolkitCApiTests`（共通部）: 版の判定（`0x020000`、`0x020103` は通り、`0x010300`、`0x030000` は通らない）、例外の分類、UTF-8（非 ASCII、サロゲートペア、対になっていないサロゲート、空、バイト長で読む）、`ReadUtf8OrNull` が NULL を `null`、長さ 0 の非 NULL を `""` にすること

### 7.2 層 2a（PlayMode・Editor、`AndroidDialogManagerEditorTests`）

Manager が Editor でもコンパイルされる（5.1）ので、Editor の契約（J-D6）を確かめる。

- 検査の失敗（題名・本文・項目の空、要素の `null`、`checkedItem` の範囲外）で、イベントが `Show*` の中で 1 回だけ、`isSuccessful == false`、今の文言
- 正しい引数では、イベントが出ず `LogWarning` が出る（`LogAssert.Expect(LogType.Warning, ...)`）
- 購読者が例外を投げると呼び出し側に出て、ハンドラーは 1 回だけ呼ばれる

### 7.3 層 2b（実機の Player、`AndroidDialogPlayerTests`）

**64 ビット（IL2CPP）。** `IntPtr.Size == 4` か `EnsureNativeAvailable() != Available` なら `Assert.Ignore`。

- **取り消しで完了の経路を通す**（UI 不要）: 先に `AndroidDialogManager.Instance` を作り、Manager の受け口の関数ポインタと `user_data = Kind.Confirm` で `ntk_dialog_show_confirm_async` を `out_request_id` つきで呼び、`ntk_dialog_cancel` で閉じる。`ConfirmDialogResult` に `"Failed to showConfirmDialog: the dialog was canceled."` が 1 回、UnityMain で来ること
- **戻る前に出ないこと**（J-D14）: UnityMain から `ShowConfirmDialog` を呼び、戻った時点ではイベントが 0 回、補助が肯定を押した後に 1 回
- **UI の補助で押す**（6 種類それぞれ）:
  - 肯定・否定・Back の値が 2 章の表（閉じたときは 5.3 の差分を反映）と同じ。否定の入力・利用者名・パスワードが `null`（`""` ではない）
  - 外側のタップで閉じる（`cancelableOnTouchOutside = true`）。`cancelable = false` と `cancelableOnTouchOutside = false` では Back と外側のタップで閉じず（`count` が 1 のまま）、肯定で閉じる
  - 単一選択: 2 番目を選んで肯定 → `1`
  - 複数選択: 1 番目と 3 番目を選んで肯定 → `[true, false, true]`。J-D4 の短い配列で、返る配列の長さが項目数
  - テキスト入力・ログイン: 英小文字と数字だけの文字を入れて肯定 → その文字。空を許さない設定で、空のとき肯定が `enabled="false"`
  - 題名・本文・ボタン・項目の文字（非 ASCII とサロゲートペアを含む）が画面に出ていること（補助の `expect`）
  - 回転: 補助で画面を横にしてもダイアログが残り、肯定で完了が 1 回（回転で作り直されない。1.5）。終わったら向きを戻す
- **後ろ（E-8）**: 試す。テストが別のスレッドを起こし、補助が `home` でアプリを後ろに回したあと、そのスレッドが Bridge を直接（Manager の受け口と `Kind.Alert`）呼ぶ。補助がアプリを前に戻し、`Update` が回ったところで `NOT_FOREGROUND` の文言が 1 回来ること。UnityMain は後ろで止まるので、別のスレッドが要る。5.5 の手順 5 で、確実に起こせなければ層 1 だけにし、理由を実装結果ファイルに書く
- 同期の失敗: J-D3、空の題名（`LogWarning` なので `LogAssert` は要らない）

**32 ビット（`--mono`、ARMv7）。** 64 ビットのテストは上の条件で `Assert.Ignore` になる。

- 6 本すべてが同期に `"Failed to <method>: the native library is not available ..."` を出し、例外が出ないこと（J-D9）

### 7.4 層 3・手動

- 層 3: 7.3 の画面の照合（補助が Unity の外から OS の画面を読む）が層 3 に当たる
- 手動: なし

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。起こせない失敗（E-6、E-7、E-9 の実物、E-13）は文言の変換を層 1 で押さえ、受け口の形をコードレビューで押さえる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- OS が求める許可: なし
- 前提の OS 設定: 画面が点いていて、ロックが解けていること。補助が起動のときに画面を点け、ロックを外し、点いたままにする。PIN のある端末では外せないので、実行の前に確かめて止める（補助の設計書 v2 の 4 章）
- アニメーションと画面の向きは補助が変え、終わりに戻す（同 4 章）

### 8.3 呼び出し側を止める OS の画面

ダイアログは非同期で、呼び出し側を止めない。ただし結果は人が押すまで来ない。
**外から押す手段を、ホストの adb の補助として作る**（補助の設計書 v2）。Share の選択画面と Notification の許可・シェードにも使う。
テストは `Debug.Log` で依頼の行を出し、補助が `adb logcat` で拾って `uiautomator dump` と `input` で押し、結果をアプリの領域のファイルで返す。テストは結果とイベントを待つ。
補助がいない実行（Unity の窓から流すなど）では、UI を使うテストは `Assert.Ignore` になる。`verify_unity_android.sh` は補助を必ず起動し、補助が応答したことを判定に入れるので、ゲートでは skip にならない。
