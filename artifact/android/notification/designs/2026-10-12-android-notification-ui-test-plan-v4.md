# Android Notification UI 自動テスト計画 v4

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Notification（Android）の層 2b（実機の Player の PlayMode テスト） |
| 設計 | `artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md`（以下「設計」。設計 7.3・8.2・8.3 と 9 章の K-3） |
| UI の補助 | `artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v15.md`（以下「補助」。補助の設計 3.5 の画面の命令、7 章の実機の実行、9 章の要検証、11 章の別の起動を要るテストの扱い） |
| 前例 | Share の UI テスト計画 `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v6.md` と `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidSharePlayerTests.cs`（依頼を先に出す形、後片付けの段、イベントの数の照合、1.x の列） |
| レビュー | `artifact/android/notification/reviews/2026-10-12-android-notification-ui-test-plan-review-v1.md`、`...-review-v2.md`、`...-review-v3.md`（どれも補助の設計と合わせて見た。v3 で A1 が 0 になり、レビューを止めた。v3 は Codex が利用上限で使えず、サブエージェントだけで判定した。この版は v3 の残りの指摘を直したもので、レビューを通していない） |
| 端末 | Pixel 6a、Android 16、IL2CPP の ARM64 |
| 1.x の列 | 設計 5.5 の手順 3 で、1.x のまま流して埋める（今のビルドは 2.0.0 の AAR が同居し、`LegacyScheduleCleaner` が動く状態での記録になる）。「C ABI だけ」の行は 1.x では流さない。保持のテストだけの実機の実行も、手順 3 ではスクリプトに無い（2 章）。手順 3 で 1 章を足す変更と同じ変更で、スクリプトに 3 つ（実機の実行の前のアンインストール、Test Android™ Profile の判定、補助への `--package`）を足す（補助の設計 7 章。R3-9） |

## v3 からの主な変更（レビュー v3 を受けて。2026-10-12）

- **AN-40・AN-42 は、C ABI の版では Manager の記録で `OPENED`（`OPENED_FALLBACK` でない）を照らす**（R3-1）。答えの組は前面の記録から読むので、Settings が SPA に転送すると、アプリの詳細の画面に落ちたことを見分けられないことがある
- **画面の命令を使うテストに `[Timeout]` を付ける**（0 章、4 章。R3-3）。Test Framework は `[UnityTest]` を既定で 180 秒で打ち切る
- アンカーは対象と違う ID と固有の題名（R3-5）。AN-05 は自分で予約を消す（R3-4）。AN-25 の Payload の出どころ（R3-7）。補助がいないときに Ignore になるのは補助を使うテストだけ（R3-8）。スクリプトを変える時期（R3-9）

## v2 からの主な変更（レビュー v2 を受けて。2026-10-12）

- **Notification の Player のテストを 1 つの fixture にまとめ、権限のテストだけメソッドに `[Order]` を付ける**（0 章。X1-7）。v2 の「fixture のクラスに `[Order(1)]`」は、同梱の NUnit の `OrderAttribute` がメソッドにしか付けられない（`AttributeTargets` が 64）ので、コンパイルが通らない
- **アクティブの確かめを 3 つに分けた**（0 章。R2-1。A1）: 「在る」と「消える」は期限まで読み直す。「出ない」は、後から出したアンカーの通知が現れてから見る
- **後片付けを Share のテストの段に合わせ、照合の起点を「期待の数がそろった時刻」「最後の補助の答え」「`OnApplicationPause(false)`」の遅いものにした**（6 章。X2-1。A1。R2-4）
- 「両方の Profile で流す」を直した（6 章。X2-2、R2-14）。必須のゲートは Android™ Profile の実行。Test Android™ Profile の実行（`--as-package-user` のときだけ）でも、サンプル画面のテストを除いて skip を見る（補助の設計 7 章）
- AN-12 は先に出してから消す（R2-11）。AN-25 の数を式で書いた（R2-10）。AN-32、AN-33 は `front` で押して `ok not-front` を期待する（R2-12）。AN-01 の「消えた」の意味（R2-9）。AN-40〜AN-42 は Activity の名前と action の組で照らす（R2-3、X1-11）。ボタンの文言を固有にした（補助の `ambiguous`）。画面の命令の待ちは 200 秒（補助の設計 3.5）。保持の実行をスクリプトに足す時期（R2-2）

