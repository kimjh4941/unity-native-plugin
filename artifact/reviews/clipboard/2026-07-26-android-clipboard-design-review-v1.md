# レビュー結果

- 日付: 2026-07-26
- 対象ファイル: artifact/designs/clipboard/2026-07-26-android-clipboard-design-v1.md
- 機能名: clipboard
- プラットフォーム: android

---

## 強み

- native-toolkit 側の公開 API、同期/非同期の違い、listener 経路、返却 JSON、AAR 同梱状態まで確認されており、実装前提が具体的。
- `read` / `hasClip` / `getDescription` を同期 API とし、copy / clear / stopObserving を event + per-call callback に分ける判断は `common.md` の同期/非同期原則と整合している。
- Manager + Bridge パターン、`AndroidJavaProxy`、`UnityMainThreadDispatcher`、proxy 保持、`AndroidJavaObject` disposal など、既存 `AndroidShareManager` の主要パターンを踏襲している。
- クリップボード本文をログに出さない方針が明示されており、`csharp.md` のログ要求との衝突をセキュリティ観点で扱えている。
- EditMode / 手動確認の分担が明確で、JSON builder/parser と dispatch 順序をネイティブ非依存で検証する方針は妥当。

## 改善点

### 高優先度

- セクション: `5.1 Payload 型`, `6.2 use case / repository 層`, `7.3 手動確認`
  - 問題点: `CopyPlainTextPayload.text` について「空文字は native 側で許容される」とあるが、native 側 `ClipboardDomainError.EmptyContent` は「required text is empty」を含み、計画書内のエラー表にも `CLIPBOARD_EMPTY_CONTENT` が存在する。現状のままだと、空の plain text を成功系として実装・テストしてしまう可能性がある。
  - severity: high
  - 改善提案: `text` blank は `CLIPBOARD_EMPTY_CONTENT` になる前提へ修正し、6.2 と 7.3 に「plain text blank」のエラーケースを追加する。native 実装で本当に許容されるなら、該当 UseCase の確認結果を根拠として追記する。

### 中優先度

- セクション: `5.5 AndroidClipboardManager 設計`, `5.7 ログ規則`
  - 問題点: 実装例の XML コメントと「理由コメント」が日本語になっている。`agent-rules/index.md` と `agent-rules/coding-rules/csharp.md` はコメント本文・ユーザー向け文言を英語にすることを要求しているため、実装時にそのまま転記するとルール違反になる。
  - severity: medium
  - 改善提案: 計画書内の実装コメント例を英語へ置き換え、「実装コードの XML コメント、行コメント、ログ文言、UI 文言は英語」と明記する。レビュー出力自体が日本語であることとは分けて扱う。

- セクション: `7.1 EditMode`
  - 問題点: `ParseReadResult` のエラー封筒は全 7 コード網羅とある一方、`ParseDescriptionResult` のエラー封筒テストが明示されていない。`getDescription` も同期 JSON 経路で同じエラー封筒を返すため、片側だけの検証だと parser の分岐不具合を見逃す可能性がある。
  - severity: medium
  - 改善提案: `ParseDescriptionResult` にもエラー封筒、`"null"`、null / 空白 / 不正 JSON、`mimeTypes` 欠落のテストを追加する。

- セクション: `5.5 AndroidClipboardManager 設計`, `6.3 C# Bridge 層`
  - 問題点: `StopObserving` は activity 不要として扱う方針だが、`TryPrepareCall` の新しい引数・戻り値契約がまだ曖昧。特に activity 不要時に `using (activity)` 相当の処理をどう分岐するか、`pluginInstance.Call("stopObserving")` の失敗時 callback が確実に戻るかが実装者依存になりやすい。
  - severity: medium
  - 改善提案: `TryPrepareCall(methodName, json, requiresActivity, out args, out activity)` のように明示し、`requiresActivity == false` では activity 取得をスキップし、`CallOperation` 側も nullable activity を dispose するだけにする、という擬似コードを追記する。

### 低優先度

- セクション: `5.4 AndroidClipboardJsonParser`, `9. 要検証事項`
  - 問題点: `JsonUtility` の制約として空文字とキー欠落を同一視する方針は説明されているが、公開 API として空文字アイテムを null に正規化する影響が利用者に見える。要検証に留めるだけだと、実装後のマニュアル・XML コメントに反映されない可能性がある。
  - severity: low
  - 改善提案: `ClipItem` / `ClipContents` の XML コメントに「native が null 値キーを省略するため、欠落値は null として返す」旨を入れる方針を追加する。空文字と欠落の区別が必要な場合は手書き parser へ切り替える判断基準も残す。

- セクション: `8. Definition of Done`
  - 問題点: DoD に「既存ファイルへの変更がない」とあるが、実装時に `.meta` 自動生成や Unity import による周辺差分が発生する可能性がある。計画本文では `.meta` を作成しない方針があるため大きな問題ではないが、DoD 文言がやや硬い。
  - severity: low
  - 改善提案: 「AI エージェントが意図的に既存 Runtime / Test 実装を変更しない。Unity 自動生成 `.meta` はレビュー対象外」のように言い換える。

## 不足項目

- plain text blank の native 返却仕様と、それに対応する JsonBuilder / 手動確認テスト。
- `ParseDescriptionResult` のエラー封筒・異常 JSON 系テスト。
- 実装コード中の XML コメント・行コメント・ログ文言を英語にする明示ルール。
- `StopObserving` / activity 不要呼び出しの `TryPrepareCall` 擬似コード。

## 総合評価

実装計画としての完成度は高く、native API 確認、C# API 方針、Bridge パターン、スレッド・メモリ契約、テスト分担は概ね十分です。実装前に修正すべき最大の点は、空の plain text を許容するかどうかの矛盾です。ここを native 実装に合わせて正せば、implement-feature に進める水準です。
