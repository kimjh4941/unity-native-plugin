# Windows Clipboard サンプルシーン 実装結果 v3

## 基本情報

- 日付: 2026-09-08
- 機能名: clipboard / 対象プラットフォーム: Windows
- サンプル計画: `artifact/designs/clipboard/2026-09-08-windows-clipboard-sample-scene-design-v3.md`
- 機能実装結果: `artifact/results/clipboard/2026-09-08-windows-clipboard-implementation-feature-result-v6.md`
- ブランチ: `feature/UNT-11`
- レビュー:
  - v1 `artifact/reviews/clipboard/2026-09-08-windows-clipboard-implement-sample-scene-review-v1.md`
    （Claude サブエージェント 5 名。**A 6 / B 22 / C 14**）
  - v2 `artifact/reviews/clipboard/2026-09-08-windows-clipboard-implement-sample-scene-review-v2.md`
    （Codex `gpt-5.5` high。**A 5 / B 6 / C 1**）
- 前版: v1（レビュー前）→ v2（レビュー v1 対応）

### 0.0 レビュー 2 巡の結果

| ラウンド | レビュアー | A | 指摘の質 |
|---|---|---|---|
| v1 | Claude サブエージェント 5 名 | 6 | 状態追跡・アンカーのゲート・テストの回避経路 |
| v2 | Codex `gpt-5.5` high | 5 | 踏まない経路・番号の非単調・アンカーの寿命・画面文言 |

**2 巡ともレビュアーを替えて A が出た。しかも指摘箇所はほとんど重なっていない。**
同じコードを別のモデルが読んで別の穴を出したということであり、
**「1 巡で A が 0 なら止める」は成立しない**ことの実例になっている。

### 0.2 v2 からの変更（レビュー v2 対応）

**A 5 件のうち 4 件は v1 と同じ失敗形** — 実装は契約どおりに動いているのに、サンプルが嘘の観測結果を出す。

| # | 内容 |
|---|---|
| A-1 | `Force Initialize While Draining` を**次フレームに分離**。`StartDrain` はセッションを作るだけで `Draining` は次の `Update` まで立たず、同一フレームの `Initialize` は冪等成功していた。**ガードの確認ボタンがガードに到達していなかった** |
| A-2 | **行番号と呼び出し ID を分離**。全行が `++_resultSequence` を取り、帰属は `call=#N` の別欄へ。従来は `#1 [accept]` → `#2 [event]` → `#1 [done]` と番号が戻り、**計画 4.3 の「単調増加 seq で配送順序を読む」が成立していなかった** |
| A-3 | **`ReplacedClipboard` に一本化**。書き込み成功で全アンカーを落とし、その操作が載せた形式だけ再設定 |
| A-4 | `Clear` は**成功時のみ**アンカーを落とす。失敗時は前の内容が残っているため |
| A-5 | UXML の About 文が v1 A-4 の訂正前のまま残っていた。**操作者が読むのは画面である** |

| # | B の修正 |
|---|---|
| B-1 | 全ログ書き出しメソッドが `++_resultSequence` を取り、`call.Sequence` を行番号に使っていないことを検査 |
| B-3 | `if (Cancelled(...)) return;` を**1 文として**検査し、その後に `Done(call,` があることも要求。従来は `if (Cancelled(...)) { } return;` で通り、**成功時も `Done` せず終了する実装**を許していた |
| B-4 | 各 provider が**自分のカウンタ**を 1 回だけ進めることを検査 |
| B-5 | `CodeOnly` を文字列状態の追跡に変更（**v1 の修正で私が入れた仕掛け自体の穴**） |
| B-6 | `ClipboardChanged` も `LogEvent` 経由にして Player.log に残す |

#### A-3 の連鎖（記録に値する）

`ClassifyFirstRead` は **`FormatUnavailable` を `EmptySuccess` に正規化する**
（`WindowsClipboardManager.cs:1023-1025`）。したがって:

1. `Copy Image` → 画像アンカー設定
2. `Copy Plain Text` → **クリップボード全体が置換される**が、画像アンカーは残る
3. `Paste Image` → 画像は無いので `FormatUnavailable` → **成功・空**として返る
4. アンカーが残っているため `match=differ`

