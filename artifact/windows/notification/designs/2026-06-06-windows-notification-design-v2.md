# Windows Notification 実装計画書

- 日付: 2026-06-06
- 対象プラットフォーム: Windows
- 機能: notification
- 改訂: v2（v1 レビュー結果を反映）

---

## 1. native-toolkit 確認結果

### 参照パス

`C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary\WindowsNotificationManager.h`
`C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary\WindowsNotificationManagerInternal.h`
`C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary\WindowsNotificationManager.cpp`

### DLL 名

```csharp
#if DEVELOPMENT_BUILD
private const string DLL_NAME = "UnityWindowsNativeToolkit-Debug.dll";
#else
private const string DLL_NAME = "UnityWindowsNativeToolkit.dll";
#endif
```

WindowsDialogManager と同じ DLL から export される。`#if` による条件コンパイルで切り替える。

### コールバック型

```c
typedef void (*NotificationInvokedCallback)(const wchar_t* argsJson);
```

- `argsJson`: アクション引数 + ユーザー入力をマージした JSON オブジェクト文字列
- `{"actionKey":"value", "userInputKey":"value", ...}` 形式（アプリ定義のキー構造）
- `initNotificationManager` 呼び出し時に一度だけ登録する（Mac の複数コールバック型と異なる）
- バックグラウンドスレッドから来る可能性があるため `UnityMainThreadDispatcher` 経由で発火する

### 公開関数一覧

| 関数名 | 引数 | 備考 |
|--------|------|------|
| `initNotificationManager` | `(NotificationInvokedCallback, BOOL isPackaged, const wchar_t* clsid, const wchar_t* launchUri, DWORD* pError)` | 初回のみ有効。再呼び出しは no-op（callback は更新される） |
| `uninitNotificationManager` | `()` | Unregister + リセット |
| `showNotification` | `(const wchar_t* jsonPayload, DWORD* pError)` | 即時表示 |
| `scheduleNotification` | `(const wchar_t* jsonPayload, int64_t scheduledTimeUnixMs, DWORD* pError)` | Unix epoch ミリ秒で指定 |
| `cancelScheduledNotification` | `(const wchar_t* tag, const wchar_t* group, DWORD* pError)` | tag + group で特定 |
| `updateNotificationProgress` | `(const wchar_t* tag, const wchar_t* group, double value, const wchar_t* valueStr, const wchar_t* status, uint32_t sequenceNumber, DWORD* pError)` | sequenceNumber は前回より大きい値が必要 |
| `setBadge` | `(int value, DWORD* pError)` | >0: 数値, 0: クリア, -1〜-6: グリフ |
| `removeNotificationById` | `(uint32_t notificationId, DWORD* pError)` | ID 指定削除 |
| `removeNotificationsByTag` | `(const wchar_t* tag, const wchar_t* group, DWORD* pError)` | tag + group 削除 |
| `removeAllNotifications` | `(DWORD* pError)` | 全件削除 |
| `getAllNotifications` | `(wchar_t* outJson, uint32_t bufferSize, DWORD* pError)` | バッファ出力型。bufferSize は wchar_t 単位（文字数） |
| `getNotificationSetting` | `()` → `int` | 同期戻り値。out pError なし。特例 API |
| `openNotificationSettings` | `(DWORD* pError)` | ms-settings:notifications を開く |

### エラーコード定数

| 値 | 定数名 | 意味 |
|----|--------|------|
| 0 | `NOTIFICATION_SUCCESS` | 成功 |
| 1 | `NOTIFICATION_ERROR_NOT_INITIALIZED` | 未初期化 |
| 2 | `NOTIFICATION_ERROR_DISABLED` | OS 設定で通知無効 |
| 3 | `NOTIFICATION_ERROR_INVALID_PAYLOAD` | JSON パースエラー |
| 4 | `NOTIFICATION_ERROR_PROGRESS_NOT_FOUND` | progress update 対象が見つからない |
| 5 | `NOTIFICATION_ERROR_HRESULT_FAILURE` | WinRT HRESULT エラー（バッファ不足時もこれが返る可能性あり。要検証） |
| 6 | `NOTIFICATION_ERROR_BADGE_FAILED` | バッジ操作失敗 |
| 7 | `NOTIFICATION_ERROR_INVALID_PARAMETER` | パラメータ不正 |

