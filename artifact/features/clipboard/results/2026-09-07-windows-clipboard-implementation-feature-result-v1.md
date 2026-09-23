# 実装結果レポート

## 基本情報

- 日付: 2026-09-07
- 機能名: clipboard
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-11
- 実装計画: `artifact/features/clipboard/designs/2026-09-05-windows-clipboard-design-v8.md`
- レビュー: `artifact/features/clipboard/reviews/2026-09-07-windows-clipboard-implementation-feature-review-v1.md`（high 4 / medium 6 / low 4 を反映済み）
- **範囲: 計画 7.9 のステップ 1（純粋層）のみ。** Manager 本体・P/Invoke・同期 / 非同期 API・遅延レンダリングは未着手

## 0. 状態サマリー

| 項目 | 状態 |
|---|---|
| 純粋層の実装 | 完了（Runtime 14 ファイル） |
| 純粋層の EditMode テスト | 完了（5 ファイル / **76 ケース**、全通過） |
| Unity Test Runner（EditMode 全体） | **649 / 649 passed、failed 0** |
| テストの有効性検証（ミューテーション） | 完了。3 変異すべてを検出（レビュー結果を参照） |
| Manager 本体（7.9 ステップ 2 以降） | **未着手**（次スライス） |
| PlayMode テスト | 未着手（Manager 実装後） |
| 実機確認 M-1 〜 M-24 | 未実施（Manager 実装後） |

## 1. 実装サマリー

### 1.1 計画由来の実装

- 6.1 の Runtime 新規ファイルのうち、Manager を除く **14 ファイル**を作成した
- 8.1 / 8.3 のエラーコード（native 0〜19、C# 1000〜1011）を `WindowsClipboardErrorCode` として実装し、2.3 の英語メッセージをそのまま採用した
- 8.4 の不変条件を結果型のファクトリで担保した（成功はメッセージ null、失敗は必ずメッセージ、空読み出しは成功、`HasFormat` / `Value` は `IsSuccess` と独立）
- 2.4 の JSON スキーマに沿って builder / parser を実装した（パス配列・複数形式配列・履歴配列・可用性オブジェクト）
- 7.6.1 の完了配送レジストリを `WindowsClipboardRequestTable` として実装した（ticket / `AwaitingNative` / `Undelivered` / `TryClaim` / `ClaimAll`）
- 3.2 のログ規約に従い、builder / parser / 結果型には**エントリログを置かない**（機微情報。失敗時のみ形状を記録し内容は出さない）
- 6.1 の命名規約どおり、全ファイルが `Windows` 接頭辞・原則 1 ファイル 1 主型（例外は `WindowsClipboardPayloads.cs`）
- 7.11 のクラスガード `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` を全ファイルへ適用した（内側の P/Invoke ガードは Manager 実装時）

### 1.2 実装時の追加判断

| # | 判断 | 理由 |
|---|---|---|
| 1 | `WindowsClipboardErrorCodeExtensions.ToMessage(code, operation, detail)` という拡張メソッド形にした | 計画は「`ToMessage()` を持つ」とだけ規定。1000 番台のメッセージが `{operation}` / `{detail}` を必要とするため引数付きにした |
| 2 | `Failure(operation, None)` を `Unknown` へ昇格させる | `None` を渡すとメッセージが null になり 8.4 の不変条件（失敗は必ずメッセージ）を破るため。呼び出し側のミスでも不変条件を保つ |
| 3 | `IsNative()` を追加 | 「native が返したコードか、C# 層が作ったコードか」を利用側とテストが判定できるようにするため |
| 4 | `WindowsClipboardPayloadKind` enum と `Bytes()` ファクトリを追加 | 計画は「text / html / base64 の排他」とだけ規定。排他を型で表現し、base64 化を利用側に強いないため（ネイティブ側の報告事項「利用者が自前で base64 を組む必要がある」への対応） |
| 5 | 履歴エントリの `id` が空のものはパース時に落とす（警告ログ） | id が無い項目は restore / delete に使えず、返しても利用側が扱えないため |
| 6 | `WindowsClipboardHistoryItem.ToUtcTime()` を追加 | `Timestamp` は 1601 基点の生 int64 で扱いにくいため。範囲外は例外にせず null を返す |
| 7 | `IssueTicket()` はラップ時に生存中 ticket を避ける | 再利用された ticket は別リクエストへ結果を配送するため。到達はしないが不変条件として実装 |
| 8 | `WindowsClipboardRequestTable.Reset()` を追加 | 7.10 の `ResetCore` から呼ぶ想定。配送せずに破棄する経路を明示的に分離した。**ticket counter は巻き戻さない**（レビュー H4） |
| 9 | パーサは payload の形状（配列か / 必須キーの有無）を検証し、合わない payload を失敗にする | レビュー H2 / H3。`JsonUtility` は不正な payload でも例外を投げずに既定値を返すため、形状検査が無いと「壊れた payload」が「空のクリップボード」「履歴オフ」に化ける |
| 10 | 3 つのコレクション保持型はバッキングフィールド + `?? Empty` で `default` でも null を返さない | レビュー M3。`readonly struct` はフィールド初期化子を実行しないため、自動プロパティのままでは XML の「Never null」に反する |
| 11 | `WindowsClipboardErrorCodeExtensions` と `WindowsClipboardRequestState` は主型と同居させた | 計画 6.1 は「1 ファイル 1 主型の例外は `WindowsClipboardPayloads.cs` のみ」と宣言しているが、この 2 型は主型と不可分な補助型のため分割しない。計画側の文言更新は次版で判断する（レビュー L7） |

