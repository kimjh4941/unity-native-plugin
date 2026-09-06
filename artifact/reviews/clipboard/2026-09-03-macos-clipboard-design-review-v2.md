# レビュー結果

- 日付: 2026-09-03
- 対象ファイル: artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v2.md
- 前版レビュー: artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v1.md
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

- **must fix 6 件はすべて正しく反映されている。** 6.3 の到達可能性表は `ClipboardRepositoryImpl.swift` / `PasteboardResolver.swift` / `UnityMacClipboardManager.swift` / 各 UseCase を全経路たどって照合した結果、**15 操作すべての到達コードが実装と一致し、削りすぎも追加漏れも無い**
  - 1507 が read `:141-143` と snapshot `:164-166` の 2 箇所だけであること
  - resolver が 1507 ではなく 1505 を throw すること
  - readData が UTI 検証も `pasteboardItems` 呼び出しもしないこと
  - detectMetadata が同じ `detectionError(from:)` を通ること
- 中・低優先度も反映済み: 1.5 の 1524 分類、7.5 #2 の append changeCount 不変（`ClipboardRepositoryImpl.swift:103-105` のコメントと一致）、`[MarshalAs(UnmanagedType.I1)]`、EditMode→PlayMode の移動、`(int)` narrow の明示判定、rawValue 対応表（複数形の注記込み）、`MacClipboardContentItem` への改名、`git mv` での `.meta` 移動、lifetime 契約、`s_dispatcher` 規約、IL2CPP、DoD、採用しなかった案、namespace、9006 統一、delegate 型を guard 外、分離 try/catch、in-flight リークの段階順序、observation Busy 専用文言、`DiscardIfTerminated` の変更イベント適用、`[TearDown] ResetForTests`

### 新規記述の確認済み

- **1.3.1 の入れ子 8 型**: 全フィールド名と「明示的 null か」の判定が `UnityMacClipboardJsonParser.swift:93-190` と 1 字単位で一致。`MoneyAmount.amount` が `Double`（`ClipboardDetection.swift:185`）である点も 5.5.2 の `double Amount` と一致
- **1.6 の新規記述**: tracker 共有（`ClipboardChangeMonitor.swift:85` / `MacClipboardManager.swift:115-121`）、「Use this instead of observation, not alongside it」（`:523-525`）、1508 の対象 5 種（`PasteboardResolver.swift:21-27`）、`sortedKeys` のみ（`:355-356`）、`ScopeJson.name` の合成エンコード省略、`OptionsJson` の `Bool? ?? true`、監視間隔 `0 < i <= 60`、15 関数の名前・引数順（`callback → onChange`）、stopObserving の NULL 許容、createPasteboard の NULL 挙動、matchingTypes の NULL/`[]`、accessBehavior の 15.4 挙動 — すべて一致
- **決定 1 の前提**: 委ねた 4 条件がすべて記述どおり（utType 空 → 1302 `UnityMacClipboardManager.swift:182` / 標準 pasteboard → 1508 / interval 範囲外 → 1523、**負値・NaN も 1523** / 空 patterns → パース成功後 1503）。detect の guard 束ねによる誤分類も、C# が enum から JSON を生成する以上到達しないことを確認（patternsJson が NULL のときだけ 1301/1302 が入れ替わるが、5.6.3 が `patterns == null` を 9005 で先に落とすので発生しない）
- **5.6.5 の状態遷移表**は `IosClipboardManager.cs:1041-1043 / 1087 / 1649-1651 / 472-474 / 815-821` と完全一致し、single-flight とも矛盾しない
- **数の一致**: 16 ファイル / テスト 5 / パーサ 12 / 公開メソッド 15 / 公開イベント 13 / 手動確認 29 / 型 17（入力4・共用3・出力9・イベント1）。4.2 の置換件数も実測一致（Runtime 3、Tests 42）
- **workflow.md の必須項目**と **7 章の層モデル適合**（testing.md 1 節・3 節 A 群・6 節）

---

## 改善点

### 高優先度

#### M1 `[A]` 1.6 の lazy data provider の発動条件が同じ節の中で矛盾している

- 該当: 1.6「10 MiB 超の単一 item は lazy data provider 経路に入る」の 4 つ目の箇条「C# の `MaxRequestBytes`（32 MiB）までの copy は、単一 item なら**必ずこの経路に入る**」
- 根拠: `ClipboardContentValidator.swift:36-37` — `guard content.items.count == 1 else { return false }` / `return content.totalBytes > limits.warnBytesPerRepresentation`（10 MiB）
- 何が違うか: 発動条件は「単一 item **かつ** 10 MiB 超」。1 MiB の単一 item copy はこの経路に入らない。2 行上の自分の記述と矛盾している
- 直し方: 「単一 item かつ 10 MiB 超の copy（C# 上限 32 MiB の範囲では 10〜32 MiB 帯）がこの経路に入る」に書き換える。7.5 #21 / #22 の前提は正しいので変更不要

