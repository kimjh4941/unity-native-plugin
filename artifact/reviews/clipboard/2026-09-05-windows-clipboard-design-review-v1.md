# レビュー結果

- 日付: 2026-09-05
- 対象ファイル: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v1.md`
- 機能名: clipboard
- プラットフォーム: Windows
- レビュー方式: サブエージェント 2 体による分担レビュー（領域A: ネイティブ契約整合性 / 領域B: リポジトリ規約準拠・実装可能性）。指摘は各正本での裏取り済み

---

## 強み

- **API 網羅性が完全**。`WindowsLibrary.def` の Clipboard export と 2.1 の表が 1:1 で一致し、取りこぼしも存在しない API の発明もない。同梱 DLL のバイナリ走査でも全シンボルの存在を確認
- **スレッド分類がネイティブ実装と一致**。owner-UI 限定（`setClipboardHistoryCallbacks` / `uninit` / `reserveDeferredFormats` / `recoverDeferredState`）は `AcquireOwnerContext` の `ownerThreadId_` 比較、任意スレッド可は `AcquireSyncLease` / `AcquireHistoryCoordinator` にスレッド判定が無いことで裏取り済み
- **受付契約の理解が正確**。`Accept` は cb null / dispatchHwnd 無し / lifecycle クローズ / `PostMessage` 失敗のいずれでも id 0 を返し table に載せない（コールバック 0 回）、受付後は `Complete` が 1 回だけ取り出す。8.3 の不変条件は正しい
- **`json` の寿命対策が妥当**。`Complete` は `cb(...)` 直後に `std::wstring` を破棄するため「コールバック中のみ有効」は事実。`[MarshalAs(LPWStr)] string?` で CLR がコピーする判断は正しく、逆 P/Invoke の in 引数は CLR が解放しないためリークもない
- **バッファ 2 回呼び出しの単位が正しい**。`WriteStringToBuffer` は wchar_t 数（終端込み）、`WriteBytesToBuffer` はバイト数
- **層分離と Singleton・GC ルート・IL2CPP 制約の記述が既存前例と一致**。`InvokeInOrder` を `internal static` に切り出す方針は `AndroidClipboardManager` の前例と完全一致
- **JsonBuilder は `public static` / JsonParser は `internal static` の可視性、`JsonUtility` + DTO 方式、`IosClipboardJsonReader` を共有化しない判断**がいずれも既存規約・ガード条件と整合
- **既存 `WindowsNotificationManager` の 2 欠陥（非 Windows での無言 return、存在しない `-debug` DLL 名）を特定し是正する判断が妥当**。どちらも現物で裏取り済み
- **変更ファイル一覧の正確性**。パス実在性、`.meta` 不記載、サンプルアプリ除外、asmdef 非変更（`AssemblyInfo.cs` に両テストアセンブリの `InternalsVisibleTo` が既存）はいずれも正しい
- **テストの命名・配置が既存規約に一致**し、「Manager インスタンス生成テストを EditMode に置かない」を 3.3 と 9.1 の両方で明記している

---

## 改善点

### 高優先度

- **[5.3 / 7.7] `ReserveDeferredFormats` の `options` 引数はネイティブに存在しない**
  ネイティブは `void reserveDeferredFormats(const wchar_t*, ClipboardRenderCallback, void*, DWORD*)`（`WindowsClipboardManager.h` 290-292 行）で書き込みオプションを受け取らない。5.1 の DllImport は正しく省いているのに 5.3 / 7.7 の公開 API 署名には `options` がある。死に引数になる。
  改善: `options` を削除し、「遅延予約に EXCLUDE_HISTORY / SENSITIVE は適用できない」ことを XML コメントに明記する

- **[7.7] 予約がクリップボードを破壊的に置き換える点が未記載**
  `DeferredClipboard::Reserve` は `EmptyClipboard()` を呼んでから `SetClipboardData(fmt, nullptr)` を並べる（`WindowsClipboardCore.cpp` 613-616 行）。既存内容は消える。
  改善: 7.7 と XML コメントに「既存のクリップボード内容は破棄される」と明記する

- **[7.7] 二相コールバックの `requiredSize` 一致要件が抜けており、黙って形式が消える**
  `MakeDeferredRenderer`（`WindowsClipboardDeferredProvider.cpp` 17-44 行）の実装契約は次のとおり。
  - 1 回目: 戻り値が `NONE` / `BUFFER_TOO_SMALL` 以外なら破棄。`queriedSize == 0` でも破棄
  - 2 回目: 戻り値が `NONE` 以外なら破棄（`BUFFER_TOO_SMALL` も破棄）。さらに `actualSize != queriedSize` なら破棄
  計画 7.7 の「2 回目に不足なら `BUFFER_TOO_SMALL`」「キャッシュが無ければその場で生成」は、長さが 1 バイトでも違えばエラーログも出ずに形式が欠落する。
  改善: 「1 回目のキャッシュを 2 回目で必ずそのまま使う（再生成禁止）」「2 回目は同じ `requiredSize` を設定し `NONE` を返す」「長さ 0 のペイロードは配置されない」を仕様として書く

- **[7.4 / 7.7] `uninitClipboardManager` の内部から provider が同期再入する**
  `Uninit` は `WindowsClipboardWindow::Destroy(dispatchHwnd_)` を呼び、`DestroyWindow` が `WM_RENDERALLFORMATS` を同期送出して managed provider に到達する。つまり `Shutdown` / `OnDestroy` / `wantsToQuit` ドレインの P/Invoke スタック内側で C# の `Func<byte[]>` が走る。
  改善: (1) キャッシュクリアは `uninit` が TRUE を返した後に限定、(2) provider は Unity オブジェクトに依存しない値をクロージャに閉じ込める規約を明記、(3) 7.4 のリトライループが provider 再入中であり得ることを明記

- **[7.6 / 5.3] `Awaitable` 併設の前提条件を半分しか満たしていない**
  「requestId ベースなので in-flight ガード不要」は、common.md が名指しする *last-registered wins による callback 取りこぼし* に関しては妥当。しかし前提条件のもう一方「`AwaitableCompletionSource` が `TrySetResult` されないまま破棄されないこと」を満たしていない。完了配送は `UnityMainThreadDispatcher.Enqueue` 経由であり、`OnDestroy` 後や Quit 確定後は `Update` が回らずキューが流れないため pending の `Awaitable` が永久未完了になる。iOS v5 設計書 5.7 が `Awaitable` 併設を見送った理由と同一。
  改善: (a) 破棄・終了時に全 completion source を同期的に失敗完了させる lifecycle coordinator を設計に追加する、または (b) 初期実装は callback 版のみとし iOS v5 5.7 と同形の「併設しない理由」節を置く

- **[7.4] `Application.wantsToQuit` の再入・購読解除・タイムアウト時の終了可否が未定義**
  `Application.Quit()` を再実行すると `wantsToQuit` が再度発火するため「ドレイン完了フラグを立てて 2 回目は true を返す」処理が必須。無いと終了しない。`ShutdownTimeout` に達した場合に quit を通すかも未定義。static event のため Singleton 再生成・ドメインリロードで多重購読が残る（`OnDestroy` での解除が未記載）

- **[7.4 / 7.6 / 9.2] static 状態のリセット手段と破棄後再生成の扱いが欠落**
  `s_pending` / `s_renderCache` / `_initialized` / delegate 群はすべて static。前例の `IosClipboardManager.cs:424` は `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] ResetStaticState()` と `#if UNITY_EDITOR internal static ResetForTests()` を単一の `ResetCore` で共有しており、`AssemblyInfo.cs` のコメントも「seam が無いと 1 つの destroy テストが以降すべてを拒否させる」と明記している。計画は `ManagerDestroyed`(1003) を定義する一方、tombstone の保持先とリセット手段が未定義で、9.2 の PlayMode テストが実行順序依存で壊れる

