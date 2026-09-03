# レビュー結果

- 日付: 2026-09-03
- 対象ファイル: artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v4.md
- 前版レビュー: artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v3.md
- 機能名: clipboard
- プラットフォーム: macOS
- ドキュメント種別: 実装計画書
- レビュー実施: サブエージェント 1 本（観点 A / B / C を統合）
  - `[A]` ネイティブ仕様との整合
  - `[B]` C# 実装可能性・既存実装とルールへの適合
  - `[C]` 内部整合性・抜け漏れ・リスク

---

## 強み

### 前回指摘の反映確認

- **S-1 / S-2 / S-3 / S-5、N-1〜N-9 はすべて反映確認済み。** とくに N-1（`UnityMacClipboardManager.swift:396` の `guard let json` 実在を確認）、N-2（`startObserving` はファサード自身の `catch { ClipboardError.unknown }`、swift:402-405 で確認）、N-8（`MacShareManager.cs:59` が素の `bool` 宣言であることを確認）は一次ソースと一致
- **S-4 は反映されたが内容が誤り**（M-3）。**F-1 は形としては反映されたが 3 点の欠陥が残る**（M-1 / M-2 / S4-3）

### 確認済み（問題なし）

- **数の表記は 10 種類すべてが全節一致**（17 ファイル / Tests 5 / パーサ 12 / 結果型 12 種 10 ファイル / 公開メソッド 15 / 公開イベント 13 / 手動確認 32 / per-call スロット 16 / ガードチェーン 6 段 / 実行段階 8）
- 5.6.1〜5.6.13 に欠番・重複なし。V-1〜V-12・D-1〜D-12 に重複・食い違いなし
- **`TryPassGuards` の引数順と骨格コードの呼び出しは整合。** `TResult` は `onResult` から推論可、`const string op` の const ローカル捕捉、`validate` ラムダの conditional の target-typed 変換、try/catch 後の definite assignment もいずれも合法
- `DispatchRejectedResult` / `DispatchOffThreadRejection` / `EndOperation` / `TryBeginOperation` の 4 シグネチャは `IosClipboardManager.cs:536-546 / 562-579 / 487-495` と一致
- per-call スロット 16 本は 5.6.2 の公開メソッド 15・5.6.10 のシーム 15 操作分・`IosClipboardManager.cs:1696-1702` の 16 スロット列挙と対応し、取り出し規約（退避 → `EndOperation` → `Dispatch`）は iOS の `FireReadResult` 等（`:742-760`）および 5.6.4 の順序契約と矛盾しない
- V-12 は 8 章・9 章・10 章・11 章と重複しない
- workflow.md ステップ 6 の必須 8 項目は充足。4.2 の置換件数は実測一致

### iOS v5 との比較

**実質的な差は解消した。** v3 レビューが挙げた 2 件（per-call スロット表 = iOS v5 5.6.2 相当、single-flight 粒度の要検証 = iOS v5 9.6 相当）は 5.6.12 と V-12 で埋まった。iOS v5 の 9.1〜9.9 に相当する論点（文字コード / scope と null の等価性 / 読み出しの privacy プロンプト / 大容量レスポンス上限 / Awaitable の追加時期 / native ABI への request・lifetime ID 追加 / Domain Reload 双方無効）は V-2 / 1.4 / V-9 / 5.6.3 の受信側制約 / D-7 / 11 章 / V-11 に一対一で対応している。macOS 固有の論点（lazy data provider、named/unique の永続、15.4 分岐、tracker の scope 単位性）は iOS v5 に無い分だけ厚い。**残る差は「通常経路の Fire / Dispatch がまだ書かれていない」ことだけ**で、これは S4-3 で閉じる。

---

## 改善点

### 高優先度

#### M-1 `[B]` `InvokeNative` から `onNativeFailureResult` を落としたため、15 操作中 12 操作で段階 7 の失敗が結果を返せない

