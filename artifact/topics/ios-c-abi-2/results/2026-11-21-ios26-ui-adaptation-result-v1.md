# iOS 26.2 への UI テストと手伝い役の手当ての記録 v1

- 記録日: 2026-11-21（夜。2026-11-22 に書いた）
- 親: [README](../README.md) の段 4（6.6 の「段 4 に iOS 26 のシミュレーターでも 1 回流す」、段 4 の計画の項目 5）
- 前の準備: `029ad32`（`SystemUI` に、iOS 26 の共有シートを `PopoverDismissRegion` で閉じる形と、見出しの題を `LP.CaptionBar.TopCaption` から読む形を写した。native-toolkit の `ShareSheet.swift` の形）
- コミット: `5b9e875`
- 実装のレビュー: [../reviews/2026-11-21-ios26-ui-and-optout-implementation-review-v1.md](../reviews/2026-11-21-ios26-ui-and-optout-implementation-review-v1.md)（サブエージェントと Codex。通知の opt-out の確かめと一緒に）
- シミュレーター: iPhone 16 / iOS 26.2（比べに iOS 18.3）。Xcode 26.3

## 1. 最初に落ちたもの（iOS 26.2）

| 回 | 落ちたもの |
|---|---|
| ゲート（Player のテスト） | Dialog のサンプルの ID07: アクションシートの Cancel が見つからない。Share のサンプルの SS01〜SS15: 共有シートが閉じない |
| UI テスト（ハーネス） | Dialog の ID8（アクションシートの Cancel）、ID12（ログインのダイアログ）、Share の SH7（シートの Close を待つ） |

## 2. 原因

- iOS 26 のアクションシートはポップオーバーで出て、Cancel のボタンが無い
- 共有シートの `PopoverDismissRegion` は画面全体に広がる。Unity の Player では、その中央がシートの下にあり、中央を押しても閉じない
- `app.snapshot()` は `UIActivityContentView` の枠の上端を 0 と返した。`ActivityListView` からは正しく読めた
- プロセスの最初の共有シートは、押しても閉じないことがある
- ログインのダイアログの後に、「パスワードを保存しますか?」が数秒遅れて出る
- SH7 の Close: iOS 26.2 の共有シートには Close のボタンが無い

## 3. 直したこと（`5b9e875`）

- アクションシート: `SystemUI` は `PopoverDismissRegion` で取り消す。手伝い役に命令 `sheet-cancel` を足し、Dialog のサンプルの ID07 が使う。取り消しに失敗したときは何も押さない
- 共有シート: 領域の上端と `ActivityListView` の上端の中間を押す。枠は 1 回の snapshot から読み、上端が止まったのを見て、押す直前にもう一度読む。シートと領域が残っていれば、5 秒後に 1 回だけ押し直す（`SystemUI.closeShareSheetAndWait`。手伝い役とハーネスの両方が使う）
- パスワードの保存の申し出は断る（命令 `password-offer-close`。シミュレーターだけ）。ハーネスの ID12 と、Dialog のサンプルの ID10 の後片付け（TearDown）で使う
- SH7 は、iOS 26 では Close を待たない
- 失敗の文は、シミュレーターで見つけたものを書く。手伝い役の答えに載せるのは数だけ

## 4. レビュー

実装のレビュー v1 の指摘（A 1、B 4、C 7 と Codex の P1・P2）はすべて直した（レビューの 3 章）。主なもの:

- 押す点は `PopoverDismissRegion` 自身の座標から作る。押す直前に、領域が 1 つ、上端が同じ、点がシートのどの要素の枠にも入っていないことを確かめる（Codex P1、I-2）
- シートの上端が領域の中（上から 88pt 以上、下から 88pt 以上）にあるときだけ押す（I-1）
- 枠と数は `app.snapshot()` を 1 回取って読む（I-3）

## 5. 実機の決まり

**実機では、iOS 26 の計算した点を押さない**（`closeShareSheet` が false を返す。レビューの I-1）。share target に当たりうる点を、私物の端末で押さないため。今の実機（iPhone XS）は iOS 18.7.2 のままで、この経路を通らない。iOS 26 の端末を使うときに、押し方を改めて決める（1.14.0 の後へ持ち越す）。

## 6. 結果

- iOS 26.2: ゲートと UI テスト全部が成功
- iOS 18.3: 前と同じく、ゲートと UI テスト全部が成功（戻りの確かめ）
- 実機（iPhone XS / iOS 18.7.2、2026-11-22 朝、`a764789`）: 新しい手伝い役で、ゲート（Player 112 件、Notification のサンプル 8 件）と UI テスト 25 件が成功（[I-17 の結果](2026-11-22-ios-c-abi-i17-git-url-result-v1.md) の 2 章）
- その後の `a75d802`（狭い画面の直し）の回も、iOS 18.3 と 26.2 のシミュレーターで Player 115 件と Notification のサンプル 8 件が成功した

## 7. 残ること

- iOS 26 の端末での押し方（5 章）
- iPad のシミュレーターは足していない（iPad のアクションシートの基点は確かめずに出す。利用者の判断、2026-11-21）
