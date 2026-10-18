# Android Dialog 実装計画 v1（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Dialog（Android） |
| 種別 | **移行。機能追加ではない。** 1.x の Unity 向けブリッジ（`android.unity.dialog.UnityAndroidDialogManager`、`AndroidJavaObject` / `AndroidJavaProxy`）から、C ABI 2.0.0（`libntk.so`、`Dialog.h`）の P/Invoke に置き換える |
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（4 機能の目次。決定 J-1〜J-7、Windows と違うところ A-1〜A-6） |
| 移行前の基準 | `48d7927`（親課題の「移行前の基準」）。Dialog の自動テストは無い（層 1 は対象が無く、層 2b も無い）。旧の振る舞いは 2 章の表がすべて |
| 方針 | **公開 API（メソッド 6 本、イベント 6 本、引数、既定値、値の意味、エラーの文言）を保つ。** 変わるのは名前空間（J-4）と、5.3「既知の差分」に列挙したものだけ |
| AAR | `android-native-toolkit-2.0.0.aar` と `android-native-toolkit-capi-2.0.0.aar`（dist 1.13.0）。親課題のスパイクで `Plugins/Android/VERSION.txt` の主のピンに入れた。旧ブリッジは移行の間 `extra_*` に残す |
| 出典 | native-toolkit `dist/1.13.0/android/include/NativeToolkitC/Dialog.h`、`android/android_library_capi/src/main/cpp/src/Dialog/Dialog.cpp`（以下 `D.cpp`）、`.../capi/jni/DialogBridge.kt`（以下 `Bridge`）、`android/android_library/.../dialog/AndroidDialogFragment.kt`（以下 `Frag`）。旧ブリッジは native-toolkit のタグ `1.12.0` の `android/unity_android_plugin/.../dialog/UnityAndroidDialogManager.kt`（以下 `OldBridge`）と `android/android_library/.../dialog/AndroidDialogFragment.kt`（以下 `OldFrag`）。C ABI の約束は native-toolkit の `artifact/topics/android-c-abi/designs/` の第 1 部（`2026-10-04-...-c-abi-design.md`）と第 2 部（`2026-10-05-...-part2.md`） |

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
| - | `ntk_dialog_cancel(uint64_t request_id)` | 無い | 公開 API には出さない。層 2b のテストが、UI を操作せずに完了を起こすために使う（7.3） |
| - | 結果の読み取り: `ntk_dialog_result_answer` / `_button` / `_button_text` / `_checked_index` / `_checked_count` / `_checked_at` / `_text` / `_username` / `_password` / `_free` | - | 文字列は `const char*` と `size_t* out_size`。`_free` するまで有効 |

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
| `CANCELED_BY_SYSTEM` | 構成の変更以外で fragment が壊れた（宿主や Activity が終わった） |
| `CANCELED` | `ntk_dialog_cancel` |
| `INVALID_PARAMETER` / `OUT_OF_MEMORY` / `UNKNOWN` | Kotlin の側の例外、結果を組めない。入口の検査と重なるので、ふつうは起きない |

### 1.4 スレッド、寿命、入力

- どの関数もどのスレッドから呼んでよく、待たない。**完了と `release` は Android の main（UI スレッド）で、この順に来る**。UnityMain ではない（親課題 A-5）
- main 以外から呼ぶと、**関数が戻る前に、`*out_request_id` が書かれる前に完了が来うる**（第 1 部の設計 1.3）。UnityMain は main ではないので、ふつうの呼び方でこれが起きる
- 受け付けた呼び出しにつき完了はちょうど 1 回。プロセスが終わるときは完了も `release` も来ない
- `release` は NULL でよい。`user_data` はライブラリが参照しない（コールバックに渡すだけ）ので、ただの整数でよい
- 入力の文字列・配列・構造体は**呼び出しの間だけ**有効であればよい。ライブラリは戻る前に Java の配列へ写す（`D.cpp`:176-196、第 1 部の設計 1.1 が引く Windows の設計 7.4）
- NULL の文字列はライブラリの既定値になる（`title` / `message` / `hint` は `""`、ボタンは "OK" など）。`""` は `""` のまま（ボタンの文字は空になる）
- コールバックから例外が C に抜けるとプロセスが終わる（第 1 部の設計 5.11）

### 1.5 画面

