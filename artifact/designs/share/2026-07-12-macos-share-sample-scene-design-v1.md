# macOS Share サンプルシーン実装計画書 (v1)

- 対象機能: share
- 対象プラットフォーム: macOS (Standalone)
- 作成日: 2026-07-12
- 参照実装結果: `artifact/results/share/2026-07-12-macos-share-implementation-feature-result-v1.md`
- 対象範囲: ExampleController / UXML / USS / ナビゲーション統合 / (任意) 配線テスト
- 対象外: Bridge / Manager 本体（`design-feature` で実装済み）

---

## 0. 前提抽出（実装結果ファイル由来）

`MacShareManager`（実装済み）の公開 API と契約:

- `void Share(MacShareContentPayload? payload, Action<MacShareResult>? onResult = null)` — ピッカー提示（`shareContent`）
- `void ShareViaService(string serviceName, MacShareContentPayload? payload, Action<MacShareResult>? onResult = null)` — 指定サービス直接実行（`shareViaService`）
- 共通イベント: `event Action<MacShareResult>? ShareCompleted`（両操作で常に発火、per-call callback より先）
- Payload: `MacShareContentPayload { items, recipients, subject, excludedServiceTitles }` / `MacShareItem.Text/Url/Image/File`
- サービス定数: `MacShareServiceNames.MailCompose` / `.Message`
- 結果: `MacShareResult { Operation, IsSuccess, Completed, ServiceName, ErrorMessage }`
  - `IsSuccess==false` のとき `ErrorMessage` 非 null 保証
  - キャンセル: `IsSuccess=true, Completed=false, ServiceName=null`
- プラットフォーム契約: native 呼び出しは `UNITY_STANDALONE_OSX && !UNITY_EDITOR`。非対象（Editor 含む）では `Failure("macOS share is only available on a macOS Standalone player.")` を返す
- ピッカー（`Share`）は mouseDown 依存で **best-effort・要検証**。`ShareViaService` が verified-safe path

不足前提（勝手に要件追加しない）:

- **`canPerform` は C Bridge 非公開**のため、native サンプル（`ShareSampleView.swift`）の `CanPerformMail` に相当する UI は Unity では提供不可。本計画では除外し、差分として明記する

---

## 1. 既存サンプルコード深掘り結果

### 1.1 参照した既存コード

- `Runtime/UI/iOS/Share/IosShareManagerExampleController.cs`（Share 機能の見せ方・結果表示パターンの基準）
  - `ShareCompleted` 共通イベントを `OnEnable`/`OnDisable` で購読/解除
  - `SetPendingOperation(title)` → 実行 → `OnShareCompleted(result)` で結果表示
  - `CreateSampleImage`（RenderTexture blit で読み取り→PNG）/ `CreateSampleTextFile`（persistentDataPath）
  - エラーボタン群（Empty / InvalidUrl / MissingFile / MissingImage）
  - Manager の内部プラットフォームガードに委譲し、ハンドラ内は `#if` なしで直接呼ぶ
- `Runtime/UI/macOS/Notification/MacNotificationManagerExampleController.cs`（macOS プラットフォームガード・ライフサイクルの基準）
  - クラスガード `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`
  - `Q<Button>` 取得 → `clicked +=`、`OnDestroy` で `clicked -=`
- `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` / `Runtime/UI/Top/TopMenuExampleController.cs`（導線）
- `Runtime/Resources/UI/iOS/Share/IosShareManagerExample.uxml` / `macOS/Notification/*.uxml`（レイアウト・命名）
- native サンプル `native-toolkit/mac/MacLibraryExample/MacLibraryExample/ShareSampleView.swift`（機能セット・セクション構成の基準）

### 1.2 native サンプルとの機能対応（ShareSampleView.swift → Unity）

| native セクション | native ボタン | Unity 反映 |
| --- | --- | --- |
| Picker - Basic | ShareText / ShareURL / ShareImage / ShareFile | 反映（`Share`） |
| Picker - Multiple | ShareMultipleImages / ShareMultipleFiles / ShareTextAndURL | 反映（`Share`） |
| Picker - Filter | ShareExcludingServices | 反映（`excludedServiceTitles`） |
| Direct Service | ShareViaMail | 反映（`ShareViaService` + recipients/subject） |
| Direct Service | CanPerformMail | **除外**（C Bridge に `canPerform` 非公開） |
| Error | ShareEmpty / ShareInvalidURL / ShareMissingFile / ShareMissingImage | 反映（`Share`） |
| Error | ShareUnknownService | 反映（`ShareViaService("invalid.service", ...)`） |

### 1.3 再利用 / 追加 / 変更方針

