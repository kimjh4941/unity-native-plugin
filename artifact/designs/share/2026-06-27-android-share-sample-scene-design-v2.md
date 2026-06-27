# Android Share サンプルシーン実装計画 v2 — Custom Chooser Action 追加

## 基本情報

- 日付: 2026-06-27
- 機能名: share
- 対象プラットフォーム: Android
- 出力言語: 日本語
- 実装結果ファイル: `artifact/results/share/2026-06-27-android-share-implementation-feature-result-v1.md`
- 前版: `artifact/designs/share/2026-06-21-android-share-sample-scene-design-v1.md`
- 後続工程: review-document スキル
- 位置づけ: v1（Custom Action 除外）に対し、chooser action callback 実装完了を受けて **Custom Chooser Action デモを追加**する。それ以外の機能・構成は v1 を踏襲する

---

## 0. v1 からの差分サマリー（このバージョンで増えるもの）

- **Custom Action を追加**: v1 では「受信基盤（BroadcastReceiver / manifest 登録 / 受信結果表示）が未整備」のため除外していた（v1 1.5）。今回 `ShareChooserActionTapped` event が実装されたため、**タップ受信から結果表示まで完結できる**ようになり追加する
- **サンプルは global event のみ使用（レビュー高2 反映）**: chooser action のタップ表示は `ShareChooserActionTapped`（global event）のみで行う。`ShareText` の per-call `onChooserAction` は **サンプルでは使わない**。理由は、per-call callback を Manager に保持させると、画面離脱（`OnDisable`）後に Sharesheet 上のカスタムアクションがタップされた際、破棄済み controller のメソッドが呼ばれ得るため。global event は `OnDisable` で購読解除でき stale 参照を完全に回避できる（per-call を個別解除する公開 API は現状 Manager に無い）
- **manifest 宣言は不要**: native-toolkit サンプルアプリ（`ShareChooserActionReceiver`）は manifest 宣言の receiver を使うが、Unity 版は **AAR 内の動的 receiver（`RECEIVER_NOT_EXPORTED`, API 34+）** が処理し、タップを `onChooserAction(actionId)` で Unity へ転送する。ホストアプリ側 manifest 追加は不要
- **表示先ラベルを 1 つ追加**: chooser action のタップ結果（global event）を表示する `ChooserActionTextBlock` を新設する
- **API 34+ 限定**: chooser action は API 34 未満では表示されない。サンプル UI 上に注意書きを表示する
- これ以外（Text/Image/File/DirectShare/Callback/ErrorHandling の各ボタン、ライフサイクル、ファイル生成、Editor 導線）は **v1 と同一**

### 0.1 実装前提（レビュー高1 / 中5 反映）

- 本サンプルは chooser action callback 機能（`AndroidShareManager` / `ShareChooserActionCallbackCoordinator`）に依存する。**`artifact/reviews/share/2026-06-27-android-share-implementation-feature-review-v1.md` の high / medium 指摘が解消済みであることを実装着手の前提とする**
  - 特に high1（`ShareChooserActionCallbackCoordinator.Fire` の例外継続バグ）が修正済みであること。なお本指摘は既に「global event と per-call を別 try/catch に分離」する形で修正適用済み
- Unity Test Runner で機能側 EditMode テストが通過済みであること
- サンプル実装は上記前提が満たされた状態で進める（未解消のまま Custom Action デモを既成事実化しない）

---

## 1. 前提情報（実装結果由来）

### 1.1 実装済みの公開 API（`AndroidShareManager`）

v1 から継続のもの:

| API | 引数 | per-call コールバック |
|-----|------|---------------------|
| `ShareText` | `ShareTextPayload` | `Action<ShareOperationResult>? onResult` |
| `ShareImage` / `ShareImages` | 各 Payload | `Action<ShareOperationResult>? onResult` |
| `ShareFile` / `ShareFiles` | 各 Payload | 同上 |
| `RegisterDirectShareTarget` / `RemoveDirectShareTargets` | 各 Payload | 同上 |
| `ShareWithCallback` | `ShareTextPayload` | `onStarted` + `onSelected`（二段階） |
| `CancelPendingShareCallback` | なし | `Action<ShareOperationResult>? onResult` |

**v2 で利用する追加 API（chooser action 実装由来）:**

