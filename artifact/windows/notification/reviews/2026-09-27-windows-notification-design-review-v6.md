# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/windows/notification/designs/2026-09-27-windows-notification-design-v6.md
- 前回: artifact/windows/notification/reviews/2026-09-27-windows-notification-design-review-v5.md
- 機能名: notification
- プラットフォーム: Windows
- レビュー: v5 との差分に絞り、2 本で行った。サブエージェント 1 本（`Windows.Data.Json` と `GetCurrentPackageFullName` を実測）と Codex。どれも読み取りのみ
- 出典の略記: [S] = サブエージェント、[X] = Codex

---

## v5 の指摘の扱い

| v5 | 判定 | 理由 |
|---|---|---|
| A1-1 `audio.loop` | 解消 | `ValidatePayload`（`1.11.0:...cpp:489-528`）が読むキー（`duration`、`audio`、`audio.loop`、`buttons`、その各要素）と 4.2 の表は矛盾しない。mute の `loop` は C ABI が保存し（`NotificationContentCApi.cpp:215-229`）、検証が見て、ビルダーが捨てるので 1.x と一致 [S][X] |
| A1-2 小数表記で 0 に丸まる値 | 解消（境界の数は C-1） | [S][X] |
| A1-3 パッケージの判定 | 解消 | 非パッケージの PowerShell で長さ 0・`NULL` を渡すと 15700（実測）[S][X] |
| B-1 作り直しのテストの隔離 | 一部 | 方針は正しいが、スクリプトの書き方が足りない（新 B-1）[S] |
| B-2 `LogError` の箇所 | 解消 | 数え方は C-3 [S][X] |
| C | 解消 | 深さは配列でも object でも、コンテナ 512 個は通り 513 個で `0x8007050C`（実測）[S][X] |

## 強み

- 1.x の読み方と 2.0.0 の検証・ビルダーの組み合わせが、mute の `loop` のような細部まで一致している [S]
- 新しい A1 / A2 は、2 本とも無い [S][X]

## 改善点

### 高優先度

- A1: なし

### 中優先度

- A2: なし

**B（検証手段の穴）**

1. **作り直しのテストを別の Player で流す書き方** [S]
   - `QuitsThePlayer` の実行は、バックグラウンドで流してタイムアウトで止め、`Quit.xml` を `report_tests` にかけずに外から判定する（`verify_unity_windows.sh:309-366`）。作り直しのテストは普通に結果を返すので、この形を写すと失敗が合否に数えられない
   - 修正:
     - 前面で流し、専用の XML と log に書き出す（`$PT_XML` は後の :381 で使うので上書きしない）
     - その XML を `report_tests` にかけ、`total=0` なら「テストが流れていない」として失敗にする（:260-264 と同じ）
     - カテゴリ名は `QuitCategory` と同じく `sed` でソースから読み、見つからなければ CANNOT RUN
     - 本体の実行の除外は、:221 と :226 の両方の分岐に足す
     - 流す位置は :244-246 の後で、M-19 の Player.log の読み取り（:328-330）とぶつからないところ
2. **GetAll の per-call が例外を投げたときのテストが無い** [X]。`LogError` が出て、`GetAllNotificationsCompleted` が出ないことを確かめる（1.x の `WindowsNotificationManager.cs:393-401`）

### 低優先度

**C（記述の整合）**

1. 小数表記の境界は「`0.` の後に 0 が **323 個**以上」（実測: 322 個では 9.88e-324、323〜5000 個では 0）[S]
2. 配列（`buttons` / `textBoxes` / `comboBoxes` / `items`）の要素は、1.x が `GetObject` で読むので、object でなければ 7。表に書き、層 1 に `"buttons":[1]` を足す [S]
3. 1.x の `LogError` は「4 種類（呼び出しは 5 箇所）」。活性化の受け口が 2 行ある（`WindowsNotificationManager.cs:496,502`）[X]
4. 8.1 の未検証の欄に、外部の場所を持つパッケージ（sparse）のプロセスも 122 を返し、J-8 では bootstrap を呼ばない扱いになる、と一文足す（Microsoft Learn には、この形も bootstrapper を使うと読める記述がある。実機では確かめていない）[S]

## 不足項目

- なし

## 総合評価

新しい A1 / A2 は無い。B 2 件と C 4 件は、どれも書き足しで直る。

**v7 で B と C を直し、再レビューはしない。** 実装に進める状態になる。
