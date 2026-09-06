# レビュー結果

- 日付: 2026-09-03
- 対象ファイル: artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v3.md
- 前版レビュー: artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v2.md
- 機能名: clipboard
- プラットフォーム: macOS
- ドキュメント種別: 実装計画書
- レビュー実施: サブエージェント 1 本（観点 A / B / C を統合）
  - `[A]` ネイティブ仕様との整合
  - `[B]` C# 実装可能性・既存実装とルールへの適合
  - `[C]` 内部整合性・抜け漏れ・リスク

---

## 強み

### 前回指摘の反映確認済み

**must fix 5 件（M1〜M5）、should fix 11 件（S1〜S11）、nit 12 件（N1〜N12）はすべて反映確認済み。** 過剰修正はなく、v2 で確認済みだった 1 章・6 章も編集の巻き込みによる破壊は無い（章 1 / 章 6 を v2 と逐行差分し、追加 5 箇所・変更 4 箇所のみで、いずれも指摘に対応する意図的変更であることを確認）。

S11 について補足: v2 レビューは「結果型 12 種 / 11 ファイル」への統一を求めたが、v3 は `MacClipboardConstants.cs` を #3 に挿入して番号を振り直した結果、**正しい数は 10 ファイル**になっている。v3 の「12 種 / 10 ファイル」が正しく、4.1 内訳・5.5.1・5.7・12 章の 4 箇所すべてで一致している。

### 確認済み（問題なし）

**A 領域の v3 新規記述 6 点はすべて一次ソースと一致。**

- lazy data provider の発動条件（`ClipboardContentValidator.swift:34-38` の `items.count == 1` かつ `totalBytes > warnBytesPerRepresentation`、`ClipboardLimits.swift:56` の warn=10 MiB）
- 変更イベント encode 失敗の黙殺
- 空白のみ pasteboard 名の素通り（`UnityMacClipboardJsonParser.swift:271/274/324` の `!name.isEmpty` と `PasteboardResolver.swift:45-48` の空名専用 1505）
- 1508 が unique にも名前一致で適用されること（`PasteboardResolver.swift:78-86`）
- tracker が scope 単位であること（`ClipboardChangeTracker.swift:18/28-32`）
- StartObserving から 1599 を外した判断（`ClipboardChangeMonitor.swift:72-78` により 1523 と resolve 由来のみ、resolve は空名専用で到達不能）

あわせて 1.1 の 15 関数と引数順、1.2 のコールバック型 3 種（`.h:44-59` と一字一致）、1.2.4 の NULL 契約、1.3 の JSON スキーマ 17 型、1.4 の必須・省略ルール（copy の guard 順 content→scope→options、`parseOptions` の nil/empty→default、`parseMatchingTypes` の二重 optional、`parsePatterns` の Set 化、readData の空 utType→1302）、1.5 のエラー表、6.3 の到達可能性表 15 行、6.4 の 9 行も全件一致。

**B / C 領域の以下も確認済み。**

- **段 2 の単独コンパイル性**: `MacClipboardConstants.cs` により Parser→Manager の逆依存が消え、段 3→段 2 の一方向で循環なし。`JsonValue.TryGetBase64Bytes(long maxDecodedLength, out byte[]?)` のシグネチャも `IosClipboardJsonReader.cs:209` と一致
- 5.6.10 のシーム表 19 行 22 本が公開メソッド 15 個・結果型 12 種・変更イベント 1 種を過不足なくカバー
- 7 章の層モデル適合（testing.md 1 節「層 1 で Manager インスタンスを生成しない」/ 3 節 A 群 / 6 節の機微情報分類）
- workflow.md ステップ 6 の必須項目 8 種すべて
- 4.2 の置換件数実測（Runtime 3 = parser 2 + クラス宣言 1、Tests 42 = Reader 40 + Builder 2）
- `AssemblyInfo.cs` の両アセンブリ `InternalsVisibleTo` 既存
- **数の整合が 7 種類すべてで一致**（17 ファイル 1〜17 連番と内訳 3+2+10+1+1 / テスト 5（EditMode 4 + PlayMode 1）/ パーサ 12 / 結果型 12 種 10 ファイル / 公開メソッド 15 / 公開イベント 13 / 手動確認 32 / ガードチェーン 6 段が 4.3・5.6.4・7.2 で一致）
- D-1〜D-12 と V-1〜V-11 に重複・食い違いが無いこと（とくに D-2 と V-6 の分離が明記されている）
- 5.5 の型名が 4.1 と 5.1 と完全対応（payload 13 型・検出 9 型 + 3 結果型）
- 断定と要検証の切り分け（マーシャリング・サイズ上限・コールバックスレッド・App Sandbox・lazy provider 実挙動はすべて V- 項目に逃がしてある）

