# 実装レビュー結果

## 基本情報

- 日付: 2026-09-07
- 対象ブランチ: `feature/UNT-11`
- レビュー対象: 未コミットの新規ファイル 19 件（Runtime 14 / Tests 5）。設計 v8 の 7.9 ステップ 1（純粋層）に相当するスライス
- 実装計画: `artifact/features/clipboard/designs/2026-09-05-windows-clipboard-design-v8.md`
- 実装結果: `artifact/features/clipboard/results/2026-09-07-windows-clipboard-implementation-feature-result-v1.md`
- プラットフォーム: Windows
- レビュー方式: 自己レビュー + **別モデルによる独立レビュー**（サブエージェント）+ **ミューテーションテスト**

`git diff develop...HEAD` は develop 取り込みマージのファイル群を含むだけで本実装を含まないため（実装は未コミット）、作業ツリーの新規ファイルを対象とした。

---

## レビュー概要

Windows Clipboard の C# 層のうち、Manager 本体を除く純粋層（結果型・エラーコード・payload・JSON builder / parser・完了配送レジストリ）と、その EditMode テストを対象とする。

3 つの独立した検査を行った。

| 検査 | 目的 | 結果 |
|---|---|---|
| 自己レビュー | 計画整合性・P1〜P5・ルール準拠 | 構造面は計画どおり |
| 独立レビュー（別モデル） | 実装の正確性・テストの有効性 | high 4 / medium 6 / low 7 を検出 |
| ミューテーションテスト | 「テストが壊すと落ちるか」の実証 | 3 変異すべてを 5 テストが捕捉 |

---

## ミューテーションテスト（停止基準「壊すと落ちる」の実証）

`review-implementation-feature` の停止基準は「追加したテストが、壊すと落ちることを確認済みであること。通ることの確認では足りない」と要求している。実装へ意図的に 3 つの変異を注入し、EditMode を実行した。

| 変異 | 破壊した契約 | 落ちたテスト |
|---|---|---|
| M1: `TryClaim` がエントリを削除しない | 7.6.1 の exactly-once | `TryClaim_SucceedsExactlyOnce` / `TryClaim_AlsoReleasesTheNativeIdMapping` / `MarkUndelivered_AfterAClaimReportsFailureInsteadOfResurrecting` |
| M2: `Failure` の `None` 昇格を削除 | 8.4「失敗は必ずメッセージ」 | `Failure_WithNoneCode_IsPromotedToUnknownSoTheMessageIsNeverNull` |
| M3: 改行のエスケープを削除 | 不正な JSON を生成 | `BuildStringArray_EscapesQuotesAndControlCharacters` |

結果: `total=632 passed=627 failed=5`。3 変異すべてを検出した。変異は 3 ファイルとも scratchpad のバックアップから復元し、バイト単位の一致と `MUTATION` 文字列の非残留を確認した。復元後の再実行は `632/632 passed`。

**副次的な発見**: `ADeliveryQueuedBeforeADrain_BecomesANoOpAfterIt` は M1 では落ちなかった。このテストは `ClaimAll` 経由の経路を通るため、`TryClaim` 単体の破壊を検出しない。テストとしては有効だが、カバーしているのは `ClaimAll` である。

**運用上の反省**: 独立レビューをミューテーション注入と並行で走らせたため、レビュアーが変異入りのツリーを読み、指摘 1・2 として報告した。レビュアーの分析（破られた契約、落ちるテストの特定）はいずれも正確だった。ツリーを変更する検査とレビューは直列に実行すべきだった。

---

## 重大な問題（high）

いずれも本レビューで修正済み。

### H1: エラーメッセージ網羅テストが「壊しても落ちない」死んだテストだった

- `Tests/Runtime/WindowsClipboardResultTests.cs`（旧 `EveryNativeErrorCode_HasAMessage` / `EveryManagedErrorCode_HasAMessageAndIsNotNative`）
- **根拠**: 検証が `Assert.IsNotNull(value.ToMessage(Op))` のみ。`ToMessage` には `_ => $"Unknown error ({(int)code})"` のフォールバックがあるため、switch から任意のコードを削除しても非 null が返り、**テストは絶対に落ちない**。計画 9.1 の「0〜19 と 1000 番台のメッセージ対応」を検証したことになっていなかった
- **修正**: `StringAssert.DoesNotStartWith("Unknown error (")` を追加。あわせて `AnUndefinedCode_FallsThroughToTheCatchAll` を新設し、「フォールバックが存在すること」自体も固定した（ガードのガード）

