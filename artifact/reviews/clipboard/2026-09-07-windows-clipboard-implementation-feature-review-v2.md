# 実装レビュー結果 v2（ステップ 1〜7 全体）

## 基本情報

- 日付: 2026-09-07
- 機能名: clipboard
- 対象プラットフォーム: Windows
- レビュー対象: ブランチ `feature/UNT-11`（コミット `8115350`〜`284a607` の 6 コミット、Runtime 15 / テスト 7 ファイル、6,744 行）
- 実装計画: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v8.md`
- 実装結果: `artifact/results/clipboard/2026-09-07-windows-clipboard-implementation-feature-result-v4.md`
- 前回レビュー: `artifact/reviews/clipboard/2026-09-07-windows-clipboard-implementation-feature-review-v1.md`（**ステップ 1 の純粋層のみが対象**）
- レビュー体制: 独立した 6 レビュアーに領域を分割（ライフサイクル / 同期 API / 非同期レジストリ / 遅延レンダリング / プロジェクトルール / テスト有効性）。作業ツリーは読み取り専用で固定し、レビュー中の編集は行っていない

## レビュー概要

前回 v1 はステップ 1（純粋層 6 ファイル）だけを対象としており、本体である `WindowsClipboardManager.cs`（3,189 行）は未レビューだった。今回はその全体を対象とする。

結果は **要修正（重大）**。A 区分 15 種、B 区分 21 種、C 区分 15 種。

ただし件数より重要なのは、**A 区分の過半がひとつの原因に帰着する**ことである。詳細は「サーキットブレーカー」の節に記す。

---

## 重大な問題（high / A 区分 — 振る舞いを変える）

A 区分は「コンパイルもテストも通り抜けたうえで、利用者から見た動作を変えるもの」。

### A-1. ドレイン中の中間試行が終端関数を通らず、`Draining` に一度も入らない

`WindowsClipboardManager.cs:2342-2367`

`DrainRoutine` は `TryShutdownCore` をループで繰り返すが、`FinishShutdownAttempt` を呼ぶのは**ループ終了後の 1 回だけ**。`s_state = Draining` への遷移とレジストリの同期ドレインは `FinishShutdownAttempt` の中にしかない（`:2281-2285`）。

結果、`ShutdownWithDrain` が 1 フレームで完了しない通常系で、**最大 60 フレーム / 2 秒のあいだ状態が `Running` のまま**になる。この間 `CanRunOperation`（`:838-842`）は `ShuttingDown`(1011) を返せず、新規操作がネイティブ境界まで到達する。呼び出し側には 1011 ではなくネイティブの `NotInitialized`(2) が返る。さらに、この窓の中で新規の非同期リクエストがレジストリに登録でき、ループ終了時にまとめて `Canceled` にされる。

同じ原因で `ShutdownWithDrain` に再入ガードが効かず、2 回呼ぶとコルーチンが 2 本走り、`ClipboardOperationCompleted` が 2 回発火する（`:2367`）。`FinishShutdownAttempt` に `ShutDown` を no-op にするガードが無いため、設計 7.4 遷移表の「`ShutDown` は変化なし」にも反する。

根拠: 設計 7.4「`TryShutdownCore` の**1 回の試行が終わるたびに**、起点を問わず必ず次を通す」。`TryShutdown`（`:675`）と `OnDestroy`（`:540`）は試行直後に呼んでおり、**ドレイン経路だけが集約から漏れている**。

### A-2. `PartialState` で `recoverDeferredState` を 1 回も試さず、しかも回復手段が到達不能

`WindowsClipboardManager.cs:2337`（分類）、`:2342-2357`（ドレイン）、`:1707-1722`（public API）

**3 名のレビュアーが独立に到達した指摘。**

`ClassifyShutdown` は `PartialState`(13) を `NotYet` に落として再試行を続けるだけで、`recoverDeferredState` を呼ばない。リポジトリ全体を検索しても、このネイティブ関数の呼び出し元は public `RecoverDeferredState` の 1 か所のみ。

そのうえ `RecoverDeferredState` は `CanRunOperation` を通す（`:1711`）ため、`Draining` / `ShutdownFailed` では `ShuttingDown`(1011) で拒否される。つまり**この関数が必要になる唯一の状況で、C# から呼ぶ手段が塞がっている**。

結果: 予約形式のロールバック失敗で partial 状態に入ると、`uninit` は `PARTIAL_STATE` を返し続け、60 フレーム使い切って `ShutdownTimeout`(1006) → `ShutdownFailed`。`DestroyWindow` が走らないので `WM_RENDERALLFORMATS` も送られず、**予約した形式はエラーもログも無くクリップボードから消える**。D-1〜D-9 で守った結果そのものが失われる。

根拠: 設計 7.4 ドレイン規則表「13 `PartialState` → 未完了（要回復）→ `recoverDeferredState` を **1 回試してから**リトライを続ける」、設計 2.6。

### A-3. `OnDestroy` が特定の状態でしかドレインせず、コールバックが永久に発火しない

`WindowsClipboardManager.cs:536`

`OnDestroy` は `s_state` が `Running` / `Draining` / `ShutdownFailed` のときだけ `FinishShutdownAttempt` を呼び、teardown ドレインはその中にしかない。`Uninitialized` と `ShutDown` ではドレインが走らない。

シナリオ: `Initialize` を呼ばずに `GetHistoryAsync()` → `RejectRequest` が ticket を `Undelivered` で登録し dispatcher に enqueue（`:2039`）→ 同フレームで PlayMode 停止 → `s_state == Uninitialized` なのでドレインなし → `Update` はもう回らない → **per-call callback も `Awaitable` も一度も完了しない**。

根拠: 設計 7.6.3、8.4「受付済み・受付前拒否のいずれでもちょうど 1 回配送される」。

### A-4. `TryClaim` が native id マッピングを所有者確認なしに削除する

`WindowsClipboardRequestTable.cs:153-159`

`if (entry.NativeRequestId != 0) _byNativeId.Remove(entry.NativeRequestId);` が、そのマッピングが今も自分の ticket を指しているかを確認しない。`MarkUndelivered` は `Undelivered` 遷移後もマッピングを保持する（ネイティブの重複完了を吸収するため）ので、同一 native id が 2 ticket にまたがる窓が実在する。

シナリオ: A が id X で受付 → A 完了 → `MarkUndelivered`、マッピング残存 → `Update` 前に B が開始しネイティブが id X を再利用 → `_byNativeId[X] = ticketB` → A の配送が `TryClaim(ticketA)` で **B のマッピングまで消す** → B の完了は「unknown request id」警告だけで捨てられ、更新系なら `s_inFlight` も解放されず**以後永久に `OperationBusy`**。

同ファイル `:61-72` の `IssueTicket` は 32bit ラップを明示的に防御しているのに、native id 側だけ無防備という非対称がある。ネイティブの id 再利用規則は未確認（**要確認**）だが、Editor のテストシーム経由では確定的に再現する。

### A-5. `CancellationTokenRegistration.Dispose()` を dispatcher のロック内で呼んでいる（デッドロック）

`WindowsClipboardManager.cs:2085`、`Runtime/Common/UnityMainThreadDispatcher.cs:55-64`

`UnityMainThreadDispatcher.Update` は `lock (_executionQueue)` を**保持したまま**キューのアクションを `Invoke` する。そのアクションが `DeliverClaimed` → `Registration.Dispose()` に至る。`CancellationTokenRegistration.Dispose()` は「別スレッドで実行中の登録 callback の完了を待つ」契約。

シナリオ: ワーカースレッドが `cts.Cancel()` → 登録 callback がそのスレッドで走り `dispatcher.Enqueue` のロック取得待ちに入る → 同時にメインスレッドの `Update` がロックを保持したまま `Dispose()` を呼ぶ → Dispose はワーカーを待ち、ワーカーはロックを待つ → **Unity メインスレッドが恒久ハング**。

設計 9.2 が要求する M1（キャンセル競合）そのものの並行。dispatcher は 4 プラットフォーム × 4 機能で共有される既存ファイルなので、P4 の観点から**修正は Clipboard 側**（`Unregister()` か、registration をローカルへ退避してロック外で破棄）に置く。

### A-6. 「すでにキャンセル済みトークン」の経路が未実装

`WindowsClipboardManager.cs:1736-1741` ほか `*Async` 5 本すべて

5 本とも「callback 版を呼ぶ → `RegisterCancellation`」の順で、トークンが既にキャンセル済みでもネイティブ受付まで進む。`RegisterCancellation`（`:2183`）にも `IsCancellationRequested` の早期分岐が無い。

設計 7.6.5 は「すでにキャンセル済みのトークンが渡された場合は、**ネイティブを呼ばずに** `Canceled`(15) で即座に完了させる」と定めている。現状は不要なリクエストがネイティブに投入され、直後に teardown が走ると `CancelRequest` が実行されないまま**ネイティブ側のリクエストだけが残る**。

### A-7. `StartRequest` の例外フィルタが狭く、in-flight マーカーが恒久リークする

`WindowsClipboardManager.cs:1973-1996`

`s_pending[ticket]` と `s_inFlight.Add` の**後**にネイティブを呼ぶが、catch は `DllNotFoundException` / `EntryPointNotFoundException` のみ。他の例外は外へ抜ける。この時点でレジストリにはエントリが 1 つも無いため、**teardown ドレインでも回収できない**。

`BadImageFormatException`（32bit / 破損 DLL）や文字列マーシャリングの例外で、`s_inFlight` に操作名が残り続け、以後の `RestoreHistoryItem` が**永久に `OperationBusy`**。`s_pending` と `CancellationTokenRegistration` も解放されない。

### A-8. `Initialize` だけ tombstone より状態判定が先で、破棄後に誤ったコードを返す

`WindowsClipboardManager.cs:576-586` vs `:594-599`

`CanRunOperation` は tombstone を状態より先に見る（`:822-830`）のに、`Initialize` だけ順序が逆。

シナリオ: `Initialize` 成功 → `Destroy(gameObject)` → `OnDestroy` が tombstone を立て 1 回だけ試行 → 未完了なら `s_state = Draining` → `Instance` が新しい GameObject を生成 → `Initialize()` が **`ShuttingDown`(1011)**（期待は `ManagerDestroyed`(1003)）。しかもこの `Draining` を抜ける契機が無い（コルーチンは破棄済み、quit handler は解除済み）ため、**以後の全操作が「shutdown 中」を名乗り続ける**。

根拠: 設計 7.4「tombstone が立っている場合は**状態に関わらず** `ManagerDestroyed` が優先する」。

### A-9. quit ハンドラがコルーチン起動失敗から復帰できず、アプリが終了不能になる

`WindowsClipboardManager.cs:2430-2438`

`s_quitDrainStarted = true` を立てた**後**に `StartCoroutine` を呼ぶが、try/catch も失敗時にフラグを戻す経路も無い。`s_quitDrainCompleted` を立てるのはコルーチンが完走した場合のみ。

Manager の GameObject が非アクティブ（`DontDestroyOnLoad` の singleton なので利用側が触れられる）だと `StartCoroutine` はコルーチンを起動せず、`OnWantsToQuit` は以後すべて `false` を返し、**アプリが二度と終了できない**。`_instance == null` の救済分岐（`:2432-2436`）はあるが、起動失敗には対応が無い。

根拠: 設計 7.4「`ShutdownTimeout` でも終端失敗でも終了は通す（アプリが終了できなくなる事態を避ける）」。

### A-10. `CanRunOperation` にプラットフォーム判定が無い

`WindowsClipboardManager.cs:826-851`

事前チェックは「メインスレッド → tombstone → `Draining`/`ShutdownFailed` → `s_state != Running`」の 4 段で、プラットフォーム判定が無い。この判定は `Initialize`（`:602`）と `CanShutdownNow` にしかない。

設計 7.5 の事前チェック順序は「メインスレッド → 破棄済み → **プラットフォーム** → 初期化済み」。結果、Editor で `Initialize` 前に操作を呼ぶと `PlatformUnavailable`(1000) ではなく `NotInitializedByHost`(1004)「requires Initialize to succeed first」が返る。Editor では `Initialize` が成功し得ないため、このメッセージは開発者を「初期化し忘れ」へ誤誘導する。

`WindowsClipboardErrorCode.cs:77` の XML「Not running on a Windows player build. **The editor always reports this.**」とも矛盾する。

なお実装結果 v4 §3 の「Editor では `PlatformUnavailable` が返る」という記述は、`Initialize` とネイティブ境界の `#else` 枝については正しいが、**この事前チェック経路については誤り**。結果ファイルの訂正が要る。

