# Android Clipboard 実装計画 v1

## 基本情報

- 日付: 2026-07-26
- 機能名: clipboard
- 対象プラットフォーム: Android
- 出力言語: 日本語
- 位置づけ: **新規機能**。native-toolkit 側に実装済みの Clipboard Bridge（`android.unity.clipboard`）を Unity C# へ配線する
- 前提: native-toolkit 側の clipboard 実装は完了済み。本計画は Unity 側の追従のみ
- 後続工程: review-document → implement-feature → design-sample-scene（サンプルシーンは別スキル）

---

## 1. native-toolkit 確認結果（unity_android_plugin / android_library）

### 1.1 公開 API 一覧（`android.unity.clipboard.UnityAndroidClipboardManager`）

Kotlin `object` + `@JvmStatic fun getInstance()` 方式（既存 `UnityAndroidShareManager` と同一）。

| メソッド | シグネチャ | 種別 | 結果の返り方 |
|---|---|---|---|
| `getInstance` | `fun getInstance(): UnityAndroidClipboardManager` | - | インスタンス取得 |
| `setClipboardOperationListener` | `(listener: ClipboardOperationListener)` | 同期 | なし |
| `clearClipboardOperationListener` | `()` | 同期 | なし |
| `setClipboardChangeListener` | `(listener: ClipboardChangeListener)` | 同期 | なし（内部で main へ post） |
| `clearClipboardChangeListener` | `()` | 同期 | なし（監視も停止する） |
| `copyPlainText` | `(context: Context, clipboardJson: String)` | **非同期** | `ClipboardOperationListener` |
| `copyHtmlText` | `(context: Context, clipboardJson: String)` | **非同期** | `ClipboardOperationListener` |
| `copyUri` | `(context: Context, clipboardJson: String)` | **非同期** | `ClipboardOperationListener` |
| `copyMultipleText` | `(context: Context, clipboardJson: String)` | **非同期** | `ClipboardOperationListener` |
| `clear` | `(context: Context)` | **非同期** | `ClipboardOperationListener` |
| `read` | `(context: Context): String` | **同期** | 戻り値 JSON |
| `hasClip` | `(context: Context): String` | **同期** | 戻り値 `"true"` / `"false"` |
| `getDescription` | `(context: Context): String` | **同期** | 戻り値 JSON |
| `startObserving` | `(context: Context)` | 非同期（main post） | **結果通知なし** |
| `stopObserving` | `()` | **非同期** | `ClipboardOperationListener`（`stopObserving`） |

参照: [UnityAndroidClipboardManager.kt](/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/clipboard/UnityAndroidClipboardManager.kt)

### 1.2 同期・非同期の区分（本計画の設計上もっとも重要な点）

native 側が明示的に 2 系統に分けている（`UnityAndroidClipboardManager.kt` クラス KDoc）:

> copy/clear operations report results through ClipboardOperationListener. read/hasClip/getDescription are synchronous and return their result as a JSON string directly (no listener round trip), since clipboard reads are inherently synchronous and foreground-bound.

`agent-rules/coding-rules/common.md`「公開 API 方式（同期・非同期の判断）」の基本原則
（**ネイティブ API が同期なら C# も同期、非同期なら C# も非同期**）に従い、C# 側も以下で固定する。

| native | C# Manager |
|---|---|
| `read` / `hasClip` / `getDescription`（戻り値あり） | **同期メソッド**（戻り値を返す。event / callback を使わない） |
| `copyPlainText` / `copyHtmlText` / `copyUri` / `copyMultipleText` / `clear` / `stopObserving` | **共通 event + 任意の per-call callback** |
| `startObserving` | 結果を返さないため callback 引数を持たせない |

- 同期 3 メソッドを `ClipboardOperationCompleted` event 経由にしない（非同期化の禁止に該当）
- 非同期メソッドを同期化しない（`.Result` / `.Wait()` 禁止）

### 1.3 定数（operation 名）

| Kotlin 定数 | 値 |
|---|---|
| `OPERATION_COPY_PLAIN_TEXT` | `"copyPlainText"` |
| `OPERATION_COPY_HTML_TEXT` | `"copyHtmlText"` |
| `OPERATION_COPY_URI` | `"copyUri"` |
| `OPERATION_COPY_MULTIPLE_TEXT` | `"copyMultipleText"` |
| `OPERATION_CLEAR` | `"clear"` |
| `OPERATION_STOP_OBSERVING` | `"stopObserving"` |

### 1.4 listener インターフェース

| インターフェース | JNI 名 | メソッド |
|---|---|---|
| `ClipboardOperationListener` | `android.unity.clipboard.UnityAndroidClipboardManager$ClipboardOperationListener` | `onClipboardOperation(operation: String, isSuccessful: Boolean, errorMessage: String?)` |
| `ClipboardChangeListener` | `android.unity.clipboard.UnityAndroidClipboardManager$ClipboardChangeListener` | `onClipboardChanged()` |

- listener 未設定時は native が warn ログのみ出して破棄する（例外にならない）
- `notifyClipboardChanged` は listener の例外を native 側で catch する（Unity 側の例外が native へ抜けない）

### 1.5 送信 JSON スキーマ（C# → native）

`UnityClipboardJsonParser` / `UnityClipboardSpecs` より確定。

| operation | スキーマ | 必須キー |
|---|---|---|
| `copyPlainText` | `{ "text": string, "label": string, "isSensitive": bool }` | `text`（`getString`） |
| `copyHtmlText` | `{ "plainText": string, "htmlText": string, "label": string, "isSensitive": bool }` | `htmlText`（`getString`） |
| `copyUri` | `{ "uri": string, "label": string, "isSensitive": bool }` | `uri`（`getString`） |
| `copyMultipleText` | `{ "texts": string[], "label": string, "isSensitive": bool }` | `texts`（`getJSONArray`） |

