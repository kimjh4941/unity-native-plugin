# Windows Dialog 実装計画 v2（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Dialog（Windows） |
| 種別 | **移行。機能追加ではない。** 1.x の C ABI（dist 1.11.0）から 2.0.0（dist 1.12.0）へ P/Invoke 層を書き換える |
| 親課題 | `artifact/topics/windows-c-abi-2/README.md`（3 機能の目次。Dialog は「Dialog・振る舞いが変わる」） |
| 移行前の基準 | `50fe7bb`（`feature/UNT-12`）。Dialog のサンプルの UI 自動テスト D-01〜D-14 が 1.x で通る |
| 方針 | **Manager の公開 API（メソッド 6 本、イベント 6 本、引数、値の意味）を変えない**（親課題 Q-4）。サンプル・UXML は触らない。D-01〜D-14 がそのまま移行の検証になる。変わる振る舞いは 5.3「既知の差分」に列挙したものだけ |
| DLL | `windows-native-toolkit-capi-2.0.0.dll` を dist の名前のまま同梱し、リポジトリにコミットする（親課題 Q-5） |
| レビュー | v1 のレビュー: `artifact/features/dialog/reviews/2026-09-27-windows-dialog-design-review-v1.md` |

## v1 からの主な変更

レビュー v1 の指摘を反映した。

- A1: `MB_*` を 4 引数の OR として読み、`MB_HELP` を通す（4.1）。J-3 の「OS の値は正」を撤回し、予約値との比較で判定する契約にした（J-3）。二重ガードにした（5.1）。例外を捕まえる範囲をネイティブ呼び出しと変換に限り、イベントは 1 回だけ出す（5.5）。`buffer_size` の扱いを J-8 として決め、既知の差分を 5.3 にまとめた。同梱する DLL と `WindowsDialogErrorCodes.cs` を変更一覧に載せた
- A2: 構造体・列挙・定数・変換を Editor でもコンパイルされる側に置いた。J-6 に PreBuildProcessor の実際の処理を書いた。J-7 の major を `(ntk_version() >> 16) == 2` と書いた
- B: `DllImport` の署名と、全構造体のフィールドの位置を 4.1 に書いた（層 1 で `Marshal.OffsetOf` を確かめる）。非 ASCII の往復、`def_ext`、`allow_missing_file`、エラーケースごとの検証の層、層 2a、J-1 / J-2 の層 2b のハーネスを 7 章に書いた。IL2CPP の扱いを J-10 とした
- 開発者の判断（2026-09-27）: J-8（`buffer_size` は意図した差分として受け入れる）、J-9（アラートがネイティブで失敗したら `result = 0`）、J-10（IL2CPP は 3 機能を移したあとに 1 回流す）

## 1. native-toolkit 側（2.0.0 の C API）

出典: native-toolkit `dist/1.12.0/windows/include/NativeToolkitC/Dialog.h`、`Common.h`。
実装: `windows/WindowsLibraryCApi/src/Dialog/DialogCApi.cpp`、`windows/WindowsLibrary/src/Dialog/`。

### 1.1 関数（6）

| # | 関数 | 1.x の対応 | 出力 |
|---|---|---|---|
| OP-01 | `ntk_dialog_show_alert(const ntk_dialog_alert_request*, ntk_dialog_alert_result*)` | `showAlertDialog` | 押したボタン（成功時だけ書かれる） |
| OP-02 | `ntk_dialog_show_open_file(request, filters, filter_count, ntk_string** out_path)` | `showFileDialog` | パス（1023 文字まで） |
| OP-03 | `ntk_dialog_show_open_files(request, filters, filter_count, ntk_string_list** out_paths)` | `showMultiFileDialog` | 各要素がフルパス |
| OP-04 | `ntk_dialog_show_save_file(const ntk_dialog_save_file_request*, filters, filter_count, ntk_string**)` | `showSaveFileDialog` | パス。ファイルは作らない |
| OP-05 | `ntk_dialog_show_pick_folder(const ntk_dialog_folder_request*, ntk_string**)` | `showFolderDialog` | パス |
| OP-06 | `ntk_dialog_show_pick_folders(const ntk_dialog_folder_request*, ntk_string_list**)` | `showMultiFolderDialog` | 各要素がフルパス |

