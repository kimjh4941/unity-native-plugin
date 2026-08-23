# iOS Clipboard 実装計画 v1

## 基本情報

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- 出力言語: 日本語（**計画書の記述言語のみ。実装コード内の文言・コメントは英語**）
- 位置づけ: **新規機能**。native-toolkit 側に実装済みの iOS Clipboard Bridge（`UnityIosClipboardManagerBridge` / `UnityIosClipboardManager`）を Unity C# へ配線する
- 前提: native-toolkit の iOS clipboard 実装（Swift / ObjC）は完了済み。本計画は Unity 側の追従のみ
- 対象外: サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は `design-sample-scene` スキルで別途設計する
- 後続工程: review-document → implement-feature → design-sample-scene

---

## 0. 前提条件の確認結果（XCFramework）

**確認済み。clipboard シンボルは同梱 XCFramework に存在する。**

確認内容（2026-08-15 実施）:

```
Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/
  unity-ios-native-toolkit-1.3.0.xcframework   ← Unity 公開層（clipboard 15 関数）
  ios-native-toolkit-1.3.0.xcframework         ← IosLibrary
（1.2.0 は削除済み）

nm -gU .../unity-ios-native-toolkit-1.3.0.xcframework/ios-arm64/UnityIosPlugin.framework/UnityIosPlugin
  → clipboardCopy / clipboardAppend / clipboardRead / clipboardReadData /
    clipboardGetSnapshot / clipboardClear / clipboardCreatePasteboard /
    clipboardRemovePasteboard / clipboardDetectPatterns / clipboardDetectValues /
    clipboardLoadItem / clipboardCancelLoads / clipboardStartObserving /
    clipboardStopObserving / clipboardCheckForegroundChange の 15 関数すべてを確認
  → ios-arm64（実機）・ios-arm64_x86_64-simulator の両スライスに存在
```

したがって `[DllImport("__Internal")]` のリンク時エラーは発生しない。

残作業:

- 1.3.0 XCFramework の `.meta` は未生成（git 上は untracked）。**Unity での次回 import で自動生成される**。`common.md` のルールどおりエージェントは `.meta` を作成しない
- import 後、Inspector で iOS プラットフォームが有効になっていることを確認する（`PreBuildProcessor` が自動設定する範囲。4.3 参照）

補足: `PreBuildProcessor` / `PostBuildProcessor` は `*.xcframework` をサフィックス一致で探索しており、**バージョン番号をハードコードしていない**。1.2.0 → 1.3.0 の差し替えに伴う Editor 側コード変更は不要（4.3 参照）。

---

## 1. native-toolkit 確認結果（UnityIosPlugin / IosLibrary）

### 1.1 公開 C 関数一覧（`UnityIosClipboardManagerBridge.h`）

すべて `extern "C"`、`const char*` は UTF-8。

| # | 関数 | 引数 | コールバック型 |
|---|---|---|---|
| P-1 | `clipboardCopy` | `(const char* requestJson, ClipboardOperationCallback)` | Operation |
| P-2 | `clipboardAppend` | `(const char* requestJson, ClipboardOperationCallback)` | Operation |
| P-3 | `clipboardRead` | `(const char* requestJson, ClipboardJsonCallback)` | Json |
| P-4 | `clipboardReadData` | `(const char* requestJson, ClipboardJsonCallback)` | Json |
| P-5 | `clipboardGetSnapshot` | `(const char* requestJson, ClipboardJsonCallback)` | Json |
| P-6 | `clipboardClear` | `(const char* requestJson, ClipboardOperationCallback)` | Operation |
| P-7 | `clipboardCreatePasteboard` | `(const char* requestJson, ClipboardJsonCallback)` | Json |
| P-8 | `clipboardRemovePasteboard` | `(const char* requestJson, ClipboardOperationCallback)` | Operation |
| P-9 | `clipboardDetectPatterns` | `(const char* requestJson, ClipboardJsonCallback)` | Json |
| P-10 | `clipboardDetectValues` | `(const char* requestJson, ClipboardJsonCallback)` | Json |
| P-11 | `clipboardLoadItem` | `(const char* requestJson, ClipboardJsonCallback)` | Json |
| P-12 | `clipboardCancelLoads` | `(ClipboardOperationCallback)` | Operation |
| P-13 | `clipboardStartObserving` | `(const char* requestJson, ClipboardChangeCallback, ClipboardOperationCallback)` | Change + Operation |
| P-14 | `clipboardStopObserving` | `(ClipboardOperationCallback)` | Operation |
| P-15 | `clipboardCheckForegroundChange` | `(const char* requestJson, ClipboardJsonCallback)` | Json |

参照: [UnityIosClipboardManagerBridge.h](/Users/jonghyunkim/Desktop/native-toolkit/ios/UnityIosPlugin/UnityIosPlugin/Clipboard/UnityIosClipboardManagerBridge.h)

**P-16（`makePasteControl` / `UIPasteControl`）は Bridge に公開されていない。** 本計画の対象外。

### 1.2 コールバック型（3 種）

```c
typedef void (*ClipboardOperationCallback)(bool isSuccess, const char* errorCode, const char* errorMessage);
typedef void (*ClipboardJsonCallback)(const char* json);
typedef void (*ClipboardChangeCallback)(const char* eventJson);
```

- `errorCode` / `errorMessage` は `isSuccess == true` のとき NULL
- コールバックは**メインスレッドで、1 呼び出しにつき厳密に 1 回**（`P-13` の change callback を除く）
- コールバック引数の C 文字列は**コールバックの実行中のみ有効**。ポインタを保持してはならない
- **コールバックに NULL を渡してよい**（結果は破棄される。クラッシュしない）

### 1.3 同期・非同期の区分（本計画で最重要）

**iOS Bridge は 15 関数すべてがコールバック方式（非同期）である。** Android の `read` / `hasClip` / `getDescription` のような同期戻り値 API は 1 つも存在しない。

`agent-rules/coding-rules/common.md`「ネイティブ API が同期なら C# も同期、非同期なら C# も非同期」に従い、**C# 側は 15 操作すべてを共通 event + 任意 per-call callback 方式にする**。

- Android 版のように `Read()` が値を返す同期メソッドを作ってはならない（同期化の禁止に該当し、`UnityMainThreadDispatcher` 経由の戻りと組み合わせるとデッドロックする）
- IosLibrary 側の `IosClipboardManager.read` は同期的に値を返すが、Unity Bridge のファサード `UnityIosClipboardManager` が `Task { @MainActor }` を挟むため、C# から見ると常に非同期である

### 1.4 リクエスト JSON スキーマ（C# → native）

`UnityIosClipboardJsonParser` より確定。**未知のトップレベルキーは無視される**（前方互換）。

| 関数 | requestJson | 必須 |
|---|---|---|
| `clipboardCopy` | `{scope?, content, options?}` | `content` |
| `clipboardAppend` | `{scope?, content}` | `content`。`options` キーが**存在するだけで失敗** |
| `clipboardRead` | `{scope?}` | なし |
| `clipboardReadData` | `{scope?, utType}` | `utType` |
| `clipboardGetSnapshot` | `{scope?, matchingTypes?}` | なし |
| `clipboardClear` | `{scope?}` | なし |
| `clipboardCreatePasteboard` | `{request}` | `request`（`scope` ではない） |
| `clipboardRemovePasteboard` | `{scope}` | 実質必須（下記） |
| `clipboardDetectPatterns` | `{scope?, patterns}` | `patterns` |
| `clipboardDetectValues` | `{scope?, patterns}` | `patterns` |
| `clipboardLoadItem` | `{scope?, request}` | `request` |
| `clipboardStartObserving` | `{scope?}` | なし |
| `clipboardCheckForegroundChange` | `{scope?}` | なし |
| `clipboardCancelLoads` / `clipboardStopObserving` | requestJson なし | - |

`scope` の解釈規則（`parseScope`）:

- **キー自体が省略された場合のみ** `.general` になる
- `scope` が存在して不正（null / 文字列 / 配列 / 未知の `kind`）なら**ハードエラー**（`CLIPBOARD_INVALID_REQUEST`）。壊れたリクエストが general pasteboard を誤操作しないための設計
- `{"kind":"general"}` / `{"kind":"named","name":<非空>}` / `{"kind":"unique","name":<非空>}`
- `named` / `unique` の `name` は**空文字不可**

**`clipboardRemovePasteboard` は C# 側で必ず `scope` を送ること。** 省略すると `.general` と解釈され `CLIPBOARD_CANNOT_REMOVE_GENERAL` になる。

### 1.5 `content` スキーマ（9 種）

| kind | 必須キー | 備考 |
|---|---|---|
| `plainText` | `text: string` | 空文字は parse を通る（検証は use case 側、1.9 参照） |
| `htmlText` | `plain: string`, `html: string` | |
| `url` | `urlString: string` | |
| `imageFile` | `path: string` | ローカルファイルパス |
| `imageData` | `base64: string`, `utType: string` | base64 デコード失敗は parse エラー |
| `color` | `red`,`green`,`blue`,`alpha`: **Double** | JSON 上で数値であること |
| `customData` | `base64: string`, `utType: string` | |
| `multipleText` | `texts: [string]` | |
| `multiRepresentation` | `representations: {string: base64string}` | 値は全て base64 |

