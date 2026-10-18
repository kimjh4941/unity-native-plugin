# Android Dialog 実装レビュー v2（review-implementation-feature、ラウンド 2）

- 対象: ブランチ `feature/UNT-13`。ベース `d9714e4` の上にある未コミットの差分（`git diff` と、`git status --short` に出る未追跡ファイルの全文）
- 実装計画: `artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`
- UI テスト計画: `artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v1.md`
- 実装結果: `artifact/android/dialog/results/2026-10-11-android-dialog-implementation-feature-result-v2.md`
- 前回のレビュー: `artifact/android/dialog/reviews/2026-10-11-android-dialog-implementation-feature-review-v1.md`
- 一次資料: native-toolkit の `NativeToolkitC/Dialog.h`、`src/Dialog/Dialog.cpp`、`src/Common/{Registry.cpp,Utf8.cpp}`。Unity Test Framework 1.6.0 のソース（`Library/PackageCache/com.unity.test-framework@76560ee600cb`）
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけで、ファイルの変更と Unity・adb・実機の実行はしていない。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない）

## 1. サブエージェント（Claude）

### レビュー概要

Android Dialog の呼び出し先を、1.x の Java ブリッジから native-toolkit の C ABI 2.0.0（`DllImport("ntk")`）に移す変更。ラウンド 1 の指摘を直した後の差分全体を、修正点に限らず見直した。

ラウンド 1 の修正のうち、ヘッダーと実装に照らして正しいと確かめたもの:

- **C1-1 `Alloc`**（`AndroidNativeToolkitCApi.cs:101-114`）: 確保の後で `List.Add` が投げても解放してから投げ直す。`AllocUtf8`、`AllocStringArray`、`AllocInt32Array` はすべてこれを通る。途中で失敗しても、それまでに確保した分は一覧に入っているので、`ShowNative` の `finally` で解放される
- **R1-5**: `ShowNative` の先頭で `EnsureCreated()` を呼ぶ（`AndroidDialogManager.cs:412`）。呼ぶのは `OnDevice` の後なので UnityMain から。`Post` は、取り出し口が無ければ `volatile bool` を見て `LogError` を出す。`Debug.LogError` はどのスレッドから呼んでもよい。重複した取り出し口は `Awake` で自分を壊し、`OnDestroy` では `s_pump != this` なので何も消さない。Editor で再生を止めると `OnDestroy` がフラグを戻す（ドメインの再読み込みを切っていても同じ）
- **R1-6**: 種類への変換は内側の `try` の中にある（`:464`）。`Read` が投げても結果は `finally` で 1 回だけ解放される
- **`Read` の `out` 引数**（`AndroidDialogCApi.cs:452`、`470-474`）: C# は引数を左から評価するので、`extern` が `out` に書いた後で 2 番目の引数が読まれる。`OptionalText`（`Dialog.cpp:471-474`）は NULL なら長さ 0 を書き、`ReadUtf8OrNull` は NULL を長さより先に見る
- **R1-4 `PostAnswer`**: ガードの外にあり、受け口と層 2a の両方がこれを呼ぶ。式は `Instance` を実行のときに引く
- **R1-3 AD-26 の 300 ms**: `ntk_dialog_cancel` は削除を Android の main に投げる（`Registry.cpp:135-165` の `PostRemove`）。そのため、UnityMain が `Thread.Sleep(300)` で止まっている間に、受け口が Android の main で走る。この確かめは空振りではない
- **`Rejected` に足した引数**: ログは `[{LogTag}][{method}]` の形になった。R1-15 のとおり
- **R1-10、R1-11、R1-12、R1-13、R1-14、R1-16**: どれも反映されている。コードに「design v3」は残っていない
- **ユーザーが入力できる値で `INVALID_PARAMETER` が返る経路は無い**: 入口が拒むのは、NULL、厳密でない UTF-8、空の項目、範囲外の `checked_index`（`Dialog.cpp:138-154`、`Utf8.cpp:57-63`）。C# は `Encoding.UTF8` で厳密な UTF-8 にし、残りは先に検査している。文字列の長さの上限は無い
- **ログに秘密を出さない**: Manager、受け口、サンプルは、入力・利用者名・パスワードを長さだけで出す。`Show*` の入口のログに出るのは題名・本文・ヒントだけで、ユーザーが入力した値は出ない

