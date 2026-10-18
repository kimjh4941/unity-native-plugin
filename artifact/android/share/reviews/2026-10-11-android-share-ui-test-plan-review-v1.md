# レビュー結果

- 日付: 2026-10-11
- 対象ファイル:
  1. `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v1.md`（以下「計画」）
  2. `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v6.md`（以下「補助 v6」）
  3. 未コミットの差分 `scripts/android_ui_driver.py`、`Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない。Codex の ID は統合のときに付けた）

---

## 1. サブエージェント（Claude）

確かめたこと: `PYTHONDONTWRITEBYTECODE=1 python3 -m doctest scripts/android_ui_driver.py` は通った（出力なし）。1.x の文言は `UnityAndroidShareManager.kt`（1.12.0）:323-400、`ShareRepositoryImpl.kt`（1.12.0）:205-213・245-266、`DirectShareShortcutPublisher.kt`（1.12.0）、サンプル `AndroidShareManagerExampleController.cs` と照らした。以下の行番号は、リポジトリの根からの相対パスに付けている。

## 強み

- サンプルの期待値の文字列は、どれもコントローラーと合っている。`SetPendingOperation` の題、`[event] <題>: Success`、`\nError: `、`[onSelected] Selected: <pkg>` は `AndroidShareManagerExampleController.cs:27-31,470-481,515-529` と同じ。AS-10 で per-call の表示が共通イベントの表示を上書きする順も正しい（`AndroidShareManager.cs` の `FireCallbackResult` が共通 → per-call の順）。
- ファイルの失敗の 1.x 列は Kotlin の裏が取れている。AS-21〜24 の `File not found: <path>` と `File cannot be shared: <path>. Ensure ...` は `ShareRepositoryImpl.kt:205-213` と `UnityAndroidShareManager.kt:339-350` のとおり。AS-24 の「1.x は原因のパス」も、`filePaths.map { fileToContentUri(it) }`（`:107`）が最初に失敗したパスで投げることと合う。AS-31・32 の 1.x「同じ」も、`DirectShareShortcutPublisher` の `decodeByteArray ?: throw InvalidBase64Icon(id)` で成り立つ。`"!!!"` は Android の `Base64.DEFAULT` が読み飛ばして空の配列になるが、ビットマップにできないので同じ文言になる。
- 「依頼を先に出し、同じフレームで開き、後で待つ」形は、2.2 のすべての Sharesheet のテストと AS-46 の 1.x 列（Back を先に出す）に通っている。AS-49 では、取り消しを同じフレームで呼ぶ理由（F-9 は main で開く処理の後に動く）も設計 7.3 と合っている。
- 補助の Sharesheet の命令は、設計 3.3・3.4 の骨格を実装している。具体的には、60 秒の期限（`SHEET_TIMEOUT`）、adb の呼び出しへの残り時間の上限（`remaining()`。doctest 付き）、`Failure` と `TimeoutExpired` のときに閉じてから `fail` を返すこと（`sheet_command`）、Sharesheet が出ている木は `last_dump` に残さず端末のダンプも消すこと（`dump()`:410-414）、理由を固定の語にすること（`not-found`、`sheet-did-not-open`、`did-not-close`、`present`）。
- `Wait` は失敗の経路でも `s_pending` から外してから `Assert.Fail` する。`Do` を `Send` と `Wait` に分けたので、今の Dialog のテストの書き方を変えずに済む。

## 改善点

### 高優先度

**U1-1（A1）`absent-in-sheet` は、同じシートで在るべきものを確かめないので、何も表示されていなくても `ok` になる（AS-43、AS-44）**
- 対象: 計画 2.2 の AS-43・AS-44（`...ui-test-plan-v1.md:84-85`）、補助 v6 3.1（`...ui-driver-design-v6.md:137`）、`scripts/android_ui_driver.py:639-654`（`do_absent_in_sheet`）・`:595-611`（`search_sheet`）
- 問題: `absent-in-sheet` は「8 回スワイプしても見つからなければ ok」なので、Chooser Action の並びがまるごと出ていない場合でも通る。たとえば `NormalizeActions` の退行で重複の 2 つとも捨てる、印の判定の誤りで正しいものまで捨てる、といった場合。AS-44 は A44a が出ることをどこでも確かめていない。AS-43 の「正しい A43 は出る」は「別のテストで」としか書いておらず、捨てたものも 3 つのうち 1 つ（B43）しか見ていない。さらに `search_sheet` は、`wait_sheet` が返した最初の木（シートのパッケージが現れた直後で、中身がまだ並んでいないことがある）で探し始めてすぐにスワイプする。スワイプでヘッダー（プレビューとアクションの並び）が上へ流れて見えなくなる場合も、「無い」と判定してしまう（要確認: Android 16 の intentresolver で、スワイプがアクションの並びを画面の外へ送るか。アクションの並びが横にスクロールして、画面の外の項目が木に出ないか）。
- 根拠: native-toolkit の `UiDriver.kt:219-228` は、スワイプの前に 5 秒 `Until.findObject` で待つ。こちらは待たずにスワイプする（`android_ui_driver.py:598-610`）。設計 5.3 の「最初の 1 つだけ表示する」（`android-share-design-v6.md:452`）を、肯定の側で押さえるテストが無い。
- 直し方: `absent-in-sheet <無いはずの文言> <在るはずの文言>` のように、同じシートの中の確かめを必須の引数にする。先に在るはずの文言を（スワイプしないで、`present-in-sheet` と同じく 10 秒）待ち、見つかった木からだけ「無い」を判定する。AS-44 は A44a を対照にし、加えて `tap-in-sheet A44a` で `Action|<id>` が 1 回届くことを確かめる。AS-43 は捨てる 3 種のうちラベルのある 2 つに別々のラベルを付け、それぞれを A43 を対照にして確かめる。あわせて AS-44 の 1.x 列で使う命令を決める。1.x では A44b が出るので、`absent-in-sheet` は `fail present` になり、`Wait` が `Assert.Fail` する。1.x 列は `present-in-sheet A44b` を使う、と書く。

**U1-2（B）Sharesheet を閉じるのに使う `back` は、Sharesheet の命令の保証（失敗しても閉じる、閉じたことの確かめ、60 秒の上限）を持たない**
- 対象: 計画 AS-01〜08・AS-48・AS-53・AS-46 の 1.x 列（`:33-40,89,94,87`）、計画 3 章（`:105`「1 つの依頼は、それだけで終わる操作（…、`back`）。補助は、失敗でも Sharesheet を閉じてから答える」）、補助 v6 3.1（`:140`）、Share 設計 v6 8.3（`android-share-design-v6.md:659`「今の `back` で足りる」）
- 問題: `do_back`（`android_ui_driver.py:473-482`）は `wait_for` の 10 秒の中でシートを見つけ、Back を 1 回押すだけである。閉じたことを確かめず、`sheet_command` を通らない。そのため、シートが 10 秒を過ぎてから出たとき（`fail timeout waiting for ...` を返した後にシートが出る）や、1 回の Back で閉じなかったときに、シートが残る。V-11 のとおりシートが出ている間は Unity が止まるので、テストの後片付けでは閉じられず、Unity のテストの実行の時間切れ（V-12 の記録では 600 秒）まで止まる。計画 3 章の「補助は、失敗でも閉じてから答える」は `back` については事実と違う。AS-01 の期待「依頼の答えが ok（Sharesheet が出て、閉じた）」も、`back` は閉じたことを確かめないので言えない。
- 根拠: `android_ui_driver.py:473-482`。`sheet_command` を通るのは `tap/absent/present-in-sheet` だけ（`:624-670`）。
- 直し方: Sharesheet の命令 `close-sheet`（引数なし）を足す。中身は `sheet_command(lambda d: wait_sheet(d); close_sheet() or raise Failure("did-not-close"))` とする。AS-01〜08・AS-48・AS-53・AS-46 の 1.x 列は、これに置き換える。補助 v6 3.1 の注記と Share 設計 8.3:659 の「`back` で足りる」を直し、計画 3 章の列挙から `back` を外す。

**U1-3（B）「同じ名前の別のアプリが無いこと」は書いてあるだけで、確かめる仕組みが無い**
- 対象: 計画 `:21`、補助 v6 V-12（`:252`）、`do_tap_in_sheet`（`android_ui_driver.py:624-637`）
- 問題: `find_in_sheet` は、文言が同じ部品のうちいちばん上のものを押す。サンプルのアプリ（`com.jonghyunkim.nativetoolkit.android`。Android™ Profile でビルドすると名前は同じ「Unity NativeToolkit」）を利用者がまた入れれば、1 回目の事故（Player が後ろで止まり、600 秒で時間切れ）がそのまま起きる。テストの共有の中身がほかのアプリに届くことにもなる。
- 直し方（どちらか、または両方）:
  - (a) 補助の側: 押した後に、前面の Activity のパッケージ（`dumpsys activity activities` の resumed の行。`app_stopped` と同じ読み方）が `self.package` かを 10 秒以内に確かめる。違えば `do_front` で Player を前に戻し、`fail wrong-app` を返す。待ち続けずに、早く失敗になる。
  - (b) テストの側: AS-10・AS-47 の前に、`count text=<名前>&package=com.android.intentresolver` を数えることはできない（シートが出ていないため）。そこで、補助の起動のときか `HELLO` のときに、`cmd package query-activities --brief -a android.intent.action.SEND -t text/plain` で SEND を受けるアプリを数え、ラベルが同じものがあれば終了コード 2 で止める（要確認: ラベルの取り方）。
  - (a) は安く入るので、まず (a) を入れる。

### 中優先度

**U1-4（B）「来ない」を確かめる待ち時間と、選択・アクションのイベントの数え方が決まっていない**
- 対象: 計画 AS-45・AS-48・AS-49（`:86,89,90`）、3 章の後片付け（`:110`）
- 問題: 後片付けの照合は「受け付けさせた要求の数」と「出た結果の数」を比べる（Dialog の形）。しかし Share には、要求に対応しない `Selected|...` と `Action|...` がある。それらを数に入れるのか、テストごとに期待の数を持つのかが書かれていない。AS-49 は ID の受け渡しを確かめる唯一のテストだが、選択のブロードキャストは Player が戻った後のフレームで届くので、`Wait` が戻った直後に「来ない」を判定すると、届く前に通ってしまうことがある。
- 直し方: 3 章に次を足す。
  - 各テストは `Op` / `Selected` / `Action` の期待の数を持つ。後片付けは 3 種を別々に照らす。
  - 「来ない」を確かめるテストは、`Wait` の後に Player が `OnApplicationPause(false)` を受けてから 2 秒待ち、そのあとで数を照らす。
  - 0.5 秒の追加の待ちは、Player が戻った時点から数える。

**U1-5（B、コード）`WaitForPending` は最初の失敗で止まり、残りの依頼を回収しない**
- 対象: `AndroidUiDriver.cs:138-144`、計画 3 章 `:106,109`
- 問題: `Wait` が `Assert.Fail` すると例外が上がる。`Safely` はそこで `yield break` する（`AndroidDialogPlayerTests.cs:194-` の形）ので、`numbers` の残りは `s_pending` に残る。補助は順に処理するので、その後の後片付けの `Do`（計画の「Sharesheet が残っていれば back」）や次のテストの最初の `Do` は、まだ動いている Sharesheet の命令（最大で約 80 秒。U1-14 を参照）の後ろに並び、20 秒で時間切れになる。1 つの失敗が後のテストの失敗に連なる。
- 直し方: `WaitForPending` の中で番号ごとに例外を受け止めて続け、最後にまとめて投げる。または、後片付けで番号ごとに `Safely(Wait(n, 90))` を回す。XML コメントの「ラッパーを通す」は、この挙動を前提にした書き方に直す。

**U1-6（B、コード）`sheet_command` は `Failure` と `TimeoutExpired` しか受け止めないので、ほかの例外ではシートが開いたまま残る**
- 対象: `android_ui_driver.py:554-560`、補助 v6 3.3（`:161`「失敗・見つからない・期限切れのどれでも … 閉じてから」）
- 問題: `ET.fromstring` の `ParseError`（`dump()`:409 は try の外）、`OSError`、`ValueError` などは `command()` の `except Exception`（`:384`）まで素通りし、`close_sheet` を呼ばない。シートは開いたまま、Unity は止まったままになる。
- 直し方: `except Exception` で閉じてから、`Failure` 以外は `Failure("error")` のような固定の語にして返す。`dump()` の `ET.fromstring` も try の中に入れて `Failure("bad-dump")` にする。

**U1-7（B）AS-52 の 1.x の記録の手順は、戻った後にシートが出たときの備えが無い。2.0.0 の呼び方と期待の書き方も合っていない**
- 対象: 計画 AS-52（`:93`）、Share 設計 5.3（`android-share-design-v6.md:444`「要確認」）
- 問題（1.x）: 後ろに回った状態で 1.x の `startActivity` が黙って止められるか、遅れて走るか（後ろからの起動の制限の例外や猶予）は確かめていない（要確認）。遅れて走ると、`background 3000` で Player が戻ったときに、Player のタスクの上にシートが出る。Unity は止まり、誰も閉じない。
- 問題（2.0.0）: 「Bridge の `ntk_share_text` を直接呼ぶ」としながら、期待は Manager の結果の文字列 `Op|shareText|False|...` になっている。Dialog の AD-27 は Manager の `s_callbackPointer` を渡して Manager の経路で結果を出していた（`AndroidDialogPlayerTests.cs:566-568`）。Share では `CallNative` と Manager の `s_donePointer` を通す、と書かないと、この期待は実装できない。
- 直し方:
  - 1.x の記録の手順で、Player が戻った後に `count package=com.android.intentresolver` で数え、0 でなければ U1-2 の `close-sheet` で閉じてから記録する。
  - 2.0.0 は「`AndroidShareManager.CallNative` に Manager の受け口を渡して `ntk_share_text` を呼ぶ」と明記する。または J-S16 のとおり、別のスレッドから `AndroidShareManager.Instance.ShareText` を呼ぶ形にして、1.x と 2.0.0 で同じテストを流す。後者を勧める。そうすれば「流さない」の非対称が消える。

**U1-8（B）AS-41 が確かめるのは `text` だけで、計画の「構造体の写しを端から端まで押さえる」に届いていない**
- 対象: 計画 AS-41（`:82`）、Share 設計 7.3（`android-share-design-v6.md:616`）
- 問題: `ntk_share_text_content` の `title` / `subject` / `preview_title` / `preview_thumbnail_path` は位置 16〜48 にある。この位置がずれても、サンプルの AS-03・AS-04 は `Success` しか見ないので通る。層 1 は大きさと位置を自分の宣言で照らすだけで、端末の上の Kotlin まで届いた値は見ない。
- 直し方: AS-41 に `previewTitle = "ntk preview title 41"` を足す。Sharesheet はプレビューの題を出す（要確認: Android 16 の intentresolver で `EXTRA_TITLE` が出るか）。そのうえで、もう 1 回シートを開いて `present-in-sheet` で確かめる（AS-41b）。サムネイルは、テストの PNG を渡したときに成功が返ることだけを確かめれば足りる。

**U1-9（B、コード）`close_sheet` は Back を 1 回しか押さない（要確認）**
- 対象: `android_ui_driver.py:613-622`
- 問題: `absent-in-sheet` は 8 回スワイプしてシートを広げ、スクロールした後に閉じる。native-toolkit の `closePackage` は、Back を最大 4 回、各 2 秒の待ちで押す（`UiDriver.kt` の `closePackage`）。1 回目の Back が広げたシートを畳むだけだと、`close_sheet` は False を返し、`did-not-close` → もう一度 `close_sheet` となる。2 回目でも閉じなければ、シートは残る（U1-2 と同じく Unity が止まる）。
- 直し方: 10 秒の中で「Back → 2 秒待って消えたか」を最大 4 回繰り返す。V-13 の確かめは `tap/present` だけなので、8 回スワイプした後の `absent-in-sheet` の閉じ方を V-15 として実測し、記録する。

### 低優先度

**U1-10（B）AS-13 の Home で、サンプルの `OnDisable` が `CancelPendingShareCallback` を呼び、要求していない結果が 1 件出る**
- 根拠: `AndroidShareManagerExampleController.cs:81-90`、`NativeToolkitSampleNavigator.cs:239-244`（`Object.Destroy`）
- 問題: 計画 3 章の「要求の数 = 結果の数」の照合で、AS-13 だけ 1 件ずれる。
- 直し方: AS-13 の期待に `[event]`（`cancelPendingShareCallback` の成功）が 1 件あることを書き、要求の数に足す。

**U1-11（B）AS-9 の「まず Success、押した後 ChooserAction」は `ResultTextBlock` の最後の値では見分けられない**
- 問題: シートが出ている間は Unity が止まるので、成功の表示が描かれる前に止まることがある。照らせるのは最後の値だけである。
- 直し方: 「最後の値が `[event] ChooserAction: ...SAVE`。途中の `Success` は Manager の `ShareOperationCompleted` を購読して 1 回を数える」と書き分ける。

**U1-12（B）AS-29 の入力では、設計 5.3 が「要確認」としている null の扱いを測れない**
- 根拠: `ShareRepositoryImpl.kt:107` は先頭から順に変換する。`"a"` が先に `File not found: a` で失敗するので、null の要素まで届かない。
- 直し方: 入力を `[在るファイル, null]` にする。

**U1-13（B）AS-51 は前の実行で残った共有先でも通りうる。サンプルの ID の後片付けも書かれていない**
- 問題: 前の実行の後片付けが失敗すると「NTK AS51」が残る。この実行で ID と label の位置が入れ替わる退行があっても、残った共有先で `present-in-sheet` が通る。
- 直し方: テストの始めに `ntk_as51` を消す。ラベルには実行ごとの接尾辞（`<run>` の先頭 6 桁など）を付ける。AS-12 の後片付けで消す ID `native_toolkit_sample_target` も明記する。

**U1-14（C）最悪の所要時間は、設計に書いた 70 秒ではなく約 80 秒になる**
- 根拠: `absent` / `present` は閉じられなかったとき `did-not-close` を投げる。それを受けた `sheet_command` がもう一度 `close_sheet`（10 秒）を呼ぶ（`android_ui_driver.py:651-652,667-668,559`）。期限の後も各 adb の呼び出しは最低 1 秒もらえる（`remaining`）。
- 問題: 90 秒の余裕はまだあるが、テストの 90 秒は依頼を出した時点から数える。そのため、logcat の遅れや、前の `background` の戻りの待ち（最大 5 秒）が食い込む。
- 直し方: 補助 v6 3.3 の最悪の時間を直す。または `did-not-close` のときは 2 回目の `close_sheet` を省く。

**U1-15（B）`PendingCallCountForTests` が 0 に戻ることを、UI のテストでは確かめていない**
- 対象: 計画 `:75`
- 問題: 成功の完了 → `release` の経路（E-23 の本来の場所）は、シートが開く UI のテストにしかない。
- 直し方: `AndroidSharePlayerTests` の後片付けにも同じ確かめを入れる。

**U1-16（B、コード）プライバシーの後始末に穴がある**
- 根拠: `android_ui_driver.py:401-415,433-440,724-735`
- 問題:
  - (a) `exec-out cat` や後の `rm -f` が時間切れになると、端末のダンプがシートの中身のまま残る（次の読み取りまで残る。補助の終わりでは消さない）。
  - (b) 押した後の非シートの画面（同じ名前の別アプリを押したときは、そのアプリの画面）が `last_dump` に残る。後の `wait` が読み取りに失敗し続けて時間切れになると、それが `--dump-dir` に書かれうる。
  - (c) `do_expect` は読み取った値を理由に入れる（`:484-489`）。シートの中で使わないことは、設計で決めてあるだけで、コードでは守られていない。
- 直し方:
  - (a) `sheet_command` の `finally` と `main` の `finally` で `rm -f DUMP_PATH` を実行する。
  - (b) Sharesheet の命令の終わりで `last_dump = None` にする。
  - (c) `do_expect` で `shows_sheet(root)` が真なら、値を出さずに `Failure("mismatch")` にする。

**U1-17（B）シートが開かないはずのテストに、開いてしまったときの備えが無い**
- 対象: AS-11、AS-21〜36、2.0.0 の AS-46
- 問題: 退行でシートが開くと、Unity は止まり、補助がいても誰も閉じない。テストの実行の時間切れまで待つことになる。
- 直し方: 補助に `no-sheet <ミリ秒>` を足す。指定の時間シートが出なければ `ok`、出れば閉じて `fail sheet-opened` を返す。UI のカテゴリのテストでは `Send` を先に出す。カテゴリの無い `AndroidShareManagerPlayerTests` は、「退行のときは止まる」と計画 4 章に書いて受け入れる。

**U1-18（B、コード）`tap-in-sheet` は、期限の近くで成功を失敗と取り違える**
- 根拠: `android_ui_driver.py:584-593,629-635`
- 問題: `wait_sheet_gone` は、期限を過ぎていると一度も確かめずに False を返す。その後の `search_sheet` は、シートが消えていると `sheet-closed` を投げる。押せていて遅れて閉じた場合も `fail` になる。native-toolkit の `pick` は、2 回目以降にシートが無ければ成功とみなす（`UiDriver.kt` の `pick`）。
- 直し方: 2 回目以降の試行で `shows_sheet` が偽なら `return` する。`wait_sheet_gone` は、少なくとも 1 回は読んでから判定する。

## 不足項目

- C-1: 補助 v6 5 章（`:204`）は `int Send(...)` と `LastResult` と書いている。コードは `IEnumerator Send`、`LastSent`、`LastAnswer` である（`AndroidUiDriver.cs:55,85,93`）。`Send` が `EnsureReady` を待つので `IEnumerator` になった、と設計を直す。
- C-2: 古い記述が残っている。V-11〜V-14 を確かめた後なのに、計画 `:104`「止まる見込み」と `:92`「V-14 で出るまでの時間を確かめ、期限を決める」、補助 v6 `:18`「見込み（要検証 V-11）」、Share 設計 8.2（`android-share-design-v6.md:648`）が見込みのまま。
- C-3: 補助 v6 6 章（`:214`「1 つの命令は 10 秒で打ち切り」）と 3.2 の表（`:152`「20 秒」）に、Sharesheet の命令の例外（60 秒と 90 秒）を書き足す。`present-in-sheet` が 10 秒読み直すこと（コード `:661-666`）も 3.1 に書く。
- C-4: 計画 3 章 `:114`「ほかの命令は 20 秒」と、`Send("back")` を 90 秒で待つこと（`:29`）が食い違う（U1-2 で `close-sheet` にすれば解ける）。
- C-5: 1.x 列の「見込み」のうち、コードで決まるものは根拠を付けて確定にする。AS-43 の「画像の印の無いものは 1.x も飛ばした見込み」と AS-46 の 1.x 列は、`ShareRepositoryImpl.kt:255` の `?: continue` で確定する。
- C-6（要確認）: AS-35・AS-53 の 1.x 列「`AndroidJavaObject` は呼んだスレッドをアタッチする」には根拠が無い。1.x の記録で例外が出たら、テストのスレッドで `AndroidJNI.AttachCurrentThread` / `DetachCurrentThread` を呼ぶ形にする。
- C-7: 計画 `:29` と補助 v6 `:205` の「Unity が戻った後に `Wait`」は、実際には開いたのと同じフレームで `Wait` に入り、その時点から 90 秒を数える。誤読を避けるため、そう書き直す。

## 総合評価

計画の期待値（サンプルの文字列、エラーの文言、1.x 列）は、ソースと設計 v6 5.3 に照らしておおむね正しい。2.2 の「先に依頼、同じフレームで開く、必ず回収」も一貫して適用されている。補助のコードは、60 秒の期限、残り時間を上限にした adb の呼び出し、シートの木を残さないこと、固定の理由の語、の骨格を正しく実装している。

一方で、次の 3 つが中心の穴である。
- `absent-in-sheet` は在るべきものを同じシートで確かめないので、何も出ていなくても通る（U1-1、A1）。
- Sharesheet を閉じる操作に、保証の無い `back` を使っている（U1-2）。
- 同じ名前のアプリの前提を強制していない（U1-3）。

どれも、退行や端末の状態の違いで、黙って通るか、実行全体が止まるかになる。中優先度では、`WaitForPending` の途中停止（U1-5）、`sheet_command` が受け止める例外の狭さ（U1-6）、「来ない」の判定の待ち時間（U1-4）を、1.x の記録より前に直すことを勧める。

件数: 高 3（A1 1、B 2）／中 6（B 6）／低 9（B 8、C 1）／不足項目 7（C）。A2 は 0。**A1 は 0 ではない（1 件: U1-1）。**

## 2. Codex

1. **CX-1（A1）** — scripts/android_ui_driver.py:225: `remaining()` は期限を過ぎても最低 1 秒を返す。期限直前・期限後に `dump()` の複数 adb 呼び出しが始まると、Sharesheet 命令が 60 秒を超える。設計 v6 3.3 の「各 adb 呼び出しは残り時間」「命令全体 60 秒」に反する。

2. **CX-2（A1）** — scripts/android_ui_driver.py:409: `ET.fromstring()` の例外が `sheet_command()` の捕捉対象外。Sharesheet の XML が不完全だと、シートを閉じず、端末上のダンプも消さず、固定語ではない `ParseError-...` を返す。設計 v6 3.3・3.4 の三契約を同時に破る。

3. **CX-3（A1）** — scripts/android_ui_driver.py:558: 失敗時の `close_sheet()` の戻り値を無視している。Back が効かない、または消滅確認に失敗しても元の `fail` を書く。Sharesheet が残ったまま回答し、Unity が止まったまま後続テストを妨げる。

4. **CX-4（A2）** — Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs:93: 設計 v6 は `int Send(...)` が依頼番号を返す契約だが、実装は `IEnumerator Send(...)` と共有の `LastSent` になっている。設計どおり `int number = Send(...)` と書く Share テストはコンパイルできない。

5. **CX-5（A1）** — AndroidUiDriver.cs:138: `WaitForPending()` は、最初の `Wait()` が `Assert.Fail` を投げると列挙が終わり、残りの pending を回収しない。複数依頼が残った失敗テストでは、後の回答を回収できない。「送った依頼は失敗経路でもすべて Wait」の契約を満たさない。

6. **CX-6（B）** — artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v6.md:231: テスト方針に、次を壊すと落ちるテストがない。60 秒上限、残り時間、全例外経路での close、固定失敗語、Sharesheet ダンプの非保存・端末削除。既存の doctest と Dialog の正常系では、今回追加した安全・プライバシー契約を検証できない。

7. **CX-7（B）** — artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v1.md:84: AS-43 は `absent-in-sheet("B43")` の 1 回だけだが、期待欄は「捨てたアクションは出ない」「正しい A43 は出る」としている。記載された「別の `present-in-sheet` テスト」に対応する AS 番号がない。正しいアクションが残る中心契約と、複数の除外理由の表示結果が未検証になる。

8. **CX-8（B）** — android-share-ui-test-plan-v1.md:107: 共通後片付けは、イベント数の照合・購読解除・取り出し口のクリアの後に Direct Share target を削除する順に読める。AS-12／34／51 が途中で失敗すると、削除操作自身の完了イベントが照合後にキューへ入り、次のテストへ流入し得る。

問題なしと確認した領域:

- サンプル Controller の表示文字列: AS-01〜AS-12 の題名、失敗文言、Chooser Action、選択結果は実装の文字列と一致している。
- 1.x の期待値: C# Manager／JSON builder と 1.12.0 Kotlin の parser・manager に照らして、確定値として書かれた期待に誤りは確認できなかった。
- 設計 v5→v6: Share 用 3 命令、90 秒待機、プライバシー規約、V-11〜V-14 の追加内容と相互参照は整合している。
- 設計 v6 7.2: Manager の per-call、寿命、配送、`user_data`／`release`、選択・Chooser Action の中心契約は、テスト項目として列挙されている。
- 正常に解析できた Sharesheet ダンプ: `last_dump` を消し、端末上のダンプも削除する経路は設計どおり。

## 3. 統合（止める基準の判定）

製品の振る舞いの指摘は無い。どれもテストの補助と計画の穴。A1 は、サブエージェント 1 件（U1-1）・Codex 4 件（CX-1〜CX-3、CX-5。CX-5 はサブエージェントの U1-5、CX-2 は U1-6 と同じ所）。A2 は CX-4（= C-1）。

**A1 が 0 でないので止めない。** 補助のコード、補助の設計 v7、UI テスト計画 v2 で直し、補助の Python の `doctest` で安全とプライバシーの決まりを固めて（CX-6）、再レビューする。

### 反映（2026-10-12）

- 補助のコード（`scripts/android_ui_driver.py`、`Tests/PlayMode/AndroidUiDriver.cs`）、補助の設計 v7、UI テスト計画 v2 で直した。決まりは補助の `doctest`（`ScriptedDevice`）で固め、決まりを 1 つずつ壊した 13 通りをすべて捕まえることを確かめた
- 実機で、新しい命令と閉じ方を 1.x の Manager で確かめた（補助の設計 v7 の 9 章の V-15、V-16）
- 持ち越し K-4: Share 設計 v6 の 8.2（「止まる見込み」）と 8.3（「今の `back` で足りる」）は古い。移行の実装で作る Share 設計の次の版で、補助の設計 v7 に合わせて直す
