# Windows Dialog 実装計画 v1（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Dialog（Windows） |
| 種別 | **移行。機能追加ではない。** 1.x の C ABI（dist 1.11.0）から 2.0.0（dist 1.12.0）へ P/Invoke 層を書き換える |
| 親課題 | `artifact/topics/windows-c-abi-2/README.md`（3 機能の目次。Dialog はその 5.4） |
| 移行前の基準 | `50fe7bb`（`feature/UNT-12`）。Dialog のサンプルの UI 自動テスト D-01〜D-14 が 1.x で通る |
| 方針 | **Manager の公開 API を変えない**（親課題 6 章 Q-4）。サンプル・UXML は触らない。D-01〜D-14 がそのまま移行の検証になる |
| DLL | `windows-native-toolkit-capi-2.0.0.dll` を dist の名前のまま同梱する（親課題 Q-5）。PreBuildProcessor が `Plugins/Windows/VERSION.txt` のピンに従ってコピーする |

## 1. native-toolkit 側（2.0.0 の C API）

出典: native-toolkit `dist/1.12.0/windows/include/NativeToolkitC/Dialog.h`、`Common.h`。

### 1.1 関数（6）

| # | 関数 | 1.x の対応 | 戻り値・出力 |
|---|---|---|---|
| OP-01 | `ntk_dialog_show_alert(const ntk_dialog_alert_request*, ntk_dialog_alert_result*)` | `showAlertDialog` | `ntk_dialog_error`。押したボタンは `out_result` |
| OP-02 | `ntk_dialog_show_open_file(request, filters, filter_count, ntk_string** out_path)` | `showFileDialog` | パスは `ntk_string`（1023 文字まで） |
| OP-03 | `ntk_dialog_show_open_files(request, filters, filter_count, ntk_string_list** out_paths)` | `showMultiFileDialog` | **各要素がフルパス**（1.x はフォルダ + 名前の並び） |
| OP-04 | `ntk_dialog_show_save_file(const ntk_dialog_save_file_request*, filters, filter_count, ntk_string**)` | `showSaveFileDialog` | ファイルは作らない |
| OP-05 | `ntk_dialog_show_pick_folder(const ntk_dialog_folder_request*, ntk_string**)` | `showFolderDialog` | |
| OP-06 | `ntk_dialog_show_pick_folders(const ntk_dialog_folder_request*, ntk_string_list**)` | `showMultiFolderDialog` | 各要素がフルパス |

共通（`Common.h`）: `ntk_version()`、`ntk_last_system_code()`、`ntk_string_data` / `_size` / `_free`、`ntk_string_list_count` / `_at` / `_free`。
**文字列は入出力とも NUL 終端の UTF-8。** 1.x は UTF-16 と呼び出し側のバッファだった。

### 1.2 型

- `ntk_dialog_error`: `NONE`(0) / `INVALID_PARAMETER`(1) / `CANCELED`(2) / `BUFFER_TOO_SMALL`(3、予約・返らない) / `SYSTEM_ERROR`(4) / `UNKNOWN`(5)。
  `SYSTEM_ERROR` の OS の生の値は、同じスレッドで直後の `ntk_last_system_code()`
- アラート: `buttons`（0〜6）、`icon`（0〜4）、`default_button`（0〜3）、`top_most`、`show_help_button`、`owner`。結果は `ntk_dialog_alert_result`（0〜10）で、**Win32 の `IDOK` 等ではない**
- リクエスト構造体（`#pragma pack(8)`、x64 の大きさ）: `ntk_dialog_alert_request` 56、`ntk_dialog_file_request` 32、`ntk_dialog_save_file_request` 40、`ntk_dialog_folder_request` 24。
  先頭は `struct_size`、`reserved*` は 0。**NULL のリクエストは全項目が既定値**
- `ntk_dialog_filter { const char* name; const char* patterns; }`（`patterns` は `;` 区切り、必須）。`filters` が NULL で `filter_count` 0 なら全ファイル
- どの関数も**モーダル**で、閉じるまで呼び出したスレッドを止める。フォルダ選択は、呼び出したスレッドが未初期化なら COM を STA で初期化する

### 1.3 1.x から変わること（親課題 5.4）

キャンセルは `-1` ではなく `CANCELED`。失敗は OS の生の値ではなく `SYSTEM_ERROR` + `ntk_last_system_code()`。フィルタは NUL 区切りの 1 本ではなく配列。
ファイル系のタイトル・`allow_missing_file`・owner が効く。複数ファイルはフルパス。複数フォルダは「成功したが未選択」とキャンセルが分かれる。
保存は `skip_overwrite_prompt` で上書き確認を止められる。パスは終端込み 1024、一覧は全体で 32768 文字。