- **再利用**: `ShareCompleted` + pending-title の結果表示パターン（iOS）、`CreateSampleImage`/`CreateSampleTextFile`（iOS）、macOS のクラスガード・ライフサイクル（MacNotification）、`NativeToolkitSampleNavigator.ApplyScreen`、`Assets/Resources/Images/share_sample_image.png`（iOS/Android と共有、既存・変更不要）
- **追加**: `MacShareManagerExampleController` + UXML/USS、Navigator の `ShowMacShare`、TopMenu の macOS 導線
- **変更**: `NativeToolkitSampleNavigator.cs`、`TopMenuExampleController.cs`（既存の Share ボタンが Android/iOS のみのため macOS を追加）

---

## 2. 画面要件

### 2.1 機能一覧（セクション構成）

- ヘッダ: Home ボタン / タイトル / サブタイトル / 結果表示 Label（`ResultTextBlock`）
- Picker - Basic: Share Text / Share URL / Share Image / Share File
- Picker - Multiple: Share Multiple Images / Share Multiple Files / Share Text + URL
- Picker - Filter: Share Excluding Services（`excludedServiceTitles: ["Add to Reading List"]`）
- Direct Service: Share via Mail（`ShareViaService(MailCompose, recipients+subject)`）
- Error Handling: Share Empty / Share Invalid URL / Share Missing File / Share Missing Image / Share Unknown Service

### 2.2 操作導線

- TopMenu の Share ボタン → （macOS Standalone プレイヤーで）`ShowMacShare` → 本 ExampleController 表示
- 各ボタン押下 → `MacShareManager.Instance.Share(...)` または `ShareViaService(...)` → 結果は `ShareCompleted` 共通イベント経由で `ResultTextBlock` に反映
- Home ボタン → `NativeToolkitSampleNavigator.ShowTopMenu`

### 2.3 エラー表示

- `MacShareResult.IsSuccess==false` のとき `ResultTextBlock` に `[<operation title>] Error: {ErrorMessage}` を表示
- 成功時は `[<title>] completed=true, service={ServiceName}` / キャンセル時は `[<title>] completed=false (cancelled)`
- Editor/非 OSX 実行時: Manager が返す `macOS share is only available on a macOS Standalone player.` をそのまま表示

---

## 3. 変更ファイル一覧

`.meta` は記載しない。

### 3.1 新規作成

| パス | 内容 |
| --- | --- |
| `Runtime/UI/macOS/Share/MacShareManagerExampleController.cs` | macOS Share サンプル UI コントローラ。`#if UNITY_STANDALONE_OSX || UNITY_EDITOR` |
| `Runtime/Resources/UI/macOS/Share/MacShareManagerExample.uxml` | 画面レイアウト（iOS Share UXML を macOS 命名に置換 + Direct/Unknown Service を追加） |
| `Runtime/Resources/UI/macOS/Share/MacShareManagerExampleStyle.uss` | スタイル（`mac-share-*` クラス。MacNotification/iOS Share の USS に準拠） |
| `Tests/Runtime/MacShareSampleSceneWiringTests.cs` | (任意・推奨) UXML/Controller 配線の EditMode 検証。`AndroidShareSampleSceneWiringTests` に準拠 |

### 3.2 既存変更

| パス | 変更内容 |
| --- | --- |
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowMacShare(UIDocument)` を追加（`#if UNITY_STANDALONE_OSX || UNITY_EDITOR`）。`RemoveExistingControllers` の OSX ガードブロックに `RemoveIfExists<MacShareManagerExampleController>` を追加 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | Share ボタン配線ガード `#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR` に `UNITY_STANDALONE_OSX` を追加。`OnShareClicked` に `#elif UNITY_STANDALONE_OSX → ShowMacShare` 分岐を追加。Editor ダイアログ文言を macOS を含む表現に更新 |

### 3.3 非変更（対象だが未変更）

- `Runtime/Resources/UI/Top/TopMenuExample.uxml`: `ShareFeatureButton` は既存。変更不要
- `Runtime/Share/MacShare*.cs`: Manager/Payload/Result（実装済み）。変更不要
- `Assets/Resources/Images/share_sample_image.png`: iOS/Android と共有。再利用のみ、変更不要
- サンプルシーン `Assets/Samples/Native Toolkit/1.0.0/.../NativeToolkitExampleScene.unity` / `Packages/.../Samples~/NativeToolkitExampleScene.unity`: コントローラは Navigator が動的 `AddComponent` するため、シーンアセットの変更は不要

---

## 4. 実装方針（共通パターンの維持/拡張）

### 4.1 維持

