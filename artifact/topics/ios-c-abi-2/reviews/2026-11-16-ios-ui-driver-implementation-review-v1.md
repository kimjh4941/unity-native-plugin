# iOS の UI の手伝い役の実装のレビュー v1

- 記録日: 2026-11-16
- 対象: `78d1a33` の時点の実装の全体（差分ではない）。`DriverUITests.swift`、`IosUiDriver*.cs`、`IosSampleScreenDriver.cs`、4 つのサンプルのテスト、`verify_unity_ios.sh`、`ios_player_results.py`、`ios_ui_runner.py`
- 設計: [../designs/2026-11-15-ios-ui-driver-design-v3.md](../designs/2026-11-15-ios-ui-driver-design-v3.md)、記録: [../results/2026-11-15-ios-ui-driver-implementation-result-v1.md](../results/2026-11-15-ios-ui-driver-implementation-result-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）を並行で回した。どちらも読むだけ（実機の回が並行して走っていた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

不具合 2 / 改善 6 / 記録 3

ファイルは読むだけで、何も編集・実行していません。

R-1 [不具合] [観点 3 実機の決まり / 観点 4 テストの確かさ] 実機の Clipboard の見張りが、存在しないラベル名を読んでいる
- 根拠: `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSampleScreenDriver.cs:128` は `Q<Label>("StatusLabel")` を読んでいます。ところがサンプルの状態の表示は `StatusTextBlock` という名前です（`Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml:12`、`Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:306`）。テストの側は正しい名前を使っています（`IosClipboardSamplePlayerTests.cs:25`）。
- 壊れ方: 実機では `OnDevice` が true になり、見張りの `status` は常に null です。129 行目で null は「汎用」とみなされます。このため `CreateNamedPasteboardButton` で名前付きに切り替えた後でも、`CopyPlainTextButton` から先はすべて `Assert.Fail("... would act on the general pasteboard")` になります。CS01 と CS02 は実機で必ず失敗し、1 つ目の回が赤になります。止まる側に倒れるので汎用のペーストボードには触れませんが、見張りの 2 段目（名前付きの間は通す）は一度も働きません。シミュレーターでは見張りを通らないので、今まで気づけませんでした。実機の回は `e34c6fe` の 1 回だけで、Clipboard のテストはまだ入っていませんでした。
- 直し方の案: `"StatusTextBlock"` に直す。名前は定数にしてテストと共有する。ラベルが見つからない（null）ときは「汎用」とみなすのではなく、名前を出して `Assert.Fail` にする（名前違いがすぐ分かるように）。

R-2 [不具合] [観点 2 実行の仕組み] 2 つ目の回が、1 つ目の回の手伝い役の出力を消す
- 根拠: `scripts/verify_unity_ios.sh:348` の `rm -rf "$log" "$OUT_DIR/driver.xcresult" "$OUT_DIR/driver.json"` と、`:324` の `rm -rf "$OUT_DIR/uitests-dd"`。どちらも `RUN_SUFFIX` が付いていません。Player の側の名前には付いています（`:407-409`）。
- 壊れ方: シミュレーターの通常の回では、Notification のサンプルの回の `start_driver` が、1 つ目の回の `driver.log`・`driver.xcresult`・`driver.json` を消します。1 つ目の回で手伝い役が失敗し `see $OUT_DIR/driver.log` と出ても、最後に表示される「Logs and results」のフォルダーには 2 つ目の回のログしか残りません。あわせて `build_driver` が回ごとに同じ NtkUiTests を組み直すので、数分が無駄になります。もう 1 点、`DRIVER_PORT` を回の初めに空に戻していません（`:86` で初期化するだけ）。2 つ目の回で xcodebuild が最初の `kill -0` より前に落ちると、1 つ目の回の番号で「listening」と出ます（起きにくいですが、直すのは 1 行です）。
- 直し方の案: `driver$RUN_SUFFIX.log`・`.xcresult`・`.json` にする。`build_driver` は `DRIVER_XCTESTRUN` があれば飛ばす（組み立てを 1 回にする）。`start_driver` の初めで `DRIVER_PORT=""` にする。

R-3 [改善] [観点 1 正しさ] つなぎ直しの 5 秒の握手が、手伝い役が命令を処理している間に始まりうる
- 根拠: `IosUiDriver.cs:226,241` は、つなぐことと `READY` を待つことを合わせて 5 秒で打ち切ります。`Reconnect`（`:257-282`）は 1 回きりで、失敗すると `s_lost` が以後ずっと true のままです。手伝い役は 1 本の線で処理します（`DriverUITests.swift:61-113`）。古い接続の命令が終わるまで `accept` しません。
- 壊れ方: 設計 3.1 が想定するのは「Player が背景にいる間に OS が接続を閉じた」場合です。実際にそうなるのは `settings-close` の間だけです。そこで手伝い役が `activate()` すると、Player は戻ってすぐに切れた接続に気づき、つなぎ直します。ところが手伝い役は `settingsClose` の中で 3 秒待ち、状態を見て、最大 3 回くり返します（`:284-291`。1 回 13 秒）。その間に Player の 5 秒が過ぎると `s_lost` になります。NS07 と、その後のすべての依頼が失敗します。
- 直し方の案: つなぎ直しのときだけ `READY` の待ちを、処理中の命令の期限まで（例えば `ScreenAnswerTimeoutSeconds`）延ばす。あわせて、手伝い役の `accept` が失敗したときは `break` ではなく `continue` にする（`:62,346-348`。EINTR や接続の取り消しで手伝い役が丸ごと終わらないように）。

R-4 [改善] [観点 1 正しさ] `front()` は、信用できないと分かった `state` で近道する
- 根拠: `DriverUITests.swift:310-314` は `app.state == .runningForeground` なら `activate()` を呼びません。一方、同じファイルの `:273-276`（記録の 5 章）は、自分で起動していないアプリの `state` は、背景なのに「前」と読めることがあると書いています。
- 壊れ方: `screenCommand` の後片付け（`:234`）、`permissionAnswer`（`:268`）、`front` の命令は、Player が本当は背景にいても `ok` を返し、前に戻しません。後のテストは Unity が止まったまま待つことになります。`IosUiDriverConnectionTests` の `front` も、この近道を通るだけの確かめになっています。
- 直し方の案: `front()` は毎回 `activate()` する。「戻った」の判定は `settingsClose` と同じく、ほかのアプリが前にいないことで見る。

R-5 [改善] [観点 4 テストの確かさ / 観点 5 設計との食い違い] 画面の命令を使うテストに `[Timeout]` が無い
- 根拠: 設計 3.3 には「画面の命令を使うテストは、メソッドに `[Timeout]` を付ける（既定の 180 秒）」とあります。Android は付けています（`AndroidNotificationPlayerTests.cs:215` など）。iOS のサンプルのテストにはどれにも付いていません。最悪の時間は、Share の `Cancelled`（`IosShareSamplePlayerTests.cs:62-72`）と NS07（`IosNotificationSamplePlayerTests.cs:109-119`）がどちらも 30+10+120+10=170 秒で、ここに後片付けの `WaitForPending`（最大 120 秒）が加わります。
- 壊れ方: 遅い回では、本当の理由ではなく Test Framework の打ち切りで落ちます。打ち切られた依頼は `s_pending` に残り、次のテストの後片付けがそれを待つか、前のテストの失敗を次のテストのものとして報告します。この件は記録の 2 章に書かれていません。
- 直し方の案: Android と同じく、定数（例えば 300 秒）で `[Timeout]` を付ける。

R-6 [改善] [観点 4 テストの確かさ] SS15 は、つなぎ直しが起きたことを確かめていない
- 根拠: `IosShareSamplePlayerTests.cs:55-60` は `drop-next` の後に普通の `Cancelled` を流すだけです。`IosUiDriver` には、つなぎ直した回数を外から読む口がありません。
- 壊れ方: 手伝い役が切らなくても（例えば `drop-next` の処理が壊れても）、SS15 は緑になります。押し直しが起きないことは、2 回目の `sheet-close` が失敗することで間接的に確かめられています。しかし V-11 の前提である「切れて、つなぎ直した」はゲートで確かめられていません。
- 直し方の案: `IosUiDriver.ReconnectCount`（internal）を足し、SS15 で 1 増えたことを `Assert` する。

R-7 [改善] [観点 4 テストの確かさ] `alert-tap` が失敗するとアラートが開いたまま残り、後の Dialog のテストが連鎖して落ちる
- 根拠: `DriverUITests.swift:191-197` は、ボタンが 1 個に絞れなければ `fail not-found` を返します。アラートは閉じません。後片付け（`screenCommand`）が付くのは 60 秒の命令だけです（設計 3.2 のとおり）。Dialog のテストの後片付け（`IosDialogSamplePlayerTests.cs:24-29`）は、シーンを外すだけです。
- 壊れ方: 1 本の文言違いでアラートが残ると、次のテストのアラートは出せず、以後の ID は全部 `not-found` になります。1 本の失敗が 10 本の失敗に見えます。
- 直し方の案: `alert-tap` と `alert-type` も、失敗したら出ているアラートの Cancel 系のボタンを押して閉じる。または、Dialog の後片付けで「アラートが無いこと」を `alert-absent` で確かめる。

R-8 [改善] [観点 3 実機の決まり] 失敗時の後片付けが、springboard のどのアラートにも「許可しない」を押す
- 根拠: `DriverUITests.swift:232-233`。どの画面の命令の失敗でも、`springboard.alerts` の「Don't Allow / 許可しない」を押します。`permissionAnswer` も、springboard の最初のアラートを許可のダイアログとみなします（`:258`）。
- 壊れ方: 実機で、たまたま別の許可のダイアログ（例えば V-7 の位置の許可）が出ていると、手伝い役が勝手に答えます。承認の表示（`ios_ui_runner.py:201`）は「通知の許可に Allow で答える」としか言っていません。
- 直し方の案: 後片付けは `permission-answer` の失敗のときだけにする。押す前に、ダイアログの文言にアプリの表示名が含まれることを照らす（照らした文言は出さない）。

R-9 [記録] [観点 3 実機の決まり] 承認の表示と DeviceRequirements の説明が、実際にすることと合っていない
- 根拠: `ios_ui_runner.py:201` の playertests の説明には、ID08（`IosDialogSamplePlayerTests.cs:59-62`。実機でも流れる）が端末のキーボードで `ntk42` を打つことが書かれていません。UI テストの側（`appdialog`）には書かれています。`Tools~/IosUiTests/DeviceRequirements.json:3` の `about` は、いまだに「playertests: ... (no system screen; ... Notification without asking for the permission)」のままです。
- 直し方の案: playertests の文に「Dialog のサンプルのテキスト入力のアラートに、端末のキーボードで試験の値を打つ（パスワード欄のあるアラートには打たない）」を足す。`about` も書き直す。

R-10 [記録] [観点 3 実機の決まり] 「スクリーンショットを残さない」は、コードではなく後片付けで守られている
- 根拠: `DriverUITests.swift:18-19` には「no screenshot」と書かれています。ところが `verify_unity_ios.sh:355` は `-resultBundlePath` に xcresult を書き、XCUITest の自動のスクリーンショットは、失敗した回では残ります（`test_ios_ui.sh:34-35` 自身が「result bundles (which hold screenshots of the device)」と書いています）。実機では OUT_DIR ごと消すので、守れてはいます。
- 直し方の案: コメントを「実機では結果の束ごと一時フォルダーで消す」と正しく書く。設計の D-9 も同じ。

R-11 [記録] [観点 5 設計との食い違い] 記録の 2 章に無い食い違い
- 合図の行の待ち: 設計 4.1 の 3 は 60 秒、実装（`verify_unity_ios.sh:357`）は 120 秒
- `settings-close` の期限: 設計 3.2 は 60 秒。実装（`DriverUITests.swift:280-291`）は最悪 30+5+3+3×13=77 秒（失敗時はさらに後片付け）。C# の待ちは 120 秒なので足りています
- `background`: 設計 3.2 は「背景になったことを確かめてから ok を書き、その後待って戻す」。実装（`:300-308`）は、前に戻した後で答えます
- 設計 6 章「サンプルを使わない `IosUiDriver` のテストは、どの回にも入る」: `IosUiDriverConnectionTests` は `IosPlayer` だけなので、Notification のサンプルの回には入りません
- 設計 4.3 は Player の上限の見積もり直しを求めています。`PLAYER_LIMIT=600`（`:84`）のままで、記録にも触れていません
- 細かい点: `IosUiDriverProtocolTests.cs:59` の `"9 alert-type 1 YWJj"` は、引数 2 つの今は使わない形です（手伝い役は 3 つを求めます）。`IosUiDriver.cs`・`IosUiDriverProtocol.cs` のコメントは「design v2」を指したままです

確かめて問題が無かったところ: 行の区切り、部分的な読み取り、`HELLO` から `READY` までの扱い（`s_handshaking`）、覚えた答えの出し直し、閉じたソケットの見分け（`Poll` と `Available == 0`）、`WaitForPending` での失敗の集め方、`BYE` と Player を止める順（`.done` の後に 60 秒）、`.done` が来ないときに手伝い役を止めること、`on_exit` の `stop_driver`、3 つのアプリの後片付けと読み戻し（シミュレーターと実機の両方）、手伝い役を起こす前後の `device_guard`、`--require` と `--allow-skip` と、選び方で外したテストの数え方。

判定: 作りの骨組みと、行のやり取りの扱いはおおむね正しいです。ただ R-1 のため、実機では Clipboard のサンプルのテストが必ず失敗します。次の実機の回の前に、R-1 と R-2 を直すべきです。

## 2. Codex

不具合 7 / 改善 2 / 記録 2

HEAD `78d1a33` の対象実装全体を確認しました。ファイル変更・ビルド・端末テストの再実行はありません。初回の `git` 照会では macOS の開発ツール初期化が呼ばれました。Python の書き込み不要な既存例53件は成功しています。以下の異常系は、コード上の条件と入力例に基づきます。

1. **C-1｜不具合：実機の Clipboard テストが名前付き対象でも拒否される**
   根拠：/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSampleScreenDriver.cs:128、同 Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml:12。
   ガードは `StatusLabel` を検索しますが、実際の名前は `StatusTextBlock` です。常に `null` → general と判定され、`CreateNamedPasteboardButton` 成功後の `CopyPlainTextButton` なども失敗します。CS01・CS02 は実機で完走できません。
   修正：ラベル名を一致させ、general では拒否・named では許可の両方を検証する。

2. **C-2｜不具合：長い命令の処理中に切断すると再接続できない**
   根拠：Tools~/IosUiTests/NtkUiTests/DriverUITests.swift:79、IosUiDriver.cs:241。
   例えば `sheet-absent 30000` の処理開始直後に切断すると、Swift は30秒の処理が終わるまで次の接続を受け付けません。C# は再接続の `READY` を5秒で諦め、`s_lost` を固定します。要求の応答期限が残っていても復旧しません。`drop-next` は処理後に切るため、この経路を検証できません。
   修正：処理中の再接続を受け付けるか、元の要求期限内で接続・ハンドシェイクを再試行する。

3. **C-3｜不具合：再接続を繰り返すと応答期限を検査しない**
   根拠：IosUiDriver.cs:121。
   相手が毎回 `READY` を返した後、要求への応答前に切断する状態では、再接続成功後の `continue` が期限検査を飛ばします。指定した30秒・120秒を超えて待ち続け、外側のテスト制限に依存します。
   修正：再接続経路でも期限を検査し、再接続自体の待ちも残り時間に制限する。

4. **C-4｜不具合：末尾改行を含む入力が行の検証を通る**
   根拠：IosUiDriverProtocol.cs:41。
   正規表現の `$` は末尾の `\n` の直前にも一致します。例えば `Request(1, "front\n", …)` は拒否されず、送信すると通常の `front` と空行になります。run ID に末尾改行を付けた場合も検証を通り、`HELLO` が分断されます（.NET の anchors の仕様どおり）。
   修正：文字列全体を `\A…\z` で検証し、末尾改行の拒否テストを追加する。

5. **C-5｜不具合：2回目の起動失敗で前回のポートを再利用する**
   根拠：scripts/verify_unity_ios.sh:345。
   `start_driver` は `DRIVER_PORT` を初期化しません。Notification 用の2回目で子プロセスが最初の `kill -0` より前に終了すると、ループを通らず、前回のポートが残ったまま起動成功を返します。Player は終了済みの driver のポートへ接続します。
   修正：起動ごとにポートを空にし、その回の合図を取得できたこととプロセスの生存を確認する。

6. **C-6｜不具合：driver の終了条件を判定に反映していない**
   根拠：scripts/verify_unity_ios.sh:380、同:392。
   `wait` の終了コードを捨てるため、成功したテスト結果を保存した後に `xcodebuild` が異常終了しても driver 判定は成功します。また `had_done=0` でも、既に終了済みなら結果判定へ進み、成功を返せます。後者は通常 Player 側の結果不足で全体が失敗しますが、「`.done` なしなら driver も失敗」という契約を満たしません。
   修正：`.done` 未取得と非ゼロ終了を明示的な失敗条件にし、子プロセスは全経路で回収する。

7. **C-7｜不具合：重複したテスト結果で失敗が上書きされる**
   根拠：scripts/ios_ui_runner.py:324。
   同じ `DriverUITests/test_drive()` が `Failed`、`Passed` の順に並ぶ JSON を渡すと、辞書の上書きで成功になります。この入力で `judge_results` が `True` を返すことを確認しました。「ちょうど1件、成功」の判定になっていません。
   修正：識別子ごとの出現数を保持し、重複または失敗を検出したら失敗にする。

8. **C-8｜改善：Unity メインスレッドの送信待ちにも上限を設ける**
   根拠：IosUiDriver.cs:342。
   `Write` は同期実行で、`WriteTimeout` は未設定です。相手が読まず送信バッファが埋まると、コルーチンの期限検査へ戻れません。NetworkStream.WriteTimeout の既定は無期限です。現在の少数要求で発生した証拠はないため、改善とします。
   修正案：期限付きの非同期送信にする。

9. **C-9｜改善：除外件数だけでなく、実行予定のケースを照合する**
   根拠：scripts/ios_player_results.py:137。
   `testcasecount=110` で接続テスト1件だけが成功した XML は、`judge` と `--require IosUiDriver` 相当の両方を通り、残り109件をすべてフィルター除外と表示します。正当な除外と実行漏れを区別できません。
   修正案：Profile・実機除外条件から実行予定ケース一覧を作り、結果と照合する。単純な件数一致へ戻すだけでは、正当な除外も失敗させます。

10. **C-10｜記録：切断検出・BYE の順序・終了時処理は基本経路を満たす**
    根拠：IosUiDriver.cs:292、scripts/verify_unity_ios.sh:498、同:564。
    単一受信者での `Poll(0, SelectRead) && Available == 0` は通常の切断検出として妥当です。`Poll` 単独を切断扱いする実装にはなっていません。両実行経路とも driver 終了待ち後に Player を停止します。EXIT・INT・TERM・HUP からの driver 停止と3アプリの削除経路、実機での background・banner・secure field 入力の拒否も確認しました。
    対応：これら自体の修正は不要。異常系は C-2・C-3・C-5・C-6 を修正する。

11. **C-11｜記録：実装記録の未実装記述が古い**
    根拠：/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/results/2026-11-15-ios-ui-driver-implementation-result-v1.md:27。
    Notification の別実行を「まだ作っていない」としていますが、実装と後続の検証表には存在します。残作業の読み取りが食い違います。
    修正：別実行は実装済み、配置検査は未実装として記述を更新する。

判定：要修正。特に実機 Clipboard の実行阻害と、再接続・終了判定の不具合を解消する必要があります。

## 3. 判断（こちら）

サブエージェントは不具合 2 / 改善 6 / 記録 3、Codex は不具合 7 / 改善 2 / 記録 2。2 人とも R-1・C-1（実機の Clipboard の見張りのラベル名）を挙げ、並行して流していた実機の回で、そのとおり CS01・CS02 が落ちた（汎用のペーストボードには触れていない）。

| 指摘 | 判断 | 直したところ |
|---|---|---|
| R-1・C-1 ラベル名 | 直した | `IosSampleScreenDriver.ClipboardStatusLabel = "StatusTextBlock"`（テストと共有）。ラベルが無ければ「汎用」とみなさず、名前を出して落とす |
| R-2・C-5 手伝い役の出力とポート | 直した | `driver$RUN_SUFFIX.*`、組み立ては 1 回、`start_driver` の初めに `DRIVER_PORT=""`、合図の後にプロセスが生きていることも見る |
| C-6 手伝い役の終わり方 | 直した | `.done` が無い回は手伝い役も失敗。xcodebuild の終了コードが 0 でなければ失敗 |
| C-7 結果の重複 | 直した | `judge_results` は 1 つのテストが 2 回以上あれば失敗（doctest を足した） |
| C-4 行末 | 直した | 正規表現を `\A…\z` に。末尾の改行のテストを 4 つ足した |
| R-3・C-2・C-3 つなぎ直し | 直した | つなぎ直しは依頼の待ちの期限まで繰り返す（1 回 5 秒、合間 1 秒）。期限はつなぎ直しの道でも毎回見る。手伝い役の `accept` の失敗で終わらない |
| C-8 書き込みの上限 | 直した | `WriteTimeout = 5000` |
| R-4 `front()` の近道 | 直した | 毎回 `activate()` |
| R-5 `[Timeout]` | 直した | 画面の命令を使うテスト（Share の 15 本、NS02・NS07）に 300 秒 |
| R-6 SS15 | 直した | `IosUiDriver.ReconnectCount` が 1 増えたことを照らす |
| R-7 アラートが残る | 直した | `alert-tap`・`alert-type` の失敗では、最後のボタン（サンプルでは取り消しの側）を押して閉じる |
| R-8 springboard のアラート | 直した | 「許可しない」で答えるのは `permission-answer` のときだけ。答える前に、ダイアログの文言にプレイヤーの表示名が入っていることを照らす（照らした文言は出さない）。表示名は、スクリプトがプレイヤーの `Info.plist` から読んで `TEST_RUNNER_NTK_DRIVER_APP_NAME` で渡す（最初は `XCUIApplication.label` を読んだが、許可のダイアログが出ている間は `Failed to get matching snapshot` で手伝い役のテストが落ちた） |
| R-9 承認の表示 | 直した | `playertests` の文にテキスト入力の打ち込みを足した。`DeviceRequirements.json` の `about` も書き直した |
| R-10 スクリーンショット | 直した | コメントを「XCTest が失敗の結果の束に入れうる。実機では一時フォルダーごと消す」に |
| R-11 設計との食い違い | 記録に書いた | 実装の記録の 2 章（合図の待ち 120 秒、`settings-close` の最悪 77 秒、`background` の答えの時点、Player の上限 600 秒のまま）。プロトコルのテストの古い形は直した。つなぐテストは Notification の回にも入れた |
| C-9 外した分と流れなかった分の見分け | 残す | 実装の記録の 3 章へ（流すはずのテストの一覧と照らす形は、Profile と実機の外し方を写す必要があり、今は件数の表示に留める） |
| C-10・C-11 | — | C-10 は確かめの結果。C-11 は実装の記録を直した |

直した後の確かめ（2026-11-16、シミュレーター）: EditMode 1625 件（成功 1624、前からの skip 1）、Editor の PlayMode 391 件、Player の 1 つ目の回 103 件、Notification のサンプルの回 8 件（つなぐテストを含む）がすべて成功。手伝い役は 2 つの回とも成功。SS15 で、手伝い役が答えの前に切り、つなぎ直した後に覚えた答えを返したこと（接続 2 本）をログで確かめた
