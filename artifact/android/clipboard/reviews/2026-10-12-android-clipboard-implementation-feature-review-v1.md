# 実装レビュー結果 v1（Android Clipboard の C ABI 2.0.0 への移行）

- 日付: 2026-10-12
- レビュー対象: ブランチ `feature/UNT-13` の未コミットの差分（`git diff HEAD` と未追跡のファイル）
- 実装計画: `artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md`
- 実装結果: `artifact/android/clipboard/results/2026-10-12-android-clipboard-implementation-feature-result-v1.md`
- レビュアー: サブエージェント（1 章）と Codex（2 章）を並行で回した。どちらも読むだけ。どちらの出力も手を加えていない
- A: サブエージェント 2 件（S:A-1、S:A-2）、Codex 1 件（X:A-1）。S:A-1 と X:C-1 は同じ箇所
- 直したことは 3 章（取りまとめ役が書いた）

---

## 1. サブエージェント（Claude）

### レビュー概要

- 対象: `feature/UNT-13` の未コミット差分（`git diff HEAD` と未追跡ファイル）。Android Clipboard を 1.x の AndroidJavaObject/JSON ブリッジから C ABI 2.0.0（libntk.so、P/Invoke）へ移す実装。ファイルは変えていません。git も読むだけです
- 照らし合わせたもの:
  - 設計 v4（J-C1〜J-C17、4.1〜4.4、5.1〜5.4、6、7、9）と実装結果 v1
  - native-toolkit の `dist/1.13.0/.../Clipboard.h` と `Clipboard.cpp`、`Registry.h`、`Accept.h`、`Jni.cpp`
  - 1.12.0 の `UnityAndroidClipboardManager.kt`（`errorInfoOf` の 321-336 行）と `CopyUriUseCase.kt`
  - HEAD 時点の 1.x の C#（Manager と、消した Parser）
- 総じて設計とよく一致しています。`extern` 26 本の署名、`CopyOptions` の 24 バイトの配置、NULL と `""` の区別、`_free` を `finally` で呼ぶこと、受け口の形（static、`MonoPInvokeCallback`、`static readonly` の delegate、全体を try/catch）、1.x の文言とコードは、いずれも正しいことを確かめました
- 見つかったのは、新しく書いた公開 XML の約束と競合の扱いのずれ、起こしにくい経路の印の扱い、テストと記述の小さな穴です

### 重大な問題（high）

なし。

### 改善提案（medium）

**S:A-1（A / medium〜low）`StopObserving` の XML の約束「no change is raised after this call either way」が、競合で破れる**
- 場所: `AndroidClipboardManager.cs:306-318`（XML と本体）、`:711-715`（`OnChangedCore`）、`:717-733`（`RaiseChanged`）
- 根拠:
  - `OnChangedCore`（Android の main）は、印が真の時点で `Post(RaiseChanged)` します
  - その後、取り出し口がまだ回っていないうちに UnityMain が `StopObserving()` を呼ぶと、印は下りても、列に入った `RaiseChanged` は印を見ずに `ClipboardChanged` を出します
  - その結果、`StopObserving` が戻った後（次の Update）に変更イベントが 1 回出ます
  - 振る舞いは 1.x と同じ系統で、設計 5.3 の「その後の受け口では捨てる」とも矛盾しません。ただ、今回新しく書いた公開 XML が、それより強い約束をしています
  - サンプルは `OnDisable` で `StopObserving` を呼んでから UI を外すので、外した UI に触る購読者が呼ばれる形になりえます
- 提案（どちらか）:
  - (a) `RaiseChanged` の先頭でも `if (!s_observing) return;` を見る。2a に `SetObservingForTests(true)` → `OnChangedCore()` → `StopObserving()` → 数フレーム → `_changes == 0` のテストを足す。このテストは、今の実装では落ちます
  - (b) XML を「この呼び出しの後に受け口が受けた変更は出さない（すでに配送待ちのものは出ることがある）」に弱める。こちらなら区分は C に落ちます