## v1 からの主な変更（レビュー v1 を受けて。2026-10-12）

- **保持のテスト（AN-01）を、既定の検証に足す専用の実機の実行に移した**（補助の設計の 7 章。R1-6、X1-5、X1-6）。押した後、通知が消えたことと 3 秒を待ってから Manager を作る（R1-7）。fixture の `SetUp` で Manager に触れない（R1-4）。C ABI の版の実装のときに足す（R1-5）
- **権限のテストは 2 つの Profile の両方で流す**（AN-01 が抜けたので、Profile で分ける必要が無くなった。R1-21。v3 で直した）
- **fixture の順は `[Order]` で決める**（X1-7。v3 で、メソッドの `[Order]` に直した）
- **「無い」の確かめは、アプリの `getActiveNotifications()` で行う**（R1-1、X1-13）。AN-20 は「何も表示しない」も確かめる。AN-25 は表示を確かめる（X1-14）
- **`front` のテストは、押す前に Player を後ろに回す**（補助の `front`。R1-8、X1-10）。1.x は `ok not-front` を記録できる
- **設定の画面は、開いた Activity の名前を照らす**（X1-11、R1-16）
- **AN-44（Progress を後ろから）を外した**（X1-12。起こせるか分からず、失敗の経路でプロセスが落ちうる。設計の層 1 の文言のテストで押さえる）
- イベントの数をすべての行に書いた（X1-9）。後片付けを Share の 7 段にした（R1-10、X1-8）。J-N7 の残りの行（AN-02b、AN-02c）。予約は 10 分後に（R1-14）。1.x の見込みの直し（R1-15）

## 0. テストの置き場所と順

| fixture | カテゴリ | 中身 | 順 |
|---|---|---|---|
| `AndroidNotificationPlayerTests` | `AndroidUiDriver`（5 章のテストには、メソッドに `AndroidSampleScreen` も） | 1 章、3 章、4 章、5 章 | **1 章のテストだけ、メソッドに `[Order(1)]`〜`[Order(7)]`**。ほかのテストには付けない（Test Framework は `Order` を持つテストを、持たないテストより先に、番号の順に流す。補助の設計 11 章、V-26） |
| `AndroidNotificationRetentionTests` | `AndroidNotificationRetention`、`AndroidUiDriver` | 2 章。保持のテストだけの実機の実行（補助の設計の 7 章）でだけ流す。ほかの実行はこのカテゴリを外す | 専用の実行 |

- Notification の Player のテストのために、ほかの fixture を作らない。fixture どうしの順は決められない（同梱の NUnit の `OrderAttribute` はメソッドにしか付けられない。補助の設計 11 章）。権限を与える側のテストが別の fixture で先に流れると、1 章の新しいインストールの状態が壊れる
- 3〜5 章のテストは、どれも最初に `EnsureGranted()`（`Do("grant", "android.permission.POST_NOTIFICATIONS")` と `Do("appop-allow", "SCHEDULE_EXACT_ALARM")`。何度呼んでもよい）を呼ぶ。1 本だけを流したときにも成り立つ。`OneTimeSetUp` では頼まない（1 章より先に走る）
- 補助への文字列の引数は Base64 の UTF-8（`AndroidUiDriver.Base64(...)`）。表では生の文字列で書く
- 題名は、テストごとに固有の `NTK-AN-<番号>-<GUID の先頭 8 桁>`（以下 `<題名>`）。テストが決めるボタンの文言も、固有の後ろ付け（`-<GUID の先頭 8 桁>`。以下 `<固有>`）を付ける（補助は、広げた後にボタンの文言に合う部品が 2 つ以上あると `fail ambiguous` にする。補助の設計 3.5）。サンプル画面のテストは、サンプルの固定の題名と文言
- **イベントの数**は「期待」の列の終わりに `Op / Tap / Recv`（`NotificationOperationCompleted` / `NotificationActionTapped` / `NotificationReceived`）で書く。同期の問い合わせは `0 / 0 / 0`。後片付けの結果は含めない（6 章で足す）
- **アクティブ**: テストの側でアプリの `NotificationManager.getActiveNotifications()` を読み、ID（と tag）で判定する（`AndroidJavaObject`。テストの側だけ）。通知の post は、呼び出しの成功のイベントより遅れることがある（Notification Assistant のある端末では、`NotificationManagerService` が post を遅らせる。R2-1）。そのため 1 回だけ読んで決めず、次の 3 つの形で確かめる
  - **「在る」**: 毎フレーム読み直し、5 秒の間に現れれば通る。現れなければ失敗
  - **「消える」**（消した、払った、押した後）: 毎フレーム読み直し、5 秒の間に消えれば通る。消えなければ失敗
  - **「出ない」**（失敗を返した呼び出しが、通知を出していないこと）: **アンカー**で確かめる。失敗のイベントを受けた後、同じ Manager で、**対象と違う ID** と固有の題名のアンカーの通知を `ShowNotification` で出す（同じ ID だと、アンカーが対象を置き換える。R3-5）（その成功のイベントは数に入れる）。アンカーが「在る」になってから、対象の ID が無いことを 1 回見る。アンカーより先に出された通知は、アンカーより先にアクティブに現れる（補助の設計 V-29。成り立たなければ、アンカーが現れた後も 2 秒読み直す）。アンカーは後片付けの `CancelAllNotifications()` で消える。**1 章（権限が無い）ではアンカーを出せない**ので、2 秒の間読み直して無いことを見る（権限が無ければシステムが通知を捨てるので、ここでは強い確かめにならない。1 章で見るのは文言）