- `label` は `optString`（欠落時 `""`）、`isSensitive` は `optBoolean`（欠落時 `false`）
- 必須キー欠落は `JSONException` となり、native の `executeOperation` が catch して `CLIPBOARD_UNKNOWN` として listener へ返す
- `isSensitive` は Android 13+ のプレビュー抑制ヒント

### 1.6 返却 JSON スキーマ（native → C#、同期メソッド）

`read(context)`:

- 内容あり: `{ "label": string?, "mimeTypes": [string], "items": [{ "text": string?, "htmlText": string?, "uri": string?, "coercedText": string? }] }`
- 空クリップボード（正常系）: 文字列 `"null"`
- 失敗: `{ "error": "<CODE>", "message": "<message>" }`

`getDescription(context)`:

- 内容あり: `{ "label": string?, "mimeTypes": [string], "isStyledText": bool, "classificationStatus": int? }`
- 空: 文字列 `"null"`
- 失敗: `{ "error": "<CODE>", "message": "<message>" }`

`hasClip(context)`:

- `"true"` / `"false"`。**失敗時も `"false"`**（成功時の false と区別できない）

**重要な性質:** native は `org.json.JSONObject.put(key, null)` を使っており、値が null のキーは**JSON から削除される**（`JSONObject.NULL` は使っていない）。C# 側パーサはキー欠落を「値なし」として扱う必要がある。

### 1.7 エラーコード表（`errorInfoOf`）

listener 経路と同期 JSON 経路で同一のコード体系を共有する。

| コード | 発生元 | errorMessage |
|---|---|---|
| `CLIPBOARD_EMPTY_CONTENT` | `ClipboardDomainError.EmptyContent` | `Clipboard content is empty. Please provide text or HTML.` |
| `CLIPBOARD_EMPTY_ITEMS` | `ClipboardDomainError.EmptyItemList` | `No items provided for clipboard copy.` |
| `CLIPBOARD_INVALID_URI` | `ClipboardDomainError.InvalidUri` | `Invalid URI: {uri}` |
| `CLIPBOARD_UNAVAILABLE` | `ClipboardDomainError.ClipboardUnavailable` | `Clipboard service is unavailable.` |
| `CLIPBOARD_READ_NOT_ALLOWED` | `ClipboardDomainError.ReadNotAllowed` | `Clipboard read is not allowed. The app must be in the foreground.` |
| `CLIPBOARD_SECURITY` | `SecurityException` | `Security restriction while accessing clipboard: {message}` |
| `CLIPBOARD_UNKNOWN` | その他（JSON パース失敗を含む） | `Failed: {message}` |

**注意:** `ClipboardOperationListener.onClipboardOperation` にはコードではなく **errorMessage 文字列のみ**が渡る（`errorInfoOf` の `first` は listener 経路では破棄される）。コードが取れるのは同期 JSON 経路（`read` / `getDescription`）だけ。

参照: [ClipboardDomainError.kt](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/android/library/clipboard/domain/error/ClipboardDomainError.kt)

### 1.8 android_library 側の背後実装

- `ClipboardUseCases(context)` ファクトリ（`data/repository/ClipboardUseCases.kt`）が `ClipboardRepositoryImpl` を注入して UseCase 一式を構築する。Bridge は毎回このファクトリを呼ぶ（状態を持たない）
- 監視は UseCase を経由せず `presentation/ClipboardChangeMonitor` が system listener（`ClipboardManager.OnPrimaryClipChangedListener`）を単独所有する。Unity Bridge は listener を持たず委譲のみ
- `ClipboardChangeMonitor.start` は二重呼び出しを no-op（重複登録なし）。`ClipboardManager` 取得失敗時は warn ログのみで**監視が開始されない**（Unity 側にエラーが返らない）
- Android 10+（API 29+）はクリップボード読み取りをフォアグラウンドアプリ / 既定 IME に制限する。監視・読み取りはフォアグラウンド時のみ信頼できる

参照: [ClipboardChangeMonitor.kt](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/android/library/clipboard/presentation/ClipboardChangeMonitor.kt)

### 1.9 同梱 AAR 確認結果（追加設定は不要）

`Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/` の AAR を展開して確認済み。

| AAR | clipboard クラス | 判定 |
|---|---|---|
| `unity-android-native-toolkit-1.3.0.aar` | `android/unity/clipboard/UnityAndroidClipboardManager` + `$ClipboardOperationListener` + `$ClipboardChangeListener` + `UnityClipboardJsonParser` + Spec 4 種 | 同梱済み |
| `android-native-toolkit-1.3.0.aar` | `android/library/clipboard/**`（domain / application / data / presentation 一式、`ClipboardChangeMonitor` を含む） | 同梱済み |

- **AAR 差し替えは不要**。現行同梱 AAR のまま実装可能
- clipboard は `AndroidManifest.xml` 宣言を必要としない（`android-native-toolkit` の manifest に clipboard 関連の permission / provider / receiver は無い）
- `res/` に clipboard 用リソースは無く、必要もない（`res/xml/native_toolkit_share_file_paths.xml` は share 用）
- **クリップボードに必要な Android 権限は存在しない**ため、利用アプリ側の追加設定は発生しない
- `minSdkVersion 31` は Unity 側の最小要件（Android 12）と整合

---

## 2. 既存 C# 実装確認結果（`Packages/com.jonghyunkim.nativetoolkit/Runtime/`）

### 2.1 準拠する既存パターン: `Share/AndroidShareManager.cs`