### A-11. 空文字列が全テキスト API で `IsEmpty` に正規化される

`WindowsClipboardManager.cs:1266-1271`

コメントは `GetPreferredFormat` 用の正規化と説明しているが、実際には `PastePlainText` / `PasteHtml` にも同じ経路が適用される。

`CopyPlainText("")` は引数検証（`text == null` のみ）を通過し、ネイティブも空文字を受理する。直後の `PastePlainText()` はネイティブ `WriteStringToBuffer` が `needed = 0 + 1 = 1` を返すので **`NONE` + サイズ 1**。設計 7.5 の表ではこれは「非空の成功、`Text == ""`」だが、実装は `IsEmpty = true, Text = null` を返す。呼び出し側は「空文字がある」と「テキストが無い」を区別できない。

### A-12. dispatcher が null のとき、拒否結果が呼び出し元のスタック内で配送される

`WindowsClipboardManager.cs:2053-2064`

`s_dispatcher == null` のとき `DeliverIfClaimed(ticket)` をその場で呼ぶため、`RejectRequest` 経由だと `GetHistory` が return する前に callback が発火する。callback から再度 `GetHistory` を呼ぶ再入経路になる。

設計 8.4 は「イベント / callback は**呼び出し元のスタック外**で配送される（teardown ドレインのみ同期発火）」。同じ null 条件で同期 API 側の `Dispatch`（`:2568-2575`）は「result dropped」とログして捨てており、**扱いが非対称**。

