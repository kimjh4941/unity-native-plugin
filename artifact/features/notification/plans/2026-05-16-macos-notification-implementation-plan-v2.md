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
| `NotificationCancelScheduled` | `identifier` | なし（fire-and-forget） | 同じ |
| `NotificationCancelAllScheduled` | — | なし（fire-and-forget） | 同じ |
| `NotificationGetScheduled` | — | `NotificationJsonCallback` | 同じ |
| `NotificationGetDelivered` | — | `NotificationJsonCallback` | 同じ |
| `NotificationRemoveDelivered` | `identifier` | なし（fire-and-forget） | 同じ |
| `NotificationRemoveAllDelivered` | — | なし（fire-and-forget） | 同じ |
| `NotificationRegisterCategory` | `categoryJson` | `NotificationSimpleCallback` | 同じ |
| `NotificationRemoveCategory` | `identifier` | `NotificationSimpleCallback` | **iOS はコールバックなし** |
| `NotificationSetActionReceivedCallback` | `NotificationActionCallback` | — (永続登録) | 同じ |
| `NotificationSetTextInputActionReceivedCallback` | `NotificationTextInputActionCallback` | — (永続登録) | 同じ |
| `NotificationSetBadgeCount` | `count` | `NotificationSimpleCallback` | 同じ |
| `NotificationHasPermission` | — | `NotificationBoolCallback` | 同じ |
| `NotificationCancel` | `identifier` | なし（fire-and-forget） | 同じ |
| `NotificationCancelAll` | — | なし（fire-and-forget） | 同じ |

**コールバックなし API のポリシー:** `Cancel` / `CancelAll` / `CancelScheduled` / `CancelAllScheduled` / `RemoveDelivered` / `RemoveAllDelivered` は Bridge 側にコールバックがない。C# Manager はネイティブを呼び出すのみで `NotificationOperationCompleted` イベントは発火しない（fire-and-forget）。

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

#### Trigger JSON スキーマ

| type | フィールド | iOS との差分 |
|---|---|---|
| `"immediate"` | `{ "type": "immediate" }` | 同じ |
| `"timeInterval"` | `{ "type": "timeInterval", "seconds": 60.0, "repeats": false }` | 同じ |
| `"calendar"` | `{ "type": "calendar", "year": 2026, "month": 5, "day": 16, "hour": 9, "minute": 0, "second": 0, "repeats": false }` | 同じ |

**macOS は `location` トリガ非対応。** `NotificationSchedule` は triggerJson を必須とする（`NotificationShow` との違い）。

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

- `[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]` — 既存 `MacDialogManager.cs` と同様に `__Internal` を使用（macOS Standalone プラグインは Unity プロジェクト内で静的リンクされる）
- **macOS Bridge のネイティブシンボル名は PascalCase**（例: `NotificationSetup`, `NotificationShow`）。C# extern メソッド名はシンボル名に揃えて PascalCase とする（`EntryPoint` 属性は使用しない）
- コールバック delegate は `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` + `[MonoPInvokeCallback]` static メソッド
- GC 回収防止のため persistent delegate は `static readonly` フィールドに格納
- `#if UNITY_STANDALONE_OSX` で全クラスを囲む

---

## 2. IL2CPP / AOT 制約

以下の制約は IL2CPP ビルド（macOS Standalone）で遵守する。

| 制約 | 詳細 |
|---|---|
| `[MonoPInvokeCallback]` | ネイティブから呼ばれる static メソッドは必ず付与する。インスタンスメソッド・ラムダ不可 |
| generic / closure 禁止 | コールバック内でジェネリッククラスや匿名関数を使用しない |
| 例外禁止 | コールバック内で例外を投げない（native 側クラッシュ防止）。`try-catch` で握りつぶしてログ出力する |
| `string` マーシャリング | `const char*` → `string` の P/Invoke 自動マーシャリングを使用。コールバック内でポインタを直接保持しない |
| AOT 確認 | Mono（Editor）と IL2CPP（Standalone ビルド）の両方で動作確認を行う |

---

## 3. 変更ファイル一覧

### 3.1 新規作成

| ファイルパス | 内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationResult.cs` | SimpleCallback 向け操作結果型（errorCode フィールドを追加） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationJsonResult.cs` | JsonCallback 向け結果型（json + errorCode + errorMessage を保持） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationActionResult.cs` | アクションコールバック結果型 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationTextInputActionResult.cs` | テキスト入力アクションコールバック結果型 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationAuthorizationStatus.cs` | 認証ステータス enum + パーサ |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationPayloads.cs` | macOS 向け Payload 型（MacNotificationCategoryPayload・MacNotificationActionPayload） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationJsonBuilder.cs` | macOS 向け JSON 組み立てクラス（category スキーマが iOS と異なる。content/trigger ロジックは MacNotificationJsonBuilder 内に複製） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationManager.cs` | メイン Manager（Singleton・Bridge 呼び出し・イベント公開） |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Editor/Notification/MacNotificationResultTests.cs` | EditMode テスト（Result / AuthorizationStatus / JsonBuilder） |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/Notification/MacNotificationManagerTests.cs` | PlayMode テスト（Singleton / コールバック経路） |

