# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/features/dialog/designs/2026-09-27-windows-dialog-design-v3.md
- 前回: artifact/features/dialog/reviews/2026-09-27-windows-dialog-design-review-v2.md
- 機能名: dialog
- プラットフォーム: Windows
- レビュー: v2 との差分に絞り、3 本を並行で行った。サブエージェント 2 本（観点を分担: ネイティブ境界 / 公開契約・テスト・自動化の前提）と Codex（全観点）。どれも読み取りのみ
- 出典の略記: [N] = ネイティブ境界、[T] = 契約・テスト、[X] = Codex

---

## v2 の指摘の扱い

| v2 | 判定 | 理由 |
|---|---|---|
| A1-1 `null` の既定値 | 解消 | 4.2 の表が `WindowsDialogManager.cs:211-214,230-233,275-276,322-323,407-408,454-455,535-537` と一致。層 1 にも入った [N][T][X] |
| A1-2 `ntk_last_system_code` の結び付け | 解消 | 7.3。`InternalsVisibleTo("NativeToolkit.Runtime.PlayModeTests")` は `Runtime/AssemblyInfo.cs:7` にある。`extern` の可視性は新 A2-2 [N][T][X] |
| A1-3 5.3 に無い差分 | 解消（一部は新 B-2） | GetLastError 0 は `WindowsDialogApi.cpp:61-62` と `WindowsDialogError.h:48`、入力側のサロゲートは `Utf8.cpp:34` で裏付けた [N][T][X] |
| A2-1 出力ハンドルの初期化 | 一部 | 書かれたが、初期化の位置が J-7（最初の P/Invoke）より後（C-2）[N][X] |
| A2-2 層 2a のガード・`LogAssert` | 解消 | [T][X] |
| A2-3 追加のピンを削除より前に解決 | 解消 | `PreBuildProcessor.cs:524-538`（解決）、`:553-565`（削除）と一致 [N][X] |
| A2-4 非 ASCII の fixture | 解消 | [T][X] |
| A2-5 閉じる役の照合 | 一部 | 手順はできたが、読み方が別のプロセスでは動かない（新 A2-1）[T] |
| B-1〜B-7 | 解消（B-2 は一部） | 層 2a の購読者の例外のテストが、Player の構造を押さえる前提が無い（新 B-1）[N][T][X] |
| C | 解消 | UI テスト計画の扱いは新 C-1 [N][T][X] |

## 強み

- **ネイティブ境界に新しい A1 / A2 は無い。** `extern` 14 本は型、`ref` / `out`、`size_t` → `UIntPtr`、戻り値まで `Dialog.h:138-170` と `Common.h:54-88` に一致 [N]
- -1〜-5 を OS の経路が返さないという主張は正しい [N]
  - Data 層が書く値は、`-1`、CommDlgExtendedError、GetLastError、HRESULT、`ERROR_INSUFFICIENT_BUFFER` だけ
  - `Classify` が `0xFFFFFFFF` を先にキャンセルにする（`WindowsDialogError.h:39-40`）
  - `Guarded` の system code は 0 か `0x8007000E`（`Guard.h:20-25`）
- J-11 の前提は正しい [N]
  - dist 1.12.0 に PDB は無い（nupkg の中も `NativeToolkitC.dll` だけ）。dist 1.11.0 にも無い
  - コピー元のフォルダに `WindowsLibrary-Debug.pdb` は無い
- 開発者の判断（`-4` / `-5` の分割、公開の維持、`result` の区別、PDB、`def_ext` の先行記録、`buffer_size`、IL2CPP）はすべて正しく反映されている [X]
- E-1〜E-13 はすべて、実行できる層に割り当たっている [T]

## 改善点

### 高優先度

**A1（この通り実装すると契約が変わり、しかもコンパイルもテストも通る。直して再レビュー）**

1. **フィルタの分解が、Win32 の「最初の空要素で一覧が終わる」読み方になっていない** [T]（4.2）
   - 「末尾の空要素は捨てる／奇数個なら J-4」を文字どおりに実装すると、`"A\0*.a\0\0B\0*.b\0\0"` は途中の空要素のせいで奇数個になり、`-2` になる。1.x では OS が最初の `\0\0` で一覧を終え、A だけを見せていた
   - `"\0*.txt\0\0"`（名前が空）も、1.x とは違う扱いになる
   - D-01〜D-14 はこの形を使わないので、テストは通ってしまう
   - 修正: 「先頭から名前・パターンの順に読み、名前が空の要素に来たら一覧を終える。それより後ろは捨てる（1.x で OS が読んでいたのと同じ）。名前があってパターンが無い、または空なら J-4」と書く。層 1 に上の 2 例を足す

### 中優先度

**A2（実装やテストで必ず表に出る。実装結果ファイルにチェックリストとして転記する）**

1. **`expect-text` は `GetWindowText` では読めない** [T]（7.3）
   - 閉じる役は別のプロセス（PowerShell）。`GetWindowText` は、別のプロセスのコントロールの文字を返さない
   - 修正: `expect-text` は `SendMessage(WM_GETTEXTLENGTH / WM_GETTEXT)` で読む。`expect-title` はトップレベルの窓なので `GetWindowText` のままでよい
   - 今の手順の分解は `Split(' ', 3)`（`WindowsDialogSamplePlayerTests.cs:495`）なので、空白を含む期待値が切れる。`select` と同じく `Substring` で残り全体を取る
