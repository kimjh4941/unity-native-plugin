# 実装結果レポート

## 基本情報

- 日付: 2026-09-07
- 機能名: clipboard
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-11
- 実装計画: `artifact/features/clipboard/designs/2026-09-05-windows-clipboard-design-v8.md`
- 前スライスの結果: `artifact/features/clipboard/results/2026-09-07-windows-clipboard-implementation-feature-result-v1.md`（ステップ 1: 純粋層）
- 前スライスのレビュー: `artifact/features/clipboard/reviews/2026-09-07-windows-clipboard-implementation-feature-review-v1.md`
- **範囲: 計画 7.9 のステップ 2（Manager 骨格）とステップ 3（ライフサイクル）。** 同期 API・非同期 API・イベント・遅延レンダリングは未着手

## 0. 状態サマリー

| 項目 | 状態 |
|---|---|
| Manager 骨格（Singleton / ガード / 配送 / static リセット / seam） | 完了 |
| ライフサイクル（アパートメント・COM 所有権・Initialize・shutdown 状態機械・quit） | 完了 |
| EditMode テスト | 完了（`WindowsClipboardManagerDispatchTests` 29 ケース） |
| Unity Test Runner（EditMode 全体） | **675 / 675 passed、failed 0** |
| テストの有効性検証（ミューテーション） | 完了。2 変異を 4 テストが検出 |
| 同期 API（ステップ 4） | **未着手** |
| 非同期 API・レジストリ結線（ステップ 5） | **未着手** |
| イベント（履歴 3 種）・遅延レンダリング（ステップ 6-7） | **未着手** |
| PlayMode テスト / 実機確認 | 未着手（API 実装後） |

## 1. 実装サマリー

### 1.1 計画由来の実装

**7.11 コンパイルガードの二重構造**

| 層 | ガード | 内容 |
|---|---|---|
| 外側 | `#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | クラス、public API、結果生成、イベント、状態機械、配送、static リセット |
| 内側 | `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` | `DllImport`（`ole32` 含む）、`MonoPInvokeCallback` 実体、delegate インスタンス、COM 呼び出し |
| Editor 経路 | `#else` | `PlatformUnavailable`(1000) を返すスタブ |

これにより **Editor ではネイティブ経路がコンパイル時に消える**ため、`Application.platform` の実行時判定に頼らず `DllNotFoundException` の発生源自体が存在しない。

**7.1 骨格**

- Singleton（`Instance` の遅延生成 + `Awake` の二重生成防止 + `DontDestroyOnLoad`）
- `Awake` で `s_mainThreadId` と `s_dispatcher` を捕捉し、tombstone が立っていれば `Debug.LogError`
- `OnDestroy` の 1 行目が `if (_instance != this) return;`（重複インスタンスの破棄が本体を壊さない）、tombstone をネイティブ呼び出しより前に設定
- `InvokeInOrder`（共通イベント → per-call callback、例外を相互に分離）と `Dispatch`（dispatcher 経由）
- イベント: `ClipboardOperationCompleted` / `FlagChecked` / `ClipboardChanged`

**7.3 初期化**

- 手順 0（状態確認）を最優先で実行。`Running` なら COM / ネイティブに触れず冪等成功、`Draining` / `ShutdownFailed` は `ShuttingDown`(1011)
- `EnsureStaApartment` が `CoGetApartmentType` を確認し、STA でなければ `CoInitializeEx` を 1 回試行。`s_comOwnership` は `None` のときだけ書き換える
- `initClipboardManager` の失敗時と P/Invoke 例外時は**その場で COM 参照を解放**
- `wantsToQuit` の購読は `s_quitHandlerSubscribed` で 1 回だけ

**7.4 終了**

- `TryShutdownCore`: tombstone / `Draining` ガードを迂回する内部経路（メインスレッド判定のみ）
- `FinishShutdownAttempt(origin, result, completed)`: 全 shutdown 起点が通る単一終端関数
  1. 初回試行で `Running` → `Draining` へ遷移し、レジストリをドレイン
  2. `ClassifyShutdown` で完了 / 未完了 / 終端失敗に分類
  3. **`completed` のときだけ** COM 参照を解放
  4. 状態を確定（`ShutDown` / `Draining` / `ShutdownFailed`）
  5. quit 起点なら**成功・タイムアウト・終端失敗のすべてで**終了を再開
  6. `Drain` 起点のみ結果を配送（`TryShutdown` は event 非発火）
