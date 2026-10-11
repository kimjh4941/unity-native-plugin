# `UnityMainThreadDispatcher` がロックを持ったまま積まれた処理を走らせる

- 記録日: 2026-10-11
- 分類: 横断課題（`Runtime/Common/` の dispatcher。全 OS の Manager が使う）
- 発見経緯: Android の Dialog を C ABI に移す設計のレビュー（`artifact/android/dialog/reviews/2026-10-11-android-dialog-design-review-v1.md` の H-4）
- 対応方針: 今は直さない。Android の C ABI の受け口は dispatcher を使わず、Manager のロックの無い列に入れる（`artifact/android/dialog/designs/2026-10-11-android-dialog-design-v2.md` の J-D2）。dispatcher を直すと全 OS の配送の時点（同じフレームで続けて走るか）が変わるので、Android の移行と混ぜない
- 進捗: 未着手

## 何が起きるか

`Runtime/Common/UnityMainThreadDispatcher.cs` の `Update` は、`_executionQueue` のロックを持ったまま、積まれた処理（購読者のコードを含む）を順に呼ぶ。その間、ほかのスレッドの `Enqueue` は同じロックで待たされる。

- ネイティブのコールバックのスレッド（Android の main、iOS / macOS のメインキュー、Windows の OS のスレッド）が、購読者の処理が終わるまで止まる
- Android の main が長く止まると ANR になる。native-toolkit の C ABI の約束（第 1 部の設計 1.3.2）も、コールバックの中で main を長く塞がないことを求める
- 購読者が、ネイティブのコールバックのスレッドを同期で待つ処理を呼ぶと、互いに待って止まる

## 直し方の案

`Update` でロックの中では列を取り出して入れ替えるだけにし、ロックの外で走らせる。ただし次が変わるので、各 OS の Manager の前提を確かめてから行う。

- 走らせている処理が `Enqueue` したものは、今は同じフレームで走る（同じスレッドなのでロックに再入できる）。直すと次のフレームになる
- 処理が例外を投げたとき、残りの処理の扱い（今は列に残り、次のフレームで走る）

関連: [event-subscriber-isolation](../event-subscriber-isolation/README.md)（1 人の購読者の例外が後続を止める）
