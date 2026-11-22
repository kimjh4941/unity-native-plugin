# iOS のサンプルの画面をセーフエリアに合わせる（サンプルの計画）v1

## 基本情報

- 記録日: 2026-11-21
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 4、[段 4 の計画](../../../topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-stage4-plan-v1.md)
- きっかけ: UI の手伝い役の設計 v3 の 9 章のスパイクと、配置の確かめのテスト（`IosSampleLayoutPlayerTests`。[実装の記録](../../../topics/ios-c-abi-2/results/2026-11-15-ios-ui-driver-implementation-result-v1.md) の「配置の確かめ」の節）。10 件のうち 9 件が落ちた
- 利用者の判断（2026-11-21）: サンプルを直す。直したら配置の確かめのテストをゲートに入れる
- 対象: iOS のサンプルの 4 つの画面（Dialog・Share・Clipboard・Notification）と、全 OS で共通のトップのメニュー

## 1. 画面要件

### 1.1 今の崩れ（シミュレーター iPhone 16 / iOS 18.3、パネルの座標）

| 画面 | 縦（セーフエリア `[0,37,246,512]`） | 横（セーフエリア `[37,0,496,233]`） |
|---|---|---|
| トップのメニュー | 問題なし | `ShareFeatureButton`・`ClipboardFeatureButton` が下にはみ出す。`ClipboardFeatureButton`（y 258〜304）は画面の高さ（246）の外で押せない |
| Dialog・Share・Clipboard・Notification | `HomeButton`（y 14〜64）が上の余白（ステータスバー・ダイナミックアイランド）にかかる。Share・Clipboard・Notification の最後のボタンがホームインジケーターにかかる | どのボタンも左端が x 20〜24 で、ノッチの側の余白（37）にかかる |

### 1.2 直した後の要件

- 縦と横の両方で、すべてのボタンがセーフエリアの中にあり、中心を押すとそのボタンに届く（`IosSampleLayoutPlayerTests` の 10 件がすべて通る）
- トップのメニューは、入りきらないとき（横）にスクロールで全部のボタンに届く。入りきるときの見た目（中央寄せ）は今のまま
- 画面の機能（ボタン、結果の表示、エラーの表示）は変えない

## 2. 変更ファイル一覧

| 区分 | ファイル | 中身 |
|---|---|---|
| 新規作成 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Common/IosSampleSafeArea.cs` | セーフエリアの余白を UIDocument のルートに当てる部品（3 章の D-1） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | iOS のとき、`ApplyScreen` が UIDocument の GameObject に `IosSampleSafeArea` を付ける（D-2） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml`・`TopMenuExampleStyle.uss` | 中身を `ScrollView` で包む（D-3） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Dialog/IosDialogManagerExampleController.cs` | 余白を 0 にする枠の処理（`ApplyIosSafeArea` と手で決める余白の項目）を外す（D-4） |
| 既存変更 | `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSampleLayoutPlayerTests.cs` | `IosPlayer` のカテゴリを足してゲートに入れる（D-5） |
| 非変更 | Android・Windows・macOS の画面の UXML・USS・Controller | 今回は iOS だけ（利用者の判断）。Android の Dialog の画面にも同じ枠の処理（余白 0）があるが、触らない |
| 非変更 | iOS の 4 つの画面の UXML・USS | ルートに余白を当てるので、中身は変えない |

## 3. 実装方針

| # | 決めること | 決定 | 理由 |
|---|---|---|---|
| D-1 | 余白の当て方 | 新しい部品 `IosSampleSafeArea`（`MonoBehaviour`）が、`LateUpdate` で `Screen.safeArea`・画面の大きさ・ルートの要素が前と変わったときだけ、`uiDocument.rootVisualElement` の `padding`（上下左右）を、セーフエリアの外の幅をパネルの座標に直した値にする（`RuntimePanelUtils.ScreenToPanel`。画面の y は上向き、パネルの y は下向き）。余白の部分の色は、ルートの最初の子の背景の色に合わせる | 1 か所で 5 つの画面を直せる。画面の差し替え（`visualTreeAsset` の付け替え）でルートの中身が替わっても、向きが変わっても、次のフレームで当て直す。座標の直し方はスパイクと配置の確かめで合っていた |
| D-2 | 部品を付ける場所 | `NativeToolkitSampleNavigator.ApplyScreen` が、`#if UNITY_IOS` のとき UIDocument の GameObject に付ける（無ければ）。Controller を外す `RemoveExistingControllers` では外さない | トップのメニューを含むすべての画面の切り替えが `ApplyScreen` を通る。iOS だけにする（利用者の判断の範囲） |
| D-3 | トップのメニュー | `.top-root` の中を `ScrollView` で包み、中身の器（`#unity-content-container`）を、入りきるときは今と同じく中央に寄せる。トップのメニューは全 OS で共通なので、ほかの OS でも、入りきらないときだけスクロールが出る | 横では高さが足りず、押せないボタンがある。セーフエリアの余白を当てると高さはさらに減る |
| D-4 | iOS の Dialog の画面の枠の処理 | 外す（`ApplyIosSafeArea`・`ApplySafeAreaCoroutine` と、`useManualSafeArea`・`manual*Padding` の項目） | 2 フレームの後にルートの余白を 0 に戻すので、D-1 の部品とぶつかる。手で決める余白の項目は、`AddComponent` で付く Controller では Inspector から変えられず、使われていない |
| D-5 | 配置の確かめ | `IosSampleLayoutPlayerTests` に `IosPlayer` を足し、ゲートの 1 つ目の Player の回に入れる。`IosSampleScreen` も足す（Test iOS Profile では Ignore） | 利用者の判断。画面を直すたびに、配置の崩れがゲートで分かる |
| D-6 | 実機 | 配置の確かめは実機でも流す（向きは `Screen.orientation` でアプリだけを回し、端末の設定は変えない） | ノッチとホームインジケーターは端末で形が違う（iPhone XS はノッチ、iPhone 16 はダイナミックアイランド） |

