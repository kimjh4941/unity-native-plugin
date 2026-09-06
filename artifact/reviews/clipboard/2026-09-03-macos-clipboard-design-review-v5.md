# レビュー結果

- 日付: 2026-09-03
- 対象ファイル: artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v5.md
- 前版レビュー: artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v4.md
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

**M-1 / M-2 / M-3 / S4-1〜S4-5 / N4-1〜N4-9 の 17 件すべて反映確認済み。** とくに次はすべて一次実装と逐語レベルで一致していた。

- `InvokeNative` の 5 引数シグネチャ ← `IosClipboardManager.cs:1458-1463`
- `Dispatch` / `FireXxxResult` の規約 ← `:705-800`
- `StartObserving` の差分骨格 ← `:1040-1060`（`StopObserving` の `s_pendingObservationGeneration = s_observingGeneration;` も `:1087` と一致）
- 引用行番号（`:193-199` / `IosClipboardPayloads.cs:463-509` / `:1656-1704` / `:591-611`）

`Convert.ToBase64String(null)` が `ArgumentNullException` を投げること、`ValidateRequestSize` の `(int Code, string Message)?` が `validate` ラムダの target-typed 変換で成立することも確認済み。

### 確認済み（問題なし）

- **数の表記 12 種類が全節一致**: 17 ファイル / Tests 5 / パーサ 12 / 結果型 12 種 10 ファイル / 公開メソッド 15 / 公開イベント 13 / 手動確認 32（実カウント）/ per-call スロット 16 / ガードチェーン 6 段 / 実行段階 8 / V-1〜V-12 / D-1〜D-12。シーム総数 22 = 段 3 の 12 + 段 4 の 10 も一致
- 5.6.1〜5.6.13 に欠番・重複なし。章節参照はすべて実在節を指す（`5.2` → `5.2.0` の是正も反映済み）
- **M-1 の「12 操作必須」ルールと値を返さない 3 操作の記述は衝突しない**（15 = 12 + 3、`StartObserving` 差分骨格が「`onNativeFailureResult` は不要」と明記）
- **`representations` の序数順ソートは 1.3 のネイティブスキーマ・5.4 のパーサと矛盾しない**（ネイティブは辞書としてデコード、パーサ出力に順序要件なし）
- **7.2 の段マーク `(3)` / `(4)` は 12 章の段の範囲と矛盾しない**（変更イベント配送一式が段 3、世代管理と observation Busy 文言が段 4 という配置は一貫）
- `DispatchRejectedResult` / `DispatchOffThreadRejection` / `EndOperation` / `TryBeginOperation` / `InvokeNative` / `Dispatch` の 6 シグネチャは iOS 実装と一致
- A 領域（ネイティブ整合）は v5 で変更されておらず、新たな指摘なし
- workflow.md ステップ 6 の必須 8 項目はすべて充足。`.meta` 移動と last-registered wins の 2 つの逸脱はいずれも根拠付きで宣言済み

---

## 改善点

### 高優先度

#### M5-1 `[B][C]` `FireOwnershipResult` が `s_onCopy` / `s_onAppend` のどちらを退避するかが未規定で、規約の文言が誤っている

- 該当: 5.6.4「`FireXxxResult` の命名規約」1198-1201 行。「手順は必ず『per-call スロットを退避して null 化 → `EndOperation` → `Dispatch`』」＋「**`MacClipboardOperationResult` を返す 3 操作は** `Operation` からスロットを引く `TakeOperationCallback(string operation)` を経由する」
- 根拠: 5.6.12 は `MacClipboardOwnershipResult` に対し `s_onCopy` と `s_onAppend` の **2 本**のスロットを割り当てている。5.6.10 の `CompleteOwnershipForTests` も「`operation` が必須（**別 in-flight キー・別スロット**・結果の `Operation` の区別）」と 2 スロットであることを自ら認めている。iOS では copy / append が `IosClipboardOperationResult` を返すため `TakeOperationCallback`（`IosClipboardManager.cs:712-725`）が両方を処理しており、macOS のような「結果型は 1 つ、スロットは 2 本」の組み合わせは iOS に前例が無い
- 何が違うか: 「結果型ごとに 1 本ずつ、計 12 本」＋「operation からスロットを引くのは 3 操作だけ」という規約に素直に従うと、実装者は `FireOwnershipResult` の中に `var perCall = s_onCopy; s_onCopy = null;` と書く。すると **Append の `onResult` は永久に呼ばれず、`s_onAppend` がリークする**（M-1 で塞いだはずの穴と同じ失敗モード）。しかも copy / append は**段 3 の範囲**であり、段 3 着手直後に踏む
- どう直すべきか: 例の行を「`FireOwnershipResult(MacClipboardOwnershipResult result, string inFlightKey)` は `result.Operation` からスロットを引く `TakeOwnershipCallback(string operation)` を経由する」と明記する。あわせて規約文を「**複数操作が共有する結果型（`MacClipboardOperationResult` の 3 操作と `MacClipboardOwnershipResult` の 2 操作）は `Operation` からスロットを引く**」に一般化する