### `getNotificationSetting` 戻り値（特例 API）

`getNotificationSetting` は out pError を持たず int を同期返却する。`WindowsNotificationResult` 契約に乗らない特例 API として扱い、C# 側で専用 enum に変換して返す。

| 値 | C# enum | 意味 |
|----|---------|------|
| 0 | `Enabled` | 通知有効 |
| 1 | `DisabledForApplication` | アプリ設定で無効 |
| 2 | `DisabledForUser` | ユーザー設定で無効 |
| 3 | `DisabledByGroupPolicy` | グループポリシーで無効 |
| 4 | `DisabledByManifest` | マニフェストで無効 |
| -1 | `Unknown` | error（WinRT 例外） |

### `setBadge` グリフ値（enum 化対象）

| 値 | C# enum | グリフ |
|----|---------|--------|
| >0 | ― | 数値バッジ |
| 0 | `Clear` | クリア |
| -1 | `Alert` | alert |
| -2 | `Activity` | activity |
| -3 | `NewMessage` | newMessage |
| -4 | `Available` | available |
| -5 | `Busy` | busy |
| -6 | `Away` | away |

### JSON ペイロードスキーマ（showNotification / scheduleNotification 共通）

```json
{
  "title": "string (optional)",
  "body": "string (optional)",
  "tag": "string (optional)",
  "group": "string (optional)",
  "scenario": "reminder | alarm | urgent | incomingCall (optional)",
  "duration": "long (optional)",
  "expiration": 60,
  "expiresOnReboot": false,
  "timestamp": 1234567890,
  "attribution": "string (optional)",
  "buttons": [
    { "label": "OK", "args": { "action": "ok" } },
    { "label": "Open", "invokeUri": "https://example.com" }
  ],
  "textBoxes": [
    { "id": "reply", "placeholder": "Type here...", "title": "Reply" }
  ],
  "comboBoxes": [...],
  "images": { ... },
  "audio": { "src": "ms-winsoundevent:Notification.Default", "loop": false },
  "progress": { "value": 0.5, "valueStr": "50%", "status": "Downloading..." }
}
```

バリデーション制約（**C# 側 WindowsNotificationJsonBuilder で事前検証する**）:
- `buttons` 最大 5 件
- `audio.loop: true` は `duration: "long"` が必須
- ボタンに `args` と `invokeUri` を同時指定不可

### getAllNotifications 出力 JSON フォーマット

```json
[{"id": 1, "tag": "my-tag", "group": "my-group"}, ...]
```

**バッファサイズの単位**: `bufferSize` は `wchar_t` 単位（文字数）。`AllocHGlobal` に渡すバイト数は `bufferSize * sizeof(wchar_t) = bufferSize * 2`。

**バッファ不足時の挙動**: pError に `NOTIFICATION_ERROR_HRESULT_FAILURE`（5）が返ることが想定される（要実機確認）。不足時はバッファサイズを拡大して再呼び出しするリトライ戦略を C# 側で実装する。

---

## 2. 既存 C# 実装確認結果

### 参照パス

`Packages/com.jonghyunkim.nativetoolkit/Runtime/`

### 既存パターン（MacNotificationManager を参照）

- コンパイルガード: `#if UNITY_STANDALONE_WIN`（全新規ファイルに適用）
- `Application.platform != RuntimePlatform.WindowsPlayer` 時は early return（全 public API で適用）
- Singleton: `_instance` + `DontDestroyOnLoad` + `Awake` / `OnDestroy`
- `Awake` で `_ = UnityMainThreadDispatcher.Instance` を呼んでメインスレッドで初期化確保
- `OnDestroy` で `uninitNotificationManager()` を呼ぶ（`m_initialized` 相当フラグで二重呼び出しを防ぐ）
- static delegate フィールドで GC 防止（IL2CPP / AOT 対応。後述）
- `[MonoPInvokeCallback]` + `UnityMainThreadDispatcher.Instance.Enqueue()` で callback → メインスレッド転送
- Result struct: `Operation` / `IsSuccess` / `ErrorCode` / `ErrorMessage` の readonly struct
- `NotificationOperationCompleted` event（全操作共通）と操作固有 per-call `Action<T>?` の2段構成

### IL2CPP / AOT 制約

