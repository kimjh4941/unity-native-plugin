# macOS Clipboard サンプルシーン実装計画書 (v1)

- 対象機能: clipboard
- 対象プラットフォーム: macOS
- 対象パッケージ: `Packages/com.jonghyunkim.nativetoolkit`
- 作成日: 2026-09-05
- 前提とする実装結果: `artifact/results/clipboard/2026-09-03-macos-clipboard-implementation-feature-result-v3.md`（段 2 / 5 操作）、`-v4.md`（段 3 / 10 操作）
- 参照する実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v12.md`
- 出力範囲: ExampleController・UXML/USS・ナビゲーション導線・EditMode の wiring / state テスト。Runtime の Manager には**原則触れない**（例外は 9.2）

---

## 0. この計画の位置づけ

**このサンプルシーンは実機確認 32 項目（計画書 7.5）の実行環境である。** 見た目のデモではなく、32 項目を漏れなく駆動できることが第一の要件になる。

macOS Clipboard の Manager 実装は完了済み（EditMode 517 / PlayMode 116、失敗 0）。ただし **P/Invoke 境界は一度も実行されていない**。Editor では `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` により P/Invoke がコンパイルされないため、テストはすべてその手前で止まっている。本サンプルをビルドして動かすことが、ブリッジの初回実行になる。

---

## 1. 前提情報（実装結果 v3 / v4 由来）

### 1.1 公開 API（15 操作 / 13 イベント）

| カテゴリ | 操作 | 結果型 | 共通イベント |
| --- | --- | --- | --- |
| 書き込み | `Copy` / `Append` | `MacClipboardOwnershipResult` | `OwnershipChanged` |
| 読み出し | `Read` | `MacClipboardReadResult` | `ReadCompleted` |
| 読み出し | `ReadData` | `MacClipboardReadDataResult` | `ReadDataCompleted` |
| 読み出し | `Snapshot` | `MacClipboardSnapshotResult` | `SnapshotCompleted` |
| 消去 | `Clear` | `MacClipboardChangeCountResult` | `ClearCompleted` |
| pasteboard | `CreatePasteboard` | `MacPasteboardScopeResult` | `PasteboardCreated` |
| pasteboard | `RemovePasteboard` | `MacClipboardOperationResult` | `ClipboardOperationCompleted` |
| 検出 | `DetectPatterns` | `MacClipboardDetectedPatternsResult` | `PatternsDetected` |
| 検出 | `DetectValues` | `MacClipboardDetectedValuesResult` | `ValuesDetected` |
| 検出 | `DetectMetadata` | `MacClipboardDetectedMetadataResult` | `MetadataDetected` |
| 検出 | `GetAccessBehavior` | `MacClipboardAccessBehaviorResult` | `AccessBehaviorChecked` |
| 監視 | `StartObserving` / `StopObserving` | `MacClipboardOperationResult` | `ClipboardOperationCompleted` |
| 監視 | （イベント） | `MacClipboardChangeEvent` | `ClipboardChanged` |
| 前面変更 | `CheckForegroundChange` | `MacClipboardForegroundChangeResult` | `ForegroundChangeChecked` |

### 1.2 入力制約（C# が拒否するもの / ネイティブに委ねるもの）

**C# が事前拒否する 2 種類だけをサンプルで区別する。**

| 拒否元 | 条件 | 結果 |
| --- | --- | --- |
| C# 段階 3 | `content` / `ownership` / `request` / `scope` / `patterns` が null | 9005 |
| C# 段階 3 | 送信 payload が 32 MiB 超 | 9007 |
| **C# factory（例外）** | `MacPasteboardScope.Named("")` / `Unique("")` / `MacPasteboardCreationRequest.Named("")` | **`ArgumentException`**（結果ではない） |
| ネイティブ | 空 patterns / 空 utType / 範囲外 interval / 標準 pasteboard 解放 / 空フィルタ | 1503 / 1302 / 1523 / 1508 / 1512 |

### 1.3 エラー契約

- `IsSuccess == false` のとき `Error != null`。`Error.Code` は `int`、`Error.Message` は非 null
- ネイティブコードは 1301 / 1302 / 1501-1599、C# 側は 9001-9007
- **`ReadData` は「型が無い」も「UTI が不正」も成功 + `Data == null`**。失敗ではない
- **`GetAccessBehavior` は macOS 15.4 未満で成功 + `Unavailable`**。失敗ではない

### 1.4 不足前提（実装結果に無く、本計画で決めるもの）

- 画面レイアウト・ボタン粒度・セクション分け
- 固定入力値（fixture）の内容とサイズ
- 結果表示の書式
- **実機確認 32 項目とボタンの対応**（4 章）

---

## 2. 既存サンプルコードの深掘り

### 2.1 確認したもの

| ファイル | 行数 | 役割 |
| --- | ---: | --- |
| `Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs` | 1,517 | **最も近い前例**。57 ボタン |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleObservationState.cs` | 190 | 監視ライフサイクルの純粋状態機械 |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleResult.cs` | 150 | 結果行・ステータス行の純粋フォーマッタ |
| `Runtime/UI/macOS/Share/MacShareManagerExampleController.cs` | 429 | macOS のガード・命名・`Start`/`OnEnable` の形 |
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | 217 | 画面遷移 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | 193 | 入口 |
| `Tests/Runtime/MacShareSampleSceneWiringTests.cs` | — | wiring テストの形 |

### 2.2 再利用する既存コンポーネント

- `NativeToolkitSampleNavigator.ApplyScreen<T>` の画面差し替え機構（**変更せず利用**）
- `TopMenuExampleController` の入口ボタン（**ガードの拡張が必要。2.5**）
- `MacShareManagerExampleController` の `Start` / `InitializeUI` / `OnDestroy` の形
- iOS Clipboard の 3 ファイル構成（Controller + 状態機械 + フォーマッタ）という**分割方針**

### 2.3 追加するコンポーネント

| コンポーネント | 理由 |
| --- | --- |
| `MacClipboardManagerExampleController` | 画面本体 |
| `MacClipboardSampleObservationState` | 監視の状態機械。**iOS と意味論が違う（2.4）ため複製ではなく作り直す** |
| `MacClipboardSampleResult` | 結果行フォーマッタ。EditMode で検証できるよう Controller から分離する |

**`common.md`「共通ファイルを作らない方針」により、iOS の 2 ファイルは共有しない。** `Ios*` には一切触れず、`Mac*` として独立に持つ。

### 2.4 iOS からそのまま写してはならない点（最重要）

**失敗した再 Start の意味論が iOS と macOS で正反対である。**

| | iOS | macOS |
| --- | --- | --- |
| 再 Start が失敗したとき | **旧監視も停止している**（native が scope 解決前に stop する） | **旧監視はそのまま動き続ける**（interval 検証と scope 解決が `stop()` より前。IT-09 が保証） |

`IosClipboardSampleObservationState.CompleteStart(owner, false)` は `IsObserving = false` を無条件に設定している。**これを macOS に写すと、実際には監視が動いているのに画面が「停止中」と表示し、teardown で stop を発行しなくなる。** 監視が残ったまま画面を離れる。

macOS 版の規則:

```
CompleteStart(owner, isSuccess):
    isSuccess  → IsObserving = true
    !isSuccess → IsObserving は変更しない（初回失敗なら false のまま、再 Start 失敗なら true のまま）
