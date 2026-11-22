# iOS のサンプルのセーフエリアの計画 v2 のレビュー v2（差分）

- 記録日: 2026-11-21
- 対象: [../designs/2026-11-21-ios-sample-layout-sample-scene-design-v2.md](../designs/2026-11-21-ios-sample-layout-sample-scene-design-v2.md)（v1 からの差分と、レビュー v1 の指摘が直ったか）
- レビュアー: サブエージェント（general-purpose）だけ。**Codex（`codex:codex-rescue`）は使えなかった**（v1 と同じ。CLI が入っていない）。決まりどおり、サブエージェントの結果だけで判断した
- 1 章はレビュアーの出力をそのまま写した。こちらの判断は 2 章にだけ書く

## 1. サブエージェント

数: A1 1 / A2 1 / B 0 / C 2

### 前の回の指摘の判定

| 指摘 | 判定 | 根拠 |
|---|---|---|
| S-1 起動した直後のトップのメニュー | 直った | D-2 と 4 章で `EnsureSafeArea` を作り、`ApplyScreen` の最後と `TopMenuExampleController.Start` の 2 か所から呼ぶ。D-5 (a) で余白が当たったこと自体を確かめるので、部品が付かないまま SL01/SL02 が通る形は捕まる。SL11 も足した |
| S-2 セレクター | 一部 | D-3 は子孫セレクター `.top-scroll #unity-content-container` に直り、V-2 に横の iOS・広い窓・縮みの確認を足した。しかし 4 章 77 行目の USS の例は、子の結合子 `.top-scroll > #unity-content-viewport > #unity-content-container` のまま残っている（R2-3） |
| S-3 横の見出しの高さ | 直った（Notification と Share について） | D-7 で `max-height: 50%` と `min-height: 50%` を入れた。D-5 (b) と V-5 も足した。ただ、この直し方から新しい問題が 2 つ出た（R2-1、R2-2） |
| S-4 向きが残る | 直った | 4 章で、まず `Portrait` にし、`Screen.height > Screen.width` を 10 秒まで待つ。V-6 で 1 つ目の回が全件通るかを見る。元の値（AutoRotation のときがある）に戻した後に実機が横へ戻るかは、V-6 の実機の回で分かる |
| S-5 承認の表示と時間 | 直った | 2 章に `ios_ui_runner.py` の `playertests` の文を足し、D-6 にも書いた。所要時間は V-7 で見る |
| S-6 余白の色 | 直った | 4 章で、写した色も変わったかの判定に入れ、透明の間は毎フレーム読み直す。iOS の 4 つの画面とトップのメニューの最初の子は、どれも不透明な背景を持つ（USS で確かめた）ので、読み直しが続くことはない |
| S-7 置き場所 | 直った | 2 章と D-1 に置く理由を書き、`common.md` の「`UI/` のディレクトリ構成」に `UI/<Platform>/Common/` を足すと決めた |
| S-8 直す説明文 | 直った | 2 章に `verify_unity_ios.sh` のコメント、`common.md`、topic の README、実装の記録を足した。4 章に remarks と Dialog の summary を足した。grep で、ほかに「ゲートの外」と書いた所は無いことを確かめた |
| S-9 数値の出典 | 直った | 1.1 を、実装の記録 4.3 の値「Dialog で y 18〜64」にそろえ、出典も書いた |

---