Android + `AndroidJavaProxy` listener + per-call callback という構成が本機能と完全に一致するため、これを基準実装とする。

- `#if UNITY_ANDROID` でクラス全体をガード、`MonoBehaviour` Singleton（`Instance` プロパティ + `Awake` の重複破棄）
- `Awake` で `_ = UnityMainThreadDispatcher.Instance;` → `Initialize()`
- `Initialize()`: `Application.platform != RuntimePlatform.Android` なら早期 return。`AndroidJavaClass(PluginClassName).CallStatic<AndroidJavaObject>("getInstance")` で `pluginInstance` 取得後、proxy を `??=` で生成し `setXxxListener` を呼ぶ
- `OnDestroy`: `clearXxxListener` → `pluginInstance?.Dispose()` → pending callback クリア → `_instance = null`
- `TryPrepareCall`: プラットフォーム / `pluginInstance` null / `currentActivity` null を検査し、失敗時は `$"{operation} could not be started."` で Failure を生成
- `GetCurrentActivity()`: `AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity")`
- `_pendingOperationCallbacks` は `Dictionary<string, Action<T>?>`（operation 単位で last-registered wins）。`FireOperationResult` で**先にスナップショット取得 → 削除 → `UnityMainThreadDispatcher.Enqueue` → 共通 event → per-call callback** の順
- proxy は `private sealed class ... : AndroidJavaProxy`、JNI 名を base に渡し、メソッドは `public` 非 static

### 2.2 準拠する既存パターン: 結果型・Payload・JsonBuilder

- `Share/ShareOperationResult.cs`: `public readonly struct`、`Operation` / `IsSuccess` / `ErrorMessage`、`Success` / `Failure` の静的ファクトリ、private コンストラクタ。プラットフォームガードなし
- `Share/AndroidSharePayloads.cs`: `[Serializable] public sealed class`、**public フィールド（lowerCamelCase）**。ガードなし
- `Share/AndroidShareJsonBuilder.cs`: 外部 JSON ライブラリ不使用の手書きシリアライザ。`Dictionary<string, object?>` を組んで `SerializeObject`
- `Runtime/AssemblyInfo.cs` に `InternalsVisibleTo("NativeToolkit.Runtime.Tests")` があり、**internal 型を EditMode テストから検証できる**

### 2.3 既存実装に無いもの（本機能で新規に必要）

- **JSON パース処理が存在しない**。既存 C# はビルドのみで、native からの戻り値 JSON を解釈するコードは 1 つも無い（`JsonUtility` の利用箇所はコメント 1 件のみ）。本機能で初めてパーサが必要になる
- **同期的に値を返す Manager メソッドが存在しない**。既存は全て `void` + event
- clipboard 用の namespace / ディレクトリ（`Runtime/Clipboard/`）が存在しない

---

## 3. 実装対象 API 一覧

### 3.1 C# → native（同期呼び出し・戻り値あり）

| C# 呼び出し | native メソッド | 戻り値 |
|---|---|---|
| `pluginInstance.Call<string>("read", activity)` | `read(Context): String` | JSON / `"null"` / エラー JSON |
| `pluginInstance.Call<string>("hasClip", activity)` | `hasClip(Context): String` | `"true"` / `"false"` |
| `pluginInstance.Call<string>("getDescription", activity)` | `getDescription(Context): String` | JSON / `"null"` / エラー JSON |

### 3.2 C# → native（非同期呼び出し・listener 経由）

| C# 呼び出し | native メソッド |
|---|---|
| `pluginInstance.Call("copyPlainText", activity, json)` | `copyPlainText(Context, String)` |
| `pluginInstance.Call("copyHtmlText", activity, json)` | `copyHtmlText(Context, String)` |
| `pluginInstance.Call("copyUri", activity, json)` | `copyUri(Context, String)` |
| `pluginInstance.Call("copyMultipleText", activity, json)` | `copyMultipleText(Context, String)` |
| `pluginInstance.Call("clear", activity)` | `clear(Context)` |
| `pluginInstance.Call("startObserving", activity)` | `startObserving(Context)` |
| `pluginInstance.Call("stopObserving")` | `stopObserving()` |

### 3.3 C# → native（listener 登録・解除）

| C# 呼び出し | native メソッド |
|---|---|
| `pluginInstance.Call("setClipboardOperationListener", operationListener)` | `setClipboardOperationListener` |
| `pluginInstance.Call("clearClipboardOperationListener")` | `clearClipboardOperationListener` |
| `pluginInstance.Call("setClipboardChangeListener", changeListener)` | `setClipboardChangeListener` |
| `pluginInstance.Call("clearClipboardChangeListener")` | `clearClipboardChangeListener` |

### 3.4 native → C# コールバック

| proxy | JNI インターフェース | メソッド |
|---|---|---|
| `ClipboardOperationListenerProxy` | `...UnityAndroidClipboardManager$ClipboardOperationListener` | `public void onClipboardOperation(string operation, bool isSuccessful, string? errorMessage)` |
| `ClipboardChangeListenerProxy` | `...UnityAndroidClipboardManager$ClipboardChangeListener` | `public void onClipboardChanged()` |

---

## 4. 変更ファイル一覧

`.meta` は Unity が自動生成するため記載しない。サンプルシーン（ExampleController / UXML / USS）は本計画の対象外。

### 4.1 新規作成