## 2. 変更ファイル

### 2.1 新規作成（Runtime 14 / `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/`）

- `WindowsClipboardErrorCode.cs`
- `WindowsClipboardResult.cs`
- `WindowsClipboardTextResult.cs`
- `WindowsClipboardStringListResult.cs`
- `WindowsClipboardBytesResult.cs`
- `WindowsClipboardFormatPresenceResult.cs`
- `WindowsClipboardFlagResult.cs`
- `WindowsClipboardHistoryItem.cs`
- `WindowsClipboardHistoryResult.cs`
- `WindowsClipboardAvailabilityResult.cs`
- `WindowsClipboardPayloads.cs`
- `WindowsClipboardJsonBuilder.cs`
- `WindowsClipboardJsonParser.cs`
- `WindowsClipboardRequestTable.cs`

### 2.2 新規作成（テスト 5 / `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/`）

- `WindowsClipboardResultTests.cs`
- `WindowsClipboardPayloadsTests.cs`
- `WindowsClipboardJsonBuilderTests.cs`
- `WindowsClipboardJsonParserTests.cs`
- `WindowsClipboardRequestTableTests.cs`

### 2.3 既存変更

なし。`.meta` は Unity が自動生成した（手動作成していない）。

### 2.4 非変更（対象だが未変更）

- `Runtime/Clipboard/WindowsClipboardManager.cs`: 次スライス（7.9 ステップ 2 以降）
- `Tests/Runtime/WindowsClipboardManagerDispatchTests.cs` / `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs`: Manager 実装後
- `Plugins/Windows/*`: `PreBuildProcessor` がビルド時に配置するため対象外（設計 2.7）
- `Runtime/Common/*`、他プラットフォームの `Android*` / `Ios*` / `Mac*`: P2 / P4 のとおり触れていない

## 3. エラー契約反映

### 3.1 エラーケース実装反映

| 層 | 反映状況 |
|---|---|
| native 0〜19（設計 8.1） | **20 定数すべて**を enum と英語メッセージに実装。EditMode で 1〜19 全件にメッセージが存在することを検証 |
| C# 1000〜1011（設計 8.3） | **12 定数すべて**を実装。`{operation}` を含むこと、`InvalidArgument` が `{detail}` を含むことを検証 |
| API 別の対応（設計 8.2） | 純粋層では判定を行わない（Manager の責務）。結果型は任意のコードを載せられる形にしてある |

### 3.2 コールバック返却仕様反映

- 純粋層にコールバックは無い。結果型が `IsSuccess` / `ErrorCode` / `ErrorMessage` を保持する形で契約を表現している
- 配送（共通イベント → per-call callback）は Manager の責務のため未実装

### 3.3 success 時契約

- `IsSuccess == true` のとき `ErrorMessage == null` を、全結果型のテストで検証済み（`Success_HasNoErrorCodeAndNoMessage` ほか）
- `IsSuccess == false` のとき `ErrorMessage != null` を、`None` 誤指定の昇格を含めて検証済み

## 4. ビルド結果

- 実行コマンド:
  - `Unity.exe -batchmode -runTests -projectPath <repo> -testPlatform EditMode -testResults editmode-results.xml`
- 結果: **SUCCESS**
- 補足:
  - コンパイルエラー 0
  - `warning CS` は 8 件だがすべて既存の iOS / macOS PlayMode テスト（`FindObjectsByType` の obsolete）。**本実装由来の警告は 0**

## 5. テスト結果

- 実行したテスト: EditMode 全件（既存を含む）
- 結果サマリー（レビュー反映後の最終実行）:
  - 実行件数: 649
  - 成功: 649
  - 失敗: 0
- 失敗時の対応:
  - レビュー反映の過程で置換スクリプトのエスケープ崩れにより `WindowsClipboardJsonParser.cs` に構文エラーが 1 件発生した。修正して再実行し、全件通過を確認した
