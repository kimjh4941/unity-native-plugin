# 段 2 (B) iOS の UI の自動化の実装結果 v1

- 記録日: 2026-11-14
- 設計: [../designs/2026-11-14-ios-ui-automation-design-v4.md](../designs/2026-11-14-ios-ui-automation-design-v4.md) の (B)（4 章）。スパイクの結果は [README](../README.md) の 6.4。(A) は [2026-11-14-ios-stage2a-player-tests-result-v1.md](2026-11-14-ios-stage2a-player-tests-result-v1.md)
- ブランチ: `feature/UNT-14`
- 利用者の指示（2026-11-14 18 時ごろ）: 利用者が話しかけるまでは自動で進め、判断は推奨案で決め、コミットと push もしてよい。20 時までは実機を使わない

## 1. 作ったもの

| ファイル | 中身 |
|---|---|
| `Assets/NativeToolkitIosUiHarness/`（このリポジトリだけ） | UI のハーネス。`Runtime/IosUiHarness.cs`（`[RuntimeInitializeOnLoadMethod]` で始まり、環境変数 `NTK_SCENARIO` のシナリオの行を重ね表示に並べる。引数の無い起動〈通知からのコールドスタート〉は前の起動が残したシナリオを読み、`source=file` と出す）、`Runtime/Scenarios/*.cs`（Dialog・Share・Notification・Clipboard。公開の API だけを呼ぶ）、`NativeSource~/NtkUiHarnessOverlay.mm`（UIKit の重ね表示: ボタン `row.<id>.run`、結果の行 `result.line.<n>`）、`Editor/IosUiHarnessBuild.cs`（書き出し）、`IosUiHarness.unity`（空のシーン） |
| `Tools~/IosUiTests/` | 手で書いた（スクリプトで生成した）XCUITest のプロジェクト。空のホスト `NtkUiTestHost` と UI テスト `NtkUiTests`（`HarnessUITestCase`、`SystemUI`、機能ごとのテスト 4 件）、共有のスキーム、`DeviceRequirements.json`（各テストが端末ですること） |
| `scripts/test_ios_ui.sh` | ハーネスを書き出して組み、UI テストを組み、テストごとにハーネスを入れ直して `test-without-building -only-testing` で流し、結果のバンドルを判定する。シミュレーターと、承認つきの実機 |
| `scripts/ios_ui_runner.py` | テストの一覧、実機の承認（発行、照らし合わせ、1 回だけ使う）、結果の判定（`xcresulttool` の JSON。流れたテストが名指ししたものとちょうど同じで、すべて成功）。`doctest` 16 件 |
| `scripts/ios_script_common.sh` | `verify_unity_ios.sh` と共有する部品（`run_limited`、子と孫を止める、戻す、シミュレーターを探す）。(A) の実装のレビューで直した形をそのまま移した |

ハーネスの書き出し（`IosUiHarnessBuild.Export`）:

- ハーネスのアセンブリは定義の制約 `NTK_UI_HARNESS` つきで、この書き出しだけが `extraScriptingDefines` で定義を渡す。サンプルのアプリと (A) の Player には入らない
- bundle id `com.jonghyunkim.nativetoolkit.uiharness`、表示名 `NTK UI Harness`。シミュレーターでは Simulator SDK と arm64。Player Settings は書き出しの後に戻す
- 重ね表示の `.mm` は `Plugins/iOS` に置かず、書き出したプロジェクトに足す
- 実機では自動の署名（`-allowProvisioningUpdates` なし。段 1 の S-9 と同じ）

UI テスト（パイロット。機能ごとの確かめる項目は 3-R で表にしてから足す）:

| テスト | 押すもの | 確かめること |
|---|---|---|
| `DialogUITests/test_confirmOk` | 確認のダイアログの OK | `[confirm@ok] OK` |
| `ShareUITests/test_dismiss` | 共有のシートを閉じる | `[text@ok] completed=False activity=none` |
| `NotificationUITests/test_permissionAndBannerTap` | 通知の許可（springboard）、ホームに戻してバナー | `[permission@ok] granted`、`[schedule@ok] scheduled`、`[tap@event] id=ntkh-1 action=com.apple.UNNotificationDefaultActionIdentifier` |
| `ClipboardUITests/test_namedRoundTrip` | 行を押すだけ（名前付きの pasteboard） | `[named@ok] items=1 match=True` |

どのテストも、予期しないアラートが出たら失敗にする（割り込みのモニターは押さずに印だけ付け、各手順と tearDown で印と springboard を見る）。失敗の文に springboard のラベルを書かない。

## 2. 実機の守り（設計書 4.6）

- `--device --dry-run`: 端末に触れない。守る木（パッケージ、ハーネス、(A) の書き出しの部品、XCUITest、Build Profiles、`ProjectSettings`、5 つのスクリプト）に未コミットの変更があれば止まる。流すテスト、各テストが端末ですること、入れて消すアプリ 3 つ（ハーネス、`uitesthost`、`uitests.xctrunner`）を出し、ランダムな許可の ID を出す。記録はリポジトリの外（`~/Library/Application Support/unity-native-plugin/test_ios-approvals/`）
- `--device --approved <ID>`: 許可の記録が、同じテスト、同じ HEAD、守る木の HEAD の木の値が同じで、未コミットの変更が無く、まだ使われていないことを確かめてから使い済みにする。**端末に触れる手順の前ごとに**照らし直す（Unity の手順の後は戻してから）。端末は、つないだ iPhone がちょうど 1 台（ペア済み、開発者モード）のときだけ選び、識別子を出さない。ログと結果のバンドル（端末の画面の写しが入る）は一時のフォルダに置き、終わりに消す。端末に入れたアプリはすべて消し、残っていれば警告する
- `--keep-changes` は実機と一緒に使えない

## 3. 確かめ

- `scripts/test_ios_ui.sh`（シミュレーター、2026-11-14 18:03〜、`iOS Profile`、Xcode 26.3、iPhone 16 / iOS 18.3）: 書き出し、ハーネスの組み立て、UI テストの組み立てが通り、**4 件とも成功**（`PASSED`）。終わった後、スクリプトが起こしたシミュレーターは止まり、入れたアプリは消え、作業木は実行前と同じ（ハーネスの書き出しが作った `IosUiHarness.unity` を除く。これはコミットする）。Simulator.app は開いていない
- 断る経路: `--dry-run` だけ、`--device` だけは止まる。`--device --dry-run` は、未コミットの木を挙げて止まった
- `python3 -I -m doctest scripts/ios_ui_runner.py`: 成功（レビューの直しの後は 16 件。5 章）
- **実機は確かめていない**（20 時まで実機を使わない、という利用者の指示）。`--device` の経路（承認、署名、`devicectl`、UI テストのランナーを実機に入れる署名）は、利用者が戻ってから、許可を得て確かめる

## 4. 設計からの違い

| 設計 | 実装 | 理由 |
|---|---|---|
| 4.3「`launchArguments` に `-ntkScenario` と `-ntkRun`」 | 環境変数 `NTK_SCENARIO`（`launchEnvironment`）。run は渡さない | 起動の引数は iOS の Player に届かない（スパイク V-4）。結果は重ね表示から読むので、run の印は要らない |
| 4.4 テストの名前「`<機能>UITests/test_<項目の ID>`」 | `test_confirmOk` など、パイロットの名前 | 項目の ID は 3-R の表で決まる。そのときに付け直す |
| 4.6 承認の台帳 `test_ios_ui-approvals.log` | 承認ごとの JSON（`test_ios-approvals/<ID>.json`） | 1 回だけ使う印と、照らす値を同じ所に持てる |
| 4.6 承認で照らす範囲（設計書 v4 の一覧） | 同じものに、共有の部品 `scripts/ios_script_common.sh` を足した | 実機の手順を決める部品なので |

