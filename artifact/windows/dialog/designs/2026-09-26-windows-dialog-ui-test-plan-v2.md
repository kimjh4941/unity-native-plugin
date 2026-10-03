# Windows Dialog UI 自動テスト計画 v2

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Windows Dialog のサンプル（`Runtime/UI/Windows/Dialog/WindowsDialogManagerExampleController.cs`）と `WindowsDialogManager` |
| 同梱 DLL | C ABI 2.0.0（`Plugins/Windows/VERSION.txt`: dist 1.12.0 / `windows-native-toolkit-capi-2.0.0.dll`）。移行の間は 1.x（dist 1.11.0）も追加のピンで置く（設計 v6 の J-6） |
| 目的 | Windows C ABI 2.0.0 への移行（`artifact/topics/windows-c-abi-2/`）で、D-01〜D-14 が期待値を変えずに通ることを確かめ、移行で足した振る舞いを固定する |
| 実行 | `scripts/verify_unity_windows.sh` の Player テスト（層 2b）。テストは `Tests/PlayMode/WindowsDialogSamplePlayerTests.cs` |
| 設計 | `artifact/windows/dialog/designs/2026-09-27-windows-dialog-design-v6.md`（7.3 が層 2b） |

## v1 からの主な変更

- 1 章: 期待値は 1.x で取ったもので、2.0.0 への移行でも変えない（v1 の「期待値は 1.x」を、移行の検証として読み替える）
- 1 章: 「タイトル・メッセージの表示は確かめない」を外した。7.3 の `expect-title` / `expect-text` で確かめる
- 1 章: 「ファイル系のタイトルと owner は 2.0.0 で効いたら足す」を外した。移行後もファイル系のタイトルは NULL、owner は NULL で固定し、使わない（設計 v6 の 4.2）
- 1.1 章を新設: 移行の前後で記録したテストと、7.3 で足したテスト
- 2 章: 閉じる役の手順に `expect-count` / `expect-title` / `expect-text` を足した。「ダイアログが出ないこと」を確かめるヘルパーを足した
- 4 章: 「2.0.0 への移行で変わる期待値」を「UI テストの期待値は変えない」に改めた（v1 の 4 章の項目は、Manager が公開 API の形に戻すので、イベントには出ない）
- 5 章: v1 で見つけた `ShowDialog` の問題は、移行で直した（J-1）

## 1. 何を確かめるか

観点は v1 と同じく **native-toolkit の Dialog UI テスト D-01〜D-14**（`windows/WindowsLibraryExampleUITest/Tests/Dialog/DialogTests.cs`）を下敷きにする。

**期待値は 1.x の DLL で取ったもので、2.0.0 への移行でも変えない。** これが移行の検証になる。

キャンセルは、どのファイル・フォルダ系でも `(null, isCancelled=true, isSuccess=true, errorCode=-1)`（`WindowsDialogErrorCodes.Cancelled`）。

| # | 操作 | 期待 | 1.x（2026-09-26） | 2.0.0（2026-09-27） |
|---|---|---|---|---|
| D-01 | ShowDialog → OK | `AlertDialogResult(1 (IDOK), true, null)`、画面に `OK` / `ShowDialog result: 1` | 合格 | 合格 |
| D-02 | ShowDialog → Cancel | `AlertDialogResult(2 (IDCANCEL), true, null)` | 合格 | 合格 |
| D-03 | ShowFileDialog → キャンセル | キャンセル | 合格 | 合格 |
| D-04 | ShowFileDialog → 用意したファイルを選ぶ | そのフルパス、`isCancelled=false` | 合格 | 合格 |
| D-05 | ShowMultiFileDialog → キャンセル | キャンセル | 合格 | 合格 |
| D-06 | ShowMultiFileDialog → 2 つ選ぶ | フルパス 2 つ。1.x は Manager が組み立て、2.0.0 はネイティブがフルパスで返す | 合格 | 合格 |
| D-07 | ShowSaveFileDialog → キャンセル | キャンセル。フォルダの中身は変わらない | 合格 | 合格 |
| D-08 | ShowSaveFileDialog → 新しい名前 | そのパス。ファイルは作られない | 合格 | 合格 |
| D-09 | ShowFolderDialog → キャンセル | キャンセル | 合格 | 合格 |
| D-10 | ShowFolderDialog → 用意したフォルダを選ぶ | そのパス | 合格 | 合格 |
| D-11 | ShowMultiFolderDialog → キャンセル | キャンセル（0 件ではなく `-1`） | 合格 | 合格 |
| D-12 | ShowMultiFolderDialog → 2 つ選ぶ | 2 つのフルパス | 合格 | 合格 |
| D-13 | Home で戻る | トップメニューに戻り、もう一度開いた画面でアラートが動く | 合格 | 合格 |
| D-14 | ShowSaveFileDialog → 既存ファイル → 上書き確認で「はい」 | そのパス。常に上書き確認を出す（`skip_overwrite_prompt = 0`）。既存ファイルは書き換えられない | 合格 | 合格 |

