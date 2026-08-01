# Android Clipboard サンプルシーン実装計画 v2

## 基本情報

- 日付: 2026-07-26
- 機能名: clipboard
- 対象プラットフォーム: Android
- 出力言語: 日本語（計画書の記述言語のみ。実装コード内の文言は英語）
- 実装結果: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v3.md`（review-implementation-feature で LGTM 判定済み）
- 機能実装計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md`
- 位置づけ: 実装済み `AndroidClipboardManager` を Unity サンプルシーンから操作できるようにする
- 後続工程: review-document → implement-sample-scene

### v1 からの変更点（レビュー反映）

対象レビュー: `artifact/reviews/clipboard/2026-07-26-android-clipboard-sample-scene-design-review-v1.md`（高優先度の指摘なし）

| 指摘 | severity | 対応 |
|---|---|---|
| 実装結果由来の境界値2件（`plainText` のみ空の HTML 成功ケース / 複数テキストの空要素成功ケース）を実行する導線が無い | medium | 3.1 に `CopyHtmlEmptyPlainTextButton` を新規追加（操作ボタン 16→17）。`CopyMultipleTextButton` の固定値を `{ "First", "", "Third" }` と明記して空要素込みに変更。あわせて 6.3 に両ボタンの固定値、7.2 に対応する実機確認行2件を追加し、実装結果 v3 の未実施項目と 1:1 で追跡できるようにした |
| ボタン総数の表現が `HomeButton` を含むかどうかで揺れている | low | 「操作ボタン 17 個 + `HomeButton` 1 個 = 合計 18 個」と全箇所で統一。wiring test の必須ボタン配列には `HomeButton` を**含める**方針に確定（既存 `AndroidShareSampleSceneWiringTests` も `HomeButton` を含んでいるため、それに揃える） |

---

## 1. 前提情報（実装結果由来）

### 1.1 実装済みの公開 API（`AndroidClipboardManager`）

**同期メソッド（戻り値あり・event を発火しない）:**

| API | シグネチャ | 戻り値 |
|---|---|---|
| `Read` | `ClipboardReadResult Read()` | `Status`（`HasContent` / `Empty` / `Failed`）+ `Contents` / `ErrorCode` / `ErrorMessage` |
| `HasClip` | `bool HasClip()` | `true` / `false`（失敗時も `false`、内容なしと区別不能） |
| `GetDescription` | `ClipboardDescriptionResult GetDescription()` | `Status` + `Description` / `ErrorCode` / `ErrorMessage` |

**非同期メソッド（共通 event + 任意の per-call callback）:**

| API | シグネチャ |
|---|---|
| `CopyPlainText` | `void CopyPlainText(CopyPlainTextPayload, Action<ClipboardOperationResult>? = null)` |
| `CopyHtmlText` | `void CopyHtmlText(CopyHtmlTextPayload, Action<ClipboardOperationResult>? = null)` |
| `CopyUri` | `void CopyUri(CopyUriPayload, Action<ClipboardOperationResult>? = null)` |
| `CopyMultipleText` | `void CopyMultipleText(CopyMultipleTextPayload, Action<ClipboardOperationResult>? = null)` |
| `Clear` | `void Clear(Action<ClipboardOperationResult>? = null)` |
| `StopObserving` | `void StopObserving(Action<ClipboardOperationResult>? = null)` |

**結果を返さないメソッド:**

| API | 備考 |
|---|---|
| `StartObserving` | `void StartObserving()`。**callback 引数なし・event も発火しない**。失敗時もログのみ。監視開始の成否は「実際にクリップボードを変更して `ClipboardChanged` が届くか」でしか確認できない |

**イベント:**

| event | シグネチャ |
|---|---|
| `ClipboardOperationCompleted` | `event Action<ClipboardOperationResult>?`（copy / clear / stopObserving で発火。per-call callback より先） |
| `ClipboardChanged` | `event Action?`（**引数なし**。変化後の内容は別途 `Read()` で取得する） |

**公開 operation 名定数:** `OperationCopyPlainText` / `OperationCopyHtmlText` / `OperationCopyUri` / `OperationCopyMultipleText` / `OperationClear` / `OperationStopObserving`

### 1.2 入力制約（Payload 由来）

| Payload | 必須フィールド | blank の扱い |
|---|---|---|
| `CopyPlainTextPayload` | `text` | **blank でも成功**（エラーにしない） |
| `CopyHtmlTextPayload` | `htmlText` | blank は `CLIPBOARD_EMPTY_CONTENT` で失敗。`plainText` は blank 可 |
| `CopyUriPayload` | `uri` | blank / パース不可は `CLIPBOARD_INVALID_URI` |
| `CopyMultipleTextPayload` | `texts` | 空配列は `CLIPBOARD_EMPTY_ITEMS`。要素の空文字は許容 |

共通オプション: `label`（null / 空白なら省略され native が `""` にフォールバック）、`isSensitive`（Android 13+ のプレビュー抑制ヒント）

### 1.3 エラー契約（実装結果由来）

- 非同期系: `ClipboardOperationResult` の `IsSuccess` / `ErrorMessage`。`IsSuccess == true` のとき `ErrorMessage == null` を保証
- 同期系: `Status == Failed` のとき `ErrorCode`（下表）+ `ErrorMessage` が非 null

