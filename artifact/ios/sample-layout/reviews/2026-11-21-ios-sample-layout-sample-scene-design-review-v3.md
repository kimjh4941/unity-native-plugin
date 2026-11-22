# iOS のサンプルのセーフエリアの計画 v3 のレビュー v3（差分）

- 記録日: 2026-11-21
- 対象: [../designs/2026-11-21-ios-sample-layout-sample-scene-design-v3.md](../designs/2026-11-21-ios-sample-layout-sample-scene-design-v3.md)（v2 からの差分と、レビュー v2 の指摘が直ったか）
- レビュアー: サブエージェント（general-purpose）。Codex（`codex:codex-rescue`）は、サブエージェントの回の時点では使えなかった（v1・v2 と同じ）。原因は CLI が無いことではなく、セッションの PATH に nvm の bin（Codex は `~/.nvm/versions/node/v22.13.0/bin` にある）が無かったこと。VSCode の再起動で PATH が入った後、v3 の全体を Codex に 1 回通した（3 章）
- 1 章と 3 章はレビュアーの出力をそのまま写した。こちらの判断は 2 章にだけ書く

## 1. サブエージェント

数: A1 0 / A2 0 / B 3 / C 1

### 前の回の指摘の判定

| 指摘 | 判定 | 根拠 |
|---|---|---|
| R2-1 横で結果が読めない | 直った | D-7 は見出しに上限を付けなくなった。横のあいだだけ見出しを横並びにし、題と説明を隠す。今の USS の値で数えると、Notification と Share の見出しは「padding＋max(Home 46, 結果の枠 約 35)」で約 62〜80 になり、窓は 120 以上残る。1.2 に「結果とエラーの表示が読める」、D-5 に (c)、5 章と V-5 にも項目を足した。(c) は「結果の枠が見出しの `worldBound` の中にある」を見るので、R2-1 の形（枠がはみ出す・縮められる）は捕まる。ただし (c) が見るのは最初の短い文だけで、そこから R3-1 と R3-2 が残る |
| R2-2 Dialog の横 | 直った | D-7 の対象を 4 つの画面にした。2 章で Dialog の USS を「既存変更」に移し、「非変更」は UXML だけになった。Dialog の横の見出しは「padding＋max(Home 46＋margin 10, 結果の枠 約 35＋margin 30)」で約 81〜97 になり、窓（約 136 以上）はボタン（46）より大きい |
| R2-3 4 章の USS の例 | 直った | 4 章の例は `.top-scroll #unity-content-container` になり、D-3 とそろった |
| R2-4 件数とナビゲーターの行 | 直った | 1.2 は 11 件になった。2 章のナビゲーターの行は「`EnsureSafeArea(UIDocument)` を足し、`ApplyScreen` の最後で呼ぶ」になった。ただ、v3 で (c) を足したので、D-5 の「2 つ足す」が新しくずれた（R3-4） |

---

**R3-1 [B] [テスト／要確認]** 横の Clipboard で長い結果が出ると、結果の枠が次の行に回り、見出しの上限（50%）を超えるおそれがある
- 根拠:
  - D-7 は Clipboard について、結果の枠に `flex-grow: 1` を付け、見出しを `flex-wrap: wrap` にするとだけ書く。`flex-basis` には触れていない。今の USS では、`.ios-clipboard-header` に `max-height: 50%` が残っている（`IosClipboardManagerExampleStyle.uss`:14-22）。横でもそれを外すとは書いていない。
  - flex で何個目から次の行に回すかは、伸ばす前の基準の大きさで決まる。`flex-basis: auto` なら、中身の幅が基準になる。
  - Clipboard の失敗の結果は、1 行の長い文になる。形は `#n [marker] NG code=CLIPBOARD_... message=<ネイティブの文>` で、`IosClipboardSampleResult.FormatFailure` が作る。
  - 要確認: 結果の枠の中の ScrollView（`ResultScrollView`）が、中の Label の幅を自分の中身の幅として返すか。返すなら、上のとおり次の行に回る。