前面が `FragmentActivity`（Unity の GameActivity はこれ）なら、そこに androidx の `DialogFragment`（`com.jonghyunkim.nativetoolkit.dialog.AndroidDialogFragment`）を出す。違えば透明な宿主 `NtkHostActivity` を起動して出す。
ダイアログは**フレームワークの `android.app.AlertDialog`**。ボタンは `android:id/button1`（肯定）と `android:id/button2`（否定）、題名は `android:id/alertTitle`、本文は `android:id/message`。
入力欄（`EditText`）には ID が無い（`View.generateViewId()`）。テキスト入力は 1 つ、ログインは上から利用者名・パスワード。肯定ボタンは、空を許さない設定では文字を入れるまで無効（`Frag`:354-398）。

## 2. 既存の C# 実装

- `Runtime/Dialog/AndroidDialogManager.cs`（613 行）: `MonoBehaviour` Singleton（`DontDestroyOnLoad`）。ガードは `#if UNITY_ANDROID` だけ。`Awake` で `UnityMainThreadDispatcher.Instance` を作り、`Initialize()` でブリッジを取って 6 つの `AndroidJavaProxy` を登録する
- 公開メソッド 6 本は戻り値なし。結果は種類ごとのイベント 6 本で返す。per-call callback は無い
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

旧の失敗（付随の値はすべて `null`）:

| 場面 | `errorMessage` | 出る時点 |
|---|---|---|
| 題名が空（6 本） | `"Title cannot be null or empty."` | C# の検査。`Show*` の中で同期に |
| 本文が空（Dialog / Confirm / TextInput / Login） | `"Message cannot be null or empty."` | 同上 |
| 項目が空 | `"singleChoiceItems cannot be null or empty."` / `"multiChoiceItems cannot be null or empty."` | 同上 |
| ブリッジが取れていない | `"pluginInstance is null"` | 同上 |
| `AndroidJavaObject.Call` の例外 | `$"Internal error: {ex.Message}"` | 同上 |
| Activity が `FragmentActivity` でない | `"Failed to cast context to FragmentActivity"` | ブリッジ。後のフレーム |
| 表示の例外（後ろからの呼び出しの `IllegalStateException` を含む） | `"Failed to showDialog: <例外の文言>"`（`showConfirmDialog` など、メソッドの名前） | 同上 |

- Editor など Android 以外: 検査に通れば `LogWarning("ShowDialog can only be called on an Android device.")` を出し、イベントは出さない
- 誰も調べていなかったもの: 単一選択の `checkedItem` の範囲、複数選択の `checkedItems` の長さ（`null` か空なら項目数の `false` に置き換えるだけ）。範囲外・長さ違いのときにフレームワークがどうしたかは、記録が無い
- 既定値: `ShowDialog` の `buttonText = "OK"`、Confirm の `"No"` / `"Yes"`、SingleChoice の `checkedItem = 0` と `"Cancel"` / `"OK"`、MultiChoice の `"Cancel"` / `"OK"`、TextInput の `hint = ""` と `"Cancel"` / `"OK"`、Login の `"Username"` / `"Password"` / `"Cancel"` / `"Login"`。`enablePositiveButtonWhenEmpty = false`、`cancelableOnTouchOutside = true`、`cancelable = true`。**`null` を明示しても同じ値に置き換える**（`??=`）
- 利用者: サンプル `Runtime/UI/Android/Dialog/AndroidDialogManagerExampleController.cs`（呼び出しは `#if UNITY_ANDROID && !UNITY_EDITOR`、`using` は同じガードの中）、`Assets/Scripts/Runtime/UI/Android/Dialog/AndroidDialogManagerTest.cs`（`using` だけ）

## 3. 実装制約

- `agent-rules/coding-rules/common.md`: Manager + Bridge の 2 層、変換は Bridge の `internal static` の純粋な関数で `DllImport` の `#if` の外、`Android` 接頭辞、共通化しない、二重のガード、結果として返す失敗は `LogWarning`
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、ログのタグ、XML コメント
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

- `out_request_id` は `IntPtr.Zero`（NULL）で渡す。要求の ID は使わない（J-D1）。層 2b のテストは、取り消しのために ID が要るので、`out ulong` を取る別の宣言を使わず、`IntPtr` に `Marshal.AllocHGlobal(8)` を渡して読む（同じ `extern` を使い回す）
- コールバックの delegate: `[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ResultCallback(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr result)`。`static readonly` のフィールドに置き、`Marshal.GetFunctionPointerForDelegate` で 1 回だけ関数ポインタにする。受け口は `[MonoPInvokeCallback(typeof(ResultCallback))]` の static メソッド
- `release` は `IntPtr.Zero`（J-D1）
- 文字列を返す読み取りは `IntPtr` で受け、`AndroidNativeToolkitCApi.ReadUtf8(ptr, ToByteCount(size))` で写す。`[return: MarshalAs(UnmanagedType.LPUTF8Str)]` は使わない

