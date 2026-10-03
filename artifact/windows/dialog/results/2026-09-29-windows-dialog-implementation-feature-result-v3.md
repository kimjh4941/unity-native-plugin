# 実装結果レポート v3（Windows Dialog の C ABI 2.0.0 への移行）

## v2 からの変更

- 手順 6（STA / MTA）と手順 7（IL2CPP、J-10）が済んだ。どちらも Dialog のコードは変えていない（5 章）
- 3 機能を移し終え、J-6 の追加のピンと 1.x の DLL を消した（`05195a5`）
- テストの補助のプロセスの起動を IL2CPP でも動く形に替えた（`722ec41`、2.2）

## v1 からの変更（v2）

- 2 本置き（J-6）の Player で `--include-destructive` を流し、合格した（5 章）。v1 で未実施としていた項目を埋めた

## 基本情報

- 日付: 2026-09-29（v1・v2 は 2026-09-27）
- 機能名: dialog
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-12
- 計画ファイル: `artifact/windows/dialog/designs/2026-09-27-windows-dialog-design-v6.md`
- UI テスト計画: `artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md`
- 親課題: `artifact/topics/windows-c-abi-2/README.md`

## 0. 状態サマリー

設計 v6 の 5.6 の手順 1〜8 をすべて終えた。

| 手順 | 内容 | コミット | 状態 |
|---|---|---|---|
| 1 | J-6（DLL の 2 本置き）、J-11（PDB）、1.x での記録テスト 2 本 | `3cf4b1b` | 済み |
| 2・3 | 共通部、`WindowsDialogErrorCodes`、Bridge、層 1 | `f759f2b` | 済み |
| 4 | Manager の置き換え、層 2a | `b7bad1c` | 済み |
| 5 | 層 2b（D-01〜D-14 を期待値を変えずに通す、設計 7.3 の追加） | `1b9a9ee` | 済み |
| 6 | Notification の移行後に D-09〜D-12 を流し直す（STA / MTA） | — | 済み（2026-09-27。Notification の移行後の実行で D-09〜D-12 を含む 26 本が合格。Notification の実装結果 v1 の 5 章） |
| 7 | 3 機能の移行後に IL2CPP の Player で層 2b を 1 回流す（J-10） | `6dc71d4`、`722ec41` | 済み（2026-09-29。IL2CPP で 26 / 26。5 章） |
| 8 | 実装結果ファイル（この文書） | この文書のコミット | 済み |

同じ段階で直したもの: 名前空間とフォルダの移動（`d84092d`、Q-4 の例外）、agent-rules の Windows の規則（`d608ea1`、`929f5b6`）。

## 1. 実装サマリー

### 1.1 計画ファイル由来の実装

- **2 層（Bridge + Manager）と共通部。** `Runtime/Windows/Common/WindowsNativeToolkitCApi.cs`（DLL 名、版の確認、`Common.h` の関数、UTF-8 の入出力）、
  `Runtime/Windows/Dialog/WindowsDialogCApi.cs`（Bridge）、`WindowsDialogManager`
