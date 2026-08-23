# iOS Clipboard サンプルシーン実装計画 v4

## 基本情報

- 日付: 2026-08-16
- 機能名: clipboard
- 対象プラットフォーム: iOS
- 出力言語: 日本語（**計画書の記述言語のみ。実装コード内の文言・コメントは英語**）
- 実装結果: `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v3.md`（レビュー LGTM）
- 実装計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 前版: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v3.md`
- 対象レビュー: `artifact/reviews/clipboard/2026-08-16-ios-clipboard-sample-scene-design-review-v3.md`（総合評価「要修正（高優先度なし）」）
- 後続工程: review-document → implement-sample-scene

### v3 からの変更点（レビュー反映）

| 指摘 | severity | 対応 |
|---|---|---|
| Restart 失敗時に旧 observation が残るかは native source 上で確定できる | medium | **native を再確認して確定**。`IosClipboardManager.swift:321` の `startObserving` は **scope 解決の guard より前**に `stopObservingInternal()` を呼ぶため、`pasteboardUnavailable` を含む native start failure では旧 subscription が既に破棄されている（1.3 O-5）。6.5.2 の遷移を **「owned な Start failure → `IsObserving = false` / `ControlPending = false` / `StopRequestedAfterStart = false`」** に変更し、**V-7 を削除**。managed rejection との差分も 6.5.3 に表で明記した |
| `_observedScope` が開始時 scope ではなく callback 時の `_activeScope` を記録している | medium | **指摘は正しい**（4.7 の記述と擬似コードが矛盾していた）。`IssueStartObserving` が `targetScope` を owner token と同じ call context に capture し、**owner 一致かつ成功時のみ** `_observedScope = targetScope` とする（6.5.4 / 6.6）。`Create pending → Start(A) → Create success(B) → Start success` で `active B (observing A)` になることをテストへ追加 |
| 画面外 deferred Stop の発行 helper と retry 終端が未確定 | medium | `BeginResult` を **screen-aware** にし（画面外では sequence / marker を発番してログするだけで `SetResult` しない）、`IssueStopObserving` を専用 helper として定義（6.5.6）。deferred Stop の発行条件を **`owned && isStart && StopRequestedAfterStart && ShouldIssueStopNow()`** に限定し、`CompleteStop` は**成功・失敗いずれでも** `StopRequestedAfterStart` を消費する。**再試行しない**（native `stopObserving` は常に成功する。1.3 O-6） |
| Errors note の「全ボタンが native layer に到達する」が `ErrDetectEmptyPatternsButton` と矛盾 | low | 文言を Manager-side rejection を含む表現へ変更（4.4） |
| S-25 系の開始状態が未記載 | low | S-25a → S-25c → S-25b の順に並べ替え、各手順に precondition を明記（7.2） |

ボタン数は 55 のまま。要検証は V-7 を削除して **V-1〜V-6 の 6 件**。

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

### 1.3 observation の Manager 側契約（v3 で追記。6.5 の設計根拠）

`IosClipboardManager.cs` を再確認して確定した事項。

| # | 事実 | 参照 | サンプルへの影響 |
|---|---|---|---|
| O-1 | `StartObserving` は `TryStartOperation` が false のとき **`s_onChanged` / generation を書き換える前に return** する | `IosClipboardManager.cs:1027-1042` | **busy 拒否された 2 本目は Manager 側の登録を壊さない**。壊れるのはサンプル側の状態機械だけなので、owner token で足りる（6.5） |
| O-2 | 「A second successful start replaces the previous observation」 | 同 `StartObserving` XML doc（`:1015`） | **Stop を挟まない Restart が正式にサポートされている**。M-16 の generation gate はこの経路で検証する |
| O-3 | 失敗した start と全ての stop は、**自分が作った登録である場合にのみ** `s_onChanged` を解放する | `ReleaseChangeRegistrationIfOwned`（`:808-821`）、`HandleObservationControlCallback`（`:1645-1652`） | 古い完了が新しい登録を巻き戻さない。サンプルは generation を持たなくてよい |
| O-4 | `ClipboardChanged`（共通イベント）と `onChanged`（per-registration）は**同一の変更で両方**発火する | `FireClipboardChanged`（`:803-806`） | **両方で件数を数えると二重計上**になる。サンプルは `onChanged: null` を渡し、共通イベントのみを計数源にする |
| O-5 | native の `startObserving` は **scope 解決の guard より前**に `stopObservingInternal()` を実行する。したがって `pasteboardUnavailable` で throw した時点で旧 notification token / scope / callback は**既に破棄されている** | native `IosLibrary/Clipboard/IosClipboardManager.swift:321`（`stopObservingInternal()`）、`:322-325`（`guard let pasteboard … else { throw }`）、`:377-385`（`stopObservingInternal`） | **native start failure は「未観測」を意味する**。v3 の保守的挙動（`IsObserving` を維持）では status が実態とずれるため、6.5.2 で失敗時に未観測へ落とす |
| O-6 | native の `stopObserving` は常に `handler(true, nil, nil)` を返す。失敗経路が無い | native `UnityIosPlugin/Clipboard/UnityIosClipboardManager.swift:303-309` | **Stop failure は managed rejection（B-1 / busy / destroyed）でしか起きない**。再試行しても同じ結果になるため、サンプルは deferred Stop を**再試行しない**（6.5.6） |

### 1.4 入力制約

| 制約 | 内容 |
|---|---|
| `IosPasteboardScope.Named/Unique` | 空・空白名は `ArgumentException` |
| `IosClipboardContent.Color` | **非有限値**（NaN / Infinity）は `ArgumentException`。0.0〜1.0 の範囲外は例外にならず native の `CLIPBOARD_INVALID_COLOR` |
| `IosClipboardContent.*` | `null` 引数は `ArgumentNullException` |
| `RemovePasteboard` | `scope` 必須 |
| `DetectPatterns` / `DetectValues` | 空配列は `CLIPBOARD_EMPTY_PATTERNS`（native 到達前） |
| main thread 限定 | `Instance` getter を含む。UI Toolkit のコールバックから呼ぶ限り常に満たされる |
| 破棄後 | 全操作が `CLIPBOARD_MANAGER_DESTROYED`。**サンプルは Manager を破棄しない** |

### 1.5 エラー契約

- 結果型は `IsSuccess` と `Error`（`IosClipboardErrorInfo?`）。**`ErrorMessage` は直下に存在しない**（`result.Error?.Message`）
- `Error` は `Code` / `Message`（非 null）＋ `Domain` / `NativeCode`（`details` があるときのみ）
- `CLIPBOARD_CANCELLED` は正常な打ち切りとして扱ってよい
- `ReadData` の「該当データなし」は失敗ではなく `HasData == false` の成功
- Editor では全操作が `CLIPBOARD_BRIDGE_UNAVAILABLE`

### 1.6 不足前提（サンプル側で補わない）

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
| `Runtime/Clipboard/IosClipboardManager.cs` | observation の owner / generation 契約（1.3） |
| native `ios/IosLibraryExample/ClipboardSampleView.swift`（1,070 行） | 機能一覧・fixture・結果表示・enabled 制御 |

### 2.2 native サンプルとの対応

| native セクション | Unity への反映 |
|---|---|
| `scopeSection`（6） | 反映する |
| `copySection`（12） | 反映する |
| `copyOptionsSection`（4） | 反映する |
| `appendSection`（3） | 反映する |
| `readSection`（4） | 反映する |
| `loadSection`（5） | 反映する（+1: 独自 UTI の load） |
| `detectSection`（4） | 反映する |
| `observeSection`（3） | 反映する（+1: Restart） |
| `pasteControlSection` | **反映しない**（P-16 未公開） |
| `clearSection`（1） | 反映する |
| `errorSection`（12） | 反映する（Paste Control 由来 2 種を除外して 10） |

**native から取り込む実装ディテール:**

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
| `Restart Observing` | O-2 の replacement を Unity から検証する導線。native サンプルには無い |

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
| `IosClipboardSampleObservationState`（`internal struct`） | **observation の owner / teardown 状態遷移（純粋）**。EditMode でテストする |
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
| **observation control の owner token** | `Begin*` が発行した owner と一致する callback だけが状態を変える | 拒否された呼び出しの callback が、進行中の呼び出しの状態を解除するのを防ぐ（6.5） |
| ステータス行 | `Scope: <label> \| Observing: <state> \| Events: <n>` | scope と observation は複数操作にまたがる状態 |
| 結果行に連番とマーカー | `#12 [copy.plainText] OK ...` | 同じ操作の連続実行で更新の有無が分かる |
| ボタンの enabled 制御 | observation 状態に連動（4.7） | native サンプルと同じ。矛盾操作を防ぐ |
| `SetResult` のログ抑止を既定に | `logMessage: false` を既定 | iOS は結果のほぼ全てが clipboard 由来 |
| `UnityMainThreadDispatcher` を Controller から使わない | Manager が全結果を main thread に載せる | 二重 dispatch を避ける |

