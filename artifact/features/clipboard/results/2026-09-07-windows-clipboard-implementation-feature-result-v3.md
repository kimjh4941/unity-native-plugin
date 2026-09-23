# 実装結果レポート

## 基本情報

- 日付: 2026-09-07
- 機能名: clipboard
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-11
- 実装計画: `artifact/features/clipboard/designs/2026-09-05-windows-clipboard-design-v8.md`
- 前スライス: `...-implementation-feature-result-v2.md`（ステップ 2-3: Manager 骨格とライフサイクル）
- **範囲: 計画 7.9 のステップ 4（同期 API）。** 非同期 API・履歴イベント・遅延レンダリングは未着手

## 0. 状態サマリー

| 項目 | 状態 |
|---|---|
| 同期 API 15 種 | 完了 |
| 空判定ロジック（設計 7.5） | 完了。純粋関数として切り出し EditMode で全分岐を検証 |
| EditMode | **698 / 698 passed**（本スライスで +49） |
| PlayMode | **125 / 125 passed**（Windows 10 件は本スライスで新規） |
| ミューテーション検証 | 完了。2 変異を 9 ケースが検出 |
| 非同期 API（ステップ 5） | **未着手** |
| 履歴イベント（ステップ 6）/ 遅延レンダリング（ステップ 7） | **未着手** |
| 実機確認 M-1 〜 M-24 | 未実施（全 API 実装後） |

## 1. 実装サマリー

### 1.1 計画由来の実装

**同期 API 15 種**

| 種別 | API |
|---|---|
| 書き込み 6 | `CopyPlainText` / `CopyHtml` / `CopyFiles` / `CopyImage` / `CopyCustomFormat` / `CopyMultipleFormats` |
| 読み出し 5 | `PastePlainText` / `PasteHtml` / `PasteFiles` / `PasteImage` / `PasteCustomFormat` |
| 内容確認 3 | `HasFormat` / `GetFormats` / `GetPreferredFormat` |
| 消去 1 | `Clear` |

**空判定（設計 7.5、レビュー v2 の H1）**

`ClassifyFirstRead` と `ClassifySecondRead` を `internal static` の純粋関数として切り出した。

- **エラーコードで判定し、戻り値のサイズでは判定しない。** ネイティブの読み出し API は lease 取得失敗でも 0 を返すため、
  サイズを根拠にすると `NotInitialized` / `Busy` / `InvalidData` が「空のクリップボード」に化ける
- `BufferTooSmall` + サイズ 0 は**バイト系 API のみ**空成功（文字列系は終端分が必ず要るため失敗）
- 2 回目の呼び出しも**アンマネージドメモリを読む前に**分類し、`Retry`（内容が増えた）と `EmptySuccess`（間に空になった）を区別する
- `ReadRaw` が両分類を使い、`finally` で必ず `FreeHGlobal`。サイズ変化時は最大 2 回まで再確保

**事前チェック（設計 7.5）**

`CanRunOperation` に集約し、メインスレッド → tombstone → shutdown 状態 → 初期化済みの順で判定する。
shutdown 経路はこれを通さない（7.4 の `TryShutdownCore`）。引数検証は各 API がネイティブ呼び出し前に行い、
`InvalidArgument`(1005) の `detail` に位置（`paths[0]` / `items[1]`）を含める。

**イベント**: `TextReadCompleted` / `StringListReadCompleted` / `BytesReadCompleted` / `FormatPresenceChecked` を追加。

### 1.2 実装時の追加判断

| # | 判断 | 理由 |
|---|---|---|
| 1 | `WindowsClipboardReadDecision` / `WindowsClipboardSecondReadDecision` を名前空間直下の `internal enum` にした | 当初 Manager 内に置いたが、既存 3 enum（`ManagerState` / `ShutdownProgress` / `ComOwnership`）と不整合になりテストから参照できなかった |
| 2 | `*Native` ラッパー（`CopyPlainTextNative` など 14 個）を外側ガードに置いた | 公開 API が両コンパイルで同じ形になる。Editor では `PlatformUnavailable` を返すだけで、`DllImport` 自体は内側ガードに閉じたまま |
| 3 | `ReadOutcome` を private readonly struct として導入 | 3 種の読み出し（text / list / bytes）が同じ 2 回呼び出し規約を共有するため。結果型ごとに規約を書き写すと H1 の再発余地が増える |
| 4 | `PasteCustomFormat` / `HasFormat` は引数検証のためガードを個別に呼ぶ（`skipGuard`） | 形式名の検証をネイティブ呼び出し前に行う必要があり、共通ヘルパの内側では順序が保てないため |
| 5 | **Editor の `TryShutdownCore` は「完了」を返す**（当初は `PlatformUnavailable` の失敗） | 解放すべきネイティブ資源が存在しない状況を終端失敗と報告すると、本物の終端失敗（`WrongThread` 等）が埋もれる。PlayMode テストで発覚した |
| 6 | ガード検証用の `CheckOperationGuardForTests` seam を追加 | 公開 API は MonoBehaviour のインスタンスメソッドで EditMode から呼べない。順序の検証を層 1 に残すため |

## 2. 変更ファイル

