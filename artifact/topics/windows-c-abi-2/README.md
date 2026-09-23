# Windows の C ABI が 2.0.0 に置き換わり、P/Invoke 層を書き直す

- 記録日: 2026-09-23
- 分類: 横断課題（移行）。**機能追加ではない。** clipboard / notification / dialog の 3 機能にまたがる
- 発見経緯: native-toolkit 側（`feature/NTKIT-16`）から、1.x の C ABI を削除し 2.0.0 に置き換えた旨の申し送りを受けた
- 対応方針: **native-toolkit が develop にマージされ 1.12.0 が出るまで、実装は始めない。** 待つ間に設計を固める
- 進捗: **企画中。** 47 本の対応は確定（4 章）。設計書は未着手

- 対象: `Runtime/Clipboard/Windows*.cs`、`Runtime/Notification/Windows*.cs`、`Runtime/Dialog/WindowsDialogManager.cs`、`Plugins/Windows/`、対応するテストとサンプル
- チケット: 未採番
- 相手側: native-toolkit `artifact/topics/windows-architecture/`（分類: 横断課題（アーキテクチャ））

---

## 1. 何が起きているか

1.x の C ABI は**削除された**。互換レイヤーは無く、2.0.0 は別物である。

| | 1.x | 2.0.0 |
|---|---|---|
| 関数 | 52 | `ntk_*` に改名。**105**（Common 11 / Dialog 6 / Notification 48 / Clipboard 40） |
| 文字列 | `wchar_t*`。呼び出し側のバッファを 2 回呼んで採寸 | UTF-8。不透明ハンドルから借りる |
| 構造化データ | JSON 文字列 | 型付き構造体（`struct_size` で版管理）とリストハンドル + index アクセサ |
| エラー | `DWORD* pError` の出力引数 | 戻り値。OS の生値は `ntk_last_system_code()` |
| 解放 | プロセス横断の状態を持つ free 関数 | ハンドルごとの `_free` |

ヘッダーの宣言は 114 あるが、うち 9 本は関数ではなく**コールバックの typedef**（`ntk_release_fn`、`ntk_notification_invoked_fn`、Clipboard の 7 本）。`105 + 9 = 114`。

**この repo は今のところ壊れていない。** 1.x の DLL を同梱して 1.11.0 として動いているので、急ぐ理由は無い。

## 2. 影響範囲

### 2.1 P/Invoke 宣言

| ファイル | P/Invoke | 行数 | 内訳 |
|---|---|---|---|
| `Runtime/Clipboard/WindowsClipboardManager.cs` | 27 | 3,984 | リネームのみ 4 / 振る舞い変化 23 |
| `Runtime/Notification/WindowsNotificationManager.cs` | 14 | 507 | リネームのみ 6 / 振る舞い変化 8 |
| `Runtime/Dialog/WindowsDialogManager.cs` | 6 | 567 | リネームのみ 0 / 振る舞い変化 6 |

**47 本のうち 37 本で振る舞いが変わる。** 対応は 4 章。

旧 52 関数のうち呼んでいなかった 5 本は `DLog` / `DFLog` / `DFLLog` / `ToWString` / `ConcatWStrings` の Common ヘルパーで、決定 E-6 により対応先なく廃止された。**移植するものは無い。**

### 2.2 JSON 廃止で消えるもの（確定）

ABI から JSON が消えるため、JSON を前提に作った層が丸ごと不要になる。**設計 8.3 と行単位で突き合わせ済み。**

| ファイル | 置き換わる先 |
|---|---|
| `Runtime/Clipboard/WindowsClipboardJsonParser.cs` | 履歴はハンドル + index（`ntk_clipboard_history_count` / `_item_id` / `_item_text` / `_item_content_type_count` / `_item_content_type_at` / `_item_timestamp_unix_ms`）。書式一覧と `pasteFiles` は `ntk_string_list`。履歴の可用性は `int32` フラグ 2 つ |
| `Runtime/Clipboard/WindowsClipboardJsonBuilder.cs` | `copyFiles` は `const char* const*` + 個数。`copyMultipleFormats` はビルダー（`ntk_clipboard_items_create` / `_add_text` / `_add_html` / `_add_bytes` / `_free`） |
| `Runtime/Notification/WindowsNotificationJsonBuilder.cs` | `ntk_notification_content_create` + セッター 22 本 + `_free` |
| `Tests/Runtime/WindowsClipboardJsonParserTests.cs` | 対象が消えるので削除 |
| `Tests/Runtime/WindowsClipboardJsonBuilderTests.cs` | 同上 |

