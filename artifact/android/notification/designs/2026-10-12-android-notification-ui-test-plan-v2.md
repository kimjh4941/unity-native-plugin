# Android Notification UI 自動テスト計画 v2

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Notification（Android）の層 2b（実機の Player の PlayMode テスト） |
| 設計 | `artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md`（以下「設計」。設計 7.3・8.2・8.3 と 9 章の K-3） |
| UI の補助 | `artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v13.md`（以下「補助」。補助の設計 3.5 の画面の命令、7 章の実機の実行、11 章の別の起動を要るテストの扱い） |
| 前例 | Share の UI テスト計画 `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v6.md`（依頼を先に出す形、後片付けの 7 段、イベントの数の照合、1.x の列） |
| レビュー | `artifact/android/notification/reviews/2026-10-12-android-notification-ui-test-plan-review-v1.md`（補助の v12 と合わせて見た） |
| 端末 | Pixel 6a、Android 16、IL2CPP の ARM64 |
| 1.x の列 | 設計 5.5 の手順 3 で、1.x のまま流して埋める（今のビルドは 2.0.0 の AAR が同居し、`LegacyScheduleCleaner` が動く状態での記録になる）。「C ABI だけ」の行は 1.x では流さない |

## v1 からの主な変更（レビュー v1 を受けて。2026-10-12）

- **保持のテスト（AN-01）を、既定の検証に足す専用の実機の実行に移した**（補助の設計の 7 章。R1-6、X1-5、X1-6）。押した後、通知が消えたことと 3 秒を待ってから Manager を作る（R1-7）。fixture の `SetUp` で Manager に触れない（R1-4）。C ABI の版の実装のときに足す（R1-5）
- **権限のテストは 2 つの Profile の両方で流す**（AN-01 が抜けたので、Profile で分ける必要が無くなった。R1-21）
- **fixture の順は `[Order]` で決める**（X1-7）
- **「無い」の確かめは、アプリの `getActiveNotifications()` で行う**（R1-1、X1-13）。AN-20 は「何も表示しない」も確かめる。AN-25 は表示を確かめる（X1-14）
- **`front` のテストは、押す前に Player を後ろに回す**（補助の `front`。R1-8、X1-10）。1.x は `ok not-front` を記録できる
- **設定の画面は、開いた Activity の名前を照らす**（X1-11、R1-16）
- **AN-44（Progress を後ろから）を外した**（X1-12。起こせるか分からず、失敗の経路でプロセスが落ちうる。設計の層 1 の文言のテストで押さえる）
- イベントの数をすべての行に書いた（X1-9）。後片付けを Share の 7 段にした（R1-10、X1-8）。J-N7 の残りの行（AN-02b、AN-02c）。予約は 10 分後に（R1-14）。1.x の見込みの直し（R1-15）

## 0. テストの置き場所と順

| fixture | カテゴリ | 中身 | 順 |
|---|---|---|---|
| `AndroidNotificationFreshInstallTests` | `AndroidUiDriver` | 1 章。新しいインストールの最初の状態を使う。中のテストも `[Order]` で順を固定する | **クラスに `[Order(1)]`**（同じ名前空間のほかの fixture より先。補助の設計の 11 章、V-26） |
| `AndroidNotificationRetentionTests` | `AndroidNotificationRetention`、`AndroidUiDriver` | 2 章。保持のテストだけの実機の実行（補助の設計の 7 章）でだけ流す。ほかの実行はこのカテゴリを外す | 専用の実行 |
| `AndroidNotificationManagerPlayerTests` | `AndroidUiDriver` | 3 章。シェードを使わない（権限は補助で与えるので、カテゴリを付ける） | 順を問わない |
| `AndroidNotificationPlayerTests` | `AndroidUiDriver` | 4 章（シェード・画面）と 5 章（サンプル画面） | 順を問わない |

