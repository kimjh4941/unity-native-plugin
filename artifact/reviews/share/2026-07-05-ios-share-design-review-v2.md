# レビュー結果

- 日付: 2026-07-05
- 対象ファイル: `artifact/designs/share/2026-07-05-ios-share-design-v3.md`
- 機能名: share
- プラットフォーム: ios
- レビュー種別: 再レビュー

---

## 強み

- v2 で残っていた `IosShareManager` / `IosShareManagerDispatchTests` の compile guard 不整合は、v3 の「Manager クラスは `UNITY_IOS || UNITY_EDITOR`、native 部分のみ `UNITY_IOS && !UNITY_EDITOR`」方針で解消されている。
- 非 iOS / Editor 経路の即時 Failure 方針が維持され、無応答 callback のリスクは設計上つぶせている。
- native P/Invoke、`MonoPInvokeCallback`、`s_shareDelegate` を実機 iOS ガード内に閉じる方針は、Editor の `__Internal` シンボル未解決を避ける意図として妥当。
- Manager の入力ガード、非 iOS Failure、dispatch 順序を Editor / PlayMode で検証できるようになり、テスト可能性が v2 より改善している。
- v1/v2 からのレビュー反映履歴が第 9 章にまとまり、なぜ既存 `IosNotificationManager` と guard 方針を変えるのかが追跡しやすい。

## 改善点

### 高優先度

- セクション: `5.4 IosShareManager`
  - 問題点: 手順 4 のコード例で `shareContent(json, s_shareDelegate)` を呼んでいるが、`json` 生成は手順 5 に「native 呼び出しの直前で行う」と書かれている。コード例どおりに実装すると、`json` が宣言前参照になりコンパイルできない。
  - severity: high
  - 改善提案: 手順 4 の `#if UNITY_IOS && !UNITY_EDITOR` ブロック内で `string json = IosShareJsonBuilder.BuildShareContentJson(payload);` を `try` の直前に移動する。もしくは手順 5 を手順 4 より前に移し、Editor 経路でも json を生成する方針に統一する。v3 の意図に合わせるなら「Editor 経路では json を生成せず、実機 iOS ブロック内だけで生成」が最も自然。

### 中優先度

- セクション: `2.2 再利用・非再利用の判断` / `5.4 結果 dispatch`
  - 問題点: `2.2` では「asmdef の変更は不要」としているが、`InvokeInOrder` を `internal static` にしてテストから直接検証するには `InternalsVisibleTo` または asmdef の friend assembly 設定が必要と書かれている。現状の `NativeToolkit.Runtime.asmdef` には該当設定がないため、変更ファイル一覧と実装方針が矛盾している。
  - severity: medium
  - 改善提案: `Runtime/NativeToolkit.Runtime.asmdef` を既存変更に追加し、`NativeToolkit.Runtime.Tests` への internal 可視化を明記する。別案として、`InvokeInOrder` を internal ではなく public にする判断もあるが、テスト用 helper を public API に出す理由は薄い。

- セクション: `5.3 IosShareJsonBuilder` / `7.1 EditMode`
  - 問題点: `items` 内の null 要素を正式に除外対象として扱う方針だが、`IosShareContentPayload.items` の型は `IosShareItem[]` のまま。nullable 有効コードでは、テストで null 要素を入れる時に `null!` 等が必要になり、通常 API と defensive behavior の境界が曖昧。
  - severity: medium
  - 改善提案: public API として null 要素を許容するなら `IosShareItem?[]` にする。Unity serialization や外部生成データへの defensive 対応に留めるなら、「通常の利用契約は non-null、テストでは `null!` を使って不正状態への defensive skip を検証する」と明記する。

### 低優先度

- セクション: `5.6 依存関係の実装順序`
  - 問題点: v3 で `IosShareManagerDispatchTests` と PlayMode / `UnityTest` の Manager 統合テストが追加されたが、実装順序は v1 のまま `IosShareJsonBuilderTests` と `ShareResultTests` だけを挙げている。
  - severity: low
  - 改善提案: 手順 5 を `IosShareJsonBuilderTests`、`IosShareManagerDispatchTests`、`ShareResultTests`、必要に応じて Manager 統合 `UnityTest` へ更新する。

- セクション: `4.5 別途対応`
  - 問題点: `_shareContent` の期待出力に固定アドレス `00000000000065c0` が含まれている。バイナリ再生成で変わり得る値なので、将来の確認で不要な差分・誤判定の原因になりやすい。
  - severity: low
  - 改善提案: 期待値は `T _shareContent` の存在確認に留める。アドレスを残すなら「例」と明記する。

## 不足項目

- 実機 iOS ブロック内での `json` 生成位置の確定。
- `InvokeInOrder` の internal 可視化に必要な `NativeToolkit.Runtime.asmdef` 変更の明記。
- null item を public API として許容するか、defensive-only とするかの明記。
- v3 追加テストを含めた実装順序の更新。

## 総合評価

v3 は、前回の最大リスクだった `IosShareManager` と dispatch test の guard 不整合を解消できています。Editor でも Manager 型を存在させ、native 参照だけを実機 iOS に閉じる方針は、この Share API の「必ず結果を返す」設計とも相性がよいです。

ただし、`Share` 実装手順のコード例では `json` が宣言前に使われる形になっており、このままだと実装時にコンパイルエラーへ直結します。そこを直し、asmdef の internal 可視化を変更ファイルに追加すれば、実装へ進める計画として十分に整います。