## 2. 既存の C# 実装

- `Runtime/Dialog/WindowsDialogManager.cs`: `MonoBehaviour` Singleton（`DontDestroyOnLoad`）。`#if UNITY_STANDALONE_WIN`（`|| UNITY_EDITOR` なし）。
  公開メソッド 6 本は同期で、結果はイベント 6 本で同じ呼び出しの中で返す
- `DLL_NAME` は `DEVELOPMENT_BUILD` で `unity-windows-native-toolkit-debug` / `unity-windows-native-toolkit` を切り替える（dist に debug 版は無く、同じ DLL を 2 つの名前で置いている）
- `Runtime/Dialog/Win32MessageBox.cs`: 公開の `MB_*` 定数。`ShowDialog` の引数はこれを受け取る
- 公開イベントとその値（**移行後もこのまま**）:

| イベント | 引数 | キャンセル | 失敗 |
|---|---|---|---|
| `AlertDialogResult` | `(int? result, bool isSuccess, int? errorCode)`。`result` は Win32 の `IDOK` 等 | Cancel ボタンは `result = IDCANCEL(2)` の成功 | `(result, false, errorCode)` |
| `FileDialogResult` / `SaveFileDialogResult` / `FolderDialogResult` | `(string? path, bool isCancelled, bool isSuccess, int? errorCode)` | `(null, true, true, -1)` | `(null, false, false, errorCode)` |
| `MultiFileDialogResult` / `MultiFolderDialogResult` | `(ArrayList? paths, bool isCancelled, bool isSuccess, int? errorCode)`。要素はフルパス | `(null, true, true, -1)` | `(null, false, false, errorCode)` |

- 既知の不具合: `ShowDialog` はタイトルかメッセージが空のとき、エラーのイベントを出したあと `return` せずネイティブを呼ぶ（`WindowsDialogManager.cs:218-228`）

## 3. 実装制約

- `agent-rules/coding-rules/common.md`: Manager + Bridge、ネイティブが同期なら C# も同期、`Runtime/<Feature>/` のファイルは `Windows` 接頭辞、共通化しない（`Runtime/Common/` を除く）
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、ログのタグ形式、XML ドキュメントコメント
- 1.x と同じく、Dialog は per-call callback を持たない。**移行では追加しない**（公開 API を変えないため）

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

- `DllImport` の名前は `windows-native-toolkit-capi-2.0.0`。`DEVELOPMENT_BUILD` の切り替えは無くす（1 本だけ置く）。呼び出し規約は `Cdecl`（`NTK_CALL` は `__cdecl`）
- 文字列（入力）は `Encoding.UTF8` で NUL 終端のバイト列にして `Marshal.AllocHGlobal` に置き、構造体の `IntPtr` に入れる。`finally` で解放する（common.md「ポインタが必要な配列渡し」）
- 文字列（出力）は `ntk_string*` を `IntPtr` で受け、`ntk_string_data` と `ntk_string_size` からバイト列を写して UTF-8 で読み、`ntk_string_free` で解放する。一覧は `ntk_string_list_count` / `_at` / `_free`
- リクエスト構造体は `[StructLayout(LayoutKind.Sequential)]`、ポインタは `IntPtr`、`struct_size = (uint)Marshal.SizeOf<T>()`
- フィルタは `ntk_dialog_filter` の配列を `AllocHGlobal` に並べて渡す

### 4.1 引数の変換（公開 API は変えない）

