# iOS のサンプルの画面をセーフエリアに合わせる（サンプルの計画）v2

## 基本情報

- 記録日: 2026-11-21
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 4、[段 4 の計画](../../../topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-stage4-plan-v1.md)
- きっかけ: UI の手伝い役の設計 v3 の 9 章のスパイクと、配置の確かめのテスト（`IosSampleLayoutPlayerTests`。[実装の記録](../../../topics/ios-c-abi-2/results/2026-11-15-ios-ui-driver-implementation-result-v1.md) の「配置の確かめ」の節）。10 件のうち 9 件が落ちた
- 利用者の判断（2026-11-21）: サンプルを直す。直したら配置の確かめのテストをゲートに入れる
- 対象: iOS のサンプルの 4 つの画面（Dialog・Share・Clipboard・Notification）と、全 OS で共通のトップのメニュー

## v1 からの主な変更（v2。レビュー v1 を受けて）

レビュー v1（[../reviews/2026-11-21-ios-sample-layout-sample-scene-design-review-v1.md](../reviews/2026-11-21-ios-sample-layout-sample-scene-design-review-v1.md)。Codex は使えず、サブエージェントだけ）で A1 が 1 件あった。

- **起動した直後のトップのメニューにも部品を付ける**（D-2。S-1）: シーンは最初からトップのメニューを持っていて、ナビゲーターを通らない
- **配置の確かめに、余白が当たったこと自体と、画面を移った後を足す**（D-5、V-1。S-1）
- トップのメニューの USS は子孫セレクター（D-3。S-2）
- **Notification と Share の見出しの高さ**を Clipboard と同じ形にする（D-7。S-3）。配置の確かめに「ボタンがスクロールの窓の中」を足す
- 後片付けで縦に戻ったのを待つ（S-4）、承認の表示と時間（S-5）、余白の色の読み直し（S-6）、置き場所と規則（S-7）、直す説明文（S-8）、数値の出典（S-9）

## 1. 画面要件

### 1.1 今の崩れ（シミュレーター iPhone 16 / iOS 18.3、パネルの座標）

| 画面 | 縦（セーフエリア `[0,37,246,512]`） | 横（セーフエリア `[37,0,496,233]`） |
|---|---|---|
| トップのメニュー | 問題なし | `ShareFeatureButton`・`ClipboardFeatureButton` が下にはみ出す。`ClipboardFeatureButton`（y 258〜304）は画面の高さ（246）の外で押せない |
| Dialog・Share・Clipboard・Notification | `HomeButton`（Dialog で y 18〜64。実装の記録の「配置の確かめ」の節の値）が上の余白（ステータスバー・ダイナミックアイランド）にかかる。Share・Clipboard・Notification の最後のボタンがホームインジケーターにかかる | どのボタンも左端が x 20〜24 で、ノッチの側の余白（37）にかかる |

### 1.2 直した後の要件

- 縦と横の両方で、すべてのボタンがセーフエリアの中にあり、中心を押すとそのボタンに届く（`IosSampleLayoutPlayerTests` の 10 件がすべて通る）
- トップのメニューは、入りきらないとき（横）にスクロールで全部のボタンに届く。入りきるときの見た目（中央寄せ）は今のまま
- 画面の機能（ボタン、結果の表示、エラーの表示）は変えない

## 2. 変更ファイル一覧

