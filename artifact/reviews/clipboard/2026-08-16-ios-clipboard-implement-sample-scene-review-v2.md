# iOS Clipboard サンプルシーン実装レビュー v2

## レビュー概要

- 対象ブランチ: `feature/UNT-9`
- 対象実装: working tree の iOS Clipboard サンプルシーン差分
- 参照 diff: `main...HEAD`（iOS Clipboard 公開 API の参照コンテキスト）
- 対象計画: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v5.md`
- 対象実装結果: `artifact/results/clipboard/2026-08-16-ios-clipboard-implement-sample-scene-result-v2.md`
- 前回レビュー: `artifact/reviews/clipboard/2026-08-16-ios-clipboard-implement-sample-scene-review-v1.md`
- 対象プラットフォーム: iOS / Editor
- レビュー日: 2026-08-16

前回レビュー v1 の medium 3件、low 2件への対応を中心に再確認した。observation 発行時の UI 更新、file exception の秘匿、solution 差分、missing-scope の発行 seam、Navigator の XML documentation はすべて実コードへ反映されている。

サンプル実装は未追跡ファイルを含むため、現在の working tree をレビュー対象とした。実装結果に記録された Unity Test Runner の結果（EditMode 391/391、PlayMode 44 passed / 11 device-only skipped）を検証材料として採用した。

## 重大な問題（high）

なし。

## 改善提案（medium）

なし。

## 軽微な指摘（low）

なし。

## 前回レビュー指摘への対応

### 1. observation 発行直後の status / enabled 更新: 解消

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:1055`

`IssueStartObserving` と `IssueStopObserving` が Manager 呼び出し後に `RefreshObservationUI()` を実行し、`BeginStart()` / `BeginStop()` が作った pending 状態を直ちに status と enabled へ反映する。

Manager callback が同期到着した場合も、callback が state を完了状態へ遷移させた後に `RefreshObservationUI()` が現在値を再計算するため、古い pending 表示へ戻らない。Busy の2本目は非所有 request であり、1本目が作った pending 状態を変更しない。

### 2. file exception message によるパス漏洩: 解消

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardSampleResult.cs:81`
- 適用先: `IosClipboardManagerExampleController.cs:573`、`:933`、`:954`

`DescribeException(Exception)` は `exception.GetType().Name` のみを返す。fixture 書き込み、file size 取得、request directory 削除の3経路から `ex.Message` が除去されている。

`DescribeException_DropsThePathBearingMessage` は、パスを含む `FileNotFoundException` を渡しても結果が `FileNotFoundException` のみで `/` を含まないことを固定している。

### 3. `unity-native-plugin.slnx` の対象外差分: 解消

現在の `git status --short` に `unity-native-plugin.slnx` は現れず、`git diff --check` も clean。前回外れていた `NativeToolkit.BuildProcessors.Editor.csproj` を含め、solution の意図しない差分は残っていない。

### 4. missing-scope の handler → 発行 helper 契約: 解消

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardSampleObservationState.cs:121`
- handler: `IosClipboardManagerExampleController.cs:1024`
- tests: `IosClipboardSampleStateTests.cs:374`

`IosClipboardSampleStartRequest` が marker / owner / targetScope を不可分に保持し、`IosClipboardSampleObservationRequests` が Start / Restart / BusyPair / MissingNamed を純粋に構築する。

Controller の4経路はこの request をそのまま `IssueStartObserving` へ渡している。MissingNamed は active scope を引数に取らず、クリックごとの fresh Named scope を作るため、`_activeScope` への退行を構造的に避けている。BusyPair も1本目だけが owner を取り、2本目は NonOwningToken かつ同じ scope であることがテストされている。

### 5. `ShowIosClipboard` の XML documentation: 解消

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs:87`

`<summary>` と `uiDocument` の `<param>` が追加され、`csharp.md` の public method documentation 規則を満たす。

## 計画整合性チェック

- ボタン一覧の実装網羅性: ○
  - 計画 4.3 のセクション表どおり、機能ボタン56個 + `HomeButton` が UXML、Controller、wiring test に揃っている。
- UXML name と Controller の一致: ○
  - 57ボタン、必須ラベル、`ResultScrollView` の query / subscribe / unsubscribe が一致する。
- API 呼び出し仕様の一致: ○
  - Manager の callback API、per-call result context、observation owner token、開始時 scope capture、deferred Stop、file cleanup は計画と整合する。
- 変更ファイル一覧との一致: ○
  - 新規7ファイル、既存変更2ファイル、および Unity 生成 `.meta` の構成に一致し、対象外の solution 差分はない。
- observation status / enabled: ○
  - 発行時と完了時の双方で現在の observation state が UI へ反映される。
- ログ秘匿: ○
  - clipboard 本文、検出値、pasteboard 名、返却パスに加え、file exception message 経由のパス漏洩も防止されている。
- GUI / 実機項目: △（計画どおり未実施）
  - S-1〜S-6、S-10〜S-32、V-1〜V-6 は結果ファイルに未実施理由が明記されている。Editor GUI と iOS 実機で扱う後続検証であり、本静的レビューの blocker とはしない。

## プロジェクトルール適合チェック

- common.md 準拠: ○
  - ExampleController は Manager 公開 API に依存し、native / Bridge 詳細を変更していない。状態・発行 request は EditMode で検証できる純粋 helper に分離されている。
- csharp.md 準拠: ○
  - public Controller と新規 public Navigator method に XML documentation があり、コメントと UI 文言は英語。純粋 internal helper のログ省略理由もコード上に明記されている。
- ライフサイクル管理（登録・解除の対称性）: ○
  - Manager event は `OnEnable` / `OnDisable`、button callback は初期化 / `OnDestroy` で対称に管理される。
- コンパイルガードの網羅性: ○
  - iOS / Editor の Controller・Navigator・TopMenu 分岐は既存プラットフォーム導線と整合する。
- 権限ガードの網羅性: ○
  - Clipboard API に追加 runtime permission は不要。
- ナビゲーション統合: ○
  - `ShowIosClipboard`、`RemoveExistingControllers`、iOS Player 分岐、Editor 2択ダイアログが揃っている。
- 既存 API 互換性: ○
  - Manager 公開 API や既存 Android Clipboard サンプルに破壊的変更はない。
- 差分衛生: ○
  - `git diff --check` は clean で、Unity 再生成による `.slnx` 差分も除去済み。

## 総合評価

LGTM。

前回レビューの5件はすべて解消され、新しい request seam による回帰も見当たらない。残る Editor GUI と iOS 実機の確認は計画・結果ファイルに明記された後続検証であり、サンプルシーン実装の採用を妨げない。