一方で、Player テストの 2 つの修正（R1-1 の後片付け、R1-9 の AD-27）が頼っている `try/finally` は、テストの失敗のうち肝心の場合に実行されない。Unity Test Framework が入れ子のコルーチンを扱う仕組みによる（R2-1）。

### 重大な問題（high）

なし

### 改善提案（medium）

**R2-1** 入れ子の手順が投げたとき、後片付けと AD-27 の `finally` は実行されない。R1-1 と R1-9 の修正は、失敗したときには効かない

- 場所:
  - `Tests/PlayMode/AndroidDialogPlayerTests.cs:64-101`（TearDown。`finally` は 93-98、コメントは 95）
  - 同 `429-483`（AD-27。`finally` は 469-477、コメントは 471-472）
  - 結果ファイル 1.2（58 行目「購読を外すことと列を空けることは `finally` で行う」）、9.1 の R1-1・R1-9
- 観点: テストカバレッジ（失敗した後のテストの分離）／計画との整合（設計 7.3「テストが失敗や期限切れで終わっても、次のテストに古いダイアログと完了を持ち越さない」）
- 区分:
  - B: 失敗した後の分離が崩れる。すべて通る実行なら「1 回の呼び出しにイベント 1 回」の照合は成り立つので、検証の中心は壊れていない。ただしラウンド 1 の修正の目的そのもの
  - C: コメントと結果ファイルの記述
- 根拠（UTF 1.6.0 のソースで確認）:
  - `[UnityTearDown]` は `EnumeratorHelper.UnpackNestedEnumerators`（`UnityEngine.TestRunner/NUnitExtensions/Attributes/EnumeratorHelper.cs:31-55`）で入れ子を平らにして回す。入れ子の `MoveNext` が投げると、例外は `BeforeAfterTestCommandBase.cs:271` で捕まえられ、ループを抜けるだけ。スタックに積まれた外側の TearDown のイテレーターは `Dispose` されない
  - テストの本体も同じ。`TestEnumerator.cs:35-62` は `ExceptionWasRecorded` で抜けるだけで、`Dispose` は呼ばない
  - パッケージ全体を見ても、テストのイテレーターに `Dispose` を呼ぶ箇所は無い
  - C# のイテレーターの `finally` が走るのは、`Dispose` されたときか、例外が外側のイテレーター自身の `MoveNext` から出たときだけ
  - TearDown と AD-27 で投げうるのは、`yield return` した `Ui.Do` の中の `Assert.Fail` / `Assert.Ignore`（補助の `fail` の答えや、20 秒の期限切れ）だけ。外側の本体には、投げる文がほとんど無い。つまり、この `finally` が効く場面は事実上無い
- 何が起きるか:
  - TearDown の `Ui.Do("count" / "back" / "tap" / "wait-gone")` が失敗すると、次の 4 つがすべて飛ぶ: 購読を外すこと、`ResetForTests()`、`Unload()`、数の照合
    - 遅れて届く完了は次のテストの `_events` に入る。完全一致で比べるテスト（AD-29〜34）や、次の後片付けの照合が落ち、原因を取り違える（R1-1 と同じ症状）
    - サンプルのシーンが読み込まれたまま残る。次の `LoadAtTopMenu` が 2 つ目を追加で読み、`GetSceneByName` が古い方を返して連鎖する
  - 失敗を起こす具体例:
    - adb が一時的に応答しない
    - `wait-gone` が期限内に終わらない
    - 閉じられない設定で肯定ボタンが無効なダイアログ。たとえば `cancelable: false` で空のテキスト入力だと、Back でも肯定でも閉じない
  - AD-27 で `Ui.Do("background", "3000")` が失敗すると（補助が `STOPPED` を確かめられず `fail` を返したなど）、`finally` の `abandon.Cancel()`・`Join()`・`_requests++` が走らない
    - スレッドが答えのファイルを先に見つけていれば、Bridge を呼ぶ。アプリは前面にいるので、"t27" のダイアログが実際に出る
    - その結果、後片付けの照合が「0 call(s) reported 1 event(s)」で落ちるか、次のテストにダイアログと完了が持ち越される
    - R1-9 が想定した場面のうち `Eventually` の期限切れは直っている。`Eventually` は投げずに `met(false)` で抜けるので、`finally` が走る。直っていないのは `Ui.Do` の失敗だけ
  - AD-28 の `finally`（`Screen.orientation` を戻す。331-334 行目）も同じ理由で走らない。これはコミット済みのコードだが、仕組みは同じ
