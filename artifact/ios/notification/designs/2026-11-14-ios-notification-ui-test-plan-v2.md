# iOS Notification 自動テスト計画 v2

v1 からの変更は [レビュー v1](../reviews/2026-11-14-ios-notification-ui-test-plan-review-v1.md) の 3 章の判断による（6 章に一覧）。

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | `IosNotificationManager`（`Runtime/iOS/Notification/IosNotificationManager.cs`）の 21 の操作（`Initialize`、`RequestPermission`、`HasPermission`、`GetAuthorizationStatus`、`OpenNotificationSettings`、`ShowNotification`、`ScheduleNotification`、`UpdateNotification`、取り消しと消去の 6 つ、`GetScheduledNotifications`、`GetDeliveredNotifications`、`SetBadgeCount`、`RegisterCategory`、`RemoveCategory`）と 3 つのイベント（`NotificationOperationCompleted`、`NotificationActionReceived`、`NotificationTextInputActionReceived`） |
| 目的 | iOS の C ABI `2.0.0` への移行（[artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md)）の段 3-R。**1.x のまま記録**し、段 3 で Notification を移した後に同じテストを流して、1.x と同じか、既知の差分かを確かめる |
| 実行 | **許可が未決のときの基準**だけを iOS の Player のテスト（`scripts/verify_unity_ios.sh`、`Tests/PlayMode/IosNotificationPlayerTests.cs`）で見る。許可の後でなければ意味の無いもの（予約・更新・取り消し・配信済み・イベント）と、許可のダイアログ、バナー、通知センター、設定のアプリを押すものは UI のハーネス（`scripts/test_ios_ui.sh`、ハーネスの `notification` のシナリオと `NotificationUITests`） |
| 書き方 | Player のテストは結果の型の値（`IsSuccess`、`ErrorMessage`、一覧の JSON の中の自分の id）を NUnit で照らす。UI の行の書き方は Dialog の計画 v3 の 1 章と同じ（null は `<null>`、全文一致、押した後 3 秒待って本数を数える）。JSON は**解いてから**比べる（`JSONSerialization` のキーの順は決まっていない） |
| 1.x の振る舞いの出典 | native-toolkit のタグ `1.13.0` の `ios/UnityIosPlugin/UnityIosPlugin/Notification/` と `ios/IosLibrary/IosLibrary/Notification/`。C# の受け手の持ち方（操作ごとに static の 1 つ。最初の完了で取り出して null にする）と、受け手の後にイベントを出す順は `IosNotificationManager.cs` |
| 1.x の実測（native-toolkit） | part2 の設計書の 0.2 の M2（権限が無いと `add` は成功し pending は 0。`notDetermined` の即時の分だけ配信済みに載る）、M4（`https://` の添付は失敗）、M5・M8（範囲外の `timeInterval` で落ちる）、M6（`cancelAll` の完了の中で足した予約は残る） |
| `2.0.0` との差 | native-toolkit の `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 の NT-01〜NT-21 と 6.3、`Notification.h` |
| 移行前の基準 | 段 2 の (B) のパイロット `NotificationUITests/test_permissionAndBannerTap`（シミュレーターだけ）。ほかは無い。1.x の列は予想で、**実測が違えば記録の文書に書く**（Dialog の ID-14a） |

**私物の扱い**: 実機では、ハーネスの通知のほかを読まない・押さない。失敗の文に springboard のラベルを書かない。通知はテストごとに本文を変え、**題と本文の両方で 1 件に絞って**から押す（3 章）。

## 1. 画面を出さないテスト（iOS の Player、`IosNotificationPlayerTests`）

Player のテストのアプリ（`com.jonghyunkim.nativetoolkit.playertests`）は、毎回入れ直され、通知の許可を**一度も求めない**。だから権限は `notDetermined` のままで、通知は画面に出ない。流す順は `[Order]` で決める。各テストは終わりに、自分の id の予約と配信済みを消す。一覧は自分の id だけを見る（ほかのテストの片付けは待たずに終わるため）。

| ID | 何をするか | 1.x の予想 | `2.0.0` の期待 |
|---|---|---|---|
| NP-1 | `GetAuthorizationStatus` | `NotDetermined` | 同じ（NT-14） |
| NP-2 | `HasPermission` | `false` | 同じ |
| NP-3 | `HasPermission` を続けて 2 回（受け手 A と B） | **A は 0 回、B は 1 回**（最初の完了で B を取り出して null にし、2 つ目の完了には受け手が無い）。static はロックなしなので、競合で B が 2 回もありうる。記録し、B = 2 は固定の期待にしない | **既知の差分**: 要求ごとに 1 回（A・B とも 1 回。NT-19） |
| NP-4 | 未決のまま `ScheduleNotification`（60 秒後、id `ntkp-4`）、その後に `GetScheduledNotifications` | 成功。予約の一覧に `ntkp-4` は**無い**（M2） | **既知の差分**: `PERMISSION_DENIED`（NT-02）。何も足さない。今の成功に写すかは写し方の設計（2c）で決める |
| NP-5 | 未決のまま `ShowNotification`（trigger なし、id `ntkp-5`）、その後に `GetDeliveredNotifications` | 成功。配信済みに `ntkp-5` が**載る**見込み（M2。記録する） | NP-4 と同じ。配信済みにも載らない（「完全には同じにならない」の材料） |
| NP-5b | 未決のまま `UpdateNotification`（無い id） | 成功、一覧は 0 件の見込み（記録する） | NP-4 と同じ。前の要求を消さない（part2 の `6.3`） |
| NP-6 | 不正な trigger（`{"type":"unknown"}`）で `ShowNotification` | 記録する（即時に出す形に落ちる見込み。P2-15） | **既知の差分**: `INVALID_PARAMETER` |
| NP-7 | 不正な trigger で `ScheduleNotification` | 失敗（文言を記録する） | 同じ（今の失敗の文言で返す。NT-03） |
| NP-8 | 生の JSON で、NT-02 の「今は通って C ではエラーになる値」を 1 つずつ渡す（下の 1.1） | 1 つずつ記録する | **既知の差分**: `INVALID_PARAMETER` を今の失敗に写す（P2-15）。未決なので `PERMISSION_DENIED` が先に来るかも記録する |
| NP-9 | 範囲外の `timeInterval`（0、-1、repeats で 30 と 59.9、NaN） | **1.x では流さない**（M5・M8 で落ちると測られている。落ちると Player の回の結果がまとめて消える）。1.x の列は「落ちる（M5・M8）」 | 落ちずに `INVALID_PARAMETER`。**`2.0.0` に切り替えた後だけ有効にする**（`[Explicit]` を外す） |
| NP-13 | `SetBadgeCount(-1)` | 記録する（成功の見込み） | **既知の差分**: `INVALID_PARAMETER`（NT-16） |
| NP-14 | `SetBadgeCount(0)` | 記録する（未決でのバッジは測られていない） | 同じ |
| NP-15 | `RegisterCategory`（正しい 1 つ）。受け手が呼ばれるのが入口から戻った後か、何フレーム目か | 成功。**入口から戻った後、後のフレーム**（ネイティブの完了は入口の中だが、C# は Dispatcher の `Update` に積む）。何フレーム目かを記録する | 成功（NT-17 の本題は NU-12 で見る） |
| NP-16 | 生の JSON の category で、(a) `title` の無い action（1.x のパーサーが捨てる）、(b) 空文字の action の id（1.x は残す）。C# の Builder は空の id を除くので使わない | (a) 捨てて成功、(b) 残して成功の見込み（記録する） | **既知の差分**: どちらも `INVALID_PARAMETER` |
| NP-18 | `ShowNotification` を続けて 2 回（受け手 A と B） | A は 0 回、B は 1 回、`NotificationOperationCompleted` は 2 回（記録する） | 1 回ずつ（包みが要求ごとに持つ） |

`NotificationOperationCompleted` は、NP-4〜7・13〜16 で、**受け手の後に**ちょうど 1 回来るかを数える（`IosNotificationManager.cs` は受け手を先に呼ぶ。受け手が例外を投げるとイベントは出ない）。

### 1.1 NP-8 の値

どれも生の JSON で渡し、境界の正しい値も対にして 1 回ずつ流す（正しい値は成功）。

| 値 | 渡し方 |
|---|---|
| 空の id | Show の `content.id`、Schedule の `identifier`、Schedule の `content.id` を別々に（1.x の Schedule は `content.id` を使わない） |
| 負の `badge` | `-2`（C では `-1` が「未設定」の意味なので避ける。`Notification.h`）。対は `0` |
| `relevanceScore` | `1.5`。対は `1.0` |
| 添付 | `https://example.com/a.png`（1.x でも失敗の見込み。M4。文言を記録する） |
| calendar | 項目の無い calendar |
| location | 範囲外の緯度（`91`）、空の識別子、入ると出るの両方が `false` |
| userInfo | オブジェクトでない値（`[1]`）、入れ子の `null`、予約のキー |
| `interruptionLevel` | 未知の文字列（1.x は `.active` にする見込み） |
| option | 未知の文字列 |

