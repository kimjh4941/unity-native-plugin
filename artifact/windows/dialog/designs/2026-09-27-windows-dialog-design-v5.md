# Windows Dialog 実装計画 v5（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Dialog（Windows） |
| 種別 | **移行。機能追加ではない。** 1.x の C ABI（dist 1.11.0）から 2.0.0（dist 1.12.0）へ P/Invoke 層を書き換える |
| 親課題 | `artifact/topics/windows-c-abi-2/README.md`（3 機能の目次。Dialog は「Dialog・振る舞いが変わる」） |
| 移行前の基準 | `50fe7bb`（`feature/UNT-12`）。Dialog のサンプルの UI 自動テスト D-01〜D-14 が 1.x で通る |
| 方針 | **既存の公開 API（Manager のメソッド 6 本、イベント 6 本、引数、既定値、値の意味、`Win32MessageBox` の定数）を変えない**（親課題 Q-4）。**公開面への追加は `WindowsDialogErrorCodes` だけ**（J-3）。サンプル・UXML は触らない。D-01〜D-14 がそのまま移行の検証になる。変わる振る舞いは 5.3「既知の差分」に列挙したものだけ |
| DLL | `windows-native-toolkit-capi-2.0.0.dll` を dist の名前のまま同梱し、リポジトリにコミットする（親課題 Q-5） |
| レビュー | `artifact/windows/dialog/reviews/2026-09-27-windows-dialog-design-review-v1.md`、`...-review-v2.md`、`...-review-v3.md`、`...-review-v4.md` |

## v4 からの主な変更

レビュー v4（A1 / A2 なし）の B と C を反映した。再レビューはしない。

- B: パターンを `;` で分けて空でない要素が 0 個なら J-4（4.2）。1.x がフィルタをどう読むかを、手順 1 で実物で記録する（5.6、7.3）。`-5` の行き先（5.5、4.3）
- C: 疑似コードの手順 3、解放は `!= IntPtr.Zero` のときだけ、閉じる役の期待値に `;` を使わない、J-4 の言い方、文字列の終わりで一覧も終わる、8.3 の参照を UI テスト計画 v2 に

## v3 からの主な変更（v4）

レビュー v3 の指摘を反映した。

- A1: Win32 形式のフィルタを、名前が空の要素で一覧が終わる読み方にした（4.2）
- A2: 閉じる役の `expect-text` を `WM_GETTEXT` で読み、期待値を行の残り全体で取る（7.3）。`extern` を `internal` にした（4.1）。層 2b の J-1 / J-2 にも `LogAssert.Expect`（7.3）
- B: `-5` の分かれ目を `WindowsDialogCApi` の中だけに置いた（5.5）。try / finally / catch の入れ子の形を書いた（5.5）。5.3 に「そのほかの例外」を足した。`def_ext = ""` の手順を具体にした（5.6）
- C: UI テスト計画は v1 を直さず v2 を作る（5.1）。ほかに初期化の位置、追加のピンのキー、`VERSION.txt` のピンごとの書き方、J-11 の範囲、`testing.md`、`--include-destructive`、fixture の置き場所、8.1

## v2 からの主な変更（v3）

- A1: 6 本すべての `null` の既定値を 4.2 に表で書き、層 1 に足した。`ntk_last_system_code` を実際に呼ぶテストを層 2b に足した。5.3 に残っていた差分（例外がイベントに変わる、GetLastError が 0 のアラート、空の引数で 3 回、入力側のサロゲート、`def_ext = ""`、Editor でのコンパイル）を足した
- A2: 出力ハンドルの初期化（5.5）、層 2a のファイルのガード（5.1）、追加 DLL を削除より前に解決する（5.4）、非 ASCII の fixture の置き場所と、閉じる役の照合の手順（7.3）
- B: 例外の変換を純粋な関数にして層 1 で固定し、層 2a に購読者の例外のテストを足した。`extern` 14 本を 4.1 に列挙した。5.6 の各段階に層 0 を入れた。マニュアルへの経路を実装結果ファイルにした
- 開発者の判断（2026-09-27）: `-4` を「DLL・関数・版」と `-5`「Editor」に分ける。`WindowsDialogErrorCodes` は公開のまま（J-3）。アラートの C# 側の失敗は `result = null`（J-9）。PDB は Dialog の最初の段階で直す（J-11）。`def_ext = ""` は 1.x の振る舞いを記録してから 5.3 の記述を決める

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

共通（`Common.h`）の 8 本: `ntk_version()`、`ntk_last_system_code()`、`ntk_string_data` / `_size` / `_free`、`ntk_string_list_count` / `_at` / `_free`。
文字列は入出力とも NUL 終端の UTF-8。1.x は UTF-16 と呼び出し側のバッファだった。
`ntk_string_list_at` が返すポインタは**一覧の中を指す借用**で、一覧を解放するまでだけ有効。個別には解放しない。

