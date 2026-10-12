# Android Notification UI 自動テスト計画 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Notification（Android）の層 2b（実機の Player の PlayMode テスト） |
| 設計 | `artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md`（以下「設計」。設計 7.3・8.2・8.3 と 9 章の K-3） |
| UI の補助 | `artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v12.md`（以下「補助」。補助の設計 3.5 の画面の命令と 11 章の別の起動を要るテストの扱い） |
| 前例 | Share の UI テスト計画 `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v6.md`（依頼を先に出す形、後片付け、イベントの数の照合、1.x の列） |
| 端末 | Pixel 6a、Android 16、IL2CPP の ARM64。Android™ Profile（サンプルのシーンあり）と Test Android™ Profile（パッケージの利用者の条件） |
| 1.x の列 | 5.5 の手順 3 で、1.x のまま流して埋める（今のビルドは 2.0.0 の AAR が同居し、`LegacyScheduleCleaner` が動く状態での記録になる。設計 5.5） |

## 0. テストの置き場所と順

| fixture | カテゴリ | 中身 | 順 |
|---|---|---|---|
| `AndroidNotificationFreshInstallTests` | `AndroidUiDriver` | 1 章。新しいインストールの最初の状態を使う。`[Order]` で順を固定する | Notification の fixture の中で最初（名前の順。補助の 11 章） |
| `AndroidNotificationManagerPlayerTests` | なし | 2 章。シェードを使わない | 2 番目 |
| `AndroidNotificationPlayerTests` | `AndroidUiDriver` | 3 章（シェード・設定の画面）と 4 章（サンプル画面） | 3 番目 |

- 補助への文字列の引数は Base64 の UTF-8（`AndroidUiDriver.Base64(...)`）。この表では生の文字列で書く
- 題名は、テストごとに固有の `NTK-AN-<番号>-<GUID の先頭 8 桁>`（以下 `<題名>`。補助の設計 3.5）。サンプル画面のテストは、サンプルの固定の題名
- イベントの数は「期待」の列の終わりに `Op / Tap / Recv`（`NotificationOperationCompleted` / `NotificationActionTapped` / `NotificationReceived`）で書く。後片付けの結果は含めない（5 章）
- 「C ABI だけ」の行は、1.x では流さない（`OnCApi` が偽なら `Assert.Ignore` ではなく、テストを短絡して `Pass` にし、1.x の列は「対象外」）

## 1. 新しいインストールの最初の状態（`AndroidNotificationFreshInstallTests`）

前提: `verify_unity_android.sh` が実機の実行の前にテストの Player をアンインストールしている（補助の設計の 7 章）。最初のテストの前に、通知の権限が無いこと（C ABI の `ntk_notification_has_permission` を直接読む。Manager は作らない。1.x の版は `AndroidJavaClass` で `ContextCompat.checkSelfPermission` を読む）を確かめ、違えば `Assert.Fail("not a fresh install")`。

**保持のテスト（AN-01）と権限のテスト（AN-02〜AN-04）は、1 つのプロセスの中で両立しない**。AN-01 は「権限があり、まだ Manager が無い」状態を、AN-02〜AN-04 は「権限が無いまま Manager を使う」状態を要る。そのため、`verify_unity_android.sh` の 2 回の実機の実行（どちらも新しいインストールから始まる）に分ける:

- **Android™ Profile の実行**（サンプルのシーンがある。`Application.CanStreamedLevelBeLoaded(<サンプルのトップメニュー>)` が真）: AN-02〜AN-06 を流し、AN-01 は短絡して `Pass`
- **Test Android™ Profile の実行**（パッケージの利用者の条件。サンプルのシーンが無い）: AN-01 を最初に流し（`grant` で権限を与えてから、Manager を作らずに）、AN-02〜AN-04 は短絡して `Pass`。AN-05・AN-06 は両方で流す

