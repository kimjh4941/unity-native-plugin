# iOS Clipboard 実装計画 v2

## 基本情報

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- 出力言語: 日本語（**計画書の記述言語のみ。実装コード内の文言・コメントは英語**）
- 位置づけ: **新規機能**。native-toolkit 側に実装済みの iOS Clipboard Bridge（`UnityIosClipboardManagerBridge` / `UnityIosClipboardManager`）を Unity C# へ配線する
- 前提: native-toolkit の iOS clipboard 実装（Swift / ObjC）は完了済み。本計画は Unity 側の追従のみ
- 対象外: サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は `design-sample-scene` スキルで別途設計する
- 後続工程: review-document → implement-feature → design-sample-scene

### v1 からの変更点（レビュー反映）

対象レビュー: `artifact/reviews/clipboard/2026-08-15-ios-clipboard-design-review-v1.md`（総合評価「要修正（高優先度 2 件）」）

| 指摘 | severity | 対応 |
|---|---|---|
| C `bool`（1 byte）と C# `bool`（既定 4 byte）の ABI 不一致を「要検証」のまま実装しようとしていた | high | **指摘を全面的に受け入れ**。3.2 で `[MarshalAs(UnmanagedType.I1)]` を最初から付与する仕様に確定。旧 9.1 の要検証項目を削除し、実機確認（M-21）を成功／失敗の両系統で追加 |
| 同一操作の並行呼び出しで、A の結果が B の per-call callback へ**誤配送**される（last-registered wins） | high | **指摘を全面的に受け入れ**。5.6.3 に**操作単位の single-flight ガード**を導入し、実行中の再呼び出しへ `CLIPBOARD_BUSY` を即時返す仕様に変更。7.1 / 7.2 に逆順完了・遅延完了・busy 経路のテストを追加。副次的に `common.md` の `Awaitable` 前提条件を満たしたため 5.7 で `XxxAsync` を併設する |
| `moneyAmounts[].amount` を `Decimal` / 文字列の両対応にしていた | medium | **指摘は正しい**。`ClipboardMoneyAmount.amount` は `Double` 確定（下記「検証根拠」）。`double Amount` 単独に確定し、旧 9.2 を削除。**あわせて v1 の `number` の型誤記（`string?` → `double?`）も修正**（レビュー未指摘の自己修正） |
| 公開結果型のプロパティ・nullability・生成規則が一覧化されていない | medium | 3.6 に**全 public 型の完全な仕様表**を新設（プロパティ名・型・nullability・成功時／失敗時の値） |
| malformed envelope / malformed success data / malformed change event の扱いが未定義 | medium | 5.5.1 に**封筒・data 必須フィールドの検証表**、5.5.2 に**変更イベントの破棄規則**を新設。構造不正は既定値で成功を合成せず B-6 の失敗へ統一 |
| response 側（最大 64MiB）の managed peak memory 契約が未評価 | medium | 5.3 の reader を**オフセット保持・遅延実体化**に変更し、5.9.2 に response 側メモリ契約を新設。M-22 / M-23 に実機メモリ確認を追加 |
| XCFramework 1.2.0 → 1.3.0 の差し替えが変更ファイル一覧で分類されていない | medium | 4.0 に「実装前に完了済みの前提変更」と「本計画の成果物」を分離した表を新設 |
| `readonly struct` の `default(IosClipboardCopyOptions)` は `LocalOnly == false` になり、説明と逆の privacy 動作になる | medium | **指摘は正しい**。`sealed class` + factory へ変更し、不正な既定値を構築できない形にした（5.2）。7.1 に null / 省略 / 明示 true / 明示 false の builder テストを追加 |
| 「symbol があるからリンクエラーは発生しない」は言い過ぎ | low | 0 節の表現を「symbol 欠落によるリンクエラーは除外できた」に限定し、import 設定起因の失敗を残リスクとして明記。DoD に iOS build/link smoke test を追加 |
| builder テスト件数が 15 と 13 で不一致 | low | 「13 builder 出力 + requestJson を持たない 2 操作」に統一（4.2 / 5.4 / 7.1） |
| `s_onChanged` が `StartObserving` 失敗時に残留する | low | 5.6.4 に**開始失敗時のスロット解放**を追加。自分が登録した callback であることを確認してからクリアする |

#### 検証根拠（medium 指摘「money amount の型」）

レビューの指摘どおり、native 側で型は確定済みだった。

- `ClipboardMoneyAmount.amount` は `public let amount: Double`（[ClipboardDetectedEntities.swift](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Clipboard/Domain/Model/ClipboardDetectedEntities.swift)）
- `serializeDetectedValues` は `["amount": entry.amount, ...]` をそのまま `JSONSerialization` に渡すため、**JSON 上は必ず数値**になる
- 同ファイルの `ClipboardDetectedValues.number` も `public let number: Double?` であり、v1 の 1.8.1 で `string?` と記載したのは**本計画側の誤り**。v2 で修正した

---

## 0. 前提条件の確認結果（XCFramework）

**clipboard シンボルは同梱 XCFramework に存在する（確認済み）。**

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

**この確認で除外できたのは「symbol 欠落によるリンクエラー」のみである。** 次はいずれも symbol の有無と独立に発生しうるため、残リスクとして扱う。

- Unity の plugin import 設定（iOS プラットフォーム無効、Add to Embedded Binaries の誤設定）
- Xcode 側の target membership / framework embedding / link 設定
- `PreBuildProcessor` / `PostBuildProcessor` によるコピー・パッチ処理の失敗

したがって **DoD に「Unity import 後の iOS build/link smoke test」を含める**（8 節 項目 2）。

残作業:

- 1.3.0 XCFramework の `.meta` は未生成（git 上 untracked）。**Unity での次回 import で自動生成される**。`common.md` のルールどおりエージェントは `.meta` を手書きしない
- import 後、Inspector で iOS プラットフォームが有効であることを確認する

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
- **`bool` は C の `bool`（`<stdbool.h>`、1 byte）** である。C# 側の宣言は 3.2 のとおり幅を明示する

**リクエストを識別する ID・context パラメータは存在しない。** 同一関数の並行呼び出しを native 側で区別する手段がないため、C# 側で single-flight ガードを持つ（5.6.3）。

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

**requestJson を持つのは 13 関数、持たないのは 2 関数**（`cancelLoads` / `stopObserving`）。

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
  "probableWebURL": string?, "probableWebSearch": string?,
  "number": number?,
  "links": [string],
  "emailAddresses": [{"value":string,"label":string?}],
  "phoneNumbers":   [{"value":string,"label":string?}],
  "postalAddresses":[{"street":string?,"city":string?,"state":string?,"postalCode":string?,"country":string?}],
  "calendarEvents": [{"startDate":ISO8601?,"endDate":ISO8601?,"startTimeZone":string?,"endTimeZone":string?,"isAllDay":bool}],
  "flightNumbers":  [{"airline":string,"flightNumber":string}],
  "moneyAmounts":   [{"amount":number,"currency":string}],
  "shipmentTrackingNumbers":[{"carrier":string,"trackingNumber":string}]
}
```

**型は native モデルで確定済み**（[ClipboardDetectedEntities.swift](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Clipboard/Domain/Model/ClipboardDetectedEntities.swift) / [ClipboardDetectedValues.swift](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Clipboard/Domain/Model/ClipboardDetectedValues.swift)）:

- `number` は `Double?` → **JSON number**（v1 の `string?` は誤記）
- `moneyAmounts[].amount` は `Double` → **JSON number**
- `calendarEvents` の日時は**小数秒つき ISO8601**（`Date.ISO8601FormatStyle(includingFractionalSeconds: true)`）
- `probableWebURL` / `probableWebSearch` は該当パターン未検出時 `null`（native が空文字を `nil` に正規化済み）

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
| `CLIPBOARD_CONTENT_TOO_LARGE` | 64MiB / 1 億ピクセル超過 | `The clipboard content exceeds the configured size limit.` |
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

**ただし last-registered wins は本計画では踏襲しない**（5.6.3。同一操作の並行呼び出しでデータが誤配送されるため）。

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
4. 構造不正を検出できない（欠落フィールドを既定値で埋めてしまい、5.5.1 の「不正は失敗にする」契約を実装できない）

したがって **iOS 側は最小限の手書き JSON リーダーを新規実装する**（5.3）。これは既存 Android パーサとの意図的な差分であり、根拠を実装コードのクラスコメントにも残す。

代替案（採用しない）: `allTypeIdentifiers` を C# 公開モデルから落として `JsonUtility` に寄せる。native が返す情報を握り潰すうえ、上記 4 も解決しないため採らない。スコープ縮小が必要になった場合の退避案として記録する。

---

## 3. 実装対象 API 一覧（C# 公開仕様）

### 3.1 クラス

`JonghyunKim.NativeToolkit.Runtime.Clipboard.IosClipboardManager`（`MonoBehaviour` Singleton）

- ガード: `#if UNITY_IOS || UNITY_EDITOR`（`IosShareManager` と同じ **A 群**。build target を切り替えずに EditMode / PlayMode テストから到達できる）
- `DllImport` 宣言・`[MonoPInvokeCallback]` 実装のみ `#if UNITY_IOS && !UNITY_EDITOR`

