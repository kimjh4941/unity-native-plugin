# Windows Dialog UI 自動テスト計画 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Windows Dialog のサンプル（`Runtime/UI/Windows/Dialog/WindowsDialogManagerExampleController.cs`）と `WindowsDialogManager` |
| 同梱 DLL | 1.x（`Plugins/Windows/VERSION.txt`: dist 1.11.0 / `windows-native-toolkit-1.2.0.dll`） |
| 目的 | Windows C ABI 2.0.0 への移行（`artifact/topics/windows-c-abi-2/`）の前に、今の動きを自動テストで固定する |
| 実行 | `scripts/verify_unity_windows.sh` の Player テスト（層 2b）。テストは `Tests/PlayMode/WindowsDialogSamplePlayerTests.cs` |

## 1. 何を確かめるか

**Dialog には手動確認の記録がない。** `artifact/features/dialog/` はこの計画書が最初で、
`artifact/topics/device-verification-records/README.md` にも「dialog の結果文書が 1 本も無い」とある。
残っているのはマニュアル用のスクリーンショット（`Documentation~/images/windows/Example_WindowsDialogManager_*.png`）だけ。
Clipboard のように手動確認をそのまま移すことはできないので、観点は **native-toolkit の Dialog UI テスト D-01〜D-14**
（`windows/WindowsLibraryExampleUITest/Tests/Dialog/DialogTests.cs`）を下敷きにする。
向こうのサンプルとこちらのサンプルは、同じ 6 種類のダイアログを 1 ボタンずつ出す。

期待値は **今の 1.x の DLL の動き**で書く。向こうのテストは 2.0.0 の動きを見ている（下の 4 章）。

キャンセルは、どのファイル・フォルダ系でも `(null, isCancelled=true, isSuccess=true, errorCode=-1)`。

| # | 操作 | 期待（1.x） | 状況 |
|---|---|---|---|
| D-01 | ShowDialog → OK | `AlertDialogResult(1 (IDOK), true, null)`、画面に `OK` / `ShowDialog result: 1` | **完了** |
| D-02 | ShowDialog → Cancel | `AlertDialogResult(2 (IDCANCEL), true, null)` | **完了** |
| D-03 | ShowFileDialog → キャンセル | キャンセル | **完了** |
| D-04 | ShowFileDialog → 用意したファイルを選ぶ | そのフルパス、`isCancelled=false` | **完了** |
| D-05 | ShowMultiFileDialog → キャンセル | キャンセル | **完了** |
| D-06 | ShowMultiFileDialog → 2 つ選ぶ | **フルパス 2 つ。** 1.x の DLL はフォルダ + ファイル名の並びを返すが、Manager がフルパスに組み立てて渡す | **完了** |
| D-07 | ShowSaveFileDialog → キャンセル | キャンセル。フォルダの中身は変わらない | **完了** |
| D-08 | ShowSaveFileDialog → 新しい名前 | そのパス。**ファイルは作られない**（ダイアログはパスを返すだけ） | **完了** |
| D-09 | ShowFolderDialog → キャンセル | キャンセル | **完了** |
| D-10 | ShowFolderDialog → 用意したフォルダを選ぶ | そのパス | **完了** |
| D-11 | ShowMultiFolderDialog → キャンセル | キャンセル（0 件ではなく `-1`） | **完了** |
| D-12 | ShowMultiFolderDialog → 2 つ選ぶ | 2 つのフルパス | **完了** |
| D-13 | Home で戻る | トップメニューに戻り、もう一度開いた画面でアラートが動く | **完了** |
| D-14 | ShowSaveFileDialog → 既存ファイル → 上書き確認で「はい」 | そのパス。**1.x は常に上書き確認を出す**。既存ファイルは書き換えられない | **完了** |

実行結果（2026-09-26、`verify_unity_windows.sh --skip-build`）: Player 31/31（既定の実行）、14 本すべて合格。1 本 2〜14 秒。

確かめないもの:

- 見た目（アイコン、既定ボタンが 2 番目か、タイトル・メッセージの表示）。ダイアログの操作と結果だけを見る
- ファイル系ダイアログのタイトルと owner。1.x では効かない（`windows-c-abi-2` README 5.4）。2.0.0 で効くようになったら足す
- 貼り付け先アプリのような層 3 の見え方は Dialog には無い。D-08 / D-14 の「ファイルが作られない / 変わらない」は、テストがファイルを直接見る

## 2. 自動化の前提

**ダイアログを出す呼び出しは、ダイアログが閉じるまで戻らない。** `WindowsDialogManager` の `ShowX()` は、ボタンの `clicked` から
Unity のメインスレッドでネイティブを直接呼ぶ（`WindowsDialogManager.cs:47-48` のコメント）。テストがボタンを押すと、テスト自身も止まる。
そこで、**押す前に、ダイアログを閉じる役の別プロセスを起動しておく。**

- 閉じる役は Windows PowerShell 5.1 のスクリプトで、テストに文字列として埋め込み、`Application.temporaryCachePath` に書き出して起動する。
  インストールは要らない（Win32 の呼び出しは `Add-Type` でその場でコンパイルする）
- ダイアログは、このプロセスの、表示されているトップレベルの `#32770` ウィンドウとして `EnumWindows` で探す（native-toolkit と同じ）。
  1.x はダイアログに owner を渡さないので、所有者なしのトップレベルになる
