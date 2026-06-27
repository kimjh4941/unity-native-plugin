# Android Share サンプルシーン実装計画 v1

## 基本情報

- 日付: 2026-06-21
- 機能名: share
- 対象プラットフォーム: Android
- 出力言語: 日本語
- 実装結果ファイル: `artifact/results/share/2026-06-21-android-share-implementation-feature-result-v1.md`
- 後続工程: review-document スキル

---

## 1. 前提情報（実装結果由来）

### 1.1 実装済みの公開 API（`AndroidShareManager`）

| API | 引数 | per-call コールバック |
|-----|------|---------------------|
| `ShareText` | `ShareTextPayload` | `Action<ShareOperationResult>? onResult` |
| `ShareImage` | `ShareImagePayload` | 同上 |
| `ShareImages` | `ShareImagesPayload` | 同上 |
| `ShareFile` | `ShareFilePayload` | 同上 |
| `ShareFiles` | `ShareFilesPayload` | 同上 |
| `RegisterDirectShareTarget` | `DirectShareTargetPayload` | 同上 |
| `RemoveDirectShareTargets` | `RemoveDirectShareTargetsPayload` | 同上 |
| `ShareWithCallback` | `ShareTextPayload` | `onStarted` + `onSelected`（二段階） |
| `CancelPendingShareCallback` | なし | `Action<ShareOperationResult>? onResult` |

- 共通イベント: `ShareOperationCompleted`（`Action<ShareOperationResult>`） / `ShareCallbackReceived`（`Action<ShareCallbackResult>`）
- イベントは Manager 内部で `UnityMainThreadDispatcher` 経由でメインスレッドに転送済み（コントローラ側で追加 dispatch 不要）
- 操作名定数: `AndroidShareManager.OperationShareText` 等を公開済み

### 1.2 入力制約（Payload 由来）

- `ShareTextPayload.text` は必須。`title` / `subject` / `mimeType` / `previewTitle` / `previewThumbnailPath` は任意（空白は native 送信前に省略される）
- `chooserActions` は API 34+ のみ有効。`ChooserActionPayload.iconBase64` は Base64 PNG/JPEG。`intentAction` の broadcast receiver は **ホストアプリ manifest 宣言が必要**
- 画像/ファイル系の `filePath` / `filePaths` は FileProvider 公開ディレクトリ配下である必要（files-path / cache-path / external-files-path）
- `DirectShareTargetPayload` は `id` / `label` / `iconBase64` 必須、`category` 任意

### 1.3 エラー契約（実装結果由来）

- `ShareOperationResult`: `Operation` / `IsSuccess` / `ErrorMessage`（`IsSuccess == true → ErrorMessage == null`）
- C# Bridge 層の失敗（非 Android / pluginInstance null / activity null / Call 例外）も `Failure(op, message)` として共通イベントに流れる
- `onShareResult`（アプリ選択）は選択時のみ発火。キャンセル / Copy・Edit では未発火

### 1.4 手動確認観点（実装結果由来）

- `AndroidShareManager` 初期化・listener 登録・イベント転送
- per-call callback の発火（共通イベント + 個別）
- `shareWithCallback` 二段階通知 / 選択なしキャンセル
- AAR 差し替え後の manifest / resource merge

### 1.5 不足前提（勝手に要件追加しない）

- `chooserActions` の `intentAction` 受信には BroadcastReceiver と manifest 宣言が必要。受信処理なしではカスタムアクションが押下後の動作・結果表示まで完結しないため、**v1 では Custom Action ボタンを提供しない**（Receiver 実装・manifest 登録・受信結果表示までを変更一覧へ含められる段階で別途追加する）
- AAR は Share 実装入りで未差し替え（別タスク・依存指摘のためスコープ外）。実機確認は AAR 差し替え後

---

## 2. 既存サンプルコード深掘り結果

### 2.1 Unity 既存サンプル構成（参照済み）

