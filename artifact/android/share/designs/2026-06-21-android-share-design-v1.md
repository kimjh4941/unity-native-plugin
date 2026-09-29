# Android Share 実装計画書

- 日付: 2026-06-21
- 対象プラットフォーム: Android
- 機能: share
- 改訂: v1

---

## 0. スコープ

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/` 配下に Bridge + Manager + Payload + JsonBuilder + Result を新規実装する。
- native-toolkit の `UnityAndroidShareManager`（Kotlin）を `AndroidJavaObject` 経由で呼び出す。
- サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は本計画書の対象外。`design-sample-scene` スキルで別途設計する。
- ネイティブ側に存在する Sharesheet 表示・FileProvider 変換・Direct Share 登録・コールバック調停はこのプロジェクトで再実装しない。C# 側は JSON 組み立てと Bridge 呼び出しのみを担う。

---

## 1. native-toolkit 確認結果

### 参照パス（Unity 公開層）

- `/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityAndroidShareManager.kt`
- `/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityShareJsonParser.kt`
- `/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityShareSpecs.kt`
- ドメインエラー: `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/android/library/share/domain/error/ShareDomainError.kt`
- 実装本体（参考・再実装しない）: `.../android_library/src/main/java/android/library/share/data/repository/ShareRepositoryImpl.kt`

### プラグインクラス名

```
android.unity.share.UnityAndroidShareManager
```

`getInstance`（`@JvmStatic`）で Singleton を取得する。Notification / Dialog と同じ取得パターン。

### 公開メソッド一覧（C# から `AndroidJavaObject.Call` する対象）

すべて第1引数に `Context`（= currentActivity）を取り、第2引数に JSON 文字列を取る。戻り値なし（結果はリスナー経由）。

| ネイティブメソッド | 引数 | JSON スキーマ |
|------------------|------|--------------|
| `shareText` | `(Context, String shareJson)` | `{ "text", "title?", "subject?", "mimeType?"(default text/plain), "chooserActions?":[...], "previewTitle?", "previewThumbnailPath?" }` |
| `shareImage` | `(Context, String shareJson)` | `{ "filePath", "mimeType?"(default image/*) }` |
| `shareImages` | `(Context, String shareJson)` | `{ "filePaths":[...] }` |
| `shareFile` | `(Context, String shareJson)` | `{ "filePath" }` |
| `shareFiles` | `(Context, String shareJson)` | `{ "filePaths":[...] }` |
| `registerDirectShareTarget` | `(Context, String shareJson)` | `{ "id", "label", "iconBase64", "category?"(default android.shortcut.conversation) }` |
| `removeDirectShareTargets` | `(Context, String shareJson)` | `{ "ids":[...] }` |
| `shareWithCallback` | `(Context, String shareJson)` | shareText と同じスキーマ（`chooserActions` は未使用） |
| `cancelPendingShareCallback` | `(Context)` | なし |

リスナー登録・解除メソッド（引数なし / リスナー）:

| ネイティブメソッド | 引数 | 用途 |
|------------------|------|------|
| `setShareOperationListener` | `(ShareOperationListener)` | 結果リスナー登録 |
| `clearShareOperationListener` | `()` | リスナー解除 + pending callback キャンセル |

### chooserActions 配列の要素スキーマ

```
{ "label", "iconBase64", "intentAction?"(default android.intent.action.SEND) }
```

- API 34+（UPSIDE_DOWN_CAKE）のみ有効。下位 API では native 側で無視される。

### コールバックインターフェース

`android.unity.share.UnityAndroidShareManager$ShareOperationListener`

```kotlin
fun onShareOperation(operation: String, isSuccessful: Boolean, errorMessage: String?)  // shareWithCallback 以外の全操作
fun onShareResult(operation: String, selectedPackageName: String?)                      // shareWithCallback のアプリ選択結果
```

- 2 メソッドを持つ単一インターフェース。`AndroidJavaProxy` 1 個で両方を実装する。
- `onShareOperation`: `shareWithCallback` を除く全操作で成否を通知。`operation` は下記 OPERATION 定数。
- `onShareResult`: `shareWithCallback` 後、ユーザーがアプリを選択したとき `selectedPackageName`（取得不可時 null）を通知。
- コールバックスレッド: native 側は `mainHandler`（Android メインスレッド）から通知するが、Unity メインスレッドとは別。**必ず `UnityMainThreadDispatcher` 経由でイベント発火する。**

### OPERATION 定数（native の文字列値）

| 定数 | 値 |
|------|-----|
| `OPERATION_SHARE_TEXT` | `shareText` |
| `OPERATION_SHARE_IMAGE` | `shareImage` |
| `OPERATION_SHARE_IMAGES` | `shareImages` |
| `OPERATION_SHARE_FILE` | `shareFile` |
| `OPERATION_SHARE_FILES` | `shareFiles` |
| `OPERATION_REGISTER_DIRECT_SHARE_TARGET` | `registerDirectShareTarget` |
| `OPERATION_REMOVE_DIRECT_SHARE_TARGETS` | `removeDirectShareTargets` |
| `OPERATION_SHARE_WITH_CALLBACK` | `shareWithCallback` |
| `OPERATION_CANCEL_PENDING_SHARE_CALLBACK` | `cancelPendingShareCallback` |

### 返却仕様（isSuccess / errorMessage）

- 成功時: `onShareOperation(operation, true, null)`。
- 失敗時: `onShareOperation(operation, false, errorMessage)`。errorMessage は native 側で人間可読な英語文に整形済み（下記「4. エラーケース一覧」参照）。
- `shareWithCallback` のみ成否ではなく `onShareResult` でアプリ選択を通知する点に注意（操作開始自体の失敗は native 側の try/catch で `onShareOperation` ではなく throw → 例外文言になるため、要検証項目とする）。

### 重要な前提（要検証）

- **FileProvider 設定**: native は `FileProvider.getUriForFile(context, "${packageName}.fileprovider", file)` を使う。`shareImage(s)` / `shareFile(s)` / `previewThumbnailPath` を使う場合、利用アプリの `AndroidManifest.xml` に authority `${applicationId}.fileprovider` の `<provider>` と `file_paths` リソースが必要。**この設定が AAR（native-toolkit）側にマージ済みか、利用側プロジェクトで必要かは要検証。** マニフェスト統合が必要なら、本パッケージの `Plugins/Android/` に AndroidManifest 断片を同梱するか、マニュアルに前提として明記する（実装ではなくドキュメント／配置課題）。
- **共有対象ファイルのパス制約**: FileProvider の `file_paths` スコープ外（例: 任意の絶対パス）は `IllegalFileAccess` になる。`Application.persistentDataPath` / `temporaryCachePath` 等のスコープ内であることが前提。要検証。
- **Direct Share の `iconBase64`**: PNG/JPEG をエンコードした Base64 文字列を C# 側で用意する必要がある（`Texture2D.EncodeToPNG` → `Convert.ToBase64String` など）。本 Manager は文字列をそのまま渡すのみとし、画像生成はサンプル／呼び出し側の責務とする。

---

## 2. 既存 C# 実装 確認結果

### 参照パターン

| ファイル | 流用するパターン |
|---------|----------------|
| `Runtime/Notification/AndroidNotificationManager.cs` | Singleton、`getInstance` 取得、`currentActivity` 取得、`PrependContext`、`TryPrepareCall`、`AndroidJavaProxy` リスナー、`UnityMainThreadDispatcher` 経由のイベント発火、`OnDestroy` でのリスナー解除 |
| `Runtime/Notification/NotificationResult.cs` | `readonly struct` の Result 型（Operation / IsSuccess / ErrorMessage、`Success` / `Failure` ファクトリ） |
| `Runtime/Notification/AndroidNotificationJsonBuilder.cs` | 手書き JSON シリアライザ（`Dictionary<string, object?>` → 文字列、`AddIfNotNullOrWhiteSpace` 等の optional 付与、`AppendEscapedString` のエスケープ）。**外部 JSON ライブラリは使わず、この実装方式に準拠する** |
| `Runtime/Notification/AndroidNotificationPayloads.cs` | Payload を `[Serializable]` クラス／struct で定義し Builder に渡す方式 |
| `Runtime/Dialog/AndroidDialogManager.cs` | `event Action<...>` でのイベント公開、`PostToMainThread` ヘルパ |
| `Runtime/Common/UnityMainThreadDispatcher.cs` | クロススレッド転送 |

### namespace / 命名

- namespace: `JonghyunKim.NativeToolkit.Runtime.Share`
- クラス全体を `#if UNITY_ANDROID` で囲む（Notification/Dialog と同一方針）。
- `#nullable enable` を先頭に付与。

### 重複追加しないもの

- `UnityMainThreadDispatcher`、`IconConfiguration` は既存を流用。Share 専用の新規共通ユーティリティは作らない。

---

## 3. 実装詳細

### 3.1 変更ファイル一覧（`Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/` 配下）

新規作成:

| ファイル | 役割 |
|---------|------|
| `Share/AndroidShareManager.cs` | MonoBehaviour Singleton。Bridge 呼び出し + リスナープロキシ + イベント公開 |
| `Share/AndroidSharePayloads.cs` | `ShareTextPayload` / `ShareImagePayload` / `ShareImagesPayload` / `ShareFilePayload` / `ShareFilesPayload` / `ChooserActionPayload` / `DirectShareTargetPayload` / `RemoveDirectShareTargetsPayload` |
| `Share/AndroidShareJsonBuilder.cs` | 各 Payload → JSON 文字列。Notification の手書きシリアライザ方式を流用 |
| `Share/ShareOperationResult.cs` | `readonly struct`。操作成否（Operation / IsSuccess / ErrorMessage） |
| `Share/ShareCallbackResult.cs` | `readonly struct`。`shareWithCallback` のアプリ選択結果（Operation / SelectedPackageName） |

既存変更:

- なし（既存ファイルへの変更は発生しない想定）。

非変更（流用のみ）:

- `Runtime/Common/UnityMainThreadDispatcher.cs`
- `Runtime/Common/IconConfiguration.cs`

要検証（実装外の配置／ドキュメント課題）:

- `Plugins/Android/AndroidManifest.xml`（FileProvider provider 断片）の要否 → セクション 1「重要な前提」参照。

### 3.2 AndroidShareManager 設計

クラス構造（Notification Manager に準拠）:

```csharp
public class AndroidShareManager : MonoBehaviour
{
    private const string PluginClassName = "android.unity.share.UnityAndroidShareManager";
    private const string LogTag = "AndroidShareManager";

    // OPERATION 定数（native と同値）
    public const string OperationShareText = "shareText";
    // ... 全 9 種

    private static AndroidShareManager? _instance;
    private AndroidJavaObject? pluginInstance;
    private ShareOperationListenerProxy? operationListener;

    public event Action<ShareOperationResult>? ShareOperationCompleted;
    public event Action<ShareCallbackResult>? ShareCallbackReceived;

    public static AndroidShareManager Instance { get; }   // 既存 Singleton パターン
}
```

メソッド設計（公開 API）:

| C# メソッド | 引数 | 呼び出す native | 備考 |
|------------|------|----------------|------|
| `ShareText` | `(ShareTextPayload payload)` | `shareText` | Builder で JSON 化 |
| `ShareImage` | `(ShareImagePayload payload)` | `shareImage` | |
| `ShareImages` | `(ShareImagesPayload payload)` | `shareImages` | |
| `ShareFile` | `(ShareFilePayload payload)` | `shareFile` | |
| `ShareFiles` | `(ShareFilesPayload payload)` | `shareFiles` | |
| `RegisterDirectShareTarget` | `(DirectShareTargetPayload payload)` | `registerDirectShareTarget` | |
| `RemoveDirectShareTargets` | `(RemoveDirectShareTargetsPayload payload)` | `removeDirectShareTargets` | |
| `ShareWithCallback` | `(ShareTextPayload payload)` | `shareWithCallback` | 結果は `ShareCallbackReceived` で通知 |
| `CancelPendingShareCallback` | `()` | `cancelPendingShareCallback` | Context のみ渡す |

ライフサイクル / Bridge 呼び出し:

- `Awake`: Notification Manager と同一（`_instance` 設定、`DontDestroyOnLoad`、`UnityMainThreadDispatcher.Instance` 生成、`Initialize`）。
- `Initialize`: `Application.platform == Android` のときのみ `AndroidJavaClass(PluginClassName).CallStatic<AndroidJavaObject>("getInstance")` → `pluginInstance`。`operationListener` を生成し `pluginInstance.Call("setShareOperationListener", operationListener)`。
- 各操作メソッド: `TryPrepareCall` 相当で「Android 判定 → pluginInstance null 判定 → currentActivity 取得 → PrependContext」を共通化。`pluginInstance.Call(operationName, fullArgs)`。例外時は `Debug.LogError` + `ShareOperationCompleted` に `ShareOperationResult.Failure` を発火。
- `OnDestroy`: `pluginInstance.Call("clearShareOperationListener")` → `pluginInstance.Dispose()` → null 化、`_instance` クリア。

リスナープロキシ:

```csharp
private sealed class ShareOperationListenerProxy : AndroidJavaProxy
{
    public ShareOperationListenerProxy(AndroidShareManager owner)
        : base("android.unity.share.UnityAndroidShareManager$ShareOperationListener") { ... }

    void onShareOperation(string operation, bool isSuccessful, string? errorMessage); // → ShareOperationCompleted
    void onShareResult(string operation, string? selectedPackageName);                 // → ShareCallbackReceived
}
```

- 両メソッドとも `UnityMainThreadDispatcher.Instance.Enqueue` でメインスレッドに転送してからイベント発火。

### 3.3 契約

- **スレッド契約**: native コールバックは Android メインスレッドだが Unity メインスレッドとは別。全イベント発火を `UnityMainThreadDispatcher` 経由にする。Bridge 呼び出し（`Call`）は Unity メインスレッド前提。
- **メモリ契約**: `pluginInstance` は `OnDestroy` で `Dispose`。`currentActivity` は取得のたびに `using` で破棄（Notification の `using (activity)` 方式）。`AndroidJavaProxy` は Manager フィールドで保持し GC されないようにする。
- **エラー契約**: `IsSuccess == true` のとき `ErrorMessage == null` を保証（common.md 準拠）。native からの errorMessage はそのまま透過。Bridge 呼び出し自体の例外（activity null / Call 例外）は C# 側で `Failure` 化して発火。
- **`shareWithCallback` の例外系（要検証）**: native 側は `shareWithCallback` 開始失敗時に `executeOperation` の catch を通り `onShareOperation(OPERATION_SHARE_WITH_CALLBACK, false, ...)` を発火する（コード上 block 内 throw は executeOperation で捕捉される）。したがって失敗は `ShareOperationCompleted`、成功時のアプリ選択は `ShareCallbackReceived` の二経路になる。サンプル側で両方を購読する想定。要検証。

### 3.4 実装順序（依存関係）

1. `ShareOperationResult` / `ShareCallbackResult`（依存なし）
2. `AndroidSharePayloads`（依存なし）
3. `AndroidShareJsonBuilder`（Payloads に依存）
4. `AndroidShareManager`（上記すべて + Common に依存）
5. EditMode テスト（JsonBuilder 中心）

---

## 4. エラーケース一覧と返却仕様

native（`UnityAndroidShareManager.executeOperation`）が `onShareOperation(operation, false, errorMessage)` で返す全ケース:

| native 例外 | errorMessage（英語、native 整形済み） |
|------------|--------------------------------------|
| `ShareDomainError.EmptyContent` | `Share content is empty. Please provide text or a file path.` |
| `ShareDomainError.NoShareTarget` / `ActivityNotFoundException` | `No app available to handle this share request.` |
| `ShareDomainError.FileNotFound(path)` | `File not found: {path}` |
| `ShareDomainError.IllegalFileAccess(path)` | `File cannot be shared: {path}. Ensure the file is in a supported directory.` |
| `ShareDomainError.InvalidMimeType(mimeType)` | `Invalid MIME type: {mimeType}` |
| `ShareDomainError.DirectShareRegistrationFailed(reason)` | `Failed to register Direct Share target: {reason}` |
| `ShareDomainError.EmptyIdList` | `No shortcut IDs provided for removal.` |
| `ShareDomainError.EmptyFileList` | `No file paths provided for share.` |
| `ShareDomainError.InvalidBase64Icon(id)` | `Invalid icon data for Direct Share target: {id}` |
| `SecurityException` | `Security restriction while executing {operation}: {message}` |
| その他 `Exception` | `Failed to {operation}: {message}` |

C# Bridge 層が独自に `Failure` を返すケース（native 到達前）:

| 条件 | Operation | ErrorMessage（C# 側で英語生成） |
|------|-----------|-------------------------------|
| 非 Android プラットフォーム | 呼び出し操作名 | 呼び出さず `Debug.LogWarning`（イベント発火しない方針 / 要確認） |
| `pluginInstance == null`（未 Initialize） | 呼び出し操作名 | `{operation} could not be started.` |
| `currentActivity == null` | 呼び出し操作名 | `{operation} could not be started.` |
| `Call` 例外 | 呼び出し操作名 | 例外 message |

- native 側 JSON パース失敗（例: `text` 空）は native の `require` → `IllegalArgumentException` → `Failed to {operation}: ...` で返る。C# 側でも Builder で必須項目を検証し、空なら呼び出し前に弾くか native に委ねるかは実装時に統一する（推奨: native に委ね二重実装を避ける。要確認）。

---

## 5. テスト方針

| 種別 | 対象 | 内容 |
|------|------|------|
| EditMode | `AndroidShareJsonBuilder` | 各 Payload → JSON の出力検証。optional 項目の有無、エスケープ、`chooserActions` / `filePaths` / `ids` 配列、default 値（mimeType 未指定時に native default に委ねるため C# は出力しない、等）を検証 |
| EditMode | `ShareOperationResult` / `ShareCallbackResult` | `Success`/`Failure` ファクトリ、`IsSuccess==true → ErrorMessage==null` 不変条件 |
| PlayMode / 手動 | `AndroidShareManager` 初期化・イベント購読 | Singleton 生成、`Initialize` の no-op（非 Android）、リスナー登録/解除 |
| 手動（実機） | 各 share 操作 | Sharesheet 表示、画像/ファイル共有（FileProvider 経由）、Direct Share 登録/削除、`shareWithCallback` のアプリ選択コールバック、chooserActions（API 34+）。FileProvider 前提（セクション 1）の実機確認を含む |

- common.md の TDD 方針に従い、native 呼び出しを含まない JsonBuilder / Result を EditMode で重点的にカバー。
- Bridge 依存・FileProvider 依存は手動確認項目として明記。

---

## 6. 未確定・要検証事項

1. FileProvider（`${applicationId}.fileprovider`）のマニフェスト設定が AAR 側で完結しているか、利用側プロジェクト設定が必要か。必要なら `Plugins/Android/` への同梱 or マニュアル明記。
2. 共有可能なファイルパスのスコープ（`persistentDataPath` 等）の確定。
3. `shareWithCallback` の失敗系がすべて `onShareOperation` 経由で来ることの実機確認。
4. 非 Android プラットフォームでの呼び出し時、イベントを発火するか warning のみで握りつぶすか（Notification Manager の挙動に合わせる方針）。
5. C# 側で必須項目（text / filePath 等）を事前検証するか native に委ねるか（二重実装回避の観点で native 委譲を推奨）。
6. `mimeType` 等の default 値を C# で埋めず native default（text/plain・image/*）に委ねる方針の最終確認。