**S:A-2（A / low。実際には起こしにくい経路）`StartObserving` が F-9 の失敗で印を無条件に偽へ戻す**
- 場所: `AndroidClipboardManager.cs:483-500`
- 根拠:
  - すでに監視中（印が真で、ネイティブも監視中）に 2 回目の `StartObserving` を呼び、F-9 が `UNKNOWN`（`Handler.post` の失敗。終了間際の looper など）か例外を返すと、印だけが偽になります
  - ネイティブの監視は続いているのに、Unity 側は以後の変更を無言で捨てます
- 提案: 呼ぶ前の印を覚えておき、失敗時は「それまで偽だったとき」だけ偽に戻す（`bool wasObserving = s_observing;`）。経路は起こせないので、テストは要りません。直さない場合は、実装結果の残件に理由つきで書いてください

**S:C-1（C / medium）公開の `StopObserving` と J-C12 の原則が対称でない**
- 場所: `AndroidClipboardManager.cs:311-318` と `:674-685`。`AndroidClipboardManagerExampleController.cs:77-84`
- 根拠:
  - J-C12 は「Unity が始めていない監視を、ほかの利用者のために止めない」として、`OnDestroy` では印が偽なら F-10 を呼びません
  - 一方、公開の `StopObserving()` は印によらず F-10 を呼びます
  - サンプルは `OnDisable` で無条件に `StopObserving()` を呼ぶので、サンプル画面を離れるだけで、同じアプリの Kotlin の利用者の監視が止まります
  - 設計 5.3 の「監視の共有」の行とクラスの remarks には書いてあるので、契約違反ではありません。ただし `StopObserving` 自身の XML には書いてありません
- 提案:
  - 意図どおりなら、`StopObserving` の XML に「プロセス全体の監視を止める（Unity が始めていなくても）」と 1 文足す
  - 揃えたいなら、印が偽のときは F-10 を呼ばずに成功を返す形を、設計判断として検討する（AC-34 の (2) はそのまま通ります）

### 軽微な指摘（low）

**S:B-1（B / low）設計 7.3 の「各テストのイベント数の照合と UnityMain の確認」が後片付けに無い**
- 場所: `AndroidClipboardManagerIntegrationTests.cs:120-146`（TearDown）、`:169-171`（`AssertEventsOnMainThread` を使うのは 569、593、667 行だけ）
- 根拠:
  - 設計 7.3 は「Share の `AndroidShareEvents` と同じ形」で、各テストが受けたイベントの数と、UnityMain で出たことを照らすとしています
  - 実装結果 5.1 は「AC-20、AC-21a、AC-24」の 3 本だけと書いていて、1.2 に設計との差分として書かれていません
  - 結果を 2 回出す誤りは AC-20 の厳密な順序の照合で落ちますが、ほかのテストは `_results` を後片付けで捨てるので照合しません
- 提案: 後片付けで `AssertEventsOnMainThread()` を呼ぶ。足さないなら、実装結果 1.2 に差分として書く

**S:B-2（B / low）壊しても落ちない所（テストが守っていない所）**
- `RaiseChanged` が印を見ないこと（S:A-1）: どのテストも落ちません
- `Initialize()` から `EnsureChangeListener()` の呼び出しを消しても、`StartObserving` が登録するので AC-21a と AC-31 は通ります。設計 9 章 K-4 の「登録の抜けは AC-21a と AC-31 が落ちる」は、Initialize 側の抜けについては成り立ちません。実害も無いので、K-4 の記述を直す程度で足ります
- `StopIfObserving` の「ネイティブが使えないなら F-10 を呼ばない」分岐: 2a の差し替え（`s_stopObservingForTests != null`）がこの判定を飛ばすので、検証されていません（`:678-680`）
- `LogFailure` の重さの振り分け（`IsRequestDefect` は `LogError`、`NOT_INITIALIZED` は `LogWarning`）: 判定の関数は層 1 にありますが、Manager の配線は未検証です
- `Invoke` の `FreeAll` と、`ReadContent` での MIME の NULL を `""` にする処理: どちらも検証の手段がありません（コードレビューで確認済み。問題なし）
- 提案: 前の 2 つは S:A-1 の対応か K-4 の記述の訂正で片付きます。残りは「コードレビューで確認」として実装結果に書けば足ります