- **[4 章 vs 5.3] `Shutdown` の同期→非同期変換が未宣言の規約逸脱**
  common.md は「ネイティブ API が同期なら C# も同期。Bridge 層で変換しない」を基本原則とし、計画 4 章自身がこれを固定事項に挙げている。逸脱の理由（TRUE を返すまでポンプを回す必要）は妥当だが、逸脱の宣言・根拠・代替案の比較が無い。
  改善: 同期の `bool TryShutdown(out WindowsClipboardResult)` を基本形として残し、フレーム跨ぎのドレインを別名 API とした上で、iOS v5 5.7 と同形の逸脱節を設ける

### 中優先度

- **[7.1 vs 7.2 vs 9.2] 同期 API の結果配送経路が計画内で三重に矛盾**
  7.1「戻る前に同一スレッドで発火」、7.2「dispatcher 経由で次の `Update`」、9.2「`Update` flush を跨いだ順序保証を検証」が食い違う。既存の両 Clipboard Manager は拒否結果も含め常に dispatcher 経由。インライン発火は同期 API のスタック内での再入を招く。
  改善: 全経路 dispatcher 経由に統一し「戻り値は即時、event / callback は次 `Update`」と明記する

- **[7.5 / 8.3] `IsEmpty` の判定条件が API ごとに違う**
  - `getClipboardFormats`: 空クリップボードでも `EMPTY` を返さず `"[]"`（必要サイズ 3）を返す（`ListFormats` は `CountClipboardFormats()==0` で `NONE`）
  - `pasteImage` / `pasteCustomFormat`: データ長 0 のとき戻り値 0 + `BUFFER_TOO_SMALL` になり、計画 7.5 の「必要サイズ 0 かつ `pError == NONE`」では捕まらない
  改善: 「必要サイズ 0 は `pError` に関わらず空成功」に条件を修正し、GetFormats はパース後の要素数 0 で `IsEmpty` を立てる