```

この 1 点は EditMode の state テストで必ず検証する（6.2）。

### 2.5 変更が必要な既存ファイル（発見事項）

**`TopMenuExampleController` は macOS で Clipboard ボタンを隠している。**

```csharp
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
    _clipboardButton.clicked += OnClipboardClicked;
#else
    // Hide Clipboard button ...
#endif
```

さらに `OnClipboardClicked` に `UNITY_STANDALONE_OSX` の分岐が無く、Editor ダイアログの文言も「Android or iOS」のままである。**このままでは macOS Player で画面に到達できない。** 3 箇所の変更が必須になる。

`NativeToolkitSampleNavigator` にも `ShowMacClipboard` と `RemoveIfExists<MacClipboardManagerExampleController>` が無い。

**これらは `Runtime/UI/Common/` と `Runtime/UI/Top/` の横断ナビゲーション基盤であり、`common.md` の「共通ファイルを作らない方針」の対象外である**（同ファイルは Android / iOS / Windows / macOS の全画面から参照されている）。iOS Clipboard 追加時も同じ 2 ファイルを変更している。

### 2.6 native-toolkit の macOS サンプルとの差分

`/Users/jonghyunkim/Desktop/native-toolkit/mac/MacLibraryExample/MacLibraryExample/ClipboardSampleView.swift`（875 行）を確認した。

**取り入れるもの:**

- セクション分け（Scope / Copy / Append / Read / Detect / Observe / Clear / Errors）は Unity 側でも同じ粒度にする
- `CopyThenAppend` / `AppendWithLastOwnership` / `AppendWithStaleOwnership` の 3 段構成。**ownership の使い回しと失効を別ボタンで見せる**のは 7.5 #2 / #3 にそのまま対応する
- `SnapshotFiltered` / `SnapshotEmptyFilter` の分離
- **到達済みエラーコードの表示**（`reachedCodes`）。32 項目のどれが未消化かを画面上で追える。**本計画での追加判断として採用する**（3.4）

**取り入れないもの:**

| native の機能 | 理由 |
| --- | --- |
| `MakePasteButton` 系 3 ボタン | **C ABI に公開されていない**。Unity から呼べない |
| `ExpectFailureThatSucceeds` / `runExpectingError` の期待コード照合 | サンプルに検証フレームワークを持ち込むことになる。結果行に実コードを出せば手動確認で足りる |

**差分として残るもの:** native サンプルは `ScopeChoice` をピッカーで切り替えるが、Unity 側は既存 iOS サンプルに合わせて**ボタンで active scope を切り替える**方式にする。UIElements にピッカーの前例が無く、ボタンの方が既存パターンに沿う。

---

## 3. 実装方針

### 3.1 共通実装パターン：維持するもの

| パターン | 適用 |
| --- | --- |
| TopMenu → 機能別 ExampleController の導線 | 維持（2.5 の変更で macOS を追加） |
| 先頭にタイトルと結果表示領域 | 維持。`ResultScrollView` + `ResultTextBlock` |
| 機能カテゴリ単位のボタン群 | 維持。9 セクション |
| 成功/失敗が一目で分かる文言 | 維持。`OK` / `NG` / `--`（局所拒否） |
| `OnEnable` / `OnDisable` でイベント購読・解除 | 維持。13 イベントすべて |
| `OnDestroy` でボタン購読解除 | 維持 |
| 公開 API 呼び出し前後のログ | 維持 |

### 3.2 共通実装パターン：拡張するもの

| パターン | 拡張内容 |
| --- | --- |
| 結果表示 | 1 行ではなく**採番付きの履歴**（`#12 [copy.plainText] OK ...`）。異なる操作が並行するため、完了行を呼び出しと対応付ける必要がある |
| ステータス行 | active scope / 監視状態 / イベント数 に加えて**到達済みエラーコード数**を出す（3.4） |

