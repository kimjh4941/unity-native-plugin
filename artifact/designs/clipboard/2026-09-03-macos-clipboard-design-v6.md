# macOS Clipboard 実装計画書 (v6)

- 対象機能: clipboard
- 対象プラットフォーム: macOS
- 対象パッケージ: `Packages/com.jonghyunkim.nativetoolkit`
- 作成日: 2026-09-03
- 前版: `2026-09-03-macos-clipboard-design-v5.md`
- レビュー: `artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v5.md`
- 出力範囲: Runtime（Bridge / Manager / Payload / Result / JSON）とテストのみ。サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は `design-sample-scene` スキルで別途設計する

## v5 からの主な変更

v5 のレビュー（must fix 2 / should fix 5 / nit 6）をすべて反映した。v5 レビューは「前回の must fix を直すと新しい must fix を生む」パターンが止まったと判定している。

| 分類 | 内容 |
| --- | --- |
| スロット解決の一般化 | `MacClipboardOwnershipResult` は結果型 1 つに対しスロットが 2 本（`s_onCopy` / `s_onAppend`）あるため、`FireOwnershipResult` も `Operation` からスロットを引く。規約を「複数操作が共有する結果型は `Operation` からスロットを引く」に一般化（M5-1） |
| 監視登録の解放条件 | `ReleaseChangeRegistrationIfOwned()` を呼ぶ外側のゲート（`!isSuccess \|\| operation == StopObserving`）を追加。**これが無いと成功した `StartObserving` が自分の `onChanged` 登録を即座に破棄する**（M5-2。v3 から 3 版生き延びた抜け） |
| テストシームの実効化 | `EffectiveMaxRequestBytes()` を追加し、`MaxRequestBytesOverrideForTests` が実際に効くようにした（S5-1） |
| 例外漏れの排除 | 合計バイト数を `long` で積み、`OverflowException` が公開 API から漏れる経路を塞いだ（S5-2） |
| その他 | `ObservationBusyMessage` のヘルパ列挙への追加（S5-3）、`TryStartOperation` の残存参照（S5-4）、7.2 の重複行（S5-5）、Fire の本数（N5-1）、`null!` の扱い（N5-2）、null の言い換え（N5-3）、delegate 命名規約（N5-4）、引用行 3 件（N5-5）、テスト文言（N5-6） |

---

## 0. 前提と現状

- native-toolkit の macOS Clipboard 実装は**完成済み**。Unity 側は C ABI を叩くだけでよく、pasteboard ロジックを C# で再実装しない
- 同梱 xcframework `Plugins/macOS/unity-mac-native-toolkit-1.3.0.xcframework` に **15 個の clipboard C 関数がすべてエクスポート済み**であることを `nm -gU` で確認した（3.1 に一覧）。ネイティブの再ビルド・差し替えは不要
- Unity 側 macOS Clipboard 実装は**存在しない**（`Runtime/Clipboard/` にあるのは Android / iOS のみ）
- 既存 macOS Manager（`MacShareManager` / `MacNotificationManager` / `MacDialogManager`）と既存 Clipboard Manager（`IosClipboardManager`）の両方のパターンを踏襲する

### 0.1 成果物境界（着手前に必ず確認する）

本計画書の 1 章は `/Users/jonghyunkim/Desktop/native-toolkit/mac/` の**ソース**から読み取っている。一方 Unity が実際にリンクするのは同梱**バイナリ** 1.3.0 である。`nm -gU` で確認したのは**シンボルの存在だけ**であり、そのバイナリがこのソースツリーからビルドされたことは確認していない。

- ソースが先行していると、JSON スキーマ・エラーコード・既定値が食い違い、**全パーサが 9006 で落ちる**
- 実装着手前に、参照したソースの commit / タグと xcframework 1.3.0 のビルド元が一致することを確認する（8 章 V-1）
- 一致しない場合は、xcframework を再ビルドして差し替えるか、バイナリ側の仕様に合わせて 1 章を書き直す

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
| `MacLibrary/MacLibrary/Clipboard/Application/UseCase/*.swift` | 検証ロジックとエラーの発生源 |
| `MacLibrary/MacLibrary/Clipboard/Data/Repository/ClipboardRepositoryImpl.swift` | pasteboard 操作の実体 |
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

`clipboardStartObserving` の第 3・第 4 引数は `callback`（操作結果） → `onChange`（イベント）の順で、iOS の同名関数とは逆である。ヘッダの宣言に従う。

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

#### 1.2.1 `NSInteger` のマーシャリング（確定）

- **`errorCode` は `NSInteger` = 64bit**。既存の `MacNotificationManager` が参照している macOS Notification bridge は C の `int errorCode` を宣言しており、**Clipboard だけ型が異なる**
- C# の delegate では `long` で受け、`MacClipboardErrorInfo.Create` の 1 箇所だけで `int` に narrow する（5.2.0）

#### 1.2.2 `BOOL` のマーシャリング（確定）

- **`[MarshalAs(UnmanagedType.I1)] bool isSuccess` とする。** v1 の「既存 `MacShareManager` の素の `bool` を踏襲する」は撤回する
- 理由: C# は既定で `bool` を 4 バイトの Win32 BOOL としてマーシャルする。Objective-C の `BOOL` は arm64 で `_Bool`、x86_64 で `signed char` の **1 バイト**であり、レジスタ上位ビットの残骸を「非 0 = true」と読んで**失敗を成功と誤判定しうる**
- Clipboard 機能の直近の前例である `IosClipboardManager.cs:193-199` は既にこの問題を認識し、`I1` で幅を固定したうえで理由コメントを付けている。macOS 版もこれに揃える
- 「既存 `MacShareManager` が素の `bool` で動いている」ことは根拠にならない（上位ビットが偶然ゼロである場合と区別できない）。`MacShareManager.cs:59` の未修正は本計画の範囲外の別課題として残す

#### 1.2.3 文字列のマーシャリング

- `const char*` は UTF-8。`errorMessage` にはネイティブが `{name}` / `{value}` を埋め込み（1504 / 1505 / 1506）、`DetectedValuesJson` の `probableWebSearch` / `postalAddresses` には非 ASCII が普通に入る
- delegate 引数・`DllImport` 引数とも、まず既定のマーシャリング（IL2CPP は Apple プラットフォームで UTF-8 として扱う）で実装する。既存 `MacShareManager` / `MacNotificationManager` が同形で動作している
- 実機で非 ASCII が化けた場合の対処は `[MarshalAs(UnmanagedType.LPUTF8Str)]` の明示付与とする（8 章 V-2）

#### 1.2.4 NULL コールバックの扱い

ネイティブ仕様:

- 操作コールバックの NULL は**エラーではない**。処理は実行され結果が返らないだけ
- 例外 1: `clipboardCreatePasteboard` は callback が NULL のとき**何も作らない**（unique pasteboard の名前を返せないと解放不能になるため）
- 例外 2: `clipboardStartObserving` は `onChange` が NULL のとき **1302 を返して購読を開始しない**

C# 側の方針:

- **通常経路では常に非 NULL の永続 delegate を渡す。** 上記 2 つの例外分岐には依存しない
- **唯一の例外は `OnDestroy` の `StopObservingForTeardown` で、ここだけ NULL を渡す。** 破棄後は結果を配送できず、渡した delegate の結果は tombstone で捨てられるだけだから

### 1.3 JSON スキーマ（`UnityMacClipboardJsonParser.swift` が正本）

出力はすべて `JSONEncoder.outputFormatting = [.sortedKeys]` で生成される。**キー順はアルファベット順に安定する**ため、テストのゴールデン文字列の前提にできる。

#### 入力専用（4）

| 型 | 形 |
| --- | --- |
| `ContentJson` | `{"items":[{"representations":{"<utType>":"<base64>"}}]}` |
| `OptionsJson` | `{"localOnly": true\|false}`。**`localOnly` はキー省略可**（`Bool?` + `?? true`）。`{}` も有効で `localOnly: true` になる |
| `CreateRequestJson` | `{"kind":"named","name":"<name>"}` / `{"kind":"unique"}` |
| `MatchingTypesJson` | `["<utType>", ...]`（トップレベル配列） |

#### 入出力共用（3）

| 型 | 形 |
| --- | --- |
| `ScopeJson` | `{"kind":"general"\|"named"\|"unique","name":"<name>"?}` |
| `OwnershipJson` | `{"scope":ScopeJson,"changeCount":Int}` |
| `PatternsJson` | `["links","number", ...]`（トップレベル配列） |

**`ScopeJson.name` は独自 `encode` を持たないため、Swift の合成エンコード（`encodeIfPresent`）により general では出力されない。** general の scope は `{"kind":"general"}` であり、`name` キーは**存在しない**。`OwnershipJson` / `ScopeResultJson` / `ChangeEventJson` の入れ子 scope も同様。

#### 出力専用（9）

| 型 | 形 |
| --- | --- |
| `ReadResultJson` | `{"changeCount":Int,"items":[{"representations":{...}}]}` |
| `ReadDataJson` | `{"data":"<base64>"\|null}`（キーは常に出力、null は「型が無い」で成功） |
| `SnapshotJson` | `{"changeCount":Int,"itemTypes":[[String]],"matchingItemIndexes":[Int]}` |
| `ChangeCountJson` | `{"changeCount":Int}` |
| `BoolJson` | `{"value":Bool}` |
| `DetectedValuesJson` | 下記 1.3.1 |
| `DetectedMetadataJson` | `{"metadataTypes":[String],"contentTypeIdentifier":String\|null}` |
| `AccessBehaviorJson` | `{"value":"default"\|"ask"\|"alwaysAllow"\|"alwaysDeny"\|"unavailable"}` |
| `ScopeResultJson` | `{"scope":ScopeJson}` |

#### イベント（1）

| 型 | 形 |
| --- | --- |
| `ChangeEventJson` | `{"scope":ScopeJson,"changeCount":Int}` |

#### 1.3.1 `DetectedValuesJson` の完全スキーマ

トップレベル（すべてのキーが常に出力され、optional は明示的 `null`）:

```json
{
  "patterns": ["links", ...],
  "probableWebURL": "https://..." | null,
  "probableWebSearch": "..." | null,
  "number": 12.5 | null,
  "links": [Link],
  "phoneNumbers": [PhoneNumber],
  "emailAddresses": [EmailAddress],
  "postalAddresses": [PostalAddress],
  "calendarEvents": [CalendarEvent],
  "shipmentTrackingNumbers": [ShipmentTracking],
  "flightNumbers": [FlightNumber],
  "moneyAmounts": [MoneyAmount]
}
```

入れ子型:

| 型 | フィールド | null になりうるか |
| --- | --- | --- |
| `Link` | `matchedString`, `url` | いずれも非 null |
| `PhoneNumber` | `matchedString`, `phoneNumber`, `label` | `label` が**明示的 null** |
| `EmailAddress` | `matchedString`, `emailAddress`, `label` | `label` が**明示的 null** |
| `PostalAddress` | `matchedString`, `street`, `city`, `state`, `postalCode`, `country` | `matchedString` 以外の 5 件すべてが**明示的 null** |
| `CalendarEvent` | `matchedString`, `isAllDay`, `startDate`, `startTimeZoneIdentifier`, `endDate`, `endTimeZoneIdentifier` | 日付・タイムゾーンの 4 件が**明示的 null**。日付は ISO 8601 UTC 文字列 |
| `ShipmentTracking` | `matchedString`, `carrier`, `trackingNumber` | すべて非 null |
| `FlightNumber` | `matchedString`, `airline`, `flightNumber` | すべて非 null |
| `MoneyAmount` | `matchedString`, `currencyCode`, `amount` | すべて非 null |

#### 1.3.2 スキーマ運用ルール

- デコード時に未知キーは無視される / エンコード時に未知キーは出力されない
- `ReadDataJson` / `DetectedValuesJson` / `DetectedMetadataJson` の optional は**キー省略ではなく明示的な `null`** で出力される（「未要求」と「要求したが未検出」を C# 側で区別できるようにするため）
- 上記 3 型以外の optional（`ScopeJson.name`）は**キーごと省略される**
- `patterns` / `metadataTypes` はソート済みで出力される
- 日付は `Date.ISO8601FormatStyle`（UTC、ロケール非依存）

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
- **空白のみの名前（`" "`）は素通りする。** パーサの判定は `!name.isEmpty` であり、resolver の 1505 も空名専用なので、`NSPasteboard(name: " ")` が実際に作られて**成功**する。これを止めるのは C# の factory（5.1）だけである

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
| 1524 | detection のキャンセル | `The clipboard operation was cancelled.` |
| 1599 | その他（エンコード失敗を含む） | `An unknown clipboard error occurred: {reason}.` |

コードの分類（v1 の注記を修正）:

- **1521 / 1522 は paste ボタン経路**のもので、macOS 版の C ABI は当該 API を公開していない。C# からは到達しない
- **1524 は detection 専用の cancel コード**（`ClipboardError.swift` の「Cancellation is deliberately narrow. `cancelled` covers only the detection APIs」）。`DetectPatterns` / `DetectValues` / `DetectMetadata` の 3 操作から到達する
- 1599 は Swift ファサードが「結果を JSON エンコードできなかった」場合にも使う（`The result could not be encoded.`）

### 1.6 ネイティブ側の制約・注意（設計に影響するもの）

- **読み出しは書き込みの鏡ではない**。pasteboard が型を派生させるため、RTF で書いても plain text として読める。`Read` の結果が `Copy` の入力と一致する前提を置かない
- **append は所有権が必要**。iOS と異なり、他アプリに pasteboard を取られると 1511 で失敗し、黙って無視されない
- **append 成功後の `changeCount` は変わらない**。`ClipboardRepositoryImpl` は append で `prepareForNewContents` を呼ばず、成功時は**受け取った ownership をそのまま返す**（「A successful append leaves the change count untouched, so the caller keeps the same proof of ownership」）。同じ ownership を次の Append にそのまま使い続けられる
- **10 MiB 超の単一 item は lazy data provider 経路に入る**（重要）
  - `ClipboardContentValidator` は `items.count == 1 && totalBytes > warnBytesPerRepresentation`（10 MiB）のとき、`ClipboardRepositoryImpl` の `setDataProvider` 経路を使う
  - pasteboard には**型だけが載り、実バイトは読み手が要求した時点で供給される**。供給時にプロセスが生きている必要がある
  - つまり **Copy 成功 ≠ 貼り付け可能**。Copy 後に Player を終了すると貼り付けられない
  - 発動条件は「単一 item **かつ** 10 MiB 超」の両方。1 MiB の単一 item はこの経路に入らない。C# 上限 32 MiB の範囲では **10〜32 MiB 帯の単一 item** が該当する
  - 複数 item の場合は合計サイズにかかわらず通常の書き込み経路になる
- **named / unique pasteboard はプロセス終了後も残る**（pasteboard server 上）。unique は `RemovePasteboard` で明示解放する。機密データを named に置かない
- **標準 pasteboard は解放できない**（1508）。対象は general だけでなく `font` / `ruler` / `find` / `drag` を含む。判定は名前の一致で行われるため、`kind == "named"` だけでなく `"unique"` でも標準 5 名を渡せば 1508 になる
- **どの読み出しも「ユーザーに通知されない保証は無い」**。`Snapshot` / `DetectPatterns` は payload を読まないが、これは最適化でありプライバシー契約ではない
- **`detectMetadata` は plain text で失敗する**（1515）。「報告するものが無い」と「報告できなかった」を区別できない
- **macOS 15.4 の分岐**
  - `detectPatterns` / `detectValues` / **`detectMetadata`** は macOS 15.4 未満で 1513（3 操作とも同じ `#available(macOS 15.4, *)` ガード）
  - `accessBehavior` は macOS 15.4 未満で `"unavailable"` を返す（失敗しない）
- **監視間隔は `0 < interval <= 60` 秒**。範囲外は 1523。既定は 0.5 秒（`MacClipboardManager.defaultObservationInterval`）
- **監視はアプリ非アクティブ中に停止し、アクティブ復帰時に追いつく**。他アプリの変更は前面復帰時に報告される
- **`startObserving` の再呼び出しは新しい設定で再開する**（重複購読にならない）。`stopObserving` は冪等
- **変更イベントの encode に失敗すると、ネイティブはそのイベントを黙って捨てる**（`UnityMacClipboardManager.swift:396` の `guard let json = ... else { return }`）。エラー通知は無い。C# からは「変更が起きたのに `ClipboardChanged` が来ない」としてしか観測できない
- **`checkForegroundChange` は「監視していない場合の」初回呼び出しで true を返す**
  - `ClipboardChangeMonitor` は start 時に `tracker.hasChanged` で初期値を記録し、tracker を `checkForegroundChange` と共有する
  - 同一 scope で `StartObserving` が走っていると、`CheckForegroundChange` は初回でもほぼ常に false になる
  - ネイティブ側に「Use this instead of observation, not alongside it」と明記がある。**併用しない**
  - tracker は **scope 単位**。ある scope を監視していても、別の scope の `CheckForegroundChange` は tracker 未登録なので初回 true になる
