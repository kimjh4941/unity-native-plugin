# iOS のサンプルのセーフエリアの計画 v1 のレビュー v1

- 記録日: 2026-11-21
- 対象: [../designs/2026-11-21-ios-sample-layout-sample-scene-design-v1.md](../designs/2026-11-21-ios-sample-layout-sample-scene-design-v1.md)
- レビュアー: サブエージェント（general-purpose）だけ。**Codex（`codex:codex-rescue`）は使えなかった**（`Codex CLI is not installed or is missing required runtime support`）。決まりどおり、待たずにサブエージェントの結果だけで判断した
- 1 章はレビュアーの出力をそのまま写した。こちらの判断は 2 章にだけ書く

## 1. サブエージェント

数: A1 1 / A2 2 / B 3 / C 3

ファイルは編集していません。Unity・シミュレーター・ビルドも動かしていません。ScrollView の要素の並びは、Unity 6000.4.2f1 の iOS 用 UIElementsModule.dll にある文字列で確かめました。

---

**S-1 [A1] [正しさ]** 起動した直後のトップのメニューには `IosSampleSafeArea` が付かない
- 根拠:
  - `NativeToolkitExampleScene.unity`:561 で、UIDocument の `sourceAsset` は最初から TopMenuExample.uxml（guid f82ef943…）になっている。579 行目で `TopMenuExampleController` も最初から付いている。
  - `ShowTopMenu` を呼ぶのは、各画面の Home の処理だけ（grep で確かめた）。つまり最初の画面は `ApplyScreen` を通らない。
  - よって D-2 の理由「トップのメニューを含むすべての画面の切り替えが `ApplyScreen` を通る」は、起動した直後の画面には当てはまらない。
- 壊れ方:
  - アプリを起動するかシーンを読み込んだ直後、一度も画面を移っていないうちは余白が当たらない。余白の部分の色も写らない。手の確認の 5 章 1 項めと 3 項めの前提が崩れる。
  - SL01/SL02 は、毎回シーンを読み込み直した直後のトップのメニューを照らす。そのため、部品が付いていないまま照らすことになる。
  - 横（パネル 533×246）では、`.top-root` の 24px の余白と、幅 420 の中央寄せだけで、ボタンは x 50〜470・y 24〜222 に入る。S-2 を直した後なら、部品が無くても SL02 は通る。計画どおりに作ると、テストは通るのに、トップのメニューにはセーフエリアの余白が当たっていない状態になる。
  - V-* にも、これを見る項目が無い。
- 直し方の案:
  - `TopMenuExampleController.Start` で、`#if UNITY_IOS` のときに部品を付ける（無ければ）。または、ナビゲーターに付ける処理だけの入口を作り、トップのメニューの Start から呼ぶ。
  - テストで、余白が当たったこと自体を照らす。例: CheckLayout で `rootVisualElement.resolvedStyle.paddingLeft/Top` が `safePanel` の外の幅と合うか。
  - 「画面 → Home → 回す → トップのメニュー」を照らす 1 件を足す。今は画面を切り替えた後の当て直しが自動では見られていない。

**S-2 [A2] [正しさ／他の OS への影響]** トップのメニューの USS のセレクターが当たらない
- 根拠:
  - Unity 6 の ScrollView の並びは、ScrollView > `#unity-content-and-vertical-scroll-container` > `#unity-content-viewport` > `#unity-content-container`。
  - 6000.4.2f1 の UIElementsModule.dll に `unity-content-and-vertical-scroll-container` があることを確かめた。この並び（container が viewport の親であること）は、Unity のソースの記憶による。
  - 計画の 4 章のセレクターは `.top-scroll > #unity-content-viewport > #unity-content-container`。子の結合子 `>` なので、この要素と一致しない。