**2.0.0 のダイアログは 1.x と同じ Win32 の呼び出しを、同じフラグで行う。** Data 層は 1.x の `WindowsDialogManager.cpp` を移したもの
（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsDialogManager.cpp` と `windows/WindowsLibrary/src/Dialog/Data/WindowsDialogWin32.h`）。
本計画の固定値（4.2）でそこに到達するので、D-01〜D-14 が頼るコントロール ID（1148 / 1001 / 1152）、上書き確認の Task Dialog、
フォルダ選択の `IFileOpenDialog` の振る舞い、カレントディレクトリの移動は変わらない。**ただしコントロール ID は OS が決めるもので、実装時に D-01〜D-14 を流して確かめる。**

### 1.2 型と値

- `ntk_dialog_error`: `NONE`(0) / `INVALID_PARAMETER`(1) / `CANCELED`(2) / `BUFFER_TOO_SMALL`(3、予約・返らない) / `SYSTEM_ERROR`(4) / `UNKNOWN`(5)
- `ntk_last_system_code()` は `uint32_t`。`SYSTEM_ERROR` のとき OS の生の値（CommDlgExtendedError、HRESULT、GetLastError のどれか。**HRESULT は上位ビットが立つので、`int` にすると負になる**）。
  C ABI がメモリ不足を捕まえたときは `UNKNOWN` と `0x8007000E`。`INVALID_PARAMETER` のときは常に 0（`DialogCApi.cpp:51`）。`thread_local` で、同じスレッドで直後に読む
- ネイティブが `INVALID_PARAMETER` を返すのは、本計画の使い方では C# 側の誤りのとき: `struct_size` の不一致、`reserved*` が 0 でない、列挙の範囲外（`DialogConvert.cpp:50-53`。例: Win32 の `0x40` をそのまま `icon` に入れる）、フィルタのパターンが NULL、`filters` が NULL で件数が 1 以上（`:71`）、out 引数が NULL
- アラートの列挙: `buttons`（OK=0、OK_CANCEL=1、YES_NO=2、YES_NO_CANCEL=3、RETRY_CANCEL=4、ABORT_RETRY_IGNORE=5、CANCEL_TRY_CONTINUE=6）、
  `icon`（NONE=0、INFORMATION=1、WARNING=2、ERROR=3、QUESTION=4）、`default_button`（FIRST=0〜FOURTH=3）、
  結果（OK=0、CANCEL=1、YES=2、NO=3、RETRY=4、ABORT=5、IGNORE=6、TRY_AGAIN=7、CONTINUE=8、CLOSE=9、HELP=10）。**Win32 の `MB_*` / `IDOK` とは番号が違う**
- OP-01 はヘッダーでは `CANCELED` を返さないとされるが、`Classify` の上では起こりうる（4.3 で扱う）
- アラートで `MessageBoxW` が 0 を返し、`GetLastError` も 0 のとき、2.0.0 は `UNKNOWN` を返す（`WindowsDialogError.h:48`、`WindowsDialogApi.cpp:62`）
- `ntk_dialog_filter { const char* name; const char* patterns; }`。`patterns` は `;` 区切りで必須。`filters` が NULL で件数 0 なら全ファイル
- 保存の `default_extension` は、空文字列を `nullptr` にして Win32 に渡す（`WindowsDialogApi.cpp:121`）
- どの関数もモーダルで、閉じるまで呼び出したスレッドを止める。フォルダ選択は、呼び出したスレッドが未初期化なら COM を STA で初期化する

### 1.3 1.x から変わること（親課題 README の「Dialog・振る舞いが変わる」）

キャンセルが `-1` ではなく `CANCELED`。フィルタが配列。ファイル系のタイトル・`allow_missing_file`・owner が効く。複数ファイルがフルパス。
保存の上書き確認を止められる。パスは終端込み 1024、一覧は全体 32768 文字。本計画でこれらを公開 API にどう映すかは 4 章と 5.3。

## 2. 既存の C# 実装

- `Runtime/Dialog/WindowsDialogManager.cs`: `MonoBehaviour` Singleton（`DontDestroyOnLoad`）。今は `#if UNITY_STANDALONE_WIN`（Editor では Windows ターゲットのときだけコンパイルされる）。
  公開メソッド 6 本は同期で、結果はイベント 6 本で同じ呼び出しの中で返す。try/catch は無く（try/finally だけ）、購読者の例外も、DLL が無いときの `DllNotFoundException` も呼び出し側へ伝わる。
  1.x の DLL は Editor 無効（`ConfigureWindowsPluginImporter`）なので、Windows ターゲットの Editor で呼ぶと例外になっていた
- `DLL_NAME` は `DEVELOPMENT_BUILD` で `unity-windows-native-toolkit-debug` / `unity-windows-native-toolkit` を切り替える
- `Runtime/Dialog/Win32MessageBox.cs`: 公開の `MB_*` 定数（`MB_HELP` は無い）
- 公開メソッドの既定値と、`null` を渡したときの置き換え（`WindowsDialogManager.cs:211-214,230-233,266-267,275-276,313-314,322-323,398-399,407-408,445-446,454-455,524-526,535-537`）は 4.2 の表のとおり
- 公開イベント（**移行後もこのまま**）:

| イベント | 引数 | キャンセル | 失敗（1.x） |
|---|---|---|---|
| `AlertDialogResult` | `(int? result, bool isSuccess, int? errorCode)`。`result` は Win32 の `IDOK` 等 | Cancel ボタンは `result = IDCANCEL(2)` の成功 | `(0, false, errorCode)`（`MessageBoxW` の戻り値 0）。GetLastError が 0 なら `(0, true, null)`（`WindowsDialogManager.cs:245-253`） |
| `FileDialogResult` / `SaveFileDialogResult` / `FolderDialogResult` | `(string? path, bool isCancelled, bool isSuccess, int? errorCode)` | `(null, true, true, -1)` | `(null, false, false, errorCode)` |
| `MultiFileDialogResult` / `MultiFolderDialogResult` | `(ArrayList? paths, bool isCancelled, bool isSuccess, int? errorCode)`。要素はフルパス | `(null, true, true, -1)` | `(null, false, false, errorCode)` |

- 既知の不具合: `ShowDialog` はタイトルかメッセージが空のとき、`(null, false, null)` を出したあと `return` せずネイティブを呼ぶ（`WindowsDialogManager.cs:218-228`）。
  両方が空なら 2 回出す。`MessageBoxW` は空の文字列を受けるので空のダイアログが出て、その結果でもう 1 回出る。**1 回の呼び出しで最大 3 回**
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
- `extern` は `internal static extern` にする（層 2b のテストが `ntk_last_system_code` を直接呼ぶため。1.x の `private static extern`、`WindowsDialogManager.cs:121-195` に倣うと、`InternalsVisibleTo` でも呼べず、しかも Player でしかコンパイルされないので層 0 まで気付かない）
- 構造体は `WindowsDialogCApi` の入れ子の `internal struct` にする（P1。C# 名は `AlertRequest` / `FileRequest` / `SaveFileRequest` / `FolderRequest` / `Filter`）。
  `[StructLayout(LayoutKind.Sequential, Pack = 8)]`、ポインタは `IntPtr`。`struct_size = (uint)Marshal.SizeOf<T>()`、`reserved*` は 0

| 構造体 | 大きさ | フィールドの位置（native-toolkit `WindowsLibraryCApiTest/Common/CApiLayoutTest.cpp:23-55`） |
|---|---|---|
| `ntk_dialog_filter` | 16 | name 0、patterns 8 |
| `ntk_dialog_alert_request` | 56 | struct_size 0、reserved0 4、title 8、message 16、buttons 24、icon 28、default_button 32、top_most 36、show_help_button 40、reserved1 44、owner 48 |
| `ntk_dialog_file_request` | 32 | struct_size 0、reserved0 4、title 8、allow_missing_file 16、reserved1 20、owner 24 |
| `ntk_dialog_save_file_request` | 40 | struct_size 0、reserved0 4、title 8、default_extension 16、skip_overwrite_prompt 24、reserved1 28、owner 32 |
| `ntk_dialog_folder_request` | 24 | struct_size 0、reserved0 4、title 8、owner 16 |

