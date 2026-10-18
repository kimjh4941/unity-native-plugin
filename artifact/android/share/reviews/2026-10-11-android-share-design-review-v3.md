# レビュー結果

- 日付: 2026-10-11
- 対象ファイル: artifact/android/share/designs/2026-10-11-android-share-design-v3.md
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 3。ラウンド 1・2 とは別のインスタンス）と Codex を並行で回した。どちらも読むだけ。どちらにも親課題の J-11（ふつうの使い方を保つ。5.3 に書いた端の差分は欠陥にしない）を伝えた。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない。Codex の ID は統合のときに付けた）

---

## 1. サブエージェント（Claude）

## 強み

- C ABI の事実は一次資料と合っている。照らした範囲は次のとおり。
  - 入口の拒否と `release` の時点（`Share.cpp:109-113`、`Accept.h:21-38`）。`out_request_id` に先に 0 を書くこと（`Share.cpp:453`）。
  - `ntk_share_cancel_selection` が初期化の前に `NONE` を返すこと（`Share.cpp:485`）。
  - 既定値: MIME の NULL は `text/plain`（`ShareBridge.kt:114`）、画像は `image/*`（`Share.cpp:370`）、カテゴリは `android.shortcut.conversation`（`Share.cpp:515`）。
  - アクションが 0 個でも世代が上がること（`ShareBridge.kt:143`、`ShareRepositoryImpl.kt:49-57`）。
  - 構造体の大きさと位置（`StructLayoutTest.cpp:118-141`）。
- J-S1（`user_data` の表）と `CallNative` は契約どおりに成り立つ。漏れや二重の結果は見つからなかった。
  - `AddCall` は `extern` の前に入る。完了の受け口は表を読むだけで、`release` だけが表から消す。
  - 入口で拒んだときは、呼び出しスレッドの上で `release` が戻る前に来る。
  - `extern` に入れなかったときだけ `catch` で消す（`returned` の旗）。
  - C の `CompleteOnMain` は、コールバックを C のミューテックスの外で呼ぶ（`Registry.h:91-111`、`Registry.cpp:111-127`）。C# の `lock` と C の表のミューテックスが互いに待つことは無い。
  - 文字列とアイコンは、`Open` が main に積む前に Java の配列へ写される（`Share.cpp:332-346`）。`finally` で解放するのは安全。
- 「1.x と同じ」の引用は、`Mgr` と `UnityAndroidShareManager.kt` の範囲ではほぼすべて正しい。照らした所は次のとおり。
  - `Mgr:197-210`、`:207-209`、`:303-316`、`:322-327`、`:331`、`:353-362`、`:399-431`、`:123-136`。
  - `kt:309-315`、`:317-321`、`:323-400`、`:237-241`。
  - 1.12.0 の `ShareRepositoryImpl.kt:226-229,245-266`、`ShareChooserActionInputs.kt:16-24`、`ShareChooserActionReceiverRegistry.kt:43-64`。
- J-11 の方針で、v2 の重い仕組み（世代、重複の印、API レベル、寛容な Base64）を外し、差分を 5.3 に寄せた。設計は小さくなった。
- 正しい入力のふつうの流れは、1.x と同じ結果・文言になる。流れは 9 操作とも追った。
  - ShareText、ShareWithCallback、Cancel、ShareFiles、Direct Share の各流れ。
  - Editor の流れ（J-S3）、`onSelected` の消える条件（J-S12〜J-S14）。
- FileProvider の範囲は 1.x と 2.0.0 で同じ（`native_toolkit_share_file_paths.xml` を 3 つの AAR とタグで照らした）。
- 機械照合（`scripts/check_design_consistency.py`）は OK。
- P1〜P5 の違反は無い。
  - 新規ファイルはすべて `Android` の接頭辞を持つ。
  - ガードは `UNITY_ANDROID || UNITY_EDITOR` と `UNITY_ANDROID && !UNITY_EDITOR` の二重になっている。
  - `AllocStringArray` は複数の機能が使うので共通部へ、`AllocBytes` は Share だけが使うので Share の Bridge へ置く。
  - 他の OS のファイルには触れない。

## 改善点

### 高優先度

