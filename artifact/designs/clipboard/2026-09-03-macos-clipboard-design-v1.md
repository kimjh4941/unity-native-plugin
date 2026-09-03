# macOS Clipboard 実装計画書 (v1)

- 対象機能: clipboard
- 対象プラットフォーム: macOS
- 対象パッケージ: `Packages/com.jonghyunkim.nativetoolkit`
- 作成日: 2026-09-03
- 出力範囲: Runtime（Bridge / Manager / Payload / Result / JSON）とテストのみ。サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は `design-sample-scene` スキルで別途設計する

---

## 0. 前提と現状

- native-toolkit の macOS Clipboard 実装は**完成済み**。Unity 側は C ABI を叩くだけでよく、pasteboard ロジックを C# で再実装しない
- 同梱 xcframework `Plugins/macOS/unity-mac-native-toolkit-1.3.0.xcframework` に **15 個の clipboard C 関数がすべてエクスポート済み**であることを `nm -gU` で確認した（3.1 に一覧）。ネイティブの再ビルド・差し替えは不要
- Unity 側 macOS Clipboard 実装は**存在しない**（`Runtime/Clipboard/` にあるのは Android / iOS のみ）
- 既存 macOS Manager（`MacShareManager` / `MacNotificationManager` / `MacDialogManager`）と既存 Clipboard Manager（`IosClipboardManager`）の両方のパターンを踏襲する

---

## 1. native-toolkit 確認結果（macOS）

参照パス: `/Users/jonghyunkim/Desktop/native-toolkit/mac/`

| ファイル | 役割 |
| --- | --- |
| `UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManagerBridge.h` / `.m` | C ABI（C# が P/Invoke する対象） |
| `UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift` | Swift ファサード（引数検証・main actor hop） |
| `UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardJsonParser.swift` | JSON スキーマの正本 |
| `MacLibrary/MacLibrary/Clipboard/Domain/Error/ClipboardError.swift` | ドメインエラー（1501-1599） |
| `MacLibrary/MacLibrary/Notification/Domain/Error/BridgeError.swift` | ブリッジ境界エラー（1301 / 1302） |
| `MacLibrary/MacLibrary/Clipboard/Domain/Model/*.swift` | scope / detection / limits の定義 |
| `MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift` | 監視間隔の制約 |

### 1.1 公開 C 関数（15 個）

| # | 関数 | 引数 | コールバック型 | 成功時 JSON |
| --- | --- | --- | --- | --- |
| OP-01 | `clipboardCopy` | `contentJson`, `optionsJson`, `scopeJson` | `ClipboardJsonCallback` | `OwnershipJson` |
| OP-02 | `clipboardAppend` | `contentJson`, `ownershipJson` | `ClipboardJsonCallback` | `OwnershipJson` |
| OP-03 | `clipboardRead` | `scopeJson` | `ClipboardJsonCallback` | `ReadResultJson` |
| OP-04 | `clipboardReadData` | `utType`, `scopeJson` | `ClipboardJsonCallback` | `ReadDataJson` |
| OP-05 | `clipboardSnapshot` | `matchingTypesJson`, `scopeJson` | `ClipboardJsonCallback` | `SnapshotJson` |
| OP-06 | `clipboardClear` | `scopeJson` | `ClipboardJsonCallback` | `ChangeCountJson` |
| OP-07 | `clipboardCreatePasteboard` | `requestJson` | `ClipboardJsonCallback` | `ScopeResultJson` |
| OP-08 | `clipboardRemovePasteboard` | `scopeJson` | `ClipboardCallback` | なし |
| OP-09 | `clipboardDetectPatterns` | `patternsJson`, `scopeJson` | `ClipboardJsonCallback` | `PatternsJson`（トップレベル配列） |
| OP-10 | `clipboardDetectValues` | `patternsJson`, `scopeJson` | `ClipboardJsonCallback` | `DetectedValuesJson` |
| OP-11 | `clipboardDetectMetadata` | `scopeJson` | `ClipboardJsonCallback` | `DetectedMetadataJson` |
| OP-12 | `clipboardAccessBehavior` | `scopeJson` | `ClipboardJsonCallback` | `AccessBehaviorJson` |
| OP-13 | `clipboardStartObserving` | `scopeJson`, `intervalSeconds`(double), `ClipboardCallback`, `ClipboardChangeCallback` | `ClipboardCallback` + イベント | なし（イベントは `ChangeEventJson`） |
| OP-14 | `clipboardStopObserving` | なし | `ClipboardCallback` | なし |
| OP-15 | `clipboardCheckForegroundChange` | `scopeJson` | `ClipboardJsonCallback` | `BoolJson` |

### 1.2 コールバック型

```c
typedef void (*ClipboardCallback)(BOOL isSuccess, NSInteger errorCode, const char* errorMessage);
typedef void (*ClipboardJsonCallback)(BOOL isSuccess, const char* json, NSInteger errorCode, const char* errorMessage);
typedef void (*ClipboardChangeCallback)(const char* eventJson);
```

- 操作コールバックは**必ず 1 回だけ**発火する（引数不正の早期失敗を含む）
- 変更コールバックは購読中に N 回発火し、終端イベントは無い
- **すべてのコールバックは main thread で呼ばれる**（Swift ファサードが `Task { @MainActor }` を通す）
- `errorCode == 0` が成功。`json` は `isSuccess == YES` のときのみ非 NULL、`errorMessage` は `isSuccess == NO` のときのみ非 NULL
- ポインタはコールバック内で即座に managed string へコピーする（Objective-C 側は `NSString.UTF8String` の一時バッファ）

#### NSInteger のマーシャリング（重要）

- **`errorCode` は `NSInteger` = 64bit**。既存の `MacNotificationManager` が参照している macOS Notification bridge は C の `int errorCode` を宣言しており、**Clipboard だけ型が異なる**
- C# の delegate では `long` で受け、managed 側で `int` に narrow する。`int` で宣言すると 64bit 引数を 32bit で読む ABI 不一致になる（現行の値域 0-1599 では実害が出ない可能性が高いが、正しさを優先する）
- `BOOL isSuccess` は既存 `MacShareManager` の `bool` 宣言と同じ扱いにする（arm64 では `_Bool`、x86_64 では `signed char`。既存出荷実装と同一のリスクプロファイル）

#### NULL コールバックの扱い（ネイティブ仕様）

- 操作コールバックの NULL は**エラーではない**。処理は実行され結果が返らないだけ
- 例外 1: `clipboardCreatePasteboard` は callback が NULL のとき**何も作らない**（unique pasteboard の名前を返せないと解放不能になるため）
- 例外 2: `clipboardStartObserving` は `onChange` が NULL のとき **1302 を返して購読を開始しない**
- C# 側は常に非 NULL の delegate を渡す設計とし、この分岐に依存しない

### 1.3 JSON スキーマ（`UnityMacClipboardJsonParser.swift` が正本）

入力専用（4）:

| 型 | 形 |
| --- | --- |
| `ContentJson` | `{"items":[{"representations":{"<utType>":"<base64>"}}]}` |
| `OptionsJson` | `{"localOnly": true\|false}` |
| `CreateRequestJson` | `{"kind":"named"\|"unique","name":"<name>"?}` |
| `MatchingTypesJson` | `["<utType>", ...]`（トップレベル配列） |

入出力共用（3）:

| 型 | 形 |
| --- | --- |
| `ScopeJson` | `{"kind":"general"\|"named"\|"unique","name":"<name>"?}` |
| `OwnershipJson` | `{"scope":ScopeJson,"changeCount":Int}` |
| `PatternsJson` | `["links","number", ...]`（トップレベル配列） |

出力専用（9）:

| 型 | 形 |
| --- | --- |
| `ReadResultJson` | `{"changeCount":Int,"items":[{"representations":{...}}]}` |
| `ReadDataJson` | `{"data":"<base64>"\|null}`（キーは常に出力、null は「型が無い」で成功） |
| `SnapshotJson` | `{"changeCount":Int,"itemTypes":[[String]],"matchingItemIndexes":[Int]}` |
| `ChangeCountJson` | `{"changeCount":Int}` |
| `BoolJson` | `{"value":Bool}` |
| `DetectedValuesJson` | `{"patterns":[String],"probableWebURL":String?,"probableWebSearch":String?,"number":Double?,"links":[...],"phoneNumbers":[...],"emailAddresses":[...],"postalAddresses":[...],"calendarEvents":[...],"shipmentTrackingNumbers":[...],"flightNumbers":[...],"moneyAmounts":[...]}` |
| `DetectedMetadataJson` | `{"metadataTypes":[String],"contentTypeIdentifier":String?}` |
| `AccessBehaviorJson` | `{"value":"default"\|"ask"\|"alwaysAllow"\|"alwaysDeny"\|"unavailable"}` |
| `ScopeResultJson` | `{"scope":ScopeJson}` |