- `NotificationInvokedCallback` delegate に `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` を付与する
- コールバック static メソッドに `[MonoPInvokeCallback(typeof(NotificationInvokedCallback))]` を付与する
- static persistent delegate フィールドに保持して GC を防ぐ（関数ポインタを渡した後に GC されないよう必須）

### 既存ファイル（変更なし）

| ファイル | 用途 |
|---------|------|
| `Common/UnityMainThreadDispatcher.cs` | コールバックのメインスレッド転送 |
| `Notification/MacNotificationManager.cs` | macOS 実装（参照パターン） |
| その他プラットフォーム実装 | 変更なし |

### 重複しないこと

- `UnityMainThreadDispatcher` は再実装しない
- `NotificationContentPayload` 等の共通型は既存のものを確認して再利用を検討する

---

## 3. 変更ファイル一覧

### 新規作成

| ファイルパス | 内容 |
|------------|------|
| `Runtime/Notification/WindowsNotificationResult.cs` | 操作結果 readonly struct |
| `Runtime/Notification/WindowsNotificationPayloads.cs` | ペイロード型 + 関連 enum（NotificationSetting, BadgeGlyph） |
| `Runtime/Notification/WindowsNotificationJsonBuilder.cs` | JSON 組み立て static class（バリデーション制約を内包） |
| `Runtime/Notification/WindowsNotificationManager.cs` | Singleton Manager（Bridge + event 公開） |

上記全ファイルに `#if UNITY_STANDALONE_WIN` を適用する。

> **サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は本計画書の対象外。`design-sample-scene` スキルで別途設計する。**

### 既存変更

なし

### 非変更

上記「変更なし」ファイルすべて

---

## 4. 実装詳細

### 4-1. WindowsNotificationResult.cs

```csharp
#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the result of a Windows notification operation.
    /// </summary>
    public readonly struct WindowsNotificationResult
    {
        /// <summary>Gets the name of the operation that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Gets whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Gets the native error code (NOTIFICATION_ERROR_* constant). Zero on success.</summary>
        public int ErrorCode { get; }

        /// <summary>Gets the human-readable error message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful result.</summary>
        public static WindowsNotificationResult Success(string operation) =>
            new(operation, true, 0, null);

        /// <summary>Creates a failure result.</summary>
        public static WindowsNotificationResult Failure(string operation, int errorCode) =>
            new(operation, false, errorCode, ErrorCodeToMessage(errorCode));

        private WindowsNotificationResult(string operation, bool isSuccess, int errorCode, string? errorMessage)
        {
            Operation    = operation;
            IsSuccess    = isSuccess;
            ErrorCode    = errorCode;
            ErrorMessage = errorMessage;
        }

        private static string? ErrorCodeToMessage(int code) => code switch
        {
            1 => "Not initialized",
            2 => "Notifications are disabled",
            3 => "Invalid JSON payload",
            4 => "Progress notification not found",
            5 => "WinRT HRESULT failure",
            6 => "Badge operation failed",
            7 => "Invalid parameter",
            _ => $"Unknown error ({code})"
        };
    }
}
#endif
```

### 4-2. WindowsNotificationPayloads.cs

Mac の `NotificationContentPayload` 等を参考に Windows 通知専用ペイロード構造体と関連 enum を定義する。

**enum 定義**:

```csharp
public enum WindowsNotificationSetting
{
    Enabled                = 0,
    DisabledForApplication = 1,
    DisabledForUser        = 2,
    DisabledByGroupPolicy  = 3,
    DisabledByManifest     = 4,
    Unknown                = -1
}

public enum WindowsBadgeValue
{
    Clear      = 0,
    Alert      = -1,
    Activity   = -2,
    NewMessage = -3,
    Available  = -4,
    Busy       = -5,
    Away       = -6
}
```

**ペイロード型**:

| 型名 | 主なフィールド |
|------|--------------|
| `WindowsNotificationPayload` | title, body, tag, group, scenario, duration, buttons, textBoxes, audio, progress, expiration, expiresOnReboot, timestamp, attribution |
| `WindowsNotificationButtonPayload` | label, args (Dictionary<string,string>?), invokeUri (string?) |
| `WindowsNotificationTextBoxPayload` | id, placeholder, title |
| `WindowsNotificationAudioPayload` | src, loop |
| `WindowsNotificationProgressPayload` | value (double 0.0〜1.0), valueStr, status |

### 4-3. WindowsNotificationJsonBuilder.cs

