# iOS Clipboard サンプルシーン実装計画 v2

## 基本情報

- 日付: 2026-08-16
- 機能名: clipboard
- 対象プラットフォーム: iOS
- 出力言語: 日本語（**計画書の記述言語のみ。実装コード内の文言・コメントは英語**）
- 実装結果: `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v3.md`（レビュー LGTM）
- 実装計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 前版: `artifact/designs/clipboard/2026-08-15-ios-clipboard-sample-scene-design-v1.md`
- 対象レビュー: `artifact/reviews/clipboard/2026-08-16-ios-clipboard-sample-scene-design-review-v1.md`（総合評価「要修正」）
- 後続工程: review-document → implement-sample-scene

### v1 からの変更点（レビュー反映）

| 指摘 | severity | 対応 |
|---|---|---|
| 単一 `_pendingMarker` + 共通イベントでは、異種操作の並行実行で結果が別 marker に誤対応する | high | **全面的に受け入れ**。6.4 を書き換え、**呼び出しごとの immutable な `ResultContext` を per-call callback が capture** する方式へ変更。共通イベントは shape-only ログ専用にし、UI 更新と scope 変更は per-call callback の所有物とした。Create / Remove は開始時 scope も capture し、Remove 成功時の active 復帰に所有条件を付けた（6.4.3） |
| `OnDisable` の無条件 `StopObserving()` は Start pending 中に busy となり監視が残る | medium | 6.5 に **observation teardown 状態機械**（`_observationControlPending` / `_isObserving` / `_stopRequestedAfterStart`）を新設。Start pending 中の離脱は Start 完了 callback が Stop を発行する。observation 中の enabled 契約表も追加（4.7） |
| Editor の TopMenu 導線（ダイアログ）と Editor 手動確認（S-3〜S-5 / V-1）が両立していない | medium | 5.2 / 6.8 を変更。Editor のダイアログを **2 択（Open Sample Screen / Close）** にし、選択時に `ShowIosClipboard` へ遷移する。Clipboard 分岐のみの変更とし、他 3 機能は現状維持（理由も明記） |
| M-1 / M-4 / M-14 / M-16 / M-22 の fixture と手順が成立しない | medium | 4.5 に **fixture 表**を新設し、手動手順と 1 対 1 対応させた。日本語・絵文字を実際に含め、長さで識別できる 3 種の body を用意し、Cancel demo は seed copy を内包、M-16 を 2 段に分割、M-22 用に別 fixture とボタンを追加 |
| `Texture2D` と `LoadItem(File)` の一時リソース解放が未定義 | medium | 4.6 に **リソース所有契約**を新設。`Texture2D` は `try/finally` で `Destroy`、`LoadItem(File)` は size 取得後に request directory を削除し、失敗時は `cleanup=failed` を表示 |
| C# 例外ケースを「ボタン化するとクラッシュする」と断定している | low | **指摘は正しい**。try/catch すれば表示できるため断定を撤回。ボタンを追加しない方針は維持し、**注記の確定英語文言と wiring 対象 label を計画で固定**した（4.4） |
| M-23 の実施主体・手順が不明 | 不足 | 7.4 に**引き継ぎ先と手順**を明記（サンプル外） |
| marker correlation / observation teardown の状態テストがない | 不足 | 6.7 に**純粋 helper への切り出しと EditMode テスト**を新設 |
| observation 中の enabled 状態と status の契約がない | 不足 | 4.7 に契約表を新設 |

---

## 1. 実装結果から抽出した前提

### 1.1 実装済みの公開 API（15 操作）

すべて `IosClipboardManager`（`#if UNITY_IOS || UNITY_EDITOR`）の instance メソッド。全操作が「共通 event → 任意の per-call callback」の順で結果を返す。

| 操作 | シグネチャ（末尾に `Action<TResult>? onResult = null`） | 結果型 |
|---|---|---|
| `Copy` | `(IosClipboardContent, IosPasteboardScope?, IosClipboardCopyOptions?)` | `IosClipboardOperationResult` |
| `Append` | `(IosClipboardContent, IosPasteboardScope?)` | 同上 |
| `Clear` | `(IosPasteboardScope?)` | 同上 |
| `RemovePasteboard` | `(IosPasteboardScope)` **必須** | 同上 |
| `CancelLoads` | `()` | 同上 |
| `StartObserving` | `(IosPasteboardScope?, Action<IosClipboardChangeEvent>?)` ＋ `onStarted` | 同上（+ `ClipboardChanged`） |
| `StopObserving` | `()` | 同上 |
| `Read` | `(IosPasteboardScope?)` | `IosClipboardReadResult` |
| `ReadData` | `(string utType, IosPasteboardScope?)` | `IosClipboardReadDataResult` |
| `GetSnapshot` | `(IosPasteboardScope?, string[]? matchingTypes)` | `IosClipboardSnapshotResult` |
| `CreatePasteboard` | `(IosPasteboardCreationRequest)` | `IosPasteboardScopeResult` |
| `DetectPatterns` | `(IosClipboardDetectionPattern[], IosPasteboardScope?)` | `IosClipboardDetectedPatternsResult` |
| `DetectValues` | `(IosClipboardDetectionPattern[], IosPasteboardScope?)` | `IosClipboardDetectedValuesResult` |
| `LoadItem` | `(IosClipboardLoadRequest, IosPasteboardScope?)` | `IosClipboardLoadedItemResult` |
| `CheckForegroundChange` | `(IosPasteboardScope?)` | `IosClipboardForegroundChangeResult` |

公開イベントは 10 個。

### 1.2 並行実行の契約（本計画の設計を規定する最重要事項）

| 契約 | 内容 |
|---|---|
| **single-flight は「同一操作」のみ** | `Read` と `GetSnapshot`、`LoadItem` と `CancelLoads` は**同時に走る**。実装計画 v5 の 5.6.3 |
| 同一操作の 2 本目 | `CLIPBOARD_BUSY` で即時拒否。**進行中の呼び出しには一切触れない**（rejected 専用 dispatch） |
| `StartObserving` / `StopObserving` | 共有キー `observation` で**互いに直列化**される。片方が pending 中はもう片方が busy |
| per-call callback | 呼び出しごとに 1 回だけ呼ばれ、**その呼び出しの結果のみ**を受け取る |
| 共通イベント | per-call callback の有無に関わらず必ず発火するが、**どの呼び出しの結果かを識別する手段を持たない**（`ClipboardOperationCompleted` の `Operation` は操作種別のみ） |
| `LoadItem` の所要時間 | 最大 15 秒（provider load timeout） |

