# iOS の Player のテストからシステムの画面を押す手伝い役（XCUITest）v2

## 基本情報

- 記録日: 2026-11-15
- 親: [../README.md](../README.md) の段 4、[段 4 の計画 v1](2026-11-15-ios-c-abi-stage4-plan-v1.md) の項目 1
- 手本: Android の [android-ui-driver-design-v15](../../android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v15.md)（`scripts/android_ui_driver.py`、`Tests/PlayMode/AndroidUiDriver.cs`）
- 使い回すもの: 段 2 の UI の自動化（[ios-ui-automation-design-v4](2026-11-14-ios-ui-automation-design-v4.md)。`Tools~/IosUiTests`、`scripts/test_ios_ui.sh`、`scripts/ios_ui_runner.py`）、Player のテストの仕組み（`scripts/verify_unity_ios.sh`、`Assets/NativeToolkitIosPlayerTests`）
- 利用者の判断（2026-11-15）: iOS のサンプルの画面の UI テストを Android と同じ深さにする。工数はかかるが、この手伝い役を作る。配置（セーフエリア、当たり判定、縦と横）も自動で照らし、手で見るのは見た目だけにする（9 章）。レビューが 3 回で収まらないときは、残った A1 を見て続けてよい（`agent-rules/workflows/review-document/workflow.md` のサーキットブレーカー）

## v1 からの主な変更（v2。レビュー v1 とスパイクを受けて）

- **実機で使わない操作**（7 章。S-1）: `background`、ログインと秘密の入力欄への `alert-type`、`banner-tap` は実機の回で使わない。テストはカテゴリ `IosSimulatorOnly` で外し、手伝い役も実機では断る
- **Clipboard のサンプルの実機の守り**（7 章。S-2）: 開いた直後の対象は汎用のペーストボード。実機では最初に名前付きの対象に切り替え、`IosSampleScreenDriver` が「実機・Clipboard の画面・対象が汎用」のときは押す前に止める
- **端末に入る 3 つのアプリと背景の xcodebuild の後片付け**（4.1、4.4。S-3）
- **通知の許可の状態**（4.5。S-6、Codex の C-3）: 許可は 1 回の入れ方につき 1 回しか聞けない。Notification のサンプルのテストは、入れ直した別の Player の回で流す
- **`BYE` と Player を止める順**（4.3。Codex の C-4、S-5）: スクリプトは結果のファイルの後、手伝い役が終わるのを待ってから Player を止める。`BYE` はパッケージの側の Run の終わりの受け手が送る
- **つなぎ直し**（3.1。S-7）: 接続が切れたら、Player は同じ run の `HELLO` でつなぎ直す
- **ポート**（D-3。S-8）: 手伝い役が 127.0.0.1 の空いたポート（0 番）で待ち、決まった番号を合図の行で出す。スクリプトがその番号を Player に渡す
- **通り道の実装**（D-1）: `NWListener` ではなく BSD のソケットを 127.0.0.1 だけに結ぶ（スパイクで動いた形。loopback 以外から受けない）
- **配置の自動の確かめ**（9 章）: `Screen.safeArea` と `panel.Pick` で、すべてのボタンを照らす
- **スパイクの結果**（10 章）: V-1・V-2・V-5 はシミュレーターで通った。アラートの間は Unity が止まらない（V-4 の一部）

## 1. 何を作るか

Unity の Player のテスト（Unity Test Framework の PlayMode。iOS の Player の中で走る）が、サンプルのシーンのボタンを C# から押し、それで開いたシステムの画面（アラート、共有のシート、許可のダイアログ、通知のバナー、設定の画面）を、XCUITest の手伝い役に押してもらう。

```
Player のテスト（アプリ: Player のバンドル ID）           手伝い役（XCUITest のランナー: DriverUITests.test_drive）
                                                         127.0.0.1 の空いたポートで待ち、番号を合図の行で出す
  TCP でつなぐ ──────────────────────────────────────▶
  HELLO <run> <bundle id>                        ─────▶  bundle id を照らす。XCUIApplication(bundleIdentifier:) を作る
                                                 ◀─────  READY <run>
  7 alert-tap <Base64 の文言>                     ─────▶  アラートを待ち、ボタンを押す
                                                 ◀─────  7 ok
  ...
  BYE <run>（Run の終わり）                       ─────▶  BYE ok を返して終わる（テストのメソッドが成功で戻る）
```