- **[7.4 / 8.1] `uninit` が FALSE のときの `pError` を失敗と誤判定しうる**
  `Uninit` は FALSE 時に `MONITOR_REGISTER_FAILED(12)` / `CANCELED(15)` / `BUSY(3)` を `pError` に入れるが、これは「まだ終わっていない」の意味。リトライ中の `pError` 無視ルールが無い。さらに `deferred_.IsPartial()` の回復に失敗し続けると永久に FALSE を返すため、`ShutdownTimeout` の前に `recoverDeferredState` を試す指針が要る

- **[5.3 / 7.1] `SetHistoryEventsEnabled(false)` 後に再有効化できなくなる sticky 状態**
  3 つとも nullptr にすると `StopWatch()` が走る。トークン解除に失敗すると WinRT 側に `revokePending_` が立ち、以後の `StartWatch` が `MONITOR_REGISTER_FAILED` で拒否され続ける。8.1 / 7.1 にこの状態と、C# 側は再試行しかない旨を反映する

- **[7.6] 「登録前にコールバックが走らない」根拠の説明が不正確**
  完了は `PostMessage` 経由ではなく、WinRT continuation から `cb` を直接呼ぶ。安全なのは *開始* が `PostMessage(WM_APP_CLIPBOARD_REQUEST)` 経由で、かつ呼び出し元が所有 UI スレッド自身だから。7.2 の「全 public API はメインスレッド限定」に依存した安全性であることを明記する

- **[7.6] 「同時実行しても安全か」の判定が未実施**
  common.md は非同期版追加前に OS 上で同時実行可能かの判定を要求している。クリップボードを変更する 3 操作（`RestoreHistoryItem` / `DeleteHistoryItem` / `ClearUnpinnedHistory`）は in-flight ガードを置くか、安全と判定した根拠を明記する

- **[5.3 / 7.6] `Awaitable` 版が requestId を返せず `CancelRequest` と併用不能、`CancellationToken` も無い**
  common.md は「`CancellationToken` は最終引数、MonoBehaviour からは `destroyCancellationToken`」としているが `XxxAsync` に無い

- **[8.1] `hasClipboardFormat` は失敗時も FALSE を返す**
  未初期化 / null 名で FALSE + `pError`、成功時も FALSE/TRUE + `NONE`。`WindowsClipboardFormatPresenceResult` は「存在しない」と「判定できなかった」を独立させる設計にすべき

- **[7.3] `Initialize(enableChangeEvents)` の二択の帰結が未記載**
  `onChanged != nullptr` で `ClipboardWatcher::Start` が失敗すると init 全体が `MONITOR_REGISTER_FAILED` で失敗し、`nullptr` なら失敗を握り潰して init は成功する。後者では `ClipboardChanged` が永久に発火せず、後から有効化する手段は uninit → init しかない

- **[7.3 / 8.1 / 8.2] `WrongApartment` を C# 層が返す設計がエラーコード分類を破る**
  `RPC_E_CHANGED_MODE` の時点でネイティブは一度も呼ばれていないのに、8.1 が「`pError` として返る」と定義するコード 18 を C# が捏造している。1000 番台に `ApartmentUnavailable` 等を追加するか、native を呼んで 18 を得る形にする

- **[3.2 / 4 章] 機微情報のログ規約の逸脱の書き方が前例と食い違う**
  前例は 2 種類。Manager は**ログ行を残しパラメータ値のみ伏せる**（`AndroidClipboardManager.CopyPlainText` は `textLength` / `hasLabel` / `isSensitive` / `hasCallback` を出す）。ログ行自体を省くのは Parser / 結果型のみ。計画 3.2 は両者を混ぜており、字義どおり実装すると Manager のログが丸ごと欠落する。逸脱を各ファイル冒頭に `Intentional deviation from ... in csharp.md:` として明記する運用（リポジトリ内 18 ファイルで実施済み）も未記載

