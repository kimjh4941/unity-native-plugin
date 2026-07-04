# Android Share 実装計画書

- 日付: 2026-06-21
- 対象プラットフォーム: Android
- 機能: share
- 改訂: v2（v1 レビュー結果を反映）

---

## 0. スコープ

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/` 配下に Bridge + Manager + Payload + JsonBuilder + Result を新規実装する。
- native-toolkit の `UnityAndroidShareManager`（Kotlin）を `AndroidJavaObject` 経由で呼び出す。
- サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は本計画書の対象外。`design-sample-scene` スキルで別途設計する。
- ネイティブ側に存在する Sharesheet 表示・FileProvider 変換・Direct Share 登録・コールバック調停はこのプロジェクトで再実装しない。C# 側は JSON 組み立てと Bridge 呼び出しのみを担う。
- FileProvider は native-toolkit の `android_library` AAR に内包済み。本プロジェクトでは更新済み AAR の差し替えと統合確認を対象に含める（セクション 7 参照）。

### v1 からの主な変更点（レビュー反映）

| # | 反映内容 |
|---|---------|
| 高1 | `shareWithCallback` の通知契約を native 実装に合わせ、起動成否（`ShareOperationCompleted`）と選択結果（`ShareCallbackReceived`）の二経路 + 成功時二段階通知に修正（セクション 1.7 / 3.3） |
| 高2 | FileProvider を「要検証」から必須前提へ格上げ。native-toolkit のライブラリ manifest + 専用 paths リソースに実装済みで、AAR 経由で配布する方針に確定。本リポジトリは AAR 差し替えのみ（セクション 3.1 / 7） |
| 中1 | エラー契約を parser / use case / repository / C# Bridge の層別に整理し、Unity が実際に受け取る文言に限定（セクション 4） |
| 中2 | テストファイルを変更一覧に追加。Builder/Payload/Result を非プラットフォームガードにして EditMode テスト対象化（セクション 3.1 / 5） |
| 中3 | 非 Android / 未初期化 / activity null をすべて既存 Manager と同じ failure イベントに統一（セクション 3.3 / 4） |
| 不足 | chooserActions receiver の配置責任、`file_paths.xml` の公開範囲、callback 非通知時の状態遷移、IL2CPP の proxy 制約、完全な変更一覧を追記 |
| 追加 | 共通イベント（`ShareOperationCompleted` / `ShareCallbackReceived`）に加え、各操作メソッドに optional な per-call callback（`Action<...>? onResult = null`）を併用提供。`IosNotificationManager` の per-call callback 方式に準拠し、購読側が `operation` で分岐せず結果を受け取れるようにする。callback は任意で、未指定でも共通イベントは常に発火する（セクション 3.2） |

---

## 1. native-toolkit 確認結果

### 1.1 参照パス（Unity 公開層）

- `/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityAndroidShareManager.kt`
- `/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityShareJsonParser.kt`
- `/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityShareSpecs.kt`
- ドメインエラー: `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/android/library/share/domain/error/ShareDomainError.kt`
- 実装本体（参考・再実装しない）: `.../android_library/src/main/java/android/library/share/data/repository/ShareRepositoryImpl.kt`

### 1.2 プラグインクラス名

```
android.unity.share.UnityAndroidShareManager
```

`getInstance`（`@JvmStatic`）で Singleton を取得する。Notification / Dialog と同じ取得パターン。

### 1.3 公開メソッド一覧（C# から `AndroidJavaObject.Call` する対象）

すべて第1引数に `Context`（= currentActivity）を取り、第2引数に JSON 文字列を取る（`cancelPendingShareCallback` は Context のみ）。戻り値なし（結果はリスナー経由）。

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

リスナー登録・解除メソッド:

| ネイティブメソッド | 引数 | 用途 |
|------------------|------|------|
| `setShareOperationListener` | `(ShareOperationListener)` | 結果リスナー登録 |
| `clearShareOperationListener` | `()` | リスナー解除 + pending callback キャンセル |

### 1.4 chooserActions 配列の要素スキーマ

```
{ "label", "iconBase64", "intentAction?"(default android.intent.action.SEND) }
```

- API 34+（UPSIDE_DOWN_CAKE）のみ有効。下位 API では native 側で無視される。
- `intentAction` で発火されるブロードキャストを受ける receiver はアプリ側の責務（セクション 6 参照）。

### 1.5 コールバックインターフェース

`android.unity.share.UnityAndroidShareManager$ShareOperationListener`

```kotlin
fun onShareOperation(operation: String, isSuccessful: Boolean, errorMessage: String?)  // 全操作の起動成否
fun onShareResult(operation: String, selectedPackageName: String?)                      // shareWithCallback のアプリ選択結果
```

- 2 メソッドを持つ単一インターフェース。`AndroidJavaProxy` 1 個で両方を実装する。
- コールバックスレッド: native 側は `mainHandler`（Android メインスレッド）から通知するが、Unity メインスレッドとは別。**必ず `UnityMainThreadDispatcher` 経由でイベント発火する。**

### 1.6 OPERATION 定数（native の文字列値）

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

### 1.7 `shareWithCallback` の通知契約（v1 から訂正・高優先度1）

native の `UnityAndroidShareManager.executeOperation` は、全操作で `block()` 成功後に `onShareOperation(name, true, null)` を発火する。`shareWithCallback` も例外ではない。したがって実際の通知シーケンスは以下の通り:

1. `shareWithCallback` 呼び出し → Sharesheet 起動成功時に **`onShareOperation("shareWithCallback", true, null)` が発火**（= `ShareOperationCompleted`）。
2. ユーザーがアプリを選択した場合のみ、後続で **`onShareResult("shareWithCallback", selectedPackageName)` が発火**（= `ShareCallbackReceived`）。
3. 起動自体が失敗した場合は `onShareOperation("shareWithCallback", false, errorMessage)`（`block` 内 throw は `executeOperation` の catch で捕捉される）。

注意（不足項目反映）:

- ユーザーが Sharesheet を**キャンセル / Copy / Edit** した場合、`onShareResult` は**通知されない**ことがある（`PendingIntent.intentSender` がアプリ選択時にのみ発火するため）。
- このため「`ShareOperationCompleted` で起動成功を受けた後、`ShareCallbackReceived` が来ないまま終わる」状態が正常系として存在する。サンプル UI 側はタイムアウトや「結果なし」を許容する設計にする（`design-sample-scene` へ申し送り）。
- 連続して `shareWithCallback` を呼ぶ前に `cancelPendingShareCallback`（または `clearShareOperationListener`）で前回の pending receiver を解除する運用を推奨。

### 1.8 返却仕様（isSuccess / errorMessage）

- 成功時: `onShareOperation(operation, true, null)`（`shareWithCallback` 含む全操作）。
- 失敗時: `onShareOperation(operation, false, errorMessage)`。errorMessage は native 側で整形済み（セクション 4 参照）。
- `isSuccess == true` のとき `errorMessage == null` を C# 側でも保証する。

---

## 2. 既存 C# 実装 確認結果

### 2.1 参照パターン

| ファイル | 流用するパターン |
|---------|----------------|
| `Runtime/Notification/AndroidNotificationManager.cs` | Singleton、`getInstance` 取得、`currentActivity` 取得、`PrependContext`、`TryPrepareCall`、`AndroidJavaProxy` リスナー、`UnityMainThreadDispatcher` 経由のイベント発火、`OnDestroy` でのリスナー解除、`Failure(operation, "{operation} could not be started.")` の準備失敗通知 |
| `Runtime/Notification/NotificationResult.cs` | `readonly struct` の Result 型（Operation / IsSuccess / ErrorMessage、`Success` / `Failure` ファクトリ） |
| `Runtime/Notification/AndroidNotificationJsonBuilder.cs` | 手書き JSON シリアライザ（`Dictionary<string, object?>` → 文字列、`AddIfNotNullOrWhiteSpace` 等の optional 付与、`AppendEscapedString` のエスケープ）。**外部 JSON ライブラリは使わず、この実装方式に準拠する。プラットフォームガードなし（Editor でコンパイル・テスト可能）** |
| `Runtime/Notification/AndroidNotificationPayloads.cs` | Payload を `[Serializable]` クラス／struct で定義し Builder に渡す方式。**プラットフォームガードなし** |
| `Runtime/Dialog/AndroidDialogManager.cs` | `event Action<...>` でのイベント公開、`PostToMainThread` ヘルパ |
| `Tests/Runtime/AndroidNotificationJsonBuilderTests.cs` | NUnit EditMode テストの書き方。`#if UNITY_ANDROID` ガードなしで Builder/Payload を直接検証 |

