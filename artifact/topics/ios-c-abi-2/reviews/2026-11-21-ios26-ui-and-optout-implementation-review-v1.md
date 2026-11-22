# iOS 26 への UI テストの手当てと、通知の opt-out の確かめの実装のレビュー v1

- 記録日: 2026-11-21
- 対象: 未コミットの変更（2026-11-21 22:45 の時点）
  - iOS 26.2 への手当て: `Tools~/IosUiTests/NtkUiTests/`（SystemUI・DriverUITests・DialogUITests・ShareUITests・HarnessUITestCase）、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosDialogSamplePlayerTests.cs`
  - opt-out の確かめ（設計 [v2](../../../ios/notification/designs/2026-11-21-ios-notification-optout-check-design-v2.md)）: `scripts/verify_unity_ios.sh` の `--skip-install` と `check_installer`、`Test iOS SkipInstall Profile`、`IosNotificationOptOutProbeTests.cs`、Probe の asmdef
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）を並行で
- 1 章と 2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. Codex

- **[P1] 共有シートの再試行がアプリ本体をタップし得ます。** /Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/SystemUI.swift:68 は、dismiss region の確認後、待機を挟んで **app 基準の座標**をタップします。/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/DriverUITests.swift:291 の再試行中、座標取得後に先の dismiss が完了すると、領域が消えていても背後のアプリ画面を押します。
  **修正:** 座標を `region.element` 基準で生成し、領域が消えた場合にはタップできない形にする。

- **[P2] キャンセル失敗時の後片付けが通常の選択肢を実行します。** /Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/DriverUITests.swift:227 はキャンセル処理が失敗すると `dismissAlert` を呼び、同ファイル218行で最後のボタンを押します。Cancel が表示されない iOS 26.2 では、それは通常の選択肢です。サンプルでは /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Dialog/IosDialogManagerExampleController.cs:256 の `Documents` が選択され、キャンセルとは異なるコールバックが発火します。
  **修正:** この失敗経路では最後のボタンを押さず、確認できた Cancel または dismiss region だけで閉じる。

補足（Codex の呼び出し役の注記）: Codex は C# ドライバクライアントと DriverUITests.swift の新コマンド（sheet-cancel、password-offer-close）の引数形式が一致していると確認しました。コンパイルエラー、verify_unity_ios.sh / check_installer のバグは報告されていません。

## 2. サブエージェント

未コミット変更のレビュー結果です（読むだけ。ファイルは一切変えていません）。A の 1 件は端末の規則に関わるので、iOS 26 の端末で回す前に直す必要があります。

### 指摘

**I-1｜区分 A｜場所 Tools~/IosUiTests/NtkUiTests/SystemUI.swift:55-69（closeShareSheet、iOS 26 の経路）**
- 指摘: シートの上端が画面内にあるかを確かめていません。押す点は、古いかもしれない枠から計算したまま押されます。
  - シートが動き出す前に、画面下の位置で 0.3 s の間止まっていると、2 回の読みが一致します。例えば上端 = 852 なら、点は y≈426 です。
  - XCUITest の `tap()` は、アプリの動きが止むのを待ってから押します。そのため、計算したときの位置と押すときの画面が食い違いえます。その点は、上がり切ったシートの見出しや、連絡先・アプリの列の上に当たりえます。
- 根拠:
  - 合格の条件は `abs(top - previous) < 1` と `sheetTop - area.minY >= 88` だけで、`sheetTop` の上限がありません。
  - コメント自身が「Unity の Player では hittable が成り立たない」「1 回目の共有は押し損ねうる」と書いています。押し損ねた点に何があったかは分かっていません。
  - 端末の規則「share target に当たりうる推測の点を押さない」を保てていません。今の実機（iPhone XS）は iOS 26 に上がれないので、今はこの経路を通りません。iOS 26 の端末を使い始めた時点で効いてきます。
- 提案:
  - `sheetTop < area.maxY - 88` を加えます（シートが画面に出ていること）。
  - 押す直前にもう一度読み、上端が変わっていないこと、点がどの marker の枠にも入っていないことを確かめます。
  - 端末では、native-toolkit の CApiUITestApp と同じ固定の上寄りの点（0.1, 0.15）にし、それが `sheetTop` より上のときだけ押す、という形にすると安全です。

**I-2｜区分 B｜場所 DriverUITests.swift:288-293、SystemUI.swift:47-69**
- 指摘: 押し直しの条件は「PopoverDismissRegion がちょうど 1 つ」だけで、それを見るのは closeShareSheet の読み取りの前です。
  - その後、最大 5 s の読みの間にシートが閉じると、計算した点が Player の画面（サンプルのボタンなど）に届きえます。
  - 「the press cannot reach the player's own screen once the sheet has gone」というコメントは言い過ぎです。1 回目の押しにも同じ隙間があります。
- 根拠: 読み取りのループと `.tap()` の間で、region と marker が今もあるかを確かめていません。
- 提案: closeShareSheet の中で、押す直前に `region.count == 1` と marker があることを確かめ直します。

**I-3｜区分 B｜場所 SystemUI.swift:59、27-30（shareSheetCounts）**
- 指摘: `allElementsBoundByIndex` で番号に結び付けた要素の `.frame` や `.isHittable` を、シートが消えかけている間に読んでいます。要素が減っていると、XCTest は「Failed to get matching snapshot / No matches found」を失敗として記録します。
  - 手伝い役は `continueAfterFailure = false` なので、`test_drive` がそこで終わります。それ以降の依頼はすべて答えのないまま落ちます。
  - 読みの回数が増えたので、前の `region.element.tap()` だけのときより起きやすくなっています。
- 根拠: DriverUITests.swift:40 の `continueAfterFailure = false`。shareSheetCounts は、シートが残った失敗の経路で呼ばれます。
- 提案: `try? app.snapshot()` で 1 回だけ木を取り、識別子で辿って枠を読みます（例外になり、失敗は記録されません）。

**I-4｜区分 B｜場所 ShareUITests.swift:34-46（openAndClose）**
- 指摘: ハーネスの側には押し直しがありません。SystemUI のコメントは「1 回目の共有は押し損ねうる。手伝い役が 1 度押し直す」と書いています。iOS 26.2 では SH1 などが時々落ちえます。
- 根拠: DriverUITests.sheetClose にしかない押し直しの決まりが、ハーネスとずれています。
- 提案: 押し直しを SystemUI（例: `closeShareSheetAndWait`）に移し、両方から使います。

**I-5｜区分 B｜場所 Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosDialogSamplePlayerTests.cs:72-81（ID10）**
- 指摘: `password-offer-close` は、SampleAndAnswer が成功したときにしか送られません。結果の待ちで落ちると、iOS 26 の保存の申し出が ID11 まで残ります。
  - ID11 の alert-tap は、"Cancel" が見つからないと dismissAlert で最後のボタンを押します。それが申し出の別のボタンに当たると、シミュレーターのキーチェーンに書かれえます。
  - 申し出が alert か sheet のどちらで出るかによるので、確実ではありません。
- 根拠: 申し出の後始末が、失敗の経路にありません。
- 提案: TearDown で、テスト名が ID10 のときに `password-offer-close` を送ります（iOS 18 ではすぐ ok が返ります）。

**I-6｜区分 C｜場所 SystemUI.swift:37-38、ShareUITests.swift:36/107/119/217 ほか**
- 指摘: 「Never a guessed point」は、座標を計算して押す今の実装と食い違います。「the share sheet has no Close button」は、iOS 26 では当たらない文です。
- 提案: 計算の根拠と、押さない条件を書いた説明に改めます。

**I-7｜区分 C｜場所 DriverUITests.swift:21-22、286、294、scripts/ios_ui_runner.py:201（承認の文）**
- 指摘: 失敗の答えに、画面から数えた個数（`did-not-close-regions-N`、`markers-…`）が載ります。ところが、ヘッダは「Failures answer with fixed words only」、承認の文は「prints nothing it reads from the screen」です。個数は数字だけで、私的な情報ではありません。ただ、文に書いた規則と実際の動きがずれています。
- 提案: 規則と承認の文に「個数は出してよい」と書き足すか、`onDevice` のときは固定の語に戻します。

**I-8｜区分 C｜場所 SystemUI.swift:45-66、DriverUITests.swift:225-229、282-295**
- 指摘: closeShareSheet の `timeout` が、region を待つ時間と読み取りのループの 2 回分に使われます。そのため、sheet-close は最悪で約 60〜80 s になり、設計 3.3 の「60 s」を超えます（C# 側の 120 s には収まります）。
  - sheet-cancel は最悪で約 30〜35 s になり、C# 側の待ち `AnswerTimeoutSeconds`（30 s）より長くなりえます。失敗したとき、手伝い役の理由の代わりに C# のタイムアウトの文が出ます。
- 提案: 時間の数え方を 1 本にまとめるか、sheet-cancel 側の待ちを短くします。

**I-9｜区分 C｜場所 SystemUI.swift:87**
- 指摘: iOS 26 で押せる Cancel があると false を返します。すると sheetCancel は dismissAlert で最後のボタンを押し、`fail not-found` と答えます。native-toolkit の `tapOutsideTheSheet` と同じ厳しさなので、意図どおりと思われます。
- 提案: 意図的にしていることをコメントに書きます。

**I-10｜区分 C｜場所 scripts/verify_unity_ios.sh:468-469**
- 指摘: `--skip-install` の側の失敗の文が、いつも「installer is in the project although the profile skips it」になります。フックが走らず、ログの行が無い場合や、`skip: False` の場合（define が Editor に届いていない）でも同じ文です。
- 提案: 文を、ログの行が無い場合・define が届かない場合・ファイルがある場合に書き分けます。

**I-11｜区分 C｜場所 IosNotificationOptOutProbeTests.cs:69-83、設計 v2 の 2.3・2.4**
- 指摘: 設計 2.4 は 1 段につき 1 行（`optout <段> delegate=… listener=…`）としていますが、実装は 2 行に分かれ、listener の行に段の名前がありません。
  - 設計 2.3 は、まだ `ntk_notification_remove_interaction_listener` と書いています。実装は 7 章の R1-2 に従った正しい `ntk_notification_listener_remove` です。
- 提案: listener の行に段の名前を入れるか、設計の文を実装に合わせます。

**I-12｜区分 C｜場所 Assets/InitTestScene*.unity（と .meta）7 組（未追跡）**
- 指摘: 走っている回が残したテスト用のシーンです。コミットに入れてはいけません。
- 提案: コミットの前に index を確かめます。

### 確かめて、問題が無かったもの
- **C# のコンパイル**:
  - SampleAndAnswer の 2 つのオーバーロードは曖昧にならず、再帰にもなりません。`answer:` を名前で渡すと 2 つ目にしか当たりません。
  - `MonoPInvokeCallback`（static のメソッド、delegate は static の欄で保持、release は NULL）は IL2CPP で問題ありません。
  - ローカル関数の `-=` での外しは、同じ closure なので効きます。
- **Swift のコンパイル**: `for _ in 0..<3 where`、`if let top, let previous` などは問題ありません。
- **プロトコル**: `sheet-cancel` と `password-offer-close` は s_command の正規表現に合います。数字を含む失敗の理由も s_reason に合います。
- **iOS 18 での動き**: 実質は変わりません。sheetClose の待ちが計 15 s になり、失敗の理由の文が変わっただけです。
- **verify_unity_ios.sh の選択肢**:
  - `--skip-install` は `--device`・`--as-package-user`・`--category` と一緒には断られます。
  - EXPLORE_CATEGORY が入るので、Notification のサンプルの回は流れず、IosUiDriver も要求されません。
  - check_installer は、実機の回と Notification のサンプルの回を含め、すべての Player の回で走ります。
  - define が Player に届かずテストが 0 件のときは、ios_player_results で落ちます。
- **OO-3 の警告の拾い方**: RegisterListeners は main thread で同期に LogWarning を出すので、`logMessageReceived` が拾います。Awake の警告も AddComponent の中で同期に出るので、LogAssert.Expect が受けます。
- **7 章のチェックリスト（A2）と R1-11**: すべて満たしています。

件数: A 1、B 4、C 7（計 12）

## 3. 判断

すべて直したうえで、確かめの回を流し直した（結果は記録の別の場所に書く）。

| 指摘 | 扱い |
|---|---|
| Codex P1、I-2 | 押す点は `PopoverDismissRegion` 自身の座標から作る。押す直前にもう一度読み、領域が 1 つ、上端が同じ、点がシートのどの要素の枠にも入っていないことを確かめる |
| Codex P2 | `sheet-cancel` が失敗したときは何も押さない |
| I-1（A） | シートの上端が領域の中（上から 88pt 以上、下から 88pt 以上）にあるときだけ押す。**実機では iOS 26 の計算した点を押さない**（`closeShareSheet` が false を返す）。今の実機（iPhone XS）は iOS 18 のままなので、実機の回には影響しない。iOS 26 の端末を使うときは、改めて押し方を決める |
| I-3 | 枠と数は `app.snapshot()` を 1 回取って読む（失敗を記録しない） |
| I-4 | 押し直しを `SystemUI.closeShareSheetAndWait` に移し、手伝い役とハーネスの `openAndClose` の両方から使う |
| I-5 | ID10 の後の「パスワードを保存しますか?」は、テストの後片付け（TearDown）で閉じる。ID10 が途中で落ちても閉じる |
| I-6 | コメントと失敗の文を、今の押し方に合わせて書き直した |
| I-7 | 数を載せるのはシミュレーターだけ。実機の答えは決まった語。手伝い役の決まりのコメントに書き足した（承認の文は「画面から読んだ文字を出さない」のままで食い違わない） |
| I-8 | `sheet-cancel` の待ちを 5 秒に縮めた（合わせて約 15 秒で、C# の 30 秒に収まる） |
| I-9 | 意図どおり。コメントを足した |
| I-10 | 失敗の文を 3 つに書き分けた |
| I-11 | 設計は直さない（v2 の 7 章が正しい関数名を書いている）。ログの形の違いは記録に書く |
| I-12 | 回の残り物で、片付けた。コミットの前に index を確かめる |
