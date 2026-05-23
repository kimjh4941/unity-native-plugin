# 実装結果レポート

## 基本情報

- 日付: 2026-05-16
- 機能名: macos-notification
- 対象プラットフォーム: macOS
- ブランチ: feature/UNT-3
- 実装計画書: artifact/plans/macos-notification/2026-05-16-macos-notification-implementation-plan-v2.md

## 1. 実装サマリー

### 1.1 計画書由来の実装

- Bridge の 21 関数すべてを DllImport で宣言（PascalCase シンボル名に揃える）
- 5 種類のコールバック delegate を `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` で定義
- SimpleCallback 系（8 操作）: per-call `Action<MacNotificationResult>?` + `NotificationOperationCompleted` global event
- JsonCallback 系（3 操作）: per-call `Action<MacNotificationJsonResult>` のみ（global event なし）
- BoolCallback 系（1 操作）: per-call `Action<bool>` のみ（global event なし）
- Persistent callback 系（2 操作）: global event のみ
- fire-and-forget API（6 操作）: ネイティブ呼び出しのみ、イベント発火なし
- Persistent delegate を `static readonly` フィールドに格納（GC 防止）
- 全 `[MonoPInvokeCallback]` メソッドを `try-catch(Exception)` で保護
- `UnityMainThreadDispatcher.Instance.Enqueue(...)` 経由でメインスレッド転送
- `#if UNITY_STANDALONE_OSX || UNITY_EDITOR` で Manager 以外のクラスをガード（EditMode テスト実行可能化）
- `#if UNITY_STANDALONE_OSX` のみで Manager をガード

### 1.2 実装時の追加判断