### 3.3 ログ・表示規約

| 区分 | ログ | 画面表示 |
|---|---|---|
| 操作名・成否・エラーコード・件数・バイト数・`kind`・`utType` | 出す | 出す |
| clipboard 本文（`Text` / `UrlString` / `Path` / base64 / representations） | **出さない** | **長さ・有無に丸める**（`Path` は長さも出さない） |
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

### 4.3 セクションとボタン一覧（計 55 ボタン）

v2 の 53 から `RestartObservingButton`（M-16）と `LoadFileCustomButton`（M-13 の UTI 対応）を追加。

#### Scope（6）

| name | text | 動作 |
|---|---|---|
| `UseGeneralButton` | Use General | active scope を `General` に |
| `CreateNamedPasteboardButton` | Create Named Pasteboard | `CreatePasteboard(Named(FixedName))` → 成功かつ active が未変更なら active に |
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

#### Load（6）

| name | request | 対応 copy |
|---|---|---|
| `LoadTextButton` | `LoadRequest.Text` | `CopyPlainTextButton` |
| `LoadUrlButton` | `LoadRequest.Url` | `CopyUrlButton` |
| `LoadImageButton` | `LoadRequest.Image` | `CopyImageDataButton` |
| `LoadFileButton` | `LoadRequest.File("public.data")` | `CopyCustomDataButton`（4.5.1） |
| `LoadFileCustomButton` | `LoadRequest.File(CustomTypeIdentifier)` | `CopyMultiRepresentationButton`（4.5.1、**V-6**） |
| `CancelLoadsButton` | `CancelLoads()` | — |

#### Detect（4）

`CopyNumberFixtureButton` / `CopySearchFixtureButton` / `DetectPatternsButton` / `DetectValuesButton`

#### Observe（4）

| name | text | 動作 | 対応 |
|---|---|---|---|
| `StartObservingButton` | Start Observing | `StartObserving(active, onChanged: null, onStarted: …)` | M-15 |
| `RestartObservingButton` | Restart Observing (no stop) | 観測中に**もう一度** `StartObserving`。O-2 の replacement | M-16 後半 |
| `StopObservingButton` | Stop Observing | `StopObserving` | M-17 |
| `CheckForegroundChangeButton` | Check Foreground Change | `CheckForegroundChange(active)` | M-18 |

#### Clear（1）

`ClearActiveScopeButton`

#### Busy / Memory（4）— Unity 固有

| name | 動作 | 対応 |
|---|---|---|
| `BusyLoadItemTwiceButton` | `LoadItem(Text)` を連続 2 回。**context を 2 個**作り、1 本目と 2 本目が別 sequence で表示される | M-24 |
| `SeedAndCancelLoadButton` | `Copy(ImageData)` → 成功 callback 内で `LoadItem(Image)` を開始し、直後に `CancelLoads` | M-14 |
| `BusyStartObservingTwiceButton` | `StartObserving` を連続 2 回。**1 本目のみが状態所有者**、2 本目は `NonOwningToken`（6.5.3） | M-16 前半 |
| `CopyLargeImageDataButton` | **約 4 MiB** の ImageData を copy（4.5.2） | M-22 |

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

### 4.4 C# 例外になる入力の扱い

`IosPasteboardScope.Named("")` と `IosClipboardContent.Color(NaN, ...)` は `ArgumentException` を投げる。try/catch すればサンプルをクラッシュさせずに表示できるため、v1 の「ボタン化するとクラッシュする」という記述は誤りだった（v2 で撤回済み）。

**方針: ボタンは追加しない。** これらは Manager の呼び出しに到達しない呼び出し側バグであり、native のエラー契約デモとは性質が異なる。

**確定する文言**（実装者判断に委ねない）:

- subtitle 2（`SubtitleValidationLabel`）:
  `"Inputs that would throw in C# (blank pasteboard name, non-finite color) are not exposed as buttons; only native error contracts are shown below."`
- Errors セクション note（`ErrorsSectionNote`）:
  `"Every button here demonstrates a stable CLIPBOARD_* result contract. C# constructor exceptions that occur before a Manager call are not represented."`

  v3 の `"Every button here reaches the native layer"` は誤りだった。`ErrDetectEmptyPatternsButton` の `CLIPBOARD_EMPTY_PATTERNS` は Manager が native 到達前に返す（1.4）。文言を「安定した結果契約を見せる」に改め、Manager-side rejection も含むようにする。

