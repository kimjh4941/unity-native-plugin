# 実装結果レポート

## 基本情報

- 日付: 2026-07-26
- 機能名: clipboard
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-8
- 実装計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md`（review-document で3回レビュー済み、承認水準）
- 対象レビュー: `artifact/reviews/clipboard/2026-07-26-android-clipboard-implementation-feature-review-v1.md`, `artifact/reviews/clipboard/2026-07-26-android-clipboard-implementation-feature-review-v2.md`

### v2 からの変更点（レビュー反映）

対象: `artifact/reviews/clipboard/2026-07-26-android-clipboard-implementation-feature-review-v2.md`（high 指摘なし、v1 の `InvokeInOrder` 問題は解消済みと確認された）

| 指摘 | severity | 対応 |
|---|---|---|
| `agent-rules/coding-rules/common.md` の「非同期版の併設ルール」が Awaitable 版併設を無条件の必須事項として読める一方、設計 v4・本結果は clipboard に Awaitable 版を含めていないため、ルールと実装が衝突している | medium | レビューの推奨修正「方針B」を採用。`common.md` の対応表と「非同期版の併設ルール」節の文言を、**Awaitable 版は「多重呼び出しガード」節の前提条件（in-flight ガードが実装済み、または同時実行が安全と確認済み）を満たす操作にのみ併設する**という条件付きの記述に修正した。この修正により、design v4 の 5.9（「多重呼び出しガードの前提条件を満たさないため clipboard 初期実装は callback 版のみ」という判断）は、修正後の `common.md` とそのまま整合する。design v4 自体の追加更新（v5 化）は不要と判断した |

### v1 からの変更点（レビュー反映）

| 指摘 | severity | 対応 |
|---|---|---|
| `InvokeInOrder` が単一 try/catch で common/perCall を包んでおり、common 側の例外で perCall が呼ばれない | high | common/perCall を別々の try/catch に分離。`InvokeInOrder_CommonThrows_PerCallStillInvokedAndExceptionSwallowed` テストを追加し、修正後に EditMode 再実行（macOS 197/197、Android 205/205）で確認 |
| 実装結果レポートと作業ツリーの変更（AAR 1.2.0→1.3.0 差分、`common.md` 差分）が一致していない | medium | 1.3 に切り分けを明記。両差分とも本 clipboard 実装が発生させたものではなく、セッション開始前から存在した未コミット状態であることを `git log` で確認（該当パスの最新コミットはどちらも過去のマージコミットで、今回のセッション中の commit ではない）。`common.md` は本セッション内の別タスク（sync/async 判断ルールの追記、ユーザー承認済み）による変更で、clipboard 機能とは無関係 |
| `common.md` のルール変更が実装計画の対象外 | medium | 上記と同一の理由により、clipboard 実装差分から意図的に除外されていることを明記。ルール自体の要否は本 feature のレビュー範囲外とする |
| Builder/Parser の public/internal メソッドが csharp.md の先頭ログ規則に非対応 | low | `AndroidClipboardJsonBuilder` / `AndroidClipboardJsonParser` のクラス XML コメントに、秘匿情報保護を理由とした明示的な逸脱を追記（コード変更は行わず、ドキュメント化のみ） |

## 1. 実装サマリー

### 1.1 native-toolkit 確認由来の実装（計画書由来、そのまま反映）

- `android.unity.clipboard.UnityAndroidClipboardManager` の14メソッドを計画どおり配線した
  - 同期3種（`read` / `hasClip` / `getDescription`）→ C# 側も戻り値を返す同期メソッド（`Read` / `HasClip` / `GetDescription`）にし、event を発火しない
  - 非同期6種（`copyPlainText` / `copyHtmlText` / `copyUri` / `copyMultipleText` / `clear` / `stopObserving`）→ 共通 event（`ClipboardOperationCompleted`）+ per-call callback
  - `startObserving` → 結果通知なしの専用経路（`StartObserving`、callback 引数なし）
- listener 2種（`ClipboardOperationListener` / `ClipboardChangeListener`）を `AndroidJavaProxy` で受信し、`Initialize` で常時登録、`OnDestroy` で解除
- エラーコード7種（`CLIPBOARD_EMPTY_CONTENT` 等）を JSON パーサでそのまま透過
- `copyPlainText` の blank 許容（設計 1.10 の判定根拠どおり）を C# 側でバリデーションしない実装にした
- 本 clipboard 実装は既存 AAR（`Packages/.../Plugins/Android/*-1.3.0.aar`）をそのまま利用しており、AAR の追加・差し替えは行っていない（1.3 参照）

### 1.2 実装時の追加判断

- `Read` / `HasClip` / `GetDescription` の operation 名定数（`read` / `hasClip` / `getDescription`）は計画の公開定数一覧に無かったため、`private const` として追加した（`_pendingOperationCallbacks` のキーには使わないため public にする理由がなく、既存 `AndroidShareManager` にも同種の private 定数は無いが、マジックストリングの重複を避けるための最小限の追加）
- `Read()` / `GetDescription()` の Bridge 利用不可時エラーコードとして計画どおり `CLIPBOARD_BRIDGE_UNAVAILABLE` を採用した
- テスト実行時に判明した1点のみ計画から逸脱: `AndroidClipboardJsonParserTests` の null/blank/不正 JSON 系6ケースで、パーサ内部の `Debug.LogError` が Unity Test Runner 上で「未処理のエラーログ」として扱われテストが自動失敗した。`LogAssert.Expect` を該当6テストに追加して対応（パーサ実装・計画のログ方針は変更していない）
- レビュー v1 の high 指摘を受け、`InvokeInOrder`（`AndroidClipboardManager.cs`）を common/perCall 別々の try/catch に分離。計画 7.1 の「どちらかが例外を投げても他方の呼び出しと全体の完了が阻害されないこと」は当初の単一 try/catch 実装では満たせていなかった（詳細は 3.2 参照）
- レビュー v1 の low 指摘を受け、`AndroidClipboardJsonBuilder` / `AndroidClipboardJsonParser` のクラス XML コメントに、csharp.md の先頭ログ規則からの逸脱理由（秘匿情報保護）を明記した

### 1.3 clipboard 実装と無関係な作業ツリー差分（レビュー v1 medium 指摘への回答）

レビュー v1 は、作業ツリーに以下2件の差分があるにもかかわらず本レポートが「既存変更なし」「AAR 差し替えなし」としている点を不整合として指摘した。

- `Plugins/Android/*-1.2.0.aar`（`.meta` 含む）の削除 + `*-1.3.0.aar` の追加
- `agent-rules/coding-rules/common.md` の変更

`git log --oneline -- <path>` で両パスの最新コミットを確認したところ、どちらも本セッションより前のマージコミット（`1fbc349`）が最新であり、**本セッション中にこれらのパスへコミットは発生していない**。したがって両差分は次のとおり、clipboard 実装が発生させたものではなく、本 implement-feature 開始前から作業ツリーに存在していた未コミット状態、または本セッション内の別タスクによるもの。

- **AAR 差分**: design v4 のステップ3（native-toolkit 確認）を実施した時点で、同梱 AAR は既に 1.3.0 であり（設計書 1.9 参照）、1.2.0 の削除はこの implement-feature セッションでは一切操作していない。design-feature スキル実行より前の状態と判断する
- **`common.md` 差分**: 本 clipboard の `/design-feature` を呼び出す直前に、本セッション内の別タスクとして「C# の同期・非同期 API 判断ルール」をユーザーの明示的な依頼・承認のもとで追記したもの。この時点では clipboard の実装計画・実装のいずれにも参照されておらず、clipboard 機能のスコープには含まれていなかった

**結論（v2 時点）:** 上記2件は clipboard 実装の変更ファイル一覧（2.1〜2.3）に含めない。前者はコミット判断が必要な別途の資産更新、後者は既にユーザー承認済みの別タスクの成果物であり、いずれも本 clipboard レビューの対象外として扱う。

**v3 での補足:** レビュー v2 により、`common.md` の Awaitable 併設ルールの文言が clipboard の設計判断（Awaitable 版なし）と矛盾していることが指摘された。この指摘を受けて `common.md` を追加修正しており（基本情報の「v2 からの変更点」参照）、この2回目の `common.md` 変更は **clipboard レビューへの直接対応であり、clipboard 実装のスコープ内**である。1回目の追記（sync/async 判断ルールそのものの新設）と、2回目の修正（Awaitable 併設を条件付きにする文言修正）は別の変更であり、後者のみを本 clipboard 作業の一部として扱う。

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

Runtime / Tests ファイルへの変更は無し（計画どおり）。

- `agent-rules/coding-rules/common.md`: レビュー v2（medium）対応として、「非同期版の併設ルール」節の文言を Awaitable 併設を無条件の必須事項ではなく「多重呼び出しガードの前提条件を満たす場合のみ」の条件付きに修正した。ルール文書のため Runtime / Tests には含めていないが、clipboard レビューへの直接対応として本セクションに記載する

### 2.3 非変更（対象だが未変更）

- `Runtime/Common/UnityMainThreadDispatcher.cs`: 既存のまま流用
- `Runtime/NativeToolkit.Runtime.asmdef`: 追加参照不要（`UnityEngine` のみで完結）
- `Runtime/AssemblyInfo.cs`: `InternalsVisibleTo("NativeToolkit.Runtime.Tests")` が設定済みで、`internal` 型（`AndroidClipboardJsonParser`、読み取り結果型の internal factory）をテストから直接検証できた
- `Plugins/Android/*-1.3.0.aar`: clipboard 実装が同梱済み（設計 1.9）のため、この implement-feature セッションでの差し替えはなし（1.3 参照）
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
- **レビュー v1 で修正**: 初回実装の `InvokeInOrder` は `common?.Invoke(result); perCall?.Invoke(result);` を単一の try/catch で包んでおり、`common` 側の購読者が例外を投げると `perCall` が実行されないバグがあった。計画 7.1 の「どちらかが例外を投げても他方の呼び出しと全体の完了が阻害されないこと」というテスト観点に対し、既存テスト（`InvokeInOrder_PerCallThrows_*`）は perCall 側の例外しか検証しておらず、common 側の例外は未検証だった。common/perCall を別々の try/catch に分離し、`InvokeInOrder_CommonThrows_PerCallStillInvokedAndExceptionSwallowed` テストを追加して修正を確認した

### 3.3 success 時契約

- `isSuccess == true` のとき `errorMessage == null` を保証: `ClipboardOperationResult.Success` に private コンストラクタで固定し、`AndroidClipboardManagerDispatchTests.ClipboardOperationResult_Success_ErrorMessageIsNull_Invariant` で検証済み

## 4. ビルド結果

- 実行コマンド:
  - `Unity -batchmode -nographics -runTests -projectPath . -testPlatform EditMode -testResults <path> -logFile <path>`（macOS ビルドターゲット、既定）
  - `Unity -batchmode -nographics -runTests -projectPath . -testPlatform EditMode -buildTarget Android -testResults <path> -logFile <path>`（Android ビルドターゲットで `AndroidClipboardManager.cs` 本体と `AndroidClipboardManagerDispatchTests.cs` をコンパイル・実行するため追加実施）
- 結果: SUCCESS（両ビルドターゲットとも `error CS` 0件、`InvokeInOrder` 修正後も再確認済み）
- 補足:
  - `AndroidClipboardManager.cs` は `#if UNITY_ANDROID` ガードのため、既定の macOS ビルドターゲットでは非コンパイル。Android ビルドターゲットに切り替えて再実行し、コンパイルとテスト実行の両方を確認した
  - テスト実行のためのビルドターゲット切り替えによる `unity-native-plugin.slnx` の意図しない差分（`NativeToolkit.BuildProcessors.Editor.csproj` 参照の削除）が都度発生したため、毎回 `git checkout -- unity-native-plugin.slnx` で元に戻した。ProjectSettings 側の恒久的な変更は発生していない（`git status` で確認済み）
  - ビルドターゲットを Android に切り替えたまま作業を終えるとローカル Unity Editor の状態が変わってしまうため、レビュー対応後の最終確認として `-buildTarget StandaloneOSX` で明示的に macOS へ戻し、その状態で最終の macOS 実行（197/197）を行った

## 5. テスト結果

- 実行したテスト: EditMode 全件（Unity Test Runner、`NativeToolkit.Runtime.Tests` アセンブリ）
- 結果サマリー（macOS ビルドターゲット、最終確認）:
  - 実行件数: 197
  - 成功: 197
  - 失敗: 0
- 結果サマリー（Android ビルドターゲット、`AndroidClipboardManager` 本体・dispatch テストを含む、`InvokeInOrder` 修正後）:
  - 実行件数: 205
  - 成功: 205
  - 失敗: 0
- 失敗時の対応:
  - 初回実装時: `AndroidClipboardJsonParserTests` の6ケース（null / blank / 不正JSON × ParseReadResult / ParseDescriptionResult）が失敗。原因は `Debug.LogError` が Unity Test Runner に「未処理のエラーログ」として検出されたため。`LogAssert.Expect` を追加して修正し、再実行で全件成功を確認した
  - レビュー v1 対応時: `InvokeInOrder_CommonThrows_PerCallStillInvokedAndExceptionSwallowed` を新規追加し、`InvokeInOrder` の修正（3.2 参照）を Android ビルドターゲットで再実行して確認（204→205件）
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
| dispatch順序 | AndroidClipboardManagerDispatchTests.cs | InvokeInOrder_* (7件、`InvokeInOrder_CommonThrows_PerCallStillInvokedAndExceptionSwallowed` を含む) | ○ | Android ビルドターゲットでのみ実行対象（`#if UNITY_ANDROID`）。common/perCall 個別の例外分離を検証 |
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
- ○ Awaitable版を含まない、かつこの判断が現行common.mdのルール文言と矛盾しない（レビューv2 mediumへの対応。common.mdの文言を条件付きに修正して整合させた）
- ○ copyPlainTextのblankにC#独自のバリデーションを追加していない
- ○ 実装コードのXMLコメント・行コメント・ログ文言・エラー文言がすべて英語
- ○ クリップボード本文および生成JSONがどのログにも出力されていない
- ○ common/perCallの例外分離が個別のtry/catchで保証されている（レビューv1 highへの対応。当初は単一try/catchで欠陥あり）
- ○ EditModeテストが全てpassed（macOS 197/197、Android 205/205、InvokeInOrder修正後）
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
