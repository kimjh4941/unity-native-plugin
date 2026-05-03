# 通知機能

言語:

- 日本語（このページ）
- English: [notification.md](notification.md)
- 한국어: [notification.ko.md](notification.ko.md)

← [マニュアルトップへ戻る](index.ja.md)

---

## 目次

- [Android](#android)
  - [セットアップ](#セットアップ)
  - [権限](#権限)
  - [チャンネル管理](#チャンネル管理)
  - [基本的な通知操作](#基本的な通知操作)
  - [通知スタイル](#通知スタイル)
  - [カスタムビュースタイル](#カスタムビュースタイル)
  - [インタラクション](#インタラクション)
  - [プログレス通知](#プログレス通知)
  - [フォアグラウンドサービス通知](#フォアグラウンドサービス通知)
  - [スケジュール通知](#スケジュール通知)
- [iOS](#ios)
- [Windows](#windows)
- [macOS](#macos)

---

## Android

### セットアップ

#### AndroidManifest.xml

使用する機能に必要な権限を追加します。

```xml
<!-- Android 13 以降で通知を送信するために必要 -->
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />

<!-- スケジュール通知（正確なアラーム）に必要 -->
<uses-permission android:name="android.permission.SCHEDULE_EXACT_ALARM" />
<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" />

<!-- フォアグラウンドサービスに必要 -->
<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_DATA_SYNC" />
```

#### 名前空間のインポート

```csharp
// 実行ガード: Android (Player) のみ有効。Editor ではネイティブ呼び出しを行わないようにします。
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Notification;
#endif
```

> **注記:** `ChannelPayload` / `NotificationPayload` などの DTO と `AndroidNotificationJsonBuilder` はランタイムに含まれています。`JsonUtility.ToJson(...)` ではなく、`AndroidNotificationJsonBuilder` を使うと optional フィールドや `data` を含む spec 準拠 JSON を組み立てられます。

---

### 権限

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// 通知権限が付与されているか（Android 13 以降）
bool hasPermission = AndroidNotificationManager.Instance.HasPermission();

// アプリの通知が有効か
bool enabled = AndroidNotificationManager.Instance.AreNotificationsEnabled();

// 正確なアラームのスケジュールが許可されているか（Android 12 以降）
bool canSchedule = AndroidNotificationManager.Instance.CanScheduleExactAlarms();

// POST_NOTIFICATIONS 権限をリクエスト（Android 13 以降）
AndroidNotificationManager.Instance.RequestPermission(granted =>
{
    if (granted) { /* 権限が付与された */ }
});

// 設定画面を開く
AndroidNotificationManager.Instance.OpenNotificationSettings();
AndroidNotificationManager.Instance.OpenAppDetailsSettings();
AndroidNotificationManager.Instance.OpenExactAlarmSettings();
#endif
```

---

### チャンネル管理

通知を送信する前に、通知チャンネルを作成する必要があります。

#### チャンネルを作成する

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string channelJson = AndroidNotificationJsonBuilder.BuildChannelJson(new ChannelPayload
{
    id = "my_channel",
    name = "マイチャンネル",
    importance = 3,             // 3 = DEFAULT
    description = "サンプル通知チャンネル",
    showBadge = true,
    enableLights = true,
    lightColor = unchecked((int)0xFF4CAF50),
    enableVibration = true,
    vibrationPattern = new long[] { 0, 250, 200, 250 },
    lockscreenVisibility = 1,   // 1 = PUBLIC
    groupId = "my_group",
    groupName = "マイグループ"
});

AndroidNotificationManager.Instance.CreateChannel(channelJson);
#endif
```

チャンネルの重要度レベル:

| 値  | レベル  | 説明                     |
| --- | ------- | ------------------------ |
| 1   | MIN     | 音なし、ヘッドアップなし |
| 2   | LOW     | 音なし                   |
| 3   | DEFAULT | 音あり                   |
| 4   | HIGH    | 音あり＋ヘッドアップ     |

#### チャンネルを削除する

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.DeleteChannel("my_channel");
#endif
```

#### イベントで結果を受け取る

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationOperationCompleted += OnOperationCompleted;
#endif

private void OnOperationCompleted(NotificationResult result)
{
    // result.Operation    — どの操作が完了したかを示す
    // result.IsSuccess    — 成功の場合 true
    // result.ErrorMessage — 失敗の場合に非 null
}
```

---

### 基本的な通知操作

#### 表示

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string notificationJson = AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
{
    id = 1101,
    title = "エネルギー回復",
    message = "あなたの部隊は完全に回復しました。すぐに次のレイドへ戻れます。",
    tag = "energy",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    largeIcon = CreateUnityAppIconResource(),
    subText = "レイド準備完了",
    autoCancel = true,
    priority = 1,
    category = "recommendation",
    ticker = "Energy refilled",
    number = 3,
    style = new NotificationStylePayload
    {
        type = "bigText",
        bigText = "あなたの部隊は完全に回復しました。スタミナ上限があふれる前に次のレイドへ戻りましょう。",
        bigContentTitle = "エネルギー回復",
        summaryText = "レイド準備完了"
    }
});

AndroidNotificationManager.Instance.ShowNotification(notificationJson);
#endif
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ShowNotification.png" alt="Example_AndroidNotificationManager_ShowNotification" width="400" />
</p>

> **注意:** 通知ペイロードの `channel` フィールドは、チャンネルが存在しない場合に作成するために使用されます。既に作成済みのチャンネルには `id` と `name` のみ渡すことができます。

> **注意:** これらのコード例は、`AndroidNotificationManagerExampleController` と同じ `CreateGameplayChannelReference()` と `CreateUnityAppIconResource()` をそのまま使う形にそろえています。

#### 更新

同じ `id` / `tag` のペイロードを渡すと、表示中の通知を上書きします。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string updatedNotificationJson = AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
{
    id = 1101,
    title = "デイリー報酬の受け取り準備完了",
    message = "ログイン継続報酬のチェストが街で待っています。",
    tag = "energy",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    largeIcon = CreateUnityAppIconResource(),
    subText = "街の報酬",
    autoCancel = true,
    priority = 1,
    style = new NotificationStylePayload
    {
        type = "bigPicture",
        picture = CreateUnityAppIconResource(),
        largeIcon = CreateUnityAppIconResource(),
        hideExpandedLargeIcon = false,
        bigText = "ログイン継続報酬のチェストが街で待っています。今受け取って報酬倍率を維持しましょう。",
        bigContentTitle = "デイリー報酬の受け取り準備完了",
        summaryText = "街の報酬"
    }
});

AndroidNotificationManager.Instance.UpdateNotification(updatedNotificationJson);
#endif
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_UpdateNotification.png" alt="Example_AndroidNotificationManager_UpdateNotification" width="400" />
</p>

#### キャンセル

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// 特定の通知をキャンセル
AndroidNotificationManager.Instance.CancelNotification(1001);
AndroidNotificationManager.Instance.CancelNotification(1001, "energy");

// すべての通知をキャンセル
AndroidNotificationManager.Instance.CancelAllNotifications();
#endif
```

---

### 通知スタイル

通知ペイロードの `style` フィールドを設定します。

#### デフォルト

```csharp
// style フィールドなし — 標準の通知として表示されます。
```

#### BigText

展開時に長いテキストを表示します。

```csharp
style = new NotificationStylePayload
{
    type = "bigText",
    bigText = "展開時に表示される長い本文テキストです。",
    bigContentTitle = "展開時のタイトル",
    summaryText = "サマリー"
}
```

#### Inbox

展開時に複数行をリスト形式で表示します。`lines` に各行を配列で渡します。

```csharp
style = new NotificationStylePayload
{
    type = "inbox",
    lines = new[] { "項目 1", "項目 2", "項目 3" },
    bigContentTitle = "展開時のタイトル",
    summaryText = "3 件"
}
```

#### BigPicture

展開時に画像を表示します。`picture` に画像リソース参照を渡します。

```csharp
style = new NotificationStylePayload
{
    type = "bigPicture",
    picture = new NotificationResourcePayload { name = "my_image", type = "drawable" },
    bigContentTitle = "展開時のタイトル",
    summaryText = "画像の説明"
}
```

---

### カスタムビュースタイル

カスタム Android レイアウトを使用して通知を表示します。

#### DecoratedCustomView

折りたたみ・展開用のレイアウトリソース名を指定します。レイアウト XML は `Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/res/layout/` に配置します（リソース名の衝突を避けるため `nt_` プレフィックスを使用）。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string notificationJson = AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
{
    id = 1601,
    title = "カスタムレイアウト通知",
    message = "展開してカスタムビューを確認し、Dismiss を押してください。",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    autoCancel = true,
    style = new NotificationStylePayload
    {
        type = "decoratedCustomView",
        customViewLayout = "nt_notification_custom_view_sample",         // 折りたたみレイアウト
        bigCustomViewLayout = "nt_notification_custom_view_sample_expanded", // 展開レイアウト（任意）
        viewActions = new[]
        {
            new NotificationViewActionPayload
            {
                type = "setClickIntent",
                viewId = "nt_notification_btn_dismiss",  // レイアウト内のビュー ID
                actionId = "com.jonghyunkim.nativetoolkit.ACTION_CUSTOM_VIEW_DISMISS"  // NotificationActionTapped で受信
            }
        }
    }
});

AndroidNotificationManager.Instance.ShowNotification(notificationJson);
#endif
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ShowDecoratedCustomViewNotification.png" alt="Example_AndroidNotificationManager_ShowDecoratedCustomViewNotification" width="400" />
</p>

> **注意:** `RemoteViews` の制約により、クリック可能な要素には `Button` ではなく `LinearLayout` + `TextView` を使用してください。

---

### インタラクション

#### NotificationOperationCompleted イベント

各操作（表示、キャンセル、スケジュールなど）の完了後に発火します。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationOperationCompleted += OnOperationCompleted;
#endif

private void OnOperationCompleted(NotificationResult result)
{
    // result.Operation    — 例: AndroidNotificationManager.OperationShowNotification
    // result.IsSuccess    — 成功の場合 true
    // result.ErrorMessage — 失敗の場合に非 null
}
```

操作定数:

| 定数                                                                    | 説明                                     |
| ----------------------------------------------------------------------- | ---------------------------------------- |
| `AndroidNotificationManager.OperationShowNotification`                  | `ShowNotification` 完了                  |
| `AndroidNotificationManager.OperationUpdateNotification`                | `UpdateNotification` 完了                |
| `AndroidNotificationManager.OperationCancelNotification`                | `CancelNotification` 完了                |
| `AndroidNotificationManager.OperationCancelAllNotifications`            | `CancelAllNotifications` 完了            |
| `AndroidNotificationManager.OperationScheduleNotification`              | `ScheduleNotification` 完了              |
| `AndroidNotificationManager.OperationCancelScheduledNotification`       | `CancelScheduledNotification` 完了       |
| `AndroidNotificationManager.OperationCancelAllScheduledNotifications`   | `CancelAllScheduledNotifications` 完了   |
| `AndroidNotificationManager.OperationStartProgressForegroundService`    | `StartProgressForegroundService` 完了    |
| `AndroidNotificationManager.OperationUpdateProgressForegroundService`   | `UpdateProgressForegroundService` 完了   |
| `AndroidNotificationManager.OperationCompleteProgressForegroundService` | `CompleteProgressForegroundService` 完了 |
| `AndroidNotificationManager.OperationStopProgressForegroundService`     | `StopProgressForegroundService` 完了     |

#### NotificationActionTapped イベント

ユーザーが通知本体、アクションボタン、または通知を削除したときに発火します。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationActionTapped += OnActionTapped;
#endif

private void OnActionTapped(NotificationActionResult result)
{
    bool isBodyTap = result.ActionId == AndroidNotificationManager.ActionBodyTap;
    bool isDismiss = result.ActionId == AndroidNotificationManager.ActionNotificationDismissed;

    // result.NotificationId — 通知 ID
    // result.ActionId       — アクション識別子
    // result.Data           — Dictionary<string, string> カスタムデータ（null の場合あり）
}
```

カスタムデータを送信するには、`data` エントリを追加して `AndroidNotificationJsonBuilder` に渡します:

```csharp
payload.data = new[]
{
    new NotificationDataEntryPayload { key = "screen", value = "battle" },
    new NotificationDataEntryPayload { key = "matchId", value = "match_5678" }
};

string json = AndroidNotificationJsonBuilder.BuildNotificationJson(payload);
```

#### NotificationReceived イベント

スケジュール通知がアプリのフォアグラウンド中に発火したときに受信します。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationReceived += OnNotificationReceived;
#endif

private void OnNotificationReceived(NotificationReceivedResult result)
{
    // result.NotificationId — 通知 ID
    // result.Tag            — タグ（null の場合あり）
    // result.ChannelId      — チャンネル ID
}
```

#### アクションボタン

通知にアクションボタンを追加します。

```csharp
NotificationPayload payload = new NotificationPayload
{
    id = 1401,
    title = "マッチ成立",
    message = "ランクマッチの準備ができました。30 秒以内に参加してください。",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    autoCancel = true,
    priority = 1,
    launchAction = "open_battle_screen",
    actions = new[]
    {
        new NotificationActionPayload
        {
            title = "今すぐプレイ",
            actionId = "com.jonghyunkim.nativetoolkit.ACTION_PLAY_NOW",
            icon = CreateUnityAppIconResource(),
            launchApp = true,
            showsUserInterface = true
        },
        new NotificationActionPayload
        {
            title = "閉じる",
            actionId = "com.jonghyunkim.nativetoolkit.ACTION_DISMISS",
            launchApp = false,
            showsUserInterface = false
        }
    },
    data = new[]
    {
        new NotificationDataEntryPayload { key = "screen", value = "battle" },
        new NotificationDataEntryPayload { key = "matchId", value = "match_5678" }
    }
};

string notificationJson = AndroidNotificationJsonBuilder.BuildNotificationJson(payload);
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ShowActionNotification.png" alt="Example_AndroidNotificationManager_ShowActionNotification" width="400" />
</p>

#### フルスクリーンインテント

デバイスがロック中またはスリープ中にフルスクリーンアクティビティを起動します（アラームや着信通知など）。`fullScreenIntent = true` を設定し、高優先度チャンネル（`importance = 4`）を使用します。

```csharp
new NotificationPayload
{
    id = 1501,
    title = "マッチ開始",
    message = "マッチが今すぐ始まります。ゲーム画面を起動します。",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    priority = 2,
    category = "call",
    fullScreenIntent = true,
    autoCancel = true
}
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ShowFullScreenNotification.png" alt="Example_AndroidNotificationManager_ShowFullScreenNotification" width="400" />
</p>

> **注意:** デバイスの状態や Android のポリシーによっては、フルスクリーンではなくヘッドアップ通知として表示される場合があります。

---

### プログレス通知

ダウンロードや長時間処理のプログレスバーを表示します。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// 確定的プログレスバーで開始
AndroidNotificationManager.Instance.StartProgressForegroundService(AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
{
    id = 1301,
    title = "ギルドバトル用アセットをダウンロード中",
    message = "次のマッチに向けてアリーナを準備しています。",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    ongoing = true,
    autoCancel = false,
    progress = new NotificationProgressPayload { max = 100, current = _currentProgressValue, indeterminate = false },
    style = new NotificationStylePayload
    {
        type = "bigText",
        bigText = "次のマッチに向けてアリーナを準備しています。アセットのダウンロードが終わるまでアプリを終了しないでください。",
        bigContentTitle = "ギルドバトル用アセットをダウンロード中",
        summaryText = "バックグラウンドダウンロード"
    }
}));

// プログレスを更新
AndroidNotificationManager.Instance.UpdateProgressForegroundService(AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
{
    id = 1301,
    title = "レジェンダリー装備を鍛造中",
    message = "鍛冶場が最大出力で稼働しています。",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    ongoing = true,
    autoCancel = false,
    onlyAlertOnce = true,
    progress = new NotificationProgressPayload { max = 100, current = progressValue, indeterminate = false },
    style = new NotificationStylePayload
    {
        type = "bigText",
        bigText = "鍛冶場が最大出力で稼働しています。完成したらすぐ装備できるよう準備してください。",
        bigContentTitle = "レジェンダリー装備を鍛造中",
        summaryText = "鍛造アップデート"
    }
}));

// 完了 — サービスを停止して通常の通知に降格
AndroidNotificationManager.Instance.CompleteProgressForegroundService(AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
{
    id = 1301,
    title = "鍛造完了",
    message = "レジェンダリーソードの受け取り準備ができました。",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    ongoing = false,
    autoCancel = true,
    progress = new NotificationProgressPayload { max = 100, current = 100, indeterminate = false },
    style = new NotificationStylePayload
    {
        type = "bigText",
        bigText = "レジェンダリーソードの受け取り準備ができました。次の戦闘前に鍛冶場へ戻って装備しましょう。",
        bigContentTitle = "鍛造完了",
        summaryText = "鍛造完了"
    }
}));

// 強制停止 — 通知も削除
AndroidNotificationManager.Instance.StopProgressForegroundService();
#endif
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ProgressNotification.png" alt="Example_AndroidNotificationManager_ProgressNotification" width="400" />
</p>

---

### フォアグラウンドサービス通知

フォアグラウンドサービス通知には以下のマニフェストエントリが必要です。

```xml
<service
    android:name="android.library.notification.presentation.progress.ProgressForegroundService"
    android:foregroundServiceType="dataSync"
    android:exported="false" />
```

`StartProgressForegroundService`、`UpdateProgressForegroundService`、`CompleteProgressForegroundService`、`StopProgressForegroundService` の使い方は[プログレス通知](#プログレス通知)を参照してください。

---

### スケジュール通知

指定した時刻に自動的に通知を表示します。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
long triggerTime = DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds();

string scheduleJson = AndroidNotificationJsonBuilder.BuildScheduledNotificationJson(new ScheduledNotificationEnvelopePayload
{
    notification = new NotificationPayload
    {
        id = 1201,
        title = "ギルドバトル間もなく開始",
        message = "チームキューが15秒後にオープンします。ギルドメンバーを集めて出撃準備をしてください。",
        tag = "guild-battle",
        channel = CreateGameplayChannelReference(),
        smallIcon = CreateUnityAppIconResource(),
        autoCancel = true,
        priority = 1,
        groupKey = "guild-events",
        sortKey = "001",
        style = new NotificationStylePayload
        {
            type = "bigText",
            bigText = "チームキューが15秒後にオープンします。ロードアウトを最終確認し、出撃準備を整えてください。",
            bigContentTitle = "ギルドバトル間もなく開始",
            summaryText = "ギルドイベント"
        }
    },
    schedule = new NotificationSchedulePayload
    {
        triggerAtMillis = triggerTime,
        exact = true,           // 正確なアラーム（SCHEDULE_EXACT_ALARM が必要）
        allowWhileIdle = true,  // Doze モード中でも発火
        persistAcrossBoot = true,
        alarmType = 0           // RTC_WAKEUP
    }
});

AndroidNotificationManager.Instance.ScheduleNotification(scheduleJson);
#endif
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ScheduleNotification.png" alt="Example_AndroidNotificationManager_ScheduleNotification" width="400" />
</p>

#### スケジュール通知をキャンセルする

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.CancelScheduledNotification(1201, "guild-battle");
AndroidNotificationManager.Instance.CancelAllScheduledNotifications();
#endif
```

#### スケジュール状態を確認する

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
bool isScheduled = AndroidNotificationManager.Instance.IsNotificationScheduled(1201, "guild-battle");
#endif
```

---

## iOS

（準備中）

---

## Windows

（準備中）

---

## macOS

（準備中）
