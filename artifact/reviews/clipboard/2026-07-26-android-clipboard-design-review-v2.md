# レビュー結果

- 日付: 2026-07-26
- 対象ファイル: artifact/designs/clipboard/2026-07-26-android-clipboard-design-v2.md
- 機能名: clipboard
- プラットフォーム: android

---

## 強み

- v1 レビューの指摘が体系的に反映されており、特に plain text blank の扱いは native UseCase まで再確認されている。
- `copyPlainText` は blank を成功、`copyHtmlText.htmlText` は blank を `CLIPBOARD_EMPTY_CONTENT` とする差分が 1.10 / 5.1 / 6.2 / 7.3 で一貫している。
- 実装コード内のコメント・ログ・エラー文言を英語にするルールが明文化され、`csharp.md` との衝突が解消されている。
- `TryPrepareCall` に `requiresActivity` を導入する契約が擬似コード化され、`stopObserving` の activity 不要経路が実装しやすくなっている。
- `ParseDescriptionResult` の異常系テストが追加され、同期 JSON 経路の read / getDescription 両方を検証する方針になっている。
- ログにクリップボード本文・生成 JSON を出さない方針が明確で、既存 Share 実装との差分も意識できている。

## 改善点

### 高優先度

- なし。

### 中優先度

- セクション: `5.5 AndroidClipboardManager 設計`, `6.3 C# Bridge 層`, `6.4 Unity 側で検知できないケース`
  - 問題点: `StartObserving()` は callback を持たないため結果通知なしでよいが、C# Bridge 利用不可時（非 Android / `pluginInstance` null / `currentActivity` null / `pluginInstance.Call` 例外）の挙動が 6.3 に明示されていない。実装者が例外を握るのか、ログだけ出すのか、`ClipboardChanged` を発火しないだけなのか判断しづらい。
  - severity: medium
  - 改善提案: `StartObserving()` 専用の準備処理を明記し、Bridge 利用不可や `Call` 例外では `Debug.LogWarning` / `Debug.LogError` のみを出して return し、event / callback は発火しないことを 6.3 または 6.4 に追加する。

- セクション: `5.4 AndroidClipboardJsonParser`, `7.1 EditMode`
  - 問題点: `JsonUtility` 用 DTO の具体的な形が未記載。Unity の `JsonUtility` は public fields を基本にマッピングするため、DTO を C# プロパティで書くと parser が空結果になりやすい。テストで検出可能だが、計画書に実装制約として書くと事故を減らせる。
  - severity: medium
  - 改善提案: Parser 内 DTO は `[Serializable] private sealed class` + lowerCamelCase の public fields に統一する、と明記する。例: `public string? error; public string? message; public string[]? mimeTypes; public ClipItemDto[]? items;`。

### 低優先度

- セクション: `5.2 結果型`
  - 問題点: `ClipItem` / `ClipContents` / `ClipDescriptionInfo` は getter-only public properties として示されているが、コンストラクタや factory の公開範囲が未記載。実装時には parser から生成する必要があるため、constructor を public にするか internal にするかで API 面が変わる。
  - severity: low
  - 改善提案: 公開型の生成方針を追記する。例: public constructor で利用者も値を作れるようにする、または internal constructor + static factory で結果型生成を parser に閉じる、のどちらかを決める。

- セクション: `5.9 非同期版（Awaitable）を本計画に含めない理由`
  - 問題点: 「したがって v1 は callback 版のみ」とあり、v2 文書内の記述としてバージョン表現が残っている。
  - severity: low
  - 改善提案: 「本計画は callback 版のみ」または「clipboard 初期実装は callback 版のみ」に修正する。

## 不足項目

- `StartObserving()` の C# Bridge 利用不可時・例外時のログ/return 仕様。
- `JsonUtility` DTO は public fields で定義するという実装制約。
- getter-only 公開結果型の constructor / factory 方針。

## 総合評価

v2 は実装計画として十分に強く、前回の主要指摘は解消されています。残る改善点は、実装時の曖昧さを減らすための補足が中心です。`StartObserving()` の失敗時挙動と `JsonUtility` DTO 形状だけ明記すれば、implement-feature に進める状態です。