### 3.3 ワークフロー共通パターンからの意図的な逸脱

**`UnityMainThreadDispatcher` を Controller から直接使わない。**

ワークフロー ステップ 5 は「コールバック結果で UI 状態を更新する際は `UnityMainThreadDispatcher` 経由でメインスレッドに反映する」と定める。しかし **`MacClipboardManager` は結果を必ず `UnityMainThreadDispatcher.Enqueue` を通して配送する**（計画書 5.6.6 / 5.6.11）。Controller が受け取る時点で既にメインスレッドであり、二重に経由させる意味が無い。

既存の `IosClipboardManagerExampleController` と `MacShareManagerExampleController` も同じ理由で dispatcher を直接参照していない（実測: 参照 0 件）。**既存パターンに合わせる。**

### 3.4 追加判断：到達済みエラーコードの表示

**実装結果 v3 / v4 に由来しない、本計画での追加である。**

7.5 の 32 項目のうち 8 項目は「特定のエラーコードが返ること」の確認である（1503 / 1508 / 1512 / 1523 / 1302 / 1511 / 1515 / 9007）。実機で 1 項目ずつ潰すとき、どれが未消化かを Console から探すのは非効率になる。

ステータス行に `Codes: 1508,1512,9007 (3/8)` の形で、**このセッションで到達したコードの集合**を出す。native サンプルの `reachedCodes` と同じ発想である。

