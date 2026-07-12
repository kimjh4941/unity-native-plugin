# レビュー結果

- 日付: 2026-07-12
- 対象ファイル: artifact/designs/share/2026-07-12-macos-share-design-v3.md
- 機能名: share
- プラットフォーム: macOS

---

## 強み

- 前回レビュー v2 の中優先度 1 件は反映済み。
  - `MacShareServiceNames.Message` が `com.apple.messages.ShareExtension` に修正され、ローカル SDK の `NSSharingService.Name.composeMessage.rawValue` と一致している。
  - `MacShareServiceNames` の各定数を SDK の `NSSharingService.Name.*.rawValue` で確認する方針と、未検証値を定数から除外する方針が明記された。
  - C Bridge に `canPerform` が公開されていないため、C# 側の可否確認は `ShareViaService` 実行時の `serviceUnavailable` で判定する、という制約が追記された。
- v1 / v2 で確認済みの PlayMode テスト追加、`ServiceName` 表示名と raw identifier の区別、native API / JSON スキーマ / エラー仕様 / スレッド・メモリ契約 / IL2CPP 制約は引き続き維持されている。
- `.meta` を手動作成対象として列挙しない方針も維持されている。

## 改善点

### 高優先度

- なし

### 中優先度

- なし

### 低優先度

- なし

## 不足項目

- なし

## 総合評価

前回レビュー v2 の残件は解消済み。ローカル SDK で `NSSharingService.Name.composeEmail.rawValue == "com.apple.share.Mail.compose"`、`NSSharingService.Name.composeMessage.rawValue == "com.apple.messages.ShareExtension"` を確認でき、v3 の `MacShareServiceNames` 方針と整合している。

macOS Share C# 層の実装計画として、native API 確認、C# 呼び出し方針、Manager + Bridge パターン、変更ファイル一覧、エラー仕様、テスト方針、スレッド・メモリ契約、IL2CPP/AOT 制約はいずれも十分。現時点で追加の改善指摘はない。