- 1 章のほかの fixture は、`OneTimeSetUp` で `grant android.permission.POST_NOTIFICATIONS` と `appop-allow SCHEDULE_EXACT_ALARM` を補助に頼む（何度呼んでもよい。1 章が先に流れなかったときにも成り立つ）
- 補助への文字列の引数は Base64 の UTF-8（`AndroidUiDriver.Base64(...)`）。表では生の文字列で書く
- 題名は、テストごとに固有の `NTK-AN-<番号>-<GUID の先頭 8 桁>`（以下 `<題名>`）。サンプル画面のテストは、サンプルの固定の題名
- **イベントの数**は「期待」の列の終わりに `Op / Tap / Recv`（`NotificationOperationCompleted` / `NotificationActionTapped` / `NotificationReceived`）で書く。同期の問い合わせは `0 / 0 / 0`。後片付けの結果は含めない（6 章）
- **「表示されている」「表示されていない」**は、テストの側でアプリの `NotificationManager.getActiveNotifications()` を読み、ID（と tag）で判定する（`AndroidJavaObject`。テストの側だけ。以下「アクティブ」）。シェードは、題名が OS の画面に出ることを見るときだけ使う（`notification-present`）

## 1. 新しいインストールの最初の状態（`AndroidNotificationFreshInstallTests`）

前提: `verify_unity_android.sh` が実機の実行の前にテストの Player をアンインストールしている（補助の設計の 7 章）。最初のテストの前に、`HasPermission()` が false であることを確かめ、違えば `Assert.Fail("not a fresh install")`（Android 13 以降。V-27）。

| # | 順 | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|---|
| AN-02 | 1 | `ShowNotification`（`<題名>`、正しい JSON） | `Failed to showNotification: notifications are not allowed for this app (permission or app notification setting).`（設計 J-N7）。アクティブに無い。1 / 0 / 0 | 流す。成功（通知は出ない）の見込み |
| AN-02b | 2 | `UpdateNotification`（同じ ID） | `Failed to updateNotification: notifications are not allowed ...`。1 / 0 / 0 | 流す。成功の見込み |
| AN-02c | 3 | `ScheduleNotification`（過ぎた時刻、`<題名>`） | `Failed to scheduleNotification: notifications are not allowed ...`。1 / 0 / 0 | 流す。成功（すぐ出そうとして黙って出ない）の見込み |
| AN-03 | 4 | `Send("permission-answer", "deny")` → `RequestPermission(cb)` → `Wait` | `cb(false)`。`HasPermission()` が false。0 / 0 / 0 | 流す。同じ |
| AN-04 | 5 | `Send("permission-answer", "allow")` → `RequestPermission(cb)` → `Wait` | `cb(true)`。`HasPermission()` が true。0 / 0 / 0 | 流す。同じ |
| AN-05 | 6 | `CanScheduleExactAlarms()` を読み、false なら `ScheduleNotification`（正確、10 分後、`<題名>`） | false: 成功（不正確に下げて呼び直す。設計 J-N8）。`IsNotificationScheduled` が true。1 / 0 / 0。true（V-24 が許可だったとき）: 値を記録し、この行は流さない（設計の層 2a の `ScheduleWithFallback` で押さえる）。0 / 0 / 0 | 流す。同じ（1.x の Kotlin が下げた） |
| AN-06 | 7 | `Do("grant", "android.permission.POST_NOTIFICATIONS")`、`Do("appop-allow", "SCHEDULE_EXACT_ALARM")` | どちらも `ok`。`HasPermission()` と `CanScheduleExactAlarms()` が true。0 / 0 / 0 | 流す |

## 2. 保持のテスト（`AndroidNotificationRetentionTests`。C ABI だけ。保持のテストだけの実機の実行）

- `SetUp` と、このテストの終わりまで、Notification の Manager に触れない（`Instance` を読まない。R1-4）
- C ABI の版の実装（設計 5.5 の手順 5 以降）のときにファイルごと足す（1.x の段では `AndroidNotificationCApi` が無い。R1-5）。1.x の列は「対象外」

| # | 操作 | 期待 |
|---|---|---|
| AN-01 | `Do("grant", "android.permission.POST_NOTIFICATIONS")`。Manager の継ぎ目でリスナーが未登録であることを確かめる。テストが C ABI で直接、`EVENT_ONLY` の通知（`<題名>`、data `k=v`、autoCancel）を出す。`Send("notification-tap", <題名>, "stay")` → `Wait`。アクティブからその ID が消えるのを待ち（10 秒）、さらに 3 秒待つ。`Instance` を取り、同じフレームで購読する | `NotificationActionTapped`（`ActionBodyTap`、ID、data `k=v`）が 1 回、UnityMain。0 / 1 / 0 |

