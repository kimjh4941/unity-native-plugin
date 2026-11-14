# iOS Notification 自動テスト計画 v1 のレビュー v1

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-notification-ui-test-plan-v1.md](../designs/2026-11-14-ios-notification-ui-test-plan-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

要約: 一番大きいのは R-1（A1）です。Player のテストは許可が未決のまま流すので、1.x でも予約は pending に残りません（native-toolkit の実測 M2）。このため NP-10 は失敗し、NP-11・NP-17 は取り消しが壊れていても通ってしまいます。ほかに、1.x の予想の誤りが 6 件（NP-3・9・15・16・8 と NU-2 の文言）、中心の変換の抜けが 4 件（NOT_FOREGROUND、category、DISMISS、userInfo と一覧の JSON の形）あります。

対象: /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/notification/designs/2026-11-14-ios-notification-ui-test-plan-v1.md（以下「計画」）。native-toolkit の資料は part2 = `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md`、1.x のソースはタグ `1.13.0` を読みました。

### R-1 [A1] 観点(1)(2): 未決の Player では pending が常に 0 になり、NP-10・11・17 が中身を確かめていない
- 根拠: part2 0.2 の M2（69 行）。権限が無いと `add` は error なしで成功し、`getPending` は 0。`notDetermined` の即時の分だけ配信済みに載る。シミュレーター 18.3 と 26.2 で同じ結果。P2-10（37 行）と stage2c-handoff 216 行も「今は黙って成功し、pending に残らない」。計画 19 行は許可を一度も求めない前提で、NP-4（26 行）は「予約は積まれる」と予想、NP-10（32 行）は「同じ id が 1 つ」と期待、NP-11（33 行）は「消える」、NP-17（39 行）は「落ちない」だけ。
- 壊れ方:
  - NP-11 は、消す前から 0 件なので 1.x でも 2.0.0 でも通ります（`2.0.0` は `PERMISSION_DENIED` で何も足さない）。段 3 の包みが `CancelScheduledNotification` を `remove_async`（配信済みを消す）に写し間違えても気づけません。NT-05〜10 は検査されません。
  - NP-10 は 1.x で失敗し、2.0.0 では `PERMISSION_DENIED` になります。2.0.0 の列「同じ」は誤りです。
  - `update` が `PERMISSION_DENIED` のときに前の要求を消さないこと（6.3）も見えません。
- 直し案:
  - pending と配信済みを使う項目（NP-4 の一覧、NP-10、NP-11、NP-17）を、ハーネスで NU-1 の許可の後に流す形に移します（実機では NU-4・5・7 と同じ回）。
  - 消す前に「count=1」を先に照らし、空振りで通らないようにします。
  - cancelAll の直後の schedule の順（M6、ヘッダーの "add new requests after the callback"）も足します。
  - Player に残すのは「未決のときの基準」だけにします。NP-4・5 は 1.x の予想を「成功、pending 0、即時の分は配信済みにある」に直し、2.0.0 の列は `PERMISSION_DENIED`（2c で写し方を決める）にします。

### R-2 [A2] 観点(1)(3): NP-9 は落ちることが既に測られており、落ちると Player の回ごと結果を失う
- 根拠: part2 M5（72 行）で `.timeInterval(0,false)` と `(30,true)` が落ちる。M8（75 行）で -1、59.9 repeats、NaN が落ちる。1.x の `NotificationRepositoryImpl.swift` 255 行がそのまま `UNTimeIntervalNotificationTrigger` を作る。結果の XML は `IosPlayerResultWriter.RunFinished` でしか書かれない。`verify_unity_ios.sh` は 337〜345 行で、落ちたら「no result」で回全体を失敗にする。実機では 600 秒待ち（372〜386 行）、承認も使い切る。計画 31 行は「落ちれば外す。NP の最後に流す」。
- 壊れ方:
  - 順番を最後にしても効きません。結果は最後にまとめて書かれるので、NP の全件も、後に流れる `IosSharePlayerTests` などほかの機能の Player の結果も消えます。
  - 実機では 10 分を失い、承認を取り直すことになります。
- 直し案:
  - 1.x では流しません。1.x の列は「落ちる（M5・M8 の実測）」と書きます。
  - テストは 2.0.0 の後だけ有効にします（`[Explicit]` か define）。
  - repeats で 60 未満、NaN、-∞ を 2.0.0 の期待（`INVALID_PARAMETER`）に足します。

### R-3 [A2] 観点(1): NP-3・NP-18 の「B は 2 回」は誤りで、1 回になる
- 根拠: `IosNotificationManager.cs` 652〜653 行（`cb = s_onHasPermission; s_onHasPermission = null;`）と 478〜479 行（Show も同じ形）。最初の完了で B が呼ばれて static が null になり、2 回目の完了には受け手がいません。一方、イベントは 485 行で毎回出るので、NP-18 の `NotificationOperationCompleted` は 2 回です。static はロックなしで背景のスレッドから読み書きされるので、競合すれば B が 2 回になることもあります。
- 壊れ方: 予想どおりの断言を書くと、1.x の記録の段階で失敗します。
- 直し案: 1.x の予想を「A 0 回、B 1 回（競合で 2 もありうる）。NP-18 はイベントが 2 回」にします。

### R-4 [A2] 観点(1)(2): NU-2 の文言は 1.13.0 には無く、2.0.0 の列の「同じ」も誤り
- 根拠: 1.13.0 の `NotificationError.swift` には `LocalizedError` が無いので、`localizedDescription` は汎用の文になります（`NotificationPermissionHelper.swift` 33〜35 行で投げる）。stage1b-working-notes 1417 行も「helper が permissionDenied を投げ、(false, <汎用の文>)」。"Notification permission was denied." は 1b で入った文言です（native-toolkit README 248 行、作業ツリーの `NotificationError.swift:77`）。part2 の NT-13 の「今の文言に写す」の「今」は 1b を指していて、出荷した 1.x ではありません。
- 壊れ方: 1.x の記録は `The operation couldn’t be completed. (IosLibrary.NotificationError error <n>.)` の形になり、移行後の比べでは文言が変わります。
- 直し案: 1.x の予想を汎用の文にし、2.0.0 の列を「既知の差分の候補: 1.x の汎用の文を保つか、新しい文言にするかを 2c で決める」にします。

### R-5 [A2] 観点(1)(2): NP-15 の「入口の中で呼ばれる」は C# では起きない
- 根拠: ネイティブの完了は入口の中で来ます（`UnityIosNotificationManager.swift` 200〜201 行）。しかし C# の `OnRegisterCategoryResult` は `UnityMainThreadDispatcher.Enqueue` に積むだけで（`IosNotificationManager.cs` 580 行）、実行されるのは次の `Update` です（`UnityMainThreadDispatcher.cs` 55〜61 行）。しかも完了の時点では、category はまだ書かれていません（1.x の `IosNotificationManager.swift` 310 行の `Task`）。
- 壊れ方: 予想は外れます。2.0.0 の列の「入口の中のコールバックは保たない（既知の差分）」も、C# からは見えない差です。NT-17 の本題（書き込みと読み返しの後に完了）は、この項目では確かめられません。
- 直し案: 予想を「後のフレーム（Dispatcher の Update）」に直し、何フレーム目かを記録します。NT-17 の本題は R-9 の UI の項目で見ます。

### R-6 [A2] 観点(1): 42 行の「受け手より前に」は順序が逆
- 根拠: `IosNotificationManager.cs` 484〜485 行。受け手が先に呼ばれ、イベントはその後です。どちらも同じ `try` の中にあるので、受け手が例外を投げるとイベントは出ません。
- 直し案: 「受け手の後に、ちょうど 1 回」に直します。NP-18 は 2 回（R-3）。

### R-7 [A2] 観点(1): NP-8・NP-16 の「どれも成功／捨てて成功」の根拠が一次ソースと合わない
- 根拠:
  - M4（71 行）: `https://` の添付は `addRequestFailed`（失敗）。
  - M5: 空の `id` は成功するが何も残らない。項目の無い calendar は成功するが pending 0。空のアクションも登録される。
  - Schedule は `content.id` を使わない（`UnityIosNotificationManager.swift` 59〜67 行、Repo 51〜55 行）。一方 2.0.0 は `_content_create` で空の id を拒む（6.3）。
  - 1.x のパーサーが捨てるのは、キーが無いか型の違う要素だけで、空文字の id は残す（`UnityIosNotificationJsonParser.swift` 161〜172 行）。
- 壊れ方:
  - 「file:// でない添付」は 1.x でも失敗です。差は文言だけになります。
  - 「空の id」は、Schedule で試すと 1.x では何も確かめていません。ただし 2.0.0 の Schedule・Update でも失敗に変わるので、既知の差分として表に要ります。
  - NP-16 の空の id は捨てられません。成功は同じなので、Player からは違いが見えません。
- 直し案:
  - 空の id は「Show の `content.id`」「Schedule の `identifier`」「Schedule の `content.id`」に分けます。
  - 添付の予想は失敗に直します（未決のときの結果は記録する）。
  - NP-16 は、実際に捨てられる要素（`title` の無いアクション）と、空の id の 2 つにします。

### R-8 [B 中心契約] 観点(2)(3): 背景での NOT_FOREGROUND（NT-13・NT-15）が無い
- 根拠: NT-13 は「未決で前面でなければ NOT_FOREGROUND」、NT-15 は「前面でなければ NOT_FOREGROUND」。ヘッダーにも "Needs the foreground only while the status is undetermined" と "Needs the foreground" とあります。計画の NU-10（61 行）は Control Center（前面で非活性）だけです。ハーネスには `OnBackground` がある（`IosUiHarness.cs` 94〜99 行、Share の SH-18）ので、背景で呼ぶことは出来ます。native-toolkit の `CApiFreshInstallUITests.swift` 41〜44 行、N42 も同じ場面を見ています。
- 直し案: シミュレーターだけで次の 2 つを足します。
  - 未決のまま背景で `RequestPermission`（1.x は記録。ダイアログが出るか、完了が来るか）
  - 背景で `OpenNotificationSettings`（1.x は黙って止まる見込み）
  - 2.0.0 の列は、2c の写し方を待つ形にします。

### R-9 [B 中心契約] 観点(2): NT-17 の category の変化を見る項目が無い
- 根拠: NT-17 は「書き込みと 1 回の読み返しの後に完了」「不正な要素は INVALID_PARAMETER」。1b で「同じ識別子の登録は置き換える」ようになった（README 248 行。1.x は `Set.insert` だけで `RegisterCategory` を重ねても置き換わらない。Repo 175〜177 行）。C# には category を読む口が無く、Player からは何も見えません。
- 直し案: UI で次の 2 つを見ます。
  - category X を `[A]` で登録し、次に `[B]` で登録し、X の通知を長押しして、出るアクションを記録する
  - 登録の受け手の中ですぐ即時の通知を出し、アクションが出るかを記録する

### R-10 [B 中心契約] 観点(2): 4 章の「1.x には custom dismiss の口が無い」は誤り（DISMISS が抜ける）
- 根拠: 1.x のパーサーは `customDismissAction` を読み（189 行）、Repo は `.customDismissAction` を付けます（291 行）。消去は text input でない応答なので、`onActionReceived` に `com.apple.UNNotificationDismissActionIdentifier` で来ます（1.x の `IosNotificationManager.swift` 345〜349 行）。NT-20 では `_kind` が DISMISS のものも「今のコールバックへ」写す対象です。native-toolkit の `clearEventInNotificationCenter` で、シミュレーターでの操作は確かめられています（`CApiRoundTripUITests.swift` 694 行）。
- 直し案: 4 章の行を消し、シミュレーターだけの NU を足します（custom dismiss つきの category、通知センターで消去、ハーネスを前面に戻して `action=` を 1 本）。

### R-11 [B 中心契約] 観点(2): userInfo と一覧の JSON の形を、どこにも記録しない
- 根拠:
  - イベントの `UserInfoJson`: 1.x は `JSONSerialization` で作る（`UnityIosNotificationManager.swift` 220〜223 行）。2.0.0 は `_user_info_json` から予約のキーの印を外して作る（6.3）。
  - 予約の一覧: `categoryIdentifier` は常に出し、空の subtitle と body は省く（同ファイル 121〜129 行。NT-11「今の JSON を作る」）。
  - 計画のタップの行（55〜57 行）は id と action だけで、予約の一覧の JSON を中身ありで記録する項目はありません。
- 直し案:
  - ハーネスの通知に `userInfo {"k":1}` を入れ、行に `userInfo=` を出します。
  - 許可の後に予約の一覧の行を足します。
  - 比べるときは JSON を解いてから比べます（`JSONSerialization` のキーの順は決まっていない）。

### R-12 [B] 観点(2): NT-02 の「今は通って C ではエラーになる値」の一部が NP-8 に無い
- 根拠: NT-02 の表と 6.3 には、範囲外の location、空の location の識別子、入ると出るの両方が知らせない組、repeats で 60 未満、NaN、未知の `interruptionLevel`（1.x は `.active` にする。パーサー 58 行）、userInfo の JSON の `null`、最上位の予約のキー、未知の option の文字列がある。計画 89 行は「Player で location の形の誤りを見る」と書いているが、NP-8 に location の項目が無い。
- 直し案: NP-8 に足します。落ちるものは R-2 と同じく 2.0.0 の後だけにします。

### R-13 [B] 観点(2): PERMISSION_DENIED の基準が `notDetermined` だけ
- 根拠: M2 は `denied` も同じ（成功して何も残らない）。P2-10 も `denied` を対象にしている。2c に渡す「完全には同じにならない」（`notDetermined` の即時の分だけ配信済みに載る）の材料になるのに、NP-5 は配信済みを読んでいない。
- 直し案:
  - NP-5 で `GetDeliveredNotifications` を記録します（1.x は載る見込み）。
  - NU-2 の後に Show と Schedule を呼び、結果と一覧を記録します（シミュレーター）。

### R-14 [A2] 観点(3): 背景のアクションの後に、ハーネスを前面に戻す手順が無い
- 根拠: 1.x は text input のアクションの options を常に `[]`（背景）にします（Repo 283 行）。背景の間は Unity の Update が止まり、結果の行は前面に戻るまで出ません。native-toolkit はアクションの後に `app.activate()` している（`CApiRoundTripUITests.swift` 686 行）。part2 6.3 の末尾には「背景のアクションの直後に前面に出すと `_is_launch` で来ることがある」とある。計画の NU-5 は `Open` に `foreground` を付けるかを決めていない。
- 壊れ方: NU-6 は、前面に戻さなければ 0 本で失敗します。2.0.0 では、2c が「保った分を捨てる」と決めると、すぐ前面に戻した回だけ揺れます。
- 直し案:
  - `Open` を `foreground` にするかを表に書きます。
  - Reply の後は N 秒待ってから activate します。
  - 2.0.0 の列に `_is_launch` の経路を書きます。

### R-15 [B] 観点(4): 実機でバナーを見分ける手がかりと、長押しの守りが足りない
- 根拠:
  - 通知の題 `NTK UI Harness` はアプリの表示名と同じで、`SystemUI.tapBanner` は `label CONTAINS` と `firstMatch` で探す（`SystemUI.swift` 67〜73 行）。
  - 2.2 は入れ直さずに NU-4・5・7 を続けるので、前のテストの通知が残る（NU-7 はわざと残す）。
  - 計画には UI のテストの後片付けの決まりが無い（80〜83 行）。
  - native-toolkit は実機で、題と本文で 1 件に絞り、展開した中身を確かめてから押し、確かめられなければ何も押さない（`Springboard.swift` の `expandBanner` と `expandedScope`、レビュー R-21）。
- 壊れ方: 残っていた古いバナーを押します。長押しの前にバナーが消えると、長押しが利用者のホーム画面に落ち、アイコンのメニューや編集の状態になります。
- 直し案:
  - NU ごとに本文を変え、題と本文で 1 件に絞ります。
  - 長押しと、アクションを押す前に、展開した中身が自分の通知かを確かめます。
  - teardown で `CancelAll` と `RemoveAllDelivered` を呼びます（native-toolkit の `removeTheNotificationsAtTheEnd`）。
  - 途中で止まったときもハーネスを消すことを、ランナーの差分に書きます。

### R-16 [C] 観点(1): NU-7 の文が食い違っている
- 根拠: 58 行は「NU-4 の後」なのに「押さない」とある。NU-4 はバナーを押すので、通知は通知センターから消える見込み。
- 直し案: 別の流れにします。3 秒後の通知を前面のまま受け（`willPresent` の `.list`）、配信済みを読みます。

### R-17 [C] 観点(3): NP-12・NP-14 と流す順
- 根拠と直し案:
  - NP-12 の「空の一覧」は、ほかのテストが即時に出した通知（M2 で配信済みに載る）の後片付けが、待たずに終わる `Task` なので（1.x の `IosNotificationManager.swift` 192〜201 行）、揺れます。消えるのを待つか、自分の id だけを見ます。
  - NP-14 の「成功」は、未決でのバッジが測られていないので「記録する」にします。
  - 流す順は `[Order]` で明示します。

### R-18 [C] 観点(3): NU-8 の起動の形
- 根拠: 通知から起こしたときは `source=file` になる（`IosUiHarness.cs` 25 行）が、`HarnessUITestCase.launch` は `source=env` を待つ（47 行）。
- 直し案:
  - 別の待ち方を用意します（native-toolkit の `testATapThatStartsTheAppReachesCAsTheLaunch` の形）。
  - 2.0.0 の列に「`.mm` が起動の中で delegate を入れる（NT-01）ことが前提」と書きます。

### 件数
- A1: 1（R-1）
- A2: 7（R-2、R-3、R-4、R-5、R-6、R-7、R-14）
- B: 7。うち B 中心契約が 4（R-8、R-9、R-10、R-11）、ほかの B が 3（R-12、R-13、R-15）
- C: 3（R-16、R-17、R-18）

ファイルは編集していません。
## 2. Codex

読み取りのみで確認しました。実機・シミュレーターは実行していません。

以下、[計画](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/notification/designs/2026-11-14-ios-notification-ui-test-plan-v1.md)、[CS](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationManager.cs)、[D2](/Users/jonghyunkim/Desktop/native-toolkit/artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md)、[H](/Users/jonghyunkim/Desktop/native-toolkit/dist/1.14.0/ios/include/NativeToolkitC/Notification.h)を参照略号とします。タグの根拠はすべて `git show 1.13.0:<path>` で確認しました。

1. **A2 / R-01 — 許可状態と、予約を必要とするテストが両立していない**

   計画:19 は Player を常に未許可としますが、NP-10 は更新できる予約、NP-11 は取り消せる予約を必要とします（計画:32–33）。2.0.0 は未許可の show・schedule・update をすべて拒否するため、この前提では成立しません（H:231–252）。シミュレーターも毎テスト入れ直す一方、NU-5〜8 に許可の前処理がありません（計画:48、56–59）。**修正:** 未許可の Player 検証と、許可済みの UI 検証を分け、更新・取消の成功検証を後者へ移してください。2.2 の実機では、各テストが状態を照会し、許可済みなら許可アラートを待たない共通前処理を定義します。

2. **A2 / R-02 — NP-3・18 の「B が2回」は C# の保持方法と一致しない**

   計画:25、40 は A=0・B=2 と予測しますが、CS:478–479、652–653 はネイティブから最初の結果が来た時点で受け手を取り出し、static を null にします。両要求が重なっていれば A=0・B=1 となり、次の結果に受け手はありません。また「同じフレーム」は、最初のネイティブ完了が2回目の呼び出しより後になる保証ではありません。**修正:** 「両方が未完了の状態」を作る方法を定義し、その条件下の予測を A=0・B=1 に変更してください。条件を制御しない場合は競合として実測し、B=2 を固定期待にしないでください。

3. **A2 / R-03 — NP-15 の同期性と、完了イベントの順序が逆**

   NP-15 は C# の受け手が入口内で呼ばれると予測し、計画:42 は操作イベントが受け手より先とします。しかし CS:574–585 はカテゴリー登録結果を dispatcher に積み、その中で受け手→イベントの順に呼びます。他の操作も同順です（CS:474–505）。タグの Swift ブリッジで同期完了することとは別です（`1.13.0:ios/UnityIosPlugin/UnityIosPlugin/Notification/UnityIosNotificationManager.swift:194–201`）。**修正:** 公開 C# の期待を「入口から戻った後」「受け手→イベント」に直し、ネイティブ内部の時機とは分けて記録してください。

4. **A2 / R-04 — NU-2 の固定エラー文言は出荷タグにない**

   計画:53 は `"Notification permission was denied."` を1.x の期待にしますが、タグの helper は `NotificationError.permissionDenied` を投げ、Manager は `error.localizedDescription` を渡します。タグの enum は `Error` のみで、独自の説明文を定義していません（`1.13.0:ios/IosLibrary/IosLibrary/Notification/Presentation/Permission/NotificationPermissionHelper.swift:30–35`、同 `IosNotificationManager.swift:276–282`、同 `Domain/Model/NotificationError.swift:9–27`）。**修正:** 1.x の実際の文言を採取してください。2.0.0 の文言を変更するなら、「同じ」ではなく既知の差分として扱います。

5. **A2 / R-05 — NP-16 の空 action ID は「捨てる要素」になっていない**

   計画:38 の予測に反し、タグの parser は文字列の存在だけを確認し、空文字を持つ action を残します（`1.13.0:ios/UnityIosPlugin/UnityIosPlugin/Notification/UnityIosNotificationJsonParser.swift:160–171`）。一方、C# Builder を使うと空 ID は JSON 化する前に除去されます（[IosNotificationJsonBuilder.cs:143](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationJsonBuilder.cs:143)）。後者では2.0.0 に不正入力が届きません。**修正:** 生 JSON で「必須フィールド欠落」と「空文字 ID」を別々に渡し、1.x の破棄と2.0.0 の拒否を区別してください。

6. **A2 / R-06 — NP-9 を最後に置いても、通常テストの結果を保全できない**

   計画:31 はクラッシュを通常の Player 実行内で記録する形ですが、結果 XML と `.done` は全体の `RunFinished` でしか書かれません（[IosPlayerResultWriter.cs:34](/Users/jonghyunkim/Desktop/unity-native-plugin/Assets/NativeToolkitIosPlayerTests/Runtime/IosPlayerResultWriter.cs:34)）。途中で落ちると、ランナーは「結果なし」で失敗します（[verify_unity_ios.sh:337](/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_ios.sh:337)）。タグは無検証で `UNTimeIntervalNotificationTrigger` を生成します（`1.13.0:ios/IosLibrary/IosLibrary/Notification/Data/Repository/NotificationRepositoryImpl.swift:252–255`）。**修正:** 1.x の危険入力はシミュレーターの別プロセスで1件ずつ実行し、異常終了を別途記録してください。2.0.0 の「落ちずに失敗する」回帰テストは残します。

7. **A2 / R-07 — NU-5・6 に前面復帰の手順が足りない**

   計画:56–57 は action・返信操作から直ちにハーネスの結果を期待しますが、タグの返信 action は `options: []` です（`1.13.0:ios/IosLibrary/IosLibrary/Notification/Data/Repository/NotificationRepositoryImpl.swift:279–286`）。C# の結果は dispatcher の Update 待ちです（CS:684–692）。既存の native-toolkit のテストも、返信送信後に明示的に `activate()` しています（[CApiRoundTripUITests.swift:681](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApiUITests/CApiRoundTripUITests.swift:681)）。**修正:** Open に `foreground` を指定し、Reply は送信後にハーネスを前面へ戻してから待ってください。長押し・返信欄・送信ボタンの特定方法も実装項目に明記します。

8. **A2 / R-08 — NU-7 の操作列が NU-4 と矛盾する**

   NU-4 はバナーを押しますが、NU-7 は「NU-4 の後」としながら「バナーを押さずに残す」としています（計画:55、58）。さらに各テストは再インストールで独立します（計画:48）。このままでは NU-7 の検証用通知を作る操作列が一意になりません。**修正:** NU-7 単独で許可→予約→ホーム→配信待ち→バナーを押さずハーネスを前面化→配信済み照会、と定義してください。

9. **B 中心契約 / R-09 — NT-02 の検証変更を覆い切れていない**

   NP-8・9 と除外表には、未知の `interruptionLevel`、壊れた添付要素、入れ子の `userInfo:null`、予約キー、位置 trigger の各制約、繰り返し interval の60秒境界が具体化されていません（計画:30–31、89）。これらは D2:342–374、558 の主要な入力変換契約です。また `badge=-1` は C では未設定の意味を持つため、「負の badge」だけでは値が曖昧です（H:324–325）。**修正:** 生 JSON と正確な値を列挙し、境界の正常値も対にしてください。位置は発火不要の入力検証として追加し、`content.id` と schedule/update の `identifier` も区別します。

10. **B 中心契約 / R-10 — NT-13・14・19 の状態変換と背景条件が不足**

    NP-1・2・3 は未決状態しか確認せず、NU-3 は許可後の再要求だけです（計画:23–25、54）。拒否後の再要求、許可・拒否後の status/HasPermission、背景での要求がありません。NU-10 の Control Center は背景とは別で、2.0.0 の前面判定は `foregroundInactive` も受け付けます（[ForegroundLocator.swift:51](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Common/Presentation/ForegroundLocator.swift:51)、D2:384–386）。**修正:** 許可・拒否それぞれで3 API を照合し、未決の背景要求と、決定済みの背景要求を追加してください。NU-10 は非活性の検証として呼出時状態を必須記録にします。

11. **B 中心契約 / R-11 — PERMISSION_DENIED の副作用を確認していない**

    NP-4・5 は結果中心で、Update の未許可経路と、拒否済み状態がありません（計画:26–27、32）。契約は3操作とも追加しないこと、Update は既存予約を消さないことです（D2:379–381）。また1.x の未決・即時通知は配信済みに載る場合が記録されています（D2:69）。**修正:** 未決・拒否済み×3操作について、結果と pending/delivered の前後を確認してください。C# が成功へ写す場合でも、副作用の差分を独立して記録します。

12. **B 中心契約 / R-12 — NT-17 の書き込み完了とカテゴリー保持が未検証**

    NP-15 はコールバック時機、NP-17 は落ちないことだけで、カテゴリー登録・削除の効果を確認しません（計画:37–39）。C# のフレーム差だけでは、C の完了が書き込み・読み返し後であることも検証できません（H:291–298）。**修正:** ハーネス専用の観測点で書き込み・読み返し・完了の順を検証し、カテゴリー A/B の連続登録、A の削除後も B が使えることを追加してください。読み返しに存在しなくても成功となる契約とは区別します（D2:389）。

13. **B 中心契約 / R-13 — NT-20/21 のデータ変換と登録寿命が未検証**

    NU-4〜6 は ID・action・返信文字列のみで、公開結果の `UserInfoJson` を比較しません（計画:55–57、CS:668–687）。また2.0.0 で必要な登録解除・古い登録の無効化も対象がありません（D2:576–577）。NU-8 の0件判定も、起動と購読準備が完了したことを確認しなければ理由を区別できません。**修正:** 入れ子・配列・日本語を含む固定 userInfo の往復、受け手の解除・再生成後の重複なしを追加してください。NU-8 は `[launch@ok] ... source=file` と購読準備完了を確認してから件数を測ります（[IosUiHarness.cs:142](/Users/jonghyunkim/Desktop/unity-native-plugin/Assets/NativeToolkitIosUiHarness/Runtime/IosUiHarness.cs:142)）。

14. **B 中心契約 / R-14 — dismiss を除外する根拠が誤っている**

    計画:92 は「1.x に custom dismiss の口が無い」としますが、タグの parser は `customDismissAction` を受け取り、Repository は OS の同 option に変換します（`1.13.0:ios/UnityIosPlugin/UnityIosPlugin/Notification/UnityIosNotificationJsonParser.swift:187–192`、`1.13.0:ios/IosLibrary/IosLibrary/Notification/Data/Repository/NotificationRepositoryImpl.swift:290–299`）。dismiss は2.0.0 でも通常 action 側へ振り分ける対象です（D2:400、576）。**修正:** シミュレーターで custom dismiss を登録し、自分の通知だけを消して通常イベント1回・返信イベント0回を確認してください。

15. **B 中心契約 / R-15 — 一覧ハンドルから旧 JSON への再構築を十分に比較していない**

    計画:10、32、34、58 の比較は主に ID・題・時刻です。しかし旧 JSON は `identifier/title/categoryIdentifier` に加え、副題・本文の有無を持ちます（`1.13.0:ios/UnityIosPlugin/UnityIosPlugin/Notification/UnityIosNotificationManager.swift:120–153`）。2.0.0 は個別 reader からこれを再構築します（H:376–392）。**修正:** 空一覧だけでなく、複数件・副題本文あり/なし・カテゴリーあり/なしを構造比較し、省略と null、時刻形式、userInfo/trigger を追加しないことまで固定してください。

16. **B / R-16 — 取消・消去が no-op でも通るケースがある**

    NP-17 は配信済みを事前に作らず「落ちない」だけで、NP-11 も複数の取消操作それぞれの事前条件がありません（計画:33、39）。タグでは個別削除と全削除は別の呼び出しです（`1.13.0:ios/IosLibrary/IosLibrary/Notification/Data/Repository/NotificationRepositoryImpl.swift:83–110`）。空の状態や最後の全削除だけでは、個別操作の誤配線を検出できません。**修正:** 各 API を独立させ、通知 A/B を作って個別操作では A だけ消えること、全操作では両方消えることを確認してください。

**件数: A1＝0、A2＝8、B 中心契約＝7、B その他＝1、C＝0。合計16件。**
## 3. 判断

A1 が 1 件（サブエージェントの R-1。Codex の R-01 と同じ件）あるので v2 を作り、2 回目のレビュー（差分だけ）に出す。

- **A1（R-1、Codex の R-01）**: 予約・更新・取り消し・配信済みの消去を見る項目（v1 の NP-10・11・17 と NP-4 の一覧）は、許可を得た後の UI のハーネスに移す（v2 の NU-11）。消す前に数が 1 以上であることを先に照らす。`cancelAll` の完了の中で足した予約が残ること（M6）も足す。Player には「未決のときの基準」（成功、一覧は 0 件、即時の分は配信済みに載る）だけを残す
- **中心契約の B（直す）**:
  - R-8、Codex の R-10（背景と状態の変換）: 未決の背景での `RequestPermission`、背景での `OpenNotificationSettings`、拒否の後の再要求、許可と拒否のそれぞれで 3 つの API（状態、`HasPermission`、再要求）を足す
  - R-9、Codex の R-12（category）: 同じ識別子の登録を重ねたときと、消した後に別の category が使えることを UI で見る
  - R-10、Codex の R-14（dismiss）: 4 章の誤りを消し、custom dismiss の項目をシミュレーターに足す
  - R-11、Codex の R-13・R-15（userInfo と一覧の JSON）: 入れ子・配列・日本語を含む userInfo を往復させ、JSON を解いて比べる。予約の一覧は、副題と本文のある・無し、category のある・無しを並べて形を記録する
  - R-13、Codex の R-11（PERMISSION_DENIED の副作用）: 未決（Player）と拒否（UI）のそれぞれで、Show・Schedule・Update の結果と、一覧の前後を記録する
  - R-12、Codex の R-09（NT-02 の値）: 生の JSON で値を並べる。`badge` は C で「未設定」を意味する `-1` を避けて `-2` にする
- **A2 だが、予想や手順を直すだけで済むので直す**: R-2・Codex の R-06（NP-9 は 1.x では流さず、`2.0.0` の後だけ有効にする）、R-3・Codex の R-02（B は 1 回）、R-4・Codex の R-04（NU-2 の文言は汎用の文。既知の差分の候補）、R-5・R-6・Codex の R-03（受け手は後のフレーム、受け手の後にイベント）、R-7・Codex の R-05（生の JSON で、欠けた要素と空の id を分ける）、R-14・Codex の R-07（`Open` は foreground、Reply の後は前に戻す）、Codex の R-08 と R-16（NU-7 を単独の流れに）
- **B（実機の安全）**: R-15 は、本文をテストごとに変えて題と本文で 1 件に絞る、長押しと押す前に展開した中身を確かめる、終わりに予約と配信済みを消す、を 3 章に書く。Codex の R-16（個別と全体の取り消しの区別）は NU-11 に入れる
- **C**: R-17（`[Order]`、NP-12 は自分の id だけ、NP-14 は記録する）、R-18（NU-8 の起動の行）は取り込む
