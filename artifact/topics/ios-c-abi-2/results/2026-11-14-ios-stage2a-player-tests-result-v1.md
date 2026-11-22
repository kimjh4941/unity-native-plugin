# 段 2 (A) iOS の Player のテストの実装結果 v1

- 記録日: 2026-11-14
- 設計: [../designs/2026-11-14-ios-ui-automation-design-v4.md](../designs/2026-11-14-ios-ui-automation-design-v4.md) の (A)（3 章）。スパイクの結果は [README](../README.md) の 6.4
- ブランチ: `feature/UNT-14`

## 1. 作ったもの

| ファイル | 中身 |
|---|---|
| `scripts/verify_unity_ios.sh` | EditMode、Editor の PlayMode、iOS の Player（シミュレーター）を流すゲート。長い手順はすべて上限の時間と「ログが伸びない時間」の上限つき（`run_limited`）。Simulator.app が開いていれば止まる。`--skip-editmode`、`--keep-changes`。`--as-package-user` と `--device` は今は断る（2 章） |
| `scripts/ios_player_results.py` | Player が書いた NUnit の結果を判定する（`IosPlayer` のテストが 1 件以上流れ、失敗も skip も無いこと）。`doctest` 7 件 |
| `Assets/NativeToolkitIosPlayerTests/Editor/IosPlayerBuildSetup.cs`（+ asmdef） | このリポジトリだけ。アセンブリ単位の `TestPlayerBuildModifier` と `PostBuildCleanup`。Editor の引数 `-ntkIosPlayerSdk simulator|device` と対象が iOS のときだけ働き、書き出すだけにし（`AutoRunPlayer`・`ConnectToHost`・`WaitForPlayerConnection` を外す）、出力先、Simulator SDK と arm64、bundle id（`com.jonghyunkim.nativetoolkit.playertests`）を替える。cleanup でテストのシーンの `RemoteTestResultSender` の数をログに出し、Player Settings を戻し、Editor を終える |
| `Assets/NativeToolkitIosPlayerTests/Runtime/IosPlayerResultWriter.cs`（+ asmdef。iOS だけ） | `TestRunCallback`。環境変数 `NTK_RUN`（16 進）があるときだけ、結果を `Documents/ntk-player-tests/<run>.xml`（一時のファイルから名前を変える）と `<run>.done` に書く |
| `Tests/PlayMode/Ios{Clipboard,Share,Dialog,Notification}PlayerTests.cs`、`IosPlayerTestSupport.cs` | iOS の Player のテスト 4 件（カテゴリー `IosPlayer`。Android と同じく `#if UNITY_IOS` と `#if !UNITY_EDITOR` で囲む）。画面を出さない |
| `Tests/PlayMode/IosClipboardManagerIntegrationTests.cs`、`IosShareManagerIntegrationTests.cs` | `#if UNITY_IOS || UNITY_EDITOR` を `#if UNITY_EDITOR` にした（iOS の Player のコンパイルを止めていた。F-16） |

Player のテスト 4 件（1.x）:

| テスト | 何を確かめる | ネイティブを通るか |
|---|---|---|
| `IosClipboardPlayerTests.NamedPasteboardRoundTripsPlainText` | 名前付きの pasteboard を作り、文字列を書いて読み、消す（一般の pasteboard には触れない） | 通る |
| `IosNotificationPlayerTests.AuthorizationIsNotDeterminedOnAFreshInstall` | 入れ直した直後の通知の権限が `NotDetermined`（ダイアログは出さない） | 通る |
| `IosSharePlayerTests.ShareWithoutItemsFailsWithoutASheet` | 項目なしの Share が失敗し、シートを出さない | 通らない（1.x は C# で断る） |
| `IosDialogPlayerTests.ActionSheetWithoutOptionsIsRejected` | 選択肢なしのアクションシートが断られる | 通らない（1.x は C# で断る。ネイティブに届く経路はすべてアラートを出す） |

## 2. 設計からの違い

| 設計 | 実装 | 理由 |
|---|---|---|
| 3.1「結果の書き出し: テストのアセンブリに `TestRunCallback` を置く」 | このリポジトリの `Assets/NativeToolkitIosPlayerTests/Runtime` の、iOS だけのアセンブリに置いた | パッケージに置くと、testables に入れた利用者の iOS の Player のテストにも入る（R2-2 と同じ考え）。環境変数 `NTK_RUN` が無ければ何もしない |
| 3.1「`<run>` は起動の引数 `-ntkRun`」 | 環境変数 `NTK_RUN`（`SIMCTL_CHILD_NTK_RUN`） | iOS の Player に起動の引数は届かない（スパイクの V-4） |
| 3.2「`--as-package-user` の回は書き出しの後に `IosFrameworkPatcher.Apply` を呼ぶ」 | `--as-package-user` を断る（3-S で足す） | `IosFrameworkPatcher` のアセンブリは `NATIVETOOLKIT_ENABLE_BUILD_STEPS` が無いと組まれず、`Test iOS Profile` には無いので呼べない |
| 4.6「(A) の実機の回も承認を通す」 | このコミットでは `--device` を断る。(B) の後に足した（(B) の実装結果の 6 章） | 承認の仕組みは (B) で作った |
| 3.2「表示名を `NTK Player Tests` に替える」 | 替えていない（bundle id だけ替える） | (A) の 4 件は共有のシートを出さず、同じ名前のアプリと取り違える場面が無い。シートを出すテストは (B) のハーネスで行い、ハーネスは表示名を替える（実装のレビュー v1 の R-9） |
| 8 章「`IosPlayerResultWriter` の XML の形は EditMode のテストで確かめる」 | EditMode のテストは置かず、判定の Python を `doctest` で固め、スパイクと本番の Player の結果のファイルで確かめた | 書き手は NUnit の `ToXml` をそのまま書くだけで、組み立てる部分が無い。アセンブリは iOS だけで、EditMode から見えない |

