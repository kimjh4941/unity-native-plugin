# iOS の UI の手伝い役の設計 v1 のレビュー v1

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-ui-driver-design-v1.md](../designs/2026-11-15-ios-ui-driver-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

## 件数
A1 3 / A2 2 / B 4 / C 2

3 件の A1 は、どれも実機の回の決まりと後片付けの話です。通り道（127.0.0.1 の TCP と NWListener）、命令の形、Android と揃えた口（Do / Send / Wait / WaitForPending）の骨組みには、大きな食い違いは見つかりませんでした。

---

### S-1 [A1] [実機の決まりとプライバシー] 今の UI テストで「シミュレーターだけ」と決めた操作を、実機の Player の回で流す形になっている
- **根拠**
  - D-5 は、実機を含むすべての Player の回に手伝い役を付けると決めている。7 章で実機から外すのは汎用のペーストボードだけ。
  - 一方、今の UI テストでは次の 2 つを実機で流さないと決めている。`Tools~/IosUiTests/DeviceRequirements.json` の about には「simulatorOnly: … backgrounding, … and login dialogs (password saving and AutoFill can take over the screen)」とある。
    - ログインのダイアログ（`DialogUITests.swift:92-93`（ID-12）、`:145-146`（ID-16）の `requireSimulator()`）
    - ホームに戻すこと（`DialogUITests.swift:133-137`。ID-15 の `press(.home)`）
  - 3.2 の `alert-type`（秘密の入力欄も対象）と `background`（`press(.home)`）は、まさにこの 2 つの操作。
  - バナーについて: 利用者の iPhone はおやすみモードのため、バナーが出ない（README:219）。Player は回ごとに入れ直すので、集中モードの「許可するアプリ」にも入れておけない。実機のバナーは `--keep-installed` の回だけ、と決めた経緯がある（keep-installed 設計 v4 の基本情報）。
- **壊れ方**
  - 実機の回で Dialog サンプルの `ShowLoginDialogButton` に `alert-type` で値を打ち、Login を押す。するとパスワードの保存や自動入力の画面が端末に出うる。利用者のキーチェーンに保存の操作が及ぶおそれもある。テストは通りうるので、どこでも捕まらない（V-1〜V-7 に該当なし）。
  - `background` も同じで、決まりに反していても緑になる。
  - バナーは逆に、おやすみモードで出ないので `banner-tap` が `timeout` で落ちる。こちらは表に出る。
- **直し方の案**
  - 7 章に「実機の回で使わない命令と画面」を表で書く。中身は `background`、ログインのダイアログへの `alert-type`、`banner-tap`。
  - テストにはカテゴリ（例: `IosSimulatorOnly`）を付け、実機の回は `-testCategory "IosPlayer;!IosGeneralPasteboard;!IosSimulatorOnly"` で外す。UTF の `RuntimeTestRunnerFilter.AddFilters` は「含める側の OR」と「除く側の AND」の積なので、この形で効く。
  - 手伝い役の側でも、実機では `background` と、秘密の入力欄への `typeText` を `fail bad-arg` で断る。二重に守る。

### S-2 [A1] [実機の決まりとプライバシー] Clipboard サンプルは、開いた時点で既定の対象が汎用のペーストボード。「UseGeneralButton の範囲を押さない」だけでは守れない
- **根拠**
  - `Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:101`: `_activeScope = IosPasteboardScope.General`（シーンを読むたびにこの値で始まる）。
  - 同じファイルの `:513-517`: `RemoveActivePasteboard` が成功すると General に戻る。
  - `:1262-1268`: `ErrRemoveGeneral` は General を直に消しにいく。README:94 の R-11 が名指ししていた「汎用のペーストボードへの消す依頼」そのもの。
  - 設計 7 章の文は「汎用のペーストボードに触るサンプルのボタン（Clipboard の `UseGeneralButton` の範囲）」。
- **壊れ方**
  - 実機の回で、名前付きの対象に切り替えないまま `CopyPlainTextButton` や `ReadButton` を押すテストを書くと、利用者の汎用のペーストボードを書き換えたり読んだりする。カテゴリの付け忘れも同じ結果になる。テストは通るので捕まらない。
  - `RemoveActivePasteboard` の後に続けて押すボタンも、General に当たる。
- **直し方の案**
  - 7 章を「実機の回の Clipboard サンプルのテストは、開いたら最初に `UseFixedNamedScopeButton` か `CreateNamedPasteboardButton` を押し、状態の表示が名前付きであることを確かめてから他を押す。`RemoveActivePasteboard` の後は、もう一度切り替えるまで何も押さない。`ErrRemoveGeneral` と `UseGeneral` は実機で押さない」に改める。
  - あわせて、`IosSampleScreenDriver.Press` の中で、実機かつ Clipboard 画面かつ対象が General のときは押す前に `Assert.Fail` する見張りを置く。

### S-3 [A1] [実行の仕組み] 手伝い役が端末に入れる 2 つのアプリと、背景で動く xcodebuild の後片付けが決まっていない
- **根拠**
  - 入るアプリについて: `test-without-building` は、ランナー（`...uitests.xctrunner`）に加えて UI の対象の空のアプリ（`...uitesthost`）も入れる。`test_ios_ui.sh:64` の `INSTALLED_IDS` と、`ios_ui_runner.py:182-187` の `UI_APPS` がこの 3 つを数えている。
  - ところが Player の回の後片付け（`verify_unity_ios.sh:216-223` の `clean_up_player`）は `PLAYER_ID` しか消さない。承認の表示も `PLAYER_APPS` だけを並べ（`ios_ui_runner.py:463`）、`playertests` の説明は「Notification calls without ever asking for the permission … nothing is shown」のまま（`:196`）。7 章は「手伝い役が入ること」を足すとしか書いておらず、空のアプリと説明文の書き換えに触れていない。
  - 背景のプロセスについて: 走っている手順の記録は `RUNNING_PID` の 1 つだけ（`ios_script_common.sh:105-142`）。`run_limited` は前景で 1 つずつ動く。`on_exit`（`verify_unity_ios.sh:236-252`）は `stop_running_step` しか呼ばない。4.1 の 3 は手伝い役を「背景で起こす」とだけ書き、その PID の扱い、trap、時間の上限、止まりの見張りを決めていない。
- **壊れ方**
  - 実機の回が成功しても失敗しても、`uitesthost` と `xctrunner` が利用者の iPhone に残る。しかも利用者は「通知の許可を求めない、何も表示しない」と書かれた一覧を承認することになる。
  - Ctrl-C や TERM で止めると、Player は消えて実行の lock も外れるが、手伝い役の xcodebuild は `NTK_DRIVER_LIMIT`（最長 600 秒）まで端末の上でテストを続ける。次の回がその間に始まりうる。どちらもテストは落ちないので捕まらない。
- **直し方の案**
  - 4.1 に次を足す。
    - Player の回の後片付けの対象を 3 つのアプリにする（消えたことを読み戻すところまで）。
    - 承認の表示を `PLAYER_APPS` と `UI_APPS` の和にし、`playertests` の説明を書き直す（押すシステムの画面、許可を求めること）。
    - 手伝い役の PID を別の変数で持ち、`on_exit` で `stop_tree` する。全体の上限は Player の上限 + 余裕にする。
    - 止まりの見張りは、手伝い役の標準出力には当てない（待っている間は何も出ないため）。
  - 実機では、手伝い役を起こす前にも `device_guard` を呼ぶ。

### S-4 [A2] [契約と境目] カテゴリと今の結果の判定（`ios_player_results.py`）のつなぎが決まっていない
- **根拠**
  - Player は `-testCategory IosPlayer` で組まれる（`verify_unity_ios.sh:291`）。
  - `ios_player_results.py` は、skip が 1 件でもあれば失敗にし、カテゴリ外のケースがあっても失敗にする（docstring の 8〜14 行、`judge` の最後）。
  - 設計 6 章は、Test iOS Profile で `IosSampleScreen` を「上の 2 つの判定（`IosUiDriver` の skip と件数）から外す」としか書いていない。新しいテストに `IosPlayer` を付けるかどうかも書いていない。
- **壊れ方**
  - `IosPlayer` を付けない場合: テストが Player に入らず、「`IosUiDriver` が 1 本も無い」で落ちる。
  - 付けた場合: `--as-package-user` の回で Ignore になったサンプル画面のテストが、今の判定の「skipped」で落ちる。
  - どちらも最初の実行で表に出る。
- **直し方の案**
  - 6 章に次を書く。
    - 新しいテストは `IosPlayer`、`IosUiDriver`、必要なら `IosSampleScreen` を併せ持つ。
    - `ios_player_results.py` に「skip を許すカテゴリ」の引数を足し、`--as-package-user` のときだけ `IosSampleScreen` を渡す。
    - その doctest を足す。

### S-5 [A2] [契約と境目] BYE の送り手が今の asmdef では組めない。.done と BYE の順もない
- **根拠**
  - `IosPlayerResultWriter` は `Assets/NativeToolkitIosPlayerTests/Runtime` にあり、asmdef の `references` は空。パッケージの `NativeToolkit.Runtime.PlayModeTests` にある `IosUiDriver` の接続は使えない。
  - 4.2 は「つながっていれば同じ接続」としている。
  - `RunFinished` は `.xml` の後に `.done` を書く（`IosPlayerResultWriter.cs:41-46`）。スクリプトは `.done` を見つけ次第、Player を止める（`verify_unity_ios.sh:361-364`）。
- **壊れ方**
  - 参照が無いのでコンパイルで止まる（表に出る）。
  - 順が決まっていないため、`.done` を書いた直後、BYE の前に Player が止められうる。すると手伝い役は上限まで待ってから失敗し、回がときどき赤になる。
- **直し方の案**
  - BYE はパッケージ側の `IosUiDriver` の中で送る。自前の `ITestRunCallback` か、`IosUiDriver` の公開の口を asmdef 参照で呼ぶ。
  - 順は「`.xml` → BYE を送って答え（`BYE ok`）を待つ（最長数秒）→ `.done`」と決める。
  - 新しい接続で送る BYE の前にも HELLO が要るかを、3.1 に書く。

### S-6 [B・中心の約束] [検証] 通知の許可は 1 回の入れ方につき 1 回しか出ない。同じ回の中の状態の流れが決まっていない
- **根拠**
  - 今の `IosNotificationPlayerTests.cs:22`: 「The player never asks for the permission」。
  - 同じファイルの `:163-169`（NP1 は NotDetermined を期待）と `:208-236`（NP4・NP5 は許可が無いことを前提にする）。
  - iOS には Android の `pm grant` / revoke にあたる戻し方が無く、設計も使わないと決めている（D-9）。
- **壊れ方**
  - サンプルの Notification のテストが `permission-answer allow` を出した後に NP1・NP4・NP5 が走ると、それらが落ちる。今はクラス名の abc 順でたまたま先に走るだけ。
  - 1 つの回では `allow` と `deny` の片方しか確かめられない。2 本目の `permission-answer` は `dialog-did-not-open` になる。
  - 許可の後に `RequestPermissionButton` を押すテストの期待も決まっていない。
- **直し方の案**
  - Notification のサンプルのテストを `[Order]` 付きの 1 つの fixture にまとめ、「許可しない状態のテスト → 1 回だけ allow → 許可が要るテスト」の順を設計に書く。
  - 今の NP 系との順は、名前ではなく明示的に保つ（例: サンプル側を別の Player の回に分ける）。
  - `deny` は 1 回の回では確かめない、と決めて書く。

### S-7 [B・中心の約束] [検証] Player が背景で止まっている間に接続が保たれるかを、どの V でも確かめていない
- **根拠**
  - 3.3 は「Unity が止まっている間に届いた答えは、OS の受け口に溜まり、戻ってから読む」としている。
  - `background`、`settings-close` では Player が背景に回り、数秒で一時停止（suspend）される。Apple の TN2277 には、一時停止したアプリのソケットの資源は回収されうる、とある（とくにロック中や実機）。これは私の理解で、TN2277 の原文はこの場で確かめていない。
  - V-4 はフレームが止まるかを測るだけ。接続が切れたときの張り直し（2 本目の HELLO を受けるか）も 3.1 に無い。
- **壊れ方**: 実機で設定のアプリが開いている間に接続が回収されると、以後のテストがすべて `ui driver disconnected` で落ちる。
- **直し方の案**
  - V-4 に「各画面の後、同じ接続で 1 往復できるか（実機で、最長の 60 秒の背景）」を足す。
  - 3.1 に「切れたら新しく HELLO（同じ run）でつなぎ直し、手伝い役は受け直す」を決めておく。

### S-8 [B・中心の約束] [実行の仕組み] 待ち始めた合図とポートの決め方が確かめられていない
- **根拠**
  - 4.1 の 3 は、ランナーの標準出力の 1 行 `[NTK-UI-DRIVER] listening <port>` が 30 秒以内に xcodebuild の出力へ届くことを前提にしている。
  - Swift の `print` は、パイプ先ではバッファされる。テストのランナーの出力を xcodebuild が実機でもすぐ中継するかも、私は確かめていない（どちらも私の理解）。V-1〜V-7 のどれもこれを見ない。
  - シミュレーターの 127.0.0.1 は Mac の loopback なので、スクリプトが選んだポートが Mac の他のプロセスとぶつかりうる。
  - 図では「127.0.0.1:<port> で待つ」だが、D-1 には `NWListener` を loopback だけに絞ること（`requiredInterfaceType = .loopback` など）が書かれていない。全部の口で待つと、実機では Wi-Fi 側からもつながる。シミュレーターでは、アドホック署名のランナーに macOS のファイアウォールのダイアログが出うる（私の理解）。
- **直し方の案**
  - 手伝い役は port 0 で loopback に絞って待ち、決まったポートを合図の行に載せる。行は `FileHandle.standardError` に即時書き出す。スクリプトはその値を Player に渡す（ぶつかりも無くなる）。
  - V-1 に「合図の行がシミュレーターと実機で 30 秒以内に届く」「loopback 以外から受けない」を足す。

### S-9 [B・網羅] [契約と境目] Clipboard サンプルが開きうる「貼り付けの許可」のダイアログを押す命令が無い
- **根拠**
  - 貼り付けの許可は springboard のアラートで、`ClipboardUITests.swift:47-55` が `springboard.alerts` で押している。
  - 同じシミュレーターでは、`test_ios_ui.sh` の CB-1〜4 が汎用のペーストボードにランナーから書く（`ClipboardUITests.swift:6-8`）。
  - 同期の読み出しは、答えが来るまで main を止める（native-toolkit の記録）。
  - 3.2 の `alert-tap` は `app.alerts` / `app.sheets` だけを見る。`permission-answer` は通知の許可だけ。
- **壊れ方**
  - シミュレーターで UI テストの後に Player の回を流すと、汎用の Read のテストで許可のダイアログが出る。Unity が止まったまま 600 秒の上限で回全体が落ちる。
  - シミュレーターの前の状態しだいで、ときどき起きる。
- **直し方の案**
  - `paste-answer allow|deny` を足す（ダイアログが Player の名前を出すときだけ押す。今の `pressPastePrompt` の照らし方）。
  - または、汎用のテストは必ず Player 自身が先に書いてから読む、と決める。

### S-10 [C] [実行の仕組み] 時間の上限の数が揃っていない
- Player の回の上限は 600 秒（`verify_unity_ios.sh:361`、`:398`）のまま。V-3 は 30 分以上を想定している。
- `NTK_DRIVER_LIMIT` を「Player の上限と同じ値」にすると、手伝い役は Player より先に起きるぶん先に切れる。
- 4.1 の 5 で 60 秒待った後にどうするか（止めて失敗）も書かれていない。
- 案: Player の上限を、サンプル画面のテストの数から見積もって改める。手伝い役は「Player の上限 + 起動の余裕 + 60 秒」にする。

### S-11 [C] [契約と境目] 細かな書き方の食い違い
- 4.2「`LoadAtTopMenu` → `Open` → `Press` の前に `Send`」は、シーンを読む前に送るとも読める。そう読むと、`alert-tap` の 10 秒をシーンの読み込みが食う。「`Press` の直前」と書く。
- 3.2 `alert-absent` の「`.cancel` の役割」は、XCUITest の要素からは読めない（ボタンの種類を出す属性が無い。私の理解）。
- 3.2 末尾の「失敗時は出ている画面を閉じる」は、許可のダイアログでは「答える」ことになる。どちらで答えるかを書く。
- D-8 は `#if UNITY_IOS && !UNITY_EDITOR`、5 章は「EditMode で固める純粋な関数」。純粋な関数は別のファイル・ガード外に置く、と書く。
- `DriverUITests/test_drive` を `DeviceRequirements.json` に載せるかを書く。載せないなら、`test_ios_ui.sh` からは選ばれない。環境変数が無いときの振る舞いも決める（`XCTSkip` は `results` で失敗扱い）。

---

## この設計で進めてよいか
このままでは進めず、A1 の 3 件（実機で流す範囲、Clipboard の既定の対象、後片付けと承認の表示）と S-4・S-5 を直した v2 を作るのがよいです。ただし V-1〜V-3、V-5、V-6 のスパイクはこれらの指摘とは独立しているので、v2 を待たずにシミュレーターで始めてかまいません。その際、S-7 と S-8 の確かめ（背景の後の 1 往復、合図の行の届き方）をスパイクに足すことを勧めます。

## 2. Codex

A1 0 / A2 2 / B 2 / C 0

1. **C-1 — A2：バナーの題名がサンプルと一致しない**

**根拠：** 設計書:86 は `NTK-IS-<番号>-<run>` の題名だけを探します。一方、[通知サンプル:264](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Notification/IosNotificationManagerExampleController.cs:264) は `"Energy Refilled"`、同ファイル:331 も固定値です。

**壊れ方：** サンプルのボタンで通知を表示しても、指定の固有題名には一致しません。そのため `banner-tap` が期限切れで失敗します。

**修正案：** サンプルにテスト用の題名差し替え口を設け、通知に設定する値とドライバーへの要求を一致させてください。

2. **C-2 — A2：Profile の Ignore 例外が既存判定では失敗になる**

**根拠：** 設計書:147 の例外は、UI ゲートの二つの判定だけが対象です。一方、[既存スクリプト:372](/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_ios.sh:372) が呼ぶ [結果判定:111](/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/ios_player_results.py:111) は、`IosPlayer` 全件の Passed を要求します。

**壊れ方：** 既存の抽出対象に含めるため `IosPlayer` も付けたサンプルテストが、package-user 実行で予定どおり Ignore になります。すると既存判定が失敗します。Codex は、Passed 1 件と該当 Skipped 1 件の XML を現行関数に渡し、失敗することを確認しました。

**修正案：** カテゴリの抽出と既存の結果判定にも、Profile 限定の `IosSampleScreen` 例外を適用してください。

3. **C-3 — B（中心契約）：通知許可の状態を隔離する検証・実行単位がない**

**根拠：** [既存通知テスト:169](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosNotificationPlayerTests.cs:169) は `NotDetermined` を、同ファイル:179 は許可なしを要求します。再インストールは実行冒頭の一度だけです。また、通知許可への回答は保存され、再要求ではダイアログが出ません（[Apple 仕様](https://developer.apple.com/documentation/usernotifications/asking-permission-to-use-notifications)）。

**壊れ方：** サンプル側が先に許可へ回答すると、後続の既存テストが失敗します。同じインストールで deny の後に allow を試しても、二度目のダイアログ待ちが期限切れになります。

**修正案：** 未回答・拒否・許可の各状態が必要なテストを、再インストールを挟む別実行へ分けてください。既存テストとの混在実行も検証対象にしてください。V-* の最小スパイクでは、この状態汚染を確認できません。

4. **C-4 — B（中心契約）：完了通知と Player 停止の順序が未確定**

**根拠：** 設計書:122 は結果を書いた後に BYE を送ります。[結果書き手:46](/Users/jonghyunkim/Desktop/unity-native-plugin/Assets/NativeToolkitIosPlayerTests/Runtime/IosPlayerResultWriter.cs:46) は `.done` を公開し、[既存スクリプト:361](/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_ios.sh:361) は検出後すぐ Player を停止します。

**壊れ方：** `.done` 公開後、BYE 送信前に Player が停止されると、Player のテストは成功していても、ドライバーは BYE 不着で失敗します。

**修正案：** 「XML 保存、BYE 受領確認、`.done` 公開」の順序を定義するか、ドライバー終了まで Player を停止しない形にしてください。この競合も検証してください。

判定：中心契約の B 2 件を補完し、A2 を実装修正事項として記録すれば、スパイクへ進めます。

## 3. 判断（こちら）

A1 はサブエージェントが 3 件、Codex が 0 件。止めずに v2 を作った。

| 指摘 | 区分 | 判断 | v2 の場所 |
|---|---|---|---|
| S-1 実機でシミュレーターだけの操作を流す | A1 | 受け入れる。`background`、ログインと秘密の入力欄への入力、`banner-tap` を実機で使わない。テストは `IosSimulatorOnly` で外し、手伝い役も実機では断る（二重） | 3.2 の「実機」の列、7 章の表 |
| S-2 Clipboard の既定の対象が汎用 | A1 | 受け入れる。実機では最初に名前付きに切り替え、`IosSampleScreenDriver.Press` が「実機・Clipboard・汎用」で止める | 7 章 |
| S-3 3 つのアプリと背景の xcodebuild の後片付け | A1 | 受け入れる。後片付けの対象、承認の表示、別の PID と `on_exit`、止まりの見張りを当てない | 4.4 |
| S-4 カテゴリと結果の判定のつなぎ | A2 | 実装で直す | 12 章 |
| S-5 `BYE` の送り手の置き場所と順 | A2（置き場所）／B（順。Codex の C-4 と同じ） | 置き場所は実装で直す。順は中心の約束として設計に書いた: スクリプトが `.done` の後に手伝い役の終わりを待ってから Player を止める。送り手と結果の書き手の呼ばれる順に頼らない | 4.2、4.3、12 章 |
| S-6 通知の許可の状態（Codex の C-3 と同じ） | B（中心） | 受け入れる。Notification のサンプルは入れ直した別の Player の回で流し、中は `[Order]` で順を決める。「許可しない」はサンプルでは見ない | 4.5 |
| S-7 背景の間の接続 | B（中心） | 受け入れる。同じ run の `HELLO` でつなぎ直し、出し直しの番号には覚えた答えを返す。V-4 に 1 往復を足した | 3.1、V-4 |
| S-8 合図の行とポート | B（中心） | 受け入れる。0 番で結び、番号を標準エラーで出す。127.0.0.1 に結ぶ（BSD のソケット。スパイクで動いた形）。合図の行はスパイクで 10 秒で届いた（シミュレーター） | D-1、D-3、V-1 |
| S-9 貼り付けの許可のダイアログ | B（網羅） | 命令は足さない。汎用を読むテストは Player 自身が先に書いてから読む、と決めた | 7 章 |
| S-10 時間の上限 | C | 上限の関係（手伝い役 = Player + 120 秒、`.done` の後 60 秒）だけ書いた。Player の上限の値は、テストを足すときに見積もる | 4.3 |
| S-11 細かな書き方 | C | `Press` の直前、`alert-absent` は閉じるボタンの文言を引数に、失敗のときの許可のダイアログは「許可しない」、行の組み立ては別のファイル、`DeviceRequirements.json` に載せない、を書いた | 3.2、4.2、D-7、D-8 |
| C-1 バナーの題名 | A2 | 実装で直す（`banner-tap` はシミュレーターだけになった） | 12 章 |
| C-2 Profile の Ignore と今の判定 | A2 | S-4 と同じ | 12 章 |
| C-3 通知の許可の状態 | B（中心） | S-6 と同じ | 4.5 |
| C-4 `BYE` と Player を止める順 | B（中心） | S-5 の順と同じ | 4.3 |

あわせて v2 に入れたもの（レビューの外）:

- 利用者の判断（2026-11-15）で、配置（セーフエリア、当たり判定、縦と横）を `Screen.safeArea` と `panel.Pick` で自動に照らす（9 章、V-9、V-10）
- スパイク（シミュレーター、コミットしない写し）の結果: V-1・V-2・V-5 が通った。アラートの間は Unity が止まらない。アラートが消えたことの判定が成り立たなかったので V-8 を足した
