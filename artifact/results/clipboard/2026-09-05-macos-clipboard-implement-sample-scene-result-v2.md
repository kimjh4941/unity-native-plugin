# サンプルシーン実装結果レポート (v2)

## 基本情報

- 日付: 2026-09-05
- 機能名: clipboard
- 対象プラットフォーム: macOS
- ブランチ: `feature/UNT-10`
- サンプル計画: `artifact/designs/clipboard/2026-09-05-macos-clipboard-sample-scene-design-v3.md`
- 参照する実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v13.md`（以降 `v13`）
- 前版: `2026-09-05-macos-clipboard-implement-sample-scene-result-v1.md`
- 反映したレビュー: `artifact/reviews/clipboard/2026-09-05-macos-clipboard-implement-sample-scene-review-v1.md`（**要修正（軽微）**。A 区分 0 / medium 5 / B 1 / low 7）

## 0. v1 からの変更

**実装レビュー（Claude サブエージェント）の反映。A 区分は 0 件だったが、medium と B の 6 件を修正した。**

| 指摘 | 分類 | 対応 |
| --- | --- | --- |
| **B-1** wiring テストが Controller を検証していない | B | **構造から直した**（1.1） |
| **M-1** 鮮度アンカーが scope で修飾されていない | medium | `_lastWrittenScope` を追加（1.2） |
| **M-2** `StartIntervalProbe` に teardown ガードが無い | medium | `_isTornDown` を追加 |
| **M-3** `NonOwningToken` を検査せず発行している | medium | pending 中は局所報告して中断 |
| **M-4** ハッシュ時間が `elapsedMs` に混入 | medium | `Succeed` を `RememberWrite` より前へ |
| **M-5** S-5 が UI から実行不能 | medium | 記述で決着（3.2） |
| **L-7** deferred stop が再試行されない | low | 設計どおりの割り切りとしてテストで固定 |
| L-1 / L-4 | low | 記述で決着（3.1 / 3.2） |
| L-2 / L-3 / L-5 / L-6 | low | 記録のみ。理由は 3.3 |

### 1.1 B-1 の修正（最初の修正は不十分だった）

**指摘:** wiring テストの照合相手が**テスト内のハードコード配列**なので、UXML を壊すと落ちるが、**Controller の bind 引数を壊すと 7 件すべて pass する。** 実機では該当ボタンが無反応になり、Console にエラーが 1 行出るだけになる。

**1 回目の修正（不十分）:** Controller に `internal static readonly string[] BoundButtonNames` を置き、テストがそれを参照する形にした。

**実測して失敗と判明した。** bind 呼び出しの名前を `DetectValuesButton` → `DetectValuesButtonX` に変える変異を当てたところ、**562 件すべて pass した。** 配列と bind 呼び出しという**リストが 2 本ある状態は変わっていなかった**ためである。

**2 回目の修正（有効）:** リストを 1 本にした。

| | 修正前 | 修正後 |
| --- | ---: | --- |
| `Button?` フィールド | 43 | **0** |
| bind 呼び出し | 43 行 | ループ 1 つ |
| `OnDestroy` の `-=` | 43 行 | ループ 1 つ |
| **名前が書かれる場所** | **2 箇所** | **1 箇所**（`Bindings` 表） |

`Bindings` は `(string Name, Action Handler)[]` で、バインド・アンバインド・テストがすべてここを読む。

**テストは Controller のインスタンスから `Bindings` を直接読む。** ただし `testing.md` 1 節が EditMode での Manager 生成を禁じているため、**非アクティブな GameObject に `AddComponent` する**。Unity は非アクティブなオブジェクトの `Awake` / `OnEnable` を呼ばないので、購読も `MacClipboardManager` の生成も起きない。

**同じ変異を当て直して落ちることを確認した。**

```
FAIL: ClipboardUxml_ContainsEveryButtonTheControllerBinds
      Button not found in UXML: DetectValuesButtonX
