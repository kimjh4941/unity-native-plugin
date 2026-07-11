# iOS Share サンプルシーン 実装計画書 (v2)

- 対象機能: share（iOS システム共有シート）
- 対象プラットフォーム: iOS（最小 iOS 18）
- 作成日: 2026-07-05
- 参照実装結果: `artifact/results/share/2026-07-05-ios-share-implementation-feature-result-v3.md`
- 対象範囲: iOS Share の ExampleController・UXML/USS・TopMenu/Navigator 導線
- 前提: Runtime 実装（`IosShareManager` 等）は実装済み・テスト済み。本計画はサンプル UI のみを対象とする。
- v2 変更点: レビュー指摘（`...-sample-scene-design-review-v1.md`）を反映。(1) Editor 導線の矛盾を解消
  （iOS ビルドターゲット選択中の Editor は iOS Share、それ以外の Editor は Android Share へ遷移する分岐に確定）、
  (2) `excludedActivityTypes` の手動確認合否条件を Copy 主・Facebook 補足に変更（第 8 章に集約）。

---

## 0. 前提情報（実装結果由来）

### 0.1 実装済み公開 API（`IosShareManager`）

- Singleton: `IosShareManager.Instance`
- 共通イベント: `event Action<IosShareResult>? ShareCompleted`（成功/失敗いずれも常に発火）
- 主 API: `void Share(IosShareContentPayload? payload, Action<IosShareResult>? onResult = null)`
- payload（`IosShareContentPayload`）:
  - `IosShareItem[] items`（必須・非空）
  - `string? subject` / `string? previewTitle` / `string[]? excludedActivityTypes`（任意）
- `IosShareItem` 生成ヘルパ: `IosShareItem.Text / Url / Image / File`
- 結果（`IosShareResult`）: `IsSuccess` / `Completed` / `ActivityType` / `ErrorMessage`
  - キャンセル: `IsSuccess=true, Completed=false, ActivityType=null`
  - 失敗: `IsSuccess=false, ErrorMessage != null`（null/空白は `"Unknown error."` に正規化）

### 0.2 入力制約・エラー契約（実装結果 §3）

- `payload == null` / `items` null・空 → 即時 Failure `"No shareable items were provided."`
- 非 iOS / Editor → 即時 Failure `"iOS share is only available on an iOS device."`
  - **重要**: `IosShareManager` は `#if UNITY_IOS || UNITY_EDITOR` でコンパイルされ、Editor でも
    `Share()` は必ず結果（即時 Failure）を返す。iOS Notification/Dialog（native callback が Editor に
    来ないため `#if UNITY_IOS && !UNITY_EDITOR` でガード）とはこの点が異なる。
- ネイティブ層のドメインエラー（invalidURL / fileNotFound / imageLoadFailed 等）は `ErrorMessage` に格納。

### 0.3 dispatch 順序

- 共通イベント `ShareCompleted` → 個別 callback `onResult` の順（Share 機能内で統一）。

### 0.4 不足前提（要検証／勝手に要件追加しない）

- サンプルとして画像/ファイル共有を含めるかは実装結果に明記なし。native-toolkit iOS サンプル
  （`ShareSampleView.swift`）は Image/File を含むため、本計画では含める方針とする（後述の native
  サンプル整合を参照）。ただし実機でのみ検証可能。
- `excludedActivityTypes` の実機挙動（該当 Activity 非表示）は実機確認項目。

---

## 1. 既存サンプルコードの深掘り結果

### 1.1 確認した既存サンプル

- `Runtime/UI/iOS/Notification/IosNotificationManagerExampleController.cs`:
  iOS の ExampleController 基本形。`UIDocument` 取得、`root.Q<Button>` バインド、`SetResult` による
  結果表示、`#if UNITY_IOS && !UNITY_EDITOR` ガード + `#else` 固定メッセージ、`OnDestroy` でボタン購読解除。
- `Runtime/UI/Android/Share/AndroidShareManagerExampleController.cs`:
  Share 機能の直接の姉妹。**共通イベント中心**の結果表示（`_pendingOperationTitle` を保持して
  `OnShareOperationCompleted` で `[event] {title}: {status}` 表示）、`OnEnable`/`OnDisable` での
  イベント購読/解除、サンプル画像/ファイル生成（`CreateSampleImage` / `CreateSampleTextFile`）。