**R2-1 [A1] [正しさ／要件]** D-7 のとおりにすると、横の Notification と Share で結果の表示が読めなくなる
- 根拠:
  - 1.2 の要件に「画面の機能（ボタン、結果の表示、エラーの表示）は変えない」とある。3 章でも「結果の表示」は維持するものに入っている。
  - D-7 は見出しに `max-height: 50%` だけを入れる。余白を当てた後の横の高さは iPhone 16 で 233（XS で約 223）なので、見出しは 116.5（XS で約 111.5）までになる。
  - `IosNotificationManagerExampleStyle.uss` の値で数えると、見出しの中身は次のとおり。
    - padding 16＋`HomeButton`（`min-height: 46px`）＝62
    - gap 12、題の margin-top 8 と字の高さ約 24、margin-bottom 4 で、約 110
    - 次の gap 12 の後に説明（12px）と結果の枠（`ResultTextBlock`、約 35）が続き、約 209 になる
  - 下の padding を除いた見出しの中は約 84.5 しかなく、結果の枠より前で尽きる。Share も同じ形（`.ios-share-header` は padding 16・gap 12、説明はもっと長い）。
  - 写す元の Clipboard は、`max-height: 50%` のほかに 3 つの手当てをしている。説明は About の節（スクロールの中）に移した。結果の枠は高さを 64 に決め、`flex-shrink: 0` を付けた。中の文は ScrollView で送る。USS のコメントにも「flex-shrink: 0 keeps the header from compressing it」とある（`IosClipboardManagerExampleStyle.uss`:10-13、31-42）。D-7 はこのうち `max-height` と `min-height` しか写していない。
  - Notification の `.ios-result-border` と Share の `.ios-share-result-border` には `flex-shrink` が無いので、UI Toolkit の既定の 1 になる。D-7 の「はみ出る分は見出しの中で切る」に当たる `overflow: hidden` は、D-7 にも Clipboard の USS にも無い。
- 壊れ方:
  - 横の Notification と Share で、ボタンを押した後の結果（`ResultTextBlock`）が読めなくなる。見出しの子が縮められて字が題・説明と重なるか、見出しの外にはみ出して後ろのスクロールに隠れる。今は見出しが伸びるので、横でも結果は読める。
  - 縦でも、見出しの上限は約 237 になる。長いエラーの文（複数行）が出ると、結果の枠が縮められて字が重なる。今は見出しが伸びて全部読める。
  - テストは通る。サンプルのテストは `Label.text` を読むだけで、見えるかは見ない。D-5 の (a) と (b) はボタンと余白しか見ない。
  - 検証でも捕まらない。V-5 は「窓がボタンの高さ以上」だけを見る。5 章の手の確認にも、結果の表示の項目が無い。
- 直し方の案:
  - Clipboard の形を全部写す。説明はスクロールの中へ移す。結果の枠は高さを決めて `flex-shrink: 0` にし、中に ScrollView を置く。見出しには `overflow: hidden` を付ける。
  - それでも、横の見出しは約 16＋46＋12＋36＋12＋64＋16＝202 で、116 には収まらない。横でも結果を見せるなら、次のどれかが要る。
    - 題を見出しから外す
    - 見出しの padding と gap を詰める
    - 横だけ見出しとスクロールを左右に並べる
  - 横では結果を見せないと割り切るなら、1.2 の要件を改めて、利用者の判断として記録する。
  - どちらにしても、配置の確かめに「`ResultTextBlock` の枠が、見出しの `worldBound` とセーフエリアの中にあり、中心の `panel.Pick` で自分か枠に当たる」を足す。5 章には「縦と横で結果の表示が読める」を足す。

**R2-2 [A2] [テスト]** D-5 の (b) を足すと、Dialog の横（SL04）が落ちる。D-7 が Dialog を外しているため
- 根拠:
  - Dialog の見出し（`IosDialogManagerExampleStyle.uss`）は `flex-shrink: 0` で、`max-height` が無い。中身は次のとおりで、約 197〜200 になる。
    - padding 16×2
    - `HomeButton`（`height: 46px`＋margin-bottom 10）
    - 題（20px、margin-bottom 20）
    - 結果の枠（margin 10＋20、中の文が約 35）
  - 実装の記録 4.3 の縦の `HomeButton` が y 18〜64 で、この見積もりと合う。
  - 余白を当てた後の横の高さは 233（XS で約 223）なので、スクロールの窓は約 33〜36（XS で約 23〜26）になる。`.ios-dialog-button` は `height: 46px`。
  - 前の回の S-3 で「Dialog は通る見込み」としたのは、中心の `Pick` で照らす今の CheckLayout を前提にした場合だった。v2 で足した (b)「送った後に `contentViewport.worldBound` の中にある」は、窓がボタンより低いと必ず落ちる。
  - 2 章では、Dialog の USS を「非変更」にしている。
