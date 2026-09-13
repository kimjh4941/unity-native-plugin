# 実装結果レポート（ステップ 1〜7 通し）

## 基本情報

- 日付: 2026-09-07
- 機能名: clipboard
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-11
- 実装計画: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v8.md`
- 本ファイルは**設計 7.9 の全 7 ステップを通した最終結果**であり、スライスごとの v1 / v2 / v3 を束ねる。
  v1〜v3 は経緯の記録として残す（コミット済み）
- 前スライスのレビュー: `artifact/reviews/clipboard/2026-09-07-windows-clipboard-implementation-feature-review-v1.md`（ステップ 1 対象）

## 0. 状態サマリー

| 実装ステップ（設計 7.9） | 状態 |
|---|---|
| 1. 純粋層（結果型 / payload / JSON / レジストリ） | 完了 |
| 2. Manager 骨格 | 完了 |
| 3. ライフサイクル（COM / Initialize / shutdown / quit） | 完了 |
| 4. 同期 API 15 種 | 完了 |
| 5. 非同期履歴 API 5 種 + `Awaitable` + レジストリ結線 | 完了 |
| 6. 履歴イベント 3 種 | 完了 |
| 7. 遅延レンダリング | 完了 |
| 8. 実機確認・`testing.md` 更新 | **未実施** |

| 検証 | 結果 |
|---|---|
| EditMode | **712 / 712 passed** |
| PlayMode | **140 / 140 passed**（Windows 26 件） |
| コンパイルエラー / 本実装由来の警告 | **0 / 0** |
| ミューテーション検証 | 5 回・11 変異、**すべて検出** |

## 1. 実装した公開 API

| 分類 | API |
|---|---|
| ライフサイクル | `Initialize` / `TryShutdown` / `ShutdownWithDrain` / `CanShutdownNow` |
| 書き込み | `CopyPlainText` / `CopyHtml` / `CopyFiles` / `CopyImage` / `CopyCustomFormat` / `CopyMultipleFormats` / `Clear` |
| 読み出し | `PastePlainText` / `PasteHtml` / `PasteFiles` / `PasteImage` / `PasteCustomFormat` |
| 内容確認 | `HasFormat` / `GetFormats` / `GetPreferredFormat` |
| 履歴（非同期） | `GetHistory` / `GetHistoryAvailability` / `RestoreHistoryItem` / `DeleteHistoryItem` / `ClearUnpinnedHistory` / `CancelRequest` |
| 履歴（Awaitable） | 上記 5 種の `*Async`（`CancellationToken` 対応） |
| イベント登録 | `SetHistoryEventsEnabled` |
| 遅延レンダリング | `ReserveDeferredFormats` / `RecoverDeferredState` |

イベント: `ClipboardOperationCompleted` / `TextReadCompleted` / `StringListReadCompleted` / `BytesReadCompleted` /
`FormatPresenceChecked` / `FlagChecked` / `HistoryReadCompleted` / `HistoryAvailabilityChecked` /
`ClipboardChanged` / `HistoryChanged` / `HistoryEnabledChanged` / `RoamingEnabledChanged`

ネイティブ 27 export のうち 26 に対応（`initWinAppSdk` は Notification 専用のため対象外）。

## 2. 変更ファイル

### 2.1 新規作成（Runtime 15）

`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/` 配下。すべて `Windows` 接頭辞、原則 1 ファイル 1 主型。

`WindowsClipboardManager` / `WindowsClipboardErrorCode` / `WindowsClipboardResult` / `WindowsClipboardTextResult` /
`WindowsClipboardStringListResult` / `WindowsClipboardBytesResult` / `WindowsClipboardFormatPresenceResult` /
`WindowsClipboardFlagResult` / `WindowsClipboardHistoryItem` / `WindowsClipboardHistoryResult` /
`WindowsClipboardAvailabilityResult` / `WindowsClipboardPayloads` / `WindowsClipboardJsonBuilder` /
`WindowsClipboardJsonParser` / `WindowsClipboardRequestTable`

### 2.2 新規作成（テスト 7）

- `Tests/Runtime/`: `WindowsClipboardResultTests` / `WindowsClipboardPayloadsTests` / `WindowsClipboardJsonBuilderTests` /
  `WindowsClipboardJsonParserTests` / `WindowsClipboardRequestTableTests` / `WindowsClipboardManagerDispatchTests`
- `Tests/PlayMode/`: `WindowsClipboardManagerIntegrationTests`（本リポジトリで Windows 初）

### 2.3 既存変更

`Packages/.../Plugins/Windows/VERSION.txt`（記録の更新のみ。DLL は `PreBuildProcessor` がビルド時に配置）

### 2.4 非変更

他プラットフォームの `Android*` / `Ios*` / `Mac*`、`Runtime/Common/`、asmdef（P2 / P4 準拠）

## 3. 設計の中核契約と、その実装

| 契約 | 実装 | 検証 |
|---|---|---|
| 7.11 二重ガード | クラスは `\|\| UNITY_EDITOR`、ネイティブ境界は `&& !UNITY_EDITOR`。Editor では DLL 解決が起こり得ない | PlayMode（`PlatformUnavailable` が返る） |
| 7.5 空判定 | エラーコードで分類し、**戻り値 0 を空に正規化しない**。2 回目もバッファを読む前に検査 | EditMode 20 件 + ミューテーション M6/M7 |
| 7.6.1 exactly-once | ticket + `AwaitingNative` / `Undelivered` + `TryClaim`。**完了済み・未配送**も追跡 | PlayMode 13 件 + ミューテーション M8 |
| 7.6.3 teardown ドレイン | dispatcher を迂回して同期配送。`AwaitingNative` は `Canceled`、`Undelivered` は確定値 | PlayMode |
| 7.6.4 in-flight ガード | 更新系 3 操作のみ。既存の結果は必ず届ける | PlayMode + ミューテーション M9 |
| 7.4 単一終端関数 | `FinishShutdownAttempt` に全 shutdown 起点が集約。`completed` のときだけ所有権解放 | EditMode + ミューテーション M4/M5 |
| 7.3 COM 所有権 | 初期化失敗・例外時はその場で解放。`Running` 中の `Initialize` は COM に触れない | EditMode |
| 2.8 D-1〜D-9 | 二相の `requiredSize` 一致、キャッシュ再利用、世代規則は `pError` だけで決定 | EditMode 11 件 + ミューテーション M10/M11 |

## 4. ミューテーション検証の記録

停止基準「追加したテストが、壊すと落ちることを確認済みであること」に対する実証。

| # | 変異 | 検出数 |
|---|---|---|
| M1-M3 | `TryClaim` の削除漏れ / `None` 昇格の削除 / 改行エスケープの削除 | 5 |
| M4-M5 | `WrongThread` を未完了扱い / 未完了でも COM 解放 | 4 |
| M6-M7 | サイズ 0 を無条件に空成功 / 2 回目でエラーでもバッファを読む | 9 |
| M8-M9 | 完了時にエントリ削除 / in-flight ガード無効化 | 5 |
| M10-M11 | 2 回目で再生成 / 失敗時も新世代へ差し替え | 6 |

M6 はレビュー v2 の H1、M8 は H2、M10/M11 は H3 と review-v3 の A1-2 を、それぞれ**実際に再現**したもの。
いずれもコンパイルと成功系テストを通過してしまう種類の欠陥であり、テストが確実に検出することを確認した。

## 5. 実装時の主な追加判断

| # | 判断 | 理由 |
|---|---|---|
| 1 | `Failure(op, None)` を `Unknown` に昇格 | 8.4「失敗は必ずメッセージ」を呼び出し側のミスでも守るため |
| 2 | `WindowsClipboardPayloadKind` と `Bytes()` ファクトリ | 排他を型で表現。ネイティブ側報告の「利用者が自前で base64 を組む」を解消 |
| 3 | `ToUtcTime()` を追加 | 1601 基点の生 int64 は扱いにくい。範囲外は例外にせず null |
| 4 | パーサは payload の形状を検証する | `JsonUtility` は不正入力でも既定値を返すため、検査が無いと「壊れた payload」が「空」に化ける |
| 5 | `default(struct)` でもコレクションが null にならない | `readonly struct` はフィールド初期化子を実行しないため |
| 6 | **Editor の `TryShutdownCore` は「完了」を返す** | 解放すべき資源が無い状況を終端失敗と報告すると、本物の終端失敗が埋もれる |
| 7 | `RaiseClipboardChanged` 等を外側ガードへ切り出し | Editor で `CS0067` を避けつつ、seam から同じ配送経路を検証できる |
| 8 | 名前空間直下の `internal enum` 5 種 | Manager と不可分な補助型。テストから参照する必要がある |
| 9 | `AcceptRequestsWithIdForTests`（ネイティブ呼び出しの差し替え） | 受付**後**に状態を書き換える seam は Editor の実際の流れと矛盾した |

## 6. Definition of Done（設計 11 章）

- ○ 26 のネイティブ API に対応する C# 公開 API（`initWinAppSdk` は対象外）
- ○ `pError` を持つ API はすべて結果型を返す
- ○ 受付済み・受付前拒否のいずれも 1 回だけ配送
- ○ `Awaitable` は in-flight ガードと teardown ドレインの実装後に公開
- ○ `CancellationToken` がワーカースレッドからでも動作し、登録が全経路で破棄される
- ○ shutdown 状態機械 / COM 所有権 / 空判定 / 遅延レンダリング D-1〜D-9
- ○ 全 public 型・メソッドに英語 XML ドキュメント、クリップボード内容をログに出していない
- ○ 二重ガード、テストにも同じクラスガード、P1〜P5 適合
- ○ EditMode / PlayMode 全 passed、既存テストの破壊なし
- ○ 追加テストが「壊すと落ちる」ことをミューテーションで実証
- **×** 実機確認 M-1 〜 M-24（未実施）
- **×** `agent-rules/coding-rules/testing.md` 7 節の適用状況表の更新（未実施）

## 7. 残作業

| # | 内容 | 備考 |
|---|---|---|
| 1 | 実機確認 M-1 〜 M-24 | V-3（予約あり終了）/ V-7（IL2CPP）/ V-8（Alt+Tab）を含む。Player ビルドが必要 |
| 2 | `testing.md` 適用状況表の更新 | Windows Clipboard を層 1・2a に追加 |
| 3 | `review-implementation-feature` による差分レビュー | ステップ 2〜7 が未レビュー |
| 4 | サンプルシーン | `design-sample-scene` の対象（本計画のスコープ外） |

## 8. 運用上の記録

- Unity 実行中に作業ツリーを編集して結果を無効にする失敗を 2 回（ステップ 1 のレビュー、ステップ 2）。以降は検査を直列化して再発なし
- ステップ 4 でコンパイルエラーによる往復が 3 回（using 不足 / enum のスコープ / `[TestCase]` のアクセシビリティ）。
  ステップ 5 以降は「型とシグネチャを先に通してから中身を埋める」順序に変え、往復は 1 回以下に収まった
- 設計 9.1 の「Manager インスタンスを生成するテストは EditMode に書かない」を一度破り、PlayMode へ移した