**あわせて消えるもの（当初見落としていた）:**

- **base64 ヘルパー。** 1.x は `copyMultipleFormats` のバイト列を base64 にして JSON へ埋めていた。2.0.0 のビルダーは**生バイトを取る**ので、この経路の base64 も不要になる
- **`getAllNotifications` の「バッファ不足で切り詰め」ケース。** リストハンドル（`ntk_notification_list_count` / `_id_at` / `_tag_at` / `_group_at`）になり、truncation そのものが起こらなくなる

**残る JSON は 1 箇所だけ。** `ntk_notification_activation_raw_arguments` が OS から来た引数文字列をそのまま返す逃げ道として残る。構造化した値は `ntk_notification_activation_value_count` / `_key_at` / `_value_at` で、押されたボタンの引数とすべてのテキスト・コンボ入力を id で束ねて返す。**raw を利用者に公開しないなら、JSON は完全に消せる。**

### 2.3 差し替えるバイナリ

| 今 | 2.0.0 |
|---|---|
| `Plugins/Windows/unity-windows-native-toolkit.dll` | `NativeToolkitC.dll`（配布物は `dist/1.12.0/windows/windows-native-toolkit-capi-2.0.0.dll`） |
| `Plugins/Windows/Microsoft.WindowsAppRuntime.Bootstrap.dll` | **変更なし。** 隣に置く要件も同じ |

x64 のみ。`ntk_version()` が `NTK_VERSION`（`0x020000`）と一致することを起動時に 1 回確かめる。

### 2.4 波及先

サンプルとテストは本トピックの設計対象に含めるが、**サンプルシーンの作り直しが要る場合は別に切り出す**（Q-4）。

- `Runtime/UI/Windows/Clipboard/`（ExampleController・Fixtures・SampleResult）
- `Runtime/UI/Windows/Dialog/`、`Runtime/UI/Windows/Notification/` の ExampleController
- `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs`
- `Tests/Runtime/WindowsClipboard*Tests.cs`（7 本）、`Tests/Runtime/WindowsNotificationTests.cs`
- `Runtime/Dialog/Win32MessageBox.cs`、`Editor/UI/NativeToolkitEditorWindow.cs`

## 3. 宣言の置換では済まないもの

**ここが本トピックの中身である。** 以下はいずれも今のコードに概念自体が無い。

### 3.1 ドメインリロード（最優先）

Unity Editor はネイティブ DLL を下ろさない。DLL の中のセッションとマネージャーはドメインリロードとプレイモードの出入りを越えて生き残る一方、C# の static に置いたハンドルは消え、登録した関数ポインタは下ろされたドメインを指す。

- `AssemblyReloadEvents.beforeAssemblyReload`、`EditorApplication.playModeStateChanged`（`ExitingPlayMode`）、`Application.quitting` で後始末する
- Clipboard: **オーナースレッドから** `ntk_clipboard_session_close` → `ntk_clipboard_session_free`。`BUSY` なら他スレッドの読み書きを終わらせて再試行
- Notification: `ntk_notification_manager_close` → `_free` の後、**すべての登録の `release` を期限付きで待つ**
- **close が成功しないまま `_free` すると、その Editor では以後セッションを作れない**（放棄）
- 活性化コールバックは受け取ったものをキューに積むだけにする。メインスレッドへ同期で戻ると、close を待つメインスレッドと相互に待って止まる

### 3.2 STA / MTA の分離

`ntk_notification_manager_create` は**呼び出しスレッドを MTA にする**。クリップボードのオーナーは STA でメッセージループを回し続ける必要があるため、**同じスレッドを両方のオーナーにできない。** スレッドを分けるか、先に STA で初期化する。

### 3.3 マーシャリングの落とし穴

