# 実装レビュー結果 v4（Codex による独立レビュー・2 回目）

## 基本情報

- 日付: 2026-09-07
- 機能名: clipboard / 対象プラットフォーム: Windows
- レビュアー: **Codex CLI 0.153.4（`gpt-5.6-sol`、reasoning effort: high）**
- レビュー対象: `git diff 284a607..HEAD -- Packages/`（レビュー v2 前の実装完了時点以降、修正 3 ラウンド分すべて）
- 実行方法: `codex exec -s read-only -c model_reasoning_effort="high"`
- 依頼の主眼: 個別の欠陥探しに加えて、**drain のライフサイクルを 5 個の静的フラグで表現し続けてよいか**の判定

## 実装側による事実確認（レビュー受領後）

- **A-1 の帰属はレビューの記載と異なる。** レビューは「`284a607` より前から存在し、今回差分起因ではない」としているが、
  `git show 284a607` で当時のメソッド名が `TryShutdownCore` であったことを確認した。
  名称を `InvokeNativeShutdown` に変更したのは `dad06ad`（レビュー v2 への対応）であり、
  **`nameof` を取り残してビルドを壊したのは実装側**である。行自体が古いだけで、破壊は今回のラウンド由来。

---

## 結論

**要修正（重大）です。A 区分は 6 件です。**

5 フィールド方式は継続すべきではありません。問題は個々の代入漏れではなく、次の状態を表現できないことです。

- drain の実行主体が「生存中」か「消滅済み」か
- 最終結果が「dispatcher に enqueue 済み」か「実配送済み」か
- quit 前の同期配送中か
- 同期配送中の再入が、現在の結果へ合流すべきか新しい drain なのか

そのため、今回の修正後も shutdown/drain に A が集中しています。`RunShutdownAttempt` への統合は有効ですが、**試行の状態機械と drain セッションの状態機械は別物**であり、後者がまだ存在していません。

---

## A. 振る舞いを変える問題

### A-1. Windows Player 用分岐がコンパイルできない

対象: [WindowsClipboardManager.cs:2600](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2600)

1. **事実**

   Player 専用分岐内に `nameof(TryShutdownCore)` がありますが、現在のクラスに `TryShutdownCore` は存在しません。現在の名前は `InvokeNativeShutdown` / `RunShutdownAttempt` です。

2. **再現**

   `UNITY_STANDALONE_WIN && !UNITY_EDITOR` でコンパイルすると、`CS0103: The name 'TryShutdownCore' does not exist in the current context` になります。Editor テストでは分岐全体が除外されるため検出されません。

3. **根拠**

   設計 7.11、9.4 は Windows Player/IL2CPP ビルドを要求しています。

補足: `git blame` 上、この行は `284a607` より前から存在します。今回差分起因ではありませんが、現時点の Windows Player を成立させないため除外できません。

---

### A-2. 起動後にコルーチンだけが死ぬと、存在しない drain に永久合流する

対象: [WindowsClipboardManager.cs:842](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:842)、[WindowsClipboardManager.cs:2726](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2726)、[WindowsClipboardManager.cs:2900](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2900)

1. **事実**

   起動失敗と `OnDestroy` は追加対応されていますが、正常に開始した後の `SetActive(false)`、`StopAllCoroutines()`、drain 内例外には終了処理がありません。`s_drainRunning` を戻すのはコルーチン末尾の `SettleDrain` だけです。

2. **再現**

   - `ShutdownWithDrain(callback)` を開始する。
   - 1 フレーム進め、native が `Busy` を返している間に Manager の GameObject を非アクティブ化する。
   - `OnDestroy` は走らずコルーチンだけが終了する。
   - `s_drainRunning == true`、waiter 保持、状態 `Draining` のまま残る。
   - 以後の `ShutdownWithDrain` は幽霊 drain に合流し、quit は 2900 行目で `false` を返し続ける。

   `NativeShutdownForTests` が例外を投げた場合にも同じ状態になります。

3. **根拠**

   設計 7.4 は、成功・タイムアウト・終端失敗の全経路で quit を再開し、`ShutdownWithDrain` の最終結果を配送する契約です。

**v3 A-2 は部分的に未解決です。** v3 が明記した「起動後の中断」は残っています。

---

### A-3. quit 中に Manager が破棄されると、waiter は配送されるが quit は再開されない

対象: [WindowsClipboardManager.cs:644](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:644)、[WindowsClipboardManager.cs:650](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:650)、[WindowsClipboardManager.cs:661](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:661)、[WindowsClipboardManager.cs:2888](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2888)