両 label は wiring テストの必須 name 対象に含める（6.8）。

### 4.5 fixture 表（手動確認と 1 対 1 対応）

| fixture | 値 | 使用ボタン | 対応 M |
|---|---|---|---|
| `PlainTextBody` | `"Hello 日本語 \U0001F680 テスト"`（**日本語・絵文字・サロゲートペアを含む**。UTF-16 length = **16**） | `CopyPlainTextButton` | M-1 |
| `LocalOnlyBody` | 14 文字の固定 ASCII 文字列 | `CopyLocalOnlyTrue/FalseButton` | M-4 |
| `DeviceBBaseline` | 31 文字（`B` の繰り返し） | `CopyDeviceBaselineButton` | M-4 の device B 基準値 |
| `AppendMarker` | 24 文字（`"APPENDED-MARKER-"` + GUID 先頭 8 文字） | `AppendPlainTextButton` | M-6 |
| `FileFixturePayload` | **64 バイト**の `0x41` | `CopyCustomDataButton`（UTI = `public.data`） | M-13（`fileSize=64` を assert） |
| `DetectionFixture` | URL / email / 電話 / 住所 / 日時 / 便名 / 金額 / 追跡番号 を含む複合テキスト | `CopyDetectionFixtureButton` | M-12 |
| `NumberFixture` | `"42"` | `CopyNumberFixtureButton` | M-12（複合 fixture では検出されない） |
| `SearchFixture` | `"swift concurrency"` | `CopySearchFixtureButton` | M-12（同上） |
| `SmallPng` | 1x1 PNG（コード生成） | `CopyImageDataButton` / `CopyImageFileButton` / `LoadImageButton` | M-3 / M-13 |
| `LargePng` | **約 4 MiB**（4.5.2） | `CopyLargeImageDataButton` | M-22 |
| `FixedName` | `"com.jonghyunkim.nativetoolkit.example.sample"` | Scope セクション | M-9 |
| `CustomTypeIdentifier` | `"com.jonghyunkim.nativetoolkit.example.custom"` | `CopyMultiRepresentationButton` / `LoadFileCustomButton` | M-13 |

**`textLen` の定義**: C# `string.Length`、すなわち **UTF-16 code unit 数**。`PlainTextBody` は Unicode scalar 15 個・UTF-16 16 units のため `textLen=16` になる。長さ以外は表示しない。

**長さで識別する設計**: `LocalOnlyBody`(14) / `AppendMarker`(24) / `DeviceBBaseline`(31) は互いに長さが異なる。`Read` の表示に `textLen=<n>` を含めることで、**本文を出さずにどの fixture が届いたかを判別できる**（M-4 に必須）。

#### 4.5.1 copy 側 representation と load 側 request UTI の対応

`IosClipboardContent.CustomData(byte[] data, string utType)` は **UTI を 1 つしか持てない**。v2 は同じボタンに `public.data` と独自 UTI の両方を割り当てており、実装者がどちらを採るかで S-22 の期待値が変わってしまうため、次のとおり分離する。

| copy ボタン | 生成する representation | 対応する load ボタン | 期待 |
|---|---|---|---|
| `CopyCustomDataButton` | `CustomData(FileFixturePayload, "public.data")` | `LoadFileButton` = `File("public.data")` | `fileSize=64` |
| `CopyMultiRepresentationButton` | `MultiRepresentation({ "public.utf8-plain-text": …, CustomTypeIdentifier: FileFixturePayload })` | `LoadFileCustomButton` = `File(CustomTypeIdentifier)` | `fileSize=64`（**V-6**） |

独自 UTI 側は「独自 UTI の representation を登録して同じ UTI で load できるか」を見る項目であり、成立しない場合は `LoadFileCustomButton` の期待値を「エラーコードを記録する」に緩める（V-6）。`public.data` 側（M-13 本体）はこの結果に依存しない。

#### 4.5.2 `LargePng` の生成条件

| 項目 | 内容 |
|---|---|
| 解像度 | 1024 x 1024 RGBA32 |
| ピクセル生成 | **固定 seed（`0x5EED_C10B`）のローカル PRNG**（32bit xorshift を helper 内に持つ）。`UnityEngine.Random` は global state を変更し他サンプルへ影響するため使わない |
| 目的 | 圧縮が効かないノイズにして PNG を数 MiB にする |
| 生成後 | `EncodeToPNG()` の byte length を結果行に `bytes=<n>` として表示する |
| 受入範囲 | **3〜5 MiB**。範囲外なら `Copy` を呼ばず `#n [copy.largeImage] NG fixture=out-of-range bytes=<n>` を表示して終了する |
| 解放 | 4.6 |

同じ seed のため実行ごとに同じ PNG になり、M-22 のピークメモリ比較が run 間で成立する。

### 4.6 リソース所有契約

| リソース | 契約 |
|---|---|
| `Texture2D`（PNG 生成） | `try { … EncodeToPNG() } finally { UnityEngine.Object.Destroy(texture); }`。生成 helper 内で完結し、Texture を外へ返さない |
| `LargePng` のバイト列 | ボタン押下ごとに生成し、`Copy` 呼び出し後は参照を保持しない（GC 対象にする） |
| `ImageFile` の書き出し先 | `Application.persistentDataPath` 直下の固定ファイル名。上書きするため蓄積しない |
| **`LoadItem(File)` の返却パス** | **caller-owned**。`IosClipboardLoadedItem.Path` の XML コメントどおり、呼び出し側に削除責任がある。**画面が破棄されていても削除する**（6.6） |

**`LoadItem(File)` 成功時の処理（v3 で全経路を確定）:**

```
long size = -1;
try   { size = new FileInfo(path).Length; }
catch { /* Debug.LogWarning with the exception message only. */ }

bool cleaned = TryDeleteRequestDirectory(path);   // 例外は内部で握りつぶし、bool で返す
// 表示: fileSize=<size> cleanup=<ok|failed>      （size 取得失敗時は fileSize=-1）
```

| 経路 | 表示 |
|---|---|
| size 取得成功・削除成功 | `fileSize=64 cleanup=ok` |
| size 取得成功・削除失敗 | `fileSize=64 cleanup=failed` |
| size 取得失敗・削除成功 | `fileSize=-1 cleanup=ok` |
| size 取得失敗・削除失敗 | `fileSize=-1 cleanup=failed` |

