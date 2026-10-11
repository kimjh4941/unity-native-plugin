# レビュー結果

- 日付: 2026-10-12
- 対象ファイル:
  1. `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v2.md`（以下「計画」）
  2. `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v7.md`（以下「補助 v7」）
  3. 未コミットの差分 `scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェント、2 章は Codex の出力（どちらも手を加えていない。ID はそれぞれのレビュアーが付けたもの。2 つの間で同じ ID が別の指摘を指すので、3 章では「S:」「X:」を前に付けて区別する）

---

## 1. サブエージェント（Claude）


- 対象: 計画 v2（`artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v2.md`）、補助 v7（`artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v7.md`）、未コミットの差分 `scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`
- 確かめたこと:
  - `PYTHONDONTWRITEBYTECODE=1 python3 -m doctest scripts/android_ui_driver.py` は通った（71 件）。`__pycache__` は作られていない
  - `check_design_consistency.py` は 2 つの文書とも OK
  - 1.12.0 の `ShareRepositoryImpl.kt`、サンプルのコントローラー、Share 設計 v6 の 4.2・5.3、Dialog の `Safely` / AD-27 と照らした
  - スクラッチパッドに置いた `ScriptedDevice` で模擬を回した（リポジトリの外。端末と adb は使っていない）
    - 補助を 9 通り壊した版で `doctest` を流した
    - 時間の上限を、adb 1 回あたり 0.25〜39.75 秒、7 命令 × 5 状態で総当たりした

### ラウンド 1 の指摘の反映状況

| ID | 状況 | 根拠・残り |
|---|---|---|
| U1-1 / CX-7 | 直った | アンカー必須（`android_ui_driver.py:782-798`）。doctest で `anchor-not-found` を固めた（:969-970）。計画は AS-43a/b・AS-44a/b に分けた（計画:110-113） |
| U1-2 | 直った | `close-sheet`（:809-814）。計画は `back` を使わない（計画:133）。Share 設計 8.3 は持ち越し K-4 のまま |
| U1-3 | 部分的 | (a) の `wrong-app` は入った（:748-761）。期限の近くでは戻せず、戻ったことも確かめない（新規 B-3） |
| U1-4 | 直った | 計画:136-137。言い回しに曖昧さが残る（C-1） |
| U1-5 / CX-5 | 直った | `AndroidUiDriver.cs:141-165`。失敗を集めてから `Assert.Fail` する |
| U1-6 / CX-2 | 部分的 | `sheet_command` の中は `except Exception` で閉じ、`bad-dump` も入った（:466-479, :640-647）。`sheet_command` に入る前の例外では閉じない（新規 A1-1） |
| U1-7 | 直った | AS-52 は 1.x と 2.0.0 で同じテストにした（計画:121） |
| U1-8 | 直った | AS-41b |
| U1-9 / CX-3 | 直った | Back を 4 回 → force-stop → `did-not-close`（:716-746）。doctest :979-992 |
| U1-10・U1-11・U1-12・U1-15 | 直った | 計画:65, 69, 91, 143 |
| U1-13 | 部分的 | `<run>` を接尾辞にする方針は入った。テストの側から `<run>` を読めない（新規 B-6） |
| U1-14 | 直った | 106 秒（補助 v7:183）、C# は 120 秒。総当たりの模擬でも最大 106.0 秒で、上限に収まった |
| U1-16 | 直った | (a) :648-650・:1097、(b) :649、(c) :566-567 |
| U1-17 | 部分的 | `no-sheet` は入った（:816-835）。読み取りに失敗すると「出ていない」とみなす（新規 B-1） |
| U1-18 | 部分的 | 押し直しの前にシートが無ければ成功とみなす部分は入った（:769-770）。`wait_sheet_gone` が 1 回も読まずに False を返す件は残った（C-7） |
| C-1〜C-7、CX-4 | 直った | 補助 v7 5 章、3.2 の表、6 章、計画:132 |
| CX-1 | 直った | `remaining()` は期限を過ぎると `timeout` を投げる（:242-260）。doctest あり |
| CX-6 | 部分的 | doctest は足した。60 秒の期限と「どの例外でも閉じる」は固まっていない（新規 B-4） |
| CX-8 | 直った | 計画:140 |

### 新しい指摘

#### A1

**A1-1 `sheet_command` に入る前の例外では Sharesheet を閉じず、決まった語でもない答えを返す（実行が止まる）**

- 対象: `android_ui_driver.py:436-446`（`command`）、:764、:785（`decode` は `sheet_command` の外）、:820-822（`int()` と `bad-span` も外）。補助 v7:178「予期しない例外のどれでも、Sharesheet が画面にあれば閉じてから `fail` を返す」、v7:193「それ以外の失敗は `error` にする」
- 問題:
  - 引数の数の誤りは、`handler(*args)` で `TypeError` になる。たとえば v1 の形の `Send("absent-in-sheet", <1 つだけ>)`。Base64 の誤りと `no-sheet` の数値の誤りも同じ
  - どれも `command()` の汎用の `except` に落ち、`sheet_command` を通らない
  - テストは `Send` → Share を開く → `Wait` の順なので、このとき Sharesheet は開いている。閉じる者がいないので Unity は止まったままになり、Unity のテストの実行の時間切れ（600 秒）まで止まる
  - 答えも `fail TypeError-Driver-do-absent-in-sheet---missing-1-...` になり、決まった語ではない
  - 模擬で確かめた: アンカーを渡さない `absent-in-sheet` は `fail TypeError-...` を返し、`sheet still open: True`。`no-sheet 500` は `fail bad-span` を返し、シートは開いたまま
  - ラウンド 1 の CX-2（`ParseError` で閉じない）と同じ種類の穴
- 直し方:
  - Sharesheet の命令は、`decode`、`int`、範囲の確かめを `work` の中に入れる
  - `command()` で、名前が Sharesheet の命令なら、ハンドラーの呼び出しそのもの（引数の数の誤りを含む）を `sheet_command` で包む
  - doctest に「引数が足りない `absent-in-sheet` でもシートは閉じ、答えは `error`」を足す

#### B

**B-1 `no-sheet` は、読み取りに失敗した回を「シートは出ていない」とみなす。読めない間に開いたシートを見逃して `ok` を返す**

- 対象: `android_ui_driver.py:824-833`（`except Failure: root = None`）。補助 v7:155「Sharesheet が出ないことを確かめる」、計画:134「退行で開いても補助が閉じ、テストは失敗で終わる」
- 問題:
  - 5 秒の窓で、1 回の読み取りに 2〜3 秒かかるので、読めるのは 2 回ほど。そのうちシートが出た後の回が失敗すると（`could not get idle state`、`bad-dump`）、見逃して `ok` を返し、シートは残る
  - 模擬で確かめた: `bad_reads=100` で `no-sheet 5000` は `ok` を返し、シートは開いたまま
  - 退行の検出を目的とした命令が、黙って通る側に倒れている
- 直し方: 窓の中に失敗した回があれば、終わりに `close_sheet()` を必ず呼ぶ。読めなければ force-stop まで進む。閉じる処理が働いた（シートが在った、または確かめられなかった）ときは `fail sheet-opened` または `fail bad-dump` にする。doctest を足す

**B-2 Sharesheet の命令の途中の読み取りの 1 回の失敗で、命令ごと `fail error` になる（読み直さない）**

- 対象: `wait_in_sheet` :683、`search_sheet` :713、`do_tap_in_sheet` :775。`wait_sheet`（:664-669）と `wait_for` は読み直す。補助 v7:236「失敗したら 1 秒おいて読み直す（期限まで）」
- 問題: `uiautomator dump failed` の Failure は捕まえずに外へ出て、`sheet_reason` で `error` になる。`present-in-sheet`、`absent-in-sheet`、`tap-in-sheet` は、一時の失敗で不安定に落ちる。模擬で確かめた（2 回目の読み取りだけ失敗させると `present-in-sheet` は `fail error`）
- 直し方: この 3 か所も、期限まで読み直す（`Failure` を捕まえ、0.5〜1 秒おいて読み直す）。`timeout` だけはそのまま外へ出す

**B-3 `check_front` は、期限の近くでは別のアプリを前に残したまま `fail timeout` を返す。戻した後に前面も確かめない**

- 対象: `android_ui_driver.py:748-761`。計画:29「このアプリを前に戻して `fail wrong-app`。テストは止まらずに失敗する」、補助 v7:151
- 問題:
  - `until = min(deadline, now+10)` なので、残りが 10 秒より短いと、ループを抜ける時点で期限を過ぎている
  - そのため `do_front(deadline)` の `limit` が必ず `timeout` を投げ、`am start` は走らない。プレイヤーは別のアプリの後ろで止まったままになる（V-12 の 600 秒の事故と同じ）
  - 模擬で確かめた: adb 1 回 5 秒、`chosen="com.other"` で `fail timeout`、前面は `com.other` のまま
  - `do_front` の後に前面がこのアプリになったかも確かめていない。doctest の `ScriptedDevice` は、`am start` で必ず戻る前提
  - 実機の速さでは、押し直しやスワイプが重なったときにだけ起きる
- 直し方: `do_front` には、Sharesheet の期限ではなく、閉じる処理と同じく別の上限（10 秒）を渡す。その後に `resumed_package` で前面を確かめ、戻らなければ `fail did-not-close` 相当の別の語にして標準エラーに出す。doctest に「期限の近くでの `wrong-app`」を足す

**B-4 doctest は 60 秒の期限と「どの例外でも閉じる」を固めていない（CX-6 の残り）**

- 対象: `android_ui_driver.py:1040-1049`（上限を `SHEET_TIMEOUT + ...` という定数の式で比べている）、:1000-1004（`failing` は `Failure` しか投げない）、補助 v7:259-260「わざと壊した 13 通りをすべて捕まえる」
- 壊した版で確かめた結果:
  - `SHEET_TIMEOUT = 600`: 捕まらない。定数ごと式が伸びるので、60 秒も C# の 120 秒との関係も固まっていない
  - `except Exception` を `except Failure` に変える: 捕まらない（CX-2 の中心）
  - 押し直しの前の「シートが消えていれば成功」を外す（U1-18）: 捕まらない
  - 時間切れの読み取りでダンプを消さない: 捕まらない（`sheet_command` の `finally` が隠す。Sharesheet の命令の外の `count` では残る）
  - 4 回目の Back の後の読み直しを外す: 捕まらない
  - `check_front` の 10 秒の待ちを外す: 捕まらない
  - 捕まった: `finally` で消さない、スワイプを 2 回にする、`no-sheet` がシートを見ない
- 直し方: `assert SHEET_TIMEOUT == 60` 相当と、`SHEET_TIMEOUT + ... + 1 <= 120`（C# の値を写す）を doctest に書く。`ValueError` を投げる `ScriptedDevice` で、閉じることと `error` を確かめる。押し直しの経路の doctest を足す。補助 v7 8 章の「13 通り」の記述は、固めた内容に合わせて直す

**B-5 イベントの数の約束（「書いていないものは 0」）が表と食い違い、文字どおりに実装すると落ちる**

- 対象: 計画:136（「書いていないものは 0」「`Op` の期待には後片付けの `RemoveDirectShareTargets` の結果を含める」）、計画:57-64、67、68、117、119、120、122
- 問題:
  - AS-01〜08、AS-11、AS-48、AS-50、AS-51、AS-52、AS-53 は `Op` の数を書いていない。規則どおりなら 0 になる（実際は 1 以上）
  - AS-12 は表が「`Op` 2」だが、計画:73・:136 では、後片付けで `native_toolkit_sample_target` を消した結果も含める。含めると 3 になる
  - AS-51 は Remove・Register・ShareText・後片付けの Remove で 3〜4 になるが、どちらとも書いていない
  - AS-50 は AS-42 と AS-47 を合わせて `Op` 2 になるはず
- 直し方: 全行に `Op/Selected/Action` の 3 つの数を書く。後片付けの Remove は「消したときだけ」か「いつも」かを決め、その分を表の数に含めるかを明記する

**B-6 AS-51 の `<run>`（補助の実行の ID）を、テストの側から読めない**

- 対象: 計画:120、`AndroidUiDriver.cs:47`（`private static string? s_run`。最初の `Send` の `EnsureReady` まで null）
- 問題: ラベルは `RegisterDirectShareTarget` の前に要るが、その時点で `<run>` を読む口が無い。AS-51 だけを流したときは、まだ null のこともある
- 直し方: `internal static string RunId`（`EnsureReady` を済ませた後に読む）を足し、計画に「最初に `Send("count", ...)` などで準備を済ませてから読む」と書く。または、テストが自分で GUID を作る、に改める

**B-7 `Do` はどの命令にも 20 秒を使う。計画の後片付けの `close-sheet` の書き方では足りない（その手順もふつうは働かない）**

- 対象: `AndroidUiDriver.cs:79-83`、計画:141「`count package=com.android.intentresolver` が 0 でなければ `close-sheet`」
- 問題:
  - `Do("close-sheet")` と書くと、Back で閉じないとき（閉じる処理は最大 25 秒）に 20 秒で `Wait` が切れる
  - 切れた依頼は `s_pending` から外れるが、補助は処理を続けている。次のテストの最初の依頼がその後ろに並んで遅れる
  - そもそも Sharesheet が出ている間は Unity が止まる（V-11）ので、後片付けが走る時点で、この確かめはふつう 0 になる
- 直し方: `Do` が Sharesheet の命令（`*-sheet`、`no-sheet`）には `SheetAnswerTimeoutSeconds` を使うようにする。または、計画のこの手順を `Send` と `Wait(n, 120)` で書く。手順の目的（念のため）も書き添える

#### C

- **C-1** 計画:137「`OnApplicationPause(false)` を受けるまで待ち」: 実際には、`Wait` が戻る前に、Unity が戻ったフレームで既に届いている。「Sharesheet を開いた後に記録した `OnApplicationPause(false)` の時刻から 2 秒」と書き、受け口（MonoBehaviour）を用意することも書く
- **C-2** 古い参照が残っている: `android_ui_driver.py:9` と `AndroidUiDriver.cs:23` が補助の設計 v6 を指している。v7 に直す
- **C-3** 日付が食い違う:
  - 計画:20 は「V-11〜V-16 は 2026-10-11」だが、補助 v7:281-282 では V-15・V-16 は 2026-10-12
  - 補助 v7:260 は壊した版の確かめを 2026-10-11 としているが、レビュー v1 の反映の節は 2026-10-12
- **C-4** 計画:71 の「Test Android™ Profile では『unity-native-plugin』。V-12」: 補助 v7 の V-12（:278）にこの名前の記録が無い
- **C-5** 補助 v7:152-153 の「出るまで 10 秒」: 実装は、Sharesheet が出るのに 10 秒（`wait_sheet`）、そのうえ文言に 10 秒（`wait_in_sheet`）で、最大 20 秒になる（:674-684）
- **C-6** 補助 v7:180 の「Back を 4 回まで（全体で 15 秒）」: Pixel 6a の読み取り 2〜3 秒では、4 回目の Back の確かめが 15 秒に入らず、force-stop に進むことがある。そうなりうると書く
- **C-7** U1-18 の残り: `wait_sheet_gone`（:686-695）は、期限を過ぎていると 1 回も読まずに False を返す。期限の間際に押せていても `fail timeout` になる（失敗の側に倒れるので C）
- **C-8** 計画:141 と :144 で「シーンを下ろす」が重複している。手順 5 の `PendingCount == 0` は、手順 3 の `WaitForPending` の後では必ず 0 になる（`Wait` は失敗でも外す）ので、確かめになっていない
- **C-9** 計画:140「AS-34、AS-51 は `ntk_as51`」: AS-34 の ID が表（:96）に無く、読み方が曖昧
- **C-10** 計画:108 の AS-41b は「`title`、`subject`、`preview_title` の写しを押さえる」としているが、確かめるのは `preview_title` だけ

### 問題なしと確認した点

- 期待の文言と 1.x の列は、1.12.0 の Kotlin と合っている
  - `EXTRA_TITLE` は内側の SEND の intent に付く（`ShareRepositoryImpl.kt:50`）
  - `requestCode = label.hashCode()` なので、同じ `intentAction` でもラベルが違えば 2 つとも出る（:258-263。AS-44a の 1.x）
  - 画像として読めないアイコンは :255 で飛ばす（AS-43、AS-46 の 1.x）
  - サンプルの ID（`native_toolkit_sample_target`、`SAVE`）と、`OnDisable` の取り消しも合っている
- 時間の上限: どの組み合わせでも 106 秒以内で、C# の 120 秒の中に収まる（ふだんの答えの書き込み〈`run-as`〉は数えていない）
- プライバシー: Sharesheet の木は `last_dump` にも端末にも残らない。`expect` は値を出さない。`warn` は例外の型の名前しか出さない
- `WaitForPending` は書いてあるとおり、失敗した答えも回収してからまとめて落ちる。`Wait` は失敗の経路でも `s_pending` から外す
- `Send` → `Wait` の形は、Dialog の `Safely` と AD-27 の形と組み合わせられる

### 件数

- A1: 1（A1-1）
- A2: 0
- B: 7（B-1〜B-7）
- C: 10（C-1〜C-10）

A1 は 1 件

## 2. Codex

読み取り専用で確認し、ファイルは変更していません。許可された doctest は成功しました（終了コード 0、出力なし）。

### CX-1〜CX-8 の状態

- CX-1: 修正済み。`remaining()` は期限到達時に timeout を投げ、期限後の adb 呼び出しを開始しません。scripts/android_ui_driver.py:242, :456
- CX-2: 修正済み。XML の ParseError は `bad-dump` に変換され、`sheet_command()` は予期しない例外も捕捉します。scripts/android_ui_driver.py:460, :633
- CX-3: 修正済み。閉じられない場合は元の理由を `did-not-close` に置き換えます。scripts/android_ui_driver.py:640
- CX-4: 修正済み。設計が `IEnumerator Send`＋`LastSent` に更新され、実装と一致しました。android-ui-driver-design-v7.md:226, AndroidUiDriver.cs:85
- CX-5: 修正済み。`WaitForPending()` は AssertionException を蓄積し、残りを回収してからまとめて失敗します。AndroidUiDriver.cs:141
- CX-6: 一部修正、未解消。`ScriptedDevice` と `__test__` は追加されましたが、期限、読取不能、削除失敗、cleanup 自身の例外を固定できていません。scripts/android_ui_driver.py:854, :948
- CX-7: 修正済み。`absent-in-sheet` にアンカーが追加され、AS-43a/b と AS-44a/b で肯定側と否定側を分けています。android-share-ui-test-plan-v2.md:107, scripts/android_ui_driver.py:782
- CX-8: 元の問題は修正済み。Direct Share 削除はイベント数照合より前に移動しました。ただし、削除イベントを期待数へ反映する記述に新しい矛盾があります（A2-2）。android-share-ui-test-plan-v2.md:138

### 新規 A1

1. A1-1: `no-sheet` は画面取得に失敗し続けても `ok` を返します。`dump()` のすべての Failure を `root = None` として握り潰し、指定時間が過ぎれば正常終了します。Sharesheet が開いていても XML を取得できなければテストが黙って通り、シートも閉じられません。scripts/android_ui_driver.py:824, android-ui-driver-design-v7.md:155
2. A1-2: 引数解析の失敗は Sharesheet 用ラッパーの外で起きます。Base64 復号、`int(milliseconds)`、引数個数の不一致は `sheet_command()` に入る前に例外になり、固定語への変換も失敗時のシート閉鎖も行われません。設計は予期しない例外も `error` にして閉じる契約です。scripts/android_ui_driver.py:763, :782, :800, :816, :430, android-ui-driver-design-v7.md:178
3. A1-3: 閉鎖処理自身の予期しない例外は固定語化されず、シートも残り得ます。`close_sheet()` は Failure と TimeoutExpired しか捕捉しません。仮説として、adb 起動時の OSError などは外側の一般エラー応答へ抜け、固定語と必ず閉じる契約の両方が破れます。scripts/android_ui_driver.py:640, :716, :314
4. A1-4: 106 秒の最悪時間に結果ファイル書込みの最大 30 秒が含まれず、120 秒待ちでは足りません。`write_result()` の既定 30 秒を足すと最大 136 秒で、C# 側は 120 秒で失敗します。doctest の `ScriptedDevice` は `run-as` の時間を数えないため、これを検出しません。android-ui-driver-design-v7.md:183, scripts/android_ui_driver.py:447, :403, :893, AndroidUiDriver.cs:39
5. A1-5: 最終的な端末ダンプ削除の失敗が黙殺されます。`forget_dump()` は全例外を握り潰すので、命令末尾とプロセス終了時の両方で失敗すると、Sharesheet の XML が端末に残ったまま終了コード 0 になり得ます。プライバシー上の契約違反です。scripts/android_ui_driver.py:486, :1089, android-ui-driver-design-v7.md:191
6. A1-6: C# の `Wait` は不正な成功応答を成功扱いします。契約は `ok` または `ok <値>` ですが、実装は `StartsWith("ok")` なので `okay` や `okfail` も成功します。AndroidUiDriver.cs:128, android-ui-driver-design-v7.md:159

### 新規 A2

1. A2-1: テスト計画の Sharesheet 文字列引数が Base64 契約に従っていません。計画は `Send("tap-in-sheet", "Save")` や `Send("present-in-sheet", "ntk share preview 41")` と生文字列で書いていますが、設計は Base64 引数と定義し、C# の `Send` は自動変換しません。手順どおりでは Python の `decode()` が失敗します。`AndroidUiDriver.Base64(...)` の明記が必要です。android-share-ui-test-plan-v2.md:53, :107, android-ui-driver-design-v7.md:151, AndroidUiDriver.cs:95
2. A2-2: Direct Share の後片付けイベントと表の `Op` 期待数が矛盾します。共通規約は後片付けの `RemoveDirectShareTargets` も期待数に含めますが、AS-12 と AS-34 は本体分の `Op` 2 だけを期待しています。AS-51 は初期削除・登録・Share・後片付け削除があるのに `Op` 数が未記載（0 扱い）です。手順どおりではイベント数照合が失敗します。android-share-ui-test-plan-v2.md:68, :96, :120, :136
3. A2-3: イベント待機期限が 5 秒と 10 秒に分かれています。後片付け手順は 5 秒、直後の待ち時間一覧は 10 秒です。android-share-ui-test-plan-v2.md:142, :146

### 新規 B

1. B-1: doctest は安全契約の正常な代表例（Back 4 回から force-stop、固定語、正常時の端末・ホスト削除）だけを押さえています。`no-sheet` の連続読取失敗、削除そのものの失敗、cleanup 中の予期しない例外、不正引数、結果ファイル書込み時間のケースがありません。scripts/android_ui_driver.py:979, :1000, :1009, :1032, :1040
2. B-2: 新しい C# 契約（`Send` の pending 登録、厳密な応答解析、複数 pending の失敗集約、各ファイルの削除）に自動検証がありません。設計は C# 側の層1テストを作らず Dialog の層2b に委ねており、回帰テストとして固定されていません。android-ui-driver-design-v7.md:226, :257, AndroidUiDriver.cs:95
3. B-3: AS-41b は `previewTitle` しか観測せず、期待欄に書いた `title`、`subject`、`preview_title` の3フィールドの写しを証明しません。`title` または `subject` 単独のマーシャリング退行は通ります。android-share-ui-test-plan-v2.md:107

### 新規 C

- Python と C# のコードコメントが正本を v6 のまま参照しています。現在は v7 です。scripts/android_ui_driver.py:8, AndroidUiDriver.cs:20
- 後片付けで「シーンを下ろす」が手順 3 と 6 に重複しています。android-share-ui-test-plan-v2.md:141, :144

A1 は 6 件

## 3. 統合（止める基準の判定）

製品の振る舞いの指摘は無い。どれも補助の約束と計画の穴。

| 分類 | サブエージェント（S:） | Codex（X:） |
|---|---|---|
| A1 | 1（S:A1-1） | 6（X:A1-1〜X:A1-6） |
| A2 | 0 | 3（X:A2-1〜X:A2-3） |
| B | 7（S:B-1〜S:B-7） | 3（X:B-1〜X:B-3） |
| C | 10（S:C-1〜S:C-10） | 2 |

同じ所を指すもの:
- S:A1-1 と X:A1-2: 引数の誤りで Sharesheet を閉じない
- S:B-1 と X:A1-1: `no-sheet` が読めない回を「出ていない」とみなす
- S:B-4 と X:B-1: doctest が固めていない決まり
- S:B-5 と X:A2-2: イベントの数
- S:C-10 と X:B-3: AS-41b が押さえるもの

**A1 が 0 でないので止めない。** 補助のコード、補助の設計 v8、UI テスト計画 v3 で直し、再レビュー（ラウンド 3）する。

### 反映（2026-10-12）

| 指摘 | 直したところ |
|---|---|
| S:A1-1、X:A1-2 | Sharesheet の命令は、引数の読み取りも含めて `sheet_command` の中で動かす（`command()`）。doctest: アンカーの欠け、Base64 の誤り、範囲の外の数 |
| X:A1-3 | `close_sheet`、`stop_sheet`、`bring_front` は例外を外に出さない。doctest: Back の途中の予期しない例外 |
| X:A1-4 | 答えの書き込みの上限を 10 秒（`RESULT_TIMEOUT`）。最悪の時間 136 秒、テストの待ち 150 秒。doctest で上限の数と合計を固め、`ScriptedDevice` は答えの書き込みの時間も数える |
| X:A1-5 | ダンプを消せなかったことを覚え、補助の終わりでも消せなければ終了コード 1 |
| X:A1-6 | テストの側は `ok` と `ok <値>` だけを成功とみなす |
| X:A1-1、S:B-1 | `no-sheet` は読めた画面だけで判定し、最後の読み取りは時間の終わりかその後 |
| S:B-2 | Sharesheet の中を探す途中の読み取りも、期限まで読み直す（`read`） |
| S:B-3 | 失敗の経路で、閉じた後にこのアプリを前面に戻し、戻ったことを確かめる（20 秒。戻らなければ `not-in-front`）。doctest: 期限の近くで別のアプリが前面に来たとき |
| S:B-4、X:B-1 | doctest を足した。決まりを壊した版を 22 通りにし、すべて捕まえた |
| X:B-2 | 自動のテストは置かない。理由を補助の設計 v8 8 章に書いた |
| S:B-5、X:A2-2 | 計画 v3 で、すべての行に `Op / Selected / Action` の数を書いた。後片付けの削除はいつも呼び、数に含める |
| S:B-6 | AS-51 の接尾辞はテストが自分で作る |
| S:B-7 | `Do` は Sharesheet の命令に 150 秒を使う。計画の後片付けの `close-sheet` は念のための手順と書いた |
| X:A2-1 | 計画 v3 に、文字列の引数は Base64 で渡すことを書いた |
| X:A2-3 | 計画 v3 で、待ち時間の使い分け（本体 10 秒、後片付け 5 秒）を書いた |
| S:C-1〜S:C-10、X の C | 計画 v3、補助の設計 v8、コードの参照（v8）で直した。S:C-7（期限を過ぎた `wait_sheet_gone` が読まない）は、失敗の側に倒れるので残す |

実機（2026-10-12、Pixel 6a、1.x の Manager）: 一時的なテスト 10 件が通った（補助の設計 v8 の「v7 からの主な変更」）。`doctest` は通り、決まりを壊した 22 通りはすべて捕まった。