- **通り道は 127.0.0.1 の TCP**（2 章の D-1）。手伝い役が待ち、Player がつなぐ
- **境目は命令**（Android の設計書の「境目を固定する」と同じ）。テストは命令だけを使い、XCUITest を知らない。手伝い役は Unity を知らない
- 手伝い役は、Player を起動しない（`launch()` を呼ばない）。スクリプトが起動した Player に、バンドル ID で付く（V-2）
- **サンプルのボタンは、今の Android・Windows と同じく `NavigationSubmitEvent` で押す**（`AndroidSampleScreenDriver.Press`）。そのうえで、配置を 9 章のとおり自動で照らす

## 2. 決めること

| # | 決めること | 決定 | 理由 |
|---|---|---|---|
| D-1 | 依頼と答えの通り道 | **127.0.0.1 の TCP**。手伝い役（XCUITest のランナーのプロセス）が BSD のソケットを `127.0.0.1` に結んで待ち、Player の C#（`System.Net.Sockets.TcpClient`）がつなぐ。1 本の接続に、行で依頼と答えを流す | シミュレーターでも実機でも同じ形（同じ機械〈端末〉の上の 2 つのプロセス）。ファイルの写しに頼らない。スパイクで、IL2CPP の `TcpClient` から XCUITest のランナーにつながった（V-1。シミュレーター）。`127.0.0.1` に結ぶので、loopback 以外から受けない |
| D-2 | どちらが待つか | **手伝い役が待つ**。Player がつなぐ | 手伝い役はスクリプトが先に起こしておける。Player のテストは「つながらなければ手伝い役がいない」と分かり、Android と同じく `Assert.Ignore` にできる（Editor の Test Runner から流したとき） |
| D-3 | ポートの決め方 | 手伝い役が 0 番で結び、OS が決めた番号を標準エラーに 1 行（`[NTK-UI-DRIVER] listening <port>`）で、すぐに書き出す。スクリプトは xcodebuild の出力からその行を読み、Player に `NTK_DRIVER_PORT`（シミュレーターは `SIMCTL_CHILD_`、実機は `devicectl --environment-variables`。今の `NTK_RUN` と同じ渡し方）で渡す | シミュレーターの 127.0.0.1 は Mac と同じなので、スクリプトが選んだ番号はほかのプロセスとぶつかりうる。0 番ならぶつからない。合図の行はスパイクで 10 秒で届いた（シミュレーター） |
| D-4 | 手伝い役の終わり方 | Player の Run の終わりに `BYE` を送り、手伝い役は `BYE ok` を返して成功として戻る。来なければ、上限（4.3）で失敗として戻る | 手伝い役の成否を、xcodebuild のテストの結果（`.xcresult` の 1 件）で判定できる |
| D-5 | どのテストの実行に手伝い役を付けるか | `verify_unity_ios.sh` の Player の実行（シミュレーターと実機）の**すべて**。Test iOS Profile の実行（`--as-package-user`）にも付ける | Android と同じ。サンプルのシーンが無い Profile では、サンプル画面のテストは Ignore になり、判定から外す（6 章） |
| D-6 | 依頼を先に出すか | **Android と同じく、`Send` で先に依頼を出し、システムの画面を開き、`Wait` で答えを待つ** | 許可のダイアログの間は Unity が止まる（NU-10 の調べ、2026-11-15）。止まっている間はテストが依頼を出せない。アラートの間は止まらなかった（V-4）が、どの画面でも正しい形にそろえる |
| D-7 | 手伝い役のコードの置き場所 | `Tools~/IosUiTests/NtkUiTests/DriverUITests.swift`（新）。押し方は今の `SystemUI.swift` を使い、足りない押し方を足す。ハーネスの UI テストと同じターゲット（`NtkUiTests`）で、`-only-testing:NtkUiTests/DriverUITests/test_drive` で選ぶ。`DeviceRequirements.json` には載せない（`test_ios_ui.sh` からは流さない）。環境変数が無ければ `XCTFail` | 署名・組み立て・実機の承認の仕組みをそのまま使える。システムの画面の押し方を 1 か所に保つ |
| D-8 | テストの側の置き場所 | `Tests/PlayMode/IosUiDriver.cs`（依頼の口）、`Tests/PlayMode/IosSampleScreenDriver.cs`（サンプルのシーンを開き、ボタンを押し、表示と配置を読む）。どちらも `#if UNITY_IOS && !UNITY_EDITOR`。行の組み立てと答えの読み方は、ガードの外の別のファイル（`Tests/Runtime/IosUiDriverProtocol.cs`）に置き、EditMode で固める | Android の 2 つのファイルと同じ分け方。行の組み立ては Editor でも組めるようにする |
| D-9 | 実機の決まり | 今の実機の回の決まりをすべて当てる（7 章）。手伝い役は画面の木（`debugDescription`）とスクリーンショットを出さない・残さない。端末の設定を変えない | 利用者の指示（端末の設定を変えない、ほかのアプリの通知を読まない、汎用のペーストボードに触らない） |

