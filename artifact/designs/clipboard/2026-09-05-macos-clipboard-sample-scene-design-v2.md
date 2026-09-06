# macOS Clipboard サンプルシーン実装計画書 (v2)

- 対象機能: clipboard
- 対象プラットフォーム: macOS
- 対象パッケージ: `Packages/com.jonghyunkim.nativetoolkit`
- 作成日: 2026-09-05
- 前版: `2026-09-05-macos-clipboard-sample-scene-design-v1.md`
- レビュー: `artifact/reviews/clipboard/2026-09-05-macos-clipboard-sample-scene-design-review-v1.md`（Codex。**差し戻し**。A1 6 件 / A2 0 件 / B 1 件 / C 3 件）
- 前提とする実装結果: `artifact/results/clipboard/2026-09-03-macos-clipboard-implementation-feature-result-v3.md`（段 2 / 5 操作）、`-v4.md`（段 3 / 10 操作）
- 参照する実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v12.md`（以降 `v12` と表記し、その節は `v12 §5.6.11` の形で参照する）
- 出力範囲: ExampleController・UXML/USS・ナビゲーション導線・EditMode の wiring / state テスト

---

## v1 からの主な変更

Codex レビュー（review-v1）の反映。**A1 6 件はすべて「実装してもコンパイルもテストも通ってしまう」種類**で、v1 のまま着手していたら実機確認の段階まで表面化しなかった。

| 指摘 | 分類 | 反映内容 |
| --- | --- | --- |
| A1-1 | A1 | **「31/32 を駆動できる」は成立していなかった。** #4 / #14 / #16 / #25 は API を呼べても期待結果を観測できない。判定値を 4 種類追加した（`§6.1` / `§6.6`） |
| A1-2 | A1 | #5 の「不正な非空 UTI」と #17 の `61` / 負値 / `NaN` を駆動する入口が無かった。ボタンを 1 個追加し、interval は 1 ボタンで 4 値を逐次実行する形にした |
| A1-3 | A1 | #16 の callback 置換を、登録別カウンタで識別できるようにした |
| A1-4 | A1 | **失敗した再 Start の完了後に deferred stop が発行されない。** iOS の「成功時のみ」条件を写すと、`§7.3` の S-4 で旧監視が残る。**成否ではなく完了後の `IsObserving` で判断する**規則に変えた（`§6.3`） |
| A1-5 | A1 | **ネイティブの `errorMessage` は固定文言ではなく pasteboard 名などを含む。** そのまま表示すると `§6.5` に反する。**raw message を一切表示しない**方式に変えた（`§4.6`） |
| A1-6 | A1 | 12 MiB fixture の型とバイト内容が未指定だった。paste 可能な公開型に固定した（`§6.4`） |
| B-1 | B | #24 の計測手順・記録欄を追加した（`§7.4`） |
| C-1 | C | 「iOS は失敗した再 Start で常に旧監視も停止」は一般化が広すぎた。**P/Invoke 例外の経路では iOS も旧監視が残る**（`§2.4`） |
| C-2 | C | ボタン表に**採用基準の列**を追加し、内訳を表から算出できるようにした。番号のずれも解消（`§4.3`） |
| C-3 | C | `check_design_consistency.py` の 2 チェックが FAIL していた。外部節参照をコードスパンにし、擬似コードを散文へ移した |

**あわせて未確定事項 SV-1〜SV-4 を 4 件とも決着させた**（`§9.2`）。

---

## 0. この計画の位置づけ

**このサンプルシーンは実機確認 32 項目（`v12 §7.5`）の実行環境である。** 見た目のデモではなく、32 項目を漏れなく駆動できることが第一の要件になる。

**「駆動できる」とは「API を呼べる」ではなく「期待結果を観測できる」ことである。** v1 はこの区別を曖昧にしたまま 31/32 を主張し、A1-1 として差し戻された。本版はボタン採用基準 3（`§4.2`）でこれを明文化する。

macOS Clipboard の Manager 実装は完了済み（EditMode 517 / PlayMode 116、失敗 0）。ただし **P/Invoke 境界は一度も実行されていない**。Editor では P/Invoke がコンパイルされないため、テストはすべてその手前で止まっている。本サンプルをビルドして動かすことが、ブリッジの初回実行になる。

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

### 1.2 入力制約

| 拒否元 | 条件 | 結果 |
| --- | --- | --- |
| C# 段階 3 | `content` / `ownership` / `request` / `scope` / `patterns` が null | 9005 |
| C# 段階 3 | 送信 payload が 32 MiB 超 | 9007 |
| **C# factory（例外）** | `MacPasteboardScope.Named("")` / `Unique("")` / `MacPasteboardCreationRequest.Named("")` | **`ArgumentException`**（結果ではない） |
| ネイティブ | 空 patterns / 空 utType / 範囲外 interval / 標準 pasteboard 解放 / 空フィルタ | 1503 / 1302 / 1523 / 1508 / 1512 |

### 1.3 エラー契約

- `IsSuccess == false` のとき `Error != null`。`Error.Code` は `int`、`Error.Message` は非 null
- **`Error.Message` はネイティブが組み立てた文字列で、固定文言とは限らない**（`§4.6`）
- `ReadData` は「型が無い」も「UTI が不正」も成功 + `Data == null`
- `GetAccessBehavior` は macOS 15.4 未満で成功 + `Unavailable`

### 1.4 不足前提（実装結果に無く、本計画で決めるもの）

画面レイアウト、ボタン粒度、固定入力値、結果表示の書式、**32 項目とボタンの対応**（`§4.3`）、**期待結果の判定方法**（`§6.6`）。

---

## 2. 既存サンプルコードの深掘り

### 2.1 確認したもの

| ファイル | 行数 | 役割 |
| --- | ---: | --- |
| `Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs` | 1,517 | 最も近い前例。57 ボタン |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleObservationState.cs` | 190 | 監視ライフサイクルの純粋状態機械 |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleResult.cs` | 150 | 結果行・ステータス行の純粋フォーマッタ |
| `Runtime/UI/macOS/Share/MacShareManagerExampleController.cs` | 429 | macOS のガード・命名・ライフサイクルの形 |
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | 217 | 画面遷移 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | 193 | 入口 |
| `Tests/Runtime/MacShareSampleSceneWiringTests.cs` | — | wiring テストの形 |

### 2.2 再利用する既存コンポーネント

- `NativeToolkitSampleNavigator.ApplyScreen<T>` の画面差し替え機構（変更せず利用）
- `TopMenuExampleController` の入口ボタン（ガードの拡張が必要。`§2.5`）
- `MacShareManagerExampleController` のライフサイクルの形
- iOS Clipboard の 3 ファイル構成という**分割方針**

### 2.3 追加するコンポーネント

| コンポーネント | 理由 |
| --- | --- |
| `MacClipboardManagerExampleController` | 画面本体 |
| `MacClipboardSampleObservationState` | 監視の状態機械。**iOS と意味論が違うため複製ではなく作り直す** |
| `MacClipboardSampleResult` | 結果行フォーマッタ。**エラーの redaction を含む**（`§4.6`）ため EditMode で検証できるよう分離する |

`common.md` の「共通ファイルを作らない方針」により、iOS の 2 ファイルは共有しない。

### 2.4 iOS からそのまま写してはならない点（最重要）

**失敗した再 Start の意味論が iOS と macOS で異なる。ただし失敗の段階によって話が変わる**（C-1 の修正）。

| 失敗の段階 | iOS | macOS |
| --- | --- | --- |
| **ネイティブに到達し、scope 解決 / interval 検証で失敗** | 旧監視は**停止済み**（`stopObservingInternal()` が scope 解決より先に走る） | 旧監視は**動き続ける**（interval 検証と `readChangeCount` が `stop()` より前。IT-09 が保証） |
| **P/Invoke 例外でネイティブに到達しなかった** | ネイティブの旧監視は**残る**（managed 登録だけ消える） | ネイティブの旧監視は**残る**（同左） |

`IosClipboardSampleObservationState.CompleteStart(owner, false)` は `IsObserving = false` を無条件に設定している。**これは iOS でも P/Invoke 例外の経路では誤りである**（ネイティブ poller が残るのにサンプルは「停止中」と判断する）。macOS ではさらに通常の失敗経路でも誤りになる。

macOS 版の規則:

```
CompleteStart(owner, isSuccess):
    isSuccess  → IsObserving = true
    !isSuccess → IsObserving は変更しない
