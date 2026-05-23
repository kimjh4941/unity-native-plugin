# サンプルシーン実装計画書

- 日付: 2026-05-16
- 機能名: macos-notification
- 対象プラットフォーム: macOS Standalone
- ブランチ: feature/UNT-3
- 参照実装結果: artifact/results/macos-notification/2026-05-16-macos-notification-implementation-result-v1.md

---

## 1. 既存コードベース深掘り結果

### 1.1 再利用するコンポーネント

| コンポーネント | 再利用方法 |
|---|---|
| `IosNotificationManagerExampleController.cs` | 構造・パターンを参考に macOS 向けに移植。差分のみ変更 |
| `IosNotificationManagerExample.uxml` | セクション・ボタン名体系を参考に macOS 向けに再作成 |
| `IosNotificationManagerExampleStyle.uss` | クラス名プレフィックスを `mac-notif-` に変更して再作成 |
| `NativeToolkitSampleNavigator.cs` | `ShowMacNotification()` メソッドを追加 |
| `TopMenuExampleController.cs` | macOS で Notification ボタンを表示する条件を追加 |

### 1.2 iOS との差分（macOS 固有の変更点）

| 項目 | iOS | macOS |
|---|---|---|
| attachment | ShowImmediateWithAttachment ボタンあり | なし（macOS は未対応） |
| location trigger | ShowLocation / ScheduleLocation ボタンあり | なし（macOS は未対応） |
| GetAuthorizationStatus 戻り型 | `MacNotificationAuthorizationStatus` (enum 直接) | `MacNotificationJsonResult` → `MacNotificationAuthorizationStatusParser.ParseJson(result.Json)` でパース |
| RemoveCategory | fire-and-forget（callback なし） | SimpleCallback あり → 結果表示 |
| ScheduleNotification | `(contentJson, triggerJson, identifier, callback)` | `(contentJson, triggerJson, callback)`（identifier なし） |
| compile guard | `#if UNITY_IOS && !UNITY_EDITOR` | `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` |

### 1.3 追加するコンポーネント

| コンポーネント | パス |
|---|---|
| `MacNotificationManagerExampleController.cs` | `Runtime/UI/macOS/Notification/` |
| `MacNotificationManagerExample.uxml` | `Runtime/Resources/UI/macOS/Notification/` |
| `MacNotificationManagerExampleStyle.uss` | `Runtime/Resources/UI/macOS/Notification/` |

### 1.4 共通実装パターンとの比較

| パターン | 維持 / 拡張 |
|---|---|
| TopMenu → ExampleController 導線 | 維持（`NativeToolkitSampleNavigator.ShowMacNotification` 経由） |
| タイトル・結果表示領域ヘッダー | 維持（`ResultTextBlock`・タイトル Label） |
| 機能カテゴリ単位のセクション分け | 維持（Permission / Show / Update・Cancel・Remove / Schedule / Query / Badge / Category） |
| `SetResult` による一元的な結果表示 | 維持 |
| イベント購読: `OnEnable` / `OnDisable` | **拡張（iOS は Start/OnDestroy 使用だが workflow 規定に従い OnEnable/OnDisable を採用）** |
| ログ: `Debug.Log` 全パラメータ | 維持（csharp.md 準拠） |

---

## 2. 画面要件

### 2.1 機能一覧（ボタン）

