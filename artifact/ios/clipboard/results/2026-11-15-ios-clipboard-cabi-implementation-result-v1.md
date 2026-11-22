# iOS Clipboard の C ABI `2.0.0` への移行の実装の記録 v1

- 記録日: 2026-11-15
- 設計: [../designs/2026-11-15-ios-clipboard-cabi-design-v5.md](../designs/2026-11-15-ios-clipboard-cabi-design-v5.md)
  - v1〜v3 はレビューを 3 回通した。3 回目でも Codex の A1 が 2 件あり、決まりの上限で止めた。
  - v4（その直し）と v5（実装のレビューの直し）は**レビューしていない**。v4 の D-12 の仕組みは、実装のレビュー v1 で全体を見てもらった。
  - 4 回目の設計のレビューは回さない（利用者の判断。5 章）。
- 設計のレビュー: [../reviews/2026-11-15-ios-clipboard-cabi-design-review-v1.md](../reviews/2026-11-15-ios-clipboard-cabi-design-review-v1.md)、[v2](../reviews/2026-11-15-ios-clipboard-cabi-design-review-v2.md)、[v3](../reviews/2026-11-15-ios-clipboard-cabi-design-review-v3.md)
- 実装のレビュー: [../reviews/2026-11-15-ios-clipboard-cabi-implementation-review-v1.md](../reviews/2026-11-15-ios-clipboard-cabi-implementation-review-v1.md)
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（3 つ目の機能）
- コミット: `3c650c8`（設計とレビューの記録）、`2670503`（移行）、`ff1cf78`（実装のレビュー v1 の直し）

## 1. 作ったもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Clipboard/IosClipboardCApi.cs`（新） | 次のものを置いた。<ul><li>`Clipboard.h` の 4 つの構造体（`Pack = 8`。24・32・136・24 バイト）</li><li>使う 58 の関数の宣言（`!UNITY_EDITOR` の外）と、7 つのコールバックの型</li><li>要求の組み立て。Remove だけは General も構造体で渡す。模様は表で写す。期限は切り捨てた整数の Unix 秒（1.x と同じ）</li><li>U+0000 の判定。名前・型・画像の道は拒み、値は警告を出す</li><li>エラーの写し方と文言（1.x と同じ）</li><li>ハンドルから読んだ値で結果を作る純粋な部分（EditMode で照らす）と、ハンドルを読む部分（Player だけ）</li></ul> |
| `Runtime/iOS/Clipboard/IosClipboardManager.cs` | 1.x の骨組みはそのまま（主スレッド、破棄の印、操作ごとの BUSY、世代）。次を足した。<ul><li>`user_data` は操作の種類で、C ABI のコールバックの型ごとに静的な受け口を 1 つ置く</li><li>呼び出しの中で決まった結果（D-12）は、配るまで印を持ち、後のフレームに出す</li><li>監視（D-6）: リスナーを 1 つだけ登録して破棄まで持つ。Manager 自身の監視の状態で、頼んでいない変化を捨てる</li><li>破棄（D-7）: 監視を止め、リスナーを外し、待っている読み込みを取り消す</li><li>ReadData の 64 MiB の上限</li></ul> |
| `Runtime/iOS/Clipboard/IosClipboardLaterPump.cs`（新） | ディスパッチャーの GameObject に付く常駐の部品。積んだフレームを過ぎた結果を、ディスパッチャーの末尾に順に渡す |
| `IosClipboardJsonParser.cs` | 封筒の無い DetectValues の JSON を読む入口 |
| テスト | <ul><li>EditMode: `IosClipboardCApiTests`（構造体、組み立て、エラー、2.1 の表、完了の芯）。`IosCApiDeclarationTests` に +58 の宣言と 7 つのコールバックの型。`RunDestroyCleanup` の段</li><li>Editor の PlayMode: `IosClipboardManagerIntegrationTests` に 23 本（後で配る結果の境・順・BUSY・再入・破棄・リセット・ディスパッチャーの消失、監視の状態、ネイティブの呼び出しの例外、U+0000）</li><li>Player: CP-1〜35 は 1.x と同じ期待。Scope・UtType・`Has*`・CP-31 の順の照らしを足した。2.0.0 だけの CP-36〜40 も足した</li></ul> |
| UI のテスト | `ClipboardUITests` を `notMovedYet` から外した。`[abi@event] 2` のとき、CB-2b・CB-3 は `domain=<null> native-code=<null>` の全文を照らす。CB-5 はリスナーが 1 回解放されたこと（`listener-released=1`）も照らす |
| ビルドの設定 | `NTK_IOS_CABI_2_CLIPBOARD`（Player のテストと UI のハーネス） |
| 文書 | `agent-rules/coding-rules/common.md`（iOS の Clipboard の決まり）、`testing.md`（ガードの表、層 1、層 2a） |

