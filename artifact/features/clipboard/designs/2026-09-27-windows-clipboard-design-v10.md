# Windows Clipboard 実装計画 v10（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Clipboard（Windows） |
| 種別 | **移行。機能追加ではない。** 1.x の C ABI（dist 1.11.0）から 2.0.0（dist 1.12.0）へ P/Invoke 層を書き換える |
| 前の版 | `2026-09-05-windows-clipboard-design-v8.md`（1.x での実装の設計。状態機械、配送の台帳、drain、遅延描画の世代などは v8 の決定を引き継ぐ）、`2026-09-27-windows-clipboard-design-v9.md`（本書の前の版） |
| 親課題 | `artifact/topics/windows-c-abi-2/README.md`（Clipboard は「リネームのみ」と「Clipboard・振る舞いが変わる」） |
| 移行前の基準 | `50fe7bb`（`feature/UNT-12`）。Clipboard の Player テスト、S-2 の全ボタン（67/67）と押した回ごとの期待値、M-19 が 1.x で通る |
| 前提 | Dialog（`artifact/features/dialog/designs/2026-09-27-windows-dialog-design-v5.md`）と Notification（`artifact/features/notification/designs/2026-09-27-windows-notification-design-v7.md`）の移行が済んでいること。Clipboard が最後で、1.x の DLL の追加のピンを消す |
| 方針 | **既存の公開 API（`WindowsClipboardManager` のメソッド 33 本・イベント 12 個・`Operation*` 定数 27 個、結果型、`WindowsClipboardErrorCode` の値、`WindowsClipboardJsonBuilder`、ペイロードの型、履歴の `Timestamp` の単位、呼べるスレッド、イベントとコールバックの届き方）を変えない**（親課題 Q-4）。サンプル・UXML は触らない。今の Player テストと S-2 の期待値（`scripts/windows_clipboard_sample_expected.py`）がそのまま移行の検証になる。S-2 の期待値で変えるのは J-12 の 1 行だけ。変わる振る舞いは 5.4「既知の差分」に列挙したものだけ |
| DLL | `windows-native-toolkit-capi-2.0.0.dll`（Dialog・Notification と同じ） |
| 開発者の判断（2026-09-27） | J-1（履歴の `Timestamp` は FILETIME のまま）、J-2（`Operation*` の値は変えない）、J-3（不正な base64 は 1.x と同じ規則で `InvalidParameter`）、J-12（S-2 の `ForceInitializeWhileDraining` の期待値は両方の結果を許す） |
| レビュー | `artifact/features/clipboard/reviews/2026-09-27-windows-clipboard-design-review-v9.md` |

## v9 からの主な変更

レビュー v9 の指摘を反映した。

- A1:
  - base64 の規則に「3 文字目が `=` なら 4 文字目も `=`」を足した（4.2）
  - `CopyMultipleFormats` のエラーの優先順位の変化を 5.4 に書いた
  - 履歴を写す途中の例外でも、結果を必ず 1 回届ける（4.6）
  - セッションが無いときの終了は、ネイティブを呼ばずに成功（4.7）
  - 捕まえる例外は API ごとの今の分け方を保ち、`BadImageFormatException` だけ足す（4.3、J-13）
  - id が空の履歴の項目は捨てる（4.6）
- A2:
  - 5.4 の誤っていた 2 行を直した（破棄のあとの `Initialize`、`TryShutdown` の 1 回目）
  - S-2 の `ForceInitializeWhileDraining` は、両方の結果を許す（J-12、開発者の判断）
  - 拒否の詳細の文言を運ぶ欄を残す（4.6）
  - `CanShutdownNow` はセッションが無ければネイティブを呼ばずに真（4.2）
  - `render_target_set` の戻り値をそのまま返す（4.5）
- B:
  - 成功で中身が空のときの扱い（4.3）
  - close の中で完了が届くこと（4.6）
  - 7.3 の空のバイト列のテストを `CopyMultipleFormats` に替えた
  - 層 1 / 2a のテストの扱い（5.1、7.2）
  - `TryShutdown` のテストは要求を待たせる形に
  - 「セッションが無いとき」の言い方
  - `--include-destructive` が前提
  - `--il2cpp`（5.1、J-10）
  - 8.1 に 5 行
- C: 5.1 にガードの列、`WriteOptions` の値、Timestamp の溢れ、`CANCELED` の条件、recover、古い XML コメント

## 1. native-toolkit 側（2.0.0 の C API）

出典: native-toolkit `dist/1.12.0/windows/include/NativeToolkitC/Clipboard.h`（以下 `CH`）、`Common.h`。
実装: `windows/WindowsLibraryCApi/src/Clipboard/`（`ClipboardCApi.cpp`、`ClipboardItemsCApi.cpp`、`ClipboardDeferredCApi.cpp`、`ClipboardHistoryCApi.cpp`、`ClipboardConvert.cpp`）、`windows/WindowsLibrary/src/Clipboard/`。
**Clipboard の中身（Core / Formats / HistoryWinRt / HistoryCoordinator）は 1.11.0 と同じ**（`diff -w` で改行コードの差だけ）。振る舞いの変化は、C ABI・C++ API・Manager の Close と Drain の境界から来る。

### 1.1 関数（Clipboard 40 本、`WindowsLibraryCApi.def:83-129`）

OP の番号は本書だけのもの（親課題 README の番号とは別）。

| # | 関数 | スレッド | 1.x |
|---|---|---|---|
| OP-01 | `ntk_clipboard_session_create(const ntk_clipboard_session_options*, ntk_clipboard_session** out)` | STA / MAINSTA のスレッド。そのスレッドがオーナーになり、`GetMessageW` のループを回し続ける必要がある（`CH:6-11`） | `initClipboardManager` |
| OP-02 | `ntk_clipboard_session_close(session)` | オーナーだけ | `uninitClipboardManager` |
| OP-03 | `int32_t ntk_clipboard_session_can_close(const session*)` | 任意 | `canDestroyClipboardManager` |
| OP-04 | `void ntk_clipboard_session_free(session)` | 任意。ほかの呼び出しと同時に呼ばない | （無し） |
| OP-05 | `ntk_clipboard_set_history_handlers(session, const ntk_clipboard_history_handlers*)` | オーナーだけ | `setClipboardHistoryCallbacks` |
| OP-06〜09 | `ntk_clipboard_copy_text(s, text, flags)` / `_copy_html(s, fragment, plain_text, flags)` / `_copy_files(s, const char* const* paths, size_t count, flags)` / `_copy_dib(s, dib, size, flags)` | 任意 | `copyPlainText` / `copyHtml` / `copyFiles` / `copyImage` |
| OP-10 | `ntk_clipboard_copy_custom(s, format_name, data, size, flags)` | 任意 | `copyCustomFormat` |
| OP-11 | `ntk_clipboard_items_create` / `_add_text` / `_add_html` / `_add_bytes` / `_free` と `ntk_clipboard_copy_multiple(s, const ntk_clipboard_items*, flags)` | 任意（builder はスレッドセーフでない） | `copyMultipleFormats` |
| OP-12 | `ntk_clipboard_clear(s)` | 任意 | `clearClipboard` |
| OP-13〜17 | `ntk_clipboard_paste_text` / `_paste_html`（`ntk_string**`）、`_paste_files`（`ntk_string_list**`）、`_paste_dib` / `_paste_custom(s, name, ntk_bytes**)` | 任意 | `pastePlainText` / `pasteHtml` / `pasteFiles` / `pasteImage` / `pasteCustomFormat` |
| OP-18 | `ntk_clipboard_has_format(s, name, int32_t* out_present)` | 任意 | `hasClipboardFormat` |
| OP-19 | `ntk_clipboard_get_formats(s, ntk_string_list**)` | 任意 | `getClipboardFormats` |
| OP-20 | `ntk_clipboard_get_preferred_format(s, ntk_string**)` | 任意 | `getPreferredClipboardFormat`（**バッファからハンドルに変わる。リネームだけではない**） |
| OP-21 | `ntk_clipboard_reserve_deferred(s, const char* const* formats, size_t count, ntk_clipboard_render_fn, void* user_data, ntk_release_fn)` / `ntk_clipboard_render_target_set(target, data, size)` | オーナーだけ | `reserveDeferredFormats` |
| OP-22 | `ntk_clipboard_recover_deferred_state(s)` | オーナーだけ | `recoverDeferredState` |
| OP-23〜27 | `ntk_clipboard_get_history(s, history_fn, user_data, uint32_t* out_id)`、`_restore_history_item` / `_delete_history_item(s, item_id, completion_fn, …)`、`_clear_unpinned_history(s, completion_fn, …)`、`_get_history_availability(s, availability_fn, …)` | 受付は任意、完了はオーナー（`CH:275-281`） | `getClipboardHistory` / `restoreHistoryItem` / `deleteHistoryItem` / `clearUnpinnedHistory` / `getClipboardHistoryAvailability` |
| OP-28 | `ntk_clipboard_cancel_request(s, uint32_t id)` | 任意 | `cancelClipboardRequest` |
| OP-29 | `ntk_clipboard_history_count` / `_item_id` / `_item_text` / `_item_content_type_count` / `_item_content_type_at` / `_item_timestamp_unix_ms`（`CH:322-341`） | 完了のコールバックの中だけ | （1.x は JSON） |