共通（`Common.h`）: `ntk_version()`、`ntk_last_system_code()`、`ntk_string_data` / `_size` / `_free`、`ntk_string_list_count` / `_at` / `_free`。
文字列は入出力とも NUL 終端の UTF-8。1.x は UTF-16 と呼び出し側のバッファだった。
`ntk_string_list_at` が返すポインタは**一覧の中を指す借用**で、一覧を解放するまでだけ有効。個別には解放しない。

**2.0.0 のダイアログは 1.x と同じ Win32 の呼び出しを、同じフラグで行う。** Data 層は 1.x の `WindowsDialogManager.cpp` を移したもの
（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsDialogManager.cpp` と `windows/WindowsLibrary/src/Dialog/Data/WindowsDialogWin32.h`）。
本計画の固定値（4.2）でそこに到達するので、D-01〜D-14 が頼るコントロール ID（1148 / 1001 / 1152）、上書き確認の Task Dialog、
フォルダ選択の `IFileOpenDialog` の振る舞い、カレントディレクトリの移動は変わらない。**ただしコントロール ID は OS が決めるもので、実装時に D-01〜D-14 を流して確かめる。**

### 1.2 型と値

- `ntk_dialog_error`: `NONE`(0) / `INVALID_PARAMETER`(1) / `CANCELED`(2) / `BUFFER_TOO_SMALL`(3、予約・返らない) / `SYSTEM_ERROR`(4) / `UNKNOWN`(5)
- `ntk_last_system_code()` は `uint32_t`。`SYSTEM_ERROR` のとき OS の生の値（CommDlgExtendedError、HRESULT、GetLastError のどれか。**HRESULT は上位ビットが立つので、`int` にすると負になる**）。
  C ABI がメモリ不足を捕まえたときは `UNKNOWN` と `0x8007000E`。`thread_local` で、同じスレッドで直後に読む
- ネイティブが `INVALID_PARAMETER` を返すのは、本計画の使い方では C# 側の構造体の誤り（`struct_size` の不一致、`reserved*` が 0 でない、フィルタのパターンが NULL）のとき
- アラートの列挙: `buttons`（OK=0、OK_CANCEL=1、YES_NO=2、YES_NO_CANCEL=3、RETRY_CANCEL=4、ABORT_RETRY_IGNORE=5、CANCEL_TRY_CONTINUE=6）、
  `icon`（NONE=0、INFORMATION=1、WARNING=2、ERROR=3、QUESTION=4）、`default_button`（FIRST=0〜FOURTH=3）、
  結果（OK=0、CANCEL=1、YES=2、NO=3、RETRY=4、ABORT=5、IGNORE=6、TRY_AGAIN=7、CONTINUE=8、CLOSE=9、HELP=10）。**Win32 の `MB_*` / `IDOK` とは番号が違う**
- `ntk_dialog_filter { const char* name; const char* patterns; }`。`patterns` は `;` 区切りで必須。`filters` が NULL で件数 0 なら全ファイル
- どの関数もモーダルで、閉じるまで呼び出したスレッドを止める。フォルダ選択は、呼び出したスレッドが未初期化なら COM を STA で初期化する

### 1.3 1.x から変わること（親課題 README の「Dialog・振る舞いが変わる」）

キャンセルが `-1` ではなく `CANCELED`。フィルタが配列。ファイル系のタイトル・`allow_missing_file`・owner が効く。複数ファイルがフルパス。
保存の上書き確認を止められる。パスは終端込み 1024、一覧は全体 32768 文字。本計画でこれらを公開 API にどう映すかは 4 章と 5.3。

## 2. 既存の C# 実装

- `Runtime/Dialog/WindowsDialogManager.cs`: `MonoBehaviour` Singleton（`DontDestroyOnLoad`）。今は `#if UNITY_STANDALONE_WIN`（Editor では Windows ターゲットのときだけコンパイルされる）。
  公開メソッド 6 本は同期で、結果はイベント 6 本で同じ呼び出しの中で返す。try/catch は無く、購読者の例外は呼び出し側へ伝わる
- `DLL_NAME` は `DEVELOPMENT_BUILD` で `unity-windows-native-toolkit-debug` / `unity-windows-native-toolkit` を切り替える
- `Runtime/Dialog/Win32MessageBox.cs`: 公開の `MB_*` 定数（`MB_HELP` は無い）
- 公開イベント（**移行後もこのまま**）:

| イベント | 引数 | キャンセル | 失敗（1.x） |
|---|---|---|---|
| `AlertDialogResult` | `(int? result, bool isSuccess, int? errorCode)`。`result` は Win32 の `IDOK` 等 | Cancel ボタンは `result = IDCANCEL(2)` の成功 | `(0, false, errorCode)`（`MessageBoxW` の戻り値 0） |
| `FileDialogResult` / `SaveFileDialogResult` / `FolderDialogResult` | `(string? path, bool isCancelled, bool isSuccess, int? errorCode)` | `(null, true, true, -1)` | `(null, false, false, errorCode)` |
| `MultiFileDialogResult` / `MultiFolderDialogResult` | `(ArrayList? paths, bool isCancelled, bool isSuccess, int? errorCode)`。要素はフルパス | `(null, true, true, -1)` | `(null, false, false, errorCode)` |

- 既知の不具合: `ShowDialog` はタイトルかメッセージが空のとき、`(null, false, null)` を出したあと `return` せずネイティブを呼ぶ（`WindowsDialogManager.cs:218-228`）。
  `MessageBoxW` は空の文字列を受けるので、1.x でも空のダイアログが出て、イベントが 2 回出ていた
- 1.x の C# は、複数選択で成功・0 件のとき空の `ArrayList` を返しうる（`WindowsDialogManager.cs:365-371`、`488-497`）

## 3. 実装制約

- `agent-rules/coding-rules/common.md`: Manager + Bridge、ネイティブが同期なら C# も同期、`Windows` 接頭辞、共通化しない
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、ログのタグ形式、XML ドキュメントコメント
- コンパイルガードは二重にする（review-document の P5、前例は `Runtime/Clipboard/WindowsClipboardManager.cs:5,823`）: 型は `UNITY_STANDALONE_WIN || UNITY_EDITOR`、`DllImport` と実際の呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR`
- Dialog は per-call callback を持たない。移行では追加しない（公開 API を変えない）

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

### 4.1 `DllImport` の署名と構造体

- `DllImport("windows-native-toolkit-capi-2.0.0.dll", CallingConvention = CallingConvention.Cdecl)`。**名前は `.dll` まで書く**（名前に `.` を含むため、拡張子を補ってもらえる前提に立たない）。`DEVELOPMENT_BUILD` の切り替えは無くす
- 型の対応: ポインタと `ntk_string*` / `ntk_string_list*` は `IntPtr`、`size_t` はすべて `UIntPtr`、`int32_t` の列挙・真偽値は `int`、`uint32_t` は `uint`、戻り値の `ntk_dialog_error` は `int`
- 構造体は `[StructLayout(LayoutKind.Sequential, Pack = 8)]`、ポインタは `IntPtr`。`struct_size = (uint)Marshal.SizeOf<T>()`、`reserved*` は 0

| 構造体 | 大きさ | フィールドの位置（native-toolkit `WindowsLibraryCApiTest/Common/CApiLayoutTest.cpp:23-55`） |
|---|---|---|
| `ntk_dialog_filter` | 16 | name 0、patterns 8 |
| `ntk_dialog_alert_request` | 56 | struct_size 0、reserved0 4、title 8、message 16、buttons 24、icon 28、default_button 32、top_most 36、show_help_button 40、reserved1 44、owner 48 |
| `ntk_dialog_file_request` | 32 | struct_size 0、reserved0 4、title 8、allow_missing_file 16、reserved1 20、owner 24 |
| `ntk_dialog_save_file_request` | 40 | struct_size 0、reserved0 4、title 8、default_extension 16、skip_overwrite_prompt 24、reserved1 28、owner 32 |
| `ntk_dialog_folder_request` | 24 | struct_size 0、reserved0 4、title 8、owner 16 |

- 入力の文字列: `Encoding.UTF8` で NUL 終端のバイト列にし、`Marshal.AllocHGlobal` に置く。確保したポインタはすべて 1 つの一覧で追い、途中で失敗しても `finally` で全部解放する。フィルタ配列も同じ
- 出力の文字列: `ntk_string_data` と `ntk_string_size`（バイト数）でバイト列を写し、UTF-8 で読む。一覧は `_count` と `_at`（`out_size` を使う）で**一覧を解放する前に**全要素を managed の文字列へ写し、最後に `ntk_string_list_free` を 1 回だけ呼ぶ
- ハンドルは `SafeHandle` にしない。呼び出しは同期で、受け取ったその場で写して `finally` で解放するため（親課題 README の「マーシャリングの落とし穴」は非同期の API を念頭に置いたもの）
- コールバックは無いので、`MonoPInvokeCallback` は要らない

### 4.2 引数の変換（公開 API は変えない）

**アラートのフラグは、1.x と同じく 4 引数の OR として読む。** 1.x のネイティブは `buttons | icon | defbutton | options` を 1 つにして `MessageBoxW` に渡していた
（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsDialogManager.cpp:63-64`）ので、どの引数に入れたフラグも効いた。