| # | 順 | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|---|
| AN-01 | 1 | **C ABI だけ。Test Android™ Profile の実行だけ**。`Do("grant", "android.permission.POST_NOTIFICATIONS")` で権限を与え、Notification の Manager がまだ無いこと（継ぎ目でリスナーが未登録）を確かめ、テストが C ABI で直接 `EVENT_ONLY` の通知（`<題名>`、data `k=v`）を出す | 保持されたイベント: 補助の `notification-tap <題名> stay` の後、`Instance` を取って同じフレームで購読すると、`NotificationActionTapped`（`ActionBodyTap`、ID、data `k=v`）が 1 回。0 / 1 / 0 | 対象外 |
| AN-02 | 2 | **Android™ Profile の実行だけ**。`ShowNotification`（`<題名>`、正しい JSON） | `Failed to showNotification: notifications are not allowed for this app (permission or app notification setting).`（設計 J-N7）。1 / 0 / 0 | 流す。成功（通知は出ない）の見込み |
| AN-03 | 3 | `Send("permission-answer", "deny")` → `RequestPermission(cb)` | `cb(false)`。`HasPermission()` が false | 流す。同じ |
| AN-04 | 4 | `Send("permission-answer", "allow")` → `RequestPermission(cb)` | `cb(true)`。`HasPermission()` が true | 流す。同じ |
| AN-05 | 5 | `CanScheduleExactAlarms()` を読み、false なら `ScheduleNotification`（正確、10 分後、`<題名>`） | false: 成功（不正確に下げて呼び直す。設計 J-N8）。`IsNotificationScheduled` が true。true（V-24 が許可だったとき）: この行は値を記録して `Pass`。1 / 0 / 0 | 流す。同じ（1.x の Kotlin が下げた） |
| AN-06 | 6 | `Do("grant", "android.permission.POST_NOTIFICATIONS")`、`Do("appop-allow", "SCHEDULE_EXACT_ALARM")` | どちらも `ok`。`HasPermission()` と `CanScheduleExactAlarms()` が true。後の fixture のための準備 | 流す |

## 2. シェードを使わないテスト（`AndroidNotificationManagerPlayerTests`）

前提: 1 章の後なので、権限と正確なアラームは許可されている。テストの前に `HasPermission()` を確かめ、false なら `Assert.Fail`。

| # | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|
| AN-10 | `ShowNotification`（`AndroidNotificationJsonBuilder` で組んだ最小の通知） | 成功が 1 回。呼び出しの中では 0 回。UnityMain。1 / 0 / 0 | 流す。同じ |
| AN-11 | `UpdateNotification`（同じ ID） | 成功。1 / 0 / 0 | 流す。同じ |
| AN-12 | `CancelNotification(id, tag)`、`CancelAllNotifications()` | どちらも成功。2 / 0 / 0 | 流す。同じ |
| AN-13 | `CreateChannel`（全キー）→ `DeleteChannel(id)` | どちらも成功。2 / 0 / 0 | 流す。同じ |
| AN-14 | `ScheduleNotification`（不正確、10 分後）→ `IsNotificationScheduled` → `CancelScheduledNotification` → `IsNotificationScheduled` | 成功、true、成功、false。2 / 0 / 0 | 流す。同じ |
| AN-15 | `CancelAllScheduledNotifications()` | 成功。1 / 0 / 0 | 流す。同じ |
| AN-16 | Progress: start（15%）→ update（50%）→ complete → stop | 4 つとも成功。4 / 0 / 0 | 流す。同じ |
| AN-17 | JSON の必須の欠け（`title` が空白） | `Invalid argument for showNotification: title is required.`。1 / 0 / 0 | 流す。同じ |
| AN-18 | JSON の構文の誤り（`{"id":`） | `Failed to showNotification: the JSON could not be read.` | 流す。`Failed to showNotification: <org.json の文言>`（設計 5.3） |
| AN-19 | 範囲の外の値（`groupAlertBehavior` 3） | `Invalid argument for showNotification: Please verify the input parameters.` | 流す。成功の見込み（設計 5.3） |
| AN-20 | 無いアイコンの名前（`smallIcon.name` = `ntk_no_such_icon`） | `Failed to showNotification: a resource named in the notification was not found.` | 流す。成功（アプリのアイコンに代わる。設計 5.3） |
| AN-21 | Progress の `progress` が無い | `Invalid argument for startProgressForegroundService: progress is required for Progress FGS operations.` | 流す。同じ |
| AN-22 | 別のスレッドから `ShowNotification` | 例外が出ず、成功が UnityMain で 1 回 | 流す。失敗の見込み（`currentActivity`。設計 5.3） |
| AN-23 | `DeleteChannel(null)` | 成功（`""` を消す） | 流す。値を記録する（設計 5.3 の推測） |
| AN-24 | `HasPermission()`、`AreNotificationsEnabled()`、`CanScheduleExactAlarms()`、`IsNotificationScheduled(id)` | true、true、true、false | 流す。同じ |
| AN-25 | `AndroidNotificationJsonBuilderTests` の 3 つの Payload と、サンプルの全通知の Payload を `ShowNotification` / `ScheduleNotification` | どれも成功（往復の層 1 のテストの実機の版）。件数 / 0 / 0 | 流す。同じ |