**S:C-2（C / low）層 2b のテストファイルのコメントが古い**
- 場所: `AndroidClipboardManagerIntegrationTests.cs:40`（`design-v3.md` を参照。今は v4）、`:59-61`（「Manager events are delivered through UnityMainThreadDispatcher」。今は `AndroidNativeCallbackQueue`）、`:283`、`:296-297`（「JNI + AndroidJavaProxy round trip」）
- 提案: v4 と C ABI の経路に合わせて直す（1.x の分岐を消すときにまとめてもよい）

**S:C-3（C / low）設計 5.3 の「どれも次のフレーム以降」は正確でない**
- 場所: 設計 v4 の 328 行目。実装結果 3.4 の最終行は、これを「設計 5.3 のとおり」と引いています
- 根拠: `AndroidNativeCallbackQueue.Update` が数えるのは、ポンプ自身の Update の開始時点の件数です（`AndroidNativeCallbackQueue.cs:168-179`）。実行順でポンプより前に走る利用者の Update から呼ぶと、同じフレームのうちに配送されます。公開 XML の「after the method has returned」は正しいので、直すのは設計の文言だけです（Share 設計の 5.3 も同じ文言です）
- 提案: 「呼び出しの後（同じフレームのことがある）」に直す

**S:C-4（C / low）2a のテスト名が中身より強い**
- 場所: `AndroidClipboardManagerEditorTests.cs:229`（`Change_ASubscriberThatThrows_IsLoggedAndOthersStillRun`）
- 根拠: マルチキャストの `Invoke` は、例外を投げた購読者より後の購読者を呼びません。確かめているのは、例外が `LogError` で握られることと、先に登録した購読者が 1 回呼ばれることだけです（1.x と同じ振る舞い）
- 提案: 名前を `..._IsLoggedAsAnError` にする

**S:C-5（C / low）csharp.md の入口のログ**
- 場所: `AndroidClipboardManager.cs:648`（`EnsureChangeListener`）、`:674`（`StopIfObserving`）、`:711`（`OnChangedCore`）
- 根拠: internal なのに入口のログがありません。公開メソッドの 1 行目も null の検査です（設計 5.4 と Share の前例どおり）
- 提案: `EnsureChangeListener` と `StopIfObserving` には入口のログを足す。`OnChangedCore` は変更のたびに走るので、除外の理由をコメントに書く

**S:C-6（C / low）XML の「fails with the CLIPBOARD_* error」**
- 場所: `AndroidClipboardManager.cs:215-216`、`:241-242`
- 根拠: `ClipboardOperationResult` はコードを持たず、文言だけを持ちます。`CopyHtmlText` の言い回しは 1.x からのものですが、`CopyUri` の行は今回書き直したものです（file:// を足したこと自体は、Kotlin の `ALLOWED_URI_SCHEMES` と合っていて正しい）
- 提案: 「fails with the message "Invalid URI: <uri>"」などに直す

**要検証（low）`ResetOnStart` が `s_listenerAdded` を戻すこと**
- 場所: `AndroidClipboardManager.cs:127-128`、`:760-766`
- 根拠: 同じプロセスの中で `SubsystemRegistration` が 2 回走る形（Unity as a Library の作り直しなど）があると、ネイティブのリスナーは外さないので 2 つ目が登録され、`ClipboardChanged` が 2 回出ます
- Share と同じ前提で、設計 9 章 K-5 の範囲です。2 回走る形が IL2CPP で起こりうるかは要検証です

### 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○（Bridge は `AndroidClipboardCApi` で、純粋な関数と `#if` の内側の `extern` と読み取り。Manager は配送、印、受け口）
- 変更ファイル一覧との一致: ○（設計 5.1 にない `ClipboardDescriptionResult.cs` の XML の変更は、実装結果 2.2 に書かれています）
- テスト方針の網羅性: △（S:B-1。設計 7.3 の後片付けでの照合が無い）
- エラーケース全実装: ○（E-1〜E-19。層 1 で 0〜11 と想定外の値、6 つの操作と read / getDescription を全部固めている）
- 返却仕様との整合: ○
  - 1.12.0 の `errorInfoOf` の文言と一致（`SECURITY` の変化は既知の差分）
  - `INVALID_URI` は 1.x と同じく、入力のままの URI を入れる（`CopyUriUseCase` の `content.uri`）
  - `ReadFailure` のコードの写し、`isSuccess == true` のとき `ErrorMessage` が null になることも確認