- 直し方の例:
  1. TearDown の UI の手順を投げない形で回す。たとえば次のような包みで、入れ子を再帰的に `try` で囲んで `MoveNext` し、失敗を変数に貯める。購読を外すこと、列を空けること、`Unload` は必ず行い、最後に貯めた失敗を `Assert.Fail` する
     ```csharp
     IEnumerator Safely(IEnumerator step, List<string> failures)
     ```
     入れ子の `IEnumerator` を `yield return` で外に渡すと UTF が平らにしてしまい、`try` の外で投げられる。そのため、包みの中で再帰する必要がある
  2. AD-27 の `thread`・`abandon`・`code` をフィールドに持つ。中止、`Join`、数え入れは TearDown の先頭（`yield` の前）で行う。TearDown は本体が失敗しても必ず呼ばれる。あわせて R2-2 も直す
  3. 安全策として、`SetUp` でも `ResetForTests()` を呼び、購読を張り直す
  4. 95 行目と 471-472 行目のコメント、結果ファイル 1.2 と 9.1 の R1-1・R1-9 の記述を実態に合わせる
- 修正の後で確かめたいこと: TearDown の 1 本目の `Ui.Do` を、テスト用に必ず失敗させる。それでも次のテストに完了が漏れないことを確かめる（壊すと落ちることの確認）

### 軽微な指摘（low）

**R2-2** AD-27 のスレッドは、中止の合図を待ちの間に見ない。また、補助の準備より前に動き出す

- 場所: `Tests/PlayMode/AndroidUiDriver.cs:98-108`（`WaitForAnswerFile`）、`AndroidDialogPlayerTests.cs:436-463`、`473-474`
- 観点: テストカバレッジ（分離と単独の実行）
- 区分: B（範囲）
- 何が起きるか:
  1. **待ちが中止を見ない**: `WaitForAnswerFile` は 20 秒の期限まで `abandon` を見ずに待つ。`Eventually` の 10 秒が切れた場合、`finally` の `thread.Join()` は、残りの最大約 10 秒、UnityMain を止める。補助が `fail` を返した直後に、UnityMain が答えのファイルを先に読んで消した場合は、スレッドは 20 秒待ち続ける
  2. **単独で流すと必ず落ちる**: `s_directory` は、最初の `Ui.Do` の `EnsureReady` が設定する。AD-27 を単独で流すと（`-testFilter` など）、スレッドは `Ui.Do` より先に `Path.Combine(s_directory!, …)` を呼んで `ArgumentNullException` になり、`threadError` で落ちる。全体を流すときは、前のテストの後片付けが補助の準備を済ませているので起きない
- 直し方:
  - `WaitForAnswerFile` に `CancellationToken` を渡す
  - スレッドを起こす前に、答えのファイルのパスを UnityMain で確定させる。たとえば `yield return Ui.Do("rotation")` などで、先に補助を準備させておく

**R2-3** 取り出し口が「無効」にされると、完了が何のログも無く止まる。R1-5 の修正にはテストが無い

- 場所: `Runtime/Android/Common/AndroidNativeCallbackQueue.cs:47-53`（`Post`）、`60-68`（`EnsureCreated`）、`77-95`
- 観点: Bridge の品質／テストカバレッジ
- 区分:
  - A 相当（潜在。ふつうでない条件）。ただし 1.x の dispatcher も無効にされると同じく止まるので、回帰ではない
  - R1-5 の確かめの抜けは B（範囲）
- 何が起きるか:
  - 利用者のコードが、DDOL のオブジェクトを `SetActive(false)` にするか、component の `enabled = false` にすると、`Update` が走らなくなる
  - それでも `s_pumping` は true のまま、`s_pump != null` も true のまま
  - そのため、`Post` は `LogError` を出さず、`EnsureCreated` も作り直さない。以後の完了は列に溜まり続け、イベントは出ない。R1-5（壊された場合）では直した「無言の失敗」が、無効にされた場合には残る
