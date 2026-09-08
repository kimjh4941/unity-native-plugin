# Windows Clipboard 実装計画書 v8

- 作成日: 2026-09-05
- 対象機能: clipboard
- 対象プラットフォーム: Windows（Windows 11 以降）
- 対象リポジトリ: unity-native-plugin（C# 層のみ）
- 前版: `2026-09-05-windows-clipboard-design-v1.md` → `-v2.md` → `-v3.md` → `-v4.md` → `-v5.md` → `-v6.md` → `-v7.md`
- レビュー: `artifact/reviews/clipboard/2026-09-05-windows-clipboard-design-review-v1.md`（high 8 / medium 16 / low 10 / 不足 14 を v2 で反映）
- レビュー: `artifact/reviews/clipboard/2026-09-05-windows-clipboard-design-review-v2.md`（high 3 / medium 4 / low 1 / 不足 3 を v3 で反映）
- **実機検証**: `artifact/results/clipboard/2026-09-05-windows-clipboard-spike-verification-result-v1.md`（V-1 / V-2 合格、F-1〜F-3 を v4 で反映）
- **最新ルール**: `agent-rules/coding-rules/common.md`「命名: OS 接頭辞と、共通ファイルを作らない方針」（feature/UNT-10 で追加、develop 取り込み済み）。P1〜P5 の自己点検は 6.5
- レビュー: `artifact/reviews/clipboard/2026-09-05-windows-clipboard-design-review-v3.md`（A1 3 領域 + 中 3 件 + C 8 件を v6 で反映）
- レビュー: `artifact/reviews/clipboard/2026-09-05-windows-clipboard-design-review-v4.md`（A1 1 種類 + B 2 件 + C 3 件を v7 で反映。サーキットブレーカー適用）
- レビュー: `artifact/reviews/clipboard/2026-09-05-windows-clipboard-design-review-v5.md`（**別モデルによる独立レビュー**。A1 1 種類 + B 2 件 + C 3 件を本版で反映）
- **ネイティブ側の実機検証**: `native-toolkit/artifact/results/clipboard/2026-08-20-windows-clipboard-implement-sample-app-result-v1.md`（F1〜F3 / O1 / O2 / 未検証 9.1〜9.9 を本版で反映）
- 対象外: サンプルアプリ（ExampleController / UXML / USS / サンプルシーン）。`design-sample-scene` で別途設計する

## v7 からの主な変更（review-v5 の反映）

| # | 変更 | 区分 |
|---|---|---|
| 1 | **`Running` 中の `Initialize` は COM / ネイティブ処理より前に managed 側で冪等成功を返す**。`s_comOwnership` と `enableChangeEvents` を変更しない（7.3） | A1 |
| 2 | `s_comOwnership` は **`None` のときだけ書き換える**不変条件を明記（7.3） | A1 |
| 3 | `Application.Quit()` を差し替え可能な seam にし、再開回数を検証できるようにした（9.2） | B |
| 4 | 機械照合に**新規作成一覧の件数チェック**を追加（OS・章番号非依存）。分割後の件数ドリフトを検出できるようにした（`scripts/check_design_consistency.py`） | B |
| 5 | `FinishShutdownAttempt` の段階 6 を **`Drain` 起点のみの配送**に統一（`TryShutdown` は event / callback を発火しない契約と整合）（7.4） | C |
| 6 | 7.9 の実装順序に残っていた「結果型 3 ファイル」を分割後の構成へ更新 | C |
| 7 | 「原則 1 ファイル 1 主型。`WindowsClipboardPayloads.cs` は既存前例に基づく明示例外」と統一（6.1 / 6.5） | C |

## v6 からの主な変更（review-v4 の反映。サーキットブレーカー適用）

shutdown ライフサイクルの指摘が 3 版続いたため、**個別経路への条件追加をやめ、全 shutdown 起点を 1 つの終端関数と
1 つの遷移表へ集約**した（7.4）。

| # | 変更 | 区分 |
|---|---|---|
| 1 | **`FinishShutdownAttempt(origin, result, completed)` を新設**し、状態・managed registry・native gate・COM / provider 所有権・quit フラグを 1 表で決める（7.4） | A1 |
| 2 | **最初の shutdown 試行（起点を問わず）でレジストリを同期ドレインする**。public `TryShutdown` が即完了しても exactly-once が崩れない（7.4 / 7.6.3） | A1 |
| 3 | **`Initialize` は `ShutDown` / `Uninitialized` からのみ許可**。`ShutdownFailed` からは shutdown を完了させてからでないと再初期化できない（native gate が閉じたまま `Running` になるのを防ぐ）（7.3 / 7.4） | A1 |
| 4 | **quit 起点は成功・タイムアウト・終端失敗のすべてで quit を再開する**（終了できなくなる経路を無くす）（7.4） | A1 |
| 5 | 機械照合のファイル一覧チェックを OS・章番号非依存に一般化し、Windows 設計でも実行されるようにした（`scripts/check_design_consistency.py`） | B |
| 6 | 上記の一般化で露見した**ファイル名と型名の不一致を解消**（`WindowsClipboardReadResults.cs` / `WindowsClipboardHistoryResults.cs` を型ごとに分割）（6.1） | B |
| 7 | shutdown の結合テストを 3 件追加（9.2） | B |
| 8 | C 区分 3 件（`Uninitialized` の `TryShutdown` 結果、`OnDestroy` の呼び出し先、provider 世代テストの記述）を修正 | C |

## v5 からの主な変更（review-v3 の反映）

| # | 変更 | 区分 |
|---|---|---|
| 1 | **内部 shutdown core を新設**（`TryShutdownCore`）。public API の tombstone / initialized ガードを迂回し、`TryShutdown` / `ShutdownWithDrain` / `OnDestroy` / quit ドレインが共通で使う。public `TryShutdown` を直接呼んだ場合の状態遷移も定義（7.4 / 7.5 / 7.10） | A1 |
| 2 | **予約世代の保持規則を `pError` だけで決まる形に修正**。`UNKNOWN`(19) から失敗地点は判別できないため、`NONE` 以外は旧世代を維持する（2.8 D-9 / 7.7） | A1 |
| 3 | **fallback COM 初期化後にネイティブ初期化が失敗した場合の解放経路を追加**。ownership 遷移を成功・ネイティブ失敗・Bridge 例外・shutdown まで網羅（7.3） | A1 |
| 4 | `Initialize` の冪等呼び出しで `wantsToQuit` が多重購読されないよう購読フラグを導入（7.3 / 7.4 / 9.2） | 中 |
| 5 | 予約 P/Invoke 中の render 再入に備え、**呼び出し中だけ旧＋新をマージした staging を公開**する（7.7 / 9.2） | 中 |
| 6 | **`RecoverDeferredState` の `NONE` で provider / cache を解放してはならない**ことを明記（native は非 partial でも `NONE` を返す）（7.7 / 8.2） | 不足 |
| 7 | 機械照合スクリプトの `FileNotFoundError` を修正し、本版で完走させた（`scripts/check_design_consistency.py`） | 必須条件 |
| 8 | C 区分 8 件（2.6 / 3.1 / 6.2 / 7.3 / 8.2 / 9.1 / DoD / 12 章 / 前版欄）を修正 | C |

## v4 からの主な変更（新ルール適合とネイティブ検証結果の反映）

### 新ルール適合（`common.md` の OS 接頭辞方針、review-document の P1〜P5）

| # | 変更 | 区分 |
|---|---|---|
| 1 | **ガードを二重構造に修正**（P5）。クラスは `#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`、P/Invoke 宣言とコールバック実体は `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`。Editor 側は native を呼ばないスタブ経路にする（5.1 / 5.2 / 7.1 / 7.11） | A1 |
| 2 | **テストファイルにもコンパイルガードを指定**（P1b）。`#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`（6.2 / 9.1 / 9.2） | A1 |
| 3 | 既存型を共有しない判断の根拠を、**ガードの有無ではなく方針と利用箇所**に置き換え（P3。3.2） | 記述 |
| 4 | P1〜P5 の自己点検表を 6.5 として新設 | — |

### ネイティブ側の実機検証結果の反映

| # | 変更 | 出典 |
|---|---|---|
| 5 | **`RestoreHistoryItem` は `ClipboardChanged` を発火させる**（履歴サービスが書き込むため self-write 抑止が効かない）（2.2 / 7.6.4） | O2 |
| 6 | **`GetPreferredFormat` は `HTML Format` を返さない**（候補が 4 形式固定）（2.9 / 8.2） | 機能側への報告事項 |
| 7 | Copy / Reserve / Recover が**同一 mutex で直列化**され、呼び出し側が待ち得る（7.2） | 同上 |
| 8 | 終了時ドレインについて、ネイティブのサンプルは**再試行しない**選択をした。Unity では `wantsToQuit` でフレームが進むため再試行できるが、成立しない場合の退避策を明記（7.4） | F2 |
| 9 | teardown ドレインが**ネイティブの `CANCELED` に依存しない**設計であることを根拠として明記（ネイティブ側でも未検証）（7.6.3） | 未検証 9.8 |
| 10 | 隠し dispatch HWND が Alt+Tab / タスクバーに出ないかを手動確認項目と V 項目に追加（9.3 / 10） | 未検証 9.2 |
| 11 | 手動確認手順に「スクリーンショット撮影がクリップボードを上書きする」注意を追加（9.3） | O1 |

## v3 からの主な変更（実機スパイク結果の反映）

| # | 変更 | 根拠 |
|---|---|---|
| 1 | **V-1 解決**。Unity Player メインスレッドは `APTTYPE_MAINSTA`（初期化済み STA）。`CoInitializeEx` フォールバックと COM 所有権管理を「防御的経路」に格下げ（7.3） | 実測 |
| 2 | **V-2 解決**。非同期完了・変更通知とも Unity メインスレッドに到達。`uninit` は 1 回目で TRUE（予約なし） | 実測 |
| 3 | **DLL 名は `DEVELOPMENT_BUILD` で分岐する（必須）**。v3 の「単一 DLL 名」を撤回（5.1） | F-2 |
| 4 | `Plugins/Windows` の DLL は `PreBuildProcessor` が dist から自動配置する。手動差し替えを作業項目から削除（2.7 / 6.3） | F-3 |
| 5 | **`ClipboardChanged` は 1 回の外部書き込みで複数回発火する**。購読側は冪等 / debounce が必要（2.2 / 12 章） | F-1 |
| 6 | self-write 抑止と 2 回呼び出しバッファ規約が実機で確認済みであることを明記（2.2 / 7.5） | 実測 |

## v2 からの主な変更（review-v2 の反映）

| # | 変更 | 指摘 |
|---|---|---|
| 1 | 読み出し系の空判定を「必要サイズ 0 なら空」から**エラー分類優先**に全面修正。2 回目呼び出しもバッファを読む前に `pError` を検査する | H1 |
| 2 | 完了配送レジストリ（ticket + claim）を導入し、**完了済み・未配送**および受付前拒否も teardown の対象に含める | H2 |
| 3 | 遅延予約の置き換え失敗時に**旧 provider 世代を保持**する（native は `EmptyClipboard` 前に `BUSY` で早期 return するため） | H3 |
| 4 | `CancellationToken` の実行スレッドと登録寿命を定義（dispatcher 経由・generation 確認・`CancellationTokenRegistration` の破棄） | M1 |
| 5 | 本層が獲得した COM 初期化の所有権を記録し、解放方針と理由を明記 | M2 |
| 6 | 配送契約を「次フレーム」から「**呼び出し元スタック外**」へ修正（`UnityMainThreadDispatcher.Update` は while で drain するため同一フレーム配送があり得る） | M3 |
| 7 | lifecycle / flag API にも per-call callback と event 対応を定義し、`TryShutdown` の**終端失敗と未完了**を分離 | M4 |
| 8 | 8.2 の表を修正（`ReserveDeferredFormats` に `Unknown`(19) を追加、`Busy` の過大な一般化を削除、`HasFormat` は clipboard を開かない） | L1 |
| 9 | テスト用 internal seam（受付済みリクエスト / 完了通知 / 予約失敗の注入）を新設 | 不足 |
| 10 | shutdown 状態機械（新規操作の拒否、初期化状態の解除時点、重複 Manager 破棄のガード）を定義 | 不足 |
| 11 | tombstone と `_instance` 再構築契約を iOS 前例に合わせて修正（getter は lifecycle を確認できる。スレッド制約と混同しない） | 不足 |

## v1 からの主な変更（review-v1 の反映。v2 で対応済み）

| # | 変更 | 反映した指摘 |
|---|---|---|
| 1 | `ReserveDeferredFormats` から `options` 引数を削除（ネイティブに対応引数が無い） | high |
| 2 | 遅延レンダリングのネイティブ実装契約を 2.8 として新設（`EmptyClipboard` 破壊、`requiredSize` 完全一致、サイズ 0 破棄、`uninit` からの同期再入） | high |
| 3 | teardown ドレイン契約を 7.6.3 として新設し、`Awaitable` 版併設の前提条件を満たす形に修正 | high |
| 4 | `Shutdown` を同期 `TryShutdown` + フレーム跨ぎヘルパの二本立てに変更し、逸脱理由を明記 | high |
| 5 | `wantsToQuit` の再入防止・購読解除・タイムアウト時の終了可否を定義 | high |
| 6 | static リセットと `ResetForTests` seam を 7.10 として新設 | high |
| 7 | 同期 API の結果配送を全経路 dispatcher 経由に統一 | medium |
| 8 | `IsEmpty` 判定条件を API 別に整理、`HasFormat` の成功/存在を分離 | medium |
| 9 | `WrongApartment` の捏造をやめ C# 側コード `ApartmentUnavailable`(1009) を新設 | medium |
| 10 | 更新系履歴 3 操作に in-flight ガードを追加（`OperationBusy`(1010)） | medium |
| 11 | API 別エラーコード対応表（8.2）と Definition of Done（11 章）を新設 | 不足項目 |
| 12 | ログ規約の逸脱の書き分け（Manager は値のみ伏せる / Parser は行ごと省く）を明確化 | medium |

---

## 1. 概要

native-toolkit の `WindowsLibrary.dll` はすでに Clipboard の `extern "C"` C Bridge（**27 関数**）を公開している。
本計画は、その C API を Unity C# から P/Invoke する **Bridge + Manager 層のみ**を対象とする。
ネイティブ側の再実装は行わない。

前提となる重要事実（詳細は 2 章）:

- **Clipboard 対応 DLL はビルド時に自動配置される。** `PreBuildProcessor` が `native-toolkit/dist/1.11.0/windows/windows-native-toolkit-1.2.0.dll`
  （Clipboard の 27 export を確認済み）をコピーする。手動差し替えの作業は不要（2.7）
- Clipboard は **OS の WinRT（`Windows.ApplicationModel.DataTransfer`）のみ**を使い、Windows App SDK に依存しない。Clipboard 単体利用では `initWinAppSdk` の呼び出しは不要
- ネイティブ側は **所有 UI スレッド（`initClipboardManager` を呼んだスレッド）** を採用する契約であり、そのスレッドは **STA 初期化済み** かつ **メッセージポンプを回し続ける**必要がある。
  **この条件は実機で確認済み**（2026-09-05）: Unity 6000.4.2f1 の Windows Player メインスレッドは `APTTYPE_MAINSTA`（初期化済み STA）であり、
  ネイティブの隠しウィンドウのメッセージも Player のポンプが配送する。専用 STA スレッドを設ける必要はない（10 章 V-1 / V-2）
- **DLL 名はビルド構成で変わる。** `PreBuildProcessor` が development build では `-debug` サフィックス付きで配置するため、
  C# 側は `DEVELOPMENT_BUILD` による名前分岐が必須（2.7 / 5.1）

---

## 2. native-toolkit 確認結果（ネイティブ側の既存実装）

参照: `C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary`
（`WindowsClipboardManager.h` / `.cpp`、`WindowsClipboardHistoryCoordinator.h` / `.cpp`、`WindowsClipboardHistoryWinRt.cpp`、
`WindowsClipboardCore.cpp`、`WindowsClipboardDeferredProvider.cpp`、`WindowsClipboardWindow.cpp`、`WindowsLibrary.def`）
補助資料: `native-toolkit/artifact/designs/clipboard/2026-07-28-windows-clipboard-design-v2.md`

### 2.1 公開関数一覧（`WindowsLibrary.def` の EXPORTS: 27 関数）

