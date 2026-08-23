# iOS Clipboard サンプルシーン実装計画 v1

## 基本情報

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- 出力言語: 日本語（**計画書の記述言語のみ。実装コード内の文言・コメントは英語**）
- 実装結果: `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v3.md`（レビュー LGTM）
- 実装計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 後続工程: review-document → implement-sample-scene

---

## 1. 実装結果から抽出した前提

### 1.1 実装済みの公開 API（15 操作）

すべて `IosClipboardManager`（`#if UNITY_IOS || UNITY_EDITOR`）の instance メソッド。全操作が「共通 event → 任意の per-call callback」の順で結果を返す。

| 操作 | シグネチャ（末尾の callback は省略可） | 結果型 |
|---|---|---|
| `Copy` | `(IosClipboardContent, IosPasteboardScope?, IosClipboardCopyOptions?)` | `IosClipboardOperationResult` |
| `Append` | `(IosClipboardContent, IosPasteboardScope?)` | 同上 |
| `Clear` | `(IosPasteboardScope?)` | 同上 |
| `RemovePasteboard` | `(IosPasteboardScope)` **必須** | 同上 |
| `CancelLoads` | `()` | 同上 |
| `StartObserving` | `(IosPasteboardScope?, Action<IosClipboardChangeEvent>?)` | 同上（+ `ClipboardChanged`） |
| `StopObserving` | `()` | 同上 |
| `Read` | `(IosPasteboardScope?)` | `IosClipboardReadResult` |
| `ReadData` | `(string utType, IosPasteboardScope?)` | `IosClipboardReadDataResult` |
| `GetSnapshot` | `(IosPasteboardScope?, string[]? matchingTypes)` | `IosClipboardSnapshotResult` |
| `CreatePasteboard` | `(IosPasteboardCreationRequest)` | `IosPasteboardScopeResult` |
| `DetectPatterns` | `(IosClipboardDetectionPattern[], IosPasteboardScope?)` | `IosClipboardDetectedPatternsResult` |
| `DetectValues` | `(IosClipboardDetectionPattern[], IosPasteboardScope?)` | `IosClipboardDetectedValuesResult` |
| `LoadItem` | `(IosClipboardLoadRequest, IosPasteboardScope?)` | `IosClipboardLoadedItemResult` |
| `CheckForegroundChange` | `(IosPasteboardScope?)` | `IosClipboardForegroundChangeResult` |

公開イベントは 10 個（`ClipboardOperationCompleted` / `ClipboardChanged` / `ReadCompleted` / `ReadDataCompleted` / `SnapshotCompleted` / `PasteboardCreated` / `PatternsDetected` / `ValuesDetected` / `ItemLoaded` / `ForegroundChangeChecked`）。

### 1.2 入力制約（サンプル側で守る必要があるもの）

| 制約 | 内容 |
|---|---|
| `IosPasteboardScope.Named/Unique` | 空・空白名は `ArgumentException`。C# 側で throw する |
| `IosClipboardContent.Color` | 非有限値（NaN / Infinity）は `ArgumentException` |
| `IosClipboardContent.*` | `null` 引数は `ArgumentNullException` |
| `RemovePasteboard` | `scope` が必須（`null` は B-3） |
| `DetectPatterns` / `DetectValues` | 空配列は `CLIPBOARD_EMPTY_PATTERNS`（native 到達前に返る） |
| **main thread 限定** | `Instance` getter を含め、UI Toolkit のコールバックから呼ぶ限り常に満たされる |
| **single-flight** | 同一操作の実行中に再呼び出しすると `CLIPBOARD_BUSY`。`LoadItem` は最大 15 秒かかる |
| **破棄後** | `OnDestroy` 後は全操作が `CLIPBOARD_MANAGER_DESTROYED`。サンプルは Manager を破棄しない |

### 1.3 エラー契約

- 結果型は `IsSuccess` と `Error`（`IosClipboardErrorInfo?`）を持つ。**`ErrorMessage` プロパティは直下に存在しない**（`result.Error?.Message` で参照する）
- `Error` は `Code` / `Message`（ともに非 null）＋ `Domain` / `NativeCode`（`details` があるときのみ）
- **`CLIPBOARD_CANCELLED` は正常な打ち切り**として扱ってよい（native doc の指示）
- `ReadData` の「該当データなし」は失敗ではなく `HasData == false` の成功
- Editor 実行時は全操作が `CLIPBOARD_BRIDGE_UNAVAILABLE`（`{operation} is only available on an iOS device.`）

