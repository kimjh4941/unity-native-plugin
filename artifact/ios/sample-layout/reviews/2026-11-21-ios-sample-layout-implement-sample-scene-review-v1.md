# iOS のサンプルのセーフエリア 実装のレビュー v1

- 記録日: 2026-11-21
- 対象: `feature/UNT-14` の作業ツリーの未コミットの差分（`IosSampleSafeArea` の新規作成ほか。実装の記録の 1 章）
- 計画: [../designs/2026-11-21-ios-sample-layout-sample-scene-design-v3.md](../designs/2026-11-21-ios-sample-layout-sample-scene-design-v3.md)
- 実装の記録: [../results/2026-11-21-ios-sample-layout-implement-sample-scene-result-v1.md](../results/2026-11-21-ios-sample-layout-implement-sample-scene-result-v1.md)
- レビュアー: サブエージェント（general-purpose）。**Codex（`codex:codex-rescue`）は走らなかった**: 呼び出しが 400 で返った（`The 'gpt-6.2-sol' model is not supported when using Codex with a ChatGPT account`、`model_not_found`）。同じ日の午前、同じ設定（`~/.codex/config.toml` の `model`。11 月 1 日から変わっていない）で計画のレビューは動いていた。決まりどおり、待たず再試行もせず、サブエージェントの結果だけで判断した。別のモデルを指定して呼び直すのは設定の判断になるので、していない
- **追記**: 利用者が Codex のモデルを `gpt-6.1-sol` に替えた後、サブエージェントの指摘の直し（2 章）を入れた差分を Codex に通した（3 章）
- 1 章と 3 章はレビュアーの出力をそのまま写した。こちらの判断は 2 章と 4 章にだけ書く

## 1. サブエージェント

## レビュー概要
- 目的: iOS のサンプルの 4 画面とトップのメニューを、縦と横の両方でセーフエリアの内側に収める（計画 v3 の D-1〜D-7）。
- 変更の中身:
  - `IosSampleSafeArea` を新しく作った。`LateUpdate` で、UIDocument のルートに padding、`ios-sample-landscape` クラス、背景の色を当てる。
  - ナビゲーターに `EnsureSafeArea` を足した。`ApplyScreen` と `TopMenuExampleController.Start` の 2 か所から呼ぶ。
  - トップのメニューを `ScrollView` で包んだ。
  - Dialog の、余白を 0 に戻す古い処理を外した。
  - 4 画面の USS に、横向きのときだけ使う見出しの形を足した。
  - `CheckLayout` に (a) ルートの padding とクラス、(b) スクロール窓の中、(c) 結果の枠、の 3 つの確かめを足した。
  - 配置のテストをゲートの 1 つ目の回に入れ、SL11 を足した。
- 計画 v3 の 2 章の変更ファイル一覧と、実際の差分は一致していた。7 章で実装に回した R3-1〜R3-4 も、実装の記録の 2 章どおりに反映されていた。
- 主な確認結果:
  - 他 OS（Android・Windows・macOS）: 影響はトップのメニューの `ScrollView` だけ。テーマの USS は入っていて、他 OS の画面も既に `ScrollView` を使っている。
  - コンパイルガード: 型は `UNITY_IOS || UNITY_EDITOR` でコンパイルされ、付けるのは `UNITY_IOS` のときだけ。Editor で iOS をビルド対象にすると両方が定義されるので、食い違いはない。
  - Editor: トップのメニューの各ボタンは `#if UNITY_EDITOR` で `EditorUtility.DisplayDialog` に分かれるため、Editor では iOS の画面に入れない。部品が当たるのはトップのメニューだけ。
  - 木の差し替え: `uiDocument.visualTreeAsset` を付け替えるとルートが別の要素になり、`root != _root` で検出して次の `LateUpdate` で当て直す。
  - シーンを閉じたとき: static な状態は無いので、何も残らない。
  - 回転時の縮尺: PanelSettings が ConstantPhysicalSize（`m_ScaleMode: 1`）なので、回しても縮尺は変わらない。

## 重大な問題（high）
- なし