`extern` の宣言（14 本。`Dialog.h:138-170`、`Common.h:54-88`）:

| C | C# |
|---|---|
| `ntk_dialog_show_alert` | `int (ref AlertRequest request, out int outResult)` |
| `ntk_dialog_show_open_file` | `int (ref FileRequest request, IntPtr filters, UIntPtr filterCount, out IntPtr outPath)` |
| `ntk_dialog_show_open_files` | `int (ref FileRequest request, IntPtr filters, UIntPtr filterCount, out IntPtr outPaths)` |
| `ntk_dialog_show_save_file` | `int (ref SaveFileRequest request, IntPtr filters, UIntPtr filterCount, out IntPtr outPath)` |
| `ntk_dialog_show_pick_folder` | `int (ref FolderRequest request, out IntPtr outPath)` |
| `ntk_dialog_show_pick_folders` | `int (ref FolderRequest request, out IntPtr outPaths)` |
| `ntk_version` / `ntk_last_system_code` | `uint ()` |
| `ntk_string_data` | `IntPtr (IntPtr s)` |
| `ntk_string_size` | `UIntPtr (IntPtr s)` |
| `ntk_string_free` / `ntk_string_list_free` | `void (IntPtr p)` |
| `ntk_string_list_count` | `UIntPtr (IntPtr list)` |
| `ntk_string_list_at` | `IntPtr (IntPtr list, UIntPtr index, out UIntPtr outSize)` |

`filters` は `Filter[]` を `AllocHGlobal` に並べたポインタ（件数 0 なら `IntPtr.Zero`）。

- 入力の文字列: `Encoding.UTF8` で NUL 終端のバイト列にし、`Marshal.AllocHGlobal` に置く。確保したポインタはすべて 1 つの一覧で追い、途中で失敗しても `finally` で全部解放する。フィルタ配列も同じ
- 出力の文字列: `ntk_string_data` と `ntk_string_size`（バイト数）でバイト列を写し、UTF-8 で読む。一覧は `_count` と `_at`（`out_size` を使う）で**一覧を解放する前に**全要素を managed の文字列へ写し、最後に `ntk_string_list_free` を 1 回だけ呼ぶ
- ハンドルは `SafeHandle` にしない。呼び出しは同期で、受け取ったその場で写して `finally` で解放するため（親課題 README の「マーシャリングの落とし穴」は非同期の API を念頭に置いたもの）
- コールバックは無いので、`MonoPInvokeCallback` は要らない

### 4.2 引数の変換（公開 API は変えない）

**`null` の置き換えは 1.x のまま、6 本すべてで行う。** 既定値の引数に明示的に `null` を渡したときも、1.x と同じ値になる。

| メソッド | 引数 | 既定値 / `null` のとき |
|---|---|---|
| `ShowDialog` | `buttons` / `icon` / `defbutton` / `options` | `MB_OK` / `MB_ICONINFORMATION` / `MB_DEFBUTTON1` / `MB_APPLMODAL` |
| `ShowFileDialog` / `ShowMultiFileDialog` / `ShowSaveFileDialog` | `filter` | `"All Files\0*.*\0\0"` |
| `ShowFolderDialog` | `title` | `"Select Folder"` |
| `ShowMultiFolderDialog` | `title` | `"Select Folders"` |
| `ShowSaveFileDialog` | `def_ext` | `"txt"` |
| 全ファイル系 | `buffer_size` | 使わない（J-8）。既定値 1024 / 4096 は宣言に残す |

