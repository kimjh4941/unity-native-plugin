# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/features/dialog/designs/2026-09-27-windows-dialog-design-v1.md
- 機能名: dialog
- プラットフォーム: Windows
- レビュー: 3 本を並行で行い、指摘を重ねて整理した。サブエージェント 2 本（観点を分担: ネイティブ境界 / 公開契約・テスト・自動化の前提）と Codex（全観点）。どれも読み取りのみ
- 出典の略記: [N] = ネイティブ境界、[T] = 契約・テスト、[X] = Codex。複数が挙げたものは併記した

---

## 強み

- C ABI の事実関係は正確だった。構造体の大きさ（56 / 32 / 40 / 24）と `#pragma pack(8)` の配置、エラー値、NULL リクエストの意味、上限（1023 / 32768）、結果を Win32 ID に戻す 11 値の対応（名前で対応させているので、列挙の番号のずれも吸収できている）[N][T][X]
- **D-01〜D-14 が期待値を変えずに通る見込みには根拠がある。** 2.0.0 の Data 層は 1.x の `WindowsDialogManager.cpp` を移したもので、同じ `MessageBoxW` / `GetOpenFileNameW` / `GetSaveFileNameW` / `IFileOpenDialog` を同じフラグで呼ぶ。本計画の固定値（owner = NULL、ファイル系の title = NULL、`allow_missing_file = 0`、`skip_overwrite_prompt = 0`）でそこに到達する [N][T][X]
- `SYSTEM_ERROR` の生の値は 1.x の errorCode と同じ種類の値（CommDlgExtendedError / HRESULT / GetLastError）。`ntk_last_system_code` は `thread_local` で、`ntk_string_free` はそれに触れない [N][T]
- J-6 の「2 本の DLL は衝突しない」は正しい。export の集合が交わらず（1.x 56 本、capi 105 本の `ntk_*`）、どちらも使う `Microsoft.WindowsAppRuntime.Bootstrap.dll` は両方の関数を export している [N][X]
- Manager → `WindowsDialogCApi` → ネイティブの依存方向と、純粋な変換（Mapping）の分離は Manager + Bridge に合う。新規ファイル名は `Windows` 接頭辞（P1〜P4 は満たす）。`InternalsVisibleTo` は既にある [T][X]
- 「自動化の前提」の章があり、モーダル 6 本と、外から閉じる手段（閉じる役）、出てはいけないダイアログが出たときに止まらない配慮まで書かれている [T][X]

## 改善点

### 高優先度

**A1（この通り実装すると契約が変わり、しかもコンパイルもテストも通る。直して再レビュー）**

1. **`MB_*` を引数ごとに変換すると、1.x の「4 引数の OR」と合わない** [N][T]（4.1、J-2）
   - 1.x のネイティブは `buttons | icon | defbutton | options` を 1 つにして `MessageBoxW` に渡していた（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsDialogManager.cpp:63-64`）。`ShowDialog(t, m, MB_YESNO | MB_ICONWARNING, 0, …)` のように、1 つの引数に別のグループのフラグを入れても効いた
   - 計画は引数ごとに表で引くので、これが J-2 の範囲外でエラーになる。サンプルは正しい引数に入れているので、テストでは見つからない
   - `MB_HELP`（0x4000）は 2.0.0 に対応先（`show_help_button`）があるのに、「未定義のビット」として拒否される。4.2 の HELP→9 にも到達しない
   - 提案: null を既定値に置き換えてから 4 引数を OR し、Win32 のマスクで分ける（`0x0F` ボタン、`0xF0` アイコン、`0xF00` 既定ボタン、`MB_TOPMOST`、`MB_HELP`、残り → J-2）。層 1 に「別の引数に入れたフラグ」と `MB_HELP` を足す。HELP ボタンは閉じずに `WM_HELP` を送るだけなので、結果として返らないことも書く
2. **J-3 の「OS の値は正、ライブラリの判断は負」は成り立たない** [N][T][X]（J-3、E-3）
   - `ntk_last_system_code()` は `uint32_t`。フォルダ選択の失敗は HRESULT（例: `0x80070005`、`E_FAIL`）で、`int` にすると負になる。1.x の `out int pError` にも負の値が入っていた
   - 実際に -1〜-4 と衝突はしない（`0xFFFFFFFF` はネイティブがキャンセルとして先に除き、`0xFFFFFFFC`〜`0xFFFFFFFE` は定義された HRESULT ではない）。しかし公開クラスのコメントに「負ならライブラリ」と書くと、利用者が HRESULT の失敗を取り違える
   - 提案: 負の定数は保つ。説明を「-1〜-4 は予約値で、OS は返さない。判定は符号ではなく定数との比較で行う。OS の値は HRESULT なら負にもなる」に改める。変換は `unchecked((int)code)` と書く
3. **コンパイルの二重ガード（P5）になっていない** [N][X]（5.1）
   - `WindowsDialogCApi.cs` 全体を `#if UNITY_STANDALONE_WIN` に置く計画で、`DllImport` が Windows ターゲットの Editor にもコンパイルされる。規則はクラスを `X || UNITY_EDITOR`、P/Invoke を `X && !UNITY_EDITOR` とする二重構造を求め、前例の Clipboard もその形
   - 実害は小さい（Editor では DLL が無効で `DllNotFoundException` → J-7 で -4）が、規則上は A1
   - 提案: 型・構造体・純粋な変換は `UNITY_STANDALONE_WIN || UNITY_EDITOR`、`DllImport` と実際の呼び出しだけを `UNITY_STANDALONE_WIN && !UNITY_EDITOR` に置き、Editor 側は -4 を返す。[T] は「Player に他 OS のコードは入らないので違反ではない」としたが、規則に合わせる方を採る
