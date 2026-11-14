# iOS Share の 1.x の記録（UI テスト計画 v3 の 5 章）v1

- 記録日: 2026-11-14
- 計画: [../designs/2026-11-14-ios-share-ui-test-plan-v3.md](../designs/2026-11-14-ios-share-ui-test-plan-v3.md)（5 章「1.x の記録」をこのファイルに書く。計画の版は上書きしない）
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3-R
- 対象: 1.x（`Plugins/iOS/VERSION.txt` の dist 1.13.0、`ios-native-toolkit-1.3.0` と `unity-ios-native-toolkit-1.3.0`）
- 実行: `scripts/test_ios_ui.sh`（`iOS Profile`、Xcode 26.3、iPhone 16 / iOS 18.3 のシミュレーター、Simulator.app は開かない）。テストは `Tools~/IosUiTests/NtkUiTests/ShareUITests.swift`、ハーネスは `Assets/NativeToolkitIosUiHarness/Runtime/Scenarios/ShareScenario.cs`
- 実機: まだ流していない（利用者の許可を得てから。`scripts/test_ios_ui.sh --device --dry-run` の一覧を見せる）

## 1. シミュレーターの記録（2026-11-14）

各テストは、ハーネスを入れ直して 1 件ずつ流し、結果の行を全文一致で比べ、押した後 3 秒待ってからその id の結果の行の数を数えた。1 回目は 18 件のうち 16 件が成功し、SH-8 と SH-13 が落ちた（2 章）。2 件を直して 2 回ずつ流し直し、4 回とも成功した。

| ID | 結果 | 1.x の結果の行（実測） | 計画の予想との関係 |
|---|---|---|---|
| SH-1〜5 | 成功 | `[share@ok] completed=False activity=<null>` | 予想どおり（文字列、URL、文字列と URL、画像のファイル、ファイル） |
| SH-6 | 成功 | `[share@ok] completed=True activity=com.apple.UIKit.activity.CopyToPasteboard` | 予想どおり |
| SH-7 | 成功 | シートが出てから 2 秒後に「コピー」が 0 件。閉じて `completed=False` | 予想どおり（対照の SH-6 では同じ問い合わせが「コピー」を 1 件見つけた） |
| SH-8 | 成功（直した後） | 見出しの題が `NTK Preview`。閉じて `completed=False` | 予想どおり。1 回目は題の読み方が iOS 18.3 の形に合わず落ちた（2 章） |
| SH-9 | 成功 | `[share@fail] completed=False activity=<null> error=Invalid URL: not a url.`。シートは出ない | 予想どおり |
| SH-10 | 成功 | SH-9 と同じ行（文字列があっても全体が失敗） | 予想どおり |
| SH-11 | 成功 | `[share@fail] completed=False activity=<null> error=Failed to load image at path: <data>/missing.png.` | 予想どおり（文言に道を含む。`2.0.0` では含まない見込みで、既知の差分の候補） |
| SH-12 | 成功 | `[share@fail] completed=False activity=<null> error=File not found at path: <data>/missing.txt.` | 予想どおり（同上） |
| SH-13 | 成功（直した後） | **シートは 1 つだけ出て、閉じた結果は `share2` の受け手とイベントに 1 本ずつ**（`[share2@ok] completed=False activity=<null>`、`[share-event@ok] completed=False activity=<null>`）。`share1` は 0 本。**もう 1 つの呼び出しには、失敗も含めて結果が来ない** | **予想と違った**。計画は「2 つ目は表示に失敗（`Failed to present the share sheet: …`）が 1 本、イベントは 2 本」。その失敗の行は来なかった（3 章） |
| SH-14 | 成功 | ホームに戻して 3 秒後に戻ると、シートが残っていた。閉じて `completed=False` | 予想どおり |
| SH-15 | 成功 | 遅らせた呼び出しは `foregroundInactive` で動き（`[late@event] state=foregroundInactive`）、`[share@fail] completed=False activity=<null> error=No root view controller available to present the share sheet.` がちょうど 1 回。シートは出ない | 予想どおり（Dialog の ID-17 と同じ形）。`2.0.0` では出る見込みで、既知の差分の候補のまま |
| SH-16 | 成功 | 未知の `type` の項目は捨てられ、文字列だけでシートが出た。閉じて `completed=False` | 予想どおり（`2.0.0` は `INVALID_PARAMETER` の見込みで、既知の差分） |
| SH-17 | 成功 | 除く一覧に null を混ぜると一覧がまるごと捨てられ、「コピー」が 1 件出た。閉じて `completed=False` | 予想どおり（既知の差分） |
| SH-18 | 成功 | 背景に入った知らせの中の呼び出しは `background` で動き（`[late@event] state=background`）、前に戻した後に `No root view controller available to present the share sheet.` の失敗が 1 回 | 予想どおり（`2.0.0` は `NOT_FOREGROUND` で、既知の差分） |

