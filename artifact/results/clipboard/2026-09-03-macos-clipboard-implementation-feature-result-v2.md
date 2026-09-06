# 実装結果レポート

## 基本情報

- 日付: 2026-09-03
- 機能名: clipboard
- 対象プラットフォーム: macOS
- ブランチ: `feature/UNT-10`
- 実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md`
- **対象範囲: 段 1（型・定数・Reader・Builder・Parser）**
- 前版: `2026-09-03-macos-clipboard-implementation-feature-result-v1.md`（**取り消し済み。0.1 参照**）

## 0. 位置づけ

### 0.1 v1 の取り消し

v1 は旧計画（design-v9 以前）の段 1「`IosClipboardJsonReader` を `ClipboardJsonReader` へ改名して iOS / macOS で共有する」を実装し、EditMode 388 / PlayMode 44 とも pass していた。

その後 **「機能ディレクトリに共通ファイルを作らない」方針**が決まったため、`git restore` で完全に巻き戻した。iOS 側のファイルは 1 バイトも変更されていない状態に戻っている。

- 方針: `agent-rules/coding-rules/common.md`「命名: OS 接頭辞と、共通ファイルを作らない方針」
- 巻き戻し確認: 巻き戻し直後に EditMode 388 件 pass、`IosClipboardJsonReaderTests` 34 件 pass

### 0.2 現在の段構成（design-v10、3 段）

| 段 | 範囲 | 状態 |
| --- | --- | --- |
| **1** | Payloads / Constants / ErrorInfo / **Reader（複製）** / 結果型 12 種 / ChangeEvent / Builder / Parser + EditMode 4 テスト | **本レポート（完了）** |
| 2 | Manager 骨格 + Copy / Append / Read / ReadData / Clear（監視には触れない） | 未着手 |
| 3 | 残り 10 操作 + 監視一式 | **着手前に監視部分の再レビューが必要** |

## 1. 実装サマリー

### 1.1 計画書由来の実装

18 ファイル中 **17 ファイル**を作成した（残り 1 は段 2 の `MacClipboardManager.cs`）。**既存ファイルの変更はゼロ。**

| 節 | 実装 |
| --- | --- |
| 5.1 | `MacClipboardPayloads.cs` — scope / creation request / ownership / content / options / 3 enum / UTI 定数 |
| 5.1.1 | `MacClipboardDetectionPatternExtensions` — 検出パターン 11 件・accessBehavior 5 件・metadataType 1 件の rawValue 対応表 |
| 5.2 | `MacClipboardErrorInfo.cs` — `Create(long, string?)` と全エラーコード定数 |
| 5.2.1 / 5.2.2 | `MacClipboardConstants.cs` — `MacClipboardOperations` 15 + 共有キー、`MacClipboardLimits` 3 定数 |
| 5.4.1 | `MacClipboardJsonReader.cs` — `IosClipboardJsonReader.cs` からの複製 + `TryGetInt64` |
| 5.5 | 結果型 12 種を 10 ファイル + `MacClipboardChangeEvent.cs` |
| 5.3 | `MacClipboardJsonBuilder.cs` — 入力 JSON 7 種 |
| 5.4 | `MacClipboardJsonParser.cs` — 出力 12 種 |

設計で重視されていた次の点を実装に落とした。

- **`changeCount` は全経路 `long`**（D-14）。`TryGetInt64` を使い、`int.MaxValue` 超の応答が 9006 に化けないようにした
- **検出パターンの rawValue は複数形**（`phoneNumbers` / `emailAddresses` / `probableWebURL`）。iOS の単数形と異なるため、対応表を明示的に持たせた
- **`BuildOptionsJson` / `BuildMatchingTypesJson` は `null` を返す**。空文字ではなく C# の null にすることで、P/Invoke が NULL ポインタを渡し、ネイティブが「既定を使う」「フィルタなし」と解釈する
- **明示的 null と欠落の区別**（5.4）。`ReadDataJson.data` / `DetectedValuesJson` の optional / `DetectedMetadataJson.contentTypeIdentifier` はキーの存在を要求し、null は「要求したが未検出」として読む
- **`scope.name` は general でのみ欠落を許容**。Swift の合成エンコーダが省略するため、ここだけ例外
- **`default(T)` は未初期化値**（D-15）。不変条件は factory 生成値に限定し、その旨をテストで固定した

### 1.2 実装時の追加判断

**判断 1: `MacJsonValue.MemberNames()` を追加した**

Parser が `representations` を読むために、オブジェクトのキー列挙が必要になった。複製元の `IosClipboardJsonReader` はキーを名前で引くだけで足りていたため、この API を持っていなかった。

`representations` は**キーが uniform type identifier というデータ**なので、固定スキーマとして名前で引けない。`internal IReadOnlyCollection<string> MemberNames()` を追加した。iOS 側の複製元には手を入れていない。

**判断 2: `BuildContentJson` の representations を序数順にソートした**

計画書 N4-3 の指示どおり。`IReadOnlyDictionary` の列挙順は保証されないため、ソートしないとテストがハッシュ次第で通ったり落ちたりする。ネイティブ側は辞書としてデコードするので順序に依存しない。テストのコメントにこの理由を残した。

**判断 3: 日付のパース失敗はイベント全体を落とさない**

`calendarEvents` の `startDate` / `endDate` が ISO 8601 として読めない場合、そのフィールドだけ null にして解析を続ける。マッチの残りは使えるうえ、書式は検出器側の都合で変わりうるため。

## 2. 変更ファイル

### 2.1 新規作成（Runtime）— 17 ファイル / 3,364 行

`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/` 配下。すべて `#nullable enable` + `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`。

