# iOS Notification 自動テスト計画 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | `IosNotificationManager`（`Runtime/iOS/Notification/IosNotificationManager.cs`）の 21 の操作（`Initialize`、`RequestPermission`、`HasPermission`、`GetAuthorizationStatus`、`OpenNotificationSettings`、`ShowNotification`、`ScheduleNotification`、`UpdateNotification`、取り消しと消去の 6 つ、`GetScheduledNotifications`、`GetDeliveredNotifications`、`SetBadgeCount`、`RegisterCategory`、`RemoveCategory`）と 3 つのイベント（`NotificationOperationCompleted`、`NotificationActionReceived`、`NotificationTextInputActionReceived`） |
| 目的 | iOS の C ABI `2.0.0` への移行（[artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md)）の段 3-R。**1.x のまま記録**し、段 3 で Notification を移した後に同じテストを流して、1.x と同じか、既知の差分かを確かめる |
| 実行 | 許可を求めず、画面も出さないものは iOS の Player のテスト（`scripts/verify_unity_ios.sh`、`Tests/PlayMode/IosNotificationPlayerTests.cs`）。許可のダイアログ、バナー、通知センター、設定のアプリを押すものは UI のハーネス（`scripts/test_ios_ui.sh`、ハーネスの `notification` のシナリオと `NotificationUITests`） |
| 書き方 | Player のテストは結果の型の値（`IsSuccess`、`ErrorMessage`、JSON の中の id の一覧）を NUnit で照らす。UI の行の書き方は Dialog の計画 v3 の 1 章と同じ（null は `<null>`、全文一致、押した後 3 秒待って本数を数える） |
| 1.x の振る舞いの出典 | native-toolkit のタグ `1.13.0` の `ios/UnityIosPlugin/UnityIosPlugin/Notification/` と `ios/IosLibrary/IosLibrary/Notification/`。C# の受け手の持ち方（操作ごとに static の 1 つ、後勝ち）は `IosNotificationManager.cs` |
| `2.0.0` との差 | native-toolkit の `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 の NT-01〜NT-21 と、`Notification.h` |
| 移行前の基準 | 段 2 の (B) のパイロット `NotificationUITests/test_permissionAndBannerTap`（シミュレーターだけ）。ほかは無い。1.x の列は予想で、**実測が違えば記録の文書に書く**（Dialog の ID-14a） |

**私物の扱い**: 実機では、ハーネスの通知のほかを読まない・押さない。失敗の文に springboard のラベルを書かない。通知センターは、ハーネスの通知だけを、題で探して押す（native-toolkit の `device-testing.md`）。

## 1. 画面を出さないテスト（iOS の Player、`IosNotificationPlayerTests`）

Player のテストのアプリ（`com.jonghyunkim.nativetoolkit.playertests`）は、毎回入れ直され、通知の許可を**一度も求めない**（許可のダイアログを押す者がいない）。だから権限は `notDetermined` のままで、通知は画面に出ない。各テストは終わりに、自分の id の予約と配信済みを消す。

| ID | 何をするか | 1.x の予想 | `2.0.0` の期待 |
|---|---|---|---|
| NP-1 | `GetAuthorizationStatus` | `NotDetermined`（記録する） | 同じ（NT-14） |
| NP-2 | `HasPermission` | `false` | 同じ |
| NP-3 | `HasPermission` を同じフレームで 2 回（受け手 A と B） | **A は 0 回、B は 2 回**の見込み（static の 1 つに後勝ちで入る。記録する） | **既知の差分**: 要求ごとに 1 回（A・B とも 1 回。NT-19） |
| NP-4 | 許可の無いまま `ScheduleNotification`（60 秒後、id `ntkp-4`）、`GetScheduledNotifications` | 成功の見込み（許可が無くても予約は積まれる。記録する）。予約の一覧の id に `ntkp-4` が入るかを記録する | **既知の差分**: `PERMISSION_DENIED`（NT-02）。今の成功に写すかは写し方の設計（2c）で決める |
| NP-5 | 許可の無いまま `ShowNotification`（trigger なし） | 成功の見込み（P2-10「黙って成功」） | NP-4 と同じ |
| NP-6 | 不正な trigger（`{"type":"unknown"}`）で `ShowNotification` | 記録する（1.x は即時に出す形に落ちる見込み。P2-15） | **既知の差分**: `INVALID_PARAMETER` |
| NP-7 | 不正な trigger で `ScheduleNotification` | 失敗（文言を記録する） | 同じ（今の失敗の文言で返す。NT-03） |
| NP-8 | NT-02 の「今は通って C ではエラーになる値」を 1 つずつ（空の `id`、負の `badge`、0〜1 の外の `relevanceScore`、`file://` でない添付、項目の無い calendar、オブジェクトでない `userInfo`）で `ScheduleNotification` | 記録する（どれも成功の見込み） | **既知の差分**: `INVALID_PARAMETER` を今の失敗に写す（P2-15） |
| NP-9 | 範囲外の `timeInterval`（0 と負の値）で `ScheduleNotification` | 記録する（UserNotifications が例外を投げて落ちうる。P2-26。**落ちれば、そのテストは記録だけにして外す**。後のテストを巻き込まないよう、NP の最後に流す） | 落ちずに `INVALID_PARAMETER` |
| NP-10 | `UpdateNotification`（予約した id に別の題）、`GetScheduledNotifications` | 同じ id が 1 つだけで、題が新しい | 同じ |
| NP-11 | `CancelScheduledNotification`、`CancelAllScheduledNotifications`、`CancelNotification`、`CancelAllNotifications` の後に `GetScheduledNotifications` | 消える。完了の受け手は無い | 同じ（完了を捨てる。NT-05・06・09・10） |
| NP-12 | `GetDeliveredNotifications` | 空の一覧の JSON（形を記録する） | 同じ（NT-12。時刻の形は NU-6 で見る） |
| NP-13 | `SetBadgeCount(-1)` | 記録する（成功の見込み） | **既知の差分**: `INVALID_PARAMETER`（NT-16） |
| NP-14 | `SetBadgeCount(0)` | 成功 | 同じ |
| NP-15 | `RegisterCategory`（正しい 1 つ）と、その受け手が呼ばれる時（入口の中か、後のフレームか） | 成功。**入口の中で**呼ばれる見込み（記録する） | 成功。完了は書き込みと読み返しの後（NT-17）。入口の中のコールバックは保たない（既知の差分） |
| NP-16 | 不正な要素（空の action の id）を含む `RegisterCategory` | 記録する（捨てて成功の見込み） | **既知の差分**: `INVALID_PARAMETER` |
| NP-17 | `RemoveCategory`、`RemoveDeliveredNotification`、`RemoveAllDeliveredNotifications` | 落ちない。完了の受け手は無い | 同じ（NT-07・08・18） |
| NP-18 | `ShowNotification` を同じフレームで 2 回（受け手 A と B） | 記録する（A は 0 回、B は 2 回の見込み） | 1 回ずつ（包みが要求ごとに持つ） |