| API / メンバ | シグネチャ | サンプルでの利用 |
|--------------|-----------|------|
| `ShareChooserActionTapped`（event） | `event Action<ShareChooserActionResult>` | **サンプルが使う主経路**。chooser action タップ時に常に発火（global）。`OnEnable` で購読、`OnDisable` で解除 |
| `ShareChooserActionResult`（struct） | `ActionId`（タップされた `intentAction`、null は `string.Empty`） | `ChooserActionTextBlock` に `ActionId` を表示 |
| `ShareText`（第3引数） | `ShareText(ShareTextPayload, Action<ShareOperationResult>? onResult = null, Action<ShareChooserActionResult>? onChooserAction = null)` | **サンプルでは第3引数 `onChooserAction` を使わない**（global event のみ。理由は 0 / 5.2 参照）。第1〜2引数のみで呼ぶ |

- 共通イベント: `ShareOperationCompleted` / `ShareCallbackReceived` / `ShareChooserActionTapped`
- イベントはすべて Manager 内部で `UnityMainThreadDispatcher` 経由でメインスレッドに転送済み（コントローラ側で追加 dispatch 不要）
- 操作名定数: `AndroidShareManager.OperationShareText` 等を公開済み

### 1.2 入力制約（Payload 由来）

- v1 と同一。加えて chooser action 固有の制約:
  - `chooserActions` は **API 34+ のみ有効**。API 34 未満では Sharesheet に表示されず、`onChooserAction` も発火しない
  - `ChooserActionPayload.iconBase64` は Base64 PNG/JPEG（必須）、`label` は表示名（必須）
  - **コールバックを受けるには各アクションに固有の非空・非 `android.intent.action.SEND` の `intentAction` が必要**（native の `normalizeActionIds` が空 / SEND を除外するため）
  - chooser action の受信 receiver は **AAR 内で動的登録**される（manifest 宣言不要）
  - chooser action は **`ShareText` 経由のみ**有効。`ShareWithCallback` の `chooserActions` は callback 登録されない（警告ログが出る）
  - Android の `EXTRA_CHOOSER_CUSTOM_ACTIONS` は最大 5 件想定。5 件超は Manager が警告ログを出す（切り詰めなし）

### 1.3 エラー契約（実装結果由来）

- `ShareOperationResult`: `Operation` / `IsSuccess` / `ErrorMessage`（`IsSuccess == true → ErrorMessage == null`）
- C# Bridge 層の失敗（非 Android / pluginInstance null / activity null / Call 例外）も `Failure(op, message)` として共通イベントに流れる
- chooser action コールバックは **成否を持たない通知**（`ActionId` のみ）。share 起動の成否は従来どおり `ShareOperationCompleted`（`OperationShareText`）で受ける
- `onShareResult`（アプリ選択）は選択時のみ発火。キャンセル / Copy・Edit では未発火
- chooser action は **タップされたときのみ**発火。未タップ / API 34 未満では不発

### 1.4 手動確認観点（実装結果由来）

- `AndroidShareManager` 初期化・listener 登録（`setShareChooserActionListener` を含む）・イベント転送
- per-call callback の発火（共通イベント + 個別）
- chooser action: API 34+ 実機で Sharesheet にカスタムアクション表示 → タップで `ShareChooserActionTapped` / per-call `onChooserAction` が発火
- 旧版 AAR では `Initialize` の try/catch で degrade し、既存 share が壊れないこと
- AAR 差し替え後の動作（chooser action 対応版が前提）

### 1.5 不足前提（勝手に要件追加しない）

- chooser action 対応版 AAR が未差し替えの場合、実機で chooser action は動作しない（`Initialize` の degrade により既存 share は継続）。実機確認は **chooser action 対応版 AAR 差し替え後**（依存指摘スコープ・本計画スコープ外）
- chooser action の表示数上限（API 34+ で最大 5 件想定）の最終挙動は OS / OEM 依存のため要検証
- 同一登録からの複数回タップ時に `onChooserAction` が複数回届くかは要検証（per-call を非クリアにする実装の妥当性）

---

## 2. 既存サンプルコード深掘り結果

### 2.1 Unity 既存サンプル構成（参照済み）

