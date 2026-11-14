# iOS の Player のテストと UI の自動化の設計書 v1 のレビュー v1

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-ui-automation-design-v1.md](../designs/2026-11-14-ios-ui-automation-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

A1 はありません。いちばん重いのは R-1 と R-2 です。どちらも Test Framework のソースで裏が取れた、Player の起動と終わり方に関する前提の誤りです。

### R-1 [B] 一次ソースとの照合 `-doNotReportTestResultsBackToEditor` は Player の起動の引数ではない
- 根拠: `PlayerLauncher.cs:104-110`（`PrepareScene`）。Editor が自分の `Environment.GetCommandLineArgs()` をビルドのときに見て、`RemoteTestResultSender` をシーンに入れるかを決めている。Player 側の読み取りは無い。設計の 47・53 行、V-4 は `simctl launch` に渡している。
- 壊れ方: 起動の引数に付けても効かない。送る部品はシーンに残り、`ConnectToHost`（`PlayerLauncher.cs:238`。buildOnly でも付く）のまま Editor を探し続ける。テストは流れる見込みなので失敗としては出ず、設計の根拠だけが誤りのまま残る。同じ Mac で別の Unity Editor が開いていると、そこへ繋がりうる。
- 直し案: この引数は Unity の batchmode のコマンドラインに渡す。`ITestPlayerBuildModifier` で `ConnectToHost` を外す。V-5 に「Player のログに `No RemoteTestResultSender` が出る」ことを足す（`TestResultRenderer.cs:103`）。

### R-2 [A2] 一次ソースとの照合・仕組みの成立 Player は自分で終わらない
- 根拠: `PlayerQuitHandler.cs:10-27` は、Editor から終了のメッセージ（`RemotePlayerTestController.cs:137`）が来たときだけ `Application.Quit` を呼ぶ。テストが終わると `TestResultRenderer.cs:106-107` の Close ボタンが出るだけ。設計の 48・49・55 行は「自分で終わる」と書いている。
- 壊れ方: 「終わるのを待つ」で作ると、15 分の上限まで止まる。`done` を待つ形でも、アプリはシミュレーターや実機の前面に残る。
- 直し案: `done` の印を見たら、ランナーが `simctl terminate` / `devicectl device process terminate` する。3.1 の流れの図と 55 行を直す。

### R-3 [A2] 一次ソースとの照合 buildOnly は公開の API で渡せない
- 根拠: `PlayerLauncherTestRunSettings.cs:6` は `internal`。`buildOnly` は `as PlayerLauncherTestRunSettings` でしか読まれない（`PlayerLauncher.cs:96, 227`）。`overloadTestRunSettings` は 1 つしか持てない。buildOnly が成功すると `EditorUtility.RevealInFinder` が呼ばれる（`:97-100`）。`ApplePlatformSetup.cs:76-87` は、ビルドの間に開いた Xcode を 1 つだけなら閉じる。
- 壊れ方: リフレクションが要る。Finder の窓が開きうる。ビルド中に利用者が開いた Xcode が閉じられうる。
- 直し案: 公式の「split build and run」の形を主にする。`ITestPlayerBuildModifier` で `AutoRunPlayer | ConnectToHost` を外し、`locationPathName` を指定し、batchmode では `PostBuildCleanup` で Editor を終える。V-1 の代わりの案（`PlaymodeTestsController` を自分で用意する）も internal に頼るので弱い。

### R-4 [B] 仕組みの成立 Profile の前提と SDK の切り替えの範囲
- 根拠: `Assets/Settings/Build Profiles/iOS Profile.asset:225` は `iOSSimulatorArchitecture: 1`（arm64）。設計の 63 行と v4 の I-13:184 の「両方とも 0」は `Test iOS Profile` だけに当てはまる。また Android では、Profile が `applicationIdentifier` を上書きしていても（`Test Android™ Profile.asset:194-197`）、Test Runner が `PlayerSettings.SetApplicationIdentifier` で付けた id（`AndroidPlatformSetup.cs:61-66`）の Player ができている（`verify_unity_android.sh:196`）。
- 壊れ方: 「Player Settings を替えても Profile が上書きする」は、この実績と合わない。V-2 の 1 は通る見込みで、bundle id も同じ方法で替えられるかもしれない（V-3 の後からの書き換えが要らなくなる）。一方で、その書き込みが Profile の asset に残るおそれがある。V-2 は Test Runner の経路だけで、ハーネスの書き出し（`IosUiHarnessBuild`）の SDK の選び方が抜けている。
- 直し案: 63 行を直す。V-2 に「ハーネスの書き出し」と「実行の後に Profile の asset と `ProjectSettings` に差分が残らない」を足す。

### R-5 [B 中心契約] 仕組みの成立 戻す処理の置き場所と範囲
- 根拠: `PlayerLauncher.cs:65, 79` で、`LoadTests(filter)` も `ExecutePostBuildCleanupMethods` も Editor でコンパイルしたテストを見る。クラスやメソッドに付けた属性は、そのテストからしか集めない（`AttributeFinderBase.cs:41-48`）。iOS の Player のテストは `#if UNITY_IOS && !UNITY_EDITOR`（設計の 72 行）なので、Editor には存在しない。`ModifyOptions` はどのプラットフォームの Player のテストのビルドでも呼ばれる（`PlayerLauncher.cs:345-358`）。
- 壊れ方: `PostBuildCleanup` を iOS のテストに付けると、黙って呼ばれない。SDK が Simulator のまま残る。3.4 の戻す一覧には `ProjectSettings` と Build Profiles が無いので、戻されない。modifier を target で分けないと、Android の `verify_unity_android.sh` の Player も変わる。
- 直し案: modifier と cleanup は、`UnityEditor.TestRunner` を参照する Editor のアセンブリに、アセンブリ単位の属性で置く。`target == iOS` のときだけ働かせる。3.4 の一覧は Android の `SIDE_EFFECT_PATHS`（`verify_unity_android.sh:140-146`。`Assets/Settings`、`ProjectSettings`、`InitTestScene*`、`PerformanceTestRun*`）と同じにする。

### R-6 [A2] 既存への影響 ハーネスの Editor フォルダーが Player のアセンブリに入る
- 根拠: 設計の 96・97 行。asmdef のある木の下では、`Editor` フォルダーという名前は特別扱いされない。
- 壊れ方: `IosUiHarnessBuild.cs` の `UnityEditor` が iOS の Player のコンパイルに入り、書き出しが止まる。
- 直し案: `Editor/` に Editor だけの asmdef を別に置く。

### R-7 [B] 既存への影響 ハーネスがこのリポジトリのすべての iOS のビルドに入る
- 根拠: asmdef が `includePlatforms: iOS`。`Assets/.../Plugins/iOS/*.mm` は既定で iOS で有効になる。
- 壊れ方: サンプルのアプリ（手で確かめるもの）にも、(A) の Player にも、重ね表示の `.mm` とハーネスのコードが入る。「サンプルと混ぜない」（97 行）が守られず、3-R の比べの対象も変わる。
- 直し案: 定義の制約（例: `NTK_UI_HARNESS`）を付け、`IosUiHarnessBuild` が書き出す間だけ有効にする。`.mm` も `PluginImporter` で同じ条件にする。

### R-8 [A2] 既存への影響 `IosPlayerResultWriter` の置き場所と XML の形
- 根拠: EditMode の asmdef（`Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`）は Runtime と Editor しか参照しない。`TestRunCallbackListener.cs:17-21` は、`UnityEngine.TestRunner` を参照するすべてのアセンブリから属性を集める。`test-run` の根の要素は Editor 側の `UnityEditor.TestRunner/Api/ResultsWriter.cs` だけが作る。
- 壊れ方: 8 章の EditMode のテストからはこの型が見えない。iOS で囲まないと、Android や Windows の Player の回でもファイルを書く。`ITestResult.ToXml` の根は `test-suite` なので、56 行の `test-run` の読みが合わない。
- 直し案: XML の組み立ては共有できる所に置き、iOS だけの属性と分ける。根の形を決める。ストリップに備えて `[Preserve]` を付ける（`TestRunCallbackAttribute.cs:65-67`）。

### R-9 [B] 一次ソースとの照合・仕組みの成立 native-toolkit と形が違う
- 根拠: native-toolkit の C ABI の UI テストは、`TEST_TARGET_NAME` がテストするアプリそのもの（`IosLibraryCApi.xcodeproj/project.pbxproj:854`）。シミュレーターだけ（`:809`、`device-testing.md`）。渡し方は `launchEnvironment`（`CApiUITestApp.swift:37-38`）。サンプルの UI テストもアプリが対象（`IosLibraryExample.xcodeproj/project.pbxproj:576`）。
- 壊れ方: 「空のホスト + 別に入れたアプリを bundle id で起動」は native-toolkit でも実機で通っていない。V-7 はシミュレーターだけ。「native-toolkit と同じ」（114・115 行）も、引数と環境変数の違いがあって正確でない。
- 直し案: 実機版の V-7 を段 2 の 4 に明記する。`launchEnvironment` を主にするかは V-4 で比べる。

### R-10 [B] 実機の安全 承認の範囲が足りない
- 根拠: `device-testing.md` の「1 回ごとに聞く操作」（入れ直し・削除）。設計の 24 行と 4.6。
- 壊れ方: (A) の実機の回には `--dry-run` と `--approved` の門が無い。それなのに入れ直しと `devicectl copy from` を行う。(B) では `test-without-building` が `NtkUiTestHost` と `NtkUiTests-Runner` を私物の iPhone に入れるが、承認の一覧にも後片付けにも載っていない。
- 直し案: (A) も同じ承認の仕組みに通す。一覧には入るアプリ 3 つと、それぞれを消すかどうかを書く。あわせて、Team ID は既に `iOS Profile.asset:276` にコミットされているので、11 行の「書かない」の前提を書き分ける。

### R-11 [B] 1.x と 2.0.0 の両方 段 4 でハーネスが替わる
- 根拠: v4 の 222 行。段 4 で iOS の通知の型を替える（破壊的変更）。設計の 8・160・161 行は「同じハーネスのコミット」を前提にしている。
- 壊れ方: Notification のシナリオは段 4 でコンパイルし直しが要る。段 4 で 1.x と同じ形のまま比べることはできない。3-S から各機能を移すまでの間、(B) でどれが落ちてよいのかも書かれていない。
- 直し案: 段ごとに、(B) で流すシナリオと期待を表にする。(B) も、入れる前に必ず消す（v4 の 174 行の、権限の状態の持ち越し）。

### R-12 [C] 文言
- 66 行: 表示名は `PBXProject` ではなく `PlistDocument` で替える。
- 55 行: 「読みが 3 分伸びなければ」の「読み」が何を指すか分からない。

| 区分 | 件数 |
|---|---|
| A1 | 0 |
| A2 | 4（R-2, R-3, R-6, R-8） |
| B | 7（R-1, R-4, R-5 中心契約, R-7, R-9, R-10, R-11） |
| C | 1（R-12） |

## 2. Codex

1. **A1 — 結果送信を止める引数の指定先が違う。**
   設計: 53行、178行。
   根拠: PlayerLauncher.cs:107（Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEditor.TestRunner/TestLaunchers/）は、Editor 側の引数を調べて、書き出すシーンに RemoteTestResultSender を追加するか決める。追加済みの送信側は RemoteTestResultSender.cs:97（UnityEngine.TestRunner/TestRunner/Callbacks/）で接続を待ち、119行で送信する。
   壊れる点: Player 起動時に引数を渡しても送信側はシーンに残り、Editor に接続すれば結果を送る。V-4（引数到達）、V-5（XML 回収）、第8章の判定テストはいずれも通り得るため、設計自身の検証では捕まらない。
   修正: 書き出しを行う Unity Editor の起動引数に指定する。送信側がシーンに入っていないことも検証する。

2. **A2 — PlayerLauncherTestRunSettings は外部から直接使えない。**
   設計: 52行。
   根拠: PlayerLauncherTestRunSettings.cs:6 が `internal class`。
   壊れる点: プロジェクトの Editor コードで直接生成し overloadTestRunSettings に渡す実装はコンパイルできない。V-1 で判明する。
   修正: 公開されている ITestPlayerBuildModifier で AutoRunPlayer を外し、出力先を指定する。実装例は TestPlayerBuildModifierAttribute.cs:60（UnityEditor.TestRunner/NUnitExtension/Attributes/）にある。

3. **A2 — PlayerQuitHandler はテスト完了で自動終了しない。**
   設計: 49行、55行。
   根拠: PlayerQuitHandler.cs:10 は Editor からの終了メッセージを登録し、受信時だけ Application.Quit() を呼ぶ。34行の RunFinished は空。
   壊れる点: Editor を使わない構成では、49行のアプリ終了待ちがタイムアウトする。55行の `done` 待ちなら結果は回収できるが、Player は終了しない。
   修正: 結果ライターが XML と `done` の書き込み後に終了処理を行う。または、ランナーが `done` を回収した後にアプリを終了させる。

4. **B — 承認の照合対象からハーネスとランナーの変更が漏れる。**
   設計: 149行。
   根拠: native-toolkit の scripts/ios_ui_runner.py:2225 はランナー自身の変更を、2233行はアプリを含む ios/ の未コミット変更を拒否する。scripts/test_ios.sh:215 は端末操作の前ごとに再検査する。
   壊れる点: 設計に列挙された HEAD、テストフォルダのハッシュ、選択一覧だけでは、承認後にハーネスの Scenarios/*.cs を編集しても照合値が変わらない。承認時と異なる動作を実機で実行できる。
   修正: ハーネス、実行対象パッケージ、ビルド設定、ランナーも承認対象に含める。端末操作の直前に再照合する。

5. **C — 両 Profile のアーキテクチャ値が 0 という記述は現状と異なる。**
   設計: 63行。
   根拠: Assets/Settings/Build Profiles/iOS Profile.asset:225 は `iOSSimulatorArchitecture: 1`。Test iOS Profile.asset:221 は 0。
   影響: V-2 の前提となる現状説明が誤っている。
   修正: Profile ごとの実値に訂正する。

（注）上記は Codex が一次ソースを読んで出した結果です。私自身は行番号を再確認していません。（中継したサブエージェントの注）

## 3. 判断

- **A1 は Codex の 1 の 1 件**（サブエージェントの R-1 と同じ件で、サブエージェントは B とした）。A1 として v2 で直す: 引数は書き出す Editor の起動の引数に渡し、`ConnectToHost` を外し、送る部品がシーンに無いことを V-5 で確かめる
- **中心契約の B として v2 で直すもの**:
  - R-5（戻す処理の置き場所と範囲。サブエージェントが中心契約とした）
  - Codex の 4 と R-10（実機の承認の範囲。実機を守る仕組みはこの設計の中心の 1 つ）
  - R-7（ハーネスがサンプルのアプリと (A) の Player に入る。「サンプルと混ぜない」はハーネスの前提）
  - Codex の 5・R-4 の前半（Profile の値の誤り。V-2 の前提なので、1 語だけ直す）
- **直さないもの**（v2 の 11 章に理由つきで書く）: A2 の R-2・R-3・R-6・R-8・Codex の 2・3（スパイクと実装で表に出る。実装の確かめの一覧に写す）、B の R-4 の後半・R-9・R-11（スパイクと 3-R で扱う）、C の R-12
