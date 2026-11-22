# サンプルの画面の狭い画面での崩れの直しのレビュー v1

- 記録日: 2026-11-22
- 対象: 未コミットの変更（12:15 の時点）。トップメニューと iOS の 4 画面の UXML・USS、配置のテスト（SL00、CheckLabels、横の位置の戻し）。この時点では、controller の結果の先頭への戻しは入っていない
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）を並行で
- 1 章と 2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. Codex

1. [P2] 新しい結果に前回のスクロール位置が残る。
   Dialog・Share・Notification は `ResultScrollView` を追加していますが、更新処理は文字列の代入だけです。
   - /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Dialog/IosDialogManagerExampleController.cs:379
   - /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Share/IosShareManagerExampleController.cs:422
   - /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Notification/IosNotificationManagerExampleController.cs:941

   長い結果を下へスクロールしたあとに次の長い結果を受け取ると、先頭の成功・失敗表示などが隠れます。
   修正: 各結果更新時に `ResultScrollView.scrollOffset = Vector2.zero` を設定する。

2. [P2] 結果欄の中身が非表示でもレイアウトテストが通る。
   `/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSampleScreenDriver.cs:298` の `CheckResult` は、ヘッダー直下の外枠だけを検査します。中央のヒットも外枠自身なら成功します。今回の固定高により、内部の `ResultScrollView` が `display: none` でも、枠の高さとヒット判定は成立します。結果ラベルは同ファイル 327 行目で `CheckLabels` から除外されるため、長文チェックもこの破損を検出できません。
   修正: 結果の viewport とラベルの表示・寸法、および長文末尾までスクロールできることを検査する。

3. [P2] 折り返したタイトルの縦方向の欠けを検出できない。
   `/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSampleScreenDriver.cs:343` は `NoWrap` の場合だけ文字サイズを検査します。今回 `white-space: normal` にしたタイトル・サブタイトルは、正の高ささえあれば、例えば高さ1pxで文字がクリップされていても合格します。`SL00` も同じ判定を使います。
   修正: 折り返すラベルも実際の内容幅で必要な文字高さを測り、`contentRect.height` と比較する。

補足: Codex の調査では、次の問題は見つかりませんでした。
- 旧要素階層に依存したコントローラーやテストコードの参照切れ。コントローラーは名前による子孫検索を使い、`IosSampleSafeArea` はルートのみを扱います。
- C# のコンパイルエラー（`MeasureTextSize` のシグネチャ、`WhiteSpace` enum、nullable）。
- USS のセレクター不一致や Unity 6 で未対応のプロパティ。
- Android、Windows、macOS のドライバーやテストの破損。

ただし、`MeasureTextSize` と `WhiteSpace` の確認は、Unity のインストール先を grep した結果が失敗終了しており、裏付けは取れていません。Codex は Unity を実行しておらず、読み取りと grep だけで確認しています。

## 2. サブエージェント

### A: テストでは見つからない、実際の欠陥

**A-1**
- 区分: A
- 場所: `Runtime/UI/iOS/Dialog/IosDialogManagerExampleController.cs:377-470`（結果を書く 6 つのハンドラー）、`Runtime/UI/iOS/Notification/IosNotificationManagerExampleController.cs:937-943`（SetResult）、`Runtime/UI/iOS/Share/IosShareManagerExampleController.cs:418-424`（SetResult）
- 指摘: 3 つの画面は結果を `ResultScrollView` に入れたのに、新しい結果を出すときにスクロール位置を 0 に戻していません。利用者が長い結果を下まで送ったあと、次の長い結果が出ると、古い位置のまま表示されます。そのため 1 行目（"OK"/"Error"、"✓/✗ RequestPermission" など）が上に隠れます。
- 根拠: Clipboard は `_resultScrollView.scrollOffset = Vector2.zero` で戻しています（`IosClipboardManagerExampleController.cs:1411-1416`）。その理由もコメントに「a new result inherits the previous scroll position and its first lines would be hidden」と書かれています。結果が短く、枠からあふれない場合は、Scroller の範囲が狭まって位置が 0 に寄せられるので目立ちません。ただし Dialog の結果は "OK\nShowConfirmDialog buttonText: …, isSuccess: …, errorMessage: …" です。13 mini（枠の文の幅は約 150〜160、高さ 64 から padding 20 を引いた約 44）では 4〜5 行になり、ほぼ毎回あふれます。Notification の権限案内の文も長いです。テストは `result.text` を直接書き換えるだけで、controller も scrollOffset も通らないので、これを見つけられません。
- 提案: 3 つの controller で `root.Q<ScrollView>("ResultScrollView")` を持ち、結果を書くたびに `scrollOffset = Vector2.zero` にします。Dialog は 6 か所の代入を SetResult 1 つにまとめます。

