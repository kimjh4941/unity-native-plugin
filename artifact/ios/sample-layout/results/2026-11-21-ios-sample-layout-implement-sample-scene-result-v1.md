# iOS のサンプルの画面をセーフエリアに合わせる 実装の記録 v1

## 基本情報

- 記録日: 2026-11-21
- 計画: [../designs/2026-11-21-ios-sample-layout-sample-scene-design-v3.md](../designs/2026-11-21-ios-sample-layout-sample-scene-design-v3.md)（レビュー 3 回で A1 が 0。最後に Codex を 1 回通して指摘 0）
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 4
- 利用者の判断（2026-11-21）: サンプルを直し、配置の確かめのテストをゲートに入れる。native-toolkit を待たずに進められるものは自律で進める

## 1. 作ったもの

| 区分 | ファイル | 中身 |
|---|---|---|
| 新規 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Common/IosSampleSafeArea.cs` | D-1。`LateUpdate` で、ルート・`Screen.safeArea`・画面の大きさ・最初の子の背景の色のどれかが変わったときだけ、ルートの padding と class `ios-sample-landscape` と背景の色を当て直す。ルートが替わったら覚えた色を捨てる（新しいルートに色が写らない形を避ける） |
| 変更 | `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | D-2。`EnsureSafeArea(UIDocument)`（`#if UNITY_IOS` のときだけ付ける）。`ApplyScreen` の最後で呼ぶ |
| 変更 | `Runtime/UI/Top/TopMenuExampleController.cs` | D-2。`Start` で `EnsureSafeArea` を呼ぶ（S-1） |
| 変更 | `Runtime/Resources/UI/Top/TopMenuExample.uxml`・`TopMenuExampleStyle.uss` | D-3。`.top-content` を `ScrollView`（`.top-scroll`）で包み、器を子孫セレクター `.top-scroll #unity-content-container` で中央に寄せる |
| 変更 | `Runtime/UI/iOS/Dialog/IosDialogManagerExampleController.cs` | D-4。`ApplyIosSafeArea`・`ApplySafeAreaCoroutine`・手で余白を決める項目を外し、summary を直した。画面の大きさの class（`ApplyResponsiveClasses`）は残した |
| 変更 | iOS の 4 画面の USS | D-7。`.ios-sample-landscape` の下でだけ、見出しを横並び（`#HomeButton` は幅 30%、結果の枠は `flex-grow: 1; flex-basis: 0`）にし、題と説明を隠す。Clipboard は `flex-wrap: wrap` で状態の行を 2 行目に回し、結果の枠を高さ 52 にする。縦の規則は変えない |
| 変更 | `Tests/PlayMode/IosSampleScreenDriver.cs` | D-5。`CheckLayout` に (a) ルートの padding と class、(b) ボタンがスクロールの窓の中、(c) 結果の枠が見出しとセーフエリアの中で押せる位置にある、を足した。(c) は結果を長い文（約 300 字と改行 2 つ）にしてもう一度見る。そのとき、ボタンの一覧の窓の高さをログに出す（R3-2） |
| 変更 | `Tests/PlayMode/IosSampleLayoutPlayerTests.cs` | D-5。カテゴリ `IosPlayer`・`IosSampleScreen` を足してゲートの 1 つ目の回に入れた。後片付けは縦にして縦になるのを待ち（10 秒まで）、元の値に戻す（S-4）。SL11（Dialog → Home → 横 → トップのメニュー）を足した |
| 変更 | `scripts/ios_ui_runner.py` | 実機の承認の文（`playertests`）に、Player の画面を縦と横に回すこと（端末の設定は変えない）を足した（S-5） |
| 変更 | `scripts/verify_unity_ios.sh` | `--category` の説明の例の「ゲートの外」を直した（S-8） |
| 変更 | `agent-rules/coding-rules/common.md` | `UI/<Platform>/Common/` を、そのプラットフォームのサンプルの全画面が使う部品の置き場として足した（S-7） |

