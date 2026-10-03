# Windows Notification 実装計画 v3（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Notification（Windows） |
| 種別 | **移行。機能追加ではない。** 1.x の C ABI（dist 1.11.0）から 2.0.0（dist 1.12.0）へ P/Invoke 層を書き換える |
| 前の版 | `2026-06-06-windows-notification-design-v2.md`（1.x での新規実装の設計）。本書はその上に移行を重ねる |
| 親課題 | `artifact/topics/windows-c-abi-2/README.md`（Notification は「Notification・振る舞いが変わる」） |
| 移行前の基準 | `50fe7bb`（`feature/UNT-12`）。W-01〜W-10（W-04 は保留）が 1.x で通る |
| 前提 | Dialog の移行（`artifact/windows/dialog/designs/2026-09-27-windows-dialog-design-v5.md`）の手順 1（J-6: DLL を 2 本置く、J-11: PDB）が済んでいること |
| 方針 | **既存の公開 API（`WindowsNotificationManager` のメソッド 12 本・イベント 3 本・定数、`WindowsNotificationResult`、`WindowsNotificationJsonBuilder`、ペイロードの型、JSON の形と単位）を変えない**（親課題 Q-4）。サンプル・UXML は触らない。W-01〜W-10 がそのまま移行の検証になり、保留していた W-04 と W-11 が加わる。変わる振る舞いは 5.4「既知の差分」に列挙したものだけ |
| DLL | `windows-native-toolkit-capi-2.0.0.dll`（Dialog と同じ。`Microsoft.WindowsAppRuntime.Bootstrap.dll` を通常のインポートで読むので、隣に置く。今も置いてある） |
| 開発者の判断（2026-09-27） | J-1（JSON の公開 API を残し C# で読む）、J-2（`timestamp` は秒のまま）、J-3（2 回目の `Initialize` はネイティブを呼ばずに成功）、J-4（Editor では何もしない）、J-5（`Audio.Src` は移行では読まない） |

## 1. native-toolkit 側（2.0.0 の C API）

出典: native-toolkit `dist/1.12.0/windows/include/NativeToolkitC/Notification.h`（以下 `N.h`）、`Common.h`。
実装: `windows/WindowsLibraryCApi/src/Notification/`（`NotificationCApi.cpp`、`NotificationContentCApi.cpp`、`NotificationConvert.cpp`、`NotificationRuntime.cpp`）、`windows/WindowsLibrary/src/Notification/`。

### 1.1 使う関数

| # | 関数（`N.h` の行） | 1.x の対応 |
|---|---|---|
| OP-01 | `ntk_notification_runtime_initialize(uint32_t major_minor, ntk_notification_runtime** out)`（154）/ `_runtime_free`（161） | `initWinAppSdk` |
| OP-02 | `ntk_notification_manager_create(const ntk_notification_manager_options*, ntk_notification_manager** out)`（179） | `initNotificationManager` |
| OP-03 | `ntk_notification_manager_close`（201）/ `_free`（204） | `uninitNotificationManager` |
| OP-04 | `ntk_notification_content_create` / `_free` と `content_set_*` / `content_add_*`（309-375）、`ntk_notification_show(manager, content)`（213） | `showNotification` |
| OP-05 | 同じビルダーと `ntk_notification_schedule(manager, content, int64_t unix_ms)`（221） | `scheduleNotification` |
| OP-06 | `ntk_notification_cancel_scheduled(manager, tag, group)`（225） | `cancelScheduledNotification` |
| OP-07 | `ntk_notification_update_progress(manager, const ntk_notification_progress_update*)`（233） | `updateNotificationProgress` |
| OP-08 | `ntk_notification_set_badge(manager, int32_t)`（237） | `setBadge` |
| OP-09 | `ntk_notification_remove_by_id(manager, uint32_t)`（241） | `removeNotificationById` |
| OP-10 | `ntk_notification_remove_by_tag(manager, tag, group)`（245） | `removeNotificationsByTag` |
| OP-11 | `ntk_notification_remove_all(manager)`（249） | `removeAllNotifications` |
| OP-12 | `ntk_notification_get_all(manager, ntk_notification_list**)`（255）と `ntk_notification_list_count` / `_id_at` / `_tag_at` / `_group_at` / `_free`（270-280） | `getAllNotifications` |
| OP-13 | `ntk_notification_get_setting(manager, ntk_notification_setting*)`（259） | `getNotificationSetting` |
| OP-14 | `ntk_notification_open_settings(manager)`（263） | `openNotificationSettings` |
| OP-15 | コールバックの中で `ntk_notification_activation_raw_arguments(activation, size_t*)`（288-299） | 1.x のコールバックの `argsJson` |

共通: `ntk_version()`、`ntk_last_system_code()`。`ntk_notification_manager_set_invoked_handler` は使わない（J-3 でハンドラを差し替えないため）。

### 1.2 型と値