### B: テストで見つかる、または確かめが足りない

**B-1**
- 区分: B
- 場所: `Tests/PlayMode/IosSampleScreenDriver.cs:284-312`（CheckResult）、`:327`（CheckLabels が ResultLabel を除く）
- 指摘: 枠の高さが 64/52 に固定されたので、長い結果を入れて確かめても、見ているのは大きさの変わらない枠（`ios-result-border`）だけです。結果の文が枠の中に収まっているか、送って読めるかは誰も見ていません。たとえば `ResultScrollView` を外して Label を固定高さの枠に直接置いても、文は枠からはみ出しますが、テストはすべて通ります。CheckLabels も結果を除いています。
- 根拠: CheckResult が見るのは box と header の矩形、box の高さが 20 以上か、中心を Pick したときに box に当たるか、だけです。UXML の入れ子を固定しているのは Clipboard の wiring テスト（`Tests/Runtime/IosClipboardSampleSceneWiringTests.cs:175-189`）だけで、Dialog、Share、Notification には同じテストがありません。
- 提案: CheckResult に次のどちらかを足します。結果の Label の先祖に box の中の ScrollView があり、文の高さが窓より高いときは縦の Scroller が効くこと。あるいは、文の高さが窓以下であること。または、Clipboard の `ClipboardUxml_ContainsResultScrollView` と同じ形の wiring テストを 3 つの画面に足します。

**B-2**
- 区分: B
- 場所: `Tests/PlayMode/IosSampleScreenDriver.cs:323-350`（CheckLabels）
- 指摘: 今は折り返さない（NoWrap の）Label の確かめが、どの画面でも 1 つの Label にも当たりません。そのため、ほぼ横の窓のはみ出しを見るだけの確かめになっています。取りこぼすものは 3 つです。
  1. 折り返す Label が切れる場合（高さが決まっている、または区切れない長い語）。
  2. Button の文字。Button は TextElement で Label ではないので `Query<Label>` に入りません（たとえば `.ios-dialog-button` は nowrap のままです）。
  3. ScrollView の外の Label（見出しの題、説明、Clipboard の状態の行）が、セーフエリアや見出しから横にはみ出す場合。
- 根拠: iOS の 4 画面の Label は 46 個で、クラスは 14 種類です。そのすべてが `white-space: normal` です（Dialog/Share/Notification/Clipboard の USS と、今回のトップのメニューの title/subtitle）。誤って失敗する心配はほぼありません。隠れた Label は自分の display:none か、Yoga が大きさを 0 にしてから HiddenByAncestor で除かれます。visibility は継承されます。Scroller の中の Label は text が空です。iPhone 16 は幅が広い（約 246）ので、さらに通りやすくなります。MeasureTextSize は content の大きさを返すので、`contentRect` と比べるのは正しいです。
- 提案: `white-space: normal` の Label は `MeasureTextSize(text, contentRect.width, Exactly, 0, Undefined)` の高さを `contentRect.height` と比べます。対象を `Query<TextElement>()` に広げ、Button の文字も入れます。ScrollView の外の Label は safePanel の左右と比べます。

