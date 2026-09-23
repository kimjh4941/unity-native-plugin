# レビュー結果 v2（サンプルシーン実装）

- 日付: 2026-09-08
- 対象: `7c89c67`（旧 `40d47fc`。メッセージ体裁のみ書き換え済み。ツリーは同一）
- 計画: `artifact/features/clipboard/designs/2026-09-08-windows-clipboard-sample-scene-design-v3.md`
- 実装結果: `artifact/features/clipboard/results/2026-09-08-windows-clipboard-implement-sample-scene-result-v2.md`
- 前ラウンド: v1（Claude サブエージェント 5 名。A 6 / B 22 / C 14）
- レビュアー: **Codex CLI `gpt-5.5` / reasoning effort high**（read-only）
- **レビュアーを替える条件を満たす**

| 区分 | 件数 |
|---|---|
| A | **5** |
| B | 6 |
| C | 1 |

---

## 0. この文書の読み方

**A 5 件はすべて実物のコードを引いて裏付けを取った。却下は無い。**

**指摘箇所は v1 とほぼ重なっていない。** 同じコードを別のモデルが読んで別の穴を出したということであり、
「A が 0 になった」を 1 巡で判定してはいけないことの実例になっている。

---

## 強み（Codex が実物と照合して確認したもの）

- `KeepsDraining` が `ClassifyShutdown` の retry set と一致している（v1 A-2 の修正が正しい）
- UXML 66 ボタンと `Bindings` の対応が、名前とハンドラ名の両方で機械検査されている
- DIB フィクスチャがヘッダだけでなく **64 画素の BGRA 値**まで検査されており、v1 B-17 の穴は塞がれている

---

## A 区分

### A-1: `Force Initialize While Draining` が Draining 中の Initialize を踏まない

- 該当: `WindowsClipboardManagerExampleController.cs`（`OnForceInitializeWhileDrainingClicked`）
- 根拠（実物で確認）:
  - `StartDrain` はセッションを作るだけで `s_state` を触らない。**doc コメント自身が
    「The first attempt happens on the next Update, never inside this call」と書いている**
    （`WindowsClipboardManager.cs:2839-2848`）
  - `s_state = Draining` が立つのは次の `Update` → `AdvanceDrain` → `FinishShutdownAttempt`
  - `Initialize` は `s_state == Running` を**冪等成功**で返す（同 `810-813`）
- 何が嘘になるか: `Running` 中に押すと Initialize は成功する。**`ShuttingDown` ガードの確認ボタンに見えて、
  確認対象のガードに到達しない。** ハンドラのコメントは「同一フレームだから拒否される」と説明しており、
  これも誤り
- 提案: 1 フレーム待って実際に `Draining` に入ったことを確認してから `Initialize` を呼ぶ

### A-2: 非同期ログの sequence が単調でない

- 該当: `Accept` / `AcceptAwaited` / `Done` は `call.Sequence` を再利用、`LogEvent` だけが `++_resultSequence`
- 根拠: Manager は共通イベントを per-call callback より先に発火する（`WindowsClipboardManager.cs:3240`）
- 何が嘘になるか: `Get History` で `#1 [accept]` → `#2 [event]` → **`#1 [done]`** と番号が戻る。
  **計画 4.3 が「単調増加の seq で配送順序を目視する」と定めた根拠そのものが成立していない。**
  「`[call]` の seq が `[event]` より小さい」という配送順序の判定も、番号の再利用があると読めない
- 提案: **行番号と呼び出し ID を分ける。** 各行は常に `++_resultSequence`、対応付けは `call=#N` の別欄

### A-3: 成功した Copy が他形式の古いアンカーを消さない

- 該当: `CopyText` は `_lastTextHash` のみ、`CopyImage` は `_lastImageHash` のみ更新。
  `DropRoundTripAnchors()` は Clear 系でしか呼ばれない
- 根拠（実物で確認）: `ClassifyFirstRead` は **`FormatUnavailable` を `EmptySuccess` に正規化する**
  （`WindowsClipboardManager.cs:1023-1025`）
- 何が嘘になるか: **Copy はクリップボード全体を置換する。**
  `Copy Image` → `Copy Plain Text` → `Paste Image` とすると、画像は既に無いので `FormatUnavailable` だが、
  **成功・空として返る**。アンカーは古い画像のまま残っているので `match=differ` になる。
  画像往復が壊れたのではなく、テキストで置き換えただけである
- **v1 の B-6 修正（失敗した読み出しは `n/a`）はこれを救わない。** 失敗ではなく成功として返るため
- 提案: **書き込み成功のたびに全アンカーを落とし、その操作が実際に載せた形式だけ再設定する**

### A-4: `Clear` が失敗してもアンカーを捨てる

- 該当: `OnClearClicked` / `OnPastePlainTextAfterClearClicked`
- 根拠: `Clear()` の成否に関係なく `DropRoundTripAnchors()` を呼んでいる。
  Manager はガードやネイティブ失敗で失敗を返す（`WindowsClipboardManager.cs:1348`）
