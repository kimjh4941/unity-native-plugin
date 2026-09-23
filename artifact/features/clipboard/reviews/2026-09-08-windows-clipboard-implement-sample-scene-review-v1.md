# レビュー結果 v1（サンプルシーン実装）

- 日付: 2026-09-08
- 対象: コミット `0e47ca5` / ブランチ `feature/UNT-11`
- 計画: `artifact/features/clipboard/designs/2026-09-08-windows-clipboard-sample-scene-design-v3.md`
- 実装結果: `artifact/features/clipboard/results/2026-09-08-windows-clipboard-implement-sample-scene-result-v1.md`
- プラットフォーム: Windows
- レビュアー: Claude サブエージェント 5 名（観点別）

| # | 担当観点 | A | B | C |
|---|---|---|---|---|
| 1 | 計画整合性・API 呼び出し仕様 | 0 | 5 | 7 |
| 2 | ライフサイクル・ガード・ナビゲーション | 1 | 3 | 5 |
| 3 | プラットフォーム独立性・ルール適合・秘匿 | 0 | 2 | 4 |
| 4 | テストが実際に何かを捕まえるか | 0 | 12 | 3 |
| 5 | Manager 契約の使い方 | 5 | 3 | 3 |
| | **重複排除後** | **6** | **22** | **14** |

---

## 0. この文書の読み方

**A 区分 6 件はすべて、私（レビュー依頼者）が実物のコードを引いて裏付けを取った。** 却下した指摘は無い。

**A 6 件のうち 4 件は同じ失敗形である: 実装は契約どおりに動いているのに、サンプルが嘘の観測結果を出す。**
計画 v1 → v2 で直したのと同じ種類の欠陥が、今度は実装側に現れた。

> サンプルシーンは実装の観測装置である。**観測対象の構造を誤ると、装置が嘘の観測結果を出す。**（計画 1.1）

---

## 強み（実物と突き合わせて確認できたもの）

- **ボタンの結線は機械的に閉じている。** UXML の `<ui:Button>` 66 個と `Bindings` 66 件を名前集合で双方向に差分してゼロ。`Bindings` が唯一の名前表で、`Bind` と `OnDestroy` が同じ `_boundButtons` を経由するため登録と解除がずれない
- **共通イベント 12 種の購読/解除が完全対称**（`Controller:250-261` / `:277-288` を 1 対 1 で照合）
- **未消化件数の収支は、A-5 の例外経路を除く全経路で成立している。** 拒否（`requestId == 0`）も `StartRequest` → `QueueDelivery` → `dispatcher.Enqueue` で次フレーム配送になるため、`Accept` を呼び出しの後に置いても +1 → -1 の順は崩れない
- **`Shutdown While Disabled` は行き止まりにならない。** 配送は Manager の `Update` ではなく別 GameObject の dispatcher が担う
- **秘匿方針が破れていない。** `Debug.Log` 82 箇所と `ErrorMessage` の生成元まで追跡済み。`ToMessage` の `detail` が履歴 JSON を受け取る経路（`WindowsClipboardManager.cs:2543`）は存在するが、`detail` を使うのは `InvalidArgument`(1004) のときだけで、native が返すコードは 0〜19 のため到達しない
- **P1 / 配置 / P2 / P4 すべて適合。** 変更ファイルは Navigator と TopMenu の 2 本のみ。他プラットフォームは 1 バイトも触れていない
- **`Encoding.Unicode.GetBytes(body + "\0")` は CF_UNICODETEXT の正しいバイト列**（BOM なし UTF-16LE + 2 バイト終端）
- **結線検査 `EveryButtonIsBoundToItsOwnHandler` は、このセットで最も強い。** ハンドラの繋ぎ替えは名前検査を全部緑にしたまま、ここだけで確実に落ちる

---

## A 区分（画面の振る舞いを変える / 嘘の観測結果を出す）

### A-1: `TryShutdown` が「完了していないが今回はエラー無し」を返したとき、State を `ShutDown` と表示する

