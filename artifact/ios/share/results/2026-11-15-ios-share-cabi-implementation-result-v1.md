# iOS Share の C ABI `2.0.0` への移行の実装の記録 v1

- 記録日: 2026-11-15
- 設計: [../designs/2026-11-15-ios-share-cabi-design-v3.md](../designs/2026-11-15-ios-share-cabi-design-v3.md)（レビュー 2 回。1 回目は Codex が A1 を 1 件、2 回目で 2 人とも A1 が 0）
- 実装のレビュー: [../reviews/2026-11-15-ios-share-cabi-implementation-review-v1.md](../reviews/2026-11-15-ios-share-cabi-implementation-review-v1.md)
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（2 つ目の機能）
- コミット: `cc8be3a`（移行）、`4a891d1`（実装のレビュー v1 の直し）

## 1. 作ったもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Share/IosShareCApi.cs`（新） | `Share.h` の 2 つの構造体（`Pack = 8`、24 と 56 バイト）、使う 4 つの関数の宣言（`!UNITY_EDITOR` の外）、項目の整え（1.x と同じく null の項目・未知の `type`・値が null の項目を捨て、除く一覧に null があれば一覧ごと捨て、空白の `subject`・`previewTitle` を省く。警告は値を出さない）、要求の組み立て、文言（1.x の文から URL・道・OS の理由を除いたもの）、結果の読み取り |
| `Runtime/iOS/Share/IosSharePendingCalls.cs`（新） | 呼び出しの番号から受け手を引く表。呼ぶ前に入れ、完了・拒み・例外で取り出し、Manager の破棄で空にする。番号は戻さない |
| `Runtime/iOS/Share/IosShareManager.cs` | 1.x の確かめの順（payload → 使えるか → 整え → 組み立て → 表 → 呼ぶ）。すべての結果を次の `Update` に（`ShareCompleted` → その呼び出しの受け手）。拒みと例外は表から取り出して失敗を出す。完了の配り `Complete` はガードの外 |
| テスト | EditMode: `IosShareCApiTests`（構造体の大きさと全項目の位置、整え、警告に値を出さないこと、組み立て、文言、表）、`IosCApiDeclarationTests`（+4 と完了の delegate の署名）。Editor の PlayMode: `IosShareManagerEditorTests`（偽のネイティブの呼び出しで、拒み・例外・完了〈戻る前のものも〉・順・本当の破棄の後）。Player: SH-23〜25 は 1.x と同じ期待と警告、`IosShareCApiPlayerTests`（`struct_size` 0 の拒み、完了がそれぞれの受け手に次の `Update`）。`NTK_IOS_CABI_2_SHARE`。Share の `UnavailableUntilMoved` を消した |
| UI のテスト | `ShareUITests` を `notMovedYet` から外した。`[abi@event] 2` のとき、SH-9〜12 は URL・道を含まない文言、SH-15 はシートが出る、SH-13 は 2 章 |
| 文書 | `agent-rules/coding-rules/common.md`（呼び出しごとの表の決まり、時点）、`testing.md`（ガードの表、層 1、層 2a） |

## 2. SH-13（同じフレームで 2 回）の実測と、native-toolkit の約束との食い違い