**R3-1 J-S15 の `OnDestroy` が static の状態を空にするのに、「自分が `_instance` のときだけ」の条件が書かれていない**
- 節: J-S15（v3:398）、5.1 の Manager の行（v3:366。「per-call の枠、`onSelected`、`user_data` の表、取り消す ID の一覧…は static に置き」）
- 問題:
  - v3 は per-call の枠、`onSelected`、取り消す ID の一覧を static にした。J-S15 は `OnDestroy` でそれらを空にし、一覧のすべてに F-9 を呼ぶと書くが、条件が無い。
  - 起きる場面: Manager を置いたシーンをもう一度読み込む。2 つ目の Manager は `Awake` で `Destroy(gameObject)` され（`Mgr:105-117`）、その `OnDestroy` が走る。
  - 条件が無いと、その `OnDestroy` が生きている Manager の状態を消す。
    - 待っている per-call と `onSelected` が消える。
    - 今の選択の待ちが F-9 で取り消され、選択のイベントそのものが来なくなる。
  - 1.x は `if (_instance == this)` で守っていた（`Mgr:126`）。1.x では状態がインスタンスにあったので被害は小さかったが、static にした v3 では守りが必須になる。
  - コンパイルは通る。7.2 にも 7.3 にも、重複した Manager のテストは無い（7.2:563 は「壊すと捨てる」だけ）。
- 区分: A1（軽い）
- 修正案:
  - J-S15 に「`_instance == this` のときだけ」と書く（1.x の `Mgr:126` と同じ）。
  - 層 2a に 1 件足す。2 つ目の Manager を作って壊しても、生きている Manager の per-call、`onSelected`、取り消す ID の一覧が残ること（`SetPending*ForTests` と ID の一覧の数で確かめる）。

### 中優先度

**R3-2 R2-6（Chooser Action の per-call を読む時点）を直したと書いているが、本文に対応が無い**
- 節: 「v2 からの主な変更」（v3:33「5.3 の各行に…直した（…R2-6）」）、5.1 の非変更の行（v3:378）、5.4 のアクションの受け口（v3:490）、5.3（v3:407-436）
- 問題:
  - 1.x は proxy の中（Android の main）で `_chooserCoordinator.Fire` を呼んでいた（`Mgr:433-436`、`:484-485`）。`Fire` はその時点で per-call を写してから dispatcher に積む（`ShareChooserActionCallbackCoordinator.cs:54-58`）。
  - v3 は `Fire` を取り出し口の中（UnityMain）で呼ぶ。そのため、タップが列に入ってから取り出されるまでに `ShareText(…, newOnChooserAction)` が呼ばれると、前の Share のタップが新しい per-call に届く。
    - 例: Sharesheet でアクションを押し、Player が戻った最初のフレームの `OnApplicationPause(false)` で `ShareText` を呼ぶ。
  - R2-6 の修正案は「直すか、5.3 に行を足す」だった。v3 はどちらもしておらず、5.3 にこの行は無い。
  - UnityMain から 1 回ずつ呼んだ流れで起きうるので、J-11 の保つ範囲に入る。
- 区分: A1（軽い）
- 修正案（どちらか）:
  - J-11 の「単純な形」に沿うなら、5.3 に 1 行足す。1.x「タップを受けた時点（Android の main）の per-call」、2.0.0「取り出した時点（UnityMain）の per-call」、根拠 `Mgr:52-53,433-436`。
  - 1.x と同じにするなら、アクションの受け口で SEND を捨てた後に、生きている Manager の coordinator の `Fire` を呼び、`dispatch` に `AndroidNativeCallbackQueue.Post` を渡す。この場合、Manager の参照を main から読める形で持つ必要がある。

**R3-3 J-S14 の「完了の受け口で `onSelected` を消す」経路に層 2a のテストが無い**
- 節: J-S14（v3:397）、5.4 の完了の受け口（v3:486）、7.2（v3:560）
- 問題:
  - 7.2 の J-S14 のテストは Editor の経路（`PostImmediateResult`）だけを通る。
  - 次の 2 点は、どの層でも固めていない。
    - 受け付けた後の失敗（`NOT_FOREGROUND` など）で、完了の受け口（`OnDoneCore`）が `onSelected` を消すこと。
    - 成功の完了では `onSelected` を消さないこと。
  - どちらも中心の契約（J-S14、J-S12）で、間違えても 2b の正常系では気づけない。