- 記録するのは**コード番号のみ**。メッセージや payload は記録しない
- `ResetReachedCodes` ボタンで消去できる（複数回の確認をやり直せるように）

---

## 4. 画面要件

### 4.1 操作導線

```
TopMenu → [Clipboard] → MacClipboardManagerExample
                          ├ Status: Scope / Observing / Events / Codes
                          ├ Result: 採番付き履歴（ScrollView）
                          └ 9 セクションのボタン群 → 実行 → 結果行が 1 行追加される
```

### 4.2 ボタン採用基準

**次のいずれかを満たすものだけをボタンにする。両方である必要はない。**

1. **7.5 の確認項目を駆動する**
2. **他のどのボタンでも通らない公開ファクトリを 1 つ以上通る**

基準 2 が要る理由: `MacClipboardContentItem` には公開ファクトリが 5 つある（`PlainText` / `Html` / `Url` / `Data` / `FromRepresentations`）。`CopyPlainText` だけでは残り 4 つが画面から見えず、サンプルが API の見本として機能しない。`write-manual` もこの画面を参照する。

**基準 2 を「便利だから」の言い換えに使わないこと。** 通るファクトリが他のボタンと重複するなら、そのボタンは不要である。

ボタン 1 個あたり同期が必要な箇所は 6 つある（フィールド宣言 / `Q<Button>` / 購読 / `OnDestroy` の解除 / UXML の 1 行 / wiring テストの名前）。42 個で約 250 箇所になるため、基準を満たさないものを足す余地は無い。

#### 採用基準による内訳

| 分類 | 数 | 根拠 |
| --- | ---: | --- |
| 基準 1（7.5 の項目が要求） | 36 | 代替手段が無い。**これが下限** |
| 基準 2 のみ（公開ファクトリの網羅） | 5 | `Html` / `Url` / `Data` / `Content.Multiple` / `PrivacyPreservingDefault` |
| サンプル運用 | 1 | `ResetReachedCodes`（D-6 の到達コード表示をやり直す） |
| **合計** | **42** | |

**v1 初稿にあった `BusyStartObservingTwiceButton` は削除した。** 7.5 の項目に無く、公開ファクトリも増やさず、監視共有キーの 9001 と専用文言は PlayMode テスト `ObservationCalls_ShareOneSingleFlightKey` が文言まで検証済みで、実機で押しても同じ managed コードが走るだけだったため（D-9）。

### 4.3 ボタン一覧（42 個）