| セクション | ボタン名 | 表示ラベル | 主ボタン / セカンダリ |
|---|---|---|---|
| Permission | RequestPermissionButton | RequestPermission | 主 |
| Permission | HasPermissionButton | HasPermission | 主 |
| Permission | AuthorizationStatusButton | AuthorizationStatus | 主 |
| Permission | OpenSettingsButton | OpenSettings | セカンダリ |
| Show Notification | ShowImmediateButton | ShowImmediate | 主 |
| Show Notification | ShowTimeIntervalButton | ShowTimeInterval(5s) | 主 |
| Show Notification | ShowCalendarButton | ShowCalendar(+1m) | 主 |
| Update / Cancel / Remove | UpdateByIdButton | UpdateById | 主 |
| Update / Cancel / Remove | CancelByIdButton | CancelById | セカンダリ |
| Update / Cancel / Remove | CancelAllButton | CancelAll | セカンダリ |
| Update / Cancel / Remove | RemoveDeliveredByIdButton | RemoveDeliveredById | セカンダリ |
| Update / Cancel / Remove | RemoveAllDeliveredButton | RemoveAllDelivered | セカンダリ |
| Schedule | ScheduleTimeIntervalButton | ScheduleTimeInterval(10s) | 主 |
| Schedule | ScheduleCalendarButton | ScheduleCalendar(+1m) | 主 |
| Schedule | CancelScheduledByIdButton | CancelScheduledById | セカンダリ |
| Schedule | CancelAllScheduledButton | CancelAllScheduled | セカンダリ |
| Query | GetScheduledButton | GetScheduled | 主 |
| Query | GetDeliveredButton | GetDelivered | 主 |
| Badge | SetBadgeCount1Button | SetBadgeCount(1) | 主 |
| Badge | SetBadgeCount0Button | SetBadgeCount(0) | セカンダリ |
| Category | RegisterCategoryButton | RegisterCategory | 主 |
| Category | RemoveCategoryButton | RemoveCategory | セカンダリ |

合計: 22 ボタン（iOS より ShowImmediateWithAttachment / ShowLocation / ScheduleLocation の 3 ボタンが減少）

### 2.2 操作導線

```
ユーザー操作:
  1. Top Menu → "Notification" ボタンタップ
  2. macOS Notification 画面へ遷移（ShowMacNotification 経由）
  3. 任意のボタンをタップ
  4. ResultTextBlock に結果が更新される（成功: "✓ <OperationName>" / 失敗: "✗ <OperationName>\nError: <message>"）
  5. "Back To Home" ボタンで Top Menu へ戻る
```

### 2.3 エラー表示

- 権限未付与: `"<OperationName>: Please allow notification permission first."` (macOS Standalone のみ)
- API 失敗: `"✗ <OperationName>\nError: <errorMessage>"` 
- Editor 実行: `"macOS Standalone only. Run this sample on macOS to verify."`
- fire-and-forget 操作: `"<OperationName>: requested"` (コールバックなし)

---

## 3. 変更ファイル一覧

### 3.1 新規作成

| ファイルパス | 内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Notification/MacNotificationManagerExampleController.cs` | macOS 通知サンプル ExampleController |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/macOS/Notification/MacNotificationManagerExample.uxml` | UI Toolkit レイアウト定義 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/macOS/Notification/MacNotificationManagerExampleStyle.uss` | UI スタイル定義 |

### 3.2 既存変更

| ファイルパス | 変更内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowMacNotification()` メソッド追加。`RemoveExistingControllers` に `MacNotificationManagerExampleController` を追加 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs` | Notification ボタン表示条件に `UNITY_STANDALONE_OSX` を追加。`OnNotificationClicked` に macOS 分岐を追加 |

### 3.3 非変更

| ファイルパス | 理由 |
|---|---|
| `Assets/Samples/Native Toolkit/1.0.0/Native Toolkit Example/NativeToolkitExampleScene.unity` | シーンファイルは既存を維持（UIDocument が既に配置済みのため変更不要） |
| `MacNotificationManager.cs` 等 Runtime 実装ファイル | サンプルシーン設計は UI/Controller 層のみ対象 |

---

## 4. 実装方針

### 4.1 再利用・拡張方針

- `MacNotificationManagerExampleController.cs` は `IosNotificationManagerExampleController.cs` の構造を参考にしつつ、macOS 固有の差分のみ変更する
- UXML は `IosNotificationManagerExample.uxml` のセクション構造をそのまま踏襲し、ボタン名と表示ラベルを macOS 向けに変更
- USS は iOS 向けのクラス定義を `mac-notif-` プレフィックスで再定義する。色はほぼ同一（macOS primary: `#0A84FF`）

### 4.2 コールバック購読ライフサイクル

iOS ExampleController は `Start` / `OnDestroy` で購読を管理しているが、本計画では workflow 規定に従い `OnEnable` / `OnDisable` を採用する。