共通（`Common.h`）: `ntk_version`、`ntk_last_system_code`、`ntk_string_*` 3 本、`ntk_bytes_*` 3 本、`ntk_string_list_*` 3 本。

### 1.2 型と値

- `ntk_clipboard_error`（`int32_t`、`CH:41-63`）: 0 NONE〜19 UNKNOWN。**名前も番号も `WindowsClipboardErrorCode` の 0〜19 と同じ**。`BUFFER_TOO_SMALL`(7) は欠番で返らない
- 書き込みのフラグ（`CH:66-71`）: 0 DEFAULT、1 EXCLUDE_HISTORY、2 EXCLUDE_ROAMING、3 SENSITIVE。ほかのビットは `INVALID_PARAMETER`
- 形式名の別名（`WindowsClipboardManager.cpp:20-29`）: `CF_UNICODETEXT`、`CF_TEXT`、`CF_HDROP`、`CF_DIB`、`CF_DIBV5`、`CF_BITMAP`。has_format・copy_multiple・reserve_deferred で効き、**copy_custom / paste_custom では効かない**（1.x と同じ）
- `ntk_last_system_code()` は OS の値ではなく、多くはエラー値そのもの。本計画では使わない
- 文字列は UTF-8。入力の不正な UTF-8 は `INVALID_PARAMETER`。出力の対になっていないサロゲートは U+FFFD
- 検査の順が 1.x と違う: 引数（NULL、UTF-8、flags）を C ABI で先に見るので、閉じたセッションに不正な flags を渡すと `INVALID_PARAMETER`（1.x は `NOT_INITIALIZED`。native-toolkit `1.11.0:windows/WindowsLibrary/WindowsClipboardManager.cpp:327-328`）。C# は閉じたセッションを呼ぶ前に止めるので、公開 API には出ない（4.4）

### 1.3 構造体（`#pragma pack(push, 8)`、`CApiLayoutTest.cpp:79-91`）

| 構造体 | 大きさ | フィールドの位置 |
|---|---|---|
| `ntk_clipboard_session_options` | 24 | struct_size 0、reserved0 4、on_clipboard_changed 8、user_data 16 |
| `ntk_clipboard_history_handlers` | 40 | struct_size 0、reserved0 4、on_history_changed 8、on_history_enabled_changed 16、on_roaming_enabled_changed 24、user_data 32 |

`struct_size` は大きさ以上 4096 以下、`reserved0` は 0。options が NULL ならリスナー無し、handlers が NULL か 3 つとも NULL なら解除。

### 1.4 セッションの寿命

- 2 回目の create は、同じスレッドなら `NOT_SUPPORTED`、別スレッドなら `WRONG_THREAD`。**close が成功して free したあとは、また create できる**（`WindowsClipboardApi.cpp:289-296`）
- close は冪等。失敗しうる: `BUSY`（ほかのスレッドの同期呼び出し、**実行中の履歴の要求**、回復中にクリップボードを握られている）、`PARTIAL_STATE`、`CANCELED`（drain が 4 回で終わらない、または配送するものが無いのに終わらない。`WindowsClipboardApi.cpp:271-287`）、`MONITOR_REGISTER_FAILED`、`WRONG_THREAD`
- close が失敗したあとは半ば閉じた状態で、読み書きと履歴の受付は `NOT_INITIALIZED`。できるのは close の再試行と free だけ。**close を 1 回試した時点で、クリップボードの変化の通知は止まる**（`WindowsClipboardManager.cpp:281-283`）
- **close が成功しないまま free すると「放棄」になり、そのプロセスでは以後 create できない**（`CH:163-166`、`WindowsClipboardApi.cpp:201-209,802-820`）
- close は自分の drain のメッセージだけを処理し、成功すると `DestroyWindow` → `WM_RENDERALLFORMATS` で未描画の予約形式を実体化し、その後に release を呼ぶ

### 1.5 コールバック

| 型 | シグネチャ | スレッド |
|---|---|---|
| `ntk_clipboard_changed_fn` | `void(void*)` | オーナー。このセッション自身の書き込みは通知しない |
| `ntk_clipboard_history_changed_fn` | `void(void*)` | オーナー。追加のときだけ |
| `ntk_clipboard_flag_changed_fn` | `void(void*, int32_t)` | オーナー。前面でないか取得に失敗すると呼ばない（1.x と同じくあてにならない） |
| `ntk_clipboard_render_fn` | `ntk_clipboard_error(void*, const char* format_name, ntk_clipboard_render_target*)` | オーナー。`WM_RENDERFORMAT` と、close の中の `WM_RENDERALLFORMATS` |
| `ntk_clipboard_history_fn` | `void(void*, uint32_t id, ntk_clipboard_error, uint32_t system_code, const ntk_clipboard_history*)` | オーナー。受け付けた要求 1 件にちょうど 1 回。受付の呼び出しの中では来ない |
| `ntk_clipboard_completion_fn` | `void(void*, uint32_t, ntk_clipboard_error, uint32_t)` | 同上 |
| `ntk_clipboard_availability_fn` | `void(void*, uint32_t, err, uint32_t, int32_t history_enabled, int32_t roaming_enabled)` | 同上。失敗なら 2 つとも 0 |

- すべてのコールバックはセッションの CallbackGate を通る。free はほかのスレッドで走っているコールバックの終了を待ち、以後は届かない
- 履歴のハンドルと render target は、そのコールバックの間だけ有効
- render: `NONE` を返して `render_target_set` で 1 バイト以上を渡せば描画される。それ以外は描画されない。**1 段**（1.x は「大きさを聞く → 書く」の 2 段）
- reserve の `release` は、次の予約・このセッションの書き込み・ほかのプロセスの消去・recover の成功・close の成功・free のどれかで 1 回。予約の失敗（`PARTIAL_STATE` 以外）では呼び出しスレッドで戻る前に呼ぶ（S-4: 失敗時に自分でも解放すると二重解放）
- コールバックの中で close / free / set_history_handlers を呼ばない。provider の中では `render_target_set` 以外のクリップボード関数を呼ばない

### 1.6 履歴

- 受付は `PostMessage` するだけで、オーナーの WndProc で始まる。前面でなければ `NOT_FOREGROUND`
- 履歴が無効なら get / restore / delete / clear は `HISTORY_DISABLED`、availability は成功して 0 / 0
- キャンセルは論理的で、完了は `CANCELED` で 1 回だけ届く。close の中で待機中の要求は `CANCELED` として配送される
- `timestamp` は Unix ミリ秒（ticks が 0 なら 0、`ClipboardConvert.cpp:114-124`）。1.x は FILETIME のティックの文字列
- `item_text` はテキストを持たない項目で NULL、空テキストで `""`

