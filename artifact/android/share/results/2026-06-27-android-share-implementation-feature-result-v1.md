# 実装結果レポート

## 基本情報

- 日付: 2026-06-27
- 機能名: share
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-5

## 1. 実装サマリー

### 1.1 設計計画由来の実装

- `ShareChooserActionResult`（readonly struct、null→string.Empty 正規化）を新規作成
- `ShareChooserActionCallbackCoordinator`（ガードなし、injectable dispatch）を新規作成
- `AndroidShareManager` に `ShareChooserActionListenerProxy`（IL2CPP 対応 proxy）を追加
- `Initialize` に `setShareChooserActionListener` 呼び出しを try/catch degrade 付きで追加
- `OnDestroy` に `ClearShareChooserActionListener` 呼び出しを追加（listener 解除 → Dispose → coordinator Clear の順）
- `ShareText` に第3引数 `onChooserAction` を追加（coordinator.Register 経由、last-registered wins）
- `ShareWithCallback` に chooserActions 非空時の警告ログを追加
- `ShareText` に chooserActions 5件超の警告ログを追加
- `ShareChooserActionTapped` event を `add`/`remove` で coordinator の event に委譲

### 1.2 実装時の追加判断

- **coordinator のテスト件数**: 設計計画の6ケースに加え、`Clear()` と結果 ActionId の pass-through 検証の2ケースを追加（合計8ケース）。いずれも設計の意図と矛盾しない
- **`AndroidShareJsonBuilderTests` の追加テスト**: 計画ケース (10) は既存テスト `BuildShareTextJson_ChooserActionWithoutIntentAction_OmitsIntentAction` で担保済みのため追加なし。(9) と (11) のみ追加

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionCallbackCoordinator.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/ShareChooserActionCallbackCoordinatorTests.cs`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs`
  - `ShareChooserActionListenerProxy` inner class 追加
  - `chooserActionListener` / `_chooserCoordinator` フィールド追加
  - `ShareChooserActionTapped` event 追加（coordinator 委譲）
  - `Initialize`: `setShareChooserActionListener` try/catch degrade
  - `OnDestroy`: `ClearShareChooserActionListener` 挿入 → `_chooserCoordinator.Clear()` 追加
  - `ShareText`: 第3引数追加、5件超警告ログ、coordinator.Register 呼び出し
  - `ShareWithCallback`: chooserActions 非空時警告ログ
  - `FireChooserAction` / `ClearShareChooserActionListener` メソッド追加
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/ShareResultTests.cs`
  - `ShareChooserActionResult` の3テストケース追加（値保持・null 正規化・empty 正規化）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidShareJsonBuilderTests.cs`
  - intentAction 含む（9）・複数 action 順序（11）の2テストケース追加
  - `using System;` 追加（StringComparison.Ordinal 使用のため）

### 2.3 非変更（対象だが未変更）

- `Runtime/Share/AndroidSharePayloads.cs`: `ChooserActionPayload.intentAction` で対応済み
- `Runtime/Share/AndroidShareJsonBuilder.cs`: chooserActions シリアライズ済み
- `Runtime/Share/ShareOperationResult.cs` / `ShareCallbackResult.cs`: 既存のまま
- `Runtime/Common/UnityMainThreadDispatcher.cs`: 流用のみ
- `Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`: `NativeToolkit.Runtime` 参照済み、`InternalsVisibleTo` 不要（coordinator は public）
- `Plugins/Android/*.aar`: chooser action 対応版差し替えは別タスク（依存）

## 3. エラー契約反映

### 3.1 エラーケース実装反映

| 層 | 条件 | 反映状況 |
|----|------|---------|
| normalize | intentAction が空 / SEND | native 側が除外（Unity 側は無関与）— 実装範囲外 |
| registry | API 34 未満 | native 側で no-op — 実装範囲外 |
| registry | 登録失敗 | native 側でログのみ — 実装範囲外 |
| C# Bridge | pluginInstance == null 等 | 既存 `TryPrepareCall` が Failure を返す — 変更なし（既存担保） |
| C# Bridge | proxy 内例外 | native 側で catch — proxy は薄く保持 |
| C# Bridge | setShareChooserActionListener 不存在 | Initialize の try/catch で degrade、警告ログのみ — 実装済み |
| coordinator | subscriber 例外 | Fire 内の try/catch で継続 — 実装済み |

### 3.2 コールバック返却仕様反映

- chooser action コールバックは成否を持たない通知（`actionId` のみ）— `ShareChooserActionResult` は `IsSuccess` を持たない設計のまま
- share 起動の成否は従来どおり `ShareOperationCompleted`（`OperationShareText`）で受ける

### 3.3 success 時契約

- `ShareChooserActionResult` は `ActionId` のみ（成否なし）のため、本契約は対象外

## 4. ビルド結果