**画像往復が壊れたのではなく、テキストで置き換えただけである。**
v1 の B-6 修正（失敗した読み出しは `n/a`）は、この経路が**失敗ではなく成功**なので救わない。

**根はアンカーの寿命が設計されていなかったこと。** 「いつ設定するか」だけを個別に直してきて、
「いつ落とすか」を一度も決めていなかった。A-3 / A-4 と v1 の A-3 / B-8 は全部これである。

### 0.1 v1 からの変更

**レビュー v1 の A 6 件をすべて修正した。** A 6 件のうち 4 件は同じ失敗形だった —
**実装は契約どおりに動いているのに、サンプルが嘘の観測結果を出す。**

| # | 内容 |
|---|---|
| A-1 | `TryShutdown` が「完了していないがエラー無し」を返したときに `ShutDown` と表示していた。`Advance` に `completed` を渡す |
| A-2 | `ShutdownTimeout` 以外の terminal 失敗を `Draining` と表示していた。`KeepsDraining` を `ClassifyShutdown` の retry 集合と対にした。あわせて**ドレイン中に `Draining` を表示する**ようにした（従来は `Running` のままだった） |
| A-3 | 往復アンカーを成功確認前に更新していた 4 箇所を、戻り値でゲートした |
| A-4 | About の「`TryShutdown` はイベントも callback も出さない」を訂正。**保留中の要求は同期配送されるため Pending と Events は動く** |
| A-5 | `Task.Run` の本体を `RunOnWorker` で包み、例外時に Pending を閉じて理由を出す |
| A-6 | `manager.enabled` の復帰を `try/finally` に入れた |

**B のうち実機判定に効くもの**を修正した。

| # | 内容 |
|---|---|
| B-1 | provider カウンタを**形式別**にした（合算では M-18 の「1 回」が判定できない） |
| B-2 | `s_renderTextCount` / `s_renderImageCount` を `OnEnable` でリセット（再入場時に前回の残骸が出ていた） |
| B-3 | 履歴に**一致判定**を追加。年齢だけでは他アプリの割り込みを区別できない |
| B-4 | `Call` / `Done` / `Accept` / `LogEvent` を `Debug.Log` にも出す。**Player.log から順序を再現できるようにした** |
| B-5 | `Restore Twice In One Frame` に id 前提ガードを追加（無いと `OperationBusy` に到達しない） |
| B-6 | 読み出し失敗時は `match=n/a`（比較していないため） |
| B-7 | `Accept` は `requestId == 0` を `_lastRequestId` に記録しない |
| B-8 | `Clear` が `_lastFilePaths` も含めて全アンカーを落とす（`DropRoundTripAnchors`） |

**テストの回避経路**を塞いだ（B-13 〜 B-20）。詳細は 4 節。

## 0. 状態サマリー

| 区分 | 状態 |
|---|---|
| 層 1 EditMode | **805 / 805 passed**（本実装で **+56**） |
| 層 2a PlayMode | **181 / 181 passed** |
| **層 0 Win64 Player ビルド** | **成功** |
| 実機動作確認 | **未実施。** 本工程はサンプルを作るところまで |

一括実行: `scripts/verify_unity_windows.sh` → `all gates passed`

**このサンプルはデモではなく検証装置である。** 公開 API 33 種のうちネイティブに対して
実行されたことがあるのは 5 種だけで、残り 28 種はコードレビューでも Editor テストでも
到達できない。**このサンプルのボタンを押すことが唯一の実行手段**になる。

---

## 1. 変更ファイル

### 1.1 新規作成（8）