**アラートのフラグは、1.x と同じく 4 引数の OR として読む。** 1.x のネイティブは `buttons | icon | defbutton | options` を 1 つにして `MessageBoxW` に渡していた
（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsDialogManager.cpp:63-64`）ので、どの引数に入れたフラグも効いた。

1. `null` を上の既定値に置き換え、4 つを OR する
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
| `filter`（Win32 形式 `"名前\0パターン\0...\0\0"`） | `ntk_dialog_filter[]`。**Win32 と同じく、先頭から「名前・パターン」の順に読み、名前が空の要素に来たら一覧を終える。それより後ろは捨てる。文字列が終わったときも一覧を終える**（1.x ではこの文字列を OS がそう読んでいたはず。Win32 の文書からの推定なので、5.6 の手順 1 で 1.x のまま記録して確かめる）。名前があってパターンが無い・空、またはパターンを `;` で分けて空でない要素が 0 個（`";"` など）なら J-4（新しいネイティブは空の要素を捨て、0 個なら `*.*` にするので、そのまま渡すと黙って全ファイルになる。`DialogConvert.cpp:81-87`、`WindowsDialogMapping.h:49-50`）。読み終えて 0 組（`""`、`"\0*.txt\0\0"` など）なら既定値（全ファイル 1 組）と同じ扱い（5.3）。例: `"A\0*.a\0\0B\0*.b\0\0"` は A の 1 組 |
| `ShowFolderDialog` / `ShowMultiFolderDialog` の `title` | `request.title`（`null` は上の既定値に置き換えてから） |
| `ShowSaveFileDialog` の `def_ext` | `request.default_extension`（同上） |

1.x の振る舞いに合わせて固定する値: `owner = NULL`、ファイル系の `title = NULL`、`allow_missing_file = 0`、`skip_overwrite_prompt = 0`。
リクエスト構造体を組み立てる処理は、ネイティブを呼ばない純粋な関数にし（5.1 の `WindowsDialogCApiMapping`）、層 1 でこれらの値と `null` の置き換えを固定する。

### 4.3 結果の変換

| 結果 | イベントへ |
|---|---|
| OP-01 の `NONE` | `result` を名前で Win32 の ID に戻す: OK→1、CANCEL→2、ABORT→3、RETRY→4、IGNORE→5、YES→6、NO→7、CLOSE→8、HELP→9、TRY_AGAIN→10、CONTINUE→11。`(id, true, null)` |
| OP-01 のネイティブの失敗（`NONE` 以外すべて。`CANCELED` を含む） | `(0, false, errorCode)`（J-9。`out_result` は読まない）。`CANCELED` は `-3` で、ログは「OP-01 で想定外の `CANCELED`」だけ（system code は 0 なので出さない） |
| OP-01 の C# 側の失敗（J-1 / J-2 / J-7、例外） | `(null, false, errorCode)`（J-9） |
| OP-02〜06 の `NONE` | パス（一覧）。0 件なら空の `ArrayList` で `(list, false, true, null)`（J-5） |
| OP-02〜06 の `CANCELED` | `(null, true, true, -1)` |
| `SYSTEM_ERROR` | `errorCode = unchecked((int)ntk_last_system_code())`。1.x と同じ種類の値。負にもなる |
| `INVALID_PARAMETER` | `-2`。ログに「C# 側の構造体の誤りの可能性」と、関数名・リクエストの `struct_size` を出す（system code は常に 0 なので出さない） |
| `WindowsDialogCApi` が返した `-5`（Editor） | `-5`。ダイアログは呼ばず、system code も読まない。アラートは `result = null` |
| `UNKNOWN`、想定外の値 | `-3`。ログに system code を出す（メモリ不足は `0x8007000E`、アラートの GetLastError 0 は 0） |
| C# 側の例外（5.5） | `DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` → `-4`、そのほか → `-3`。変換は `WindowsDialogCApiMapping` の純粋な関数 |

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | ガード | 内容 |
|---|---|---|---|
| 既存変更 | `Runtime/Dialog/WindowsDialogManager.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`（今は `UNITY_STANDALONE_WIN`） | 1.x の `DllImport` と、バッファ・UTF-16 の処理を削除。`WindowsDialogCApi` を呼ぶ。公開メソッド・イベント・既定値・`null` の置き換えはそのまま |
| 新規作成 | `Runtime/Dialog/WindowsDialogCApi.cs` | 型は `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR`、`DllImport` と呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR` | `internal static`。4.1 の入れ子の構造体、`extern` 14 本、UTF-8 の入出力、`ntk_string` / `_list` の読み取りと解放。Editor では呼び出しがネイティブへ行かず、`-5` を返す |
| 新規作成 | `Runtime/Dialog/WindowsDialogCApiMapping.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | `internal static`。4.2 / 4.3 の変換（`null` の置き換え、フラグの分解、フィルタの分解、リクエスト構造体の組み立て、結果・エラー・例外の変換、J-7 の版判定）。ネイティブを呼ばない |
| 新規作成 | `Runtime/Dialog/WindowsDialogErrorCodes.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | 公開の静的クラス。J-3 の定数。`int?` の errorCode に入れる値なので enum ではなく `int` の定数。前例は `MacClipboardErrorCodes`（`Runtime/Clipboard/MacClipboardErrorInfo.cs:76`、public static class・`int` 定数・複数形）。`WindowsClipboardErrorCode` は enum で、形が違う |
| 新規作成 | `Plugins/Windows/windows-native-toolkit-capi-2.0.0.dll` と `.meta` | — | dist 1.12.0 から。両方コミットする（既存の `unity-windows-native-toolkit.dll.meta` もコミットされている）。importer は Win64 のみ・Editor 無効 |
| 既存変更 | `Editor/Build/PreBuildProcessor.cs`、`Plugins/Windows/VERSION.txt` | — | J-6（5.4）。`VERSION.txt` の「`DLL_NAME` は `.dll` を付けない」（:18-19）をピンごとに書き直す: 主のピン（2.0.0）は `.dll` を付けた名前、移行中の追加のピン（1.x の `unity-windows-native-toolkit(-debug)`）は今のまま付けない |
| 既存変更 | `Editor/Build/PostBuildProcessor.cs` | — | J-11。Windows のブロック（:135-166）の PDB のコピーと development ビルドの分岐を消し、クラスのコメント（:16「copies PDB files」）も直す |
| 既存変更 | `Runtime/Dialog/Win32MessageBox.cs` | 今のまま | J-2 の XML コメントだけ。定数は変えない |
| 新規作成 | `Tests/Runtime/WindowsDialogCApiMappingTests.cs` | `UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` | 層 1 |
| 新規作成 | `Tests/PlayMode/WindowsDialogManagerEditorTests.cs` | **`#if UNITY_EDITOR`** | 層 2a（7.2）。PlayMode の asmdef は `includePlatforms: []` で Player にも入るので、Editor だけに絞る。Player に入ると `-5` の前提が崩れ、正しい引数の呼び出しが本物のダイアログを出して止まる。前例は `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:3-9` |
| 既存変更 | `Tests/PlayMode/WindowsDialogSamplePlayerTests.cs` | 今のまま | 層 2b。D-01〜D-14 は期待値を変えない。7.3 の追加。閉じる役に照合の手順を足す。「1.x」と書いたコメント（:68,153,179）を直す |
| 新規作成 | `artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md` | — | 版のついた文書は上書きしない（`artifact/README.md:28`）ので、v1 は残して v2 を作る。直すのは 1 章の「期待値は 1.x」（v1:21）、「タイトル・メッセージの表示は確かめない」（v1:46。7.3 の `expect-title` / `expect-text` で確かめるようになる）、「2.0.0 で効いたら足す」（v1:47。移行後もタイトルと owner は固定値で使わない）、4 章（UI テストの期待値は変えない）。`agent-rules/coding-rules/testing.md` の計画書への参照も v2 にする |
| 既存変更 | `agent-rules/coding-rules/testing.md` | — | :235（Dialog を「`\|\| UNITY_EDITOR` なし」の群から「あり」の群へ）、:314 と :404-406（Dialog に層 1 が無い → 層 1 と層 2a がある）、層 2b の本数と説明 |
| 既存変更 | `artifact/topics/windows-c-abi-2/README.md` | — | 「差し替えるバイナリ」（`ntk_version` の完全一致 → major、`DLL_NAME` に `.dll` を付ける）、「移行時にやること」（1 本で一括 → J-6 の 2 本置き）、「マーシャリングの落とし穴」（`SafeHandle` にしない理由）、「併せて壊れているもの」（J-11）、「Dialog・振る舞いが変わる」 |
| 非変更 | `Runtime/UI/Windows/Dialog/*`、リポジトリの `Assets/Scripts/Runtime/UI/Windows/Dialog/WindowsDialogManagerTest.cs`、`Runtime/Resources/UI/Windows/Dialog/*`、`Editor/UI/NativeToolkitEditorWindow.cs` | — | 公開 API とサンプルは変えない。Editor ウィンドウが参照するのはサンプルのコントローラーだけ |

