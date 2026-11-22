# iOS の Player のテストと、システムの画面を押す UI の自動化 v2

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | 段 2（設計書 v4 の I-12・I-13）。iOS の Unity の Player で、(A) NUnit のテストをシミュレーターで流す仕組みと、(B) システムの画面（アラート、共有のシート、通知の許可・バナー・アクション・返信、コールドスタート、ペーストの許可）を XCUITest で押して確かめる仕組み |
| 使う段 | 段 2（1.x のまま Player の基準を取る、仕組みを試す）、3-R（1.x の振る舞いを機能ごとに記録する）、3-S と段 3（C ABI の `2.0.0` に替えた後に同じ項目で比べる）、段 4（git の URL の形で通す） |
| 親 | [README](../README.md)、設計書 [v4](2026-11-14-ios-c-abi-2c-plan-design-v4.md) の I-12・I-13、段 1 の結果（README 6.3） |
| 写すもの | native-toolkit の iOS の UI テスト（`ios/IosLibraryCApiUITests`、`ios/IosLibraryExample/IosLibraryExampleUITests`、`scripts/test_ios.sh`、`scripts/ios_ui_runner.py`、設計 `artifact/topics/ios-c-abi/designs/2026-10-18-ios-c-abi-ui-test-design.md`）と、このリポジトリの Android の仕組み（`scripts/verify_unity_android.sh`、`scripts/android_ui_driver.py`、設計 `../../android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v15.md`） |
| 決まり | 実機は native-toolkit の `agent-rules/device-testing.md`（毎回の許可、端末の設定を変えない、UDID・Team ID を記録やログに書かない、端末の画面の記録を消す）。Simulator.app を開かない |
| レビュー | [../reviews/2026-11-14-ios-ui-automation-design-review-v1.md](../reviews/2026-11-14-ios-ui-automation-design-review-v1.md)（サブエージェントと Codex） |

## v1 からの主な変更（v2。レビュー v1 を受けて。2026-11-14）

- `-doNotReportTestResultsBackToEditor` は、書き出す Editor の起動の引数に渡す（Player の起動の引数ではない。Codex の A1、サブエージェントの R-1）。`ConnectToHost` も外す（3.1）
- 書き出しを変える処理と戻す処理を、Editor のアセンブリにアセンブリ単位の属性で置き、iOS のときだけ働かせる。戻すファイルを Android の一覧にそろえる（R-5。3.2・3.4）
- ハーネスを、ハーネスの書き出しの間だけ組まれるようにした。重ね表示の `.mm` は `Plugins/iOS/` に置かない（R-7。4.1）
- 実機の承認を (A) にも掛け、承認の照らしの範囲（ハーネス、パッケージ、ビルドの設定、ランナー）と、端末に入るアプリの一覧を足した（Codex の 4、R-10。4.6）
- Profile の `iOSSimulatorArchitecture` の値を直した（Codex の 5、R-4。3.2）
- 直さなかった指摘を 11 章に書いた

## 1. 何を作るか

2 つの仕組みを作る。どちらも Simulator.app を開かず、`xcodebuild`・`simctl`・`devicectl` だけを使う。

| | (A) Player の NUnit のテスト | (B) UI のシナリオ |
|---|---|---|
| 何を確かめる | P/Invoke と IL2CPP の経路（呼べる、値が往復する、構造体の配置、「使えない」への落ち方）。システムの画面を出さないもの | 人が押すシステムの画面を通るもの（Dialog・Share・Notification・Clipboard の振る舞い） |
| アプリ | Unity の Test Runner が組むテストの Player（`IncludeTestAssemblies`） | このリポジトリだけにあるハーネスのシーン（`Assets/` の下。パッケージに入れない）を組んだアプリ |
| 動かす人 | アプリが起動すると Test Runner がテストを流し、終わると自分で終わる | XCUITest のテストが、アプリを bundle id で起動し、重ね表示のボタンを押し、システムの画面を押し、重ね表示の結果の行を読む |
| 結果 | アプリの中のファイル（NUnit の XML）を `simctl get_app_container` で読む | XCUITest の結果（`.xcresult`）を `xcresulttool` で読む |
| スクリプト | `scripts/verify_unity_ios.sh` | `scripts/test_ios_ui.sh`（とその中で使う `scripts/ios_ui_runner.py`） |
| 実機 | 許可を得て、同じスクリプトに端末を渡す | 許可を得て（許可の ID。4.6） |

