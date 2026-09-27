# Windows Notification 実装計画 v7（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Notification（Windows） |
| 種別 | **移行。機能追加ではない。** 1.x の C ABI（dist 1.11.0）から 2.0.0（dist 1.12.0）へ P/Invoke 層を書き換える |
| 前の版 | `2026-06-06-windows-notification-design-v2.md`（1.x での新規実装の設計）、`2026-09-27-windows-notification-design-v6.md`（本書の前の版） |
| 親課題 | `artifact/topics/windows-c-abi-2/README.md`（Notification は「Notification・振る舞いが変わる」） |
| 移行前の基準 | `50fe7bb`（`feature/UNT-12`）。W-01〜W-10（W-04 は保留）が 1.x で通る |
| 前提 | Dialog の移行（`artifact/features/dialog/designs/2026-09-27-windows-dialog-design-v5.md`）の手順 1（J-6: DLL を 2 本置く、J-11: PDB）が済んでいること |
| 方針 | **既存の公開 API（`WindowsNotificationManager` のメソッド 12 本・イベント 3 本・定数、`WindowsNotificationResult`、`WindowsNotificationJsonBuilder`、ペイロードの型、JSON の形と単位、エラーの判定の順）を変えない**（親課題 Q-4）。サンプル・UXML は触らない。W-01〜W-10 がそのまま移行の検証になり、保留していた W-04 と W-11 が加わる。変わる振る舞いは 5.4「既知の差分」に列挙したものだけ |
| DLL | `windows-native-toolkit-capi-2.0.0.dll`（Dialog と同じ。`Microsoft.WindowsAppRuntime.Bootstrap.dll` を通常のインポートで読むので、隣に置く。今も置いてある） |
| 開発者の判断（2026-09-27） | J-1（JSON の公開 API を残し C# で読む）、J-2（`timestamp` は秒のまま）、J-3（2 回目の `Initialize` はネイティブを呼ばずに成功）、J-4（Editor では何もしない）、J-5（`Audio.Src` は移行では読まない） |
| レビュー | `artifact/features/notification/reviews/2026-09-27-windows-notification-design-review-v3.md`、`...-review-v4.md`、`...-review-v5.md`、`...-review-v6.md` |

## v6 からの主な変更

レビュー v6（A1 / A2 なし）の B と C を反映した。再レビューはしない。

- B: 作り直しのテストを別の Player で流すスクリプトの書き方（5.1）。GetAll の per-call の例外のテスト（7.3）
- C: 小数表記の境界は 323 個、配列の要素は object、`LogError` は 4 種類（5 箇所）、sparse パッケージ（8.1）

## v5 からの主な変更（v6）

レビュー v5 の指摘を反映した。

- A1:
  - 型を見るキーを「1.x がどこかで読むキー（`ValidatePayload` を含む）」にし、`null` の規則もそれに合わせた。`audio.loop` はどの `type` でも読む（4.2）
  - 小数表記で 0 に丸まる値を既知の差分にした（4.2、5.4）
  - パッケージの判定を 3 つに分けた（J-8）
- B:
  - 作り直しのテストは専用のカテゴリにし、スクリプトで別の Player の実行に分ける（7.3、5.1）
  - 1.x の `LogError` の箇所を 4 つにした（5.5）
- C: 深さの数え方（コンテナ 512 個）、DLL が無いときの `SetBadge(-7)`

## v4 からの主な変更（v5）

レビュー v4 の指摘を反映した。

- A1:
  - `null` の payload は、1.x ではプロセスが落ちていた。5.4 に書き、テストは移行後だけにする
  - 設定の取得が例外になったときの 5 → 2 を 5.4 に書く
  - 1.x がその分岐で読まないキーは、型も見ない（4.2）
  - 数値は構文を読む段階で変換し、深さの上限は 1.x に合わせる（4.2）
  - runtime を呼ぶかは、実際にパッケージのプロセスかどうかで決める（J-8）
- B:
  - 設定の事前確認は、SetUp の `Initialize` の後に置く（8.2）
  - 作り直しの流れは別の fixture にして最後に流す（7.3）
  - 結果として返す失敗は `LogError` にしない（5.5）
  - 判定の順と活性化の購読者の例外を、層 1 の純粋な関数で試す（7.1）
  - `SetBadge(-7)` の判定の位置（4.3、7.2）
- C: DLL が無いときの行を全メソッドに、5.1 の漏れ、`expiration` の範囲外、埋め込みの NUL、秒の範囲、配列の要素の `null`

## v3 からの主な変更（v4）

レビュー v3 の指摘を反映した。

- A1:
  - 活性化の購読者の例外を、積んだ処理の中でも捕まえる（4.5）
  - 予約時刻の範囲外（5.4）
  - **「通知が無効なら 2」の判定の順は、JSON を読む前に `get_setting` を呼んで保つ**（J-11、5.5）
  - `ShowNotification(null)` は 3（1.x と同じ）
  - runtime は `OnDestroy` まで持ち続ける（J-9）
  - JSON の `null` は 7（4.2）
  - サブキーをすべて表に書いた（4.2）
  - GetAll の文字列の規則を実測に基づいて書いた（4.4）
  - ボタンの引数の順の差分（5.4）
- A2: Manager の呼び出し箇所を `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` で囲む（3 章、5.1）
- B:
  - E-1 / E-2 と bootstrap の再初期化を 1 本の流れで確かめる（7.3）
  - NULL で安全な `extern` を全部呼ぶ（7.3）
  - `FireResult` の形とイベントの種類（5.5）
  - 確かめないもの（8.1）
  - fixture（7.3、UI テスト計画 v2）
  - 手順 1 の記録の対象（5.6）
  - 破棄された dispatcher（4.5）
  - 設定の事前確認（8.2）
  - 1e400 と重複キー（4.2、5.4）
- C: `extern` の本数、引用行、E-1 の言い方、J-6 と 5.5、5.1 のファイル、切り捨ての向き、tag / group の null、サロゲート

## 1. native-toolkit 側（2.0.0 の C API）

出典: native-toolkit `dist/1.12.0/windows/include/NativeToolkitC/Notification.h`（以下 `N.h`）、`Common.h`。
実装: `windows/WindowsLibraryCApi/src/Notification/`（`NotificationCApi.cpp`、`NotificationContentCApi.cpp`、`NotificationConvert.cpp`、`NotificationRuntime.cpp`）、`windows/WindowsLibrary/src/Notification/`。

### 1.1 使う関数

| # | 関数（`N.h` の行） | 1.x の対応 |
|---|---|---|
| OP-01 | `ntk_notification_runtime_initialize(uint32_t major_minor, ntk_notification_runtime** out)`（154）/ `_runtime_free`（161） | `initWinAppSdk` |
| OP-02 | `ntk_notification_manager_create(const ntk_notification_manager_options*, ntk_notification_manager** out)`（179） | `initNotificationManager` |
| OP-03 | `ntk_notification_manager_close`（201）/ `_free`（204） | `uninitNotificationManager` |
| OP-04 | `ntk_notification_content_create` / `_free` と `content_set_*` / `content_add_*`（309-375、22 本）、`ntk_notification_show(manager, content)`（213） | `showNotification` |
| OP-05 | 同じビルダーと `ntk_notification_schedule(manager, content, int64_t unix_ms)`（221） | `scheduleNotification` |
| OP-06 | `ntk_notification_cancel_scheduled(manager, tag, group)`（225） | `cancelScheduledNotification` |
| OP-07 | `ntk_notification_update_progress(manager, const ntk_notification_progress_update*)`（233） | `updateNotificationProgress` |
| OP-08 | `ntk_notification_set_badge(manager, int32_t)`（237） | `setBadge` |
| OP-09 | `ntk_notification_remove_by_id(manager, uint32_t)`（241） | `removeNotificationById` |
| OP-10 | `ntk_notification_remove_by_tag(manager, tag, group)`（245） | `removeNotificationsByTag` |
| OP-11 | `ntk_notification_remove_all(manager)`（249） | `removeAllNotifications` |
| OP-12 | `ntk_notification_get_all(manager, ntk_notification_list**)`（255）と `ntk_notification_list_count` / `_id_at` / `_tag_at` / `_group_at` / `_free`（270-280） | `getAllNotifications` |
| OP-13 | `ntk_notification_get_setting(manager, ntk_notification_setting*)`（259） | `getNotificationSetting`。Show / Schedule の前の確認にも使う（J-11） |
| OP-14 | `ntk_notification_open_settings(manager)`（263） | `openNotificationSettings` |
| OP-15 | コールバックの中で `ntk_notification_activation_raw_arguments(activation, size_t*)`（288-299） | 1.x のコールバックの `argsJson` |