**size 取得の失敗が削除をスキップさせない**（v2 は try の中で連続実行しており、この経路で caller-owned resource が残っていた）。

**パスは表示にもログにも出さない。長さも出さない。** 削除範囲は「返却ファイルの親ディレクトリ」に固定する（native が request ごとに一時ディレクトリを作る契約に依存するため、**要検証 V-2**）。

### 4.7 observation 状態と enabled 契約

| 状態 | `IsObserving` | `ControlPending` |
|---|---|---|
| 未観測 | false | false |
| Start 実行中 | false | true |
| 観測中 | true | false |
| Restart 実行中 | true | true |
| Stop 実行中 | true | true |

| 要素 | enabled 条件 |
|---|---|
| Scope セクションの 6 ボタン | `!IsObserving && !ControlPending` |
| `StartObservingButton` | `!IsObserving && !ControlPending` |
| `RestartObservingButton` | `IsObserving && !ControlPending` |
| `StopObservingButton` | `IsObserving && !ControlPending` |
| `ErrObserveMissingNamedButton` | `!IsObserving && !ControlPending` |
| `BusyStartObservingTwiceButton` | `!IsObserving && !ControlPending` |
| 上記以外 | 常に enabled |

**status の scope 表記**: 観測中は `Scope: <active> (observing <observed>)` とし、**Start / Restart を発行した時点で capture した `targetScope`** を併記する（6.5.4）。scope ボタンを無効化しているため通常は一致するが、Start の**発行前**に始まった `CreatePasteboard` が Start pending 中に完了すると `_activeScope` だけが変わりうる。この場合 native は `targetScope` を観測しているので、**callback 時点の `_activeScope` を記録してはならない**（V-5）。`Restart` は `targetScope` を capture し直す。

### 4.8 結果表示

```
#12 [copy.plainText] OK
#13 [read] OK items=2 firstItemTypes=1 textLen=16
#14 [copy.plainText] NG code=CLIPBOARD_EMPTY_CONTENT message=Clipboard content is empty. Please provide text or HTML.
#15 [load.image] -- code=CLIPBOARD_CANCELLED message=The clipboard load was cancelled.
```

- `OK` / `NG` / `--`（`CLIPBOARD_CANCELLED` のみ）
- `Error.Domain` / `NativeCode` があるときのみ ` details=<domain>:<code>`
- 成功時の payload は 4.9

### 4.9 成功時の表示（content を丸める）

| 操作 | 表示内容 |
|---|---|
| `Read` | `items=<n> firstItemTypes=<n> textLen=<n or ->`（`textLen` は最初の text を持つ item の UTF-16 長。**本文は出さない**） |
| `ReadData` | `hasData=<bool> utType=<utType> bytes=<n>` |
| `GetSnapshot` | `items=<n> strings=<b> urls=<b> images=<b> colors=<b> matching=<null or n>` |
| `CreatePasteboard` | `scope=<kind>(len=<n>)` |
| `DetectPatterns` | `patterns=<n>` |
| `DetectValues` | `patterns=<n> emails=<n> phones=<n> addresses=<n> events=<n> flights=<n> money=<n> shipments=<n> links=<n>` |
| `LoadItem` | `kind=<kind>` ＋ Text は `textLen=<n>`、ImageData は `bytes=<n> utType=<t>`、File は `fileSize=<n> cleanup=<ok\|failed>` |
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
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleObservationState.cs` | observation の owner / 状態遷移の純粋 helper（`internal struct`） |
| `Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml` | 画面定義 |
| `Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExampleStyle.uss` | スタイル |
| `Tests/Runtime/IosClipboardSampleSceneWiringTests.cs` | wiring テスト |
| `Tests/Runtime/IosClipboardSampleStateTests.cs` | 結果 context / observation 状態のテスト |

### 5.2 既存変更

| パス | 変更内容 |
|---|---|
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowIosClipboard` の追加（`#if UNITY_IOS \|\| UNITY_EDITOR`）と `RemoveExistingControllers` への登録 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | ①配線ガードを `#if UNITY_ANDROID \|\| UNITY_IOS \|\| UNITY_EDITOR` に拡張 ②`OnClipboardClicked` に `#elif UNITY_IOS → ShowIosClipboard` を追加 ③**Editor 分岐を 2 択ダイアログに変更**（6.9） |

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
| V-2 | `LoadItem(File)` の返却パスの**親ディレクトリ**を削除してよいこと。実機で 1 回削除し、後続の `LoadFile` が成功することを確認する |
| V-3 | `ErrCopyColorOutOfRangeButton` が C# 例外にならず `CLIPBOARD_INVALID_COLOR` を返すこと |
| V-4 | `LargePng`（約 4 MiB）の生成が実機で許容できる時間・メモリに収まり、PNG が 3〜5 MiB に収まること |
| V-5 | Start pending 中に `CreatePasteboard` の完了が届き、`active` と `observing` が食い違う status 表記（4.7）が実際に発生しうるか |
| V-6 | `MultiRepresentation` で登録した独自 UTI を `LoadItem(File(CustomTypeIdentifier))` で取得できること（4.5.1） |

v3 の V-7（Restart 失敗時に旧 observation が残るか）は、native source で確定したため**削除した**（1.3 O-5）。

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

    // 55 個の Button? フィールド
}
#endif
```

ガードは `#if UNITY_IOS || UNITY_EDITOR`。**ハンドラ内にプラットフォームガードを置かない**（`IosShareManagerExampleController` と同じく Manager の B-1 に委ねる）。

### 6.2 結果 context

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

`BeginResult` は **screen-aware** にする。deferred Stop（6.5.6）は画面破棄後に発行されるため、`BeginResult` が無条件に `SetResult` すると、必須 cleanup の途中で破棄済みの UI に触れることになる。

```csharp
/// Allocates the identity of a new call. Safe to call after the screen is gone: the sequence and
/// marker are still allocated for the log, but no UI is touched.
private IosClipboardSampleResultContext BeginResult(string marker)
{
    _resultSequence++;
    var context = new IosClipboardSampleResultContext(_resultSequence, marker);
    Debug.Log($"[{LogTag}][{nameof(BeginResult)}] seq: {context.Sequence}, marker: {context.Marker}");
    if (IsScreenAlive())
    {
        SetResult(IosClipboardSampleResult.FormatRunning(context));
    }
    return context;
}
```

`IsScreenAlive()` は `this != null && isActiveAndEnabled`（6.6）。

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
- 例外は `ClipboardChanged`。これは呼び出しに紐づかない継続イベントなので、**UI と status を更新する唯一の共通イベント**であり、**`Events` カウントの唯一の発生源**（O-4）