- 共通の実装の形のうち、維持するもの: TopMenu → 機能の画面の導線、Controller の作りと購読、結果の表示。拡張するもの: ナビゲーターが画面の外側（余白）も受け持つ
- 入力欄（`TextField`）は置かない（今回の変更は配置だけ）

## 4. 実装詳細

- `IosSampleSafeArea`:
  - `#if UNITY_IOS || UNITY_EDITOR`。名前空間はサンプルの Controller と同じ形（グローバル）
  - `[RequireComponent(typeof(UIDocument))]`。前に当てた値（ルートの要素、`Screen.safeArea`、`Screen.width`・`height`）を覚え、どれかが変わったときだけ当て直す
  - パネルがまだ無い（`rootVisualElement.panel == null`）フレームは飛ばす
  - 余白の色: ルートの最初の子の `resolvedStyle.backgroundColor` をルートの `backgroundColor` に写す（子の背景が透明なら写さない）
  - ログ: 当て直したときに 1 行（`[IosSampleSafeArea] padding top/bottom/left/right`）
- ナビゲーター: `ApplyScreen` の最後に `#if UNITY_IOS` で `if (uiDocument.GetComponent<IosSampleSafeArea>() == null) uiDocument.gameObject.AddComponent<IosSampleSafeArea>();`
- トップのメニューの UXML: `<ui:ScrollView class="top-scroll" vertical-scroller-visibility="Auto" horizontal-scroller-visibility="Hidden">` で `.top-content` を包む。USS: `.top-scroll { flex-grow: 1; width: 100%; }`、`.top-scroll > #unity-content-viewport > #unity-content-container { flex-grow: 1; justify-content: center; align-items: center; min-height: 100%; }`。中央寄せが崩れたら、実装で `align-self` などで合わせる（V-2）
- `IosSampleLayoutPlayerTests`: カテゴリを `IosPlayer`・`IosSampleScreen`・`IosSampleLayout` に。`IosSampleScreenDriver.CheckLayout` は今のまま

## 5. 手動確認観点

- 縦と横で、ステータスバー・ダイナミックアイランド（ノッチ）・ホームインジケーターの部分の色が、画面の背景と続いて見えること（見た目は手の確認表で見る。配置は自動）
- トップのメニューが、縦では今と同じ見た目（中央寄せ）で、横ではスクロールで Clipboard まで届くこと
- 画面を行き来しても、余白が付いたままであること
- Android・Windows・macOS のトップのメニューが、入りきるときは今と同じ見た目であること

## 6. 要検証

| # | 内容 | 確かめ方 |
|---|---|---|
| V-1 | ルート（`TemplateContainer`）の `padding` で、iOS の 4 つの画面の中身（`ios-safe-area-container` などの `height: 100%` の器）がセーフエリアの中に収まるか | `IosSampleLayoutPlayerTests` |
| V-2 | トップのメニューの `ScrollView` で、入りきるときに中央寄せが保たれるか | 縦のスクリーンショットを手で見る（シミュレーター）。崩れたら USS を直す |
| V-3 | 実機（iPhone XS、ノッチ）でも配置の確かめが通るか | 実機の回 |
| V-4 | Editor の Device Simulator でも余白が当たるか（`Screen.safeArea` が端末の形を返す） | 手で見る（任意） |
