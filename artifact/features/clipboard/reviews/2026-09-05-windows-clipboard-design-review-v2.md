# レビュー結果

- 日付: 2026-09-05
- 対象ファイル: `artifact/features/clipboard/designs/2026-09-05-windows-clipboard-design-v2.md`
- 機能名: clipboard
- プラットフォーム: Windows
- 結果: 要修正 — 高優先度 3 件、中優先度 4 件、低優先度 1 件
- レビュー方法: リポジトリ規約、前回レビュー、既存 dispatcher、Clipboard の既存パターン、ローカルのネイティブ Clipboard ソースと照合した静的レビュー。Player 実行および DLL バイナリの同一性検証は未実施。V-1 / V-2 / V-3 は引き続き未検証

## 強み

- 27 API の対応関係、Windows の P/Invoke 方針、Manager の境界、コンパイルガード、AOT 対応の static callback、callback 文字列の寿命が明示されている
- v2 では、ネイティブに存在しない遅延配置の options 引数が削除され、予約時の破壊的変更、二相呼び出しの厳密なサイズ契約、ネイティブ shutdown 中の provider 寿命が記載された
- Runtime とテストのファイル分離、明示的な初期化、エラー結果型、Definition of Done が実装の有用な基準になっている
- STA とメッセージポンプの実現可能性を、実装初期に確認すべき Player 検証ゲートとして適切に扱っている

## 改善点

### 高優先度

1. **H1 — 必要サイズ 0 を空として解釈する前に、エラーを保持する（7.5、642 行付近 / 9.1）**
   ネイティブの `WindowsClipboardManager.cpp` にある `PasteFiles`、`PasteImage`、`PasteCustomFormat` は、lease 取得または内部の読み出しに失敗した場合も 0 を返す。このため現在の規則では、戻り値 0 と `Busy`、`InvalidData`、`NotInitialized` の組み合わせが、空データの成功に変換される。これは review v1 の提案自体が誤っており、確定済みのネイティブ契約として扱わず修正する必要がある。先にエラーを分類し、明示的に対応する空・形式未提供のみを空の成功へ正規化する。バイト API では 0 + `BufferTooSmall` を許容し、それ以外の失敗は保持する。2 回目の呼び出しでも、アンマネージドメモリを読む前にエラーを確認する。各関連エラーと正当な 0 バイトデータの組み合わせをテストする

2. **H2 — teardown の対象に「完了済み・未配送」の結果を含める（7.6.1、675 行付近 / 7.6.3、691～696 行付近）**
   `OnRequestCompleted` は dispatcher へ配送を enqueue する前にリクエスト表からエントリを削除する一方、teardown はリクエスト表に残ったエントリだけを drain する。ネイティブ完了が破棄または quit の直前に届くと、表にはエントリがなく、その後の dispatcher の `Update` も保証されないため、callback と Awaitable が未完了のまま残り得る。実際に配送されるまで未配送完了レコードを保持するか、別の配送レジストリを Manager が所有する。通常配送と teardown の双方が、同じ完了を exactly-once で取得する構造にし、同期 teardown 後に残った enqueue 済み action は無害にする。ネイティブ requestId を持たず、完了だけが enqueue される受付前拒否の Awaitable も対象に含める。「完了 → enqueue → Update 前に teardown → 後から Update」をテストし、未完了と二重配送が起きないことを確認する

3. **H3 — 予約の置き換えに失敗した場合、旧 provider 世代を保持する（7.7、765 行付近）**
   新しい予約を試すたびに provider と cache を先に消すと、ネイティブが `EmptyClipboard` より前に `Busy` を返した場合でも旧世代が失われる。`WindowsClipboardCore.cpp` の `DeferredClipboard::Reserve` は、クリップボードを開いてから `renderers_` を置き換えるため、早期失敗時にはネイティブ側の旧 renderer が残る。その renderer が単一の managed static delegate を呼ぶと、対応する provider が無いか、別世代の provider を参照する。新しい辞書を一時領域に準備し、変更前の失敗では旧世代を維持する。成功と部分変更を明確に分け、許可された全 callback タイミングで選択された世代が利用可能であることを保証する。「予約 A 成功 → 予約 B 失敗 → A をレンダリング」をテストする

### 中優先度

1. **M1 — キャンセルの実行スレッドと登録寿命を定義する（7.6.5、718 行付近）**
   `CancellationToken` の callback は Unity メインスレッドで実行されるとは限らない。その callback からメインスレッド限定の `CancelRequest` を直接呼ぶと `MainThreadRequired` になり、ネイティブ要求が継続する可能性がある。キャンセルをキャッシュ済み dispatcher 経由でメインスレッドへ送り、ネイティブキャンセル前に request identity または generation を確認する。すでにキャンセル済みの token の扱いを定義し、完了・受付拒否・teardown の全経路で `CancellationTokenRegistration` を破棄する。ワーカースレッドからのキャンセルと、完了との競合をテストする。参考: [Cancellation in Managed Threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)、[CancellationToken.Register](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken.register)（2026-09-05 確認）

