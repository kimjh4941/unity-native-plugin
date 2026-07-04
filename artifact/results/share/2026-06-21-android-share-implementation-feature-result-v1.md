# 実装結果レポート

## 基本情報

- 日付: 2026-06-21
- 機能名: share
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-5
- 計画ファイル: `artifact/designs/share/2026-06-21-android-share-design-v2.md`

## 1. 実装サマリー

### 1.1 設計書由来の実装

- `ShareOperationResult` / `ShareCallbackResult`: `readonly struct`、`Success` / `Failure` ファクトリ、プラットフォームガードなし
- `AndroidSharePayloads`: 全 8 Payload（`ShareTextPayload` / `ShareImagePayload` / `ShareImagesPayload` / `ShareFilePayload` / `ShareFilesPayload` / `DirectShareTargetPayload` / `RemoveDirectShareTargetsPayload` / `ChooserActionPayload`）、プラットフォームガードなし
- `AndroidShareJsonBuilder`: 全 7 ビルダーメソッド、手書き JSON シリアライザ（`AndroidNotificationJsonBuilder` と同方式）、プラットフォームガードなし
- `AndroidShareManager`: `#if UNITY_ANDROID` ガード、Singleton、共通イベント + per-call optional callback、`ShareOperationListenerProxy`（`AndroidJavaProxy`）

### 1.2 実装時の追加判断

