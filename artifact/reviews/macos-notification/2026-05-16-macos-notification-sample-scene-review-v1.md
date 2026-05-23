# サンプルシーンレビュー結果

- 日付: 2026-05-16
- ブランチ: feature/UNT-3
- 対象: git diff main...HEAD（サンプルシーン関連ファイル）
- プラットフォーム: macOS Standalone
- サンプルシーン計画: artifact/plans/macos-notification/2026-05-16-macos-notification-sample-scene-plan-v2.md
- サンプルシーン実装結果: artifact/results/macos-notification/2026-05-16-macos-notification-sample-scene-result-v1.md

---

## レビュー概要

ブランチ `feature/UNT-3` の macOS 通知サンプルシーン実装レビュー。変更対象は `MacNotificationManagerExampleController.cs` / `MacNotificationManagerExample.uxml` / `MacNotificationManagerExampleStyle.uss`（新規）と `NativeToolkitSampleNavigator.cs` / `TopMenuExampleController.cs`（既存変更）。

---

## 重大な問題（high）

なし

---

## 改善提案（medium）

**M-1: `sound` フィールドが JSON にシリアライズされない**

- 該当箇所: `MacNotificationManagerExampleController.cs` の `sound = "default"` 設定箇所（L249, L275, L301, L337, L419, L445）
- `NotificationContentPayload.sound` を設定しているが、`MacNotificationJsonBuilder.BuildContentJson` はこのフィールドをシリアライズしない（`id` / `title` / `subtitle` / `body` / `badge` / `categoryIdentifier` のみ出力）
- macOS ネイティブ側がデフォルト音を自動適用するなら無害だが、意図した音設定がサイレントに無視されている
- 対処案:
  - macOS 側がデフォルト音を自動使用する設計であれば `sound = "default"` の設定を削除する（不要なフィールド設定の除去）
  - 音設定が必要であれば `BuildContentJson` に `sound` のシリアライズを追加する（native 側の期待スキーマ確認が前提）

---

## 軽微な指摘（low）

**L-1: UXML の "Cancel All" 表示ラベルにスペース**

- 該当箇所: `MacNotificationManagerExample.uxml` L34
- `text="Cancel All"` になっているが、計画・実装結果では "CancelAll"（スペースなし）
- iOS 同画面の実装と統一する場合は確認推奨。機能影響なし

**L-2: `OnShowImmediateClicked` と `OnRegisterCategoryClicked` の成功メッセージが直書き**

- 該当箇所: `MacNotificationManagerExampleController.cs` L255-259 / L600-603
- 他のハンドラは `FormatResult(...)` を使うが、この2つは成功時のみカスタムメッセージ（"✓ ShowImmediate\nLong-press..." / "✓ RegisterCategory\nNext..."）
- ユーザー補助としての意図は明確で問題ないが、計画（`FormatResult` 統一）との差分として実装結果に明示推奨

**L-3: `textInputButtonTitle`（"Send"）が Payload に存在しない**

- `MacNotificationActionPayload` に `textInputButtonTitle` フィールドがないため、計画 5.3 で示した `textInputButtonTitle: "Send"` は実装されていない
- フィールド自体が存在しないため実装は正しいが、計画と実装結果のギャップとして記録推奨

---

## 計画整合性チェック

- ボタン一覧の実装網羅性: ○（22ボタン全て実装、UXML・Controller の name 一致）
- UXML name と Controller の一致: ○
- API 呼び出し仕様の一致: △（`sound` フィールドが JSON に未反映 → M-1）
- 変更ファイル一覧との一致: ○（計画記載の5ファイルと一致）

---

## プロジェクトルール適合チェック

- common.md 準拠: ○
- csharp.md 準拠: ○（全メソッドに `Debug.Log` あり、`public class` に XML コメントあり）
- ライフサイクル管理（登録・解除の対称性）: ○（OnEnable/OnDisable でイベント管理、OnDestroy で Button.clicked 解除）
- コンパイルガードの網羅性: ○（クラスレベル `|| UNITY_EDITOR`、ハンドラ内部 `&& !UNITY_EDITOR`）
- 権限ガードの網羅性: ○（18ボタン全てに `ExecuteIfNotificationPermissionGranted`、権限確認系4ボタンはガードなし）
- ナビゲーション統合: ○（`ShowMacNotification` 追加、`RemoveExistingControllers` 追加、`TopMenuExampleController` 分岐追加）
- 既存 API 互換性: ○（破壊的変更なし）

---

## 総合評価

**要修正（軽微）** → 修正済み（LGTM）

M-1（`sound` フィールドの JSON 未反映）は macOS ネイティブ側の動作次第で無害になる可能性があるが、意図的かどうかの確認が必要。確認の結果「macOS はデフォルト音を自動使用するため `sound` 指定不要」であれば `sound = "default"` の設定削除を推奨（不要なフィールド設定の除去）。L-1〜L-3 は機能に影響しない。

---

## 修正対応ログ

### M-1 対応: `sound = "default"` 削除

`MacNotificationJsonBuilder.BuildContentJson` が `sound` フィールドをシリアライズしないため、設定しても JSON に含まれない。macOS はシステムデフォルト音を自動適用する設計のため `sound` 指定は不要と判断し削除した。

- `MacNotificationManagerExampleController.cs`: `sound = "default"` を6箇所削除
  - `OnShowImmediateClicked`（ShowImmediate）
  - `OnShowTimeIntervalClicked`（ShowTimeInterval 5s）
  - `OnShowCalendarClicked`（ShowCalendar +1m）
  - `OnUpdateByIdClicked`（UpdateById）
  - `OnScheduleTimeIntervalClicked`（ScheduleTimeInterval 10s）
  - `OnScheduleCalendarClicked`（ScheduleCalendar +1m）

### L-1 対応: UXML の "Cancel All" → "CancelAll"

- `MacNotificationManagerExample.uxml` L34: `text="Cancel All"` → `text="CancelAll"` に修正

### L-2・L-3: 対応なし

- L-2（`OnShowImmediateClicked` / `OnRegisterCategoryClicked` のカスタム成功メッセージ）: ユーザー補助として有益なため変更しない
- L-3（`textInputButtonTitle` 欠落）: `MacNotificationActionPayload` にフィールドが存在しないため実装は正しい。計画 5.3 の記述が実装の実態と乖離していたが、コード変更は不要