1. null の引数を今の既定値（`MB_OK`、`MB_ICONINFORMATION`、`MB_DEFBUTTON1`、`MB_APPLMODAL`）に置き換え、4 つを OR する
2. マスクで分ける: `0x0000000F` ボタン、`0x000000F0` アイコン、`0x00000F00` 既定ボタン、`MB_TOPMOST`（0x40000）、`MB_HELP`（0x4000）
3. 残ったビットがあれば J-2

| 分けた値 | 2.0.0 へ |
|---|---|
| ボタン 0〜6（`MB_OK`〜`MB_CANCELTRYCONTINUE`） | OK、OK_CANCEL、ABORT_RETRY_IGNORE、YES_NO_CANCEL、YES_NO、RETRY_CANCEL、CANCEL_TRY_CONTINUE（**名前で対応させる。番号は一致しない**。例: `MB_ABORTRETRYIGNORE`=2 → ABORT_RETRY_IGNORE=5）。7〜15 は J-2 |
| アイコン 0 / 0x10 / 0x20 / 0x30 / 0x40 | NONE / ERROR / QUESTION / WARNING / INFORMATION。0x50〜0xF0 は J-2 |
| 既定ボタン 0x000 / 0x100 / 0x200 / 0x300 | FIRST / SECOND / THIRD / FOURTH。0x400〜0xF00 は J-2 |
| `MB_TOPMOST` | `top_most = 1` |
| `MB_HELP`（0x4000） | `show_help_button = 1`。ヘルプボタンは閉じずに `WM_HELP` を送るだけなので、結果の HELP は返らない |
| `MB_APPLMODAL`（0） | 何もしない（既定） |
| `MB_SYSTEMMODAL`（0x1000）/ `MB_TASKMODAL`（0x2000）/ `MB_RIGHT`（0x80000）/ `MB_RTLREADING`（0x100000）/ ほかのビット | 2.0.0 に対応先が無い（native-toolkit の決定 NTK E-2）。J-2 |

`MB_HELP` は `Win32MessageBox` に定数が無いが、1.x では `0x4000` を渡すと効いていたので通す。定数は足さない（公開 API を増やさない）。

| ファイル・フォルダの引数 | 2.0.0 へ |
|---|---|
| `filter`（Win32 形式 `"名前\0パターン\0...\0\0"`） | `ntk_dialog_filter[]`。名前とパターンの組に分け、末尾の空要素は捨てる。組にならない（奇数個、パターンが空）なら J-4。`""` と null は既定値（全ファイル 1 組）と同じ扱い（5.3） |
| `buffer_size` | **使わない**（J-8） |
| `ShowFolderDialog` / `ShowMultiFolderDialog` の `title` | `request.title` |
| `ShowSaveFileDialog` の `def_ext` | `request.default_extension` |

1.x の振る舞いに合わせて固定する値: `owner = NULL`、ファイル系の `title = NULL`、`allow_missing_file = 0`、`skip_overwrite_prompt = 0`。
リクエスト構造体を組み立てる処理は、ネイティブを呼ばない純粋な関数にし（5.1 の `WindowsDialogCApiMapping`）、層 1 でこれらの値を固定する。

### 4.3 結果の変換