### 1.1 Player のテストの記録（2026-11-14）

`scripts/verify_unity_ios.sh --skip-editmode`（同じシミュレーター）で `IosSharePlayerTests` の 6 件がすべて成功した。

| ID | 1.x の実測 | 計画の予想との関係 |
|---|---|---|
| SH-20 | 項目なしは `IsSuccess = false`、`No shareable items were provided.`、受け手はちょうど 1 回 | 予想どおり |
| SH-21 | `payload` が `null` も同じ | 予想どおり |
| SH-22 | イベントが先、受け手が後。どちらも 1 回 | 予想どおり |
| SH-23 | 未知の `type` の項目だけは、ネイティブで捨てられて `No shareable items were provided.` | 予想どおり（`2.0.0` は `INVALID_PARAMETER` の見込みで、既知の差分） |
| SH-24 | 項目の配列が `[null]` は、Builder が除いて同じ失敗 | 予想どおり |
| SH-25 | `value` が null の文字列の項目だけは、パーサーが捨てて同じ失敗 | 予想どおり（既知の差分） |

## 2. 計画 v3 との違い（計画の版は上書きしない）

- **SH-8 の題の読み方**: native-toolkit の iOS 18 の読み方（Close を持つナビゲーションバーの、2 つの `Other` の子の 1 つ目）では読めなかった。iOS 18.3 のシミュレーターでも、プレビューの題があると**見出しのバーの識別子が題そのもの（`NTK Preview`）になり、`Other` の子は 1 つ**だった（native-toolkit が iOS 26.2 で見た形と同じ）。`SystemUI.headerTitles` に、子が 1 つで識別子が `UIActivityContentView` でないときはバーの識別子を題とする形を足した
- **SH-13 の期待**: 実測に合わせて、`share1` は 0 本、`share2` と `share-event` は閉じた結果が 1 本ずつに直した
- SH-13 の調べに、シミュレーターでだけ、失敗の文にハーネス自身の結果の行を出す形を足した（`ShareUITests.harnessLines`。ハーネスの文字だけで、システムの画面の文字は出さない）
- 実機の承認の区分は `sharesheet`。シミュレーターだけは SH-6・7・8・14・15・17・18（`DeviceRequirements.json`）

## 3. 分かったこと

- 1.x の Share を同じフレームで 2 回呼ぶと、**1 つの呼び出しの結果が失われる**。C# の `s_onShare` は後勝ちで、出たシートを閉じた結果は 2 つ目の受け手に届いた。もう 1 つの呼び出しには、ネイティブからの完了がイベントにも来ない（表示の失敗も来ない）。計画とレビュー v1 の読み（`ShareSheetPresenter` が 2 つ目を断る）は外れた。Dialog の ID-14a と同じく、流して決めるべき項目だった
- `2.0.0` は呼び出しごとにちょうど 1 回なので、SH-13 は**既知の差分**（失われていた 1 本が戻る）。Share の写し方の設計で、2 つ目のシートが出るか、2 つ目が失敗するかを決めて、移行ガイドに書く
- 失敗の文言は、無いファイルと画像の道をそのまま含む（SH-11・12）。記録では `<data>` に伏せた

## 4. 残っていること

- 実機での記録（SH-1〜5、9〜13、16。許可を得てから）
- 実装のレビュー（サブエージェントと Codex）
