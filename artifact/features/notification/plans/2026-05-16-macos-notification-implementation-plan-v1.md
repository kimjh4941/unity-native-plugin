# 実装計画書

- 日付: 2026-05-16
- 機能名: macos-notification
- 対象プラットフォーム: macOS
- ブランチ: feature/UNT-3

---

## 1. 実装対象 API 一覧

### 1.1 native-toolkit macOS Bridge 確認結果

参照: `/Users/jonghyunkim/Desktop/native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/Notification/UnityMacNotificationManagerBridge.h/.m`

#### コールバック型

| C Bridge 型 | シグネチャ | iOS との差分 |
|---|---|---|
| `NotificationSimpleCallback` | `(bool isSuccess, int errorCode, const char* errorMessage)` | **errorCode 追加** |
| `NotificationJsonCallback` | `(const char* json, int errorCode, const char* errorMessage)` | **errorCode + errorMessage 追加** |
| `NotificationBoolCallback` | `(bool value)` | 同じ |
| `NotificationActionCallback` | `(const char* notificationId, const char* actionId, const char* userInfoJson)` | 同じ |
| `NotificationTextInputActionCallback` | `(const char* notificationId, const char* actionId, const char* userText, const char* userInfoJson)` | 同じ |

#### 公開関数一覧

| 関数名 | 引数 | コールバック型 | iOS との差分 |
|---|---|---|---|
| `NotificationSetup` | なし | なし | 名前のみ異なる |
| `NotificationRequestPermission` | — | `NotificationSimpleCallback` | 同じ |
| `NotificationGetAuthorizationStatus` | — | `NotificationJsonCallback` | 同じ |
| `NotificationOpenSettings` | — | `NotificationSimpleCallback` | **iOS はコールバックなし** |
| `NotificationShow` | `contentJson`, `triggerJson` | `NotificationSimpleCallback` | 同じ |
| `NotificationUpdate` | `identifier`, `contentJson`, `triggerJson` | `NotificationSimpleCallback` | 同じ |
| `NotificationSchedule` | `contentJson`, `triggerJson` | `NotificationSimpleCallback` | **iOS は identifier 引数あり、macOS はなし** |
| `NotificationCancelScheduled` | `identifier` | なし | 同じ |
| `NotificationCancelAllScheduled` | — | なし | 同じ |
| `NotificationGetScheduled` | — | `NotificationJsonCallback` | 同じ |
| `NotificationGetDelivered` | — | `NotificationJsonCallback` | 同じ |
| `NotificationRemoveDelivered` | `identifier` | なし | 同じ |
| `NotificationRemoveAllDelivered` | — | なし | 同じ |
| `NotificationRegisterCategory` | `categoryJson` | `NotificationSimpleCallback` | 同じ |
| `NotificationRemoveCategory` | `identifier` | `NotificationSimpleCallback` | **iOS はコールバックなし** |
| `NotificationSetActionReceivedCallback` | `NotificationActionCallback` | — (永続登録) | 同じ |
| `NotificationSetTextInputActionReceivedCallback` | `NotificationTextInputActionCallback` | — (永続登録) | 同じ |
| `NotificationSetBadgeCount` | `count` | `NotificationSimpleCallback` | 同じ |
| `NotificationHasPermission` | — | `NotificationBoolCallback` | 同じ |
| `NotificationCancel` | `identifier` | なし | 同じ |
| `NotificationCancelAll` | — | なし | 同じ |

#### Authorization Status JSON スキーマ

```json
{ "status": "authorized" | "denied" | "notDetermined" | "provisional" | "unsupported" }
```

iOS と同じキー・値（`"ephemeral"` は macOS では存在しない）。

#### Content JSON スキーマ（macOS JsonParser が使用するフィールド）

```json
{
  "id": "notif-001",
  "title": "Hello",
  "body": "World",
  "subtitle": "Sub",
  "categoryIdentifier": "cat-001",
  "badge": 1,
  "userInfo": { "key": "value" }
}
```