| ファイル | 行 | 内容 |
|---|---|---|
| `Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExample.uxml` | 157 | 画面定義。66 ボタン |
| `Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExampleStyle.uss` | 177 | macOS 版から複製し、`windows-` 接頭辞に変更。状態行と入力欄のスタイルを追加 |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardManagerExampleController.cs` | 1519 | 操作と結果表示 |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardSampleResult.cs` | 312 | 結果整形・状態追跡 |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardSampleFixtures.cs` | 223 | フィクスチャ生成と後始末 |
| `Tests/Runtime/WindowsClipboardSampleSceneWiringTests.cs` | 結線検証（**16 件**） |
| `Tests/Runtime/WindowsClipboardSampleResultTests.cs` | 整形・状態遷移（**23 件**） |
| `Tests/Runtime/WindowsClipboardSampleFixtureTests.cs` | フィクスチャの形（**14 件**） |

すべて `Windows` 接頭辞。クラスガードは `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`（P1 / P5 適合）。

### 1.2 既存変更（2）

| ファイル | 変更 |
|---|---|
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowWindowsClipboard` を追加。`RemoveExistingControllers` の Windows ブロックに `RemoveIfExists<WindowsClipboardManagerExampleController>` を追加 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | 購読ガードに `UNITY_STANDALONE_WIN` を追加、ログ文言 2 箇所、`OnClipboardClicked` に `#elif UNITY_STANDALONE_WIN` 分岐を追加 |

### 1.3 非変更

- 他プラットフォームの `Android*` / `Ios*` / `Mac*` — **0 件**（P2 適合）
- `Runtime/Clipboard/` の実装本体 — **サンプルのために製品コードへ試験用の細工を入れていない**
- `Runtime/Common/` — 0 件（P4 適合）
- `ProjectSettings/` — 0 件。`runInBackground` はコントローラの `OnEnable` で設定し `OnDisable` で戻す（計画 7.9）

---

## 2. 計画どおりに実装したもの

| 計画 | 実装 |
|---|---|
| 内側コンパイルガードを置かない（7.3） | 置いていない。**ただし計画 7.3 の根拠「Editor でも `PlatformUnavailable` が観測できる」は成立しない。** TopMenu の Editor 分岐は `DisplayDialog` のみで本画面へ到達しないため（8 節 V-3）。ガードを置かない判断自体は Player の挙動を変えないので維持する |
| シーケンス番号つきの時系列ログ（4.3） | `#seq [kind] operation ...`。kind は `call` / `accept` / `done` / `event` / `local` |
| 操作名は `result.Operation` から出す（S-2） | `Call` / `Done` は結果の `Operation` を出力。ボタンのラベルは使わない |
| クリップボード内容を一切出さない（4.4） | 長さ・サイズ・件数・一致 bool のみ。`ErrorMessage` は出す（固定リテラルのため） |
| provider は Unity API を呼ばない（8.3） | `Interlocked.Increment` と値キャプチャした `byte[]` のみ。表示は `Update`。**形式別カウンタ**（B-1） |
| DIB は 8x8 / 32bpp / `BI_RGB` / 296 バイト（8.2） | `BuildDib()`。機械検査つき |
| パスは `Path.GetFullPath` で正規化（8.2） | `CreateTempFiles()`。機械検査つき |
| Await の cancel は例外を投げない（7.4.1） | 全 6 await 地点が `Canceled` で早期 return。機械検査つき |
| `StateTextBlock` で lifecycle を追跡（4.1） | `WindowsClipboardSampleResult.Advance` の純粋関数。単体テストつき |
| UI を無効化しない（7.8） | 全ボタン常時有効。`OperationBusy` と `Copy After Shutdown` を観測可能に保つ |
| `runInBackground` を有効化（7.9） | `OnEnable` で `true`、`OnDisable` で元の値へ |
| 未消化件数を常時表示（4.3） | `Pending:` として状態行に表示 |

---

## 3. 実装時に決めたこと（計画に無かった判断）

**計画由来と区別するため独立させる。**