Manager を `|| UNITY_EDITOR` にするのは、層 2a で J-1 / J-2 / J-4 / J-7 をダイアログなしで確かめるため。
Player に他 OS のコードは入らない（型のガードは Windows と Editor だけ）。サンプルのコントローラーは今も `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` で Manager を呼ぶので影響しない。

### 5.2 判断（開発者の承認済み、2026-09-27）

| # | 内容 | 決定 |
|---|---|---|
| J-1 | `ShowDialog` のタイトルかメッセージが空 | ネイティブを呼ばず `AlertDialogResult(null, false, -2)` を 1 回出して終わる。1.x は最大 3 回出していた（2 章） |
| J-2 | 2.0.0 に対応先の無いフラグ | ダイアログを出さずに `AlertDialogResult(null, false, -2)` とエラーログ。`Win32MessageBox` の定数は消さず、XML コメントに「2.0.0 では使えない」と書く |
| J-3 | 予約の errorCode | 公開の `WindowsDialogErrorCodes` に置く: `Cancelled = -1`（既存）、`InvalidArgument = -2`（J-1 / J-2 / J-4 / ネイティブの `INVALID_PARAMETER`）、`Unknown = -3`、`NativeUnavailable = -4`（DLL が無い・関数が無い・形式が違う・major が 2 でない）、`PlatformUnavailable = -5`（Editor）。**原因ごとに分けておく**（公開の定数は、あとから分けると破壊的な変更になる。Clipboard も `PlatformUnavailable` と `BridgeUnavailable` を分けている、`WindowsClipboardErrorCode.cs:78,81`）。**それ以外は OS の生の値で、HRESULT なら負にもなる。判定は符号ではなく定数との比較で行う**（XML コメントにこう書く）。-1〜-5 は OS が返さない（`0xFFFFFFFF` はネイティブがキャンセルとして先に除き、`0xFFFFFFFB`〜`0xFFFFFFFE` は定義された HRESULT ではない） |
| J-4 | Win32 形式のフィルタで、名前があるのにパターンが無い・空、または `;` で分けて空でない要素が 0 個 | ネイティブを呼ばずに `-2`。一覧の終わりは Win32 と同じく名前が空の要素（4.2） |
| J-5 | 複数選択で成功・0 件 | 空の `ArrayList` で `(list, false, true, null)`。1.x も同じ形を返しえた。キャンセルとは混ぜない |
| J-6 | 移行の途中で 1.x と 2.0.0 の DLL を 2 本置くか | 置く。5.4 |
| J-7 | DLL と C# の版 | 最初の呼び出しで `(ntk_version() >> 16) == 2` を 1 回確かめる。違えば以後ネイティブへ渡さず `-4` とエラーログ。`DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` も `-4`。Editor（`!UNITY_EDITOR` の外）は `-5` |
| J-8 | `buffer_size` | **使わない。意図した差分として受け入れる**（5.3）。引数は互換のため残し、XML コメントに「2.0.0 では無視する。上限は 1 本 1023 文字、一覧は全体 32768 文字」と書く。1.x の上限を C# で再現しても、1024 文字を超える長いパスは 2.0.0 が返さないので完全にはならない |
| J-9 | アラートが失敗したときの `result` | ネイティブの失敗は `0`（1.x の `MessageBoxW` の戻り値と同じ）。C# 側の失敗（J-1 / J-2 / J-7、例外）は `null`（ネイティブを呼んでいない、または結果が無い） |
| J-10 | IL2CPP | Windows の Player テストは既定の Mono で走る（IL2CPP は Android だけ）。**3 機能を移したあとに、IL2CPP の Player で 1 回流す。** それまで層 2b の結果は Mono での保証 |
| J-11 | `PostBuildProcessor` の PDB | Dialog の最初の段階（5.6 の手順 1）で直す。今は development ビルドのとき、解決しない絶対パスから 1.x の名前の PDB を写そうとして `LogError` を出す（`PostBuildProcessor.cs:148-161`）。dist 1.12.0 に PDB は無いので、**コピーの処理を消す** |

### 5.3 既知の差分（意図したもの）