1. **事実**

   `OnDestroy` は active drain を同期 `SettleDrain` しますが、`s_quitDrainCompleted` を立てず、`ResumeQuit` も呼びません。その前に quit handler を解除します。

2. **再現**

   - `OnWantsToQuit` が `s_quitDrainStarted = true` として `false` を返す。
   - drain 完了前に Manager を破棄する。
   - `OnDestroy` が waiter を settle し、`s_drainRunning = false` にする。
   - 残る状態は `quitStarted=true / quitCompleted=false / drainRunning=false`。
   - 最初の quit は既に拒否され、handler も解除済みなので、誰も `Application.Quit()` を再実行しない。

3. **根拠**

   設計 7.4 の 975、982、1024 行相当は「quit が必ず再開する」ことを要求しています。

これはユーザー提示例の「drain 中に quit、その後コルーチンが死ぬ」を `OnDestroy` が完全には回収できていないケースです。

---

### A-4. quit 前の同期配送中に `ShutdownWithDrain` が再入すると、新しい結果が失われる

対象: [WindowsClipboardManager.cs:824](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:824)、[WindowsClipboardManager.cs:2773](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2773)、[WindowsClipboardManager.cs:2791](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2791)

1. **事実**

   `SettleDrain` は callback を呼ぶ前に全 drain フィールドを idle 相当に戻します。しかし `ResumeQuit` は callback が戻った後です。「settling 中」という状態がありません。

2. **再現**

   - 通常 drain に quit が合流する。
   - `SettleDrain(... synchronous:true)` が callback A を呼ぶ。
   - A 内から `ShutdownWithDrain(callbackB)` を呼ぶ。
   - `ShutDown` なら B は dispatcher に enqueue される。`ShutdownFailed` なら新しい drain が開始される。
   - A が戻ると元の処理が直ちに `ResumeQuit` を実行する。
   - 次の Update が保証されないため、B は未配送、または新 drain が途中終了する。

3. **根拠**

   `ShutdownWithDrain` の各呼び出しは最終結果を受け取る契約です。設計 8.4 も teardown 時の未配送を許していません。

---

### A-5. enqueue 済みの drain 結果が状態から消え、teardown が回収できない

対象: [WindowsClipboardManager.cs:2791](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2791)、[WindowsClipboardManager.cs:2826](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2826)

1. **事実**

   通常 drain では、`SettleDrain` が静的フィールドをすべてクリアしてから `Hand` を enqueue します。この時点で「配送待ち」と「配送完了」が同じ組み合わせになります。

2. **再現**

   - 通常 drain が終了し `Hand` を enqueue する。
   - dispatcher の次回 `Update` 前に dispatcher/Player teardown が発生する。
   - `OnDestroy` は `s_drainRunning == false` のため同期配送しない。
   - queued closure が破棄されると callback/event は一度も呼ばれず、waiter が保持していた参照も dispatcher とともに失われる。

3. **根拠**

   非同期リクエストでは同じ問題を避けるため、設計 7.6.1 が `Undelivered` を明示的に要求しています。drain 結果にも同等の「配送所有権」が必要です。

---

### A-6. 共通イベントの第1購読者が投げると、第2購読者以降が未配送になる

対象: [WindowsClipboardManager.cs:2801](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2801)、[WindowsClipboardManager.cs:3023](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:3023)

1. **事実**

   drain waiter は個別 try/catch になりましたが、`ClipboardOperationCompleted` は multicast delegate 全体を一度だけ `Invoke` しています。

2. **再現**

   `ClipboardOperationCompleted` に A、B の順で登録し、A が例外を投げると B は呼ばれません。

3. **根拠**

   v3 A-4 は waiter とイベント購読者の両方を対象としていました。結果 v5 もこの問題を A-14 として明示的に未対応と記録しています。

**v3 A-4 は waiter 部分だけ解消し、イベント部分は未解決です。**

---

## 5 フィールドの状態分析

フィールド順を `Running / Delivery / Waiters / QuitStarted / QuitCompleted` とします。

| 状況 | 現在の組み合わせ | 判定 |
|---|---|---|
| idle | `F/F/∅/F/F` | 正常 |
| 通常 drain 実行中 | `T/T/W/F/F` | 正常 |
| quit 専用 drain | `T/F/∅/T/F` | 正常 |
| 通常 drain に quit 合流 | `T/T/W/T/F` | 正常 |
| コルーチン消滅後 | `T/T/W/(任意)/F` | **生存中と区別不能** |
| 結果 enqueue 済み | `F/F/∅/F/F` | **完全配送済みと区別不能** |
| quit 中の同期 callback 実行中 | `F/F/∅/T/F` | **settling 状態を表せない** |
| quit 中に破棄された後 | `F/F/∅/T/F` | **到達可能な矛盾状態** |
| quit 完了 | `F/F/∅/T/T` | 正常 |

