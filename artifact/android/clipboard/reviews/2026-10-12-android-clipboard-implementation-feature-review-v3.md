# 実装レビュー結果 v3（Android Clipboard の C ABI 2.0.0 への移行）

- 日付: 2026-10-12
- レビュー対象: ブランチ `feature/UNT-13` の未コミットの差分（実装のレビュー v2 の指摘を直した後）
- 実装計画: `artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md`
- 実装結果: `artifact/android/clipboard/results/2026-10-12-android-clipboard-implementation-feature-result-v3.md`
- レビュアー: サブエージェント（ラウンド 3。前のラウンドとは別のインスタンス。1 章）と Codex（2 章）を並行で回した。どちらも読むだけ。どちらの出力も手を加えていない
- **A: 両方とも 0 件。止める基準に届いたので、レビューはここで止める**
- 対応は 3 章（取りまとめ役が書いた）

---

## 1. サブエージェント（Claude）

ファイルと git は読んだだけで、何も変えていません。

ほかに読んだもの:
- native-toolkit の `Clipboard.cpp`（`PostObserving`）、`ClipboardBridge.kt`、`MainPoster.kt`
- `AndroidNativeCallbackQueue.cs`、`AndroidNativeToolkitCApi.EnsureNativeAvailable`
- 直近の `PlayMode.xml` / `PlayMode.log`（`/var/folders/.../unity-android-verify/`。06:41Z の実行で、ソースの最終更新 15:39〜15:40 JST より後）

### 1. v2 の指摘の判定

| 指摘 | 判定 | 根拠 |
|---|---|---|
| S2:A-1 / X2:A-1 | 解決 | 下の 3 点。詳しくは 2 章の「確かめた所」 |
| S2:C-1 | 解決 | 下の 3 点 |
| X2:A-2 | 解決 | `Invoke` が `allocated = new List<IntPtr>()` を `try` の中で作り、`finally` で null を見てから `FreeAll` する（`:573-597`）。継ぎ目は作らず、コードレビューで押さえる（3 章のとおり） |
| S2:B-1 | 解決 | 下の (a)〜(c) |
| X:B-2 / X:B-1 / S:B-1（Codex が一部解消・未解消とした分） | 記録で対応 | 実装結果 v3 の 1.2 に、コードレビューで押さえる配線として書いてある。3 章のとおり |
| S2:C-2 / X2:C-1 | 解決 | 下の 3 点 |
| S2:C-3 / X2:C-2 | 解決 | 下の 6 点 |

**S2:A-1 / X2:A-1 の根拠**
- 開始は、印・F-9・失敗の戻しを `lock (s_observeLock)` の中で行う（`:507-523`）
- `StopObserving` は、Invoke の lambda の中で `lock` を取り、`LowerObserving` と `StopNative` をその中で呼ぶ（`:332-339`）。`StopIfObserving` も印・判定・F-10 を `lock` の中で行う（`:720-729`）
- S2:A-1 の順（2 回目の開始の F-9 の間に、別のスレッドが止める）は、止める側が `LowerObserving` の `lock` で待つので、最後は偽で終わる
- X2:A-1 の順（2 つの開始の競合）も直列になり、後のほうの結果で終わる

**S2:C-1 の根拠**
- `s_generation` は止めるたびに増える（`LowerObserving`。`:735-742`）
- `OnChangedCore` は、積むときの世代を閉じ込める。`RaiseChanged` は世代を照らす（`:783-792`）
- `Change_QueuedBeforeAStopAndARestart_IsNotRaised` は、世代の照合を消すと最初の assert が `1` になって落ちる

**S2:B-1 の根拠**
- (a) `Start_ASecondStartThatThrows_KeepsTheFirstObserving` は、catch の戻し先を `false` に変えると落ちる
- (b) `flagAtTheCall` は、印を真にする所を `StartNative()` の後へ動かすと落ちる
- (c) `Start_Refused` と `Start_Throwing` で、F-9 を呼んだ回数を照らしている

