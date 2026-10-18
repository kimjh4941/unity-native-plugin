# 実装レビュー結果 v2（Android Clipboard の C ABI 2.0.0 への移行）

- 日付: 2026-10-12
- レビュー対象: ブランチ `feature/UNT-13` の未コミットの差分（実装のレビュー v1 の指摘を直した後）
- 実装計画: `artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md`
- 実装結果: `artifact/android/clipboard/results/2026-10-12-android-clipboard-implementation-feature-result-v2.md`
- レビュアー: サブエージェント（ラウンド 2。前のラウンドとは別のインスタンス。1 章）と Codex（2 章）を並行で回した。どちらも読むだけ。どちらの出力も手を加えていない
- A: サブエージェント 1 件（S2:A-1）、Codex 2 件（X2:A-1、X2:A-2）。S2:A-1 と X2:A-1 は同じ種類（開始の失敗の戻し方と、別のスレッドとの競合）
- 直したことは 3 章（取りまとめ役が書いた）

---

## 1. サブエージェント（Claude）

ファイルも git も読んだだけで、何も変えていません。

### 1. v1 の指摘の判定

| 指摘 | 判定 | 根拠 |
|---|---|---|
| S:A-1 / X:C-1 | 解決（ただし新しい縁あり → S2:C-1） | `RaiseChanged` が先頭で `s_observing` を見直す（`AndroidClipboardManager.cs:754`）。2a の `Change_QueuedBeforeStopObserving_IsNotRaisedAfterIt` は、見直しを消すと `_changes == 1` になって落ちる |
| S:A-2 | 解決（ただし新しい競合あり → S2:A-1） | `wasObserving` を取っておき、例外のときも拒否のときも戻す（`:489-505`）。`Start_ASecondStartThatFails_KeepsTheFirstObserving` は、戻し先を `false` にすると落ちる |
| X:A-1 | 解決 | `AsPassed(payload.texts)` が `Invoke` の lambda の中に入った（`:286`）。継ぎ目は作らず、コードレビューで押さえる判断（v1 の 3 章） |
| X:B-1 | 解決 | `ReadAndFree` を作り、2a で「読めたとき」と「読む関数が投げたとき」に解放が 1 回だけ呼ばれることを見ている。`Read` / `GetDescription` がこれを使っていること（`:357`、`:434`）はコードで確認した |
| X:B-2 | 解決 | `Start_RegistersAgainAfterAFailedRegistration`、`Start_WithoutAListener_DoesNotStart` を足した。公開の `StartObserving` から `StartObservingCore` への配線は、実機の AC-21a / AC-31 が落ちることで守られる |
| X:B-3 | 解決（小さな穴が残る → S2:B-1） | 受け付け・拒否・例外の 3 本を足した |
| S:B-1 | 解決 | 後片付けで `AssertEventsOnMainThread()` を呼ぶ。数の照合を省いたことは実装結果 v2 の 1.2 に差分として書いてある |
| S:B-2 | 解決（記録で対応） | 実装結果 v2 の 1.2 に「コードレビューで押さえる配線」として書いた。列挙された 5 つの配線（`Initialize`→`EnsureChangeListener`、`StopIfObserving` のネイティブの判定、`LogFailure` の重さ、`FreeAll`、MIME の NULL）は、いずれもコードで確認した |
| S:C-1 | 解決 | `StopObserving` の XML に「プロセス全体の監視を止める（Unity が始めていなくても）」が入った（`:311-312`） |
| S:C-2 / X:C-2 | 解決（ごく小さな残り → S2:C-3） | 指摘された行（設計 v4、キューの記述、コメント 2 か所）は直った。ただし `SetUp` の `Assert.Ignore` の文言に「real JNI / IL2CPP behaviour」が残る |
| S:C-3 | 解決 | 実装結果 v2 の 3.4 に書いた（設計の新しい版は作らない、という判断どおり） |
| S:C-4 | 解決 | `Change_ASubscriberThatThrows_IsLoggedAsAnError` に改名した |
| S:C-5 | 解決（軽微な残り → S2:C-3） | `EnsureChangeListener`、`StopIfObserving`、`StartObservingCore` に入口のログを足し、`OnChangedCore` に入れない理由をコメントに書いた。internal の `ReadAndFree` には入口のログも除外の理由も無い |
| S:C-6 | 解決 | `CopyHtmlText` は `EmptyContentMessage` と同じ文言になった。`CopyUri` は `"Invalid URI: <uri>"` で、`OperationFailureMessage` と一致する |
| 要検証（`ResetOnStart`） | 記録済み | 実装結果 v2 の 7 章 |