`options`（`copy` のみ）:

- `localOnly: bool`（省略時 `true`。**非 bool 値はハードエラー**）
- `expirationDate: ISO8601 string | null`（省略可。**小数秒あり・なしの両方を受け付ける**。不正文字列はハードエラー）

### 1.6 `request` スキーマ

- `clipboardCreatePasteboard`: `{"request":{"kind":"named","name":<非空>}}` または `{"request":{"kind":"unique"}}`
- `clipboardLoadItem`: `{"request":{"kind":"text"|"url"|"image"}}` または `{"request":{"kind":"file","utType":<string>}}`

### 1.7 `patterns` スキーマ（rawValue）

`ClipboardDetectionPattern` の rawValue と完全一致が必要（不一致は 1 つでもハードエラー）。

`probableWebURL` / `probableWebSearch` / `number` / `link` / `emailAddress` / `phoneNumber` / `postalAddress` / `calendarEvent` / `flightNumber` / `moneyAmount` / `shipmentTrackingNumber`

### 1.8 レスポンス JSON スキーマ（native → C#）

**Json コールバックは常に封筒形式**:

- 成功: `{"ok":true,"data":<任意>}`（データなしの場合 `data` は `null`）
- 失敗: `{"ok":false,"error":{"code":"<CODE>","message":"<message>","details":{"domain":<string>,"code":<int>}?}}`
- シリアライズ自体に失敗した場合のフォールバック: `{"ok":false,"error":{"code":"CLIPBOARD_UNKNOWN","message":"An unknown error occurred."}}`

`data` の形（関数別）:

| 関数 | data |
|---|---|
| `clipboardRead` | `{"numberOfItems":int,"items":[{"typeIdentifiers":[string],"text":string?,"urlString":string?,"imageDataUTType":string?}]}` |
| `clipboardReadData` | `{"utType":string,"base64":string,"byteCount":int}` または **`null`**（該当データなし） |
| `clipboardGetSnapshot` | `{"hasStrings":bool,"hasURLs":bool,"hasImages":bool,"hasColors":bool,"numberOfItems":int,"typeIdentifiers":[string],"allTypeIdentifiers":[[string]],"matchingItemIndexes":[int]?}` |
| `clipboardCreatePasteboard` | `{"scope":{"kind":...,"name":...}}` |
| `clipboardDetectPatterns` | `{"patterns":[string]}` |
| `clipboardDetectValues` | 1.8.1 参照 |
| `clipboardLoadItem` | `{"kind":"text","text":...}` / `{"kind":"url","urlString":...}` / `{"kind":"imageData","base64":...,"utType":...}` / `{"kind":"file","path":...}` / `{"kind":"unknown"}` |
| `clipboardCheckForegroundChange` | `{"changed":bool}` |

**変更イベント（`ClipboardChangeCallback`）は封筒に包まれない**:

```json
{"scope":{"kind":"general"},"kind":"changed","typesAdded":[...],"typesRemoved":[...]}
{"scope":{...},"kind":"changedDetectedOnForeground"}
{"scope":{...},"kind":"removed"}
{"scope":{...},"kind":"unknown"}
```

#### 1.8.1 `detectValues` の data

```json
{
  "detectedPatterns": [string],
  "probableWebURL": string?, "probableWebSearch": string?, "number": string?,
  "links": [string],
  "emailAddresses": [{"value":string,"label":string?}],
  "phoneNumbers":   [{"value":string,"label":string?}],
  "postalAddresses":[{"street":string?,"city":string?,"state":string?,"postalCode":string?,"country":string?}],
  "calendarEvents": [{"startDate":ISO8601?,"endDate":ISO8601?,"startTimeZone":string?,"endTimeZone":string?,"isAllDay":bool}],
  "flightNumbers":  [{"airline":string,"flightNumber":string}],
  "moneyAmounts":   [{"amount":<number>,"currency":string}],
  "shipmentTrackingNumbers":[{"carrier":string,"trackingNumber":string}]
}
```

`calendarEvents` の日時は**小数秒つき ISO8601**（`Date.ISO8601FormatStyle(includingFractionalSeconds: true)`）で出力される。

### 1.9 エラーコード表（`ClipboardError.errorCode` / `errorDescription`）

**メッセージは固定・英語・入力値を埋め込まない**（`invalidURL(String)` 等の associated value はメッセージに出ない）。

| コード | 主な発生元 | errorMessage |
|---|---|---|
| `CLIPBOARD_EMPTY_CONTENT` | `ClipboardContentValidator`（空 text / 空 data / 空 representation 値） | `Clipboard content is empty. Please provide text or HTML.` |
| `CLIPBOARD_EMPTY_ITEMS` | 空 `texts` / 空 `representations` | `No items provided for clipboard copy.` |
| `CLIPBOARD_EMPTY_PATTERNS` | `DetectPatternsUseCase` / `DetectValuesUseCase` の空 patterns | `No detection patterns were specified.` |
| `CLIPBOARD_INVALID_URL` | URL 検証失敗、`ClipboardMappers` | `The URL is invalid.` |
| `CLIPBOARD_INVALID_TYPE` | `ClipboardTypeIdentifierValidator`（`utType` / representation キー） | `The uniform type identifier is invalid.` |
| `CLIPBOARD_INVALID_NAME` | `CreatePasteboardUseCase`（不正な pasteboard 名） | `The pasteboard name is invalid.` |
| `CLIPBOARD_INVALID_COLOR` | 色成分が有限でない / 0.0...1.0 外 | `Color components must be finite and within 0.0...1.0.` |
| `CLIPBOARD_INVALID_IMAGE_DATA` | 画像デコード失敗 | `The provided image data could not be decoded.` |
| `CLIPBOARD_INVALID_EXPIRATION` | `expirationDate` が過去 | `expirationDate must be in the future.` |
| `CLIPBOARD_INVALID_REQUEST` | **JSON parse 層すべて** / `append` への `options` / 空 `imageFile` path | `The request is invalid.` |
| `CLIPBOARD_CONTENT_TOO_LARGE` | 64MB / 1 億ピクセル超過 | `The clipboard content exceeds the configured size limit.` |
| `CLIPBOARD_FILE_NOT_FOUND` | `imageFile` の実体なし | `The requested file was not found.` |
| `CLIPBOARD_IMAGE_LOAD_FAILED` | 画像読み込み失敗 | `Failed to load the image.` |
| `CLIPBOARD_IMAGE_ENCODE_FAILED` | PNG エンコード失敗 | `Failed to encode the pasted image.` |
| `CLIPBOARD_UNAVAILABLE` | `PasteboardResolver` / `startObserving` の scope 解決失敗 | `The requested pasteboard is unavailable.` |
| `CLIPBOARD_CANNOT_REMOVE_GENERAL` | `removePasteboard(.general)` | `The general pasteboard cannot be removed.` |
| `CLIPBOARD_NO_MATCHING_ITEM` | `loadItem` に該当 provider なし | `No clipboard item matches the requested type.` |
| `CLIPBOARD_LOAD_FAILED` | `NSItemProvider` ロード失敗（`details` あり） | `Failed to load the clipboard item.` |
| `CLIPBOARD_UNEXPECTED_TYPE` | 要求型へ変換できない | `The clipboard item could not be converted to the requested type.` |
| `CLIPBOARD_FILE_COPY_FAILED` | 一時ファイルコピー失敗（`details` あり） | `Failed to copy the pasted file.` |
| `CLIPBOARD_CANCELLED` | `cancelLoads` / タスクキャンセル | `The clipboard load was cancelled.` |
| `CLIPBOARD_TIMED_OUT` | detection 5s / providerLoad 15s / imageCoding 10s 超過 | `The clipboard operation timed out.` |
| `CLIPBOARD_DETECTION_FAILED` | detection のシステム失敗（`details` あり） | `Pattern detection failed.` |
| `CLIPBOARD_UNKNOWN` | 上記に分類できないシステムエラー | `An unknown error occurred.` |

`details` は `providerLoadFailed` / `fileCopyFailed` / `detectionFailed` / `unknown` の 4 ケースにのみ付く（`{"domain":string,"code":int}`）。

**`CLIPBOARD_CANCELLED` は正常な打ち切りとして扱ってよい**（native 側 doc の明示的な指示）。

### 1.10 制限値・タイムアウト（`ClipboardLimits.default` / `ClipboardTimeouts.default`）

| 項目 | 値 |
|---|---|
| `maxCopyByteCount` | 64 MiB |
| `maxLoadByteCount` | 64 MiB |
| `maxImagePixelCount` | 100,000,000 |
| detection timeout | 5.0 s |
| providerLoad timeout | 15.0 s |
| imageCoding timeout | 10.0 s |

`UnityIosClipboardManager.shared` は `IosClipboardManager.shared`（default 値）を使うため、**Unity 側から制限値・タイムアウトを変更する手段はない**。

### 1.11 その他の重要な性質

- `clipboardStartObserving` の 2 回目の呼び出しは、**先に前回の監視を停止する**（購読は常に高々 1 つ）。世代ゲートにより古い通知は破棄される
- named / unique pasteboard は**非永続**。作成したアプリが生きている間だけ存在する
- `append` は privacy options を引き継ぐ保証がない。機微データは必ず `copy` を使う
- `checkForegroundChange` は失敗しない（常に `{"ok":true,...}`）
- `cancelLoads` / `stopObserving` は常に成功する（`isSuccess == true`）