```

**iOS 実装を macOS 設計の正当性の根拠にしない。** 根拠は `v12 §1.6` と native の `ClipboardChangeMonitor.start()`、および IT-09 である。

この規則は EditMode の state テストで必ず検証する（`§8`）。

### 2.5 変更が必要な既存ファイル

**`TopMenuExampleController` は macOS で Clipboard ボタンを隠している。** ボタンの購読ガードが Android と iOS と Editor だけを対象にしており、macOS Player では else 側に入って `_clipboardButton` の表示を切っている。`OnClipboardClicked` にも macOS の分岐が無く、Editor 用ダイアログの文言も Android と iOS しか挙げていない。**このままでは macOS Player で画面に到達できない。**

`NativeToolkitSampleNavigator` にも `ShowMacClipboard` と、macOS Clipboard Controller の除去処理が無い。

**これらは `Runtime/UI/Common/` と `Runtime/UI/Top/` の横断ナビゲーション基盤であり、`common.md` の「共通ファイルを作らない方針」の対象外である。** 同方針が禁じるのはプラットフォーム機能ロジックを接頭辞なしで共有することで、今回の変更は macOS 専用ガード付きの入口追加にすぎない。iOS Clipboard 追加時も同じ 2 ファイルを変更している。

### 2.6 native-toolkit の macOS サンプルとの差分

`ClipboardSampleView.swift`（875 行）を確認した。

**取り入れるもの:** セクション分けの粒度、`CopyThenAppend` / `AppendWithLastOwnership` / `AppendWithStaleOwnership` の 3 段構成、`SnapshotFiltered` / `SnapshotEmptyFilter` の分離、**到達済みエラーコードの表示**（`§3.4`）。

**取り入れないもの:**

| native の機能 | 理由 |
| --- | --- |
| `MakePasteButton` 系 3 ボタン | **C ABI に公開されていない**。Unity から呼べない |
| 期待コード照合の仕組み | サンプルに検証フレームワークを持ち込むことになる |

**差分として残るもの:** native は scope をピッカーで切り替えるが、Unity 側はボタンで切り替える（UIElements にピッカーの前例が無い）。

---

## 3. 実装方針

### 3.1 共通実装パターン：維持するもの

TopMenu からの導線、先頭のタイトルと結果表示領域、機能カテゴリ単位のボタン群、成功/失敗が一目で分かる文言、`OnEnable` / `OnDisable` でのイベント購読・解除、`OnDestroy` でのボタン購読解除、公開 API 呼び出し前後のログ。

### 3.2 共通実装パターン：拡張するもの

| パターン | 拡張内容 |
| --- | --- |
| 結果表示 | 1 行ではなく**採番付きの履歴**。異なる操作が並行するため、完了行を呼び出しと対応付ける必要がある |
| ステータス行 | active scope / 監視状態 / イベント数 / **到達済みエラーコード数**（`§3.4`） |
| 結果の中身 | **期待結果を一意に判定できる値**を含める（`§6.6`）。A1-1 の反映 |

### 3.3 ワークフロー共通パターンからの意図的な逸脱

**`UnityMainThreadDispatcher` を Controller から直接使わない。**

ワークフロー ステップ 5 は dispatcher 経由での UI 反映を求めるが、`MacClipboardManager` は結果を必ず `UnityMainThreadDispatcher.Enqueue` を通して配送する（`v12 §5.6.6`、`v12 §5.6.11`）。通常結果・ガード拒否・off-thread 拒否・変更イベントの 4 経路すべてが該当する。Controller が受け取る時点で既にメインスレッドであり、二重に経由させる意味が無い。

既存の `IosClipboardManagerExampleController` と `MacShareManagerExampleController` も dispatcher を直接参照していない（実測: 参照 0 件）。

### 3.4 追加判断：到達済みエラーコードの表示

**実装結果に由来しない、本計画での追加である。**

32 項目のうち 8 項目は「特定のエラーコードが返ること」の確認である（1503 / 1508 / 1512 / 1523 / 1302 / 1511 / 1515 / 9007）。実機で 1 項目ずつ潰すとき、どれが未消化かを Console から探すのは非効率になる。

ステータス行に `Codes: 1508,1512,9007 (3/8)` の形で、このセッションで到達したコードの集合を出す。記録するのは**コード番号のみ**。`ResetReachedCodesButton` で消去できる。

---

## 4. 画面要件

### 4.1 操作導線

TopMenu の Clipboard ボタンから `MacClipboardManagerExample` に入る。画面は上から順に、スクロールしない固定ヘッダー（ステータス行と結果履歴の `ScrollView`）と、縦スクロールする操作領域（9 セクションのボタン群）で構成する。ボタンを押すと結果履歴に 1 行追加される。Home ボタンで TopMenu へ戻る。

ステータス行には active scope、監視状態、受信イベント数、到達済みエラーコードを出す。

### 4.2 ボタン採用基準

**次の 3 つを**すべて**満たすものだけをボタンにする。**

1. **7.5 の確認項目を駆動する**、または**他のどのボタンでも通らない公開ファクトリを 1 つ以上通る**
2. **押した結果が、他のボタンの結果と区別できる**
3. **対応する確認項目の期待結果を、機微情報を露出せずに一意に判定できる**（A1-1 の反映）

**基準 3 が要る理由:** v1 は基準 1 だけで 42 個を選び、「31/32 を駆動できる」と主張した。しかし #4（派生型）/ #14（accessBehavior）/ #16（callback 置換）/ #25（Unicode 往復）は、**API を呼べても期待結果を観測できなかった**。wiring テストはボタン名の存在しか見ないため、実装もテストも通ってしまう。基準 3 は「呼べる」と「確かめられる」の差を埋める。

**基準 1 の後半を「便利だから」の言い換えに使わないこと。** 通るファクトリが他のボタンと重複するなら、そのボタンは不要である。

ボタン 1 個あたり同期が必要な箇所は 6 つある（フィールド宣言 / `Q<Button>` / 購読 / `OnDestroy` の解除 / UXML の 1 行 / wiring テストの名前）。43 個で約 260 箇所になる。

**「ファクトリ 1 つにつきボタン 1 つ」は下限ではない。** 1 つの fixture 生成ボタンが複数のファクトリを通ることもありうる。基準 2 が満たせる範囲で束ねてよい。

### 4.3 ボタン一覧（43 個）

`基準` 列: **1**=確認項目、**F**=公開ファクトリ、**S**=サンプル運用。内訳はこの列から算出する。

| # | セクション | ボタン名 | 呼ぶ API | 7.5 の項目 | 基準 |
| --- | --- | --- | --- | --- | --- |
| 1 | 共通 | `HomeButton` | — | — | S |
| 2 | Scope | `UseGeneralButton` | — | 9 | 1 |
| 3 | Scope | `UseFixedNamedScopeButton` | — | 9 | 1 |
| 4 | Scope | `CreateNamedPasteboardButton` | `CreatePasteboard(Named)` | 9 / 29 | 1 |
| 5 | Scope | `CreateUniquePasteboardButton` | `CreatePasteboard(Unique)` | 9 / 29 | 1 |
| 6 | Scope | `RemoveActivePasteboardButton` | `RemovePasteboard` | 9 / 29 | 1 |
| 7 | Scope | `ProbeRemovedScopeButton` | `Read`（解放済み scope） | 9 | 1 |
| 8 | Copy | `CopyPlainTextButton` | `Copy` | 1 / 2 / 13 | 1 |
| 9 | Copy | `CopyHtmlButton` | `Copy` | — | F |
| 10 | Copy | `CopyUrlButton` | `Copy` | — | F |
| 11 | Copy | `CopyCustomDataButton` | `Copy` | — | F |
| 12 | Copy | `CopyMultipleItemsButton` | `Copy` | — | F |
| 13 | Copy | `CopyMultiRepresentationButton` | `Copy` | 6 | 1 |
| 14 | Copy | `CopyDetectionFixtureButton` | `Copy` | 11 / 12 | 1 |
| 15 | Copy | `CopyUnicodeButton` | `Copy` | 25 | 1 |
| 16 | Copy | `CopyLargeSingleItemButton` | `Copy`（12 MiB 単一 item） | 21 / 22 | 1 |
| 17 | Options | `CopyLocalOnlyTrueButton` | `Copy(options)` | — | F |
| 18 | Options | `CopyLocalOnlyFalseButton` | `Copy(options)` | 26 | 1 |
| 19 | Append | `AppendWithLastOwnershipButton` | `Append` | 2 | 1 |
| 20 | Append | `AppendWithStaleOwnershipButton` | `Append` | 3 | 1 |
| 21 | Read | `ReadButton` | `Read` | 4 / 8 / 24 / 25 | 1 |
| 22 | Read | `ReadDataPlainTextButton` | `ReadData("public.utf8-plain-text")` | 5 | 1 |
| 23 | Read | `ReadDataMissingTypeButton` | `ReadData("public.png")` | 5 | 1 |
| 24 | Read | `ReadDataInvalidTypeButton` | `ReadData("abc")` | **5** | 1 |
| 25 | Read | `SnapshotButton` | `Snapshot(null)` | 6 | 1 |
| 26 | Read | `SnapshotMatchingButton` | `Snapshot(types)` | 6 | 1 |
| 27 | Detect | `DetectPatternsButton` | `DetectPatterns` | 11 | 1 |
| 28 | Detect | `DetectValuesButton` | `DetectValues` | 12 | 1 |
| 29 | Detect | `DetectMetadataButton` | `DetectMetadata` | 13 | 1 |
| 30 | Detect | `GetAccessBehaviorButton` | `GetAccessBehavior` | 14 | 1 |
| 31 | Observe | `StartObservingButton` | `StartObserving` | 15 / 20 | 1 |
| 32 | Observe | `RestartObservingButton` | `StartObserving`（別 `onChanged`） | 16 | 1 |
| 33 | Observe | `StopObservingButton` | `StopObserving` | 18 | 1 |
| 34 | Observe | `CheckForegroundChangeButton` | `CheckForegroundChange` | 19 / 20 | 1 |
| 35 | Clear | `ClearActiveScopeButton` | `Clear` | 8 | 1 |
| 36 | Errors | `ErrRemoveGeneralButton` | `RemovePasteboard(General)` | 10（1508） | 1 |
| 37 | Errors | `ErrSnapshotEmptyFilterButton` | `Snapshot(空配列)` | 7（1512） | 1 |
| 38 | Errors | `ErrDetectEmptyPatternsButton` | `DetectPatterns(空)` | 17b（1503） | 1 |
| 39 | Errors | `ErrReadDataEmptyUtTypeButton` | `ReadData("")` | 17c（1302） | 1 |
| 40 | Errors | `ErrObservingIntervalMatrixButton` | `StartObserving` を **0 / 61 / -1 / NaN** で逐次 | **17**（1523 ×4） | 1 |
| 41 | Errors | `ErrCopyOversizeButton` | `Copy`（33 MiB） | 23（9007） | 1 |
| 42 | Errors | `ErrBlankScopeNameButton` | `MacPasteboardScope.Named(" ")` | 17d（`ArgumentException`） | 1 |
| 43 | 共通 | `ResetReachedCodesButton` | — | — | S |

**内訳: 基準 1 が 36 / F が 5 / S が 2、合計 43。**

v1 からの差分は 2 点。`ReadDataInvalidTypeButton`（#24）を追加し、`ErrStartObservingInvalidIntervalButton` を **4 値を逐次実行する** `ErrObservingIntervalMatrixButton`（#40）に置き換えた。いずれも A1-2 の反映である。

**#40 は 1 ボタンで 4 回呼ぶが、結果行は入力ごとに 1 行ずつ出す。** `interval` は `v12 §5.6.11` でログ許可されているので、どの値の結果かを行に書ける。共有 single-flight キーがあるため 4 回は**逐次**（前の完了を待って次を発行）で実行する。

### 4.4 iOS サンプルとの方針差分

**`ErrBlankScopeNameButton`（#42）は iOS には無い。** iOS サンプルは「C# で例外になる入力はボタンとして公開しない」と明記している。しかし macOS の 7.5 #17d は **`ArgumentException` が投げられること自体を確認項目としている**（ネイティブが空白名を素通りさせるため、C# factory が唯一の防波堤である）。

したがって macOS 版はこの 1 ボタンだけ例外を捕捉して局所結果として表示する。UXML の注記文もそれに合わせる。

### 4.5 結果表示の書式

| 種別 | 表示 | 例 |
| --- | --- | --- |
| 実行中 | `...` | `#3 [read] ...` |
| 成功 | `OK` + 判定値 | `#3 [read] OK items=2 changeCount=41 readTypes=3 derived=true` |
| 失敗 | `NG` + code + **正規化した説明** | `#4 [err.removeGeneral] NG code=1508 reason=standardPasteboard` |
| 局所拒否（例外） | `--` + 例外型名 | `#5 [err.blankScopeName] -- local=ArgumentException` |