各 .cs ファイルに対応する .meta は Unity Editor が自動生成する（手動作成不要）。

### 3.2 既存変更

なし（iOS/Android の既存ファイルは変更しない）

### 3.3 非変更（再利用）

| ファイルパス | 理由 |
|---|---|
| `Runtime/Notification/IosNotificationPayloads.cs` の `NotificationContentPayload` | compile guard なし、macOS でもそのまま使用可 |
| `Runtime/Notification/IosNotificationPayloads.cs` の `TimeIntervalTriggerPayload`, `CalendarTriggerPayload` | compile guard なし、macOS でもそのまま使用可 |
| `Runtime/Common/UnityMainThreadDispatcher.cs` | compile guard なし、共通 |

**IosNotificationJsonBuilder.cs は再利用しない。** content/trigger の JSON 組み立てロジックは `MacNotificationJsonBuilder` 内に複製する（iOS クラスを直接参照すると compile guard 導入時に破綻するため）。

---

## 4. 実装詳細

### 4.1 MacNotificationResult

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

### 4.2 MacNotificationJsonResult

`GetAuthorizationStatus` / `GetScheduled` / `GetDelivered` 用。`NotificationJsonCallback(json, errorCode, errorMessage)` の 3 引数を受ける。

```csharp
#nullable enable
#if UNITY_STANDALONE_OSX
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    public readonly struct MacNotificationJsonResult
    {
        public string Operation { get; }
        public string? Json { get; }         // 成功時: JSON 文字列, 失敗時: null
        public bool IsSuccess { get; }
        public int ErrorCode { get; }
        public string? ErrorMessage { get; }

        public static MacNotificationJsonResult Success(string operation, string json) =>
            new(operation, json, true, 0, null);

        public static MacNotificationJsonResult Failure(string operation, int errorCode, string? errorMessage) =>
            new(operation, null, false, errorCode, errorMessage);

        private MacNotificationJsonResult(string operation, string? json, bool isSuccess, int errorCode, string? errorMessage)
        {
            Operation = operation; Json = json; IsSuccess = isSuccess; ErrorCode = errorCode; ErrorMessage = errorMessage;
        }
    }
}
#endif
```

Manager の即時callback（per-call のみ、global event なし）:
- `GetAuthorizationStatus` → `Action<MacNotificationJsonResult>` (required)
- `GetScheduled` / `GetDelivered` → `Action<MacNotificationJsonResult>` (required)
- `HasPermission` → `Action<bool>` (required)

caller は `MacNotificationJsonResult.Json` を `MacNotificationAuthorizationStatusParser.Parse()` に渡して enum を取得する。

### 4.3 MacNotificationActionResult / MacNotificationTextInputActionResult

- `IosNotificationActionResult` / `IosNotificationTextInputActionResult` と同一構造
- compile guard を `#if UNITY_STANDALONE_OSX` に変更
- `UserInfo` は `IReadOnlyDictionary<string, string>?` で内部パース（同一ロジック複製）
- **型名を分離する理由:** iOS/macOS で Action イベントを異なる API として公開し、将来の差分対応を容易にするため

### 4.4 MacNotificationAuthorizationStatus

```csharp
public enum MacNotificationAuthorizationStatus { Authorized, Denied, NotDetermined, Provisional, Unsupported }
```

Parse: `"authorized"` / `"denied"` / `"notDetermined"` / `"provisional"` / `"unsupported"` → 対応 enum 値。`"ephemeral"` は macOS に存在しない。

### 4.5 MacNotificationPayloads

```csharp
#if UNITY_STANDALONE_OSX
public sealed class MacNotificationCategoryPayload
{
    public string Id { get; set; } = "";
    public MacNotificationActionPayload[] Actions { get; set; } = Array.Empty<MacNotificationActionPayload>();
}

public sealed class MacNotificationActionPayload
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsForeground { get; set; }
    public bool IsTextInput { get; set; }
    public string? TextInputPlaceholder { get; set; }
}
#endif
```

### 4.6 MacNotificationJsonBuilder

