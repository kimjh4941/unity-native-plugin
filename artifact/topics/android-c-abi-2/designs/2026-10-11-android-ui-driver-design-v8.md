# Android の Player テストから OS の画面を操作する補助（ホストの adb）v8

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Android の層 2b（実機の Player の PlayMode テスト）で、ダイアログ・Share の選択画面・通知の許可とシェードを押す、読む |
| 最初の利用者 | Dialog（`artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`。Dialog 設計 v4 7.3、8.3） |
| 後の利用者 | **Share**（`artifact/android/share/designs/2026-10-11-android-share-design-v6.md` の「層 2b」と「呼び出し側を止める OS の画面」の節。この版で足した）、Notification（親課題の「進め方」の段 3） |
| Windows との違い | Windows では、同じ役割のもの（ダイアログを外から閉じる PowerShell の「閉じる役」）を Dialog の UI テスト計画 v1 の 2 章に書いた。Android では Dialog・Share・Notification が共有し、ホストと端末の受け渡しや端末の設定の戻し方まで含むので、親課題の下の独立した文書にした（2026-10-11、利用者と相談） |
| 方針の出典 | `agent-rules/coding-rules/testing.md` 2 節（Android は「Appium + UiAutomator2、または UiAutomator」）。Windows は同じ節の考え方で、Appium ではなく PowerShell の Win32 メッセージを選んだ（`artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md`） |
| レビュー | `artifact/android/dialog/reviews/2026-10-11-android-dialog-design-review-v1.md`、`...-review-v2.md`、`...-review-v3.md`（v3 で A1 が 0 になり、レビューを止めた）。Share の分は `artifact/android/share/reviews/2026-10-11-android-share-ui-test-plan-review-v1.md`、`...-review-v2.md`（Share の UI テスト計画と一緒に見た） |

## v7 からの主な変更（v8。Share の UI テスト計画のレビュー v2 を受けて。2026-10-12）

レビュー v2 では、どちらのレビュアーも製品の振る舞いの指摘は無く、補助の約束の穴を挙げた。ID の前の「S:」はサブエージェント、「X:」は Codex。

- **引数の読み取りも Sharesheet の命令の中で行う**（3.3。S:A1-1、X:A1-2）。テストは依頼を出したフレームで Sharesheet を開くので、Base64 の誤り、引数の数の誤り、範囲の外の数も、ほかの失敗と同じく閉じてから決まった語で答える
- **閉じる処理と前に戻す処理は例外を外に出さない**（3.3。X:A1-3）
- **失敗のときは、閉じた後にこのアプリを前面に戻す**（3.3。S:B-3）。どの失敗でも、前面を確かめ、違えば前に戻す。期限とは別の上限（20 秒）を持ち、戻ったことも確かめる（戻らなければ `fail not-in-front`）。`tap-in-sheet` の前面の確かめは `wrong-app` を返すだけにした（戻すのは失敗の経路）
- **`no-sheet` は読めた画面だけで判定する**（3.1。X:A1-1、S:B-1）。読めなかった回は読み直し、最後の読み取りは指定の時間の終わりかその後。読めないまま期限が来たら `timeout` で、Sharesheet を閉じる手順に入る
- **途中の読み取りの失敗は読み直す**（6 章。S:B-2）。Sharesheet の中を探す途中の読み取りも、期限まで 1 秒おいて読み直す
- **答えを書く時間も上限に入れる**（3.3。X:A1-4）。答えの書き込み（`run-as`）の上限を 10 秒にした。最悪の時間は 136 秒、テストの待ちは 150 秒
- **端末のダンプを消せなかったら、補助の終了コードを 1 にする**（3.4、6 章。X:A1-5）
- **テストの側は `ok` と `ok <値>` だけを成功とみなす**（5 章。X:A1-6）。`Do` は Sharesheet の命令に 150 秒を使う（S:B-7）
- **実機で流し直した**（2026-10-12、Pixel 6a、1.x の Manager）: v7 のときの一時的なテスト 9 件に、引数を欠いた `absent-in-sheet`（Sharesheet を開いた後に届く）を足した 10 件が、すべて通った。どの後も Sharesheet は残らず、このアプリが前面にいた（`hasWindowFocus`）。引数を欠いたものは `fail error` で答え、シートを閉じた。テストの側の比べ方の誤り（v7 の記録）も直した。補助の終了コードは 0
- **`doctest` を足した**（8 章。S:B-4、X:B-1）。上限の数そのもの、引数の誤り、予期しない例外、読み取りの一時と連続の失敗、押し直し、期限の近くでの別のアプリ、ダンプを消せないとき、時間切れの読み取り。決まりを壊した版は 22 通りにした

## v6 からの主な変更（v7。Share の UI テスト計画のレビュー v1 を受けて。2026-10-11）

レビュー v1 の指摘（U1-* はサブエージェント、CX-* は Codex）は、どれも補助とテストの計画の穴で、製品の振る舞いの指摘は無かった。

- **`absent-in-sheet` に在るべき文言（アンカー）を足した**（3.1。U1-1、CX-7）。先にアンカーが出るのを待ち、その木から「無い」を判定する。Sharesheet が中身を並べ終える前の木や、何も出ていない木では通らない
- **命令 `close-sheet` と `no-sheet` を足した**（3.1。U1-2、U1-17）。Sharesheet を閉じるのに `back` を使わない（`back` は閉じたことを確かめず、時間の上限も無い）。Sharesheet が開かないはずの呼び出しは `no-sheet` で見張り、開いてしまったら閉じて `fail sheet-opened` にする
- **閉じ方**（3.3。U1-9、CX-3）: Back を 4 回まで押す（広げたシートは 1 回目の Back で畳まれるだけのことがある）。それでも閉じなければ Sharesheet のプロセスを止める（`am force-stop com.android.intentresolver`）。閉じられなかったときは、元の理由ではなく `fail did-not-close` を返す。V-15、V-16 で確かめた
- **時間の上限**（3.3。CX-1、U1-14）: 期限を過ぎたら adb の呼び出しを始めない（v6 の実装は最低 1 秒を渡していた）。閉じる処理には別の上限（Back に 15 秒、プロセスを止めるのに 10 秒）。最悪の時間と、テストの待ち（120 秒）を直した
- **どの例外でも閉じ、固定の語で答える**（3.3、3.4。CX-2、U1-6）: 読み取った XML が壊れていたとき（`bad-dump`）も、adb の失敗の文言も、Sharesheet の命令の答えには入らない（決まった語のどれでもなければ `error`）
- **`tap-in-sheet` の前面の確かめ**（3.1。U1-3）: 押した後、このアプリが前面に来るのを待つ。別のアプリが来たら（同じ名前のアプリを押したとき）、このアプリを前に戻して `fail wrong-app`。押し直しのときに Sharesheet が消えていれば、前の押下が効いたとみなす（U1-18）
- **プライバシーの穴**（3.4。U1-16）: 読み取りが時間切れ・壊れていたときも端末のダンプを消す。命令の終わりと補助の終わりにも消す。Sharesheet が出ている木での `expect` の失敗は値を入れない
- **テストの側**（5 章。CX-4、CX-5、U1-5）: `Send` は `IEnumerator` で、番号は `LastSent`（v6 の設計の `int Send` から変えた。最初の依頼の前に `ready` を待つため）。`WaitForPending` は途中の失敗で止まらず、全部の答えを回収してからまとめて失敗する。未回収の数 `PendingCount` を足した
- **実機で確かめた**（9 章）: 1.x の Manager で、新しい命令と閉じ方を 9 件の一時的なテストで流した（2026-10-12）。`close-sheet`、アンカー付きの `absent-in-sheet` の 3 通り（`ok`、`fail present`、`fail anchor-not-found`）、`no-sheet` の 2 通り（`ok`、`fail sheet-opened`）、`tap-in-sheet` の前面の確かめ、`WaitForPending` の回収（先の依頼が `fail not-found` でも後の依頼の `ok 0` を回収し、未回収 0）、V-15、V-16。どれも補助の答えは期待どおりで、どの後にも Sharesheet は残らなかった。補助の終了コードは 0。一時的なテストのうち 3 件は、テストの側の比べ方の誤り（答えを `fail <語>` と完全一致で比べたが、`Wait` の失敗の文言に包まれていた）で不合格と出たが、答えの語は期待どおり
- **決まりを `doctest` で固めた**（8 章。CX-6）: 端末の代わりの `ScriptedDevice` の上で、閉じ方、固定の語、時間の上限、読み取りを残さないことを確かめる。わざと壊した 13 通りをすべて捕まえることを確かめた