---

## 改善点

### 高優先度

#### F-1 `[B]` 5.6.4 の `TryPassGuards` / `TryBeginOperation` 分割に、段階 5・6 の拒否を実行する手段が書かれていない

- 該当: 5.6.4（1044-1092 行）の 2 シグネチャと骨格コード、とくに 1069-1070 行の `catch (Exception ex) { /* 拒否経路で 9002 */ return; }` / `if (!TryBeginOperation(s_inFlight, key)) { /* 拒否経路で 9001 */ return; }`
- 根拠: `IosClipboardManager.cs:536-546`（`private static void DispatchRejectedResult<TResult>(TResult result, Action<TResult>? common, Action<TResult>? rejectedCallback)`）、`:625-632`（`internal static bool TryBeginOperation(HashSet<string>, string) => inFlight.Add(operation)` — 純粋関数で dispatch しない）、`:633-711`（iOS は 5 段すべての拒否を `TryStartOperation` 内部で dispatch している）
- 何が違うか: 分割によって段階 5・6 の拒否 dispatch は**呼び出し側の責務になった**が、その具体手段が計画書に無い。骨格コードは `TryPassGuards(...)` の引数を省略しているため、実装者は `commonSelector` / `failure` をインラインのラムダとして書き、段階 5・6 に到達した時点で再利用できるものが手元に無い状態になる。正しく書くには `Func<Action<TResult>?> commonSelector` と `Func<int,string,TResult> failure` を**ローカル変数に持ち上げ**、`DispatchRejectedResult(failure(9002, CouldNotStartMessage(op)), commonSelector(), onResult)` の形で呼ぶ必要がある。この構造要求が明記されていない。あわせて `DispatchRejectedResult` / `DispatchOffThreadRejection` / `EndOperation` / `InvokeNative` は 5.6.4 の段階表と骨格コードで名前だけ登場し、シグネチャがどこにも無い（`EndOperation` は 7.1 が EditMode テスト対象に挙げているのに宣言が無い）
- どう直すべきか: 5.6.4 に (1) `commonSelector` / `failure` をローカルに持ち上げる骨格（引数を省略しない完全形）、(2) `DispatchRejectedResult` / `DispatchOffThreadRejection` / `EndOperation` / `InvokeNative` の 4 シグネチャ、の 2 点を追記する。**型の不整合そのものは無い**（`failure` の `int` は `MacClipboardErrorInfo.Create(long, string?)` へ暗黙変換で通り、`MacClipboardOwnershipResult` の `Operation` は `failure` ラムダが operation 定数をクロージャで captures すれば供給できる）ので、追記のみで解決する

### 中優先度

#### S-1 `[B][C]` 5.5.1 の「コレクションは常に非 null（失敗時は空）」が入れ子型のコレクションに適用できない