- `BuildContentJson(NotificationContentPayload)` → content/trigger ロジックを iOS JsonBuilder から複製（独立実装）
- `BuildTimeIntervalTriggerJson(TimeIntervalTriggerPayload)` → 複製
- `BuildCalendarTriggerJson(CalendarTriggerPayload)` → 複製
- `BuildCategoryJson(MacNotificationCategoryPayload)` → **macOS 固有:** キーを `"id"` に変更、action フィールドも `"id"` 使用、`sfSymbolName`/`options` 除外

### 4.7 MacNotificationManager

```csharp
#if UNITY_STANDALONE_OSX
public class MacNotificationManager : MonoBehaviour
{
    private const string LogTag = "MacNotificationManager";

    public const string OperationRequestPermission   = "requestPermission";
    public const string OperationGetAuthorizationStatus = "getAuthorizationStatus";
    public const string OperationOpenSettings        = "openSettings";
    public const string OperationShow                = "showNotification";
    public const string OperationSchedule            = "scheduleNotification";
    public const string OperationUpdate              = "updateNotification";
    public const string OperationGetScheduled        = "getScheduledNotifications";
    public const string OperationGetDelivered        = "getDeliveredNotifications";
    public const string OperationSetBadgeCount       = "setBadgeCount";
    public const string OperationRegisterCategory    = "registerCategory";
    public const string OperationRemoveCategory      = "removeCategory";

    // Singleton
    private static MacNotificationManager? _instance;
    public static MacNotificationManager Instance { get { ... } }

    // Events (SimpleCallback 系: per-call optional + global event)
    public event Action<MacNotificationResult>? NotificationOperationCompleted;
    // Events (Persistent callback 系: global event のみ)
    public event Action<MacNotificationActionResult>? NotificationActionReceived;
    public event Action<MacNotificationTextInputActionResult>? NotificationTextInputActionReceived;

    // Delegate 型 (5種)
    // DllImport (21関数, PascalCase シンボル名に揃える)
    // static readonly persistent delegates (2種: Action / TextInputAction)
    // static readonly per-operation delegates (8種: SimpleCallback, 3種: JsonCallback, 1種: BoolCallback)
    // static per-call user callbacks (SimpleCallback 系: Action<MacNotificationResult>?, JsonCallback 系: Action<MacNotificationJsonResult>, BoolCallback 系: Action<bool>)

    // Lifecycle: Awake → Initialize()
    // Public API: 全 Bridge 関数に対応するメソッド
    // Static AOT callbacks: [MonoPInvokeCallback] + UnityMainThreadDispatcher.Enqueue
}
#endif
```

**スレッド契約:** 全コールバックは `UnityMainThreadDispatcher.Instance.Enqueue(...)` 経由でメインスレッドに転送する。転送時に Manager が既に Destroy されている場合は null チェックを行い、イベント発火をスキップする。

**メモリ契約:** コールバック内の `const char*` ポインタはコールバック呼び出し中のみ有効。C# の P/Invoke マーシャリングが自動コピーするため追加の解放処理は不要。

**GC 防止契約:** Persistent delegate（ActionReceived, TextInputActionReceived）および per-operation delegate は `static readonly` フィールドに格納し、GC 回収を防ぐ。

**例外処理契約:** 全 `[MonoPInvokeCallback]` 内を `try-catch(Exception)` で囲み、例外をログ出力してから return する（native 側へのクラッシュ伝播防止）。

**Persistent callback 解除ポリシー:** Bridge 側に解除 API がないため、Manager 破棄時（OnDestroy）でも解除処理は行わない。Persistent delegate は `static readonly` のためプロセス終了まで保持される。

**依存関係の実装順序:**
1. `MacNotificationResult.cs`
2. `MacNotificationJsonResult.cs`
3. `MacNotificationActionResult.cs`
4. `MacNotificationTextInputActionResult.cs`
5. `MacNotificationAuthorizationStatus.cs`
6. `MacNotificationPayloads.cs`
7. `MacNotificationJsonBuilder.cs`
8. `MacNotificationManager.cs`

---

## 5. エラーケース一覧と返却仕様