```csharp
/// <summary>
/// Builds JSON strings for Windows notification APIs.
/// Also enforces payload validation constraints before serialization.
/// </summary>
public static class WindowsNotificationJsonBuilder
{
    /// <summary>
    /// Builds a JSON payload string from a WindowsNotificationPayload.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when validation constraints are violated.</exception>
    public static string BuildNotificationPayload(WindowsNotificationPayload payload) { ... }

    /// <summary>Validates payload constraints and returns error message on violation, null on success.</summary>
    public static string? Validate(WindowsNotificationPayload payload) { ... }
}
```

**バリデーション担当**: `WindowsNotificationJsonBuilder` が責任を持つ（native 側は JSON パース後に検証するが、C# 側で事前チェックすることで early fail させる）。

バリデーション制約:
- `buttons` が 5 件超: `"buttons count exceeds 5"` でエラー
- `audio.loop == true` かつ `duration != "long"`: `"audio.loop requires duration=long"` でエラー
- 同一ボタンに `args` と `invokeUri` を同時指定: `"button cannot have both args and invokeUri"` でエラー

JSON 組み立て:
- `JsonUtility` / `System.Text.Json` は使わず、手動で `Dictionary<string, object?>` + `StringBuilder` で組み立てる（Mac JsonBuilder パターンに準拠）
- ネスト構造（buttons 配列等）は `SerializeObject` ヘルパーで再帰的に組み立てる

### 4-4. WindowsNotificationManager.cs

**Operation 定数**:

```csharp
public const string OperationInitialize      = "initialize";
public const string OperationShow            = "showNotification";
public const string OperationSchedule        = "scheduleNotification";
public const string OperationCancelScheduled = "cancelScheduledNotification";
public const string OperationUpdateProgress  = "updateNotificationProgress";
public const string OperationSetBadge        = "setBadge";
public const string OperationRemoveById      = "removeNotificationById";
public const string OperationRemoveByTag     = "removeNotificationsByTag";
public const string OperationRemoveAll       = "removeAllNotifications";
public const string OperationGetAll          = "getAllNotifications";
public const string OperationOpenSettings    = "openNotificationSettings";
```

**DllImport 宣言**:

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void NotificationInvokedCallback(
    [MarshalAs(UnmanagedType.LPWStr)] string argsJson);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void initNotificationManager(
    NotificationInvokedCallback callback,
    [MarshalAs(UnmanagedType.Bool)] bool isPackaged,
    [MarshalAs(UnmanagedType.LPWStr)] string? clsid,
    [MarshalAs(UnmanagedType.LPWStr)] string? launchUri,
    out int pError);