#### M2 `[B][C]` 決定 2 の「確実に発火する」が成立しない（5.6.2 と 5.6.1 / 11 章が矛盾）

- 該当: 5.6.2「拒否経路: `_instance` に依存しないため、`OnDestroy` で `_instance = null` にした後の 9004 拒否でも**確実に発火する**」、D-4、7.2 の「破棄後の呼び出しが 9004 で拒否され、共通イベントが発火すること（決定 2 の検証）」
- 根拠: 5.6.1「破棄後に `Instance` を参照 → 新しい GameObject が生成される」、11 章「`Instance` getter の Unity `==` が破棄済みオブジェクトを null と判定するため、結局新しい GameObject が生成される」、`IosClipboardManager.cs:139-146`
- 何が違うか: **C# の言語上の可否は問題ない**（field-like event は宣言型の内部では下位デリゲートフィールドとして読めるので `() => this.XxxCompleted` を `private static TryStartOperation` に渡せる。`this` の managed フィールド読みは破棄済み MonoBehaviour でも off-thread でも安全で、Unity の `==` を踏まない）。成立しないのは**契約のほう**。破棄後に `MacClipboardManager.Instance.Copy(...)` と呼ぶと `this` は**新しく生成された別インスタンス**で、そのイベントには購読者がいない。決定 2 が救えるのは「呼び出し側が破棄前の参照をキャッシュしている場合」だけであり、11 章が却下案の理由として挙げた事実がそのまま決定 2 の穴になっている
- 直し方: 契約を「呼び出し側が保持している instance の共通イベントに発火する。破棄後に `Instance` から取り直した場合は別インスタンスであり、旧購読者には届かない」に訂正する。7.2 のテストは「破棄前に取得した参照を保持したまま呼ぶ」形であることを明記する

#### M3 `[C]` 12 章 段 2 が単独でコンパイルできない

- 該当: 12 章 段 2（Payloads / ErrorInfo / 結果型 / Builder / Parser + EditMode 3 テスト）と 5.4 の「`maxDecodedLength` は 5.6.3 の `MaxResponseBytesPerRepresentation` を渡す」
- 根拠: 5.6.3 は `MaxRequestBytes` / `MaxResponseBytesPerRepresentation` を「5.6 `MacClipboardManager.cs`」配下で宣言している。`MacClipboardManager.cs` は 4.1 #16 で段 3 の成果物
- 何が違うか: 段 2 の Parser（と「base64 上限超過の失敗」を検証する `MacClipboardJsonParserTests`）が段 3 の型を参照する。段の境界が成立しない
- 直し方: サイズ定数を段 2 に置ける型へ移す（`MacClipboardErrorInfo.cs` に `MacClipboardLimits` を併置する等）か、`maxDecodedLength` を Parser メソッドの引数にする。**同じ問題が S3 の operation 名定数にもあるので、置き場所は 2 件まとめて決めること**

#### M4 `[B]` 5.6.10 の完了シームが 5.6.2 の 15 メソッドを実際にはカバーできない（3 点）

- 該当: 5.6.10 のシーム表、7.2 の世代管理テスト
- 根拠: `IosClipboardManager.cs:1637-1653`（`HandleObservationControlCallback`）、`:1665-1667`（`CompleteObservationControlForTests`）、`:1661-1663`（`CompleteOperationForTests`）
- 何が違うか:
  1. **StartObserving / StopObserving を `CompleteOperationForTests` に載せている。** iOS はこの 2 操作だけ専用の `CompleteObservationControlForTests` を持ち、そこでのみ `ReleaseChangeRegistrationIfOwned()` と `s_pendingObservationGeneration = 0` が走る。通常の operation 完了経路では 5.6.5 の状態遷移が起きず、7.2 の「start → stop → start で登録が入れ替わる」「`PendingObservationGenerationForTests` の遷移」を駆動できない。**しかも v2 自身が 2.3 で `CompleteObservationControlForTests` を既存シームとして列挙しており内部矛盾**
  2. **`CompleteOwnershipForTests(bool, string?, long, string?)` に operation 引数が無い。** copy と append は別 in-flight キー・別 per-call スロットで、結果の `Operation` も区別が必要（5.5.2 / 7.1）
  3. **`CompleteDetectForTests` 1 本で 3 操作・3 結果型を注入できない。** DetectPatterns / DetectValues / DetectMetadata は別 in-flight キー・別結果型