4. **「例外を外に出さない」をそのまま実装すると、購読者の例外を飲み込み、イベントが 2 回出うる** [T]（5.4）
   - メソッド全体を try/catch で包むと、購読者が投げた例外を捕まえてエラーのイベントを出し、「1 呼び出し 1 回」が崩れる。1.x は購読者の例外を呼び出し側へ伝えていた
   - 提案: 捕まえるのは「ネイティブ呼び出しと変換」だけ。イベントは catch の外で 1 回だけ出し、購読者の例外は 1.x と同じく伝える。それ以外の例外（`OutOfMemoryException`、`BadImageFormatException` など）は -3 とし、6 章に行を足す
5. **`buffer_size` を無視すると、公開 API の振る舞いが静かに変わる** [X]（high）、[N] A1、[T] C（4.1、基本情報）
   - 1.x はこの値を実際のバッファの大きさとして使っていた。小さい値を渡していた呼び出しは、移行後だけ長いパスで成功するようになる。1024 を超える値を渡して長いパスを受けていた呼び出しは、2.0.0 の上限（1023 文字）で失敗するようになる
   - サンプルは 1024 / 4096 固定なので、D-01〜D-14 では見つからない
   - **判断が要る。** 案 A: 1.x の上限を C# 側で再現する（超えたら 1.x 相当のエラー）。ただし 1024 文字を超える長いパスは 2.0.0 が返さないので、完全には再現できない。案 B: 意図した差分として受け入れ、XML コメントとマニュアルに書く
6. **書かれていない振る舞いの差がある** [N] A1、[T] C、[X] B
   - 複数ファイルをドライブ直下で選んだとき、1.x の C# は `C:\\a.txt` を作っていた。2.0.0 は `C:\a.txt`
   - フォルダの title に `""` を渡したとき、1.x は空の題名、2.0.0 はシステムの題名
   - フィルタに `""` を渡したとき、1.x は空のフィルタ、2.0.0 は 0 件で全ファイル（J-4 の対象かが未定）
   - アラートがネイティブで失敗したときの `result`。2.0.0 は成功時にしか `out_result` を書かない。1.x は 0 だった
   - 対になっていないサロゲートは U+FFFD になる
   - 提案: 「既知の差分（意図したもの）」の節を足し、XML コメントとマニュアルに書く内容を決める。アラートの失敗は 1.x と同じ `result = 0` にする
7. **同梱する 2.0.0 の DLL が変更ファイル一覧に無い** [X]（high）、[N][T] 不足項目
   - `VERSION.txt` は「ピンとバイナリを同じコミットで動かす」契約。一覧に無いと、dist がある開発機ではビルドもテストも通るのに、配布物に DLL が入らないことがありうる
   - 提案: `Plugins/Windows/windows-native-toolkit-capi-2.0.0.dll` を新規作成として 5.1 に載せる

### 中優先度

**A2（実装やテストで必ず表に出る。実装結果ファイルにチェックリストとして転記する）**

