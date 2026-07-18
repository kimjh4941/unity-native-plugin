# レビュー結果

- 日付: 2026-07-12
- 対象ファイル: artifact/designs/share/2026-07-12-macos-share-design-v2.md
- 機能名: share
- プラットフォーム: macOS

---

## 強み

- 前回レビュー v1 の低優先度 2 件はどちらも反映済み。
  - `Tests/PlayMode/MacShareManagerIntegrationTests.cs` が変更ファイル一覧とテスト方針に追加され、`IosShareManagerIntegrationTests.cs` に倣う dispatcher flush 経路まで明記された。
  - callback の `ServiceName` が表示名であり、`ShareViaService` 入力の raw `NSSharingService.Name` ではないことが `1.2` / `1.3` / `5.2` / `5.3` に明示された。
- native API、JSON スキーマ、エラー仕様、スレッド・メモリ契約、IL2CPP/AOT 制約は v1 から引き続き具体的で、実装に必要な判断材料が揃っている。
- `.meta` を手動作成対象として列挙しない方針も維持されている。

## 改善点

### 高優先度

- なし

### 中優先度

- セクション: `5.2 MacShareServiceNames（参考定数）`
  - 問題点: 参考定数例の `Message = "com.apple.share.Message.window"` は、ローカル SDK の `NSSharingService.Name.composeMessage.rawValue` と一致しない。`swift` で確認した macOS SDK の値は `com.apple.messages.ShareExtension` だった。計画どおりに public 定数として実装すると、`ShareViaService(MacShareServiceNames.Message, ...)` が `serviceUnavailable` になり、利用者向け API として誤誘導する可能性がある。
  - severity: medium
  - 改善提案: `Message` 定数は `com.apple.messages.ShareExtension` に修正するか、実機検証済みの `MailCompose` のみに絞る。複数定数を提供する場合は、実装前に `NSSharingService.Name.*.rawValue` で値を確認し、少なくとも各定数の `CanPerform` / `ShareViaService` 手動確認を `7.3` または `8` に含める。

### 低優先度

- なし

## 不足項目

- `MacShareServiceNames.Message` の raw identifier 修正、または未検証定数を計画から外す判断。

## 総合評価

前回レビュー v1 の指摘は解消されており、macOS Share C# 層の計画としては引き続き高い完成度。PlayMode テストの実装対象化と `ServiceName` の意味の明確化により、v1 より実装者が迷いにくい内容になっている。

ただし v2 で追加された `MacShareServiceNames` の `Message` 例は、ローカル SDK で確認できる raw 値と不一致だった。これは新規追加 API の使い勝手に直結するため、実装前に定数値を修正すればレビュー上の残件はなくなる。
