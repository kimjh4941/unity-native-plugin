# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/windows/dialog/designs/2026-09-27-windows-dialog-design-v2.md
- 前回: artifact/windows/dialog/reviews/2026-09-27-windows-dialog-design-review-v1.md
- 機能名: dialog
- プラットフォーム: Windows
- レビュー: 3 本を並行で行い、指摘を重ねて整理した。サブエージェント 2 本（観点を分担: ネイティブ境界 / 公開契約・テスト・自動化の前提）と Codex（全観点）。どれも読み取りのみ
- 出典の略記: [N] = ネイティブ境界、[T] = 契約・テスト、[X] = Codex。複数が挙げたものは併記した
- 分類の調整: [X] が A1 とした「公開型の追加」は、既存の API を変えない追加なので B に下げ、判断事項（B-1）とした。[N] [T] が B / C とした「5.3 に無い差分」は、v1 の A1-6 の残りなので A1 に上げた

---

## v1 の指摘の扱い

| v1 | 判定 | 理由 |
|---|---|---|
| A1-1 `MB_*` の OR、`MB_HELP` | 解消 | 4.2 で OR してからマスクで分ける。層 1 に「別の引数に入れたフラグ」と `MB_HELP` [N][T][X] |
| A1-2 J-3 の符号 | 解消 | 定数との比較、`unchecked`、予約値の根拠も正しい [N][T][X] |
| A1-3 二重ガード | 解消 | 5.1。前例 `WindowsClipboardManager.cs:5,823` と一致 [N][T][X] |
| A1-4 例外と単一イベント | 解消 | 5.5。ただし検証はコードレビューだけ（B-2）[N][T][X] |
| A1-5 `buffer_size` | 解消 | J-8、4.2、5.3 [N][T][X] |
| A1-6 書かれていない差分 | **一部** | 5.3 はできたが、まだ漏れがある（新 A1-3）[N][T] |
| A1-7 同梱 DLL | 解消 | 5.1（`.meta` は C）[N][T][X] |
| A2（ガード、`WindowsDialogErrorCodes`、J-6、J-7） | 解消 | J-6 の細部は新 A2-3 [N][T][X] |
| B（非 ASCII、フィールドの位置、IL2CPP、各エラーの層、層 2a、`allow_missing_file`、J-1 / J-2 のハーネス、借用ポインタ、`UNKNOWN` のログ、D-01〜D-14 の根拠） | 解消 | 非 ASCII の組み方は新 A2-4 / A2-5。`extern` 宣言 14 本の列挙は B-6 [N][T][X] |
| C | 解消（UI テスト計画・README は一部） | C-5 [T][X] |
| 不足: PDB（`PostBuildProcessor`） | **未解消** | B-5 [T][X] |
| 不足: そのほか（変更一覧、Editor ウィンドウ、`MonoPInvokeCallback`、STA / MTA、マニュアル） | 解消（マニュアルの経路は B-4） | [T][X] |

## 強み

- **ネイティブ境界に A1 は無い。** 4.1 の大きさとフィールドの位置は `Dialog.h` の `#pragma pack(push, 8)` と `CApiLayoutTest.cpp:23-55` に全部一致。マスク、名前での対応、結果の 11 値も正しい。2.0.0 の `ToMessageBoxType` は同じ `MB_*` に戻し、サンプルの 0x141 は同じ uType になる [N]
- 2.0.0 は 1.x と同じ Win32 の呼び出しになる（`OFN_*`、`nFilterIndex = 1`、`FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM`、既定のフィルタのバイト列、上限を超えたときの `SYSTEM_ERROR`）[N]
- J-3 の予約値の根拠（`Classify` が `0xFFFFFFFF` を先に除く）と J-7（`NTK_VERSION` = 0x020000、`Version.cpp` の static_assert）は正しい [N]
- J-1 / J-2 の層 2b のハーネスは今の閉じる役で組める（10 秒待って `no dialog`、`exit 2`。`WindowsDialogSamplePlayerTests.cs:397,492`）[T]
- 整合チェックは FAIL なし [T][X]

## 改善点

### 高優先度

**A1（この通り実装すると契約が変わり、しかもコンパイルもテストも通る。直して再レビュー）**

1. **明示的な `null` の既定値が抜けている** [X]（4.2）
   - 今の C# は `null` を既定値に戻す: `ShowFolderDialog` の `title ?? "Select Folder"`、`ShowMultiFolderDialog` の `title ?? "Select Folders"`、`ShowSaveFileDialog` の `def_ext ?? "txt"`（`WindowsDialogManager.cs:408,455,537`。確かめた）
   - 4.2 はアラートの `null` だけを書き、ファイル系はそのまま渡すと読める。`null` の題名はシステムの題名に、`null` の `def_ext` は拡張子なしに変わる。今のテストは明示的な `null` を渡さない
   - 修正: 4.2 に 6 本すべての `??` の既定値を表で書く（`filter` も含む）。層 1 のリクエスト組み立てに「`null` → 既定値」を足す