- 壊れ方:
  - 横で Errors の節のボタンを押すと、結果の枠が Home の横に入りきらず、次の行に回る。
  - 見出しの中身は、12＋46＋6＋64＋6＋16（状態の行）＋12 で約 162 になる。上限は約 111.5〜116.5 なので収まらない。
  - 結果の枠の下の端と `StatusTextBlock` が見出しからはみ出し、後ろの兄弟であるスクロールの下に隠れる。
  - テストは通る。(c) は最初の文（約 57 字、約 320px）で見るので、枠は Home の横に収まる。それに、(c) は状態の行を見ない。5 章の手の確認では、横の Clipboard でエラーを出せば分かる。
- 直し方の案:
  - 横の結果の枠を `flex: 1 0 0`（基準 0）にする。状態の行は `width: 100%` にして、行の分け方を決めてしまう。
  - Clipboard の横で、`max-height: 50%` を残すか外すかを D-7 に書く。
  - (c) の前に、ドライバーが `ResultTextBlock.text` に長い文（数百字、改行を含む）を入れ、(b) と (c) をもう一度見る形を足す。`StatusTextBlock` があれば、それも見出しの中にあるかを見る。

**R3-2 [B] [要件／テスト]** 「長い結果では見出しが伸びる」を、Notification の一覧の結果に当てると、横で窓がボタン 1 つより小さくなる
- 根拠:
  - D-7 は「結果の枠は高さを決めず、長いエラーでは見出しが伸びる」とし、理由に「窓は 120 以上残る」を挙げる。この理由は、結果の文が短いときにしか成り立たない。
  - Notification の `GetScheduled` と `GetDelivered` は、ネイティブの JSON をそのまま結果に出す（`IosNotificationManagerExampleController.cs`:623、639）。
  - その JSON は、溜まっている通知を全部並べ、1 件ごとに identifier・title・subtitle・body・categoryIdentifier・userInfo を持つ（native-toolkit `NotificationRepositoryImpl.swift`:187-197）。
  - `ShowLocation` と `ScheduleLocation` の通知は、場所に入るまで溜まったままになる。`GetDelivered` は、配信した通知の数だけ長くなる。
- 壊れ方:
  - 横の幅（約 530px、1 行に約 80 字）で、3〜4 件の一覧が 10 行ほどになると、見出しは約 190〜200 になる。
  - 窓は 233 −見出しなので、XS では 46 を下回る。1.2 の「縦と横の両方で、すべてのボタンが…届く」が、長い結果のあいだは崩れる。Home ボタンは見出しの中にあるので、画面を出れば戻せる。
  - 今の横（見出し 約 209）より悪くはなっていない。
  - テストは最初の文でしか見ないので、通る。
- 直し方の案: どちらかに決め、1.2 か D-7 に書く。
  - 1.2 の要件に「結果が長いときは、窓が狭くなるのを受け入れる」と書く（利用者の判断）。
  - 横では、結果の枠に上限を付け、中に ScrollView を置く（Clipboard の形。UXML の変更が要るので、2 章の「非変更」も直す）。
  - どちらにしても、R3-1 の「長い文を入れて (b) と (c) を見る」で、どこまで伸びるかを数値で残す。

**R3-3 [B] [検証の穴]** Home ボタンの幅を決める規則が、スクロールの中のボタンにも当たりうる
- 根拠:
  - Notification・Share・Clipboard の UXML では、`HomeButton` の class は `.ios-secondary-button` で、同じ class がスクロールの中のボタンにも付いている。
    - Notification: 19、34-37、45-46、58、64 行
    - Share: 47-50 行
    - Clipboard: 28、30-31、37 行ほか多数
  - D-7 は「Home ボタンは幅を決めて縮めない」と書くだけで、どのセレクターで当てるかを書いていない。