- **文字列を返す関数に `[return: MarshalAs(UnmanagedType.LPUTF8Str)]` を付けない。** マーシャラーが戻りポインタを `CoTaskMemFree` で解放してヒープを壊す。`IntPtr` で受けて自分で複製する。引数側の `LPUTF8Str` は可
- 借りたポインタ（`ntk_string_data` など）はそのハンドルを `_free` するまで有効。コールバックが受け取ったハンドルは**そのコールバックから戻るまで**
- **登録ごとに `GCHandle` を 1 つ**作り、`release` で `Free` する。同じ `user_data` の値で登録し直すときも別の所有権を渡す。`release` は登録が失敗したときも必ず 1 回呼ばれる（呼び出しスレッドで、戻る前に）
- コールバックは `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`（`NTK_CALL` は `__cdecl`）
- 構造体は 0 で埋め、`struct_size` に **`Marshal.SizeOf<T>()`**（`sizeof` ではない）。`reserved` は 0 のまま
- ハンドルは `SafeHandle` に包み、対応する `_free` で解放する

### 3.4 意味が反転する引数

0 が「既定」になった結果、C++ API に対して**意味が反転している**フラグが 2 つある。移行時に取り違えると静かに壊れる。

- `allow_missing_file`
- `skip_overwrite_prompt`

## 4. 静かに壊れる 4 件

**コンパイルエラーにならない変更。** 型が合ってしまうため、テストで捕まえないと実行時まで抜ける。native-toolkit 側が「Unity への移植で最も刺さる」と名指しした 4 件。

| # | 変更 | 症状 |
|---|---|---|
| S-1 | 通知のタイムスタンプが **Unix 秒 → Unix ミリ秒** | 値は通るが時刻が 1000 倍ずれる |
| S-2 | 履歴のタイムスタンプが **WinRT ticks の十進文字列 → `int64` Unix ミリ秒**（E-14） | 文字列パースを残すと壊れ、型を合わせても基準が違う |
| S-3 | **2 回目の `init` が成功ではなく `NOT_SUPPORTED`** | リロード時に再 init する実装がすべて破綻する。ハンドラ差し替えは `ntk_notification_manager_set_invoked_handler` を使う |
| S-4 | `reserveDeferredFormats` の `release` が `user_data` の解放を持つ | **エラー戻り時に自分で解放すると二重解放。** 失敗しても `release` は必ず呼ばれる |

S-1 / S-2 は EditMode テストで境界値を固定して守る。S-3 / S-4 は 3.1 の後始末と同じ箇所に出るので、設計で一体に扱う。

## 5. 47 本の対応

設計 8.3 より。**リネームのみ 10 本、振る舞いが変わる 37 本。**

### 5.1 リネームのみ（10）

| 旧 | 新 |
|---|---|
| `canDestroyClipboardManager` | `ntk_clipboard_session_can_close` |
| `clearClipboard` | `ntk_clipboard_clear` |
| `getPreferredClipboardFormat` | `ntk_clipboard_get_preferred_format` |
| `recoverDeferredState` | `ntk_clipboard_recover_deferred_state` |
| `cancelScheduledNotification` | `ntk_notification_cancel_scheduled` |
| `removeNotificationById` | `ntk_notification_remove_by_id` |
| `removeNotificationsByTag` | `ntk_notification_remove_by_tag` |
| `removeAllNotifications` | `ntk_notification_remove_all` |
| `openNotificationSettings` | `ntk_notification_open_settings` |
| `setBadge` | `ntk_notification_set_badge` |

### 5.2 Clipboard・振る舞いが変わる（23）