2. **`ntk_last_system_code` の実際の結び付けがどのテストでも通らない** [X]（7 章）
   - `ntk_version` は J-7 で、`ntk_string_*` / `_list_*` は D-01〜D-14 で毎回通る。`ntk_last_system_code` だけは OS の失敗（E-3）を起こせないので、宣言名や呼び出し規約を誤っても全テストが通り、実際の失敗のときだけ `-4` に化ける
   - 修正: 層 2b に、`WindowsDialogCApi` の `ntk_last_system_code` を直接 1 回呼び、例外なく戻ることを確かめるテストを足す（`internal` なので `InternalsVisibleTo` で呼べる）
3. **5.3 に無い差分が残っている** [N][T]（5.3、J-1、J-9）
   - 例外がイベントに変わる: 1.x の Manager に try/catch は無い（`WindowsDialogManager.cs:208-564`）。DLL が無い、または Windows ターゲットの Editor（1.x の DLL は Editor 無効）では、例外が呼び出し側に出ていた。移行後は `-4` のイベントになる [T]
   - Manager が Windows 以外のターゲットの Editor でもコンパイルされる（型が見えるようになる）[T]
   - アラートで `MessageBoxW` が 0 を返し、`GetLastError` も 0 のとき: 1.x は成功扱いで `(0, true, null)`（`WindowsDialogManager.cs:245`）。2.0.0 は `UNKNOWN` を返すので `-3` になる（`WindowsDialogError.h:48`、`WindowsDialogApi.cpp:62`）[N]
   - タイトルとメッセージの両方が空のとき、1.x はエラーを 2 回出してからネイティブを呼ぶので、最大 3 回出た（`WindowsDialogManager.cs:218-243`）。5.3 と J-1 の「2 回」を直す [T]
   - 入力側の対になっていないサロゲート（title / message / filter / def_ext）も `Encoding.UTF8` で U+FFFD になる。今の 5.3 は出力側だけ [N]
   - `def_ext = ""`: 1.x は空文字列をそのまま `lpstrDefExt` に渡した（1.11.0 `WindowsDialogManager.cpp:459`）。2.0.0 は空を `nullptr` にする（`WindowsDialogApi.cpp:121`）。空で NULL でないときに、選んだフィルタの拡張子を付けるかは未確認 [N][T]。C# では再現できない（ネイティブが両方を同じ扱いにする）ので、差があれば既知の差分になる
   - 修正: 5.3 に行を足す。`def_ext = ""` は、実装の最初に 1.x のまま層 2b のテストで振る舞いを記録してから、5.3 の記述を決める

### 中優先度

**A2（実装やテストで必ず表に出る。実装結果ファイルにチェックリストとして転記する）**

1. **出力ハンドルを `IntPtr.Zero` で初期化すると書く** [X]（5.5）。P/Invoke の解決前に例外になると、`finally` が不定の値を解放しうる。ネイティブが NULL を書くのは関数に入ったあと（`DialogCApi.cpp:66`）
2. **層 2a のテストファイルのガードは `#if UNITY_EDITOR`** [T]（5.1、7.2）。PlayMode の asmdef は `includePlatforms: []` で Player にも入る。Player で「正しい引数で `-4`」を呼ぶと本物のダイアログが出て止まる。前例は `WindowsClipboardManagerIntegrationTests.cs:3-9`。7.2 に `LogAssert.Expect` も書く
3. **J-6 の追加 DLL は、削除より前にコピー元を解決する** [N]（5.4）。今の処理は、主の DLL について「削除より前に解決し、無ければ `BuildFailedException`」（`PreBuildProcessor.cs:524-538`）。追加も同じにしないと、Clipboard / Notification の DLL が Player に入らず、実行時に `DllNotFoundException` で初めて気付く
4. **非 ASCII の fixture は、足すテストの中だけで作る** [T]（7.3）。SetUp は全テスト共通で（`WindowsDialogSamplePlayerTests.cs:84-89`）、`AssertFolderUnchanged` は中身の完全一致を求める（`:229-233`）。共通にすると D-07 / D-08 / D-14 が落ちる
5. **閉じる役に、文字列を読んで照合する手順を足す** [T]（7.3）。今の手順は `text | press | select | confirm` だけで、`GetWindowText` も無い（`:407-420`、`:493-506`）。例: `expect-title <文字列>`、`expect-text 65535 <文字列>`、違えば `exit 3`。期待値は今の base64 の UTF-8 で渡せる

**B（検証手段の穴・判断）**