イベント（1）:

| 型 | 形 |
| --- | --- |
| `ChangeEventJson` | `{"scope":ScopeJson,"changeCount":Int}` |

スキーマ運用ルール（ネイティブ側の明文化事項）:

- デコード時に未知キーは無視される
- エンコード時に未知キーは出力されない
- `DetectedValuesJson` / `DetectedMetadataJson` / `ReadDataJson` の optional は**キー省略ではなく明示的な `null`** で出力される（「未要求」と「要求したが未検出」を C# 側で区別できるようにするため）
- `patterns` / `metadataTypes` はソート済みで出力される（同一内容なら同一文字列）
- 日付は `Date.ISO8601FormatStyle`（UTC、ロケール非依存）の文字列

### 1.4 引数の必須・省略ルール（iOS と異なるので注意）

| 引数 | NULL / 空の扱い |
| --- | --- |
| `scopeJson` | **必須**。NULL は 1302。iOS のように「省略 = general」にはならない |
| `contentJson` | 必須。NULL は 1302、不正 JSON は 1301 |
| `ownershipJson` | 必須（append） |
| `requestJson` | 必須（createPasteboard） |
| `utType` | 必須・非空（readData） |
| `optionsJson` | **省略可**。NULL / 空文字は `localOnly: true` の既定。ただし供給されて不正なら 1301 |
| `matchingTypesJson` | **省略可**。NULL / 空文字はフィルタ無し。空配列 `[]` は 1512 |
| `patternsJson` | 必須。空配列 `[]` はパース成功後に 1503 |

- `scope.kind == "general"` のとき `name` は**無視される**（拒否されない）。読み戻した scope をそのまま往復させられる
- `kind == "named"` / `"unique"` は `name` が非空でなければパース失敗

### 1.5 エラー仕様

C ABI の返却契約は `(isSuccess, errorCode, errorMessage)`。`errorCode == 0` が成功。

ブリッジ境界（`BridgeError`）:

| code | 条件 | errorMessage |
| --- | --- | --- |
| 1301 | 引数が供給されたが JSON として解釈できない | `Failed to parse JSON: Invalid clipboard JSON argument.` |
| 1302 | 必須引数が NULL / 空 | `Bridge contract violation: A required argument was missing.` |
| 1302 | `startObserving` の `onChange` が NULL | `Bridge contract violation: onChange is required; observation would produce no observable result.` |

ドメイン（`ClipboardError`、1501-1599）:

| code | ケース | errorMessage |
| --- | --- | --- |
| 1501 | copy / append に item が無い | `No clipboard content was provided.` |
| 1502 | item に representation が無い | `Clipboard item at index {i} has no representations.` |
| 1503 | 検出パターンが空 | `No detection patterns were specified.` |
| 1504 | UTI が不正 | `Invalid uniform type identifier: {value}.` |
| 1505 | pasteboard 名が不正 | `Invalid pasteboard name: {value}.` |
| 1506 | サイズ超過 | `Clipboard content is too large: {bytes} bytes (limit {limit}).` |
| 1507 | pasteboard を読めない | `Pasteboard is unavailable: {name}.` |
| 1508 | 標準 pasteboard の解放要求 | `Standard pasteboard cannot be released: {name}.` |
| 1509 | copy の書き込み拒否 | `The pasteboard rejected the write operation.` |
| 1510 | append の書き込み拒否 | `The pasteboard rejected the append operation.` |
| 1511 | 所有権喪失（append 不可） | `Pasteboard ownership was lost (expected change count {e}, found {a}). ...` |
| 1512 | 型フィルタが空配列 | `The type filter must not be empty. Pass nil to disable filtering.` |
| 1513 | 検出 API が OS 未対応 | `Pasteboard detection requires macOS {minimumOS} or later.` |
| 1514 | 検出をユーザーが拒否 | `The user denied access to the pasteboard contents.` |
| 1515 | 検出失敗（metadata 含む） | `Pasteboard detection failed: {reason}.` |
| 1521 | 貼り付け item のロード失敗 | `Failed to load pasted item: {reason}.` |
| 1522 | ロードのタイムアウト | `Loading pasted items timed out after {seconds} seconds.` |
| 1523 | 設定値が不正（監視間隔など） | `Invalid configuration: {reason}.` |
| 1524 | 検出のキャンセル | `The clipboard operation was cancelled.` |
| 1599 | その他（エンコード失敗を含む） | `An unknown clipboard error occurred: {reason}.` |

- 1521 / 1522 / 1524 は macOS 版の C ABI が公開していない paste ボタン経路のもの。C# からは通常到達しないが、値域として定義する
- 1599 は Swift ファサードが「結果を JSON エンコードできなかった」場合にも使う（`The result could not be encoded.`）

### 1.6 ネイティブ側の制約・注意（設計に影響するもの）

- **読み出しは書き込みの鏡ではない**。pasteboard が型を派生させるため、RTF で書いても plain text として読める。`Read` の結果が `Copy` の入力と一致する前提を置かない
- **append は所有権が必要**。iOS と異なり、他アプリに pasteboard を取られると 1511 で失敗し、黙って無視されない。`Append` は直前の `Copy` / `Append` が返した ownership を引き回す
- **named / unique pasteboard はプロセス終了後も残る**（pasteboard server 上）。unique は `RemovePasteboard` で明示解放する。機密データを named に置かない
- **general / 標準 pasteboard は解放できない**（1508）
- **どの読み出しも「ユーザーに通知されない保証は無い」**。`Snapshot` / `DetectPatterns` は payload を読まないが、これは最適化でありプライバシー契約ではない
- **`detectMetadata` は plain text で失敗する**（1515）。「報告するものが無い」と「報告できなかった」を区別できない
- **`accessBehavior` は macOS 15.4 未満で `"unavailable"` を返す**（失敗しない）。`detectPatterns` / `detectValues` は macOS 15.4 未満で 1513 を返す
- **監視間隔は `0 < interval <= 60` 秒**。範囲外は 1523。既定は 0.5 秒（`MacClipboardManager.defaultObservationInterval`）
- **監視はアプリ非アクティブ中に停止し、アクティブ復帰時に追いつく**。他アプリの変更は前面復帰時に報告される
- **`startObserving` の再呼び出しは新しい設定で再開する**（重複購読にならない）。`stopObserving` は冪等
- **`checkForegroundChange` は scope ごとの初回呼び出しで必ず true を返す**
- **サイズ制限**: representation あたり 100 MiB、合計 200 MiB で 1506。10 MiB でログ警告のみ（`ClipboardLimits.default`。ネイティブ側で「実機未計測の暫定値」と明記されている）
- **`localOnly` は未検証**。Universal Clipboard への効果は実機未確認とネイティブ側が明記している
- `detectPatterns` / `detectValues` は `patternsJson` と `scopeJson` を 1 つの `guard` で束ねており、**patterns が不正でも `argumentError(scopeJson)` で分類される**。scopeJson が正常なら 1301、両方 NULL なら 1302 になる。エラーコードから引数を特定できないため、C# 側で patterns の事前検証を行う（5.6 参照）

---

## 2. 既存 C# 実装の確認結果

参照: `Packages/com.jonghyunkim.nativetoolkit/Runtime/`

### 2.1 Common

| 型 | 内容 |
| --- | --- |
| `Common/UnityMainThreadDispatcher` | `Instance` シングルトン + `Enqueue(Action)`。Manager の `Awake` でメインスレッド上に生成しておく |
| `Common/IconConfiguration` | 今回は不使用 |

### 2.2 macOS Manager のパターン（`Share/MacShareManager.cs`）

- クラスガード: `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`（testing.md の A 群）
- P/Invoke とコールバック: `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR`
- `[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]`（`MacNotificationManager` も同じ）
- `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` delegate + `[MonoPInvokeCallback]` static メソッド
- delegate は `static readonly` フィールドで永続保持し GC 回収を防ぐ
- 共通 `event Action<TResult>?` + per-call `Action<TResult>?`（後勝ち）
- `InvokeInOrder(result, common, perCall)` を `internal static` に切り出して EditMode で dispatch 順序を検証
- 非対応プラットフォームは `Application.platform != RuntimePlatform.OSXPlayer` で早期失敗

