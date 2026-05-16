# サンプルシーン実装計画書

- 日付: 2026-05-16
- 機能名: macos-notification
- 対象プラットフォーム: macOS Standalone
- ブランチ: feature/UNT-3
- 参照実装結果: artifact/results/macos-notification/2026-05-16-macos-notification-implementation-result-v1.md
- 前バージョン: 2026-05-16-macos-notification-sample-scene-plan-v1.md
- 変更概要: H-1（OnEnable/OnDisable 根拠追記）/ H-2（Editor fallback パターン明確化）/ H-3（ダブル発火回避戦略）/ M-1（JsonBuilder 入力マッピング追記）/ M-2（identifier 生成方針追記）/ M-3（namespace 追記）

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
| GetAuthorizationStatus 戻り型 | enum 直接 | `MacNotificationJsonResult` → `MacNotificationAuthorizationStatusParser.ParseJson(result.Json)` でパース |
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
| イベント購読ライフサイクル | **拡張: `OnEnable`/`OnDisable` を採用（iOS は `Start`/`OnDestroy` 使用）** |
| ログ: `Debug.Log` 全パラメータ | 維持（csharp.md 準拠） |

**OnEnable/OnDisable 採用根拠（H-1 対応）:**

`NativeToolkitSampleNavigator.ApplyScreen<T>()` は画面切替のたびに既存コントローラーを Destroy して新規 GameObject を Instantiate する。このため `Start` は初回のみ実行され、`OnDestroy` は画面離脱時に発火する。将来的にコントローラーを再活性化する実装（例: キャッシュ再利用）に備え、`OnEnable`/`OnDisable` を採用してサブスクリプション解除漏れを防ぐ。現 iOS 実装が `Start`/`OnDestroy` を使う理由は明示されていないため、macOS では安全側の `OnEnable`/`OnDisable` を選択する。

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

### 2.4 Editor fallback 実装パターン（H-2 対応）

`MacNotificationManagerExampleController.cs` は `#if UNITY_STANDALONE_OSX || UNITY_EDITOR` でコンパイル対象に含める（Editor でも型参照が通るようにするため）。ただし Manager 呼び出しは `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` で保護する。

Editor 実行時の処理フロー:

```csharp
private void OnShowImmediateClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnShowImmediateClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    ExecuteIfNotificationPermissionGranted("ShowNotification", () =>
    {
        // ... 実際の API 呼び出し
    });
#else
    SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
}
```

このパターンをすべてのボタンハンドラに適用する。`ExecuteIfNotificationPermissionGranted` 自体も `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` ブロック内で呼ぶ。

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

iOS ExampleController は `Start` / `OnDestroy` で購読を管理しているが、本計画では Section 1.4 記載の根拠に従い `OnEnable` / `OnDisable` を採用する。

```csharp
private void OnEnable()
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    MacNotificationManager.Instance.NotificationActionReceived += OnNotificationActionReceived;
    MacNotificationManager.Instance.NotificationTextInputActionReceived += OnNotificationTextInputActionReceived;
#endif
}

private void OnDisable()
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    MacNotificationManager.Instance.NotificationActionReceived -= OnNotificationActionReceived;
    MacNotificationManager.Instance.NotificationTextInputActionReceived -= OnNotificationTextInputActionReceived;
#endif
}
```

### 4.3 グローバルイベント vs per-call callback の使い分け（H-3 対応）

`NotificationOperationCompleted` はボタン操作に起因しない自発的なイベント（将来的なバックグラウンド通知受信など）のみを対象とするため、本サンプルでは購読しない。

- すべての SimpleCallback 系操作（RequestPermission / OpenSettings / ShowNotification / ScheduleNotification / SetBadgeCount / RegisterCategory / RemoveCategory）は per-call callback のみで結果を表示する
- `NotificationOperationCompleted` を購読しないことで、同一操作結果の二重表示を構造的に回避する

`NotificationActionReceived` と `NotificationTextInputActionReceived` はユーザーの通知アクション（通知センターからのアクションボタン操作）に対応する自発的なイベントであり、これは引き続き購読する。

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