### 1.4 手動確認観点（実装結果 7.3 の M-1〜M-24 が未実施）

本サンプルは M-1〜M-24 を実機で消化するための操作導線を提供することを目的の一つとする。M-25（build / link）は実施済み。

### 1.5 不足前提（サンプル側で勝手に補わない）

| 項目 | 内容 |
|---|---|
| `Awaitable` 版 | 実装されていない。サンプルも callback 版のみを使う |
| Paste Control（P-16） | Unity Bridge に公開されていない。native サンプルの `pasteControlSection` は**移植しない** |
| 制限値・タイムアウトの変更 | Unity 側から変更する手段がない。サンプルでも扱わない |
| 画像アセット | 既存サンプルに同梱画像がない。`ImageData` 用のバイト列は**コード内で生成**する（後述 4.4） |

---

## 2. 既存サンプルコードの深掘り結果

### 2.1 確認したもの

| 対象 | 確認内容 |
|---|---|
| `UI/Android/Clipboard/AndroidClipboardManagerExampleController.cs`（686 行） | clipboard 固有の型・ログ規約の先行例 |
| `Resources/UI/Android/Clipboard/AndroidClipboardManagerExample.uxml`（71 行） | セクション分割・結果表示 ScrollView |
| `UI/iOS/Share/IosShareManagerExampleController.cs`（426 行） | **iOS プラットフォームの Controller パターン** |
| `Resources/UI/iOS/Share/IosShareManagerExample.uxml` / `.uss` | `ios-*` クラス命名 |
| `UI/Common/NativeToolkitSampleNavigator.cs`（202 行） | 画面遷移とコントローラ差し替え |
| `UI/Top/TopMenuExampleController.cs`（191 行） | TopMenu の機能ボタンとプラットフォーム分岐 |
| `Tests/Runtime/AndroidClipboardSampleSceneWiringTests.cs` | UXML / Controller の name 不一致検出テスト |
| native-toolkit `ios/IosLibraryExample/ClipboardSampleView.swift`（1,070 行） | **iOS native サンプルの機能一覧・操作単位・結果表示** |

### 2.2 native サンプル（`ClipboardSampleView.swift`）との対応

native サンプルのセクション構成を基準に、Unity サンプルへ反映する範囲を決める。

| native セクション | Unity への反映 | 備考 |
|---|---|---|
| `scopeSection`（Use General / Create Named / Use Fixed Named / Create Unique / Remove Active / Probe Last Removed） | **反映する** | scope は iOS 固有の中心概念。Android サンプルには無い |
| `copySection`（12 種） | **反映する** | 9 content kind + fixture 系 |
| `copyOptionsSection`（localOnly true/false / baseline / expiring） | **反映する** | M-4 / M-5 の導線 |
| `appendSection`（3 種） | **反映する** | |
| `readSection`（Read / ReadData / Snapshot / Snapshot matching） | **反映する** | |
| `loadSection`（Load Text/URL/Image/File / Cancel All Loads） | **反映する** | M-13 / M-14 の導線 |
| `detectSection`（fixture 2 種 + Detect Patterns / Values） | **反映する** | M-12 の導線 |
| `observeSection`（Start / Stop / Check Foreground Change） | **反映する** | M-15〜M-18 の導線 |
| `pasteControlSection`（Mount Paste Control） | **反映しない** | P-16 は Unity Bridge 未公開（1.5） |
| `clearSection` | **反映する** | |
| `errorSection`（12 種） | **反映する（10 種に調整）** | Paste Control 由来の 2 種を除外 |

**Unity 側で追加するもの（native サンプルに無い）:**

| 追加 | 理由 |
|---|---|
| **Busy（single-flight）デモ** | `CLIPBOARD_BUSY` は Unity Bridge 固有の契約（native ABI に request ID が無いため導入）。native サンプルには存在しないが、利用者が最も踏みやすい制約なので明示的に見せる |
| **Editor 実行時の案内** | Unity 固有。`CLIPBOARD_BRIDGE_UNAVAILABLE` がそのまま結果欄に出る |

### 2.3 再利用する既存コンポーネント