## 2. 1.x との違い（設計 v5 の 3 章）

- 実測で同じだったもの:
  - CP-1〜35 の値・回数・順。CP-17 の「1 回の Copy で変化が 2 回」も同じ
  - CB-1〜4 の値
  - 入口で拒まれた要求の間の BUSY
- 既知の差分:
  - 失敗の `Domain`・`NativeCode` は null（C ABI は数だけを返す）
  - 読み込み系の失敗のエラーのログは出ない。代わりに、入口で拒まれた要求に `LogWarning` が 1 行出る
  - U+0000 の扱い:
    - 名前・型・画像の道に入っていれば `CLIPBOARD_INVALID_REQUEST`
    - 書き込む値は U+0000 の前で切れ、警告が出る（CP-38 で `"ntk\0secret"` が `"ntk"` になった）
    - 読み取った文字列では U+FFFD になる
  - 別の C ABI の利用者が監視を置き換えたときは、Manager の状態が立っている間、置き換えた先の変化も出る
- 1.x から残るもの: BUSY などの枠の前の拒みの受け手の中から呼び直すと、ディスパッチャーの同じ `Update` が終わらない（実装のレビュー v1 のサブの R-5）

## 3. 確かめたこと

### 3.1 シミュレーター（iPhone 16 / iOS 18.3）

| 時点 | EditMode | Editor の PlayMode | Player | `ClipboardUITests` |
|---|---|---|---|---|
| v3 の実装（コミット前） | 1565 件（成功 1564、既にあった 1 件の skip） | 324 件成功 | 59 件成功 | — |
| `2670503`（v4） | 1565 件（同上） | 329 件成功 | 59 件成功 | 6 件成功（CB-1〜5、2b） |
| `ff1cf78`（v5） | 1567 件（成功 1566、同じ skip） | 335 件成功 | 59 件成功 | 6 件成功 |

Player の記録の行（`[NTK-CLIPBOARD-RECORD]`）は次のとおり:

| ケース | 記録 |
|---|---|
| CP-12 | `has strings=True urls=True images=False colors=False` |
| CP-31 | `order=cancel,load` |
| CP-37 | `frames first=1360 second=1361`（受け手の中からの呼び直しは次のフレーム） |
| CP-38・CP-39 | `CLIPBOARD_INVALID_REQUEST domain=<null> native-code=<null>` |
| CP-40 | `events=2 on-changed=2` |

失敗の行は、どれも `domain=<null> native-code=<null>` だった。

### 3.2 実機（iPhone XS / iOS 18.7.2）

利用者の指示に従い、`--dry-run` の 4 点と承認 ID を会話に書いてから流した。端末の識別子、ログ、結果の束は残していない。

| 時点 | Player | CB-5 |
|---|---|---|
| `2670503` | 59 件成功 | 成功（`listener-released=1`） |
| `ff1cf78` | 59 件成功 | 成功 |

## 4. 途中で起きたこと

- 設計のレビューは 3 回で止まらなかった。どれも、呼び出しの中で決まった結果をいつ・どの順で配るか（D-12）の指摘だった。
  - 1 回目: 入口の拒みで印が早く外れる。
  - 2 回目: ディスパッチャーは `Update` の中で積まれたものも回すので、同じフレームで配られ、呼び直しで止まらない。
  - 3 回目: Manager の `Update` で配ると、先に積まれた変化を追い越し、Manager を無効にすると止まる。
  - v4 で、境を見る役をディスパッチャーの GameObject の常駐の部品に置き、ディスパッチャーの末尾に渡す形にした。
- 実装のレビュー v1 の Codex の A1: 配る時に登録の世代を見直したため、1.x なら届いた変化が呼び出しごとの受け手に届かなかった。外すのを「入口で拒まれた start の登録」だけにして直した。

## 5. 残っていること

- 設計 v4・v5 はレビューしていない。4 回目の設計のレビューは回さない（利用者の判断、2026-11-15。D-12 の仕組みは実装のレビュー v1 で全体を見てもらい、指摘は直してテストを足した）。
- 段 4: JSON Builder（`IosClipboardJsonBuilder`）を消す。→ 済み（2026-11-21、`4dcf144`）
