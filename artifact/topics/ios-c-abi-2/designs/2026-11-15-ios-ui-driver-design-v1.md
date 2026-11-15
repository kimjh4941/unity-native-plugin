# iOS の Player のテストからシステムの画面を押す手伝い役（XCUITest）v1

## 基本情報

- 記録日: 2026-11-15
- 親: [../README.md](../README.md) の段 4、[段 4 の計画 v1](2026-11-15-ios-c-abi-stage4-plan-v1.md) の項目 1
- 手本: Android の [android-ui-driver-design-v15](../../android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v15.md)（`scripts/android_ui_driver.py`、`Tests/PlayMode/AndroidUiDriver.cs`）
- 使い回すもの: 段 2 の UI の自動化（[ios-ui-automation-design-v4](2026-11-14-ios-ui-automation-design-v4.md)。`Tools~/IosUiTests`、`scripts/test_ios_ui.sh`、`scripts/ios_ui_runner.py`）、Player のテストの仕組み（`scripts/verify_unity_ios.sh`、`Assets/NativeToolkitIosPlayerTests`）
- 利用者の判断（2026-11-15）: iOS のサンプルの画面の UI テストを Android と同じ深さにする。工数はかかるが、この手伝い役を作る。レビューが 3 回で収まらないときは、残った A1 を見て続けてよい（`agent-rules/workflows/review-document/workflow.md` のサーキットブレーカー）

## 1. 何を作るか

Unity の Player のテスト（Unity Test Framework の PlayMode。iOS の Player の中で走る）が、サンプルのシーンのボタンを C# から押し、それで開いたシステムの画面（アラート、共有のシート、許可のダイアログ、通知のバナー、設定の画面）を、XCUITest の手伝い役に押してもらう。

```
Player のテスト（アプリ: Player のバンドル ID）           手伝い役（XCUITest のランナー: DriverUITests.test_drive）
                                                         127.0.0.1:<port> で待つ
  TCP でつなぐ ──────────────────────────────────────▶
  HELLO <run> <bundle id>                        ─────▶  bundle id を照らす。XCUIApplication(bundleIdentifier:) を作る
                                                 ◀─────  READY <run>
  7 alert-tap <Base64 の文言>                     ─────▶  アラートを待ち、ボタンを押す
                                                 ◀─────  7 ok
  ...
  BYE <run>（結果を書いた後）                     ─────▶  答えて終わる（テストのメソッドが成功で戻る）
```

- **通り道は 127.0.0.1 の TCP**（2 章の D-1）。手伝い役が待ち、Player がつなぐ。ポートはスクリプトが決めて、両方の環境変数で渡す
- **境目は命令**（Android の設計書の「境目を固定する」と同じ）。テストは命令だけを使い、XCUITest を知らない。手伝い役は Unity を知らない
- 手伝い役は、Player を起動しない（`launch()` を呼ばない）。スクリプトが起動した Player に、バンドル ID で付く
- **サンプルのボタンは、今の Android・Windows と同じく `NavigationSubmitEvent` で押す**（`AndroidSampleScreenDriver.Press`）。当たり判定・レイアウト・本当のタッチは自動では見ない（9 章の手の確認表で見る）

## 2. 決めること

