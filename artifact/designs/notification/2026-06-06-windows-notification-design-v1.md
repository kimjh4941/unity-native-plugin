# Windows Notification 実装計画書

- 日付: 2026-06-06
- 対象プラットフォーム: Windows
- 機能: notification

---

## 1. native-toolkit 確認結果

### 参照パス

`C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary\WindowsNotificationManager.h`
`C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary\WindowsNotificationManagerInternal.h`
`C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary\WindowsNotificationManager.cpp`

### DLL 名

```csharp
private const string DLL_NAME = "UnityWindowsNativeToolkit.dll";        // Release
// or "UnityWindowsNativeToolkit-Debug.dll"                              // DEVELOPMENT_BUILD
```

WindowsDialogManager と同じ DLL から export される。

### コールバック型

```c
typedef void (*NotificationInvokedCallback)(const wchar_t* argsJson);
```

- `argsJson`: ネイティブ側がアクション引数 + ユーザー入力をマージした JSON オブジェクト文字列
- `{"actionKey":"value", "userInputKey":"value", ...}` 形式
- `initNotificationManager` 呼び出し時に一度だけ登録する（Mac の複数コールバック型と異なる）

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
| `getAllNotifications` | `(wchar_t* outJson, uint32_t bufferSize, DWORD* pError)` | バッファ出力型 |
| `getNotificationSetting` | `()` → `int` | 同期・コールバックなし |
| `openNotificationSettings` | `(DWORD* pError)` | ms-settings:notifications を開く |

### エラーコード定数

| 値 | 定数名 | 意味 |
|----|--------|------|
| 0 | `NOTIFICATION_SUCCESS` | 成功 |
| 1 | `NOTIFICATION_ERROR_NOT_INITIALIZED` | 未初期化 |
| 2 | `NOTIFICATION_ERROR_DISABLED` | OS 設定で通知無効 |
| 3 | `NOTIFICATION_ERROR_INVALID_PAYLOAD` | JSON パースエラー |
| 4 | `NOTIFICATION_ERROR_PROGRESS_NOT_FOUND` | progress update 対象が見つからない |
| 5 | `NOTIFICATION_ERROR_HRESULT_FAILURE` | WinRT HRESULT エラー |
| 6 | `NOTIFICATION_ERROR_BADGE_FAILED` | バッジ操作失敗 |
| 7 | `NOTIFICATION_ERROR_INVALID_PARAMETER` | パラメータ不正（ボタン 5 超過 / audio.loop に duration=long なし 等） |

### `getNotificationSetting` 戻り値

| 値 | 意味 |
|----|------|
| 0 | Enabled |
| 1 | DisabledForApplication |
| 2 | DisabledForUser |
| 3 | DisabledByGroupPolicy |
| 4 | DisabledByManifest |
| -1 | error |

### setBadge グリフ値

| 値 | グリフ |
|----|--------|
| -1 | alert |
| -2 | activity |
| -3 | newMessage |
| -4 | available |
| -5 | busy |
| -6 | away |

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

バリデーション制約:
- `buttons` 最大 5 件
- `audio.loop: true` は `duration: "long"` が必須
- ボタンに `args` と `invokeUri` を同時指定不可

### getAllNotifications 出力 JSON フォーマット

```json
[{"id": 1, "tag": "my-tag", "group": "my-group"}, ...]
```

---

## 2. 既存 C# 実装確認結果

### 参照パス

`Packages/com.jonghyunkim.nativetoolkit/Runtime/`

### 既存パターン（MacNotificationManager を参照）

- コンパイルガード: `#if UNITY_STANDALONE_OSX` → Windows は `#if UNITY_STANDALONE_WIN`
- Singleton: `_instance` + `DontDestroyOnLoad` + `Awake` / `OnDestroy`
- `Awake` で `_ = UnityMainThreadDispatcher.Instance` を呼んでメインスレッドで初期化確保
- static delegate フィールドで GC 防止
- `[MonoPInvokeCallback]` + `UnityMainThreadDispatcher.Instance.Enqueue()` で callback → メインスレッド転送
- Result struct: `Operation` / `IsSuccess` / `ErrorCode` / `ErrorMessage` の readonly struct
- `NotificationOperationCompleted` event（全操作共通）と操作固有 per-call `Action<T>?` の2段構成

### 既存ファイル（変更なし）

| ファイル | 用途 |
|---------|------|
| `Common/UnityMainThreadDispatcher.cs` | コールバックのメインスレッド転送 |
| `Common/IconConfiguration.cs` | アイコン設定（通知では不使用） |
| `Notification/MacNotificationManager.cs` | macOS 実装（参照パターン） |
| `Notification/MacNotificationResult.cs` | 結果型（参照パターン） |
| `Notification/MacNotificationJsonBuilder.cs` | JSON ビルダー（参照パターン） |
| `Notification/MacNotificationPayloads.cs` | ペイロード型（参照パターン） |
| その他プラットフォーム実装 | 変更なし |