### H2: `TryParseAvailability` が壊れた payload を「履歴オフ」として成功扱いしていた

- `Runtime/Clipboard/WindowsClipboardJsonParser.cs`
- **根拠**: `JsonUtility.FromJson<AvailabilityDto>("{}")` は例外を投げず、両フィールドが既定値 `false` の DTO を返す。キーが無い payload・キー名が違う payload・想定外のオブジェクトが、いずれも正当な「履歴オフ・ローミングオフ」と区別できなかった。`WindowsClipboardAvailabilityResult` は UI 分岐に使う想定のため、誤って「履歴が無効です」と表示する事故になる
- **修正**: `LooksLikeObjectWithKeys` でオブジェクト形状と必須キー 2 つの存在を検証してから解析。欠落は失敗（呼び出し側が `ResultParseFailed`(1007) を返せる）

### H3: 配列 payload の形状未検証により、壊れた payload が「空成功」に化けていた

- `Runtime/Clipboard/WindowsClipboardJsonParser.cs`
- **根拠**: `Wrap` は `{"values":` + json + `}` の単純連結で、json が配列であることを検査していなかった。スカラやオブジェクトを渡すと `dto.values == null` となり、`TryParseStringArray` / `TryParseHistoryItems` は **true + 空リスト**を返す。呼び出し側は 2.4 の「空クリップボードでも `[]` が返る」を根拠に空成功として扱うため、壊れた payload が「クリップボードが空」「履歴が空」に化ける
- **修正**: `TryWrapArray` で `[` / `]` を確認。あわせて `dto.values == null` を true ではなく **false** に変更した

### H4: `Reset()` が ticket を巻き戻し、リセットを跨いだ配送が別リクエストの結果を奪い得た

- `Runtime/Clipboard/WindowsClipboardRequestTable.cs`
- **根拠**: `Reset()` が `_nextTicket = 1` に戻すため、リセット直後の発行が過去と同じ値になる。リセット時点で dispatcher に enqueue 済みの遅延 action は古い ticket を握ったままで、後から走ると新規リクエストの ticket を `TryClaim` で奪い、無関係な結果を配送する。計画 7.6.5 は generation の確認を前提にしているが、本テーブルに generation は無い
- **修正**: `Reset()` で ticket counter を巻き戻さない（プロセス寿命で単調増加）。理由を `<remarks>` に記載

---

## 改善提案（medium）

いずれも本レビューで修正済み。

### M1: 計画 9.1 が明示する「壊れた JSON」のパーサテストが 1 件も無かった

計画 9.1 / 6.2 がともに「壊れた JSON / 空文字 / null」を要求していたが、実装は null / 空文字のみだった。**これが H2・H3 が実装時に検出されなかった直接の原因**である。閉じ括弧なし / 配列でないオブジェクト / 非 JSON / スカラ / キー欠落など 8 ケースを追加した。

### M2: `Bytes(format, null)` が空 base64 を黙って生成していた

null の byte 配列が「空の base64」として検証を通過し、ゼロ長の形式をクリップボードへ配置してしまう。null のまま保持して `TryValidate` で弾き、8.3 の `InvalidArgument`(1005) へ落ちるようにした。

### M3: `default(struct)` で「Never null」の XML コメントに反していた

`WindowsClipboardStringListResult.Values` / `WindowsClipboardBytesResult.Data` / `WindowsClipboardHistoryResult.Items` は自動プロパティのため、`readonly struct` の `default` では null になる（構造体はフィールド初期化子を実行しない）。バッキングフィールド + `?? Empty` に変更し、`default` を検証するテストを追加した。

### M4: `ToUtcTime()` が完全に未テストだった

実装時の追加判断で足した public API にテストが無かった。正常な FILETIME の変換と、`long.MaxValue`（`DateTimeOffset.FromFileTime` の範囲外）で例外ではなく null を返すことを追加した。

### M5: `RegisterAwaitingNative` の再登録で `_byNativeId` が孤児化し得た