共通: `ntk_version()`、`ntk_last_system_code()`。使わないもの: `ntk_notification_manager_set_invoked_handler`（J-3 でハンドラを差し替えない）、`ntk_notification_activation_value_count` / `_key_at` / `_value_at`（`raw_arguments` だけで 1.x の引数になる）。

### 1.2 型と値

- `ntk_notification_error`（`N.h:34-45`）: `NONE`(0) / `NOT_INITIALIZED`(1) / `DISABLED`(2) / `INVALID_PAYLOAD`(3、予約・返らない) / `PROGRESS_NOT_FOUND`(4) / `HRESULT_FAILURE`(5) / `BADGE_FAILED`(6) / `INVALID_PARAMETER`(7) / `NOT_SUPPORTED`(8)。**1.x の errorCode 1〜8 と値も意味も同じ**なので、そのまま `WindowsNotificationResult.ErrorCode` に入れる
- `ntk_last_system_code()`: 多くの失敗では HRESULT ではなく、エラー値そのもの（1、5、7、8 など）が入る（`WindowsNotificationApi.cpp:84-90`）。本物の HRESULT が入るのは `runtime_initialize` の失敗とメモリ不足（`0x8007000E`）だけ。本計画ではログにだけ使う
- 列挙（すべて `int32_t`、関数の引数で渡す）: scenario（DEFAULT=0、REMINDER=1、ALARM=2、URGENT=3、INCOMING_CALL=4）、audio_kind（EVENT=0、MUTE=1、URI=2）、duration（SHORT=0、LONG=1）、logo_crop（NONE=0、CIRCLE=1）、setting（ENABLED=0〜DISABLED_BY_MANIFEST=4）。範囲外は `INVALID_PARAMETER`
- ビルダーの文字列の `NULL` と `""`: title / body / 画像 / attribution は `NULL` が「無い」、`""` が「空で在る」。tag / group の `NULL` は `""`。`add_button` の label、`add_text_input` / `add_combo` / `add_combo_item` の id（と item の label）は必須（`NULL` は `INVALID_PARAMETER`）
- `set_timestamp` は Unix **ミリ秒**で、±922,337,203,685,477 を超えると `INVALID_PARAMETER`。`set_expiration` は秒で検査なし
- `schedule` の `unix_ms` も同じ範囲を超えると `INVALID_PARAMETER`（`NotificationContentCApi.cpp:368-375`）
- 検証（ボタン 6 個以上、ループ音で duration が LONG でない、ボタンに引数と invokeUri の両方、kind=URI で uri が無い）は show / schedule のときに行い、`INVALID_PARAMETER`（`windows/WindowsLibrary/src/Notification/Domain/WindowsNotificationValidation.h:46-70`）。1.x と同じ規則
- show / schedule はまず `NOT_INITIALIZED`、次に通知の設定が有効でなければ `DISABLED` を返す（取得失敗の -1 も `DISABLED`、`WindowsNotificationManager.cpp:321-331`）
- `get_setting` は、取得に失敗すると `HRESULT_FAILURE`（`WindowsNotificationApi.cpp:317-320`）
- `cancel_scheduled` の空の tag / group はワイルドカード（1.x と同じ）

### 1.3 構造体（x64、`#pragma pack(push, 8)`、`CApiLayoutTest.cpp:57-77`）

| 構造体 | 大きさ | フィールドの位置 |
|---|---|---|
| `ntk_notification_manager_options` | 56 | struct_size 0、reserved0 4、on_invoked 8、user_data 16、release 24、is_unpackaged 32、reserved1 36、display_name 40、icon_uri 48 |
| `ntk_notification_progress_update` | 56 | struct_size 0、reserved0 4、tag 8、group 16、value 24（double）、value_string 32、status 40、sequence_number 48、reserved1 52 |

`struct_size` は 56 以上 4096 以下、`reserved*` は 0。`options` が `NULL` か大きさの範囲外のとき、`release` は呼ばれない。

### 1.4 ハンドルとコールバック

- runtime / manager / content / list はそれぞれの `_free` で解放する。`NULL` は何もしない。二重解放は未定義
- コールバック `ntk_notification_invoked_fn(void* user_data, const ntk_notification_activation*)`（`N.h:103-104`、`__cdecl`）は **OS が選んだスレッドで来る**（`N.h:9-12`）。コードの上では、非パッケージのコールドスタート（`-ToastActivated` 付きで起動）のとき `manager_create` の中で呼び出しスレッドに 1 回届く（`WindowsNotificationManager.cpp:419-429`）。**ただし `CustomActivator` を書いたあとのコールドスタートは native 側でも試していない**（`artifact/topics/windows-architecture/results/2026-09-27-windows-unpackaged-activation-finding.md` 3 章、native-toolkit リポジトリ）。本計画でも確かめない（8.1）
- `raw_arguments` は 1.x のコールバックの `argsJson` と同じ作り方のテキストを UTF-8 にしたもの（`N.h:287`、`CApiNotificationTest.cpp:350-369`）。activation とそこから読んだ文字列は、コールバックから戻るまでだけ有効
- close の後にも、配送中の活性化が 1 回走りうる（`N.h:198-199`）。止める仕組みは無い
- `release` は登録ごとにちょうど 1 回呼ばれる。`NULL` でよい

### 1.5 ランタイムと活性化の登録

- `runtime_initialize` は `MddBootstrapInitialize` を呼ぶ。2 回目（ロード済み・shutdown 待ち）は `NOT_SUPPORTED`。`runtime_free` の後、生きている manager が 0 になった時点で `MddBootstrapShutdown` を 1 回呼ぶ（`NotificationRuntime.cpp:43-83`）。shutdown の後なら再び初期化できる
- `manager_create` は呼び出しスレッドで `CoInitializeEx(COINIT_MULTITHREADED)` を行い、解除しない。既に STA なら `RPC_E_CHANGED_MODE` を受け入れて STA のまま（1.x と同じ、`WindowsNotificationManager.cpp:384`）
- 2 つ目の manager は `NOT_SUPPORTED`（close の後なら再び create できる）
- 非パッケージでは、HKCU に `CLSID\{clsid}\LocalServer32`、`AppUserModelId\<display_name>` の `DisplayName` / `IconUri` / **`CustomActivator`**（c9f4071b で追加）を書き、スタートメニューにショートカットを作る（`WindowsClassicActivator.cpp:363-454`）。**これで、動いている非パッケージのアプリにボタンのクリックが届くようになる**（1.x の不具合。`2026-09-27-windows-notification-ui-test-plan-v1.md` 2 章）

## 2. 既存の C# 実装

出典: `Runtime/Notification/WindowsNotificationManager.cs`、`WindowsNotificationJsonBuilder.cs`、`WindowsNotificationPayloads.cs`、`WindowsNotificationResult.cs`、`Runtime/Common/UnityMainThreadDispatcher.cs`。

- ガード: 4 ファイルとも `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`。ネイティブの呼び出しは `#if` ではなく、実行時の `Application.platform != RuntimePlatform.WindowsPlayer` で早く戻って止めている（`WindowsNotificationManager.cs:229` ほか 11 箇所）。**Editor では何もせず、per-call callback もイベントも出さない**
- `DllImport` 14 本。`DLL_NAME` は `DEVELOPMENT_BUILD` で `unity-windows-native-toolkit(-debug)` を切り替える（`:21-25`）。`CallingConvention` の指定は無い
- 公開 API（すべて同期。結果は同じ呼び出しの中で出す）:

| メソッド（行） | 引数と既定値 | 1.x の結果 |
|---|---|---|
| `Initialize`（`:224-242`） | `(bool isPackaged = false, string? displayName = null, string? iconUri = null, Action<WindowsNotificationResult>? onResult = null)` | 毎回 `initWinAppSdk(0x00010007)` と `initNotificationManager` を呼ぶ。**2 回目も検証なしで成功**（ネイティブがコールバックを差し替えるだけ） |
| `ShowNotification`（`:249-257`） | `(string jsonPayload, onResult)` | 判定の順: 1 → 2（設定が 0 以外。値が -1 のときも。取得が例外を投げたら 5）→ 3（JSON の構文）→ 7（検証）/ 5（組み立ての例外）（`1.11.0:...cpp:820-857`）。**`null` の payload は、初期化済みで有効ならプロセスが落ちた**（`hstring{ nullptr }` が `wcslen(NULL)` を呼ぶ。cppwinrt `base.h:3028-3030`、実測 `0xC0000005`） |
| `ScheduleNotification`（`:265-274`） | `(string jsonPayload, long scheduledTimeUnixMs, onResult)` | 同上（`1.11.0:...cpp:868-912`）。予定時刻は検査しない |
| `CancelScheduledNotification`（`:282-291`） | `(string tag, string group, onResult)` | 1、5 |
| `UpdateNotificationProgress`（`:303-312`） | `(string tag, string group, double value, string valueStr, string status, uint sequenceNumber, onResult)` | 1、4、5 |
| `SetBadge`（`:319-327`） | `(int value, onResult)` | **7（value < -6。初期化の確認より先）**、1、8（非パッケージ）、6 |
| `RemoveNotificationById`（`:334-342`） | `(uint notificationId, onResult)` | 1、8（非パッケージ）、5 |
| `RemoveNotificationsByTag`（`:350-359`） | `(string tag, string group, onResult)` | 1、5 |
| `RemoveAllNotifications`（`:365-373`） | `(onResult)` | 1、5 |
| `GetAllNotifications`（`:383-403`） | `(Action<string?, WindowsNotificationResult>? onResult)`。成功なら `[{"id":N,"tag":"…","group":"…"}]` の JSON | 1、8（非パッケージ）、5。4096〜65536 wchar のバッファを 5 回まで試す。ネイティブは切り詰めても成功を返していた |
| `GetNotificationSetting`（`:410-419`） | `()` → `WindowsNotificationSetting` | 未初期化・失敗・未定義の値は `Unknown`（-1） |
| `OpenNotificationSettings`（`:425-433`） | `(onResult)` | 1、5 |

