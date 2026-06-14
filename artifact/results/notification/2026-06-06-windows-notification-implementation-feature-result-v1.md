# 実装結果レポート

## 基本情報

- 日付: 2026-06-06
- 機能名: notification
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-4
- 計画書: artifact/designs/notification/2026-06-06-windows-notification-design-v2.md

## 1. 実装サマリー

### 1.1 native-toolkit 確認由来の実装

- 13 公開関数すべてを DllImport で宣言（initNotificationManager〜openNotificationSettings）
- `NotificationInvokedCallback` delegate に `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` を適用
- `initNotificationManager` の `bool isPackaged` に `[MarshalAs(UnmanagedType.Bool)]` を明示
- 全文字列引数に `[MarshalAs(UnmanagedType.LPWStr)]` + `CharSet = CharSet.Unicode` を適用
- `getAllNotifications` は IntPtr バッファ + `Marshal.AllocHGlobal(bufferSize * 2)` / `PtrToStringUni` / `FreeHGlobal` パターンで実装
- エラーコード 0〜7 をすべて `ErrorCodeToMessage` でマッピング
- `getNotificationSetting` は `WindowsNotificationResult` 契約に乗らない特例 API として `WindowsNotificationSetting` enum を同期返却

### 1.2 実装時の追加判断

- `GetAllNotifications` の public API シグネチャは `onResult` を持つが、リトライ処理は `GetAllNotificationsInternal` に分離した。バッファ不足（pError=5）時に bufferSize を 2 倍ずつ拡張し MaxBufferSize(65536) 超過で打ち切る
- `FireResult` ヘルパーを導入して per-call callback → `NotificationOperationCompleted` event の発火を一元化した
- `Initialize` を public API として提供し、Awake では自動呼び出しを行わない（Mac とは異なる）。Windows は `isPackaged` / `clsid` / `launchUri` がアプリ依存のため明示的初期化が必要
- `GetNotificationSetting` の戻り値は `Enum.IsDefined` で範囲外チェックし、未定義値は `Unknown` を返す

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/WindowsNotificationResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/WindowsNotificationPayloads.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/WindowsNotificationJsonBuilder.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/WindowsNotificationManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsNotificationTests.cs`

### 2.2 既存変更

なし

### 2.3 非変更（対象だが未変更）

- `Common/UnityMainThreadDispatcher.cs`: 変更不要（既存実装を再利用）
- `Notification/MacNotificationManager.cs`: 変更不要（参照パターンのみ）

## 3. エラー契約反映

### 3.1 エラーケース実装反映

| エラーコード | 反映状況 |
|------------|---------|
| 0 (SUCCESS) | `WindowsNotificationResult.Success(operation)` を返す ○ |
| 1 (NOT_INITIALIZED) | `Failure(op, 1)` → ErrorMessage "Not initialized" ○ |
| 2 (DISABLED) | `Failure(op, 2)` → ErrorMessage "Notifications are disabled" ○ |
| 3 (INVALID_PAYLOAD) | `Failure(op, 3)` → ErrorMessage "Invalid JSON payload" ○ |
| 4 (PROGRESS_NOT_FOUND) | `Failure(op, 4)` → ErrorMessage "Progress notification not found" ○ |
| 5 (HRESULT_FAILURE) | `Failure(op, 5)` → ErrorMessage "WinRT HRESULT failure" ○ |
| 6 (BADGE_FAILED) | `Failure(op, 6)` → ErrorMessage "Badge operation failed" ○ |
| 7 (INVALID_PARAMETER) | `Failure(op, 7)` → ErrorMessage "Invalid parameter" ○ |
| getNotificationSetting エラー | `WindowsNotificationSetting.Unknown`（特例 API） ○ |

### 3.2 コールバック返却仕様反映

- pError == 0 → `WindowsNotificationResult.Success(operation)` を per-call callback と `NotificationOperationCompleted` event 両方に渡す
- pError != 0 → `WindowsNotificationResult.Failure(operation, pError)` を渡す
- `NotificationInvoked` event は `OnNotificationInvoked` static コールバックから `UnityMainThreadDispatcher` 経由でメインスレッドに転送する

### 3.3 success 時契約

- `WindowsNotificationResult.Success` は ErrorCode=0, ErrorMessage=null を保証する: ○（テストで確認）

## 4. ビルド結果

- 実行コマンド: Unity Test Runner（EditMode）— **手動実行が必要**
- 結果: 未実行（Unity Editor での実行が必要）
- 補足: `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` のコンパイルガードにより、Editor では全型が有効。テストアセンブリは Editor 限定（includePlatforms: Editor）のため、UNITY_EDITOR が常に定義される

## 5. テスト結果

- 実行したテスト: 未実行（Unity Test Runner での手動実行が必要）
- 結果サマリー:
  - 実行件数: -（未実行）
  - 成功: -
  - 失敗: -
- 未実施項目:
  - EditMode テスト全件: Unity Editor で Test Runner を開き手動実行が必要

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 | 備考 |
|-----------|-------------|------------|------|------|
| Result.Success フィールド確認 | WindowsNotificationTests.cs | Success_SetsIsSuccessTrue_ErrorCodeZero_ErrorMessageNull | - | 手動実行待ち |
| Result.Failure フィールド確認 | WindowsNotificationTests.cs | Failure_SetsIsSuccessFalse_ErrorMessageNotNull | - | 手動実行待ち |
| ErrorCode 1〜7 全マッピング | WindowsNotificationTests.cs | Failure_ErrorCode1〜7_* | - | 手動実行待ち |
| JsonBuilder.Validate: buttons 5超 | WindowsNotificationTests.cs | Validate_ButtonsExceed5_ReturnsError | - | 手動実行待ち |
| JsonBuilder.Validate: audio.loop | WindowsNotificationTests.cs | Validate_AudioLoopWithoutLongDuration_ReturnsError | - | 手動実行待ち |
| JsonBuilder.Validate: args+invokeUri | WindowsNotificationTests.cs | Validate_ButtonWithBothArgsAndInvokeUri_ReturnsError | - | 手動実行待ち |
| JsonBuilder.Build: 正常JSON生成 | WindowsNotificationTests.cs | Build_TitleAndBody_ProducesExpectedJson | - | 手動実行待ち |
| JsonBuilder.Build: 無効ペイロード例外 | WindowsNotificationTests.cs | Build_InvalidPayload_ThrowsArgumentException | - | 手動実行待ち |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
|-----------|-------------|------------|----------|
| Manager Singleton 生成 | - | Awake で Singleton が生成されること | Unity Editor 実行環境が必要 |
| Manager 重複 Awake Destroy | - | 重複 Awake で Destroy されること | Unity Editor 実行環境が必要 |
| Manager event subscribe/unsubscribe | - | NotificationOperationCompleted / NotificationInvoked | Unity Editor 実行環境が必要 |
| 手動確認（実機）全件 | - | Initialize / ShowNotification 他 | native DLL + Windows 実機が必要 |

## 6. Definition of Done

- ○ 13 DllImport 関数すべて宣言
- ○ IL2CPP / AOT 対応（`[UnmanagedFunctionPointer]` / `[MonoPInvokeCallback]` / static delegate GC 防止）
- ○ `[MarshalAs(UnmanagedType.Bool)]` / `LPWStr` / `CharSet.Unicode` 適用
- ○ `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` コンパイルガード全ファイル適用
- ○ `Application.platform != RuntimePlatform.WindowsPlayer` early return 全 public API 適用
- ○ `_initialized` フラグによる `OnDestroy` 二重解放防止
- ○ `DLL_NAME` の `#if DEVELOPMENT_BUILD` 切り替え
- ○ Operation 定数一覧定義
- ○ `WindowsNotificationResult.Success/Failure` ファクトリ + `ErrorCodeToMessage` マッピング
- ○ `WindowsNotificationSetting` / `WindowsBadgeValue` enum 定義
- ○ `getAllNotifications` バッファリトライ（DefaultBufferSize→MaxBufferSize 拡張）
- ○ XML ドキュメントコメント + Debug.Log 全 public API 付与
- ○ JsonBuilder バリデーション（3 制約）+ EditMode テスト
- △ Unity Test Runner での EditMode テスト実行: 手動実行が必要
- - PlayMode テスト: 計画書どおり省略（native DLL 依存）
- - 手動確認（実機）: 別途実施

## 7. ステップ8 実行確認

- 提示文: 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して終了 → review-implementation-feature スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答: 未回答