| # | セクション | ボタン名 | 呼ぶ API | 7.5 の項目 |
| --- | --- | --- | --- | --- |
| 1 | 共通 | `HomeButton` | — | — |
| 2 | Scope | `UseGeneralButton` | — | 9 |
| 3 | Scope | `UseFixedNamedScopeButton` | — | 9 |
| 4 | Scope | `CreateNamedPasteboardButton` | `CreatePasteboard(Named)` | 9 / 29 |
| 5 | Scope | `CreateUniquePasteboardButton` | `CreatePasteboard(Unique)` | 9 / 29 |
| 6 | Scope | `RemoveActivePasteboardButton` | `RemovePasteboard` | 9 / 29 |
| 7 | Scope | `ProbeRemovedScopeButton` | `Read`（解放済み scope） | 9 |
| 8 | Copy | `CopyPlainTextButton` | `Copy` | 1 / 2 / 13 |
| 9 | Copy | `CopyHtmlButton` | `Copy` | 4 |
| 10 | Copy | `CopyUrlButton` | `Copy` | 4 |
| 11 | Copy | `CopyCustomDataButton` | `Copy` | 5 |
| 12 | Copy | `CopyMultipleItemsButton` | `Copy` | 6 |
| 13 | Copy | `CopyMultiRepresentationButton` | `Copy` | 4 / 6 |
| 14 | Copy | `CopyDetectionFixtureButton` | `Copy` | 11 / 12 |
| 15 | Copy | `CopyUnicodeButton` | `Copy` | **25** |
| 16 | Copy | `CopyLargeSingleItemButton` | `Copy`（12 MiB 単一 item） | **21 / 22** |
| 17 | Options | `CopyLocalOnlyTrueButton` | `Copy(options)` | 26 |
| 18 | Options | `CopyLocalOnlyFalseButton` | `Copy(options)` | **26** |
| 19 | Append | `AppendWithLastOwnershipButton` | `Append` | **2** |
| 20 | Append | `AppendWithStaleOwnershipButton` | `Append` | **3** |
| 21 | Read | `ReadButton` | `Read` | 4 / 8 / 24 / 25 |
| 22 | Read | `ReadDataPlainTextButton` | `ReadData` | 5 |
| 23 | Read | `ReadDataMissingTypeButton` | `ReadData("public.png")` | **5** |
| 24 | Read | `SnapshotButton` | `Snapshot(null)` | 6 |
| 25 | Read | `SnapshotMatchingButton` | `Snapshot(types)` | 6 |
| 26 | Detect | `DetectPatternsButton` | `DetectPatterns` | 11 |
| 27 | Detect | `DetectValuesButton` | `DetectValues` | **12** |
| 28 | Detect | `DetectMetadataButton` | `DetectMetadata` | **13** |
| 29 | Detect | `GetAccessBehaviorButton` | `GetAccessBehavior` | **14** |
| 30 | Observe | `StartObservingButton` | `StartObserving` | 15 / 20 |
| 31 | Observe | `RestartObservingButton` | `StartObserving`（別 onChanged） | **16** |
| 32 | Observe | `StopObservingButton` | `StopObserving` | **18** |
| 33 | Observe | `CheckForegroundChangeButton` | `CheckForegroundChange` | **19 / 20** |
| 34 | Clear | `ClearActiveScopeButton` | `Clear` | **8** |
| 35 | Errors | `ErrRemoveGeneralButton` | `RemovePasteboard(General)` | **10**（1508） |
| 36 | Errors | `ErrSnapshotEmptyFilterButton` | `Snapshot(空配列)` | **7**（1512） |
| 37 | Errors | `ErrDetectEmptyPatternsButton` | `DetectPatterns(空)` | **17b**（1503） |
| 38 | Errors | `ErrReadDataEmptyUtTypeButton` | `ReadData("")` | **17c**（1302） |
| 39 | Errors | `ErrStartObservingInvalidIntervalButton` | `StartObserving(0)` | **17**（1523） |
| 40 | Errors | `ErrCopyOversizeButton` | `Copy`（33 MiB） | **23**（9007） |
| 41 | Errors | `ErrBlankScopeNameButton` | `MacPasteboardScope.Named(" ")` | **17d**（`ArgumentException`） |
| 42 | 共通 | `ResetReachedCodesButton` | — | 3.4 |

**42 個**（うち Home と Reset を除く操作系が 40）。

基準 2 のみで採用したものは #9 `CopyHtmlButton`（`Html`）/ #10 `CopyUrlButton`（`Url`）/ #11 `CopyCustomDataButton`（`Data`）/ #12 `CopyMultipleItemsButton`（`Content.Multiple`）/ #17 `CopyLocalOnlyTrueButton`（`PrivacyPreservingDefault`）の 5 個である。

### 4.4 iOS サンプルとの方針差分

**`ErrBlankScopeNameButton`（#42）は iOS には無い。** iOS サンプルは「C# で例外になる入力はボタンとして公開しない」と明記している（UXML の `SubtitleValidationLabel`）。しかし macOS の 7.5 #17d は **`ArgumentException` が投げられること自体を確認項目としている**（ネイティブが空白名を素通りさせるため、C# factory が唯一の防波堤である）。

したがって macOS 版は**この 1 ボタンだけ例外を捕捉して局所結果（`--`）として表示する**。UXML の注記文もそれに合わせて書き換える。

### 4.5 エラー表示

| 種別 | 表示 | 例 |
| --- | --- | --- |
| 成功 | `OK` + payload 要約 | `#3 [read] OK items=2 changeCount=41` |
| 失敗 | `NG` + code + message | `#4 [err.removeGeneral] NG code=1508 message=...` |
| 局所拒否（例外） | `--` + 例外型名 | `#5 [err.blankScopeName] -- local=ArgumentException` |

**message はネイティブ由来の固定文言のみ**。clipboard 本文・pasteboard 名・検出値は表示しない。

---

## 5. 変更ファイル一覧

### 5.1 新規作成（Runtime）— 3 ファイル

すべて `#if UNITY_STANDALONE_OSX || UNITY_EDITOR` でガードする。