| 公開 API の引数 | 2.0.0 へ |
|---|---|
| `ShowDialog` の `buttons`（`MB_OK`〜`MB_CANCELTRYCONTINUE`、0〜6） | `ntk_dialog_alert_buttons`: `MB_OK`→OK、`MB_OKCANCEL`→OK_CANCEL、`MB_ABORTRETRYIGNORE`→ABORT_RETRY_IGNORE、`MB_YESNOCANCEL`→YES_NO_CANCEL、`MB_YESNO`→YES_NO、`MB_RETRYCANCEL`→RETRY_CANCEL、`MB_CANCELTRYCONTINUE`→CANCEL_TRY_CONTINUE |
| `icon`（0、`MB_ICONHAND` 0x10、`MB_ICONQUESTION` 0x20、`MB_ICONEXCLAMATION` 0x30、`MB_ICONASTERISK` 0x40） | NONE / ERROR / QUESTION / WARNING / INFORMATION |
| `defbutton`（`MB_DEFBUTTON1`〜`4`、0x000〜0x300） | FIRST〜FOURTH |
| `options` の `MB_TOPMOST`（0x40000） | `top_most = 1` |
| `options` の `MB_APPLMODAL`（0） | 何もしない（既定） |
| `options` の `MB_SYSTEMMODAL` / `MB_TASKMODAL` / `MB_RIGHT` / `MB_RTLREADING`、ほか未定義のビット | **2.0.0 に対応先が無い（E-2）。5.2 J-2** |
| `filter`（Win32 形式 `"名前\0パターン\0...\0\0"`） | `ntk_dialog_filter[]`。名前とパターンの組に分け、末尾の空要素は捨てる。組にならない（奇数個・パターンが空）なら 5.2 J-4 |
| `buffer_size` | **使わない。** 2.0.0 が大きさを持つ（1 本 1023 文字、一覧は全体 32768）。引数は互換のため残し、XML コメントに「無視する」と書く |
| `ShowFolderDialog` / `ShowMultiFolderDialog` の `title` | `request.title` |
| `ShowSaveFileDialog` の `def_ext` | `request.default_extension` |

1.x の振る舞いに合わせて固定する値: `owner = NULL`（1.x は owner を渡していない）、ファイル系の `title = NULL`（1.x は効かなかった）、`allow_missing_file = 0`、`skip_overwrite_prompt = 0`（1.x は常に確認した）。
2.0.0 で使えるようになったこれらを公開 API に出すのは、移行とは別の課題にする。

### 4.2 結果の変換

| 2.0.0 | イベントへ |
|---|---|
| OP-01 の `NONE` + `ntk_dialog_alert_result` | `result` を Win32 の ID に戻す: OK→1、CANCEL→2、ABORT→3、RETRY→4、IGNORE→5、YES→6、NO→7、CLOSE→8、HELP→9、TRY_AGAIN→10、CONTINUE→11。`(id, true, null)` |
| OP-02〜06 の `NONE` | パス（一覧）を渡す。一覧が 0 件なら空の `ArrayList`（5.2 J-5） |
| `CANCELED` | `(null, true, true, -1)`（1.x と同じ） |
| `SYSTEM_ERROR` | `errorCode = (int)ntk_last_system_code()`。1.x の errorCode も OS の値だった |
| `INVALID_PARAMETER` / `UNKNOWN` / 想定外の値 | 5.2 J-3 |

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | 内容 |
|---|---|---|
| 既存変更 | `Runtime/Dialog/WindowsDialogManager.cs` | 1.x の `DllImport` 6 本と、バッファ・UTF-16 の処理を削除。`WindowsDialogCApi` を呼ぶ。公開メソッド・イベント・既定値はそのまま |
| 新規作成 | `Runtime/Dialog/WindowsDialogCApi.cs` | `internal static`。2.0.0 の `DllImport`（Dialog 6 本 + Common の 8 本）、リクエスト構造体、UTF-8 の入出力、`ntk_string` / `_list` の読み取りと解放 |
| 新規作成 | `Runtime/Dialog/WindowsDialogCApiMapping.cs` | `internal static`。4.1 / 4.2 の変換（`MB_*` と列挙、Win32 形式のフィルタの分解、結果とエラーの変換）。ネイティブを呼ばない純粋な関数だけ |
| 既存変更 | `Editor/Build/PreBuildProcessor.cs`、`Plugins/Windows/VERSION.txt` | 5.2 J-6（移行の間だけ DLL を 2 本置く） |
| 新規作成 | `Tests/Runtime/WindowsDialogCApiMappingTests.cs` | 層 1。変換と構造体の大きさ |
| 既存変更 | `Tests/PlayMode/WindowsDialogSamplePlayerTests.cs` | 層 2b。D-01〜D-14 は期待値を変えない。5.2 J-1 / J-2 のテストを足す |
| 非変更 | `Runtime/Dialog/Win32MessageBox.cs`、`Runtime/UI/Windows/Dialog/*`、`Runtime/Resources/UI/Windows/Dialog/*` | 公開 API とサンプルは変えない |

コンパイルガード: `WindowsDialogManager.cs` と `WindowsDialogCApi.cs` は今と同じ `#if UNITY_STANDALONE_WIN`。
`WindowsDialogCApiMapping.cs` はネイティブを呼ばないので `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` とし、層 1 で Editor から確かめられるようにする（テストも同じガード）。

### 5.2 判断が要る点（推奨つき）

