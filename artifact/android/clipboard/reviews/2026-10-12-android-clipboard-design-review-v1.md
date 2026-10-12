# レビュー結果

- 日付: 2026-10-12
- 対象ファイル: artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v1.md
- 機能名: clipboard
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 1）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない）
- A1: サブエージェント 1 件（R1-1）、Codex 2 件（X1-1、X1-2）。R1-1 と X1-1 は同じ指摘

---

## 1. サブエージェント（Claude）

対象: `/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v1.md`（読むだけで、どのファイルも変えていない）

### 範囲と照らした一次資料

- 規則: `agent-rules/workflows/review-document/workflow.md`（手順 4・8）、`agent-rules/coding-rules/common.md`（Android C ABI の節、Share の例外）、`csharp.md`、`testing.md`、親課題 `artifact/topics/android-c-abi-2/README.md`（J-9、J-11）
- 今の C#: `Runtime/Android/Clipboard/` の 7 ファイルすべて、`Runtime/Android/Common/AndroidNativeToolkitCApi.cs`、`AndroidNativeCallbackQueue.cs`、`Runtime/Common/UnityMainThreadDispatcher.cs`、`Runtime/Android/Share/AndroidShareManager.cs` と `AndroidShareCApi.cs`（前例）、`Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs`、`Tests/Runtime/AndroidClipboard*.cs`、サンプルの Controller
- native-toolkit: `dist/1.13.0/.../Clipboard.h`、`Clipboard.cpp`（全体）、`ClipboardBridge.kt`、`Errors.kt`、`Utf8.kt`、`NtkRuntime.kt`、`MainPoster.kt`、`Registry.h`、`StructSize.h`、2.0.0 の `Lib` の全ファイル、`StructLayoutTest.cpp`、`ClipboardTest.cpp`、P2 8.3（CL-01〜CL-15）、申し送り 4.2、マニュアル `clipboard.ja.md:469,507`
- 1.x: タグ `1.12.0` の `UnityAndroidClipboardManager.kt` と `UnityClipboardJsonParser.kt`。`OldLib` と 2.0.0 の `Lib` は、パッケージ名を置き換えて 12 ファイルを `diff` した（違いは `ClipboardChangeMonitor` の KDoc だけ）
- 引用した行番号はおよそ 80 か所を照らした。ずれは見つからなかった（下の C は文言の正確さの問題）

### 強み

- 「1.x と同じ」と書いた行には、ほぼすべて正しい行番号が付いている。`OldParser` の `getString` / `optString`、`Json:80-86,88-96,109-111`、`Mgr` の各行、`OldBridge:205-213,262-276,297-301,321-336`、`C.cpp` と `Clipboard.h` の行、`ClipboardTest.cpp:224-240,269-271,293,345,356-366` は、どれも一次資料どおりだった
- 1.x の文字列の null が `"null"` になる見込みは、Android の org.json の振る舞い（JSON の null に `getString` / `optString` を使うと `"null"` を返す）と合う。「要確認」とし、AC-26 で実測する手順は妥当
- 構造体 `CopyOptions`（24 バイト。位置は 0 / 4 / 8 / 16 / 20、Pack 8）、`extern` 26 本の型、`out` と `ref`、NULL と大きさ 0 の区別、`finally` での `_free` は、ヘッダー・`StructLayoutTest.cpp:18-23`・`C.cpp` の読み取り関数と合う。`count == 0` のときも `AllocStringArray` が NULL でないポインタを渡すので、入口の `EMPTY_ITEMS` に届く
- スレッド: 変更の受け口は `volatile` の印を読んで `ConcurrentQueue` に積むだけ。登録の `lock` を Android の main が取る経路は無い。`DeliverOnMain` は registry の mutex の外でコールバックを呼ぶ（`Registry.h:116-121`）。どの `ntk_clipboard_*` も main を待たない。**UnityMain と Android の main が互いに待つ形は見つからなかった**
- 監視がプロセスに 1 つで共有されること（`ClipboardObserver.kt`、マニュアル 469・507 行）を正しく捉えている。印 `s_observing` によって「`StartObserving` の前には `ClipboardChanged` を出さない」という XML の約束を保つ判断は筋が通っている
- 起こせない失敗（E-4〜E-18）を、文言は層 1、形はコードレビューに分けている。自動化の前提（8 章）が「なし」も含めて書かれている