**Android との違い**: Android は (A) の Player のテストの中から、ホストの補助（`android_ui_driver.py`）に画面の操作を頼む 1 つの仕組みにした。iOS では、システムの画面を押せるのは XCUITest のプロセスだけで、XCUITest はアプリのログを読めない。そこで、押す側（XCUITest）が流れを持ち、アプリは言われた手順を流す形（native-toolkit と同じ）にする。(A) は画面を出さないテストだけを持つ。

## 2. 選ばなかった案

| 案 | 選ばなかった理由 |
|---|---|
| Android と同じく、Player の NUnit のテストが依頼を出し、XCUITest が待ち受けて押す | XCUITest のテストは、アプリの標準出力も `os_log` も読めない。依頼をファイルで渡すと、実機ではアプリの中のファイルを XCUITest から読めない（シミュレーターだけの仕組みになる）。押す側が流れを持つ形は、native-toolkit で実機でも動いている |
| (B) の結果をアプリの中のファイルに書き、ランナーが `simctl get_app_container`・`devicectl device copy from` で読む | 読むのはテストが終わった後になり、テストの中で「押した後の値」を照らせない。実機では取り出しのたびに端末にアクセスが要る。重ね表示の文字なら、XCUITest がその場で読める（native-toolkit と同じ） |
| サンプルのシーン（`NativeToolkitExampleScene`）を (B) に使う | UI Toolkit の画面は XCUITest から要素として見えない。サンプルは人が見るためのもので、確かめる手順を決まった形で流す作りではない |
| Unity の Test Runner の `-runTests -testPlatform iOS`（Build and Run）で (A) を流す | Build and Run は Xcode にシミュレーターでの起動を任せ、Simulator.app を開きうる（F-18）。Editor との接続（PlayerConnection）にも頼る。書き出すだけにして、起動は自分で行う |
| XcodeGen・Tuist で XCUITest のプロジェクトを生成する | この Mac に無く、版の固定と導入が増える。プロジェクトは小さい（ターゲット 2 つ）ので、手で書いてコミットする |
| XCUITest のテストをハーネスのアプリと同じ Xcode のプロジェクト（Unity の書き出し）に足す | 書き出しのたびに作り直され、Append でもテストのターゲットを保つ手当てが要る。Unity の版で書き出しの形が変わる。別のプロジェクトにして bundle id で起動する |

## 3. (A) Player の NUnit のテスト（`scripts/verify_unity_ios.sh`）

### 3.1 流れ

```
Unity（batchmode、Editor の引数に -doNotReportTestResultsBackToEditor）:
      TestRunnerApi.Execute（PlayMode、Player、書き出すだけ）
      → Xcode のプロジェクトに書き出す（Simulator SDK、arm64。ConnectToHost なし）
xcodebuild（シミュレーター向け、署名なし）→ .app
simctl install → simctl launch <bundle id> -ntkRun <run>
      アプリの中: Test Runner がテストを流す → TestRunCallback が NUnit の XML を書く → 自分で終わる
ランナー: アプリが終わるのを待つ（上限つき）→ simctl get_app_container data で XML を読む → 判定
```