### 4.6 エラー表示の正規化（A1-5 の反映）

**ネイティブの `Error.Message` を画面にもログにも出さない。**

v1 は「message はネイティブ由来の固定文言のみ」と前提していたが、**これは誤りだった**。native の `ClipboardError.errorMessage` は動的値を埋め込む。

| code | 埋め込まれる値 |
| --- | --- |
| 1504 | 不正な UTI そのもの |
| **1505 / 1507 / 1508** | **pasteboard 名** |
| 1506 | バイト数と上限 |
| 1511 | expected / actual の changeCount |
| 1515 | OS 由来の検出失敗理由 |
| 1599 | 任意の reason |

とくに `ProbeRemovedScopeButton` は解放済み scope を `Read` するボタンで、**1507 を引き当てるために存在する**。その message をそのまま出すと pasteboard 名が画面に出る。

**方式: `MacClipboardSampleResult` が code から固定の `reason` トークンへ変換する。**

```
1302 → contractViolation      1503 → emptyPatterns
1507 → pasteboardUnavailable  1508 → standardPasteboard
1511 → ownershipLost          1512 → emptyTypeFilter
1513 → detectionUnavailable   1515 → detectionFailed
1523 → invalidConfiguration   9001 → busy
9002 → bridgeUnavailable      9005 → invalidRequest
9006 → responseParseFailed    9007 → requestTooLarge
未知 → NG code=<n> reason=<unmapped>
```