- 結果の出し方: `FireResult`（`:465-479`）は、per-call callback と `NotificationOperationCompleted` を**1 つの try** の中で順に呼び、例外は `LogError(ex.Message)` で受ける（per-call が投げると、イベントは出ない）。`GetAllNotifications` は `NotificationOperationCompleted` を出さず、per-call → `GetAllNotificationsCompleted` を同じ形で出す（`:393-401`）。`GetNotificationSetting` はイベントを出さない
- イベント: `NotificationOperationCompleted(WindowsNotificationResult)`、`NotificationInvoked(string argsJson)`（dispatcher 経由、次の `Update` 以降）、`GetAllNotificationsCompleted(string?, WindowsNotificationResult)`
- `WindowsNotificationResult`: `Operation` / `IsSuccess` / `ErrorCode`（成功で 0）/ `ErrorMessage`。文言の対応は `WindowsNotificationResult.cs:42-53`（1〜8。8 はテストが無い）
- 公開の JSON の形（`WindowsNotificationJsonBuilder.cs:48-84`）と、1.x のネイティブが読んでいた形（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsNotificationManager.cpp:489-656,660-816`）は 4.2 の表。**`timestamp` は Unix 秒、`expiration` は相対秒**（`1.11.0:...cpp:607-612,640`）。`scheduledTimeUnixMs` はミリ秒
- コールバック: `[UnmanagedFunctionPointer(Cdecl)] delegate void NotificationInvokedCallback([MarshalAs(LPWStr)] string argsJson)` を static で保持（`:92-94,161`）。受け口 `OnNotificationInvoked`（`:483-504`）は `UnityMainThreadDispatcher.Instance.Enqueue(...)` だけを行う。**積んだラムダの中でも try/catch して `LogError` する**（`:490-497`）。**`Instance` の getter はスレッドセーフではなく、無ければその場で GameObject を作る**（`UnityMainThreadDispatcher.cs:24-36`。Manager の `Awake` が先に作る、`:200`）
- `OnDestroy` で、`_instance == this && _initialized` のときだけ `uninitNotificationManager` を呼ぶ（`:203-213`）。bootstrap は shutdown しない。ドメインリロードの処理は無い（Editor でネイティブを呼ばないので要らなかった）
- 既知の奇妙な点: `Audio.Src` は `"src"` として出るが、1.x のネイティブは読まない（`WindowsNotificationJsonBuilder.cs:126`、`1.11.0:...cpp:757-784`）。GetAll の再試行は、切り詰めた成功を見分けられない。`Initialize()` を既定の引数のまま呼ぶと、Player では必ず 7 になる（XML コメントは「iconUri は任意」と書く）

## 3. 実装制約

- `agent-rules/coding-rules/common.md`: Manager + Bridge、ネイティブが同期なら C# も同期、コールバックは dispatcher 経由（:217-221）、`Windows` 接頭辞、プラットフォーム間で共通化しない（JSON リーダーは `MacClipboardJsonReader` と同じ形の Windows 用を作る）
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、全メソッドの 1 行目に全引数の `Debug.Log`、英語の XML コメント
- コンパイルガードは二重にする（P5、前例は `WindowsClipboardManager.cs:5,823` と Dialog v5）: 型は `UNITY_STANDALONE_WIN || UNITY_EDITOR`、`DllImport` と、Manager の中の実際の呼び出し箇所は `UNITY_STANDALONE_WIN && !UNITY_EDITOR`。Manager の呼び出し箇所は `#else` で何もせずに戻す（J-4）。`Application.platform` の確認は `#if` の内側に残す
- 公開 API を変えない（基本情報の方針）

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

### 4.1 `DllImport`

- `DllImport("windows-native-toolkit-capi-2.0.0.dll", CallingConvention = CallingConvention.Cdecl)`、`internal static extern`（層 2b のテストが直接呼ぶ）。`DEVELOPMENT_BUILD` の切り替えは無くす
- 型の対応: ハンドル・文字列ポインタ・関数ポインタは `IntPtr`、`size_t` は `UIntPtr`、`int32_t` は `int`、`uint32_t` は `uint`、`int64_t` は `long`、`double` は `double`、戻り値の `ntk_notification_error` は `int`
- 文字列の入力は、Dialog と同じく `Encoding.UTF8` の NUL 終端バイト列を `AllocHGlobal` に置き、`finally` で全部解放する。出力（list の tag / group、activation の raw_arguments）は `IntPtr` と `out UIntPtr size` で受け、バイト列を写して UTF-8 で読む（`LPUTF8Str` の戻り値は使わない、親課題 README「マーシャリングの落とし穴」）
- 構造体は `WindowsNotificationCApi` の入れ子の `internal struct ManagerOptions` / `ProgressUpdate`（`[StructLayout(Sequential, Pack = 8)]`、`struct_size = (uint)Marshal.SizeOf<T>()`、`reserved*` = 0）
- コールバック: `[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void InvokedCallback(IntPtr userData, IntPtr activation)`。static readonly のフィールドで保持し、`Marshal.GetFunctionPointerForDelegate` で `options.on_invoked` に入れる。受け口は `[MonoPInvokeCallback(typeof(InvokedCallback))]` の static メソッド（IL2CPP のため）
- `user_data` と `release` は `IntPtr.Zero`（J-7）

`extern` の宣言（**46 本**。Common 2、ランタイム 2、マネージャー 3、ビルダー 22、操作 11、一覧 5、活性化 1。表は同じ型の関数を 1 行にまとめたが、`extern` は関数ごとに書く。実装時にヘッダーの宣言と突き合わせる）:

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

### 4.2 JSON の読み取り（J-1）

公開の `ShowNotification` / `ScheduleNotification` は今のまま JSON 文字列を受ける。C# で読み、ビルダーの呼び出しに変える。
**読む形と、キーが無い・未知の値のときの扱いは、1.x のネイティブ（`1.11.0:windows/WindowsLibrary/WindowsNotificationManager.cpp:489-816`）に合わせる**。C# の `WindowsNotificationJsonBuilder` が出さないキーも読む（JSON を手で書いている利用者のため）。

「必須」はキーが無ければ 7、「省略可」は無ければ下の既定。

**型を見るのは、1.x がどこかで実際に読むキーだけ**（組み立ての `BuildFromJson` / `BuildPayload` と、その前の検証 `ValidatePayload`、`1.11.0:...cpp:489-528` の両方を含む）。読むキーの型が違えば 7。**値が JSON の `null` なのも「型の違い」として 7**（1.x では `HasKey` が真になり、`GetNamedString` などが例外を投げていた。`1.11.0:...cpp:540-541`）。配列の要素が `null`（`"buttons":[null]`）も 7。
1.x が読まないキーは、型が違っても `null` でも誤りにしない（例: `audio.type` が `"mute"` なら `uri` / `event` を読まない、`"uri"` なら `event` を読まない。`1.11.0:...cpp:757-788`）。**`audio.loop` は `type` にかかわらず読む**（`ValidatePayload` が読む、`1.11.0:...cpp:494-501`）。