既存の公開 API は変えないが、次の振る舞いは変わる。XML コメントに書き、実装結果ファイル（5.6 の手順 8）経由で次の版のマニュアルへ引き継ぐ。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| `buffer_size`（J-8） | 渡した大きさがバッファになり、超えると失敗。大きく渡せば長いパスも受けた | 無視。1 本 1023 文字、一覧は全体 32768 文字を超えると `SYSTEM_ERROR` |
| `null`・空のタイトル・メッセージ（J-1） | `(null, false, null)` と空のダイアログ（イベント最大 3 回） | ダイアログを出さず `(null, false, -2)` を 1 回 |
| 2.0.0 に無いフラグ（J-2） | `MessageBoxW` に渡って効いた | ダイアログを出さず `-2` |
| 名前があってパターンが無い・空、または `;` だけのフィルタ（J-4） | そのまま OS に渡した | `-2` |
| パターンの中の空の要素（`"*.txt;;*.log"`） | そのまま OS に渡した | 詰めて `"*.txt;*.log"` になる（新しいネイティブが行う） |
| DLL が無い・関数が無い・形式が違う | 例外が呼び出し側に出た | 例外は出ず `-4` のイベント |
| そのほかの C# 側の例外（`OutOfMemoryException` など） | 呼び出し側に出た | 例外は出ず `-3` のイベント |
| Editor で呼ぶ | Windows ターゲットなら例外、ほかのターゲットではコンパイルされない | どのターゲットでもコンパイルされ、`-5` のイベント |
| アラートで `MessageBoxW` が 0、GetLastError も 0 | 成功扱いの `(0, true, null)` | `(0, false, -3)` |
| 複数ファイルでドライブ直下を選ぶ | `C:\\a.txt`（区切りが重なる） | `C:\a.txt` |
| フォルダの `title` に `""` | 空の題名 | システムの題名 |
| `filter` が 0 組（`""`、名前が空で始まる） | 空のフィルタ | 全ファイル 1 組（`null` と同じ） |
| `def_ext` に `""` | 空文字列のまま `lpstrDefExt` に渡した（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsDialogManager.cpp:459`） | `nullptr` で渡す。**振る舞いが変わるかは 5.6 の手順 1 で 1.x のまま記録してから、この行を確定する**。C# では完全には再現できない（ネイティブが `""` と NULL を同じ扱いにする）。`nFilterIndex = 1` で固定なので、最初のフィルタの拡張子を渡せば一部は再現できるが、選んだフィルタを変えたときに合わなくなるので行わない |
| 対になっていないサロゲート | 入力（title / message / filter / def_ext）も出力のパスもそのまま | 入力は `Encoding.UTF8` で、出力はネイティブで U+FFFD に置き換わる |
| 新しい errorCode（J-3） | -1 と OS の値 | -2 / -3 / -4 / -5 が加わる |

### 5.4 J-6: 移行の間だけ DLL を 2 本置く

- `VERSION.txt` にキーを足す: `extra_dist_version`、`extra_dll`、`extra_install_as`、`extra_install_as_debug`（意味は既存のキーと同じ）。`debug_dll` に当たるキーは置かず、追加のピンのコピー元は常に `extra_dll`（dist に Windows の `-debug.dll` は無い。`PreBuildProcessor.cs:85` の `SourceFor` の規則）。移行の間は、今の 1.x のピンを「追加」側に、2.0.0 を主のピンにする
- PreBuildProcessor:
  - **追加のピンも、削除より前にコピー元を解決する。** 今の処理は主のピンについて「削除より前に解決し、無ければ `BuildFailedException`」（`PreBuildProcessor.cs:524-538`）。追加も同じにする。`extra_dist_version` があるのに `extra_dll` が無い、またはファイルが無いときもビルドを失敗させる（Player に入らず、実行時に `DllNotFoundException` で初めて気付くのを避ける）
  - 削除処理（今は置く DLL 以外の `unity-windows-native-toolkit*.dll` と `windows-native-toolkit*.dll` を `.meta` ごとすべて消す、`:546-565`）で、**主と追加の 2 つの名前を除外する**
  - 追加の DLL も development ビルドなら `extra_install_as_debug` の名前で置く（Clipboard / Notification は `DEVELOPMENT_BUILD` で `-debug` の名前を読む）。`ConfigureWindowsPluginImporter` を両方に当てる
- 確かめ方（5.6 の手順 1）: Player の `Plugins/x86_64` に 2 本あり、md5 が dist と一致する。Dialog に加えて Clipboard / Notification の Player テストも通る（Notification の 10 本はすべて `Destructive` なので、`scripts/verify_unity_windows.sh --include-destructive` で流す。`testing.md:376`）
- 3 機能を移し終えたら追加のキーと処理を消す。`VERSION.txt` のコメント（3 つの Manager の `DLL_NAME` と一致させる）と親課題 README の「移行時にやること」をこれに合わせて直す

### 5.5 呼び出しの流れと契約（OP-02 の例）

1. `ShowFileDialog(buffer_size, filter)`（`buffer_size` は使わない。`filter` の `null` は既定値に置き換える）
2. **引数の検査**: J-4 のフィルタの分解。誤りなら `FileDialogResult(null, false, false, -2)` を出して終わる
3. 出力のハンドルと、確保した入力の一覧を、try の外で空に初期化する（出力は `IntPtr.Zero`。最初の P/Invoke は 4 の `ntk_version` なので、その前に置く。解決前に例外になっても、`finally` が不定の値を解放しない。ネイティブが NULL を書くのは関数に入ったあと、`DialogCApi.cpp:66`）
4. **J-7**: 版の確認。`-4`（版が合わない・DLL が無い）か `-5`（Editor）なら、それを結果にして 8 へ進む（ダイアログは呼ばず、system code も読まない）
5. UTF-8 に変換して確保し、4.2 の固定値で `ntk_dialog_file_request` を組み立てる
6. `ntk_dialog_show_open_file(ref request, filters, count, out outPath)`。ダイアログが閉じるまで戻らない。**戻り値が `NONE` / `CANCELED` 以外なら、ほかの呼び出しを挟まずに `ntk_last_system_code()` を読む**（`SYSTEM_ERROR` の errorCode と、`UNKNOWN` のログに使う）
7. `finally` で解放する。出力のハンドルは `!= IntPtr.Zero` のときだけ `ntk_string_free` / `ntk_string_list_free` に渡す（版が合わず `-4` になったあとにネイティブへ渡さない、J-7）。確保した入力の `FreeHGlobal` は、ハンドルの解放が例外を投げても必ず走る順・形にする
8. **catch の外で**、4.3 の結果でイベントを 1 回出す

try の形は次のとおり。C# の catch は、同じ try の finally から出た例外を捕まえないので、finally を内側の try に置く。
こうすると、`ntk_string_free` などの結び付けの誤り（`EntryPointNotFoundException`）も `-4` のイベントになる（5.3）。

```
int code; string? result = null;       // 3
IntPtr outPath = IntPtr.Zero;          // 3
var allocated = new List<IntPtr>();    // 3
try
{
    try { /* 4〜6 */ }
    finally { /* 7 */ }
}
catch (Exception e) { code = WindowsDialogCApiMapping.FromException(e); }   // -4 / -3
FileDialogResult?.Invoke(...);          // 8
```

- 捕まえる範囲は 4〜7（ネイティブ呼び出しと変換）だけ。例外は 4.3 の純粋な関数で `-4` / `-3` に変え、8 でイベントを出す
- **Editor の分かれ目（`-5`）は `WindowsDialogCApi` の中だけに置く。** `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` の外では、`WindowsDialogCApi` の版の確認とダイアログの呼び出しが `-5` を戻り値として返し、Manager は Player と同じ try の中を通る。Manager には `#if UNITY_EDITOR` を置かない。
  これで層 2a の購読者の例外のテスト（7.2）が、Player と同じ構造を押さえる。`-5` のログは `LogWarning`（Editor で試しに呼ぶのは誤りではないため）
- **購読者が投げた例外は捕まえない。** 1.x と同じく呼び出し側へ伝わる。イベントは 1 呼び出しに 1 回
- スレッド: 同期。呼び出したスレッド（通常はメインスレッド）で閉じるまで止まる。イベントは同じ呼び出しの中で、そのスレッドで出る
- ドメインリロード: Dialog はコールバックもハンドルも持ち越さないので、対処は要らない（親課題 README の「ドメインリロード」の対象外）
- アラートは 2 が「J-1 / J-2」になる