| パス | 内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardManager.cs` | Singleton Manager 本体（`#if UNITY_ANDROID`） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardPayloads.cs` | `CopyPlainTextPayload` / `CopyHtmlTextPayload` / `CopyUriPayload` / `CopyMultipleTextPayload`（ガードなし） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardJsonBuilder.cs` | 送信 JSON 生成（手書きシリアライザ、ガードなし） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardJsonParser.cs` | 受信 JSON 解釈（`internal static`、ガードなし） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/ClipboardOperationResult.cs` | copy/clear/stopObserving の結果 struct（ガードなし） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/ClipboardReadResult.cs` | `read` の結果 struct + `ClipboardReadStatus` enum + `ClipContents` / `ClipItem`（ガードなし） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/ClipboardDescriptionResult.cs` | `getDescription` の結果 struct + `ClipDescriptionInfo`（ガードなし） |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardJsonBuilderTests.cs` | 送信 JSON の EditMode テスト |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardJsonParserTests.cs` | 受信 JSON の EditMode テスト |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardManagerDispatchTests.cs` | dispatch 順序の EditMode テスト |

### 4.2 既存変更

なし。既存ファイルへの変更は発生しない。

### 4.3 非変更（対象だが変更しない）

| パス | 理由 |
|---|---|
| `Runtime/Common/UnityMainThreadDispatcher.cs` | 既存のまま流用 |
| `Runtime/NativeToolkit.Runtime.asmdef` | 追加参照不要（`UnityEngine` のみ） |
| `Runtime/AssemblyInfo.cs` | `InternalsVisibleTo` は設定済み |
| `Plugins/Android/*.aar` | clipboard 実装が同梱済み（1.9 参照） |
| `Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef` | 追加参照不要 |

---

## 5. 実装詳細（implement-feature ステップ3で行う内容）

namespace: `JonghyunKim.NativeToolkit.Runtime.Clipboard`

### 5.1 Payload 型（`AndroidClipboardPayloads.cs`）

既存 `AndroidSharePayloads.cs` に準拠し `[Serializable] public sealed class` + public フィールド。

```csharp
[Serializable]
public sealed class CopyPlainTextPayload
{
    public string text = string.Empty;      // 必須。空文字は native 側で許容される
    public string? label;                   // 省略時 native で ""
    public bool isSensitive;                // Android 13+ のプレビュー抑制ヒント
}

[Serializable]
public sealed class CopyHtmlTextPayload
{
    public string plainText = string.Empty; // フォールバック用プレーンテキスト
    public string htmlText = string.Empty;  // 必須。blank は CLIPBOARD_EMPTY_CONTENT
    public string? label;
    public bool isSensitive;
}

[Serializable]
public sealed class CopyUriPayload
{
    public string uri = string.Empty;       // 必須。blank / パース不可は CLIPBOARD_INVALID_URI
    public string? label;
    public bool isSensitive;
}

[Serializable]
public sealed class CopyMultipleTextPayload
{
    public string[] texts = Array.Empty<string>(); // 必須。空配列は CLIPBOARD_EMPTY_ITEMS
    public string? label;
    public bool isSensitive;
}
```

### 5.2 結果型

```csharp
public readonly struct ClipboardOperationResult
{
    public string Operation { get; }
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }   // IsSuccess == true のとき null を保証
    public static ClipboardOperationResult Success(string operation);
    public static ClipboardOperationResult Failure(string operation, string error);
}

public enum ClipboardReadStatus { HasContent, Empty, Failed }

public sealed class ClipItem
{
    public string? Text { get; }
    public string? HtmlText { get; }
    public string? Uri { get; }
    public string? CoercedText { get; }
}

public sealed class ClipContents
{
    public string? Label { get; }
    public IReadOnlyList<string> MimeTypes { get; }  // 欠落時は空配列に正規化
    public IReadOnlyList<ClipItem> Items { get; }    // 欠落時は空配列に正規化
}

public readonly struct ClipboardReadResult
{
    public ClipboardReadStatus Status { get; }
    public ClipContents? Contents { get; }   // Status == HasContent のときのみ非 null
    public string? ErrorCode { get; }        // Status == Failed のときのみ非 null（1.7 のコード）
    public string? ErrorMessage { get; }
    public bool IsSuccess => Status != ClipboardReadStatus.Failed;
}

public sealed class ClipDescriptionInfo
{
    public string? Label { get; }
    public IReadOnlyList<string> MimeTypes { get; }
    public bool IsStyledText { get; }
    public int? ClassificationStatus { get; }  // ClipDescription.CLASSIFICATION_* 生値
}

public readonly struct ClipboardDescriptionResult
{
    public ClipboardReadStatus Status { get; }
    public ClipDescriptionInfo? Description { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public bool IsSuccess => Status != ClipboardReadStatus.Failed;
}
```

`ClipboardReadStatus` を `read` / `getDescription` で共用する（両者の 3 状態が同一のため）。

**設計判断:** `Status` に空クリップボードを表す `Empty` を持たせ、「内容なし」を失敗として扱わない。native 側が空クリップボードを正常系（`"null"`）として返しており、`ReadNotAllowed` と明確に区別しているため、この区別を C# でも保持する。

### 5.3 `AndroidClipboardJsonBuilder`（送信）

`AndroidShareJsonBuilder` の手書きシリアライザ方式に準拠。外部 JSON ライブラリを使わない。

| メソッド | 出力 |
|---|---|
| `BuildCopyPlainTextJson(CopyPlainTextPayload)` | `{"text":...,"label":...,"isSensitive":...}` |
| `BuildCopyHtmlTextJson(CopyHtmlTextPayload)` | `{"plainText":...,"htmlText":...,"label":...,"isSensitive":...}` |
| `BuildCopyUriJson(CopyUriPayload)` | `{"uri":...,"label":...,"isSensitive":...}` |
| `BuildCopyMultipleTextJson(CopyMultipleTextPayload)` | `{"texts":[...],"label":...,"isSensitive":...}` |