## 2. 既存の C# 実装

出典: `Runtime/Clipboard/WindowsClipboardManager.cs`（3984 行）ほか。v8 の設計のとおり。

- ガードは既に二重（型は `UNITY_STANDALONE_WIN || UNITY_EDITOR`、`DllImport` と呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR`、`WindowsClipboardManager.cs:5,823`）。Editor ではネイティブを呼ばず `PlatformUnavailable`(1000)
- 呼べるのは Unity のメインスレッドだけ（`MainThreadRequired`）。**メインスレッドがオーナー**（実測で MAINSTA、v8 の 10 章 V-1）。`Initialize` は `EnsureStaApartment`（`:3176-3205`）で STA を確かめてから `initClipboardManager`
- 同期 API も、結果のイベントとコールバックは dispatcher で次の `Update` に届く（「共通イベント → per-call」、例外は個別に隔離）
- `DllImport` 30 本（ole32 3、ネイティブ 27）。`DLL_NAME` は `DEVELOPMENT_BUILD` で切り替え（`:168-172`）。`private static extern`
- コールバック 5 種は static readonly の delegate で保持（GCHandle なし）。変化の通知と履歴のイベントは dispatcher に積む。遅延描画の受け口（`OnRenderFormatNative`、`:3569`）は `WM_RENDERFORMAT` の中で同期に走り、2 段で答える（1 回目に provider を呼んで cache に置き、2 回目は cache を写す、`:2244-2297`）
- 読み出しは 2 回呼び出し（`ReadRaw`、`:1615-1695`）。`Empty` と `FormatUnavailable` は空の成功、文字列は `PtrToStringUni` で途中の NUL で切れる
- 履歴の完了は、ネイティブのコールバックで JSON 文字列を保存し、配送のときに `WindowsClipboardJsonParser` でパースする（`:2545,2611-2612`）
- 状態機械（Uninitialized / Running / Draining / ShutDown / ShutdownFailed）、`FinishShutdownAttempt` への集約、drain（`Update` で最大 60 フレーム・2 秒）、`Application.wantsToQuit` での quit の保留、static リセット（`:3750`）は v8 のとおり
- 公開の履歴の `Timestamp` は「1601 年起点の FILETIME のティック」（`WindowsClipboardHistoryItem.cs:28-33`）、`ToUtcTime()` は `FromFileTime`
- 公開の `Operation*` 定数の値は 1.x の関数名（`:177-255`）で、S-2 の期待値の照合キー
- 結果として返す失敗にも `LogError` を出す箇所がある（`ReadRaw`、`InvokeWrite`、`HasFormat`、`CanShutdownNow` など）。サンプルのログ判定と今のテストはこれを前提に通っている

## 3. 実装制約

- `agent-rules/coding-rules/common.md`: ネイティブが同期なら C# も同期、Awaitable は callback 版を包むだけ、コールバックは dispatcher 経由、`Windows` 接頭辞、共通化しない
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、英語の XML コメント。Clipboard はクリップボードの中身をログに出さない（`WindowsClipboardManager.cs:157-160`。v8 の決定）
- ガードは今の二重の形のまま
- 公開 API を変えない（基本情報の方針）
- 前例（Dialog v5・Notification v7）の約束: `DllImport` は `.dll` まで書き `internal static extern`、構造体は入れ子の `internal struct`（`Pack = 8`、`struct_size = Marshal.SizeOf<T>()`）、文字列の入力は UTF-8 を `AllocHGlobal` に置いて `finally` で解放、出力は data と size で写す、try の形は「finally を内側、catch を外側」、既知の差分の表、各段階の終わりに層 0

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

### 4.1 `DllImport`

`DllImport("windows-native-toolkit-capi-2.0.0.dll", CallingConvention = CallingConvention.Cdecl)`、`internal static extern`。ole32 の 3 本（`CoGetApartmentType` / `CoInitializeEx` / `CoUninitialize`）は今のまま。`DEVELOPMENT_BUILD` の切り替えは無くす。

| C | C# |
|---|---|
| `ntk_version` | `uint ()` |
| `ntk_clipboard_session_create` | `int (ref SessionOptions options, out IntPtr session)` |
| `ntk_clipboard_session_close` / `_clear` / `_recover_deferred_state` | `int (IntPtr session)` |
| `ntk_clipboard_session_can_close` | `int (IntPtr session)` |
| `ntk_clipboard_session_free` | `void (IntPtr session)` |
| `ntk_clipboard_set_history_handlers` | `int (IntPtr session, ref HistoryHandlers handlers)` と、解除用に `int (IntPtr session, IntPtr handlers)`（`EntryPoint` で同じ関数） |
| `ntk_clipboard_copy_text` | `int (IntPtr s, IntPtr text, uint flags)` |
| `ntk_clipboard_copy_html` | `int (IntPtr s, IntPtr fragment, IntPtr plainText, uint flags)` |
| `ntk_clipboard_copy_files` | `int (IntPtr s, IntPtr paths, UIntPtr count, uint flags)` |
| `ntk_clipboard_copy_dib` | `int (IntPtr s, byte[] dib, UIntPtr size, uint flags)` |
| `ntk_clipboard_copy_custom` | `int (IntPtr s, IntPtr formatName, byte[] data, UIntPtr size, uint flags)` |
| `ntk_clipboard_items_create` | `int (out IntPtr items)` |
| `ntk_clipboard_items_add_text` / `_add_html` | `int (IntPtr items, IntPtr formatName, IntPtr value)` |
| `ntk_clipboard_items_add_bytes` | `int (IntPtr items, IntPtr formatName, byte[] data, UIntPtr size)` |
| `ntk_clipboard_items_free` | `void (IntPtr items)` |
| `ntk_clipboard_copy_multiple` | `int (IntPtr s, IntPtr items, uint flags)` |
| `ntk_clipboard_paste_text` / `_paste_html` / `_get_preferred_format` | `int (IntPtr s, out IntPtr str)` |
| `ntk_clipboard_paste_files` / `_get_formats` | `int (IntPtr s, out IntPtr list)` |
| `ntk_clipboard_paste_dib` | `int (IntPtr s, out IntPtr bytes)` |
| `ntk_clipboard_paste_custom` | `int (IntPtr s, IntPtr formatName, out IntPtr bytes)` |
| `ntk_clipboard_has_format` | `int (IntPtr s, IntPtr formatName, out int present)` |
| `ntk_clipboard_reserve_deferred` | `int (IntPtr s, IntPtr formats, UIntPtr count, IntPtr renderFn, IntPtr userData, IntPtr releaseFn)` |
| `ntk_clipboard_render_target_set` | `int (IntPtr target, byte[] data, UIntPtr size)` |
| `ntk_clipboard_get_history` | `int (IntPtr s, IntPtr historyFn, IntPtr userData, out uint id)` |
| `ntk_clipboard_restore_history_item` / `_delete_history_item` | `int (IntPtr s, IntPtr itemId, IntPtr completionFn, IntPtr userData, out uint id)` |
| `ntk_clipboard_clear_unpinned_history` | `int (IntPtr s, IntPtr completionFn, IntPtr userData, out uint id)` |
| `ntk_clipboard_get_history_availability` | `int (IntPtr s, IntPtr availabilityFn, IntPtr userData, out uint id)` |
| `ntk_clipboard_cancel_request` | `int (IntPtr s, uint id)` |
| `ntk_clipboard_history_count` | `UIntPtr (IntPtr h)` |
| `ntk_clipboard_history_item_id` / `_item_text` | `IntPtr (IntPtr h, UIntPtr index, out UIntPtr size)` |
| `ntk_clipboard_history_item_content_type_count` | `UIntPtr (IntPtr h, UIntPtr index)` |
| `ntk_clipboard_history_item_content_type_at` | `IntPtr (IntPtr h, UIntPtr index, UIntPtr typeIndex, out UIntPtr size)` |
| `ntk_clipboard_history_item_timestamp_unix_ms` | `long (IntPtr h, UIntPtr index)` |
| `ntk_string_data` / `ntk_bytes_data` | `IntPtr (IntPtr handle)` |
| `ntk_string_size` / `ntk_bytes_size` / `ntk_string_list_count` | `UIntPtr (IntPtr handle)` |
| `ntk_string_list_at` | `IntPtr (IntPtr list, UIntPtr index, out UIntPtr size)` |
| `ntk_string_free` / `ntk_bytes_free` / `ntk_string_list_free` | `void (IntPtr handle)` |

`out` のハンドルは、呼ぶ前に `IntPtr.Zero` で初期化した変数に受け、`finally` で `!= IntPtr.Zero` のときだけ解放する（Dialog v5 の 5.5）。
コールバックの delegate（7 種、`[UnmanagedFunctionPointer(Cdecl)]`）は static readonly のフィールドで保持し、`Marshal.GetFunctionPointerForDelegate` で渡す。受け口は `[MonoPInvokeCallback]` の static。`user_data` はすべて `IntPtr.Zero`、`release` も `IntPtr.Zero`（J-7）。

### 4.2 引数の変換

| メソッド | 2.0.0 へ |
|---|---|
| `Initialize(enableChangeEvents, …)` | 状態の判定と `EnsureStaApartment` は今のまま。J-6 の版の確認のあと `session_create`（`on_clipboard_changed` は `enableChangeEvents` なら 4.4 の受け口、でなければ 0） |
| `CopyPlainText` / `CopyHtml` | UTF-8。`plainText` の `null` は `NULL`（ネイティブが `""` とし、`""` なら CF_UNICODETEXT を置かない。1.x と同じ Core） |
| `CopyFiles` | 各パスを UTF-8 にし、ポインタの配列を `AllocHGlobal` に並べる（JSON を作らない） |
| `CopyImage` | `copy_dib`。`byte[]` をそのまま渡す |
| `CopyCustomFormat` | `copy_custom`。空のバイト列は今と同じく C# が先に `InvalidArgument` で拒む（`WindowsClipboardManager.cs:1281-1288`） |
| `CopyMultipleFormats` | `items_create` → 要素ごとに `add_text` / `add_html` / `add_bytes` → `copy_multiple` → `finally` で `items_free`。**base64 の要素は C# で復号してから `add_bytes`**（J-3）。`Bytes(format, byte[])` で作ったものは内部の base64 を復号する（空の配列は base64 が `""` になり、1.x と同じく `InvalidParameter`）。**全部の要素の base64 を先に復号してから builder に渡す**ので、誤りが 2 つ以上あるときに返るコードの優先順位が 1.x と変わる（5.4） |
| 書き込みの `options` | `WindowsClipboardWriteOptions` の値をそのままフラグにする（None=0、ExcludeHistory=1、ExcludeRoaming=2、Sensitive=3、`WindowsClipboardPayloads.cs:17-30`。ネイティブのビットと同じ） |
| `HasFormat` | `has_format`。`out_present` は成功のときだけ読む |
| `ReserveDeferredFormats(providers)` | 形式名を UTF-8 の配列にして `reserve_deferred`（`render_fn` は 4.5 の受け口、`user_data` / `release` は 0）。staging・世代の決め方（`ApplyReservationOutcome`）は v8 のまま |
| 履歴の要求 | 4.6 |
| `SetHistoryEventsEnabled(enabled)` | 真なら 3 つの受け口を入れた `HistoryHandlers`、偽なら `NULL` で `set_history_handlers`。失敗するとネイティブは前のハンドラーに戻す（`WindowsClipboardApi.cpp:347-352`）。受け口は static なので C# からは違いが見えない |
| `CancelRequest(id)` | `cancel_request` |
| `CanShutdownNow` | セッションがあれば `session_can_close(session)`。**無ければネイティブを呼ばずに真**（1.x の `canDestroyClipboardManager` も、新しい C ABI の `can_close(NULL)` も真。`1.11.0:...WindowsClipboardManager.cpp:291-296`、`ClipboardCApi.cpp:57-60`） |
| `TryShutdown` / drain | `session_close`。4.7 |

**base64 の復号（J-3）**: 1.x のネイティブの `Base64Decode`（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsClipboardFormats.cpp:443-487`）と同じ規則を、`WindowsClipboardCApiMapping` の純粋な関数にする。