### 2. 新しい指摘

**S2:A-1（A / low）`StartObservingCore` の戻し方が、別のスレッドの `StopObserving` を打ち消す**
- 場所: `AndroidClipboardManager.cs:489-505`（`wasObserving` を戻す所）、`:319`（`StopObserving` が印を下ろす所）
- 根拠:
  - 次の順で起きると、`StopObserving` の後に印が真に戻る。
    1. スレッド X: 監視中（印が真）に 2 回目の `StartObserving` を呼び、`wasObserving = true` を読む
    2. スレッド Y: `StopObserving` が `s_observing = false` にして、F-10 を積む
    3. スレッド X: F-9 が `UNKNOWN` を返すか例外を投げ、`s_observing = wasObserving`、つまり `true` に戻す
  - ネイティブは F-10 で止まっているのに印は真のままになる。この後で同じアプリの Kotlin の利用者が監視を始めると、その変更が `ClipboardChanged` に出る（AC-32 の約束と、`StopObserving` の XML が破れる）
  - v1 の無条件に `false` に戻す形では、この順は正しく偽で終わっていた。つまり、直したことで新しくできた経路
  - 前提は F-9 の失敗（実機では起こせない）と、別スレッドでの割り込みなので、実際に起きる見込みは非常に低い
- 提案:
  - 失敗のときは「下ろすだけで、上げ直さない」形にする: `if (!wasObserving) s_observing = false;`
    - `wasObserving` が真なら何も書かないので、1 回目の監視は保たれ、Y の `false` も残る
  - 2a に決定的なテストを足せる: 開始の継ぎ目を `() => { AndroidClipboardManager.SetObservingForTests(false); return ErrorUnknown; }` にし、`SetObservingForTests(true)` → `StartObservingCore()` → `IsFalse(IsObservingForTests)`。今の実装ではこのテストが落ちる
  - Start と Stop を別スレッドから同時に呼んだときの最終状態は、成功の経路でも v1 から保証が無い（J-C13 の一般論）。それは残件として実装結果に書けば足りる。完全に揃えるなら、印と F-9 / F-10 を 1 つの lock で囲む（F-9 / F-10 は積むだけで待たないので、lock の中で呼んでも互いに待つ形にはならない）

**S2:C-1（C / low。直し方によっては A）`StopObserving` → `StartObserving` を配送の前に続けて呼ぶと、止める前の変更が出る**
- 場所: `AndroidClipboardManager.cs:309-312`（XML）、`:745-754`
- 根拠:
  - 監視中に `OnChangedCore` が `RaiseChanged` を積む。その後、取り出し口が回る前（同じフレームの中、または別のスレッドから）に `StopObserving()` → `StartObserving()` と呼ぶ
  - `RaiseChanged` が印を見直す時点では印がもう真なので、止める前の変更が出る
  - XML の「no change is raised after this call either way, including one already on its way」は、この形では成り立たない
  - 実害は小さい: 監視をやり直した利用者に、止める前の変更が 1 回余計に届くだけ
- 提案（どちらか）:
  - (a) XML に「配送の前に監視をやり直したときを除く」と足す
  - (b) 世代番号を持つ（`StopObserving` / `StopIfObserving` で増やし、`OnChangedCore` は積むときの値を閉じ込め、`RaiseChanged` で照らす）。この場合は 2a に「`SetObserving(true)` → `OnChangedCore` → `StopObserving` → 開始の継ぎ目を通した `StartObservingCore` → 数フレーム → 0」を足す。今の実装ではこのテストが落ちる

**S2:B-1（B / low）開始のテストで、壊しても落ちない所**
- 場所: `AndroidClipboardManagerEditorTests.cs:323-383`
- (a) 例外の経路の戻し先（`:498`）を `false` に変えても、どのテストも落ちない。2 回目の失敗のテストは拒否（`UNKNOWN`）の経路しか通らない
- (b) J-C10 の「F-9 の前に印を真にする」が守られていない。`s_observing = true` を `StartNative()` の後ろへ動かしても全テストが通る。継ぎ目の中で `IsObservingForTests` を記録して照らせば固められる
- (c) `Start_Refused` と `Start_Throwing` は F-9 を呼んだ回数を数えていない（X:B-3 の提案は回数の照合も含んでいた）
- 提案: 継ぎ目を少し足す程度で 3 つとも固められる。S2:A-1 と S2:C-1 のテストと一緒に足すとよい