- `Runtime/UI/Common/NativeToolkitSampleNavigator.cs`:
  `ShowXxx` メソッドで UXML/USS を Resources からロードし ExampleController を AddComponent。
  `RemoveExistingControllers` で既存 controller を除去。**現状 iOS Share の導線なし**。
- `Runtime/UI/Top/TopMenuExampleController.cs`:
  `ShareFeatureButton` は現状 **`#if UNITY_EDITOR || UNITY_ANDROID` のみ**で AndroidShare へ遷移。
  iOS では Share ボタンが非表示（`#else` で `display: None`）。
- `Runtime/Resources/UI/Android/Share/AndroidShareManagerExample.uxml` / `iOS/Notification/*.uxml`:
  ScrollView + セクション（`android-section` / `ios-section`）+ ボタン群 + ヘッダ（Back To Home /
  タイトル / サブタイトル / 結果表示ボーダー）の構成。

### 1.2 native-toolkit iOS サンプルアプリ（`ShareSampleView.swift`）との整合

native iOS サンプルの機能一覧（すべて Unity の `Share(payload)` 単一 API で表現可能）:

| native セクション | native ボタン | payload マッピング |
| ---------------- | ------------ | ----------------- |
| Text | ShareText | items=[Text] |
| URL | ShareURL / ShareURLWithPreview | items=[Url] / +previewTitle |
| Image | ShareImage / ShareMultipleImages | items=[Image] / [Image,Image] |
| File | ShareFile / ShareMultipleFiles | items=[File] / [File,File] |
| Combination | ShareMultiple / ShareWithSubject / ShareExcludingActivities | items=[Text,Url] / +subject / +excludedActivityTypes |
| Error | ShareEmpty / ShareInvalidURL / ShareMissingFile / ShareMissingImage | items=[] / [Url(invalid)] / [File(missing)] / [Image(missing)] |

→ Unity サンプルシーンは上記 native サンプルの機能一覧をほぼそのまま反映する。差分は「Unity では
`IosShareContentPayload` を組んで単一 `Share()` を呼ぶ」点のみ。

### 1.3 再利用 / 追加 / 変更方針

- **再利用する既存コンポーネント**:
  - `NativeToolkitSampleNavigator`（`ShowIosShare` を追加して利用）
  - `UnityMainThreadDispatcher`（Manager 側で使用済み。ExampleController は直接不要）
  - `Assets/Resources/Images/share_sample_image.png`（画像共有サンプルで流用。Android Share と共有）
  - iOS スタイルクラス規約（`ios-*`）を踏襲した新規 USS
- **追加するコンポーネント**:
  - `IosShareManagerExampleController.cs`
  - `IosShareManagerExample.uxml` / `IosShareManagerExampleStyle.uss`
- **変更するファイルと理由**:
  - `NativeToolkitSampleNavigator.cs`: iOS Share 画面遷移 `ShowIosShare` と controller 除去を追加
  - `TopMenuExampleController.cs`: `OnShareClicked` に iOS 分岐、Share ボタン表示ガードに `UNITY_IOS` を追加
  - サンプルシーン（`.unity`）は **変更不要**（TopMenu 起点で controller は動的 AddComponent されるため）

---

## 2. 画面要件

### 2.1 機能一覧（セクション構成）

iOS Notification のセクション化パターン（`ios-section` + `ios-section-title` + ボタン群）に準拠。

- **Text**: `ShareTextButton`（"Share Text"）
- **URL**: `ShareUrlButton`（"Share URL"）/ `ShareUrlPreviewButton`（"Share URL with Preview"）
- **Image**: `ShareImageButton`（"Share Image"）/ `ShareImagesButton`（"Share Multiple Images"）
- **File**: `ShareFileButton`（"Share File"）/ `ShareFilesButton`（"Share Multiple Files"）
- **Combination**: `ShareMultipleButton`（"Share Text + URL"）/ `ShareWithSubjectButton`（"Share with Subject"）/
  `ShareExcludingActivitiesButton`（"Share Excluding Activities"）