## 3. 依頼の形

### 3.1 行

UTF-8 の 1 行（`\n` で終わる）。1 行は 4096 バイトまで（超えたらテストの側で `ArgumentException`）。

```
HELLO <run> <bundle id>                  Player → 手伝い役（つないだ直後に 1 回。つなぎ直しのたびにも）
READY <run>                              手伝い役 → Player
<番号> <命令> <引数 1> <引数 2> ...        Player → 手伝い役
<番号> ok | <番号> ok <値> | <番号> fail <理由>   手伝い役 → Player
BYE <run>                                Player → 手伝い役（Run の終わり）
BYE ok                                   手伝い役 → Player
```

- `<run>` は Player が起動ごとに作る GUID（32 桁の小文字の 16 進）。スクリプトが渡す `NTK_RUN`（結果のファイルの名前）とは別
- `<bundle id>` は手伝い役の引数（`TEST_RUNNER_NTK_DRIVER_BUNDLE`）と同じでなければ、手伝い役は接続を閉じる
- `<番号>` は run の中で 1 から増やす整数
- 文字列の引数は Base64 の UTF-8。命令の表（3.2）で「生」と書いたものはそのまま（英数字と `-` だけ）
- 手伝い役は依頼を受けた順に 1 つずつ処理する。答えは同じ接続で返す
- 理由は英数字と `-` だけの決まった語（3.4）。画面から読んだ文字を理由に入れない
- **つなぎ直し**: 接続が切れたら（Player が背景で止まっている間に OS が閉じた、など）、Player は次の依頼の前に新しくつなぎ、同じ `<run>` の `HELLO` を送る。手伝い役は、同じ run の `HELLO` を受け直し、`READY` を返す（別の run の `HELLO` は断る）。切れた接続で答えを返せなかった依頼は、つなぎ直した後に Player が同じ番号で出し直す。手伝い役は、処理した番号を覚えていて、出し直された番号には覚えた答えを返す（押し直さない）

### 3.2 命令

サンプルの 4 つの画面が開くシステムの画面から決めた。足すときは、この形を守る。