- 区分: B（中心の契約）
- 修正案: 7.2 に次を足す。いずれも今の継ぎ目（`AddCall`、`SetPendingSelectedForTests`、`OnDoneCore`、`OnSelectionCore`）で書ける。
  - `AddCall(shareWithCallback の ShareCall)` → `SetPendingSelectedForTests(cb)` → `OnDoneCore(key, NOT_FOREGROUND)` → 取り出し口を回す → `OnSelectionCore(id, "pkg")`。`cb` は呼ばれず、`ShareCallbackReceived` は出る。
  - 同じ手順で `OnDoneCore(key, NONE)` にすると、`cb` が 1 回呼ばれる。

### 低優先度

**R3-4 5.3「操作をまたいだ結果の順」は「同期に決まる失敗」に限っているが、同期に決まる成功（取り消し、Direct Share）でも順が入れ替わる**
- 節: 5.3（v3:411）、5.4 の `CancelPendingShareCallback`（v3:455）、7.3 の選択のテスト（v3:577）
- 問題:
  - 1.x では、`shareWithCallback` と `cancelPendingShareCallback` の結果は、どちらも Kotlin の main で呼んだ順に出た（`UnityAndroidShareManager.kt:309-321`）。
  - v3 は取り消しの成功を呼んだ時点で積む（`PostImmediateResult`）。同じフレームで `ShareWithCallback` → `CancelPendingShareCallback` と呼ぶと、取り消しの成功が `onStarted` より先に出うる。Direct Share の成功も同じ。
  - 5.3 の行は「失敗」としか書いていないので、5.3 の行として誤りになる。
  - 7.3 の「開く呼び出しと同じフレームで取り消す」テストは、まさにこの順になる。
- 区分: A1（軽い。5.3 の記述）
- 修正案:
  - 5.3 の行を「同期に決まる結果（C# の検査などの失敗、Direct Share、取り消しの成功）」に直す。
  - UI テスト計画では、このテストで 2 つの結果の順を仮定しない。

**R3-5 J-S2 の「1.x の dispatcher も同じ」は、UnityMain から積んだ結果については正しくない**
- 節: J-S2（v3:385）
- 問題:
  - 1.x の `UnityMainThreadDispatcher.Update` は、`lock` の中で `while (Count > 0)` を回した（同じスレッドの `lock` は入り直せる）。そのため、動いている処理（利用者のコールバック）の中で積んだものも同じループで走った。
  - 新しい取り出し口は、始めの件数だけを走らせる（`AndroidNativeCallbackQueue.cs:168-179`）。
  - Dialog の設計 v4:378 は、この違いを「Dialog は Android の main からしか入れないので観測できない」と書いた。Share は UnityMain から積むので観測できる。
    - 例: Editor で、`ShareOperationCompleted` の中から別の Share の操作を呼ぶ。1.x は同じフレームで失敗を受けたが、v3 は次のフレームになる。
  - Editor の結果は J-11 の保つ範囲に入る。
- 区分: A1（軽い。時点の差が 5.3 に無い）
- 修正案:
  - J-S2 の括弧書きを「UnityMain の処理の中から呼んだときは、1.x は同じループで出し、2.0.0 は次のフレームになる」に直す。
  - 5.3 に 1 行足す（根拠: `UnityMainThreadDispatcher.cs` の `Update`、`AndroidNativeCallbackQueue.cs:168-179`）。

**R3-6 1.12.0 の parser の引用行が 4 か所ずれている**
- 節: 4.2 の表（v3:291、296、298、300）、5.3（v3:425）
- 問題: 1.12.0 の `UnityShareJsonParser.kt` を照らした。
  - `Parser:14` の `require` は、正しくは 15（14 は `getString`）。
  - `filePaths must not be empty` の `Parser:66-71` は、正しくは 43 と 59（66-71 は Direct Share の検査）。
  - Direct Share の `id` / `label` / `iconBase64` の `Parser:92-94` は、正しくは 66-71（92-94 は Chooser Action の空白の除外）。5.3:425 の `92-94` も同じ。
  - `ids must not be empty` の `Parser:103-105` は、正しくは 83-84（103-105 は `parseStringArray`）。
  - 主張の中身はどれも正しい。J-11 は引用を必須にしたが、機械照合は Kotlin の行を見ない。