### 2.3 Clipboard Manager のパターン（`Clipboard/IosClipboardManager.cs`）

macOS 版が踏襲すべき、Share には無い仕組み:

- **単一実行ガード（single-flight）**: C ABI にリクエスト ID が無いため、同一操作の同時実行は結果を区別できない。`HashSet<string> s_inFlight` で操作ごとに 1 件だけ許可し、2 件目は即失敗
- **ガードチェーン `TryStartOperation`**: メインスレッド → 破棄済み → 引数 → プラットフォーム → 単一実行 の順で検査
- **破棄後の tombstone（`IsTerminated`）**: `OnDestroy` 後は全操作を拒否し、遅延コールバックを破棄する
- **`RunDestroyCleanup(stop, cancel, managedCleanup)`**: teardown の例外境界を `internal static` の純粋関数に切り出して EditMode で検証
- **監視の世代管理（generation）**: 古い `stopObserving` の完了が新しい `startObserving` の登録を消さないようにする
- **`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` による static リセット**: Domain Reload 無効時に tombstone が残る問題への対処
- **`BridgeAvailableOverrideForTests`**（`#if UNITY_EDITOR`）: 実際の pending スロット・in-flight 集合を EditMode から駆動するためのシーム
- 機微情報のため、値ではなく shape / count / flag のみをログに出す（csharp.md の「全パラメータをログ」から意図的に逸脱）

### 2.4 再利用できる既存資産

| 型 | 現状 | 判断 |
| --- | --- | --- |
| `Clipboard/IosClipboardJsonReader.cs`（`JsonValue` / `JsonValueKind` / `JsonBase64Status` を含む） | `#if UNITY_IOS \|\| UNITY_EDITOR` | **共有化する**。汎用の手書き JSON リーダーで、macOS 版パーサに必須。`ClipboardJsonReader` へ改名しガードを広げる（4.2 参照） |
| `Clipboard/ClipboardOperationResult.cs` | Android 専用（`Operation` / `IsSuccess` / `ErrorMessage`、errorCode なし） | **流用しない**。macOS は数値 errorCode を返すため別型 |
| `Clipboard/ClipboardReadResult.cs`（`ClipItem` / `ClipContents`） | Android の ClipData モデル | **流用しない**。macOS は UTI → bytes の representation モデル |
| `Clipboard/Ios*` 各型 | iOS 専用ガード、文字列 errorCode | **流用しない**。命名・エラー表現ともに不一致 |
| `Common/UnityMainThreadDispatcher` | 共通 | **そのまま使う** |

- 既存の `Ios*` / `Android*` 型に macOS 用の分岐を足さない。プラットフォームごとに独立した型を持つのが本パッケージの既存方針（`MacShareResult` / `IosShareResult` が別型なのと同じ）

---

## 3. 実装対象 API 一覧

### 3.1 P/Invoke 宣言（`#if UNITY_STANDALONE_OSX && !UNITY_EDITOR`）

```csharp
[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardCopy(string contentJson, string? optionsJson, string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardAppend(string contentJson, string ownershipJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardRead(string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardReadData(string utType, string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardSnapshot(string? matchingTypesJson, string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardClear(string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardCreatePasteboard(string requestJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardRemovePasteboard(string scopeJson, ClipboardCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardDetectPatterns(string patternsJson, string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardDetectValues(string patternsJson, string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardDetectMetadata(string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardAccessBehavior(string scopeJson, ClipboardJsonCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardStartObserving(string scopeJson, double intervalSeconds, ClipboardCallback callback, ClipboardChangeCallback onChange);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardStopObserving(ClipboardCallback? callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void clipboardCheckForegroundChange(string scopeJson, ClipboardJsonCallback callback);
```

- 関数名は macOS bridge のシンボルに合わせて camelCase のまま宣言する（`MacNotificationManager` の既存方針と同じ）
- `clipboardStopObserving` のみ teardown 時に `null` を渡すため nullable

### 3.2 delegate 宣言

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardCallback(bool isSuccess, long errorCode, string? errorMessage);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardJsonCallback(bool isSuccess, string? json, long errorCode, string? errorMessage);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardChangeCallback(string? eventJson);
```

- `errorCode` は `NSInteger`（64bit）なので `long`。managed 側で `checked` せず `(int)` へ narrow し、値域外は 1599 相当として扱う
- 永続 delegate は `static readonly` で 15 個 + 変更通知 1 個を保持する

---

## 4. 変更ファイル一覧

`.meta` は Unity が自動生成するため記載しない。

### 4.1 新規作成（Runtime）

すべて `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/` 配下。
ガードは特記なき限り `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`。

| ファイル | 内容 |
| --- | --- |
| `MacClipboardPayloads.cs` | `MacPasteboardScopeKind` / `MacPasteboardScope` / `MacPasteboardCreationRequestKind` / `MacPasteboardCreationRequest` / `MacPasteboardOwnership` / `MacClipboardItem`（入力用） / `MacClipboardContent` / `MacClipboardCopyOptions` / `MacClipboardDetectionPattern` / `MacClipboardMetadataType` / `MacClipboardAccessBehavior` / `MacClipboardTypes`（代表 UTI 定数） |
| `MacClipboardErrorInfo.cs` | `MacClipboardErrorInfo`（`int Code` / `string Message`）と `MacClipboardErrorCodes`（ネイティブ 1301-1599 と C# ブリッジ 9001-9005 の定数） |
| `MacClipboardJsonBuilder.cs` | 7 種の入力 JSON を組み立てる `public static` メソッド群 |
| `MacClipboardJsonParser.cs` | 10 種の出力 JSON（9 + イベント）を結果型へ変換する `internal static` メソッド群 |
| `MacClipboardOperationResult.cs` | 値を返さない操作（removePasteboard / startObserving / stopObserving）の結果 |
| `MacClipboardOwnershipResult.cs` | `MacPasteboardOwnership` を返す結果（copy / append） |
| `MacClipboardReadResult.cs` | `MacClipboardReadContents`（`ChangeCount` + `IReadOnlyList<MacClipboardReadItem>`）と結果型 |
| `MacClipboardReadDataResult.cs` | `byte[]? Data`（型不在は成功 + null）と結果型 |
| `MacClipboardSnapshotResult.cs` | `MacClipboardSnapshot`（`ChangeCount` / `ItemTypes` / `MatchingItemIndexes`）と結果型 |
| `MacClipboardChangeCountResult.cs` | `clear` の結果（`ChangeCount`） |
| `MacPasteboardScopeResult.cs` | `createPasteboard` の結果 |
| `MacClipboardDetectionResults.cs` | `MacClipboardLabeledValue` / `MacClipboardPostalAddress` / `MacClipboardCalendarEvent` / `MacClipboardShipmentTracking` / `MacClipboardFlightNumber` / `MacClipboardMoneyAmount` / `MacClipboardDetectedLink` / `MacClipboardDetectedValues` と、patterns / values / metadata の 3 結果型 |
| `MacClipboardAccessBehaviorResult.cs` | `accessBehavior` の結果 |
| `MacClipboardForegroundChangeResult.cs` | `checkForegroundChange` の結果（`bool Changed`） |
| `MacClipboardChangeEvent.cs` | 監視イベント（`Scope` / `ChangeCount`） |
| `MacClipboardManager.cs` | Manager 本体（Bridge + ガードチェーン + dispatch） |

### 4.2 既存変更（Runtime）

| ファイル | 変更内容 |
| --- | --- |
| `Clipboard/IosClipboardJsonReader.cs` → `Clipboard/ClipboardJsonReader.cs` | ファイル名と static クラス名を `ClipboardJsonReader` に改名し、ガードを `#if UNITY_IOS \|\| UNITY_STANDALONE_OSX \|\| UNITY_EDITOR` へ拡張する。`JsonValue` / `JsonValueKind` / `JsonBase64Status` は既に接頭辞なしの共有型なので変更しない |
| `Clipboard/IosClipboardJsonParser.cs` | `IosClipboardJsonReader.` 参照 2 箇所を `ClipboardJsonReader.` に置換（振る舞いの変更なし） |