```csharp
private void OnEnable()
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    MacNotificationManager.Instance.NotificationOperationCompleted += OnNotificationOperationCompleted;
    MacNotificationManager.Instance.NotificationActionReceived += OnNotificationActionReceived;
    MacNotificationManager.Instance.NotificationTextInputActionReceived += OnNotificationTextInputActionReceived;
#endif
}

private void OnDisable()
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    MacNotificationManager.Instance.NotificationOperationCompleted -= OnNotificationOperationCompleted;
    MacNotificationManager.Instance.NotificationActionReceived -= OnNotificationActionReceived;
    MacNotificationManager.Instance.NotificationTextInputActionReceived -= OnNotificationTextInputActionReceived;
#endif
}
```

---

## 5. 実装詳細

### 5.1 MacNotificationManagerExampleController.cs

```csharp
#nullable enable
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using JonghyunKim.NativeToolkit.Runtime.Notification;
using UnityEngine;
using UnityEngine.UIElements;

public class MacNotificationManagerExampleController : MonoBehaviour
{
    private const string LogTag = "MacNotificationManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private const string SampleNotificationId = "mac-sample-notification";
    private const string SampleScheduledId = "mac-scheduled-notification";
    private const string SampleCategoryId = "mac-sample-category";
    private const string NotificationPermissionRequiredMessage = "Please allow notification permission first.";
}
#endif
```

**フィールド:**
- `_resultLabel` (Label)
- `_homeButton` (Button)
- 22 個のボタンフィールド（Button?）

**ライフサイクル:**
- `Awake`: Log のみ
- `Start`: UIDocument 取得 → `InitializeUI()`
- `OnEnable`: グローバルイベント購読（`#if UNITY_STANDALONE_OSX && !UNITY_EDITOR`）
- `OnDisable`: グローバルイベント購読解除
- `OnDestroy`: ボタン clicked イベント解除 + Log

**ヘルパーメソッド:**
- `ExecuteIfNotificationPermissionGranted(string operationName, Action onGranted)` — `MacNotificationManager.Instance.HasPermission` で権限確認してから `onGranted()` を呼ぶ
- `static string FormatResult(string label, MacNotificationResult result)` — `IsSuccess ? "✓ label" : "✗ label\nError: errorMessage"` を返す
- `void SetResult(string message)` — `Debug.Log` + `_resultLabel.text = message`

### 5.2 各ボタンハンドラの実装方針

| ハンドラ | 呼び出す API | 結果表示方法 |
|---|---|---|
| `OnRequestPermissionClicked` | `MacNotificationManager.Instance.RequestPermission(result => SetResult(FormatResult(...)))` | per-call callback |
| `OnHasPermissionClicked` | `MacNotificationManager.Instance.HasPermission(v => SetResult($"HasPermission: {v}"))` | per-call callback |
| `OnAuthorizationStatusClicked` | `MacNotificationManager.Instance.GetAuthorizationStatus(result => { var status = MacNotificationAuthorizationStatusParser.ParseJson(result.Json); SetResult(...); })` | per-call callback + ParseJson |
| `OnOpenSettingsClicked` | `MacNotificationManager.Instance.OpenSettings(result => SetResult(FormatResult(...)))` | per-call callback |
| `OnShowImmediateClicked` | `ExecuteIfPermissionGranted` → `MacNotificationManager.Instance.ShowNotification(contentJson, null, callback)` | per-call callback |
| `OnShowTimeIntervalClicked` | `ExecuteIfPermissionGranted` → `ShowNotification(contentJson, timeIntervalTriggerJson, callback)` (5秒) | per-call callback |
| `OnShowCalendarClicked` | `ExecuteIfPermissionGranted` → `ShowNotification(contentJson, calendarTriggerJson, callback)` (+1分) | per-call callback |
| `OnUpdateByIdClicked` | `ExecuteIfPermissionGranted` → `UpdateNotification(SampleNotificationId, contentJson, null, callback)` | per-call callback |
| `OnCancelByIdClicked` | `ExecuteIfPermissionGranted` → `CancelNotification(SampleNotificationId)` + `SetResult("... requested")` | fire-and-forget |
| `OnCancelAllClicked` | `CancelAllNotifications()` + `SetResult("... requested")` | fire-and-forget |
| `OnRemoveDeliveredByIdClicked` | `RemoveDeliveredNotification(SampleNotificationId)` + `SetResult("... requested")` | fire-and-forget |
| `OnRemoveAllDeliveredClicked` | `RemoveAllDeliveredNotifications()` + `SetResult("... requested")` | fire-and-forget |
| `OnScheduleTimeIntervalClicked` | `ExecuteIfPermissionGranted` → `ScheduleNotification(contentJson, triggerJson, callback)` (10秒) | per-call callback |
| `OnScheduleCalendarClicked` | `ExecuteIfPermissionGranted` → `ScheduleNotification(contentJson, triggerJson, callback)` (+1分) | per-call callback |
| `OnCancelScheduledByIdClicked` | `CancelScheduledNotification(SampleScheduledId)` + `SetResult("... requested")` | fire-and-forget |
| `OnCancelAllScheduledClicked` | `CancelAllScheduledNotifications()` + `SetResult("... requested")` | fire-and-forget |
| `OnGetScheduledClicked` | `GetScheduledNotifications(result => SetResult($"GetScheduled: {result.Json}"))` | per-call callback |
| `OnGetDeliveredClicked` | `GetDeliveredNotifications(result => SetResult($"GetDelivered: {result.Json}"))` | per-call callback |
| `OnSetBadgeCount1Clicked` | `SetBadgeCount(1, result => SetResult(FormatResult(...)))` | per-call callback |
| `OnSetBadgeCount0Clicked` | `SetBadgeCount(0, result => SetResult(FormatResult(...)))` | per-call callback |
| `OnRegisterCategoryClicked` | `RegisterCategory(categoryJson, result => SetResult(...))` — category: id="mac-sample-category", actions: Open(isForeground:true) / Delete / Reply(isTextInput:true) | per-call callback |
| `OnRemoveCategoryClicked` | `RemoveCategory(SampleCategoryId, result => SetResult(FormatResult(...)))` | per-call callback (macOS は callback あり) |