- **書き出すだけ**: Test Framework 1.6 の `PlayerLauncherTestRunSettings.buildOnly`（`com.unity.test-framework` の `UnityEditor.TestRunner/TestLaunchers/PlayerLauncher.cs` の 95 行と 250〜270 行。Library の中にあり、このリポジトリには無い）。`-executeMethod` から `TestRunnerApi.Execute` を `overloadTestRunSettings` つきで呼ぶ。テストの選び方（カテゴリー、名前）はこのとき決まる
- **Editor に結果を返さない**: `-doNotReportTestResultsBackToEditor` は **書き出しを行う Unity の Editor の起動の引数**に渡す。Editor が書き出しのときに自分の引数を見て、`RemoteTestResultSender` をテストのシーンに入れるかを決める（同じファイルの 104〜110 行。Player の起動の引数は読まれない）。あわせて 3.2 の書き出しを変える処理で `BuildOptions.ConnectToHost` を外す（buildOnly でも付く。同じファイルの 238 行）。送る部品がシーンに無いことを V-5 で確かめる（Player のログに `No RemoteTestResultSender` が出る）
- **結果の書き出し**: テストのアセンブリに `[assembly: TestRunCallback(typeof(IosPlayerResultWriter))]` を置く。`RunFinished(ITestResult)` で NUnit 3 の XML を `Application.persistentDataPath/ntk-player-tests/<run>.xml` に一時ファイルから名前を変えて書き、最後に `done` の印を書く。`<run>` は起動の引数 `-ntkRun <32 桁の 16 進>` で渡す（前の実行の結果を読まないため）
- **終わり方**: Test Runner の `PlayerQuitHandler` がテストの後にアプリを終える。ランナーは `done` の印が出るのを待つ（上限は (A) の全体で 15 分。読みが 3 分伸びなければ止まったとみなす。段 1 の見張りと同じ）
- **判定**: XML の `test-run` の `result`、`total`、`failed`、`skipped` を読む。**流れたテストの数が 0 なら失敗**。名指ししたカテゴリーのテストが skip されたら失敗（Android の `AndroidUiDriver` の判定と同じ考え）
- **署名**: シミュレーターは署名なし（`CODE_SIGN_IDENTITY=-`。段 1 と同じ）。実機は段 1 と同じく自動の署名で `-allowProvisioningUpdates` なし、Team は profile から読み、どこにも書かない

### 3.2 Build Profile、SDK、bundle id

- スクリプトは **`-activeBuildProfile` を必ず渡す**（Android と同じ。Library に残るので）。既定は `iOS Profile`（ビルドの処理が有効。1.x では Embed を `PostBuildProcessor` が行う）。`--as-package-user` で `Test iOS Profile`（定義なし。利用者の形）
- **1.x の `Test iOS Profile` では Embed が入らない**（レビュー v2 の N-9）。1.x では `--as-package-user` の回は書き出しの後に `IosFrameworkPatcher.Apply` を呼ぶ（利用者がメニューで行うことの代わり）。3-S 以降は I-1 の口と `AddToEmbeddedBinaries` で入るので呼ばない（スクリプトは `Plugins/iOS/VERSION.txt` の `dist_version` で分ける）
- **Simulator SDK と arm64**: 両方の Profile が `iPhoneSdkVersion: 988`（Device SDK）を持つ。`iOSSimulatorArchitecture` は `iOS Profile` が 1（arm64）、`Test iOS Profile` が 0（設計書 v4 の I-13 の「両方とも 0」を直す）。Profile が Player Settings を上書きするときに、書き出しの間に替えた値が効くかは確かめていない。V-2 で次の順に試し、最初に通ったものにする:
  1. `ITestPlayerBuildModifier` で、書き出しの前に `PlayerSettings.iOS.sdkVersion` と `simulatorSdkArchitecture` を替え、`IPostBuildCleanup` で戻す（Profile が上書きしていても、書き出しの間は効くか）
  2. シミュレーター用の Profile（`Test iOS Simulator Profile`）を足す（Profile が 3 つになる）