| # | 判断 | 理由 |
|---|---|---|
| I-1 | `Paste Plain Text (after Clear)` を **Paste セクションに置き、Errors セクションには置かない** | 計画 8.5 は Errors を 9 種としつつ、同じ表で「**失敗ではない**」と書いている。両方に置くと同一操作のボタンが 2 つになる。Errors は 8 ボタン、9 種目は Paste 側にあると Errors セクションの説明文に明記した |
| I-2 | ACP は `CultureInfo.CurrentCulture.TextInfo.ANSICodePage` で表示 | 計画 8.2.1 は「`GetACP()` 相当」を要求。`GetACP` は P/Invoke になり、サンプルのためだけにネイティブ境界を増やすことになる。**両者は通常一致するが同一ではない**（ネイティブは*システム* ACP を使う）。**要検証**（V-6） |
| I-3 | M-8 のフィクスチャに**ハングル + 非 BMP 文字（絵文字）**を使用 | 計画は「現在の ACP で表現できない文字」を要求。ハングルは 932 / 1252 の外だが 949 では表現できる。非 BMP 文字は**どの ANSI コードページでも表現できない**ため、機種に依存しない。両方入れた |
| I-4 | `Begin` は開始行を出さない | macOS 版は `FormatRunning` で 1 行出すが、本画面は 1 操作で複数行（accept / event / done）出るためログが倍になる。**開始 seq は結果行が持つ**ので順序は読める |
| I-5 | 遅延実行はコルーチン | ワーカースレッドのタイマーだと `MainThreadRequired` になり `NotForeground` に到達しない。フレームで待つ必要があるため `WaitForSeconds` |
| I-6 | メインスレッド id は `Awake` で捕捉 | 「今この場で聞いたスレッド」ではどこからでも true になる。ワーカー観測が成立しない |
| I-7 | `Clear` の per-call callback 行は `[event]` として出す | `Accept` していないため `Done` を通すと未消化件数が負に振れる。**v2 で `[done]` から `[event]` に変更**（C-9）。accept を伴わない done は、Pending の読み方に対する唯一の例外になってしまう |
| I-8 | `StatusTextBlock` はカウンタ行にした。計画 4.3 の表は「直近 1 操作の要約」 | 同じ節の本文が「未消化件数を画面に**常時**表示する」を要求しており、両立しない。直近 1 操作は `ResultTextBlock` の最終行が持つ |
| I-9 | ワーカー版 `Copy` だけ同期 API を `[accept]` / `[done]` で出す | 戻り値がワーカー側にしか無いため。`[call]` 行は作れない |
| I-10 | 履歴 item id の入力欄を Fixtures ではなく History セクションに置いた | `Get History` が値を埋めるため、操作の近くにある方が自然 |
| I-11 | テストのソース走査は**コメントを除去してから**行う（`CodeOnly`） | 「`OperationCanceledException` を書かない」規則が、なぜ書かないかを説明したコメント自身を落とした。**規則の説明を規則の隣に置けないなら、その検査は割に合わない** |

---

## 4. 自動テスト（53 件）

計画 7.7 は 3 本を要求。すべて実装した。

### 4.1 `WindowsClipboardSampleSceneWiringTests`（16 件）

| 検証 | 何を捕まえるか |
|---|---|
| UXML / **USS** の Resources パス存在 | USS が無いと `ApplyScreen` が `styleSheets.Clear()` を行わず、TopMenu のスタイルを被る。**レビュー v1 A1-3 の再発防止** |
| 全ボタン名が UXML にある | 結線ミスは実行時に 1 行ログを出して黙って続く |
| **UXML に未結線のボタンが無い** | 押せて反応しないボタンは、カバレッジ照合で「操作済み」と記録される |
| ボタン総数 = 66 | 画面と計画の乖離 |
| 1 ボタン 1 ハンドラ（命名規約照合） | `DeleteLastButton` を `OnRestoreLastClicked` に繋いでも名前検査は全部通る |
| **`catch` がちょうど 1 つ**（ワーカーガードのみ）。コメント除去後に走査 | v1 は 1 リテラルの不在だけを見ており、`catch(` / `System.` 修飾 / `TaskCanceledException` / フィルタ / 素の `catch` ですり抜けた |
| **ハンドラごとに、await の後ろに `Cancelled` ガードと `return`**。件数は 6 で固定 | v1 は 2 つの総数の一致だけで、地点と結びついていなかった。ローカル変数経由の await、ガードの水増し、**ハンドラの削除**（5==5）で全部すり抜けた |
| **provider 内に Unity API が無い**。範囲は**波括弧の対応**で取り、両方がラムダであることを要求 | v1 は最初の `};` までを見ており、入れ子の初期化子で範囲が切れた。メソッドグループ化・事前構築でも本体が範囲外に出た。禁止語に `RefreshState` が無かった |
| Navigator の `RemoveIfExists` が **`RemoveExistingControllers` の本文内にある** | v1 はファイル全体の文字列検査で、**`ShowWindowsClipboard` の検査はメソッド定義自体にマッチするため構造上落ちなかった** |
| TopMenu の `#elif UNITY_STANDALONE_WIN` 分岐と購読ガード | 無いとボタンは押せるが何も起きない |
| **Navigator が読む Resources パスが、このテスト群が検証しているものと同一** | パスの typo は他のどのテストも見ない（各自が自分のコピーを読む）。**USS 欠落と同じ穴** |
| **`OnEnable` の `+=` と `OnDisable` の `-=` が同じイベント集合** | `-=` を 1 行消しても 43 件全部緑だった。総数は Manager の公開イベント数と照合 |

