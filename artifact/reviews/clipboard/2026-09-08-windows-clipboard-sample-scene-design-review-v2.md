# レビュー結果 v2

- 日付: 2026-09-08
- 対象ファイル: `artifact/designs/clipboard/2026-09-08-windows-clipboard-sample-scene-design-v2.md`
- 機能名: clipboard
- プラットフォーム: Windows
- レビュアー: Codex CLI `gpt-5.5` / reasoning effort high（read-only サンドボックス）
- 前ラウンド: v1（Claude サブエージェント 4 名）。**レビュアーを替える条件を満たす**

---

## 0. この文書の読み方

Codex の生の指摘をそのまま採らず、**全件を実物と突き合わせて採否を決めた。**
1 件は却下している（A1-1）。根拠は 2 節に記す。

| Codex の区分 | 件数 | 検証後 |
|---|---|---|
| A1 | 1 | **0**（1 件却下） |
| A2 | 0 | 0 |
| B | 3 | **3 件とも成立** |
| C | 1 | **成立** |

---

## 強み（実物と照合して確認できたもの）

- 公開 API の分類が実装と一致する。履歴の callback 版が `uint requestId` を返し、
  Awaitable 版は返さない点まで合っている（`WindowsClipboardManager.cs:1802` / `1818` / `1837` /
  `1855` / `1872` / `2066` / `2090` / `2115` / `2140` / `2164`）
- コンパイルガードの二重構造の理解が正しい。クラス `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`、
  P/Invoke 境界 `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`（`WindowsClipboardManager.cs:3-5` / `3810-3815`）
- deferred provider の契約 — Unity API を呼ばない / 値でキャプチャ / 1 形式 1 回生成して cache —
  が実装と一致（`WindowsClipboardManager.cs:1953-1958` / `2250-2269` / `2272-2287`）。
  **v1 の最大の誤りだった箇所であり、v2 の修正が正しかったことの独立確認になる**
- `TryShutdown` がイベントも callback も発火しない例外扱いである点が実装と一致
  （`WindowsClipboardManager.cs:879-888`）

---

## 改善点

### 却下（Codex は A1 としたが、成立しない）

#### 却下-1: 「DIB フィクスチャが 96 バイトと書かれている」

Codex の主張: 計画書が「96 バイト」と書きつつ「40B + 256B」とも書いており矛盾している。

**成立しない。計画書はそもそも 96 と書いていない。**

```
451:| Image | **8x8 / 32bpp / `BI_RGB` の DIB、296 バイト**（`BITMAPINFOHEADER` 40B + 画素 256B）。…
```

40 + 256 = 296 で、括弧内と本文は一致している。v1 にも「96」の記述はない
（v1 はバイト数を書かず `P-1 要検証` としていた）。Codex の桁落ちによる誤読。

引用位置も合っていない（Codex は「8.2 行 356」としたが、v2 の 356 行目は 7.3 節）。

**したがって A1 は 0 件。** ただし Codex の副次的な提案（DIB を機械検査で固定する）は
B-4 として採用する。

### B（検証手段の穴）

#### B-1: M-13 は非フォアグラウンド経路を踏まない可能性がある

- 該当箇所: 6 節ブロック B（行 299）、8.1、8.7
- 根拠: `ProjectSettings/ProjectSettings.asset:85` が **`runInBackground: 0`**。
  `WindowsClipboardManager.cs:1802-1808` は `GetHistory` を callback 非同期で開始する
- 何が起きるか: 「`Delayed History Call (5s)` を押して 5 秒以内に別ウィンドウを前面にする」だけでは、
  Player がバックグラウンドでフレームを回さない。遅延発火も callback 表示も**フォアグラウンド復帰後**に
  動く可能性があり、その場合 M-13 は `NotForeground` を踏んだ確認になっていない。
  **しかも失敗ではなく成功として観測される**（前面に戻ってから成功が返るため）
- 提案: サンプル起動時に `Application.runInBackground = true` を設定することを計画に明記する。
  判定条件も「**別ウィンドウが前面のまま** callback が表示され、`NotForeground` であること」にする

#### B-2: M-20 は `ShutdownTimeout` に到達するとは限らない

- 該当箇所: 7.6 (a)、6 節ブロック C（行 312）、11 節 R-2（行 591）
- 根拠: `WindowsClipboardManager.cs:2886-2891` — 試行の `progress` が `NotYet` 以外なら
  **`force` 分岐に入る前に `FinishDrain` して return する**。
  `force || spent` で `ShutdownTimeout` にするのは `NotYet` のときだけ（`2905-2921`）
- 何が起きるか: 正常な実機では 1 回目の native uninit が完了するため、
  `Shutdown While Disabled` は**成功で終わる**。R-2 の「(a) で `ShutdownTimeout` 自体は確認する」は
  成立しない。このまま実機に持っていくと、正しく動いた shutdown を M-20 の失敗として読む