- 壊れ方:
  - `.ios-sample-landscape .ios-secondary-button { width: …; flex-shrink: 0; }` と書くと、横のあいだ、スクロールの中のグレーのボタンも同じ狭い幅になる。
  - 長い名前（`Copy Multi Representation (empty) -> EMPTY_ITEMS` など）が何行にも折り返す。
  - ボタンはセーフエリアの中にあり、中心で押せるので、(b) は通る。5 章にもこれを見る項目は無い。
- 直し方の案:
  - D-7 に、Home ボタンだけに当てると書く。例: `.ios-sample-landscape #HomeButton`、または `.ios-sample-landscape .ios-notif-header > .ios-secondary-button`。
  - 5 章に「横でも、スクロールの中のボタンは縦と同じく幅いっぱい」を足す。

**R3-4 [C] [記述の整合]** D-5・D-1 の書き方が、v3 で足した部分と合っていない
- 根拠:
  - D-5 は「`CheckLayout` に 2 つ足す」のまま、(a)・(b)・(c) の 3 つを並べている。理由の列にも (c) と R2-1 が無い。
  - D-1 で、class の文を padding の文の後に入れた。そのため、`（RuntimePanelUtils.ScreenToPanel。画面の y は上向き…）` の括弧が、class の文に付いて読める。
  - (c) の「`ResultTextBlock` の枠」がどれを指すかが曖昧。
    - トップのメニュー（SL01・SL02・SL11）には `ResultTextBlock` が無い。
    - Clipboard では、Label の親は ScrollView の `#unity-content-container` で、`.ios-clipboard-result-border` ではない。
- 直し方の案:
  - D-5 を「3 つ足す」にし、理由に R2-1 を足す。
  - D-1 の座標の括弧を、padding の文の直後に戻す。
  - (c) に「`ResultTextBlock` が無い画面では飛ばす」と書く。「枠」は、`ResultTextBlock` を含む見出しの直下の子、と決めて書く。

## 2. 判断（こちら）

差分レビューで A1 が 0 になったので、決まりどおりここで止める（3 回）。機械照合（`check_design_consistency.py`）は v3 で FAIL 0。レビュアーを替えた回として、v3 の全体を Codex に通し、指摘は 0 件だった（3 章。Codex のジョブの記録で、計画・前の回の記録・ナビゲーター・Dialog の Controller・4 画面の USS・ゲートのスクリプトを読んだことを確かめた）。止める条件の 1〜3 を満たす。直さない残件は、計画 v3 の 7 章に理由つきで書いた。

| 指摘 | 区分 | 判断 | 行き先 |
|---|---|---|---|
| R3-1 横の Clipboard の長い結果 | B | 受け入れる。実装で、横の結果の枠を基準 0（`flex: 1 0 0`）、状態の行を `width: 100%` にし、横の見出しの `max-height` の扱いを決める。ドライバーに「長い文（数百字、改行を含む）を入れて (b)・(c) を見直す」を足し、`StatusTextBlock` も見る | 実装の記録のチェックリスト |
| R3-2 Notification の長い一覧 | B | 今回は「長い結果のあいだは横で窓が狭くなるのを受け入れる」とする（今より悪くならず、Home ボタンは見出しの中に残る。結果の枠を送れる形にするには UXML を変える）。R3-1 の長い文の確かめで、どこまで伸びるかを数値で残す。利用者に報告し、直すかは利用者が決める | 計画 v3 の 7 章、利用者への報告 |
| R3-3 Home ボタンのセレクター | B | 受け入れる。実装で `#HomeButton` だけに当てる。手の確認に「横でもスクロールの中のボタンは幅いっぱい」を足す | 実装の記録のチェックリスト |
| R3-4 記述のずれ | C | 実装で読み替える（D-5 は 3 つ、D-1 の座標の括弧は padding にかかる、(c) は `ResultTextBlock` の無いトップのメニューでは飛ばし、「枠」は `ResultTextBlock` を含む見出しの直下の子） | 実装の記録のチェックリスト |

## 3. Codex（v3 の全体。レビュアーを替えた回）

数: A1 0 / A2 0 / B 0 / C 0

指摘なし。