`NotificationOperationCompleted` は、NP-4・5・7・10・13〜16 で、受け手より前にちょうど 1 回来るかも数える。

## 2. 画面を押すテスト（UI のハーネス、`NotificationUITests`）

### 2.1 シミュレーター

ハーネスはテストごとに入れ直す（許可がまだ決まっていない状態から始める）。

| ID | ハーネスの行 | XCUITest がすること | 1.x の予想（結果の行） | `2.0.0` の期待 |
|---|---|---|---|---|
| NU-1 | `RequestPermission` | 許可のダイアログで「許可」 | `[permission@ok] granted` | 同じ |
| NU-2 | 同じ | 「許可しない」 | `[permission@fail] error=Notification permission was denied.`（行に文言を足す） | 同じ（`NONE` を今の文言に写す。NT-13） |
| NU-3 | NU-1 の後に `RequestPermission` をもう一度 | ダイアログが出ないことを確かめる | `[permission@ok] granted` が 1 本 | 同じ（OS に求めず今の状態で答える） |
| NU-4 | 許可の後、10 秒後の通知を予約（今のパイロット） | ホームに戻してバナーを押す | `[tap@event] id=ntkh-1 action=com.apple.UNNotificationDefaultActionIdentifier` が 1 本 | 同じ |
| NU-5 | カテゴリー（`Open` と、文字の入力の `Reply`）を登録し、それを持つ通知を予約 | バナーを長押しして `Open` を押す | `[tap@event] id=… action=Open` が 1 本 | 同じ（`_kind` が `TEXT_INPUT` でないものを今のコールバックへ。NT-20） |
| NU-6 | NU-5 と同じ | `Reply` を押し、`ntk reply` と打って送る | `[reply@event] id=… action=Reply text=ntk reply` が 1 本、`[tap@event]` は 0 本 | 同じ（NT-21） |
| NU-7 | NU-4 の後に `GetDeliveredNotifications` | 押さない（バナーを押さずに通知センターに残す形にする） | 配信済みの JSON に `ntkh-1` があり、時刻の形を記録する（小数秒なしの ISO 8601 の見込み） | 同じ（NT-12 で写す） |
| NU-8 | 通知を予約してハーネスを終わらせる | バナーを押してハーネスを起動させる | **タップは届かない**見込み（1.x は受け手の登録前のものを失う。native-toolkit の第 1 部の設計書〈`2026-10-31-ios-c-abi-c-abi-design.md`〉の `5.12.7`。記録する） | **既知の差分の候補**: コールドスタートのタップが届く（NT-20）。保った分を捨てるかは写し方の設計（2c）で決める |
| NU-9 | `OpenNotificationSettings` | 設定のアプリが前に来たことを確かめ、ハーネスに戻す | 記録する（完了の受け手は無い） | 同じ（空の受け手を渡す。NT-15） |
| NU-10 | 3 秒後にネイティブの側で `RequestPermission` を呼ぶ行（未決のまま、Control Center を開いて非活性に） | Dialog の ID-17 と同じ | 記録する（ダイアログが出るか、失敗か） | 未決で前面でなければ `NOT_FOREGROUND`（NT-13）。前面で非活性の扱いは記録で決める |