### 2.2 既存テスト構成の確認結果（中優先度2 の根拠）

- `Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef` は `includePlatforms: ["Editor"]`、`optionalUnityReferences: ["TestAssemblies"]`。**EditMode 専用**。
- 既存テストは `AndroidNotificationJsonBuilder`（非ガード）を対象にしており、`#if UNITY_ANDROID` で囲まれた Manager / Result はテスト対象外。
- 結論: **Builder と Payload と Result を `#if UNITY_ANDROID` で囲まない**ことで EditMode テスト可能にする。Manager のみ `#if UNITY_ANDROID` で囲む（`AndroidJavaObject` 依存のため）。

### 2.3 namespace / 命名

- namespace: `JonghyunKim.NativeToolkit.Runtime.Share`
- `#nullable enable` を全ファイル先頭に付与。
- 重複追加しないもの: `UnityMainThreadDispatcher`、`IconConfiguration` は既存を流用。

---

## 3. 実装詳細

### 3.1 変更ファイル一覧

※ `.meta` ファイルは Unity が自動生成するため、作成対象には含めない。

新規作成（Runtime）:

| ファイル | プラットフォームガード | 役割 |
|---------|--------------------|------|
| `Runtime/Share/AndroidShareManager.cs` | `#if UNITY_ANDROID` で全体 | MonoBehaviour Singleton。Bridge 呼び出し + リスナープロキシ + イベント公開 |
| `Runtime/Share/AndroidSharePayloads.cs` | なし | 各 Payload struct/class |
| `Runtime/Share/AndroidShareJsonBuilder.cs` | なし | Payload → JSON 文字列 |
| `Runtime/Share/ShareOperationResult.cs` | なし | 操作成否 `readonly struct` |
| `Runtime/Share/ShareCallbackResult.cs` | なし | `shareWithCallback` 選択結果 `readonly struct` |

