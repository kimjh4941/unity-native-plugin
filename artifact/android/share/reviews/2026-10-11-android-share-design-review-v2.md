# レビュー結果

- 日付: 2026-10-11
- 対象ファイル: artifact/android/share/designs/2026-10-11-android-share-design-v2.md
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 2。ラウンド 1 とは別のインスタンス）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない。Codex の ID は統合のときに付けた）

---

## 1. サブエージェント（Claude）

## 強み

- v1 の A1 の多くが直っている。具体的には次のとおり。
  - per-call の枠を取り出す時点（J-S5）。Editor で同じ操作を同じフレームに 2 回呼んだときに、それぞれが自分の失敗を受けることも含む。
  - Editor の取り消しは失敗を返す（J-S13）。
  - 表示する Chooser Action の重複・SEND・API 33 以下・null・U+0000・サロゲートの扱いと、5.3 への記載。
  - `user_data` を表に入れる時点、`[MonoPInvokeCallback]`、選択の文字列の `finally`、リスナーを 1 回だけ登録すること、`AllocBytes` の置き場所（P4）。
  - テストの共有先を、既存の androidlib を使ってアプリ自身にすること。
- C ABI の事実を一次資料で照らし直し、一致を確かめた。
  - 構造体の大きさと位置: StructLayoutTest.cpp:118-141、Share.h:52-81
  - 入口の検査の順: Share.cpp:319-328
  - `out_request_id` を先に 0 にすること: Share.cpp:453
  - 取り消しの意味: ShareBridge.kt:199-212、Share.h:103-104
  - AP-7 の世代: ShareRepositoryImpl.kt:49-57。アイコンを確かめた後、`nextGeneration` で世代を進める
  - Chooser Action の通知: 世代と ID は data の URI に入る（ShareChooserActionReceiver.kt:74-88）
- 1.x の文言の表（2.3）と、null が文字列 `"null"` になる推定（4.2、5.3）は、1.12.0 の parser と `AndroidShareJsonBuilder` に照らして正しい。
- J-S17 の世代の比較（「生きている世代 == 最新の世代」）は、Android の main とほかのスレッドが競合しても、誤って届けることは起きず、取りこぼす側にしか倒れない。画像・ファイルの Share と F-8 が世代を変えないことも、1.x の registry と合っている。
- P1〜P5 の違反は見つからなかった。
  - 新しいファイル名はすべて `Android` で始まる。
  - 置き場所と名前空間は `Runtime/Android/Share`。
  - 二重のガードになっている。
  - 既存変更に入っているのは Android のファイルだけ。
  - `AllocStringArray` を共通部に移す理由は、ヘッダーを実際に使う機能（Dialog、Clipboard、Notification）に基づいている。
- 機械照合（`scripts/check_design_consistency.py`）はすべて OK だった（SKIP を除く）。

## 改善点

### 高優先度

**R2-1 Editor で不正な payload を渡したときの結果が、1.x の `could not be started.` から検査の文言に変わる**
- 節: 5.4 の手順 4・5、J-S3、7.2「検査の失敗」、5.3 の Editor の行
- 問題:
  - 1.x の Editor（Android ターゲット）は、JSON を組んだ後で `TryPrepareCall` が偽になった。そのため、本文が空でも `filePaths` が空でも、9 操作すべてが `<op> could not be started.` を返した。検査は Kotlin の parser にあり、Editor からは届かない。
  - 計画は検査（手順 4）を Editor の判定（手順 5）より先に置く。このため、Editor で `Failed to shareText: text is required` などが返る。
  - common.md:74 は「Editor では、その機能の 1.x の契約を保つ」と定めている。Dialog が検査を先にしたのは、1.x の Dialog がそうだったからで、Share の 1.x は違う。
  - 7.2 はこの新しい振る舞いを層 2a で固めるので、テストは通る。
  - 起きる条件: 開発中に Editor で、空の本文などを渡して呼ぶ。ふつうに起きる。
- 根拠: AndroidShareManager.cs:329-337、:353-362、common.md:74、計画 5.4:447-448、7.2:557、5.3:434
- 区分: A1
- 修正案: どちらかを決めて書く。
  - (a) 1.x に合わせる: 引数の null の検査と `EnsureCreated` の直後に、Editor・Android 以外の判定を置く。検査の文言は層 1（純粋な関数）と層 2b で確かめ、7.2 の「検査の失敗」は外す。
  - (b) 今の順を保つ: 5.3 の Editor の行に「不正な payload は Editor でも検査の文言」を足し、common.md:74 の例外として 5.1 に書く。
  - (a) を勧める。Editor の契約を変える理由が、テストのしやすさしかないため。

**R2-2 Sharesheet が出ている間は Unity の Player が止まる見込みで、7.3 と 8.3 の手順がそのままでは進まない**
- 節: 1.9、7.3、8.2、8.3
- 問題:
  - Sharesheet は別の Activity なので、出るとアプリの Activity は `PAUSED` になる（1.9 の「started のまま」は正しいが、`RESUMED` ではない）。Unity は `onPause` で Player のループを止める（`OnApplicationPause(true)`。権限のダイアログのときと同じ。要確認）。
  - テスト側の `AndroidUiDriver.Do` はコルーチンで、フレームを進めながら答えのファイルを待つ。Player が止まっている間は、次の命令を出すことも答えを読むこともできない。
  - そのため、7.3 の「開く → 出ているかを数える（8.3 の `count package=`）→ Back」を命令の列として書くと、2 つ目の答えを待つところで止まり、Back を出せない。
  - 7.3 の「`CancelPendingShareCallback` の後に選んでも来ない」も、Sharesheet が出ている間は C# から取り消しを呼べない。
  - Dialog はアプリの Activity の上に出る窓なので、この問題は無かった。補助の設計 v5 の V-8 が扱うのは `background`（STOPPED）だけ。