| コンポーネント | 再利用方法 |
|---|---|
| `NativeToolkitSampleNavigator` | `ShowIosClipboard` を**追加**して同じ `ApplyScreen<T>` 経路に乗せる |
| `TopMenuExampleController` | Clipboard ボタンの**プラットフォームガードを拡張**（現在 Android 限定） |
| `UIDocument` + Resources ロード方式 | そのまま |
| 結果表示の ScrollView 構造 | `AndroidClipboardManagerExample.uxml` の `ResultScrollView` + `ResultTextBlock` を踏襲 |
| `ios-secondary-button` クラス | `IosShareManagerExampleStyle.uss` と同名クラスを iOS clipboard 用 USS にも定義（USS はスクリーン単位で差し替わるため共有はしない） |

### 2.4 追加するコンポーネント

| 追加 | 役割 |
|---|---|
| `IosClipboardManagerExampleController` | 15 操作 + 10 イベントの購読 |
| `IosClipboardManagerExample.uxml` | セクション分割された操作ボタン群 + 結果表示 + ステータス行 |
| `IosClipboardManagerExampleStyle.uss` | `ios-clipboard-*` クラス |
| `IosClipboardSampleSceneWiringTests` | UXML / Controller の name 不一致検出 |

### 2.5 変更するファイルと理由

| ファイル | 変更理由 |
|---|---|
| `UI/Common/NativeToolkitSampleNavigator.cs` | `ShowIosClipboard` の追加と `RemoveExistingControllers` への登録。追加しないと画面遷移できない |
| `UI/Top/TopMenuExampleController.cs` | Clipboard ボタンが `#if UNITY_ANDROID \|\| UNITY_EDITOR` でしか配線されず、iOS では非表示になる。iOS を追加しないと導線が無い |

---

## 3. 共通実装パターンの維持と拡張

### 3.1 維持するもの

| パターン | 適用 |
|---|---|
| TopMenu → 機能別 ExampleController の導線 | そのまま |
| 先頭にタイトル + 結果表示領域 | そのまま（`ResultTextBlock` / `ResultScrollView`） |
| 機能カテゴリ単位のボタン群 | Scope / Copy / Copy Options / Append / Read / Load / Detect / Observe / Clear / Errors |
| 成功・失敗が一目で分かる結果文言 | `[OK]` / `[NG]` プレフィックス（3.2） |
| `OnEnable` / `OnDisable` でのイベント購読・解除 | 10 イベントすべて |
| 公開 API 呼び出し前後のログ | 呼び出し前にログ、結果はイベントハンドラでログ |
| `Start` で `UIDocument` 解決 → `InitializeUI` | そのまま |
| `OnDestroy` で全ボタンの `clicked` 解除 | そのまま |

### 3.2 拡張するもの

| 拡張 | 内容 | 理由 |
|---|---|---|
| **ステータス行の追加** | `ResultTextBlock` とは別に `StatusTextBlock` を置き、`Scope: <label> \| Observing: on/off \| Events: <n>` を表示 | native サンプルと同じ。scope と observing は複数操作にまたがる状態で、結果行だけでは追えない |
| **結果行に連番とマーカー** | `#12 [copy.plainText] OK ...` | 同じ操作を連続実行したとき、結果が更新されたのか固まったのか区別できない。native サンプルの `#seq [marker]` に合わせる |
| **`UnityMainThreadDispatcher` を Controller から直接使わない** | Manager 側が全結果を dispatcher 経由で main thread に載せるため、Controller のハンドラは常に main thread | 二重 dispatch を避ける。共通パターンの「dispatcher 経由で反映」は Manager 側で満たされている |
| **ログ抑止の既定値** | `SetResult(message, logMessage: false)` を**既定**にする | Android サンプルは既定 true で content 系のみ false。iOS は結果のほぼ全てが clipboard 由来（read / snapshot / detect / load）なので、既定を逆にして事故を減らす |

### 3.3 ログ・表示規約（clipboard 固有）

`common.md` / `csharp.md` の「全パラメータをログ」から意図的に逸脱する。実装計画 v5 の 5.6.7 と同じ方針。

