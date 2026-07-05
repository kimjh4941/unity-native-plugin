# 実装結果レポート (v2)

## 基本情報

- 日付: 2026-07-05
- 機能名: share
- 対象プラットフォーム: iOS
- ブランチ: feature/UNT-6

参照計画書: `artifact/designs/share/2026-07-05-ios-share-design-v5.md`（v1〜v5 のレビュー反映を経て確定）
参照レビュー: `artifact/reviews/share/2026-07-05-ios-share-implementation-feature-review-v1.md`

v1 からの変更点: レビュー指摘（medium 2件、low 1件）を反映。
(1) `IosShareResult.Failure` の null/空白 error 正規化、
(2) `IosShareManager.Share` の Manager 経由フルパスを検証する PlayMode 統合テストを追加、
(3) テスト実行ログの所在を明記。

## 1. 実装サマリー

### 1.1 native-toolkit 確認由来の実装

- C ABI `shareContent(const char* contentJson, ShareCallback callback)` と
  `ShareCallback(bool isSuccess, bool completed, const char* activityType, const char* errorMessage)` を
  計画書 §1.1 のとおり `[DllImport("__Internal")]` + `[UnmanagedFunctionPointer(Cdecl)]` +
  `[MonoPInvokeCallback]` で実装（`IosShareManager.cs`）。
- contentJson スキーマ（§1.2: `items[{type,value}]` 必須、`subject`/`previewTitle`/`excludedActivityTypes` 任意）を
  `IosShareJsonBuilder.BuildShareContentJson` に反映。
- ドメインエラー文言（§1.3）はネイティブ側の責務のため C# 側では未実装・未再実装（計画どおり）。
- 呼び出し方針（§1.4）: persistent 登録型コールバックを持たない一発呼び出し方式のため、
  `OnDestroy` でのネイティブコールバック解除処理は実装していない（計画どおり）。

### 1.2 実装時の追加判断

- 計画書のコード例をそのまま反映し、当初の追加ロジック判断は発生しなかった。
- `IosShareManager.Share` の入力ガード順序（null/items空 → native呼び出し/非iOS判定）は計画書 §5.4 の
  手順どおりに実装。
- `IosShareItem.Text/Url/Image/File` の生成ヘルパは計画書 §5.2 で「任意だが推奨」とされていたため実装した。
- **（v2・レビュー反映）** `IosShareResult.Failure(string? error)` は計画書に明記のない
  defensive 正規化として、null/空白 `error` を `"Unknown error."` に正規化するよう変更した。
  計画書 §5.5 の「`IsSuccess=false` のとき `ErrorMessage` 非 null を保証」という契約を、
  ファクトリメソッド自身で構造的に満たすための追加判断（レビュー medium #1 対応）。
- **（v2・レビュー反映）** `IosShareManager.Share()` の Manager 経由フルパス（`FireResult` →
  `UnityMainThreadDispatcher.Enqueue` → `ShareCompleted`/`onResult`）を検証する統合テストは
  `UnityMainThreadDispatcher.Update()` のフレーム進行を要するため、PlayMode 実行が必須と判明した。
  既存 `Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`（`includePlatforms: ["Editor"]`）は
  EditMode 専用として Unity Test Runner に認識されており、この制限を外すと既存 EditMode テスト
  107件が丸ごと検出されなくなる回帰を確認した（実機検証済み、後述）。そのため計画書にない
  追加判断として、**新規に `Tests/PlayMode/` ディレクトリと専用 asmdef
  `NativeToolkit.Runtime.PlayModeTests.asmdef` を作成し、統合テストをそこに分離**した
  （レビュー medium #2 対応）。既存 EditMode 資産には影響しない構成。
- **（v2）** 統合テストの実装中に、「非iOS/Editor経路は同期的に即時解決するため、
  `Share()` を連続呼び出ししても実際には競合しない」ことが判明した。last-registered-wins
  （`s_onShare` の上書き）は実機の非同期 native 経路でのみ意味を持つ挙動であり、
  Editor 上で「後勝ち」を検証しようとした当初のテストは前提が誤っていたため、
  「各呼び出しが自身のコールバックを1回ずつ発火する」ことを検証する内容に修正した。

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosSharePayloads.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareJsonBuilder.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/AssemblyInfo.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosShareJsonBuilderTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosShareManagerDispatchTests.cs`
- **（v2・計画書外・レビュー対応）** `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/NativeToolkit.Runtime.PlayModeTests.asmdef`
- **（v2・計画書外・レビュー対応）** `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosShareManagerIntegrationTests.cs`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/ShareResultTests.cs`
  - `IosShareResult` のファクトリ/不変条件テストを追加（v1: 4件）
  - **（v2）** `Failure(null)` / `Failure("   ")` の正規化テストを追加（2件）