- `ntk_notification_error`（`N.h:34-45`）: `NONE`(0) / `NOT_INITIALIZED`(1) / `DISABLED`(2) / `INVALID_PAYLOAD`(3、予約・返らない) / `PROGRESS_NOT_FOUND`(4) / `HRESULT_FAILURE`(5) / `BADGE_FAILED`(6) / `INVALID_PARAMETER`(7) / `NOT_SUPPORTED`(8)。**1.x の errorCode 1〜8 と値も意味も同じ**なので、そのまま `WindowsNotificationResult.ErrorCode` に入れる
- `ntk_last_system_code()`: 多くの失敗では HRESULT ではなく、エラー値そのもの（1、5、7、8 など）が入る（`WindowsNotificationApi.cpp:84-90`）。本物の HRESULT が入るのは `runtime_initialize` の失敗とメモリ不足（`0x8007000E`）だけ。本計画ではログにだけ使う
- 列挙（すべて `int32_t`、関数の引数で渡す）: scenario（DEFAULT=0、REMINDER=1、ALARM=2、URGENT=3、INCOMING_CALL=4）、audio_kind（EVENT=0、MUTE=1、URI=2）、duration（SHORT=0、LONG=1）、logo_crop（NONE=0、CIRCLE=1）、setting（ENABLED=0〜DISABLED_BY_MANIFEST=4）。範囲外は `INVALID_PARAMETER`
- ビルダーの文字列の `NULL` と `""`: title / body / 画像 / attribution は `NULL` が「無い」、`""` が「空で在る」。tag / group の `NULL` は `""`。`add_button` の label、`add_text_input` / `add_combo` / `add_combo_item` の id（と item の label）は必須（`NULL` は `INVALID_PARAMETER`）
- `set_timestamp` は Unix **ミリ秒**で、±922,337,203,685,477 を超えると `INVALID_PARAMETER`。`set_expiration` は秒で検査なし
- 検証（ボタン 6 個以上、ループ音で duration が LONG でない、ボタンに引数と invokeUri の両方、kind=URI で uri が無い）は show / schedule のときに行い、`INVALID_PARAMETER`（`WindowsNotificationValidation.h:481-505`）。1.x と同じ規則
- show / schedule はまず `NOT_INITIALIZED`、次に通知の設定が有効でなければ `DISABLED` を返す（取得失敗の -1 も `DISABLED`、`WindowsNotificationManager.cpp:321-331`）
- `cancel_scheduled` の空の tag / group はワイルドカード（1.x と同じ）

### 1.3 構造体（x64、`#pragma pack(push, 8)`、`CApiLayoutTest.cpp:57-77`）

| 構造体 | 大きさ | フィールドの位置 |
|---|---|---|
| `ntk_notification_manager_options` | 56 | struct_size 0、reserved0 4、on_invoked 8、user_data 16、release 24、is_unpackaged 32、reserved1 36、display_name 40、icon_uri 48 |
| `ntk_notification_progress_update` | 56 | struct_size 0、reserved0 4、tag 8、group 16、value 24（double）、value_string 32、status 40、sequence_number 48、reserved1 52 |

`struct_size` は 56 以上 4096 以下、`reserved*` は 0。`options` が `NULL` か大きさの範囲外のとき、`release` は呼ばれない。

### 1.4 ハンドルとコールバック

- runtime / manager / content / list はそれぞれの `_free` で解放する。`NULL` は何もしない。二重解放は未定義
- コールバック `ntk_notification_invoked_fn(void* user_data, const ntk_notification_activation*)`（`N.h:103-104`、`__cdecl`）は **OS が選んだスレッドで来る**（`N.h:9-12`）。例外は、非パッケージのコールドスタート（`-ToastActivated` 付きで起動）で、`manager_create` の中で呼び出しスレッドに 1 回届く
- `raw_arguments` は 1.x のコールバックの `argsJson` と同じテキストを UTF-8 にしたもの（`N.h:287`、`CApiNotificationTest.cpp:350-369`）。activation とそこから読んだ文字列は、コールバックから戻るまでだけ有効
- close の後にも、配送中の活性化が 1 回走りうる（`N.h:198-199`）。止める仕組みは無い
- `release` は登録ごとにちょうど 1 回呼ばれる。`NULL` でよい

### 1.5 ランタイムと活性化の登録

- `runtime_initialize` は `MddBootstrapInitialize` を呼ぶ。2 回目（ロード済み・shutdown 待ち）は `NOT_SUPPORTED`。`runtime_free` の後、生きている manager が 0 になった時点で `MddBootstrapShutdown` を 1 回呼ぶ（`NotificationRuntime.cpp:43-83`）
- `manager_create` は呼び出しスレッドで `CoInitializeEx(COINIT_MULTITHREADED)` を行い、解除しない。既に STA なら `RPC_E_CHANGED_MODE` を受け入れて STA のまま（1.x と同じ、`WindowsNotificationManager.cpp:384`）
- 2 つ目の manager は `NOT_SUPPORTED`（close の後なら再び create できる）
- 非パッケージでは、HKCU に `CLSID\{clsid}\LocalServer32`、`AppUserModelId\<display_name>` の `DisplayName` / `IconUri` / **`CustomActivator`**（c9f4071b で追加）を書き、スタートメニューにショートカットを作る（`WindowsClassicActivator.cpp:363-454`）。**これで、動いている非パッケージのアプリにボタンのクリックが届くようになる**（1.x の不具合。`2026-09-27-windows-notification-ui-test-plan-v1.md` 2 章）

## 2. 既存の C# 実装

出典: `Runtime/Notification/WindowsNotificationManager.cs`、`WindowsNotificationJsonBuilder.cs`、`WindowsNotificationPayloads.cs`、`WindowsNotificationResult.cs`、`Runtime/Common/UnityMainThreadDispatcher.cs`。

- ガード: 4 ファイルとも `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`。ネイティブの呼び出しは `#if` ではなく、実行時の `Application.platform != RuntimePlatform.WindowsPlayer` で早く戻って止めている（`WindowsNotificationManager.cs:229` ほか 11 箇所）。**Editor では何もせず、per-call callback もイベントも出さない**
- `DllImport` 14 本。`DLL_NAME` は `DEVELOPMENT_BUILD` で `unity-windows-native-toolkit(-debug)` を切り替える（`:21-25`）。`CallingConvention` の指定は無い
- 公開 API（すべて同期。結果は per-call callback → イベントの順に、同じ呼び出しの中で出す）:

| メソッド（行） | 引数と既定値 | 1.x の結果 |
|---|---|---|
| `Initialize`（`:224-242`） | `(bool isPackaged = false, string? displayName = null, string? iconUri = null, Action<WindowsNotificationResult>? onResult = null)` | 毎回 `initWinAppSdk(0x00010007)` と `initNotificationManager` を呼ぶ。**2 回目も検証なしで成功**（ネイティブがコールバックを差し替えるだけ） |
| `ShowNotification`（`:249-257`） | `(string jsonPayload, onResult)` | 1、2、3（JSON の構文）、7（検証）、5 |
| `ScheduleNotification`（`:265-274`） | `(string jsonPayload, long scheduledTimeUnixMs, onResult)` | 同上 |
| `CancelScheduledNotification`（`:282-291`） | `(string tag, string group, onResult)` | 1、5 |
| `UpdateNotificationProgress`（`:303-312`） | `(string tag, string group, double value, string valueStr, string status, uint sequenceNumber, onResult)` | 1、4、5 |
| `SetBadge`（`:319-327`） | `(int value, onResult)` | **7（value < -6。初期化の確認より先）**、1、8（非パッケージ）、6 |
| `RemoveNotificationById`（`:334-342`） | `(uint notificationId, onResult)` | 1、8（非パッケージ）、5 |
| `RemoveNotificationsByTag`（`:350-359`） | `(string tag, string group, onResult)` | 1、5 |
| `RemoveAllNotifications`（`:365-373`） | `(onResult)` | 1、5 |
| `GetAllNotifications`（`:383-403`） | `(Action<string?, WindowsNotificationResult>? onResult)`。成功なら `[{"id":N,"tag":"…","group":"…"}]` の JSON | 1、8（非パッケージ）、5。4096〜65536 wchar のバッファを 5 回まで試す。ネイティブは切り詰めても成功を返していた |
| `GetNotificationSetting`（`:410-419`） | `()` → `WindowsNotificationSetting` | 未初期化・失敗・未定義の値は `Unknown`（-1） |
| `OpenNotificationSettings`（`:425-433`） | `(onResult)` | 1、5 |

- イベント: `NotificationOperationCompleted(WindowsNotificationResult)`、`NotificationInvoked(string argsJson)`（dispatcher 経由、次の `Update` 以降）、`GetAllNotificationsCompleted(string?, WindowsNotificationResult)`
- `WindowsNotificationResult`: `Operation` / `IsSuccess` / `ErrorCode`（成功で 0）/ `ErrorMessage`。文言の対応は `WindowsNotificationResult.cs:42-53`（1〜7。8 は `ErrorCodeToMessage` にあるがテストが無い）
- 公開の JSON の形（`WindowsNotificationJsonBuilder`、`WindowsNotificationJsonBuilder.cs:48-84`）と、1.x のネイティブが読んでいた形（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsNotificationManager.cpp:489-656,660-816`）は 4.2 の表。**`timestamp` は Unix 秒、`expiration` は相対秒**（`1.11.0:...cpp:607-612,640`）。`scheduledTimeUnixMs` はミリ秒
- コールバック: `[UnmanagedFunctionPointer(Cdecl)] delegate void NotificationInvokedCallback([MarshalAs(LPWStr)] string argsJson)` を static で保持（`:92-94,161`）。受け口 `OnNotificationInvoked`（`:483-504`）は `UnityMainThreadDispatcher.Instance.Enqueue(...)` だけを行い、実行時に `_instance?.NotificationInvoked` を呼ぶ。**`Instance` の getter はスレッドセーフではなく、無ければその場で GameObject を作る**（`UnityMainThreadDispatcher.cs:24-36`）
- `OnDestroy` で、`_instance == this && _initialized` のときだけ `uninitNotificationManager` を呼ぶ（`:203-213`）。bootstrap は shutdown しない。ドメインリロードの処理は無い（Editor でネイティブを呼ばないので要らなかった）
- 既知の奇妙な点: `Audio.Src` は `"src"` として出るが、1.x のネイティブは読まない（`WindowsNotificationJsonBuilder.cs:126`、`1.11.0:...cpp:757-784`）。GetAll の再試行は、切り詰めた成功を見分けられない。`Initialize()` を既定の引数のまま呼ぶと、Player では必ず 7 になる（XML コメントは「iconUri は任意」と書く）

## 3. 実装制約

- `agent-rules/coding-rules/common.md`: Manager + Bridge、ネイティブが同期なら C# も同期、コールバックは dispatcher 経由（:217-221）、`Windows` 接頭辞、プラットフォーム間で共通化しない（JSON リーダーは `MacClipboardJsonReader` と同じ形の Windows 用を作る）
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、全メソッドの 1 行目に全引数の `Debug.Log`、英語の XML コメント
- コンパイルガードは二重にする（P5、前例は `WindowsClipboardManager.cs:5,823` と Dialog v5）: 型は `UNITY_STANDALONE_WIN || UNITY_EDITOR`、`DllImport` と実際の呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR`
- 公開 API を変えない（基本情報の方針）

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

### 4.1 `DllImport`

- `DllImport("windows-native-toolkit-capi-2.0.0.dll", CallingConvention = CallingConvention.Cdecl)`、`internal static extern`（層 2b のテストが `ntk_last_system_code` を直接呼ぶ）。`DEVELOPMENT_BUILD` の切り替えは無くす
- 型の対応: ハンドル・文字列ポインタ・関数ポインタは `IntPtr`、`size_t` は `UIntPtr`、`int32_t` は `int`、`uint32_t` は `uint`、`int64_t` は `long`、`double` は `double`、戻り値の `ntk_notification_error` は `int`
- 文字列の入力は、Dialog と同じく `Encoding.UTF8` の NUL 終端バイト列を `AllocHGlobal` に置き、`finally` で全部解放する。出力（list の tag / group、activation の raw_arguments）は `IntPtr` と `out UIntPtr size` で受け、バイト列を写して UTF-8 で読む（`LPUTF8Str` の戻り値は使わない、親課題 README「マーシャリングの落とし穴」）
- 構造体は `WindowsNotificationCApi` の入れ子の `internal struct ManagerOptions` / `ProgressUpdate`（`[StructLayout(Sequential, Pack = 8)]`、`struct_size = (uint)Marshal.SizeOf<T>()`、`reserved*` = 0）
- コールバック: `[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void InvokedCallback(IntPtr userData, IntPtr activation)`。static readonly のフィールドで保持し、`Marshal.GetFunctionPointerForDelegate` で `options.on_invoked` に入れる。受け口は `[MonoPInvokeCallback(typeof(InvokedCallback))]` の static メソッド（IL2CPP のため）
- `user_data` と `release` は `IntPtr.Zero`（J-7）