### 4.2 `WindowsClipboardSampleResultTests`（23 件）

整形（内容を出さない・矛盾した結果をそのまま出す・アンカー無しは `n/a`・**失敗した読み出しも `n/a`**）と、
`Advance` の状態遷移。**v2 で追加**: 成功かつ未完了は `Draining`、`ClassifyShutdown` の retry 集合 5 種と
terminal 5 種の総当たり、試行前の拒否は状態を動かさない、失敗 Initialize は現状維持（`Running` 起点で固定）。

### 4.3 `WindowsClipboardSampleFixtureTests`（14 件）

DIB の全ヘッダフィールド、bottom-up、`BITMAPFILEHEADER` を付けないこと、
ANSI フィクスチャが非 BMP とハングルを含むこと、パスが絶対かつ `/` を含まないこと、
2 回目の削除が 0 件で成功すること、フィクスチャのダイジェストが 0 にならないこと。

**v2 で追加**: **画素 64 個の BGRA 値**（画素ループごと削除しても 12 件全部緑だった。
296 バイトの正しく見えるビットマップが空白として貼られ、Paint に貼るまで気づかない）と、
`HashOf(null) == 0` が「アンカー無し」番兵と同値であること。

---

## 5. 実装中に見つけたこと

### 5.1 テストの誤り 1 件（自分の書いたテスト）

`AnEmptyReadIsDescribedAsEmptyRatherThanAsAFailure` が失敗した。原因は実装ではなくテストで、
`WindowsClipboardTextResult.Success(op, "")` を「空の読み出し」と思い込んでいた。実物には
**専用の `Empty(operation)` ファクトリ**があり、`Success` は `IsEmpty == false` を返す。

これは設計 8.4 の「**戻り値 0 は空を意味しない**」そのものだったので、
テストを **2 つの成功を区別する**形に書き直した（`AnEmptyClipboardIsNotTheSameAsAnEmptyString`）。

| 経路 | `IsEmpty` | `length` |
|---|---|---|
| `Empty(op)` — クリップボードが空 | `True` | `-1`（Text は null） |
| `Success(op, "")` — 空文字を読んだ | `False` | `0` |

長さだけを出していたら両者は同じに見えていた。

### 5.2 コンパイルエラー 1 件

`using System;` を置いたテストファイルで `Object` が `UnityEngine.Object` と `object` の間で
曖昧になった（CS0104）。`UnityEngine.Object.DestroyImmediate` に明示。
**過去にも同じ組み合わせで踏んでいる。**

### 5.3 検証手順の運用ミス（記録）

`verify_unity_windows.sh` を前回の実行が終わる前に起動し、Unity のプロジェクトロックにより
2 回連続で「EditMode: NO RESULT FILE (Unity exited 1)」になった。
**ログにはコンパイルエラーが 1 件も無く、`Successfully changed project path` の直後に終了する。**
コンパイル失敗と見分けが付きにくい。ログサイズが 1170 バイト固定なのが目印。

---

## 6. ボタン単位のカバレッジ照合表（計画 9.3 / N-3）

