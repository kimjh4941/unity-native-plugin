# サンプルシーン実装結果レポート (v3)

## 基本情報

- 日付: 2026-09-05
- 機能名: clipboard / 対象プラットフォーム: macOS / ブランチ: `feature/UNT-10`
- サンプル計画: `artifact/designs/clipboard/2026-09-05-macos-clipboard-sample-scene-design-v3.md`
- 参照する実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v13.md`（以降 `v13`）
- 前版: `2026-09-05-macos-clipboard-implement-sample-scene-result-v2.md`
- 反映したレビュー: `artifact/reviews/clipboard/2026-09-05-macos-clipboard-implement-sample-scene-review-v2.md`（**Codex。2 巡目。要修正（重大）**。A 区分 2 / B 区分 1 / medium 2）

## 0. v2 からの変更

**レビュアーを替えた 2 巡目（Codex）で A 区分が 2 件出たため、それを潰した。**

| 指摘 | 分類 | 対応 |
| --- | --- | --- |
| **H-1** `Read` 完了時に可変フィールドを読み、scope 切替で誤判定 | A | **仕組みごと修正**（1.1） |
| **H-2** 7.5 #16 の「旧 callback が 0 回」が画面に出ない | A | 登録別カウンタをステータス行へ（1.2） |
| **M-1（B 区分）** handler 差し替えの変異を検出できない | B | テストが name と handler の対応を検査（1.3） |
| M-2 / M-3 | medium | 記述で決着（3.1） |

## 1. 修正内容

### 1.1 H-1: サーキットブレーカーによる仕組みの修正

**H-1 は「鮮度判定の限定不足」の 3 回目である。**

| 巡 | レビュアー | 指摘 |
| --- | --- | --- |
| 設計レビュー | Codex | R-3: `changeCount` によるゲートが無い |
| 実装レビュー 1 巡目 | Claude サブエージェント | M-1: アンカーが scope で修飾されていない |
| 実装レビュー 2 巡目 | Codex | **H-1: `Read` 発行時の scope を捕捉していない** |

ワークフローのサーキットブレーカーは「**同じ種類の指摘が 3 回続いたら、個別の修正ではなく、それを生む仕組みを直す**」と定めている。**個別修正を 3 回目も繰り返さない。**

**生んでいた仕組み:** `_activeScope` が可変フィールドで、**非同期コールバックの中から読めてしまう**こと。呼び出し時に渡した値と、完了時に読む値が食い違う。R-3 で `changeCount` を、M-1 で `_lastWrittenScope` を足したが、**読む場所が可変フィールドである限り同じ穴が開き続ける。**

**修正:**

1. **`MacClipboardSampleResultContext` に `Scope` を持たせた。** このコンテキストは呼び出しごとに作られ全 closure が既に捕捉しているので、**コールバックが `_activeScope` を読む理由が無くなった**
2. 全操作が `context.Scope` を Manager へ渡す。別 scope を狙う 3 操作（`ProbeRemovedScope` / `RemoveActivePasteboard` / `ErrRemoveGeneral`）は `Begin(marker, target)` で明示する
3. 判定を純粋関数 `MacClipboardSampleResult.IsFresh(writtenScope, writtenChangeCount, **readScope**, readChangeCount)` に切り出した。**引数名が「読み出しが発行された scope」であることを示す**ので、次に触る人が同じ間違いをしにくい
4. 不要になった `SameScope` を削除

**`_activeScope` の残存参照 12 箇所の内訳:** 宣言 1 / `Begin` の既定値 1 / ステータス行 1 / 代入とその直後の同期表示 8 / コメント 2。**非同期コールバック内の判断に使っている箇所はゼロ。**

### 1.2 H-2: 登録別カウンタの可視化

計画 `§6.6` は #16 の判定値を `obs#1=0 obs#2=1` と定義していたが、実装は**callback が来た登録しか表示していなかった。** 「正しく置換されて 0 回」と「計数自体が壊れている」を区別できない。

**修正:** 登録を**順序付きリスト**で保持し、ステータス行に `Registrations: observe.start#1=0 observe.restart#2=1` の形で全件出す。辞書ではなくリストにしたのは、**発行順が #16 の判定材料そのもの**だからである。

### 1.3 B 区分: handler 対応の固定

v2 の修正で name の照合は閉じたが、**`DetectValuesButton` を `OnDetectMetadataClicked` に結び替えても全テストが通る**状態だった。ボタンは動くが**別の操作を実行する**ため、実機確認では最も紛らわしい壊れ方になる。

**修正:** `EveryButtonIsBoundToItsOwnHandler` が `Bindings` の各組について `handler.Method.Name` が `On<Name>Clicked` と一致することを検査する。