## 改善提案（medium）
- **M1. 余白の色が 4 画面とも黒になり、隣の面（白か #F8F8F8）と続かない（要確認。見た目の判断）**
  - 該当箇所: `Runtime/UI/iOS/Common/IosSampleSafeArea.cs:63,76-80,104-107`。色はルートの最初の子の背景から取っている。
  - 最初の子の背景はどれも黒: `IosDialogManagerExampleStyle.uss:5-7`（`.ios-safe-area-container`）、`IosShareManagerExampleStyle.uss:1-3`、`IosNotificationManagerExampleStyle.uss:1-3`、`IosClipboardManagerExampleStyle.uss:1-3`。
  - 余白に接する面は、見出しが白（Dialog:14-15、Share:10-11、Notification:10-11、Clipboard:14-15）、一覧の中身が白か #F8F8F8。
  - 起きること:
    - 縦: 上に黒い帯（パネル座標で 37）、ホームインジケーターの位置に黒い帯（21）。
    - 横: 左右に黒い帯（37）。
  - 部品の説明（同じファイルの 16-17 行の "so the bars read as part of the screen"）とも、手の確認の 1 項目目（「画面の背景と続いて見える」）とも合わない。トップのメニューは `.top-root` が薄い灰色なので問題ない。
  - シミュレーターのゲートは色を見ないので、捕まえられない。
  - 直し方（どちらか）:
    - (a) 4 画面の最初の子の背景を、見出しや一覧と同じ白にする。黒が見えるのは隙間だけなので、見た目の退行は小さい。
    - (b) 画面の側で色を宣言させる。例: USS のカスタムプロパティ `--ios-sample-inset-color` を `customStyle` で読む。
  - 黒い帯を狙った見た目として受け入れるなら、部品の説明の文と手の確認の文言を直す。

## 軽微な指摘（low）
- **L1. `CheckLayout` が通るのに、何も確かめていない形が残る**
  - `Tests/PlayMode/IosSampleScreenDriver.cs:176`: 幅か高さが 0 のボタンは黙って `continue` する。さらに (b) は「窓の中にあるか」しか見ず、ボタンの大きさを見ない。
    - 起きること: 一覧がスクロールせずに縮んでしまう形（ボタンが 46 より低く潰れて、全部が窓に収まる）でも (b) は通る。今回 `.top-scroll #unity-content-container` に `flex-grow` と `min-height` を足したので、そこに潜む余地がある（V-2 は手の確認待ち）。
    - 直し方: `display:none` の祖先を持たないのに大きさが 0 のボタンは問題として数える。加えて、`r.height` が最小の高さ（44 程度。ボタンは 46 か `min-height: 46px`）以上であることを確かめる。
  - 同じファイルの 254 行: Dialog・Share・Notification の見出しは `max-height` を持たず、結果の枠と一緒に伸びる。このため「枠が見出しの中にある」は事実上いつも通る。意味があるのは Clipboard（`max-height: 50%`）だけ。
    - 直し方: 長い結果のときに、見出しの下端がセーフエリアの中にあることと、一覧の窓がボタン 1 つ分（46）以上あることを、ログだけでなく判定にする。R3-2 で受け入れた範囲を数値で固定する形。もしくは、計画 7 章に「ここは判定しない」と明記する。
  - 同じファイルの 197 行と 264 行: `ResultTextBlock` や `StatusTextBlock` が見つからないと、(c) を黙って飛ばす。サンプルの Player のテストが `ResultTextBlock` を読んでいるので、名前を変えれば別の所で落ちる。影響は小さい。
    - 直し方: `Check` から「結果を持つ画面か」を渡し（`topMenuButton != null`）、見つからなければ問題として数える。
- **L2. 後片付けで縦に戻らなかったとき、黙って先へ進む**
  - 該当箇所: `Tests/PlayMode/IosSampleLayoutPlayerTests.cs:43`。10 秒待っても縦にならないと、何も言わずに `_orientationBefore` に戻して終わる。
  - 起きること: 次のテストクラスが横のまま始まり、原因と関係の無い所で落ちる。
  - 直し方: 待ちが切れたら `Debug.LogWarning`、または `Assert.Fail` で、原因をこのテストに付ける。
- **L3. 横向きの判定が大きさを見ていない**
  - 該当箇所: `IosSampleSafeArea.cs:99`。`width > height` だけで判定している。
  - 起きること: iPad の横向き（高さは十分ある）でも、題と説明が隠れ、見出しが横並びになる。
  - 直し方: 余白を引いたパネルの高さが一定以下（例 400）のときだけ、クラスを付ける。