| 種別 | 関数 | シグネチャ（C） | 同期/非同期 | スレッド要件 |
|---|---|---|---|---|
| ライフサイクル | `initClipboardManager` | `void(ClipboardChangedCallback, DWORD* pError)` | 同期 | 呼び出しスレッドを所有 UI スレッドとして採用。STA 必須 |
| ライフサイクル | `setClipboardHistoryCallbacks` | `void(ClipboardHistoryChangedCallback, ClipboardFlagChangedCallback, ClipboardFlagChangedCallback, DWORD*)` | 同期 | **所有 UI スレッド限定**。コールバック内から呼ぶのは禁止 |
| ライフサイクル | `uninitClipboardManager` | `BOOL(DWORD* pError)` | 同期（複数回呼ぶ前提） | **所有 UI スレッド限定**。コールバック内から呼ぶのは禁止 |
| ライフサイクル | `canDestroyClipboardManager` | `BOOL(DWORD* pError)` | 同期（非ブロッキング状態問い合わせ） | 制限なし |
| 書き込み | `copyPlainText` | `void(const wchar_t* text, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyHtml` | `void(const wchar_t* htmlFragment, const wchar_t* plainText, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyFiles` | `void(const wchar_t* pathsJson, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyImage` | `void(const BYTE* dib, DWORD dibSize, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyCustomFormat` | `void(const wchar_t* formatName, const BYTE* data, DWORD size, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyMultipleFormats` | `void(const wchar_t* itemsJson, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 読み出し | `pastePlainText` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 読み出し | `pasteHtml` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 読み出し | `pasteFiles` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)`（JSON 配列文字列） | 同期（2 回呼び出し） | 任意スレッド |
| 読み出し | `pasteImage` | `DWORD(BYTE* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 読み出し | `pasteCustomFormat` | `DWORD(const wchar_t* formatName, BYTE* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 内容確認 | `hasClipboardFormat` | `BOOL(const wchar_t* formatName, DWORD*)` | 同期 | 任意スレッド |
| 内容確認 | `getClipboardFormats` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)`（JSON 配列文字列） | 同期（2 回呼び出し） | 任意スレッド |
| 内容確認 | `getPreferredClipboardFormat` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 消去 | `clearClipboard` | `void(DWORD*)` | 同期 | 任意スレッド |
| 遅延配置 | `reserveDeferredFormats` | `void(const wchar_t* formatNamesJson, ClipboardRenderCallback, void* context, DWORD*)`（**options 引数は無い**） | 同期 | **所有 UI スレッド限定** |
| 遅延配置 | `recoverDeferredState` | `void(DWORD*)` | 同期 | **所有 UI スレッド限定** |
| 履歴 | `getClipboardHistory` | `uint32_t(ClipboardRequestCallback, DWORD*)` | 非同期（requestId 返却） | 任意スレッド |
| 履歴 | `restoreHistoryItem` | `uint32_t(const wchar_t* itemId, ClipboardRequestCallback, DWORD*)` | 非同期 | 任意スレッド |
| 履歴 | `deleteHistoryItem` | `uint32_t(const wchar_t* itemId, ClipboardRequestCallback, DWORD*)` | 非同期 | 任意スレッド |
| 履歴 | `clearUnpinnedHistory` | `uint32_t(ClipboardRequestCallback, DWORD*)` | 非同期 | 任意スレッド |
| 履歴 | `getClipboardHistoryAvailability` | `uint32_t(ClipboardRequestCallback, DWORD*)` | 非同期 | 任意スレッド |
| 履歴 | `cancelClipboardRequest` | `BOOL(uint32_t requestId, DWORD*)` | 同期（キャンセル投入のみ） | 任意スレッド |

`getNotificationSetting` 等の Notification 系 export は本計画の対象外。

### 2.2 コールバック型

| 型 | シグネチャ | 実行スレッド | 契約 |
|---|---|---|---|
| `ClipboardChangedCallback` | `void(void)` | 所有 UI スレッド | クリップボード内容変更時。自プロセスの書き込みは通知されない（native 側で self-write 抑止）。**1 回の外部書き込みで複数回発火し得る**（下記） |
| `ClipboardHistoryChangedCallback` | `void(void)` | 所有 UI スレッド | **新規項目追加時のみ**。削除 / ClearHistory では発火しない |
| `ClipboardFlagChangedCallback` | `void(BOOL enabled)` | 所有 UI スレッド | 履歴 / ローミング設定変更。**信頼できない**（後述） |
| `ClipboardRequestCallback` | `void(uint32_t requestId, DWORD error, const wchar_t* json)` | 所有 UI スレッド | 受付済みリクエストごとに**ちょうど 1 回**。`json` は**コールバック中のみ有効**（`Complete` が `cb` 呼び出し直後に `std::wstring` を破棄する） |
| `ClipboardRenderCallback` | `DWORD(const wchar_t* formatName, void* context, BYTE* buffer, DWORD buffer_size, DWORD* pRequiredSize)` | 所有 UI スレッド（`WM_RENDERFORMAT` / `WM_RENDERALLFORMATS` 処理中） | 2.8 の実装契約に厳密に従う必要がある |

`ClipboardFlagChangedCallback` が信頼できない理由（native 側の実測 + 実装）:

- 元の WinRT イベントがプロセス中 1 回しか発火しない / 履歴無効時の登録では発火しない
- `OnHistoryEventMessage` は `QueryHistoryEnabled` / `QueryRoamingEnabled` を呼ぶが、**非フォアグラウンドだと `NOT_FOREGROUND` で break してイベントを捨てる**
  （`WindowsClipboardHistoryCoordinator.cpp` 393-420 行、`WindowsClipboardHistoryWinRt.cpp` 191-212 行）

現在の設定が必要なときは常に `getClipboardHistoryAvailability` を呼ぶ。ただしそれ自体もフォアグラウンドでなければ `NOT_FOREGROUND` を返す（8.2）。

**完了コールバックは inline / reentrant に呼ばれない。** コールバック内から `uninitClipboardManager` / `setClipboardHistoryCallbacks` を呼ぶことは禁止。

**`ClipboardChanged` の発火回数（実機確認済み、2026-09-05）**

抑止はクリップボードのシーケンス番号（`GetClipboardSequenceNumber`）単位で行われる（`ClipboardWatcher::OnClipboardUpdate`）。

| 事象 | 実測 |
|---|---|
| 外部プロセスの書き込み 1 回（PowerShell `Set-Clipboard`） | **`ClipboardChanged` が 3 回**発火。複数フォーマットの配置やクリップボード履歴サービスの再書き込みでシーケンス番号が複数回進むため |
| 自プロセスの `copyPlainText`（`options = NONE`、履歴に載る） | 0 回（抑止が機能） |
| 自プロセスの `copyPlainText`（`options = EXCLUDE_HISTORY`） | 0 回（抑止が機能） |

- **`ClipboardChanged` はユーザーのコピー操作 1 回と 1:1 ではない。** 購読側は冪等に扱うか debounce する
- 「変更回数」をそのまま UI に出すと実際のコピー回数と一致しない（サンプル設計への申し送り。12 章）
- **`RestoreHistoryItem` の成功でも `ClipboardChanged` が発火する。** 復元を実行するのは Windows の履歴サービスであり、
  ライブラリの書き込み経路を通らないため self-write 抑止の対象外になる（ネイティブ側検証 O2。実装の不具合ではない）
- 参考: 履歴 / ローミングのフラグイベントが信頼できないことは、ネイティブ側で 2 セッションにわたり実測されている
  （履歴オンで購読 → 最初のオフで 1 回だけ発火、以後は無反応 / 履歴オフで購読 → 一度も発火せず。同条件でも `onHistoryChanged` は正常）

### 2.3 定数

エラーコード（`CLIPBOARD_ERROR_*`、0 = 成功、非成功 1〜19）:

| 値 | 定数 | 意味（ネイティブ設計書の英語メッセージ） |
|---|---|---|
| 0 | `NONE` | 成功 |
| 1 | `INVALID_PARAMETER` | `Invalid parameter` |
| 2 | `NOT_INITIALIZED` | `Clipboard manager is not initialized` |
| 3 | `BUSY` | `Clipboard is held by another process` |
| 4 | `EMPTY` | `Clipboard is empty` |
| 5 | `FORMAT_UNAVAILABLE` | `Requested format is not available` |
| 6 | `INVALID_DATA` | `Clipboard data failed validation` |
| 7 | `BUFFER_TOO_SMALL` | `Buffer too small; required size returned` |
| 8 | `OUT_OF_MEMORY` | `Out of memory` |
| 9 | `ACCESS_DENIED` | `Access to clipboard history is denied` |
| 10 | `HISTORY_DISABLED` | `Clipboard history is disabled` |
| 11 | `ITEM_DELETED` | `History item was already deleted` |
| 12 | `MONITOR_REGISTER_FAILED` | `Failed to register or unregister the clipboard listener` |
| 13 | `PARTIAL_STATE` | `Rollback failed; clipboard may hold partial content` |
| 14 | `WRONG_THREAD` | `API must be called from the owner UI thread` |
| 15 | `CANCELED` | `Request was canceled` |
| 16 | `NOT_SUPPORTED` | `Operation is not supported on Windows` |
| 17 | `NOT_FOREGROUND` | `App is not in the foreground` |
| 18 | `WRONG_APARTMENT` | `Calling thread is not an initialized STA` |
| 19 | `UNKNOWN` | `Unexpected failure (raw code logged)` |

書き込みオプション（ビットフラグ。**copy 系 6 関数のみに適用され、`reserveDeferredFormats` には適用されない**）:

| 値 | 定数 | 意味 |
|---|---|---|
| `0x0` | `CLIPBOARD_WRITE_OPTION_NONE` | 既定 |
| `0x1` | `CLIPBOARD_WRITE_OPTION_EXCLUDE_HISTORY` | クリップボード履歴に含めない |
| `0x2` | `CLIPBOARD_WRITE_OPTION_EXCLUDE_ROAMING` | クラウド同期しない |
| `0x3` | `CLIPBOARD_WRITE_OPTION_SENSITIVE` | 上記両方 |

### 2.4 JSON スキーマ（ネイティブ境界）

| 用途 | 方向 | スキーマ |
|---|---|---|
| `copyFiles` | C# → native | `["C:\\a.txt","C:\\b.png"]` |
| `pasteFiles` | native → C# | 同上 |
| `getClipboardFormats` | native → C# | 形式名の JSON 配列。名前解決できない形式は `0x%04X` 表記。**空クリップボードでも `EMPTY` にはならず `"[]"` が返る** |
| `copyMultipleFormats` | C# → native | `[{"format":"CF_UNICODETEXT","text":"..."},{"format":"HTML Format","html":"..."},{"format":"CF_DIB","base64":"..."}]`。richest first。1 要素につきペイロードキーは 1 つのみ。重複 `format` と型不一致は配置前に `INVALID_PARAMETER`。**`CF_TEXT` は `CP_ACP` 変換されるため非 ASCII が欠落し得る**（`EncodeAnsiText`） |
| `reserveDeferredFormats` | C# → native | `["CF_UNICODETEXT","MyApp Format"]` |
| `getClipboardHistory` | native → C# | `[{"id":"...","text":"..."\|null,"contentTypes":["Text","Bitmap"],"timestamp":"<int64 の 10 進文字列>"}]`。`timestamp` は 1601 基点の 100ns FILETIME ティック |
| `getClipboardHistoryAvailability` | native → C# | `{"historyEnabled":true,"roamingEnabled":false}` |

### 2.5 受付（acceptance）契約 — 非同期 API

| 段階 | 失敗内容 | 通知 | コールバック回数 |
|---|---|---|---|
| 受付成立前 | 引数不正 / 未初期化 / gate クローズ / `PostMessage` 失敗 | 戻り値の requestId `0` + `pError` | **0 回** |
| 受付成立後 | フォアグラウンド不成立 / 履歴無効 / WinRT 失敗 / 成功 / キャンセル / `uninit` ドレイン | コールバック | **ちょうど 1 回** |

- `Accept` の全拒否経路は**必ず非 0 の `pError` を設定する**（`pError == 0` かつ id 0 は理論上到達しない。8.3 の 1008 は防御的コード）
- `NOT_FOREGROUND` は受付後にコールバックで返る（同期戻り値では返らない）
- `cancelClipboardRequest` が `FALSE` を返しても、すでに配送中の完了は取り消されない
- **開始は `PostMessage(WM_APP_CLIPBOARD_REQUEST)` 経由だが、完了は WinRT continuation から `cb` を直接呼ぶ**（`PostMessage` ではない）。7.6.1 の安全性の根拠に関わる

### 2.6 終了（shutdown）契約

- `uninitClipboardManager` は **TRUE を返すまで繰り返し呼ぶ**。FALSE の間はメッセージポンプを回し続ける必要がある
- **FALSE のときの `pError` の多くは「エラー」ではなく「まだ終わっていない」**。`MONITOR_REGISTER_FAILED(12)` / `CANCELED(15)` / `BUSY(3)` などが入る。
  ただし `WRONG_THREAD(14)` は何度呼んでも完了しない終端失敗である。分類は 7.4 の表を正本とする
- `deferred_.IsPartial()` の回復に失敗し続けると `uninit` は**永久に FALSE を返す**。諦める前に `recoverDeferredState` を試す必要がある
- **プロセス終了前に必ず呼ぶ。** `reserveDeferredFormats` で予約した形式は `WM_RENDERALLFORMATS`（所有ウィンドウ破棄時のみ OS が送る）で実体化される
- **`Uninit` は内部で `WindowsClipboardWindow::Destroy(dispatchHwnd_)` を呼び、`DestroyWindow` が `WM_RENDERALLFORMATS` を同期送出する。**
  つまり `uninitClipboardManager` の P/Invoke スタックの内側で managed の render provider が同期実行される（2.8 / 7.4 / 7.7）
- `canDestroyClipboardManager` は「次の uninit が成功する」保証ではなく状態問い合わせのみ

### 2.7 同梱 DLL の管理（実機ビルドで確認済み、2026-09-05）

**DLL は手動管理ではない。** `Packages/com.jonghyunkim.nativetoolkit/Editor/Build/PreBuildProcessor.cs` の `CopyWindowsLibraries` が
ビルドのたびに `native-toolkit/dist/<version>/windows` から DLL をコピーし、**ビルド構成に応じてリネームして配置**する
（既存の `unity-windows-native-toolkit*.dll` は削除される）。meta も `ConfigureWindowsPluginImporter` が設定する。

```
[Build][Windows] Copying libraries from dist (config=Debug, version=1.11.0)
[Build][Windows] No -debug.dll published for prefix=windows-native-toolkit-; falling back to the release DLL: windows-native-toolkit-1.2.0.dll
[Build][Windows] Copied windows-native-toolkit-1.2.0.dll → unity-windows-native-toolkit-debug.dll
```

ログの `Copying` 行が dist からの解決結果を、`Copied` 行が最終的な配置名を示している。

| ビルド構成 | 配置されるファイル名 |
|---|---|
| Development build（`config = Debug`） | `unity-windows-native-toolkit-debug.dll` |
| Release build | `unity-windows-native-toolkit.dll` |

- dist に `-debug.dll` が published されていなくても、**リリース DLL を debug 名で配置する**（フォールバック）
- したがって **C# 側の `DEVELOPMENT_BUILD` による DLL 名分岐は必須**（5.1）。分岐が無いと development build で必ず `DllNotFoundException` になる（スパイクで実証）
- 既存 `WindowsNotificationManager` の同分岐は正しい実装であり、是正対象ではない

| 項目 | 現状 | 必要な対応 |
|---|---|---|
| Clipboard API を含む DLL | dist 1.11.0 の `windows-native-toolkit-1.2.0.dll`。27 export すべて確認済み。実機で init / copy / paste / 履歴 API が動作 | なし（ビルド時に自動配置） |
| `Plugins/Windows/VERSION.txt` | `PreBuildProcessor` は更新しない情報ファイル。`source:` 行が dist 1.4.0 / 1.1.0 のまま古い | 実害は無いが、混乱を避けるため `dist/1.11.0/windows/windows-native-toolkit-1.2.0.dll` へ更新する（任意） |
| `Microsoft.WindowsAppRuntime.Bootstrap.dll` | 同梱済み | 変更不要（Clipboard は WinAppSDK 非依存） |
| dist のバージョン選択 | `PreBuildProcessor` が config と version（例: `1.11.0`）で解決する | Clipboard 対応版を使うには dist に 1.11.0 以降が存在する必要がある |

### 2.8 遅延レンダリングのネイティブ実装契約（重要）

`MakeDeferredRenderer`（`WindowsClipboardDeferredProvider.cpp` 17-44 行）と `DeferredClipboard::Reserve`（`WindowsClipboardCore.cpp` 613-616 行）から確定した契約。
**ここを外すとエラーもログも出ないまま予約形式がクリップボードから消える。**

| # | 契約 | 破った場合 |
|---|---|---|
| D-1 | `Reserve` は `EmptyClipboard()` を呼んでから `SetClipboardData(fmt, nullptr)` を並べる。**既存のクリップボード内容は破棄される** | 利用側の想定と異なりクリップボードが消える |
| D-2 | 1 回目（`buffer == nullptr`）の戻り値は `NONE` か `BUFFER_TOO_SMALL` のみ許容。それ以外は破棄 | 形式が黙って欠落 |
| D-3 | 1 回目で `*pRequiredSize == 0` なら破棄（長さ 0 のペイロードは配置できない） | 形式が黙って欠落 |
| D-4 | 2 回目（`buffer != nullptr`）の戻り値は `NONE` のみ許容。`BUFFER_TOO_SMALL` も破棄 | 形式が黙って欠落 |
| D-5 | **2 回目の `*pRequiredSize` は 1 回目と完全一致しなければならない**（小さいと HGLOBAL の未初期化末尾が露出するため native 側が破棄する） | 形式が黙って欠落 |
| D-6 | provider はクリップボード API を呼ばない / ブロックしない / 例外を出さない | デッドロック・未定義動作 |
| D-7 | `WM_RENDERALLFORMATS` は `uninit` 内の `DestroyWindow` から同期送出される。provider は Unity のシャットダウン進行中に走り得る | 破棄済み Unity オブジェクト参照でクラッシュ |
| D-8 | Windows のクリップボード履歴が有効な場合、履歴サービスが予約直後に全形式を実体化する | 「まだ provider が呼ばれていない」を前提にした実装が破綻 |
| D-9 | **`Reserve` は失敗地点によって native 側の renderer テーブルの状態が変わる**（下表） | C# 側が旧世代を先に捨てると、生き残った native renderer に対応する provider が無くなる |

`DeferredClipboard::Reserve`（`WindowsClipboardCore.cpp`）の失敗地点と `renderers_` の状態:

| 失敗地点 | 返却 | `renderers_` | C# から判別可能か |
|---|---|---|---|
| 引数検証（owner null / 空 / fmt 0 / fn null） | `INVALID_PARAMETER`(1) | **旧世代のまま**（未変更） | 可 |
| `ClipboardScope` を開けない | `BUSY`(3) | **旧世代のまま**（未変更） | 可 |
| `EmptyClipboard()` 失敗 | `UNKNOWN`(19) | **旧世代のまま**（未変更） | **不可** |
| 個別 `SetClipboardData` 失敗 → rollback 成功 | `UNKNOWN`(19) | `Clear()` 済み（空） | **不可** |
| 個別 `SetClipboardData` 失敗 → rollback 失敗 | `PARTIAL_STATE`(13) | **新世代**（`partial_ = true`） | 可 |
| 全形式配置成功 | `NONE`(0) | 新世代 | 可 |

**`UNKNOWN`(19) の 2 経路は C ABI からは区別できない。** ただしどちらの場合も native が保持するのは
**旧世代か空**であり、新世代を保持することは無い。したがって「`UNKNOWN` では旧世代を維持する」という規則が
両経路で安全に成立する（7.7）。

`recoverDeferredState` が回復するのは **`DeferredClipboard` の partial 状態のみ**。`copyMultipleFormats` が返す `PARTIAL_STATE` は回復対象外。

### 2.9 その他の確定事項

- `hasClipboardFormat` は**失敗時も FALSE を返す**（未初期化 / null 名で FALSE + `pError`）。「存在しない」と「判定できなかった」は `pError` でしか区別できない。
  実装は `IsClipboardFormatAvailable` のみで**クリップボードを開かない**ため `BUSY`(3) は返らない
- **読み出し系は失敗時にも戻り値 0 を返す。** `PasteFiles` / `PasteImage` / `PasteCustomFormat` などは lease 取得失敗（`NOT_INITIALIZED`）や
  内部読み出し失敗（`BUSY` / `INVALID_DATA` / `EMPTY` 等）でも `return 0` する。
  **「戻り値 0 = 空」と解釈してはならない**（7.5 でエラー分類を先に行う）
- `getPreferredClipboardFormat` は該当なし・空クリップボードのいずれでも `PickPreferredFormat()` が 0 / -1 を返し、`name` が空文字になる。
  結果は **必要サイズ 1 + `BUFFER_TOO_SMALL` → 2 回目は空文字 + `NONE`**。`EMPTY` は返らない（旧 V-4 はこれで解決）
- `pasteImage` / `pasteCustomFormat` はデータ長 0 のとき **戻り値 0 + `BUFFER_TOO_SMALL`** を返す（`WriteBytesToBuffer` は `needed == 0` でも `buffer == nullptr` なら `BUFFER_TOO_SMALL`）
- `setClipboardHistoryCallbacks(nullptr, nullptr, nullptr)` による停止でトークン解除に失敗すると WinRT 側に `revokePending_` が立ち、
  **以後の再有効化が `MONITOR_REGISTER_FAILED` で拒否され続ける**（sticky 状態。回復手段は後の停止成功のみ）
- 全同期 API は `AcquireSyncLease` により init 前に `NOT_INITIALIZED` を返す
- **`getPreferredClipboardFormat` は `HTML Format` を返さない。** `PickPreferredFormat` の候補は
  `{CF_UNICODETEXT, CF_HDROP, CF_DIB, CF_BITMAP}` に固定されており、HTML はどれだけ情報量が多くても選ばれない
  （ネイティブ側から機能側への報告事項）。「最も情報量の多い形式」を UI に出す場合はこの制約を明記する
- **書き込み系は同一の self-write mutex で直列化される。** `copy*` / `reserveDeferredFormats` / `recoverDeferredState` は
  互いに待つ。呼び出しスレッド（本設計では Unity メインスレッド）が短時間ブロックし得る（7.2）

---

## 3. 既存 C# 実装の確認結果

参照: `Packages/com.jonghyunkim.nativetoolkit/Runtime/`

### 3.1 Windows の P/Invoke パターン（`Notification/WindowsNotificationManager.cs`）

踏襲する点:

- コンパイルガード `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`（Editor でもコンパイルされる A 群）
- `[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]` + `[MarshalAs(UnmanagedType.LPWStr)] string`、`out int pError`
- コールバック delegate は `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`、実体は `[MonoPInvokeCallback]` の **static** メソッド（`using AOT;` が必要）
- delegate の GC ルート: `private static readonly XxxCallback s_persistentDelegate = OnXxx;`
- 2 回呼び出しバッファ: `Marshal.AllocHGlobal(size * 2)` → `Marshal.PtrToStringUni` → `finally` で `FreeHGlobal`
- Singleton（`Instance` プロパティ + `Awake` での `_instance` 確定 + `DontDestroyOnLoad` + `_ = UnityMainThreadDispatcher.Instance`）
- 結果型（`Operation` / `IsSuccess` / `ErrorCode` / `ErrorMessage` + コード → メッセージ変換）
- ファイル構成: 1 行目 `#nullable enable`、3 行目 `#if`、`using` は `namespace` の内側

