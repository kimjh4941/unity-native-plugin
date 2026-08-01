# 実装結果レポート

## 基本情報

- 日付: 2026-07-26
- 機能名: clipboard
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-8
- 実装計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md`（review-document で3回レビュー済み、承認水準）

## 1. 実装サマリー

### 1.1 native-toolkit 確認由来の実装（計画書由来、そのまま反映）

- `android.unity.clipboard.UnityAndroidClipboardManager` の14メソッドを計画どおり配線した
  - 同期3種（`read` / `hasClip` / `getDescription`）→ C# 側も戻り値を返す同期メソッド（`Read` / `HasClip` / `GetDescription`）にし、event を発火しない
  - 非同期6種（`copyPlainText` / `copyHtmlText` / `copyUri` / `copyMultipleText` / `clear` / `stopObserving`）→ 共通 event（`ClipboardOperationCompleted`）+ per-call callback
  - `startObserving` → 結果通知なしの専用経路（`StartObserving`、callback 引数なし）
- listener 2種（`ClipboardOperationListener` / `ClipboardChangeListener`）を `AndroidJavaProxy` で受信し、`Initialize` で常時登録、`OnDestroy` で解除
- エラーコード7種（`CLIPBOARD_EMPTY_CONTENT` 等）を JSON パーサでそのまま透過
- `copyPlainText` の blank 許容（設計 1.10 の判定根拠どおり）を C# 側でバリデーションしない実装にした
- 同梱 AAR（1.3.0）に clipboard 実装が含まれることを計画時に確認済みのため、AAR 差し替えは行っていない

### 1.2 実装時の追加判断

- `Read` / `HasClip` / `GetDescription` の operation 名定数（`read` / `hasClip` / `getDescription`）は計画の公開定数一覧に無かったため、`private const` として追加した（`_pendingOperationCallbacks` のキーには使わないため public にする理由がなく、既存 `AndroidShareManager` にも同種の private 定数は無いが、マジックストリングの重複を避けるための最小限の追加）
- `Read()` / `GetDescription()` の Bridge 利用不可時エラーコードとして計画どおり `CLIPBOARD_BRIDGE_UNAVAILABLE` を採用した
- テスト実行時に判明した1点のみ計画から逸脱: `AndroidClipboardJsonParserTests` の null/blank/不正 JSON 系6ケースで、パーサ内部の `Debug.LogError` が Unity Test Runner 上で「未処理のエラーログ」として扱われテストが自動失敗した。`LogAssert.Expect` を該当6テストに追加して対応（パーサ実装・計画のログ方針は変更していない）

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/ClipboardOperationResult.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/ClipboardReadResult.cs`（`ClipboardReadStatus` enum、`ClipItem`、`ClipContents` を含む）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/ClipboardDescriptionResult.cs`（`ClipDescriptionInfo` を含む）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardPayloads.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardJsonBuilder.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardJsonParser.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardJsonBuilderTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardJsonParserTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardManagerDispatchTests.cs`

`.meta` ファイルは Unity が自動生成したもの（`Clipboard.meta` および各 `.cs.meta`）で、AI エージェントは作成していない。

### 2.2 既存変更

なし。計画どおり既存 Runtime / Tests ファイルへの変更は発生していない。

### 2.3 非変更（対象だが未変更）

- `Runtime/Common/UnityMainThreadDispatcher.cs`: 既存のまま流用
- `Runtime/NativeToolkit.Runtime.asmdef`: 追加参照不要（`UnityEngine` のみで完結）
- `Runtime/AssemblyInfo.cs`: `InternalsVisibleTo("NativeToolkit.Runtime.Tests")` が設定済みで、`internal` 型（`AndroidClipboardJsonParser`、読み取り結果型の internal factory）をテストから直接検証できた
- `Plugins/Android/*.aar`: clipboard 実装が同梱済み（設計 1.9）のため差し替えなし
- `Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`: 追加参照不要

## 3. エラー契約反映

### 3.1 エラーケース実装反映

計画 6.1〜6.4 の全ケースをコードへ反映した。

- parser 層（6.1）: 必須キー欠落は native 側の挙動のため C# 側実装は無し。JsonBuilder が必須キーを常に出力することで回避（`AndroidClipboardJsonBuilderTests` で検証）
- use case / repository 層（6.2）: エラーコード7種すべてを `AndroidClipboardJsonParser` が透過。「エラーにならないケース」（blank plain text 等）は C# 側で独自バリデーションを追加していない
- C# Bridge 層（6.3）: 非 Android / 未初期化 / activity null / `Call` 例外の4パターンを非同期6種・同期3種・`StartObserving` それぞれで反映。`StartObserving` は計画どおりログのみで event / callback を発火しない
- Unity 側で検知できないケース（6.4）: `StartObserving` の native 側 no-op、`HasClip` の内部失敗が `false` と区別不能である点は XML コメントに明記