`extern` の宣言（31 本。ビルダー 18、ほか 13）:

| C | C# |
|---|---|
| `ntk_version` / `ntk_last_system_code` | `uint ()` |
| `ntk_notification_runtime_initialize` | `int (uint majorMinor, out IntPtr runtime)` |
| `ntk_notification_runtime_free` | `void (IntPtr runtime)` |
| `ntk_notification_manager_create` | `int (ref ManagerOptions options, out IntPtr manager)` |
| `ntk_notification_manager_close` / `_free` | `void (IntPtr manager)` |
| `ntk_notification_content_create` | `int (out IntPtr content)` |
| `ntk_notification_content_free` | `void (IntPtr content)` |
| `content_set_title` / `_body` / `_hero_image` / `_inline_image` / `_attribution` / `_tag` / `_group` | `int (IntPtr content, IntPtr utf8)` |
| `content_set_scenario` / `_duration` / `_expires_on_reboot` | `int (IntPtr content, int value)` |
| `content_set_app_logo` | `int (IntPtr content, IntPtr uri, int crop)` |
| `content_set_audio` | `int (IntPtr content, int kind, IntPtr eventName, IntPtr uri, int loop)` |
| `content_add_button` | `int (IntPtr content, IntPtr label, IntPtr invokeUri, int withArguments, out UIntPtr index)` |
| `content_add_button_argument` | `int (IntPtr content, UIntPtr buttonIndex, IntPtr key, IntPtr value)` |
| `content_add_text_input` | `int (IntPtr content, IntPtr id, IntPtr placeholder, IntPtr title)` |
| `content_add_combo` | `int (IntPtr content, IntPtr id, IntPtr title, IntPtr defaultSelection, out UIntPtr index)` |
| `content_add_combo_item` | `int (IntPtr content, UIntPtr comboIndex, IntPtr id, IntPtr label)` |
| `content_set_progress` | `int (IntPtr content, IntPtr title, double value, IntPtr valueString, IntPtr status)` |
| `content_set_timestamp` / `_expiration` | `int (IntPtr content, long value)` |
| `ntk_notification_show` | `int (IntPtr manager, IntPtr content)` |
| `ntk_notification_schedule` | `int (IntPtr manager, IntPtr content, long unixMs)` |
| `ntk_notification_cancel_scheduled` / `_remove_by_tag` | `int (IntPtr manager, IntPtr tag, IntPtr group)` |
| `ntk_notification_update_progress` | `int (IntPtr manager, ref ProgressUpdate update)` |
| `ntk_notification_set_badge` | `int (IntPtr manager, int value)` |
| `ntk_notification_remove_by_id` | `int (IntPtr manager, uint id)` |
| `ntk_notification_remove_all` / `_open_settings` | `int (IntPtr manager)` |
| `ntk_notification_get_all` | `int (IntPtr manager, out IntPtr list)` |
| `ntk_notification_list_count` | `UIntPtr (IntPtr list)` |
| `ntk_notification_list_id_at` | `uint (IntPtr list, UIntPtr index)` |
| `ntk_notification_list_tag_at` / `_group_at` | `IntPtr (IntPtr list, UIntPtr index, out UIntPtr size)` |
| `ntk_notification_list_free` | `void (IntPtr list)` |
| `ntk_notification_get_setting` | `int (IntPtr manager, out int setting)` |
| `ntk_notification_activation_raw_arguments` | `IntPtr (IntPtr activation, out UIntPtr size)` |

（`content_set_*` の文字列版は 1 行にまとめたが、`extern` は関数ごとに書く。数は実装時にヘッダーと突き合わせる）

### 4.2 JSON の読み取り（J-1）

公開の `ShowNotification` / `ScheduleNotification` は今のまま JSON 文字列を受ける。C# で読み、ビルダーの呼び出しに変える。
**読む形と、キーが無い・未知の値のときの扱いは、1.x のネイティブ（`1.11.0:windows/WindowsLibrary/WindowsNotificationManager.cpp:489-816`）に合わせる**。C# の `WindowsNotificationJsonBuilder` が出さないキー（`comboBoxes`、`appLogo`、`heroImage`、`inlineImage`、`audio.type` / `event` / `uri`、`progress.title`）も読む（JSON を手で書いている利用者のため）。

| キー | 型 | ビルダーへ | 1.x と同じにする点 |
|---|---|---|---|
| `title` / `body` / `attribution` / `heroImage` / `inlineImage` | 文字列 | `set_title` / `set_body` / `set_attribution` / `set_hero_image` / `set_inline_image` | 無ければ呼ばない |
| `tag` / `group` | 文字列 | `set_tag` / `set_group` | 同上 |
| `scenario` | 文字列 | `set_scenario` | `"reminder"`→1、`"alarm"`→2、`"urgent"`→3、`"incomingCall"`→4、**それ以外は呼ばない**（既定のまま。範囲外の値をネイティブに渡さない） |
| `duration` | 文字列 | `set_duration(1)` | `"long"` のときだけ。それ以外は呼ばない |
| `buttons` | 配列 | 要素ごとに `add_button(label, invokeUri, args があれば 1, …)` と `add_button_argument(i, k, v)` | `label` は必須。`args` は文字列の値だけの object。**`args` と `invokeUri` の両方があればそのまま両方渡し、ネイティブの検証で 7 にする**（1.x と同じ結果） |
| `textBoxes` | 配列 | `add_text_input(id, placeholder, title)` | `id` は必須。`placeholder` / `title` のどちらかがあれば、無い方は `""`。どちらも無ければ両方 `NULL`（1.x の `AddTextBox(id)` と `AddTextBox(id, p, t)` の使い分け） |
| `comboBoxes` | 配列 | `add_combo(id, title, defaultSelection)` と `add_combo_item(i, id, label)` | `id`、item の `id` / `label` は必須。`title` / `defaultSelection` は無ければ `NULL` |
| `appLogo` | object | `set_app_logo(uri, crop)` | `uri` は必須。`crop` は `"circle"` のときだけ 1、それ以外は 0 |
| `audio` | object | `set_audio(kind, event, uri, loop)` | `type` は `"mute"`→1、`"uri"`→2（`uri` が無ければ 7）、それ以外→0。`event` は文字列のまま渡す（ネイティブの対応は 1.x と同じ、`WindowsNotificationBuilder.h:47-54`）。`loop` は bool。**`src` は読まない**（J-5） |
| `progress` | object | `set_progress(title, value, valueStr, status)` | `value` は無ければ 0.0。文字列は無ければ `NULL` |
| `timestamp` | 数 | `set_timestamp(秒 × 1000)` | **秒で受ける**（J-2）。1.x と同じく小数は切り捨てて整数秒にしてから 1000 倍する。`long` にできない・範囲外は 7 |
| `expiration` | 数 | `set_expiration(秒)` | 整数に切り捨て |
| `expiresOnReboot` | bool | `set_expires_on_reboot(1)` | 真のときだけ |
| そのほかのキー | — | 無視 | 1.x と同じ |