- `MacNotificationAuthorizationStatus` に `ParseJson(string? json)` メソッドを追加。JSON から `"status"` フィールドを抽出して enum を返す。caller が `Json` を自分でパースする手間を省く補助メソッドとして提供
- `BuildContentJson` で macOS parser が使用しないフィールド（sound, interruptionLevel, threadIdentifier 等）は出力しない設計とした。iOS JsonBuilder と異なり macOS parser が必要とするフィールドのみに絞った
- `OnDestroy` で Persistent callback の解除処理は行わない（Bridge に解除 API がないため。計画書通り）

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationJsonResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationActionResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationTextInputActionResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationAuthorizationStatus.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationPayloads.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationJsonBuilder.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/MacNotificationTests.cs`

### 2.2 既存変更

なし

### 2.3 非変更（再利用）

- `Runtime/Notification/IosNotificationPayloads.cs`: `NotificationContentPayload`, `TimeIntervalTriggerPayload`, `CalendarTriggerPayload` を macOS でも使用（compile guard なし）
- `Runtime/Common/UnityMainThreadDispatcher.cs`: compile guard なし、共通
- `Runtime/Notification/IosNotificationJsonBuilder.cs`: 再利用しない。MacNotificationJsonBuilder 内に独立実装

## 3. エラー契約反映

### 3.1 エラーケース実装反映

| API | エラーケース | 反映状況 |
|---|---|---|
| RequestPermission | OS が権限を拒否 | ○ SimpleCallback → Failure |
| GetAuthorizationStatus | 取得失敗 | ○ json=null → JsonResult.Failure |
| OpenSettings | 開けない | ○ SimpleCallback → Failure |
| Show | パース失敗 / スケジューリング失敗 | ○ SimpleCallback → Failure |
| Update | 見つからない / パース失敗 | ○ SimpleCallback → Failure |
| Schedule | パース失敗 / スケジューリング失敗 | ○ SimpleCallback → Failure |
| GetScheduled | 取得失敗 | ○ json=null → JsonResult.Failure |
| GetDelivered | 取得失敗 | ○ json=null → JsonResult.Failure |
| RegisterCategory | パース失敗 | ○ SimpleCallback → Failure |
| RemoveCategory | 見つからない | ○ SimpleCallback → Failure |
| SetBadgeCount | 設定失敗 | ○ SimpleCallback → Failure |
| HasPermission | — | ○ BoolCallback（エラーなし） |
| Cancel 系 / Remove 系 | — | ○ fire-and-forget |

### 3.2 コールバック返却仕様反映

- `MacNotificationResult.Failure(operation, errorCode, errorMessage)` で errorCode・errorMessage を保持
- `MacNotificationJsonResult.Failure(operation, errorCode, errorMessage)` で同様に保持
- `json != null` を成功判定条件として使用（Bridge 仕様に準拠）

### 3.3 success 時契約

- `MacNotificationResult.Success`: `IsSuccess=true, ErrorCode=0, ErrorMessage=null` を保証（コンストラクタで固定値）
- `MacNotificationJsonResult.Success`: `IsSuccess=true, ErrorCode=0, ErrorMessage=null, Json=非null` を保証

## 4. ビルド結果

- 実行コマンド: Unity Test Runner（EditMode）
- 結果: 手動確認が必要（Unity Editor を CLI から操作できないため）
- 補足: C# 構文エラーは目視確認済み。型参照・名前空間は既存パターンに準拠

## 5. テスト結果

- 実行したテスト: EditMode テスト（Unity Test Runner で実行が必要）
- 結果サマリー:
  - 実行件数: 要Unity Editor実行（14 テストケース実装済み）
  - 成功: 未確認
  - 失敗: 未確認
- 未実施項目:
  - EditMode テスト実行: Unity Editor の起動が必要（手動確認）
  - PlayMode テスト（Singleton / コールバック経路）: 未実装（要別タスク）
  - 手動確認項目: macOS 実機が必要

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 | 備考 |
|---|---|---|---|---|
| MacNotificationResult.Success | MacNotificationTests.cs | Success_SetsCorrectFields | - | 要Unity実行 |
| MacNotificationResult.Failure | MacNotificationTests.cs | Failure_SetsCorrectFields | - | 要Unity実行 |
| MacNotificationJsonResult.Success | MacNotificationTests.cs | Success_SetsCorrectFields | - | 要Unity実行 |
| MacNotificationJsonResult.Failure | MacNotificationTests.cs | Failure_SetsNullJson | - | 要Unity実行 |
| AuthorizationStatus パース（5種） | MacNotificationTests.cs | Parse_AllKnownStatuses | - | 要Unity実行 |
| AuthorizationStatus 未知文字列 | MacNotificationTests.cs | Parse_UnknownString_ReturnsUnsupported | - | 要Unity実行 |
| AuthorizationStatus JSON パース | MacNotificationTests.cs | ParseJson_ExtractsStatusFromJson | - | 要Unity実行 |
| BuildContentJson 必須フィールド | MacNotificationTests.cs | BuildContentJson_RequiredFields | - | 要Unity実行 |
| BuildContentJson オプションフィールド | MacNotificationTests.cs | BuildContentJson_OptionalFields | - | 要Unity実行 |
| BuildTimeIntervalTriggerJson "seconds"キー | MacNotificationTests.cs | BuildTimeIntervalTriggerJson_UsesSecondsKey | - | 要Unity実行 |
| BuildCalendarTriggerJson | MacNotificationTests.cs | BuildCalendarTriggerJson_IncludesDateComponents | - | 要Unity実行 |
| BuildCategoryJson "id"キー | MacNotificationTests.cs | BuildCategoryJson_UsesIdKey_NotIdentifier | - | 要Unity実行 |
| BuildCategoryJson textInputPlaceholder | MacNotificationTests.cs | BuildCategoryJson_TextInputAction_IncludesPlaceholder | - | 要Unity実行 |
| BuildCategoryJson actions 省略 | MacNotificationTests.cs | BuildCategoryJson_EmptyActions_OmitsActionsField | - | 要Unity実行 |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
|---|---|---|---|
| Singleton 生成 / DontDestroyOnLoad | — | PlayMode テスト | 未実装。Manager は #if UNITY_STANDALONE_OSX のため |
| コールバック→Dispatcher→イベント経路 | — | PlayMode テスト | 未実装。モック native が必要 |
| 全手動確認項目 | — | — | macOS 実機・Standalone ビルドが必要 |

## 6. Definition of Done

- ○ Bridge API 21 関数すべての DllImport 実装
- ○ 5 種コールバック delegate の IL2CPP/AOT 準拠実装
- ○ SimpleCallback 系: per-call callback + global event の両方を提供
- ○ JsonCallback / BoolCallback 系: per-call callback のみ（global event なし）
- ○ fire-and-forget API: イベント発火なし
- ○ GC 防止: persistent delegate と per-operation delegate を static readonly に格納
- ○ スレッド契約: 全コールバックを UnityMainThreadDispatcher 経由でメインスレッドに転送
- ○ 例外処理: 全 MonoPInvokeCallback を try-catch で保護
- ○ compile guard: Manager は UNITY_STANDALONE_OSX のみ。他は UNITY_STANDALONE_OSX || UNITY_EDITOR
- ○ エラー契約: IsSuccess==true ↔ ErrorCode==0 かつ ErrorMessage==null
- △ EditMode テスト: 実装済み、Unity Editor での実行確認が必要
- × PlayMode テスト: 未実装
- × 手動確認: 実機未確認

## 7. ステップ7 実行確認

- 提示文:
  - 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して次工程へ進む
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答