- **L4. csharp.md のログ規則への違反**（プロジェクトルール由来）
  - `IosSampleSafeArea.Awake`（38 行）に、先頭のログが無い。
  - `Apply` のログ（101 行）が `[{LogTag}][{nameof(...)}]` の形になっていない。
  - `public` の `EnsureSafeArea`（`NativeToolkitSampleNavigator.cs:219`）にも先頭のログが無い。ただし同じファイルの `Show*` も全部ログを持たないので、ファイルの慣習とは揃っている。
  - 直し方: `Awake` に先頭のログを足し、`Apply` を `[{LogTag}][{nameof(Apply)}]` の形にする。
  - 補足: Editor で iOS をビルド対象にして Game ビューを引き伸ばすと、フレームごとにこのログが出る。Editor の中だけの雑音。
- **L5. 実機での所要時間はまだ測っていない（要確認）**
  - V-7 で 1 つ目の回が 314 秒だったのはシミュレーターの値。配置のテスト 11 件で約 80 秒増えている。
  - 実機（iPhone XS）での 1 つ目の回の時間が、Player の上限 600 秒に収まるかは、V-3 の回で記録する必要がある。

## 計画整合性チェック
- ボタン一覧の実装網羅性: ○（ボタンの追加・削除なし。トップのメニューの 4 ボタンの name は保たれている）
- UXML name と Controller の一致: ○
- API 呼び出し仕様の一致: ○（`EnsureSafeArea(UIDocument?)` が null を受ける形になったのは、計画の範囲内）
- 変更ファイル一覧との一致: ○（`.top-scroll` は計画の `width: 100%` を `align-self: stretch` に替えた。V-2 の範囲内）

## プロジェクトルール適合チェック
- common.md 準拠: ○（`UI/iOS/Common/` と `Ios` 接頭辞は、追加した規則に合う。`UI/Common/` のナビゲーターに入ったのは iOS 用のフックだけで、機能ロジックではない。D-2 で決めたとおり）
- csharp.md 準拠: △（L4）
- ライフサイクル管理（登録・解除の対称性）: ○（イベントの購読は無い。木の差し替え・UIDocument の再有効化・シーンの解放で、状態は残らない）
- コンパイルガードの網羅性: ○（`IosSampleScreenDriver` は `UNITY_IOS && !UNITY_EDITOR` で、型の参照も解決する）
- 権限ガードの網羅性: ○（対象なし）
- ナビゲーション統合: ○（`ApplyScreen` の最後と、トップのメニューの `Start` の 2 か所。SL01・SL02・SL11 で両方の経路を通る）
- 既存 API 互換性: ○（Dialog から外したのは private な `[SerializeField]` で、`AddComponent` で付くので Inspector からも使われていなかった。他 OS のトップのメニューは、入りきるときは中央寄せのはず。V-2 の手の確認は残っている）

## 総合評価
- 要修正（軽微）。
- ゲートが捕まえられない所で動作の欠陥は見つからなかった。M1 は見た目の判断なので利用者に確認する。L1 は、配置のテストがすり抜ける余地を塞ぐ改善。

## 2. 判断（こちら）