| 区分 | ログ | 画面表示 |
|---|---|---|
| 操作名・成否・エラーコード・件数・バイト数・`kind` | **出す** | 出す |
| clipboard 本文（`Text` / `UrlString` / `Path` / base64 / representations） | **出さない** | 出す（長さ・有無に丸めたうえで。4.6） |
| 検出値（メール・電話・住所・金額など） | **出さない** | **件数のみ**（native サンプルと同じ） |
| pasteboard 名 | **出さない** | **長さのみ**（`named(len=12)`） |

---

## 4. 画面要件

### 4.1 画面構成

```
[Back To Home]
IosClipboardManager Example
<subtitle: iOS 18+ 実機で動作。Editor では全操作が CLIPBOARD_BRIDGE_UNAVAILABLE になる旨>
┌ ResultScrollView ─────────────────┐
│ ResultTextBlock                   │  ← #seq [marker] OK/NG 詳細
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
2. Scope セクションで active scope を切り替える（Create Named / Create Unique は `CreatePasteboard` の結果 scope をそのまま保持する）
3. 以降の Copy / Read / Detect / Observe などは**すべて active scope に対して実行**する
4. 結果は `ResultTextBlock` に 1 行、状態は `StatusTextBlock` に反映

**入力欄は設けない。** 既存サンプル（Android clipboard / iOS share）と同じくボタンのみで完結させ、値はコード内の固定 fixture を使う。

### 4.3 セクションとボタン一覧（計 51 ボタン）

#### Scope（6）

| name | text | 動作 |
|---|---|---|
| `UseGeneralButton` | Use General | active scope を `IosPasteboardScope.General` に |
| `CreateNamedPasteboardButton` | Create Named Pasteboard | `CreatePasteboard(Named("group.nativetoolkit.sample"))` → 成功した scope を active に |
| `UseFixedNamedScopeButton` | Use Fixed Named Scope (no create) | 作成せずに `Named(...)` を active に（未作成 scope の `CLIPBOARD_UNAVAILABLE` を見る） |
| `CreateUniquePasteboardButton` | Create Unique Pasteboard | `CreatePasteboard(Unique)` → 生成名つき scope を active に（M-11） |
| `RemoveActivePasteboardButton` | Remove Active Pasteboard | `RemovePasteboard(active)` → 成功したら active を `General` に戻す |
| `ProbeRemovedScopeButton` | Probe Last Removed Scope | 直前に削除した scope へ `Read` して `CLIPBOARD_UNAVAILABLE` を確認 |

#### Copy（11）

`CopyPlainTextButton` / `CopyEmptyPlainTextButton` / `CopyHtmlTextButton` / `CopyUrlButton` / `CopyImageFileButton` / `CopyImageDataButton` / `CopyColorButton` / `CopyCustomDataButton` / `CopyMultipleTextButton` / `CopyMultiRepresentationButton` / `CopyDetectionFixtureButton`

#### Copy Options（3）

| name | 動作 |
|---|---|
| `CopyLocalOnlyTrueButton` | `Create(localOnly: true)`（既定と同じ。M-4 の対照） |
| `CopyLocalOnlyFalseButton` | `Create(localOnly: false)`（Universal Clipboard へ載る。M-4） |
| `CopyExpiringButton` | `Create(true, DateTime.UtcNow.AddSeconds(30))` |

#### Append（2）

`AppendPlainTextButton` / `AppendUrlButton`

#### Read（4）

`ReadButton` / `ReadDataPngButton`（`public.png`）/ `SnapshotButton` / `SnapshotMatchingButton`（`matchingTypes: ["public.utf8-plain-text"]`）

#### Load（5）

`LoadTextButton` / `LoadUrlButton` / `LoadImageButton` / `LoadFileButton`（`public.data`）/ `CancelLoadsButton`

#### Detect（4）

| name | 動作 |
|---|---|
| `CopyNumberFixtureButton` | 数値のみのテキストを copy |
| `CopySearchFixtureButton` | 検索語句らしいテキストを copy |
| `DetectPatternsButton` | 11 パターン全指定で `DetectPatterns` |
| `DetectValuesButton` | 11 パターン全指定で `DetectValues` |

#### Observe（3）

`StartObservingButton` / `StopObservingButton` / `CheckForegroundChangeButton`

#### Clear（1）

`ClearActiveScopeButton`

#### Busy（single-flight）（2）— **Unity 固有の追加**

| name | 動作 |
|---|---|
| `BusyLoadItemTwiceButton` | `LoadItem(Text)` を連続 2 回。2 本目が `CLIPBOARD_BUSY` になることを見せる（M-24） |
| `CancelDuringLoadButton` | `LoadItem(Image)` 直後に `CancelLoads`。1 本目が `CLIPBOARD_CANCELLED`、`CancelLoads` 自体は別操作なので busy にならない（M-14） |

#### Errors（10）

| name | 期待コード |
|---|---|
| `ErrCopyMultipleEmptyButton` | `CLIPBOARD_EMPTY_ITEMS` |
| `ErrCopyMultiRepEmptyButton` | `CLIPBOARD_EMPTY_ITEMS` |
| `ErrCopyImageFileMissingButton` | `CLIPBOARD_FILE_NOT_FOUND` |
| `ErrCopyInvalidUtiButton` | `CLIPBOARD_INVALID_TYPE` |
| `ErrCopyInvalidUrlButton` | `CLIPBOARD_INVALID_URL` |
| `ErrCopyColorOutOfRangeButton` | `CLIPBOARD_INVALID_COLOR`（0.0〜1.0 の範囲外。**有限値**なので C# 例外にはならない） |
| `ErrReadDataInvalidUtiButton` | `CLIPBOARD_INVALID_TYPE` |
| `ErrRemoveGeneralButton` | `CLIPBOARD_CANNOT_REMOVE_GENERAL` |
| `ErrObserveMissingNamedButton` | `CLIPBOARD_UNAVAILABLE` |
| `ErrDetectEmptyPatternsButton` | `CLIPBOARD_EMPTY_PATTERNS`（native 到達前に C# が返す） |

**C# 側で例外になるケースは Errors セクションに置かない。** `IosPasteboardScope.Named("")` や `Color(NaN, ...)` は `ArgumentException` であり、ボタンに割り当てるとサンプルがクラッシュする。実装結果の「C# 側では native が弾く値を先回りして弾かない」方針と、この 2 つだけが例外である点を subtitle の注記で説明する（要検証: 注記だけで足りるか、実装時に文言を確定する）。

### 4.4 fixture の作り方（追加判断）

| 用途 | 生成方法 |
|---|---|
| `ImageData` | コード内で 1x1 の PNG を `Texture2D.EncodeToPNG()` で生成（`new Texture2D(1,1)` → `SetPixel` → `Apply`）。同梱アセットを追加しない |
| `ImageFile` | 上記 PNG を `Application.persistentDataPath` に書き出し、そのパスを使う |
| `CustomData` / `MultiRepresentation` | `Encoding.UTF8.GetBytes("...")` |
| `LoadFile`(`public.data`) の対象 | `CopyCustomDataButton` で `public.data` を copy した後に `LoadFile` する導線（順序を subtitle に明記） |

### 4.5 エラー表示

```
#12 [copy.plainText] NG code=CLIPBOARD_EMPTY_CONTENT message=Clipboard content is empty. Please provide text or HTML.
```

- `Error.Domain` / `Error.NativeCode` があるときのみ ` details=<domain>:<code>` を追記
- `CLIPBOARD_CANCELLED` は `NG` ではなく **`--` 表記**（正常な打ち切りである旨を示す）
- Editor 実行時は `CLIPBOARD_BRIDGE_UNAVAILABLE` がそのまま出る。subtitle で「Editor では全操作がこのコードになる」と説明する

### 4.6 成功時の表示（content を丸める）

| 操作 | 表示内容 |
|---|---|
| `Read` | `items=<n> types=<first item type count>`。**本文は出さない** |
| `ReadData` | `hasData=<bool> utType=<utType> bytes=<n>` |
| `GetSnapshot` | `items=<n> strings=<bool> urls=<bool> images=<bool> colors=<bool> matching=<null or count>` |
| `CreatePasteboard` | `scope=<kind>(len=<n>)` |
| `DetectPatterns` | `patterns=<n>` |
| `DetectValues` | `patterns=<n> emails=<n> phones=<n> addresses=<n> events=<n> flights=<n> money=<n> shipments=<n> links=<n>`。**値は出さない** |
| `LoadItem` | `kind=<kind>` ＋ `Text` は `len=<n>`、`ImageData` は `bytes=<n>`、`File` は `pathLen=<n>` |
| `CheckForegroundChange` | `changed=<bool>` |
| `ClipboardChanged` | `kind=<kind> added=<n> removed=<n> scope=<label>` |

**`utType` は表示・ログとも許可**（利用者が指定した固定文字列であり、clipboard 由来の機微値ではない）。

---

## 5. 変更ファイル一覧

`.meta` は Unity が自動生成するため記載しない。

### 5.1 新規作成

| パス | 内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs` | Controller |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml` | 画面定義 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExampleStyle.uss` | スタイル |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosClipboardSampleSceneWiringTests.cs` | wiring テスト |

### 5.2 既存変更

| パス | 変更内容 |
|---|---|
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowIosClipboard` を追加（`#if UNITY_IOS \|\| UNITY_EDITOR`）。`RemoveExistingControllers` の iOS ブロックに `RemoveIfExists<IosClipboardManagerExampleController>` を追加 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | Clipboard ボタンの配線ガードを `#if UNITY_ANDROID \|\| UNITY_IOS \|\| UNITY_EDITOR` に拡張。`OnClipboardClicked` に `#elif UNITY_IOS → ShowIosClipboard` を追加。Editor ダイアログ文言を「Android or iOS」に更新 |