**S2:C-2（C / medium）v2 の直しを入れた後の実行結果と件数が、文書に反映されていない**
- 場所:
  - 実装結果 v2 の 4 章と 5 章（表）: 層 2a が「18 本を足した・264 通過」のままで、v1 の内容から変わっていない（`diff` で確認した）
  - 2a の実際の本数は 27 本。`/var/folders/.../unity-android-verify/PlayMode.xml`（14:55 の実行）では Editor の PlayMode が 273 通過、37 スキップで、264 + 9 と合う
  - 5.1 の「2b の各テストは…照らした（AC-20、AC-21a、AC-24）」は、後片付けですべてのテストを照らすようになったので古い
  - 3.1 の E-16「開始の失敗はコードレビュー」と 5.2「`StartObserving` の失敗は起こせない」も古い。今は 2a で確かめている
  - `agent-rules/coding-rules/testing.md` の 2a の行が「`AndroidClipboardManagerEditorTests` 18 本」のままで、並べた観点にも開始と `ReadAndFree` が無い
- 要検証: 実機（IL2CPP）での v2 の通しの結果が、まだ記録されていない
  - レビューの時点で、検証スクリプトが走っている最中に見えた（`Temp` と `Library` が 14:56 に更新され、`Android.log` も同じ時刻のもの）
  - IL2CPP について、コードを読んだ範囲では問題は見つからなかった:
    - `ReadAndFree` に渡す extern を包んだ lambda は、何も閉じ込めていないので、static にキャッシュされる delegate になる
    - `ReadAndFree<ClipContents>` は参照型での実体化なので、共有のコードで済む
    - managed から native への呼び出しなので、`MonoPInvokeCallback` は要らない
  - 実機の結果が出たら、表と 6 章の「v2 は次」を更新すること
- 補足: 同じ実行の副作用で、`Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` と `unity-native-plugin.slnx` が差分に入っている。コミットから外すこと

**S2:C-3（C / low）記述の小さな残り**
- `AndroidClipboardManager.cs:88` のフィールドのコメント（「registration and the native stop」）と `:785` の `SetNativeForTests` の summary に、開始（3 つ目の引数）が書かれていない
  - 補足: 3 つ目の引数の既定値が null なので、2 引数で呼ぶと開始の継ぎ目も戻る。`DestroyCase` では問題にならない。`ResetStaticStateForTests` も開始の継ぎ目を戻しているので正しい
- `AndroidClipboardManagerIntegrationTests.cs` の `SetUp` で、`Assert.Ignore` の文言が「real JNI / IL2CPP behaviour」のまま
- `DrainOperation` の doc の「後片付けではテストを落とさない」と、後片付けで新しく assert することが食い違って読める
  - 実害は無い: UTF（`TestResultExtensions.RecordPrefixedException`）は、後片付けの失敗を元の失敗の文言の後ろに足すので、元の失敗は隠れない
  - ただし、assert の例外は stack trace を上書きする
  - 対応: `TearDownCoroutine` の summary に「イベントのスレッドだけは落とす」と 1 文足すとよい
- `ReadAndFree`（internal）に入口のログも、除外の理由も無い（csharp.md。S:C-5 と同じ観点）
- 実装結果 3.4 の「確保の失敗 → 結果の失敗」は少し言い過ぎ。`Invoke` の `new List<IntPtr>()`（`:555`）、lambda の閉じ込め、ログの文字列の確保は `try` の外にある。「アンマネージの確保と引数の写し」と範囲を絞ると正確になる

#### 調べて、欠陥ではなかった所
- 後片付けの `AssertEventsOnMainThread`
  - 誤って落とすことは無い: 配送はすべてキュー経由で UnityMain で起き、`_mainThread` は `SetUp`（UnityMain）で取っている。`Unsubscribe` の後は新しいイベントが記録されない
  - 本当に落ちる: たとえば `Invoke` が `Post` を使わずに `Deliver` を直に呼ぶと、AC-24 の行の assert を消しても後片付けで落ちる
- `_subscribed` が偽で後片付けを飛ばす経路: AC-31 と AC-34 の (3) の、`Unsubscribe` と `Subscribe` の間だけ。そこにあるのは `Destroy` と `yield return null` だけで、失敗しようがない
- `Change_AfterTheManagerIsDestroyed_IsDroppedWithoutANewManager` は、破棄の後に印を真へ戻しているので、`RaiseChanged` の見直しを入れた後も、`_instance` が null の経路を正しく確かめている
- `Start_Refused` の `UNKNOWN` は `LogWarning` になるので、予期しない `LogError` でテストが落ちることは無い