## 2. テスト結果

| プラットフォーム | 総数 | 成功 | 失敗 | 判定 |
| --- | ---: | ---: | ---: | --- |
| EditMode | **571** | 571 | 0 | **Passed** |
| PlayMode | 116 | 116 | 0 | **Passed** |

v2 時点は 562。**9 件を追加**（`IsFresh` 5 / 登録カウンタ 3 / handler 対応 1）。

### 2.1 変異テスト（累計 5 件）

| # | 注入した欠陥 | 結果 |
| --- | --- | --- |
| 1 | UXML のボタン name を書き換え | 落ちる |
| 2 | Controller の bind 名を書き換え（**v2 修正前**） | **落ちない**（B-1 の実証） |
| 3 | Controller の bind 名を書き換え（v2 修正後） | 落ちる |
| 4 | **handler を別の既存 handler に差し替え** | **落ちる**（`EveryButtonIsBoundToItsOwnHandler`） |
| 5 | **鮮度判定から pasteboard 比較を削除** | **落ちる**（`IsFresh_*` 2 件） |

## 3. 記述で決着させた指摘

### 3.1 M-2 / M-3: 画面ではなく Player.log を判定先とする 3 項目

| 7.5 | 指摘 | 決着 |
| --- | --- | --- |
| #1 / #2 | `OwnershipChanged` の発火と `Operation` が画面に出ない | **Player.log を判定先とする。** 共通イベントは全 13 本が `LogEvent` で `operation` と `errorCode` を出す。画面は per-call callback の結果を示すもので、両者は別の契約である |
| #15 | イベント受信時刻・前面復帰マーカーが無く、時系列を判定できない | **Player.log の時刻を併用する。** 画面は累積件数と changeCount のみ |

**これらはコードの欠陥ではなく、判定先の記述漏れである。** `v13 §7.5` は #27 で既に Player.log の確認を求めており、ログを正式な判定先として使うこと自体は想定内である。

**手順への追記:** #1 / #2 / #15 は **画面 + Player.log の併用**で判定する。

### 3.2 32 項目の判定可能性（Codex の集計）

**○ 19 / △ 11 / × 2** だった。× の 2 件は #15（M-3）と #16（H-2）。

**H-2 の修正により #16 は ○ になった。** #15 は 3.1 のとおり Player.log 併用の △ とする。

**更新後: ○ 20 / △ 12 / × 0。**

## 4. 変更ファイル（v2 からの差分）

| ファイル | 変更 |
| --- | --- |
| `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs` | `Begin(marker, target)` を追加し全操作を `context.Scope` 経由に。`SameScope` を削除。登録カウンタを順序付きリスト化し `IncrementRegistration` を追加 |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleResult.cs` | `MacClipboardSampleResultContext.Scope` を追加。`IsFresh` / `FormatRegistrationCounts` を追加。`FormatStatus` に登録カウンタを追加 |
| `Tests/Runtime/MacClipboardSampleStateTests.cs` | `IsFresh` 5 件、登録カウンタ 3 件を追加 |
| `Tests/Runtime/MacClipboardSampleSceneWiringTests.cs` | `EveryButtonIsBoundToItsOwnHandler` を追加 |

`MacClipboardSampleObservationState.cs` / UXML / USS / Navigator / TopMenu は変更なし。

## 5. 止める基準（ワークフロー）

| 条件 | 状態 |
| --- | --- |
| **A 区分 0 件** | **達成**（H-1 / H-2 を修正） |
| 1. レビュアーを替えて 1 回通していること | **達成**（1 巡目 Claude サブエージェント、2 巡目 Codex） |
| 2. wiring テストが name 破壊で落ちること | **達成**（変異 #1 / #3、加えて #4 で handler も） |
| 3. 直さない残件が理由つきで明記されていること | **達成**（3.1 と v2 の 3.3） |

**サーキットブレーカー:** 同種の指摘 3 回で作動し、1.1 のとおり仕組みを直した。ラウンド数は 2 で上限 5 に達していない。

## 6. 次のステップ

**レビューは完了。次は実機である。**

1. **macOS Standalone Player をビルドする**
2. `v13 §7.5` の 32 項目と `§7.3` の S-1〜S-7（S-5 は PlayMode で代替済み）を実施する
3. V-2 / V-3 / V-4 / V-5 / V-6 / V-7 / V-8 / V-9 / V-10 / V-13 を実測値で更新する
4. `write-manual` → `release`

**実機確認はまだ 1 項目も実施していない。ブリッジが動くことは何も確認できていない。**
