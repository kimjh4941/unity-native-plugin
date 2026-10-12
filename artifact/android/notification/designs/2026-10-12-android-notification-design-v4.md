# Android Notification 実装計画 v4（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Notification（Android） |
| 種別 | **移行。機能追加ではない。** 1.x の Unity 向けブリッジ（`android.unity.notification.UnityAndroidNotificationManager`、`AndroidJavaObject` / `AndroidJavaProxy`、JSON）から、C ABI 2.0.0（`libntk.so`、`Notification.h`）の P/Invoke に置き換える |
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（決定 J-1〜J-11、Windows と違うところ A-1〜A-6。移す順は Dialog → Share → Clipboard → **Notification**。最後の機能） |
| 前例 | Android Clipboard（`artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md`、実装結果 v3）: 同期の C を呼んで結果を後で出す形、`_instance` への配送、リスナーを 1 回だけ登録する形、`AndroidClipboardJsonBuilder` を残す形。Android Share（`artifact/android/share/designs/2026-10-11-android-share-design-v7.md`）: UI の補助で OS の画面を押す形。Windows Notification（`artifact/windows/notification/designs/2026-09-27-windows-notification-design-v8.md`）: 公開の JSON API を残し、C# の JSON リーダーで 1.x のネイティブの読み方に合わせる形 |
| 保つ範囲 | **親課題の J-11「ふつうの使い方を保つ」**。保つのは、公開 API と、UnityMain から 1 回ずつ正しい入力で呼んだときの結果・時点・文言、Editor の結果。同時の呼び出し、Manager を壊す前後、変わった入力は、いちばん単純な形にして 5.3 に書く。「1.x と同じ」と書く行には、タグ `1.12.0` のコードの位置（または今の C# の位置）を付ける。**例外（利用者の判断、2026-10-12）**: 通知の権限が無いときの表示は、1.x の「成功」を保たず失敗にする（J-N7） |
| 方針 | **公開 API（`Instance`、`Initialize()`、メソッド 21 本、イベント 3 本、`Action*` 定数 2 個、`Operation*` 定数 16 個、Payload 10 型、結果 3 型、`AndroidNotificationJsonBuilder`、引数、既定値、JSON の形、エラーの文言）を保つ。** 変わるのは 5.3「既知の差分」に列挙したものだけ。型の名前は変えない（接頭辞の無い結果 3 型の改名は、4 機能の移行の後にまとめて行う） |
| AAR | `android-native-toolkit-2.0.0.aar` と `android-native-toolkit-capi-2.0.0.aar`（dist 1.13.0）。**この移行が最後の機能なので、移行の後に旧ブリッジ（`extra_*`、dist 1.12.0）を外せる**（親課題の段 4。外すのはこの設計の範囲の外） |
| 出典 | native-toolkit の `dist/1.13.0/android/include/NativeToolkitC/Notification.h`（以下 `H`）、`android/android_library_capi/src/main/cpp/src/Notification/`（`Notification.cpp` は `CPP`、`Builders.cpp` は `BLD`）、`.../capi/jni/NotificationBridge.kt`（以下 `NB`）、`android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/notification/`（以下 `K`）。旧ブリッジはタグ `1.12.0` の `android/unity_android_plugin/src/main/java/android/unity/notification/`（`UnityAndroidNotificationManager.kt` は `OB`、`UnityNotificationJsonParser.kt` は `OP`、`NotificationActionReceiver.kt` は `OR`）、1.x のライブラリ `android/android_library/src/main/java/android/library/notification/`（以下 `OL`）。今の C# は `Runtime/Android/Notification/AndroidNotificationManager.cs`（以下 `Mgr`）、`AndroidNotificationPayloads.cs`（`Pay`）、`AndroidNotificationJsonBuilder.cs`（`Json`）、`NotificationActionResult.cs`（`ActRes`）。C ABI の約束は第 1 部（P1）・第 2 部（P2。8.3 の NT-01〜NT-27）、申し送りは `artifact/topics/android-c-abi/results/2026-10-10-android-c-abi-stage2c-handoff.md`（`Handoff`）、マニュアルは native-toolkit の `manual/1.13.0/notification.ja.md`（`MAN`） |
| 調べた事実 | 設計の前に、事実と出典だけの一覧を作った（作業メモ。リポジトリには置かない）。1 章・2 章はその要約 |
| UI の補助 | **広げる**（利用者の判断、2026-10-12）: 通知のシェードを開き、通知を探して本文・ボタンを押し、スワイプで消す命令と、権限のダイアログを押す命令。補助の設計書の新しい版（v12）にする（J-N21） |
| レビュー | `artifact/android/notification/reviews/2026-10-12-android-notification-design-review-v1.md`、`...-review-v2.md`、`...-review-v3.md`（どれもサブエージェントと Codex。v3 で両方の A1 が 0 になり、止めた） |
| UI テスト計画 | この設計のレビューの後に作る（7.3 に番号を振り、1.x の列を実測で埋める） |
| 1.x の記録 | **この repo に Android Notification の設計・結果・実機の記録は無い**（`artifact/android/notification/` はこの設計が初めて）。テストは `AndroidNotificationJsonBuilderTests` の 3 件だけ。移行の前に UI テスト計画の表を 1.x のまま流して記録する（5.5 の手順 3） |

## v3 からの主な変更（v4。レビュー v3 を受けて。2026-10-12）

レビュー v3 で両方の A1 が 0 になったので、レビューは止めた。この版は残った B・C を直した記録で、振る舞いを決める行は変えていない（レビュー v3 の 3 章）。

- **実機の前提**（R3-1、X3-1）: 新しいインストールとは限らない。検証スクリプトがテストの Player を実行の前と中断のときにもアンインストールするように直す。K-3 を「別の起動を要るテストすべて」に広げた
- **テスト**（R3-2、R3-6）: `ScheduleWithFallback` の 4 つの場合を 7.2 に。設定の画面の結果は、待つことだけを期待にする
- 記述（R3-3〜R3-5、R3-7〜R3-9、X1-6）: 呼び直しの記述、補助の命令の一覧、7.3 の K-3 の参照、-1000 の推測と測る行、メッセージの負の時刻、color の範囲、正確なアラームの版の条件、`isGroupConversation` の型の行、設定の画面の `SecurityException` の行、想定外の値の名前

## v2 からの主な変更（v3。レビュー v2 を受けて。2026-10-12）

- **正確なアラームの呼び直しの継ぎ目の形を直した**（R2-1。A1）: `ScheduleWithFallback(int inexact, Func<int,int>)`。不正確な予約を正確に変えない
- **終了した状態のタップを、実機の自動テストで回せるかを残件にした**（R2-2。K-3）
- **前提の抜けを足した**（R2-10）: 毎回の実行は新しいインストールから始まる。権限の付与と正確なアラームの許可はテストの初めに補助に頼む。full screen と予約の発火の期待
- 直し漏れ: E-2 の重複（後勝ち）、E-5 の入力、チャンネルの解放の場所、5.3 の重なった行、設定の画面の結果の時点（`startActivity` の直後。X2-1）、`STORAGE_FAILED`、`customViewLayout` の空白と null、メッセージの負の時刻、color の範囲とテスト、起動の Intent の書き方、1.x の記録の条件、「要確認」の振り分け、K-4 の移行ガイド

## v1 からの主な変更（v2。レビュー v1 を受けて。2026-10-12）

- **利用者の判断を 2 つ足した**: 引けないリソースの名前は、申し送りと違うが失敗にする（J-N6。R1-2）。アプリの通知がオフのときも、権限が無いときと同じく失敗にする（J-N7。R1-3。文言を両方を含む形に変えた）
- **1.x の読み方・補正の誤りを直した**（A1）: `id` の `int.MinValue` は欠落（X1-1）、`isGroupConversation` の null は false（X1-2）、groupName の無い groupId は groupId を名前にする（R1-1）、予約の tag の空・空白は寄せない（R1-4）、importance の説明と -1000 の差分（X1-3）
- **丸めを保つ補正に移した**（R1-5）: priority と progress
- **5.3 に足した**: Manager を壊す前後の届け先（X1-4）、保持は最初の登録の前だけ（X1-9）、アクションの launchApp、full screen の Intent、設定の画面と結果の時点、`SecurityException`、不正確に下げた予約の再起動の後、JSON の緩い構文・型・data の値（R1-16、R1-17）
- **テストと継ぎ目**: J-N8 の呼び直しを純粋な関数にして数える（R1-10）、リスナーの印を種類ごとに 2 つ（R1-11、X1-8）、イベントのハンドルの解放（X1-7、R1-12）、未知の kind（R1-14）、往復のテストの範囲（X1-5）、終了した状態のタップと権限の取り消しは別の起動（R1-8、R1-9）
- **記述**: 6・7・9 の文言（X1-6）、`STORAGE_FAILED` と `SERVICE_START_NOT_ALLOWED` の文言、構造体の名前（R1-6）、`NotificationActionResult` の古いコンストラクタを消す（R1-7）、ログを長さだけに（R1-19）、XML コメントの計画、setter の数、Android 12

## 1. native-toolkit 側（2.0.0 の C ABI）

### 1.1 関数（86 個）