| キー | 型 | 必須 | ビルダーへ | 1.x と同じにする点 |
|---|---|---|---|---|
| `title` / `body` / `attribution` / `heroImage` / `inlineImage` | 文字列 | 省略可 | `set_title` / `set_body` / `set_attribution` / `set_hero_image` / `set_inline_image` | 無ければ呼ばない |
| `tag` / `group` | 文字列 | 省略可 | `set_tag` / `set_group` | 同上 |
| `scenario` | 文字列 | 省略可 | `set_scenario` | `"reminder"`→1、`"alarm"`→2、`"urgent"`→3、`"incomingCall"`→4、**それ以外の文字列は呼ばない**（既定のまま） |
| `duration` | 文字列 | 省略可 | `set_duration(1)` | `"long"` のときだけ |
| `buttons` | 配列（要素は object。違えば 7） | 省略可 | 要素ごとに `add_button` | 1.x は要素を `GetObject` で読む |
| `buttons[].label` | 文字列 | 必須 | `add_button` の label | — |
| `buttons[].args` | object（値は文字列） | 省略可 | `withArguments = 1` と、メンバーごとに `add_button_argument(i, k, v)`（文書の順） | 1.x は `JsonObject` の列挙順で渡していた（5.4） |
| `buttons[].invokeUri` | 文字列 | 省略可 | `add_button` の invokeUri | `args` と両方あればそのまま両方渡し、ネイティブの検証で 7 にする（1.x と同じ結果） |
| `textBoxes` | 配列（要素は object。違えば 7） | 省略可 | 要素ごとに `add_text_input` | 同上 |
| `textBoxes[].id` | 文字列 | 必須 | id | — |
| `textBoxes[].placeholder` / `.title` | 文字列 | 省略可 | placeholder / title | どちらかがあれば、無い方は `""`。どちらも無ければ両方 `NULL`（1.x の `AddTextBox(id)` と `AddTextBox(id, p, t)` の使い分け、`1.11.0:...cpp:568-583`） |
| `comboBoxes` | 配列（要素は object。違えば 7） | 省略可 | 要素ごとに `add_combo` | 同上 |
| `comboBoxes[].id` | 文字列 | 必須 | id | — |
| `comboBoxes[].title` / `.defaultSelection` | 文字列 | 省略可 | title / defaultSelection。無ければ `NULL` | — |
| `comboBoxes[].items` | 配列（要素は object。違えば 7） | 省略可 | 要素ごとに `add_combo_item(i, id, label)` | `1.11.0:...cpp:712-719` |
| `comboBoxes[].items[].id` / `.label` | 文字列 | 必須 | id / label | — |
| `appLogo` | object | 省略可 | `set_app_logo(uri, crop)` | — |
| `appLogo.uri` | 文字列 | 必須 | uri | — |
| `appLogo.crop` | 文字列 | 省略可 | `"circle"` のときだけ 1、それ以外は 0 | — |
| `audio` | object | 省略可 | `set_audio(kind, event, uri, loop)` | — |
| `audio.type` | 文字列 | 省略可 | `"mute"`→1（`uri` / `event` は読まない）、`"uri"`→2、それ以外・無し→0 | `1.11.0:...cpp:757-762` |
| `audio.uri` | 文字列 | `type` が `"uri"` のとき必須（無ければ 7）。それ以外では読まない | uri | `:769-778` |
| `audio.event` | 文字列 | `type` が `"mute"` / `"uri"` でないときだけ読む。省略可 | 文字列のまま渡す（無ければ `NULL`。ネイティブの対応は 1.x と同じ、`WindowsNotificationBuilder.h:47-54`） | `:782-788` |
| `audio.loop` | bool | **どの `type` でも読む**。省略可 | 0 / 1 を `set_audio` に渡す（mute でも渡す。新しいネイティブの検証は kind を問わず `loop` を見て、`duration` が long でなければ 7、`WindowsNotificationValidation.h:49`。ビルダーは mute のとき `loop` を捨てる、`WindowsNotificationBuilder.h:14`） | `ValidatePayload`（`:494-501`）と `ApplyAudio`（`:765`） |
| `audio.src` | — | — | **読まない**（J-5） | 1.x も読まない |
| `progress` | object | 省略可 | `set_progress(title, value, valueStr, status)` | — |
| `progress.value` | 数 | 省略可 | 無ければ 0.0 | — |
| `progress.title` / `.valueStr` / `.status` | 文字列 | 省略可 | 無ければ `NULL`。**キー名は 1.x の `valueStr`**（C ABI の `value_string` ではない） | `1.11.0:...cpp:651-654,802-809` |
| `timestamp` | 数 | 省略可 | `set_timestamp(秒 × 1000)` | **秒で受ける**（J-2）。**0 方向に切り捨てて**整数秒にしてから 1000 倍する（1.x の `static_cast<time_t>`、`1.11.0:...cpp:609`）。`long` にできない・範囲外は 7 |
| `expiration` | 数 | 省略可 | `set_expiration(秒)` | 0 方向に切り捨て。`long` に収まらなければ 7（1.x の `static_cast<int64_t>` は範囲外で値が決まらなかった） |
| `expiresOnReboot` | bool | 省略可 | 真のときだけ `set_expires_on_reboot(1)` | — |
| そのほかのキー | — | — | 無視 | 1.x と同じ |

- **JSON の構文**（1.x の `Windows.Data.Json` を実測して合わせる）:
  - 最上位が object でない、構文の誤り → **3**（1.x と同じ）
  - `null` の `jsonPayload` → **3**（1.x はプロセスが落ちていた。5.4）
  - 重複したキーは**後の値が勝つ**（1.x と同じ）
  - **数値は構文を読む段階で全部 `double` に変換する**（未知のキーの数値も）。±∞ になる（`1e400`）、または 0 でない値が 0 に丸められる（`1e-400`）なら 3（1.x は `TryParse` が例外 `0x83750008` を投げて 5 になっていた。5.4）。ただし 1.x は、指数を使わない小数表記で 0 に丸まる値（`0.` の後に 0 が 323 個以上続いてから `1`）を例外にせず 0 として通していた（実測）。1.x の判定は一貫しないので写さず、3 にする（5.4）
  - 入れ子の深さの上限は 1.x と同じにする: **最上位を含めてコンテナ（object / 配列）512 個までは読み、513 個で 3**（1.x は 513 個で例外 `0x8007050C` → 5。5.4。実測）。雛形の `MaxDepth = 64`（`MacClipboardJsonReader.cs:476`）は使わない
- 検証（ボタン 6 個以上、ループ音で `duration` が `"long"` でない、`args` と `invokeUri` の両方）は C# ではせず、ネイティブの show / schedule に任せる（規則は 1.x と同じで、結果も 7）。公開の `WindowsNotificationJsonBuilder.Validate` はそのまま残す
- 読み取りは `WindowsNotificationJsonReader`（汎用の JSON リーダー、`MacClipboardJsonReader` と同じ形）と `WindowsNotificationContentPlan`（JSON → ビルダーの呼び出しの並び）の 2 つの純粋な関数にする。**リーダーは「キーが無い」と「値が `null`」を区別して返す**（雛形の `GetMemberOrNull` は両者を同じ扱いにするので、そのまま写さない、`MacClipboardJsonReader.cs:123-130`）
- 公開の `WindowsNotificationPayload.Timestamp` と `Expiration` の XML コメントに、単位（Unix 秒、相対秒）を書く。値は変えない

### 4.3 そのほかの引数の変換

| メソッド | 2.0.0 へ |
|---|---|
| `Initialize` | 5.5 の流れ。`options.is_unpackaged = isPackaged ? 0 : 1`、`display_name` / `icon_uri` は UTF-8（`null` は `NULL`、ネイティブが `""` として扱い、非パッケージなら 7） |
| `ScheduleNotification` の `scheduledTimeUnixMs` | そのまま `schedule` の `unix_ms`（元からミリ秒）。範囲外はネイティブが 7（5.4） |
| `CancelScheduledNotification` / `RemoveNotificationsByTag` / `UpdateNotificationProgress` の `tag` / `group` | UTF-8。`null` は `NULL`（ネイティブで `""`、1.x と同じ） |
| `UpdateNotificationProgress` | `ProgressUpdate` 構造体に 6 つを入れる。`valueStr` / `status` の `null` は `NULL`（ネイティブが `""` として設定する。5.4） |
| `SetBadge` | 判定の順は **プラットフォーム（J-4）→ `value < -6` なら 7 → manager が無ければ 1 → `set_badge`**（1.x の順を保つ。-6 の判定を `#if` の外に置くと Editor で 7 が出て J-4 が崩れる）。-6 の判定は純粋な関数にする（層 1） |
| `RemoveNotificationById` | そのまま |

### 4.4 結果の変換