#### Category JSON スキーマ（macOS JsonParser が使用するフィールド）

```json
{
  "id": "cat-001",
  "actions": [
    { "id": "act-ok", "title": "OK", "isForeground": false, "isTextInput": false, "textInputPlaceholder": "..." }
  ]
}
```

**iOS との差分:** macOS は `"identifier"` ではなく `"id"` キーを使用する。actions の中も `"id"` キー。`sfSymbolName`・`options` は macOS では不要。

### 1.2 C# 側 DllImport 呼び出し方針

- `[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]` — iOS/macOS 共通
- コールバック delegate は `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` + `[MonoPInvokeCallback]` static メソッド
- GC 回収防止のため persistent delegate は `static readonly` フィールドに格納
- `#if UNITY_STANDALONE_OSX` で全クラスを囲む

---

## 2. 変更ファイル一覧

### 2.1 新規作成

| ファイルパス | 内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationResult.cs` | 操作結果型（errorCode フィールドを追加） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationActionResult.cs` | アクションコールバック結果型 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationTextInputActionResult.cs` | テキスト入力アクションコールバック結果型 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationAuthorizationStatus.cs` | 認証ステータス enum + パーサ |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationJsonBuilder.cs` | macOS 向け JSON 組み立てクラス（category スキーマが iOS と異なる） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationManager.cs` | メイン Manager（Singleton・Bridge 呼び出し・イベント公開） |

### 2.2 既存変更

なし（iOS/Android の既存ファイルは変更しない）

### 2.3 非変更（再利用）

| ファイルパス | 理由 |
|---|---|
| `Runtime/Notification/IosNotificationPayloads.cs` の `NotificationContentPayload` | compile guard なし、macOS でもそのまま使用可 |
| `Runtime/Notification/IosNotificationPayloads.cs` の `TimeIntervalTriggerPayload`, `CalendarTriggerPayload` | compile guard なし、macOS でもそのまま使用可 |
| `Runtime/Common/UnityMainThreadDispatcher.cs` | compile guard なし、共通 |
| `Runtime/Notification/IosNotificationJsonBuilder.cs` | content/trigger JSON 組み立て部分は再利用可（category のみ異なるため MacNotificationJsonBuilder で対応） |

---

## 3. 実装詳細

### 3.1 MacNotificationResult

```csharp
#nullable enable
#if UNITY_STANDALONE_OSX
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    public readonly struct MacNotificationResult
    {
        public string Operation { get; }
        public bool IsSuccess { get; }
        public int ErrorCode { get; }        // macOS 固有: iOS にはない
        public string? ErrorMessage { get; }

        public static MacNotificationResult Success(string operation) =>
            new(operation, true, 0, null);

        public static MacNotificationResult Failure(string operation, int errorCode, string? errorMessage) =>
            new(operation, false, errorCode, errorMessage);

        private MacNotificationResult(string operation, bool isSuccess, int errorCode, string? errorMessage)
        {
            Operation = operation; IsSuccess = isSuccess; ErrorCode = errorCode; ErrorMessage = errorMessage;
        }
    }
}
#endif
```

契約: `IsSuccess == true` のとき `ErrorCode == 0` かつ `ErrorMessage == null` を保証する。

### 3.2 MacNotificationActionResult / MacNotificationTextInputActionResult

- `IosNotificationActionResult` / `IosNotificationTextInputActionResult` と同一構造
- compile guard を `#if UNITY_STANDALONE_OSX` に変更
- `UserInfo` は `IReadOnlyDictionary<string, string>?` で内部パース（同一ロジック再実装）

### 3.3 MacNotificationAuthorizationStatus

```csharp
public enum MacNotificationAuthorizationStatus { Authorized, Denied, NotDetermined, Provisional, Unsupported }
```

Parse: `"authorized"` / `"denied"` / `"notDetermined"` / `"provisional"` / `"unsupported"` → 対応 enum 値。`"ephemeral"` は macOS に存在しない。

### 3.4 MacNotificationJsonBuilder