### 4.2 引数の変換（公開 API は変えない）

1. `null` の置き換えは今のまま（2 章の既定値。C# で行うので、ライブラリの既定値には頼らない）
2. C# の検査は今のまま、今の順で、今の文言で（題名、本文、項目）
3. 加える検査（J-D3、J-D4）:
   - 単一選択の `checkedItem` が `-1` 未満か項目数以上 → ネイティブを呼ばず失敗（J-D3）
   - 複数選択の `checkedItems` の長さが項目数と違う → 足りない分を `false` で埋め、余る分を捨てて渡す（J-D4）
4. 構造体の組み立て（純粋な関数）:

| C# の引数 | 構造体へ |
|---|---|
| 文字列 | NUL 終端の UTF-8（`AndroidNativeToolkitCApi.AllocUtf8`）。対になっていないサロゲートは U+FFFD |
| `cancelable` | `not_cancelable = cancelable ? 0 : 1`（**名前の向きが逆**） |
| `cancelableOnTouchOutside` | `not_cancelable_on_touch_outside = cancelableOnTouchOutside ? 0 : 1`（同上） |
| `enablePositiveButtonWhenEmpty` | `enable_positive_when_empty = 1 / 0` |
| 単一選択の `singleChoiceItems` / `checkedItem` | `items`（`const char*` の配列を `AllocHGlobal` に並べる）、`item_count`、`checked_index` |
| 複数選択の `checkedItems` | `checked`（`int32_t` の配列。`true` は 1） |

確保したものは 1 つの一覧で追い、`finally` で全部解放する。ライブラリは戻る前に写すので（1.4）、呼び出しの直後に解放してよい。

### 4.3 結果の変換

完了の受け口（Android の main）で、結果のハンドルから managed の値（`DialogAnswer`: 種類、答え、ボタンの文字、添字、真偽の配列、文字列 3 つ、エラー）を写し、ハンドルを `_free` してから UnityMain に積む。イベントは UnityMain で出す。

| 完了 | イベントへ |
|---|---|
| `NONE`、`BUTTON` | `buttonText = button_text`（表示したラベル。C# が置き換えた既定値を含む）。単一選択は `checked_index`（-1 は `null`）、複数選択は `checked_at` を `checked_count` 個読んだ `bool[]`（否定のときは `count == 0` なので `null`）、入力とログインは文字列（否定のときは NULL なので `null`）。`(…, true, null)` |
| `NONE`、`DISMISSED` | 今と同じ `("Cancel", …, true, "")`。入力は `""`、ログインは `""` と `""`。**単一選択は `null`、複数選択は `null`**（選択の状態は来ない。5.3、第 2 部 AP-13） |
| 失敗（`NONE` 以外） | `(null, …null, false, 文言)`。文言は 4.4 |

### 4.4 エラーの文言

旧のブリッジの形 `"Failed to <method>: <理由>"`（`<method>` は `showDialog` などのブリッジのメソッド名）を保ち、理由を Manager が持つ。

| 原因 | 時点 | `errorMessage` |
|---|---|---|
| C# の検査（題名・本文・項目） | 同期 | 今の文言のまま |
| J-D3（`checkedItem` の範囲外） | 同期 | `"checkedItem is out of range."` |
| ネイティブが使えない（`DllNotFoundException` などと版。共通部の `NativeUnavailable`） | 同期 | `"Failed to <method>: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only)."` |
| 入口の `NOT_INITIALIZED` | 同期 | `"Failed to <method>: the native library is not initialized yet."` |
| 入口の `INVALID_PARAMETER` / `NOT_SUPPORTED` | 同期 | `"Failed to <method>: the request was rejected (<名前>)."`。C# の構造体の誤りなので `LogError` も出す |
| 入口の `OUT_OF_MEMORY` / `UNKNOWN` / 想定外の値 | 同期 | `"Failed to <method>: <名前>."` |
| 完了の `NOT_FOREGROUND` | 非同期 | `"Failed to <method>: the app is not in the foreground."` |
| 完了の `HOST_START_FAILED` | 非同期 | `"Failed to <method>: the dialog host could not be started."` |
| 完了の `SHOW_FAILED` | 非同期 | `"Failed to <method>: the dialog could not be shown."` |
| 完了の `CANCELED_BY_SYSTEM` | 非同期 | `"Failed to <method>: the dialog was closed by the system."` |
| 完了の `CANCELED` | 非同期 | `"Failed to <method>: the dialog was canceled."`（公開 API では起きない。テストだけ） |
| 完了のそのほか | 非同期 | `"Failed to <method>: <名前>."` |
| C# の例外（変換・確保の失敗など） | 同期 | `$"Internal error: {ex.Message}"`（今と同じ） |