**S2:C-2 / X2:C-1 の根拠**
- 2a は 30 本（数え直した）。`PlayMode.xml` は 276 通過、37 スキップ（273 + 3）で、実装結果 v3 の 5 章と合う
- `testing.md` の 2a の行は 30 本と観点に更新されている
- `.asset` と `.slnx` の差分は、今の `git status` には無い

**S2:C-3 / X2:C-2 の根拠**
- フィールドのコメント（`:98`）と `SetNativeForTests` の summary（`:823-826`）に、開始が入った
- `Assert.Ignore` の文言は「real native behaviour under IL2CPP」になった
- 後片付けの summary に「except for one check at the end」が入った
- `ReadAndFree` に、ログを入れない理由のコメント（`:607`）が入った
- 実装結果 3.4 の確保の範囲が、「アンマネージの確保と引数の写し」に絞られた

### 2. 新しい指摘

#### S3:B-1（B / low）F-10 を囲む `lock` と、受け口が読む順を、テストも 1.2 の一覧も押さえていない

**場所**
- `AndroidClipboardManager.cs:332-339`（`StopObserving` の lambda の `lock`）
- `:720-729`（`StopIfObserving` の `lock`）
- `:783-784`（`OnChangedCore` の読む順）
- 実装結果 v3 の 1.2

**根拠**
- `Start_AStopFromAnotherThread_WaitsAndWins` が守るのは 2 つだけ: 開始の側の `lock` と、`LowerObserving` 自身の `lock`。Editor の `StopObserving` は `CanCall` で返るので、外側の `LowerObserving` しか通らない
- 次の 3 つは、消しても 2a のどのテストも落ちない
  - `StopObserving` の lambda の `lock (s_observeLock)`（印と F-10 を 1 つのまとまりにする所）
  - `StopIfObserving` の `lock` が `StopNative()` まで囲んでいること
- 消したときに起きること:
  - `LowerObserving` → (別のスレッドで開始が成功: 印は真、F-9) → F-10 の順になる
  - 印は真なのに、ネイティブは止まる。v2 の 3 章の約束（「最後に lock を取ったものの状態で終わる」）が破れる
- `OnChangedCore` の読む順（世代が先、印が後）も、逆にすると「止める → 再開」の間に読んだ変更が出る。これを落とすテストは無い（並行でしか起きない）
- いずれも実装結果 v3 の 1.2 の「コードレビューで押さえる配線」に書かれていない

**提案**
- `StopIfObserving` は継ぎ目で届く（`s_stopObservingForTests` があると、ネイティブの判定を飛ばす）。開始のテストと対になる形で足せる:
  1. 停止の継ぎ目の中で、別のスレッドに `StartObservingCore()` を走らせ、`Join(200)` する
  2. 開始が停止を待つこと（`Join` が偽）を照らす
  3. 最後に印が真で、開始と停止が 1 回ずつ呼ばれたことを照らす
- `lock` を `LowerObserving` までに狭めると、このテストは落ちる
- `StopObserving` の lambda の `lock` と読む順は、1.2 の一覧に足す

**補足（指摘ではない）**
- `Start_AStopFromAnotherThread_WaitsAndWins` は、開始の側の `lock` を外すと、ほぼ確実に落ちる
  - 止める側が 200 ms 以内に終わり、最初の assert が落ちる
  - 戻しが真を書くので、2 つ目の assert も落ちる
- 落ちないのは、止める側の起動に 200 ms 以上かかったときだけ（見逃しの方向）
- `lock` があるときは決定的: 止める側は必ず `LowerObserving` の `lock` で止まる
  - ログの順でも確かめた。止める側の `hasCallback` → 本体が `lock` を出た後の `startObserving failed: UNKNOWN` と、止める側の `can only be called on Android` が並ぶ
  - テストの所要は 0.208 s

#### S3:C-1（C / low）`StopObserving` の「呼んだ後は出さない」は、別のスレッドから呼んだときは成り立たない

**場所**
- `AndroidClipboardManager.cs:319-324`（XML）
- 実装結果 v3 の 3.4（「`StopObserving` の後の変更」の行）

