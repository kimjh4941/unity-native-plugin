# レビュー結果

- 日付: 2026-09-07
- 対象ファイル: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v7.md`
- 機能名: clipboard
- プラットフォーム: Windows
- レビュー条件: 前回とは別モデルによる独立レビュー

---

## 強み

- review-v4 の shutdown A1 は、`TryShutdownCore` と `FinishShutdownAttempt` に全起点を集約する形で反映されている。`PublicApi` / `Drain` / `Destroy` / `Quit` の状態、managed registry、COM / provider 所有権、quit 再開条件を一つの表で追える。
- native 実装との対応も正しい。`Uninit` は未完了時に lifecycle gate を閉じたまま `initialized_` と資源を保持し、完全成功時だけ HWND、coordinator、callback を解放する。再初期化時の `lifecycle_.Reopen()` は実初期化の commit 時だけ行われる。
- public `TryShutdown` の即時完了を含め、最初の shutdown 試行で `AwaitingNative` と `Undelivered` を同期ドレインする契約が追加された。前回の exactly-once の穴は閉じている。
- quit は成功、タイムアウト、終端失敗の全終端結果で再開される。前回の永久終了不能経路は解消されている。
- 前回要求した shutdown の結合テスト 3 件が 9.2 に追加されている。
- Runtime 15 ファイル、テスト 7 ファイルはすべて `Windows` 接頭辞を持ち、旧 `WindowsClipboardReadResults.cs` / `WindowsClipboardHistoryResults.cs` は主結果型ごとに分割されている。他プラットフォーム変更や `Runtime/Common` 追加もない。
- ファイル一覧検査の OS・章番号一般化は有効である。Windows の 6.1 / 7 章と macOS の 4.1 / 5 章の双方を検査できる。

## 改善点

### 高優先度

#### A1: `Running` 中の冪等 `Initialize` に COM 所有権を失う経路が残る

設計は `Initialize` を `Uninitialized` / `ShutDown` からのみ許可するとしている一方、同一スレッドからの複数回成功も前提にしている（7.3 の 855 行、7.4 の 990〜992 行、9.2 の 1484 行）。後者を現在の 7.3 の手順どおり実装すると、次の静かなリーク経路が成立する。

1. 初回 `Initialize` の fallback `CoInitializeEx` が `S_OK` を返し、`s_comOwnership = Initialized` となる。
2. `Running` 中に再度 `Initialize` すると、今度は `CoGetApartmentType` が STA を返す。その分岐は `s_comOwnership = None` と規定されている（813 行）。
3. native `InitClipboardManager` は同一 owner thread からの再呼び出しを `NONE` の冪等成功として直ちに返す。
4. shutdown 完了時、managed 層は ownership を `None` と認識して `CoUninitialize` を呼ばない。初回に取得した COM 参照が残る。

通常の確認環境では `MAINSTA` のため発生しないが、設計が明示的に支える fallback 経路では、コンパイルと既存テストを通過したままリークするため A1 とする。

`Running` 中の `Initialize` は COM/native 処理より前に managed 側で冪等成功を返し、既存の `s_comOwnership` と `enableChangeEvents` の状態を変更しない契約を追加する。あるいは `Running` では `Initialize` を拒否する方針へ統一する。現在の API 方針を保つなら前者が自然である。

併せて「fallback ownership 取得 → 冪等 `Initialize` → ownership 不変 → shutdown 完了時に `CoUninitialize` が 1 回」のテストを追加する。

### 中優先度

#### B: quit 再開テストの観測 seam が不足している

9.2 の seam 一覧には shutdown 結果注入があるが、`Application.Quit()` の呼び出しを置換または計数する seam がない。その状態で「quit が再開し、`Application.Quit()` が多重起動しない」を Editor PlayMode で検証する要件がある。

`Application.Quit()` を包む internal helper または delegate を注入可能にし、呼び出し回数と `s_quitDrainCompleted` を検証できるようにする。

#### B: Windows の count 検査は SKIP のままで、分割後の古い件数を見逃している

ファイル名照合の一般化は成功しているが、count 系検査は Windows v7 で 2 件とも SKIP になる。そのため、6.1 で結果型を分割した後も 7.9 に残る「結果型 3 ファイル」（1254 行）を検出できていない。

Windows の一覧形式にも count 検査を適用するか、設計内の重複する件数表記を削除する。章構成を macOS と同じ形式へ変える必要はなく、現在の表の行数またはファイル名集合を基準にできる。

### 低優先度

#### C: shutdown 結果配送表が `TryShutdown` の契約と矛盾する

`FinishShutdownAttempt` の段階 6 は `Drain` / `PublicApi` が共通イベントと per-call callback を配送すると書く一方、同じ行の括弧書きと API 表は public `TryShutdown` が event / callback を発火しないとしている（572、611、950 行）。`TryShutdown` には callback 引数もない。

段階 6 を `Drain` のみの配送に直し、`FinishShutdownAttempt` が callback を受け取るのか、caller が終端後に配送するのかを一方に統一する。

#### C: 分割後の実装順序に旧件数が残る

6.1 は結果型を個別ファイルへ分割済みだが、7.9 は「結果型 3 ファイル」のままである（631〜640 行、1254 行）。現在の構成に合わせる。

#### C: 「1 ファイル 1 型」の断定が明示例外と合わない

6.1 と P1 自己点検は「1 ファイル 1 型」と断定する一方、`WindowsClipboardPayloads.cs` は二つの主な public 型をまとめる例外である（622〜641、685 行）。「原則 1 ファイル 1 主型。`WindowsClipboardPayloads.cs` は既存前例に基づく明示例外」と統一する。

## 不足項目

- `Running` 中の managed 冪等初期化について、COM/native 呼び出し前の早期成功分岐と ownership 不変条件。
- 冪等 `Initialize` 後も COM ownership が維持され、shutdown で取得分が解放されるテスト。
- `Application.Quit()` の再開回数を観測できるテスト seam。
- Windows のファイル分割後の件数ドリフトを検出する機械照合、または重複件数表記の除去。

## 機械照合

- Windows v7: exit 0、FAIL 0、count 系 2 件のみ SKIP。一般化したファイル一覧検査は OK。
- macOS v14（`2026-09-05-macos-clipboard-design-v14.md`）: exit 0、FAIL 0、SKIP 0。
- Windows v6: exit 1。旧 `WindowsClipboardHistoryResults` / `WindowsClipboardReadResults` を未記載型として検出し、意図どおり FAIL。
- `git diff --check`: 本レビュー対象の設計 v7 と照合スクリプトに問題なし。

## 総合評価

**要修正。A1 は 1 種類残っている。** review-v4 の shutdown 終端集約は整合し、ファイル一覧検査の一般化と結果型分割も機能している。別モデルによる独立レビューと Windows v7 / macOS v14 の機械照合は完了したため、「レビュアーを替えて 1 回」と「機械照合が通る」は満たした。ただし `Running` 中の冪等 `Initialize` と COM ownership の組み合わせが参照をリークし得るため、A1 が 0 という停止条件はまだ満たさない。