- 該当: 5.5.1 の不変条件 3 分岐表 847 行、および 7.1 `MacClipboardResultTests` の「コレクションは失敗時も非 null」
- 根拠: 5.5.2 の型表 — `Items` は `MacClipboardReadContents` の、`ItemTypes` / `MatchingItemIndexes` は `MacClipboardSnapshot` の、`MacClipboardDetectedValues` のコレクション 9 件は同型のプロパティであり、いずれも「参照型 payload は失敗時 null」行の対象（`Contents` / `Snapshot` / `Values`）の**内側**にある
- 何が違うか: 失敗時は入れ子オブジェクト自体が null なので `Items` 等は存在しない。3 分岐表は「結果型 payload の種別」を分類しているのに、この行だけ入れ子オブジェクトのフィールドを混ぜている。7.1 のテストは書けない
- どう直すべきか: コレクション行を「結果型が直接持つコレクション（`MacClipboardDetectedPatternsResult.Patterns` のみ）は失敗時も非 null で空」に限定し、入れ子型のコレクション（`Items` / `ItemTypes` / `MatchingItemIndexes` / `DetectedValues` の 9 件）は別立てで「入れ子オブジェクトが存在する限り常に非 null」と書き分ける。7.1 の文言も同様に分ける

#### S-2 `[C]` 12 章「段 3 の Manager は結果型 4 種しか参照しない」が 5.6.2 と矛盾する

- 該当: 12 章 1538 行
- 根拠: 5.6.2（921-936 行）は公開イベント 13 本を 1 ブロックで規定しており、13 本は結果型 12 種 + `MacClipboardChangeEvent` を全部参照する。12 章 段 3 の範囲は「Manager 骨格 + 5 操作」
- 何が違うか: 「Manager 骨格」に公開 API 面が含まれるなら 12 種すべてを参照する。段 2 で全型が揃うのでコンパイルは通り**実害は無い**が、主張自体が誤り。この行は D-10 の根拠でも何でもなく、無くても段の境界の正しさは変わらない
- どう直すべきか: 「段 3 は自分が実装する 5 操作のイベント・メソッドだけを宣言し、残る 8 イベントは段 4 で追加する」に書き換えるか、この行を削除する

#### S-3 `[C]` 5.6.8 の teardown は段 4 の監視機構に依存するのに、12 章が段 3 に置いている

- 該当: 12 章 段 3 の「lifecycle / tombstone」、5.6.8
- 根拠: 5.6.8 は `RunDestroyCleanup(stop: StopObservingForTeardown, ...)`、`ClearAllPendingCallbacks()` による世代 3 変数のリセット（5.6.5）、`DiscardIfTerminated` の変更コールバックへの適用を段 3 の内容として規定している。一方 12 章は `StartObserving` / `StopObserving` と「監視の世代管理」を段 4 に置いている
- 何が違うか: 段 3 は `clipboardStopObserving` の `extern` 宣言、`s_observingGeneration` / `s_onChangedGeneration` / `s_pendingObservationGeneration` / `s_onChanged` の各フィールド、`StopObservingForTeardown`、変更コールバックの `DiscardIfTerminated` を**先に持たざるを得ない**。宣言してしまえばコンパイルは通るが、段の境界の説明が実態と食い違う
- どう直すべきか: 12 章 段 3 の範囲に「teardown が触る監視の static 状態と `StopObservingForTeardown`（公開 API は段 4）」を明記する

#### S-4 `[B][C]` 7.2 が段階 5（JSON 構築）の拒否経路を検証項目に挙げているが、駆動手段が無い

- 該当: 7.2 の 1385 行「段階 5: 9002」
- 根拠: 5.6.10 のシーム表に Builder を失敗させるシームが無く、5.3 の Builder は正常な `MacClipboardContent` からは例外を投げない。他 5 段は `BridgeAvailableOverrideForTests` / スレッド / tombstone / null 引数 / in-flight で駆動できる
- 何が違うか: このままでは 10 章 DoD の「対象範囲の新規テストがすべて作成され pass する」を段 3 で満たせない
- どう直すべきか: 駆動方法を明記する（`MacClipboardContentItem.FromRepresentations` に値が `null` の `byte[]` を含む辞書を渡せば `Convert.ToBase64String` が例外を投げ、段階 5 を通せる。5.6.3 の事前検証は要素の null を検査しないのでこれが有効な入口になる）か、シームを追加するか、7.2 の列挙から段階 5 を外して「実測不能」と記す

