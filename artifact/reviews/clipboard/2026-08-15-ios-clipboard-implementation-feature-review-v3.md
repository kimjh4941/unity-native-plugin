# iOS Clipboard 実装レビュー v3

## レビュー概要

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- ブランチ: `feature/UNT-9`
- レビュー対象差分: `develop...HEAD` は空のため、ユーザー承認済みのローカル未コミット差分（未追跡ファイルを含む）を対象
- 実装計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 実装結果: `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v3.md`
- 前回レビュー: `artifact/reviews/clipboard/2026-08-15-ios-clipboard-implementation-feature-review-v2.md`
- 総合評価: LGTM

前回の medium 1 件と low 1 件は解消されている。snapshot 専用の strict reader が `typeIdentifiers`、`allTypeIdentifiers` の各行、`matchingItemIndexes` に適用され、不正要素型は E-11 / B-6 となる。追加テストは不正要素、正常配列、snapshot 外の lenient 契約をカバーしている。

active build target は Android に戻され、最新の `NativeToolkit.Runtime.Tests.rsp` の `UNITY_ANDROID` define と Editor 設定でも確認できた。結果 v3 では Android 固有テストを含む EditMode 356 件が全 passed、PlayMode は44 passed・device-only 11 skipped と記録されている。

## 重大な問題（high）

なし。

## 改善提案（medium）

なし。

## 軽微な指摘（low）

### 1. snapshot 内のコメントが lenient reader の実際の適用範囲より狭い（非ブロッキング）

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonParser.cs:143`
- コメントは lenient skipping が `detectPatterns` / `detectValues` に限定されるとしているが、同ファイルの `ReadStringList` は、意図どおり `read` items の `typeIdentifiers` と変更イベントの types にも使用されている。
- helper 側のコメントと結果 v3 の説明は正確で、実装・返却契約・テストに問題はない。将来の保守時の混乱を避けるなら、「snapshot では element-level skipping を行わない」程度の表現へ直すと一貫する。

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: ○
- テスト方針の網羅性: ○
- エラーケース全実装: ○
- 返却仕様との整合: ○

## プロジェクトルール適合チェック

- `common.md` 準拠: ○
- `csharp.md` 準拠: ○
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○
- 既存 API 互換性: ○

## テストカバレッジ

カバー済み:

- JSON reader の不正 escape、raw 制御文字、厳密な number 文法
- envelope E-1〜E-16、base64 padding / 上限 / `byteCount` 境界
- snapshot の必須配列・内側配列・string / int 要素型検証
- snapshot の正常配列と null / empty の区別
- snapshot 外の意図的な lenient list 契約
- production の pending slot / `s_inFlight` を通す single-flight と rejected 不変条件
- observation の共有キー・世代・登録解放
- 全15操作の B-11、late callback 破棄、static reset、dispatcher 破棄判定
- Android target での EditMode 356 passed、PlayMode 44 passed・11 device-only skipped
- Unity iOS export / Xcode link smoke test（M-25、結果 v2 で実施済み）

計画どおり未実施:

- B-2 の実到達
- M-1〜M-24 の iOS 実機確認
- response 側メモリの実機実測と、計画 9.1〜9.6 / 9.9 の要検証事項

今回の変更は managed parser とその EditMode テストに限定され、P/Invoke 宣言、XCFramework、Unity build processor に変更がないため、M-25 を再実行しない判断は妥当である。

本レビューでは Unity Test Runner と Xcode build は再実行していない。結果 v3 のテスト報告を参照し、strict reader の全適用箇所、追加テスト、最新コンパイル応答の Android define、Editor の active target、`git diff --check` を静的に確認した。

## 総合評価

LGTM。

前回までの受け入れ阻害要因はすべて解消されている。残る未実施項目は設計・結果レポートで明示された実機検証のみであり、初期実装の受け入れを妨げない。low のコメント表現は非ブロッキングである。