- `ShutdownWithDrain`: コルーチンで最大 60 フレーム / 2 秒までリトライ
- `Initialize` を許可する状態は `Uninitialized` / `ShutDown` のみ

### 1.2 実装時の追加判断

| # | 判断 | 理由 |
|---|---|---|
| 1 | `WindowsClipboardManagerState` / `WindowsClipboardShutdownProgress` / `WindowsClipboardComOwnership` を `internal enum` として Manager と同居させた | 計画は状態を表で定義するが型名を規定していない。いずれも Manager と不可分で、EditMode テストが状態を検証するために必要（前スライスの L7 と同じ判断） |
| 2 | `ClassifyShutdown` を `internal static` の純粋関数として切り出した | Manager インスタンスを生成せずに全分岐を EditMode で検証するため（testing.md 層 1 の規約） |
| 3 | `RaiseClipboardChanged()` を外側ガードへ切り出し、native callback と Editor seam が共有する | Editor では発火元が内側ガード内にしか無く `CS0067`（イベント未使用）が出る。seam を通じて同じ経路を検証できるようにもなる |
| 4 | `InjectClipboardChangedForTests` / `SetStateForTests` / `SetComOwnershipForTests` / `InjectShutdownResultForTests` / `QuitActionForTests` / `ComReleaseCountForTests` を `#if UNITY_EDITOR` で追加 | 計画 9.2 の seam 方針に沿う。Editor ではネイティブへ到達しないため、これが無いと状態機械と COM 所有権を一切検証できない |
| 5 | `DrainRequestRegistry()` は現時点で空実装 | レジストリの結線はステップ 5。全 shutdown 起点がすでにこの 1 点を通る構造にしてあるため、結線時に呼び出し側の変更は不要 |
| 6 | `IsMainThread()` は `s_mainThreadId == 0` を「未捕捉」として true 扱いにする | `Awake` 前（EditMode テストの静的呼び出し）でも判定が破綻しないようにするため |
| 7 | `OnWantsToQuit` はインスタンスが無い場合に quit を通す | コルーチンを回せる主体が無く、false を返し続けると終了できなくなるため |

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardManagerDispatchTests.cs`

### 2.2 既存変更

なし。`.meta` は Unity が自動生成した。

### 2.3 非変更（対象だが未変更）

- ステップ 1 で作成した Runtime 14 ファイル: 本スライスでは変更していない
- `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs`: API 実装後
- 他プラットフォーム / `Runtime/Common/`: P2 / P4 のとおり不変

## 3. エラー契約反映

### 3.1 エラーケース実装反映

| コード | 実装箇所 |
|---|---|
| `MainThreadRequired`(1002) | `Initialize` / `TryShutdown` / `ShutdownWithDrain` / `CanShutdownNow` / `TryShutdownCore` |
| `ManagerDestroyed`(1003) | `Initialize` / `CanShutdownNow`（tombstone） |
| `ShuttingDown`(1011) | `Initialize`（`Draining` / `ShutdownFailed` から） |
| `PlatformUnavailable`(1000) | Editor 経路と非 Windows Player |
| `BridgeUnavailable`(1001) | `DllNotFoundException` / `EntryPointNotFoundException` の捕捉 |
| `ApartmentUnavailable`(1009) | `CoInitializeEx` 失敗 |
| `ShutdownTimeout`(1006) | ドレインの budget 超過 |
| native 0〜19 | `WindowsClipboardResult.FromNative` 経由 |

未使用のコード（`NotInitializedByHost` / `InvalidArgument` / `ResultParseFailed` / `RequestRejected` / `OperationBusy`）は、対応する API がまだ無いため次スライス以降で使う。

### 3.2 コールバック返却仕様反映

- 同期 API は戻り値で即時に結果を返し、イベントと per-call callback は dispatcher 経由で配送する
- `TryShutdown` のみ意図的に event / callback を発火しない（ドレインが毎フレーム呼ぶため。設計 5.3 の例外規定）

### 3.3 success 時契約

- 結果型のファクトリで担保済み（ステップ 1 で検証）。本スライスは同じファクトリのみを使用する

## 4. ビルド結果

- 実行コマンド: `Unity.exe -batchmode -runTests -projectPath <repo> -testPlatform EditMode -testResults <xml>`
- 結果: **SUCCESS**
- コンパイルエラー 0、**本実装由来の警告 0**（`CS0067` は 1.2 の判断 3 で解消）

## 5. テスト結果

- 結果サマリー: 実行件数 **675** / 成功 **675** / 失敗 **0**
- 実行履歴:

| 実行 | 結果 | 備考 |
|---|---|---|
| ステップ 2 実装直後 | 674 / 674 passed | 新規 25 ケース。`CS0067` 警告 2 件 |
| （破棄した実行） | — | ミューテーション実行中にファイルを編集したため結果を破棄 |
| ミューテーション（直列） | 671 / 675 passed（4 failed） | 意図どおり 2 変異を検出 |
| 復元後 | **675 / 675 passed** | 新規 29 ケース、警告 0 |

### 5.1 ミューテーションテスト

| 変異 | 破壊した契約 | 落ちたテスト |
|---|---|---|
| M4: `WrongThread` を未完了扱いにする | 終端失敗の判定（7.4）。リトライしても完了しない経路を打ち切れなくなる | `ClassifyShutdown_WrongThreadIsTerminal` / `ShutdownAttempt_FromRunning_TerminalFailureReachesShutdownFailed` / `TerminalShutdownFailure_KeepsTheComReference` |
| M5: 未完了でも COM を解放する | 所有権契約（7.3）。ネイティブが COM とウィンドウを保持している間にアパートメントを畳む | `IncompleteShutdown_KeepsTheComReference` |

### 5.2 テスト詳細

| テスト観点 | ケース数 | 結果 |
|---|---|---|
| `InvokeInOrder`（順序、同一結果、例外の相互分離、null 安全） | 5 | ○ |
| `ClassifyShutdown`（完了 / 未完了 5 種 / 終端失敗 5 種） | 11 | ○ |
| 状態機械（`Running` からの完了・未完了・終端失敗、`ShutdownFailed` からの復帰） | 4 | ○ |
| COM 所有権（完了時に解放、未完了時に保持、終端失敗時に保持、二重解放なし） | 4 | ○ |
| `ClipboardChanged` seam（dispatcher 不在時の安全性） | 1 | ○ |
| `ResetForTests`（状態・所有権・tombstone・購読のクリア） | 1 | ○ |
| 合計 | **29** | ○ |

### 5.3 未実施

| 観点 | 理由 |
|---|---|
| `Initialize` / `TryShutdownCore` のネイティブ経路 | Editor ではコンパイルされない。PlayMode（Player 上）または実機確認の対象 |
| quit ドレインの実挙動（`QuitActionForTests` の呼び出し回数） | コルーチンが必要なため PlayMode の対象。seam は実装済み |
| `Awake` / `OnDestroy` を通す検証 | Manager インスタンス生成が必要なため PlayMode の対象 |

## 6. Definition of Done（設計 11 章に対する現時点の判定）

- ○ ガードが二重構造になっている（7.11）
- ○ テストファイルにも同じクラスガードが付いている
- ○ `Running` 中の `Initialize` が COM / ネイティブに触れずに冪等成功を返す（7.3）
- ○ shutdown 経路が `TryShutdownCore` を通り、tombstone / `Draining` に阻まれない（7.4）
- ○ COM 所有権の記録と解放が 7.3 の表どおり（初期化失敗・例外時の即時解放を含む）
- ○ `wantsToQuit` の多重購読防止と全終端結果での quit 再開
- ○ 全 public 型・メソッドに英語の XML ドキュメントコメント
- ○ `[MonoPInvokeCallback]` は static、delegate は `static readonly` で保持
- ○ 追加したテストが「壊すと落ちる」ことをミューテーションで実証
- \- 同期 API / 非同期 API / 遅延レンダリング / PlayMode / 実機確認（次スライス以降）

## 7. 実行確認

- 提示文: 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用する（次はステップ 4 の同期 API、または本スライスのレビュー）
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの差分は保持したまま終了
- ユーザー回答: 未回答

## 8. 運用上の記録

ステップ 1 のレビューと本スライスで、**Unity の実行中に作業ツリーを編集して結果を無効にする**失敗を 2 回繰り返した（1 回目は独立レビューと並行、2 回目はミューテーション実行と並行）。以降は Unity 実行中はツリーに触れず、検査は直列に実行する。