| 2.0.0 | イベントへ |
|---|---|
| OP-01 の `NONE` | `result` を名前で Win32 の ID に戻す: OK→1、CANCEL→2、ABORT→3、RETRY→4、IGNORE→5、YES→6、NO→7、CLOSE→8、HELP→9、TRY_AGAIN→10、CONTINUE→11。`(id, true, null)` |
| OP-01 のそれ以外 | `(0, false, errorCode)`（J-9。`out_result` は読まない） |
| OP-02〜06 の `NONE` | パス（一覧）。0 件なら空の `ArrayList` で `(list, false, true, null)`（J-5） |
| `CANCELED` | `(null, true, true, -1)` |
| `SYSTEM_ERROR` | `errorCode = unchecked((int)ntk_last_system_code())`。1.x と同じ種類の値。負にもなる |
| `INVALID_PARAMETER` | `-2`。ログに「C# 側の構造体の誤りの可能性」と system code を出す |
| `UNKNOWN`、想定外の値 | `-3`。ログに system code を出す（メモリ不足は `0x8007000E`） |

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | ガード | 内容 |
|---|---|---|---|
| 既存変更 | `Runtime/Dialog/WindowsDialogManager.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`（今は `UNITY_STANDALONE_WIN`） | 1.x の `DllImport` と、バッファ・UTF-16 の処理を削除。`WindowsDialogCApi` を呼ぶ。公開メソッド・イベント・既定値はそのまま |
| 新規作成 | `Runtime/Dialog/WindowsDialogCApi.cs` | 型は `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`、`DllImport` と呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR` | `internal static`。4.1 の構造体、`DllImport`（Dialog 6 本 + Common 8 本）、UTF-8 の入出力、`ntk_string` / `_list` の読み取りと解放。Editor では呼び出しがネイティブへ行かず、「使えない」を返す |
| 新規作成 | `Runtime/Dialog/WindowsDialogCApiMapping.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | `internal static`。4.2 / 4.3 の変換（フラグの分解、フィルタの分解、リクエスト構造体の組み立て、結果とエラーの変換、J-7 の版判定）。ネイティブを呼ばない |
| 新規作成 | `Runtime/Dialog/WindowsDialogErrorCodes.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | 公開の静的クラス。J-3 の定数。`int?` の errorCode に入れる値なので enum ではなく `int` の定数（`WindowsClipboardErrorCode` は enum で、形が違う） |
| 新規作成 | `Plugins/Windows/windows-native-toolkit-capi-2.0.0.dll` | — | dist 1.12.0 から。コミットする。importer は Win64 のみ・Editor 無効 |
| 既存変更 | `Editor/Build/PreBuildProcessor.cs`、`Plugins/Windows/VERSION.txt` | — | J-6 |
| 新規作成 | `Tests/Runtime/WindowsDialogCApiMappingTests.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | 層 1 |
| 新規作成 | `Tests/PlayMode/WindowsDialogManagerEditorTests.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` かつ Editor だけ | 層 2a（7.2） |
| 既存変更 | `Tests/PlayMode/WindowsDialogSamplePlayerTests.cs` | 今のまま | 層 2b。D-01〜D-14 は期待値を変えない。7.3 の追加 |
| 既存変更 | `artifact/features/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v1.md` の 4 章、`artifact/topics/windows-c-abi-2/README.md`（「差し替えるバイナリ」「移行時にやること」「マーシャリングの落とし穴」「Dialog・振る舞いが変わる」） | — | 本計画に合わせる（UI テストの期待値は変えない、版の確認は major、ハンドルは `SafeHandle` にしない理由、J-6 の手順） |
| 非変更 | `Runtime/Dialog/Win32MessageBox.cs`、`Runtime/UI/Windows/Dialog/*`、リポジトリの `Assets/Scripts/Runtime/UI/Windows/Dialog/WindowsDialogManagerTest.cs`、`Runtime/Resources/UI/Windows/Dialog/*`、`Editor/UI/NativeToolkitEditorWindow.cs` | — | 公開 API とサンプルは変えない。Editor ウィンドウが参照するのはサンプルのコントローラーだけ |

Manager を `|| UNITY_EDITOR` にするのは、層 2a で J-1 / J-2 / J-4 / J-7 をダイアログなしで確かめるため。
Player に他 OS のコードは入らない（型のガードは Windows と Editor だけ）。サンプルのコントローラーは今も `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` で Manager を呼ぶので影響しない。

### 5.2 判断（開発者の承認済み、2026-09-27）

