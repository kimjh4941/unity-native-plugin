# 알림 기능

언어:

- 한국어（이 페이지）
- English: [notification.md](notification.md)
- 日本語: [notification.ja.md](notification.ja.md)

← [매뉴얼 상단으로 돌아가기](index.ko.md)

---

## 목차

- [Android](#android)
  - [설정](#설정)
  - [권한](#권한)
  - [채널 관리](#채널-관리)
  - [기본 알림 작업](#기본-알림-작업)
  - [알림 스타일](#알림-스타일)
  - [커스텀 뷰 스타일](#커스텀-뷰-스타일)
  - [인터랙션](#인터랙션)
  - [진행 알림](#진행-알림)
  - [포그라운드 서비스 알림](#포그라운드-서비스-알림)
  - [예약 알림](#예약-알림)
- [iOS](#ios)
- [Windows](#windows)
- [macOS](#macos)

---

## Android

### 설정

#### AndroidManifest.xml

사용할 기능에 필요한 권한을 추가합니다.

```xml
<!-- Android 13 이상에서 알림 전송에 필요 -->
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />

<!-- 예약 알림(정확한 알람)에 필요 -->
<uses-permission android:name="android.permission.SCHEDULE_EXACT_ALARM" />
<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" />

<!-- 포그라운드 서비스에 필요 -->
<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_DATA_SYNC" />
```

#### 네임스페이스 임포트

```csharp
// 가드: Android (Player)만. Editor에서는 네이티브 호출을 피합니다.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Notification;
#endif
```

---

### 권한

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// 알림 권한이 부여되었는지 (Android 13+)
bool hasPermission = AndroidNotificationManager.Instance.HasPermission();

// 앱 알림이 활성화되었는지
bool enabled = AndroidNotificationManager.Instance.AreNotificationsEnabled();

// 정확한 알람 예약이 허용되었는지 (Android 12+)
bool canSchedule = AndroidNotificationManager.Instance.CanScheduleExactAlarms();

// POST_NOTIFICATIONS 권한 요청 (Android 13+)
AndroidNotificationManager.Instance.RequestPermission(granted =>
{
    if (granted) { /* 권한 허용됨 */ }
});

// 설정 화면 열기
AndroidNotificationManager.Instance.OpenNotificationSettings();
AndroidNotificationManager.Instance.OpenAppDetailsSettings();
AndroidNotificationManager.Instance.OpenExactAlarmSettings();
#endif
```

---

### 채널 관리

알림을 전송하기 전에 알림 채널을 생성해야 합니다.

#### 채널 생성

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string channelJson = JsonUtility.ToJson(new ChannelPayload
{
    id = "my_channel",
    name = "내 채널",
    importance = 3,             // 3 = DEFAULT
    description = "샘플 알림 채널",
    showBadge = true,
    enableLights = true,
    lightColor = unchecked((int)0xFF4CAF50),
    enableVibration = true,
    vibrationPattern = new long[] { 0, 250, 200, 250 },
    lockscreenVisibility = 1,   // 1 = PUBLIC
    groupId = "my_group",
    groupName = "내 그룹"
});

AndroidNotificationManager.Instance.CreateChannel(channelJson);
#endif
```

채널 중요도 레벨:

| 값 | 레벨 | 설명 |
|----|------|------|
| 1 | MIN | 소리 없음, 헤드업 없음 |
| 2 | LOW | 소리 없음 |
| 3 | DEFAULT | 소리 있음 |
| 4 | HIGH | 소리 있음 + 헤드업 |

#### 채널 삭제

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.DeleteChannel("my_channel");
#endif
```

#### 이벤트로 결과 받기

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationOperationCompleted += OnOperationCompleted;
#endif

private void OnOperationCompleted(NotificationResult result)
{
    // result.Operation    — 어떤 작업이 완료되었는지 나타냄
    // result.IsSuccess    — 성공 시 true
    // result.ErrorMessage — 실패 시 non-null
}
```

---

### 기본 알림 작업

#### 표시

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string notificationJson = JsonUtility.ToJson(new NotificationPayload
{
    id = 1001,
    title = "에너지 회복",
    message = "부대가 완전히 회복되었습니다.",
    tag = "energy",
    channel = new ChannelPayload { id = "my_channel", name = "내 채널", importance = 3 },
    subText = "레이드 준비 완료",
    autoCancel = true,
    priority = 1    // 1 = HIGH
});

AndroidNotificationManager.Instance.ShowNotification(notificationJson);
#endif
```

> **참고:** 알림 페이로드의 `channel` 필드는 채널이 없는 경우 생성하는 데 사용됩니다. 이미 생성된 채널에는 `id`와 `name`만 전달해도 됩니다.

#### 업데이트

동일한 `id` / `tag` 페이로드를 전달하면 현재 표시 중인 알림을 덮어씁니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.UpdateNotification(updatedNotificationJson);
#endif
```

#### 취소

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// 특정 알림 취소
AndroidNotificationManager.Instance.CancelNotification(1001);
AndroidNotificationManager.Instance.CancelNotification(1001, "energy");

// 모든 알림 취소
AndroidNotificationManager.Instance.CancelAllNotifications();
#endif
```

---

### 알림 스타일

알림 페이로드의 `style` 필드를 설정합니다.

#### 기본

```csharp
// style 필드 없음 — 표준 알림으로 표시됩니다.
```

#### BigText

확장 시 긴 텍스트를 표시합니다.

```csharp
style = new NotificationStylePayload
{
    type = "bigText",
    bigText = "확장 시 표시되는 긴 본문 텍스트입니다.",
    bigContentTitle = "확장 제목",
    summaryText = "요약"
}
```

#### Inbox

확장 시 여러 줄을 목록 형식으로 표시합니다. `bigText`에 줄바꿈으로 구분하여 내용을 전달합니다.

```csharp
style = new NotificationStylePayload
{
    type = "inbox",
    bigText = "• 항목 1\n• 항목 2\n• 항목 3",
    bigContentTitle = "확장 제목",
    summaryText = "3건"
}
```

#### BigPicture

확장 시 이미지를 표시합니다. `bigPictureResource`에 이미지 리소스 이름을 전달합니다.

```csharp
style = new NotificationStylePayload
{
    type = "bigPicture",
    bigPictureResource = "my_image",    // drawable 리소스 이름
    bigContentTitle = "확장 제목",
    summaryText = "이미지 설명"
}
```

---

### 커스텀 뷰 스타일

커스텀 Android 레이아웃을 사용하여 알림을 표시합니다.

#### DecoratedCustomView

접힘/펼침용 레이아웃 리소스 이름을 지정합니다. 레이아웃 XML은 `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/res/layout/`에 배치합니다（리소스 이름 충돌 방지를 위해 `nt_` 접두사 사용）.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string notificationJson = JsonUtility.ToJson(new NotificationPayload
{
    id = 1601,
    title = "커스텀 레이아웃 알림",
    message = "펼쳐서 커스텀 뷰를 확인하세요.",
    channel = new ChannelPayload { id = "my_channel", name = "내 채널", importance = 3 },
    autoCancel = true,
    style = new NotificationStylePayload
    {
        type = "decoratedCustomView",
        customViewLayout = "nt_notification_custom_view_sample",         // 접힘 레이아웃
        bigCustomViewLayout = "nt_notification_custom_view_sample_expanded", // 펼침 레이아웃 (선택)
        viewActions = new[]
        {
            new NotificationViewActionPayload
            {
                type = "setClickIntent",
                viewId = "nt_notification_btn_dismiss",  // 레이아웃 내 뷰 ID
                actionId = "com.example.ACTION_DISMISS"  // NotificationActionTapped에서 수신
            }
        }
    }
});

AndroidNotificationManager.Instance.ShowNotification(notificationJson);
#endif
```

> **참고:** `RemoteViews` 제약으로 인해 클릭 가능한 요소에는 `Button` 대신 `LinearLayout` + `TextView`를 사용하세요.

---

### 인터랙션

#### NotificationOperationCompleted 이벤트

각 작업（표시, 취소, 예약 등）완료 후 발생합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationOperationCompleted += OnOperationCompleted;
#endif

private void OnOperationCompleted(NotificationResult result)
{
    // result.Operation    — 예: AndroidNotificationManager.OperationShowNotification
    // result.IsSuccess    — 성공 시 true
    // result.ErrorMessage — 실패 시 non-null
}
```

작업 상수:

| 상수 | 설명 |
|------|------|
| `AndroidNotificationManager.OperationShowNotification` | `ShowNotification` 완료 |
| `AndroidNotificationManager.OperationUpdateNotification` | `UpdateNotification` 완료 |
| `AndroidNotificationManager.OperationCancelNotification` | `CancelNotification` 완료 |
| `AndroidNotificationManager.OperationCancelAllNotifications` | `CancelAllNotifications` 완료 |
| `AndroidNotificationManager.OperationScheduleNotification` | `ScheduleNotification` 완료 |
| `AndroidNotificationManager.OperationCancelScheduledNotification` | `CancelScheduledNotification` 완료 |
| `AndroidNotificationManager.OperationCancelAllScheduledNotifications` | `CancelAllScheduledNotifications` 완료 |
| `AndroidNotificationManager.OperationStartProgressForegroundService` | `StartProgressForegroundService` 완료 |
| `AndroidNotificationManager.OperationUpdateProgressForegroundService` | `UpdateProgressForegroundService` 완료 |
| `AndroidNotificationManager.OperationCompleteProgressForegroundService` | `CompleteProgressForegroundService` 완료 |
| `AndroidNotificationManager.OperationStopProgressForegroundService` | `StopProgressForegroundService` 완료 |

#### NotificationActionTapped 이벤트

사용자가 알림 본문, 액션 버튼을 탭하거나 알림을 삭제할 때 발생합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationActionTapped += OnActionTapped;
#endif

private void OnActionTapped(NotificationActionResult result)
{
    bool isBodyTap = result.ActionId == AndroidNotificationManager.ActionBodyTap;
    bool isDismiss = result.ActionId == AndroidNotificationManager.ActionNotificationDismissed;

    // result.NotificationId — 알림 ID
    // result.ActionId       — 액션 식별자
    // result.Data           — Dictionary<string, string> 커스텀 데이터 (null일 수 있음)
}
```

커스텀 데이터를 전송하려면 JSON에 `data` 필드를 수동으로 주입합니다（JsonUtility는 Dictionary 직렬화를 지원하지 않음）:

```csharp
string json = JsonUtility.ToJson(payload);
// 닫는 중괄호를 제거하고 데이터를 주입
json = json[..^1] + ",\"data\":{\"screen\":\"battle\",\"matchId\":\"match_5678\"}}";
```

#### NotificationReceived 이벤트

예약 알림이 앱 포그라운드 중에 발생했을 때 수신합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationReceived += OnNotificationReceived;
#endif

private void OnNotificationReceived(NotificationReceivedResult result)
{
    // result.NotificationId — 알림 ID
    // result.Tag            — 태그 (null일 수 있음)
    // result.ChannelId      — 채널 ID
}
```

#### 액션 버튼

알림에 액션 버튼을 추가합니다.

```csharp
actions = new[]
{
    new NotificationActionPayload
    {
        title = "지금 플레이",
        actionId = "com.example.ACTION_PLAY_NOW",
        launchApp = true,
        showsUserInterface = true
    },
    new NotificationActionPayload
    {
        title = "닫기",
        actionId = "com.example.ACTION_DISMISS",
        launchApp = false,
        showsUserInterface = false
    }
}
```

#### 전체 화면 인텐트

기기가 잠겨 있거나 화면이 꺼져 있을 때 전체 화면 액티비티를 실행합니다（알람, 수신 통화 등）. `fullScreenIntent = true`를 설정하고 높은 우선순위 채널（`importance = 4`）을 사용합니다.

```csharp
new NotificationPayload
{
    id = 1501,
    title = "매치 시작",
    message = "지금 바로 매치가 시작됩니다.",
    channel = new ChannelPayload { id = "high_channel", name = "높은 우선순위", importance = 4 },
    priority = 2,
    fullScreenIntent = true,
    autoCancel = true
}
```

> **참고:** 기기 상태 및 Android 정책에 따라 전체 화면이 아닌 헤드업 알림으로 표시될 수 있습니다.

---

### 진행 알림

다운로드나 오래 걸리는 작업의 진행 바를 표시합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// 결정적 진행 바로 시작
AndroidNotificationManager.Instance.StartProgressForegroundService(JsonUtility.ToJson(new NotificationPayload
{
    id = 1301,
    title = "에셋 다운로드 중",
    message = "15% 완료",
    channel = new ChannelPayload { id = "my_channel", name = "내 채널", importance = 3 },
    ongoing = true,
    autoCancel = false,
    progress = new NotificationProgressPayload { max = 100, current = 15, indeterminate = false }
}));

// 진행 업데이트
AndroidNotificationManager.Instance.UpdateProgressForegroundService(JsonUtility.ToJson(new NotificationPayload
{
    id = 1301,
    title = "에셋 다운로드 중",
    message = "50% 완료",
    channel = new ChannelPayload { id = "my_channel", name = "내 채널", importance = 3 },
    ongoing = true,
    autoCancel = false,
    onlyAlertOnce = true,
    progress = new NotificationProgressPayload { max = 100, current = 50, indeterminate = false }
}));

// 완료 — 서비스 중지 및 일반 알림으로 강등
AndroidNotificationManager.Instance.CompleteProgressForegroundService(JsonUtility.ToJson(new NotificationPayload
{
    id = 1301,
    title = "다운로드 완료",
    message = "에셋이 준비되었습니다.",
    channel = new ChannelPayload { id = "my_channel", name = "내 채널", importance = 3 },
    ongoing = false,
    autoCancel = true,
    progress = new NotificationProgressPayload { max = 100, current = 100, indeterminate = false }
}));

// 강제 중지 — 알림도 제거
AndroidNotificationManager.Instance.StopProgressForegroundService();
#endif
```

---

### 포그라운드 서비스 알림

포그라운드 서비스 알림에는 다음 매니페스트 항목이 필요합니다.

```xml
<service
    android:name="android.library.notification.presentation.progress.ProgressForegroundService"
    android:foregroundServiceType="dataSync"
    android:exported="false" />
```

`StartProgressForegroundService`, `UpdateProgressForegroundService`, `CompleteProgressForegroundService`, `StopProgressForegroundService`의 사용법은 [진행 알림](#진행-알림)을 참조하세요.

---

### 예약 알림

지정한 시간에 자동으로 알림을 표시합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
long triggerTime = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();

string scheduleJson = JsonUtility.ToJson(new ScheduledNotificationEnvelopePayload
{
    notification = new NotificationPayload
    {
        id = 1201,
        title = "길드 배틀 곧 시작",
        message = "팀 큐가 1분 후에 열립니다.",
        tag = "guild-battle",
        channel = new ChannelPayload { id = "my_channel", name = "내 채널", importance = 3 },
        autoCancel = true,
        priority = 1
    },
    schedule = new NotificationSchedulePayload
    {
        triggerAtMillis = triggerTime,
        exact = true,           // 정확한 알람 (SCHEDULE_EXACT_ALARM 필요)
        allowWhileIdle = true,  // Doze 모드에서도 발생
        persistAcrossBoot = true // 기기 재부팅 후 복원
    }
});

AndroidNotificationManager.Instance.ScheduleNotification(scheduleJson);
#endif
```

#### 예약 알림 취소

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.CancelScheduledNotification(1201, "guild-battle");
AndroidNotificationManager.Instance.CancelAllScheduledNotifications();
#endif
```

#### 예약 상태 확인

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
bool isScheduled = AndroidNotificationManager.Instance.IsNotificationScheduled(1201, "guild-battle");
#endif
```

---

## iOS

（준비 중）

---

## Windows

（준비 중）

---

## macOS

（준비 중）