## 3. シェードを使わないテスト（`AndroidNotificationManagerPlayerTests`）

| # | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|
| AN-10 | `ShowNotification`（`AndroidNotificationJsonBuilder` で組んだ最小の通知） | 成功が 1 回。呼び出しの中では 0 回。UnityMain。アクティブに在る。1 / 0 / 0 | 流す。同じ |
| AN-11 | `UpdateNotification`（同じ ID） | 成功。1 / 0 / 0 | 流す。同じ |
| AN-12 | `CancelNotification(id, tag)` → `CancelAllNotifications()` | どちらも成功。アクティブに無い。2 / 0 / 0 | 流す。同じ |
| AN-13 | `CreateChannel`（全キー）→ `DeleteChannel(id)` | どちらも成功。2 / 0 / 0 | 流す。同じ |
| AN-14 | `ScheduleNotification`（不正確、10 分後）→ `IsNotificationScheduled` → `CancelScheduledNotification` → `IsNotificationScheduled` | 成功、true、成功、false。2 / 0 / 0 | 流す。同じ |
| AN-15 | `CancelAllScheduledNotifications()` | 成功。1 / 0 / 0 | 流す。同じ |
| AN-16 | Progress: start（15%）→ update（50%）→ complete → stop | 4 つとも成功。4 / 0 / 0 | 流す。同じ |
| AN-17 | JSON の必須の欠け（`title` が空白） | `Invalid argument for showNotification: title is required.`。1 / 0 / 0 | 流す。同じ |
| AN-18 | JSON の構文の誤り（`{"id":`） | `Failed to showNotification: the JSON could not be read.`。1 / 0 / 0 | 流す。`Failed to showNotification: <org.json の文言>`（設計 5.3） |
| AN-19 | 範囲の外の値（`groupAlertBehavior` 3） | `Invalid argument for showNotification: Please verify the input parameters.`。アクティブに無い。1 / 0 / 0 | 流す。成功の見込み（設計 5.3） |
| AN-20 | 無いアイコンの名前（`smallIcon.name` = `ntk_no_such_icon`、`<題名>`） | `Failed to showNotification: a resource named in the notification was not found.`。**アクティブに無い**（何も表示しない。X1-13）。1 / 0 / 0 | 流す。成功（アプリのアイコンに代わる）。アクティブに在る |
| AN-21 | Progress の `progress` が無い | `Invalid argument for startProgressForegroundService: progress is required for Progress FGS operations.`。1 / 0 / 0 | 流す。同じ |
| AN-22 | 別のスレッドから `ShowNotification` | 例外が出ず、成功が UnityMain で 1 回。1 / 0 / 0 | 流す。`showNotification could not be started.`（`currentActivity`。Share の AS-35 と同じ経路）の見込み |
| AN-23 | `DeleteChannel(null)` | 成功（`""` を消す）。1 / 0 / 0 | 流す。値を記録する（設計 5.3 の推測） |
| AN-24 | `HasPermission()`、`AreNotificationsEnabled()`、`CanScheduleExactAlarms()`、`IsNotificationScheduled(id)` | true、true、true、false。0 / 0 / 0 | 流す。同じ |
| AN-25 | `AndroidNotificationJsonBuilderTests` の 3 つの Payload と、サンプルの全通知の Payload を、1 つずつ出して消す（予約は 10 分後に置き換える。R1-14） | 即時のものは、成功の後にアクティブに在り、消した後に無い。予約は成功と `IsNotificationScheduled` が true。各 1 / 0 / 0（消す結果は後片付けの扱いで数えない） | 流す。同じ |

## 4. シェードと画面を使うテスト（`AndroidNotificationPlayerTests`）

**画面の操作の約束**（補助の設計 3.5）: テストは補助への依頼を先に `Send` し、同じフレームで Manager を呼び、`Wait`（180 秒）する。通知は 1 度に 1 つだけ出し、テストの終わりに消す（V-28）。

| # | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|
| AN-30 | `ShowNotification(<題名>)` → `Do("notification-present", <題名>)` | 成功。答えが `ok`（シェードに題名が出る）。1 / 0 / 0 | 流す。同じ |
| AN-31 | `ShowNotification(<題名>、launchAppOnTap=true、data k=v)` → `Send("notification-tap", <題名>, "front")` → `Wait` | 答えが `ok front`（補助が押す前に Player を後ろに回し、押した後に前面に戻った）。`NotificationActionTapped`（`ActionBodyTap`、ID、data `k=v`）が 1 回、UnityMain。1 / 1 / 0 | 流す。答え（`ok front` か `ok not-front`。設計 K-1）とイベントを記録する |
| AN-32 | 同上、`launchAppOnTap=false` → `notification-tap <題名> stay` | `ActionBodyTap` が 1 回。1 / 1 / 0 | 流す。同じ |
| AN-33 | アクション 2 つ（`Play`: launchApp、`Later`: showsUserInterface=false）→ `notification-action <題名> Later stay` | `NotificationActionTapped`（ActionId `later`）が 1 回。1 / 1 / 0 | 流す。同じ |
| AN-34 | 同上 → `notification-action <題名> Play front` | 答えが `ok front`。ActionId `play` が 1 回。1 / 1 / 0 | 流す。答えを記録する |
| AN-35 | `ShowNotification(<題名>)` → `notification-dismiss <題名>` | `ActionNotificationDismissed` が 1 回。アクティブに無い。1 / 1 / 0 | 流す。同じ |
| AN-36 | `ShowNotification(<題名>、fullScreenIntent=true、priority 2、category call)` → `notification-present <題名>` | 成功。シェードに出る（画面が点いているので heads-up。設計 7.3）。1 / 0 / 0 | 流す。同じ |
| AN-37 | `ScheduleNotification(<題名>、正確、5 秒後)` → 10 秒待つ → `notification-present <題名>` | 成功。`NotificationReceived`（ID、tag、channel）が 1 回。1 / 0 / 1 | 流す。同じ |
| AN-38 | `ShowNotification(<題名>、tag t)` → `CancelNotification(id, "t")` | 成功が 2 回。アクティブに無い。2 / 0 / 0 | 流す。同じ |
| AN-39 | data に `"`、`/`、日本語を含む値 → `notification-tap <題名> stay` | `Data` の値が元の値と同じ。1 / 1 / 0 | 流す。値を記録する（設計 5.3 のエスケープ） |
| AN-40 | `Send("close-screen", "com.android.settings")` → `OpenNotificationSettings()` → `Wait` | 成功が 1 回。答えの Activity の名前が、アプリの通知の設定の画面（V-25 と同時に名前を確かめて固定する）。1 / 0 / 0。結果が画面を離れる前か後かは判定しない（設計 5.3） | 流す。答えの Activity の名前を記録する（設計 5.3 の要確認: アプリの詳細の画面に落ちていた見込み） |
| AN-41 | 同上 → `OpenAppDetailsSettings()` | 成功。答えの名前がアプリの詳細の画面で、AN-40 の名前と違う。1 / 0 / 0 | 流す。名前を記録する |
| AN-42 | 同上 → `OpenExactAlarmSettings()` | 成功。答えの名前が正確なアラームの画面（AN-40・AN-41 と違う）。1 / 0 / 0 | 流す。名前を記録する |
| AN-43 | `Do("background", "1000")` の間に、別のスレッドから `OpenNotificationSettings()` を呼ぶ（Dialog の AD-27 と同じ形） | `Failed to openNotificationSettings: the app is not in the foreground.`。1 / 0 / 0 | 流す。`openNotificationSettings could not be started.`（別のスレッド。Share の AS-35）の見込み |

## 5. サンプル画面を通すテスト（`AndroidNotificationPlayerTests`）

サンプルのトップメニューを読み込み、Notification の画面を開いてボタンを押す（Share の 1 章と同じ形）。結果は `ResultTextBlock` の最後の値（`<操作名>: Success\n<説明>`）で照らす。Test Android™ Profile では `Assert.Ignore`（Share と同じ。ゲートの skip の判定は Android™ Profile の実行だけ）。