### 改善点

#### A1（実装フェーズで気づけない）

**R1-1 / A1 / medium: `StartObserving` の直後に同じ流れでコピーすると、その変更が届かないことがある（1.x は順序が決まっていた）。AC-21 はこれを見逃す**

- 該当: 5.2 J-C1・J-C10・J-C11、5.3（行が無い）、7.3 AC-21
- 根拠:
  - 1.x は `startObserving` も copy も、同じ `mainHandler` に `post` していた（`OldBridge:262-265`、`297-301`、`363-365`。UnityMain は main looper ではない）。このため start → copy の順で走り、変更は必ず届いた
  - 2.0.0 では F-9 が `MainPoster.post` で積むだけ（`ClipboardBridge.kt:161-171`）。copy は呼んだスレッドで `setPrimaryClip` までその場で走る（`ClipboardBridge.kt:66-72`、`ClipboardRepositoryImpl.kt:24-28`）。システムのリスナーは main で `addPrimaryClipChangedListener` されてから有効になる（`ClipboardChangeMonitor.kt:37-52`）。2 つの Binder 呼び出しのどちらが先に system_server に着くかで決まる、本物の競合になる
  - AC-21 の「1 回以上」は、Android 12 以降の分類の後の 2 回目の報告（`ClipboardTest.cpp:269-271`）で通ってしまう。URI は 1 回しか報告されない（`ClipboardTest.cpp` の `AUriCopyReachesEachListenerExactlyOnce`）ので、URI のコピーだと変更が 1 回も来ないことがありうる
- J-11 の「UnityMain から 1 回ずつ正しい入力で呼んだときの時点」に当たる
- 提案:
  1. 5.3 に行を足す。`StartObserving` の XML にも書く（「監視は Android の main で非同期に始まる。呼んだ直後〈同じフレーム〉の変更は届かないことがある」）
  2. AC-21 を分ける
     - 21a: `StartObserving` の後、数フレームか 0.5 秒待ってから、テキストをコピーする。1 回以上を期待する
     - 21b: `StartObserving` と `CopyUri`（`content://`）を同じフレームで呼び、回数を記録する。1.x は 1 回の見込み。2.0.0 は 0 か 1 で、値では落とさない
  3. C# の側で順序を保つ手段は無い（main の完了を待てない。A-4）。なので文書化で受けるのが妥当

#### A2（実装フェーズで気づける）

- **R1-2 / A2 / low**: 4.2 の「`texts` の要素の null は `""`」は、`AllocStringArray` に渡す前に要素ごとに `AsPassed` を通さないと成り立たない。`AllocUtf8(null)` は `IntPtr.Zero` を返し、入口が `INVALID_PARAMETER` で拒む（`AndroidNativeToolkitCApi.cs:88`、`C.cpp:273`）。AC-26 の要素のケースが実機で落ちるので気づける。実装結果のチェックリストに書く
- **R1-3 / A2 / low**: `ReadFailure` が `EMPTY_CONTENT` / `EMPTY_ITEMS` / `INVALID_URI` を受けたときのコードと文言は「起きない」とだけあって未定義（4.4）。7.1 で「0〜11 の全値」を層 1 で固めるので、実装の時点で決まる

#### B（検証手段）

**R1-4 / B / medium: Manager を壊して作り直した後の監視を、実機で確かめるテストが無い**

- 該当: 7.3、J-C9・J-C12
- 根拠: J-C9（登録は外さない。ハンドルは static）、J-C12（OnDestroy で印を下ろし F-10）、`_instance` への配送の組み合わせ。workflow の「気づけない例」と同じ種類（登録・解放の条件の誤りで、機能が静かに止まる）。層 2a は Editor で登録しない。7.3 はこの順序を通さない
- 提案: AC-31 を足す。手順は「`StartObserving` → Manager を `Destroy` → `Instance` で作り直す → `StartObserving` → コピー」。期待は新しい Manager に `ClipboardChanged` が 1 回以上。1.x でも同じ結果の見込み（`OldBridge:97-111` の差し替えと停止）

**R1-5 / B / low: 印 `s_observing` が実機で本当に門になっていることを、2b が確かめていない**