#### M5-2 `[B][C]` 制御呼び出しの完了で `ReleaseChangeRegistrationIfOwned()` を無条件に呼ぶと、成功した `StartObserving` が自分の `onChanged` 登録を即座に破棄する

- 該当: 5.6.5 状態遷移表 1283 行「制御呼び出しの完了（**成否問わず**）｜変更なし｜`ReleaseChangeRegistrationIfOwned()` の判定に従う｜`= 0`」と、5.6.10 の `CompleteObservationControlForTests` の説明
- 根拠: `IosClipboardManager.cs:1638-1652`（`HandleObservationControlCallback`）は解放に**外側のゲート**を置いている。

  ```csharp
  // A failed start never began observing, and any stop ends it: both give up the
  // registration, but only if this call is the one that created it.
  if (!isSuccess || operation == OperationStopObserving)
  {
      ReleaseChangeRegistrationIfOwned();
  }
  s_pendingObservationGeneration = 0;
  ```
- 何が違うか: 計画書は 5.6.5 で `ReleaseChangeRegistrationIfOwned` の**本体コードを逐語で示している**が、その条件は `s_onChangedGeneration != 0 && s_onChangedGeneration <= s_pendingObservationGeneration` だけ。`StartObserving` 段階 7 の直後は `s_onChangedGeneration == s_pendingObservationGeneration == G` なので、**成功完了でも `G <= G` が成立して `s_onChanged = null` になる**。結果として `StartObserving(onChanged: ...)` の個別 callback が一度も発火しない（共通イベント `ClipboardChanged` だけが出る）。7.5 #16 と 7.2 の「start → stop → start で登録が正しく入れ替わる」も、この記述どおりに実装すると成立しない
- **これは v5 が持ち込んだ欠陥ではなく、v3 から 3 版・3 回のレビューを生き延びた既存の抜けである**（v3 / v4 / v5 の当該行が同一であることを確認）。ただし v5 が `StartObserving` の差分骨格でこの経路を呼び出すコードを追加したため、実装者が確実に踏む位置に移動した
- どう直すべきか: 5.6.5 の表の行を「**`!isSuccess` または `operation == StopObserving` のとき**のみ `ReleaseChangeRegistrationIfOwned()`。成功した `StartObserving` では呼ばない」に改め、上記 iOS の 4 行を根拠コメントごと転記する。5.6.10 の該当説明にも同じゲートを書く

### 中優先度

#### S5-1 `[B]` `MaxRequestBytesOverrideForTests` の値がどこで効くかが未規定で、5.6.3 の `const` 参照と矛盾したまま

- 該当: 5.6.10 の新シーム行 1362 と、5.6.3 の「合計バイト数が `MacClipboardLimits.MaxRequestBytes` を超える → 9007」・`ValidateRequestSize` のシグネチャ・6.1 の文言
- 根拠: `MacClipboardLimits.MaxRequestBytes` は `const long`（5.2.2）。iOS の同種シームは `BridgeAvailableOverrideForTests`（`IosClipboardManager.cs:511`）を `IsBridgeAvailable()`（`:514-522`）が `#if UNITY_EDITOR` 分岐で読む形をとっており、**読み側の分岐が対で必要**であることが前例として示されている
- 何が違うか: v5 は override プロパティを足しただけで、`ValidateRequestSize` が実効上限をどう解決するかを書いていない。素直に書くと `const` を直接参照するので**シームが効かず、7.2 の「段階 3 の 9007 は `MaxRequestBytesOverrideForTests` で駆動する」が成立しない**。さらに override は `#if UNITY_EDITOR` 側にしか存在しないため、guard 無しで参照すると Standalone ビルドがコンパイルできない
- どう直すべきか: 5.6.3 に実効上限の解決を明記する。

  ```csharp
  private static long EffectiveMaxRequestBytes()
  {
  #if UNITY_EDITOR
      return MaxRequestBytesOverrideForTests ?? MacClipboardLimits.MaxRequestBytes;
  #else
      return MacClipboardLimits.MaxRequestBytes;
  #endif
  }
  ```
  あわせて 6.1 の 9007 メッセージが**実効上限**を埋め込むこと、`ResetForTests()` が override を null に戻すことも 1 行ずつ足す