### 重複しないこと

- `UnityMainThreadDispatcher` は再実装しない
- `NotificationContentPayload` / `NotificationResult` 等の共通型は既存のものを確認して再利用を検討する

---

## 3. 変更ファイル一覧

### 新規作成

| ファイルパス | 内容 |
|------------|------|
| `Runtime/Notification/WindowsNotificationResult.cs` | 操作結果 readonly struct |
| `Runtime/Notification/WindowsNotificationPayloads.cs` | ペイロード型（JsonBuilder 用の入力型） |
| `Runtime/Notification/WindowsNotificationJsonBuilder.cs` | JSON 組み立て static class |
| `Runtime/Notification/WindowsNotificationManager.cs` | Singleton Manager（Bridge + event 公開） |
| `Runtime/UI/Windows/Notification/WindowsNotificationManagerExampleController.cs` | サンプル UI コントローラ |

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
    public readonly struct WindowsNotificationResult
    {
        public string Operation { get; }
        public bool IsSuccess { get; }
        public int ErrorCode { get; }
        public string? ErrorMessage { get; }

        public static WindowsNotificationResult Success(string operation) => new(operation, true, 0, null);
        public static WindowsNotificationResult Failure(string operation, int errorCode, string? errorMessage = null)
            => new(operation, false, errorCode, errorMessage);

        private WindowsNotificationResult(string operation, bool isSuccess, int errorCode, string? errorMessage)
        { ... }
    }
}
#endif
```

### 4-2. WindowsNotificationPayloads.cs

Mac の `NotificationContentPayload` 等を参考に Windows 通知専用ペイロード構造体を定義する。

| 型名 | 主なフィールド |
|------|--------------|
| `WindowsNotificationPayload` | title, body, tag, group, scenario, duration, buttons, textBoxes, audio, progress, expiration, attribution |
| `WindowsNotificationButtonPayload` | label, args (Dictionary), invokeUri |
| `WindowsNotificationTextBoxPayload` | id, placeholder, title |
| `WindowsNotificationAudioPayload` | src, loop |
| `WindowsNotificationProgressPayload` | value, valueStr, status |

### 4-3. WindowsNotificationJsonBuilder.cs

```csharp
public static class WindowsNotificationJsonBuilder
{
    public static string BuildNotificationPayload(WindowsNotificationPayload payload) { ... }
    // 必要に応じて各サブオブジェクトのビルドメソッドを追加
}
```

- `JsonUtility` または `System.Text.Json` は使わず、手動で Dictionary + StringBuilder で組み立てる（Mac JsonBuilder パターンに準拠）
- ネスト構造（buttons 配列等）は SerializeObject ヘルパーで再帰的に組み立てる

### 4-4. WindowsNotificationManager.cs

**DllImport 宣言**:

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void NotificationInvokedCallback([MarshalAs(UnmanagedType.LPWStr)] string argsJson);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void initNotificationManager(
    NotificationInvokedCallback callback, bool isPackaged,
    [MarshalAs(UnmanagedType.LPWStr)] string? clsid,
    [MarshalAs(UnmanagedType.LPWStr)] string? launchUri,
    out int pError);

[DllImport(DLL_NAME)]
private static extern void uninitNotificationManager();

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void showNotification([MarshalAs(UnmanagedType.LPWStr)] string jsonPayload, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void scheduleNotification(
    [MarshalAs(UnmanagedType.LPWStr)] string jsonPayload, long scheduledTimeUnixMs, out int pError);

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
private static extern void getAllNotifications(IntPtr outJson, uint bufferSize, out int pError);

[DllImport(DLL_NAME)]
private static extern int getNotificationSetting();

[DllImport(DLL_NAME)]
private static extern void openNotificationSettings(out int pError);
```

**イベント公開**:

```csharp
// 全操作共通
public event Action<WindowsNotificationResult>? NotificationOperationCompleted;
// 通知タップ時（持続コールバック）
public event Action<string>? NotificationInvoked;   // argsJson をそのまま公開
```

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

**Awake / Initialize の分離**:

- Mac とは異なり `initNotificationManager` に `isPackaged` / `clsid` / `launchUri` が必要
- `Awake` では `UnityMainThreadDispatcher` の確保のみ行う
- `Initialize(bool isPackaged, string? clsid = null, string? launchUri = null)` を明示的 public API として公開
- `OnDestroy` で `uninitNotificationManager()` を呼ぶ

**コールバックの設計（Mac との違い）**:

- Mac は操作ごとに都度コールバックを渡すが、Windows は `initNotificationManager` で一度だけ `NotificationInvokedCallback` を登録する
- `showNotification` 等の操作結果は `out int pError` で同期的に得られるため、コールバックは不要
- `NotificationInvokedCallback` はユーザーが通知をタップしたときのみ発火する
- static persistent delegate を保持して GC を防ぐ

**getAllNotifications のバッファ処理**:

```csharp
IntPtr buf = Marshal.AllocHGlobal((int)bufferSize * 2);
try
{
    getAllNotifications(buf, bufferSize, out int pError);
    // Marshal.PtrToStringUni(buf) で JSON 文字列取得
}
finally
{
    Marshal.FreeHGlobal(buf);
}
```

WindowsDialogManager の ShowMultiFileDialog と同パターン。

**スレッド契約**:

- `NotificationInvokedCallback` はバックグラウンドスレッドから来る可能性がある
- `UnityMainThreadDispatcher.Instance.Enqueue()` 経由で `NotificationInvoked` event を発火する

**メモリ契約**:

- `getAllNotifications` の IntPtr バッファは必ず `try/finally` で `FreeHGlobal`

**エラー契約**:

- `pError == 0` → `IsSuccess = true`, `ErrorMessage = null`
- `pError != 0` → `IsSuccess = false`, `ErrorCode = pError 値`

### 4-5. WindowsNotificationManagerExampleController.cs

- `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` でガード
- `WindowsNotificationManager.Instance` を使用
- `NotificationOperationCompleted` / `NotificationInvoked` event を購読してログ表示
- 実行ボタン一覧（最低限）:
  - Initialize（isPackaged=false, clsid/launchUri は空）
  - Show（シンプルなタイトル + ボディ）
  - Schedule（5秒後）
  - Cancel Scheduled
  - Remove All
  - Get Notification Setting
  - Open Settings
  - Set Badge（数値入力）

---

## 5. エラーケース一覧と返却仕様

| ケース | errorCode | isSuccess |
|--------|-----------|-----------|
| 成功 | 0 | true |
| 未初期化で操作呼び出し | 1 | false |
| OS 設定で通知無効 | 2 | false |
| JSON ペイロード不正 | 3 | false |
| progress 更新対象が見つからない | 4 | false |
| WinRT HRESULT エラー | 5 | false |
| バッジ設定失敗 | 6 | false |
| パラメータ不正（ボタン 5 超過等） | 7 | false |
| `getNotificationSetting` エラー | -1（戻り値） | ― |

---

## 6. 依存関係の実装順序

1. `WindowsNotificationResult.cs`（依存なし）
2. `WindowsNotificationPayloads.cs`（依存なし）
3. `WindowsNotificationJsonBuilder.cs`（Payloads に依存）
4. `WindowsNotificationManager.cs`（Result / JsonBuilder / UnityMainThreadDispatcher に依存）
5. `WindowsNotificationManagerExampleController.cs`（Manager に依存）

---

## 7. テスト方針

| 種別 | 対象 | 内容 |
|------|------|------|
| EditMode | `WindowsNotificationJsonBuilder` | 各 payload 型から期待 JSON 文字列が生成されることを確認 |
| EditMode | `WindowsNotificationManager` | Awake で Singleton が生成されること / 重複 Awake で Destroy されること |
| EditMode | `WindowsNotificationManager` | `NotificationOperationCompleted` / `NotificationInvoked` event の subscribe / unsubscribe |
| 手動確認（実機） | `Initialize` | `isPackaged=false` で初期化、エラーコード 0 を確認 |
| 手動確認（実機） | `ShowNotification` | Toast が表示されること |
| 手動確認（実機） | `ScheduleNotification` | 指定秒後に Toast が表示されること |
| 手動確認（実機） | `NotificationInvoked` | Toast をタップして argsJson が ExampleController に届くこと |
| 手動確認（実機） | `SetBadge` | タスクバーアイコンにバッジが表示されること |
| 手動確認（実機） | 通知無効状態 | `getNotificationSetting` が 0 以外を返すこと / `openNotificationSettings` で設定が開くこと |

---

## 8. 要検証事項

- Unity スタンドアロン Windows アプリは unpackaged（MSIX なし）で動作するため `isPackaged=false` が基本。ただし CLSID・launchUri が必要かどうかは Unity の COM サーバー登録状況に依存するため、実機で動作確認が必要
- `initNotificationManager` を管理者権限で実行すると `Show()` がサイレントに失敗することがネイティブ側のコメントに記載あり。要確認
- `getAllNotifications` のバッファサイズ（デフォルト値）は実機で通知が多い場合に不足しないか確認が必要