- **Error Handling**: `ShareEmptyButton`（"Share Empty (error)"）/ `ShareInvalidUrlButton`（"Share Invalid URL"）/
  `ShareMissingFileButton`（"Share Missing File"）/ `ShareMissingImageButton`（"Share Missing Image"）
- ヘッダ: `HomeButton`（"Back To Home"）+ タイトル + サブタイトル + `ResultTextBlock`

### 2.2 操作導線（入力 → 実行 → 結果表示）

1. ボタンタップ
2. ハンドラで操作ラベルを保持（`SetPendingOperation(title)`）し、`IosShareContentPayload` を構築
   （画像/ファイル系はサンプルファイルを生成してパスを設定）
3. `IosShareManager.Instance.Share(payload)` を呼び出す（per-call callback は使わず共通イベントに一元化）
4. 共通イベント `ShareCompleted` で結果を受け、`ResultTextBlock` を更新

### 2.3 結果・エラー表示

- 成功（活動完了）: `[{title}] completed=true, activityType={activityType}`
- 成功（キャンセル）: `[{title}] completed=false (cancelled)`
- 失敗: `[{title}] Error: {ErrorMessage}`
- Editor / 非 iOS 実行: Manager から `"iOS share is only available on an iOS device."` が Failure として
  返るため、上記「失敗」表示でそのまま表示される（Editor でも導線・結果表示の疎通確認が可能）。

---

## 3. 変更ファイル一覧

`.meta` は Unity 自動生成のため記載しない。パスは `Packages/com.jonghyunkim.nativetoolkit/` 起点。

### 3.1 新規作成

| ファイル | 役割 |
| ------- | ---- |
| `Runtime/UI/iOS/Share/IosShareManagerExampleController.cs` | iOS Share サンプル UI コントローラ。`#if UNITY_IOS \|\| UNITY_EDITOR` |
| `Runtime/Resources/UI/iOS/Share/IosShareManagerExample.uxml` | 画面レイアウト（ScrollView + セクション + ボタン群） |
| `Runtime/Resources/UI/iOS/Share/IosShareManagerExampleStyle.uss` | スタイル（`ios-share-*` クラス。iOS Notification USS に準拠） |

### 3.2 既存変更

| ファイル | 変更内容 |
| ------- | ------- |
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowIosShare(UIDocument)` を追加（`UI/iOS/Share/IosShareManagerExample` をロード）。`RemoveExistingControllers` の iOS ブロックに `IosShareManagerExampleController` の除去を追加 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | `OnShareClicked` に iOS 分岐（`ShowIosShare`）を追加。Share ボタン表示ガードを `UNITY_ANDROID \|\| UNITY_IOS \|\| UNITY_EDITOR` に拡張 |

### 3.3 非変更（対象だが未変更）

- `Assets/Samples/Native Toolkit/1.0.0/Native Toolkit Example/NativeToolkitExampleScene.unity`:
  TopMenu 起点で ExampleController は動的 AddComponent されるため変更不要。
- `Assets/Resources/Images/share_sample_image.png`: 既存リソースを流用（新規追加なし）。
- `Runtime/NativeToolkit.Runtime.asmdef`: UI は既存 Runtime asmdef 配下のため変更不要。

---

## 4. 実装方針

### 4.1 共通実装パターン（維持する点）

- TopMenu → 機能別 ExampleController の導線を維持（Navigator 経由）。
- ExampleController 先頭にタイトル + 結果表示領域（`ResultTextBlock`）を配置。
- 操作を機能カテゴリ単位（Text / URL / Image / File / Combination / Error）でセクション化。
- 実行結果は成功/失敗が一目で分かる文言（`completed` / `Error:` プレフィックス）で更新。
- Manager イベント購読は `OnEnable` / `OnDisable` でライフサイクル管理（`AndroidShareManager`
  ExampleController に準拠）。
- 公開 API 呼び出し前後で `Debug.Log`（全メソッド先頭、`csharp.md` 準拠）。
- ボタン購読解除は `OnDestroy` で実施。

### 4.2 共通パターンから拡張する点（設計判断）

- **結果表示の一元化（共通イベント中心）**: iOS Share は単一操作 `Share` で共通イベントと per-call
  callback が必ずペアで来るため、二重更新を避けて **共通イベント `ShareCompleted` のみで結果表示**する
  （per-call `onResult` は渡さない）。`IosShareResult` は Operation 名を持たないため、
  `AndroidShareManager` ExampleController の `_pendingOperationTitle` パターンで直前操作ラベルを保持する。
- **Editor でも Share() を呼ぶ**: iOS Notification/Dialog は `#if UNITY_IOS && !UNITY_EDITOR` +
  `#else` 固定メッセージだが、iOS Share の Manager は Editor でも即時 Failure を返す設計のため、
  ExampleController のボタンハンドラは **プラットフォームガードなしで統一的に `Share()` を呼ぶ**。
  クラス全体は `#if UNITY_IOS || UNITY_EDITOR`（Manager と同一）とし、共通イベント購読も Editor で有効化する。
  これにより Editor 上でも導線・イベント購読・結果表示の疎通が確認できる（PlayMode 統合テストと整合）。
  なお TopMenu から iOS Share 画面へ到達できるのは **iOS ビルドターゲット選択中の Editor**（§5.6 の
  `#if UNITY_EDITOR && UNITY_IOS` 分岐）。それ以外のターゲットの Editor では TopMenu は Android Share を
  表示するため、iOS Share 画面の Editor 確認は iOS ビルドターゲットに切り替えてから行う。
  - 注記: サンプル画像/ファイル生成は Editor でも実行される（`persistentDataPath` へ書き出し）。Editor では
    共有自体は Failure になるが、生成処理と結果表示の確認はできる。