- 該当: 5.6.4 のヘルパ宣言 `private static void InvokeNative(string operation, string inFlightKey, Action nativeCall, Action? onNativeFailure = null);` と、骨格コードの `InvokeNative(op, op, () => {...})`、段階表 7 行目「通常経路 `FireXxxResult`」
- 根拠: `IosClipboardManager.cs:1458-1484`。iOS の `InvokeNative` は第 5 引数 `Action<string>? onNativeFailureResult` を持ち、これが `null` のときだけ `FireOperationResult(...)` にフォールバックする。`onNativeFailureResult` は `:1141 / 1184 / 1224 / 1269 / 1310 / 1351 / 1397 / 1436` の 8 操作（`IosClipboardOperationResult` 以外を返す操作）で必須になっている
- 何が違うか: macOS で `MacClipboardOperationResult` を返すのは removePasteboard / startObserving / stopObserving の **3 操作だけ**で、残る 12 操作（Copy / Append を含む）は `MacClipboardOwnershipResult` などを返す。`onNativeFailureResult` が無いと、Copy の P/Invoke が例外を投げたとき
  1. `MacClipboardOwnershipResult` を組み立てる手段が無い
  2. フォールバックが型違いの `MacClipboardOperationResult` を `ClipboardOperationCompleted` に流す
  3. `s_onCopy` が null 化されず**スロットがリークし、呼び出し側の `onResult` が永久に呼ばれない**
  骨格コードは第 4 引数すら渡していないため、実装者がこの穴に気づく手がかりが無い。段階表 7 行目の「`FireXxxResult` で通常経路」という宣言と、示されたシグネチャが噛み合っていない
- どう直すべきか: `InvokeNative` に `Action<string>? onNativeFailureResult = null` を復活させ（既定は 3 つの void 操作用フォールバック）、骨格コードに `onNativeFailureResult: message => FireOwnershipResult(MacClipboardOwnershipResult.Failure(op, MacClipboardErrorCodes.BridgeUnavailable, message), op)` を明記する

#### M-2 `[B]` 骨格コードが未定義ヘルパ `TooLargeOrNull` を呼んでおり、そのままではコンパイルできない

- 該当: 5.6.4 骨格コード 1095 行 `: TooLargeOrNull(content)))`
- 根拠: `TooLargeOrNull` は文書内で他に 1 度も出現しない。5.6.3 の表に「representations の合計バイト数が `MacClipboardLimits.MaxRequestBytes` を超える → 9007」があるだけで、シグネチャも実装も規定が無い
- 何が違うか: F-1 は「完全形の骨格コード」を求めた指摘であり、v4 はそれに答えたつもりで**新しい未定義シンボルを 1 個持ち込んだ**。`validate` ラムダの conditional は `TooLargeOrNull` の戻り値型が `(int, string)?` である場合にのみ target-typed 変換が成立するので、戻り値型が決まらないと型検査すら通らない
- どう直すべきか: 5.6.4 のヘルパ一覧に `private static (int Code, string Message)? TooLargeOrNull(MacClipboardContent content);` を追加する（合計バイト数が `MacClipboardLimits.MaxRequestBytes` を超えたとき `(RequestTooLarge, ...)` を返し、それ以外は `null`）。名前が「大きすぎるか null か」と読めて紛らわしいので `ValidateRequestSize` 等への改名も検討する

#### M-3 `[B][C]` S-4 で追記した「段階 5 の駆動手段」が、実際には段階 3 で `NullReferenceException` になり成立しない