**したがってサンプルの結果表示は per-call callback を基準にする**（6.4）。

### 1.3 入力制約

| 制約 | 内容 |
|---|---|
| `IosPasteboardScope.Named/Unique` | 空・空白名は `ArgumentException` |
| `IosClipboardContent.Color` | **非有限値**（NaN / Infinity）は `ArgumentException`。0.0〜1.0 の範囲外は例外にならず native の `CLIPBOARD_INVALID_COLOR` |
| `IosClipboardContent.*` | `null` 引数は `ArgumentNullException` |
| `RemovePasteboard` | `scope` 必須 |
| `DetectPatterns` / `DetectValues` | 空配列は `CLIPBOARD_EMPTY_PATTERNS`（native 到達前） |
| main thread 限定 | `Instance` getter を含む。UI Toolkit のコールバックから呼ぶ限り常に満たされる |
| 破棄後 | 全操作が `CLIPBOARD_MANAGER_DESTROYED`。**サンプルは Manager を破棄しない** |

### 1.4 エラー契約

- 結果型は `IsSuccess` と `Error`（`IosClipboardErrorInfo?`）。**`ErrorMessage` は直下に存在しない**（`result.Error?.Message`）
- `Error` は `Code` / `Message`（非 null）＋ `Domain` / `NativeCode`（`details` があるときのみ）
- `CLIPBOARD_CANCELLED` は正常な打ち切りとして扱ってよい
- `ReadData` の「該当データなし」は失敗ではなく `HasData == false` の成功
- Editor では全操作が `CLIPBOARD_BRIDGE_UNAVAILABLE`

### 1.5 不足前提（サンプル側で補わない）

| 項目 | 内容 |
|---|---|
| `Awaitable` 版 | 未実装。callback 版のみ使う |
| Paste Control（P-16） | Unity Bridge 未公開。native サンプルの `pasteControlSection` は移植しない |
| 制限値・タイムアウトの変更 | Unity から変更不可 |
| 同梱画像アセット | 無い。PNG はコード内生成（4.5） |

---

## 2. 既存サンプルコードの深掘り結果

### 2.1 確認したもの

| 対象 | 確認内容 |
|---|---|
| `UI/iOS/Share/IosShareManagerExampleController.cs`（426 行） | **iOS の Controller パターン**。ハンドラ内にプラットフォームガードを置かない |
| `UI/Android/Clipboard/AndroidClipboardManagerExampleController.cs`（686 行） | clipboard 固有のログ規約、結果 ScrollView |
| `Resources/UI/iOS/Share/*.uxml` / `.uss` | `ios-*` クラス命名 |
| `UI/Common/NativeToolkitSampleNavigator.cs`（202 行） | `ApplyScreen<T>` による動的差し替え |
| `UI/Top/TopMenuExampleController.cs`（191 行） | Editor はダイアログ、Player はプラットフォーム分岐 |
| `Tests/Runtime/AndroidClipboardSampleSceneWiringTests.cs` | UXML / Controller の name 不一致検出 |
| native `ios/IosLibraryExample/ClipboardSampleView.swift`（1,070 行） | 機能一覧・fixture・結果表示・enabled 制御 |

### 2.2 native サンプルとの対応

| native セクション | Unity への反映 |
|---|---|
| `scopeSection`（6） | 反映する |
| `copySection`（12） | 反映する |
| `copyOptionsSection`（4） | 反映する |
| `appendSection`（3） | 反映する |
| `readSection`（4） | 反映する |
| `loadSection`（5） | 反映する |
| `detectSection`（4） | 反映する |
| `observeSection`（3） | 反映する |
| `pasteControlSection` | **反映しない**（P-16 未公開） |
| `clearSection`（1） | 反映する |
| `errorSection`（12） | 反映する（Paste Control 由来 2 種を除外して 10） |

**native から取り込む実装ディテール（v2 で追加）:**

| 取り込み | native の該当箇所 |
|---|---|
| **長さで識別できる fixture** | `localOnlyBody`（14 文字）/ `deviceBBaseline`（31 文字）/ `appendMarker`（24 文字）。「値を出さずに、どの fixture が届いたかを長さだけで判別する」ための設計 |
| **64 バイト固定の file fixture** | `fileFixturePayload = Data(repeating: 0x41, count: 64)`。`Load File` が `fileSize=64` を assert できる |
| **request directory の削除** | `url.deletingLastPathComponent()` を `removeItem`、失敗時は `cleanup=failed` |
| **observation 中の enabled 制御** | scope セクション・Start・観測系エラーボタンを `.disabled(isObserving)`、Stop を `.disabled(!isObserving)` |
| **detection fixture の分割** | 複合 fixture では `number` / `probableWebSearch` が検出されないため、`numberFixture` / `searchFixture` を別に持つ |

**Unity 側で追加するもの（native に無い）:**

| 追加 | 理由 |
|---|---|
| Busy（single-flight）セクション | `CLIPBOARD_BUSY` は Unity Bridge 固有の契約 |
| Editor 実行時の案内 | Unity 固有。全操作が B-1 になる |
| 数 MiB の ImageData fixture | M-22（response 側メモリ）の導線。native サンプルには対応物が無い |

### 2.3 再利用する既存コンポーネント

| コンポーネント | 再利用方法 |
|---|---|
| `NativeToolkitSampleNavigator` | `ShowIosClipboard` を追加して同じ `ApplyScreen<T>` 経路に乗せる |
| `TopMenuExampleController` | Clipboard ボタンのガード拡張と Editor 導線の追加 |
| 結果 ScrollView 構造 | `AndroidClipboardManagerExample.uxml` の `ResultScrollView` + `ResultTextBlock` |
| `ios-secondary-button` 等のクラス命名 | `IosShareManagerExampleStyle.uss` に合わせる（USS はスクリーン単位で差し替わるため共有はしない） |

### 2.4 追加するコンポーネント