既存 ticket を別の native id で再登録すると古いマッピングが残り、以後どの経路でも解放されない。7.6.1 の手順上は起きないが、`TryClaim` 側の防御と対称になるよう削除処理を追加した。

### M6: `new T[0]` がリポジトリ内で唯一の書き方だった

既存実装（Android / iOS / macOS の計 20 箇所）はすべて `Array.Empty<T>()`。とくに `ClaimAll` の早期 return は**呼ばれるたびに空配列を確保**しており、teardown ドレインが冪等に複数回呼ばれる契約（7.6.3）では回数が読めない。全 6 ファイルを `Array.Empty<T>()` に統一し、`ClaimAll` は static にキャッシュした。

---

## 軽微な指摘（low）

| # | 指摘 | 対応 |
|---|---|---|
| L1 | `IssueTicket` が ticket 1 を決して発行しない（初期値 1 + 先頭で `++`） | `_nextTicket` の初期値を 0 にして解消 |
| L2 | JsonBuilder のテストに `\b` / `\f` / `\r`、サロゲートペア、null 要素のケースが無い | 3 ケース追加（絵文字が壊れずに通ることを含む） |
| L3 | `FromNative` の範囲外コード（99 等）が未検証 | `FromNative_UnknownCodeIsNotClassifiedAsNative` を追加 |
| L4 | 履歴の id 欠落 drop パス、非数値 timestamp のパスが未カバー | `LogAssert` を使う 2 ケースを追加 |
| L5 | `WindowsClipboardHistoryResult` の非空リストのケースが無い | `HistoryResult_NonEmptyListIsNotEmpty` を追加 |
| L6 | 結果型 10 ファイルに機微情報の逸脱コメントが無い（macOS の結果型は全て持つ） | **未対応**。結果型はエントリログを持たない方針だが、その理由がファイル上に無い。次スライスで Manager と合わせて統一する |
| L7 | 1 ファイル 1 主型の例外が計画の宣言（`WindowsClipboardPayloads.cs` のみ）より多い（`WindowsClipboardErrorCodeExtensions` / `WindowsClipboardRequestState`） | **記録のみ**。いずれも主型と不可分な補助型で分割の必要は無いと判断。計画 6.1 の文言を「主型と密結合な補助型は同居可」に緩めるか、次版で判断表に追記する |
| L8 | パース失敗ログが `ex.Message` をそのまま出す（payload 断片の混入は未確認） | **要検証**。`JsonUtility` の例外メッセージは通常パース位置のみだが保証は無い。次スライスで `ex.GetType().Name` のみに絞るか判断する |

---

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: **○**（純粋層は Bridge に依存せず、Manager からのみ参照される設計。層の逆流なし）
- 変更ファイル一覧との一致: **○**（6.1 の 15 ファイル中 14 件。Manager のみ意図的に未実装）
- テスト方針の網羅性: **○**（修正後。計画 9.1 の純粋層該当分をすべてカバー。修正前は「壊れた JSON」が欠落し △ だった）
- エラーケース全実装: **○**（8.1 の 20 定数、8.3 の 12 定数すべて。メッセージ文言も一致）
- 返却仕様との整合: **○**（8.4 の不変条件をファクトリで担保し、テストで固定）

## プロジェクトルール適合チェック

- `common.md` 準拠: **○**（OS 接頭辞、共通ファイル非作成、`Runtime/Common/` 非追加、他プラットフォーム非変更）
- `csharp.md` 準拠: **△**（XML ドキュメント・英語コメント・命名は適合。機微情報によるログ規約逸脱の明記が結果型 10 ファイルで欠落。L6）
- Bridge 実装品質（スレッド安全性・メモリ管理）: **-**（純粋層に P/Invoke・アンマネージドバッファ・スレッド跨ぎは無い。次スライスの対象）
- 既存 API 互換性: **○**（新規追加のみ。破壊的変更なし）

### プラットフォーム独立性（P1〜P5）

| 観点 | 判定 | 根拠 |
|---|---|---|
| P1 OS 接頭辞（テスト含む） | ○ | 19 ファイルすべて `Windows` 接頭辞。型名・クラス名も一致 |
| P1b 単一プラットフォーム / ガード一致 | ○ | 全テストが Windows 専用。`#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` を対象型と同一に付与 |
| P2 他プラットフォーム非変更 | ○ | 作業ツリーに `Android*` / `Ios*` / `Mac*` の変更なし（機械確認） |
| P3 既存型の再利用判断 | ○ | Android の接頭辞なし型を使わず Windows 専用型を新設。iOS の JSON リーダーも共有せず複製 |
| P4 `Runtime/Common/` への追加 | ○ | 追加なし |
| P5 二重ガード | - | 純粋層に P/Invoke が無いため対象外。次スライスで適用 |
| 既知の逸脱 11 件の引用 | ○ | していない |