- 実行履歴:

| 実行 | 結果 | 備考 |
|---|---|---|
| 初回（実装直後） | 632 / 632 passed | 本実装分 59 ケース |
| ミューテーション注入時 | 627 / 632 passed（5 failed） | 意図どおり 3 変異を検出 |
| 復元後 | 632 / 632 passed | バイト単位で復元を確認 |
| レビュー反映後 | **649 / 649 passed** | 本実装分 76 ケース |
- 未実施項目:
  - PlayMode（設計 9.2）: Manager 未実装のため
  - 実機確認 M-1 〜 M-24（設計 9.3）: Manager 未実装のため

### 5.1 テスト詳細

| テスト観点 | テストファイル | ケース数 | 結果 | 備考 |
|---|---|---|---|---|
| 結果型の不変条件、エラーコード対応 | `Tests/Runtime/WindowsClipboardResultTests.cs` | 24 | ○ | 失敗が `IsEmpty` にならないこと、native / managed の全コードが**独自の**メッセージを持つこと（フォールバック落ちの検出付き）、`default` 構造体でもコレクションが null にならないこと |
| payload の排他とフラグ値 | `Tests/Runtime/WindowsClipboardPayloadsTests.cs` | 9 | ○ | ネイティブのビット値との一致、null format / null payload / null byte 配列の拒否 |
| JSON 生成とエスケープ | `Tests/Runtime/WindowsClipboardJsonBuilderTests.cs` | 12 | ○ | 制御文字全種 / 非 ASCII / サロゲートペア / null 要素 / 順序保持 |
| JSON パース | `Tests/Runtime/WindowsClipboardJsonParserTests.cs` | 19 | ○ | **壊れた JSON 8 ケース**、id 欠落の drop、非数値 timestamp、`long.MaxValue`、`ToUtcTime` の正常・範囲外 |
| 完了配送レジストリ | `Tests/Runtime/WindowsClipboardRequestTableTests.cs` | 12 | ○ | exactly-once、drain が両状態を回収、drain 後の遅延配送が no-op、drain 冪等 |
| 合計（本実装分） | — | **76** | ○ | 初回 59 件から、レビュー反映で 17 件追加 |

**件数に関する訂正**: 本ファイルの初版は合計を 64 件と記載していたが、実測は 59 件だった（結果 XML の `classname` 属性を数えた際に test-suite 要素を重複計上していた）。独立レビューの指摘により発覚し、上表は実測値である。

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
|---|---|---|---|
| dispatch 順序、空判定、遅延レンダリング二相 | `Tests/Runtime/WindowsClipboardManagerDispatchTests.cs` | 設計 9.1 の残り | Manager 未実装（次スライス） |
| 拒否経路、event 発火、teardown、COM 所有権、quit 再開 | `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs` | 設計 9.2 | Manager 未実装（次スライス） |
| 実機 24 項目 | — | 設計 9.3 M-1 〜 M-24 | Manager 未実装（次スライス） |

## 6. Definition of Done（設計 11 章に対する現時点の判定）

- △ 27 のネイティブ API に対応する C# 公開 API が存在する（純粋層のみ完了。Manager 未実装）
- ○ 結果型が `pError` を保持し、`bool` を直接返す API を作っていない
- △ 非同期の exactly-once 配送（レジストリは完成しテスト済み。Manager 結線が未実装）
- \- `Awaitable` / `CancellationToken`（次スライス）
- \- `Initialize` / shutdown 状態機械 / COM 所有権 / 遅延レンダリング（次スライス）
- ○ 全 public 型・メソッドに英語の XML ドキュメントコメント
- ○ ログ規約の書き分け（builder / parser / 結果型はエントリログ無し）と逸脱の明記
- ○ クリップボード内容をログに出していない
- ○ `#nullable enable` / `using` 配置 / クラスガードが既存ファイルと同形
- ○ `.meta` を手動作成していない
- ○ 6.5 の P1〜P5 自己点検（接頭辞、1 ファイル 1 主型、他プラットフォーム非変更、`Common/` 非追加）
- ○ EditMode 全件 passed、既存テストの破壊なし
- ○ 追加したテストが「壊すと落ちる」ことをミューテーションテストで実証（レビュー結果を参照）
- \- PlayMode / 実機確認 / `testing.md` 更新（次スライス）

## 7. 実行確認

- 提示文: 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用する（次は 7.9 ステップ 2 の Manager 骨格、または `review-implementation-feature`）
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの差分は保持したまま終了
- ユーザー回答: **実行する**（2026-09-07）。この実装結果を採用し、`review-implementation-feature` へ引き継ぐ