- 画面遷移: `NativeToolkitSampleNavigator`（`ApplyScreen<TController>` で UXML/USS 差し替え + Controller 付与）
- 導線: `TopMenuExampleController` の機能ボタン → `NativeToolkitSampleNavigator.ShowAndroidXxx(uiDocument)`
- 機能別 Controller の標準構成（`AndroidNotificationManagerExampleController` 準拠）:
  - `Start` で（実機時）Manager イベント購読 + `InitializeUI`、`OnDestroy` で購読解除 + ボタン解除
  - UXML: `header`（HomeButton + タイトル + サブタイトル + 結果表示 Label `ResultTextBlock`）+ `ScrollView` 内に機能カテゴリ別 `section`
  - 結果表示は `SetResult(string)` で `ResultTextBlock` を更新
  - 共通イベントハンドラで `Operation` から title/description を引いて結果表示
  - 操作前に「Requested: …」を表示し、完了イベントで上書き
  - 非 Android（Editor）は `#if UNITY_ANDROID && !UNITY_EDITOR` ガードで native 呼び出しを抑止し `SetResult("Android device only. …")` を返す
- 注意: 既存 Notification Controller は購読も `Start`/`OnDestroy`。本サンプルは workflow ステップ5 の必須指定に従い、Manager 購読を `OnEnable`/`OnDisable` に分離する（v1 と同方針）

### 2.2 native-toolkit サンプルアプリ構成（参照済み）

- 参照: `/Users/jonghyunkim/Desktop/native-toolkit/android/AndroidLibraryExample/app/src/main/java/.../example/ShareSampleScreen.kt` および `ShareChooserActionReceiver.kt`
- 機能カテゴリ（Unity サンプルにも踏襲）:
  - Text Share: Share Text / Share URL / **Share Text with Custom Action** / Rich Preview / Subject & Title
  - Image / Multiple Images / File / Multiple Files
  - Direct Share Target: Register / Remove
  - Share with Callback: Callback / Callback + Rich Preview / Cancel Pending Callback
- **Custom Action（native サンプル）**:
  - `chooserActionsJson` に `label` / `iconBase64` / `intentAction = ShareChooserActionReceiver.ACTION_CUSTOM_CHOOSER` を 1 件設定して `shareText` を呼ぶ
  - `ShareChooserActionReceiver`（manifest 宣言、`exported=false`）が `onReceive` で Toast 表示
  - **Unity 版との差分**: Unity は manifest receiver を持たず、AAR 内動的 receiver → `onChooserAction(actionId)` で Unity へ転送する。よって Unity サンプルでは **Toast の代わりに `ChooserActionTextBlock` にタップされた `ActionId` を表示**する
- 結果表示: native は単一 `statusText`（✅/❌/ℹ️）。Unity は `ResultTextBlock` + per-call 用ラベルに分離（v1 方針）
- ファイル準備: `context.cacheDir` に書き出してから共有（Unity は `Application.temporaryCachePath`）
- ライフサイクル: `DisposableEffect` の `onDispose` で `cancelPendingCallback()`（Unity は `OnDisable`）

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
- **v1 からの追加分（Controller / UXML 内）**
  - Text Share に `ShareCustomActionButton` を追加
  - chooser action 表示用 `ChooserActionTextBlock` を header に追加
  - Controller に chooser action の購読（`OnEnable`/`OnDisable`）・ハンドラ・per-call 配線を追加

---

## 3. 画面要件

### 3.1 機能一覧（カテゴリ別ボタン）

- Text Share
  - Share Text（最小: `text` のみ）
  - Share URL（`text` = URL, `mimeType` = text/plain）
  - **Share Text with Custom Action（v2 で追加）**: `chooserActions` に固有 `intentAction` 付きアクションを 1〜2 件設定して `ShareText(payload, onResult, onChooserAction)` を呼ぶ。タップで `ChooserActionTextBlock` に `ActionId` を表示
  - Share with Subject & Title（`title` / `subject` / `mimeType`）
  - Share with Rich Preview（`previewTitle` / `previewThumbnailPath`）
- Image Share / Multiple Images / File Share / Multiple Files（v1 と同一）
- Direct Share Target: Register / Remove（v1 と同一、固定 ID）
- Share with Callback: Share with Callback（二段階）/ Cancel Pending Callback（v1 と同一）
- Error Handling: Share Invalid File（v1 と同一）

### 3.2 操作導線（入力 → 実行 → 結果表示）