設計との突き合わせの要点:
- J-C1〜J-C17 と 5.4 の流れ（null の検査、`EnsureCreated`、ログ、Editor、ネイティブの確認、try / finally での解放、`Post`）は、コードと一致しています
- 設計と違う所のうち、実装結果 1.2 に書かれているもの: AC-33 からライブラリ名の照合を外したこと（K-6）、MIME の NULL を `""` にしたこと、Editor の既定の関数
- 1.2 に書かれていないもの:
  - S:B-1（後片付けでの照合）
  - 設計 5.1 の「リスナーのハンドル」を bool（`s_listenerAdded`）で持ったこと（実害なし）

### プロジェクトルール適合チェック

- common.md 準拠: ○
  - 受け口は印を読んで `Post` するだけで、Unity のオブジェクトに触らない（印を読むことは「Clipboard の形」に書いてある）
  - `UnityMainThreadDispatcher` は使っていない。`EnsureCreated` を `Awake` と各メソッドで呼んでいる
  - P1〜P5 は ○:
    - 新規 3 ファイルと型すべてに `Android` の接頭辞がある
    - テストのガードが型と一致している
    - 名前空間 `JonghyunKim.NativeToolkit.Runtime.Android.Clipboard` と置き場所が合っている
    - 差分に Android 以外の OS のファイルが無い
    - `Runtime/Android/Common` への変更は、横断の `s_state` だけ
    - クラスのガード（`UNITY_ANDROID || UNITY_EDITOR`）と P/Invoke のガード（`UNITY_ANDROID && !UNITY_EDITOR`）が二重になっている
    - 既知の逸脱を前例として引いていない
- csharp.md 準拠: △（S:C-5）
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○（S:A-1 と S:A-2 を除く）
  - Android の main と UnityMain が互いに待つ形は見つかりませんでした:
    - `lock` の中の `ntk_clipboard_add_change_listener` は、`Accept` が投稿するだけで待たない
    - `DeliverOnMain` は表のロックの外で受け口を呼ぶ（`Registry.h:116-121`）
    - 受け口は C# のロックを取らない
    - `OnDestroy` の F-10 も投稿するだけ
  - 別のスレッドから呼んだときの JNI のアタッチは、native-toolkit が `pthread_key` でスレッドの終了時に外す（`Jni.cpp:14-29`）。AC-24 と AC-25 が実機で通っている
  - `_instance` を読むのは UnityMain（`Deliver`、`RaiseChanged`）だけ
  - `s_state` は `volatile int` で、0 を未確認とする符号化が正しい
  - メモリ: 確保はすべて一覧に入り、`finally` の `FreeAll` で解放される。読み取りのハンドルも `finally` で `_free`。例外の経路でも漏れない
- 既存 API 互換性: ○（公開のシグネチャ、イベント、定数、既定値は変わらない。変わった振る舞いは 5.3 と XML に書かれている。`AndroidClipboardJsonBuilder` は public のまま）

### テストカバレッジ

カバーできている観点:
- 層 1（24 本）: options の配置、コールバックの形、空白のラベル（U+3000 を含む）、null を `""` で渡すこと（配列の順を含む）、0〜11 と想定外の値の文言とコード（全操作）、`IsRequestDefect`、読み取りの正規化（分類の -1 と 0）
- 層 2a（18 本）: Editor の 10 メソッド、呼び出しの中では出ないこと、per-call、null、届け先、無効にした Manager、印と受け口、購読者の例外、2 つ目の Manager と印の 4 つの組み合わせでの F-10、登録の 1 回とやり直し
- 層 2b（40 本）:
  - AC-32: 印を外すと落ちる
  - AC-34: `StopObserving` と `OnDestroy` の F-10 を外すと落ちる
  - AC-33: `extern` の過不足と署名を見る
  - AC-20: 結果の二重の配送で落ちる
  - AC-21a: リスナーの二重登録で落ちる