- 壊れ方:
  - 中身の器は既定の `align-items: stretch` のままになる。`.top-content`（`max-width: 420px`）は左に寄る。
  - iOS の横では x が 24 から始まり、ノッチの側の余白 37 にかかる。SL02 が落ちる（起動した直後で S-1 の部品が無いとき）。
  - Android の横、タブレット、Windows・macOS の広い窓でも、メニューが左に寄って見た目が変わる。
  - V-2 は「縦のシミュレーターのスクリーンショット」で確かめる計画だが、iPhone の縦はパネルの幅が 246 で 420 より狭い。中身は幅いっぱいに広がるので、寄りの違いが見えず検出できない。
- 直し方の案:
  - 子孫セレクターにする。例: `.top-scroll .unity-scroll-view__content-container`、または `.top-scroll #unity-content-container`。
  - V-2 に、横の iOS と、広い窓（macOS・Windows のどちらか）を足す。
  - `flex-grow: 1` と `min-height: 100%` を器に付けたとき、縮んでスクロールが効かなくならないか（UI Toolkit の既定 `flex-shrink: 1`）も V-2 で見る。確かめていない。

**S-3 [A2] [正しさ／テスト]** 横では、Notification と Share の見出しの部分が高すぎて、スクロールの窓がボタン 1 つより小さくなる
- 根拠:
  - `.ios-notif-header` と `.ios-share-header` は `flex-shrink: 0` で、`max-height` が無い。
  - 中身は次のとおり。見積もり（文字の高さはフォントの大きさ×約 1.2 と置いた）で約 209px。
    - 上下の余白 32
    - HomeButton（min-height 46）
    - 隙間 12×3
    - 題（20px＋余白 12）
    - 説明（12px＋余白 8）
    - 結果の枠（約 35）
  - 余白を当てた後の横の高さは、iPhone 16 で 246−13＝233、iPhone XS で約 236−13＝223（`CommonPanelSettings` は ConstantPhysicalSize、96dpi で計算）。
  - スクロールの窓は、iPhone 16 で約 24px、XS で約 14px になる。ボタンは `min-height: 46px`。
  - CheckLayout の動き（`IosSampleScreenDriver.cs`:152-178）:
    - ボタンの下端をセーフエリアの下端に合わせる。中心は下端から 23px 上になる。
    - 窓の外（`overflow: hidden` で切られた所）の中心を `panel.Pick` すると、見出しかルートの器に当たる。
  - 11-16 の測定では、横は x の外れで `continue` したため（169-175 行目）、`Pick` の照らしは一度も走っていない。
- 壊れ方:
  - 実機（V-3、XS）の SL06/SL10 は「covered by …」で落ちる見込み。シミュレーターは 1px ほどの差で通るか落ちるかの境目。
  - 通っても、横の画面は 20px ほどの窓でしか操作できない。
  - Dialog は見出しが約 195px で、窓が 28〜38px あるので通る見込み。Clipboard は `max-height: 50%` があるので問題ない。
- 直し方の案:
  - 2 章の「iOS の 4 つの画面の UXML・USS は変えない」を改める。
  - Notification と Share の見出しに、Clipboard と同じ形（見出し `max-height: 50%`、スクロール `min-height: 50%`）を入れる。または、見出しの説明・結果の枠もスクロールの中に入れる。
  - V に「横で、スクロールの窓の高さがボタンの高さ以上あるか」を足す。
  - CheckLayout に「ボタンの `worldBound` がスクロールの窓（`contentViewport.worldBound`）の中にあるか」を足すと、半分だけ見えるボタンも拾える。

**S-4 [B] [テスト]** ゲートに入れた後、画面の向きが後のテストに残りうる
- 根拠:
  - `ProjectSettings.asset`:11 は `defaultScreenOrientation: 4`（AutoRotation）。
  - TearDown は `_orientationBefore` に戻して 2 秒待つだけで、縦に戻ったかは確かめない（`IosSampleLayoutPlayerTests.cs`:34-40）。
  - Android の実装のレビュー v3（`artifact/android/dialog/reviews/2026-10-11-android-dialog-implementation-feature-review-v3.md`:59）で、同じ問題が挙がっている。机に置いた端末で AutoRotation に戻しても、元の向きに戻る保証は無い。
  - iOS も、平置き（FaceUp）のときは今の向きを保つ、と私は理解している（未確認）。`Screen.orientation` の値が AutoRotation に対して何を返すかも未確認。
