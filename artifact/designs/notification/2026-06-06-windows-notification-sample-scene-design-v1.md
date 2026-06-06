# サンプルシーン設計計画

- 日付: 2026-06-06
- 機能名: notification
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-4
- 実装結果ファイル: artifact/results/notification/2026-06-06-windows-notification-implementation-feature-result-v1.md

---

## 1. 既存サンプルコードの深掘り結果

### 再利用する既存コンポーネント

- `NativeToolkitSampleNavigator.cs` — `ShowWindowsNotification` メソッドを追加してルーティングを提供する
- `TopMenuExampleController.cs` — `OnNotificationClicked` に `UNITY_STANDALONE_WIN` ルートを追加する
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
| `TopMenuExampleController.cs` | `OnNotificationClicked` の `#elif UNITY_STANDALONE_WIN` ルートを追加（現在 Windows は Editor のみのフォールバックになっている）|

---

## 2. 画面要件

### 2.1 機能一覧（セクション構成）

| セクション | 操作 | Manager API |
|-----------|------|------------|
| Initialize | isPackaged(toggle) / appUserModelId / clsid / launchUri 入力 → InitializeButton | `Initialize(isPackaged, appUserModelId, clsid, launchUri)` |
| Show | ShowNotification（固定サンプルペイロード） / UpdateNotificationProgress（progress 0.5 固定）| `ShowNotification(payload, tag?)` / `UpdateNotificationProgress(tag, progress)` |
| Remove | RemoveNotification（SampleTag 使用） / RemoveAllNotifications | `RemoveNotification(tag)` / `RemoveAllNotifications()` |
| Query | GetAllNotifications / GetNotificationSetting / OpenNotificationSettings | `GetAllNotifications(onResult)` / `GetNotificationSetting()` / `OpenNotificationSettings()` |
| Badge | SetBadge(Alert) / SetBadge(NewMessage) / ClearBadge | `SetBadge(WindowsBadgeValue)` / `ClearBadge()` |
| Focus Assist | SetFocusAssistOn / SetFocusAssistOff | `SetFocusAssist(bool)` |

### 2.2 操作導線

```
HomeButton → TopMenu

[Initialize セクション]
isPackaged toggle + appUserModelId / clsid / launchUri TextField
→ InitializeButton → SetResult("✓ Initialize" or "✗ Initialize\n{errorMessage}")

[Show セクション]
ShowNotificationButton → ShowNotification(samplePayload, SampleNotificationTag)
  → NotificationOperationCompleted event → SetResult
UpdateProgressButton → UpdateNotificationProgress(SampleNotificationTag, sampleProgress)
  → NotificationOperationCompleted event → SetResult

[Remove セクション]
RemoveNotificationButton → RemoveNotification(SampleNotificationTag) → SetResult
RemoveAllButton → RemoveAllNotifications() → SetResult

[Query セクション]
GetAllButton → GetAllNotifications(onResult) → GetAllNotificationsCompleted event → SetResult(JSON)
GetSettingButton → GetNotificationSetting() → SetResult(setting.ToString())
OpenSettingsButton → OpenNotificationSettings() → SetResult

[Badge セクション]
SetBadgeAlertButton → SetBadge(WindowsBadgeValue.Alert) → SetResult
SetBadgeNewMessageButton → SetBadge(WindowsBadgeValue.NewMessage) → SetResult
ClearBadgeButton → ClearBadge() → SetResult

[Focus Assist セクション]
SetFocusAssistOnButton → SetFocusAssist(true) → SetResult
SetFocusAssistOffButton → SetFocusAssist(false) → SetResult
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
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs` | `OnNotificationClicked` に `#elif UNITY_STANDALONE_WIN` ルートを追加 |

### 3.3 非変更

| ファイル | 理由 |
|---------|------|
| `WindowsNotificationManager.cs` | 変更不要（実装済み） |
| `WindowsNotificationJsonBuilder.cs` | 変更不要（サンプルペイロードはコントローラ側で構築）|
| `WindowsNotificationPayloads.cs` | 変更不要 |

---

## 4. 実装方針

### 4.1 共通実装パターンの維持と拡張

| パターン | 維持 / 拡張 |
|---------|-----------|
| TopMenu → ExampleController 導線 | 維持（NavigatorにShowWindowsNotification追加）|
| タイトル + ResultTextBlock ヘッダー固定 | 維持（macOS Notification と同構造）|
| セクション単位のボタン群 | 維持（Initialize / Show / Remove / Query / Badge / Focus Assist）|
| `UnityMainThreadDispatcher` 経由でのUI更新 | 維持（`NotificationOperationCompleted` event のコールバックはメインスレッド保証済み） |
| `OnEnable` / `OnDisable` でのイベント購読管理 | 維持（`NotificationOperationCompleted`, `NotificationInvoked`, `GetAllNotificationsCompleted` を購読）|
| 全ハンドラ先頭 `Debug.Log` | 維持（csharp.md ルール準拠）|
| Editor 上のフォールバック表示 | 拡張 — `Awake` で Editor ダイアログを表示（WindowsDialog と同パターン）。個別ハンドラでは `#else SetResult("Windows Standalone only...")` |

### 4.2 Initialize 入力の状態管理