[DllImport(DLL_NAME)]
private static extern void uninitNotificationManager();

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void showNotification(
    [MarshalAs(UnmanagedType.LPWStr)] string jsonPayload, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void scheduleNotification(
    [MarshalAs(UnmanagedType.LPWStr)] string jsonPayload,
    long scheduledTimeUnixMs, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void cancelScheduledNotification(
    [MarshalAs(UnmanagedType.LPWStr)] string tag,
    [MarshalAs(UnmanagedType.LPWStr)] string group, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void updateNotificationProgress(
    [MarshalAs(UnmanagedType.LPWStr)] string tag,
    [MarshalAs(UnmanagedType.LPWStr)] string group,
    double value,
    [MarshalAs(UnmanagedType.LPWStr)] string valueStr,
    [MarshalAs(UnmanagedType.LPWStr)] string status,
    uint sequenceNumber, out int pError);

[DllImport(DLL_NAME)]
private static extern void setBadge(int value, out int pError);

[DllImport(DLL_NAME)]
private static extern void removeNotificationById(uint notificationId, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void removeNotificationsByTag(
    [MarshalAs(UnmanagedType.LPWStr)] string tag,
    [MarshalAs(UnmanagedType.LPWStr)] string group, out int pError);

[DllImport(DLL_NAME)]
private static extern void removeAllNotifications(out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void getAllNotifications(
    IntPtr outJson, uint bufferSize, out int pError);

[DllImport(DLL_NAME)]
private static extern int getNotificationSetting();

[DllImport(DLL_NAME)]
private static extern void openNotificationSettings(out int pError);
```

**イベント公開**:

```csharp
// 全操作共通（pError 系操作）
public event Action<WindowsNotificationResult>? NotificationOperationCompleted;

// 通知タップ時（持続コールバック）
// argsJson はアプリ定義の JSON 文字列のため string のまま公開する
// 構造化型への変換はアプリ層の責任とする
public event Action<string>? NotificationInvoked;
```

`NotificationInvoked` を `string` で公開する理由: argsJson のキー構造はアプリが定義する（buttons の `args` / textBoxes の入力値）ため、共通型に固定できない。Mac 実装では `MacNotificationActionResult` があるが Windows では JSON 構造が多様なため string 公開を選択する。

**per-call onResult 設計**:

```csharp
// static per-call callback ストレージ（GC防止 + per-call Action 保持）
private static readonly NotificationInvokedCallback s_persistentInvokedDelegate = OnNotificationInvoked;
private static Action<WindowsNotificationResult>? s_onInitialize;
private static Action<WindowsNotificationResult>? s_onShow;
private static Action<WindowsNotificationResult>? s_onSchedule;
// ... 各操作ごとに定義

// Public API シグネチャ例
public void Initialize(bool isPackaged, string? clsid = null, string? launchUri = null,
    Action<WindowsNotificationResult>? onResult = null) { ... }

public void ShowNotification(string jsonPayload,
    Action<WindowsNotificationResult>? onResult = null) { ... }
```

**Awake / OnDestroy の設計**:

```csharp
private bool _initialized = false;

private void Awake()
{
    Debug.Log($"[{LogTag}][{nameof(Awake)}]");
    if (_instance == null)
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    else if (_instance != this)
    {
        Destroy(gameObject);
        return;
    }
    _ = UnityMainThreadDispatcher.Instance;
}

private void OnDestroy()
{
    Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
    if (_instance != this) return;
    if (_initialized)
    {
        uninitNotificationManager();
        _initialized = false;
    }
    _instance = null;
}
```

**プラットフォームガード**（全 public メソッドで適用）:

```csharp
public void ShowNotification(string jsonPayload, Action<WindowsNotificationResult>? onResult = null)
{
    Debug.Log($"[{LogTag}][{nameof(ShowNotification)}] jsonPayload: {jsonPayload}");
    if (Application.platform != RuntimePlatform.WindowsPlayer) return;
    // ...
}
```

**getAllNotifications のバッファ処理**（リトライ対応）:

```csharp
const uint DefaultBufferSize = 4096;
const uint MaxBufferSize = 65536;

private string? GetAllNotificationsInternal(out int pError)
{
    uint bufferSize = DefaultBufferSize;
    while (bufferSize <= MaxBufferSize)
    {
        IntPtr buf = Marshal.AllocHGlobal((int)bufferSize * 2); // bufferSize は wchar_t 単位 → * 2 でバイト数
        try
        {
            getAllNotifications(buf, bufferSize, out pError);
            if (pError == 0)
                return Marshal.PtrToStringUni(buf);
            if (pError == 5) // HRESULT_FAILURE: バッファ不足の可能性
            {
                bufferSize *= 2;
                continue;
            }
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }
    pError = 5;
    return null;
}
```

**スレッド契約**:

- `NotificationInvokedCallback` はバックグラウンドスレッドから来る可能性がある
- `UnityMainThreadDispatcher.Instance.Enqueue()` 経由で `NotificationInvoked` event を発火する

**メモリ契約**:

- `getAllNotifications` の `bufferSize` は wchar_t 単位（文字数）
- `AllocHGlobal` 引数は `bufferSize * 2`（バイト数）
- IntPtr バッファは必ず `try/finally` で `FreeHGlobal`

**エラー契約**:

- `pError == 0` → `WindowsNotificationResult.Success(operation)` を返す
- `pError != 0` → `WindowsNotificationResult.Failure(operation, pError)` を返す
  - `ErrorMessage` は `ErrorCodeToMessage(pError)` でマッピングして付与

**コーディングルール準拠**:

- 全 `public` / `override` / `MonoBehaviour` イベント関数の先頭に `Debug.Log($"[{LogTag}][{nameof(Method)}] ...")` を付与
- 全 `public` / `class` / `struct` / `enum` に XML ドキュメントコメントを付与
- `private const string LogTag = "WindowsNotificationManager";`

---

## 5. エラーケース一覧と返却仕様

| ケース | errorCode | isSuccess | ErrorMessage |
|--------|-----------|-----------|--------------|
| 成功 | 0 | true | null |
| 未初期化で操作呼び出し | 1 | false | "Not initialized" |
| OS 設定で通知無効 | 2 | false | "Notifications are disabled" |
| JSON ペイロード不正 | 3 | false | "Invalid JSON payload" |
| progress 更新対象が見つからない | 4 | false | "Progress notification not found" |
| WinRT HRESULT エラー（バッファ不足含む） | 5 | false | "WinRT HRESULT failure" |
| バッジ設定失敗 | 6 | false | "Badge operation failed" |
| パラメータ不正（ボタン 5 超過等） | 7 | false | "Invalid parameter" |
| `getNotificationSetting` エラー | ― | ― | `WindowsNotificationSetting.Unknown`（enum 特例 API） |

`getNotificationSetting` は `WindowsNotificationResult` 契約に乗らない特例 API。戻り値は `WindowsNotificationSetting` enum として返す。

---

## 6. 依存関係の実装順序

1. `WindowsNotificationResult.cs`（依存なし）
2. `WindowsNotificationPayloads.cs`（依存なし）
3. `WindowsNotificationJsonBuilder.cs`（Payloads に依存）
4. `WindowsNotificationManager.cs`（Result / JsonBuilder / UnityMainThreadDispatcher に依存）

---

## 7. テスト方針

| 種別 | 対象 | 内容 |
|------|------|------|
| EditMode | `WindowsNotificationJsonBuilder` | 正常ペイロードから期待 JSON 文字列が生成されること |
| EditMode | `WindowsNotificationJsonBuilder.Validate` | buttons 5 件超 → エラーメッセージを返すこと |
| EditMode | `WindowsNotificationJsonBuilder.Validate` | audio.loop=true + duration 未指定 → エラーメッセージを返すこと |
| EditMode | `WindowsNotificationJsonBuilder.Validate` | ボタンに args + invokeUri 同時指定 → エラーメッセージを返すこと |
| EditMode | `WindowsNotificationManager` | Awake で Singleton が生成されること |
| EditMode | `WindowsNotificationManager` | 重複 Awake で Destroy されること |
| EditMode | `WindowsNotificationManager` | `NotificationOperationCompleted` / `NotificationInvoked` event の subscribe / unsubscribe |
| EditMode | `WindowsNotificationResult` | `Success(operation)` で IsSuccess=true, ErrorCode=0, ErrorMessage=null |
| EditMode | `WindowsNotificationResult` | `Failure(operation, 2)` で ErrorMessage が "Notifications are disabled" |
| PlayMode | ― | 不要（native Bridge に依存する動作は手動確認で対応） |
| 手動確認（実機） | `Initialize` | `isPackaged=false` で初期化、errorCode=0 を確認 |
| 手動確認（実機） | `ShowNotification` | Toast が表示されること |
| 手動確認（実機） | `ScheduleNotification` | 指定秒後に Toast が表示されること |
| 手動確認（実機） | `NotificationInvoked` | Toast をタップして argsJson が `NotificationInvoked` event に届くこと |
| 手動確認（実機） | `SetBadge` | タスクバーアイコンにバッジが表示されること |
| 手動確認（実機） | `GetAllNotifications` | バッファ不足時のリトライが正常動作すること（要検証） |
| 手動確認（実機） | 通知無効状態 | `GetNotificationSetting` が `DisabledForUser` 等を返すこと |
| 手動確認（実機） | `OpenNotificationSettings` | 設定画面が開くこと |

PlayMode テストは native DLL に依存するため省略し、手動確認で代替する。

---

## 8. 要検証事項

- Unity スタンドアロン Windows アプリは unpackaged（MSIX なし）で動作するため `isPackaged=false` が基本。ただし CLSID・launchUri が必要かどうかは Unity の COM サーバー登録状況に依存するため、実機で動作確認が必要
- `initNotificationManager` を管理者権限で実行すると `Show()` がサイレントに失敗することがネイティブ側のコメントに記載あり。要確認
- `getAllNotifications` でバッファ不足時に pError=5（HRESULT_FAILURE）が返るかどうかを確認し、リトライ戦略の妥当性を検証する
- `getAllNotifications` のデフォルトバッファサイズ（4096 wchar_t）が実機で通知が多い場合に不足しないか確認が必要
