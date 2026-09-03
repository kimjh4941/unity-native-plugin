# 実装結果レポート

## 基本情報

- 日付: 2026-09-03
- 機能名: clipboard
- 対象プラットフォーム: macOS
- ブランチ: `feature/UNT-10`
- 実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v11.md`
- **対象範囲: 段 3（3a = 残り 8 操作 / 3b = 監視一式）**
- 前版: `2026-09-03-macos-clipboard-implementation-feature-result-v3.md`（段 2）
- 反映したレビュー: `artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v8.md`（Codex）

## 0. 位置づけ

design-v11 の 3 段構成の**最終段**。計画書 12 章の「必要なら 3a（単発操作）/ 3b（監視）に分割してよい」に従い 2 回に分けた。

**これで公開 API 15 操作すべてが実装された。** 残るのはサンプルシーン（`design-sample-scene`）とマニュアル（`write-manual`）で、いずれも本計画書の範囲外である。

## 1. 実装サマリー

### 1.1 段 3a — 単発操作 8 個

`Snapshot` / `CreatePasteboard` / `RemovePasteboard` / `DetectPatterns` / `DetectValues` / `DetectMetadata` / `GetAccessBehavior` / `CheckForegroundChange`。段 2 で確立した骨格の反復で、新しい機構は無い。

**段 2 で予告した `InvokeNative` の差し替えを実施した。** `RemovePasteboard` が `MacClipboardOperationResult` を返す最初の操作になり、フォールバックが到達可能な分岐になったため、計画書 5.6.4 のシグネチャ（`inFlightKey` + 任意の `onNativeFailureResult`）に戻した。15 操作のうち `onNativeFailureResult` を省略するのは値を返さない 3 操作だけで、その省略が意図的であることをコメントに明示している。

### 1.2 段 3b — 監視一式

| 項目 | 計画書 | 実装 |
| --- | --- | --- |
| active / pending の 2 スロット | 5.6.5 | `s_onChanged` / `s_pendingOnChanged`。配送先は active のみ |
| 状態遷移 8 契機 | 5.6.5 の表 | 全件実装。start 成功で昇格、失敗で pending 破棄・active 維持、stop 成功で active 破棄、stop 失敗で維持 |
| 世代カウンタの撤去 | D-13 | v6 までの 3 変数と `ReleaseChangeRegistrationIfOwned()` は実装していない。single-flight により完了コールバックの持ち主が一意なので不要 |
| 共有 single-flight キー | 5.2.1 / 5.6.4 | `ObservationControlKey`。専用 Busy 文言も実装 |
| D-16 の再発行規則 | 5.6.8 | `HandleObservationControlCallback` の tombstone 分岐。**成功した start のみ**再発行する |
| teardown の `stop` 差し替え | 12 章 / 10 章 DoD | 段 2 の空実装を `StopObservingForTeardown` に置換済み |
| 変更コールバックへの `DiscardIfTerminated` | 5.6.8 | 実装。parse より前に判定する |
| 変更イベントの parse 失敗 | 6.4 | 結果を返さず捨てる。待っている操作が無いため |
| per-call スロット 17 本 | 5.6.12 | 完了。`ClearAllPendingCallbacks` / `HasAnyPendingCallbackForTests` も 17 本すべてを見る |
| 公開イベント 13 本 | 5.6.2 | 完了 |

### 1.3 review-v8 の B 指摘 2 件の実装

| 指摘 | 実装 |
| --- | --- |
| B-1 | `TeardownStopIssueCountForTests`（改名後の名前）を実装。**総発行回数**であることと、「1 だけを確認するテストは D-16 未実装でも通る」ことをシームの XML コメントに明記した。テストは 1 / 2 / 1 の 3 点を確認する |
| B-2 | `PendingRestart_DoesNotDivertEventsFromTheActiveRegistration` を実装。A を active、B を pending にしたままイベントを注入し、A だけが発火することと、B の成功完了後は B だけが発火することを確認する |

### 1.4 実装時の判断

**ログ許可リストに入らない引数を出していない。** 計画書 5.6.11 の許可リスト（`itemCount` / `representationCount` / `totalBytes` / `hasScope` / `scopeKind` / `hasCallback` / `operation` / `errorCode`）に従い、`StartObserving` の `intervalSeconds` は出さず `hasScope` / `hasOnChanged` / `hasCallback` に留めた。段 2 の `ReadData` で `utType` を出さなかったのと同じ扱いである。

**ただしこれには代償がある（要判断）。** `intervalSeconds` は 1523（範囲外）の唯一の原因、`utType` は 1302 の主要な原因であり、どちらもログに出ないと**実機ログだけでは原因を特定できない**。両方とも利用者データではなく呼び出し側の設定値なので、許可リストを広げる余地がある。**計画書側で決めるべき事項として残す。**

## 2. 変更ファイル

### 2.1 変更（Runtime）— 1 ファイル

| ファイル | 段 2 時点 | 現在 |
| --- | ---: | ---: |
| `Runtime/Clipboard/MacClipboardManager.cs` | 1,294 行 | **2,695 行** |

### 2.2 変更（Tests）— 1 ファイル

| ファイル | 段 2 時点 | 現在 | 件数 |
| --- | ---: | ---: | ---: |
| `Tests/PlayMode/MacClipboardManagerIntegrationTests.cs` | 788 行 | **1,540 行** | 36 → **72** |

### 2.3 既存変更

**なし。** 変更したのは段 2 で自分が作成した 2 ファイルのみ。iOS / Android / Share / Notification には一切触れていない。

## 3. テスト結果

| プラットフォーム | 総数 | 成功 | 失敗 | 判定 |
| --- | ---: | ---: | ---: | --- |
| EditMode | 517 | 517 | 0 | **Passed** |
| PlayMode | **116** | 116 | 0 | **Passed** |

段 3 開始前は PlayMode 80。**36 件を追加**（3a で 20 / 3b で 16）。

### 3.1 変異テスト 6 件

監視領域は v5〜v7 で 3 ラウンド連続 A1 が出ていた場所なので、**実装に既知の欠陥を注入して狙ったテストが落ちることを確認した。**

| # | 段 | 注入した欠陥 | 落ちたテスト |
| --- | --- | --- | --- |
| 1 | 3a | `Snapshot` が `Read` の single-flight キーを流用 | `EveryOperation_UsesItsOwnSingleFlightKey` |
| 2 | 3a | `createPasteboard` の完了が snapshot のスロットを奪う | `CreatePasteboard_DeliversTheGeneratedScope` / `EachOperationCompletion_ReleasesOnlyItsOwnSlot` |
| 3 | 3b | **D-16 の再発行を削除** | `LateSuccessfulStart_AfterTeardown_ReissuesTheStop` |
| 4 | 3b | **段階 7 が pending ではなく active に書く** | `PendingRestart_DoesNotDivertEventsFromTheActiveRegistration` |
| 5 | 3b | 失敗した start が active を破棄する | `FailedRestart_KeepsTheRegistrationItAlreadyHad` |
| 6 | 3b | 失敗した stop が active を破棄する | `FailedStop_KeepsTheRegistrationBecauseNativeIsStillObserving` |

**3〜6 は 1 回の実行で 4 件同時に注入し、失敗したのはちょうどこの 4 件だった。** 各欠陥に対応するテストが 1 件ずつ存在し、取りこぼしも巻き添えも無い。

**3 と 4 は review-v8 の B-1 / B-2 がまさに指摘した穴である。** 指摘前のテスト設計だと、この 2 つの欠陥は検出できなかった。

### 3.2 実装中に検出した自分のバグ

**0 件。** 段 3a・3b とも初回のコンパイル・テストで通った。

### 3.3 未実施ケース

| ケース | 理由 |
| --- | --- |
| 段階 7（P/Invoke 例外）の 9002 | Editor では P/Invoke がコンパイルされず、例外を起こす手段が無い |
| `StartObserving` の段階 5（JSON 構築失敗） | `scope` が null なら general になるため、Builder を失敗させる入口が無い。**経路自体は実装済み** |
| ネイティブ実機動作 | 7.5 の手動確認 32 項目。**未実施** |

## 4. Definition of Done（計画書 10 章）

| 条件 | 判定 |
| --- | --- |
| 対象範囲の Runtime ファイルが作成され、コンパイルエラーが無い | 満たす |
| `public` メンバに英語の XML コメント | 満たす |
| ログが shape / count / flag のみ | 満たす（1.4 の判断を含む） |
| 逸脱の宣言が既存の使い分けに従っている | 満たす |
| 対象範囲の新規テストが pass | 満たす（36 件） |
| **（段 3 のみ）5.6.8 の teardown を実装し、段 2 の `stop` 空実装を置換** | **満たす** |
| **（段 3 のみ）D-16 と `TeardownStopIssueCountForTests` の回帰テスト** | **満たす**（3.1 の #3 で有効性も確認） |
| **（段 3 のみ）active / pending 遷移が 7.2 の `(3)` 全項目で検証されている** | **満たす**（pending 中の配送先を含む） |
| **既存テストが全件 pass** | **満たす（EditMode 517 / PlayMode 116、失敗 0）** |
| EditMode テストが Manager インスタンスを生成していない | 満たす |
| PlayMode テストが `[TearDown]` で `ResetForTests()` を呼ぶ | 満たす |
| `.meta` を新規作成していない | 満たす |
| ネイティブ未検証事項を XML コメントに引き写す | 満たす（段 2 で 4 件、段 3 で 6 件） |
| single-flight の公開トレードオフをクラス XML コメントに書く | 満たす |
| 新規ファイルに OS 接頭辞 | 該当なし（新規ファイル無し） |
| 他プラットフォームのファイルを変更していない | 満たす（2.3） |
| **7.5 の手動確認 32 項目** | **未実施** |
| **V-1 〜 V-13 のすべてに結論または継続課題の記載** | **未実施** |
| **`testing.md` 7 節の層 1 カバレッジ表の更新** | **未実施** |

## 5. 次のステップ

コード実装は完了した。**段 3 完了時の追加条件 3 件が未実施**である。

1. **7.5 の手動確認 32 項目**を macOS Standalone Player で実施する。V-3（サイズ上限の実測）と V-4（コールバックのスレッド実測）はここでしか埋まらない
2. **V-1 〜 V-13 の棚卸し**。V-1 は段 0 でクローズ済み
3. `agent-rules/coding-rules/testing.md` 7 節の層 1 カバレッジ表で、Clipboard の macOS 列を「対象外」→「実装済み」に更新する

そのあとに `design-sample-scene` と `write-manual`、最後に `release`。

**1.4 のログ許可リストの件は、マニュアルを書く前に決めておくのが望ましい。** 実機ログで 1302 / 1523 の原因を追えるかどうかが変わる。