### 3.2 `[DllImport]` 宣言と ABI（レビュー高優先度 1 の反映）

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardOperationCallback(
    [MarshalAs(UnmanagedType.I1)] bool isSuccess,   // C の bool は 1 byte
    string? errorCode,
    string? errorMessage);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardJsonCallback(string? json);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardChangeCallback(string? eventJson);
```

**`[MarshalAs(UnmanagedType.I1)]` を最初から付与する（確定仕様。要検証扱いにしない）。**

- 根拠: `UnityIosClipboardManagerBridge.h` の `ClipboardOperationCallback` 第 1 引数は C の `bool`（`<stdbool.h>`、**1 byte**）。C# の `bool` は既定で `UnmanagedType.Bool`（Win32 `BOOL`、**4 byte**）としてマーシャルされる
- 既存 `IosShareManager.ShareCallback` が素の `bool` で動作している事実は、**この新しい ABI 宣言が正しいことの根拠にならない**（ARM64 でレジスタ上位ビットが偶然ゼロだった場合も同じ結果になるため）
- 幅を一致させることで、成功／失敗の判定が実行環境依存にならない
- 記録（今回は採用しない代替案）: native 側の callback signature を `uint8_t` に固定すれば ABI が言語仕様上一意になる。native 変更を伴うため本計画では採らないが、将来 native を触る機会があれば検討する

DllImport 宣言:

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

- `const char*` ↔ `string` は既存 `IosShareManager` / `IosNotificationManager` と同じ既定マーシャリングを使う（IL2CPP で UTF-8）
- `errorCode` / `errorMessage` / `json` は native から NULL が来うるため `string?`

### 3.3 公開メソッド（15 操作、callback 版）

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
- `scope == null` は「`scope` キーを送らない」= native 側の `.general` にあたる（9.2 で実機確認する）

### 3.4 公開メソッド（`Awaitable` 版、15 操作）

single-flight ガード（5.6.3）の導入により `common.md`「多重呼び出しガード」の前提条件を満たしたため、**callback 版を包む薄いラッパーとして併設する**（5.7）。

```csharp
public Awaitable<IosClipboardOperationResult> CopyAsync(
    IosClipboardContent content, IosPasteboardScope? scope = null, IosClipboardCopyOptions? options = null);
public Awaitable<IosClipboardReadResult> ReadAsync(IosPasteboardScope? scope = null);
public Awaitable<IosClipboardOperationResult> StartObservingAsync(
    IosPasteboardScope? scope = null, Action<IosClipboardChangeEvent>? onChanged = null);