## v5 からの主な変更（Share のため。2026-10-11）

Share の設計 v6（7.3、8.3）とそのレビュー（v1〜v6。持ち越しの K-3）を受けて、Sharesheet を扱えるようにした。

- **依頼を先に出し、後で答えを待つ口**（5 章）。Sharesheet が出ている間は、アプリの Activity が `PAUSED` になり Unity が止まる見込み（要検証 V-11。v7 の時点で確かめた）。止まっている間はテストが次の依頼を出せないので、テストは依頼を先に出し、同じフレームで Share を開き、Unity が戻ってから答えを待つ
- **Sharesheet の命令 3 つ**（3.1）: `tap-in-sheet`（探して押す）、`absent-in-sheet`（無いことを確かめて閉じる）、`present-in-sheet`（在ることを確かめて閉じる）。どれも 1 つで完結し、失敗でも Sharesheet を閉じてから `fail` を返す
- **命令の時間の上限**（3.3）。Sharesheet の命令は全体に 60 秒の期限を持ち、中の adb の呼び出しには残り時間を上限として渡す（K-3。今の `dump` は adb の呼び出し 3 つで、既定の上限のままだと 1 回に最大 75 秒かかる）。テストの待ちは 90 秒（v7 で 120 秒に変えた）
- **プライバシー**（3.4）。Sharesheet にはほかのアプリの共有先と連絡先が出る。Sharesheet が出ている間は、画面の構造を書き出さず、端末の上のダンプを消し、失敗の理由に読み取った値を入れない
- Share の要検証（9 章の V-11〜V-14）

## v4 からの主な変更（v5。実装で分かったこと。2026-10-11）

Dialog の 1.x の UI テスト（UI テスト計画 v1 の AD-01〜33 のうち 1.x で流すもの）を、補助を作って実機で 4 回流した。4 回目に 42/42。直したことと、確かめた要検証をこの版に写した。実装は `scripts/android_ui_driver.py`、`Tests/PlayMode/AndroidUiDriver.cs`、`Tests/PlayMode/AndroidSampleScreenDriver.cs`、`scripts/verify_unity_android.sh`。

- **自動入力を切る**（4 章）。ログインのダイアログのパスワードの欄に入れて送ると、Google の自動入力が「Save password to Google Password Manager?」を出し、それが操作の対象の窓を取る。`uiautomator dump` はその窓しか読まないので、以後のテストがすべてダイアログを見つけられなくなった（1 回目、AD-08 以降の 19 件）。テストの値（`user01` / `pass02`）は「Not now」で閉じ、保存させていない
- **外側のタップの基準**（3.1）。`uiautomator dump` が読むのはダイアログの窓だけで、いちばん外の部品がダイアログの窓そのもの。v4 の「画面全体を覆わない最も外側の祖先」は、画面全体の部品が木に無いので成り立たなかった。基準はダイアログの窓（木のいちばん外の部品）、押す点はその上端と画面の上端の中点にした
- **回転**（3.1）。Unity の `AutoRotation` はセンサーに従うので、`user_rotation` を変えても、机に置いた端末では回らない。テストはアプリから `Screen.orientation` で回し、補助は `rotation` で表示が回ったことを確かめるだけにした（`rotate` の命令は残すが、Dialog のテストは使わない）
- **後片付けを途中で切らない**（4 章）。Unity はテストの実行の最後に adb のサーバーを止める。補助の logcat がそこで切れて後片付けに入り、その最中にスクリプトの `SIGTERM` が届いて、設定を戻しきれなかった（1・2 回目）。後片付けに入ったら合図を無視する
- **状態ファイル**（4 章）。変える前の値をホストのファイル（`--state-file`）にも書き、戻し終えたら消す。補助が途中で止まったら、次の起動の最初にそこから戻す。2 回目の後にこれで 7 つの設定を戻せた
- **期限切れのときの画面**（6 章）。最後に読んだ木を `--dump-dir` に残す。外側のタップの誤りはこれで分かった
- **`adb shell` の引用**（6 章）。adb は引数を空白でつないで端末のシェルに渡すので、`run-as … sh -c '…'` は全体を 1 つの文字列にして渡す（分けると `sh -c` の後ろが `run-as` の外で走る）
- **ゲートの判定**（7 章）。Test Android™ Profile の Player にはサンプルのシーンが入らない（その Profile のシーンはテスト用だけ）ので、サンプル画面を通すテストは Ignore になる。カテゴリ `AndroidUiDriver` の skip の判定は、Android™ Profile の実行だけに当てる
- 要検証の結果は 9 章

## v3 からの主な変更（v4）

レビュー v3 の B と C を反映した（新しい A1 は無い）。