- 改名の理由: macOS 版パーサが一級の利用者になるため、`Ios` 接頭辞が実体と食い違う。本パッケージは共有型を接頭辞なしで置く方針（`ClipboardOperationResult` / `ClipboardReadResult`）
- 影響は機械的な識別子置換のみ（Runtime 2 箇所、Tests 42 箇所）。旧 `.meta` は Unity が再生成する
- **代替案**: 改名せずガードだけ広げる。iOS 側テストへの変更がゼロになるが、`Ios*` 型に macOS が依存する構造が残る。レビュー時に判断してよい

### 4.3 新規作成（Tests）

すべて `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/`（EditMode）。

| ファイル | 内容 |
| --- | --- |
| `MacClipboardJsonBuilderTests.cs` | 7 種の入力 JSON の形・キー省略・base64・エスケープ・数値のカルチャ非依存性 |
| `MacClipboardJsonParserTests.cs` | 10 種の出力 JSON の解析、明示的 null、トップレベル配列、不正 JSON の失敗 |
| `MacClipboardResultTests.cs` | 結果型の不変条件（`IsSuccess == true` ⇔ `Error == null`、正規化） |
| `MacClipboardManagerDispatchTests.cs` | `InvokeInOrder` の順序と例外分離、`TryBeginOperation` / `EndOperation`、`RunDestroyCleanup` の例外境界、ガードチェーンの拒否経路 |

`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/`:

| ファイル | 内容 |
| --- | --- |
| `MacClipboardManagerIntegrationTests.cs` | dispatcher 経由の共通 event → per-call callback 順序、非 macOS Player での失敗経路、`StartObserving` が操作 event を発火する契約 |

### 4.4 既存変更（Tests）

| ファイル | 変更内容 |
| --- | --- |
| `Tests/Runtime/IosClipboardJsonReaderTests.cs` | `IosClipboardJsonReader.` → `ClipboardJsonReader.` の識別子置換 40 箇所。ファイル名は iOS 由来の網羅ケースを保つため据え置いてよい（改名する場合は `ClipboardJsonReaderTests.cs`） |
| `Tests/Runtime/IosClipboardJsonBuilderTests.cs` | 同置換 2 箇所 |

### 4.5 非変更（参照のみ）

| ファイル | 理由 |
| --- | --- |
| `Runtime/Common/UnityMainThreadDispatcher.cs` | そのまま使う |
| `Runtime/Clipboard/Android*` / `Ios*`（Reader / Parser を除く） | プラットフォーム独立の型を維持する |
| `Runtime/Share/Mac*` / `Runtime/Notification/Mac*` / `Runtime/Dialog/MacDialogManager.cs` | パターン参照のみ |
| `Plugins/macOS/unity-mac-native-toolkit-1.3.0.xcframework` | 15 関数が既にエクスポート済み。差し替え不要 |
| `Runtime/NativeToolkit.Runtime.asmdef` | 参照追加は不要 |
| `package.json` | バージョン更新はリリース工程（`release` スキル）で行う |

### 4.6 対象外

- `Runtime/UI/macOS/Clipboard/`（ExampleController）、UXML / USS、サンプルシーン、`NativeToolkitSampleNavigator` への導線追加、`*SampleSceneWiringTests`
- マニュアル（`write-manual` スキル）

---

## 5. 実装詳細

### 5.1 `MacClipboardPayloads.cs`

```csharp
public enum MacPasteboardScopeKind { General, Named, Unique }

public sealed class MacPasteboardScope
{
    public MacPasteboardScopeKind Kind { get; }
    public string? Name { get; }                 // General は null
    public static MacPasteboardScope General { get; }
    public static MacPasteboardScope Named(string name);   // 空白名は ArgumentException
    public static MacPasteboardScope Unique(string name);
}

public enum MacPasteboardCreationRequestKind { Named, Unique }

public sealed class MacPasteboardCreationRequest
{
    public MacPasteboardCreationRequestKind Kind { get; }
    public string? Name { get; }
    public static MacPasteboardCreationRequest Unique { get; }
    public static MacPasteboardCreationRequest Named(string name);
}

public sealed class MacPasteboardOwnership
{
    public MacPasteboardScope Scope { get; }
    public int ChangeCount { get; }
}

public sealed class MacClipboardItem
{
    public IReadOnlyDictionary<string, byte[]> Representations { get; }
    public static MacClipboardItem FromRepresentations(IReadOnlyDictionary<string, byte[]> representations);
    public static MacClipboardItem PlainText(string text);        // public.utf8-plain-text
    public static MacClipboardItem Html(string html, string? plainFallback = null);
    public static MacClipboardItem Url(string url);               // public.url
    public static MacClipboardItem Data(string utType, byte[] bytes);
}

public sealed class MacClipboardContent
{
    public IReadOnlyList<MacClipboardItem> Items { get; }
    public static MacClipboardContent Single(MacClipboardItem item);
    public static MacClipboardContent Multiple(IReadOnlyList<MacClipboardItem> items);
    public static MacClipboardContent PlainText(string text);     // 最頻ケースの短縮形
}

public sealed class MacClipboardCopyOptions
{
    public bool LocalOnly { get; }
    public static MacClipboardCopyOptions Default { get; }        // localOnly: true
    public static MacClipboardCopyOptions Create(bool localOnly);
}

public enum MacClipboardDetectionPattern
{
    ProbableWebUrl, ProbableWebSearch, Number, Links, PhoneNumbers,
    EmailAddresses, PostalAddresses, CalendarEvents,
    ShipmentTrackingNumbers, FlightNumbers, MoneyAmounts
}

public enum MacClipboardMetadataType { ContentType }

public enum MacClipboardAccessBehavior { Default, Ask, AlwaysAllow, AlwaysDeny, Unavailable, Unknown }

public static class MacClipboardTypes   // 代表 UTI 定数
{
    public const string PlainText = "public.utf8-plain-text";
    public const string Html = "public.html";
    public const string Rtf = "public.rtf";
    public const string Url = "public.url";
    public const string FileUrl = "public.file-url";
    public const string Png = "public.png";
    public const string Tiff = "public.tiff";
}
```

- **enum の rawValue 変換は Builder / Parser 側に集約**する。`MacClipboardDetectionPattern.ProbableWebUrl` ⇔ `"probableWebURL"` の対応表を 1 箇所だけに置く
- `MacClipboardAccessBehavior.Unknown` は将来ネイティブが値を追加した場合の受け皿。`Unavailable`（macOS 15.4 未満）とは別物として区別する
- ネイティブが検証する値（サイズ超過、不正 UTI、空 item）は C# で再検証しない。単一のエラー契約を保つため。C# が投げるのは呼び出し側のバグ（null 引数、空白の pasteboard 名）だけ

### 5.2 `MacClipboardErrorInfo.cs`

```csharp
public readonly struct MacClipboardErrorInfo
{
    public const int UnknownErrorCode = 1599;
    public const string UnknownErrorMessage = "An unknown clipboard error occurred.";

    public int Code { get; }         // ネイティブ 1301/1302/1501-1599、または C# 9001-9005
    public string Message { get; }   // 空にならない（正規化済み）
    public bool IsBridgeCode => Code >= 9000;

    public static MacClipboardErrorInfo Create(long code, string? message);
}

public static class MacClipboardErrorCodes
{
    // ネイティブ（BridgeError）
    public const int ParseFailed = 1301;
    public const int ContractViolation = 1302;

    // ネイティブ（ClipboardError）1501-1599 — 1.5 の表と 1:1
    public const int EmptyContent = 1501;
    // ... 1502-1524, 1599

    // C# Bridge 層のみが返すコード
    public const int Busy = 9001;
    public const int BridgeUnavailable = 9002;
    public const int MainThreadRequired = 9003;
    public const int ManagerDestroyed = 9004;
    public const int InvalidRequest = 9005;
    public const int ResponseParseFailed = 9006;
}
```

- 9000 番台を選ぶ理由: ネイティブは現在 1001-1999 のみを割り当てている（notification 1001-1205 / 1999、bridge 1301-1302、share 1401-1499、clipboard 1501-1599）。9000 番台はどのファミリからも離れており衝突しにくい
- **要検証**: ネイティブ側が将来 9000 番台を使わないこと。native-toolkit 側のエラーコード表に「9000+ は Unity ブリッジ予約」と記載してもらうのが望ましい
- `Create(long code, string? message)` が `long` を受けて `int` へ narrow する唯一の場所にする

### 5.3 `MacClipboardJsonBuilder.cs`

`IosClipboardJsonBuilder` と同じ手書きシリアライザ方式（optional キーの制御が必要なため）。