- 該当: `WindowsClipboardSampleResult.cs:244-246`、呼び出し側 `Controller.cs:576-583`
- 根拠（実物で確認済み）:
  - `ClassifyShutdown(completed: false, code: None)` → **`NotYet`**（`WindowsClipboardManager.cs:2828-2829`）
  - `FinishShutdownAttempt` の `NotYet` → **`s_state = Draining`**（同 `2797-2799`）
  - `TryShutdown` の戻り値は `FromNative` 由来で、`pError == 0` なら **`Success`**（`WindowsClipboardResult.cs:50-51`）
  - つまり **`IsSuccess == true` かつ `completed == false` が正常系**であり、`None → NotYet` の分岐はそのために存在する
- 何が嘘になるか: `Advance` は `operation == OperationShutdown && isSuccess` を無条件に `ShutDown` にする。
  画面には `#n [call] uninitClipboardManager OK code=None completed=False` と `State: ShutDown` が並ぶ。
  **実機の Manager は `Draining`。** 直後に `Copy After Shutdown` を押すと `ShuttingDown` が返り、
  「State は ShutDown なのに ShuttingDown」という契約違反にしか見えない観測結果になる。
  **Editor では `completed = true` 固定（`2730-2731`）なので、この嘘は実機でしか出ない。**
- 提案: `Advance` に `completed` を渡し、`isSuccess && !completed` は `Draining` にする

### A-2: `ShutdownTimeout` 以外の terminal 失敗を `Draining` と表示し、実際のドレイン中には `Draining` を表示しない

- 該当: `WindowsClipboardSampleResult.cs:246-259`
- 根拠（実物で確認済み）:
  - `ClassifyShutdown` は `None / Busy / MonitorRegisterFailed / Canceled / PartialState` **以外をすべて `Terminal`** に落とす（`WindowsClipboardManager.cs:2827-2835`）
  - `FinishShutdownAttempt` の `default:` が **`s_state = ShutdownFailed` を latch** する（同 `2800-2805`）
  - `InvokeNativeShutdown` は `BridgeUnavailable` / `Unknown` も返す（同 `2710-2714`）
- 何が嘘になるか: これらで `Advance` は `code != ShutdownTimeout` なので **`Draining`** を返す。
  `Draining` の定義コメントは「a shutdown is still making progress」だが、実体は**二度と回復しない `ShutdownFailed`**。
  **この enum は `ShutdownFailed` を `ShuttingDown`(1011) から区別するために存在する**と自分で書いているのに、その区別を取り違える。
  さらに `ShutdownWithDrain` は最終結果を 1 回しか配送しないので、**実際にドレインが走っている複数フレームの間 State は `Running` のまま**。
  `Draining` 表示が出るのは「実際には Draining ではない時」だけ、という反転が起きている。
- 提案: 失敗側を「`ShuttingDown` 系のみ `Draining`、それ以外の失敗は `ShutdownFailed`」に反転。
  あわせて `AcceptAwaited(call, OperationShutdown)` の時点で `_state = Draining` を立てる

### A-3: 往復判定のアンカーを、成功を確かめずに更新している箇所が 4 つある

- 該当: `Controller.cs:755-756` / `:770-771` / `:788` / `:1021-1022`
- 根拠: 他の copy はすべて成功をゲートしている —
  `if (result.IsSuccess) _lastTextHash = ...`（`:663`）、`if (result.IsSuccess) _lastImageHash = ...`（`:726`）、
  `if (result.IsSuccess) _lastCustomHash = ...`（`:738`）、`if (copied.IsSuccess) { ... }`（`:887-890`）。
  **上記 4 箇所だけが呼び出し前に、戻り値を見ずに書き換えている。**
  `CopyMultipleFormats` / `ReserveDeferredFormats` は `CanRunOperation` で拒否されうる
- 何が嘘になるか: 「アンカーはこの画面が最後に**書いた**もの」という定義（`Controller.cs:92-93`）が破れる。
  Copy Plain Text 成功（アンカー = A）→ ドレイン中に Copy Multiple Formats 失敗（アンカー = B に上書き）→
  Paste Plain Text で A が返る、で **`match=differ`**。往復は壊れていないのに「往復が内容を失った」と表示する。
  これは `MatchLabel` の doc comment が名指ししている失敗そのもの
- 提案: 4 箇所とも戻り値を受けてから `if (result.IsSuccess)` で更新する

### A-4: 「`TryShutdown` はイベントも callback も出さない」は成立しない