- `BuildContentJson(NotificationContentPayload)` → `IosNotificationJsonBuilder` と同一ロジック（macOS parser は `id`, `title`, `body`, `subtitle`, `categoryIdentifier`, `badge`, `userInfo` を使用）
- `BuildTimeIntervalTriggerJson(TimeIntervalTriggerPayload)` → 同一ロジック
- `BuildCalendarTriggerJson(CalendarTriggerPayload)` → 同一ロジック
- `BuildCategoryJson(MacNotificationCategoryPayload)` → **macOS 固有:** キーを `"id"` に変更、action フィールドも `"id"` 使用、`sfSymbolName`/`options` 除外

`MacNotificationCategoryPayload` は新規 sealed class として MacNotificationJsonBuilder.cs 内に定義する（`id`, `actions: MacNotificationActionPayload[]`）。`MacNotificationActionPayload` は `id`, `title`, `isForeground`, `isTextInput`, `textInputPlaceholder?` を持つ。

### 3.5 MacNotificationManager

```csharp
#if UNITY_STANDALONE_OSX
public class MacNotificationManager : MonoBehaviour
{
    private const string LogTag = "MacNotificationManager";

    public const string OperationRequestPermission   = "requestPermission";
    public const string OperationOpenSettings        = "openSettings";
    public const string OperationShow                = "showNotification";
    public const string OperationSchedule            = "scheduleNotification";
    public const string OperationUpdate              = "updateNotification";
    public const string OperationSetBadgeCount       = "setBadgeCount";
    public const string OperationRegisterCategory    = "registerCategory";
    public const string OperationRemoveCategory      = "removeCategory";

    // Singleton
    private static MacNotificationManager? _instance;
    public static MacNotificationManager Instance { get { ... } }

    // Events
    public event Action<MacNotificationResult>? NotificationOperationCompleted;
    public event Action<MacNotificationActionResult>? NotificationActionReceived;
    public event Action<MacNotificationTextInputActionResult>? NotificationTextInputActionReceived;

    // Delegate 型 (5種)
    // DllImport (21関数)
    // static readonly persistent delegates (2種: Action / TextInputAction)
    // static readonly per-operation delegates (8種: SimpleCallback)
    // static per-call user callbacks (8種: Action<MacNotificationResult>?)

    // Lifecycle: Awake → Initialize()
    // Public API: 全 Bridge 関数に対応するメソッド
    // Static AOT callbacks: [MonoPInvokeCallback] + UnityMainThreadDispatcher.Enqueue
}
```

**スレッド契約:** 全コールバック（`NotificationSimpleCallback`, `NotificationJsonCallback`, `NotificationBoolCallback`, `NotificationActionCallback`, `NotificationTextInputActionCallback`）は `UnityMainThreadDispatcher.Instance.Enqueue(...)` 経由でメインスレッドに転送する。

**メモリ契約:** コールバック内の `const char*` ポインタはコールバック呼び出し中のみ有効。C# の P/Invoke マーシャリングが自動コピーするため追加の解放処理は不要。

**GC 防止契約:** Persistent delegate（ActionReceived, TextInputActionReceived）および per-operation delegate は `static readonly` フィールドに格納し、GC 回収を防ぐ。

**依存関係の実装順序:**
1. `MacNotificationResult.cs`
2. `MacNotificationActionResult.cs`
3. `MacNotificationTextInputActionResult.cs`
4. `MacNotificationAuthorizationStatus.cs`
5. `MacNotificationJsonBuilder.cs`（`MacNotificationCategoryPayload` を含む）
6. `MacNotificationManager.cs`

---

## 4. エラーケース一覧と返却仕様