- 入力: 固定値ベース（v1 と同方針。入力欄なし）
- 実行: ボタン押下 → 必要に応じサンプルファイル/アイコンを生成 → Manager API 呼び出し
- 結果表示（経路を識別可能にする）:
  - 操作前に主結果ラベル `ResultTextBlock` に `Requested: …` を表示
  - 共通イベント `ShareOperationCompleted` は `[event]` プレフィックスで `ResultTextBlock` を上書き（成功/失敗 + `ErrorMessage`）
  - `ShareWithCallback` の per-call `onSelected` は `[onSelected]` プレフィックスで **`CallbackTextBlock`** に表示（`ShareCallbackReceived` の `[event]` 表示と競合させない）
  - **chooser action（v2 追加、global event のみ）**: タップ結果を **`ChooserActionTextBlock`** に表示
    - global event `ShareChooserActionTapped` → `[event] ChooserAction: {ActionId}`
    - **2 件のカスタムアクションを設定**し、タップした action ごとに異なる `ActionId`（例 `…SHARE_CUSTOM_ACTION_SAVE` / `…SHARE_CUSTOM_ACTION_OPEN`）が表示されることを示す（補助として `Debug.Log`）
    - **クリア条件（不足項目反映）**: `ShareCustomActionButton` 押下時に `ChooserActionTextBlock` を `"Waiting for custom action tap..."` で初期化（前回タップ表示を消す）。タップ受信で最新の `ActionId` に上書きする（履歴は残さない）

### 3.3 エラー表示

- v1 と同一:
  - `ShareOperationResult.ErrorMessage` が非 null/非空のとき主結果文言末尾に `Error: {ErrorMessage}` を付与
  - `Share Invalid File` で存在しない/範囲外パスを渡し、`Failure` の `ErrorMessage` 表示経路を実演
  - ファイル生成失敗時はコントローラ側で `SetResult("File preparation failed: …")`
  - 非 Android（Editor）は Share 画面へ遷移したうえで `SetResult("Android device only. …")`
- v2 追加:
  - chooser action は API 34+ のみ有効。サブタイトルまたは Custom Action ボタン付近に「Custom actions require Android 14 (API 34) or later.」を表示し、未対応端末での不発を誤解しないようにする

---

## 4. 変更ファイル一覧

### 4.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Android/Share/AndroidShareManagerExampleController.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/AndroidShareManagerExample.uxml`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/AndroidShareManagerExampleStyle.uss`

**`.meta` の扱い（レビュー中1 反映）**:

- `.meta` ファイルは **AI エージェントが手動作成しない**（`agent-rules/coding-rules/common.md` の規定）。Unity が生成する
- ただし、上記新規 `.cs` / `.uxml` / `.uss` および新規フォルダ（`UI/Android/Share/`, `Resources/UI/Android/Share/`）の `.meta` は、**Unity 生成後にアセットと一緒にコミットする**（GUID の安定性確保のため）。実装後のコミット前に `.meta` 生成・追加を確認する

### 4.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs`

### 4.3 非変更（対象だが未変更）

- `AndroidShareManager` / `AndroidSharePayloads` / `AndroidShareJsonBuilder` / `ShareOperationResult` / `ShareCallbackResult` / `ShareChooserActionResult` / `ShareChooserActionCallbackCoordinator`: 実装済みをそのまま利用（サンプルからの変更なし）
- `UnityMainThreadDispatcher`: 流用のみ
- AAR（`Plugins/Android/*.aar`）: chooser action 対応版差し替えは別タスク（依存）

---

## 5. 実装方針

### 5.1 共通実装パターンの維持 / 拡張

- **維持**（v1 と同一）
  - TopMenu → 機能別 Controller 導線（`NativeToolkitSampleNavigator.ApplyScreen`）
  - Controller 先頭にタイトル + 結果表示領域（`ResultTextBlock`）
  - 機能カテゴリ単位の section + ボタン群
  - 成功/失敗を一目で分かる文言で `SetResult` 更新
  - 公開 API 呼び出し前後のログ（`Debug.Log`）
  - UI 初期化は `Start`、Manager イベント購読・解除は `OnEnable` / `OnDisable`
- **拡張 / 変更（v2 追加分）**
  - `OnEnable` で `ShareOperationCompleted` / `ShareCallbackReceived` に加え **`ShareChooserActionTapped`（global event）** を購読、`OnDisable` で解除
  - Text Share に `ShareCustomActionButton` を追加。ハンドラで chooser action（2 件、別 `intentAction`）付き `ShareTextPayload` を構築し、**`ShareText(payload, onResult: null)`（per-call `onChooserAction` は渡さない）** を呼ぶ。タップ結果は global event `ShareChooserActionTapped` で受ける
  - chooser action 用アイコンはサンプル生成（既存のアイコン生成ユーティリティを流用）
  - chooser action のタップ表示先 `ChooserActionTextBlock` を追加