- シェードは、題名が OS の画面に出ることを見るときだけ使う（`notification-present`）
- **打ち切り**: Test Framework は `[UnityTest]` を、テストの開始から既定で 180 秒で打ち切る。画面の命令（`notification-*`、`permission-answer`、`close-screen`）を使うテストは、メソッドに `[Timeout(<画面の命令の数> × 200000 + 100000)]` を付ける（補助の設計 5 章。R3-3）

## 1. 新しいインストールの最初の状態（`AndroidNotificationPlayerTests` の順の付いたテスト）

前提: `verify_unity_android.sh` が実機の実行の前にテストの Player をアンインストールしている（補助の設計の 7 章）。`[Order(1)]` のテストの初めに、`HasPermission()` が false であることを確かめ、違えば `Assert.Fail("not a fresh install")`（Android 13 以降。V-27）。

| # | 順 | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|---|
| AN-02 | 1 | `ShowNotification`（`<題名>`、正しい JSON） | `Failed to showNotification: notifications are not allowed for this app (permission or app notification setting).`（設計 J-N7）。アクティブに出ない（2 秒の読み直し。0 章）。1 / 0 / 0 | 流す。成功（通知は出ない）の見込み |
| AN-02b | 2 | `UpdateNotification`（同じ ID） | `Failed to updateNotification: notifications are not allowed ...`。1 / 0 / 0 | 流す。成功の見込み |
| AN-02c | 3 | `ScheduleNotification`（過ぎた時刻、`<題名>`） | `Failed to scheduleNotification: notifications are not allowed ...`。1 / 0 / 0 | 流す。成功（すぐ出そうとして黙って出ない）の見込み |
| AN-03 | 4 | `Send("permission-answer", "deny")` → `RequestPermission(cb)` → `Wait` | `cb(false)`。`HasPermission()` が false。0 / 0 / 0 | 流す。同じ |
| AN-04 | 5 | `Send("permission-answer", "allow")` → `RequestPermission(cb)` → `Wait` | `cb(true)`。`HasPermission()` が true。0 / 0 / 0 | 流す。同じ |
| AN-05 | 6 | `CanScheduleExactAlarms()` を読み、false なら `ScheduleNotification`（正確、10 分後、`<題名>`） | false: 成功（不正確に下げて呼び直す。設計 J-N8）。`IsNotificationScheduled` が true。続けて `CancelScheduledNotification` で消す（成功。後片付けの消す呼び出しは 1 章で行わないので、自分で消す。R3-4）。2 / 0 / 0。true（V-24 が許可だったとき）: 値を記録し、この行は流さない（設計の層 2a の `ScheduleWithFallback` で押さえる）。0 / 0 / 0 | 流す。同じ（1.x の Kotlin が下げた） |
| AN-06 | 7 | `Do("grant", "android.permission.POST_NOTIFICATIONS")`、`Do("appop-allow", "SCHEDULE_EXACT_ALARM")` | どちらも `ok`。`HasPermission()` と `CanScheduleExactAlarms()` が true。0 / 0 / 0 | 流す |

## 2. 保持のテスト（`AndroidNotificationRetentionTests`。C ABI だけ。保持のテストだけの実機の実行）