- **[7.2] メインスレッド判定の実現手段と `Instance` getter の非ガード性が未記載**
  前例は `s_mainThreadId` と `s_dispatcher` を `Awake` でキャッシュし、`Instance` getter は GameObject を生成するためガードできない旨を XML に明記している。計画は `MainThreadRequired`(1002) を定義するのみで、終了処理中に getter を叩くと teardown 中に GameObject を生成することになる

- **[7.7] `providers` 辞書の保持場所とライフサイクルが未定義**
  static な render delegate から到達するには辞書自体も static 保持が必要。ユーザーの `Func<byte[]>` を static 保持することの GC ルートと、`Shutdown` / `OnDestroy` でのクリア規則が未定義

- **[5.3] 戻り値の型が不統一で `pError` を捨てる**
  `CanShutdownNow()` → `bool`、`CancelRequest()` → `bool`、履歴系 → `uint`。ネイティブはいずれも `pError` を返すのに C# 側で破棄され、他の全 API が結果型を返すこととも不揃い

- **[2.7 / 6.3] `VERSION.txt` の更新後の値が現物の書式と合わない**
  現物は `source: https://github.com/kimjh4941/native-toolkit/blob/main/dist/1.4.0/...` というフル URL。計画の更新値は URL 前半が落ちており、そのまま書き換えると書式が壊れる

- **[6.1 / 7.1] `#nullable enable`・名前空間・`using` の配置が未指定**
  既存の全ファイルは 1 行目が `#nullable enable`、3 行目が `#if`、`using` は `namespace` の内側。名前空間 `JonghyunKim.NativeToolkit.Runtime.Clipboard` の指定も無い

- **[9.2] PlayMode テストの網羅性が不足**
  7.1 で 7 種の共通 event を定義しているが、9.2 の検証は拒否経路・`Awaitable` 完了・順序のみ。残り 6 event の発火検証と、「発火しない契約」（自プロセス書き込みで `ClipboardChanged` が出ない等）の検証項目が無い

### 低優先度

- **[1 / 2.1 / 2.7] 関数数の不整合**: 1 章は「26 関数」、2.7 と 2.1 の表は 27。正は 27
- **[7.8] `bool` の既定マーシャリングの説明が誤り**: P/Invoke の `bool` 既定は 4 バイト Win32 `BOOL`。2 バイト `VARIANT_BOOL` は COM 相互運用の話。`[MarshalAs(UnmanagedType.Bool)]` の明示自体は妥当なので理由の記述のみ修正
- **[5.1] `CallingConvention` / `ExactSpelling` が未指定**: ネイティブは `__cdecl`、DllImport 既定は `Winapi`(StdCall)。x64 専用のため実害は無いが delegate だけ `Cdecl` 明示という非対称が残る。`CharSet.Unicode` + `ExactSpelling` 既定 false により `copyPlainTextW` を先に探す無駄な探索も入る
- **[5.2] `out uint requiredSize` の例外時規約**: 例外離脱時に書き戻らないため、7.8 の try/catch では「必ず `requiredSize = 0` を代入して非 `NONE` を返す」まで規定する
- **[8.2] `RequestRejected`(1008) は実際には到達不能**: 全拒否経路が非 0 の `pError` を設定する。防御的コードとしては可だが前提の記述が不正確
- **[2.2] Flag コールバックが不発火になる理由の追加**: `OnHistoryEventMessage` は非フォアグラウンド時に `NOT_FOREGROUND` でイベントを捨てる。「信頼できない」理由として追記価値あり
- **[7.3] `CoGetApartmentType` / `CoInitializeEx` の宣言が未記載**: HRESULT の受け方、`APTTYPE`（STA=0 / MTA=1 / NA=2 / MAINSTA=3）、`S_FALSE` の扱いをネイティブ側判定と同一条件で書く
- **[7.1] `public static WindowsClipboardManager Instance { get; }`** が get-only 自動プロパティに見え、common.md の遅延生成パターンと形が違う
- **[5.2] `using AOT;` の記載が無い**（`WindowsNotificationManager.cs:7` に前例）
- **[5.1] extern 名が camelCase** である旨（ネイティブ関数名をそのまま使い `EntryPoint` を省略する方針）を明記すると規約違反との誤認を避けられる

---

## 不足項目

