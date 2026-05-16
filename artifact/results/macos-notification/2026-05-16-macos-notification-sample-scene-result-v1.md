# サンプルシーン実装結果

## 基本情報

- 日付: 2026-05-16
- 機能名: macos-notification
- 対象プラットフォーム: macOS Standalone
- ブランチ: feature/UNT-3
- 参照計画: artifact/plans/macos-notification/2026-05-16-macos-notification-sample-scene-plan-v2.md

---

## 変更ファイル

### 新規作成

| ファイルパス | 内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Notification/MacNotificationManagerExampleController.cs` | macOS 通知サンプル ExampleController（22 ボタン / OnEnable/OnDisable ライフサイクル） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/macOS/Notification/MacNotificationManagerExample.uxml` | UI Toolkit レイアウト（7 セクション・22 ボタン） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/macOS/Notification/MacNotificationManagerExampleStyle.uss` | USS スタイル（macOS システムカラー準拠） |

### 既存変更

| ファイルパス | 変更内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowMacNotification()` 追加。`RemoveExistingControllers` に `MacNotificationManagerExampleController` 追加 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs` | Notification ボタン表示条件に `UNITY_STANDALONE_OSX` 追加。`OnNotificationClicked` に `#elif UNITY_STANDALONE_OSX` 分岐追加。Editor ダイアログメッセージに macOS 追記 |

---

## 実装したサンプル機能

### 計画からの追加判断

- namespace は使用しない（既存 ExampleController 群が全て namespace なしのため一貫性を優先）
- `SampleScheduledId` は削除（macOS `ScheduleNotification` は identifier を持たないため不要。`SampleNotificationId` で統一）

### Permission セクション（4 ボタン）

| ボタン | 実装 |
|---|---|
| RequestPermission | `MacNotificationManager.Instance.RequestPermission(callback)` |
| HasPermission | `MacNotificationManager.Instance.HasPermission(callback)` |
| AuthorizationStatus | `MacNotificationManager.Instance.GetAuthorizationStatus(callback)` + `MacNotificationAuthorizationStatusParser.ParseJson(result.Json)` |
| OpenSettings | `MacNotificationManager.Instance.OpenSettings(callback)` |

### Show Notification セクション（3 ボタン）

| ボタン | 通知内容 | トリガー |
|---|---|---|
| ShowImmediate | "Energy Refilled" (categoryIdentifier: SampleCategoryId) | なし（即時） |
| ShowTimeInterval(5s) | "Guild Battle Countdown" | TimeInterval 5秒 |
| ShowCalendar(+1m) | "Daily Reward Ready" | Calendar +1分 |

### Update / Cancel / Remove セクション（5 ボタン）

| ボタン | 実装 |
|---|---|
| UpdateById | `UpdateNotification(SampleNotificationId, contentJson, null, callback)` / 内容: "Town Entry Bonus" |
| CancelById | `CancelNotification(SampleNotificationId)` (fire-and-forget) |
| CancelAll | `CancelAllNotifications()` (fire-and-forget) |
| RemoveDeliveredById | `RemoveDeliveredNotification(SampleNotificationId)` (fire-and-forget) |
| RemoveAllDelivered | `RemoveAllDeliveredNotifications()` (fire-and-forget) |

### Scheduled Notification セクション（4 ボタン）

| ボタン | 通知内容 | トリガー |
|---|---|---|
| ScheduleTimeInterval(10s) | "Guild Battle Starts Soon" | TimeInterval 10秒 |
| ScheduleCalendar(+1m) | "Daily Reward Window" | Calendar +1分 |
| CancelScheduledById | `CancelScheduledNotification(SampleNotificationId)` (fire-and-forget) | - |
| CancelAllScheduled | `CancelAllScheduledNotifications()` (fire-and-forget) | - |

### Query セクション（2 ボタン）

- GetScheduled: `GetScheduledNotifications(result => SetResult($"GetScheduled:\n{result.Json}"))` 
- GetDelivered: `GetDeliveredNotifications(result => SetResult($"GetDelivered:\n{result.Json}"))`

### Badge セクション（2 ボタン）

- SetBadgeCount(1) / SetBadgeCount(0): `SetBadgeCount(count, callback)`

### Category セクション（2 ボタン）

- RegisterCategory: actions = Open(isForeground) / Delete / Reply(isTextInput, placeholder: "Type a message")
- RemoveCategory: `RemoveCategory(SampleCategoryId, callback)` (macOS は callback あり)