```

副次的に、購読と解除の非対称という事故の余地も消えた（同じ辞書から解除するため）。

### 1.2 M-1 の修正

**指摘:** `_lastWrittenChangeCount` が scope で修飾されておらず、`_activeScope` を変える 4 箇所がアンカーを無効化しない。**changeCount は pasteboard ごとの値なので、別 pasteboard の値と偶然一致すると `fresh=true` になり、他アプリの内容を自分の書き込みとして判定する。**

**これは Codex が設計レビューで出した R-3（鮮度判定の限定不足）と同じ種類の 2 回目である。**

**修正:** `_lastWrittenScope` を追加し、**pasteboard と changeCount の両方**が一致したときだけ `fresh` とする。scope の比較は値で行う（Copy 結果の scope はネイティブ応答から再構築されるため、呼び出し時のインスタンスとは別物）。

## 2. テスト結果

| プラットフォーム | 総数 | 成功 | 失敗 | 判定 |
| --- | ---: | ---: | ---: | --- |
| EditMode | **562** | 562 | 0 | **Passed** |
| PlayMode | 116 | 116 | 0 | **Passed** |

v1 時点は 560。**2 件を追加**（`DeferredStop_IsNotReissuedAfterAFailedStop` / `Controller_BindsExactlyThePlannedNumberOfButtons`）。

### 2.1 変異テスト 3 件

| # | 注入した欠陥 | 結果 |
| --- | --- | --- |
| 1 | UXML のボタン name を 1 つ書き換え | **落ちる**（欠けたボタン名まで表示） |
| 2 | Controller の bind 名を書き換え（**修正前**） | **落ちない。** B-1 の実証 |
| 3 | Controller の bind 名を書き換え（**修正後**） | **落ちる** |

**ワークフローの止める基準 条件 2（wiring テストが name 破壊で落ちること）は #1 と #3 で満たした。**

## 3. 記述で決着させた指摘

### 3.1 L-1: `DetectPatterns` の `kinds=` 表示

`kinds={string.Join(",", result.Patterns)}` は `MacClipboardDetectionPattern` の**列挙名**を並べる。計画 `§6.5` の許可リストに挙がっている enum は `MacClipboardAccessBehavior` だけで、これは含まれていない。

**意図的な追加として維持する。** `v13 §7.5` #11 は「一致パターンが返る」の確認で、件数だけでは**どのパターンが一致したか判定できない**。列挙名は閉じた集合で利用者データを含まない点も `MacClipboardAccessBehavior` と同じである。

**計画 `§6.5` の許可リストに `MacClipboardDetectionPattern` が漏れていた。** 本レポートを正本とし、次に計画書を改訂する際に反映する。

### 3.2 M-5 / L-4: 計画書の手順記述と実装の食い違い 2 件

**M-5（`§7.3` S-5「Editor で実行」）:** `OnClipboardClicked` は `#if UNITY_EDITOR` が最初の分岐なので、**Editor では画面に入らずダイアログが出る。** これは全機能で共通の既存慣習であり、macOS Clipboard だけの問題ではない。

したがって **S-5 は UI からは実行できない。** ただし「Editor で全操作が 9002 になる」ことは PlayMode の `EveryOperation_InEditor_FailsWithBridgeUnavailable` が 15 操作すべてで検証済みで、**確認の実体は失われていない。** S-5 は「PlayMode テストで代替済み」として扱う。

**L-4（`v13 §7.5` #4 の文言）:** #4 は「**他アプリがコピーした**テキストを Read して派生型を確認」だが、実装の `derived` は自分の `Copy` 直後でないと `n/a` になる。他アプリのコピーでは `writtenTypes` が分からず**判定できない**ためで、計画 `§6.6` / D-9 の「呼べる」を「判定できる」に置き換えた結果である。

**実施手順は `CopyHtml` → `Read` とする。** 派生型の確認という目的は満たせる。`v13 §7.5` #4 の文言は次の改訂で合わせる。

### 3.3 記録のみとした low 4 件

| # | 内容 | 直さない理由 |
| --- | --- | --- |
| L-2 | `_registrationCounts` のエントリが解放されない | 1 セッションで数十件が上限。実害が無い |
| L-3 | 式形式ハンドラに `Debug.Log` が無い | `csharp.md` の必須対象外。`Begin()` が全呼び出しを記録するので追跡できる |
| L-5 | 結果表示が 64px 固定で 4 行しか見えない | 計画 `§9.2` SV-4（実機での収まり）として既に記録済み。実機で判断する |
| L-6 | `BeginStart` と `BeginStop` の本体が同一 | 呼び出し側の意図を名前で区別するための分割。統合すると意図が読めなくなる |

## 4. 変更ファイル（v1 からの差分）

| ファイル | 変更 |
| --- | --- |
| `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs` | 1,149 → **1,143 行**。43 フィールドと 86 行の bind / unbind を `Bindings` 表 + ループに置換。`_lastWrittenScope` / `_isTornDown` / `SameScope` を追加 |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleObservationState.cs` | 変更なし |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleResult.cs` | 変更なし |
| `Tests/Runtime/MacClipboardSampleSceneWiringTests.cs` | 165 → **159 行**。`ReadBoundButtonNames()` で Controller から読む形に |
| `Tests/Runtime/MacClipboardSampleStateTests.cs` | 391 → **411 行**。L-7 の回帰テストを追加 |

## 5. Definition of Done

v1 の判定に加えて次を満たした。

- [x] **wiring テストが UXML の name 破壊で落ちる**（変異 #1 で実測）
- [x] **wiring テストが Controller の bind 名の破壊でも落ちる**（変異 #3 で実測。B-1 の修正）
- [x] 直さない残件が理由つきで明記されている（3.3）

## 6. 次のステップ

**ワークフローの止める基準は 3 条件のうち 2 つを満たした。**

| 条件 | 状態 |
| --- | --- |
| 1. **レビュアーを替えて 1 回通していること** | **未達。** Claude サブエージェントで 1 巡したのみ |
| 2. wiring テストが name 破壊で落ちること | **達成**（2.1） |
| 3. 直さない残件が理由つきで明記されていること | **達成**（3.3） |

**条件 1 のため、Codex で 1 巡する必要がある。** そのあと macOS Standalone Player をビルドして `v13 §7.5` の 32 項目と `§7.3` の S-1〜S-7（S-5 を除く）を実施する。

**実機確認はまだ 1 項目も実施していない。** ブリッジが動くことは何も確認できていない。