**B-3**
- 区分: B
- 場所: `Runtime/Resources/UI/Top/TopMenuExampleStyle.uss:24-63`
- 指摘: トップのメニューは Android、Windows、macOS でも共通ですが、今回の変更は iOS のシミュレーターでしか確かめていません。設計 v3 の V-2 は「広い窓（macOS の Editor の Game ビュー）で中央寄せが保たれるかをスクリーンショットで見る」としています。今回、content container の幅と title の折り返しを変えたので、もう一度見ておくべきです。
- 根拠: 他のプラットフォームのテストは壊れません。Android と Windows のテストは `NavigationSubmitEvent` で押し、`FindButton` で探すので、レイアウトに依りません。wiring テスト（Android Share、Mac Clipboard、Windows Clipboard）はボタンの名前があるかだけを見ています。デスクトップで変わるのは、ボタンの幅が 420 から約 414 になる（テーマの左右の margin が内側に入る）ことだけと見込みます。Android の縦長の端末も Constant Physical Size なら、同じ切れ方をしていた可能性があり、今回の変更で直るはずです。ただし確かめてはいません。
- 提案: ゲートが終わったあと、Editor の Game ビュー（広い窓と縦長）と、Android の端末かエミュレーター 1 台で、トップのメニューを目で見ます。

### C: 文言

**C-1**
- 区分: C
- 場所: `Resources/UI/iOS/Dialog/IosDialogManagerExampleStyle.uss:159`、`Share/IosShareManagerExampleStyle.uss:143`、`Notification/IosNotificationManagerExampleStyle.uss:143`、`Clipboard/IosClipboardManagerExampleStyle.uss:160`
- 指摘: 横向きの規則のコメント「The result keeps its full text: the header grows with it.」が古くなっています。今は横でも枠は 52 に固定で、中が送れます。Clipboard では以前から合っていませんでした。
- 提案: 「The result box is 52 tall and its text scrolls inside.」などに直します。

**C-2**
- 区分: C
- 場所: `Tests/PlayMode/IosSampleScreenDriver.cs:222-224`
- 指摘: 「The header may then leave the button list short: that is accepted (R3-2), so its height is only logged.」は 2 つの点で合っていません。235 行で 44 未満を problem に足していること（今回の差分の前から）と、今は結果の枠が固定なので見出しが伸びないことです。
- 提案: コメントを今の動きに合わせます。

**C-3**
- 区分: C
- 場所: `Resources/UI/Top/TopMenuExample.uxml:4`
- 指摘: `mode="Vertical"` は ScrollView の既定の値なので、レイアウトは変わりません。変更のまとめでは直した点の 1 つに見えます。
- 根拠: ScrollView は既定で Vertical で作られ、UXML の `mode` 属性の既定も Vertical です。
- 提案: 意図を明示するために残すならそのままでかまいません。直した点として記録するなら、この行は除きます。

**C-4**
- 区分: C
- 場所: `Resources/UI/Top/TopMenuExampleStyle.uss:20-23`
- 指摘: 「the iPhone XS leaves the menu about 170 wide」の数が XS のものではありません。
- 根拠: XS は 1125px、458ppi なので、パネルの幅は 1125×96/458 ≈ 236 です。`.top-root` の padding 24×2 を引くと約 188 になります。約 170 になるのは 13 mini（1080px、476ppi → 約 218 → 約 170）です。
- 提案: 「about 188 on the iPhone XS, 170 on the 13 mini」とします。

**C-5**
- 区分: C
- 場所: `Tests/PlayMode/IosSampleLayoutPlayerTests.cs:51-54`
- 指摘: SL00 の説明「on the iPhone XS … cut at both sides at launch while SL01, which sets the orientation first, passed」は、原因を向きの設定にしています。しかし変更前の CheckLayout には CheckLabels がなく、ScrollTo が横にも動いてボタンのはみ出しを隠していました。SL01 が XS で通ったのは、確かめる側が見えていなかったためである可能性があり、裏付けがありません。今の確かめ方で、変更前のトップのメニューに対して SL01 も失敗するなら、SL00 は SL01 とほぼ同じ確かめになり、説明が誤解を招きます。
- 提案: ゲートのあとで、変更前のレイアウトに今の SL01 を当てて確かめます。それができるまでは、説明を事実だけ（XS では起動直後に切れていた）にとどめます。