| 追加 | 役割 |
|---|---|
| `IosClipboardManagerExampleController` | 15 操作 + イベント購読 + 状態管理 |
| `IosClipboardSampleResult`（`internal static`） | **結果行の整形（純粋関数）**。EditMode でテストする |
| `IosClipboardSampleObservationState`（`internal struct`） | **observation teardown の状態遷移（純粋）**。EditMode でテストする |
| `IosClipboardManagerExample.uxml` / `...Style.uss` | 画面定義とスタイル |
| `IosClipboardSampleSceneWiringTests` | UXML / Controller の name 不一致検出 |
| `IosClipboardSampleStateTests` | 上記 2 helper の状態テスト |

### 2.5 変更するファイルと理由

| ファイル | 変更理由 |
|---|---|
| `UI/Common/NativeToolkitSampleNavigator.cs` | `ShowIosClipboard` が無いと遷移できない |
| `UI/Top/TopMenuExampleController.cs` | Clipboard ボタンが Android 限定で iOS では非表示。加えて Editor から画面へ到達できず S-3〜S-5 / V-1 が実行できない |

---

## 3. 共通実装パターンの維持と拡張

### 3.1 維持するもの

TopMenu 導線 / 先頭のタイトルと結果表示 / 機能カテゴリ単位のボタン群 / 成否が一目で分かる文言 / `OnEnable`・`OnDisable` でのイベント購読管理 / API 呼び出し前のログ / `Start` で `UIDocument` 解決 / `OnDestroy` で `clicked` 解除。

### 3.2 拡張するもの

| 拡張 | 内容 | 理由 |
|---|---|---|
| **per-call callback による結果対応付け** | 各呼び出しが `ResultContext` を capture する | Manager は異種操作の並行実行を許すため、共通イベントだけでは結果を呼び出しへ対応付けられない（1.2） |
| ステータス行 | `Scope: <label> \| Observing: <state> \| Events: <n>` | scope と observation は複数操作にまたがる状態 |
| 結果行に連番とマーカー | `#12 [copy.plainText] OK ...` | 同じ操作の連続実行で更新の有無が分かる |
| ボタンの enabled 制御 | observation 状態に連動（4.7） | native サンプルと同じ。矛盾操作を防ぐ |
| `SetResult` のログ抑止を既定に | `logMessage: false` を既定 | iOS は結果のほぼ全てが clipboard 由来 |
| `UnityMainThreadDispatcher` を Controller から使わない | Manager が全結果を main thread に載せる | 二重 dispatch を避ける |

### 3.3 ログ・表示規約

| 区分 | ログ | 画面表示 |
|---|---|---|
| 操作名・成否・エラーコード・件数・バイト数・`kind`・`utType` | 出す | 出す |
| clipboard 本文（`Text` / `UrlString` / `Path` / base64 / representations） | **出さない** | **長さ・有無に丸める** |
| 検出値 | **出さない** | **件数のみ** |
| pasteboard 名 | **出さない** | **長さのみ**（`named(len=12)`） |

---

## 4. 画面要件

### 4.1 画面構成

```
[Back To Home]
IosClipboardManager Example
<subtitle 1: 実機 iOS 18+ 用。Editor では全操作が CLIPBOARD_BRIDGE_UNAVAILABLE を返す>
<subtitle 2: 入力欄なし。値は固定 fixture。C# 側で例外になる入力はボタン化していない（4.4）>
┌ ResultScrollView ─────────────────┐
│ ResultTextBlock                   │  ← #seq [marker] OK/NG/-- 詳細
└───────────────────────────────────┘
StatusTextBlock                        ← Scope: general | Observing: off | Events: 0
┌ scroll ───────────────────────────┐
│ Scope / Copy / Copy Options /     │
│ Append / Read / Load / Detect /   │
│ Observe / Clear / Busy / Errors   │
└───────────────────────────────────┘
```

### 4.2 操作導線

1. 既定の active scope は `general`
2. Scope セクションで active scope を切り替える
3. 以降の scope 対応操作は**すべて active scope に対して実行**する
4. 結果は `ResultTextBlock` に 1 行、状態は `StatusTextBlock` に反映

入力欄は設けない。値はコード内の固定 fixture（4.5）。

### 4.3 セクションとボタン一覧（計 53 ボタン）

v1 の 51 から、M-22 用の 1 個と M-16 用の 1 個を追加。

#### Scope（6）

| name | text | 動作 |
|---|---|---|
| `UseGeneralButton` | Use General | active scope を `General` に |
| `CreateNamedPasteboardButton` | Create Named Pasteboard | `CreatePasteboard(Named(FixedName))` → 成功 scope を active に |
| `UseFixedNamedScopeButton` | Use Fixed Named Scope (no create) | 作成せず `Named(FixedName)` を active に |
| `CreateUniquePasteboardButton` | Create Unique Pasteboard | `CreatePasteboard(Unique)` → 生成名つき scope を active に |
| `RemoveActivePasteboardButton` | Remove Active Pasteboard | `RemovePasteboard(active)` → 成功かつ active が未変更なら `General` へ戻す |
| `ProbeRemovedScopeButton` | Probe Last Removed Scope | 直前に削除した scope へ `Read` |

#### Copy（11）

`CopyPlainTextButton` / `CopyEmptyPlainTextButton` / `CopyHtmlTextButton` / `CopyUrlButton` / `CopyImageFileButton` / `CopyImageDataButton` / `CopyColorButton` / `CopyCustomDataButton` / `CopyMultipleTextButton` / `CopyMultiRepresentationButton` / `CopyDetectionFixtureButton`

#### Copy Options（4）

| name | 動作 | 対応 |
|---|---|---|
| `CopyLocalOnlyTrueButton` | `LocalOnlyBody`（14 文字）を `localOnly: true` で copy | M-4 対照 |
| `CopyLocalOnlyFalseButton` | `LocalOnlyBody`（14 文字）を `localOnly: false` で copy | M-4 |
| `CopyDeviceBaselineButton` | `DeviceBBaseline`（31 文字）を copy | M-4 の device B 基準値 |
| `CopyExpiringButton` | 30 秒後に失効する copy | M-5 |

#### Append（2）

`AppendPlainTextButton`（`AppendMarker` 24 文字）/ `AppendUrlButton`

#### Read（4）

`ReadButton` / `ReadDataPngButton` / `SnapshotButton` / `SnapshotMatchingButton`

#### Load（5）

`LoadTextButton` / `LoadUrlButton` / `LoadImageButton` / `LoadFileButton` / `CancelLoadsButton`