`<名前>` はエラーの列挙の名前（`OUT_OF_MEMORY` など）。名前への変換は純粋な関数にする。

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | ガード | 内容 |
|---|---|---|---|
| 移動・既存変更 | `Runtime/Dialog/AndroidDialogManager.cs` → `Runtime/Android/Dialog/AndroidDialogManager.cs` | `UNITY_ANDROID \|\| UNITY_EDITOR`（今は `UNITY_ANDROID`） | `git mv`（`.meta` ごと。GUID を保つ）を**中身を変える前の別のコミットで**行い、名前空間を `JonghyunKim.NativeToolkit.Runtime.Android.Dialog` にする（J-4）。次のコミットで `AndroidJavaObject` / `AndroidJavaProxy` を消し、`AndroidDialogCApi` を呼ぶ。公開メソッド・イベント・既定値・検査・文言はそのまま |
| 新規作成 | `Runtime/Android/Dialog/AndroidDialogCApi.cs` | 型と変換は `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport` と呼び出しは `UNITY_ANDROID && !UNITY_EDITOR` | Bridge。4.1 の構造体・`extern`・delegate・受け口、4.2〜4.4 の純粋な関数（構造体の組み立て、`checked` の長さの合わせ方、結果の写し、エラーの名前と文言） |
| 既存変更 | `Runtime/UI/Android/Dialog/AndroidDialogManagerExampleController.cs` | 今のまま | `using` を新しい名前空間に（1 行） |
| 既存変更 | `Assets/Scripts/Runtime/UI/Android/Dialog/AndroidDialogManagerTest.cs` | 今のまま | 同上 |
| 新規作成 | `Tests/Runtime/AndroidDialogCApiTests.cs` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（7.1） |
| 新規作成 | `Tests/Runtime/AndroidNativeToolkitCApiTests.cs` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（共通部: 版の判定、UTF-8、例外の分類） |
| 新規作成 | `Tests/PlayMode/AndroidDialogPlayerTests.cs` | `UNITY_ANDROID` | 層 2b（7.3）。UI の操作はホストの補助（8.3）に頼む |
| 既存変更 | `Runtime/Android/Common/AndroidNativeToolkitCApi.cs` | 今のまま | 変更なし（スパイクで入れたものを使う）。足すものがあればここ |
| 既存変更 | `agent-rules/coding-rules/common.md` | - | 「Unity Bridge パターン > Android」を、C ABI の形（Windows の節に倣い、A-1〜A-6 の違いを書く）に書き直す。Dialog の実装で決まった形を写す |
| 既存変更 | `agent-rules/coding-rules/testing.md` | - | Dialog の Android の層 1 と層 2b、コンパイルガードの群（A 群へ） |
| 非変更 | `Runtime/Resources/UI/Android/Dialog/*`、`Runtime/UI/Common/NativeToolkitSampleNavigator.cs`、`Editor/UI/NativeToolkitEditorWindow.cs` | - | コントローラーの型名で参照するだけ。コントローラーの名前空間は変えない |

Manager を `|| UNITY_EDITOR` にするのは、型を Editor でも見えるようにして層 1 のテストから変換に届くため。Editor での振る舞い（検査の失敗はイベント、通れば `LogWarning` だけ）は変えない（J-D6）。

### 5.2 判断