| 状況 | 結果 |
|---|---|
| ネイティブの戻り値 0 | `Success` |
| ネイティブの戻り値 1〜8 | `Failure(code)`。値はそのまま（1.2） |
| manager のハンドルが無い（未初期化・`OnDestroy` の後・J-6 で初期化に失敗した後） | ネイティブを呼ばず `Failure(1)`（1.x のネイティブも 1） |
| JSON の誤り（4.2） | 3 か 7 |
| `Initialize` で、DLL が無い・関数が無い・形式が違う・major が 2 でない（J-6） | `Failure(-4)`。文言は `Native library unavailable`。manager は作られないので、以後の操作は 1 |
| そのほかの C# 側の例外（`OutOfMemoryException` など） | `Failure(5)`。ログに例外の型を出す |
| `GetAllNotifications` の成功 | list から JSON を組み立てる（下の規則） |
| `GetNotificationSetting` | 成功なら値を enum に（`Enum.IsDefined` でないものは `Unknown`）。失敗・未初期化・Editor・`-4` は `Unknown`（1.x と同じ） |

**GetAll の JSON**（1.x は `JsonArray.Stringify()` の出力そのもの、`1.11.0:...cpp:251-262`。規則は PowerShell 5.1 で `Windows.Data.Json` を動かして測った）:

- 形は `[{"id":N,"tag":"…","group":"…"},…]`。キーは `id`、`tag`、`group` の順、空白なし。0 件は `[]`
- `id` は `uint` の 10 進の数字列（例: `4294967295`）
- 文字列のエスケープ:
  - `"` と `\` は `\"` と `\\`
  - `\b`、`\f`、`\n`、`\r`、`\t` は短い形
  - そのほかの U+0000〜U+001F は **大文字の 16 進**で `\u001F` の形（公開ビルダーの小文字の `x4`、`WindowsNotificationJsonBuilder.cs:227` は流用しない）
  - `/`、DEL、非 ASCII、U+2028 はエスケープしない
- この関数は純粋な関数にし、層 1 の期待値は上の規則から作る。非パッケージでは GetAll が 8 なので、成功の経路は Player では通らない（8.1）

`WindowsNotificationResult` の文言の対応に `-4` を足す（`Unknown error (-4)` にならないように）。公開の型もメソッドも増やさない。

### 4.5 活性化のコールバック（J-7）

1. 受け口（OS のスレッド）: `ntk_notification_activation_raw_arguments` でバイト列を写し、UTF-8 の `string` にする（コールバックから戻ると無効になるため、その場で写す）
2. `s_dispatcher`（`Awake` でメインスレッドから `UnityMainThreadDispatcher.Instance` を読んで入れておく static）に積む。**ネイティブのスレッドで `UnityMainThreadDispatcher.Instance` の getter に触らない**（無ければメインスレッド以外で GameObject を作るため）。`s_dispatcher` が `null`、または破棄済み（Unity の `==` で null）なら、ログを出して捨てる
3. **積むラムダの中は、1.x と同じく try/catch で囲み、購読者の例外を `LogError` で受ける**（`WindowsNotificationManager.cs:490-497`。漏らすと `UnityMainThreadDispatcher.Update` の外へ抜け、同じフレームのほかの処理が遅れる）。ラムダは `_instance?.NotificationInvoked?.Invoke(args)` を呼ぶ
4. 受け口の全体も try/catch で囲み、例外をネイティブに返さない。close / free を受け口から呼ばない、待たない（`N.h:98-102`）
5. `NotificationInvoked` の引数は `raw_arguments`。1.x と同じ作り方の JSON テキスト（ボタンの引数が 2 つ以上のときのキーの順は 5.4）