- `\r`、`\n`、空白、タブは飛ばす
- 残りが空、または長さが 4 の倍数でなければ不正
- `=` は最後の 2 文字にだけ置ける（それより前に `=` があれば不正）
- **各組で 3 文字目が `=` なら、4 文字目も `=` でなければ不正**（`XY=Z` を弾く、`:463-466`）。パディングは最後の組にだけ置ける
- 各組の 1・2 文字目は必ず base64 の文字。3・4 文字目は `=` でなければ base64 の文字
- 標準の文字（`A-Z`、`a-z`、`0-9`、`+`、`/`）以外は不正（非 ASCII も）。余りのビットは検査しない（1.x と同じ）
- 復号の結果が空なら不正
- 不正なら `InvalidParameter`（1.x の `CLIPBOARD_ERROR_INVALID_PARAMETER`、`1.11.0:...WindowsClipboardManager.cpp:563-566`）。**`Convert.FromBase64String` は規則が違う（空白の扱いなど）ので使わない**

### 4.3 結果の変換

| 状況 | 結果 |
|---|---|
| ネイティブの戻り値 0〜19 | そのまま `WindowsClipboardErrorCode`（名前と番号が同じ、1.2） |
| 読み出しの `FORMAT_UNAVAILABLE` / `EMPTY` | 1.x と同じく空の成功（`ClassifyFirstRead` の分け方を保つ）。**ほかの失敗のコードは空の成功にしない**（1.x の「失敗のコードで大きさ 0 は失敗」を保つ） |
| 読み出しの成功 | 文字列は `ntk_string_data` と `size` のバイト列を UTF-8 で読み、**最初の NUL で切る**（1.x の `PtrToStringUni` と同じ。`paste_text` に途中の NUL は無い）。バイトは `size` だけ写す。一覧は要素ごとに写す |
| 読み出しの成功で中身が空 | `GetFormats` / `PasteFiles` の 0 件は `Success`（`IsEmpty = false`。1.x の `[]` と同じ、`WindowsClipboardManager.cs:1399-1402`）。`PastePlainText` / `PasteHtml` の `""` は `Success("")`。`GetPreferredFormat` の `""` だけが `Empty`（1.x と同じ、`:1538-1546`） |
| 版が合わない・DLL が無い・関数が無い・形式が違う（J-6） | `BridgeUnavailable`（1001） |

**捕まえる例外（J-13）**: API ごとの今の捕まえ方を保つ。足すのは `BadImageFormatException`（`BridgeUnavailable`）だけ。

| API | 捕まえる例外とコード | そのほかの例外 |
|---|---|---|
| 書き込み（`InvokeWrite`）、`HasFormat`、`CanShutdownNow` | `DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` → `BridgeUnavailable` | 呼び出し側に出る（今と同じ） |
| 読み出し | 上の 3 つ → `BridgeUnavailable`、`OutOfMemoryException` → `OutOfMemory` | 呼び出し側に出る（今と同じ） |
| `Initialize`、`StartRequest`、終了の試行 | 上の 3 つ → `BridgeUnavailable`、そのほか → `Unknown` | — |

2.0.0 では C# の側で `AllocHGlobal` と UTF-8 の変換が増える。書き込みでここから `OutOfMemoryException` が出ると、今と同じく呼び出し側に出る（1.x でもマーシャラーの確保の失敗は同じく出ていた）。`LogError` の箇所は今のまま（J-9）。

**1.x の 2 回呼び出しに固有の結果は無くなる**: 読み出しで `BufferTooSmall` が返ることも、大きさの変化による再試行（`SizeChangedRetryBudget`）も無い。JSON のパースの失敗（`ResultParseFailed`）も、履歴・一覧・可用性では起きなくなる（5.4）。