#### S-5 `[C]` 5.2.1 / 5.2.2 の見出し階層が `MacClipboardConstants.cs` を 5.2 の子に見せている

- 該当: 714 行 `### 5.2.1 ...`、762 行 `### 5.2.2 ...`
- 根拠: 他のサブ節（5.1.1 / 5.5.1 / 5.5.2 / 5.6.1〜5.6.12）はすべて `####`。5.2.1 / 5.2.2 だけ `###` で 5.2 と同階層でありながら子番号を持つ
- 何が違うか: 4.1 は #2 ErrorInfo / #3 Constants と 2 ファイルを並置しているのに、5 章では Constants に独立節が無く ErrorInfo の下位に見える。段 2 の成果物として D-10 で独立性を強調している主張と表記が噛み合わない
- どう直すべきか: `MacClipboardConstants.cs` を独立節（例: 5.3）に昇格させて以降を繰り下げるか、少なくとも `####` に落として 5.2 の見出しを「`MacClipboardErrorInfo.cs` と `MacClipboardConstants.cs`」に改める。参照している側（5.4 / 5.6.2 / 5.6.3 / D-10 / 12 章）の番号更新も忘れないこと

### 低優先度

- **N-1 `[A]`** 1.6（291 行）の引用行番号 `UnityMacClipboardManager.swift:393` は実際には **:396**。記述内容自体は正しい
- **N-2 `[A]`** 6.3 補足の「void 経路の 1599 の発生源は `completeVoid` の総称 catch だけ」は `removePasteboard` には当てはまるが、`startObserving` は `completeVoid` を通らず**ファサード自身の `catch { ClipboardError.unknown(...) }`**（`UnityMacClipboardManager.swift:404-407`）が発生源。結論（実質非到達なので挙げない）は変わらないので根拠の 1 語だけ直す
- **N-3 `[B]`** 5.5.1（838 行）の入れ子 `sealed class` 列挙にワイルドカード `MacClipboardDetected*` を使っているが、これでは `MacClipboardLabeledValue` / `MacClipboardPostalAddress` / `MacClipboardCalendarEvent` / `MacClipboardShipmentTracking` / `MacClipboardFlightNumber` / `MacClipboardMoneyAmount` の 6 型が掛からない。明示列挙するか「5.5.2 の検出結果表の全型」と書く
- **N-4 `[C]`** 5.6.2（999 行）の「購読者がいないため誰にも届かない」は共通イベントについては正しいが、**個別 callback（`onResult`）は新インスタンス経由でも発火する**。7.2 の該当テスト行は正確なので、5.6.2 の文言だけ「共通イベントは誰にも届かないが、個別 callback は発火する」に補う
- **N-5 `[B]`** iOS v5 5.6.2「delegate / callback スロット」に相当する節が無い。per-call スロット 15 本のフィールド名と `TakeXxxCallback` 相当の取り出し規約が未定義で、`HasAnyPendingCallbackForTests` の実装（`IosClipboardManager.cs:1697-1702` は 16 スロットを列挙）が段 3 で書き起こしになる。5.6 に 1 表足すと段 3 が軽くなる
- **N-6 `[C]`** 5.7 の 10 ステップは「12 章の 4 段階に対応する」と書くだけで対応が無い（実際は 1→段1 / 2-6→段2 / 7-8→段3 / 9→段4 / 10→全段）。1 列足すと 12 章との照合が機械的になる
- **N-7 `[C]`** iOS v5 9.6「single-flight の粒度が実運用で過剰でないか」に相当する要検証が 8 章に無い。10 章 DoD の「トレードオフを XML コメントに書く」は文書化義務であって粒度の妥当性検証ではない
- **N-8 `[B]`** 1.2.2 の引用 `MacShareManager.cs:58` は実際には **:59**、`IosClipboardManager.cs:193-200` は **:194-201**。いずれも指している内容は正しい
- **N-9 `[C]`** 5.6.10 の `CompleteOperationForTests(bool, long, string?)` は iOS の同名シーム（`IosClipboardManager.cs:1662-1664` は `operation` 引数を持つ）と引数が異なる。removePasteboard 専用なので問題は無いが、「operation は `MacClipboardOperations.RemovePasteboard` 固定」と 1 行足すとよい

