# iOS Clipboard 実装レビュー v2

## レビュー概要

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- ブランチ: `feature/UNT-9`
- レビュー対象差分: `develop...HEAD` は空のため、ユーザー承認済みのローカル未コミット差分（未追跡ファイルを含む）を対象
- 実装計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 実装結果: `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v2.md`
- 前回レビュー: `artifact/reviews/clipboard/2026-08-15-ios-clipboard-implementation-feature-review-v1.md`
- 総合評価: 要修正（軽微）

前回の high 1 件、medium 3 件、low 2 件は解消されている。1.3.0 の両 `.meta` は iOS のみ有効な `PluginImporter` を持ち、結果 v2 には Unity export から Xcode link 成功までの M-25 証跡が記録されている。本レビューでも `UnityIosPlugin` の device binary に15個の `clipboard*` 定義シンボルがあることを確認した。

JSON reader の escape・制御文字・number 検証、Editor-only seam を通した production 状態遷移テスト、main-thread dispatcher の Unity null 判定、公開 factory のログ規則逸脱説明も実装されている。ただし、snapshot の配列要素型に E-11 未適用の箇所が残る。

## 重大な問題（high）

なし。

## 改善提案（medium）

### 1. snapshot の配列内要素の型不一致が依然として成功値へ変換される

- 対象:
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonParser.cs:154`
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonParser.cs:174`
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonParser.cs:188`
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonParser.cs:517`
- v2 は `allTypeIdentifiers` の各行が array か、`matchingItemIndexes` 自体が array かを検証するようになった。一方、共通の `ReadStringList` は非 string 要素を黙って除外し、`matchingItemIndexes` の loop も非 int 要素を黙って除外する。
- そのため、例えば `typeIdentifiers:["public.text",7]`、`allTypeIdentifiers:[[7]]`、`matchingItemIndexes:[0,"x"]` が B-6 にならず、それぞれ一部要素または空要素へ縮退した成功結果になる。
- 設計 5.5.1 は「既定値で成功を合成しない」とし、要素単位スキップを `detectValues` / `detectPatterns` のみに限定している。snapshot は E-11 として全体を失敗させる必要がある。

修正方針:

- snapshot 専用の strict な string / int array reader を用意し、要素が1つでも期待型でなければ `IosClipboardSnapshotResult.Failure(MalformedResponse())` を返す。
- `typeIdentifiers`、`allTypeIdentifiers` の各内側配列、`matchingItemIndexes` について、要素型不一致のテストを追加する。

## 軽微な指摘（low）

### 1. ローカル workspace の active build target が元へ戻されておらず、v2 後の Android テストが未実行

- 対象:
  - `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v2.md:181`
  - `agent-rules/coding-rules/testing.md:200`
  - `agent-rules/coding-rules/common.md:256`
- 結果 v2 は iOS target で EditMode 344 / PlayMode 44 を完走しており、iOS 実装の主要検証としては有効である。また Android production code は変更されておらず、v1 の Android target では Android EditMode 8 件が passed しているため、現時点で Android 回帰を示す証拠はない。
- ただし `testing.md` はローカル / 永続 workspace では検証後に元の target へ戻すことを要求し、`common.md` は修正後に Test Runner の全テスト passed を要求する。v2 の変更は `UNITY_EDITOR` でもコンパイルされる iOS C# を含むため、最終状態としては Android target へ戻した後の確認が残っている。

修正方針:

- 元の Android target へ戻し、少なくとも EditMode を再実行して Android 8 件を含む全テストが passed することを記録する。
- PlayMode も通常の検証コマンドで再実行し、device-only 11 件が既定どおり skip であることを確認する。

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: ○
- テスト方針の網羅性: △（snapshot の要素型不一致、Android target 復元後の再実行が未完）
- エラーケース全実装: △（snapshot 配列要素の E-11 が未適用）
- 返却仕様との整合: △（malformed な snapshot 配列を部分的な成功値へ変換する）

## プロジェクトルール適合チェック

- `common.md` 準拠: △（iOS test は全 passed。target 復元後の既存 Android test 確認が残る）
- `csharp.md` 準拠: ○
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○
- 既存 API 互換性: ○

## テストカバレッジ

カバー済み:

- JSON reader の不正 escape、raw 制御文字、厳密な number 文法
- envelope E-1〜E-16 の主要ケース、base64 padding / 上限 / `byteCount` 境界
- production の pending slot / `s_inFlight` を通す busy rejection と callback 保持
- rejected 経路の状態不変条件、異なる操作の並行、callback からの同一操作再開
- observation の共有キー、開始失敗・停止時の登録解放、世代管理
- 全15操作の B-11、late result / change event 破棄、`ResetCore` の mutable static
- dispatcher 破棄判定
- Unity iOS export / Xcode link（M-25）

不足:

- snapshot の string / int 配列に含まれる不正要素型の B-6 テスト
- v2 変更後、元の Android target に戻した状態での Android EditMode 8 件を含む回帰テスト
- B-2 の実到達、M-1〜M-24 の実機確認（計画どおり未実施）

本レビューでは Unity Test Runner と Xcode build は再実行していない。結果 v2 の EditMode 344 / 344、PlayMode 44 / 44、M-25 `BUILD SUCCEEDED` の報告を参照し、成果物については `.meta` の `PluginImporter` と device binary の15定義シンボルを静的に再確認した。`git diff --check` は問題なし。

## 総合評価

要修正（軽微）。

前回の受け入れ阻害要因だった XCFramework import / link と、single-flight / lifetime の production 経路テストは解消されている。残るコード上の問題は snapshot の配列要素型検証1件で、修正範囲は parser と境界テストに限定できる。あわせて元の Android target へ戻し、ローカル検証状態をプロジェクトルールに沿って閉じる必要がある。