| 群 | 関数 | 形 |
|---|---|---|
| 表示 | `ntk_notification_show(content)`、`_update(content)`、`_remove(id, tag)`、`_remove_all()` | 同期（呼び出しスレッド）。show / update は権限か通知の有効が偽なら `PERMISSION_DENIED`（`NB:132-156`） |
| チャンネル | `_create_channel(channel)`、`_delete_channel(id)` | 同期 |
| 予約 | `_schedule(content, options)`、`_cancel_scheduled(id, tag)`、`_cancel_all_scheduled()` | 同期。**呼び出しスレッドでファイルを書く**（`H:130-132`）。未来の正確な予約で正確なアラームが不許可なら `EXACT_ALARM_NOT_ALLOWED`、過ぎた時刻で権限が無ければ `PERMISSION_DENIED`（`NB:186-205`）。`cancel_all_scheduled` と `is_scheduled` は保存した予約だけ（`H:136,144`） |
| Progress | `_start_progress(content)`、`_update_progress`、`_complete_progress`、`_stop_progress()` | 同期。補正（ongoing など）は C ABI が行う（`NB:219-275`）。権限は確かめない。後ろからは `SERVICE_START_NOT_ALLOWED` になりうる |
| 問い合わせ | `_has_permission(out)`、`_are_enabled(out)`、`_can_schedule_exact_alarms(out)`、`_is_scheduled(id, tag, out)` | 同期。失敗はエラー値（out は 0）（`CPP:302-320,505-543`） |
| 設定の画面 | `_open_settings_async(target, cb, user_data, release)` | **非同期**。Android の main で前面を判定し、前面でなければ `NOT_FOREGROUND`。`OPENED` / `OPENED_FALLBACK`、開けなければ `SETTINGS_NOT_OPENED`（`NB:318-346`） |
| 権限の要求 | `_request_permission(cb, user_data, release, out_id)`、`_cancel_permission_request(id)` | 非同期（この設計では使わない。J-N10） |
| イベント | `_add_interaction_listener(cb, user_data, release, out)`、`_add_shown_listener(...)`、`_listener_remove(listener)` | 登録。挿入は Android の main（`CPP:207-237,623-647`） |
| ビルダー（内容 40） | `_content_create(id, title, message, out)`、`_content_free`、`_content_set_*`（26）、`_set_style_*`（5）、`_add_message`、`_add_view_click`、`_add_data`、`_add_action`、`_set_tap`、`_set_dismiss_event`、`_set_full_screen` | 呼び出しスレッドで、初期化の前でも使える（`H:74`）。範囲の検査は setter の入口（`BLD:97-417`） |
| ビルダー（チャンネル 11） | `_channel_create(id, name, importance, out)`、`_channel_free`、`_channel_set_*`（9） | 同上。importance は 0〜4（`BLD:421-544`） |
| 読み取り（12） | `_interaction_kind / _notification_id / _tag / _action_id / _data_count / _data_key_at / _data_value_at / _free`、`_shown_notification_id / _tag / _channel_id / _free` | イベントは受け手の所有。受け口の中で読んで `_free`（`H:80-81,279-297`） |

- エラー値: 0 `NONE`、1 `INVALID_PARAMETER`、2 `NOT_INITIALIZED`、3 `NOT_SUPPORTED`、4 `UNKNOWN`、5 `OUT_OF_MEMORY`、6 `CANCELED`、7 `CANCELED_BY_SYSTEM`、8 `NOT_FOREGROUND`、9 `HOST_START_FAILED`、10 `PERMISSION_DENIED`、11 `EXACT_ALARM_NOT_ALLOWED`、12 `RESOURCE_NOT_FOUND`、13 `STORAGE_FAILED`、14 `SERVICE_START_NOT_ALLOWED`、15 `SETTINGS_NOT_OPENED`（`H:21-39`）。Kotlin の `SecurityException` は 4（P2:663-670）
- 構造体（`#pragma pack(8)`。`StructLayoutTest.cpp:96-116`）: `ntk_notification_schedule_options` 32 バイト（struct_size 0、reserved0 4、trigger_at_millis〈int64〉8、inexact 16、not_allow_while_idle 20、not_persist_across_boot 24、alarm_type 28）。`ntk_notification_action` 56 バイト（struct_size 0、reserved0 4、title 8、action_id 16、icon_name 24、launch_app 32、allow_generated_replies 36、semantic_action 40、contextual 44、no_user_interface 48、reserved1 52）
- 列挙: settings target 0 NOTIFICATIONS / 1 APP_DETAILS / 2 EXACT_ALARM、settings result 0 OPENED / 1 OPENED_FALLBACK、tap 0 OPEN_APP / 1 EVENT_ONLY / 2 NONE、event kind 0 BODY_TAP / 1 ACTION / 2 DISMISS（`H:41-72`）
- コールバック: settings `(user_data, error, system_code, result)`、interaction `(user_data, event*)`、shown `(user_data, event*)`、release `(user_data)`（`H:85-92`）
- 名前（アイコン・画像・layout・view ID）は setter では引かず、show / update / schedule / progress の時点で引く。引けなければ `RESOURCE_NOT_FOUND` で何も表示しない。アイコンと画像は drawable → mipmap の順、layout は layout、view ID は id。型は指定できない（`MAN:1235`、P2 AP-8）

### 1.2 イベントの保持と経路

- 通知の操作のイベント（interaction）だけを保つ。ACTIVE な C の登録が 0 の間、32 件まで保ち、次の登録の挿入の中で配る。最初の登録より前のものも同じく渡る（`NB:436-472`、`K/presentation/event/NotificationEvents.kt:56-64`）。shown は保たない
- 本文のタップ（OPEN_APP）は、見えない Activity（`NotificationLaunchActivity`）でイベントを出し、ランチャーの Intent でアプリを開く。起動の Intent に actionId の action も extra も付かない（`K/presentation/event/NotificationEventReceivers.kt:27-60`、P2 の NT-04）
- shown は予約した通知が発火したときだけ出る（`K/data/repository/ScheduledNotificationReceiver.kt`）
- 2.0.0 のライブラリは、アプリの起動時に 1.x が保存した予約を 1 回だけ消す（`LegacyScheduleCleaner.kt:133-148`、呼ぶ所は `LibraryRuntime.kt:69`）。**2.0.0 の AAR を同梱した Dialog の移行から、もう起きている**（5.3）

### 1.3 前面と権限

- 前面が要るのは `request_permission`（未許可のとき）と `open_settings_async` だけ。表示・予約・チャンネル・Progress・問い合わせは要らない（P1 の設計 5.8）
- Kotlin のライブラリは権限が無いと黙って成功する（1.x も同じ。`OL/data/repository/NotificationRepositoryImpl.kt:279-283`）。C ABI は show / update / 過ぎた時刻の予約の前に確かめて `PERMISSION_DENIED` を返す（P2 AP-16）

## 2. 既存の C# 実装（1.x）

### 2.1 公開 API

`Mgr`（`#if UNITY_ANDROID`。`testing.md` の B 群）。結果型 3 つも `#if UNITY_ANDROID`。Payload と JsonBuilder はガード無し。

| メンバー | 形 | 出典 |
|---|---|---|
| `ActionBodyTap`、`ActionNotificationDismissed` | `android.unity.notification.ACTION_BODY_TAP` / `..._NOTIFICATION_DISMISSED` | `Mgr:23,28` |
| `Operation*` 16 個 | `NotificationResult.Operation` の値 | `Mgr:33-108` |
| `NotificationOperationCompleted`、`NotificationActionTapped`、`NotificationReceived` | イベント | `Mgr:119-129` |
| `Instance`、`Initialize()` | | `Mgr:135-147,191-226` |
| `HasPermission()`、`CanScheduleExactAlarms()`、`AreNotificationsEnabled()`、`IsNotificationScheduled(id, tag = null)` | 同期の bool。失敗は `false` | `Mgr:231-254,425-429` |
| `RequestPermission(Action<bool>? onResult = null)` | Unity の `Permission.RequestUserPermission`（native-toolkit を呼ばない）。許可済みなら呼び出しの中で `onResult(true)` | `Mgr:260-276` |
| `OpenNotificationSettings()`、`OpenAppDetailsSettings()`、`OpenExactAlarmSettings()`、`CreateChannel(string)`、`DeleteChannel(string)`、`ShowNotification(string)`、`UpdateNotification(string)`、`CancelNotification(int, string? = null)`、`CancelAllNotifications()`、`ScheduleNotification(string)`、`CancelScheduledNotification(int, string? = null)`、`CancelAllScheduledNotifications()`、`Start/Update/CompleteProgressForegroundService(string)`、`StopProgressForegroundService()` | 結果は `NotificationOperationCompleted` だけ（per-call 無し） | `Mgr:281-420` |

### 2.2 経路・時点・文言

- 1.x の Kotlin は**呼び出しスレッドで同期に**動き、結果の listener を同期に呼ぶ（`OB:397-444`）。C# のプロキシは `UnityMainThreadDispatcher` に積む（`Mgr:579-601`）ので、**結果のイベントはいつも呼び出しの後**
- C# の準備の失敗（Android 以外、`pluginInstance` が null、`currentActivity` が null）は `"{op} could not be started."`。`Call` の例外は `ex.Message` と `LogError`（`Mgr:468-526`）
- Kotlin の失敗の文言（`executeOperation`、`OB:397-431`）:

| 例外 | `ErrorMessage` |
|---|---|
| `IllegalArgumentException` | `Invalid argument for {op}: {message ?: "Please verify the input parameters."}`（JSON の必須の欠けは `"{key} is required."` など） |
| `ActivityNotFoundException` | `No Activity found to handle {op}. Please check device settings support.` |
| `SecurityException` | `Security restriction while executing {op}: {message ?: "Permission or device policy denied this operation."}` |
| ほか（`JSONException` を含む） | `Failed to {op}: {message ?: 型名}` |

- イベント: タップ・ボタン・dismiss は `NotificationActionReceiver`（Android の main）からプロキシ `onNotificationAction(actionId, notificationId, dataJson)`（`OR:16-33`）。shown は予約の発火で `onNotificationShown(id, tag, channelId)`。どちらも dispatcher に積む。受け手（Kotlin の static）が null なら捨てる
- `OnDestroy`（`_instance == this` のとき）: 3 つの `clear*Listener` で Kotlin の受け手を null にする（`Mgr:173-185`、`OB:98-137`）
- `NotificationActionResult`: `ActionId`、`NotificationId`、`Data`（`IReadOnlyDictionary<string,string>?`。0 件は null。エスケープを解かない自前の読み方。`ActRes:35-78`）
- ログ: JSON の本文をそのまま出す（`Mgr:310,328,364,391`）