## 3. シェードと画面を使うテスト（`AndroidNotificationPlayerTests`）

**画面の操作の約束**（補助の設計 3.5）: テストは補助への依頼を先に `Send` し、同じフレームで Manager を呼び、`Wait`（180 秒）する。

| # | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|
| AN-30 | `ShowNotification(<題名>)` → `Do("notification-present", <題名>)` | 成功。答えが `ok`（シェードに題名が出る）。1 / 0 / 0 | 流す。同じ |
| AN-31 | `ShowNotification(<題名>、launchAppOnTap=true、data k=v)` → `Send("notification-tap", <題名>, "front")` → `Wait` | `NotificationActionTapped`（`ActionBodyTap`、ID、data `k=v`）が 1 回、UnityMain。Player が前面。1 / 1 / 0 | 流す。アプリが前面に来るか（設計 K-1）とイベントを記録する |
| AN-32 | 同上、`launchAppOnTap=false` → `notification-tap <題名> stay` | `ActionBodyTap` が 1 回。1 / 1 / 0 | 流す。同じ |
| AN-33 | アクション 2 つ（`Play`: launchApp、`Later`: showsUserInterface=false）→ `notification-action <題名> Later stay` | `NotificationActionTapped`（ActionId `later`）が 1 回。1 / 1 / 0 | 流す。同じ |
| AN-34 | 同上 → `notification-action <題名> Play front` | ActionId `play` が 1 回。Player が前面。1 / 1 / 0 | 流す。アプリが前面に来るかを記録する |
| AN-35 | `ShowNotification(<題名>)` → `notification-dismiss <題名>` | `ActionNotificationDismissed` が 1 回。1 / 1 / 0 | 流す。同じ |
| AN-36 | `ShowNotification(<題名>、fullScreenIntent=true、priority 2、category call)` → `notification-present <題名>` | 成功。シェードに出る（画面が点いているので heads-up。設計 7.3）。1 / 0 / 0 | 流す。同じ |
| AN-37 | `ScheduleNotification(<題名>、正確、5 秒後)` → 10 秒待つ → `notification-present <題名>` | 成功。`NotificationReceived`（ID、tag、channel）が 1 回。1 / 0 / 1 | 流す。同じ |
| AN-38 | `ShowNotification(<題名>、tag t)` → `CancelNotification(id, "t")` → `notification-absent <題名> 3000` | 成功が 2 回。答えが `ok`。2 / 0 / 0 | 流す。同じ |
| AN-39 | data に `"`、`/`、日本語を含む値 → `notification-tap <題名> stay` | `Data` の値が元の値と同じ | 流す。値を記録する（設計 5.3 のエスケープ） |
| AN-40 | `Send("close-screen", "com.android.settings")` → `OpenNotificationSettings()` → `Wait` | 成功が 1 回。答えが `ok`。1 / 0 / 0。結果が画面を離れる前か後かは判定しない（設計 5.3） | 流す。同じ。どの画面が開いたかを記録する（設計 5.3 の要確認） |
| AN-41 | 同上 → `OpenAppDetailsSettings()` | 成功。1 / 0 / 0 | 流す。同じ |
| AN-42 | 同上 → `OpenExactAlarmSettings()` | 成功。1 / 0 / 0 | 流す。同じ |
| AN-43 | `Do("background", "1000")` の間に `OpenNotificationSettings()` を呼ぶ（Dialog の AD-27 と同じ形） | `Failed to openNotificationSettings: the app is not in the foreground.`。1 / 0 / 0 | 流す。成功の見込み（設計 5.3） |
| AN-44 | Progress を後ろから: `background` の間に `StartProgressForegroundService` | `Failed to startProgressForegroundService: the system did not allow the foreground service from the background.`（起きなければ値を記録して `Pass`） | 流す。値を記録する |