#### S5-2 `[B]` 合計バイト数を `int` で積むため、超過ケースそのもので `OverflowException` が公開 API から伝播しうる

- 該当: 5.6.3 の 1043 行 `item.Representations.Sum(r => r.Value?.Length ?? 0)`
- 根拠: `Enumerable.Sum(IEnumerable<T>, Func<T,int>)` は checked で積算し、`int.MaxValue`（約 2 GiB）を超えると `OverflowException` を投げる。この加算は段階 3（`TryPassGuards` の `validate` ラムダ内）で走るため、**どこにも捕捉されず公開 API `Copy` から呼び出し側へ例外が漏れる**
- 何が違うか: 「拒否は結果で返す」という 5.6.2 の契約違反であり、M-3 で潰したはずの失敗モードが別条件（3 × 1 GiB の representation など、まさに 9007 が防ごうとしている入力）で復活する。`MaxRequestBytes` が `long` であることとも整合しない
- どう直すべきか: `Sum(r => (long)(r.Value?.Length ?? 0))` に直し、item 横断の合計も `long` で積むと明記する

#### S5-3 `[C]` `ObservationBusyMessage()` がメッセージヘルパの列挙から漏れている

- 該当: `StartObserving` 差分骨格 1224 行が `ObservationBusyMessage()` を呼ぶが、1206 行のヘルパ列挙に含まれていない
- 何が違うか: この 1 行はヘルパの**全列挙**として読める書き方なので、骨格に出てくる 6 番目の識別子が未定義に見える。文言自体は 6.1 に定義済みなので実害は小さいが、v4 の M-2（未定義 `TooLargeOrNull`）と同じ形の抜け
- どう直すべきか: 列挙に `ObservationBusyMessage`（引数なし、6.1 の `Another observation control call is already in progress.` を返す）を加える

#### S5-4 `[C]` 5.6.2 が既に存在しない `TryStartOperation` を名指ししている

- 該当: 5.6.2 の 1008 行「`private static TryStartOperation` へは iOS と同じ `Func<Action<TResult>?> commonSelector` 引数を使い」
- 根拠: D-11 と 5.6.4 で `TryStartOperation` は `TryPassGuards` と `TryBeginOperation` に分割済み。macOS 側にこの名前の関数は存在しない
- 何が違うか: 12 章 1775 行が「段 3 の着手前に 5.6.2 の決定 2 と 5.6.4 の `TryPassGuards` 分割を理解していること」を求めているのに、その 2 節が別名で同じものを指している。段 3 の入口で最初に読まれる 2 節なので混乱が直撃する
- どう直すべきか: 1008 行の `TryStartOperation` を `TryPassGuards` に置換する（2.3 の 336 行は iOS の説明なので現状のままでよい）

#### S5-5 `[C]` 7.2 の 1612 行だけ段マークが無く、直上の子項目と重複している

- 該当: 7.2 の「拒否経路が in-flight マーカーと per-call スロットに触れないこと」
- 根拠: 1602 行が「段 3 の DoD は `(3)` の項目だけで判定する」と宣言しており、マーク無しの項目があると判定できない。内容も 1609 行の子項目と同一
- どう直すべきか: 1612 行を削除する（1609 行に統合済みのため）。残す場合は `(3)` を付ける

### 低優先度

