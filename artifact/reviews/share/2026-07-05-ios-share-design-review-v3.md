# レビュー結果

- 日付: 2026-07-05
- 対象ファイル: `artifact/designs/share/2026-07-05-ios-share-design-v4.md`
- 機能名: share
- プラットフォーム: ios
- レビュー種別: 再レビュー

---

## 強み

- v3 レビューの高優先指摘だった `json` 宣言前参照は、実機 iOS ガード内の `try` 直前で生成する形に修正されている。
- `InvokeInOrder` の internal 可視化について、`Runtime/AssemblyInfo.cs` に `InternalsVisibleTo("NativeToolkit.Runtime.Tests")` を追加する方針が変更ファイル一覧と §2.2 に反映されている。
- null item の扱いが defensive-only と明記され、`IosShareItem[]` を維持する理由と `null!` を使ったテスト方針が整理されている。
- 実装順序に `AssemblyInfo.cs`、`IosShareManagerDispatchTests`、Manager 統合 `UnityTest` が追加され、v3 追加分のテスト方針と整合している。
- `_shareContent` の確認期待値から固定アドレスが外され、将来のバイナリ差し替えに強い確認手順になっている。

## 改善点

### 高優先度

- なし。

### 中優先度

- セクション: `3.2 C# 公開 API（新規）` / `5.4 IosShareManager`
  - 問題点: §5.4 では `Share(IosShareContentPayload? payload, Action<IosShareResult>? onResult = null)` と nullable payload 方針が確定しているが、§3.2 の公開 API 一覧は `void Share(IosShareContentPayload payload, ...)` のまま。実装者が API 一覧だけを見ると nullable 契約を取り違える可能性がある。
  - severity: medium
  - 改善提案: §3.2 を `void Share(IosShareContentPayload? payload, Action<IosShareResult>? onResult = null)` に更新する。

- セクション: `5.4 結果 dispatch（OnShareResult / FireResult / テストシーム）`
  - 問題点: v4 で `Runtime/AssemblyInfo.cs` に `InternalsVisibleTo("NativeToolkit.Runtime.Tests")` を追加する方針へ整理された一方、この節にはまだ `[assembly: InternalsVisibleTo("...Tests")]` と「Runtime asmdef 側で Internals 可視を設定」という曖昧な文が残っている。
  - severity: medium
  - 改善提案: 該当文を `Runtime/AssemblyInfo.cs` に `[assembly: InternalsVisibleTo("NativeToolkit.Runtime.Tests")]` を付与する、という §2.2 / §4.1 と同じ表現に置き換える。

### 低優先度

- セクション: `6.3 C# Bridge 層`
  - 問題点: 非 iOS / Editor 行の説明に「v3」と残っており、v4 の `json` 生成位置修正や `AssemblyInfo.cs` 追加とは関係ないが、最新設計としてはやや古い版の注釈に見える。
  - severity: low
  - 改善提案: 「v3」ではなく「v3/v4」または単に「`#else` 経路として実装し EditMode で検証可能」と書く。

## 不足項目

- §3.2 の nullable payload 反映。
- §5.4 dispatch 節の `InternalsVisibleTo` 記述の最新版への統一。

## 総合評価

v4 は実装計画としてかなり整っています。前回のコンパイル直結リスクだった `json` 生成位置と internal 可視化の扱いは解消され、guard 方針、非 iOS Failure、null item defensive 対応、テスト方針も一貫してきました。

残りは主にドキュメント内の古い表記の修正です。§3.2 の nullable API 一覧と §5.4 の `InternalsVisibleTo` 表記を揃えれば、実装へ進めてよい計画です。