- 該当: 7.3 の AC-22・AC-23
- 根拠: AC-23 は誰も監視を始めていないので、AC-22 は F-10 が監視そのものを止めるので、どちらも印の判定が無くても通る。層 2a の `OnChangedCore` のテストは単体では押さえている
- 提案: C ABI の版だけのテスト（`OnCApi`）を 1 本足す。`AndroidClipboardCApi.ntk_clipboard_start_observing()` を直接呼んで「ほかの利用者」をまねる。コピーして `ClipboardChanged` が来ないことを確かめ、最後に `ntk_clipboard_stop_observing` で後片付けする

**R1-6 / B / low: 網羅の穴**

- AC-26 に `plainText` の null が無い（4.2 では同じ扱いに入れている）
- `Read` の `ClipContents.Label`（`""` と null の正規化）を実機で見るケースが無い（AC-17・AC-18 は `GetDescription` だけ）
- 記録して先へ進めてよい

#### C（記述の整合と正確さ）

- **R1-7 / C / low**: 5.3 の「logcat の中身」の行が誤っている
  - 2.0.0 の列は、コピーのラベルをそのまま出すことを差分のように書いている
  - しかし `OldLib` は 2.0.0 と同じコードで、1.x でもユースケースとリポジトリがラベルを出していた（`CopyUriUseCase.kt:24`、`ClipboardRepositoryImpl.kt:63-74`。`diff` で同一）
  - 差分は `Lib/AndroidClipboardManager.kt` の行が増えたことだけ
- **R1-8 / C / low**: 4.4 の `INVALID_PARAMETER` の行の「正しい実装では起きない」は厳密でない
  - Kotlin の `IllegalArgumentException` も `Errors.common` で `INVALID_PARAMETER` になる（`Errors.kt:26-32`、`ClipboardBridge.kt:50`）
  - 実害は、ログが `LogError` になることと、文言が `Failed: INVALID_PARAMETER` になることだけ。注記と 5.3 の「UNKNOWN などの文言」の行に足せば足りる
- **R1-9 / C / low**: 5.3 の「監視の共有」の行は、逆向きを書いていない。Kotlin の利用者が `stopObserving()` を呼ぶと、Unity の印は真のままでも `ClipboardChanged` が止まる
- **R1-10 / C / low**: J-C16 の「`volatile int`。未確認を 0」は、`NativeState.Available == 0`（`AndroidNativeToolkitCApi.cs:48-58`）とぶつかる
  - そのまま書くと、`Available` がキャッシュされず、毎回 `ntk_version` を呼ぶ。観測できる振る舞いは変わらない
  - 「状態 + 1 を入れる」などと符号化を明記する
- **R1-11 / C / low**: 5.1 の XML の更新の一覧が足りない
  - 足すもの: クラスの要約（「Wraps UnityAndroidClipboardManager via AndroidJavaObject」）、`Initialize()` の要約、`StopObserving` の成功の意味（積めた）、`ClipItem` の「JSON keys」の言い回し
  - 5.3 は「XML コメントに書く」としているので、方針とは合っている
- **R1-12 / C / low**: 細かい点
  - 4.4 の「HasClip はログは同じ列」に従うと、C# の例外は `LogError` になる。1.x は `LogWarning` だった（`Mgr:297`）
  - 1.1 の F-7 の「0 にしない」は、入口で `*out_has_clip = 0` にする（`C.cpp:362`）ことと読み違えやすい
  - J-C7 の「null 要素を `""` にする」は、`common.md` の「NULL の要素を含む配列は Manager が先に検査する」の Clipboard 版の言い替えになる。`common.md` を更新するときに、その旨を一言書く

### 不足項目

- 5.3: `StartObserving` の直後の変更の競合（R1-1）、Kotlin の利用者の停止が Unity に効くこと（R1-9）
- 7.3: 作り直した後の監視（R1-4）、ほかの利用者が監視しているときの門（R1-5）、`plainText` の null と `Read` の `Label`（R1-6）
- 要検証: Unity as a Library で `Application.Unload()` した後も、リスナーを外さないので（J-C9）、ほかの Kotlin の利用者の監視による変更が managed の受け口に入りうる。Share の J-S6 と同じ前提なので、ここで直す必要は無い。持ち越しとして記録するかの判断材料にとどめる
- 確かめたこと（指摘ではない）
  - 2.0.0 の Kotlin は Activity ではなく Application の Context を使う。それでも、クリップボードの読み取りの可否は UID とフォーカスで判定される見込みなので、1.4 の結論（1.x と同じ）は変わらない（要検証の度合いは低い）
  - AC-14 の「存在しない authority」は、C ABI のテストが同じ形で通っている（`content://com.jonghyunkim.capitest/once`）ので実行できる