### 5.6 実装順

各段階の終わりに、層 0（`scripts/verify_unity_windows.sh` の Player ビルド）を流す。`!UNITY_EDITOR` 側のコードは層 1 でも層 2a でもコンパイルされないため。

1. J-6（2 本置き）と J-11（PDB）を入れ、1.x のままビルドと全テストが通ること、Player に 2 本あることを確かめる。あわせて、層 2b に `def_ext = ""` のテストと、フィルタの読み方のテスト（7.3）を足し、1.x で見た結果を期待値として assert する（5.3、4.2）
2. `WindowsDialogErrorCodes`、`WindowsDialogCApiMapping` と層 1 のテスト
3. `WindowsDialogCApi`
4. `WindowsDialogManager` の置き換え（二重ガード、J-1〜J-9）と層 2a のテスト
5. 層 2b: D-01〜D-14 が期待値を変えずに通ること、7.3 の追加。`def_ext = ""` のテストが落ちたら（1.x と違う）、そのテストの期待値と 5.3 の行を同じコミットで直す。通ったら 5.3 の行は「差分なし」として消す
6. Notification を 2.0.0 に移したあとで、D-09〜D-12 を流し直す（`ntk_notification_manager_create` は呼び出したスレッドを MTA にする。フォルダ選択は STA を前提にしている。親課題 README の「STA / MTA の分離」）
7. 3 機能を移したあとで、IL2CPP の Player で層 2b を 1 回流す（J-10）
8. 実装結果ファイル（`artifact/windows/dialog/results/*-implementation-result*.md`）に、5.3 の表、`-2`〜`-5` の意味、使えなくなった `MB_*` を書き写す。write-manual はこのファイルを読む（`agent-rules/workflows/write-manual/workflow.md:26-29`）

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | ユーザーがキャンセル（OP-02〜06） | ネイティブ | `(null, true, true, -1)` | 2b（D-03 / D-05 / D-07 / D-09 / D-11） |
| E-2 | アラートで Cancel を押す（OP-01） | ネイティブ | 成功。`result = 2` | 2b（D-02） |
| E-3 | OS が失敗を返す | ネイティブ | `unchecked((int)ntk_last_system_code())`（負にもなる） | 1（変換関数）、2b（`ntk_last_system_code` の結び付け）。実際の OS の失敗は起こせない |
| E-4 | ネイティブが引数を拒む | ネイティブ | `-2`、ログに関数名と `struct_size` | 1（変換関数）。正しい実装では起きない |
| E-5 | ネイティブの想定外・メモリ不足・アラートの GetLastError 0 | ネイティブ | `-3`、ログに system code | 1（変換関数）。起こせない |
| E-6 | タイトルかメッセージが空（J-1） | C# | ネイティブを呼ばず `(null, false, -2)` を 1 回 | 2a、2b |
| E-7 | 2.0.0 に無いフラグ（J-2） | C# | ネイティブを呼ばず `(null, false, -2)` | 1（分解）、2a、2b |
| E-8 | 名前があってパターンが無い・空、または `;` だけのフィルタ（J-4） | C# | ネイティブを呼ばず `-2` | 1、2a |
| E-9 | DLL が無い・関数が無い・形式が違う・major が 2 でない（J-7） | C# | `-4`。アラートは `result = null` | 1（版判定、例外の変換） |
| E-10 | Editor で呼ぶ（J-7） | C# | `-5`。アラートは `result = null` | 2a |
| E-11 | 複数選択で 0 件（J-5） | ネイティブ | 空の一覧で成功 | 1（一覧の変換）。実際のダイアログでは起こせない |
| E-12 | C# 側の予期しない例外（`OutOfMemoryException` など） | C# | `-3`、イベントは 1 回。アラートは `result = null` | 1（例外の変換）。イベントを catch の外で 1 回出す構造は 2a（購読者の例外） |
| E-13 | アラートのネイティブの失敗（J-9） | ネイティブ | `(0, false, errorCode)` | 1（変換関数） |

## 7. テスト方針

### 7.1 層 1（EditMode、`WindowsDialogCApiMappingTests`）

- 構造体の大きさと、**全フィールドの位置**（`Marshal.OffsetOf`、4.1 の表）
- `null` の置き換え: 6 本の各引数に `null` を渡したとき、4.2 の表の既定値になる（題名、`def_ext`、フィルタ、アラートのフラグ）
- フラグの分解: 各 `MB_*` の値、**別の引数に入れたフラグ**（例: `buttons = MB_YESNO | MB_ICONWARNING`）、`MB_HELP`、J-2 の拒否（`MB_SYSTEMMODAL` など、未定義のビット）。期待値の列挙の番号はヘッダーの値を使う
- 結果の Win32 ID への逆変換（11 値）、J-9（ネイティブの失敗は `0`、C# 側は `null`、OP-01 の `CANCELED` は `-3`）
- エラーの変換（E-3〜E-5、E-13。`SYSTEM_ERROR` の値が負になる場合を含む）
- 例外の変換: `DllNotFoundException` / `EntryPointNotFoundException` / `BadImageFormatException` → `-4`、`OutOfMemoryException` など → `-3`
- Win32 形式のフィルタの分解: 正しい形、`""`、`null`、`"A\0*.a\0\0B\0*.b\0\0"`（A の 1 組だけ）、`"\0*.txt\0\0"`（0 組 → 全ファイル）、`"Text\0*.txt"`（終わりの NUL が無い → 1 組）、名前があってパターンが無い・空、`"A\0;\0\0"`（J-4）
- リクエスト構造体の組み立て: `allow_missing_file = 0`、`skip_overwrite_prompt = 0`、ファイル系 `title = 0`、`owner = 0`、`reserved* = 0`、`struct_size`
- 版判定（J-7）: `0x020000` と `0x020103` は通り、`0x010200` と `0x030000` は `-4`
- UTF-8 の変換（非 ASCII、サロゲートペア、対になっていないサロゲート、空文字、バイト長で読むこと）と、一覧の変換（0 件で空の一覧、E-11）

### 7.2 層 2a（PlayMode・Editor、`WindowsDialogManagerEditorTests`、`#if UNITY_EDITOR`）