| メソッド | 戻り値 |
| --- | --- |
| `BuildScopeJson(MacPasteboardScope scope)` | `{"kind":...,"name":...}`。General は `name` キーを出さない |
| `BuildContentJson(MacClipboardContent content)` | `{"items":[{"representations":{...}}]}`。bytes は base64 |
| `BuildOptionsJson(MacClipboardCopyOptions? options)` | `null` のとき **C# の `null` を返す**（空文字ではなく）。ネイティブは NULL / 空文字とも既定にフォールバックするが、意図を明示する |
| `BuildOwnershipJson(MacPasteboardOwnership ownership)` | `{"scope":{...},"changeCount":n}` |
| `BuildCreateRequestJson(MacPasteboardCreationRequest request)` | `{"kind":"named","name":"..."}` / `{"kind":"unique"}` |
| `BuildMatchingTypesJson(IReadOnlyList<string>? types)` | `null` のとき `null`（フィルタ無し）。空リストはそのまま `[]` を出し、ネイティブの 1512 に委ねる |
| `BuildPatternsJson(IReadOnlyCollection<MacClipboardDetectionPattern> patterns)` | `["links",...]`。rawValue へ変換し、決定的な順序にソートする |

- 数値は `CultureInfo.InvariantCulture` で書式化する
- 文字列は制御文字・`"`・`\` を JSON エスケープする
- **ログを出さない**。clipboard 本文はパスワード・トークンを含みうる（csharp.md の「全メソッドにログ」から意図的に逸脱。`IosClipboardJsonBuilder` と同じ扱い）
- base64 化はメモリを 4/3 に膨らませる。100 MiB の representation は約 133 MB の managed string になる。Manager 側で事前に合計サイズを見積もり、閾値超過は `InvalidRequest`(9005) で即時失敗させる（既定閾値は 5.6 に記載）

### 5.4 `MacClipboardJsonParser.cs`

`ClipboardJsonReader.Parse(json)` で `JsonValue` を得てから結果型へ変換する。

| メソッド | 入力 | 出力 |
| --- | --- | --- |
| `TryParseOwnership` | `OwnershipJson` | `MacPasteboardOwnership` |
| `TryParseReadResult` | `ReadResultJson` | `MacClipboardReadContents` |
| `TryParseReadData` | `ReadDataJson` | `byte[]?`（キーが `null` なら成功 + null） |
| `TryParseSnapshot` | `SnapshotJson` | `MacClipboardSnapshot` |
| `TryParseChangeCount` | `ChangeCountJson` | `int` |
| `TryParseBool` | `BoolJson` | `bool` |
| `TryParseScopeResult` | `ScopeResultJson` | `MacPasteboardScope` |
| `TryParsePatterns` | `PatternsJson`（**トップレベル配列**） | `IReadOnlyList<MacClipboardDetectionPattern>` |
| `TryParseDetectedValues` | `DetectedValuesJson` | `MacClipboardDetectedValues` |
| `TryParseDetectedMetadata` | `DetectedMetadataJson` | `MacClipboardDetectedMetadata` |
| `TryParseAccessBehavior` | `AccessBehaviorJson` | `MacClipboardAccessBehavior` |
| `TryParseChangeEvent` | `ChangeEventJson` | `MacClipboardChangeEvent` |

パース規約:

- `null` と「キー欠落」を区別する。ネイティブは optional を明示的な `null` で出すため、`null` は「要求したが未検出」、欠落はスキーマ不一致として扱う
- 未知の enum 文字列は**捨てずに保持しない**方針とする: `DetectionPattern` の未知値は無視（前方互換）、`AccessBehavior` の未知値は `Unknown` にマップする
- base64 のデコードは `JsonValue.TryGetBase64Bytes(maxDecodedLength, out bytes)` を使い、上限超過は `JsonBase64Status.TooLarge` として結果を失敗（`ResponseParseFailed` 9006）にする
- 日付文字列（`calendarEvents` の `startDate` / `endDate`）は ISO 8601 UTC。`DateTimeOffset.TryParse` を `InvariantCulture` + `AssumeUniversal` で行い、失敗時は `null` として保持する（イベント全体は落とさない）
- パース失敗時は `MacClipboardErrorCodes.ResponseParseFailed`(9006) の失敗結果を返す。**ネイティブが成功と言った結果を C# が解釈できない状態を、成功として通さない**
- ログを出さない（Builder と同じ理由）

### 5.5 結果型（共通形）

```csharp
public readonly struct MacClipboardXxxResult
{
    public string Operation { get; }              // 値なし操作のみ。値あり結果は型で判別できるので任意
    public bool IsSuccess { get; }
    public MacClipboardErrorInfo? Error { get; }  // IsSuccess == false のときだけ非 null
    public TPayload? Value { get; }               // IsSuccess == true のときだけ非 null（readData の null は例外）
}
```

不変条件（EditMode で検証する）:

- `IsSuccess == true` ⇔ `Error == null`
- `IsSuccess == false` ⇔ `Value == null`
- `MacClipboardReadDataResult` のみ「成功 かつ `Data == null`」を許す（要求した UTI が pasteboard に無いケース。エラーではない）
- `Error.Message` は空にならない（`Create` が正規化する）

### 5.6 `MacClipboardManager.cs`

#### クラス構成

- `public class MacClipboardManager : MonoBehaviour`
- ガード: `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`（testing.md の A 群。EditMode / PlayMode から build target を切り替えずに到達できる）
- P/Invoke・`[MonoPInvokeCallback]`・永続 delegate: `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR`
- `private const string LogTag = "MacClipboardManager";`
- Singleton は `MacShareManager` と同形（`Instance` getter が GameObject を生成して `DontDestroyOnLoad`）
- `Awake` で `s_mainThreadId` と `s_dispatcher`（`UnityMainThreadDispatcher.Instance`）を捕捉する

#### 操作名（single-flight キー）

```csharp
public const string OperationCopy = "copy";
public const string OperationAppend = "append";
public const string OperationRead = "read";
public const string OperationReadData = "readData";
public const string OperationSnapshot = "snapshot";
public const string OperationClear = "clear";
public const string OperationCreatePasteboard = "createPasteboard";
public const string OperationRemovePasteboard = "removePasteboard";
public const string OperationDetectPatterns = "detectPatterns";
public const string OperationDetectValues = "detectValues";
public const string OperationDetectMetadata = "detectMetadata";
public const string OperationAccessBehavior = "accessBehavior";
public const string OperationStartObserving = "startObserving";
public const string OperationStopObserving = "stopObserving";
public const string OperationCheckForegroundChange = "checkForegroundChange";

internal const string ObservationControlKey = "observation";  // start / stop で共有
```

#### 公開イベント（共通イベント・常に発火）

```csharp
public event Action<MacClipboardOperationResult>? ClipboardOperationCompleted;  // removePasteboard / startObserving / stopObserving
public event Action<MacClipboardOwnershipResult>? OwnershipChanged;             // copy / append
public event Action<MacClipboardReadResult>? ReadCompleted;
public event Action<MacClipboardReadDataResult>? ReadDataCompleted;
public event Action<MacClipboardSnapshotResult>? SnapshotCompleted;
public event Action<MacClipboardChangeCountResult>? ClearCompleted;
public event Action<MacPasteboardScopeResult>? PasteboardCreated;
public event Action<MacClipboardDetectedPatternsResult>? PatternsDetected;
public event Action<MacClipboardDetectedValuesResult>? ValuesDetected;
public event Action<MacClipboardDetectedMetadataResult>? MetadataDetected;
public event Action<MacClipboardAccessBehaviorResult>? AccessBehaviorChecked;
public event Action<MacClipboardForegroundChangeResult>? ForegroundChangeChecked;
public event Action<MacClipboardChangeEvent>? ClipboardChanged;                 // 監視イベント
```

#### 公開メソッド（callback 提供方針）

各操作は「共通イベント（常に発火）」と「個別 callback（任意・省略可）」の両方を提供する。

```csharp
public void Copy(MacClipboardContent content, MacPasteboardScope? scope = null,
                 MacClipboardCopyOptions? options = null,
                 Action<MacClipboardOwnershipResult>? onResult = null);

public void Append(MacClipboardContent content, MacPasteboardOwnership ownership,
                   Action<MacClipboardOwnershipResult>? onResult = null);

public void Read(MacPasteboardScope? scope = null,
                 Action<MacClipboardReadResult>? onResult = null);

public void ReadData(string utType, MacPasteboardScope? scope = null,
                     Action<MacClipboardReadDataResult>? onResult = null);