### A-13. `OnRenderFormatNative` だけ try/catch が無く、`out` も設定されない

`WindowsClipboardManager.cs:2864-2872`

他の 5 コールバック（`:2149` / `:2889` / `:2903` / `:2916` / `:3040`）は本体全体を try/catch で包むが、render だけは 1 段下の `RenderDeferredFormat` の catch に依存する。provider は Unity のシャットダウン進行中に走る（D-7）ため、catch 節内の `Debug.LogError` がログサブシステム破棄後に投げると、例外が C ABI 境界を越える。

加えて例外で抜けると `out requiredSize` が書き戻されず、**ネイティブが未初期化のスタック値を必要サイズとして読む**（D-5 の比較対象が乱数になる）。

根拠: 設計 7.8「コールバック内で例外を C ABI 境界へ漏らさない（**全体を** try/catch）。`out` 引数を持つ delegate では catch 節で必ず `out` に値を代入する」。

### A-14. イベント購読者どうしが隔離されていない（横断課題）

`WindowsClipboardManager.cs:2537` / `:2592` / `:2949`

multicast delegate を 1 回で呼び、try/catch が呼び出し全体を包む。`GetInvocationList` はパッケージ全体で 0 件。先に登録された購読者が例外を投げると、**以後の毎回のイベントで 2 番目以降の購読者に届かない**（1 回の取りこぼしではなく恒久）。

