# サンプルシーン設計計画

- 日付: 2026-06-06
- 機能名: notification
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-4
- 実装結果ファイル: artifact/results/notification/2026-06-06-windows-notification-implementation-feature-result-v1.md
- レビュー: artifact/reviews/notification/2026-06-06-windows-notification-sample-scene-design-review.md（v1 → v2 改善点を反映）

---

## 1. 既存サンプルコードの深掘り結果

### 再利用する既存コンポーネント

- `NativeToolkitSampleNavigator.cs` — `ShowWindowsNotification` メソッドを追加してルーティングを提供する
- `TopMenuExampleController.cs` — `OnNotificationClicked` に `UNITY_STANDALONE_WIN` ルートを追加する。**加えて購読ガード（`#if` の `_notificationButton.clicked +=` 周辺）に `|| UNITY_STANDALONE_WIN` を追加し、Editor フォールバック文言を「Android, iOS, macOS, or Windows」に更新する**
- macOS Notification UXML の構造（header 固定 + ScrollView + セクション分割）をベースにする
- Windows Dialog USS の color / font-size / border-radius パターン（Windows Fluent Design: `#0078D4`, font-size 24px, border-radius 4px）を踏襲する

### 追加するコンポーネント

- `WindowsNotificationManagerExampleController.cs` — 新規
- `WindowsNotificationManagerExample.uxml` — 新規
- `WindowsNotificationManagerExampleStyle.uss` — 新規

### 変更するファイルと理由

| ファイル | 変更理由 |
|---------|---------|
| `NativeToolkitSampleNavigator.cs` | `ShowWindowsNotification` の追加、`RemoveExistingControllers` に `WindowsNotificationManagerExampleController` を追加 |
| `TopMenuExampleController.cs` | 購読ガードに `UNITY_STANDALONE_WIN` を追加、`OnNotificationClicked` に `#elif UNITY_STANDALONE_WIN` ルートを追加、Editor フォールバック文言に Windows を追記 |

---

## 2. 画面要件

### 2.1 機能一覧（セクション構成）

| セクション | 操作 | Manager API |
|-----------|------|------------|
| Initialize | isPackaged(toggle) / clsid / launchUri 入力 → InitializeButton | `Initialize(isPackaged, clsid?, launchUri?)` |
| Show | ShowNotification（固定サンプルペイロード） / ScheduleNotification（+30秒後） | `ShowNotification(jsonPayload)` / `ScheduleNotification(jsonPayload, scheduledTimeUnixMs)` |
| Update Progress | UpdateNotificationProgress（SampleTag/SampleGroup, progress 0.5 → 1.0） | `UpdateNotificationProgress(tag, group, value, valueStr, status, sequenceNumber)` |
| Cancel Scheduled | CancelScheduledNotification（SampleTag/SampleGroup） | `CancelScheduledNotification(tag, group)` |
| Remove | RemoveNotificationsByTag（SampleTag/SampleGroup） / RemoveNotificationById（固定 ID=1） / RemoveAllNotifications | `RemoveNotificationsByTag(tag, group)` / `RemoveNotificationById(uint)` / `RemoveAllNotifications()` |
| Query | GetAllNotifications / GetNotificationSetting / OpenNotificationSettings | `GetAllNotifications(onResult)` / `GetNotificationSetting()` / `OpenNotificationSettings()` |
| Badge | SetBadge(Alert) / SetBadge(NewMessage) / SetBadge(1) / ClearBadge | `SetBadge(int)` |

### 2.2 操作導線