- **サイズ制限**: representation あたり 100 MiB、合計 200 MiB で 1506。10 MiB でログ警告（`ClipboardLimits.default`。ネイティブ側で「実機未計測の暫定値」と明記されている）
- **`localOnly` は未検証**。Universal Clipboard への効果は実機未確認とネイティブ側が明記している
- `detectPatterns` / `detectValues` は `patternsJson` と `scopeJson` を 1 つの `guard` で束ねており、**patterns が不正でも `argumentError(scopeJson)` で分類される**
  - ただし **C# は patterns JSON を enum から生成するため、不正な patterns JSON を送ることがない**。この誤分類は C# 経由では発生しない（決定 1 の根拠）
  - `snapshot` は引数ごとに `argumentError()` を分けているため同じ問題は無い（guard 順は scope → matchingTypes）

---

## 2. 既存 C# 実装の確認結果

参照: `Packages/com.jonghyunkim.nativetoolkit/Runtime/`

### 2.1 Common

| 型 | 内容 |
| --- | --- |
| `Common/UnityMainThreadDispatcher` | `Instance` シングルトン + `Enqueue(Action)` + `Update` での flush。Manager の `Awake` でメインスレッド上に生成しておく |
| `Common/IconConfiguration` | 今回は不使用 |

### 2.2 macOS Manager のパターン（`Share/MacShareManager.cs`）

- クラスガード: `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`（testing.md の A 群）
- P/Invoke とコールバック本体: `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR`
- **delegate 型の宣言は narrow guard の外**（`MacShareManager.cs:58-59` が `#if` より前）
- `[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]`
- `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` delegate + `[MonoPInvokeCallback]` static メソッド
- delegate は `static readonly` フィールドで永続保持し GC 回収を防ぐ
- 共通 `event Action<TResult>?` + per-call `Action<TResult>?`
- 非対応プラットフォームは `Application.platform != RuntimePlatform.OSXPlayer` で早期失敗
- `Awake` に `else if (_instance != this) { Destroy(gameObject); return; }` の重複インスタンス破棄分岐がある

**注意（本計画では踏襲しない点）**: `MacShareManager.InvokeInOrder` は共通イベントと per-call callback を**1 つの try/catch で包んでいる**ため、共通イベントが例外を投げると per-call が呼ばれない。本計画は `IosClipboardManager.cs:591-611` の**分離した try/catch** を採る（5.6.7）。

### 2.3 Clipboard Manager のパターン（`Clipboard/IosClipboardManager.cs`）

macOS 版が踏襲すべき、Share には無い仕組み:

- **単一実行ガード（single-flight）**: C ABI にリクエスト ID が無いため、同一操作の同時実行は結果を区別できない。`HashSet<string> s_inFlight` で操作ごとに 1 件だけ許可し、2 件目は即失敗
- **ガードチェーン `TryStartOperation`**: メインスレッド → 破棄済み → 引数 → プラットフォーム → 単一実行 の順
- **破棄後の tombstone（`IsTerminated`）**: `OnDestroy` 後は全操作を拒否し、遅延コールバックを破棄する
- **`RunDestroyCleanup`**: teardown の例外境界を `internal static` の純粋関数に切り出して EditMode で検証
- **監視の世代管理**: context を持てない static コールバックへ「どの登録に責任を持つか」を渡す
- **`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` による static リセット**
- **`#if UNITY_EDITOR` のテストシーム群**: `ResetForTests` / `BridgeAvailableOverrideForTests` に加え、`CompleteOperationForTests` / `CompleteObservationControlForTests` / `CompleteReadForTests` / `CompleteSnapshotForTests` / `DeliverChangeEventForTests` / `IsInFlightForTests` / `HasChangeRegistrationForTests` / `PendingObservationGenerationForTests` / `HasAnyPendingCallbackForTests` / `InFlightCountForTests`
  - **これらは PlayMode から駆動する**（EditMode では `UnityMainThreadDispatcher` が flush されず、Manager インスタンスも作れない）
- **`[MonoPInvokeCallback]` 本体は narrow guard 内、実処理は `HandleXxxCallback` として guard 外**に切り出す。この分離が無いと Editor 用シームがコンパイルできない
- 機微情報のため、値ではなく shape / count / flag のみをログに出す

### 2.4 再利用できる既存資産

| 型 | 現状 | 判断 |
| --- | --- | --- |
| `Clipboard/IosClipboardJsonReader.cs`（`JsonValue` / `JsonValueKind` / `JsonBase64Status` を含む） | `#if UNITY_IOS \|\| UNITY_EDITOR` | **共有化する**（4.2）。`ClipboardJsonReader` へ改名しガードを広げる |
| `Clipboard/ClipboardOperationResult.cs` | プラットフォームガード無し。Android から利用（`Operation` / `IsSuccess` / `ErrorMessage`、errorCode なし） | **流用しない**。macOS は数値 errorCode を返すため別型 |
| `Clipboard/ClipboardReadResult.cs`（`ClipItem` / `ClipContents`） | プラットフォームガード無し。Android の ClipData モデル | **流用しない**。macOS は UTI → bytes の representation モデル |
| `Clipboard/Ios*` 各型 | iOS 専用ガード、文字列 errorCode | **流用しない**。命名・エラー表現ともに不一致 |
| `Common/UnityMainThreadDispatcher` | 共通 | **そのまま使う** |

- 既存の `Ios*` / `Android*` 型に macOS 用の分岐を足さない。プラットフォームごとに独立した型を持つのが本パッケージの既存方針

### 2.5 テストアセンブリ（確認済み・変更不要）

- `Runtime/AssemblyInfo.cs` が `NativeToolkit.Runtime.Tests` と `NativeToolkit.Runtime.PlayModeTests` の**両方**に `InternalsVisibleTo` を与えている
- `Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef` / `Tests/PlayMode/NativeToolkit.Runtime.PlayModeTests.asmdef` はどちらも `NativeToolkit.Runtime` を参照済み
- asmdef の追加・変更は不要。`AssemblyInfo.cs` の既存コメントが `IosClipboardManager.ResetForTests` を名指ししているため、`MacClipboardManager.ResetForTests` 追加時にコメントのみ追記する可能性がある

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

- 関数名は**ネイティブシンボルに一致させる**（既存 `MacNotificationManager` の方針。あちらは `NotificationShow` などの PascalCase シンボルに一致させており、clipboard は結果として camelCase になる）
- `clipboardStopObserving` のみ teardown で `null` を渡すため nullable（1.2.4）

### 3.2 delegate 宣言

**delegate 型の宣言は narrow guard の外に置く**（`static readonly` フィールドと `[MonoPInvokeCallback]` 本体だけが guard 内）。既存 `MacShareManager.cs:58-59` / `IosClipboardManager.cs:193-205` と同じ配置。

```csharp
// The C header declares isSuccess as Objective-C BOOL (1 byte: _Bool on arm64,
// signed char on x86_64). C# marshals a bare bool as a 4-byte Win32 BOOL by
// default, so the width is pinned explicitly.
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardCallback(
    [MarshalAs(UnmanagedType.I1)] bool isSuccess,
    long errorCode,
    string? errorMessage);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardJsonCallback(
    [MarshalAs(UnmanagedType.I1)] bool isSuccess,
    string? json,
    long errorCode,
    string? errorMessage);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ClipboardChangeCallback(string? eventJson);
```

- `errorCode` は `NSInteger`（64bit）なので `long`。narrow は `MacClipboardErrorInfo.Create` の 1 箇所だけで行う
- 永続 delegate は `static readonly` で 15 個 + 変更通知 1 個。命名は `s_<operation>Delegate`（`s_copyDelegate` / `s_startObservingDelegate` ほか）、変更通知のみ `s_changeDelegate`

---

## 4. 変更ファイル一覧

`.meta` は Unity が自動生成するため新規作成しない。既存 `.meta` の**移動**は 4.2 のとおり行う。

### 4.1 新規作成（Runtime）— 17 ファイル

すべて `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/` 配下。
ガードは `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`。namespace は `JonghyunKim.NativeToolkit.Runtime.Clipboard`。

| # | ファイル | 内容 |
| --- | --- | --- |
| 1 | `MacClipboardPayloads.cs` | `MacPasteboardScopeKind` / `MacPasteboardScope` / `MacPasteboardCreationRequestKind` / `MacPasteboardCreationRequest` / `MacPasteboardOwnership` / `MacClipboardContentItem` / `MacClipboardContent` / `MacClipboardCopyOptions` / `MacClipboardDetectionPattern` / `MacClipboardMetadataType` / `MacClipboardAccessBehavior` / `MacClipboardTypes` / `MacClipboardDetectionPatternExtensions`（internal, rawValue 変換） |
| 2 | `MacClipboardErrorInfo.cs` | `MacClipboardErrorInfo`（`int Code` / `string Message`）と `MacClipboardErrorCodes`（ネイティブ 1301-1599 と C# 9001-9007 の全定数） |
| 3 | `MacClipboardConstants.cs` | `MacClipboardOperations`（操作名 15 + `ObservationControlKey`）と `MacClipboardLimits`（サイズ上限・既定監視間隔）。**Manager から独立させ、段 2 が単独でコンパイルできるようにする**（5.2.1 / 5.2.2） |
| 4 | `MacClipboardJsonBuilder.cs` | 7 種の入力 JSON を組み立てる `public static` メソッド群 |
| 5 | `MacClipboardJsonParser.cs` | 12 種の出力（出力専用 9 + 共用 2 + イベント 1）を結果型へ変換する `internal static` メソッド群 |
| 6 | `MacClipboardOperationResult.cs` | 値を返さない操作（removePasteboard / startObserving / stopObserving） |
| 7 | `MacClipboardOwnershipResult.cs` | copy / append。`Operation` を持つ |
| 8 | `MacClipboardReadResult.cs` | `MacClipboardItem`（読み出し item） / `MacClipboardReadContents`（`ChangeCount` + `Items`） / `MacClipboardReadResult` の 3 型 |
| 9 | `MacClipboardReadDataResult.cs` | `byte[]? Data`（型不在は成功 + null） |
| 10 | `MacClipboardSnapshotResult.cs` | `MacClipboardSnapshot` と結果型 |
| 11 | `MacClipboardChangeCountResult.cs` | `clear` の結果 |
| 12 | `MacPasteboardScopeResult.cs` | `createPasteboard` の結果 |
| 13 | `MacClipboardDetectionResults.cs` | `MacClipboardDetectedLink` / `MacClipboardLabeledValue` / `MacClipboardPostalAddress` / `MacClipboardCalendarEvent` / `MacClipboardShipmentTracking` / `MacClipboardFlightNumber` / `MacClipboardMoneyAmount` / `MacClipboardDetectedValues` / **`MacClipboardDetectedMetadata`** と、patterns / values / metadata の 3 結果型 |
| 14 | `MacClipboardAccessBehaviorResult.cs` | `accessBehavior` の結果 |
| 15 | `MacClipboardForegroundChangeResult.cs` | `checkForegroundChange` の結果 |
| 16 | `MacClipboardChangeEvent.cs` | 監視イベント（結果型ではない） |
| 17 | `MacClipboardManager.cs` | Manager 本体 |

内訳: payload / 定数 / エラー 3 ファイル、Builder / Parser 2 ファイル、**結果型 12 種を 10 ファイル**（`MacClipboardDetectionResults.cs` が 3 結果型を持つ）、イベント 1 ファイル、Manager 1 ファイル。

### 4.2 既存変更（Runtime）

| ファイル | 変更内容 |
| --- | --- |
| `Clipboard/IosClipboardJsonReader.cs` → `Clipboard/ClipboardJsonReader.cs` | **`git mv` で `.cs` と `.cs.meta` を対で移動する**（GUID を保存するため。移動であって新規作成ではないので `common.md` の「`.meta` を作らない」ルールに抵触しない）。static クラス名を `ClipboardJsonReader` に改名し、ガードを `#if UNITY_IOS \|\| UNITY_STANDALONE_OSX \|\| UNITY_EDITOR` へ拡張する。**クラス `<summary>` の iOS 固有記述**（`loadItem` is polymorphic on `kind` など）をプラットフォーム中立に書き直す。`JsonValue` / `JsonValueKind` / `JsonBase64Status` は既に接頭辞なしの共有型なので変更しない |
| `Clipboard/IosClipboardJsonParser.cs` | `IosClipboardJsonReader.` 参照 2 箇所を `ClipboardJsonReader.` に置換 |

- **改名は確定事項**（v1 では要検証扱いだった）。macOS 版パーサが一級の利用者になるため `Ios` 接頭辞が実体と食い違う。本パッケージは共有型を接頭辞なしで置く方針（`ClipboardOperationResult` / `ClipboardReadResult` はいずれもプラットフォームガードを持たない）
- 識別子の置換件数（実測）: Runtime 3 箇所（`IosClipboardJsonParser.cs` 2 + クラス宣言 1）、Tests 42 箇所

### 4.3 新規作成（Tests）— 5 ファイル

すべて `#if UNITY_STANDALONE_OSX || UNITY_EDITOR` でガードする（既存 `MacShareManagerIntegrationTests.cs:3` と同形）。

`Tests/Runtime/`（EditMode）:

| ファイル | 内容 |
| --- | --- |
| `MacClipboardJsonBuilderTests.cs` | 7 種の入力 JSON の形・キー省略・base64・エスケープ・カルチャ非依存性 |
| `MacClipboardJsonParserTests.cs` | 12 種の出力の解析、明示 null と欠落の区別、トップレベル配列、不正 JSON、base64 上限 |
| `MacClipboardResultTests.cs` | 結果型の不変条件、`MacClipboardErrorInfo.Create` の正規化と narrow、payload factory の `ArgumentException`、rawValue 変換表の往復 |
| `MacClipboardManagerDispatchTests.cs` | `InvokeInOrder` / `TryBeginOperation` / `EndOperation` / `RunDestroyCleanup` の**純粋関数のみ**。Manager インスタンスは生成しない |

`Tests/PlayMode/`:

| ファイル | 内容 |
| --- | --- |
| `MacClipboardManagerIntegrationTests.cs` | ガードチェーンの 6 段、dispatch 順序、監視の世代管理、tombstone、完了シーム経由の結果配送 |

### 4.4 既存変更（Tests）

| ファイル | 変更内容 |
| --- | --- |
| `Tests/Runtime/IosClipboardJsonReaderTests.cs` | `IosClipboardJsonReader.` → `ClipboardJsonReader.` の識別子置換 40 箇所。ファイル名は iOS 由来の網羅ケースを保つため据え置く |
| `Tests/Runtime/IosClipboardJsonBuilderTests.cs` | 同置換 2 箇所 |

### 4.5 非変更（参照のみ・確認済み）

| ファイル | 理由 |
| --- | --- |
| `Runtime/Common/UnityMainThreadDispatcher.cs` | そのまま使う |
| `Runtime/Clipboard/Android*` / `Ios*`（Reader / Parser を除く） | プラットフォーム独立の型を維持する |
| `Runtime/Share/Mac*` / `Runtime/Notification/Mac*` / `Runtime/Dialog/MacDialogManager.cs` | パターン参照のみ |
| `Runtime/AssemblyInfo.cs` | `InternalsVisibleTo` は両テストアセンブリに既存。**変更不要**（コメントの追記のみ検討） |
| `Tests/Runtime/*.asmdef` / `Tests/PlayMode/*.asmdef` | いずれも `NativeToolkit.Runtime` を参照済み。**変更不要** |
| `Runtime/NativeToolkit.Runtime.asmdef` | 参照追加は不要 |
| `Plugins/macOS/unity-mac-native-toolkit-1.3.0.xcframework` | 15 関数が既にエクスポート済み。差し替え不要（ただし 0.1 の版対応確認は必要） |
| `package.json` | バージョン更新はリリース工程（`release` スキル）で行う |

### 4.6 対象外

- `Runtime/UI/macOS/Clipboard/`（ExampleController）、UXML / USS、サンプルシーン、`NativeToolkitSampleNavigator` への導線追加、`*SampleSceneWiringTests` → `design-sample-scene` スキル
- マニュアル → `write-manual` スキル

---

## 5. 実装詳細

すべて namespace `JonghyunKim.NativeToolkit.Runtime.Clipboard`（既存 `Ios*` / `Android*` と同一）。

**新規 17 ファイルはすべて 1 行目に `#nullable enable` を置く**（既存 `Ios*` / `Android*` 全ファイルと同じ）。本計画書のシグネチャはすべて nullable 有効を前提に書かれている。

`csharp.md` の XML ドキュメントコメントのルールは**逸脱しない**。public メンバには英語の XML コメントを必ず付ける。逸脱するのはログのみ（5.6.11）。