- **bundle id**: テストの Player とハーネスは、サンプルのアプリ（`com.jonghyunkim.nativetoolkit.ios`）と別の bundle id にする（通知の権限の状態が混ざらないように。Share のシートで同じ名前のアプリと取り違えないように。Android の 2026-10-12 の判断と同じ）。テストの Player は `com.jonghyunkim.nativetoolkit.playertests`、ハーネスは `com.jonghyunkim.nativetoolkit.uiharness`、表示名もそれぞれ `NTK Player Tests`・`NTK UI Harness`。Profile の上書きを避けるため、**書き出した Xcode のプロジェクトの `PRODUCT_BUNDLE_IDENTIFIER` と `Info.plist` の表示名を `PBXProject` で替える**（V-3）
- **入れ直し**: 実行のたびに `simctl uninstall` してから入れる（通知の権限が未決の状態から始める）
- **書き出しを変える処理と戻す処理の置き場所**（R-5）: Test Runner は、属性をテストのクラスとメソッドからは Editor で組んだテストからしか集めない。iOS の Player のテストは Editor では組まれないので、そこに付けた属性は黙って効かない。そこで、`UnityEditor.TestRunner` を参照する Editor のアセンブリ（`Tests/Editor/NativeToolkit.IosPlayerTests.Editor.asmdef`、Editor だけ）に、**アセンブリ単位の** `[assembly: TestPlayerBuildModifier(...)]` と `[assembly: PostBuildCleanup(...)]` を置く。どちらも、対象が iOS のときだけ働き、Android・Windows の Player のテストの書き出しには何もしない。替えた Player Settings は cleanup で戻し、batchmode が途中で止まったときは 3.4 の戻しが拾う

### 3.3 Editor 専用のテストを分ける（F-16）

- `IosClipboardManagerIntegrationTests`（31 件）と `IosShareManagerIntegrationTests`（5 件）は `#if UNITY_IOS || UNITY_EDITOR` で、iOS の Player にも入る。Editor の偽の経路を前提にしているので、`#if UNITY_EDITOR` にする（Editor の 36 件はそのまま流れる）
- iOS の Player のテストは新しいファイルに置く（`Tests/PlayMode/Ios*PlayerTests.cs`、`#if UNITY_IOS && !UNITY_EDITOR`、カテゴリー `IosPlayer`）。段 2 で足すのは、1.x のまま画面を出さずに確かめられる最小のもの:

| 機能 | 段 2 で足すテスト（1.x） |
|---|---|
| Clipboard | 名前付きの pasteboard に書いて読む（文字列）。一般の pasteboard には書かない（設計書 v4 の I-13 のクリップボード） |
| Share | 空の入力での失敗の結果（シートを出さない経路） |
| Dialog | 引数の誤りでの失敗（アラートを出さない経路）。無ければ「呼べる」だけ |
| Notification | 権限の状態を読む（ダイアログを出さない） |

- 3-S で、これらは「使えない」を期待する形に替える（レビュー v2 の N-10）。機能を移すたびに元に戻す

### 3.4 Unity が書き換えるファイル

Android のスクリプトの `SIDE_EFFECT_PATHS`（`scripts/verify_unity_android.sh` の 140〜146 行）と同じ範囲（`Assets/Settings`〈Build Profiles を含む〉、`ProjectSettings`、`Assets/InitTestScene*`、`Assets/PerformanceTestRun*`）と `unity-native-plugin.slnx` を、実行の前から変わっていたものを除いて戻す。**(B) のハーネスの書き出しにも同じ戻しを掛ける**。`--keep-changes` で戻さない。

## 4. (B) UI のシナリオ（`scripts/test_ios_ui.sh`）

### 4.1 ハーネスのアプリ

- 置き場所: `Assets/NativeToolkitIosUiHarness/`（このリポジトリだけ。`package.json` の範囲の外）
  - `IosUiHarness.unity`（空のシーンに `IosUiHarnessController` を 1 つ）
  - `IosUiHarnessController.cs`（`#if UNITY_IOS`）: 起動の形（4.3）を読み、そのシナリオの行を重ね表示に登録する。各行の「流す」が押されたら、公開の Manager を呼び、イベントと結果を行の値に書く
  - `Scenarios/*.cs`: 機能ごとの行の定義（1 行 = 1 つの操作と、その結果の書き方）。**公開の API だけを呼ぶ**（1.x と `2.0.0` で同じハーネスが使えるように。I-5・I-6 で保つ範囲）
  - `NativeSource~/NtkUiHarnessOverlay.mm`: 重ね表示（4.2）。**`Plugins/iOS/` には置かない**（置くと、このリポジトリのすべての iOS のビルド〈サンプル、(A) の Player〉に入る）。ハーネスの書き出しが、書き出した Xcode のプロジェクトに写して UnityFramework に足す（I-1 の口と同じやり方）
  - `Editor/IosUiHarnessBuild.cs`: 書き出し（`-executeMethod`。シーンを 1 つだけ入れ、bundle id と表示名を替え、シミュレーター向けか実機向けか）。`Editor/` には Editor だけの asmdef を別に置く