### 3. 総合評価

**要修正（軽微）。A は 1 件（S2:A-1。low）**
- v1 の 15 件と要検証 1 件は、すべて 3 章の対応どおりに直っているか、記録されている
- 直したことから生まれた新しい欠陥は 2 つ:
  - S2:A-1: `wasObserving` の戻し方と、別スレッドの `StopObserving` の競合（前提は F-9 の失敗で、実機では起こせない）
  - S2:C-1: Stop → Start を配送の前に続けて呼ぶと、止める前の変更が出る
- どちらも数行で直り、2a で決定的なテストを書ける
- 文書の更新漏れ（S2:C-2）は、実機の v2 の結果が出たらまとめて直すこと
- 推測を含む項目は、S2:C-2 の「実機（IL2CPP）での v2 の結果」の 1 件で、「要検証」と付けた

---

## 2. Codex

### 前回指摘の判定

| ID | 判定 | 根拠 |
|---|---|---|
| X:A-1 | 解消 | `AsPassed(payload.texts)` は `Invoke` に渡すラムダ内へ移動している。AndroidClipboardManager.cs:283 |
| X:B-1 | 一部解消 | `ReadAndFree` と、成功時・変換例外時のテストは追加された。ただし `Read` / `GetDescription` の実配線や、content / description それぞれの free 呼び出しを壊しても、テストは落ちない。AndroidClipboardManagerEditorTests.cs:388 |
| X:B-2 | 未解消 | retry の判断はテストされた。しかし、公開 `StartObserving` から `StartObservingCore` を呼ぶ配線を削除しても、全テストが通る。結果報告も、この配線はコードレビューのみとしている。AndroidClipboardManager.cs:474 |
| X:B-3 | 解消 | F-9 の成功、拒否、例外、二度目の失敗、リスナー未登録を、シーム経由で検証している。AndroidClipboardManagerEditorTests.cs:324 |
| X:C-1 | 解消 | `RaiseChanged` でも監視フラグを再確認している。配送待ちのイベントも、停止後は破棄する。AndroidClipboardManager.cs:751 |
| X:C-2 | 解消 | 設計 v4 とキュー配送を示すコメントに更新された。AndroidClipboardManagerIntegrationTests.cs:40 |
| S:A-1 | 解消 | X:C-1 と同じ。`Change_QueuedBeforeStopObserving_IsNotRaisedAfterIt` も追加された。AndroidClipboardManagerEditorTests.cs:254 |
| S:A-2 | 解消 | 失敗時に `wasObserving` へ戻す。二度目の開始失敗のテストもある。AndroidClipboardManager.cs:488 |
| S:B-1 | 一部解消 | teardown で全イベントが UnityMain で実行されたことは確認する。しかし、設計 7.3 が求める、テストごとのイベント数の照合は追加されていない。AndroidClipboardManagerIntegrationTests.cs:147 |
| S:C-1 | 解消 | 公開 `StopObserving` がプロセス全体の監視を止める旨を明記した。AndroidClipboardManager.cs:308 |
| S:C-2 | 解消 | v4・C ABI・キュー配送に更新された。 |
| S:C-3 | 解消 | 結果報告 3.4 で「同じフレームのことがある」と訂正された。 |
| S:C-4 | 解消 | テスト名が `Change_ASubscriberThatThrows_IsLoggedAsAnError` になった。AndroidClipboardManagerEditorTests.cs:229 |
| S:C-5 | 解消 | 入口ログと、`OnChangedCore` の除外理由が追加された。AndroidClipboardManager.cs:669 |
| S:C-6 | 解消 | XML コメントは、エラーコードではなく実際に返すメッセージを説明している。AndroidClipboardManager.cs:215 |

### 新規指摘

#### X2:A-1 (A)
- 場所: AndroidClipboardManager.cs:488
- 根拠: `wasObserving` の取得、`s_observing = true`、F-9、失敗時の復元が、一つの同期操作になっていない。たとえば二つのスレッドがともに `false` を読み、一方の F-9 が成功したとする。その後、他方が失敗して `false` を復元すると、ネイティブは監視中なのに、Unity は以後の変更を無言で破棄する。`volatile` では、この read-modify-rollback の競合を防げない。
- 修正: observation 専用ロックで、Start の状態変更・F-9・rollback を直列化する。同じロックを、`StopObserving` / `StopIfObserving` のフラグ変更と F-10 にも適用する。Barrier を使った、二つの並行 Start のテストを追加する。