### 5.1 `MacClipboardPayloads.cs`

```csharp
public enum MacPasteboardScopeKind { General, Named, Unique }

public sealed class MacPasteboardScope
{
    public MacPasteboardScopeKind Kind { get; }
    public string? Name { get; }                 // General は null
    public static MacPasteboardScope General { get; }
    public static MacPasteboardScope Named(string name);   // 空白名は ArgumentException
    public static MacPasteboardScope Unique(string name);  // 空白名は ArgumentException
}

public enum MacPasteboardCreationRequestKind { Named, Unique }

public sealed class MacPasteboardCreationRequest
{
    public MacPasteboardCreationRequestKind Kind { get; }
    public string? Name { get; }
    public static MacPasteboardCreationRequest Unique { get; }
    public static MacPasteboardCreationRequest Named(string name);  // 空白名は ArgumentException
}

public sealed class MacPasteboardOwnership
{
    public MacPasteboardScope Scope { get; }
    public int ChangeCount { get; }
}

public sealed class MacClipboardContentItem          // 入力側（v1 の MacClipboardItem を改名）
{
    public IReadOnlyDictionary<string, byte[]> Representations { get; }   // 常に非 null
    public static MacClipboardContentItem FromRepresentations(IReadOnlyDictionary<string, byte[]> representations);
    public static MacClipboardContentItem PlainText(string text);        // public.utf8-plain-text, UTF-8
    public static MacClipboardContentItem Html(string html, string? plainFallback = null);
    public static MacClipboardContentItem Url(string url);               // public.url
    public static MacClipboardContentItem Data(string utType, byte[] bytes);
}

public sealed class MacClipboardContent
{
    public IReadOnlyList<MacClipboardContentItem> Items { get; }         // 常に非 null
    public static MacClipboardContent Single(MacClipboardContentItem item);
    public static MacClipboardContent Multiple(IReadOnlyList<MacClipboardContentItem> items);
    public static MacClipboardContent PlainText(string text);
}

public sealed class MacClipboardCopyOptions
{
    public bool LocalOnly { get; }
    public static MacClipboardCopyOptions PrivacyPreservingDefault { get; }  // localOnly: true
    public static MacClipboardCopyOptions Create(bool localOnly);
}

public enum MacClipboardDetectionPattern { /* 5.1.1 の 11 件 */ }
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

- 命名の由来: `IosClipboardItem` は**読み出し結果**の型なので、macOS も出力側を `MacClipboardItem`（5.5）、入力側を `MacClipboardContentItem` とする。プラットフォーム間で同じ語が逆の意味を持たないようにする
- `PrivacyPreservingDefault` は `IosClipboardCopyOptions.PrivacyPreservingDefault` に合わせる。`Default` だと「localOnly が既定で true」という意図が名前から失われる
- ネイティブが検証する値（サイズ超過、不正 UTI、空 item）は C# で再検証しない。C# が投げるのは**呼び出し側のバグ**（factory への空白名）だけ
- **pasteboard 名の空白チェックは決定 1 の例外である。** ネイティブは `!name.isEmpty` しか見ないため `" "` が素通りして pasteboard が実際に作られてしまう（1.4）。factory の `string.IsNullOrWhiteSpace` が**唯一の防波堤**であり、`ArgumentException` を投げる

#### 5.1.1 rawValue 対応表（`MacClipboardDetectionPatternExtensions`）

置き場所は `MacClipboardPayloads.cs` の `internal static class MacClipboardDetectionPatternExtensions`（`IosClipboardPayloads.cs:463-509` の `IosClipboardDetectionPatternExtensions` と同形）。`RawValues` 辞書 + `ToRawValue` / `TryParse` を持つ。

**macOS の rawValue は複数形で、iOS の単数形と異なる。iOS からコピーしないこと。**

| C# enum | macOS rawValue |
| --- | --- |
| `ProbableWebUrl` | `probableWebURL` |
| `ProbableWebSearch` | `probableWebSearch` |
| `Number` | `number` |
| `Links` | `links` |
| `PhoneNumbers` | `phoneNumbers` |
| `EmailAddresses` | `emailAddresses` |
| `PostalAddresses` | `postalAddresses` |
| `CalendarEvents` | `calendarEvents` |
| `ShipmentTrackingNumbers` | `shipmentTrackingNumbers` |
| `FlightNumbers` | `flightNumbers` |
| `MoneyAmounts` | `moneyAmounts` |

`MacClipboardAccessBehavior` の rawValue:

| C# enum | rawValue |
| --- | --- |
| `Default` | `default` |
| `Ask` | `ask` |
| `AlwaysAllow` | `alwaysAllow` |
| `AlwaysDeny` | `alwaysDeny` |
| `Unavailable` | `unavailable` |
| `Unknown` | （rawValue なし。未知文字列の受け皿） |

`MacClipboardMetadataType.ContentType` ⇔ `contentType`。

**送信時に 1 値でも未知なら、ネイティブは要求全体を拒否する。** C# は enum からしか生成しないため未知値は発生しない。

### 5.2 `MacClipboardErrorInfo.cs` と `MacClipboardConstants.cs`

`MacClipboardConstants.cs` は `MacClipboardErrorInfo.cs` の下位ではなく、4.1 #3 の独立したファイルである（D-10）。

#### 5.2.0 `MacClipboardErrorInfo.cs`

```csharp
public readonly struct MacClipboardErrorInfo
{
    public const string UnknownErrorMessage = "An unknown clipboard error occurred.";

    public int Code { get; }         // ネイティブ 1301/1302/1501-1599、または C# 9001-9007
    public string Message { get; }   // 空にならない（正規化済み）

    /// Unity ブリッジ層が生成したコードかどうか。
    /// ネイティブの BridgeError(1301/1302) とは別物である点に注意。
    public bool IsManagedCode => Code >= 9000;

    public static MacClipboardErrorInfo Create(long code, string? message);
}

public static class MacClipboardErrorCodes
{
    // ネイティブ（BridgeError）
    public const int ParseFailed = 1301;
    public const int ContractViolation = 1302;

    // ネイティブ（ClipboardError）— 1.5 の表と 1:1 で全件を定義する。省略しない
    public const int EmptyContent = 1501;
    public const int EmptyRepresentations = 1502;
    public const int EmptyDetectionPatterns = 1503;
    public const int InvalidTypeIdentifier = 1504;
    public const int InvalidPasteboardName = 1505;
    public const int ContentTooLarge = 1506;
    public const int PasteboardUnavailable = 1507;
    public const int CannotReleaseStandardPasteboard = 1508;
    public const int WriteRejected = 1509;
    public const int AppendRejected = 1510;
    public const int OwnershipLost = 1511;
    public const int EmptyTypeFilter = 1512;
    public const int DetectionUnavailable = 1513;
    public const int DetectionDenied = 1514;
    public const int DetectionFailed = 1515;
    public const int PasteLoadFailed = 1521;      // C ABI 未公開。値域の完全性のため定義
    public const int PasteLoadTimedOut = 1522;    // 同上
    public const int InvalidConfiguration = 1523;
    public const int Cancelled = 1524;
    public const int Unknown = 1599;

    // Unity ブリッジ層のみが返すコード
    public const int Busy = 9001;
    public const int BridgeUnavailable = 9002;
    public const int MainThreadRequired = 9003;
    public const int ManagerDestroyed = 9004;
    public const int InvalidRequest = 9005;        // null 引数（ネイティブに対応コードが無い）
    public const int ResponseParseFailed = 9006;
    public const int RequestTooLarge = 9007;       // managed 側 OOM 防止の上限（5.6.3）
}
```

- 9000 番台を選ぶ理由: ネイティブは現在 1001-1999 のみを割り当てている（notification 1001-1205 / 1999、bridge 1301-1302、share 1401-1499、clipboard 1501-1599）
- **1599 の定義は `MacClipboardErrorCodes.Unknown` の 1 箇所だけに置く。** `MacClipboardErrorInfo` 側で重複定義しない
- `Create(long code, string? message)` が narrow の唯一の場所。**`code < int.MinValue || code > int.MaxValue` を明示的に判定し、外れた場合のみ `Unknown`(1599) に落とす。** unchecked な `(int)` キャストは上位ビットを捨てて折り返す（`0x1_0000_0001L` → `1`）ため、キャストだけでは値域外を検出できない
- `message` が null / 空白のとき `UnknownErrorMessage` に正規化する

#### 5.2.1 `MacClipboardConstants.cs` — 操作名と single-flight キー

`Operation` は公開契約であり、6.1 の `{operation}` メッセージ・6.3 の操作名・5.6.5 の共有キーがすべてこの表に依存する。**Manager ではなくこのファイルに置く**（段 2 で確定させ、段 3 の Manager に依存させないため）。

```csharp
public static class MacClipboardOperations
{
    public const string Copy = "copy";
    public const string Append = "append";
    public const string Read = "read";
    public const string ReadData = "readData";
    public const string Snapshot = "snapshot";
    public const string Clear = "clear";
    public const string CreatePasteboard = "createPasteboard";
    public const string RemovePasteboard = "removePasteboard";
    public const string DetectPatterns = "detectPatterns";
    public const string DetectValues = "detectValues";
    public const string DetectMetadata = "detectMetadata";
    public const string AccessBehavior = "accessBehavior";
    public const string StartObserving = "startObserving";
    public const string StopObserving = "stopObserving";
    public const string CheckForegroundChange = "checkForegroundChange";

    /// StartObserving と StopObserving が共有する single-flight キー。
    public const string ObservationControlKey = "observation";
}
```

| 操作 | `Operation` 値 | single-flight キー |
| --- | --- | --- |
| Copy | `copy` | `copy` |
| Append | `append` | `append` |
| Read | `read` | `read` |
| ReadData | `readData` | `readData` |
| Snapshot | `snapshot` | `snapshot` |
| Clear | `clear` | `clear` |
| CreatePasteboard | `createPasteboard` | `createPasteboard` |
| RemovePasteboard | `removePasteboard` | `removePasteboard` |
| DetectPatterns | `detectPatterns` | `detectPatterns` |
| DetectValues | `detectValues` | `detectValues` |
| DetectMetadata | `detectMetadata` | `detectMetadata` |
| GetAccessBehavior | `accessBehavior` | `accessBehavior` |
| StartObserving | `startObserving` | **`observation`** |
| StopObserving | `stopObserving` | **`observation`** |
| CheckForegroundChange | `checkForegroundChange` | `checkForegroundChange` |

- `Operation` と single-flight キーが一致しないのは observation の 2 操作だけ。結果には `Operation`（どちらを呼んだか）が載り、in-flight 集合には共有キーが入る

#### 5.2.2 `MacClipboardConstants.cs` — 上限と既定値

```csharp
public static class MacClipboardLimits
{
    /// 送信 payload（representations の合計バイト数）の上限。暫定値。
    public const long MaxRequestBytes = 32L * 1024 * 1024;

    /// 受信した 1 representation の base64 デコード後の上限。暫定値。
    public const long MaxResponseBytesPerRepresentation = 32L * 1024 * 1024;

    /// 監視のポーリング間隔の既定値。ネイティブの defaultObservationInterval と一致させる。
    public const double DefaultObservationInterval = 0.5;
}
```

- **Parser（段 2）が `MaxResponseBytesPerRepresentation` を参照するため、Manager（段 3）に置いてはならない**
- `DefaultObservationInterval` は `StartObserving` の既定引数に使う。`const` なので既定引数として合法
- 契約と実測の扱いは 5.6.3

### 5.3 `MacClipboardJsonBuilder.cs`

`IosClipboardJsonBuilder` と同じ手書きシリアライザ方式（optional キーの制御が必要なため）。`public static`。

| メソッド | 戻り値 |
| --- | --- |
| `BuildScopeJson(MacPasteboardScope scope)` | `{"kind":...,"name":...}`。General は `name` キーを出さない |
| `BuildContentJson(MacClipboardContent content)` | `{"items":[{"representations":{...}}]}`。bytes は base64。**`representations` のキーは序数順にソートする**（`IReadOnlyDictionary` の列挙順は保証されないため、ソートしないと 7.1 のテストがゴールデン文字列で書けない。ネイティブ側は辞書としてデコードするので順序に依存しない） |
| `BuildOptionsJson(MacClipboardCopyOptions? options)` | `null` のとき **C# の `null` を返す**（空文字ではなく） |
| `BuildOwnershipJson(MacPasteboardOwnership ownership)` | `{"scope":{...},"changeCount":n}` |
| `BuildCreateRequestJson(MacPasteboardCreationRequest request)` | `{"kind":"named","name":"..."}` / `{"kind":"unique"}` |
| `BuildMatchingTypesJson(IReadOnlyList<string>? types)` | `null` のとき `null`（フィルタ無し）。空リストは `[]` を出しネイティブの 1512 に委ねる |
| `BuildPatternsJson(IReadOnlyCollection<MacClipboardDetectionPattern> patterns)` | `["links",...]`。rawValue へ変換しソートする。空でもそのまま `[]` を出しネイティブの 1503 に委ねる |

- 数値は `CultureInfo.InvariantCulture` で書式化する
- 文字列は `"`・`\`・制御文字（U+0000〜U+001F）を JSON エスケープする。**非 ASCII は生の UTF-8 のまま出力し、`\uXXXX` へはエスケープしない**（JSON 仕様上有効で、ペイロードが小さくなる）。7.1 のテストはこの期待値で書く
- **ログを出さない**（5.6.11）
- base64 化はメモリを 4/3 に膨らませる。32 MiB の payload は約 43 MB の managed string になる。上限チェックは Manager 側で行う（5.6.3）

### 5.4 `MacClipboardJsonParser.cs`

`ClipboardJsonReader.Parse(json)` で `JsonValue` を得てから結果型へ変換する。`internal static`。

| # | メソッド | 入力 | 出力 |
| --- | --- | --- | --- |
| 1 | `TryParseOwnership` | `OwnershipJson`（共用） | `MacPasteboardOwnership` |
| 2 | `TryParseScopeResult` | `ScopeResultJson` | `MacPasteboardScope` |
| 3 | `TryParseReadResult` | `ReadResultJson` | `MacClipboardReadContents` |
| 4 | `TryParseReadData` | `ReadDataJson` | `byte[]?`（`null` は成功） |
| 5 | `TryParseSnapshot` | `SnapshotJson` | `MacClipboardSnapshot` |
| 6 | `TryParseChangeCount` | `ChangeCountJson` | `int` |
| 7 | `TryParseBool` | `BoolJson` | `bool` |
| 8 | `TryParsePatterns` | `PatternsJson`（共用・**トップレベル配列**） | `IReadOnlyList<MacClipboardDetectionPattern>` |
| 9 | `TryParseDetectedValues` | `DetectedValuesJson` | `MacClipboardDetectedValues` |
| 10 | `TryParseDetectedMetadata` | `DetectedMetadataJson` | `MacClipboardDetectedMetadata` |
| 11 | `TryParseAccessBehavior` | `AccessBehaviorJson` | `MacClipboardAccessBehavior` |
| 12 | `TryParseChangeEvent` | `ChangeEventJson` | `MacClipboardChangeEvent` |

パース規約:

- **`null` と「キー欠落」の扱い**
  - `ReadDataJson.data` / `DetectedValuesJson` の optional / `DetectedMetadataJson.contentTypeIdentifier` は**明示的 null で出力される**ため、`null` は「要求したが未検出」、キー欠落はスキーマ不一致として失敗させる
  - **例外**: `ScopeJson.name` は general でキーごと省略される。`kind == "general"` のとき `name` の欠落を許容する（`OwnershipJson` / `ScopeResultJson` / `ChangeEventJson` の入れ子 scope も同じ）
- 未知の pattern 文字列は無視する（前方互換）。未知の accessBehavior は `Unknown` にマップする
- base64 のデコードは `JsonValue.TryGetBase64Bytes(maxDecodedLength, out bytes)` を使い、`JsonBase64Status.TooLarge` は失敗（9006）にする。`maxDecodedLength` は `MacClipboardLimits.MaxResponseBytesPerRepresentation`（5.2.2）を渡す
- 日付は `DateTimeOffset.TryParse` を `InvariantCulture` + `AssumeUniversal | AdjustToUniversal` で行う。失敗時はそのフィールドのみ `null` とし、イベント全体は落とさない
- パース失敗時は `MacClipboardErrorCodes.ResponseParseFailed`(9006) の失敗結果を返す。**ネイティブが成功と言った結果を C# が解釈できない状態を、成功として通さない**
- ログを出さない（5.6.11）

### 5.5 結果型の仕様

#### 5.5.1 共通規約

型の種別:

- **結果型（`*Result`）は `public readonly struct`。** `Error` を `MacClipboardErrorInfo?` と書けるのは struct 前提であり、class にすると `Error == null` の意味が変わる（`IosClipboardOperationResult` ほか iOS の全結果型と同じ）
- **値を運ぶ入れ子型は `sealed class`**: `MacClipboardItem` / `MacClipboardReadContents` / `MacClipboardSnapshot` / `MacClipboardChangeEvent` と、5.5.2 の検出結果表の 9 型（`MacClipboardDetectedLink` / `MacClipboardLabeledValue` / `MacClipboardPostalAddress` / `MacClipboardCalendarEvent` / `MacClipboardShipmentTracking` / `MacClipboardFlightNumber` / `MacClipboardMoneyAmount` / `MacClipboardDetectedValues` / `MacClipboardDetectedMetadata`）
- `MacClipboardErrorInfo` も `public readonly struct`

不変条件（payload の種別で 3 分岐する）:

**結果型が直接持つ payload プロパティ**の 3 分岐:

| payload の種別 | 失敗時 | 該当 |
| --- | --- | --- |
| 参照型 | `null` | `Ownership` / `Contents` / `Snapshot` / `Scope` / `Values` / `Metadata` |
| 値型（`int` / `bool` / enum） | **既定値**（`0` / `false` / `Unknown`） | `ChangeCount` / `Changed` / `Behavior` |
| コレクション | **常に非 null**（失敗時は空） | `MacClipboardDetectedPatternsResult.Patterns` のみ |

**入れ子オブジェクトが持つコレクション**は上の分岐とは別の規約に従う:

- `MacClipboardReadContents.Items` / `MacClipboardSnapshot.ItemTypes` / `MacClipboardSnapshot.MatchingItemIndexes` / `MacClipboardDetectedValues` のコレクション 9 件は、**その入れ子オブジェクトが存在する限り常に非 null**（要素が無ければ空）
- 失敗時は入れ子オブジェクト自体が `null` になるので、これらのプロパティはそもそも存在しない。「失敗時も非 null」ではないので混同しないこと

- `IsSuccess == true` ⇔ `Error == null`（例外なし）
- `MacClipboardReadDataResult.Data` は**成功 かつ `null`** を許す唯一のプロパティ（要求した UTI が pasteboard に無い場合）
- 値型 payload は失敗時に既定値になるため、**`IsSuccess` を見ずに値を読んではならない**。XML コメントに明記する
- **nullable にするのは「値が無いこと」に意味があるフィールドだけ**で、各プロパティの XML コメントにその意味を書く
- `Failure` ファクトリは `MacClipboardErrorInfo.Create` を通して code / message を正規化する
- `Operation` を持つのは**複数の操作が同じ結果型を共有する場合**（`MacClipboardOperationResult` / `MacClipboardOwnershipResult`）。1 型 1 操作の結果型は持たない。値は `MacClipboardOperations`（5.2.1）の定数を使う

結果型は **12 種**（Operation / Ownership / Read / ReadData / Snapshot / ChangeCount / ScopeResult / AccessBehavior / ForegroundChange / DetectedPatterns / DetectedValues / DetectedMetadata）。`MacClipboardDetectionResults.cs` が 3 種を持つため **10 ファイル**に収まる。

#### 5.5.2 各型のプロパティ

| 型 | プロパティ | 備考 |
| --- | --- | --- |
| `MacClipboardOperationResult` | `string Operation` / `bool IsSuccess` / `MacClipboardErrorInfo? Error` | removePasteboard / startObserving / stopObserving |
| `MacClipboardOwnershipResult` | `string Operation` / `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `MacPasteboardOwnership? Ownership` | **copy と append が共有するため `Operation` は必須**。1511 が append 固有であることも判別できる |
| `MacClipboardItem` | `IReadOnlyDictionary<string, byte[]> Representations` | 読み出し item。常に非 null |
| `MacClipboardReadContents` | `int ChangeCount` / `IReadOnlyList<MacClipboardItem> Items` | Items は常に非 null。**iOS の `IosClipboardReadResult` はフラット構成だが、macOS は `changeCount` と `items` が ownership と同じ「その時点の pasteboard 状態」を表す 1 組の値なので入れ子にする**（`MacClipboardSnapshotResult` が `MacClipboardSnapshot` を入れ子にしているのと同じ理由） |
| `MacClipboardReadResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `MacClipboardReadContents? Contents` | |
| `MacClipboardReadDataResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `byte[]? Data` | **成功 かつ `Data == null` を許す唯一の型**（要求した UTI が pasteboard に無い場合。不正な UTI もここに落ちる） |
| `MacClipboardSnapshot` | `int ChangeCount` / `IReadOnlyList<IReadOnlyList<string>> ItemTypes` / `IReadOnlyList<int> MatchingItemIndexes` | いずれも常に非 null |
| `MacClipboardSnapshotResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `MacClipboardSnapshot? Snapshot` | |
| `MacClipboardChangeCountResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `int ChangeCount` | 失敗時は `ChangeCount == 0` |
| `MacPasteboardScopeResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `MacPasteboardScope? Scope` | unique pasteboard の生成名を得る唯一の手段 |
| `MacClipboardAccessBehaviorResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `MacClipboardAccessBehavior Behavior` | 失敗時は `Unknown` |
| `MacClipboardForegroundChangeResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `bool Changed` | 失敗時は `false` |
| `MacClipboardChangeEvent` | `MacPasteboardScope Scope` / `int ChangeCount` | イベントなので成否を持たない |

検出結果:

| 型 | プロパティ |
| --- | --- |
| `MacClipboardDetectedLink` | `string MatchedString` / `string Url` |
| `MacClipboardLabeledValue` | `string MatchedString` / `string Value` / `string? Label` — phoneNumbers と emailAddresses が共有。`Label` は検出器が付けなければ null |
| `MacClipboardPostalAddress` | `string MatchedString` / `string? Street` / `string? City` / `string? State` / `string? PostalCode` / `string? Country` |
| `MacClipboardCalendarEvent` | `string MatchedString` / `bool IsAllDay` / `DateTimeOffset? StartDate` / `string? StartTimeZoneIdentifier` / `DateTimeOffset? EndDate` / `string? EndTimeZoneIdentifier` |
| `MacClipboardShipmentTracking` | `string MatchedString` / `string Carrier` / `string TrackingNumber` |
| `MacClipboardFlightNumber` | `string MatchedString` / `string Airline` / `string FlightNumber` |
| `MacClipboardMoneyAmount` | `string MatchedString` / `string CurrencyCode` / `double Amount` |
| `MacClipboardDetectedValues` | `IReadOnlyList<MacClipboardDetectionPattern> Patterns` / `string? ProbableWebUrl` / `string? ProbableWebSearch` / `double? Number` / `IReadOnlyList<MacClipboardDetectedLink> Links` / `IReadOnlyList<MacClipboardLabeledValue> PhoneNumbers` / `IReadOnlyList<MacClipboardLabeledValue> EmailAddresses` / `IReadOnlyList<MacClipboardPostalAddress> PostalAddresses` / `IReadOnlyList<MacClipboardCalendarEvent> CalendarEvents` / `IReadOnlyList<MacClipboardShipmentTracking> ShipmentTrackingNumbers` / `IReadOnlyList<MacClipboardFlightNumber> FlightNumbers` / `IReadOnlyList<MacClipboardMoneyAmount> MoneyAmounts` — **コレクション 9 件はすべて常に非 null、スカラー 3 件は「該当パターンが一致しなかった」を表す nullable** |
| `MacClipboardDetectedMetadata` | `IReadOnlyList<MacClipboardMetadataType> MetadataTypes`（常に非 null） / `string? ContentTypeIdentifier` |
| `MacClipboardDetectedPatternsResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `IReadOnlyList<MacClipboardDetectionPattern> Patterns`（常に非 null） |
| `MacClipboardDetectedValuesResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `MacClipboardDetectedValues? Values` |
| `MacClipboardDetectedMetadataResult` | `bool IsSuccess` / `MacClipboardErrorInfo? Error` / `MacClipboardDetectedMetadata? Metadata` |

### 5.6 `MacClipboardManager.cs`

- `public class MacClipboardManager : MonoBehaviour`
- クラスガード: `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`（testing.md の A 群）
- P/Invoke・`[MonoPInvokeCallback]` 本体・永続 delegate フィールド: `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR`
- `private const string LogTag = "MacClipboardManager";`
- 既定監視間隔は `MacClipboardLimits.DefaultObservationInterval`（5.2.2）を使う。Manager 側に定数を重複定義しない
- Singleton は `MacShareManager` と同形。`Awake` は `_instance` の設定・`DontDestroyOnLoad`・**重複インスタンスの `Destroy(gameObject); return;`**・`s_mainThreadId` と `s_dispatcher` の捕捉・tombstone が立っている場合の `Debug.LogError` を行う

#### 5.6.1 Manager lifetime 契約

**明示的な破棄と再生成は非対応。** 破棄後の挙動を仕様として固定する。

| 事象 | 挙動 |
| --- | --- |
| `OnDestroy` 実行 | `s_isTerminated = true`（tombstone）。以降すべての操作を 9004 で拒否する |
| 破棄後に `Instance` を参照 | Unity の `==` により新しい GameObject が生成される。**しかし tombstone は解除されないため、全 API が 9004 を返す「死んだ Manager」になる。これは仕様である** |
| 破棄後に届いたネイティブコールバック | `DiscardIfTerminated` で捨てる（変更イベントも含む、5.6.8） |
| tombstone の解除 | Play セッション開始 / アプリ起動時の `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` のみ。**production から解除する手段は提供しない** |
| `Awake` が tombstone 済みで走った場合 | `Debug.LogError` で「破棄後に再生成された。すべての操作は拒否される」と記録する |

- `public static bool IsTerminated => s_isTerminated;` を**公開する**。呼び出し側が死んだ Manager を検出できるようにする
- tombstone が必要な理由: ネイティブ ABI にリクエスト ID も lifetime ID も無いため、遅延コールバックと新規コールバックを区別できない。tombstone があれば「破棄後に新規操作は開始されていない」ことが保証され、捨てて安全になる（11 章）

#### 5.6.2 公開イベントと公開メソッド

操作名と single-flight キーは `MacClipboardOperations`（5.2.1）を参照する。Manager 側に文字列を重複定義しない。

共通イベント（常に発火）:

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
public event Action<MacClipboardChangeEvent>? ClipboardChanged;
```

公開メソッド:

```csharp
public void Copy(MacClipboardContent content, MacPasteboardScope? scope = null,
                 MacClipboardCopyOptions? options = null,
                 Action<MacClipboardOwnershipResult>? onResult = null);

public void Append(MacClipboardContent content, MacPasteboardOwnership ownership,
                   Action<MacClipboardOwnershipResult>? onResult = null);

public void Read(MacPasteboardScope? scope = null, Action<MacClipboardReadResult>? onResult = null);

public void ReadData(string utType, MacPasteboardScope? scope = null,
                     Action<MacClipboardReadDataResult>? onResult = null);

public void Snapshot(IReadOnlyList<string>? matchingTypes = null, MacPasteboardScope? scope = null,
                     Action<MacClipboardSnapshotResult>? onResult = null);

public void Clear(MacPasteboardScope? scope = null, Action<MacClipboardChangeCountResult>? onResult = null);

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
                           double intervalSeconds = MacClipboardLimits.DefaultObservationInterval,
                           Action<MacClipboardChangeEvent>? onChanged = null,
                           Action<MacClipboardOperationResult>? onStarted = null);

public void StopObserving(Action<MacClipboardOperationResult>? onResult = null);

public void CheckForegroundChange(MacPasteboardScope? scope = null,
                                  Action<MacClipboardForegroundChangeResult>? onResult = null);
```

callback 契約:

- **共通イベントは常に発火する。** 個別 callback の有無に関係なく、拒否経路（メインスレッド違反・破棄済み・引数不正・非対応プラットフォーム・多重呼び出し）でも発火する
- **共通イベントの取得元は経路で異なる**（決定 2）
  - **拒否経路**: 公開 API はすべて instance メソッドなので **`this` の event から取る**。`private static TryPassGuards`（5.6.4）へは iOS と同じ `Func<Action<TResult>?> commonSelector` 引数を使い、呼び出し側が `() => this.XxxCompleted` を渡す。`_instance` を経由してはならない
  - **通常経路**: `[MonoPInvokeCallback]` は static なので `_instance?.XxxCompleted` から取る。破棄後のコールバックは tombstone で捨てられるため、`_instance` が null でも取りこぼしにならない
  - field-like event は宣言型の内部から下位のデリゲートフィールドとして読めるため、この形は C# として合法。`this` の managed フィールド読みは破棄済み MonoBehaviour でも別スレッドからでも安全で、Unity の `==` オーバーロードを踏まない
- **決定 2 が保証する範囲（重要）**
  - 拒否結果は「**呼び出しに使われたその instance**」の共通イベントに発火する。`OnDestroy` で `_instance = null` にした後でも、呼び出し側が破棄前の参照を保持していれば、その購読者に確実に届く
  - **保証しないのは、破棄後に `Instance` から取り直した場合。** その `this` は新しく生成された別インスタンスで購読者がいないため、**共通イベントは誰にも届かない**（5.6.1 の「死んだ Manager」と同じ状態）。ただし**個別 callback（`onResult`）はこの経路でも発火する**ので、9004 を検出する手段は残る。`Awake` の重複インスタンス破棄経路で掴んだ複製インスタンスも同様
  - つまり決定 2 は「破棄後も必ず誰かに届く」保証ではなく、「**参照を保持している購読者を取りこぼさない**」保証である。XML コメントにこの範囲で書く
- **個別 callback は任意**。`IosClipboardManager` の per-call callback 方式に準拠する
- **dispatch 順序は 共通イベント → 個別 callback** で固定する。`InvokeInOrder` が唯一の発火経路。**変更イベント（`ClipboardChanged` と `onChanged`）も同じ順序・同じ経路を通る**
- 共通イベントと個別 callback は**それぞれ独立した try/catch** で包む。片方の例外がもう片方を止めない。例外はネイティブ呼び出し元へ漏らさない
- `workflow.md` は個別 callback を「last-registered wins」と規定するが、本設計は single-flight により pending が最大 1 件になるため後勝ちが発生しない。**規定より強い保証を満たす形の逸脱**である（iOS Clipboard と同じ）
- `scope` を `null` にすると `MacPasteboardScope.General` を使う。**ネイティブは `scopeJson` の省略を許さない**ため、C# 側が必ず general の JSON を組み立てて渡す
- `onChanged` は監視 1 回分の登録。ネイティブの `onChange` には常に永続 delegate を渡すため、C# の `onChanged` が `null` でも 1302 にはならず `ClipboardChanged` だけが発火する
- **`StartObserving` を再度呼ぶと、前回の `onChanged` 登録は黙って置き換わる。** XML コメントに明記する

#### 5.6.3 引数検証（決定 1）

**C# が行う事前検証は、ネイティブに対応するエラーコードが存在しないケースだけに限る。** ネイティブと同じ条件を C# でも検査すると、同一条件に 2 つのコードが割り当てられ「単一のエラー契約」が壊れるため。

| 操作 | 検証 | 返すコード | 理由 |
| --- | --- | --- | --- |
| Copy / Append | `content == null` | 9005 | null 参照は呼び出し側のバグ。ネイティブに対応コードが無い |
| Append | `ownership == null` | 9005 | 同上 |
| CreatePasteboard | `request == null` | 9005 | 同上 |
| RemovePasteboard | `scope == null` | 9005 | 同上 |
| DetectPatterns / DetectValues | `patterns == null` | 9005 | 同上（**空コレクションは検査しない**。ネイティブが 1503 を返す） |
| Copy / Append | representations の合計バイト数が `MacClipboardLimits.MaxRequestBytes` を超える | **9007** | base64 化で 4/3 に膨らみ managed 側で OOM しうる。ネイティブの 1506 とは目的も閾値も異なる独立した制約 |

サイズ検証は次のヘルパで行う（5.6.4 のヘルパ一覧にも掲載）。

```csharp
/// 合計バイト数が実効上限を超えていれば (RequestTooLarge, message) を、超えていなければ null を返す。
private static (int Code, string Message)? ValidateRequestSize(MacClipboardContent content);