- `SetUp` と、このテストの終わりまで、Notification の Manager に触れない（`Instance` を読まない。R1-4）
- C ABI の版の実装（設計 5.5 の手順 5）のときに、このファイルと、保持のテストだけの実機の実行（`verify_unity_android.sh`。補助の設計の 7 章）を、**同じ変更で**足す（1.x の段では `AndroidNotificationCApi` が無い。スクリプトだけ先に足すと、テストが 0 本でゲートが落ちる。R1-5、R2-2）。1.x の列は「対象外」

| # | 操作 | 期待 |
|---|---|---|
| AN-01 | `Do("grant", "android.permission.POST_NOTIFICATIONS")`。Manager の継ぎ目でリスナーが未登録であることを確かめる。テストが C ABI で直接、`EVENT_ONLY` の通知（`<題名>`、data `k=v`、autoCancel）を出し、アクティブに「在る」を待つ。`Send("notification-tap", <題名>, "stay")` → `Wait`。アクティブから「消える」を待つ（10 秒。システムが押下を受けた印。autoCancel で消すのはシステムで、受信機が走った印にはならない。R2-9）。さらに 3 秒待つ（受信機が走るのを待つ。保持の確かめを支えるのはこの待ち。足りることは補助の設計 V-30）。`Instance` を取り、同じフレームで購読する | `NotificationActionTapped`（`ActionBodyTap`、ID、data `k=v`）が 1 回、UnityMain。0 / 1 / 0 |

## 3. シェードを使わないテスト（`AndroidNotificationPlayerTests`）

| # | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|
| AN-10 | `ShowNotification`（`AndroidNotificationJsonBuilder` で組んだ最小の通知） | 成功が 1 回。呼び出しの中では 0 回。UnityMain。アクティブに在る。1 / 0 / 0 | 流す。同じ |
| AN-11 | `UpdateNotification`（同じ ID） | 成功。1 / 0 / 0 | 流す。同じ |
| AN-12 | `ShowNotification`（A、tag `t`）→ 在る → `CancelNotification(A, "t")` → 消える → `ShowNotification`（B）→ 在る → `CancelAllNotifications()` → 消える | 4 つとも成功。4 / 0 / 0（R2-11） | 流す。同じ |
| AN-13 | `CreateChannel`（全キー）→ `DeleteChannel(id)` | どちらも成功。2 / 0 / 0 | 流す。同じ |
| AN-14 | `ScheduleNotification`（不正確、10 分後）→ `IsNotificationScheduled` → `CancelScheduledNotification` → `IsNotificationScheduled` | 成功、true、成功、false。2 / 0 / 0 | 流す。同じ |
| AN-15 | `CancelAllScheduledNotifications()` | 成功。1 / 0 / 0 | 流す。同じ |
| AN-16 | Progress: start（15%）→ update（50%）→ complete → stop | 4 つとも成功。4 / 0 / 0 | 流す。同じ |
| AN-17 | JSON の必須の欠け（`title` が空白） | `Invalid argument for showNotification: title is required.`。1 / 0 / 0 | 流す。同じ |
| AN-18 | JSON の構文の誤り（`{"id":`） | `Failed to showNotification: the JSON could not be read.`。1 / 0 / 0 | 流す。`Failed to showNotification: <org.json の文言>`（設計 5.3） |
| AN-19 | 範囲の外の値（`groupAlertBehavior` 3、`<題名>`） | `Invalid argument for showNotification: Please verify the input parameters.`。アクティブに出ない（アンカー。0 章）。2 / 0 / 0（アンカーの成功を含む） | 流す。成功の見込み（設計 5.3）。アクティブに在る |
| AN-20 | 無いアイコンの名前（`smallIcon.name` = `ntk_no_such_icon`、`<題名>`） | `Failed to showNotification: a resource named in the notification was not found.`。**アクティブに出ない**（アンカー。何も表示しない。X1-13、R2-1）。2 / 0 / 0（アンカーの成功を含む） | 流す。成功（アプリのアイコンに代わる）。アクティブに在る |
| AN-21 | Progress の `progress` が無い | `Invalid argument for startProgressForegroundService: progress is required for Progress FGS operations.`。1 / 0 / 0 | 流す。同じ |
| AN-22 | 別のスレッドから `ShowNotification` | 例外が出ず、成功が UnityMain で 1 回。1 / 0 / 0 | 流す。`showNotification could not be started.`（`currentActivity`。Share の AS-35 と同じ経路）の見込み |
| AN-23 | `DeleteChannel(null)` | 成功（`""` を消す）。1 / 0 / 0 | 流す。値を記録する（設計 5.3 の推測） |
| AN-24 | `HasPermission()`、`AreNotificationsEnabled()`、`CanScheduleExactAlarms()`、`IsNotificationScheduled(id)` | true、true、true、false。0 / 0 / 0 | 流す。同じ |
| AN-25 | `AndroidNotificationJsonBuilderTests` の 3 つの Payload と、サンプルの全通知の Payload を、1 つずつ流す（Payload は PlayMode のテストのアセンブリに写す〈`AndroidNotificationTestPayloads`〉。元の `AndroidNotificationJsonBuilderTests` は Editor だけのアセンブリにあり、サンプルの JSON を組むメソッド〈`AndroidNotificationManagerExampleController.cs` の `BuildImmediateNotificationJson` など〉は private で、どちらも参照できない。写した元のファイルとメソッドを注に書き、元を変えたら合わせる。ずれは自動では見つけない。R3-7）（予約は 10 分後に置き換える。R1-14）。即時のものは `ShowNotification` → 在る → `CancelNotification` → 消える。予約は `ScheduleNotification` → `IsNotificationScheduled`（予約は後片付けの `CancelAllScheduledNotifications()` で消す） | どれも成功。即時のものは在って消え、予約は `IsNotificationScheduled` が true。**即時の数 × 2 ＋ 予約の数** / 0 / 0（数は実装のときに書く。R2-10） | 流す。同じ |