| # | 内容 | 推奨 |
|---|---|---|
| J-1 | `ShowDialog` のタイトルかメッセージが空のとき、エラーを出したあとネイティブを呼ぶ不具合 | **直す。** エラーのイベントを 1 回出して `return` する。2.0.0 は NULL を `""` として受けるので、直さないと空のダイアログが出る。層 2b で「ダイアログを出さずにエラーが 1 回」を確かめる |
| J-2 | 2.0.0 に対応先の無い `MB_*`（`MB_SYSTEMMODAL`、`MB_TASKMODAL`、`MB_RIGHT`、`MB_RTLREADING`、未定義のビット） | **ダイアログを出さずにエラーにする**（`AlertDialogResult(null, false, J-3 の値)` とエラーログ）。黙って捨てると、1.x で効いていた指定が見た目だけ変わる。`Win32MessageBox` の定数は消さない（公開 API のため）。XML コメントに「2.0.0 では使えない」と書く |
| J-3 | `INVALID_PARAMETER` / `UNKNOWN` を `int? errorCode` にどう出すか | **負の値の公開定数を足す**: `-1` キャンセル（既存）、`-2` 引数の誤り（J-2・J-4 を含む）、`-3` 不明、`-4` DLL の版違い（J-7）。OS の値（`SYSTEM_ERROR`）は正、ライブラリの判断は負、と分かれる。定数は新しい公開の静的クラス `WindowsDialogErrorCodes`（`Runtime/Dialog/WindowsDialogErrorCodes.cs`）に置く。追加だけなので既存の呼び出し側は壊れない |
| J-4 | Win32 形式のフィルタが組にならない（奇数個、パターンが空） | **ネイティブを呼ばずに `-2`。** 1.x はそのまま OS に渡していた。サンプルと既定値（`"All Files\0*.*\0\0"`）は正しい形 |
| J-5 | 複数フォルダ・複数ファイルで「成功したが 0 件」 | **空の `ArrayList` で `(list, false, true, null)`。** 1.x では起きず、2.0.0 で区別できるようになった。キャンセルとは混ぜない |
| J-6 | 移行の途中で、1.x（Clipboard / Notification がまだ使う）と 2.0.0（Dialog）の DLL を同時に置けるか | **移行の間だけ 2 本置けるようにする。** `VERSION.txt` に `extra_dist_version` / `extra_dll` を足し、PreBuildProcessor が両方をコピーする。関数名が違うので衝突しない。こうすると機能ごとに移し、そのたびに Player テストで確かめられる。3 機能が揃ったら消す。**2 本置かない場合、3 機能すべてを書き換えるまで Player で何も確かめられない** |
| J-7 | DLL と C# の版が合っているか | **最初の呼び出しで `ntk_version()` の上位（major）が 2 かを 1 回確かめる。** 違えば以後の呼び出しをネイティブへ渡さず `-4` とエラーログ。`DllNotFoundException` / `EntryPointNotFoundException` も `-4` にまとめる |

### 5.3 呼び出しの流れ（OP-02 の例）

1. `ShowFileDialog(buffer_size, filter)`（`buffer_size` は使わない）
2. J-7 の版確認。J-4 でフィルタを分解（誤りなら `FileDialogResult(null, false, false, -2)` で終わり）
3. UTF-8 に変換して `AllocHGlobal`、`ntk_dialog_file_request { struct_size, title = 0, allow_missing_file = 0, owner = 0 }` を作る
4. `ntk_dialog_show_open_file(ref request, filters, count, out IntPtr path)`。**ダイアログが閉じるまで戻らない**
5. 結果を 4.2 で変換してイベントを 1 回出す。`SYSTEM_ERROR` なら、ほかの呼び出しを挟まずに `ntk_last_system_code()` を読む
6. `finally` で `ntk_string_free(path)` と `FreeHGlobal`

### 5.4 契約

- スレッド: 同期。呼び出したスレッド（通常はメインスレッド）で、閉じるまで止まる。イベントは同じ呼び出しの中で、そのスレッドで出る（1.x と同じ）
- メモリ: ネイティブから受けたハンドルはすべて `finally` で `_free`。こちらが確保した入力は `finally` で `FreeHGlobal`
- エラー: 例外を外に出さない。`DllImport` 由来の例外も捕まえてイベントのエラーにする（J-7）
- ドメインリロード: Dialog はコールバックもハンドルも持ち越さないので、対処は要らない（親課題 README の「ドメインリロード」の対象外）

### 5.5 実装順