- アセンブリは `NativeToolkit.IosUiHarness`（asmdef。`includePlatforms` は iOS と Editor）。**定義の制約 `NTK_UI_HARNESS` を付け、ハーネスの書き出しの間だけ定義する**（R-7。サンプルのアプリと (A) の Player に入れない）。定義の入れ方は V-2 と同じく、書き出しの間に替えて戻す。サンプルのアセンブリと混ぜない
- ビルドの処理は (A) と同じく Profile で決まる（既定は `iOS Profile`）

### 4.2 重ね表示（`NtkUiHarnessOverlay.mm`）

Unity の描く画面は XCUITest から見えない。UIKit の部品を Unity の view の上に重ねる。

- `UnityGetGLViewController().view` に、画面の下半分を覆う `UIScrollView` を足す（上半分は Unity の画面のまま。ダイアログやシートは上に出る）
- 行ごとに:
  - ボタン: `accessibilityIdentifier = row.<id>.run`、題 `<id>`。押すと C# に登録した関数ポインタ（`[MonoPInvokeCallback]`）を `<id>` で呼ぶ
  - 結果の文字: `accessibilityIdentifier = result.line.<連番>`、文字 `[<id>@<状態>] <値>`（native-toolkit の `ResultLog` と同じ形）。状態は `pending` / `ok` / `fail` / `event`
- C# からは `ntkh_overlay_add_row(id)`、`ntkh_overlay_append(id, state, value)`、`ntkh_overlay_clear()` を呼ぶ（`__Internal`）
- **C# のコールバックは必ず try / catch**（B-7。投げるとアプリが落ちる）。ボタンからの呼び出しも、Manager のイベントも、Unity の main の上で来る
- 値に入れるのは、テストが決めた値と、結果の種類・数・長さだけ。**ほかのアプリのデータ（一般の pasteboard の中身など）は値に入れない**（native-toolkit の「ログには行の長さしか出さない」と同じ）

### 4.3 シナリオの渡し方とコールドスタート

- XCUITest は `XCUIApplication(bundleIdentifier:)` の `launchArguments` に `-ntkScenario <名前>` と `-ntkRun <32 桁の 16 進>` を渡す。Unity は `Environment.GetCommandLineArgs()` で読む（V-4 で iOS の Player に届くことを確かめる）
- **コールドスタート**（通知のタップでの起動）は引数が届かない。ハーネスは、引数のある起動のたびに、その形を `Application.persistentDataPath/ntk-ui-harness/launch.json` に書き、引数の無い起動ではそれを読む。読んだときは結果の行の先頭に `[launch@ok] source=file` を出す（native-toolkit の `LaunchShape.swift` と同じ）

### 4.4 XCUITest のプロジェクト（`Tools~/IosUiTests/`）

- 置き場所はリポジトリの直下の `Tools~/IosUiTests/`（`~` で Unity が取り込まない。パッケージの外）
- ターゲットは 2 つ: 空のホストのアプリ `NtkUiTestHost`（UI テストのターゲットに要る。何もしない）と、UI テストのバンドル `NtkUiTests`。テストは `XCUIApplication(bundleIdentifier: "com.jonghyunkim.nativetoolkit.uiharness")` で起動する。**ハーネスのアプリは xcodebuild が入れないので、ランナーが先に入れる**
- 共通の部品（native-toolkit の `CApiUITestCase.swift`・`CApiUITestApp.swift` を写す）:
  - `HarnessApp`: 起動、`row(id).run()`、`waitResult(id, state, timeout)`、`resultValue(id)`
  - `SystemUI`: 通知の許可、バナー、長押しのアクション、返信、共有のシートの選択と閉じ方、ペーストの許可、アラートのボタン（英語と日本語のラベル）
  - 予期しないアラートの見張り（割り込みのモニターは押さずに印だけ付け、各操作の後と tearDown で見る）
