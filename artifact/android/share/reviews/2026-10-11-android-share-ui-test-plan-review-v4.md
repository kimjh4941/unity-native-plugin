# レビュー結果

- 日付: 2026-10-12
- 対象ファイル:
  1. `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v4.md`（以下「計画」）
  2. `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v9.md`（以下「補助 v9」）
  3. 未コミットの差分 `scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけ。今回は、A1 と判断するときは失敗に至る経路を具体的に示し、実機で起きる見込みも書くよう頼んだ。1 章はサブエージェント、2 章は Codex の出力（どちらも手を加えていない。3 章では「S:」「X:」を前に付けて区別する）

---

## 1. サブエージェント（Claude）


- 対象: 計画 v4（`artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v4.md`）、補助 v9（`artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v9.md`）、未コミットの差分（`scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`）
- 確かめたこと:
  - `PYTHONDONTWRITEBYTECODE=1 python3 -B -m doctest scripts/android_ui_driver.py` は通った（115 件）。リポジトリに `__pycache__` はできていない
  - `check_design_consistency.py` は、計画 v4 も補助 v9 も OK
  - 模擬はすべてスクラッチパッドで回した（`.../scratchpad/review4/`。sim1.py、sim2.py、mut.py、mut2.py、repro.py）。adb と端末は使っていない
    - 最悪の時間: 呼び出しの長さの列を山登りで探した（10 命令 × 13 状態 × 6 回の再開 × 1500 回）。最大は 146.5 秒（`tap-in-sheet`、前面が戻らない端末）。151 秒も C# の 180 秒も超えなかった
    - 決まりを壊した版: 33 通り作った。そのうち意味のある版で `doctest` が見逃したものを B-1 に書いた

### ラウンド 3 の指摘の反映状況

| 指摘 | 状況 | 根拠・残り |
|---|---|---|
| S:A1-1（シートを見ないまま失敗すると、遅れて出るシートを閉じない） | 直った（コード）／部分的（doctest） | `sheet_command` `:673-676`、`watch_for_sheet` `:688-697`。doctest は `:1150-1155`。見張りがあることは固まっている。10 秒という長さと、命令ごとに `seen_sheet` を戻すことは固まっていない（B-1） |
| X:A1-1（閉じられないとき前に戻さない） | 直った | `:680`。doctest は `:1193-1195`。壊した版（閉じられたときだけ戻す）は捕まる |
| X:A1-2（`no-sheet` の読み直しが 60 秒続く） | 直った（コード）／固まっていない | `:887`。`reads_until = deadline` に戻した版も、`+30` にした版も doctest を通る（B-1） |
| X:A1-3（`dump` のどの失敗でも端末のダンプを消す） | 直った（コード）／部分的（doctest） | `:497-503`。v8 の形（時間切れと XML の誤りのときだけ消す）に戻した版は doctest を通る。`uiautomator dump` の失敗と `exec-out cat` の失敗の経路に doctest が無い（B-1） |
| X:A1-4（答えを書けなくても終了コード 0） | 直った | `:469-474`。doctest は `:1227-1234`（`exit_code` 1）。壊した版は捕まる |
| X:A1-5、S:B-1（シーンを下ろすときの取り消し） | 直った | 計画 v4:75、:90-91、:158、:164、:167。数はサンプル（`AndroidShareManagerExampleController.cs:81-90`、`NativeToolkitSampleNavigator.cs` の `RemoveExistingControllers` → `Object.Destroy`）と 1.x の Kotlin（`UnityAndroidShareManager.kt` 1.12.0 `:309-315`。取り消しはいつも成功を通知する）に合う |
| X の A2、S:C-5（10 秒の待ちに 60 秒の期限） | 直った（コード）／固まっていない | `wait_sheet` `:719-722`、`wait_in_sheet` `:735-743`。`dump(deadline)` / `read(deadline)` に戻した版はどちらも doctest を通る（B-1）。`check_front` には同じ直しが入っていない（C-3） |
| X の B、S:B-2（doctest が上限の使い方を固めていない） | 部分的 | 答えの書き込み、削除、`bring_front` の上限、前面の確かめ、`bring_front` の例外は捕まる。上の 4 行と、`bring_back` の上限、閉じる処理と止める処理の上限（30 秒にした版）、`main` の終わりの削除は捕まらない（B-1） |
| S:C-1、S:C-2、S:C-7（上限の内訳） | 直った／内訳の文言が残る | `SHEET_ANSWER_LIMIT` `:66-67`、C# `:39-47`、補助 v9:217、モジュールの説明 `:23-26`。合計は安全な側の上限として成り立つ。内訳と「127 秒」は今のコードと合わない（C-1） |
| S:C-3（`background` の戻しに上限） | 直った（コード）／固まっていない | `:647`、計画 v4:143。固まっていない（B-1）。戻ったことを確かめない点は既存のまま（B-2） |
| S:C-4（前に戻す処理 25 秒） | 直った | `:60`。doctest は `:1238-1240` |
| S:C-6（終わりの削除） | 直った（コード）／doctest の対象外 | `:1303-1314` |
| S:C-8（命令の一覧が 2 か所） | 直った | `:68-70`、C# `:49-54` |
| S:C-9（起点が無いテスト） | 直った | 計画 v4:159、:165 |

### 新しい指摘

#### A1

なし。

具体的な失敗の経路を探したが、黙って通る経路、止まったままになる経路、シートの中身を漏らす経路は見つからなかった。

- 遅れて出るシートの見張り（10 秒）を過ぎてから出るシートは、閉じられずに残る（C-8 の再現。0.5 秒の端末で `open_at=11`）。これは補助 v9 3.3 が決めた範囲の外で、正しい引数の `sheet-did-not-open` と同じ幅。実機ではシートは 1 秒ほどで出るので、起きる見込みは低い

#### A2

なし。計画 v4 の待ち（180 秒）、`Do`、後片付けの順と数は、C# と補助 v9 に合っている。

#### B

**B-1 ラウンド 3 で直したところのうち 5 つを doctest が固めていない。補助 v9 8 章（:295）の「直したところを足して 30 通り、どれも落ちた」と合わない**

- 壊した版（`mut.py`、`mut2.py`）のうち、次のものは doctest が通ってしまう:

| 壊し方 | 戻る指摘 | 再現（`repro.py`。今のコード → 壊した版） |
|---|---|---|
| `reads_until = deadline`（`:887`） | X:A1-2 | `scripted([], sheet=False, bad_reads=999)` で `no-sheet 3000` の所要が 51.0 秒 → 98.0 秒 |
| `dump` が時間切れと XML の誤りのときだけ消す（`:497-503`。v8 の形） | X:A1-3 | `scripted(["Alice"], failing="exec-out")` の `driver.dump()` の後、`device.file is None` が True → False（シートの写しが端末に残る） |
| `wait_sheet` が `dump(deadline)`（`:722`） | X の A2 | `scripted([], sheet=False, slow={"uiautomator": 25})` で `close-sheet` が 38.5 秒 → 44.0 秒 |
| `wait_in_sheet` が `read(deadline)`（`:741`） | S:C-5 | 差が小さい（49.0 秒 → 49.5 秒）。読み取りが続けて失敗する端末で、10 秒を超えないことを直接照らす形が要る |
| `bring_back` が `do_front()`（上限なし。`:647`） | S:C-3 | `scripted([], sheet=False, seconds=1000, front="com.other")` で `bring_back(3000)` が 28.0 秒 → 33.0 秒 |
| `UNSEEN_WATCH` を 1 秒や 3 秒にする（`:690`） | S:A1-1 | doctest の `open_at=2.0` は、見張りの後の閉じる処理の読み取りで拾えてしまう。`open_at=8` なら見分けられる |
| 命令の始めに `seen_sheet` を戻さない（`:666`） | S:A1-1 | 同じ `Driver` で `close-sheet` の後に `device.open_at = device.time + 2` として、引数を欠いた `absent-in-sheet` を流す。今のコードではシートは閉じる。壊した版ではシートが開いたまま残る（doctest は命令ごとに新しい `Driver` を使うので見えない） |
| `close_sheet` / `stop_sheet` の上限を 30 秒にする | S:B-2 の残り | すべて時間切れの模擬に余裕があり（`close-sheet` は 85 秒）、151 秒を超えない |

- `main` の終わりの削除（`dump_left` のときだけ `wait-for-device` してから消す、`:1303-1314`）は doctest の対象外
- 問題: どれも今のコードは正しい。ただ、次に誰かが戻しても気づけない。8 章の書き方が、固めた範囲より広く読める
- 直し方:
  - 上の表の再現を、そのまま doctest にする（時間の上限か、`device.file` を照らす）
  - `seen_sheet` は、1 つの `Driver` で 2 つの命令を流す形で照らす
  - 8 章の「30 通り」は、壊した版の一覧と、捕まえる doctest の対応を書く。または「捕まえないもの」を書く

**B-2 `background` の戻しは、前面に戻ったことを確かめない。補助 v9 3.1（:180）「戻ったこと（RESUMED）は確かめる…戻せなかったときは補助の終了コードを 1」、6 章（:276）と食い違う（v3 からある。v9 で上限を足したところ）**

- 対象:
  - `bring_back` `:644-650`。`do_front` が例外を出さなければ成功とみなす
  - `do_front` `:895-902`。`am start` を出すだけ
- 再現: `scripted([], sheet=False, front="com.launcher", front_sticks=True)` → `driver.bring_back(3000)` → `device.front == "com.launcher"`、`driver.exit_code == 0`
- 影響:
  - AS-52（計画 v4:143）で Player が後ろに残ると、Unity が止まったままになり、Unity のテストの実行の時間切れまで進まない。補助の終了コードは 0 のまま
  - 止まった状態そのものは、確かめを足しても解けない（足して変わるのは診断と終了コード）。黙って通る側ではなく、Unity の時間切れで落ちる
  - 実機の `am start -n` で起きる見込みはごく低い（Dialog の AD-27 は通っている）。そのため B にした
- 直し方: `bring_back` は `bring_front()`（`:822-836`。上限と確かめがある）を呼び、偽なら終了コードを 1 にする。doctest は上の再現で、`exit_code == 1` を照らす

#### C

- **C-1** 補助 v9:217 の内訳が、今のコードと合わない
  - 見張り（10 秒）は「一度もシートを読まないまま失敗した」ときだけ走る。そのとき命令は 60 秒を使い切れない（`no-sheet` は最大 30＋10 秒に、削除の 5 秒）。60 秒と見張りが同時に最大になることは無い
  - 期限を越える削除は 4 回でなく 5 回ありうる（命令の最後の読み取り、見張り、Back、プロセスを止めた後の `wait_sheet_gone`、`finally`）
  - 「レビュー v3 の模擬の最大は 127 秒」は v8 のコードの値。v9 では山登りで 146.5 秒を見つけた
  - 合計の 151 秒は安全な側の上限として成り立つ。直すのは言い回しだけ
- **C-2** `dump` は、期限を過ぎて呼ばれたときも `rm -f` を出す（上限 5 秒）。補助 v9:209「期限を過ぎたら adb の呼び出しを始めず」と、v9 で足した 3.4「読み取りのどの失敗でもすぐ消す」がぶつかる
  - 再現: `read(3.0)` で読み取りが続けて失敗すると、時刻 3.0（期限ちょうど）に `rm -f`（timeout 5.0）が出る
  - 原因: `limit()` が `:491` で例外を出すと、`:497-498` の `forget_dump` に入る
  - その時点では新しいダンプは作られていないので、消す意味が無い。最初の `limit()` が失敗したときは消さない形にするか、文言を直す
- **C-3** `check_front`（`:816-817`）は 10 秒の `until` を持つのに、`front_package(deadline)` に命令の 60 秒の期限を渡す。1 回の `dumpsys` が最大 30 秒かかりうる。補助 v9 3.1（:183）の「10 秒」と合わない。X の A2 の直しを、ここにも当てる
- **C-4** モジュールの説明の終了コード（`:33-35`）に「答えを書けなかった」（v9）が無い。補助 v9:276 にはある
- **C-5** 読み取りが続けて失敗すると、答えは `timeout` ではなく `did-not-close` になる。シートが無いのに `am force-stop com.android.intentresolver` も出る
  - 再現: `scripted([], sheet=False, bad_reads=999)` で `no-sheet 3000` → `fail did-not-close`（51 秒）
  - 補助 v9 3.1 の `no-sheet` の行（:187）「`fail timeout`」と合わない
  - `wait_in_sheet` の読み取りの失敗も、`not-found` / `anchor-not-found` で答える（`:740-743`）
  - どれも失敗の側だが、診断の語が実際と違う。3.3 に「閉じられたか確かめられないときは `did-not-close`」と書く
- **C-6** 補助 v9 の 8 章（:294）は、v9 で足した doctest に「前面に戻らない端末」と書く。`bring_back`（B-2）は含まない。読み手が `background` の戻しも確かめていると取りうる
- **C-7**（要確認）ホストの側の時間切れで adb を切っても、端末の上の `uiautomator dump` が走り続けることがある。その場合、`forget_dump` の後にシートのダンプを書き、`dump_left` は偽のまま残りうる
  - 命令の `finally` の削除（`:686`）は数秒後に走るので、実際に残る見込みは低い
  - v9 で上限を短く渡す場面（見張り、10 秒の待ち）が増えた。実機で `uiautomator` が子のプロセスとして残るかを一度確かめるとよい
- **C-8** 見張りの範囲: 0.5 秒の端末で、引数の誤りから 11〜12 秒後に出るシートは開いたまま残る。2 秒の端末で、正しい引数の `close-sheet` から 15 秒後に出るシートも残る（`repro.py`）
  - 設計どおりの範囲（10 秒）だが、補助 v9 3.3 と計画 v4 4 章は、これを受け入れた危険として書いていない。「依頼から約 10 秒を過ぎて出るシートは閉じない」と書く

### 問題なしと確認した点

- 計画 v4 の 1 章の数: AS-01〜08 は 2/0/0、AS-09 は 2/0/1、AS-10 は 2/1/0、AS-11 は 2/0/0、AS-12 は 4/0/0、AS-13 は 3/0/0
  - サンプルの各ボタンが Manager を 1 回だけ呼ぶこと（`AndroidShareManagerExampleController.cs:166-505`）と合う
  - Home で `Object.Destroy` → `OnDisable` → 取り消し、を数に含めている
  - 1.x の取り消しはいつも成功を通知する（`UnityAndroidShareManager.kt` 1.12.0 `:309-321`）
  - 2.0.0 も成功を通知する（設計 v6 J-S13、:492）
  - `UnityMainThreadDispatcher` は `DontDestroyOnLoad`（`UnityMainThreadDispatcher.cs:30-32`）なので、シーンを下ろしても結果は届く
- 後片付けの順（計画 v4:160-167）:
  - 手順 4 で下ろし、手順 5 で取り消しを含めた数を待つ。手順 7 は下ろし損ねたときの予備
  - 失敗の経路でも、`Safely` の中で下ろす
  - 2.2 節のテストはシーンを読み込まないので、取り消しが無いことと矛盾しない
- `bring_front` を閉じた後に必ず呼ぶ形（`:680`）:
  - 例外を外に出さない
  - 上限は 25 秒（`seconds=1000` で、ちょうど `FRONT_TIMEOUT`）
  - 答えの語は `did-not-close` を優先し、そのほかの失敗は `not-in-front`
- 答えを書けないときは終了コード 1（`:469-474`）。`_Deferred` の戻しは、その後も走る
- C# の側:
  - `SheetAnswerTimeoutSeconds = 180`（`:47`）の内訳のコメントは、151 秒の項と一致する
  - `Do`、`WaitForPending` は 180 秒を使う
  - 成功は `ok` と `ok <値>` だけ（`:142`）
- 最悪の時間: 模擬の最大は 146.5 秒で、`SHEET_ANSWER_LIMIT`（151 秒）にも 180 秒にも収まる
- プライバシー: 見張りと前面を戻す処理は、シートの木を残さない。警告は例外の型の名前だけを出す。`finally`（`:684-686`）が `last_dump` を捨て、端末のダンプを消す

### 件数

- A1: 0
- A2: 0
- B: 2（B-1、B-2）
- C: 8（C-1〜C-8）

A1 は 0 件

## 2. Codex

読み取り専用で確認しました。ファイル変更・`adb` 使用はありません。指定の doctest は成功しました（exit 0）。

### Round 3 の X 指摘

| 指摘 | 状態 | 根拠 |
|---|---|---|
| X:A1-1 | 修正済み | `did-not-close` でも `bring_front()` は評価されます。scripts/android_ui_driver.py:677 |
| X:A1-2 | 修正済み | `no-sheet` の読取期限は観測区間終了から最大10秒です。scripts/android_ui_driver.py:883 |
| X:A1-3 | 修正済み | `dump()` の全例外経路が `forget_dump()` を通ります。scripts/android_ui_driver.py:484 |
| X:A1-4 | 修正済み | 回答書込み失敗時に終了コード1を設定します。scripts/android_ui_driver.py:469 |
| X:A1-5 | 修正済み | 1章全行が scene unload の cancel を含み、照合前に scene を下ろします。artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v4.md:75, :164 |
| X:A2 | 修正済み | sheet 表示待ち・文言待ちとも、それぞれの10秒期限を `dump/read` に渡します。scripts/android_ui_driver.py:717, :731 |
| X:B | 修正済み | 遅延 sheet、close 失敗後の前面復帰、全 adb timeout、回答書込み失敗などの doctest が追加されています。scripts/android_ui_driver.py:1147, :1191, :1224 |

### 新規指摘

#### A1-1: tap 後の前面待ちだけ、10秒期限を adb 読取へ渡していない
scripts/android_ui_driver.py:816 で10秒の `until` を作っていますが、:817 は `front_package(until)` ではなく、Sharesheet 命令全体の `deadline` を渡しています。

失敗経路は次のとおりです。
1. `tap-in-sheet` が同名の別アプリを開く。
2. `dumpsys activity activities` が一時的に20秒停止する。
3. 設計上の前面待ち10秒を超えても、最大で命令全体の残り時間まで戻らない。
4. cleanup の前面復帰も遅れ、回答理由が本来の `wrong-app` ではなく `not-in-front` へ変わり得る。

Codex の模擬では、この条件で回答まで52秒かかり、`fail not-in-front` になりました。通常の実機で `dumpsys` が20秒止まる可能性は低いです。adb が不安定な実機や CI では現実的です。設計 v9 の「このアプリが前面に来るのを10秒待つ」という契約に反します（android-ui-driver-design-v9.md:183）。

修正は `self.front_package(until)` です。

#### A1-2: 非 Sharesheet 命令の10秒契約は、現在も1回の画面読取で超過できる
設計は「1つの命令は10秒で打ち切る」としています（android-ui-driver-design-v9.md:272）。

しかし通常の `wait` / `tap` / `expect` は、scripts/android_ui_driver.py:521 で10秒期限を作りながら、:526 の `dump()` に期限を渡していません。1回の読取だけで `rm` 30秒、dump 15秒、`cat` 30秒まで進めます。

実機で通常発生する可能性は低いです。接続不良時には、C# 側が20秒で失敗した後も補助が先の命令を処理し続け、後続依頼を滞留させます。少なくとも明記された10秒契約は成立しません。

#### B-1: `check_front` の10秒期限伝播を doctest が固定していない
scripts/android_ui_driver.py:1181 付近のテストは、adb 1回5秒または前面復帰不能を見ています。「10秒を超える1回の `dumpsys`」は検査していません。そのため A1-1 の `deadline` と `until` の取り違えを検出できません。

#### C-1: 回答書込み失敗時の説明が更新されていない
実装と設計6章は終了コード1にしますが、モジュール説明の終了コード一覧には回答書込み失敗がありません（scripts/android_ui_driver.py:30）。

また「回答は `SHEET_ANSWER_LIMIT` 内に来る」と断定していますが（:23）、回答書込み自体が失敗した場合は回答が来ません。「その時間内に処理を諦め、書込み失敗は終了コード1にする」が実装どおりです。

#### C-2: `sheet_command` の docstring が旧挙動のまま
scripts/android_ui_driver.py:661 は「close が失敗した場合を除いて前面へ戻す」としています。現実装は `did-not-close` でも `bring_front()` を実行します。設計 v9 と実装が正しく、docstring だけが古い状態です。

### 指定された回帰点の確認
- `UNSEEN_WATCH` は close より前に実行される: 問題なし。
- close の成否にかかわらず `bring_front` を呼ぶ: 問題なし。
- `no-sheet` は span 終了後最大10秒だけ再読取する: 問題なし。
- `dump()` は全失敗時に端末コピーを削除する: 問題なし。
- 回答書込み失敗は終了コード1: 問題なし。
- sheet 表示・文言待ちの10秒期限伝播: 問題なし。tap 後の前面待ちだけ A1-1。
- `FRONT_TIMEOUT = 25` と各 adb 呼出しへの残り期限伝播: 問題なし。
- 終了時削除は以前の削除失敗時だけ: 問題なし。
- `151秒` と C# の `180秒`: 今回の各経路では安全側です。151秒は同時成立しない上限の和で、C# に29秒の余裕があります（AndroidUiDriver.cs:39）。
- 1章の件数: AS-01〜11は2、AS-12は4、AS-13は3で、scene unload の cancel を正しく含みます。

A2 は 0 件、B は 1 件、C は 2 件です。

A1 は 2 件

## 3. 統合（止める基準の判定）

製品の振る舞いの指摘は無い。

| 分類 | サブエージェント（S:） | Codex（X:） |
|---|---|---|
| A1 | 0 | 2（X:A1-1、X:A1-2） |
| A2 | 0 | 0 |
| B | 2（S:B-1、S:B-2） | 1（X:B-1） |
| C | 8（S:C-1〜S:C-8） | 2 |

同じ所を指すもの: X:A1-1 と S:C-3（押した後に前面を待つ処理の期限）、X:B-1 と S:B-1（doctest が固めていない所）。

**A1 が 0 でない（Codex の 2 件）ので止めない。** 2 件とも、期限を adb の呼び出しに渡し忘れていたもので、実機で起きる見込みはどちらも低い（Codex の見立て）。直して再レビュー（ラウンド 5）する。止める基準の打ち切り（5 回を超えたら止める）では、ラウンド 5 が最後になる。

### 反映（2026-10-12）

| 指摘 | 直したところ |
|---|---|
| X:A1-1、S:C-3 | `check_front` の読み取りに、その 10 秒を上限として渡す。読めなかった回（adb の時間切れを含む）は読み直し、10 秒が過ぎたら `wrong-app` |
| X:A1-2 | ほかの命令の `wait_for` の読み取りに、その命令の 10 秒を上限として渡す |
| S:B-1、X:B-1 | doctest を足した: 読み取りのどの失敗でも端末のダンプを消す、10 秒の待ち（シート、文言、前面、ほかの命令）が 10 秒で戻る（`wait_sheet`、`wait_in_sheet`、`check_front`、`do_wait` を直接呼ぶ）、`no-sheet` の読み直しの上限、見張りの長さ（`open_at=8`）と命令ごとの戻し（1 つの `Driver` で 2 つの命令）、`background` の戻しの上限と確かめ。壊した版は 40 通り、すべて捕まえた |
| S:B-2 | `background` の戻しは、Sharesheet の命令と同じ戻し方（確かめつき、上限 25 秒）を使い、戻らなければ終了コード 1 |
| S:C-1 | 補助の設計 v10 3.3 の最悪の時間の書き方を直した（各項は上限、同時に最大にならない項、期限を越える削除は 5 回ありうる、レビュー v4 の山登りの最大 146.5 秒） |
| S:C-2 | 期限を過ぎて呼ばれた読み取りは、削除も出さない |
| S:C-4、X の C-1 | モジュールの説明の終了コードと「上限の中で諦める」の書き方を直した |
| X の C-2 | `sheet_command` の説明を、閉じられなかったときも前に戻す形に直した |
| S:C-5 | 補助の設計 v10 3.3 に「閉じたか確かめられないときも `did-not-close`」と書いた |
| S:C-6 | 補助の設計 v10 8 章の書き方を直し、`background` の戻しの doctest も足した |
| S:C-7 | 要確認として補助の設計 v10 の V-17 にした |
| S:C-8 | 受け入れる危険として、補助の設計 v10 3.3 と計画 v5 4 章に書いた |

実機（2026-10-12、Pixel 6a）: 一時的なテスト 10 件と Dialog の UI テスト 36 件、計 46 件が通った（補助の設計 v10 の「v9 からの主な変更」）。