- 該当: `Controller.cs:572-575` の remark、および画面の About ラベル `WindowsClipboardManagerExample.uxml:20`
- 根拠（実物で確認済み）: `TryShutdown` → `RunShutdownAttempt` → `FinishShutdownAttempt` で
  **無条件に `DrainRequestRegistry()` が走る**（`WindowsClipboardManager.cs:2771-2774`）。コメントも
  「**Unconditional.** A request rejected while the manager was never initialized still holds a queued delivery」と書いている。
  `DrainRequestRegistry` は **dispatcher を意図的にバイパスして同期で配送**し（同 `3067-3083`）、
  `DeliverClaimed` → `InvokeInOrder` が共通イベントと per-call callback の両方を発火する
- 何が嘘になるか: Manager 側の限定は「**TryShutdown 自身の shutdown 結果**についてイベントも callback も出さない」（同 `881-884`）。
  サンプルはこれを「TryShutdown ではカウンタが一切動かないのが正しい」に一般化した。
  **Get History を出した直後に Try Shutdown を押すと Pending と Events は必ず動く。**
  About は操作者向けの契約リファレンスなので、**正しい挙動を欠陥として報告させる**
- 提案: About と remark を「TryShutdown 自身の結果は乗らない。ただし teardown ドレインが走るため、
  保留中の非同期要求はこの行の直後に `Canceled` 等で**同期配送**され、Pending と Events はその分だけ動く」に直す

### A-5: ワーカースレッド呼び出しに失敗経路がなく、例外が出ると Pending が無説明で残る

- 該当: `Controller.cs:1353-1359` / `:1373-1376`
- 根拠: `AcceptAwaited` で `_pending++` した後、`Task.Run(() => manager.CopyPlainText(...))` の本体で例外が出ると、
  **誰も await していない Task なので例外は握り潰され、`Done` は永久に呼ばれない**。
  例外が出うる理由は `CanRunOperation` の並びで、`Application.platform` の読み取りが `IsMainThread()` **より前**に来る
  （`WindowsClipboardManager.cs:1074-1078`）。**`Application.platform` がワーカースレッドから安全かは要確認**
  （PlayMode の該当テストは `SetMainThreadIdForTests` でスレッド ID を偽装しているだけで、実際にはメインスレッドで走っている）
- 何が嘘になるか: 画面には `[accept]` 行だけが残り `Pending: 1` が延々と表示される。
  **これは exactly-once を判定するために置いている唯一の指標**であり、操作者は契約違反として記録する。
  実際には計画 8.7 が観測したかった検証がそもそも実行されていない
- 提案: `Task.Run` の本体を try/catch で包み、catch では dispatcher 経由で `Local` 相当の行を出して `_pending--` する

### A-6: `manager.enabled` の復帰が例外安全でない位置にある

- 該当: `Controller.cs:600-614`
- 根拠: 復帰 `manager.enabled = true` が `Done(...)` の**後ろ**にある。
  `Done` が投げると Manager 側の `SettleDrain` が例外を握り潰し（`WindowsClipboardManager.cs:3040-3048`）、
  `LogError` 1 行だけ残して `enabled` は false のまま
- 何が起きるか: この Manager は `Instance` ゲッタで **`DontDestroyOnLoad` 付きで生成される**（同 `269-282`）。
  一度外れると**画面を離れても TopMenu に戻ってもセッション全体で無効のまま**残り、
  以後の `ShutdownWithDrain` は毎回強制 1 回試行パスに落ちる。自己修復する経路は無い。
  **`Done` が実際に投げる具体経路は特定できていない（要確認）が、代償に対して `try/finally` は安すぎる**
- 提案: `try/finally` で囲む。あわせて `OnDisable` に保険を入れる

---

## B 区分（検証手段を変える）

### B-a. 判定条件が誤っている / 足りない（実機で誤った結論を出す）

