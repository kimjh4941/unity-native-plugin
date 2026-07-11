# iOS Share 実装レビュー結果

## レビュー対象

- 日付: 2026-07-05
- ブランチ: `feature/UNT-6`
- PR番号: なし
- diff: `develop...HEAD`
- 実装計画: `artifact/designs/share/2026-07-05-ios-share-design-v5.md`
- 実装結果: `artifact/results/share/2026-07-05-ios-share-implementation-feature-result-v1.md`
- 対象プラットフォーム: iOS

## レビュー概要

- iOS Share の Runtime API、JSON builder、Manager、EditMode テスト、iOS xcframework 1.2.0 更新をレビューした。
- `shareContent` シンボルは `unity-ios-native-toolkit-1.2.0.xcframework` の device slice で `T _shareContent` として確認できた。
- Manager + Bridge の guard 分離、`InternalsVisibleTo`、JSON schema、dispatch 順序は概ね計画どおり。

## 重大な問題（high）

- なし。

## 改善提案（medium）

1. `IosShareResult.Failure(null)` で失敗時の `ErrorMessage` 非 null 契約が崩れる。
   - 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareResult.cs:47`
   - 関連: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareManager.cs:142`
   - 計画書では `ErrorMessage` は `IsSuccess=false` のときのみ非 null と定義されているが、`Failure(string? error)` は null をそのまま保持する。native callback が `isSuccess=false, errorMessage=null` を返した場合や、外部コードが `Failure(null)` を呼んだ場合に、失敗結果なのに `ErrorMessage == null` になる。
   - 修正案: `Failure(string? error)` 内で `error ?? "Unknown error."` などに正規化するか、引数を `string error` にして呼び出し側で null fallback する。あわせて `IosShareResult_Failure_NullError_NormalizesMessage` のようなテストを追加する。

2. Manager 経由の即時 Failure / dispatcher 経路が未テストのまま残っている。
   - 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/IosShareManager.cs:102`
   - 関連: `artifact/designs/share/2026-07-05-ios-share-design-v5.md:501`
   - 関連: `artifact/results/share/2026-07-05-ios-share-implementation-feature-result-v1.md:143`
   - 現状の `IosShareManagerDispatchTests` は `InvokeInOrder` を直接検証しているが、`Share(payload, onResult)` から `FireResult`、`UnityMainThreadDispatcher.Enqueue`、`ShareCompleted`、per-call callback までの実経路は未実施。計画書 §7.2 が挙げる `payload == null`、items 空、Editor 非 iOS Failure、last-registered wins は、実装結果でも未実施として残っている。
   - 修正案: PlayMode `UnityTest` で 1 フレーム進め、`ShareCompleted` と `onResult` が両方 1 回届くこと、エラー文言、連続呼び出し時の callback クリアを検証する。

## 軽微な指摘（low）

1. 実装結果に記載された Unity Test Runner の出力ファイルを現在の working tree で確認できなかった。
   - 対象: `artifact/results/share/2026-07-05-ios-share-implementation-feature-result-v1.md:88`
   - `editmode-results.xml` / `editmode-run.log` は repo 配下の `rg --files` では見つからなかった。テスト成功の記録自体は結果レポートにあるが、レビュー時点では生ログを突合できていない。
   - 修正案: 結果レポートに実際の保存先を追記するか、レビュー可能な場所に test result を残す。

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: ○
- テスト方針の網羅性: △（Manager 統合 PlayMode が未実施）
- エラーケース全実装: △（失敗時 ErrorMessage null の defensive 正規化がない）
- 返却仕様との整合: △（`Failure(null)` で失敗時 errorMessage 契約が崩れる）

## プロジェクトルール適合チェック

- `common.md` 準拠: △（失敗時 errorMessage 契約の defensive 保証が不足）
- `csharp.md` 準拠: ○
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○
- 既存 API 互換性: ○

## テストカバレッジ

- カバー済み:
  - JSON builder の type / value / optional fields / excludedActivityTypes / escape / null item 除外。
  - `IosShareResult.Success` / `Failure("...")` の基本契約。
  - `InvokeInOrder` の共通イベント → 個別 callback の順序、null callback 許容、per-call 例外時の握りつぶし。
- 不足:
  - `IosShareResult.Failure(null)` の契約固定。
  - `IosShareManager.Share` を Manager 経由で呼ぶ PlayMode / `UnityTest`。
  - 実機 iOS 18+ の共有シート提示、キャンセル、不正 URL / ファイル、`excludedActivityTypes` の手動確認。
- レビュー時に実行した確認:
  - `git diff --check develop...HEAD`: 問題なし。
  - `nm -gU .../UnityIosPlugin | rg " T _shareContent"`: `T _shareContent` を確認。
  - Unity Test Runner は今回のレビューでは再実行していない。

## 総合評価

要修正（軽微）。実装の骨格は計画どおりで大きな設計逸脱はないが、失敗時 `ErrorMessage` の契約保証と Manager 実経路のテストを追加してから完了扱いにするのが安全。