#### Detect（4）

`CopyNumberFixtureButton` / `CopySearchFixtureButton` / `DetectPatternsButton` / `DetectValuesButton`

#### Observe（3）

`StartObservingButton` / `StopObservingButton` / `CheckForegroundChangeButton`

#### Clear（1）

`ClearActiveScopeButton`

#### Busy / Memory（4）— Unity 固有

| name | 動作 | 対応 |
|---|---|---|
| `BusyLoadItemTwiceButton` | `LoadItem(Text)` を連続 2 回。**context を 2 個**作り、1 本目と 2 本目が別 sequence で表示される | M-24 |
| `SeedAndCancelLoadButton` | `Copy(ImageData)` → 成功 callback 内で `LoadItem(Image)` を開始し、直後に `CancelLoads` | M-14 |
| `BusyStartObservingTwiceButton` | `StartObserving` を連続 2 回。2 本目が busy | M-16 前半 |
| `CopyLargeImageDataButton` | **数 MiB** の ImageData を copy（4.5） | M-22 |

#### Errors（10）

| name | 期待コード |
|---|---|
| `ErrCopyMultipleEmptyButton` | `CLIPBOARD_EMPTY_ITEMS` |
| `ErrCopyMultiRepEmptyButton` | `CLIPBOARD_EMPTY_ITEMS` |
| `ErrCopyImageFileMissingButton` | `CLIPBOARD_FILE_NOT_FOUND` |
| `ErrCopyInvalidUtiButton` | `CLIPBOARD_INVALID_TYPE` |
| `ErrCopyInvalidUrlButton` | `CLIPBOARD_INVALID_URL` |
| `ErrCopyColorOutOfRangeButton` | `CLIPBOARD_INVALID_COLOR`（**有限値**なので C# 例外にならない） |
| `ErrReadDataInvalidUtiButton` | `CLIPBOARD_INVALID_TYPE` |
| `ErrRemoveGeneralButton` | `CLIPBOARD_CANNOT_REMOVE_GENERAL` |
| `ErrObserveMissingNamedButton` | `CLIPBOARD_UNAVAILABLE` |
| `ErrDetectEmptyPatternsButton` | `CLIPBOARD_EMPTY_PATTERNS` |

### 4.4 C# 例外になる入力の扱い（v1 の断定を撤回）

`IosPasteboardScope.Named("")` と `IosClipboardContent.Color(NaN, ...)` は `ArgumentException` を投げる。**try/catch すればサンプルをクラッシュさせずに表示できる**ため、「ボタン化するとクラッシュする」という v1 の記述は誤りだった。

**方針: ボタンは追加しない。** これらは Manager の呼び出しに到達しない呼び出し側バグであり、native のエラー契約デモとは性質が異なる。ボタン数も増える。

**確定する文言**（実装者判断に委ねない）:

- subtitle 2（`SubtitleValidationLabel`）:
  `"Inputs that would throw in C# (blank pasteboard name, non-finite color) are not exposed as buttons; only native error contracts are shown below."`
- Errors セクション note（`ErrorsSectionNote`）:
  `"Every button here reaches the native layer and returns a CLIPBOARD_* code. Argument errors that fail before the bridge are not represented."`

両 label は wiring テストの必須 name 対象に含める（6.7）。

### 4.5 fixture 表（手動確認と 1 対 1 対応）

| fixture | 値 | 使用ボタン | 対応 M |
|---|---|---|---|
| `PlainTextBody` | `"Hello 日本語 \U0001F680 テスト"`（**日本語・絵文字・サロゲートペアを含む**） | `CopyPlainTextButton` | M-1 |
| `LocalOnlyBody` | 14 文字の固定文字列 | `CopyLocalOnlyTrue/FalseButton` | M-4 |
| `DeviceBBaseline` | 31 文字（`B` の繰り返し） | `CopyDeviceBaselineButton` | M-4 |
| `AppendMarker` | 24 文字（`"APPENDED-MARKER-"` + GUID 先頭 8 文字） | `AppendPlainTextButton` | M-6 |
| `FileFixturePayload` | **64 バイト**の `0x41` | `CopyCustomDataButton`（`public.data`） | M-13（`fileSize=64` を assert） |
| `DetectionFixture` | URL / email / 電話 / 住所 / 日時 / 便名 / 金額 / 追跡番号 を含む複合テキスト | `CopyDetectionFixtureButton` | M-12 |
| `NumberFixture` | `"42"` | `CopyNumberFixtureButton` | M-12（複合 fixture では検出されない） |
| `SearchFixture` | `"swift concurrency"` | `CopySearchFixtureButton` | M-12（同上） |
| `SmallPng` | 1x1 PNG（コード生成） | `CopyImageDataButton` / `CopyImageFileButton` / `LoadImageButton` | M-3 / M-13 |
| `LargePng` | **約 4 MiB**（1024x1024 のノイズ画像。圧縮が効かないようピクセルをランダム化） | `CopyLargeImageDataButton` | M-22 |
| `FixedName` | `"com.jonghyunkim.nativetoolkit.example.sample"` | Scope セクション | M-9 |
| `CustomTypeIdentifier` | `"com.jonghyunkim.nativetoolkit.example.custom"` | `CopyCustomDataButton` | M-13 |

**長さで識別する設計**: `LocalOnlyBody`(14) / `AppendMarker`(24) / `DeviceBBaseline`(31) は互いに長さが異なる。`Read` の表示に `textLen=<n>` を含めることで、**本文を出さずにどの fixture が届いたかを判別できる**（M-4 / M-6 に必須）。

### 4.6 リソース所有契約

| リソース | 契約 |
|---|---|
| `Texture2D`（PNG 生成） | `try { ... EncodeToPNG() } finally { UnityEngine.Object.Destroy(texture); }`。生成 helper 内で完結し、Texture を外へ返さない |
| `LargePng` のバイト列 | ボタン押下ごとに生成し、`Copy` 呼び出し後は参照を保持しない（GC 対象にする） |
| `ImageFile` の書き出し先 | `Application.persistentDataPath` 直下の固定ファイル名。上書きするため蓄積しない |
| **`LoadItem(File)` の返却パス** | **caller-owned**。`IosClipboardLoadedItem.Path` の XML コメントどおり、呼び出し側に削除責任がある |