- 直し方: `CompleteObservationControlForTests(string, bool, long, string?)` を追加し、`CompleteOwnershipForTests` に operation を足し、Detect を 3 本に割る。各シームの引数リストを表に書く

#### M5 `[C]` 段階 5（JSON 構築）を段階 4 と 6 の間に置くと、`TryStartOperation` の構造が iOS のままでは実装できない

- 該当: 5.6.4 の段階表
- 根拠: `IosClipboardManager.cs:633-711` — `TryStartOperation` は 5 段を 1 本の `private static` で通し、**最後に in-flight を取得して true を返す**。呼び出し側がその途中に処理を差し込む余地が無い
- 何が違うか: v2 は段階 5（JSON 構築）を段階 6（in-flight 取得）より前に置くと決めたが、その構造変更が書かれていない。JSON 構築を `TryStartOperation` の前に出すと段階 1〜4 より前になり、段階表と矛盾する
- 直し方: 「`TryStartOperation` を段階 1〜4 の `TryPassGuards` と段階 6 の `TryBeginOperation` に分割し、呼び出し側が両者の間で JSON を構築する」か「`Func<string>` の JSON ビルダを `TryStartOperation` に渡し、段階 4 の後・段階 6 の前に評価する」のどちらかを 5.6.4 に明記する

### 中優先度

#### S1 `[B]` 5.5 が結果型の種別（`readonly struct` / `sealed class`）を規定していない

- 根拠: `IosClipboardErrorInfo.cs:25`、`IosClipboardReadResult.cs:49` — iOS は結果型がすべて `public readonly struct`、値を運ぶ入れ子型が `sealed class`
- `Error` を `MacClipboardErrorInfo?` と書いている以上 struct 前提の記述だが、明示が無いと実装者が class にしうる（そうすると `Error == null` の意味が変わる）
- 5.5.1 に「結果型は `public readonly struct`。入れ子の値型（`MacClipboardItem` / `MacClipboardReadContents` / `MacClipboardSnapshot` / `MacClipboardDetected*`）は `sealed class`」と規定する

#### S2 `[B][C]` 5.5.1 の不変条件に、明示されている以外の例外が 4 件ある

- 該当: 「`IsSuccess == false` ⇔ payload プロパティが `null`（`MacClipboardReadDataResult.Data` を除く）」
- 実際の例外: `MacClipboardChangeCountResult.ChangeCount`（`int`、失敗時 0）/ `MacClipboardAccessBehaviorResult.Behavior`（enum、失敗時 `Unknown`）/ `MacClipboardForegroundChangeResult.Changed`（`bool`、失敗時 false）/ `MacClipboardDetectedPatternsResult.Patterns`（常に非 null コレクション）。いずれも 5.5.2 の同じ表に書かれている
- 7.1 が「5.5.1 の不変条件すべて」を検証すると書いているので、このままではテストが書けない
- 「参照型 payload は失敗時 null / 値型 payload は失敗時に既定値 / コレクションは常に非 null」の 3 分岐に書き直す

#### S3 `[C]` operation 名定数と single-flight キーの対応表が無い（iOS v5 3.6 相当）

- `MacClipboardOperationResult.Operation` / `MacClipboardOwnershipResult.Operation` は公開契約で、7.1 も「copy / append を区別できること」を検証すると書いているのに、文字列値がどこにも定義されていない。6.1 の `{operation}` メッセージ、5.6.5 の `ObservationControlKey`、6.3 の操作名がすべてこの表に依存する
- 15 操作の Operation 値と single-flight キー（Start / Stop のみ共有キー `"observation"`）の対応表を追加し、置き場所（Manager か段 2 の型か）を M3 と同時に決める

#### S4 `[C]` ガードチェーンの段数表記が節ごとに食い違う

- 5.6.4 はネイティブ呼び出し前の拒否段階を 6 段（9003 / 9004 / 9005・9007 / 9002 / 9002 / 9001）にしたが、4.3 の PlayMode テスト説明と 7.2 は「ガードチェーン 5 段」のまま。2.3 の 5 段は既存 iOS 実装の記述なので正しい
- 4.3 / 7.2 を 6 段に直す

#### S5 `[A]` 6.3 の void 経路 3 操作で 1599 の扱いが不揃い