- 件数（24 / 18 / 40）と、実装結果 5 章の数（1278 − 39 + 24 = 1263）は合っています

不足している観点（壊しても落ちない所）:
- S:A-1 の、配送待ちの変更が `StopObserving` の後に出ること
- `Initialize` の登録の配線（K-4 の記述の訂正）
- `StopIfObserving` のネイティブの確認の分岐
- `LogFailure` の重さの配線
- 後片付けでのイベントの照合（S:B-1）
- 起こせない失敗（E-4〜E-6、E-10〜E-18）は、設計どおり層 1 の文言とコードレビューで押さえています

### 総合評価

**要修正（軽微）**
- A の件数: **2 件**（S:A-1 は medium〜low、S:A-2 は low）。どちらも数行で直ります
- S:A-1 は、XML の記述を直す形で対応するなら C に落ちて、A は 1 件になります
- S:A-2 は、起こしにくい経路なので、直さないなら残件として実装結果に理由を書いてください
- 推測を含む項目は 1 件で、`ResetOnStart` の項に「要検証」と付けました

---

## 2. Codex

X:A-1
Class: A
File: Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Clipboard/AndroidClipboardManager.cs:282
Evidence: `CopyMultipleText` calls `AndroidClipboardCApi.AsPassed(payload.texts)` before it enters `Invoke`. That call allocates a new array outside `Invoke`'s `try`. If the allocation throws `OutOfMemoryException`, the exception reaches the caller. Design sections 5.3 and 5.4 and result report section 3.4 say a C# allocation exception after the move must come back as an asynchronous failure carrying `ex.Message`. The other copy operations do their unmanaged allocation inside `Invoke`'s `try`. This is the only operation outside that contract.
Fix: Run `AsPassed(payload.texts)` inside `Invoke`'s `try`. Add a seam that can make the preparation step throw. Add a test that checks the exception does not escape and that the failure reaches the common event and the per-call callback once each.

X:B-1
Class: B
File: Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs:900
Evidence: AC-33 only checks that the `_content_free` and `_description_free` declarations exist. `Clipboard.h:81-105` requires the caller to free the handles explicitly. `Clipboard.cpp:334-352,401-415` returns heap-allocated handles. The manager frees them correctly today, in a `finally` at AndroidClipboardManager.cs:358 (Read) and :441 (GetDescription). If either free were deleted, every test would still pass, so a native memory leak would go unnoticed. Freeing when an exception is thrown partway through a read is also untested.
Fix: Add an internal helper or test seam that takes the reader and free delegates. Test that free is called exactly once, both on success and when conversion throws, for content and for description separately.

X:B-2
Class: B
File: Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidClipboardManagerEditorTests.cs:324
Evidence: `Listener_AFailedRegistrationIsTriedAgain` calls `EnsureChangeListener()` twice directly. It never goes through the public path `StartObserving` (AndroidClipboardManager.cs:481). If the retry wiring in `StartObserving` were removed, the test would still pass. The device test only covers the case where the first registration succeeds. Nothing catches the E-16/J-C9 path, where a registration that failed in `Initialize` is retried from `StartObserving`.
Fix: Split the start logic that runs after the platform check into an internal core. Add a seam where registration returns failure and then success. Test that a `StartObserving`-equivalent call registers again after an `Initialize`-equivalent failure.

X:B-3
Class: B
File: Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidClipboardManagerEditorTests.cs:139
Evidence: The Editor tests only reach the path that returns at the platform check. The device test only covers F-9 success. Deleting `s_observing = false` at AndroidClipboardManager.cs:492 or :498 would not make any test fail. A regression where the manager keeps accepting changes after the start request fails (by exception or non-`NONE` code) would go unnoticed.
Fix: Add a seam for `ntk_clipboard_start_observing`. Test the exception, `UNKNOWN` and `NONE` cases, checking the observing flag and the call count in each.