### 4.4 スレッドとコールバック

- 同期 API はメインスレッド（オーナー）から呼ぶ。今の `MainThreadRequired` の判定を保つ
- セッションのハンドルは `s_session`（static）に持つ。**終了以外の操作**は、ハンドルが `IntPtr.Zero` ならネイティブを呼ばずに今の判定（`NotInitializedByHost` など）を返す。**close を試したあと（Draining / ShutdownFailed）は、今の判定（`ShuttingDown`）で止め、半ば閉じたセッションに操作を渡さない**。終了の試行は 4.7
- 変化の通知・履歴のイベント・可用性の変化の受け口は、今と同じく dispatcher に積むだけ
- 例外は受け口の中で捕まえ、ネイティブに返さない

### 4.5 遅延描画（J-8）

受け口 `OnRenderFormatNative(IntPtr userData, IntPtr formatName, IntPtr target)`（オーナーで同期）:

1. 形式名を UTF-8 で読む
2. 今と同じく `s_renderStaging`、なければ `s_renderProviders` から provider を引く。無ければ `INVALID_PARAMETER` を返す（1.x と同じ）
3. provider を 1 回呼ぶ。`null` か長さ 0 なら `INVALID_DATA` を返す（1.x と同じ）
4. `render_target_set(target, bytes, size)` を呼び、**その戻り値をそのまま返す**（`INVALID_PARAMETER` / `OUT_OF_MEMORY` を `NONE` で上書きしない。`ClipboardConvert.cpp:143-163`）
5. 例外は捕まえて `UNKNOWN` を返す

- **2 段と cache（`s_renderCache`）は無くす**。1.x でも provider を呼ぶのは 1 回の描画につき 1 回だった（1 回目で呼び、2 回目は cache、`WindowsClipboardManager.cs:2253-2267`）ので、利用者から見た呼ばれ方は変わらない。1.x の「2 回目で大きさが変わると黙って捨てる」危険も無くなる
- render の戻り値は、ネイティブではログにしか使われない（`WindowsClipboardDeferredProvider.cpp:71-79`）
- `release` は使わない（provider は static の辞書にあり、`user_data` を持たない）。S-4 の二重解放は起きない
- close の中の `WM_RENDERALLFORMATS` でも同じ受け口が呼ばれる。自分の Paste の中でも、CallbackGate を通って同期で呼ばれる

### 4.6 履歴

- 受付は今の `StartRequest`（台帳、同時実行ガード、ticket）のまま。ネイティブの戻り値が 0 以外なら、今の「受け付ける前の拒否」の経路（戻り値 0、結果は 1 回だけ届く）。`out_id` は成功のときだけ読む
- **完了の受け口の中で、その場で結果を managed の値に写す**（ハンドルはコールバックの間だけ有効）
  - `get_history` は項目ごとに id・text・content types・timestamp を読み、`WindowsClipboardHistoryItem` の一覧にする
  - **id が空の項目は、1.x と同じく `LogWarning` を出して捨てる**（`WindowsClipboardJsonParser.cs:117-125`。新しいネイティブは id が無ければ `""` を入れる、`WindowsClipboardApi.cpp:603`）
  - text は、NULL も `""` も `null`（1.x と同じ、`WindowsClipboardJsonParser.cs:137`）
  - 可用性は 2 つの `int32_t` を保存する
  - 保存先は `pending` の新しい欄（今の `pending.Json` の代わり）。**拒否の詳細の文言（例: `itemId was null or blank`）を運ぶ欄は別に残す**（今は `pending.Json` が兼ねている、`WindowsClipboardManager.cs:2436,2543,2564,2585`。`WindowsClipboardPlayerTests.cs:159-160` がこの文言を見る）
- **写す処理は受け口の中の別の try で囲む。** 途中で例外が起きたら、`OutOfMemoryException` は `OutOfMemory`、ほかは `Unknown` を結果として保存し、**`MarkUndelivered` と `QueueDelivery` を必ず通す**（結果を 1 回だけ届け、同時実行ガードを外す）。1.x では壊れた JSON は配送のときに `ResultParseFailed` になっていた。native も変換の失敗を `OUT_OF_MEMORY` の完了として必ず届ける（`ClipboardHistoryCApi.cpp:69-87`）
- **`Timestamp` は FILETIME のティックに変換する**（J-1）: `unix_ms == 0` なら 0、そうでなければ `(unix_ms + 11644473600000) * 10000`。`checked` で計算し、溢れたら 0（ネイティブが出す範囲では溢れない）。`unix_ms` がちょうど 1970-01-01 00:00:00.000 のときも 0 になる（1.x の tick 0 と同じ扱い）。`ToUtcTime()` は今のまま
- 配送（`DeliverClaimed`）は、保存した値から結果を作る。パースはしない
- 完了は受付の呼び出しの中では来ず、オーナー（メインスレッド）で来るので、今の id での対応付けのまま（`user_data` は使わない）
- **待機中の要求の `CANCELED` は、`session_close` の呼び出しの中で届く**（`CH:150-152`）。つまり受け口が `TryShutdown` / `OnDestroy` / drain の中から、`FinishShutdownAttempt` より前に呼ばれる。受け口は `MarkUndelivered` と `QueueDelivery` だけを行い、`DrainRequestRegistry` は結果の出た要求を上書きせずに配送するので、今の仕組みのままで正しく 1 回届く

### 4.7 終了（J-5）