新規作成（Tests）:

| ファイル | 内容 |
|---------|------|
| `Tests/Runtime/AndroidShareJsonBuilderTests.cs` | 各 Payload → JSON の EditMode 検証 |
| `Tests/Runtime/ShareResultTests.cs` | `ShareOperationResult` / `ShareCallbackResult` の不変条件検証 |

既存変更:

- `Plugins/Android/android-native-toolkit-1.1.0.aar`（native-toolkit の FileProvider 実装を含むビルド成果物へ差し替え）

非変更（流用のみ）:

- `Runtime/Common/UnityMainThreadDispatcher.cs`
- `Runtime/Common/IconConfiguration.cs`
- `Plugins/Android/unity-android-native-toolkit-1.1.0.aar`
- `Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`（参照に `NativeToolkit.Runtime` を既に含むため追加不要）

### 3.2 AndroidShareManager 設計

```csharp
#if UNITY_ANDROID
public class AndroidShareManager : MonoBehaviour
{
    private const string PluginClassName = "android.unity.share.UnityAndroidShareManager";
    private const string LogTag = "AndroidShareManager";

    public const string OperationShareText = "shareText";
    // ... 全 9 種（セクション 1.6 と同値）

    private static AndroidShareManager? _instance;
    private AndroidJavaObject? pluginInstance;
    private ShareOperationListenerProxy? operationListener;

    // 共通イベント（常に発火・横断ハンドリング用）
    public event Action<ShareOperationResult>? ShareOperationCompleted;
    public event Action<ShareCallbackResult>? ShareCallbackReceived;

    // 操作別の per-call callback（任意・operation 文字列をキーに保持）
    // 注: 同一操作を連続呼び出しした場合は last-registered wins（IosNotificationManager と同方針）
    private readonly Dictionary<string, Action<ShareOperationResult>?> _pendingOperationCallbacks = new();
    private Action<ShareCallbackResult>? _pendingShareSelectedCallback;

    public static AndroidShareManager Instance { get; }   // 既存 Singleton パターン
}
#endif
```