到達不能または本来禁止すべき組み合わせは、少なくとも次です。

- `QuitCompleted=true && QuitStarted=false`
- 安定状態で `Waiters!=null && DeliveryRequested=false`
- 安定状態で `DeliveryRequested=true && DrainRunning=false`
- `QuitStarted=true && QuitCompleted=false && DrainRunning=false`  
  現在は A-3 により永続化可能
- `QuitCompleted=true && DrainRunning=true`  
  quit callback 後の再入などで現在は到達可能だが、本来は禁止すべき状態
- `s_state==ShutDown && DrainRunning=true`  
  `ShutdownWithDrain` の初回 yield 前に public `TryShutdown` が完了すると一時的に到達可能

したがって、**ビットの直積が意味のある状態集合より広すぎる一方、必要な状態は不足しています。**

---

## 推奨する単一状態機械

単なる enum 一本ではなく、**1 個の `DrainSession` を唯一の所有者とする状態機械**にすべきです。

```csharp
enum DrainPhase
{
    Scheduled,
    Running,
    RunningWithQuit,
    Settling,
    SettlingForQuit,
    DeliveryQueued,
    Completed
}

sealed class DrainSession
{
    public ulong Generation;
    public DrainPhase Phase;
    public Coroutine? Runner;
    public WindowsClipboardResult? FinalResult;
    public List<Action<WindowsClipboardResult>> Waiters;
}
```

必要な遷移は次です。

```text
Idle
  └─ ShutdownWithDrain → Scheduled → Running
                               └─ quit → RunningWithQuit

Running
  ├─ terminal → DeliveryQueued → delivery claim → Idle
  └─ abort/destroy/disable/exception → Settling → Idle

RunningWithQuit
  └─ terminal/abort → SettlingForQuit
                     → 全 callback を同期配送
                     → ResumeQuit exactly once
                     → Completed
```

重要なのは以下です。

- enqueue 時にはセッションを消さない
- dispatcher 側が generation を `TryClaimDelivery` してから消す
- teardown は同じ claim を同期的に奪える
- `SettlingForQuit` 中の再入は、保存済みの最終結果へ同期合流させる
- `OnDisable` / `OnDestroy` / 例外を正式な遷移にする
- 可能ならコルーチンではなく `Update` 駆動にして、外部の `StopAllCoroutines()` で executor が消えないようにする

---

## 畳まない場合に最低限必要な不変条件

畳まない選択は推奨しませんが、続けるなら次を assertion とテストで固定する必要があります。

1. `s_drainRunning` は「意図」ではなく、実在する executor の所有権と同値。
2. `s_drainWaiters != null => s_drainDeliveryRequested == true`。
3. `s_drainDeliveryRequested` は enqueue ではなく、実配送が claim されるまで true。
4. `s_quitDrainCompleted => s_quitDrainStarted`。
5. `quitStarted && !quitCompleted` なら、live drain または同一スタック内の `Settle → ResumeQuit` が必ず存在する。
6. quit 完了後は新しい drain を開始しない。
7. `ShutDown` / `ShutdownFailed` と live retry executor の併存条件を明文化する。
8. 起動失敗、停止、非アクティブ化、破棄、例外、タイムアウトの全経路が同じ settle 関数を exactly once 通る。
9. 非同期配送は generation/ticket で claim し、teardown と dispatcher が競合しても片方だけが配送する。
10. event の invocation list も購読者ごとに例外隔離する。

これは実質的に、分散した状態機械をコメントで再実装することになります。

---

## B. 検証手段の問題

### B-1. Editor テストが Windows Player 専用コードを一度もコンパイルしない

対象: [WindowsClipboardManager.cs:2592](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2592)、[WindowsClipboardManagerIntegrationTests.cs:3](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:3)

`!UNITY_EDITOR` の分岐を壊しても EditMode/PlayMode は通ります。A-1 が現存していること自体が、この検証穴の再現証拠です。Windows Player compile/build gate が必要です。

### B-2. drain 中断テストが「起動拒否」と「破棄」に限定されている

対象: [WindowsClipboardManagerIntegrationTests.cs:637](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:637)、[WindowsClipboardManagerIntegrationTests.cs:660](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:660)