## 2. 画面を押すテスト（UI のハーネス、`NotificationUITests`）

**許可の前処理**: 許可が要るテストは、初めに `RequestPermission` の行を押す。シミュレーターではテストごとに入れ直すので、許可のダイアログで「許可」を押す。実機（2.2）では、ダイアログが出なければ待たずに進む（既に許可されている）。どちらも `[permission@ok] granted` を確かめる。

**後片付け**: どのテストも `tearDown` で、ハーネスの片付けの行（`CancelAllNotifications` と `RemoveAllDeliveredNotifications`）を押す。

### 2.1 シミュレーター

| ID | ハーネスの行 | XCUITest がすること | 1.x の予想（結果の行） | `2.0.0` の期待 |
|---|---|---|---|---|
| NU-1 | `RequestPermission`、続けて状態と `HasPermission` | 「許可」 | `[permission@ok] granted`、`[status@ok] Authorized`、`[has@ok] True` | 同じ |
| NU-2 | 同じ | 「許可しない」 | `[permission@fail] error=<文言>`。文言は 1.x の汎用の文（`NotificationError` に説明が無いので `localizedDescription` の既定。実測を全文で記録する）。`[status@ok] Denied`、`[has@ok] False` | **既知の差分の候補**: 1.x の汎用の文を保つか、新しい文言にするかを 2c で決める（`NONE` は 0。NT-13） |
| NU-2b | NU-2 の後に Show と Schedule、続けて予約と配信済みの一覧 | 押さない | 記録する（M2: 成功し、どちらの一覧にも載らない見込み） | `PERMISSION_DENIED`。写し方は 2c |
| NU-3 | NU-1 の後に `RequestPermission` をもう一度 | ダイアログが出ないことを確かめる | `[permission@ok] granted` が 1 本 | 同じ（OS に求めず今の状態で答える） |
| NU-3b | NU-2 の後に `RequestPermission` をもう一度 | ダイアログが出ないことを確かめる | 記録する（失敗が 1 本の見込み） | `NONE` を今の失敗に写す |
| NU-4 | 許可の後、userInfo（`{"k":1,"nested":{"a":[1,"日本"]}}`）を持つ 10 秒後の通知を予約 | ホームに戻して、題と本文で 1 件に絞ったバナーを押す | `[tap@event] id=ntkh-4 action=com.apple.UNNotificationDefaultActionIdentifier userInfo=<JSON>` が 1 本。JSON は解いて比べる | 同じ（予約のキーの印を外して作る。part2 の `6.3`） |
| NU-5 | 許可の後、category（`Open` は **foreground**、文字の入力の `Reply`）を登録し、それを持つ通知を予約 | バナーを長押しし、展開した中身が自分の通知であることを確かめてから `Open` を押す | `[tap@event] id=ntkh-5 action=Open …` が 1 本 | 同じ（`_kind` が `TEXT_INPUT` でないものを今のコールバックへ。NT-20） |
| NU-6 | NU-5 と同じ | `Reply` を押し、`ntk reply` と打って送る。**3 秒待ってからハーネスを前に戻す**（1.x の文字の入力の action は背景で動く。options が `[]`） | `[reply@event] id=ntkh-6 action=Reply text=ntk reply` が 1 本、`[tap@event]` は 0 本 | 同じ（NT-21）。すぐ前に戻すと `_is_launch` で来ることがある（part2 の `6.3` の末尾）ので、待ちは同じにする |
| NU-7 | 許可の後、前面のまま即時の通知を出し、3 秒後に `GetDeliveredNotifications` | 押さない | 配信済みの JSON に `ntkh-7` があり、時刻の形を記録する（小数秒なしの ISO 8601 の見込み） | 同じ（NT-12 で写す） |
| NU-8 | 許可の後、通知を予約してハーネスを終わらせる | バナーを押してハーネスを起動させ、`[launch@ok] scenario=notification source=file` と受け手の登録の行を確かめてから数える | **タップは届かない**見込み（1.x は受け手の登録前のものを失う。native-toolkit の第 1 部の設計書〈`2026-10-31-ios-c-abi-c-abi-design.md`〉の `5.12.7`。記録する） | **既知の差分の候補**: コールドスタートのタップが届く（NT-20）。`.mm` が起動の中で delegate を入れること（NT-01）が前提。保った分を捨てるかは 2c で決める |
| NU-9 | `OpenNotificationSettings` | 設定のアプリが前に来たことを確かめ、ハーネスに戻す | 記録する（完了の受け手は無い） | 同じ（空の受け手を渡す。NT-15） |
| NU-10 | 3 秒後にネイティブの側で `RequestPermission` を呼ぶ行（未決のまま、Control Center で非活性に）。呼んだ時点の状態を `[late@event] state=…` に出す（必ず照らす） | Dialog の ID-17 と同じ | 記録する（ダイアログが出るか、失敗か） | 前面で非活性は受け付ける（`ForegroundLocator`） |
| NU-11 | 許可の後の、予約と配信済みの一連（下の 2.3） | 押さない | 2.3 の各段の一覧 | 同じ（NT-03〜12） |
| NU-12 | 許可の後、category `X` を `[A]` で登録、続けて `[B]` で登録し、`X` の通知を出す。別に、category `Y` を登録して `X` を消し、`Y` の通知を出す | 長押しして出る action を記録する（`X` は `A` か `B` か、`Y` は出るか）。押さずに閉じる | 記録する（1.x は `Set.insert` だけで、重ねても置き換わらない見込み） | 書き込みと読み返しの後に完了（NT-17）。置き換えるかは記録で決める |
| NU-13 | 許可の後、custom dismiss を持つ category の通知を出す | 通知センターで、題と本文で絞った自分の通知だけを消し（native-toolkit の `clearEventInNotificationCenter`）、ハーネスを前に戻す | `[tap@event] id=ntkh-13 action=com.apple.UNNotificationDismissActionIdentifier …` が 1 本、`[reply@event]` は 0 本 | 同じ（DISMISS も今のコールバックへ。NT-20） |
| NU-14 | 未決のまま、背景に入ったら `RequestPermission`（ハーネスの `OnBackground`。Share の SH-18 と同じ） | ホームに戻し、5 秒後にハーネスを前に戻す | 記録する（ダイアログが出るか、完了が来るか） | 未決で前面でなければ `NOT_FOREGROUND`（NT-13）。写し方は 2c |
| NU-15 | 背景に入ったら `OpenNotificationSettings` | 同じ | 記録する（1.x は OS が黙って止める見込み） | `NOT_FOREGROUND`（NT-15） |

