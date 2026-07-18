# レビュー結果

- 日付: 2026-07-12
- 対象ファイル: artifact/designs/share/2026-07-12-macos-share-design-v1.md
- 機能名: share
- プラットフォーム: macOS

---

## 強み

- native-toolkit 側の C Bridge、JSON スキーマ、コールバック契約、エラー文言が具体的に整理されており、C# 実装で必要な P/Invoke 境界が明確。
- iOS Share の testable な Manager パターンを macOS に移植する判断が妥当で、`UNITY_STANDALONE_OSX || UNITY_EDITOR` と native 呼び出し部の `UNITY_STANDALONE_OSX && !UNITY_EDITOR` 分離も EditMode テスト方針と整合している。
- macOS 固有の `recipients` / `subject` / `excludedServiceTitles` を iOS payload から分離する方針が明確で、スキーマ混同を避けられている。
- `shareContent` の mouseDown 依存を要検証として扱い、`shareViaService` を verified-safe path として推奨しているため、手動確認のリスク管理が現実的。
- スレッド契約、メモリ契約、IL2CPP/AOT 制約、callback delegate の GC 防止が明記されており、Unity native plugin 実装時の落とし穴を事前に押さえている。
- `.meta` を手動作成対象として列挙しない方針が維持されている。

## 改善点

### 高優先度

- なし

### 中優先度

- なし

### 低優先度

- セクション: `4. 変更ファイル一覧` / `7.2 PlayMode`
  - 問題点: `7.2` で `MacShareManager.Instance` 生成や非 OSX プレイヤーでの即時 `Failure` 経路を PlayMode で検証するとしている一方、`4.2 新規作成（Tests）` には `Tests/Runtime/*` のみが列挙され、PlayMode テストファイルが変更対象に含まれていない。
  - severity: low
  - 改善提案: `Tests/PlayMode/MacShareManagerIntegrationTests.cs` を新規作成対象に追加し、実装順序にも PlayMode テストを含める。既存の `IosShareManagerIntegrationTests.cs` と同様に、dispatcher 経由で callback が flush される経路を確認する想定まで明記すると実装漏れを防げる。

- セクション: `1.2 コールバック型` / `1.3 定数（サービス名）` / `5.3 MacShareResult`
  - 問題点: `ShareViaService` の入力 `serviceName` は raw `NSSharingService.Name` だが、callback / `MacShareResult.ServiceName` は選択サービスの表示名として扱われる。本文中で両者は説明されているものの、API 使用者が `ServiceName` の戻り値を次回の `ShareViaService` 入力に再利用できない点が明示されていない。
  - severity: low
  - 改善提案: `MacShareResult.ServiceName` の説明に「表示名であり、`ShareViaService` の raw service identifier ではない」ことを追記する。併せて既知の raw service identifier を参考定数化する場合は、表示名との混同を避ける命名（例: `MacShareServiceNames.MailCompose`）にする。

## 不足項目

- `Tests/PlayMode/MacShareManagerIntegrationTests.cs` の追加計画。
- `ServiceName` 戻り値が表示名であり raw service identifier ではないことを public API コメントまたは設計本文で明示する項目。

## 総合評価

macOS Share の C# 層を実装する計画として、native API 確認、Manager + P/Invoke 境界、JSON スキーマ、エラー仕様、スレッド・メモリ・IL2CPP 制約、EditMode テスト方針はいずれも十分に具体化されている。高・中優先度の設計欠陥は見当たらない。

残る改善点は、計画内で宣言した PlayMode テストを変更ファイル一覧にも反映することと、direct share の raw service identifier と callback 表示名の区別をさらに明示すること。これらを補えば、実装者が迷わず着手できる精度の計画になる。