- TopMenu → 機能別 ExampleController の導線
- ヘッダに Home / タイトル / 結果表示、機能カテゴリ単位のボタン群
- `ShareCompleted` 共通イベント購読を `OnEnable`/`OnDisable`、`clicked` 解除を `OnDestroy`
- 結果コールバックは Manager 側で `UnityMainThreadDispatcher` 経由済み（サンプルは受け取って Label 更新のみ）
- 公開 API 呼び出し前後のログ（`Debug.Log`）

### 4.2 拡張（iOS Share に対する差分）

- **2 操作を扱う**: iOS は `Share` のみだが、macOS は `Share`（ピッカー）+ `ShareViaService`（直接）。Direct Service セクションと Unknown Service エラーボタンを追加
- **macOS スキーマ**: `previewTitle`/`excludedActivityTypes` ではなく `recipients`/`subject`/`excludedServiceTitles` を使用
- **結果表示**: `MacShareResult.Operation` を title のフォールバックに利用可能だが、iOS と同じく `SetPendingOperation(title)` で明示 title を優先する
- **プラットフォームガード**: iOS の `UNITY_IOS || UNITY_EDITOR` を `UNITY_STANDALONE_OSX || UNITY_EDITOR` に置換

### 4.3 追加判断（計画時判断・理由付き）

- **ハンドラ内 `#if` なしで Manager を直接呼ぶ**（iOS 方式を踏襲）。Manager が内部で native ガード + Editor 失敗メッセージを返すため、MacNotification のような各ハンドラ `#if ... #else SetResult(...)` は不要。`#if` ノイズを削減する
- **`CanPerformMail` 非提供**: C Bridge に `canPerform` が無いため（前提§0）。将来 Bridge 公開時に追加検討と明記

---

## 5. 実装詳細

### 5.1 UXML 要素（`MacShareManagerExample.uxml`）

- ルート: `mac-share-root` > `mac-share-header`（HomeButton / title / subtitle / `ResultTextBlock`）+ `ScrollView`（`mac-share-content`）
- ボタン（`name` / text）:
  - `HomeButton` / "Back To Home"
  - Picker - Basic: `ShareTextButton` "Share Text", `ShareUrlButton` "Share URL", `ShareImageButton` "Share Image", `ShareFileButton` "Share File"
  - Picker - Multiple: `ShareImagesButton` "Share Multiple Images", `ShareFilesButton` "Share Multiple Files", `ShareTextAndUrlButton` "Share Text + URL"
  - Picker - Filter: `ShareExcludingServicesButton` "Share Excluding Services"
  - Direct Service: `ShareViaMailButton` "Share via Mail"
  - Error Handling: `ShareEmptyButton` "Share Empty (error)", `ShareInvalidUrlButton` "Share Invalid URL", `ShareMissingFileButton` "Share Missing File", `ShareMissingImageButton` "Share Missing Image", `ShareUnknownServiceButton` "Share Unknown Service"
- 結果表示 Label: `ResultTextBlock`

### 5.2 Controller（`MacShareManagerExampleController.cs`）

- クラスガード `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`、`[SerializeField] UIDocument? uiDocument`
- `Start` で `InitializeUI`（`Q<Button>`/`Q<Label>` 取得 + `clicked +=`）
- `OnEnable`: `MacShareManager.Instance.ShareCompleted += OnShareCompleted`、`OnDisable`: `-=`
- `OnDestroy`: 全ボタン `clicked -=`
- 各ハンドラ:
  - Basic/Multiple/Filter/Error(Share系) → `SetPendingOperation(title)` → `MacShareManager.Instance.Share(new MacShareContentPayload { ... })`
  - `ShareViaMailButton` → `ShareViaService(MacShareServiceNames.MailCompose, new MacShareContentPayload { items=[Text], recipients=["test@example.com"], subject="Sample Subject" })`
  - `ShareUnknownServiceButton` → `ShareViaService("invalid.service", new MacShareContentPayload { items=[Text] })`
  - `ShareExcludingServicesButton` → `Share(new MacShareContentPayload { items=[Url], excludedServiceTitles=["Add to Reading List"] })`
  - Image/File 系は `CreateSampleImage`/`CreateSampleTextFile`（iOS からロジック移植）で一時ファイルを作成してから share
- `OnShareCompleted(MacShareResult result)`:
  - title = pending（無ければ `result.Operation`）
  - `!IsSuccess` → `[{title}] Error: {ErrorMessage}`
  - `Completed` → `[{title}] completed=true, service={ServiceName ?? "nil"}` / else `[{title}] completed=false (cancelled)`