- 該当: 7.2 の「段階 5（JSON 構築の例外）は、`MacClipboardContentItem.FromRepresentations` に値が `null` の `byte[]` を含む辞書を渡して駆動する」
- 根拠: 5.6.3 の事前検証表が Copy / Append に対し「representations の**合計バイト数**が `MaxRequestBytes` を超える → 9007」を課している。合計バイト数の算出は各 `byte[]` の `Length` を読む以外に手段が無く、値が `null` の要素があればそこで NRE になる。この検証は段階 3、すなわち `TryPassGuards` の `validate` ラムダ内で走るため、**段階 5 に到達する前に落ちる**
- 何が違うか: v3 レビュー S-4 の指摘（「駆動手段が無い」）は解消していない。さらに悪いことに、`validate` の中の NRE はどこにも捕捉されないので**公開 API `Copy` が例外を投げて呼び出し側へ伝播する**（拒否は結果で返すという 5.6.2 の契約違反）。結果として 7.2 の段階 5 テストは書けず、段 3 の DoD「対象範囲の新規テストがすべて pass する」を満たせない
- どう直すべきか: 5.6.3 に「合計バイト数の算出は要素の null を 0 バイトとして扱う（`r.Value?.Length ?? 0`）。要素の null 検査はネイティブ・C# のどちらでも行わず、段階 5 の `Convert.ToBase64String` の `ArgumentNullException` として 9002 に落とす」と明記する。あわせて `MacClipboardContent.Items` の要素が null の場合も同様に段階 3 を素通りすることを書く

### 中優先度

#### S4-1 `[C]` S-2 の反映（段 3 のイベントは 4 本）と S-3 の反映（段 3 に変更コールバックの `DiscardIfTerminated`）が相互に矛盾する

- 該当: 12 章 1674 行「段 3 が宣言するのは…4 イベントだけ。残り 9 本は段 4」と、1680-1684 行の「段 3 に先に入れる 3 点」の 3 番目「変更コールバックへの `DiscardIfTerminated` の適用」
- 根拠: 5.6.2 は変更コールバックが `ClipboardChanged`（共通）→ `s_onChanged`（個別）の順で発火すると規定。5.6.10 の `DeliverChangeEventForTests` はこの経路を駆動するシームであり、7.2 の「破棄後に届いた変更イベントが捨てられること」はこれを使う
- 何が違うか: 段 3 で変更コールバックの discard を実装・検証するには `ClipboardChanged` イベント・`MacClipboardChangeEvent` の parse・`DeliverChangeEventForTests` が段 3 に必要になる。「残り 9 本は段 4」と両立しない。v3 レビューの S-2 と S-3 を別々に反映した結果として生じた食い違い
- どう直すべきか: 段 3 の宣言範囲を「4 イベント + `ClipboardChanged` の計 5 本、残り 8 本は段 4」に改め、段 3 に入れる 3 点の 3 番目に「`ClipboardChanged` / `s_onChanged` への配送経路と `DeliverChangeEventForTests`」を含めると明記する

#### S4-2 `[B]` 「他の操作も同じ形」が observation 2 操作には成立しない

- 該当: 5.6.4 骨格コードの見出し「（`Copy` の完全形。他の操作も同じ形）」、および `TryBeginOperation(s_inFlight, op)` / `InvokeNative(op, op, ...)`
- 根拠: 5.2.1 は StartObserving / StopObserving の single-flight キーを `ObservationControlKey` と規定し、6.1 は observation の Busy 文言を専用文言と規定。5.6.5 は段階 7 で `s_observingGeneration++` 等の世代更新が入ると規定
- 何が違うか: この 2 操作では次の 4 点が異なる。骨格をそのまま写すと段 4 で確実に事故る
  1. `TryBeginOperation` の第 2 引数が `op` ではなく `ObservationControlKey`
  2. Busy メッセージが `BusyMessage(op)` ではなく専用文言
  3. 段階 7 に世代更新と `s_onChanged` 登録が入る
  4. `InvokeNative` の `onNativeFailure` に `ReleaseChangeRegistrationIfOwned()` + `s_pendingObservationGeneration = 0` を渡す必要がある
- どう直すべきか: 骨格コードの直後に「observation の 2 操作だけは 4 点が異なる」と箇条書きするか、`StartObserving` の差分骨格を 5〜10 行追記する

#### S4-3 `[B]` 段階 7・8 の通常経路（`FireXxxResult` / `Dispatch`）のシグネチャが未定義のまま