- **（v2）** `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareResult.cs`:
  `Failure(string? error)` を `string.IsNullOrWhiteSpace(error) ? "Unknown error." : error` に変更。

### 2.3 非変更（対象だが未変更）

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Common/UnityMainThreadDispatcher.cs`: 既存実装をそのまま再利用。
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareOperationResult.cs` /
  `ShareCallbackResult.cs` / `ShareChooserActionResult.cs`: Android 用のため変更なし。
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/NativeToolkit.Runtime.asmdef`:
  計画書 §2.2 のとおり asmdef フィールド自体の変更は不要（`AssemblyInfo.cs` の
  `InternalsVisibleTo` 属性で対応）。
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`:
  **（v2）** 一時的に `includePlatforms` を空にする変更を検証したが EditMode 全件検出不能の
  回帰を確認したため revert し、最終的に無変更（`["Editor"]` のまま）。PlayMode 対応は
  新規の別 asmdef（2.1参照）で行った。
- サンプル UI（`IosShareManagerExampleController` 等）: 計画書の対象外（`design-sample-scene` で別途設計）。

## 3. エラー契約反映

### 3.1 エラーケース実装反映

| 層 | ケース | 反映状況 |
| -- | ------ | -------- |
| parser（native） | JSON構文エラー / `items`不在 | ネイティブ側の責務。C#側は未実装（再実装しない方針どおり） |
| parser（native） | 未知type / value欠落 | ネイティブ側で無視。`IosShareJsonBuilder`はtype値を検証せずそのまま出力（テストで確認） |
| use case（native） | 各種`ShareError`文言 | ネイティブ側の責務。C#側は`errorMessage`をそのまま`IosShareResult.Failure`に格納（null/空白のみ`"Unknown error."`に正規化） |
| C# Bridge | `payload==null` / `items`null・空 | `IosShareManager.Share`で実装。`"No shareable items were provided."`を即時Failure。PlayMode統合テストでManager経由の発火を確認済み |
| C# Bridge | 非iOS / Editor | `IosShareManager.Share`で実装。`"iOS share is only available on an iOS device."`を即時Failure。PlayMode統合テストでManager経由の発火を確認済み |
| C# Bridge | `shareContent`呼び出し時の例外 | `try/catch`で`"Internal error: {ex.Message}"`に変換（実機ガード内、EditMode/PlayModeいずれのEditor実行でも経路に到達しないため未検証・要実機確認） |

### 3.2 コールバック返却仕様反映

- `IosShareResult`は`(IsSuccess, Completed, ActivityType, ErrorMessage)`の4値構造で実装し、
  ネイティブコールバックの`(isSuccess, completed, activityType, errorMessage)`と1:1対応。
- dispatch順序は計画書どおり「共通イベント(`ShareCompleted`) → 個別callback(`onResult`)」を
  `IosShareManager.InvokeInOrder`に実装し、EditModeテストで順序を直接検証済み。
  **（v2）** さらに `IosShareManager.Share()` を実際に呼び出す PlayMode 統合テストでも
  Manager 経由での順序（共通→個別）を確認済み。

### 3.3 success 時契約

- `IsSuccess == true`のとき`ErrorMessage == null`を満たすこと: `IosShareResult`のファクトリメソッド
  (`Success`/`Failure`)で構造的に保証。`ShareResultTests.IosShareResult_Success_ErrorMessageIsNull_Invariant`
  で確認済み（成功）。
- **（v2追加）** `IsSuccess == false`のとき`ErrorMessage != null`を満たすこと: `Failure(string? error)`が
  null/空白を`"Unknown error."`に正規化することで構造的に保証。
  `IosShareResult_Failure_NullError_NormalizesMessage` /
  `IosShareResult_Failure_WhitespaceError_NormalizesMessage` で確認済み（成功）。

## 4. ビルド結果

- 実行コマンド（EditMode）:
  ```
  /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -projectPath <repo> \
    -runTests -testPlatform EditMode \
    -testResults <scratchpad>/editmode-final.xml -logFile <scratchpad>/editmode-final.log
  ```