public void Snapshot(IReadOnlyList<string>? matchingTypes = null, MacPasteboardScope? scope = null,
                     Action<MacClipboardSnapshotResult>? onResult = null);

public void Clear(MacPasteboardScope? scope = null,
                  Action<MacClipboardChangeCountResult>? onResult = null);

public void CreatePasteboard(MacPasteboardCreationRequest request,
                             Action<MacPasteboardScopeResult>? onResult = null);

public void RemovePasteboard(MacPasteboardScope scope,
                             Action<MacClipboardOperationResult>? onResult = null);

public void DetectPatterns(IReadOnlyCollection<MacClipboardDetectionPattern> patterns,
                           MacPasteboardScope? scope = null,
                           Action<MacClipboardDetectedPatternsResult>? onResult = null);

public void DetectValues(IReadOnlyCollection<MacClipboardDetectionPattern> patterns,
                         MacPasteboardScope? scope = null,
                         Action<MacClipboardDetectedValuesResult>? onResult = null);

public void DetectMetadata(MacPasteboardScope? scope = null,
                           Action<MacClipboardDetectedMetadataResult>? onResult = null);

public void GetAccessBehavior(MacPasteboardScope? scope = null,
                              Action<MacClipboardAccessBehaviorResult>? onResult = null);

public void StartObserving(MacPasteboardScope? scope = null,
                           double intervalSeconds = DefaultObservationInterval,
                           Action<MacClipboardChangeEvent>? onChanged = null,
                           Action<MacClipboardOperationResult>? onStarted = null);

public void StopObserving(Action<MacClipboardOperationResult>? onResult = null);

public void CheckForegroundChange(MacPasteboardScope? scope = null,
                                  Action<MacClipboardForegroundChangeResult>? onResult = null);
```

`public const double DefaultObservationInterval = 0.5;`（ネイティブの `MacClipboardManager.defaultObservationInterval` と一致させる）

callback 契約:

- **共通イベントは常に発火する**。個別 callback の有無に関係しない。拒否経路（メインスレッド違反・破棄済み・引数不正・非対応プラットフォーム・多重呼び出し）でも発火する
- **個別 callback は任意**。`IosNotificationManager` / `IosClipboardManager` の per-call callback 方式に準拠する
- **dispatch 順序は 共通イベント → 個別 callback** で固定する。`InvokeInOrder` が唯一の発火経路
- 共通イベントと個別 callback は**それぞれ独立した try/catch** で包む。片方の例外がもう片方を止めない。例外はネイティブ呼び出し元へ漏らさない
- 単一実行ガードにより、同一操作の pending は最大 1 件。個別 callback スロットも操作あたり 1 つで足り、「後勝ちで先行 callback が消える」問題は起きない（2 件目は `Busy` で即失敗する）
- `scope` を `null` にすると `MacPasteboardScope.General` を使う。**ネイティブは `scopeJson` の省略を許さない**ため、C# 側が必ず general の JSON を組み立てて渡す
- `onChanged` は監視 1 回分の登録。ネイティブの `onChange` には常に永続 delegate を渡すため、C# の `onChanged` が `null` でも 1302 にはならず、共通イベント `ClipboardChanged` だけが発火する

#### ガードチェーン（`TryStartOperation`）

`IosClipboardManager` と同じ 5 段。順序を変えない。

1. **メインスレッド** — 違反時は `MainThreadRequired`(9003)。ログを含むすべてを dispatcher の closure 内で行う
2. **破棄済み（tombstone）** — `ManagerDestroyed`(9004)。引数の妥当性に関係なく拒否
3. **引数検証** — `InvalidRequest`(9005)。5.6「引数検証」参照
4. **プラットフォーム** — `Application.platform != RuntimePlatform.OSXPlayer` は `BridgeUnavailable`(9002)、メッセージは `"{operation} is only available on a macOS Standalone player."`
5. **単一実行** — `s_inFlight.Add(key)` が false なら `Busy`(9001)、メッセージは `"{operation} is already in progress."`

- 拒否経路は `DispatchRejectedResult` を通し、**pending スロットと in-flight マーカーに触れない**（それらは進行中の別呼び出しの所有物）
- ネイティブ呼び出しの例外は `InvokeNative` が捕捉し、in-flight を解放して `BridgeUnavailable`(9002) + `"{operation} could not be started."` を返す

#### 引数検証（C# 側で行うもの）

| 操作 | 検証 | 理由 |
| --- | --- | --- |
| Copy / Append | `content == null` | null 参照バグ |
| Copy / Append | representations の合計バイト数が `MaxRequestBytes`(既定 32 MiB) を超える | base64 化で 4/3 に膨らみ、managed 側で OOM しうる。ネイティブの 100/200 MiB 制限に到達する前に落とす |
| Append | `ownership == null` | null 参照バグ |
| ReadData | `utType` が null / 空白 | ネイティブは 1302 を返すが、原因が呼び出し側にあることを明示する |
| CreatePasteboard | `request == null` | null 参照バグ |
| RemovePasteboard | `scope == null`、または `scope.Kind == General` | general は必ず 1508 になる。ネイティブ往復を省く（メッセージはネイティブと同文言にする） |
| DetectPatterns / DetectValues | `patterns == null` または空 | ネイティブは 1503 を返すが、**patterns の不正が scope のエラーとして分類される**（1.6 参照）。C# 側で先に落として原因を明確にする |
| StartObserving | `intervalSeconds <= 0` または `> 60` | ネイティブは 1523。事前に落として往復を省く |

- ここに挙げた以外の検証（UTI の妥当性、pasteboard 名の形式、サイズ上限、空 representation）は**ネイティブに委ねる**。二重検証は 2 つのエラー契約を生む
- `MacPasteboardScope.Named("")` / `Unique("")` は payload の factory が `ArgumentException` を投げる（呼び出し側のバグ扱い、`IosPasteboardScope` と同じ）

#### スレッド契約

- **公開 API は Unity メインスレッド専用**。他スレッドからの呼び出しは `MainThreadRequired`(9003) で拒否する。`Instance` getter だけは GameObject を生成するため保護できない（`IosClipboardManager` と同じ制約）
- ネイティブコールバックは**必ず main thread で届く**（Swift ファサードが main actor へ hop する）。macOS Standalone では AppKit のメインスレッド = Unity のメインスレッド
- それでも結果は `UnityMainThreadDispatcher.Enqueue` を通す。ネイティブのスタックフレーム内で購読者コードを実行しない（既存 `MacShareManager` / `IosClipboardManager` と同じ）
- `s_inFlight` / per-call スロットはメインスレッドからしか触らないためロック不要

#### メモリ契約

- 15 個の操作 delegate と 1 個の変更 delegate を `static readonly` で保持し、GC 回収とネイティブ関数ポインタの無効化を防ぐ
- コールバックの `const char*` は即座に managed string へコピーする（marshaling が行う）。ポインタを保持しない
- base64 の巨大文字列は解析後に参照を切る。`JsonValue` は文字列のオフセットを保持する設計なので、`TryGetBase64Bytes` でソース span から直接デコードし、中間 substring を作らない
- unique pasteboard は pasteboard server 上に残る。`CreatePasteboard` の結果 scope を呼び出し側が保持し、不要になったら `RemovePasteboard` を呼ぶ責務は**呼び出し側**にある（Manager は追跡しない）

#### エラー契約

- ネイティブの `errorCode` / `errorMessage` は**再翻訳せずそのまま** `MacClipboardErrorInfo` に載せる
- C# ブリッジ層のみ 9001-9006 を使う（5.2）
- `isSuccess == true` かつ `json == null`、または JSON が解釈できない場合は `ResponseParseFailed`(9006) の失敗に変換する
- `IsSuccess == true` のとき `Error == null` を必ず保証する

#### ライフサイクル

- `OnDestroy`:
  1. tombstone (`s_isTerminated = true`) を**ネイティブ呼び出しより先に**立てる
  2. `RunDestroyCleanup(stop: StopObservingForTeardown, cancel: NoOp, managedCleanup: ClearAllPendingCallbacks + _instance = null)`
     - macOS には iOS の `cancelLoads` に相当する関数が無いため、`cancel` ステップは不要。`RunDestroyCleanup` のシグネチャを 2 引数版にするか、`cancel` に空実装を渡すかは実装時に決める（テストのしやすさを優先し、`stop` / `managedCleanup` の 2 引数版を新設する）
  3. `s_dispatcher` は残す（破棄後の拒否結果を配送するため）
- 破棄後の遅延コールバックは `DiscardIfTerminated` で捨てる。ネイティブ ABI にリクエスト ID が無いため、遅延コールバックと新規コールバックを区別できない。tombstone があるので「破棄後に新規操作は開始されていない」ことが保証され、捨てて安全
- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` で static 状態をリセットする（Domain Reload 無効時に tombstone が Play セッションをまたぐのを防ぐ）
- `#if UNITY_EDITOR` の `ResetForTests()` / `BridgeAvailableOverrideForTests` を用意する