- 根拠: AndroidUiDriver.cs:68-92（`Do` はフレームで待つ）、:94-109（Player が止まっている間に別スレッドで待つ `WaitForAnswerFile` は `background` 用）、android_ui_driver.py:411-420（`back` はセレクタが出るのを待ってから押す）、:429-431（`count` は待たない）、補助の設計 v5:123、:215、計画 1.9:156、7.3:570・573、8.3:607-608
- 区分: B（中心の検証手段）
- 修正案:
  - 8.2 の前提に「Sharesheet が出ている間、Unity は止まる（要確認。5.5 の手順 2 で確かめる）」を書く。
  - Sharesheet への操作は、開く呼び出しと同じフレームで出す、それ 1 つで完結する命令にする。
    - 出たことを確かめて閉じる: `back package=<com.android.intentresolver>`（今の `back` で足りる）
    - 選ぶ: `tap-in-sheet`（出るのを待ち、探して押す）
    - `count package=` は、閉じた後に何も残っていないことの確認にだけ使う。
  - 取り消しのテストは、`ShareWithCallback` の直後、同じフレームで `CancelPendingShareCallback` を呼ぶ（F-9 は main で、開く処理の後に動く。1.5）。その後に `tap-in-sheet` で自分を押す。
  - この形を、UI テスト計画 v1 の共通の約束に書く。

### 中優先度

**R2-3 取り消し用の ID を「大きい方の 1 つ」だけ持つと、開けなかった `ShareWithCallback` の後に、前の待ちを取り消せない**
- 節: J-S12、J-S13、J-S15、5.3「複数のスレッドから同時に `ShareWithCallback`」
- 問題:
  - F-8 の戻り値が NONE でも、完了が `NOT_FOREGROUND` で来ることがある。このとき Kotlin は新しい印を作らず、`selectionIds` も変えないので、ネイティブには前の待ちが残る。P2:41 も「印を作る前の失敗では前の要求の待ちが残る」「利用者には値で分ける」と書いている。
  - 計画は NONE が返った時点で ID を新しい（大きい）方に置き換える。そのため、後の `CancelPendingShareCallback` と `OnDestroy` は、開けなかった要求の ID で F-9 を呼び、何も取り消さない（帳簿に無い ID は無視される）。前の待ちが残り、後でアプリが選ばれると、取り消した後なのに `ShareCallbackReceived` が出る。
  - 1.x の取り消し（`cancelPendingCallback()`、`clearShareOperationListener`）は、ID を使わず今の待ちを外していた。
  - 起きる条件: Sharesheet A を出したまま後ろに回る → ほかのスレッドから `ShareWithCallback` B → `NOT_FOREGROUND` → 取り消し → 前に戻って A で選ぶ。
- 根拠: ShareBridge.kt:88-91、:192-196、:203-205、Share.h:103-104、P2:41、1.12.0 の ShareCallbackCoordinator.kt:151-155、UnityAndroidShareManager.kt:104-111、:309-315、計画 J-S12:401、J-S13:402、J-S15:404
- 区分: A1（起きる条件は狭い）
- 修正案:
  - ID を 1 つではなく集合で持つ。中身は「NONE で受け付け、まだ取り消していない要求の ID」（`lock` の中で扱う）。取り消しと `OnDestroy` は、集合のすべてに F-9 を呼んでから空にする。F-9 は今の待ちの ID でなければ何もしない（Share.h:103-104）ので、どれが今の待ちかを C# が知る必要は無い。
  - 集合は、完了がまだ来ていない ID と、最後に成功した ID だけを残して小さく保つ。
  - これで 5.3 の「複数のスレッド」の行も要らなくなる。
  - F-9 に渡す ID の一覧を返す処理を純粋な関数にし、層 1 で固める。

**R2-4 Chooser Action の世代を進める時点が決まっておらず、「呼ぶたびに」だと検査で失敗した後に前のアクションが止まる。`ShareText` と `ShareWithCallback` の手順も無い**
- 節: J-S17、J-S7、5.4（`ShareFiles` しか無い）
- 問題:
  - 1.x の `shareText` は、parser（本文の空白の検査）を通った後に `registry.register`（前の受け手を外す）を呼んだ。parser で失敗すると前の受け手が残った。
  - 一方、C# の `_chooserCoordinator.Register(onChooserAction)` は、JSON を組む前に呼ばれていた。そのため、前の Share のタップは新しい per-call に届いた。`chooserActions` の要素が null で起きる NRE も、登録の後、Kotlin を呼ぶ前なので同じ扱いになる。
  - 計画は「`ShareText` を呼ぶたびに最新の世代を 1 進め」とし、「失敗した後 … 前のアクションを捨てる（1.x と同じ）」と書く。これだと C# の検査の失敗でも前のアクションが止まる。
  - coordinator に登録する時点（検査の前か後か）と、`onSelected` を入れる時点も書かれていない。
  - 起きる条件: 前の Sharesheet が出ている間に、ほかのスレッドから呼ぶ（UnityMain は止まっている。R2-2）。狭いが、順が決まっていないことが同じ種類の差を生む。