- 待ち時間の既定は 10 秒（native-toolkit の設計 6.2 の表を写す）
- テストの名前は `<機能>UITests/test_<項目の ID>`（項目の ID は 3-R の確かめる項目の表の ID）
- Xcode は 26.3 を既定にし、`DEVELOPER_DIR` で 26.6 に替えられる（段 1 で、`2.0.0` は 26.3 で組めた）

### 4.5 段 2 で作るシナリオ（仕組みの確かめ）

段 2 では仕組みを確かめる最小の行だけを作る。機能ごとの確かめる項目は 3-R で表にしてから足す。

| シナリオ | 行 | XCUITest がすること | 確かめること |
|---|---|---|---|
| `dialog` | Dialog の確認（OK / Cancel）を出す | アラートの OK を押す | 結果の行が「OK が押された」になる。アラートの見つけ方とボタンの押し方 |
| `share` | 文字列を共有する | 共有のシートが出たのを見て、閉じる | 結果の行が「閉じた」になる。シートの見つけ方と閉じ方 |
| `notification` | 権限を求める、5 秒後の通知を予約する | 許可のダイアログで許可を押す。アプリを背景に回し、バナーを待ってタップする | 権限の結果、バナーのタップの結果の行。コールドスタートは段 2 では作らない（3-R で） |
| `clipboard` | 名前付きの pasteboard に書いて読む | 行を押すだけ | 結果の行の往復。ペーストの許可のダイアログは 3-R で |

### 4.6 ランナーと実機の守り

- `scripts/test_ios_ui.sh` が次を行う（native-toolkit の `test_ios.sh` の形を写し、小さくする）:
  1. `pgrep -x Simulator` で Simulator.app が開いていれば止まる
  2. ハーネスを書き出し（Unity）、組み（`xcodebuild`）、入れる（`simctl install`。実機は `devicectl device install app`）
  3. `xcodebuild build-for-testing` で `.xctestrun` を作り、テストごとに `test-without-building -only-testing:<テスト> -parallel-testing-enabled NO -resultBundlePath <tmp>` を呼ぶ
  4. `xcresulttool get test-results tests` で、流れたテストが名指ししたものとちょうど同じで、すべて通ったかを見る
  5. シミュレーターを元の状態（起動していなければ Shutdown）に戻す
- **シミュレーターか実機か**: `simctl list -j devices` に載っている UDID だけをシミュレーターとし、それ以外は実機として扱う（fail-closed）
- **実機**: `--dry-run` で、流すテストの一覧、各テストが端末で起こすこと（区分: `systemui`・`sharesheet`・`clipboard`・`reinstall`）、**端末に入るアプリの一覧と、それぞれを終わりに消すか**（(A) はテストの Player、(B) はハーネスと、`test-without-building` が入れる `NtkUiTestHost` と `NtkUiTests-Runner`。どれも終わりに消す）、HEAD と、ランダムな許可の ID を出して止まる（端末には触れない）。利用者がその一覧を見て許可したら、`--approved <ID>` で流す。許可の ID は一度しか使えない（台帳はリポジトリの外、`~/Library/Application Support/unity-native-plugin/test_ios-approvals.log`）。native-toolkit の仕組みの写し
- **承認の照らしの範囲**（Codex の 4）: 一覧には、HEAD に加えて、次の木の中身のハッシュを入れる: パッケージ（`Packages/com.jonghyunkim.nativetoolkit`）、ハーネス（`Assets/NativeToolkitIosUiHarness`）、XCUITest（`Tools~/IosUiTests`）、ビルドの設定（`Assets/Settings/Build Profiles`、`ProjectSettings`）、ランナー（`scripts/verify_unity_ios.sh`、`scripts/test_ios_ui.sh`、`scripts/ios_ui_runner.py`）。これらに未コミットの変更があれば止まる。**端末に触れる手順の直前ごとに照らし直す**（native-toolkit の `scripts/ios_ui_runner.py` の 2225・2233 行、`scripts/test_ios.sh` の 215 行と同じ）
- **(A) の実機の回も同じ承認を通す**（R-10）。`scripts/verify_unity_ios.sh` は、実機を渡されたら `ios_ui_runner.py` の承認の部分を使う
- **後片付け**: 実機の `.xcresult`・DerivedData・ログは `mktemp` のフォルダに置き、終わりの trap でフォルダごと消す（私物の画面の写しが入る）。ハーネスのアプリは消す（実機は `devicectl device uninstall app`）
- **実機で画面の階層を書き出さない**。失敗の文に springboard のラベルを書かない（native-toolkit の `device-testing.md`）