- 区分: C
- 修正案: 上の行に直す。可能なら、`check_design_consistency.py` の照合をタグ付きの Kotlin の引用（`git show <tag>:<path>` の行数）にも広げる。

**R3-7 2b の「表が 0」の確かめは、完了と `release` の間の競合で揺れうる**
- 節: 7.3（v3:587）、E-23（v3:534）
- 問題:
  - `OnDoneCore` は Android の main で取り出し口に積んでから戻る。C はその後で `release` を呼ぶ（`Registry.h:96-103`）。
  - そのため、UnityMain が結果を受け取った直後は、`PendingCallCountForTests` がまだ 1 のことがある。
- 区分: B
- 修正案: 「結果の後、数フレームのうちに 0 になる」（Dialog の `Eventually` の形）で確かめる。

**R3-8 Sharesheet の中を探す命令の時間の上限が、C# の待ち時間と合っていない**
- 節: 8.3（v3:612-613）、7.3（v3:571）
- 問題:
  - C# の答えの待ちは 20 秒（`AndroidUiDriver.cs:36`）。補助は待ち 1 回が 10 秒（`android_ui_driver.py:45`）。
  - そこに `uiautomator dump`（1 回に数秒）を伴う 8 回のスワイプが加わると、20 秒を超えうる。
  - 先に出した依頼を `Wait` しないまま次の依頼を出すと、補助が直列で処理するため、後の依頼が遅れる。
- 区分: B
- 修正案:
  - 8.3 に、`tap-in-sheet` / `absent-in-sheet` の最大時間を書き、`Wait` の期限をそれより長くする。
  - 7.3 に「出した依頼は、失敗の経路でも必ず `Wait` する（後片付けで回収する）」を足す。

**R3-9 テストの網羅の穴（記録して先へ）**
- 節: 7.2、7.3
- 区分: B（網羅系）
- 問題と修正案:
  - J-S6 の「登録は 1 回だけ」: 2b で、アクションのタップや選択の前に `Manager.Initialize()` をもう一度呼び、イベントが 1 回であることを確かめる（今の「タップ 1 回にイベント 1 回」は `Awake` の 1 回しか通らない）。
  - static の状態: 層 2a は static の枠、`onSelected`、ID の一覧、表をテストの間で持ち越す。`ResetStaticStateForTests()` の継ぎ目を 5.1 に足す。
  - Direct Share のテスト: 登録した共有先を後片付けで消す。残ると Sharesheet の並びが変わり、選択のテストの探索に響く。
  - 内容の確かめ: 2b の開く操作は成功しか見ない。テキストの Share では、Sharesheet のプレビューにテストの本文（自分の値なのでプライバシーの問題は無い）が出ることを 1 件確かめると、構造体の写しを端から端まで押さえられる。

**R3-10 `CallNative` が戻り値を返さないので、`ShareWithCallback` が「戻り値が `NONE` なら」を判定できない**
- 節: 5.4（v3:453、462）
- 問題:
  - 擬似コードの `CallNative` は `void` で、`ShareWithCallback` は `NONE` かどうかを知る手段が無い。
  - `requestId != 0` で代わりに判定できる（`Share.cpp:453`、`Accept.h:37`）が、書かれていない。
- 区分: A2（実装で気づく）
- 修正案: `CallNative` が `bool accepted` を返すか、「`requestId != 0` なら `AddSelectionId`」と書く。

