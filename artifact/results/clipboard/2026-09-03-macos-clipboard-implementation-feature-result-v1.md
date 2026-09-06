# 実装結果レポート

## 基本情報

- 日付: 2026-09-03
- 機能名: clipboard
- 対象プラットフォーム: macOS
- ブランチ: `feature/UNT-10`
- 実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v9.md`
- **対象範囲: 段 1 のみ**（12 章の 4 段階のうち第 1 段）

## 0. 段 1 の位置づけ

計画書 12 章は実装を 4 段に分けている。本レポートは**段 1（共有基盤）** のみを対象とする。

| 段 | 範囲 | 状態 |
| --- | --- | --- |
| **1** | `ClipboardJsonReader` への改名 + ガード拡張 + `TryGetInt64` 追加 + 参照置換 | **本レポート（完了）** |
| 2 | Payloads / Constants / ErrorInfo / 結果型 12 種 / Builder / Parser + EditMode テスト | 未着手 |
| 3 | Manager 骨格 + Copy / Append / Read / ReadData / Clear（監視には触れない） | 未着手 |
| 4 | 残り 10 操作 + 監視一式 | **着手前に監視部分の再レビューが必要** |

段 1 は**振る舞いを一切変更しない**。既存 iOS テストの全 pass が完了条件である。

## 1. 実装サマリー

### 1.1 計画書由来の実装

計画書 4.2 / D-1 / 5.4（A1-2 由来の `TryGetInt64`）に対応する。

- `IosClipboardJsonReader.cs` → `ClipboardJsonReader.cs` へ改名（`git mv` で `.cs` と `.cs.meta` を対で移動）
- static クラス名を `IosClipboardJsonReader` → `ClipboardJsonReader` に変更
- コンパイルガードを `#if UNITY_IOS || UNITY_EDITOR` → `#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR` に拡張
- クラスの XML `<summary>` から iOS 固有記述（`allTypeIdentifiers` / `readData` / `loadItem` is polymorphic on `kind`）を除き、プラットフォーム中立な説明に書き換え
- `JsonValue.TryGetInt64(out long)` を追加。既存 `TryGetInt(out int)` は変更せず併存
- 参照 44 箇所を置換（Runtime 2 / Tests 42）

改名の根拠は `agent-rules/coding-rules/common.md`「命名: OS 接頭辞」。2 つ以上のプラットフォームが使う型に接頭辞を付けない、および「単一専用の型が 2 番目の利用者を得たら接頭辞を外す」に該当する。

### 1.2 実装時の追加判断

**判断 1: テストファイルも改名した（計画書 4.4 からの変更）**

計画書 4.4 は「ファイル名は iOS 由来の網羅ケースを保つため据え置く」としていたが、次の理由で `IosClipboardJsonReaderTests.cs` → `ClipboardJsonReaderTests.cs` に改名し、クラス名も `ClipboardJsonReaderTests` に変更した。

- 据え置くと `IosClipboardJsonReaderTests` が `ClipboardJsonReader` をテストする形になり、**今回新設した OS 接頭辞ルールが防ごうとしている誤解そのもの**を生む
- 計画書の「iOS 由来の網羅ケースを保つ」という理由は、テスト内容が iOS 固有であることを意味しない。テスト対象は JSON リーダーであり、iOS 固有の入力を使っていない
- 計画書 4.4 の記述は、改名根拠が誤っていた時期（v1〜v8）に書かれたもの

`git mv` で `.cs` と `.cs.meta` を対で移動しており、GUID は保持されている。

**判断 2: テストファイルのガードも対象型に合わせた**

`ClipboardJsonReaderTests.cs` のガードを `#if UNITY_IOS || UNITY_EDITOR` → `#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR` に拡張した。テストアセンブリは `includePlatforms: ["Editor"]` なので実行上は `UNITY_EDITOR` だけで足りるが、**対象型とガードを揃えておかないと、対象が macOS で有効なのにテストだけ除外される形が残る**ため。

