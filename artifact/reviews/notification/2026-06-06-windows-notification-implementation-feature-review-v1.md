# レビュー結果

- 日付: 2026-06-06
- ブランチ: feature/UNT-4（未コミット差分）
- 実装計画: artifact/designs/notification/2026-06-06-windows-notification-design-v2.md
- 実装結果: artifact/results/notification/2026-06-06-windows-notification-implementation-feature-result-v1.md
- プラットフォーム: Windows

---

## レビュー概要

Windows Notification 機能の新規実装（4 ファイル + テスト 1 ファイル）のレビュー。
全体的に Manager + Bridge パターン・IL2CPP 対応・エラー契約・コーディングルールへの準拠度は高い。
ただし `GetAllNotifications` の JSON 返却に設計上の欠陥があり、現状では呼び出し元が通知一覧を受け取れない。

## 重大な問題（high）

**[H-1] `GetAllNotifications` の JSON 結果が呼び出し元に届かない**

- ファイル: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/WindowsNotificationManager.cs` L369
- `GetAllNotificationsInternal` の戻り値（`string?` JSON）が破棄されている
- `FireResult` は `WindowsNotificationResult.Success` を発火するが、`Success` は `ErrorMessage = null` 固定
- 結果として `onResult` コールバックを登録しても JSON 配列を受け取る手段がない
- 設計書コメント「ErrorMessage contains the JSON on success」は `Success` ファクトリの契約と矛盾している
- 改善案: `GetAllNotifications` 専用コールバック `Action<string?, WindowsNotificationResult>?` を追加するか、`GetAllNotificationsCompleted` event（`Action<string?, WindowsNotificationResult>`）を別途公開する。Mac の `MacNotificationJsonResult` パターンに準ずるのが最も整合性が高い

## 改善提案（medium）

**[M-1] Editor 上で per-call callback が silent drop される**

- ファイル: `WindowsNotificationManager.cs` L219
- `s_onInitialize = onResult` を設定した後に `Application.platform != WindowsPlayer` で early return すると、`s_onInitialize` はクリアされず `FireResult` も呼ばれない
- Editor 上ではコールバックが一切発火しないため、Editor 確認が困難
- Mac の動作と一致しており設計どおりではあるが、ドキュメントに「Editor 上では callback は発火しない」と明記することを推奨

**[M-2] `buttons.Count == 5` の境界値テストが欠落**

- ファイル: `Tests/Runtime/WindowsNotificationTests.cs`
- `Validate` のテストは「6件→エラー」のみ。「5件→null（正常）」の境界値ケースがない
- `buttons.Count > 5` の条件なので 5 件は通過するが、テストで明示すべき

**[M-3] `GetAllNotificationsInternal` の `string?` 戻り値が常に無視される**

- ファイル: `WindowsNotificationManager.cs` L369
- H-1 の根本原因。`void` にするか呼び出し元で受け取るかのどちらかに統一すべき

## 軽微な指摘（low）

**[L-1] `private bool _initialized = false;` の冗長な初期化**

- ファイル: `WindowsNotificationManager.cs` L174
- `bool` のデフォルト値は `false` なので `= false` は不要

**[L-2] `GetNotificationSetting` の `Enum.IsDefined` リフレクション**

- ファイル: `WindowsNotificationManager.cs` L385
- 呼び出し頻度が低い API なので実用上問題なし。switch 式による明示的マッピングの方が IL2CPP 互換性の観点で確実（要確認）

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: ○
- テスト方針の網羅性: △（境界値欠落・Manager Singleton は Editor 実行待ち）
- エラーケース全実装: ○
- 返却仕様との整合: △（GetAllNotifications の JSON 返却が未実装）

## プロジェクトルール適合チェック

- common.md 準拠: ○
- csharp.md 準拠: ○
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○
- 既存 API 互換性: ○

## テストカバレッジ

- カバーできている観点: WindowsNotificationResult 全フィールド / 全エラーコードマッピング / Validate 3 制約 / Build 正常系・例外系
- 不足している観点: buttons=5 件（境界値）/ GetAllNotifications JSON 返却 / GetNotificationSetting 非 Windows 戻り値 / Manager Singleton（Editor 実行待ち）

## 総合評価

要修正（重大） — H-1（GetAllNotifications の JSON 未返却）を解消しなければ API として機能不全。それ以外は品質が高く、H-1 修正後は LGTM 相当。