- **構造体・列挙・定数を置くファイルのガードが、層 1 のテストと合わない** [N][T][X]。構造体を `UNITY_STANDALONE_WIN` だけのファイルに置くと、Windows 以外のターゲットの Editor でテストアセンブリ全体がコンパイルに失敗し、他 OS の EditMode テストも止まる。A1-3 の直しで二重ガードにすれば解消する
- **`WindowsDialogErrorCodes.cs` が 5.1 に無く、ガードも決まっていない** [N][T][X]。Mapping（`|| UNITY_EDITOR`）が参照するので、同じガードにする。既存の `WindowsClipboardErrorCode`（enum）と形が違う理由（`int?` に入れるため定数）も一言書く
- **J-6 の中身が PreBuildProcessor の実際の動きに足りない** [N][T][X]
  - 今の処理は、destName 以外の `unity-windows-native-toolkit*.dll` と `windows-native-toolkit*.dll` を `.meta` ごとすべて消す。2 本目のコピーが 1 本目を消す
  - Clipboard / Notification は development ビルドで `-debug` の名前を読むので、追加の DLL にも debug 名の規則が要る
  - importer の設定（Editor 無効、Win64 のみ）を追加の DLL にも当てる
  - 確かめ方: Player の `Plugins/x86_64` に 2 本あり、md5 が dist と一致すること。Dialog に加えて Clipboard / Notification の Player テストも通すこと
  - `VERSION.txt` のコメント（3 つの Manager の `DLL_NAME` と一致させる）と、移行の README 2.5 の手順を J-6 に合わせて直す
- **J-7 の「上位（major）」の読み方** [N]。`NTK_VERSION` は `(MAJOR<<16)|(MINOR<<8)|PATCH`。最上位バイトと読むと 0 になり、すべての呼び出しが -4 になる。`(ntk_version() >> 16) == 2` と書く

**B（検証手段の穴）**

- **非 ASCII の文字列を確かめるテストが無い** [N][T][X]。fixture もアラートの文字列も ASCII だけなので、UTF-16 から UTF-8 への変更が壊れても全テストが通る。fixture に非 ASCII（日本語、サロゲートペア）のフォルダとファイルを足し、D-04 / D-10 / D-12 相当で往復を確かめる。アラートやフォルダ選択の題名は、閉じる役が `GetWindowText` で読んで照合する。`def_ext` を試す「`new` と入れて `new.txt` が返る」も足す
- **構造体のフィールドの並びと、`size_t` の型が決まっていない** [N][X]。大きさだけではフィールドの入れ替えを見つけられない（サンプルのアラートは buttons / icon / default_button がどれも 1）。`Marshal.OffsetOf` で全フィールドの位置を確かめる（native-toolkit の `WindowsLibraryCApiTest/Common/CApiLayoutTest.cpp` の表を期待値にできる）。`size_t` はすべて `UIntPtr`、構造体は `Pack = 8`、各 `DllImport` の署名を設計に列挙する
- **IL2CPP で一度も動かない** [N][X]。Windows の scripting backend は既定の Mono（IL2CPP は Android だけ）。規則上、Mono で通っても IL2CPP の保証にならない。`DllImport` の名前 `windows-native-toolkit-capi-2.0.0` は `.` を含み、IL2CPP の Windows 版が `.dll` を補うかは未確認 → 名前は `.dll` まで書く。IL2CPP の Player で 1 回流すか、「層 2b は Mono、IL2CPP は未検証」と明記する
- **エラーケースに検証の層が割り当てられていない** [T][X]。6 章の「層」は発生元で、検証の層ではない。E-3〜E-5（変換関数で層 1）、E-9 / J-7（版判定を純粋関数にして層 1、DLL が無い経路は層 2a）、E-10 / J-5（層 1）など、エラーごとに層を書く
- **層 2a で安く確かめられる項目を逃している** [T]。B 群は「Windows ターゲットのときは Editor でもコンパイルされる」（`verify_unity_windows.sh` は EditMode を Win64 で回す）。引数の検査を版確認より前に置けば、J-1 / J-2 / J-4（ダイアログを出さず -2 が 1 回）と J-7（-4）を、閉じる役なしで Editor 内で確かめられる。呼び出し順を「引数の検査 → J-7 → ネイティブ」と書く
- **`allow_missing_file` の取り違えを捕まえるテストが無い** [T]。`skip_overwrite_prompt` は D-14 が捕まえるが、`allow_missing_file = 1` にしても D-04 は通る。リクエスト構造体の組み立てを純粋関数にして層 1 で各欄を固定する
- **J-1 / J-2 を層 2b で試すハーネスが書かれていない** [N][T]。サンプルからは起こせないので Manager を直接呼ぶ。閉じる役は「ダイアログが無い」と終了コード 2 で終わるので、それを期待するヘルパーを足し、出てしまったときに閉じられるよう手順は `press 2` にする
- **借用ポインタと、確保の途中での失敗** [X]。`ntk_string_list_at` の要素は一覧を解放するまでだけ有効で、個別に解放してはいけない（一覧を解放する前に managed の文字列へ写す）。フィルタ配列の各文字列の確保の途中で失敗したときの解放を決める
- **`UNKNOWN` のときの system code** [N][X]。メモリ不足のとき C ABI は `UNKNOWN` と `0x8007000E` を返す。-3 のときは system code をログに出す
- **D-01〜D-14 が通る根拠と、実装時に確かめる前提** [T]。根拠（同じ Win32 呼び出しとフラグ）を 1 行書き、実装時に確かめる前提（コントロール ID 1148 / 1001 / 1152、上書き確認の Task Dialog、D-06 の引用符つきフルパス、カレントディレクトリの移動）を並べる。D-01 / D-02 は変換の誤り（列挙に 1 を足すだけ、など）を見つけられないので、変換は層 1 で押さえる

