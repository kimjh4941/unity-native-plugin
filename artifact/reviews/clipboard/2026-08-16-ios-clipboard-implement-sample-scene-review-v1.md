# iOS Clipboard サンプルシーン実装レビュー v1

## レビュー概要

- 対象ブランチ: `feature/UNT-9`
- 対象実装: working tree のサンプル新規7ファイル、既存変更2ファイル、および関連 `.meta`
- 参照 diff: `main...HEAD`（iOS Clipboard 公開 API の参照コンテキスト）
- 対象計画: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v5.md`
- 対象実装結果: `artifact/results/clipboard/2026-08-16-ios-clipboard-implement-sample-scene-result-v1.md`
- 対象プラットフォーム: iOS / Editor
- レビュー日: 2026-08-16

今回のサンプルファイルは未追跡で `main...HEAD` に含まれないため、現在の working tree を実装対象として確認した。56個の機能ボタンと `HomeButton`、per-call の result context、observation owner token、deferred Stop、画面外 file cleanup、TopMenu / Navigator 導線は概ね計画どおり実装されている。

実装結果に記録された Unity Test Runner の結果（EditMode 387/387、PlayMode 44 passed / 11 device-only skipped）は確認材料として採用した。レビュー中に `dotnet build NativeToolkit.Runtime.csproj --no-restore` も試したが、Unity が生成する `Temp/obj/.../project.assets.json` が現作業ツリーにないため、コードのコンパイルへ進む前に `NETSDK1004` で終了した。この失敗は実装不良の判定には用いていない。

## 重大な問題（high）

なし。

## 改善提案（medium）

### 1. observation control の発行直後に status / enabled 状態が反映されない

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:1018`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:1063`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:1082`
- 関連計画: 4.7、6.5.2

`BeginStart()` / `BeginStop()` は `ControlPending` を即時変更するが、`UpdateStatus()` と `UpdateEnabledStates()` は完了 callback でしか呼ばれない。そのため Start / Restart / Stop の発行から callback 到着まで、画面は発行前の状態を表示し続ける。

具体的には、Start 実行中も `Observing: off` のままで Scope / Start / Busy Start / missing-scope error ボタンが押せ、Restart / Stop 実行中も pending 表示と無効化が反映されない。Manager の single-flight と owner token により observation state 自体は壊れないが、計画 4.7 の enabled 契約と、`starting` / `on (pending)` を持つ status 契約を満たしていない。

改善提案:

- `IssueStartObserving` と `IssueStopObserving` で owner を取得した直後に、画面が生存している場合だけ `UpdateStatus()` / `UpdateEnabledStates()` を呼ぶ。
- Busy の非所有2本目では、1本目が作った pending 状態をそのまま表示する。
- controller 発行時の UI 遷移を検証できるテストまたは小さな純粋 seam を追加し、状態 helper 単体の enabled 計算だけで完了としない。

### 2. file cleanup の例外ログが一時ファイルパスを含み得る

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:920`
- 対象行: 934、952（同ファイル）
- 関連計画: 3.3、4.6、S-29 / M-20

`ConsumeLoadedFile` と `TryDeleteRequestDirectory` は `ex.Message` をログへ出している。`FileInfo.Length` や `Directory.Delete` が投げる `FileNotFoundException`、`DirectoryNotFoundException`、`UnauthorizedAccessException`、`IOException` などの message は対象パスを含み得るため、「`Path` は長さも含めて表示・ログしない」という明示契約を保証できない。

改善提案:

- これらのログは exception 型名と処理段階だけにし、`ex.Message` を出さない。
- 同じ方針で `OnCopyImageFileClicked` の fixture 書き込み失敗ログも見直す。clipboard 返却パスではないが、`Application.persistentDataPath` を含む可能性がある。
- 可能なら、パス文字列を含む例外を注入してログに出ないことを EditMode で固定する。

### 3. 「復元済み」とされた solution 差分が残り、既存プロジェクトが外れている

- 対象: `unity-native-plugin.slnx:5`
- 関連実装結果: 1.3、3.1

実装結果は `unity-native-plugin.slnx` を `git checkout` で復元済みとしているが、現在の working tree では変更状態にある。差分では `NativeToolkit.BuildProcessors.Editor.csproj` が solution から削除され、`NativeToolkit.Runtime.Tests.csproj` の並びも変わっている。

今回のサンプル計画に solution 変更は含まれず、既存 BuildProcessors プロジェクトを IDE / solution build の対象から外す理由もない。結果レポートと実際の作業ツリーも一致していないため、最終差分から除去する必要がある。

改善提案:

- `unity-native-plugin.slnx` を基準状態へ復元し、`git status --short` と `git diff -- unity-native-plugin.slnx` で差分がないことを再確認する。
- 実装結果の「復元済み」は、復元後の最終確認結果に合わせて更新する。

## 軽微な指摘（low）

### 1. missing-scope テストが handler から発行 helper への受け渡しを固定していない

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosClipboardSampleStateTests.cs:381`
- 関連計画: 6.8.2