/// 実効上限。Editor ではテストシームで差し替えられる（5.6.10）。
/// MacClipboardLimits.MaxRequestBytes は const なので、シームを効かせるには読み側の分岐が要る。
private static long EffectiveMaxRequestBytes()
{
#if UNITY_EDITOR
    return MaxRequestBytesOverrideForTests ?? MacClipboardLimits.MaxRequestBytes;
#else
    return MacClipboardLimits.MaxRequestBytes;
#endif
}
```

- **合計バイト数は `long` で積む**（`item.Representations.Sum(r => (long)(r.Value?.Length ?? 0))`。item 横断の合計も `long`）。`int` の `Sum` は checked で積算するため、まさに 9007 が防ごうとしている入力（1 GiB の representation を 3 つなど）で `OverflowException` を投げ、段階 3 から**公開 API 経由で呼び出し側へ例外が漏れる**
- **要素の null をエラーとして扱わない。** 0 バイトとして数え、拒否コードを割り当てない（`content.Items` の要素が null の場合も同様）。段階 3 で NRE を投げると公開 API が例外を伝播させ、「拒否は結果で返す」という 5.6.2 の契約を破るため
- null 要素は段階 5 の `Convert.ToBase64String(null)` が投げる `ArgumentNullException` として捕捉し、9002 に落とす。この設計により、7.2 は「値が null の `byte[]` を含む辞書」で**段階 5 の拒否経路を駆動できる**
- `#nullable enable` により `Representations` の値型 `byte[]` は非 null 注釈である。**null 要素は「注釈を抑止した呼び出し側のバグ」という位置づけ**で、正常系では起こらない。factory に null ガードを足さないこと（足すと段階 5 の駆動手段が失われる）。テスト側は `null!` を書いて意図的に注釈を破る
- 6.1 の 9007 メッセージには**実効上限**（`EffectiveMaxRequestBytes()` の値）を埋め込む。`ResetForTests()` は `MaxRequestBytesOverrideForTests` を null に戻す

**v1 から削除した事前検証**（すべてネイティブに委ねる）:

| 条件 | ネイティブが返すコード |
| --- | --- |
| `utType` が null / 空 | 1302 |
| `RemovePasteboard` の対象が標準 pasteboard | 1508（general だけでなく `font` / `ruler` / `find` / `drag` も網羅され、`Unique` で渡した場合も名前一致で判定される） |
| `intervalSeconds` が範囲外 | 1523 |
| `patterns` が空 | 1503 |

- 特に `patterns` の事前検証を外せる理由: v1 は「ネイティブの guard が patterns の不正を scope のエラーとして誤分類する」ことを根拠にしていたが、**C# は patterns JSON を enum から生成するため不正な JSON を送りえない**。誤分類は C# 経由では発生しない
- `MacPasteboardScope.Named("")` などの factory は従来どおり `ArgumentException` を投げる（結果ではなく例外。呼び出し側のバグ）

サイズ上限の契約（定数は 5.2.2 の `MacClipboardLimits`）:

- **`MacClipboardLimits.MaxRequestBytes` が利用者から見える実効上限である。** ネイティブの 100 MiB / 200 MiB には通常到達せず、1506 はほぼ返らない。XML コメントとマニュアルにはネイティブ値ではなくこの値を書く
- 両方とも暫定値。実機で所要時間とピークメモリを測って確定する（8 章 V-3）

受信側の制約（明記する）:

- **`MacClipboardLimits.MaxResponseBytesPerRepresentation` はデコード時に効くのであって、マーシャリング時には効かない。** 他アプリが 100 MiB の representation をコピーした状態で `Read` を呼ぶと、ネイティブは約 133 MB の base64 を含む JSON を返し、**マーシャリングの時点で約 266 MB の UTF-16 string が確保される**。上限を掛けてもこのメモリは減らせない
- これは C# 側では回避できないプラットフォームの制約である。大きい pasteboard が予想される場面では、payload を読まない `Snapshot` で `ItemTypes` を先に確認してから `Read` / `ReadData` するよう XML コメントで誘導する

#### 5.6.4 実行段階とエラーの帰属

操作を 8 段階に分け、各段階の失敗時にどの dispatch 経路を使うかを固定する。

**`TryStartOperation` を 2 つに分割する（iOS からの構造変更）。** iOS の `TryStartOperation` は 5 段を 1 本の `private static` で通し最後に in-flight を取得するため、その途中に JSON 構築を差し込めない。macOS 版は次の形にする。

```csharp
// 段階 1〜4。in-flight にも per-call スロットにも触れない。
private static bool TryPassGuards<TResult>(
    string operation,
    Action<TResult>? onResult,
    Func<Action<TResult>?> commonSelector,       // 呼び出し側が () => this.XxxCompleted を渡す
    Func<int, string, TResult> failure,
    Func<(int Code, string Message)?>? validate = null);

// 段階 6。true を返した時点から呼び出し側がマーカーを所有する。
internal static bool TryBeginOperation(HashSet<string> inFlight, string inFlightKey);
```

**段階 5・6 の拒否 dispatch は呼び出し側の責務になる。** そのため `commonSelector` と `failure` を**ローカル変数に持ち上げ**、`TryPassGuards` に渡したうえで段階 5・6 でも再利用する。インラインのラムダとして書くと、段階 5 に到達した時点で再利用できるものが手元に残らない。

呼び出し側の骨格（`Copy` の完全形。他の操作も同じ形）:

```csharp
public void Copy(MacClipboardContent content, MacPasteboardScope? scope = null,
                 MacClipboardCopyOptions? options = null,
                 Action<MacClipboardOwnershipResult>? onResult = null)
{
    const string op = MacClipboardOperations.Copy;

    // 段階 5・6 でも使うのでローカルに持ち上げる。
    Func<Action<MacClipboardOwnershipResult>?> commonSelector = () => this.OwnershipChanged;
    Func<int, string, MacClipboardOwnershipResult> failure =
        (code, message) => MacClipboardOwnershipResult.Failure(op, code, message);

    // 段階 1〜4。in-flight にも per-call スロットにも触れない。
    if (!TryPassGuards(op, onResult, commonSelector, failure,
                       validate: () => content == null
                           ? (MacClipboardErrorCodes.InvalidRequest, "content must not be null.")
                           : ValidateRequestSize(content)))
    {
        return;
    }

    Debug.Log($"[{LogTag}][{nameof(Copy)}] itemCount: {content.Items.Count}, " +
              $"hasScope: {scope != null}, hasOptions: {options != null}, hasCallback: {onResult != null}");

    // 段階 5。ここで失敗しても in-flight はまだ取得していないので拒否経路を使う。
    string contentJson, scopeJson;
    string? optionsJson;
    try
    {
        contentJson = MacClipboardJsonBuilder.BuildContentJson(content);
        scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
        optionsJson = MacClipboardJsonBuilder.BuildOptionsJson(options);
    }
    catch (Exception ex)
    {
        Debug.LogError($"[{LogTag}][{nameof(Copy)}] build: {ex.Message}");
        DispatchRejectedResult(
            failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
            commonSelector(), onResult);
        return;
    }

    // 段階 6。true を返した時点から呼び出し側がマーカーを所有する。
    if (!TryBeginOperation(s_inFlight, op))
    {
        Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
        DispatchRejectedResult(
            failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
            commonSelector(), onResult);
        return;
    }

    // 段階 7。ここから先の失敗は通常経路（EndOperation する）。
    s_onCopy = onResult;                                             // スロット規約は 5.6.12
    InvokeNative(op, op,
        nativeCall: () =>
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            clipboardCopy(contentJson, optionsJson, scopeJson, s_copyDelegate);
#endif
        },
        // Copy は MacClipboardOperationResult を返さないので、既定のフォールバックでは
        // 型違いの結果が別イベントへ流れ、s_onCopy も解放されない。必ず指定する。
        onNativeFailureResult: message => FireOwnershipResult(
            MacClipboardOwnershipResult.Failure(
                op, MacClipboardErrorCodes.BridgeUnavailable, message),
            op));
}
```

**`onNativeFailureResult` は `MacClipboardOperationResult` を返す 3 操作（removePasteboard / startObserving / stopObserving）以外では必須。** 省略すると既定のフォールバックが `MacClipboardOperationResult` を組み立てて `ClipboardOperationCompleted` に流し、呼び出し側の `onResult` は永久に呼ばれず per-call スロットもリークする。値を返す 12 操作すべてで指定すること。

ヘルパのシグネチャ（`IosClipboardManager` と同形。5.6.7 の `InvokeInOrder` が唯一の発火経路であることは変わらない）:

```csharp
/// 拒否経路。pending スロットにも in-flight マーカーにも触れない。
/// メインスレッドから呼ばれる前提で、dispatcher の null 判定に Unity の == を使う。
private static void DispatchRejectedResult<TResult>(
    TResult result, Action<TResult>? common, Action<TResult>? rejectedCallback);

/// 段階 1 専用の拒否経路。Unity の == を踏まないよう (object?)dispatcher == null で判定し、
/// 共通イベントの取得とログを Enqueue した closure の中で行う（5.6.6）。
private static void DispatchOffThreadRejection<TResult>(
    TResult result, Func<Action<TResult>?> commonSelector,
    Action<TResult>? rejectedCallback, string operation);

/// in-flight マーカーの解放。未登録でも安全。dispatch より前に呼ぶ（5.6.4 の契約）。
internal static void EndOperation(HashSet<string> inFlight, string inFlightKey);

/// 段階 7。nativeCall の例外を捕捉し、in-flight を解放して 9002 を通常経路で返す。
/// onNativeFailure は監視の世代リセットなど、操作固有の巻き戻しに使う（5.6.5）。
/// onNativeFailureResult は失敗結果の組み立てと配送を呼び出し側へ委ねる。null のときだけ
/// MacClipboardOperationResult を組み立てるフォールバックが働くので、それ以外の結果型を
/// 返す 12 操作では必ず指定する。
private static void InvokeNative(
    string operation, string inFlightKey, Action nativeCall,
    Action? onNativeFailure = null, Action<string>? onNativeFailureResult = null);

/// 通常経路の最終段。共通イベント → 個別 callback の順で InvokeInOrder に渡す（5.6.7）。
/// dispatcher が破棄済みなら結果を捨ててログに残す。
private static void Dispatch<TResult>(
    TResult result, Action<TResult>? common, Action<TResult>? perCall);
```

**`FireXxxResult` の命名規約**（結果型ごとに 1 本ずつ 12 本 + 変更イベント 1 本の計 13 本）:

- 手順は必ず「per-call スロットを退避して null 化 → `EndOperation` → `Dispatch`」の順（5.6.4 の契約、5.6.12 の取り出し規約）
- 共通イベントは `_instance?.XxxCompleted` から取る（static メソッドから呼ばれる通常経路のため。拒否経路の `this` とは取得元が異なる。5.6.2）
- **複数の操作が共有する結果型は、`result.Operation` からスロットを引く。** 該当するのは次の 2 つで、それぞれ専用の `TakeXxxCallback(string operation)` を用意する
  - `MacClipboardOperationResult`（removePasteboard / startObserving / stopObserving の 3 操作 → `TakeOperationCallback`）
  - **`MacClipboardOwnershipResult`（copy / append の 2 操作 → `TakeOwnershipCallback`）**
  - 後者は「結果型は 1 つ、スロットは 2 本（`s_onCopy` / `s_onAppend`）」という組み合わせで iOS に前例が無い。`s_onCopy` を決め打ちで退避すると **Append の `onResult` が永久に呼ばれずスロットがリークする**
- 1 型 1 操作の結果型は対応するスロットを直接退避してよい
- 例: `FireOwnershipResult(MacClipboardOwnershipResult result, string inFlightKey)`（`result.Operation` で分岐）/ `FireReadResult(MacClipboardReadResult result)`（`s_onRead` を直接）/ `FireOperationResult(MacClipboardOperationResult result, string inFlightKey)`（`result.Operation` で分岐）
- 変更イベントは `FireClipboardChanged(MacClipboardChangeEvent e)`。in-flight にもスロットにも触れず、`Dispatch(e, _instance?.ClipboardChanged, s_onChanged)` を呼ぶだけ

- `failure` の第 1 引数は `int` だが、`MacClipboardErrorInfo.Create(long, string?)` へは暗黙変換で通る
- `MacClipboardOwnershipResult` の `Operation` は、`failure` ラムダが `op` 定数をクロージャで捕捉することで供給される
- メッセージ生成（`CouldNotStartMessage` / `BusyMessage` / `ObservationBusyMessage` / `MainThreadMessage` / `DestroyedMessage` / `UnavailableMessage`）は 6.1 の文言を返す private static ヘルパ。`ObservationBusyMessage` だけ引数を取らず、`Another observation control call is already in progress.` を返す

**observation の 2 操作（`StartObserving` / `StopObserving`）だけは上の骨格をそのまま写せない。** 次の 4 点が異なる。

1. `TryBeginOperation` の第 2 引数が `op` ではなく `MacClipboardOperations.ObservationControlKey`
2. Busy メッセージが `BusyMessage(op)` ではなく専用文言（6.1 の `Another observation control call is already in progress.`）
3. 段階 7 に世代更新と `s_onChanged` の登録が入る（5.6.5 の状態遷移表）
4. `InvokeNative` の `onNativeFailure` に世代の巻き戻しを渡す

`StartObserving` の差分（段階 6 以降のみ。段階 1〜5 は同形）:

```csharp
const string op = MacClipboardOperations.StartObserving;
const string key = MacClipboardOperations.ObservationControlKey;

if (!TryBeginOperation(s_inFlight, key))
{
    DispatchRejectedResult(
        failure(MacClipboardErrorCodes.Busy, ObservationBusyMessage()),  // 専用文言
        commonSelector(), onStarted);
    return;
}

// 段階 7。世代更新は in-flight 取得の後、P/Invoke の前。
s_onStartObserving = onStarted;
s_onChanged = onChanged;
s_onChangedGeneration = ++s_observingGeneration;
s_pendingObservationGeneration = s_onChangedGeneration;

InvokeNative(op, key,
    nativeCall: () =>
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        clipboardStartObserving(scopeJson, intervalSeconds, s_startObservingDelegate, s_changeDelegate);
#endif
    },
    onNativeFailure: () =>
    {
        ReleaseChangeRegistrationIfOwned();
        s_pendingObservationGeneration = 0;
    });
    // startObserving は MacClipboardOperationResult を返すので
    // onNativeFailureResult は不要（既定のフォールバックが正しい型を組み立てる）。
```

`StopObserving` は世代を進めず `s_pendingObservationGeneration = s_observingGeneration;` だけを行う（5.6.5）。

| 段階 | 内容 | 失敗時のコード | dispatch 経路 |
| --- | --- | --- | --- |
| 1 | メインスレッド判定 | 9003 | `DispatchOffThreadRejection`（**触れない**） |
| 2 | tombstone 判定 | 9004 | `DispatchRejectedResult`（**触れない**） |
| 3 | 引数検証（5.6.3） | 9005 / 9007 | `DispatchRejectedResult`（**触れない**） |
| 4 | プラットフォーム判定 | 9002 | `DispatchRejectedResult`（**触れない**） |
| 5 | JSON 構築（Builder 呼び出し） | 9002 | `DispatchRejectedResult`（**触れない**） |
| 6 | in-flight マーカー取得 | 9001 | `DispatchRejectedResult`（**触れない**） |
| 7 | per-call スロット格納・世代更新・P/Invoke | 9002 | 通常経路 `FireXxxResult`（**`EndOperation` する**） |
| 8 | ネイティブコールバック到着 | ネイティブのコード、または応答を解析できない場合の **9006** | 通常経路 `FireXxxResult`（**`EndOperation` する**） |

- **段階 5（JSON 構築）を段階 6 より前に置くのが v1 からの変更点。** v1 の順序では、in-flight を取得した後の base64 化で `OutOfMemoryException` が出るとマーカーが解放されず、**そのプロセスの残り全体でその操作が恒久的に 9001 になる**。構築を先に行えばこの経路は消える
- 段階 5 の例外は `try/catch` で捕捉し、`{operation} could not be started.` として 9002 を返す。この時点ではまだ in-flight を取得していないので**拒否経路**を使う
- 段階 8 の 9006 は C# が生成する managed コードであり、ネイティブ自体は成功を返している。in-flight は通常経路として解放する
- **段階 1〜6 の失敗（拒否経路）は、pending スロットにも in-flight マーカーにも触れない。** それらは進行中の別呼び出しの所有物である
- **段階 7〜8 の失敗（通常経路）は、自分が所有しているので必ず `EndOperation` する**
- **`EndOperation` は dispatch より前に呼ぶ。** 購読者が callback の中から同じ操作を再度呼べるようにするため。この順序は契約であり、7.2 で検証する

#### 5.6.5 監視の世代管理

`StartObserving` / `StopObserving` は `ObservationControlKey`（= `"observation"`）を共有し、同時に 1 件だけ実行できる。

**世代カウンタの目的は「古い stop が新しい start の登録を消すのを防ぐ」ことではない**（共有キーがある以上その状態は 9001 で拒否されて発生しない）。目的は、**context を受け取れない `[MonoPInvokeCallback]` static メソッドへ「自分がどの登録に責任を持つか」を渡すこと**である。

状態遷移:

| 契機 | `s_observingGeneration` | `s_onChangedGeneration` | `s_pendingObservationGeneration` |
| --- | --- | --- | --- |
| `StartObserving` 段階 7 | `++` | `= s_observingGeneration` | `= s_onChangedGeneration` |
| `StopObserving` 段階 7 | **進めない** | 変更なし | `= s_observingGeneration` |
| 制御呼び出しの完了（**成功した StartObserving は除く**、下記ゲート参照） | 変更なし | `ReleaseChangeRegistrationIfOwned()` の判定に従う | `= 0` |
| 段階 7 の P/Invoke 例外 | 変更なし | `ReleaseChangeRegistrationIfOwned()` | `= 0` |
| `ClearAllPendingCallbacks` | `= 0` | `= 0` | `= 0` |

**解放には外側のゲートが必須。** `ReleaseChangeRegistrationIfOwned()` を無条件に呼んではならない。

```csharp
// 失敗した start は観測を開始していないし、stop はどのみち終わらせる。
// どちらも登録を手放すが、その登録を作ったのが自分である場合に限る。
if (!isSuccess || operation == MacClipboardOperations.StopObserving)
{
    ReleaseChangeRegistrationIfOwned();
}
s_pendingObservationGeneration = 0;
```

- **このゲートが無いと、成功した `StartObserving` が自分の登録を即座に破棄する。** 段階 7 の直後は `s_onChangedGeneration == s_pendingObservationGeneration == G` なので、`ReleaseChangeRegistrationIfOwned` 内の `G <= G` が成立して `s_onChanged = null` になり、**`onChanged` が一度も発火しない**（共通イベント `ClipboardChanged` だけが出る状態になる）
- ゲートは `HandleObservationControlCallback`（5.6.9 の guard 外に切り出す実処理）に置く。`IosClipboardManager.cs:1638-1652` と同形

```csharp
private static void ReleaseChangeRegistrationIfOwned()
{
    if (s_onChangedGeneration != 0 && s_onChangedGeneration <= s_pendingObservationGeneration)
    {
        s_onChanged = null;
        s_onChangedGeneration = 0;
    }
}
```

- `s_onChangedGeneration != 0` のガードが必要。登録が無い状態で `s_pendingObservationGeneration` が 0 のとき、`0 <= 0` が成立して誤って「解放した」ことにならないようにする
- 参照等価では判定できない。同じ delegate インスタンスが 2 回登録されうるため

#### 5.6.6 `s_dispatcher` の所有と有効性

| 項目 | 規約 |
| --- | --- |
| 所有者 | `UnityMainThreadDispatcher` 自身（`DontDestroyOnLoad`）。Manager は非所有の参照を持つだけ |
| 捕捉 | `Awake` で `UnityMainThreadDispatcher.Instance` をメインスレッド上で捕捉する。`Instance` の getter は GameObject を生成するためメインスレッド専用 |
| `OnDestroy` | **null にしない。** 破棄後の 9004 拒否結果を配送するために必要 |
| リセット | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で null に戻し、次の `Awake` で再捕捉する |
| メインスレッドでの null 判定 | **Unity の `==` オーバーロードを使う**（`if (dispatcher == null)`）。GameObject が破棄された dispatcher を検出できる。破棄済みへ Enqueue しても `Update` が回らず結果が消える |
| off-thread（段階 1）での null 判定 | **`(object?)dispatcher == null` のプレーン参照判定を使う。** Unity の `==` はネイティブオブジェクトを参照するため、メインスレッド以外から呼んではならない |

- 段階 1 の拒否は「ログを含むすべてを `Enqueue` した closure の中で行う」。off-thread で触れるのはプレーン参照読みと dispatcher の lock 保護された `Enqueue` だけになる

#### 5.6.7 dispatch と例外分離

```csharp
internal static void InvokeInOrder<TResult>(TResult result, Action<TResult>? common, Action<TResult>? perCall)
{
    try { common?.Invoke(result); }
    catch (Exception ex) { Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] common: {ex.Message}"); }

    try { perCall?.Invoke(result); }
    catch (Exception ex) { Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] perCall: {ex.Message}"); }
}
```

- **try/catch は共通イベントと個別 callback で分離する。** `MacShareManager` は 1 つの try/catch で包んでいるが、それでは共通イベントの例外が個別 callback を止めてしまう。`IosClipboardManager` の分離形を採る
- `internal static` の純粋関数にして EditMode で検証する

#### 5.6.8 ライフサイクルと teardown

`OnDestroy` の手順:

1. **tombstone (`s_isTerminated = true`) をネイティブ呼び出しより先に立てる。** P/Invoke が例外を投げても、部分的な teardown で「再生成可能かつ遅延コールバックを捨てない」状態にしない
2. `RunDestroyCleanup(stop: StopObservingForTeardown, managedCleanup: ...)` を呼ぶ
3. `managedCleanup` は `ClearAllPendingCallbacks()`（per-call スロット 16 本の全消し・世代 3 変数を 0・`s_inFlight.Clear()`。5.6.12）を行い、`finally` で `_instance = null` にする
4. `s_dispatcher` は残す（5.6.6）

- `RunDestroyCleanup` は **`stop` / `managedCleanup` の 2 引数版を新設する。** macOS には iOS の `cancelLoads` に相当する関数が無いため 3 引数版は使わない。`stop` が例外を投げても `managedCleanup` が必ず走る形にし、`internal static` の純粋関数として EditMode で検証する
- **`StopObservingForTeardown` はガードチェーンを迂回してネイティブを直接叩く。** `ObservationControlKey` が in-flight（`StartObserving` の応答待ち）である可能性があるが、**tombstone が先に立っており新規操作が入らないため安全**。到着するコールバックはすべて `DiscardIfTerminated` で捨てられる
- 順序は「`stop` → `managedCleanup`」。世代変数のリセットは `stop` の**後**に走る
- teardown の `clipboardStopObserving` には **NULL を渡す**（1.2.4）
- **`DiscardIfTerminated` は操作コールバックだけでなく変更コールバック（`OnClipboardChanged`）にも適用する。** teardown で stop を呼んでも、既に main queue に載ったイベントが 1 回届きうる

#### 5.6.9 IL2CPP 制約

- `[MonoPInvokeCallback]` を付けるメソッドは **`static` でなければならない**。インスタンスメソッドは AOT で関数ポインタ化できない
- **例外をネイティブ境界へ漏らさない。** コールバック本体は全体を try/catch で囲み、`Debug.LogError` に落とす。ネイティブのスタックフレームへ managed 例外が伝播すると未定義動作になる
- 永続 delegate は `static readonly` フィールドで保持する。ローカル変数や一時オブジェクトに置くと GC に回収され、ネイティブが保持する関数ポインタが無効になる
- `[MonoPInvokeCallback]` 本体は `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` の中に置き、**実処理は `HandleXxxCallback` として guard の外に切り出す**。この分離が無いと Editor 用の完了シーム（5.6.10）がコンパイルできない

#### 5.6.10 テストシーム（`#if UNITY_EDITOR`）

Editor では P/Invoke がコンパイルされないため、`BridgeAvailableOverrideForTests` だけでは操作が永久に pending のままになる。結果配送・in-flight 解放・世代管理・tombstone を検証するには**完了シームが必須**。`IosClipboardManager.cs:1656-1704` と同形で用意する。

**シームは操作ごとに 1 本ずつ用意する。** 1 本にまとめると、操作ごとに異なる in-flight キー・per-call スロット・結果型を注入し分けられない。

| シーム | 引数 | 用途 |
| --- | --- | --- |
| `ResetForTests()` | なし | tombstone と全 static 状態のリセット |
| `BridgeAvailableOverrideForTests` | `bool`（プロパティ） | ガードチェーン段階 4 を通過させる |
| `MaxRequestBytesOverrideForTests` | `long?`（プロパティ） | 段階 3 の 9007 を小さな payload で駆動する。`MacClipboardLimits.MaxRequestBytes` は `const` でテストから差し替えられず、実測 32 MiB の `byte[]` を確保するのは PlayMode テストとして重い |
| `CompleteOwnershipForTests` | `(string operation, bool isSuccess, string? json, long errorCode, string? errorMessage)` | copy / append。**`operation` が必須**（別 in-flight キー・別スロット・結果の `Operation` の区別） |
| `CompleteObservationControlForTests` | `(string operation, bool isSuccess, long errorCode, string? errorMessage)` | **startObserving / stopObserving 専用。** この経路でのみ `s_pendingObservationGeneration = 0` と、5.6.5 のゲート（`!isSuccess \|\| operation == StopObserving` のときだけ `ReleaseChangeRegistrationIfOwned()`）が走る |
| `CompleteOperationForTests` | `(bool isSuccess, long errorCode, string? errorMessage)` | **removePasteboard 専用**（値なし操作のうち observation 以外はこれだけ）。iOS の同名シームは `operation` 引数を持つが、macOS 版は対象が 1 操作なので `MacClipboardOperations.RemovePasteboard` 固定とする |
| `CompleteReadForTests` | `(bool, string?, long, string?)` | read |
| `CompleteReadDataForTests` | `(bool, string?, long, string?)` | readData |
| `CompleteSnapshotForTests` | `(bool, string?, long, string?)` | snapshot |
| `CompleteClearForTests` | `(bool, string?, long, string?)` | clear |
| `CompleteCreatePasteboardForTests` | `(bool, string?, long, string?)` | createPasteboard |
| `CompleteDetectPatternsForTests` | `(bool, string?, long, string?)` | detectPatterns |
| `CompleteDetectValuesForTests` | `(bool, string?, long, string?)` | detectValues |
| `CompleteDetectMetadataForTests` | `(bool, string?, long, string?)` | detectMetadata |
| `CompleteAccessBehaviorForTests` | `(bool, string?, long, string?)` | accessBehavior |
| `CompleteForegroundChangeForTests` | `(bool, string?, long, string?)` | checkForegroundChange |
| `DeliverChangeEventForTests` | `(string? eventJson)` | 変更イベントを注入 |
| `IsInFlightForTests` / `InFlightCountForTests` | `(string key)` / なし | in-flight 集合の観測 |
| `HasChangeRegistrationForTests` / `PendingObservationGenerationForTests` | なし | 世代管理の観測 |
| `HasAnyPendingCallbackForTests` | なし | per-call スロット 16 本（5.6.12）のリーク検出 |

- **`CompleteObservationControlForTests` を `CompleteOperationForTests` で代用してはならない。** 通常の operation 完了経路には世代のリセットが無く、7.2 の「start → stop → start で登録が入れ替わる」「`PendingObservationGenerationForTests` の遷移」を駆動できない
- Detect 系を 1 本にまとめてはならない。3 操作は別 in-flight キー・別結果型である
- 各シームは対応する操作の実装と**同じ段**（12 章）で追加する

- これらは **PlayMode から駆動する**。EditMode では `UnityMainThreadDispatcher` が flush されず、Manager インスタンスも生成できない
- player ビルドからはコンパイル除外される

#### 5.6.11 スレッド契約・メモリ契約・ログ方針

スレッド契約:

- **公開 API は Unity メインスレッド専用。** 他スレッドからの呼び出しは 9003 で拒否する。`Instance` getter だけは GameObject を生成するため保護できない（XML コメントに「メインスレッドから最初に触れること」と明記する）
- ネイティブコールバックは**必ず main thread で届く**（Swift ファサードが main actor へ hop する）。macOS Standalone では AppKit のメインスレッド = Unity のメインスレッドと想定しているが、実測記録は無い（8 章 V-4）
- それでも結果は `UnityMainThreadDispatcher.Enqueue` を通す。ネイティブのスタックフレーム内で購読者コードを実行しない
- `s_inFlight` / per-call スロット / 世代変数はメインスレッドからしか触らないためロック不要

メモリ契約:

- 15 個の操作 delegate と 1 個の変更 delegate を `static readonly` で保持する
- コールバックの `const char*` は marshaling が即座に managed string へコピーする。ポインタを保持しない
- `JsonValue` は文字列のオフセットを保持する設計なので、`TryGetBase64Bytes` はソース span から直接デコードし中間 substring を作らない
- 送信側・受信側の上限は 5.6.3
- unique pasteboard は pasteboard server 上に残る。`CreatePasteboard` の結果 scope を保持し、不要になったら `RemovePasteboard` を呼ぶ責務は**呼び出し側**にある。Manager は追跡しない。XML コメントに明記する

ログ方針（`csharp.md` のログルールからの意図的な逸脱。XML コメントのルールは逸脱しない）:

- clipboard 本文はパスワード・トークン・文書を含みうる。**値をログに出さない**
- 出してよいのは shape / count / flag のみ: `itemCount`、`representationCount`、`totalBytes`、`hasScope`、`scopeKind`、`hasCallback`、`operation`、`errorCode`
- pasteboard 名も出さない（ネイティブ側 `NTScope` が general 以外をハッシュ化しているのと同じ方針）
- Builder / Parser / 結果型のファクトリはログを出さない。Manager の dispatch 境界だけが操作名・成否・errorCode を出す
- **`Debug.Log` の位置も `csharp.md` からの意図的な逸脱である。** 同ルールは「全メソッドの先頭 1 行目」を求めるが、本設計はガードチェーン（段階 1〜4）の**後**に置く。段階 1 のメインスレッド判定は入場ログを含むすべてに先行しなければならないため（別スレッドから `Debug.Log` を呼ぶこと自体を避ける）。`IosClipboardManager` も同じ理由でこの順序を採り、`TryStartOperation` 内にコメントを残している
- 逸脱の宣言方法は既存の使い分けに従う
  - Manager / Builder / Parser: **クラスの XML `<summary>` 内 `<para>`**（`IosClipboardManager.cs:43-47`、`IosClipboardJsonBuilder.cs:23-26`）
  - 結果型・payload 型: **`#if` 直後のファイル先頭 `//` コメント**（`IosClipboardErrorInfo.cs:3-7`）

#### 5.6.12 per-call スロット

per-call callback は操作ごとに static スロット 1 本で保持する。single-flight により pending は最大 1 件なので、これで足りる（5.6.2）。

| スロット | 型 | 対応操作 |
| --- | --- | --- |
| `s_onCopy` / `s_onAppend` | `Action<MacClipboardOwnershipResult>?` | copy / append |
| `s_onRead` | `Action<MacClipboardReadResult>?` | read |
| `s_onReadData` | `Action<MacClipboardReadDataResult>?` | readData |
| `s_onSnapshot` | `Action<MacClipboardSnapshotResult>?` | snapshot |
| `s_onClear` | `Action<MacClipboardChangeCountResult>?` | clear |
| `s_onCreatePasteboard` | `Action<MacPasteboardScopeResult>?` | createPasteboard |
| `s_onRemovePasteboard` / `s_onStartObserving` / `s_onStopObserving` | `Action<MacClipboardOperationResult>?` | removePasteboard / startObserving / stopObserving |
| `s_onDetectPatterns` | `Action<MacClipboardDetectedPatternsResult>?` | detectPatterns |
| `s_onDetectValues` | `Action<MacClipboardDetectedValuesResult>?` | detectValues |
| `s_onDetectMetadata` | `Action<MacClipboardDetectedMetadataResult>?` | detectMetadata |
| `s_onAccessBehavior` | `Action<MacClipboardAccessBehaviorResult>?` | accessBehavior |
| `s_onCheckForegroundChange` | `Action<MacClipboardForegroundChangeResult>?` | checkForegroundChange |
| `s_onChanged` | `Action<MacClipboardChangeEvent>?` | 監視イベント（操作ではなく登録単位。5.6.5 の世代管理の対象） |

計 16 本（操作 15 + 変更イベント 1）。

取り出し規約:

- 結果 dispatch の直前にスロットを**ローカルへ退避してから null にする**（`var perCall = s_onCopy; s_onCopy = null;`）。退避せずに dispatch すると、購読者が callback 内から同じ操作を再実行したときにスロットが上書きされる
- 退避 → `EndOperation` → `Dispatch` の順（5.6.4 の契約）
- **複数操作が共有する結果型は `Operation` からスロットを引く。** `TakeOperationCallback(string operation)`（removePasteboard / startObserving / stopObserving）と `TakeOwnershipCallback(string operation)`（copy / append）の 2 本を用意する
- `ClearAllPendingCallbacks()` は 16 本すべてを null にする。`HasAnyPendingCallbackForTests` も 16 本すべてを見る
- この表の被参照: 5.6.4 の骨格コード（`s_onCopy = onResult;`）、5.6.8 の `ClearAllPendingCallbacks()`、5.6.10 の `HasAnyPendingCallbackForTests`

#### 5.6.13 Awaitable 版について

- 本計画では **callback 版のみ**を実装する
- `common.md` の前提条件（in-flight ガード）は本設計で満たされるため、`XxxAsync` の追加は後から非破壊的に可能
- 現時点で見送る理由: `Runtime/` 配下に `Awaitable` / `*Async` の実装が 1 件も無く、パッケージ全体でまだ前例が無い。Clipboard だけ先行させない

### 5.7 実装順序

