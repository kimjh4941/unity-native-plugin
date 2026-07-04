# Android Share サンプルシーン実装レビュー v1

- 日付: 2026-06-27
- 対象: `artifact/results/share/2026-06-27-android-share-implement-sample-scene-result-v1.md`
- 設計: `artifact/designs/share/2026-06-27-android-share-sample-scene-design-v2.md`
- レビュー範囲: 実装結果、Share サンプル UI/Controller、TopMenu 導線、Navigator、EditMode 配線テスト、関連 Share callback 実装

---

## 総評

実装は概ね設計どおりです。TopMenu から Share 画面への導線、UXML の element name と Controller の `root.Q(...)`、`OnEnable`/`OnDisable` での Manager event 購読、Custom Chooser Action の global event 受信方針、Editor / Android 分岐はいずれも整合しています。

ただし、Unity パッケージとしてコミットする前に新規アセットの `.meta` 生成・追加が未完了です。これは設計と実装結果でも明示されている完了条件なので、コミット前に確認が必要です。

---

## 指摘事項

### 中1: 新規 Share サンプル資産の `.meta` が未生成/未追加

- 重要度: Medium
- 該当:
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/`
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Android/Share/`
  - `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidShareSampleSceneWiringTests.cs`
  - `artifact/results/share/2026-06-27-android-share-implement-sample-scene-result-v1.md:30`

設計では、新規 `.cs` / `.uxml` / `.uss` と新規フォルダの `.meta` は Unity が生成した後にアセットと一緒にコミットする前提です。実装結果にも「コミット前に Unity 生成済みの `.meta` を確認してステージングに含める」と記載されています。

現状のファイルシステム確認では、以下の新規資産に対応する `.meta` が見当たりません。

- `Runtime/Resources/UI/Android/Share.meta`
- `Runtime/Resources/UI/Android/Share/AndroidShareManagerExample.uxml.meta`
- `Runtime/Resources/UI/Android/Share/AndroidShareManagerExampleStyle.uss.meta`
- `Runtime/UI/Android/Share.meta`
- `Runtime/UI/Android/Share/AndroidShareManagerExampleController.cs.meta`
- `Tests/Runtime/AndroidShareSampleSceneWiringTests.cs.meta`

Unity が自動生成するため AI が手書きする必要はありませんが、このままコミットすると Unity import 後に各環境で GUID が生成され直し、パッケージ内アセットの履歴・参照安定性が崩れます。Unity Editor で一度 import して `.meta` を生成し、対象アセットと一緒にコミットしてください。

---

## 確認済み事項

- `AndroidShareManagerExample.uxml` の `HomeButton` / `ResultTextBlock` / `CallbackTextBlock` / `ChooserActionTextBlock` / 各 Share ボタン名は Controller 側の `root.Q(...)` と一致しています。
- `ShareCustomActionButton` は `ShareTextPayload.chooserActions` に Save/Open の 2 件を設定し、per-call `onChooserAction` を渡さず global event `ShareChooserActionTapped` のみで表示する設計に沿っています。
- `OnEnable` / `OnDisable` で `ShareOperationCompleted` / `ShareCallbackReceived` / `ShareChooserActionTapped` を購読・解除しており、画面離脱後の stale 参照回避方針に沿っています。
- `ShareWithCallback` は `onStarted` を ResultLabel、`onSelected` を CallbackLabel に分離しており、二段階 callback の表示方針に沿っています。
- `TopMenuExample.uxml` の `ShareFeatureButton`、`TopMenuExampleController.OnShareClicked`、`NativeToolkitSampleNavigator.ShowAndroidShare` の導線は整合しています。
- 以前の機能レビューで指摘された `ShareChooserActionCallbackCoordinator.Fire` の例外継続問題は、global event と per-call callback を別 try/catch に分離する形で修正されています。
- `AndroidShareSampleSceneWiringTests.cs` は `UNITY_EDITOR` ガード内で `AssetDatabase` を使っており、`NativeToolkit.Runtime.Tests.asmdef` も `includePlatforms: ["Editor"]` のため EditMode テスト配置として妥当です。

---

## 未確認 / 残リスク

- Unity Test Runner はこのレビューでは実行していません。
- Android 実機での Sharesheet 起動、Direct Share 表示、API 34+ Custom Chooser Action タップ、API 33 以下での不発確認は未実施です。
- chooser action 対応版 AAR 差し替え後の実機確認は、実装結果の未実施項目どおり後続確認が必要です。

---

## 判定

- 判定: 修正必要
- 理由: 実装ロジックは設計と整合しているが、新規 Unity 資産の `.meta` 生成・追加がコミット前完了条件として残っているため。