#### 監視の世代管理

`StartObserving` / `StopObserving` は `ObservationControlKey` を共有する（同時に 1 件だけ）。加えて `IosClipboardManager` と同じ世代カウンタを持つ。

- `s_observingGeneration`（単調増加）/ `s_onChangedGeneration`（現在の登録の世代）/ `s_pendingObservationGeneration`（進行中の制御呼び出しが責任を持つ世代）
- `ReleaseChangeRegistrationIfOwned()` は `s_onChangedGeneration <= s_pendingObservationGeneration` のときだけ登録を消す。**古い stop の完了が新しい start の登録を消さない**
- 参照等価では判定できない（同じ delegate インスタンスが 2 回登録されうる）

#### Awaitable 版について

- 本計画では **callback 版のみ**を実装する。`IosClipboardManager` と対称にする
- common.md の前提条件（in-flight ガード）は本設計で満たされるため、`XxxAsync` の追加は後から非破壊的に可能。必要になった時点で別途設計する

#### ログ方針（csharp.md からの意図的な逸脱）

- clipboard 本文はパスワード・トークン・文書を含みうる。**値をログに出さない**
- 出してよいのは shape / count / flag のみ: `itemCount`、`representationCount`、`totalBytes`、`hasScope`、`scopeKind`、`hasCallback`、`operation`、`errorCode`
- pasteboard 名も出さない（ワークフローや文書を識別しうる。ネイティブ側 `NTScope` が general 以外をハッシュ化しているのと同じ方針）
- Builder / Parser / 結果型のファクトリはログを出さない。Manager の dispatch 境界だけが操作名・成否・errorCode を出す
- この逸脱の理由は各ファイル冒頭のコメントに明記する（`IosClipboardManager` / `IosClipboardJsonBuilder` と同じ形式）

### 5.7 実装順序（依存順）

1. `ClipboardJsonReader.cs` への改名とガード拡張（+ 既存 iOS 側の参照更新）。既存 iOS テストが全 pass することを先に確認する
2. `MacClipboardPayloads.cs` / `MacClipboardErrorInfo.cs`（依存なし）
3. 結果型 11 ファイル（Payload / ErrorInfo に依存）
4. `MacClipboardJsonBuilder.cs` + `MacClipboardJsonBuilderTests.cs`
5. `MacClipboardJsonParser.cs` + `MacClipboardJsonParserTests.cs`
6. `MacClipboardResultTests.cs`
7. `MacClipboardManager.cs`（Bridge → ガードチェーン → dispatch → 15 操作の順に組む）
8. `MacClipboardManagerDispatchTests.cs`（`internal static` の純粋関数を対象）
9. `MacClipboardManagerIntegrationTests.cs`（PlayMode）
10. Unity Test Runner で EditMode / PlayMode を実行し、**既存テストを含めて**全 pass を確認する

---

## 6. エラーケース一覧と返却仕様（層別）

### 6.1 C# Bridge 層（ネイティブ呼び出し前 / 例外）

| code | 条件 | Message |
| --- | --- | --- |
| 9001 `Busy` | 同一操作が既に pending（start/stop observing は共通キー） | `{operation} is already in progress.` |
| 9002 `BridgeUnavailable` | macOS Standalone Player 以外（Editor・他プラットフォーム） | `{operation} is only available on a macOS Standalone player.` |
| 9002 `BridgeUnavailable` | P/Invoke が例外を投げた | `{operation} could not be started.` |
| 9003 `MainThreadRequired` | Unity メインスレッド以外から呼ばれた | `{operation} must be called from the Unity main thread.` |
| 9004 `ManagerDestroyed` | `OnDestroy` 後の呼び出し | `{operation} is unavailable: MacClipboardManager has been destroyed.` |
| 9005 `InvalidRequest` | 5.6「引数検証」の各ケース | ケースごとの具体文言（例: `content must not be null.` / `utType must not be blank.` / `patterns must not be empty.` / `intervalSeconds must be greater than 0 and at most 60.` / `The general pasteboard cannot be released.` / `Request payload is too large.`） |
| 9006 `ResponseParseFailed` | 成功応答の JSON を解釈できない、または base64 が上限超過 | `The native result could not be parsed.` |

- 呼び出し側のバグ（`MacPasteboardScope.Named("")` 等）は結果ではなく `ArgumentException` を投げる

### 6.2 parser 層（ネイティブ `UnityMacClipboardJsonParser` / `BridgeError`）

| code | 条件 | Message |
| --- | --- | --- |
| 1301 | 引数が供給されたが JSON として解釈できない | `Failed to parse JSON: Invalid clipboard JSON argument.` |
| 1302 | 必須引数が NULL / 空 | `Bridge contract violation: A required argument was missing.` |
| 1302 | `startObserving` の `onChange` が NULL | `Bridge contract violation: onChange is required; observation would produce no observable result.` |

- C# は常に必須引数を埋め、`onChange` に永続 delegate を渡すため、正しく実装されていればこの層には到達しない。到達した場合は C# 側のバグを示す

### 6.3 use case / repository 層（MacLibrary `ClipboardError`）

1.5 の表がそのまま返却仕様。操作ごとの到達可能性:

| 操作 | 到達しうる code |
| --- | --- |
| Copy | 1501, 1502, 1504, 1505, 1506, 1507, 1509, 1599 |
| Append | 1501, 1502, 1504, 1505, 1506, 1507, 1510, 1511, 1599 |
| Read | 1505, 1507, 1599 |
| ReadData | 1504, 1505, 1507, 1599 |
| Snapshot | 1505, 1507, 1512, 1599 |
| Clear | 1505, 1507, 1599 |
| CreatePasteboard | 1505, 1599 |
| RemovePasteboard | 1505, 1507, 1508, 1599 |
| DetectPatterns | 1503, 1505, 1507, 1513, 1514, 1515, 1524, 1599 |
| DetectValues | 1503, 1505, 1507, 1513, 1514, 1515, 1524, 1599 |
| DetectMetadata | 1505, 1507, 1513, 1514, 1515, 1599 |
| GetAccessBehavior | 1505, 1507, 1599（**macOS 15.4 未満は成功 + `"unavailable"`**） |
| StartObserving | 1505, 1507, 1523, 1599 |
| StopObserving | なし（常に成功） |
| CheckForegroundChange | 1505, 1507, 1599 |

- 上表は Swift ファサードと use case の実装から読み取った到達可能性であり、**実機での網羅確認は未実施**。要検証（8 章）
- 1599 はエンコード失敗も含むため、どの値返却操作でも起こりうる

### 6.4 エラーではないケース

| ケース | 表現 |
| --- | --- |
| `ReadData` で該当 UTI が無い | 成功 + `Data == null` |
| `Read` で pasteboard が空 | 成功 + `Items.Count == 0` |
| `DetectPatterns` で何も一致しない | 成功 + 空リスト |
| `GetAccessBehavior` が macOS 15.4 未満 | 成功 + `MacClipboardAccessBehavior.Unavailable` |
| `StopObserving` を購読していない状態で呼ぶ | 成功（冪等） |
| `CheckForegroundChange` の scope 初回呼び出し | 成功 + `Changed == true`（実際の変更有無に関係なく） |

---

## 7. テスト方針

### 7.1 EditMode（層 1 / `Tests/Runtime/`）

Manager インスタンスを生成しない。`internal static` の純粋関数と Builder / Parser / 結果型だけを対象にする。

`MacClipboardJsonBuilderTests`:

- scope: general が `name` キーを出さないこと、named / unique が出すこと
- content: representations の base64、複数 item、空 dictionary の扱い
- options: `null` → `null` を返す、`localOnly` の true / false
- ownership: scope の入れ子と `changeCount`
- createRequest: named / unique の形
- matchingTypes: `null` → `null`、空リスト → `[]`
- patterns: enum → rawValue（`ProbableWebUrl` → `"probableWebURL"` を含む）、ソート順の決定性
- エスケープ: `"`・`\`・制御文字・非 ASCII
- カルチャ非依存: `CultureInfo.CurrentCulture` を `de-DE` にしても数値表現が変わらないこと

`MacClipboardJsonParserTests`:

- 10 出力型 + イベントの正常系
- `ReadDataJson` の `{"data":null}` が成功 + null になること
- `DetectedValuesJson` の明示的 null と空配列の区別
- `PatternsJson` がトップレベル配列であること
- 未知の pattern 文字列が無視されること、未知の accessBehavior が `Unknown` になること
- 不正 JSON / 型不一致 / キー欠落が失敗になること
- base64 上限超過が失敗になること
- ISO 8601 日付のパース（UTC、オフセット付き、不正文字列 → null）

`MacClipboardResultTests`:

- 5.5 の不変条件すべて
- `MacClipboardErrorInfo.Create` の正規化（null / 空白 message → 既定文言、`long` → `int` の narrow）

`MacClipboardManagerDispatchTests`:

- `InvokeInOrder`: 共通 → 個別の順序、共通が例外を投げても個別が呼ばれること、逆も同様、例外が外へ漏れないこと
- `TryBeginOperation` / `EndOperation`: 2 回目の `Add` が false、`Remove` 後は再度取得できる、未登録の `Remove` が安全
- `RunDestroyCleanup`: `stop` が例外を投げても `managedCleanup` が必ず走ること
- `BridgeAvailableOverrideForTests` を使い、実際の pending スロット・in-flight 集合を駆動して 5 段のガードチェーンの各拒否経路を検証する

### 7.2 PlayMode（層 2a / `Tests/PlayMode/`）

`UnityMainThreadDispatcher` は実際の `Update` が無いと flush されないため、EditMode では通らない経路を埋める。

- dispatcher 経由で共通イベント → 個別 callback の順序が保たれること
- Editor（非 macOS Player）での失敗経路: `BridgeUnavailable` + `"{operation} is only available on a macOS Standalone player."`
- `StopObserving` を購読なしで呼んでも `ClipboardOperationCompleted` が 1 回だけ発火すること
- 破棄後の呼び出しが `ManagerDestroyed` で拒否されること
- `MacClipboardManager` は A 群ガード（`|| UNITY_EDITOR`）なので **build target の切り替えなしに実行できる**

### 7.3 層 2b / 層 3

testing.md の通り未着手。本計画では新規に導入しない。

### 7.4 手動確認（実機 macOS 15+ / macOS Standalone Player）

ネイティブ Bridge に依存するため自動化しない。`design-sample-scene` で作る ExampleController から確認する。

| # | 確認項目 | 期待 |
| --- | --- | --- |
| 1 | `Copy`（plain text）→ 他アプリで Cmd+V | 貼り付けできる。`OwnershipChanged` が成功で発火 |
| 2 | `Copy` 直後に `Append` | 成功。`changeCount` が更新される |
| 3 | 他アプリでコピー後に `Append` | 1511（ownership lost） |
| 4 | `Read`（他アプリがコピーしたテキスト） | 読める。書いた型以外の派生型も含まれることを確認 |
| 5 | `ReadData`（存在しない UTI） | 成功 + `Data == null` |
| 6 | `Snapshot`（フィルタなし / あり） | `itemTypes` と `matchingItemIndexes` が返る |
| 7 | `Snapshot`（空配列フィルタ） | 1512 |
| 8 | `Clear` → `Read` | items が空 |
| 9 | `CreatePasteboard(Unique)` → `Copy` → `Read` → `RemovePasteboard` | 一連が成功。解放後の `Read` の挙動を記録 |
| 10 | `RemovePasteboard(General)` | C# 側で 9005 拒否（ネイティブ 1508 に到達しない） |
| 11 | `DetectPatterns`（URL・電話番号を含むテキスト） | 一致パターンが返る。macOS 15.4 未満では 1513 |
| 12 | `DetectValues`（同上） | 値が返る。**許可ダイアログの有無と、拒否時に 1514 になること**を記録 |
| 13 | `DetectMetadata`（plain text） | 1515 で失敗することを確認（仕様通り） |
| 14 | `GetAccessBehavior` | macOS 15.4+ で `default`/`ask`/`alwaysAllow`/`alwaysDeny`、15.0-15.3 で `unavailable` |
| 15 | `StartObserving` → 他アプリでコピー | `ClipboardChanged` が発火。**アプリ非アクティブ中は止まり、前面復帰時に追いつく**ことを確認 |
| 16 | `StartObserving` を 2 回連続 | 2 回目が新設定で再開。重複発火しない |
| 17 | `StartObserving(interval: 0)` / `(interval: 61)` | C# 側で 9005 拒否 |
| 18 | `StopObserving` 後にコピー | イベントが来ない |
| 19 | `CheckForegroundChange` の初回 / 2 回目 | 初回 true、変更なしの 2 回目 false |
| 20 | 大きな画像（10 MiB 超）の `Copy` | 成功。所要時間と `Copy` 中のフレーム落ちを記録 |
| 21 | 32 MiB 超の `Copy` | C# 側で 9005 拒否 |
| 22 | Universal Clipboard（`localOnly: false`）で別 Apple デバイスへ | **未検証項目**。ネイティブ側も実機未確認と明記しているため結果を記録する |
| 23 | ログ確認 | clipboard 本文・pasteboard 名が Console / Player.log に出ていないこと |
| 24 | App Sandbox 有効ビルド | named / unique pasteboard の作成・解放が可能か記録（8 章） |

---

## 8. 要検証事項

| # | 項目 | 内容 |
| --- | --- | --- |
| 1 | `NSInteger` のマーシャリング | delegate の `errorCode` を `long` で宣言し、arm64 / x86_64 の macOS Standalone（IL2CPP）で正しい値が届くことを実機確認する。既存 macOS Manager に前例が無い |
| 2 | `BOOL` のマーシャリング | `bool` 宣言（既存 `MacShareManager` と同形）で x86_64 でも正しく届くか。厳密には `[MarshalAs(UnmanagedType.I1)]` が正しい。実機確認の結果次第で切り替える |
| 3 | C# ブリッジエラーコードの範囲 | 9001-9006 が将来のネイティブコードと衝突しないこと。native-toolkit 側に「9000+ は Unity ブリッジ予約」を明記してもらう |
| 4 | `ClipboardJsonReader` 共有化の是非 | 改名するか、ガード拡張のみに留めるか（4.2 の代替案）。レビューで確定する |
| 5 | エラー到達可能性の表（6.3） | Swift 実装からの読み取りであり、実機で全ケースを再現していない |
| 6 | App Sandbox 下の named / unique pasteboard | サンドボックス有効時に pasteboard server 上のリソースを作成・解放できるか未確認 |
| 7 | `localOnly` の効果 | ネイティブ側が「Universal Clipboard への効果は実機未確認」と明記している。C# 側も同じ注意を XML コメントに引き写す |
| 8 | サイズ上限 `MaxRequestBytes = 32 MiB` | 暫定値。実機の所要時間・メモリ使用量を測って確定する。ネイティブの `ClipboardLimits.default` も「実機未計測の暫定値」と明記されている |
| 9 | 監視のアクティブ／非アクティブ挙動 | Unity の macOS Player がバックグラウンドに入ったときのポーリング停止・復帰の実挙動 |
| 10 | `detectValues` の許可 UI | ユーザーへの通知の有無・条件・拒否時の 1514 の再現性。testing.md 5 節の「層 3 再確認」対象 |
| 11 | `RunDestroyCleanup` のシグネチャ | macOS には `cancelLoads` 相当が無い。2 引数版を新設するか、既存 3 引数版に空実装を渡すか実装時に確定する |
| 12 | Editor 実行 | 本計画では Editor 実行を非対応とする（既存 macOS Manager と同じ）。Editor で xcframework をロードして動かす要求が出た場合は別途設計する |

---

## 9. 出力範囲の明記

本計画書に**含まないもの**:

- サンプルアプリ: `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs`、UXML / USS、サンプルシーン、`NativeToolkitSampleNavigator` への導線、`MacClipboardSampleSceneWiringTests`
  - → `design-sample-scene` スキルで別途設計する
- マニュアル（`Documentation~/` / `docs/`）
  - → `write-manual` スキルで別途作成する
- ネイティブ（native-toolkit）側の変更
  - → 15 関数が実装済み・エクスポート済みのため不要
- `package.json` のバージョン更新、CHANGELOG、リリース作業
  - → `release` スキルで行う