### 5.2 スレッド / 購読 / ライフサイクル方針

- Manager イベントは内部で `UnityMainThreadDispatcher` 経由で発火 → コントローラは直接 Label 更新でメインスレッド安全
- 購読: `OnEnable` で `+=`、`OnDisable` で `-=`（`ShareOperationCompleted` / `ShareCallbackReceived` / `ShareChooserActionTapped`）
- **per-call `onChooserAction` は使わない（レビュー高2 反映）**:
  - per-call callback を Manager に保持させると、画面離脱（`OnDisable`）後に Sharesheet 上のカスタムアクションがタップされた際、破棄済み controller のメソッドが呼ばれ、stale 参照・null label 更新の不具合になり得る
  - Manager には per-call を個別解除する公開 API が現状無い（`_chooserCoordinator.Clear()` は Manager `OnDestroy` のみ）
  - そのため **サンプルは global event のみ**を使い、`OnDisable` の購読解除で stale 参照を完全に回避する
  - 画面離脱後に Sharesheet が残ったまま action がタップされても、global event は購読解除済みのため controller の表示は更新されない（安全）。動的 receiver の解除は Manager `OnDestroy`（`clearShareChooserActionListener`）/ 次回 `ShareText` の世代置換で native が担当
- **label null 安全（レビュー中2 反映）**: Unity ライフサイクル上 `OnEnable` は `Start` より先に走るため、UI 取得前に Manager イベントが届く可能性がある。全イベントハンドラと `SetResult` / chooser・callback 表示は **label が null のとき安全に no-op**（または取得を試行）し、落ちない/欠落しても継続するようにする
- 保留コールバック（`ShareWithCallback`）の解放: `OnDisable` で `CancelPendingShareCallback()`（v1 と同一）
- **購読対象の整理（レビュー低1 反映）**: `OnEnable`/`OnDisable` に寄せるのは **Manager events のみ**。button の `clicked` 購読は `Start`（`InitializeUI`）/ `OnDestroy` で行う（既存 Notification Controller と同じ責務分割）

### 5.3 コーディング制約（適用済み）

- `agent-rules/coding-rules/common.md`: Manager + Bridge + ExampleController 層、イベント駆動、メインスレッド転送、`.meta` は作成しない
- `agent-rules/coding-rules/csharp.md`:
  - 全 public / MonoBehaviour イベント / 対象ローカル関数の先頭に `Debug.Log`（`[{LogTag}][{nameof(...)}]`）
  - コメント・UI 文言は英語
  - public メンバに XML ドキュメントコメント

---

## 6. 実装詳細（implement-sample-scene ステップ3 で行う内容）

### 6.1 UXML（`AndroidShareManagerExample.uxml`）

- ルート: `android-share-root`（USS class は `android-share-*`、配色/レイアウトは Notification を踏襲）
- header:
  - `HomeButton`（Back To Home）
  - タイトル `AndroidShareManager Example`
  - サブタイトル: `"Share text, images, and files. Custom chooser actions require Android 14 (API 34) or later."`
  - `ResultTextBlock`（共通イベント `[event]` 経路。初期文言 `"Android share sample ready. Run on an Android device to test sharing."`）
  - `CallbackTextBlock`（`ShareWithCallback` per-call `[onSelected]` 経路）
  - **`ChooserActionTextBlock`（chooser action `[event]` 経路、v2 追加。初期文言 `"Custom action result will appear here."`）**
- ScrollView 内 section と Button name:
  - Text Share: `ShareTextButton` / `ShareUrlButton` / **`ShareCustomActionButton`（v2 追加）** / `ShareWithSubjectTitleButton` / `ShareRichPreviewButton`
  - Image Share: `ShareImageButton`
  - Multiple Images: `ShareImagesButton`
  - File Share: `ShareFileButton`
  - Multiple Files: `ShareFilesButton`
  - Direct Share Target: `RegisterDirectShareTargetButton` / `RemoveDirectShareTargetButton`
  - Share with Callback: `ShareWithCallbackButton` / `CancelPendingCallbackButton`
  - Error Handling: `ShareInvalidFileButton`

### 6.2 Controller（`AndroidShareManagerExampleController.cs`）