`InvokeInOrder` の XML（`:2525`）は "isolating each from the other's exceptions so one bad subscriber cannot swallow the other's result" と書いているが、隔離されているのは「共通イベント全体 vs per-call callback」の 2 者のみ。

**Android / iOS / macOS の既存 Manager にも共通する慣行**であり Windows 固有の退行ではない。横断課題として別途扱うのが妥当。dispatcher 自体は壊れない（enqueue されるアクション本体が必ず try/catch の内側）。

### A-15. `Initialize` の例外フィルタが狭く、COM 参照が漏れる（要確認）

`WindowsClipboardManager.cs:617`

catch フィルタが `DllNotFoundException` / `EntryPointNotFoundException` のみ。`SEHException` 等は `ReleaseOwnedComReference()`（`:622`）に到達しない。この場合 `s_state == Uninitialized` かつ `s_comOwnership != None` が成立し、`TryShutdown` は `Uninitialized` を即成功で返す（`:668`）ため**解放経路が無い**。

設計 7.3 の ownership 表は「`initClipboardManager` の P/Invoke が例外 → その場で解放」と定めるが、設計 8.3 は `BridgeUnavailable` をこの 2 例外に限定しており、**設計内で記述が競合している**。fallback 経路自体が実機では稀なため影響は限定的。

---

## 改善提案（medium / B 区分 — 検証手段を変える）

B 区分は「中心契約が未検証」と「網羅系の不足」を区別する。**中心契約**を先に挙げる。

### 中心契約が未検証

| # | 内容 | 壊し方（全テストが通る） |
|---|---|---|
| B-1 | `MainThreadRequired`(1002) のガードが全テストで無効。`IsMainThread()` は `s_mainThreadId == 0` を無条件 true とし、EditMode では `ResetForTests` が 0 にする | `CanRunOperation:828-832` のスレッド判定と、`Initialize` / `TryShutdown` / `ShutdownWithDrain` / `CanShutdownNow` / `TryShutdownCore` の同判定を**全削除**しても通る |
| B-2 | quit ドレインが完全に未テスト。`QuitActionForTests`（`:389`）の参照は 0 件。**しかも現状のシームでは書けない** — `InjectShutdownResultForTests` が `ShutdownOrigin.PublicApi` 固定で `ResumeQuit` に到達できない | `ResumeQuit:2454` の `s_quitDrainCompleted = true` を削除しても通る。実機ではアプリが終了不能になる |
| B-3 | `Initialize` がどのテストからも呼ばれていない。`Running` は全て `SetStateForTests` で作られる | `:638` の `SubscribeQuitHandler()` 削除、冪等分岐の反転、`:622`/`:633` の COM 解放削除、いずれも通る |
| B-4 | **M1（キャンセル競合）の回帰テストが存在しない**（DoD 11 が名指しで要求）。`RegisterCancellation` を通るテストが 0 件 | dispatcher ホップを外す / 完了済み id の再利用チェックを削る / `Dispose()` を削る、いずれも通る。A-5・A-6 はこの穴で見逃された |
| B-5 | `HistoryReadCompleted` と `HistoryAvailabilityChecked` を購読するテストが 0 件 | `:2108` / `:2130` の `InvokeInOrder` から共通イベント引数を `null` にしても通る。購読者に結果が届かなくなる |
| B-6 | 「発火しない契約」が 1 件も未検証 | `TryShutdown:677` を `Deliver(result, null)` に変えても通る。`ShutdownWithDrain` 経由で最大 60 回イベントが飛ぶ |
| B-7 | `ReserveDeferredFormats` / `RecoverDeferredState` の public 経路が未テスト。設計 9.2 が要求する `InvokeRenderDuringReserveForTests` シームが未実装で、staging 窓（`:1682` / `:1691` / `:1852-1856`）を検証できない | staging を丸ごと削っても通る。予約中に届いた `WM_RENDERFORMAT` が provider を解決できず**形式が無言で落ちる** |
| B-9 | PlayMode の `[TearDown]` が `DestroyManagerIfPresent()` の**前**に `SetStateForTests(Uninitialized)` を実行するため、`OnDestroy` の shutdown 分岐（`:536-548`）が**どのテストでも一度も走らない** | `:536-548` を丸ごと削除しても通る。Editor の `TryShutdownCore` は `#else` で無条件に `completed = true` を返すので、この 1 行を外すだけで観点が回復する |
| B-13 | `s_historyEventsEnabled` は本番の分岐条件として読まれない死にフィールド。2 件のテストはこれしか観測していない | `:1604-1606` の三項を無条件 delegate 渡しに変えても通る。`SetHistoryEventsEnabled(false)` が監視を止めなくなる |
| B-15 | **非同期完了に `pError != 0` を注入するテストが 1 件もない**（`InjectCompletionForTests` の 6 回の呼び出しはすべて第 2 引数 0） | `DeliverClaimed` の失敗分岐（`:2094` / `:2115` / `:2136`）を `if (false)` にしても通る。受付後の `AccessDenied`(9) / `HistoryDisabled`(10) / `ItemDeleted`(11) / `NotForeground`(17) が**すべて `ResultParseFailed`(1007) に化ける** |