正常開始後の `SetActive(false)`、`StopAllCoroutines()`、drain 本体の例外がありません。これらで callback、running、quit 再開を観測する必要があります。

### B-3. quit テストが破棄・再入・DeliveryQueued を検証していない

対象: [WindowsClipboardManagerIntegrationTests.cs:680](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:680)

現在のテストは「callback が quit より先」の一点は有効に検出します。ただし callback 内再入、quit 保留中の Manager 破棄、enqueue 後の teardown は未検証です。

### B-4. 複数購読者テストがない

対象: [WindowsClipboardManagerDispatchTests.cs:59](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardManagerDispatchTests.cs:59)、[WindowsClipboardManagerIntegrationTests.cs:707](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:707)

前者は「common 全体と per-call」の隔離、後者は複数 waiter の隔離だけです。common event の第1・第2購読者は区別していません。

### B-5. `SetStateForTests` が本番に存在しない `Running` を作り、成功初期化テストを死なせている

対象: [WindowsClipboardManager.cs:487](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:487)、[WindowsClipboardManagerIntegrationTests.cs:63](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:63)、[WindowsClipboardManager.cs:763](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:763)

`RunningManager()` は `Initialize` を通さず、`Running` なのに native 未初期化・quit handler 未購読という本番到達不能状態を作ります。

そのため `SubscribeQuitHandler()` を削除しても、quit テストは `InvokeWantsToQuitForTests()` を直接呼ぶので通ります。設計 9.2 が求める「成功 Initialize と多重購読防止」は実質未検証です。

---

## C. 記述の問題

### C-1. shutdown origin の説明とシームが既に死んでいる

対象: [WindowsClipboardManager.cs:418](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:418)、[WindowsClipboardManager.cs:427](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:427)

コメントは「origin が quit 再開を決める」としていますが、`FinishShutdownAttempt` では origin は終端ログにしか使われません。テストからも `ShutdownOriginForTests` は一度も参照されていません。

設計 7.4 の「Finish が quit・配送まで決める」という記述も、現在の `DrainRoutine` / `SettleDrain` / `ResumeQuit` 分割と一致していません。

### C-2. result v5 が最新コミットの状態を表していない

対象: [windows-clipboard-implementation-feature-result-v5.md:10](C:/Users/User/Desktop/unity-native-plugin/artifact/results/clipboard/2026-09-07-windows-clipboard-implementation-feature-result-v5.md:10)、[同:17](C:/Users/User/Desktop/unity-native-plugin/artifact/results/clipboard/2026-09-07-windows-clipboard-implementation-feature-result-v5.md:17)、[同:169](C:/Users/User/Desktop/unity-native-plugin/artifact/results/clipboard/2026-09-07-windows-clipboard-implementation-feature-result-v5.md:169)

v5 は v2 対応を対象とし、745/163 と記録していますが、`788f16b` は 748/168 を申告しています。また「テスト 5」としながら列挙・差分は6ファイルです。今回状態の検証記録としては使えません。

---

## v3 対応の判定

| v3 | 判定 |
|---|---|
| A-1 cancellation ticket | **解消**。ticket 所有者照合と競合テストは有効 |
| A-2 drain 起動失敗・中断 | **部分解消**。起動拒否と破棄は解消、起動後の停止・例外が残る |
| A-3 quit 合流後の配送順 | **元の再現は解消**。ただし同期配送中の再入という新しい穴あり |
| A-4 waiter / event 例外 | **部分解消**。waiter は解消、共通 event は未解決 |
| A-5 nested key | **解消**。深さ・escaped quote のテストにも判別力あり |

未解決の v3 指摘番号は **A-2、A-4（ともに部分）**です。

---

## 問題なしと判断した範囲

- cancellation の ticket 所有者確認
- parser のトップレベル必須キー検査
- request registry の `AwaitingNative` / `Undelivered` exactly-once
- drain waiter の個別例外隔離
- COM/provider/cache の完了時のみ解放する条件
- Builder、Parser、RequestTable、結果型の今回差分
- 他プラットフォームファイルへの変更なし
- Editor 用シームは確認した範囲ですべて `#if UNITY_EDITOR` 内。Player への漏出なし
- `git diff --check 284a607..HEAD -- Packages/` は問題なし

Unity とテストは指示どおり起動していません。ファイルも作成・編集していません。既存の未コミット変更 `Assets/Settings/Build Profiles/Windows Profile.asset` にも触れていません。

使用スキル: `review-implementation-feature` — リポジトリ標準の実装レビュー手順に従い、A/B/C と再現可能性で判定しました。