- N-2、Codex の新規 1: `background` は、アプリの Activity が `STOPPED` になったことを確かめてから `ok` を書く（ネイティブの前面の判定は `started && !stopped` で、`PAUSED` では前面のまま）
- N-5: `tap-outside` の基準を、選び方に合う部品を含むダイアログの窓の最上位の部品にした
- L-3: ユーザー ID は `android.os.Process.myUid() / 100000` で取る
- L-4: 端末の時刻の取り方の引用
- L-5: `background` の待ち時間の上限と、命令の打ち切りとの関係
- L-6: 設定は最初に変えるときに 1 回だけ覚える
- L-7: 7 章の文言
- L-8: 結果の XML のカテゴリの出方を要検証に足し、判定は親の suite のカテゴリも継ぐ
- N-1: 回る向きの前提（アプリが縦に固定されていないこと）を要検証に足した。固定を外すのはテストの側（Dialog 設計 v4 の 7.3）
- 10 章「拡張の見通し」を足した（利用者との相談、2026-10-11。レビューの後に足したもの）

## v2 からの主な変更（v3）

- ゲート（R2-M-2、Codex v1 の 3）: 補助は `READY` を書けなかったとき、`HELLO` から `READY` まで 5 秒を超えたとき、終了コードを 0 以外にする。補助は `run_tests` の 1 回ごとに起動・停止する。結果の XML でカテゴリ `AndroidUiDriver` のテストの skip が 0 であることを判定に入れる（7 章）
- ユーザー（Codex v2 の 2）: `HELLO` に Android のユーザー ID を入れ、補助は `run-as <package> --user <id>` で書く。`READY` の前に、補助が書いたものをテストが読んで消す往復を 1 回行う
- 画面の点灯（Codex v2 の 3、R2-M-6）: `stay_on_while_plugged_in` の値を覚えて戻す。`svc power stayon` は使わない
- `rotate`（Codex v2 の 4）: 完了の条件を「今の向きが指定した向きと同じ」にする。向きを読む `rotation` を足した。テストは今と違う向きを選ぶ
- `background`（R2-M-3）: 後ろに回し、止まったことを確かめてから結果を書き、時間を置いて前に戻す命令を足した
- 細部（R2-M-6）: `tap-outside` を部品の `bounds` から計算する。`back` は `wait` と同じ期限で待つ。新しい `<run>` の `HELLO` で切り替える。時刻は端末で取る。`fail` の理由は英数字と `-` だけ
- `run-as` が使えないとき（R2-M-4）: V-1 に debuggable の確認を足し、代わりの手段（`adb reverse` のソケット）を書いた

## v1 からの主な変更（v2）

- 補助が応答したかで分けた（`HELLO` と `READY`）。実行ごとの `<run>`、一時ファイルと名前の変更。`run-as` とアプリの内部の領域に一本化。`back` に選び方。依頼の行の長さの上限。`type` の文字の制限。設定を戻す経路。層 1 のテストをやめた

## 1. 何を作るか

ホストの Python のスクリプト `scripts/android_ui_driver.py` が、テストの実行の間 `adb logcat` を読み、テストからの依頼の行に応じて `adb shell uiautomator dump` と `adb shell input` で画面を操作し、結果を端末のファイルで返す。

```
Player のテスト                                    ホスト（android_ui_driver.py）
  Debug.Log("NTK-UI-HELLO|<run>|<package>|<user>")
        ── logcat ────────────────────────────▶   run を切り替える。<files>/ntk-ui/<run>/ を作り直す
                                                   probe を書く
  probe を読んで消す                    ◀──────
                                                   probe が消えたのを見て ready を書く
  <files>/ntk-ui/<run>/ready を待つ     ◀──────
  Debug.Log("NTK-UI|<run>|7|tap|...")
        ── logcat ────────────────────────────▶   uiautomator dump → 部品を探す → input tap
  <files>/ntk-ui/<run>/7 を待つ         ◀──────   .7.tmp に書いて 7 に名前を変える
  結果（ok / fail 理由）を読む
  イベントを待って値を照らす
```

`<files>` はアプリの内部の領域（`Context.getFilesDir()`）。補助は `adb shell run-as <package> --user <user>` でここに書く。テスト用の Player は development ビルドで debuggable なので `run-as` が使える（V-1 で確かめる）。

`scripts/verify_unity_android.sh` が、実機の PlayMode の実行の 1 回ごとに補助を起動し、終わったら止める。

## 2. 選ばなかった案

| 案 | 選ばなかった理由 |
|---|---|
| Appium + UiAutomator2 | Node と Appium の導入と版の固定が要る。この用途（押す、入れる、読む）には過ぎる。クリップボードの層 3 で既定 IME を変える副作用もある（`testing.md` 2 節） |
| アプリの中から操作する（`Instrumentation` で自分の窓へキーを送る、Java の反射でボタンを押す） | 自分のプロセスの窓にしか届かない。Share の選択画面と通知のシェードはシステムの UI で、届かない |
| instrumentation のテスト APK（UiAutomator）を別に入れる | Unity の Test Runner の Player と別の APK を組み、同時に走らせる仕組みが要る。adb の `uiautomator dump` と `input` で同じ部品に届く |
| 結果を外部の領域（`/sdcard/Android/data/<package>/files`）で返す | Android 11 以降の保存の制限で、シェルが書けるかが端末と版に左右される。`run-as` は debuggable なら確実 |

**`run-as` が使えない端末のための代わりの手段**（V-1 が通らなかったときに切り替える。今は作らない）: 補助が `adb reverse tcp:<port> tcp:<port>` を張ってホストで待ち、テストが `127.0.0.1:<port>` に依頼を送り、同じ接続で結果を受け取る。logcat も保存領域も使わない。

`uiautomator dump` は UiAutomator と同じアクセシビリティの木を XML で出す。native-toolkit のテスト（`android_library_capi_test` の `UiDriver.kt`、サンプルの `ViewDialogs.kt`）が使う選び方（`resource-id`、`text`、`class`、`checked`）は、そのまま XML の属性で使える。

## 3. 依頼の形

テストは `Debug.Log` で 1 行を出す。Unity はこれを logcat のタグ `Unity` に出す。

```
NTK-UI-HELLO|<run>|<パッケージ名>|<ユーザー ID>
NTK-UI|<run>|<番号>|<命令>|<引数 1>|<引数 2>...
```

- `<run>` はテストのプロセスが最初に 1 回作る GUID（32 桁の 16 進）。補助は新しい `<run>` の `HELLO` を受けたら、それに切り替え、前の `<run>` のフォルダを消す。今の `<run>` 以外の依頼の行は捨てる
- `<ユーザー ID>` は `android.os.Process.myUid() / 100000`（`<files>` のパスから取ると、外付けの保存領域〈adoptable storage〉の `/mnt/expand/<uuid>/user/<id>/...` で外れる）
- `<番号>` は `<run>` の中で 1 から増やす整数（テストの間で戻さない）
- 文字列の引数は Base64 の UTF-8。命令の表で「生」と書いたものはそのまま
- **1 行は UTF-8 で 1000 バイトまで。** テストの側で組み立てるときに超えたら `ArgumentException`（logcat の 1 件は 4076 バイトで切れる。Unity の前置きと余裕を見て 1000 にした）
- 補助は、起動より前の行を読まない（起動時に `adb logcat -c` で消し、端末の時計で取った起動の時刻を `-T` に渡す。時刻は `adb shell "date '+%m-%d %H:%M:%S.000'"` のように 1 つの文字列で渡す。`subprocess` の引数に分けると、adb が空白でつないで端末の側で 2 つの引数に割れる）