#### X2:A-2 (A)
- 場所: AndroidClipboardManager.cs:553
- 根拠: `Invoke` 内の `new List<IntPtr>()` が `try` の外にある。ここで `OutOfMemoryException` が出ると、呼び出し側へ例外が漏れる。これは、設計 5.3/5.4 と結果報告 3.4 の「C# の確保失敗は非同期の失敗結果にする」に反する。`AsPassed` をラムダ内へ移しただけでは、確保をすべては捕捉できていない。
- 修正: 外側の `try/catch` で、一覧の作成から解放までを囲む。解放は内側の `finally` に置く。確保一覧の生成を失敗させられるシームで、例外が漏れず、結果が一度だけ届くことを検証する。

#### X2:C-1 (C)
- 場所: artifact/android/clipboard/results/2026-10-12-android-clipboard-implementation-feature-result-v2.md:163
- 根拠: 現在の `AndroidClipboardManagerEditorTests` は 27 本ある。しかし結果報告は「18 本」「264 通過」のままである。v1 後に追加した 9 本を含む実行結果になっていない。
- 修正: 現在のコードで PlayMode テストを再実行し、件数と結果を更新する。

#### X2:C-2 (C)
- 場所: AndroidClipboardManager.cs:785
- 根拠: `SetNativeForTests` の XML コメントは「registration and native stop」だけを置換すると説明している。しかし現在は、第3引数で native start も置換する。
- 修正: 「registration, native start, and native stop」に更新する。フィールド群のコメントも揃える。

**A の件数: 2**

補足（取りまとめ役の転記）: Codex の出力は、Codex を呼んだ転送役のエージェントがそのまま返したもの。転送役は一次資料を確かめ直していない。

---

## 3. 直したこと（取りまとめ役）

| 指摘 | 区分 | 対応 |
|---|---|---|
| S2:A-1、X2:A-1 | A | 監視専用の `lock`（`s_observeLock`）を足し、開始（印・F-9・失敗の戻し）、`StopObserving`（印・F-10）、`StopIfObserving`（印・F-10）をその中で行う。別のスレッドの開始と停止は、最後のものの状態で終わる。F-9 / F-10 は Android の main に積むだけで待たないので、`lock` の中で呼んでも互いに待つ形にならない。受け口は `lock` を取らない。2a の `Start_AStopFromAnotherThread_WaitsAndWins`（開始の F-9 の中で別のスレッドが止める。止める側が開始を待ち、印は偽で終わる。`lock` を外すと落ちる） |
| S2:C-1 | C（直し方は A の扱い） | (b) を採った。止めるたびに数える世代（`s_generation`）を持ち、受け口は積むときの世代を閉じ込め、`RaiseChanged` で照らす。止める前に積まれた変更は、配送の前に再開しても出ない。2a の `Change_QueuedBeforeAStopAndARestart_IsNotRaised` |
| X2:A-2 | A | `Invoke` の確保の一覧を `try` の中で作る。継ぎ目は作らない（コードレビューで押さえる） |
| S2:B-1 | B | 2a の開始のテストで、F-9 を呼んだ回数と、F-9 の時点で印が上がっていることを照らす。例外の経路の 2 回目の失敗（`Start_ASecondStartThatThrows_KeepsTheFirstObserving`）を足した |
| X:B-2（Codex の判定は未解消） | B | 公開の `StartObserving` から `StartObservingCore` への配線は、実機の AC-21a と AC-31 が落ちることで守られる（サブエージェントの判定と同じ）。2a では Editor の判定で届かない |
| X:B-1（Codex の判定は一部解消） | B | `Read` / `GetDescription` が `ReadAndFree` を使い、それぞれの `_free` を渡す配線は、コードレビューで押さえる（実装結果 v3 の 1.2） |
| S:B-1（Codex の判定は一部解消） | B | イベントの数の照合を足さない理由は、実装結果 v2 / v3 の 1.2 のとおり |
| S2:C-2、X2:C-1 | C | 実装結果 v3 と `testing.md` の件数と記述を、v2 の直しの後の実行に合わせた |
| S2:C-3、X2:C-2 | C | `SetNativeForTests` とフィールドのコメント、`Assert.Ignore` の文言、後片付けの summary、`ReadAndFree` のログを入れない理由、実装結果 3.4 の確保の範囲 |