- シミュレーター（iPhone 16 / iOS 18.3）でも実機（iPhone XS / iOS 18.7.2）でも、**シートは 1 つだけ出て、閉じた結果は `share2` の受け手と `ShareCompleted` に 1 本ずつ届き、`share1` には 30 秒待っても結果が来なかった**（`[share2@ok] completed=False activity=<null>`、`[share-event@ok] completed=False activity=<null>`）。1.x の記録と同じ形
- `ShareCompleted` は番号が表にあるかに関係なく、ネイティブの完了ごとに必ず 1 回出る。それが 1 本だけなので、**ネイティブの完了が 1 回しか来ていない**。拒みや例外なら失敗の行が出る。実装のレビューの 2 人（サブエージェントと Codex）も、この包みの誤りではなく native-toolkit の側と判断した
- `Share.h:30-31` は「受け付けた呼び出しには完了がちょうど 1 回」と約束していて、この場面はその外（ホストが window を替えた、scene が切られた）に当たらない。**native-toolkit の不具合**として申し送る候補（利用者の判断を仰ぐ）。見立て（実装のレビュー v1 の 1.2）: 2 つ目のシートが UIKit に断られたまま解放されず `ReleaseWatch` が働かない、か、遷移の待ちで手順が止まる。見分け方は native の `.debug` のログ
- 扱い: `ShareUITests.test_SH13` は、届いた結果が閉じた成功であることを照らし、約束（各呼び出しに 1 本、イベント 2 本）は `XCTExpectFailure` の中で照らす（native-toolkit が直れば、期待した失敗が起きずにテストが落ちて気づける）。来ない呼び出しの受け手は表に残る
- 設計 v3 の 3 章（SH-13 は「呼び出しごとにちょうど 1 回」）と 4 章（各 id に 1 本、イベント 2 本を照らす）からの外れ。1.x との差分は「失われる 1 本が戻る」ではなく、**「届いた結果は正しい受け手に届く（1.x は後勝ちの枠で、2 つ目の受け手に届いた）。1 本は来ないまま」**

## 3. 確かめたこと

### 3.1 シミュレーター（iPhone 16 / iOS 18.3）

| 確かめ | 結果 |
|---|---|
| V-1 | `cc8be3a` の前: EditMode 1520 件（成功 1519、既にあった 1 件の skip）、Editor の PlayMode 311 件成功 |
| V-2 | Player のテスト 21 件成功（SH-20〜25、Share の C ABI の経路 2 件、Dialog、ほか） |
| V-3 | `ShareUITests` 18 件のうち 17 件が 1 回目で成功。SH-9〜12 は URL・道を含まない文言、SH-15 はシートが出て閉じれば成功、SH-16・17 は 1.x と同じ、SH-18 は 1.x と同じ文言で失敗（完了は前に戻った後）。SH-13 は 2 章 |
| V-5 | `test_ios_ui.sh` の既定の一覧は `DialogUITests` と `ShareUITests` の 36 件 |

### 3.1.1 実装のレビュー v1 の直しの後（`4a891d1`）

EditMode 1521 件（成功 1520、既にあった 1 件の skip）、Editor の PlayMode 312 件、Player 21 件（SH-23〜25 の警告の確かめを含む）が成功。SH-13 は `XCTExpectFailure` の中の約束が落ちて「Expected Failure」になった（`ios_ui_runner.py` は成功に数える）。

### 3.2 実機（iPhone XS / iOS 18.7.2、`cc8be3a`）

利用者の指示に従い、`--dry-run` の 4 点と承認 ID を会話に書いてから流した。端末の識別子、ログと結果の束は残していない。

| 確かめ | 結果 |
|---|---|
| V-4 | `ShareUITests` の実機の 11 件（SH-1〜5、9〜13、16）がすべて成功（SH-13 は 2 章の形） |
| V-2（実機） | Player のテスト 21 件がすべて成功 |
| V-6 | サンプルの画面の手の確かめは、利用者に頼む項目として残す |
| 直しの後（`4a891d1`） | SH-13 は「Expected Failure」（実機でも同じく 1 つの呼び出しの完了が来ない）。Player のテスト 21 件がすべて成功 |

## 4. 途中で起きたこと

- `IosShareManagerEditorTests` を `#if UNITY_EDITOR` で囲み忘れ、iOS の Player にも組まれた。Player では IosPlayer のカテゴリの外なので流れず、結果の確かめ（`ios_player_results.py`）が「21 件流れたが root は 26 件」で失敗にした。囲んで直した
- シミュレーターのログ（`log stream`）からは Unity の行を拾えず、SH-13 の手がかりにはならなかった

## 5. 残っていること

- native-toolkit への申し送り（SH-13。利用者の判断）
- V-6: サンプルの画面（`IosShareManagerExampleController`。`subject` と複数の画像・ファイル）を実機で手で一度見る（利用者に頼む）