```
HomeButton → TopMenu

[Initialize セクション]
isPackaged toggle + clsid(TextField) + launchUri(TextField)
→ InitializeButton → SetResult("✓ Initialize" or "✗ Initialize\n{errorMessage}")
  ※ clsid が空文字かつ isPackaged=false の場合 SetResult("clsid is required when not packaged.") で早期リターン

[Show セクション]
ShowNotificationButton
  → payload に Tag=SampleTag, Group=SampleGroup を含めて BuildNotificationPayload → ShowNotification(json)
  → NotificationOperationCompleted event → SetResult

ScheduleNotificationButton
  → 同ペイロード + scheduledTimeUnixMs = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds()
  → ScheduleNotification(json, scheduledTimeUnixMs)
  → NotificationOperationCompleted event → SetResult

[Update Progress セクション]
UpdateProgressButton
  → UpdateNotificationProgress(SampleTag, SampleGroup, 0.5, "50%", "Downloading...", ++_sequenceNumber)
  → NotificationOperationCompleted event → SetResult
  ※ _sequenceNumber はコントローラフィールドとして保持（単調増加保証）

[Cancel Scheduled セクション]
CancelScheduledButton
  → CancelScheduledNotification(SampleTag, SampleGroup)
  → NotificationOperationCompleted event → SetResult

[Remove セクション]
RemoveByTagButton → RemoveNotificationsByTag(SampleTag, SampleGroup) → SetResult
RemoveByIdButton  → RemoveNotificationById(1u) → SetResult
RemoveAllButton   → RemoveAllNotifications() → SetResult

[Query セクション]
GetAllButton
  → GetAllNotifications((json, result) => SetResult(result.IsSuccess ? $"GetAll:\n{json}" : FormatError(result)))
  ※ GetAllNotificationsCompleted event からも同内容を受信できる

GetSettingButton
  → WindowsNotificationSetting setting = GetNotificationSetting()  // 同期返却・event なし
  → SetResult($"NotificationSetting: {setting}")

OpenSettingsButton → OpenNotificationSettings() → NotificationOperationCompleted event → SetResult

[Badge セクション]
SetBadgeAlertButton      → SetBadge((int)WindowsBadgeValue.Alert)      → SetResult
SetBadgeNewMessageButton → SetBadge((int)WindowsBadgeValue.NewMessage)  → SetResult
SetBadge1Button          → SetBadge(1)                                  → SetResult（数値バッジ）
ClearBadgeButton         → SetBadge((int)WindowsBadgeValue.Clear)       → SetResult
  ※ Clear = 0。独立した ClearBadge API は存在しない
```

### 2.3 エラー表示

- 全操作の結果を `ResultTextBlock` Label に表示する
- 成功: `"✓ {OperationName}"` または付加情報あり（JSON など）
- 失敗: `"✗ {OperationName}\nError: {result.ErrorMessage}"`
- Editor 表示: `"Windows Standalone only. Run this sample on Windows to verify."`（per-operation）

---

## 3. 変更ファイル一覧

### 3.1 新規作成

| ファイル | 内容 |
|---------|------|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Windows/Notification/WindowsNotificationManagerExampleController.cs` | MonoBehaviour サンプルコントローラ |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Windows/Notification/WindowsNotificationManagerExample.uxml` | UI レイアウト |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Windows/Notification/WindowsNotificationManagerExampleStyle.uss` | スタイル |

### 3.2 既存変更

| ファイル | 変更内容 |
|---------|---------|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowWindowsNotification` 追加、`RemoveExistingControllers` に `WindowsNotificationManagerExampleController` を追加 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs` | 購読ガードに `UNITY_STANDALONE_WIN` 追加、`OnNotificationClicked` に `#elif UNITY_STANDALONE_WIN` ルート追加、Editor フォールバック文言更新 |

### 3.3 非変更

| ファイル | 理由 |
|---------|------|
| `WindowsNotificationManager.cs` | 変更不要（実装済み） |
| `WindowsNotificationJsonBuilder.cs` | 変更不要（サンプルペイロードはコントローラ側で構築）|
| `WindowsNotificationPayloads.cs` | 変更不要（Tag / Group フィールドは既に定義済み）|

---

## 4. 実装方針

### 4.1 共通実装パターンの維持と拡張

| パターン | 維持 / 拡張 |
|---------|-----------|
| TopMenu → ExampleController 導線 | 維持（Navigator に ShowWindowsNotification 追加 + TopMenu ガード修正）|
| タイトル + ResultTextBlock ヘッダー固定 | 維持（macOS Notification と同構造）|
| セクション単位のボタン群 | 維持（Initialize / Show / Update Progress / Cancel Scheduled / Remove / Query / Badge）|
| `UnityMainThreadDispatcher` 経由での UI 更新 | 維持（Manager events はメインスレッド保証済み） |
| `OnEnable` / `OnDisable` でのイベント購読管理 | 維持（`NotificationOperationCompleted`, `NotificationInvoked`, `GetAllNotificationsCompleted` を購読）|
| 全ハンドラ先頭 `Debug.Log` | 維持（csharp.md ルール準拠）|
| Editor 上のフォールバック表示 | 拡張 — `Awake` で Editor ダイアログ表示（WindowsDialog と同パターン）。個別ハンドラでは `#else SetResult("Windows Standalone only...")` |

### 4.2 Initialize 入力の状態管理

- `isPackaged` は `Toggle` で管理（デフォルト: false）
- `clsid` / `launchUri` は `TextField` で管理
- サンプルデフォルト値: `clsid = "{00000000-0000-0000-0000-000000000000}"`, `launchUri = "myapp://"`
- バリデーション: `isPackaged=false` かつ `clsid` が空文字 → `SetResult("clsid is required when not packaged.")` で早期リターン