`MacClipboardPayloads.cs` / `MacClipboardErrorInfo.cs` / `MacClipboardConstants.cs` / `MacClipboardJsonReader.cs` / `MacClipboardJsonBuilder.cs` / `MacClipboardJsonParser.cs` / `MacClipboardOperationResult.cs` / `MacClipboardOwnershipResult.cs` / `MacClipboardReadResult.cs` / `MacClipboardReadDataResult.cs` / `MacClipboardSnapshotResult.cs` / `MacClipboardChangeCountResult.cs` / `MacPasteboardScopeResult.cs` / `MacClipboardDetectionResults.cs` / `MacClipboardAccessBehaviorResult.cs` / `MacClipboardForegroundChangeResult.cs` / `MacClipboardChangeEvent.cs`

### 2.2 新規作成（Tests）— 4 ファイル / 1,377 行

`Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/` 配下。

| ファイル | 件数 |
| --- | ---: |
| `MacClipboardJsonReaderTests.cs` | 39 |
| `MacClipboardJsonParserTests.cs` | 29 |
| `MacClipboardJsonBuilderTests.cs` | 24 |
| `MacClipboardResultTests.cs` | 22 |
| 合計 | **114** |

### 2.3 既存変更

**なし。** `git status` で確認済み。`Runtime/Clipboard/Ios*` / `Android*`、`Tests/Runtime/Ios*` / `Android*`、`AssemblyInfo.cs`、asmdef のいずれにも変更が無い。

これは「共通ファイルを作らない」方針の副次的な効果である。段 1 が既存ファイルを触らないため、**iOS / Android への影響がゼロであることが構造的に保証される**。

### 2.4 作業前から存在した未コミット変更（本実装とは無関係）

1.2.0 xcframework の削除 67 件 + 変更 5 件、1.3.0 xcframework の未追跡ファイル。**一切触れていない。**

## 3. エラー契約反映

段 1 は Manager を含まないため、エラーの**返却**は段 2 以降になる。段 1 で実装したのは契約を表現する型と、それを組み立てる部分である。

| 項目 | 反映状況 |
| --- | --- |
| エラーコード定数 | 1.5 の表 22 件 + C# の 9001-9007 を `MacClipboardErrorCodes` に全件定義。省略記法を使っていない |
| `IsSuccess == true` のとき `Error == null` | 全結果型の factory で保証。`MacClipboardResultTests` で検証 |
| 失敗時の payload | 参照型は null / 値型は既定値 / 直接持つコレクションは空、の 3 分岐で実装・検証 |
| `MacClipboardReadDataResult` の例外 | 成功 + `Data == null` を許す唯一の型として実装・検証 |
| `Create(long, string?)` の narrow | `int` 値域外を `Unknown`(1599) に落とす。unchecked キャストの折り返しを避けた |
| 9006（ResponseParseFailed）の判定 | Parser が `false` を返す条件として実装。コードの付与は段 2 の Manager が行う |