### 網羅系の不足

| # | 内容 |
|---|---|
| B-8 | 予約失敗時に**キャッシュ**が保持されることを誰も確認していない。`Reservation_OnFailure_*` は provider 名の集合しか見ない。`:1840` に `s_renderCache.Clear()` を足しても通る |
| B-10 | `ReadRaw`（`:1337-1408`）が構造上テスト不能（本体全部が内側ガードの中）。再試行上限・`required * 2` のバイト数計算が未検証。`* 2` を消しても通り、`PtrToStringUni` がバッファ外を読む。**設計 9.1 が層 1 の検証項目として明記している** |
| B-11 | `None → Unknown` 昇格が 8 結果型中 1 型でしか検証されていない。`WindowsClipboardTextResult.cs:54-55` の昇格を削っても通り、`IsSuccess == false` かつ `ErrorMessage == null` が作れる |
| B-12 | **前回レビュー v1 自身の修正に回帰テストが無い**。`Reset()` に `_nextTicket = 0;` を足しても（H4 の欠陥が復活）、`RegisterAwaitingNative:83-86` の掃除を消しても（M5）、`IssueTicket` の `do/while` を `return ++_nextTicket;` にしても（L1）、全テストが通る |
| B-14 | `RequestRejected`(1008) の防御分岐（`:2003-2005`）が未検証。削除すると `code == None` のまま**成功結果**が配送される |
| B-16 | `AcceptRequestsWithIdForTests` が単一 id 固定で、設計 7.6.4 が「同時実行許可」と定める読み取り系 2 種の**受付後**の並行を検証できない。既存の並行テストは 2 本とも受付前拒否で終わっている |
| B-17 | 孤立サロゲート（`"a\uD83Db"`）が未検証。`AppendEscaped` は 0x20 以上を素通しするため不正な UTF-16 がネイティブへ渡る |
| B-18 | `TryParseAvailability` の必須キー検査が生文字列の部分一致（`:201-210`）。`{"note":"historyEnabled roamingEnabled"}` は形状検査を通過し「両方 false の成功」になる |
| B-19 | `TryClaim_AlsoReleasesTheNativeIdMapping` が ticket 1 つのケースしか置かず、A-4 の状況を仕様として固定している |
| B-20 | 巨大サイズ境界が未検証。`:1357` は `required > 0x7FFFFFFF` で `byteCount` が負になる |
| B-21 | `ShutdownTimeout`(1006) と予算超過分岐（`:2360-2364`）を通るテストが無い。`ClassifyShutdown_OtherCodesAreTerminal` の `[TestCase]` にも 1006 が無い |

---

## 軽微な指摘（low / C 区分 — 記述だけを変える）

