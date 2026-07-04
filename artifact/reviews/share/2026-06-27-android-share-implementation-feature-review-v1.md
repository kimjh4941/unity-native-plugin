# Android Share 実装レビュー v1

## レビュー対象

- ブランチ: `feature/UNT-5`
- PR番号: なし（ローカル差分レビュー）
- diff:
  - `git diff develop...HEAD`
  - 未コミット差分（`git status --short` 上の Share Chooser Action 追加分を含めて確認）
- 設計書: `artifact/designs/share/2026-06-27-android-share-design-v3.md`
- 実装結果: `artifact/results/share/2026-06-27-android-share-implementation-feature-result-v1.md`
- 対象 OS: Android

## レビュー概要

Unity C# 側の Android Share Bridge に Custom Chooser Action callback を追加する実装。`ShareChooserActionResult`、`ShareChooserActionCallbackCoordinator`、`AndroidShareManager.ShareChooserActionTapped`、`ShareText(..., onChooserAction)`、旧 AAR 向け degrade、EditMode テストが追加されている。

設計方針の大半は反映されているが、callback coordinator の例外継続契約に実装バグがあり、該当テストも失敗する状態。加えて public payload コメントの契約不整合、新規 Unity script の `.meta` 不足が残っている。

## 重大な問題（high）

1. `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionCallbackCoordinator.cs:55`
   - 設計書 7.1 と実装結果 5.1 は「共通イベント購読側が例外を投げても per-call callback は継続」としているが、実装は `ChooserActionTapped?.Invoke(result); cb?.Invoke(result);` を同じ `try` に入れている。共通イベントが例外を投げると `cb` まで到達しない。
   - 影響: `ShareChooserActionCallbackCoordinatorTests.Fire_GlobalEventThrows_PerCallCallbackStillInvoked` は失敗する。global event の1購読者の例外で per-call callback が失われるため、callback 転送順序・例外安全の DoD を満たさない。
   - 修正案: global event と per-call callback を別々に `try/catch` する。さらに multicast event の途中購読者例外で後続購読者が止まらないようにするなら `GetInvocationList()` で個別 dispatch する。

## 改善提案（medium）

1. `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidSharePayloads.cs:19`
   - `ChooserActionPayload.intentAction` の XML コメントが「未指定時は native 側で `android.intent.action.SEND` default」「manifest receiver が必要」と説明しているが、今回の設計・native 実装では callback 用 action は非空/非 SEND が必要で、receiver は dynamic registration のため manifest 宣言不要。
   - public API ドキュメントとして利用者を誤誘導する。今回の実装で `ShareText` 側に詳しい説明は追加されているが、payload のフィールドコメントも更新するべき。

2. 新規ファイルの `.meta` が不足
   - 対象:
     - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionCallbackCoordinator.cs`
     - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionResult.cs`
     - `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/ShareChooserActionCallbackCoordinatorTests.cs`
   - 既存 package 内の `.cs` は `.meta` とセットで管理されているため、新規 script も `.meta` をコミット対象に含めるべき。Unity が後から生成すると GUID が環境依存になり、package 配布や参照安定性で揺れる。

3. `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionCallbackCoordinator.cs:53`
   - `Fire` は XML コメントで “Never throws” としているが、`_dispatch(...)` 自体が例外を投げた場合は外へ漏れる。テスト用 dispatch や将来の dispatcher 差し替えで契約とズレる。
   - 修正案: `_dispatch` 呼び出し自体も `try/catch` で包むか、「subscriber exception does not propagate」まで契約を弱める。

4. `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionCallbackCoordinator.cs:32`
   - public constructor / public methods に `Debug.Log` がない。`agent-rules/coding-rules/csharp.md` は public/internal メソッド先頭の `Debug.Log` を要求している。
   - ただし coordinator を Unity 非依存に保つ設計目的もあるため、ルール例外として設計・レビュー結果に明記するか、UnityEngine 依存を許容してログを追加するかを決める必要がある。

## 軽微な指摘（low）

1. `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs:202`
   - `ShareText` は public method だが、先頭で警告判定・coordinator 登録・JSON build を行った後に `Debug.Log` している。C# ルールの「全メソッドの先頭1行目に全パラメータを含む Debug.Log」には厳密には合っていない。既存スタイルに合わせた可能性はあるが、今回追加した `onChooserAction` の有無も含め、先頭ログへ寄せると規約にはより素直。

2. `artifact/results/share/2026-06-27-android-share-implementation-feature-result-v1.md`
   - テスト結果がすべて `△ 手動確認必要` の状態で、DoD は多くが `○` になっている。Unity Test Runner が未実行なら、`EditMode テスト通過` だけでなく、例外安全などテストでしか担保しにくい項目も `△` にしておくと後続が誤読しにくい。

## 設計書整合性チェック

- 企画書との整合性: △（専用 research は確認対象外。設計書 v3 とは概ね一致）
- Clean Architecture 準拠: ○（Manager が AndroidJavaObject を隠蔽し、callback 調整を非 Android 依存クラスへ分離）
- 既存実装との差分分析の正確性: △（実装対象は一致。ただし `.meta` 不足、payload コメント更新漏れあり）
- テスト設計の網羅性: △（テストケースは追加済みだが未実行。1件は現実装で失敗見込み）
- ドメインエラー全ケース実装: ○（chooser action は成否を持たない通知で、新規ドメインエラーなし）
- エラーコード/メッセージ対応表との整合: ○（既存 `ShareOperationResult` 契約は維持）

## プロジェクトルール適合チェック

- common.md 準拠: △（callback 転送を coordinator で EditMode テスト可能にした点は良いが、テスト未実行かつ例外継続契約が未達）
- csharp.md 準拠: △（public methods の先頭 `Debug.Log` ルールに未対応/例外未明記）
- エラー契約反映: △（旧 AAR degrade は反映済み。coordinator の例外安全契約に実装漏れ）
- 既存 API 互換性: ○（`ShareText` 第3引数は optional で追加、既存呼び出しは維持）

## テストカバレッジ

カバーできている観点:

- `ShareChooserActionResult` の値保持 / null 正規化 / empty 保持
- `ShareChooserActionCallbackCoordinator` の発火順序、last-registered wins、null clear、複数回 fire、result pass-through
- `AndroidShareJsonBuilder` の `intentAction` 出力、複数 chooserActions の順序維持

不足・リスク:

- Unity Test Runner で未実行
- `Fire_GlobalEventThrows_PerCallCallbackStillInvoked` は現実装で失敗見込み
- `AndroidShareManager.Initialize` が `setShareChooserActionListener` を呼ぶこと、旧 AAR 時に既存 share が継続することは手動/実機確認のみ
- `OnDestroy` の clear 順序はコードレビュー担保のみ
- 新規 `.meta` 不足

## 実行確認

- `Unity` / `unity` は PATH 上には存在しない
- Unity 実体候補:
  - `/Applications/Unity/Hub/Editor/6000.0.44f1/Unity.app/Contents/MacOS/Unity`
  - `/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity`
- 実装結果では Unity がプロジェクトを開いているため batchmode abort と記載あり。今回レビューでは Unity Test Runner は実行していない。

## 総合評価

**要修正（重大）**

機能の構成は良いが、callback の例外安全という今回の中核契約に実装バグがある。まず `ShareChooserActionCallbackCoordinator.Fire` を修正し、Unity Test Runner で追加テストが通ることを確認してから次工程へ進むべき。