---

## テストカバレッジ

### カバー済み

- 結果型: 成功時 `ErrorMessage == null`、失敗時のメッセージ必須、失敗を `IsEmpty` にしない、`HasFormat` / `Value` と `IsSuccess` の独立、`default` でもコレクションが null にならない
- エラーコード: native 19 件・managed 12 件が**独自のメッセージを持つこと**（フォールバック落ちの検出付き）、範囲外コードの分類
- payload: ネイティブのビット値との一致、`Kind` の排他、null format / null payload / null byte 配列の拒否
- JsonBuilder: `\` `"` `\n` `\t` `\b` `\f` `\r` / 制御文字の `\uXXXX` / 非 ASCII 通過 / サロゲートペア保持 / null 要素 / 順序保持
- JsonParser: 正常系、空配列、エスケープ解除、**壊れた JSON 8 ケース**、id 欠落の drop、非数値 timestamp、`long.MaxValue`、`ToUtcTime` の正常・範囲外
- RequestTable: 計画 9.1 の 8 項目すべて（ticket 発行、状態遷移、exactly-once、未知 ID の無害、両状態のドレイン、ドレイン後の遅延配送 no-op、ドレイン冪等、受付前拒否の追跡）

### 不足（次スライスへ持ち越し）

- `IssueTicket` のラップ経路（4 億件到達後）。`_nextTicket` を注入する seam が無く現状は検証不能
- `Reset()` 前後の ticket 衝突を直接検証するテスト（実装は修正済みだが、テストは未追加）
- Manager 側の観点（dispatch 順序、空判定、遅延レンダリング二相、teardown、COM 所有権、quit 再開）。設計 9.1 / 9.2 の残りで、Manager 実装後に対応

---

## ビルド・テスト結果

| 実行 | 結果 |
|---|---|
| 初回（実装直後） | `total=632 passed=632 failed=0` |
| ミューテーション注入時 | `total=632 passed=627 failed=5`（意図どおり） |
| 復元後 | `total=632 passed=632 failed=0` |
| レビュー修正後 | `total=649 passed=649 failed=0`（本実装分 59 → 76 ケース） |

レビュー反映の途中で、置換スクリプトのエスケープ崩れにより `WindowsClipboardJsonParser.cs` に構文エラーを 1 件作り込んだ。修正のうえ再実行し、全件通過とコンパイルエラー 0 を確認している。

コンパイルエラー 0、本実装由来の警告 0（既存の警告 8 件はすべて iOS / macOS の PlayMode テスト）。

---

## 総合評価

**要修正（軽微）** — high 4 件・medium 6 件はすべて本レビュー内で修正済み。残るのは low 3 件（L6 / L7 / L8）で、いずれも次スライスで Manager と合わせて判断すればよい水準。

特筆すべき点:

- **テストの網羅性ではなく有効性が最大の問題だった。** 「通るテスト」は 59 件あったが、そのうち 2 件は絶対に落ちない死んだテストであり、パーサの寛容さ（H2 / H3）を検出できる形になっていなかった。ミューテーションテストと独立レビューの両方が、この種の欠陥を別々の角度から捕まえた
- 構造面（命名・ガード・エラーコード網羅・P1〜P5）は初回から計画どおりで、修正は不要だった
- 独立レビューをツリー変更と並行実行したのは手順上の誤りで、次回は直列化する

## 修正対応の確認

- 提示文: 「このレビュー結果をもとに修正を行いますか？」
- 選択肢:
  - 修正する: 指摘内容を反映して `implement-feature` に準じて修正する
  - レビュー結果のみで終了: 修正は行わず終了する
- ユーザー回答: high 4 / medium 6 / low 4 は**本レビュー内で修正済み**（最終実行 649 / 649 passed）。残る L6 / L7 / L8 は次スライス（Manager 実装）で Manager と合わせて判断する