### 4.3 再利用 / 追加コンポーネント

- 再利用: `NativeToolkitSampleNavigator`、`share_sample_image.png`、iOS USS スタイル規約。
- 追加: `IosShareManagerExampleController` + UXML + USS。

---

## 5. 実装詳細（implement-sample-scene ステップ3で行う内容）

### 5.1 UI 要素（ボタン・結果表示）

- ヘッダ: `HomeButton` / タイトル Label / サブタイトル Label / `ResultTextBlock`（結果表示）
- セクションごとに 2.1 のボタンを配置（`ios-share-button` / 副次操作は `ios-secondary-button`）。
- Error セクションのボタンは `ios-secondary-button`（副次的操作として区別。Android Share の
  Error Handling セクション踏襲）。

### 5.2 各操作の payload 構築方針

- Text: `items=[IosShareItem.Text("Shared from Unity Native Toolkit")]`
- URL: `items=[IosShareItem.Url("https://unity.com")]`
- URL+Preview: 上記 + `previewTitle="Unity"`
- Image: サンプル画像を生成しパス取得 → `items=[IosShareItem.Image(path)]`
- MultipleImages: サンプル画像を2枚生成 → `items=[Image(path1), Image(path2)]`
- File: サンプルテキストファイルを生成 → `items=[IosShareItem.File(path)]`
- MultipleFiles: 2ファイル生成 → `items=[File(path1), File(path2)]`
- Multiple: `items=[Text("Check this out"), Url("https://unity.com")]`
- WithSubject: `items=[Text("Body text")]` + `subject="Sample Subject"`
- ExcludingActivities: `items=[Url("https://unity.com")]` +
  `excludedActivityTypes=["com.apple.UIKit.activity.CopyToPasteboard", "com.apple.UIKit.activity.PostToFacebook"]`
  - 手動確認の主合否条件は **Copy（CopyToPasteboard）の非表示**とする（§6）。Facebook は端末環境依存の
    補足確認。
- ShareEmpty: `items=[]`（Manager が `"No shareable items were provided."` を返す）
- ShareInvalidURL: `items=[Url("not a valid url")]`（native が `invalidURL` を返す想定）
- ShareMissingFile: `items=[File("/nonexistent/share-missing.txt")]`
- ShareMissingImage: `items=[Image("/nonexistent/share-missing.png")]`

### 5.3 サンプルファイル生成（Image / File 系）