- 該当: 5.6.4 段階表の 7・8 行が `FireXxxResult` を名指ししているが、5.6.4 のヘルパ一覧（4 本）にも他の節にも宣言が無い
- 根拠: `IosClipboardManager.cs:705-800` は `FireOperationResult(result, inFlightKey)` / `FireReadResult(result)` / `Dispatch<TResult>(result, common, perCall)` の 3 系統を持ち、`TakeOperationCallback` → `EndOperation` → `Dispatch` の順を各 Fire が担っている。5.6.12 の取り出し規約はこの Fire 群の内部規約そのもの
- 何が違うか: M-1 を直す際に `onNativeFailureResult` の中身が `FireOwnershipResult(...)` になるため、この 1 本が未定義だと F-1 の追記が再び穴を残す
- どう直すべきか: 5.6.4 のヘルパ一覧に `private static void Dispatch<TResult>(TResult result, Action<TResult>? common, Action<TResult>? perCall);` と `FireXxxResult` の命名規約（「スロット退避 → `EndOperation` → `Dispatch`、共通イベントは `_instance?.XxxCompleted` から取る」）を 3 行足す

#### S4-4 `[C]` 10 章 DoD が `V-1 〜 V-11` のままで、新設した V-12 が抜けている

- 該当: 10 章 1642 行。8 章は V-1〜V-12 の 12 件
- どう直すべきか: `V-1 〜 V-12` に書き換える

#### S4-5 `[C]` 冒頭の変更サマリ表が per-call スロット表を 5.6.13 と書いているが、実体は 5.6.12

- 該当: 21 行。1330 行が `#### 5.6.12 per-call スロット`、1359 行が `#### 5.6.13 Awaitable 版について`
- どう直すべきか: `5.6.12` に修正する

### 低優先度

- **N4-1 `[C]`** 新設 5.6.12 は本文中の被参照がゼロ。5.6.4 の `s_onCopy = onResult;`、5.6.8 の `ClearAllPendingCallbacks()`、5.6.10 の `HasAnyPendingCallbackForTests` の 3 箇所から「（5.6.12）」を張ると孤立しない
- **N4-2 `[C]`** 12 章の段別シーム列挙から `CompleteOperationForTests`（removePasteboard、段 4）と `DeliverChangeEventForTests` が漏れている。5.6.10 の「各シームは対応する操作と同じ段で追加する」と照合できない
- **N4-3 `[B]`** 5.3 の `BuildContentJson` は `representations` のキー順を規定していない（ソートを明記しているのは `BuildPatternsJson` だけ）。`IReadOnlyDictionary` の列挙順は保証されないため、7.1 の「content: representations の base64、複数 item」をゴールデン文字列で書けない。キーをソートするか、テストを単一キーに限る旨を書く
- **N4-4 `[B]`** 骨格コードは `Debug.Log` を `TryPassGuards` の**後**に置いているが、`csharp.md`「全メソッドの先頭 1 行目に Debug.Log」からのこの逸脱がどこにも宣言されていない（5.6.11 が宣言しているのは「値を出さない」だけ）。iOS は `TryStartOperation` 内に「Main thread. Must precede everything else, including the entry log.」と根拠コメントを持つ。同趣旨の 1 行を 5.6.11 に足す
- **N4-5 `[C]`** 98 行の「（5.2）」は S-5 の階層変更後は 5.2.0 を指すのが正確
- **N4-6 `[C]`** 7.2 の項目がどの段のものか分かれていない。「監視の世代管理」「observation 共有キーの Busy 文言」は明らかに段 4 だが、段 3 の DoD「対象範囲の新規テストがすべて pass」の判定ができない。段 3 / 段 4 の印を付ける
- **N4-7 `[B]`** 7.2 の段階 3（9007）を駆動するには 32 MiB 超の `byte[]` を実確保する必要がある（`MaxRequestBytes` が `const` でテストから差し替えられない）。PlayMode テストのメモリ・所要時間の実務コストとして一言添えるか、`internal static long MaxRequestBytesOverrideForTests` を 5.6.10 のシームに加えることを検討する
- **N4-8 `[A]`** 1.2.2 の引用 `IosClipboardManager.cs:194-201` は、実体では 193-199（193-194 が理由コメント、195 が属性、196-199 が delegate）。v3 レビューの指示どおりに直した結果なので責は無いが、範囲としては 193-199 が正しい。同様に 5.1.1 の `IosClipboardPayloads.cs:462-509` は 463-509
- **N4-9 `[C]`** 新規 17 ファイルの `#nullable enable` 付与が明記されていない。文書全体が `string?` 前提で書かれており、既存 `Ios*` は全ファイル 1 行目に付けている。5 章冒頭に 1 行足すと段 2 の実装がぶれない