改める点（本設計で明示的に変える）:

- 非 Windows Player で **何も返さず無言で return** している（per-call callback も event も発火しない）。Clipboard では**失敗結果を返して event と callback を必ず発火**させる
- （v3 まで是正対象としていたが撤回）DLL 名定数の `DEVELOPMENT_BUILD` 分岐は `PreBuildProcessor` の配置規則に沿った**正しい実装**であり、Clipboard も同じ分岐を持つ（2.7 / 5.1）

### 3.2 Clipboard の Manager パターン（`Clipboard/IosClipboardManager.cs` / `AndroidClipboardManager.cs`）

踏襲する点:

- 共通 event（`event Action<TResult>?`）+ 任意の per-call callback（`Action<TResult>? onResult = null`）の二本立て
- dispatch 順序は **共通 event → 個別 callback**（`InvokeInOrder`）。`internal static` の純粋関数として切り出し、EditMode で検証。購読者の例外は片方ずつ `try/catch` で分離する
- **拒否結果も含めて全経路を `UnityMainThreadDispatcher` 経由で配送する**（`AndroidClipboardManager.FireOperationResult` / `IosClipboardManager` の rejected 専用 dispatch）
- 操作名の `public const string Operation*` 定数
- `s_mainThreadId` / `s_dispatcher` を `Awake` でキャッシュする（`Instance` getter は GameObject を生成するため**スレッド**の観点ではガードできない）
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` による static リセットと `#if UNITY_EDITOR internal static ResetForTests()` を単一の `ResetCore` で共有する
- teardown の前例（`IosClipboardManager.Awake` / `OnDestroy`）:
  - `Awake` は `_instance == null` なら自分を採用、`_instance != this` なら `Destroy(gameObject)` して return（重複インスタンスが稼働中の singleton を壊さない）
  - `OnDestroy` の 1 行目が `if (_instance != this) return;`（**重複インスタンスの破棄で本体を shutdown しない**）
  - tombstone（`s_isTerminated`）は**ネイティブ呼び出しより前**に立てる（P/Invoke が throw しても teardown が中途半端にならないため）
  - 破棄後の拒否結果も配送する必要があるため `s_dispatcher` は意図的に残す
  - `ClearAllPendingCallbacks()` で保留中の callback を破棄する（本設計ではこれを「完了配送レジストリの同期ドレイン」に置き換える。7.6.3）
  - `Instance` getter は tombstone を確認せず生成する。生成後 `Awake` で `s_isTerminated` を検出して `Debug.LogError` を出し、以降の操作は結果型で拒否する（7.10）
- JsonBuilder は `public static` / JsonParser は `internal static`。`JsonUtility` + DTO 方式

**機微情報のログ規約（前例は 2 種類に分かれる。混同しないこと）**:

| 対象 | 扱い | 前例 |
|---|---|---|
| Manager の public / internal メソッド | **ログ行は残し、値だけ伏せる**（`textLength` / `hasLabel` / `isSensitive` / `hasCallback` のような派生値を出す） | `AndroidClipboardManager.CopyPlainText`、`IosClipboardManager.cs:44`（"log every **parameter**" 規約からの逸脱と記す） |
| JsonBuilder / JsonParser / 結果型 | **ログ行自体を出さない**（`no entry log is emitted at all`） | `AndroidClipboardJsonBuilder.cs:19`、`AndroidClipboardJsonParser.cs:21` |

いずれの場合も、逸脱をファイル冒頭またはクラスの XML コメントに `Intentional deviation from csharp.md's ...` の形で明記する（リポジトリ内 18 ファイルで実施済みの運用）。

再利用しない点:

- `ClipboardOperationResult` / `ClipboardReadResult` / `ClipboardDescriptionResult` は Android の ClipData 概念に強く結びついており Windows の形式概念と一致しない。**Windows 専用の結果型を新規追加する**
- `IosClipboardJsonReader` は **共有しない**。`common.md`「命名: OS 接頭辞と、共通ファイルを作らない方針」が
  「2 つのプラットフォームが同じロジックを必要とする場合も、共通化せずそれぞれに持たせる」と定めているため。
  macOS Clipboard でも同じ判断で `MacClipboardJsonReader` として複製した前例がある。
  Windows は `JsonUtility` ベースの軽量パーサ（`WindowsClipboardJsonParser`）を新設する
- 既存の接頭辞なし型（`ClipboardOperationResult` / `ClipboardReadResult` / `ClipboardDescriptionResult`）は
  **共有型ではなく Android 専用**である（`artifact/OS_PREFIX_VIOLATIONS.md` の既知の逸脱 11 件）。
  他プラットフォームから使わない、前例として引用しない、という規約に従う

### 3.3 テスト構成

- `Tests/Runtime`（asmdef は `includePlatforms: [Editor]`）= EditMode。`Runtime/AssemblyInfo.cs` に両テストアセンブリの `InternalsVisibleTo` が既にあるため asmdef 変更は不要
- `Tests/PlayMode` = Editor 内 PlayMode
- Manager インスタンスを生成するテストは EditMode に置かない
- `AssemblyInfo.cs` のコメントは「PlayMode テストが tombstone を分離するために `ResetForTests` seam が必要。無いと 1 つの destroy テストが以降すべてを拒否させる」と明記している（7.10 の根拠）

---

## 4. 実装制約（common.md / csharp.md からの固定事項）

- Bridge 層で同期・非同期を変換しない。ネイティブ同期 API は C# も同期メソッド、非同期 API は callback 版を必須とし、多重呼び出しガードが成立する場合のみ `Awaitable<T>` 版を併設する
- 非同期版を追加する前に、その操作が **OS 上で同時実行可能か**を判定する（7.6.4）
- ネイティブコールバックは `UnityMainThreadDispatcher.Instance.Enqueue` 経由で配送する（例外は 7.7 の render provider と 7.6.3 の teardown ドレインのみ。いずれも理由を明記する）
- Singleton は `Instance` プロパティ + `Awake` の二重生成防止 + `DontDestroyOnLoad`
- `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` でクラス全体を囲む
- 全 public / internal メソッド先頭に `Debug.Log`（機微情報は 3.2 の書き分けに従う）
- public 型・メソッドに XML ドキュメントコメント、コメントとユーザー向け文言は英語
- `.meta` ファイルは作成しない
- 最小サポート: Unity 6 / Windows 11
- **規約から逸脱する場合は、逸脱・理由・代替案の比較を設計書とコードコメントの両方に明記する**（本計画では 7.4 の `Shutdown` が該当）

---

## 5. 実装対象 API 一覧（native → C# 対応）

### 5.1 `[DllImport]` 宣言（`WindowsClipboardManager` 内 private static extern）

方針:

- **DLL 名は `DEVELOPMENT_BUILD` で分岐する（必須）。** `PreBuildProcessor` が development build では `-debug` サフィックス付きで配置するため（2.7）。
  既存 `WindowsNotificationManager` と同一の形にする

```csharp
#if DEVELOPMENT_BUILD
    private const string DLL_NAME = "unity-windows-native-toolkit-debug";
#else
    private const string DLL_NAME = "unity-windows-native-toolkit";
#endif
```

- **`CallingConvention = CallingConvention.Cdecl` を全 `DllImport` に明示する**（ネイティブは `__cdecl`、`DllImport` 既定は `Winapi`(StdCall)。x64 では等価だが delegate 側の `Cdecl` 明示と非対称にしない）
- **`ExactSpelling = true` を明示する**（`CharSet.Unicode` の既定では `copyPlainTextW` を先に探す無駄な探索が入る）
- extern メソッド名はネイティブ関数名をそのまま使い `EntryPoint` を省略する（camelCase は意図的。既存 `WindowsNotificationManager` と同じ方針）
- **P/Invoke 宣言はクラスガードの内側にさらに `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` を重ねる**（P5。7.11 に構造の全体像）

```csharp
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
[DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
private static extern void initClipboardManager(ClipboardChangedCallback? onChanged, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
private static extern void setClipboardHistoryCallbacks(
    ClipboardHistoryChangedCallback? onHistoryChanged,
    ClipboardFlagChangedCallback? onHistoryEnabledChanged,
    ClipboardFlagChangedCallback? onRoamingEnabledChanged,
    out int pError);

[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool uninitClipboardManager(out int pError);

[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool canDestroyClipboardManager(out int pError);

// 以降、属性は同一のため省略して列挙する
void copyPlainText([MarshalAs(UnmanagedType.LPWStr)] string text, uint options, out int pError);
uint pastePlainText(IntPtr buffer, uint bufferSize, out int pError);
void copyHtml([MarshalAs(UnmanagedType.LPWStr)] string htmlFragment,
              [MarshalAs(UnmanagedType.LPWStr)] string? plainText, uint options, out int pError);
uint pasteHtml(IntPtr buffer, uint bufferSize, out int pError);
void copyFiles([MarshalAs(UnmanagedType.LPWStr)] string pathsJson, uint options, out int pError);
uint pasteFiles(IntPtr buffer, uint bufferSize, out int pError);
void copyImage(byte[] dib, uint dibSize, uint options, out int pError);
uint pasteImage(IntPtr buffer, uint bufferSize, out int pError);
void copyCustomFormat([MarshalAs(UnmanagedType.LPWStr)] string formatName, byte[] data, uint size, uint options, out int pError);
uint pasteCustomFormat([MarshalAs(UnmanagedType.LPWStr)] string formatName, IntPtr buffer, uint bufferSize, out int pError);
void copyMultipleFormats([MarshalAs(UnmanagedType.LPWStr)] string itemsJson, uint options, out int pError);
[return: MarshalAs(UnmanagedType.Bool)] bool hasClipboardFormat([MarshalAs(UnmanagedType.LPWStr)] string formatName, out int pError);
uint getClipboardFormats(IntPtr buffer, uint bufferSize, out int pError);
uint getPreferredClipboardFormat(IntPtr buffer, uint bufferSize, out int pError);
void clearClipboard(out int pError);
void reserveDeferredFormats([MarshalAs(UnmanagedType.LPWStr)] string formatNamesJson,
                            ClipboardRenderCallback provider, IntPtr context, out int pError);  // options 引数は無い
void recoverDeferredState(out int pError);
uint getClipboardHistory(ClipboardRequestCallback cb, out int pError);
uint restoreHistoryItem([MarshalAs(UnmanagedType.LPWStr)] string itemId, ClipboardRequestCallback cb, out int pError);
uint deleteHistoryItem([MarshalAs(UnmanagedType.LPWStr)] string itemId, ClipboardRequestCallback cb, out int pError);
uint clearUnpinnedHistory(ClipboardRequestCallback cb, out int pError);
uint getClipboardHistoryAvailability(ClipboardRequestCallback cb, out int pError);
[return: MarshalAs(UnmanagedType.Bool)] bool cancelClipboardRequest(uint requestId, out int pError);
#endif
```

COM アパートメント判定用（`ole32.dll`）。これも同じ内側ガードに入れる:

```csharp
private enum AptType { Current = -1, Sta = 0, Mta = 1, Na = 2, MainSta = 3 }

[DllImport("ole32.dll")]
private static extern int CoGetApartmentType(out AptType pAptType, out int pAptQualifier);

[DllImport("ole32.dll")]
private static extern int CoInitializeEx(IntPtr reserved, uint coInit);   // COINIT_APARTMENTTHREADED = 0x2

private const int S_OK = 0;
private const int S_FALSE = 1;                      // 既に初期化済み（成功扱い）
private const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);
```

判定条件はネイティブ側（`WindowsClipboardManager.cpp` 160-168 行）と同一にする: `CoGetApartmentType` が成功し、かつ `Sta` または `MainSta` であること。

その他の補足:

- 出力バッファは `IntPtr`（`Marshal.AllocHGlobal`）で受ける。`wchar_t` 系の戻り値は **wchar_t 数**（終端含む）、`BYTE` 系は **バイト数**
- `copyImage` / `copyCustomFormat` の入力は `byte[]` を直接マーシャリング（ピン留めは CLR が行う）

### 5.2 delegate 型（IL2CPP / AOT 安全）

delegate 型そのものはクラスガード内（Editor でもコンパイルされる）に置き、**`[MonoPInvokeCallback]` を持つ実体と
`static readonly` の delegate インスタンスは `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` の内側**に置く（P5。7.11）。

```csharp
using AOT;   // MonoPInvokeCallback に必要

[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClipboardChangedCallback();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClipboardHistoryChangedCallback();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClipboardFlagChangedCallback([MarshalAs(UnmanagedType.Bool)] bool enabled);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClipboardRequestCallback(uint requestId, int error, [MarshalAs(UnmanagedType.LPWStr)] string? json);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint ClipboardRenderCallback([MarshalAs(UnmanagedType.LPWStr)] string formatName, IntPtr context, IntPtr buffer, uint bufferSize, out uint requiredSize);
```

- `json` を `string` でマーシャリングすると CLR が**呼び出し中にコピー**するため、「コールバック中のみ有効」というネイティブ契約を満たす。逆 P/Invoke の in 引数は CLR が解放しないためリークもない。`null` は `null` として届く
- すべての実体は `static` + `[MonoPInvokeCallback(typeof(...))]`。例外を C ABI 境界へ漏らさないよう全体を `try/catch` で包む
- **`ClipboardRenderCallback` の `out uint requiredSize` は例外離脱時に書き戻らない。** catch 節で必ず `requiredSize = 0` を代入し、非 `NONE`（`Unknown` = 19）を返す

### 5.3 公開 API 対応表

| ネイティブ | C# 公開 API | 形 |
|---|---|---|
| `initClipboardManager` | `WindowsClipboardResult Initialize(bool enableChangeEvents = true, Action<WindowsClipboardResult>? onResult = null)` | 同期 |
| `setClipboardHistoryCallbacks` | `WindowsClipboardResult SetHistoryEventsEnabled(bool enabled, Action<WindowsClipboardResult>? onResult = null)` | 同期 |
| `uninitClipboardManager` | `WindowsClipboardResult TryShutdown(out bool completed)` | **同期**。単発呼び出しのため event / callback は発火しない（例外扱い。理由は下記） |
| （上記のフレーム跨ぎヘルパ） | `void ShutdownWithDrain(Action<WindowsClipboardResult>? onResult = null)` | Unity 側ヘルパ（7.4 に逸脱理由）。**最終結果のみ** event + callback を発火する |
| `canDestroyClipboardManager` | `WindowsClipboardFlagResult CanShutdownNow(Action<WindowsClipboardFlagResult>? onResult = null)` | 同期 |
| `copyPlainText` | `WindowsClipboardResult CopyPlainText(string text, WindowsClipboardWriteOptions options = None, Action<WindowsClipboardResult>? onResult = null)` | 同期 |
| `copyHtml` | `WindowsClipboardResult CopyHtml(string htmlFragment, string? plainText = null, ...)` | 同期 |
| `copyFiles` | `WindowsClipboardResult CopyFiles(IReadOnlyList<string> paths, ...)` | 同期 |
| `copyImage` | `WindowsClipboardResult CopyImage(byte[] dib, ...)` | 同期 |
| `copyCustomFormat` | `WindowsClipboardResult CopyCustomFormat(string formatName, byte[] data, ...)` | 同期 |
| `copyMultipleFormats` | `WindowsClipboardResult CopyMultipleFormats(IReadOnlyList<WindowsClipboardFormatPayload> items, ...)` | 同期 |
| `pastePlainText` | `WindowsClipboardTextResult PastePlainText(Action<WindowsClipboardTextResult>? onResult = null)` | 同期 |
| `pasteHtml` | `WindowsClipboardTextResult PasteHtml(...)` | 同期 |
| `pasteFiles` | `WindowsClipboardStringListResult PasteFiles(...)` | 同期 |
| `pasteImage` | `WindowsClipboardBytesResult PasteImage(...)` | 同期 |
| `pasteCustomFormat` | `WindowsClipboardBytesResult PasteCustomFormat(string formatName, ...)` | 同期 |
| `hasClipboardFormat` | `WindowsClipboardFormatPresenceResult HasFormat(string formatName, ...)` | 同期 |
| `getClipboardFormats` | `WindowsClipboardStringListResult GetFormats(...)` | 同期 |
| `getPreferredClipboardFormat` | `WindowsClipboardTextResult GetPreferredFormat(...)` | 同期 |
| `clearClipboard` | `WindowsClipboardResult Clear(...)` | 同期 |
| `reserveDeferredFormats` | `WindowsClipboardResult ReserveDeferredFormats(IReadOnlyDictionary<string, Func<byte[]>> providers, Action<WindowsClipboardResult>? onResult = null)`（**`options` は取らない**） | 同期 |
| `recoverDeferredState` | `WindowsClipboardResult RecoverDeferredState(...)` | 同期 |
| `getClipboardHistory` | `uint GetHistory(Action<WindowsClipboardHistoryResult>? onResult = null)` / `Awaitable<WindowsClipboardHistoryResult> GetHistoryAsync(CancellationToken ct = default)` | 非同期 |
| `restoreHistoryItem` | `uint RestoreHistoryItem(string itemId, Action<WindowsClipboardResult>? onResult = null)` / `RestoreHistoryItemAsync(string itemId, CancellationToken ct = default)` | 非同期（in-flight ガードあり） |
| `deleteHistoryItem` | `uint DeleteHistoryItem(string itemId, ...)` / `DeleteHistoryItemAsync(...)` | 非同期（in-flight ガードあり） |
| `clearUnpinnedHistory` | `uint ClearUnpinnedHistory(...)` / `ClearUnpinnedHistoryAsync(...)` | 非同期（in-flight ガードあり） |
| `getClipboardHistoryAvailability` | `uint GetHistoryAvailability(Action<WindowsClipboardAvailabilityResult>? onResult = null)` / `GetHistoryAvailabilityAsync(CancellationToken ct = default)` | 非同期 |
| `cancelClipboardRequest` | `WindowsClipboardResult CancelRequest(uint requestId, Action<WindowsClipboardResult>? onResult = null)` | 同期 |

戻り値の方針:

- **`pError` を持つ API はすべて結果型を返す**。`bool` を直接返す API は作らない（`CanShutdownNow` は `WindowsClipboardFlagResult`、`CancelRequest` は `WindowsClipboardResult`）
- 非同期 API のみ `uint`（requestId）を返す。`Awaitable` 版は requestId を返せないため、キャンセルは `CancellationToken` で行う（7.6.5）

**結果配送の対応（M4 の反映）**:

| API | 共通イベント | per-call callback | 備考 |
|---|---|---|---|
| `Initialize` / `SetHistoryEventsEnabled` / copy 系 / `Clear` / `ReserveDeferredFormats` / `RecoverDeferredState` / `CancelRequest` | `ClipboardOperationCompleted` | あり | 通常経路 |
| `ShutdownWithDrain` | `ClipboardOperationCompleted` | あり | **最終結果のみ**。リトライ途中の未完了結果は配送しない |
| `CanShutdownNow` | `FlagChecked`（新設。`event Action<WindowsClipboardFlagResult>?`） | あり | 7.1 の event 一覧に追加する |
| `TryShutdown` | **発火しない**（明示的な例外） | なし | 状態問い合わせに近い単発 API であり、`ShutdownWithDrain` のループから毎フレーム呼ばれるため。イベント購読者に未完了結果が大量配送されるのを避ける。この例外理由を XML コメントに明記する |
| paste 系 / `GetFormats` / `GetPreferredFormat` / `HasFormat` | 各読み出し event | あり | 通常経路 |
| 履歴 5 種 | 各 event | あり | 通常経路 |

---

## 6. 変更ファイル一覧

### 6.1 新規作成（Runtime）

すべて `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/` 配下、名前空間は `JonghyunKim.NativeToolkit.Runtime.Clipboard`。
`common.md` が課すのは**ファイル名とその主たる型名の一致**であり、1 ファイル 1 型ではない（macOS の `MacClipboard*Result.cs` と同じ粒度）。
主型に不可分な補助型は同じファイルに置く。実装時点で複数の型を持つのは次の 4 ファイル:
`WindowsClipboardPayloads.cs`（`WindowsClipboardWriteOptions` / `WindowsClipboardFormatPayload` /
`WindowsClipboardPayloadKind`。既存の `IosClipboardPayloads` / `AndroidClipboardPayloads` に倣う）、
`WindowsClipboardErrorCode.cs`（enum と `ToMessage` 拡張）、
`WindowsClipboardManager.cs`（Manager と、それと不可分な internal enum 6 種）、
`WindowsClipboardRequestTable.cs`（テーブルと `WindowsClipboardRequestState`）。
ファイル構成は既存に合わせ、1 行目 `#nullable enable`、3 行目 `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`、`using` は `namespace` の内側に置く。
ネイティブ境界（`DllImport` / `MonoPInvokeCallback`）はさらに `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` で囲む（7.11）。
全ファイル名と public / internal 型名に `Windows` 接頭辞を付ける（`common.md` の OS 接頭辞方針）。

| ファイル | 内容 |
|---|---|
| `WindowsClipboardManager.cs` | Singleton Manager。P/Invoke 宣言、ライフサイクル、同期 API、非同期 API、イベント配送、遅延レンダリング、static リセット seam |
| `WindowsClipboardErrorCode.cs` | `WindowsClipboardErrorCode` enum（native 0〜19 + C# 側 1000 番台）と `ToMessage()` |
| `WindowsClipboardResult.cs` | `WindowsClipboardResult`（void 操作の結果型） |
| `WindowsClipboardTextResult.cs` | `PastePlainText` / `PasteHtml` / `GetPreferredFormat` の結果型 |
| `WindowsClipboardStringListResult.cs` | `PasteFiles` / `GetFormats` の結果型 |
| `WindowsClipboardBytesResult.cs` | `PasteImage` / `PasteCustomFormat` の結果型 |
| `WindowsClipboardFormatPresenceResult.cs` | `HasFormat` の結果型（`IsSuccess` と `HasFormat` は独立） |
| `WindowsClipboardFlagResult.cs` | `CanShutdownNow` の結果型 |
| `WindowsClipboardHistoryItem.cs` | 履歴 1 件（`Id` / `Text` / `ContentTypes` / `Timestamp`） |
| `WindowsClipboardHistoryResult.cs` | `GetHistory` の結果型 |
| `WindowsClipboardAvailabilityResult.cs` | `GetHistoryAvailability` の結果型 |
| `WindowsClipboardPayloads.cs` | `WindowsClipboardWriteOptions`（`[Flags]`）、`WindowsClipboardFormatPayload`（format + text / html / base64 の排他とその検証） |
| `WindowsClipboardJsonBuilder.cs` | `BuildPathsJson` / `BuildMultiFormatItemsJson` / `BuildFormatNamesJson`（public static、手書きシリアライザ） |
| `WindowsClipboardJsonParser.cs` | `TryParseStringArray` / `TryParseHistoryItems` / `TryParseAvailability`（internal static、`JsonUtility` + DTO） |
| `WindowsClipboardRequestTable.cs` | `internal sealed class`。requestId → pending エントリの登録 / 取り出し / 一括ドレイン。ネイティブ非依存の純粋ロジックとして EditMode で検証する |

### 6.2 新規作成（テスト）

全テストファイルに **`#if UNITY_STANDALONE_WIN || UNITY_EDITOR`**（対象型のクラスガードと同一）を付け、
ファイル名・クラス名に `Windows` 接頭辞を付ける。1 ファイルで複数プラットフォームの型を扱わない（P1b）。

| ファイル | 層 | 内容 |
|---|---|---|
| `Tests/Runtime/WindowsClipboardResultTests.cs` | EditMode | 結果型の不変条件、エラーコード → メッセージ対応、`IsEmpty` / `HasFormat` の独立性 |
| `Tests/Runtime/WindowsClipboardPayloadsTests.cs` | EditMode | `WindowsClipboardFormatPayload` の排他規則、`WindowsClipboardWriteOptions` のフラグ合成 |
| `Tests/Runtime/WindowsClipboardJsonBuilderTests.cs` | EditMode | パス配列 / 複数形式 / 予約形式の JSON 生成、エスケープ、順序保持 |
| `Tests/Runtime/WindowsClipboardJsonParserTests.cs` | EditMode | 履歴配列（`text` null、`contentTypes` 省略、`timestamp` 巨大値）、可用性オブジェクト、壊れた JSON |
| `Tests/Runtime/WindowsClipboardRequestTableTests.cs` | EditMode | 登録 / 取り出しで削除、未知 ID の完了が無害、二重完了の抑止、ドレインで全件が 1 回ずつ完了 |
| `Tests/Runtime/WindowsClipboardManagerDispatchTests.cs` | EditMode | `InvokeInOrder`（共通 → 個別）、購読者例外の分離、バッファ再試行判定、遅延レンダリングの二相判定（キャッシュ再利用・サイズ不一致検出） |
| `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs` | PlayMode（Editor 内） | 拒否経路、8 種の共通 event の発火、発火しない契約、`Awaitable` の完了保証、teardown ドレイン、`ResetForTests` による分離 |

### 6.3 既存変更

| ファイル | 変更内容 | 状態 |
|---|---|---|
| `Packages/.../Plugins/Windows/unity-windows-native-toolkit*.dll` | **変更不要**。`PreBuildProcessor` がビルド時に dist から配置・リネームする（2.7）。作業ツリー上のファイル名はビルド構成で入れ替わるため、コミット対象として扱わない | — |
| `Packages/.../Plugins/Windows/VERSION.txt` | `source:` 行が dist 1.4.0 / 1.1.0 のまま古い。同じフル URL 書式で `.../dist/1.11.0/windows/windows-native-toolkit-1.2.0.dll` へ更新（任意。自動更新されない情報ファイル） | 任意 |
| `scripts/check_design_consistency.py` | 本設計の機械照合中に判明した 2 点を修正: macOS の `.framework` 配下を走査して `FileNotFoundError` で止まる問題と、新規ファイル一覧の照合が Android 専用だった問題 | 必須 |

### 6.4 非変更（明示）

- `Runtime/Clipboard/` の Android / iOS 実装、`ClipboardOperationResult` / `ClipboardReadResult` / `ClipboardDescriptionResult`
- `Runtime/Notification/Windows*`、`Runtime/Dialog/*`、`Runtime/Common/*`、`Runtime/AssemblyInfo.cs`
- `Runtime/NativeToolkit.Runtime.asmdef`、`Tests/*/*.asmdef`（新規 asmdef は追加しない）
- `Runtime/UI/**`、`Samples~/**`、シーン、UXML / USS（`design-sample-scene` の対象）
- `package.json`（バージョン更新はリリースワークフローの責務）

---

### 6.5 プラットフォーム独立性の自己点検（P1〜P5）

`agent-rules/coding-rules/common.md`「命名: OS 接頭辞と、共通ファイルを作らない方針」と、
review-document / review-implementation-feature の P1〜P5 に対する本計画の状態。

| 観点 | 状態 | 根拠 |
|---|---|---|
| **P1** 新規ファイル名・型名の OS 接頭辞（テストを含む） | 適合 | Runtime 15 ファイル・テスト 7 ファイルすべて `Windows` 接頭辞。**原則 1 ファイル 1 主型**でファイル名と型名が一致し、例外は `WindowsClipboardPayloads.cs` のみ（6.1 / 6.2） |
| **P1b** 1 テストファイル 1 プラットフォーム / ガード一致 | 適合 | すべて Windows 専用。`#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` を付ける（6.2） |
| **P2** 既存変更に他プラットフォームを含めない | 適合 | 6.3 は `Plugins/Windows` のみ。`Android*` / `Ios*` / `Mac*` に触れない（6.4） |
| **P3** 再利用判断が実際の利用箇所に基づく | 適合 | `IosClipboardJsonReader` は方針に従い複製（`MacClipboardJsonReader` の前例）。接頭辞なし型は Android 専用として使わない（3.2） |
| **P4** `Runtime/Common/` への追加 | 適合 | 追加なし（6.4） |
| **P5** クラスガードと P/Invoke ガードの二重構造 | 適合 | 7.11 に構造を定義。5.1 / 5.2 / 7.1 の擬似コードも同形 |
| 既知の逸脱 11 件を前例に引用していない | 適合 | 3.2 で「共有型ではなく Android 専用」と明記し、使わない方針を宣言 |

---

## 7. 実装詳細

### 7.1 クラス構成とイベント

```csharp
#nullable enable

// クラスガード: Editor を含めコンパイルされる（テストと Editor 実行のため）
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    // using は namespace の内側に置く（6.1）。System / System.Threading / AOT /
    // JonghyunKim.NativeToolkit.Runtime.Common / UnityEngine ほか。
    public class WindowsClipboardManager : MonoBehaviour
    {
        private const string LogTag = "WindowsClipboardManager";

        private static WindowsClipboardManager? _instance;

        public static WindowsClipboardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("WindowsClipboardManager");
                    _instance = go.AddComponent<WindowsClipboardManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // 操作名定数（結果型の Operation に入る。全 API 分を定義する）
        public const string OperationInitialize       = "initClipboardManager";
        public const string OperationShutdown         = "uninitClipboardManager";
        public const string OperationSetHistoryEvents = "setClipboardHistoryCallbacks";
        public const string OperationCopyPlainText    = "copyPlainText";
        // ...

        // 共通イベント（常に発火）
        public event Action<WindowsClipboardResult>? ClipboardOperationCompleted;          // void 系すべて
        public event Action<WindowsClipboardTextResult>? TextReadCompleted;                // PastePlainText / PasteHtml / GetPreferredFormat
        public event Action<WindowsClipboardStringListResult>? StringListReadCompleted;    // PasteFiles / GetFormats
        public event Action<WindowsClipboardBytesResult>? BytesReadCompleted;              // PasteImage / PasteCustomFormat
        public event Action<WindowsClipboardFormatPresenceResult>? FormatPresenceChecked;  // HasFormat
        public event Action<WindowsClipboardFlagResult>? FlagChecked;                      // CanShutdownNow
        public event Action<WindowsClipboardHistoryResult>? HistoryReadCompleted;          // GetHistory
        public event Action<WindowsClipboardAvailabilityResult>? HistoryAvailabilityChecked;

        // 通知イベント（ネイティブ発）
        public event Action? ClipboardChanged;
        public event Action? HistoryChanged;
        public event Action<bool>? HistoryEnabledChanged;   // 信頼できない旨を XML コメントに明記（2.2）
        public event Action<bool>? RoamingEnabledChanged;   // 同上
    }
}
#endif
```

**callback 提供方針**:

| 種別 | 形 | 発火 |
|---|---|---|
| 共通イベント | `event Action<TResult>?` | 常に発火（拒否・失敗・成功のすべて） |
| 個別 callback | 各メソッドの `Action<TResult>? onResult = null` 引数 | 任意。未指定でも共通イベントは発火 |
| dispatch 順序 | **共通イベント → 個別 callback** | `InvokeInOrder` を `internal static` として切り出し EditMode で検証 |

**結果配送経路**:

- 同期 API は**戻り値で即時に結果を返す**（common.md「同期ネイティブ API は同期 C# API」）
- **イベントと per-call callback は、同期・非同期・拒否のすべての経路で `UnityMainThreadDispatcher.Enqueue` を通す**。
  例外は 7.6.3 の teardown ドレインのみ（`Update` が回らないため同期発火する）
- これにより (1) 既存 Android / iOS Manager と配送経路が揃い、(2) 同期 API のスタック内で購読者が Manager へ再入することがなくなり、
  (3) 「ネイティブコールバック内から `uninit` / `setClipboardHistoryCallbacks` を呼ばない」というネイティブ契約（2.2）を購読者経由でも破れなくなる

**配送タイミングの正確な契約（M3 の反映）**:

`UnityMainThreadDispatcher.Update` は `while (_executionQueue.Count > 0)` で**キューが空になるまで**実行するため、
実行中の action が新たに `Enqueue` したものも同じ `Update` 内で処理される。また Manager の API を dispatcher の `Update` より前に呼べば、
同じフレームの `Update` で配送される。したがって「次フレームで発火する」は保証できない。

- 保証するのは「**呼び出し元 API またはネイティブコールバックのスタックの外側で配送される**」ことのみ
- 同一フレーム内で配送されることがある（dispatcher callback の中から Clipboard API を呼んだ場合を含む）
- 配送順序は enqueue 順（FIFO）で、1 つの結果については必ず 共通イベント → 個別 callback の順
- XML コメントとテストをこの契約に揃える。厳密な「次フレーム」が必要になった場合のみ Manager 固有のフレーム staging を追加検討する（本計画では採らない）
- `Runtime/Common/UnityMainThreadDispatcher.cs` は**変更しない**

### 7.2 スレッド契約

| 対象 | 契約 |
|---|---|
| 全 public API | **Unity メインスレッド限定**。他スレッドからの呼び出しは `MainThreadRequired`(1002) で即時失敗を返す |
| メインスレッド判定 | `Awake` で `s_mainThreadId = Thread.CurrentThread.ManagedThreadId` を捕捉して比較する。`s_dispatcher` も `Awake` でキャッシュする（`Instance` getter は GameObject を生成するためスレッドガードできず、teardown 中に呼ぶと破棄済み環境で GameObject を作ってしまう） |
| 所有 UI スレッド | `Initialize` を呼んだ Unity メインスレッドをネイティブの所有 UI スレッドとして採用する。Player のメッセージポンプがネイティブの隠しウィンドウのメッセージも処理する前提（V-2） |
| ネイティブコールバック | 所有 UI スレッド（= Unity メインスレッド）に届くが、Player ループ内のポンプ処理中に届く。配送は 7.1 のとおり dispatcher 経由 |
| 遅延レンダリング provider | **例外**。`WM_RENDERFORMAT` / `WM_RENDERALLFORMATS` 処理中に同期的に値を返す必要があるため dispatcher を使わない（7.7） |
| teardown ドレイン | **例外**。`Update` が回らないため同期発火する（7.6.3） |

**書き込み系の直列化（ネイティブ側の報告事項）**: `copy*` / `reserveDeferredFormats` / `recoverDeferredState` は
ネイティブ内部で同一の self-write mutex を共有するため互いに待つ。本設計では呼び出しが Unity メインスレッドに限定されるため
競合相手は主にネイティブ内部の経路だが、**同期 API がメインスレッドを短時間ブロックし得る**ことを XML コメントに明記する。
大きな DIB / カスタムデータの連続コピーはフレーム落ちの要因になり得る。

ネイティブの同期 API は任意スレッド可だが、C# 層はイベント配送・リクエスト表・ライフサイクル管理を単純化するためメインスレッド限定とする（設計判断）。
7.6.1 の「登録前に完了が走らない」という安全性もこの制約に依存する。

### 7.3 初期化（`Initialize`）

0. **状態確認（COM / ネイティブに触れる前に行う）**
   - `Running` なら、**何もせずに冪等成功を返す**。`s_comOwnership` / `enableChangeEvents` / delegate 登録のいずれも変更しない（A1 指摘の反映）
   - `Draining` / `ShutdownFailed` なら `ShuttingDown`(1011) で拒否する（7.4 の「`Initialize` を許可する状態」）
   - `Uninitialized` / `ShutDown` のときだけ以降の手順へ進む
1. メインスレッド確認 → 失敗時 `MainThreadRequired`(1002)
2. 破棄済み確認 → `ManagerDestroyed`(1003)
3. `Application.platform != RuntimePlatform.WindowsPlayer` → `PlatformUnavailable`(1000) を返し、イベント / callback を発火（Editor では DLL の meta が Editor 無効のため必ずここで止まる）
4. アパートメント確認（COM 所有権の記録を含む。M2 の反映）:

   **実機確認済み（2026-09-05）**: Unity 6000.4.2f1 の Windows Player メインスレッドは `CoGetApartmentType` = `APTTYPE_MAINSTA`(3)、
   すなわち**初期化済み STA** であり、通常経路では下記の `CoInitializeEx` フォールバックには入らない（`s_comOwnership` は常に `None`）。
   以下はサポート外の構成・将来の Unity バージョン変更に備えた**防御的経路**として実装する。

   - `CoGetApartmentType` が `S_OK` かつ `Sta` / `MainSta` なら**本層は何も初期化していない**（`s_comOwnership` は `None` のまま）。そのまま次へ（**実機ではここを通る**）
     - **不変条件: `s_comOwnership` は `None` のときだけ書き換える。** 既に `Initialized` / `RefCounted` を保持している状態でこの分岐に入っても
       `None` へ上書きしない。上書きすると、fallback で獲得した参照を shutdown 時に解放できなくなる（A1 指摘の反映）。
       手順 0 により `Running` 中は本手順に到達しないが、二重の防御として不変条件を実装する
   - それ以外なら `CoInitializeEx(IntPtr.Zero, 0x2 /* COINIT_APARTMENTTHREADED */)` を 1 回だけ試行し、結果を記録する
     - `S_OK`: 本層がアパートメントを確立した（`s_comOwnership = Initialized`）
     - `S_FALSE`: 既に初期化済みのスレッドで参照カウントだけ増えた（`s_comOwnership = RefCounted`）
     - `RPC_E_CHANGED_MODE`（MTA 確定）およびその他の失敗は **`ApartmentUnavailable`(1009)** を返して終了（回復不能）
     - ネイティブの 18 `WrongApartment` は「ネイティブが返したコード」に限定し、C# 側で捏造しない
   - V-1 は **2026-09-05 の実機スパイクで解決済み**（`MAINSTA`。10 章）

**COM 初期化の解放方針（`CoUninitialize`）**:

Microsoft のドキュメントは `S_FALSE` を含む成功した `CoInitializeEx` に対応する `CoUninitialize` を要求している。本設計はこれに従い、
**本層が獲得した参照のみを、同一スレッド上で、ネイティブの cleanup 完了後に解放する**。

| 状態 | 解放 | 理由 |
|---|---|---|
| `None`（観測しただけ） | **しない** | 本層は参照を獲得していない。解放すると他者の参照を壊す |
| `RefCounted`（`S_FALSE`） | `TryShutdown` が `completed == true` を返した**後**に 1 回だけ `CoUninitialize` | 参照カウントの釣り合いを取る。他者のアパートメントは維持される |
| `Initialized`（`S_OK`） | 同上 | 本層が確立したアパートメントを本層が畳む |
| shutdown がタイムアウト / 終端失敗 | **しない**（保持したまま終了） | ネイティブがまだ COM オブジェクトと隠しウィンドウを保持している可能性があり、先に畳むと未定義動作になる。`Debug.LogWarning` に残す |