| 旧 | 新 | 変わること |
|---|---|---|
| `initClipboardManager` | `ntk_clipboard_session_create` | 同じスレッドからの 2 回目は成功ではなく `NOT_SUPPORTED`（S-3） |
| `uninitClipboardManager` | `ntk_clipboard_session_close` | `BOOL` + `pError` ではなくエラーのみ。成功後に `_free`。要求が飛んでいてもポンプせずに閉じる（E-19）。再試行は `BUSY` のときだけ |
| `copyPlainText` | `ntk_clipboard_copy_text` | 未知のオプションビットは `INVALID_PARAMETER`。`SENSITIVE` は値 3 のまま |
| `copyHtml` | `ntk_clipboard_copy_html` | 同上 |
| `copyImage` | `ntk_clipboard_copy_dib` | 運ぶもの（DIB）に合わせて改名。未知ビットの扱いは同上 |
| `copyFiles` | `ntk_clipboard_copy_files` | 文字列配列。JSON 配列ではない |
| `copyCustomFormat` | `ntk_clipboard_copy_custom` | 未知ビットの扱いは同上 |
| `copyMultipleFormats` | `ntk_clipboard_copy_multiple` | ビルダーに**生バイト**。base64 入り JSON ではない |
| `pastePlainText` | `ntk_clipboard_paste_text` | 採寸パスが消え、1 回の呼び出しで済む |
| `pasteHtml` | `ntk_clipboard_paste_html` | 同上 |
| `pasteImage` | `ntk_clipboard_paste_dib` | 同上 |
| `pasteFiles` | `ntk_clipboard_paste_files` | リストハンドル。JSON ではない |
| `pasteCustomFormat` | `ntk_clipboard_paste_custom` | 同上（採寸パスなし） |
| `hasClipboardFormat` | `ntk_clipboard_has_format` | 結果は戻り値ではなく出力引数 |
| `getClipboardFormats` | `ntk_clipboard_get_formats` | リストハンドル。JSON ではない |
| `getClipboardHistory` | `ntk_clipboard_get_history` | 完了が履歴ハンドルを運ぶ（**そのコールバックの間だけ有効**）。**タイムスタンプが変わる（S-2）。** `NULL` コールバックは `INVALID_PARAMETER` |
| `getClipboardHistoryAvailability` | `ntk_clipboard_get_history_availability` | 完了が bool 2 つを運ぶ。JSON ではない |
| `deleteHistoryItem` | `ntk_clipboard_delete_history_item` | 同上 |
| `restoreHistoryItem` | `ntk_clipboard_restore_history_item` | 完了がシステムコードを運ぶ |
| `clearUnpinnedHistory` | `ntk_clipboard_clear_unpinned_history` | 同上 |
| `setClipboardHistoryCallbacks` | `ntk_clipboard_set_history_handlers` | 3 つのコールバックが 1 構造体 + `user_data` に。解除は `NULL` か 3 つとも `NULL`。**失敗時は古いハンドラが残り、新しい `user_data` は登録されない**（E-20） |
| `reserveDeferredFormats` | `ntk_clipboard_reserve_deferred` | provider が「採寸してから書く」2 段階ではなく 1 段階に。`void* context` が `user_data` + `release` に。**エラー戻り時に自分で解放しない（S-4）** |
| `cancelClipboardRequest` | `ntk_clipboard_cancel_request` | `BOOL` + `pError` ではなくエラーのみ |

### 5.3 Notification・振る舞いが変わる（8）

| 旧 | 新 | 変わること |
|---|---|---|
| `initWinAppSdk` | `ntk_notification_runtime_initialize` | ハンドルの解放で、全マネージャーが閉じた時点で `MddBootstrapShutdown` を 1 回呼ぶ。**1.x は一度も shutdown していなかった** |
| `initNotificationManager` | `ntk_notification_manager_create` | 2 回目はハンドラ差し替えではなく `NOT_SUPPORTED`（S-3）。差し替えは `ntk_notification_manager_set_invoked_handler`。`release` で `user_data` の終わりが分かる（E-12） |
| `uninitNotificationManager` | `ntk_notification_manager_close` / `_free` | 戻った後に、飛んでいた活性化が 1 件だけ走ることがある。`user_data` の解放は `release` の**後** |
| `showNotification` | `ntk_notification_show` | ビルダー。JSON ではない。`INVALID_PAYLOAD` は起こり得なくなる。**タイムスタンプが秒からミリ秒へ（S-1）。** 範囲外の scenario / audio kind / logo crop は既定値へ落とさず `INVALID_PARAMETER`。ボタンラベルや入力 id の欠落は `HRESULT_FAILURE` ではなく `INVALID_PARAMETER` |
| `scheduleNotification` | `ntk_notification_schedule` | `showNotification` と同じ。予定時刻は Unix ミリ秒のままだが、`system_clock` の範囲（前後およそ 29,000 年）を超えると `INVALID_PARAMETER` |
| `updateNotificationProgress` | `ntk_notification_update_progress` | 引数 6 個が 1 構造体に |
| `getAllNotifications` | `ntk_notification_get_all` | リストハンドル。**切り詰めが起こらなくなる** |
| `getNotificationSetting` | `ntk_notification_get_setting` | `-1` ではなくエラー。閉じたマネージャーは `NOT_INITIALIZED`、`NULL` ハンドルは `INVALID_PARAMETER` |

### 5.4 Dialog・振る舞いが変わる（6）