## 3. 確かめ

- `scripts/verify_unity_ios.sh`（2026-11-14 17:34〜、このリポジトリ、`iOS Profile`、Xcode 26.3、iPhone 16 / iOS 18.3）:
  - EditMode: 1419 件中 1418 件成功、失敗 0、skip 1（Windows だけのテスト。段 0 の基準と同じ）
  - PlayMode（Editor）: 300 件すべて成功（段 0 の基準と同じ。Editor 専用にした iOS の 36 件を含む）
  - iOS の Player: 1 回目は Player のテストのコンパイルで止まった（結果の型が `readonly struct` で、`Nullable<T>` から `.Value` を通さずに読んでいた）。直して `--skip-editmode` で流し直し、**`IosPlayer: 4 ran, 4 passed, 0 failed, 0 skipped`**。書き出しのログに `remote-sender-count=0 runner-callback-count=1`
- 終わった後: 作業木は実行の前と同じ（実行の前から変わっていた 2 つのファイル以外に差分なし）。スクリプトが起こしたシミュレーターは止まっていた。Simulator.app は開いていない
- `python3 -I -m doctest scripts/ios_player_results.py`: 7 件成功
- 時間: EditMode と Editor の PlayMode で約 10 分、Player（書き出し 1 分、組み立て 3〜4 分、実行 数秒）

**これが 1.x の iOS の Player の基準**（段 0 の残り。設計書 v4 の I-13「Player の基準はこの後に取る」）。3-S で、この 4 件は「使えない」を期待する形に替える（レビュー v2 の N-10）。

## 4. 実装のレビュー v1 を受けて直したこと

[レビュー v1](../reviews/2026-11-14-ios-stage2a-player-tests-review-v1.md)（サブエージェントと Codex）の指摘を、利用者の指示（推奨案で決める）に従って直した。

| 指摘 | 直したこと |
|---|---|
| Codex の重大（`Plugins/iOS` の実行前の変更が、ビルドの処理に上書きされて戻せない） | 実行の前に `Plugins/iOS` に変更があれば止まる |
| R-2・Codex（シグナルで止めても、裏の Unity と xcodebuild が残る） | `run_limited` が動かしている子を覚え、終わりの trap が子と孫を止めて（TERM、10 秒で KILL）から戻す |
| R-1（Unity の手順で、止まった判定が空のファイルを見ていた） | `run_limited` に見張るファイルの引数を足し、Unity では `-logFile` を見張る |
| R-3（時間切れで TERM だけ） | TERM の後 10 秒で KILL。孫まで止める |
| R-4（書き出しの成否と、アプリが落ちたことを見ていない） | 書き出しのログで `Build Finished, Result: Success` を要件にした。起動したプロセスが終われば待つのをやめる |
| R-5・Codex（スイートの失敗、一部だけ流れた実行を通す） | 落ちたスイートがあれば失敗。カテゴリーの外のケース、根の `testcasecount` と数が違うことも失敗。`doctest` は 9 件 |
| Codex（Editor の回で、Unity の終了コードと、成功 0 件を見ていない） | Unity の終了コードが 0 でなければ失敗。成功が 1 件も無ければ失敗 |
| R-6（時間切れのとき `finally` が走らない） | `WaitFor` は例外を投げずに待つだけにし、各テストが待った後に確かめる |
| R-7（実行前からの変更を `printf | grep` で比べていた） | here-string で比べる。中身の値の比較は足していない |
| R-8（古い `.app` を入れうる、Xcode のそろえ） | 組む前に `-derivedDataPath` を消し、`.app` がちょうど 1 つであることを確かめる。`DEVELOPER_DIR` を `XCODE_APP` にそろえる |

直した後に `scripts/verify_unity_ios.sh` を最初から流し直した（2026-11-14 17:54〜）: EditMode 1419 件中 1418 件成功（Unity の終了コード 0）、Editor の PlayMode 300 件成功、iOS の Player **`IosPlayer: 4 ran, 4 passed, 0 failed, 0 skipped`**、`PASSED`。止める仕組みは、関数を取り出してダミーの命令で確かめた: TERM を送ると子と孫が止まり（3 → 0）、ログが 4 秒伸びない命令は 124 で止まり、孫は残らない。

直さなかったもの: R-9（2 章の表に書いた）、R-10（`Plugins/iOS` の戻しは、3-S で VERSION.txt を 2.0.0 にする前に見直す。今の 1.x では起きない）。

## 5. 残っていること

- (B) UI の自動化（ハーネス、XCUITest のプロジェクト、`scripts/test_ios_ui.sh`、実機の承認）→ 済み（[2026-11-14-ios-stage2b-ui-automation-result-v1.md](2026-11-14-ios-stage2b-ui-automation-result-v1.md)）
- (A) の実機の回（承認の後）、`--as-package-user`（3-S）→ 済み。実機は 2026-11-14（[README](../README.md) の 6.6。4 件成功）。`--as-package-user` は 3-S で足した（[2026-11-15-ios-c-abi-3s-switch-result-v1.md](2026-11-15-ios-c-abi-3s-switch-result-v1.md) の V-3）
- 実装のレビュー（サブエージェントと Codex）→ 済み（[../reviews/2026-11-14-ios-stage2a-player-tests-review-v1.md](../reviews/2026-11-14-ios-stage2a-player-tests-review-v1.md)。直しは 4 章）