- `isPackaged` は `Toggle` で管理（デフォルト: false）
- `appUserModelId`, `clsid`, `launchUri` は `TextField` で管理
- サンプルデフォルト値: `appUserModelId = "com.example.app"`, `clsid = "{00000000-0000-0000-0000-000000000000}"`, `launchUri = "myapp://"`

### 4.3 NotificationInvoked イベントの扱い

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
    private const string SampleNotificationTag = "win-sample-notification";

    // UI refs
    private Label? _resultLabel;
    private Button? _homeButton;
    private Toggle? _isPackagedToggle;
    private TextField? _appUserModelIdField;
    private TextField? _clsidField;
    private TextField? _launchUriField;
    private Button? _initializeButton;
    private Button? _showNotificationButton;
    private Button? _updateProgressButton;
    private Button? _removeNotificationButton;
    private Button? _removeAllButton;
    private Button? _getAllButton;
    private Button? _getSettingButton;
    private Button? _openSettingsButton;
    private Button? _setBadgeAlertButton;
    private Button? _setBadgeNewMessageButton;
    private Button? _clearBadgeButton;
    private Button? _setFocusAssistOnButton;
    private Button? _setFocusAssistOffButton;

    // Awake: Editor ダイアログ表示（WindowsDialogと同パターン）
    // Start: InitializeUI()
    // OnEnable: イベント購読 (#if UNITY_STANDALONE_WIN && !UNITY_EDITOR)
    // OnDisable: イベント解除
    // OnDestroy: ボタン clicked -= アンバインド
}
#endif
```

### 5.2 Manager イベント購読方針

```csharp
private void OnEnable()
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    WindowsNotificationManager.Instance.NotificationOperationCompleted += OnNotificationOperationCompleted;
    WindowsNotificationManager.Instance.NotificationInvoked += OnNotificationInvoked;
    WindowsNotificationManager.Instance.GetAllNotificationsCompleted += OnGetAllNotificationsCompleted;
#endif
}
```

### 5.3 サンプルペイロード

```csharp
// ShowNotification 用固定ペイロード
var payload = new WindowsNotificationPayload
{
    Title = "Energy Refilled",
    Body = "Your squad is fully rested. Jump back in and clear the next raid.",
    Buttons = new List<WindowsNotificationButtonPayload>
    {
        new() { Label = "Open", Args = new Dictionary<string, string> { ["action"] = "open" } }
    }
};
var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
WindowsNotificationManager.Instance.ShowNotification(json, SampleNotificationTag);

// UpdateNotificationProgress 用固定ペイロード
var progress = new WindowsNotificationProgressPayload
{
    Value = 0.5,
    ValueStr = "50%",
    Status = "Downloading..."
};
WindowsNotificationManager.Instance.UpdateNotificationProgress(SampleNotificationTag, progress);
```

### 5.4 入力バリデーション方針

- `Initialize` ボタン押下時、`appUserModelId` / `clsid` が空文字なら `SetResult("appUserModelId and clsid are required.")` として早期リターン
- `launchUri` は空文字可（null として渡す）
- その他の操作は UI 上でのバリデーションなし（Manager / DLL 側のエラーを resultLabel に表示）

### 5.5 UXML 構造方針

- macOS Notification と同じ `header 固定 + ScrollView` 構造を採用
- Windows Fluent Design カラー: primary `#0078D4`, secondary `#636366`, background `#F3F3F3`
- font-size: 24px（Windows Dialog と統一）
- Initialize セクションに `Toggle` + `TextField` x3 を配置（既存パターンにない拡張）

### 5.6 USS クラス命名

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
| Initialize → ShowNotification が成功すること | Windows 実機、appUserModelId / clsid / launchUri を入力して実行 |
| 通知バナーがシステムトレイに届くこと | Windows アクションセンターで確認 |
| 通知バナーをクリックすると `NotificationInvoked` event が `ResultTextBlock` に届くこと | バナークリック後の ResultTextBlock を確認 |
| UpdateNotificationProgress が通知の進捗バーを更新すること | 通知センターで進捗バーの変化を確認 |
| RemoveNotification / RemoveAllNotifications が通知センターから通知を削除すること | 通知センター上での消去を確認 |
| GetAllNotifications が JSON 配列を ResultTextBlock に表示すること | 結果文字列が `[` で始まる JSON であることを確認 |
| GetNotificationSetting が `Enabled` / `DisabledForApplication` 等を返すこと | 設定変更後に確認 |
| SetBadge(Alert) がタスクバーアイコンにバッジを表示すること | タスクバーのバッジを確認 |
| ClearBadge でバッジが消えること | タスクバーのバッジ消去を確認 |
| Editor 上でボタンを押すと "Windows Standalone only." が表示されること | Editor で動作確認 |
| TopMenu の Notification ボタンが Windows で WindowsNotification 画面に遷移すること | Windows スタンドアロンで確認 |

---

## 7. 実装計画をユーザーに確認する

この実装計画を採用して、次工程へ進めますか？

- **承認する**: 計画を確定し終了 → review-document スキルへ引き継ぐ
- **修正する**: 指摘内容を反映して計画ファイルを更新 → ステップ7へ戻る
- **キャンセル**: 計画ファイルは保持したまま終了
