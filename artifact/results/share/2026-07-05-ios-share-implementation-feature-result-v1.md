# 実装結果レポート

## 基本情報

- 日付: 2026-07-05
- 機能名: share
- 対象プラットフォーム: iOS
- ブランチ: feature/UNT-6

参照計画書: `artifact/designs/share/2026-07-05-ios-share-design-v5.md`（v1〜v5 のレビュー反映を経て確定）

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

- 計画書のコード例をそのまま反映し、追加のロジック判断は発生しなかった。
- `IosShareManager.Share` の入力ガード順序（null/items空 → native呼び出し/非iOS判定）は計画書 §5.4 の
  手順どおりに実装。
- `IosShareItem.Text/Url/Image/File` の生成ヘルパは計画書 §5.2 で「任意だが推奨」とされていたため実装した。

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosSharePayloads.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareJsonBuilder.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/AssemblyInfo.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosShareJsonBuilderTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosShareManagerDispatchTests.cs`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/ShareResultTests.cs`
  （`IosShareResult` のファクトリ/不変条件テスト 4 件を追加）

### 2.3 非変更（対象だが未変更）

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Common/UnityMainThreadDispatcher.cs`: 既存実装をそのまま再利用。
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareOperationResult.cs` /
  `ShareCallbackResult.cs` / `ShareChooserActionResult.cs`: Android 用のため変更なし。
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/NativeToolkit.Runtime.asmdef`:
  計画書 §2.2 のとおり asmdef フィールド自体の変更は不要（`AssemblyInfo.cs` の
  `InternalsVisibleTo` 属性で対応）。
- サンプル UI（`IosShareManagerExampleController` 等）: 計画書の対象外（`design-sample-scene` で別途設計）。

## 3. エラー契約反映

### 3.1 エラーケース実装反映

| 層 | ケース | 反映状況 |
| -- | ------ | -------- |
| parser（native） | JSON構文エラー / `items`不在 | ネイティブ側の責務。C#側は未実装（再実装しない方針どおり） |
| parser（native） | 未知type / value欠落 | ネイティブ側で無視。`IosShareJsonBuilder`はtype値を検証せずそのまま出力（テストで確認） |
| use case（native） | 各種`ShareError`文言 | ネイティブ側の責務。C#側は`errorMessage`をそのまま`IosShareResult.Failure`に格納 |
| C# Bridge | `payload==null` / `items`null・空 | `IosShareManager.Share`で実装。`"No shareable items were provided."`を即時Failure |
| C# Bridge | 非iOS / Editor | `IosShareManager.Share`で実装。`"iOS share is only available on an iOS device."`を即時Failure |
| C# Bridge | `shareContent`呼び出し時の例外 | `try/catch`で`"Internal error: {ex.Message}"`に変換（実機ガード内、EditModeでは経路に到達しないため未検証・要実機確認） |

### 3.2 コールバック返却仕様反映

- `IosShareResult`は`(IsSuccess, Completed, ActivityType, ErrorMessage)`の4値構造で実装し、
  ネイティブコールバックの`(isSuccess, completed, activityType, errorMessage)`と1:1対応。
- dispatch順序は計画書どおり「共通イベント(`ShareCompleted`) → 個別callback(`onResult`)」を
  `IosShareManager.InvokeInOrder`に実装し、EditModeテストで順序を直接検証済み。

### 3.3 success 時契約

- `IsSuccess == true`のとき`ErrorMessage == null`を満たすこと: `IosShareResult`のファクトリメソッド
  (`Success`/`Failure`)で構造的に保証。`ShareResultTests.IosShareResult_Success_ErrorMessageIsNull_Invariant`
  で確認済み（成功）。

## 4. ビルド結果

- 実行コマンド:
  ```
  /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -projectPath <repo> \
    -runTests -testPlatform EditMode -testResults editmode-results.xml -logFile editmode-run.log
  ```
- 結果: SUCCESS
- 補足ログ:
  - コンパイルエラー（`error CS`）: 0件
  - コンパイル警告（`warning CS`）: 0件
  - プロジェクト全体のコンパイルが正常終了し、Editor（`UNITY_EDITOR`）ビルドで
    `IosShareManager`（`#if UNITY_IOS || UNITY_EDITOR`）と native P/Invoke 部
    （`#if UNITY_IOS && !UNITY_EDITOR`）の guard 分離が意図どおり機能することを確認
    （Editor 実行時に native シンボル未解決エラーが発生しない）。