### 6.4 完了ハンドラ

```csharp
private void CompleteOperation(IosClipboardSampleResultContext context, IosClipboardOperationResult result)
{
    Debug.Log($"[{LogTag}][{nameof(CompleteOperation)}] seq: {context.Sequence}, marker: {context.Marker}, " +
              $"operation: {result.Operation}, isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
    if (!IsScreenAlive()) return;                       // 6.6
    if (!result.IsSuccess) { SetResult(IosClipboardSampleResult.FormatFailure(context, result.Error!.Value)); return; }
    SetResult(IosClipboardSampleResult.FormatSuccess(context, string.Empty));
}
```

`Read` / `ReadData` / `Snapshot` / `Detect*` / `CheckForegroundChange` も同形で、payload の組み立てだけが異なる（4.9）。`LoadItem` と observation 系は必須処理があるため 6.6 の順序に従う。

#### 6.4.1 scope を変更する操作の所有条件

```csharp
private void OnRemoveActivePasteboardClicked()
{
    IosPasteboardScope target = _activeScope;              // capture at call time
    var context = BeginResult("scope.remove");
    IosClipboardManager.Instance.RemovePasteboard(target, result =>
    {
        if (!IsScreenAlive()) return;                      // scope mutation is screen state only
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
            if (!IsScreenAlive()) return;           // do not start new demo work off-screen (6.6)

            var load = BeginResult("cancel.loadImage");
            IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.Image, _activeScope,
                r => CompleteLoadItem(load, r));

            var cancel = BeginResult("cancel.cancelLoads");
            IosClipboardManager.Instance.CancelLoads(r => CompleteOperation(cancel, r));
        });
}
```

seed copy が失敗した場合、および seed 完了時点で画面が生きていない場合は load を開始しない。3 つの context が別々に表示される。

### 6.5 observation の owner token と teardown（レビュー高優先度の反映）

#### 6.5.1 なぜ owner token が必要か

`BusyStartObservingTwiceButton` は `StartObserving` を 2 回続けて呼ぶ。Manager は 2 本目を `CLIPBOARD_BUSY` で即時拒否するが、**その拒否も per-call callback として届く**。v2 のように両方の callback が同じ `CompleteStart(isSuccess)` を通ると、2 本目の失敗が `ControlPending` を false に戻し、1 本目がまだ native pending であるにもかかわらず状態機械は「未観測」へ戻る。その状態で画面を離れると `RequestStop()` が deferred stop を記録できず、後から成功した 1 本目の observation が画面外に残る。

Manager 側は安全である（O-1: 拒否は `s_onChanged` を書き換える前に return する）。壊れるのは**サンプルの状態機械だけ**なので、owner token で解決する。

#### 6.5.2 状態機械

```csharp
/// Pure state machine for the observation lifecycle.
///
/// StartObserving and StopObserving share a single-flight key, so a call issued while another is
/// pending is rejected with CLIPBOARD_BUSY — and that rejection arrives on the rejected call's own
/// callback. Only the call that took ownership may change the state; otherwise a rejected second
/// start would clear ControlPending while the first start is still running natively, and a later
/// screen teardown would fail to stop the observation it eventually creates.
internal struct IosClipboardSampleObservationState
{
    internal const int NonOwningToken = 0;

    internal bool IsObserving { get; private set; }
    internal bool ControlPending { get; private set; }
    internal bool StopRequestedAfterStart { get; private set; }

    /// Returns a fresh owner token, or NonOwningToken when another control call is already pending.
    internal int BeginStart();
    internal int BeginStop();

    /// Returns true when the callback owned the state and the state actually changed.
    internal bool CompleteStart(int owner, bool isSuccess);
    internal bool CompleteStop(int owner, bool isSuccess);

    internal void RequestStop();          // OnDisable
    internal bool ShouldIssueStopNow();   // IsObserving && !ControlPending
}
```

| 現在 | 契機 | 次 | 副作用 |
|---|---|---|---|
| 未観測 / Pending なし | Start クリック | `ControlPending = true`, owner 発行 | `StartObserving` 発行 |
| 観測中 / Pending なし | **Restart クリック** | `ControlPending = true`, owner 発行、`IsObserving` は **true のまま** | `StartObserving` 発行（O-2 の replacement） |
| Pending 中 | 追加の `BeginStart` / `BeginStop` | 状態不変 | `NonOwningToken` を返す（Busy デモの 2 本目） |
| Start / Restart 実行中 | 成功（owner 一致） | `IsObserving = true`, `ControlPending = false` | `StopRequestedAfterStart` なら Stop を発行 |
| Start / Restart 実行中 | **失敗（owner 一致）** | **`IsObserving = false`, `ControlPending = false`, `StopRequestedAfterStart = false`** | 何もしない。native は既に未観測（O-5 / 6.5.3） |
| 任意 | **owner 不一致の完了** | **状態不変** | 結果表示のみ |
| Start 実行中 | `OnDisable` | `StopRequestedAfterStart = true` | **Stop を発行しない**（busy になるため） |
| 観測中 | Stop クリック / `OnDisable` | `ControlPending = true`, owner 発行 | `StopObserving` 発行 |
| Stop 実行中 | 成功（owner 一致） | すべて false（`StopRequestedAfterStart` も消費） | - |
| Stop 実行中 | 失敗（owner 一致） | `ControlPending = false`, `StopRequestedAfterStart = false`, `IsObserving` は true のまま | **再発行しない**（6.5.6）。画面上なら Stop ボタンで再試行できる |

#### 6.5.3 Start failure の扱い（v4 で確定）

v3 は「Restart 失敗時に旧 observation が残るか判別できない」として `IsObserving` を維持していたが、native source で確定できた。

`IosClipboardManager.swift` の `startObserving` は **scope 解決の guard より前**に `stopObservingInternal()` を呼ぶ（`:321` → `:322-325`）。したがって `pasteboardUnavailable` で throw した時点で、旧 subscription の notification token / scope / callback は既に破棄されている（O-5）。native sample も start failure で `isObserving = false` にしている。

**owned な Start failure は、初回 / Restart を問わず未観測へ落とす。**

managed rejection（bridge 到達前）との差分は次のとおりで、いずれもこの規則で矛盾しない。