| 命令 | 引数 | すること | 期限 | 実機 |
|---|---|---|---|---|
| `alert-tap` | `<ボタンの文言>` | アプリのアラート（`app.alerts` か、action sheet の `app.sheets`）が出るのを待ち、文言が同じボタンを押す。押した後、アラートが消えるのを待つ | 10 秒 | 使う |
| `alert-type` | `<欄の番号>`（生。0〜3）、`<文字列>` | アラートの中の、番号目の入力の欄（`textFields`。上から数える）を押して、文字列を入れる。文字列は ASCII の印字できる文字の 1〜64 文字。**秘密の入力欄（`secureTextFields`）には入れない**（`fail bad-arg`） | 10 秒 | 使う（ログインのダイアログを除く。7 章） |
| `alert-absent` | `<ミリ秒>`（生。1000〜30000）、`<閉じるボタンの文言>` | 指定の時間、アラートが出ないことを確かめる。出たら、文言のボタンを押して閉じ、`fail alert-opened` | 指定の時間 + 10 秒 | 使う |
| `sheet-close` | なし | 共有のシートが出るのを待ち、閉じる（`SystemUI.closeShareSheet`）。開いたことと閉じたことの両方を確かめる | 60 秒 | 使う |
| `sheet-absent` | `<ミリ秒>`（生。1000〜30000） | 指定の時間、共有のシートが出ないことを確かめる。出たら閉じて `fail sheet-opened` | 指定の時間 + 60 秒 | 使う |
| `permission-answer` | `allow` か `deny`（生） | springboard の通知の許可のダイアログが出るのを待ち、答える（今の `answerPermission` の文言の一覧）。ダイアログが消え、Player が前面に戻るのを待つ | 60 秒 | 使う |
| `settings-close` | なし | 設定のアプリ（`com.apple.Preferences`）が前面に来るのを待ち、Player を前面に戻す（`activate()`）。来たかどうかを `ok opened` / `ok not-opened` で返す | 60 秒 | 使う |
| `banner-tap` | `<題名>` | 題名が同じ通知のバナーを押す（`SystemUI.tapBanner`）。Player が前面に来るのを待つ | 60 秒 | **使わない** |
| `background` | `<ミリ秒>`（生。100〜5000） | ホームに戻し、Player が `runningBackground` になったことを確かめてから `ok` を書く。そのあと指定の時間待ち、Player を前面に戻す | 10 秒 | **使わない** |
| `front` | なし | Player を前面に戻す（`activate()`）。前面に来たことを確かめる | 10 秒 | 使う |

- 押す相手のアプリは、HELLO の `<bundle id>` の `XCUIApplication`（アラートと共有のシートはアプリのプロセスの中に出る。V-2 でアラートを読めた）と springboard（許可のダイアログ、バナー）。設定のアプリは前面に来たかだけを見る
- 共有のシートの中身（ほかのアプリの共有先、連絡先）は読まない。閉じるのに要る部品だけを探す（`SystemUI` の今の形）
- **失敗・期限切れのときの片付け**: 期限 60 秒の命令は、どの失敗でも、出ている画面を閉じ（アラートは引数の文言のボタン、共有のシートは閉じる、許可のダイアログは「許可しない」で答える、設定のアプリは Player を前面に戻す）、Player を前面に戻してから `fail` を返す（開いたまま残すと、後のテストが止まる）
- 「消えた」の確かめは、要素の `exists` を期限まで読み直す形にする。スパイクでは `XCTNSPredicateExpectation`（`exists == false`）が 10 秒たっても成り立たず、押して結果は届いたのに `fail did-not-close` になった（V-8）

### 3.3 テストの側の待ち

| 状況 | テスト |
|---|---|
| `NTK_DRIVER_PORT` が無い、つながらない（5 秒）、`READY` が 5 秒来ない | 手伝い役がいないとみなし、そのテストを `Assert.Ignore`。以後の依頼も Ignore |
| `READY` の後、答えが来ない | `Assert.Fail("ui driver did not answer")`。待ちは、期限 10 秒の命令は 30 秒、60 秒の命令は 120 秒（`ScreenAnswerTimeoutSeconds`）、`-absent` の命令は指定の時間 + その命令の待ち |
| `fail ...` | `Assert.Fail` |
| 接続が切れ、つなぎ直せない（5 秒） | `Assert.Fail("ui driver disconnected")`。以後の依頼も Fail |

- 待ちの間、テストのコルーチンは毎フレーム、ソケットに届いた分を読む（`Socket.Available`）。Unity が止まっている間に届いた答えは、OS の受け口に溜まり、戻ってから読む
- 画面の命令を使うテストは、メソッドに `[Timeout]` を付ける（既定の 180 秒で打ち切られるため。Android と同じ）

### 3.4 理由の語

`timeout`、`not-found`、`did-not-close`、`not-in-front`、`alert-opened`、`sheet-opened`、`sheet-did-not-open`、`dialog-did-not-open`、`bad-arg`、`not-background`、`device-only-refused`（実機で使わない命令。7 章）、`error`（予期しない例外。中身は手伝い役の出力にも出さない）。

## 4. 動かし方

### 4.1 `verify_unity_ios.sh` の Player の実行