### 低優先度

**C（記述の整合）**

- 7 章の「Manager は `UNITY_EDITOR` でコンパイルされない」は不正確（Windows ターゲットの Editor ではされる）[N][T]
- J-1 の「2.0.0 では空のダイアログが出る」は 1.x でも同じだった（`MessageBoxW` は空文字を受ける）[N]
- J-3 の -2 の説明に J-1（E-6）が入っていない。J-1 で errorCode が 1.x の null から -2 に変わることも書く [N][T]
- J-5 の「1.x では起きず」は誤り。1.x の C# も、errorCode が 0 で件数 0 なら空の `ArrayList` を返していた [N]
- native が `INVALID_PARAMETER` を返すのは、実質的に C# 側の構造体の誤り（struct_size の不一致、reserved が 0 でない）のとき。E-4 のログに「実装の誤りの可能性」と添える [N]
- 4.1 の「（E-2）」は native-toolkit 側の決定番号で、6 章の E-2 と紛らわしい。「NTK E-2」と書く [T]
- UI テスト計画の 4 章（2.0.0 で期待値を書き換える）が本計画と逆。移行の README 2.3（`NTK_VERSION` との完全一致）・3.3（`SafeHandle`）と本計画の違いの理由が無い。UI テストのコメントの「1.x」の記述。これらを直す対象として 5.1 に加える [T]

## 不足項目

- 5.1 の変更ファイル一覧: `Runtime/Dialog/WindowsDialogErrorCodes.cs`（新規作成）、`Plugins/Windows/windows-native-toolkit-capi-2.0.0.dll`（新規作成）、UI テスト計画の 4 章、移行の README（2.3 / 2.5 / 3.3 / 5.4）、`Plugins/Windows/VERSION.txt` のコメント
- 非変更として `Editor/UI/NativeToolkitEditorWindow.cs`（移行の README 2.4 が波及先に挙げる。参照しているのはサンプルのコントローラーだけ）[T]
- マニュアル（`manual/<次の版>/dialog.md`）への引き継ぎ。1.11.0 は `bufferSize` を使う例を載せている。-2 / -3 / -4、使えなくなった `MB_*`、既知の差分を write-manual に渡すと書く [T]
- `PostBuildProcessor` の PDB のパス（移行の README 2.5 で「移行時に一緒に直す」）を、Dialog の段階で扱うか最後の機能で扱うか [T]
- IL2CPP については「コールバックが無いので `MonoPInvokeCallback` は要らない」の一文 [T]
- 別機能との干渉: Notification を 2.0.0 に移すと、`ntk_notification_manager_create` が呼び出したスレッドを MTA にする（移行の README 3.2）。フォルダ選択は STA を前提にしている。Notification を移したあとで D-09〜D-12 を流し直すことを 5.5 に書く [N][T]

## 総合評価

方針（公開 API を保ち、D-01〜D-14 をそのまま移行の検証にする）と、C ABI の事実関係は正確で、J-1〜J-7 の判断そのものにも誤りは無い。

一方で、**A1 が 7 件**ある（3 本の重なりを除いた数）。`MB_*` の OR の意味と `MB_HELP`、J-3 の符号による区別、P5 の二重ガード、例外の飲み込み、`buffer_size` の扱い、書かれていない振る舞いの差、DLL の記載漏れ。このうち `buffer_size` は案 A / 案 B の判断が要る。
A2 はガードの置き場所、`WindowsDialogErrorCodes` の記載、J-6 の中身、J-7 の読み方の 4 件。
B では、非 ASCII・フィールドの並び・IL2CPP の 3 つが、今のテストでは誤りを見つけられない大きな穴になっている。

**このまま実装に入ることは勧めない。** A1 を直して再レビューする。