## 2. レビューから実装に回したもの（チェックリスト）

| 出どころ | 内容 | 扱い |
|---|---|---|
| R3-1（B） | 横の Clipboard で長い結果が次の行に回る | 結果の枠を `flex-basis: 0`、状態の行を `width: 100%`。見出しの `max-height: 50%` は残した（横の見出しは約 8＋52＋6＋16＋8＝90 で、上限の約 111〜116 に収まる見込み）。ドライバーの長い文の確かめで見る |
| R3-2（B） | Notification の長い一覧で、横の窓が狭くなる | 受け入れた（計画 v3 の 7 章）。長い文の確かめで窓の高さをログに残す（3 章） |
| R3-3（B） | Home ボタンの規則がほかのボタンに当たる | `#HomeButton` だけに当てた。手の確認で見る（4 章） |
| R3-4（C） | D-5 の数、D-1 の括弧、(c) の「枠」 | D-5 は 3 つ。(c) の「枠」は、class 名が `-header` で終わる祖先（見出し）の直下の子で、`ResultTextBlock` を含むもの。トップのメニュー（`ResultTextBlock` が無い）では飛ばす |

## 3. 確かめたこと

シミュレーター（iPhone 16 / iOS 18.3）、`scripts/verify_unity_ios.sh`（全ゲート）、2026-11-21:

| 回 | 結果 |
|---|---|
| EditMode | 1592 成功、1 スキップ（前からのもの） |
| Editor の PlayMode | 391 成功 |
| Player の 1 つ目の回（`IosPlayer`） | **114 成功**（103 に `IosSampleLayoutPlayerTests` の 11 を足した数）、ドライバー 27 件。SL01〜SL11 は 1 件 5.7〜10.3 秒 |
| Player の Notification のサンプルの回 | 8 成功、ドライバー 8 件 |

- 余白（`IosSampleSafeArea` のログ。パネルの座標）: 縦で上 37・下 21・左右 0、横で上 0・下 13・左右 37。直す前のスパイクで測ったセーフエリア（縦 `[0,37,246,512]`、横 `[37,0,496,233]`）と合う。(a) の確かめも通った
- 長い結果（約 300 字と改行 2 つ）のときの、ボタンの一覧の窓の高さ（R3-2 の数値）:

| 画面 | 縦 | 横 |
|---|---|---|
| Dialog | 77 | 59 |
| Share | 59 | 59 |
| Clipboard | 237 | 136 |
| Notification | 73 | 59 |

  どれもボタン 1 つ（46）より高い。縦でも、長い結果のあいだは窓が狭くなる（見出しが伸びる今の形のまま）
- V-7: 1 つ目の回のテストの時間は 231 秒（直す前、同じ日の回）→ 314 秒。上限の 600 秒に収まる
- V-1・V-5・V-6: 上の回で確かめた（配置の後の向きが後のテストに残らず、続く Player のテストも全件成功）

### 3.1 実装のレビュー v1 の直しの後

[実装のレビュー v1](../reviews/2026-11-21-ios-sample-layout-implement-sample-scene-review-v1.md)（サブエージェントと Codex）の直し（余白の色を画面が宣言する形、配置の確かめのすり抜けを塞ぐ、ログの形）の後:

| 回 | 結果 |
|---|---|
| 全ゲート（色の宣言と L1・L2・L4 の直しの後） | EditMode 1592 成功・1 スキップ、Editor の PlayMode 391 成功、Player 114 成功（ドライバー 27）、Notification のサンプルの回 8 成功（ドライバー 8） |
| Player の 2 つの回（Codex M1 の直しの後。テストのファイルだけを変えたので `--skip-editmode`） | Player 114 成功（ドライバー 27）、Notification のサンプルの回 8 成功（ドライバー 8）。SL01〜SL11 すべて成功 |