- 壊れ方:
  - 名前の順で後に流れる `IosShareSamplePlayerTests`・`IosSharePlayerTests`・`IosUiDriver*` などが、横のまま流れうる。
  - これまでは `--category IosSampleLayout` だけで流していて、後に続くテストが無かったので表に出なかった。
- 直し方の案:
  - TearDown で、まず `Portrait` にして `Screen.height > Screen.width` になるまで待つ（期限つき）。それから元の値に戻す。
  - 計画の V に「ゲートの 1 つ目の回が全件通る（シミュレーターと実機）」を明示する。

**S-5 [B] [テスト／決まり]** 実機の許可の一覧と、時間の上限に触れていない
- 根拠:
  - `scripts/ios_ui_runner.py`:201 の `CATEGORIES["playertests"]` が、実機の許可で見せる「端末に起きること」の文。画面を回すことが書かれていない。
  - native-toolkit の `agent-rules/device-testing.md` は、流す前に「端末に起きること」を知らせると決めている。
  - 1 つ目の回の上限は `PLAYER_LIMIT=600`（`verify_unity_ios.sh`:84）。10 件に、それぞれシーンの読み込み、4 秒の待ち、2 秒の待ち、57 個のボタンの照らし（Clipboard）が加わる。見積もりで約 100 秒増える。
  - 2 章の変更ファイルの一覧に、`ios_ui_runner.py` が無い。
- 壊れ方:
  - D-6 の実機の回で、知らせていない動き（画面が横に回る）が端末に起きる。
  - 回が上限に近づくと、結果が出ずに落ちうる。
- 直し方の案:
  - `playertests` の文に「Player の画面を縦と横に回す（端末の設定は変えない）」を足す。これを変更ファイルの一覧に入れる。
  - 実装の後、シミュレーターで 1 つ目の回の所要時間を記録し、600 秒との差を見る。

**S-6 [B] [正しさ]** 余白の色を写す時点と、「変わったときだけ当て直す」の組み合わせ
- 根拠:
  - D-1 と 4 章では、最初の子の `resolvedStyle.backgroundColor` を写し、透明なら写さない。当て直すかの判定は、ルートの要素・`safeArea`・画面の大きさだけで決める。
  - `ApplyScreen` でルートを作り直した同じフレームの LateUpdate で、新しい子のスタイルがもう解決済みかは確かめていない。パネルの更新とクリックのイベントの処理の順による。私の信念では、どちらもありうる。
  - `CommonPanelSettings` は `m_ClearColor: 0` なので、色が写らないと余白の部分にはカメラの色が出る。
- 壊れ方:
  - 1 回めに透明を読むと、もう読み直さない。余白の色が続かないまま残る。
  - 気づけるのは 5 章の手の確認だけ。
- 直し方の案:
  - 写した色も判定の値に入れる（透明のあいだは毎フレーム読み直す）。
  - または、最初の子の `GeometryChangedEvent` で一度だけ写す（購読を外す処理も書く）。

**S-7 [C] [決まり]** `Runtime/UI/iOS/Common/` という置き場所は、決まりに書かれていない
- 根拠:
  - common.md の「`UI/` のディレクトリ構成」は、`UI/<Platform>/<Feature>/` だけを決めている。
  - `<Platform>/Common/` は、Runtime の「ネイティブライブラリの共通部」に限って決めている（common.md:466-480 付近）。