| ErrorCode | errorMessage |
|---|---|
| `CLIPBOARD_EMPTY_CONTENT` | `Clipboard content is empty. Please provide text or HTML.` |
| `CLIPBOARD_EMPTY_ITEMS` | `No items provided for clipboard copy.` |
| `CLIPBOARD_INVALID_URI` | `Invalid URI: {uri}` |
| `CLIPBOARD_UNAVAILABLE` | `Clipboard service is unavailable.` |
| `CLIPBOARD_READ_NOT_ALLOWED` | `Clipboard read is not allowed. The app must be in the foreground.` |
| `CLIPBOARD_SECURITY` | `Security restriction while accessing clipboard: {message}` |
| `CLIPBOARD_UNKNOWN` | `Failed: {message}` |
| `CLIPBOARD_BRIDGE_UNAVAILABLE` | C# Bridge 層固有（`read could not be started.` 等） |

**注意:** 非同期（listener）経路には **ErrorCode が渡らない**。コードが取得できるのは `Read` / `GetDescription` の同期経路のみ。

### 1.4 手動確認観点（実装結果由来、全18項目）

実装結果 v3 の 5.2 で未実施とされた項目。本サンプルシーンはこれらを実機で確認するための操作面を提供する。

プレーンテキストコピー / blank、HTML コピー / blank / plainText のみ空、URI コピー、複数テキスト / 空要素、機微フラグ、読み取り、空読み取り、メタデータ、有無判定、監視、監視停止（0 引数 JNI 解決の確認を含む）、監視の二重開始、バックグラウンド読み取り、ログ安全性、ライフサイクル

### 1.5 不足前提（勝手に要件追加しない）

- **`content://` URI の生成手段が Manager に無い**。`CopyUri` は URI 文字列を受け取るだけで、URI を作る API は clipboard 機能のスコープ外。サンプル側で用意する必要がある（2.3 / 6.4 で方針を確定）
- **入力欄の前例が無い**。既存 Unity サンプル 13 本の UXML に `TextField` は 1 つも存在しない。本計画でも入力欄は追加せず、固定サンプル値のボタン方式を維持する
- **iOS / macOS / Windows の clipboard C# 実装は未着手**。TopMenu の Clipboard 導線は Android のみ有効にする

---

## 2. 既存サンプルコード深掘り結果

### 2.1 Unity 既存サンプル構成（参照済み）

- サンプルシーンは 1 つだけ: `Assets/Samples/Native Toolkit/1.0.0/Native Toolkit Example/NativeToolkitExampleScene.unity`。単一 `UIDocument` を持つ GameObject に、`NativeToolkitSampleNavigator` が画面ごとの Controller を `AddComponent` / `Destroy` して差し替える方式
- UXML / USS は `Runtime/Resources/UI/<Platform>/<Feature>/` に配置し、`Resources.Load` でロードする（`Assets/` 側ではない）
- `TopMenuExampleController` が Dialog / Notification / Share の 3 ボタンを持ち、`#if UNITY_EDITOR` では `EditorUtility.DisplayDialog` で「実機で動く」旨を出すだけ、実機では `NativeToolkitSampleNavigator.ShowAndroidXxx()` を呼ぶ
- `AndroidShareManagerExampleController` が本機能に最も近い前例（Android + operation 結果 + per-call callback）:
  - `Start` で `InitializeUI()` → `root.Q<Button>(name)` で取得し `clicked +=`
  - `OnEnable` / `OnDisable` で Manager イベントを購読・解除（`#if UNITY_ANDROID && !UNITY_EDITOR` ガード付き）
  - `OnDestroy` で全ボタンの `clicked -=`
  - `_pendingOperationTitle` で「どの操作を実行したか」を保持し、event 受信時に人間可読なタイトルとして表示
  - 各操作は `#if UNITY_ANDROID && !UNITY_EDITOR` / `#else` で分岐し、非対応環境では `SetResult("Android device only. ...")`
  - 結果表示は `Label name="ResultTextBlock"` 1 か所を `SetResult(string)` で更新
- UXML 構造: `header`（HomeButton + タイトル + サブタイトル + 結果表示枠）+ `ScrollView`（`android-section` 単位でボタン群）
- USS クラス: `android-share-root` / `-header` / `-scroll` / `-content` / `-title` / `-subtitle` / `android-result-border` / `android-result-text` / `android-section` / `android-section-title` / `android-share-button`（青 `#1E88E5`）/ `android-secondary-button`（グレー `#607D8B`）
- `AndroidShareSampleSceneWiringTests.cs` が UXML と Controller の名前不一致を EditMode で検出する前例を作っている（Resources パス存在確認 + 必須ボタン/ラベル名の存在確認 + TopMenu ボタン存在確認）

### 2.2 native-toolkit サンプルアプリ構成（参照済み）

`android/AndroidLibraryExample/app/src/main/java/.../example/ClipboardSampleScreen.kt`（Jetpack Compose）:

- 画面構成: Back ボタン → タイトル `Clipboard Example` → `statusText` 表示 → `LazyColumn` にセクション別ボタン
- セクション構成（**Unity 側もこの区分を踏襲する**）:

| セクション | ボタン |
|---|---|
| Copy | Copy Plain Text / Copy Plain Text (empty, allowed) / Copy HTML Text / Copy URI (content:// via FileProvider) / Copy Multiple Text |
| Copy - Sensitive | 注意書き + Copy Sensitive Text |
| Read / Inspect | Read Clipboard / Has Clip / Get Description |
| Clear | Clear Clipboard |
| Observe | 注意書き + Start Observing / Stop Observing |
| Error Cases | Copy HTML (empty) → EmptyContent / Copy Multiple (empty list) → EmptyItemList / Copy URI (blank) → InvalidUri / Copy URI (http scheme) → InvalidUri |

- 結果表示の作法:
  - 成功は `✅ copyPlainText called`、情報は `ℹ️ Clipboard is empty (normal)`、失敗は `❌ EmptyContent: ...`
  - **Unity 側は絵文字を使わない**（出力ルール）ため、`Success:` / `Info:` / `Failed:` のテキスト接頭辞に置き換える
- `Read` の結果は `formatReadResult()` で `label=... , mimeTypes=[...], items=[{text=..., htmlText=..., uri=..., coercedText=...}]` の形に整形して表示。`GetDescription` も同様に `formatDescription()`
- 監視は変化回数カウンタ `changeCount` を持ち `Clipboard changed (N)` と表示。`DisposableEffect` の `onDispose` で `monitor.stop()`
- `content://` URI は `FileProvider.getUriForFile(context, "${packageName}.native_toolkit.share.fileprovider", file)` で cacheDir 上のファイルから生成
- Android 13 未満では `isSensitive` が効かないため、アプリ側で Toast を出して代替している旨を注意書きで明記

**Unity サンプルに反映しない差分:**

- Toast による代替表示は行わない（Unity 側に Toast API が無く、結果ラベルで代替する）
- Compose の `LazyColumn` は `ScrollView` に置き換える（既存 Unity サンプルの作法）

### 2.3 差分方針（再利用 / 追加 / 変更）

**再利用する既存コンポーネント:**

- `NativeToolkitSampleNavigator`（画面差し替え機構）
- `UIDocument` + `Resources.Load` 方式、`ScrollView` + `android-section` の UXML 構造
- `AndroidShareManagerExampleStyle.uss` のクラス設計（clipboard 用に同等クラスを新規 USS へ複製する。既存 USS は共有せず機能ごとに独立させる既存方針を維持）
- `AndroidShareManagerExampleController` の Start / OnEnable / OnDisable / OnDestroy / SetResult / `_pendingOperationTitle` パターン

**追加するコンポーネント:**

- `AndroidClipboardManagerExampleController.cs`（新規）
- `AndroidClipboardManagerExample.uxml` / `AndroidClipboardManagerExampleStyle.uss`（新規）
- `AndroidClipboardSampleSceneWiringTests.cs`（新規、既存 Share の wiring テストに倣う）

**変更するファイルと変更理由:**

| ファイル | 変更理由 |
|---|---|
| `TopMenuExample.uxml` | `ClipboardFeatureButton` を追加（Clipboard 画面への導線） |
| `TopMenuExampleController.cs` | Clipboard ボタンの取得・購読・解除、Android のみ有効化（他プラットフォームは非表示）、`OnClipboardClicked` の追加 |
| `NativeToolkitSampleNavigator.cs` | `ShowAndroidClipboard()` の追加と `RemoveExistingControllers` への登録 |

---

## 3. 画面要件

### 3.1 機能一覧（操作ボタン 17 個 + `HomeButton` 1 個 = 合計 18 個）

native サンプル（2.2）の区分をそのまま踏襲する。

**ボタン数の数え方（v2 で統一）:** 以下の表は `HomeButton` を含めた全 18 行。うち `HomeButton` を除く 17 個が「操作ボタン」。本計画で「操作ボタン 17 個」と書く場合は `HomeButton` を含まず、「全 18 ボタン」と書く場合は含む。wiring test（7.1）の必須ボタン配列には **`HomeButton` を含める**（既存 `AndroidShareSampleSceneWiringTests` の `RequiredShareButtonNames` が `HomeButton` を含んでいるため、それに揃える）。

| セクション | ボタン name | 表示テキスト | 呼び出す API |
|---|---|---|---|
| （ヘッダ） | `HomeButton` | Back To Home | `NativeToolkitSampleNavigator.ShowTopMenu` |
| Copy | `CopyPlainTextButton` | Copy Plain Text | `CopyPlainText` |
| Copy | `CopyEmptyPlainTextButton` | Copy Plain Text (empty, allowed) | `CopyPlainText`（`text = ""`） |
| Copy | `CopyHtmlTextButton` | Copy HTML Text | `CopyHtmlText` |
| Copy | `CopyHtmlEmptyPlainTextButton` | Copy HTML Text (empty plain text, allowed) | `CopyHtmlText`（`plainText = ""`、`htmlText` は非空） |
| Copy | `CopyUriButton` | Copy URI (content:// via FileProvider) | `CopyUri` |
| Copy | `CopyMultipleTextButton` | Copy Multiple Text (includes an empty item) | `CopyMultipleText`（`{ "First", "", "Third" }`） |
| Copy - Sensitive | `CopySensitiveTextButton` | Copy Sensitive Text | `CopyPlainText`（`isSensitive = true`） |
| Read / Inspect | `ReadClipboardButton` | Read Clipboard | `Read`（同期） |
| Read / Inspect | `HasClipButton` | Has Clip | `HasClip`（同期） |
| Read / Inspect | `GetDescriptionButton` | Get Description | `GetDescription`（同期） |
| Clear | `ClearClipboardButton` | Clear Clipboard | `Clear` |
| Observe | `StartObservingButton` | Start Observing | `StartObserving` |
| Observe | `StopObservingButton` | Stop Observing | `StopObserving` |
| Error Cases | `CopyEmptyHtmlButton` | Copy HTML (empty) -> EmptyContent | `CopyHtmlText`（`htmlText = ""`） |
| Error Cases | `CopyEmptyItemsButton` | Copy Multiple (empty list) -> EmptyItemList | `CopyMultipleText`（空配列） |
| Error Cases | `CopyBlankUriButton` | Copy URI (blank) -> InvalidUri | `CopyUri`（`uri = ""`） |
| Error Cases | `CopyHttpUriButton` | Copy URI (http scheme) -> InvalidUri | `CopyUri`（`uri = "http://example.com/x"`） |

結果表示ラベル: `ResultTextBlock`（既存 Share と同名）

### 3.2 操作導線（入力 → 実行 → 結果表示）

入力欄は設けない（1.5 の不足前提のとおり、既存 Unity サンプルに `TextField` の前例が無い）。各ボタンが固定のサンプル値を送る。

**非同期系（copy / clear / stopObserving）:**

1. ボタン押下 → `SetPendingOperation("<操作名>")` で `Requested: <操作名>` を表示
2. `AndroidClipboardManager.Instance.Xxx(payload)` を呼ぶ
3. `ClipboardOperationCompleted` event 受信 → `[event] <操作名>: Success` / `Failed` + 失敗時は `Error: <errorMessage>` を表示

**同期系（Read / HasClip / GetDescription）:**

1. ボタン押下
2. 戻り値をその場で整形して表示（event を待たない）
   - `Read`: `HasContent` → `Success: Read: label=..., mimeTypes=[...], items=[...]` / `Empty` → `Info: Clipboard is empty (normal)` / `Failed` → `Failed: <ErrorCode>: <ErrorMessage>`
   - `HasClip`: `Success: hasClip = true` / `false`
   - `GetDescription`: `Read` と同じ 3 分岐

**監視:**

1. `Start Observing` 押下 → `StartObserving()` を呼ぶ → `Info: startObserving requested. Change the clipboard to verify.` を表示
   - **成功表示にしない**。native は結果を返さず、実際に監視が始まったかは C# から判定できない（1.1）
2. クリップボード変化 → `ClipboardChanged` event → `_changeCount` をインクリメントし `[event] Clipboard changed (N)` を表示
3. `Stop Observing` 押下 → `StopObserving()` → `ClipboardOperationCompleted` で `[event] Stop observing: Success`

### 3.3 エラー表示

- 非同期系: `[event] <操作名>: Failed` の次行に `Error: <result.ErrorMessage>`（native の文言をそのまま表示、C# 側で書き換えない）
- 同期系: `Failed: <ErrorCode>: <ErrorMessage>`（**同期経路のみ ErrorCode が取れる**ため、コードも併記して 1.3 の表と対応付けられるようにする）
- 非 Android 環境（Editor 含む）: `Android device only. Run this sample on Android to <操作>.`（既存 Share と同一文型）
- URI 生成失敗など、Manager 呼び出し前に失敗した場合: `File preparation failed: <message>`（既存 Share と同一文型）

---

## 4. 変更ファイル一覧

`.meta` は Unity が自動生成するため記載しない。

### 4.1 新規作成

| パス | 内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Android/Clipboard/AndroidClipboardManagerExampleController.cs` | Controller 本体 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Clipboard/AndroidClipboardManagerExample.uxml` | 画面レイアウト |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Clipboard/AndroidClipboardManagerExampleStyle.uss` | スタイル |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardSampleSceneWiringTests.cs` | UXML / Controller 名前一致の EditMode 静的チェック |

### 4.2 既存変更

| パス | 変更内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml` | `ClipboardFeatureButton` を追加（Share ボタンの下） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs` | `_clipboardButton` フィールド、`InitializeUI` での取得・購読、`OnDestroy` での解除、`OnClipboardClicked` を追加。Android 以外では非表示 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowAndroidClipboard()` を追加し、`RemoveExistingControllers` に `AndroidClipboardManagerExampleController` を登録 |

### 4.3 非変更（対象だが変更しない）

| パス | 理由 |
|---|---|
| `Runtime/Clipboard/*.cs` | 機能実装は完了済み。サンプル追加のための変更は不要 |
| `Assets/Samples/.../NativeToolkitExampleScene.unity` | Navigator が Controller を動的に差し替えるため、シーン自体の変更は不要 |
| `Runtime/Resources/UI/Android/Share/*` | Share 用資産は流用せず独立させる（既存の機能別独立方針を維持） |
| `Plugins/Android/*.aar` | FileProvider は同梱済み（6.4 参照） |

---

## 5. 実装方針

### 5.1 共通実装パターンの維持 / 拡張

**維持するもの:**

- TopMenu → 機能別 ExampleController の導線（`NativeToolkitSampleNavigator` 経由）
- ヘッダに Home ボタン・タイトル・サブタイトル・結果表示領域を置く構造
- 機能カテゴリ単位のボタン群（`android-section` + `android-section-title`）
- `SetResult(string)` 1 か所で結果を更新
- Manager イベント購読は `OnEnable` / `OnDisable`、ボタン購読解除は `OnDestroy`
- 公開 API 呼び出し前後に `Debug.Log` を残す
- `#if UNITY_ANDROID && !UNITY_EDITOR` / `#else` による実機・非実機の分岐

**拡張するもの（clipboard 固有）:**

1. **同期 API の結果表示経路を新設する**。既存サンプルは全て「操作 → event で結果表示」だったが、`Read` / `HasClip` / `GetDescription` は戻り値をその場で表示する。`_pendingOperationTitle` を経由させない
2. **`ClipboardChanged`（引数なし event）の購読と変化回数カウンタ `_changeCount` を追加**。既存サンプルに引数なし event の前例は無い
3. **「成功と断定しない」結果表示を導入**。`StartObserving` は成否不明のため `Info:` 接頭辞で要求のみを表示する
4. **結果表示に 3 種の接頭辞を導入**（`Success:` / `Info:` / `Failed:`）。native サンプルの絵文字表現を、絵文字を使わない形に置き換えたもの

### 5.2 スレッド / 購読 / ライフサイクル方針

- Manager 側で `ClipboardOperationCompleted` / `ClipboardChanged` ともに `UnityMainThreadDispatcher` 経由でメインスレッドに転送済みのため、**Controller 側で追加の Dispatcher 呼び出しは不要**（既存 Share Controller と同じ前提）
- `OnEnable`: `ClipboardOperationCompleted += ...` / `ClipboardChanged += ...`
- `OnDisable`: 上記を `-=` で解除し、あわせて **`StopObserving()` を呼んで監視を止める**
  - 理由: 画面を離れた後もクリップボード変化を拾い続けるのを防ぐ。native サンプルも `onDispose` で `monitor.stop()` している
  - 既存 Share Controller が `OnDisable` で `CancelPendingShareCallback()` を呼ぶのと同じ考え方
- `OnDestroy`: 全ボタンの `clicked -=`
- `_changeCount` は `OnEnable` ではリセットせず、画面表示中の累計とする（実装時に迷わないよう明記）

### 5.3 コーディング制約（適用済み）

- `agent-rules/coding-rules/common.md`: Manager + Bridge パターン、`UnityMainThreadDispatcher` 経由の dispatch、`#if UNITY_ANDROID` ガード
- `agent-rules/coding-rules/csharp.md`: `public` / `private` を問わず主要メソッド先頭に `Debug.Log`、`private const string LogTag = "AndroidClipboardManagerExampleController";`
- **ログ秘匿の逸脱を Controller にも適用する**（機能実装の 5.7.2 と同じ扱い）:
  - クリップボード本文・読み取り結果本文をログに出さない
  - `Read` / `GetDescription` の結果は `Status` と `ErrorCode` のみログに出す（結果ラベルへの表示は行うが、ログには出さない）
  - この逸脱理由を英語の行コメントで残す
- コメント・ログ・UI 文言はすべて英語。絵文字を使わない

---

## 6. 実装詳細（implement-sample-scene ステップ3 で行う内容）

### 6.1 UXML（`AndroidClipboardManagerExample.uxml`）

既存 `AndroidShareManagerExample.uxml` と同構造。ルートクラス名のみ `android-clipboard-*` に変える。

```
android-clipboard-root
├── android-clipboard-header
│   ├── Button name="HomeButton" class="android-secondary-button" text="Back To Home"
│   ├── Label class="android-clipboard-title" text="AndroidClipboardManager Example"
│   ├── Label class="android-clipboard-subtitle"
│   │     text="Copy, read, and observe the clipboard. Sensitive-content
│   │           suppression requires Android 13 (API 33) or later."
│   └── VisualElement class="android-result-border"
│       └── Label name="ResultTextBlock" class="android-result-text"
│             text="Android clipboard sample ready. Run on an Android device to test."
└── ScrollView class="android-clipboard-scroll"
    └── VisualElement class="android-clipboard-content"
        ├── section "Copy"            : 6 ボタン（3.1 の表のとおり）
        ├── section "Copy - Sensitive": 注意 Label + 1 ボタン
        ├── section "Read / Inspect"  : 3 ボタン
        ├── section "Clear"           : 1 ボタン
        ├── section "Observe"         : 注意 Label + 2 ボタン
        └── section "Error Cases"     : 4 ボタン
```

- 注意 Label は `android-section-note` クラスを新設（`android-clipboard-subtitle` より小さく左寄せ）
  - Sensitive: `Preview suppression only takes effect on Android 13 (API 33) or later.`
  - Observe: `Observation is only reliable while this app is in the foreground (Android 10+ restriction). Starting observation reports no result; change the clipboard to verify it works.`
- ボタンのクラス割り当て:
  - 主要操作（Copy 系 4: PlainText / HtmlText / Uri / MultipleText、Read 系 3、Sensitive 1、Start Observing 1 = 計 9）: `android-clipboard-button`（青）
  - 副次・破壊・エラー系（Home / Clear / Stop Observing / Error Cases 4 / allowed 系 2: `CopyEmptyPlainTextButton`・`CopyHtmlEmptyPlainTextButton` = 計 9）: `android-secondary-button`（グレー）

### 6.2 USS（`AndroidClipboardManagerExampleStyle.uss`）

`AndroidShareManagerExampleStyle.uss` を複製し、以下のみ変更する。

- `.android-share-*` → `.android-clipboard-*`（root / header / scroll / content / title / subtitle）
- `.android-share-button` → `.android-clipboard-button`（配色は同じ `#1E88E5`）
- `.android-result-border` / `.android-result-text` / `.android-section` / `.android-section-title` / `.android-secondary-button` は**同名のまま維持**（機能間で名前が揃っている方が読みやすいため）
- `.android-section-note` を追加: `font-size: 11px; color: #757575; white-space: normal;`

### 6.3 Controller（`AndroidClipboardManagerExampleController.cs`）

```csharp
#if UNITY_ANDROID || UNITY_EDITOR
public class AndroidClipboardManagerExampleController : MonoBehaviour
{
    private const string LogTag = "AndroidClipboardManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private Label? _resultLabel;
    private string _pendingOperationTitle = string.Empty;
    private int _changeCount;

    // 18 個の Button フィールド（操作ボタン 17 + HomeButton 1、3.1 の name に対応）
}
#endif
```

**ライフサイクル（既存 Share Controller と同型）:**

| メソッド | 処理 |
|---|---|
| `Start` | `uiDocument` 解決 → `InitializeUI()` |
| `InitializeUI` | `root.Q<Label>("ResultTextBlock")` / 各 `root.Q<Button>(name)` → `clicked +=` |
| `OnEnable` | `#if UNITY_ANDROID && !UNITY_EDITOR` で `ClipboardOperationCompleted += OnClipboardOperationCompleted;` `ClipboardChanged += OnClipboardChanged;` |
| `OnDisable` | 同ガード内で両 event を `-=`、続けて `AndroidClipboardManager.Instance.StopObserving();` |
| `OnDestroy` | 全ボタン `clicked -=` |

**非同期系ハンドラの型（Copy 系 6 種 + Clear + StopObserving + エラー系 4 種で共通）:**

```csharp
private void OnCopyPlainTextClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnCopyPlainTextClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
    SetPendingOperation("Copy plain text");
    AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
    {
        text = "Hello from Unity Native Toolkit",
        label = "sample"
    });
#else
    SetResult("Android device only. Run this sample on Android to copy plain text.");
#endif
}
```

**各ボタンの固定サンプル値（v2 で確定。実装時はこの値をそのまま使う）:**

| ボタン | Payload の固定値 | 期待結果 | 対応する確認観点 |
|---|---|---|---|
| `CopyPlainTextButton` | `text = "Hello from Unity Native Toolkit"`, `label = "sample"` | Success | プレーンテキストコピー |
| `CopyEmptyPlainTextButton` | `text = ""` | **Success**（blank 許容） | プレーンテキスト blank |
| `CopyHtmlTextButton` | `plainText = "Hello"`, `htmlText = "<b>Hello</b>"` | Success | HTML コピー |
| `CopyHtmlEmptyPlainTextButton` | `plainText = ""`, `htmlText = "<b>Html only</b>"` | **Success**（検証対象は `htmlText` のみ） | HTML の plainText のみ空 |
| `CopyUriButton` | `uri = CreateSampleContentUri("clipboard_sample.txt")` | Success | URI コピー |
| `CopyMultipleTextButton` | `texts = new[] { "First", "", "Third" }` | **Success**（要素の空文字は許容） | 複数テキスト / 空要素 |
| `CopySensitiveTextButton` | `text = "P@ssw0rd-sample"`, `isSensitive = true` | Success | 機微フラグ |
| `CopyEmptyHtmlButton` | `plainText = "Hello"`, `htmlText = ""` | Failed / `CLIPBOARD_EMPTY_CONTENT` | HTML 本文 blank |
| `CopyEmptyItemsButton` | `texts = Array.Empty<string>()` | Failed / `CLIPBOARD_EMPTY_ITEMS` | 複数テキスト空配列 |
| `CopyBlankUriButton` | `uri = ""` | Failed / `CLIPBOARD_INVALID_URI` | URI 不正（blank） |
| `CopyHttpUriButton` | `uri = "http://example.com/x"` | Failed / `CLIPBOARD_INVALID_URI` | URI 不正（http） |

`CopyHtmlEmptyPlainTextButton` と `CopyMultipleTextButton` の空要素は、**成功することを確認するためのケース**であり、Error Cases セクションには置かない（3.1 のとおり Copy セクションに配置する）。

**共通 event ハンドラ:**

```csharp
private void OnClipboardOperationCompleted(ClipboardOperationResult result)
{
    Debug.Log($"[{LogTag}][{nameof(OnClipboardOperationCompleted)}] operation: {result.Operation}, isSuccess: {result.IsSuccess}");
    string status = result.IsSuccess ? "Success" : "Failed";
    string title = !string.IsNullOrEmpty(_pendingOperationTitle)
        ? _pendingOperationTitle
        : GetOperationTitle(result.Operation);
    _pendingOperationTitle = string.Empty;
    string msg = $"[event] {title}: {status}";
    if (!string.IsNullOrEmpty(result.ErrorMessage))
    {
        msg += $"\nError: {result.ErrorMessage}";
    }
    SetResult(msg);
}

private void OnClipboardChanged()
{
    _changeCount++;
    Debug.Log($"[{LogTag}][{nameof(OnClipboardChanged)}] changeCount: {_changeCount}");
    SetResult($"[event] Clipboard changed ({_changeCount})");
}
```

`GetOperationTitle` は `AndroidClipboardManager.Operation*` 定数で switch する（既存 Share Controller と同型）。

**同期系ハンドラ（clipboard 固有・event を経由しない）:**

```csharp
private void OnReadClipboardClicked()
{
    Debug.Log($"[{LogTag}][{nameof(OnReadClipboardClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
    var result = AndroidClipboardManager.Instance.Read();
    // Log status and error code only: clipboard content may hold passwords or tokens.
    Debug.Log($"[{LogTag}][{nameof(OnReadClipboardClicked)}] status: {result.Status}, errorCode: {result.ErrorCode}");
    switch (result.Status)
    {
        case ClipboardReadStatus.HasContent:
            SetResult($"Success: Read: {FormatContents(result.Contents!)}");
            break;
        case ClipboardReadStatus.Empty:
            SetResult("Info: Clipboard is empty (normal)");
            break;
        default:
            SetResult($"Failed: {result.ErrorCode}: {result.ErrorMessage}");
            break;
    }
#else
    SetResult("Android device only. Run this sample on Android to read the clipboard.");
#endif
}
```

`HasClip` は `SetResult($"Success: hasClip = {AndroidClipboardManager.Instance.HasClip()}")`、`GetDescription` は `Read` と同じ 3 分岐。

**整形ヘルパ（native サンプルの `formatReadResult` / `formatDescription` 相当、private static）:**

```csharp
// Formats clip contents for on-screen display only. Never logged: clipboard content is sensitive.
private static string FormatContents(ClipContents contents)
    => $"label={contents.Label}, mimeTypes=[{string.Join(", ", contents.MimeTypes)}], items=[{...}]";

private static string FormatDescription(ClipDescriptionInfo info)
    => $"label={info.Label}, mimeTypes=[...], isStyledText={info.IsStyledText}, classificationStatus={info.ClassificationStatus}";
```

`ClipItem` の各値は null になりうるため（機能実装の仕様）、`item.Text ?? "(null)"` の形で表示し、null と空文字が同一視されている旨が読み取れるようにする。

### 6.4 `content://` URI の生成方針（不足前提 1.5 への対応）

`CopyUri` に渡す `content://` URI を作る API は clipboard 機能に無いため、**サンプル側で `FileProvider` を直接呼ぶ**。

```csharp
// The clipboard API takes a URI string but provides no way to build one. Mirror the native sample
// (ClipboardSampleScreen.prepareSampleUri) and use the FileProvider bundled in the native-toolkit AAR.
private static string CreateSampleContentUri(string fileName)
{
    string path = Path.Combine(Application.persistentDataPath, fileName);
    File.WriteAllText(path, "Clipboard sample file content");

    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    using var file = new AndroidJavaObject("java.io.File", path);
    string authority = $"{Application.identifier}.native_toolkit.share.fileprovider";
    using var fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider");
    using var uri = fileProvider.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file);
    return uri.Call<string>("toString");
}
```

根拠と制約:

- FileProvider は同梱 AAR（`android-native-toolkit-1.3.0.aar`）の manifest に `${applicationId}.native_toolkit.share.fileprovider` として宣言済み。**利用アプリ側の追加設定は不要**
- `res/xml/native_toolkit_share_file_paths.xml` は `files-path` / `cache-path` / `external-files-path` の 3 つを `path="."` で公開しており、`Application.persistentDataPath`（external-files-path 相当）は対象に含まれる
- 例外時は `SetResult($"File preparation failed: {ex.Message}")` として Manager を呼ばずに終了する（既存 Share Controller と同型）
- **要検証**: `Application.identifier` と実際の `applicationId` が一致すること（Unity の Package Name 設定をそのまま使うため通常は一致するが、Gradle 側で suffix を付けている場合はずれる）。実機確認項目に含める

### 6.5 入力バリデーション方針

- **Controller 側で入力値を検証しない**。全ボタンが固定のサンプル値を送り、Error Cases セクションはむしろ native 側のドメインエラーを意図的に発火させるためにある
- 機能実装が `copyPlainText` の blank を許容する仕様（設計 1.10）のため、**空文字を弾く独自チェックを追加しない**。`Copy Plain Text (empty, allowed)` ボタンはこの仕様を確認するためのもので、成功表示になることが正しい
- Manager 呼び出し前に失敗しうるのは `CreateSampleContentUri` のファイル生成・FileProvider 呼び出しのみ。ここだけ try/catch する

### 6.6 導線追加

**`TopMenuExample.uxml`:**

```xml
<ui:Button name="ClipboardFeatureButton" text="Clipboard" class="top-button top-button-secondary" />
```

**`TopMenuExampleController.cs`:**

- `_clipboardButton` を `root.Q<Button>("ClipboardFeatureButton")` で取得
- Share ボタンと同じく、対応プラットフォームのみ購読し、非対応では `style.display = DisplayStyle.None`
- **clipboard は Android のみ実装済み**のため、ガードは `#if UNITY_ANDROID || UNITY_EDITOR`（Share の `UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR` より狭い）
- `OnClipboardClicked` は Editor では `EditorUtility.DisplayDialog("Clipboard Feature", "This feature runs natively on Android.\nRun on an Android player for full functionality.", "OK")`、実機では `NativeToolkitSampleNavigator.ShowAndroidClipboard(uiDocument)`

**`NativeToolkitSampleNavigator.cs`:**

```csharp
public static void ShowAndroidClipboard(UIDocument uiDocument)
{
#if UNITY_ANDROID || UNITY_EDITOR
    ApplyScreen<AndroidClipboardManagerExampleController>(
        uiDocument,
        "UI/Android/Clipboard/AndroidClipboardManagerExample",
        "UI/Android/Clipboard/AndroidClipboardManagerExampleStyle");
#endif
}
```

`RemoveExistingControllers` の `#if UNITY_ANDROID || UNITY_EDITOR` ブロックに `RemoveIfExists<AndroidClipboardManagerExampleController>(gameObject);` を追加する。

---

## 7. 手動確認観点

### 7.1 EditMode 静的チェック（`AndroidClipboardSampleSceneWiringTests.cs`）

既存 `AndroidShareSampleSceneWiringTests.cs` と同型。UXML と Controller の名前不一致を実機に持ち込む前に検出する。

- `AndroidClipboardManagerExample.uxml` が Resources パスに存在すること
- `AndroidClipboardManagerExampleStyle.uss` が Resources パスに存在すること
- UXML が 3.1 の全18ボタン name（操作ボタン 17 + `HomeButton`）を含むこと
- UXML が `ResultTextBlock` Label を含むこと
- `TopMenuExample.uxml` が `ClipboardFeatureButton` を含むこと

### 7.2 実機確認（Android 12 以降）

実装結果 v3 の 1.4（全18項目）を、本サンプルの操作に対応付ける。

| 観点 | 操作 | 期待 |
|---|---|---|
| プレーンテキストコピー | Copy Plain Text → 他アプリで貼り付け | 貼り付け成功。`[event] Copy plain text: Success` |
| プレーンテキスト blank | Copy Plain Text (empty, allowed) | **Success**（エラーにならない）。直後の Has Clip が true |
| HTML コピー | Copy HTML Text → HTML 対応アプリで貼り付け | 書式付きで貼り付けられる |
| HTML の plainText のみ空 | Copy HTML Text (empty plain text, allowed) | **Success**（`htmlText` が非空なら成功する。エラーにならないことを確認） |
| HTML 本文 blank | Copy HTML (empty) -> EmptyContent | `Failed` + `Error: Clipboard content is empty. Please provide text or HTML.` |
| URI コピー | Copy URI | `Success`。`Application.identifier` と applicationId のずれが無いことも確認（6.4 要検証） |
| URI 不正（blank） | Copy URI (blank) -> InvalidUri | `Failed` + `Error: Invalid URI: ` |
| URI 不正（http） | Copy URI (http scheme) -> InvalidUri | `Failed` + `Error: Invalid URI: http://example.com/x` |
| 複数テキスト / 空要素 | Copy Multiple Text (includes an empty item) | **Success**（`{ "First", "", "Third" }` の 3 件。要素に空文字が含まれても成功することを確認）。直後の Read Clipboard で items が 3 件返ることも確認する |
| 複数テキスト空配列 | Copy Multiple (empty list) -> EmptyItemList | `Failed` + `Error: No items provided for clipboard copy.` |
| 機微フラグ | Copy Sensitive Text | Android 13+ でコピー時のプレビューに内容が出ない |
| 読み取り | 何かコピー後 Read Clipboard | `Success: Read: label=..., mimeTypes=[...], items=[...]` |
| 空読み取り | Clear Clipboard → Read Clipboard | `Info: Clipboard is empty (normal)`（`Failed` ではない） |
| メタデータ | Get Description | mimeTypes / isStyledText / classificationStatus が表示される |
| 有無判定 | Has Clip | コピー後 true、Clear 後 false |
| 監視 | Start Observing → 他アプリでコピー → 復帰 | `[event] Clipboard changed (1)` が表示される |
| 監視の二重開始 | Start Observing を 2 回 → 変化 1 回 | カウンタが 1 だけ増える（2 増えない） |
| 監視停止 | Stop Observing → 他アプリでコピー | カウンタが増えない。`[event] Stop observing: Success` が届く。**0 引数 JNI 呼び出しが解決できていること**（`AndroidJavaException` / `NoSuchMethodError` が出ない。機能実装の要検証 9.3） |
| バックグラウンド読み取り | ホームへ移動した状態から復帰し Read | `Empty` または `Failed: CLIPBOARD_READ_NOT_ALLOWED`。クラッシュしない |
| ログ安全性 | 一連の操作後に Logcat / Unity ログを確認 | **クリップボード本文が出力されていない**（結果ラベルには表示されるがログには出ない） |
| ライフサイクル | Back To Home → 再度 Clipboard → 他アプリでコピー | `OnDisable` の `StopObserving` により、画面を離れている間の変化を拾わない。再入場後もクラッシュしない |
| 非対応環境 | Unity Editor で Clipboard ボタン | `EditorUtility.DisplayDialog` が出るだけでエラーにならない |

---

## 8. Definition of Done

- 4.1 の新規ファイルが全て作成されている
- 4.2 の既存 3 ファイルに導線が追加されている
- 3.1 の全18ボタン（操作ボタン 17 + `HomeButton`）が UXML と Controller で名前一致している（7.1 の EditMode テストが passed）
- 同期 3 メソッドの結果が event を介さずその場で表示される
- `StartObserving` の結果を「成功」と断定表示していない
- `OnEnable` / `OnDisable` で event 購読・解除し、`OnDisable` で `StopObserving()` を呼んでいる
- `copyPlainText` の blank に Controller 独自のバリデーションを追加していない
- クリップボード本文・読み取り結果がどのログにも出力されていない
- UI 文言・コメント・ログがすべて英語、絵文字なし
- EditMode テストが全て passed
- 7.2 の実機確認が完了している

---

## 9. 要検証事項

1. **`Application.identifier` と applicationId の一致**（6.4）: FileProvider の authority を `Application.identifier` から組み立てるため、Gradle テンプレートで `applicationIdSuffix` を付けている場合に authority が実際と食い違い、`IllegalArgumentException`（Failed to find configured root）になる。実機の Copy URI 操作で確認する。ずれる場合は authority をサンプル側の定数へ切り出す
2. **`ClipboardChanged` の発火回数**: Android の `OnPrimaryClipChangedListener` は 1 回のコピーで複数回発火する端末が存在するとの報告がある。カウンタ表示にしているため実機で挙動を観察し、実際に複数回発火する場合はその旨をサンプルの注意 Label に追記する
3. **`OnDisable` での `StopObserving()` 呼び出し**: 監視していない状態で `StopObserving` を呼んでも native 側は `monitor.stop()` が no-op のため安全と読めるが、`ClipboardOperationCompleted` が `stopObserving` の Success で発火する。画面遷移時に結果ラベルが更新される可能性があるものの、遷移後は Controller が破棄されるため実害はない見込み。実機で不自然な表示が出ないか確認する
4. **Editor 実行時の `AndroidClipboardManagerExampleController`**: クラス自体は `#if UNITY_ANDROID || UNITY_EDITOR` でコンパイルされるが、Manager 呼び出しは全て `#if UNITY_ANDROID && !UNITY_EDITOR` で囲むため Editor では UI 表示のみになる。Navigator 経由で Editor でも画面が開けること自体は既存 Share と同じ挙動