- 直し方:
  - 直すなら、`EnsureCreated` で `!s_pump.isActiveAndEnabled` のとき、`gameObject.SetActive(true)` と `enabled = true` に戻す（2 行）
  - 直さないなら、結果ファイル 7 章に残件として理由つきで書く（止める基準 3）
- R1-5 の確かめ: 層 2a で書ける
  1. 取り出し口のオブジェクトを `DestroyImmediate` する
  2. `PostAnswer` を呼び、`LogAssert.Expect(LogType.Error, …)` で `LogError` を確かめる
  3. Manager を壊して `Instance` を引き直す（`Awake` → `EnsureCreated`）
  4. 次のフレームで 1 回届くことを見る

  結果ファイル 9.2 は、R1-5 を「コードレビュー」としている。Editor では `ShowNative` を通らないので、作り直しは `Awake` の経路でしか確かめられない。その点も書いておくとよい

**R2-4** 結果ファイル 9.2 の「AD-26 の 300 ms は、壊し方として成り立たない」は要確認

- 場所: 結果ファイル 9.2（291 行目）
- 観点: テストカバレッジ（壊すと落ちることの確認）
- 区分: C（要確認）
- 内容:
  - 受け口が `PostAnswer` を通さずに、その場で `Instance.Raise(answer)` を呼ぶよう壊したとする。インスタンスが既にあれば、`Instance` の getter が触るのは `MonoBehaviour` の null 比較だけ。これはキャッシュしたポインタを見るだけで、main スレッドを確かめる API は呼ばない（要確認）
  - `Raise` が呼ぶのも、`Debug.Log` と event の呼び出しだけ
  - そのため、この壊し方は Android の main で例外を出さずに動き、AD-26 の `Assert.IsEmpty` が落ちるはず
- 直し方: 流せるなら流して、表に足す。流さないなら、理由の文を「確かめていない」に改める

**R2-5** 記述が実態とずれている箇所

- 観点: ルール／計画との整合
- 区分: C
- 該当箇所:
  - `agent-rules/coding-rules/testing.md:376`: 「`AndroidDialogManagerEditorTests` 9 本」。実際は 11 本（結果ファイル 5 も 11 / 11）
  - `agent-rules/coding-rules/common.md:65`: 「取り出し口は Manager の `Awake`（UnityMain）で作る」。R1-5 の修正（呼び出しのたびに `EnsureCreated`）が規則に無い。Share、Clipboard、Notification はこの規則を写すので、同じ穴が再び生じる
  - `AndroidNativeCallbackQueue.cs:21-24`: 「Otherwise it behaves like the dispatcher path the 1.x bridge used … each frame runs only the actions that were queued when the frame's Update began」。1.x の dispatcher は、走らせている間に足されたものも同じフレームで走らせていた（`UnityMainThreadDispatcher.cs:55-64`）。「始めの件数だけ」は新しい振る舞いだと分かる書き方にする
  - `scripts/verify_unity_android.sh:3`、`45`: 冒頭の説明が Editor の PlayMode（層 2a）に触れていない。`--skip-editmode` で層 2a も飛ぶことも書かれていない
  - UI テスト計画 5 章の実行記録と `artifact/topics/android-c-abi-2/README.md` 6.6 は、移行後の 1 回目（結果 v1。EditMode 1226/1227）しか書いていない。結果 v2 の 4 章にある修正後の回（`verify-fix1`、1229/1230）が無い

**R2-6** `Request` は呼ぶ前に数えるので、呼び出しが投げると後片付けに 2 つ目の失敗が出る

- 場所: `AndroidDialogPlayerTests.cs:490-494`
- 観点: テストカバレッジ（失敗の読みやすさ）
- 区分: B（範囲。ごく軽い）
- 何が起きるか: たとえば `FindButton(...)!` が null で、`Press` が `NullReferenceException` を投げると、`_requests` は 1、イベントは 0 になる。後片付けが「1 call(s) reported 0 event(s)」を足し、本当の原因の横に紛らわしい失敗が並ぶ
- 直し方: `call()` が戻った後で数える。同期の失敗は `call()` の中でイベントが出るので、数える順を変えても照合は崩れない