- コントロールは **コントロール ID** で探す。表示言語に左右されない
- ボタンは、クリックしたときと同じ `WM_COMMAND`（`BN_CLICKED`）をボタンの親に送って押す
- テストは、閉じる役が「ready」を出すまで押さない。閉じる役は、目的のコントロールを押せないときダイアログに `WM_CLOSE` を送ってから失敗で終わる。
  **どちらが失敗しても、実行がダイアログで止まったままにならない**

**ボタンと入力欄には UI Automation を使わない**（ファイル一覧の項目の選択にだけ使う。下の表）。最初に PowerShell の UI Automation（.NET の managed クライアント）で試したが、
メッセージボックスのボタンが Invoke パターンの無い Pane として見え、押せなかった。クライアント側プロバイダの登録も例外で失敗した（2026-09-26）。
native-toolkit の FlaUI（COM の UIA3）では押せているので、差は managed クライアントの側にある。
これは testing.md の「Windows の外部ツールは Appium + appium-windows-driver」とも別の選択で、testing.md に追記する。

### 各ダイアログの操作（2026-09-26 に確かめた）

1.x の DLL は、開く・複数ファイル・保存に `GetOpenFileNameW` / `GetSaveFileNameW`、フォルダに `IFileOpenDialog`（`FOS_PICKFOLDERS`）を使う
（native-toolkit のタグ `1.11.0` の `windows/WindowsLibrary/WindowsDialogManager.cpp`）。どれも新しい形のダイアログで出る。
PowerShell から同じ呼び出しでダイアログを出し、子ウィンドウを列挙して確かめた。

| ダイアログ | 入力欄 | 決定 / キャンセル | 操作 |
|---|---|---|---|
| 開く・複数ファイル | `Edit` 1148（同じ ID の ComboBox の中） | Button 1 / 2 | パスを書いて決定。複数はフルパスを `"A" "B"` と並べる |
| 保存 | `Edit` 1001 | Button 1 / 2 | パスを書いて決定。既存なら上書き確認（Task Dialog）に `TDM_CLICK_BUTTON` で `IDYES`（6） |
| フォルダ・複数フォルダ | `Edit` 1152 | Button 1（フォルダーの選択）/ 2 | 下のとおり |

- **保存ダイアログのアドレスバー（`ToolbarWindow32`）も ID 1001 を持つ。** 入力欄は ID とクラス（`Edit`）で探す
- 入力欄は `WM_SETTEXT` で書け、ダイアログはそれを入力として受け取る
- **フォルダ選択は、パスを書いて決定するとそのフォルダの中へ移動する。** 1 つ選ぶときは、移動した後に入力欄を空にしてもう一度決定する
- **1.x のフォルダ選択は、並べて書いた複数のフォルダを受け付けない**（「f1 フォルダー名は有効ではありません」）。native-toolkit の D-12 はこの書き方で通っているが、
  2.0.0 のダイアログでのこと。2 つ選ぶときは親へ移動し、一覧の f1 と f2 を選択してから決定する。一覧の項目は DirectUI で、自分で UI Automation に答えるので、
  PowerShell の UI Automation でも `SelectionItemPattern.AddToSelection` が効く（Win32 のボタンは押せない、2 章の上）
- どのダイアログも `WM_CLOSE` はキャンセルとして扱う。閉じる役が失敗したときの後始末はこれで足りる
- 引数で渡すと Windows PowerShell が引用符を落とすので、手順は Base64 で渡す

### 実装で踏んだもの

- **Editor のコンパイルで CS1024。** テストのファイルは Player 向けの `#if` の中にあり、Editor ではその区間が読み飛ばされる。
  読み飛ばされる区間では、文字列の中でも行頭が `#` の行がプリプロセッサ指令と見なされる。埋め込んだ PowerShell のコメントを `<# ... #>` にした
- 1.x のファイルダイアログはプロセスのカレントディレクトリを動かしうるので、テストごとに元へ戻す

未確認:

- ダイアログが開いている間、Player から Editor への生存通知が止まるか。既定のタイムアウトは 600 秒で、ダイアログは長くても 14 秒で閉じたので問題になっていない

## 3. テスト用のファイル

- `%TEMP%\ntk-unity-dialog-test` に a.txt / b.txt と フォルダ f1 / f2 を作り、終わったら消す
- ファイルダイアログはアプリのカレントディレクトリをその場所へ移すことがある（native-toolkit で観測）。消すときは再試行する

## 4. 2.0.0 への移行で変わる期待値

`artifact/topics/windows-c-abi-2/README.md` 5.4 のとおり。テストの期待値もそこに合わせて書き換える。

- キャンセルは `-1` ではなく `CANCELED`
- 複数ファイルはフォルダ + 名前ではなくフルパス
- 複数フォルダは「成功したが未選択」とキャンセルが区別できる
- 保存の上書き確認を止められる（`skip_overwrite_prompt`）
- ファイル系のタイトルと owner が効く

## 5. 見つかった問題（自動化とは別件）

`WindowsDialogManager.ShowDialog` は、タイトルかメッセージが空のときエラーのイベントを出したあと `return` せず、ネイティブの呼び出しまで進む
（`WindowsDialogManager.cs:218-228`）。移行でマネージャーを書き直すときに直す。
