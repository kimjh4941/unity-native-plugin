# レビュー結果

- 日付: 2026-07-26
- 対象ファイル: `artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md`
- 機能名: clipboard
- プラットフォーム: Android

---

## 強み

- 実装済み `AndroidClipboardManager` の同期 API / 非同期 API / event の違いを明確に整理しており、サンプル Controller で event を待つべき操作と即時表示する操作の境界が分かりやすい。
- `StartObserving` を成功表示にしない方針が明記されており、native から結果が返らない API の制約を UI 表現へ正しく落とし込めている。
- 既存 `AndroidShareManagerExampleController` / UXML / USS / wiring test の構造を深掘りしたうえで、再利用するパターンと clipboard 固有の拡張点を分けている。
- `content://` URI 生成について、FileProvider authority、対象 path、失敗時の表示、実機要検証項目まで書かれている。
- ログにクリップボード本文や読み取り結果本文を出さない方針が、機能実装側の秘匿情報保護ルールと整合している。

## 改善点

### 高優先度

なし。

### 中優先度

#### 実装結果由来の境界値 2 件を操作面で確認できるかが曖昧

- セクション: `1.4 手動確認観点`, `3.1 機能一覧`, `7.2 実機確認`
- severity: medium
- 問題点:
  - 1.4 では、実装結果由来の確認観点として `HTML コピー / blank / plainText のみ空` と `複数テキスト / 空要素` を挙げている（`artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md:83`）。
  - しかし 3.1 のボタン一覧には `plainText` 空・`htmlText` 非空の HTML 成功ケースを実行するボタンがない（`artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md:180`）。
  - 7.2 の実機確認表にも `plainText` のみ空の HTML 成功ケースがない（`artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md:557`）。
  - 複数テキストについても「3 件」の成功確認だけで、空要素を含むケースかどうかが明示されていない（`artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md:562`）。
- 改善提案:
  - `Copy HTML Text (empty plain text, allowed)` のボタンを追加する、または既存 `CopyHtmlTextButton` の固定値を `plainText = ""`, `htmlText = "<b>...</b>"` と明記して確認観点へ対応させる。
  - `CopyMultipleTextButton` の固定サンプル値を `new[] { "First", "", "Third" }` のように空要素込みと明記する、または専用ボタンを追加する。
  - 7.2 の実機確認表にも上記 2 ケースを明示し、実装結果 v3 の未実施項目と 1:1 で追跡できるようにする。

### 低優先度

#### ボタン数の表現が `HomeButton` を含むかどうかで揺れている

- セクション: `3.1 機能一覧`, `6.3 Controller`, `7.1 EditMode 静的チェック`, `8 Definition of Done`
- severity: low
- 問題点:
  - 3.1 は「全16ボタン」としているが、表には `HomeButton` を含めると 17 行のボタンがある（`artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md:171`）。
  - 6.3 の Controller スケルトンも「16 個の Button フィールド」と書いているが、`HomeButton` をフィールド管理するなら 17 個になる（`artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md:363`）。
  - 7.1 / 8 も「3.1 の全16ボタン」と書いており、wiring test で `HomeButton` を含めるべきかが曖昧になる（`artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md:545`、`artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v1.md:583`）。
- 改善提案:
  - 「操作ボタン 16 個 + `HomeButton` 1 個 = 合計 17 個」と明記する。
  - wiring test の必須ボタン配列には `HomeButton` を含める方針に統一する。

## 不足項目

- `plainText` 空・`htmlText` 非空の HTML コピー成功ケースを実行する導線、または既存ボタンの固定値としての明記。
- 複数テキストに空要素を含めた成功ケースを実行する導線、または既存ボタンの固定値としての明記。
- `HomeButton` を含む総ボタン数と wiring test 対象の明確化。

## 総合評価

要修正（軽微）。

サンプルシーン計画としての骨格は十分に整っており、公開 API・エラー契約・既存 ExampleController パターン・FileProvider 方針・ログ安全性はいずれもよく整理されている。実装へ進める水準に近いが、実装結果 v3 の未実施手動確認項目をサンプル上で確実に潰すという目的に対して、境界値 2 件の操作導線が曖昧なため、v2 で補強してから進めるのが望ましい。
