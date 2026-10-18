# 알림 기능

언어:

- 한국어（이 페이지）
- English: [notification.md](notification.md)
- 日本語: [notification.ja.md](notification.ja.md)

← [매뉴얼 상단으로 돌아가기](index.ko.md)

---

## 목차

- [Android](#android)
  - [1.13.0 변경 사항](#1130-변경-사항)
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
  - [개요](#개요)
  - [지원 기능](#지원-기능)
    - [기본 설정](#기본-설정)
    - [즉시 알림 표시](#즉시-알림-표시)
    - [앱 아이콘 첨부 즉시 알림](#앱-아이콘-첨부-즉시-알림)
    - [예약 알림 등록](#예약-알림-등록)
    - [카테고리와 액션 등록](#카테고리와-액션-등록)
    - [이벤트 수신](#이벤트-수신)
- [Windows](#windows)
  - [1.12.0 변경 사항](#1120-변경-사항)
  - [설정](#설정-1)
  - [초기화](#초기화)
  - [알림 표시](#알림-표시)
  - [예약 알림](#예약-알림-2)
  - [진행률 업데이트](#진행률-업데이트)
  - [알림 제거](#알림-제거)
  - [쿼리](#쿼리)
  - [이벤트 수신](#이벤트-수신-1)
- [macOS](#macos)
  - [지원 기능](#지원-기능-1)
  - [기본 설정](#기본-설정-1)
  - [권한](#권한-1)
  - [알림 표시](#알림-표시)
  - [업데이트, 취소 및 삭제](#업데이트-취소-및-삭제)
  - [예약 알림](#예약-알림-1)
  - [조회](#조회)
  - [배지](#배지)
  - [카테고리와 액션](#카테고리와-액션)
  - [이벤트 수신](#이벤트-수신-1)

---

## Android

### 1.13.0 변경 사항

1.13.0부터 Android 알림은 1.x의 Kotlin 브리지 대신 native-toolkit의 C ABI 2.0.0으로 동작합니다. `AndroidNotificationManager`의 메서드, 이벤트, 상수는 바뀌지 않았고, 지금도 `AndroidNotificationJsonBuilder`로 만든 JSON을 받아 1.12.0과 같은 규칙으로 읽습니다. 고쳐야 할 것과 1.12.0과 다른 점은 다음과 같습니다.

#### 코드 수정 방법

- Android 코드의 `using JonghyunKim.NativeToolkit.Runtime.Notification;`를 `using JonghyunKim.NativeToolkit.Runtime.Android.Notification;`로 바꿔 주세요. iOS와 macOS 코드의 줄은 그대로 두세요(해당 타입은 옮겨지지 않았습니다).
- 아래 표의 타입 이름을 바꿔 주세요. Unity는 자동으로 고쳐 주지 않습니다. API Updater는 이 패키지의 타입에 대한 참조를 다시 쓰지 않으므로, 1.12.0의 이름을 그대로 쓰는 스크립트는 컴파일에 실패합니다. 이 페이지의 예처럼 `#if UNITY_ANDROID && !UNITY_EDITOR` 안에 작성한 코드는 Editor에서 컴파일되지 않으므로, 오류는 Android Player를 빌드할 때 처음 나타납니다.
- 1.12.0의 이름으로 `[SerializeReference]`에 저장한 데이터는 새 타입으로 값과 함께 불러와집니다. 이름이 바뀐 타입은 1.12.0의 이름을 `[MovedFrom]`으로 기록하고 있습니다.

이름이 바뀐 타입입니다. `AndroidNotificationManager`와 `AndroidNotificationJsonBuilder`는 이름이 그대로이고 네임스페이스만 옮겨졌습니다.

| 1.12.0 | 1.13.0 |
| --- | --- |
| `NotificationResult` | `AndroidNotificationResult` |
| `NotificationActionResult` | `AndroidNotificationActionResult` |
| `NotificationReceivedResult` | `AndroidNotificationReceivedResult` |
| `NotificationResourcePayload` | `AndroidNotificationResourcePayload` |
| `ChannelPayload` | `AndroidChannelPayload` |
| `NotificationPayload` | `AndroidNotificationPayload` |
| `NotificationActionPayload` | `AndroidNotificationActionPayload` |
| `ScheduledNotificationEnvelopePayload` | `AndroidScheduledNotificationEnvelopePayload` |
| `NotificationSchedulePayload` | `AndroidNotificationSchedulePayload` |
| `NotificationProgressPayload` | `AndroidNotificationProgressPayload` |
| `NotificationStylePayload` | `AndroidNotificationStylePayload` |
| `NotificationMessagePayload` | `AndroidNotificationMessagePayload` |
| `NotificationDataEntryPayload` | `AndroidNotificationDataEntryPayload` |
| `NotificationViewActionPayload` | `AndroidNotificationViewActionPayload` |

#### Android 전체 기능에 공통인 호환성이 깨지는 변경

- **1.x 네이티브 라이브러리를 더 이상 포함하지 않습니다.** `android-native-toolkit-1.3.0.aar`와 `unity-android-native-toolkit-1.3.0.aar`를 제거했습니다. 패키지에 포함된 것은 `android-native-toolkit-2.0.0.aar`와 `android-native-toolkit-capi-2.0.0.aar`(native-toolkit의 C ABI 2.0.0)입니다. 이 라이브러리의 클래스(`android.unity.*`, `android.library.*`)를 직접 호출하던 코드는 동작하지 않습니다.
- **"Configure Gradle Project"가 없어졌습니다.** 메뉴 Tools > Native Toolkit > Android > Configure Gradle Project와 그 창을 삭제했습니다. 실행 시 필요한 라이브러리(`kotlin-stdlib` 2.2.21, `kotlinx-coroutines-core` 1.9.0을 포함한 7개)는 빌드가 `unityLibrary/build.gradle`에 직접 추가합니다. Kotlin Gradle 플러그인은 더 이상 적용하지 않습니다. 직접 만든 Gradle 템플릿이나 EDM4U를 사용한다면 버전이 충돌하지 않는지 확인해 주세요.
- **64비트와 IL2CPP만 지원합니다.** ARMv7(32비트)도 포함하는 빌드는 메시지를 표시하고 중단됩니다. Mono는 지원하지 않습니다. [Android 요구 사항](index.ko.md#android-요구-사항)을 참고해 주세요.

#### 알림 변경 사항

- **1.12.0을 위해 추가한 `<service>` 줄을 지워 주세요.** 1.12.0 매뉴얼은 `AndroidManifest.xml`에 `<service android:name="android.library.notification.presentation.progress.ProgressForegroundService" ... />`를 추가하도록 안내했습니다. 이 클래스는 1.x 라이브러리에 있던 것으로 지금은 없습니다. 이 줄을 지워 주세요. 포그라운드 서비스는 라이브러리가 자체 매니페스트에서 선언하므로, 진행 알림을 위해 앱의 매니페스트에 적을 것은 없습니다.
- **권한이 없을 때의 알림 표시는 실패가 됩니다.** `POST_NOTIFICATIONS` 권한이 없을 때(Android 13 이상)나 앱의 알림이 꺼져 있을 때, `ShowNotification`, `UpdateNotification`, 이미 지난 시각으로의 `ScheduleNotification`은 `NotificationOperationCompleted`로 실패를 알립니다. 1.12.0은 성공을 알리고 아무것도 표시하지 않았습니다. 먼저 `HasPermission()`과 `AreNotificationsEnabled()`를 확인하거나 `RequestPermission`을 호출해 주세요.
- **찾을 수 없는 리소스 이름은 실패가 됩니다.** 아이콘, 이미지, 레이아웃, 뷰 ID는 이름으로 지정하며, 알림을 표시·업데이트·예약할 때나 진행 메서드에 전달할 때 찾습니다. 앱에 없는 이름은 그 호출을 실패시키고 아무것도 표시하지 않습니다. 1.12.0은 앱 아이콘이나 기본 스타일로 대신했습니다. `AndroidNotificationResourcePayload`의 `type`은 더 이상 사용하지 않습니다(아이콘과 이미지는 `drawable`, 그다음 `mipmap`에서 찾습니다). 이름으로만 참조되는 리소스는 리소스 축소로 제거될 수 있으므로 `tools:keep`으로 유지해 주세요([이름으로 지정하는 리소스 유지](#이름으로-지정하는-리소스-유지) 참고).
- **1.12.0이 저장한 예약은 지워집니다.** 1.13.0으로 앱이 처음 시작될 때, 라이브러리는 1.12.0이 저장한 예약을 읽을 수 없으므로 그 알람을 취소하고 한 번만 삭제합니다. 업데이트 후에 다시 예약해 주세요. `persistAcrossBoot = false`로 만든 1.12.0의 예약은 찾을 수 없습니다. Android에 남지만 발생해도 아무것도 표시하지 않습니다.
- **1.12.0이 표시한 알림은 반응하지 않습니다.** 업데이트 전에 표시된 알림은 알림 창에 남지만, 업데이트 후에는 본문을 탭하거나 버튼을 눌러도 아무 일도 일어나지 않습니다(탭을 받던 클래스가 없어졌기 때문입니다).
- **`IsNotificationScheduled`가 보는 것은 재부팅 후에도 남는 예약뿐입니다.** 1.12.0과 마찬가지로 `IsNotificationScheduled`와 `CancelAllScheduledNotifications`는 `persistAcrossBoot = true`(기본값)로 만든 예약만 찾습니다. 그 밖의 예약은 `CancelScheduledNotification`에 ID를 지정해 취소해 주세요.

#### 1.12.0과의 동작 차이

`{op}`는 `showNotification` 같은 작업 이름입니다.

| 상황 | 1.12.0 | 1.13.0 |
| --- | --- | --- |
| 알림 권한이 없거나 앱의 알림이 꺼져 있을 때의 표시, 업데이트, 이미 지난 시각으로의 예약 | 성공(아무것도 표시하지 않음) | 실패: `Failed to {op}: notifications are not allowed for this app (permission or app notification setting).` |
| 앱에 없는 리소스 이름(아이콘, 이미지, 레이아웃, 뷰 ID) | 앱 아이콘, 항목 생략, 기본 스타일로 대신함 | 실패: `Failed to {op}: a resource named in the notification was not found.` 아무것도 표시하지 않음 |
| 리소스의 `type` | 그 종류에서만 찾음 | 사용하지 않음. `drawable`, 그다음 `mipmap`(레이아웃은 `layout`, 뷰 ID는 `id`) |
| 플랫폼 범위를 벗어난 값(`groupAlertBehavior`, `timestamp`, `timeoutAfter`, `semanticAction`, `alarmType`, 진동의 음수 값, 메시지의 음수 시각) | 검사 없이 전달 | `Invalid argument for {op}: Please verify the input parameters.` |
| JSON 구문 오류, `null`이나 빈 JSON | `Failed to {op}:`와 JSON 파서의 메시지 | `Failed to {op}: the JSON could not be read.` |
| 느슨한 JSON(따옴표 없는 키, 작은따옴표, 주석) | 받아들임 | 구문 오류 |
| 타입이 다른 JSON 값(문자열로 쓴 숫자 등) | 변환함 | 없는 것으로 취급. 정수 항목의 소수는 0 방향으로 버림 |
| 문자열이 아닌 `data` 값, `lines` 안의 `null` | 문자열로 바꿈(`null`은 `"null"`) | 버림 |
| `AndroidNotificationActionResult.Data`의 값 | JSON 이스케이프가 남음(`a\"b`, `c\/d`) | 보낸 그대로의 값 |
| 본문 탭이나 `launchApp` 액션 | 앱이 앞으로 나오지 않음(Android 16에서 확인) | 앱이 열림 |
| 탭으로 앱을 여는 Intent의 action | 액션 ID | 없음(런처 Intent) |
| 앱을 여는 Intent의 플래그(탭과 전체 화면 인텐트) | `NEW_TASK \| CLEAR_TOP` | `NEW_TASK \| RESET_TASK_IF_NEEDED` |
| Manager가 없는 동안의 탭(앱 시작 전 포함) | 버림 | 32개까지 보관하고, Manager를 처음 만든 뒤의 프레임에서 전달. `Instance`를 처음 읽은 프레임에서 `NotificationActionTapped`를 구독 |
| Unity 메인 스레드가 아닌 곳에서의 호출 | `{op} could not be started.` | 받아들임(`RequestPermission`과 `Instance`의 첫 읽기는 여전히 메인 스레드에서) |
| `OpenNotificationSettings` | 앱 상세 화면으로 넘어감 | 앱의 알림 설정 화면을 엶 |
| 앱이 백그라운드에 있을 때 설정 화면 열기 | 다른 스레드에서 호출하면(백그라운드에서는 Unity의 메인 스레드가 멈춰 있음) `{op} could not be started.`로 실패했음 | `Failed to {op}: the app is not in the foreground.` |
| 앱이 백그라운드에 있을 때의 진행 메서드 | 시스템 예외 메시지로 실패 | `Failed to {op}: the system did not allow the foreground service from the background.` |
| `DeleteChannel(null)` | `NullPointerException` 메시지로 실패 | 성공(채널 `""`를 삭제) |
| 정확한 알람 권한이 없어 부정확하게 바꾼 예약의 재부팅 후 | 권한이 있으면 정확하게 되돌림 | 부정확한 채로 남음 |
| Manager를 파괴하기 전후에 도착하는 결과와 이벤트 | 파괴한 Manager에서 발생 | 그 시점에 살아 있는 Manager에서 발생하고, 없으면 버림 |
| 1.12.0이 저장한 예약 | 남음 | 첫 시작 때 한 번만 취소하고 삭제 |
| 1.12.0이 표시한 알림 | 탭과 버튼에 반응 | 반응하지 않음 |
| Editor에서의 타입 | 빌드 타깃이 Android일 때만 있음 | 모든 빌드 타깃에서 있음(Editor에서는 여전히 작업이 아무 일도 하지 않고, 시작할 수 없었다고 알림) |

바뀌지 않은 것: 메서드, 이벤트, `Operation*`와 `Action*` 상수, JSON의 형태. 정확한 알람 권한이 없을 때 정확한 예약을 부정확하게 바꾸는 것. 1.12.0의 JSON 보정(예: 0~4 밖의 `importance`는 3으로, `priority`는 -2~2로 맞춤).

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
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;
#endif
```

> **참고:** `AndroidChannelPayload`, `AndroidNotificationPayload`, `AndroidNotificationJsonBuilder` 는 런타임 패키지에 포함되어 있습니다. optional 필드와 `data` 를 포함한 spec 정합 JSON 이 필요하면 `JsonUtility.ToJson(...)` 대신 `AndroidNotificationJsonBuilder` 를 사용하세요.

#### 이름으로 지정하는 리소스 유지

알림의 아이콘, 이미지, 레이아웃, 뷰 ID는 이름으로 지정하며, 알림을 표시·업데이트·예약할 때나 진행 메서드에 전달할 때 찾습니다. 아이콘과 이미지는 `drawable`, 그다음 `mipmap`, 레이아웃은 `layout`, 뷰 ID는 `id`에서 찾습니다. `AndroidNotificationResourcePayload`의 `type`은 사용하지 않습니다. 앱에 없는 이름은 그 호출을 `Failed to {op}: a resource named in the notification was not found.`로 실패시키고 아무것도 표시하지 않습니다.

리소스 축소는 이름으로만 참조되는 리소스를 제거합니다. 빌드에서 리소스를 축소한다면 `tools:keep`으로 유지해 주세요. 예를 들어 Android 라이브러리 폴더(`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/` 등)의 `res/raw/keep.xml`에 적습니다.

```xml
<resources xmlns:tools="http://schemas.android.com/tools"
    tools:keep="@drawable/my_image,@layout/nt_notification_custom_view_sample,@layout/nt_notification_custom_view_sample_expanded" />
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
string channelJson = AndroidNotificationJsonBuilder.BuildChannelJson(new AndroidChannelPayload
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

| 값  | 레벨    | 설명                   |
| --- | ------- | ---------------------- |
| 1   | MIN     | 소리 없음, 헤드업 없음 |
| 2   | LOW     | 소리 없음              |
| 3   | DEFAULT | 소리 있음              |
| 4   | HIGH    | 소리 있음 + 헤드업     |

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

private void OnOperationCompleted(AndroidNotificationResult result)
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
string notificationJson = AndroidNotificationJsonBuilder.BuildNotificationJson(new AndroidNotificationPayload
{
    id = 1101,
    title = "에너지 회복",
    message = "부대가 완전히 회복되었습니다. 바로 다음 레이드에 복귀할 수 있습니다.",
    tag = "energy",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    largeIcon = CreateUnityAppIconResource(),
    subText = "레이드 준비 완료",
    autoCancel = true,
    priority = 1,
    category = "recommendation",
    ticker = "Energy refilled",
    number = 3,
    style = new AndroidNotificationStylePayload
    {
        type = "bigText",
        bigText = "부대가 완전히 회복되었습니다. 스태미나가 넘치기 전에 다음 레이드로 복귀하세요.",
        bigContentTitle = "에너지 회복",
        summaryText = "레이드 준비 완료"
    }
});

AndroidNotificationManager.Instance.ShowNotification(notificationJson);
#endif
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ShowNotification.png" alt="Example_AndroidNotificationManager_ShowNotification" width="400" />
</p>

> **참고:** 알림 페이로드의 `channel` 필드는 채널이 없는 경우 생성하는 데 사용됩니다. 이미 생성된 채널에는 `id`와 `name`만 전달해도 됩니다.

> **참고:** 이 코드 예제들은 `AndroidNotificationManagerExampleController` 와 동일하게 `CreateGameplayChannelReference()` 와 `CreateUnityAppIconResource()` 를 사용하도록 맞췄습니다.

> **참고:** `POST_NOTIFICATIONS` 권한이 없을 때(Android 13 이상)나 앱의 알림이 꺼져 있을 때 `ShowNotification`과 `UpdateNotification`은 실패하고, `NotificationOperationCompleted`에 `Failed to {op}: notifications are not allowed for this app (permission or app notification setting).`가 전달됩니다. 먼저 `HasPermission()`과 `AreNotificationsEnabled()`를 확인해 주세요.

#### 업데이트

동일한 `id` / `tag` 페이로드를 전달하면 현재 표시 중인 알림을 덮어씁니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string updatedNotificationJson = AndroidNotificationJsonBuilder.BuildNotificationJson(new AndroidNotificationPayload
{
    id = 1101,
    title = "일일 보상 준비 완료",
    message = "로그인 연속 보상 상자가 마을에서 기다리고 있습니다.",
    tag = "energy",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    largeIcon = CreateUnityAppIconResource(),
    subText = "마을 보상",
    autoCancel = true,
    priority = 1,
    style = new AndroidNotificationStylePayload
    {
        type = "bigPicture",
        picture = CreateUnityAppIconResource(),
        largeIcon = CreateUnityAppIconResource(),
        hideExpandedLargeIcon = false,
        bigText = "로그인 연속 보상 상자가 마을에서 기다리고 있습니다. 지금 받아서 보상 배율을 유지하세요.",
        bigContentTitle = "일일 보상 준비 완료",
        summaryText = "마을 보상"
    }
});

AndroidNotificationManager.Instance.UpdateNotification(updatedNotificationJson);
#endif
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_UpdateNotification.png" alt="Example_AndroidNotificationManager_UpdateNotification" width="400" />
</p>

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
style = new AndroidNotificationStylePayload
{
    type = "bigText",
    bigText = "확장 시 표시되는 긴 본문 텍스트입니다.",
    bigContentTitle = "확장 제목",
    summaryText = "요약"
}
```

#### Inbox

확장 시 여러 줄을 목록 형식으로 표시합니다. `lines` 배열에 각 줄을 전달합니다.

```csharp
style = new AndroidNotificationStylePayload
{
    type = "inbox",
    lines = new[] { "항목 1", "항목 2", "항목 3" },
    bigContentTitle = "확장 제목",
    summaryText = "3건"
}
```

#### BigPicture

확장 시 이미지를 표시합니다. `picture` 에 이미지 리소스 참조를 전달합니다.

```csharp
style = new AndroidNotificationStylePayload
{
    type = "bigPicture",
    picture = new AndroidNotificationResourcePayload { name = "my_image", type = "drawable" },
    bigContentTitle = "확장 제목",
    summaryText = "이미지 설명"
}
```

---

### 커스텀 뷰 스타일

커스텀 Android 레이아웃을 사용하여 알림을 표시합니다.

#### DecoratedCustomView

접힘/펼침용 레이아웃 리소스 이름을 지정합니다. 레이아웃 XML은 `Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/res/layout/`에 배치합니다（리소스 이름 충돌 방지를 위해 `nt_` 접두사 사용）.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string notificationJson = AndroidNotificationJsonBuilder.BuildNotificationJson(new AndroidNotificationPayload
{
    id = 1601,
    title = "커스텀 레이아웃 알림",
    message = "펼쳐서 커스텀 뷰를 확인하고 Dismiss 를 누르세요.",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    autoCancel = true,
    style = new AndroidNotificationStylePayload
    {
        type = "decoratedCustomView",
        customViewLayout = "nt_notification_custom_view_sample",         // 접힘 레이아웃
        bigCustomViewLayout = "nt_notification_custom_view_sample_expanded", // 펼침 레이아웃 (선택)
        viewActions = new[]
        {
            new AndroidNotificationViewActionPayload
            {
                type = "setClickIntent",
                viewId = "nt_notification_btn_dismiss",  // 레이아웃 내 뷰 ID
                actionId = "com.jonghyunkim.nativetoolkit.ACTION_CUSTOM_VIEW_DISMISS"  // NotificationActionTapped에서 수신
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

> **참고:** `RemoteViews` 제약으로 인해 클릭 가능한 요소에는 `Button` 대신 `LinearLayout` + `TextView`를 사용하세요.

---

### 인터랙션

#### NotificationOperationCompleted 이벤트

각 작업（표시, 취소, 예약 등）완료 후 발생합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationOperationCompleted += OnOperationCompleted;
#endif

private void OnOperationCompleted(AndroidNotificationResult result)
{
    // result.Operation    — 예: AndroidNotificationManager.OperationShowNotification
    // result.IsSuccess    — 성공 시 true
    // result.ErrorMessage — 실패 시 non-null
}
```

작업 상수:

| 상수                                                                    | 설명                                     |
| ----------------------------------------------------------------------- | ---------------------------------------- |
| `AndroidNotificationManager.OperationShowNotification`                  | `ShowNotification` 완료                  |
| `AndroidNotificationManager.OperationUpdateNotification`                | `UpdateNotification` 완료                |
| `AndroidNotificationManager.OperationCancelNotification`                | `CancelNotification` 완료                |
| `AndroidNotificationManager.OperationCancelAllNotifications`            | `CancelAllNotifications` 완료            |
| `AndroidNotificationManager.OperationScheduleNotification`              | `ScheduleNotification` 완료              |
| `AndroidNotificationManager.OperationCancelScheduledNotification`       | `CancelScheduledNotification` 완료       |
| `AndroidNotificationManager.OperationCancelAllScheduledNotifications`   | `CancelAllScheduledNotifications` 완료   |
| `AndroidNotificationManager.OperationStartProgressForegroundService`    | `StartProgressForegroundService` 완료    |
| `AndroidNotificationManager.OperationUpdateProgressForegroundService`   | `UpdateProgressForegroundService` 완료   |
| `AndroidNotificationManager.OperationCompleteProgressForegroundService` | `CompleteProgressForegroundService` 완료 |
| `AndroidNotificationManager.OperationStopProgressForegroundService`     | `StopProgressForegroundService` 완료     |

#### NotificationActionTapped 이벤트

사용자가 알림 본문, 액션 버튼을 탭하거나 알림을 삭제할 때 발생합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationActionTapped += OnActionTapped;
#endif

private void OnActionTapped(AndroidNotificationActionResult result)
{
    bool isBodyTap = result.ActionId == AndroidNotificationManager.ActionBodyTap;
    bool isDismiss = result.ActionId == AndroidNotificationManager.ActionNotificationDismissed;

    // result.NotificationId — 알림 ID
    // result.ActionId       — 액션 식별자
    // result.Data           — Dictionary<string, string> 커스텀 데이터 (null일 수 있음)
}
```

커스텀 데이터를 전송하려면 `data` 엔트리를 설정한 뒤 `AndroidNotificationJsonBuilder` 를 사용합니다:

```csharp
payload.data = new[]
{
    new AndroidNotificationDataEntryPayload { key = "screen", value = "battle" },
    new AndroidNotificationDataEntryPayload { key = "matchId", value = "match_5678" }
};

string json = AndroidNotificationJsonBuilder.BuildNotificationJson(payload);
```

#### NotificationReceived 이벤트

예약 알림이 앱 포그라운드 중에 발생했을 때 수신합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidNotificationManager.Instance.NotificationReceived += OnNotificationReceived;
#endif

private void OnNotificationReceived(AndroidNotificationReceivedResult result)
{
    // result.NotificationId — 알림 ID
    // result.Tag            — 태그 (null일 수 있음)
    // result.ChannelId      — 채널 ID
}
```

#### 액션 버튼

알림에 액션 버튼을 추가합니다.

```csharp
AndroidNotificationPayload payload = new AndroidNotificationPayload
{
    id = 1401,
    title = "매치 발견",
    message = "랭크 매치가 준비되었습니다. 30초 안에 수락하세요.",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    autoCancel = true,
    priority = 1,
    launchAction = "open_battle_screen",
    actions = new[]
    {
        new AndroidNotificationActionPayload
        {
            title = "지금 플레이",
            actionId = "com.jonghyunkim.nativetoolkit.ACTION_PLAY_NOW",
            icon = CreateUnityAppIconResource(),
            launchApp = true,
            showsUserInterface = true
        },
        new AndroidNotificationActionPayload
        {
            title = "닫기",
            actionId = "com.jonghyunkim.nativetoolkit.ACTION_DISMISS",
            launchApp = false,
            showsUserInterface = false
        }
    },
    data = new[]
    {
        new AndroidNotificationDataEntryPayload { key = "screen", value = "battle" },
        new AndroidNotificationDataEntryPayload { key = "matchId", value = "match_5678" }
    }
};

string notificationJson = AndroidNotificationJsonBuilder.BuildNotificationJson(payload);
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ShowActionNotification.png" alt="Example_AndroidNotificationManager_ShowActionNotification" width="400" />
</p>

#### 전체 화면 인텐트

기기가 잠겨 있거나 화면이 꺼져 있을 때 전체 화면 액티비티를 실행합니다（알람, 수신 통화 등）. `fullScreenIntent = true`를 설정하고 높은 우선순위 채널（`importance = 4`）을 사용합니다.

```csharp
new AndroidNotificationPayload
{
    id = 1501,
    title = "매치 시작",
    message = "지금 바로 매치가 시작됩니다. 게임 화면을 실행합니다.",
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

> **참고:** 기기 상태 및 Android 정책에 따라 전체 화면이 아닌 헤드업 알림으로 표시될 수 있습니다.

---

### 진행 알림

다운로드나 오래 걸리는 작업의 진행 바를 표시합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// 결정적 진행 바로 시작
AndroidNotificationManager.Instance.StartProgressForegroundService(AndroidNotificationJsonBuilder.BuildNotificationJson(new AndroidNotificationPayload
{
    id = 1301,
    title = "길드 배틀 에셋 다운로드 중",
    message = "다음 매치를 위해 경기장을 준비하고 있습니다.",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    ongoing = true,
    autoCancel = false,
    progress = new AndroidNotificationProgressPayload { max = 100, current = _currentProgressValue, indeterminate = false },
    style = new AndroidNotificationStylePayload
    {
        type = "bigText",
        bigText = "다음 매치를 위해 경기장을 준비하고 있습니다. 에셋 다운로드가 끝날 때까지 앱을 종료하지 마세요.",
        bigContentTitle = "길드 배틀 에셋 다운로드 중",
        summaryText = "백그라운드 다운로드"
    }
}));

// 진행 업데이트
AndroidNotificationManager.Instance.UpdateProgressForegroundService(AndroidNotificationJsonBuilder.BuildNotificationJson(new AndroidNotificationPayload
{
    id = 1301,
    title = "전설 장비 제작 중",
    message = "대장간이 최대 출력으로 가동 중입니다.",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    ongoing = true,
    autoCancel = false,
    onlyAlertOnce = true,
    progress = new AndroidNotificationProgressPayload { max = 100, current = progressValue, indeterminate = false },
    style = new AndroidNotificationStylePayload
    {
        type = "bigText",
        bigText = "대장간이 최대 출력으로 가동 중입니다. 제작이 끝나면 바로 장착할 수 있도록 준비하세요.",
        bigContentTitle = "전설 장비 제작 중",
        summaryText = "제작 업데이트"
    }
}));

// 완료 — 서비스 중지 및 일반 알림으로 강등
AndroidNotificationManager.Instance.CompleteProgressForegroundService(AndroidNotificationJsonBuilder.BuildNotificationJson(new AndroidNotificationPayload
{
    id = 1301,
    title = "제작 완료",
    message = "전설 검을 수령할 준비가 되었습니다.",
    channel = CreateGameplayChannelReference(),
    smallIcon = CreateUnityAppIconResource(),
    ongoing = false,
    autoCancel = true,
    progress = new AndroidNotificationProgressPayload { max = 100, current = 100, indeterminate = false },
    style = new AndroidNotificationStylePayload
    {
        type = "bigText",
        bigText = "전설 검을 수령할 준비가 되었습니다. 다음 전투 전에 대장간으로 돌아가 장착하세요.",
        bigContentTitle = "제작 완료",
        summaryText = "제작 완료"
    }
}));

// 강제 중지 — 알림도 제거
AndroidNotificationManager.Instance.StopProgressForegroundService();
#endif
```

<p align="center">
    <img src="images/android/notification/Example_AndroidNotificationManager_ProgressNotification.png" alt="Example_AndroidNotificationManager_ProgressNotification" width="400" />
</p>

---

### 포그라운드 서비스 알림

포그라운드 서비스(종류는 `dataSync`)는 라이브러리가 자체 매니페스트에서 선언하므로 `<service>` 줄을 추가할 필요가 없습니다. 1.12.0을 위해 추가한 줄은 지워 주세요([1.13.0 변경 사항](#1130-변경-사항) 참고).

`StartProgressForegroundService`, `UpdateProgressForegroundService`, `CompleteProgressForegroundService`, `StopProgressForegroundService`의 사용법은 [진행 알림](#진행-알림)을 참조하세요.

---

### 예약 알림

지정한 시간에 자동으로 알림을 표시합니다.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
long triggerTime = DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds();

string scheduleJson = AndroidNotificationJsonBuilder.BuildScheduledNotificationJson(new AndroidScheduledNotificationEnvelopePayload
{
    notification = new AndroidNotificationPayload
    {
        id = 1201,
        title = "길드 배틀 곧 시작",
        message = "팀 큐가 15초 후에 열립니다. 길드원을 모아 출격 준비를 하세요.",
        tag = "guild-battle",
        channel = CreateGameplayChannelReference(),
        smallIcon = CreateUnityAppIconResource(),
        autoCancel = true,
        priority = 1,
        groupKey = "guild-events",
        sortKey = "001",
        style = new AndroidNotificationStylePayload
        {
            type = "bigText",
            bigText = "팀 큐가 15초 후에 열립니다. 장비를 최종 점검하고 출격 준비를 마치세요.",
            bigContentTitle = "길드 배틀 곧 시작",
            summaryText = "길드 이벤트"
        }
    },
    schedule = new AndroidNotificationSchedulePayload
    {
        triggerAtMillis = triggerTime,
        exact = true,           // 정확한 알람 (SCHEDULE_EXACT_ALARM 필요)
        allowWhileIdle = true,  // Doze 모드에서도 발생
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

> **참고:** `IsNotificationScheduled`와 `CancelAllScheduledNotifications`가 보는 것은 `persistAcrossBoot = true`(기본값)로 만든 예약뿐입니다. `persistAcrossBoot = false`로 만든 예약은 `CancelScheduledNotification`에 ID를 지정해 취소해 주세요.

---

## iOS

### 개요

iOS 알림은 `IosNotificationManager`를 통해 사용할 수 있습니다. 샘플 화면에서는 권한 요청, 즉시 알림, 첨부 알림, 예약 알림, 카테고리/액션, 배지 조작까지 확인할 수 있습니다.

#### 지원 기능

- 알림 권한 요청 / 권한 상태 확인 / 알림 설정 화면 이동
- 즉시 알림 (첨부 파일 알림 포함)
- 예약 알림 (시간 간격 / 캘린더 / 위치 기반)
- 알림 업데이트 / 취소 / 전달 완료 알림 삭제 / 목록 조회
- 배지 개수 설정
- 카테고리 등록 / 액션 / 텍스트 입력 액션

#### 기본 설정

```csharp
#if UNITY_IOS && !UNITY_EDITOR
IosNotificationManager.Instance.RequestPermission(result =>
{
    if (result.IsSuccess)
    {
        Debug.Log("iOS notification permission granted");
    }
    else
    {
        Debug.LogError($"Permission failed: {result.ErrorMessage}");
    }
});

IosNotificationManager.Instance.GetAuthorizationStatus(status =>
{
    Debug.Log($"Authorization status: {status}");
});
#endif
```

<a id="ios-basic-setup-sample"></a>

<p align="center">
    <img src="images/ios/notification/Example_IosNotificationManager_RequestPermission.png" alt="Example_IosNotificationManager_RequestPermission" width="400" />
</p>

#### 즉시 알림 표시

```csharp
#if UNITY_IOS && !UNITY_EDITOR
var content = new NotificationContentPayload
{
    id = "sample-notification",
    title = "Energy Refilled",
    body = "Your squad is fully rested. Jump back in and clear the next raid.",
    sound = "default",
    categoryIdentifier = "sample-category"
};

var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
IosNotificationManager.Instance.ShowNotification(contentJson, null, result =>
{
    Debug.Log($"ShowNotification: {result.IsSuccess}");
});
#endif
```

<a id="ios-show-immediate-sample"></a>

<p align="center">
    <img src="images/ios/notification/Example_IosNotificationManager_ShowImmediate.png" alt="Example_IosNotificationManager_ShowImmediate" width="400" />
</p>

#### 앱 아이콘 첨부 즉시 알림

```csharp
#if UNITY_IOS && !UNITY_EDITOR
var content = new NotificationContentPayload
{
    id = "sample-notification",
    title = "Immediate Notification with Attachment",
    body = "Displayed with app icon attachment.",
    sound = "default",
    attachments = new[]
    {
        new NotificationAttachmentPayload
        {
            identifier = "app-icon",
            fileURL = appIconFileUrl
        }
    }
};

var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
IosNotificationManager.Instance.ShowNotification(contentJson, null, null);
#endif
```

<a id="ios-show-attachment-sample"></a>

<p align="center">
    <img src="images/ios/notification/Example_IosNotificationManager_ShowImmediateWithAttachment.png" alt="Example_IosNotificationManager_ShowImmediateWithAttachment" width="400" />
</p>

#### 예약 알림 등록

```csharp
#if UNITY_IOS && !UNITY_EDITOR
var content = new NotificationContentPayload
{
    id = "scheduled-notification",
    title = "Guild Battle Starts Soon",
    body = "Battle queue opens in 10 seconds. Finalize your loadout and deploy.",
    sound = "default"
};

var trigger = new TimeIntervalTriggerPayload
{
    repeats = false,
    interval = 10.0
};

string contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
string triggerJson = IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(trigger);

IosNotificationManager.Instance.ScheduleNotification(
    contentJson,
    triggerJson,
    "scheduled-notification",
    result => Debug.Log($"ScheduleNotification: {result.IsSuccess}"));
#endif
```

<a id="ios-schedule-sample"></a>

<p align="center">
    <img src="images/ios/notification/Example_IosNotificationManager_ScheduleNotification.png" alt="Example_IosNotificationManager_ScheduleNotification" width="400" />
</p>

캘린더 예약은 `CalendarTriggerPayload`, 위치 기반 예약은 `LocationTriggerPayload`를 사용합니다.

#### 카테고리와 액션 등록

```csharp
#if UNITY_IOS && !UNITY_EDITOR
var category = new IosNotificationCategoryPayload
{
    identifier = "sample-category",
    actions = new[]
    {
        new IosNotificationActionPayload
        {
            identifier = "open",
            title = "Open",
            options = new[] { "foreground" }
        }
    },
    textInputActions = new[]
    {
        new IosNotificationTextInputActionPayload
        {
            identifier = "reply",
            title = "Reply",
            buttonTitle = "Send",
            textInputPlaceholder = "Type a message"
        }
    }
};

string categoryJson = IosNotificationJsonBuilder.BuildCategoryJson(category);
IosNotificationManager.Instance.RegisterCategory(categoryJson, null);
#endif
```

<a id="ios-category-sample"></a>

<p align="center">
    <img src="images/ios/notification/Example_IosNotificationManager_RegisterCategory.png" alt="Example_IosNotificationManager_RegisterCategory" width="400" />
</p>

#### 이벤트 수신

```csharp
#if UNITY_IOS && !UNITY_EDITOR
IosNotificationManager.Instance.NotificationOperationCompleted += result =>
{
    Debug.Log($"Operation={result.Operation}, Success={result.IsSuccess}");
};

IosNotificationManager.Instance.NotificationActionReceived += result =>
{
    Debug.Log($"Action={result.ActionId}, Notification={result.NotificationId}");
};

IosNotificationManager.Instance.NotificationTextInputActionReceived += result =>
{
    Debug.Log($"TextInput={result.UserText}");
};
#endif
```

<a id="ios-events-sample"></a>

<p align="center">
    <img src="images/ios/notification/Example_IosNotificationManager_Result.png" alt="Example_IosNotificationManager_Result" width="400" />
</p>

---

## Windows

Windows 알림은 `WindowsNotificationManager`를 통해 제공됩니다. 샘플 씬에서는 초기화, 즉시 알림, 예약 알림, 진행률 바 알림, 알림 삭제, 알림 권한 쿼리를 Unity로 빌드한 언패키지 Win32 앱에서 시연합니다.

### 1.12.0 변경 사항

1.12.0부터 Windows 알림은 native-toolkit의 C ABI 2.0.0(`windows-native-toolkit-capi-2.0.0.dll`)으로 동작합니다. 메서드, 이벤트, JSON 페이로드와 그 단위는 바뀌지 않았습니다. 1.11.0과 다른 점은 다음과 같습니다.

- **네임스페이스.** `WindowsNotificationManager`와 페이로드·빌더·결과 타입은 `JonghyunKim.NativeToolkit.Runtime.Windows.Notification`에 있습니다. Windows 코드의 `using JonghyunKim.NativeToolkit.Runtime.Notification;`를 바꿔 주세요. iOS / macOS 타입의 네임스페이스는 그대로입니다. Android 타입은 1.13.0에서 `JonghyunKim.NativeToolkit.Runtime.Android.Notification`으로 옮겨졌습니다([1.13.0 변경 사항](#1130-변경-사항) 참고).
- **클릭이 전달됩니다.** 패키지화하지 않은 앱(Unity의 Windows 스탠드얼론 빌드는 모두 해당)에서 앱이 실행 중일 때 알림이나 그 버튼을 클릭하면 `NotificationInvoked`가 발생합니다. 1.11.0에서는 클릭이 전달되지 않았습니다. 앱이 실행 중이 아닐 때 클릭하면 앱이 시작되고, `Initialize` 후에 클릭이 한 번 전달됩니다.
- **등록.** `Initialize`는 `HKCU\Software\Classes\AppUserModelId\<displayName>\CustomActivator`도 기록하며, 이를 통해 클릭이 이 실행 파일로 전달됩니다. **같은 `displayName`을 쓰는 앱끼리는 클릭을 서로 가져갑니다.** 빌드마다 다른 표시 이름을 사용해 주세요.
- **오류 코드 `-4`**("Native library unavailable"). `Initialize`만 반환합니다. 네이티브 라이브러리가 없거나, 함수가 없거나, 불러올 수 없거나, C ABI 2.x가 아닐 때입니다. Manager가 만들어지지 않으므로 이후 작업은 `1`로 실패합니다.
- **Editor.** Editor에서는 Windows 알림 API가 아무 일도 하지 않고, 아무것도 보고하지 않습니다.

| 상황 | 1.11.0 | 1.12.0 |
| --- | --- | --- |
| JSON 키가 없거나, 타입이 다르거나, `null` | `5` | `7` |
| `double`에 담기지 않는 JSON 숫자(`1e400`, `1e-400`) | `5` | `3` |
| 0으로 반올림되는 JSON 소수 | 0으로 성공 | `3` |
| 컨테이너가 512개를 넘게 중첩된 JSON | `5` | `3` |
| `null` 페이로드 | 프로세스가 종료됨 | `3` |
| 문자열 안의 `NUL` | 끝까지 전달 | `NUL` 앞에서 잘림 |
| 범위를 벗어난 `timestamp`·`expiration`·`scheduledTimeUnixMs` | 검사 없이 전달 | `7` |
| 인수가 2개 이상인 버튼의 인수 | 순서가 정해져 있지 않았음 | JSON에 쓴 순서 |
| `UpdateNotificationProgress`의 `valueStr`이나 `status`가 `null` | 그 항목을 바꾸지 않았음 | 그 항목을 `""`로 설정 |
| 네이티브 라이브러리가 없거나 불러올 수 없음 | 모든 메서드에서 예외 | `Initialize`는 `-4`, 다른 메서드는 `1` |
| 그 밖의 C# 쪽 예외 | 호출한 쪽으로 예외 | `5` |
| Manager를 파괴한 뒤 | Windows App SDK 런타임이 초기화된 채로 남음 | 한 번 종료하고, 새 Manager의 `Initialize`에서 다시 초기화 |
| 문자열 안의 짝이 맞지 않는 서로게이트 | 그대로 | U+FFFD로 바뀜 |

바뀌지 않은 것: 두 번째 `Initialize`는 성공합니다. `timestamp`는 초 단위입니다. `Audio.Src`는 여전히 네이티브 쪽에서 읽지 않으므로, 무엇을 넣어도 기본 소리가 납니다.

### 지원 기능

- Windows App SDK 런타임 초기화 및 알림 콜백 등록
- 즉시 알림 표시
- 예약 알림 등록
- 예약 알림 취소
- 진행률 바 알림 표시 및 업데이트
- 태그 지정 또는 전체 알림 삭제
- 알림 권한 설정 쿼리
- 시스템 알림 설정 열기
- 알림 활성화 이벤트 수신 (콜드 스타트 포함)

### 설정

#### 요구 사항

- Windows 11 이상
- 대상 머신에 [Windows App Runtime 1.7.2 (1.7.250513003)](https://learn.microsoft.com/ko-kr/windows/apps/windows-app-sdk/downloads-archive)이 설치되어 있어야 함
- `StreamingAssets/app-icon.png` — 이 경로에 알림 아이콘을 배치하면 Unity가 빌드 출력에 자동으로 포함합니다

#### 네임스페이스 임포트

```csharp
using JonghyunKim.NativeToolkit.Runtime.Windows.Notification;
```

> **참고:** 모든 Windows 알림 API 호출을 `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`로 감싸 에디터에서 네이티브 호출이 실행되지 않도록 하세요.

### 초기화

다른 알림 API를 호출하기 전에 `Initialize`를 한 번 호출하세요. 언패키지 앱(모든 Unity Windows 스탠드얼론 빌드)의 경우 `isPackaged: false`와 함께 표시 이름과 아이콘 URI를 지정하세요.

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
string iconPath = System.IO.Path.Combine(Application.streamingAssetsPath, "app-icon.png");
string iconUri  = new Uri(iconPath).AbsoluteUri; // file:/// URI 형식

WindowsNotificationManager.Instance.Initialize(
    isPackaged: false,
    displayName: Application.productName,
    iconUri: iconUri,
    onResult: result =>
    {
        if (result.IsSuccess)
            Debug.Log("Windows 알림 초기화 완료");
        else
            Debug.LogError($"Initialize 실패: {result.ErrorMessage}");
    });
#endif
```

> **참고:** `iconUri`는 언패키지 앱에서 필수입니다. `new Uri(path).AbsoluteUri`를 사용하면 Windows App SDK가 요구하는 `file:///` 형식의 URI가 생성됩니다.


### 알림 표시

#### 즉시 알림

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
var payload = new WindowsNotificationPayload
{
    Title   = "에너지 충전 완료",
    Body    = "부대의 휴식이 완료되었습니다. 다음 레이드에 도전하세요.",
    Tag     = "win-sample-notification",
    Group   = "win-sample-group",
    Buttons = new List<WindowsNotificationButtonPayload>
    {
        new() { Label = "열기", Args = new Dictionary<string, string> { ["action"] = "open" } }
    }
};

string json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
WindowsNotificationManager.Instance.ShowNotification(json, result =>
{
    Debug.Log($"ShowNotification: {result.IsSuccess}");
});
#endif
```

<p align="center">
  <img src="images/windows/notification/Example_WindowsNotificationManager_ShowNotification.png" alt="Example_WindowsNotificationManager_ShowNotification" width="720" />
</p>

### 예약 알림

#### 알림 예약

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
var payload = new WindowsNotificationPayload
{
    Title   = "길드 배틀 곧 시작",
    Body    = "1분 후 배틀 큐가 열립니다. 편성을 확인하고 출격하세요.",
    Tag     = "win-sample-notification",
    Group   = "win-sample-group",
    Buttons = new List<WindowsNotificationButtonPayload>
    {
        new() { Label = "열기", Args = new Dictionary<string, string> { ["action"] = "open" } }
    }
};

string json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
long scheduledTimeUnixMs = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();

WindowsNotificationManager.Instance.ScheduleNotification(json, scheduledTimeUnixMs, result =>
{
    Debug.Log($"ScheduleNotification: {result.IsSuccess}");
});
#endif
```

<p align="center">
  <img src="images/windows/notification/Example_WindowsNotificationManager_ScheduleNotification.png" alt="Example_WindowsNotificationManager_ScheduleNotification" width="720" />
</p>

#### 예약 알림 취소

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsNotificationManager.Instance.CancelScheduledNotification(
    "win-sample-notification",
    "win-sample-group",
    result => Debug.Log($"CancelScheduled: {result.IsSuccess}"));
#endif
```


### 진행률 업데이트

진행률 바 알림을 먼저 표시한 후 `UpdateNotificationProgress`를 호출하세요. 두 호출에서 동일한 `tag`와 `group`을 사용해야 합니다. `sequenceNumber`는 업데이트마다 증가시켜야 합니다.

#### 진행률 알림 표시

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
uint sequenceNumber = 0; // 새 진행률 알림 표시 시 초기화

var payload = new WindowsNotificationPayload
{
    Title    = "다운로드 중...",
    Tag      = "win-sample-notification",
    Group    = "win-sample-group",
    Progress = new WindowsNotificationProgressPayload
    {
        Value    = 0.3,
        ValueStr = "30%",
        Status   = "다운로드 중"
    }
};

string json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
WindowsNotificationManager.Instance.ShowNotification(json, result =>
{
    Debug.Log($"ShowProgressNotification: {result.IsSuccess}");
});
#endif
```

<p align="center">
  <img src="images/windows/notification/Example_WindowsNotificationManager_ShowProgressNotification.png" alt="Example_WindowsNotificationManager_ShowProgressNotification" width="720" />
</p>

#### 진행률 업데이트

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
sequenceNumber++; // 이전 값보다 큰 값이어야 합니다

WindowsNotificationManager.Instance.UpdateNotificationProgress(
    tag:            "win-sample-notification",
    group:          "win-sample-group",
    value:          0.5,
    valueStr:       "50%",
    status:         "다운로드 중...",
    sequenceNumber: sequenceNumber,
    onResult: result =>
    {
        if (!result.IsSuccess && result.ErrorCode == 4)
            Debug.LogWarning("진행률 알림을 찾을 수 없습니다. 먼저 ShowProgressNotification을 호출하세요.");
        else
            Debug.Log($"UpdateProgress: {result.IsSuccess}");
    });
#endif
```

> **참고:** `UpdateNotificationProgress`를 호출하기 전에 `ShowProgressNotification`을 호출하세요. 진행률 알림이 알림 센터에 없으면 오류 코드 `4`(`NOTIFICATION_ERROR_PROGRESS_NOT_FOUND`)가 반환됩니다.

<p align="center">
  <img src="images/windows/notification/Example_WindowsNotificationManager_UpdateProgress.png" alt="Example_WindowsNotificationManager_UpdateProgress" width="720" />
</p>

### 알림 제거

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
// 특정 태그와 그룹에 해당하는 알림 제거
WindowsNotificationManager.Instance.RemoveNotificationsByTag(
    "win-sample-notification",
    "win-sample-group",
    result => Debug.Log($"RemoveByTag: {result.IsSuccess}"));

// 알림 센터의 모든 알림 제거
WindowsNotificationManager.Instance.RemoveAllNotifications(result =>
{
    Debug.Log($"RemoveAll: {result.IsSuccess}");
});
#endif
```


### 쿼리

#### 알림 권한 설정

`GetNotificationSetting`은 동기적으로 즉시 값을 반환합니다.

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsNotificationSetting setting = WindowsNotificationManager.Instance.GetNotificationSetting();
Debug.Log($"NotificationSetting: {setting}");
// Enabled / DisabledForApplication / DisabledForUser / DisabledByGroupPolicy / DisabledByManifest / Unknown
#endif
```

#### 알림 설정 열기

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsNotificationManager.Instance.OpenNotificationSettings(result =>
{
    Debug.Log($"OpenNotificationSettings: {result.IsSuccess}");
});
#endif
```


### 이벤트 수신

`OnEnable`에서 구독하고 `OnDisable`에서 구독을 해제하세요.

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
private void OnEnable()
{
    WindowsNotificationManager.Instance.NotificationOperationCompleted += OnOperationCompleted;
    WindowsNotificationManager.Instance.NotificationInvoked            += OnNotificationInvoked;
}

private void OnDisable()
{
    WindowsNotificationManager.Instance.NotificationOperationCompleted -= OnOperationCompleted;
    WindowsNotificationManager.Instance.NotificationInvoked            -= OnNotificationInvoked;
}

private void OnOperationCompleted(WindowsNotificationResult result)
{
    Debug.Log($"Operation={result.Operation}, IsSuccess={result.IsSuccess}");
}

private void OnNotificationInvoked(string argsJson)
{
    // 사용자가 알림 또는 액션 버튼을 클릭했을 때 발생합니다.
    // 콜드 스타트 시에도 발생합니다: 알림 클릭으로 앱이 실행된 경우
    // 씬 로드 후 Initialize()를 호출하면 활성화 인수를 받을 수 있습니다.
    // argsJson 예: {"action":"open"}
    Debug.Log($"NotificationInvoked: {argsJson}");
}
#endif
```

#### 오퍼레이션 상수

| 상수 | 설명 |
|---|---|
| `WindowsNotificationManager.OperationInitialize` | `Initialize` 완료 |
| `WindowsNotificationManager.OperationShow` | `ShowNotification` 완료 |
| `WindowsNotificationManager.OperationSchedule` | `ScheduleNotification` 완료 |
| `WindowsNotificationManager.OperationCancelScheduled` | `CancelScheduledNotification` 완료 |
| `WindowsNotificationManager.OperationUpdateProgress` | `UpdateNotificationProgress` 완료 |
| `WindowsNotificationManager.OperationRemoveByTag` | `RemoveNotificationsByTag` 완료 |
| `WindowsNotificationManager.OperationRemoveAll` | `RemoveAllNotifications` 완료 |
| `WindowsNotificationManager.OperationOpenSettings` | `OpenNotificationSettings` 완료 |

---

## macOS

macOS 알림은 `MacNotificationManager`를 통해 제공됩니다. 샘플 씬에서는 권한 흐름, 즉시 알림, 예약 알림, 카테고리와 액션 등록, 배지 관리, 조회 작업을 확인할 수 있습니다.

### 지원 기능

- 권한 요청 / 인증 상태 확인 / 시스템 알림 설정 열기
- 즉시 알림
- 예약 알림 (시간 간격 / 캘린더)
- 알림 업데이트 / 취소 / 전달된 알림 삭제
- 예약된 알림 및 전달된 알림 목록 조회
- 배지 카운트 관리
- 카테고리 등록 / 액션 / 텍스트 입력 액션

### 기본 설정

```csharp
// Guard: macOS Standalone 전용. 에디터에서의 네이티브 호출을 방지합니다.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Notification;
#endif
```

---

### 권한

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// 알림 권한 요청
MacNotificationManager.Instance.RequestPermission(result =>
{
    if (result.IsSuccess)
    {
        Debug.Log("macOS 알림 권한이 허용되었습니다");
    }
    else
    {
        Debug.LogError($"권한 요청 실패: {result.ErrorMessage}");
    }
});

// 알림 권한이 허용되었는지 확인
MacNotificationManager.Instance.HasPermission(hasPermission =>
{
    Debug.Log($"HasPermission: {hasPermission}");
});

// 전체 인증 상태 조회
MacNotificationManager.Instance.GetAuthorizationStatus(result =>
{
    var status = MacNotificationAuthorizationStatusParser.ParseJson(result.Json);
    Debug.Log($"AuthorizationStatus: {status}");
});

// 시스템 알림 설정 열기
MacNotificationManager.Instance.OpenSettings(result =>
{
    Debug.Log($"OpenSettings: {result.IsSuccess}");
});
#endif
```

<p align="center">
    <img src="images/mac/notification/Example_MacNotificationManager_RequestPermission.png" alt="Example_MacNotificationManager_RequestPermission" width="720" />
</p>

---

### 알림 표시

#### 즉시 알림

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
var content = new NotificationContentPayload
{
    id = "mac-sample-notification",
    title = "Energy Refilled",
    body = "Your squad is fully rested. Jump back in and clear the next raid.",
    categoryIdentifier = "mac-sample-category"
};
var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
MacNotificationManager.Instance.ShowNotification(contentJson, null, result =>
{
    if (result.IsSuccess)
    {
        Debug.Log("알림이 표시되었습니다");
    }
    else
    {
        Debug.LogError($"ShowNotification 실패: {result.ErrorMessage}");
    }
});
#endif
```

<p align="center">
    <img src="images/mac/notification/Example_MacNotificationManager_ShowImmediate.png" alt="Example_MacNotificationManager_ShowImmediate" width="720" />
</p>

#### 시간 간격 트리거

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
var content = new NotificationContentPayload
{
    id = "mac-sample-notification",
    title = "Guild Battle Countdown",
    body = "Your team queue opens in 5 seconds. Rally your party and get ready."
};
var trigger = new TimeIntervalTriggerPayload { interval = 5.0, repeats = false };
var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
var triggerJson = MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson(trigger);
MacNotificationManager.Instance.ShowNotification(contentJson, triggerJson, result =>
{
    Debug.Log($"ShowNotification: {result.IsSuccess}");
});
#endif
```

<p align="center">
    <img src="images/mac/notification/Example_MacNotificationManager_ShowTimeInterval.png" alt="Example_MacNotificationManager_ShowTimeInterval" width="720" />
</p>

#### 캘린더 트리거

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
var now = DateTime.Now.AddMinutes(1);
var content = new NotificationContentPayload
{
    id = "mac-sample-notification",
    title = "Daily Reward Ready",
    body = "Your login streak chest is ready in town. Claim it before reset."
};
var trigger = new CalendarTriggerPayload
{
    year = now.Year,
    month = now.Month,
    day = now.Day,
    hour = now.Hour,
    minute = now.Minute,
    second = now.Second,
    repeats = false
};
var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
var triggerJson = MacNotificationJsonBuilder.BuildCalendarTriggerJson(trigger);
MacNotificationManager.Instance.ShowNotification(contentJson, triggerJson, result =>
{
    Debug.Log($"ShowNotification: {result.IsSuccess}");
});
#endif
```

<p align="center">
    <img src="images/mac/notification/Example_MacNotificationManager_ShowCalendar.png" alt="Example_MacNotificationManager_ShowCalendar" width="720" />
</p>

---

### 업데이트, 취소 및 삭제

#### 업데이트

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
var content = new NotificationContentPayload
{
    id = "mac-sample-notification",
    title = "Town Entry Bonus",
    body = "Welcome back to town. Your blacksmith bonus is now available."
};
var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
MacNotificationManager.Instance.UpdateNotification("mac-sample-notification", contentJson, null, result =>
{
    Debug.Log($"UpdateNotification: {result.IsSuccess}");
});
#endif
```

#### 취소

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// ID로 특정 대기 중인 알림 취소
MacNotificationManager.Instance.CancelNotification("mac-sample-notification");

// 모든 대기 중인 알림 취소
MacNotificationManager.Instance.CancelAllNotifications();
#endif
```

#### 전달된 알림 삭제

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// 알림 센터에서 특정 전달된 알림 삭제
MacNotificationManager.Instance.RemoveDeliveredNotification("mac-sample-notification");

// 알림 센터에서 모든 전달된 알림 삭제
MacNotificationManager.Instance.RemoveAllDeliveredNotifications();
#endif
```

<p align="center">
    <img src="images/mac/notification/Example_MacNotificationManager_Update.png" alt="Example_MacNotificationManager_Update" width="720" />
</p>

---

### 예약 알림

게임 루프를 차단하지 않고 미래 시각에 알림을 예약합니다.

#### 시간 간격

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
var content = new NotificationContentPayload
{
    id = "mac-sample-notification",
    title = "Guild Battle Starts Soon",
    body = "Battle queue opens in 10 seconds. Finalize your loadout and deploy."
};
var trigger = new TimeIntervalTriggerPayload { interval = 10.0, repeats = false };
var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
var triggerJson = MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson(trigger);
MacNotificationManager.Instance.ScheduleNotification(contentJson, triggerJson, result =>
{
    Debug.Log($"ScheduleNotification: {result.IsSuccess}");
});
#endif
```

#### 캘린더

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
var future = DateTime.Now.AddMinutes(1);
var content = new NotificationContentPayload
{
    id = "mac-sample-notification",
    title = "Daily Reward Window",
    body = "Your daily reward window is open. Check in now to keep your streak."
};
var trigger = new CalendarTriggerPayload
{
    year = future.Year,
    month = future.Month,
    day = future.Day,
    hour = future.Hour,
    minute = future.Minute,
    second = future.Second,
    repeats = false
};
var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
var triggerJson = MacNotificationJsonBuilder.BuildCalendarTriggerJson(trigger);
MacNotificationManager.Instance.ScheduleNotification(contentJson, triggerJson, result =>
{
    Debug.Log($"ScheduleNotification: {result.IsSuccess}");
});
#endif
```

#### 예약 취소

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// ID로 특정 예약된 알림 취소
MacNotificationManager.Instance.CancelScheduledNotification("mac-sample-notification");

// 모든 예약된 알림 취소
MacNotificationManager.Instance.CancelAllScheduledNotifications();
#endif
```

<p align="center">
    <img src="images/mac/notification/Example_MacNotificationManager_Schedule.png" alt="Example_MacNotificationManager_Schedule" width="720" />
</p>

---

### 조회

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// 대기 중인 예약 알림 조회
MacNotificationManager.Instance.GetScheduledNotifications(result =>
{
    Debug.Log($"GetScheduled: {result.Json}");
});

// 알림 센터의 전달된 알림 조회
MacNotificationManager.Instance.GetDeliveredNotifications(result =>
{
    Debug.Log($"GetDelivered: {result.Json}");
});
#endif
```

---

### 배지

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// 배지 카운트를 1로 설정
MacNotificationManager.Instance.SetBadgeCount(1, result =>
{
    Debug.Log($"SetBadgeCount(1): {result.IsSuccess}");
});

// 배지 지우기
MacNotificationManager.Instance.SetBadgeCount(0, result =>
{
    Debug.Log($"SetBadgeCount(0): {result.IsSuccess}");
});
#endif
```

---

### 카테고리와 액션

`categoryIdentifier`를 사용하는 알림을 보내기 전에 액션 버튼이 포함된 카테고리를 먼저 등록하세요.

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
var category = new MacNotificationCategoryPayload
{
    id = "mac-sample-category",
    actions = new[]
    {
        new MacNotificationActionPayload
        {
            id = "open",
            title = "Open",
            isForeground = true,
            isTextInput = false
        },
        new MacNotificationActionPayload
        {
            id = "delete",
            title = "Delete",
            isForeground = false,
            isTextInput = false
        },
        new MacNotificationActionPayload
        {
            id = "reply",
            title = "Reply",
            isForeground = false,
            isTextInput = true,
            textInputPlaceholder = "Type a message"
        }
    }
};
string categoryJson = MacNotificationJsonBuilder.BuildCategoryJson(category);
MacNotificationManager.Instance.RegisterCategory(categoryJson, result =>
{
    Debug.Log($"RegisterCategory: {result.IsSuccess}");
});
#endif
```

액션 버튼을 보려면 알림 배너에 마우스를 올려 Options (▼) 버튼을 클릭하세요.

#### 카테고리 삭제

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
MacNotificationManager.Instance.RemoveCategory("mac-sample-category", result =>
{
    Debug.Log($"RemoveCategory: {result.IsSuccess}");
});
#endif
```

<p align="center">
    <img src="images/mac/notification/Example_MacNotificationManager_RegisterCategory.png" alt="Example_MacNotificationManager_RegisterCategory" width="720" />
</p>

---

### 이벤트 수신

`OnEnable`에서 구독하고 `OnDisable`에서 구독을 해제하세요.

```csharp
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
private void OnEnable()
{
    MacNotificationManager.Instance.NotificationActionReceived += OnNotificationActionReceived;
    MacNotificationManager.Instance.NotificationTextInputActionReceived += OnNotificationTextInputActionReceived;
}

private void OnDisable()
{
    MacNotificationManager.Instance.NotificationActionReceived -= OnNotificationActionReceived;
    MacNotificationManager.Instance.NotificationTextInputActionReceived -= OnNotificationTextInputActionReceived;
}

private void OnNotificationActionReceived(MacNotificationActionResult result)
{
    Debug.Log($"Action: notificationId={result.NotificationId}, actionId={result.ActionId}");
}

private void OnNotificationTextInputActionReceived(MacNotificationTextInputActionResult result)
{
    Debug.Log($"TextInput: notificationId={result.NotificationId}, actionId={result.ActionId}, userText={result.UserText}");
}
#endif
```

<p align="center">
    <img src="images/mac/notification/Example_MacNotificationManager_Result.png" alt="Example_MacNotificationManager_Result" width="720" />
</p>
