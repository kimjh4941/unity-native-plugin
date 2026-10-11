# レビュー結果

- 日付: 2026-10-11
- 対象ファイル: artifact/android/share/designs/2026-10-11-android-share-design-v5.md
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 5。前のラウンドとは別のインスタンス）と Codex を並行で回した。どちらも v4 との差分を中心に、読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない。Codex の ID は統合のときに付けた）

---

## 1. サブエージェント（Claude）

範囲は、`diff v4 v5` と、直した話題を v5 全文で grep した結果（per-call を読む時点、Editor のフレーム、6 個以上の警告、`lock`、`OnDestroy`、`AddSelectionIdForTests`、60 秒、`present-in-sheet`）。照らした一次資料は次のとおり。

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Common/UnityMainThreadDispatcher.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeCallbackQueue.cs`
- `scripts/android_ui_driver.py`
- native-toolkit のタグ `1.12.0` の `UnityAndroidShareManager.kt`

ファイルは何も変えていない。

## 強み

ラウンド 4 の指摘はすべて意図どおりに直っている。

| 指摘 | 判定 | 確かめたこと |
|---|---|---|
| R4-1（A1） | 解決 | 5.3 の行（v5:437）と J-S2（v5:409）を、Editor と端末の C# 側の即時の失敗に限った。1.x の dispatcher は `lock` を持ったまま `while` を回す（`UnityMainThreadDispatcher.cs:57-62`）。UnityMain から入り直す `Enqueue`（`:45`）だけが同じループに入る。タグ 1.12.0 の Kotlin は、parser の失敗を含めて、すべての操作を `executeOperationOnMain` で Android の main に積む（`UnityAndroidShareManager.kt:143-315,317-321`）。このため「端末で Kotlin を通った結果は、もとから次のフレーム以降」は、不正な入力でも正しい。2.0.0 の列の「端末の正しい入力の操作では変わらない」も正しい。Direct Share と取り消しは同期で `Post` されるが、ループの中から呼べば次のフレームになる |
| C4-1 / R4-4（A1 / C） | 解決 | v5:471。6 個以上の警告は `ShareText` だけ。`ShareWithCallback` は「使わない」の警告だけ。1.x の `Mgr:203-206`（`> 5` で警告）、`:308-311`（`> 0` で警告）と同じ。2.1（v5:195、197）、4.2（v5:316）、5.4 の `ShareWithCallback` の段落（v5:480）とも合う |
| R4-2（A2） | 解決 | 5.1 の継ぎ目に `AddSelectionIdForTests(ulong)` を足した（v5:388）。J-S15 は、Editor では F-9 を呼ばず一覧を空にするだけ（v5:422）。7.2 は状態を入れ、2 つ目を作り、1 フレーム待って確かめる（v5:590）。層 2a は PlayMode なので、`Destroy` と 1 フレーム待つ書き方は使える（v5:394、583）。per-call が残っていることは、今の継ぎ目（`AddCall` → `OnDoneCore` → 取り出し口を回す）で確かめられる |
| R4-3 / C4-2（C） | 解決 | v5:388 で、登録の印を J-S6 の登録専用の `lock` に分けた。J-S6（v5:413）と矛盾しない |
| R4-5（B） | 概ね解決（失敗の経路に穴。R5-1、R5-2） | 7.3（v5:602）と 8.3（v5:647）に、命令の全体の 60 秒の期限と `fail` を書いた。`dump` が 1 回で最大 15 秒かかることは、`android_ui_driver.py:349` の `timeout=15` と合う |
| R4-6（B） | 解決 | `present-in-sheet` を 8.3（v5:648）に足した。7.3（v5:606）で使う。Sharesheet の命令は `wait` の `keep_last_dump` を通らないので、プライバシーの決まりとも合う |
| R4-7（C） | 解決（理由の書き方に限定の漏れ。R5-4） | 親課題の行を J-1〜J-11 にした（v5:9）。UI の補助の行に v6 を書いた（v5:15、401）。Chooser Action の行に、J-11 の外とみた理由を書いた（v5:438） |

新しく足した引用は、どれも一次資料と合っていた。

- `UnityMainThreadDispatcher.cs:45,57-62`: `Enqueue` の `lock` と、`Update` の `lock`・`while`・`Invoke`
- `Mgr:203-206,308-311`: 2 つの `LogWarning`
- `Mgr:333-349`: `TryPrepareCall` が偽のときの `FireOperationResult`、`Call`、`catch` の `FireOperationResult`
- `AndroidNativeCallbackQueue.cs:168-179`: 始めの件数だけを回す `Update`

直した所どうしの矛盾（per-call、Editor の `OnDestroy`、`lock`、6 個以上の警告）は見つからなかった。

## 改善点

### 高優先度

なし。

### 中優先度

**R5-1 Sharesheet の命令が期限切れや見つからないときに `fail` を返しても、Sharesheet を閉じない。Unity が止まったままになりうる**

- 節: 7.3（v5:602、604）、8.3（v5:647、648）、8.2（v5:636）
- 問題:
  - v5 は、期限を超えたら「探すのをやめて `fail` を返す」と書いた。Back で閉じるのは、`absent-in-sheet` と `present-in-sheet` が「確かめた後」だけ（v5:648）。
  - 次の場合に、補助が Sharesheet を出したまま終わる。
    - `tap-in-sheet` が見つけられない。
    - `present-in-sheet` が期限を超える。
  - 7.3 と 8.2 は、Sharesheet が出ている間は Unity が止まる見込みとしている（v5:602、636）。そうなら、C# の `Wait`（90 秒）も、テストの後片付けの「Sharesheet が残っていれば Back で閉じる」（v5:604）も動かない。テストは失敗にならず、止まったままになる。
  - 「出した依頼は、失敗の経路でも必ず `Wait` する（後片付けで回収する）」（v5:602）は、Unity が動いていることが前提なので、ここでは効かない。
- 区分: B（失敗したときの経路のテストの穴。ふつうの使い方の契約には響かない）
- 修正案:
  - 8.3 の「Sharesheet の命令はどれも…」の箇条に、次の 1 文を足す。「期限切れや見つからないときも、Sharesheet のパッケージ（`com.android.intentresolver`）が画面にあれば Back で閉じてから `fail` を返す」。
  - 7.3 の約束にも同じことを書く。

### 低優先度

**R5-2 60 秒の期限は手順の合間に見るので、最悪の時間は「60 秒 + 1 回の読み取り」になる。`dump` の上限の 15 秒は、`uiautomator` の部分だけ**

- 節: 8.3（v5:647）
- 問題:
  - 今の補助は、期限を `dump` が終わった後に見る（`android_ui_driver.py:357-366`）。
  - `dump` は 3 つの adb 呼び出しでできている（`:348-352`）。
    - `rm -f`（既定の 30 秒）
    - `uiautomator dump`（15 秒）
    - `exec-out cat`（既定の 30 秒。`:208`、`:214`）
  - そのため「1 回で最大 15 秒」は `uiautomator` の部分だけの話で、理屈の上では 60 秒の期限の後に、さらに読み取り 1 回と Back が加わる。ふつうは 90 秒に収まるが、文書の上では保証になっていない。
- 区分: B（軽い）
- 修正案: 8.3 に次のどちらかを書く。
  - 「Sharesheet の命令の中の adb 呼び出しは、残り時間を timeout にする」。
  - 「上限は 60 秒 + 読み取り 1 回。`Wait` の 90 秒はその余裕を見た値」。

**R5-3 2.2 / 2.3 と J-S2 に「1.x の結果はすべて後のフレーム、すべて Kotlin を通る」が残っており、v5:437 と食い違う**

- 節: 2.2（v5:210）、2.3 の表の最後から 3 行目（v5:234）、J-S2（v5:409 の末尾）
- 問題:
  - v5:437 は今回、1.x では「Editor（と端末の C# 側の即時の失敗）では同じループ（同じフレーム）で結果が出た」とはっきり書いた。
  - 一方で、ほかの 3 か所は直していない。
    - 2.2（v5:210）: 「すべての結果は `UnityMainThreadDispatcher` を通って後のフレームに出る（Kotlin が main に積み…）」
    - 2.3（v5:234）: `TryPrepareCall` が偽のとき「イベントは出る（後のフレーム）」
    - J-S2 の末尾: 「1.x はどの結果も Kotlin と dispatcher を通っていた」
  - Editor の 1.x は Kotlin を通らない（`Mgr:333-336`）。同じフレームに出ることもある（`UnityMainThreadDispatcher.cs:45,57-62`）。
  - 5.3 が正しいので振る舞いの誤りではないが、移行ガイドを書く人が 2 章を読むと逆の結論になる。
- 区分: C
- 修正案:
  - 2.2 と 2.3 の「後のフレーム」を「呼び出しの後に出る（Editor と C# 側の即時の失敗は同じフレームのこともある。5.3）」にする。
  - J-S2 の末尾は「端末の 1.x の結果はどれも Kotlin と dispatcher を通っていた」に限る。

**R5-4 R4-7 で足した「J-11 の外とみた理由」の場面の限定が、言い過ぎている**

- 節: 5.3 の Chooser Action の per-call の行（v5:438）
- 問題:
  - 「そこで新しい `ShareText` を呼ぶのは `OnApplicationPause(false)` / `OnApplicationFocus(true)` の中から呼ぶときに限られる」と書いている。
  - 実際には、`AndroidNativeCallbackQueue` に実行順の指定が無い（`AndroidNativeCallbackQueue.cs:32` に `DefaultExecutionOrder` が無い）。そのため、戻った最初のフレームで取り出し口より先に走る、利用者の別の `Update` から呼んでも同じことが起きる。
  - さらに、「Unity が戻る最初のフレーム」という窓は、Sharesheet が出ている間に Unity が止まること（5.5 の手順 2 で要検証）が前提になっている。
  - 結論（ふつうの使い方の外）は変わらない。
- 区分: C
- 修正案: 「限られる」を「主に `OnApplicationPause(false)` / `OnApplicationFocus(true)`、または取り出し口より先に走る `Update` の中から呼ぶとき（どちらも 1 フレームの窓。窓の形は 5.5 の手順 2 の結果で直す）」にする。

**R5-5 細かい整合**

- 区分: C
- 4.2.1 の手順 6（v5:337）は `Mgr:202-206`、5.4 の前半の 3（v5:471）は `Mgr:203-206` で、同じ警告を指す行の範囲が違う。`:202` は `Debug.Log` なので、`203-206` に揃える。
- 「v4 からの主な変更」（v5:22-29）に、R4-7 のうち次の 2 つが書かれていない。差分を追う人のために 1 行足すとよい。
  - 親課題の行を J-1〜J-11 にした。
  - UI の補助の行に v6 を書いた。

## 不足項目

- Sharesheet の命令が失敗や期限切れで終わるときに、Sharesheet を閉じること（R5-1）。
- 60 秒の期限が、読み取りの途中で超えたときの扱い（R5-2）。

## 総合評価

- ラウンド 4 の 9 件（R4-1〜R4-7、C4-1、C4-2）は、すべて意図どおりに直っている。
  - A1 だった R4-1 と C4-1 は、一次資料と照らして正しくなった。
    - R4-1: タグ 1.12.0 の Kotlin は、すべてを main に積む。
    - C4-1: 1.x の 2 つの警告の条件と合う。
  - A2 の R4-2 は、継ぎ目・Editor のガード・1 フレーム待つことの 3 点が揃い、テストを書ける。
  - 新しい引用 4 つも、行の範囲まで合っていた。
- 新しく出た指摘は、どれもテストの補助の失敗の経路か、記述の食い違い。ふつうの使い方の振る舞いや契約の誤りではない。
  - R5-1 は、Unity が止まる見込み（要検証）が当たったときに、テストが止まったままになる穴。補助の設計書 v6 に 1 文足せば直る。
- 区分ごとの件数:
  - A1: 0 件
  - A2: 0 件
  - B: 2 件（R5-1、R5-2）
  - C: 3 件（R5-3、R5-4、R5-5）
- **A1 は 0。** 止める基準を満たす。R5-1〜R5-5 は、補助の設計書 v6 と実装のときに反映すれば足りる。

## 2. Codex

確認済みの欠陥は 2 件です。

- **C5-1（A1）5.2 J-S2 — 1.x の全結果が Kotlin を通るという誤記**
  - 証拠: artifact/android/share/designs/2026-10-11-android-share-design-v5.md:409、Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareManager.cs:333、同 :399
  - シナリオ: Editor、または端末で `TryPrepareCall` が失敗した場合、`FireOperationResult` から dispatcher に直接積まれ、Kotlin は通らない。同じ行の直前の説明とも矛盾する。

- **C5-2（A1）5.3 Chooser Action の per-call 読み取り時点 — J-11 の外とする理由が誤り**
  - 証拠: artifact/android/share/designs/2026-10-11-android-share-design-v5.md:438、Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeCallbackQueue.cs:168
  - シナリオ: Sharesheet から戻った最初のフレームで、queue より先に実行された通常の `MonoBehaviour.Update` から正しい入力で `ShareText` を呼べる。したがって新しい呼び出しは `OnApplicationPause(false)` / `OnApplicationFocus(true)` に限られず、J-11 の通常利用から除外する根拠にならない。

（Codex の出力はこの 2 件だけで、ラウンド 4 の指摘ごとの解消の判定と、引用ごとの正否は含まれていない。欠陥として挙がらなかった項目は問題なしとして扱う）

## 3. 統合（止める基準の判定）

サブエージェントは A1 0 件。Codex は A1 2 件だが、サブエージェントは同じ所を C（R5-3 = C5-1、R5-4 = C5-2）とした。

- C5-1 / R5-3: 記述の範囲の誤り。v6 で 2.2・2.3・J-S2 を直す
- C5-2 / R5-4: Chooser Action の per-call を読む時点。ラウンド 3 から続いている論点で、「ふつうの使い方の外」とみる理由がレビューのたびに揺れる。そこで v6 では、理由を書き直すのではなく、1.x と同じ時点にする（今の coordinator への参照を static に置き、アクションの受け口が Android の main でその `Fire` を呼ぶ。1.x の proxy と同じ。5.3 の行は消える）。仕組みは 1 つの static の参照だけ
- R5-1（B）は v6 で直す（Sharesheet の命令は、失敗でも Sharesheet を閉じてから `fail`）。R5-2、R5-5 も v6 で直す

ワークフローのサーキットブレーカー（5 ラウンドを超えた）に当たるので、v6 の後のレビューは v5 との差分だけを確かめる短いもの（ラウンド 6）にし、そこで止める。ラウンド 3 からは仕組みの誤りは出ず、指摘は記述の範囲と、1 つの論点（C5-2）に収まっている。