**ownership の遷移（すべての経路を網羅する。A1-3 の反映）**

`CoInitializeEx` が成功した直後に `initClipboardManager` が失敗すると、`_initialized` が false のままとなり通常の
`TryShutdown` 経路に乗らない。この場合に本層が獲得した COM 参照を解放する経路が無いと参照が残り続けるため、
**初期化失敗はその場で解放する**。

| きっかけ | ネイティブの状態 | COM の扱い | 遷移後 |
|---|---|---|---|
| `CoGetApartmentType` が STA を返した | 未初期化 | 何も獲得していない | `None` |
| `CoInitializeEx` が `S_OK` / `S_FALSE` | 未初期化 | 獲得（`Initialized` / `RefCounted`） | 同左 |
| `initClipboardManager` が `pError == 0` | 初期化済み | 保持し続ける | 変化なし |
| **`initClipboardManager` が非 0 で失敗** | ネイティブ側が自力でロールバック済み（HWND / coordinator を解放し `initialized_ = false`） | **その場で同一スレッドから解放** | `None` |
| **`initClipboardManager` の P/Invoke が例外** | 状態不明。`BridgeUnavailable`(1001) を返す | **その場で解放**（ネイティブは何も保持していないか、そもそもロードされていない） | `None` |
| `TryShutdownCore` が `completed == true` | 解放済み | 解放 | `None` |
| shutdown タイムアウト / 終端失敗 | 保持したまま | **解放しない** | 変化なし |

- 解放は `s_comOwnership != None` のときだけ行い、直後に `None` へ戻す（二重解放を防ぐ）
- 解放は必ず**獲得したのと同じスレッド**（Unity メインスレッド）から行う
- `OnDestroy` の best-effort 経路は `completed == true` を得られたときにのみ解放する
- 実機では `s_comOwnership` は常に `None`（V-1 で `MAINSTA` を確認済み）であり、この表は防御的経路の規定である
- この規則は 7.10 の `ResetCore` にも反映する（PlayMode テストでの繰り返し初期化に耐えるため）
5. `initClipboardManager(enableChangeEvents ? s_changedDelegate : null, out pError)`
6. `pError == 0` で `_initialized = true`。同一スレッドからの再呼び出しは冪等成功（ネイティブ契約）

**`enableChangeEvents` の帰結（必ず XML コメントに書く）**:

| 値 | ネイティブの挙動 | 帰結 |
|---|---|---|
| `true` | `ClipboardWatcher::Start` が失敗すると **init 全体が `MONITOR_REGISTER_FAILED`(12) で失敗する** | 変更監視が使えないことを呼び出し側が確実に知れる |
| `false` | 監視の失敗を握り潰して init は成功する | `ClipboardChanged` は永久に発火しない。後から有効化する手段は `uninit` → `init` のみ |

`Awake` では `_ = UnityMainThreadDispatcher.Instance;` と `s_mainThreadId` / `s_dispatcher` の捕捉のみ行い、**自動初期化はしない**。

### 7.4 終了（`TryShutdown` / `ShutdownWithDrain`）

**規約逸脱の宣言**: `uninitClipboardManager` は同期 API のため、common.md「Bridge 層で同期・非同期を変換しない」に従い**同期版を基本形とする**。
ただし「TRUE を返すまでメッセージポンプを回す」という契約は 1 フレーム内では満たせないため、フレームを跨ぐドレインを**別名の Unity 側ヘルパ**として併設する。
これは Bridge 層での同期→非同期変換ではなく、同期 API を複数フレームにわたり再呼び出しするループの提供である。代替案（同期のままスピンで待つ）は UI スレッドを止めポンプも回らないためデッドロックするので採らない。

```csharp
// 内部 core: public API の tombstone / initialized ガードを通さない。
// メインスレッド判定とプラットフォームガードだけを行い、ネイティブへ必ず到達する。
private WindowsClipboardResult TryShutdownCore(out bool completed);

// 基本形（ネイティブと同じ形）。事前チェックは「メインスレッド」のみ。
public WindowsClipboardResult TryShutdown(out bool completed);

// フレーム跨ぎヘルパ（内部で TryShutdownCore をコルーチンから繰り返す）
public void ShutdownWithDrain(Action<WindowsClipboardResult>? onResult = null);
```

**内部 core を分ける理由（A1 指摘の反映）**

`Draining` は新規操作を `ShuttingDown`(1011) で拒否し、`OnDestroy` は tombstone をネイティブ呼び出しより前に立てる。
この 2 つと 7.5 の通常の事前チェック（破棄済み → 初期化済み）を素直に組み合わせると、
**shutdown 自身が `ManagerDestroyed` / `NotInitializedByHost` で拒否され、ネイティブに到達しない**。
そこで shutdown 経路だけは core を直接呼ぶ。

| 呼び出し元 | 経路 | tombstone / initialized ガード |
|---|---|---|
| `ShutdownWithDrain` のリトライループ | `TryShutdownCore` | 迂回する |
| `OnDestroy`（tombstone 設定後） | `TryShutdownCore` | 迂回する |
| `wantsToQuit` ドレイン | `TryShutdownCore` | 迂回する |
| public `TryShutdown` | メインスレッド判定 → `TryShutdownCore` | 迂回する。`Uninitialized` / `ShutDown` では何もせず**成功**（`completed == true`）を返す（ネイティブ `Uninit` の冪等性に合わせる） |

`TryShutdownCore` が行う事前チェックはメインスレッド判定とプラットフォームガードのみで、
ネイティブが未初期化なら `uninitClipboardManager` は `TRUE` + `NONE` を返す（冪等）ため、余分なガードは不要である。

ドレインの規則:

- リトライは最大 N フレーム（既定 60）または T 秒（既定 2 秒）。`yield return null` でポンプを回す
- **`completed == false` のときの `pError` は「未完了」と「終端失敗」に分類する**（M4 の反映。すべてを抑制すると本物の失敗を隠す）

| `pError` | 分類 | 扱い |
|---|---|---|
| 3 `Busy` / 12 `MonitorRegisterFailed` / 15 `Canceled` | 未完了 | ログのみ。リトライを続ける |
| 13 `PartialState` | 未完了（要回復） | `recoverDeferredState` を 1 回試してからリトライを続ける |
| **14 `WrongThread`** | **終端失敗** | リトライしても永久に完了しない。即座に失敗結果で打ち切る |
| 0 `None` + `completed == false` | 未完了 | リトライを続ける |
| その他 | 終端失敗 | 即座に失敗結果で打ち切る |

- 上限超過時は `ShutdownTimeout`(1006) で完了させる

**shutdown 状態機械（不足項目の反映）**:

```
Uninitialized --Initialize 成功--> Running --ShutdownWithDrain 開始--> Draining --完了--> ShutDown
                                      ^                                   |
                                      +------- 終端失敗 / タイムアウト -----+（Running へは戻らない）
```

| 状態 | 新規操作（shutdown 以外）の受付 | `_initialized` | ネイティブ資源 |
|---|---|---|---|
| `Uninitialized` | 拒否（`NotInitializedByHost`(1004)） | false | なし |
| `Running` | 受付 | true | 保持 |
| `Draining` | **拒否**（`ShuttingDown`(1011)） | false | 保持 |
| `ShutDown` | 拒否（`NotInitializedByHost`） | false | 解放済み |
| `ShutdownFailed`（終端失敗 / タイムアウト） | 拒否（`ShuttingDown`） | false | **保持したまま**（ネイティブが所有し続けるため C# 側の delegate・provider・キャッシュも解放しない） |

#### 終端関数 `FinishShutdownAttempt`（全 shutdown 起点の単一経路）

**shutdown の後始末を呼び出し元ごとに書かない。** `TryShutdownCore` の 1 回の試行が終わるたびに、起点を問わず必ず次を通す。

```csharp
private enum ShutdownOrigin { PublicApi, Drain, Destroy, Quit }

// core の 1 回の試行結果を受け取り、状態・レジストリ・所有権・quit フラグを一括で決める。
private static void FinishShutdownAttempt(ShutdownOrigin origin, WindowsClipboardResult result, bool completed);
```

| 段階 | 実施内容 |
|---|---|
| 1. 初回試行の入口 | 状態が `Running` なら **`Draining` へ遷移し、その場でレジストリを同期ドレインする**（7.6.3）。以後の新規操作は `ShuttingDown`(1011) で拒否。ドレインはユーザー callback を呼ぶ前に拒否へ切り替えてから行う |
| 2. 結果の分類 | `completed == true` → 完了 / `completed == false` かつ未完了コード → 継続 / 終端失敗コード（`WrongThread` 等）またはタイムアウト → 終端失敗（分類は本節の表） |
| 3. 所有権の解放 | **`completed == true` のときだけ** COM 参照（7.3）と `s_renderProviders` / `s_renderCache` を解放する。delegate の GC ルートは `static readonly` のまま解放しない: 終端失敗のときネイティブはポインタを保持し続けるため、解放できてしまう方が危険 |
| 4. 状態の確定 | 完了 → `ShutDown` / 継続 → `Draining` / 終端失敗・タイムアウト → `ShutdownFailed`（ネイティブ資源は保持） |
| 5. quit の再開 | `origin == Quit` なら**成功・タイムアウト・終端失敗のすべてで** `s_quitDrainCompleted = true` を立て、結果をログに残して `Application.Quit()` を再実行する |
| 6. 結果配送 | **`ShutdownWithDrain` が呼ばれた drain だけ**が、最終結果を共通イベントと per-call callback へ配送する。判定は起点ではなく「誰かが結果を待っているか」で行う（`DrainSession.DeliveryRequested`）。`PublicApi`（= `TryShutdown`）は戻り値だけで結果を返し、event も callback も発火しない（5.3 の例外規定と一致）。quit 単独で始まった drain も配送しない。ただし **`ShutdownWithDrain` に quit が後から合流した場合は配送する** — 呼び出し元は結果を待っており、quit が来たことはその約束を取り消さない |

この 1 関数に集約することで、次の 3 つが起点によらず成立する。

- **レジストリの exactly-once**: どの起点でも初回試行時にドレインされるため、public `TryShutdown` が即完了しても `Awaitable` が取り残されない
- **所有権の解放条件の一致**: 「`completed == true` のときだけ解放」がひとつの場所にしかない
- **quit が必ず再開する**: 終端失敗でも `s_quitDrainStarted` が立ったまま残らない

| 呼び出し時の状態 | core の結果 | 遷移後 | レジストリ | COM / provider |
|---|---|---|---|---|
| `Running` | 完了 | `ShutDown` | 初回ドレイン済み | 解放 |
| `Running` | 未完了 | `Draining` | 初回ドレイン済み | 保持 |
| `Running` | 終端失敗 | `ShutdownFailed` | 初回ドレイン済み | 保持 |
| `Draining` | 完了 | `ShutDown` | 済み | 解放 |
| `Draining` | 未完了 | `Draining` | 済み | 保持 |
| `Draining` | 終端失敗 / タイムアウト | `ShutdownFailed` | 済み | 保持 |
| `ShutdownFailed` | 完了 | `ShutDown` | 済み | 解放 |
| `ShutdownFailed` | 未完了 / 終端失敗 | `ShutdownFailed` | 済み | 保持 |
| `ShutDown` / `Uninitialized` | 何もせず完了扱い | 変化なし | 済み / 不要 | 変化なし |

**`Initialize` を許可する状態（A1 指摘の反映）**

| 遷移元 | 可否 | 理由 |
|---|---|---|
| `Uninitialized` / `ShutDown` | **許可** | ネイティブは `initialized_ == false` から通常の初期化を行い、`lifecycle_.Reopen()` に到達する |
| `Draining` / `ShutdownFailed` | **拒否**（`ShuttingDown`(1011)） | ネイティブ `Uninit` は `CloseAndDrain()` で lifecycle gate を閉じてから `FALSE` を返すことがあり、そのとき `initialized_` は true のまま残る。この状態で `initClipboardManager` を呼んでも**冪等成功を返すだけで `Reopen()` に到達しない**ため、C# だけ `Running` に戻り、以後の全操作が `NOT_INITIALIZED` になる |

`ShutdownFailed` から復帰したい場合は、**shutdown を再試行して `completed == true`（= `ShutDown`）を得てから** `Initialize` を呼ぶ。
tombstone が立っている場合は状態に関わらず `ManagerDestroyed` が優先する。
- **重複 Manager の破棄が稼働中の singleton を shutdown しないこと**: `OnDestroy` の 1 行目を `if (_instance != this) return;` にする（`IosClipboardManager` の前例）
- **キャッシュ（7.7 の provider 辞書とレンダリング結果）のクリアは、`TryShutdown` が `completed == true` を返した後に限定する。**
  `uninit` 内の `DestroyWindow` → `WM_RENDERALLFORMATS` で provider が同期再入するため、先にクリアすると予約形式が失われる（2.6 / 2.8 D-7）

**プロセス終了前の保証（`Application.wantsToQuit`）**:

```csharp
private static bool s_quitDrainStarted;    // 再入防止
private static bool s_quitDrainCompleted;  // 2 回目の wantsToQuit を通すためのフラグ
```

- `Application.wantsToQuit += OnWantsToQuit;` の購読は **`s_quitHandlerSubscribed` フラグで 1 回だけ**行う。
  ネイティブの `initClipboardManager` は同一スレッドからの再呼び出しを冪等成功として返すため、`Initialize` は複数回成功し得る。
  フラグが無いと同じ handler が複数登録され、`OnDestroy` の 1 回の `-=` では購読が残る（中優先度指摘の反映）
- **`OnDestroy` と `ResetCore` で必ず解除し、フラグを false に戻す**（static event のため購読が残るとドメインをまたいで生き続ける）
- `OnWantsToQuit`:
  1. `s_quitDrainCompleted == true` なら `true` を返す（終了を通す）
  2. `s_quitDrainStarted == true` なら `false` を返す（進行中。多重起動しない）
  3. それ以外は `s_quitDrainStarted = true` としてドレインコルーチンを開始し `false` を返す
  4. 終了の再開は `FinishShutdownAttempt` の段階 5 が行う。**成功・`ShutdownTimeout`・終端失敗のすべてで** `s_quitDrainCompleted = true` を立てて `Application.Quit()` を再実行する
- **`ShutdownTimeout` でも終端失敗でも終了は通す**（アプリが終了できなくなる事態を避ける。いずれも `Debug.LogError` に残す）
- `OnDestroy` は最後の砦として **`TryShutdownCore` を 1 回だけ呼び、結果を `FinishShutdownAttempt(ShutdownOrigin.Destroy, ...)` に渡す**
  （best-effort。この経路ではフレームが進まないため完了しない可能性がある）

**ネイティブ側サンプルとの対比（F2）**

ネイティブの WinUI サンプルは同じ問題（予約したまま終了すると内容が失われる）を `MainWindow.Closed` で `uninitClipboardManager` を
**1 回だけ呼び、`FALSE` でも再試行しない**方法で解決した。閉じ中のウィンドウではメッセージポンプを回せず、再試行するとアプリ終了をブロックするためである。
それでも「Reserve → 終了 → メモ帳へ貼り付け」は成功している。

- Unity では `Application.wantsToQuit` が `false` を返す間もプレイヤーループが回るため、**ポンプを回しながら再試行できる**（本設計が採る方法）
- ただしこれは V-3 として未検証である。**再試行しても `completed` にならない場合は、ネイティブサンプルと同じ「1 回だけ呼んで終了を通す」に退避する**
  （`ShutdownTimeout` で quit を通す既定動作がそのまま退避策になっている）
- ドレインにより未完了リクエストは `CANCELED` でコールバックされる。これを含む配送は 7.6.3 の teardown ドレイン契約に従う

### 7.5 同期 API の実装方針

- 事前チェックの順序: メインスレッド → 破棄済み → プラットフォーム → 初期化済み（`NotInitializedByHost`(1004) で C# 側で止め、ネイティブへは到達させない）→ 引数検証（`InvalidArgument`(1005)）
- **例外: shutdown 経路（`TryShutdown` / `ShutdownWithDrain` / `OnDestroy` / quit ドレイン）はこの事前チェックを通さず `TryShutdownCore` を使う**（7.4）。
  通常のガードを通すと、tombstone や `Draining` によって shutdown 自身が拒否されてネイティブに到達しなくなるため
- 書き込み系: 引数検証 → ネイティブ呼び出し → 結果生成 → 配送（dispatcher）→ 戻り値を返す
- 読み出し系は **2 回呼び出しバッファ規約**。
  **戻り値 0 を「空」と解釈してはならない**（v2 の規則を撤回。`PasteFiles` / `PasteImage` / `PasteCustomFormat` は lease 取得失敗や内部読み出し失敗でも 0 を返すため、
  `NotInitialized` / `Busy` / `InvalidData` が「空の成功」に化ける。2.9）

**1 回目の呼び出し（`buffer = IntPtr.Zero, bufferSize = 0`）の分類順序**:

1. **まず `pError` を分類する**（戻り値は見ない）
   - `EMPTY`(4) / `FORMAT_UNAVAILABLE`(5) → **空の成功**（`IsSuccess = true`, `IsEmpty = true`）
   - `NONE`(0) → 戻り値 0 なら空の成功、1 以上なら 2 回目へ（文字列 API は終端分を含むため通常 1 以上）
   - `BUFFER_TOO_SMALL`(7) → 戻り値 0 なら**バイト系 API のみ**空の成功（長さ 0 のデータ。2.9）。
     文字列系 API で戻り値 0 + `BUFFER_TOO_SMALL` は起こらないため、起きた場合は `Unknown` として失敗にする。戻り値 1 以上なら 2 回目へ
   - **上記以外はすべて失敗**（`NOT_INITIALIZED` / `BUSY` / `INVALID_DATA` / `OUT_OF_MEMORY` / `WRONG_THREAD` など）。戻り値が 0 でも空に正規化しない
2. 2 回目が必要な場合のみ `Marshal.AllocHGlobal`（文字列は `size * 2` バイト）
3. **2 回目の呼び出しでも、アンマネージドメモリを読む前に `pError` を検査する**
   - `NONE` 以外なら読まずに失敗として返す（`EMPTY` / `FORMAT_UNAVAILABLE` は間にクリップボードが変わった場合であり空の成功として扱う）
   - `BUFFER_TOO_SMALL` の場合（間にクリップボードが変わった）は最大 2 回まで再確保して再試行し、超過は失敗として返す
4. `finally` で必ず `FreeHGlobal`

**`IsEmpty` の判定条件（API 別）**:

| API | 空の成功とする条件 | 失敗として保持する条件 |
|---|---|---|
| `PastePlainText` / `PasteHtml` | `EMPTY` / `FORMAT_UNAVAILABLE`、または `NONE` + サイズ 0 | 他のすべての非 `NONE`（サイズ 0 でも） |
| `PasteFiles` | 同上、またはパース成功後の要素数 0 | 同上。パース失敗は `ResultParseFailed`(1007) |
| `PasteImage` / `PasteCustomFormat` | `EMPTY` / `FORMAT_UNAVAILABLE`、`NONE` + サイズ 0、**`BUFFER_TOO_SMALL` + サイズ 0** | 同上 |
| `GetFormats` | パース成功後の要素数 0（**`EMPTY` は返らない**） | 同上 |
| `GetPreferredFormat` | 取得文字列が空文字（**`EMPTY` は返らない**。2.9） | 同上 |