| # | 箇所 | 内容 |
|---|---|---|
| C-1 | `WindowsClipboardResultTests.cs:64-77` | メッセージの**一意性**を見ていない。`FormatUnavailable` を `Empty` と同じ文言にしても通る。`HashSet` 1 行で塞げる |
| C-2 | 設計 6.3 / 結果 v4 §2.3 | 既存変更の一覧に `scripts/check_design_consistency.py` が無い（実際は 2 件、記載は 1 件） |
| C-3 | `WindowsClipboardManager.cs:116-128` | ログ規約の逸脱注記がクラス doc に無い。設計 DoD が「逸脱がファイル冒頭に明記されている」を完了条件にしており、結果 v4 §6 はこれを○としているが実装と一致しない。**ログ出力自体は適合**（後述） |
| C-4 | `WindowsClipboardManager.cs:125-126` | クラス doc が「現在はライフサイクルのみ。操作は後続ステップで追加」とステップ 1 時点のまま |
| C-5 | 設計 6.5 / `:634-636` | 「1 ファイル 1 主型、例外は `WindowsClipboardPayloads.cs` のみ」が実態と違う（実際は 4 ファイル）。`common.md` が課すのは「ファイル名と主型名の一致」までなのでルール違反ではない |
| C-6 | 設計 7.11 の表 | `[MonoPInvokeCallback]` 実体と `static readonly` delegate を内側ガードに置くと規定するが、実装は 6 本中 5 本が外側。**P5 の目的（他プラットフォームの Player ビルドに入らない）は達成済みで振る舞いへの影響は無い**。コード側 `:2847-2848` に理由も書かれている。設計か結果 §5 のどちらかを実態に合わせる |
| C-7 | 結果 v4 §5 #8 | 「名前空間直下の `internal enum` 5 種」は実際は 6 種（`RequestTable` を含めると 7 種） |
| C-8 | 結果 v4 §3 | 「Editor では `PlatformUnavailable` が返る」は事前チェック経路については誤り（A-10） |
| C-9 | 設計 7.4 段階 3 | 「delegate の GC ルートを解放する」は実装されておらず、**解放しない方が安全**（`ShutdownFailed` でネイティブが保持し続けるケースで壊れない）。設計から落とすのが妥当 |
| C-10 | `RequestTable.cs:36-37` / `:110-120` | 設計 7.6.1 は「native requestId は `AwaitingNative` の間だけ副キー」と書くが、実装は `MarkUndelivered` 後も保持する（重複完了の吸収にはこちらが正しい）。この食い違いが A-4 の根本原因 |
| C-11 | `RequestTable.cs:113-119` | `MarkUndelivered` の `<returns>` が「already delivered and claimed のとき false」とあるが、実際はエントリ不在全般で false |
| C-12 | `:2586` / `:2944` | dispatcher が null のとき通知イベント側は無言で return。`Dispatch:2571` は `LogError` を残しており不揃い |
| C-13 | `:2525` / `:2530` | `InvokeInOrder` の XML が invocation list 単位の隔離を謳うが実態と違う（A-14 と対） |
| C-14 | `JsonParser.cs:83` / `HistoryItem.cs:56` | `string[]` をそのまま `IReadOnlyList<string>` として公開しており、キャストで書き換えられる |
| C-15 | `:482-483` | `SetRenderProvidersForTests` が `s_renderCache` をクリアしない。本番の世代差し替え 3 経路は必ずクリアを伴うため、本番に無い状態を作れる |

---

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: **○**
- 変更ファイル一覧との一致: **△**（新規 Runtime 15 / テスト 7 は過不足 0 で完全一致。既存変更のみ 1 件欠落 = C-2）
- テスト方針の網羅性: **×**（設計 9.1 は 14 観点中 13。**設計 9.2 は 21 観点中 7**）
- エラーケース全実装: **△**（1000〜1011 と 0〜19 は実装済み。ただし A-10 / A-11 で返るコードが設計と違う経路がある）
- 返却仕様との整合: **△**（`IsSuccess` / `ErrorMessage` の不変条件は全結果型で実装済み。検証は 8 型中 1 型 = B-11）

## プロジェクトルール適合チェック

- `common.md` 準拠: **○**
- `csharp.md` 準拠: **○**
- Bridge 実装品質（スレッド安全性・メモリ管理）: **△**（メモリ管理は問題なし。スレッド安全性に A-5 のデッドロック）
- 既存 API 互換性: **○**（破壊的変更なし。他プラットフォームのファイル変更 0 件）

### P1〜P5 の機械確認結果

| 観点 | 結果 |
|---|---|
| P1 | 追加 `.cs` 22 件のファイル名 22/22、public/internal 型 25 件すべてに `Windows` 接頭辞。接頭辞なしは全て `private` の入れ子型 |
| P1b | テスト 7 ファイルとも `#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` で対象型のガードと一致。他プラットフォームの型参照 0 件 |
| P2 | 差分に `Android*` / `Ios*` / `Mac*` の追加・変更 **0 件** |
| P3 | `UnityMainThreadDispatcher` は実利用箇所で 4 プラットフォーム × 4 機能の真の共有型と確認。本ブランチでの変更 0 |
| P4 | `Runtime/Common/` への差分 **0 件** |
| P5 | `[DllImport]` **30 件すべて**が内側ガード内。ネイティブ呼び出し 30 か所も同様。外側ガードだけで守られている呼び出しは 0 件 |
| 既知の逸脱 11 件 | 前例としての引用なし。逆に「使わない対象」として正しく参照 |

`artifact/OS_PREFIX_VIOLATIONS.md` への新規追加は不要。

### ログ規則

