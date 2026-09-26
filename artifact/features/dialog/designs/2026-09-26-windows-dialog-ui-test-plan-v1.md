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

| # | 操作 | 期待（1.x） | 状況 |
|---|---|---|---|
| D-01 | ShowDialog → OK | `AlertDialogResult(1 (IDOK), true, null)`、画面に `OK` / `ShowDialog result: 1` | **完了** |
| D-02 | ShowDialog → Cancel | `AlertDialogResult(2 (IDCANCEL), true, null)` | **完了** |
| D-03 | ShowFileDialog → キャンセル | `FileDialogResult(null, isCancelled=true, isSuccess=true, -1)` | 未 |
| D-04 | ShowFileDialog → 用意したファイルを選ぶ | そのフルパス、`isCancelled=false` | 未 |
| D-05 | ShowMultiFileDialog → キャンセル | キャンセル | 未 |
| D-06 | ShowMultiFileDialog → 2 つ選ぶ | **フォルダ + ファイル名 2 つの並び**（1.x。2.0.0 はフルパス 2 つ） | 未 |
| D-07 | ShowSaveFileDialog → キャンセル | キャンセル。ファイルは作られない | 未 |
| D-08 | ShowSaveFileDialog → 新しい名前 | そのパス。**ファイルは作られない**（ダイアログはパスを返すだけ） | 未 |
| D-09 | ShowFolderDialog → キャンセル | キャンセル | 未 |
| D-10 | ShowFolderDialog → 用意したフォルダを選ぶ | そのパス | 未 |
| D-11 | ShowMultiFolderDialog → キャンセル | 1.x は「0 件」か `-1`（どちらになるかは実装時に確かめて書く） | 未 |
| D-12 | ShowMultiFolderDialog → 2 つ選ぶ | 2 つのパス | 未 |
| D-13 | Home で戻る | トップメニューに戻り、画面の Controller が破棄される | 未 |
| D-14 | ShowSaveFileDialog → 既存ファイル → 上書き確認で「はい」 | そのパス。**1.x は常に上書き確認を出す** | 未 |

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

**UI Automation は使わない。** 最初に PowerShell の UI Automation（.NET の managed クライアント）で試したが、
メッセージボックスのボタンが Invoke パターンの無い Pane として見え、押せなかった。クライアント側プロバイダの登録も例外で失敗した（2026-09-26）。
native-toolkit の FlaUI（COM の UIA3）では押せているので、差は managed クライアントの側にある。
これは testing.md の「Windows の外部ツールは Appium + appium-windows-driver」とも別の選択で、testing.md に追記する。

未確認:

- ファイル系ダイアログのコントロール ID。native-toolkit の ID（開く 1148、保存 1001、フォルダ 1152、決定 1、キャンセル 2）は 2.0.0 の IFileDialog のもの。
  1.x が同じダイアログを使っているかは、実装時に子ウィンドウを列挙して確かめる
- 入力欄への書き込み（`WM_SETTEXT`）で、ダイアログが入力として受け取るか
- 上書き確認（Task Dialog）は `TDM_CLICK_BUTTON` で `IDYES` を送る。効くかは D-14 の実装時に確かめる
- ダイアログが開いている間、Player から Editor への生存通知が止まるか。既定のタイムアウトは 600 秒で、ダイアログは数秒で閉じるので問題にならない見込み

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