| # | 決めること | 決定 | 理由 |
|---|---|---|---|
| D-1 | 依頼と答えの通り道 | **127.0.0.1 の TCP**。手伝い役（XCUITest のランナーのプロセス）が `NWListener` で待ち、Player の C#（`System.Net.Sockets.TcpClient`）がつなぐ。1 本の接続に、行で依頼と答えを流す | シミュレーターでも実機でも同じ形（どちらも同じ機械〈端末〉の上の 2 つのプロセス）。ファイルの写し（`simctl` / `devicectl device copy`）に頼らない。答えを待つのにポーリングが要らない。loopback は Local Network の許可の対象外の見込み（V-1） |
| D-2 | どちらが待つか | **手伝い役が待つ**。Player がつなぐ | 手伝い役はスクリプトが先に起こしておける。Player のテストは「つながらなければ手伝い役がいない」と分かり、Android と同じく `Assert.Ignore` にできる（Editor の Test Runner から流したとき） |
| D-3 | ポートの決め方 | スクリプトが実行ごとに 49152〜65535 から選び、手伝い役には `TEST_RUNNER_NTK_DRIVER_PORT`（xcodebuild が `TEST_RUNNER_` を外してランナーに渡す）、Player には `NTK_DRIVER_PORT`（シミュレーターは `SIMCTL_CHILD_`、実機は `devicectl --environment-variables`。今の `NTK_RUN` と同じ渡し方）で渡す | 起動の引数は Player に届かない（README の「段 2 のスパイクの結果」）。環境変数は届く |
| D-4 | 手伝い役の終わり方 | Player の結果の書き手（`IosPlayerResultWriter`）が、結果を書いた後に `BYE` を送る（つながっていなければ、つないでから送る）。手伝い役は `BYE` で成功として戻る。来なければ、上限（4.3）で失敗として戻る | 手伝い役の成否を、xcodebuild のテストの結果（`.xcresult` の 1 件）で判定できる。スクリプトが xcodebuild を殺して終わらせると、成否が分からない |
| D-5 | どのテストの実行に手伝い役を付けるか | `verify_unity_ios.sh` の Player の実行（シミュレーターと実機）の**すべて**。Test iOS Profile の実行（`--as-package-user`）にも付ける | Android の 7 章と同じ。サンプルのシーンが無い Profile では、サンプル画面のテストは Ignore になり、判定から外す（6 章） |
| D-6 | 依頼を先に出すか | **Android と同じく、`Send` で先に依頼を出し、システムの画面を開き、`Wait` で答えを待つ**。開いた後に出す形にしない | 許可のダイアログの間は Unity が止まる（NU-10 の調べ、2026-11-15）。止まっている間はテストが依頼を出せない。アラートと共有のシートで止まるかは V-4 で測るが、どちらでも正しい形にする |
| D-7 | 手伝い役のコードの置き場所 | `Tools~/IosUiTests/NtkUiTests/DriverUITests.swift`（新）。押し方は今の `SystemUI.swift` を使い、足りない押し方を足す。ハーネスの UI テストと同じターゲット（`NtkUiTests`）で、`-only-testing:NtkUiTests/DriverUITests/test_drive` で選ぶ | 署名・組み立て・実機の承認の仕組み（`test_ios_ui.sh`、`ios_ui_runner.py`）をそのまま使える。システムの画面の押し方を 1 か所に保つ |
| D-8 | テストの側の置き場所 | `Tests/PlayMode/IosUiDriver.cs`（依頼の口）、`Tests/PlayMode/IosSampleScreenDriver.cs`（サンプルのシーンを開き、ボタンを押し、表示を読む）。どちらも `#if UNITY_IOS && !UNITY_EDITOR` | Android の 2 つのファイルと同じ分け方。機能ごと・OS ごとのファイルの決まり（`agent-rules/coding-rules/common.md`） |
| D-9 | 実機の決まり | 今の実機の回の決まりをすべて当てる（7 章）。手伝い役は画面の木を書き出さず、スクリーンショットを残さない。端末の設定を変えない（Android の補助は設定を変えるが、iOS の手伝い役は変えない） | 利用者の指示（端末の設定を変えない、ほかのアプリの通知を読まない、汎用のペーストボードに触らない） |

## 3. 依頼の形

### 3.1 行

UTF-8 の 1 行（`\n` で終わる）。1 行は 4096 バイトまで（超えたらテストの側で `ArgumentException`）。

```
HELLO <run> <bundle id>                  Player → 手伝い役（つないだ直後に 1 回）
READY <run>                              手伝い役 → Player
<番号> <命令> <引数 1> <引数 2> ...        Player → 手伝い役
<番号> ok | <番号> ok <値> | <番号> fail <理由>   手伝い役 → Player
BYE <run>                                Player → 手伝い役（実行の終わり）
```