`Debug.Log*` 全 78 件を 1 件ずつ確認。**クリップボードの内容（テキスト本文・ファイルパス・バイト列）の出力は 0 件**。出るのは `length` / `count` / `size` / `formatName` / `operation` / `ErrorCode`(enum) のみ。`ErrorMessage` はどこでもログに出していない。`pending.Json`（ネイティブ生 payload）が `Failure(..., detail)` に渡る 3 か所も、`ToMessage` が `detail` を使うのは `InvalidArgument` のときだけで、ネイティブコード経路では捨てられる。**漏洩経路なし。**

---

## テストカバレッジ

### カバーできている観点

- 純粋層（結果型 / payload / JSON / RequestTable）: 前回 v1 の H1 修正が有効に機能。`StringAssert.DoesNotStartWith("Unknown error (")` による catch-all 落ちの検出と、catch-all の存在自体を固定する「ガードのガード」が両方ある
- トートロジー・`IsNotNull` だけ・ループ 0 回・握りつぶし: **該当なし**。ビット値はリテラル固定、2 フィールドは非対称な値で取り違えを検出できる形になっている
- 分類器（`ClassifyFirstRead` / `ClassifySecondRead` / `ClassifyShutdown` / `InvokeInOrder` / `ApplyReservationOutcome` / `RenderDeferredFormat`）: `testing.md` 層 1 の「`internal static` の純粋関数として切り出して検証する」規約に沿った正しい形
- テスト順序への依存: **なし**。両テストクラスの `[TearDown]` が `ResetForTests` を必ず呼び、`ResetCore` が全静的状態を戻す（静的フィールドを全列挙して確認）
- exactly-once の主要 4 経路、in-flight ガード、重複 Manager、`ResetForTests` による分離

### 不足している観点

**設計 9.2（PlayMode 21 観点）のうち 14 が未検証。** 特に、`Initialize` / `TryShutdown` / `ShutdownWithDrain` / `CancelRequest` / `ReserveDeferredFormats` / `RecoverDeferredState` の **6 つの public API がどのテストからも一度も呼ばれていない**。

設計 8.1 のネイティブコード 9 / 10 / 11 / 16 / 17 / 18 はどのテストにも登場しない。DoD 11 が名指しする H1 / H2 / H3 / M1 の回帰テストのうち、**M1 が欠落**。

---

## 総合評価

**要修正（重大）**

A 区分 15 種。ステップ 1 のみを対象にした前回 v1 では A 区分 0 件だったが、Manager 3,189 行を対象に加えた結果、契約違反・無言の失敗・恒久ハング・アプリ終了不能が出た。

---

## サーキットブレーカー

workflow の「同じ種類の指摘が 3 回続く」に**該当する**。

shutdown / ライフサイクルの状態機械に関する A1 級指摘は、設計レビュー v3・v4・v5 で 3 ラウンド続き、v4 でサーキットブレーカーを適用して「全 shutdown 起点を単一の終端関数と遷移表へ集約」した。今回それが**実装レビューで 4 ラウンド目**として再発している（A-1 / A-2 / A-3 / A-8 / A-9 の 5 種）。

個別に直すべきではない。**それを生む仕組みは 2 つある。**

### 仕組み 1: 集約したのは「起点」であって「試行」ではなかった

設計 7.4 は「1 回の試行が終わるたびに、起点を問わず必ず通す」と書いている。実装は起点（`TryShutdown` / `OnDestroy` / `Drain` / `Quit`）を集約したが、`DrainRoutine` の**ループ内の中間試行**は集約の外に残った（A-1）。同じ形で、`OnDestroy` は状態で分岐して終端関数の呼び出し自体をスキップし（A-3）、`Initialize` は独自のチェック順序を持ち（A-8）、`OnWantsToQuit` は独自のフラグ操作を持つ（A-9）。

**対処案**: `FinishShutdownAttempt` を `TryShutdownCore` の内側に取り込み、「試行したら必ず終端処理が走る」を呼び出し側の規律ではなく**型で強制**する。状態判定による呼び出しスキップを許さない。

### 仕組み 2: テストシームが状態遷移そのものを飛ばしている

`SetStateForTests` は `Uninitialized → Running` の遷移を実行せずに `Running` を作れる。その結果、`Initialize` は一度も呼ばれず（B-3）、`OnDestroy` の shutdown 分岐は `[TearDown]` が能動的に潰し（B-9）、quit 経路は `InjectShutdownResultForTests` の origin 固定で構造的に到達不能（B-2）。

**A-1 / A-3 / A-8 / A-9 はすべて「状態遷移を実際に駆動していれば落ちたはず」の欠陥である。** 個別のテストを足すのではなく、シームを直すのが正しい。