- `#if UNITY_ANDROID || UNITY_EDITOR` ガード（Notification Controller 準拠）
- フィールド: `UIDocument`、各 Button 参照、`Label _resultLabel` / `Label _callbackLabel` / **`Label _chooserActionLabel`**、`LogTag`、Direct Share 用固定 ID 定数 `SampleDirectShareTargetId`、**chooser action 用 `intentAction` 定数を 2 件（レビュー中3 反映）**:
  - `SampleChooserActionIdSave = "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_SAVE"`
  - `SampleChooserActionIdOpen = "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_OPEN"`
- ライフサイクル:
  - `Start`: `InitializeUI`（UI 要素取得 + ボタン `clicked` 購読）
  - `OnEnable`: `#if UNITY_ANDROID && !UNITY_EDITOR` で `ShareOperationCompleted` / `ShareCallbackReceived` / **`ShareChooserActionTapped`** を購読
  - `OnDisable`: 同 Manager イベント購読解除 + `CancelPendingShareCallback()`
  - `OnDestroy`: ボタン `clicked` 購読解除
  - 注意: `OnEnable` が `Start` より先に走るため、イベントハンドラは label null 安全にする（5.2）
- 各ボタンハンドラ（v1 と同一のものは省略、v2 追加分を記載）:
  - `OnShareCustomActionClicked`（v2 追加）:
    - 操作前ログ + `SetResult("Requested: Share text with custom chooser action")`
    - `_chooserActionLabel` を `"Waiting for custom action tap..."` で初期化（前回タップ表示をクリア）
    - サンプルアイコン Base64 を生成し、`chooserActions` に **2 件**の `ChooserActionPayload` を構築（`label="Save"`/`intentAction=SampleChooserActionIdSave`、`label="Open"`/`intentAction=SampleChooserActionIdOpen`）
    - `AndroidShareManager.Instance.ShareText(payload, onResult: null)` を呼ぶ（**per-call `onChooserAction` は渡さない**。タップは global event で受ける）
    - 非 Android は `SetResult("Android device only. Run this sample on Android 14 (API 34) or later to use custom chooser actions.")`
- 結果ハンドラ:
  - `OnShareOperationCompleted(ShareOperationResult)`: v1 と同一（`[event]` + title + 成功/失敗 + `ErrorMessage` → `ResultTextBlock`）。label null 安全
  - `OnShareCallbackReceived(ShareCallbackResult)`: v1 と同一（`[event]` → `ResultTextBlock`）。label null 安全
  - **`OnShareChooserActionTapped(ShareChooserActionResult)`（v2 追加、global event ハンドラ）**: `_chooserActionLabel` を `[event] ChooserAction: {result.ActionId}` で更新（null 安全）。2 件の action は異なる `ActionId` で届くため、どちらをタップしたか識別できる
  - `GetOperationTitle` は `AndroidShareManager.Operation*` 定数で switch（v1 と同一）

### 6.3 サンプルファイル/アイコン生成ユーティリティ（Controller 内 private、v1 と同一 + 流用）

- `string CreateSampleImage(string fileName)`: `Texture2D` 単色生成 → `EncodeToPNG` → `Application.temporaryCachePath` に書き出し → パス返却
- `string CreateSampleTextFile(string fileName, string content)`: テキストを cache に書き出し
- `string CreateSampleIconBase64()`: `Texture2D` → `EncodeToPNG` → `Convert.ToBase64String`（**chooser action のアイコンにも流用**）
- **Texture2D 破棄契約**: PNG byte 取得後、`try/finally` で `UnityEngine.Object.Destroy(texture)` を実行し native texture を確実に解放
- **ファイル上書き/削除方針**: 生成ファイル名は固定。毎回同一パスへ上書き保存。cache 配下のため明示削除はしない
- いずれも try/catch でファイル準備失敗を `SetResult` 表示

### 6.4 入力バリデーション方針

- v1 と同一（固定値生成のため UI 入力バリデーションなし）
- 正常系ファイル共有はサンプル生成後に `File.Exists` を確認してから共有 API を呼ぶ
- `ShareInvalidFile`（異常系）はあえて存在しない/範囲外パスを渡す
- chooser action（v2）: `intentAction` は固定の非空・非 SEND 値を 2 件使用（`SampleChooserActionIdSave` / `SampleChooserActionIdOpen`）。アイコン生成失敗時は `SetResult` でエラー表示し share 呼び出しを行わない