### 3.1 命令

| 命令 | 引数 | すること |
|---|---|---|
| `wait` | `<選び方>` | 部品が出るまで木を読み直す（期限 10 秒） |
| `wait-gone` | `<選び方>` | 部品が消えるまで待つ（同じ期限） |
| `tap` | `<選び方>` | 部品が出るまで待ち、`bounds` の中心を `input tap`。押す前に部品が `enabled="true"` であることを確かめる（無効なら `fail disabled`） |
| `tap-outside` | `<選び方>` | 部品が出るまで待つ。**基準は、その部品を含むダイアログの窓**（木のいちばん外の部品。`uiautomator dump` は操作の対象の窓だけを読むので、ダイアログでは窓そのものがいちばん外にある）。押す点は、窓の横の中央、画面の上端と窓の上端の中点。窓の上端が 48 ピクセルに満たなければ `fail no-outside`。`doctest` で固めた |
| `type` | `<選び方>`、`<文字列>`（生） | 部品を押してから `input text`。**文字列は `[a-z0-9]` の 1〜64 文字だけ**（範囲外は `fail bad-text`）。シェルの escape と `%s` の変換、IME の自動修正・大文字化を避けるため |
| `back` | `<選び方>` | 部品が出るまで待つ（`wait` と同じ期限）。入力のキーボードが出ていれば（`dumpsys input_method` の `mInputShown=true`）、先に 1 回 Back で閉じ、閉じたことを確かめる。そのあと `input keyevent KEYCODE_BACK`。閉じたことは確かめない。**Sharesheet には使わない**（`close-sheet` を使う。v7） |
| `expect` | `<選び方>`、`<属性>`（生）、`<値>` | 部品が出るまで待ち、属性（`text`、`enabled`、`checked`、`password`）が値と同じか。`text` は大文字と小文字を区別する |
| `count` | `<選び方>` | 合う部品の数を結果に書く（待たない） |
| `rotation` | なし | 今の向き（`uiautomator dump` の木の `rotation` 属性、0〜3）を結果に書く |
| `rotate` | `0` / `1` / `2` / `3`（生） | `settings put system accelerometer_rotation 0` と `user_rotation <値>`。今の向きが指定した値と同じになるまで待つ（期限 10 秒）。**Unity の `AutoRotation` はセンサーに従うので、これでは回らない。Dialog のテストはアプリから `Screen.orientation` で回し、この命令は使わない**（v5） |
| `background` | `<ミリ秒>`（生。100〜5000） | `input keyevent KEYCODE_HOME`。アプリの Activity が **`STOPPED`** になった（`dumpsys activity activities` の状態が `STOPPED`）ことを確かめてから、結果の `ok` を書く（ネイティブの前面の判定は `started && !stopped` なので、`PAUSED` では前面のまま。ここまでが 10 秒の打ち切りの対象）。そのあと指定の時間待ち（打ち切りの対象の外。上限 5 秒）、`am start` で `<パッケージ名>` の起動の Activity を前に戻す。戻ったこと（`RESUMED`）は確かめるが、結果はもう書いてあるので、戻せなかったときは補助の終了コードを 1 にする |
| `home` | なし | `input keyevent KEYCODE_HOME` |
| `front` | なし | `cmd package resolve-activity` で `<パッケージ名>` の起動の Activity を引き、`am start -n` で前に戻す（`monkey` は使わない。別の Activity を起こすことがある） |
| `tap-in-sheet` | `<文言>`（Base64） | **Sharesheet の命令**（3.3、3.4）。Sharesheet（パッケージ `com.android.intentresolver`）が出るのを待つ（10 秒）。その中で `text` が文言と同じ部品を探し（大文字と小文字を区別する）、無ければ画面の中央で 3/4 の高さから 1/4 の高さへ上にスワイプして探し直す（8 回まで。1 回目はシートを広げ、2 回目からは一覧をスクロールする。native-toolkit の `UiDriver.kt:219-228` と同じ）。見つけたら中心を押し、Sharesheet が消えるのを待つ（10 秒）。押した要素が古くなって消えなければ、3 回まで探し直して押す（押し直す前に読んだ木に Sharesheet が無ければ、前の押下が効いたとみなす。3 回押しても残れば `fail still-open`）。**消えた後、このアプリが前面に来るのを待つ**（`dumpsys activity activities` の `topResumedActivity` のパッケージ。10 秒）。別のアプリが来たら `fail wrong-app`（v7）。このアプリを前に戻すのは、失敗の経路（3.3。v8） |
| `absent-in-sheet` | `<無いはずの文言>`、`<在るはずの文言>`（どちらも Base64） | Sharesheet が出るのを待ち、**在るはずの文言（アンカー）が出るまで**、スワイプせずに木を読み直す（10 秒。出なければ `fail anchor-not-found`）。アンカーの出た木から、`tap-in-sheet` と同じ探し方で、無いはずの文言の部品が**無い**ことを確かめる（8 回スワイプしても無ければ `ok`、在れば `fail present`）。確かめた後、閉じる（3.3）。アンカーは、Sharesheet が中身を並べ終えたことの印。テストは、無いはずのものと同じ並びに出る、在るはずのもの（同じ呼び出しの正しい Chooser Action など）を選ぶ（v7） |
| `present-in-sheet` | `<文言>`（Base64） | Sharesheet が出るのを待ち（10 秒）、文言の部品が**在る**ことを確かめる（スワイプしない。プレビューの本文など、シートの上の方にあるもの。Sharesheet が出てから 10 秒、木を読み直す。合わせて最大 20 秒。出なければ `fail not-found`）。確かめた後、閉じる（3.3） |
| `close-sheet` | なし | Sharesheet が出るのを待ち、閉じる（3.3）。開いたことと閉じたことの両方を確かめる（v7） |
| `no-sheet` | `<ミリ秒>`（生。1000〜30000。範囲の外は `fail bad-span`） | 指定の時間、木を読み直して Sharesheet が出ないことを確かめる（`ok`）。出たら閉じて `fail sheet-opened`。Sharesheet が開かないはずの呼び出しの前に `Send` する。開いてしまったとき、Unity が止まったまま誰も閉じない、を防ぐ（v7）。**読めた画面だけで判定する**: 読めなかった回は読み直し、最後の読み取りは指定の時間の終わりかその後。読めないまま 60 秒の期限が来たら `fail timeout`（閉じる手順に入る。v8） |