| # | 内容 |
|---|---|
| B-1 | **provider カウンタが 2 形式の合算。** `CF_UNICODETEXT` と `CF_DIB` が同じ `s_renderCount` を加算する（`Controller.cs:1024-1028`）。計画 8.3 の「M-18 は provider が **1 回**呼ばれ」で読むと、貼り付け先が両形式を要求した正常動作を「2 回＝不具合」と記録する。**形式別カウンタにする** |
| B-2 | **`s_renderCount` が static で再入場時に 0 に戻らない。** `_shownRenderCount` はインスタンス変数なので 0 から始まり、再訪直後に「今回 provider が呼ばれた」ように見える値が出る。Deferred は数字 1 個で判定する項目 |
| B-3 | **履歴に一致判定が無い。** 計画 4.4 は「履歴の内容確認（M-11）| 同じ一致判定 + `ToUtcTime()` の相対秒数」を要求。`DescribeHistory` は `empty` / `count` / `newestAgeSec` のみ。**他アプリのコピーが割り込んでも「数秒前」は満たすので誤って合格と読める** |
| B-4 | **結果行が `Debug.Log` に出ない。** `Begin` だけがログを出し、`Call` / `Done` / `LogEvent` は出さない。macOS 版は三方とも出している。**Player.log を回収しても accept / done / event が残らず**、結果表示欄は高さ 64px 固定なのでスクリーンショットでも数行しか残らない。計画 4.3 の 3 契約が画面をその場でスクロールする以外に確認できない |
| B-5 | **`Restore Twice In One Frame` に前提ガードが無い。** `Get History` の前に押すと id が空で `InvalidArgument` が 2 行出るだけで、**M-15 が見たい `OperationBusy` に到達しない**。`Copy Files` には `noTempFiles` ガードがあるため、操作者は「ガードが無い＝前提不要」と読む |
| B-6 | **読み出し失敗時にも `match=differ` を出す。** `DescribeText` / `DescribeBytes` は `IsSuccess` を見ない。失敗時 `Text` は null なのでハッシュ 0 になり、アンカーが立っていれば必ず `differ`。`MatchLabel` が「アンカー無しは比較していないので `n/a`」としているのと同じ理屈が、失敗した読み出しにも当てはまる |
| B-7 | **`Cancel Last` の対象 id を取り違える。** `Accept` は `requestId == 0` でも `_lastRequestId` を上書きし（`Controller.cs:408-414`）、`AcceptAwaited` は更新しない（`:423-429`）。`Restore Twice` の直後は 0、Await 版の直後ははるか前の callback 版の id を撃つ |
| B-8 | **`Clear` が `_lastFilePaths` を消さない。** Clear 後の `Paste Files` は `differ`、`Paste Image` は `n/a`。同じ「Clear 直後」で判定列の意味が形式ごとに変わる |
| B-9 | **M-20a の判定根拠が画面に無い。** 試行回数は `AdvanceDrain` の `Debug.LogError` にしか出ない。計画 7.6 は「見るのは終端の種類ではなく試行回数」と明記しているのに、画面からは「成功した」以上のことが読めない |
| B-10 | **S-7 の「11. Threading は `MainThreadRequired`」が `Delayed History Call` に当てはまらない。** これはコルーチン＝メインスレッドなので `PlatformUnavailable` 側。期待値がセクション単位で書かれているため、正しい結果を不一致として記録し得る |
| B-11 | **M-3 は正常時にも `match=differ` を出す。** アンカーを 0 に戻すのは `Clear` 系のみ。手順に「M-3 の前に Clear を押す」を明記する（実装変更は不要） |
| B-12 | **Editor から本画面に到達できない。** TopMenu の Editor 分岐は `DisplayDialog` のみ。**実装結果 65 行「Editor でも Manager を呼び `PlatformUnavailable` が観測できる」は、同じ文書の 268 行（V-3）と矛盾している。** 内側ガードを置かない判断自体は Player の挙動を変えないので妥当だが、その根拠として書いた文が成立していない |

### B-b. テストが名前ほど強くない

**コミットメッセージで「コメントではなくテストで固定した」と前面に出した 3 つが、最も緩い。**