### 4.3 SampleTag / SampleGroup の一貫管理

- `SampleNotificationTag = "win-sample-notification"` — ShowNotification / UpdateProgress / CancelScheduled / RemoveByTag で共通使用
- `SampleNotificationGroup = "win-sample-group"` — 同上。group が必要な API すべてで同値を使用
- ShowNotification ペイロードに必ず `Tag = SampleNotificationTag`, `Group = SampleNotificationGroup` を設定する

### 4.4 UpdateNotificationProgress の sequenceNumber 管理

- コントローラに `private uint _sequenceNumber;` フィールドを保持
- UpdateProgress ボタン押下ごとに `++_sequenceNumber` でインクリメント（単調増加保証）

### 4.5 NotificationInvoked イベントの扱い

- `OnEnable` でグローバル event として購読（`#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`）
- 受信時は `SetResult($"NotificationInvoked: {argsJson}")` で ResultTextBlock に表示する

---

## 5. 実装詳細

### 5.1 ExampleController クラス設計

```
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
public class WindowsNotificationManagerExampleController : MonoBehaviour
{
    private const string LogTag = "WindowsNotificationManagerExampleController";
    private const string SampleNotificationTag   = "win-sample-notification";
    private const string SampleNotificationGroup = "win-sample-group";

    private uint _sequenceNumber;

    // UI refs
    private Label?     _resultLabel;
    private Button?    _homeButton;
    private Toggle?    _isPackagedToggle;
    private TextField? _clsidField;
    private TextField? _launchUriField;
    private Button?    _initializeButton;
    private Button?    _showNotificationButton;
    private Button?    _scheduleNotificationButton;
    private Button?    _updateProgressButton;
    private Button?    _cancelScheduledButton;
    private Button?    _removeByTagButton;
    private Button?    _removeByIdButton;
    private Button?    _removeAllButton;
    private Button?    _getAllButton;
    private Button?    _getSettingButton;
    private Button?    _openSettingsButton;
    private Button?    _setBadgeAlertButton;
    private Button?    _setBadgeNewMessageButton;
    private Button?    _setBadge1Button;
    private Button?    _clearBadgeButton;

    // Awake: Editor ダイアログ表示（WindowsDialog と同パターン）
    // Start: InitializeUI()
    // OnEnable: イベント購読 (#if UNITY_STANDALONE_WIN && !UNITY_EDITOR)
    // OnDisable: イベント解除
    // OnDestroy: ボタン clicked -= アンバインド（全 Button フィールド）
}
#endif
```

### 5.2 Manager イベント購読方針

```csharp
private void OnEnable()
{
    Debug.Log($"[{LogTag}][{nameof(OnEnable)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    WindowsNotificationManager.Instance.NotificationOperationCompleted += OnNotificationOperationCompleted;
    WindowsNotificationManager.Instance.NotificationInvoked            += OnNotificationInvoked;
    WindowsNotificationManager.Instance.GetAllNotificationsCompleted   += OnGetAllNotificationsCompleted;
#endif
}
```

### 5.3 サンプルペイロード

```csharp
// ShowNotification / ScheduleNotification 共通ペイロード
// Tag / Group を必ず設定して UpdateProgress / CancelScheduled / Remove と共有する
var payload = new WindowsNotificationPayload
{
    Title  = "Energy Refilled",
    Body   = "Your squad is fully rested. Jump back in and clear the next raid.",
    Tag    = SampleNotificationTag,
    Group  = SampleNotificationGroup,
    Buttons = new List<WindowsNotificationButtonPayload>
    {
        new() { Label = "Open", Args = new Dictionary<string, string> { ["action"] = "open" } }
    }
};
var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);

// ShowNotification
WindowsNotificationManager.Instance.ShowNotification(json);

// ScheduleNotification（+30秒後）
long scheduledTimeUnixMs = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds();
WindowsNotificationManager.Instance.ScheduleNotification(json, scheduledTimeUnixMs);

// UpdateNotificationProgress
WindowsNotificationManager.Instance.UpdateNotificationProgress(
    SampleNotificationTag, SampleNotificationGroup,
    0.5, "50%", "Downloading...", ++_sequenceNumber);

// RemoveNotificationsByTag
WindowsNotificationManager.Instance.RemoveNotificationsByTag(SampleNotificationTag, SampleNotificationGroup);

// Badge（キャストパターン）
WindowsNotificationManager.Instance.SetBadge((int)WindowsBadgeValue.Alert);
WindowsNotificationManager.Instance.SetBadge((int)WindowsBadgeValue.NewMessage);
WindowsNotificationManager.Instance.SetBadge(1);                          // 数値バッジ
WindowsNotificationManager.Instance.SetBadge((int)WindowsBadgeValue.Clear); // ClearBadge 相当
```