## 4. シェードと画面を使うテスト（`AndroidNotificationPlayerTests`）

**画面の操作の約束**（補助の設計 3.5）: テストは補助への依頼を先に `Send` し、同じフレームで Manager を呼び、`Wait`（200 秒。`ScreenAnswerTimeoutSeconds`）する。どのテストにも `[Timeout]` を付ける（0 章）。通知は 1 度に 1 つだけ出し、テストの終わりに消す（V-28）。

| # | 操作 | 期待（C ABI の版） | 1.x |
|---|---|---|---|
| AN-30 | `ShowNotification(<題名>)` → `Do("notification-present", <題名>)` | 成功。答えが `ok`（シェードに題名が出る）。1 / 0 / 0 | 流す。同じ |
| AN-31 | `ShowNotification(<題名>、launchAppOnTap=true、data k=v)` → `Send("notification-tap", <題名>, "front")` → `Wait` | 答えが `ok front`（補助が押す前に Player を後ろに回し、押した後に前面に戻った）。`NotificationActionTapped`（`ActionBodyTap`、ID、data `k=v`）が 1 回、UnityMain。1 / 1 / 0 | 流す。答え（`ok front` か `ok not-front`。設計 K-1）とイベントを記録する |
| AN-32 | 同上、`launchAppOnTap=false` → `notification-tap <題名> front` | 答えが **`ok not-front`**（押してもアプリを開かない。native-toolkit の `AnEventOnlyTapOpensNothing` と同じ確かめ。R2-12）。`ActionBodyTap` が 1 回。1 / 1 / 0 | 流す。答えとイベントを記録する |
| AN-33 | アクション 2 つ（`Play-<固有>`: launchApp、`Later-<固有>`: showsUserInterface=false）→ `notification-action <題名> Later-<固有> front` | 答えが **`ok not-front`**（R2-12）。`NotificationActionTapped`（ActionId `later`）が 1 回。1 / 1 / 0 | 流す。答えとイベントを記録する |
| AN-34 | 同上 → `notification-action <題名> Play-<固有> front` | 答えが `ok front`。ActionId `play` が 1 回。1 / 1 / 0 | 流す。答えを記録する |
| AN-35 | `ShowNotification(<題名>)` → `notification-dismiss <題名>` | `ActionNotificationDismissed` が 1 回。アクティブから消える。1 / 1 / 0 | 流す。同じ |
| AN-36 | `ShowNotification(<題名>、fullScreenIntent=true、priority 2、category call)` → `notification-present <題名>` | 成功。シェードに出る（画面が点いているので heads-up。設計 7.3）。1 / 0 / 0 | 流す。同じ |
| AN-37 | `ScheduleNotification(<題名>、正確、5 秒後)` → 10 秒待つ → `notification-present <題名>` | 成功。`NotificationReceived`（ID、tag、channel）が 1 回。1 / 0 / 1 | 流す。同じ |
| AN-38 | `ShowNotification(<題名>、tag t)` → 在る → `CancelNotification(id, "t")` | 成功が 2 回。アクティブから消える。2 / 0 / 0 | 流す。同じ |
| AN-39 | data に `"`、`/`、日本語を含む値 → `notification-tap <題名> stay` | `Data` の値が元の値と同じ。1 / 1 / 0 | 流す。値を記録する（設計 5.3 のエスケープ） |
| AN-40 | `Send("close-screen", "com.android.settings")` → `OpenNotificationSettings()` → `Wait` | 成功が 1 回。答えの組（Activity の名前と Intent の action。補助の設計 3.5）が、アプリの通知の設定の画面のもの（補助の設計 V-25 で確かめて、この表に書く）。1 / 0 / 0。結果が画面を離れる前か後かは判定しない（設計 5.3）。**アプリの詳細の画面に落ちたら失敗とする**: C ABI の版では、Manager の記録（`LastSettingsResultForTests`。internal。設定の完了の受け口〈`OnSettingsCore`〉が最後の result を残す。実装のときに足す）が `OPENED`（`OPENED_FALLBACK` でない）であることも照らす（答えの組は前面の記録から読むので、Settings が SPA に転送すると見分けられないことがある。R3-1）。native-toolkit の API はこの画面が無い端末のためにアプリの詳細へ落とす（`AndroidNotificationSettingsGateway.kt:43-45`）が、Pixel 6a（Android 16）にはこの画面があるので、落ちるのは Intent の誤り（設計 5.3 の 1.x の要確認がこれに当たる。X1-11） | 流す。答えの組を記録する（設計 5.3 の要確認: アプリの詳細の画面に落ちていた見込み） |
| AN-41 | 同上 → `OpenAppDetailsSettings()` | 成功。答えの組がアプリの詳細の画面のもので、AN-40 の組と違う。1 / 0 / 0 | 流す。組を記録する |
| AN-42 | 同上 → `OpenExactAlarmSettings()` | 成功。答えの組が正確なアラームの画面のもので、AN-40・AN-41 と違う。C ABI の版では、Manager の記録が `OPENED`（AN-40 と同じ。R3-1）。1 / 0 / 0 | 流す。組を記録する |
| AN-43 | `Do("background", "1000")` の間に、別のスレッドから `OpenNotificationSettings()` を呼ぶ（Dialog の AD-27 と同じ形） | `Failed to openNotificationSettings: the app is not in the foreground.`。1 / 0 / 0 | 流す。`openNotificationSettings could not be started.`（別のスレッド。Share の AS-35）の見込み |