### 2.2 実機（ハーネスを 1 回だけ入れる）

おやすみモードの集中モードでは、許可するアプリに入っていない通知のバナーは出ない。ハーネスを入れ直すと許可するアプリから外れる（段 2 の実機の回、[README](../../../topics/ios-c-abi-2/README.md) の `6.6`）。そこで native-toolkit と同じ形にする。

1. 承認を得た実行の中で、ランナーがハーネスを入れる（入れ直さない形の実行。`scripts/test_ios_ui.sh` に足す）
2. 利用者が、集中モードの「許可するアプリ」に「NTK UI Harness」を足す（ランナーは止まって待つ。端末の設定は agent から変えない）
3. NU-1 を流して許可を「許可」にする（許可のダイアログは springboard の操作。承認の区分は `systemui`）
4. NU-4・5・7・11 を、入れ直さずに続けて流す。終わりに（途中で止まったときも）ハーネスを消す（承認の一覧に書く）

| 実機で流す | 流さない（シミュレーターだけ） |
|---|---|
| NU-1、NU-4、NU-5、NU-7、NU-11 | NU-2・2b・3b（「許可しない」は入れ直さないと戻せない）、NU-3（NU-1 の後に続けて流せるが、実機の回を短くするため）、NU-6（返信の欄が集中モードの下で出るかを測っていない。シミュレーターの記録の後に決める）、NU-8（アプリを終わらせて通知から起こす）、NU-9（設定のアプリを開く）、NU-10（Control Center）、NU-12（長押しの後に押さずに閉じる操作が、実機で私物の通知に当たりうる）、NU-13（通知センターの操作）、NU-14・15（背景） |