`user_data` / `release` を使わない理由: 受け口は static で、登録ごとの状態を持たない。delegate は static readonly で Player の生存中ずっと生きているので、close や free の後に配送中の活性化が 1 回走っても、解放済みのものに触らない。Editor ではネイティブを読まない（J-4）ので、ドメインリロードで delegate が消える場面が無い。

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | ガード | 内容 |
|---|---|---|---|
| 既存変更 | `Runtime/Notification/WindowsNotificationManager.cs` | 型は今のまま `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`。ネイティブを呼ぶ箇所は `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`、`#else` は何もせずに戻る | 1.x の `DllImport` と GetAll のバッファ処理を削除。`WindowsNotificationCApi` を呼ぶ。manager / runtime のハンドルを持つ。実行時の `Application.platform` の確認は `#if` の内側に残す（J-4）。`FireResult` の形は変えない。公開 API はそのまま。XML コメントを直す: `GetAllNotifications` の「retries with a larger buffer」（`:377`）、`Initialize` の「iconUri は任意」（`:222`。1.x でも 2.0.0 でも、非パッケージで無ければ 7） |
| 新規作成 | `Runtime/Notification/WindowsNotificationCApi.cs` | 型は `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`、`DllImport` と呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR` | `internal static`。4.1 の入れ子の構造体、delegate、`extern`、UTF-8 の入出力、list と activation の読み取り、J-8 の `GetCurrentPackageFullName`（kernel32） |
| 新規作成 | `Runtime/Notification/WindowsNotificationJsonReader.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | `internal`。汎用の JSON リーダー（`MacClipboardJsonReader` と同じ形、型名に `Windows` 接頭辞。「無い」と `null` を区別する） |
| 新規作成 | `Runtime/Notification/WindowsNotificationCApiMapping.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | `internal static`。4.2 の JSON → ビルダーの呼び出しの並び（`WindowsNotificationContentPlan`）、4.4 の GetAll の JSON、結果と例外の変換、`SetBadge` の事前判定、Show / Schedule の判定の順（J-11）、活性化のイベントを出す処理、J-6 の版判定。ネイティブを呼ばない |
| 既存変更 | `Runtime/Notification/WindowsNotificationResult.cs` | 今のまま | 文言の対応に `-4` を足す。`ErrorCode` の XML コメント（`:17`）に `-4` を足す |
| 既存変更 | `Runtime/Notification/WindowsNotificationPayloads.cs` | 今のまま | `Timestamp` / `Expiration` の単位と、`Audio.Src` が効かないこと（J-5）を XML コメントに書く |
| 既存変更 | `Plugins/Windows/VERSION.txt` | — | Dialog の J-6 の追加のピン（1.x）が Clipboard だけのためになる旨をコメントに書く（キーは変えない） |
| 新規作成 | `Tests/Runtime/WindowsNotificationCApiMappingTests.cs` | asmdef が Editor だけ（既存の `WindowsNotificationTests.cs` と同じ） | 層 1（7.1） |
| 既存変更 | `Tests/Runtime/WindowsNotificationTests.cs` | 今のまま | コード 8 と `-4` の文言のテストを足す |
| 新規作成 | `Tests/PlayMode/WindowsNotificationManagerEditorTests.cs` | `#if UNITY_EDITOR` | 層 2a（7.2） |
| 既存変更 | `Tests/PlayMode/WindowsNotificationSamplePlayerTests.cs` | 今のまま | W-04 の `[Ignore]` を外す。W-11 と 7.3 の追加。SetUp の `Initialize` の直後に設定の事前確認（8.2）。クラスコメントの UI テスト計画の参照（`:24`）を v2 にする |
| 新規作成 | `Tests/PlayMode/WindowsNotificationRecreatePlayerTests.cs` | `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`（既存の Player テストと同じ） | 7.3 の作り直しの流れ。専用のカテゴリ `RecreatesTheNotificationManager` |
| 既存変更 | `scripts/verify_unity_windows.sh` | — | 作り直しのテストを別の Player の実行として流す（`--include-destructive` のときだけ）。**`QuitsThePlayer` の形（バックグラウンドで流してタイムアウトで止め、XML を外から判定する、`:309-366`）は写さない**（普通に結果を返すテストなので、写すと失敗が合否に数えられない）。書き方: 本体の実行の除外を `:221` と `:226` の両方の分岐に足す。カテゴリ名は `QuitCategory` と同じく `sed` でソースから読み、見つからなければ CANNOT RUN。前面で流し、専用の XML と log に書き出す（`$PT_XML` は後の `:381` で使うので上書きしない）。その XML を `report_tests` にかけ、`total=0` なら「テストが流れていない」として失敗にする（`:260-264` と同じ）。流す位置は `:244-246` の後で、M-19 の Player.log の読み取り（`:328-330`）とぶつからないところ |
| 新規作成 | `artifact/features/notification/designs/2026-09-27-windows-notification-ui-test-plan-v2.md` | — | v1 は残す。W-04 / W-11 の保留を解き、4 章の「2.0.0 で変わる期待値」を本計画に合わせる（2 回目の init は成功のまま、timestamp は秒のまま）。7.3 の追加のテストと、全キーの fixture の中身を書く |
| 新規作成 | `artifact/features/notification/issues/audio-src-is-ignored.md` と `artifact/README.md` の課題一覧（:37） | — | J-5 |
| 既存変更 | `artifact/features/notification/issues/unreachable-notification-apis.md` と `artifact/README.md` の同じ課題の行 | — | 7.3 で 3 API が非パッケージで 8 を返すことを実機で確かめた結果と、進捗を書く |
| 新規作成 | `artifact/features/notification/results/<日付>-windows-notification-implementation-feature-result-v1.md` | — | 5.6 の手順 7。名前の形は既存の `2026-06-06-windows-notification-implementation-feature-result-v1.md` に合わせる（既存は上書きしない） |
| 既存変更 | `agent-rules/coding-rules/testing.md`、`artifact/topics/windows-c-abi-2/README.md` | — | 層 2a の追加、Notification の本数、README の「Notification・振る舞いが変わる」（2 回目の init と timestamp は公開 API では変わらない）と「ドメインリロード」（Notification は Editor でネイティブを読まないので対象外） |
| 非変更 | `Runtime/Notification/WindowsNotificationJsonBuilder.cs`、`Runtime/Common/UnityMainThreadDispatcher.cs`、`Runtime/UI/Windows/Notification/*`、`Runtime/Resources/UI/Windows/Notification/*`、`Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | — | 公開 API・共通部品・サンプルは変えない |

### 5.2 判断（開発者の承認済み、2026-09-27）

| # | 内容 | 決定 |
|---|---|---|
| J-1 | JSON を受ける公開 API | 残す。C# で 1.x の JSON の形を読み、ビルダーに渡す（4.2） |
| J-2 | `timestamp` の単位 | 公開の JSON では秒のまま。C# で 1000 倍する |
| J-3 | 2 回目の `Initialize` | manager があれば、ネイティブを呼ばずに `Success`。引数の変更は無視（1.x と同じ）。`OnDestroy` の後に新しい Manager で呼んだときは、初回として扱う |
| J-4 | Editor で呼ぶ | 1.x のまま何もしない（per-call callback もイベントも出さない、`GetNotificationSetting` は `Unknown`）。DLL を Editor で読まないので、ドメインリロードの後片付けは要らない |
| J-5 | `Audio.Src` | 移行では読まない（1.x と同じ）。別の issue に記録する |

### 5.3 判断（本計画で決めたもの）

| # | 内容 | 決定 |
|---|---|---|
| J-6 | DLL と C# の版 | `Initialize` の最初のネイティブ呼び出しの前に `(ntk_version() >> 16) == 2` を確かめる。違えば `-4` で、manager は作らない（以後の操作は 1。次の `Initialize` でもう一度確かめる）。`DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` も `-4`（Dialog の J-7 と同じ考え方。コードは共有しない） |
| J-7 | コールバックの登録 | static の delegate、`user_data` / `release` は `NULL`、dispatcher は `Awake` で static に取っておく（4.5） |
| J-8 | runtime を呼ぶか | **引数の `isPackaged` ではなく、実際にパッケージのプロセスかどうかで決める。** `GetCurrentPackageFullName`（kernel32）を長さ 0・バッファ `NULL` で呼び、戻り値で 3 つに分ける: `APPMODEL_ERROR_NO_PACKAGE`（15700）なら非パッケージで `runtime_initialize` を呼ぶ、`ERROR_INSUFFICIENT_BUFFER`（122）ならパッケージで呼ばない、そのほかなら `Initialize` の結果を 5 にしてログを出す（Microsoft Learn の GetCurrentPackageFullName）。この分け方は純粋な関数にする（層 1）。1.x は `isPackaged` にかかわらず必ず `initWinAppSdk` を呼んでいた（`WindowsNotificationManager.cs:229-236`）ので、非パッケージのプロセス（テストしている唯一の形）では 1.x と同じになる。パッケージのプロセスでは呼ばない（`NativeToolkit/Notification.h:246-249` は、パッケージアプリから呼ぶとブートストラッパーの失敗になると書く）。パッケージの経路は実機で確かめられないので未検証（8.1）。`GetCurrentPackageFullName` の `DllImport` は `WindowsNotificationCApi` に置く |
| J-9 | runtime と manager の寿命 | runtime のハンドルは `OnDestroy` まで持ち続ける。`Initialize` は runtime を持っていれば `runtime_initialize` を飛ばす。**`manager_create` が失敗しても runtime は解放しない**（やり直しの `Initialize` で bootstrap を下ろして初期化し直さない。1.x は bootstrap を残していた）。`OnDestroy`（`_instance == this` のとき）で `manager_free` → `runtime_free` の順に解放し、ハンドルを `IntPtr.Zero` に戻す。`Application.quitting` でも `OnDestroy` は来るので、別には拾わない |
| J-10 | IL2CPP | Dialog の J-10 と同じ。3 機能を移したあとに 1 回流す |
| J-11 | Show / Schedule の判定の順 | **1.x の順を保つ**: manager が無ければ 1 → `get_setting` が失敗するか値が 0 でなければ 2 → JSON を読む（3 / 7）→ ビルダー → `show` / `schedule`（ネイティブがもう一度設定を見る）。`get_setting` を 1 回余分に呼ぶ |

### 5.4 既知の差分（意図したもの）

既存の公開 API は変えないが、次の振る舞いは変わる。XML コメントに書き、実装結果ファイル経由で次の版のマニュアルへ引き継ぐ。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| ボタンのクリック（非パッケージ、アプリが動いている） | 届かない（`CustomActivator` を書かない） | `NotificationInvoked` が来る |
| レジストリ | `CLSID\{clsid}\LocalServer32`、`AppUserModelId\<displayName>` の `DisplayName` / `IconUri`、ショートカット | 加えて `CustomActivator` を書く |
| JSON の必須キーが無い・型が違う・値が `null` | 5（WinRT の例外） | 7 |
| JSON の数値が `double` に収まらない（`1e400`、`1e-400`） | 5（`TryParse` の例外） | 3 |
| JSON の入れ子がコンテナ 513 個以上（最上位を含む） | 5（`TryParse` の例外） | 3 |
| JSON の小数表記で 0 に丸まる値（`0.` の後に 0 が 323 個以上続いてから `1`） | 0 として成功 | 3 |
| `null` の payload（初期化済みで有効なとき） | **プロセスが落ちた**（`0xC0000005`） | 3 |
| Show / Schedule で、設定の取得が例外になる（主にパッケージ、未検証） | 5（`Setting()` の例外を catch） | 2（ネイティブが -1 → `HRESULT_FAILURE` にし、J-11 で 2） |
| 文字列の中の NUL（`"\u0000"`） | UTF-16 のまま、NUL の後ろも渡した | NUL 終端の UTF-8 にするので、NUL の手前で切れる |
| `timestamp` が範囲外（±922,337,203,685 秒を超える） | `time_t` への変換のまま | 7 |
| `expiration` が `long` に収まらない | 値が決まらなかった | 7 |
| `scheduledTimeUnixMs` が範囲外（±922,337,203,685,477 を超える） | 検査せずに渡した（`1.11.0:...cpp:900-910`） | 7 |
| 引数が 2 つ以上のボタンの活性化の JSON テキスト | `args` を `JsonObject` の列挙順（文書の順ではない）で渡し、その順で並んだ | 文書の順で渡すので、キーの順が変わりうる。キーと値の組は同じ |
| `GetAllNotifications` の結果が長い | 黙って切り詰めて成功 | 切り詰めない（1M wchar を超えると 5） |
| `UpdateNotificationProgress` の `valueStr` / `status` が `null` | その項目を変えない | `""` にする（ネイティブの仕様。C# では保てない） |
| DLL が無い・関数が無い・形式が違う | **どのメソッドでも**例外が呼び出し側に出た | 例外は出ない。`Initialize` は `-4` の結果、ほかのメソッドは manager が無いので 1（`GetNotificationSetting` は `Unknown`、`SetBadge(-7)` は判定の順で 7） |
| そのほかの C# 側の例外 | 呼び出し側に出た | `5` の結果 |
| `OnDestroy` の後 | bootstrap を shutdown しない | `MddBootstrapShutdown` を 1 回呼ぶ。そのあと新しい Manager で `Initialize` すると、同じプロセスで bootstrap を初期化し直す |
| パッケージのプロセスの `Initialize`（J-8） | `initWinAppSdk` を呼んだ | 呼ばない（未検証）。非パッケージのプロセスでは、`isPackaged` にかかわらず 1.x と同じく呼ぶ |
| 対になっていないサロゲートを含む文字列 | UTF-16 のまま渡した | `Encoding.UTF8` で U+FFFD に置き換わる。活性化の引数と GetAll の tag / group も、ネイティブが U+FFFD にする（`Utf8.cpp:34`） |
| close の後に届く活性化 | 1.x も 1 回走りえた | 同じ。`_instance` が無ければ捨てる |

### 5.5 呼び出しの流れと契約

**`Initialize`**

1. `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` の外、または `Application.platform != WindowsPlayer` なら何もせずに戻る（J-4）
2. manager があれば `Success` を出して終わる（J-3）
3. 出力のハンドルを `IntPtr.Zero` で初期化する（最初の P/Invoke より前）
4. J-6 の版の確認。`-4` ならそれを結果にして 8 へ
5. 非パッケージのプロセス（J-8）で runtime を持っていなければ `runtime_initialize(0x00010007, out runtime)`。失敗ならその errorCode を結果にして 8 へ（`HRESULT_FAILURE` なら system code の HRESULT をログに出す。bootstrap の失敗は 1.x も `LogError` だったので、ここだけは `LogError`、`WindowsNotificationManager.cs:233`）
6. `ManagerOptions` を組み立て（`on_invoked` は 4.5 の受け口、`user_data` / `release` は 0）、`manager_create`
7. 失敗しても runtime は持ち続ける（J-9）
8. **catch の外で**、`FireResult` で結果を出す

**`ShowNotification`（`ScheduleNotification` も同じ）**

1. プラットフォームの確認（J-4）
2. manager が無ければ `Failure(1)`
3. `get_setting` が失敗するか値が 0 でなければ `Failure(2)`（J-11）
4. JSON を読み、ビルダーの呼び出しの並びを作る（4.2）。誤りなら 3 か 7
5. `content_create` → 並びを順に呼ぶ（どれかが失敗したら、その errorCode）→ `show`（`schedule`）
6. `finally` で `content_free` と、確保した UTF-8 をすべて解放する
7. catch の外で、`FireResult` で結果を出す

try の形は Dialog v5 の 5.5 と同じ（finally を内側の try に置き、外側で catch する。解放の失敗も結果になる）。

- 捕まえるのはネイティブ呼び出しと変換だけ
- **ログの重さ**: 結果として返す失敗（1〜8、`-4`、JSON の誤り、`get_setting` の失敗、例外の変換）は `Debug.Log` か `LogWarning` で書き、`LogError` にしない（Unity Test Framework は想定していない `LogError` でテストを落とすので、7 / 3 / 8 / 1 を期待するテストが移行後だけ落ちる）。`LogError` は 1.x と同じ 4 種類（呼び出しは 5 箇所）だけ: bootstrap の失敗（`WindowsNotificationManager.cs:233`）、`FireResult` の例外（`:477`）、GetAll の per-call とイベントの例外（`:400`）、活性化の受け口の例外（`:496,502`）
- 判定の順（manager の有無・`get_setting` の結果・設定の値 → 「JSON へ進む / 1 / 2」）と、活性化のイベントを出す処理（購読者の例外を `LogError` で受ける）は、`internal static` の純粋な関数にする（層 1、7.1）
- **結果の出し方は 1.x の `FireResult`（`WindowsNotificationManager.cs:465-479`）の形をそのまま使う**: per-call callback と `NotificationOperationCompleted` を 1 つの try の中で順に呼び、例外は `LogError` で受ける。per-call が投げるとイベントは出ない（1.x と同じ）
- **出すイベント**: `GetAllNotifications` は `NotificationOperationCompleted` を出さず、per-call → `GetAllNotificationsCompleted`（1.x の `:393-401` と同じ形）。`GetNotificationSetting` はイベントを出さない。ほかのメソッドは per-call → `NotificationOperationCompleted`
- スレッド: 同期。呼び出したスレッドで結果まで出す。活性化だけは dispatcher 経由
- 同じ manager に対して、`OnDestroy` の close / free と、ほかの操作を同時に行わない（`WindowsLibraryCApi` の設計 `:153`）。Manager の操作はメインスレッドから呼ぶ前提で、1.x と同じく強制はしない

### 5.6 実装順

各段階の終わりに層 0（`scripts/verify_unity_windows.sh` の Player ビルド）を流す。

1. `WindowsNotificationJsonReader`、`WindowsNotificationCApiMapping` と層 1 のテスト。**1.x のまま**、7.3 の追加のうち「1.x で記録」と書いたものを層 2b に足し、1.x の結果を期待値として記録する
2. `WindowsNotificationCApi`
3. `WindowsNotificationManager` の置き換えと層 2a のテスト
4. 層 2b: W-01〜W-10 と、手順 1 で記録したテストが期待値を変えずに通ること。W-04 の `[Ignore]` を外し、W-11 と「移行後だけ」のテストを書く（`--include-destructive`）
5. Dialog の D-09〜D-12 と、Clipboard の Player テストを流し直す（`manager_create` がメインスレッドで MTA を試みるため。1.x と同じはずだが確かめる、親課題 README「STA / MTA の分離」）
6. 3 機能を移したあとで IL2CPP（J-10）
7. 実装結果ファイル（`artifact/features/notification/results/*-implementation-result*.md`）に、5.4 の表、`-4` の意味を書き写す（write-manual が読む）

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | 初期化の前に操作する | C# | `Failure(1)`（`GetNotificationSetting` は `Unknown`） | 2b（7.3 の作り直しの流れ。`Instance` を破棄して作り直した、未初期化の Manager で確かめる） |
| E-2 | 非パッケージで表示名かアイコンが空 | ネイティブ | 7 | 2b（同じ流れ） |
| E-3 | bootstrap の失敗 | ネイティブ | 5、ログに HRESULT | 1（変換）。起こせない |
| E-4 | 2 回目の `Initialize`（J-3） | C# | `Success`、ネイティブを呼ばない | 2b（W-01。SetUp で初期化済み） |
| E-5 | 通知が無効 | ネイティブ | 2（Show / Schedule では JSON より先、J-11） | 起こせない（設定を変えるテストはしない） |
| E-6 | JSON の構文の誤り、`null` の payload | C# | 3 | 1、2b |
| E-7 | JSON の必須キーが無い・型が違う・値が `null` | C# | 7 | 1、2b |
| E-8 | 検証の誤り（ボタン 6 個など） | ネイティブ | 7 | 2b |
| E-9 | 進捗の通知が無い | ネイティブ | 4 | 2b（W-08） |
| E-10 | 非パッケージで SetBadge / RemoveById / GetAll | ネイティブ | 8 | 2b（7.3） |
| E-11 | `SetBadge` の値が -6 より小さい | C# | 7（初期化の確認より先） | 1（事前判定の関数）、2b |
| E-12 | DLL が無い・major が 2 でない（J-6） | C# | `-4` | 1（版判定、例外の変換）。実物は起こせない |
| E-13 | WinRT の失敗 | ネイティブ | 5 | 起こせない |
| E-14 | C# 側の予期しない例外 | C# | 5、結果は 1 回 | 1（例外の変換） |
| E-15 | Editor で呼ぶ（J-4） | C# | 何もしない | 2a |
| E-16 | close の後に届く活性化 | ネイティブ | 捨てるか、Manager があれば `NotificationInvoked` | 起こせない。4.5 の構造をコードレビューで確かめる |
| E-17 | 予約時刻が範囲外 | ネイティブ | 7 | 2b（移行後だけ） |
| E-18 | per-call callback か購読者が例外を投げる | C# | `LogError`。per-call が投げるとイベントは出ない（1.x と同じ） | 2b |

## 7. テスト方針

### 7.1 層 1（EditMode）

- 構造体 2 つの大きさと全フィールドの位置（`Marshal.OffsetOf`、1.3 の表）
- JSON リーダー: 構文の誤り、最上位が配列、`null` の入力、エスケープ、数値の形（`1e400` と `1e-400` は未知のキーでも 3、`0e5` や `-0` は通る）、深さ（最上位を含めてコンテナ 512 個は通り 513 個で 3。object だけを 512 段入れ子にしても通る）、小数表記で 0 に丸まる値（3）、UTF-16 のサロゲートペア、重複したキー（後が勝つ）、「無い」と `null` の区別
- JSON → ビルダーの呼び出しの並び: 4.2 の表の**全キーとサブキー**、`scenario` / `audio.type` / `crop` の未知の値、`textBoxes` の `NULL` と `""` の使い分け、`args` と `invokeUri` の両方、必須キーの欠落（7）、型の違い（7）、`null` の値と配列の要素の `null`、object でない要素（`"buttons":[1]`）（7）、**1.x が読まないキーは型が違っても `null` でも通る**（`{"audio":{"type":"mute","event":5}}`、`{"audio":{"type":"mute","event":null}}`、`{"audio":{"type":"uri","uri":"…","event":5}}`）、**`audio.loop` はどの `type` でも読む**（mute＋`loop:true` で `duration` なしは 7、mute＋`loop:"x"` は 7、mute＋`loop:true`＋`duration:"long"` は成功）、`src` を読まないこと、`progress.valueStr` のキー名、`timestamp` の秒 → ミリ秒（0 方向の切り捨て、負の値、範囲外で 7）、`expiration` の範囲外で 7
- Show / Schedule の判定の順の関数（J-11）: manager の有無 × `get_setting` の結果（成功・失敗）× 設定の値（0・0 以外）で、「1 / 2 / JSON へ進む」になること
- 活性化のイベントを出す関数: 購読者が投げても `LogError` になって外へ漏れないこと（`LogAssert.Expect`）、`_instance` が無ければ何もしないこと
- `WindowsNotificationJsonBuilder` が出す JSON を読むと、元のペイロードと同じ呼び出しの並びになること（公開の組み立てと読み取りの往復）。ビルダーが出さないキーは、手で書いた JSON で試す
- GetAll の JSON の組み立て（0 件、4.4 のエスケープの規則の各文字、`id` の最大値）
- `SetBadge` の事前判定、版判定（J-6）と例外の変換、J-8 のパッケージの判定（15700 / 122 / そのほか）
- `WindowsNotificationResult` のコード 8 と `-4` の文言

### 7.2 層 2a（PlayMode・Editor、`#if UNITY_EDITOR`）

- J-4: 各メソッドを呼んでも、per-call callback も `NotificationOperationCompleted` も `GetAllNotificationsCompleted` も数フレームの間に出ないこと。`SetBadge(-7)` も何も出さないこと（-6 の判定がプラットフォームの確認より後にあること）。`GetNotificationSetting` が `Unknown` であること

### 7.3 層 2b・層 3（PlayMode・Player、`WindowsNotificationSamplePlayerTests`、`Destructive`）

- **W-01〜W-10 を期待値を変えずに通す。**
- **W-04 の `[Ignore]` を外し、通す。** 前提の確認（`CustomActivator` とその CLSID の `LocalServer32` がこの exe、`:138-145`）は、移行後の `Initialize` が書くので満たされる
- **W-11 を書く**（UI テスト計画 v1 の 1 章 `:34`）
- 追加（Manager を直接呼ぶ）。「1.x で記録」は 5.6 の手順 1 で 1.x のまま流して期待値を記録する:
  - **作り直しの流れ**（1.x で記録）。**専用のカテゴリ（`RecreatesTheNotificationManager`）の別の fixture にし、スクリプトで別の Player の実行に分ける**（`QuitsThePlayer` と同じ形。失敗すると Manager が未初期化のまま残り、以後の SetUp の `Initialize` も同じ理由で失敗し続けるので、プロセスごと分けてほかのテストを道連れにしない。`[Order]` はクラスに付けられず、付けたメソッドは先に流れるので使わない。`com.unity.ext.nunit` の `OrderAttribute`、UTF の CompositeWorkItem（`Library/PackageCache/com.unity.test-framework@*/UnityEngine.TestRunner/NUnitExtensions/Runner/CompositeWorkItem.cs` の 237-273 行））:
    1. `DestroyImmediate(Instance.gameObject)`
    2. 新しい `Instance` で Show → 1、`GetNotificationSetting` → `Unknown`
    3. `Initialize(false, "", "")` → 7
    4. サンプルと同じ引数で `Initialize` → 成功
    5. Show → 成功し、通知センターに出る
    - E-1、E-2、J-3 の「新しい Manager は初回」、J-9 を確かめる。移行後は、bootstrap の shutdown のあとの再初期化（native 側でも実物では試していない、`NotificationRuntime.cpp:85` はフック）もここで通る
    - 1.x で通ることは確かめてある（`Uninit` の `CoRevokeClassObject`、再 Init の 7、Show の 1、設定の -1）。シーンはテストごとに読み直すので、サンプルの購読も壊れない（`WindowsClipboardSampleScreenDriver.cs:216-227`）
  - **全キーの JSON**（1.x で記録）: `ShowNotification` が成功し、通知センターに title が出ること。1 つの JSON に全部は入らない（`audio.type` は 1 値）ので、UI テスト計画 v2 に書く fixture で行う
    - 画像は `StreamingAssets/app-icon.png` の `file:///`
    - `expiration` は 60 秒以上
    - `scenario` は `reminder` だけにする（`alarm` / `incomingCall` / `urgent` は応答不可を越えてバナーが残り、通知センターを読む PowerShell が頼る前面の窓を奪いうるため）
  - JSON の構文の誤りで 3、ボタン 6 個で 7（1.x で記録）
  - 非パッケージで `SetBadge(1)` / `RemoveNotificationById(1)` / `GetAllNotifications` が 8（issue `unreachable-notification-apis.md` の未検証を確かめる）。GetAll は `GetAllNotificationsCompleted` が出て、`NotificationOperationCompleted` が出ないこと（1.x で記録）
  - `SetBadge(-7)` が 7（1.x で記録）
  - per-call callback が例外を投げると、`LogError` が出て `NotificationOperationCompleted` が出ないこと。`GetAllNotifications` でも同じく、per-call が投げると `LogError` が出て `GetAllNotificationsCompleted` が出ないこと（`LogAssert.Expect`、1.x で記録。1.x の `WindowsNotificationManager.cs:393-401`）
  - 移行後だけ（1.x とは期待値が違う、5.4）: JSON の必須キーの欠落・`null` の値で 7、予約時刻が範囲外で 7、**`null` の payload で 3**（1.x ではプロセスが落ちるので、1.x では流さない）
  - **`extern` の結び付け**: NULL を渡しても安全な関数を `IntPtr.Zero` で直接呼び、例外なく戻ること。対象は次のとおり
    - `ntk_last_system_code`
    - `list_count` / `_id_at` / `_tag_at` / `_group_at` / `_free`
    - `runtime_free`、`manager_close` / `_free`、`content_free`
    - `open_settings`（NULL は `INVALID_PARAMETER`）
    - `get_all`（NULL の manager は `INVALID_PARAMETER`）
    - `activation_raw_arguments`
    - ほかの関数は、W-01〜W-10 と上の追加で毎回通る