**`LoadItem(File)` 成功時の処理（native `ClipboardSampleView` L834-841 に合わせる）:**

1. `new FileInfo(path).Length` で size を取得
2. `Path.GetDirectoryName(path)` を **request directory** とみなし `Directory.Delete(dir, recursive: true)`
3. 成功: `fileSize=<n>` を表示
4. 失敗: `fileSize=<n>, cleanup=failed` を表示し、`Debug.LogWarning` に**例外メッセージのみ**（パスは出さない）

**パスは表示にもログにも出さない。** 長さも出さない（v1 の `pathLen` は削除。サイズだけで用は足り、パスは一時ディレクトリ名を含む）。

削除範囲は「返却ファイルの親ディレクトリ」に固定する。これは native が request ごとに一時ディレクトリを作る契約に依存するため、**要検証 V-2** とする。

### 4.7 observation 状態と enabled 契約

| 状態 | `_isObserving` | `_observationControlPending` |
|---|---|---|
| 未観測 | false | false |
| Start 実行中 | false | true |
| 観測中 | true | false |
| Stop 実行中 | true | true |

| 要素 | enabled 条件 |
|---|---|
| Scope セクションの 6 ボタン | `!_isObserving && !_observationControlPending` |
| `StartObservingButton` | `!_isObserving && !_observationControlPending` |
| `StopObservingButton` | `_isObserving && !_observationControlPending` |
| `ErrObserveMissingNamedButton` | `!_isObserving && !_observationControlPending` |
| `BusyStartObservingTwiceButton` | `!_isObserving && !_observationControlPending` |
| 上記以外 | 常に enabled |

**status の scope 表記**: 観測中は `Scope: <active> (observing <observed>)` とし、**観測開始時に capture した scope** を併記する。scope ボタンを無効化しているため通常は一致するが、`CreatePasteboard` の完了が観測開始後に届く可能性があるため、表示上区別できるようにする。

### 4.8 結果表示

```
#12 [copy.plainText] OK
#13 [read] OK items=2 firstItemTypes=1 textLen=14
#14 [copy.plainText] NG code=CLIPBOARD_EMPTY_CONTENT message=Clipboard content is empty. Please provide text or HTML.
#15 [load.image] -- code=CLIPBOARD_CANCELLED message=The clipboard load was cancelled.
```

- `OK` / `NG` / `--`（`CLIPBOARD_CANCELLED` のみ）
- `Error.Domain` / `NativeCode` があるときのみ ` details=<domain>:<code>`
- 成功時の payload は 4.9

### 4.9 成功時の表示（content を丸める）

| 操作 | 表示内容 |
|---|---|
| `Read` | `items=<n> firstItemTypes=<n> textLen=<n or ->`（`textLen` は最初の text を持つ item の長さ。**本文は出さない**） |
| `ReadData` | `hasData=<bool> utType=<utType> bytes=<n>` |
| `GetSnapshot` | `items=<n> strings=<b> urls=<b> images=<b> colors=<b> matching=<null or n>` |
| `CreatePasteboard` | `scope=<kind>(len=<n>)` |
| `DetectPatterns` | `patterns=<n>` |
| `DetectValues` | `patterns=<n> emails=<n> phones=<n> addresses=<n> events=<n> flights=<n> money=<n> shipments=<n> links=<n>` |
| `LoadItem` | `kind=<kind>` ＋ Text は `textLen=<n>`、ImageData は `bytes=<n> utType=<t>`、File は `fileSize=<n>`（+ `cleanup=failed`） |
| `CheckForegroundChange` | `changed=<bool>` |
| `ClipboardChanged` | `kind=<kind> added=<n> removed=<n> scope=<label>` |

---

## 5. 変更ファイル一覧

`.meta` は Unity が自動生成するため記載しない。

### 5.1 新規作成

| パス | 内容 |
|---|---|
| `Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs` | Controller |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleResult.cs` | 結果行整形の純粋 helper（`internal static`） |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleObservationState.cs` | observation 状態遷移の純粋 helper（`internal struct`） |
| `Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml` | 画面定義 |
| `Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExampleStyle.uss` | スタイル |
| `Tests/Runtime/IosClipboardSampleSceneWiringTests.cs` | wiring テスト |
| `Tests/Runtime/IosClipboardSampleStateTests.cs` | 結果 context / observation 状態のテスト |

### 5.2 既存変更

| パス | 変更内容 |
|---|---|
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowIosClipboard` の追加（`#if UNITY_IOS \|\| UNITY_EDITOR`）と `RemoveExistingControllers` への登録 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | ①配線ガードを `#if UNITY_ANDROID \|\| UNITY_IOS \|\| UNITY_EDITOR` に拡張 ②`OnClipboardClicked` に `#elif UNITY_IOS → ShowIosClipboard` を追加 ③**Editor 分岐を 2 択ダイアログに変更**（6.8） |

### 5.3 非変更

| パス | 理由 |
|---|---|
| `Runtime/Clipboard/Ios*.cs` | 実装済み。サンプルのために公開 API を変更しない |
| Android サンプル一式 | 影響を与えない |
| `Resources/UI/Top/TopMenuExample.uxml` | `ClipboardFeatureButton` は既存 |
| `Tests/Runtime/AndroidClipboardSampleSceneWiringTests.cs` | Android 側の検証 |
| `Assets/Samples/.../NativeToolkitExampleScene.unity` | Navigator が動的にロードするため不要（V-1） |

### 5.4 要検証

| # | 事項 |
|---|---|
| V-1 | シーンファイルの変更が不要であること（Navigator 経由で完結する前提の実地確認） |
| V-2 | `LoadItem(File)` の返却パスの**親ディレクトリ**を削除してよいこと（native が request ごとに一時ディレクトリを作る前提）。実機で 1 回削除し、後続の `LoadFile` が成功することを確認する |
| V-3 | `ErrCopyColorOutOfRangeButton` が C# 例外にならず `CLIPBOARD_INVALID_COLOR` を返すこと |
| V-4 | `LargePng`（約 4 MiB）の生成が実機で許容できる時間・メモリに収まること |
| V-5 | 観測中に `CreatePasteboard` の完了が届いた場合の status 表記（4.7）が実際に発生しうるか |

---

## 6. 実装詳細（implement-sample-scene ステップ3で行う内容）

### 6.1 Controller の骨格