- AN-40〜AN-42: 補助の設計 V-25 で、3 つの画面が組でも見分けられないと分かったら、AN-41・AN-42 の「違う」の確かめを外す（そのときも、アプリの詳細の画面への落ちは Manager の記録で照らせる。1.x の版には記録が無いので、答えの組を記録するだけ）。画面が Back と `front` で消えないと分かったら、3 行とも手動に回す（補助の設計 V-25。v3 の CLEAR_TOP は、起動の Activity が singleTask で `front` と同じ働きなので、補助の v15 で外した）

## 5. サンプル画面を通すテスト（`AndroidNotificationPlayerTests`。カテゴリ `AndroidSampleScreen` も付ける）

サンプルのトップメニューを読み込み、Notification の画面を開いてボタンを押す（Share の 1 章と同じ形）。結果は `ResultTextBlock` の最後の値（`<操作名>: Success\n<説明>`）で照らす。Test Android™ Profile では `Assert.Ignore`（Share と同じ。その実行のゲートは `AndroidSampleScreen` のテストだけを skip の判定から外す。補助の設計 7 章）。

| # | ボタン | 期待 | 1.x |
|---|---|---|---|
| AN-50 | ShowNotification → `notification-present Energy Refilled` → UpdateNotification → CancelNotification | 3 つとも `Success`。答えが `ok`。3 / 0 / 0 | 流す。同じ |
| AN-51 | ScheduleNotification → IsScheduledNotification → CancelScheduled | `Success`、予約ありの表示、`Success`（サンプルの予約は 15 秒後なので、ScheduleNotification から 10 秒以内に取り消す）。2 / 0 / 0 | 流す。同じ |
| AN-52 | Start / Update / Complete / Stop Progress | 4 つとも `Success`。4 / 0 / 0 | 流す。同じ |
| AN-53 | ShowActionNotification → `notification-action <サンプルの題名> Dismiss stay` | `Success`、続いて `Notification Action Button` の表示（ActionId）。1 / 1 / 0。サンプルのボタンの文言は固定なので、ほかのアプリの通知に同じ文言のボタンがあると `fail ambiguous` で落ちる（黙って取り違えはしない） | 流す。同じ |
| AN-54 | ShowFullScreenNotification → CancelAll → ShowDecoratedCustomView → CancelAll | 2 つとも `Success`（custom view の layout はこのプロジェクトの androidlib にある）。4 / 0 / 0 | 流す。同じ |
| AN-55 | HasPermission、NotificationsEnabled、CanScheduleExactAlarms | `HasPermission: True` など。0 / 0 / 0 | 流す。同じ |