### 5.3 非変更（対象だが変更しない）

| パス | 理由 |
|---|---|
| `Runtime/Clipboard/Ios*.cs` | 実装済み。サンプルのために公開 API を変更しない |
| `Runtime/UI/Android/Clipboard/*` および Android の UXML / USS | Android サンプルに影響を与えない |
| `Resources/UI/Top/TopMenuExample.uxml` | `ClipboardFeatureButton` は既に存在する。UXML 変更は不要 |
| `Tests/Runtime/AndroidClipboardSampleSceneWiringTests.cs` | Android 側の検証。変更しない |
| `Assets/Samples/.../NativeToolkitExampleScene.unity` | 画面は `NativeToolkitSampleNavigator` が Resources から動的にロードする。**シーン自体の変更は不要**（要検証: 5.5） |

### 5.4 サンプルシーンへの追加が不要な理由

既存 4 画面（Android clipboard 含む）はいずれもシーンに配置されておらず、`TopMenuExampleController` → `NativeToolkitSampleNavigator.ApplyScreen<T>` が実行時に `UIDocument.visualTreeAsset` を差し替え、`AddComponent<T>()` で Controller を足す方式である。したがって iOS clipboard も**同じ経路に登録するだけ**でよい。

### 5.5 要検証

| # | 事項 |
|---|---|
| V-1 | シーン `NativeToolkitExampleScene.unity` に変更が不要であること（Navigator 経由で完結するという前提の実地確認）。implement-sample-scene の Editor 起動時に確認する |
| V-2 | Errors セクションの `ErrCopyColorOutOfRangeButton` が C# 例外にならず `CLIPBOARD_INVALID_COLOR` として返ること（範囲外だが有限値なので factory は通る想定） |
| V-3 | 4.3 の注記だけで「C# 例外になる入力はボタン化していない」ことが利用者に伝わるか。実装時に文言を確定する |
| V-4 | `LoadFile(public.data)` の前提（先に `CustomData` を copy する）を UI 上どう案内するか。section note で足りるか |

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
    private bool _isObserving;
    private int _observedEventCount;
    private int _resultSequence;
    private string _pendingMarker = string.Empty;

    // 51 個の Button? フィールド
}
#endif
```

- ガードは `#if UNITY_IOS || UNITY_EDITOR`（`IosShareManagerExampleController` と同じ A 群）
- **ハンドラ内にプラットフォームガードを置かない。** `IosShareManagerExampleController` と同じく Manager の B-1 に委ねる（Android サンプルの `#if UNITY_ANDROID && !UNITY_EDITOR` 方式は採らない）