```csharp
#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using ...
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

public class IosClipboardManagerExampleController : MonoBehaviour
{
    private const string LogTag = "IosClipboardManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private Label? _resultLabel;
    private ScrollView? _resultScrollView;
    private Label? _statusLabel;

    private IosPasteboardScope _activeScope = IosPasteboardScope.General;
    private IosPasteboardScope? _lastRemovedScope;
    private IosPasteboardScope? _observedScope;

    private IosClipboardSampleObservationState _observation;   // 6.5
    private int _observedEventCount;
    private int _resultSequence;

    // 53 個の Button? フィールド
}
#endif
```

ガードは `#if UNITY_IOS || UNITY_EDITOR`。**ハンドラ内にプラットフォームガードを置かない**（`IosShareManagerExampleController` と同じく Manager の B-1 に委ねる）。

### 6.2 結果 context（レビュー高優先度の反映）

```csharp
/// Immutable identity of one Manager call, captured by that call's per-call callback.
///
/// The Manager serializes only same-operation calls, so Read and GetSnapshot — or LoadItem and
/// CancelLoads — genuinely overlap. A single "pending marker" field would therefore label a
/// completing call with whichever marker was set last. Capturing the context per call is the only
/// way to keep the result line correlated with the call that produced it.
internal readonly struct IosClipboardSampleResultContext
{
    internal int Sequence { get; }
    internal string Marker { get; }
}
```

```csharp
private IosClipboardSampleResultContext BeginResult(string marker)
{
    _resultSequence++;
    var context = new IosClipboardSampleResultContext(_resultSequence, marker);
    SetResult(IosClipboardSampleResult.FormatRunning(context));
    return context;
}
```

### 6.3 API 呼び出し方針

**すべての操作で per-call callback を使い、context を capture する。**

```csharp
private void OnCopyPlainTextClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnCopyPlainTextClicked)}] scope: {ScopeLabel(_activeScope)}");
    var context = BeginResult("copy.plainText");
    IosClipboardManager.Instance.Copy(
        IosClipboardContent.PlainText(PlainTextBody),
        _activeScope,
        options: null,
        onResult: result => CompleteOperation(context, result));
}
```

**共通イベントの扱い**:

- 10 個すべてを `OnEnable` / `OnDisable` で購読・解除する
- **ハンドラは shape-only ログのみ**（`Debug.Log` に成否とコード、件数）。**UI と scope 状態には触れない**
- 例外は `ClipboardChanged`。これは呼び出しに紐づかない継続イベントなので、**UI と status を更新する唯一の共通イベント**とする

### 6.4 完了ハンドラ

```csharp
private void CompleteOperation(IosClipboardSampleResultContext context, IosClipboardOperationResult result)
{
    Debug.Log($"[{LogTag}][{nameof(CompleteOperation)}] seq: {context.Sequence}, marker: {context.Marker}, " +
              $"operation: {result.Operation}, isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
    if (!result.IsSuccess) { SetResult(IosClipboardSampleResult.FormatFailure(context, result.Error!.Value)); return; }
    SetResult(IosClipboardSampleResult.FormatSuccess(context, string.Empty));
}
```

`Read` / `ReadData` / `Snapshot` / `Detect*` / `LoadItem` / `CheckForegroundChange` も同形で、payload の組み立てだけが異なる（4.9）。

#### 6.4.1 scope を変更する操作の所有条件

```csharp
private void OnRemoveActivePasteboardClicked()
{
    IosPasteboardScope target = _activeScope;              // capture at call time
    var context = BeginResult("scope.remove");
    IosClipboardManager.Instance.RemovePasteboard(target, result =>
    {
        if (result.IsSuccess)
        {
            _lastRemovedScope = target;
            // Only reset when this call still owns the active scope: the user may have switched
            // scopes while the removal was in flight.
            if (ReferenceEquals(_activeScope, target))
            {
                _activeScope = IosPasteboardScope.General;
            }
            UpdateStatus();
        }
        // ... result line
    });
}
```

`CreatePasteboard` も同様に「完了時点で active がまだ開始時と同じ場合にのみ差し替える」。

#### 6.4.2 Busy デモの context

```csharp
private void OnBusyLoadItemTwiceClicked()
{
    var first  = BeginResult("busy.loadItem#1");
    IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.Text, _activeScope,
        r => CompleteLoadItem(first, r));

    var second = BeginResult("busy.loadItem#2");
    IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.Text, _activeScope,
        r => CompleteLoadItem(second, r));
}
```

2 本目は即座に `CLIPBOARD_BUSY` で完了し、1 本目は後から完了する。**別 sequence / 別 marker で表示される**ことが期待動作。

#### 6.4.3 Cancel デモ（seed を内包）

```csharp
private void OnSeedAndCancelLoadClicked()
{
    var seed = BeginResult("cancel.seedCopy");
    IosClipboardManager.Instance.Copy(IosClipboardContent.ImageData(SmallPng(), "public.png"), _activeScope,
        options: null,
        onResult: copyResult =>
        {
            CompleteOperation(seed, copyResult);
            if (!copyResult.IsSuccess) return;      // nothing to load; do not start the demo

            var load = BeginResult("cancel.loadImage");
            IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.Image, _activeScope,
                r => CompleteLoadItem(load, r));

            var cancel = BeginResult("cancel.cancelLoads");
            IosClipboardManager.Instance.CancelLoads(r => CompleteOperation(cancel, r));
        });
}
```

seed copy が失敗した場合は load を開始しない。3 つの context が別々に表示される。

### 6.5 observation teardown 状態機械（レビュー中優先度 1 の反映）

```csharp
/// Pure state machine for the observation lifecycle.
///
/// StartObserving and StopObserving share a single-flight key, so a stop issued while a start is
/// still pending is rejected with CLIPBOARD_BUSY. Leaving the screen at that moment would leave
/// the native subscription running with no way to stop it, so the pending start records the
/// request and issues the stop itself once it completes.
internal struct IosClipboardSampleObservationState
{
    internal bool IsObserving { get; private set; }
    internal bool ControlPending { get; private set; }
    internal bool StopRequestedAfterStart { get; private set; }

    internal void BeginStart();
    internal void CompleteStart(bool isSuccess);
    internal void BeginStop();
    internal void CompleteStop();
    internal void RequestStop();          // OnDisable
    internal bool ShouldIssueStopNow();   // OnDisable / start 完了時に問い合わせる
}
```