- 画面遷移: `NativeToolkitSampleNavigator`（`ApplyScreen<TController>` で UXML/USS 差し替え + Controller 付与）
- 導線: `TopMenuExampleController` の機能ボタン → `NativeToolkitSampleNavigator.ShowAndroidXxx(uiDocument)`
- 機能別 Controller の標準構成（`AndroidNotificationManagerExampleController` 準拠）:
  - `Start` で Manager イベント購読 + `InitializeUI`、`OnDestroy` で購読解除 + ボタン解除
  - UXML: `header`（HomeButton + タイトル + サブタイトル + 結果表示 Label `ResultTextBlock`）+ `ScrollView` 内に機能カテゴリ別 `section`
  - 結果表示は `SetResult(string)` で `ResultTextBlock` を更新（成功/失敗を文言で明示）
  - 共通イベントハンドラ（`OnNotificationOperationCompleted`）で `Operation` から title/description を引いて結果表示
  - `RegisterRequestedOperation(operation, description)` で操作前に「Requested: …」を表示し、完了イベントで上書き
  - 非 Android（Editor）では `SetResult("Android device only. …")` を返す `#if UNITY_ANDROID && !UNITY_EDITOR` ガード

### 2.2 native-toolkit サンプルアプリ構成（参照済み）

- 参照: `/Users/jonghyunkim/Desktop/native-toolkit/android/AndroidLibraryExample/app/src/main/java/.../example/ShareSampleScreen.kt`
- 機能カテゴリ（このセクション分割を Unity サンプルにも踏襲する）:
  - Text Share: Share Text / Share URL / Rich Preview / Custom Action / Subject & Title
  - Image Share: Share Image
  - Multiple Images: Share Multiple Images
  - File Share: Share File
  - Multiple Files: Share Multiple Files
  - Direct Share Target: Register / Remove
  - Share with Callback: Callback / Callback + Rich Preview / Cancel Pending Callback
- 結果表示: 単一 `statusText` を成功（✅）/ 失敗（❌）/ 進行中（ℹ️）で更新
- ファイル準備: `context.cacheDir` に Bitmap/テキストを書き出してから共有 API を呼ぶ
- ライフサイクル: `DisposableEffect` の `onDispose` で `cancelPendingCallback()` を呼ぶ
- 補助クラス: `ShareChooserActionReceiver`（カスタムアクション用 BroadcastReceiver）→ Unity では未移植（要検証扱い）

### 2.3 差分方針（再利用 / 追加 / 変更）

- **再利用する既存コンポーネント**
  - `NativeToolkitSampleNavigator`（Share 用エントリ追加）
  - `TopMenuExampleController` + `TopMenuExample.uxml`（Share ボタン追加）
  - Notification Controller の UXML/USS 構造（header + ScrollView + section）と `SetResult` / 操作 title マッピングのパターン
  - `UnityMainThreadDispatcher`（Manager 内部で使用済み。コントローラは直接 label 更新で可）
- **追加するコンポーネント**
  - `AndroidShareManagerExampleController.cs`
  - `AndroidShareManagerExample.uxml` / `AndroidShareManagerExampleStyle.uss`
- **変更するファイルと理由**
  - `TopMenuExample.uxml`: Share 機能ボタン追加
  - `TopMenuExampleController.cs`: Share ボタンの取得・クリック導線追加
  - `NativeToolkitSampleNavigator.cs`: `ShowAndroidShare` 追加 + `RemoveExistingControllers` に登録

---

## 3. 画面要件

### 3.1 機能一覧（カテゴリ別ボタン）

- Text Share
  - Share Text（最小: `text` のみ）
  - Share URL（`text` = URL, `mimeType` = text/plain）
  - Share with Subject & Title（`title` / `subject` / `mimeType`）
  - Share with Rich Preview（`previewTitle` / `previewThumbnailPath`）
  - （Custom Action は v1 では提供しない。1.5 参照）
- Image Share
  - Share Image（単一画像、サンプル PNG を生成）
- Multiple Images
  - Share Multiple Images（複数 PNG を生成）
- File Share
  - Share File（サンプル txt を生成）
- Multiple Files
  - Share Multiple Files（複数 txt を生成）
- Direct Share Target
  - Register Direct Share Target（`id` / `label` / `iconBase64` / `category`）
  - Remove Direct Share Target（同 `id`）
- Share with Callback
  - Share with Callback（`onStarted` → `onSelected` 二段階）
  - Cancel Pending Callback
- Error Handling（異常系の `ErrorMessage` 表示確認）
  - Share Invalid File（FileProvider 範囲外 / 存在しないパスを 1 件渡し、`Failure` の `ErrorMessage` 表示を確認）

### 3.2 操作導線（入力 → 実行 → 結果表示）