今の流れ（書き出し → xcodebuild → インストール → 起動 → 結果のファイルを待つ → 止める）に、次を足す。

1. 書き出しの前に、`NtkUiTests` を build-for-testing する（`test_ios_ui.sh` の手順を `ios_script_common.sh` に移して共有する）
2. Player を入れた後、起動の前に、手伝い役を背景で起こす: `xcodebuild test-without-building -xctestrun <...> -only-testing:NtkUiTests/DriverUITests/test_drive -destination <同じ端末> -test-timeouts-enabled NO`、環境変数 `TEST_RUNNER_NTK_DRIVER_BUNDLE`・`TEST_RUNNER_NTK_DRIVER_LIMIT`（秒）・`TEST_RUNNER_NTK_DRIVER_DEVICE`（実機なら `1`）。実機では、起こす前に `device_guard` を通す
3. 合図の行（D-3）を 60 秒待ち、番号を読む。来なければ実行を失敗にする
4. Player を起動する（`NTK_DRIVER_PORT` を足す）
5. 結果のファイル（`.done`）を待つ。**`.done` の後、手伝い役の xcodebuild が終わるのを 60 秒待ってから** Player を止める（4.3）
6. 手伝い役の判定（6 章）

### 4.2 Player の側の流れ

- 最初の依頼の前に 1 回つなぎ、`HELLO` を送って `READY` を待つ
- サンプル画面のテストは、`IosSampleScreenDriver.LoadAtTopMenu` → `Open` の後、システムの画面を開くボタンを押す**直前**に、その画面の命令を `Send` する
- Run の終わりの受け手（`IosUiDriverRunCallback`。パッケージの側の `ITestRunCallback`、`IosUiDriver` と同じアセンブリ）が、`BYE` を送って `BYE ok` を 5 秒待つ。一度もつないでいなければ、新しくつなぎ、`HELLO` の後に `BYE` を送る。つながらなければ何もしない

### 4.3 時間の上限と順番

- 手伝い役の全体の上限 `TEST_RUNNER_NTK_DRIVER_LIMIT` = Player の実行の上限 + 起動の余裕 120 秒。Player の実行の上限（今の 600 秒）は、サンプル画面のテストを足すときに、テストの数から見積もり直す
- 手伝い役は `BYE` を受けるか上限で終わる。スクリプトは `.done` の後、手伝い役の xcodebuild の終わりを 60 秒待ち、それでも終わらなければ止めて実行を失敗にする（Player を先に止めて `BYE` を失わせない。Run の終わりの受け手と結果の書き手の呼ばれる順に頼らない）
- 1 つの命令の期限は 3.2 の表。手伝い役の中の待ちは、残りの期限を上限にする

### 4.4 後片付けと止め方

- 手伝い役の xcodebuild の PID は、今の `RUNNING_PID`（前景の手順の 1 つ）とは別の変数で持つ。`on_exit` で `stop_tree` する（シグナルで止めたときも、端末の上でテストを続けさせない）
- 手伝い役の出力には、止まりの見張り（ログが変わらない時間の上限）を当てない（待っている間は何も出ない）。全体の上限だけを当てる
- 端末とシミュレーターの後片付けは、Player に加えて、手伝い役が入れる 2 つのアプリ（UI テストのランナー `...uitests.xctrunner` と、UI の対象の空のアプリ `...uitesthost`）を消し、消えたことを読み戻す
- 実機の承認の表示（`ios_ui_runner.py` の `--dry-run`）は、Player の回でも 3 つのアプリを並べ、`playertests` の説明を書き直す（押すシステムの画面、通知の許可を求めること、Notification のサンプルの回で入れ直すこと）

### 4.5 通知の許可と、Player の回の分け方

通知の許可は、1 回の入れ方につき 1 回しか聞けない（答えは保存され、二度目のダイアログは出ない）。今の Notification の Player のテストは「まだ聞いていない」を前提にしている（`IosNotificationPlayerTests.cs`）。

- **Player の回を 2 つにする**:
  1. 今の回（カテゴリ `IosPlayer`、ただし `IosNotificationSample` を除く）。サンプル画面のテストのうち、Notification 以外もここで流す
  2. **Notification のサンプルの回**: Player を消して入れ直し、カテゴリ `IosNotificationSample` だけを流す（別の書き出し。書き出しのカテゴリは組み立てのときに決まるため）