- 直し方の案:
  - 計画に、置く理由を書く（iOS のサンプルの全画面が使う部品で、機能の中身ではない）。
  - common.md の「`UI/` のディレクトリ構成」に、`UI/<Platform>/Common/` の例外を足す。
  - 接頭辞 `Ios` と、グローバルの名前空間は、今の Controller と揃っていて問題ない。

**S-8 [C] [決まり]** 変更ファイルの一覧から、直す必要のある説明文が抜けている
- 根拠:
  - `IosSampleLayoutPlayerTests.cs`:18-23 の remarks に「Not in the gate yet … Once the samples are fixed, add IosPlayer」とある。
  - `verify_unity_ios.sh`:111-113 のコメントに、IosSampleLayout を「ゲートの外のテスト」の例として書いている。
  - `IosDialogManagerExampleController.cs`:15 の summary に「safe area adaptation」とあり、D-4 の後は事実と合わない。
  - topic の README と実装の記録に「ゲートに入れない」と書いてある。
- 直し方の案: これらを変更ファイルの一覧に足す。

**S-9 [C] [正しさ]** 1.1 の数値が、出典と食い違う
- 根拠: 1.1 では `HomeButton`（y 14〜64）。実装の記録 4.3 では Dialog は y 18〜64。14〜64 は設計 v3 の 9 章のスパイクの値。
- 直し方の案: どの回の値かを書くか、4.3 の値に揃える。

---

他の OS への影響で、問題が無いと確かめた点:
- Android・Windows のサンプルのテストと配線のテストは、`Q<Button>` と `NavigationSubmitEvent` で押す（`AndroidSampleScreenDriver.cs`:72-78、`WindowsClipboardSampleScreenDriver.cs`:233-237）。ScrollView で包んでも壊れない。
- 配線のテストが読むソースの部分（`RemoveExistingControllers` と各 `Show*` の中身）は、計画の変更では変わらない。

結論: このままでは進めない方がよい。S-1・S-2・S-3 を計画に直し込み、S-4・S-5 の確かめ方を V に足した v2 を作ってから進める。

## 2. 判断（こちら）

A1 が 1 件あるので止めない。v2 を作り、2 回目（差分）に回す。

| 指摘 | 区分 | 判断 | v2 の場所 |
|---|---|---|---|
| S-1 起動した直後のトップのメニュー | A1 | 受け入れる。`TopMenuExampleController` の起動のときにも部品を付ける（ナビゲーターの付ける口を共有）。配置の確かめに「ルートの余白がセーフエリアの外の幅と合う」を足し、「画面 → Home → 横 → トップのメニュー」の 1 件を足す | D-2、D-5、4 章、V-1 |
| S-2 セレクター | A2 | 受け入れる。子孫セレクター（`.top-scroll #unity-content-container`）。V-2 に横の iOS と広い窓を足す | D-3、4 章、V-2 |
| S-3 横の見出しの高さ | A2 | 受け入れる。Notification と Share の見出しに Clipboard と同じ形（見出し `max-height: 50%`、スクロール `min-height: 50%`）。配置の確かめに「ボタンがスクロールの窓の中」を足す | D-7（新）、4 章、V-5 |
| S-4 向きが残る | B | 受け入れる。後片付けで縦に戻し、縦になるのを待つ | 4 章、V-6 |
| S-5 承認の表示と時間 | B | 受け入れる。`playertests` の文に「画面を縦と横に回す」を足す。1 つ目の回の所要時間を記録する | 2 章、V-7 |
| S-6 余白の色 | B | 受け入れる。写した色も変わったかの判定に入れ、透明の間は読み直す | 4 章 |
| S-7 置き場所 | C | 受け入れる。理由を書き、`common.md` に `UI/<Platform>/Common/` を足す | 2 章、D-1 |
| S-8 直す説明文 | C | 受け入れる | 2 章 |
| S-9 数値の出典 | C | 受け入れる。4.3 の値にそろえた | 1.1 |