- RemovePasteboard = 1508 のみ、StartObserving = 1523 + 1599、StopObserving = なし
- 6.3 の補足は「void 経路の 1599 は `ClipboardError` 以外を catch した場合に限る」と 3 操作を同列に説明している（native 確認: removePasteboard / startObserving とも `completeVoid` の総称 catch しか 1599 の発生源が無く、実質非到達）
- 3 操作とも同じ扱い（1599 を挙げる／挙げない）に揃える

#### S6 `[A]` 「1505 は到達不能」は正しいが、空白のみの名前は素通りする

- 根拠: `UnityMacClipboardJsonParser.swift:271` / `:274` / `:324` は `guard let name = shape.name, !name.isEmpty` であり、`" "` は通過する。resolver の 1505 も空名専用なので、`NSPasteboard(name: " ")` が実際に作られて**成功**する
- これを止めているのは 5.1 の C# factory（`MacPasteboardScope.Named("")` などが `ArgumentException`）だけである
- 6.3 の 1505 注記か 5.1 に「空白名の拒否は C# factory が唯一の防波堤であり、決定 3 の『ネイティブに委ねる』の例外である」と 1 行足す

#### S7 `[A]` 変更イベントの encode 失敗をネイティブが黙って捨てる点が未記載

- 根拠: `UnityMacClipboardManager.swift:393` — `guard let json = parser.encodeChangeEvent(event) else { return }`。エラー通知なしにイベントが 1 件消える
- C# からは「変更が起きたのに `ClipboardChanged` が来ない」としてしか観測できない。1.6 の監視の項と 6.4 に追記する

#### S8 `[C]` 5.6.4 の段階 8 が 9006 の帰属を書いていない

- 段階 8 を「ネイティブのコード」としているが、9006（`ResponseParseFailed`）は段階 8 で C# が生成する managed コード（5.4 / 6.1 に記載あり）
- 段階表に「段階 8 の失敗はネイティブのコード、または解析不能時の 9006」と追記する

#### S9 `[B]` 4.1 #7 の内容が 5.5 と対応していない

- 5.5.2 では `MacClipboardReadResult.cs` に `MacClipboardItem` / `MacClipboardReadContents` / `MacClipboardReadResult` の 3 型があるが、4.1 は「`MacClipboardItem`（読み出し item）と結果型」の 2 型しか書いていない。`MacClipboardReadContents` を明記する

#### S10 `[B]` v1 レビューの「Read 結果の入れ子構成が iOS と逆」への回答が未反映

- 根拠: `IosClipboardReadResult.cs:50-62` は `NumberOfItems` + `Items` のフラット構成
- v2 は `MacClipboardReadResult.Contents`（入れ子）を採ったが、理由が書かれていない（前回レビューは「揃えるか理由を書く」だった）。5.5.2 に 1 行（changeCount と items を ownership 相当の 1 値として扱うため 等）を書く

#### S11 `[C]` 「結果型 11」の数え方が節で揺れる

- 5.5 が定義する結果型は 12 種（Operation / Ownership / Read / ReadData / Snapshot / ChangeCount / ScopeResult / AccessBehavior / ForegroundChange / DetectedPatterns / DetectedValues / DetectedMetadata）。「11」は 4.1 のファイル #5〜#15 の**ファイル数**であり、しかもそこには結果型でない `MacClipboardChangeEvent.cs` が含まれる
- 5.7 は「結果型 11 ファイル」と書けているが 12 章 段 2 は「結果型 11」。「結果型 12 種 / 11 ファイル」に統一する

### 低優先度