### 4.7 実機でだけ起きること

- 私物の通知が割り込むとバナーのテストが落ちる（native-toolkit で起きた）。利用者に、ハーネスの通知だけを許す集中モードを頼む（設定は agent が変えない）
- 実機ではペーストの許可のダイアログが出ないことがある（native-toolkit の CB1）。Clipboard の UI は 3-R で、シミュレーターだけのものと実機でも流すものを分ける

## 5. 1.x と `2.0.0` で同じに使う

- (A) のテストは 3-S で「使えない」を期待する形に替え、機能ごとに戻す。(B) のハーネスは公開の API だけを呼ぶので、3-S の前後で替えない
- 3-R の記録（1.x）と段 3 の比べ（`2.0.0`）は、**同じハーネスのコミットと同じ XCUITest のテスト**で流し、結果の行を並べて比べる。1.x と `2.0.0` で違ってよいもの（I-6 で保たない範囲）は、3-R の表に書いてからテストに入れる

## 6. 段 2 の作業の順

1. スパイク（7 章の V-1〜V-8）。コミットしない写しで行い、結果を README の 6 章に書く
2. (A): `verify_unity_ios.sh`、`IosPlayerResultWriter`、Editor 専用のテストの `#if`、iOS の Player の最小のテスト。シミュレーターで 1.x の Player の基準を取る
3. (B): ハーネス、重ね表示、XCUITest のプロジェクト、`test_ios_ui.sh`。シミュレーターで 4.5 のシナリオを 1.x のまま流す
4. 実機（許可を得て）: (A) と (B) を 1 回ずつ
5. 実装のレビュー（サブエージェントと Codex）

## 7. 要検証（段 2 のスパイク）

| # | 確かめること | 通らなかったとき |
|---|---|---|
| V-1 | `TestRunnerApi.Execute` に `buildOnly` を渡すと、iOS の Xcode のプロジェクトを書き出すだけで終わる（Simulator.app も Xcode も開かない）。batchmode で Execute が終わるまで待てる | `BuildPipeline.BuildPlayer` に `IncludeTestAssemblies` を付け、Test Runner のシーンを自分で用意する |
| V-2 | Simulator SDK と arm64 の選び方（3.2 の 1 → 2 の順） | シミュレーター用の Profile を足す |
| V-3 | 書き出した後に `PRODUCT_BUNDLE_IDENTIFIER` と表示名を替えて、組めて、その bundle id で起動する | Profile の Player Settings の上書きの `applicationIdentifier` を、実行の間だけ替えて戻す |
| V-4 | `simctl launch <id> -ntkRun <run>` の引数が iOS の Player の `Environment.GetCommandLineArgs()` に届く。XCUITest の `launchArguments` も同じく届く | 環境変数（`SIMCTL_CHILD_*`、`launchEnvironment`）で渡し、`.mm` で読む |
| V-5 | `TestRunCallback` が Player で呼ばれ、`persistentDataPath` に書いた XML を `simctl get_app_container <udid> <id> data` で読める。Editor の引数の `-doNotReportTestResultsBackToEditor` で、送る部品（`RemoteTestResultSender`）がテストのシーンに入っていない（Player のログに `No RemoteTestResultSender` が出る） | Player の中で `ITestRunCallback` を `PlaymodeTestsController` に足す別の方法を探す |
| V-6 | 重ね表示のボタンと文字が XCUITest から `identifier` で見え、押すと C# に届き、Unity の描画を止めない | 重ね表示を `UIWindow` にする |
| V-7 | 手で書いた最小の Xcode のプロジェクト（空のホストと UI テスト）が、`build-for-testing` と `test-without-building` で、別のアプリ（ハーネス）を bundle id で起動して押せる（シミュレーター） | ホストのアプリをハーネスに替える案を検討する（書き出しのたびの作り直しが要る） |
| V-8 | 1.x のまま、`iOS Profile` で書き出したハーネスが、シミュレーターで 4 つのシナリオを通る | 1.x の振る舞いとして記録し、ハーネスの側か XCUITest の側かを切り分ける |

