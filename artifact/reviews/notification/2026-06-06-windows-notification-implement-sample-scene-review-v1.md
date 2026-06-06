# レビュー結果レポート

## 基本情報

- 日付: 2026-06-06
- ブランチ: feature/UNT-4
- サンプルシーン計画: artifact/designs/notification/2026-06-06-windows-notification-sample-scene-design-v2.md
- 実装結果: artifact/results/notification/2026-06-06-windows-notification-implement-sample-scene-result-v1.md
- 対象プラットフォーム: Windows

---

## レビュー概要

Windows 通知サンプルシーンの実装（3 新規ファイル + 2 既存変更）を対象にレビューした。
設計 v2 の仕様に沿い、16 ボタン・3 Manager イベント購読・7 UXML セクションを実装している。
コンパイルガード・ライフサイクル・ナビゲーション統合はすべて正確に実装されている。
1 件の medium 問題（グローバルイベントハンドラによる per-call 表示の上書き）が確認された。

---

## 重大な問題（high）

なし

---

## 改善提案（medium）

### [M-1] OnNotificationOperationCompleted が per-call コールバックの表示結果を上書きする

**ファイル**: `WindowsNotificationManagerExampleController.cs:395-399`

**内容**:
- Manager の `FireResult` は per-call コールバックを呼び出した「直後」に `NotificationOperationCompleted` イベントも同期的に発火する
- コントローラは 12 操作（Initialize, ShowNotification, ScheduleNotification, UpdateProgress, CancelScheduled, RemoveByTag, RemoveById, RemoveAll, SetBadge×3, OpenSettings）でいずれも per-call コールバックを利用している
- グローバルイベントハンドラ `OnNotificationOperationCompleted` が後から `SetResult` を呼び出すため、per-call で設定したラベルが常に上書きされる
- 結果として `ResultTextBlock` には raw 操作名（"scheduleNotification", "updateNotificationProgress", "setBadge" 等）が表示され、per-call で付加したコンテキスト（"+30s"、"seq=1"、"Alert" 等）は表示されない

**修正案**:
`OnNotificationOperationCompleted` の `SetResult` 呼び出しを削除し、ログのみに変更する。
すべてのボタン操作は per-call コールバックで表示を担保しているため、グローバルハンドラは UI 更新不要。

```csharp
// Before
private void OnNotificationOperationCompleted(WindowsNotificationResult result)
{
    Debug.Log($"[{LogTag}][{nameof(OnNotificationOperationCompleted)}] operation: {result.Operation}, isSuccess: {result.IsSuccess}");
    SetResult(FormatResult(result.Operation, result));  // ← 削除
}

// After
private void OnNotificationOperationCompleted(WindowsNotificationResult result)
{
    Debug.Log($"[{LogTag}][{nameof(OnNotificationOperationCompleted)}] operation: {result.Operation}, isSuccess: {result.IsSuccess}");
}
```

---

## 軽微な指摘（low）

### [L-1] win-notif-toggle に hover/focus スタイルが未定義

**ファイル**: `WindowsNotificationManagerExampleStyle.uss:148-151`

- `win-notif-button` / `win-notif-secondary-button` には `:hover` / `:active` / `:disabled` スタイルが定義されているが、`win-notif-toggle` には定義がない
- Toggle の視覚フィードバックが他コントロールと統一されていない
- 機能上の影響はないが、UI 一貫性として定義を追加することを推奨する

---

## 計画整合性チェック

| 観点 | 結果 | 備考 |
|------|------|------|
| ボタン一覧の実装網羅性 | ○ | 計画の 16 ボタンすべて実装済み |
| UXML name と Controller の一致 | ○ | Q<Button>("name") がすべて UXML name と一致 |
| API 呼び出し仕様の一致 | ○ | Manager の実際のシグネチャと完全一致。per-call コールバックは Manager が対応済みの任意引数として正しく使用 |
| 変更ファイル一覧との一致 | ○ | 計画記載の 3 新規 + 2 変更と一致 |

---

## プロジェクトルール適合チェック

| 観点 | 結果 | 備考 |
|------|------|------|
| common.md 準拠 | ○ | Manager + ExampleController 依存構造を維持。UI 更新は Manager events 経由 |
| csharp.md 準拠 | ○ | 全ライフサイクルメソッド・ハンドラ先頭に Debug.Log あり。public クラスに XML doc コメントあり |
| ライフサイクル管理（登録・解除の対称性） | ○ | InitializeUI で += / OnDestroy で -= が 16 ボタン分すべて対称。OnEnable/OnDisable で Manager イベント 3 つが対称 |
| コンパイルガードの網羅性 | ○ | クラス全体: `#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`。ハンドラ内: `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` + `#else` フォールバック全 16 ハンドラに実装済み |
| 権限ガードの網羅性 | N/A | Windows は操作前の明示的 Permission Guard パターン不要（GetNotificationSetting で確認） |
| ナビゲーション統合 | ○ | ShowWindowsNotification 追加・RemoveExistingControllers に追加・TopMenu のガードと遷移分岐が正確 |
| 既存 API 互換性 | ○ | Navigator / TopMenu の既存メソッド・動作に破壊的変更なし |

---

## 総合評価

**LGTM**

M-1 修正済み（`OnNotificationOperationCompleted` の `SetResult` 削除 — 2026-06-06）。
L-1 はオプション対応。
