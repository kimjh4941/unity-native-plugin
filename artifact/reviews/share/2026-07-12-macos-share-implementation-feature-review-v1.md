# macOS Share 実装レビュー結果 (v1)

- 日付: 2026-07-12
- レビュー対象ブランチ: `feature/UNT-7`
- レビュー対象 diff: `develop...HEAD` は空。ローカル未コミット差分を対象
- 実装計画: `artifact/designs/share/2026-07-12-macos-share-design-v3.md`
- 実装結果: `artifact/results/share/2026-07-12-macos-share-implementation-feature-result-v1.md`
- 対象プラットフォーム: macOS
- 機能名: share

---

## レビュー概要

- macOS Share の C# Runtime 実装として、`MacSharePayloads` / `MacShareJsonBuilder` / `MacShareResult` / `MacShareManager` が追加された。
- 計画どおり `shareContent` / `shareViaService` を `DllImport("__Internal")` + Cdecl callback で呼び出し、共通イベント `ShareCompleted` と per-call callback へ dispatch している。
- EditMode / PlayMode テストも計画どおり追加され、JSON 生成、Result 契約、dispatch 順序、Editor/非 macOS 経路の即時 failure をカバーしている。
- 本レビュー中に `.meta` の存在も再確認し、新規 C# / Test ファイル 8 件すべてに対応する `.meta` が存在することを確認した。

## 重大な問題（high）

- なし

## 改善提案（medium）

- なし

## 軽微な指摘（low）

- なし

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
  - `MacShareManager` が P/Invoke 境界を隠蔽し、native callback を `UnityMainThreadDispatcher` 経由で公開イベント / per-call callback に転送している。
- 変更ファイル一覧との一致: ○
  - 計画の Runtime / Tests 追加対象と実ファイルが一致。`.meta` も生成済み。
  - macOS plugin bundle はローカル差分上では 1.1.0 削除 / 1.2.0 追加が含まれるが、実装結果では本実装前の前提更新として整理されており、1.2.0 に `_shareContent` / `_shareViaService` が含まれることを確認した。
- テスト方針の網羅性: ○
  - 計画された EditMode / PlayMode テストファイルと主要観点が実装されている。
- エラーケース全実装: ○
  - C# 事前検証の no items / empty serviceName / non-macOS / native call exception が実装済み。
  - native 由来エラーは `errorMessage` を透過する設計どおり。
- 返却仕様との整合: ○
  - `MacShareResult.Success` は `ErrorMessage = null`、`Failure` は errorMessage 非 null 正規化を保証している。

## プロジェクトルール適合チェック

- common.md 準拠: ○
  - Manager + Bridge パターン、JSON builder 分離、Unity main thread dispatch、`.meta` hygiene は適合。
- csharp.md 準拠: ○
  - public API の XML コメント、主要 public / lifecycle method 先頭ログ、英語コメントが適合。
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○
  - static callback + `[MonoPInvokeCallback]` + persistent delegate で IL2CPP / GC 対策済み。
  - string marshaling のみで手動 unmanaged buffer は不要。
- 既存 API 互換性: ○
  - iOS / Android / 共通 Share API への破壊的変更なし。macOS 専用クラスの追加のみ。

## テストカバレッジ

- カバー済み:
  - `MacShareJsonBuilderTests`: item type、複数 item、null entry 除外、optional fields、escape。
  - `MacShareResultTests`: success / cancel / failure / errorMessage 正規化。
  - `MacShareManagerDispatchTests`: common → per-call 順序、null callback、per-call 例外 swallow。
  - `MacShareManagerIntegrationTests`: dispatcher 経由の PlayMode callback flush、非 macOS / Editor failure 経路、`Share` と `ShareViaService` の callback 独立性。
- 未実施 / 残リスク:
  - Unity Test Runner の実実行は実装結果どおり未実施。
  - 実機 macOS での `Share` picker mouseDown 挙動、`ShareViaService` の Mail / Message 動作、NULL pointer marshaling は手動確認待ち。
- レビュー中の追加確認:
  - `NativeToolkit.Runtime.csproj`
  - `NativeToolkit.Runtime.Tests.csproj`
  - `NativeToolkit.Runtime.PlayModeTests.csproj`
  - 上記 3 件の `dotnet build --no-restore` は最終的にすべて成功、0 Warning / 0 Error。

## 総合評価

LGTM。

実装計画 v3 と実装結果 v1 に対するコード上の追加指摘はなし。未実施の Unity Test Runner / 実機確認は結果レポートに明記されており、現時点ではレビュー上の修正必須事項ではない。