12 章の 4 段階に対応する（対応は下表の「段」列）。各段の完了条件は 10 章。

| ステップ | 段 |
| --- | --- |
| 1 | 段 1 |
| 2〜6 | 段 2 |
| 7〜8 | 段 3 |
| 9 | 段 4 |
| 10 | 全段（各段の完了時に実施する） |

1. `ClipboardJsonReader.cs` への改名（`git mv` で `.cs` と `.cs.meta` を対で移動）、ガード拡張、クラス doc の中立化、Runtime 3 箇所 + Tests 42 箇所の置換。既存 iOS テスト全 pass を確認する
2. `MacClipboardPayloads.cs` / `MacClipboardErrorInfo.cs` / `MacClipboardConstants.cs`
3. 結果型 12 種（10 ファイル）+ `MacClipboardChangeEvent.cs`
4. `MacClipboardJsonBuilder.cs` + `MacClipboardJsonBuilderTests.cs`
5. `MacClipboardJsonParser.cs` + `MacClipboardJsonParserTests.cs`
6. `MacClipboardResultTests.cs`
7. `MacClipboardManager.cs` の骨格（Singleton / lifecycle / tombstone / dispatcher / ガードチェーン / dispatch / テストシーム）+ Copy / Append / Read / ReadData / Clear
8. `MacClipboardManagerDispatchTests.cs`（純粋関数）+ `MacClipboardManagerIntegrationTests.cs`（PlayMode）
9. 残り 10 操作 + 監視の世代管理 + 対応するテスト
10. 各段で Unity Test Runner の EditMode / PlayMode を実行し、**既存テストを含めて**全 pass を確認する

---

## 6. エラーケース一覧と返却仕様（層別）

### 6.1 C# Bridge 層

| code | 条件 | Message |
| --- | --- | --- |
| 9001 `Busy` | 同一操作が既に pending | `{operation} is already in progress.` |
| 9001 `Busy` | **observation 共有キーが別の制御呼び出しに占有されている** | `Another observation control call is already in progress.`（`startObserving is already in progress.` だと stop が進行中のときに嘘になる） |
| 9002 `BridgeUnavailable` | macOS Standalone Player 以外 | `{operation} is only available on a macOS Standalone player.` |
| 9002 `BridgeUnavailable` | 段階 5（JSON 構築）または段階 7（P/Invoke）で例外 | `{operation} could not be started.` |
| 9003 `MainThreadRequired` | Unity メインスレッド以外から呼ばれた | `{operation} must be called from the Unity main thread.` |
| 9004 `ManagerDestroyed` | `OnDestroy` 後の呼び出し | `{operation} is unavailable: MacClipboardManager has been destroyed.` |
| 9005 `InvalidRequest` | null 引数（5.6.3） | `content must not be null.` / `ownership must not be null.` / `request must not be null.` / `scope must not be null.` / `patterns must not be null.` |
| 9006 `ResponseParseFailed` | 成功応答の JSON を解釈できない、または base64 が受信上限超過 | `The native result could not be parsed.` |
| 9007 `RequestTooLarge` | 送信 payload が `MacClipboardLimits.MaxRequestBytes` 超過 | `Clipboard content exceeds the {MaxRequestBytes} byte request limit.` |

- 段階 1〜6 の失敗は拒否経路、段階 7〜8 の失敗は通常経路（5.6.4）。同じ 9002 でも経路が異なるので dispatch 関数を取り違えないこと
- factory への空白名は結果ではなく `ArgumentException`

### 6.2 parser 層（ネイティブ `BridgeError`）

| code | 条件 | Message |
| --- | --- | --- |
| 1301 | 引数が供給されたが JSON として解釈できない | `Failed to parse JSON: Invalid clipboard JSON argument.` |
| 1302 | 必須引数が NULL / 空（`ReadData` の空 `utType` はここに落ちる） | `Bridge contract violation: A required argument was missing.` |
| 1302 | `startObserving` の `onChange` が NULL | `Bridge contract violation: onChange is required; observation would produce no observable result.` |

- 1301 は C# が JSON を生成する以上、正しく実装されていれば到達しない。到達した場合は C# 側のバグを示す

### 6.3 use case / repository 層（`ClipboardError`）

操作ごとの到達可能性。**v1 から 1505 を全削除、1507 を Read / Snapshot のみに限定、ReadData から 1504 を削除、DetectMetadata に 1524 を追加した。**

この表は use case / repository 層のコードだけを対象とする。**1301 / 1302（6.2）は引数をパースするすべての操作に共通で到達しうるため、この表には載せていない。**

| 操作 | 到達しうる code |
| --- | --- |
| Copy | 1501, 1502, 1504, 1506, 1509, 1599 |
| Append | 1501, 1502, 1504, 1506, 1510, 1511, 1599 |
| Read | 1507, 1599 |
| ReadData | 1599 |
| Snapshot | 1507, 1512, 1599 |
| Clear | 1599 |
| CreatePasteboard | 1599 |
| RemovePasteboard | 1508 |
| DetectPatterns | 1503, 1513, 1514, 1515, 1524, 1599 |
| DetectValues | 1503, 1513, 1514, 1515, 1524, 1599 |
| DetectMetadata | 1513, 1514, 1515, 1524, 1599 |
| GetAccessBehavior | 1599（**macOS 15.4 未満は成功 + `Unavailable`**） |
| StartObserving | 1523 |
| StopObserving | なし（常に成功） |
| CheckForegroundChange | 1599 |

補足:

- **1505（invalid pasteboard name）は C ABI 経由では到達しない。** 空名は `UnityMacClipboardJsonParser` の `parseScope` / `parseCreateRequest` が先に弾き、1301 / 1302 になる。`PasteboardResolver` の 1505 はブリッジ経由では死にコード
- **1507（pasteboard unavailable）は Read と Snapshot だけ。** `pasteboardItems` を触るのはこの 2 経路のみ
- **ReadData は 1504 を返さない。** `readData` は UTI を検証せず `pasteboard.data(forType:)` を呼ぶだけで、UTI 検証は copy / append 経路の `ClipboardContentValidator` からしか走らない
- **void 経路の 3 操作（`removePasteboard` / `startObserving` / `stopObserving`）に 1599 は挙げない。** encode を通らないため、1599 の発生源は総称 catch だけ（`removePasteboard` は `completeVoid` の catch、`startObserving` はファサード自身の `catch { ClipboardError.unknown(...) }`）で、いずれも実質非到達。3 操作とも同じ扱いに揃えている
- 値を返す操作の 1599 には、ネイティブ側のエンコード失敗（`The result could not be encoded.`）が含まれる
- 1521 / 1522 は C ABI 未公開のため到達しない

### 6.4 エラーではないケース

| ケース | 表現 |
| --- | --- |
| `ReadData` で該当 UTI が無い | 成功 + `Data == null` |
| **`ReadData` に不正な UTI を渡した** | 成功 + `Data == null`。**C# 側で UTI の誤りを検出する手段は無い** |
| `Read` で pasteboard が空 | 成功 + `Items.Count == 0` |
| `DetectPatterns` で何も一致しない | 成功 + 空リスト |
| `GetAccessBehavior` が macOS 15.4 未満 | 成功 + `MacClipboardAccessBehavior.Unavailable` |
| `StopObserving` を購読していない状態で呼ぶ | 成功（冪等） |
| `CheckForegroundChange` の初回呼び出し | 成功 + `Changed == true`。**ただし同一 scope で `StartObserving` していない場合に限る**（監視中はほぼ常に false）。tracker は scope 単位なので、監視中でも別 scope なら初回 true になる |
| 監視中に変更イベントの encode が失敗した | ネイティブが**黙って捨てる**。C# には何も届かず、エラーも観測できない（1.6） |
| `Append` 成功後の changeCount | **変わらない。** 同じ ownership を次の Append にそのまま使える |

---

## 7. テスト方針

### 7.1 EditMode（層 1 / `Tests/Runtime/`）

**Manager インスタンスを生成しない。** `internal static` の純粋関数と Builder / Parser / 結果型 / payload 型だけを対象にする（`testing.md` 1 節）。

`MacClipboardJsonBuilderTests`:

- scope: general が `name` キーを出さない、named / unique が出す
- content: representations の base64、複数 item、空 dictionary
- options: `null` → `null` を返す、`localOnly` の true / false
- ownership / createRequest / matchingTypes（`null` → `null`、空 → `[]`）/ patterns（rawValue・ソート順・空 → `[]`）
- エスケープ: `"`・`\`・制御文字。**非 ASCII は生の UTF-8 のまま出力される**ことを確認する
- カルチャ非依存: `CurrentCulture` を `de-DE` にしても数値表現が変わらない

`MacClipboardJsonParserTests`:

- 12 種の正常系
- `ReadDataJson` の `{"data":null}` が成功 + null
- `DetectedValuesJson` の明示 null と空配列の区別、入れ子型 8 種の全フィールド（1.3.1）
- `ScopeJson.name` が general で欠落していても解析できること、named で欠落したら失敗すること
- `PatternsJson` がトップレベル配列であること
- 未知 pattern の無視、未知 accessBehavior → `Unknown`
- 不正 JSON / 型不一致 / キー欠落の失敗
- base64 上限超過の失敗
- ISO 8601 日付（UTC・オフセット付き・不正文字列 → null）

`MacClipboardResultTests`:

- 5.5.1 の 3 分岐すべて（参照型 payload は失敗時 null / 値型 payload は失敗時に既定値 / `MacClipboardDetectedPatternsResult.Patterns` は失敗時も非 null で空）
- 入れ子オブジェクトのコレクション規約（`MacClipboardReadContents.Items` などが、そのオブジェクトを構築した以上は常に非 null であること）。失敗結果からは入れ子自体が取れないので、成功結果に対してのみ検証する
- 結果型が `readonly struct` であること（`default(T)` が `IsSuccess == false` になる）
- `MacClipboardOwnershipResult.Operation` が copy / append を区別できること
- `MacClipboardErrorInfo.Create` の正規化と、**`int` 値域外の `long` が 1599 に落ちること**
- payload factory の `ArgumentException`（`MacPasteboardScope.Named("")` / `Unique("")` / `MacPasteboardCreationRequest.Named("")`）
- `MacClipboardContentItem.PlainText` の UTF-8 エンコード
- rawValue 変換表の往復（11 パターン + 5 accessBehavior + 1 metadataType）
- `MacClipboardOperations` の 15 定数がすべて相異なり、`ObservationControlKey`（`"observation"`）が 15 の操作名のいずれとも一致しないこと

`MacClipboardManagerDispatchTests`（**純粋関数のみ**）:

- `InvokeInOrder`: 共通 → 個別の順序、共通が例外を投げても個別が呼ばれる、逆も同様、例外が外へ漏れない
- `TryBeginOperation` / `EndOperation`: 2 回目の `Add` が false、`Remove` 後は再取得可能、未登録の `Remove` が安全
- `RunDestroyCleanup`: `stop` が例外を投げても `managedCleanup` が必ず走る

### 7.2 PlayMode（層 2a / `Tests/PlayMode/`）

各**テスト項目**の末尾の `(3)` / `(4)` は、12 章のどの段で追加するかを示す。段 3 の DoD は `(3)` の項目だけで判定する。マークが付かない行はテスト項目ではなく実行上の注意である（ガードチェーンの行は子項目側にマークを持つ）。

`UnityMainThreadDispatcher` は実際の `Update` が無いと flush されないため、EditMode では通らない経路をここで埋める。**ガードチェーンの検証は EditMode ではなくこの層で行う**（v1 からの修正）。

- ガードチェーン 6 段の各拒否経路（段階 1: 9003 / 段階 2: 9004 / 段階 3: 9005・9007 / 段階 4: 9002 / 段階 5: 9002 / 段階 6: 9001）を駆動する
  - 段階 1: 別スレッドから呼ぶ / 段階 2: `OnDestroy` 後に呼ぶ / 段階 3: null 引数と上限超過 payload / 段階 4: `BridgeAvailableOverrideForTests` を false のままにする / 段階 6: 完了シームを呼ばずに 2 回続けて呼ぶ
  - **段階 5（JSON 構築の例外）は、`MacClipboardContentItem.FromRepresentations` に値が `null` の `byte[]` を含む辞書を渡して駆動する。** `Convert.ToBase64String(null)` が例外を投げる。5.6.3 の事前検証は辞書の要素の null までは検査しないため、これが段階 5 に到達する正規の入口になる
  - いずれの拒否でも in-flight マーカーと per-call スロットに触れていないことを `IsInFlightForTests` / `HasAnyPendingCallbackForTests` で確認する
  - 段階 3 の 9007 は `MaxRequestBytesOverrideForTests` で上限を小さくして駆動する（32 MiB の実確保はしない）
  - 6 段すべて `(3)`
- dispatcher 経由で共通イベント → 個別 callback の順序が保たれること `(3)`
- **`EndOperation` が dispatch より前に走ること**（callback の中から同じ操作を再実行しても 9001 にならない）`(3)`
- 監視の世代管理: start → stop → start の順で登録が正しく入れ替わること、`PendingObservationGenerationForTests` の遷移 `(4)`
- observation 共有キーの Busy メッセージが専用文言であること `(4)`
- Editor（非 macOS Player）での失敗経路 `(3)`
- **破棄前に取得した参照を保持したまま**呼んだ場合、9004 で拒否され共通イベントが発火すること（決定 2 が保証する範囲）`(3)`
- 破棄後に `Instance` から取り直して呼んだ場合、9004 で拒否されるが**旧購読者には届かない**こと（決定 2 が保証しない範囲。5.6.2）`(3)`
- 破棄後に届いた変更イベントが捨てられること `(3)`
- **`[TearDown]` で必ず `ResetForTests()` を呼ぶ。** これを欠くと、破棄系テスト 1 件が Domain Reload 無効設定で以降の全 PlayMode テストを 9004 にする
- `MacClipboardManager` は A 群ガードなので build target の切り替えなしに実行できる

### 7.3 層 2b / 層 3

`testing.md` のとおり未着手。本計画では新規導入しない。

### 7.4 テストの機微情報の扱い

`testing.md` 6 節に従う。**テストはサンプル値のみを用い、実クリップボード内容を持ち込まない。** 本計画のテストはすべて「サンプル値のみを扱うテスト」に分類され、通常のアサーションでよい。

### 7.5 手動確認（実機 macOS 15+ / macOS Standalone Player）

| # | 確認項目 | 期待 |
| --- | --- | --- |
| 1 | `Copy`（plain text）→ 他アプリで Cmd+V | 貼り付けできる。`OwnershipChanged` が `Operation == "copy"` で発火 |
| 2 | `Copy` 直後に `Append` | 成功。**`changeCount` は変わらず、同じ ownership を次の Append にそのまま使える**。`Operation == "append"` |
| 3 | 他アプリでコピー後に `Append` | 1511（ownership lost） |
| 4 | `Read`（他アプリがコピーしたテキスト） | 読める。書いた型以外の派生型も含まれることを確認 |
| 5 | `ReadData`（存在しない UTI / 不正な UTI） | **どちらも成功 + `Data == null`** |
| 6 | `Snapshot`（フィルタなし / あり） | `ItemTypes` と `MatchingItemIndexes` が返る |
| 7 | `Snapshot`（空配列フィルタ） | 1512 |
| 8 | `Clear` → `Read` | items が空 |
| 9 | `CreatePasteboard(Unique)` → `Copy` → `Read` → `RemovePasteboard` | 一連が成功。解放後の `Read` の挙動を記録 |
| 10 | `RemovePasteboard(General)` | **1508**（C# は事前拒否しない） |
| 11 | `DetectPatterns`（URL・電話番号を含むテキスト） | 一致パターンが返る。macOS 15.4 未満では 1513 |
| 12 | `DetectValues`（同上） | 値が返る。**許可ダイアログの有無と、拒否時に 1514 になること**を記録 |
| 13 | `DetectMetadata`（plain text） | 1515 で失敗（仕様通り）。macOS 15.4 未満では 1513 |
| 14 | `GetAccessBehavior` | 15.4+ で `default`/`ask`/`alwaysAllow`/`alwaysDeny`、15.0-15.3 で `Unavailable` |
| 15 | `StartObserving` → 他アプリでコピー | `ClipboardChanged` が発火。**非アクティブ中は止まり、前面復帰時に追いつく**ことを確認 |
| 16 | `StartObserving` を 2 回連続（別の `onChanged` で） | 2 回目の登録に置き換わる。1 回目の callback は呼ばれない |
| 17 | `StartObserving(interval: 0)` / `(interval: 61)` / 負値 / NaN | **1523**（C# は事前拒否しない） |
| 17b | `DetectPatterns` に空コレクション | **1503**（C# は事前拒否しない） |
| 17c | `ReadData` に空文字の utType | **1302**（C# は事前拒否しない） |
| 17d | `MacPasteboardScope.Named(" ")`（空白のみ） | **`ArgumentException`**。ネイティブは素通りさせるため C# factory が唯一の防波堤（1.4 / 5.1） |
| 18 | `StopObserving` 後にコピー | イベントが来ない |
| 19 | `CheckForegroundChange`（**監視していない状態で**）初回 / 2 回目 | 初回 true、変更なしの 2 回目 false |
| 20 | `CheckForegroundChange` を `StartObserving` 中に呼ぶ | ほぼ常に false。併用しない旨を確認 |
| 21 | 単一 item で 10 MiB 超の `Copy` → **Player を終了** → 他アプリで貼り付け | **lazy data provider 経路のため貼り付けできない**ことを確認・記録 |
| 22 | 単一 item で 10 MiB 超の `Copy` → Player 起動中に貼り付け | 貼り付けできる。所要時間とフレーム落ちを記録 |
| 23 | 32 MiB 超の `Copy` | **9007**（C# 側で拒否） |
| 24 | 他アプリが 50 MiB 超をコピーした状態で `Read` | ピークメモリと所要時間を記録（5.6.3 の受信側制約） |
| 25 | 日本語・絵文字・サロゲートペアを含むテキストの `Copy` → `Read` | 往復して同一であること（1.2.3） |
| 26 | Universal Clipboard（`localOnly: false`）で別 Apple デバイスへ | **未検証項目**。結果を記録 |
| 27 | ログ確認 | clipboard 本文・pasteboard 名が Console / Player.log に出ていない |
| 28 | コールバックのスレッド | ネイティブコールバック到着時のスレッド ID が Unity メインスレッドと一致する |
| 29 | App Sandbox 有効ビルド | named / unique pasteboard の作成・解放が可能か記録（8 章 V-5） |

---

## 8. 要検証事項

**実機・実測が必要なものだけを残した。** 設計判断（`ClipboardJsonReader` の改名、`RunDestroyCleanup` のシグネチャ、Editor 実行、`BOOL` の宣言方法、定数の置き場所、`TryStartOperation` の構造、シームの粒度）はすべて 9 章で確定させている。V-6 が検証するのは D-2 で確定した宣言の**実挙動**であり、宣言方法そのものは未決ではない。

| # | 項目 | 内容 |
| --- | --- | --- |
| V-1 | xcframework とソースの版対応 | 参照したソースの commit / タグと同梱 xcframework 1.3.0 のビルド元が一致すること。**着手前に確認する**（0.1） |
| V-2 | 文字列マーシャリング | 既定のマーシャリングで非 ASCII（日本語・絵文字・サロゲートペア）が往復すること。化ける場合は `[MarshalAs(UnmanagedType.LPUTF8Str)]` を付与する |
| V-3 | サイズ上限 | `MacClipboardLimits` の 32 MiB 2 件は暫定値。所要時間・ピークメモリを実測して確定する。受信側は上限を掛けてもマーシャル時のメモリを減らせない点も実測する |
| V-4 | コールバックのスレッド | macOS Standalone で AppKit のメインスレッド = Unity のメインスレッドであることの実測（7.5 #28） |
| V-5 | App Sandbox | サンドボックス有効時に named / unique pasteboard を作成・解放できるか。必要な entitlement は `com.apple.security.app-sandbox` 有効下での pasteboard アクセス可否として確認する |
| V-6 | `NSInteger` / `BOOL` のマーシャリング実挙動 | **D-2 で確定した宣言**（`long` + `[MarshalAs(UnmanagedType.I1)]`）が arm64 / x86_64 の IL2CPP ビルドで正しい値を届けることの実測。宣言の決定は済んでおり、ここで検証するのは実挙動だけ |
| V-7 | `localOnly` の効果 | ネイティブ側が「Universal Clipboard への効果は実機未確認」と明記している。実機での効果を記録する（XML コメントへの引き写しは実測ではなく作業なので 10 章 DoD に置いた） |
| V-8 | 監視のアクティブ／非アクティブ挙動 | macOS Player がバックグラウンドに入ったときのポーリング停止・復帰の実挙動 |
| V-9 | `detectValues` の許可 UI | ユーザーへの通知の有無・条件・拒否時の 1514 の再現性。`testing.md` 5 節の「層 3 再確認」対象 |
| V-10 | lazy data provider の実挙動 | 10 MiB 超の単一 item で「Copy 後に Player 終了 → 貼り付け不可」が実際に起きるか（7.5 #21） |
| V-11 | Domain Reload / Scene Reload 双方無効の構成 | `Awake` が再実行されず `s_dispatcher` が捕捉されないまま dispatch 経路が死ぬ可能性 |
| V-12 | single-flight の粒度 | 操作単位のロックが実運用で過剰でないか。とくに `Read` / `Snapshot` / `CheckForegroundChange` のような読み取り系を UI から連打したときに 9001 が頻発しないかを実測する。過剰なら「同一操作の 2 件目を拒否する」から「読み取り系だけ並行を許す」への緩和を検討する（緩和は非破壊的変更） |

---

## 9. 決定事項（非対応範囲を含む）

v1 で未決だった事項をここで確定する。実装時に再検討しない。

| # | 事項 | 決定 |
| --- | --- | --- |
| D-1 | `ClipboardJsonReader` の共有化 | **改名する。** `git mv` で `.cs` と `.cs.meta` を対で移動し、ガードを 3 プラットフォームに拡張、クラス doc を中立化する |
| D-2 | `BOOL` のマーシャリング | **`[MarshalAs(UnmanagedType.I1)]` を付ける。** `MacShareManager` の素の `bool` は踏襲しない |
| D-3 | 事前検証の範囲 | **null 引数（9005）とサイズ上限（9007）のみ。** ネイティブと同じ条件は C# で検査しない |
| D-4 | 拒否経路の共通イベント取得元 | **`this` から取る**（`commonSelector` に `() => this.XxxCompleted` を渡す）。`_instance` に依存するのは static なネイティブコールバックだけ。保証範囲は「参照を保持している購読者を取りこぼさない」ことであり、破棄後に `Instance` から取り直した場合は別インスタンスなので届かない（5.6.2） |
| D-5 | `RunDestroyCleanup` のシグネチャ | **`stop` / `managedCleanup` の 2 引数版を新設する。** macOS に `cancelLoads` 相当は無い |
| D-6 | Editor 実行 | **非対応。** 既存 macOS Manager と同じく `Application.platform == RuntimePlatform.OSXPlayer` を要求する。Editor で xcframework をロードする要求が出た場合は別途設計する |
| D-7 | Awaitable 版 | **本計画では実装しない。** パッケージ全体に前例が無く、後から非破壊的に追加できる |
| D-8 | Manager の破棄・再生成 | **非対応。** 破棄後は tombstone により全操作が 9004 を返す（5.6.1） |
| D-9 | 非 ASCII の JSON 出力 | **生の UTF-8 のまま出力する。** `\uXXXX` へエスケープしない |
| D-10 | 操作名定数とサイズ上限の置き場所 | **`MacClipboardConstants.cs`（段 2）に置く。** Manager に置くと Parser が段 3 に依存し、段 2 が単独でコンパイルできなくなる（5.2.1 / 5.2.2） |
| D-11 | `TryStartOperation` の構造 | **`TryPassGuards`（段階 1〜4）と `TryBeginOperation`（段階 6）に分割する。** その間で JSON を構築し、in-flight 取得後の OOM によるマーカーリークを構造的に排除する（5.6.4） |
| D-12 | Editor 完了シームの粒度 | **操作ごとに 1 本。** observation は専用の `CompleteObservationControlForTests` を持つ（5.6.10） |

---

## 10. Definition of Done

各段（12 章）の完了条件。すべて満たすまで次の段に進まない。

- [ ] 対象範囲の Runtime ファイルがすべて作成され、コンパイルエラーが無い
- [ ] `public` メンバに英語の XML ドキュメントコメントが付いている（`csharp.md`）
- [ ] ログが shape / count / flag のみで、clipboard 本文と pasteboard 名を出していない（5.6.11）
- [ ] 逸脱の宣言が既存の使い分け（クラス `<summary>` / ファイル先頭 `//`）に従っている
- [ ] 対象範囲の新規テストがすべて作成され、Unity Test Runner の EditMode / PlayMode で pass する
- [ ] **既存テスト（iOS / Android / Share / Notification）が全件 pass する**
- [ ] EditMode テストが Manager インスタンスを生成していない（`testing.md` 1 節）
- [ ] PlayMode テストが `[TearDown]` で `ResetForTests()` を呼んでいる
- [ ] `.meta` を新規作成していない。改名は `git mv` で `.cs` と `.cs.meta` を対で移動している（`common.md`「ファイル作成ルール」に対する**承認済みの逸脱**。D-1 / 4.2 が根拠）
- [ ] ネイティブ側が未検証と明記している事項（`localOnly` の Universal Clipboard への効果、読み出し時のユーザー通知）を、対応する public メンバの XML コメントに引き写している
- [ ] single-flight の公開トレードオフ（同一操作は並行不可・異なる操作は並行可・2 件目は 9001 で即失敗）を Manager クラスの XML コメントに書いている
- [ ] 8 章の要検証事項のうち、その段で判明したものが実測値で更新されている

段 4 完了時の追加条件:

- [ ] 7.5 の手動確認 32 項目を macOS Standalone Player で実施し、結果を実装結果ファイルに記録している
- [ ] V-1 〜 V-12 のすべてに結論または継続課題としての記載がある
- [ ] `agent-rules/coding-rules/testing.md` 7 節の層 1 カバレッジ表で、Clipboard の macOS 列を「対象外」から「実装済み」に更新している

---

## 11. 採用しなかった案

single-flight と tombstone はいずれも**ネイティブ ABI の制約への回避策**であり、恒久解ではない。次に native を触るときの判断材料として記録する。

| 案 | 却下した理由 |
| --- | --- |
| **FIFO キューで同一操作の複数呼び出しを直列化する** | C ABI にリクエスト ID が無いため、届いたコールバックがキューのどの要素に対応するか判定できない。先頭から順に割り当てるとネイティブ側の完了順が前後した場合に取り違える |
| **per-call callback を last-registered wins にする（`MacShareManager` 方式）** | 捨てられた callback は永久に呼ばれない。`common.md` はこの方式のまま `Awaitable` 版を作ることを禁じており、将来の拡張を塞ぐ |
| **ネイティブ ABI に request ID / lifetime ID を追加する** | **これが恒久解である。** リクエスト ID があれば single-flight は不要になり、lifetime ID があれば tombstone による一律破棄が不要になる。ただし native-toolkit 側の変更と全プラットフォームの ABI 更新を伴うため、本計画の範囲外とする |
| **`_instance = null` をやめて破棄後も共通イベントを配送する** | `Instance` getter の Unity `==` が破棄済みオブジェクトを null と判定するため、結局新しい GameObject が生成される。この案でも「破棄後に `Instance` から取り直した購読者ゼロのインスタンス」は避けられず、決定 2 と保証範囲は変わらない。`_instance` を残すと破棄済み MonoBehaviour への参照が生き続ける分だけ副作用が大きい |
| **`Instance` getter を破棄後に例外へ変える** | 既存 macOS Manager と挙動が揃わず、`Instance` を触るだけで落ちる API になる。tombstone による 9004 の方が呼び出し側で扱いやすい（`IsTerminated` で明示的に検出できる） |
| **サイズ上限をネイティブと同じ 100 MiB / 200 MiB にする** | base64 化で 4/3 に膨らんだ managed string が 133 MB / 266 MB になり、Unity の managed heap を圧迫する。C# 側は独自のより厳しい上限を持つべき |

---

## 12. 実装分割

**1 回の implement-feature で通さず 4 段に分ける。** リスクが Manager コアに集中しているため、その段を小さく保つことに最大の価値がある。

| 段 | 範囲 | 単独マージ可否の根拠 |
| --- | --- | --- |
| 1 | `ClipboardJsonReader.cs` への改名 + ガード拡張、`IosClipboardJsonParser.cs` と iOS テストの置換 | 振る舞いの変更ゼロ。既存 iOS テストの全 pass が完了条件 |
| 2 | `MacClipboardPayloads` / `MacClipboardErrorInfo` / **`MacClipboardConstants`** / 結果型 12 種（10 ファイル）/ `MacClipboardChangeEvent` / Builder / Parser + EditMode 3 テスト | ネイティブにも Manager にも依存しない。**操作名とサイズ上限を `MacClipboardConstants.cs` に置くことで、Parser が段 3 を参照せず単独でコンパイルできる**（D-10） |
| 3 | Manager 骨格（Singleton / lifecycle / tombstone / dispatcher / ガードチェーン / dispatch / per-call スロット / テストシーム）+ Copy / Append / Read / ReadData / Clear + Dispatch テスト + PlayMode テスト | 設計上のリスク（5.6.1 / 5.6.3 / 5.6.4 / 5.6.6 / 5.6.8）がすべてこの段に集中する |
| 4 | Snapshot / CreatePasteboard / RemovePasteboard / Detect×3 / AccessBehavior / CheckForegroundChange / Start・StopObserving + 監視の世代管理 | 3 で確立したパターンの反復。監視は新機構なので必要なら 4a（単発操作）/ 4b（監視）に分割してよい |

- **Editor 完了シーム（5.6.10）は、対応する操作と同じ段で追加する。** 段 3 で入るのは Ownership / Read / ReadData / Clear と観測系（`IsInFlightForTests` など）だけで、Detect / AccessBehavior / ForegroundChange / Snapshot / CreatePasteboard / ObservationControl のシームは段 4 に入る
- **段 3 が宣言するのは、自分が実装する 5 操作の公開イベント 4 本（`OwnershipChanged` / `ReadCompleted` / `ReadDataCompleted` / `ClearCompleted`）と `ClipboardChanged` の計 5 本。** 5.6.2 の公開イベント 13 本のうち残り 8 本は段 4 で追加する。段 2 で結果型 12 種はすべて揃っているので、どちらの順でもコンパイルは通る
- **teardown（5.6.8）は段 4 の監視機構に一部依存するため、段 3 に次の 3 点を先に入れる**（公開 API の `StartObserving` / `StopObserving` は段 4 のまま）
  1. `clipboardStopObserving` の `extern` 宣言と `StopObservingForTeardown`
  2. 監視の static 状態 4 件（`s_observingGeneration` / `s_onChangedGeneration` / `s_pendingObservationGeneration` / `s_onChanged`）と `ClearAllPendingCallbacks` でのリセット
  3. **変更コールバックの配送経路一式**: `ClipboardChanged` イベント、`MacClipboardChangeEvent` の parse、`FireClipboardChanged`、`DiscardIfTerminated` の適用、および `DeliverChangeEventForTests`。7.2 の「破棄後に届いた変更イベントが捨てられること」を段 3 で検証するために必要
- **段 3 に入るシーム**: `ResetForTests` / `BridgeAvailableOverrideForTests` / `MaxRequestBytesOverrideForTests` / `CompleteOwnershipForTests` / `CompleteReadForTests` / `CompleteReadDataForTests` / `CompleteClearForTests` / `DeliverChangeEventForTests` / 観測系 4 本（`IsInFlightForTests` / `InFlightCountForTests` / `HasChangeRegistrationForTests` / `HasAnyPendingCallbackForTests`）
- **段 4 に入るシーム**: `CompleteOperationForTests`（removePasteboard）/ `CompleteObservationControlForTests` / `CompleteSnapshotForTests` / `CompleteCreatePasteboardForTests` / `CompleteDetectPatternsForTests` / `CompleteDetectValuesForTests` / `CompleteDetectMetadataForTests` / `CompleteAccessBehaviorForTests` / `CompleteForegroundChangeForTests` / `PendingObservationGenerationForTests`

- **段 2 と段 3 の間に一度レビューを挟む**（`review-implementation-feature`）
- 段 1 の着手前に V-1（xcframework とソースの版対応）を確認する
- 段 3 の着手前に、5.6.2 の決定 2 の保証範囲・5.6.4 の `TryPassGuards` 分割・5.6.10 のシーム粒度を実装者が理解していることを確認する（v2 レビューで「書いてあるとおりに実装すると破綻する」と指摘された 3 点）

---

## 13. 出力範囲の明記

本計画書に**含まないもの**:

- サンプルアプリ: `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs`、UXML / USS、サンプルシーン、`NativeToolkitSampleNavigator` への導線、`MacClipboardSampleSceneWiringTests` → `design-sample-scene` スキル
- マニュアル（`Documentation~/` / `docs/`）→ `write-manual` スキル
- ネイティブ（native-toolkit）側の変更 → 15 関数が実装済み・エクスポート済みのため不要（ただし V-1 の版対応確認は行う）
- `package.json` のバージョン更新、CHANGELOG、リリース作業 → `release` スキル
- `MacShareManager.cs:59` の素の `bool` 宣言の修正 → 別課題