### 総合評価

- **A1: 1 件**（R1-1）、A2: 2 件、B: 3 件、C: 6 件
- 移行の骨格は筋がよく、一次資料と合っている。同期の C を呼んで取り出し口で後に出すこと、`_instance` への配送、外さないリスナーと static の印、検査を Kotlin に任せることがそうで、「1.x と同じ」の根拠と行番号の正確さも高い
- A1 は J-C1（同期化）と監視の非同期の開始が組み合わさって生まれる時点の差で、AC-21 が Android 12 の 2 回目の報告で隠してしまう。これを 5.3、XML、AC-21 の分割で受けて再レビューすれば、止める基準（A1 が 0）に届く見込み
- R1-4（作り直した後の監視）は中心の契約に近いので、B の中でも直すことを勧める。ほかの B と C は記録して先へ進めてよい

---

## 2. Codex

### X1-1 — A1

- 設計箇所: 5.2 J-C10/J-C13、5.4、7.3 AC-21(設計書 262、265、327、427行)
- 欠陥: Unity main から `StartObserving(); CopyPlainText(...)` と連続して呼ぶと、移行後は監視開始前に copy が完了し、`ClipboardChanged` を取りこぼし得ます。この 1.x との差が 5.3 にありません。`s_observing = true` は、ネイティブの system listener 登録が完了したことを保証しません。AC-21 もスケジューリング次第で通るため、この欠陥を安定して検出できません。
- 根拠:
  - 2.0.0 の start は Android main への post を受け付けるだけです。
    - /Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Clipboard/Clipboard.cpp:423
    - /Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/java/com/jonghyunkim/nativetoolkit/capi/jni/ClipboardBridge.kt:160
    - /Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/common/runtime/MainPoster.kt:8
  - copy は呼び出しスレッドで同期実行されます。Clipboard.cpp:207
  - 1.x は start と copy を同じ main Handler に順番に積みます。`1.12.0:android/unity_android_plugin/src/main/java/android/unity/clipboard/UnityAndroidClipboardManager.kt:262-265,297-300,363-365`
- 修正:
  - 1.x の挙動を維持するなら、監視開始の完了通知か、後続 write の非同期な順序付けを設計します。同期 wait は使いません。
  - 差分を許容するなら、5.3 と XML コメントに明記します。AC-21 は、監視開始処理を通過するバリアの後に copy するよう変えます。

### X1-2 — A1

- 設計箇所: 4.4、5.3、5.4(216、271–299、310行)
- 欠陥: `OUT_OF_MEMORY` と managed 側の確保例外の扱いが 1.x から変わりますが、5.3 に記載がありません。移行後は失敗結果として配送されます。1.x では結果化されず、そのまま伝播し得ました。
- 根拠:
  - 1.x の Kotlin は `Exception` のみ捕捉し、`OutOfMemoryError` は対象外です。`1.12.0:android/unity_android_plugin/src/main/java/android/unity/clipboard/UnityAndroidClipboardManager.kt:303-311`
  - 2.0.0 は `Throwable` を捕捉し、OOM を 5 に写します。ClipboardBridge.kt:40
  - C 実装も OOM を明示的に返します。Clipboard.cpp:213、Clipboard.cpp:321
  - 現行 C# の JSON 確保は catch の対象外です。/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Clipboard/AndroidClipboardManager.cs:173
- 修正:
  - 5.3 に次を追加します。native/JNI の OOM は `Failed: OUT_OF_MEMORY` になります。managed 側の確保例外と extern 例外は、`ex.Message` を持つ非同期の失敗になります。
  - 1.x を維持するなら、managed 側の確保例外を結果化しない方針に変えます。

### X1-3 — A2