### 5.4 GetAllNotifications / GetNotificationSetting のハンドリング

```csharp
// GetAllNotifications — (json, result) 2引数コールバック。event と per-call 両方を受信できる
private void OnGetAllClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnGetAllClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    WindowsNotificationManager.Instance.GetAllNotifications((json, result) =>
    {
        SetResult(result.IsSuccess
            ? $"✓ GetAllNotifications:\n{json}"
            : $"✗ GetAllNotifications\nError: {result.ErrorMessage}");
    });
#else
    SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
}

// GetNotificationSetting — 同期返却・NotificationOperationCompleted を発火しない特例 API
private void OnGetSettingClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnGetSettingClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    WindowsNotificationSetting setting = WindowsNotificationManager.Instance.GetNotificationSetting();
    SetResult($"✓ NotificationSetting: {setting}");
#else
    SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
}
```

### 5.5 入力バリデーション方針

- `Initialize` ボタン押下時、`isPackaged=false` かつ `clsid` が空文字 → `SetResult("clsid is required when not packaged.")` で早期リターン
- `launchUri` は空文字可（null として渡す）
- その他の操作は UI 上でのバリデーションなし（Manager / DLL 側のエラーを resultLabel に表示）

### 5.6 UXML 構造方針

- macOS Notification と同じ `header 固定 + ScrollView` 構造を採用
- Windows Fluent Design カラー: primary `#0078D4`, secondary `#636366`, background `#F3F3F3`
- font-size: 24px（Windows Dialog と統一）
- Initialize セクションに `Toggle` + `TextField` x2（clsid, launchUri）を配置
- セクション: Initialize / Show / Update Progress / Cancel Scheduled / Remove / Query / Badge

### 5.7 USS クラス命名

macOS の `mac-notif-*` に対して `win-notif-*` 系を新設する。Windows Dialog の `win-dialog-*` と統一感を持たせる。

```
win-notif-root, win-notif-header, win-notif-title, win-notif-subtitle
win-notif-result-border, win-notif-result-text
win-notif-scroll, win-notif-content
win-notif-section, win-notif-section-title
win-notif-button (primary), win-notif-secondary-button
win-notif-input-row, win-notif-label, win-notif-textfield
```

---

## 6. 手動確認観点

| 確認内容 | 方法 |
|---------|------|
| Initialize が成功すること | Windows 実機、clsid / launchUri を入力して実行 |
| 通知バナーがアクションセンターに届くこと | Windows アクションセンターで確認 |
| 通知バナーをクリックすると `NotificationInvoked` が `ResultTextBlock` に届くこと | バナークリック後の ResultTextBlock を確認 |
| ScheduleNotification が 30 秒後に通知を配信すること | 実行後 30 秒待ちアクションセンターで確認 |
| CancelScheduledNotification が配信前通知をキャンセルすること | Schedule 直後に Cancel → 30 秒後に通知が来ないことを確認 |
| UpdateNotificationProgress が通知の進捗バーを更新すること | 通知センターで進捗バーの変化を確認 |
| RemoveNotificationsByTag が指定 tag/group の通知を削除すること | 通知センターから消去されることを確認 |
| RemoveNotificationById が指定 ID の通知を削除すること | 通知センターから消去されることを確認 |
| RemoveAllNotifications が全通知を削除すること | 通知センター上で全消去を確認 |
| GetAllNotifications が JSON 配列を ResultTextBlock に表示すること | 結果が `[` 始まりの JSON であることを確認 |
| GetNotificationSetting が `Enabled` / `DisabledForApplication` 等を返すこと | 設定変更後に確認 |
| SetBadge(Alert) がタスクバーアイコンにグリフバッジを表示すること | タスクバーのバッジを確認 |
| SetBadge(1) が数値バッジを表示すること | タスクバーのバッジを確認 |
| ClearBadge（SetBadge(0)）でバッジが消えること | タスクバーのバッジ消去を確認 |
| Editor 上でボタンを押すと "Windows Standalone only." が表示されること | Editor で動作確認 |
| TopMenu の Notification ボタンが Windows スタンドアロンで WindowsNotification 画面に遷移すること | Windows スタンドアロンビルドで確認 |