## 5. テスト結果

- 実行したテスト: EditMode（NUnit / Unity Test Runner）全件
- 結果サマリー:
  - 実行件数: 108（プロジェクト全体）/ うち新規・追加分 27
  - 成功: 108
  - 失敗: 0
- 失敗時の対応: 該当なし（失敗なし）
- 未実施項目:
  - PlayMode（`UnityTest`コルーチンでのManager統合テスト。計画書 §7.2、§5.6で「必要に応じて」の任意項目）: 未実装。
    理由: EditModeの`IosShareManagerDispatchTests`で`InvokeInOrder`（dispatch順序・例外握りつぶし）を、
    `ShareResultTests`で`IosShareResult`契約を、それぞれ直接検証済みであり、計画書が要求する
    「Manager経由の入力ガード・非iOS Failure・last-registered wins」の検証は
    `UnityMainThreadDispatcher`のフレーム進行が必要なPlayMode環境でのみ可能なため、
    今回のEditMode実行では未実施。追加実装が必要な場合はユーザー確認のうえ着手する。
  - 手動確認（実機iOS 18+、計画書 §7.3）: 未実施。実機・Xcodeビルド環境が必要なため要検証として残す。

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
| dispatch順序 | IosShareManagerDispatchTests.cs | InvokeInOrder_CommonThenPerCall_InvokedOnceEachInOrder | ○ | 共通→個別の順序を記録して確認 |
| dispatch結果伝搬 | IosShareManagerDispatchTests.cs | InvokeInOrder_PassesResultToBothDelegates | ○ | - |
| dispatch null許容 | IosShareManagerDispatchTests.cs | OnlyPerCall / OnlyCommon / BothNull_DoesNotThrow | ○ | 3件 |
| 例外握りつぶし | IosShareManagerDispatchTests.cs | InvokeInOrder_PerCallThrows_CommonAlreadyInvokedAndExceptionSwallowed | ○ | LogAssertでDebug.LogErrorを許容しつつ、共通側は既発火済みを確認 |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
| ---------- | -------------- | ------------ | ---------- |
| Manager経由の入力ガード（payload null/items空） | - | Manager統合PlayMode | UnityMainThreadDispatcherのフレーム進行が必要なPlayMode環境が必要。今回未着手 |
| Manager経由の非iOS Failure経路 | - | Manager統合PlayMode | 同上 |
| 連続呼び出しでのlast-registered wins | - | Manager統合PlayMode | 同上 |
| 共有シート提示・キャンセル・エラー実機挙動 | - | 実機手動確認 | 実機iOS 18+ + Xcodeビルド環境が必要 |
| `shareContent`呼び出し時のtry/catch経路 | - | 実機 or PlayMode | Editor経路（`#else`）では到達しない実機ガード内のコードパス |

## 6. Definition of Done

- 判定基準:
  - ○: 実装・コード・テスト確認の範囲では OK
  - △: 一部 OK だが、追加確認が必要
  - ×: 未達
  - -: 対象外
- ○ 実装対象API（`IosShareManager.Share`、`ShareCompleted`イベント）を計画書どおり実装
- ○ Manager+Bridgeパターン準拠（platform guard戦略 `UNITY_IOS || UNITY_EDITOR` / `UNITY_IOS && !UNITY_EDITOR` を含む）
- ○ エラーケース一覧の反映（C# Bridge層はテスト確認済み、native層は再実装対象外）
- ○ スレッド契約（UnityMainThreadDispatcher経由）・メモリ契約（IL2CPP自動marshal、追加のAllocHGlobal不要）の実装反映
- ○ EditModeテスト（JsonBuilder / IosShareResult / dispatchシーム）全件成功
- △ PlayMode（Manager統合）テストは未実装。次工程で要否判断
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