**「静的な message だけ通す」方式を採らない理由:** どの case が静的かはネイティブ側の実装詳細で、ネイティブが後から動的値を足したときに Unity 側が気づけない。**code から自前の文言を引く方式なら、ネイティブが何を書こうと漏れない。**

`MacClipboardSampleResult` を Controller から分離するのは、この変換を EditMode で検証するためである。

**なお `MacClipboardManager` 本体は raw message をログに出していない**（実測: `Debug.Log` 系 21 箇所すべてが managed 例外か自前の固定文言）。漏洩経路はサンプルの表示層だけである。

---

## 5. 変更ファイル一覧

### 5.1 新規作成（Runtime）— 3 ファイル

すべて `#if UNITY_STANDALONE_OSX || UNITY_EDITOR` でガードする。

| ファイル | 内容 |
| --- | --- |
| `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs` | 画面本体 |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleObservationState.cs` | 監視の純粋状態機械（`§6.3`） |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleResult.cs` | 結果行・ステータス行のフォーマッタと**エラー正規化**（`§4.6`） |

### 5.2 新規作成（Resources）— 2 ファイル

`Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExample.uxml` と `MacClipboardManagerExampleStyle.uss`。

### 5.3 新規作成（Tests）— 2 ファイル

| ファイル | 内容 |
| --- | --- |
| `Tests/Runtime/MacClipboardSampleSceneWiringTests.cs` | UXML / USS の Resources パス、43 ボタン名、Label 名、TopMenu ボタンの存在 |
| `Tests/Runtime/MacClipboardSampleStateTests.cs` | 状態機械の遷移（**失敗した再 Start と deferred stop を含む**）と**エラー正規化** |