**R3-11 記述の整合（機械照合・実装に回す）**
- 区分: C
- 4.2.1 の手順 6（6 個以上の警告）は、1.x では生の個数で、Editor でも検査の前に出た（`Mgr:202-206`）。v3 では整えた後・Editor の判定の後になり、Editor では出ない。ログだけの差だが、「今と同じ」と書くなら、手順 3 の位置で生の個数で出す。
- J-S7 の「`chooserActions` の要素の null」が `ShareWithCallback` にも当たるかが書かれていない。1.x は `ShareWithCallback` でも `BuildShareTextJson` が NRE を出した（`Mgr:312`、`Json:120-129`）。
- `ShareChooserActionCallbackCoordinator` を「非変更」としているが、XML コメント（`:27-30`「In production this is `UnityMainThreadDispatcher.Instance.Enqueue`」）が古くなる。
- 8.3 は「補助の設計書の新しい版にする」と書くが、5.1 のファイルの表に無い。
- J-S6 は、リスナーの登録（`extern`）を、受け口と共有の `lock` の中で行う。デッドロックは無い（C は main を待たない）が、その間 Android の main が `lock` で待つ。登録の印には別の `lock` を使う方が、`common.md` の「main を塞がない」に沿う。

## 不足項目

- 5.3 の行:
  - Chooser Action の per-call を読む時点（R3-2）。
  - UnityMain の処理の中から呼んだときの結果のフレーム（R3-5）。
  - 「操作をまたいだ結果の順」の対象に、同期に決まる成功を足す（R3-4）。
- J-S15 の `_instance == this` の条件と、そのテスト（R3-1）。
- 層 2a: 完了の受け口での `onSelected` の扱い（R3-3）、static の状態を戻す継ぎ目（R3-9）。
- 層 2b:
  - `Initialize()` を 2 回呼んだときのイベントの数。
  - Direct Share の後片付け。
  - 本文の端から端までの確かめ。
  - 依頼の時間の上限と、必ず `Wait` すること（R3-8、R3-9）。

## 総合評価

- C ABI の事実と、J-S1・`CallNative` の `user_data` と `release` の扱いは一次資料と合っている。漏れ・二重の結果・IL2CPP の問題は見つからなかった。
- J-11 の方針で設計は単純になった。正しい入力を UnityMain から 1 回ずつ呼ぶふつうの流れは、9 操作とも 1.x と同じ結果と文言になる。
- 残る A1 はどれも軽く、それぞれ 1〜2 行で直せる。
  - static にしたことで要るようになった `OnDestroy` の条件（R3-1）。
  - 前のラウンドで直したことになっているが本文に無い 5.3 の行（R3-2）。
  - 結果の時点の記述の誤り 2 件（R3-4、R3-5）。
- 区分ごとの件数:
  - A1: 4 件（R3-1、R3-2、R3-4、R3-5。すべて軽い）
  - A2: 1 件（R3-10）
  - B: 4 件（R3-3、R3-7、R3-8、R3-9）
  - C: 2 件（R3-6、R3-11）
- **A1 は 0 ではない。** ただし、どれも新しい仕組みは要らず、J-S15 に 1 行、5.3 に 2〜3 行、J-S2 に 1 文を足せば直る。直した版は、差分だけを確かめる短い再レビューで止められる見込み。
- 要確認の点: 無し。R3-2 が J-11 の「ふつうの使い方」に入るかは、利用者の判断の余地がある。

## 2. Codex

### 結論

- A1: 2件
- A2: 1件
- B: 1件
- C: 0件

5.3に記載済みの差分は欠陥として数えていません。ファイル変更は行っていません。

### C3-1（A1）Chooser Action の per-call callback を読む時点が1.xと異なる

- 節: 5.4「アクションの受け口」、J-S5
- 問題: 設計では `OnChooserActionCore` が UnityMain に処理を積み、その処理内で `coordinator.Fire` を呼びます。このため、per-call callback のスナップショットは UnityMainでの配送時になります。1.xはAndroid mainでイベントを受けた時点に `Fire` を呼び、dispatcherへ積む前にcallbackをスナップショットしていました。通常利用の未記載の時点変更です。
- 根拠: 設計 v3:490、1.x `UnityAndroidShareManager.kt:423-435`、現行 C# `AndroidShareManager.cs:483`、`ShareChooserActionCallbackCoordinator.cs:54`
- 具体例: Share Aのアクションが押され、C callbackが配送を積む。その取り出し前にUnityMainでShare Bを呼ぶと、移行後はAのイベントがBのper-call callbackへ届きます。1.xではAのcallbackが既に閉じ込められていました。
- 修正案: `OnChooserActionCore`で、その時点のcoordinator/per-call callbackをスナップショットした処理を積む。