| # | 内容 | 決定 |
|---|---|---|
| J-1 | `ShowDialog` のタイトルかメッセージが空 | ネイティブを呼ばず `AlertDialogResult(null, false, -2)` を 1 回出して終わる。1.x は `(null, false, null)` を出したあと空のダイアログを出していた（errorCode も null から -2 に変わる） |
| J-2 | 2.0.0 に対応先の無いフラグ | ダイアログを出さずに `AlertDialogResult(null, false, -2)` とエラーログ。`Win32MessageBox` の定数は消さず、XML コメントに「2.0.0 では使えない」と書く |
| J-3 | `INVALID_PARAMETER` / `UNKNOWN` の出し方 | `WindowsDialogErrorCodes` に予約値を置く: `Cancelled = -1`（既存）、`InvalidArgument = -2`（J-1 / J-2 / J-4 / ネイティブの `INVALID_PARAMETER`）、`Unknown = -3`、`NativeUnavailable = -4`（J-7）。**それ以外は OS の生の値で、HRESULT なら負にもなる。判定は符号ではなく定数との比較で行う**（XML コメントにこう書く）。-1〜-4 は OS が返さない（`0xFFFFFFFF` はネイティブがキャンセルとして先に除き、`0xFFFFFFFC`〜`0xFFFFFFFE` は定義された HRESULT ではない） |
| J-4 | Win32 形式のフィルタが組にならない | ネイティブを呼ばずに `-2` |
| J-5 | 複数選択で成功・0 件 | 空の `ArrayList` で `(list, false, true, null)`。1.x も同じ形を返しえた。キャンセルとは混ぜない |
| J-6 | 移行の途中で 1.x と 2.0.0 の DLL を 2 本置くか | 置く。5.4 |
| J-7 | DLL と C# の版 | 最初の呼び出しで `(ntk_version() >> 16) == 2` を 1 回確かめる。違えば以後ネイティブへ渡さず `-4` とエラーログ。`DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` も `-4`。Editor（`!UNITY_EDITOR` の外）も `-4` |
| J-8 | `buffer_size` | **使わない。意図した差分として受け入れる**（5.3）。引数は互換のため残し、XML コメントに「2.0.0 では無視する。上限は 1 本 1023 文字、一覧は全体 32768 文字」と書く。1.x の上限を C# で再現しても、1024 文字を超える長いパスは 2.0.0 が返さないので完全にはならない |
| J-9 | アラートがネイティブで失敗したときの `result` | `0`（1.x の `MessageBoxW` の戻り値と同じ） |
| J-10 | IL2CPP | Windows の Player テストは既定の Mono で走る（IL2CPP は Android だけ）。**3 機能を移したあとに、IL2CPP の Player で 1 回流す。** それまで層 2b の結果は Mono での保証 |

### 5.3 既知の差分（意図したもの）

公開 API は変えないが、次の振る舞いは変わる。XML コメントに書き、次の版のマニュアル（`manual/<次の版>/dialog.md`）へ write-manual で引き継ぐ。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| `buffer_size`（J-8） | 渡した大きさがバッファになり、超えると失敗。大きく渡せば長いパスも受けた | 無視。1 本 1023 文字、一覧は全体 32768 文字を超えると `SYSTEM_ERROR` |
| 空のタイトル・メッセージ（J-1） | `(null, false, null)` と空のダイアログ（イベント 2 回） | ダイアログを出さず `(null, false, -2)` を 1 回 |
| 2.0.0 に無いフラグ（J-2） | `MessageBoxW` に渡って効いた | ダイアログを出さず `-2` |
| 組にならないフィルタ（J-4） | そのまま OS に渡した | `-2` |
| 複数ファイルでドライブ直下を選ぶ | `C:\\a.txt`（区切りが重なる） | `C:\a.txt` |
| フォルダの `title` に `""` | 空の題名 | システムの題名 |
| `filter` に `""` | 空のフィルタ | 全ファイル 1 組（null と同じ） |
| 対になっていないサロゲートを含むパス | そのまま | U+FFFD に置き換わる |
| 新しい errorCode（J-3） | -1 と OS の値 | -2 / -3 / -4 が加わる |

### 5.4 J-6: 移行の間だけ DLL を 2 本置く

- `VERSION.txt` にキーを足す: `extra_dist_version`、`extra_dll`、`extra_install_as`、`extra_install_as_debug`（意味は既存のキーと同じ）。移行の間は、今の 1.x のピンを「追加」側に、2.0.0 を主のピンにする
- PreBuildProcessor: 削除処理（今は置く DLL 以外の `unity-windows-native-toolkit*.dll` と `windows-native-toolkit*.dll` を `.meta` ごとすべて消す、`PreBuildProcessor.cs:546-565`）で、**主と追加の 2 つの名前を除外する**。
  追加の DLL も development ビルドなら `extra_install_as_debug` の名前で置く（Clipboard / Notification は `DEVELOPMENT_BUILD` で `-debug` の名前を読む）。`ConfigureWindowsPluginImporter` を両方に当てる
- 確かめ方（5.6 の手順 1）: Player の `Plugins/x86_64` に 2 本あり、md5 が dist と一致する。Dialog に加えて Clipboard / Notification の Player テストも通る
- 3 機能を移し終えたら追加のキーと処理を消す。`VERSION.txt` のコメント（3 つの Manager の `DLL_NAME` と一致させる）と親課題 README の「移行時にやること」をこれに合わせて直す

### 5.5 呼び出しの流れと契約（OP-02 の例）