ランナーの変更（入れ直さない形、止まって待つ所、承認の一覧、途中で止まったときの片付け）は、この計画が決まった後に、UI の自動化の設計書 v4 の 4.6 の差分として書いてレビューに出す。

### 2.3 NU-11 の段

どの段も、**消す前に、消す対象が在ることを先に照らす**（空振りで通らないように）。予約は 60 秒後。一覧は id の集合と、各要素の形（`identifier`、`title`、`categoryIdentifier`、副題と本文の有無）を記録する。

1. 予約 `A`（副題と本文あり、category あり）と `B`（本文だけ、category なし）→ 予約の一覧が `{A, B}`。形を記録する
2. `UpdateNotification(A)`（題を変える）→ `{A, B}` のまま、`A` の題が新しい
3. `CancelScheduledNotification(A)` → `{B}`
4. `CancelAllScheduledNotifications` の完了の中で予約 `C` → `{C}`（M6）
5. `CancelNotification(C)` → `{}`。予約 `D`・`E` → `CancelAllNotifications` → `{}`
6. 即時の通知 `F`・`G` → 配信済みが `{F, G}` → `RemoveDeliveredNotification(F)` → `{G}` → `RemoveAllDeliveredNotifications` → `{}`

1.x の取り消しと消去には完了の受け手が無いので、各段の後に 1 秒待ってから一覧を読む。