---

## 不足項目

- `InvokeNative` の `onNativeFailureResult` 引数と、値を返す 12 操作での使い方（M-1）
- `TooLargeOrNull`（または `ValidateRequestSize`）のシグネチャ（M-2）
- 合計バイト数算出時の要素 null の扱い（M-3）
- `Dispatch<TResult>` と `FireXxxResult` の命名規約（S4-3）
- `StartObserving` の差分骨格（S4-2）
- `BuildContentJson` の `representations` キー順（N4-3）
- 新規 17 ファイルの `#nullable enable`（N4-9）

---

## 総合評価

**A 領域は v3 で完成しており、v4 で触った 2 箇所（1.6 の `:396`、6.3 の 1599 発生源）も一次ソースと一致した。** ネイティブ整合について新たな指摘は無い。

**C 領域（内部整合）は良好。** 数の表記は 10 種類すべてが全節で一致し、5.6 のサブ節に欠番・重複は無く、S-5 の階層変更による参照切れも生じていない。残った矛盾は、S-2 と S-3 を別々に反映した結果として生じた段 3 の境界の食い違い（S4-1）、DoD の V-11 止まり（S4-4）、変更表の節番号（S4-5）の 3 件で、いずれも局所修正。

**問題は F-1 の反映品質に集中している。** v3 が「書いてあるとおりに実装すると詰まる唯一の箇所」と指摘した 5.6.4 は、骨格コードと 4 シグネチャを得たものの、

1. `InvokeNative` から `onNativeFailureResult` を落としたことで 15 操作中 12 操作の段階 7 失敗経路が塞がり（M-1）
2. 未定義の `TooLargeOrNull` を持ち込んでコンパイル不能になり（M-2）
3. 通常経路の `FireXxxResult` は依然として名前だけ（S4-3）

という状態で、**「詰まる箇所」が拒否経路から通常経路へ移動しただけになっている**。加えて S-4 の反映は前提が誤っており、指定された駆動手段は段階 3 で NRE になる（M-3）。修正量はいずれも数行だが、これらを残したまま段 3 に入ると v3 レビューと同じ事故が形を変えて再発する。

---

## 着手可否の判定

**条件付きで着手可。**

| 段 | 判定 | 条件 |
| --- | --- | --- |
| 段 1（`ClipboardJsonReader` への改名） | **即着手可** | 修正待ちの指摘は 1 件も掛からない。置換件数（Runtime 3 / Tests 42）を実測で再確認済み。V-1（xcframework 1.3.0 とソースの版対応）の確認だけが前提 |
| 段 2（型・定数・Builder・Parser） | **即着手可** | must fix はいずれも段 3 の Manager に掛かる。着手前に N4-3（`BuildContentJson` のキー順）を 1 行決めておくと 7.1 の Builder テストがゴールデン文字列で書ける。N4-9 も同時に決めると手戻りが無い |
| 段 3（Manager コア） | **M-1 / M-2 / M-3 の修正が必須** | あわせて S4-1（段 3 のイベント範囲に `ClipboardChanged` を含める）と S4-3（`Dispatch` / `FireXxxResult` の規約）を直す。3 件とも 5.6.4 の追記漏れであり、実装しながら決めると段 3 のリスク集中がそのまま実装事故になる |
| 段 4（残り 10 操作 + 監視） | **S4-2 を直してから** | observation 2 操作は骨格コードをそのまま写せない（in-flight キー・Busy 文言・世代更新・`onNativeFailure` の 4 点が異なる）。段 4 着手前に差分骨格を書き足す |

S4-4 / S4-5 / nit 群は着手を止めるものではないが、段 2 の修正時にまとめて処理すれば追加の版を切らずに済む。