- **N1 `[A]`** 6.3 に 1301 / 1302 が 1 行も無い。層別に切り出した設計上は正しいが、冒頭に「1301 / 1302 は引数をパースする全操作に共通で到達する（6.2 参照）」と 1 行入れると読み違いが減る
- **N2 `[A]`** 1508 の判定は `kind == "named"` だけでなく `"unique"` でも標準 5 名に一致すれば true（`PasteboardResolver.swift:78-86`）。5.6.3 の削除理由に 1 語足すだけでよい
- **N3 `[A]`** `CheckForegroundChange` は**別 scope** なら tracker 未登録で初回 true になる（`ClipboardChangeTracker.swift:28-32`）。6.4 の行にも scope 限定であることを書くと 7.5 #20 の期待値がぶれない
- **N4 `[C]`** 8 章冒頭が「BOOL のマーシャリングは 9 章へ移した」と書きながら V-6 に残っている。宣言の決定（D-2）と実挙動の実測（V-6）は別物なので、V-6 の文言を「D-2 で確定した宣言が実機で正しく動くことの実測」に直す
- **N5 `[C]`** V-7（`localOnly`）は実測項目ではなく「ネイティブの注意を XML コメントに引き写す」作業。8 章ではなく 5.6.11 か 10 章 DoD が適切
- **N6 `[C]`** `testing.md` 7 節の層 1 カバレッジ表は「都度更新」対象で、macOS Clipboard は現在「対象外」。4.5 の非変更一覧にも 10 章 DoD にも更新項目が無い
- **N7 `[B]`** 5.6.2 は「拒否経路は `this` から取る」と書くだけで、`private static TryStartOperation` へどう渡すか（iOS と同じ `Func<Action<TResult>?> commonSelector` に `() => this.XxxCompleted` を渡す形）が書かれていない。実装者が `_instance` 版と取り違えないよう 1 行明記する
- **N8 `[B]`** `Awake` の重複インスタンス破棄経路で `this` を掴んだまま呼ばれると、`this.XxxCompleted` は購読者ゼロの複製のイベントになる。M2 と同性質なので契約文にまとめて書けばよい
- **N9 `[C]`** 5.6.10 のシーム表は段 3 で全部入るように読めるが、Detect / AccessBehavior / ForegroundChange / Snapshot / CreatePasteboard の完了経路は段 4 の操作。12 章に「シームは対応する操作と同じ段で追加する」と 1 行足す
- **N10 `[C]`** 4.2 の `git mv` による `.cs.meta` 移動は workflow.md ステップ 6 と common.md「ファイル作成ルール」に対する意図的な逸脱。理由は書けているので問題ないが、10 章 DoD の該当行と合わせて「承認済みの逸脱」と明記しておくと implement 時にぶれない
- **N11 `[C]`** single-flight の公開トレードオフ（同一操作は並行不可・異種操作は並行可）を XML コメントに書く契約が無い（iOS v5 5.6.3 は明記）。11 章は却下案の記録のみ
- **N12 `[C]`** 7.5 に「空 patterns → 1503」「空 utType → 1302」の手動確認が無い。決定 1 でネイティブに委ねた 4 条件のうち #10（1508）と #17（1523）の 2 つしか確認されていない

---

## 不足項目

- operation 名定数と single-flight キーの対応表（S3、iOS v5 3.6 相当）
- 結果型の種別規定（`readonly struct` / `sealed class`）（S1）
- サイズ定数の置き場所の確定（M3）
- `CompleteObservationControlForTests` と、シーム各種の引数リスト（M4）
- `TryStartOperation` の分割方針（M5）
- `testing.md` 7 節の層 1 カバレッジ表の更新（N6）
- single-flight の公開トレードオフを XML コメントに書く契約（N11）

---

## 総合評価

**v1 の主要な欠陥はほぼ解消されており、A 領域（ネイティブ仕様との整合）は実質的に完成している。** 6.3 の到達可能性表は全 15 操作を一次ソースと逐条照合しても不一致が出ず、前回の「削りすぎ」懸念は杞憂だった。1.3.1 の入れ子 8 型、tracker 共有、`sortedKeys`、1508 の範囲、決定 1 が委ねた 4 条件も、すべて実装どおりである。A の残課題は M1（lazy provider の発動条件が節内で自己矛盾）と S5〜S7 の 3 件で、いずれも局所修正で済む。

**残るリスクは B / C 領域、とくに段 3 に着手する前に潰すべき 3 点に集中している。** M4（完了シームが監視の世代遷移を駆動できず、7.2 の中核テストが成立しない）、M5（段階 5 の挿入で `TryStartOperation` の構造変更が必要なのに未記載）、M2（決定 2 の契約が 5.6.1 / 11 章と矛盾）は、いずれも「書いてあるとおりに実装すると破綻する」種類の欠陥である。M3 と S3 は段の境界に関わるので、段 1 着手前に定数の置き場所を決めておく必要がある。

**設計密度の不足は解消された。** 992 行 → 1,395 行への加筆で型仕様表・lifetime 契約・段階表・世代遷移表・DoD・却下案がすべて揃い、iOS v5 との差は operation 名定数表（S3）と結果型の種別規定（S1）に絞られた。節をまたぐ不整合も探した限りでは S4 / S8 / S9 / S11 の 4 件で、いずれも表記レベルである。

**推奨: 段 1（改名）と段 2（型・Builder・Parser）は M3 / S3 の定数配置を決めれば着手してよい。段 3 の着手前に M2 / M4 / M5 を計画書へ反映すること。**