## 6. テストの共通の約束

- **後片付け**（`AndroidSharePlayerTests.cs:74-140` の段に合わせる。X2-1、R2-4）。どの段の例外も集めて、最後にまとめて `Assert.Fail` する（Share の `Safely`）:
  1. UI を使わないもの: 別のスレッドの呼び出し（AN-22、AN-43）を止めて待つ
  2. 回収されていない依頼: `Ui.PendingCount` を記録する（0 でなければ、後で失敗に足す。補助の設計 5 章）
  3. UI の段: `WaitForPending`（未回収の依頼をすべて回収）。5 章のテストでは、サンプルのシーンを下ろす（下ろすときに結果が来れば数に入れる）。画面が残っていれば Unity は止まったままでここに来ない（閉じるのは補助。補助の設計 3.5）
  4. 消す: `CancelAllNotifications()`、`CancelAllScheduledNotifications()`、Progress の stop を呼ぶ。この 3 回の結果は、表の数に足して照らす。**1 章の順の付いたテストでは呼ばない**（AN-02〜AN-03 は権限が無く、その間の消す呼び出しの結果は設計で決めていない。AN-05 は残した予約を自分で消す。R3-4）。依頼の回収の後に置く（Share は回収の前に消すが、消す呼び出しが、補助がシェードで探している通知を消さないように）
  5. 数: 表の数（と 4 の 3 回）がそろうのを待つ（5 秒）。**照合の起点は、イベントがそろった時刻（最後のイベントの時刻）、最後の補助の答えを受けた時刻、`OnApplicationPause(false)` の時刻のうち遅いもの**（X2-1。v2 は最後の補助の答えだけで、補助を使わないテストには起点が無かった）。起点から 0.5 秒、さらに 0.5 秒待ち（Share と同じ）、余分なイベント（`stay` の後の二重の通知など）が来ていないこと、数が表と同じこと、すべて UnityMain であることを照らす。`PendingCount` の記録（2）が 0 でなければ、ここで失敗に足す
  6. C ABI の版では、`PendingCallCountForTests` が 0 になるのを待つ（5 秒。Share の 6 段）
  7. 必ず: 購読を外し、取り出し口の列が空になるまで 2 フレーム回してから、数を 0 に戻す
- **保持のテスト（AN-01）**は、プロセスの中で最初に Notification の Manager を作るテストでなければ成り立たない。前提が崩れていれば `Assert.Fail`（Ignore にしない）
- 予約の発火を待つテスト（AN-37）は、正確なアラームが許可されている前提（1 章の AN-06、3〜5 章の `EnsureGranted()`）
- **ゲート**（補助の設計の 7 章。X2-2、R2-14）: 必須は既定の Android™ Profile の実行。そこでは `AndroidUiDriver` のテストが 1 本以上あり、skip が 0。補助がいなければ、補助を使うテストが `Assert.Ignore` になり、ゲートで落ちる（補助を使わない順の付いたテストは走り、権限の前提が崩れて失敗しうる。Editor から流したときだけの話。R3-8）。Test Android™ Profile の実行は、`--as-package-user` を付けたときだけ流れる。そこでも、`AndroidSampleScreen` を除く `AndroidUiDriver` のテストは skip 0（1〜4 章が Ignore になれば落ちる）。v1、v2 は「権限のテストを両方の Profile で流す」と書いたが、Test Android™ Profile の実行は既定ではない

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