`<選び方>` は `属性=値` を `&` でつないだもの（生。値は Base64）。属性は `resource-id`、`text`、`class`、`package`、`index`（同じ選び方に合う部品のうち、上から何番目か。0 から）。Sharesheet が出たことを確かめて閉じるのは `close-sheet`、閉じた後に残っていないことは `count package=com.android.intentresolver` で確かめる。例: `class=YW5kcm9pZC53aWRnZXQuRWRpdFRleHQ=&index=1`（2 つ目の `EditText`）。

### 3.2 結果

`<files>/ntk-ui/<run>/<番号>` に 1 行: `ok`、`ok <値>`（`count`、`rotation`）、`fail <理由>`。理由は英数字と `-` だけ（`run-as … sh -c '…'` の中で引用を要らなくするため）。
補助は `<番号>.tmp` に書いてから `mv` で `<番号>` にする（同じフォルダの中の名前の変更。どちらも `run-as` の中で行うので、所有者はアプリ）。

テストの待ち方:

| 状況 | テスト |
|---|---|
| `HELLO` の後、`ready` が 5 秒来ない | 補助がいないとみなし、そのテストを `Assert.Ignore`。以後の `Do` も Ignore |
| `ready` が来た後、結果が 20 秒来ない（Sharesheet の命令は 150 秒。3.3） | `Assert.Fail("ui driver did not answer")` |
| `fail ...` | `Assert.Fail` |

20 秒は、補助の 1 命令の期限 10 秒と、`uiautomator dump`（1〜3 秒。Pixel 6a で、ダンプの削除と読み出しを合わせて 2 秒あまり）と `run-as` の往復の余裕。`background` は指定の時間に関わらず、止まったことを確かめた時点で結果を書くので同じ期限で足りる。

### 3.3 Sharesheet の命令の時間の上限と閉じ方

- Sharesheet の命令（`tap-in-sheet`、`absent-in-sheet`、`present-in-sheet`、`close-sheet`、`no-sheet`）は、**命令の全体に 60 秒の期限**を持つ（引数の読み取り、出るのを待つ 10 秒、探してスワイプする 8 回、消えるのを待つ 10 秒を含む）。期限は手順の合間に見て、超えたら探すのをやめる。引数の読み取り（Base64、数、範囲）も命令の中で行うので、引数の誤りでも Sharesheet を閉じてから答える（v8）
- **中の adb の呼び出しには、残り時間（と、ふだんの上限の小さい方）を上限として渡す**（Share の設計レビュー v6 の K-3）。`dump` は `rm -f`（ふだんの上限 30 秒）、`uiautomator dump`（15 秒）、`exec-out cat`（30 秒）の 3 つで、上限をそのままにすると 1 回に最大 75 秒かかり、期限を大きく超える。**期限を過ぎたら adb の呼び出しを始めず**、`timeout` で止める（v7。v6 の実装は最低 1 秒を渡していた）
- **失敗・見つからない・期限切れ・予期しない例外のどれでも、Sharesheet が画面にあれば閉じ、このアプリを前面に戻してから `fail` を返す**（Sharesheet が出ている間は Unity が止まる〈V-11〉ので、テストの後片付けでは閉じられない。別のアプリが前面に残っても Player は止まったまま）。閉じる処理と前に戻す処理は、例外を外に出さない（v8）
- **閉じ方**（v7）: 期限とは別の上限で行う。
  1. 木を読み、Sharesheet が無ければ終わり。あれば Back を押して 1 秒待つ。これを 4 回まで（全体で 15 秒）。8 回スワイプして広げたシートでも閉じることは V-15 で確かめた。Pixel 6a の読み取りは 1 回 2 秒あまりなので、Back が 3 回を超えて要るときは 15 秒に収まらず、次の手順に進むことがある（それでも閉じる）
  2. それでも残れば、Sharesheet のプロセスを止める（`am force-stop com.android.intentresolver`）。消えるのを待つ（10 秒）。止めるとシートが消え、Player が戻ることは V-16 で確かめた
  3. それでも残れば `fail did-not-close`（元の理由の代わりに。補助の標準エラーに元の理由の語を出す）。閉じ方を 2 回は繰り返さない
- **前に戻し方**（v8）: 閉じた後、`topResumedActivity` のパッケージがこのアプリでなければ、`front` と同じ手順で前に戻し、前面に来たことを確かめる。期限とは別に 20 秒（adb の呼び出し 4 回分。adb が 1 回 5 秒かかる端末でも収まる）。戻らなければ `fail not-in-front`
- **最悪の時間**: 60 秒（命令）＋ 15 秒（Back）＋ 10 秒（プロセスを止める）＋ 20 秒（前に戻す）＋ 20 秒（時間切れの読み取りの後の端末のダンプの削除。1 回 5 秒、最大 4 回）＋ 1 秒 ＋ 10 秒（答えの書き込み）＝ 136 秒（v8。v7 は前に戻す時間と答えの書き込みを数えず 106 秒としていた）。adb の呼び出しがどれも時間切れになる端末での値で、`doctest` が上限の数と合計を固め、adb の呼び出しが 1 回 7 秒かかる模擬の端末で収まることを確かめる（8 章）。ふつうは、失敗しても閉じるのに数秒
- テストの側の答えの待ちは、Sharesheet の命令だけ **150 秒**（3.2 の 20 秒の代わり。v6 は 90 秒、v7 は 120 秒）。待ちは `Wait` を始めた時点から数える
- ほかの命令の期限（10 秒）は変えない

### 3.4 プライバシー（Sharesheet）

Sharesheet には、ほかのアプリの共有先と、連絡先（Direct Share の候補）が出る。native-toolkit の UI テストの決まり（native-toolkit の `artifact/topics/android-c-abi/designs/2026-10-03-android-c-abi-ui-test-design.md:33`「失敗したときもシェードと Sharesheet の画面の構造を書き出さない」）と同じにする。

- 読み取った木に Sharesheet のパッケージの部品があれば、その木は**書き出さない**（`--dump-dir` の `keep_last_dump` を、どの命令でも飛ばす）。読み取りが時間切れになったとき、XML として読めなかったときも同じ（Sharesheet の一部かもしれない。v7）。Sharesheet の命令の終わりには、最後に読んだ木（押した後の別の画面も）を捨てる（v7）
- 読み取った後に、**端末の上のダンプ**（`/data/local/tmp/ntk-ui-dump.xml`）を消す（Sharesheet が出ているとき、時間切れ・読めなかったときは毎回。ほかのときは今と同じく次の読み取りの前に消す）。消すのに失敗しても、Sharesheet の命令の終わりと補助の終わりにもう一度消す（v7）。補助の終わりでも消せなければ、標準エラーに出して終了コードを 1 にする（ゲートが落ちる。v8）
- Sharesheet の命令が失敗したとき、**理由は決まった語だけ**（`timeout`、`bad-dump`、`no-screen-size`、`sheet-did-not-open`、`sheet-closed`、`not-found`、`anchor-not-found`、`present`、`still-open`、`did-not-close`、`wrong-app`、`not-in-front`、`sheet-opened`、`bad-span`）。それ以外の失敗（adb の失敗の文言、予期しない例外）は `error` にする（v7）。`expect` の失敗は読み取った値を理由に入れるが、Sharesheet が出ている木では値を入れず `fail mismatch` にする（v7）
- 読み取った木は判定にだけ使い、ログ（補助の標準出力、logcat）に出さない。native-toolkit の `UiDriver.kt:229-231` の「見つからなければ画面の文言を全部ログに出す」は写さない
- 押す文言（テストの共有先のアプリ名、テストの Chooser Action のラベル、テストの本文）はテストが決めた値なので、依頼の行と結果に出てよい