`IosClipboardJsonBuilderTests.cs` のガードは `#if UNITY_IOS || UNITY_EDITOR` のまま変更していない。これは iOS の Builder のテストであり、`ClipboardJsonReader` は `UNITY_IOS` でもコンパイルされるため問題ない。

**判断 3: テストのコメントも中立化した（識別子置換では拾えなかった）**

識別子の置換だけでは、コメント内の "iOS" が残ることに後から気づいた。`ClipboardJsonReaderTests.cs` の 2 箇所を直した。

| 箇所 | 修正前 | 修正後 |
| --- | --- | --- |
| クラス `<summary>` | `backing the iOS clipboard parser` | `backing the clipboard parsers (iOS and macOS)` |
| 行内コメント | `snapshot.allTypeIdentifiers`（iOS のキー名を断定） | `an array of arrays, as in the snapshot payloads (iOS allTypeIdentifiers, macOS itemTypes)` |

**テスト内容自体はプラットフォーム非依存であることを確認済み。** 34 件はすべて JSON の読み取り仕様（パース・アクセサ・エスケープ・サロゲートペア・深さ制限・base64 のデコード長）を検証しており、`UIPasteboard` や `IosClipboard*` 型に依存する箇所は無い。改名の前提は正しかった。

**判断 4: `TryGetInt64` の実装方針**

既存 `TryGetInt` と同じ構造にし、`long.TryParse` を先に試して失敗したら整数値の `double`（`"3.0"` 形式）を受け入れる二段構えにした。`JSONSerialization` がどちらの形でも出力しうるという既存コメントの前提をそのまま引き継いでいる。

## 2. 変更ファイル

### 2.1 新規作成

なし。

### 2.2 既存変更

| ファイル | 変更 |
| --- | --- |
| `Runtime/Clipboard/IosClipboardJsonReader.cs` → `Runtime/Clipboard/ClipboardJsonReader.cs` | 改名（`git mv`）、クラス名、ガード拡張、クラス doc 中立化、`TryGetInt64` 追加 |
| `Runtime/Clipboard/IosClipboardJsonReader.cs.meta` → `Runtime/Clipboard/ClipboardJsonReader.cs.meta` | 改名（`git mv`）。GUID `f66926606bc0c4f0fb64cfaad5989ad6` を保持 |
| `Runtime/Clipboard/IosClipboardJsonParser.cs` | 参照 2 箇所を置換 |
| `Tests/Runtime/IosClipboardJsonReaderTests.cs` → `Tests/Runtime/ClipboardJsonReaderTests.cs` | 改名（`git mv`）、クラス名、参照 40 箇所を置換、ガード拡張 |
| `Tests/Runtime/IosClipboardJsonReaderTests.cs.meta` → `Tests/Runtime/ClipboardJsonReaderTests.cs.meta` | 改名（`git mv`）。GUID を保持 |
| `Tests/Runtime/IosClipboardJsonBuilderTests.cs` | 参照 2 箇所を置換 |

`.meta` は新規作成していない。既存 `.meta` の移動のみで、`common.md`「ファイル作成ルール」に抵触しない（計画書 4.2 で承認済みの逸脱）。

### 2.3 非変更（対象だが未変更）

| ファイル | 理由 |
| --- | --- |
| `Runtime/Clipboard/Ios*`（Parser を除く） | 改名の影響を受けない |
| `Runtime/Clipboard/Android*` | 同上 |
| `Runtime/AssemblyInfo.cs` | `InternalsVisibleTo` は既存のままで足りる |
| `Tests/Runtime/*.asmdef` / `Tests/PlayMode/*.asmdef` | 変更不要 |
| `JsonValue` / `JsonValueKind` / `JsonBase64Status` | 元から接頭辞なしの共有型。名前を変更していない |
| `TryGetInt(out int)` | iOS 側の呼び出しを変えないため残置 |

### 2.4 作業前から存在した未コミット変更（本実装とは無関係）

作業開始時点で 72 件の未コミット変更があった。**本実装では一切触れていない。**

- 1.2.0 xcframework の削除 67 件 + 変更 5 件
- 1.3.0 xcframework は未追跡ファイルとして存在