**C-6**
- 区分: C
- 場所: `artifact/ios/sample-layout/designs/2026-11-21-ios-sample-layout-sample-scene-design-v3.md:72`（D-7）、`artifact/ios/sample-layout/results/2026-11-21-ios-sample-layout-implement-sample-scene-result-v1.md:30-31`（R3-1、R3-2）、`manual/1.14.0/images/ios/*`
- 指摘: 設計 D-7 の「結果の枠は高さを決めず、長いエラーでは見出しが伸びる」と、結果 v1 の「見出しの max-height: 50% は残した」を、今回逆にしています。その判断がどこにも記録されていません。また manual/1.14.0 の iOS の画像（dialog、share、notification、clipboard）は 1.13.0 と同一のコピーです。11-21 のセーフエリアの対応も、今回の固定の結果欄も写っていません。
- 根拠: `diff -rq manual/1.13.0/images/ios manual/1.14.0/images/ios` で同一でした（c492594 で写しただけ）。Samples~ にはシーンが 1 つあるだけで、UXML/USS の写しはありません。Documentation~ にも、これらの画面の説明はありません。
- 提案: 結果の記録に v2 を作って、D-7、R3-1、R3-2 を変えたことと理由（13 mini）を残します。1.14.0 のリリースの前に、iOS のスクリーンショットを撮り直します。

### 確かめて指摘がなかったところ
- (1) 9 つの controller（iOS、Android、macOS、Windows）は、どれも `root.Q<Label>("ResultTextBlock")`（子孫を探す）で結果を見つけます。今回の入れ子の変更の影響はありません。`IosSampleSafeArea.PaddingColor` は `root[0]`（画面の一番外の要素）だけを見るので、影響はありません。CheckResult は ScrollView の内側の要素をたどって上がり、`-header` で終わる class を持つ要素で止まるので、box は結果の枠になります。一覧は `!view.Contains(result)` で ResultScrollView が除かれ、その次の ScrollView が選ばれます。Navigator と TopMenu の controller は名前でボタンを探すだけです。
- (5) 横の位置を 0 に戻す処理は、ScrollTo で横に送られて見えなくなっていたはみ出しを、利用者が最初に見る位置（x=0）で検出するためのものです。iOS の画面に横に送る必要のある ScrollView はないので、新しく問題を作ることはありません。縦を合わせ直す処理（`+= (0, down-up)`）も x を 0 のまま保ちます。

### 件数
- A: 1
- B: 3
- C: 6
- 合計: 10

## 3. 判断

| 指摘 | 扱い |
|---|---|
| A-1、Codex 1 | 直した。3 つの controller が `ResultScrollView` を持ち、結果を書くたびに先頭へ戻す（Dialog は 6 か所の後に `ScrollResultToTop()`） |
| B-1、Codex 2 | 直した。CheckResult は、結果のラベルが欄の中の ScrollView に入っていること、文字が見えること、幅がその view に収まること、view より長いときに末尾までスクロールできることを見る |
| B-2、Codex 3 | 直した。CheckLabels は `TextElement`（ボタンの文字を含む）を見る。ScrollView の外はセーフエリアの左右と比べる。折り返す文字は、今の幅で要る高さを枠の高さと比べる |
| B-3 | 残件。他のプラットフォームの見た目は確かめていないと記録に書いた（結果の記録の 5 章） |
| C-1、C-2、C-4、C-5 | コメントを直した |
| C-3 | 意図を明示するために残す。記録では「既定の値と同じ」と書いた |
| C-6 | 結果の記録（[../results/2026-11-22-ios-sample-layout-narrow-screen-fix-result-v1.md](../results/2026-11-22-ios-sample-layout-narrow-screen-fix-result-v1.md)）に D-7・R3-1・R3-2 を逆にしたことと理由を書いた。マニュアルの iOS の画像の撮り直しは、リリースの前の作業として利用者に伝える |