- Notification のサンプルの回の中は、1 つの fixture にまとめ、`[Order]` で「許可を聞く前のテスト → `permission-answer allow` を 1 回 → 許可が要るテスト」の順にする
- 「許可しない」は、サンプルのテストでは確かめない（ハーネスの NU-2・NU-2b で確かめている）

## 5. テストの側の口（`IosUiDriver`）

Android の `AndroidUiDriver` と同じ名前と意味にする。

- `IEnumerator Do(string command, params string[] args)`: 依頼を出して答えを待つ。答えは `LastAnswer`、値は `LastValue`
- `IEnumerator Send(string command, params string[] args)` と `IEnumerator Wait(int number, float timeoutSeconds)`: 先に出して後で待つ（D-6）。`LastSent` に番号
- `IEnumerator WaitForPending()`: まだ待っていない依頼をすべて待つ（後片付け）。途中の `fail` でも残りを回収してから、まとめて `Assert.Fail`
- カテゴリ: `IosUiDriver`（手伝い役を使う）、`IosSampleScreen`（サンプルのシーンを通る）、`IosSimulatorOnly`（7 章）、`IosNotificationSample`（4.5）
- 行の組み立てと答えの読み方は、D-8 のとおり EditMode のテストで固める（Android には無かった層 1）

## 6. ゲートでの判定

`verify_unity_ios.sh` の Player の実行ごとに、次のどれかなら失敗にする。

- 手伝い役の xcodebuild の結果が成功でない（`ios_ui_runner.py results` で、`DriverUITests/test_drive` がちょうど 1 件、成功）
- 結果の XML のカテゴリ `IosUiDriver` のテストに skip がある（手伝い役に届かなかった UI のテストを緑にしない）
- カテゴリ `IosUiDriver` のテストが 1 本も無い
- Test iOS Profile の実行（`--as-package-user`）では、`IosSampleScreen` のテストを上の 2 つの判定から外す（サンプルのシーンが無いので Ignore になる。Android と同じ）。実機の回では `IosSimulatorOnly` のテストは流さない（7 章）ので、数えない

## 7. 実機での決まり

今の実機の回の決まり（`--dry-run` の 4 点と承認 ID、端末の識別子・team を出さない、ログと結果の束を残さない、終わりに入れたアプリを消す）に加えて:

| 操作 | 実機の回 | 理由 | 守り方 |
|---|---|---|---|
| ホームに戻す（`background`） | 使わない | 今の UI テストでもシミュレーターだけ（`DeviceRequirements.json` の `simulatorOnly`、Dialog の ID-15） | テストに `IosSimulatorOnly` を付け、実機の回は `-testCategory` の否定で外す。手伝い役も `TEST_RUNNER_NTK_DRIVER_DEVICE=1` のとき `fail device-only-refused` で断る |
| ログインのダイアログへの入力（`alert-type`）、秘密の入力欄 | 使わない | パスワードの保存・自動入力の画面が出て、利用者のキーチェーンに及びうる（今の UI テストでもシミュレーターだけ。Dialog の ID-12・ID-16） | 同上。手伝い役は秘密の入力欄にはどこでも入れない（3.2） |
| 通知のバナー（`banner-tap`） | 使わない | 利用者の iPhone はおやすみモードでバナーが出ない。Player は回ごとに入れ直すので、許可するアプリにも入れられない（実機のバナーは `--keep-installed` の回だけ） | 同上 |
| 汎用のペーストボード | 触らない | 利用者の指示 | 下の Clipboard の決まり |