### 5.3 グローバルイベントハンドラ

```csharp
private void OnNotificationOperationCompleted(MacNotificationResult result)
{
    // RegisterCategory の成功時はサイレント（結果は RegisterCategory ボタンの per-call callback が表示済み）
    if (result.IsSuccess && result.Operation == MacNotificationManager.OperationRegisterCategory)
        return;
    SetResult(FormatResult(result.Operation, result));
}

private void OnNotificationActionReceived(MacNotificationActionResult result)
{
    SetResult($"Action received: notificationId={result.NotificationId}, actionId={result.ActionId}");
}

private void OnNotificationTextInputActionReceived(MacNotificationTextInputActionResult result)
{
    SetResult($"TextInput action: notificationId={result.NotificationId}, actionId={result.ActionId}, userText={result.UserText}");
}
```

### 5.4 入力バリデーション方針

- `SampleNotificationId` / `SampleCategoryId` はハードコード定数で管理する（入力フォームなし）
- カレンダートリガーは `DateTime.Now.AddMinutes(1)` を使用（固定値）
- 権限チェックは `ExecuteIfNotificationPermissionGranted` で一元化する

### 5.5 NativeToolkitSampleNavigator.cs の変更内容

追加するメソッド:
```csharp
public static void ShowMacNotification(UIDocument uiDocument)
{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
    ApplyScreen<MacNotificationManagerExampleController>(
        uiDocument,
        "UI/macOS/Notification/MacNotificationManagerExample",
        "UI/macOS/Notification/MacNotificationManagerExampleStyle");
#endif
}
```

`RemoveExistingControllers` に追加:
```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
    RemoveIfExists<MacNotificationManagerExampleController>(gameObject);
#endif
```

### 5.6 TopMenuExampleController.cs の変更内容

Notification ボタン表示条件の変更:
```csharp
// Before:
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR

// After:
#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR
```

`OnNotificationClicked` の分岐追加:
```csharp
private void OnNotificationClicked()
{
    ...
#elif UNITY_STANDALONE_OSX
    NativeToolkitSampleNavigator.ShowMacNotification(uiDocument);
#endif
}
```

また、Editor 実行時のダイアログメッセージも macOS を含めるよう更新:
```csharp
// Before:
"This feature runs natively on Android or iOS."
// After:
"This feature runs natively on Android, iOS, or macOS."
```

### 5.7 MacNotificationManagerExample.uxml の構造