**ネイティブ側は 55 シナリオを全消化したあとで 49 ボタン中 9 ボタンが未操作だったことに気づいた。**
シナリオ一覧はボタン一覧ではないため、シナリオに登場しない操作は誰も押さない。
**実機確認では必ずこの表で照合すること。** 空欄は「M 項目に現れない＝カバレッジ工程で押す」を意味する。

| # | セクション | ボタン | 担当 |
|---|---|---|---|
| 1 | Header | Back To Home | S-5 / S-6 |
| 2 | Lifecycle | Initialize | M-1 / M-23 |
| 3 | Lifecycle | Try Shutdown | About 3（発火しない契約） |
| 4 | Lifecycle | Shutdown With Drain | — |
| 5 | Lifecycle | Can Shutdown Now | — |
| 6 | Lifecycle | Shutdown While Disabled | **M-20a** |
| 7 | Lifecycle | Force Initialize While Draining | — |
| 8 | Lifecycle | Quit | **M-19** |
| 9 | Copy | Copy Plain Text | M-2 |
| 10 | Copy | Copy Plain Text (empty) | — |
| 11 | Copy | Copy Large Text | M-21 |
| 12 | Copy | Copy Html | M-4 |
| 13 | Copy | Copy Files | M-5 |
| 14 | Copy | Copy Image | M-6 |
| 15 | Copy | Copy Custom Format | — |
| 16 | Copy | Copy Multiple Formats | M-7 |
| 17 | Copy | Copy Multiple Formats (with image) | — |
| 18 | Copy | Copy Multiple Formats (CF_TEXT + non-ASCII) | **M-8**（ACP 前提つき） |
| 19 | Copy | Copy Multiple Formats (duplicate format) | — |
| 20 | Options | Copy (Sensitive) | M-9 |
| 21 | Options | Copy (Exclude History) | — |
| 22 | Options | Copy (Exclude Roaming) | **未実施**（R-5。押して `None` までは見る） |
| 23 | Paste | Paste Plain Text | M-3 / M-21 |
| 24 | Paste | Paste Plain Text (after Clear) | 8.5 の 9 種目 |
| 25 | Paste | Paste Html | — |
| 26 | Paste | Paste Html (text only) | — |
| 27 | Paste | Paste Files | M-5 |
| 28 | Paste | Paste Image | M-6 / M-21 |
| 29 | Paste | Paste Custom Format | — |
| 30 | Inspect | Has Format (CF_UNICODETEXT) | — |
| 31 | Inspect | Get Formats | — |
| 32 | Inspect | Get Preferred Format | **8.8 の期待値 4 件** |
| 33 | Inspect | Clear | M-17 / 共通イベント→callback の順序 |
| 34 | Deferred | Reserve Deferred Formats | M-17 / M-18 / M-19 |
| 35 | Deferred | Recover Deferred State | — |
| 36 | History | Get History Availability | M-12 |
| 37 | History | Get History | M-11 / M-12 |
| 38 | History | Restore Last | M-14 / M-24 |
| 39 | History | Delete Last | M-14 |
| 40 | History | Clear Unpinned | M-14 |
| 41 | History | Cancel Last | — |
| 42 | History | Restore Twice In One Frame | M-15 |
| 43 | History | Issue And Cancel In One Frame | M-16 |
| 44 | History | Request + Immediate Shutdown | — |
| 45 | History (Await) | Get History (Await) | — |
| 46 | History (Await) | Get History (Await + Cancel) | **M-16 / S-9** |
| 47 | History (Await) | Get Availability (Await) | — |
| 48 | History (Await) | Restore (Await) | — |
| 49 | History (Await) | Delete (Await) | — |
| 50 | History (Await) | Clear Unpinned (Await) | — |
| 51 | Events | Enable History Events | M-10 |
| 52 | Events | Disable History Events | ブロック C の前提 |
| 53 | Events | Reset Event Counters | — |
| 54 | Threading | Copy From Worker Thread | S-7 |
| 55 | Threading | Get History From Worker Thread | S-7 |
| 56 | Threading | Delayed History Call (5s) | **M-13** |
| 57 | Errors | Copy Plain Text (null) | 8.5 |
| 58 | Errors | Copy Files (empty) | 8.5 |
| 59 | Errors | Copy Custom Format (blank name) | 8.5 |
| 60 | Errors | Copy Multiple Formats (empty) | 8.5 |
| 61 | Errors | Restore (blank id) | 8.5 |
| 62 | Errors | Restore (unknown id) | 8.5 |
| 63 | Errors | Cancel (unknown id) | 8.5 |
| 64 | Errors | Copy After Shutdown | 8.5。**最後に押す** |
| 65 | Fixtures | Create Temp Files | M-5 の前提 |
| 66 | Fixtures | Delete Temp Files | — |