### 2.3 JSON の読み方（`OP`。この設計で C# に写す規則）

- `optStringOrNull`: JSON の null か空白なら「無い」（`OP:275-278`）。`requireString` は無ければ `"{key} is required."` の `IllegalArgumentException`（`OP:290-292`）
- 通知（`OP:46-92`）: `channel` 必須（`"channel is required."`）、`id` 必須（`optInt(id, Int.MIN_VALUE)`。`"id is required."`）、`title`・`message` 必須、ほかは省略可で既定値（表は 4.2）
- チャンネル（`OP:116-132`）: `id`・`name` 必須、importance 3、showBadge / enableLights / enableVibration true、lockscreenVisibility 1
- 予約（`OP:24-44`）: `notification`・`schedule` 必須、`triggerAtMillis > 0`（でなければ `"schedule.triggerAtMillis must be greater than 0."`）、exact / allowWhileIdle / persistAcrossBoot 既定 true、alarmType 既定 0
- style（`OP:142-203`）: type が無い・未知は default。bigText は `bigText` 必須。inbox の空白の行を捨てる。messaging の名前の既定は `"You"`、メッセージの時刻が無ければ今。decoratedCustomView は layout が空なら無し、viewActions は type・viewId の無いものを捨てる
- アクション（`OP:94-114`）: title・actionId の無いものを捨てる
- 解いた値の補正（`OB:508-857`）: importance と visibility を丸める、引けないリソースの代わりの値（アプリのアイコン、省略、Default style）、`{name, type}` の型の指定、本文のタップはいつもブロードキャスト（`launchAppOnTap` が真なら受信機からアプリを開く）、dismiss はいつも付ける

### 2.4 テストとサンプル

- テスト: `Tests/Runtime/AndroidNotificationJsonBuilderTests.cs`（3 件。ガード無し）だけ
- サンプル `Runtime/UI/Android/Notification/AndroidNotificationManagerExampleController.cs`（`UNITY_ANDROID || UNITY_EDITOR`）。ボタン 23 個、入力欄なし。カスタムビューの layout はパッケージの外（`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/res/layout/`）。この移行では変えない

## 3. 実装制約

- `agent-rules/coding-rules/common.md` の「Unity Bridge パターン > Android（native-toolkit の C ABI）」と「Clipboard の形」: 2 層、共通部、ガードの二重化、受け口は写して `_free` して取り出し口に入れるだけ、`UnityMainThreadDispatcher` は使わない、どのスレッドでも待たない
- `agent-rules/coding-rules/csharp.md`
- 公開 API を変えない。32 ビットと Mono は対象外（親課題 J-10）
- ほかの OS の JSON リーダー（`WindowsNotificationJsonReader`）を共有しない。Android 用に持つ（`common.md` の「共通ファイルを作らない方針」）

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

### 4.1 `DllImport` と構造体

`Runtime/Android/Notification/AndroidNotificationCApi.cs`（Bridge）。`[DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)] internal static extern`。

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ScheduleOptions { public uint StructSize; public uint Reserved0; public long TriggerAtMillis; public int Inexact; public int NotAllowWhileIdle; public int NotPersistAcrossBoot; public int AlarmType; }   // 32
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ActionEntry { public uint StructSize; public uint Reserved0; public IntPtr Title, ActionId, IconName; public int LaunchApp, AllowGeneratedReplies, SemanticAction, Contextual, NoUserInterface; public uint Reserved1; }   // 56