### 6.2 イベント購読

```csharp
private void OnEnable()
{
    var m = IosClipboardManager.Instance;
    m.ClipboardOperationCompleted += OnOperationCompleted;
    m.ClipboardChanged            += OnClipboardChanged;
    m.ReadCompleted               += OnReadCompleted;
    m.ReadDataCompleted           += OnReadDataCompleted;
    m.SnapshotCompleted           += OnSnapshotCompleted;
    m.PasteboardCreated           += OnPasteboardCreated;
    m.PatternsDetected            += OnPatternsDetected;
    m.ValuesDetected              += OnValuesDetected;
    m.ItemLoaded                  += OnItemLoaded;
    m.ForegroundChangeChecked     += OnForegroundChangeChecked;
}

private void OnDisable()
{
    // 同じ 10 個を解除し、最後に StopObserving() を呼ぶ
}
```

- `OnDisable` で `StopObserving()` を呼ぶ（Android サンプルと同じ）。**Manager は破棄しない**（破棄すると tombstone が立ち以降の全操作が拒否されるため）
- `Instance` getter は main thread からのみ呼ぶ。`OnEnable` / `OnDisable` / UI コールバックはすべて main thread

### 6.3 API 呼び出し方針

**per-call callback は使わず、共通イベントのみを購読する。**

