# レビュー結果

- 日付: 2026-09-07
- 対象ファイル: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v6.md`
- 機能名: clipboard
- プラットフォーム: Windows

---

## 強み

- v3 の provider 世代問題は、`pError` だけで確定する 3 分岐へ整理されている。`NONE` は新世代、`PARTIAL_STATE` は新世代と旧世代の非衝突キー、その他は旧世代維持という規則は、D-9 のネイティブ状態と対応しており実装可能である。
- `reserveDeferredFormats` 呼び出し中の同期再入に対して、旧世代と新世代を合わせた staging を公開する設計が追加された。結果確定と `finally` での staging 解除も記載され、失敗経路を含めて寿命が明確になった。
- `RecoverDeferredState` の `NONE` が「回復済み」や「予約消失」を保証しない点を実装から確認し、provider / cache を解放しない規則へ直している。
- COM 所有権表は、初期化成功、ネイティブ失敗、Bridge 例外、shutdown 完了、未完了を一つの表で追える。前回のリーク経路は閉じられた。
- `s_quitHandlerSubscribed` による多重購読防止と解除条件が追加され、冪等な `Initialize` と static event の寿命が整合した。
- `scripts/check_design_consistency.py` は Windows 上で解決不能な xcframework のリンクを安全に飛ばすよう修正されている。v6 と既存 macOS v13 の双方を実行し、どちらも exit 0 を確認した。v6 では章参照、識別子宣言、C# 引用行を含む実行対象チェックがすべて成功している。

## 改善点

### 高優先度

#### A1: shutdown の終端処理が呼び出し元ごとに分散し、状態・リクエスト・quit の整合がまだ崩れる

同じ原因から次の 3 経路が発生する。いずれも利用者が通常の API とテストだけでは気づきにくい状態不整合または永久未完了になるため A1 とする。

1. **`ShutdownFailed` からの `Initialize` が閉じた native lifecycle gate を再開しない。** 7.4 は `ShutdownFailed` からの再初期化を許可している（921 行）。一方、native `Uninit` は最初に `CloseAndDrain()` を行い、`BUSY`、`CANCELED`、monitor 停止失敗などでは `initialized_ == true` のまま `FALSE` を返す。続く native `InitClipboardManager` は `initialized_` を見て `NONE` の冪等成功を返すだけで、`lifecycle_.Reopen()` に到達しない。C# は `Running` に戻るが native gate は閉じたままとなり、以後の操作が `NOT_INITIALIZED` になる。
2. **public `TryShutdown` の完了時に受付済みリクエストを同期ドレインする契約が無い。** 7.6.3 の teardown 発火元は `ShutdownWithDrain`、`OnDestroy`、`ResetForTests`、quit に限定されている（1045 行）。public `TryShutdown` が `completed == true` で直接 `ShutDown` へ遷移した場合、レジストリ内の `AwaitingNative` / `Undelivered` が残り、設計自身が保証する exactly-once と「Awaitable の永久未完了が発生しない」（1057 行）を満たせない。native の `CANCELED` 配送には依存しないという契約なので、native 側の挙動では補えない。
3. **quit 中の終端失敗で終了を再開する規則が無い。** 7.4 は `WrongThread` とその他のエラーを終端失敗として即時打ち切りにする一方、`OnWantsToQuit` が `s_quitDrainCompleted` を立てて `Application.Quit()` を再実行するのは成功と `ShutdownTimeout` だけである（941 行）。終端失敗では `s_quitDrainStarted == true` が残り、以後の `wantsToQuit` が常に `false` を返す実装になり得る。

局所的に 3 箇所へ条件を足すのではなく、全 shutdown 起点が共通で通る終端関数を設計することを推奨する。例えば `FinishShutdownAttempt(origin, result, completed)` に次を一括して持たせる。

- 最初の shutdown 試行で `Running` から `Draining` へ遷移し、同時に managed request registry を同期ドレインする。
- `completed == true` のときだけ COM、delegate、provider、cache を解放して `ShutDown` へ遷移する。
- 未完了なら `Draining`、終端失敗またはタイムアウトなら `ShutdownFailed` とし、native 資源を保持する。
- quit 起点では成功・タイムアウト・その他の終端失敗のすべてで quit 完了フラグを立て、結果をログへ残して終了を再開する。
- `Initialize` は `ShutDown` からだけ許可する。`ShutdownFailed` は `TryShutdownCore` の再試行で native の `completed == true` を得て `ShutDown` へ移ってから再初期化する。

この一つの関数について、状態、managed registry、native gate、COM/provider 所有権を同じ遷移表に記載すれば、各呼び出し元の記述が再び分岐するのを防げる。

### 中優先度

#### B: Windows 設計のファイル一覧照合は実行されていない

照合スクリプトは成功しているが、最後の検査は `4.1` と `Mac\w+\.cs` に固定されている。v6 では `no 4.1 section` として SKIP になり、Windows 設計のファイル一覧と実装章の対応は機械検証されない。チェック名と抽出規則を OS・章番号に依存しない形へ一般化し、v6 でも OK になるケースを追加すると、「機械照合が通る」の対象範囲が明確になる。

#### B: shutdown の重要経路を結合したテストが不足している

9.1 にはエラー分類テストがあるが、上記 A1 の状態連鎖を検出するテストが無い。少なくとも次を追加する必要がある。

- `Running` から public `TryShutdown` が即時完了したとき、受付済み callback / Awaitable がちょうど 1 回完了する。
- `ShutdownFailed` では `Initialize` を拒否し、shutdown 再試行の完了後だけ再初期化できる。
- quit ドレインが `WrongThread` またはその他の終端失敗で終わっても、quit を再開し多重起動しない。

### 低優先度

#### C: `Uninitialized` の public `TryShutdown` の結果が矛盾する

871 行は `NotInitializedByHost` を返すとしているが、状態表の 901 行と直接呼び出し表の 919 行は成功扱いとしている。native の冪等性と表の一貫性から、成功扱いへ統一するのが自然である。

#### C: `OnDestroy` の呼び出し先が古い

866〜870 行は `OnDestroy` が tombstone 後に `TryShutdownCore` を呼ぶとしているが、943 行は public `TryShutdown` を呼ぶとしている。`TryShutdownCore` に統一する。

#### C: provider 世代テストの「rollback 成功時のマージ規則」が古い

1378 行の「`PARTIAL_STATE` / rollback 成功時のマージ規則」は、v6 の `pError` だけで決める表と対応しない。rollback 成功後の失敗は D-9 の具体的な `pError` に置き換え、`PARTIAL_STATE` と「その他の失敗」を別ケースとして記載する。

## 不足項目

- shutdown の全起点を共通化した終端関数と、その単一の状態・所有権遷移表。
- `ShutdownFailed` から native shutdown 完了を経て再初期化するテスト。
- public `TryShutdown` と request registry の exactly-once を結合したテスト。
- quit の全終端結果を網羅するテスト。
- Windows のファイル一覧にも適用される機械照合。

## 総合評価

**要修正。A1 は 1 種類（shutdown ライフサイクルの終端処理）残っている。** provider 世代、COM 解放、予約中の再入、購読の多重化、回復時の provider 寿命については前回指摘が適切に反映され、機械照合の回帰確認も完了している。

ただし設計 v6 まで進み、shutdown ライフサイクルという同種の指摘が 3 回以上続いているため、レビュー手順のサーキットブレーカーに該当する。次版では個別経路への追記を止め、全 shutdown 起点を一つの終端関数と一つの遷移表へ集約してから再照合するべきである。A1 が解消し、上記 B/C が設計自身に追跡可能な形で記載されれば停止基準を満たせる。
