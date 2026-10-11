# レビュー結果

- 日付: 2026-10-12
- 対象ファイル:
  1. `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v5.md`（以下「計画」）
  2. `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v10.md`（以下「補助 v10」）
  3. 未コミットの差分 `scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけ。A1 と判断するときは失敗に至る経路を具体的に示し、実機で起きる見込みも書くよう頼んだ。1 章はサブエージェント、2 章は Codex の出力（どちらも手を加えていない。3 章では「S:」「X:」を前に付けて区別する）

---

## 1. サブエージェント（Claude）


- 対象: 計画 v5（`artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v5.md`）、補助 v10（`artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v10.md`）、未コミットの差分（`scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`）
- 確かめたこと:
  - `PYTHONDONTWRITEBYTECODE=1 python3 -B -m doctest scripts/android_ui_driver.py` は通った（146 件）。リポジトリに `__pycache__` はできていない。リポジトリのファイルは変えていない
  - `check_design_consistency.py` は、計画 v5 も補助 v10 も OK
  - 模擬はすべてスクラッチパッドで回した（`.../scratchpad/review5/`。`mut.py`、`mut2.py`、`mut3.py` は壊した版、`repro_dumpdir.py`、`repro_ten.py` は再現）。adb と端末は使っていない

### ラウンド 4 の指摘の反映状況

| 指摘 | 状況 | 根拠・残り |
|---|---|---|
| X:A1-1、S:C-3（`check_front` の期限） | 直った | `check_front` `:812-825`。`front_package(until)`、読めなかった回は `(Failure, TimeoutExpired)` を捕まえて読み直す。doctest `:1205-1207`。壊した版の `front_package(deadline)`、`until = deadline`、`except Failure` だけ、はどれも捕まる。「読み直す」こと自体は固まっていない（C-1） |
| X:A1-2（ほかの命令の 10 秒） | 部分的 | `wait_for` `:523-537` が `dump(deadline)` を渡すので、`wait`、`wait-gone`、`tap`、`tap-outside`、`type`、`expect`、`back` の待つ部分は直った。`count`、`rotation`、`rotate`、`background`、`back` のキーボードの段は直っていない。補助 v10 6 章（:286）の「1 つの命令は 10 秒で打ち切り」とまだ合わない（B-1） |
| S:B-1、X:B-1（doctest が固めていない所） | 部分的 | ラウンド 4 の表の壊し方は 8 通りとも捕まる（`mut2.py` R1〜R8、R10）。ただし v10 で直したところのうち 3 つは捕まらない。また「10 秒で戻る」の doctest は実際には 17 秒まで通す（C-1） |
| S:B-2（`background` の戻しで前面を確かめない） | 直った | `bring_back` `:646-650` は `bring_front()`（`:827-841`）を呼び、偽なら終了コード 1。doctest は `:1232-1246`。`do_front` に戻した版、確かめを外した版、上限を延ばした版は捕まる |
| S:C-1（最悪の時間の内訳の書き方） | 直った | 補助 v10 3.3 :231 |
| S:C-2（期限を過ぎた読み取りが削除を出す） | 直った（コード）／固まっていない | `dump` `:491` で `first` を `try` の外で取る。壊した版（`try` の中に戻す）は doctest を通る（C-1） |
| S:C-4、X の C-1（モジュールの説明） | 直った | `:23-27`、`:34-36` |
| X の C-2（`sheet_command` の説明） | 直った | `:661-664` |
| S:C-5（`did-not-close` の書き方） | 直った | 補助 v10 3.3 :228 |
| S:C-6（8 章の書き方） | 直った | 補助 v10 :310 |
| S:C-7（端末の上の `uiautomator` が残る） | 直った（要確認として） | 補助 v10 V-17 :334。v10 で Dialog の側にも広がった（C-4） |
| S:C-8（受け入れる危険） | 直った | 補助 v10 3.3 :229、計画 v5 4 章 :185 |

### 新しい指摘

#### A1

**A1-1 `wait_for` に期限を渡したことで、待ちが時間切れになったときの画面（`--dump-dir`）がほぼ残らなくなった。補助 v10 6 章 :286 の「打ち切ったときは最後に読んだ木を `--dump-dir` に残す」と食い違う（X:A1-2 の直しで入った）**

- 対象:
  - `dump` `:490`。始めに `self.last_dump = None` にしてから、`:491` で期限を確かめる
  - `wait_for` `:528`、`:534-535`。時間切れのときに `keep_last_dump()` を呼ぶ
- 失敗に至る経路: 最後の回の読み取りは、どちらの場合も `last_dump` が `None` のまま終わる。
  1. 期限が読み取りの途中に来る: adb の呼び出しが期限で切られ（`TimeoutExpired`）、`last_dump` は `None` のまま
  2. 期限が 0.5 秒の `sleep` の間に来る: 次の `dump(deadline)` が `:490` で `last_dump` を消し、`:491` で `timeout` を出す
  - どちらの場合も、`:535` の `keep_last_dump()` は何も書かない。v9 までは、最後の読み取りが（上限なしで）必ず終わったので、画面が残った
- 再現（`repro_dumpdir.py`。`dump_dir` を渡した `scripted([], sheet=False, seconds=s)` で、無い部品を `do_wait`）:
  - 今のコード: `s` = 0.5 / 0.7 / 2.2 のとき何も残らない。残ったのは、読み取りがちょうど 10.0 秒に終わる `s=1.0` だけ
  - v9 の形（`dump()`）: 4 通りすべてで `timeout-0.xml` が残る
- 実機で起きる見込み: Pixel 6a の読み取りは 1 回 2 秒あまり。期限が読み取りの途中か `sleep` の間に来るのはどちらでも同じなので、`wait` / `tap` / `expect` の時間切れのたびに、ほぼ必ず起きる。ゲートは `--dump-dir` を渡している（`scripts/verify_unity_android.sh:190`）。補助 v10 の v5 の記録（:93「外側のタップの誤りはこれで分かった」）のとおり、実際に原因の切り分けに使った仕組み
- 程度: テストの合否は変わらない。黙って通る経路も、シートが漏れる経路も無い。それでも A1 にしたのは、書かれた約束に反し、実機ではほぼ毎回起き、ラウンド 4 の直しで入ったものだから。診断だけの問題とみて B に下げる判断もありうる
- 直し方: `wait_for` の中で、読めた非シートの画面を手元に取っておく（`screen = self.last_dump`。読み取りが失敗しても上書きしない）。時間切れのときはそれを残す（`keep_last_dump(screen)` のように渡す）。シートの木は、もともと `last_dump` に入らないので、プライバシーは変わらない。doctest には、一時フォルダを `dump_dir` に渡して `do_wait` を時間切れにし、ファイルが 1 つ残ることを照らす形を足す

#### A2

なし。計画 v5 の待ち（180 秒、ほかは 20 秒）、`Send` / `Wait` / `WaitForPending` / `PendingCount` / `Base64` は、C#（`AndroidUiDriver.cs:83-179`）と補助 v10 5 章に合っている。AS-52 の「`no-sheet` は戻しの後に始まる」も、v10 の `bring_back`（確かめつき）で成り立つ。前に戻らなければ Unity は止まったままなので、`Wait(no-sheet)` は戻った後に始まる。

#### B

**B-1 「1 つの命令は 10 秒で打ち切る」（補助 v10 6 章 :286、3.3 :233）を、`count`、`rotation`、`rotate`、`background`、`back` のキーボードの段がまだ守らない**

- 対象: `do_count` `:599-601` と `do_rotation` `:603-604` は `dump()`（期限なし。1 回最大 75 秒）。`do_rotate` `:613-617`。`app_stopped` `:621` は `dumpsys`（30 秒）。`do_back` `:581-587` は `dumpsys input_method`（30 秒）。`input tap` / `input text` も 30 秒
- 再現（`repro_ten.py`）: `slow={"exec-out": 40}` で、`do_count` も `do_rotation` も 31.5 秒かかってから `timeout` になる
- 影響:
  - C# の待ち（20 秒）が先に切れて、テストは失敗する（黙って通りはしない）
  - 遅れた答えの分だけ次の依頼も遅れ、後に続くテストが連鎖して落ちうる
  - `count` は Dialog で多用している（`AndroidDialogPlayerTests.cs:146` ほか）。Share でも、後片付けの手順 4（計画 v5 3 章）で使う
  - 実機で 1 回の adb が 20 秒を超える見込みは低い。v10 の変更点（:19）は「`wait`、`tap`、`expect` など」と書くが、6 章 :286 の文言は全部の命令に読める
- 直し方: `do_count`、`do_rotation`、`do_rotate` の `dump` に、命令の 10 秒の期限を渡す。`app_stopped` と `keyboard_shown` の `dumpsys` にも同じ期限を渡す。または 6 章を「読み取りを待つ命令は」に絞り、残りを列挙する

#### C

- **C-1 v10 で直したところのうち、doctest が固めていないもの**（`mut.py`、`mut2.py`、`mut3.py`）。今のコードは正しいが、補助 v10 8 章 :310 の「10 秒の待ち…がそれぞれ 10 秒で戻る」は、実際に固めている範囲より広く読める
  - `check_front` で、読めなかった回を読み直さず、すぐ `wrong-app` にする版が通る（M1）
  - 期限を過ぎた `dump` が削除を出す版（S:C-2 に戻す）が通る（M2）
  - `watch_for_sheet` が `dump()`（期限なし）の版が通る（R9）
  - `within_ten` の基準は `COMMAND_TIMEOUT + DELETE_TIMEOUT + 2` = 17 秒。`check_front` を 15 秒、`wait_sheet` と `wait_for` を 14 秒にした版も通る（T1〜T3）。`check_front` は削除を伴わないので、10 秒ちょうどで照らせる
- **C-2 `check_front` は、命令の 60 秒が尽きたときも、adb が読めないときも `wrong-app` で答える**（`:821-824`）
  - v9 では、期限の `Failure("timeout")` がそのまま外に出て `timeout` になっていた
  - 補助 v10 3.3 :222 の「期限を過ぎたら…`timeout` で止める」と、`wait_sheet` の「`until >= deadline` なら `timeout`」の形に合わせるとよい。答えは失敗の側なので、診断の語だけの問題
- **C-3 `background` の戻しは、ふつうに成功するときも毎回 `another app is in front; bringing the player back` を標準エラーに出す**（`bring_front` `:835`。ホームの後は必ずランチャーが前面にいるため）。補助のログを読む人が、異常と取りうる。`bring_back` から呼ぶときは出さないか、文言を分ける
- **C-4 `wait_for` が adb の呼び出しを期限で切るようになり、ホストの側で `uiautomator dump` を切る回数が Dialog のテストでも増えた**（V-17 の範囲が Sharesheet の外にも広がった）。また、期限の近くで出た部品は、最後の読み取り（約 2 秒）が切られるので見つからない。待ちの実際の幅は 8 秒ほどになる。Dialog の 36 件は通っているので、記録だけでよい。V-17 の「確かめる時点」に Dialog の時間切れも足すとよい
- **C-5 `WaitForPending`（`AndroidUiDriver.cs:152-175`）は `AssertionException` だけを捕まえる**。`Wait` の中の `File.ReadAllText` / `File.Delete` が `IOException` を出すと、残りを回収しないまま外に出る。`mv` で書くので起きる見込みはごく低い

### 問題なしと確認した点

- `check_front` の読み直しの経路: どこにも休まず回る輪は無い。期限を過ぎた `front_package(until)` は `limit()` ですぐ `Failure` になり、`now >= until` で抜ける。`bring_front`、`wait_sheet_gone`、`watch_for_sheet`、`read` も同じ
- `dump` が期限を過ぎて呼ばれたとき: 何も始めず、何も消さない。端末に残っているのは、前の非シートの画面か、もう消したものだけ。シートの木は読んだ直後に消し、命令の `finally`（`:684-686`）でも消す。プライバシーは変わらない
- `bring_back`: 上限は遅らせた分の後に 25 秒（`seconds=1000` で 3＋25 秒）。戻らなければ終了コード 1。`_Deferred` なので、答えはその前に書いてある。AS-52 と AD-27 で、次の依頼はこの戻しの後に処理される。C# の待ちは Unity が戻ってから進むので、20 秒と 180 秒に収まる
- 最悪の時間: v10 の変更は Sharesheet の命令の時間を延ばさない（`check_front` の読み取りが短くなり、期限を過ぎた削除が無くなった分、短くなるだけ）。`SHEET_ANSWER_LIMIT`（151 秒）と C# の 180 秒の関係は保たれる。doctest の 1000 秒の端末の 4 命令も上限の中で諦める
- 見張りが走るとき、命令は 60 秒を使い切らない、という補助 v10 3.3 :231 の主張: `no-sheet` は最長 30＋10 秒と削除。ほかの命令は、シートを読まずに失敗すると `sheet-did-not-open` か引数の誤りで、見張りに入らないか、すぐ失敗する。成り立つ
- 計画 v5: 1 章と 2 章の数は v4 から変わっていない。4 章の受け入れる危険（:185）は補助 v10 3.3 :229 と同じ範囲。補助の版の参照はすべて v10
- C# の側: 成功は `ok` と `ok <値>` だけ（`:141`）。時間切れでも `s_pending` から外す（`:133`）。`SheetAnswerTimeoutSeconds` の内訳のコメント（`:39-46`）は 151 秒の各項と一致する

### 件数

- A1: 1（A1-1）
- A2: 0
- B: 1（B-1）
- C: 5（C-1〜C-5）

A1 は 1 件

## 2. Codex

読み取り専用で確認しました。ファイル変更・`adb` 使用はありません。doctest は 146/146 件成功しました。

#### Round 4 の X 指摘

| 指摘 | 状態 | 根拠 |
|---|---|---|
| X:A1-1 `check_front` の10秒期限 | 一部修正（未完了） | `front_package(until)` に直され、10秒以内には終了します（scripts/android_ui_driver.py:816）。ただし実際の adb timeout 後の再試行は行えません。下記 A2-1。 |
| X:A1-2 通常命令の読取期限 | 修正済み | `wait_for` が自身の10秒期限を `dump(deadline)` に渡します（scripts/android_ui_driver.py:523）。Dialog UI テストが使う `wait`、`tap`、`expect` などにも適用されます。ただし読取後の操作は別問題です。下記 A1-1。 |
| X:B-1 `check_front` の回帰テスト | 修正済み（元の取り違えについて） | 命令全体の期限を渡す旧実装は、追加 doctest で検出できます。ただし「10秒」という絶対値と新しい他の修正には穴があります。下記 B-1。 |
| X:C-1 終了コードの説明 | 修正済み | 回答書込み失敗と「上限内で諦める」がモジュール説明に反映されています（scripts/android_ui_driver.py:23、34）。 |
| X:C-2 `sheet_command` の説明 | 修正済み | 閉じられなかった場合も前面復帰を試す説明になっています（scripts/android_ui_driver.py:660）。 |

#### 新しい問題

##### A1-1: 通常命令は読取後の adb 操作で10秒契約を超える

`wait_for` の読取は修正されましたが、`tap` はその後の `input tap` を期限なしで実行します（scripts/android_ui_driver.py:523、556、561）。既定 timeout は30秒です（scripts/android_ui_driver.py:329）。

具体的な失敗経路:

1. Dialog の部品が約1.5秒で見つかる。
2. 続く `input tap` で adb が停止する。
3. 補助は30秒待ち、模擬では回答まで32秒かかった。
4. C# 側は通常命令を20秒で失敗させるため（Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs:37、93）、補助が前の命令を処理中のまま後続依頼も滞留する。

`tap-outside`、`type`、`back`、`rotate`、`background` にも同種の期限外 adb 呼出しがあります。設計の「1つの命令は10秒で打ち切る」と矛盾します（artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v10.md:286）。

正常な実機では起きにくいものの、USB切断、adb server 再起動、負荷の高いCIでは現実的です。明示された時間契約を破り、C# の待ちより長く補助を塞ぐため A1 とします。

##### A2-1: `check_front` は実際には adb timeout 後に再読取できない

`check_front` は `TimeoutExpired` を捕捉してループしますが（scripts/android_ui_driver.py:817）、`front_package(until)` は adb に10秒の残り時間すべてを渡します（scripts/android_ui_driver.py:809）。

`subprocess.run` の timeout はその時間を消費してから発生するため、捕捉時には既に `now >= until` です。したがって再試行せず `wrong-app` になります。期限内に終了する点は正しいものの、設計と fix table の「adb の時間切れを含め、読めなかった回は読み直す」は成立していません。

再試行を契約に残すなら、1回ごとの `dumpsys` に10秒より短い上限が必要です。外部結果は適切に失敗するため A1 ではなく、実装不能な記述との不一致として A2 とします。

##### B-1: 新しい doctest が修正契約を固定していない

scripts/android_ui_driver.py:1192 の `within_ten` は、期待値にも `COMMAND_TIMEOUT` を使い、さらに5秒＋2秒の余裕を持ちます。

メモリ上だけで `COMMAND_TIMEOUT = 10.0` を `15.0` に置き換えて全 doctest を実行しても、146/146 件が成功しました。したがって「10秒」は固定されておらず、設計の「壊した40通りをすべて捕捉」とも整合しません（artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v10.md:309）。

また、期限切れで読取が始まらなかった場合に削除を発行しない修正（scripts/android_ui_driver.py:491）も固定されていません。`limit()` を `try` 内へ戻し、期限切れを `forget_dump()` に流す変異でも146件すべて通りました。

#### 問題なしと確認した点

- `wait_for` は自身の10秒期限を `dump` へ渡しています。
- `dump()` は読取開始前の期限切れでは adb 削除を実行しません。読取開始後の失敗は `forget_dump()` を通ります。
- `bring_back` は検証付きの `bring_front()` を使い、復帰不能なら終了コード1にします（scripts/android_ui_driver.py:646）。
- Sharesheet の失敗時は、閉じられなかった場合も `bring_front()` 自体は必ず評価されます。
- doctest の通常実行結果は 146 passed / 0 failed です。
- Share 計画 v5 の180秒待ち、後片付け、遅延 Sharesheet の受入リスクは実装・補助設計と整合しています。

A1 は 1 件

## 3. 統合（止める基準の判定）

製品の振る舞いの指摘は無い。

| 分類 | サブエージェント（S:） | Codex（X:） |
|---|---|---|
| A1 | 1（S:A1-1） | 1（X:A1-1） |
| A2 | 0 | 1（X:A2-1） |
| B | 1（S:B-1） | 1（X:B-1） |
| C | 5（S:C-1〜S:C-5） | 0 |

同じ所を指すもの: X:A1-1 と S:B-1（ほかの命令の 10 秒）、X:B-1 と S:C-1（doctest が固めていない所）。

- S:A1-1 は、ラウンド 4 の直し（`wait_for` に期限を渡した）で入った退行。テストの合否は変わらない（時間切れの画面を残す、原因を調べるための仕組みの問題）
- X:A1-1 は、v5 からある形（読み取りの後の adb の呼び出しに期限が無い）。実機で起きる見込みは低い（Codex の見立て）

**A1 は 0 にならなかったが、止める基準の打ち切り（5 回）に達したので、レビューはここで止める。** 指摘は直し、`doctest`（壊した版 49 通り）と実機で確かめた。6 回目のレビューを回すかは、利用者が決める。

### 反映（2026-10-12）

| 指摘 | 直したところ |
|---|---|
| S:A1-1 | `wait_for` の中で、最後に読めた画面（シートの無いもの）を取っておき、時間切れのときに残す。doctest: 時間切れの `wait` が画面を 1 つ残す |
| X:A1-1、S:B-1 | Sharesheet 以外の命令は、受けた時点から 10 秒の期限（`Driver.deadline`）を持ち、その中のすべての adb の呼び出しに残りの時間を渡す（`Driver.shell`、`count`、`rotation`、`rotate`、`background`、キーボード）。doctest: `tap`、`count`、`rotation`、`background` が 10 秒で戻る |
| X:A2-1 | 前面を待つ処理は、1 回の読み取りに 3 秒（`FRONT_READ_TIMEOUT`）の上限を付けた。doctest: 一度失敗する読み取りと、一度だけ止まる読み取りの後に、読み直して通る |
| X:B-1、S:C-1 | doctest で 10 秒という値を固め、期限を過ぎた読み取りが何も出さないこと、見張りが上限で戻ることを足した。壊した版は 49 通り、すべて捕まえた |
| S:C-2 | 命令の 60 秒が尽きたときは `timeout` で答える |
| S:C-3 | `background` の戻しでは「別のアプリが前面にいる」の警告を出さない |
| S:C-4 | V-17 に Dialog の時間切れも足した |
| S:C-5 | `WaitForPending` は `Assert.Ignore` 以外のどの例外でも残りを回収する |

実機（2026-10-12、Pixel 6a）: 一時的なテスト 10 件と Dialog の UI テスト 36 件、計 46 件が通った（補助の設計 v11 の「v10 からの主な変更」）。