| ファイル | 内容 |
| --- | --- |
| `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs` | 画面本体 |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleObservationState.cs` | 監視の純粋状態機械（2.4 の規則） |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleResult.cs` | 結果行・ステータス行の純粋フォーマッタ |

### 5.2 新規作成（Resources）— 2 ファイル

| ファイル |
| --- |
| `Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExample.uxml` |
| `Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExampleStyle.uss` |

### 5.3 新規作成（Tests）— 2 ファイル

| ファイル | 内容 |
| --- | --- |
| `Tests/Runtime/MacClipboardSampleSceneWiringTests.cs` | UXML / USS の Resources パス、42 ボタン名、Label 名、TopMenu ボタンの存在 |
| `Tests/Runtime/MacClipboardSampleStateTests.cs` | `MacClipboardSampleObservationState` と `MacClipboardSampleResult` の純粋ロジック |

### 5.4 既存変更（Runtime）— 2 ファイル

| ファイル | 変更内容 | 理由 |
| --- | --- | --- |
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowMacClipboard` を追加、`RemoveExistingControllers` の macOS ブロックに `MacClipboardManagerExampleController` を追加 | 画面遷移 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | Clipboard ボタンのガードに `UNITY_STANDALONE_OSX` を追加、`OnClipboardClicked` に macOS 分岐を追加、Editor ダイアログ文言を更新 | **2.5。現状 macOS では画面に到達できない** |

**この 2 ファイルは横断ナビゲーション基盤であり、`common.md`「共通ファイルを作らない方針」の対象外である。** iOS Clipboard 追加時も同じ 2 ファイルを変更している。

### 5.5 非変更（確認済み）

| ファイル | 理由 |
| --- | --- |
| `Runtime/Clipboard/Mac*`（Manager 含む 18 ファイル） | **サンプルのために Manager を変えない**（例外の検討は 9.2） |
| `Runtime/UI/iOS/Clipboard/*` | 複製元だが**一切変更しない**（`common.md`） |
| `Runtime/Resources/UI/Top/TopMenuExample.uxml` | `ClipboardFeatureButton` は既に存在する。変更不要 |
| `Tests/Runtime/*.asmdef` | 変更不要 |

### 5.6 対象外

- マニュアル → `write-manual`
- `package.json` のバージョン更新 → `release`
- サンプルシーン `.unity` の編集 → 既存 `NativeToolkitExampleScene` が `UIDocument` 1 つで全画面を差し替える構造のため**シーンファイルの変更は不要**

---

## 6. 実装詳細

### 6.1 状態

| フィールド | 用途 |
| --- | --- |
| `_activeScope` | 現在の対象 pasteboard。既定は `General` |
| `_lastRemovedScope` | `ProbeRemovedScope` 用 |
| `_lastOwnership` | `AppendWithLastOwnership` 用。`Copy` / `Append` の成功で更新 |
| `_staleOwnership` | `AppendWithStaleOwnership` 用。**最初の `Copy` 時に退避し以降更新しない** |
| `_observedScope` | 監視対象。active と異なりうる |
| `_observation` | `MacClipboardSampleObservationState` |
| `_observedEventCount` | `ClipboardChanged` の受信数 |
| `_reachedCodes` | 到達済みエラーコードの `SortedSet<int>` |
| `_resultSequence` | 結果行の採番 |

### 6.2 API 呼び出しとコールバック購読

- **すべての呼び出しは per-call callback で結果を受ける。** 共通イベントは shape のみのログに使い、画面更新には使わない
  - 理由: Manager は同一操作しか直列化しない。`Read` と `Snapshot` は本当に並行するため、共通イベントで画面を更新すると完了行が取り違わる
  - 例外は `ClipboardChanged`。どの呼び出しにも属さないため、これだけは共通イベントで画面を更新する
- 各呼び出しの直前に `MacClipboardSampleResultContext`（採番 + マーカー）を作り、closure で捕捉する
- `OnEnable` で 13 イベントすべてを購読、`OnDisable` で解除する
- `OnDisable` では**購読解除の前に監視停止を判断する**（iOS と同形）
  - `_observation.RequestStop()` → `ShouldIssueStopNow()` なら `StopObserving` を発行
  - 制御呼び出しが pending の場合は発行できないので、完了側が肩代わりする

### 6.3 監視の状態機械（2.4 の規則を実装）

| 契機 | `IsObserving` | `ControlPending` |
| --- | --- | --- |
| `BeginStart` / `BeginStop` 成功 | 変更しない | true |
| `CompleteStart(owner, true)` | **true** | false |
| `CompleteStart(owner, false)` | **変更しない**（iOS との差分） | false |
| `CompleteStop(owner, true)` | **false** | false |
| `CompleteStop(owner, false)` | **変更しない** | false |
| 非所有トークンでの完了 | 変更しない | 変更しない |

**`NonOwningToken` は専用のデモボタンを落としたあとも必要である。** 制御呼び出しが pending の間に
`StartObserving` / `RestartObserving` / `StopObserving` のいずれかを押せば 2 件目は 9001 で拒否され、
その拒否は 2 件目自身の callback に届く。**そこで状態を変更してしまうと、まだネイティブで走っている
1 件目の登録を取りこぼす。** `BeginStart` / `BeginStop` は `ControlPending` のとき `NonOwningToken` を
返し、`CompleteStart` / `CompleteStop` は所有者トークンが一致しない完了を無視する。

### 6.4 入力バリデーション方針

**サンプル側では検証しない。** 1.2 のとおり、拒否は C# 段階 3 かネイティブが行い、サンプルはその結果をそのまま表示する。

例外は 1 つだけ:

- `ErrBlankScopeNameButton` は `MacPasteboardScope.Named(" ")` を `try` で囲み、`ArgumentException` を捕捉して局所結果として表示する（4.3）

**固定入力値（fixture）**:

| 名前 | 内容 | 用途 |
| --- | --- | --- |
| `PlainTextBody` | `"Hello macOS clipboard"` | 基本 |
| `UnicodeBody` | 日本語 + 絵文字 + サロゲートペア | 7.5 #25 |
| `DetectionFixture` | URL / メール / 電話 / 住所 / 金額 / 便名 / 追跡番号を含む 1 行 | 7.5 #11 / #12 |
| `LargeItemBytes` | **12 MiB**（10 MiB 超 32 MiB 未満） | 7.5 #21 / #22 |
| `OversizeBytes` | **33 MiB**（32 MiB 超） | 7.5 #23 |

- 大きい fixture は**ボタン押下時に生成し、フィールドに保持しない**。常駐させると全操作のメモリ計測（7.5 #24）が濁る
- 生成には固定シードの局所 PRNG を使う。`UnityEngine.Random` はグローバル状態を他サンプルと共有するため使わない

### 6.5 ログ方針

計画書 5.6.11（v12）に従う。

- clipboard 本文・pasteboard 名・検出値・base64 を**表示もログもしない**
- 出してよいのは shape / count / flag と、`utType` / `intervalSeconds`
- `_reachedCodes` は**コード番号のみ**

---

## 7. 手動確認観点

### 7.1 本サンプルで駆動できる項目（31 / 32）

4.2 の対応表のとおり。#1〜#27 と #29 は、記載のボタン操作（一部は他アプリでのコピー / 貼り付けを併用）で駆動できる。

### 7.2 本サンプルでは駆動できない項目（1 件）

**7.5 #28「コールバックのスレッド」は、このサンプルからは観測できない。**

`MacClipboardManager` はネイティブコールバックを受けたあと必ず `UnityMainThreadDispatcher.Enqueue` を通す。Controller が結果を受け取る時点では**定義上メインスレッド**であり、ネイティブ到着時のスレッドは分からない。

対応案は 2 つで、**いずれも Manager 側の変更を伴うため本計画の範囲外とする**（9.2）。

1. `[MonoPInvokeCallback]` 本体で `Thread.CurrentThread.ManagedThreadId == s_mainThreadId` を評価し、flag としてログに出す（`callbackOnMainThread: true`）。ログ許可リストの flag に該当する
2. ネイティブ側（Swift）でスレッドを記録する

**#28 は V-4 の実測項目そのものなので、放置すると V-4 が閉じられない。** 実機確認の前に扱いを決める必要がある。

### 7.3 サンプル自体の確認

| # | 項目 | 期待 |
| --- | --- | --- |
| S-1 | TopMenu → Clipboard | macOS Player で画面が開く（2.5 の修正が効いていること） |
| S-2 | Home で戻る | TopMenu に戻り、Controller が破棄される |
| S-3 | 画面を離れる（監視中） | `StopObserving` が発行され、以降イベントが来ない |
| S-4 | **再 Start 失敗後に画面を離れる** | **監視は継続しているので stop が発行される**（2.4 の差分の実地確認） |
| S-5 | Editor で実行 | 全操作が 9002 で失敗し、その旨が結果行に出る |
| S-6 | 結果行の対応付け | `Read` と `Snapshot` を連続で押しても、完了行が取り違わらない |

---

## 8. Definition of Done

- [ ] 5.1〜5.3 のファイルが作成され、コンパイルエラーが無い
- [ ] 5.4 の 2 ファイルが変更され、**macOS Player の TopMenu から Clipboard 画面に到達できる**
- [ ] `public` メンバに英語の XML コメントが付いている
- [ ] 表示・ログに clipboard 本文 / pasteboard 名 / 検出値 / base64 が出ていない
- [ ] `MacClipboardSampleStateTests` が **2.4 の差分（失敗した再 Start で `IsObserving` を維持する）を検証している**
- [ ] `MacClipboardSampleSceneWiringTests` が 42 ボタンすべての名前を検証している
- [ ] 既存テスト（EditMode 517 / PlayMode 116）が全件 pass する
- [ ] `Ios*` / `Android*` / `Windows*` のファイルを変更していない
- [ ] `Runtime/Clipboard/Mac*` を変更していない（9.2 の決定次第）
- [ ] `.meta` を新規作成していない

---

## 9. 決定事項と要検証

### 9.1 決定事項

| # | 決定 | 理由 |
| --- | --- | --- |
| D-1 | iOS の状態機械を複製せず作り直す | 失敗した再 Start の意味論が正反対（2.4） |
| D-2 | 3 ファイル分割（Controller / 状態機械 / フォーマッタ）を踏襲する | 状態遷移と書式を EditMode で検証できる。iOS の前例あり |
| D-3 | `UnityMainThreadDispatcher` を Controller から直接使わない | Manager が既に経由している（3.3） |
| D-4 | active scope はボタンで切り替える（ピッカーにしない） | 既存 UIElements サンプルに前例が無い（2.6） |
| D-5 | `ErrBlankScopeNameButton` を公開する | 7.5 #17d が `ArgumentException` 自体を確認項目にしている（4.3）。iOS 方針からの意図的な逸脱 |
| D-6 | 到達済みエラーコードを表示する | 32 項目の消化状況を画面で追えるようにする（3.4）。native サンプルに前例あり |
| D-7 | 大きい fixture はボタン押下時に生成する | 常駐させると 7.5 #24 のメモリ計測が濁る |
| D-8 | シーンファイル（`.unity`）は変更しない | 既存構造が `UIDocument` 1 つの差し替え方式（5.6） |
| D-9 | ボタン採用基準を明文化し、`BusyStartObservingTwiceButton` を落とす | 基準が無いとボタン数の根拠を説明できない。落とした 1 個は 7.5 の項目にも公開ファクトリにも該当せず、PlayMode テストが既に文言まで検証していた（4.2） |

### 9.2 要検証（着手前に決めるもの）

| # | 項目 | 内容 |
| --- | --- | --- |
| SV-1 | **7.5 #28 の扱い** | Manager に flag ログを 1 行足すか、ネイティブ側で見るか、V-4 を別手段で閉じるか。**本計画は Manager 非変更を前提にしているため、変更するなら計画書 v13 側の判断になる**（7.2） |
| SV-2 | `LargeItemBytes` = 12 MiB の妥当性 | 10 MiB 超で lazy data provider 経路に入る前提だが、閾値の実挙動は未確認（V-10）。実機で経路に入らなければ値を上げる |
| SV-3 | App Sandbox 有効ビルドの作り方 | 7.5 #29 / V-5。Unity の macOS ビルドで entitlement をどう付けるか未調査 |
| SV-4 | 42 ボタンの画面収まり | 既存 iOS サンプルは 57 ボタンを ScrollView に収めている。macOS のウィンドウサイズでの見え方は実機で確認する |