## 4. ビルド結果

| 項目 | 結果 |
| --- | --- |
| コンパイル | **成功**（`error CS` 0 件） |
| Unity | 6000.4.2f1 |

**実行時の注意（v1 から継続して記録）:** `-runTests` と `-quit` は併用できない。併用するとテスト完了前に終了し、結果 XML が生成されない。

## 5. テスト結果

| プラットフォーム | 総数 | 成功 | 失敗 | 判定 |
| --- | ---: | ---: | ---: | --- |
| EditMode | **502** | 502 | 0 | **Passed** |
| PlayMode | 44 | 44 | 0 | **Passed** |

段 1 開始前は EditMode 388 件。**114 件を追加して 502 件、既存 388 件も全て pass。**

### 5.1 実装中に検出した自分のバグ 3 件

いずれもテストが検出した。設計の問題ではない。

| # | 内容 | 対処 |
| --- | --- | --- |
| 1 | `TryGetInt64` のテストで、JSON の `null` メンバに `GetMemberOrNull(...)!` を付けて逆参照し `NullReferenceException` | `GetMemberOrNull` が **JSON null を C# null に畳む**仕様だったため。テストを直すだけでなく、**その挙動自体を検証するテストに作り替えた**（`TryGetInt64_OnAnExplicitNullMember_IsNotReachedThroughGetMemberOrNull`）。5.4 の「null と欠落の区別」に直結する挙動なので、明示しておく価値があると判断した |
| 2 | Builder テストのソースに**生の制御文字**（U+0001）が混入 | `` のエスケープ表記に修正 |
| 3 | Parser が存在しない `MemberNames()` を呼んでいた | Reader に追加（判断 1） |

### 5.2 未実施ケース

| ケース | 理由 |
| --- | --- |
| ガードチェーン・dispatch・単一実行ガード | **段 2**。Manager がまだ無い |
| 監視の active / pending 遷移 | **段 3** |
| P/Invoke の実マーシャリング（V-2 / V-6） | 実機の macOS Standalone Player が必要。段 3 完了後の手動確認 |
| 7.5 の手動確認 32 項目 | 同上 |

## 6. Definition of Done（計画書 10 章）

| 条件 | 判定 |
| --- | --- |
| 対象範囲のファイルが作成され、コンパイルエラーが無い | 満たす（17 + 4 ファイル） |
| `public` メンバに英語の XML コメント | 満たす |
| ログが shape / count / flag のみ | 満たす（段 1 の全ファイルがログを出さない。逸脱理由をファイル先頭またはクラス `<summary>` に明記） |
| 逸脱の宣言が既存の使い分けに従っている | 満たす（Builder / Parser / Reader はクラス `<summary>` 内の `<para>`、payload / 結果型は `#if` 直後の `//`） |
| 対象範囲の新規テストが pass | 満たす（114 件） |
| **既存テストが全件 pass** | **満たす（EditMode 502 / PlayMode 44、失敗 0）** |
| EditMode テストが Manager インスタンスを生成していない | 満たす（段 1 に Manager が無い） |
| PlayMode テストが `[TearDown]` で `ResetForTests()` を呼ぶ | 該当なし（PlayMode を変更していない） |
| `.meta` を新規作成していない | 満たす（Unity が自動生成） |
| **新規ファイルに OS 接頭辞**（`common.md`） | 満たす（Runtime 17 / Tests 4 すべて `Mac` 接頭辞） |
| **他プラットフォームのファイルを変更していない** | 満たす（2.3） |
| 要検証事項の更新 | V-1 はクローズ済み。段 1 で新たに判明した要検証事項は無い |

## 7. 次のステップ

**段 2（Manager 骨格 + Copy / Append / Read / ReadData / Clear）に着手可能。**

段 2 に掛かる未解決の A1 は無い。着手前に読むべきは 5.6.2（決定 2 の保証範囲）、5.6.4（`TryPassGuards` 分割と実行段階表）、5.6.10（シームの粒度）。

**段 3 の着手前には、監視部分に絞った再レビューが必要。** v5 → v6 → v7 と 3 ラウンド連続で監視領域から A1 が出ており、v8 の D-16（teardown 競合の再発行規則）と v10 の段の切り直しは、まだレビューを通っていない。