- 実行コマンド: `Unity -batchmode -runTests -testPlatform EditMode`
- 結果: 要手動確認（Unity がプロジェクトを既に開いているためバッチモード不可）
- 補足: コードの静的確認（型・シグネチャ・using・asmdef 参照）ではエラーなし

## 5. テスト結果

- 実行方法: Unity Test Runner（Edit Mode）で手動実行が必要
- 実行件数: 自動実行不可
- 未実施理由: Unity インスタンスがプロジェクトを開いているためバッチモードが abort

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 | 備考 |
|-----------|--------------|-------------|------|------|
| Fire 順序（global → per-call） | ShareChooserActionCallbackCoordinatorTests | Fire_GlobalEventFiredBeforePerCallCallback | △ | 手動確認必要 |
| per-call 未登録で global 発火 | ShareChooserActionCallbackCoordinatorTests | Fire_NoPerCallCallback_GlobalEventStillFires | △ | 手動確認必要 |
| last-registered wins | ShareChooserActionCallbackCoordinatorTests | Register_LastRegisteredWins_OnlyLatestCallbackFires | △ | 手動確認必要 |
| Register(null) でクリア | ShareChooserActionCallbackCoordinatorTests | Register_Null_ClearsPreviousCallback | △ | 手動確認必要 |
| 複数回 Fire で per-call 複数発火 | ShareChooserActionCallbackCoordinatorTests | Fire_MultipleTimes_PerCallCallbackInvokedEachTime | △ | 手動確認必要 |
| global 例外でも per-call 継続 | ShareChooserActionCallbackCoordinatorTests | Fire_GlobalEventThrows_PerCallCallbackStillInvoked | △ | 手動確認必要 |
| Clear 後 Fire で per-call 不発 | ShareChooserActionCallbackCoordinatorTests | Clear_SubsequentFireDoesNotInvokeCallback | △ | 手動確認必要 |
| Fire が結果を正しく渡す | ShareChooserActionCallbackCoordinatorTests | Fire_PassesResultToSubscribers | △ | 手動確認必要 |
| ActionId 値保持 | ShareResultTests | ShareChooserActionResult_WithActionId_StoresValue | △ | 手動確認必要 |
| null → string.Empty 正規化 | ShareResultTests | ShareChooserActionResult_NullActionId_NormalizedToEmpty | △ | 手動確認必要 |
| empty ActionId 保持 | ShareResultTests | ShareChooserActionResult_EmptyActionId_StoredAsEmpty | △ | 手動確認必要 |
| intentAction 含む JSON | AndroidShareJsonBuilderTests | BuildShareTextJson_ChooserActionWithIntentAction_IncludesIntentAction | △ | 手動確認必要 |
| 複数 action 順序維持 | AndroidShareJsonBuilderTests | BuildShareTextJson_MultipleChooserActions_PreservesOrder | △ | 手動確認必要 |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
|-----------|--------------|-------------|---------|
| Manager listener 登録 | — | Initialize で setShareChooserActionListener が呼ばれること | #if UNITY_ANDROID ガードのため EditMode 不可（coordinator で代替） |
| Manager proxy 受信 | — | onChooserAction → ShareChooserActionTapped / per-call の転送 | 同上（実機確認） |
| Manager OnDestroy 順序 | — | listener 解除 → Dispose → Clear の順 | 同上（コードレビューで担保） |

## 6. Definition of Done

| 項目 | 判定 |
|------|------|
| `ShareChooserActionResult`（null 正規化）と `ShareChooserActionCallbackCoordinator` を実装 | ○ |
| `AndroidShareManager` に `ShareChooserActionListenerProxy` / 共通イベント / per-call `onChooserAction` を配線 | ○ |
| listener 登録（Initialize、try/catch degrade）/ 解除（OnDestroy、正しい順序）が実装されている | ○ |
| per-call callback の置換・null クリア（last-registered wins）が実装されている | ○ |
| callback 転送順序（global → per-call）と例外安全が実装されている | ○ |
| `ShareWithCallback` の chooserActions 非対応が XML コメント + 警告ログで明示されている | ○ |
| public メンバの XML コメントに契約（API 34+ 限定 / ShareText 限定 / ShareWithCallback 不可 / 未タップ時は不発）を記載 | ○ |
| EditMode テスト（7.1）が全ケース通過する | △ Unity Test Runner 未実行（Unity 開いているためバッチモード不可）。coordinator 例外安全バグ修正後も要確認 |
| 未対応 AAR でも既存 share 機能が degrade しないことを設計・実装で担保 | ○ |
| （前提・依存スコープ外）chooser action 対応版 AAR への差し替え後に実機確認 | - 対象外 |

## 7. 実行確認

この実装結果を採用して、次工程へ進めますか？

- **実行する**: この実装結果を採用して終了 → review-implementation-feature スキルへ引き継ぐ
- **修正する**: 指摘内容を反映して再実装
- **キャンセル**: ここまでの修正差分は保持したまま、終了
