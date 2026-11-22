# iOS Share の C ABI への写し方の設計 v2 のレビュー v2（差分）

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-share-cabi-design-v2.md](../designs/2026-11-15-ios-share-cabi-design-v2.md)。1 回目: [2026-11-15-ios-share-cabi-design-review-v1.md](2026-11-15-ios-share-cabi-design-review-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。差分のレビュー（前の指摘の判定と、v2 で変えた所だけ）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

iOS Share の C ABI 設計 v2 の差分レビューです。ファイルは編集していません。行番号は v2（`/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v2.md`）のものです。

**先に答え:** 1 回目の指摘はほぼすべて判断どおり直っていました。新しい指摘は A1・B が 0 件、A2 が 2 件、C が 4 件です。A2 の 2 件はどちらも、破棄の扱いを直した D-1・D-13 から出たものです。

v2 で新しく引いた出典と、D-9 の C# の型は、一次ソースと照らしてすべて合っていました。
- 出典: F-4（`StructReader.swift:115-119`）、F-5（`ShareRepositoryImpl.swift:87-90`）、F-6（`ShareMapping.swift:98-100`、stage1b-result の 56・91 行）、F-8（`Registry.swift:190-195`）、F-12（`1.13.0` の `ShareError.swift:44-45`、`ShareSheetPresenter.swift:52-55, 81-84`、stage1b-working-notes の 484-491 行）、F-13（`CApiString.swift:62-63`、`93e6b8f` の `IosShareJsonBuilder.cs:141-169`）
- 型: `Share.h:73-74, 78-95, 99-100`
- D-2・D-3 は、1.x の `UnityIosShareJsonParser.swift:36-51`（`1.13.0`）と `IosShareJsonBuilder.cs:31-64` の振る舞いに一致します。

### 1.1 1 回目の指摘の判定

| 指摘 | 判定 | 根拠（v2） |
|---|---|---|
| R-1（1.x の文言の出典） | 直った | F-12（L29）の出典が `1.13.0` になった。D-8（L44）の `ACTIVITY_FAILED` は前置きを保つ。3 章に「活動の失敗」「表示が断られた・落ち着かない」の行がある（L64-65）。`SHOW_FAILED` の書き方は新 R-5 |
| R-2（P2-15、D-3） | 直っていない（一部） | D-2（L38）に P2-15 と J-N5 が書かれた。D-3（L39）は案 (a)。3 章の SH-17 は「同じ」（L55）。ただし 4 章の UI テストの行（L80）は、SH-17 をまだ既知の差分の欄に挙げている → 新 R-3 |
| R-3（順） | 直った | D-6（L42）に順が書かれた。4 章の PlayMode（L78）に、未知の `type` だけの項目がある |
| R-4（破棄・表に無い番号・`_instance`） | 直し方に新しい問題 | D-1（L37）と D-7（L43）で 3 つとも決まった。F-8 の取り残しも既知と書かれた。ただし直し方から新 R-1・R-2 が出た |
| R-5（例外） | 直った | D-1 は「組み立て終えた後、呼ぶ前に入れる」。D-6 は表から取り出して `Internal error: <ex.Message>` を出す |
| R-6（`file:`） | 直った | F-5（L23）。`ShareRepositoryImpl.swift:87-90` と一致 |
| R-7（コールバックの署名） | 直った | D-9（L45）の `Void(IntPtr, Int32, UInt32, IntPtr)` が `Share.h:73-74` と一致。4 章の EditMode（L77）に「コールバックの署名」がある |
| R-8（SH-13 の値） | 直った | 3 章（L60）に 3 つの値。4 章（L80）は「各値が 3 つのどれか、実際の行をテストの出力に書く」。V-3 の言い回しが v1 のまま残っている → 新 R-3 |
| R-9（表への入口） | 直った | 純粋なクラス `IosSharePendingCalls`（L37、L75）、D-13（L48）、Player の「internal の口」（L79） |
| R-10（U+0000） | 直った | F-13、D-5、3 章（L66）、EditMode の警告の判定（L77） |
| R-11（`common.md`） | 直った | 4 章の文書（L81） |
| R-12（細部） | 直った | D-9 の型（`Share.h:78-95, 99-100` と一致）、D-1 の lock、D-2 の ordinal とログ、D-4 の `IsNullOrWhiteSpace` |
| R-13（出典） | 直った | F-4・F-6・F-8。出典はすべて照らして合った。置き場所の細部は新 R-6 |
| R-14（V-6・iPad・記録） | 直った | V-6（L94）、iPad の行（L67）、4 章の「実装の記録とトピックの README」（L81） |
| Codex R2-1（U+0000） | 直った | R-10 と同じ |
| Codex R2-2（破棄の後） | 直し方に新しい問題 | D-1・D-7 で決まった。ただし新 R-1・R-2 が出た。補足: 4 章の確かめは「受け手に出ない」だけで、Codex が挙げた「再生成した Manager の `ShareCompleted` に出る」を照らす項目は無い（3 章の判断は求めていない） |
| Codex R2-3（拒みの後処理） | 直った | D-13 と 4 章の PlayMode（L78）。拒み・例外で表から取り出し、次の `Update` に 1 回、ほかの保留は残る |
| Codex R2-4（`file:`） | 直った | F-5 |

### 1.2 新しい指摘

#### R-1 [A2]: Editor の PlayMode の「破棄の後の完了」を呼ぶ口が、設計のガードの上で無い
- **根拠**
  - D-9（L45）は「コールバックの本体と呼び出しは `UNITY_IOS && !UNITY_EDITOR`」とします。
  - D-13（L48）で Editor から呼べる関数にするのは、「表に入れる → 呼ぶ → 拒み・例外で取り出して出す」の流れだけです。完了の受け口は「Player のテストで使う」と書かれています。
  - 一方、4 章の `Tests/PlayMode`（Editor）（L78）には「破棄の後の完了（表に無い番号）は受け手に出ない」があります。これは完了の配り（番号から表を引き、次の `Update` に出す）を Editor から呼ぶことが前提です。
- **壊れ方**
  - 設計どおりに書くと、Editor ではこのテストが組めません（受け口がガードの中にあるため）。
  - 実装する人は、配りの部分をガードの外に分けるか、テストを Player に移すかを、その場で決めることになります。
  - コンパイルの段で気づくので影響は小さいです。参考までに、作業中の `Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Share/IosShareManager.cs:193` は、`Complete(long, IosShareResult)` をガードの外に分けて解いています。
- **直し案**
  - D-13 に次を足します。「完了の配り（番号と結果を受け、表から取り出し、`_instance` の `ShareCompleted` と受け手を次の `Update` に出す）も、ガードの外の internal の静的な関数にする。`[MonoPInvokeCallback]` の本体は、読み取りと `ntk_share_result_free` だけにする」。

#### R-2 [A2]: `OnDestroy` で表を空にするとき、番号を 1 に戻すかどうかが決まっていない
- **根拠**
  - D-1（L37）は「1 から増える `long`」と「`OnDestroy` で表を空にする」を別々に書いています。4 章（L75、L77）の表の項目も「空にする」「番号が増える」を別々に確かめるだけです。
  - F-8（`Share.h:69-72`）により、受け付けた共有の完了は、シートが閉じるまでいつまででも遅れます。`user_data` は読まれずにそのまま返ります。
- **壊れ方**
  - 「空にする」を、表を作り直す形（番号も 1 に戻る）で実装したとします。
  - 共有 #1 のシートが出ている間に Manager を壊し、作り直して新しい共有を呼ぶと、新しい共有も #1 になります。
  - その後に古い #1 の完了が来ると、新しい呼び出しの受け手に古い結果が出ます。新しい呼び出しの本当の完了は「表に無い番号」になり、`ShareCompleted` にしか出ません。
  - D-1 の中心の約束（呼び出しごとに自分の結果）が、破棄をまたぐと崩れます。4 章の EditMode の項目では捕まりません。
  - 作業中の `IosSharePendingCalls.cs:48-51` は番号を戻していないので、今の実装では起きません。
- **直し案**
  - D-1 に「空にしても番号は戻さない（プロセスの間、増え続ける）」と書きます。
  - 4 章の EditMode に「空にした後の番号は、空にする前より大きい」を足します。

#### R-3 [C]: 4 章と 5 章に v1 の言い回しが残っている
- **根拠と直し案**
  - 4 章の UI テスト（L80）の「3 章の既知の差分の期待で照らす（SH-9〜13、15、17）」に、SH-17 が残っています。D-3 と 3 章（L55）で SH-17 は「同じ」になりました。「（SH-9〜13、15）」に直します。
    - これを読んで `2` の枝に v1 の期待（「コピー」が出ない）を書くと、V-3 で落ちます。
    - 作業中の `Tools~/IosUiTests/NtkUiTests/ShareUITests.swift:218-226` は 3 章に従っていて、1.x と同じ期待です。
  - V-3（L91）の「SH-13 の 2 つ目の値を記録する」は v1 の言い回しです。4 章（L80）では 2 本とも値を照らして書き出すので、「SH-13 の 2 本の値を記録する」に直します。

#### R-4 [C]: D-8 の理由「前置きは 1.x に合わせ」と `SHOW_FAILED` の文言が合っていない
- **根拠**
  - D-8（L44）では、`ACTIVITY_FAILED` は `Failed to present the share sheet: …` の前置きを保ちます。
  - `SHOW_FAILED` は `Failed to present the share sheet.` で、`: ` がありません。1.x は両方とも `Failed to present the share sheet: <理由>.` でした（`1.13.0` の `ShareError.swift:44-45`）。
  - 理由の欄は「文の形と前置きは 1.x に合わせ、利用者の文字列の照合が壊れにくいようにする」です。
- **壊れ方**
  - 1 回目の R-1 が例に挙げた `StartsWith("Failed to present the share sheet:")` は、`SHOW_FAILED` では今も合いません。
  - 3 章（L65）に既知の差分として載っているので、黙って変わるわけではありません。理由の書き方が実際より広いだけです。
- **直し案**
  - (a) 理由を「`ACTIVITY_FAILED` だけ前置きを保つ。`SHOW_FAILED` は 1.x の OS の理由を取り戻せないので前置きを付けない」に直します。
  - (b) `SHOW_FAILED` も `Failed to present the share sheet: the share sheet could not be shown.` の形にそろえます。この場合、3 章の L60 と L65 の値も直します。
  - どちらにするかを決めて書きます。

#### R-5 [C]: D-5 の U+0000 の警告に、何を出すかと、いつ見るかが書かれていない
- **根拠**
  - D-2（L38）は「捨てた件数と `type` だけ。値や道は出さない」と明記しています。D-5（L41）には、ログの中身がありません。
  - `common.md` のログの決まりは「入力…の値は長さだけを出す」です。
  - 整え（D-2〜D-4）の前と後のどちらで見るかも書かれていません。前で見ると、捨てる項目や省く空白の `subject` にも警告が出ます。
- **直し案**
  - D-5 に「整えた後の、実際に渡す値だけを見る。警告には、どの欄か（項目の何番目・`subject`・`previewTitle`・除く一覧）だけを出し、値は出さない」と足します。

#### R-6 [C]: 表と出典の置き場所の細部
- F-12・F-13（L29-30）が F-11（L31）より前に、D-13（L48）が D-12（L49）より前にあります。番号順に並べ替えます。
- F-4（L22）の `NOT_SUPPORTED` は事実ですが、出典の欄に入っています。事実の欄に移し、出典の欄には `StructReader.swift:115-119` だけを残します。
- F-10（L28）の「1.x の `1.13.0` も中央」に行番号がありません。`1.13.0` の `ShareSheetPresenter.swift:70-75` を足します。

---

新しい指摘: A1 0 件 / A2 2 件 / B 0 件 / C 4 件

## 2. Codex

- R2-1：修正済み。決定どおり警告付き切断と警告判定テストを記載（v2:41、v2:77）。切断の一次根拠は /Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Common/CApiString.swift:63。
- R2-2：修正済み。破棄時の表消去、遅延完了の配送先、結果解放を規定（v2:37、v2:43）。一次ソースの遅延・所有権契約と整合（Share.h:69、Share.h:66）。
- R2-3：修正済み。拒否注入で表除去、次のUpdateでの配送順・各1回、別の保留呼び出しの維持を検証する計画（v2:78）。拒否時に完了が来ない一次契約を補う内容（EntryError.swift:20）。
- R2-4：修正済み。`file:`の受け入れ条件を訂正（v2:23）。一次実装と一致（ShareRepositoryImpl.swift:82）。

Part 2：新規の指摘なし。

注記：
- v2 は /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v2.md です。
- Share.h は Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Share.h です。
- CApiString.swift、EntryError.swift、ShareRepositoryImpl.swift は /Users/jonghyunkim/Desktop/native-toolkit/ios 配下です。
- Codex は scripts/check_design_consistency.py を実行し、終了コード 0 でした。
- Codex の出力は上の結論のみで、R3 級の指摘が無い理由の説明は付いていません。

## 3. 判断

**2 人とも新しい A1 が 0 で、止めた。** 止めた後の直しは v3 に入れ、レビューには出さない。

| 指摘 | 判断 | v3 での直し |
|---|---|---|
| サブの R-1（完了の配りの置き場所） | 直す（実装はすでにこの形） | D-13 に、完了の配り `Complete(番号, 結果)` もガードの外の internal の静的な関数にし、`[MonoPInvokeCallback]` の本体は読み取りと解放だけ、と書く |
| サブの R-2（番号を戻さない） | 直す（実装はすでにこの形） | D-1 に「空にしても番号は戻さない」。EditMode の項目（空にした後の番号は前より大きい）は実装済み |
| サブの R-3（v1 の言い回し） | 直す | 4 章の SH-17 を外す。V-3 を「2 本の値」に |
| サブの R-4（`SHOW_FAILED` の前置き） | (a) | D-8 の理由を「`ACTIVITY_FAILED` だけ前置きを保つ」に |
| サブの R-5（U+0000 の警告） | 直す | 整えた後の、実際に渡す値だけを見る。警告は値を出さない（実装は値を出さず、渡す値だけを見ている。どの欄かは出さない） |
| サブの R-6（並び） | 直す | F と D の並び、F-4 の欄、F-10 の行番号 |
| Codex | 新しい指摘なし | — |