| 区分 | ファイル | 中身 |
|---|---|---|
| 新規作成 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Common/IosSampleSafeArea.cs` | セーフエリアの余白を UIDocument のルートに当てる部品（3 章の D-1）。iOS のサンプルの全画面が使う部品で、機能の中身ではないので `UI/iOS/Common/` に置く |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs` | 起動したときにも部品を付ける（D-2） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Notification/IosNotificationManagerExampleStyle.uss`・`Runtime/Resources/UI/iOS/Share/IosShareManagerExampleStyle.uss` | 見出しとスクロールの高さ（D-7） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSampleScreenDriver.cs` | 配置の確かめに「余白が当たった」「ボタンがスクロールの窓の中」を足す（D-5） |
| 既存変更 | `scripts/ios_ui_runner.py` | 実機の承認の表示（`playertests`）に「Player の画面を縦と横に回す（端末の設定は変えない）」を足す |
| 既存変更 | `scripts/verify_unity_ios.sh`（`--category` の説明の例）、`agent-rules/coding-rules/common.md`（`UI/<Platform>/Common/` を足す）、`artifact/topics/ios-c-abi-2/README.md`・`artifact/topics/ios-c-abi-2/results/2026-11-15-ios-ui-driver-implementation-result-v1.md`（「ゲートに入れない」を直す） | 説明の直し |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | iOS のとき、`ApplyScreen` が UIDocument の GameObject に `IosSampleSafeArea` を付ける（D-2） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml`・`TopMenuExampleStyle.uss` | 中身を `ScrollView` で包む（D-3） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Dialog/IosDialogManagerExampleController.cs` | 余白を 0 にする枠の処理（`ApplyIosSafeArea` と手で決める余白の項目）を外す（D-4） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSampleLayoutPlayerTests.cs` | `IosPlayer` のカテゴリを足してゲートに入れる（D-5） |
| 非変更 | Android・Windows・macOS の画面の UXML・USS・Controller | 今回は iOS だけ（利用者の判断）。Android の Dialog の画面にも同じ枠の処理（余白 0）があるが、触らない |
| 非変更 | iOS の 4 つの画面の UXML、Dialog と Clipboard の USS | ルートに余白を当てるので、中身は変えない（Notification と Share の USS だけ D-7 で直す） |

## 3. 実装方針

| # | 決めること | 決定 | 理由 |
|---|---|---|---|
| D-1 | 余白の当て方 | 新しい部品 `IosSampleSafeArea`（`MonoBehaviour`。`Runtime/UI/iOS/Common/`。`common.md` の「`UI/` のディレクトリ構成」に `UI/<Platform>/Common/` を、そのプラットフォームのサンプルの全画面が使う部品の置き場として足す）が、`LateUpdate` で `Screen.safeArea`・画面の大きさ・ルートの要素・写した色が前と変わったときだけ、`uiDocument.rootVisualElement` の `padding`（上下左右）を、セーフエリアの外の幅をパネルの座標に直した値にする（`RuntimePanelUtils.ScreenToPanel`。画面の y は上向き、パネルの y は下向き）。余白の部分の色は、ルートの最初の子の背景の色に合わせる | 1 か所で 5 つの画面を直せる。画面の差し替え（`visualTreeAsset` の付け替え）でルートの中身が替わっても、向きが変わっても、次のフレームで当て直す。座標の直し方はスパイクと配置の確かめで合っていた |
| D-2 | 部品を付ける場所 | ナビゲーターに付ける口（`NativeToolkitSampleNavigator.EnsureSafeArea(UIDocument)`。`#if UNITY_IOS` のときだけ付け、ほかでは何もしない）を作り、`ApplyScreen` の最後と、`TopMenuExampleController` の起動のとき（`Start`）の 2 か所から呼ぶ。Controller を外す `RemoveExistingControllers` では外さない | シーンは最初からトップのメニューの UXML と Controller を持っていて、起動した直後の画面は `ApplyScreen` を通らない（レビュー v1 の S-1）。画面の切り替えは `ApplyScreen` を通る。iOS だけにする（利用者の判断の範囲） |
| D-3 | トップのメニュー | `.top-root` の中を `ScrollView` で包み、中身の器を子孫セレクター（`.top-scroll #unity-content-container`。Unity 6 では ScrollView と器の間に `#unity-content-and-vertical-scroll-container` と `#unity-content-viewport` が挟まる）で、入りきるときは今と同じく中央に寄せる。トップのメニューは全 OS で共通なので、ほかの OS でも、入りきらないときだけスクロールが出る | 横では高さが足りず、押せないボタンがある。セーフエリアの余白を当てると高さはさらに減る |
| D-4 | iOS の Dialog の画面の枠の処理 | 外す（`ApplyIosSafeArea`・`ApplySafeAreaCoroutine` と、`useManualSafeArea`・`manual*Padding` の項目） | 2 フレームの後にルートの余白を 0 に戻すので、D-1 の部品とぶつかる。手で決める余白の項目は、`AddComponent` で付く Controller では Inspector から変えられず、使われていない |
| D-5 | 配置の確かめ | `IosSampleLayoutPlayerTests` に `IosPlayer` を足し、ゲートの 1 つ目の Player の回に入れる。`IosSampleScreen` も足す（Test iOS Profile では Ignore）。`CheckLayout` に 2 つ足す: (a) ルートの `resolvedStyle.padding*` が、セーフエリアの外の幅（パネルの座標）と 1 以内で合う、(b) スクロールの中のボタンは、送った後に `ScrollView.contentViewport.worldBound` の中にある。テストに「Dialog の画面 → Home → 横 → トップのメニュー」の 1 件（SL11）を足す | 利用者の判断。部品が付かないまま通る形（S-1）と、窓が小さすぎて半分だけ見える形（S-3）を捕まえる |
| D-6 | 実機 | 配置の確かめは実機でも流す（向きは `Screen.orientation` でアプリだけを回し、端末の設定は変えない）。実機の承認の表示に、画面を回すことを書く | ノッチとホームインジケーターは端末で形が違う（iPhone XS はノッチ、iPhone 16 はダイナミックアイランド） |
| D-7 | Notification と Share の見出し | Clipboard と同じ形にする: 見出し（`.ios-notif-header`・`.ios-share-header`）に `max-height: 50%`（はみ出る分は見出しの中で切る）、スクロール（`.ios-notif-scroll`・`.ios-share-scroll`）に `min-height: 50%` | 横では見出し（約 209）が高さ（223〜233）のほとんどを取り、スクロールの窓がボタン 1 つ（46）より小さくなる（レビュー v1 の S-3） |