**`HasFormat` の結果型**: `IsSuccess`（判定できたか）と `HasFormat`（存在するか）を**独立したプロパティ**にする。
ネイティブは失敗時も `FALSE` を返すため、`IsSuccess == false` のとき `HasFormat` は意味を持たない旨を XML コメントに明記する。

クリップボード内容そのものはログに出さない（3.2 の Manager 規約に従い、長さ・形式名・エラーコードのみ）。

### 7.6 非同期（履歴）API の実装方針

#### 7.6.1 リクエスト表（完了配送レジストリ）

**v2 の問題（H2）**: `OnRequestCompleted` が「表から取り出す → dispatcher へ enqueue」する形だと、
enqueue 済みで `Update` 未実行のまま teardown が起きた結果（完了済み・未配送）が、どちらのドレイン対象にもならず消える。
受付前拒否（requestId を持たず enqueue だけされる）も同じ穴に落ちる。

**解決**: リクエスト表を「ネイティブ待ち」だけでなく **配送完了までを追跡するレジストリ**にする。

```csharp
private static readonly ClipboardRequestCallback s_requestDelegate = OnRequestCompleted;  // GC ルート
private static readonly WindowsClipboardRequestTable s_registry = new();                  // メインスレッドのみが触る
```

エントリの状態:

| 状態 | 意味 | 遷移契機 |
|---|---|---|
| `AwaitingNative` | 受付済み。ネイティブ完了待ち（native requestId を持つ） | 受付成立時に登録 |
| `Undelivered` | 完了値が確定したが、まだ callback / event を呼んでいない（dispatcher に enqueue 済み） | `OnRequestCompleted`、または受付前拒否の登録時 |
| （削除） | 配送完了 | `TryClaim` に成功した経路が配送した直後 |

- エントリは **ticket（C# 側で採番する内部 ID）** をキーにする。native requestId は `AwaitingNative` の間だけ副キーとして持つ
  （受付前拒否は native requestId を持たないため、ticket が無いと追跡できない）
- 配送は必ず `TryClaim(ticket)` を通す。**成功した経路だけが callback / event を呼ぶ**（exactly-once）
- teardown 後に遅れて走る enqueue 済み action は `TryClaim` に失敗し、**無害な no-op** になる
- 呼び出し手順: 事前チェック（7.5 と同じ）→ in-flight ガード（7.6.4）→ ticket 発行 → `getClipboardHistory(s_requestDelegate, out pError)` → `requestId != 0` なら `AwaitingNative` で登録して return
- **登録タイミングの安全性の根拠**: 完了そのものは `PostMessage` ではなく WinRT continuation から直接呼ばれる（2.5）。安全なのは、
  **リクエストの開始が `PostMessage(WM_APP_CLIPBOARD_REQUEST)` 経由であり、かつ呼び出し元が所有 UI スレッド自身（7.2 のメインスレッド限定）であるため、
  Bridge 呼び出しから return するまでの間はメッセージがポンプされない**から。非メインスレッドからの呼び出しを許すとこの前提は崩れる
- `requestId == 0`（受付前失敗）: **ticket を `Undelivered` として登録してから** dispatcher へ enqueue する。`pError == 0` は理論上到達しないが `RequestRejected`(1008) で防御する
- `GetHistory` の完了時は `WindowsClipboardJsonParser` が JSON 配列を `WindowsClipboardHistoryItem` の列へ変換し、`WindowsClipboardHistoryResult` に載せる。
  `GetHistoryAvailability` は同様に `WindowsClipboardAvailabilityResult` を作る
- `OnRequestCompleted(requestId, error, json)`: native requestId から ticket を引き（未知 ID は警告ログのみ）、JSON をパースし、
  **エントリを削除せずに `Undelivered` へ遷移させてから** dispatcher へ enqueue する。実際の配送時に `TryClaim` で初めて削除する
- 二重完了（ネイティブの `CANCELED` と通常完了が競合する等）は `TryClaim` で 1 回に収束する

#### 7.6.2 `Awaitable` 版

- `AwaitableCompletionSource<T>` で callback 版を包む**薄いラッパー**とし、ネイティブ呼び出しロジックを重複させない
- 併設の前提条件は 7.6.3（teardown ドレイン）と 7.6.4（in-flight ガード）の両方で満たす。
  どちらかが未実装の段階では `Awaitable` 版を公開しない
- `Awaitable<T>` は 1 回しか await できないため、戻り値を保持せず呼び出しごとに `XxxAsync` を呼ぶ旨を XML コメントに明記する

#### 7.6.3 teardown ドレイン契約（新規）

**問題**: 通常経路の配送は dispatcher 経由だが、`OnDestroy` 後・Quit 確定後・PlayMode 終了時は `Update` が回らない。
pending の per-call callback と `AwaitableCompletionSource` が完了しないまま捨てられる。

**契約**:

- teardown は **`FinishShutdownAttempt` の初回試行（起点を問わず。7.4 の段階 1）** と `ResetForTests` で起きる。
  public `TryShutdown` が 1 回で完了した場合もここに含まれる（A1 指摘の反映）。teardown では、
  レジストリに残る **`AwaitingNative` と `Undelivered` の両方**を **dispatcher を経由せず同期的に**配送する（H2 の反映）
  - `Undelivered` は確定済みの結果をそのまま配送する（結果を上書きしない）
  - `AwaitingNative` は `Canceled`(15)（ネイティブのドレインで既に値が届いていればそれを優先）または `ManagerDestroyed`(1003) で完了させる
- 配送はすべて `TryClaim` を通すため、**後から走る enqueue 済み action は no-op になり二重配送しない**
- 順序: **新規操作の拒否へ切り替える（`Draining`）→ 同期ドレイン → ユーザー callback 呼び出し**。
  callback 内から新しい操作を呼ばれても受け付けない（7.4 の状態機械）
- ドレインは**冪等**（複数回呼んでも `TryClaim` により 1 回に収束する）
- **ネイティブの `CANCELED` 配送に依存しない。** 「受付済みリクエストが `uninit` で `FALSE + CANCELED` になるか」は
  ネイティブ側でも未検証（native `9.8`）。本設計はレジストリ側で完結させるため、ネイティブが `CANCELED` を配送してもしなくても
  結果はちょうど 1 回配送される（配送された場合は `TryClaim` で二重配送が抑止される）
- この同期発火は 4 章「dispatcher 経由」ルールの明示的な例外であり、理由（`Update` が回らない）をコードコメントに書く
- これにより「受付済み・受付前拒否を問わず、結果は必ず 1 回だけ配送される」（8.4）が teardown を含めて成立し、`Awaitable` の永久未完了が発生しない

#### 7.6.4 同時実行の判定と in-flight ガード

common.md の要求に従い、操作ごとに OS 上の同時実行可否を判定する。

| 操作 | 同時実行 | 判定理由 | ガード |
|---|---|---|---|
| `GetHistory` | 許可 | 読み取り専用。requestId で結果が分離され副作用も無い | なし |
| `GetHistoryAvailability` | 許可 | 同上 | なし |
| `RestoreHistoryItem` | **不可** | クリップボード内容を置き換えるため、並行実行すると最後に完了したものが勝ち、呼び出し側の意図と食い違う。**成功時は `ClipboardChanged` も発火する**（履歴サービスが書き込むため。2.2） | 操作単位の in-flight ガード |
| `DeleteHistoryItem` | **不可** | 履歴を変更する。並行実行時の順序が不定 | 同上 |
| `ClearUnpinnedHistory` | **不可** | 同上 | 同上 |

- ガードは**操作種別ごと**（`RestoreHistoryItem` と `DeleteHistoryItem` は同時に走ってよい）
- 進行中の再呼び出しは、進行中の操作をキャンセルせず、**新しい呼び出し側へ `OperationBusy`(1010) を即時に返す**（既存の呼び出しの結果は必ず届ける）
- ガードは callback 版に実装し、`Awaitable` 版は薄いラッパーのままにする
- ガードの解除は完了配送時（teardown ドレインを含む）

#### 7.6.5 キャンセル

- callback 版: `CancelRequest(uint requestId)` を使う
- `Awaitable` 版: requestId を返せないため `CancellationToken` を最終引数で受ける。MonoBehaviour からは `destroyCancellationToken` を渡す運用を XML コメントに例示する
- `cancelClipboardRequest` が `false` を返しても完了は届き得るため、レジストリからは削除しない

**`CancellationToken` の実行スレッドと登録寿命（M1 の反映）**:

`CancellationToken` の callback は**キャンセルを実行したスレッドで同期的に走る**ため、Unity メインスレッドとは限らない。
そこから直接メインスレッド限定の `CancelRequest` を呼ぶと `MainThreadRequired`(1002) になり、ネイティブのリクエストが残り続ける。

- `token.Register(...)` の callback では**キャンセル要求を `s_dispatcher.Enqueue` でメインスレッドへ送るだけ**にする（`Awake` でキャッシュ済みの dispatcher を使う。`Instance` getter は呼ばない）
- メインスレッドで実行される際に、**ticket と generation が現在も有効か確認してから** `cancelClipboardRequest` を呼ぶ
  （その ticket が既に配送済み・再利用済みの場合、無関係な新しいリクエストをキャンセルしてしまうため）
- **すでにキャンセル済みのトークン**が渡された場合は、ネイティブを呼ばずに `Canceled`(15) で即座に完了させる（受付前拒否と同じ経路。ticket を `Undelivered` に登録してから配送する）
- `CancellationTokenRegistration` は **完了配送時・受付前拒否時・teardown 時のすべての経路で `Dispose` する**（登録が生き残ると `destroyCancellationToken` に紐づいて Manager が解放されない）
- ワーカースレッドからのキャンセルと通常完了が競合しても、`TryClaim` により結果は 1 回だけ配送される
- 参考: [Cancellation in Managed Threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) / [CancellationToken.Register](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken.register)

### 7.7 遅延レンダリング（`ReserveDeferredFormats`）

```csharp
public WindowsClipboardResult ReserveDeferredFormats(
    IReadOnlyDictionary<string, Func<byte[]>> providers,
    Action<WindowsClipboardResult>? onResult = null);   // options 引数は無い（2.1 / 2.3）
```

**状態の保持**:

```csharp
private static readonly ClipboardRenderCallback s_renderDelegate = OnRenderFormat;   // GC ルート
private static Dictionary<string, Func<byte[]>> s_renderProviders = new();           // 有効な provider（世代マージ済み）
private static readonly Dictionary<string, byte[]> s_renderCache = new();            // 1 回目の結果を 2 回目で再利用
```

- `s_renderDelegate` は static のため、そこから到達する `providers` 辞書も **static に保持する**（`s_renderProviders`）。
  ユーザーの `Func<byte[]>` を static に保持することは、shutdown 完了まで参照が生き続けることを意味する（XML コメントに明記）
- `context` には `IntPtr.Zero` を渡す（形式名でルーティングするため不要）

**予約の置き換え規則（H3 の反映。D-9 の表に対応）**:

`reserveDeferredFormats` が失敗しても、失敗地点によっては **native 側の旧 renderer が生き残る**。
C# 側が先に `s_renderProviders` を差し替え / クリアすると、生き残った native renderer に対応する provider が無くなり、
以後のレンダリング要求が `Unknown` で落ちる（= 予約済み形式が黙って消える）。

- **新しい辞書は一時変数に組み立て、ネイティブ呼び出しが成功するまで `s_renderProviders` に反映しない**
- 判定は **`pError` だけで決まる**形にする。`UNKNOWN`(19) から失敗地点を判別する規則は C ABI 上成立しないため採らない（2.8 D-9。A1 指摘の反映）

| ネイティブの結果 | native が保持する renderer | `s_renderProviders` | `s_renderCache` |
|---|---|---|---|
| `NONE`(0) 成功 | 新世代 | **新世代へ差し替える** | 全消去（新しい予約のため） |
| `PARTIAL_STATE`(13) | 新世代（partial） | **新世代へ差し替える。**ただし旧世代のうち新世代に無い形式名は残す | 全消去 |
| **上記以外のすべての失敗**（1 / 3 / 8 / 14 / 19 …） | **旧世代のまま、または空** | **旧世代を変更しない**（新世代は破棄する） | **変更しない** |

- 「上記以外」で旧世代を維持してよい根拠: D-9 のとおり、`NONE` と `PARTIAL_STATE` 以外の経路で native が新世代を保持することは無い。
  旧世代を残しておけば、生き残った旧 renderer からの要求を必ず解決できる。空になっている場合は誰も要求しないため余分な保持は無害である
- 同じ形式名が旧・新に存在する場合も、この規則なら **native が実際に持っている世代と C# の provider が必ず一致する**
- C# 側の引数検証（空辞書 / 形式名が空白 / provider が null）はネイティブ呼び出し前に行い、その場合は `InvalidArgument`(1005) で**何も変更しない**

**呼び出し中の再入に備えた staging（中優先度指摘の反映）**

native は `EmptyClipboard` の直後、個別の `SetClipboardData` を実行する**前**に `renderers_` を新世代へ置き換える。
その区間で render 要求が同期的に届く可能性は完全には否定できない（履歴サービスが予約直後に実体化するため。D-8）。

- `reserveDeferredFormats` を呼ぶ**直前**に、`s_renderStaging = 旧世代 ∪ 新世代`（衝突は新世代優先）を公開する
- render callback の解決順は `s_renderStaging`（非 null のとき）→ `s_renderProviders`
- P/Invoke から戻ったら上表に従って `s_renderProviders` を確定し、`s_renderStaging` を null に戻す（`finally` で必ず戻す）
- これにより、呼び出し中に旧・新どちらの形式が要求されても解決できる

**`RecoverDeferredState` は provider / cache を解放しない**

native の `RecoverFromPartialState` は **partial でないときも `NONE` を返す**（何もせずに戻る）。
また owner 不一致のときは `Clear()` して `NONE` を返す。つまり **`NONE` は「回復した」ことも「予約が消えた」ことも意味しない**。

- `RecoverDeferredState` の結果に関わらず `s_renderProviders` / `s_renderCache` を消さない
- 解放するのは「新しい予約が `NONE` で成功したとき」と「shutdown が `completed == true` になったとき」だけである

**二相呼び出しの実装（2.8 の D-2 〜 D-5 を厳守）**:

1. 1 回目（`buffer == IntPtr.Zero`）:
   - `s_renderProviders[formatName]()` を実行してバイト列を生成し、**`s_renderCache[formatName]` に必ず保持する**
   - 長さ 0 なら配置できないため（D-3）、`requiredSize = 0` を設定し `InvalidData`(6) を返してログに残す
   - `requiredSize = bytes.Length` を設定し `BUFFER_TOO_SMALL`(7) を返す
2. 2 回目（`buffer != IntPtr.Zero`）:
   - **キャッシュを必ずそのまま使う。再生成は禁止**（D-5。再生成して長さが 1 バイトでも変われば native が黙って破棄する）
   - キャッシュが無い場合は復旧不能なので `Unknown`(19) を返してログに残す（その場で生成して長さが変わる事故を防ぐ）
   - `bufferSize < bytes.Length` なら `requiredSize = bytes.Length` を設定し `BUFFER_TOO_SMALL` を返す（D-4 により native は破棄するが、契約上ここで嘘をつかない）
   - それ以外は `Marshal.Copy` し、**`requiredSize = bytes.Length`（1 回目と同一値）** を設定して `None`(0) を返す
3. provider から例外が出た場合は catch し、`requiredSize = 0` を代入して `Unknown`(19) を返す。ログには形式名のみ記録する

**provider の制約（XML コメントに明記）**:

- Unity API を呼ばない / クリップボード API を呼ばない / ブロックしない / 例外を投げない（D-6）
- `UnityMainThreadDispatcher` は使えない（`WM_RENDERFORMAT` 処理中に同期実行されるため、キューは次の `Update` まで流れずデッドロックする）
- **Unity オブジェクト（MonoBehaviour / Texture / GameObject）を参照しない。** provider は `uninit` 内の `DestroyWindow` から
  シャットダウン進行中に呼ばれ得るため、必要な値はクロージャに**値としてコピーして**閉じ込める（D-7）

**副作用と後始末**:

- **`ReserveDeferredFormats` は既存のクリップボード内容を破棄する**（D-1: native が `EmptyClipboard()` を呼ぶ）。XML コメントの先頭に書く
- `s_renderProviders` / `s_renderCache` のクリアは、**予約が成功したとき**（上表）と、**`TryShutdown` が `completed == true` を返した後**に限定する（7.4）。
  shutdown が未完了・タイムアウトの場合は保持したままにする（native がまだ renderer を持ち、`WM_RENDERALLFORMATS` を送り得るため）
- クリップボード所有権の喪失を C# から観測できないため、それまでバイト列を保持する（既知の制限としてコメントに残す）
- 履歴が有効な場合、予約直後に全形式が実体化される（D-8）
- `RecoverDeferredState` が回復するのは `DeferredClipboard` の partial 状態のみ。`CopyMultipleFormats` の `PARTIAL_STATE` は回復対象外（2.8）

### 7.8 IL2CPP / マーシャリング制約

- コールバック実体はすべて `private static` + `[MonoPInvokeCallback(typeof(...))]`（`using AOT;`）。インスタンスメソッド・ラムダを直接渡さない
- delegate インスタンスは `static readonly` フィールドで保持し GC を防ぐ。ネイティブが保持する期間（`uninitClipboardManager` が TRUE を返すまで）は必ず生存させる
- コールバック内で例外を C ABI 境界へ漏らさない（全体を `try/catch`）。`out` 引数を持つ delegate では catch 節で必ず `out` に値を代入する（5.2）
- `bool` の戻り値・引数には `[MarshalAs(UnmanagedType.Bool)]` を明示する。
  （P/Invoke の `bool` 既定は 4 バイト Win32 `BOOL` で偶然一致するが、明示して意図を固定する。2 バイト `VARIANT_BOOL` は COM 相互運用の話であり本件には該当しない）
- `AndroidJavaProxy` は使用しない（Windows のため該当なし）

### 7.9 依存関係と実装順序

1. **純粋層**: `WindowsClipboardErrorCode` → 結果型 9 ファイル（`WindowsClipboardResult` と 6.1 の 8 型）→ `WindowsClipboardPayloads` → `WindowsClipboardJsonBuilder` / `WindowsClipboardJsonParser` →
   `WindowsClipboardRequestTable`（ticket / 状態遷移 / `TryClaim` / ドレイン）（EditMode テストを先に書く）
2. **Manager 骨格**: Singleton / `Awake`（`s_mainThreadId` / `s_dispatcher` 捕捉、tombstone 検出）/ `OnDestroy`（`if (_instance != this) return;`）/
   ログ / プラットフォームガード / `InvokeInOrder` / 7.10 の static リセット / `#if UNITY_EDITOR` のテスト seam（9.2）
3. **ライフサイクル**: アパートメント処理と COM 所有権（7.3 の遷移表）→ `Initialize`（購読フラグ）→ shutdown 状態機械 →
   `TryShutdownCore` → `TryShutdown` / `ShutdownWithDrain` / `wantsToQuit` → `CanShutdownNow`
   - V-1 / V-2 は **2026-09-05 のスパイクで検証済み**（合格）。実装時は同じログを Manager 側で再現できることを確認する
