# iOS Dialog UI 自動テスト計画 v1 のレビュー v1

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-dialog-ui-test-plan-v1.md](../designs/2026-11-14-ios-dialog-ui-test-plan-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

ID-1〜9・11・12 の 1.x の予想は一次ソースと合っている。キャンセルでも押した文言と `isSuccess=true` が返る（1.13.0 `IosDialogManager.swift` L212・244・287）。テキスト入力の OK で空のときは `""` が返る（L335）。ID-20・21 のイベントが同じ呼び出しの中で返るという予想も合っている（`IosDialogManager.cs` L274 ほか。dispatcher を通らない）。問題は記録の形と網羅にある。

### R-1 [A1] 記録の形: null と空文字が同じ行になり、前方一致で比べるため差分が見えない
- **根拠**: キャンセルでは入力欄の値が `nil` で返る（`UnityIosDialogManager.swift` L233・L280）。空のまま OK を押すと `""` が返る（1.13.0 L335）。`Dialog.h` L167-172 も確定時以外は NULL。計画の予想では ID-10（null）も ID-11（""）も `input=` で、ID-13 は `password-length=0`。さらに `HarnessUITestCase.result()` は `label BEGINSWITH` で照合する（L52-54）。既存の `DialogScenario` は null を `"null"` と書く。
- **壊れ方**: 2.0.0 の包みがキャンセル時に `""` を返す退行をしても、行は `input=` で始まるため「同じ」と判定される。`input=null` も `button=OKAY` も前方一致で通る。計画を流しても見つからない。
- **直し案**: `input=<null>` と `input=""` を書き分ける（`password-length=null` と `0` も同じ）。結果の行は全文一致（`label ==`）で比べる。Android の AD-17 と AD-20 と同じ区別にする。

### R-2 [B 中心契約] 網羅・一次ソース: ID-14 で 2.0.0 の既知の差分（P1-12）が抜けている
- **根拠**: 1.x は最上位の presented VC までたどる（1.13.0 L155-158）。同じ main の回で 2 回呼ぶと、2 つ目は表示途中のアラートの上に present され、UIKit に断られる。`present` の completion が来ないので、成功も失敗も返らない（0 回）。1 つ目が出終わった後に呼べば、2 つ目は上に重なる。つまり「UIKit は表示中に次を出せない」は正確ではない。2.0.0 は第 1 部 P1-12（D）と 5.13.2 で両方を出す（G-4 で「2 つとも出て 2 つ目が上」と実測済み）。
- **壊れ方**: 計画の「OK を 1 回押す」手順のままだと、2.0.0 ではアラートが 1 つ残り、tearDown（L36）で落ちる。同じイベント・同じ行 id なので、どちらのダイアログの結果かも分からない。
- **直し案**: 呼ぶ時機を 2 通りに固定する（同じフレーム／1 つ目が出た後）。ボタンの文言を `OK1` と `OK2` に分ける。押した後に決まった秒数待ってから回数を数える。1.x は「2 回目は 0 回」、2.0.0 は「両方出て 2 回」を既知の差分として期待の列に書く。

### R-3 [B 中心契約] 網羅: 「ちょうど 1 回」を確かめていない
- **根拠**: `Dialog.h` L30-31 に「the callback comes exactly once」とある。DL-01 は「0 回だった場面で来る」。`result().firstMatch` では重複や遅れて来る 2 回目を見られない。native-toolkit の D14 は増分がちょうど +1 であることを数えている。
- **直し案**: 各 ID で押した後に決まった秒数待ち、その id の結果の行がちょうど 1 本であることを確かめる。

### R-4 [B] 実現性・端末の安全: ログインのテストを `credentials` の区分なしで実機に流そうとしている
- **根拠**: native-toolkit の UI テスト設計 U-9・L100・L675 は、ログインに `credentials` の区分を求め、「利用者の前で手で確かめるまで許さない」としている。Android 計画の 1 回目の実行では、パスワード保存の確認が窓を奪って、AD-08 以降が落ちた（L133）。この repo の `DeviceRequirements.json` にはこの区分が無い。
- **直し案**: ID-12 と ID-13 は、手で確かめるまでシミュレーターだけで流す。あるいは区分を足し、`--dry-run` の説明に「AutoFill や保存の確認が出うる」と書く。

### R-5 [B] 実現性: 「シミュレーターだけ」（ID-15）を守る仕組みが無い
- **根拠**: `scripts/ios_ui_runner.py` と `DeviceRequirements.json` には区分しか無く、native-toolkit の `SIMULATOR_ONLY_TESTS` に当たるものが無い。計画 3 章の「実機は systemui と reinstall」のままだと、ID-15 も実機の許可の一覧に載りうる。
- **直し案**: シミュレーターだけの印をランナーに足す。足すまでは ID-15 を実機の一覧に載せないと明記する。

### R-6 [B] 網羅: 押せるかどうかの移り変わりと、ログインの反転の片側が無い
- **根拠**: 1.x はユーザー名とパスワードの両方が要る（L415-443）。`Dialog.h` L132 も「both fields are required」。ユーザー名だけ入れた状態、入れてから消した状態（native-toolkit の D8・D11、Android の AD-06・07）、`enableLoginWhenEmpty: true` が無い。最後の欠けは、`login_disabled_when_empty` の反転の真の側を確かめる項目が無いこと（テキスト入力の ID-11 に当たる項目）。
- **直し案**: 3 項目を足す。

### R-7 [A2] 一次ソース: ID-22 の「null の選択肢」が 2 通りに読める
- **根拠**: 配列の中の null 要素は `Encoding.UTF8.GetBytes(null)` が例外を出し、`"Internal error: …"` になる（`IosDialogManager.cs` L419、L429-446）。`"Options cannot…"` にはならない。2.0.0 は NULL 要素を入口で `INVALID_PARAMETER` として拒む（part2 6.2）。
- **直し案**: 配列が null の場合と、要素が null の場合の 2 項目に分ける。要素が null の方は既知の差分の候補にする。

### R-8 [C] 網羅: DL-06（パスワードをログに出さない）に項目が無い
- **根拠**: 1.x のブリッジは `password: %@` を平文で `Log.d` に渡す（`UnityIosDialogManagerBridge.m` L223 付近）。
- **直し案**: シミュレーターの `log show` でテストの値 `pw1234` を探す項目を足すか、4 章に「確かめない」と理由つきで書く。

### R-9 [C] 実現性: アクションシートの探し方
- アクションシートは `app.sheets` で、`alerts` ではない。tearDown は `app.alerts.count` しか見ないので（L34）、残ったシートを見逃す。
- iOS 26 系ではシートが Popover になり、`Cancel` を押せない（native-toolkit の D7）。今の既定（シミュレーター 18.3、実機 18.7.2）なら問題ない。版の前提を計画に書いておく。

### R-10 [C] 網羅: 時機
- `Show*` が戻った時点でイベントが 0 回であることを確かめる項目が無い（Android の AD-25 に当たる）。1.x は dispatcher を通す（L152）。2.0.0 は Dialog.h L27-28 で決まっている。

そのほか、アラートのボタンが押せないことは `isEnabled` で読める（D8 で実績あり）。日本語の文言のボタンと secure field への入力も自動化できる。パスワードを長さだけで記録する形はよい。

**件数**: A1 1 件、A2 1 件、B 5 件（うち中心契約 2 件）、C 3 件。

## 2. Codex

1. **A1 — ID-10／ID-13：`null` を空文字として記録し、移行後の差を見逃す。**
   - plan:31 と plan:34 は、Cancel 時を空文字／長さ 0 と予想している。
   - しかし 1.x は `inputText=nil`、`username=nil`、`password=nil` を返す（/Users/jonghyunkim/Desktop/native-toolkit/ios/UnityIosPlugin/UnityIosPlugin/Dialog/UnityIosDialogManager.swift:233、同:280）。
   - この記録形式では、移行時に `null` が非 null の空文字へ変わっても同じ結果になる。
   - 修正：期待値を `null` に訂正し、`input-is-null`、`username-is-null`、`password-is-null` を記録する。パスワードの長さは非 null の場合だけ記録する。

2. **B — ID-14：第二のアラートが表示される経路を扱えない。**
   - plan:35 は「UIKit は表示中に次を出せない」として、第二要求の破棄／失敗を予想し、`OK` を一度押す手順になっている。
   - 実装は表示済み ViewController の連鎖を最上位まで辿り、そこから次を表示する（/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Common/Presentation/ForegroundLocator.swift:45、/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Common/Presentation/PresentationProcedure.swift:116）。
   - 1.x の Dialog はこの経路を使う（/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Dialog/Presentation/AlertDialogPresenter.swift:296）。
   - 重ね表示された場合、現在の手順では両要求の結果を回収できない。
   - 修正：表示遷移中の連続呼び出しと、第一の表示完了後の第二要求を分ける。ボタン名を `First OK`／`Second OK` にし、表示された両方を閉じて結果と回数を記録する。

3. **B — ID-15／除外項目：`foregroundInactive` での受付差分が抜けている。**
   - plan:36（ID-15）は表示後のホーム往復であり、plan:63 の除外理由も背景中の呼び出しだけを扱う。
   - 1.x の検索は `foregroundActive` に限定される。一方、C ABI は `.foreground` を指定し、`foregroundInactive` も受け入れる（/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Common/Presentation/ForegroundLocator.swift:40、同:61、/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Dialog/DialogReceiver.swift:45）。
   - 現行表ではこの受付条件の変更を比較できない。
   - 修正：シミュレーターで遅延呼び出しを予約して Control Center を開き、その間に要求するケースを追加する。1.x の失敗と、2.0.0 の表示・応答を既知差分として記録する。

（注）Codex の出力をそのまま中継した。中継したサブエージェントは各 file:line を確かめていない。

## 3. 判断

利用者の指示（2026-11-14、「判断は推奨案でいい」）に従い、止める基準（A1 と中心契約の B だけを直す）で次のとおり決めた。

- **A1（2 人とも同じ件）**: null と空文字を書き分け（`input=<null>` と `input=""`、パスワードは `password=<null>` と `password-length=<n>`）、結果の行を全文一致で比べる。ID-10・ID-13 の 1.x の予想を null に直す
- **中心契約の B として直すもの**:
  - R-2・Codex の 2（2 つ続けたとき）: 呼ぶ時機を 2 通り（同じフレーム／1 つ目が出た後）に分け、ボタンの文言を `OK1`・`OK2` にし、決まった秒数の後に結果の数を数える。1.x の予想（同じフレームでは 2 つ目は 0 回、出た後なら重なる）と 2.0.0 の既知の差分（両方出る）を書く
  - R-3（ちょうど 1 回）: 各 ID で、押した後に決まった秒数待ち、その id の結果の行がちょうど 1 本であることを確かめる
  - R-4（端末の安全）: ログインの ID は、手で確かめるまでシミュレーターだけにする
  - R-5（シミュレーターだけの印）: 計画に「実機の一覧に載せない ID」を書き、ランナーに印を足すことを実装の作業にする
  - R-6 の一部（ログインの反転の真の側）と Codex の 3（`foregroundInactive`）: 既知の差分を見つけるための項目なので足す（どちらもシミュレーターだけ）
- **直さない（v2 の 6 章に理由つきで書く）**: R-6 の残り（押せるかの移り変わり）、R-7（A2。実装で 2 項目に分ける）、R-8・R-9・R-10（C）