- 共通の実装の形のうち、維持するもの: TopMenu → 機能の画面の導線、Controller の作りと購読、結果の表示。拡張するもの: ナビゲーターが画面の外側（余白）も受け持つ
- 入力欄（`TextField`）は置かない（今回の変更は配置だけ）

## 4. 実装詳細

- `IosSampleSafeArea`:
  - `#if UNITY_IOS || UNITY_EDITOR`。名前空間はサンプルの Controller と同じ形（グローバル）
  - `[RequireComponent(typeof(UIDocument))]`。前に当てた値（ルートの要素、`Screen.safeArea`、`Screen.width`・`height`、写した色）を覚え、どれかが変わったときだけ当て直す。色が透明に読めた間は、毎フレーム読み直す（新しい子のスタイルが、付け替えた同じフレームではまだ解けていないことがある）
  - パネルがまだ無い（`rootVisualElement.panel == null`）フレームは飛ばす
  - 余白の色: ルートの最初の子の `resolvedStyle.backgroundColor` をルートの `backgroundColor` に写す（子の背景が透明なら写さない）
  - ログ: 当て直したときに 1 行（`[IosSampleSafeArea] padding top/bottom/left/right`）
- ナビゲーター: `public static void EnsureSafeArea(UIDocument uiDocument)`（`#if UNITY_IOS` の中で `if (uiDocument.GetComponent<IosSampleSafeArea>() == null) uiDocument.gameObject.AddComponent<IosSampleSafeArea>();`）。`ApplyScreen` の最後と `TopMenuExampleController.Start` から呼ぶ
- トップのメニューの UXML: `<ui:ScrollView class="top-scroll" vertical-scroller-visibility="Auto" horizontal-scroller-visibility="Hidden">` で `.top-content` を包む。USS: `.top-scroll { flex-grow: 1; width: 100%; }`、`.top-scroll > #unity-content-viewport > #unity-content-container { flex-grow: 1; justify-content: center; align-items: center; min-height: 100%; }`。中央寄せが崩れたら、実装で `align-self` などで合わせる（V-2）
- `IosSampleLayoutPlayerTests`: カテゴリを `IosPlayer`・`IosSampleScreen`・`IosSampleLayout` に。remarks の「ゲートの外」を直す。後片付けは、まず `Portrait` にして `Screen.height > Screen.width` になるまで待ち（10 秒）、それから元の値に戻す。SL11 を足す
- `IosSampleScreenDriver.CheckLayout`: D-5 の (a) と (b) を足す
- Notification と Share の USS: D-7
- `IosDialogManagerExampleController`: summary の「safe area adaptation」の記述も直す

## 5. 手動確認観点

- 縦と横で、ステータスバー・ダイナミックアイランド（ノッチ）・ホームインジケーターの部分の色が、画面の背景と続いて見えること（見た目は手の確認表で見る。配置は自動）
- トップのメニューが、縦では今と同じ見た目（中央寄せ）で、横ではスクロールで Clipboard まで届くこと
- 画面を行き来しても、余白が付いたままであること
- Android・Windows・macOS のトップのメニューが、入りきるときは今と同じ見た目であること

## 6. 要検証

| # | 内容 | 確かめ方 |
|---|---|---|
| V-1 | ルート（`TemplateContainer`）の `padding` で、iOS の 4 つの画面の中身（`ios-safe-area-container` などの `height: 100%` の器）がセーフエリアの中に収まるか | `IosSampleLayoutPlayerTests` |
| V-2 | トップのメニューの `ScrollView` で、入りきるときに中央寄せが保たれ、入りきらないときはスクロールできるか | iOS の横（シミュレーター）と、広い窓（macOS の Editor の Game ビュー）で、スクリーンショットを手で見る。器の `flex-grow` と `min-height` で縮んでスクロールが効かなくならないかも見る |
| V-3 | 実機（iPhone XS、ノッチ）でも配置の確かめが通るか | 実機の回 |
| V-4 | Editor の Device Simulator でも余白が当たるか（`Screen.safeArea` が端末の形を返す） | 手で見る（任意） |
| V-5 | 横の Notification と Share で、スクロールの窓がボタンの高さ以上あるか | `CheckLayout` の (b)（窓の中に入らなければ落ちる） |
| V-6 | ゲートの 1 つ目の回が全件通る（配置の確かめの後の向きが、後のテストに残らない） | シミュレーターと実機の回 |
| V-7 | 1 つ目の回の所要時間が、Player の上限（600 秒）に収まる | シミュレーターの回の時間を記録し、差を見る |