**根拠**
- `RaiseChanged` は UnityMain で印と世代を照らす。照らした後で `ClipboardChanged` を呼ぶ前に、別のスレッドの `StopObserving` が戻ることがある
- そのときは、`StopObserving` が戻った後に購読者が呼ばれる
- 購読者の呼び出しを `lock` の中に入れない限り防げない（入れるべきではない）
- UnityMain から呼んだときは成り立つ

**提案**
- XML と 3.4 に、「別のスレッドから呼んだときは、出ている最中の 1 回が呼び出しの後に届くことがある」と 1 文足す

#### S3:C-2（C / low）実装結果 v3 の 1.1 の J-C10 が古い

**場所**
- 実装結果 v3 の 1.1 の J-C10

**根拠**
- 「`StartObserving` が F-9 の前に真にし、失敗なら偽に戻す」と書いてある
- 実装は v1 の直しから「呼ぶ前の値に戻す」。v2 の直しから「開始と停止は `lock` の中」
- 「v1 からの変更」と 1.2 には正しく書いてある

**提案**
- 1.1 の J-C10 の行を「失敗なら呼ぶ前の値に戻す（1.2 の監視の `lock` と世代を参照）」にする

### 3. 確かめて、欠陥ではなかった所

**2 つの `lock` の順と入れ子**
- `StartObservingCore` は、`EnsureChangeListener`（`s_listenerLock`）を、`s_observeLock` を取る前に呼び終える（`:504` → `:507`）
- `ResetStaticStateForTests` も 2 つを順に取って離す
- `s_observeLock` の中で `s_listenerLock` を取る所も、その逆も無い。入れ子は無い
- `LowerObserving` の再入（`StopObserving` の lambda、`StopIfObserving`）は、`Monitor` が再入できるので問題ない

**F-9 / F-10 は待たない**
- `ntk_clipboard_start_observing` / `stop_observing` は `PostObserving` を通る。ここで行うのは `Enter`（`IsReady` と `jni::Env()`）と、`ClipboardBridge.startObserving` / `stopObserving` の呼び出しだけ
- Kotlin の側は `MainPoster.post`（`Handler.post`）するだけ。`MainPoster` の doc に「main から呼ばれても post する」とある。したがって、Android の main から呼ばれても、`lock` の中で受け口に再入することは無い
- 受け口（`OnChanged` → `OnChangedCore` → `AndroidNativeCallbackQueue.Post`）は、どの C# の `lock` も取らない（`Post` は `ConcurrentQueue` だけ）
- したがって、Android の main と UnityMain が互いに待つ形は無い
- UnityMain が `s_observeLock` で待つのは、別のスレッドが F-9 / F-10 の JNI を呼んでいる間だけで、その待ちには終わりがある
- 単一の `Handler` の FIFO なので、ネイティブに積まれる開始・停止の順は `lock` を取った順と一致する

**世代**
- `++` は、すべて `s_observeLock` の中にある（`LowerObserving`、`ResetStaticStateForTests`）
- 書く側は「印を偽 → 世代を増やす」、受け口は「世代 → 印」の順で、どちらも volatile（release / acquire）
- 「新しい世代と古い真の印」の組は読めない。「古い世代と真の印」は、`RaiseChanged` の照合で落ちる
- `RaiseChanged` は「印 → 世代」の順で読む。これも、停止が間に入れば落ちる
- 停止は外側と lambda の中で 2 回数えるが、等しいかどうかしか見ないので問題ない。int の折り返しも同じ
- 失敗した開始の戻しは世代を増やさない。そのため、失敗した開始の F-9 の間に積まれた変更は、配送の前にもう一度開始が成功すると出る。ただしその変更は 1 回目の `StartObserving` の呼び出しより後のものなので、XML の約束（「`StartObserving` の前には出さない」）は破れない

**`OnChangedCore` が積む lambda**
- ローカルを 1 つ閉じ込めるだけ。IL2CPP でも問題ない。変更 1 回ごとの小さな確保に留まる