**遷移**:

| 現在 | 契機 | 次 | 副作用 |
|---|---|---|---|
| 未観測 | Start クリック | `ControlPending = true` | `StartObserving` 発行 |
| Start 実行中 | Start 成功 | `IsObserving = true`, `ControlPending = false` | `StopRequestedAfterStart` なら即 Stop を発行 |
| Start 実行中 | Start 失敗 | `ControlPending = false` | 何もしない |
| Start 実行中 | **`OnDisable`** | `StopRequestedAfterStart = true` | **Stop を発行しない**（busy になるため） |
| 観測中 | Stop クリック / `OnDisable` | `ControlPending = true` | `StopObserving` 発行 |
| Stop 実行中 | Stop 完了 | すべて false | - |

**`OnDisable` の順序**（v1 から変更）:

1. `_observation.RequestStop()`
2. `if (_observation.ShouldIssueStopNow())` → `StopObserving(onResult: ...)` を per-call callback つきで発行
3. **その後に**共通イベント 10 個を解除

per-call callback はイベントではないため、解除後も届く。Start pending 中の離脱では、Start の per-call callback が `ShouldIssueStopNow()` を再評価して Stop を発行する。

**UI 更新のガード**: 離脱後に届いた callback は `if (this == null || !isActiveAndEnabled) { ログのみ; return; }` で UI に触れない。

### 6.6 入力バリデーション方針

- UI 側で入力バリデーションを行わない（入力欄が無い）
- C# 例外に該当する値はボタンに割り当てない（4.4）
- `RemovePasteboard(General)` は native の `CLIPBOARD_CANNOT_REMOVE_GENERAL` をそのまま見せる
- `ProbeRemovedScopeButton` は `_lastRemovedScope == null` のとき**画面に注意文を出して Manager を呼ばない**。この 1 箇所のみ画面側の前提チェックを行う

### 6.7 テスト

#### 6.7.1 wiring テスト（`IosClipboardSampleSceneWiringTests`）

| 検証 | 内容 |
|---|---|
| Resources パス | `UI/iOS/Clipboard/IosClipboardManagerExample` / `...Style` |
| 必須ボタン名 | 4.3 の 53 個 + `HomeButton` |
| 必須ラベル名 | `ResultTextBlock` / `StatusTextBlock` / `SubtitleValidationLabel` / `ErrorsSectionNote` |
| TopMenu | `ClipboardFeatureButton` の存在 |

#### 6.7.2 状態テスト（`IosClipboardSampleStateTests`）— v2 で追加

UXML wiring テストでは今回の主要な状態バグを検出できないため、純粋 helper を切り出して検証する。

| 対象 | 検証内容 |
|---|---|
| `IosClipboardSampleResult.Format*` | context の sequence / marker がそのまま出力に載る。`CLIPBOARD_CANCELLED` が `--` になる。`details` の有無で表記が変わる |
| **marker correlation** | 2 つの context を作り、**完了順を入れ替えて**整形しても、それぞれ自分の sequence / marker で出力されること（`_pendingMarker` 方式では不可能なことを固定する） |
| `IosClipboardSampleObservationState` | 6.5 の遷移表を全件。特に **Start pending → RequestStop → Start 成功 → `ShouldIssueStopNow()` が true** |
| 同上 | Stop pending 中の重複 Stop 要求で `ShouldIssueStopNow()` が false のままであること |
| enabled 契約 | 4.7 の各状態で Scope / Start / Stop の enabled が期待どおりになること（純粋関数として切り出す） |

Controller 本体は `Start` で `UIDocument` に触れるため、EditMode ではインスタンス化しない（`testing.md` の層 1 制約）。

### 6.8 TopMenu の Editor 導線（レビュー中優先度 2 の反映）

現状の Editor 分岐は `DisplayDialog(..., "OK")` のみで画面へ到達できない。**Clipboard 分岐のみ 2 択に変更**する。

```csharp
private void OnClipboardClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnClipboardClicked)}]");
    if (uiDocument == null) return;
#if UNITY_EDITOR
    // The sample screen is reachable in the Editor so the wiring and the B-1 rejection path can be
    // exercised without a device. Every operation returns CLIPBOARD_BRIDGE_UNAVAILABLE there.
    bool open = UnityEditor.EditorUtility.DisplayDialog(
        "Clipboard Feature",
        "This feature runs natively on Android or iOS.\n" +
        "Opening the sample screen in the Editor lets you check the layout; every operation will " +
        "report CLIPBOARD_BRIDGE_UNAVAILABLE.",
        "Open Sample Screen",
        "Close");
    if (!open) return;
    NativeToolkitSampleNavigator.ShowIosClipboard(uiDocument);
#elif UNITY_ANDROID
    NativeToolkitSampleNavigator.ShowAndroidClipboard(uiDocument);
#elif UNITY_IOS
    NativeToolkitSampleNavigator.ShowIosClipboard(uiDocument);
#endif
}
```

**Editor では常に iOS 画面を開く。** Android 画面も Editor から開けるようにするとアクティブビルドターゲットによる分岐が要るが、Android サンプルは既に実機で確認済みであり、本計画の対象外。**この非対称性は意図的なもの**として実装コードのコメントに残す。

Dialog / Notification / Share の 3 機能は現状のダイアログのままとする（本計画のスコープ外）。

### 6.9 実装順序

1. `IosClipboardSampleResult.cs` / `IosClipboardSampleObservationState.cs`（純粋 helper）
2. `IosClipboardSampleStateTests.cs`（helper のテスト）
3. `IosClipboardManagerExampleStyle.uss`
4. `IosClipboardManagerExample.uxml`
5. `IosClipboardManagerExampleController.cs`
6. `NativeToolkitSampleNavigator` / `TopMenuExampleController` の変更
7. `IosClipboardSampleSceneWiringTests.cs`
8. Editor で TopMenu → Clipboard の遷移を確認（V-1）

---

## 7. 手動確認観点

### 7.1 サンプル自体の確認（Editor）