4. **同期 API**: 空判定ロジック（7.5）→ 書き込み 6 種 → 読み出し 5 種 → 内容確認 3 種 → `Clear`
5. **非同期 API**: レジストリ結線 → 履歴 5 種 → in-flight ガード → teardown ドレイン → `CancelRequest` → `Awaitable` 版と `CancellationToken`（ガードとドレインの完了後）
6. **イベント**: `ClipboardChanged` → `SetHistoryEventsEnabled` と履歴 3 イベント
7. **遅延レンダリング**: `ReserveDeferredFormats` / `RecoverDeferredState`
8. **仕上げ**: 実機確認（9.3）、`VERSION.txt` の `source:` 行更新（6.3、任意）

### 7.10 static 状態のリセットと破棄後の再生成

前例: `IosClipboardManager.cs:424-450`（`ResetStaticState` / `ResetForTests` が単一の `ResetCore` を共有）。

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState() => ResetCore();

#if UNITY_EDITOR
internal static void ResetForTests() => ResetCore();
#endif

private static void ResetCore()
{
    // wantsToQuit の購読解除、リクエスト表の同期ドレイン（7.6.3）、
    // s_renderProviders / s_renderCache のクリア、in-flight ガードのクリア、
    // _initialized / s_isTerminated / s_quitDrain* / _instance のリセット
}
```

- リセット対象: `s_registry`（同期ドレイン後）/ `s_renderProviders` / `s_renderCache` / in-flight セット / shutdown 状態 / `s_comOwnership` /
  `s_isTerminated`（tombstone）/ `s_quitDrainStarted` / `s_quitDrainCompleted` / `s_mainThreadId` / `s_dispatcher` / `_instance` /
  `Application.wantsToQuit` の購読解除 / `CancellationTokenRegistration` の破棄
- **tombstone（`s_isTerminated`）は static に持つ。** `ManagerDestroyed`(1003) の判定に使い、`ResetForTests` で解除する。
  seam が無いと 1 つの destroy テストが以降すべてのテストを拒否させる（`AssemblyInfo.cs` のコメント）
- tombstone は**ネイティブ呼び出しより前に立てる**（P/Invoke が throw しても teardown が中途半端にならないため。`IosClipboardManager.OnDestroy` の前例）
- `s_dispatcher` は `OnDestroy` でも**意図的に残す**（破棄後の拒否結果を配送する必要があるため）。`ResetForTests` でのみクリアする

**`_instance` 再構築と tombstone の関係（review-v2 の指摘を反映）**:

v2 の「getter 自体はガードできない」という記述は、**スレッド制約**（GameObject 生成はメインスレッド限定で事前にガードできない）と
**lifecycle 状態の確認**（tombstone は生成前に確認できる）を混同していた。正しくは次のとおり。

| 論点 | 事実 | 本設計の方針 |
|---|---|---|
| スレッド | `Instance` getter は GameObject を生成するためメインスレッド限定。getter 内で事前ガードできない | XML コメントで「メインスレッド専用」と明記する（`IosClipboardManager` の前例と同じ） |
| tombstone | getter は生成前に `s_isTerminated` を確認**できる** | **iOS 前例に合わせ、getter では生成を止めない。** 生成後 `Awake` で `s_isTerminated` を検出して `Debug.LogError` を出し、以降の全操作を `ManagerDestroyed`(1003) で拒否する。`public static bool IsTerminated` を公開して呼び出し側が事前確認できるようにする |
| 理由 | getter が例外を投げる / null を返す形にすると既存 Manager 群と API 形状が食い違い、呼び出し側の分岐が増える | 一貫性を優先する。破棄後に生成された Manager は「何もできない殻」として振る舞う |

- **Reload Domain / Scene Reload をともに無効化した構成**では `Awake` が再実行されない。
  この構成では `_instance` が生存したまま `RuntimeInitializeOnLoadMethod` が走るため、**`_instance` を無条件に null へ戻してはならない**（生きた Manager に再接続できなくなる）。
  `ResetCore` は次のように分岐する:
  - `_instance != null`（生存中）: static 状態のみリセットし、`s_mainThreadId` / `s_dispatcher` を**その場で再捕捉**する（`RuntimeInitializeOnLoadMethod` はメインスレッドで走る）。`_instance` は維持する
  - `_instance == null`: 全 static をリセットする
- `ResetForTests`（Editor 限定）は破棄テスト用に `_instance` も含めて完全にリセットする

---

### 7.11 コンパイルガードの構造（P5）

`common.md` の OS 接頭辞方針と review-document の P5 に従い、**二重構造**にする。
最新実装（`MacClipboardManager.cs`）と同じ形であり、Player ビルドに他プラットフォーム向けのネイティブ参照が入らないことを保証する。

| 層 | ガード | 含めるもの |
|---|---|---|
| 外側（クラス） | `#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | クラス宣言、public API、結果型の生成、イベント、リクエストレジストリ、ログ、delegate 型宣言、`[MonoPInvokeCallback]` の実体と `static readonly` delegate インスタンス |
| 内側（ネイティブ境界） | `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` | `[DllImport]` 宣言（`ole32` を含む）、ネイティブ呼び出し |
| Editor 経路 | `#if UNITY_EDITOR` | 上記の代わりに `PlatformUnavailable`(1000) を返すスタブ |

```csharp
// 呼び出し側の形（全 API 共通）
public WindowsClipboardResult CopyPlainText(string text, WindowsClipboardWriteOptions options = ..., Action<...>? onResult = null)
{
    // 事前チェック（メインスレッド / tombstone / 状態 / 引数）はガードの外側で行う
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    copyPlainText(text, (uint)options, out int pError);
    var result = FromNative(OperationCopyPlainText, pError);
#else
    var result = WindowsClipboardResult.Failure(OperationCopyPlainText, WindowsClipboardErrorCode.PlatformUnavailable);
#endif
    Dispatch(result, onResult);
    return result;
}
```

擬似コード中の内部ヘルパの定義: `FromNative` は 8.1 の対応表に従って `pError` を結果型へ変換する、
`Dispatch` は 7.1 の配送ヘルパ（共通イベント → per-call callback）、`Failure` は結果型のファクトリメソッドである。

- 事前チェックと結果配送は**外側**に置き、Editor / Player で同じ順序・同じイベント発火にする
- Editor では `Application.platform` の判定に依存せず**コンパイル時に**ネイティブ経路が消えるため、
  Editor で DLL 解決が走らない（`DllNotFoundException` の発生源をそもそも作らない）
- `PlatformUnavailable` は 8.3 のとおり結果型で返し、イベントと per-call callback を必ず発火させる
- テストファイルにも**外側と同じガード**を付ける（P1b。6.2 / 9.1 / 9.2）

## 8. エラーケース一覧と返却仕様

### 8.1 ネイティブ層（`pError` として返る 19 種）

2.3 の表を正本とする。C# は `WindowsClipboardErrorCode` enum で 0〜19 を受け、`ToMessage()` で 2.3 の英語メッセージへ変換する。

| 層 | コード | 主な発生条件 |
|---|---|---|
| ネイティブ引数検証 | 1 `InvalidParameter` | `null` 引数、サイズ 0、JSON 解析失敗、`copyMultipleFormats` の重複 format / ペイロード種別不一致、`reserveDeferredFormats` の空配列・形式名解決失敗 |
| ネイティブ状態 | 2 `NotInitialized` | `init` 前、`uninit` 後、lifecycle gate クローズ中 |
| Win32 | 3 `Busy` / 4 `Empty` / 5 `FormatUnavailable` / 8 `OutOfMemory` | `OpenClipboard` リトライ上限、空クリップボード、要求形式なし、`GlobalAlloc` / `GlobalLock` 失敗・`std::bad_alloc` |
| ネイティブ境界検証 | 6 `InvalidData` / 7 `BufferTooSmall` / 13 `PartialState` | 終端 NUL / サイズ / オフセット / DIB 構造の検証失敗、出力バッファ不足、配置失敗後のロールバック失敗 |
| WinRT（履歴） | 9 `AccessDenied` / 10 `HistoryDisabled` / 11 `ItemDeleted` / 17 `NotForeground` | 履歴アクセス拒否、履歴設定オフ、項目削除済み、自プロセスが前面でない（**受付後にコールバックで返る**） |
| スレッド / 監視 | 12 `MonitorRegisterFailed` / 14 `WrongThread` / 18 `WrongApartment` | リスナ登録・解除失敗（sticky 状態あり。2.9）、UI スレッド限定 API の誤用、`init` 呼び出しスレッドが STA でない |
| ライフサイクル | 15 `Canceled` | `cancelClipboardRequest` または `uninit` ドレインによる失効 |
| その他 | 16 `NotSupported` / 19 `Unknown` | Windows に等価概念がない操作、`DeleteItemFromHistory` / `ClearHistory` の `false` を含む上記以外 |

### 8.2 API 別に返り得るエラーコード

| API | 返り得るネイティブコード | 備考 |
|---|---|---|
| `Initialize` | 0 / 12 / 14 / 18 / 19 / 8 | 12 は `enableChangeEvents = true` のときのみ（7.3） |
| `SetHistoryEventsEnabled` | 0 / 2 / 12 / 14 | 12 は sticky 状態になり得る（2.9） |
| `TryShutdown` | 0 / 3 / 12 / 13 / 14 / 15 | **`completed == false` のときの値は「未完了」または「終端失敗」**。分類は 7.4 の表を正本とする |
| `CanShutdownNow` | 0 / 2 | 状態問い合わせのみ。クリップボードを開かないため 3 は返らない |
| copy 系 6 種 | 0 / 1 / 2 / 3 / 6 / 8 / 13 / 19 | 13 は `CopyMultipleFormats` のみ。`RecoverDeferredState` では回復できない |
| paste 系 5 種 | 0 / 1 / 2 / 3 / 4 / 5 / 6 / 7 / 8 / 19 | 7 は 2 回呼び出しの 1 回目で必ず出る（正常系）。**失敗時も戻り値は 0 になる**（7.5） |
| `HasFormat` | 0 / 1 / 2 | `IsClipboardFormatAvailable` のみで**クリップボードを開かないため 3 は返らない**。失敗時も戻り値は `FALSE`（2.9） |
| `GetFormats` | 0 / 2 / 3 / 6 / 7 / 8 | 空でも `"[]"`。`EMPTY` は返らない |
| `GetPreferredFormat` | 0 / 2 / 6 / 7 | 該当なしは空文字。`EMPTY` は返らない。`PickPreferredFormat` はクリップボードを開かない。**候補は `{CF_UNICODETEXT, CF_HDROP, CF_DIB, CF_BITMAP}` 固定で `HTML Format` を返さない**（2.9） |
| `Clear` | 0 / 2 / 3 / 19 | |
| `ReserveDeferredFormats` | 0 / 1 / 2 / 3 / 8 / 13 / 14 / **19** | 19 は `EmptyClipboard` 失敗時と、配置失敗後の rollback 成功時（L1 の反映）。14 は所有 UI スレッド外。既存内容を破棄する（D-1）。失敗地点による native 状態は D-9 |
| `RecoverDeferredState` | 0 / 2 / **3** / 13 / 14 / 19 | 対象は deferred の partial 状態のみ。クリップボードを開くため `Busy` を返し得る。**`NONE` は「回復した」を意味しない**（非 partial でも `NONE` を返す。7.7） |
| 履歴 5 種（受付前） | 1 / 2 / 8 / 19 | requestId 0 + `pError`。ネイティブのコールバックは呼ばれない（C# 側は 7.6.1 の ticket で結果を配送する） |
| 履歴 5 種（受付後） | 0 / 9 / 10 / 11 / 15 / 17 / 19 | **`GetHistoryAvailability` も 17 を返す**（前面でないと設定を取得できない） |
| `CancelRequest` | 0 / 2 / 19 | `false` でも完了は届き得る |

表は各 API の通常分岐と `SafeBridgeCall` の例外マッピング（`bad_alloc` → 8、その他 → 19）から導出している。

**`Busy`(3) の扱い**: 他プロセスがクリップボードを排他保持している一時的な失敗。
**クリップボードを開く API でのみ発生する**（copy 系 / paste 系 / `GetFormats` / `Clear` / `ReserveDeferredFormats`）。
`HasFormat` / `GetPreferredFormat` / `CanShutdownNow` / 履歴 API では発生しない。
C# 層では自動リトライしない（ネイティブ側が既に `OpenClipboard` をリトライしている）。呼び出し側にそのまま返し、
サンプル / アプリ側で「他のアプリが使用中です。もう一度お試しください」と再試行を促す方針を XML コメントに明記する。

### 8.3 C# Bridge 層（本実装が生成する。1000 番台）

| 値 | 定数 | 条件 | メッセージ |
|---|---|---|---|
| 1000 | `PlatformUnavailable` | `Application.platform != RuntimePlatform.WindowsPlayer`（Editor 実行を含む） | `{operation} is available only on Windows player builds.` |
| 1001 | `BridgeUnavailable` | `DllNotFoundException` / `EntryPointNotFoundException` | `{operation} could not be started; the native clipboard bridge is unavailable.` |
| 1002 | `MainThreadRequired` | Unity メインスレッド以外からの呼び出し | `{operation} must be called from the Unity main thread.` |
| 1003 | `ManagerDestroyed` | `OnDestroy` 後（tombstone）の呼び出し | `{operation} was rejected because the manager has been destroyed.` |
| 1004 | `NotInitializedByHost` | `Initialize` 未実行 / 失敗のままの呼び出し | `{operation} requires Initialize to succeed first.` |
| 1005 | `InvalidArgument` | C# 側の引数検証失敗（null / 空 / 形式名が空白 / `WindowsClipboardFormatPayload` の排他違反） | `{operation} received an invalid argument: {detail}` |
| 1006 | `ShutdownTimeout` | ドレインのリトライ上限超過 | `Shutdown did not complete within the retry budget.` |
| 1007 | `ResultParseFailed` | ネイティブが返した JSON のパース失敗 | `{operation} returned a payload that could not be parsed.` |
| 1008 | `RequestRejected` | `requestId == 0` かつ `pError == 0`（理論上到達しない防御的コード。2.5） | `{operation} was rejected without a native error code.` |
| 1009 | `ApartmentUnavailable` | `CoInitializeEx` が `RPC_E_CHANGED_MODE` 等で失敗（ネイティブ未到達） | `{operation} requires an STA thread; the Unity main thread could not be initialized as STA.` |
| 1010 | `OperationBusy` | in-flight ガード中の再呼び出し（7.6.4） | `{operation} is already in progress.` |
| 1011 | `ShuttingDown` | shutdown ドレイン中（`Draining`）の新規操作（7.4） | `{operation} was rejected because the manager is shutting down.` |

### 8.4 不変条件

- `IsSuccess == true` のとき `ErrorCode == None` かつ `ErrorMessage == null`
- `IsSuccess == false` のとき `ErrorMessage != null`
- 読み出し結果は `IsSuccess == true` でも `IsEmpty == true` があり得る（空クリップボードは正常系）。
  **戻り値 0 は空を意味しない。**`IsEmpty` は 7.5 の条件でのみ立てる
- `HasFormat` の `IsSuccess` と `HasFormat` は独立（`IsSuccess == false` のとき `HasFormat` は無意味）
- 非同期 API は、**受付済み・受付前拒否のいずれでも**、成功・失敗・キャンセル・shutdown ドレイン・teardown ドレインのいずれかで
  **ちょうど 1 回**結果が配送される（`TryClaim` による exactly-once。7.6.1）
- 拒否（受付前失敗）でも共通イベントと per-call callback は必ず発火する
- 同期 API は戻り値で即時に結果を返す。イベント / callback は**呼び出し元のスタック外**で配送される（同一フレーム内の場合がある。7.1）
- **同期発火は「この先 `Update` が回る保証が無い」場合に限る。** 該当するのは 2 つ:
  teardown ドレイン（7.6.3）と、**quit を再開する直前の drain の settle**。
  後者では settle 中に始まった操作の配送も同期になる（配送を積んでも走らせる `Update` が来ないため）

---

## 9. テスト方針

### 9.1 層 1: EditMode（`Tests/Runtime`）

| 対象 | 検証内容 |
|---|---|
| `WindowsClipboardErrorCode` / 結果型 | 0〜19 と 1000 番台のメッセージ対応、成功時 `ErrorMessage == null`、`IsEmpty` の判定、`HasFormat` と `IsSuccess` の独立性 |
| `WindowsClipboardPayloads` | `WindowsClipboardFormatPayload` の排他規則（text / html / base64 のうち 1 つのみ）、`WindowsClipboardWriteOptions` のフラグ合成（`Sensitive == ExcludeHistory | ExcludeRoaming`） |
| `WindowsClipboardJsonBuilder` | パス配列（バックスラッシュ / 非 ASCII / 引用符のエスケープ）、複数形式（順序保持、排他キー違反の検出）、予約形式 |
| `WindowsClipboardJsonParser` | 履歴配列（`text: null`、`contentTypes` 省略、`timestamp` が `long.MaxValue` 相当）、可用性オブジェクト、壊れた JSON / 空文字 / null |
| `WindowsClipboardRequestTable` | ticket 発行、`AwaitingNative` → `Undelivered` 遷移、`TryClaim` の exactly-once、未知 ID の完了が無害、ドレインで `AwaitingNative` と `Undelivered` の両方が 1 回ずつ完了、ドレイン後に走る遅延 action が no-op になること、ドレインの冪等性、受付前拒否 ticket の追跡 |
| 空判定ロジック（H1） | 各 API × `pError`（`NONE` / `EMPTY` / `FORMAT_UNAVAILABLE` / `BUFFER_TOO_SMALL` / `NOT_INITIALIZED` / `BUSY` / `INVALID_DATA`）× 戻り値（0 / 正）の組み合わせで、空の成功と失敗が正しく分かれること。特に **0 + `Busy` / 0 + `NotInitialized` / 0 + `InvalidData` が失敗として保持される**こと、バイト系 API の 0 + `BufferTooSmall` が空の成功になること |
| 予約世代（H3 / A1） | 予約 A 成功 → 予約 B が `BUSY`(3) / `INVALID_PARAMETER`(1) / `UNKNOWN`(19) で失敗 → **旧世代（A）が変更されない**こと。`PARTIAL_STATE`(13) では新世代へ差し替わり、旧世代の非衝突キーだけが残ること（7.7 の 3 行規則） |
| shutdown 分類（7.4） | `WrongThread` が終端失敗、`Busy` / `MonitorRegisterFailed` / `Canceled` が未完了として扱われること |
| `InvokeInOrder` | 共通イベント → 個別 callback の順序、片方 null、購読者の例外が他方の発火を妨げないこと |
| バッファ helper | **エラーを先に分類**したうえでの空判定（7.5 の表）。`0 + Busy` / `0 + NotInitialized` / `0 + InvalidData` を成功にしないこと、バイト系の `0 + BufferTooSmall` のみ空成功、`BUFFER_TOO_SMALL` の再試行上限 |
| 遅延レンダリング判定 | 1 回目でキャッシュされること、2 回目がキャッシュを再利用し `requiredSize` が一致すること、長さ 0 が `InvalidData` になること、キャッシュ無しの 2 回目が `Unknown` になること |

全テストファイルに `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` を付ける（P1b）。
Manager インスタンスを生成するテストは書かない（testing.md 層 1 の規約）。上記はすべて `internal static` / `internal sealed` の純粋ロジックとして切り出して検証する。

### 9.2 層 2a: PlayMode（Editor 内、`Tests/PlayMode`）

**テスト用 internal seam（必須。不足項目の反映）**

Editor 実行ではネイティブ要求がすべて `PlatformUnavailable` で拒否されるため、Manager を生成するだけでは受付後の状態を検証できない。
次の `#if UNITY_EDITOR internal static` seam を Manager に用意し、`InternalsVisibleTo` 済みのテストアセンブリから注入する。