**R2-7** コードの小さな整理

- 場所: `AndroidDialogCApi.cs:55-58`（`AnswerButton`、`ButtonPositive`、`ButtonNegative`）、`504-505`（`ntk_dialog_result_button`）、`AndroidDialogManager.cs:369-370`（`PostAnswer`）
- 観点: コード品質／ルール（csharp.md）
- 内容:
  - 使われていない定数と `extern` がある。ヘッダーを写すために残すなら、そうとコメントに書く
  - `PostAnswer` は internal で、入口のログが無い。中の `Post` がログを出すので実害は無い

**R2-8** アプリの終了時に、`LogError` が出るかもしれない（要確認）

- 場所: `AndroidNativeCallbackQueue.cs:51-52`、`89-95`
- 観点: Bridge の品質
- 区分: 要確認（ログだけで、実害は無い）
- 内容:
  - `Application.Quit` のとき、取り出し口の `OnDestroy` が先に走る。その後で、開いていたダイアログが Activity の破棄によって `CANCELED_BY_SYSTEM` で完了すると、`Post` が「取り出し口が無い」という `LogError` を出す
  - 終了の順序と、完了がそもそも来るかは確かめていない

### 実装計画整合性チェック

| 項目 | 判定 | 根拠 |
|---|---|---|
| Manager + Bridge パターン準拠 | ○ | Bridge（`AndroidDialogCApi`）は Manager に依存しない。受け口、delegate、関数ポインタは Manager にある。共通部の取り出し口は Common に置いている |
| 変更ファイル一覧との一致 | ○ | 設計 5.1 の表と一致する。差分にあるのは Android のファイル、規則、artifact、スクリプトだけ |
| テスト方針の網羅性 | △ | 7.3 の「失敗や期限切れで終わっても持ち越さない」が、UTF の仕組みのために失敗の場合には成り立たない（R2-1）。層 1・2a・2b の観点そのものはそろっている |
| エラーケース全実装 | ○ | E-1〜E-13 は 4.4 の文言とログの段階のとおり。`IsRequestDefect` の `LogError` は、ユーザーの入力では起きない（入口の検査を確認済み） |
| 返却仕様との整合 | ○ | 4.3 の表（肯定、否定、閉じた、失敗）と、閉じたときの `""` の例外が一致する。common.md と XML コメントにも書いてある |

### プロジェクトルール適合チェック

| 項目 | 判定 | 根拠 |
|---|---|---|
| common.md 準拠 | ○ | P1〜P5 は下の表のとおり。記述のずれは R2-5（取り出し口を作る時点） |
| csharp.md 準拠 | ○ | `#nullable enable`、`LogTag`、ログの書式（R1-15 は反映済み）、`LogWarning` と `LogError` の段階を守っている。軽微な点は R2-7 |
| Bridge 実装品質（スレッド安全性・メモリ管理） | ○ | ロックを持ったまま処理を走らせない。確保したものは全部 `finally` で解放し、追加の失敗にも対応した（C1-1）。結果は 1 回だけ解放する。IL2CPP の形も正しい。潜在の点は R2-3 |
| 既存 API 互換性 | ○ | 公開のメソッド、既定値、イベントの型は 1.x と同じ。変わるのは名前空間（J-D11）と、5.3 の既知の差分だけ。ガードに `\|\| UNITY_EDITOR` を足したのは追加で、壊す変更ではない |

**プラットフォーム独立性:**

| 項目 | 判定 | 根拠 |
|---|---|---|
| P1 | ○ | 新しいファイルと型（テストを含む）はすべて `Android` で始まる |
| P1b | ○ | テストは Android の型だけを扱う。ガードは、層 1 が `UNITY_ANDROID \|\| UNITY_EDITOR`（asmdef は Editor のみ）、層 2a が `UNITY_EDITOR`、層 2b が `UNITY_ANDROID` の中に `!UNITY_EDITOR` |
| P1c | ○ | 置き場所は `Runtime/Android/{Common,Dialog}/`、名前空間も一致する。新しいファイルには `.meta` がある |
| P2 | ○ | `git diff --name-only d9714e4` と未追跡ファイルに、`Ios*` / `Mac*` / `Windows*` は無い |
| P3 | ○ | `AndroidNativeToolkitCApi` と `AndroidNativeCallbackQueue` は、Android の全機能の共通部として、common.md の表に載せてある |
| P4 | ○ | `Runtime/Common/` への追加は無い |
| P5 | ○ | クラスは `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport`・`Read`・受け口は `UNITY_ANDROID && !UNITY_EDITOR` の二重構造になっている |
| 既知の逸脱 11 件 | ○ | 前例として引いた箇所は無い |