- 実行コマンド（PlayMode）:
  ```
  /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -projectPath <repo> \
    -runTests -testPlatform PlayMode \
    -testResults <scratchpad>/playmode-final.xml -logFile <scratchpad>/playmode-final.log
  ```
- 結果: SUCCESS（EditMode / PlayMode 両方）
- 補足ログ:
  - コンパイルエラー（`error CS`）: 0件（EditMode / PlayMode とも）
  - コンパイル警告（`warning CS`）: 0件
  - **（v2・low #1 対応）** テスト実行ログ・結果 XML の保存先はセッションの scratchpad ディレクトリ
    （`/private/tmp/claude-501/.../scratchpad/`）であり、リポジトリには含めていない
    （エフェメラルな実行成果物のため意図的に非コミット。再現手順は本レポートの実行コマンドを参照）。
  - `IosShareManager`（`#if UNITY_IOS || UNITY_EDITOR`）と native P/Invoke 部
    （`#if UNITY_IOS && !UNITY_EDITOR`）の guard 分離が意図どおり機能することを、
    EditMode・PlayMode 双方の Editor 実行（native シンボル未解決エラーなし）で確認。
  - asmdef の `includePlatforms` を空にする変更は EditMode 全件の検出不能という重大な回帰を
    引き起こすことを実機検証で確認し、最終的に不採用（3.節・2.3節参照）。

## 5. テスト結果

- 実行したテスト: EditMode + PlayMode（NUnit / Unity Test Runner）全件
- 結果サマリー:
  - EditMode 実行件数: 110（プロジェクト全体）/ うち新規・追加分 29
  - EditMode 成功: 110 / 失敗: 0
  - PlayMode 実行件数: 5（`NativeToolkit.Runtime.PlayModeTests.dll`、全て新規）
  - PlayMode 成功: 5 / 失敗: 0
- 失敗時の対応:
  - PlayMode 実装過程で `Share_ConsecutiveCalls_LastRegisteredCallbackWins` が1件失敗
    （前提誤り: Editor/非iOS経路は同期解決のため2回連続呼び出しでも競合しない）。
    テストを `Share_ConsecutiveCalls_EachCallbackFiresExactlyOnce` に修正し、
    「各呼び出しが自身のコールバックを1回ずつ発火する」ことを検証する内容に変更して解消。
  - 上記以外の失敗は発生していない。