**`ResetStaticStateForTests`**
- 印と世代を `lock` の中で戻す
- `SetUp` と `TearDown` は先にキューを空にするので、古い世代の `RaiseChanged` は残らない
- `SubsystemRegistration` では、両方の `ResetOnStart` の順にかかわらず、最初のフレームの前に両方とも走る

**Editor でスレッドから `StopObserving` を呼ぶこと**
- `EnsureCreated` は、`s_mainThreadId` が UnityMain と違うので何もしない
- `Application.platform` はスレッドから読めた。ログに `can only be called on Android` が出ており、例外は無い
- `Post` はスレッドから呼んでよい
- 積まれた失敗の結果は、`[Test]` なのでフレームが回らず、`TearDown` の `ResetForTests` で消える
- `Join(200)` で上限があるので、テストが固まることは無い

**`lock` の中のログ**
- 開始の例外の `LogError` と、`StopIfObserving` の `EnsureNativeAvailable` の警告は、`lock` の中で出る
- 実害は無い: Unity のログは別のスレッドを待たない

**P2**
- 差分は Android、`agent-rules`、`artifact` だけ。ほかのプラットフォームのファイルは変えていない

### 4. 総合評価

**A は 0 件。B は 1 件（S3:B-1、low）、C は 2 件（S3:C-1、S3:C-2、どちらも low）**
- v2 の 9 件は、すべて 3 章の対応どおりに直っているか、記録されている
  - 内訳: A 3、B 1、C 5。加えて、Codex が一部解消・未解消とした 3 件
- 直した所（監視の `lock`、世代、`Invoke` の確保）から生まれた、実装を直す必要のある欠陥は見つからなかった
- `lock` の順、Android の main と UnityMain が互いに待つ形が無いこと、世代を書く・読む順は、native-toolkit のソースまで降りて確かめた
- 残りは、テストの穴 1 件と記述 2 件。どれも数行で直る
- 推測を含む項目: S3:B-1 の補足の「起動に 200 ms 以上かかると見逃す」は、論理から導いたもので、実際に `lock` を外して流してはいない（要検証）

---

## 2. Codex

### Round 2 指摘の判定

| ID | 判定 | 根拠 |
|---|---|---|
| X2:A-1 | 解決 | `StartObservingCore` の状態変更・F-9・rollback、公開 Stop の状態変更・F-10、`StopIfObserving` の状態変更・F-10 が `s_observeLock` で直列化された。AndroidClipboardManager.cs:507、334、720 |
| X2:A-2 | 解決 | `new List<IntPtr>()` が `try` 内に移り、確保・入力変換・ネイティブ呼び出しの例外が失敗結果になる。AndroidClipboardManager.cs:573 |
| X2:C-1 | 解決 | v3 は層2aを30本、Editor PlayModeを276通過と記録。実ファイルのテスト属性も30件。result-v3.md:173 |
| X2:C-2 | 解決 | フィールドと `SetNativeForTests` の説明がいずれも registration/start/stop を記載。AndroidClipboardManager.cs:98、823 |
| S2:A-1 | 解決 | X2:A-1 と同じロックにより、失敗した Start の rollback が並行 Stop を上書きしない。 |
| S2:B-1 | 解決 | F-9 の呼び出し回数・呼び出し時の印、拒否・例外、2回目の例外経路のテストが追加された。AndroidClipboardManagerEditorTests.cs:342 |
| S2:C-1 | 解決 | stop 時に世代を増やし、受け口で閉じ込めた世代を配送時に再照合する。AndroidClipboardManager.cs:735、779 |
| S2:C-2 | 解決 | v3 結果と `testing.md` が30本・276通過に更新され、IL2CPP 実機結果も記録された。 |
| S2:C-3 | 解決 | test seam、Ignore 文言、teardown 説明、`ReadAndFree` のログ除外理由、確保失敗の対象範囲がすべて更新された。 |

### 重点項目の確認結果

