# Android Share サンプルシーン実装結果 v1

- 日付: 2026-06-27
- 機能名: share
- 対象プラットフォーム: Android
- 設計ファイル: `artifact/designs/share/2026-06-27-android-share-sample-scene-design-v2.md`
- 後続工程: review-document スキル

---

## 変更ファイル

### 新規作成

| ファイル | 内容 |
|---------|------|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/AndroidShareManagerExample.uxml` | Share サンプル画面の UXML（header 3 ラベル + 8 section + 15 ボタン） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/AndroidShareManagerExampleStyle.uss` | USS（android-share-* クラス群、Notification 準拠） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Android/Share/AndroidShareManagerExampleController.cs` | Share サンプルコントローラ |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidShareSampleSceneWiringTests.cs` | EditMode 静的配線チェストテスト |

### 既存変更

| ファイル | 変更内容 |
|---------|---------|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml` | `ShareFeatureButton` ボタン追加 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs` | `_shareButton` フィールド / `OnShareClicked` / `OnDestroy` 購読解除追加 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowAndroidShare` 追加 / `RemoveExistingControllers` に `AndroidShareManagerExampleController` 登録 |

### `.meta` の扱い

- 上記新規ファイルおよびフォルダ（`Runtime/UI/Android/Share/`、`Runtime/Resources/UI/Android/Share/`）の `.meta` は Unity が自動生成する。コミット前に Unity 生成済みの `.meta` を確認してステージングに含める

---

## 実装したサンプル機能

### UXML / USS

- header: `HomeButton` / タイトル / サブタイトル（API 34+ 注記）/ `ResultTextBlock` / `CallbackTextBlock` / `ChooserActionTextBlock`
- 8 section: Text Share（5 ボタン）/ Image / Multiple Images / File / Multiple Files / Direct Share Target / Share with Callback / Error Handling
- USS: `android-share-*` クラス群（Notification スタイルを踏襲）

### Controller

- **ライフサイクル**: UI 初期化は `Start`（`InitializeUI`）。Manager events は `OnEnable`/`OnDisable` で購読・解除。button `clicked` は `Start`/`OnDestroy`
- **Manager events（`OnEnable` 購読）**:
  - `ShareOperationCompleted` → `[event] {OperationTitle}: Success/Failed` → `ResultTextBlock`
  - `ShareCallbackReceived` → `[event] ShareCallback: Selected {pkg}` → `CallbackTextBlock`
  - `ShareChooserActionTapped` → `[event] ChooserAction: {ActionId}` → `ChooserActionTextBlock`（global event のみ、per-call は使わない）
- **ボタンハンドラ**（各 `#if UNITY_ANDROID && !UNITY_EDITOR` ガード）:
  - `ShareText` / `ShareUrl` / `ShareWithSubjectTitle` / `ShareRichPreview`: テキスト固定値
  - **`ShareCustomAction`**: 2 件の `ChooserActionPayload`（`SAVE` / `OPEN`）、アイコン生成、`onChooserAction` は渡さない、クリア前表示 `"Waiting for custom action tap..."`
  - `ShareImage` / `ShareImages`: `CreateSampleImage` でキャッシュに PNG 生成
  - `ShareFile` / `ShareFiles`: `CreateSampleTextFile` でキャッシュにテキスト生成
  - `RegisterDirectShareTarget`: アイコン生成 + 固定 ID `native_toolkit_sample_target`
  - `RemoveDirectShareTargets`: 同固定 ID
  - `ShareWithCallback`: `onStarted`（ResultLabel）/ `onSelected`（CallbackLabel）の両 callback
  - `CancelPendingCallback`: `OnDisable` でも自動呼び出し
  - `ShareInvalidFile`: 存在しないパスを渡すエラーハンドリングデモ
- **label null 安全**: 全イベントハンドラ・`SetResult` は null チェック済み（`OnEnable` が `Start` より先に走る対応）
- **ファイル/アイコン生成ユーティリティ**: `CreateSampleImage` / `CreateSampleTextFile` / `CreateSampleIconBase64`（try/finally で `Destroy(texture)` を保証）

### 導線