- 根拠: 1.12.0 の UnityAndroidShareManager.kt:145-167、ShareChooserActionReceiverRegistry.kt:43-49、AndroidShareManager.cs:207-209、:314、計画 J-S17:406
- 区分: A1（軽い）
- 修正案:
  - 世代を進めるのは、1.x の `register` に当たる時点にする。つまり、C# の検査、Editor の判定、ネイティブが使えるかの確認を通った後、`extern` を呼ぶ直前。
  - coordinator への登録は 1.x と同じく、J-S7 の null の検査の後、本文の検査の前にする。
  - `ShareText` と `ShareWithCallback` の手順を、5.4 と同じ細かさで書く。並びは次のとおり。
    - null の検査、`EnsureCreated`、警告、coordinator の登録と `onSelected` の設定、検査、Editor の判定、ネイティブが使えるかの確認、`NormalizeActions`、世代、`AddCall`、`extern`、ID の保存。
  - 層 2a の世代のテストは、R2-8 の継ぎ目を使う形にする。

**R2-5 J-S4: 破棄の前にできた結果を捨てるのは、1.x と違う**
- 節: J-S4、5.3「結果が Manager の破棄の後に来る」、7.2「配送」
- 問題:
  - 1.x の `FireOperationResult` は、結果を作った時点（Android の main）で、`this`（その時点の Manager）と per-call を閉じ込めて dispatcher に積んだ。
  - 1.x の `OnDestroy` は、Kotlin のリスナーを外す処理を main に積むだけだった。そのため、すでに積まれた結果と、外す処理より前に main でできた結果は、壊した Manager のイベントと per-call に届いた。
  - 計画は取り出しの時点の `_instance` に出し、いなければ per-call ごと捨てる。新しい Manager がもうあれば、新しい方のイベントと古い per-call に出る。
  - 5.3 は「捨てる（同じ）」と書き、7.2 はこの捨て方をテストで固める。
  - 起きる条件: 呼んだ直後（同じフレーム）に Manager を `Destroy` する後片付けのコード。
- 根拠: AndroidShareManager.cs:123-136、:415-419、1.12.0 の UnityAndroidShareManager.kt:104-111、計画 J-S4:393、5.3:437、7.2:560
- 区分: A1（軽い）
- 修正案:
  - 「今の Manager」への普通の参照を static に置く。Unity の `==` は使わない。`Awake` で入れ、`OnDestroy` で消し、`Volatile` で読み書きする。
  - 完了・選択・アクションの受け口（Android の main）がこの参照を読み、積む処理に閉じ込める。null なら捨てる（1.x でリスナーが外れた後と同じ）。
  - 取り出しの時点では、閉じ込めた Manager に出す。壊れていても C# のイベントは呼べる。
  - 7.2 と 5.3 をこの形に合わせる。採らない場合は、5.3 の行を正しい 1.x の振る舞いに直す。

**R2-6 Chooser Action の per-call を読む時点が、1.x（Android の main）から取り出しの時点（UnityMain）に変わる**
- 節: 5.1 の非変更の行（coordinator）、5.4 のアクションの受け口
- 問題:
  - 1.x は proxy（Android の main）で `_chooserCoordinator.Fire` を呼び、その中で per-call を写してから dispatcher に積んでいた。
  - 計画は coordinator に「その場で走らせる」を渡し、`Fire` を取り出し口の中で呼ぶ。そのため、タップが列に入ってから取り出されるまでの間に `ShareText` が呼ばれると、前の Share のタップが新しい `onChooserAction` に届く。たとえば、Sharesheet が閉じて Player が戻った最初のフレームの `OnApplicationPause(false)` で呼ぶ場合。
  - R1-1 で操作の結果について直したのと同じ種類の差で、テストは通る。
- 根拠: AndroidShareManager.cs:52-53、:433-436、:484-485、ShareChooserActionCallbackCoordinator.cs:54-58、計画 5.1:382、5.4:486
- 区分: A1（軽い）
- 修正案:
  - R2-5 の static の参照を使い、アクションの受け口（Android の main）で世代を確かめた後に `coordinator.Fire` を呼ぶ。`dispatch` には `AndroidNativeCallbackQueue.Post` を渡す。これで、per-call を読む時点も、Manager がいないときに捨てる時点も 1.x と同じになる。
  - coordinator 自体は変えなくてよい。
  - 採らない場合は、5.3 に行を足す。

**R2-7 J-S14 が `onSelected` を消す場面を、「入口の拒否」と「完了の失敗」に限っている**
- 節: J-S14、5.4 の手順 4〜6・10
- 問題:
  - 1.x は、`shareWithCallback` のすべての失敗で `onSelected` を消した。対象は parser の検査、Editor、`pluginInstance` が null、`Call` の例外、ライブラリの失敗。
  - 計画は、`PostImmediateResult` で作る失敗（C# の検査、Editor、ネイティブが使えない、C# の例外）で消すと書いていない。
  - 消さない場合: 本文が空白の `ShareWithCallback` B の後に、ネイティブに残った前の待ち A（C# の検査の失敗は前の待ちを消さない。1.5）の選択が来ると、B の `onSelected` が呼ばれる。