- **N5-1 `[C]`** 5.6.4 の「`FireXxxResult`（結果型ごとに 1 本ずつ、**計 12 本**）」は直後に挙げる `FireClipboardChanged` を含まない。「結果型 12 本 + 変更イベント 1 本の計 13 本」と書くと他の数の表記と同じ粒度で照合できる
- **N5-2 `[B]`** 5 章冒頭で `#nullable enable` を必須にしたため、`MacClipboardContentItem.Representations` の値型 `byte[]` は非 null 注釈になる。5.6.3 が意図的に許容する「値が null の要素」と 7.2 の駆動手段は、テスト側で `null!` を書く必要がある。「nullable 注釈上は非 null だが、注釈を抑止した呼び出し側のバグとして段階 5 に落ちる」ことを 5.6.3 に 1 行足すと、実装者が factory に null ガードを足してしまう事故を防げる
- **N5-3 `[C]`** 5.6.3 の「要素の null 検査は C# でもネイティブでも行わない」と、同じ箇条書き内の「`content.Items` の要素が null の場合も同様にスキップする」（＝ null 検査を行う）は字面が衝突する。「null を**エラーとして扱わない**（0 バイト扱いでスキップし、拒否コードを割り当てない）」と言い換えるのが正確
- **N5-4 `[B]`** 骨格コードの `s_copyDelegate` / `s_startObservingDelegate` / `s_changeDelegate` という永続 delegate の命名規約が 3.2 に無い。`s_<operation>Delegate` と 1 行で規定すると 16 フィールドの名前が確定する
- **N5-5 `[C]`** 逸脱宣言の引用行が 3 件とも 1 行ずれている。`IosClipboardManager.cs:44-48` → 43-47、`IosClipboardJsonBuilder.cs:22-25` → 23-26、2.2 と 3.2 の `MacShareManager.cs:57-58` → 58-59
- **N5-6 `[C]`** 7.1 の「`ObservationControlKey` が Start / Stop 以外のキーと衝突しないこと」は、実際には `"observation"` がどの操作名とも一致しないことの検証なので、「15 の操作名のいずれとも一致しないこと」と書いたほうがテストが一意に決まる

---

## 不足項目

- `TakeOwnershipCallback` と、複数操作が共有する結果型のスロット解決規約（M5-1）
- `ReleaseChangeRegistrationIfOwned` を呼ぶ外側のゲート条件（M5-2）
- `EffectiveMaxRequestBytes()` と `ResetForTests()` での override リセット（S5-1）
- `ObservationBusyMessage` のヘルパ列挙への追加（S5-3）
- 永続 delegate の命名規約 `s_<operation>Delegate`（N5-4）

---

## 総合評価

**「前回の must fix を直すと、その修正自体が新しい must fix を生む」パターンは、ほぼ止まった。** v4 の 17 指摘はすべて正確に反映され、しかも骨格コード・ヘルパ 6 本・差分骨格のいずれも一次実装（`IosClipboardManager.cs`）と逐語レベルで一致している。v5 が新たに持ち込んだ欠陥は **M5-1（`FireOwnershipResult` のスロット解決）1 件のみ**で、これは S4-3 で新設した命名規約の一般化不足という局所的なもの。v4 が「コンパイル不能」「12 操作の失敗経路が塞がる」という規模だったのに比べ、明確に収束している。

一方で、**M5-2 は v3 から 3 版・3 回のレビューを生き延びた既存の抜け**であり、v5 が `StartObserving` の差分骨格を追加したことで初めて可視化された。成功した `StartObserving` が自分の `onChanged` を即座に破棄するという、監視機能の中核が動かない誤りなので、段 4 の着手前に必ず潰す必要がある。「v5 の品質が落ちた」のではなく、「**骨格が具体化したことで、これまで抽象的すぎて検証できなかった箇所の誤りが検出可能になった**」と読むべき。

残る should fix 5 件は、いずれも 1〜5 行の局所修正で、設計判断の変更を伴わない。

---

## 着手可否の判定

| 段 | 判定 | 条件 |
| --- | --- | --- |
| 段 1（`ClipboardJsonReader` への改名） | **即着手可。修正待ちの指摘は 1 件も掛からない** | V-1（xcframework 1.3.0 とソースの版対応）の確認のみが前提 |
| 段 2（型・定数・Builder・Parser） | **即着手可。修正待ちの指摘は 1 件も掛からない** | `#nullable enable`・`representations` の序数順ソート・`MacClipboardConstants.cs` の独立はいずれも v5 で確定済み。N5-2 を段 2 の実装前に決めておくと、factory に不要な null ガードを足す手戻りが消える |
| 段 3（Manager コア） | **M5-1 と S5-1 の修正が必須** | M5-1 は copy / append（段 3 の中心）で `s_onAppend` がリークする。S5-1 は 7.2 の段階 3 テスト（`(3)` マーク＝段 3 の DoD 対象）が駆動できない。あわせて S5-2 / S5-4 / S5-5 も同時に直せば追加の版を切らずに済む |
| 段 4（残り 10 操作 + 監視） | **M5-2 と S5-3 の修正が必須** | M5-2 を残したまま実装すると、`StartObserving` の `onChanged` が一度も発火せず、7.2 の世代管理テストと 7.5 #15 / #16 が通らない。iOS の `HandleObservationControlCallback`（`IosClipboardManager.cs:1644-1651`）のゲートを 5.6.5 に転記すれば足りる |

nit 6 件はいずれも着手を止めるものではない。段 3 の修正時にまとめて処理することを推奨する。