---

## 2. 既存 C# 実装の確認結果（`Packages/com.jonghyunkim.nativetoolkit/Runtime/`）

### 2.1 既存 Clipboard 実装（Android 専用）

| ファイル | ガード | 内容 |
|---|---|---|
| `Clipboard/AndroidClipboardManager.cs` | `#if UNITY_ANDROID` | Singleton + `AndroidJavaProxy` × 2 |
| `Clipboard/AndroidClipboardJsonBuilder.cs` | なし | 手書きシリアライザ |
| `Clipboard/AndroidClipboardJsonParser.cs` | なし | `JsonUtility` + DTO |
| `Clipboard/AndroidClipboardPayloads.cs` | なし | Copy 系 payload |
| `Clipboard/ClipboardOperationResult.cs` | **なし** | `Operation` / `IsSuccess` / `ErrorMessage`（**ErrorCode を持たない**） |
| `Clipboard/ClipboardReadResult.cs` | **なし** | `ClipboardReadStatus` / `ClipItem` / `ClipContents` / `ClipboardReadResult` |
| `Clipboard/ClipboardDescriptionResult.cs` | **なし** | `ClipDescriptionInfo` / `ClipboardDescriptionResult` |

**重要な制約: 無ガードの共通名が既に占有されている。**

`ClipboardOperationResult` / `ClipboardReadResult` / `ClipboardDescriptionResult` / `ClipItem` / `ClipContents` / `ClipboardReadStatus` は `#if` で囲まれておらず、namespace `JonghyunKim.NativeToolkit.Runtime.Clipboard` に**全プラットフォームで存在する**。かつ形状が Android 固有（`label` / `mimeTypes` / `coercedText`、`ErrorCode` なし）で iOS の形と一致しない。

したがって **iOS 側の型はすべて `Ios` プレフィックスで新規定義する**。既存型の再利用・改変・共通化は行わない（Android の公開 API を壊すため）。

### 2.2 既存の iOS Manager 実装パターン

`Share/IosShareManager.cs`（本計画の主参照）:

- ガードは `#if UNITY_IOS || UNITY_EDITOR`（クラス全体）、`DllImport` / `[MonoPInvokeCallback]` は `#if UNITY_IOS && !UNITY_EDITOR`
- `MonoBehaviour` Singleton、`Awake` で `DontDestroyOnLoad` + `UnityMainThreadDispatcher.Instance` を先に起こす
- `[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate` + `private static readonly` フィールドで関数ポインタを GC から保護
- per-call callback は `private static Action<T>? s_onXxx`（**last-registered wins**）
- 結果発火は `FireResult` → `UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, common, perCall))`
- `InvokeInOrder` は `internal static` の純粋関数（EditMode テスト対象）

`Notification/IosNotificationManager.cs`（永続コールバックの参照）:

- 操作ごとに `static readonly` delegate を 1 つずつ持つ（`s_showNotificationDelegate` 等）
- 永続コールバック（`setNotificationActionReceivedCallback`）は別枠の `s_persistentActionDelegate`
- `OnDestroy` で `setNotificationActionReceivedCallback(null)` を呼び、native 側の保持を解除

`Clipboard/AndroidClipboardManager.cs`（clipboard 固有ルールの参照）:

- **クリップボード本文をログに出さない**（長さ・有無・フラグのみ）。`csharp.md` の「全パラメータをログ」から意図的に逸脱し、その旨をコメントで明記
- `InvokeInOrder` は **common と perCall を別々の try/catch** で囲む（`IosShareManager` は 1 つの try/catch で、common が投げると perCall が呼ばれない）。本計画は Android 版（別々）を採用する

### 2.3 共通ユーティリティ

- `Common/UnityMainThreadDispatcher.cs`: `Instance.Enqueue(Action)`。`Update` で flush するため EditMode では流れない
- `Common/IconConfiguration.cs`: 本機能では未使用

### 2.4 JSON 取り扱いの既存方針と、iOS で成立しない点

| 方向 | 既存方針 | iOS clipboard での可否 |
|---|---|---|
| C# → native | 手書き `StringBuilder` シリアライザ（`IosShareJsonBuilder`） | **可**。ただし `double`（color 成分）と `Dictionary` 値（representations）の対応追加が必要 |
| native → C# | `JsonUtility` + `[Serializable]` DTO（`AndroidClipboardJsonParser`） | **不可**（下記） |

`JsonUtility` が扱えない要素が iOS レスポンスに含まれる:

1. `allTypeIdentifiers: [[String]]` — **`string[][]` / `List<List<string>>` を `JsonUtility` はデシリアライズできない**（配列の配列は非対応）
2. `data` が値なしのとき `null`（`clipboardReadData`）— `JsonUtility` は `null` オブジェクトと既定値を区別しにくい
3. `loadItem` の `data` が **`kind` による多相**（text / url / imageData / file / unknown で形が変わる）
4. `moneyAmounts[].amount` の数値型が未確定（`Decimal` 由来。文字列でなく数値で出る可能性がある）

したがって **iOS 側は最小限の手書き JSON リーダーを新規実装する**（5.3）。これは既存 Android パーサとの意図的な差分であり、根拠を実装コードのクラスコメントにも残す。

代替案（採用しない）: `allTypeIdentifiers` を C# 公開モデルから落として `JsonUtility` に寄せる。native が返す情報を握り潰すことになるため採らない。ただしスコープ縮小が必要になった場合の退避案として記録する。

---

## 3. 実装対象 API 一覧（C# 公開仕様）

### 3.1 クラス

`JonghyunKim.NativeToolkit.Runtime.Clipboard.IosClipboardManager`（`MonoBehaviour` Singleton）

- ガード: `#if UNITY_IOS || UNITY_EDITOR`（`IosShareManager` と同じ **A 群**。build target を切り替えずに EditMode / PlayMode テストから到達できる）
- `DllImport` 宣言・`[MonoPInvokeCallback]` 実装のみ `#if UNITY_IOS && !UNITY_EDITOR`

### 3.2 `[DllImport]` 宣言

```csharp
[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardCopy(string requestJson, ClipboardOperationCallback callback);
// append / clear / removePasteboard も同形
[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardRead(string requestJson, ClipboardJsonCallback callback);
// readData / getSnapshot / createPasteboard / detectPatterns / detectValues / loadItem
// / checkForegroundChange も同形
[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardCancelLoads(ClipboardOperationCallback callback);
[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardStopObserving(ClipboardOperationCallback callback);
[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardStartObserving(string requestJson,
                                                   ClipboardChangeCallback changeCallback,
                                                   ClipboardOperationCallback startCallback);
```