- 必須キー（`text` / `htmlText` / `uri` / `texts`）は**値が空でも必ず出力する**。欠落すると native が `JSONException` → `CLIPBOARD_UNKNOWN` になり、本来の `CLIPBOARD_EMPTY_CONTENT` 等のドメインエラーが得られなくなるため
- `label` は null / 空白のとき省略（native が `optString` で `""` にフォールバックする）
- `isSensitive` は常に出力（`false` でも明示）
- 文字列エスケープは `AndroidShareJsonBuilder` の既存ヘルパと同じ規則（`"`、`\`、制御文字）

### 5.4 `AndroidClipboardJsonParser`（受信）

`internal static class`。`InternalsVisibleTo` により EditMode テストから直接検証する。

```csharp
internal static ClipboardReadResult ParseReadResult(string? raw);
internal static ClipboardDescriptionResult ParseDescriptionResult(string? raw);
internal static bool ParseHasClip(string? raw);
```

判定順序（両 Parse 共通）:

1. `raw` が null / 空白 → `Failed`（`CLIPBOARD_UNKNOWN` / `"Clipboard bridge returned no data."`）
2. `raw.Trim() == "null"` → `Empty`
3. エラー封筒 DTO へ `JsonUtility.FromJson` し、`error` が非空 → `Failed`（`ErrorCode` = `error`、`ErrorMessage` = `message`）
4. それ以外 → 本体 DTO へ `JsonUtility.FromJson` して結果型へ変換
5. `ArgumentException`（不正 JSON）は catch し `Failed` / `CLIPBOARD_UNKNOWN` にマップする

**`JsonUtility` を採用する理由と制約:**

- Unity 標準で追加依存が不要。既存方針（外部 JSON ライブラリ不使用）と整合する
- 制約1: **キー欠落と空文字を区別できない**。native は値 null のキーを JSON から削除する（1.6）ため、`text` / `htmlText` / `uri` / `coercedText` / `label` は**空文字を「値なし（null）」として正規化**する。クリップボードの空文字アイテムと欠落アイテムが同一視されるが、実用上の影響は無いと判断する（要検証事項 9.2）
- 制約2: `classificationStatus` 欠落時 `JsonUtility` は `0` を返す。`ClipDescription.CLASSIFICATION_*` の有効値は 1〜3 のため、**`0` を「未提供（null）」として `int?` に変換**する
- 制約3: 配列キー欠落時は null になるため、`MimeTypes` / `Items` は**空配列へ正規化**して公開する（呼び出し側の null チェックを不要にする）

DTO は `[Serializable] private sealed class` として Parser 内に閉じ、公開型へは変換して返す。

`ParseHasClip`: `bool.TryParse` で解釈し、失敗時は `false` + `Debug.LogWarning`。

### 5.5 `AndroidClipboardManager` 設計

```csharp
private const string PluginClassName = "android.unity.clipboard.UnityAndroidClipboardManager";
private const string LogTag = "AndroidClipboardManager";

public const string OperationCopyPlainText = "copyPlainText";
public const string OperationCopyHtmlText = "copyHtmlText";
public const string OperationCopyUri = "copyUri";
public const string OperationCopyMultipleText = "copyMultipleText";
public const string OperationClear = "clear";
public const string OperationStopObserving = "stopObserving";
```

**イベント:**

```csharp
/// copy / clear / stopObserving のいずれかが完了したとき発火。成功・失敗どちらも発火し、
/// 常に per-call callback より先に呼ばれる。
public event Action<ClipboardOperationResult>? ClipboardOperationCompleted;