## 4. 端末の準備と後片付け

起動のとき（補助）:

- 画面: `input keyevent KEYCODE_WAKEUP`、`wm dismiss-keyguard`。`dumpsys window` でまだロックされていれば（PIN のある端末）、理由を出して終了コード 2 で止める
- 次の設定を、値と「設定があったか」の両方を覚えてから変える（`settings get` が `null` なら「無かった」）:

| 設定 | 変える値 | 変える時点 |
|---|---|---|
| `global stay_on_while_plugged_in` | 7（AC・USB・無線の充電中は点けたまま） | 起動のとき |
| `secure autofill_service` | 消す（自動入力を切る） | 起動のとき |
| `global window_animation_scale`、`transition_animation_scale`、`animator_duration_scale` | 0 | 起動のとき |
| `system accelerometer_rotation`、`user_rotation` | `rotate` の値 | 最初の `rotate` の命令のとき（覚えるのはこの 1 回だけ。2 回目の `rotate` で元の値を上書きしない） |

終わりのとき（補助の `finally`。`SIGTERM`・`SIGINT`・`SIGHUP` でも通る）:

- 合図（`SIGTERM`・`SIGINT`・`SIGHUP`）を無視してから戻す。Unity はテストの実行の最後に adb のサーバーを止めるので、logcat が切れて先に後片付けに入り、その最中にスクリプトの合図が来る
- 覚えた値に戻す。**無かった設定は `settings delete` で消す**（`put null` は元の状態に戻らない）
- 戻し終えたら状態ファイル（`--state-file`）を消す。起動のときに状態ファイルが残っていれば、前の実行が戻しきれなかった値を先に戻す
- 戻せなかったら終了コード 1

`verify_unity_android.sh` は補助を起動したら `trap ... EXIT INT TERM HUP` で補助に `SIGTERM` を送って待つ。スクリプトが途中で落ちても補助が戻す。

## 5. テストの側（`Tests/PlayMode/AndroidUiDriver.cs`）

- `internal static class AndroidUiDriver`（外 `#if UNITY_ANDROID`、中 `#if !UNITY_EDITOR`）。`IEnumerator Do(string command, params string[] args)` を `yield return` で待ち、結果を `LastAnswer` に置く（`ok <値>` の値は `LastValue`）。`Do` の待ちは、Sharesheet の命令なら `SheetAnswerTimeoutSeconds`（150 秒）、ほかは 20 秒（v8）
- 成功とみなす答えは `ok` と `ok <値>` だけ。それ以外は `Assert.Fail`（v8。v7 の実装は `ok` で始まるものを何でも成功にしていた）
- 最初の `Do` の前に 1 回、`<run>` を作って `HELLO` を出し、probe のファイルが来たら読んで消し、`ready` を待つ
- 結果のファイルは毎フレーム見る。読んだら消す
- `background` の結果だけは、別のスレッドからも待てるように `WaitForAnswerFile(int number, TimeSpan timeout, CancellationToken cancel)`（ファイルを見るだけ。Unity の API に触らない）を用意する。依頼の行と番号は UnityMain で出す
- **依頼を先に出し、後で答えを待つ口**（v6。Share の Sharesheet のため）: `IEnumerator Send(string command, params string[] args)` は依頼の行を出すだけ（答えは待たない）。番号は `LastSent` に置く。`IEnumerator` なのは、最初の依頼の前に `ready` を待つため（v7 で設計をコードに合わせた。v6 は `int Send` と書いていた）。`IEnumerator Wait(int number, float timeoutSeconds)` はその番号の答えを待つ（`Do` と同じく、答えを `LastAnswer` に置き、`fail` なら `Assert.Fail`）。`Do` は `Send` と `Wait` をつなげたもの。補助は依頼を受けた順に 1 つずつ処理するので、`Send` した依頼は、失敗の経路でも必ず `Wait` する（テストの後片付けで回収する。回収しないと、後の依頼の答えが遅れる）
- `IEnumerator WaitForPending()` は、まだ待っていない依頼を番号の順にすべて待つ（Sharesheet の命令の待ち 150 秒）。途中の答えが `fail` でも止まらず、残りを回収してから、すべての失敗をまとめて `Assert.Fail` する（v7。v6 の実装は最初の失敗で止まり、残りが後の依頼を遅らせた）。後片付けで使う。`PendingCount` はまだ待っていない依頼の数（テストの後片付けで、`WaitForPending` の前に 0 を確かめる。後では必ず 0 になる）
- テストは、`yield return Send("tap-in-sheet", ...)` → `int n = LastSent` → 同じフレームで Share を開く → `yield return Wait(n, SheetAnswerTimeoutSeconds)` の順に書く。`Wait` は開いたのと同じフレームで始まり、Sharesheet が出ている間は Unity ごと止まり（V-11）、閉じて戻ってから進む。期限は `Wait` を始めた時点から数える
- `<files>` は `UnityPlayer.currentActivity.getFilesDir()`（`AndroidJavaObject`。テストの側だけで使う）
- Unity のテストのスレッドは UnityMain なので、待つ間も取り出し口の `Update` は回り、ネイティブの完了のイベントは届く

## 6. 補助の実装（`scripts/android_ui_driver.py`）

- 依存: Python 3 の標準ライブラリ（`subprocess`、`xml.etree`、`base64`、`signal`）と、Unity 同梱の adb（`verify_unity_android.sh` が `PATH` の先に置く）
- `adb logcat -v raw -s Unity:I -T <時刻>` を 1 行ずつ読み、`NTK-UI` で始まる行だけを扱う
- 木の読み方: `uiautomator dump /data/local/tmp/ntk-ui-dump.xml` の前に古いファイルを消し、`adb exec-out cat` で読む。`could not get idle state` などで失敗したら 1 秒おいて読み直す（期限まで。Sharesheet の中を探す途中の読み取りも同じ。v8）
- 1 つの命令は 10 秒で打ち切り、`fail timeout` を書く。打ち切ったときは最後に読んだ木を `--dump-dir` に残す（Sharesheet の木は残さない。3.4）。Sharesheet の命令の期限は 60 秒と閉じる処理の上限（3.3）
- `adb shell` には、端末で走らせるコマンドの行を 1 つの文字列で渡す（adb は引数を空白でつないで端末のシェルに渡すので、分けると引用が崩れる）
- 終了のときに、受けた `HELLO` の数、返した `READY` の数、命令の数を標準出力に出す
- 時計と待ち（`time.monotonic`、`time.sleep`）は `Driver` に渡す。`doctest` では、adb の呼び出しのたびに進む時計に替える（8 章。v7）
- 終了コード: 0 は正常。1 は設定を戻せなかった、`background` で前に戻せなかった、端末のダンプを消せなかった（v8）。2 は端末の準備ができなかった。3 は `HELLO` を受けたのに `READY` を返せなかった（probe の往復の失敗、`run-as` の失敗、5 秒を超えた）