| API | エラーケース | 結果型 | IsSuccess | ErrorCode | ErrorMessage |
|---|---|---|---|---|---|
| `RequestPermission` | OS が通知権限を拒否 | `MacNotificationResult` | false | 非ゼロ | エラー詳細文字列 |
| `GetAuthorizationStatus` | 取得失敗 | `MacNotificationJsonResult` | false | 非ゼロ | エラー詳細文字列 |
| `OpenSettings` | 設定画面を開けない | `MacNotificationResult` | false | 非ゼロ | エラー詳細文字列 |
| `Show` | contentJson パース失敗 | `MacNotificationResult` | false | 非ゼロ | "parseFailed: ..." |
| `Show` | スケジューリング失敗 | `MacNotificationResult` | false | 非ゼロ | エラー詳細文字列 |
| `Update` | identifier が見つからない | `MacNotificationResult` | false | 非ゼロ | エラー詳細文字列 |
| `Update` | JSON パース失敗 | `MacNotificationResult` | false | 非ゼロ | "parseFailed: ..." |
| `Schedule` | JSON パース失敗 | `MacNotificationResult` | false | 非ゼロ | "parseFailed: ..." |
| `Schedule` | スケジューリング失敗 | `MacNotificationResult` | false | 非ゼロ | エラー詳細文字列 |
| `GetScheduled` | 取得失敗 | `MacNotificationJsonResult` | false | 非ゼロ | エラー詳細文字列 |
| `GetDelivered` | 取得失敗 | `MacNotificationJsonResult` | false | 非ゼロ | エラー詳細文字列 |
| `RegisterCategory` | JSON パース失敗 | `MacNotificationResult` | false | 非ゼロ | "parseFailed: ..." |
| `RemoveCategory` | identifier が見つからない | `MacNotificationResult` | false | 非ゼロ | エラー詳細文字列 |
| `SetBadgeCount` | バッジ設定失敗 | `MacNotificationResult` | false | 非ゼロ | エラー詳細文字列 |
| `HasPermission` | — (bool 返却のみ) | `bool` | — | — | — |
| `Cancel` / `CancelAll` 等 | コールバックなし（fire-and-forget） | — | — | — | — |

成功時契約: `IsSuccess == true` ↔ `ErrorCode == 0` かつ `ErrorMessage == null`（MacNotificationResult / MacNotificationJsonResult 共通）

---

## 6. テスト方針

### EditMode テスト

**compile guard:** `MacNotificationResult`, `MacNotificationJsonResult`, `MacNotificationAuthorizationStatus`, `MacNotificationJsonBuilder`, `MacNotificationPayloads` は `#if UNITY_STANDALONE_OSX || UNITY_EDITOR` でガードし、macOS 以外の Editor でも EditMode テストが実行可能にする。Manager のみ `#if UNITY_STANDALONE_OSX` のままとする。

| 観点 | テストケース |
|---|---|
| `MacNotificationResult.Success` | `IsSuccess == true`, `ErrorCode == 0`, `ErrorMessage == null` |
| `MacNotificationResult.Failure` | `IsSuccess == false`, `ErrorCode == 指定値`, `ErrorMessage == 指定値` |
| `MacNotificationJsonResult.Success` | `IsSuccess == true`, `Json == 指定値` |
| `MacNotificationJsonResult.Failure` | `IsSuccess == false`, `Json == null` |
| `MacNotificationAuthorizationStatus` | 全ステータス文字列（5種）のパース正確性。未知文字列は `Unsupported` |
| `MacNotificationJsonBuilder.BuildContentJson` | 必須フィールド・省略フィールドの JSON 出力確認 |
| `MacNotificationJsonBuilder.BuildCategoryJson` | `"id"` キー使用、action フィールド確認、`sfSymbolName` 未出力 |
| `MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson` | `type="timeInterval"`, `seconds`, `repeats` |
| `MacNotificationJsonBuilder.BuildCalendarTriggerJson` | `type="calendar"`, 各日時フィールド |

### PlayMode テスト

| 観点 | テストケース |
|---|---|
| `MacNotificationManager.Instance` | Singleton 生成・DontDestroyOnLoad 確認 |
| `MacNotificationManager.Awake` | 二重生成時に後発インスタンスが Destroy されること |
| コールバック→Dispatcher→イベント経路 | `UnityMainThreadDispatcher` 経由でイベントが発火されることを確認（モック native ロジックで代替、要検証） |

### 手動確認（実機・Unity Editor macOS / IL2CPP Standalone ビルド）

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
| `Cancel` / `CancelAll` | 通知がキャンセルされること（fire-and-forget で問題ないことを確認） |
| `GetScheduled` | スケジュール済み通知の JSON 配列が返ること |
| `GetDelivered` | 配信済み通知の JSON 配列が返ること |
| `RemoveDelivered` / `RemoveAllDelivered` | 通知センターから通知が消えること |
| `RegisterCategory` | カテゴリが登録されアクションボタンが表示されること |
| `RemoveCategory` | カテゴリが削除されること |
| `NotificationActionReceived` | アクションボタンタップ時にイベントが発火すること |
| `NotificationTextInputActionReceived` | テキスト入力アクション時にイベントが発火すること |
| `SetBadgeCount` | アプリアイコンのバッジが更新されること |
| IL2CPP ビルド smoke test | Standalone ビルドで `RequestPermission` → `Show` → `ActionReceived` フローが動作すること |