- 検証（ボタン 6 個以上、ループ音で `duration` が `"long"` でない、`args` と `invokeUri` の両方）は C# ではせず、ネイティブの show / schedule に任せる（規則は 1.x と同じで、結果も 7）。公開の `WindowsNotificationJsonBuilder.Validate` はそのまま残す
- **誤りの errorCode**:
  - JSON の構文の誤り、最上位が object でない → **3**（1.x と同じ `Invalid JSON payload`）
  - 必須のキーが無い、既知のキーの型が違う（例: `"title": 1`、`"buttons": {}`）→ **7**。1.x では WinRT の `GetNamedString` などが例外を投げ、5 になっていた（5.4）
- 読み取りは `WindowsNotificationJsonReader`（汎用の JSON リーダー、`MacClipboardJsonReader` と同じ形）と `WindowsNotificationContentPlan`（JSON → ビルダーの呼び出しの並び）の 2 つの純粋な関数にする。ネイティブを呼ばないので層 1 で試せる
- 公開の `WindowsNotificationPayload.Timestamp` と `Expiration` の XML コメントに、単位（Unix 秒、相対秒）を書く。値は変えない

### 4.3 そのほかの引数の変換

| メソッド | 2.0.0 へ |
|---|---|
| `Initialize` | 5.5 の流れ。非パッケージなら `runtime_initialize(0x00010007)` → `manager_create`。パッケージ（`isPackaged = true`）なら `runtime_initialize` を呼ばない（J-8）。`options.is_unpackaged = isPackaged ? 0 : 1`、`display_name` / `icon_uri` は UTF-8（`null` は `NULL`、ネイティブが `""` として扱い、非パッケージなら 7） |
| `ScheduleNotification` の `scheduledTimeUnixMs` | そのまま `schedule` の `unix_ms`（元からミリ秒） |
| `CancelScheduledNotification` / `RemoveNotificationsByTag` の `tag` / `group` | UTF-8。`null` は `NULL`（ネイティブで `""`、1.x と同じ） |
| `UpdateNotificationProgress` | `ProgressUpdate` 構造体に 6 つを入れる。`valueStr` / `status` の `null` は `NULL`（ネイティブが `""` として設定する。5.4） |
| `SetBadge` | **`value < -6` なら、初期化の確認より先に C# で 7 を返す**（1.x の判定の順を保つ）。それ以外は `set_badge` |
| `RemoveNotificationById` | そのまま |

### 4.4 結果の変換

| 状況 | 結果 |
|---|---|
| ネイティブの戻り値 0 | `Success` |
| ネイティブの戻り値 1〜8 | `Failure(code)`。値はそのまま（1.2） |
| manager のハンドルが無い（未初期化・`OnDestroy` の後） | ネイティブを呼ばず `Failure(1)`（1.x のネイティブも 1） |
| JSON の誤り（4.2） | 3 か 7 |
| DLL が無い・関数が無い・形式が違う・major が 2 でない（J-6） | `Failure(-4)`。文言は `Native library unavailable` |
| そのほかの C# 側の例外（`OutOfMemoryException` など） | `Failure(5)`。ログに例外の型を出す |
| `GetAllNotifications` の成功 | list から `[{"id":N,"tag":"…","group":"…"}]` を組み立てる。**1.x のネイティブと同じ文字列**（キーの順、空白なし、エスケープの規則。native-toolkit `1.11.0:windows/WindowsLibrary/WindowsNotificationManager.cpp` の GetAll の実装に合わせ、実装時に行番号を確かめる） |
| `GetNotificationSetting` | 成功なら値を enum に（`Enum.IsDefined` でないものは `Unknown`）。失敗・未初期化・Editor・`-4` は `Unknown`（1.x と同じ） |

`WindowsNotificationResult` の文言の対応に `-4` を足す（`Unknown error (-4)` にならないように）。公開の型もメソッドも増やさない。

### 4.5 活性化のコールバック（J-7）

1. 受け口（OS のスレッド）: `ntk_notification_activation_raw_arguments` でバイト列を写し、UTF-8 の `string` にする（コールバックから戻ると無効になるため、その場で写す）
2. `s_dispatcher`（`Awake` でメインスレッドから `UnityMainThreadDispatcher.Instance` を読んで入れておく static）の `Enqueue` に、`_instance?.NotificationInvoked?.Invoke(args)` を積むだけにする。`s_dispatcher` が `null` なら、ログを出して捨てる。**ネイティブのスレッドで `UnityMainThreadDispatcher.Instance` の getter に触らない**（無ければメインスレッド以外で GameObject を作るため）
3. 受け口の全体を try/catch で囲み、例外をネイティブに返さない（ネイティブは捨てるが、ログに残す）。close / free を受け口から呼ばない、待たない（`N.h:98-102`）
4. `NotificationInvoked` の引数は 1.x と同じ JSON テキスト（`raw_arguments`）