| 旧 | 新 | 変わること |
|---|---|---|
| `showAlertDialog` | `ntk_dialog_show_alert` | `MB_*` の 4 グループが列挙型になり、他の `MB_*` は渡せない（E-2）。結果は `IDOK` 等ではなく `ntk_dialog_alert_result`。**owner が効くようになった**（E-13） |
| `showFileDialog` | `ntk_dialog_show_open_file` | キャンセルは `*pError == -1` ではなく `CANCELED`。失敗は生の OS 値ではなく `SYSTEM_ERROR` + `ntk_last_system_code()`。フィルタは NUL 区切りの 1 本ではなく name/pattern の配列。**title / `allow_missing_file` / owner が効くようになった**。パス長は終端込み 1024、超過は `SYSTEM_ERROR`（1.x は呼び出し側がバッファを選んでいた） |
| `showMultiFileDialog` | `ntk_dialog_show_open_files` | フォルダ + 名前の並びではなく**フルパス**。フォルダは件数に数えない。全体で 32768 文字 |
| `showFolderDialog` | `ntk_dialog_show_pick_folder` | キャンセルは `TRUE` + `pError == -1` ではなく `CANCELED`。終端込み 1024 |
| `showMultiFolderDialog` | `ntk_dialog_show_pick_folders` | キャンセルは 0 件や `-1` ではなく `CANCELED`。**「成功したが未選択」と「キャンセル」がようやく区別できる**。全体で 32768 |
| `showSaveFileDialog` | `ntk_dialog_show_save_file` | `skip_overwrite_prompt` で上書き確認を止められる（1.x は常に確認した）。**title / owner が効くようになった**。終端込み 1024 |

## 6. 未確認・未決

| # | 内容 | 状態 |
|---|---|---|
| Q-1 | 関数の数 | **解決（2026-09-23）。** 105。`.def` が 105 行で、これがリンカの export。114 は typedef 9 本を含めた数 |
| Q-2 | JSON 依存層の廃止範囲 | **解決（2026-09-23）。** 5 ファイルすべて廃止。base64 ヘルパーと truncation 処理も道連れ。残る JSON は `raw_arguments` のみ（2.2） |
| Q-3 | 47 本の対応と未使用 5 本 | **解決（2026-09-23）。** 5 章。未使用 5 本は E-6 で廃止、移植不要 |
| Q-4 | サンプルシーンの作り直しが要るか。要るなら `design-sample-scene` へ別途切り出す | 未決 |
| Q-5 | NuGet の `NativeToolkit.CApi 2.0.0` を使うか、DLL を直接同梱するか | 未決 |
| Q-6 | チケット番号（`UNT-13` 以降）を採番する | 未決 |

**残る 3 件はこちら側で決めること。** native-toolkit への確認は無い。

## 7. 参照

native-toolkit のパス（このマシン上）。**`feature/NTKIT-16` は develop 未マージ、1.12.0 未リリース**（2026-09-23 時点、HEAD `72d83fce`）。

| 見るもの | 場所 |
|---|---|
| こちらへの申し送り | `artifact/topics/windows-architecture/results/2026-09-23-windows-architecture-stage5-result.md` 2 章 |
| 52 関数の対応表（5 章の出典） | `artifact/topics/windows-architecture/designs/2026-09-21-windows-architecture-c-abi-design.md` 8.3 |
| スレッドと寿命の契約 | 同 1.3（Unity 固有は 1.3.4、STA/MTA は 1.3.3） |
| 受け渡しの 3 方式 | 同 1.2 |
| 21 の決定（E-1〜E-21） | 同 4.2 |
| 公開ヘッダー | `windows/WindowsLibraryCApi/include/NativeToolkitC/{Common,Dialog,Notification,Clipboard}.h` |
| **export の正本** | `windows/WindowsLibraryCApi/WindowsLibraryCApi.def`（105 行） |
| 整合検査 | `python scripts/check_c_abi_contract.py`。`.def`・ヘッダー・設計 8.2・付録 A の 4 つを相互照合する。ツールチェーン不要 |
| 全 105 関数の C 実例 | `manual/1.12.0/{dialog,notification,clipboard}.md` の「C ABI」節。コンパイル検査済み |
| 配布物 | `dist/1.12.0/windows/`（`-capi-` が C ABI。`-capi-` の付かない方は C++ の静的ライブラリで、**こちらではない**） |