| seam | 用途 |
|---|---|
| `internal static uint InjectAcceptedRequestForTests(string operation, Action<...> onResult)` | ネイティブを呼ばずに受付済みエントリ（`AwaitingNative`）を作る |
| `internal static void InjectCompletionForTests(uint requestId, int error, string? json)` | ネイティブ完了コールバックと同じ経路を駆動する |
| `internal static void InjectReserveResultForTests(int pError)` | 予約結果ごとの世代規則（7.7 の 3 行）を検証する。規則は `pError` だけで決まるため、この seam で全分岐を再現できる |
| `internal static void InvokeRenderDuringReserveForTests(string formatName)` | ネイティブ呼び出し中に render callback を同期実行し、staging が旧・新どちらの形式も解決することを検証する |
| `internal static void InjectShutdownResultForTests(bool completed, int pError)` | 未完了 / 終端失敗の分類とドレインのリトライを検証する |
| `internal static bool HasPendingForTests(uint ticket)` / `internal static int RegistryCountForTests` | レジストリ状態の検証 |
| `internal static Action? QuitActionForTests` | `Application.Quit()` を差し替える。未設定なら本物を呼ぶ。**quit の再開回数**と `s_quitDrainCompleted` を観測できるようにする（Editor では実際に終了させないため必須） |
| `internal static int ComReleaseCountForTests` | fallback で獲得した COM 参照の解放回数（`CoUninitialize` 呼び出し回数）。冪等 `Initialize` 後も 1 回だけ解放されることを検証する |

seam はすべて `#if UNITY_EDITOR` で囲み、Player ビルドに含めない。

**検証項目**

- 非 Windows Player 環境での拒否経路（`PlatformUnavailable`）が **event と per-call callback の両方**に届くこと
- **8 種の共通 event がそれぞれ発火すること**（`ClipboardOperationCompleted` / `TextReadCompleted` / `StringListReadCompleted` / `BytesReadCompleted` / `FormatPresenceChecked` / `FlagChecked` / `HistoryReadCompleted` / `HistoryAvailabilityChecked`）
- **発火しない契約**: `TryShutdown` が event を発火しないこと（5.3 の例外）、`Initialize` の失敗時に読み出し系 event が発火しないこと
- 配送契約: 共通 → 個別の順序が保たれること、**呼び出し元のスタック外で配送されること**（同一フレーム配送を許容する形でアサートする。「次フレーム」を前提にしない）
- `GetHistoryAsync` 等が拒否時にも必ず完了し、ハングしないこと
- **完了済み・未配送の teardown（H2）**: `InjectCompletionForTests` で完了させた直後、`Update` を回さずに `OnDestroy` / `ResetForTests` を実行し、
  per-call callback と `Awaitable` が同期的に 1 回だけ完了すること。その後 `Update` を回しても**二重配送されない**こと
- **受付前拒否の `Awaitable`** も teardown 対象になること
- **キャンセルの競合（M1）**: ワーカースレッドから `CancellationTokenSource.Cancel()` を呼んだ場合に `MainThreadRequired` にならず、
  完了と競合しても結果が 1 回だけ配送されること。`CancellationTokenRegistration` が全経路で破棄されること
- **shutdown core の経路**: `Draining` 中でも `ShutdownWithDrain` のリトライがネイティブへ到達すること、
  tombstone 設定後の `OnDestroy` でも core が実行されること、public `TryShutdown` を直接呼んだときの状態遷移（7.4 の表）
- **`Running` から public `TryShutdown` が即時完了**したとき、受付済みの per-call callback と `Awaitable` が**ちょうど 1 回**完了すること（7.4 段階 1 のドレイン）
- **`ShutdownFailed` では `Initialize` が `ShuttingDown`(1011) で拒否**され、shutdown を再試行して `completed == true` を得た後にのみ再初期化できること
- **quit ドレインが終端失敗（`WrongThread` 注入）で終わっても quit が再開**し、`QuitActionForTests` の呼び出しが 1 回だけであること。
  成功・`ShutdownTimeout`・終端失敗の 3 結果すべてで `s_quitDrainCompleted` が立ち、quit が再開すること
- **COM ownership の遷移**: `initClipboardManager` 失敗時と P/Invoke 例外時に、本層が獲得した参照がその場で解放されること（7.3 の表の全行）
- **冪等 `Initialize` と COM 所有権（A1）**: fallback で `s_comOwnership` を獲得した状態で `Running` 中に `Initialize` を再度呼んでも
  ownership が `None` に戻らず、shutdown 完了時の解放が**ちょうど 1 回**であること（`ComReleaseCountForTests == 1`）。
  併せて 2 回目の `Initialize` が COM / ネイティブに触れずに冪等成功を返すこと
- **`Initialize` の冪等呼び出し**: 2 回成功させても `wantsToQuit` の購読数が 1 のままであること、`OnDestroy` 後に購読が残らないこと
- **予約世代**: 予約 A 成功 → 予約 B が `BUSY` / `UNKNOWN` で失敗 → A の provider が解決できること。`PARTIAL_STATE` では新世代が優先されること
- **`RecoverDeferredState`**: `NONE` を返しても provider / cache が解放されないこと
- shutdown 状態機械: `Draining` 中の新規操作が `ShuttingDown`(1011) で拒否されること、同期ドレインの callback 内から呼んだ操作も拒否されること
- 重複 Manager の破棄が稼働中の singleton を shutdown しないこと（2 つ目の GameObject に AddComponent → Destroy）
- `ShutdownWithDrain` がリトライ後に完了結果を返すこと、`WrongThread` 注入で即座に打ち切ること
- `ResetForTests` によりテスト間で tombstone / in-flight ガード / レジストリが分離されること（テスト実行順序に依存しないこと）
- 破棄後に `Instance` を取得すると新しい GameObject が生成され、`IsTerminated == true` のまま全操作が `ManagerDestroyed` で拒否されること

### 9.3 層 2b / 層 3: 実機（Windows 11 Player）

本計画では**テストコードは作成せず、手動確認項目として定義する**（自動化は testing.md の未定義事項に従い別途）。

**実施手順の注意（ネイティブ側検証 O1）**: 確認中のスクリーンショット撮影は**クリップボードを画像で上書きする**。
コピーと貼り付けの間に撮影すると内容が失われ、履歴の件数も変わり、`ClipboardChanged` も余分に発火する。
撮影はステップの区切りで行い、撮影後は対象データを置き直す。

| # | 確認項目 | 期待 |
|---|---|---|
| M-1 | `Initialize` 成功 | `WrongApartment` / `ApartmentUnavailable` が返らない（V-1 の実証） |
| M-2 | テキスト copy → メモ帳に貼り付け | 同一内容 |
| M-3 | メモ帳でコピー → `PastePlainText` | 同一内容 |
| M-4 | HTML copy → Word / ブラウザに貼り付け | 書式が保たれる |
| M-5 | ファイル copy → エクスプローラに貼り付け | ファイルがコピーされる |
| M-6 | 画像（DIB）copy → ペイントに貼り付け | 画像が一致 |
| M-7 | `CopyMultipleFormats` | 貼り付け先アプリごとに richest な形式が選ばれる |
| M-8 | `CopyMultipleFormats` に非 ASCII の `CF_TEXT` を含める | CP_ACP 変換で欠落し得ることを確認し、ドキュメント記述と一致すること |
| M-9 | `Sensitive` オプションで copy | Win+V の履歴に載らない |
| M-10 | 他アプリでコピー | `ClipboardChanged` が発火する（**1 回のコピーで複数回発火し得る**。2.2）。自プロセスの copy では発火しない（`options` の値によらず） |
| M-11 | 履歴取得 | Win+V の内容と一致。`timestamp` が妥当 |
| M-12 | 履歴無効時に履歴 API | `HistoryDisabled` がコールバックで返る |
| M-13 | 非フォアグラウンド時に履歴 API と `GetHistoryAvailability` | いずれも `NotForeground` がコールバックで返る |
| M-14 | `RestoreHistoryItem` / `DeleteHistoryItem` / `ClearUnpinnedHistory` | Win+V 上の状態が変わる。ピン留め項目は残る |
| M-15 | 更新系履歴操作の連続呼び出し | 2 回目が `OperationBusy` で即時に失敗し、1 回目の結果は必ず届く |
| M-16 | 進行中リクエストの `CancelRequest` / `CancellationToken` | `Canceled` が 1 回だけ返る |
| M-17 | `ReserveDeferredFormats` 実行 | 実行前のクリップボード内容が消える（D-1 の確認） |
| M-18 | `ReserveDeferredFormats` 後に他アプリで貼り付け | provider が二相で呼ばれ内容が渡る |
| M-19 | 予約状態のままアプリ終了 | `wantsToQuit` ドレインにより予約形式が実体化される |
| M-20 | ドレインがタイムアウトする状況 | `ShutdownTimeout` をログに残しつつアプリは終了する |
| M-21 | 大きなテキスト / 画像（バッファ再試行経路） | 2 回呼び出しで正しく取得できる |
| M-22 | IL2CPP ビルドでの全経路 | `MonoPInvokeCallback` が機能し、コールバックが届く |
| M-23 | `Initialize` 後にタスクバーと Alt+Tab を確認 | ネイティブの隠し dispatch ウィンドウが**タスクバー / Alt+Tab に出ない**（ネイティブ側でも未検証。9.2） |
| M-24 | `RestoreHistoryItem` 実行 | 復元が成功し、**`ClipboardChanged` も発火する**（2.2。仕様どおり） |

### 9.4 テスト実行手順（common.md「テストの確認タイミング」）

1. コード追加・修正のたびに既存テストへの影響を確認する（壊れていないか、新規フィールドにテストが不足していないか）
2. Unity Test Runner の **EditMode を全実行**して all passed を確認する
3. Unity Test Runner の **PlayMode を全実行**して all passed を確認する（active build target は Windows）
4. 実機確認（9.3）は Player ビルドで実施する。IL2CPP 経路（M-22）は Scripting Backend を IL2CPP に切り替えて実施し、結果を実装結果ファイルに記録する

---

## 10. 未確定・要検証事項

| # | 事項 | 影響 | 対応 |
|---|---|---|---|
| V-1 | ~~Unity メインスレッドの STA~~ | — | **解決済み・合格**（2026-09-05 実機）。`CoGetApartmentType` = `APTTYPE_MAINSTA`(3)、`initClipboardManager` は `pError = 0`。専用 STA スレッド案は不要。`CoInitializeEx` フォールバックは防御的経路として残す（7.3） |
| V-2 | ~~メッセージポンプの配送~~ | — | **解決済み・合格**（2026-09-05 実機）。非同期リクエストが受付 → WinRT continuation → **Unity メインスレッド**で完了（`{"historyEnabled":true,"roamingEnabled":false}`）。`ClipboardChanged` も同様にメインスレッドへ到達 |
| V-3 | `Application.wantsToQuit` 経由のドレインと、**予約ありの終了で `WM_RENDERALLFORMATS` が届くか** | 予約形式の消失、または終了できない不具合 | **部分検証済み**: 予約なしでは `uninitClipboardManager` が 1 回目で TRUE。予約あり（D-7 の同期再入を含む）と `wantsToQuit` 経路は M-19 / M-20 で確認する |
| V-4 | ~~`getPreferredClipboardFormat` の空時の戻り値~~ | — | **解決済み**（2026-09-05）。`PickPreferredFormat()` は 0 / -1 を返し、必要サイズ 1 + `BUFFER_TOO_SMALL` → 2 回目は空文字 + `NONE`。判定は「取得文字列が空」で行う（2.9 / 7.5） |
| V-5 | Editor 実行時の扱い | DLL の meta が Editor 無効のため Editor では動作しない。`PlatformUnavailable` を返す | **判断確定**（Editor 対応はスコープ外）。サンプルシーンへの申し送りは 12 章 |
| V-6 | ~~debug 版 DLL の有無~~ | — | **再解決（v3 の結論を撤回）**。dist に `-debug.dll` は無いが、`PreBuildProcessor` がリリース DLL を `-debug` 名で配置する。**`DEVELOPMENT_BUILD` 分岐は必須**（2.7 / 5.1）。既存 `WindowsNotificationManager` の分岐は正しい実装であり是正対象ではない |
| V-7 | IL2CPP での `MonoPInvokeCallback` の実挙動 | コールバックが届かない可能性 | スパイクは Mono で実施した。M-22 で IL2CPP ビルドを確認する |
| V-8 | 隠し dispatch ウィンドウが Unity Player でタスクバー / Alt+Tab に出ないか | 利用者から見える不要なウィンドウが増える | ネイティブ側でも未検証（同 9.2）。M-23 で確認する |

**ネイティブ側で未検証のまま引き継がれている項目**（`native-toolkit` の実装結果 8 章より）。本設計はいずれにも依存しない作りにしてあるが、実機確認時に併せて観察する。

| ネイティブ側 No | 項目 | 本設計での扱い |
|---|---|---|
| 9.2 | 隠し HWND が Alt+Tab / タスクバーに出ないか | V-8 / M-23 |
| 9.3 | deferred provider が UI queue で hang しないか | D-6 の provider 制約で回避（7.7） |
| 9.4 | `NOT_FOREGROUND` の実挙動 | M-13 で確認 |
| 9.5 | history ID が restore / delete で受理されるか | M-14 / M-24 で確認 |
| 9.7 | worker から Win32 clipboard API を呼べるか | 本設計はメインスレッド限定のため対象外（7.2） |
| 9.8 | `Request + Immediate Uninitialize` が `FALSE + CANCELED` になるか | teardown ドレインが非依存（7.6.3） |
| 9.9 | `MONITOR_REGISTER_FAILED` 後の復旧 | sticky 状態として 2.9 / 8.1 に記載。再試行のみ |

---

## 11. Definition of Done

### 機能

- [ ] 27 のネイティブ API すべてに対応する C# 公開 API が存在し、5.3 の対応表と一致する
- [ ] 同期 API は戻り値で結果を返し、`pError` を持つ API はすべて結果型を返す（`bool` を直接返す API が無い）
- [ ] 非同期 API は requestId を返し、**受付済み・受付前拒否のいずれも**必ず 1 回だけ配送される（`Undelivered` の teardown を含む）
- [ ] `Awaitable` 版は in-flight ガード（7.6.4）と teardown ドレイン（7.6.3）の実装後にのみ公開されている
- [ ] `CancellationToken` がワーカースレッドからのキャンセルでも正しく動作し、登録が全経路で破棄される（7.6.5）
- [ ] `Initialize` / `TryShutdown` / `ShutdownWithDrain` / `wantsToQuit` が 7.3 / 7.4 の規則どおり動作し、shutdown 状態機械の遷移が実装されている
- [ ] shutdown 経路が `TryShutdownCore` を通り、tombstone / `Draining` に阻まれずネイティブへ到達する（7.4）
- [ ] `Running` 中の `Initialize` が COM / ネイティブに触れずに冪等成功を返し、`s_comOwnership` を変更しない（7.3）
- [ ] 予約世代の保持が `pError` だけで決まる規則になっている（`UNKNOWN` の失敗地点を判別しようとしていない）（7.7）
- [ ] `RecoverDeferredState` の成功で provider / cache を解放していない（7.7）
- [ ] COM 所有権の記録と解放が 7.3 の表どおり実装されている
- [ ] 読み出し系の空判定が 7.5 の分類順序に従い、**戻り値 0 を空に正規化していない**
- [ ] 遅延レンダリングが 2.8 の D-1 〜 D-9 をすべて満たし、予約失敗時に旧 provider 世代が保持される

### 品質

- [ ] 全 public 型・メソッドに英語の XML ドキュメントコメントがある
- [ ] ログが 3.2 の書き分け（Manager は値を伏せる / Parser は行ごと省く）に従い、逸脱がファイル冒頭に明記されている
- [ ] クリップボード内容・履歴テキストがログに出ていない
- [ ] `#nullable enable` / `using` 配置が既存ファイルと同形
- [ ] **ガードが二重構造になっている**（クラス = `\|\| UNITY_EDITOR` / ネイティブ境界 = `&& !UNITY_EDITOR`）。7.11
- [ ] **テストファイルにも同じクラスガードが付いている**（P1b）
- [ ] 6.5 の P1〜P5 自己点検がすべて適合のまま維持されている
- [ ] `[MonoPInvokeCallback]` は static のみ、delegate は `static readonly` で保持されている
- [ ] `.meta` ファイルを手動作成していない

### テスト

- [ ] 9.1 の EditMode テストがすべて存在し passed
- [ ] 9.2 の PlayMode テストがすべて存在し passed、実行順序に依存しない
- [ ] 9.2 の internal seam が `#if UNITY_EDITOR` で囲まれ、Player ビルドに含まれない
- [ ] H1（空判定）/ H2（完了済み・未配送の teardown）/ H3（予約世代の保持）/ M1（キャンセル競合）の回帰テストが存在する
- [ ] 既存テスト（Android / iOS / Notification）が壊れていない
- [ ] 9.3 の手動確認 M-1 〜 M-24 を実機で確認し、結果を実装結果ファイルに記録した

### 構成

- [ ] `scripts/check_design_consistency.py` が exit 0（FAIL 0）である。
      `declared counts` / `repeated counts` の 2 チェックは、本設計が macOS 設計特有の番号付き行形式を採らないため SKIP になる（既知）
- [ ] `agent-rules/coding-rules/testing.md` 7 節の適用状況表を更新した（Windows Clipboard を「対象外」から実装済みへ）
- [ ] `VERSION.txt` の `source:` 行を更新した（任意項目。未実施でも可）
- [ ] `Plugins/Windows` の DLL をコミットに含めていない（ビルド構成で入れ替わるため）
- [ ] 6.4 の非変更ファイルに差分が無い
- [ ] サンプルアプリ（UI / シーン / UXML / USS）に差分が無い

---

## 12. スコープ外と申し送り

スコープ外:

- サンプルアプリ（`Runtime/UI/Windows/Clipboard/**`、UXML / USS、サンプルシーン、`NativeToolkitSampleNavigator` への導線）→ `design-sample-scene`
- マニュアル / `docs` の更新 → `write-manual`
- `package.json` のバージョン更新・リリース作業 → `release`
- ネイティブ（`native-toolkit`）側の実装変更
- 層 2b / 層 3 のテスト自動化ハーネス構築
- 既存 `WindowsNotificationManager` の是正（非 Windows での無言 return）。`-debug` DLL 名の分岐は正しい実装のため対象外（2.7 / V-6）

`design-sample-scene` への申し送り:

- **全 API が Unity Editor では `PlatformUnavailable` になる**（DLL の meta が Editor 無効。V-5）。サンプルシーンの動作確認は毎回 Windows Player ビルドが必要になる
- `Initialize` を明示的に呼ぶ導線が必要（Manager は自動初期化しない。7.3）
- `ReserveDeferredFormats` は既存のクリップボード内容を破棄するため、サンプル UI で事前に警告を出す必要がある（D-1）
- 更新系履歴操作は in-flight ガードがあるため、実行中はボタンを無効化する導線が望ましい（7.6.4）
- 終了時のドレイン（7.4）があるため、アプリ終了に最大 2 秒かかり得る
- shutdown 開始後は全操作が `ShuttingDown`(1011) で拒否されるため、サンプル UI もその時点で操作不能にする
- 結果の配送は「呼び出し元のスタック外」であり**次フレームとは限らない**（7.1）。UI 更新をフレーム前提で組まない
- **`ClipboardChanged` は 1 回の外部コピーで複数回発火する**（実機で 3 回。2.2）。変更回数をそのまま表示せず、
  「最後に検知した時刻」や debounce 済みの状態表示にする