- **二重ガード。** 型と純粋な関数は `UNITY_STANDALONE_WIN || UNITY_EDITOR`、`DllImport` とその呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR`。
  Editor では Bridge の呼び出しが `-5` を返し、Manager は Player と同じ経路を通る
- **J-1** 空のタイトル・メッセージ → ネイティブを呼ばず `(null, false, -2)` を 1 回
- **J-2** 4 つのフラグ引数を OR し、マスクで分ける。対応先の無いビット → `(null, false, -2)`。`MB_HELP`（0x4000）は通す
- **J-3** `WindowsDialogErrorCodes`（`-1`〜`-5`）を公開
- **J-4** フィルタは Win32 と同じく最初の空の名前まで読む。パターンの無い名前・`;` だけ → `-2`
- **J-5** 複数選択の 0 件は空の `ArrayList` で成功
- **J-6** 移行の間は DLL を 2 本置く（VERSION.txt の追加のピン、PreBuildProcessor）
- **J-7** 最初の呼び出しで `(ntk_version() >> 16) == 2` を 1 回確かめて覚える。違う・DLL が無い → `-4`
- **J-8** `buffer_size` は無視（XML コメントに上限を書いた）
- **J-9** アラートの失敗の `result` は、ネイティブの失敗なら `0`、C# 側なら `null`
- **J-11** PostBuildProcessor の PDB のコピーを消した
- **try の形**（設計 5.5）: 出力のハンドルは `IntPtr.Zero` で初期化し、`!= IntPtr.Zero` のときだけ解放。入力の解放はハンドルの解放が投げても走る。イベントは catch の外で 1 回
- `Win32MessageBox` は定数を変えず、2.0.0 で使えない 4 つに XML コメントを足した

### 1.2 実装時の追加判断

- **try / finally の置き場所。** 5.5 の擬似コードは Manager の中に内側の try / finally を置くが、解放は Bridge（`PathCall` / `ListCall` / `ShowAlert`）の finally に置き、
  Manager は Bridge の呼び出しを try / catch で包んだ。Bridge の finally から出た例外も Manager の catch に届くので、契約（解放の失敗も結果になる、イベントは 1 回）は同じ
- **ログの入口。** csharp.md は `internal` メソッドにも入口の `Debug.Log` を求めるが、Bridge と共通部の `internal static` には付けなかった。
  前例（`WindowsClipboardJsonBuilder` などの `internal static` の変換クラス）に合わせ、全引数の入口ログは公開 API の Manager に置いた
- **ログの重さ。** 結果として返す失敗（`-2`、`-4`、`-5`、ネイティブの失敗）は `LogWarning`。`-3` になる想定外の例外だけ `LogError`（csharp.md「想定外の失敗」）
- **アラートの未定義の結果。** `NONE` で `ntk_dialog_alert_result` に無い値が来たら `(0, false, -3)` と `LogWarning`（設計 4.3 の「想定外の値」を当てはめた）
- **`Win32MessageBox` の `cref`。** このクラスはガードが無く全プラットフォームでコンパイルされるので、ガードのある型（`WindowsDialogManager`、`WindowsDialogErrorCodes`）は `<c>` で書いた
- **`def_ext = ""` を確定した**（設計 5.3 の最後の未確定の行）。1.x は `new.txt`、2.0.0 は `new`。設計どおり意図した差分として受け入れた（下の 3.4）
- **テストで踏んだもの。** `MB_OK` だけのメッセージボックスでは、OK ボタンのコントロール ID が 2（IDCANCEL）になる（押した結果は IDOK）。非 ASCII のアラートのテストは `MB_OKCANCEL` にした

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Common/WindowsNativeToolkitCApi.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Dialog/WindowsDialogCApi.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Dialog/WindowsDialogErrorCodes.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/windows-native-toolkit-capi-2.0.0.dll`（dist 1.12.0。importer は Win64 のみ・Editor 無効）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsDialogCApiTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsNativeToolkitCApiTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsDialogManagerEditorTests.cs`
- `artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Dialog/WindowsDialogManager.cs`（1.x の `DllImport` と UTF-16 のバッファ処理を削除）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Dialog/Win32MessageBox.cs`（XML コメントのみ）
- `Packages/com.jonghyunkim.nativetoolkit/Editor/Build/PreBuildProcessor.cs`、`PostBuildProcessor.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/VERSION.txt`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsDialogSamplePlayerTests.cs`（v3: 閉じ役の PowerShell を `WindowsTestProcess` で起動し、出力を UTF-8 で読む。IL2CPP の Player は `Process.Start` に対応しないため）
- `agent-rules/coding-rules/testing.md`
- `artifact/topics/windows-c-abi-2/README.md`

### 2.3 非変更（対象だが未変更）

- `Runtime/UI/Windows/Dialog/*`、`Assets/Scripts/Runtime/UI/Windows/Dialog/WindowsDialogManagerTest.cs`、`Runtime/Resources/UI/Windows/Dialog/*`、`Editor/UI/NativeToolkitEditorWindow.cs`: 公開 API を変えていないため（名前空間の `using` は `d84092d` で直した）
- 設計書 v6: 版の付いた文書は上書きしない（`artifact/README.md`）。5.3 の `def_ext` の行の確定は、この文書の 3.4 に書く

## 3. エラー契約反映

### 3.1 エラーケース実装反映（設計 v6 の 6 章）

| # | 状況 | 実装 | 検証 |
|---|---|---|---|
| E-1 | キャンセル（OP-02〜06） | `(null, true, true, -1)` | 2b（D-03 / D-05 / D-07 / D-09 / D-11）合格 |
| E-2 | アラートで Cancel | `result = 2` で成功 | 2b（D-02）合格 |
| E-3 | OS が失敗を返す | `unchecked((int)ntk_last_system_code())` | 1（`FailureCode`、負の HRESULT を含む）、2b（結び付け）合格 |
| E-4 | ネイティブが引数を拒む | `-2`、ログに関数名と `struct_size` | 1（`FailureCode`）合格 |
| E-5 | ネイティブの想定外 | `-3`、ログに system code | 1 合格 |
| E-6 | タイトル・メッセージが空（J-1） | `(null, false, -2)` を 1 回 | 2a（6 通り）、2b（ダイアログが出ない）合格 |
| E-7 | 使えないフラグ（J-2） | `(null, false, -2)` | 1（分解）、2a（5 通り）、2b 合格 |
| E-8 | パターンの無いフィルタ（J-4） | `-2` | 1、2a（3 メソッド）、2b 合格 |
| E-9 | DLL が無い・版が違う（J-7） | `-4`、アラートは `result = null` | 1（版判定、例外の変換）合格。実際の DLL 欠落は起こしていない |
| E-10 | Editor（J-7） | `-5`、アラートは `result = null` | 2a（6 メソッド）合格 |
| E-11 | 複数選択で 0 件（J-5） | 空の一覧で成功 | Manager の変換（`new ArrayList(paths)`）。実際のダイアログでは起こせない |
| E-12 | C# 側の想定外の例外 | `-3`、イベント 1 回 | 1（例外の変換）、2a（購読者の例外でイベント 1 回）合格 |
| E-13 | アラートのネイティブの失敗（J-9） | `(0, false, errorCode)` | 1（変換関数）合格。起こせない |

### 3.2 イベントの返却仕様

- ファイル・フォルダ系: `isSuccess` はキャンセルまたは成功で `true`、失敗で `false`。キャンセルは `errorCode = -1`
- アラート: `isSuccess` は `errorCode == null` と同じ
- 1 呼び出しにイベント 1 回。購読者の例外は捕まえず呼び出し側に出る（2a で確認）

### 3.3 success 時契約

- `isSuccess == true` かつ `isCancelled == false` のとき `errorCode == null`: 2b の D-01 / D-02 / D-04 / D-06 / D-08 / D-10 / D-12 / D-14 と追加の成功系で確認

### 3.4 既知の差分（マニュアルへ引き継ぐ）

設計 v6 の 5.3 の表に、手順 5 の結果を反映したもの。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 名前空間とフォルダ | `JonghyunKim.NativeToolkit.Runtime.Dialog`、`Runtime/Dialog/` | `JonghyunKim.NativeToolkit.Runtime.Windows.Dialog`、`Runtime/Windows/Dialog/`。利用者は `using` を書き換える |
| `buffer_size` | 渡した大きさがバッファになり、超えると失敗 | 無視。1 本 1023 文字、一覧は全体 32768 文字を超えると OS の失敗コード |
| 空のタイトル・メッセージ | `(null, false, null)` と空のダイアログ（イベント最大 3 回） | ダイアログを出さず `(null, false, -2)` を 1 回 |
| 使えないフラグ | `MessageBoxW` に渡って効いた | ダイアログを出さず `-2` |
| パターンの無いフィルタ | そのまま OS に渡した | `-2` |
| パターンの中の空の要素（`"*.txt;;*.log"`） | そのまま OS に渡した | 詰めて `"*.txt;*.log"` |
| DLL が無い・関数が無い・形式が違う | 例外が呼び出し側に出た | 例外は出ず `-4` のイベント |
| そのほかの C# 側の例外 | 呼び出し側に出た | 例外は出ず `-3` のイベント |
| Editor で呼ぶ | Windows ターゲットなら例外、ほかではコンパイルされない | どのターゲットでもコンパイルされ、`-5` のイベント |
| アラートで `MessageBoxW` が 0、GetLastError も 0 | 成功扱いの `(0, true, null)` | `(0, false, -3)` |
| 複数ファイルでドライブ直下を選ぶ | `C:\\a.txt`（区切りが重なる） | `C:\a.txt` |
| フォルダの `title` に `""` | 空の題名 | システムの題名 |
| `filter` が 0 組（`""`、名前が空で始まる） | 空のフィルタ | 全ファイル 1 組 |
| **`def_ext` に `""`（確定）** | **Windows が選んだフィルタの拡張子を足した**（`new` → `new.txt`、2026-09-27 に 1.x で記録） | **入力したまま**（`new` → `new`、2026-09-27 に `windows-native-toolkit-capi-2.0.0.dll` で確認）。拡張子を付けたいときは `def_ext` を渡す（既定値の `"txt"` なら `new.txt`） |
| 対になっていないサロゲート | そのまま | U+FFFD に置き換わる |
| 新しい errorCode | -1 と OS の値 | -2 / -3 / -4 / -5 が加わる |

**予約した errorCode**（`WindowsDialogErrorCodes`）:

| 定数 | 値 | 意味 |
|---|---|---|
| `Cancelled` | -1 | ファイル・フォルダ系でキャンセル（`isCancelled = true`、`isSuccess = true`） |
| `InvalidArgument` | -2 | ダイアログを出す前に拒んだ引数（空のタイトル・メッセージ、使えないフラグ、パターンの無いフィルタ）。ネイティブが拒んだときも |
| `Unknown` | -3 | 詳しく言えない失敗 |
| `NativeUnavailable` | -4 | DLL が無い・関数が無い・形式が違う・major が 2 でない |
| `PlatformUnavailable` | -5 | Windows の Player でない（Editor）。ダイアログは出ない |

それ以外は OS の値（`CommDlgExtendedError`、`HRESULT`、`GetLastError`）で、`HRESULT` は負になる。**判定は符号ではなく定数との比較で行う。**

**2.0.0 で使えなくなった `Win32MessageBox` の定数:** `MB_SYSTEMMODAL`、`MB_TASKMODAL`、`MB_RIGHT`、`MB_RTLREADING`（定数は残し、渡すと `-2`）。
使えるのはボタン 7 種、アイコン 4 種、既定ボタン 4 種、`MB_APPLMODAL`、`MB_TOPMOST`、`MB_HELP`（0x4000、定数は無い）。

## 4. ビルド結果

- 実行コマンド:
  - `bash scripts/verify_unity_windows.sh`（Editor の EditMode / PlayMode、Win64 のテスト用 Player のビルドと Player テスト）
- 結果: SUCCESS（最終回 2026-09-27）
- 補足:
  - Player に DLL が 2 本入り、md5 が dist と一致（手順 1）
  - `!UNITY_EDITOR` 側のコード（Bridge の呼び出し、Manager の Player の経路）は Player ビルドでだけコンパイルされる。各手順で層 0 を通した

## 5. テスト結果

- 実行したテスト: EditMode 全体、PlayMode 全体、Player テスト（既定の実行）
- 結果サマリー（最終回）:
  - EditMode: 921 / 921（うち Dialog の層 1 は 112: `WindowsDialogCApiTests` 96、`WindowsNativeToolkitCApiTests` 16）
  - PlayMode: 204 / 204（うち `WindowsDialogManagerEditorTests` 23）
  - Player: 43 / 43（うち `WindowsDialogSamplePlayerTests` 26）
- 失敗時の対応（Player の 1 回目、41 / 43）:
  - `def_ext = ""` のテスト: 2.0.0 で `new` が返った。設計どおり差分として受け入れ、期待値を `new` に直した（3.4）
  - 非 ASCII のアラート: `MB_OK` の OK ボタンの ID が 2 だった。テストを `MB_OKCANCEL` に直した（1.2）
- **IL2CPP での実行**（手順 7、J-10。2026-09-29、`bash scripts/verify_unity_windows.sh --skip-build --include-destructive --il2cpp`）:
  - `WindowsDialogSamplePlayerTests` 26 / 26（D-01〜D-14 の本物のダイアログを外から操作するものを含む）。Player 全体は 93 / 93、作り直し 1 / 1
  - 同じ日の Mono の実行も 26 / 26（全体 93 / 93）
  - 経過:
    - 2026-09-27: 最初の IL2CPP の実行では、テストが PowerShell を起動する箇所と Player の exe のパスを読む箇所で落ちた（IL2CPP の `System.Diagnostics.Process` は `Start` と `MainModule` に対応しない）。本題に届く前の失敗で、移行したコードの問題ではない
    - テストの補助のプロセスを `Tests/PlayMode/WindowsTestProcess.cs`（kernel32 を直接呼ぶ）で起動するように直した（`722ec41`）
  - 詳細は Clipboard の実装結果 v7 の 5 章（`artifact/windows/clipboard/results/2026-09-29-windows-clipboard-implementation-feature-result-v7.md`）
- **STA / MTA**（手順 6）: Notification の `manager_create` がメインスレッドで MTA を試みても、Unity のメインスレッドは STA のまま。その状態で D-09〜D-12（フォルダ選択、STA 前提）を含む 26 本が通った（2026-09-27 以降の Mono の実行と、補助のプロセスを直した 2026-09-28 以降の IL2CPP の実行）
- **2 本置きでの Destructive の実行**（設計 v6 5.4 の確かめ方。2026-09-27、`bash scripts/verify_unity_windows.sh --skip-build --include-destructive`）:
  - Player: 59 本中 58 本合格、1 本スキップ（W-04。1.x の不具合で `[Ignore]` のまま。Notification の移行で外す）
  - M-19（予約したまま Quit）: 合格
  - Clipboard の全ボタンの実行のログ照合: 失敗 0
  - 内訳: `WindowsClipboardPlayerTests` 16、`WindowsClipboardSampleRunPlayerTests` 5、`WindowsClipboardSampleScreenPlayerTests` 2、`WindowsDialogSamplePlayerTests` 26、`WindowsNotificationSamplePlayerTests` 10（うちスキップ 1）
  - Dialog が 2.0.0、Clipboard / Notification が 1.x の DLL を読む状態で、3 機能の Player テストがすべて通ることを確かめた

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 | 備考 |
|---|---|---|---|---|
| 構造体の大きさと全フィールドの位置 | `Tests/Runtime/WindowsDialogCApiTests.cs` | `Structures_HaveTheCApiSizes`、`*_FieldOffsets` | ○ | 期待値は native-toolkit の `CApiLayoutTest.cpp` |
| リクエストの固定値 | 同上 | `Build*Request_*` | ○ | `allow_missing_file = 0`、`skip_overwrite_prompt = 0`、title / owner = 0 |
| フラグの分解（J-2） | 同上 | `CombineAlertStyle_*`、`TryMapAlertStyle_*` | ○ | 別の引数に入れたフラグ、`MB_HELP`、拒否 8 通り |
| 結果の対応 | 同上 | `TryToWin32Result_*` | ○ | 11 値と未定義 |
| エラー・例外の変換 | 同上 | `FailureCode_*`、`FromException_*`、`StatusFor_*` | ○ | 負の HRESULT |
| フィルタの読み方（J-4） | 同上 | `TryParseFilter_*` | ○ | 最初の空の名前で終わる、0 組は全ファイル |
| Editor の stub | 同上 | `Calls_InTheEditor_ArePlatformUnavailable_AndShowNothing` | ○ | |
| 版判定・UTF-8 | `Tests/Runtime/WindowsNativeToolkitCApiTests.cs` | 全 16 | ○ | サロゲートペア、対になっていないサロゲート |
| 引数の拒否・Editor・イベント 1 回 | `Tests/PlayMode/WindowsDialogManagerEditorTests.cs` | 全 23 | ○ | |
| D-01〜D-14 | `Tests/PlayMode/WindowsDialogSamplePlayerTests.cs` | 14 本 | ○ | 期待値は 1.x のまま |
| 1.x で記録した 2 本 | 同上 | `ShowSaveFileDialog_EmptyDefaultExtension_ReportsTheTypedNameAsIs`、`ShowFileDialog_FilterEndsAtTheFirstEmptyName` | ○ | 前者は期待値を変えた（3.4） |
| 7.3 の追加 | 同上 | 10 本 | ○ | UI テスト計画 v2 の 1.1 章 |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
|---|---|---|---|
| 実際の DLL 欠落・版違い（E-9） | - | - | Player から DLL を抜く仕組みが無い。判定と例外の変換は層 1 |
| OS の失敗・ネイティブの想定外（E-3〜E-5、E-13） | - | - | 起こせない。変換は層 1、`ntk_last_system_code` の結び付けは層 2b |
| 複数選択の 0 件（E-11） | - | - | 実際のダイアログでは起こせない |

## 6. Definition of Done

- 判定基準: ○ 実装・コード・テスト確認の範囲では OK / △ 一部 OK だが追加確認が必要 / × 未達 / - 対象外
- ○ 公開 API（メソッド、イベント、既定値）を変えずに C ABI 2.0.0 へ移した（名前空間の変更は Q-4 の例外として承認済み）
- ○ D-01〜D-14 が期待値を変えずに Player で通る
- ○ J-1〜J-9、J-11 を実装し、層 1 / 2a / 2b で確かめた
- ○ J-10（IL2CPP）: 26 / 26（2026-09-29）
- ○ STA / MTA（手順 6）: Notification の移行後も D-09〜D-12 が通る
- ○ 2 本置き（J-6）で 3 機能の Player テストが通る（Destructive を含む、5 章）
- ○ 既知の差分を XML コメントとこの文書に書いた（3.4）

## 7. 残作業

- 無し（手順 6・7 は済み。J-6 の追加のピンは `05195a5` で消した）
- マニュアル: 次の版の write-manual でこの文書の 3.4 を読む

## 8. ステップ 9 実行確認

- 提示文:
  - 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して次工程へ進む
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答