### テストカバレッジ

確かめられているもの（コードで確認）:

- **層 1（62 本: 48 + 14）**: 6 つの構造体の大きさと位置、64 ビットの要求 ID、`Build*`（Confirm と、全種類の否定・肯定の文字を含む）、検査と境界、6 種類の写し（失敗のときの全引数を含む）、エラーの名前と文言（想定外の値 42 を含む）、共通部の版の判定・UTF-8・NULL と空の区別。数は結果ファイル 5 と一致する
- **層 2a（11 本）**: 検査の失敗（項目の空、`null` の要素、範囲外の `checkedItem`）、6 種類の Editor の警告、購読者の例外、`PostAnswer` の直後は 0 回（J-D14）、無効な Manager と壊れた Manager（新しい `Instance` に 1 回届き、古い購読者には届かない）、1 フレームの件数の上限
- **層 2b（34 本）**: 後片付けで、要求の数とイベントの数を照らす。成功した実行では、全テストで「1 回だけ」を確かめる。2 回出す壊し方で AD-10 と AD-13 が落ちたことは、結果ファイル 9.2 にある。AD-26 の 300 ms の確かめも空振りではない（`Registry.cpp` で確認）

足りないもの:

- 失敗した後の分離（R2-1、R2-2）
- R1-5 の取り出し口の作り直しと `LogError`（R2-3）。C1-1 は起こせないので、コードレビューで確かめた（このレビューで確認済み）
- AD-26 の 300 ms の確かめを、壊して落ちるかで確かめていない（R2-4）
- AD-25 は、層 2b では今も空振り（ラウンド 1 の R1-3 で、層 2a の 1 本で代えると決めた。変更なし）
- 起こせない失敗（E-5〜E-7、E-9、E-12、E-13）は、層 1 の文言とコードレビューで確かめた。計画どおり

結果ファイルの記述の照合:

- **1.2**: 後片付けの記述のうち、「`finally` で行う」に込めた「失敗しても」の意味は成り立たない（R2-1）。それ以外（`Request` で数える、5 秒待つ、0.5 秒待つ、等しくなければ失敗にする）はコードと一致する
- **5**: 本数（48、14、11、34）はコードと一致する。実機の 47/47 は記録で、このレビューでは確かめられない
- **6**: 「全 34 本で照らす」は、成功した実行については正しい
- **9**: C1-1、R1-3〜R1-8、R1-10〜R1-16 の対応はコードと一致する。R1-1 と R1-9 は失敗の場合に効かない（R2-1）。9.2 の AD-26 の理由は要確認（R2-4）

### 総合評価

**要修正（軽微）**

製品のコードに、1.x からの回帰として利用者から見える欠陥は見つからなかった。ラウンド 1 で直した製品側の修正（C1-1、R1-5、R1-6、R1-11 ほか）は正しい。

止める基準での集計:

- **A: 1 件**（R2-3）。潜在の、ふつうでない条件で、1.x と同じ振る舞い。2 行で直すか、結果ファイル 7 章に理由つきで残件として書く
- **B（中心の契約に関わる）: 1 件**（R2-1）。直すことを勧める。成功した実行での検証は成り立っているが、R1-1 と R1-9 の修正が目的とした「失敗しても次のテストに持ち越さない」は、UTF が入れ子の例外の後で外側のイテレーターを `Dispose` しないために成り立たない。修正の後は、後片付けの手順をわざと失敗させて、漏れないことを確かめる
- **B（範囲）: 3 件**（R2-2、R2-3 の確かめの抜け、R2-6）
- **C: 3 件**（R2-1 の記述、R2-4、R2-5）
- **区分なし（コード品質）と要確認: 2 件**（R2-7、R2-8）