- 設計箇所: 4.1、4.2、5.1、7.1、AC-26(177、187、236、377–378、432行)
- 欠陥: `texts` の null 要素を `""` に変換すると定めていますが、実装手順は既存の `AllocStringArray` を使うとしか書いていません。そのまま使うと null は NULL ポインタになり、C 入口で `INVALID_PARAMETER` になります。AC-26 でテストが失敗します。
- 根拠:
  - `AllocUtf8(null)` は `IntPtr.Zero` を返します。/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeToolkitCApi.cs:86
  - `AllocStringArray` は各要素をそのまま渡します。AndroidNativeToolkitCApi.cs:121
  - C 入口は NULL 要素を拒否します。Clipboard.cpp:265
- 修正: `CopyMultipleText` では全要素に `AsPassed` を適用してから `AllocStringArray` に渡すと 5.4 に明記します。null から空文字列への変換と順序保持は、層1で検証します。

### X1-4 — B

- 設計箇所: 4.1、7.1(141–172、373–381行)
- 欠陥: 層1が固定するのは構造体配置と callback 引数だけです。全 extern の戻り値、引数幅、`DllImport` 属性は検証しません。たとえば `size_t` を `int` に誤宣言しても、小さい fixture では実機テストが通り得ます。
- 根拠: 正式な署名は /Users/jonghyunkim/Desktop/native-toolkit/dist/1.13.0/android/include/NativeToolkitC/Clipboard.h:58–105 です。設計 7.1 に extern の反射照合がありません。
- 修正: 全 extern について、名前、戻り値、引数型、`size_t = UIntPtr`、out handle、`internal static extern`、`DllImport("ntk", Cdecl)` を反射テストで固定します。

### X1-5 — B

- 設計箇所: 5.2 J-C9、7.2–7.3(261、385–440行)
- 欠陥: 「リスナーをプロセスで 1 回だけ登録し、失敗後だけ再試行する」という契約を検証するテストがありません。AC-21 は通知を「1 回以上」とするため、二重登録を識別できません。
- 根拠:
  - 登録ごとに別 ID が作られます。Clipboard.cpp:447
  - 複数登録には、それぞれ通知されます。/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi_test/src/main/cpp/tests/ClipboardTest.cpp:277、ClipboardTest.cpp:310
- 修正:
  - add-listener を注入可能にします。
  - 層2a で、反復 `Initialize`、`Initialize` から `StartObserving`、反復 `StartObserving` を検証します。成功後は登録 1 回、失敗後のみ再試行することを確認します。
  - 実機で重複を確認するには、通知が 1 回の URI copy を使います。

### X1-6 — B

- 設計箇所: 5.2 J-C12、7.2(264、397行)
- 欠陥: `OnDestroy` のテストは `s_observing` が偽になることしか確認しません。F-10 の呼び出しを完全に欠落させても、C# 側の受け口ゲートによって AC-22 を含むテストが通ります。
- 根拠:
  - F-10 は stop 処理を Android main に post します。Clipboard.cpp:442
  - 実際の system listener 解除は次の箇所です。
    - /Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/clipboard/presentation/ClipboardObserver.kt:35
    - /Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/clipboard/presentation/ClipboardChangeMonitor.kt:57
- 修正: stop 呼び出しを注入可能にします。層 2a で、live と duplicate の Manager、flag の true と false の各組合せについて、F-10 の呼び出し回数を固定します。

### X1-7 — C

- 設計箇所: 5.3 logcat 差分(295行)
- 欠陥: 「2.0.0 ではコピーのラベルをそのまま出す」を移行差分としています。しかし 1.x ライブラリもラベルを平文で記録していました。
- 根拠:
  - 1.x: `1.12.0:android/android_library/src/main/java/android/library/clipboard/data/repository/ClipboardRepositoryImpl.kt:24-25,59-73`
  - 2.0.0: /Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/clipboard/AndroidClipboardManager.kt:40–72

- 修正: 「両版ともラベルは平文。差分は、1.x bridge が失敗 message を出したのに対し、2.0.0 Bridge は例外クラス名だけを出す点」と訂正します。

補足（取りまとめ役の転記）: Codex の出力は、Codex を呼んだ転送役のエージェントがそのまま返したもの。転送役は一次資料を確かめ直していない。

A1件数: 2