1. `ShowFileDialog(buffer_size, filter)`（`buffer_size` は使わない）
2. **引数の検査**: J-4 のフィルタの分解。誤りなら `FileDialogResult(null, false, false, -2)` を出して終わる
3. **J-7**: 版の確認（Editor では常に `-4`）
4. UTF-8 に変換して確保し、4.2 の固定値で `ntk_dialog_file_request` を組み立てる
5. `ntk_dialog_show_open_file(ref request, filters, count, out IntPtr path)`。ダイアログが閉じるまで戻らない。`SYSTEM_ERROR` なら、ほかの呼び出しを挟まずに `ntk_last_system_code()` を読む
6. `finally` で `ntk_string_free(path)` と、確保した入力をすべて `FreeHGlobal`
7. **try / catch / finally の外で**、4.3 の結果でイベントを 1 回出す

- 捕まえる範囲は 3〜6（ネイティブ呼び出しと変換）だけ。`DllNotFoundException` などは `-4`、それ以外の例外（`OutOfMemoryException` など）は `-3` にして、7 でイベントを出す
- **購読者が投げた例外は捕まえない。** 1.x と同じく呼び出し側へ伝わる。イベントは 1 呼び出しに 1 回
- スレッド: 同期。呼び出したスレッド（通常はメインスレッド）で閉じるまで止まる。イベントは同じ呼び出しの中で、そのスレッドで出る
- ドメインリロード: Dialog はコールバックもハンドルも持ち越さないので、対処は要らない（親課題 README の「ドメインリロード」の対象外）
- アラートは 2〜3 が「J-1 / J-2 → J-7」になる

### 5.6 実装順

1. J-6（2 本置き）を入れ、1.x のままビルドと全テストが通ること、Player に 2 本あることを確かめる
2. `WindowsDialogErrorCodes`、`WindowsDialogCApiMapping` と層 1 のテスト
3. `WindowsDialogCApi`
4. `WindowsDialogManager` の置き換え（二重ガード、J-1〜J-9）と層 2a のテスト
5. 層 2b: D-01〜D-14 が期待値を変えずに通ること、7.3 の追加
6. Notification を 2.0.0 に移したあとで、D-09〜D-12 を流し直す（`ntk_notification_manager_create` は呼び出したスレッドを MTA にする。フォルダ選択は STA を前提にしている。親課題 README の「STA / MTA の分離」）
7. 3 機能を移したあとで、IL2CPP の Player で層 2b を 1 回流す（J-10）

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | ユーザーがキャンセル（OP-02〜06） | ネイティブ | `(null, true, true, -1)` | 2b（D-03 / D-05 / D-07 / D-09 / D-11） |
| E-2 | アラートで Cancel を押す（OP-01） | ネイティブ | 成功。`result = 2` | 2b（D-02） |
| E-3 | OS が失敗を返す | ネイティブ | `unchecked((int)ntk_last_system_code())`（負にもなる） | 1（変換関数）。実際の OS の失敗は起こせない |
| E-4 | ネイティブが引数を拒む | ネイティブ | `-2`、ログに system code | 1（変換関数）。正しい実装では起きない |
| E-5 | ネイティブの想定外・メモリ不足 | ネイティブ | `-3`、ログに system code | 1（変換関数）。起こせない |
| E-6 | タイトルかメッセージが空（J-1） | C# | ネイティブを呼ばず `(null, false, -2)` を 1 回 | 2a、2b |
| E-7 | 2.0.0 に無いフラグ（J-2） | C# | ネイティブを呼ばず `(null, false, -2)` | 1（分解）、2a、2b |
| E-8 | 組にならないフィルタ（J-4） | C# | ネイティブを呼ばず `-2` | 1、2a |
| E-9 | DLL が無い・関数が無い・major が 2 でない・Editor（J-7） | C# | `-4` | 1（版判定）、2a（Editor で `-4`） |
| E-10 | 複数選択で 0 件（J-5） | ネイティブ | 空の一覧で成功 | 1（一覧の変換）。実際のダイアログでは起こせない |
| E-11 | C# 側の予期しない例外（`OutOfMemoryException` など） | C# | `-3`、イベントは 1 回 | 起こせない。5.5 の構造（イベントは catch の外）をコードレビューで確かめる |
| E-12 | アラートのネイティブの失敗（J-9） | ネイティブ | `(0, false, errorCode)` | 1（変換関数） |

## 7. テスト方針

### 7.1 層 1（EditMode、`WindowsDialogCApiMappingTests`）