### 3.2 コールバック返却仕様反映

- `ClipboardOperationCompleted`（共通 event）→ per-call callback の順で dispatch する `InvokeInOrder` を `internal static` の純粋関数として実装し、`AndroidClipboardManagerDispatchTests` で順序・例外分離を検証
- `_pendingOperationCallbacks`（`Dictionary<string, Action<ClipboardOperationResult>?>`）による operation 単位の last-registered wins は計画どおり実装

### 3.3 success 時契約

- `isSuccess == true` のとき `errorMessage == null` を保証: `ClipboardOperationResult.Success` に private コンストラクタで固定し、`AndroidClipboardManagerDispatchTests.ClipboardOperationResult_Success_ErrorMessageIsNull_Invariant` で検証済み

## 4. ビルド結果

- 実行コマンド:
  - `Unity -batchmode -nographics -runTests -projectPath . -testPlatform EditMode -testResults <path> -logFile <path>`（macOS ビルドターゲット、既定）
  - `Unity -batchmode -nographics -runTests -projectPath . -testPlatform EditMode -buildTarget Android -testResults <path> -logFile <path>`（Android ビルドターゲットで `AndroidClipboardManager.cs` 本体と `AndroidClipboardManagerDispatchTests.cs` をコンパイル・実行するため追加実施）
- 結果: SUCCESS（両ビルドターゲットとも `error CS` 0件）
- 補足:
  - `AndroidClipboardManager.cs` は `#if UNITY_ANDROID` ガードのため、既定の macOS ビルドターゲットでは非コンパイル。Android ビルドターゲットに切り替えて再実行し、コンパイルとテスト実行の両方を確認した
  - テスト実行のためのビルドターゲット切り替えによる `unity-native-plugin.slnx` の意図しない差分（`NativeToolkit.BuildProcessors.Editor.csproj` 参照の削除）が発生したため、`git checkout -- unity-native-plugin.slnx` で元に戻した。ProjectSettings 側の恒久的な変更は発生していない（`git status` で確認済み）

## 5. テスト結果

- 実行したテスト: EditMode 全件（Unity Test Runner、`NativeToolkit.Runtime.Tests` アセンブリ）
- 結果サマリー（macOS ビルドターゲット）:
  - 実行件数: 197
  - 成功: 197
  - 失敗: 0
- 結果サマリー（Android ビルドターゲット、`AndroidClipboardManager` 本体を含む）:
  - 実行件数: 204
  - 成功: 204
  - 失敗: 0
- 失敗時の対応:
  - 初回実行で `AndroidClipboardJsonParserTests` の6ケース（null / blank / 不正JSON × ParseReadResult / ParseDescriptionResult）が失敗。原因は `Debug.LogError` が Unity Test Runner に「未処理のエラーログ」として検出されたため。`LogAssert.Expect` を追加して修正し、再実行で全件成功を確認した