---

## 設計上の選択（計画との差分）

| 項目 | 計画 | 実装 | 理由 |
|---|---|---|---|
| namespace | `JonghyunKim.NativeToolkit.Runtime.UI` | なし | 既存 ExampleController 群（iOS・Android・macOS Dialog）が全て namespace なしのため一貫性を優先 |
| SampleScheduledId | `"mac-scheduled-notification"` | `SampleNotificationId` で代替 | macOS `ScheduleNotification` は identifier パラメータを持たないため別定数は不要 |
| `NotificationOperationCompleted` | 購読しない | 購読しない | H-3 対応済み。per-call callback のみで全結果を表示 |

---

## ビルド / 実行結果

- Unity Editor でのコンパイル: 未確認（Unity Editor 実行環境なし）
- `#if UNITY_STANDALONE_OSX || UNITY_EDITOR` ガードにより Editor でもコンパイル対象
- API シグネチャはすべて MacNotificationManager の public API と照合済み
- フィールド名（IsSuccess / ErrorMessage / Json / NotificationId / ActionId / UserText）は結果型と照合済み

---

## 手動確認観点 / 未実施項目

### 実施可能な手動確認観点（macOS Standalone ビルド必須）

| 観点 | 手順 | 期待結果 |
|---|---|---|
| TopMenu → macOS Notification 導線 | macOS Standalone でシーン起動 → Notification ボタンタップ | macOS Notification 画面へ遷移 |
| Back To Home | macOS Notification 画面 → "Back To Home" タップ | Top Menu へ戻る |
| RequestPermission | RequestPermission ボタンタップ | システム権限ダイアログ表示・結果が ResultTextBlock に表示 |
| HasPermission | HasPermission ボタンタップ | `HasPermission: True/False` が表示 |
| AuthorizationStatus | AuthorizationStatus ボタンタップ | `AuthorizationStatus: Authorized` 等 enum 名が表示 |
| OpenSettings | OpenSettings ボタンタップ | システム通知設定画面が開く・結果表示 |
| ShowImmediate | ShowImmediate タップ | "Energy Refilled" 通知が即時表示される |
| ShowTimeInterval | ShowTimeInterval(5s) タップ | "Guild Battle Countdown" 通知が5秒後に表示される |
| ShowCalendar | ShowCalendar(+1m) タップ | "Daily Reward Ready" 通知が1分後に表示される |
| UpdateById | ShowImmediate 後 → UpdateById タップ | 通知が "Town Entry Bonus" 内容に更新される |
| CancelById | 通知スケジュール後 → CancelById タップ | 通知がキャンセルされる |
| ScheduleTimeInterval | ScheduleTimeInterval(10s) タップ | "Guild Battle Starts Soon" 通知が10秒後に表示される |
| ScheduleCalendar | ScheduleCalendar(+1m) タップ | "Daily Reward Window" 通知が1分後に表示される |
| GetScheduled | GetScheduled タップ | スケジュール済み通知の JSON が表示される |
| GetDelivered | GetDelivered タップ | 配信済み通知の JSON が表示される |
| SetBadgeCount(1) | SetBadgeCount(1) タップ | アプリアイコンにバッジ "1" が表示される |
| SetBadgeCount(0) | SetBadgeCount(0) タップ | バッジが消える |
| RegisterCategory | RegisterCategory タップ → ShowImmediate タップ | 通知に "Open" / "Delete" / "Reply" アクションボタンが表示される |
| RemoveCategory | RegisterCategory 後 → RemoveCategory タップ | 結果が表示される |
| NotificationActionReceived | "Open" または "Delete" をタップ | ResultTextBlock に actionId が表示される |
| NotificationTextInputActionReceived | "Reply" でメッセージを入力して送信 | ResultTextBlock に userText が表示される |
| Editor 実行 | Unity Editor でボタンをタップ | `"macOS Standalone only..."` が表示される |
| 権限拒否時の操作 | HasPermission=False の状態で ShowImmediate タップ | `"ShowImmediate: Please allow notification permission first."` が表示される |

### 未実施項目と理由

| 項目 | 理由 |
|---|---|
| Unity Editor コンパイル確認 | Unity Editor 実行環境なし。API シグネチャ照合で代替 |
| macOS Standalone 実機確認（全手動観点） | macOS Standalone ビルド環境なし。実機確認はユーザーに委任 |