- `AndroidShareManagerExampleController` の `CreateSampleImage` / `CreateSampleTextFile` を踏襲する。
  - 画像: `Resources.Load<Texture2D>("Images/share_sample_image")` → RenderTexture blit で readable 化
    → `EncodeToPNG` → `Application.persistentDataPath` に書き出し。
  - ファイル: `Application.persistentDataPath` にテキスト書き出し。
  - iOS では Android の FileProvider 制約はないため、`persistentDataPath` のパスをそのまま共有可能
    （要検証: 実機で共有シートがアクセスできること）。
  - 複数画像/ファイルは異なるファイル名で複数生成する。
- 生成失敗時は `try/catch` で `SetResult("File preparation failed: ...")` を表示し、`Share()` を呼ばない。

### 5.4 Manager API 呼び出し・コールバック購読

- `OnEnable`: `IosShareManager.Instance.ShareCompleted += OnShareCompleted`
- `OnDisable`: `IosShareManager.Instance.ShareCompleted -= OnShareCompleted`
- 各ボタンハンドラ: `SetPendingOperation(title)` → payload 構築 → `IosShareManager.Instance.Share(payload)`
- `OnShareCompleted(IosShareResult result)`:
  - `title = _pendingOperationTitle`（空なら "Share"）、`_pendingOperationTitle` をクリア
  - 成功: `completed` に応じて表示、失敗: `Error: {ErrorMessage}` 表示
  - `SetResult(...)` で `ResultTextBlock` 更新（common.md のスレッド契約: 共通イベントは Manager が
    `UnityMainThreadDispatcher` 経由でメインスレッド発火するため、ExampleController 側で追加の
    dispatch は不要）

### 5.5 入力バリデーション方針

- ExampleController 側は固定サンプル値を使うため、ユーザー入力のバリデーションは不要。
- 空 items / 不正 URL / 不在ファイルは「エラー挙動デモ」として意図的に不正値を渡し、Manager /
  native の Failure 表示を確認する。

### 5.6 Navigator / TopMenu 変更詳細

- `NativeToolkitSampleNavigator.ShowIosShare`:
  ```csharp
  public static void ShowIosShare(UIDocument uiDocument)
  {
  #if UNITY_IOS || UNITY_EDITOR
      ApplyScreen<IosShareManagerExampleController>(
          uiDocument,
          "UI/iOS/Share/IosShareManagerExample",
          "UI/iOS/Share/IosShareManagerExampleStyle");
  #endif
  }
  ```
  `RemoveExistingControllers` の `#if UNITY_IOS || UNITY_EDITOR` ブロックに
  `RemoveIfExists<IosShareManagerExampleController>(gameObject);` を追加。
- `TopMenuExampleController.OnShareClicked`: 既存の Android 分岐に iOS を追加。**Editor は
  ビルドターゲットで分岐する（v2 確定）**:
  ```csharp
  #if UNITY_EDITOR && UNITY_IOS
      // iOS ビルドターゲット選択中の Editor: iOS Share をプレビュー表示
      NativeToolkitSampleNavigator.ShowIosShare(uiDocument);
  #elif UNITY_EDITOR
      // それ以外のビルドターゲットの Editor: 従来どおり Android Share を表示（既存挙動維持）
      NativeToolkitSampleNavigator.ShowAndroidShare(uiDocument);
  #elif UNITY_ANDROID
      NativeToolkitSampleNavigator.ShowAndroidShare(uiDocument);
  #elif UNITY_IOS
      NativeToolkitSampleNavigator.ShowIosShare(uiDocument);
  #endif
  ```
  - **確定方針（v2）**: `UNITY_IOS` は iOS ビルドターゲット選択時に Editor でも定義される
    （Runtime 実装計画 v5 で確認済みの事実）。これを利用し、**iOS ビルドターゲットの Editor では
    `ShowIosShare`**、Android など他ターゲットの Editor では従来どおり `ShowAndroidShare` を表示する。
    これにより §4.2 / §6 の「Editor で iOS Share の導線・イベント購読・結果表示を確認する」は
    「iOS ビルドターゲット選択中の Editor」で成立し、かつ Android サンプルの Editor 確認導線も
    壊さない。前 v1 の暫定方針（Editor は常に Android）との矛盾を解消。