- 定数: `SampleUrl="https://unity.com"`, `SampleInvalidUrl="not a valid url"`, `MissingFilePath`/`MissingImagePath`, `ExcludedReadingList="Add to Reading List"`, `SampleMailRecipient="test@example.com"`, `SampleSubject="Sample Subject"`, `InvalidServiceName="invalid.service"`
- 全 public/イベント/ハンドラ/ローカル関数の先頭に `Debug.Log`（csharp.md ルール）

### 5.3 入力バリデーション方針

- ユーザー入力欄は無し（固定サンプルデータ）。バリデーションは Manager 側の契約に委譲
- エラーボタンは意図的に失敗を誘発し、`ErrorMessage` 表示を確認する用途
- 画像/ファイル生成失敗時は share を呼ばず `File preparation failed: {ex.Message}` を表示（iOS と同じ防御）

### 5.4 ナビゲーション統合詳細

- `NativeToolkitSampleNavigator.ShowMacShare`:
  ```
  ApplyScreen<MacShareManagerExampleController>(uiDocument,
      "UI/macOS/Share/MacShareManagerExample",
      "UI/macOS/Share/MacShareManagerExampleStyle");
  ```
- `TopMenuExampleController.OnShareClicked`: `#elif UNITY_STANDALONE_OSX → NativeToolkitSampleNavigator.ShowMacShare(uiDocument);`
- Share ボタン表示ガードに `UNITY_STANDALONE_OSX` を追加（現状 macOS では非表示になっているため）

---

## 6. 手動確認観点

判定は実機 macOS 15 以降で行う。step 4 の自動化方針（下記 6.2）と分離する。

### 6.1 実機手動確認（macOS Standalone プレイヤー）

- TopMenu に Share ボタンが表示され、押下で macOS Share サンプルへ遷移する
- Picker - Basic/Multiple/Filter 各ボタンで共有ピッカーが提示される（**mouseDown 依存・要検証**）。サービス選択で `completed=true, service=...`、キャンセルで `completed=false`
- Share Excluding Services で "Add to Reading List" が候補から除外される（ベストエフォート）
- Share via Mail で Mail 作成画面が起動し、recipients/subject が反映される（`ShareViaService`・verified-safe）
- Share Empty → `No shareable items were provided.`
- Share Invalid URL / Missing File / Missing Image → 対応する `ErrorMessage`（`Invalid URL: ...` / `File not found at path: ...` / `Failed to load image at path: ...`）
- Share Unknown Service → `Sharing service unavailable: invalid.service.`
- Home ボタンで TopMenu へ戻る

### 6.2 自動化方針（PlayMode / EditMode）

- **EditMode（`MacShareSampleSceneWiringTests`）**: UXML の Resources パス存在、必須ボタン名（§5.1 の全 `name`）と `ResultTextBlock` の存在、TopMenu の `ShareFeatureButton` 存在を検証（`AndroidShareSampleSceneWiringTests` に準拠）
- **PlayMode（任意）**: Editor 実行時に各ボタン押下 → `ResultTextBlock` に `macOS share is only available on a macOS Standalone player.` が表示されること（合成 `ClickEvent`/`NavigationSubmitEvent` dispatch でボタン起動 → `ShareCompleted` 経由の結果を assert）。native 経路・ピッカー提示は Editor では動かないため対象外
- **自動化できない項目（理由付きで手動に残す）**:
  - 共有ピッカー本体の提示・サービス選択・キャンセル: ネイティブモーダル UI + mouseDown 依存で Unity の合成イベントでは再現不可
  - Mail 等 外部アプリ起動の結果: 他アプリ内部 UI のため
  - `excludedServiceTitles` の実効フィルタ: ネイティブピッカー内部表示のため
  - 上記はいずれも実機 macOS で 6.1 として人手確認する

---

## 7. 要検証事項

- ピッカー（`Share`）の mouseDown コンテキスト依存（実装結果 §要検証）。UI Toolkit ボタンの `clicked` が `NSSharingServicePicker.show` の mouseDown 要件を満たすかは実機未検証。信頼パスは `ShareViaService`
- `MacShareServiceNames.MailCompose` / `Message` の raw 値が対象 SDK で有効に解決されること（実装結果 §8 と同一の要検証項目）
- `Assets/Resources/Images/share_sample_image.png` はアプリ側 Resources に存在（iOS/Android と共有）。サンプル配布パッケージ側に含める必要があるかは別途確認（本計画では既存挙動を踏襲）
- UI Toolkit の合成イベント（`ClickEvent`/`NavigationSubmitEvent`）の pooling API 可視性は UITK バージョン依存。PlayMode 自動化を実装する場合は実バージョンで要確認

---

## 8. 出力範囲の明記

- 本計画は ExampleController / UXML / USS / ナビゲーション統合 / (任意) 配線テストのみ
- Bridge / Manager 本体は `design-feature`（実装済み）の範囲