- `<run>` は Player が起動ごとに作る GUID（32 桁の小文字の 16 進）。スクリプトが渡す `NTK_RUN` とは別（`NTK_RUN` は結果のファイルの名前）
- `<bundle id>` は手伝い役の引数（`TEST_RUNNER_NTK_DRIVER_BUNDLE`）と同じでなければ、手伝い役は接続を閉じる（ほかのアプリが同じポートにつないでも、押さない）
- `<番号>` は run の中で 1 から増やす整数
- 文字列の引数は Base64 の UTF-8。命令の表（3.2）で「生」と書いたものはそのまま（英数字と `-` だけ）
- 手伝い役は依頼を受けた順に 1 つずつ処理する。答えは同じ接続で返す
- 理由は英数字と `-` だけの決まった語（3.4）。画面から読んだ文字を理由に入れない

### 3.2 命令

サンプルの 4 つの画面が開くシステムの画面から決めた。足すときは、この形（`<命令> <引数>`、答えは `ok` / `fail <理由>`）を守る。

| 命令 | 引数 | すること | 期限 |
|---|---|---|---|
| `alert-tap` | `<ボタンの文言>` | アプリのアラート（`app.alerts` か、action sheet の `app.sheets`）が出るのを待ち、文言が同じボタンを押す。押した後、アラートが消えるのを待つ | 10 秒 |
| `alert-type` | `<欄の番号>`（生。0〜3）、`<文字列>` | アラートの中の、番号目の入力の欄（`textFields` と `secureTextFields` を上から数える）を押して、文字列を入れる（`typeText`）。文字列は ASCII の印字できる文字の 1〜64 文字 | 10 秒 |
| `alert-absent` | `<ミリ秒>`（生。1000〜30000） | 指定の時間、アラートが出ないことを確かめる。出たら、取り消しのボタン（`.cancel` の役割。無ければ最後のボタン）を押して閉じ、`fail alert-opened` | 指定の時間 + 10 秒 |
| `sheet-close` | なし | 共有のシートが出るのを待ち、閉じる（`SystemUI.closeShareSheet`）。開いたことと閉じたことの両方を確かめる | 60 秒 |
| `sheet-absent` | `<ミリ秒>`（生。1000〜30000） | 指定の時間、共有のシートが出ないことを確かめる。出たら閉じて `fail sheet-opened` | 指定の時間 + 60 秒 |
| `permission-answer` | `allow` か `deny`（生） | springboard の通知の許可のダイアログが出るのを待ち、答える（今の `answerPermission` の文言の一覧）。ダイアログが消え、Player が前面に戻るのを待つ | 60 秒 |
| `settings-close` | なし | 設定のアプリ（`com.apple.Preferences`）が前面に来るのを待ち、Player を前面に戻す（`activate()`）。来たかどうかを `ok opened` / `ok not-opened` で返す（期待はテストが決める） | 60 秒 |
| `banner-tap` | `<題名>` | 題名が同じ通知のバナーを押す（`SystemUI.tapBanner`）。Player が前面に来るのを待つ | 60 秒 |
| `background` | `<ミリ秒>`（生。100〜5000） | ホームに戻し（`XCUIDevice.shared.press(.home)`）、Player が `runningBackground` になったことを確かめてから `ok` を書く。そのあと指定の時間待ち、Player を前面に戻す（戻せなければ手伝い役の終わりに失敗を記録する） | 10 秒 |
| `front` | なし | Player を前面に戻す（`activate()`）。前面に来たことを確かめる | 10 秒 |

- 押す相手のアプリは、HELLO の `<bundle id>` の `XCUIApplication`（アラートと共有のシートはアプリのプロセスの中に出る）と springboard（許可のダイアログ、バナー）。設定のアプリは前面に来たかだけを見る
- 共有のシートの中身（ほかのアプリの共有先、連絡先）は読まない。閉じるのに要る部品（閉じるボタン、外側）だけを探す（`SystemUI` の今の形）
- バナーは、テストが決めた固有の題名（`NTK-IS-<番号>-<run の先頭 8 桁>`）でだけ探す。ほかのアプリの通知の文字を読んで照らすことはしない（7 章）
- 期限の 60 秒の命令（画面の命令）は、失敗・期限切れのどれでも、出ている画面を閉じて Player を前面に戻してから `fail` を返す（Android の 3.3 と同じ考え。開いたまま残すと、後のテストが止まる）