// 以下、15 操作すべてに XxxAsync を用意する
```

### 3.5 公開イベント（10 個）

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
- 共通 event は per-call callback / `Awaitable` の利用有無に関わらず**常に**発火する

### 3.6 公開型の完全仕様（レビュー中優先度「公開結果型の仕様」の反映）

**共通規約:**

- すべての result 型は `readonly struct`。`IsSuccess == true` ⇔ `Error == null`
- payload 参照型プロパティは `IsSuccess == false` のとき必ず `null`
- collection プロパティは**常に非 null**（失敗時・キー欠落時は空コレクション）。ただし「欠落と空を区別する必要がある」ものだけ nullable にし、表で明示する
- 失敗 factory は `code` が null/空白なら `"CLIPBOARD_UNKNOWN"`、`message` が null/空白なら `"An unknown error occurred."` へ正規化する

#### 3.6.1 エラー情報

| 型 | 種別 | プロパティ | 型 | nullability / 規則 |
|---|---|---|---|---|
| `IosClipboardErrorInfo` | readonly struct | `Code` | `string` | 非 null（正規化済み） |
| | | `Message` | `string` | 非 null（正規化済み） |
| | | `Domain` | `string?` | `error.details.domain`。details なしのとき null |
| | | `NativeCode` | `int?` | `error.details.code`。details なしのとき null |

#### 3.6.2 payload 型（C# → native）

| 型 | 種別 | プロパティ | 型 | 規則 |
|---|---|---|---|---|
| `IosPasteboardScopeKind` | enum | `General` / `Named` / `Unique` | - | - |
| `IosPasteboardScope` | sealed class | `Kind` | `IosPasteboardScopeKind` | - |
| | | `Name` | `string?` | `General` のとき null、他は非 null |
| | | factory | `General`（static プロパティ）/ `Named(string)` / `Unique(string)` | 空・空白名は `ArgumentException` |
| `IosPasteboardCreationRequestKind` | enum | `Named` / `Unique` | - | - |
| `IosPasteboardCreationRequest` | sealed class | `Kind`, `Name` | `...Kind`, `string?` | `Unique` のとき `Name` は null |
| | | factory | `Named(string)` / `Unique`（static プロパティ） | 空・空白名は `ArgumentException` |
| `IosClipboardContentKind` | enum | `PlainText` / `HtmlText` / `Url` / `ImageFile` / `ImageData` / `Color` / `CustomData` / `MultipleText` / `MultiRepresentation` | - | - |
| `IosClipboardContent` | sealed class | `Kind` | `IosClipboardContentKind` | public。値は `internal`（builder / テストのみ参照） |
| | | factory | 1.5 の 9 種に対応（5.2） | `null` 引数は `ArgumentNullException`。色成分が非有限なら `ArgumentException`（5.4） |
| `IosClipboardCopyOptions` | **sealed class** | `LocalOnly` | `bool` | - |
| | | `ExpirationDate` | `DateTime?` | null のときキーごと省略 |
| | | factory | `PrivacyPreservingDefault`（static、`LocalOnly = true` / 期限なし）/ `Create(bool localOnly, DateTime? expirationDate = null)` | struct の `default` 問題を構造的に排除するため class にした |
| `IosClipboardLoadRequestKind` | enum | `Text` / `Url` / `Image` / `File` | - | - |
| `IosClipboardLoadRequest` | sealed class | `Kind`, `UtType` | `...Kind`, `string?` | `File` 以外は `UtType` が null |
| | | factory | `Text` / `Url` / `Image`（static プロパティ）/ `File(string utType)` | - |
| `IosClipboardDetectionPattern` | enum | 1.7 の 11 種 | - | rawValue 変換は `internal static` の対応表 |

#### 3.6.3 result 型（native → C#）

| 型 | プロパティ | 型 | 成功時 | 失敗時 |
|---|---|---|---|---|
| `IosClipboardOperationResult` | `Operation` | `string` | 非 null | 非 null |
| | `IsSuccess` | `bool` | true | false |
| | `Error` | `IosClipboardErrorInfo?` | null | 非 null |
| `IosClipboardReadResult` | `IsSuccess` / `Error` | - | - | - |
| | `NumberOfItems` | `int` | data の値 | 0 |
| | `Items` | `IReadOnlyList<IosClipboardItem>` | 非 null（0 件可） | 空 |
| `IosClipboardItem`（sealed class） | `TypeIdentifiers` | `IReadOnlyList<string>` | 非 null（キー欠落時は空） | - |
| | `Text` / `UrlString` / `ImageDataUtType` | `string?` | JSON の `null` / キー欠落は null | - |
| `IosClipboardReadDataResult` | `IsSuccess` / `Error` | - | - | - |
| | `HasData` | `bool` | data が `null` なら false | false |
| | `UtType` | `string?` | `HasData` が false なら null | null |
| | `Data` | `byte[]?` | `HasData` が false なら null | null |
| | `ByteCount` | `int` | `HasData` が false なら 0 | 0 |
| `IosClipboardSnapshotResult` | `IsSuccess` / `Error` | - | - | - |
| | `Snapshot` | `IosClipboardSnapshot?` | 非 null | null |
| `IosClipboardSnapshot`（sealed class） | `HasStrings` / `HasUrls` / `HasImages` / `HasColors` | `bool` | 必須（欠落は 5.5.1 で失敗） | - |
| | `NumberOfItems` | `int` | 必須 | - |
| | `TypeIdentifiers` | `IReadOnlyList<string>` | 非 null | - |
| | `AllTypeIdentifiers` | `IReadOnlyList<IReadOnlyList<string>>` | 非 null | - |
| | `MatchingItemIndexes` | `IReadOnlyList<int>?` | **null は `matchingTypes` 未指定**。空リストは「該当なし」（区別する） | - |
| `IosPasteboardScopeResult` | `IsSuccess` / `Error` | - | - | - |
| | `Scope` | `IosPasteboardScope?` | 非 null | null |
| `IosClipboardDetectedPatternsResult` | `IsSuccess` / `Error` | - | - | - |
| | `Patterns` | `IReadOnlyList<IosClipboardDetectionPattern>` | 非 null。**未知 rawValue はスキップ**（version skew 対応。件数のみログ） | 空 |
| `IosClipboardDetectedValuesResult` | `IsSuccess` / `Error` | - | - | - |
| | `Values` | `IosClipboardDetectedValues?` | 非 null | null |
| `IosClipboardDetectedValues`（sealed class） | `DetectedPatterns` | `IReadOnlyList<IosClipboardDetectionPattern>` | 非 null | - |
| | `ProbableWebUrl` / `ProbableWebSearch` | `string?` | 未検出は null | - |
| | `Number` | **`double?`** | 未検出は null | - |
| | `Links` | `IReadOnlyList<string>` | 非 null | - |
| | `EmailAddresses` / `PhoneNumbers` | `IReadOnlyList<IosClipboardLabeledValue>` | 非 null | - |
| | `PostalAddresses` | `IReadOnlyList<IosClipboardPostalAddress>` | 非 null | - |
| | `CalendarEvents` | `IReadOnlyList<IosClipboardCalendarEvent>` | 非 null | - |
| | `FlightNumbers` | `IReadOnlyList<IosClipboardFlightNumber>` | 非 null | - |
| | `MoneyAmounts` | `IReadOnlyList<IosClipboardMoneyAmount>` | 非 null | - |
| | `ShipmentTrackingNumbers` | `IReadOnlyList<IosClipboardShipmentTracking>` | 非 null | - |
| `IosClipboardLabeledValue` | `Value` / `Label` | `string` / `string?` | `Value` 必須 | - |
| `IosClipboardPostalAddress` | `Street` / `City` / `State` / `PostalCode` / `Country` | すべて `string?` | すべて任意 | - |
| `IosClipboardCalendarEvent` | `StartDate` / `EndDate` | `DateTimeOffset?` | 解釈失敗・欠落は null | - |
| | `StartTimeZone` / `EndTimeZone` | `string?` | 任意 | - |
| | `IsAllDay` | `bool` | 欠落は false | - |
| `IosClipboardFlightNumber` | `Airline` / `FlightNumber` | `string` / `string` | 両方必須 | - |
| `IosClipboardMoneyAmount` | `Amount` | **`double`** | 必須（JSON number） | - |
| | `Currency` | `string` | 必須 | - |
| `IosClipboardShipmentTracking` | `Carrier` / `TrackingNumber` | `string` / `string` | 両方必須 | - |
| `IosClipboardLoadedItemResult` | `IsSuccess` / `Error` | - | - | - |
| | `Item` | `IosClipboardLoadedItem?` | 非 null | null |
| `IosClipboardLoadedItemKind` | enum | `Text` / `Url` / `ImageData` / `File` / `Unknown` | - | - |
| `IosClipboardLoadedItem`（sealed class） | `Kind` | `IosClipboardLoadedItemKind` | 必須 | - |
| | `Text` | `string?` | `Kind == Text` のときのみ非 null | - |
| | `UrlString` | `string?` | `Kind == Url` のときのみ非 null | - |
| | `Data` / `UtType` | `byte[]?` / `string?` | `Kind == ImageData` のときのみ非 null | - |
| | `Path` | `string?` | `Kind == File` のときのみ非 null | - |
| `IosClipboardForegroundChangeResult` | `IsSuccess` / `Error` | - | - | - |
| | `Changed` | `bool` | data の値 | false |
| `IosClipboardChangeEventKind` | enum | `Changed` / `ChangedDetectedOnForeground` / `Removed` / `Unknown` | - | - |
| `IosClipboardChangeEvent`（sealed class） | `Kind` | `IosClipboardChangeEventKind` | **必須**（欠落イベントは破棄、5.5.2） | - |
| | `Scope` | `IosPasteboardScope?` | 欠落・不正時は null（発火は行う、5.5.2） | - |
| | `TypesAdded` / `TypesRemoved` | `IReadOnlyList<string>` | 非 null（`Changed` 以外は空） | - |

**`ErrorCode` / `ErrorMessage` を result 型の直下に重複公開しない。** `result.Error?.Code` / `result.Error?.Message` に一本化し、配置の曖昧さをなくす。

### 3.7 operation 名定数

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

Json 系 8 操作は single-flight キー・ログ用に `internal const string` で `read` / `readData` / `getSnapshot` / `createPasteboard` / `detectPatterns` / `detectValues` / `loadItem` / `checkForegroundChange` を定義する（result 型には載せない）。

---

## 4. 変更ファイル一覧

`.meta` ファイルは Unity が自動生成するため、**エージェントは一切作成しない**。

### 4.0 XCFramework の成果物境界（レビュー中優先度の反映）

| 分類 | 対象 | 扱い |
|---|---|---|
| **実装前に完了済みの前提変更** | `Plugins/iOS/ios-native-toolkit-1.2.0.xcframework/` 削除 | 本計画の成果物ではない。0 節で確認済み |
| | `Plugins/iOS/unity-ios-native-toolkit-1.2.0.xcframework/` 削除 | 同上 |
| | `Plugins/iOS/ios-native-toolkit-1.2.0.xcframework.meta` 削除 | 同上（Unity 生成物の削除） |
| | `Plugins/iOS/unity-ios-native-toolkit-1.2.0.xcframework.meta` 削除 | 同上 |
| | `Plugins/iOS/ios-native-toolkit-1.3.0.xcframework/` 追加 | 同上 |
| | `Plugins/iOS/unity-ios-native-toolkit-1.3.0.xcframework/` 追加 | 同上 |
| **本計画の成果物に含める** | `Plugins/iOS/ios-native-toolkit-1.3.0.xcframework.meta` | **Unity が import 時に生成**する。エージェントは手書きしない。生成後、最終コミットに含める |
| | `Plugins/iOS/unity-ios-native-toolkit-1.3.0.xcframework.meta` | 同上 |
| | 4.1 / 4.2 の C# ファイルと、それらに対して Unity が生成する `.meta` | 同上 |

「エージェントが `.meta` を手書きしない」ことと「Unity が生成した `.meta` を成果物に含める」ことは別のルールである。前者は作成方法の禁止、後者はコミット対象の定義。

### 4.1 新規作成（`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/`）

| ファイル | 役割 |
|---|---|
| `IosClipboardManager.cs` | Bridge + Manager 本体（Singleton、15 操作 callback 版 + 15 操作 Awaitable 版、10 event、single-flight ガード） |
| `IosClipboardPayloads.cs` | 3.6.2 の payload 型一式 |
| `IosClipboardJsonBuilder.cs` | リクエスト JSON 組み立て（13 メソッド） |
| `IosClipboardJsonReader.cs` | 最小 JSON リーダー（`internal`。オフセット保持・遅延実体化。5.3） |
| `IosClipboardJsonParser.cs` | 封筒検証 + 各 `data` → 結果型へのマッピング（5.5） |
| `IosClipboardErrorInfo.cs` | 3.6.1 |
| `IosClipboardOperationResult.cs` | Operation 系 7 操作の結果型 |
| `IosClipboardReadResult.cs` | `IosClipboardItem` + `IosClipboardReadResult` |
| `IosClipboardReadDataResult.cs` | `IosClipboardReadDataResult` |
| `IosClipboardSnapshotResult.cs` | `IosClipboardSnapshot` + 結果型 |
| `IosPasteboardScopeResult.cs` | `CreatePasteboard` の結果型 |
| `IosClipboardDetectionResults.cs` | 検出系の結果型・エンティティ型一式（3.6.3） |
| `IosClipboardLoadedItemResult.cs` | `IosClipboardLoadedItemKind` / `IosClipboardLoadedItem` + 結果型 |
| `IosClipboardForegroundChangeResult.cs` | `Changed` を持つ結果型 |
| `IosClipboardChangeEvent.cs` | `IosClipboardChangeEventKind` + イベント型 |

### 4.2 新規作成（テスト）

`Tests/Runtime/`（EditMode）:

| ファイル | 対象 |
|---|---|
| `IosClipboardJsonReaderTests.cs` | JSON リーダーの構文網羅、オフセット保持と遅延実体化、深さ上限 |
| `IosClipboardJsonBuilderTests.cs` | **requestJson を持つ 13 操作の出力**（`cancelLoads` / `stopObserving` の 2 操作は requestJson を持たないため builder を持たない）+ scope 省略規則 + options 契約 |
| `IosClipboardJsonParserTests.cs` | 封筒検証表（5.5.1）と data 必須フィールド、変更イベント破棄規則（5.5.2） |
| `IosClipboardResultTests.cs` | 3.6.3 の全 result 型の不変条件・正規化 |
| `IosClipboardManagerDispatchTests.cs` | `InvokeInOrder` の順序・例外分離、`TryBeginOperation` / `EndOperation` の single-flight 遷移（逆順・遅延完了を含む） |

`Tests/PlayMode/`:

| ファイル | 対象 |
|---|---|
| `IosClipboardManagerIntegrationTests.cs` | 非実機での失敗経路、dispatcher 経由の順序、busy 経路、`StartObserving` の event 契約、`Awaitable` 版の完了保証 |

### 4.3 既存変更

**なし。**

- `Editor/Build/PreBuildProcessor.cs` / `PostBuildProcessor.cs` / `Tools/iOS/IosFrameworkPatcher.cs` は `*.xcframework` をサフィックス一致で探索しており、バージョン番号をハードコードしていない
- `Runtime/NativeToolkit.Runtime.asmdef` / `Tests/**/*.asmdef` は変更不要（新規ファイルは既存アセンブリ配下）
- `package.json` のバージョン更新は release スキルの担当

### 4.4 非変更（明示）

- `Runtime/Clipboard/AndroidClipboard*.cs`、`ClipboardOperationResult.cs`、`ClipboardReadResult.cs`、`ClipboardDescriptionResult.cs`（2.1 の理由により一切触らない）
- `Runtime/Common/*`
- サンプルアプリ一式（`design-sample-scene` の担当）

---

## 5. 実装詳細

### 5.1 実装順序（依存順）

1. `IosClipboardErrorInfo.cs` → 各結果型 → `IosClipboardChangeEvent.cs`
2. `IosClipboardPayloads.cs`
3. `IosClipboardJsonReader.cs` → `IosClipboardJsonReaderTests.cs`
4. `IosClipboardJsonBuilder.cs` → `IosClipboardJsonBuilderTests.cs`
5. `IosClipboardJsonParser.cs` → `IosClipboardJsonParserTests.cs` / `IosClipboardResultTests.cs`
6. `IosClipboardManager.cs`（callback 版 → single-flight ガード → `Awaitable` 版）→ `IosClipboardManagerDispatchTests.cs`
7. `IosClipboardManagerIntegrationTests.cs`（PlayMode）

Bridge（`DllImport`）と Manager は同一ファイル（既存 iOS Manager と同じ構成）。

### 5.2 payload 型設計

**判別共用体は「private コンストラクタ + static ファクトリ + kind」で表現する**（C# に discriminated union がないため）。仕様表は 3.6.2。

```csharp
public sealed class IosClipboardContent
{
    public IosClipboardContentKind Kind { get; }
    internal string? Text { get; }        // builder / tests only
    // ... plain / html / urlString / path / data / utType / color 成分 / texts / representations

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

**`IosClipboardCopyOptions` は `sealed class` にする**（レビュー中優先度の反映）:

```csharp
public sealed class IosClipboardCopyOptions
{
    public bool LocalOnly { get; }
    public DateTime? ExpirationDate { get; }

    /// Safe default matching the native `ClipboardCopyOptions.default`:
    /// not shared through Universal Clipboard, no expiration.
    public static IosClipboardCopyOptions PrivacyPreservingDefault { get; }

    public static IosClipboardCopyOptions Create(bool localOnly, DateTime? expirationDate = null);

    private IosClipboardCopyOptions(bool localOnly, DateTime? expirationDate);
}
```

- `readonly struct` にすると `default(IosClipboardCopyOptions)` が `LocalOnly == false` になり、**privacy 既定が説明と逆になる**。class + private コンストラクタで不正な既定値を構築できないようにする
- `Copy(..., options: null)` は「`options` キーを送らない」= native 既定（`localOnly = true`）。`PrivacyPreservingDefault` を明示的に渡した場合と挙動は同じで、JSON 上のみ差がある

**C# 側では「native が弾く値」を先回りして弾かない。** `ArgumentException` / `ArgumentNullException` は次に限る。

| 検証 | 理由 |
|---|---|
| scope / creation request の空・空白名 | 空名は JSON に出した時点で `CLIPBOARD_INVALID_REQUEST` になり、原因が判別できない |
| `content` factory への `null` 引数 | NullReferenceException を後段で起こすより早く落とす |
| 色成分が非有限（`NaN` / `Infinity`） | JSON に出せない値のため、そのまま送ると不正 JSON になり `CLIPBOARD_INVALID_REQUEST` となる。本来の `CLIPBOARD_INVALID_COLOR` と区別できなくなる（5.4） |

空文字 text・不正 URL・サイズ超過・0.0〜1.0 の範囲外などは native のエラー契約（1.9）に委ねる。二重検証はメッセージの二系統化を招く。

### 5.3 `IosClipboardJsonReader`（最小 JSON リーダー）

`JsonUtility` が使えない理由は 2.4。

**構造（レビュー中優先度「response 側メモリ」の反映で v1 から変更）:**

- `internal readonly struct JsonToken`: `Kind` + **元文字列へのオフセット `(int Start, int Length)`**。文字列を eager に切り出さない
- `internal sealed class JsonValue`: `Kind`（`Object` / `Array` / `String` / `Number` / `Bool` / `Null`）+ 子要素。文字列は `JsonToken` を保持し、`AsString()` が呼ばれた時点で初めて `string` を実体化する
- `internal static JsonValue? Parse(string json)`: 再帰下降。失敗時は `null`（例外を投げない）
- `TryGetObject(key, out JsonValue)` / `AsArray()` / `AsString()` / `TryGetDouble` / `TryGetLong` / `TryGetBool`
  - キー欠落・型不一致は例外にせず `false` / `null` を返す。**必須／任意の判定は呼び出し側（parser）が行う**（5.5.1）
- **`TryGetBase64Bytes(string key, out byte[] bytes)`**: base64 文字列を `string` として実体化せず、元文字列のスパンから直接デコードする（`Convert.TryFromBase64Chars(ReadOnlySpan<char>, Span<byte>, out int)`）。64MiB 級のペイロードで managed string の複製を 1 段減らすため（5.9.2）
- 対応: オブジェクト / 配列 / 文字列（`\"` `\\` `\/` `\b` `\f` `\n` `\r` `\t` `\uXXXX`、**サロゲートペア含む**）/ 数値（整数・小数・指数）/ `true` / `false` / `null`
- 非対応（native が出力しない）: コメント、末尾カンマ、単一引用符、`NaN` / `Infinity`
- 深さ上限（64）を設けて異常入力での stack overflow を防ぐ

**エスケープを含む文字列は遅延実体化できない**（`\uXXXX` の展開が必要）。リーダーは「エスケープを含むか」をスキャン時に記録し、含まない場合のみゼロコピー相当の扱いをする。base64 はエスケープを含まないため、上記の最適化が確実に効く。

**このクラスはネイティブ非依存の純粋ロジックであり、層 1（EditMode）テストの主対象。**

### 5.4 `IosClipboardJsonBuilder`

`IosShareJsonBuilder` の `Dictionary<string, object?>` + `StringBuilder` パターンを踏襲し、次を追加する。

- `case double d:` → `d.ToString("R", CultureInfo.InvariantCulture)`。**非有限値は factory 段階で `ArgumentException`** とし、builder には到達させない（5.2）
- `multiRepresentation` の `representations` は `Dictionary<string, object?>`（値は base64 文字列）として `AppendObject` に流す
- `expirationDate` は `value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)`
  - native は小数秒あり・なしの両方を受理する（1.5）。**小数秒なしの UTC 形式**を採用し、丸め差による不一致を避ける
  - `expirationDate == null` のときは**キーごと省略**する
- `scope` は `null` のときキーごと省略。非 null のときのみ `{"kind":...,"name":...}` を出力（`general` は `name` を出さない）
- `matchingTypes` は `null` / 空配列のときキーごと省略

メソッド一覧（**requestJson を持つ 13 操作に 1 対 1 対応**）:

`BuildCopyJson` / `BuildAppendJson` / `BuildReadJson` / `BuildReadDataJson` / `BuildGetSnapshotJson` / `BuildClearJson` / `BuildCreatePasteboardJson` / `BuildRemovePasteboardJson` / `BuildDetectPatternsJson` / `BuildDetectValuesJson` / `BuildLoadItemJson` / `BuildStartObservingJson` / `BuildCheckForegroundChangeJson`

`cancelLoads` / `stopObserving` は requestJson を持たないため builder メソッドを持たない。

**`BuildAppendJson` は `options` キーを絶対に出力しない**（native が `CLIPBOARD_INVALID_REQUEST` を返すため。`Append` / `AppendAsync` が `options` 引数を持たないことで構造的に保証する）。

### 5.5 `IosClipboardJsonParser`

#### 5.5.1 封筒と data の検証規則（レビュー中優先度の反映）

**既定値で成功を合成しない。構造不正はすべて失敗へ倒す。**

| # | 条件 | 結果 |
|---|---|---|
| E-1 | `json` が null / 空白 | 失敗 `CLIPBOARD_UNKNOWN` / `Clipboard bridge returned no data.`（B-5） |
| E-2 | JSON として parse 不能、またはトップレベルがオブジェクトでない | 失敗 `CLIPBOARD_UNKNOWN` / `Failed to parse the clipboard response.`（B-6） |
| E-3 | `ok` キー欠落、または bool でない | **B-6 の失敗**（成功と見なさない） |
| E-4 | `ok == false` かつ `error` が欠落 / 非オブジェクト | 失敗 `CLIPBOARD_UNKNOWN` / `An unknown error occurred.`（B-7） |
| E-5 | `ok == false`、`error.code` 欠落 / 非文字列 | `CLIPBOARD_UNKNOWN` へフォールバック。`error.message` があれば採用 |
| E-6 | `ok == false`、`error.message` 欠落 / 非文字列 | `An unknown error occurred.` へフォールバック |
| E-7 | `ok == false`、`error.details` が非オブジェクト、または `domain` / `code` の片方のみ | `Domain` / `NativeCode` をともに null にする（エラー自体は正常に返す） |
| E-8 | `ok == true` かつ `data` キー欠落 | `readData` は「データなしの成功」、**それ以外は B-6 の失敗** |
| E-9 | `ok == true` かつ `data` が `null` | 同上 |
| E-10 | `ok == true` かつ `data` の型が期待と不一致（オブジェクト期待に配列など） | **B-6 の失敗** |
| E-11 | `ok == true` かつ data の**必須フィールド**が欠落 / 型不一致 | **B-6 の失敗** |
| E-12 | `ok == true` かつ data の**任意フィールド**が欠落 / `null` | 3.6.3 の規則（null / 空コレクション / false / 0） |
| E-13 | base64 のデコード失敗 | 失敗 `CLIPBOARD_UNKNOWN` / `Failed to decode the clipboard data.`（B-8） |

**data の必須フィールド表:**

| 関数 | 必須 | 任意 |
|---|---|---|
| `read` | `numberOfItems`(number), `items`(array) | items 要素の `typeIdentifiers` は欠落時 空、`text` / `urlString` / `imageDataUTType` は null 可 |
| `readData` | （data が非 null のとき）`utType`(string), `base64`(string), `byteCount`(number) | なし |
| `getSnapshot` | `hasStrings` / `hasURLs` / `hasImages` / `hasColors`(bool), `numberOfItems`(number), `typeIdentifiers`(array), `allTypeIdentifiers`(array) | `matchingItemIndexes`（欠落・null は「未指定」を意味し null。空配列と区別する） |
| `createPasteboard` | `scope`(object), `scope.kind`(string) | `scope.name`（`general` のみ欠落可。`named` / `unique` では必須） |
| `detectPatterns` | `patterns`(array) | 未知 rawValue は要素単位でスキップ（失敗にしない） |
| `detectValues` | `detectedPatterns`(array) | 他すべて（3.6.3 の規則） |
| `loadItem` | `kind`(string) | `kind` ごとの値。`kind` に対応する値が欠落した場合は **B-6 の失敗**（例: `kind:"text"` で `text` 欠落）。`kind:"unknown"` は値不要 |
| `checkForegroundChange` | `changed`(bool) | なし |

- `detectValues` の要素型（`emailAddresses[].value` など 3.6.3 で「必須」としたもの）が欠落した場合、**その要素だけをスキップ**し、全体は成功とする。検出結果は本質的にベストエフォートであり、1 要素の欠落で全体を失敗にすると利用価値が落ちるため。スキップ件数のみログに出す（値は出さない）
- 上記の要素単位スキップは `detectValues` / `detectPatterns` に限る。他の関数は E-11 のとおり全体を失敗にする

#### 5.5.2 変更イベントの検証規則（封筒なし）

| 条件 | 扱い |
|---|---|
| `eventJson` が null / 空白 / parse 不能 / 非オブジェクト | **破棄**。`ClipboardChanged` を発火しない。`Debug.LogError` に「parse 失敗」の事実のみ記録（本文は出さない） |
| `kind` キー欠落 / 非文字列 | **破棄**（同上） |
| `kind` が未知の文字列 | `IosClipboardChangeEventKind.Unknown` として**発火する**（native が `"unknown"` を意図的に出すケースと、将来の kind 追加を同じ扱いにする） |
| `scope` 欠落 / 不正 | `Scope = null` として**発火する**。`kind` が本イベントの主情報であり、scope 欠落で通知を落とす方が害が大きい |
| `typesAdded` / `typesRemoved` 欠落 | 空リスト |

**「破棄」と「Unknown で発火」を明確に分ける。** 前者は Bridge 破損（parse できない）、後者は native が意図的に出した値。専用の parse-error event は設けない（購読者が対処できる情報がないため）。破棄はログのみで検出する。

#### 5.5.3 ログ規約

本文・base64 をログに出さない。出してよいのは `ok`、`errorCode`、件数、バイト数、`kind` のみ。クラスコメントに `AndroidClipboardJsonParser` と同じ趣旨の逸脱理由を英語で記載する。

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
    ClearAllPendingCallbacks();   // per-call スロット・s_onChanged・in-flight 集合をすべてクリア
    _instance = null;
}
```

- `Initialize()` は不要（native 側に setup 関数がない）
- `OnDestroy` で `null` コールバックを渡すのは native 契約上安全（1.2）

#### 5.6.2 delegate / callback スロット

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

// Per-call user callbacks. Guarded by the single-flight set below, so at most one
// pending call exists per operation and a result can never reach another call's callback.
private static Action<IosClipboardOperationResult>? s_onCopy;
// ... 各操作ぶん
private static Action<IosClipboardChangeEvent>?     s_onChanged;
```

#### 5.6.3 single-flight ガード（レビュー高優先度 2 の反映）

**問題:** native の C ABI に request ID / context がないため、同一関数の 2 つの並行呼び出しを native 側で区別できない。per-call callback を単一スロットで last-registered wins にすると、**A の結果が B の callback に渡り、B の結果には callback が残らない**。`Read` / `ReadData` / `LoadItem` では別リクエストのデータが B の結果として誤配送される。

**対策: 操作単位の single-flight。**

```csharp
private const string BusyErrorCode = "CLIPBOARD_BUSY";

// Operations currently awaiting a native callback. The native C ABI carries no request ID,
// so two concurrent calls to the same function are indistinguishable on the native side.
// Rejecting the second call is the only way to guarantee that a result reaches the caller
// that produced it.
private static readonly HashSet<string> s_inFlight = new();

/// <summary>Marks an operation as in flight. Returns false when one is already pending.</summary>
internal static bool TryBeginOperation(HashSet<string> inFlight, string operation);

/// <summary>Releases an operation's in-flight marker. Safe to call when not marked.</summary>
internal static void EndOperation(HashSet<string> inFlight, string operation);
```

呼び出し手順（全 15 操作で共通）:

1. `TryBeginOperation` が `false` → **pending スロットに一切触れず**、この呼び出しの `onResult` へ busy 失敗を発火して return
2. `true` → per-call スロットへ `onResult` を格納し、native を呼ぶ
3. 引数検証失敗・非 iOS・`DllImport` 例外 → `EndOperation` してから失敗を発火
4. native コールバック到着 → per-call スロットをスナップショットしてクリア → `EndOperation` → dispatch

`EndOperation` を dispatch より**前**に置く。これにより、購読者が callback 内から同じ操作を再呼び出ししても busy にならない。すでにスナップショット済みのため誤配送も起きない。

busy 結果:

| 操作系 | 結果 |
|---|---|
| Operation 系 | `IosClipboardOperationResult.Failure(op, "CLIPBOARD_BUSY", "{op} is already in progress.")` |
| Json 系 | 対応する result 型の `Failure("CLIPBOARD_BUSY", "{op} is already in progress.")` |

- busy 失敗も**共通 event を発火する**（他の失敗と同じ扱い。購読者から見て一貫させる）
- **busy は pending 側の状態を一切変更しない**。進行中の呼び出しの結果は必ず元の呼び出し元へ届く
- `CLIPBOARD_BUSY` は **C# 側でのみ使う新規コード**（native の `ClipboardError` には存在しない）

**トレードオフ（公開契約として XML コメントに明記する）:**

- 同一操作の並行実行はできない。異なる操作の並行実行は可能（例: `Read` 中に `GetSnapshot`）
- `LoadItem` は最大 15 秒かかりうるため、その間の 2 本目の `LoadItem` は busy になる。複数種別を同時に読みたい場合は逐次実行する
- `CancelLoads` は `LoadItem` と別操作なので、`LoadItem` 進行中でも呼べる（設計上必須）

**採用しなかった案:**

| 案 | 却下理由 |
|---|---|
| FIFO キューで callback を順に払い出す | native は**完了順を保証しない**（`loadItem` は非同期・タイムアウトあり）。逆順完了で誤配送が再発する |
| last-registered wins のまま契約に明記する | データの誤配送は「callback が呼ばれない」より深刻で、契約の明記では緩和されない |
| native に request ID を追加する | native 変更を伴い本計画のスコープ外。将来の改善案として記録する |

#### 5.6.4 `StartObserving` / `StopObserving`

```csharp
public void StartObserving(IosPasteboardScope? scope = null,
                           Action<IosClipboardChangeEvent>? onChanged = null,
                           Action<IosClipboardOperationResult>? onStarted = null)
```

- single-flight キーは `OperationStartObserving`
- `s_onChanged = onChanged` を設定してから native を呼ぶ
- native は 2 回目の `startObserving` で前回の監視を先に停止する（1.11）
- **開始に失敗した場合（Editor / 非 iOS / `DllImport` 例外 / native の `CLIPBOARD_UNAVAILABLE`）、この呼び出しが登録した callback であることを確認したうえで `s_onChanged` をクリアする**（レビュー低優先度の反映）

```csharp
// Release only our own registration: a later StartObserving may already have replaced it.
if (ReferenceEquals(s_onChanged, onChanged)) s_onChanged = null;
```

- `StopObserving` 成功時も同様に `s_onChanged = null`
- **共通 event `ClipboardChanged` は Manager 側から解除しない**（購読者が明示的に解除するまで残す）
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
    var evt = IosClipboardJsonParser.ParseChangeEvent(eventJson);
    if (evt == null) return;   // discarded; see 5.5.2
    FireClipboardChanged(evt);
}
#endif
```

- IL2CPP 制約: `[MonoPInvokeCallback]` を付ける実装は**必ず `static`**。インスタンスメンバへは `_instance?.` 経由でアクセスする
- コールバック内で例外を native へ抜けさせない。`FireXxx` 側で `try/catch` する
- コールバック引数の `string` はマーシャラがコピー済みのため、そのまま保持してよい

#### 5.6.6 dispatch 順序と例外分離

```csharp
private static void FireOperationResult(IosClipboardOperationResult result)
{
    var perCall = TakeOperationCallback(result.Operation);      // snapshot & clear
    EndOperation(s_inFlight, result.Operation);                 // release before dispatch
    var common = _instance?.ClipboardOperationCompleted;
    UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, common, perCall));
}

/// <summary>
/// Invokes the common event first, then the per-call callback. Each is wrapped in its own
/// try/catch so a throwing subscriber cannot suppress the other.
/// Extracted as a pure, Unity-lifecycle-independent helper for EditMode tests.
/// </summary>
internal static void InvokeInOrder<T>(T result, Action<T>? common, Action<T>? perCall);
```

**契約（全 15 操作で統一）:**

1. 共通 event を先に発火し、次に per-call callback を呼ぶ
2. per-call callback は発火直前にスナップショットして**スロットをクリア**する
3. **single-flight により、結果は必ずその呼び出しを行った側の callback へ渡る**（誤配送しない）
4. 共通 event は per-call callback / `Awaitable` の利用有無に関わらず**常に**発火する
5. どちらの例外も `Debug.LogError` で握り潰し、native へ抜けさせない
6. すべて `UnityMainThreadDispatcher` 経由

#### 5.6.7 ログ規約（clipboard 固有の逸脱）

`csharp.md` は「全メソッドの先頭で全パラメータをログ」と定めるが、**clipboard 本文はパスワード・トークンを含みうる**ため、`AndroidClipboardManager` と同じ扱いにする。

- 出してよい: `kind`、文字数、バイト数、件数、`hasScope` / `hasCallback` などの真偽、`utType`、`errorCode`、`Operation`
- 出してはならない: `text` / `plain` / `html` / `urlString` / `path` / `base64` / `representations` の値 / 検出された値（メール・電話・住所等）/ pasteboard 名
- 逸脱理由をクラスコメントに英語で明記する（native 側 `ClipboardRedaction` と同じ趣旨）

### 5.7 `Awaitable` 版の併設

`common.md`「非同期版の併設ルール」に従う。

- **前提条件は 5.6.3 の single-flight ガードで満たされた**（in-flight ガードが実装されている）。v1 では last-registered wins のため `Awaitable` を作れなかったが、v2 ではガードが入ったため作れる
- busy になった呼び出しは**即座に失敗結果で完了する**ため、`AwaitableCompletionSource` が未完了のまま破棄されることはない（ハングしない）

```csharp
public Awaitable<IosClipboardOperationResult> CopyAsync(
    IosClipboardContent content,
    IosPasteboardScope? scope = null,
    IosClipboardCopyOptions? options = null)
{
    Debug.Log($"[{LogTag}][{nameof(CopyAsync)}] kind: {content?.Kind}, hasScope: {scope != null}");
    var source = new AwaitableCompletionSource<IosClipboardOperationResult>();
    Copy(content, scope, options, result => source.TrySetResult(result));
    return source.Awaitable;
}
```

- ガードは**callback 版に実装**し、`Awaitable` 版は薄いラッパーのままにする
- 戻り値は結果型をそのまま返し、`IsSuccess == false` を例外に変換しない
- `UnityEngine.Awaitable` を使う（UniTask 等の外部依存を追加しない）
- `Awaitable<T>` は **await できるのは 1 回だけ**。戻り値をフィールドに保持しない旨を XML コメントに記載する
- `CancellationToken` は本計画では受けない（native 側に個別キャンセル API がなく、`CancelLoads` は全 load の一括キャンセルであるため。将来 native が per-request cancel を持てば追加する）

### 5.8 スレッド契約

- native コールバックは**メインスレッドで、1 呼び出しにつき 1 回**（native ヘッダの保証）
- それでも結果発火は `UnityMainThreadDispatcher.Instance.Enqueue` を通す（既存 Manager 全実装と統一）
- `UnityMainThreadDispatcher.Instance` は `Awake` でメインスレッド上に生成しておく
- 変更イベント（`ClipboardChanged`）も同じ経路を通る。**発火順序は native の到着順を保つ**（`Enqueue` は FIFO）
- `s_inFlight` はメインスレッドからのみ触る前提とする（呼び出しもコールバックもメインスレッド）。ロックは設けず、その前提をコメントに明記する

### 5.9 メモリ契約

#### 5.9.1 request 側

- `[UnmanagedFunctionPointer]` delegate は `static readonly` フィールドで保持し、GC による関数ポインタ回収を防ぐ
- 変更通知用 delegate（`s_changeDelegate`）は**監視中ずっと native から呼ばれる**ため、特に static 保持が必須
- `byte[]`（imageData / customData / multiRepresentation）は base64 文字列へ変換した時点で 2 倍以上のメモリを一時的に消費する。64MiB 上限（1.10）に対し、リクエスト JSON はおよそ 1.4 倍に膨らむ
- 大きな画像は `ImageFile(path)` の利用を XML コメントで推奨する
- `Marshal.AllocHGlobal` は使わない

#### 5.9.2 response 側（レビュー中優先度の反映）

**64MiB のバイナリを `ReadData` / `LoadItem(Image)` で受け取る場合の managed ピークメモリを設計上の制約として扱う。**

素直に実装した場合の同時保持:

| 段階 | 概算サイズ（64MiB のバイナリに対して） |
|---|---|
| native が組み立てた JSON（UTF-8、native ヒープ） | 約 85 MB |
| C# へマーシャルされた `string`（**UTF-16**） | 約 170 MB |
| JSON リーダーが切り出した base64 部分文字列 | 約 170 MB |
| デコード後の `byte[]` | 64 MB |
| **合計ピーク** | **約 490 MB** |

**削減策（実装必須）:**

1. **リーダーは base64 の部分文字列を実体化しない。** `TryGetBase64Bytes` が元 `string` のスパンから直接デコードする（5.3）。これで 3 段目の約 170 MB を削減 → ピーク約 320 MB
2. **デコード直後に JSON 文字列への参照を解放する。** parser は `byte[]` を取り出したら `JsonValue` ツリーと元 `string` の参照を保持しない（ローカル変数のスコープを閉じる）
3. **`byteCount` を先に読み、`Data` を確保する前にサイズを確認する。** 上限（64MiB）を超える値が来た場合は確保せず `CLIPBOARD_CONTENT_TOO_LARGE` 相当の失敗として扱う（native 側で弾かれるはずだが、防御的に持つ）
4. 実測より前に「安全な実効サイズ」を断定しない。**数 MiB 級を推奨サイズとし、大容量は `LoadItem(File)` / `ImageFile(path)`（パス受け渡し）へ誘導する**旨を XML コメントに明記する

**残る制約:** 2 段目（マーシャル後の UTF-16 string、約 170 MB）は `DllImport` の `string` マーシャリングを使う限り避けられない。回避には `IntPtr` を受けて `Encoding.UTF8.GetString` するか、native 側に長さ付きバイト列 API を追加する必要がある。**本計画では採用せず、実機実測（M-22 / M-23）の結果を見て判断する。**

### 5.10 IL2CPP 制約

- `[MonoPInvokeCallback(typeof(...))]` を付ける実装は `static` 必須
- delegate 型は `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` を明示（native は `extern "C"` / cdecl）
- `bool` 引数には `[MarshalAs(UnmanagedType.I1)]` を付与（3.2）
- `AndroidJavaProxy` は iOS では使わないため、Android 固有の proxy 制約は本計画に該当しない
- `using AOT;` は `#if UNITY_IOS && !UNITY_EDITOR` 内でのみ有効にする

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
| B-0 | **同一操作が実行中**（single-flight、5.6.3） | `CLIPBOARD_BUSY` | `{operation} is already in progress.` |
| B-1 | 非 iOS プラットフォーム（Editor 含む） | `CLIPBOARD_BRIDGE_UNAVAILABLE` | `{operation} is only available on an iOS device.` |
| B-2 | `DllImport` 呼び出しが例外を投げた | `CLIPBOARD_BRIDGE_UNAVAILABLE` | `{operation} could not be started.` |
| B-3 | 必須引数が `null`（`content` / `request` / `utType` / `patterns` / `scope`） | `CLIPBOARD_INVALID_REQUEST` | `{parameterName} must not be null.` |
| B-4 | `patterns` が空配列 | `CLIPBOARD_EMPTY_PATTERNS` | `No detection patterns were specified.`（native と同一文言。native 到達前に返す） |
| B-5 | native から返った JSON が null / 空白 | `CLIPBOARD_UNKNOWN` | `Clipboard bridge returned no data.` |
| B-6 | 封筒・data の構造不正（5.5.1 の E-2 / E-3 / E-8〜E-11） | `CLIPBOARD_UNKNOWN` | `Failed to parse the clipboard response.` |
| B-7 | `ok == false` だが `error` / `error.code` が欠落 | `CLIPBOARD_UNKNOWN` | `error.message` があればそれ、無ければ `An unknown error occurred.` |
| B-8 | base64 デコード失敗 | `CLIPBOARD_UNKNOWN` | `Failed to decode the clipboard data.` |

- B-0 / B-1 / B-2 の `CLIPBOARD_BUSY` / `CLIPBOARD_BRIDGE_UNAVAILABLE` は **C# 側でのみ使う新規コード**（native の `ClipboardError` には存在しない）。`CLIPBOARD_BRIDGE_UNAVAILABLE` は `AndroidClipboardManager` の同名定数と値を揃える
- B-4 は「native と同じ結論を、往復せずに返す」だけの前倒し。文言・コードを native と完全一致させる
- 変更イベントの parse 失敗は結果型を持たないため上表に含めない（5.5.2 の破棄規則）

### 6.4 エラーにならない（正常系として扱う）ケース

| ケース | 扱い |
|---|---|
| `clipboardReadData` の `data == null` / `data` キー欠落 | 成功。`HasData == false` |
| `clipboardRead` の `numberOfItems == 0` | 成功。空 `Items` |
| `clipboardLoadItem` の `kind == "unknown"` | 成功。`IosClipboardLoadedItemKind.Unknown` |
| `detectPatterns` / `detectValues` の未知 rawValue・要素欠落 | 該当要素をスキップして成功（5.5.1） |
| `CLIPBOARD_CANCELLED` | 失敗として返るが、native doc により**無視可能な正常終了**として XML コメントに明記する |
| `getSnapshot` の `matchingItemIndexes == null` | `matchingTypes` 未指定を意味する。空配列（該当なし）と区別する |
| 変更イベントの `kind == "unknown"` | イベントとして発火する（`Unknown`）。**parse 失敗とは区別する**（5.5.2） |

---

## 7. テスト方針

`agent-rules/coding-rules/testing.md` の層モデルに従う。

### 7.1 層 1: EditMode（`Tests/Runtime/`）

`IosClipboardManager` は **A 群**（`#if UNITY_IOS || UNITY_EDITOR`）のため、build target を切り替えずにテストできる。ただし **Manager インスタンスを生成するテストは書かない**（`Awake` が `DllImport` に触れうるため）。

| テストファイル | 検証内容 |
|---|---|
| `IosClipboardJsonReaderTests.cs` | オブジェクト / 配列 / **配列の配列** / エスケープ（`\uXXXX`・サロゲートペア）/ 数値（整数・小数・指数・負値）/ `null` / bool。壊れた JSON で例外を投げず `null` を返すこと。深さ上限で打ち切ること。**`TryGetBase64Bytes` が中間 string を作らずにデコードすること**（エスケープなし文字列の遅延実体化を含む） |
| `IosClipboardJsonBuilderTests.cs` | **requestJson を持つ 13 操作の出力**。`scope == null` でキーが出ないこと。`scope.general` に `name` が出ないこと。**`BuildAppendJson` に `options` が絶対出ないこと**。`options` の 4 パターン（`null` / `PrivacyPreservingDefault` / 明示 `localOnly:true` / 明示 `localOnly:false`）。`expirationDate` の UTC ISO8601 形式と `null` 時の省略。`matchingTypes` 空配列でキーが出ないこと。base64 変換。非有限 double で `ArgumentException`。空 scope 名で `ArgumentException`。制御文字・日本語・絵文字のエスケープ |
| `IosClipboardJsonParserTests.cs` | **5.5.1 の E-1〜E-13 を全件**。data 必須フィールド欠落が B-6 になること。任意フィールド欠落が 3.6.3 の既定値になること。`readData` の `data:null` / キー欠落が成功になること。`allTypeIdentifiers` のネスト配列。`matchingItemIndexes` の null と空配列の区別。`loadItem` の 5 kind と kind 別値欠落。`detectValues` の全フィールド・要素単位スキップ。**5.5.2 の変更イベント破棄規則（破棄 vs Unknown 発火の分岐）** |
| `IosClipboardResultTests.cs` | 3.6.3 の全 result 型: `IsSuccess == true` ⇔ `Error == null`、失敗時に payload が null / 空、`Failure` の code / message 正規化、collection が常に非 null |
| `IosClipboardManagerDispatchTests.cs` | `InvokeInOrder`: common → perCall の順序、common が例外でも perCall が呼ばれる、perCall が例外でも例外が外へ出ない、両方 null でも落ちない。**`TryBeginOperation` / `EndOperation`**: 2 本目が false になる、`EndOperation` 後に再取得できる、別操作は互いに干渉しない、**逆順完了・遅延完了でも A の結果が B の callback へ渡らないこと**（in-flight 集合を引数で受ける純粋関数として検証） |

### 7.2 層 2a: PlayMode（Editor 内、`Tests/PlayMode/`）

`IosShareManagerIntegrationTests` / `MacShareManagerIntegrationTests` と同じ構成。

| 検証内容 |
|---|
| Editor 実行時に全 15 操作が `CLIPBOARD_BRIDGE_UNAVAILABLE` で失敗結果を返すこと（B-1） |
| dispatcher 経由で共通 event → per-call callback の順序が保たれること |
| per-call callback を指定しなくても共通 event が発火すること |
| **同一操作が実行中に再呼び出しされた場合、2 本目が `CLIPBOARD_BUSY` で失敗し、1 本目の callback は失われないこと**（制御可能な callback seam を使う。5.6.3） |
| **busy 失敗も共通 event を発火すること** |
| `EndOperation` 後に同じ操作を再度呼べること（callback 内からの再呼び出しを含む） |
| `StartObserving` が `ClipboardOperationCompleted`（`Operation == "startObserving"`）を発火すること |
| **`StartObserving` が失敗したとき `s_onChanged` が解放されること**（5.6.4） |
| Editor では `ClipboardChanged` が発火しないこと |
| **`Awaitable` 版が必ず完了すること**（成功・失敗・busy のいずれでもハングしない） |
| `Instance` 生成 → `Destroy` → 再取得で例外が出ないこと |

**テスト用 seam:** native 呼び出しを直接テストできないため、`TryBeginOperation` / `EndOperation` / `InvokeInOrder` と、per-call スロットを操作する `internal` メソッドを介して並行シナリオを再現する。Editor の即時失敗を連続実行するだけでは競合を再現できない（レビュー指摘のとおり）。

### 7.3 層 2b / 層 3: 実機（本計画では自動化せず、手動確認で代替）

| # | 操作 | 期待 |
|---|---|---|
| M-1 | `Copy(PlainText)` → 他アプリ（メモ等）で貼り付け | 文字列が一致する。**日本語・絵文字・サロゲートペアが化けない**（9.1） |
| M-2 | `Copy(HtmlText)` → リッチテキスト対応アプリで貼り付け | 書式が保持される |
| M-3 | `Copy(Url)` / `Copy(ImageFile)` / `Copy(ImageData)` / `Copy(Color)` | それぞれ貼り付け先で期待どおり |
| M-4 | `Copy` に `LocalOnly = false` → 同一 Apple Account の別デバイスで貼り付け | Universal Clipboard に載る。`true`（既定）では載らない |
| M-5 | `Copy` に過去の `ExpirationDate` | `CLIPBOARD_INVALID_EXPIRATION` |
| M-6 | `Append` の後に `Read` | 項目が増えている |
| M-7 | `Read` / `GetSnapshot` を他アプリがコピーした内容に対して実行 | `GetSnapshot` は貼り付け許可プロンプトを出さない。`Read` は出しうる（9.3） |
| M-8 | `ReadData` に該当型なし | 成功 + `HasData == false`（失敗にならない） |
| M-9 | `CreatePasteboard(Named)` → `Copy(scope)` → `Read(scope)` → `RemovePasteboard(scope)` | 一連が成功する |
| M-10 | `RemovePasteboard(General)` | `CLIPBOARD_CANNOT_REMOVE_GENERAL` |
| M-11 | `CreatePasteboard(Unique)` の返り `scope` をそのまま `Copy` / `Read` に渡す | 往復して動作する（`kind:"unique"` + 生成名の round-trip） |
| M-12 | `DetectPatterns` / `DetectValues`（メール・電話・URL・金額を含むテキスト） | 検出される。`MoneyAmounts[].Amount` が数値として取れる。**ログに検出値が出ていないこと** |
| M-13 | `LoadItem(Image)` → `LoadItem(File)` | 画像は PNG、file は一時パスが返る |
| M-14 | `LoadItem` 実行中に `CancelLoads` | `CLIPBOARD_CANCELLED` が返る（`CancelLoads` 自体は busy にならない） |
| M-15 | `StartObserving` → 他アプリでコピー → 復帰 | `ClipboardChanged`（`changed` または `changedDetectedOnForeground`）が届く |
| M-16 | `StartObserving` を 2 回連続 | 重複してイベントが届かない（native の世代ゲート） |
| M-17 | `StopObserving` 後に他アプリでコピー | イベントが届かない |
| M-18 | `CheckForegroundChange` をバックグラウンド復帰後に実行 | `Changed == true` |
| M-19 | シーン遷移 / アプリ終了 | `OnDestroy` 後にクラッシュしない（永続 delegate の解除確認） |
| M-20 | 全操作のログ確認 | クリップボード本文・base64・検出値・pasteboard 名がログに一切出ていない |
| **M-21** | **`Copy` の成功系と失敗系（例: 過去 `ExpirationDate`）を実機 IL2CPP/ARM64 で実行** | **`IsSuccess` が両方とも正しく判定される**（`[MarshalAs(UnmanagedType.I1)]` の ABI 検証。3.2） |
| **M-22** | **数 MiB の `ImageData` を `Copy` → `ReadData` で読み戻す** | 往復が成功する。Xcode Instruments で managed / native ピークメモリを記録する（5.9.2） |
| **M-23** | **上限（64MiB）近傍の `LoadItem(Image)`** | 成功するか、`CLIPBOARD_CONTENT_TOO_LARGE` で失敗するかを記録。**OOM でクラッシュしないこと**。ピークメモリを記録し、推奨サイズの上限を確定する |
| **M-24** | **`LoadItem` 実行中に 2 本目の `LoadItem` を呼ぶ** | 2 本目が `CLIPBOARD_BUSY` で即座に失敗し、1 本目の結果が正しく届く（5.6.3 の実機確認） |
| **M-25** | **iOS build / link smoke test** | Unity import 後に iOS ビルドが通り、Xcode でリンクが成功する（0 節の残リスク） |

**実行前提:** IL2CPP / ARM64 / 実機 iOS 18 以降（`common.md` Minimum Versions）。Simulator は pasteboard 挙動が実機と異なるため確認対象に含めない。

### 7.4 テスト実行

- EditMode / PlayMode ともに Unity Test Runner で実行し、全 passed を確認する
- 既存テスト（`AndroidClipboard*Tests` 等）が壊れていないことも同時に確認する
- テストデータに実在の機微値を使わない。サンプル値のみ（`testing.md` 6 節）

---

## 8. Definition of Done

1. 4.0 の分類どおり、1.3.0 XCFramework が Unity に import され `.meta` が生成され、成果物コミットに含まれている
2. **M-25（iOS build / link smoke test）が通っている**（0 節の残リスクの解消確認）
3. 4.1 / 4.2 の新規ファイルがすべて作成されている
4. 4.3 のとおり既存 C# ファイルへの変更が 0 件である
5. `ClipboardOperationCallback` の `isSuccess` に `[MarshalAs(UnmanagedType.I1)]` が付与されている（3.2）
6. 全 15 操作に single-flight ガードが実装され、busy 時に `CLIPBOARD_BUSY` を返す（5.6.3）
7. 全 15 操作に callback 版と `Awaitable` 版の両方がある（3.3 / 3.4 / 5.7）
8. 15 操作すべてが「共通 event → per-call callback」の順で結果を返し、結果が他の呼び出しへ誤配送されない
9. 3.6 の公開型仕様どおりに型が実装されている（nullability・collection の非 null 契約を含む）
10. 5.5.1 の E-1〜E-13、5.5.2 の変更イベント破棄規則が実装されている
11. 6.3 の C# Bridge 層エラー B-0〜B-8 がすべて実装されている
12. 5.9.2 の response 側メモリ削減策 1〜4 が実装されている
13. 層 1 / 層 2a のテストが追加され、Unity Test Runner で全 passed
14. クリップボード本文・base64・検出値・pasteboard 名がログに出力されない
15. `public` メンバに英語の XML ドキュメントコメントがある
16. 7.3 の手動確認項目 M-1〜M-25 が実機で確認済み（未実施項目は理由とともに記録）

---

## 9. 要検証事項（断定しない）

v1 の 9.1（`bool` の幅）と 9.2（money amount の型）は**確定仕様へ移した**ため削除した。

### 9.1 `const char*` → `string` の文字コード

IL2CPP は既定で UTF-8 として解釈する想定だが、日本語・絵文字を含むクリップボード内容での往復は未検証。

- M-1 で日本語・絵文字・サロゲートペア（例: 🧑‍🚀）を含む文字列の往復を確認する
- 化ける場合は `[MarshalAs(UnmanagedType.LPUTF8Str)]` の付与を検討する

### 9.2 `IosPasteboardScope.General` と `null` の等価性

C# API では `scope: null` を「general」として説明するが、JSON 上は「キー省略」と `{"kind":"general"}` の 2 通りになる。native の `parseScope` はどちらも `.general` に解決するため機能差はない想定。

- M-9 のバリエーションとして両者の挙動差がないことを確認する
- 差があった場合は、C# 側で `null` を常に `{"kind":"general"}` へ正規化する方針へ変更する

### 9.3 `Read` の pasteboard privacy プロンプト

`clipboardRead` は `UIPasteboard` の値へアクセスするため、他アプリ由来データに対して**貼り付け許可 UI が出る可能性がある**（`testing.md` 2 節、iOS 16+）。

- プロンプトの有無・条件は未実測。M-7 で実機確認する
- 確認結果を `Read` / `ReadAsync` の XML コメントおよびマニュアルへ反映する（write-manual 工程）

### 9.4 大容量レスポンスの実効上限

5.9.2 の削減策を入れても、マーシャル後の UTF-16 string（64MiB のバイナリで約 170 MB）は避けられない。

- M-22 / M-23 の実測でピークメモリと実効上限を確定する
- 実機で許容できないと判明した場合、`IntPtr` + `Encoding.UTF8.GetString` 方式、または native への長さ付きバイト列 API 追加を検討する（いずれも本計画のスコープ外）

### 9.5 `matchingItemIndexes` の JSON 表現

`snapshot.matchingItemIndexes ?? NSNull()` のため、未指定時は `"matchingItemIndexes": null` になる想定。空配列（該当なし）との区別は JSON 上明確だが、実出力での確認は未実施。M-7 相当の実機確認時に併せて確認する。

### 9.6 single-flight の粒度が実運用で過剰でないか

操作単位の single-flight は安全だが、`LoadItem` を複数種別で同時に走らせる用途を塞ぐ。

- サンプルシーン設計（`design-sample-scene`）と実機確認（M-24）で、逐次実行が実用上の制約にならないか確認する
- 制約が大きいと判明した場合の改善方向は「native に request ID を追加する」であり、C# 側だけでは解決できない旨を記録する