### C3-2（A1）Cancelの成功結果が、ネイティブ側の取り消し完了より先に届きうる

- 節: 4.3、5.4「CancelPendingShareCallback」
- 問題: F-9はAndroid mainへ取り消しを積んだ時点で戻りますが、設計は直後に成功を`PostImmediateResult`します。1.xではAndroid main上で`cancelPendingCallback()`を実行した後に成功を通知しました。「成功結果が表す時点」が変わりますが、5.3には記載がありません。
- 根拠: 設計 v3:455、`Share.cpp:481`、`ShareBridge.kt:199`、1.x `UnityAndroidShareManager.kt:309-326`
- 具体例: 選択イベントとCancelがAndroid mainのキュー上で競合した場合、移行後はCancel成功をUnity側が先に観測し、その後に共通の`ShareCallbackReceived`を観測しえます。1.xではAndroid main上の処理順が結果通知にも反映されました。
- 修正案: 1.xの時点を保てないならC ABI側にcancel完了が必要です。単なるUnity側の1フレーム遅延ではAndroid mainでの実行完了を保証できません。

### C3-3（A2）`CallNative`が受理結果を呼び出し元へ返さない

- 節: 5.4「ShareWithCallback」「CallNative」
- 問題: `ShareWithCallback`は戻り値が`NONE`の場合だけ`AddSelectionId(requestId)`する契約ですが、提示された`CallNative`は`void`で、ネイティブの戻り値`code`もローカル変数です。呼び出し元は受理されたか判定できません。
- 根拠: 設計 v3:453、462、`Accept.h:37`
- 具体例: 擬似コードどおりでは呼び出し元から`code`を参照できずコンパイルできません。ID追加を省けば、7.3のCancelテストが選択を取り消せず失敗します。
- 修正案: `CallNative`から戻り値または`accepted`を返すか、初期値0の`requestId != 0`を受理条件として明記する。

### C3-4（B）`Initialize()`の冪等性を検証するテストがない

- 節: J-S6、7.2、7.3
- 問題: J-S6は重複登録によるイベント二重配送を明示的に防ぐ設計ですが、`Initialize()`を複数回呼び、登録またはイベントが重複しないことを確かめるテストがありません。
- 根拠: 設計 v3:389、555
- 具体例: 公開`Initialize()`をAwake後に再度呼ぶ実装でlistenerを二重登録しても、現在の単一初期化テストではChooser Actionとselectionが各2回届く欠陥を検出できません。

問題なしと確認した領域:

- C ABIの署名、構造体配置、エラー値、`ntk_string`の所有権は一次資料と整合しています。
- `user_data`表、完了がexternのreturn前に来る場合、入口拒否時の同期`release`、完了直後の`release`の処理順は妥当です。
- 受理済みIDをすべてF-9へ渡す取消規則と、`onSelected`をすべての`ShareWithCallback`失敗で消す規則は整合しています。
- selectionをC#で再照合しない方針は、ネイティブ側が現在のtokenだけを配送する実装と整合しています。
- SharesheetでUnityが止まる前提に対する「先に依頼し、同じフレームで開き、復帰後に回答を待つ」層2bプロトコルは妥当です。手順2で実測してから採用する条件も明記されています。

## 3. 統合（止める基準の判定）

A1 は、サブエージェント 4 件（R3-1、R3-2、R3-4、R3-5）・Codex 2 件（C3-1 = R3-2、C3-2）。どれも軽く、新しい仕組みは要らない（J-S15 の条件 1 行と、5.3 の行の追加・修正、J-S2 の 1 文）。A2 は R3-10 = C3-3。B は R3-3、R3-7、R3-8、R3-9 = C3-4。C は R3-6、R3-11。

**A1 が 0 でないので止めない。** v4 で直し、差分を確かめる短い再レビュー（ラウンド 4。レビュアーを替える）で止める。同じ種類の指摘（「1.x と同じ」の誤り）は、J-11 の後は 5.3 の記述の抜けだけになり、仕組みの誤りは出ていない。