X:C-1
Class: C
File: Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Clipboard/AndroidClipboardManager.cs:307
Evidence: The XML comment says no change fires after this call in either case. But `OnChangedCore` may already have queued `RaiseChanged` (AndroidClipboardManager.cs:711) before the stop. `RaiseChanged` does not recheck the flag, so it still fires after `StopObserving` clears it. Design 5.3 only guarantees that changes arriving at the native entry point after the stop are dropped.
Fix: Narrow the comment: "A change that reaches the native entry point after this call is dropped. A change already queued for UnityMain can still be delivered." If a full ban is intended, recheck the flag in `RaiseChanged` too.

X:C-2
Class: C
File: Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs:39
Evidence: The class comment names `design-v3.md` as the design, but the reference design and the result report use `design-v4.md`. Lines 59-61 describe delivery as `UnityMainThreadDispatcher`, which has been removed. The implementation uses `AndroidNativeCallbackQueue`.
Fix: Update the design reference to v4. Change the delivery mechanism description to `AndroidNativeCallbackQueue`.

Count of A: 1

Note: this review was produced by Codex. I did not check the findings against the code myself.

---

## 3. 直したこと（取りまとめ役）

| 指摘 | 区分 | 対応 |
|---|---|---|
| S:A-1、X:C-1 | A | (a) を採った。`RaiseChanged` の先頭でも印を見る。`StopObserving` の XML の「この呼び出しの後は出さない」が、配送待ちの変更を含めて正しくなった。2a に `Change_QueuedBeforeStopObserving_IsNotRaisedAfterIt` を足した |
| S:A-2 | A | `StartObservingCore` が、失敗したときに印を「呼ぶ前の値」に戻す。2a の `Start_ASecondStartThatFails_KeepsTheFirstObserving` |
| X:A-1 | A | `CopyMultipleText` の `AsPassed(payload.texts)` を `Invoke` の `try` の中（呼ぶ関数の中）に移した。確保の失敗を起こす継ぎ目は作らない（コードレビューで押さえる） |
| X:B-1 | B | 読み取りのハンドルの解放を `ReadAndFree`（読む関数と解放の関数を受ける）にまとめた。2a で、読めたときと読む関数が投げたときの両方で、解放が 1 回だけ呼ばれることを確かめる。`Read` / `GetDescription` がそれを使う配線はコードレビュー |
| X:B-2、X:B-3 | B | `StartObserving` のうち、Editor とネイティブの判定の後を `StartObservingCore` に切り出し、F-9 を差し替えられるようにした（`SetNativeForTests` の 3 つ目の引数）。2a で、受け付け・拒否・例外・リスナーが無いとき・`Initialize` 相当の登録の失敗の後のやり直しを確かめる |
| S:B-1 | B | 層 2b の後片付けで、すべてのイベントが UnityMain で出たことを照らす。イベントの数の照合は足していない（後片付けの `StopObserving` と `Clear` の結果があり、テストごとの期待の数を持たせるほどの価値が無い。AC-20 が二重の配送を見る） |
| S:B-2 | B | `Initialize` の登録の配線、`StopIfObserving` のネイティブの判定、`LogFailure` の重さの配線は、コードレビューで押さえる（実装結果 v2 の 1.2） |
| S:C-1 | C | `StopObserving` の XML に、プロセス全体の監視を止めること（Unity が始めていなくても）を書いた。振る舞いは変えない |
| S:C-2、X:C-2 | C | 層 2b のテストファイルのコメントを、設計 v4 と C ABI の経路に合わせた |
| S:C-3 | C | 設計 5.3 の「どれも次のフレーム以降」は、正しくは「呼び出しの後（取り出し口より前に走る `Update` から呼べば同じフレームのことがある）」。実装結果 v2 の 3.4 に書いた（設計の新しい版は作らない） |
| S:C-4 | C | テスト名を `Change_ASubscriberThatThrows_IsLoggedAsAnError` にした |
| S:C-5 | C | `EnsureChangeListener`、`StopIfObserving`、`StartObservingCore` に入口のログを足した。`OnChangedCore` は変更のたびに走るので入れない（コメントに書いた） |
| S:C-6 | C | `CopyHtmlText` と `CopyUri` の XML を、結果の文言で書いた |
| 要検証（`ResetOnStart`） | - | 設計 9 章の K-5 と同じ前提。実装結果 v2 の 7 章の残件に書いた |
