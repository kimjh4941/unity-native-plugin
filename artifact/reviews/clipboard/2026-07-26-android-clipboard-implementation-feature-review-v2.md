# Android Clipboard 実装レビュー v2

## レビュー概要

- 日付: 2026-07-26
- 機能名: clipboard
- 対象プラットフォーム: Android
- ブランチ: `feature/UNT-8`
- レビュー対象差分: `develop...HEAD` は空のため、ローカル未コミット差分を対象
- 実装計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md`
- 実装結果: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v2.md`
- 前回レビュー: `artifact/reviews/clipboard/2026-07-26-android-clipboard-implementation-feature-review-v1.md`
- 総合評価: 要修正（軽微）

v1 の high 指摘だった `InvokeInOrder` の例外分離は、common/per-call を個別の `try/catch` に分ける修正と追加テストにより解消されている。JSON builder/parser、同期/非同期の呼び分け、StartObserving の非通知契約も設計 v4 に概ね沿っている。

一方で、現在の `agent-rules/coding-rules/common.md` は非同期 native API に callback 版 + `Awaitable<TResult>` 版の併設を必須としている。設計 v4 と結果 v2 は `Awaitable` 版を本実装に含めない方針のままで、レビュー workflow は現行の project rule 読み込みを必須としているため、この衝突は未解決として扱う必要がある。

## 重大な問題（high）

なし。

前回 high の `InvokeInOrder` 問題は解消済み。

- `AndroidClipboardManager.InvokeInOrder` は common と per-call を別々の `try/catch` で囲んでいる（`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardManager.cs:492`）
- common 側例外でも per-call が呼ばれるテストが追加されている（`Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardManagerDispatchTests.cs:86`）

## 改善提案（medium）

### 1. 現行 `common.md` の Awaitable 併設ルールと設計 v4 の「Awaitable 版なし」が衝突している

- 対象ルール: `agent-rules/coding-rules/common.md:142`
- 対象実装: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardManager.cs:165`
- 対象設計: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md:899`
- 対象結果: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v2.md:175`

現行 `common.md` は「非同期ネイティブ API に対しては、2 種類の公開 API を併設する」とし、callback 版に加えて `Awaitable<TResult>` を返す `XxxAsync` メソッドを要求している。一方、設計 v4 と実装結果 v2 は `Awaitable` 版を含めないことを DoD にしている。

review-implementation-feature workflow は `common.md` / `csharp.md` を読み込んで準拠性を確認する手順を必須としているため、結果 v2 の「`common.md` は clipboard レビュー対象外」とする整理だけでは、現行ルールへの不適合を解消できない。

推奨修正:

- 方針 A: clipboard 実装を現行 `common.md` に合わせ、非同期 6 API に `XxxAsync` と in-flight ガードを追加する。この場合は設計 v5 相当の更新が必要。
- 方針 B: clipboard 初期実装では Awaitable 版を不要とするなら、`common.md` のルールを「必須」ではなく「条件付き」に修正し、その根拠を設計/結果へ反映する。
- どちらの場合も、設計 v4 の `common.md` 解釈（「併設してよい」）と現在の `common.md` の文言（「併設する」）を一致させる。

## 軽微な指摘（low）

なし。

Builder/Parser の先頭ログ逸脱は、秘匿情報保護を理由にクラス XML コメントへ明記されており、設計 5.7.2 の「本文・生成 JSON をログに出さない」方針と整合している。

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: △
- テスト方針の網羅性: ○
- エラーケース全実装: ○
- 返却仕様との整合: ○

補足:

- 計画 v4 内だけを見ると、変更ファイルと API 形状は概ね一致している。
- ただし現行 `common.md` を正本として読むと、非同期 API の Awaitable 併設が未実装であり、設計とプロジェクトルールの整合が崩れている。
- AAR 差分と `common.md` 差分は作業ツリーに残っている。結果 v2 は clipboard 実装外として切り分けているが、コミット単位では混入しないよう注意が必要。

## プロジェクトルール適合チェック

- `common.md` 準拠: △
- `csharp.md` 準拠: ○
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○
- 既存 API 互換性: ○

補足:

- `common.md` の Manager + Bridge、AndroidJavaProxy、UnityMainThreadDispatcher 経由 dispatch、`#if UNITY_ANDROID` ガードは満たしている。
- `common.md` の Awaitable 併設ルールだけが未解決。
- `csharp.md` のログ規則からの逸脱は、clipboard の秘匿情報保護を理由に設計とコードコメントで明示されているため妥当。

## テストカバレッジ

結果 v2 によると、macOS EditMode 197/197、Android ビルドターゲット EditMode 205/205 が成功している。本レビューではテスト再実行はしていない。

カバー済み:

- JSON builder の必須キー、label 省略、`isSensitive`、配列、エスケープ
- JSON parser の正常系、空 sentinel、エラー封筒 7 コード、null/blank/不正 JSON
- `InvokeInOrder` の順序、結果伝搬、null delegate、common/per-call 双方向の例外分離
- `ClipboardOperationResult.Success` の `ErrorMessage == null` 契約

不足/未実施:

- 実機手動確認（計画 7.3 の全18項目）
- `stopObserving` の 0 引数 JNI 解決
- Android foreground/background 制限下の read/observe 挙動
- Awaitable 版を採用する場合の async API と in-flight ガードのテスト

## 総合評価

要修正（軽微）。

v1 の実装バグは修正済みで、clipboard 機能そのものの主要ロジックには新たな high 指摘はない。ただし、現行 `common.md` と設計 v4 の Awaitable 方針が衝突しているため、このまま LGTM にはできない。先に「今回の初期実装で Awaitable を含めるのか、ルール側を条件付きにするのか」を決め、設計・実装・結果レポートの三者をそろえる必要がある。