---

## 不足項目

- 段階 5・6 の拒否 dispatch の具体手段と、`DispatchRejectedResult` / `DispatchOffThreadRejection` / `EndOperation` / `InvokeNative` の 4 シグネチャ（F-1）
- per-call スロット 15 本のフィールド名と取り出し規約（N-5、iOS v5 5.6.2 相当）
- single-flight の粒度が実運用で過剰でないかの要検証項目（N-7、iOS v5 9.6 相当）
- 5.7 の各ステップと 12 章の段の対応（N-6）

---

## 総合評価

**A 領域（ネイティブ仕様との整合）は完成している。** v3 で追加された 6 点はすべて一次ソースと一致し、v2 で確認済みだった 1 章・6 章も編集の巻き込みによる劣化が無い。指摘は引用行番号 2 件と根拠語 1 件の nit だけで、実装判断に影響しない。

**残る欠陥は 5.6.4 の分割に集中している。** M5 の反映として `TryPassGuards` / `TryBeginOperation` の 2 シグネチャは示されたが、分割によって呼び出し側へ移った段階 5・6 の拒否 dispatch をどう書くかが空欄のままで、ここだけが「書いてあるとおりに実装すると詰まる」箇所である（F-1）。型・引数の不整合そのものは無いので、骨格コードの完全形と 4 シグネチャの追記で解消する。

**C 領域の自己矛盾は 5 件、いずれも局所修正で済む。** 機械的置換に伴う編集漏れ・番号ずれ・参照切れを網羅的に探した結果、数の表記は 7 種類すべてが全節で一致しており、v2 で指摘した数え方の揺れは解消されている。残ったのは 5.5.1 の不変条件が入れ子型を巻き込んだこと（S-1、7.1 のテストが書けない）、12 章が段 3 の型参照を過小に主張していること（S-2）、teardown と段 4 の依存関係（S-3）、7.2 の段階 5 に駆動手段が無いこと（S-4）、Constants の見出し階層（S-5）である。

**設計密度は iOS v5 と同等に達した。** 1,395 行 → 1,554 行への加筆で操作名定数表・型種別規定・シーム引数表・分割方針が揃い、iOS v5 との差は per-call スロット表（N-5）と single-flight 粒度の要検証（N-7）の 2 件のみになった。

---

## 着手可否の判定

**条件付きで着手可。**

| 段 | 判定 | 条件 |
| --- | --- | --- |
| 段 1（`ClipboardJsonReader` への改名） | **即着手可** | 置換件数を実測で照合済み、`git mv` の逸脱も DoD で承認済み。修正待ちの項目は 1 つも掛からない |
| 段 2（型・定数・Builder・Parser） | **S-1 を直してから** | 1 行の書き換えで済むが、放置すると 7.1 の `MacClipboardResultTests` が書けず段 2 の DoD を満たせない。S-5 も同時に直すと参照更新が 1 回で済む |
| 段 3（Manager コア） | **F-1 の反映が必須** | あわせて S-2 / S-3 / S-4 を直す。F-1 は分割の実装形そのものなので、着手しながら決めると段 3 のリスク集中がそのまま実装事故になる |
| 段 4 | **追加の前提条件なし** | — |

段 1 着手前に V-1（xcframework 1.3.0 とソースの版対応）を確認する、という 12 章の指示は引き続き有効であり、これが未了なら全パーサが 9006 で落ちるため段 2 以降の意味が失われる。