| 指摘 | 判断 | 直したところ |
|---|---|---|
| M1 余白が黒い帯になる | 受け入れる（USS で確かめた。4 画面の最初の子の背景は黒、見出しと一覧は白か #F8F8F8）。(b) にした: 画面の最初の要素が USS のカスタムプロパティ `--ios-sample-inset-color` で余白の色を宣言し、部品はそれを読む。宣言が無ければ今までどおり背景の色（トップのメニューは薄い灰色）。4 画面は白を宣言した。配置の確かめ (a) に「宣言した色がルートに写っている」を足した。見た目の判断なので利用者に報告する（黒い帯がよければ USS の 1 行ずつで戻せる） | `IosSampleSafeArea.cs`（`InsetColorProperty`、`PaddingColor`）、4 画面の USS、`IosSampleScreenDriver.CheckPadding` |
| L1 すり抜け | 受け入れる。大きさ 0 のボタン（`display: none` の祖先が無いもの）と、高さ 44 未満のボタンを問題にした。長い結果のときの一覧の窓の高さを判定にした（ボタン 1 つ分の 44 以上。R3-2 で受け入れた範囲を数値で固定）。結果を持つ画面で `ResultTextBlock` が無ければ問題にした | `IosSampleScreenDriver.cs`、`IosSampleLayoutPlayerTests.cs`（`expectResult`） |
| L2 後片付け | 受け入れる。縦に戻らなければ `Debug.LogWarning` | `IosSampleLayoutPlayerTests.cs` |
| L3 iPad の横 | 直さない。iPad は今回の対象外（シミュレーターの比べは iPhone 16、実機は iPhone XS。README の iOS 26 系の判断で、iPad は「確かめていない」とマニュアルに書く扱い）。実装の記録の残件に書く | — |
| L4 ログの形 | 受け入れる。`Awake` に先頭のログ、`Apply` を `[{LogTag}][{nameof(Apply)}]` に。`EnsureSafeArea` はファイルの慣習（`Show*` もログ無し）に合わせて足さない | `IosSampleSafeArea.cs` |
| L5 実機の所要時間 | 記録する。実機の回（V-3）で 1 つ目の回の時間を測る | 実装の記録の 5 章 |

## 3. Codex（サブエージェントの指摘の直しの後）

### レビュー概要

- 対象: `feature/UNT-14` の未コミット差分と、新規 `IosSampleSafeArea`。セーフエリア対応、横向きの見出し、共通トップメニューのスクロール化を確認した。
- 実行時の追加不具合は確認できなかった。前回の修正で追加された色の検査に、異常を見逃す条件が1件ある。
- 読み取りのみ実施。ファイル編集、Unity・シミュレーター・ビルド・テストの実行は行っていない。114件成功は実装記録の報告値として扱った。

### 重大な問題（high）

なし。

### 改善提案（medium）

- **M1. カスタムプロパティを取得できないと、余白の色の検査が成功扱いになる**
  - 箇所: /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSampleScreenDriver.cs:249
  - 具体的な失敗: `TryGetValue(...)` が `false` の場合、問題を記録せず終了する。4画面の宣言が欠落・誤記した場合、実装側の /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Common/IosSampleSafeArea.cs:119 は黒い背景色へフォールバックするが、この検査は通る。スタイル未解決の場合も同じく検査を飛ばす。前回M1への修正で追加した検査が、元の黒帯への退行を保証できていない。
  - 修正: 結果を持つ4画面では宣言の取得を必須とし、解決を期限付きで待って、未取得なら失敗させる。その後、ルートへ色が反映されたことを検査する。トップメニューは背景色へのフォールバックを別途検査する。

### 軽微な指摘（low）

なし。

### 計画整合性チェック

| 項目 | 判定 |
|---|---|
| ボタン一覧の実装網羅性 | ○ |
| UXML name と Controller の一致 | ○ |
| API 呼び出し仕様の一致 | ○ |
| 変更ファイル一覧との一致 | ○ |

### プロジェクトルール適合チェック

| 項目 | 判定 |
|---|---|
| common.md 準拠 | ○ |
| csharp.md 準拠 | ○ |
| ライフサイクル管理（登録・解除の対称性） | ○ |
| コンパイルガードの網羅性 | ○ |
| 権限ガードの網羅性 | ○（変更なし） |
| ナビゲーション統合 | ○ |
| 既存 API 互換性 | ○（静的確認の範囲） |

### 総合評価

**要修正（軽微）**。確認できた残件はM1の検査漏れ。Editor・他プラットフォームの表示について、今回の静的確認だけで動作確認済みとは判定しない。

## 4. 判断（Codex の分）

| 指摘 | 判断 | 直したところ |
|---|---|---|
| Codex M1 色の確かめが宣言の無いとき黙って通る | 受け入れる（こちらでも、直しの後のゲートの結果を見て同じ穴に気づいていた）。結果を持つ 4 画面では宣言を必須にし、無ければ問題にした。待ちは、画面を回した後の 4 秒の待ちで足りる（スタイルは解けている）。トップのメニュー（宣言なし）は、ルートの色が最初の子の背景の色と同じかを見る | `IosSampleScreenDriver.CheckPadding`（`sampleScreen` 引数） |