- `CancelPendingShareCallback` の `fullArgs` 構築: JSON なしのため `null` を分岐して `new object?[] { activity }` を渡す設計にした（設計書は JSON 有無の詳細分岐を未記載）
- `FireOperationResult` / `FireCallbackResult` を private メソッドに切り出し、`TryPrepareCall` 失敗時と proxy からの呼び出し時の両方で再利用
- 設計書の「dispatch 順序: 共通 → 個別」を `FireOperationResult` 内の `UnityMainThreadDispatcher.Enqueue` ラムダ内で `ShareOperationCompleted?.Invoke` → dict から取り出して `cb?.Invoke` の順で実装

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareOperationResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareCallbackResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidSharePayloads.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareJsonBuilder.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidShareJsonBuilderTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/ShareResultTests.cs`

### 2.2 既存変更

- なし（AAR 差し替えは native-toolkit ビルド後に別途実施）

### 2.3 非変更（対象だが未変更）

- `Plugins/Android/android-native-toolkit-1.1.0.aar`: native-toolkit 側の更新済みビルド成果物への差し替えは別タスク（手動確認項目）
- `Runtime/Common/UnityMainThreadDispatcher.cs`: 流用のみ
- `Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`: 既存参照に `NativeToolkit.Runtime` を含むため追加不要

## 3. エラー契約反映

### 3.1 エラーケース実装反映

| 層 | 条件 | 返却 | 反映箇所 |
|----|------|------|---------|
| C# Bridge | 非 Android / `pluginInstance == null` | `Failure(op, "{op} could not be started.")` | `CallOperation` → `TryPrepareCall` 失敗時 |
| C# Bridge | `currentActivity == null` | `Failure(op, "{op} could not be started.")` | 同上 |
| C# Bridge | `Call` 例外 | `Failure(op, ex.Message)` | `CallOperation` の catch |
| parser / use case / repository | native 側エラー | native が整形した errorMessage をそのまま `Failure` へ | `onShareOperation` proxy |

### 3.2 コールバック返却仕様反映

- `onShareOperation(operation, true, null)` → `ShareOperationResult.Success(operation)`
- `onShareOperation(operation, false, errorMessage)` → `ShareOperationResult.Failure(operation, errorMessage ?? string.Empty)`
- `onShareResult(operation, selectedPackageName)` → `new ShareCallbackResult(operation, selectedPackageName)`

### 3.3 success 時契約

- `ShareOperationResult.Success` は `ErrorMessage = null` を強制（コンストラクタで null 固定）。`isSuccess == true → errorMessage == null` 不変条件を満たす。

## 4. ビルド結果

- 実行コマンド: Unity 6000.4.2f1 `-batchmode -nographics -runTests -testPlatform EditMode`
- コンパイル: SUCCESS（`NativeToolkit.Runtime.Tests.dll` 再ビルド確認済み）
- テスト実行: SUCCESS

## 5. テスト結果

- 実行件数（全体）: 57
- 成功: 57
- 失敗: 0
- Share 関連テスト: 18 件（`AndroidShareJsonBuilderTests` 13 件 + `ShareResultTests` 5 件）

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 |
|-----------|-------------|------------|------|
| ShareText 最小 JSON | `AndroidShareJsonBuilderTests` | `BuildShareTextJson_RequiredOnly_ProducesMinimalJson` | ○ |
| ShareText 全 optional | `AndroidShareJsonBuilderTests` | `BuildShareTextJson_AllOptionals_ProducesFullJson` | ○ |
| ChooserAction intentAction なし | `AndroidShareJsonBuilderTests` | `BuildShareTextJson_ChooserActionWithoutIntentAction_OmitsIntentAction` | ○ |
| ChooserActions 空配列 → 省略 | `AndroidShareJsonBuilderTests` | `BuildShareTextJson_EmptyChooserActions_OmitsChooserActions` | ○ |
| 特殊文字エスケープ | `AndroidShareJsonBuilderTests` | `BuildShareTextJson_EscapesSpecialCharacters` | ○ |
| ShareImage 最小 JSON | `AndroidShareJsonBuilderTests` | `BuildShareImageJson_RequiredOnly_ProducesMinimalJson` | ○ |
| ShareImage mimeType | `AndroidShareJsonBuilderTests` | `BuildShareImageJson_WithMimeType_IncludesMimeType` | ○ |
| ShareImages filePaths | `AndroidShareJsonBuilderTests` | `BuildShareImagesJson_ProducesFilePaths` | ○ |
| ShareFile filePath | `AndroidShareJsonBuilderTests` | `BuildShareFileJson_ProducesFilePath` | ○ |
| ShareFiles filePaths | `AndroidShareJsonBuilderTests` | `BuildShareFilesJson_ProducesFilePaths` | ○ |
| DirectShareTarget 最小 | `AndroidShareJsonBuilderTests` | `BuildDirectShareTargetJson_RequiredOnly_ProducesMinimalJson` | ○ |
| DirectShareTarget category | `AndroidShareJsonBuilderTests` | `BuildDirectShareTargetJson_WithCategory_IncludesCategory` | ○ |
| RemoveDirectShareTargets | `AndroidShareJsonBuilderTests` | `BuildRemoveDirectShareTargetsJson_ProducesIds` | ○ |
| Success 不変条件 | `ShareResultTests` | `ShareOperationResult_Success_IsSuccessTrueAndErrorMessageNull` | ○ |
| Failure 不変条件 | `ShareResultTests` | `ShareOperationResult_Failure_IsSuccessFalseAndErrorMessageSet` | ○ |
| IsSuccess→ErrorMessage null | `ShareResultTests` | `ShareOperationResult_Success_ErrorMessageIsNull_Invariant` | ○ |
| ShareCallbackResult 正常 | `ShareResultTests` | `ShareCallbackResult_WithPackageName_StoresValues` | ○ |
| ShareCallbackResult null 許容 | `ShareResultTests` | `ShareCallbackResult_NullPackageName_IsAllowed` | ○ |

### 5.2 未実施ケース詳細

| テスト観点 | 理由 |
|-----------|------|
| `AndroidShareManager` の初期化・listener 登録・イベント転送 | `#if UNITY_ANDROID` + `AndroidJavaObject` 依存のため EditMode 不可。実機手動確認 |
| per-call callback の発火（共通イベント + 個別）| 同上 |
| `shareWithCallback` 二段階通知 / 選択なしキャンセル | 同上 |
| AAR 差し替え後の manifest / resource merge | native-toolkit AAR ビルド後に別途確認 |

## 6. Definition of Done

- ○ `ShareOperationResult` / `ShareCallbackResult` 実装（ガードなし）
- ○ `AndroidSharePayloads` 実装（ガードなし）
- ○ `AndroidShareJsonBuilder` 実装（ガードなし）
- ○ `AndroidShareManager` 実装（`#if UNITY_ANDROID`）
- ○ EditMode テスト実装・全通過（18 件）
- △ `AndroidShareManager` 実機動作確認（手動確認が必要）
- △ AAR 差し替えと manifest / resource merge 確認（native-toolkit ビルド後）

## 7. ステップ9 実行確認

「この実装結果を採用して、次工程へ進めますか？」

- 実行する: この実装結果を採用して次工程へ進む
- 修正する: 指摘内容を反映して再実装
- キャンセル: ここまでの修正差分は保持したまま、終了
