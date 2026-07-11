# レビュー結果

- 日付: 2026-07-05
- 対象ファイル: `artifact/designs/share/2026-07-05-ios-share-sample-scene-design-v1.md`
- 機能名: share
- プラットフォーム: ios

---

## 強み

- 実装結果 v3 の公開 API、`IosShareResult` の成功/失敗契約、Editor/非 iOS Failure、dispatch 順序をサンプル UI 方針へ反映できている。
- native iOS サンプルアプリの Text / URL / Image / File / Combination / Error の操作群を、Unity 側の `IosShareContentPayload` に対応付けており、画面要件の網羅性が高い。
- `AndroidShareManagerExampleController` と `IosNotificationManagerExampleController` の既存パターンを参照し、TopMenu → Navigator → ExampleController、`OnEnable` / `OnDisable` 購読、`OnDestroy` 解除、`ResultTextBlock` 表示の流儀を維持している。
- `.meta` を手動作成対象として列挙せず、Unity 自動生成物として扱っている点はリポジトリ方針と整合している。
- 実機 iOS 18+ の共有シート提示、キャンセル、各エラー、iPad popover、Editor Failure 表示まで手動確認観点が具体化されている。

## 改善点

### 高優先度

- なし。

### 中優先度

1. Editor で iOS Share サンプルを確認する方針と、Editor では Android Share に遷移する暫定方針が矛盾している。
   - 対象: `artifact/designs/share/2026-07-05-ios-share-sample-scene-design-v1.md:184`
   - 関連: `artifact/designs/share/2026-07-05-ios-share-sample-scene-design-v1.md:282`
   - 関連: `artifact/designs/share/2026-07-05-ios-share-sample-scene-design-v1.md:314`
   - 問題: §4.2 と §6 では Editor 上で iOS Share の導線・イベント購読・結果表示を確認できる前提になっている。一方、§5.6 では Editor の暫定方針を従来どおり `ShowAndroidShare` としており、TopMenu から iOS Share 画面へ到達できない。
   - 改善提案: Editor の Share ボタン遷移先を設計時点で確定する。iOS サンプル計画としては、`ShowIosShare` を Editor でも開ける別導線を明記するか、TopMenu の Editor 分岐を iOS 優先にするか、Editor 確認項目を Navigator 直接呼び出し前提へ書き換える。

### 低優先度

1. `excludedActivityTypes` の手動確認で Facebook を主な合否条件にしない方が安定する。
   - 対象: `artifact/designs/share/2026-07-05-ios-share-sample-scene-design-v1.md:219`
   - 関連: `artifact/designs/share/2026-07-05-ios-share-sample-scene-design-v1.md:309`
   - 問題: Facebook の共有先表示は端末環境や OS 状態に依存しやすく、表示されないことだけでは除外指定が効いた証拠になりにくい。
   - 改善提案: Copy / CopyToPasteboard を主確認にし、Facebook は環境依存の補足確認として明記する。

## 不足項目

- Editor で iOS Share サンプル画面へ到達する導線の最終方針。
- `excludedActivityTypes` の安定した合否条件（Copy 主確認、Facebook 補足確認）。

## 総合評価

要修正（軽微）。サンプルシーン計画としての画面要件・API 整合・手動確認観点は十分に具体化されています。実装前に Editor 導線の矛盾だけ解消すれば、実装に進める状態です。