delegate 定義:

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardOperationCallback(bool isSuccess, string? errorCode, string? errorMessage);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardJsonCallback(string? json);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardChangeCallback(string? eventJson);
```

- `const char*` ↔ `string` は既存 `IosShareManager` / `IosNotificationManager` と同じ既定マーシャリングを使う（IL2CPP で UTF-8）
- `errorCode` / `errorMessage` / `json` は native から NULL が来うるため `string?`
- `bool` の幅は 9.1 の要検証事項

### 3.3 公開メソッド（15 操作）

| メソッド | 引数 | per-call callback 型 | 共通 event |
|---|---|---|---|
| `Copy` | `IosClipboardContent content, IosPasteboardScope? scope = null, IosClipboardCopyOptions? options = null` | `Action<IosClipboardOperationResult>?` | `ClipboardOperationCompleted` |
| `Append` | `IosClipboardContent content, IosPasteboardScope? scope = null` | 同上 | `ClipboardOperationCompleted` |
| `Clear` | `IosPasteboardScope? scope = null` | 同上 | `ClipboardOperationCompleted` |
| `RemovePasteboard` | `IosPasteboardScope scope`（必須） | 同上 | `ClipboardOperationCompleted` |
| `CancelLoads` | なし | 同上 | `ClipboardOperationCompleted` |
| `StopObserving` | なし | 同上 | `ClipboardOperationCompleted` |
| `StartObserving` | `IosPasteboardScope? scope = null, Action<IosClipboardChangeEvent>? onChanged = null` | `Action<IosClipboardOperationResult>? onStarted = null` | `ClipboardOperationCompleted` + `ClipboardChanged` |
| `Read` | `IosPasteboardScope? scope = null` | `Action<IosClipboardReadResult>?` | `ReadCompleted` |
| `ReadData` | `string utType, IosPasteboardScope? scope = null` | `Action<IosClipboardReadDataResult>?` | `ReadDataCompleted` |
| `GetSnapshot` | `IosPasteboardScope? scope = null, string[]? matchingTypes = null` | `Action<IosClipboardSnapshotResult>?` | `SnapshotCompleted` |
| `CreatePasteboard` | `IosPasteboardCreationRequest request` | `Action<IosPasteboardScopeResult>?` | `PasteboardCreated` |
| `DetectPatterns` | `IosClipboardDetectionPattern[] patterns, IosPasteboardScope? scope = null` | `Action<IosClipboardDetectedPatternsResult>?` | `PatternsDetected` |
| `DetectValues` | `IosClipboardDetectionPattern[] patterns, IosPasteboardScope? scope = null` | `Action<IosClipboardDetectedValuesResult>?` | `ValuesDetected` |
| `LoadItem` | `IosClipboardLoadRequest request, IosPasteboardScope? scope = null` | `Action<IosClipboardLoadedItemResult>?` | `ItemLoaded` |
| `CheckForegroundChange` | `IosPasteboardScope? scope = null` | `Action<IosClipboardForegroundChangeResult>?` | `ForegroundChangeChecked` |

- per-call callback は**すべて最終引数**（`Action<...>? onResult = null`）
- `scope == null` は「`scope` キーを送らない」= native 側の `.general` にあたる（1.4 の規則どおり、null と `IosPasteboardScope.General` は JSON 上のみ差がある。C# 公開仕様では同義として扱い、XML コメントに明記）

### 3.4 公開イベント（10 個）

```csharp
public event Action<IosClipboardOperationResult>?          ClipboardOperationCompleted;
public event Action<IosClipboardChangeEvent>?              ClipboardChanged;
public event Action<IosClipboardReadResult>?               ReadCompleted;
public event Action<IosClipboardReadDataResult>?           ReadDataCompleted;
public event Action<IosClipboardSnapshotResult>?           SnapshotCompleted;
public event Action<IosPasteboardScopeResult>?             PasteboardCreated;
public event Action<IosClipboardDetectedPatternsResult>?   PatternsDetected;
public event Action<IosClipboardDetectedValuesResult>?     ValuesDetected;
public event Action<IosClipboardLoadedItemResult>?         ItemLoaded;
public event Action<IosClipboardForegroundChangeResult>?   ForegroundChangeChecked;
```

- Operation 系 7 操作は `ClipboardOperationCompleted` を共有し、`Operation` プロパティで判別する（Android 版と同じ構造）
- Json 系 8 操作は payload 型が異なるため個別 event

### 3.5 operation 名定数

native 側に `OPERATION_*` 定数は存在しないため C# 側で定義する（値は Bridge 関数名の接頭辞を除いた形）。

```csharp
public const string OperationCopy             = "copy";
public const string OperationAppend           = "append";
public const string OperationClear            = "clear";
public const string OperationRemovePasteboard = "removePasteboard";
public const string OperationCancelLoads      = "cancelLoads";
public const string OperationStartObserving   = "startObserving";
public const string OperationStopObserving    = "stopObserving";
```

Json 系のログ用に `private const string` で `read` / `readData` / `getSnapshot` / `createPasteboard` / `detectPatterns` / `detectValues` / `loadItem` / `checkForegroundChange` も定義する（結果型には載せない）。

---

## 4. 変更ファイル一覧

`.meta` ファイルは Unity が自動生成するため記載しない。

### 4.1 新規作成（`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/`）

| ファイル | 役割 |
|---|---|
| `IosClipboardManager.cs` | Bridge + Manager 本体（Singleton、15 操作、10 event） |
| `IosClipboardPayloads.cs` | `IosPasteboardScope` / `IosPasteboardCreationRequest` / `IosClipboardContent` / `IosClipboardCopyOptions` / `IosClipboardLoadRequest` / `IosClipboardDetectionPattern` |
| `IosClipboardJsonBuilder.cs` | リクエスト JSON 組み立て（手書きシリアライザ） |
| `IosClipboardJsonReader.cs` | 最小 JSON 値リーダー（`internal`。5.3 参照） |
| `IosClipboardJsonParser.cs` | 封筒 + 各 `data` → 結果型へのマッピング |
| `IosClipboardErrorInfo.cs` | `ErrorCode` / `ErrorMessage` / `ErrorDomain` / `ErrorNativeCode` |
| `IosClipboardOperationResult.cs` | Operation 系 7 操作の結果型 |
| `IosClipboardReadResult.cs` | `IosClipboardItem` + `IosClipboardReadResult` |
| `IosClipboardReadDataResult.cs` | `IosClipboardReadDataResult`（`HasData` 付き） |
| `IosClipboardSnapshotResult.cs` | `IosClipboardSnapshot` + 結果型 |
| `IosPasteboardScopeResult.cs` | `CreatePasteboard` の結果型 |
| `IosClipboardDetectionResults.cs` | `IosClipboardDetectedPatternsResult` / `IosClipboardDetectedValuesResult` + 内部型（labeled value / postal address / calendar event / flight / money / shipment） |
| `IosClipboardLoadedItemResult.cs` | `IosClipboardLoadedItem`（kind 判別）+ 結果型 |
| `IosClipboardForegroundChangeResult.cs` | `Changed` を持つ結果型 |
| `IosClipboardChangeEvent.cs` | `IosClipboardChangeEventKind` + イベント型 |

### 4.2 新規作成（テスト）

`Tests/Runtime/`（EditMode）:

| ファイル | 対象 |
|---|---|
| `IosClipboardJsonReaderTests.cs` | JSON リーダーの構文網羅（エスケープ / ネスト配列 / null / 数値） |
| `IosClipboardJsonBuilderTests.cs` | 15 リクエスト形状 + scope 省略規則 + ISO8601 + base64 |
| `IosClipboardJsonParserTests.cs` | 封筒（ok true/false）、各 data 形状、`data:null`、`details` あり／なし、壊れた JSON |
| `IosClipboardResultTests.cs` | 結果型の不変条件（`IsSuccess == true` なら `ErrorCode == null` 等） |
| `IosClipboardManagerDispatchTests.cs` | `InvokeInOrder`（common → perCall の順序、片方が例外でも他方が呼ばれる） |

`Tests/PlayMode/`:

| ファイル | 対象 |
|---|---|
| `IosClipboardManagerIntegrationTests.cs` | 非実機での失敗経路、dispatcher 経由の順序、`StartObserving` の event 契約 |

### 4.3 既存変更

**なし。**

- `Editor/Build/PreBuildProcessor.cs` / `PostBuildProcessor.cs` / `Tools/iOS/IosFrameworkPatcher.cs` は `*.xcframework` をサフィックス一致で探索しており、バージョン番号をハードコードしていない。0 節の XCFramework 差し替えに伴う変更は不要
- `Runtime/NativeToolkit.Runtime.asmdef` / `Tests/**/*.asmdef` は変更不要（新規ファイルは既存アセンブリ配下）
- `package.json` のバージョン更新は release スキルの担当（本計画では扱わない）

### 4.4 非変更（明示）

- `Runtime/Clipboard/AndroidClipboard*.cs`、`ClipboardOperationResult.cs`、`ClipboardReadResult.cs`、`ClipboardDescriptionResult.cs`（2.1 の理由により一切触らない）
- `Runtime/Common/*`
- サンプルアプリ一式（`design-sample-scene` の担当）

---

## 5. 実装詳細

### 5.1 実装順序（依存順）

1. `IosClipboardErrorInfo.cs` → 各結果型 → `IosClipboardChangeEvent.cs`（他に依存しない）
2. `IosClipboardPayloads.cs`
3. `IosClipboardJsonReader.cs` → `IosClipboardJsonReaderTests.cs`
4. `IosClipboardJsonBuilder.cs` → `IosClipboardJsonBuilderTests.cs`
5. `IosClipboardJsonParser.cs` → `IosClipboardJsonParserTests.cs` / `IosClipboardResultTests.cs`
6. `IosClipboardManager.cs` → `IosClipboardManagerDispatchTests.cs`
7. `IosClipboardManagerIntegrationTests.cs`（PlayMode）

Bridge（`DllImport`）と Manager は同一ファイル（既存 iOS Manager と同じ構成）。

### 5.2 payload 型設計

**判別共用体は「private コンストラクタ + static ファクトリ + kind フィールド」で表現する**（C# に discriminated union がないため。既存 `IosShareResult` 等の factory パターンと整合）。

```csharp
public sealed class IosPasteboardScope
{
    internal string Kind { get; }          // "general" | "named" | "unique"
    internal string? Name { get; }

    public static IosPasteboardScope General { get; } = new("general", null);
    public static IosPasteboardScope Named(string name);   // name が空/空白なら ArgumentException
    public static IosPasteboardScope Unique(string name);  // 同上
}

public sealed class IosClipboardContent
{
    internal string Kind { get; }
    // kind ごとの値（text / plain / html / urlString / path / base64 / utType /
    //                red,green,blue,alpha / texts / representations）
    public static IosClipboardContent PlainText(string text);
    public static IosClipboardContent HtmlText(string plain, string html);
    public static IosClipboardContent Url(string urlString);
    public static IosClipboardContent ImageFile(string path);
    public static IosClipboardContent ImageData(byte[] data, string utType);
    public static IosClipboardContent Color(double red, double green, double blue, double alpha);
    public static IosClipboardContent CustomData(byte[] data, string utType);
    public static IosClipboardContent MultipleText(string[] texts);
    public static IosClipboardContent MultiRepresentation(IReadOnlyDictionary<string, byte[]> representations);
}
```

- `byte[]` → base64 変換は **Builder 側**で行う（payload は生バイト列を保持し、利用者に base64 を要求しない）
- `IosClipboardCopyOptions`: `readonly struct { bool LocalOnly; DateTime? ExpirationDate; }`。既定は `LocalOnly = true`（native の privacy-preserving 既定と一致させる）
- `IosClipboardDetectionPattern`: `enum` + `internal static string ToRawValue(this ...)`。rawValue は 1.7 の文字列と完全一致
- `IosClipboardLoadRequest`: `Text` / `Url` / `Image` / `File(string utType)` の 4 ファクトリ
- `IosPasteboardCreationRequest`: `Named(string name)` / `Unique`

**C# 側では「native が弾く値」を先回りして弾かない**（`ArgumentException` は空 scope 名など明らかな呼び出し側バグに限る）。空文字 text・不正 URL・サイズ超過などは native のエラー契約（1.9）に委ねる。二重検証はメッセージの二系統化を招くため避ける。

### 5.3 `IosClipboardJsonReader`（最小 JSON 値リーダー）

`JsonUtility` が使えない理由は 2.4。次の最小構成で実装する。

- `internal sealed class JsonValue`: `Kind`（`Object` / `Array` / `String` / `Number` / `Bool` / `Null`）+ 型別アクセサ
  - `TryGetObject(key, out JsonValue)` / `AsArray()` / `AsString()` / `AsDouble()` / `AsLong()` / `AsBool()`
  - キー欠落・型不一致は例外にせず `false` / `null` / 既定値を返す（前方互換のため）
- `internal static JsonValue? Parse(string json)`: 再帰下降。失敗時は `null`（例外を投げない）
- 対応: オブジェクト / 配列 / 文字列（`\"` `\\` `\/` `\b` `\f` `\n` `\r` `\t` `\uXXXX`、**サロゲートペア含む**）/ 数値（整数・小数・指数）/ `true` / `false` / `null`
- 非対応（native が出力しないため）: コメント、末尾カンマ、単一引用符、`NaN` / `Infinity`
- 深さ上限（例: 64）を設けて異常入力での stack overflow を防ぐ

**このクラスはネイティブ非依存の純粋ロジックであり、層 1（EditMode）テストの主対象。**

### 5.4 `IosClipboardJsonBuilder`

`IosShareJsonBuilder` の `Dictionary<string, object?>` + `StringBuilder` パターンを踏襲し、次を追加する。

- `case double d:` → `d.ToString("R", CultureInfo.InvariantCulture)`。`NaN` / `Infinity` は JSON に出せないため、値をそのまま出さず**キーごと省略せずに native へ渡して `CLIPBOARD_INVALID_COLOR` を返させる**方針は取れない（不正 JSON になる）。`double.IsFinite` でない場合のみ `0` を出力せず、**C# 側で `ArgumentException`** とする（唯一の先回り検証。理由: 不正 JSON を native へ送ると `CLIPBOARD_INVALID_REQUEST` になり、本来の `CLIPBOARD_INVALID_COLOR` と区別できなくなるため）
- `multiRepresentation` の `representations` は `Dictionary<string, object?>`（値は base64 文字列）としてそのまま `AppendObject` に流す
- `expirationDate` は `value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)`
  - native は小数秒あり・なしの両方を受理する（1.5）。**小数秒なしの UTC 形式**を採用し、丸め差による不一致を避ける
  - `expirationDate == null` のときは**キーごと省略**する（`null` を出しても native は受理するが、省略の方が意図が明確）
- `scope` は `null` のときキーごと省略。非 null のときのみ `{"kind":...,"name":...}` を出力（`general` は `name` を出さない）
- `matchingTypes` は `null` / 空配列のときキーごと省略

メソッド一覧（各 1 リクエストに対応、計 13。`cancelLoads` / `stopObserving` は requestJson を持たない）:

`BuildCopyJson` / `BuildAppendJson` / `BuildReadJson` / `BuildReadDataJson` / `BuildGetSnapshotJson` / `BuildClearJson` / `BuildCreatePasteboardJson` / `BuildRemovePasteboardJson` / `BuildDetectPatternsJson` / `BuildDetectValuesJson` / `BuildLoadItemJson` / `BuildStartObservingJson` / `BuildCheckForegroundChangeJson`

**`BuildAppendJson` は `options` キーを絶対に出力しない**（native が `CLIPBOARD_INVALID_REQUEST` を返すため。`Append` メソッドが `options` 引数を持たないことで構造的に保証する）。

### 5.5 `IosClipboardJsonParser`

```
ParseXxxResult(string? json) → 各結果型
```

共通手順:

1. `json` が null / 空白 → `Failed("CLIPBOARD_UNKNOWN", "Clipboard bridge returned no data.")`
2. `IosClipboardJsonReader.Parse` が null → `Failed("CLIPBOARD_UNKNOWN", "Failed to parse the clipboard response.")`
3. `ok` が `false` → `error.code` / `error.message` / `error.details.domain` / `error.details.code` を取り出して `Failed(...)`
   - `code` 欠落時は `"CLIPBOARD_UNKNOWN"`、`message` 欠落時は `"An unknown error occurred."` にフォールバック
4. `ok` が `true` → `data` を型別にマッピング

型別マッピングの注意点:

- `ParseReadDataResult`: `data` が `Null` のとき **失敗ではなく `HasData == false` の成功**として返す（1.8）。`base64` は `Convert.FromBase64String` で `byte[]` へ。デコード失敗時は `CLIPBOARD_UNKNOWN` の失敗にフォールバックする
- `ParseSnapshotResult`: `allTypeIdentifiers` は `IReadOnlyList<IReadOnlyList<string>>`。`matchingItemIndexes` は `Null` のとき `null`（`matchingTypes` 未指定を意味する）で、空配列（該当なし）と区別する
- `ParseLoadedItemResult`: `kind` で分岐。`"unknown"` および未知の kind は `IosClipboardLoadedItemKind.Unknown` として**成功扱い**で返す（native が `ok:true` で返すため、C# 側でエラーへ変換しない）
- `ParseDetectedValuesResult`: `moneyAmounts[].amount` は数値・文字列の両方を受け付け、`decimal`（不可なら `string RawAmount`）へ格納する（9.2 の要検証事項）。日時は `DateTimeOffset.TryParse(..., DateTimeStyles.RoundtripKind)` で解釈し、失敗時は `null`
- `ParseChangeEvent`: 封筒なし。`kind` 未知は `Unknown`。`typesAdded` / `typesRemoved` 欠落は空配列

**ログ規約**: 本文・base64 をログに出さない。出してよいのは `ok`、`errorCode`、件数、バイト数、`kind` のみ。クラスコメントに `AndroidClipboardJsonParser` と同じ趣旨の逸脱理由を記載する。

### 5.6 `IosClipboardManager` 本体

#### 5.6.1 Singleton / ライフサイクル

```csharp
public static IosClipboardManager Instance { get; }   // 無ければ GameObject を生成し DontDestroyOnLoad

private void Awake()
{
    // _instance 設定 / 重複破棄
    _ = UnityMainThreadDispatcher.Instance;   // メインスレッド上で先に起こす
}

private void OnDestroy()
{
    if (_instance != this) return;
#if UNITY_IOS && !UNITY_EDITOR
    if (Application.platform == RuntimePlatform.IPhonePlayer)
    {
        clipboardStopObserving(null);   // native 側の change callback 保持を解除
        clipboardCancelLoads(null);     // 進行中の loadItem を打ち切る
    }
#endif
    ClearAllPendingCallbacks();
    _instance = null;
}
```

- `Initialize()` は不要（native 側に setup 関数がない）。`IosNotificationManager` と異なり `Awake` からの初期化呼び出しは行わない
- `OnDestroy` で `null` コールバックを渡すのは native 契約上安全（1.2）

#### 5.6.2 delegate / callback スロット

操作ごとに `static readonly` delegate を 1 つ、per-call callback スロットを 1 つ持つ（計 15 + 変更通知用の永続 delegate 1）。

```csharp
#if UNITY_IOS && !UNITY_EDITOR
private static readonly ClipboardOperationCallback s_copyDelegate             = OnCopyResult;
private static readonly ClipboardOperationCallback s_appendDelegate           = OnAppendResult;
private static readonly ClipboardOperationCallback s_clearDelegate            = OnClearResult;
private static readonly ClipboardOperationCallback s_removePasteboardDelegate = OnRemovePasteboardResult;
private static readonly ClipboardOperationCallback s_cancelLoadsDelegate      = OnCancelLoadsResult;
private static readonly ClipboardOperationCallback s_startObservingDelegate   = OnStartObservingResult;
private static readonly ClipboardOperationCallback s_stopObservingDelegate    = OnStopObservingResult;
private static readonly ClipboardJsonCallback      s_readDelegate             = OnReadResult;
// ... readData / snapshot / createPasteboard / detectPatterns / detectValues
//     / loadItem / checkForegroundChange
// Persistent: invoked many times per StartObserving; must never be collected.
private static readonly ClipboardChangeCallback    s_changeDelegate           = OnClipboardChanged;
#endif

// Per-call user callbacks. Last-registered wins for the same operation.
private static Action<IosClipboardOperationResult>? s_onCopy;
// ... 各操作ぶん
private static Action<IosClipboardChangeEvent>?     s_onChanged;
```

#### 5.6.3 呼び出しの共通手順（Operation 系）

```csharp
public void Copy(IosClipboardContent content,
                 IosPasteboardScope? scope = null,
                 IosClipboardCopyOptions? options = null,
                 Action<IosClipboardOperationResult>? onResult = null)
{
    // Log shape only: clipboard content may hold passwords or tokens.
    Debug.Log($"[{LogTag}][{nameof(Copy)}] kind: {content?.Kind}, hasScope: {scope != null}, " +
              $"hasOptions: {options != null}, hasCallback: {onResult != null}");

    s_onCopy = onResult;

    if (content == null)
    {
        FireOperationResult(IosClipboardOperationResult.Failure(
            OperationCopy, InvalidRequestErrorCode, "content must not be null."));
        return;
    }

#if UNITY_IOS && !UNITY_EDITOR
    if (Application.platform != RuntimePlatform.IPhonePlayer)
    {
        FireOperationResult(UnavailableFailure(OperationCopy));
        return;
    }

    try
    {
        clipboardCopy(IosClipboardJsonBuilder.BuildCopyJson(content, scope, options), s_copyDelegate);
    }
    catch (Exception ex)
    {
        Debug.LogError($"[{LogTag}][{nameof(Copy)}] {ex.Message}");
        FireOperationResult(IosClipboardOperationResult.Failure(
            OperationCopy, BridgeUnavailableErrorCode, $"{OperationCopy} could not be started."));
    }
#else
    FireOperationResult(UnavailableFailure(OperationCopy));
#endif
}
```

Json 系も同形（`FireXxxResult` と対応する結果型が変わるだけ）。

#### 5.6.4 `StartObserving` / `StopObserving`

```csharp
public void StartObserving(IosPasteboardScope? scope = null,
                           Action<IosClipboardChangeEvent>? onChanged = null,
                           Action<IosClipboardOperationResult>? onStarted = null)
```

- `s_onChanged = onChanged` を設定してから native を呼ぶ
- native は 2 回目の `startObserving` で前回の監視を先に停止する（1.11）。C# 側もスロットを上書きするだけでよい（last-registered wins が native の挙動と一致する）
- `StopObserving` 成功時に `s_onChanged = null` にする（**共通 event `ClipboardChanged` は購読者が明示的に解除するまで残す**。Manager が勝手に event を解除しない）
- `StartObserving` は `ClipboardOperationCompleted`（`Operation == "startObserving"`）でも結果を通知する。Android 版と異なり **iOS は開始成否を返せる**

#### 5.6.5 コールバック実装（`[MonoPInvokeCallback]`）

```csharp
#if UNITY_IOS && !UNITY_EDITOR
[MonoPInvokeCallback(typeof(ClipboardOperationCallback))]
private static void OnCopyResult(bool isSuccess, string? errorCode, string? errorMessage)
{
    FireOperationResult(isSuccess
        ? IosClipboardOperationResult.Success(OperationCopy)
        : IosClipboardOperationResult.Failure(OperationCopy, errorCode, errorMessage));
}

[MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
private static void OnReadResult(string? json)
{
    FireReadResult(IosClipboardJsonParser.ParseReadResult(json));
}

[MonoPInvokeCallback(typeof(ClipboardChangeCallback))]
private static void OnClipboardChanged(string? eventJson)
{
    FireClipboardChanged(IosClipboardJsonParser.ParseChangeEvent(eventJson));
}
#endif
```

- IL2CPP 制約: `[MonoPInvokeCallback]` を付ける実装は**必ず `static`**。インスタンスメンバへは `_instance?.` 経由でアクセスする
- コールバック内で例外を native へ抜けさせない。`FireXxx` 側で `try/catch` する（5.6.6）
- コールバック引数の `string` はマーシャラがコピー済みのため、そのまま保持してよい（`const char*` の寿命問題は発生しない）

#### 5.6.6 dispatch 順序と例外分離

```csharp
private static void FireOperationResult(IosClipboardOperationResult result)
{
    var perCall = TakeOperationCallback(result.Operation);   // snapshot & clear
    var common  = _instance?.ClipboardOperationCompleted;
    UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, common, perCall));
}

/// <summary>
/// Invokes the common event first, then the per-call callback. Each is wrapped in its own
/// try/catch so a throwing subscriber cannot suppress the other.
/// Extracted as a pure, Unity-lifecycle-independent helper for EditMode tests.
/// </summary>
internal static void InvokeInOrder(IosClipboardOperationResult result,
                                   Action<IosClipboardOperationResult>? common,
                                   Action<IosClipboardOperationResult>? perCall)
```

**契約（全 15 操作で統一）:**

1. 共通 event を先に発火し、次に per-call callback を呼ぶ
2. per-call callback は発火直前にスナップショットして**スロットをクリア**する（同一操作の連続呼び出しで別の callback へ結果が渡らないようにする）
3. 同一操作の連続呼び出しは **last-registered wins**（`IosNotificationManager` / `IosShareManager` と同じ）。1 回目の per-call callback は呼ばれない
4. 共通 event は per-call callback の指定有無に関わらず**常に**発火する
5. どちらの例外も `Debug.LogError` で握り潰し、native へ抜けさせない
6. すべて `UnityMainThreadDispatcher` 経由（native は main thread 保証だが、Unity の player loop 外での Unity API 呼び出しを避けるため）

`InvokeInOrder` は結果型ごとにジェネリックで 1 つ実装してよい（`internal static void InvokeInOrder<T>(T result, Action<T>? common, Action<T>? perCall)`）。EditMode テストからは `IosClipboardOperationResult` と 1 つの Json 系型で検証する。

#### 5.6.7 ログ規約（clipboard 固有の逸脱）

`csharp.md` は「全メソッドの先頭で全パラメータをログ」と定めるが、**clipboard 本文はパスワード・トークンを含みうる**ため、`AndroidClipboardManager` と同じ扱いにする。

- 出してよい: `kind`、文字数、バイト数、件数、`hasScope` / `hasCallback` などの真偽、`utType`、`errorCode`、`Operation`
- 出してはならない: `text` / `plain` / `html` / `urlString` / `path` / `base64` / `representations` の値 / 検出された値（メール・電話・住所等）/ pasteboard 名
- 逸脱理由をクラスコメントに英語で明記する（native 側 `ClipboardRedaction` と同じ趣旨）

### 5.7 非同期版（`Awaitable`）の扱い

**本計画では callback 版のみを実装する。`XxxAsync` は作らない。**

`common.md`「多重呼び出しガード」より:

- per-call callback を static スロット 1 つで last-registered wins にしている実装に `Awaitable` 版を足すと、上書きされた側の `AwaitableCompletionSource` が完了せず**永久にハングする**
- したがって in-flight ガードを入れるまで `Awaitable` 版を作らないのが正しい初期実装である

補足: clipboard 操作は UI を占有しないため OS 上は同時実行できる。将来 `Awaitable` 版を足す場合は、last-registered wins をやめて**呼び出しごとの完了トークンで結果を対応付ける**設計変更が前提になる（`ShareChooserActionCallbackCoordinator` に近い形）。本計画のスコープ外として記録する。

### 5.8 スレッド契約

- native コールバックは**メインスレッドで、1 呼び出しにつき 1 回**（native ヘッダの保証）
- それでも結果発火は `UnityMainThreadDispatcher.Instance.Enqueue` を通す（既存 Manager 全実装と統一。player loop 外での Unity API 呼び出しを避ける）
- `UnityMainThreadDispatcher.Instance` は `Awake` でメインスレッド上に生成しておく
- 変更イベント（`ClipboardChanged`）も同じ経路を通る。**発火順序は native の到着順を保つ**（`Enqueue` は FIFO）

### 5.9 メモリ契約

- `[UnmanagedFunctionPointer]` delegate は `static readonly` フィールドで保持し、GC による関数ポインタ回収を防ぐ
- 変更通知用 delegate（`s_changeDelegate`）は**監視中ずっと native から呼ばれる**ため、特に static 保持が必須
- native から渡る `const char*` は `string` へマーシャル済み（コピー）。ポインタを保持しない
- `byte[]`（imageData / customData / multiRepresentation）は base64 文字列へ変換した時点で 2 倍以上のメモリを一時的に消費する。**64MiB 上限（1.10）に対し、リクエスト JSON はおよそ 1.4 倍に膨らむ**。大きな画像は `ImageFile(path)` の利用を XML コメントで推奨する
- `Marshal.AllocHGlobal` は使わない（string[] のポインタ渡しが不要なため）

### 5.10 IL2CPP 制約

- `[MonoPInvokeCallback(typeof(...))]` を付ける実装は `static` 必須
- delegate 型は `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` を明示（native は `extern "C"` / cdecl）
- `AndroidJavaProxy` は iOS では使わないため、Android 固有の proxy 制約は本計画に該当しない
- `using AOT;` は `#if UNITY_IOS && !UNITY_EDITOR` 内でのみ有効にする（`IosShareManager` と同じ）

---

## 6. エラーケース一覧と返却仕様（層別）

### 6.1 native parser 層（`UnityIosClipboardJsonParser` / `UnityIosClipboardManager`）

すべて `CLIPBOARD_INVALID_REQUEST` / `The request is invalid.` で返る。**個別の理由は C# へ伝わらない。**

| # | 条件 | 該当関数 |
|---|---|---|
| 1 | `requestJson` が NULL | 全 requestJson あり関数 |
| 2 | JSON として parse 不能 / トップレベルがオブジェクトでない | 同上 |
| 3 | `scope` が存在して非オブジェクト / `kind` 欠落 / 未知 `kind` | scope を取る全関数 |
| 4 | `scope.kind` が `named` / `unique` で `name` 欠落 or 空 | 同上 |
| 5 | `content` 欠落 / 非オブジェクト / `kind` 欠落 / 未知 `kind` | copy, append |
| 6 | `content` の kind 別必須キー欠落・型不一致 | copy, append |
| 7 | `imageData` / `customData` / `multiRepresentation` の base64 デコード失敗 | copy, append |
| 8 | `color` の成分が Double でない | copy, append |
| 9 | `options` が存在して非オブジェクト | copy |
| 10 | `options.localOnly` が存在して非 bool | copy |
| 11 | `options.expirationDate` が文字列でも null でもない / ISO8601 として解釈できない | copy |
| 12 | **`append` に `options` キーが存在する**（値に関わらず） | append |
| 13 | `utType` 欠落 / 非文字列 | readData |
| 14 | `matchingTypes` が存在して `[String]` でない | getSnapshot |
| 15 | `request` 欠落 / `kind` 欠落 / 未知 `kind` | createPasteboard, loadItem |
| 16 | `createPasteboard` の `kind == "named"` で `name` 欠落 or 空 | createPasteboard |
| 17 | `loadItem` の `kind == "file"` で `utType` 欠落 | loadItem |
| 18 | `patterns` 欠落 / `[String]` でない / 未知 rawValue を 1 つでも含む | detectPatterns, detectValues |

返却経路: Operation 系は `(false, "CLIPBOARD_INVALID_REQUEST", "The request is invalid.")`、Json 系は `{"ok":false,"error":{"code":"CLIPBOARD_INVALID_REQUEST","message":"The request is invalid."}}`。

### 6.2 use case / repository 層（ドメインエラー）

1.9 の全 24 コードが該当する。関数別の主な組み合わせ:

| 関数 | 起こりうるコード |
|---|---|
| `clipboardCopy` | `EMPTY_CONTENT`, `EMPTY_ITEMS`, `INVALID_URL`, `INVALID_TYPE`, `INVALID_COLOR`, `INVALID_IMAGE_DATA`, `INVALID_EXPIRATION`, `INVALID_REQUEST`(空 imageFile path), `CONTENT_TOO_LARGE`, `FILE_NOT_FOUND`, `IMAGE_LOAD_FAILED`, `IMAGE_ENCODE_FAILED`, `UNAVAILABLE`, `TIMED_OUT`, `UNKNOWN` |
| `clipboardAppend` | copy と同じ（`INVALID_EXPIRATION` を除く） |
| `clipboardRead` | `UNAVAILABLE`, `UNKNOWN` |
| `clipboardReadData` | `INVALID_TYPE`, `UNAVAILABLE`, `UNKNOWN` |
| `clipboardGetSnapshot` | `UNAVAILABLE`, `UNKNOWN` |
| `clipboardClear` | `UNAVAILABLE`, `UNKNOWN` |
| `clipboardCreatePasteboard` | `INVALID_NAME`, `UNAVAILABLE`, `UNKNOWN` |
| `clipboardRemovePasteboard` | `CANNOT_REMOVE_GENERAL`, `UNAVAILABLE`, `UNKNOWN` |
| `clipboardDetectPatterns` / `clipboardDetectValues` | `EMPTY_PATTERNS`, `DETECTION_FAILED`(details あり), `TIMED_OUT`, `UNAVAILABLE`, `UNKNOWN` |
| `clipboardLoadItem` | `NO_MATCHING_ITEM`, `LOAD_FAILED`(details あり), `UNEXPECTED_TYPE`, `FILE_COPY_FAILED`(details あり), `CONTENT_TOO_LARGE`, `IMAGE_ENCODE_FAILED`, `INVALID_IMAGE_DATA`, `CANCELLED`, `TIMED_OUT`, `UNAVAILABLE`, `UNKNOWN` |
| `clipboardStartObserving` | `UNAVAILABLE`（scope 解決失敗。監視は開始されない） |
| `clipboardCancelLoads` / `clipboardStopObserving` | **失敗しない**（常に成功） |
| `clipboardCheckForegroundChange` | **失敗しない**（常に `{"ok":true,...}`） |

### 6.3 C# Bridge 層（本計画で新規に定義するエラー）

| # | 条件 | ErrorCode | ErrorMessage |
|---|---|---|---|
| B-1 | 非 iOS プラットフォーム（Editor 含む） | `CLIPBOARD_BRIDGE_UNAVAILABLE` | `{operation} is only available on an iOS device.` |
| B-2 | `DllImport` 呼び出しが例外を投げた | `CLIPBOARD_BRIDGE_UNAVAILABLE` | `{operation} could not be started.` |
| B-3 | 必須引数が `null`（`content` / `request` / `utType` / `patterns` / `scope`） | `CLIPBOARD_INVALID_REQUEST` | `{parameterName} must not be null.` |
| B-4 | `patterns` が空配列 | `CLIPBOARD_EMPTY_PATTERNS` | `No detection patterns were specified.`（native と同一文言に揃える。native 到達前に返す） |
| B-5 | native から返った JSON が null / 空白 | `CLIPBOARD_UNKNOWN` | `Clipboard bridge returned no data.` |
| B-6 | native から返った JSON が parse 不能 | `CLIPBOARD_UNKNOWN` | `Failed to parse the clipboard response.` |
| B-7 | 封筒の `error.code` が欠落 | `CLIPBOARD_UNKNOWN` | `error.message` があればそれ、無ければ `An unknown error occurred.` |
| B-8 | `readData` の base64 デコード失敗 | `CLIPBOARD_UNKNOWN` | `Failed to decode the clipboard data.` |

- B-4 は「native と同じ結論を、往復せずに返す」だけの前倒し。文言・コードを native と完全一致させ、利用者から見て区別がつかないようにする
- `CLIPBOARD_BRIDGE_UNAVAILABLE` は **C# 側でのみ使う新規コード**（native の `ClipboardError` には存在しない）。`AndroidClipboardManager` の同名定数と値を揃える
- B-3 の `IosClipboardOperationResult.Failure` は `ErrorCode` を持つ。既存 `ClipboardOperationResult`（Android）は `ErrorCode` を持たないが、これは別型なので影響しない

### 6.4 エラーにならない（正常系として扱う）ケース

| ケース | 扱い |
|---|---|
| `clipboardReadData` の `data == null` | 成功。`HasData == false` |
| `clipboardRead` の `numberOfItems == 0` | 成功。空 `Items` |
| `clipboardLoadItem` の `kind == "unknown"` | 成功。`IosClipboardLoadedItemKind.Unknown` |
| `CLIPBOARD_CANCELLED` | 失敗として返るが、native doc により**無視可能な正常終了**として XML コメントに明記する |
| `getSnapshot` の `matchingItemIndexes == null` | `matchingTypes` 未指定を意味する。空配列（該当なし）と区別する |
| 変更イベントの `kind == "unknown"` | イベントとして発火する（`Unknown`） |

---

## 7. テスト方針

`agent-rules/coding-rules/testing.md` の層モデルに従う。

### 7.1 層 1: EditMode（`Tests/Runtime/`）

`IosClipboardManager` は **A 群**（`#if UNITY_IOS || UNITY_EDITOR`）のため、build target を切り替えずにテストできる。ただし **Manager インスタンスを生成するテストは書かない**（`Awake` が `DllImport` に触れうるため）。

| テストファイル | 検証内容 |
|---|---|
| `IosClipboardJsonReaderTests.cs` | オブジェクト / 配列 / **配列の配列** / エスケープ（`\uXXXX`・サロゲートペア）/ 数値（整数・小数・指数・負値）/ `null` / bool。壊れた JSON で例外を投げず `null` を返すこと。深さ上限で打ち切ること |
| `IosClipboardJsonBuilderTests.cs` | 13 リクエストの形。`scope == null` でキーが出ないこと。`scope.general` に `name` が出ないこと。**`BuildAppendJson` に `options` が絶対出ないこと**。`expirationDate` の UTC ISO8601 形式。`matchingTypes` 空配列でキーが出ないこと。base64 変換。非有限 double で `ArgumentException`。制御文字・日本語・絵文字のエスケープ |
| `IosClipboardJsonParserTests.cs` | 封筒 `ok:true` / `ok:false`。`details` あり / なし。`code` 欠落フォールバック。`data:null`（readData）。`allTypeIdentifiers` のネスト配列。`matchingItemIndexes` の null と空配列の区別。`loadItem` の 5 kind。`detectValues` の全フィールド。変更イベントの 4 kind。壊れた JSON / 空文字 / null |
| `IosClipboardResultTests.cs` | 全結果型の不変条件: `IsSuccess == true` なら `ErrorCode == null` かつ `ErrorMessage == null`、`IsSuccess == false` なら `ErrorMessage != null`。`Failure` に null/空白を渡したときの正規化 |
| `IosClipboardManagerDispatchTests.cs` | `InvokeInOrder`: common → perCall の順序、common が例外でも perCall が呼ばれる、perCall が例外でも例外が外へ出ない、両方 null でも落ちない |

### 7.2 層 2a: PlayMode（Editor 内、`Tests/PlayMode/`）

`IosShareManagerIntegrationTests` / `MacShareManagerIntegrationTests` と同じ構成。

| 検証内容 |
|---|
| Editor 実行時に全 15 操作が `CLIPBOARD_BRIDGE_UNAVAILABLE` で失敗結果を返すこと（B-1） |
| dispatcher 経由で共通 event → per-call callback の順序が保たれること |
| per-call callback を指定しなくても共通 event が発火すること |
| 同一操作を連続呼び出ししたとき、1 回目の per-call callback が呼ばれず 2 回目だけが呼ばれること（last-registered wins） |
| `StartObserving` が `ClipboardOperationCompleted`（`Operation == "startObserving"`）を発火すること |
| Editor では `ClipboardChanged` が発火しないこと |
| `Instance` 生成 → `Destroy` → 再取得で例外が出ないこと |

### 7.3 層 2b / 層 3: 実機（本計画では未着手、手動確認で代替）

`testing.md` 7 節のとおり層 2b / 層 3 は未着手。本機能でも自動化は行わず、下記を**手動確認項目**として定義する。

| # | 操作 | 期待 |
|---|---|---|
| M-1 | `Copy(PlainText)` → 他アプリ（メモ等）で貼り付け | 文字列が一致する。**日本語・絵文字が化けない**（UTF-8 マーシャリングの確認、9.1） |
| M-2 | `Copy(HtmlText)` → リッチテキスト対応アプリで貼り付け | 書式が保持される |
| M-3 | `Copy(Url)` / `Copy(ImageFile)` / `Copy(ImageData)` / `Copy(Color)` | それぞれ貼り付け先で期待どおり |
| M-4 | `Copy` に `LocalOnly = false` → 同一 Apple Account の別デバイスで貼り付け | Universal Clipboard に載る。`true`（既定）では載らない |
| M-5 | `Copy` に過去の `ExpirationDate` | `CLIPBOARD_INVALID_EXPIRATION` |
| M-6 | `Append` の後に `Read` | 項目が増えている |
| M-7 | `Read` / `GetSnapshot` を他アプリがコピーした内容に対して実行 | `GetSnapshot` は貼り付け許可プロンプトを出さない。`Read` は出しうる（pasteboard privacy） |
| M-8 | `ReadData` に該当型なし | 成功 + `HasData == false`（失敗にならない） |
| M-9 | `CreatePasteboard(Named)` → `Copy(scope)` → `Read(scope)` → `RemovePasteboard(scope)` | 一連が成功する |
| M-10 | `RemovePasteboard(General)` | `CLIPBOARD_CANNOT_REMOVE_GENERAL` |
| M-11 | `CreatePasteboard(Unique)` の返り `scope` をそのまま `Copy` / `Read` に渡す | 往復して動作する（`kind:"unique"` + 生成名の round-trip） |
| M-12 | `DetectPatterns` / `DetectValues`（メール・電話・URL を含むテキスト） | 検出される。**ログに検出値が出ていないこと** |
| M-13 | `LoadItem(Image)` → `LoadItem(File)` | 画像は PNG、file は一時パスが返る |
| M-14 | `LoadItem` 実行中に `CancelLoads` | `CLIPBOARD_CANCELLED` が返る |
| M-15 | `StartObserving` → 他アプリでコピー → 復帰 | `ClipboardChanged`（`changed` または `changedDetectedOnForeground`）が届く |
| M-16 | `StartObserving` を 2 回連続 | 重複してイベントが届かない（native の世代ゲート） |
| M-17 | `StopObserving` 後に他アプリでコピー | イベントが届かない |
| M-18 | `CheckForegroundChange` をバックグラウンド復帰後に実行 | `Changed == true` |
| M-19 | シーン遷移 / アプリ終了 | `OnDestroy` 後にクラッシュしない（永続 delegate の解除確認） |
| M-20 | 全操作のログ確認 | クリップボード本文・base64・検出値・pasteboard 名がログに一切出ていない |

**実行前提:** IL2CPP / ARM64 / 実機 iOS 18 以降（`common.md` Minimum Versions）。Simulator は pasteboard 挙動が実機と異なるため確認対象に含めない。

### 7.4 テスト実行

- EditMode / PlayMode ともに Unity Test Runner で実行し、全 passed を確認する
- 既存テスト（`AndroidClipboard*Tests` 等）が壊れていないことも同時に確認する（本計画は既存ファイルを変更しないため、壊れる想定はない）
- テストデータに実在の機微値を使わない。サンプル値のみ（`testing.md` 6 節）

---

## 8. Definition of Done

1. 0 節の XCFramework（1.3.0）が Unity に import され、`.meta` が生成されている（シンボル確認は 0 節で完了済み）
2. 4.1 / 4.2 の新規ファイルがすべて作成されている（`.meta` は Unity 自動生成）
3. 4.3 のとおり既存ファイルへの変更が 0 件である（`.meta` の自動生成を除く）
4. 15 操作すべてが「共通 event → per-call callback」の順で結果を返す
5. 6.3 の C# Bridge 層エラー B-1〜B-8 がすべて実装されている
6. 層 1 / 層 2a のテストが追加され、Unity Test Runner で全 passed
7. クリップボード本文・base64・検出値・pasteboard 名がログに出力されない
8. `public` メンバに英語の XML ドキュメントコメントがある
9. 7.3 の手動確認項目 M-1〜M-20 が実機で確認済み（未実施項目は理由とともに記録）

---

## 9. 要検証事項（断定しない）

### 9.1 `bool` のマーシャリング幅

`ClipboardOperationCallback` の第 1 引数は C 側で `bool`（stdbool、1 バイト）。C# の `bool` は既定で `UnmanagedType.Bool`（4 バイト）としてマーシャルされる。

- 既存 `IosShareManager.ShareCallback(bool isSuccess, bool completed, ...)` は素の `bool` で実機動作している実績がある
- 本計画では**既存パターンを踏襲して素の `bool` を使う**（一貫性を優先）
- ただし M-1 等の実機確認で `isSuccess` が誤判定される場合は `[MarshalAs(UnmanagedType.I1)]` を付与する。この判断を implement-feature の実機確認時に確定させる

### 9.2 `moneyAmounts[].amount` の JSON 型

Swift 側は `entry.amount` をそのまま `JSONSerialization` に渡している。`Decimal` の場合 `NSDecimalNumber` として数値で出るが、`String` の可能性も排除できない。

- パーサは**数値・文字列の両方を受け付ける**実装にする（片方だけを前提にしない）
- 公開型は `decimal? Amount` + `string? RawAmount` の併載とし、変換できない場合も情報を落とさない
- 実機の `DetectValues` 出力（M-12）で実型を確定させ、確定後に片方へ寄せるか判断する

### 9.3 `const char*` → `string` の文字コード

IL2CPP は既定で UTF-8 として解釈する想定だが、日本語・絵文字を含むクリップボード内容での往復は未検証。

- M-1 で日本語・絵文字・サロゲートペア（例: 🧑‍🚀）を含む文字列の往復を確認する
- 化ける場合は `[MarshalAs(UnmanagedType.LPUTF8Str)]` の付与を検討する

### 9.4 `Read` の pasteboard privacy プロンプト

`clipboardRead` は `UIPasteboard` の値へアクセスするため、他アプリ由来データに対して**貼り付け許可 UI が出る可能性がある**（`testing.md` 2 節、iOS 16+）。

- プロンプトの有無・条件は未実測。M-7 で実機確認する
- 確認結果を `IosClipboardManager.Read` の XML コメントおよびマニュアルへ反映する（write-manual 工程）

### 9.5 大きな base64 ペイロードの実効上限

64MiB のバイナリは base64 で約 85MB、リクエスト JSON 全体ではさらに大きくなる。C# 文字列 → UTF-8 マーシャリングで一時的に 2〜3 倍のメモリを消費する。

- 実機で許容できる実効サイズは未測定
- 数 MB 程度での動作は確認し、大きな画像は `ImageFile(path)` を推奨する旨を XML コメントに記載する
- 実効上限の測定は本計画のスコープ外（必要になった時点で別途）

### 9.6 `matchingItemIndexes` の JSON 表現

`snapshot.matchingItemIndexes ?? NSNull()` のため、未指定時は `"matchingItemIndexes": null` になる想定。`[Int]` 空配列との区別は JSON 上明確だが、実出力での確認は未実施。M-7 相当の実機確認時に併せて確認する。

### 9.7 `IosPasteboardScope.General` と `null` の等価性

C# API では `scope: null` を「general」として説明するが、JSON 上は「キー省略」と `{"kind":"general"}` の 2 通りになる。native の `parseScope` はどちらも `.general` に解決するため機能差はない想定。

- 実機で両者の挙動差がないことを M-9 のバリエーションとして確認する
- 差があった場合は、C# 側で `null` を常に `{"kind":"general"}` へ正規化する方針へ変更する
