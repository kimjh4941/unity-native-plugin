# native-toolkit への申し送り（下書き）: 同じフレームで 2 回呼んだ共有の 1 つに完了が来ない

- 記録日: 2026-11-15
- 宛先: native-toolkit（iOS の C ABI `2.0.0`、`ios-native-toolkit-capi-2.0.0.xcframework`）
- 出どころ: unity-native-plugin の iOS Share の移行（[2026-11-15-ios-share-cabi-implementation-result-v1.md](2026-11-15-ios-share-cabi-implementation-result-v1.md) の 2 章、実装のレビュー [../reviews/2026-11-15-ios-share-cabi-implementation-review-v1.md](../reviews/2026-11-15-ios-share-cabi-implementation-review-v1.md) の 1.2）
- 状態: **下書き**。利用者が目を通してから出す

## 1. 何が起きるか

主スレッドで `ntk_share_content_async` を続けて 2 回呼ぶ（同じフレーム、どちらも受け付けられて `NONE` が返る）と、次のようになる。

- 共有のシートは 1 つだけ出る。
- シートを閉じると、完了は 1 回だけ来る（`completed = 0`、`activity_type = NULL`）。
- もう 1 つの呼び出しには、**30 秒待っても完了が来ない**。失敗（`SHOW_FAILED` など）も来ない。

`Share.h:30-31` は「受け付けた呼び出しには完了がちょうど 1 回」と約束している。約束の外とされているのは「ホストが window / root を替えた」「システムが scene を切った」場合だけで、この場面は当たらない。

## 2. 再現

| 項目 | 内容 |
|---|---|
| 版 | native-toolkit `2.0.0`（`dist/1.14.0` に入れた `ios-native-toolkit-capi-2.0.0.xcframework`） |
| 環境 | シミュレーター（iPhone 16 / iOS 18.3）と実機（iPhone XS / iOS 18.7.2）の両方で、毎回起きた |
| 呼び方 | 主スレッドで、同じフレームに続けて 2 回。項目はどちらも文字 1 つ（`"first"`、`"second"`）。`user_data` は呼び出しごとに別の値、`release` は NULL |
| 手順 | シートが出たら閉じる（XCUITest で Close を押す）。その後 30 秒待って、各呼び出しの完了を数える |
| 結果 | 完了は 1 回だけ。どちらの `user_data` に来るかは、Unity 側の記録では 2 つ目の呼び出し |

Unity 側では、どの呼び出しの完了も同じコールバック 1 つで受け、`user_data` で振り分けている。コールバックが 1 回しか呼ばれていないことは、完了のたびに必ず出す共通のイベントが 1 本だったことで確かめた。Unity 側の拒み・例外・使えないときの経路は、どれもログか結果の行を出すが、どれも出ていない。

参考: 1.x（`1.13.0` のブリッジ）でも同じく、シートは 1 つで、もう 1 つの呼び出しには何も来なかった。2.0.0 の「呼び出しごとにちょうど 1 回」でも救えていない。

## 3. 原因の見立て（推測。確かめていない）

2 つ目の要求が受け付けられた後に完了が来ないのは、`ShareSheetPresenter.present` の continuation が再開されない場合だけに見える。箱を閉じるのは `completionWithItemsHandler`、手順の失敗、`ReleaseWatch.deinit` の 3 つ。

- **(a) 2 つ目のシートが UIKit に持たれたまま、表示も解放もされない**（いちばん有力）
  - `UIActivityViewController` の `presentingViewController` は、`present` から 0.08〜0.9 秒後に初めて入る（stage1b 作業記録 7.7 の MS-03）。
  - そのため同じフレームの 2 つ目は、ルートがまだ何も出していないように見えて待たずに `present` し、遅れて始まる 2 つの表示が競合して断られる、と見る。
  - 「断られたシートは約 0.04 秒で解放」（SR-16）は出し終えた VC に出した場合の実測で、遅れて始まる表示同士の競合は測られていない。
  - シート 2 が持たれ続けると `ReleaseWatch` が働かない。
- **(b) 手順の待ちで止まる**
  - 2 つ目の手順がシート 1 を最上位と見て `waitForTransition` に入る（`PresentationProcedure.swift:212, 283-294`）。
  - その coordinator の completion が呼ばれないと、再試行も表示も起きない。見張りはまだ作られておらず、待ちに時間の上限も無い。

## 4. 見分け方

native の `.debug` のログで、2 つ目の要求の流れを見る。

```
log stream --level debug --predicate 'subsystem BEGINSWITH "com.jonghyunkim.nativetoolkit"'
```

- `[UIKit.waitForTransition]` の後に `[attempt] retried: true` が来なければ (b)。
- `[show]` と `ReleaseWatch [init]` の後に `[deinit]` が来なければ (a)。
- あわせて、UIKit の "Attempt to present … which is already presenting" の行と、Xcode のメモリーグラフでシート 2 を持っているものを見る。

`IosLibraryCApiTests/ShareTests.swift` の `testTwoSharesCompletedInTheOtherOrder`（392-422 行）は偽の `sharing` を使っているので、本物の UIKit で 2 つの共有が重なる場合は確かめていない。

## 5. 直し方の候補

- 表示が保留中・表示中のあいだは、2 つ目を待ち行列に入れる。
- または、2 つ目を `SHOW_FAILED` で早く完了させる。
- または、表示の待ちと遅れた表示に時間の上限を付け、超えたら失敗で完了させる。

どれでも「受け付けた呼び出しにはちょうど 1 回」が保たれる。

## 6. unity-native-plugin 側の扱い

- 来ない呼び出しの受け手は、呼び出しごとの表に残る（起きるたびに 1 つ増える）。
- UI テスト `ShareUITests.test_SH13` は、約束（各呼び出しに 1 本、イベント 2 本）を `XCTExpectFailure` の中で照らしている。native-toolkit が直れば、期待した失敗が起きずにテストが落ちるので気づける。
- C# で 2 つ目を待たせる回避は、1.x にも無い振る舞い（シートが続けて出る）になるので入れていない。