internal delegate void SettingsCallback(IntPtr userData, int error, uint systemCode, int result);
internal delegate void InteractionCallback(IntPtr userData, IntPtr interaction);
internal delegate void ShownCallback(IntPtr userData, IntPtr shown);
```

- 宣言するのは、1.1 の表のうち 4.2〜4.4 で使う関数（`request_permission`、`cancel_permission_request`、`listener_remove` は使わないので宣言しない）。ハンドル（content、channel、listener、interaction、shown）は `IntPtr`、`size_t` は `UIntPtr`、`int32_t*` の出力は `out int`、色は `uint`、時刻は `long`、文字列は共通部の `AllocUtf8`、文字列の配列は `AllocStringArray`、振動のパターンは `long[]` を確保した領域に写す
- 受け口は Manager の private static メソッドに `[MonoPInvokeCallback]`。delegate は `static readonly`、関数ポインタは 1 回だけ作る。受け口は 3 つ: 設定の完了、操作のイベント、shown

### 4.2 JSON の読み取り（J-N4）

公開の `ShowNotification` などは今のまま JSON 文字列を受ける。C# で読み、ビルダーの呼び出しに変える。**読む形と、キーが無い・空白・null のときの扱いは、1.x の Kotlin の parser（`OP`）に合わせる**。`AndroidNotificationJsonBuilder` が書かないキーも読む（JSON を手で書いている利用者のため）。

読み取りは 2 つの純粋な関数にする: 汎用の JSON リーダー（`AndroidNotificationJsonReader`。Windows の `WindowsNotificationJsonReader` と同じ形を Android 用に持つ。「キーが無い」と「値が null」を区別する）と、JSON から 1.x の規則で中間の値（`AndroidNotificationSpec` などの internal の型。1.x の `UnityNotificationSpecs.kt` と同じ形）を作る関数（`AndroidNotificationJsonMapping`）。中間の値からビルダーを呼ぶのは `extern` 側（層 2b）。

**型の扱い**: 値の型は `AndroidNotificationJsonBuilder` が書く型（文字列・数・真偽・object・配列）で読む。**型が違う値は「無い」として扱う**（例外: `isGroupConversation` は、キーがあれば真偽でない値を false にする。1.x の `has` と `optBoolean`）（org.json の `opt*` が既定値を返すのと同じ向き。ただし org.json は数の文字列を数に、数を文字列に変える。その変換は写さない。5.3）。

| キー（通知） | 型 | 必須 | ビルダーへ | 1.x と同じにする点（`OP`） |
|---|---|---|---|---|
| `id` | 数（整数） | 必須（`"id is required."`）。**`int.MinValue`（-2147483648）も「無い」** | `content_create` の id | `:54-55`（`optInt(id, Int.MIN_VALUE)` が MIN なら欠落）。小数は 0 方向に切り捨てる。int に収まらない数は「無い」 |
| `title` / `message` | 文字列 | 必須（空白も不可。`"title is required."`） | `content_create` | `:56-57` |
| `channel` | object | 必須（`"channel is required."`） | チャンネルのビルダー → `set_channel` | `:50-51` |
| `tag` / `subText` / `soundUri` / `category` / `ticker` / `groupKey` / `sortKey` | 文字列 | 省略可（空白は無し） | `set_tag` / `set_sub_text` / `set_sound` / `set_category` / `set_ticker` / `set_group` / `set_sort_key` | `:58,65,68-69,73-74,77` |
| `smallIcon` / `largeIcon` | object `{name, type?}` | 省略可（name が空白なら無し） | `set_small_icon` / `set_large_icon` に name だけ | `:60-61,238-245`。type は渡せない（5.3） |
| `priority` | 数 | 省略可（0） | `set_priority`。**-2〜2 に丸める**（J-N5） | `:62`。1.x は検査せず渡し、API 26 以降は表示に効かない |
| `autoCancel` / `showTimestamp` | 真偽 | 省略可（true） | `set_auto_cancel` / `set_show_timestamp` | `:63,66` |
| `ongoing` / `isGroupSummary` / `onlyAlertOnce` / `localOnly` / `silent` / `usesChronometer` | 真偽 | 省略可（false） | 各 setter | `:64,75,78-81` |
| `timestampMillis` / `timeoutAfterMillis` | 数 | 省略可（null は無し） | `set_timestamp` / `set_timeout_after` | `:67,82` |
| `visibility` | 数 | 省略可（1） | `set_visibility`。**-1 / 0 / 1 以外は 1 に丸める** | `:70`、`OB:806-813` |
| `color` / `number` | 数 | 省略可 | `set_color`（**int.MinValue〜uint.MaxValue の整数**を `uint` に写す。負の値は 2 の補数のまま〈1.x の Payload は `int?` で負の ARGB を書く〉、それより外は「無い」）/ `set_number` | `:71-72` |
| `groupAlertBehavior` | 数 | 省略可（0） | `set_group_alert_behavior` | `:76` |
| `progress` | object `{max, current, indeterminate}` | 省略可（max / current 0、indeterminate false） | `set_progress`。**不定でなければ max を 0 以上に、current を 0〜max に丸める**（J-N5） | `:83,134-140` |
| `style` | object | 省略可（default） | 下の表 | `:84,142-203` |
| `launchAppOnTap` | 真偽 | 省略可（true） | `set_tap`: 真は `OPEN_APP`、偽は `EVENT_ONLY` | `:86`。1.x は偽でもイベントを出した（`OB:571-587`） |
| `launchAction` | 文字列 | - | **読まない** | 1.x も読むだけで使わなかった（`OP:87`、事実の一覧 4.3） |
| `actions` | 配列（要素は object） | 省略可 | 要素ごとに `add_action` | `:88,94-114`。object でない要素、title か actionId が無い要素を捨てる |
| `fullScreenIntent` | 真偽 | 省略可（false） | `set_full_screen` | `:89` |
| `data` | object | 省略可。0 件なら無し | キーごとに `add_data`。値は文字列のときだけ（ほかの型・null は捨てる。5.3） | `:90,256-261`（1.x は `optString` でどの型も文字列にした） |
| そのほかのキー | - | - | 無視 | 1.x と同じ |

| キー（チャンネル。通知の中と `CreateChannel` の両方） | 型 | 必須 | ビルダーへ | 1.x と同じにする点 |
|---|---|---|---|---|
| `id` / `name` | 文字列 | 必須（空白も不可。`"id is required."` / `"name is required."`） | `channel_create` | `OP:118-119` |
| `importance` | 数 | 省略可（3） | `channel_create`。**0〜4 以外は 3 に丸める** | `OP:120`。1.x のブリッジは -1000 と 0〜5 を通し、ほかを 3 にした（`OB:793-804`）。ライブラリが 5 を 3 に下げた（`OL/data/repository/NotificationRepositoryImpl.kt:600-608`）。-1000 は 1.x では Android が拒んだ見込み（推測。5.3） |
| `description` / `soundUri` | 文字列 | 省略可 | `set_description` / `set_sound` | `OP:121,127` |
| `showBadge` / `enableLights` / `enableVibration` | 真偽 | 省略可（true） | 各 setter | `OP:122-123,125` |
| `lightColor` | 数 | 省略可 | `set_light_color`（`color` と同じ範囲と写し方） | `OP:124` |
| `vibrationPattern` | 配列（数） | 省略可。数でない要素を捨て、0 件なら無し | `set_vibration_pattern` | `OP:126,263-273` |
| `lockscreenVisibility` | 数 | 省略可（1） | `set_lockscreen_visibility`。**-1 / 0 / 1 以外は 1 に丸める** | `OP:128`、`OB:806-813` |
| `groupId` / `groupName` | 文字列 | 省略可 | groupId があるときだけ `set_group(groupId, groupName ?? groupId)` | `OP:129-130`。1.x のライブラリは groupName が無ければ groupId を名前にした（`OL/data/repository/NotificationRepositoryImpl.kt:99-104`） |

| style の `type` | ビルダーへ | 1.x と同じにする点 |
|---|---|---|
| 無い・空白・未知・`"default"` | 何も呼ばない | `OP:146,201` |
| `"bigText"` | `set_style_big_text(bigText, summaryText, bigContentTitle)`。`bigText` 必須（`"bigText is required."`） | `OP:148-155` |
| `"inbox"` | `set_style_inbox(lines, summaryText, bigContentTitle)`。空白の行を捨てる | `OP:157-164,247-254` |
| `"bigPicture"` | `picture.name` と `pictureUri` の**どちらも無ければ何も呼ばない**（Default）。あれば `set_style_big_picture(name, uri, summaryText, bigContentTitle, largeIcon.name, hideExpandedLargeIcon)` | `OP:166-176`、`OB:686-700` |
| `"messaging"` | `set_style_messaging(userDisplayName ?? "You", conversationTitle, isGroupConversation が**キーごと無ければ -1、null か真偽でない値なら 0**、真偽ならその値)` と、メッセージごとに `add_message(text, timestampMillis ?? 今の Unix ミリ秒, senderName)`。text の無いメッセージを捨てる | `OP:178-190,222-236`（`has` が真なら `optBoolean`、null は false） |
| `"decoratedCustomView"` | `customViewLayout` が無い・空白・null なら何も呼ばない（Default）。`bigCustomViewLayout` の空白・null は無し（1.x は空でない文字列ならそのまま引き、引けなければ Default か省略した。`OP:195-196`、`OB:717-729`。空白を名前として渡すと J-N6 で失敗するので、空白と null は「無い」に寄せる。手で書いた JSON だけに起こる）。あれば `set_style_custom_view(layout, bigCustomViewLayout)` と、`viewActions` のうち type が `"setClickIntent"` で viewId と actionId のあるものに `add_view_click(viewId, actionId)` | `OP:192-220`、`OB:717-771` |

| キー（予約の最上位と `schedule`） | 型 | 必須 | 写し方 | 1.x と同じにする点 |
|---|---|---|---|---|
| `notification` / `schedule` | object | 必須（`"notification is required."` / `"schedule is required."`） | 通知は上の表、`schedule` は下 | `OP:30-33` |
| `schedule.triggerAtMillis` | 数 | 1 以上（でなければ `"schedule.triggerAtMillis must be greater than 0."`） | `TriggerAtMillis` | `OP:37-38` |
| `schedule.exact` / `allowWhileIdle` / `persistAcrossBoot` | 真偽 | 省略可（true） | `Inexact = !exact`、`NotAllowWhileIdle = !allowWhileIdle`、`NotPersistAcrossBoot = !persistAcrossBoot` | `OP:39-41` |
| `schedule.alarmType` | 数 | 省略可（0） | `AlarmType` | `OP:42` |

**アクション**（`actions[]`）: `title`・`actionId` 必須（無い要素は捨てる）、`icon`（`{name}`）、`launchApp` false、`allowGeneratedReplies` false、`semanticAction` 0、`contextual` false、`showsUserInterface` true（`NoUserInterface = !showsUserInterface`）（`OP:94-114`）。

**JSON の構文**: 最上位が object でない、構文の誤り、`null` / 空の文字列、入れ子が 512 を超える → `Failed to {op}: the JSON could not be read.`（1.x は org.json の例外の文言。5.3）。**重複したキーは後の値が勝つ**（Android の org.json〈libcore〉と同じ）。libcore の緩い構文（引用符の無いキー、単引用符、コメント、`=` / `=>`）は受けない（5.3）。入れ子の上限は libcore に無い（深い入れ子は StackOverflow になりうる）。Windows のリーダーと同じ 512 を、安全のための上限として採る

**Progress の 3 つ**（start / update / complete）: `progress` が無ければ `Invalid argument for {op}: progress is required for Progress FGS operations.`（1.x と同じ。`OB:381,401-406`）。

### 4.3 そのほかの引数

| メソッド | 2.0.0 へ | 1.x |
|---|---|---|
| `CancelNotification(id, tag)` / `CancelScheduledNotification(id, tag)` / `IsNotificationScheduled(id, tag)` | `remove` / `cancel_scheduled` / `is_scheduled`。**tag は null だけを NULL にし、空・空白もそのまま渡す** | 同じ。空白をタグ無しに寄せるのは Kotlin の `cancel` だけで、予約は寄せない（`OL/.../NotificationRepositoryImpl.kt:85`、`1.12.0:.../NotificationSchedulerSupport.kt:39`、`NotificationUseCases.kt:40-43`）。2.0.0 の Kotlin も同じ規則 |
| `DeleteChannel(channelId)` | `delete_channel`。null は `""` | null は Kotlin の引数の検査で例外だった見込み（5.3） |
| `CancelAllNotifications()` / `CancelAllScheduledNotifications()` / `StopProgressForegroundService()` | `remove_all` / `cancel_all_scheduled` / `stop_progress` | 同じ |
| `OpenNotificationSettings()` / `OpenAppDetailsSettings()` / `OpenExactAlarmSettings()` | `open_settings_async(0 / 1 / 2)`（J-N9） | 同期に開いた |
| `RequestPermission(onResult)` | **今のまま**（Unity の `Permission.RequestUserPermission`。J-N10） | 同じ |

### 4.4 結果とイベント

- **操作の結果はどれも取り出し口 `AndroidNativeCallbackQueue` を通して、呼び出しの後に出す**（J-N1。1.x と同じ。`Mgr:579-601`）。届け先は取り出した時点の `_instance`（J-N3）
- 同期の操作: 戻り値 `NONE` で成功、ほかは 4.5 の文言で失敗。`schedule` は `ScheduleWithFallback`（J-N8）を通す（正確な予約で `EXACT_ALARM_NOT_ALLOWED` なら 1 回だけ不正確で呼び直す）
- 設定の画面: 完了の受け口（Android の main）が、`user_data`（target の値）と `error` / `result` から結果を作って `Post` する。`OPENED` / `OPENED_FALLBACK` は成功（J-N9）
- 操作のイベント（J-N13）: 受け口が kind、通知の ID、action_id、data を読んで `_free` し（`ReadAndFree`）、`Post`。`NotificationActionTapped(new NotificationActionResult(actionId, notificationId, data))`。actionId は BODY_TAP → `ActionBodyTap`、DISMISS → `ActionNotificationDismissed`、ACTION → `action_id`。**未知の kind と、action_id の無い ACTION は、ログを出して捨てる**（`ActionIdOf` が null を返す）。data は 0 件なら null
- shown（J-N14）: `NotificationReceived(new NotificationReceivedResult(id, tag, channelId))`（tag の空は null。1.x と同じ。`NotificationReceivedResult.cs`）
- 問い合わせ: `NONE` なら値、ほかは `false`（J-N11）

### 4.5 エラーの文言とログ

`{op}` は `Operation*` の値。文言への変換は Bridge の純粋な関数（層 1）。

| 原因 | `ErrorMessage` | ログ |
|---|---|---|
| Editor・Android 以外（J-N16） | `{op} could not be started.` | `LogWarning("{op} can only be called on an Android device.")`（1.x と同じ。`Mgr:511-526`） |
| ネイティブが使えない / `NOT_INITIALIZED` | `{op} could not be started.`（1.x の `pluginInstance` が null と同じ文言） | `LogWarning` |
| JSON の必須の欠け・値の誤り（4.2） | `Invalid argument for {op}: {key} is required.` など（1.x と同じ） | `LogWarning` |
| JSON の構文 | `Failed to {op}: the JSON could not be read.`（新しい） | `LogWarning` |
| `INVALID_PARAMETER`（ビルダーの範囲の検査・Kotlin の `IllegalArgumentException`） | `Invalid argument for {op}: Please verify the input parameters.`（1.x の message が無いときの文言） | `LogWarning`（入口の `NULL` は C# の誤りなので `LogError`。区別できないので、どの setter で拒まれたかをログに出す） |
| `NOT_SUPPORTED` | `Failed to {op}: NOT_SUPPORTED` | `LogError`（C# の組み立ての誤り） |
| `UNKNOWN` / `OUT_OF_MEMORY` / `CANCELED` / `CANCELED_BY_SYSTEM` / `HOST_START_FAILED` / 想定外の値 | `Failed to {op}: <名前>`（想定外の値の名前は `ERROR_<値>`。Clipboard と同じ。`Failed to {op}: UNKNOWN` など。6・7・9 は権限の要求だけの値で、この設計では来ない） | `LogWarning` |
| `NOT_FOREGROUND`（設定の画面） | `Failed to {op}: the app is not in the foreground.`（新しい） | `LogWarning` |
| `PERMISSION_DENIED`（J-N7） | `Failed to {op}: notifications are not allowed for this app (permission or app notification setting).`（新しい。権限が無いときと、アプリの通知がオフのときの両方。C ABI は区別しない。`NB:132-136`） | `LogWarning` |
| `EXACT_ALARM_NOT_ALLOWED` | 不正確で呼び直す（J-N8）。呼び直しの失敗はその値の文言 | `Debug.Log`（呼び直したこと） |
| `RESOURCE_NOT_FOUND` | `Failed to {op}: a resource named in the notification was not found.`（新しい） | `LogWarning` |
| `STORAGE_FAILED` | `Failed to {op}: the schedule store could not be read or written.`（新しい。予約と取り消しで来る。`is_scheduled` でも来るが、問い合わせは `false` を返す〈J-N11〉） | `LogWarning` |
| `SERVICE_START_NOT_ALLOWED` | `Failed to {op}: the system did not allow the foreground service from the background.`（新しい。start と stop の両方で来る。P2 の NT-27） | `LogWarning` |
| `SETTINGS_NOT_OPENED` | `No Activity found to handle {op}. Please check device settings support.`（1.x で両方の画面が `ActivityNotFoundException` のときと同じ。`SecurityException` のときの 1.x の文言は 5.3。`OB:466-483,401-424`） | `LogWarning` |
| C# の例外 | `ex.Message`（1.x の `Call` の例外と同じ扱い） | `LogError` |
| 受け口の中の例外 | イベントは出ない | `LogError` |

**ログ**: `common.md` の規則（入力の値は長さだけ）に合わせ、JSON をそのまま出すのをやめる（R1-19）。出すのは操作名、通知の ID、題名と本文の長さ、style の種類、アクションと data の件数、tag の有無。1.x の C# は JSON をそのまま出していた（`Mgr:310,328,364,391`）。2.0.0 のライブラリも本文と data の値を伏せる（`BLD:285,353-355`）

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | 置き場所 | ガード | 内容 |
|---|---|---|---|---|
| 新規作成 | `AndroidNotificationCApi.cs` | `Runtime/Android/Notification/` | 型と純粋な関数は `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport` は `UNITY_ANDROID && !UNITY_EDITOR` | Bridge。4.1 の構造体・delegate・`extern`、エラー定数 0〜15、4.5 の文言（`ErrorName`、`FailureMessage(op, error)`、`IsRequestDefect`）、操作のイベントの写し（`ActionIdOf(kind, actionId)`）、中間の値からビルダーを呼ぶ関数（`BuildContent(spec, allocated)`、`BuildChannel(spec)`。`extern` 側）、予約の options（`BuildScheduleOptions(spec)`） |
| 新規作成 | `AndroidNotificationJsonReader.cs` | 同上 | `UNITY_ANDROID \|\| UNITY_EDITOR` | 汎用の JSON リーダー（4.2。internal） |
| 新規作成 | `AndroidNotificationJsonMapping.cs` | 同上 | 同上 | JSON → 中間の値（`AndroidNotificationSpec`、`AndroidNotificationChannelSpec`、`AndroidNotificationScheduleSpec` などの internal の型）。1.x の `OP` の規則（4.2）。失敗は文言を返す |
| 既存変更 | `AndroidNotificationManager.cs` | 同上 | 型は `UNITY_ANDROID \|\| UNITY_EDITOR`（今は `UNITY_ANDROID`）。受け口などは `UNITY_ANDROID && !UNITY_EDITOR` | `AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher` を消し、Bridge を呼ぶ。5.2 の J-N1〜J-N20。`RequestPermission` は今のまま。**XML コメント**（5.3 を写す）: クラスの要約、`ShowNotification` などの権限・通知のオフ（J-N7）、引けないリソース（J-N6）、設定の画面の前面（J-N9）、どのスレッドからでも（J-N15。ただし `RequestPermission` は Unity の権限 API なので UnityMain から）、**保たれていたタップのイベントは Manager を最初に作った後のフレームで出るので、`Instance` を最初に取ったのと同じフレームで購読すること**（J-N12）。**層 2a の継ぎ目**（`internal`）: `SetNativeForTests(Func<int>? addInteraction, Func<int>? addShown)`（リスナーの登録を数える）、`ScheduleWithFallback(int inexact, Func<int, int> schedule)`（J-N8 の呼び直しの判断。純粋。**最初は `schedule(inexact)`、呼び直すのは `inexact == 0` で結果が `EXACT_ALARM_NOT_ALLOWED` のときだけ `schedule(1)`**。R2-1。置き場所は Manager の internal static、テストは層 2a）、`ReadAndFree<T>(IntPtr, Func<IntPtr,T>, Action<IntPtr>)`（イベントのハンドルを 1 回だけ解放する。Clipboard と同じ形）、`EnsureListeners()`、`OnInteractionCore(int kind, int notificationId, string? actionId, IReadOnlyList<KeyValuePair<string,string>> data)`、`OnShownCore(int id, string? tag, string? channelId)`、`OnSettingsCore(int target, int error, int result)`、`ResetStaticStateForTests()` |
| 既存変更 | `NotificationResult.cs`、`NotificationActionResult.cs`、`NotificationReceivedResult.cs` | 同上 | `UNITY_ANDROID \|\| UNITY_EDITOR`（Manager と同じ群） | `NotificationActionResult` の今の internal のコンストラクタ（`dataJson` を読む。`ActRes:11-33,35-78`）を、読んだ組から作るコンストラクタに置き換える（古い方は消す。R1-7）。公開の形は変えない |
| 既存変更 | `AndroidNotificationJsonBuilder.cs` | 同上 | 今のまま | XML コメントに「2.0.0 では Manager は使わない。段 4 で消す」 |
| 新規作成 | `AndroidNotificationCApiTests.cs`、`AndroidNotificationJsonReaderTests.cs`、`AndroidNotificationJsonMappingTests.cs` | `Tests/Runtime/` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（7.1） |
| 新規作成 | `AndroidNotificationManagerEditorTests.cs` | `Tests/PlayMode/` | `UNITY_EDITOR` | 層 2a（7.2） |
| 新規作成 | `AndroidNotificationPlayerTests.cs`、`AndroidNotificationManagerPlayerTests.cs` | `Tests/PlayMode/` | 外 `UNITY_ANDROID`、中 `!UNITY_EDITOR` | 層 2b（UI テスト計画） |
| 既存変更 | `android_ui_driver.py`、`AndroidUiDriver.cs` | `scripts/`、`Tests/PlayMode/` | - | 通知のシェードと権限のダイアログの命令、権限の付与（`pm grant`）と正確なアラームの許可（`appops`）の命令（J-N21。補助の設計 v12） |
| 新規作成 | 補助の設計 v12、UI テスト計画 v1 | `artifact/topics/android-c-abi-2/designs/`、`artifact/android/notification/designs/` | - | 7.3、8.3 |
| 既存変更 | `common.md`、`testing.md`、親課題の README | - | - | Notification の形、`verify_unity_android.sh` がテストの Player を実機の実行の前（`--as-package-user` の 2 回の実行の間を含む）と中断のときにもアンインストールする（R3-1。補助の設計 v12 と一緒に作る）、ガードの表（B 群から A 群へ）、層、段の状況。`common.md` の「まだ移していない機能」の行を消す |
| 非変更 | Payload 10 型、`AndroidNotificationJsonBuilderTests`、サンプルの Controller と UXML、`Assets/Plugins/Android/*` | - | - | |

### 5.2 判断

| # | 内容 | 決定 |
|---|---|---|
| J-N1 | 結果の時点 | **C の関数（同期）を呼び出しスレッドで呼び、結果を取り出し口に入れて、呼び出しの後に出す**。設定の画面（非同期）は完了の受け口から入れる。1.x はどの結果も dispatcher を通した（`Mgr:579-601`） |
| J-N2 | per-call | 無い（1.x と同じ）。`RequestPermission` の `onResult` だけ（J-N10） |
| J-N3 | 届け先 | 取り出した時点で生きている Manager（`_instance`）。いなければ捨てる（Share・Clipboard と同じ。1.x は `OnDestroy` で受け手を外した。`Mgr:173-185`） |
| J-N4 | JSON | C# で読む（4.2）。1.x の `OP` の規則（必須・既定・空白・捨てる要素）を写す。型の違う値は「無い」。構文の誤りは 1 つの文言 |
| J-N5 | 1.x の補正のうち保つもの | importance（0〜4 以外 → 3）、visibility と lockscreenVisibility（-1〜1 以外 → 1）、**priority（-2〜2 に丸める）、progress（不定でなければ max ≥ 0、current を 0〜max に丸める）**、**groupName の無い groupId（groupId を名前にする）**、messaging の名前 `"You"` とメッセージの時刻（今）、inbox の空白の行、title / actionId の無いアクション、text の無いメッセージ、type・viewId・actionId の無いクリック、画像も URI も無い bigPicture と layout の無い custom view を Default にすること、0 件の data を無しにすること、`id` の `int.MinValue` を欠落にすること。どれも JSON の段で決まり、C# で安く写せる。priority は API 26 以降は表示に効かず、progress のはみ出しは 1.x では OS が丸めた（ふつうの使い方で起こりうる） |
| J-N6 | 保たないもの（5.3） | **引けないリソースの名前**（**利用者の判断、2026-10-12**。1.x はアプリのアイコン・省略・Default に代えた。C ABI は `RESOURCE_NOT_FOUND` で何も表示しない。**native-toolkit の申し送り〈P2 AP-8、16 章、`Handoff`:164〉は「包みが 1.x の代わりの値を保つ」としていたが、それと違う判断**。C# からはどの名前が引けなかったかが分からず、名前の群を外して呼び直しても 1.x と同じ結果にはならない〈引けた名前まで外れる〉。名前の誤りは利用者が直すべきもの。リソースの縮小で消える危険は、移行ガイドとマニュアルに `tools:keep` として書く〈`MAN:1235-1240`〉）。**リソースの型の指定**（C ABI は名前だけ）。**範囲の外の値**（groupAlertBehavior、timestamp、timeoutAfter、semanticAction、alarmType、振動の負の値、メッセージの負の時刻。1.x は検査せず渡した。C ABI は `INVALID_PARAMETER`） |
| J-N7 | 権限が無い・通知がオフのときの表示（AP-16） | **失敗にする**（利用者の判断、2026-10-12。J-11 の例外。アプリの通知がオフのときも含める〈同日、利用者の判断〉）。show / update / 過ぎた時刻の予約の `PERMISSION_DENIED` を 4.5 の文言にする。1.x は黙って成功した（`OL/.../NotificationRepositoryImpl.kt:279-283`）。親課題の README の J-11 に例外として書く |
| J-N8 | 正確なアラームが不許可 | **1.x と同じく不正確に下げる**: `schedule` が `EXACT_ALARM_NOT_ALLOWED` を返したら **正確な予約（`exact` が真）のときだけ** `Inexact = 1` で 1 回だけ呼び直す（`ScheduleWithFallback`）（P2 の NT-21 の案。1.x は Kotlin が黙って下げた。`OL/.../NotificationRepositoryImpl.kt:326`）。Android 14 以降は既定で不許可の端末が多く、ふつうの使い方に当たる。呼び直しの判断は純粋な `ScheduleWithFallback` にし、層 2a で数える（R1-10） |
| J-N9 | 設定の画面 | `open_settings_async` を呼ぶ。`user_data` は target の値（呼び出しごとの状態が要らない。Dialog の J-D1 と同じ形）、`release` は NULL。完了の `OPENED` / `OPENED_FALLBACK` は成功（1.x も fallback を成功にした。`OB:446-490`）。`NOT_FOREGROUND` と `SETTINGS_NOT_OPENED` は失敗（4.5）。入口の拒否は呼び出しの中で結果を作って `Post` |
| J-N10 | 権限の要求 | **今のまま**（Unity の `Permission.RequestUserPermission`。`Mgr:260-276`）。C ABI の `request_permission` は使わない（結果の時点・前面の扱いが 1.x と変わるため。保つ範囲の中でいちばん単純） |
| J-N11 | 問い合わせ | `NONE` なら値、ほかは `false` と `LogWarning`（1.x も `false`。`Mgr:447-466`） |
| J-N12 | リスナー | 操作のイベントと shown のリスナーを、**プロセスで 1 回だけ登録し、外さない**（Share・Clipboard と同じ。static の受け口、`user_data` と `release` は NULL）。**登録済みの印は種類ごとに 2 つ持ち**、失敗した側だけをやり直す（R1-11、X1-8）。登録は登録専用の `lock` の中で、`Initialize()`（`Awake` から）と、通知を出す・予約する各メソッドの前（Clipboard の `EnsureChangeListener` と同じく、やり直しの機会を増やす）。失敗は `LogWarning`、例外は `LogError`。**最初の登録で、それまで保たれていた操作のイベント（アプリが起動する前のタップを含む）が届く**（5.3） |
| J-N13 | 操作のイベントの写し | 4.4。tag は 1.x と同じく出さない（結果型に無い） |
| J-N14 | shown | 4.4 |
| J-N15 | 呼ぶスレッド | どのスレッドから呼んでもよい。`Instance` の最初の取得は UnityMain。`Awake` と各メソッドで `EnsureCreated()` |
| J-N16 | Editor・Android 以外 | 1.x と同じ: `Initialize` はログだけ、問い合わせは `LogWarning` と `false`、操作は `LogWarning` と `{op} could not be started.` を後で、`RequestPermission` は何もしない（`Mgr:194-198,447-526`）。JSON の検査より前に判定する（1.x の Editor は JSON を読まなかった） |
| J-N17 | JSON の層 | `AndroidNotificationJsonBuilder` は公開のまま残し Manager からは使わない（Share・Clipboard と同じ）。1.x の C# に parser は無かった（Kotlin が読んだ） |
| J-N18 | `NotificationActionResult` の data | 読んだ組をそのまま辞書にする（エスケープの残りが無い）。0 件は null（1.x と同じ。`ActRes:63`） |
| J-N19 | 1.x の予約の破棄 | C# では何もしない。2.0.0 のライブラリが起動時に 1.x の予約を 1 回だけ消す（1.2）。Dialog の移行（2.0.0 の AAR の同梱）から起きている。移行ガイドに書く |
| J-N20 | `OnDestroy` | 自分が `_instance` のときだけ `_instance = null`。リスナーは外さない（J-N12）。壊した後のイベントは取り出し口で捨てる（1.x も捨てた） |
| J-N21 | UI の補助 | 通知のシェードを開く・通知を探す・本文やボタンを押す・スワイプで消す命令と、権限のダイアログを押す命令、権限の付与（`pm grant`）と正確なアラームの許可（`appops`）の命令を足す（利用者の判断、2026-10-12）。補助の設計 v12 にする（8.3） |

### 5.3 既知の差分（意図したもの）

「要確認」と「推測」の行のうち、1.x の側は 5.5 の手順 3（1.x の実測）で、C ABI の版の側は手順 7 で直す。

| 差分 | 1.x | 2.0.0 移行後 | 根拠 |
|---|---|---|---|
| 権限が無い・通知がオフのときの表示（J-N7。利用者の判断） | 成功（通知は出ない） | `Failed to {op}: notifications are not allowed for this app (permission or app notification setting).` | `OL/.../NotificationRepositoryImpl.kt:279-283`、`NB:132-156` |
| 本文のタップ・アクションの `launchApp` でアプリを開く | ブロードキャストの受信機から `startActivity`（Android 12 以降は止められる見込み。**要確認**） | 見えない Activity から開く | `OR:25-32`、`NotificationEventReceivers.kt:27-60` |
| アプリを開く起動の Intent | `action` が actionId、`NEW_TASK \| CLEAR_TOP` | actionId の action も extra も付かない（ACTION_MAIN と CATEGORY_LAUNCHER のランチャーの Intent）。`NEW_TASK \| RESET_TASK_IF_NEEDED` | `OR:26-31`、P2 の NT-04 |
| 最初の登録より前のタップ（アプリが起動する前を含む） | 捨てた | 32 件まで保ち、最初の登録（Manager の `Awake`）で届く。**最初の登録の後は保たない**（登録を外さないので、Manager がいない間のイベントは取り出し口で捨てる。X1-9） | `OR:23`、`NB:436-472` |
| 引けないリソースの名前（J-N6。利用者の判断。申し送りと違う） | アプリのアイコン・省略・Default style に代えた | `Failed to {op}: a resource named in the notification was not found.`、何も表示しない | `OB:526-857`、`NB:556-669`、P2 AP-8 |
| リソースの型 | `{name, type}` の型だけを探した | 名前だけ。drawable → mipmap（layout は layout、view は id） | `OB:815-857`、P2 AP-8 |
| 範囲の外の値（J-N6） | 検査せずに渡した（groupAlertBehavior、timestamp、timeoutAfter、semanticAction、alarmType、振動の負の値、メッセージの負の時刻） | `Invalid argument for {op}: Please verify the input parameters.` | `OP` 全体、`BLD:142-544` |
| importance の -1000（`IMPORTANCE_UNSPECIFIED`） | Android が拒んで失敗した見込み（**推測**。`NotificationChannel` の importance の検査） | 3 に丸めて成功 | `OB:793-804`、`OL/.../NotificationRepositoryImpl.kt:600-608` |
| JSON の構文の誤り・null・空 | `Failed to {op}: <org.json の例外の文言>` | `Failed to {op}: the JSON could not be read.` | `OB:419-424` |
| JSON の緩い構文（引用符の無いキー、単引用符、コメント、`=` / `=>`） | org.json（libcore）が受けた | 構文の誤り | 同上 |
| JSON の型の違い・数の扱い | org.json が数の文字列を数に、数を文字列に、真偽の文字列を真偽に変えた。int に収まらない数は巻き戻した | 「無い」として扱う。小数は 0 方向に切り捨てる。`color` / `lightColor` は int.MinValue〜uint.MaxValue の整数を受ける（負の値も符号無しの ARGB も） | `OP:275-292` |
| `isGroupConversation` の真偽でない値 | org.json が真偽の文字列を真偽に変えた（`"true"` は true） | キーがあれば false（真偽の文字列も false） | `OP:183-187` |
| 設定の画面が `SecurityException` で開けない | `Security restriction while executing {op}: ...` | `SETTINGS_NOT_OPENED` → `No Activity found to handle {op}. ...` | `OB:470-483`、`K/data/repository/AndroidNotificationSettingsGateway.kt:57-66` |
| `data` の文字列でない値・null、`lines` の null の要素 | 文字列にした（null は `"null"`） | 捨てる | `OP:251,259` |
| 設定の画面（J-N9） | 呼んだスレッドで同期に開き、後ろからでも成功 | Android の main で前面を確かめ、前面でなければ `Failed to {op}: the app is not in the foreground.`。成功は開いた後 | `OB:446-490`、`NB:318-346` |
| 通知の設定の画面の Intent | `package:` の data を付けた | 付けない。1.x は `package:` のせいで、いつもアプリの詳細の画面に落ちていた見込み（**要確認**）。2.0.0 は通知の設定の画面を開く | P2 の NT-12、`K/data/repository/AndroidNotificationSettingsGateway.kt:34-36` |
| 設定の画面の結果の時点 | 開いた直後（呼び出しの中で同期に。イベントは次のフレーム） | Android の main で `startActivity`（と fallback の判断）の直後に完了する。設定の画面から戻るのは待たない。**C# のイベントは取り出し口を通るので、Unity が止まっている間は出ない**（画面を離れる前か戻った後かは競合。テストは結果を待つことだけを期待にする。R3-6）。呼び出しの後、Android の main が積んだ処理を走らせてから | `OB:446-490`、`NB:336-340`、`K/data/repository/AndroidNotificationSettingsGateway.kt:43-61` |
| full screen の起動の Intent | `NEW_TASK \| CLEAR_TOP`、mutable | `NEW_TASK \| RESET_TASK_IF_NEEDED` | `OB:607-620`、`NotificationEventIntents.kt:106-116` |
| Kotlin の `SecurityException` | `Security restriction while executing {op}: ...` | `Failed to {op}: UNKNOWN` | `OB:413-418`、P2:663-670 |
| 不正確に下げた予約（J-N8）の再起動の後 | 復元のときに正確にできるかを判定し直した | 不正確のまま残る | `1.12.0:.../NotificationRepositoryImpl.kt:170-181,326` |
| 結果とイベントが Manager を壊す前後に来る（J-N3） | 壊す前に dispatcher に積まれたものは、壊した Manager（プロキシの持ち主）のイベントに出た | 取り出した時点で生きている Manager に出し、いなければ捨てる（作り直していれば新しい Manager に出る） | `Mgr:579,614-661`、`UnityMainThreadDispatcher.cs:43-63` |
| 1.x が保存した予約 | 残った | 2.0.0 のライブラリが起動時に 1 回だけ消す（Dialog の移行から） | `LegacyScheduleCleaner.kt:133-148` |
| 1.x が出した通知 | タップ・ボタンに反応した | 2.0.0 にした後はタップにもボタンにも反応しない（受信機のクラス名が変わった） | `MAN:183,1199` |
| 予約の発火の shown | 受け手が null なら捨てた | 同じく保たない | `NotificationEvents.kt:62-63` |
| `NotificationActionResult.Data` の値 | `JSONObject.toString()` のエスケープが残った（`"` を含む値、`/` が `\/` になった URL など。**要確認**） | 元の値 | `ActRes:66-78` |
| Progress を後ろから | `Failed to {op}: <例外の文言>`（推測） | `Failed to {op}: the system did not allow the foreground service from the background.` | `OB:419-424`、P2 AP-14 |
| 別のスレッドから呼ぶ（J-N15） | 失敗した見込み（Clipboard・Share と同じ経路。**要確認**） | 受け付ける | Clipboard 設計 v4 の AC-24 |
| `DeleteChannel(null)`・JSON が null | Kotlin の例外の文言（推測） | `DeleteChannel` は `""`、JSON は構文の誤りの文言 | 事実の一覧 8 章の 5 |
| Editor（J-N16） | Android ターゲットのときだけ型があった | どのターゲットでも型があり、同じ失敗 | `Mgr:3` |
| `Initialize()` | ブリッジを取り、リスナーを差し替えた | リスナーが無ければ登録するだけ | `Mgr:191-226` |
| 32 ビットと Mono | 動いた | ビルドが止まる | 親課題 J-10 |
| `AndroidNotificationJsonBuilder` | Manager が使った | 使わない。段 4 で消す | `Mgr` |

### 5.4 呼び出しの流れと契約

**JSON を受ける操作**（`ShowNotification(json)` の例）:

1. `EnsureCreated()`、今と同じログ
2. Editor・Android 以外なら `LogWarning` と `Post(Failure("{op} could not be started."))` で終わる（J-N16）
3. ネイティブが使えなければ同じく終わる
4. `AndroidNotificationJsonMapping.ReadNotification(json, now)` で中間の値を作る。失敗なら `LogWarning` と `Post(Failure(文言))`
5. `try`: 確保の一覧、`BuildContent(spec, allocated)`（チャンネルのビルダーを中で作り、`set_channel` の直後にその場の `try/finally` で `channel_free` する。`set_channel` は写すので〈`BLD:162-169`〉。ビルダーの拒否はその setter の名前をログに出して失敗）、`ntk_notification_show(content)`。`finally`: `content_free`、`FreeAll`
6. 戻り値から結果を作り（4.5）、`Post`

**予約**: 4 の後、`BuildScheduleOptions`、`ntk_notification_schedule`。`ScheduleWithFallback`（J-N8）を通す。**設定の画面**: 2・3 の後 `ntk_notification_open_settings_async(target, s_settingsPointer, (IntPtr)target, IntPtr.Zero)`。戻り値が `NONE` 以外なら呼び出しの中で結果を `Post`。完了の受け口（Android の main）は `OnSettingsCore(target, error, result)` で結果を `Post`。**問い合わせ**: 2・3 の後（失敗は `false`）、`extern` の値。

**受け口**（Android の main。`[MonoPInvokeCallback]`、全体を `try/catch`）:

- 操作のイベント: kind・ID・action_id・data を読み、`finally` で `ntk_notification_interaction_free`。`OnInteractionCore(...)` が `Post(() => Raise(...))`
- shown: 読んで `ntk_notification_shown_free`。`OnShownCore(...)`
- 設定の完了: `OnSettingsCore(target, error, result)`

**どのスレッドでも Android の main を待たない**。登録の `lock` の中で呼ぶ `add_*_listener` は挿入を積むだけ（`CPP:207-237`）。

### 5.5 実装順

1. この設計のレビュー
2. UI の補助を通知に広げる（補助の設計 v12 とレビュー、実装、doctest）。UI テスト計画 v1（7.3 に番号を振る）とレビュー
3. **1.x のまま** UI テスト計画の 1.x の列を流して記録する。5.3 の 1.x の側の「要確認」と「推測」の行（タップ・アクションでアプリが開くか、別のスレッド、設定の画面がどの画面を開いたか、`Data` のエスケープ、Progress を後ろから、`DeleteChannel(null)`、importance の -1000）を実測する。**記録の条件**: 今のビルドには 2.0.0 の AAR が同居している（`LegacyScheduleCleaner` と 2.0.0 の起動完了の受信機が動く）ので、1.x の列はその状態での記録になる。そのことを記録に書く
4. JSON のリーダーとマッピング、層 1
5. `AndroidNotificationCApi` の構造体・純粋な関数・`extern`・ビルダーの呼び出し、層 1
6. `AndroidNotificationManager` の置き換え（J-N1〜J-N20）、層 2a
7. 実機で UI テスト計画の 2.0.0 の列を流し、1.x の列と比べる。5.3 の C ABI の版の側の「要確認」を実測する
8. `common.md`、`testing.md`、親課題の README（J-11 の例外、段の状況。最後の機能なので、段 4 の残り〈旧ブリッジの AAR を外す〉を確かめる）
9. 実装結果ファイル

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | JSON の必須の欠け・空白 | C# | 1.x の文言 | 1（マッピング）、2b |
| E-2 | JSON の構文・null・空 | C# | `the JSON could not be read.` | 1（リーダー）、2a。重複したキーは誤りにせず、後の値が勝つ（4.2） |
| E-3 | Editor・Android 以外 | C# | `{op} could not be started.`、問い合わせは `false` | 2a（全メソッド） |
| E-4 | ネイティブが使えない・`NOT_INITIALIZED` | C# / ネイティブ | 同上 | 1（文言）。起こせない |
| E-5 | ビルダーの範囲の検査（`INVALID_PARAMETER`） | ネイティブ（ビルダー） | `Invalid argument for {op}: Please verify the input parameters.` | 1（文言）、2b（`groupAlertBehavior` 3。priority の 3 は丸めて成功する） |
| E-6 | `PERMISSION_DENIED` | ネイティブ | 4.5 | 1、2b（権限を取り消した端末。8.2） |
| E-7 | `EXACT_ALARM_NOT_ALLOWED` | ネイティブ | 不正確で呼び直し | 2a（`ScheduleWithFallback`: 正確な予約で 11 なら 1 回だけ inexact で呼び直す、不正確な予約は 1 回だけ 1 で呼ぶ、ほかのエラーでは呼び直さない、呼び直しの失敗の文言）、2b（正確なアラームを不許可にした端末。要検証: adb で切り替えられるか） |
| E-8 | `RESOURCE_NOT_FOUND` | ネイティブ | 4.5 | 1、2b（無い名前のアイコン） |
| E-9 | `STORAGE_FAILED` / `SERVICE_START_NOT_ALLOWED` / `UNKNOWN` / `OUT_OF_MEMORY` / `NOT_SUPPORTED` | ネイティブ | 4.5 | 1（文言）。起こせない（Progress を後ろからは 2b で試す） |
| E-10 | `NOT_FOREGROUND`（設定の画面） | ネイティブ（完了） | 4.5 | 1、2b（後ろから） |
| E-11 | `SETTINGS_NOT_OPENED` | ネイティブ（完了） | 1.x の文言 | 1（文言）。起こせない |
| E-12 | 問い合わせの失敗 | ネイティブ | `false` | 1（ログの分類）。起こせない |
| E-13 | C# の例外 | C# | `ex.Message`、`LogError` | コードレビュー |
| E-14 | 受け口の中の例外 | C# | 出ない、`LogError` | コードレビュー |
| E-15 | リスナーの登録の失敗 | ネイティブ | ログ、次の `Initialize()` か通知の操作でやり直す（種類ごと） | 2a（継ぎ目で数える。片方だけ失敗したとき、やり直しで失敗した側だけ登録する） |
| E-16 | 未知の kind・action_id の無い ACTION | ネイティブ | ログを出して捨てる | 1（`ActionIdOf`） |
| E-17 | イベントのハンドルの解放 | C# | 読めても読む途中で例外でも 1 回だけ `_free` | 2a（`ReadAndFree`）。content とチャンネルのハンドルはコードレビュー（`BuildContent` の中で `set_channel` の直後にチャンネルを解放し、content は呼ぶ側の `finally`） |

## 7. テスト方針

### 7.1 層 1（EditMode）

- `AndroidNotificationCApiTests`: 構造体 2 つの大きさと全フィールドの位置（32 / 56）、コールバックの形、4.5 の全エラー値（0〜15）と想定外の値の文言（16 の操作）、`IsRequestDefect`、`ActionIdOf`（3 つの kind、未知の kind、action_id の無い ACTION）、予約の options の写し（既定、各 bool の反転、alarmType）
- `AndroidNotificationJsonReaderTests`: 構文、文字列のエスケープ、数、入れ子の上限、重複したキー（後の値が勝つ）、「無い」と null の区別
- `AndroidNotificationJsonMappingTests`: 4.2 の各表の行（必須・既定・空白・型の違い・捨てる要素・丸め・style ごと・予約・Progress の必須）。**`AndroidNotificationJsonBuilder` が書いた JSON を読んで、元の Payload と同じ値になること**（比べるのは 1.x が使う値だけ。`launchAction` とリソースの `type` は読まないので比べない。X1-5）。`id` の `int.MinValue`、`isGroupConversation` の無い・null・真偽、`groupName` の無い `groupId`、priority と progress の丸め、`color` と `lightColor` の範囲（-1、2147483648、4294967295、それより外）、`customViewLayout` の空白と null（今の Builder のテストの 3 件の Payload と、サンプルの全通知）

### 7.2 層 2a（PlayMode・Editor、`AndroidNotificationManagerEditorTests`）

- Editor の全メソッド（J-N16）: 操作は呼び出しの後に `{op} could not be started.` が 1 回、問い合わせは `false`、`RequestPermission` は何もしない。不正な JSON でも同じ文言（JSON を読む前に判定）
- 届け先（J-N3）: 結果を積んだ後に Manager を壊すと、何も出ず、新しい Manager も作られない
- 操作のイベント（`OnInteractionCore`）: 3 つの kind が 2 つの定数と action_id に写ること、data の辞書と 0 件の null、Manager が無いときは捨てること
- shown（`OnShownCore`）: tag の空が null
- 設定の完了（`OnSettingsCore`）: `OPENED` / `OPENED_FALLBACK` が成功、`NOT_FOREGROUND` / `SETTINGS_NOT_OPENED` が 4.5 の文言、3 つの target が 3 つの操作名
- `ScheduleWithFallback`（J-N8、R3-2）: 正確な予約で `EXACT_ALARM_NOT_ALLOWED` なら 1 回だけ 1 で呼び直す、不正確な予約は 1 で 1 回だけ呼ぶ、ほかのエラーでは呼び直さない、呼び直しの失敗はその値
- リスナーの登録（`SetNativeForTests` で数える）: 種類ごとに 1 回だけ。片方だけ失敗したとき、次の `EnsureListeners()` で失敗した側だけ登録する（R1-11、X1-8）
- `ReadAndFree`: 読めたときと、読む関数が投げたときの両方で、解放が 1 回だけ
- 届け先（X1-4）: Manager を壊して作り直したとき、壊す前に積まれた結果は新しい Manager に出る

### 7.3 層 2b（実機の Player）

番号と期待値は UI テスト計画 v1 に書く。**IL2CPP の ARM64 だけ。** UI の補助を使うもの（`AndroidNotificationPlayerTests`、カテゴリ `AndroidUiDriver`）と、使わないもの（`AndroidNotificationManagerPlayerTests`）に分ける。

- 使わない: 各操作の成功と結果が 1 回（show / update / cancel / channel / schedule / is_scheduled / cancel_scheduled / progress の 4 つ）、JSON の必須の欠けの文言、範囲の外の値、無いリソース、別のスレッドからの呼び出し、問い合わせ、`AndroidNotificationJsonBuilder` の全 Payload（サンプルの全通知）が表示できること
- 使う: シェードに通知が出て題名と本文が読めること、本文のタップでアプリが前に戻り `NotificationActionTapped`（`ActionBodyTap`）が 1 回、ボタン（launchApp と showsUserInterface=false）、スワイプで dismiss、full screen（`USE_FULL_SCREEN_INTENT` が Android 14 以降で不許可になりうる。画面が点いている間は heads-up になるので、期待はシェードに出ることだけ。8.2）、予約の発火と `NotificationReceived`（正確なアラームを許可した状態で。8.2）、アプリを終了した状態でのタップ（C ABI の版は保持して届ける。1.x は捨てる。**K-3 で決まるまで仮。別の起動の手順**: スクリプトがアプリを止め、補助がシェードで通知を押し、アプリが起動した後に「Manager を最初に作るテスト」だけを流す。購読は `Instance` を最初に取るのと同じフレームで行う。R1-8）、権限のダイアログ（`RequestPermission` の許可と拒否）と権限を取り消した端末の `PERMISSION_DENIED`（どちらも K-3 の別の起動）、設定の画面（開いて戻る、後ろからは失敗）

### 7.4 層 3・手動

- 層 3: 7.3 の画面の照合
- 手動: サンプルアプリの 23 個のボタン（利用者の確認）。Android 12（API 31 / 32。minSdk は 1.x も C ABI の版も 31）の端末（手元に無い。実行時の権限が無く、通知のオフだけが `PERMISSION_DENIED` の経路になる）

## 8. 自動化の前提

### 8.1 検証の層

6 章と 7 章のとおり。起こせない失敗は文言を層 1 で、形をコードレビューで押さえる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- **通知の権限（POST_NOTIFICATIONS）**: Android 13 以降は実行時の許可が要る。テストの初めに補助に付与（`pm grant`。プロセスを止めない）を頼む。**権限を取り消すとアプリのプロセスが止まる**（P2 AP-16、`part2.md:40`）ので、権限の無いテストと `RequestPermission` のダイアログのテストは、別の起動が要る（K-3）。ダイアログは補助で押す
- **正確なアラーム**: Android 14 以降の新しいインストールでは既定で不許可（Android 12・13 では既定で許可）（manifest は `SCHEDULE_EXACT_ALARM`。`USE_EXACT_ALARM` ではない）。`adb shell appops set <pkg> SCHEDULE_EXACT_ALARM allow|deny` で切り替える。**付与はプロセスを止めないので、テストから補助に頼める**。取り消しはプロセスを止めるので、不許可の端末のテスト（E-7 の 2b）は別の起動が要る（K-3）
- **実機の実行は新しいインストールから始まるとは限らない**（`verify_unity_android.sh` はテストの Player を実行の後にしかアンインストールしない。中断した後や、`--as-package-user` の 2 回目の実行では、前の権限と appops が残りうる。R3-1、X3-1）。スクリプトが実機の実行の前（2 回の実行の間を含む）と中断のときにもアンインストールするように直す（5.1）。そのうえで、権限の付与と正確なアラームの許可は、テストの初めに補助に頼む
- 通知のシェードは、ほかのアプリの通知も出る。補助はこのアプリの通知だけを探す（題名にテストの固有の文字列を入れる）。シェードの内容は判定にだけ使い、ログ・成果物に残さない（Share のプライバシーの決まりと同じ）
- 画面が点いてロックが解けていること（補助が点ける）

### 8.3 呼び出し側を止める OS の画面

- 権限のダイアログと設定の画面は、Unity を止める（`PAUSED`）見込み。Share の Sharesheet と同じく、テストは補助への依頼を先に出してから呼ぶ
- 補助に足す命令（補助の設計 v12 で決める）: シェードを開く（`cmd statusbar expand-notifications`）、閉じる（`collapse`）、題名で通知を探す、本文を押す、ボタンを押す、スワイプで消す、権限のダイアログの「許可」「許可しない」を押す、権限を付与する（`pm grant`）、正確なアラームを許可する（`appops`）、設定の画面が出たことを確かめて戻る

### 8.4 アプリ側の設定（利用者の責任）

- manifest の権限・受信機・サービスは AAR が宣言する（1.x と同じ）。カスタムビューの layout と、通知に使うアイコンはアプリが持つ。R8 で消されないよう `tools:keep` が要ることがある（`MAN:1235-1240`）

## 9. 直さない残件

| # | 区分 | 中身 | 理由 |
|---|---|---|---|
| K-1 | 要検証 | 1.x の本文のタップ・アクションで、実際にアプリが開くか（Android 12 以降の trampoline の制限） | 5.5 の手順 3 で実測する |
| K-2 | 要検証 | `pm revoke` / `pm clear-permission-flags` / `appops` で、別の起動の前に状態を作れるか | 補助の設計 v12 で確かめる |
| K-3 | B / 要検証 | **別の起動を要るテストを、Editor 主導の実機のテストで回せるか**: アプリを終了した状態のタップ（保持されたイベント）、`PERMISSION_DENIED`（権限の取り消しはプロセスを止める）、`RequestPermission` の許可と拒否（「まだ聞いていない」状態に戻す）、正確なアラームの不許可（E-7 の 2b）。タップで起動した Player はどのテストの実行にもつながらず、起動の Intent に extra が無いので流すテストを指定できない。`am force-stop` は通知と Alarm も消す見込み（R2-2、R3-1） | 補助の設計 v12 で仕組みを決める（止め方は HOME の後の `am kill` か `run-as kill`、選び方は補助が `run-as` で書く印のファイル、結果は補助のファイルの経路。代わりの案: 新しいインストールの最初の状態を順に使う、プロセスを止めない「通知のオフ」で J-N7 の経路を起こす）。決められなければ、理由を付けて層 3 か手動に回す |
| K-4 | 範囲の外 | 旧ブリッジの AAR（`extra_*`）を外すこと、段 4 の一括改名、マニュアルと移行ガイド（J-N6 の `tools:keep`、J-N19 の 1.x の予約の破棄、5.3 の全行） | 親課題の段 4 |