- 何が起きるか: Clear が失敗してクリップボードが前の内容を保持していても「比較対象なし」にしてしまう。
  **本来「前回書いた内容が残っている」と観測できる状態を `match=n/a` にする**
- 提案: `IsSuccess` のときだけ落とす。`Paste Plain Text (after Clear)` は Clear 失敗時に
  Paste へ進まず local 行で前提未成立を出す

### A-5: 画面の About に古い説明が残っている

- 該当: `WindowsClipboardManagerExample.uxml:20`
- 根拠: UXML はまだ「TryShutdown fires neither the event nor the callback, so counters standing still
  there is correct」と表示する。**Controller 側の remark は v1 A-4 で直したが、UXML を直していない**
- 何が起きるか: `Get History` 直後に `Try Shutdown` した正常な drain を、**画面の説明どおり読むと
  「カウンタが動いたので異常」と誤判定する。** 操作者が読むのは画面であって XML doc ではない
- 提案: UXML の About 文も Controller に合わせる

---

## B 区分

| # | 内容 | どう壊すと落ちないか |
|---|---|---|
| B-1 | **sequence 単調性の自動テストが無い** | 現状どおり `Done` が `call.Sequence` を再利用しても落ちない。A-2 がテストをすり抜けている |
| B-2 | **`_pending` の収支を固定するテストが無い** | `Update` の worker failure 側の `_pending--` を消しても、`Done` を `_pending -= 2` にしても、53 件は Controller の pending 遷移を実行しないため検出できない |
| B-3 | **Await の cancel 検査が「ガードの後ろのどこかに `return;` がある」だけ** | 各ハンドラを `if (Cancelled(...)) { } return;` に変えると、**成功した Await でも `Done` せず終了する**が検査は通る |
| B-4 | **provider 検査が「各 provider が自分のカウンタを進める」ことを固定していない** | text provider に両方の increment を置き、image provider を `return dib;` だけにしても `Interlocked.Increment` は 2 回のまま |
| B-5 | **`CodeOnly` がエスケープされた引用符を扱えない** | provider 内に `string s = "\" // " + Time.frameCount.ToString();` を置くと、実コードの `Time.` が削られて禁止語検査をすり抜ける |
| B-6 | **`ClipboardChanged` だけ Player.log に残らない** | `OnClipboardChangedEvent` は `LogEvent` を通さず `AppendResult` だけ。**M-10 の外部コピー観測で最重要のイベントが証跡から抜ける** |

**B-5 は v1 の修正で私が追加したものである。** テストを強くするために足した仕掛け自体に穴があった。

---

## C 区分

| # | 内容 |
|---|---|
| C-1 | 新規 `.meta` 3 件に trailing whitespace（Unity 生成物）。`git diff --check` が報告する |

---

## 前回の修正（v1 対応）が持ち込んだもの

| # | 内容 |
|---|---|
| 1 | `AcceptAwaited` が `OperationShutdown` で `_state = Draining` を立てるため、`ShutdownWithDrain` が**冪等成功で drain を開始しない場合にも**一時的に `Draining` と表示される。配送時に `Advance` が `ShutDown` へ訂正するので残らないが、1 フレーム以上は嘘を出す |
| 2 | `DropRoundTripAnchors` の導入により、**Clear 失敗時にも全アンカーを捨てる経路が明確になった**（A-4） |

**実装フェーズでも、レビュー v2 の修正 5 件のうち 3 件が v3 で「修正が持ち込んだもの」として指摘された。
同じことがサンプルでも起きている。**

---

## 総合評価

**要修正。A 区分 5 件。**

止める基準（A = 0）は満たしていない。ただし v1 とは指摘の質が変わっている。

| ラウンド | レビュアー | A | 主な内容 |
|---|---|---|---|
| v1 | Claude サブエージェント 5 名 | 6 | 状態追跡・アンカーのゲート・テストの回避経路 |
| v2 | Codex `gpt-5.5` high | **5** | **踏まない経路・番号の非単調・アンカーの寿命・画面文言** |

### 構造として見えたこと

**1. アンカーの寿命が設計されていない。**
A-3 / A-4 と v1 の A-3 / B-8 は全部これ。「いつ設定するか」だけを個別に直してきたが、
**「いつ落とすか」を一度も決めていない。** 個別修正を続ける限り再発する。
**書き込み成功で全落とし → その操作が載せた形式だけ再設定**という単一の規則にする。

**2. 番号の再利用が観測の土台を壊していた。**
A-2 は「行の順序を読む」という画面の存在意義に直接効く。**行番号と呼び出し ID は別物である。**

**3. 画面とコードで説明が二重管理されている。**
A-5 は Controller の remark を直して UXML を直し忘れたもの。**操作者が読むのは画面である。**

### 次

A 5 件と B の中心（B-1 / B-2 / B-3 / B-6）を直し、**3 巡目**に回す。
2 巡でどちらも A が 0 にならなかったので、**1 巡で判定しないことを前提に置く。**