公開 API:

- すべての操作メソッドは共通イベントに加えて optional な per-call callback を受け取る。callback は任意で、`null` でも共通イベントは発火する。
- callback 引数の型は結果イベントと同一（`Action<ShareOperationResult>?` / `ShareWithCallback` のみ選択結果用 `Action<ShareCallbackResult>?` を追加）。

| C# メソッド | シグネチャ | native | 共通イベント |
|------------|-----------|--------|------------|
| `ShareText` | `(ShareTextPayload payload, Action<ShareOperationResult>? onResult = null)` | `shareText` | `ShareOperationCompleted` |
| `ShareImage` | `(ShareImagePayload payload, Action<ShareOperationResult>? onResult = null)` | `shareImage` | `ShareOperationCompleted` |
| `ShareImages` | `(ShareImagesPayload payload, Action<ShareOperationResult>? onResult = null)` | `shareImages` | `ShareOperationCompleted` |
| `ShareFile` | `(ShareFilePayload payload, Action<ShareOperationResult>? onResult = null)` | `shareFile` | `ShareOperationCompleted` |
| `ShareFiles` | `(ShareFilesPayload payload, Action<ShareOperationResult>? onResult = null)` | `shareFiles` | `ShareOperationCompleted` |
| `RegisterDirectShareTarget` | `(DirectShareTargetPayload payload, Action<ShareOperationResult>? onResult = null)` | `registerDirectShareTarget` | `ShareOperationCompleted` |
| `RemoveDirectShareTargets` | `(RemoveDirectShareTargetsPayload payload, Action<ShareOperationResult>? onResult = null)` | `removeDirectShareTargets` | `ShareOperationCompleted` |
| `ShareWithCallback` | `(ShareTextPayload payload, Action<ShareOperationResult>? onStarted = null, Action<ShareCallbackResult>? onSelected = null)` | `shareWithCallback` | `ShareOperationCompleted`（起動成否）+ `ShareCallbackReceived`（選択時） |
| `CancelPendingShareCallback` | `(Action<ShareOperationResult>? onResult = null)` | `cancelPendingShareCallback` | `ShareOperationCompleted` |

- `ShareWithCallback` の `onStarted` は起動成否（共通 `ShareOperationCompleted` と同タイミング）、`onSelected` はアプリ選択時のみ発火（共通 `ShareCallbackReceived` と同タイミング）。`onSelected` はキャンセル / Copy / Edit 時に呼ばれないことがある（セクション 1.7）。

ライフサイクル / Bridge（Notification Manager に準拠）:

- `Awake`: `_instance` 設定、`DontDestroyOnLoad`、`UnityMainThreadDispatcher.Instance` 生成、`Initialize`。
- `Initialize`: Android のときのみ `getInstance` → `pluginInstance`、`setShareOperationListener` 登録。
- 各操作: `TryPrepareCall` 相当で「Android 判定 → pluginInstance null 判定 → currentActivity 取得 → PrependContext」を共通化。呼び出し前に `onResult` を `_pendingOperationCallbacks[operation]` に保存する（`ShareWithCallback` は `onSelected` を `_pendingShareSelectedCallback` に保存）。例外時は `Debug.LogError` + 共通 `ShareOperationCompleted` 発火 + 保存済み callback 発火（いずれも `ShareOperationResult.Failure`）してから callback を除去。
- `OnDestroy`: `clearShareOperationListener` → `pluginInstance.Dispose()` → null 化 → 保留中の callback を破棄。

リスナープロキシ（IL2CPP 制約反映・不足項目）:

```csharp
private sealed class ShareOperationListenerProxy : AndroidJavaProxy
{
    public ShareOperationListenerProxy(AndroidShareManager owner)
        : base("android.unity.share.UnityAndroidShareManager$ShareOperationListener") { ... }

    // Java から呼ばれるため public。メソッド名・シグネチャを native と完全一致させる。
    public void onShareOperation(string operation, bool isSuccessful, string? errorMessage);
    public void onShareResult(string operation, string? selectedPackageName);
}
```

- `AndroidJavaProxy` 方式のため `[MonoPInvokeCallback]` は不要。ただし IL2CPP / AOT で Java→C# リフレクション呼び出しが解決できるよう、proxy メソッドは `public` 非 static とし、メソッド名・引数型を native インターフェースに正確に一致させる。
- プロキシは Manager フィールドで保持し GC されないようにする。
- 両メソッドとも `UnityMainThreadDispatcher.Instance.Enqueue` でメインスレッドに転送してからイベント発火。
- ディスパッチ手順（メインスレッド転送後）:
  - `onShareOperation(operation, isSuccessful, errorMessage)`: `ShareOperationResult` を生成 → 共通 `ShareOperationCompleted` を発火 → `_pendingOperationCallbacks` から `operation` の callback を取り出して発火し、辞書から除去。
  - `onShareResult(operation, selectedPackageName)`: `ShareCallbackResult` を生成 → 共通 `ShareCallbackReceived` を発火 → `_pendingShareSelectedCallback` を発火（選択は一度きりのため発火後に null 化）。
- per-call callback はあくまで補助。未指定（`null`）でも共通イベントは必ず発火する。

### 3.3 契約

- **スレッド契約**: native コールバックは Android メインスレッドだが Unity メインスレッドとは別。全イベント発火を `UnityMainThreadDispatcher` 経由にする。Bridge 呼び出しは Unity メインスレッド前提。
- **メモリ契約**: `pluginInstance` は `OnDestroy` で `Dispose`。`currentActivity` は取得のたびに `using` で破棄。`AndroidJavaProxy` は Manager フィールドで保持。
- **エラー契約（中優先度3 で統一）**: 以下をすべて `ShareOperationCompleted` に `ShareOperationResult.Failure(operation, message)` で通知する（既存 `AndroidNotificationManager` と統一。非 Android 時も握り潰さず failure 発火）。
  - 非 Android プラットフォーム
  - `pluginInstance == null`（未 Initialize）
  - `currentActivity == null`
  - `Call` 例外
- `IsSuccess == true` のとき `ErrorMessage == null` を保証。

### 3.4 実装順序（依存関係）

1. `ShareOperationResult` / `ShareCallbackResult`（依存なし）
2. `AndroidSharePayloads`（依存なし）
3. `AndroidShareJsonBuilder`（Payloads に依存）
4. `AndroidShareJsonBuilderTests` / `ShareResultTests`（上記に依存・EditMode）
5. `AndroidShareManager`（上記すべて + Common に依存）
6. native-toolkit で `android_library` AAR をビルドし、`Plugins/Android/android-native-toolkit-1.1.0.aar` を差し替えて FileProvider の manifest / paths リソース内包を確認

---

## 4. エラーケース一覧と返却仕様（層別・中優先度1）

native は parser → use case → repository の順に処理する。Unity が実際に受け取る文言は発生層で異なる。

### 4.1 parser 層（JSON 検証）— `Failed to {operation}: ...` 系で返る

`UnityShareJsonParser` の `require` / `getString` 失敗は `IllegalArgumentException` / `JSONException` となり、`executeOperation` の汎用 catch（`Exception`）で `Failed to {operation}: {message}` として返る。**個別の `ShareDomainError` 文言にはならない**点に注意。

| 入力不備 | 実際に Unity が受け取る文言 |
|---------|--------------------------|
| `text` 空（shareText / shareWithCallback） | `Failed to {operation}: text is required`（または JSONException 文言） |
| `filePath` 空（shareImage / shareFile） | `Failed to {operation}: filePath is required` |
| `filePaths` 空配列（shareImages / shareFiles） | `Failed to {operation}: filePaths must not be empty` |
| `id` / `label` / `iconBase64` 空（register） | `Failed to {operation}: {field} is required` |
| `ids` 空配列（remove） | `Failed to {operation}: ids must not be empty` |