### 2.1 既存変更

- `Runtime/Clipboard/WindowsClipboardManager.cs`（同期 API・空判定・ガード・P/Invoke 15 宣言を追加）
- `Tests/Runtime/WindowsClipboardManagerDispatchTests.cs`（空判定とガードのテストを追加）

### 2.2 新規作成

- `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs`（本リポジトリで Windows 初の PlayMode テスト）

### 2.3 非変更

- ステップ 1 の純粋層 14 ファイル、他プラットフォーム、`Runtime/Common/`

## 3. エラー契約反映

| コード | 実装箇所 |
|---|---|
| `NotInitializedByHost`(1004) | `CanRunOperation`（Initialize 未実行） |
| `ShuttingDown`(1011) | `CanRunOperation`（`Draining` / `ShutdownFailed`） |
| `ManagerDestroyed`(1003) | `CanRunOperation`（tombstone は状態より優先） |
| `InvalidArgument`(1005) | 各 API の引数検証。位置を `detail` に含める |
| `ResultParseFailed`(1007) | `ReadStringList` のパース失敗 |
| `BridgeUnavailable`(1001) | `DllNotFoundException` / `EntryPointNotFoundException` |
| `OutOfMemory`(8) | `Marshal.AllocHGlobal` の失敗 |
| `PlatformUnavailable`(1000) | Editor 経路 |
| native 0〜19 | `FromNative` および読み出し分類 |

未使用は `RequestRejected`(1008) / `OperationBusy`(1010)（ステップ 5 で使用）。

## 4. ビルド結果

- 結果: **SUCCESS**（コンパイルエラー 0、本実装由来の警告 0）
- 途中で 3 回コンパイルエラーを出した（`System.Collections.Generic` の using 不足、enum のスコープ、
  `[TestCase]` が internal enum を引数に取れない CS0051）。いずれも記述レベルで、設計契約への影響はない

## 5. テスト結果

| 層 | 結果 |
|---|---|
| EditMode | **698 / 698 passed**（本スライスで +49） |
| PlayMode | **125 / 125 passed**（Windows 10 件） |

### 5.1 ミューテーションテスト

レビュー v2 の H1 で実際に修正した欠陥を再現した。

| 変異 | 内容 | 落ちたケース |
|---|---|---|
| M6 | `ClassifyFirstRead` に「サイズ 0 なら無条件に空成功」を戻す | 6（`0 + Busy` / `0 + NotInitialized` / `0 + InvalidData` / `0 + OutOfMemory` / `0 + WrongThread`、およびバイト系との区別） |
| M7 | `ClassifySecondRead` の既定を `Failure` → `Read` に | 3 |

`total=698 passed=689 failed=9`。復元後は 698 / 698 に戻り、`MUTATION` の残留も無い。

### 5.2 PlayMode で検証した契約（EditMode では原理的に測れないもの）

| 観点 | 内容 |
|---|---|
| 配送タイミング | 同期 API 呼び出し直後は 0 件、次フレームで「共通イベント → per-call callback」の順に届く |
| 二重ガードの実証 | Editor で `PlatformUnavailable` が返る（ネイティブ経路がコンパイル時に消えている） |
| 拒否が空に化けない | 拒否された読み出しの `IsEmpty` が false |
| 引数検証 | 位置付きメッセージ（`paths[0]` / `items[1]`） |
| ライフサイクル | 破棄後の全操作拒否、再生成時のエラーログ、重複インスタンスが本体を tombstone にしない |
| イベント振り分け | 5 種の結果がそれぞれ対応するイベントへ届く |

### 5.3 未実施

- ネイティブ経路の実挙動（Editor ではコンパイルされない）。実機確認 M-1 〜 M-24 の対象
- quit ドレインの実挙動（seam は実装済み、ステップ 5 以降で PlayMode 化を検討）

## 6. Definition of Done（現時点）

- ○ 同期 API 15 種が設計 5.3 の対応表どおり
- ○ 空判定が 7.5 の分類順序に従い、戻り値 0 を空に正規化していない
- ○ 事前チェックの順序と shutdown 経路の除外
- ○ 全 public 型・メソッドに英語 XML ドキュメント、クリップボード内容をログに出していない
- ○ 二重ガード、テストにも同じクラスガード
- ○ 追加テストが「壊すと落ちる」ことをミューテーションで実証
- ○ PlayMode テストを追加（testing.md 層 2a に Windows を追加）
- \- 非同期 API / 履歴イベント / 遅延レンダリング / 実機確認（次スライス以降）
- \- `testing.md` 7 節の適用状況表の更新（全 API 実装後にまとめて行う）

## 7. 実行確認

- 提示文: 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢: 実行する / 修正する / キャンセル
- ユーザー回答: 未回答

## 8. 運用上の記録

- Unity 実行中にツリーを編集して結果を無効にする失敗は、本スライスでは発生していない（検査を直列化した）
- 一方でコンパイルエラーによる往復が 3 回発生した。ステップ 5 では「型とシグネチャを先に通してから中身を埋める」順序に変える
- 設計 9.1 の「Manager インスタンスを生成するテストは EditMode に書かない」を一度破り、PlayMode へ移した
