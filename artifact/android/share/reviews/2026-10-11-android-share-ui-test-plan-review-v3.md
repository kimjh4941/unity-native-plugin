# レビュー結果

- 日付: 2026-10-12
- 対象ファイル:
  1. `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v3.md`（以下「計画」）
  2. `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v8.md`（以下「補助 v8」）
  3. 未コミットの差分 `scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェント、2 章は Codex の出力（どちらも手を加えていない。3 章では「S:」「X:」を前に付けて区別する）

---

## 1. サブエージェント（Claude）


- 対象: 計画 v3（`artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v3.md`）、補助 v8（`artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v8.md`）、未コミットの差分 `scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`
- 確かめたこと:
  - `PYTHONDONTWRITEBYTECODE=1 python3 -B -m doctest scripts/android_ui_driver.py` は通った（99 件）。リポジトリに `__pycache__` はできていない
  - `check_design_consistency.py` は、計画 v3 も補助 v8 も OK
  - 模擬はすべてスクラッチパッドで回した（`.../scratchpad/review3/`。h1〜h4.py、mut.py）。端末と adb は使っていない
    - 最悪の時間の総当たり: 9 命令 × 9 状態 × 1 回の adb 0.25〜49.75 秒と 100・1000 秒、命令の前置きごとに遅くする組み合わせ、呼び出しの長さの列をランダムに 6 万通り。最大は 115.75 秒と 127.0 秒で、136 秒は超えなかった
    - 決まりを 1 つずつ壊した版を 13 通り作り、`doctest` が落ちるかを見た

### ラウンド 2 の指摘の反映状況

| 指摘 | 状況 | 根拠・残り |
|---|---|---|
| S:A1-1、X:A1-2（引数の誤りで閉じない） | 部分的 | 引数の読み取りは `sheet_command` の中に入った（`android_ui_driver.py:448-451`）。ただし、閉じるのは最初の読み取りの時点でシートが既に出ているときだけ（新規 A1-1） |
| X:A1-1、S:B-1（`no-sheet` が読めない回を「出ていない」とみなす） | 直った | `:844-858`。読めた画面だけで判定する。読めないまま期限が来ると `timeout` になり、閉じる手順に入る。doctest は `:1109-1112` |
| X:A1-3（閉じる処理の例外） | 直った（コード）／部分的（doctest） | `close_sheet` `:760`、`stop_sheet` `:772`、`bring_front` `:804` は例外を外に出さない。doctest は Back の途中の例外だけを押さえ、`bring_front` の例外は押さえていない（B-2 の M15） |
| X:A1-4（答えの書き込みが上限に入っていない） | 直った（上限）／部分的（doctest） | `RESULT_TIMEOUT` を `:418` で使う。上限の 136 秒は守られる（下の「最悪の時間」）。doctest が固めているのは定数とその和だけで、コードがその定数を使っていることは固めていない（B-2 の M6・M7・M10） |
| X:A1-5（ダンプを消せなくても終了コード 0） | 直った | `forget_dump` `:500-508`、`main` `:1211-1215`。終了コードの経路は doctest の対象外（C-6） |
| X:A1-6（`ok` で始まれば成功にしていた） | 直った | `AndroidUiDriver.cs:139` |
| S:B-2（途中の読み取りの失敗を読み直さない） | 直った | `read` `:672-680` を `:713`、`:743`、`:817`、`:754`、`:758` で使う |
| S:B-3（期限の近くで別のアプリを前に残す） | 直った | `bring_front` `:792-805`。上限は自分で持ち、前面に来たことも確かめる。doctest は `:1124-1126`。確かめの部分は doctest が固めていない（B-2 の M2） |
| S:B-4、X:B-1（doctest が決まりを固めていない） | 部分的 | B-2 を見よ。壊した版のうち 5 通りを見逃す |
| X:B-2（C# の側に自動のテストが無い） | 判断として反映 | 補助 v8:278 |
| S:B-5、X:A2-2（イベントの数） | 直った | すべての行に `Op/Selected/Action` がある（計画 v3:72-84、98-113、122-137）。数も合っている。シーンを下ろすときの取り消しが抜けている（新規 B-1） |
| S:B-6（AS-51 の `<run>` を読めない） | 直った | 計画 v3:135 |
| S:B-7（`Do` が 20 秒しか待たない） | 直った | `AndroidUiDriver.cs:90`、計画 v3:157 |
| X:A2-1（Base64 の約束） | 直った | 計画 v3:66 |
| X:A2-3（5 秒と 10 秒の食い違い） | 直った | 計画 v3:158、:162 |
| S:C-1〜S:C-10、X の C | 直った | 計画 v3:20、:23、:111、:123、:152、:155-160。補助 v8:168、:195。参照先の版はコードの `:9` と C# の `:23`。S:C-7 は判断として残した |

### 新しい指摘

#### A1

**A1-1 引数の誤りを読み取りの直後に見つけるので、Sharesheet がまだ出ていないことがある。閉じる処理はシートが無いのを見て成功とみなし、その後に出たシートは誰も閉じない**

- 対象:
  - `android_ui_driver.py:448-451`。引数の読み取りが命令の最初、`wait_sheet` より前にある
  - `close_sheet` `:753-755`。最初の読み取りでシートが無ければ、すぐ `True` を返す
  - doctest `:1081-1092`。`ScriptedDevice` は最初からシートが出ている（`sheet=True`）
- 契約: 補助 v8:18、:191「引数の誤りでも Sharesheet を閉じてから答える」、:193
- 問題:
  - テストは `Send` と同じフレームで Share を開く。補助は logcat の行を受けてすぐ、Base64 や数の誤りで例外にする。このときシートはまだ立ち上がっていないことがある
  - `close_sheet` の 1 回目の読み取り（`rm -f` → `uiautomator dump`）がシートより先に終わると「シートは無い」とみなす。`bring_front` も前面が Player なので `True`。答えは `fail error` / `fail bad-span`
  - その後にシートが出ると Unity は止まったままで、閉じる者がいない。ラウンド 2 の S:A1-1 と同じく、Unity のテストの実行の時間切れ（600 秒）まで止まる
  - 補助 v8:26 の実機の確かめは「Sharesheet を開いた後に届く」形で、この競り合いを通っていない
- 模擬での再現（h2.py）: `ScriptedDevice` を継いで、シートが開始から `open_at` 秒後に出るようにした（adb 1 回 0.5 秒）
  - `open_at=1.2` と `2.0` では、引数を欠いた `absent-in-sheet`、Base64 の誤りの `tap-in-sheet`、引数の多すぎる `present-in-sheet` のどれも `fail error` を約 3 秒で返し、**シートは開いたまま**
  - `open_at=0.6` では閉じた
  - 正しい引数の `present-in-sheet` は、`wait_sheet` があるので `open_at=2.0` でも閉じた
- 実機で起きるか: 実機の速さ（Pixel 6a で読み取りに 2 秒あまり）で起きる見込みは高くない。それでも、v8 で直したと書いた契約の穴そのもの
- 直し方（どれか）:
  - (a) `tap-in-sheet`、`absent-in-sheet`、`present-in-sheet`、`close-sheet` は、引数を読む前に `wait_sheet(deadline)` を済ませる。引数の誤りでも、シートが出るのを待ってから閉じる
  - (b) `sheet_command` に「この命令でシートを見たか」を持たせる。見ていないまま失敗したら、閉じる前に一定の時間（例: 10 秒、`no-sheet` と同じ見張り）シートが出ないことを確かめる
  - どちらでも、遅れて出るシートの `ScriptedDevice`（h2.py の `TimedSheet` と同じもの）で doctest を足す

#### A2

なし。

#### B

**B-1 後片付けでシーンを下ろすと、サンプルの `OnDisable` が `CancelPendingShareCallback` を呼ぶ。その `Op` は数えられず、次のテストの数に紛れ込みうる**

- 対象:
  - 計画 v3:160（手順 7「購読を外し、取り出し口の列を空にし、シーンを下ろす」）、:151、:158
  - `AndroidShareManagerExampleController.cs:81-89`（`OnDisable` → `CancelPendingShareCallback`）
  - `AndroidShareManager.cs:415`（1.x は `UnityMainThreadDispatcher.Enqueue` で次のフレームに配る）
  - `NativeToolkitSampleNavigator.cs:211-246`（コントローラーは Share の画面を開いたときだけ付く）
- 問題:
  - AS-13 は、Home で `OnDisable` が呼ばれて `Op` が 1 増えることを数えている（計画 v3:84）。しかし 1 章のどのテスト（AS-01〜AS-13）も、手順 7 でシーンを下ろすと同じ取り消しが 1 回走る
  - 手順 7 は購読を外し、列を空にしてからシーンを下ろす。そのため、取り消しの結果は列を空にした後に積まれる
    - 1.x では、`OnDisable` は下ろし終わるフレームで走り、結果はその次のフレームの `Update` で配られる
    - そのころ次のテストの `SetUp` が購読を済ませていると、次のテストの `Op` が 1 多くなる（例: AS-02 が 2 ≠ 1 で落ちる）
    - 2.0.0 でも、完了がネイティブから届くのが次の `SetUp` の `ResetForTests` より後なら同じ
  - 黙って通る側ではないが、1 章を続けて流すと不安定に落ちうる。計画の文言どおりに作ると、この競り合いがそのまま入る
- 直し方: 1 章のテストの後片付けは、手順 5 の前にシーンを下ろす。取り消しの `Op` 1 を表の数に含める（「後片付けの取り消しを含む」と明記）。または、シーンを下ろした後にその `Op` が届くのを待ってから購読を外す。2.0.0 では、その後に `PendingCallCountForTests` が 0 に戻るのを待つ。計画 3 章の「`Op` の数に含めるもの」（:151）にも書く

**B-2 doctest は上限を「定数とその和」でしか固めておらず、コードがその定数を使っていることを固めていない。`no-sheet` の見張りの時間と、`bring_front` の確かめも固めていない（S:B-4、X:B-1 の残り）**

- 対象: `android_ui_driver.py:1149-1163`、補助 v8:23、:199、:277（「決まりを壊した版を 22 通り…どれも落ちた」）
- 壊した版で確かめた結果（mut.py）。次の 6 通りは `doctest` が通ってしまう:
  - M6: `write_result` が `RESULT_TIMEOUT` ではなく 30 秒を渡す → 最悪の時間は 156 秒になり、C# の 150 秒を超える
  - M7: `forget_dump` が `DELETE_TIMEOUT` ではなく 30 秒を渡す → 4 回分で最大 120 秒延びる
  - M10: `bring_front` の上限を 60 秒にする
  - M11: `no-sheet` が最初の読み取りで `ok` を返す（指定の時間を見張らない）→ 後から開くシートを見逃す。退行の検出の中心
  - M2: `bring_front` が `am start` の後に前面を確かめない（`ScriptedDevice` の `am start` は必ず戻る）
  - M15: `bring_front` が例外を外に出す → 答えが `fail ValueError-Alice` のように、決まった語でなく adb の文言を含む
- 捕まったもの: `bring_front` が常に真、閉じる処理の上限を 60 秒、`stop_sheet` の待ちを伸ばす、`finally` で消さない、`read` が期限を見ない、`wait_in_sheet` が 10 秒を見ない
- 問題:
  - X:A1-4 の反映（「doctest で上限の数と合計を固め」）は、定数の検査にとどまる
  - 1 回 7 秒の模擬（`:1156-1163`）は、どの上限にも当たらないので、使い方の誤りを捕まえない
  - 補助 v8:277 の「22 通りすべて捕まえた」は、この範囲の話だと読めない
- 直し方:
  - (1) どの呼び出しも時間切れになる端末（`seconds=1000`）で、`close-sheet`（閉じない・止まらない）、`tap-in-sheet`（別のアプリ）、`no-sheet` の 3 つを流し、`device.time <= 136` を確かめる。M6・M7・M10 はこれで捕まる
  - (2) シートが一定の時間の後に出る `ScriptedDevice` で `no-sheet 3000` が `fail sheet-opened` になる（M11）
  - (3) `am start` で戻らない端末で `fail not-in-front` になる（M2）
  - (4) `broken="dumpsys"` で `bring_front` の例外を押さえる（M15）
  - 補助 v8 8 章の記述を、固めた範囲に合わせる

#### C

- **C-1** `AndroidUiDriver.cs:41-43` の内訳は 60＋25＋20＋20＋10＝135 秒で、136 秒と書いた合計と合わない（2 つの 0.5 秒の待ちに当たる「＋1 秒」が抜けている）。補助 v8:199 と合わせる
- **C-2** 最悪の時間の実際の値:
  - 補助 v8:199 の 136 秒は、安全な側の上限として成り立つ
  - 内訳の書き方は実際と違う。前に戻す処理（20 秒）は閉じられたときだけ走る。閉じられたときの閉じる処理と止める処理は合わせて 30 秒以内（止める処理が成功するのは、シートの無い読み取りが期限の中で済んだときだけで、削除の上乗せが無い）。命令の中の上乗せは、`wait_sheet_gone` の削除 5 秒と 0.5 秒の待ちで 5.5 秒になりうる
  - 手で追った最大は約 131 秒。模擬では最大 127 秒
  - コードのコメント `:1146-1147` の「時間切れの読み取りの後の削除 4 回」も不正確。削除はシートの読み取りと読めなかった回のたびにも起きる
  - どれも 150 秒の中に収まるので、言い回しの直し
- **C-3** 136 秒は、補助が依頼を受けてからの時間。テストの `Wait` を始めてからの時間ではない。AS-52（計画 v3:136）では、`no-sheet` は `background` の後の戻し（`bring_back`: 3 秒＋上限の無い `do_front`、各 30 秒）が済んでから始まる。そのため `Wait(no-sheet, 150)` は、最悪 3＋60＋136 秒に届きうる。ふつうは 15 秒ほど。計画か補助 v8 3.3 に「前の依頼の残りは数えない」と書く。または、`bring_back` の `do_front` に上限を渡す
- **C-4** `bring_front` の 20 秒は「adb の呼び出し 4 回分、1 回 5 秒でも収まる」（補助 v8:198）としている。しかし 4 回目の確かめは残り 0 秒ちょうどに当たるので、実際には時間切れになり、前面にいても `not-in-front` を返しうる（模擬は「時間＝上限」を通すので見えない）。上限を 25 秒にするか、文言を直す
- **C-5** `wait_in_sheet` の読み直し（`:713`）は 60 秒の期限まで続く。読み取りの失敗が続くと、10 秒の `not-found` / `anchor-not-found` ではなく、60 秒の `timeout` で答える。補助 v8:167-168 の「10 秒」と合わない（失敗の側に倒れるので C）
- **C-6** 終わりの `forget_dump`（`:1211`）は、Unity が adb のサーバーを止めた直後の最初の adb の呼び出しで、上限は 5 秒、`check=True`。サーバーの起動直後の「no devices」や起動の遅れで失敗すると、シートを読んだことが無い実行でも終了コード 1 になり、ゲートが落ちうる（要確認。v8 の実機では 0 だった）。最後の削除だけ、長い上限か `wait-for-device` の後にする。または「消せていないシートの読み取りがあるときだけ 1」にする。この経路は doctest の対象外
- **C-7** モジュールの説明（`:23-24`）の「within a 60-second limit」は、閉じる処理と前に戻す処理を含めた上限（136 秒）と食い違って読める
- **C-8** Sharesheet の命令の一覧が、Python（`SHEET_COMMANDS` `:63`）と C#（`s_sheetCommands` `AndroidUiDriver.cs:48-51`）の 2 か所にある。片方だけ足すと、`Do` が 20 秒で切る。互いを指すコメントを置く
- **C-9** 計画 v3:158 の「`OnApplicationPause(false)` の時刻から 0.5 秒」は、シートが開かないテスト（AS-11、AS-12、2.0.0 の AS-46、AS-21〜AS-36）では起点が無い。AS-52 の `OnApplicationPause(false)` は `background` から戻ったときのもの。起点が無いときの扱いを書く

### 問題なしと確認した点

- 新しい経路で決まった語が守られる: 引数の誤り、`bad-span`、読み取りの時間切れ、`not-in-front`。どれも `SHEET_REASONS`（`:65-69`）と補助 v8 3.4 の一覧と同じ
- プライバシー:
  - 前面の確かめで別のアプリの画面を読んだときも、`last_dump` は `finally` で捨てる
  - 端末のダンプは命令の終わりに消し、消せなければ覚える
  - 警告は例外の型の名前しか出さない
- `no-sheet` は、読めた画面だけで判定する。読めないまま期限が来たときは `timeout` で閉じる。黙って通る経路は無い
- C# の側:
  - `ok` と `ok <値>` だけを成功とみなす
  - `Wait` は、時間切れでも `s_pending` から外す
  - `WaitForPending` は、`AssertionException` を集めてから最後に失敗する
  - `Do` は、Sharesheet の命令に 150 秒を使う
- 計画 v3 の各行の `Op / Selected / Action`（AS-12 は 3、AS-34 は 3、AS-35 は 2、AS-51 は 4、AS-13 は 2、AS-50 は 2/1/1、AS-49 は 2/0/0）は、サンプルと手順に合っている（B-1 の、シーンを下ろすときの取り消しを除く）

### 件数

- A1: 1（A1-1）
- A2: 0
- B: 2（B-1、B-2）
- C: 9（C-1〜C-9）

A1 は 1 件

## 2. Codex

読み取り専用で確認しました。ファイル変更および adb 使用はありません。指定の doctest は成功しました（exit 0）。独立レビューも併用しています。

### Round 2 の X: 指摘

| ID | 状態 | 根拠 |
|---|---|---|
| X:A1-1 | 一部修正 | 読取失敗が続けば `timeout` になります。ただし、観測期間後に正常読取が1回成功するだけで `no-sheet` が ok になる穴が残っています。 |
| X:A1-2 | 修正済み | 引数個数、Base64、数値・範囲の解析が `sheet_command` 内になりました（android_ui_driver.py:439）。 |
| X:A1-3 | 修正済み | close/stop/front の例外は外へ出ません。ただし `did-not-close` 時に前面復帰を飛ばす新規問題があります（新規 A1-1）。 |
| X:A1-4 | 一部修正 | `RESULT_TIMEOUT=10` と C# の 150 秒待ちは追加されました。しかし結果書込み自体が時間切れになると回答が存在しません（新規 A1-4）。 |
| X:A1-5 | 修正済み | 最終削除の失敗を `dump_left` に保持し、終了コード1にします（android_ui_driver.py:500）。 |
| X:A1-6 | 修正済み | 成功判定は `ok` または `ok ` で始まる値付き応答に限定されました（AndroidUiDriver.cs:137）。 |
| X:A2-1 | 修正済み | 計画に `AndroidUiDriver.Base64(...)` が明記されました（test-plan-v3.md:64）。 |
| X:A2-2 | 修正済み | AS-12=3、AS-34=3、AS-35=2、AS-51=4 として Direct Share cleanup が反映されています。ただし scene unload の cancel が別途漏れています（新規 A1-5）。 |
| X:A2-3 | 修正済み | 本体10秒、cleanup 5秒の使い分けが明記されました（test-plan-v3.md:158）。 |
| X:B-1 | 修正済み | 元の不足ケースは doctest に追加されました。ただし新しい時間契約は十分に固定できていません（B 参照）。 |
| X:B-2 | 未修正・受容のみ | C# 側契約の自動テストは依然なく、設計が明示的に見送っています（driver-design-v8.md:278）。 |
| X:B-3 | 修正済み | AS-41b の保証範囲を `preview_title` のみに限定しました（test-plan-v3.md:123）。 |

### 新規指摘

#### A1

1. `did-not-close` で前面復帰を実行しません（android_ui_driver.py:649、driver-design-v8.md:193）。
   - `close_sheet()` が失敗すると `word = "did-not-close"` になります。続く `word != "did-not-close"` の条件が `bring_front()` を飛ばします。
   - Sharesheet または別アプリの後ろで Player が停止したままになり得ます。
   - 設計の「どの失敗でも前面へ戻す」とも矛盾します。

2. `no-sheet` が観測不能期間を黙って通せます（android_ui_driver.py:672、android_ui_driver.py:844）。
   - 5秒の観測期間中に一度も正常読取できなくても、`read()` は60秒の命令期限まで再試行します。
   - その後、既に閉じた通常画面を1回読めば `now >= until` となり ok になります。
   - 観測不能期間に一時的に開いた Sharesheet を見逃します。

3. 一般的な読取失敗では、端末上の dump を直ちに削除しません（android_ui_driver.py:474、driver-design-v8.md:203）。
   - `dump()` が削除するのは `TimeoutExpired` と XML parse 失敗だけです。
   - `uiautomator dump` の非成功応答や `exec-out cat` の `Failure` は削除せずに抜けます。`read()` は1秒後に再試行します。
   - 部分的な Sharesheet XML が、その間または非 Sharesheet 命令の終了後まで残り得ます。
   - 設計 3.4 のプライバシー契約を破ります。

4. `RESULT_TIMEOUT` 発生時は「136秒以内に回答する」が成立しません（android_ui_driver.py:415、android_ui_driver.py:461、AndroidUiDriver.cs:39）。
   - 結果書込みの10秒を「回答までの時間」に加えています。しかしその adb 呼出し自体が時間切れになると、結果ファイルは作られません。
   - 例外はログに出るだけで握られ、補助の終了コードも0のままです。C# は150秒後に失敗するだけです。
   - `ScriptedDevice` で全 adb 呼出しを結果書込み上限より遅くすると、回答が `None` になることも確認しました。

5. scene unload が、検査後に新しい native call を作ります（test-plan-v3.md:151、AndroidShareManagerExampleController.cs:81）。
   - cleanup は、イベント数照合、`PendingCallCountForTests == 0`、購読解除、queue 消去の後に scene を下ろします。
   - しかし `OnDisable()` がそこで `CancelPendingShareCallback()` を発行します。この最後の `Op` は、AS-01〜13 すべての期待数から漏れています。
   - 検査後に pending call や次テスト向けのイベントを生成します。
   - AS-13 では Home 時の cancel に加えて、最終 unload でも再度 cancel します。

#### A2

- Sharesheet 表示待ち・文言待ちの「10秒」が実装されていません（android_ui_driver.py:690、android_ui_driver.py:704、driver-design-v8.md:166）。
  - 各ループの期限は10秒です。しかし内部の `dump(deadline)` / `read(deadline)` には、Sharesheet 命令全体の60秒期限を渡しています。
  - 失敗・停止した1回の読取だけで10秒を超え、最大60秒まで戻りません。

#### B

- doctest の136秒検査は、定数の足し算を再掲しているだけです（android_ui_driver.py:1144）。
  - `RESULT_TIMEOUT` 時に結果ファイルが作られること、`did-not-close` 後の前面復帰を、どちらも検査していません。
  - 「7秒かかる端末」は結果書込みの10秒以内です。そのため「すべての adb 呼出しが時間切れ」の検査にもなっていません。

総合評価は要修正（重大）です。

A1 は 5 件

## 3. 統合（止める基準の判定）

製品の振る舞いの指摘は無い。どれも補助の約束と計画の穴。

| 分類 | サブエージェント（S:） | Codex（X:） |
|---|---|---|
| A1 | 1（S:A1-1） | 5（X:A1-1〜X:A1-5） |
| A2 | 0 | 1 |
| B | 2（S:B-1、S:B-2） | 1 |
| C | 9（S:C-1〜S:C-9） | 0 |

同じ所を指すもの: X:A1-5 と S:B-1（シーンを下ろすときの取り消し）、X の A2 と S:C-5（10 秒の待ちに 60 秒の期限）、X の B と S:B-2（doctest が上限の使い方を固めていない）。

**A1 が 0 でないので止めない。** 補助のコード、補助の設計 v9、UI テスト計画 v4 で直し、再レビュー（ラウンド 4）する。

### 反映（2026-10-12）

| 指摘 | 直したところ |
|---|---|
| S:A1-1 | 命令の中で一度もシートを読まないまま失敗したら、閉じる前に 10 秒（`UNSEEN_WATCH`）シートを見張る。doctest: 遅れて出るシートと引数の誤り（`absent-in-sheet`、`no-sheet`） |
| X:A1-1 | 閉じられなかったときも前面に戻す。doctest: 閉じないシートの後ろに別のアプリ |
| X:A1-2 | `no-sheet` の読み直しは、時間の終わりから 10 秒まで。doctest: 遅れて出るシートを `no-sheet` が見つける |
| X:A1-3 | `dump` のどの失敗でも端末のダンプを消す |
| X:A1-4 | 答えを書けなかったら終了コード 1。doctest: どの呼び出しも時間切れの端末で 4 つの命令が上限の中で諦め、終了コードが 1 |
| X:A1-5、S:B-1 | 計画 v4: 1 章は数を照らす前にシーンを下ろし、取り消しの `Op` 1 を全行に含めた |
| X の A2、S:C-5 | シートを待つ間と文言を待つ間の読み取りは、その 10 秒を上限にする |
| X の B、S:B-2 | doctest を足した（上の各行、前面に戻らない端末、前面を読む途中の例外、答えの書き込みと前に戻す処理がそれぞれの上限で諦める）。決まりを壊した版は 30 通り、すべて捕まえた（どれも 90 秒で終わらない版は「捕まえた」と数えた。1 通り: 期限を過ぎても時間を渡す版で、読み直しが止まらない） |
| S:C-1、S:C-2、S:C-7 | 上限の合計を `SHEET_ANSWER_LIMIT`（151 秒）として、コード、C# のコメント、補助の設計 v9 3.3 で同じ内訳にした。テストの待ちは 180 秒 |
| S:C-3 | `background` の戻しに上限を渡した。計画 v4 の AS-52 に、待ちが前の依頼の後に始まることを書いた |
| S:C-4 | 前に戻す処理の上限を 25 秒 |
| S:C-6 | 補助の終わりの削除で終了コードを 1 にするのは、前に消し損ねたときだけ。そのときは adb が戻るのを待つ |
| S:C-8 | Python と C# の命令の一覧に、互いを指すコメントを置いた |
| S:C-9 | 計画 v4 に、起点が無いテストの扱いを書いた |

実機（2026-10-12、Pixel 6a、1.x の Manager）: 一時的なテスト 10 件が通った（補助の設計 v9 の「v8 からの主な変更」）。