### 4.2 use case / repository 層 — 個別 `ShareDomainError` 文言で返る

parser を通過した後に発生するもの。

| native 例外 | errorMessage |
|------------|--------------|
| `ShareDomainError.EmptyContent` | `Share content is empty. Please provide text or a file path.` |
| `ShareDomainError.NoShareTarget` / `ActivityNotFoundException` | `No app available to handle this share request.` |
| `ShareDomainError.FileNotFound(path)` | `File not found: {path}` |
| `ShareDomainError.IllegalFileAccess(path)` | `File cannot be shared: {path}. Ensure the file is in a supported directory.` |
| `ShareDomainError.InvalidMimeType(mimeType)` | `Invalid MIME type: {mimeType}` |
| `ShareDomainError.DirectShareRegistrationFailed(reason)` | `Failed to register Direct Share target: {reason}` |
| `ShareDomainError.EmptyIdList` | `No shortcut IDs provided for removal.` |
| `ShareDomainError.EmptyFileList` | `No file paths provided for share.` |
| `ShareDomainError.InvalidBase64Icon(id)`（register の Base64 decode 失敗） | `Invalid icon data for Direct Share target: {id}` |
| `SecurityException` | `Security restriction while executing {operation}: {message}` |
| その他 `Exception` | `Failed to {operation}: {message}` |

### 4.3 C# Bridge 層（native 到達前）

| 条件 | Operation | ErrorMessage |
|------|-----------|--------------|
| 非 Android / `pluginInstance == null` / `currentActivity == null` | 呼び出し操作名 | `{operation} could not be started.` |
| `Call` 例外 | 呼び出し操作名 | 例外 message |

### 4.4 方針

- C# 側で必須項目の事前検証は行わず native に委譲する（二重実装回避）。Unity が受け取る文言は 4.1〜4.3 の通りになることをサンプル／マニュアルで前提化する。
- `mimeType` 等の default 値も C# で埋めず native default（text/plain・image/*）に委ねる。

---

## 5. テスト方針

| 種別 | 対象 | 内容 |
|------|------|------|
| EditMode | `AndroidShareJsonBuilder`（`Tests/Runtime/AndroidShareJsonBuilderTests.cs`） | 各 Payload → JSON。optional 有無、エスケープ、`chooserActions` / `filePaths` / `ids` 配列、未指定 default を出力しない（native default に委譲）等を検証 |
| EditMode | `ShareOperationResult` / `ShareCallbackResult`（`Tests/Runtime/ShareResultTests.cs`） | `Success`/`Failure` ファクトリ、`IsSuccess==true → ErrorMessage==null` 不変条件、`ShareCallbackResult` の `SelectedPackageName == null` 許容 |
| 手動（実機） | `AndroidShareManager` 全操作 | Sharesheet 表示、画像/ファイル共有（更新済み AAR の FileProvider 経由）、Direct Share 登録/削除、`shareWithCallback` の二段階通知（起動 → 選択）、選択せずキャンセル時に `ShareCallbackReceived` / `onSelected` が来ないこと、chooserActions（API 34+） |
| 手動（実機） | per-call callback 併用 | `onResult` を指定した操作で「共通イベント + per-call callback の両方」が発火すること、`null` 指定時に共通イベントのみ発火すること、同一操作の連続呼び出しで last-registered callback が発火すること |

- Manager は `#if UNITY_ANDROID` + `AndroidJavaObject` 依存のため EditMode 自動検証の対象外。初期化・listener・イベント転送は実機手動確認に寄せる（asmdef は Editor 限定のため）。
- テスト可能なロジック（JSON 組み立て・Result 不変条件）は Builder/Payload/Result の非ガード化により EditMode でカバーする。

---

## 6. chooserActions receiver / Direct Share の配置責任（不足項目）

