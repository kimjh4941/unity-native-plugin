# iOS Dialog の 1.x の記録（UI テスト計画 v3 の 5 章）v1

- 記録日: 2026-11-14
- 計画: [../designs/2026-11-14-ios-dialog-ui-test-plan-v3.md](../designs/2026-11-14-ios-dialog-ui-test-plan-v3.md)（5 章「1.x の記録」をこのファイルに書く。計画の版は上書きしない）
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3-R
- 対象: 1.x（`Plugins/iOS/VERSION.txt` の dist 1.13.0、`ios-native-toolkit-1.3.0` と `unity-ios-native-toolkit-1.3.0`）
- 実行: `scripts/test_ios_ui.sh`（`iOS Profile`、Xcode 26.3、iPhone 16 / iOS 18.3 のシミュレーター、Simulator.app は開かない）。テストは `Tools~/IosUiTests/NtkUiTests/DialogUITests.swift`、ハーネスは `Assets/NativeToolkitIosUiHarness/Runtime/Scenarios/DialogScenario.cs`
- 実機: 2026-11-14 に iPhone XS（iOS 18.7.2）で記録した（3.5 章。シミュレーターだけの ID 12・13・15・16・17 は流していない）

## 1. シミュレーターの記録（2026-11-14 19:36〜、19:41〜）

各テストは、ハーネスを入れ直して 1 件ずつ流し、結果の行を全文一致で比べ、押した後 3 秒待ってからその id の結果の行の数を数えた（計画 v3 の 1 章）。

| ID | 結果 | 1.x の結果の行（実測） | 計画の予想との関係 |
|---|---|---|---|
| ID-1 | 成功 | `[dialog@ok] button=OK` | 予想どおり |
| ID-2 | 成功 | `[dialog@ok] button=了解` | 予想どおり（日本語の文言がそのまま返る） |
| ID-3 | 成功 | `[confirm@ok] button=OK` | 予想どおり |
| ID-4 | 成功 | `[confirm@ok] button=Cancel` | 予想どおり（キャンセルも成功で、押した文言） |
| ID-5 | 成功 | `[destructive@ok] button=Delete` | 予想どおり |
| ID-6 | 成功 | `[destructive@ok] button=Cancel` | 予想どおり |
| ID-7 | 成功 | `[sheet@ok] button=B` | 予想どおり |
| ID-8 | 成功 | `[sheet@ok] button=Cancel` | 予想どおり |
| ID-9 | 成功 | `[text@ok] button=OK input=ntk42` | 予想どおり |
| ID-10 | 成功 | 空のとき OK は押せない。`[text@ok] button=Cancel input=<null>` | 予想どおり（v2 で直した予想） |
| ID-11 | 成功 | `[text@ok] button=OK input=""` | 予想どおり |
| ID-12 | 成功 | `[login@ok] button=Login username=user1 password-length=6` | 予想どおり |
| ID-13 | 成功 | 空のとき Login は押せない。`[login@ok] button=Cancel username=<null> password=<null>` | 予想どおり |
| ID-14a | 成功（2 回目の流し直し） | **2 つとも出て、2 つ目が上**。上から押して `[confirm@ok] button=OK2`、`[confirm@ok] button=OK1`。どちらもちょうど 1 回 | **予想と違った**。計画は「2 つ目は 0 回」（レビュー v1 の R-2 の読みで、レビュー v2 のサブエージェントも支持した）。1 回目の流しは、2 つ目のアラートが 1 つ目の `OK1` を覆って押せずに落ちた。テストを「上から 2 つとも押す」に直して流し直した。**`2.0.0` の「両方出る」は既知の差分ではなく「同じ」になる** |
| ID-14b | 成功 | 1 つ目が出ていることを確かめてから（`[twice@event] first-shown=yes`。実装のレビューの後に足した）2 つ目を呼ぶと、2 つとも出て、2 つ目が上。`[confirm@ok] button=OK2`、`[confirm@ok] button=OK1` | 予想どおり |
| ID-15 | 成功 | 背景から戻ってもアラートが残り、`[dialog@ok] button=OK` | 予想どおり |
| ID-16 | 成功 | `[login@ok] button=Login username="" password-length=0` | 予想どおり |
| ID-17 | 成功 | 遅らせた呼び出しは `foregroundInactive` で動き（`[late@event] state=foregroundInactive`）、`[dialog@fail] button=<null> error=Failed to get root view controller` がちょうど 1 回。アラートは出ない | 予想どおり（v3 で直した予想）。**`2.0.0` では出て押せる見込みで、既知の差分の候補のまま** |

同じ回の、ほかの UI テスト（Share・Notification・Clipboard のパイロット）も成功した。

## 2. 計画 v3 との違い（計画の版は上書きしない）

- ID-14b の 2 つ目の呼び出しの遅れは、計画の「C# の側で 1 秒後」ではなく、ネイティブの `dispatch_after`（ID-17 と同じ仕組み）で作った。2 つ目を呼ぶ直前に、1 つ目が出ていることを結果の行に書いて確かめる
- ID-17 の 1.x の行は、計画の予想に無い `button=<null>` を含む（`[dialog@fail] button=<null> error=Failed to get root view controller`）。ハーネスは失敗のときもボタンの値を書くため。テストはこの全文で比べる
- 結果の行は、Dialog の 6 種類すべての完了の行をまとめて期待の配列と比べる（別の種類の余分なイベントも落ちる）。成功のイベントに error が付いてきたら、行に ` error=<値>` を足す（実装のレビュー v1 の Codex の 2 件）
- 実機の承認の区分は `appdialog`（ハーネス自身のアラートとシートを押し、テキスト欄に打つ）

## 3. 分かったこと

- 1.x のキャンセル（テキスト入力、ログイン）は値を null で返し、空のまま確定すると空文字を返す。結果の行で区別できている
- 1.x のアラートは、同じフレームで 2 回呼んでも 2 つとも出る（ID-14a）。レビュー v2 の 2 人の読みは食い違っていて（サブエージェントは「0 回」、Codex は「2 つ目は失敗が 1 回」）、どちらも外れた。一次ソースを読むより、流して決めるべき項目だった
- 前面で非活性のときの呼び出しは、1.x では失敗を 1 回返す（ID-17）。Dialog の写し方の設計で、`2.0.0` の振る舞い（出す）を既知の差分として書く

## 4. 残っていること

- 実機での記録（ID 1〜11、14a、14b。許可を得てから。`scripts/test_ios_ui.sh --device --dry-run` の一覧を見せる）
- ID-20〜22（画面を出さない Player のテスト）を `IosDialogPlayerTests` に足す（段 2 の (A) の仕組みで流す）

## 5. 実機の記録（2026-11-14 20:3x、iPhone XS / iOS 18.7.2）

利用者の許可を得て、HEAD `f58c695` で流した（[README](../../../topics/ios-c-abi-2/README.md) の 6.6）。

| ID | 結果 |
|---|---|
| ID-1〜3、5〜11、14a、14b | 成功（シミュレーターと同じ結果の行。ID-14a も 2 つとも出た） |
| ID-4 | **失敗**。シミュレーターでは成功。ハーネスを入れ直すたびに出たローカルネットワークの許可のダイアログ（development ビルドのため）が、初めて出た頃に当たった見込み。実機の失敗の文言は決まりどおり消したので確定していない。ハーネスを development でない形に直してから、実機で流し直す |