| # | テスト | どう壊すと落ちないか |
|---|---|---|
| B-13 | `TheDeferredProvidersTouchNoUnityApi` | (1) ラムダを別メソッドに切り出す (2) テーブルの前で `Func<byte[]>` を作る (3) ラムダ内に `new byte[] { 0 };` を置くと `};` が立って範囲が切れる。加えて **禁止語に `RefreshState` が無い**。`RefreshState()` は `_stateLabel.text` を書き `Instance.enabled` を読む、まさに 8.3 が禁じている操作 |
| B-14 | `EveryAwaitedHandlerChecksForCancellation` | 数の一致だけで地点と結びついていない。(1) `manager` ローカル経由で 7 本目を書けば `awaits` は 6 のまま (2) 1 本足して冗長なガードを 1 行足せば 7==7 (3) **`OnRestoreAwaitClicked` を同期に書き換えると 5==5 で緑**。計画 9.3 が「押さなければ永久に一度も実行されない」と名指しした 6 ボタンが静かに消せる |
| B-15 | `NoAwaitSiteCatchesOperationCanceledException` | `catch(...)`（空白なし）/ `System.` 修飾 / `TaskCanceledException` / `when` フィルタ / 素の `catch`。**このファイルには現在 `catch` が 1 つも無いので、`AreEqual(0, Count("catch"))` に変えるだけで全部塞がる** |
| B-16 | `TheNavigatorRemovesThisControllerBeforeSwitchingScreens` | **構造上落ちない死んだテスト。** `StringAssert.Contains("ShowWindowsClipboard", source)` はメソッド定義そのものにマッチする。`RemoveIfExists` の行を `RemoveExistingControllers` の外に出しても緑 |

**未検証の領域 4 つ:**

| # | 内容 |
|---|---|
| B-17 | **DIB の画素が無検査。** `Fixtures.cs:120-126` の画素ループを丸ごと削除しても 12 件全部緑（配列は確保済みで全長 296 は不変、FNV もヘッダで非ゼロになる）。全面ゼロの画像が貼られ、Paint に貼って初めて気づく。BGRA の並び替えも検出できない |
| B-18 | **購読対称性が無検査。** `manager.ClipboardChanged -= ...` を 1 行消しても 43 件全部緑。再入場でそのイベントだけカウンタが 2 倍になる。**S-5 / S-6 が肉眼で見ようとしている事象そのもので、層 1 に前倒しできる** |
| B-19 | **シーケンス単調性と pending 収支が無検査。** 計画 7.7 が明示的に挙げた項目 |
| B-20 | **ナビゲータの Resources パスが二重管理。** テストは自前定数を `Resources.Load` するだけで、Navigator が実際に渡す文字列は無検査。`Navigator.cs:130` を typo しても 14 件全部緑で、実機では画面が開かない。**USS 欠落（計画レビュー A1-3）を捕まえるために存在を検査したのと同じ失敗形** |

| # | 内容 |
|---|---|
| B-21 | `Advance` の未テスト分岐。**`ShutDown` を保持する三項（`:257-259`）を `return Draining;` に単純化しても 17 件中 1 件も落ちない。** 唯一「わざわざ書かれた特例」にテストが無い |
| B-22 | `FormatStatus` テストが `Events:` を検証していない。`| Events: {events} ` を削除しても緑 |

---

## C 区分（記述だけ）

| # | 内容 |
|---|---|
| C-1 | 実装結果 1.1 の `WindowsClipboardSampleResultTests.cs` を 244 行としているが実物は 251 行 |
| C-2 | 計画 5.1 の見出し「新規作成（7）」に対し表は 8 行（v3 で 1 本追加した際の直し漏れ） |
| C-3 | 計画 8.1「12. Errors = 8.5 の 9 種」と実装の 8 ボタン（I-1 の判断は妥当。計画側が二重計上のまま） |
| C-4 | 計画 4.3 の `StatusTextBlock` =「直近 1 操作の要約」と実装のカウンタ行の差が、実装結果 3 節に記録されていない |
| C-5 | ワーカー版 `Copy` だけが同期 API を `[accept]` / `[done]` で出す判断が未記録 |
| C-6 | フィクスチャ連番が `[call]` の seq と 1 ずれる箇所が 3 つ（`:1016` / `:1349` / `:1494`） |
| C-7 | `ShutdownFailed` というエラーコードは存在しない（`Controller.cs:1480-1484` の remark。近いのは `ShutdownTimeout`。計画 8.5 から持ち込まれた誤り） |
| C-8 | provider が走るスレッドの説明が Manager の契約と食い違う。契約は「**Unity メインスレッド上で**、Win32 メッセージの中で同期的に走る」（`WindowsClipboardManager.cs:1953-1958`）。禁止理由はスレッド親和性ではなく、シャットダウン進行中に走ること |
| C-9 | `Clear` の per-call callback 行が accept を伴わない `[done]` になる（`_pending` に載せていないため） |
| C-10 | テスト 3 本のガードが `#if UNITY_EDITOR` で、対象型の `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` と揃っていない（asmdef が Editor 限定なので実害なし。実装結果 43 行の記述が実態と違う） |
| C-11 | `RequiredLabelNames` の `AwaitCancelLabel` は Controller が参照しない（コメントの「none of which the controller can do without」と食い違う） |
| C-12 | `TheDibRunsBottomUp` は `TheDibHeaderCarriesWhatTheNativeValidatorChecks` に完全に包含される |
| C-13 | `AByteDescriptionReportsSizeAndMatchOnly` が "Only"（非開示）を検証していない |
| C-14 | 計画側の項番ゆれ（9.1 の見出し「S-1 〜 S-8」に対し表は S-9 まで、7.3 の「S-6」は実体 S-7、3.2 の「8.1 の 11 節」は 12 節） |