### 1.1 Manager を直接呼ぶテスト（設計 v6 の 5.6 と 7.3）

| テスト | 確かめること | 1.x | 2.0.0 |
|---|---|---|---|
| `ShowSaveFileDialog_EmptyDefaultExtension_ReportsTheTypedNameAsIs` | `def_ext = ""`、フィルタ `Text\0*.txt\0\0` で `new` を入れる | `new.txt`（Windows が選んだフィルタの拡張子を足した） | **`new`**。意図した差分として受け入れた（設計 v6 5.3。確定した結果は実装結果ファイルに書く） |
| `ShowFileDialog_FilterEndsAtTheFirstEmptyName` | `"A\0*.a\0\0B\0*.b\0\0"` でファイルの種類が 1 つ（`expect-count 1136 1`）。そのあとキャンセル | 1 つ | 1 つ |
| `TheCApi_IsAvailable_AndLastSystemCodeBinds` | 版の確認が通る（major 2）。`ntk_last_system_code` が結び付く | — | 合格 |
| `ShowDialog_EmptyTitle_ShowsNothing_AndReportsInvalidArgument` | J-1。ダイアログが出ず `(null, false, -2)` | — | 合格 |
| `ShowDialog_SystemModal_ShowsNothing_AndReportsInvalidArgument` | J-2。同上 | — | 合格 |
| `ShowFileDialog_AFilterWithoutAPattern_ShowsNothing_AndReportsInvalidArgument` | J-4。ダイアログが出ず `(null, false, false, -2)` | — | 合格 |
| `ShowDialog_NonAsciiTitleAndMessage_AreShown` | 題名と本文（ID 65535 の Static）に日本語とサロゲートペアが出る。ボタンは OK と Cancel（2 章の「実装で踏んだもの」） | — | 合格 |
| `ShowFolderDialog_NonAsciiTitle_IsShown` | フォルダ選択の題名に同じ文字が出る | — | 合格 |
| `ShowFileDialog_NonAsciiPath_RoundTrips` | D-04 を `ダイアログ-𠮷.txt` で | — | 合格 |
| `ShowFolderDialog_NonAsciiPath_RoundTrips` | D-10 を `ダイアログ-𠮷` で | — | 合格 |
| `ShowMultiFolderDialog_NonAsciiPaths_RoundTrip` | D-12 を `ダイアログ-𠮷` と `ダイアログ-𠮷-2` で | — | 合格 |
| `ShowSaveFileDialog_DefaultExtension_IsAddedToTheTypedName` | 既定の `def_ext`（`txt`）で `new` → `new.txt` | — | 合格 |

非 ASCII のフォルダとファイルは、そのテストの中だけで作る。共通の SetUp に足すと、中身の完全一致を見る D-07 / D-08 / D-14 が落ちる。

確かめないもの:

- 見た目（アイコン、既定ボタンが何番目か）
- ファイル系ダイアログのタイトルと owner。移行後も NULL で固定し、使わない
- 貼り付け先アプリのような層 3 の見え方は Dialog には無い

## 2. 自動化の前提

v1 の 2 章のとおり（閉じる役の別プロセス、`EnumWindows` での探索、コントロール ID、`WM_COMMAND`、`WM_CLOSE` の後始末）。2.0.0 のダイアログでも同じ ID で動いた。