- 余白の色の確かめは、4 画面では宣言（`--ios-sample-inset-color`）が無いと落ちる形にした。それで通ったので、4 画面のルートに宣言した白が写っている。トップのメニューは最初の子の背景（薄い灰色）が写っている
- 長い結果のときの一覧の窓の高さは、直しの前と同じ（最小 59。ボタン 1 つ分の 44 以上を判定にした）

## 4. 手の確認（残り）

> 2026-11-22 の追記: 下の 4 項目を手で見た記録は無い（未記録）。利用者が iPhone XS で見たサンプルでトップメニューの左右が切れていて、[狭い画面の直し](2026-11-22-ios-sample-layout-narrow-screen-fix-result-v1.md)（`a75d802`）をした。その直しは、2 章の R3-1・R3-2 と、設計 v3 の D-7（結果の枠は高さを決めない）を逆にした（結果の欄は固定の高さ、Clipboard の見出しの `max-height: 50%` は外した）。目で見たのは iPhone 13 mini のシミュレーターのトップメニューだけ。Android・Windows・macOS のトップメニュー（4 項目め）は 1.14.0 の後へ持ち越す

- 縦と横で、ステータスバー・ダイナミックアイランド・ホームインジケーターの部分の色（4 画面は白、トップのメニューは薄い灰色）が、隣の見出しと一覧に続いて見えるか
- トップのメニューが、縦では今と同じ見た目（中央寄せ）で、横ではスクロールで Clipboard まで届くか（V-2）。広い窓（macOS の Editor の Game ビュー）でも中央寄せか
- 横で、題と説明が隠れ、結果が読め、スクロールの中のボタンが幅いっぱいのままか（R3-3）
- Android・Windows・macOS のトップのメニューが、入りきるときは今と同じ見た目か

## 5. 残っていること

- ~~実機（iPhone XS、ノッチ）での配置の確かめ（V-3）~~ → **済み**（2026-11-21 17:23〜17:40、HEAD `83a0d55`、dist の作り直しの後）。ゲートの 1 つ目の回で Player 112 件（配置の確かめ 11 件を含む）がすべて成功、Notification のサンプルの回 8 件も成功。シミュレーターとの差の 2 件は、シミュレーターだけのログインのテスト。同じ日の 1 回目（16:42）は、ビルドの間に端末の画面がロックされ、手伝い役の XCUITest が `Timed out while enabling automation mode.` で起動できず、1 つ目の回が流れなかった（テストの中身の失敗ではない）
- 実機の回の 1 つ目の回の所要時間（実装のレビュー v1 の L5）: ゲートの出力からは 1 つ目の回だけを切り分けられなかった。ゲート全体（書き出し・ビルド 2 回ずつと 2 つの回）は 994 秒で、配置の確かめを入れる前の実機のゲート（2026-11-16、約 26 分）より長くはなっていない。目に見える遅れは無いとして閉じる
- ~~サンプルの画面のドライバー（iOS・Android・Windows）は、ボタンに `NavigationSubmitEvent` を直接送るので、隠れたボタンや画面の外のボタンでも押せてしまう。押す前にボタンの中心を `Pick` して当たるかを確かめる案は、一旦入れない（利用者の判断、2026-11-21）。UI Test Framework（`com.unity.ui.test-framework`）の `simulate.Click` への置き換えも今は行わない~~ → 入れないと決めた（利用者の判断、2026-11-21）
- iPad の横は確かめていない。横の形（見出しの横並び）は `Screen.width > Screen.height` だけで決まるので、高さの十分ある iPad の横でも題と説明が隠れる（実装のレビュー v1 の L3。iPad は今回の対象外）
- 実装のレビュー v1（[../reviews/2026-11-21-ios-sample-layout-implement-sample-scene-review-v1.md](../reviews/2026-11-21-ios-sample-layout-implement-sample-scene-review-v1.md)）の直しの後のゲートの結果は 3.1 に書く