---

## 適合チェック

| 項目 | 判定 |
|---|---|
| ボタン一覧の実装網羅性 | ○ |
| UXML name と Controller の一致 | ○ |
| API 呼び出し仕様の一致 | ○ |
| 変更ファイル一覧との一致 | △（C-1） |
| common.md 準拠 | ○（テストのガードのみ C-10） |
| csharp.md 準拠 | ○ |
| P1 / 配置 / P2 / P4 | ○ |
| 内容秘匿 | ○ |
| ライフサイクル管理（登録・解除の対称性） | △（A-6 / B-18） |
| コンパイルガードの網羅性 | ○ |
| ナビゲーション統合 | △（B-12） |
| 既存 API 互換性 | ○ |

---

## 総合評価

**要修正（重大）。A 区分 6 件。**

止める基準（A = 0）は満たしていない。

### 構造として見えたこと

**1. A 6 件のうち 4 件が「実装は正しいのにサンプルが嘘をつく」。**
A-1 / A-2 は `WindowsClipboardSampleResult.Advance` の 1 メソッドに集中しており、原因は
「shutdown の成功 = 完了」「失敗のうち `ShutdownTimeout` だけが terminal」という 2 つの前提。
どちらも Manager の `completed` と `ClassifyShutdown` を読めば成立しない。
**計画 v1 が実装の構造を誤って想定していたのと同じ失敗形が、実装側に移っただけである。**

**2. 最も守りたいと宣言した 3 つのテストが、最も緩い。**
コミットメッセージが「コメントではなくテストにした」として挙げた
provider 検査 / await ガード検査 / catch 不在検査が、いずれも回避可能（B-13 〜 B-15）。
**ソース文字列の走査は、書き方を変えれば必ずすり抜ける。** 範囲を構文で取るか、許可リスト方式にする必要がある。

**3. 死んだテストが 1 件（B-16）。** 前回の実装フェーズでミューテーション検証が 5 件見つけたのと同じ種類。
**今回はミューテーション検証を回していない。** 43 件は自分で書いたものなので、同じ盲点を継承している。

### 優先順位

1. **A-1 / A-2**（`Advance` の 2 つの前提）— 1 メソッドで閉じる
2. **A-3 / A-6**（成功ゲートと `try/finally`）— 機械的
3. **A-4**（About の文言）— 文言のみだが、操作者に誤報告させる
4. **A-5**（`Task.Run` の catch）
5. **B-13 〜 B-16**（テストの回避経路）— B-15 は 1 行、B-16 は範囲を取るだけ
6. **B-1 〜 B-5**（判定条件）— 実機に入る前に必須
7. **B-17 / B-18**（画素と購読対称性）— どちらも層 1 に前倒しできる
8. C 区分

### 次

**A 6 件と B の実機必須分を直し、レビュアーを替えて再レビュー。**
止める基準の 3 条件のうち、
「`*SampleSceneWiringTests` が UXML の name を壊すと落ちることを確認済み」は
**B-13 〜 B-16 と B-20 が未達**である。