閉じる役に足した手順:

| 手順 | 読み方 | 使うテスト |
|---|---|---|
| `expect-count <id> <n>` | コンボボックスの項目数（`CB_GETCOUNT`）。一覧が埋まるまで待つ | フィルタの読み方 |
| `expect-title <文字列>` | ダイアログの題名（`GetWindowText`） | 非 ASCII の題名 |
| `expect-text <id> <文字列>` | 子のコントロールの文字（`WM_GETTEXTLENGTH` / `WM_GETTEXT`。`GetWindowText` は別のプロセスのコントロールの文字を返さない） | アラートの本文 |

- 期待値は手順の残り全体（空白を含む）。手順は先に `;` で分けるので、期待値に `;` は使わない
- 照合は大文字・小文字を区別する（PowerShell の `-cne`）
- 期待値は、ほかの手順と同じく base64 の UTF-8 で渡すので、非 ASCII も通る

**ダイアログが出ないこと**は、閉じる役を起動してから呼び、閉じる役が終了コード 2（`no dialog`、既定の 10 秒待つ）で終わることで確かめる。出てしまったときは、閉じる役の `press 2` か `WM_CLOSE` で閉じる。1 本およそ 11 秒。

### 実装で踏んだもの

- **`MB_OK` だけのメッセージボックスでは、OK ボタンのコントロール ID が 2（IDCANCEL）になる。** 押したときの戻り値は IDOK（1）。`press 1` がボタンを見つけられなかった。
  サンプルのアラートは OK と Cancel なので D-01 / D-02 には出ない。非 ASCII のアラートのテストは `MB_OKCANCEL` にした

## 3. テスト用のファイル

v1 と同じ（`%TEMP%\ntk-unity-dialog-test` に a.txt / b.txt と f1 / f2）。非 ASCII の名前は 1.1 章のとおり、そのテストの中だけで作る。

## 4. UI テストの期待値は変えない

v1 の 4 章に挙げた 2.0.0 の違いは、Manager が公開 API の形に戻すので、UI テストのイベントには出ない。

| v1 の 4 章の項目 | 移行後 |
|---|---|
| キャンセルは `CANCELED` | Manager が `-1` に戻す |
| 複数ファイルはフルパス | 公開 API も 1.x からフルパス。変わらない |
| 複数フォルダの「成功・未選択」とキャンセル | キャンセルは `-1`、0 件は空の一覧で成功（設計 v6 の J-5）。実際のダイアログでは 0 件を起こせない |
| 上書き確認を止められる | 使わない（常に確認する） |
| ファイル系のタイトルと owner | 使わない（NULL） |

変わる振る舞いは設計 v6 の 5.3 にまとめてある。UI テストで見えるのは `def_ext = ""` の 1 つだけ（1.1 章）。

## 5. 見つかった問題

v1 の 5 章の `ShowDialog` の問題（空のタイトル・メッセージでイベントを出したあとネイティブまで進む）は、移行で直した。
今は `(null, false, -2)` を 1 回出して終わり、ダイアログは出ない（設計 v6 の J-1、1.1 章のテスト）。

## 6. 実行結果

| 日付 | 実行 | Player | Dialog |
|---|---|---|---|
| 2026-09-26 | 1.x、`--skip-build` | 31/31 | D-01〜D-14 の 14 本すべて合格 |
| 2026-09-27 | 1.x、J-6 の 2 本置きと 1.1 章の 2 本を足して | 33/33 | 16 本すべて合格 |
| 2026-09-27 | 2.0.0、1.1 章の 10 本を足して（1 回目） | 41/43 | 2 本が落ちた: `def_ext = ""`（`new` が返った。期待値を直した）、非 ASCII のアラート（2 章の「実装で踏んだもの」。テストを直した） |
| 2026-09-27 | 2.0.0、上の 2 本を直して（2 回目） | 43/43 | 26 本すべて合格。1 本 2〜12 秒（ダイアログが出ないことを見る 3 本は閉じる役の待ち時間でおよそ 11 秒） |