## 3. テストの共通の約束

- Player のテストのカテゴリーは `IosPlayer`。名前は `IosNotificationPlayerTests.NP<番号>_<意味>`。予約の id は `ntkp-<番号>`。流す順は `[Order]`
- UI のテストの名前は `NotificationUITests/test_NU<番号>`。パイロット `test_permissionAndBannerTap` は NU-1 と NU-4 に分けて消す
- 通知の題は `NTK UI Harness`、**本文はテストごとに変える**（`ntk NU-4` など）。バナーと通知センターの通知は、題と本文の両方で 1 件に絞ってから押す。長押しの後と action を押す前に、展開した中身が自分の通知であることを確かめ、確かめられなければ何も押さない（native-toolkit の `Springboard.swift` の `expandBanner`）
- 予期しないアラートが残れば失敗（`HarnessUITestCase`）
- 通知の起動（NU-8）は `source=file` の起動の行を待つ別の待ち方を使う

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| 位置の trigger（`LocationTriggerPayload`）の発火 | 位置の許可と移動が要る。形の誤りは NP-8 で見る |
| 添付の画像の見た目 | 手で見る |
| アイコンのバッジの数の見た目 | springboard のアイコンを読むのは端末ごとに違う。`SetBadgeCount` の結果だけを見る |
| `Initialize` を入れ直しの口として呼ぶ（I-3） | 段 3 の写し方の設計で決めてから足す |
| 受け手の外し方と、古い登録の無効化（NT-20・21 の `2.0.0` の内側） | 1.x の公開の API に外す口が無い。段 3 の包みのテスト（EditMode か、ハーネスの差分）で見る |

## 5. 1.x の記録

記録は別の文書に書く（計画の版は上書きしない）。

## 6. v1 からの変更

| 指摘 | 変更 |
|---|---|
| R-1（A1）、Codex の R-01 | 予約・更新・取り消し・配信済みの消去を、許可の後の UI（NU-11、2.3）に移した。Player は未決の基準だけ。NP-4・5 の予想を M2 に合わせた。NP-5b を足した |
| R-2、Codex の R-06 | NP-9 は 1.x では流さず、`2.0.0` の後だけ有効にする |
| R-3、Codex の R-02 | NP-3・18 の予想を「B は 1 回」にした |
| R-4、Codex の R-04 | NU-2 の文言は 1.x の汎用の文。既知の差分の候補 |
| R-5、R-6、Codex の R-03 | NP-15 は後のフレーム。イベントは受け手の後 |
| R-7、Codex の R-05・R-09、R-12 | NP-8 を生の JSON の値の表（1.1）にした。NP-16 を (a) と (b) に分けた |
| R-8、Codex の R-10 | NU-1・2 で 3 つの API、NU-3b、NU-14・15 を足した。NU-10 は状態を必ず照らす |
| R-9、Codex の R-12 | NU-12 |
| R-10、Codex の R-14 | NU-13。4 章の誤りを消した |
| R-11、Codex の R-13・R-15 | NU-4 の userInfo、2.3 の一覧の形。JSON は解いて比べる |
| R-13、Codex の R-11 | NP-5 の配信済み、NP-5b、NU-2b |
| R-14、Codex の R-07 | `Open` は foreground。NU-6 は待ってから前に戻す |
| R-15 | 3 章（本文で絞る、展開した中身を確かめる、後片付け） |
| Codex の R-08、R-16（サブエージェント）| NU-7 を単独の流れにした |
| Codex の R-16 | 2.3 で個別と全体の取り消しを分けた |
| R-17、R-18 | `[Order]`、自分の id だけを見る、NP-14 は記録する。NU-8 の起動の行 |