### 6.5 導線追加（v1 と同一）

- `TopMenuExample.uxml`: `ShareFeatureButton`（text="Share"）を追加
- `TopMenuExampleController.cs`: `_shareButton` 取得 + `OnShareClicked` 追加。`#if UNITY_EDITOR || UNITY_ANDROID` で `NativeToolkitSampleNavigator.ShowAndroidShare(uiDocument)` を呼ぶ（Editor でも Share 画面へ遷移）
- `NativeToolkitSampleNavigator.cs`: `ShowAndroidShare`（`UI/Android/Share/AndroidShareManagerExample` + `…Style`、ガードは `#if UNITY_ANDROID || UNITY_EDITOR`）追加、`RemoveExistingControllers` の Android ブロックに `AndroidShareManagerExampleController` を登録

---

## 7. 手動確認観点

v1 と同一の項目に加え、chooser action（v2）を追加:

- TopMenu に Share ボタンが表示され、押下で Share サンプル画面へ遷移する（Editor / Android 両方）
- 各 Share ボタンで Android Sharesheet が起動し、`ShareOperationCompleted`（`[event]`）の成功/失敗が `ResultTextBlock` に反映される
- 画像/ファイル共有でサンプルファイルが生成され、共有先アプリで開ける（FileProvider 経由）
- `Share Invalid File`（異常系）で `Failure` となり `ErrorMessage` が表示される
- Direct Share: Manager の登録成功（`ShareOperationCompleted` Success）を必須判定。Remove で削除成功（同一固定 ID）
- （要検証・端末依存）登録後にシステム共有メニューへ Direct Share ターゲット表示
- `Share with Callback`: onStarted（`[event]`）→ アプリ選択で `ShareCallbackReceived`（`[event]`／`ResultTextBlock`）と per-call `onSelected`（`[onSelected]`／`CallbackTextBlock`）の両経路が別々に届く
- `Share with Callback` キャンセル / Copy・Edit で `onSelected` 不発
- `Cancel Pending Callback` / 画面離脱（`OnDisable`）で保留コールバック解放
- **（v2）Custom Action（API 34+ / 対応 AAR）**: `Share Text with Custom Action` で Sharesheet に **2 件**のカスタムアクション（Save / Open）が表示され、タップで `ChooserActionTextBlock` に `[event] ChooserAction: {ActionId}` が表示される
- **（v2）複数 action 識別（レビュー中3 反映）**: Save をタップした場合と Open をタップした場合で、異なる `ActionId`（`…SAVE` / `…OPEN`）が表示されること
- **（v2）画面離脱後の安全性（レビュー高2 反映）**: Custom Action 共有後に Home へ戻る（`OnDisable`）→ その後 Sharesheet 上でカスタムアクションをタップしても、購読解除済みのため表示更新もクラッシュも起きないこと
- **（v2）API 33 以下**: カスタムアクションが表示されず、Share 自体は正常動作する（chooser action 不発、サブタイトルの注意書きと整合）
- **（v2）旧版 AAR（未対応）**: `Initialize` の degrade により既存 share が壊れず、chooser action のみ不発になる
- Editor 実行時は Share 画面へ遷移し「Android device only.」表示、native 呼び出しなし
- （前提・依存スコープ外）chooser action 対応版 AAR 差し替え後に上記実機項目を確認

### 7.1 軽量 EditMode 静的チェック（レビュー中4 反映）

ネイティブ非依存で、UI 配線ミスを実装後すぐ検出するための軽量 EditMode テストを追加する（実機手動確認の前段）。

- 新規: `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidShareSampleSceneWiringTests.cs`
- 検証ケース:
  - `AndroidShareManagerExample.uxml` / `AndroidShareManagerExampleStyle.uss` が想定 Resources パスからロードできる（`Resources.Load` 相当 / `AssetDatabase` で存在確認）
  - UXML 内に必須 element name が存在する: `HomeButton` / `ResultTextBlock` / `CallbackTextBlock` / `ChooserActionTextBlock` / 各機能ボタン name（`ShareTextButton` … `ShareCustomActionButton` … `ShareInvalidFileButton`）
  - `TopMenuExample.uxml` に `ShareFeatureButton` が存在する
  - Controller が参照する button name と UXML の name 一致（name 配列を共有 or 文字列突合）