理由:

- 共通イベントは per-call callback の有無に関わらず必ず発火する（実装計画 3.4）
- サンプルは「どの操作の結果か」を marker で表示すればよく、呼び出しごとの分岐が不要
- `ClipboardOperationCompleted` は `Operation` で 7 操作を判別できる

```csharp
private void OnCopyPlainTextClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnCopyPlainTextClicked)}] scope: {ScopeLabel(_activeScope)}");
    SetPendingMarker("copy.plainText");
    IosClipboardManager.Instance.Copy(
        IosClipboardContent.PlainText("Hello from Unity Native Toolkit"), _activeScope);
}
```

- 呼び出し前に `Debug.Log` と `SetPendingMarker`
- **scope はすべての scope 対応操作に `_activeScope` を渡す**（`null` は渡さない。active scope が明示的に反映されることを見せるため）

### 6.4 結果ハンドラ

```csharp
private void OnReadCompleted(IosClipboardReadResult result)
{
    // 本文は出さない。件数と型数のみ。
    Debug.Log($"[{LogTag}][{nameof(OnReadCompleted)}] isSuccess: {result.IsSuccess}, " +
              $"errorCode: {result.Error?.Code}, items: {result.Items.Count}");
    if (!result.IsSuccess) { SetFailure(result.Error!.Value); return; }
    SetSuccess($"items={result.NumberOfItems} firstItemTypes={FirstItemTypeCount(result)}");
}
```

共通ヘルパ:

| ヘルパ | 役割 |
|---|---|
| `SetPendingMarker(string marker)` | `_pendingMarker` を更新し、結果欄に `#n [marker] running...` を出す |
| `SetSuccess(string payload)` | `#n [marker] OK <payload>` |
| `SetFailure(IosClipboardErrorInfo error)` | `#n [marker] NG code=... message=...`（`CLIPBOARD_CANCELLED` は `--`） |
| `SetResult(string message, bool logMessage = false)` | 表示更新 + `ResultScrollView.scrollOffset = Vector2.zero` |
| `UpdateStatus()` | `Scope: ... \| Observing: ... \| Events: ...` |
| `ScopeLabel(IosPasteboardScope?)` | `general` / `named(len=n)` / `unique(len=n)` |

### 6.5 入力バリデーション方針

- **UI 側で入力バリデーションを行わない。** 入力欄が無く、値はすべてコード内 fixture のため
- `IosPasteboardScope.Named` / `IosClipboardContent.Color` の `ArgumentException` に該当する値は**ボタンに割り当てない**（4.3 の注記）
- `RemovePasteboard(active)` で active が `General` のときは、native の `CLIPBOARD_CANNOT_REMOVE_GENERAL` をそのまま見せる（先回りして無効化しない）
- `ProbeRemovedScopeButton` は `_lastRemovedScope == null` のとき**画面に注意文を出して何もしない**（Manager を呼ばない）。この 1 箇所のみ画面側の前提チェックを行う

### 6.6 wiring テスト

`AndroidClipboardSampleSceneWiringTests` と同じ構成で作る。

| 検証 | 内容 |
|---|---|
| Resources パス | `UI/iOS/Clipboard/IosClipboardManagerExample` / `...Style` がロードできる |
| 必須ボタン名 | 4.3 の 51 個 + `HomeButton` がすべて UXML に存在する |
| 必須ラベル名 | `ResultTextBlock` / `StatusTextBlock` |
| TopMenu | `ClipboardFeatureButton` が存在する |

Controller のフィールド名と UXML の `name` の不一致を EditMode で検出することが目的。

### 6.7 実装順序