| # | 内容 | 決定（案） |
|---|---|---|
| J-D1 | 完了と呼び出しの対応付け | `user_data` に**ダイアログの種類**（0〜5）を入れ、`release` は NULL。イベントが種類ごとで per-call の状態が無いので、要求ごとの表は要らない。完了が関数の戻る前に来ても、種類だけで正しいイベントに届く（1.4）。要求の ID は使わず、`out_request_id` は NULL |
| J-D2 | 完了の受け口ですること | `try` で全体を囲み、結果を `DialogAnswer` に写し、`_free` し、`Awake` で取っておいた dispatcher に積む。受け口で Manager の状態に触らない。**ネイティブのスレッドで `UnityMainThreadDispatcher.Instance` の getter に触らない**（`common.md`）。例外は `LogError` にして飲む（C に抜けるとプロセスが終わる） |
| J-D3 | 単一選択の `checkedItem` が `-1` 未満か項目数以上 | ネイティブを呼ばず、同期に `(null, null, false, "checkedItem is out of range.")`。C ABI は入口で拒む。旧は検査せずに渡しており、結果は記録が無い（2 章） |
| J-D4 | 複数選択の `checkedItems` の長さが項目数と違う | 足りない分は `false`、余る分は捨てる。C ABI は長さを知らないので、短い配列をそのまま渡すと範囲外を読む。旧は `null` と空だけを置き換え、ほかはフレームワークに渡していた |
| J-D5 | 閉じたとき（`DISMISSED`）の選択の状態 | 単一選択も複数選択も `null`。C ABI が返さないので保てない（第 2 部 DL-10 / DL-11）。旧は今の選択を返していた。5.3 と移行ガイドに書く |
| J-D6 | Editor（Android 以外） | 今と同じ: 検査に失敗すればイベント、通れば `LogWarning` だけでイベントは出さない。Windows の Dialog（`-5` のイベント）とは揃えない（公開 API の振る舞いを変えない） |
| J-D7 | 失敗の時点 | ネイティブを呼ぶ前と入口で拒まれた失敗は、`Show*` の中で同期にイベントを出す（今の C# の検査と同じ）。受け付けた後の失敗は完了から UnityMain で出す |
| J-D8 | 後ろから呼んだとき | `NOT_FOREGROUND` の完了を `"Failed to <method>: the app is not in the foreground."` にする。旧は `"Failed to <method>: <IllegalStateException の文言>"` で、同期の失敗だった（第 2 部 DL-08）。親課題 J-6 のとおり、保てない振る舞いとして 5.3 に書く |
| J-D9 | ネイティブが使えないとき（32 ビット、Mono） | 同期の失敗（4.4）。旧の `"pluginInstance is null"` に当たる。Mono の Player は ARMv7 だけなので、**Mono では Dialog は常にこの失敗になる**（親課題の「スパイク」）。移行ガイドに「IL2CPP と ARM64 が要る」と書く |

### 5.3 既知の差分（意図したもの）

XML コメントに書き、実装結果ファイル経由で次の版のマニュアル（移行ガイド）へ引き継ぐ。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 名前空間とフォルダ（J-4） | `JonghyunKim.NativeToolkit.Runtime.Dialog`、`Runtime/Dialog/` | `JonghyunKim.NativeToolkit.Runtime.Android.Dialog`、`Runtime/Android/Dialog/`。利用者は `using` を書き換える |
| 閉じたときの選択（J-D5） | 単一選択は今の選択、複数選択は今の状態 | どちらも `null` |
| 範囲外の `checkedItem`（J-D3） | フレームワークに渡した | 同期の失敗 `"checkedItem is out of range."` |
| 長さの違う `checkedItems`（J-D4） | フレームワークに渡した | 項目数に合わせる |
| 後ろから呼ぶ（J-D8） | 同期の `"Failed to <method>: <例外の文言>"` | 後のフレームで `"Failed to <method>: the app is not in the foreground."` |
| 前面が `FragmentActivity` でない | `"Failed to cast context to FragmentActivity"` | 透明な宿主に出る（第 2 部 DL-08）。Unity の GameActivity は `FragmentActivity` なので、ふつうは起きない |
| ネイティブが使えない（J-D9） | 無い（ブリッジは Java なので 32 ビットでも動いた） | 同期の `"Failed to <method>: the native library is not available ..."`。**Mono（ARMv7）と 32 ビットの ARM では Dialog は使えない** |
| ボタンの文字の対になっていないサロゲート | そのまま | U+FFFD |
| エラーの文言 | 例外の文言を含む | 4.4 の固定の文言 |

### 5.4 呼び出しの流れと契約（`ShowConfirmDialog` の例）

