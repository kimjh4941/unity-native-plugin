# レビュー結果

- 日付: 2026-10-11
- 対象ファイル: artifact/android/share/designs/2026-10-11-android-share-design-v6.md
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 6。前のラウンドとは別のインスタンス）と Codex を並行で回した。どちらも v5 との差分だけを、読むだけで確かめた（ワークフローのサーキットブレーカー「5 ラウンドを超えた」に当たるので、最後のレビューとして差分に絞った）。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない。Codex の ID は統合のときに付けた）

---

## 1. サブエージェント（Claude）

範囲は `diff v5 v6` と、v6 全文での grep（coordinator、`OnChooserActionCore`、`Fire`、J-S4、4.3、5.1、5.4、7.2）。照らした一次資料は次の 4 つ。

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/ShareChooserActionCallbackCoordinator.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareManager.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeCallbackQueue.cs`
- `scripts/android_ui_driver.py`

ファイルは何も変えていない。

## 強み

ラウンド 5 の指摘への対応:

| 指摘 | 判定 | 確かめたこと |
|---|---|---|
| C5-2 / R5-4 | 解決 | 5.3 の「per-call を読む時点」の行は消えている（grep で 0 件）。4.3（v6:358）、5.1（v6:399）、5.4（v6:528）、7.2（v6:606）、変更の記録（v6:24）が同じ仕組みを書いており、互いに食い違わない |
| C5-1 / R5-3 | 解決 | 2.2（v6:221）、2.3（v6:245）、J-S2（v6:420）を、端末で Kotlin を通った結果に限った。J-S2 の引用 `Mgr:333-349,399-420` は、`TryPrepareCall` が偽のときの `FireOperationResult` と、dispatcher に積む部分に当たる |
| R5-1 | 解決 | 7.3（v6:612）と 8.3（v6:657）に「失敗・見つからない・期限切れでも、Sharesheet が出ていれば Back で閉じてから `fail`」を書いた |
| R5-2 | 部分的に解決 | R6-3 を参照 |
| R5-5 | 解決 | `Mgr:203-206` に揃えた（v6:348）。変更の記録に R4-7 の 2 点を足した（v6:30-31） |

仕組みの変更（受け口が Android の main で `Fire` を呼ぶ）の検証:

- **1.x と同じスレッド・同じ時点になっている。**
  - 1.x では、proxy の `onChooserAction`（`Mgr:483-485`）が Android の main で `FireChooserAction` → `_chooserCoordinator.Fire` を呼んでいた（`Mgr:433-436`）。
  - `Fire` は `_pending` をその場で写し（`ShareChooserActionCallbackCoordinator.cs:56`）、`_dispatch` に渡していた（`:57-64`）。
  - v6 は `dispatch` を `Post` に替えるだけで、`Fire` の中身は変えない（v6:411）。
  - 引用 `Mgr:52-53,433-436,483-485` と `ShareChooserActionCallbackCoordinator.cs:54-58` は、行の範囲まで合っている。
- **スレッドについて問題はない。**
  - `Post` はどのスレッドから呼んでもよい。`ConcurrentQueue` を使い、ロックを取らない（`AndroidNativeCallbackQueue.cs:70-79`）。
  - `ChooserActionTapped` の購読者と per-call は、取り出し口の中、つまり UnityMain で呼ばれ、それぞれ try/catch で守られている（`:59-63`）。
  - `_pending` を Android の main からロックなしで読むのは 1.x と同じ。参照の読み書きは原子的で、`Register` → `extern` → Android の main への積み込み、の順で前後関係もできる。
  - static の参照を `volatile` にするのも妥当。coordinator は UnityEngine.Object ではないので、Unity の `==` の注意は害のない念押しにとどまる。
- **Editor の継ぎ目が成り立つ。**
  - `OnChooserActionCore` は、型のガード（`UNITY_ANDROID || UNITY_EDITOR`）の中の `internal` の本体。
  - 参照は `Awake` で入れるので、層 2a でも動く。
  - 7.2 の新しいテスト（v6:606）は、Editor の `ShareText` が前半の 3 で coordinator に登録する（v6:488）ので書ける。

## 改善点

### 高優先度

なし。

### 中優先度

**R6-1 J-S4 の「取り出した時点で生きている Manager に出す」が、Chooser Action には当てはまらなくなった。5.3 と 5.1 の書き方がそれを反映していない**

- 節: J-S4（v6:422）、5.3 の J-S4 の行（v6:456）、5.1 の `common.md` の行（v6:409）、7.2 の配送（v6:604）
- 問題:
  - `Fire` は受け口の時点で、その Manager の coordinator の `ChooserActionTapped` と per-call を閉じ込めたラムダを `Post` する（`ShareChooserActionCallbackCoordinator.cs:56-64`）。取り出すときに、Manager が生きているかは見ない。
  - `OnDestroy` の `Clear()` は `_pending` を消すだけで、写した分には効かない（`Mgr:134`、`ShareChooserActionCallbackCoordinator.cs:43`）。
  - そのため、次のどちらの場合も、アクションは壊れた Manager の購読者と前の per-call に出る。新しい Manager には出ない。
    - タップを受けてから取り出すまでの間に Manager を壊した場合。
    - その間に Manager を壊して作り直した場合。
  - 一方、J-S4 の本文と 5.3 の行の 2.0.0 の列は、すべての結果について「取り出した時点で生きている Manager に出し、いなければ捨てる（作り直していれば新しい Manager に出る）」としている。
  - J-S4 の末尾に足した「Chooser Action は、受け口の時点で Manager がいなければ捨てる」は、受け口の時点で決めることを述べているだけで、受けた後の扱いは書いていない。
  - 実際の振る舞いは 1.x と同じ。5.3 の 1.x の列にも「壊す前に積まれた結果は、壊した Manager のイベントと per-call に出た」とある。
  - 起きるのは、Manager を壊す 1 フレームの窓だけ。ふつうの使い方は変わらない。移行ガイドが Chooser Action について 1.x との差を言い過ぎる、という記述の誤りにとどまる。
- 区分: C
- 修正案:
  - J-S4 の末尾を次にする。「Chooser Action は受け口の時点の Manager の coordinator に閉じ込める。いなければ捨てる。受けた後に Manager を壊しても、そのイベントと per-call に出る（1.x と同じ。4.3）」。
  - 5.3 の J-S4 の行の 2.0.0 の列に「（Chooser Action を除く。1.x と同じ）」を足す。
  - `common.md` に書く J-S4 の説明（v6:409）にも同じ例外を書く。
  - 7.2 の Chooser Action のテストに 1 行足すとよい。「`OnChooserActionCore` → Manager を壊す → 1 フレーム待つ → 取り出し口を回す、の順で、壊した Manager の購読者に 1 回出る」。

**R6-2 static の参照を `Awake` のどこで入れるかが書かれていない。重複した Manager がこの参照を上書きすると、生きている Manager にアクションが届かなくなる**

- 節: 5.1 の `AndroidShareManager.cs` の行（v6:399）、4.3（v6:358）、J-S15（v6:433）、7.2 の `OnDestroy` のテスト（v6:600）
- 問題:
  - v6 は「`Awake` で入れ、`OnDestroy`（自分が `_instance` のとき）で消す」とだけ書いている。
  - 1.x の `Awake` は、重複のとき `Destroy(gameObject); return;` で抜け、それより後で初期化していた（`Mgr:105-121`）。
  - 参照を重複の判定より前で入れると、次のことが起きる。
    1. シーンを読み直して 2 つ目の Manager ができると（R3-1 と同じ場面）、2 つ目の coordinator が参照を上書きする。
    2. 2 つ目の Manager は `_instance` ではないので、その `OnDestroy` は参照を消さない。
    3. 以後のアクションは、壊れた 2 つ目の coordinator（購読者がおらず、per-call も無い）に入る。
    4. 生きている Manager の `ShareChooserActionTapped` は黙って出なくなる。
  - Manager をシーンに置く使い方はふつうの使い方なので、実装したときの穴になりうる。
  - J-S15（`OnDestroy` の決定）の消す物の一覧にも、この参照が無い。
  - 7.2 の `OnDestroy` のテスト（v6:600）は、per-call、`onSelected`、取り消す ID の一覧だけを確かめていて、この参照を見ていない。
- 区分: C（記述の限定。7.2 に 1 行足す）
- 修正案:
  - 5.1 と 4.3 を「`Awake` で自分が `_instance` になったとき（重複の `return` の後）に入れる」にする。
  - J-S15 の消す物の一覧に「coordinator の参照（`Clear()` の前に null にする）」を足す。
  - `SubsystemRegistration` と `ResetStaticStateForTests()` がこの参照も戻すことを明記する。層 2a のテストは `SetUp` の後に Manager を作る。
  - 7.2 の `OnDestroy` のテストに 1 行足す。「2 つ目の Manager が壊れた後の `OnChooserActionCore` が、生きている Manager の `ShareChooserActionTapped` に 1 回出る」。

### 低優先度

**R6-3 R5-2 の「最悪の時間」が、R5-1 で足した閉じる手順の読み取りと、adb の既定の timeout を数えていない**

- 節: 8.3（v6:657）
- 問題:
  - v6 は「最悪の時間は 60 秒と、読み取り 1 回（adb の呼び出し 3 つ）と Back。`Wait` の 90 秒はその余裕を見た値」と書いている。
  - しかし、読み取り 1 回の上限は次の合計で 75 秒になる（`android_ui_driver.py:208,214,348-352`）。
    - `rm -f`（既定の 30 秒）
    - `uiautomator dump`（15 秒）
    - `exec-out cat`（既定の 30 秒）
  - さらに、R5-1 で足した「Sharesheet が画面にあれば Back」は、在るかを確かめるために読み取りがもう 1 回要る。
  - 理屈の上の最悪は 60 秒 + 読み取り 2 回 + Back で、90 秒に収まらない。ふつうは数秒で終わるので、ふつうの使い方には響かない。R5-2 が挙げた「文書の上の保証になっていない」は残っている。
- 区分: B（軽い。テストの補助の失敗の経路）
- 修正案: 次のどちらかにする。
  - 「Sharesheet の命令の中の adb の呼び出しは、残り時間（と既定の値の小さい方）を timeout にする」。そうすれば上限が 60 秒 + Back になり、90 秒の主張が成り立つ。
  - 「90 秒はふつうの場合の余裕。adb が詰まったときの上限は保証しない」と書く。

## 不足項目

- Chooser Action を J-S4 の例外とすること。受け口の時点の Manager に閉じ込め、受けた後に壊れても、その Manager に出る（R6-1）。
- static の参照を入れる時点を、重複の判定の後に限ること。J-S15 と 7.2 にも入れる（R6-2）。

## 総合評価

- ラウンド 5 の 7 件は、R5-2 の一部を除いて意図どおりに直っている。
  - 直した 7 件: R5-1、R5-3、R5-4、R5-5、C5-1、C5-2、R5-2 の書き換え。
  - C5-2 は、理由を書き直すのではなく仕組みを 1.x と同じ時点に戻した。この判断は一次資料と合っている。1.x の proxy は Android の main で `Fire` を呼び、`Fire` はその場で `_pending` を写していた。
- 仕組みの変更は、スレッド、`Post` の安全性、購読者を呼ぶスレッド、Editor の継ぎ目のどれにも問題が無い。4.3、5.1、5.4、7.2 の記述も互いに合っている。
- 新しく出た指摘は、どれもこの変更の周りの記述の範囲の話。
  - R6-1: J-S4 の例外の書き漏れ。振る舞いは 1.x と同じ。
  - R6-2: `Awake` で入れる位置の限定。実装でまず踏まないが、書いておくべき前提。
  - R6-3: テストの補助の時間の見積もり。
- 区分ごとの件数:
  - A1: 0 件
  - A2: 0 件
  - B: 1 件（R6-3）
  - C: 2 件（R6-1、R6-2）
- **A1 は 0。** 止める基準を満たす。R6-1〜R6-3 は、実装と補助の設計書 v6 のときに反映すれば足りる。

## 2. Codex

- **C6-1（B）8.3 — 90秒の `Wait` は最悪時間を覆わない。** 根拠: artifact/android/share/designs/2026-10-11-android-share-design-v6.md:657、scripts/android_ui_driver.py:208、scripts/android_ui_driver.py:348。期限直前に始まった画面読み取りは adb 3回で最大75秒、その後の画面確認・Backも加わり得るため、補助の応答が90秒を超えてテスト側が先にタイムアウトする。R5-2は未解決。

（Codex の出力はこの 1 件だけ。A1 は無い）

## 3. 統合（止める基準の判定）

**サブエージェントも Codex も A1 0 件。止める基準を満たしたので、設計レビューを止める。** 基準の 1（レビュアーを替えて A1 が 0）はラウンド 5（サブエージェント）とラウンド 6（両方）で満たした。

**設計 v6 から実装へ持ち越す項目（直さずに先へ。実装のチェックリストとして実装結果ファイルに写す）:**

| # | 内容 | 反映先 |
|---|---|---|
| K-1 | R6-1（C）: Chooser Action は J-S4 の例外。受け口の時点の Manager の coordinator に閉じ込め、いなければ捨てる。受けた後に Manager を壊しても、そのイベントと per-call に出る（1.x と同じ）。5.3 の J-S4 の行と `common.md` に例外として書く。7.2 に「壊した後に取り出すと、壊した Manager の購読者に 1 回」のテスト | 実装（手順 7）、`common.md`（手順 9）、実装結果ファイルの 5.3 |
| K-2 | R6-2（C）: coordinator の static の参照は、`Awake` で自分が `_instance` になった後（重複の `return` の後）に入れる。J-S15 で `Clear()` の前に null にする。`SubsystemRegistration` と `ResetStaticStateForTests()` で戻す。7.2 の `OnDestroy` のテストに「2 つ目の Manager が壊れた後の `OnChooserActionCore` が、生きている Manager に 1 回出る」 | 実装（手順 7） |
| K-3 | R6-3 / C6-1（B）: Sharesheet の命令の中の adb の呼び出しは、残り時間（と既定の値の小さい方）を timeout にし、上限を 60 秒 + Back にする | UI の補助の設計書 v6（手順 2） |