- **セッションが無い（`s_session == IntPtr.Zero`）ときの終了の試行は、ネイティブを呼ばずに `completed = true` の成功**（1.x の uninit も未初期化なら TRUE、`1.11.0:...WindowsClipboardManager.cpp:252`）。`OnDestroy`（`WindowsClipboardManager.cs:752-755`）と `OnWantsToQuit`（`:3127-3128`）は状態を見ずに `RunShutdownAttempt` を通るので、これが無いと終端の失敗になり、quit で `ResumeQuit` が `LogError` を出す。この分岐はネイティブのガード（`#if`）の外に置く
- 1 回の試行は `session_close`。`ClassifyShutdown` の分け方（`None` / `Busy` / `MonitorRegisterFailed` / `Canceled` / `PartialState` は「まだ終わっていない」）は今のまま
- **成功したら、その場で `session_free` してハンドルを `IntPtr.Zero` に戻す**。次の `Initialize` は新しいセッションを作る
- **close が成功しないうちは free しない**（放棄すると、そのプロセスで二度と作れないため）。drain（最大 60 フレーム・2 秒）の再試行は今のまま。実行中の履歴の要求による `BUSY` も、この再試行で待つ（オーナーのメッセージループが回るのは Unity のフレームの間）
- drain の中の `PartialState` のときの `recover_deferred_state` は今のまま呼ぶ。close を試したあとのセッションでは、1.x も 2.0.0 も `NOT_INITIALIZED` で何もしない（`1.11.0:...WindowsClipboardManager.cpp:703-708`）
- `OnDestroy` / `OnDisable` の 1 回だけの試行で終わらなかったら、ハンドルは残したまま（プロセスの終了まで。ネイティブの資源も残る）。`ShutdownFailed` のままにする。同じプロセスで新しい Manager を作っても、C# が `ManagerDestroyed`（破棄のあと、`:801-806`）か `ShuttingDown`（`:815-820`）で止めるので、ネイティブの 2 つ目のセッションには届かない
- quit（`Application.wantsToQuit`）の流れは今のまま。close が成功すれば `WM_RENDERALLFORMATS` で予約が実体化してから終わる（Core は 1.x と同じ）
- **close は、待っている要求を呼び出しの中で配送して、1 回目で終わりうる**（`WindowsClipboardApi.cpp:268-287`）。1.x は、待っている要求があると 1 回目の uninit が必ず「まだ終わっていない」だった。このため Draining の期間ができないことがある（5.4、J-12）

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | ガード | 内容 |
|---|---|---|---|
| 既存変更 | `Runtime/Clipboard/WindowsClipboardManager.cs` | 今のまま（型は `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`、ネイティブの呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR`） | 1.x の `DllImport` 27 本とラッパー、`ReadRaw` の 2 回呼び出し、遅延描画の 2 段と cache、履歴の JSON の保存を、`WindowsClipboardCApi` の呼び出しに置き換える。セッションのハンドル。公開 API・状態機械・台帳・drain・quit・static リセットはそのまま（`ResetCore` でハンドルは消さない。Editor ではハンドルを持たない）。古い XML コメントを直す（`:1400-1401`） |
| 新規作成 | `Runtime/Clipboard/WindowsClipboardCApi.cs` | 型は `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`、`DllImport` と呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR` | `internal static`。入れ子の `SessionOptions` / `HistoryHandlers`、delegate、`extern`、UTF-8 の入出力、ハンドルの読み取りと解放 |
| 新規作成 | `Runtime/Clipboard/WindowsClipboardCApiMapping.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | `internal static`。base64 の復号（J-3）、Timestamp の変換（J-1）、読み出しの結果の分け方、履歴の項目の規則（id が空なら捨てる、text の `""` は `null`）、例外の変換、J-6 の版判定。ネイティブを呼ばない |
| 削除 | `Runtime/Clipboard/WindowsClipboardJsonParser.cs` | — | ネイティブから JSON が来なくなる（親課題 README の「JSON 廃止で消えるもの」）。`internal` なので公開 API は変わらない |
| 削除と移設 | `Tests/Runtime/WindowsClipboardJsonParserTests.cs` | — | 消す。ただし次は `WindowsClipboardCApiMappingTests` に移す: `ToUtcTime` のテスト（`:185,200`）、id が空の項目を捨てる、text が無い、contentTypes が無い |
| 非変更（使わなくなる） | `Runtime/Clipboard/WindowsClipboardJsonBuilder.cs` とそのテスト | 今のまま | **公開の型なので残す**（J-2 と同じ考え方）。XML コメントに「2.0.0 からは内部では使わない。互換のために残す」と書く |
| 既存変更 | `Runtime/Clipboard/WindowsClipboardErrorCode.cs` | 今のまま | XML コメントの `WindowsLibrary.dll`（`:10`）と `:20` を直す。`BufferTooSmall` と `ResultParseFailed` は、読み出し・履歴では起きなくなると書く（値は残す） |
| 既存変更 | `Runtime/Clipboard/WindowsClipboardPayloads.cs` | 今のまま | `Base64()` の XML コメントに、1.x と同じ規則で検証すると書く |
| 既存変更 | `Runtime/Clipboard/WindowsClipboardHistoryItem.cs`、`WindowsClipboardStringListResult.cs` | 今のまま | `Timestamp` の XML コメントに、ミリ秒より細かい桁は 0 になると書く（値の意味は変えない）。JSON に触れた古い記述（`WindowsClipboardHistoryItem.cs:22-24,30-31`、`WindowsClipboardStringListResult.cs:11-13`）を直す |
| 既存変更 | `Plugins/Windows/VERSION.txt`、`Editor/Build/PreBuildProcessor.cs` | — | **1.x の追加のピン（Dialog の J-6）を消す**。`extra_*` のキーと処理を消し、`VERSION.txt` のコメントを「3 つの Manager の `DLL_NAME` と一致（`.dll` 付き）」に戻す。1.x の DLL（`unity-windows-native-toolkit(-debug).dll`）が `Plugins/Windows` から消えることを確かめる |
| 既存変更 | `Tests/Runtime/WindowsClipboardManagerDispatchTests.cs` | 今のまま | 1.x 固有の仕組みを確かめるテスト（約 30 本: `ClassifyFirstRead` / `ClassifySecondRead`、`ReadRawForTests`、`RenderForTests` と cache）のうち、2 回呼び出しと cache の仕組みそのものを見るものは消す。**公開の結果にかかわる規則は、新しい仕組みで同じ結果になるテストに移す**（「失敗のコードを空の成功にしない」、`FORMAT_UNAVAILABLE` / `EMPTY` は空の成功、`GetPreferredFormat` の `""` は `Empty`、provider が無い・null・空・例外）。消すもの・移すものの一覧は、手順 4 の最初に作って実装結果ファイルに書く |
| 既存変更 | `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs` | 今のまま（`#if UNITY_EDITOR`） | 新しいテスト用フックに合わせる。`ResultParseFailed` を見るテスト（`:1373-1386`）は、「変換が例外を投げても 1 回だけ届く」（4.6）に置き換える。「close の中で完了が届く」「セッションが無いときの終了は成功」の注入テストを足す |
| 新規作成 | `Tests/Runtime/WindowsClipboardCApiMappingTests.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | 層 1（7.1） |
| 既存変更 | `Tests/PlayMode/WindowsClipboardPlayerTests.cs` | 今のまま（`UNITY_STANDALONE_WIN && !UNITY_EDITOR`） | 7.3 の追加 |
| 既存変更 | `scripts/windows_clipboard_sample_expected.py` | — | **J-12**: ブロック D の `ForceInitializeWhileDraining` の行（`:154-157`）だけ、`initClipboardManager NG ShuttingDown` を必須から外し、drain が 1 回目で終わる場合（`initClipboardManager OK` が続く）も通す。`getClipboardHistory` の行も `NG Canceled \| OK` にする。コメントに理由を書く |
| 既存変更 | `scripts/verify_unity_windows.sh` | — | IL2CPP で流す `--il2cpp` を足す（J-10）: Editor のスクリプトで Standalone Windows の backend を IL2CPP、x86_64 に固定してビルドし、専用の出力・XML・ログに書き出して `report_tests` で判定する。終わったら設定を戻す。`:13-15` の debug DLL の注記を直す |
| 非変更 | `Runtime/UI/Windows/Clipboard/*`、`Runtime/Resources/UI/Windows/Clipboard/*`、`Tests/PlayMode/WindowsClipboardSample*`、`scripts/check_windows_clipboard_sample_log.py` | — | サンプルと、ほかの S-2 の期待値は変えない（J-2 で照合キーも同じ） |
| 既存変更 | `artifact/topics/windows-c-abi-2/README.md` | — | 事実の誤りを直す: 2.1 の「リネームのみ 4」（`getPreferredClipboardFormat` はハンドルに変わる）、5.2 の `deleteHistoryItem` と `pasteCustomFormat` の「同上」、3.1 と 5.2 の「close の BUSY は他スレッドの読み書き」（実行中の履歴の要求と回復も）、3.3 の「release は必ず呼び出しスレッドで」（`PARTIAL_STATE` は保持、書き込みではオーナー）、3.2 の「同じスレッドを両方のオーナーにできない」（先に STA にすれば兼ねられる）。「移行時にやること」を 2 本置きの撤去で締める |
| 既存変更 | `agent-rules/coding-rules/testing.md` | — | 層 1 の本数、`WindowsClipboardJsonParserTests` の削除、`:376` の本数、`:414` の古い記述 |
| 新規作成 | `artifact/features/clipboard/results/<日付>-windows-clipboard-implementation-feature-result-v7.md` | — | 5.6 の手順 8（既存の v1〜v6 は上書きしない） |

### 5.2 判断（開発者の承認済み、2026-09-27）

| # | 内容 | 決定 |
|---|---|---|
| J-1 | 履歴の `Timestamp` | 公開の値は FILETIME のティックのまま。Unix ミリ秒から C# で変換する（4.6）。0 は 0 |
| J-2 | `Operation*` の値 | 変えない。関数名が変わっても「操作の名前」として残す |
| J-3 | 不正な base64 | 1.x のネイティブと同じ規則で C# が検証し、不正なら `InvalidParameter`（4.2） |
| J-12 | S-2 の `ForceInitializeWhileDraining` | 2.0.0 の close は待っている要求を呼び出しの中で配送して 1 回目で終わりうるので、Draining の期間ができないことがある。**この 1 行の期待値だけ、両方の結果を許す形に変える**（5.1）。「drain の最中の `Initialize` は `ShuttingDown`」は、層 2a の注入テスト（既存）で確かめ続ける。5.4 に書く |

### 5.3 判断（本計画で決めたもの）

| # | 内容 | 決定 |
|---|---|---|
| J-4 | Editor | 今と同じくネイティブを呼ばず `PlatformUnavailable`。DLL を Editor で読まないので、ドメインリロードの後片付け（親課題 README「ドメインリロード」）は要らない |
| J-5 | 終了と放棄 | セッションが無ければ成功。close が成功したら free。成功しないうちは free しない（4.7） |
| J-6 | DLL と C# の版 | `Initialize` の最初のネイティブ呼び出しの前に `(ntk_version() >> 16) == 2` を確かめる。違えば `BridgeUnavailable`（1001）で、セッションは作らない。`DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` も 1001 |
| J-7 | コールバックの登録 | static の delegate、`user_data` / `release` は 0 |
| J-8 | 遅延描画 | 1 段にし、cache を無くす（4.5） |
| J-9 | ログ | `LogError` の箇所は今のまま。新しくは足さない（Notification の 5.5 と同じく、結果として返す失敗に新しい `LogError` を足すと、今の期待値が移行後だけ崩れるため） |
| J-10 | IL2CPP | Clipboard が最後なので、Clipboard を移したあとに 3 機能まとめて IL2CPP の Player で 1 回流す（Dialog の J-10）。流す仕組みは `verify_unity_windows.sh --il2cpp`（5.1） |
| J-11 | COM のアパートメント | `EnsureStaApartment` は今のまま。Unity のメインスレッドは MAINSTA（実測）なので、Notification の `manager_create` が MTA を試みても STA のまま（`RPC_E_CHANGED_MODE` を受け入れる）。Clipboard と Notification の両方をメインスレッドで使える |
| J-13 | 捕まえる例外 | API ごとの今の捕まえ方を保ち、`BadImageFormatException` だけ足す（4.3 の表） |

### 5.4 既知の差分（意図したもの）

既存の公開 API は変えないが、次の振る舞いは変わる。XML コメントに書き、実装結果ファイル経由で次の版のマニュアルへ引き継ぐ。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 履歴の `Timestamp` の精度 | 100 ナノ秒 | ミリ秒（下の桁は 0） |
| 読み出しの `BufferTooSmall` | 大きさが呼び出しの間に変わり続けると返った | 返らない（1 回の呼び出しで取る） |
| 履歴・一覧・可用性の `ResultParseFailed` | JSON が壊れていれば返った | 返らない。履歴を写す途中の例外は `OutOfMemory` / `Unknown` |
| 対になっていないサロゲートを含む文字列 | UTF-16 のまま渡した・受け取った | 入力は `Encoding.UTF8` で、出力はネイティブで U+FFFD |
| `CopyMultipleFormats` で誤りが 2 つ以上あるとき | 要素を 1 件ずつ（名前 → 重複 → 種類 → base64 → DIB / HDROP の検証）見て、最初の誤りを返した。flags は最後に見た | 全部の base64 を C# で先に検証し、そのあとネイティブが flags を先に見てから要素を見る。例: 形の正しい base64 で DIB として不正な要素の後ろに不正な base64 があると、1.x は `InvalidData`、移行後は `InvalidParameter` |
| 待っている要求があるときの終了の 1 回目（J-12） | 必ず「まだ終わっていない」で、Draining の期間ができた。その間の `Initialize` は `ShuttingDown` | 待っている要求を呼び出しの中で `Canceled` として配送し、1 回目で終わりうる。そのときは Draining の期間が無く、直後の `Initialize` は成功する。WinRT の処理が既に始まっていれば `Busy` で、1.x と同じく Draining になる |
| DLL の形式が違う（`BadImageFormatException`） | `Initialize` は `Unknown`、`DllNotFound` / `EntryPointNotFound` だけを捕まえる API（書き込み、読み出し、`HasFormat`、`CanShutdownNow`）では呼び出し側に出た | `BridgeUnavailable` |

### 5.5 呼び出しの流れと契約

- 同期 API: 今の guard → 引数の検査 → UTF-8 の確保 → ネイティブ → `finally` で解放 → 結果を作る → dispatcher に積む（今と同じ届き方）。try の形は Dialog v5 の 5.5 と同じ
- 非同期 API: 今の `StartRequest` → 受付 → 完了の受け口で写す（失敗しても必ず 1 回届ける）→ 台帳 → 配送
- 例外: 受け口（7 種）はすべて try/catch で囲み、ネイティブに返さない。API ごとの捕まえ方は 4.3
- 購読者の例外の隔離（`InvokeInOrder`、`RaiseIsolated`）は今のまま

### 5.6 実装順

各段階の終わりに層 0（`scripts/verify_unity_windows.sh` の Player ビルド）を流す。

1. **1.x のまま**、7.3 の「1.x で記録」のテストを足し、1.x の結果を期待値として記録する
2. `WindowsClipboardCApiMapping` と層 1 のテスト（`WindowsClipboardJsonParserTests` から移すものを含む）
3. `WindowsClipboardCApi`
4. `WindowsClipboardManager` の置き換え（同期 API → 遅延描画 → 履歴 → 終了の順）。最初に、Dispatch のテストで消すもの・移すものの一覧を作る。層 1 と層 2a の既存テストを新しい仕組みに合わせる
5. 層 2b: 今の Player テスト、S-2 の全ボタンと押した回ごとの期待値（J-12 の 1 行を除いて変えない）、M-19 が通ること（`--include-destructive`）
6. 1.x の追加のピンを消し（5.1）、Player に 1.x の DLL が入らないこと、Dialog・Notification・Clipboard の Player テストがすべて通ることを確かめる
7. `verify_unity_windows.sh --il2cpp` を足し、IL2CPP の Player で 3 機能の層 2b を 1 回流す（J-10）
8. 実装結果ファイルに 5.4 の表を書き写す（write-manual が読む）

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | メインスレッド以外から呼ぶ | C# | `MainThreadRequired` | 2b（S-7、既存） |
| E-2 | 初期化の前・終了のあと | C# | `NotInitializedByHost` / `ShuttingDown` | 2b（ブロック D、既存） |
| E-3 | Editor | C# | `PlatformUnavailable` | 2a（既存） |
| E-4 | DLL が無い・major が 2 でない（J-6） | C# | `BridgeUnavailable` | 1（版判定、例外の変換）。実物は起こせない |
| E-5 | STA でない | C# | `ApartmentUnavailable` | 1（既存）。実物は起こせない |
| E-6 | 引数の誤り（null、空の一覧など） | C# | `InvalidArgument` | 2b（ブロック D、既存） |
| E-7 | 不正な base64（J-3） | C# | `InvalidParameter` | 1、2b |
| E-8 | ネイティブが引数を拒む（重複、組み合わせ、空のバイト列、DIB の検証） | ネイティブ | `InvalidParameter` / `InvalidData` | 2b（S-2 のブロック D の期待値、既存） |
| E-9 | ほかのプロセスがクリップボードを握っている | ネイティブ | `Busy` | 起こせない |
| E-10 | 空のクリップボード・形式が無い | ネイティブ | 空の成功 | 2b（既存） |
| E-11 | 履歴が無効 | ネイティブ | `HistoryDisabled` | 2b（ブロック C2、既存） |
| E-12 | 前面でない | ネイティブ | `NotForeground` | 2b（ブロック B、既存） |
| E-13 | 削除された項目 | ネイティブ | `ItemDeleted` | 2b（ブロック D、既存） |
| E-14 | 要求のキャンセル | ネイティブ | `Canceled` | 2b（9 章、既存） |
| E-15 | close が `BUSY` / `PartialState` | ネイティブ | drain で再試行 | 2a（注入、既存）。実物の `BUSY` は起こしにくい |
| E-16 | drain が上限に達する | C# | `ShutdownTimeout` | 2a（既存）。実物（M-20b）は起こせない |
| E-17 | 予約の失敗（`PartialState`） | ネイティブ | 世代を併合 | 1（`ApplyReservationOutcome`、既存） |
| E-18 | provider が null・空・例外、`render_target_set` の失敗 | C# | 描画しない（戻り値を返す） | 1（受け口の関数） |
| E-19 | 破棄のあとの `Initialize` | C# | `ManagerDestroyed`（ネイティブの 2 つ目のセッションには届かない） | 2a（既存） |
| E-20 | 履歴を写す途中の例外 | C# | `OutOfMemory` / `Unknown` を 1 回だけ届ける | 1、2a（注入） |
| E-21 | セッションが無いときの終了（`OnDestroy`、quit） | C# | `completed = true` の成功 | 2a（注入） |
| E-22 | close の中で待機中の要求が `Canceled` で届く | ネイティブ | 1 回だけ届く | 2a（注入）、2b（S-2 の `RequestAndImmediateShutdown`、既存） |

## 7. テスト方針

### 7.1 層 1（EditMode）

- 構造体 2 つの大きさと全フィールドの位置（1.3）
- base64 の復号（J-3）: 正しい形、空白と改行、長さが 4 の倍数でない、`=` の位置（途中、最後の組以外）、**`XY=Z`（`"QQ=Q"`）**、標準でない文字（`-`、`_`、非 ASCII）、空、復号して空
- Timestamp の変換（J-1）: 0、1970-01-01 00:00:00.000、今日の値、負の値、溢れる値
- 読み出しの結果の分け方: `FORMAT_UNAVAILABLE` / `EMPTY` → 空の成功、**ほかの失敗のコードは失敗のまま**、文字列の最初の NUL で切る、0 件の一覧は `Success`、`""` の文字列は `Success("")`、`GetPreferredFormat` の `""` だけ `Empty`
- 履歴の項目の規則: id が空の項目を捨てる、text の NULL と `""` は `null`、contentTypes が無い
- `ToUtcTime`（`WindowsClipboardJsonParserTests` から移す）
- 遅延描画の受け口の関数: provider が無い → `INVALID_PARAMETER`、null / 空 → `INVALID_DATA`、例外 → `UNKNOWN`、成功 → `render_target_set` が 1 回、`render_target_set` の失敗の値（`INVALID_PARAMETER` / `OUT_OF_MEMORY`）がそのまま返る
- 版判定（J-6）と例外の変換（`BadImageFormatException` を含む）
- 既存の層 1 のテストのうち、公開の結果にかかわる規則は、新しい仕組みで同じ結果になることを確かめる形に移す（5.1）

### 7.2 層 2a（PlayMode・Editor、既存の `WindowsClipboardManagerIntegrationTests`）

- 今の 65 本のうち、`ResultParseFailed` を見るもの（`:1373-1386`）は「履歴を写す途中の例外でも、イベントと per-call が 1 回ずつ届き、Awaitable が終わり、同時実行ガードが外れる」に置き換える。ほかは新しいテスト用フックで通す（拒否の経路、ちょうど 1 回の配送、drain、quit、キャンセル、同時実行ガード、例外の隔離、破棄と重複 instance）
- 足す: 「close の中で完了が届いても 1 回だけ配送される」（E-22）、「セッションが無いときの `OnDestroy` と quit は成功で、`LogError` を出さない」（E-21）

### 7.3 層 2b・層 3（PlayMode・Player）

- **今の Player テスト（`WindowsClipboardPlayerTests` 16 本、`SampleScreen` 2 本、`SampleRun` 5 本、`SampleQuit` 1 本）と S-2 の全ボタン・押した回ごとの期待値（J-12 の 1 行を除く）・M-19 を、期待値を変えずに通す。これが移行の検証。** Get History の時刻が今から 10 分以内であること（`WindowsClipboardPlayerTests.cs:292-298`）は、J-1 の変換の確認にもなる
- 多くのネイティブ関数は `Destructive` のサンプルの実行（S-2）でしか通らないので、手順 5 は `--include-destructive` で流すことが前提（既定の実行は Player テスト 48 本のうち 31 本、`testing.md:376`）
- 追加（Manager を直接呼ぶ。「1.x で記録」は 5.6 の手順 1 で 1.x のまま流して期待値を記録する）:
  - 不正な base64 の `CopyMultipleFormats`（`Base64(format, "@@@@")`、`"QQ="`、`"Q=Q="`、`"QQ=Q"`）が `InvalidParameter`、空白と改行を含む正しい base64 が成功（1.x で記録）
  - `CopyMultipleFormats([Bytes("X", new byte[0])])`（base64 が `""` になる）が `InvalidParameter`（1.x で記録）
  - `CopyMultipleFormats([Base64("CF_DIB", 形は正しいが DIB として不正), Base64("X", "@@@@")])`: 1.x は `InvalidData`（1.x で記録）、移行後は `InvalidParameter`（5.4。期待値を移行後に書き換える）
  - セッションが無いとき（`ShutdownWithDrain` の後）の `CanShutdownNow` が真（1.x で記録）
  - 履歴の要求を待たせたまま `TryShutdown` した 1 回目の `completed`（1.x で記録。移行後は変わりうる、5.4。移行後は `true` / `false` のどちらでも、要求がちょうど 1 回届くことを確かめる）
  - `ShutdownWithDrain` のあとに `Initialize` し直して書き込みと読み出しが通ること（close と free のあとの作り直し。1.x で記録）
  - **`extern` の結び付け**: NULL を渡しても安全な関数を `IntPtr.Zero` で直接呼び、例外なく戻ること（`session_can_close`、`session_free`、`items_free`、`ntk_string_free` / `ntk_bytes_free` / `ntk_string_list_free`、`history_count`、`history_item_*`、`render_target_set`（NULL は `INVALID_PARAMETER`）、`cancel_request`（NULL のセッションは `INVALID_PARAMETER`））。ほかの関数は、`--include-destructive` の実行で既存のテストが通す
- Mono で走る。IL2CPP は J-10

### 7.4 手動

今と同じ（層 3 の見え方、M-23、M-20b、D-9、Exclude Roaming の効き目、cross-platform-testing README の「自動テストで確かめていないもの」）。移行では増やさない。

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。起こせない失敗（E-4、E-5、E-9、E-16）は、変換を層 1 で押さえ、構造をコードレビューで確かめる。

**確かめないもの**:

| 項目 | 理由 | 押さえ方 |
|---|---|---|
| close の中の `WM_RENDERALLFORMATS` で provider が呼ばれること | native の単体テストでも provider 側は未検証 | M-19 の期待値で間接的に見る（予約を持ったまま終了し、終了後に形式が残るか） |
| 実行中の履歴の要求による close の `BUSY` と、その解消 | 起こすには履歴の要求を close と重ねる必要がある | drain の再試行（既存）で吸収。コードレビュー |
| close が待っている要求を呼び出しの中で配送すること（J-12） | タイミング次第 | 層 2a の注入（E-22）と、S-2 の `ForceInitializeWhileDraining` / `RequestAndImmediateShutdown` の両方を許す期待値 |
| 履歴を写す途中の例外（E-20） | 実物は起こせない | 層 1 と層 2a の注入 |
| セッションが無いときの `OnDestroy` と quit（E-21） | Player では、セッションの無い Manager を破棄する経路をテストから作りにくい | 層 2a の注入 |
| 自分の Paste の中で render の受け口が CallbackGate を通って同期で走ること | 直接は見えない | S-2 の M-17（画面の `Render:` 行）で間接的に見る |
| WinRT の継続がオーナーの STA で再開すること | native 側でも未確認 | 既存の履歴のテスト（オーナーで完了が届く）で間接的に見る |

### 8.2 OS が出す画面・許可・前提の OS 設定

今と同じ（前面を取る、履歴の設定をレジストリで切り替えて戻す、履歴をオフにした直後の消去を待つ、テスト中に PC に触らない。`artifact/topics/cross-platform-testing/README.md`）。

### 8.3 呼び出し側を止める OS の画面

なし。