## 7. ゲートでの判定（`verify_unity_android.sh`）

- 実機の PlayMode の実行（`run_tests` の 1 回）ごとに補助を起動し、`trap` を張り、終わったら止める
- 次のどれかなら、その実行を失敗にする:
  - 補助の終了コードが 0 でない
  - 結果の XML のカテゴリ `AndroidUiDriver` のテストに skip（Ignored）がある（補助に届かなかった UI のテストを緑にしない）
  - カテゴリ `AndroidUiDriver` のテストが 1 本も無い（カテゴリの付け忘れ）
- 32 ビットの実行は持たない（親課題の J-10。32 ビットを含むビルドは止まる）
- カテゴリは、`test-case` 自身の `Category` に加えて、親の `test-suite`（fixture）の `Category` も継いで判定する（fixture に付けたカテゴリは suite の側に出る。V-9）
- 補助の `doctest` を最初に流す（8 章）
- カテゴリの判定（skip が 0、1 本以上ある）は Android™ Profile の実行だけに当てる。Test Android™ Profile の Player にはサンプルのシーンが無く、サンプル画面を通すテストが Ignore になるため（v5）

## 8. テスト

- 補助そのものの確かめ方: Dialog の層 2b が最初の利用者で、そこで通ることをもって確かめる
- 補助の選び方の照合、依頼の行の解釈、`tap-outside` の座標の計算、設定の戻し方（値があった・無かった）は、Python の `doctest` で小さな例に当てる（`python3 -m doctest scripts/android_ui_driver.py`）。`verify_unity_android.sh` の最初に流す
- **Sharesheet の命令の決まり**（3.3、3.4）も `doctest` で固める（v7）。端末の代わりの `ScriptedDevice`（Back・押下・プロセスの停止で閉じる Sharesheet と、adb の呼び出しのたびに進む時計）の上で、依頼を 1 つずつ流し、テストに書かれる答えを照らす。確かめること: 押して閉じて前面にこのアプリが来る、見つからないときに固定の語で答えて閉じる、アンカーが無ければ通らない、Back 2 回で閉じるシートと Back で閉じないシート（プロセスを止める）、何をしても閉じないシートで閉じ方を 2 回繰り返さない、別のアプリが前面に来たら戻して `wrong-app`、adb の失敗の文言が答えに入らない、Sharesheet の木での `expect` の失敗、読み取りを端末にもホストにも残さない、`no-sheet`。v8 で足したもの: 引数の誤り（アンカーの欠け、Base64 の誤り、範囲の外の数）でも閉じて決まった語、命令の中と閉じる途中の予期しない例外、読み取りの一時の失敗は読み直して通り、続く失敗では `no-sheet` が通らない、押した後に遅れて消えるシートを押し直さない、期限の近くで別のアプリが前面に来ても Player を戻す、時間切れの読み取りを消す、消せないダンプを覚える、上限の数（60、20、15、10、5、10 秒）と合計 136 秒、adb の呼び出しがどれも 7 秒かかる端末でも合計に収まる
- `doctest` が上の決まりを本当に見ているかは、決まりを 1 つずつ壊した版で確かめた。どれも `doctest` が落ちた（v7 の 13 通り〈2026-10-11〉に、レビュー v2 が「捕まらない」とした壊し方などを足して 22 通り。2026-10-12）
- テストの側（`AndroidUiDriver.cs`）の約束（`Send` の記録、答えの読み方、`WaitForPending` の回収）には、自動のテストを置かない（PlayMode のアセンブリで、端末の上でしか動かない）。Dialog と Share の層 2b が毎回通ること、補助を変えたときは一時的なテストで実機に流すこと（9 章の V-15、V-16 のとき）で確かめる（Codex の B-2 への答え）
- テストの側の行の組み立ては、層 1 では書かない（PlayMode のアセンブリにあり、EditMode のテストから参照できない）。Dialog の層 2b が毎回通る

## 9. 要検証