- Mono で走る。IL2CPP は J-10

### 7.4 手動

なし。確かめないもの（手動でも確かめない）は 8.1。

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。手動に残す項目は無い。
起こせない失敗（E-3、E-5、E-12〜E-14、E-16）は、変換を層 1 で押さえ、構造をコードレビューで確かめる。

**確かめないもの**（理由つき）:

| 項目 | 理由 | 押さえ方 |
|---|---|---|
| GetAll の成功時の JSON と、list の実際の読み取り | 非パッケージでは GetAll が 8 | 組み立ては層 1（4.4 の実測の規則）、list の読み取りはコードレビュー。`extern` の結び付けは 7.3 |
| パッケージのプロセス（J-8）と、設定の取得が例外になる場合 | テストの Player は非パッケージ。MSIX の検証手順は無い | 未検証として XML コメントと実装結果に書く。外部の場所を持つパッケージ（sparse）のプロセスも `GetCurrentPackageFullName` が 122 を返すので、J-8 では bootstrap を呼ばない扱いになる。Microsoft Learn にはこの形も bootstrapper を使うと読める記述があり、実機では確かめていない |
| 非パッケージのプロセスで `Initialize(isPackaged: true)` | 1.x での結果が分からず、試すと Manager がパッケージの形で初期化されたまま残り、以後のテストを壊しうる | 移行後は J-8 で runtime を呼ぶので、bootstrap の有無は 1.x と同じ。`manager_create` の結果はネイティブ次第で、未検証と書く |
| コールドスタートの活性化 | native 側でも試していない。テストから起動の仕方を作れない | 未検証として書く |