| 失敗の種類 | native へ到達 | 旧 observation | サンプルの扱い |
|---|---|---|---|
| native の `CLIPBOARD_UNAVAILABLE`（scope 解決失敗） | する | **破棄済み**（O-5） | 未観測へ |
| `CLIPBOARD_BRIDGE_UNAVAILABLE`（Editor） | しない | そもそも存在しない | 未観測へ（矛盾しない） |
| `CLIPBOARD_BUSY` | しない | 残る | **owned にならない**（`BeginStart` が `NonOwningToken` を返す）ため状態に触れない |
| `CLIPBOARD_MANAGER_DESTROYED` | しない | 残るが以後どの操作も拒否される | 未観測へ（Stop も不可能なので状態を維持しても意味がない）。サンプルは Manager を破棄しないため到達しない |
| `CLIPBOARD_NOT_MAIN_THREAD` | しない | 残る | UI callback からのみ呼ぶため到達しない |

#### 6.5.4 Start / Restart の発行と `targetScope` の capture（v4 で修正）

v3 の擬似コードは成功 callback で `_observedScope = _activeScope` としていたが、これは **callback 時点**の scope であり、`Start` を発行した時点の scope ではない。Start の発行**前**に始まった `CreatePasteboard` が Start pending 中に完了すると `_activeScope` が変わるため、native が観測している scope と status の表示がずれる（4.7 / V-5）。

**`targetScope` は owner token と同じ call context に capture する。**

```csharp
/// Issues one StartObserving. The owner token and the scope this call asked for are both captured
/// here: the state machine and the status line must describe the same call, and _activeScope can
/// change while the start is pending (a CreatePasteboard issued earlier may complete first).
private void IssueStartObserving(string marker, int owner)
{
    IosPasteboardScope targetScope = _activeScope;
    var context = BeginResult(marker);
    IosClipboardManager.Instance.StartObserving(
        targetScope,
        onChanged: null,                                    // 6.5.5
        onStarted: r => CompleteStartObserving(context, owner, targetScope, r));
}
```

`CompleteStartObserving` は **owner が一致し、かつ成功した場合にのみ** `_observedScope = targetScope` とする。非所有の Busy callback は `_observedScope` を変更しない。

Busy デモは 1 本目だけを状態所有者にする。

```csharp
private void OnBusyStartObservingTwiceClicked()
{
    // The first call owns the observation state; the second is issued purely to show the
    // CLIPBOARD_BUSY contract and must never touch it.
    IssueStartObserving("observe.busy#1", _observation.BeginStart());
    IssueStartObserving("observe.busy#2", IosClipboardSampleObservationState.NonOwningToken);
}
```

`BeginStart()` は Pending 中に呼ばれると `NonOwningToken` を返すため、仮に 2 本目も `BeginStart()` 経由で発行しても状態は壊れない。**二重の防御**とする。

`ErrObserveMissingNamedButton` も `StartObserving` であり、**`IssueStartObserving` と `BeginStart()` を経由する**。失敗を前提としたボタンだが、万一成功した場合に追跡されない observation が残ることを避ける。

#### 6.5.5 `onChanged` は常に `null`

`ClipboardChanged`（共通イベント）と `onChanged`（per-registration）は同じ変更で両方発火する（O-4）。両方で `Events` を数えると 1 回の変更が 2 と表示され、M-16 の「1 だけ増える」判定が成立しない。**サンプルは `onChanged: null` を渡し、共通 `ClipboardChanged` のみを計数源・status 更新源とする。**

#### 6.5.6 Stop の発行と deferred Stop の終端（v4 で確定）

```csharp
/// Issues one StopObserving. Also used for the deferred stop that a start completing after
/// teardown must issue, so it must not assume the screen is alive; BeginResult (6.2) already
/// skips the UI in that case.
private void IssueStopObserving(string marker)
{
    var context = BeginResult(marker);
    int owner = _observation.BeginStop();
    IosClipboardManager.Instance.StopObserving(r => CompleteStopObserving(context, owner, r));
}
```

**`OnDisable` の順序**:

1. `_observation.RequestStop()`
2. `if (_observation.ShouldIssueStopNow())` → `IssueStopObserving("observe.stop.teardown")`
3. **その後に**共通イベント 10 個を解除

per-call callback はイベントではないため、解除後も届く。Start pending 中の離脱では Stop を発行せず、Start の per-call callback が発行を引き継ぐ。

**deferred Stop の発行条件（1 回だけ）**:

```
owned && isStart && result.IsSuccess && StopRequestedAfterStart && ShouldIssueStopNow()
```

- `isStart` に限定する。`CompleteStop` から再度 Stop を発行させない
- `CompleteStop` は**成功・失敗のいずれでも** `StopRequestedAfterStart` を消費して false にする
- **再試行しない。** native の `stopObserving` は常に成功するため（O-6）、Stop failure は managed rejection だけであり、同じ callback から再発行しても結果は変わらない。失敗時は `Debug.LogWarning` にコードのみを残して終端する

これにより「画面破棄後の Start 成功 → Stop がちょうど 1 回発行 → Stop 失敗 → 2 回目は発行されない」が保証される（6.8.2 のテスト対象）。

### 6.6 画面破棄後 callback の共通順序（レビュー中優先度 4 の反映）

Navigator は画面切り替えで Controller を `Destroy` するため、**任意の操作 callback が画面離脱後に届きうる**。UI に触れないだけでは不十分で、処理ごとに「画面外でも必須」「画面外では禁止」を分ける。

**確定する順序**（全 callback 共通）:

```
1. ログ（shape only）
2. 画面外でも必須の state / ownership cleanup
3. IsScreenAlive() ガード           // this == null || !isActiveAndEnabled → return
4. UI 更新・status 更新・enabled 更新・後続デモ操作
```

**ガードで callback 全体を早期 return してはならない**（deferred Stop と file cleanup が実行されなくなる）。

| callback | 画面外でも必須（手順 2） | 画面外では禁止（手順 4） |
|---|---|---|
| `StartObserving` / `Restart` | `CompleteStart(owner, …)`、条件を満たすときの deferred Stop 発行（6.5.6）、`_observedScope` の更新 | 結果行・status・enabled |
| `StopObserving` | `CompleteStop(owner, …)`、`_observedScope` の解除 | 結果行・status・enabled |
| `LoadItem(File)` | size 取得と **request directory 削除**（4.6） | 結果行 |
| `SeedAndCancelLoad` の seed `Copy` | なし | **後続の Load + Cancel の開始**、結果行 |
| `CreatePasteboard` / `RemovePasteboard` | なし（`_lastRemovedScope` は画面状態にすぎない） | active scope の変更、結果行、status |
| その他すべて | なし | 結果行 |

`_observedScope` は status の材料でしかないが、画面外でも state と同時に更新して整合を保つ（再入場せずに破棄されるため実害はない一方、条件分岐が減る）。