- 根拠: AndroidShareManager.cs:408-413、計画 J-S14:403、5.4:447-449・453
- 区分: A1（軽い）
- 修正案:
  - J-S14 を「`shareWithCallback` の失敗の結果をどこで作っても（`PostImmediateResult` と完了の受け口の両方）、per-call の枠を取り出すのと同じ `lock` の中で `onSelected` を消す」に直す。
  - 7.2 に次のテストを足す: Editor で `ShareWithCallback(..., onSelected)` を呼んだ後に `OnSelectionCore` を入れても、`onSelected` は呼ばれない。

**R2-8 層 2a のテストに要る継ぎ目が 5.1 に足りない。E-20 は Editor では起こせない**
- 節: 5.1（Manager の行）、5.4:451、7.2:556・559・562、E-20、E-23
- 問題:
  - 「完了の時点の枠」と E-23 のテストは、per-call の枠に callback が入り、`ShareCall` が表にある状態から `OnDoneCore` を呼ぶ必要がある。しかし Editor の公開 API は手順 5 で失敗し、枠をその場で取り出してしまう。`AddCall` は private で、テストから呼べない。
  - 「世代が生きているとき」のテストには、世代を持つ `ShareText` の `ShareCall` を表に入れる口が無い。R2-4 の修正で Editor の `ShareText` が世代を進めなくなると、なおさら要る。
  - E-20 の「継ぎ目で例外を起こす」が、どの継ぎ目かが書かれていない。Editor の呼び出しは `AddCall` まで届かない。
- 区分: B
- 修正案:
  - 継ぎ目を 5.1 に列挙する。例: `AddCall` を internal にする、`SetPendingOperationForTests(string, Action<ShareOperationResult>?)`、世代を進めて `ShareCall` を返す `NewShareTextCallForTests()`。
  - E-20 は次のどちらかにする。
    - 確保・`AddCall`・`extern`・後片付けを 1 つの関数にまとめ、`extern` の代わりの delegate を受けられるようにして、層 2a で例外を注ぐ。
    - 検証の層をコードレビューに下げ、その理由を書く。

**R2-9 受け口が Manager の状態に触ることが、common.md と `Handoff` の決まりの例外として記録されていない。枠の置き場所も決まっていない**
- 節: J-S5、J-S12、J-S14、J-S17、5.1（`common.md` の変更の行）、1.5 の最後の項
- 問題:
  - 既存の決まりは次のとおり。
    - common.md:64: 受け口では写して `_free` し、Post に入れるだけ。Manager の状態には触らない。
    - `Handoff`:113: 待っているコールバックなどの状態を変えるのは、UnityMain に積んだ処理の中だけ。
    - `Handoff`:140: ID は UnityMain の表に置き、選択は UnityMain で照らす。
  - 計画は、1.x の時点を保つため次のようにする。理由は書かれているが、5.1 の `common.md` の変更は J-S4 と `user_data` の形しか挙げていない。
    - Android の main で、枠を取り出し、`onSelected` を消し、世代を書く。
    - ID を、呼んだスレッドで書く。
    - 選択を照らさない。
  - 1.5 の最後の項は `Handoff` 4.3 の照合をそのまま引いており、J-S12 と食い違って読める。
  - 枠・`onSelected`・coordinator を static と instance のどちらに置くかが書かれていない。5.1 のリセットの一覧に無いので、instance と読める。instance だと、static の受け口が Android の main から `_instance` を見ることになり、Unity のオブジェクトの比較を main 以外で使ってしまう。
- 根拠: common.md:64、Handoff:113、:140、計画 1.5:127、J-S5:394、5.1:370・380
- 区分: C（規則の例外の記録）
- 修正案:
  - 5.1 の `common.md` の変更に、Share の例外として次を足す: 受け口で枠を取り出す、`onSelected` を消す、世代を書く。ロックで守る。
  - J-S5 と J-S12 に、`Handoff` 4.2・4.3 から外れることを書く。1.5 には「この計画は照らさない」を添える。
  - 枠と `onSelected` は static にし、R2-5 の参照と同じ所に置いて、`SubsystemRegistration` のリセットの一覧に足す。

### 低優先度

**R2-10 重複した ID に付ける印が、利用者の ID と衝突しうる。対応表にすれば、5.3 の差分も消える**
- 節: 4.2.1 の手順 5・6、5.3「`#ntk-dup-<数字>`」、4.4 の入口の `INVALID_CHOOSER_ACTION`
- 問題:
  - `intentAction` が `["x#ntk-dup-2", "x", "x"]` のとき、3 つ目に付ける `x#ntk-dup-2` が 1 つ目と重なる。C ABI はこれを入口で `INVALID_CHOOSER_ACTION` として拒み、Share 全体が失敗する（4.4 は「整えた後では起きない」として `LogError` にしている）。1.x は 3 つとも表示した。
  - 印を元の文字列に付けると、元の文字列に U+0000 があるとき、`AllocUtf8` が印ごと切ってしまい、重複に戻る。
  - 元から `#ntk-dup-<数字>` で終わる ID は、通知で末尾が外れる（5.3 に書いた差分）。