iOS の `IosNotificationManagerExample.uxml` と同一セクション構成。クラス名プレフィックスを `mac-notif-` に変更。ボタン名は 5.1 節の表に準拠。

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" ...>
    <Style src="MacNotificationManagerExampleStyle.uss" />
    <ui:VisualElement class="mac-notif-root">
        <ui:VisualElement class="mac-notif-header">
            <ui:Button name="HomeButton" class="mac-secondary-button" text="Back To Home" />
            <ui:Label text="MacNotificationManager Example" class="mac-notif-title" />
            <ui:Label text="Gameplay-focused notification scenarios for macOS." class="mac-notif-subtitle" />
            <ui:VisualElement class="mac-result-border">
                <ui:Label name="ResultTextBlock" text="Notification result will be displayed here" class="mac-result-text" />
            </ui:VisualElement>
        </ui:VisualElement>
        <ui:ScrollView class="mac-notif-scroll" vertical-scroller-visibility="Auto">
            <ui:VisualElement class="mac-notif-content">
                <!-- Permission セクション -->
                <!-- Show Notification セクション -->
                <!-- Update / Cancel / Remove セクション -->
                <!-- Schedule セクション -->
                <!-- Query セクション -->
                <!-- Badge セクション -->
                <!-- Category セクション -->
            </ui:VisualElement>
        </ui:ScrollView>
    </ui:VisualElement>
</ui:UXML>
```

### 5.8 MacNotificationManagerExampleStyle.uss の方針

- iOS の `IosNotificationManagerExampleStyle.uss` をほぼ踏襲
- クラス名プレフィックス: `ios-notif-` → `mac-notif-`、`ios-secondary-button` → `mac-secondary-button`
- 主ボタン色: `#0A84FF`（macOS システムブルー、iOS の `#007AFF` とほぼ同一）
- セカンダリボタン色: `#636366`（macOS システムグレー）

---

## 6. 手動確認観点

| 観点 | 手順 | 期待結果 |
|---|---|---|
| TopMenu → macOS Notification 導線 | macOS Standalone でシーン起動 → Notification ボタンタップ | macOS Notification 画面へ遷移 |
| Back To Home | macOS Notification 画面 → "Back To Home" タップ | Top Menu へ戻る |
| RequestPermission | RequestPermission ボタンタップ | システム権限ダイアログ表示・結果が ResultTextBlock に表示 |
| HasPermission | HasPermission ボタンタップ | `HasPermission: True/False` が表示 |
| AuthorizationStatus | AuthorizationStatus ボタンタップ | `AuthorizationStatus: Authorized` 等 enum 名が表示 |
| OpenSettings | OpenSettings ボタンタップ | システム通知設定画面が開く・結果表示 |
| ShowImmediate | ShowImmediate タップ | 即時通知が表示される |
| ShowTimeInterval | ShowTimeInterval(5s) タップ | 5秒後に通知が表示される |
| ShowCalendar | ShowCalendar(+1m) タップ | 1分後に通知が表示される |
| UpdateById | ShowImmediate 後 → UpdateById タップ | 既存通知の内容が更新される |
| CancelById | 通知スケジュール後 → CancelById タップ | 通知がキャンセルされる |
| ScheduleTimeInterval | ScheduleTimeInterval(10s) タップ | 10秒後に通知が表示される |
| ScheduleCalendar | ScheduleCalendar(+1m) タップ | 1分後に通知が表示される |
| GetScheduled | GetScheduled タップ | スケジュール済み通知の JSON が表示される |
| GetDelivered | GetDelivered タップ | 配信済み通知の JSON が表示される |
| SetBadgeCount(1) | SetBadgeCount(1) タップ | アプリアイコンにバッジ "1" が表示される |
| SetBadgeCount(0) | SetBadgeCount(0) タップ | バッジが消える |
| RegisterCategory | RegisterCategory タップ → ShowImmediate タップ | 通知に Open/Delete/Reply アクションボタンが表示される |
| RemoveCategory | RegisterCategory 後 → RemoveCategory タップ | 結果が表示される（削除確認は次回の ShowImmediate で確認） |
| NotificationActionReceived | アクションボタンをタップ | ResultTextBlock に actionId が表示される |
| NotificationTextInputActionReceived | Reply テキスト入力して送信 | ResultTextBlock に userText が表示される |
| Editor 実行 | Unity Editor でボタンをタップ | `"macOS Standalone only..."` が表示される |