```csharp
private void CompleteStartObserving(
    IosClipboardSampleResultContext context, int owner, IosPasteboardScope targetScope,
    IosClipboardOperationResult result)
{
    Debug.Log($"[{LogTag}][{nameof(CompleteStartObserving)}] seq: {context.Sequence}, owner: {owner}, " +
              $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");

    // Mandatory: runs even after the screen is gone, otherwise a start that completes after
    // teardown leaves the native observation running with nothing left to stop it.
    bool owned = _observation.CompleteStart(owner, result.IsSuccess);
    if (owned)
    {
        // The scope captured when this call was issued — not _activeScope, which may have been
        // replaced by a CreatePasteboard that completed while this start was pending.
        _observedScope = result.IsSuccess ? targetScope : null;

        if (result.IsSuccess && _observation.StopRequestedAfterStart && _observation.ShouldIssueStopNow())
        {
            IssueStopObserving("observe.stop.deferred");   // issued exactly once (6.5.6)
        }
    }

    if (!IsScreenAlive()) return;
    SetResult(...); UpdateStatus(); UpdateEnabledStates();
}

private void CompleteStopObserving(
    IosClipboardSampleResultContext context, int owner, IosClipboardOperationResult result)
{
    Debug.Log($"[{LogTag}][{nameof(CompleteStopObserving)}] seq: {context.Sequence}, owner: {owner}, " +
              $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");

    // CompleteStop consumes the deferred request in both outcomes, so no stop is ever re-issued
    // from here. Native stopObserving cannot fail (O-6); a failure here is a managed rejection.
    bool owned = _observation.CompleteStop(owner, result.IsSuccess);
    if (owned && result.IsSuccess) { _observedScope = null; }
    if (owned && !result.IsSuccess)
    {
        Debug.LogWarning($"[{LogTag}][{nameof(CompleteStopObserving)}] stop rejected: {result.Error?.Code}");
    }

    if (!IsScreenAlive()) return;
    SetResult(...); UpdateStatus(); UpdateEnabledStates();
}
```

`IsScreenAlive()` は `this != null && isActiveAndEnabled`。破棄済み `MonoBehaviour` でも managed オブジェクトは生きているため、手順 2 のフィールド操作と `IosClipboardManager.Instance`（別オブジェクト）の呼び出しは安全である。

### 6.7 入力バリデーション方針

- UI 側で入力バリデーションを行わない（入力欄が無い）
- C# 例外に該当する値はボタンに割り当てない（4.4）
- `RemovePasteboard(General)` は native の `CLIPBOARD_CANNOT_REMOVE_GENERAL` をそのまま見せる
- `ProbeRemovedScopeButton` は `_lastRemovedScope == null` のとき**画面に注意文を出して Manager を呼ばない**。この 1 箇所のみ画面側の前提チェックを行う

### 6.8 テスト

#### 6.8.1 wiring テスト（`IosClipboardSampleSceneWiringTests`）

| 検証 | 内容 |
|---|---|
| Resources パス | `UI/iOS/Clipboard/IosClipboardManagerExample` / `...Style` |
| 必須ボタン名 | 4.3 の 55 個 + `HomeButton` |
| 必須ラベル名 | `ResultTextBlock` / `StatusTextBlock` / `SubtitleValidationLabel` / `ErrorsSectionNote` |
| TopMenu | `ClipboardFeatureButton` の存在 |

#### 6.8.2 状態テスト（`IosClipboardSampleStateTests`）

UXML wiring テストでは主要な状態バグを検出できないため、純粋 helper を切り出して検証する。

| 対象 | 検証内容 |
|---|---|
| `IosClipboardSampleResult.Format*` | context の sequence / marker がそのまま出力に載る。`CLIPBOARD_CANCELLED` が `--` になる。`details` の有無で表記が変わる |
| **marker correlation** | 2 つの context を作り、**完了順を入れ替えて**整形しても、それぞれ自分の sequence / marker で出力されること |
| `LoadItem(File)` 表示 | 4.6 の 4 経路すべてが `fileSize=<n or -1> cleanup=<ok\|failed>` になること（size 取得と削除の結果を引数に取る純粋関数として切り出す） |
| **owner 競合** | `Start #1 pending → Start #2 が NonOwningToken → Start #2 失敗完了` で `ControlPending` が **true のまま**であること |
| **owner 競合 + teardown** | `Start #1 pending → Start #2 busy → RequestStop → Start #1 成功 → ShouldIssueStopNow() == true → Stop 成功` の全経路 |
| Restart | `観測中 → BeginStart → 成功` で `IsObserving` が true のまま、`ControlPending` が false に戻ること |
| **Restart 失敗**（v4 で反転） | 失敗で `IsObserving` / `ControlPending` / `StopRequestedAfterStart` がすべて **false** になること（O-5）。続けて enabled 契約が `Start enabled / Restart disabled / Stop disabled` になること |
| Stop 失敗 | `IsObserving` が true のまま、`ControlPending` と `StopRequestedAfterStart` が false になること |
| **deferred Stop の終端**（v4 で追加） | `Start pending → RequestStop → Start 成功 → Stop がちょうど 1 回発行 → Stop 失敗 → 2 回目が発行されない`。発行回数はカウンタで数える |
| **deferred Stop と Start failure** | `Start pending → RequestStop → Start 失敗` で Stop が **1 回も発行されない**こと（native は既に未観測） |
| **`targetScope` の対応**（v4 で追加） | `Start(A) 発行 → active が B へ変化 → Start 成功` で `observed == A` になること。非所有 callback では `observed` が変化しないこと。ownership + scope を扱う純粋 helper へ切り出して検証する |
| 重複 Stop | Stop pending 中の `RequestStop` で `ShouldIssueStopNow()` が false のままであること |
| enabled 契約 | 4.7 の各状態で Scope / Start / Restart / Stop の enabled が期待どおりになること（純粋関数として切り出す） |

Controller 本体は `Start` で `UIDocument` に触れるため、EditMode ではインスタンス化しない（`testing.md` の層 1 制約）。

### 6.9 TopMenu の Editor 導線

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