| # | 確認 | 期待 |
|---|---|---|
| S-1 | TopMenu に Clipboard ボタンが表示される（iOS ターゲット） | 表示される |
| S-2 | Clipboard ボタン押下 → `Open Sample Screen` | clipboard 画面へ遷移する |
| S-3 | Back To Home | TopMenu へ戻る |
| S-4 | 任意の操作を Editor で実行 | `CLIPBOARD_BRIDGE_UNAVAILABLE` が結果欄に出る |
| S-5 | 画面遷移を往復 | イベントの二重購読が起きない（結果が 2 回出ない） |
| S-6 | Editor で Start Observing → 画面離脱 | Start は B-1 で失敗し、observation 状態が未観測に戻る |

### 7.2 実機確認（実装計画 v5 の M-1〜M-24）

| # | 手順 | 対応 M |
|---|---|---|
| S-10 | `Copy Plain Text` → メモ等に貼り付け。**日本語・絵文字・サロゲートペアが化けないこと** | M-1 |
| S-11 | `Copy HTML Text` → リッチテキスト対応アプリに貼り付け | M-2 |
| S-12 | `Copy URL` / `Copy Image File` / `Copy Image Data` / `Copy Color` を順に実行し貼り付け | M-3 |
| S-13a | 端末 B で `Copy Device Baseline`（31 文字）→ 端末 A で `Read` し `textLen=31` を確認 | M-4 準備 |
| S-13b | 端末 A で `Copy (localOnly = false)`（14 文字）→ 端末 B で `Read` し **`textLen=14`** を確認 | M-4（載る） |
| S-13c | 端末 B で再度 baseline を copy → 端末 A で `Copy (localOnly = true)` → 端末 B で `Read` し **`textLen=31` のまま**を確認 | M-4（載らない） |
| S-14 | `Copy (expires in 30s)` → 30 秒後に `Read` | M-5 |
| S-15 | `Copy Plain Text`（14 文字）→ `Append Plain Text`（24 文字）→ `Read` で `items=2` | M-6 |
| S-16 | 他アプリでコピー → `Snapshot` → `Read` | M-7（貼り付け許可 UI の有無を記録） |
| S-17 | 何もコピーしていない scope で `Read Data (public.png)` | M-8（`hasData=false` の成功） |
| S-18 | `Create Named` → `Copy Plain Text` → `Read` → `Remove Active` → `Probe Last Removed` | M-9 |
| S-19 | `Use General` → `Remove Active` | M-10 |
| S-20 | `Create Unique` → `Copy` → `Read` | M-11 |
| S-21 | `Copy Detection Fixture` → `Detect Patterns` / `Detect Values`。続けて `Copy Number Fixture` → `Detect Patterns`、`Copy Search Fixture` → `Detect Patterns` | M-12（**検出値がログに出ていないこと**を併せて確認） |
| S-22 | `Copy Custom Data` → `Load File (public.data)` で **`fileSize=64`**。続けて `Load Image` / `Load Text` / `Load URL` | M-13 |
| S-23 | `Seed And Cancel Load` | M-14（load が `CLIPBOARD_CANCELLED`、cancel が OK） |
| S-24 | `Start Observing` → 他アプリでコピー → アプリへ復帰 | M-15 |
| S-25a | `Busy Start Observing Twice` | M-16 前半（2 本目が `CLIPBOARD_BUSY`） |
| S-25b | `Stop Observing` → `Start Observing` → **他アプリで 1 回だけコピー** → 復帰し `Events` が **1 だけ増える**ことを確認 | M-16 後半（世代ゲート） |
| S-26 | `Stop Observing` 後に他アプリでコピー | M-17（`Events` が増えない） |
| S-27 | `Check Foreground Change` をバックグラウンド復帰後に実行 | M-18 |
| S-28 | 画面遷移・アプリ終了 | M-19 |
| S-29 | 全操作のログ確認 | M-20（本文・base64・検出値・pasteboard 名・**パス**が出ていないこと） |
| S-30 | `Copy Plain Text`（成功系）と `Copy (expires in 30s)` を過去日時に変えたビルド（失敗系） | M-21（**要検証**: 失敗系を fixture だけで作れるか。作れない場合は `Err Copy Invalid URL` で代替） |
| S-31 | `Copy Large Image Data`（約 4 MiB）→ `Read Data (public.png)` | M-22 |
| S-32 | `Busy Load Item Twice` | M-24 |

### 7.3 Errors セクションの確認

4.3 の 10 ボタンについて、表示コードが期待どおりであることを 1 件ずつ確認する。

### 7.4 M-22 / M-23 の計測手順（v2 で追加）

**M-22（数 MiB）— 本サンプルで実施する**

1. Xcode で Instruments の Allocations を attach
2. `Copy Large Image Data` → `Read Data (public.png)` を 3 回繰り返す
3. 記録項目: `Persistent Bytes` のピーク、実行後に baseline へ戻るか、`bytes=` 表示値との比

**M-23（64MiB 近傍）— 本サンプルでは実施しない**

画面から数十 MB のバイト列を生成するとサンプル自体が不安定になり、測りたい対象（parser の一時割り当て）と生成コストが混ざる。以下へ引き継ぐ。

| 項目 | 内容 |
|---|---|
| 実施主体 | 実装計画 v5 の 9.4 の要検証事項として、別途 |
| fixture 供給 | サンプル UI ではなく、`ReadData` へ与える応答 JSON を直接組み立てた EditMode / PlayMode ベンチ |
| 観測項目 | `TryGetDecodedLength` の上限判定でバッファを確保しないこと、`Data.Length == byteCount` 検証まで到達したときの managed ピーク |
| 記録先 | 本計画ではなく、実装計画 v5 の 9.4 を更新する |

---

## 8. 出力ルールの遵守

- 実装結果由来（1 章）と、サンプル計画時の追加判断（2.2 の「Unity 側で追加するもの」、3.2、4.4〜4.7、6.2、6.5、6.8）を分離した
- 既存サンプルの深掘り結果（2.3 / 2.4 / 2.5）と、native から取り込む実装ディテール（2.2）を記載した
- 共通実装パターンの維持（3.1）と拡張（3.2）を分けた
- 変更対象は具体パスで示した（5 章）
- 不確実な事項は V-1〜V-5 および S-30 の要検証として明記した

---

## 9. 実行確認

- 提示文:
  - 「この実装計画を採用して、次工程へ進めますか？」
- 選択肢:
  - 承認する: 計画を確定し終了 → review-document スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して計画ファイルを更新
  - キャンセル: 計画ファイルは保持したまま終了
- ユーザー回答:
  - 未回答