1. **`WindowsDialogErrorCodes` を公開にするかと、`-4` に 3 つの原因をまとめるか** [T][X]（J-3、基本情報）。公開の型を足すことは既存の API を壊さないが、「公開 API を変えない」という方針の書き方と合わない [X]。Clipboard は `PlatformUnavailable = 1000` と `BridgeUnavailable = 1001` を分けている（`WindowsClipboardErrorCode.cs:78,81`）。公開定数は、あとから分けると破壊的な変更になる [T]
2. **5.5 の契約をテストで押さえる** [T]（5.5、E-9、E-11）。例外を errorCode に変える処理を `WindowsDialogCApiMapping` の純粋な関数にし、層 1 で固定する（`DllNotFound` / `EntryPointNotFound` / `BadImageFormat` → `-4`、そのほか → `-3`）。層 2a に「Editor の `-4` で、例外を投げる購読者を付けると、例外が呼び出し側に出て、ハンドラーは 1 回だけ呼ばれる」を足す
3. **アラートの `-3` / `-4` のときの `result`** [T]（4.3、J-7、J-9）。J-1 / J-2 は `null`、J-9 のネイティブの失敗は `0`。C# 側で決まる `-4` と、例外による `-3` が決まっていない
4. **マニュアルへの引き継ぎの経路** [T]（5.3）。write-manual が読むのは `artifact/<os>/<feature>/results/*-implementation-result*.md`（`write-manual/workflow.md:26-29`）。5.3、`-2`〜`-4` の意味、使えなくなった `MB_*` を実装結果ファイルに書き写すと、5.6 に書く
5. **PDB（`PostBuildProcessor`）を、どの段階で直すか** [T][X]。親課題 README は「移行時に一緒に直す」とするが、担当の段階が無い。今のパスは解決せず、名前も 1.x のもの
6. **`DllImport` 14 本の宣言を列挙する** [X]（4.1）。型の対応はあるが、各関数の C# の宣言が無い
7. **5.6 の各段階に層 0（Player ビルド）を入れる** [T]。`!UNITY_EDITOR` 側のコードは層 1 でも層 2a でもコンパイルされない。手順 3 と 4 で `scripts/verify_unity_windows.sh` を流す

### 低優先度

**C（記述の整合）**

- `Win32MessageBox.cs` は、J-2 で XML コメントを変えるので「既存変更」に移す（定数は変えない）[T][X]
- DLL 名に `.dll` を付けるのは Dialog が最初なので、`VERSION.txt:18-19` と親課題 README:78-79,205 の「`.dll` を付けない」を Dialog の変更と一緒に直す。後から書く Clipboard / Notification の計画が合わなくなる [N]
- 1.2 の `INVALID_PARAMETER` の原因に、次の 3 つを足す [N]
  - 列挙の範囲外（`DialogConvert.cpp:50-53`）
  - `filters` が NULL で件数が 1 以上（`:71`）
  - out 引数が NULL
- `INVALID_PARAMETER` のとき system code は常に 0（`DialogCApi.cpp:51`）なので、4.3 の「ログに system code」は外す。5.5 の手順 5 は「`NONE` / `CANCELED` 以外なら直後に読む」にする（`UNKNOWN` のログと合わせる）[N]
- OP-01 の `CANCELED` は、ヘッダーでは返らないとされる。ただし `Classify` の上では起こりうるので、`-3` にすると書く [N]
- 5.1 の DLL の行に `.meta` を足す（既存の `unity-windows-native-toolkit.dll.meta` もコミットされている）[N]
- 構造体の C# 名を書く。P1 に合わせ、`WindowsDialogCApi` の入れ子にする [T]
- `WindowsDialogErrorCodes` の形（public static class、`int` の定数、複数形）の前例として `MacClipboardErrorCodes`（`MacClipboardErrorInfo.cs:76`）を挙げる [T]
- 次の「1.x」の記述を、5.1 の変更一覧に足す [T][X]
  - テストのコメント（`WindowsDialogSamplePlayerTests.cs:68,153,179`）
  - UI テスト計画 1 章の :21「期待値は 1.x」と :47「2.0.0 で効いたら足す」。移行後もタイトルと owner は固定値で使わない
  - 親課題 README の完全一致の判定、`SafeHandle`、一括移行の記述

## 不足項目

- なし（PDB は B-5）

## 総合評価

v1 の A1 7 件は、A1-6 の一部を除いて解消した。ネイティブ境界は 3 本とも誤りを見つけておらず、構造体、フラグ、Win32 の呼び出しの一致は裏付けられた。

新しい **A1 は 3 件**。
1. 明示的な `null` の既定値が抜けている
2. `ntk_last_system_code` の結び付けがどのテストでも通らない
3. 5.3 に無い差分が残っている

どれも直し方は決まっていて、小さい。
A2 は 5 件で、どれもテストのハーネスと J-6 の処理の細部。
B-1（公開と `-4` の分割）と B-3（アラートの `result`）は、開発者の判断が要る。

**A1 を直した v3 で、もう一度再レビューする。** 範囲は差分に絞ってよい。