- 入力: 本サンプルは固定値ベース（既存 Notification サンプルと同方針。入力欄は設けない）
- 実行: ボタン押下 → 必要に応じサンプルファイル/アイコンを生成 → Manager API 呼び出し
- 結果表示（二経路を識別可能にする）:
  - 操作前に主結果ラベル `ResultTextBlock` に `Requested: …` を表示
  - 共通イベント経由は経路名プレフィックス `[event]` を付けて `ResultTextBlock` を上書き（`Operation` → title/description で成功/失敗）
  - `ShareWithCallback` の per-call `onSelected` は経路名 `[onSelected]` を付けて **別の Label `CallbackTextBlock`** に表示（`ShareCallbackReceived` の `[event]` 表示と上書き競合しないよう分離）
  - これにより「共通イベント」「per-call callback」の両発火を画面から識別できる（補助として `Debug.Log` も両経路に出す）

### 3.3 エラー表示

- `ShareOperationResult.ErrorMessage` が非 null/非空のとき主結果文言末尾に `Error: {ErrorMessage}` を付与（Notification サンプル準拠）
- `Share Invalid File` で存在しない/範囲外パスを渡し、`Failure` の `ErrorMessage` 表示経路を実演する
- ファイル生成失敗時はコントローラ側で `SetResult("File preparation failed: …")`
- 非 Android（Editor）は Share 画面へ遷移したうえで `SetResult("Android device only. Run this sample on an Android device.")` を表示（UI プレビュー可能。3.x / 6.5 の Editor 導線と整合）

---

## 4. 変更ファイル一覧

### 4.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Android/Share/AndroidShareManagerExampleController.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/AndroidShareManagerExample.uxml`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/AndroidShareManagerExampleStyle.uss`
- （`.meta` は Unity が自動生成。手動作成不要）

### 4.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs`

### 4.3 非変更（対象だが未変更）

- `AndroidShareManager` / `AndroidSharePayloads` / `AndroidShareJsonBuilder` / `ShareOperationResult` / `ShareCallbackResult`: 既存実装をそのまま利用
- `UnityMainThreadDispatcher`: 流用のみ
- AAR（`Plugins/Android/*.aar`）: Share 実装入り差し替えは別タスク

---

## 5. 実装方針

### 5.1 共通実装パターンの維持 / 拡張

- **維持**
  - TopMenu → 機能別 Controller 導線（`NativeToolkitSampleNavigator.ApplyScreen`）
  - Controller 先頭にタイトル + 結果表示領域（`ResultTextBlock`）
  - 機能カテゴリ単位の section + ボタン群
  - 成功/失敗を一目で分かる文言で `SetResult` 更新
  - 公開 API 呼び出し前後のログ（`Debug.Log`）
- **拡張 / 変更**
  - UI 初期化は `Start`、Manager イベント購読・解除は **`OnEnable` / `OnDisable`** に分離（workflow ステップ5 の必須指定に準拠。既存 Android Controller の `Start`/`OnDestroy` 規約からは変更する）
  - 共通イベント `ShareOperationCompleted` に加え、`ShareCallbackReceived`（二段階通知）の購読を追加
  - `ShareWithCallback` のみ per-call `onSelected` も併用し「グローバル + per-call」両パターンを実演。表示先を分離（`[event]`→`ResultTextBlock` / `[onSelected]`→`CallbackTextBlock`）して両発火を識別可能にする
  - 画像/ファイル共有のためのサンプルファイル生成ユーティリティを追加（`Application.temporaryCachePath` = cacheDir = FileProvider cache-path に書き出し）

### 5.2 スレッド / 購読方針

- Manager イベントは内部で `UnityMainThreadDispatcher` 経由で発火 → コントローラは直接 Label 更新でメインスレッド安全（Notification サンプルと同一）
- 購読: `OnEnable` で `ShareOperationCompleted` / `ShareCallbackReceived` を `+=`、`OnDisable` で `-=`（無効化中にイベントを受信し続けないようにする）
- 保留コールバックの解放: `OnDisable` で `CancelPendingShareCallback()` を呼ぶ（native サンプルの `onDispose` 相当。`OnDestroy` 重複解放はしない）
- UI 要素の取得・ボタン購読は `Start` の `InitializeUI`、ボタン購読解除は `OnDisable` または `OnDestroy` のいずれか一方で行い責務を明確化する

### 5.3 コーディング制約（適用済み）