### 3.3 テストの側の待ち

| 状況 | テスト |
|---|---|
| `NTK_DRIVER_PORT` が無い、つながらない（5 秒）、`READY` が 5 秒来ない | 手伝い役がいないとみなし、そのテストを `Assert.Ignore`。以後の依頼も Ignore |
| `READY` の後、答えが来ない | `Assert.Fail("ui driver did not answer")`。待ちは、期限 10 秒の命令は 30 秒、60 秒の命令は 120 秒（`ScreenAnswerTimeoutSeconds`）、`-absent` の命令は指定の時間 + その命令の待ち |
| `fail ...` | `Assert.Fail` |
| 接続が切れた | `Assert.Fail("ui driver disconnected")`。以後の依頼も Fail |

- 待ちの間、テストのコルーチンは毎フレーム、ソケットに届いた分を読む（`Socket.Available`）。Unity が止まっている間に届いた答えは、OS の受け口に溜まり、戻ってから読む
- 画面の命令を使うテストは、メソッドに `[Timeout]` を付ける（Android の 5 章の v15 と同じ。既定の 180 秒で打ち切られるため）

### 3.4 理由の語

`timeout`、`not-found`、`did-not-close`、`not-in-front`、`alert-opened`、`sheet-opened`、`sheet-did-not-open`、`dialog-did-not-open`、`bad-arg`、`not-background`、`error`（予期しない例外。中身は手伝い役の標準出力にも出さない）。

## 4. 動かし方

### 4.1 `verify_unity_ios.sh` の Player の実行

今の流れ（書き出し → xcodebuild → インストール → 起動 → 結果のファイルを待つ → 止める）に、次を足す。

1. 書き出しの前に、`NtkUiTests` を build-for-testing する（`test_ios_ui.sh` と同じ手順を `ios_script_common.sh` に移して共有する）
2. ポートを選ぶ（D-3）
3. Player を入れた後、起動の前に、手伝い役を背景で起こす: `xcodebuild test-without-building -xctestrun <...> -only-testing:NtkUiTests/DriverUITests/test_drive -destination <同じ端末>`、環境変数 `TEST_RUNNER_NTK_DRIVER_PORT`・`TEST_RUNNER_NTK_DRIVER_BUNDLE`・`TEST_RUNNER_NTK_DRIVER_LIMIT`（秒）。手伝い役が待ち始めたことは、ランナーの標準出力の 1 行（`[NTK-UI-DRIVER] listening <port>`）で見る（30 秒。来なければ実行を失敗にする）
4. Player を起動する（`NTK_DRIVER_PORT` を足す）
5. 結果のファイルを待つ（今のまま）。Player を止めた後、手伝い役の xcodebuild が終わるのを待つ（60 秒。`BYE` を受けていれば数秒で終わる）
6. 手伝い役の判定（6 章）

### 4.2 Player の側の流れ

- 最初の依頼の前に 1 回つなぎ、`HELLO` を送って `READY` を待つ
- サンプル画面のテストは、`IosSampleScreenDriver.LoadAtTopMenu` → `Open` → `Press(<ボタン>)` の前に、そのボタンが開く画面の命令を `Send` しておく
- `IosPlayerResultWriter` は、結果を書いた後に `BYE` を送る（つながっていれば同じ接続、なければ新しくつないで。つながらなければ何もしない）

### 4.3 時間の上限

- 手伝い役の全体の上限: `TEST_RUNNER_NTK_DRIVER_LIMIT`（スクリプトの Player の実行の上限と同じ値）。`BYE` が来ないまま過ぎたら、テストのメソッドを失敗で終える
- XCTest の実行時間の上限（`executionTimeAllowance`）は使わない（既定で無効。xcodebuild に `-test-timeouts-enabled NO` を明示する）
- 1 つの命令の期限は 3.2 の表。手伝い役の中の待ち（`waitForExistence` など）は、残りの期限を上限にする