- 未実施項目:
  - PlayMode テスト: 計画 7.2 のとおり本機能では追加していない（ネイティブ Bridge 依存部分は手動確認に委ねる方針）
  - 実機での手動確認（計画 7.3、全18項目）: 実機環境がないため未実施。詳細は 5.2 参照

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 | 備考 |
|---|---|---|---|---|
| 送信JSON: copyPlainText | AndroidClipboardJsonBuilderTests.cs | BuildCopyPlainTextJson_* (5件) | ○ | blank text も必須キーとして出力されることを検証 |
| 送信JSON: copyHtmlText | AndroidClipboardJsonBuilderTests.cs | BuildCopyHtmlTextJson_* (3件) | ○ | |
| 送信JSON: copyUri | AndroidClipboardJsonBuilderTests.cs | BuildCopyUriJson_* (2件) | ○ | |
| 送信JSON: copyMultipleText | AndroidClipboardJsonBuilderTests.cs | BuildCopyMultipleTextJson_* (4件) | ○ | 配列順序保持を検証 |
| 送信JSON: エスケープ | AndroidClipboardJsonBuilderTests.cs | *SpecialCharacters* / *Unicode* (2件) | ○ | |
| 受信JSON: ParseReadResult 正常系 | AndroidClipboardJsonParserTests.cs | ParseReadResult_ContentWithAllFields / MissingHtmlTextAndUri / MissingItemsAndMimeTypes (3件) | ○ | 欠落値のnull正規化・空配列正規化を検証 |
| 受信JSON: ParseReadResult Empty | AndroidClipboardJsonParserTests.cs | ParseReadResult_NullSentinel_ReturnsEmpty | ○ | |
| 受信JSON: ParseReadResult エラー封筒 | AndroidClipboardJsonParserTests.cs | ParseReadResult_ErrorEnvelope_* (7件、全エラーコード) | ○ | |
| 受信JSON: ParseReadResult 異常系 | AndroidClipboardJsonParserTests.cs | ParseReadResult_NullRaw / BlankRaw / InvalidJson (3件) | ○ | LogAssert.Expect 追加後に成功 |
| 受信JSON: ParseDescriptionResult 正常系 | AndroidClipboardJsonParserTests.cs | ParseDescriptionResult_AllFields / IsStyledText / ClassificationStatus / MissingClassificationStatus / MissingMimeTypes / MissingLabel (8件) | ○ | |
| 受信JSON: ParseDescriptionResult Empty | AndroidClipboardJsonParserTests.cs | ParseDescriptionResult_NullSentinel_ReturnsEmpty | ○ | |
| 受信JSON: ParseDescriptionResult エラー封筒 | AndroidClipboardJsonParserTests.cs | ParseDescriptionResult_ErrorEnvelope_* (7件、全エラーコード) | ○ | |
| 受信JSON: ParseDescriptionResult 異常系 | AndroidClipboardJsonParserTests.cs | ParseDescriptionResult_NullRaw / BlankRaw / InvalidJson (3件) | ○ | LogAssert.Expect 追加後に成功 |
| 受信JSON: ParseHasClip | AndroidClipboardJsonParserTests.cs | ParseHasClip_* (5件) | ○ | |
| dispatch順序 | AndroidClipboardManagerDispatchTests.cs | InvokeInOrder_* (6件) | ○ | Android ビルドターゲットでのみ実行対象（`#if UNITY_ANDROID`） |
| success契約 | AndroidClipboardManagerDispatchTests.cs | ClipboardOperationResult_Success_ErrorMessageIsNull_Invariant | ○ | 同上 |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
|---|---|---|---|
| 実機手動確認（計画7.3、全18項目） | - | プレーンテキストコピー/blank、HTMLコピー/blank、URIコピー、複数テキスト、機微フラグ、読み取り/空読み取り、メタデータ、有無判定、監視/監視停止/二重開始、バックグラウンド読み取り、ログ安全性、ライフサイクル | Android実機・エミュレータ環境が本セッションにないため未実施。要検証事項9.3（`stopObserving`の0引数JNI呼び出し）も実機確認が必要 |
| PlayMode テスト | - | - | 計画7.2のとおり本機能では対象外（既存Share実装のPlayModeテストもiOS/macOSのみでAndroid相当は無し） |

## 6. Definition of Done

判定基準: ○ 実装・コード・テスト確認の範囲では OK / △ 一部OKだが追加確認が必要 / × 未達 / - 対象外

- ○ 4.1の新規ファイルが全て作成されている
- ○ AIエージェントが既存のRuntime/Tests実装を意図的に変更していない（.metaは自動生成、slnxの一時変更は元に戻した）
- ○ 同期3メソッドが戻り値を返し、eventを発火しない
- ○ 非同期6メソッドが共通event→per-call callbackの順でdispatchする
- ○ 失敗経路を含め、CallOperationの全経路がFireOperationResultを通る
- ○ StartObservingにcallback引数が無く、失敗時はログのみでreturnする
- ○ parserのDTOが[Serializable]+publicフィールドで定義されている
- ○ 結果型の生成方針が設計5.2の表どおり（読み取り結果型のconstructor/factoryがinternal）
- ○ Awaitable版を含まない
- ○ copyPlainTextのblankにC#独自のバリデーションを追加していない
- ○ 実装コードのXMLコメント・行コメント・ログ文言・エラー文言がすべて英語
- ○ クリップボード本文および生成JSONがどのログにも出力されていない
- ○ EditModeテストが全てpassed（macOS 197/197、Android 204/204）
- × 計画7.3の手動確認項目が実機で確認済み（実機環境なしのため未実施、5.2参照）

## 7. ステップ7 実行確認

- 提示文:
  - 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して次工程へ進む → review-implementation-feature スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答