1. `IosClipboardManagerExampleStyle.uss`（`ios-clipboard-*`）
2. `IosClipboardManagerExample.uxml`（セクションとボタン）
3. `IosClipboardManagerExampleController.cs`（骨格 → イベント購読 → ハンドラ → 表示ヘルパ）
4. `NativeToolkitSampleNavigator` / `TopMenuExampleController` の変更
5. `IosClipboardSampleSceneWiringTests.cs`
6. Editor で TopMenu → Clipboard の遷移を確認（V-1）

---

## 7. 手動確認観点

### 7.1 サンプル自体の確認（Editor）

| # | 確認 | 期待 |
|---|---|---|
| S-1 | TopMenu に Clipboard ボタンが表示される（iOS ターゲット） | 表示される |
| S-2 | Clipboard ボタン押下 | Editor ではダイアログ、iOS Player では clipboard 画面へ遷移 |
| S-3 | Back To Home | TopMenu へ戻る |
| S-4 | 任意の操作を Editor で実行 | `CLIPBOARD_BRIDGE_UNAVAILABLE` が結果欄に出る |
| S-5 | 画面遷移を往復 | イベントの二重購読が起きない（結果が 2 回出ない） |

### 7.2 実機確認（実装計画 v5 の M-1〜M-24 に対応）

| # | 操作 | 対応 M |
|---|---|---|
| S-10 | Copy Plain Text → 他アプリで貼り付け（日本語・絵文字を含む fixture） | M-1 |
| S-11 | Copy HTML Text → リッチテキスト対応アプリで貼り付け | M-2 |
| S-12 | Copy URL / Image File / Image Data / Color | M-3 |
| S-13 | Copy (localOnly = false) → 別デバイスで貼り付け／(true) で載らないこと | M-4 |
| S-14 | Copy (expires in 30s) → 30 秒後に Read | M-5 |
| S-15 | Append の後に Read | M-6 |
| S-16 | 他アプリがコピーした内容に Read / Snapshot | M-7（貼り付け許可 UI の有無を記録） |
| S-17 | Read Data (public.png) を該当データ無しで実行 | M-8（`hasData=false` の成功） |
| S-18 | Create Named → Copy → Read → Remove の一連 | M-9 |
| S-19 | Remove General | M-10 |
| S-20 | Create Unique → その scope で Copy / Read | M-11 |
| S-21 | Copy Detection Fixture → Detect Patterns / Values | M-12（**検出値がログに出ていないこと**を併せて確認） |
| S-22 | Load Image → Load File | M-13 |
| S-23 | Cancel During Load | M-14（`CLIPBOARD_CANCELLED`） |
| S-24 | Start Observing → 他アプリでコピー → 復帰 | M-15 |
| S-25 | Start Observing を 2 回連続 | M-16（2 本目は `CLIPBOARD_BUSY`。native の世代ゲートは 1 本目完了後の再実行で確認） |
| S-26 | Stop Observing 後に他アプリでコピー | M-17 |
| S-27 | Check Foreground Change をバックグラウンド復帰後に実行 | M-18 |
| S-28 | 画面遷移・アプリ終了 | M-19 |
| S-29 | 全操作のログ確認 | M-20（**本文・base64・検出値・pasteboard 名が出ていないこと**） |
| S-30 | Copy の成功系と失敗系 | M-21（`isSuccess` の ABI 検証） |
| S-31 | Copy Image Data（数 MiB）→ Read Data | M-22 |
| S-32 | Busy Load Item Twice | M-24 |

**M-23（上限 64MiB 近傍）は本サンプルでは扱わない。** 画面から数十 MB のバイト列を生成するとサンプルアプリ自体が不安定になるため、別途計測用の手段で確認する（要検証: 実施方法は未定）。

### 7.3 Errors セクションの確認

4.3 の 10 ボタンについて、表示されるコードが期待どおりであることを 1 件ずつ確認する。

---

## 8. 出力ルールの遵守

- 実装結果由来（1 章）と、サンプル計画時の追加判断（2.2 の「Unity 側で追加するもの」、3.2、4.4、6.3）を分離して記載した
- 既存サンプルの深掘り結果（2.3 / 2.4 / 2.5）を記載した
- 共通実装パターンの維持（3.1）と拡張（3.2）を分けて記載した
- 変更対象は具体パスで示した（5 章）
- 不確実な事項は V-1〜V-4 および 7.2 の M-23 として要検証と明記した

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