**M 項目に現れないボタンは 27 / 66。** 計画 9.3 の予測（Await 5 ボタンほか）より多い。
**照合工程を省くと、この 27 ボタンは一度も押されない。**

---

## 7. 手動確認観点

`artifact/designs/clipboard/2026-09-08-windows-clipboard-sample-scene-design-v3.md` の
6 節（ブロック A 〜 E）と 9 節（S-1 〜 S-9）に従う。**本工程では 1 件も実施していない。**

### 7.1 未実施項目（理由つき）

| # | 内容 | 理由 |
|---|---|---|
| M-1 〜 M-24 | 実機動作確認 | **本工程の範囲外。** サンプルを作るところまで |
| S-1 〜 S-9 | サンプルシーン自体の観点 | 同上 |
| M-20b | `ShutdownTimeout` の観測 | native が `NotYet` を返す条件が要る（計画 R-2） |
| M-8 | ACP が UTF-8 の環境 | 欠落しないため観測不能（計画 8.2.1） |
| `Copy (Exclude Roaming)` の効果 | 2 台目デバイスと同期有効が必要 | ネイティブ側も同じ理由で未実施（計画 R-5） |
| D-9 | 予約失敗時の世代管理 | サンプルから失敗を起こす手段が無い（計画 R-1） |
| M-22 | IL2CPP | 本検証はすべて Mono |

---

## 8. 要検証事項

| # | 内容 |
|---|---|
| V-1 | `CF_TEXT` の欠落挙動（M-8）。**まず状態行の `ACP:` を確認する**。65001 なら未実施 |
| V-2 | `Application.temporaryCachePath` の区切り文字。`Path.GetFullPath` で正規化しているが実機で確認する |
| V-5 | 画像の往復で置いたものと違う画像が返らないか。ネイティブは 8x8 を置いた往復で `1887x820` を得ている |
| **V-6** | **状態行の `ACP:` は `CultureInfo` 由来であり、ネイティブが使う*システム* ACP と一致する保証はない**（I-2）。食い違う環境があるなら `GetACP` の P/Invoke が要る |
| V-7 | **`Application.platform` をワーカースレッドから読めるか**（レビュー v1 A-5）。`CanRunOperation` は `IsMainThread()` より先にこれを読む。層 2a のテストはスレッド ID を偽装しているだけで実スレッドでは走っていない。`RunOnWorker` の catch は保険であり、**実際に投げるかは実機で初めて分かる** |
| V-3 | S-7 を実施するには TopMenu の Editor 分岐を本画面へ遷移させる必要がある。**現状は `DisplayDialog` のみで到達できない**（計画 10 節から未解決） |

---

## 9. 依存方向チェック

| 確認項目 | 結果 |
|---|---|
| UI 層 → Manager 層の一方向か | **適合。** `Runtime/Clipboard/` は UI を知らない |
| 製品コードに試験用の細工を入れていないか | **適合。** `enabled` は MonoBehaviour の公開プロパティ |
| 他プラットフォームのファイルに触れていないか | **適合。** 0 件 |
| OS 接頭辞（P1） | **適合。** 新規 8 ファイルすべて `Windows` 接頭辞。テストのクラス名も同様 |
| 二重ガード（P5） | **適合。** クラスガードのみ。サンプルは P/Invoke を持たない |

---

## 9.1 直さなかった残件（レビュー v1 由来）

