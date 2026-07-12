# macOS Share サンプルシーン実装レビュー結果 (v1)

- 日付: 2026-07-12
- レビュー対象ブランチ: `feature/UNT-7`
- レビュー対象 diff: `main...HEAD` には前段の macOS Share 機能実装差分が含まれる。今回のサンプルシーン実装はローカル未コミット差分を対象
- サンプルシーン計画: `artifact/designs/share/2026-07-12-macos-share-sample-scene-design-v1.md`
- サンプルシーン実装結果: `artifact/results/share/2026-07-12-macos-share-implement-sample-scene-result-v1.md`
- 対象プラットフォーム: macOS
- 機能名: share

---

## レビュー概要

- macOS Share サンプルシーンとして、`MacShareManagerExampleController`、UXML / USS、TopMenu / Navigator 配線、EditMode wiring test が追加された。
- 計画どおり `Share` picker 系、`ShareViaService` Mail 直接実行、エラー系ボタン、Home 導線が実装されている。
- `MacShareSampleSceneWiringTests` は Resources パス、必須ボタン名、`ResultTextBlock`、TopMenu の `ShareFeatureButton` を検証している。
- 新規 `.cs` / `.uxml` / `.uss` / フォルダに対応する `.meta` が存在することを確認した。

## 重大な問題（high）

- なし

## 改善提案（medium）

- なし

## 軽微な指摘（low）

- なし

## 計画整合性チェック

- ボタン一覧の実装網羅性: ○
  - 計画の `HomeButton`、Basic / Multiple / Filter / Direct Service / Error Handling の全 15 ボタンが UXML と Controller に実装されている。
- UXML name と Controller の一致: ○
  - `MacShareManagerExample.uxml` の button name と `root.Q<Button>(...)` が一致している。
  - `MacShareSampleSceneWiringTests` でも同じ required name 群を検証している。
- API 呼び出し仕様の一致: ○
  - picker 系は `MacShareManager.Instance.Share(...)`、Mail / Unknown Service は `ShareViaService(...)` を使用。
  - `recipients` / `subject` / `excludedServiceTitles` の macOS スキーマも計画どおり。
- 変更ファイル一覧との一致: ○
  - 新規 Controller / UXML / USS / wiring test、既存 Navigator / TopMenu の変更が計画と一致。
  - TopMenu UXML、Runtime Share 本体、サンプルシーンアセット、共有画像は計画どおり未変更。

## プロジェクトルール適合チェック

- common.md 準拠: ○
  - ExampleController が Manager 公開 API のみを呼び、native 直接依存はない。
  - プラットフォーム依存は `UNITY_STANDALONE_OSX || UNITY_EDITOR` で分離されている。
- csharp.md 準拠: ○
  - 新規 Controller の MonoBehaviour イベント関数とハンドラには先頭ログがある。
  - public class の XML summary も付与されている。
- ライフサイクル管理（登録・解除の対称性）: ○
  - `ShareCompleted` は `OnEnable` / `OnDisable` で購読解除。
  - `Button.clicked` は `InitializeUI` で登録し、`OnDestroy` で対称に解除している。
- コンパイルガードの網羅性: ○
  - Controller / Navigator / TopMenu の macOS share 経路は `UNITY_STANDALONE_OSX || UNITY_EDITOR` 方針と整合。
  - 各ハンドラは計画どおり Manager 内部ガードに委譲している。
- 権限ガードの網羅性: ○
  - Share サンプルには通知権限のような事前権限ガード対象はない。
- ナビゲーション統合: ○
  - `NativeToolkitSampleNavigator.ShowMacShare` と `RemoveExistingControllers` が追加されている。
  - `TopMenuExampleController` の Share ボタン表示ガードと macOS 分岐が追加されている。
- 既存 API 互換性: ○
  - Android / iOS / Windows / macOS Dialog / macOS Notification の既存遷移分岐は維持されている。

## USS / UXML 品質

- CSS クラス名は UXML と USS で一致している。
- `ScrollView` は `vertical-scroller-visibility="Auto"`、`horizontal-scroller-visibility="Hidden"` で、既存 sample UI と同じ縦スクロール主体の構成。
- ボタン・結果表示・セクション構成は既存 iOS Share / macOS Notification のサンプル UI パターンと整合している。

## テスト / 検証

- `git diff --check`: 成功
- `dotnet build --no-restore NativeToolkit.Runtime.Tests.csproj`: 成功、0 Warning / 0 Error
- `dotnet build --no-restore NativeToolkit.Runtime.csproj`: 成功、0 Warning / 0 Error
  - 最初に Runtime / Tests を並列実行した際、同一 `Temp/bin/Debug` の `*.deps.json` を取り合って Runtime 側が `GenerateDepsFile` file-in-use で失敗した。単独再実行では成功したため、コード起因の失敗ではない。
- Unity Test Runner 実行、Unity Editor 上での UXML/USS import 確認、macOS Standalone 実機確認は実装結果どおり未実施。

## 残リスク

- ネイティブ picker の mouseDown 要件、Mail / Messages service の実 OS 挙動、`excludedServiceTitles` の実効フィルタは手動確認待ち。
- Unity Editor 起動中のため、Test Runner 実行と実機 macOS Standalone の確認はレビュー内では未実施。

## 総合評価

LGTM。

計画 v1 と実装結果 v1 に対するサンプルシーン実装として、コード上の追加指摘はなし。残る確認事項は結果レポートに明記済みの Unity Test Runner / Editor import / macOS 実機手動確認であり、現時点ではレビュー上の修正必須事項ではない。