`user_data` / `release` を使わない理由: 受け口は static で、登録ごとの状態を持たない。delegate は static readonly で Player の生存中ずっと生きているので、close や free の後に配送中の活性化が 1 回走っても、解放済みのものに触らない。Editor ではネイティブを読まない（J-4）ので、ドメインリロードで delegate が消える場面が無い。

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | ガード | 内容 |
|---|---|---|---|
| 既存変更 | `Runtime/Notification/WindowsNotificationManager.cs` | 今のまま `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | 1.x の `DllImport` と GetAll のバッファ処理を削除。`WindowsNotificationCApi` を呼ぶ。manager / runtime のハンドルを持つ。実行時の `Application.platform` の確認は残す（J-4）。公開 API はそのまま |
| 新規作成 | `Runtime/Notification/WindowsNotificationCApi.cs` | 型は `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`、`DllImport` と呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR` | `internal static`。4.1 の入れ子の構造体、delegate、`extern`、UTF-8 の入出力、list と activation の読み取り。Editor では呼び出しがネイティブへ行かない |
| 新規作成 | `Runtime/Notification/WindowsNotificationJsonReader.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | `internal`。汎用の JSON リーダー（`MacClipboardJsonReader` と同じ形、型名に `Windows` 接頭辞） |
| 新規作成 | `Runtime/Notification/WindowsNotificationCApiMapping.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | `internal static`。4.2 の JSON → ビルダーの呼び出しの並び（`WindowsNotificationContentPlan`）、4.4 の GetAll の JSON の組み立て、結果と例外の変換、J-6 の版判定。ネイティブを呼ばない |
| 既存変更 | `Runtime/Notification/WindowsNotificationResult.cs` | 今のまま | 文言の対応に `-4` を足す |
| 既存変更 | `Runtime/Notification/WindowsNotificationPayloads.cs` | 今のまま | `Timestamp` / `Expiration` の単位と、`Audio.Src` が効かないこと（J-5）を XML コメントに書く |
| 既存変更 | `Plugins/Windows/VERSION.txt` | — | Dialog の J-6 の追加のピン（1.x）が Clipboard だけのためになる旨をコメントに書く（キーは変えない） |
| 新規作成 | `Tests/Runtime/WindowsNotificationCApiMappingTests.cs` | asmdef が Editor だけ（既存の `WindowsNotificationTests.cs` と同じ） | 層 1（7.1） |
| 既存変更 | `Tests/Runtime/WindowsNotificationTests.cs` | 今のまま | コード 8 と `-4` の文言のテストを足す |
| 新規作成 | `Tests/PlayMode/WindowsNotificationManagerEditorTests.cs` | `#if UNITY_EDITOR` | 層 2a（7.2） |
| 既存変更 | `Tests/PlayMode/WindowsNotificationSamplePlayerTests.cs` | 今のまま | W-04 の `[Ignore]` を外す。W-11 と 7.3 の追加 |
| 新規作成 | `artifact/windows/notification/designs/2026-09-27-windows-notification-ui-test-plan-v2.md` | — | v1 は残す。W-04 / W-11 の保留を解き、4 章の「2.0.0 で変わる期待値」を本計画に合わせる（2 回目の init は成功のまま、timestamp は秒のまま） |
| 新規作成 | `artifact/windows/notification/issues/audio-src-is-ignored.md` | — | J-5 |
| 既存変更 | `artifact/windows/notification/issues/unreachable-notification-apis.md` | — | 7.3 で 3 API が非パッケージで 8 を返すことを実機で確かめたら、その結果を書く |
| 既存変更 | `agent-rules/coding-rules/testing.md`、`artifact/topics/windows-c-abi-2/README.md` | — | 層 2a の追加、Notification の本数、README の「Notification・振る舞いが変わる」（2 回目の init と timestamp は公開 API では変わらない）と「ドメインリロード」（Notification は Editor でネイティブを読まないので対象外） |
| 非変更 | `Runtime/Notification/WindowsNotificationJsonBuilder.cs`、`Runtime/Common/UnityMainThreadDispatcher.cs`、`Runtime/UI/Windows/Notification/*`、`Runtime/Resources/UI/Windows/Notification/*`、`Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | — | 公開 API・共通部品・サンプルは変えない |

### 5.2 判断（開発者の承認済み、2026-09-27）

| # | 内容 | 決定 |
|---|---|---|
| J-1 | JSON を受ける公開 API | 残す。C# で 1.x の JSON の形を読み、ビルダーに渡す（4.2） |
| J-2 | `timestamp` の単位 | 公開の JSON では秒のまま。C# で 1000 倍する |
| J-3 | 2 回目の `Initialize` | manager があれば、ネイティブを呼ばずに `Success`。引数の変更は無視（1.x と同じ）。`OnDestroy` の後に新しい Manager で呼んだときは、初回として扱う |
| J-4 | Editor で呼ぶ | 1.x のまま何もしない（per-call callback もイベントも出さない、`GetNotificationSetting` は `Unknown`）。`DllImport` は `&& !UNITY_EDITOR` の中だけ。DLL を Editor で読まないので、ドメインリロードの後片付けは要らない |
| J-5 | `Audio.Src` | 移行では読まない（1.x と同じ）。別の issue に記録する |

### 5.3 判断（本計画で決めたもの）

| # | 内容 | 決定 |
|---|---|---|
| J-6 | DLL と C# の版 | `Initialize` の最初のネイティブ呼び出しの前に `(ntk_version() >> 16) == 2` を 1 回確かめる。違えば以後ネイティブへ渡さず `-4`。`DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` も `-4`（Dialog の J-7 と同じ考え方。コードは共有しない） |
| J-7 | コールバックの登録 | static の delegate、`user_data` / `release` は `NULL`、dispatcher は `Awake` で static に取っておく（4.5） |
| J-8 | パッケージアプリ | `runtime_initialize` を呼ばない（`NativeToolkit/Notification.h:246-249` は、パッケージアプリから呼ぶとブートストラッパーの失敗になると書く）。1.x は呼んでいた。実機では確かめられないので未検証と明記する |
| J-9 | 後片付け | `OnDestroy`（`_instance == this` のとき）で `manager_free` → `runtime_free`。ハンドルを `IntPtr.Zero` に戻す。`Application.quitting` でも `OnDestroy` は来るので、別には拾わない |
| J-10 | IL2CPP | Dialog の J-10 と同じ。3 機能を移したあとに 1 回流す |

### 5.4 既知の差分（意図したもの）

既存の公開 API は変えないが、次の振る舞いは変わる。XML コメントに書き、実装結果ファイル経由で次の版のマニュアルへ引き継ぐ。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| ボタンのクリック（非パッケージ、アプリが動いている） | 届かない（`CustomActivator` を書かない） | `NotificationInvoked` が来る |
| レジストリ | `CLSID\{clsid}\LocalServer32`、`AppUserModelId\<displayName>` の `DisplayName` / `IconUri`、ショートカット | 加えて `CustomActivator` を書く |
| JSON の必須キーが無い・型が違う | 5（WinRT の例外） | 7 |
| 通知が無効で、JSON の構文も誤り | 2（有効かを先に見た） | 3（C# が先に読む） |
| `timestamp` が範囲外（±約 922 兆 ms を超える秒） | `time_t` への変換のまま | 7 |
| `GetAllNotifications` の結果が長い | 黙って切り詰めて成功 | 切り詰めない（1M wchar を超えると 5） |
| `UpdateNotificationProgress` の `valueStr` / `status` が `null` | その項目を変えない | `""` にする（ネイティブの仕様。C# では保てない） |
| DLL が無い・関数が無い・形式が違う | 例外が呼び出し側に出た | 例外は出ず `-4` の結果 |
| そのほかの C# 側の例外 | 呼び出し側に出た | `5` の結果 |
| `OnDestroy` の後 | bootstrap を shutdown しない | `MddBootstrapShutdown` を 1 回呼ぶ |
| パッケージアプリの `Initialize`（J-8） | `initWinAppSdk` を呼んだ | 呼ばない（未検証） |
| 対になっていないサロゲートを含む文字列 | UTF-16 のまま渡した | `Encoding.UTF8` で U+FFFD に置き換わる。活性化の引数も、ネイティブが U+FFFD にする |
| close の後に届く活性化 | 1.x も 1 回走りえた | 同じ。`_instance` が無ければ捨てる |

### 5.5 呼び出しの流れと契約

**`Initialize`**

1. `Application.platform != WindowsPlayer` なら何もせずに戻る（J-4）
2. manager があれば `Success` を出して終わる（J-3）
3. 出力のハンドルを `IntPtr.Zero` で初期化する（最初の P/Invoke より前）
4. J-6 の版の確認。`-4` ならそれを結果にして 8 へ
5. 非パッケージなら `runtime_initialize(0x00010007, out runtime)`。失敗ならその errorCode（`HRESULT_FAILURE` なら system code の HRESULT をログに出す）を結果にして 8 へ
6. `ManagerOptions` を組み立て（`on_invoked` は 4.5 の受け口、`user_data` / `release` は 0）、`manager_create`。コールドスタートの活性化は、この中で呼び出しスレッドに届き、キューに積まれる
7. 失敗なら、5 で作った runtime を `runtime_free` する
8. **catch の外で**、per-call callback → `NotificationOperationCompleted` の順に 1 回ずつ出す（1.x と同じ順）

**`ShowNotification`（`ScheduleNotification` も同じ）**

1. プラットフォームの確認（J-4）
2. manager が無ければ `Failure(1)`（1.x と同じく、JSON を読むより先）
3. JSON を読み、ビルダーの呼び出しの並びを作る（4.2）。誤りなら 3 か 7
4. `content_create` → 並びを順に呼ぶ（どれかが失敗したら、その errorCode）→ `show`
5. `finally` で `content_free` と、確保した UTF-8 をすべて解放する
6. catch の外で結果を 1 回出す

try の形は Dialog v5 の 5.5 と同じ（finally を内側の try に置き、外側で catch する。解放の失敗も結果になる）。

- 捕まえるのはネイティブ呼び出しと変換だけ。**購読者が投げた例外は、1.x と同じく try/catch で受けて `LogError` する**（1.x の `FireResult`、`WindowsNotificationManager.cs:470-478`。Dialog とは違い、1.x の Notification は購読者の例外を外に出していなかったので、それを保つ）
- スレッド: 同期。呼び出したスレッドで結果まで出す。活性化だけは dispatcher 経由
- 同じ manager に対して、`OnDestroy` の close / free と、ほかの操作を同時に行わない（`WindowsLibraryCApi` の設計 `:153`）。Manager の操作はメインスレッドから呼ぶ前提で、1.x と同じく強制はしない

### 5.6 実装順

各段階の終わりに層 0（`scripts/verify_unity_windows.sh` の Player ビルド）を流す。

1. `WindowsNotificationJsonReader`、`WindowsNotificationCApiMapping` と層 1 のテスト。**1.x のまま**、層 2b に 7.3 の追加のうち 1.x で通るもの（JSON の全キー、3 API の 8、`SetBadge(-7)` の 7）を足し、1.x の結果を期待値として記録する
2. `WindowsNotificationCApi`
3. `WindowsNotificationManager` の置き換えと層 2a のテスト
4. 層 2b: W-01〜W-10 と、手順 1 で記録したテストが期待値を変えずに通ること。W-04 の `[Ignore]` を外し、W-11 を書く（`--include-destructive`）
5. Dialog の D-09〜D-12 と、Clipboard の Player テストを流し直す（manager_create がメインスレッドで MTA を試みるため。1.x と同じはずだが確かめる、親課題 README「STA / MTA の分離」）
6. 3 機能を移したあとで IL2CPP（J-10）
7. 実装結果ファイル（`artifact/windows/notification/results/*-implementation-result*.md`）に、5.4 の表、`-4` の意味を書き写す（write-manual が読む）

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | 初期化の前に操作する | C# | `Failure(1)`（`GetNotificationSetting` は `Unknown`） | 2b（新しい Manager は作れないので、`OnDestroy` の後で確かめる。7.3） |
| E-2 | 非パッケージで表示名かアイコンが空 | ネイティブ | 7 | 2b（7.3） |
| E-3 | bootstrap の失敗 | ネイティブ | 5、ログに HRESULT | 1（変換）。起こせない |
| E-4 | 2 回目の `Initialize`（J-3） | C# | `Success`、ネイティブを呼ばない | 2b（W-01。SetUp で初期化済み） |
| E-5 | 通知が無効 | ネイティブ | 2 | 起こせない（設定を変えるテストはしない） |
| E-6 | JSON の構文の誤り | C# | 3 | 1、2b |
| E-7 | JSON の必須キーが無い・型が違う | C# | 7 | 1、2b |
| E-8 | 検証の誤り（ボタン 6 個など） | ネイティブ | 7 | 2b |
| E-9 | 進捗の通知が無い | ネイティブ | 4 | 2b（W-08） |
| E-10 | 非パッケージで SetBadge / RemoveById / GetAll | ネイティブ | 8 | 2b（7.3） |
| E-11 | `SetBadge` の値が -6 より小さい | C# | 7（初期化の確認より先） | 1、2b |
| E-12 | DLL が無い・major が 2 でない（J-6） | C# | `-4` | 1（版判定、例外の変換）。実物は起こせない |
| E-13 | WinRT の失敗 | ネイティブ | 5 | 起こせない |
| E-14 | C# 側の予期しない例外 | C# | 5、結果は 1 回 | 1（例外の変換） |
| E-15 | Editor で呼ぶ（J-4） | C# | 何もしない | 2a |
| E-16 | close の後に届く活性化 | ネイティブ | 捨てるか、Manager があれば `NotificationInvoked` | 起こせない。4.5 の構造をコードレビューで確かめる |

## 7. テスト方針

### 7.1 層 1（EditMode）

- 構造体 2 つの大きさと全フィールドの位置（`Marshal.OffsetOf`、1.3 の表）
- JSON リーダー: 構文の誤り、最上位が配列、エスケープ、数値の形、UTF-16 のサロゲートペア
- JSON → ビルダーの呼び出しの並び: 4.2 の表の全キー、`scenario` / `audio.type` / `crop` の未知の値、`textBoxes` の `NULL` と `""` の使い分け、`args` と `invokeUri` の両方、必須キーの欠落（7）、型の違い（7）、`src` を読まないこと、`timestamp` の秒 → ミリ秒（小数の切り捨て、範囲外で 7）
- `WindowsNotificationJsonBuilder` が出す JSON を読むと、元のペイロードと同じ呼び出しの並びになること（公開の組み立てと読み取りの往復）
- GetAll の JSON の組み立て（0 件、tag / group のエスケープ、1.x と同じ文字列）
- 版判定（J-6）と例外の変換
- `WindowsNotificationResult` のコード 8 と `-4` の文言

### 7.2 層 2a（PlayMode・Editor、`#if UNITY_EDITOR`）

- J-4: 各メソッドを呼んでも、per-call callback も `NotificationOperationCompleted` も数フレームの間に出ないこと。`GetNotificationSetting` が `Unknown` であること

### 7.3 層 2b・層 3（PlayMode・Player、`WindowsNotificationSamplePlayerTests`、`Destructive`）

- **W-01〜W-10 を期待値を変えずに通す。**
- **W-04 の `[Ignore]` を外し、通す。** 前提の確認（`CustomActivator` とその CLSID の `LocalServer32` がこの exe、`:138-145`）は、移行後の `Initialize` が書くので満たされる
- **W-11 を書く**（UI テスト計画 v1 の 1 章 `:34`）
- 追加（Manager を直接呼ぶ。手順 1 で 1.x のまま流して期待値を記録する）:
  - 4.2 の全キーを含む JSON で `ShowNotification` が成功し、通知センターに title が出ること
  - JSON の構文の誤りで 3、ボタン 6 個で 7
  - 非パッケージで `SetBadge(1)` / `RemoveNotificationById(1)` / `GetAllNotifications` が 8（issue `unreachable-notification-apis.md` の未検証を確かめる）
  - `SetBadge(-7)` が 7
- 移行後だけ（1.x とは期待値が違う、5.4）: JSON の必須キーの欠落で 7
- `ntk_last_system_code` の結び付け: 直接 1 回呼び、例外なく戻ること
- Mono で走る。IL2CPP は J-10

### 7.4 手動

なし。

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。手動に残す項目は無い。
起こせない失敗（E-3、E-5、E-12〜E-14、E-16）は、変換を層 1 で押さえ、構造をコードレビューで確かめる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- 通知センターを外から開いて読む PowerShell（`WindowsNotificationSamplePlayerTests` の `CenterScript`、既存）。通知センターは `EnumWindows` に出ないので、前面の窓から取る（UI テスト計画 v1）
- 前提の OS 設定: この PC の通知の設定が有効であること（無効なら show が 2）。応答不可（DND）が ON でも、通知センターには入る（W-03 で確認済み）
- 非パッケージの登録（レジストリ、ショートカット）は `Initialize` が HKCU に書く。**表示名（`Application.productName`、"Unity NativeToolkit"）が同じ別のアプリと、クリックを奪い合う**ので、テスト中に同じ名前のアプリを動かさない
- テスト用 Player のファイアウォールの規則（既存）

### 8.3 呼び出し側を止める OS の画面

なし。どの操作もモーダルの画面を出さない（`OpenNotificationSettings` は設定アプリを別に開くだけ。テストでは押さない）。
