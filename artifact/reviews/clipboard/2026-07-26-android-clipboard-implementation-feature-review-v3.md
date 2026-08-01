# Android Clipboard 実装レビュー v3

## レビュー概要

- 日付: 2026-07-26
- 機能名: clipboard
- 対象プラットフォーム: Android
- ブランチ: `feature/UNT-8`
- レビュー対象差分: `develop...HEAD` は空のため、ローカル未コミット差分を対象
- 実装計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md`
- 実装結果: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v3.md`
- 前回レビュー: `artifact/reviews/clipboard/2026-07-26-android-clipboard-implementation-feature-review-v1.md`, `artifact/reviews/clipboard/2026-07-26-android-clipboard-implementation-feature-review-v2.md`
- 総合評価: LGTM

レビュー v2 の medium 指摘だった `common.md` と Awaitable 方針の衝突は、`common.md` 側を「Awaitable 版は多重呼び出しガード等の前提条件を満たす場合のみ併設」とする条件付きルールへ修正したことで解消されている。これにより、設計 v4 の「clipboard 初期実装は callback 版のみ」という判断と現行ルールが整合した。

前回までに確認した `InvokeInOrder` の例外分離修正も維持されており、実装・テスト・結果レポートの主要な不整合は解消済み。

## 重大な問題（high）

なし。

## 改善提案（medium）

なし。

## 軽微な指摘（low）

なし。

補足:

- ローカル作業ツリーには AAR 1.2.0 削除 / 1.3.0 追加の差分が残っているが、結果 v3 では clipboard 実装外の別資産更新として切り分けられている。コミット時に混入させるか分離するかだけ注意すること。
- 実機手動確認は未実施のため、`stopObserving` の 0 引数 JNI 解決や Android foreground/background 制限下の挙動は次工程で確認が必要。

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: ○
- テスト方針の網羅性: ○
- エラーケース全実装: ○
- 返却仕様との整合: ○

補足:

- 同期 3 メソッドは戻り値 API として実装され、`ClipboardOperationCompleted` を発火しない。
- 非同期 6 メソッドは共通 event + per-call callback として実装されている。
- `StartObserving` は callback 引数なし、失敗時はログのみで return する設計どおり。
- `Awaitable` 版なしの判断は、現行 `common.md` の条件付きルールおよび設計 v4 5.9 と整合している。

## プロジェクトルール適合チェック

- `common.md` 準拠: ○
- `csharp.md` 準拠: ○
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○
- 既存 API 互換性: ○

確認内容:

- `common.md` は callback 版必須、Awaitable 版は前提条件を満たす場合のみ併設と明記されている（`agent-rules/coding-rules/common.md:142`）。
- `AndroidClipboardManager` は `#if UNITY_ANDROID` ガード、`AndroidJavaObject` / `AndroidJavaProxy`、`UnityMainThreadDispatcher` 経由 dispatch に準拠している。
- `InvokeInOrder` は common と per-call を個別 `try/catch` で保護している（`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardManager.cs:492`）。
- Builder / Parser の先頭ログ逸脱は、クリップボード本文や生成 JSON をログに出さないための明示的なセキュリティ判断としてコードコメント化されている。

## テストカバレッジ

結果 v3 によると、以下は成功済み。

- macOS EditMode: 197/197 passed
- Android ビルドターゲット EditMode: 205/205 passed

カバー済み:

- JSON builder の必須キー、label 省略、`isSensitive`、配列、エスケープ
- JSON parser の正常系、空 sentinel、エラー封筒 7 コード、null/blank/不正 JSON
- `InvokeInOrder` の順序、結果伝搬、null delegate、common/per-call 双方向の例外分離
- `ClipboardOperationResult.Success` の `ErrorMessage == null` 契約

未実施:

- 実機手動確認（計画 7.3 の全18項目）
- `stopObserving` の 0 引数 JNI 解決
- Android foreground/background 制限下の read/observe 挙動

本レビューではテスト再実行はしていない。結果レポートの実行結果とコード差分の照合に基づく評価。

## 総合評価

LGTM。

実装レビュー v1 / v2 の指摘は解消されており、設計 v4・結果 v3・現行プロジェクトルールの整合も取れている。残るリスクは実機依存の手動確認項目であり、これは計画上も次工程で確認する前提として整理済み。