### 5.4 既存変更（Runtime）— 2 ファイル

| ファイル | 変更内容 | 理由 |
| --- | --- | --- |
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowMacClipboard` を追加、`RemoveExistingControllers` の macOS ブロックに追加 | 画面遷移 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | Clipboard ボタンのガードに macOS を追加、`OnClipboardClicked` に macOS 分岐を追加、Editor ダイアログ文言を更新 | `§2.5`。**現状 macOS では画面に到達できない** |

### 5.5 非変更（確認済み）

| ファイル | 理由 |
| --- | --- |
| `Runtime/Clipboard/Mac*`（Manager 含む 18 ファイル） | **サンプルのために Manager を変えない。** ただし SV-1 の決定により `v13` 側で別途 1 箇所変更する（`§9.2`） |
| `Runtime/UI/iOS/Clipboard/*` | 複製元だが一切変更しない |
| `Runtime/Resources/UI/Top/TopMenuExample.uxml` | `ClipboardFeatureButton` は既に存在する |
| `Tests/Runtime/*.asmdef` | 変更不要 |

### 5.6 対象外

マニュアル（`write-manual`）、`package.json` のバージョン更新（`release`）、サンプルシーン `.unity` の編集（既存が `UIDocument` 1 つの差し替え方式のため不要）。

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
| `_registrationCounts` | **登録マーカーごとの受信数**（A1-3。`§6.6`） |
| `_observedEventCount` | `ClipboardChanged` の総受信数 |
| `_lastWrittenTypeCount` | 直近の `Copy` で書いた representation 型の数（`§6.6` の #4） |
| `_lastWrittenPayloadHash` | 直近の `Copy` で書いたバイト列のハッシュ（`§6.6` の #25） |
| `_reachedCodes` | 到達済みエラーコードの `SortedSet<int>` |
| `_resultSequence` | 結果行の採番 |

### 6.2 API 呼び出しとコールバック購読

- **すべての呼び出しは per-call callback で結果を受ける。** 共通イベントは shape のみのログに使い、画面更新には使わない
  - 理由: Manager は同一操作しか直列化しない。`Read` と `Snapshot` は本当に並行するため、共通イベントで画面を更新すると完了行が取り違わる
  - **例外は `ClipboardChanged`。** どの呼び出しにも属さないため共通イベントで受ける。ただし**総数の更新だけ**を行い、登録別の識別は各 `onChanged` closure が行う（`§6.6` の #16）
- 各呼び出しの直前に `MacClipboardSampleResultContext`（採番 + マーカー）を作り、closure で捕捉する
- `OnEnable` で 13 イベントすべてを購読、`OnDisable` で解除する
- `OnDisable` では**購読解除の前に監視停止を判断する**（`§6.3`）

### 6.3 監視の状態機械（A1-4 の反映）

| 契機 | `IsObserving` | `ControlPending` |
| --- | --- | --- |
| `BeginStart` / `BeginStop` 成功 | 変更しない | true |
| `CompleteStart(owner, true)` | **true** | false |
| `CompleteStart(owner, false)` | **変更しない** | false |
| `CompleteStop(owner, true)` | **false** | false |
| `CompleteStop(owner, false)` | **変更しない** | false |
| 非所有トークンでの完了 | 変更しない | 変更しない |

**`NonOwningToken` は専用のデモボタンを置かなくても必要である。** 制御呼び出しが pending の間に `StartObserving` / `RestartObserving` / `StopObserving` のいずれかを押せば 2 件目は 9001 で拒否され、その拒否は 2 件目自身の callback に届く。そこで状態を変更すると、**まだネイティブで走っている 1 件目の登録を取りこぼす。** `BeginStart` / `BeginStop` は `ControlPending` のとき `NonOwningToken` を返し、完了側は所有者トークンが一致しない完了を無視する。

**deferred stop の規則（A1-4）:**

画面を離れるとき、制御呼び出しが pending なら stop を発行できない。その場合 `StopRequestedAfterControl` を立て、**完了側が肩代わりする**。

```
制御呼び出しの完了後:
    StopRequestedAfterControl && IsObserving && !ControlPending
        → StopObserving を発行し、StopRequestedAfterControl を下ろす
```

**判断材料は「完了の成否」ではなく「完了後も `IsObserving` か」である。** iOS Controller は `result.IsSuccess && StopRequestedAfterStart` を条件にしているが、macOS でこれを写すと次が漏れる。

- 旧監視あり → Restart 開始 → pending のまま画面離脱 → **Restart が失敗**
- macOS では旧監視がまだ動いている（`§2.4`）
- iOS の条件では `IsSuccess == false` なので **Stop が発行されない**
- 監視が残ったまま画面が消える

これは `§7.3` の S-4 そのものである。**成否ではなく `IsObserving` で判断すれば、成功・失敗のどちらでも正しく発行される。**

### 6.4 入力バリデーション方針と fixture

**サンプル側では検証しない。** 拒否は C# 段階 3 かネイティブが行い、サンプルはその結果を表示する。例外は `ErrBlankScopeNameButton` だけで、`ArgumentException` を捕捉して局所結果として表示する。

| fixture | 内容 | 用途 |
| --- | --- | --- |
| `PlainTextBody` | `"Hello macOS clipboard"` | 基本 |
| `UnicodeBody` | 日本語 + 絵文字 + サロゲートペア | 7.5 #25 |
| `DetectionFixture` | URL / メール / 電話 / 住所 / 金額 / 便名 / 追跡番号を含む 1 行 | 7.5 #11 / #12 |
| `LargeItemBytes` | **12 MiB** | 7.5 #21 / #22 |
| `OversizeBytes` | **33 MiB** | 7.5 #23 |

**`LargeItemBytes` の形を固定する（A1-6）:**

- **単一 item・単一 representation**（両方が lazy data provider 経路の条件。`v12 §1.6`）
- 型は **`public.utf8-plain-text`**。custom UTI にすると、lazy provider が正常でも受け側アプリが貼り付けられず、**Player 生存中と終了後の差が判別できなくなる**
- 内容は **ASCII 1 文字の反復で 12 MiB ちょうど**。有効な UTF-8 であることを保証し、生成コストを一定にする
- 12 MiB が閾値を超えることは確認済み: native validator は「単一 item かつ `totalBytes > warnBytesPerRepresentation`」で lazy を選び、既定の閾値は 10 MiB である（`§9.2` SV-2）

大きい fixture は**ボタン押下時に生成し、フィールドに保持しない**。常駐させると 7.5 #24 のメモリ計測が濁る。

### 6.5 表示・ログ方針

`v12 §5.6.11` に従う。**サンプル側で追加する制約:**

- clipboard 本文・pasteboard 名・検出値・base64 を**表示もログもしない**
- **ネイティブの `Error.Message` を出さない**（`§4.6`）
- **`Read` / `Snapshot` が返した型名を出さない。** 他アプリのカスタム UTI を含みうる。**数だけ**出す
- 出してよいもの: shape / count / flag、`utType`（呼び出し側が指定したもの）、`intervalSeconds`、**`MacClipboardAccessBehavior` の enum 値**（`§6.6` の #14）
- `_reachedCodes` はコード番号のみ

### 6.6 期待結果の判定値（A1-1 の反映）

**4 項目は「呼べる」だけでは確認できない。** 機微情報を出さずに判定できる値を結果行に含める。

| 7.5 | 判定値 | 定義 | なぜ安全か |
| --- | --- | --- | --- |
| **#4** 派生型 | `writtenTypes` / `readTypes` / `derived` | 直近の `Copy` で書いた型数と、`Read` が返した型数。`derived = readTypes > writtenTypes` | **数のみ**。型名は出さない |
| **#14** accessBehavior | `behavior=<enum 名>` | `MacClipboardAccessBehavior` の 5 値のいずれか | OS が返す助言的な列挙で、clipboard 内容ではない。**5 値は固定**で利用者データを含みえない |
| **#16** callback 置換 | `obs#1=0 obs#2=1` | 登録マーカーごとの受信数（`_registrationCounts`） | 数のみ |
| **#25** Unicode 往復 | `roundTrip=match \| differ \| n/a` | `Copy` 時のバイト列ハッシュと、`Read` が返した同型のバイト列ハッシュの一致 | **ハッシュ比較の結果だけ**を出す。本文もハッシュ値も出さない |

- `#4` と `#25` は `ReadButton` が判定する。直前の `Copy` が本アプリからでない場合（他アプリがコピーした場合）は `n/a` を出す
- `#16` は `StartObserving` と `RestartObserving` がそれぞれ別マーカーの `onChanged` を渡し、closure がマーカー別カウンタを進める

---

## 7. 手動確認観点

### 7.1 駆動できる項目（31 / 32）

`§4.3` の対応表と `§6.6` の判定値による。#1〜#27 と #29 は、記載のボタン操作（一部は他アプリでのコピー / 貼り付けを併用）で駆動でき、**期待結果を画面上で判定できる**。

### 7.2 駆動できない項目（1 件）

**7.5 #28「コールバックのスレッド」は、このサンプルからは観測できない。**

`MacClipboardManager` はネイティブコールバックを受けたあと必ず `UnityMainThreadDispatcher.Enqueue` を通す。Controller が受け取る時点では**定義上メインスレッド**であり、`[MonoPInvokeCallback]` 入口に到着した瞬間のスレッドは復元できない。

**SV-1 として決着させた（`§9.2`）。** Manager 側に計測を追加するため、`v13` の変更として扱う。本計画の範囲外である。

### 7.3 サンプル自体の確認

| # | 項目 | 期待 |
| --- | --- | --- |
| S-1 | TopMenu → Clipboard | macOS Player で画面が開く（`§2.5` の修正が効いていること） |
| S-2 | Home で戻る | TopMenu に戻り、Controller が破棄される |
| S-3 | 画面を離れる（監視中） | `StopObserving` が発行され、以降イベントが来ない |
| S-4 | **再 Start 失敗後に画面を離れる** | **監視は継続しているので stop が発行される**（`§6.3` の deferred stop 規則） |
| S-5 | Editor で実行 | 全操作が 9002 で失敗し、その旨が結果行に出る |
| S-6 | 結果行の対応付け | `Read` と `Snapshot` を連続で押しても、完了行が取り違わらない |
| S-7 | **エラー表示の正規化** | 1507 / 1508 を引く操作で、**pasteboard 名が画面にも Console にも出ていない** |

### 7.4 #24 の計測手順（B-1 の反映）

「他アプリが 50 MiB 超をコピーした状態で `Read`」は、手順と記録先が無いと実施できない。

1. **fixture の作成**: `mkfile 60m ~/Desktop/large.bin` などで 60 MiB のファイルを作り、Finder でコピーする（Finder のコピーは file URL を載せるため、payload としては小さい）。payload 自体を大きくするには、**テキストエディタで 60 MiB のテキストを開いて全選択・コピー**する方が確実
2. **計測点**: Unity の Profiler（Memory モジュール）で `Read` 実行前後の Total Reserved / Mono Used を記録する。Player 上では Activity Monitor の Real Memory も併記する
3. **所要時間**: 結果行の採番時刻と完了時刻の差を計測し、`elapsedMs` として結果行に出す（本計画で追加する。数値のみなので表示可）
4. **記録先**: 実装結果ファイル（`-v5`）の手動確認表に、`payloadBytes` / `peakMemoryMB` / `elapsedMs` の列を設ける

**#22 の「所要時間とフレーム落ち」も同じ `elapsedMs` で記録する。**

---

## 8. Definition of Done

- [ ] `§5.1`〜`§5.3` のファイルが作成され、コンパイルエラーが無い
- [ ] `§5.4` の 2 ファイルが変更され、**macOS Player の TopMenu から Clipboard 画面に到達できる**
- [ ] `public` メンバに英語の XML コメントが付いている
- [ ] **表示・ログにネイティブの `Error.Message` が出ていない**（`§4.6`）
- [ ] 表示・ログに clipboard 本文 / pasteboard 名 / 検出値 / base64 / `Read` の返した型名が出ていない
- [ ] `MacClipboardSampleStateTests` が次を検証している
  - [ ] **失敗した再 Start で `IsObserving` を維持する**（`§2.4`）
  - [ ] **失敗した再 Start の完了後に deferred stop が発行される**（`§6.3`。S-4 の回帰）
  - [ ] 非所有トークンの完了が状態を変えない
  - [ ] **エラー正規化が 1507 / 1508 で pasteboard 名を出さない**（`§4.6`）
  - [ ] 未知 code が `unmapped` に落ちる
- [ ] `MacClipboardSampleSceneWiringTests` が 43 ボタンすべての名前を検証している
- [ ] 既存テスト（EditMode 517 / PlayMode 116）が全件 pass する
- [ ] `Ios*` / `Android*` / `Windows*` のファイルを変更していない
- [ ] `Runtime/Clipboard/Mac*` を変更していない
- [ ] `.meta` を新規作成していない
- [ ] **`scripts/check_design_consistency.py` が本計画書に対して FAIL 0 である**

---

## 9. 決定事項と要検証

### 9.1 決定事項

| # | 決定 | 理由 |
| --- | --- | --- |
| D-1 | iOS の状態機械を複製せず作り直す | 失敗した再 Start の意味論が異なる（`§2.4`） |
| D-2 | 3 ファイル分割（Controller / 状態機械 / フォーマッタ） | 状態遷移とエラー正規化を EditMode で検証できる |
| D-3 | `UnityMainThreadDispatcher` を Controller から直接使わない | Manager が既に経由している（`§3.3`） |
| D-4 | active scope はボタンで切り替える | 既存 UIElements サンプルに前例が無い |
| D-5 | `ErrBlankScopeNameButton` を公開する | 7.5 #17d が `ArgumentException` 自体を確認項目にしている |
| D-6 | 到達済みエラーコードを表示する | 32 項目の消化状況を画面で追える（`§3.4`） |
| D-7 | 大きい fixture はボタン押下時に生成する | 常駐させると 7.5 #24 のメモリ計測が濁る |
| D-8 | シーンファイル（`.unity`）は変更しない | 既存構造が `UIDocument` 1 つの差し替え方式 |
| D-9 | ボタン採用基準に「期待結果を判定できること」を加える | 基準 1 だけでは 4 項目が観測不能だった（A1-1） |
| D-10 | **ネイティブの `Error.Message` を出さず、code から自前の文言を引く** | どの case が静的かはネイティブの実装詳細であり、後から動的値が足されても Unity 側が気づけない（`§4.6`） |
| D-11 | **deferred stop は成否ではなく `IsObserving` で判断する** | 失敗した再 Start でも macOS では監視が残る（`§6.3`） |
| D-12 | 12 MiB fixture を `public.utf8-plain-text` の単一 representation に固定する | custom UTI では受け側が貼り付けられず、lazy provider の成否を判別できない（`§6.4`） |
| D-13 | interval の異常値 4 種を 1 ボタンで逐次実行する | 共有 single-flight キーがあるため並行実行できない。ボタンを 4 つに割るより結果の対応付けが明確（`§4.3` #40） |
| D-14 | `MacClipboardAccessBehavior` の enum 値を表示可とする | 5 値固定の助言的列挙で、利用者データを含みえない（`§6.6`） |
| D-15 | `elapsedMs` を結果行に出す | 7.5 #22 / #24 の所要時間記録に必要。数値のみ（`§7.4`） |

### 9.2 未確定事項の決着

**v1 の SV-1〜SV-4 は 4 件とも決着した。**

| # | 決定 | 根拠 |
| --- | --- | --- |
| **SV-1** | **案 (a) を採用。`v13` で Manager に計測を追加する** | 下記 |
| **SV-2** | **12 MiB を維持。fixture の形を `§6.4` で固定** | native validator は「単一 item かつ `totalBytes > warnBytesPerRepresentation`」で lazy を選び、既定閾値は 10 MiB。12 MiB は厳密に超える。**閾値の確認は完了**し、残るのは V-10（実機で本当に貼り付け不可になるか）だけ |
| **SV-3** | **既存の `PostBuildProcessor` 経由で Sandbox 検証用ビルドを作る** | 下記 |
| **SV-4** | **削減せず、固定ヘッダー + 単一縦 ScrollView で実装し実測する** | iOS の前例が 57 ボタンを ScrollView に収めている。43 個は致命的ではない。実機確認条件は下記 |

**SV-1 の内容（`v13` 側の変更）:**

- `[MonoPInvokeCallback]` 入口で `Thread.CurrentThread.ManagedThreadId == s_mainThreadId` を評価し、bool / count として記録する
- **入口から `Debug.Log` を直接呼んではならない。** 不一致だった場合、off-thread から Unity API を呼ぶことになる。記録だけを行い、**`s_dispatcher.Enqueue` した closure の中でログに出す**
- 対象は少なくとも 3 つの ABI 形（値あり callback / void callback / 変更 callback）。理想は 15 入口すべて
- 出力は flag（`callbackOnMainThread: true`）なので `v12 §5.6.11` のログ許可リストに収まる
- **これは Manager の境界診断であり、サンプル Controller の責務ではない。** `v13` の判断として扱う

**SV-3 の手順:**

1. Sandbox 専用の `.entitlements` に `com.apple.security.app-sandbox` を追加する
2. 生成された Xcode target の `CODE_SIGN_ENTITLEMENTS` に設定する（既存 `Editor/Build/PostBuildProcessor.cs` が `Mac.xcodeproj` を編集しているので、その経路を使う）
3. Development signing でビルドする
4. `codesign -dvvv --entitlements - <App.app>` で埋め込み値を確認する
5. Activity Monitor の Sandbox 列が `Yes` であることを確認する
6. named / unique の作成・copy / read・削除を実行し、通常ビルドと結果を比較する

`--deep` は埋め込みコードにも同じ entitlements を適用するため、恒久運用ではなく検証用に限定する。**pasteboard 専用の追加 entitlement が存在するとは一次資料から確認できなかった。** まず app-sandbox だけで実測し、失敗した場合に sandbox ログと署名内容を保存する。

**SV-4 の実機確認条件:**

最小ウィンドウサイズを決める。結果・Status・Home はスクロール外に固定する。操作部だけ縦スクロールする。すべてのボタンがキーボード操作で到達できる。長いエラー行が操作領域を押し出さない。**セクションの折り畳みは任意だが、確認項目を削らない。**

### 9.3 残る要検証

| # | 項目 | 内容 |
| --- | --- | --- |
| SV-5 | `v13` の要否 | SV-1 を実施するなら `v12` → `v13` の改訂と Manager の変更が要る。**サンプル実装と並行して進めるか、先に片付けるか** |
