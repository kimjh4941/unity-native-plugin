# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/features/dialog/designs/2026-09-27-windows-dialog-design-v4.md
- 前回: artifact/features/dialog/reviews/2026-09-27-windows-dialog-design-review-v3.md
- 機能名: dialog
- プラットフォーム: Windows
- レビュー: v3 との差分に絞り、2 本で行った。サブエージェント 1 本（公開契約・テスト。v3 の A1 を見つけた観点）と Codex（全観点）。どれも読み取りのみ
- 出典の略記: [T] = 契約・テスト、[X] = Codex

---

## v3 の指摘の扱い

| v3 | 判定 | 理由 |
|---|---|---|
| A1-1 フィルタの読み方 | 解消 | 1.x は Win32 形式の文字列をそのまま `lpstrFilter` に渡していた（native-toolkit `1.11.0:windows/WindowsLibrary/WindowsDialogManager.cpp:104,155,456`、C# は `MarshalAs(LPWStr)` で NUL も保つ、`WindowsDialogManager.cs:144,160,171`）。v4 の読み方は Win32 の形式と一致する。パターンの差分は新 B-1 [T][X] |
| A2-1〜A2-3 | 解消 | `WM_GETTEXT`、`internal static extern`、層 2b の `LogAssert.Expect` [T][X] |
| B-1〜B-4 | 解消 | `-5` の置き場所の行き先は新 B-3 [T][X] |
| C-1〜C-11 | 解消 | 8.3 の参照だけ v1 のまま（新 C-1）[T][X] |

## 強み

- try / finally を内側に置いて外側で catch する形は正しく、finally から出た例外も `-4` のイベントになる。イベントを出すのは catch の外で、文章とも合う [T][X]
- `WM_GETTEXT` と `Substring` による取り方は、今の閉じる役（`WindowsDialogSamplePlayerTests.cs:495-500`）に合う [T]
- C の引用（`PostBuildProcessor.cs:16,135-166`、`PreBuildProcessor.cs:85`、`testing.md:235,314,376,404-406`、`artifact/README.md:28`）は実物と一致 [T]

## 改善点

### 高優先度

- A1: なし

### 中優先度

- A2: なし

**B（検証手段の穴）**

1. **パターンが `;` だけのフィルタが、J-4 を通り抜けて全ファイルになる** [T]（4.2、5.3）
   - 2.0.0 はパターンを `;` で分けて空の要素を捨て（`DialogConvert.cpp:81-87`。確かめた）、残りが 0 個なら `*.*` にする（`WindowsDialogMapping.h:49-50`。確かめた）
   - `"A\0;\0\0"` は v4 では全ファイル表示、1.x は `;` をそのまま OS に渡していた
   - `"*.txt;;*.log"` は `"*.txt;*.log"` に詰められる
2. **1.x がフィルタをどう読むかを、実物で記録する手順が無い** [T]（5.6）。4.2 の「1.x では OS がそう読んでいた」は Win32 の文書からの推定。手順 1 で `def_ext` と同じく、1.x のまま `"A\0*.a\0\0B\0*.b\0\0"` を開き、ファイルの種類のコンボ（cmb1 = 0x470）の項目数を閉じる役に数えさせて記録する
3. **`-5` の戻り値の行き先が書かれていない** [T]（5.5、4.3）
   - 手順 6 は「`NONE` / `CANCELED` 以外なら system code を読む」、4.3 は「想定外の値 → `-3`」で、どちらも `-5` を除いていない
   - 層 2a の J-7 のテストで落ちるので A ではない

### 低優先度

**C（記述の整合）**

1. 8.3 が UI テスト計画 v1 を参照している。v2 にする [X]
2. 疑似コードの手順 3 に、`IntPtr` の出力ハンドルと確保の一覧が無い [T]
3. finally の解放は `!= IntPtr.Zero` のときだけ行う。そうしないと、版が合わず `-4` になった後にも `ntk_string_free` を呼び、J-7 の「以後ネイティブへ渡さない」に反する [T]
4. 閉じる役は手順をまず `;` で分ける（`WindowsDialogSamplePlayerTests.cs:494`）。7.3 の「行の残り」は「その手順の残り」に直し、「期待値に `;` を使わない」と書く [T]
5. J-4 の言い方をそろえる。5.3、E-8、7.2 にも「・空」を付ける [T]
6. 文字列が終わったら一覧も終わる、と 4.2 に書く。7.1 に `"Text\0*.txt"`（終わりの NUL が無い形 → 1 組）を足す [T]

## 不足項目

- なし

## 総合評価

v3 の A1 は解消し、**新しい A1 / A2 は無い。** B 3 件と C 6 件は、どれも書き足しで直る。
**v5 で B と C を直し、再レビューはしない。** 実装に進める状態になる。