- 根拠: Share.cpp:156-162、計画 4.2.1:316-317、5.3:420、4.4:342
- 区分: A1（軽い）
- 修正案:
  - C に渡す ID は元の文字列から作らず、並びの番号（`ntk-0`、`ntk-1`…）にする。`ShareCall` に「C の ID → 元の `intentAction`」の表を持たせる。
  - 成功の完了で、生きている世代とこの表を 1 つの物にまとめ、R2-5 の参照と同じ所に `Volatile` で置く。
  - アクションの受け口は表で元の ID に戻し、SEND なら捨てる。
  - これで衝突、U+0000、印を外す規則がすべて無くなり、5.3 の行を消せる。C の ID は PendingIntent の URI にしか使われず、画面には出ない（ShareChooserActionReceiver.kt:74-84）。

**R2-11 5.3 の「同じ操作の per-call の対応は 1.x と同じ」は成り立たない**
- 節: 5.3「操作をまたいだ結果の順」、J-S2、J-S5
- 問題:
  - 同じ操作で、A（受け付け済みで、完了を待っている）の後に、B（C# の検査で失敗）を呼んだ場合を考える。
    - 1.x: A の完了が B の callback を受け、B の失敗には per-call が無かった。検査が Kotlin にあり、main の上で順に枠を取り出したため。
    - 計画: B が自分の callback を受け、A の完了には per-call が無い。
  - Direct Share も同じ（1.x では main の上の非同期だった）。
  - 計画の振る舞いの方が自然だが、5.3 の記述はその逆を言っている。
- 根拠: AndroidShareManager.cs:331、:399-406、1.12.0 の UnityAndroidShareManager.kt:317-321、計画 5.3:418
- 区分: A1（軽い。5.3 の記述だけ）
- 修正案: 5.3 の行を「同期に決まる結果は、呼んだ時点の自分の per-call を受ける。1.x では、同じ操作の先の完了がこの per-call を受けることがあった」に直す。

**R2-12 任意の値と `intentAction` の空白の判定は、1.x では .NET と Kotlin の両方だった**
- 節: J-S19、4.2 の表、4.2.1 の手順 2、5.3「空白の判定」
- 問題:
  - 1.x の `AndroidShareJsonBuilder` は、次の値が `string.IsNullOrWhiteSpace` なら JSON から省いた。parser はさらに Kotlin の `isBlank` で省いた。
    - 対象: `title`、`subject`、`mimeType`、`previewTitle`、`previewThumbnailPath`、`category`、`intentAction`
  - つまり、U+0085 だけの値も「無し」になった（MIME なら既定値、`intentAction` ならそのアクションを捨てる）。計画は Kotlin の判定だけにするので、これらが値として渡る。5.3 は「差分なし」と書いている。
  - 必須の値は C# で省いていなかったので、Kotlin の判定だけで正しい。
- 根拠: AndroidShareJsonBuilder.cs:24-28、:41、:90、:105-111、:128、1.12.0 の UnityShareJsonParser.kt:19-24、:35、:76、:94、計画 J-S19:408、5.3:430
- 区分: A1（軽い）
- 修正案:
  - 任意の値・`category`・`intentAction` は `IsBlank(s) || string.IsNullOrWhiteSpace(s)` で「無し」にする。必須の値は `IsBlank` だけで判定する。
  - 層 1 で U+0085 を固める。J-S19 と 5.3 も直す。

**R2-13 画像の印の一覧が、`BitmapFactory` が読める形式より狭い**
- 節: 4.2.1 の手順 4、5.3「画像として読めない Chooser Action のアイコン」
- 問題:
  - 1.x は `BitmapFactory.decodeByteArray` で読めたアイコンを表示した。`BitmapFactory` は、ICO（`00 00 01 00`）、WBMP、HEIF / AVIF（4 バイト目からの `ftyp`）も読む（API による。要確認）。
  - 計画は 5 つの印しか見ないので、これらのアイコンのアクションを捨てる。5.3 は「飛ばす（同じ）」と書いている。
- 根拠: 1.12.0 の ShareRepositoryImpl.kt:254-255、計画 4.2.1:315、5.3:419
- 区分: A1（軽い。要確認）
- 修正案: ICO と `ftyp` の印を足すか、5.3 に「5 形式以外のアイコンは表示されない」と書く。WBMP には印が無いので、5.3 に書く。

**R2-14 5.4 の擬似コードの `called` が、本文の説明と食い違っている**
- 節: 5.4 の擬似コードとその注
- 問題:
  - `called = true` を `extern` の直前に立てるので、`!called` で表から消す分岐は実際には通らない。注は「例外の型（`IsNativeUnavailable`）で見る」と書くが、コードはそうなっていない。型で見る形は、`IsNativeUnavailable` が挙げる 3 つの型以外の例外で漏れる。
  - 受け付けた（NONE が返った）後に例外が出る書き方をすると、ここで失敗を出し、完了でも結果が出て、枠を 2 回取り出してしまう。今の擬似コードには NONE の後に投げる処理が無いが、F-8 の ID の保存やログを同じ `try` の中に置くと起きる。
- 根拠: 計画 5.4:458-483、AndroidNativeToolkitCApi.cs:67-70
- 区分: B（漏れるのは表の要素だけで、利用者には見えない）
- 修正案:
  - 旗を `returned` にし、`extern` が戻った直後に立てる。`catch` では `key != 0 && !returned` のときに表から消す。ネイティブのコードは managed の例外を投げないので、`extern` の式が投げたなら、関数には入っていない。
  - NONE の後の処理は `try` の外に出す。