## 2. Codex

（ID は統合のときに付けた。本文は Codex の出力のまま）

**C2-1 [A]** AndroidNativeCallbackQueue.cs:89

- Defect: pump 破棄と `Post` が競合すると、完了イベントが無期限に配送されず、ログも出ない。
- Scenario: `OnDestroy` は `s_pump = null` の後に `s_pumping = false` とする。その間に Android main から `Post` すると、action は enqueue される。しかし `s_pumping == true` を読むため、エラーは記録されない。pump は既に破棄中で、その後に別の `Show*` がなければ `Update` は二度と走らない。受理済みダイアログのイベントが欠落する。

**C2-2 [B]** AndroidDialogPlayerTests.cs:436

- Defect: AD-27 は UI driver の初期化前に worker thread を開始する。
- Scenario: AD-27 を単独実行したときや実行順が変わったとき、thread が先に `WaitForAnswerFile` へ入る。まだ `s_directory` が設定されていないため、例外になり得る。正しい実装でもテストが失敗する。

**C2-3 [B]** AndroidDialogPlayerTests.cs:467

- Defect: AD-27 の thread 待機は20秒だが、テスト側は10秒で thread 完了を失敗扱いにする。
- Scenario: UI driver が契約内の10～20秒で応答しても、テストは失敗する。cancellation は `WaitForAnswerFile` 中には確認されない。`finally` の無期限 `Join()` は、残りの待機時間だけ Unity main を停止する。

**C2-4 [C]** agent-rules/coding-rules/testing.md:376

- Defect: `AndroidDialogManagerEditorTests` を9本と記載しているが、現在は11本。実装結果 v2 の記録とも一致しない。

**C2-5 [C]** artifact/topics/android-c-abi-2/README.md:169

- Defect: Dialog の実装結果として修正前の v1 を参照している。171行目の EditMode／PlayMode 件数も v1 時点のまま。Round 1 修正後の正本は v2。

欠陥なしと確認した領域:

- `AndroidNativeToolkitCApi.Alloc` と `AllocUtf8`／文字列・`int32_t` 配列の各 caller は、登録失敗時を含めて解放が保証されている。
- `Read` の extern 呼び出しと `out UIntPtr` のインライン使用は、C# の左から右への引数評価により正しい。NULL と空文字も区別できている。
- callback queue の重複 pump 排除と Editor play-mode 終了時の `s_pump` リセットには、上記の破棄競合以外の欠陥は確認しなかった。
- 非 Unity thread からの `Debug.Log*` は設計 v4 で明示的に許可されており、一次資料との不整合はない。
- Player test の `_requests` 対 `_events` の照合と iterator の `try/finally` は、通常完了と同期拒否を正しく数えている。
- 構造体配置、P/Invoke シグネチャ、result の所有権移譲と `finally` による解放、1.x から維持する公開 API に欠陥は確認しなかった。
- `AndroidDialogManager.PostAnswer` と `ShowNative` が `EnsureCreated` を呼ぶ点は、上記以外の欠陥を指摘されていない。

## 3. 総合評価

**要修正（軽微）**（サブエージェントの評価。Codex は A 1 件・B 2 件・C 2 件）

止める基準の区分での集計: A は 2 件（R2-3、C2-1。どちらも取り出し口の、ふつうでない条件での無言の停止）、中心の契約の B は 1 件（R2-1。統合のとき、テストフレームワークのソース〈`EnumeratorHelper.ProgressOnEnumerator`〉で、入れ子の例外の後に外側のイテレーターが `Dispose` されないことを確かめた）、範囲の B は 4 件（R2-2 = C2-2・C2-3、R2-3 の確かめ、R2-6）、C は 4 件（R2-1 の記述、R2-4、R2-5 = C2-4・C2-5）、区分なし・要確認 2 件（R2-7、R2-8）。A が 0 でないので止めない。

同一レビュアーの連続ではない（ラウンド 1・2 とも別のサブエージェントのインスタンスと Codex）。ラウンド 1 の 3 件の A は 0 になり、ラウンド 2 の A は R1-5 の修正の周り（取り出し口の無効化と破棄の競合）から出た。