namespace JonghyunKim.NativeToolkit.Runtime.UI
{
    public class MacNotificationManagerExampleController : MonoBehaviour
    {
        private const string LogTag = "MacNotificationManagerExampleController";

        [SerializeField] private UIDocument? uiDocument;

        private const string SampleNotificationId = "mac-sample-notification";
        private const string SampleCategoryId = "mac-sample-category";
        private const string NotificationPermissionRequiredMessage = "Please allow notification permission first.";
    }
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
- `OnEnable`: `NotificationActionReceived` / `NotificationTextInputActionReceived` 購読（`#if UNITY_STANDALONE_OSX && !UNITY_EDITOR`）
- `OnDisable`: 上記のイベント購読解除
- `OnDestroy`: ボタン clicked イベント解除 + Log

**ヘルパーメソッド:**
- `ExecuteIfNotificationPermissionGranted(string operationName, Action onGranted)` — `MacNotificationManager.Instance.HasPermission` で権限確認してから `onGranted()` を呼ぶ
- `static string FormatResult(string label, MacNotificationResult result)` — `IsSuccess ? "✓ label" : "✗ label\nError: errorMessage"` を返す
- `void SetResult(string message)` — `Debug.Log` + `_resultLabel.text = message`

### 5.2 各ボタンハンドラの実装方針

**全ハンドラ共通テンプレート（H-2 対応）:**

```csharp
private void OnShowImmediateClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnShowImmediateClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    ExecuteIfNotificationPermissionGranted("ShowNotification", () =>
    {
        var contentJson = MacNotificationJsonBuilder.BuildContentJson("Sample", "Immediate notification body");
        MacNotificationManager.Instance.ShowNotification(contentJson, null, result =>
            SetResult(FormatResult("ShowNotification", result)));
    });
#else
    SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
}
```

| ハンドラ | 呼び出す API | 結果表示方法 |
|---|---|---|
| `OnRequestPermissionClicked` | `RequestPermission(result => SetResult(FormatResult(...)))` | per-call callback |
| `OnHasPermissionClicked` | `HasPermission(v => SetResult($"HasPermission: {v}"))` | per-call callback |
| `OnAuthorizationStatusClicked` | `GetAuthorizationStatus(result => { var status = MacNotificationAuthorizationStatusParser.ParseJson(result.Json); SetResult($"AuthorizationStatus: {status}"); })` | per-call callback + ParseJson |
| `OnOpenSettingsClicked` | `OpenSettings(result => SetResult(FormatResult(...)))` | per-call callback |
| `OnShowImmediateClicked` | `ExecuteIfPermissionGranted` → `ShowNotification(contentJson, null, callback)` | per-call callback |
| `OnShowTimeIntervalClicked` | `ExecuteIfPermissionGranted` → `ShowNotification(contentJson, timeIntervalTriggerJson, callback)` (5秒) | per-call callback |
| `OnShowCalendarClicked` | `ExecuteIfPermissionGranted` → `ShowNotification(contentJson, calendarTriggerJson, callback)` (+1分) | per-call callback |
| `OnUpdateByIdClicked` | `ExecuteIfPermissionGranted` → `UpdateNotification(SampleNotificationId, contentJson, null, callback)` | per-call callback |
| `OnCancelByIdClicked` | `ExecuteIfPermissionGranted` → `CancelNotification(SampleNotificationId)` + `SetResult("... requested")` | fire-and-forget |
| `OnCancelAllClicked` | `ExecuteIfPermissionGranted` → `CancelAllNotifications()` + `SetResult("... requested")` | fire-and-forget |
| `OnRemoveDeliveredByIdClicked` | `ExecuteIfPermissionGranted` → `RemoveDeliveredNotification(SampleNotificationId)` + `SetResult("... requested")` | fire-and-forget |
| `OnRemoveAllDeliveredClicked` | `ExecuteIfPermissionGranted` → `RemoveAllDeliveredNotifications()` + `SetResult("... requested")` | fire-and-forget |
| `OnScheduleTimeIntervalClicked` | `ExecuteIfPermissionGranted` → `ScheduleNotification(contentJson, triggerJson, callback)` (10秒) | per-call callback |
| `OnScheduleCalendarClicked` | `ExecuteIfPermissionGranted` → `ScheduleNotification(contentJson, triggerJson, callback)` (+1分) | per-call callback |
| `OnCancelScheduledByIdClicked` | `ExecuteIfPermissionGranted` → `CancelScheduledNotification(SampleNotificationId)` + `SetResult("... requested")` | fire-and-forget |
| `OnCancelAllScheduledClicked` | `ExecuteIfPermissionGranted` → `CancelAllScheduledNotifications()` + `SetResult("... requested")` | fire-and-forget |
| `OnGetScheduledClicked` | `ExecuteIfPermissionGranted` → `GetScheduledNotifications(result => SetResult($"GetScheduled: {result.Json}"))` | per-call callback |
| `OnGetDeliveredClicked` | `ExecuteIfPermissionGranted` → `GetDeliveredNotifications(result => SetResult($"GetDelivered: {result.Json}"))` | per-call callback |
| `OnSetBadgeCount1Clicked` | `ExecuteIfPermissionGranted` → `SetBadgeCount(1, result => SetResult(FormatResult(...)))` | per-call callback |
| `OnSetBadgeCount0Clicked` | `ExecuteIfPermissionGranted` → `SetBadgeCount(0, result => SetResult(FormatResult(...)))` | per-call callback |
| `OnRegisterCategoryClicked` | `ExecuteIfPermissionGranted` → `RegisterCategory(categoryJson, result => SetResult(FormatResult(...)))` | per-call callback |
| `OnRemoveCategoryClicked` | `ExecuteIfPermissionGranted` → `RemoveCategory(SampleCategoryId, result => SetResult(FormatResult(...)))` | per-call callback |

### 5.3 JsonBuilder 呼び出しと入力マッピング（M-1 対応）

**ContentJson（iOS と同一内容）:**

```csharp
// ShowImmediate
var contentJson = MacNotificationJsonBuilder.BuildContentJson(
    title: "Energy Refilled",
    body: "Your squad is fully rested. Jump back in and clear the next raid.",
    subtitle: null,
    badge: null,
    sound: null,
    userInfo: null,
    categoryIdentifier: SampleCategoryId);

// ShowTimeInterval (5秒)
var contentJson = MacNotificationJsonBuilder.BuildContentJson(
    title: "Guild Battle Countdown",
    body: "Your team queue opens in 5 seconds. Rally your party and get ready.",
    subtitle: null,
    badge: null,
    sound: null,
    userInfo: null,
    categoryIdentifier: null);

// ShowCalendar (+1分)
var contentJson = MacNotificationJsonBuilder.BuildContentJson(
    title: "Daily Reward Ready",
    body: "Your login streak chest is ready in town. Claim it before reset.",
    subtitle: null,
    badge: null,
    sound: null,
    userInfo: null,
    categoryIdentifier: null);

// UpdateById
var contentJson = MacNotificationJsonBuilder.BuildContentJson(
    title: "Town Entry Bonus",
    body: "Welcome back to town. Your blacksmith bonus is now available.",
    subtitle: null,
    badge: null,
    sound: null,
    userInfo: null,
    categoryIdentifier: null);

// ScheduleTimeInterval (10秒)
var contentJson = MacNotificationJsonBuilder.BuildContentJson(
    title: "Guild Battle Starts Soon",
    body: "Battle queue opens in 10 seconds. Finalize your loadout and deploy.",
    subtitle: null,
    badge: null,
    sound: null,
    userInfo: null,
    categoryIdentifier: null);

// ScheduleCalendar (+1分)
var contentJson = MacNotificationJsonBuilder.BuildContentJson(
    title: "Daily Reward Window",
    body: "Your daily reward window is open. Check in now to keep your streak.",
    subtitle: null,
    badge: null,
    sound: null,
    userInfo: null,
    categoryIdentifier: null);
```

**TimeInterval trigger（ShowTimeInterval: 5秒 / ScheduleTimeInterval: 10秒）:**

```csharp
// Show 系: 5秒
var triggerJson = MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson(seconds: 5.0, repeats: false);

// Schedule 系: 10秒
var triggerJson = MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson(seconds: 10.0, repeats: false);
```

**Calendar trigger（ShowCalendar / ScheduleCalendar: +1分）:**

```csharp
var dt = DateTime.Now.AddMinutes(1);
var triggerJson = MacNotificationJsonBuilder.BuildCalendarTriggerJson(
    year: dt.Year, month: dt.Month, day: dt.Day,
    hour: dt.Hour, minute: dt.Minute, second: dt.Second,
    repeats: false);
```

**RegisterCategory の categoryJson（iOS と同一アクション）:**

```csharp
var openAction = MacNotificationJsonBuilder.BuildActionJson(
    actionId: "open", title: "Open", isForeground: true, isTextInput: false, textInputButtonTitle: null, textInputPlaceholder: null);
var deleteAction = MacNotificationJsonBuilder.BuildActionJson(
    actionId: "delete", title: "Delete", isForeground: false, isTextInput: false, textInputButtonTitle: null, textInputPlaceholder: null);
var replyAction = MacNotificationJsonBuilder.BuildActionJson(
    actionId: "reply", title: "Reply", isForeground: false, isTextInput: true, textInputButtonTitle: "Send", textInputPlaceholder: "Type a message");
var categoryJson = MacNotificationJsonBuilder.BuildCategoryJson(
    categoryId: SampleCategoryId,
    actions: new[] { openAction, deleteAction, replyAction });
```

### 5.4 identifier 生成方針（M-2 対応）

- `ShowNotification` / `CancelNotification` / `RemoveDeliveredNotification` / `UpdateNotification` の identifier として `SampleNotificationId = "mac-sample-notification"` を使用する（ハードコード固定文字列）
- `CancelScheduledNotification` の identifier として `SampleNotificationId` を再利用する（Schedule は identifier を持たないため、キャンセル対象は Show 系で表示された通知を想定）
- GUID 生成はサンプルの目的（動作確認）には過剰なため採用しない

### 5.5 グローバルイベントハンドラ（H-3 対応）

`NotificationOperationCompleted` は購読しない。`NotificationActionReceived` と `NotificationTextInputActionReceived` のみ購読する。

```csharp
private void OnNotificationActionReceived(MacNotificationActionResult result)
{
    SetResult($"Action received: notificationId={result.NotificationId}, actionId={result.ActionId}");
}

private void OnNotificationTextInputActionReceived(MacNotificationTextInputActionResult result)
{
    SetResult($"TextInput action: notificationId={result.NotificationId}, actionId={result.ActionId}, userText={result.UserText}");
}
```

### 5.6 NativeToolkitSampleNavigator.cs の変更内容

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

### 5.7 TopMenuExampleController.cs の変更内容

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

Editor 実行時のダイアログメッセージを macOS を含めるよう更新:

```csharp
// Before:
"This feature runs natively on Android or iOS."
// After:
"This feature runs natively on Android, iOS, or macOS."
```

### 5.8 MacNotificationManagerExample.uxml の構造

iOS の `IosNotificationManagerExample.uxml` と同一セクション構成。クラス名プレフィックスを `mac-notif-` に変更。ボタン名は Section 2.1 の表に準拠。

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
        <ui:ScrollView class="mac-notif-scroll" vertical-scroller-visibility="Auto" horizontal-scroller-visibility="Hidden">
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

### 5.9 MacNotificationManagerExampleStyle.uss の方針

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
| RemoveCategory | RegisterCategory 後 → RemoveCategory タップ | 結果が表示される（削除確認は次回の ShowImmediate で確認） |
| NotificationActionReceived | "Open" または "Delete" をタップ | ResultTextBlock に actionId が表示される |
| NotificationTextInputActionReceived | "Reply" でメッセージを入力して送信 | ResultTextBlock に userText が表示される |
| Editor 実行 | Unity Editor でボタンをタップ | `"macOS Standalone only..."` が表示される |
| 権限拒否時の操作 | HasPermission=False の状態で ShowImmediate タップ | `"ShowNotification: Please allow notification permission first."` が表示される |