- **Clipboard のサンプル**: サンプルを開いた直後の対象は汎用のペーストボード（`IosClipboardManagerExampleController.cs` の `_activeScope` の初期値）。実機の回のテストは、開いたら最初に名前付きの対象（`UseFixedNamedScopeButton` か `CreateNamedPasteboardButton`）に切り替え、状態の表示が名前付きであることを確かめてから他を押す。`RemoveActivePasteboard` の後は、切り替え直すまで他を押さない。`UseGeneralButton` と `ErrRemoveGeneralButton` は実機で押さない。**`IosSampleScreenDriver.Press` は、実機・Clipboard の画面・表示の対象が汎用のとき、押す前に `Assert.Fail` する**（カテゴリの付け忘れでも触らない）。汎用のペーストボードを使うテストは `IosGeneralPasteboard` を付け、シミュレーターでだけ流す。シミュレーターでも、汎用を読むテストは Player 自身が先に書いてから読む（ほかのアプリが書いた中身を読むと、貼り付けの許可のダイアログが出る）
- `--dry-run` の表示に、手伝い役のアプリ（4.4）と、押すシステムの画面の一覧を書く
- 手伝い役は、画面の木（`debugDescription`）とスクリーンショットを出さない・残さない。失敗のときも、理由の語だけを返す
- 端末の設定は変えない。許可は `permission-answer` で答える

## 8. 選ばなかった案

| 案 | 選ばなかった理由 |
|---|---|
| アプリの領域のファイルで依頼と答えを渡す（Android の形） | 実機では、ホストがアプリの領域に書くのに `devicectl device copy to` が要り、1 回が数秒かかる。XCUITest のランナーは別のアプリで、Player の領域に書けない |
| 依頼をアプリの隠しのラベル（アクセシビリティの要素）で出し、答えを隠しの入力欄に打ち込む | ラベルを出すための `.mm` が Player に要る。入力欄に打つとキーボードが出て、アラートの入力の欄のテストとぶつかる |
| Player と同じアプリの中から押す（UIKit の API でボタンを押す） | springboard（許可のダイアログ）と設定のアプリに届かない |
| WebDriverAgent・Appium | 導入と版の固定が要る。今の XCUITest の仕組みで足りる |
| AltTester など、Unity の UI を外から押すツール | サンプルのボタンは C# から押せば足りる。システムの画面は押せない |
| Player が待ち、手伝い役がつなぐ | 手伝い役の側に繰り返しの処理が要る。Player が手伝い役の有無を知れない |
| スクリプトがポートを選ぶ（v1） | シミュレーターでは Mac のほかのプロセスとぶつかりうる |
| `InputTestFixture`・`com.unity.ui.test-framework` で当たり判定を見る | `panel.Pick` で足りる（9 章）。追加のパッケージが要らない |

## 9. 配置の確かめ（自動）と手の確認表

自動のテストはボタンを C# から押すので、そのままでは配置（見えるか、押せるか）を通らない。サンプル画面のテストが画面を開くたびに、`IosSampleScreenDriver.CheckLayout()` で次を照らす。

- その画面のすべての `Button` について、スクロールで見える位置に出してから:
  - `worldBound` が `Screen.safeArea`（パネルの座標に直したもの）の中にある（ノッチ、ダイナミックアイランド、ホームインジケーターに隠れない）
  - `worldBound` の中心で `panel.Pick` した要素が、そのボタンか、その子孫（別の要素に覆われていない）
- 縦と横: テストから `Screen.orientation` で回し（Android の Dialog のテストと同じやり方）、同じ照らしをもう一度。Player Settings で横が許されているかは V-9
- 照らせなかったボタンは、名前を `Assert.Fail` の文に入れる（ボタンの名前はサンプルが決めた値なので出してよい）

**手の確認表**（リリースの前に実機で 1 回）: 見た目だけ（文字の切れ、読みやすさ、並び）。押せるか・隠れないかは上で見る。Android と Windows にはこの確認が無い。今回は iOS にだけ置き、ほかの 2 つは必要になったら足す。

## 10. 要検証

