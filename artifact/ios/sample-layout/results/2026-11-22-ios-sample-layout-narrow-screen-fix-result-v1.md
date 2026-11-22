# サンプルの画面の狭い画面での崩れの直しの記録 v1

- 記録日: 2026-11-22
- 親: [2026-11-21-ios-sample-layout-implement-sample-scene-result-v1.md](2026-11-21-ios-sample-layout-implement-sample-scene-result-v1.md)（セーフエリアの対応）、設計 [v3](../designs/2026-11-21-ios-sample-layout-sample-scene-design-v3.md)
- きっかけ: 利用者が iPhone XS（iOS 18.7.2）に Unity から入れたサンプル（iOS Profile）で、トップメニューの左右が切れていた（スクリーンショット）。自動の配置のテスト（SL01）は同じ端末で通っていた

## 1. 何が起きていたか

### 1.1 トップメニュー（全プラットフォームで共通）

- `CommonPanelSettings` は Constant Physical Size（基準 96 DPI）。iPhone XS（458 ppi）ではパネルの幅が約 236、余白を引いたメニューの幅は約 188。iPhone 13 mini（476 ppi）では約 218 と約 170
- 11-21 にメニューを ScrollView で包んだ（`01a1545`）。その結果、次の 2 つが起きた
  - 題名と説明は折り返さない（`white-space` の既定）ので、文字の幅（題名 192、説明 214）が枠（161）からはみ出し、左右が切れた
  - ボタンは `width: 100%` に、テーマの左右の margin（各 4）が足され、表示の範囲より広くなった。包む前は親が切り落とさなかったので目立たなかった
- テストが見逃した理由: ボタンの確かめは `ScrollTo(button)` の後に行っていた。`ScrollTo` は横にも送るので、利用者が見ない横の位置でボタンが範囲に収まっていた。また、ラベルは確かめていなかった

### 1.2 iOS の 4 つのサンプルの画面（iPhone 13 mini で）

| 画面 | 崩れ |
|---|---|
| Dialog、Share、Notification | 長い結果で見出しが伸び、ボタンの一覧が 0（横向きは 17）まで潰れた |
| Share | 長い結果で、結果の欄がセーフエリアの外へ出た（縦向き） |
| Clipboard | 見出しの `max-height: 50%` の中に収まらず、状態の行が見出しの外へ出た（縦向き） |

XS の実機の回（今朝）では通っていた。13 mini はパネルがさらに狭い。12 mini・13 mini の実機でも起きる見込み。

## 2. 直したこと

| 対象 | 直し |
|---|---|
| トップメニュー（`Runtime/Resources/UI/Top`） | ScrollView の中身の入れ物を `width: 100%` にし、題名と説明を `white-space: normal`（折り返す）にした。ボタンは `width: 100%` をやめ、親の幅に合わせて伸ばす。ScrollView に `mode="Vertical"` を明示した（既定の値と同じ。意図を書くため） |
| Dialog の画面のボタン | 文字が折り返し、高さが最低 46 で中身に合わせて伸びる（Home のボタンも）。4 章 |
| Dialog・Share・Notification の画面 | 結果のラベルを `ResultScrollView` で包み、結果の欄を高さ 64（横向き 52）に固定した（Clipboard の画面の形）。各画面の controller は、新しい結果を出すたびに `ResultScrollView` を先頭に戻す |
| Clipboard の画面 | 見出しの `max-height: 50%` と、一覧の `min-height: 50%`（対になっていた下限）を外した。結果の欄は固定の高さなので、見出しが伸びすぎることはない |
| 配置のテスト | SL00（起動したままのトップメニュー）を足した。ボタンの確かめは `ScrollTo` の後に横の位置を 0 に戻す。ラベルとボタンの文字（`TextElement`）について、横の範囲（ScrollView の中は viewport、外はセーフエリア）、折り返さない文字の幅、折り返す文字の高さを見る（`CheckLabels`）。結果の欄は、中の ScrollView に入っていること、文字が見えること、幅、末尾までスクロールできることも見る |

設計 v3 の D-7（結果の枠は高さを決めず、長いエラーでは見出しが伸びる）と、前回の記録の R3-1・R3-2（Clipboard の見出しの `max-height: 50%` は残す）を、この直しで逆にした。理由は 1.2 のとおり、狭い画面でボタンの一覧が潰れるため。

## 3. 確かめたこと

- 直す前の形に、新しい確かめ（SL00 と CheckLabels）を当てて、13 mini で失敗することを確かめた（12 件中 11 件が失敗。トップメニューの崩れを SL00・SL01 などが捕まえた）
- iPhone 13 mini（iOS 18.3、一時的に作ったシミュレーター）: 配置のテスト 12 件がすべて成功。起動した直後のトップメニューを撮り、題名と説明が折り返し、ボタンが丸い角まで収まっていることを目で見た
- iPhone 16（iOS 18.3 と 26.2）: ゲート全体（Player 115 件、Notification のサンプル 8 件など）が成功（controller の直しとテストの強化の前の回。直した後の回は 4 章）

## 4. 直した後の回

実装のレビュー v1（[../reviews/2026-11-22-ios-sample-layout-narrow-screen-fix-review-v1.md](../reviews/2026-11-22-ios-sample-layout-narrow-screen-fix-review-v1.md)）を直した後（controller の先頭への戻し、CheckResult と CheckLabels の強化）。

- 強めたテストが、Dialog の画面で新しい崩れを 2 つ見つけた。ボタンの文字が 1 行に収まらない（「ShowDestructiveDialog」は iPhone 16 でも 154 対 150、13 mini では 3 つのボタン）。横向きの Home のボタンの 2 行の文字が、固定の高さ 46 で下が切れる（13 mini）。Dialog のボタンを Clipboard の画面のボタンと同じく、文字が折り返し、高さが最低 46 で中身に合わせて伸びる形にした
- その後の回（14:00〜14:46）: iPhone 13 mini でサンプルの画面のテスト 47 件（配置を含む）、iPhone 16 / iOS 18.3 と 26.2 で Player 115 件と Notification のサンプル 8 件がすべて成功。EditMode と Editor の PlayMode は、controller の直しの前の回（12:08〜12:38）で成功
- 一時的に作った iPhone 13 mini のシミュレーターは、確かめの後に消した

## 5. 残っていること

- トップメニューは Android・Windows・macOS でも共通。今回は iOS のシミュレーターでしか目で見ていない。他のプラットフォームのテストはボタンを名前で探して押すので壊れないが、見た目（広い窓での中央寄せ、Android の縦長の端末）は確かめていない（実装のレビューの B-3）
- マニュアル 1.14.0 の iOS の画像（Dialog・Share・Notification・Clipboard）は 1.13.0 の写しのまま。11-21 のセーフエリアの対応も、今回の固定の結果の欄も写っていない。~~リリースの前に撮り直す~~ → 撮り直さない（利用者の判断、2026-11-22）
- 実機の回は `a75d802` の前（`a764789`、2026-11-22 朝）が最後。この直しの後はシミュレーターだけ（4 章）