## 5. 実装のレビュー v1 を受けて直したこと

[レビュー v1](../reviews/2026-11-14-ios-stage2b-ui-automation-review-v1.md)（サブエージェントと Codex）の指摘を、利用者の指示（推奨案で決める）に従って直した。どちらも、実機の経路は読むだけで確かめた。

| 指摘 | 直したこと |
|---|---|
| R-1・Codex の 1（重大。使い済みにした承認を、端末の手順の前の照らし直しが必ず断る。実機の回は 1 件目の前で止まる） | `use-approval` が実行ごとの印（この実行だけが知るランダムな値）を記録に書いて返す。端末の手順の前の照らし直しは新しい子コマンド `guard`（この印の実行が使った承認で、テスト・HEAD・木が同じ）で行う。`doctest` は 16 件 |
| Codex の 2（重大）・R-1 の後半（照らし直しに通らなくても、後片付けが端末のアプリを消す） | 端末に触れた印は、照らし直しが通った後、最初に入れる直前に立てる。何も入れていない実行は、端末に何もしない。**入れた後の後片付けの uninstall は照らし直しなしで行う**と決め、スクリプトの説明に書いた（承認の一覧に「入れて、終わりに消す」と書いた 3 つの bundle id だけ。残すほうが、承認していないものを利用者の端末に残す） |
| R-2・Codex の 3（`xcodebuild` に devicectl の識別子を渡していた） | `xcodebuild` には `hardwareProperties.udid`、`devicectl` には `identifier`。どちらも出力しない |
| R-3（Team を名前順の最初の profile から取る。エラー行から個人の値が出うる） | 期限内の profile の Team を集め、ちょうど 1 つのときだけ使う。実機の回の組み立てのエラーは件数だけを出す |
| R-4（共有シートを座標で閉じる） | 消した。閉じるボタンが無ければ失敗 |
| R-5（`test-without-building` が一時フォルダの外に書きうる） | `-derivedDataPath` を一時フォルダの中にした |
| R-6（後片付けの devicectl に上限が無い、残りの確かめが失敗を見逃す） | 上限つきにし、アプリごとに JSON で読む。読めなければ警告 |
| R-7・Codex の 4（xcodebuild の終了コードを見ていない、空の一覧） | 終了コード 0 と結果の判定の両方を要件にした。一覧が空なら止まる |
| R-8 の一部（照らす範囲） | `Packages/manifest.json`、`Packages/packages-lock.json`、`Assets/Editor`、`Assets/Plugins` を足した |
| R-9（`Start` の例外、`.mm` の文字コード、アプリのアラートの見落とし） | `Start` を try/catch で包む。`.mm` は `%@` と `stringWithUTF8String:`。tearDown でハーネスのアラートも見る |
| R-10（通知の 5 秒が実機では際どい） | 10 秒にし、バナーを待つ上限を 30 秒にした |

直した後に `scripts/test_ios_ui.sh` をシミュレーターで流し直した（2026-11-14 18:2x〜）: 書き出しと 2 つの組み立てが通り、**4 件とも成功**（`PASSED`）。`verify_unity_ios.sh --skip-editmode` も、共有の部品に移した後に流し直して `IosPlayer: 4 ran, 4 passed` だった。

直さなかったもの（R-8 の残り）: 承認に端末のハッシュを持たない（dry-run は端末に触れないので、どの端末かを知らない）。同じ ID の同時実行のロックは無い（利用者 1 人の手元の実行で、同時に 2 つ流さない）。ペア済みの iPhone が 2 台あると断る（止まる向き）。

## 6. 残っていること

- 実機（許可を得て）: `test_ios_ui.sh --device`（承認、署名、4 件）と、(A) の実機の回（`verify_unity_ios.sh --device` は、承認の仕組みができたので足せる）
- 実装のレビュー（サブエージェントと Codex）