### 8.2 OS が出す画面・許可・前提の OS 設定

- 通知センターを外から開いて読む PowerShell（`WindowsNotificationSamplePlayerTests` の `CenterScript`、既存）。通知センターは `EnumWindows` に出ないので、前面の窓から取る（UI テスト計画 v1）
- **前提の OS 設定: この PC の通知の設定が有効であること。** **SetUp の `Initialize` が成功した直後に** `GetNotificationSetting` を見て（初期化の前は `Unknown` になるので、前には置かない）、`Enabled` でなければ「設定 > システム > 通知 で通知と Unity NativeToolkit を有効にする」と書いて、すぐ失敗させる（無効のまま進めると、Show が 2 で落ちて原因が分かりにくい）。応答不可（DND）が ON でも、通知センターには入る（W-03 で確認済み）
- 非パッケージの登録（レジストリ、ショートカット）は `Initialize` が HKCU に書く。**表示名（`Application.productName`、"Unity NativeToolkit"）が同じ別のアプリと、クリックを奪い合う**ので、テスト中に同じ名前のアプリを動かさない
- テスト用 Player のファイアウォールの規則（既存）

### 8.3 呼び出し側を止める OS の画面

なし。どの操作もモーダルの画面を出さない（`OpenNotificationSettings` は設定アプリを別に開くだけ。テストでは押さない）。