## 5. テストの側の口（`IosUiDriver`）

Android の `AndroidUiDriver` と同じ名前と意味にする（読む人が 1 つの形を覚えればよい）。

- `IEnumerator Do(string command, params string[] args)`: 依頼を出して答えを待つ。答えは `LastAnswer`、値は `LastValue`
- `IEnumerator Send(string command, params string[] args)` と `IEnumerator Wait(int number, float timeoutSeconds)`: 先に出して後で待つ（D-6）。`LastSent` に番号
- `IEnumerator WaitForPending()`: まだ待っていない依頼をすべて待つ（後片付け）。途中の `fail` でも残りを回収してから、まとめて `Assert.Fail`
- `const string Category = "IosUiDriver"`、`const string SampleScreenCategory = "IosSampleScreen"`（6 章の判定に使う）
- 依頼の行の組み立てと答えの読み方は、Editor でも組める純粋な関数に分け、EditMode のテストで固める（Android は PlayMode のアセンブリにあって層 1 のテストが無かった。iOS はここを足す）

## 6. ゲートでの判定

`verify_unity_ios.sh` の Player の実行ごとに、次のどれかなら失敗にする。

- 手伝い役の xcodebuild の結果が成功でない（`ios_ui_runner.py results` で、`DriverUITests/test_drive` がちょうど 1 件、成功）
- 結果の XML のカテゴリ `IosUiDriver` のテストに skip がある（手伝い役に届かなかった UI のテストを緑にしない）
- カテゴリ `IosUiDriver` のテストが 1 本も無い
- Test iOS Profile の実行（`--as-package-user`）では、`IosSampleScreen` のテストを上の 2 つの判定から外す（サンプルのシーンが無いので Ignore になる。Android の 7 章と同じ）

## 7. 実機での決まり

今の実機の回の決まり（`--dry-run` の 4 点と承認 ID、端末の識別子・team を出さない、ログと結果の束を残さない、終わりに入れたアプリを消す）に加えて:

- `--dry-run` の表示に、手伝い役（XCUITest のランナー）が入ること、押すシステムの画面の一覧（3.2 の命令のうち、その回のテストが使うもの）を書く
- 手伝い役は、画面の木（`debugDescription`）とスクリーンショットを出さない・残さない。失敗のときも、理由の語だけを返す
- バナーは固有の題名でだけ探す。通知センターは開かない
- **汎用のペーストボードに触るサンプルのボタン（Clipboard の `UseGeneralButton` の範囲）は、実機の回では押さない**。テストはカテゴリ `IosGeneralPasteboard` を付け、実機の回ではそのカテゴリを流さない（`-testCategory` の否定。Android の保持のテストで使った形）。シミュレーターでは流す
- 端末の設定は変えない。許可は `permission-answer` で答える（Android の `pm grant` にあたるものは使わない）

## 8. 選ばなかった案

| 案 | 選ばなかった理由 |
|---|---|
| アプリの領域のファイルで依頼と答えを渡す（Android の形） | 実機では、ホストがアプリの領域に書くのに `devicectl device copy to` が要り、1 回が数秒かかる。XCUITest のランナーは別のアプリで、Player の領域に書けない |
| 依頼をアプリの隠しのラベル（アクセシビリティの要素）で出し、答えを隠しの入力欄に打ち込む | ラベルを出すための `.mm` が Player に要る。入力欄に打つとキーボードが出て、アラートの入力の欄のテストとぶつかる。ラベルの読み取りは XCUITest の木の読み直しで遅い |
| Player と同じアプリの中から押す（UIKit の API でボタンを押す） | springboard（許可のダイアログ、バナー）と設定のアプリに届かない |
| WebDriverAgent・Appium | 導入と版の固定が要る。この用途（押す、待つ）には過ぎる。今の XCUITest の仕組みで足りる |
| AltTester など、Unity の UI を外から押すツール | サンプルのボタンは C# から押せば足りる（Android・Windows と同じ）。システムの画面は押せない |
| Player が待ち、手伝い役がつなぐ | 手伝い役の側に「Player が待ち始めるまで繰り返す」処理が要る。Player が手伝い役の有無を知れない |