- `TopMenuExampleController`: `ShareFeatureButton` → `OnShareClicked` → `NativeToolkitSampleNavigator.ShowAndroidShare`
- `NativeToolkitSampleNavigator.ShowAndroidShare`: `#if UNITY_ANDROID || UNITY_EDITOR`、`UI/Android/Share/AndroidShareManagerExample` + `…Style`
- `RemoveExistingControllers`: `#if UNITY_ANDROID || UNITY_EDITOR` ブロックに `AndroidShareManagerExampleController` 追加

---

## ビルド / 実行結果

- **ビルド確認**: Unity Editor でのコンパイルエラーはなし（Editor 実行環境で `#if UNITY_ANDROID && !UNITY_EDITOR` ガードにより native 呼び出しは抑止）
- **実機依存の確認**: Android 実機（chooser action 対応版 AAR 差し替え後）での確認は手動確認の対象（以下を参照）

---

## 手動確認観点

| 観点 | 期待結果 | 確認環境 |
|------|----------|---------|
| TopMenu に Share ボタンが表示される | "Share" ボタン表示、押下で Share 画面遷移 | Editor / Android |
| Share 画面の header 表示 | 3 ラベル（Result / Callback / ChooserAction）表示 | Editor / Android |
| 各 Share ボタンで Sharesheet 起動 | `[event] ShareText: Success` 等が ResultTextBlock に表示 | Android 実機 |
| 画像/ファイル共有でサンプルファイル共有 | 共有先アプリで開ける（FileProvider 経由） | Android 実機 |
| Share Invalid File | `[event] ShareFile: Failed\nError: ...` が表示 | Android 実機 |
| Direct Share 登録成功 | `[event] RegisterDirectShareTarget: Success` | Android 実機 |
| Direct Share 削除成功 | `[event] RemoveDirectShareTargets: Success` | Android 実機 |
| Direct Share ターゲット表示（端末依存） | システム共有メニューに表示 | Android 実機（要検証） |
| ShareWithCallback の両経路 | `onStarted`（ResultLabel）→ アプリ選択で `onSelected`（CallbackLabel）| Android 実機 |
| ShareWithCallback キャンセル/Copy/Edit | `onSelected` 不発 | Android 実機 |
| Cancel Pending Callback / 画面離脱 | 保留 callback 解放 | Android 実機 |
| **（v2）Custom Action（API 34+ / 対応 AAR）** | 2 件のカスタムアクション（Save/Open）表示 → タップで ChooserActionTextBlock に `[event] ChooserAction: {ActionId}` | Android 14+ 実機 |
| **（v2）複数 action 識別** | Save/Open 各タップで異なる `ActionId`（`…SAVE` / `…OPEN`）が表示 | Android 14+ 実機 |
| **（v2）画面離脱後の安全性** | Custom Action 共有後に Home 遷移 → Sharesheet でタップしてもクラッシュ/更新なし | Android 14+ 実機 |
| **（v2）API 33 以下** | カスタムアクション不表示、Share 正常動作 | Android 13 以下実機 |
| **（v2）旧版 AAR** | degrade により既存 share は動作、chooser action は不発 | 旧版 AAR 環境 |
| Editor 実行時 | 各ボタンで「Android device only.」表示、native 呼び出しなし | Unity Editor |

---

## 未実施項目（理由付き）

| 項目 | 理由 |
|------|------|
| Android 実機での Sharesheet 起動確認 | 実機依存。chooser action 対応版 AAR 差し替えが依存スコープ外 |
| EditMode wiring テスト実行 | Unity Test Runner での確認は Unity Editor で手動実行が必要（batch mode 不可） |
| chooser action 実機タップ確認 | chooser action 対応版 AAR 差し替え後が前提 |
| Direct Share ターゲット実機表示 | OS / OEM 依存のため要検証 |
| 同一登録からの複数回タップ挙動 | per-call 非クリア実装の実機検証が必要 |

---

## 実装上の追加判断（設計との差分）

- `ShareWithCallback` の `onStarted` / `onSelected` は per-call callback として Controller 内の lambda で実装した。設計では `onStarted` の結果を `ResultTextBlock`（`SetResult` 経由）、`onSelected` を `CallbackTextBlock` に表示する方針を踏襲
- `OnDisable` では Manager events 解除に加えて `CancelPendingShareCallback()` を呼び、保留コールバックを自動解放する（`v1` 方針）
- `CreateSampleImage` は 2 箇所（シングル / マルチ）で同じ PNG を上書きするが、ファイル名を分けて別パスに保存することで `ShareImages` で同一コンテンツの 2 ファイル共有を成立させた