/// 監視中にクリップボード内容が変化したとき発火。StartObserving を呼ぶまで発火しない。
public event Action? ClipboardChanged;
```

`ClipboardChanged` は引数を持たない（native の `onClipboardChanged()` が無引数であり、変化後の内容は別途 `Read()` で取得する設計のため）。

**非同期メソッド（共通 event + per-call callback）:**

```csharp
public void CopyPlainText(CopyPlainTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
public void CopyHtmlText(CopyHtmlTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
public void CopyUri(CopyUriPayload payload, Action<ClipboardOperationResult>? onResult = null)
public void CopyMultipleText(CopyMultipleTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
public void Clear(Action<ClipboardOperationResult>? onResult = null)
public void StopObserving(Action<ClipboardOperationResult>? onResult = null)
```

内部は `CallOperation(operationName, json?, onResult)` に集約し、`AndroidShareManager.CallOperation` / `TryPrepareCall` と同一構造にする。`stopObserving` のみ activity 引数を取らないため、`TryPrepareCall` に**activity を渡さないモード**を追加する（`json == null && !needsActivity` → `fullArgs = Array.Empty<object?>()`）。

`_pendingOperationCallbacks` は `Dictionary<string, Action<ClipboardOperationResult>?>`、operation 単位で last-registered wins。

**同期メソッド（戻り値あり・event を発火しない）:**

```csharp
public ClipboardReadResult Read()
public bool HasClip()
public ClipboardDescriptionResult GetDescription()
```

- `ClipboardOperationCompleted` は発火させない（同期 API のため）
- Bridge 利用不可時（非 Android / `pluginInstance` null / activity null）:
  - `Read` / `GetDescription` → `Failed`、`ErrorCode` = `CLIPBOARD_BRIDGE_UNAVAILABLE`、`ErrorMessage` = `"read could not be started."` / `"getDescription could not be started."`（既存 Share の `$"{operation} could not be started."` 文言規則に合わせる）
  - `HasClip` → `false` + `Debug.LogWarning`
- `pluginInstance.Call<string>` が例外を投げた場合も同じ Failure にマップする（`Debug.LogError` 付き）

**監視の開始:**

```csharp
/// 監視を開始する。結果は返らない（native が startObserving の結果を通知しないため）。
/// 変化は ClipboardChanged で受け取る。二重呼び出しは native 側で no-op。
public void StartObserving()
```

- per-call callback 引数を**持たせない**。native の `startObserving` は `ClipboardOperationListener` を呼ばないため、callback を用意すると永久に発火しない API になる
- `ClipboardManager` 取得失敗時、native は warn ログのみで監視が始まらない。Unity 側では成否を検知できないことを XML コメントに明記する

**ライフサイクル:**

- `Awake`: `_ = UnityMainThreadDispatcher.Instance;` → `Initialize()`
- `Initialize()`: `getInstance` 取得 → `operationListener ??= new ClipboardOperationListenerProxy(this)` → `setClipboardOperationListener` → `changeListener ??= new ClipboardChangeListenerProxy(this)` → `setClipboardChangeListener`
  - 両 listener を `Initialize` で登録する（`setClipboardChangeListener` は監視を開始しないため、常時登録して副作用がない）
- `OnDestroy`: `clearClipboardChangeListener`（監視も停止する）→ `clearClipboardOperationListener` → `pluginInstance?.Dispose()` → `_pendingOperationCallbacks.Clear()` → `_instance = null`
  - **`clearClipboardChangeListener` を先に呼ぶ**。system listener の解除漏れを防ぐため

**dispatch:**

```csharp
private void FireOperationResult(ClipboardOperationResult result)
{
    _pendingOperationCallbacks.TryGetValue(result.Operation, out var cb);
    _pendingOperationCallbacks.Remove(result.Operation);   // 先にスナップショット
    UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, ClipboardOperationCompleted, cb));
}

internal static void InvokeInOrder(
    ClipboardOperationResult result,
    Action<ClipboardOperationResult>? common,
    Action<ClipboardOperationResult>? perCall)   // try/catch で例外を握り LogError
```

`InvokeInOrder` を `internal static` の純粋関数として切り出す（`IosShareManager.InvokeInOrder` と同方式）。Unity ライフサイクル非依存になり、dispatch 順序と例外処理を EditMode テストから直接検証できる。

`ClipboardChanged` も同様に `UnityMainThreadDispatcher.Instance.Enqueue` 経由で発火し、購読側の例外を try/catch で握る。

### 5.6 スレッド契約・メモリ契約・エラー契約

**スレッド:**

- `onClipboardOperation` は native が main handler へ post した後に呼ばれるが、C# 側は**無条件に `UnityMainThreadDispatcher.Enqueue` を通す**
- `onClipboardChanged` はドキュメント上の記載が native 側で食い違っている（要検証 9.1）。**必ず `Enqueue` を通す**
- 同期 3 メソッドは Unity メインスレッド（= Android UI スレッド）から呼ばれる前提。フォアグラウンド制限を満たすためにもメインスレッド以外から呼ばない旨を XML コメントに記載する

**メモリ:**

- 2 つの proxy は Manager のフィールドで保持し GC を防ぐ（`??=` で 1 度だけ生成）
- `GetCurrentActivity()` の `AndroidJavaObject` は `using` で確実に Dispose する
- `pluginInstance` は `OnDestroy` で Dispose
- `Marshal.AllocHGlobal` は使わない（P/Invoke 経路なし）

**エラー:**

- `IsSuccess == true` のとき `ErrorMessage == null` を保証する
- native の errorMessage 文言をそのまま透過し、C# 側で書き換えない
- C# Bridge 層固有の失敗のみ `$"{operation} could not be started."` を生成する

### 5.7 ログ規則（csharp.md への準拠と明示的な逸脱）

`csharp.md` は「全メソッドの先頭1行目に全パラメータを含む `Debug.Log`」を要求するが、**クリップボード内容はパスワード・トークンを含みうるため、値そのものはログに出さない**。

- native 側も同じ理由で `maskJson()` により `<redacted, length=N>` に置換している。C# 側もこれに合わせる
- 出力するのは長さ・件数・有無のみ（例: `Debug.Log($"[{LogTag}][{nameof(CopyPlainText)}] textLength: {payload.text?.Length ?? 0}, hasLabel: {...}, isSensitive: {...}, hasCallback: {onResult != null}")`）
- `Read` / `GetDescription` の戻り値もログに出さない（`Status` と `ErrorCode` のみ）
- この逸脱は各メソッドのコード上に理由コメントを 1 行入れる

### 5.8 IL2CPP / `AndroidJavaProxy` 制約

- proxy メソッドは `public` **非 static**（`[MonoPInvokeCallback]` は不要。P/Invoke ではなく JNI 経路のため）
- メソッド名・引数型を native インターフェースに完全一致させる
  - `onClipboardOperation(string operation, bool isSuccessful, string? errorMessage)`
  - `onClipboardChanged()`
- base コンストラクタに渡す JNI 名は `$` 区切りの内部クラス名（3.4 の表のとおり）
- proxy から例外を native へ抜けさせない（`onClipboardChanged` は native 側でも catch されるが、C# 側でも握る）

### 5.9 非同期版（`Awaitable`）を本計画に含めない理由

`common.md`「非同期版の併設ルール」は非同期ネイティブ API に `Awaitable<T>` 版を併設してよいと定めるが、同じく「多重呼び出しガード（非同期版を追加する前提条件）」が前提条件を課している。

- 本 Manager は既存 Share Manager と同じ **last-registered wins**（operation 単位の単一スロット）を採用する
- この方式のまま `Awaitable` 版を作ると、同一 operation の 2 回目の呼び出しで 1 回目の `AwaitableCompletionSource` が未完了のまま破棄され、`await` がハングする
- したがって v1 は **callback 版のみ**とし、`Awaitable` 版は in-flight ガードの導入とセットで別計画とする

### 5.10 実装順序

1. 結果型（`ClipboardOperationResult` / `ClipboardReadResult` / `ClipboardDescriptionResult` + 付随クラス）
2. `AndroidClipboardPayloads`
3. `AndroidClipboardJsonBuilder` + テスト
4. `AndroidClipboardJsonParser` + テスト
5. `AndroidClipboardManager`（Singleton → Initialize / listener → 同期メソッド → 非同期メソッド → 監視）
6. `AndroidClipboardManagerDispatchTests`
7. Unity Test Runner で全テスト実行

---

## 6. エラーケース一覧と返却仕様（層別）

### 6.1 parser 層（`UnityClipboardJsonParser`、native）

| ケース | 返却 |
|---|---|
| `text` キー欠落（copyPlainText） | `JSONException` → listener に `CLIPBOARD_UNKNOWN` の文言 `Failed: ...` |
| `htmlText` キー欠落（copyHtmlText） | 同上 |
| `uri` キー欠落（copyUri） | 同上 |
| `texts` キー欠落 / 配列でない（copyMultipleText） | 同上 |
| 不正 JSON 文字列 | 同上 |

C# の JsonBuilder が必須キーを常に出力するため、通常運用では発生しない（5.3）。

### 6.2 use case / repository 層（`android_library`、ドメインエラー）

| ケース | errorMessage（listener / JSON 共通） |
|---|---|
| HTML 本文が blank | `Clipboard content is empty. Please provide text or HTML.` |
| `texts` が空配列 | `No items provided for clipboard copy.` |
| `uri` が blank / パース不可 | `Invalid URI: {uri}` |
| `ClipboardManager` 取得不可 | `Clipboard service is unavailable.` |
| 読み取りが system に拒否された | `Clipboard read is not allowed. The app must be in the foreground.` |
| `SecurityException` | `Security restriction while accessing clipboard: {message}` |
| その他例外 | `Failed: {message}` |

同期経路（`read` / `getDescription`）ではこれに加えて `ErrorCode`（1.7 のコード）が取得できる。

### 6.3 C# Bridge 層（本実装で生成する失敗）

| ケース | 対象 API | 返却 |
|---|---|---|
| 非 Android プラットフォーム | 非同期 6 種 | `Failure(operation, $"{operation} could not be started.")` + `LogWarning` |
| `pluginInstance` が null（初期化失敗） | 非同期 6 種 | 同上 + `LogError` |
| `currentActivity` が null | 非同期 5 種（`stopObserving` を除く） | 同上 + `LogError` |
| `pluginInstance.Call` が例外 | 非同期 6 種 | `Failure(operation, ex.Message)` + `LogError` |
| Bridge 利用不可 | `Read` | `Failed` / `CLIPBOARD_BRIDGE_UNAVAILABLE` / `read could not be started.` |
| Bridge 利用不可 | `GetDescription` | `Failed` / `CLIPBOARD_BRIDGE_UNAVAILABLE` / `getDescription could not be started.` |
| Bridge 利用不可 | `HasClip` | `false` + `LogWarning` |
| `Call<string>` が例外 | `Read` / `GetDescription` | `Failed` / `CLIPBOARD_BRIDGE_UNAVAILABLE` / `ex.Message` + `LogError` |
| 戻り値が null / 空白 | `Read` / `GetDescription` | `Failed` / `CLIPBOARD_UNKNOWN` / `Clipboard bridge returned no data.` |
| 戻り値が不正 JSON | `Read` / `GetDescription` | `Failed` / `CLIPBOARD_UNKNOWN` / パース失敗メッセージ |
| 戻り値が `"true"` / `"false"` 以外 | `HasClip` | `false` + `LogWarning` |

### 6.4 Unity 側で検知できないケース（仕様として明記する）

| ケース | 挙動 |
|---|---|
| `startObserving` 時に `ClipboardManager` が取得できない | native が warn ログのみ。監視は始まらず Unity にエラーが返らない |
| `hasClip` の内部失敗 | `"false"` が返り、内容なしと区別できない |
| listener 未設定時の操作結果 | native が warn ログのみで破棄（`Initialize` で常時登録するため通常発生しない） |
| バックグラウンドでの読み取り | Android 10+ の制限により空クリップボード（`Empty`）として返る場合がある |

---

## 7. テスト方針

### 7.1 EditMode（Unity Test Runner、ネイティブ非依存）

`AndroidClipboardJsonBuilderTests`:

- 4 種の Build メソッドが必須キーを必ず含むこと（空値でも省略しない）
- `label` が null / 空白のとき省略されること、非空のとき出力されること
- `isSensitive` が `false` でも出力されること
- `texts` の配列シリアライズ（0 件 / 1 件 / 複数件）
- 特殊文字（`"`、`\`、改行、Unicode）のエスケープ

`AndroidClipboardJsonParserTests`:

- `ParseReadResult`: 内容あり JSON → `HasContent` + Label / MimeTypes / Items のマッピング
- `ParseReadResult`: `"null"` → `Empty`
- `ParseReadResult`: エラー封筒 → `Failed` + `ErrorCode` / `ErrorMessage`（1.7 の全 7 コードを網羅）
- `ParseReadResult`: null / 空白 / 不正 JSON → `Failed` + `CLIPBOARD_UNKNOWN`
- `ParseReadResult`: `htmlText` / `uri` キー欠落時に空文字ではなく null へ正規化されること
- `ParseReadResult`: `items` / `mimeTypes` キー欠落時に空配列へ正規化されること
- `ParseDescriptionResult`: `isStyledText` の true / false、`classificationStatus` の 1〜3 と欠落（`0` → null）
- `ParseHasClip`: `"true"` → true、`"false"` → false、不正値 → false

`AndroidClipboardManagerDispatchTests`:

- `InvokeInOrder` が 共通 event → per-call callback の順で 1 回ずつ呼ぶこと
- 両デリゲートに同一の結果が渡ること
- per-call のみ / 共通のみ / 両方 null でも例外を投げないこと
- どちらかが例外を投げても他方の呼び出しと全体の完了が阻害されないこと
- `ClipboardOperationResult.Success` が `ErrorMessage == null` を保証すること

`AndroidClipboardManager` 自体は `#if UNITY_ANDROID` かつ `AndroidJavaObject` 依存のため、Manager インスタンスを生成するテストは書かない（`internal static` に切り出した純粋関数のみを検証する）。

### 7.2 PlayMode

本計画では追加しない。ネイティブ Bridge 依存部分は実機での手動確認に委ねる（既存 Share の PlayMode テストは iOS / macOS のみで、Android 相当のものが存在しない）。

### 7.3 手動確認（実機、Android 12 以降）

| 観点 | 手順 | 期待 |
|---|---|---|
| プレーンテキストコピー | `CopyPlainText` → 他アプリで貼り付け | テキストが貼り付けられ、`copyPlainText` の成功が event と callback の順で届く |
| HTML コピー | `CopyHtmlText` → HTML 対応アプリで貼り付け | 書式付きで貼り付けられる |
| HTML 本文 blank | `htmlText` を空で `CopyHtmlText` | `Clipboard content is empty. Please provide text or HTML.` |
| URI コピー | `CopyUri` に `content://` を指定 | 成功。不正 URI で `Invalid URI: {uri}` |
| 複数テキスト | `CopyMultipleText` で 3 件 | 成功。空配列で `No items provided for clipboard copy.` |
| 機微フラグ | `isSensitive = true` でコピー | Android 13+ でコピー時のプレビューに内容が出ない |
| 読み取り | コピー後 `Read()` | `HasContent` + items が取得できる |
| 空読み取り | `Clear()` 後 `Read()` | `Empty`（`Failed` ではない） |
| メタデータ | `GetDescription()` | mimeTypes / isStyledText が取得できる |
| 有無判定 | `HasClip()` | コピー後 true、`Clear()` 後 false |
| 監視 | `StartObserving()` → 他アプリでコピー → 復帰 | `ClipboardChanged` が発火する |
| 監視停止 | `StopObserving()` → 他アプリでコピー | 発火しない。`stopObserving` の成功が届く |
| 監視の二重開始 | `StartObserving()` を 2 回 | 変化 1 回に対し `ClipboardChanged` は 1 回のみ |
| バックグラウンド読み取り | ホームへ移動した状態で `Read()` | `Empty` または `CLIPBOARD_READ_NOT_ALLOWED` になり、クラッシュしない |
| ログ安全性 | コピー・読み取りを実行し Logcat / Unity ログを確認 | クリップボード本文が出力されていない |
| ライフサイクル | シーン遷移・アプリ終了 | listener 解除でクラッシュ・リークが起きない |

---

## 8. Definition of Done

- 4.1 の新規ファイルが全て作成されている
- 既存ファイルへの変更がない（4.2）
- 同期 3 メソッドが戻り値を返し、event を発火しない（1.2 の原則）
- 非同期 6 メソッドが共通 event → per-call callback の順で dispatch する
- `StartObserving` に callback 引数が無い
- `Awaitable` 版を含まない（5.9）
- クリップボード本文がどのログにも出力されていない（5.7）
- EditMode テストが全て passed
- 7.3 の手動確認項目が実機で確認済み

---

## 9. 要検証事項

1. **`onClipboardChanged` の呼び出しスレッド**: `UnityAndroidClipboardManager.ClipboardChangeListener` の KDoc は「Callbacks are delivered on the main thread」と記載するが、`ClipboardChangeMonitor.start` の KDoc は「Called on the system listener's callback thread ... Callers that update UI state must marshal to the main thread themselves」と記載しており、`notifyClipboardChanged` は main へ marshal していない。実機で確認するまで**メインスレッド前提を置かず、必ず `UnityMainThreadDispatcher` を通す**。native 側 KDoc の齟齬は native-toolkit へフィードバックする候補
2. **空文字と欠落の同一視**: 5.4 制約1 のとおり `JsonUtility` はキー欠落と空文字を区別できない。空文字アイテムを実際に生成しうるアプリがあるかを実機で確認し、区別が必要と判明した場合は手書きパーサへ切り替える
3. **`stopObserving` の activity 不要呼び出し**: 既存 `TryPrepareCall` は activity を必須としているため、activity なしの呼び出しモード追加が必要（5.5）。`AndroidJavaObject.Call` の引数 0 個呼び出しが既存経路に無いため、実装時に動作を確認する
4. **`clear` の operation 名衝突**: native の `OPERATION_CLEAR` は `"clear"` で、C# の `Clear()` メソッド名と一致する。`_pendingOperationCallbacks` のキーとしては問題ないが、将来 `clearClipboardOperationListener` 等と混同しないよう定数経由でのみ参照する
5. **AAR バージョン整合**: 現行同梱 AAR（1.3.0）に clipboard 実装が含まれることは確認済み（1.9）だが、native-toolkit のソースが AAR ビルド後に更新されている可能性がある。implement-feature 実行前に AAR のビルド時点とソースの差分有無を確認する