## 3. エラー契約反映

段 1 はエラー契約を導入しない。`ClipboardJsonReader` は既存どおり「不正な文書では `null` を返し、例外を投げない」契約を維持している。

`TryGetInt64` も既存 `TryGetInt` と同じく、数値でない場合と範囲外の場合に `false` を返し、例外を投げない。

## 4. ビルド結果

| 項目 | 結果 |
| --- | --- |
| コンパイル | **成功**（`error CS` 0 件） |
| Unity バージョン | 6000.4.2f1 |
| 実行方法 | `Unity -batchmode -runTests -projectPath . -testResults <xml> -logFile <log>` |

**注意点として記録:** 初回は `-quit` を併用したためテスト結果 XML が生成されなかった。`-runTests` と `-quit` は併用できない（テスト完了前に終了する）。`-quit` を外して再実行した。

## 5. テスト結果

| プラットフォーム | 総数 | 成功 | 失敗 | スキップ | 判定 |
| --- | ---: | ---: | ---: | ---: | --- |
| EditMode | 388 | 388 | 0 | 0 | **Passed** |
| PlayMode | 44 | 44 | 0 | 0 | **Passed** |

### 5.1 テスト詳細

- **既存テストの全 pass を確認**。これが段 1 の完了条件である
- `ClipboardJsonReaderTests` 34 件すべて Passed（改名後のクラス名で実行されていることを確認）
- 新規テストは追加していない。段 1 は振る舞いを変更しないため

### 5.2 未実施ケース詳細

| ケース | 理由 |
| --- | --- |
| `TryGetInt64` の単体テスト | **段 2 で追加する。** 計画書 7.1 が「`changeCount` が `int.MaxValue + 1` の各 JSON が成功として解析されること」を段 2 の Parser テストとして定義しており、そこで実効的に検証される。段 1 時点では呼び出し側が存在しない |
| macOS Standalone でのコンパイル | 段 1 では macOS 固有コードが無く、ガード拡張のみ。段 2 以降で `MacClipboard*` を追加した時点で確認する |

## 6. Definition of Done（計画書 10 章）

| 条件 | 判定 |
| --- | --- |
| 対象範囲のファイルが作成され、コンパイルエラーが無い | 満たす |
| `public` メンバに英語の XML コメント | 満たす（`TryGetInt64` は `internal` だが、既存の記述水準に合わせて XML コメントを付与） |
| ログが shape / count / flag のみ | 満たす（`ClipboardJsonReader` は元からログを出さない。逸脱宣言も維持） |
| 逸脱の宣言が既存の使い分けに従っている | 満たす（クラス `<summary>` 内の `<para>`） |
| 対象範囲の新規テストが pass | 該当なし（新規テストなし） |
| **既存テストが全件 pass** | **満たす（EditMode 388 / PlayMode 44、失敗 0）** |
| EditMode テストが Manager インスタンスを生成していない | 満たす（変更したテストは JSON リーダーの純粋関数のみ） |
| PlayMode テストが `[TearDown]` で `ResetForTests()` を呼ぶ | 該当なし（PlayMode テストを変更していない） |
| `.meta` を新規作成せず、改名は `git mv` で対に移動 | 満たす（GUID 保持を確認） |
| 要検証事項の更新 | V-1 は計画書 0.2 でクローズ済み。段 1 で新たに判明した要検証事項は無い |

## 7. 次のステップ

**段 2（型・定数・Builder・Parser）に着手可能。**

計画書 v9 時点で、段 2 に掛かる未解決の A1 は無い（Codex レビュー v7 で A1-2 / A1-3 は反映済みと判定）。

**段 4 の着手前には、監視部分（5.6.5 / 5.6.8 の監視項 / 監視シーム / 7.2 の監視テスト）に絞った再レビューが必要。** v5 → v6 → v7 と 3 ラウンド連続で監視領域から A1 が出ており、v8 の D-16（teardown 競合の再発行規則）と v9 の段の切り直しは、いずれもまだレビューを通っていない。