`MissingNamedScope_IsAFreshNamedScope` は `CreateMissingNamedScope()` が fresh な Named scope を返すことだけを検証する。`OnErrObserveMissingNamedClicked` がその値を `IssueStartObserving` へ渡すことはテストしていないため、将来 `_activeScope` を渡す実装へ退行してもテストは通る。

Controller を EditMode で生成しない制約は妥当なので、計画どおり `(marker, owner, targetScope)` を記録する純粋な発行 seam、または handler の引数選択を表す純粋 helper まで切り出すと契約を直接固定できる。

### 2. 新しい public Navigator メソッドに XML ドキュメントコメントがない

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs:87`
- 関連ルール: `agent-rules/coding-rules/csharp.md`「XML ドキュメントコメント」

`ShowIosClipboard(UIDocument)` は新規 public メソッドだが `<summary>` と `<param>` がない。既存ファイルの他メソッドも同形式ではあるものの、新規コードとしてはプロジェクトルールに合わせてコメントを追加するのが望ましい。

## 計画整合性チェック

- ボタン網羅性: ○
  - 計画 4.3 のセクション表どおり、機能ボタン56個 + `HomeButton` の57個が UXML、Controller の query / subscribe / unsubscribe、wiring test に揃っている。計画見出しの「55」は表の合計と不一致であり、表を正とした判断は妥当。
- result correlation: ○
  - immutable な `IosClipboardSampleResultContext` を callback ごとに capture し、共通イベントは ClipboardChanged を除いて shape-only logging に限定している。
- observation ownership / teardown: ○
  - owner token、NonOwningToken、native Start failure、開始時 scope capture、deferred Stop の1回限りの発行、画面外 callback の処理順は計画と整合する。
- observation status / enabled: ×
  - 状態計算は正しいが、発行直後に UI へ反映されないため 4.7 の実行時契約を満たさない。
- file ownership: △
  - size 取得と親ディレクトリ削除は画面外でも実行され、4経路表示も実装されている。一方、例外 message からパスがログへ出る可能性が残る。
- fixture / error 導線: ○
  - length fixture、独自 UTI、missing named scope、native error buttons、Large PNG の範囲 guard は計画に沿う。
- TopMenu / Navigator: ○
  - iOS Player 導線と Editor 2択ダイアログ、Controller 除去登録、Resources path は計画どおり。
- 自動テスト: △
  - wiring と純粋 state の主要契約は広く固定されているが、controller の発行時 UI 更新と missing scope の最終受け渡しは未固定。
- GUI / 実機項目: △（計画どおり未実施）
  - S-1〜S-6、S-10〜S-32、V-1〜V-6 は結果ファイルに未実施理由が記録されている。静的レビューで完了扱いにはしない。

## プロジェクトルール適合チェック

- Manager / Bridge 境界: ○
  - サンプルは `IosClipboardManager` の公開 API のみを利用し、native ABI や Bridge 実装を変更していない。
- callback lifecycle: ○
  - 共通イベントは `OnEnable` / `OnDisable`、button callback は初期化 / `OnDestroy` で対称に管理されている。
- 画面外 callback: ○
  - observation state、deferred Stop、caller-owned file cleanup は `IsScreenAlive()` より前に処理される。
- ログ秘匿: ×
  - clipboard 本文・検出値・pasteboard 名は丸められているが、file API の `ex.Message` がパスを漏らし得る。
- C# コメント / UI 文言: ○
  - コメントとユーザー向け文言は英語で、絵文字は使われていない。
- public XML documentation: △
  - public Controller には XML コメントがあるが、新規 `ShowIosClipboard` にはない。
- `.meta`: ○（実装結果の説明を前提）
  - 新規 `.meta` は Unity 生成と明記されている。
- 差分衛生: ×
  - `git diff --check` は clean だが、対象外の `unity-native-plugin.slnx` 差分が残っている。

## 総合評価

要修正（軽微）。

Manager API の使い方、result correlation、owner-aware observation state、deferred Stop、画面外 cleanup という中核は適切で、high の問題はない。一方、pending 中の UI 契約、パス非出力契約、solution 差分の3点は、計画上の実機確認へ進む前に解消すべきである。いずれも局所的な修正であり、設計の組み直しは不要。