| # | 内容 | 確かめ方 | 結果・通らなければ |
|---|---|---|---|
| V-1 | Player（C# の `TcpClient`）から、XCUITest のランナーが 127.0.0.1 で待つポートにつなげるか。合図の行が 60 秒以内に xcodebuild の出力に届くか。**シミュレーターと実機の両方**。実機で Local Network の許可のダイアログが出ないか | 最小の Player と最小の手伝い役 | **シミュレーターで通った**（2026-11-15。コミットしない写し。つながり、`HELLO`・`READY`・`BYE` が往復した。合図の行は 10 秒）。実機は未。実機だけ通らなければ、Player が待つ形を試す。それも通らなければ、8 章の隠しのラベルの案に戻る |
| V-2 | `XCUIApplication(bundleIdentifier:)` が、`simctl launch` / `devicectl` で起動済みの Player に、起動し直さずに付けるか。`app.alerts` が Player のアラートを読めるか | V-1 と同じ回 | **シミュレーターで通った**（起動の行は環境変数からの 1 本だけ。アラートを見つけて押せた）。実機は未 |
| V-3 | 手伝い役のランナーが 30 分以上、1 つのテストのメソッドの中で待ち続けられるか（端末の画面ロックを含む） | 手伝い役を 35 分待たせる | 通らなければ、Player の回を機能ごとに分け、手伝い役もその単位で起こす |
| V-4 | 各画面（アラート、共有のシート、許可のダイアログ、設定の画面）で Unity のフレームが止まるか。**各画面の後、同じ接続で 1 往復できるか**（実機で、設定のアプリの間の背景を含む） | 各画面を出して、フレームの進みと 1 往復を記録 | アラートは止まらない（シミュレーター、1900 ミリ秒で 53 フレーム）。ほかは未。切れるなら 3.1 のつなぎ直しで続ける |
| V-5 | 手伝い役がいる間、Player が前面のままか | V-1 の回 | **シミュレーターで通った**（`HELLO` のときもアラートのときも `runningForeground`） |
| V-6 | iOS Profile の Player（Test Framework の Player）に、サンプルのシーンが入り、`LoadSceneAsync` で読めるか | 最小の Player のテストで `Application.CanStreamedLevelBeLoaded` | 通らなければ、Test Framework の Player の組み立てで、シーンを足す口を探す |
| V-7 | 位置のトリガー（`ShowLocationButton`、`ScheduleLocationButton`）が、位置の許可を求めるか | Notification のサンプルのテストの最初 | 求めるなら、その 2 つのボタンは自動のテストで押さない（位置の許可には答えない） |
| V-8 | アラートが消えたことを、XCUITest で確実に見る方法（スパイクでは `XCTNSPredicateExpectation` が成り立たなかった） | 実装の最初に、`exists` の読み直しと `waitForNonExistence(timeout:)` を比べる | 確実な方を使う |
| V-9 | テストから `Screen.orientation` で横に回せるか（Player Settings の許す向き）。回した後に `Screen.safeArea` が変わるか | 配置の確かめの最初 | 回せなければ、縦だけを自動にして、横は手の確認表に入れる |
| V-10 | `panel.Pick` が、スクロールの中のボタンで正しい要素を返すか（`ScrollView` の内側の座標） | 同上 | 返さなければ、`worldBound` とほかの要素の重なりを計算で照らす |

## 11. 進め方

1. この設計のレビュー（差分。前の指摘の判定と、変えた部分）
2. スパイクの続き: V-6、V-8、V-9、V-10 をシミュレーターで。V-1〜V-5 の実機は、利用者が端末を用意できるとき
3. 実装: 手伝い役、`IosUiDriver`、スクリプト、EditMode の行の組み立てのテスト、配置の確かめ
4. 最初の利用者: Dialog のサンプル（`ShowDialogButton` → `alert-tap` → 結果の表示 → 配置）。続けて Share、Clipboard、Notification（4.5 の別の回）
5. 実機の回（承認を得て）
6. 実装のレビュー

## 12. 実装で直すこと（レビュー v1 の A2。設計には直し込まない）

- サンプルの通知の題名が固定（`IosNotificationManagerExampleController.cs` の `"Energy Refilled"` など）で、手伝い役が探す固有の題名と合わない（Codex の C-1）。`banner-tap` はシミュレーターだけになったが、題名の照らし方は実装で決める
- 新しいテストは `IosPlayer` も持つ（Player に入るため）。`ios_player_results.py` に「skip を許すカテゴリ」の引数を足し、`--as-package-user` のときだけ `IosSampleScreen` を渡す。その doctest を足す（S-4、Codex の C-2）
- `BYE` を送る受け手はパッケージの側に置く（`Assets/NativeToolkitIosPlayerTests` の asmdef は参照が空で、`IosUiDriver` を呼べない。S-5）