**R2-15 J-S18: Android の `Base64.DEFAULT` は、空白だけでなく、アルファベット以外のすべての文字を飛ばす（要確認）**
- 節: J-S18、5.5 の手順 3
- 問題:
  - AOSP の `Base64.Decoder` は、表で SKIP になる文字をすべて読み飛ばす。エラーにするのは、`=` の位置の誤りと、端数が 1 文字のときだけ。
  - 計画は空白だけを除くので、パディングの後の余分な文字などを含む入力で、1.x と結果が分かれうる。
- 区分: B
- 修正案:
  - 5.5 の手順 3 に、次の入力を足して実測する: パディングの後の余分な文字、途中の記号、URL 安全な `-` と `_`。
  - AOSP と同じだと分かったら、`DecodeBase64Leniently` を「`A-Za-z0-9+/=` 以外を除き、`=` で 4 の倍数にそろえてから解く」にする。

**R2-16 記述の整合（機械照合に回す）**
- 区分: C
- J-S20:
  - 「`Initialize()` の前に別のスレッドから `ShareText`」は起きない（`Instance` は UnityMain の `Awake` で `Initialize` を通る）。
  - 書くべきなのは、読めなかったときにやり直すかどうか。
  - 「Unity の JNI は、呼んだスレッドをアタッチする」は要確認。
- 8.3 の「Sharesheet が出ているかの命令」は、新しく作らなくても今の `count`（`package` のセレクタ）で足りる（android_ui_driver.py:98、:429-431）。
- 4.4 の `NOT_INITIALIZED` の「完了」は来ない。返るのは入口だけ（Share.cpp:117）。Kotlin の `Errors.common` が返すのは `INVALID_PARAMETER`、`OUT_OF_MEMORY`、`UNKNOWN` だけ（Errors.kt:26-33）。
- 新しい文言 `... must not contain null.` だけが末尾に `.` を持つ。1.x の parser の文言には無い。
- J-S2 の「後のフレームに出す」は正確でない。取り出し口の `Update` より前に走る `Update` から呼ぶと、同じフレームに出る（AndroidNativeCallbackQueue.cs:168-179。1.x の dispatcher も同じ）。保証できるのは「呼び出しの中では出ない」こと。

## 不足項目

- `ShareText` と `ShareWithCallback` の呼び出しの流れ（5.4 と同じ細かさ）。R2-4 と R2-7 は、これが無いことから来ている。
- Direct Share は同期になったので、`pushDynamicShortcut`（Binder の呼び出しとビットマップの読み込み）が、呼んだスレッド（ふつうは UnityMain）を塞ぐ。1.x では Android の main で非同期だった。J-S2 か 5.3 に一言書く。
- 5.5 の手順 3（1.x の実測）に足す項目:
  - 検査で失敗した `ShareText` の後に、前のアクションを押せるか（R2-4）
  - U+0085 だけの題名・MIME・`intentAction`（R2-12）
  - HEIF / ICO のアイコン（R2-13）
  - Base64 の余分な文字（R2-15）
  - Sharesheet が出ている間に Unity が止まるか（R2-2）
- 層 1 と層 2a に足すテスト:
  - `ShareWithCallback` が失敗した後の `onSelected`（R2-7）
  - 取り消しで F-9 に渡す ID の一覧（R2-3）
  - 破棄の前にできた結果の届き先（R2-5）
- 同じ種類の指摘が 2 回続いている。1 回目は R1-1・R1-2・R1-4・R1-5・R1-8・R1-11、2 回目は R2-1・R2-4〜R2-7・R2-11〜R2-13 で、どちらも「1.x と同じ」と書いた所が 1.12.0 のコードと違っていた。サーキットブレーカーの「3 回続く」の前に、仕組みで止めることを勧める。4.2・5.2・5.3 の「1.x と同じ」と書いた行すべてに、根拠の列（1.12.0 のコードの位置）を付けることを必須にする。

## 総合評価

- v1 の A1 はおおむね直っており、C ABI の事実、構造体、スレッドと `release` の扱いは一次資料と合っている。P1〜P5 の違反は無く、機械照合も OK だった。
- 残る A1 は、v2 で入れた新しい仕組みの細部で、「1.x と同じ」とした時点や条件が 1.x のコードと違うもの。
  - いちばん起きやすいのは R2-1（Editor の検査の順）。
  - R2-3（取り消しの ID）と R2-4（世代を進める時点）は、起きる条件が狭いが、テストが通ったまま振る舞いが変わる。
  - R2-5、R2-6、R2-10 は、Android の main で「今の Manager」への参照を読んで閉じ込める仕組み 1 つで、まとめて直せる。
- 検証の面では、Sharesheet が Unity を止めること（R2-2）が、層 2b の手順の前提を変える。
- 区分ごとの件数:
  - A1: 10 件（R2-1、R2-3、R2-4、R2-5、R2-6、R2-7、R2-10、R2-11、R2-12、R2-13）
  - A2: 0 件
  - B: 4 件（R2-2、R2-8、R2-14、R2-15）
  - C: 2 件（R2-9、R2-16）
- A1 は 0 ではない。止める基準に達していないので、v3 で直してから再レビューすることを勧める。

## 2. Codex

（各項の見出しの番号を C2-1〜C2-6 と読む）

確認できた欠陥は6件です。

### 1. 失敗した `ShareWithCallback` が取り消し用IDを壊す