Manager が Editor でもコンパイルされる（5.1）ので、ネイティブに届かない経路をダイアログなしで確かめる。エラーログは `LogAssert.Expect` で受ける。

- J-1: 空のタイトルで `(null, false, -2)` が 1 回だけ。タイトルとメッセージの両方が空でも 1 回だけ
- J-2: `MB_SYSTEMMODAL` で `(null, false, -2)`
- J-4: 名前があってパターンが無い・空、または `;` だけのフィルタで `-2`
- J-7: 正しい引数でも Editor では `-5`（ネイティブへ行かない）。アラートは `result = null`
- 5.5 の契約: `-5` の経路で例外を投げる購読者を付けると、例外が呼び出し側に出て（`Assert.Throws`）、ハンドラーは 1 回だけ呼ばれる。メソッド全体を try で包む誤りはこれで落ちる

### 7.3 層 2b（PlayMode・Player、`WindowsDialogSamplePlayerTests`）

- **D-01〜D-14 を期待値を変えずに通す。これが移行の検証。** 実装時に確かめる前提: コントロール ID 1148 / 1001 / 1152、上書き確認の Task Dialog、D-06 の引用符つきフルパス、カレントディレクトリの移動
- 閉じる役に照合の手順を足す。違えば `exit 3`。期待値は今の手順と同じく base64 の UTF-8 で渡すので、非 ASCII も通る
  - `expect-title <文字列>`: ダイアログ（トップレベルの窓）の題名を `GetWindowText` で読む
  - `expect-text <id> <文字列>`: 子のコントロールの文字を `SendMessage(WM_GETTEXTLENGTH / WM_GETTEXT)` で読む。閉じる役は別のプロセスで、`GetWindowText` は別のプロセスのコントロールの文字を返さないため
  - `expect-count <id> <n>`: コンボボックスの項目数を `SendMessage(CB_GETCOUNT)` で読む（フィルタの読み方の記録に使う。ファイルの種類のコンボは cmb1 = 0x470）
  - 期待値は空白を含みうるので、今の `Split(' ', 3)`（`WindowsDialogSamplePlayerTests.cs:495`）ではなく、`select` と同じく `Substring` でその手順の残り全体を取る。手順は先に `;` で分ける（`:494`）ので、**期待値に `;` は使わない**
- 追加（Manager を直接呼ぶ）:
  - J-1 / J-2 がダイアログを出さないこと。閉じる役を起動しておき、終了コード 2（`no dialog`、`WindowsDialogSamplePlayerTests.cs:397,492`）を期待するヘルパーを足す。出てしまったときに閉じられるよう、手順は `press 2`。J-1 / J-2 はエラーログを出すので、Player のテストでも `LogAssert.Expect` で受ける（予期しない `LogError` はテストの失敗になる）
  - `ntk_last_system_code` の結び付け: `WindowsDialogCApi` の同関数を直接 1 回呼び、例外なく戻ること（`InternalsVisibleTo` で呼べる）。ほかの 13 本は D-01〜D-14 と J-7 で毎回通る
  - 非 ASCII の往復: 日本語とサロゲートペアを含むフォルダとファイル（例: `ダイアログ-𠮷`）を**そのテストの中だけで**、テスト用フォルダ `_folder` の下に作る（途中で落ちても、次の SetUp の `DeleteFolder` が片付ける）。共通の SetUp（`:84-89`）に足すと、中身の完全一致を見る `AssertFolderUnchanged`（`:229-233`）で D-07 / D-08 / D-14 が落ちる。D-04 / D-10 / D-12 相当で返るパスが一致し、`File.Exists` / `Directory.Exists` が真になること
  - `def_ext`: 保存で拡張子なしの `new` を入れ、`new.txt` が返ること。`def_ext = ""` と `Text\0*.txt\0\0` で `new` を入れた結果を記録する（5.6 の手順 1 と 5）
  - フィルタの読み方: `ShowFileDialog` に `"A\0*.a\0\0B\0*.b\0\0"` を渡し、`expect-count 1136 1`（cmb1 = 0x470）で項目数を確かめてから `press 2` で閉じる。5.6 の手順 1 で 1.x のまま流して期待値を記録し、手順 5 で移行後も同じになることを確かめる
  - 題名の UTF-8: フォルダ選択の `title` に非 ASCII を渡し、`expect-title` で照合する。アラートの `title` と本文も照合する（本文は ID 65535 の Static に `expect-text`）
- 層 2b は Mono で走る。IL2CPP は J-10

### 7.4 層 3・手動

- 層 3: D-08 / D-14（保存でファイルが作られない・既存が書き換わらない）をテストがファイルを直接見る（既存）
- 手動: なし。見た目（アイコン、既定ボタンの位置、表示言語）は確かめない。題名と本文の文字は 7.3 の照合で確かめる（UI テスト計画 v2 の 1 章をこれに合わせる、5.1）

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。手動に残す項目は無い。起こせない失敗（E-3 / E-5 / E-12 の実物）は、変換を層 1 で押さえ、`ntk_last_system_code` の結び付けを層 2b で、イベントを 1 回出す構造を層 2a で確かめる。
E-9 の実物（Manager の catch が DLL の例外を `-4` にすること、版が合わないことを覚えて以後ネイティブへ渡さないこと）も起こせない。例外の変換と版判定を層 1 で、それを呼ぶ形をコードレビューで押さえる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- テスト用 Player のファイアウォールの規則（受信ブロック、既存。`artifact/topics/cross-platform-testing/README.md`）
- 前提の OS 設定: なし
- OS が求める許可: なし

### 8.3 呼び出し側を止める OS の画面

6 関数すべてがモーダルで、閉じるまで呼び出し側（メインスレッド）を止める。
現在のハーネスには外から閉じる手段がある: `WindowsDialogSamplePlayerTests` が押す前に PowerShell の閉じる役を起動し、Win32 のメッセージ
（`WM_COMMAND`、`WM_SETTEXT`、`TDM_CLICK_BUTTON`）と、ファイル一覧の選択に UI Automation を使う（`2026-09-26-windows-dialog-ui-test-plan-v2.md` 2 章。v1 から変えない）。
ダイアログが出ないことを確かめるテスト（7.3）も閉じる役を起動しておき、出てしまっても実行が止まらないようにする。
層 2a は Editor だけで走り、ネイティブへ行かないのでダイアログは出ない。