| # | ボタン | 期待 | 1.x |
|---|---|---|---|
| AN-50 | ShowNotification → `notification-present Energy Refilled` → UpdateNotification → CancelNotification | 3 つとも `Success`。答えが `ok`。3 / 0 / 0 | 流す。同じ |
| AN-51 | ScheduleNotification → IsScheduledNotification → CancelScheduled | `Success`、予約ありの表示、`Success`（サンプルの予約は 15 秒後なので、ScheduleNotification から 10 秒以内に取り消す）。2 / 0 / 0 | 流す。同じ |
| AN-52 | Start / Update / Complete / Stop Progress | 4 つとも `Success`。4 / 0 / 0 | 流す。同じ |
| AN-53 | ShowActionNotification → `notification-action <サンプルの題名> Dismiss stay` | `Success`、続いて `Notification Action Button` の表示（ActionId）。1 / 1 / 0 | 流す。同じ |
| AN-54 | ShowFullScreenNotification → CancelAll → ShowDecoratedCustomView → CancelAll | 2 つとも `Success`（custom view の layout はこのプロジェクトの androidlib にある）。4 / 0 / 0 | 流す。同じ |
| AN-55 | HasPermission、NotificationsEnabled、CanScheduleExactAlarms | `HasPermission: True` など。0 / 0 / 0 | 流す。同じ |

## 6. テストの共通の約束

- **後片付け**（Share の計画 v6 の 3 章の 7 段と同じ形。R1-10、X1-8）:
  1. `WaitForPending`（未回収の依頼をすべて回収）
  2. 照合の起点を決める: 最後の補助の答えを受けた時刻（シェードは Unity を止めない見込み〈V-18〉なので、`OnApplicationPause(false)` は起点にしない）
  3. 表のイベントの数がそろうのを待つ（5 秒）
  4. 起点から 0.5 秒、余分なイベントが来ないことを確かめる（`stay` の後の二重の通知など）
  5. 数とスレッドを照らす（表の数と同じ、すべて UnityMain）
  6. `CancelAllNotifications()`、`CancelAllScheduledNotifications()`、Progress の stop を呼び、その結果（3 回）を待つ（10 秒）
  7. 購読を外し、取り出し口の列が空になるまで 2 フレーム回してから、数を 0 に戻す
- **保持のテスト（AN-01）**は、プロセスの中で最初に Notification の Manager を作るテストでなければ成り立たない。前提が崩れていれば `Assert.Fail`（Ignore にしない）
- 予約の発火を待つテスト（AN-37）は、正確なアラームが許可されている前提（1 章の AN-06、ほかの fixture の `OneTimeSetUp`）
- 補助がいない実行では、`AndroidUiDriver` の fixture は `Assert.Ignore` になり、ゲートで落ちる（補助の設計の 7 章）

## 7. 確かめないもの

| もの | 理由 |
|---|---|
| プロセスが死んだ状態からのタップでアプリが起動し、保持されたイベントが届くこと | Editor 主導の実行の外になる（補助の設計の 11 章）。手動 |
| アプリの通知がオフのときの `PERMISSION_DENIED`（設計 J-N7 の後半） | Android 13 以降、通知をオフにすると通知の権限が取り消され、プロセスが止まる。権限が無いときの経路（AN-02）と同じ値・同じ文言なので、層 1 の文言のテストで押さえる |
| Progress を後ろから呼んだときの `SERVICE_START_NOT_ALLOWED` | 安定して起こせない。失敗の経路（`startForegroundService` の後 `startForeground` の前に `stopSelf`）でプロセスが落ちうる。層 1 の文言のテストで押さえる |
| Android 12（API 31 / 32）の端末 | 手元に無い（設計 7.4） |
| 1.x が出した通知が 2.0.0 にした後で反応しないこと | 1.x と 2.0.0 を入れ替える手順が要る。マニュアルに書く（設計 5.3） |
| リソースの縮小（R8）で通知のアイコンが消えること | 出荷のビルドの設定に左右される。マニュアルの `tools:keep`（設計 J-N6） |

## 8. 実行結果

設計 5.5 の手順 3（1.x）と手順 7（C ABI の版）で埋める。