- **API 別のエラーコード対応表**。8.1 は発生源別だが、実装時に必要なのは「どの API がどのコードを返し得るか」。特に `BUSY`(3) は全同期 API で発生し得るのに C# 側のリトライ / 通知方針が無い
- **`RecoverDeferredState` を呼ぶべき条件**。回復対象は `DeferredClipboard` の partial 状態のみで、`copyMultipleFormats` が返す `PARTIAL_STATE` は対象外である点の明記
- **`getClipboardHistoryAvailability` も `NOT_FOREGROUND` を返す**点。「設定を知りたいときは availability を呼べ」はフォアグラウンド時のみ成立する
- **同期 API の初期化前提**。全同期 API が init 前に `NOT_INITIALIZED` を返すため、C# の `NotInitializedByHost`(1004) と二重防御になる。どちらで止めるかを決める
- **`copyMultipleFormats` の `CF_TEXT` は CP_ACP 変換**（`EncodeAnsiText`）で非 ASCII が欠落し得る点
- **再入禁止の具体的境界**。`ClipboardChanged` / `HistoryChanged` の購読者から `uninit` / `setClipboardHistoryCallbacks` を呼ぶ経路の安全性の根拠（dispatcher 経由で次 `Update` に出るため安全）を明記
- **Definition of Done 節**（iOS v5 設計書は 26 項目を持つ）。implement-feature の完了判定基準が無い
- **テスト実行手順**（Unity Test Runner で EditMode / PlayMode 全 passed、既存テストが壊れていないことの確認）
- **`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` による static リセットと `ResetForTests` seam**
- **Reload Domain / Scene Reload をともに無効化した構成での挙動**（`Awake` が再実行されない構成）
- **破棄時に pending リクエストを完結させる lifecycle 契約**、または `Awaitable` を初期実装から外す判断
- **`s_dispatcher` の static キャッシュ方針**
- **`WindowsClipboardPayloads` 単体の EditMode テスト**（現状は JsonBuilder テストに混在）
- **design-sample-scene への申し送り**: 全 API が Editor で拒否されるため、サンプルシーンの確認は毎回 Player ビルドが必要になる（V-5 の帰結）

---

## 解決済み（要検証から格上げ可能）

- **V-4（`getPreferredClipboardFormat` の空時の戻り値）は正本から確定できる**。`PickPreferredFormat()` は `GetPriorityClipboardFormat` で、空なら 0 / 該当なしなら -1 を返す。どちらも `fmt > 0` が偽で `name` は空となり、`WriteStringToBuffer(L"")` は必要サイズ 1 + `BUFFER_TOO_SMALL`、2 回目は空文字 + `NONE` を返す。「サイズ 0 かつ `NONE`」は起こらず `EMPTY` も返らない。空判定は「取得文字列が空」で行う。実機検証は不要

---

## 総合評価

ネイティブ契約の整理（27 export の完全網羅、スレッド分類、受付契約、`json` 寿命、バッファ単位、delegate の型対応）と、変更ファイル一覧の正確さ、既存 `WindowsNotificationManager` の欠陥是正判断は、正本と照合しても問題がなく水準は高い。推測で API を発明した箇所も無い。

一方で high は 2 領域に集中している。第一に**遅延レンダリング**で、ネイティブ実装にしか書かれていない「二相の `requiredSize` 完全一致」「サイズ 0 は破棄」「`Reserve` が `EmptyClipboard` を呼ぶ」「`uninit` 内から provider が同期再入する」の 4 点は、計画のまま実装するとエラーを出さずに予約形式が消える。加えて `ReserveDeferredFormats(options)` はネイティブに対応引数が無く死に引数になる。

第二に**ライフサイクルと `Awaitable`** で、「requestId ベースなので in-flight ガード不要」という主張は callback 取りこぼしの回避としては妥当だが、common.md の前提条件のもう一方（破棄・終了時に completion source が捨てられないこと）を満たしていない。これは iOS v5 設計書 5.7 が `Awaitable` 併設を見送った理由そのもの。`wantsToQuit` の再入・解除、static リセットと `ResetForTests` seam、`Shutdown` の未宣言逸脱も同じ領域にある。

中優先度では、同期 API の結果配送経路が計画内で三重に矛盾している点（7.1 / 7.2 / 9.2）と、`IsEmpty` の判定条件が API ごとに異なる点が実装時の手戻り要因になる。

いずれも既存コードとネイティブ実装に明確な根拠がある領域であり、v2 で埋めれば実装着手可能な水準に達する。Definition of Done 節の追加も併せて推奨する。残る真の未検証項目は V-1 / V-2 / V-3 の 3 件（V-4 は解決済み、V-5 / V-6 は判断確定済み）。