- 未実施項目:
  - 手動確認（実機iOS 18+、計画書 §7.3）: 未実施。実機・Xcodeビルド環境が必要なため要検証として残す。
  - `shareContent`呼び出し時のtry/catch経路・native `OnShareResult`コールバック経路: 実機ガード内
    （`#if UNITY_IOS && !UNITY_EDITOR`）のため、Editor実行（EditMode/PlayMode）では到達不可。
  - 連続呼び出し時の真の last-registered-wins（native非同期経路での`s_onShare`上書き）:
    実機でのみ再現可能なため未検証。

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 | 備考 |
| ---------- | -------------- | ------------ | ---- | ---- |
| JSON生成（各type） | IosShareJsonBuilderTests.cs | BuildShareContentJson_TextItem_ExactJson 他3件（Url/Image/File） | ○ | 完全一致で検証 |
| JSON生成（複数items順序） | IosShareJsonBuilderTests.cs | BuildShareContentJson_MultipleItems_PreservesOrder | ○ | - |
| JSON生成（空items） | IosShareJsonBuilderTests.cs | BuildShareContentJson_EmptyItems_ProducesEmptyArray | ○ | - |
| null要素除外（defensive） | IosShareJsonBuilderTests.cs | BuildShareContentJson_NullItemInArray_IsExcluded | ○ | 前後の要素は保持されることを確認 |
| 空value素通し | IosShareJsonBuilderTests.cs | BuildShareContentJson_EmptyValue_PassesThroughAsIs | ○ | - |
| 未知type素通し | IosShareJsonBuilderTests.cs | BuildShareContentJson_UnknownType_PassesThroughAsIs | ○ | - |
| subject/previewTitle | IosShareJsonBuilderTests.cs | WithSubjectAndPreviewTitle_IncludesBoth / NullOrWhitespace...OmitsBoth | ○ | - |
| excludedActivityTypes | IosShareJsonBuilderTests.cs | WithExcludedActivityTypes_IncludesArray / Null.../Empty...OmitsField | ○ | 3件 |
| 特殊文字/制御文字エスケープ | IosShareJsonBuilderTests.cs | EscapesSpecialCharacters / EscapesControlCharacters | ○ | - |
| IosShareResult契約 | ShareResultTests.cs | IosShareResult_Success_Completed_StoresValues 他3件 | ○ | キャンセル非エラー・不変条件含む |
| **（v2）** Failure null/空白正規化 | ShareResultTests.cs | IosShareResult_Failure_NullError_NormalizesMessage / _WhitespaceError_NormalizesMessage | ○ | レビュー medium #1 対応 |
| dispatch順序 | IosShareManagerDispatchTests.cs | InvokeInOrder_CommonThenPerCall_InvokedOnceEachInOrder | ○ | 共通→個別の順序を記録して確認 |
| dispatch結果伝搬 | IosShareManagerDispatchTests.cs | InvokeInOrder_PassesResultToBothDelegates | ○ | - |
| dispatch null許容 | IosShareManagerDispatchTests.cs | OnlyPerCall / OnlyCommon / BothNull_DoesNotThrow | ○ | 3件 |
| 例外握りつぶし | IosShareManagerDispatchTests.cs | InvokeInOrder_PerCallThrows_CommonAlreadyInvokedAndExceptionSwallowed | ○ | LogAssertでDebug.LogErrorを許容しつつ、共通側は既発火済みを確認 |
| **（v2）** Manager経由: nullペイロード | IosShareManagerIntegrationTests.cs (PlayMode) | Share_NullPayload_FiresBothCallbacksWithNoShareableItemsFailure | ○ | 共通イベント・個別callback双方の発火を確認 |
| **（v2）** Manager経由: 空items | IosShareManagerIntegrationTests.cs (PlayMode) | Share_EmptyItems_FiresNoShareableItemsFailure | ○ | - |
| **（v2）** Manager経由: 非iOS/Editor Failure | IosShareManagerIntegrationTests.cs (PlayMode) | Share_NonIosPlatformOrEditor_FiresIosOnlyFailure | ○ | - |
| **（v2）** Manager経由: 連続呼び出し | IosShareManagerIntegrationTests.cs (PlayMode) | Share_ConsecutiveCalls_EachCallbackFiresExactlyOnce | ○ | 前提修正済み（本文1.2参照） |
| **（v2）** Manager経由: dispatch順序 | IosShareManagerIntegrationTests.cs (PlayMode) | Share_DispatchOrder_CommonEventFiresBeforePerCallCallback | ○ | - |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
| ---------- | -------------- | ------------ | ---------- |
| 共有シート提示・キャンセル・エラー実機挙動 | - | 実機手動確認 | 実機iOS 18+ + Xcodeビルド環境が必要 |
| `shareContent`呼び出し時のtry/catch経路 | - | 実機 | 実機ガード内（`#if UNITY_IOS && !UNITY_EDITOR`）のため Editor実行では到達不可 |
| native非同期経路での真のlast-registered-wins | - | 実機 | native `shareContent`呼び出しの非同期性が前提のため、Editor同期経路では再現不可 |

## 6. Definition of Done

- 判定基準:
  - ○: 実装・コード・テスト確認の範囲では OK
  - △: 一部 OK だが、追加確認が必要
  - ×: 未達
  - -: 対象外
- ○ 実装対象API（`IosShareManager.Share`、`ShareCompleted`イベント）を計画書どおり実装
- ○ Manager+Bridgeパターン準拠（platform guard戦略 `UNITY_IOS || UNITY_EDITOR` / `UNITY_IOS && !UNITY_EDITOR` を含む）
- ○ エラーケース一覧の反映（C# Bridge層はEditMode/PlayMode両方でテスト確認済み、native層は再実装対象外）
- ○ スレッド契約（UnityMainThreadDispatcher経由）・メモリ契約（IL2CPP自動marshal、追加のAllocHGlobal不要）の実装反映
- ○ EditModeテスト（JsonBuilder / IosShareResult / dispatchシーム）全件成功
- ○ **（v2）** PlayMode統合テスト（Manager経由フルパス）を追加し全件成功
- - 手動確認（実機iOS）は対象外（実機環境なし、要検証として明記）
- - サンプルUI実装は対象外（別途design-sample-scene）

## 7. ステップ7 実行確認

- 提示文:
  - 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して次工程へ進む
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答