- `agent-rules/coding-rules/common.md`: Manager + Bridge + ExampleController 層、イベント駆動、メインスレッド転送
- `agent-rules/coding-rules/csharp.md`:
  - 全 public / MonoBehaviour イベント / ローカル関数の先頭に `Debug.Log`（`[{LogTag}][{nameof(...)}]`）
  - コメント・UI 文言は英語
  - public メンバに XML ドキュメントコメント

---

## 6. 実装詳細（implement-sample-scene ステップ3 で行う内容）

### 6.1 UXML（`AndroidShareManagerExample.uxml`）

- ルート: `android-share-root`（USS は Notification の class 命名に準拠して `android-share-*` で新設、配色/レイアウトは Notification を踏襲）
- header: `HomeButton`（Back To Home）/ タイトル `AndroidShareManager Example` / サブタイトル / `ResultTextBlock`（共通イベント `[event]` 経路）/ `CallbackTextBlock`（per-call `[onSelected]` 経路）
- ScrollView 内 section と Button name:
  - Text Share: `ShareTextButton` / `ShareUrlButton` / `ShareWithSubjectTitleButton` / `ShareRichPreviewButton`
  - Image Share: `ShareImageButton`
  - Multiple Images: `ShareImagesButton`
  - File Share: `ShareFileButton`
  - Multiple Files: `ShareFilesButton`
  - Direct Share Target: `RegisterDirectShareTargetButton` / `RemoveDirectShareTargetButton`
  - Share with Callback: `ShareWithCallbackButton` / `CancelPendingCallbackButton`
  - Error Handling: `ShareInvalidFileButton`

### 6.2 Controller（`AndroidShareManagerExampleController.cs`）

- `#if UNITY_ANDROID || UNITY_EDITOR` ガード（Notification Controller 準拠）
- フィールド: `UIDocument`、各 Button 参照、`Label _resultLabel` / `Label _callbackLabel`、`LogTag`、Direct Share 用固定 ID 定数 `SampleDirectShareTargetId`（登録・削除で同一 ID を使用することを明示）
- ライフサイクル:
  - `Start`: `InitializeUI`（UI 要素取得 + ボタン購読）
  - `OnEnable`: `#if UNITY_ANDROID && !UNITY_EDITOR` で `ShareOperationCompleted` / `ShareCallbackReceived` を購読
  - `OnDisable`: 同イベント購読解除 + `CancelPendingShareCallback()`（保留コールバック解放）
  - `OnDestroy`: ボタン購読解除
- 各ボタンハンドラ:
  - 操作前ログ + `SetResult("Requested: …")`
  - Payload を構築して `AndroidShareManager.Instance.Xxx(payload)` を呼ぶ（結果は共通イベントで受ける）
  - `ShareWithCallback` は `onSelected` per-call も渡し、選択パッケージ名を **`CallbackTextBlock`** に `[onSelected]` プレフィックス付きで表示
  - `ShareInvalidFile` は存在しない/FileProvider 範囲外パスで `ShareFile` を呼び、`ErrorMessage` 表示経路を実演
  - 非 Android は `SetResult("Android device only. …")`
- 結果ハンドラ:
  - `OnShareOperationCompleted(ShareOperationResult)`: `[event]` + `GetOperationTitle(result.Operation)` + 成功/失敗 + `ErrorMessage` を `ResultTextBlock` に表示
  - `OnShareCallbackReceived(ShareCallbackResult)`: `[event]` + `Selected: {SelectedPackageName}`（null 時は package unavailable 文言）を `ResultTextBlock` に表示。per-call `onSelected` は `[onSelected]` を付けて `CallbackTextBlock` に表示し、両経路の発火を識別可能にする
  - `GetOperationTitle` は `AndroidShareManager.Operation*` 定数で switch

### 6.3 サンプルファイル/アイコン生成ユーティリティ（Controller 内 private）

- `string CreateSampleImage(string fileName)`: `Texture2D` を単色生成 → `EncodeToPNG` → `Application.temporaryCachePath` に書き出し → パス返却
- `string CreateSampleTextFile(string fileName, string content)`: テキストを cache に書き出し
- `string CreateSampleIconBase64()`: `Texture2D` → `EncodeToPNG` → `Convert.ToBase64String`
- **Texture2D 破棄契約**: PNG byte 配列取得後、`try/finally` で `UnityEngine.Object.Destroy(texture)` を実行し native texture を確実に解放する（ボタン連打でのメモリ蓄積を防ぐ）
- **ファイル上書き/削除方針**: 生成ファイル名は固定（例 `share_sample.png`）。毎回同一パスへ上書き保存し、cache 配下のためファイル削除は OS / native の cache 管理に委ねる（明示削除は行わない）
- いずれも try/catch でファイル準備失敗を `SetResult` 表示