- Share ボタン表示ガード（`InitializeUI`）: `#if UNITY_ANDROID || UNITY_EDITOR` を
  `#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR` に拡張し、iOS でも Share ボタンを表示する。

### 5.7 USS 方針

- iOS Notification USS（`ios-notif-*`）をベースに `ios-share-*` クラスを定義する（root/header/title/
  subtitle/result-border/result-text/scroll/content/section/section-title/share-button/secondary-button）。
- 配色・角丸・ボタン高さは iOS Notification USS（`#007AFF` プライマリ / `#8E8E93` セカンダリ）を踏襲。

---

## 6. 手動確認観点（実機 iOS 18+）

- TopMenu で Share ボタンが表示され、タップで iOS Share サンプル画面へ遷移する。
- Back To Home で TopMenu へ戻る。
- Text / URL / URL+Preview: 共有シートが提示され、活動選択で `completed=true, activityType=...`、
  キャンセルで `completed=false (cancelled)`。
- URL+Preview: 共有シートヘッダにプレビュータイトル "Unity" が表示される。
- Image / MultipleImages: サンプル画像が共有シートに表示され共有できる。
- File / MultipleFiles: サンプルファイルが共有できる。
- Multiple: テキストと URL が両方共有対象になる。
- WithSubject: Mail 等で件名 "Sample Subject" が反映される。
- ExcludingActivities（**主合否条件**）: **Copy（CopyToPasteboard）が共有シートに表示されない**ことを確認する。
  - 補足（環境依存）: Facebook 共有先も表示されないことを確認する。ただし Facebook の表示有無は端末環境・
    OS 状態に依存するため、非表示だけを合否条件にしない。主合否は Copy の非表示で判定する。
- ShareEmpty: `Error: No shareable items were provided.` が表示される。
- ShareInvalidURL: `Error: Invalid URL: ...`（native 文言）が表示される。
- ShareMissingFile / ShareMissingImage: `Error: File not found ...` / `Failed to load image ...` が表示される。
- iPad: 共有シートが popover で提示され、クラッシュしない（native presenter 責務だが実機確認）。
- Editor 実行（**iOS ビルドターゲット選択中**）: TopMenu の Share ボタンから iOS Share 画面へ遷移でき、
  各ボタンで `Error: iOS share is only available on an iOS device.` が表示され、導線・イベント購読・
  結果表示の疎通が確認できる。他ビルドターゲットの Editor では TopMenu は Android Share を表示する。

---

## 7. 補足・留意点

- 本計画は Runtime のサンプル UI のみを対象とする。Runtime 機能実装（`IosShareManager` 等）は変更しない。
- 結果表示は共通イベント `ShareCompleted` に一元化（per-call callback 不使用）。
- サンプルシーン（`.unity`）は変更不要（動的 AddComponent 構成）。
- **（v2 確定）** Editor での Share ボタン遷移先はビルドターゲットで分岐（iOS ターゲット → iOS Share、
  他ターゲット → Android Share。§5.6）。
- `excludedActivityTypes`（Copy 主確認）/ 画像・ファイル共有の実機挙動は要実機確認。

---

## 8. レビュー指摘の反映（v1 → v2）

対象レビュー: `artifact/reviews/share/2026-07-05-ios-share-sample-scene-design-review-v1.md`

| # | 優先度 | 指摘 | v2 での確定方針 | 反映箇所 |
| - | ----- | ---- | -------------- | ------- |
| 1 | medium | Editor で iOS Share を確認する前提（§4.2/§6）と、Editor 遷移先を Android とする暫定方針（§5.6）が矛盾 | Editor 遷移先をビルドターゲットで分岐（`#if UNITY_EDITOR && UNITY_IOS` → iOS Share、それ以外の Editor → Android Share）。iOS ターゲットの Editor で iOS Share 確認が成立し、Android の Editor 導線も維持 | 5.6 / 4.2 / 6 |
| 2 | low | `excludedActivityTypes` 手動確認で Facebook を主合否にすると不安定 | 主合否を Copy（CopyToPasteboard）の非表示に変更。Facebook は環境依存の補足確認と明記 | 5.2 / 6 |

- 不足項目（レビュー「不足項目」節）はいずれも上記で確定済み。