- 構造体の大きさと、**全フィールドの位置**（`Marshal.OffsetOf`、4.1 の表）
- フラグの分解: 各 `MB_*` の値、**別の引数に入れたフラグ**（例: `buttons = MB_YESNO | MB_ICONWARNING`）、`MB_HELP`、J-2 の拒否（`MB_SYSTEMMODAL` など、未定義のビット）。期待値の列挙の番号はヘッダーの値を使う
- 結果の Win32 ID への逆変換（11 値）、J-9
- エラーの変換（E-3〜E-5、E-12。`SYSTEM_ERROR` の値が負になる場合を含む）
- Win32 形式のフィルタの分解（正しい形、`""`、null、奇数個、パターンが空）
- リクエスト構造体の組み立て: `allow_missing_file = 0`、`skip_overwrite_prompt = 0`、ファイル系 `title = 0`、`owner = 0`、`reserved* = 0`、`struct_size`
- 版判定（J-7）: `0x020000` と `0x020103` は通り、`0x010200` と `0x030000` は `-4`
- UTF-8 の変換（非 ASCII、サロゲートペア、空文字、バイト長で読むこと）と、一覧の変換（0 件で空の一覧、E-10）

### 7.2 層 2a（PlayMode・Editor、`WindowsDialogManagerEditorTests`）

Manager が Editor でもコンパイルされる（5.1）ので、ネイティブに届かない経路をダイアログなしで確かめる。

- J-1: 空のタイトルで `(null, false, -2)` が 1 回だけ
- J-2: `MB_SYSTEMMODAL` で `(null, false, -2)`
- J-4: 組にならないフィルタで `-2`
- J-7: 正しい引数でも Editor では `-4`（ネイティブへ行かない）

### 7.3 層 2b（PlayMode・Player、`WindowsDialogSamplePlayerTests`）

- **D-01〜D-14 を期待値を変えずに通す。これが移行の検証。** 実装時に確かめる前提: コントロール ID 1148 / 1001 / 1152、上書き確認の Task Dialog、D-06 の引用符つきフルパス、カレントディレクトリの移動
- 追加（Manager を直接呼ぶ）:
  - J-1 / J-2 がダイアログを出さないこと。閉じる役を起動しておき、終了コード 2（`no dialog`）を期待するヘルパーを足す。出てしまったときに閉じられるよう、手順は `press 2`
  - 非 ASCII の往復: fixture に日本語とサロゲートペアを含むフォルダとファイル（例: `ダイアログ-𠮷`）を足し、D-04 / D-10 / D-12 相当で返るパスが一致し、`File.Exists` / `Directory.Exists` が真になること
  - `def_ext`: 保存で拡張子なしの `new` を入れ、`new.txt` が返ること
  - 題名の UTF-8: フォルダ選択の `title` に非 ASCII を渡し、閉じる役が `GetWindowText` で読んで照合する。アラートの `title` と本文も同じく照合する（本文は ID 65535 の Static）
- 層 2b は Mono で走る。IL2CPP は J-10

### 7.4 層 3・手動

- 層 3: D-08 / D-14（保存でファイルが作られない・既存が書き換わらない）をテストがファイルを直接見る（既存）
- 手動: なし。見た目（アイコン、既定ボタンの位置、表示言語）は確かめない（`2026-09-26-windows-dialog-ui-test-plan-v1.md` 1 章と同じ）

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。手動に残す項目は無い。起こせない失敗（E-3 / E-5 / E-11 の実物）は、変換を層 1 で押さえ、構造をコードレビューで確かめる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- テスト用 Player のファイアウォールの規則（受信ブロック、既存。`artifact/topics/cross-platform-testing/README.md`）
- 前提の OS 設定: なし
- OS が求める許可: なし

### 8.3 呼び出し側を止める OS の画面

6 関数すべてがモーダルで、閉じるまで呼び出し側（メインスレッド）を止める。
現在のハーネスには外から閉じる手段がある: `WindowsDialogSamplePlayerTests` が押す前に PowerShell の閉じる役を起動し、Win32 のメッセージ
（`WM_COMMAND`、`WM_SETTEXT`、`TDM_CLICK_BUTTON`）と、ファイル一覧の選択に UI Automation を使う（`2026-09-26-windows-dialog-ui-test-plan-v1.md` 2 章）。
ダイアログが出ないことを確かめるテスト（7.3）も閉じる役を起動しておき、出てしまっても実行が止まらないようにする。