### 6.10 実装順序

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
| S-6 | Editor で Start Observing → 画面離脱 | Start は B-1 で失敗し、observation 状態が未観測に戻る。`NullReferenceException` が出ない |

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
| S-15 | `Copy Plain Text` → `Append Plain Text` → `Read` で **`items=2`** | M-6 |
| S-16 | 他アプリでコピー → `Snapshot` → `Read` | M-7（貼り付け許可 UI の有無を記録） |
| S-17 | 何もコピーしていない scope で `Read Data (public.png)` | M-8（`hasData=false` の成功） |
| S-18 | `Create Named` → `Copy Plain Text` → `Read` → `Remove Active` → `Probe Last Removed` | M-9 |
| S-19 | `Use General` → `Remove Active` | M-10 |
| S-20 | `Create Unique` → `Copy` → `Read` | M-11 |
| S-21 | `Copy Detection Fixture` → `Detect Patterns` / `Detect Values`。続けて `Copy Number Fixture` → `Detect Patterns`、`Copy Search Fixture` → `Detect Patterns` | M-12（**検出値がログに出ていないこと**を併せて確認） |
| S-22 | `Copy Custom Data` → `Load File (public.data)` で **`fileSize=64 cleanup=ok`**。続けて `Load Image` / `Load Text` / `Load URL` | M-13 |
| S-22b | `Copy Multi Representation` → `Load File (custom UTI)` | M-13 / **V-6** |
| S-23 | `Seed And Cancel Load` | M-14（load が `CLIPBOARD_CANCELLED`、cancel が OK） |
| S-24 | **前提: 未観測**。`Start Observing` → 他アプリでコピー → アプリへ復帰 → `Events` が増える → **`Stop Observing` で片付ける** | M-15 |
| S-25a | **前提: 未観測**。`Busy Start Observing Twice` → 2 本目が `CLIPBOARD_BUSY`。その後 `Observing: on` になり **Stop が有効・Start が無効**であること | M-16 前半（+ owner 契約の実機確認） |
| S-25b | **S-25a の直後（observing on）から続ける**。Stop を押さずに `Back To Home` → 再入場（`Observing: off` に戻っていること）→ 他アプリでコピー → `Events` が増えないこと | teardown（画面外に observation を残さない） |
| S-25c | **前提: S-25b 後の未観測**。`Start Observing` 成功 → **Stop を挟まず** `Restart Observing (no stop)` → 他アプリで **1 回だけ**コピー → 復帰し `Events` が **1 だけ増える** | M-16 後半（世代ゲート、O-2） |
| S-26 | **S-25c の直後（observing on）から続ける**。`Stop Observing` → 他アプリでコピー | M-17（`Events` が増えない） |
| S-25d | **前提: S-26 後の未観測**。`Err Observe Missing Named` → `CLIPBOARD_UNAVAILABLE` で失敗し、**`Observing: off` のまま Start が有効**であること | O-5（Start failure = 未観測）の実機確認 |
| S-27 | `Check Foreground Change` をバックグラウンド復帰後に実行 | M-18 |
| S-28 | 画面遷移・アプリ終了 | M-19 |
| S-29 | 全操作のログ確認 | M-20（本文・base64・検出値・pasteboard 名・**パス**が出ていないこと） |
| S-30 | `Copy Plain Text`（成功系）と `Copy (expires in 30s)` を過去日時に変えたビルド（失敗系） | M-21（**要検証**: 失敗系を fixture だけで作れるか。作れない場合は `Err Copy Invalid URL` で代替） |
| S-31 | `Copy Large Image Data`（約 4 MiB、`bytes=` を記録）→ `Read Data (public.png)` | M-22 |
| S-32 | `Busy Load Item Twice` | M-24 |

### 7.3 Errors セクションの確認

4.3 の 10 ボタンについて、表示コードが期待どおりであることを 1 件ずつ確認する。

### 7.4 M-22 / M-23 の計測境界

**M-22（数 MiB）— 本サンプルで実施する**

1. Xcode で Instruments の Allocations を attach
2. `Copy Large Image Data` → `Read Data (public.png)` を 3 回繰り返す
3. 記録項目: `Persistent Bytes` のピーク、実行後に baseline へ戻るか、`bytes=` 表示値との比
4. `LargePng` は固定 seed のため 3 回とも同一のバイト列になる（4.5.2）

**M-23（64MiB 近傍）— 本サンプルでは実施しない**

実装計画 v5 の M-23 は、実機 IL2CPP / ARM64 で上限近傍の `LoadItem(Image)` を通し、**native JSON → `DllImport` の UTF-16 string マーシャリング → parser → decoded `byte[]`** を含むピークと OOM の有無を測る項目である。v5 の 5.9.2 が残存リスクとしている約 170 MB の UTF-16 マーシャリングは、**この経路を通さなければ評価できない**。

| 区分 | 位置づけ | 内容 |
|---|---|---|
| **補助測定**（先行して実施可） | M-23 の一部にすぎない | 応答 JSON を直接組み立てる EditMode / PlayMode ベンチ。`TryGetDecodedLength` の上限判定でバッファを確保しないこと、`Data.Length == byteCount` 検証までの managed ピークを測る。**native callback と `DllImport` の string マーシャリングを迂回するため、M-23 の代替にはならない** |
| **M-23 本体** | 別 artifact へ引き継ぐ | 実機で上限近傍の応答を native から返し、Unity の P/Invoke callback まで通す |

M-23 本体の要件:

| 項目 | 内容 |
|---|---|
| fixture 供給 | native 側で**事前生成またはファイルから読み込む**（Unity 側で数十 MB を生成しない）。サンプル UI からは供給しない |
| 経路 | native 応答 → `[DllImport("__Internal")]` の `string` マーシャリング → `IosClipboardJsonParser` → decoded `byte[]` → callback 復帰 |
| 計測区間 | **`LoadItem` 呼び出し直前から、callback 復帰・結果参照の解放後まで**。fixture 生成コストは区間外に置く |
| 記録項目 | Instruments の native / managed ピーク、OOM の有無、`CLIPBOARD_CONTENT_TOO_LARGE` になるサイズ境界 |
| 記録先 | 実装計画 v5 の 9.4 を更新する |
| 状態 | **本計画のスコープ外。実施前に別 artifact（native fixture の追加要否を含む）が必要** |

---

## 8. 出力ルールの遵守

- 実装結果由来（1 章）と、サンプル計画時の追加判断（2.2 の「Unity 側で追加するもの」、3.2、4.4〜4.7、6.2、6.5、6.6、6.9）を分離した
- native source で確定できた事項（O-5 / O-6）は、推測ではなく参照元とともに 1.3 に記載した
- Manager の observation 契約は実装コードを再確認して 1.3 に根拠付きで記載した
- 既存サンプルの深掘り結果（2.3 / 2.4 / 2.5）と、native から取り込む実装ディテール（2.2）を記載した
- 共通実装パターンの維持（3.1）と拡張（3.2）を分けた
- 変更対象は具体パスで示した（5 章）
- 不確実な事項は V-1〜V-6 および S-30 の要検証として明記した（v3 の V-7 は native source で確定したため削除した）

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