- セクション: 1.5、J-S12〜J-S14、7.3
- 問題: F-8 が入口で受理された時点で新しいIDを保存しますが、その後 `NOT_FOREGROUND` などで開く前に失敗しても、以前の待ちがネイティブ側に残る一方、C#側のIDは元に戻りません。以後の `CancelPendingShareCallback` は存在しない新IDを取り消そうとし、以前の待ちを取り消せません。
- 根拠:
  - 設計自身が「開く前の失敗では前の待ちが残る」としています。[design-v2.md:125](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:125)
  - J-S12はF-8が`NONE`なら直ちにIDを保存し、J-S14は失敗時に`onSelected`だけを消します。[design-v2.md:401](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:401)
  - `NOT_FOREGROUND`では `shareForSelection` 自体が呼ばれず、旧マッピングは残ります。[ShareBridge.kt:83](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/java/com/jonghyunkim/nativetoolkit/capi/jni/ShareBridge.kt:83)
  - IDとKotlin tokenの対応は実際に開いた場合だけ作られます。[ShareBridge.kt:192](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/java/com/jonghyunkim/nativetoolkit/capi/jni/ShareBridge.kt:192)
  - 取り消しはIDに対応するtokenが見つからなければ何もしません。[ShareBridge.kt:201](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/java/com/jonghyunkim/nativetoolkit/capi/jni/ShareBridge.kt:201)
- シナリオ: Aを正常に開く → アプリを背面にしてBを呼ぶ → BのIDをC#が保存 → Bは`NOT_FOREGROUND` → Cancelを呼ぶ。CancelはBのIDを渡すためAは残り、後からAで共有先を選ぶと、ID照合をしない設計により共通の`ShareCallbackReceived`が発火します。7.3は正常に開いた直後のCancelしか検証しないため通ります。
- カテゴリ: A1

### 2. J-S17のC#世代とネイティブ世代が並行呼び出しで逆転する

- セクション: J-S16、J-S17、7.2
- 問題: C#世代は`ShareText`を呼び始めた順に`Interlocked`で増やしますが、ネイティブ世代はAndroid mainで処理された順に増えます。どのスレッドからも呼べるため、この2つの順序は一致しません。F-10イベントにはネイティブ世代が含まれないので、C#側で復元できません。
- 根拠:
  - J-S16は任意スレッドからの呼び出しを許可し、J-S17は呼び出し時のC#世代だけで通知可否を判定します。[design-v2.md:405](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:405)
  - C ABIのChooser Actionイベントが渡すのは`action_id`だけです。[Share.h:47](/Users/jonghyunkim/Desktop/native-toolkit/dist/1.13.0/android/include/NativeToolkitC/Share.h:47)
  - ネイティブ世代はAndroid main上で、画像検査後に増えます。[ShareRepositoryImpl.kt:49](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/share/data/repository/ShareRepositoryImpl.kt:49)
  - 受信時にはネイティブ世代だけで古いアクションを落とします。[ShareChooserActionReceiver.kt:22](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/share/presentation/ShareChooserActionReceiver.kt:22)
- シナリオ: worker AがC#世代1を取得して停止 → worker Bが世代2を取得して先にexternを呼び、正常完了してC#の生存世代が2になる → Aが後からexternを呼ぶ。ネイティブではAが最新世代ですが、Aの完了は「C#最新世代1ではない」と無視されます。`live == latest == 2`のままなのでAのアクションが通り、last-registeredのB用callbackへ配送されます。
- カテゴリ: A1

### 3. J-S18は `android.util.Base64.DEFAULT` と同じ寛容さではない