| # | 内容 | 理由 |
|---|---|---|
| B-9 | M-20a の「再試行せずに 1 回で終わった」判定根拠が画面に出ない | 試行回数は Manager 側のログにしかない。**B-4 で結果行を `Debug.Log` に出すようにしたので、Player.log で Manager のログと並べて読める**。画面への露出は製品コードに手を入れないと作れない |
| B-10 | S-7 の「11. Threading は `MainThreadRequired`」が `Delayed History Call` に当てはまらない | 計画側の記述。**そもそも Editor から本画面に到達できない**（V-3）ため、S-7 全体が実施保留 |
| B-11 | M-3 は正常時にも `match=differ` を出す | 実装は正しい。**手順側で「M-3 の前に Clear を押す」を守る**。設計変更ではない |
| B-19 | シーケンス単調性の自動テスト | **v3 で実施**（レビュー v2 の B-1）。`++_resultSequence` を取ることをソース検査で固定した |
| C-14 | 計画 v3 の項番ゆれ（9.1 の見出し / 7.3 の S-6 / 3.2 の節番号） | 次の計画改訂時 |

### 9.2 直さなかった残件（レビュー v2 由来）

| # | 内容 | 理由 |
|---|---|---|
| **B-2** | **`_pending` の収支を固定するテストが無い** | 収支は Controller のフィールド操作（`Accept` / `AcceptAwaited` / `Done` / `Update` の worker failure）に埋まっており、層 1 から触るには**純粋型への切り出しが要る**。構造変更になるため見送った。**`Done` を `_pending -= 2` にしても 56 件は落ちない** |
| C-1 | 新規 `.meta` 3 件の trailing whitespace | Unity 生成物。`git diff --check` が報告する |
| — | `AcceptAwaited` が `OperationShutdown` で無条件に `_state = Draining` を立てるため、`ShutdownWithDrain` が**冪等成功で drain を開始しない場合にも**一時的に `Draining` と表示される | 配送時に `Advance` が `ShutDown` へ訂正するので残らないが、**1 フレーム以上は嘘を出す**。外から drain の開始を知る手段が無いため、製品コードに触れずには消せない |

**ミューテーション検証は 2 巡とも回していない。** 56 件は依然として私が書いたものであり、
レビュー v1 が死んだテスト 1 件と回避可能な 3 件を、
レビュー v2 がさらに 4 件（うち 1 件は v1 の修正で私が入れた仕掛け自体）を見つけたのは、
いずれも**別の目で読んだから**である。**「全部緑」はテストが有効であることの証拠にならない。**

### 9.3 止める基準の充足状況

| 条件 | 状態 |
|---|---|
| A が 0 | **未確認。** レビュー v2 の修正後に誰も見ていない |
| レビュアーを替えて 1 回通す | **満たす**（v1 Claude 5 名 → v2 Codex `gpt-5.5` high） |
| wiring test が UXML の name を壊すと落ちる | **満たす**（v1 で確認済み。v2 で回避経路をさらに 4 件塞いだ） |
| 直さない残件が理由つきで明記されている | **満たす**（9.1 / 9.2） |

**基準未達を承知で実機確認へ進む。** 理由は実装結果 v6 の 6 節と同じで、
**公開 API 33 種のうち 28 種が一度も実行されていない**という状態が 2 巡のレビューで何も変わっていないためである。
3 巡目に投じるより、実機で 28 種を初めて動かす方が得られるものが大きい。

## 10. 次

1. **レビュアーを替えて再レビュー。** 止める基準の 3 条件のうち
   「レビュアーを替えて 1 回通す」が未達（v1 は Claude サブエージェント 5 名）
2. **実機確認**。計画 6 節のブロック A → B → C → D → E の順。
   **初回は落ちる前提**（28 種を初めて動かすため）
3. **6 節の照合表で全 66 ボタンを潰す**。M 項目だけでは 27 ボタンが残る

## 11. 実行確認

このサンプル実装結果を採用して、次工程へ進めますか？

- 実行する: この実装結果を採用して `review-implementation-sample-scene` へ
- 修正する: 指摘内容を反映して再実装
- キャンセル: ここまでの差分は保持したまま終了