- 壊れ方: SL04（Dialog の横）が、シミュレーターでも実機でも (b) で落ちる。テストの失敗として表に出る。
- 直し方の案:
  - D-7 の対象に Dialog（`.ios-dialog-header` と `.ios-scroll`）を足し、2 章の「非変更」から Dialog の USS を外す。
  - そうすると R2-1 と同じ「結果の表示」の問題が Dialog にも出る。R2-1 の直し方を 3 つの画面にそろえて当てる。

**R2-3 [C] [記述の整合]** 4 章のトップのメニューの USS の例が、D-3（子孫セレクター）と食い違う
- 根拠:
  - D-3 は `.top-scroll #unity-content-container`（子孫）。
  - 4 章 77 行目は、v1 の `.top-scroll > #unity-content-viewport > #unity-content-container`（子の結合子）のまま。
  - レビュー v1 の 2 章の表は、S-2 の直す場所を「D-3、4 章」としている。
- 直し方の案:
  - 4 章の例を D-3 にそろえる。
  - 4 章のとおりに書いても、テストは通る。横の iPhone では中身が幅いっぱいに広がり、寄りの違いが出ないため。ただ、縦の中央寄せと広い窓の左寄りは V-2 で見えるので、A1 ではない。

**R2-4 [C] [記述の整合]** 件数と、2 章のナビゲーターの行が v2 の決定に合っていない
- 根拠:
  - 1.2 は「`IosSampleLayoutPlayerTests` の 10 件がすべて通る」のまま。D-5 と 4 章で SL11 を足したので 11 件になる。
  - 2 章のナビゲーターの行は「`ApplyScreen` が UIDocument の GameObject に `IosSampleSafeArea` を付ける」のまま。D-2 と 4 章で決めた公開の口 `EnsureSafeArea(UIDocument)` に触れていない。
- 直し方の案: 1.2 を 11 件に直す。2 章のナビゲーターの行を「`EnsureSafeArea` を足し、`ApplyScreen` の最後で呼ぶ」に直す。

## 2. 判断（こちら）

A1 が 1 件あるので止めない。v3 を作り、3 回目（差分）に回す。

R2-1 と R2-2 の根拠は USS で確かめた（Notification と Share の結果の枠に `flex-shrink` も `overflow` も無い。Dialog の見出しは `flex-shrink: 0` で `max-height` が無い）。レビュアーの案のうち「Clipboard の形を全部写す」は、レビュアー自身が数えたとおり、横の見出しが約 202 で上限の約 116 に収まらない。Clipboard 自身も、見出しの中身（Home・題・結果 64・状態の行）が横の 50% を超える。

そこで、D-7 を「横の間だけの見出しの形」に替える。縦は今の見出しのまま変えない（縦は今も通っていて、長いエラーでも見出しが伸びて読める）。

| 指摘 | 区分 | 判断 | v3 の場所 |
|---|---|---|---|
| R2-1 横で結果が読めない | A1 | 受け入れる。`IosSampleSafeArea` が、パネルが縦より横に長いあいだルートに class `ios-sample-landscape` を付ける。4 つの画面の USS は、その class の下でだけ、見出しを横並び（Home ボタン｜結果の枠。Clipboard は結果の下に状態の行）にし、題と説明を隠す。縦の USS は変えない。配置の確かめに (c)「結果の枠が見出しとセーフエリアの中にあり、中心の `Pick` で枠か字に当たる」を足し、5 章に「縦と横で結果が読める」を足す | 2 章、D-7、4 章、5 章、V-5 |
| R2-2 Dialog の横 | A2 | 受け入れる。D-7 は 4 つの画面すべてに当てる。2 章の「非変更」から USS を外す | 2 章、D-7 |
| R2-3 4 章の USS の例 | C | 受け入れる（D-3 にそろえる） | 4 章 |
| R2-4 件数とナビゲーターの行 | C | 受け入れる | 1.2、2 章 |