1. J-6（2 本置き）を先に入れ、1.x のままビルドとテストが通ることを確かめる
2. `WindowsDialogCApiMapping` と層 1 のテスト
3. `WindowsDialogCApi`
4. `WindowsDialogManager` の置き換え（J-1〜J-7 を含む）
5. 層 2b: D-01〜D-14 が期待値を変えずに通ること、J-1 / J-2 の追加テスト

## 6. エラーケース一覧

| # | 状況 | 層 | 返り方 |
|---|---|---|---|
| E-1 | ユーザーがキャンセル（OP-02〜06） | ネイティブ | `(null, true, true, -1)` |
| E-2 | アラートで Cancel を押す（OP-01） | ネイティブ | 成功。`result = 2`（IDCANCEL） |
| E-3 | OS が失敗を返す | ネイティブ | `SYSTEM_ERROR` → `errorCode = ntk_last_system_code()`（正） |
| E-4 | ネイティブが引数を拒む | ネイティブ | `INVALID_PARAMETER` → `-2` |
| E-5 | ネイティブの想定外 | ネイティブ | `UNKNOWN` や想定外の値 → `-3` |
| E-6 | タイトルかメッセージが空（OP-01） | C# | ネイティブを呼ばず `AlertDialogResult(null, false, -2)`（J-1） |
| E-7 | 2.0.0 に無い `MB_*` | C# | ネイティブを呼ばず `AlertDialogResult(null, false, -2)`（J-2） |
| E-8 | Win32 形式のフィルタが組にならない | C# | ネイティブを呼ばず各イベントに `-2`（J-4） |
| E-9 | DLL が無い・関数が無い・major が 2 でない | C# | `-4`（J-7） |
| E-10 | 複数選択で 0 件 | ネイティブ | 空の一覧で成功（J-5） |

## 7. テスト方針

| 層 | 対象 | 内容 |
|---|---|---|
| 1（EditMode） | `WindowsDialogCApiMappingTests` | `MB_*` の各値の変換と J-2 の拒否、結果の Win32 ID への逆変換（11 値）、Win32 形式のフィルタの分解と J-4、エラーの変換（J-3）、構造体の大きさ（56 / 32 / 40 / 24） |
| 2a（PlayMode・Editor） | なし | Manager は `UNITY_EDITOR` でコンパイルされない（B 群）。Editor ではネイティブに届かない |
| 2b（PlayMode・Player） | `WindowsDialogSamplePlayerTests` | D-01〜D-14 を**期待値を変えずに**通す（これが移行の検証）。追加: J-1（空のタイトルでダイアログが出ずエラーが 1 回）、J-2（`MB_SYSTEMMODAL` でダイアログが出ずエラー） |
| 3（OS 境界） | D-08 / D-14 | 保存でファイルが作られない・既存が書き換わらないことを、テストがファイルを直接見る（既存） |
| 手動 | なし | タイトルの表示言語・アイコンの見た目は自動化しない（UI テスト計画 1 章と同じ理由） |

## 8. 自動化の前提

### 8.1 検証の層

7 章の表のとおり。**手動に残す項目は無い。** 見た目（アイコン、既定ボタンの位置）は確かめない（`2026-09-26-windows-dialog-ui-test-plan-v1.md` 1 章）。

### 8.2 OS が出す画面・許可・前提の OS 設定

- テスト用 Player のファイアウォールの規則（受信ブロック、既存。`artifact/topics/cross-platform-testing/README.md`）
- 前提の OS 設定: なし
- OS が求める許可: なし

### 8.3 呼び出し側を止める OS の画面

**6 関数すべてがモーダル**で、閉じるまで呼び出し側（メインスレッド）を止める。
現在のハーネスには外から閉じる手段がある: `WindowsDialogSamplePlayerTests` が押す前に PowerShell の閉じる役を起動し、
Win32 のメッセージ（`WM_COMMAND`、`WM_SETTEXT`、`TDM_CLICK_BUTTON`）と、ファイル一覧の選択に UI Automation を使う
（`2026-09-26-windows-dialog-ui-test-plan-v1.md` 2 章）。
J-1 / J-2 の追加テストは「ダイアログが出ない」ことを確かめるが、出てしまったときに実行が止まらないよう、閉じる役を起動しておき、
`no dialog` で終わることを確かめる。

**2.0.0 で owner を渡すようになった場合**（本計画では渡さない）、ダイアログは Unity のウィンドウに所有されるが、トップレベルの `#32770` のままなので、閉じる役の探し方は変わらない。