- API 34+ の `chooserActions` で指定する `intentAction`（default `android.intent.action.SEND`）のブロードキャストを受ける receiver は、**同梱 AAR に汎用実装が含まれない**（manifest 確認済み: notification 用 receiver のみ）。カスタムアクションを使う場合、利用アプリ側で receiver の宣言・実装が必要。サンプルでは chooserActions 未使用または受信なしのデモに留める方針を `design-sample-scene` へ申し送る。
- Direct Share（`registerDirectShareTarget`）の shortcut publish は native（`ShortcutManagerCompat`）で完結。Unity 側は `iconBase64`（PNG/JPEG エンコード）を渡すのみ。画像生成（`Texture2D.EncodeToPNG` → `Convert.ToBase64String`）は呼び出し側の責務。

---

## 7. FileProvider 配置（高優先度2・必須前提）

### 7.1 現状（確認済み）

- `/Users/jonghyunkim/Desktop/native-toolkit` の `android_library` に FileProvider 実装済み（commit `411c591`）。
- `android/android_library/src/main/AndroidManifest.xml` に共有専用 provider を宣言し、`android/android_library/src/main/res/xml/native_toolkit_share_file_paths.xml` を同梱する。
- `ShareRepositoryImpl.fileToContentUri` はライブラリ専用 authority suffix を使用する実装へ更新済み。
- Example アプリ側の共有用 provider 宣言は削除済みで、利用アプリによる個別設定を不要とする構成になっている。
- native-toolkit では unit test、AndroidTest のコンパイル、Example アプリ build、release AAR 生成まで成功済み。AndroidTest の実機実行は未実施。
- 本プロジェクトに現在同梱されている `android-native-toolkit-1.1.0.aar` は更新前のため、更新済みビルド成果物への差し替えが必要。

### 7.2 確定した宣言と公開範囲

- authority: `${applicationId}.native_toolkit.share.fileprovider`
- provider クラス: `androidx.core.content.FileProvider`
- paths リソース: `@xml/native_toolkit_share_file_paths`
- 公開範囲:
  - `files-path`（→ Unity `Application.persistentDataPath` に概ね対応／要検証）
  - `cache-path`（→ Unity `Application.temporaryCachePath` に概ね対応／要検証）
  - `external-files-path`

### 7.3 配置方針（確定）

- FileProvider の provider 宣言と paths リソースは native-toolkit の `android_library` に置き、`android-native-toolkit` AAR に内包して配布する。
- 本プロジェクトでは `Plugins/Android/AndroidManifest.xml` や `Plugins/Android/res/xml/` を新規作成しない。
- 利用アプリ側にも FileProvider の追加設定を要求しない。AAR の manifest / resource merge で自動反映する。
- Unity Bridge の `unity-android-native-toolkit` AAR は FileProvider 実装を持たないため、本対応による差し替え対象外とする。

### 7.4 本プロジェクトでの統合確認

- native-toolkit で生成した更新済み `android_library` AAR を `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/android-native-toolkit-1.1.0.aar` へ差し替える。
- 差し替え後の AAR に `${applicationId}.native_toolkit.share.fileprovider` の provider 宣言と `res/xml/native_toolkit_share_file_paths.xml` が含まれることを確認する。
- Unity の Android build で manifest / resource merge が成功し、利用アプリ側の追加宣言なしで provider が解決されることを確認する。
- `persistentDataPath` / `temporaryCachePath` と `files-path` / `cache-path` の対応は実機で要検証。共有対象ファイルはこのスコープ内に配置することが前提。

---

## 8. 未確定・要検証事項

1. 更新済み `android-native-toolkit` AAR への差し替えと、Unity build での manifest / resource merge 確認（セクション 7.4）。
2. `persistentDataPath` / `temporaryCachePath` と `native_toolkit_share_file_paths.xml` の `files-path` / `cache-path` の対応（実機検証）。
3. `shareWithCallback` の二段階通知・選択なし時の非通知挙動の実機確認（セクション 1.7）。
4. chooserActions の `intentAction` receiver をサンプルでどう扱うか（受信デモを含めるか）。
5. 更新済み AAR の FileProvider AndroidTest を接続デバイスまたはエミュレーターで実行する。