| # | 内容 | 確かめる時点 |
|---|---|---|
| V-1 | テスト用の Player が debuggable であること（`dumpsys package <package>` の `flags` に `DEBUGGABLE`）。`run-as <package> --user <id> sh -c 'cat > files/...'` で補助が書いたファイルを、アプリが読めて消せること（probe の往復）。通らなければ 2 章の代わりの手段に切り替える | 実装の最初 / **確かめた（2026-10-11）**: `DEBUGGABLE`。`run-as` の中で書いて名前を変えたファイルの持ち主はアプリの uid で、アプリが読んで消せる |
| V-2 | `uiautomator dump` が、Unity の窓の上のダイアログを出すか（`android.app.AlertDialog` の部品が `resource-id` つきで出るか） | Dialog の層 2b の最初 / **確かめた**: ダイアログが操作の対象の窓のとき、`android:id/alertTitle` などが `resource-id` つきで出る |
| V-3 | ボタンの文字が大文字に変えられて木に出ないか（native-toolkit の `UiDriver.kt` は大文字と小文字を区別しない照合にしていた） | 同上。出るなら `expect` の `text` に大文字と小文字を区別しない形を足す / **確かめた**: 大文字に変わらない（AD-24 で非 ASCII のボタンの文字も一致） |
| V-4 | Unity の `Debug.Log` の行が、分かれずに 1 行で logcat に出るか（1000 バイトまで） | 実装の最初 / **確かめた**: 依頼の行は 1 行で届く |
| V-5 | Unity が毎フレーム描いている間に `uiautomator dump` が失敗し続けないか | V-2 と同時 / **確かめた**: 描画中でも `dump` は失敗し続けない（4 回目で依頼 101 件、期限切れ 0） |
| V-6 | `dumpsys input_method` の `mInputShown` で、Android 16 のキーボードの表示を判定できるか | Dialog の入力のテスト / **確かめた**: 入力の欄があるダイアログの Back（AD-16、AD-18）が通る |
| V-7 | `rotate` の後、Unity と透明な宿主の窓が作り直されないこと（`configChanges`） | Dialog の回転のテスト / **確かめた**: アプリから回しても、ダイアログは残る（AD-28） |
| V-8 | `background` で Unity が一時停止している間も、Unity Test Runner と Editor の接続（PlayerConnection）が切れず、前に戻ったあとテストが続くこと | Dialog の E-8 のテスト |
| V-9 | Unity の結果の XML で、カテゴリが `test-case` と `test-suite` の `<properties><property name="Category" .../>` に出ること | ゲートの判定を書くとき（最初の実機の実行の XML で確かめる） / **確かめた**: fixture に付けたカテゴリは `test-suite` に出る。親から継ぐ判定で 29 件を数えた |
| V-10 | テストの中で `Screen.orientation = AutoRotation` にしたあと、`rotate` で表示が回ること（既定の Android™ Profile の Player は縦に固定） | Dialog の回転のテスト / **変えた**: `AutoRotation` はセンサーに従うので回らない。アプリから `Screen.orientation` で回す形にした |
| V-11 | Sharesheet が出ている間に、アプリの Activity が `PAUSED` になり、Unity のフレームが止まるか | Share の 5.5 の手順 2 / **確かめた（2026-10-11、Pixel 6a、1.x の Manager）**: 止まる。Sharesheet が出ると `OnApplicationPause(true)` と `OnApplicationFocus(false)` が来て、フレームが進まない（Back で閉じるまでの約 3 秒で 6 フレーム）。Sharesheet は `com.android.intentresolver` の `ChooserActivityLauncher` が前面の Activity になる。依頼を先に出す形（`Send` / `Wait`）で正しい |
| V-12 | Sharesheet に、このアプリ自身が共有先として出るか。選ぶと Player が前に戻り、テストが続くか | 同上 / **確かめた**: 出る。名前は Player の名前（Android™ Profile では「Unity NativeToolkit」）。`tap-in-sheet` で押すと、選択のイベントに自分のパッケージ名（`com.UnityTestRunner.UnityTestRunner`）が届き、Player が前に戻ってテストが続く。**同じ名前の別のアプリが端末にあると、補助がそちらを押す**（1 回目の実行で、6 月に入れたサンプルアプリ `com.jonghyunkim.nativetoolkit.android` を押し、テストの Player が後ろで止まって Unity のテストの実行が 600 秒で時間切れになった。そのサンプルアプリは利用者の指示で消した） |
| V-13 | `uiautomator dump` が Sharesheet を読めるか。Chooser Action のラベルとプレビューの本文が `text` に出るか | 同上 / **確かめた**: 読める。プレビューの本文（`present-in-sheet`）、Chooser Action のラベル（`tap-in-sheet`。押すと通知が届く）とも見つかった。ダンプは補助が読み取った後に端末から消し、書き出していない |
| V-14 | 登録した Direct Share の共有先が Sharesheet に出るか | 同上 / **確かめた**: 出る（登録の直後に開いた Sharesheet で `present-in-sheet` が通った） |
| V-15 | 8 回スワイプして広げた Sharesheet が、Back で閉じるか（`absent-in-sheet` の後の閉じ方） | 補助の v7 の実装の後 / **確かめた（2026-10-12、Pixel 6a、1.x の Manager）**: 閉じる。`absent-in-sheet`（無いはずの文言を 8 回スワイプして探す）が `ok` を返すまで 36.0 秒。補助は Back だけで閉じた（プロセスを止める手順に入らなかった）。閉じた後の `count package=com.android.intentresolver` は 0 |
| V-16 | Sharesheet のプロセスを止める（`am force-stop com.android.intentresolver`）と、Sharesheet が消え、Player が前面に戻り、Unity が再開するか | 同上 / **確かめた**: 消える。止めてから 0.1 秒で前面の Activity がテストの Player に戻り、Unity は `OnApplicationPause(false)` を受けて続いた（Share の成功の結果は 1 回）。確かめは、補助とは別のホストの見張りが前面の Activity の名前だけを見て止めた（画面の中身は読んでいない） |

## 10. 拡張の見通し

機能が増えたときにこの仕組みで足りるかの見立てと、補助の中身を差し替える目安（2026-10-11、利用者と相談して記録）。

### 10.1 境目を固定する

テストと補助の境目は **3 章の依頼の形と 3.1 の命令**に置く。テスト（`AndroidUiDriver` を通すもの）は命令だけを使い、`uiautomator dump` や `input` を直接知らない。
補助の中身（今は adb のコマンド）を差し替えても、命令の意味が同じならテストは書き直さない。新しい命令を足すときも、この境目の形（`<命令>|<引数>`、結果は `ok` / `fail <理由>`）を守る。

### 10.2 向いているもの

- OS が出す標準の画面を押して結果を見るもの（ダイアログ、Share の選択画面、権限の許可、通知のシェード、設定の画面）
- 自分のアプリの外の画面（システムの UI）

### 10.3 増えると効いてくる限界

| 限界 | 問題になる時点 | 越え方 |
|---|---|---|
| 速さ: `uiautomator dump` は 1 回 1〜3 秒で、押すたびに読み直す | UI のテストが増え、1 回の実機の実行が長くなったとき | 補助の中身を、端末の上で常に動く UiAutomator（`androidx.test.uiautomator` の instrumentation）に替える。命令を受けて API で読む・押す。読むのが数十ミリ秒になる |
| 安定性: Unity が毎フレーム描くので `dump` が待たされる・失敗する（V-5） | Dialog の層 2b の最初 | 同上（UiAutomator の API は待ち方を調整できる） |
| 入力と操作の幅: `input` は ASCII の文字、タップ、スワイプ、キーまで | 日本語の入力、長押しからのドラッグ、2 本指の操作を確かめる機能が来たとき | 同上。または入力用の IME を入れる（端末の既定のキーボードが変わる副作用があり、`testing.md` 2 節の Appium の IME と同じ扱いが要る） |
| 端末の違い: システムの UI の部品の ID と文字が、メーカー・Android の版・端末の言語で変わる | Pixel 6a 以外でも流すとき | `resource-id` を優先する。端末の言語を準備で固定する。権限は押す代わりに `pm grant` で先に与える |

### 10.4 この仕組みの外になるもの

機能の設計のときに、別の手段が要るかを決める。

| もの | 要る手段 |
|---|---|
| 他のアプリに届いた中身（Share の送り先が受け取った内容など） | 受け手のテスト用アプリ |
| 位置情報・カメラ・センサーなどの入力 | 模擬の値を入れる手段（OS のテスト用の口、模擬のプロバイダ） |
| クラウドの CI での実行、並列の実行 | 端末を置く場所と、端末ごとの補助 |

### 10.5 差し替えの目安

次のどれかに当たったら、補助の中身を UiAutomator の instrumentation に替える設計を始める（テストは変えない）。

- 1 回の実機の実行で、UI のテストの所要時間が 15 分を超えた
- V-5（`dump` が描画中に失敗し続けないか）が通らなかった
- 日本語の入力か、`input` で表せない操作が要る機能が来た