- **ロック順**: 循環はない。`EnsureChangeListener` は `s_listenerLock` を解放してから `s_observeLock` を取る。`ResetStaticStateForTests` も両方を同時には保持しない。
- **ネイティブ start/stop**: ロック内で呼んでも問題ない。C++ の `PostObserving` が Kotlin を同期呼び出しするが、Kotlin 側は `MainPoster.post`、最終的には `Handler.post` を呼んで直ちに戻る。Android main や変更 callback の完了は待たない。したがってデッドロックしない。
  - 根拠: Clipboard.cpp:423、ClipboardBridge.kt:160、MainPoster.kt:28（native-toolkit 側）
- **`s_generation`**: 書き込みはロック内、ロック外の読み取りは `volatile`。stop が二つの読みの間に割り込んでも、旧世代として破棄される。
- **変更ごとの lambda**: ネイティブ callback 全体の `try/catch` 内で確保される。
- **`Invoke` の確保**: `try` 内に入っている。
- **`ResetStaticStateForTests`**: 観測状態と世代を同じロック内で戻している。
- **worker からの `StopObserving`**: Editor で安全。`EnsureCreated` が非 UnityMain スレッドを検出し、Unity オブジェクトに触れず戻る（AndroidNativeCallbackQueue.cs:88）。

### 新規指摘

#### X3:B-1（B: テストの穴）Start/Stop 競合テストが、公開 Stop の F-10 をロック内で呼ぶ契約を検証していない

- **場所**: AndroidClipboardManagerEditorTests.cs:408（`Start_AStopFromAnotherThread_WaitsAndWins`）、AndroidClipboardManager.cs:330-338
- **根拠**:
  - worker からの `StopObserving()` は、最初の `LowerObserving()` でロックを待つ。
  - Editor では続く `CanCall` が false を返すため、F-10 を含む `Invoke` の lambda に到達しない（AndroidClipboardManager.cs:331）。
  - そのため、行334〜338の内側のロックを削除して `StopNative()` をロック外へ出しても、このテストは通る。
  - 検証できているのは「先行する印の変更が Start を待つこと」だけで、X2:A-1 が要求した「印と F-10 を一まとまりにすること」ではない。
  - worker が `Join(200)` 以内に実行されない場合、ロックを壊しても、後から印を下げて両 assert を通過できる。
- **修正**:
  - 状態変更と `StopNative` をまとめた platform 非依存の `StopObservingCore` を切り出し、テストから直接呼ぶ。
  - barrier/event で stop スレッドが core に到達したことを確認してから Start 側を解放する。
  - stop delegate が Start の解放前には呼ばれず、その後ちょうど1回呼ばれることを検証する。
  - 時間依存の `Join(200)` を同期条件にしない。

**A の件数: 0**

補足（取りまとめ役の転記）: Codex の出力は、Codex を呼んだ転送役のエージェントがそのまま返したもの。転送役は一次資料を確かめ直していない。

---

## 3. 対応（取りまとめ役）

両方の A が 0 件になったので、止める基準（`agent-rules/workflows/review-document/workflow.md` の手順 8 と同じ考え方）に従い、レビューはここで止める。

| 指摘 | 区分 | 対応 |
|---|---|---|
| S3:B-1、X3:B-1 | B | テストは足さない（網羅系。停止の側の F-10 が監視の `lock` の中にあることは、Editor では F-10 に届かない）。実装結果 v3 の 1.2 の「コードレビューで押さえる配線」に、停止の側の `lock` と、受け口が世代を印より先に読む順を足した。`Start_AStopFromAnotherThread_WaitsAndWins` が時間（200 ms）に頼ることも、見逃しの方向にしか働かないので、そのままにする |
| S3:C-1 | C | `StopObserving` の XML と実装結果 v3 の 3.4 に、「別のスレッドから呼んだときは、UnityMain でちょうど出ている最中の 1 回が、呼び出しの後に届くことがある」を足した（コメントだけで振る舞いは変えないので、通しの検証は流し直していない） |
| S3:C-2 | C | 実装結果 v3 の 1.1 の J-C10 の行を直した |