## 4. サンプル画面を通すテスト（`AndroidNotificationPlayerTests`）

サンプルのトップメニューを読み込み、Notification の画面を開いてボタンを押す（Share の 1 章と同じ形）。結果は `ResultTextBlock` の最後の値（`<操作名>: Success\n<説明>`）で照らす。Test Android™ Profile では `Assert.Ignore`（Share と同じ）。

| # | ボタン | 期待 | 1.x |
|---|---|---|---|
| AN-50 | ShowNotification → UpdateNotification → CancelNotification | 3 つとも `Success`。`ShowNotification` の後、`notification-present Energy Refilled` が `ok` | 流す。同じ |
| AN-51 | ScheduleNotification → IsScheduledNotification → CancelScheduled | `Success`、`IsScheduled: True` 相当の表示、`Success` | 流す。同じ |
| AN-52 | Start / Update / Complete / Stop Progress | 4 つとも `Success` | 流す。同じ |
| AN-53 | ShowActionNotification → `notification-action <サンプルの題名> Dismiss stay` | `Success`、続いて `Notification Action Button` の表示（ActionId） | 流す。同じ |
| AN-54 | ShowFullScreenNotification、ShowDecoratedCustomView | どちらも `Success`（custom view の layout はこのプロジェクトの androidlib にある） | 流す。同じ |
| AN-55 | HasPermission、NotificationsEnabled、CanScheduleExactAlarms | `HasPermission: True` など | 流す。同じ |

## 5. テストの共通の約束

- **後片付け**: 各テストの後、`WaitForPending`（未回収の依頼）、`CancelAllNotifications()`、`CancelAllScheduledNotifications()`、Progress の stop を呼び、その結果は数えない。シェードが開いたままなら補助が閉じている（補助の設計 3.5）
- **イベントの数の照合**: 表の数と、後片付けの前に数えた `Op / Tap / Recv` が同じ。イベントはすべて UnityMain で出る
- **保持のテスト（AN-01）**は、プロセスの中で最初に Notification の Manager を作るテストでなければ成り立たない。前提が崩れていれば `Assert.Fail`（Ignore にしない）
- 予約の発火を待つテスト（AN-37）は、正確なアラームが許可されている前提（1 章の AN-06）
- 補助がいない実行では、`AndroidUiDriver` の fixture は `Assert.Ignore` になり、ゲートで落ちる（補助の設計の 7 章）

## 6. 確かめないもの

| もの | 理由 |
|---|---|
| プロセスが死んだ状態からのタップでアプリが起動し、保持されたイベントが届くこと | Editor 主導の実行の外になる（補助の設計の 11 章）。手動 |
| Android 12（API 31 / 32）の端末 | 手元に無い（設計 7.4） |
| 1.x が出した通知が 2.0.0 にした後で反応しないこと | 1.x と 2.0.0 を入れ替える手順が要る。マニュアルに書く（設計 5.3） |
| リソースの縮小（R8）で通知のアイコンが消えること | 出荷のビルドの設定に左右される。マニュアルの `tools:keep`（設計 J-N6） |

## 7. 実行結果

5.5 の手順 3（1.x）と手順 7（C ABI の版）で埋める。