### 6.4 入力バリデーション方針

- 本サンプルは固定値生成のため UI 入力バリデーションは行わない
- 正常系ファイル共有はサンプル生成後に `File.Exists` を確認してから共有 API を呼ぶ
- `ShareInvalidFile`（異常系）はあえて存在しない/範囲外パスを渡し、`Failure` の `ErrorMessage` 表示を確認する目的のため `File.Exists` チェックを行わない
- Custom Action（`chooserActions`）は v1 では提供しない（1.5 参照）

### 6.5 導線追加

- `TopMenuExample.uxml`: `ShareFeatureButton`（text="Share"）を追加
- `TopMenuExampleController.cs`: `_shareButton` 取得 + `OnShareClicked` 追加。**Editor でも Share 画面へ遷移**できるよう `#if UNITY_EDITOR || UNITY_ANDROID` で `NativeToolkitSampleNavigator.ShowAndroidShare(uiDocument)` を呼ぶ（Dialog/Notification は Editor で `DisplayDialog` 分岐だが、Share は手動確認の「Editor で Android device only. 表示」と整合させるため画面遷移を採用）
- `NativeToolkitSampleNavigator.cs`: `ShowAndroidShare`（`UI/Android/Share/AndroidShareManagerExample` + `…Style`、ガードは `#if UNITY_ANDROID || UNITY_EDITOR`）追加、`RemoveExistingControllers` の Android ブロックに `AndroidShareManagerExampleController` を登録

---

## 7. 手動確認観点

- TopMenu に Share ボタンが表示され、押下で Share サンプル画面へ遷移する（Editor / Android 両方で遷移する）
- 各 Share ボタンで Android Sharesheet が起動し、`ShareOperationCompleted`（`[event]`）の成功/失敗が `ResultTextBlock` に反映される
- 画像/ファイル共有でサンプルファイルが生成され、共有先アプリで開ける（FileProvider 経由）
- `Share Invalid File`（異常系）で `Failure` となり、`ErrorMessage` が結果表示に出ること
- `Register Direct Share Target`: **Manager の登録成功（`ShareOperationCompleted` Success）を必須判定とする**。Remove で削除成功すること（同一固定 ID を使用）
- （要検証・端末依存）登録後にシステム共有メニューへ Direct Share ターゲットが表示されること（launcher / 共有先 / OS 実装に依存し、登録成功でも表示されない場合がある）
- `Share with Callback`: Sharesheet 起動（onStarted = `[event]`）→ アプリ選択で `ShareCallbackReceived`（`[event]`／`ResultTextBlock`）と per-call `onSelected`（`[onSelected]`／`CallbackTextBlock`）の**両経路が別々に**届き、画面から識別できること
- `Share with Callback` でキャンセル / Copy・Edit 選択時に `onSelected` が発火しないこと
- `Cancel Pending Callback` / 画面離脱（`OnDisable`）で保留コールバックが解放されること
- Editor 実行時は Share 画面へ遷移したうえで「Android device only.」が表示され、native 呼び出しが走らないこと
- （前提・依存指摘スコープ外）AAR を Share 実装入りに差し替え後に上記実機項目を確認すること

---

## 8. 補足

- 実装結果由来の内容（セクション1）と、サンプル計画時の追加判断（セクション2以降）を分離している
- USS は新規（`android-share-*`）。配色・余白は Notification サンプルを踏襲し、画面間の見た目を統一する
- ライフサイクルは workflow ステップ5 の必須指定に従い、UI 初期化を `Start`、Manager イベント購読・解除を `OnEnable`/`OnDisable` に分離する（既存 Android Controller の `Start`/`OnDestroy` 規約から変更する）
- 共通イベント（`[event]`／`ResultTextBlock`）と per-call callback（`[onSelected]`／`CallbackTextBlock`）を別表示にし、両発火を識別可能にする
- カスタムアクション（`chooserActions` / `ShareChooserActionReceiver` 相当の受信）は v1 では提供しない。Receiver 実装・manifest 登録・受信結果表示まで変更一覧へ含められる段階で追加する
- AAR 差し替え / Minimum API Level / AndroidX 依存解決は依存指摘（実装基盤側）として本計画のスコープ外。実機確認の前提条件として扱う