- セクション: 4.2.1、J-S18、7.1
- 問題: 計画は空白除去とパディング補完だけを行います。しかしAndroidの`Base64.DEFAULT`は、デコード表でBase64文字でも`=`でもない全バイトを`SKIP`として無視します。.NETの`Convert.FromBase64String`は空白以外の非Base64文字を拒否します。
- 根拠:
  - 1.xのDirect Shareは`Base64.decode(..., DEFAULT)`です。`1.12.0:android/unity_android_plugin/.../UnityAndroidShareManager.kt:237-240`
  - 1.xのChooser Actionも同じです。`1.12.0:android/android_library/.../ShareRepositoryImpl.kt:254-255`
  - 計画のJ-S18は「空白を除き、`=`を足す」だけです。[design-v2.md:407](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:407)
  - AOSP実装では非データ値が`SKIP=-1`で、状態機械がそれを読み飛ばします。[AOSP `Base64.java`:188-205, 230-232, 324-342](https://android.googlesource.com/platform/frameworks/base/+/android16-qpr2-release/core/java/android/util/Base64.java#188)
  - `Convert.FromBase64String`は空白以外の非Base64文字を`FormatException`にします。[Microsoft APIリファレンス](https://learn.microsoft.com/en-us/dotnet/api/system.convert.frombase64string)
- シナリオ: 正常なPNGのBase64文字列の途中に`!`を1文字挿入する。1.xは`!`を無視して元のPNGを復元します。移行後はDirect Shareなら検査失敗、Chooser Actionならその項目が削除されます。現在のテスト項目はパディング、改行、完全に読めない値だけなので、この差を検出しません。
- カテゴリ: A1

### 4. 5.4の擬似コードはextern例外時に表から削除しない

- セクション: 5.4、E-20
- 問題: 擬似コードはextern直前に`called = true`とし、catchでは`!called`の場合だけ`RemoveCall`します。そのため`DllNotFoundException`や`EntryPointNotFoundException`ではキーが残ります。直後の説明文は「`called`ではなく例外型で判断して消す」と正しく書いており、コードと本文が矛盾しています。
- 根拠:
  - 問題の代入と条件は擬似コードの469行、478行です。[design-v2.md:469](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:469)
  - 直後の注記はその条件を否定しています。[design-v2.md:483](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:483)
  - C側の`release`はCエントリに到達した拒否、または受理された登録についてのみ呼ばれます。[Share.cpp:108](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Share/Share.cpp:108)、[Accept.h:21](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Common/Accept.h:21)
- シナリオ: `AddCall`成功後、P/Invoke解決で`EntryPointNotFoundException`が出る。Cへ到達しないため`release`は来ず、`called == true`なのでcatchも削除せず、`PendingCallCountForTests`が1のままになります。
- カテゴリ: A2（E-20を実行可能な形で実装すればテスト失敗になる）

### 5. `#ntk-dup-<n>` の予約名前空間衝突をテストしていない

- セクション: 4.2.1、7.1
- 問題: 合成IDが、利用者が最初から渡したIDと衝突する場合の採番規則が確定されておらず、テストも単純な同一IDの重複しか要求していません。
- 根拠:
  - 合成規則は`<intentAction>#ntk-dup-<n>`とだけ定めています。[design-v2.md:316](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:316)
  - 層1テストは単純な`#ntk-dup-2`、`#ntk-dup-3`だけです。[design-v2.md:543](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:543)
  - C入口は最終IDが1つでも重複すると`INVALID_CHOOSER_ACTION`にします。[Share.cpp:148](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Share/Share.cpp:148)
- シナリオ: `x`、`x`、`x#ntk-dup-2`を渡す。2番目に生成したIDと3番目の利用者IDが衝突しないよう再採番する実装が必要ですが、その期待値がなく、単純実装は既存テストを通ったまま実機で入口拒否されます。
- カテゴリ: B

### 6. 層2aで要求している状態を作るseamが5.1にない

- セクション: 5.1、E-20、E-23、7.2
- 問題: `OnDoneCore`等は公開されますが、`user_data`表へ`ShareCall`を挿入するseam、extern前後で例外を注入するseam、Chooser Actionの「生きている世代」を設定するseamがありません。このため、7.2に記載された重要テストを通常のEditor経路から構成できません。
- 根拠:
  - 5.1が列挙するseamは4つの`On*Core`、`SetPendingSelectedForTests`、`PendingCallCountForTests`だけです。[design-v2.md:370](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:370)
  - E-20は「継ぎ目で例外を起こす」としますが、その継ぎ目が定義されていません。[design-v2.md:527](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:527)
  - E-23のテストはprivateな`AddCall`から始める前提です。[design-v2.md:530](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:530)
  - Chooser Actionの正常配送テストには生存世代が必要ですが、成功完了を作るには表内の`ShareCall`が必要です。[design-v2.md:559](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/share/designs/2026-10-11-android-share-design-v2.md:559)
- シナリオ: Editorテストから`OnDoneCore(key, NONE)`を呼んでも、表に対応する`ShareCall`を入れられず、完了や世代更新を発生させられません。例外後の表削除、完了→release順、Chooser Action正常配送のテストが実装不能またはprivate reflection依存になります。
- カテゴリ: B

欠陥なしの重点領域:

- J-S5のper-call枠を読む時点は、現行C#の`FireOperationResult`が配送前にcallbackを取り出す動作と一致しています。
- 選択イベントをC#でID照合しない方針自体は、ネイティブのtoken照合と古い選択の破棄に一致しています。上記1は取り消し用IDの復元だけの問題です。
- SEND抑止は1.xの`normalizeActionIds`と一致しています。
- API 33以下でデコード前に全アクションを落とす方針は、1.xのSDK gate順と一致しています。
- 画像マジック判定の「明白な非画像を先に落とし、印だけ正しい破損画像は既知差分とする」区分に、追加の確定欠陥はありません。
- J-S19の空白集合はKotlinの`isBlank`相当です。
- androidlibの`SEND */*`と`<share-target>`宣言は実ファイルと一致しています。
- C ABIの署名、構造体、所有権、完了直後の`release`順には追加の欠陥はありません。

ファイル変更、Unity・adb・Android端末・クリップボード・デスクトップ操作は行っていません。

## 3. 統合（止める基準の判定）

A1 は、サブエージェント 10 件・Codex 3 件（C2-1 は R2-3 と、C2-3 は R2-15 と同じ所）。A2 は C2-4（= R2-14）。B は R2-2、R2-8 = C2-6、C2-5、R2-15。C は R2-9、R2-16。

**A1 が 0 でないので止めない。** ただし、同じ種類の指摘（「1.x と同じ」と書いた所が 1.12.0 のコードと違う）が 2 回続いた（ラウンド 1 の R1-1・R1-2・R1-4・R1-5・R1-8・R1-11、ラウンド 2 の R2-1・R2-4〜R2-7・R2-11〜R2-13）。ワークフローのサーキットブレーカー（同じ種類が 3 回続いたら、個別に直すのではなく、それを生む仕組みを直す）の手前なので、次の版の前に、1.x のどこまでを保つかの方針を利用者に確かめる。