### 2.2 実機（ハーネスを 1 回だけ入れる）

おやすみモードの集中モードでは、許可するアプリに入っていない通知のバナーは出ない。ハーネスを入れ直すと許可するアプリから外れる（段 2 の実機の回、[README](../../../topics/ios-c-abi-2/README.md) の 6.6）。そこで native-toolkit と同じ形にする。

1. 承認を得た実行の中で、ランナーがハーネスを入れる（入れ直さない形の実行。`scripts/test_ios_ui.sh` に足す）
2. 利用者が、集中モードの「許可するアプリ」に「NTK UI Harness」を足す（ランナーは止まって待つ。端末の設定は agent から変えない）
3. NU-1 を流して許可を「許可」にする（許可のダイアログは springboard の操作。承認の区分は `systemui`）
4. NU-4・5・7 を、入れ直さずに続けて流す。終わりにハーネスを消す（承認の一覧に書く）

| 実機で流す | 流さない（シミュレーターだけ） |
|---|---|
| NU-1、NU-4、NU-5、NU-7 | NU-2・3（許可を「許可しない」にすると、その起動の間は戻せない。入れ直しが要る）、NU-6（端末のキーボードで打つ。Dialog の ID-9 と同じく流せるが、返信の欄が集中モードの下で出るかを測っていない。シミュレーターの記録の後に決める）、NU-8（アプリを終わらせて通知から起こす。springboard の操作が長い）、NU-9（設定のアプリを開く）、NU-10（Control Center） |

ランナーの変更（入れ直さない形、止まって待つ所、承認の一覧）は、この計画が決まった後に、UI の自動化の設計書 v4 の 4.6 の差分として書いてレビューに出す。

## 3. テストの共通の約束

- Player のテストのカテゴリーは `IosPlayer`。名前は `IosNotificationPlayerTests.NP<番号>_<意味>`。予約の id は `ntkp-<番号>`。各テストは `finally` で自分の id の予約と配信済みを消す
- UI のテストの名前は `NotificationUITests/test_NU<番号>`。パイロット `test_permissionAndBannerTap` は NU-1 と NU-4 に分けて消す
- 結果の行はバナーの題 `NTK UI Harness` で探す。通知の本文にテストの値以外を入れない
- 予期しないアラートが残れば失敗（`HarnessUITestCase`）

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| 位置の trigger（`LocationTriggerPayload`）の発火 | 位置の許可と移動が要る。Player のテストで形の誤り（NT-02 の範囲外）だけを見る |
| 添付の画像の見た目 | 手で見る |
| アイコンのバッジの数の見た目 | springboard のアイコンを読むのは端末ごとに違う。`SetBadgeCount` の結果だけを見る |
| 通知センターから消したときの dismiss の動作 | 1.x のカテゴリーに custom dismiss の口が無い（記録の後に、無ければ 4 章に残す） |
| `Initialize` を入れ直しの口として呼ぶ（I-3） | 段 3 の写し方の設計で決めてから足す |

## 5. 1.x の記録

記録は別の文書に書く（計画の版は上書きしない）。