2. **M2 — この層が所有する COM 初期化を対応する終了処理と釣り合わせる（7.3、582 行付近）**
   `CoUninitialize` を常に呼ばない方針では、この層が fallback として成功させた COM 初期化が、後続のネイティブ初期化に失敗した場合も含めて残る。この層による `CoInitializeEx` 成功を記録し、ネイティブの cleanup 後に、同一スレッド上で、この層が所有する参照だけを解放する。shutdown のタイムアウトまたは cleanup 失敗時の扱いも明示する。`CoGetApartmentType` で既存状態を観測しただけの場合は解放しない。プロセス寿命まで保持する判断を採る場合は、その方針と理由を明記する。Microsoft は `S_FALSE` を含む成功した初期化呼び出しについて対応する終了処理を要求している: [CoInitialize のドキュメント](https://learn.microsoft.com/en-us/windows/win32/api/objbase/nf-objbase-coinitialize)（2026-09-05 確認）

3. **M3 — 既存 dispatcher は「次フレーム」の配送を保証しない（7.1、555 行付近 / 9.2、911 行付近）**
   `UnityMainThreadDispatcher.Update` は、処理中の購読者が新たに enqueue した action も含め、queue を `while` で空になるまで実行する。dispatcher callback 内から Clipboard API を呼ぶと、同じ `Update` の後半で結果が配送される可能性がある。また dispatcher の `Update` より前に API を呼んだ場合も、同一フレームで配送され得る。実際の保証を「呼び出し元 API またはネイティブ callback のスタック外で queue 配送される」と定義するか、厳密な次フレーム配送が必要なら Manager 固有のフレーム staging を追加する。XML コメントとテストを同じ契約に揃える。前者を選ぶ場合、`Runtime/Common` は変更不要

4. **M4 — flag / lifecycle API の結果配送契約を完成させる（5.3 / 7.1）**
   `CanShutdownNow` は `WindowsClipboardFlagResult` を返すが、宣言された 7 種の event のどれもこの型を受け取らない。また、一律の callback 方針を掲げる一方、`TryShutdown`、`CanShutdownNow`、`CancelRequest` には per-call callback がない。明示的な event 対応、または例外扱いとその理由を定義する。`TryShutdown` については未完了中の結果を定義し、進行状況と最終完了を分離する。`completed == false` のエラーをすべて抑制する規則によって、本当の wrong-thread 失敗などを隠さないようにする。公開 API 対応表と event テストを同時に更新する

### 低優先度

1. **L1 — API 別エラーコード表を修正する（8.2）**
   `ReserveDeferredFormats` の表には `Unknown`（19）がないが、`DeferredClipboard::Reserve` は `EmptyClipboard` 失敗時、および配置失敗後の rollback が成功した場合にこれを返す。また、「`Busy` は全同期 API で発生し得る」という記述を削除する。提示されている `HasFormat` の実装はクリップボードを開かずに availability を確認する。review v1 の広すぎる記述を引き継がず、通常分岐と `SafeBridgeCall` の例外マッピングから表を導出する

## 不足項目

- H1～H3 と M1 の回帰テスト。特に完了済み・未配送、および受付拒否された Awaitable の teardown を含める。Editor ではネイティブ要求が拒否されるため、受付済み要求、完了通知、ネイティブ予約失敗を注入できる internal seam を定義する。PlayMode で Manager を生成するだけでは、これらの状態を検証できない
- shutdown の明示的な状態遷移。同期 drain がユーザー callback を呼ぶ前に新規操作を拒否し、適切な時点で初期化状態を解除し、shutdown 未完了時にはネイティブ所有リソースを保持する。重複 Manager の破棄が、稼働中の singleton を shutdown しないことも定義する
- Reload Domain 無効時の `_instance` 再構築契約。7.10 は `Awake` が再実行されない可能性を認識しつつ `_instance` をリセットするが、dispatcher と thread の再捕捉だけでは生存中の Manager に再接続できない。tombstone は GameObject 作成前に getter で確認できるため、「getter 自体はガードできない」という記述は、スレッド制約と単純な lifecycle 状態確認を混同している

## 総合評価

v2 ではネイティブ ABI の文書化と実装計画が大きく改善されたが、現状のまま実装へ進める状態ではない。必要サイズ 0 の規則が実際のエラーを隠すこと、新設した teardown 契約でも queue 済み完了を失うこと、予約の置き換えによって有効な provider 世代が無効化され得ることが主要な問題である。この 3 点を修正し、キャンセル、COM 所有権、配送タイミング、lifecycle 結果の契約を揃えてから実装することを推奨する。

V-1 / V-2 / V-3 は実際の Windows Player における検証ゲートとして維持する。この静的レビューでは成功を確認していない。

元の設計書と前回レビューは変更していない。改善版は、レビュー手順 Step 8 のユーザー選択後に別バージョンとして保存する。