**対処案**: (a) `InjectShutdownResultForTests` に origin 引数を足す、(b) `SetMainThreadIdForTests` を足す（B-1 の 6 か所のガードが一度に守られる）、(c) PlayMode `[TearDown]` の `SetStateForTests(Uninitialized)` を外す（Editor の `TryShutdownCore` は無条件に完了を返すので安全）、(d) 設計 9.2 が要求済みの `InvokeRenderDuringReserveForTests` を実装する。

### ミューテーション検証の限界について

前回、11 個の変異がすべて検出されたことを「テストが有効である証拠」として記録した。これは**変異を注入した箇所についてのみ正しい**。今回のレビューは、変異を注入しなかった領域（ライフサイクル遷移・quit・キャンセル・メインスレッドガード）に穴が集中していることを示した。

自分で選んだ箇所に自分で変異を入れる限り、**変異の選定自体が自分の盲点を継承する**。この点は結果ファイルに記録する。

---

## 止める基準の判定

| 条件 | 判定 |
|---|---|
| A が 0 になった | **×**（15 種） |
| レビュアーを替えて 1 回通した | ○（6 名の独立レビュー。うち 3 名が独立に A-2 へ到達） |
| 追加したテストが壊すと落ちることを確認済み | **×**（B 区分 21 種のうち 10 種が中心契約の未検証） |
| 直さない残件が結果ファイルに理由つきで明記されている | 未（本レビュー反映後に更新が要る） |

**続行不可。修正して再レビューが必要。**

## 修正の推奨順序

1. **仕組み 1 の修正**（A-1 / A-3 / A-8 / A-9 をまとめて解消）— 終端処理を `TryShutdownCore` に取り込む
2. **仕組み 2 の修正**（シーム 4 点）— これ無しでは 1 の修正を検証できない
3. **A-2**（`PartialState` の回復）— 3 名が独立に到達した最優先の実バグ。遅延レンダリングの最終防衛線
4. **A-5 / A-6 / A-7**（キャンセルと例外経路）— B-4 のテストと同時に
5. **A-4**（native id の所有者確認）— 1 行の条件追加 + B-19 のテスト
6. **B-15**（失敗完了の注入）— 1 行で 8.2 の履歴系 6 コードが一斉に守られる、費用対効果が最大
7. **A-10 / A-11 / A-12 / A-13**、残りの B、C

A-14（購読者の隔離）は 4 プラットフォーム共通の課題なので、本スライスの修正対象から外し、横断課題として別に起票することを推奨する。

## 棄却した指摘

- **遅延レンダリングのフォーマット名を大文字小文字非区別で引くべき**: ネイティブ `ClipboardManager::ReserveDeferredFormats` は `MakeDeferredRenderer(provider, context, name)` で **C# が JSON で送った文字列そのものを捕捉**して callback に渡す（`GetClipboardFormatName` によるアトムからの復元ではない）。大文字小文字の不一致は起こらない
- **文字列 API の `0 + BUFFER_TOO_SMALL` を `Unknown` にすべき（A 区分として）**: 設計 7.5 との差分は事実だが、ネイティブ `WriteStringToBuffer` は `needed = size() + 1 >= 1` を返すため**現行 DLL では到達不能**。振る舞いは変わらないので C 区分が妥当。将来の DLL 差し替えに備えて設計かコードのどちらかを揃えるべき点として記録する
- **`ReadRaw` の 2 回目のサイズが確保長を超え得る**: ネイティブは `bufferSize >= needed` のときだけ `NONE` を返し、文字列は `wcscpy_s` で必ず NUL 終端されるため、バッファ外読み出しは現行 DLL では起きない。依存がネイティブ側の不変条件であることをコメントに残すことを推奨（C 区分）

## 検証の記録

- P/Invoke 27 件を設計 2.1 と `native-toolkit` の実ヘッダ双方に突き合わせ、**27/27 一致**。`CallingConvention.Cdecl` / `ExactSpelling` も全件。`ole32.dll` の 3 件が `CallingConvention` 未指定なのは正しい（`__stdcall`）
- `Marshal.AllocHGlobal` 1 か所 / `Marshal.FreeHGlobal` 1 か所（`finally` 内）で 1:1。`for` ループの内側に `try/finally` があるため再試行でも解放が走る。**リーク・二重解放なし**
- COM の二重解放なし（`s_comOwnership == None` の早期 return が全経路を吸収）。`completed == false` での解放もなし
- `ResetCore` の静的状態リセット漏れなし（静的フィールドを全列挙して照合）
- 遅延レンダリング D-1〜D-9 を 1 件ずつ突き合わせ、**9 件とも実装に反映済み**
- `[MonoPInvokeCallback]` 6 メソッドはすべて `static`、delegate はすべて `static readonly` で GC ルート確保済み。ジェネリック経由の P/Invoke 0 件、非 blittable の受け渡し 0 件
- `python scripts/check_design_consistency.py` は FAIL 0 で完走。ただし同スクリプトは新規ファイルの件数照合であり、C-2 / C-5 のような記述の不一致は検出範囲外