## 8. テスト

- (A) の判定（XML の読み方、0 件・skip の扱い）と、(B) の判定（`xcresulttool` の出力と名指しの照らし）は Python の `doctest` か単体のテストで固める（Android の `android_ui_driver.py` と同じ）
- `IosPlayerResultWriter` の XML の形は EditMode のテストで、書いた XML をランナーと同じ読み方で読み直して確かめる
- 重ね表示の `.mm` は V-6 の XCUITest で確かめる（単体のテストは置かない）

## 9. 手に残る見込みのもの

native-toolkit でも手に残っているもの（設計書 v4 の I-12）: 通知センターを開く操作、通知の添付の画像、共有の画像の中身、通知の音、実機での貼り付けのコントロール。3-R の表で、どれを人が見るかを項目ごとに書く。

## 10. 決めていないこと

- (B) のシナリオを 3-R でどこまで足すか（項目の表で決める）
- (A) と (B) を 1 つのスクリプトにまとめるか（段 2 では分ける。Android の `verify_unity_android.sh` のように 1 つのゲートにするかは、段 2 の実装の後に決める）
- 実機で (A) を流すときの結果の取り出し（`devicectl device copy from --domain-type appDataContainer`。V-5 の実機版。許可を得て段 2 の 4 で確かめる）

## 11. レビュー v1 で直さなかったもの

レビュー v1 の判断（3 章）のとおり、A1 と中心契約の B だけを直した。次は設計に直し込まず、ここに残す。

| 指摘 | 区分 | 直さない理由 | どこで扱うか |
|---|---|---|---|
| R-2・Codex の 3（Player はテストの後に自分で終わらない。`PlayerQuitHandler` は Editor の指示でだけ終える） | A2 | 「終わるのを待つ」形で作ると上限で止まり、スパイクと実装で表に出る | V-5 と (A) の実装。ランナーは `done` の印を見たら `simctl terminate`・`devicectl device process terminate` で終える（3.1 の「自分で終わる」は、この形に読み替える） |
| R-3・Codex の 2（`PlayerLauncherTestRunSettings` は `internal`。buildOnly は Finder を開き、開いた Xcode を閉じうる） | A2 | コンパイルで表に出る | V-1。公開の `ITestPlayerBuildModifier` で `AutoRunPlayer` と `ConnectToHost` を外し、出力先を決める形（split build and run）を主にする。Finder と Xcode の振る舞いも V-1 で見る |
| R-6（ハーネスの `Editor/` が Player のアセンブリに入る） | A2 | iOS の書き出しのコンパイルで表に出る | 4.1 に Editor だけの asmdef を書いた（v2）。実装で確かめる |
| R-8（`IosPlayerResultWriter` の置き場所、XML の根の形、`[Preserve]`） | A2 | EditMode のテストのコンパイルと V-5 で表に出る | (A) の実装。XML の組み立てと iOS だけの属性を分け、根の形（`test-run` を自分で組むか、`test-suite` を読むか）を実装で決める |
| R-4 の後半（Android では Profile が上書きしても Test Runner の id が効いた。ハーネスの書き出しの SDK、戻した後に差分が残らないこと） | B | V-2・V-3 の結果で、bundle id の替え方も決まる。網羅の指摘 | V-2・V-3。3.4 の戻しは (B) にも掛けた（v2） |
| R-9（空のホストから別のアプリを起動する形は native-toolkit でも実機で通っていない。引数と環境変数の違い） | B | スパイクの範囲の指摘 | V-7 を実機でも段 2 の 4 で確かめる。V-4 で `launchArguments` と `launchEnvironment` を比べる |
| R-11（段 4 でハーネスが替わる。3-S から機能を移すまでの (B) の期待） | B | 3-R で、段ごとに流すシナリオと期待を表にする | 3-R の確かめる項目の表 |
| R-12（表示名は `PlistDocument` で替える。「読み」の意味） | C | 文言 | 実装 |