- 提案: M-20 を 2 つに分ける。
  - **M-20a**: disabled 時に `force` 分岐（1 回打ち切り）へ入ること。
    判定は「`no attempts left` ログが出ること」ではなく「**再試行せずに 1 回で終わること**」
  - **M-20b**: `ShutdownTimeout` の観測。native が `NotYet` を返す条件が要る。
    **(b) と同じく未実施・要検証に戻す**
- **これは私（v2 の作成者）の誤りである。** 前回「`force` 分岐で `ShutdownTimeout` に到達する」と
  報告したが、早期 return を見落としていた

#### B-3: M-8 は「欠落を必ず観測できる」フィクスチャになっていない

- 該当箇所: 6 節ブロック A（行 286）、8.2（行 455）、10 節
- 根拠: `WindowsClipboardPayloads.cs:55-56` —
  「CF_TEXT is encoded by the native layer with the system ANSI code page, so non-ASCII text
  **can be lost** for that format」
- 何が起きるか: 契約は「欠落し得る」であって「非 ASCII なら必ず欠落」ではない。
  ANSI コードページと文字の組み合わせによっては表現できる。
  Windows 11 は ACP を UTF-8 にする設定があり、その環境では欠落しない。
  M-8 が「欠落を確認」と断定しているため、**正常な実装を環境依存で失敗扱いする**
- 提案: フィクスチャを「現在の ACP で表現不能な文字を含む」ものに固定する。
  判定を「`CF_UNICODETEXT` は一致、`CF_TEXT` は不一致または `?` への lossy 変換」にする。
  **前提条件に「ACP が UTF-8 でないこと」を明記**し、UTF-8 ACP 環境では要検証として記録する

#### B-4: DIB フィクスチャに機械検査がない（Codex の副次提案）

- 根拠: 既存テストは `CopyImage(new byte[0])` の拒否までで、DIB 構造を固定していない
  （`WindowsClipboardManagerIntegrationTests.cs:1113-1119`）。
  managed 側は `dib == null || dib.Length == 0` しか見ない（`WindowsClipboardManager.cs:1245-1248`）
- 何が起きるか: フィクスチャ生成を後から壊しても C# は通る。実機で「画像機能が動かない」に見える
- 提案: 9 節の自動テストに 3 本目を足す。
  `dib.Length == 296` / `biSize == 40` / `biPlanes == 1` / `biBitCount == 32` /
  `biCompression == 0` / `biSizeImage == 256`

### C（記述の整合）

#### C-1: M-18 の判定条件が章間で矛盾している

- 6 節 行 310: 「判定は『貼り付け先に内容が届くか』。**provider 呼び出し回数ではない**」
- 8.3 行 483: 「provider が **1 回呼ばれ**、貼り付け先に内容が届くこと」
- 根拠: `WindowsClipboardManager.cs:2258-2269`（size phase で呼び cache）、
  `2272-2287`（data phase は cache を使う）。**1 回であることは観測に値する**
- 提案: 8.3 に統一する。「provider 呼び出し回数 1 回、かつ貼り付け先に内容が届くこと」
- **v1 の A1 を直したときに私が持ち込んだ矛盾である**（「回数で判定するな」と書いた側が古い）

---

## 不足項目

| # | 内容 |
|---|---|
| 1 | M-13 のための `Application.runInBackground` 設定（B-1） |
| 2 | DIB フィクスチャの機械検査（B-4） |
| 3 | M-20b で native に `NotYet` を返させる実機条件（B-2）。現状 (a) と混同している |
| 4 | M-8 の ACP 前提（B-3） |

---

## 総合評価

**A1 は 0 件。** Codex が挙げた唯一の A1 は誤読であり、成立しない。

止める基準（`agent-rules/workflows/review-document/workflow.md` 8 節）の充足状況:

| 条件 | 状態 |
|---|---|
| A1 が 0 | **満たす** |
| レビュアーを替えて 1 回通す | **満たす**（v1 = Claude 4 名 → v2 = Codex `gpt-5.5` high） |
| 機械照合が通る | **対象外**。`check_design_consistency.py` はサンプルシーン計画書を見ない（残件） |
| 直さない残件が文書に明記されている | v2 の 11 節に R-1 〜 R-4。**本レビューの B-1 〜 B-4 を追記する必要がある** |

### 収束の状況

| ラウンド | レビュアー | A1 |
|---|---|---|
| v1 | Claude サブエージェント 4 名 | 13 |
| v2 | Codex `gpt-5.5` high | **0** |

v1 の A1 13 件のうち中核だった「実装の構造の誤認」5 件は、
**別モデルが実物のコードを読み直しても再発しなかった**。v2 の修正が正しかったことの独立確認になる。

残った 4 件はすべて **B（手動確認の再現条件）** である。指摘の種類が
「契約の誤り」から「確認手順の詰め」へ移っており、これは設計の収束を示す。

ただし B-1 / B-2 / B-3 は**放置すると実機で誤った結論を出す**種類の穴である。
とくに B-2 は正常動作を失敗と読み、B-1 は未確認を成功と読む。
**いずれも安いドキュメント修正で塞げるため、v3 で直してから実装へ進むことを推奨する。**