2. **`extern` の可視性を `internal` と書く** [N][T]（4.1、7.3）
   - 1.x の前例は `private static extern`（`WindowsDialogManager.cs:121-195`）。`private` では `InternalsVisibleTo` でも呼べない
   - `extern` は Player でしかコンパイルされないので、層 0 まで気付かない
3. **層 2b の J-1 / J-2 のテストにも `LogAssert.Expect` が要る** [T]（7.3）。予期しない `LogError` は Player のテストでも失敗になる

**B（検証手段の穴）**

1. **`-5` を Manager の `#if` で早く抜ける形にすると、層 2a の購読者の例外のテストが、Player の構造を押さえられない** [T]（5.5、7.2）
   - 修正: `#if` の分かれ目は `WindowsDialogCApi` の中だけに置き、`-5` はネイティブの結果と同じ経路（try の中の戻り値）で返す。Manager には `#if UNITY_EDITOR` を置かない。`-5` のログの種類も決める
2. **catch の範囲と finally の関係を書く** [N]（5.5）
   - C# の catch は、同じ try の finally から出た例外を捕まえない。`ntk_string_free` などの結び付けの誤り（`EntryPointNotFoundException`）は、イベントを出さずに呼び出し側へ出る。5.3 の「例外は出ず `-4`」と食い違う
   - 修正: 形を `try { try { 3〜6 } finally { 7 } } catch (Exception e) { code = Map(e); }` と書く。7 は、ハンドルの解放が失敗しても `FreeHGlobal` が必ず走る順にする
3. **5.3 に「そのほかの C# の例外（`OutOfMemoryException` など）: 1.x は呼び出し側に出た → `-3` のイベント」の行が無い** [T]
4. **`def_ext = ""` の手順** [T]（5.6）
   - 手順 1 のテストは、1.x で見た値を assert する
   - 手順 5 で違いが出たら、期待値と 5.3 の行を同じコミットで直す
   - 「C# では再現できない」は「完全には再現できない」に直す。`nFilterIndex = 1` で固定なので、最初のフィルタの拡張子を渡せば一部は再現できる。見送るなら、その理由を添える

### 低優先度

**C（記述の整合）**

1. **UI テスト計画 v1 を「既存変更」にしているが、版のついた文書は上書きしない**（`artifact/README.md:28`）[X]。`2026-09-26-windows-dialog-ui-test-plan-v2.md` を新しく作る。v2 では次を直す [T]
   - 1 章の :46「タイトル・メッセージの表示は確かめない」が、7.3 の `expect-title` / `expect-text` と矛盾する
   - 7.4 の「1 章と同じ」も合わせる
2. **初期化の位置** [N]（5.5）。出力のハンドルは try の外で、J-7（`ntk_version` の最初の P/Invoke）より前に `= IntPtr.Zero` で宣言する
3. **追加のピンに `debug_dll` に当たるキーが無い** [N]（5.4）。「`SourceFor` は常に `extra_dll` を使う」と書く（`PreBuildProcessor.cs:85`）
4. **`VERSION.txt` の書き換えをピンごとに書く** [N]（5.1）。主のピンは `.dll` を付けた名前、移行中の追加のピン（`unity-windows-native-toolkit(-debug)`）は今のまま
5. **J-11 で消す範囲** [N]。Windows のブロック（`PostBuildProcessor.cs:135-166`）の development ビルドの分岐と、クラスのコメント（:16「copies PDB files」）も直す
6. **OP-01 の想定外の `CANCELED` のログ** [N]（4.3）。system code は読まないので、「OP-01 で想定外の `CANCELED`」とだけ出す
7. **`agent-rules/coding-rules/testing.md` の古くなる記述を 5.1 に足す** [T]
   - :235（Dialog が「`|| UNITY_EDITOR` なし」の群）
   - :314 と :404-406（Dialog に層 1 が無い）
8. **Notification の Player テストは `--include-destructive` を付けないと 1 本も走らない** [T]（5.4、`testing.md:376`）。5.6 の手順 1 に書く
9. **非 ASCII の fixture は `_folder` の下に作る** [T]（7.3）。途中で落ちても、SetUp の `DeleteFolder` が片付ける
10. **5.3 の J-1 の行** [T]。「空」を「`null`・空」にする
11. **E-9 の実物** [T]（8.1）。Manager の catch が `-4` を出すことと、版が合わないことを覚えて以後渡さないことは、実物では起こせない。層 1 とコードレビューで押さえる、と 8.1 に書く

## 不足項目

- なし

## 総合評価

v2 の A1 3 件は解消した。ネイティブ境界の担当と Codex は、新しい A1 を見つけていない。

新しい **A1 は 1 件**（フィルタの分解の読み方）。直し方は決まっていて小さい。
A2 は 3 件で、閉じる役の読み方、`extern` の可視性、層 2b のログ。

**A1 を直した v4 で、その差分に絞って再レビューする。**