- 目的: UXML の name と Controller の `root.Q<Button>` 名ズレ、TopMenu 導線漏れ、Resources path 間違いを実装後すぐ検出する
- 実機依存（Sharesheet 表示・タップ）は引き続き手動確認

### 7.2 サンプル実装前提チェック（レビュー高1 / 中5 反映）

実装着手前に以下を確認する:

- `artifact/reviews/share/2026-06-27-android-share-implementation-feature-review-v1.md` の high / medium 指摘が解消済み（特に high1 の `Fire` 例外継続バグ修正）
- Unity Test Runner で機能側 EditMode テストが通過済み

---

## 8. Definition of Done（不足項目反映）

- 判定基準: ○ = 実装・コード・テスト確認の範囲で OK / △ = 一部 OK・追加確認要 / × = 未達 / - = 対象外
- [ ] TopMenu に Share ボタンが表示され、Editor / Android 両方で Share 画面へ遷移する
- [ ] `AndroidShareManagerExample.uxml` / `…Style.uss` が Resources からロードされ、全 element name が Controller と一致する（7.1 で検証）
- [ ] 各機能ボタンの `clicked` が `Start` で購読、`OnDestroy` で解除される
- [ ] Manager イベント（`ShareOperationCompleted` / `ShareCallbackReceived` / `ShareChooserActionTapped`）が `OnEnable` 購読・`OnDisable` 解除される
- [ ] Custom Action（global event のみ）で 2 件の action が表示され、各 `ActionId` が `ChooserActionTextBlock` に表示される
- [ ] 画面離脱後にカスタムアクションをタップしても stale 参照・クラッシュが起きない（global event 購読解除で担保）
- [ ] イベントハンドラ・`SetResult` が label null 安全（`OnEnable` 先行に対応）
- [ ] API 33 以下でカスタムアクション不発・Share 正常動作
- [ ] 旧版 AAR で既存 share が degrade しない
- [ ] 新規 `.cs` / `.uxml` / `.uss` / フォルダの `.meta` が Unity 生成後にコミットされる
- [ ] 実装前提（7.2）が満たされている（機能実装レビュー high/medium 解消・Test Runner 通過）
- [ ] （前提・依存スコープ外）chooser action 対応版 AAR 差し替え後に実機項目を確認

## 9. 実装タスク分解（不足項目反映）

1. UXML/USS 作成（`AndroidShareManagerExample.uxml` / `…Style.uss`）: header + 3 ラベル + 機能別 section + 全ボタン name
2. Controller 作成（`AndroidShareManagerExampleController.cs`）: フィールド / `Start`（UI 取得・ボタン購読）/ `OnEnable`・`OnDisable`（Manager events）/ `OnDestroy`（ボタン解除）/ 各ハンドラ / 結果ハンドラ（null 安全）/ ファイル・アイコン生成ユーティリティ
3. TopMenu 導線（`TopMenuExample.uxml` の `ShareFeatureButton` / `TopMenuExampleController.cs` の `OnShareClicked`）
4. Navigator（`NativeToolkitSampleNavigator.cs` の `ShowAndroidShare` + `RemoveExistingControllers` 登録）
5. 軽量 EditMode 静的チェック（`AndroidShareSampleSceneWiringTests.cs`、7.1）
6. `.meta` 生成確認（Unity 生成 → コミット対象に含める）
7. 手動確認（7 の観点、実機 / Editor）

## 10. 補足

- 実装結果由来の内容（セクション1）と、サンプル計画時の追加判断（セクション2以降）を分離している
- v1 からの主差分は **Custom Action デモ追加**のみ。それ以外は v1 を踏襲し、画面間の見た目・構成を統一する
- USS は新規（`android-share-*`）。配色・余白は Notification サンプルを踏襲する
- ライフサイクルは workflow ステップ5 に従い UI 初期化を `Start`、Manager イベント購読・解除を `OnEnable`/`OnDisable` に分離する
- chooser action の表示先 `ChooserActionTextBlock` を新設し、global event（`[event]`）でタップされた `ActionId` を表示する。レビュー高2 を受け、サンプルは per-call `onChooserAction` を使わず global event のみで表示する（画面離脱後の stale 参照回避）
- native サンプルの manifest receiver（`ShareChooserActionReceiver`）は Unity 版では不要（AAR 内動的 receiver が `onChooserAction` へ転送）。この差分を実装時に混同しない
- AAR 差し替え（chooser action 対応版）/ Minimum API Level は依存指摘スコープ。実機確認の前提条件として扱う