## 9. 手の確認表（自動では見ないもの）

Android・Windows では、当たり判定・レイアウト・本当のタッチの決まった確かめが無い（マニュアルの撮影のときに実機で触っただけ）。iOS はセーフエリア（ノッチ、ホームインジケーター）で要素が隠れやすいので、短い表を置く（利用者への推奨、2026-11-15）。

- リリースの前に実機で 1 回。4 つの画面とトップのメニューについて: すべてのボタンが見えて押せる、セーフエリアに隠れない、縦と横、スクロールの端まで届く
- 結果はリリースの記録に書く

## 10. 要検証（スパイクで先に確かめる）

| # | 内容 | 確かめ方 | 通らなければ |
|---|---|---|---|
| V-1 | Player（C# の `TcpClient`）から、XCUITest のランナーが `NWListener` で待つ 127.0.0.1 のポートにつなげるか。**シミュレーターと実機の両方**。実機で Local Network の許可のダイアログが出ないか | 最小の Player（つないで `HELLO` を送るだけのテスト 1 本）と最小の手伝い役 | 実機だけ通らなければ、ランナーの側から Player につなぐ形（Player が待つ）を試す。それも通らなければ、8 章の隠しのラベルの案に戻る |
| V-2 | `XCUIApplication(bundleIdentifier:)` が、`simctl launch` / `devicectl` で起動済みの Player に、起動し直さずに付けるか。付いた後、`app.alerts` が Player のアラートを読めるか | V-1 と同じ回で、Player に 1.x と同じアラートを出させる | 手伝い役が `launch()` で Player を起こし、スクリプトの起動をやめる形を考える（環境変数は `launchEnvironment` で渡す） |
| V-3 | 手伝い役のランナーが 30 分以上、1 つのテストのメソッドの中で待ち続けられるか（xcodebuild・XCTest の打ち切り、端末の画面ロック） | 手伝い役を 35 分待たせる | テストの実行を機能ごとに分け、手伝い役もその単位で起こす |
| V-4 | アラート・共有のシート・許可のダイアログ・設定の画面・バナーのそれぞれで、Unity のフレームが止まるか | 各画面を出して、`Time.frameCount` の進みを記録 | 止まらない画面でも D-6 の形は正しいので、記録だけ |
| V-5 | 手伝い役がいる間、Player のテスト（キーボード、フォーカス、アプリの状態）に影響が無いか。XCUITest のランナーが前面を取らないか | V-1 の回で、ランナーを起こしてから Player を起動し、Player が前面のままか | 起動の順を入れ替える |
| V-6 | iOS Profile の Player（Test Framework の Player）に、サンプルのシーンが入り、`LoadSceneAsync` で読めるか | 最小の Player のテストで `Application.CanStreamedLevelBeLoaded` | Test Framework の Player の組み立てで、シーンを足す口を探す |
| V-7 | 位置のトリガー（`ShowLocationButton`、`ScheduleLocationButton`）が、位置の許可を求めるか | Notification のサンプルのテストの最初 | 求めるなら、その 2 つのボタンは手の確認に回す（位置の許可は端末の設定の扱いに近いので、手伝い役では答えない） |

## 11. 進め方

1. この設計のレビュー（サブエージェントと Codex）
2. スパイク: V-1〜V-3、V-5、V-6 をシミュレーターで（実機は利用者が端末を用意できるとき）
3. 実装: 手伝い役、`IosUiDriver`、スクリプト、EditMode の行の組み立てのテスト
4. 最初の利用者: Dialog のサンプル（`ShowDialogButton` → `alert-tap` → 結果の表示）。続けて Share、Notification、Clipboard
5. 実機の回（承認を得て）
6. 実装のレビュー