| API | エラーケース | IsSuccess | ErrorCode | ErrorMessage |
|---|---|---|---|---|
| `RequestPermission` | OS が通知権限を拒否 | false | 非ゼロ | エラー詳細文字列 |
| `GetAuthorizationStatus` | 取得失敗 | — (json=null) | 非ゼロ | エラー詳細文字列 |
| `OpenSettings` | 設定画面を開けない | false | 非ゼロ | エラー詳細文字列 |
| `Show` | contentJson パース失敗 | false | 非ゼロ | "parseFailed: ..." |
| `Show` | スケジューリング失敗 | false | 非ゼロ | エラー詳細文字列 |
| `Update` | identifier が見つからない | false | 非ゼロ | エラー詳細文字列 |
| `Update` | JSON パース失敗 | false | 非ゼロ | "parseFailed: ..." |
| `Schedule` | JSON パース失敗 | false | 非ゼロ | "parseFailed: ..." |
| `Schedule` | スケジューリング失敗 | false | 非ゼロ | エラー詳細文字列 |
| `GetScheduled` | 取得失敗 | — (json=null) | 非ゼロ | エラー詳細文字列 |
| `GetDelivered` | 取得失敗 | — (json=null) | 非ゼロ | エラー詳細文字列 |
| `RegisterCategory` | JSON パース失敗 | false | 非ゼロ | "parseFailed: ..." |
| `RemoveCategory` | identifier が見つからない | false | 非ゼロ | エラー詳細文字列 |
| `SetBadgeCount` | バッジ設定失敗 | false | 非ゼロ | エラー詳細文字列 |
| `HasPermission` | — (bool 返却のみ) | — | — | — |
| `Cancel` / `CancelAll` / `CancelScheduled` / 等 | コールバックなし（失敗は無視） | — | — | — |

成功時契約: `IsSuccess == true` ↔ `ErrorCode == 0` かつ `ErrorMessage == null`

---

## 5. テスト方針

### EditMode テスト（要実装）

| 観点 | テストケース |
|---|---|
| `MacNotificationResult.Success` | `IsSuccess == true`, `ErrorCode == 0`, `ErrorMessage == null` |
| `MacNotificationResult.Failure` | `IsSuccess == false`, `ErrorCode == 指定値`, `ErrorMessage == 指定値` |
| `MacNotificationAuthorizationStatus` | 全ステータス文字列のパース正確性 |
| `MacNotificationJsonBuilder.BuildContentJson` | 必須フィールド・省略フィールドの JSON 出力確認 |
| `MacNotificationJsonBuilder.BuildCategoryJson` | `"id"` キー使用、action フィールド確認 |
| `MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson` | type="timeInterval", seconds, repeats |
| `MacNotificationJsonBuilder.BuildCalendarTriggerJson` | type="calendar", 各日時フィールド |

### PlayMode テスト

| 観点 | テストケース |
|---|---|
| `MacNotificationManager.Instance` | Singleton 生成・DontDestroyOnLoad 確認 |
| `MacNotificationManager.Awake` | 二重生成時に後発インスタンスが Destroy されること |

### 手動確認（実機・Unity Editor macOS）

| 観点 | 手順 |
|---|---|
| `Initialize` | アプリ起動後に Setup が呼ばれること |
| `RequestPermission` | システム権限ダイアログが表示されること |
| `HasPermission` | 権限状態に応じた bool が返ること |
| `GetAuthorizationStatus` | JSON が正しくパースされ enum が返ること |
| `OpenSettings` | システム通知設定画面が開くこと |
| `Show` | 通知が即時表示されること |
| `Schedule` | 指定時刻に通知が表示されること |
| `Update` | 既存通知が更新されること |
| `Cancel` / `CancelAll` | 通知がキャンセルされること |
| `GetScheduled` | スケジュール済み通知の JSON 配列が返ること |
| `GetDelivered` | 配信済み通知の JSON 配列が返ること |
| `RemoveDelivered` / `RemoveAllDelivered` | 通知センターから通知が消えること |
| `RegisterCategory` | カテゴリが登録されアクションボタンが表示されること |
| `RemoveCategory` | カテゴリが削除されること |
| `NotificationActionReceived` | アクションボタンタップ時にイベントが発火すること |
| `NotificationTextInputActionReceived` | テキスト入力アクション時にイベントが発火すること |
| `SetBadgeCount` | アプリアイコンのバッジが更新されること |