1. `null` を既定値に置き換え、C# の検査（2 章の順）。失敗ならイベントを同期に出して終わる
2. Android 以外（Editor）は `LogWarning` で終わる（J-D6）
3. `AndroidNativeToolkitCApi.EnsureNativeAvailable()` が `Available` でなければ、4.4 の同期の失敗
4. 確保の一覧を try の外で空に作る。UTF-8 に変換して構造体を組み立てる
5. `ntk_dialog_show_confirm_async(ref request, s_callbackPointer, (IntPtr)DialogKind.Confirm, IntPtr.Zero, IntPtr.Zero)`。**完了はこの呼び出しが戻る前に main で来うる**が、受け口は UnityMain に積むだけなので、5〜7 の間に UnityMain でイベントが出ることはない
6. `finally` で確保したものを全部解放する
7. 戻り値が `NONE` 以外なら 4.4 の同期の失敗をイベントで出す（catch の外で 1 回）。`NONE` なら何もしない（完了を待つ）

```
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

- 完了の受け口（main）: `try { answer = Read(result); } finally { ntk_dialog_result_free(result); }` を `try/catch` で囲み、`s_dispatcher.Enqueue(() => Instance.Raise(answer))`
- UnityMain: 種類に応じてイベントを 1 回出す。購読者の例外は捕まえない（dispatcher の扱いのまま。今と同じ）
- **どのスレッドでも完了を待たない**（親課題 A-4）。`Show*` は受け付けたら戻る
- Manager が壊されたあとに完了が来たときは、今と同じく `Instance` で新しい Manager を作って出す（今の `AndroidJavaProxy` と同じ。UnityMain なので作ってよい）

### 5.5 実装順

各段階の終わりに `scripts/verify_unity_android.sh`（EditMode と実機の IL2CPP）を流す。`!UNITY_EDITOR` 側は Player のビルドでしかコンパイルされない。

1. 移動のコミット（5.1。`git mv` と名前空間、`using` 2 か所）。振る舞いは変えない
2. `AndroidDialogCApi` の構造体と変換の関数、層 1 のテスト
3. `AndroidDialogCApi` の `extern` と受け口
4. `AndroidDialogManager` の置き換え（J-D1〜J-D9）
5. ホストの UI の補助（`artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md`）と層 2b
6. `--mono`（ARMv7）で J-D9 を確かめる
7. 実装結果ファイル（`artifact/android/dialog/results/`）に 5.3 と 4.4 を書き写す

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | 題名・本文・項目が空 | C# | 同期、今の文言 | 1（検査の純粋な関数）、2b |
| E-2 | `checkedItem` の範囲外（J-D3） | C# | 同期、`"checkedItem is out of range."` | 1、2b |
| E-3 | `checkedItems` の長さ違い（J-D4） | C# | 合わせて渡す（失敗にしない） | 1（組み立て）、2b（項目数と同じ長さが返る） |
| E-4 | ネイティブが使えない（32 ビット・Mono・版違い） | C# | 同期、4.4 | 1（例外の分類と版）、2b（`--mono`） |
| E-5 | 未初期化 | ネイティブ（入口） | 同期、4.4 | 1（文言）。起こせない（androidx.startup が起動時に済ませる） |
| E-6 | 入口の `INVALID_PARAMETER` / `NOT_SUPPORTED` | ネイティブ（入口） | 同期、4.4 と `LogError` | 1（文言）。正しい実装では起きない。構造体の位置は 1 で固定 |
| E-7 | `NOT_FOREGROUND` | ネイティブ（完了） | 非同期、4.4 | 1（文言）。2b では起こさない（8.2） |
| E-8 | `HOST_START_FAILED` / `SHOW_FAILED` / `CANCELED_BY_SYSTEM` | ネイティブ（完了） | 非同期、4.4 | 1（文言）。起こせない |
| E-9 | `CANCELED`（取り消し） | ネイティブ（完了） | 非同期、4.4 | 2b（`ntk_dialog_cancel` で完了の経路を通す） |
| E-10 | 閉じた（Back・外側） | ネイティブ（完了） | `("Cancel", true, "")` ほか 4.3 | 1（写し）、2b（Back） |
| E-11 | C# の例外 | C# | 同期、`"Internal error: ..."` | 1（形はコードレビュー） |

## 7. テスト方針

### 7.1 層 1（EditMode）

`AndroidDialogCApiTests`:

- 6 つの構造体の大きさと全フィールドの位置（`Marshal.SizeOf`、`Marshal.OffsetOf`。4.1 の表）
- 構造体の組み立て: `struct_size`、`reserved* = 0`、`not_cancelable` の向き（`cancelable = true` で 0）、`enable_positive_when_empty`、文字列のポインタが NUL 終端の UTF-8 であること（読み戻す）、項目の配列と件数、`checked` の値（J-D4 の短い・長い・`null`・空）
- `checkedItem` の検査（J-D3）: `-1`、`0`、項目数 - 1 は通り、`-2` と項目数は失敗
- 結果の写し（4.3）: `BUTTON` / `DISMISSED` × 6 種類から、イベントの引数への変換（純粋な関数に `DialogAnswer` を渡す形）。`DISMISSED` で `button` を読まないこと
- エラーの名前と文言（4.4）: 全値と想定外の値

`AndroidNativeToolkitCApiTests`（共通部）: 版の判定（`0x020000`、`0x020103` は通り、`0x010300`、`0x030000` は通らない）、例外の分類、UTF-8（非 ASCII、サロゲートペア、対になっていないサロゲート、空、バイト長で読む）

### 7.2 層 2a

無い。Manager は Editor では検査のあとに `LogWarning` を出すだけで、層 1 と同じ以上のことを確かめられない。

### 7.3 層 2b（実機の Player、`AndroidDialogPlayerTests`）

64 ビット（IL2CPP）:

- **取り消しで完了の経路を通す**（UI 不要）: Bridge の `extern` で直接 `ntk_dialog_show_confirm_async` を、`out_request_id` を受けて呼び、`ntk_dialog_cancel` で閉じる。完了が 1 回、`CANCELED`、UnityMain で受けられること
- **UnityMain 以外から呼ぶ**: 別のスレッドから `ShowConfirmDialog` を呼び、ホストの補助が肯定を押す。イベントが UnityMain で 1 回（完了が関数の戻る前に来うる経路）
- **ホストの補助で押す**（8.3）。6 種類それぞれで:
  - 肯定・否定・Back の値が 2 章の表（閉じたときは 5.3 の差分を反映）と同じ
  - 単一選択: 2 番目を選んで肯定 → `1`
  - 複数選択: 1 番目と 3 番目を選んで肯定 → `[true, false, true]`。J-D4 の短い配列で、返る配列の長さが項目数
  - テキスト入力・ログイン: ASCII の文字を入れて肯定 → その文字。空を許さない設定で、空のとき肯定が押せないこと（`enabled="false"`）
  - 題名・本文・ボタンの文字（非 ASCII とサロゲートペアを含む）が画面に出ていること（補助が UI の木を読んで照合する）
- 同期の失敗: J-D3、空の題名

32 ビット（`--mono`、ARMv7）:

- 6 本すべてが同期に `"Failed to <method>: the native library is not available ..."` を出し、例外が出ないこと（J-D9）

### 7.4 層 3・手動

- 層 3: 7.3 の画面の照合が、Unity の外から OS の画面を読む層 3 に当たる
- 手動: 回転（構成の変更で fragment が作り直されても完了が 1 回）。補助の `adb shell settings put system user_rotation` で自動にできるかを 5.5 の手順 5 で試し、できなければ手動に残す

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。起こせない失敗（E-5〜E-8 の実物）は文言の変換を層 1 で押さえる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- OS が求める許可: なし
- 前提の OS 設定: 画面が点いていて、ロックが解けていること。補助が実行の前に `adb shell input keyevent KEYCODE_WAKEUP` と `wm dismiss-keyguard` を送る。ロックに PIN がある端末では解けないので、実行の前に確かめて止める
- `NOT_FOREGROUND` は層 2b で起こさない。Unity は後ろに回ると止まり（`runInBackground` は Android では効かない）、テストが進まないため

### 8.3 呼び出し側を止める OS の画面

ダイアログは非同期で、呼び出し側を止めない。ただし結果は人が押すまで来ない。
**外から押す手段を、ホストの adb の補助として作る**（親課題の別の設計書 `designs/2026-10-11-android-ui-driver-design-v1.md`）。Share の選択画面と Notification の許可・シェードにも使う。
テストは `Debug.Log` で依頼の行を出し、補助が `adb logcat` で拾って `uiautomator dump` と `input` で押し、終わったら印のファイルをアプリの領域に置く。テストは印とイベントを待つ。
