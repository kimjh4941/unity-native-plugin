# Windows Clipboard 実機確認結果 v1

## 基本情報

- 日付: 2026-09-09
- 機能名: clipboard / 対象プラットフォーム: Windows
- 実施環境: Windows 11 Home 10.0.26200 / **Mono** / Development Build 相当 / ACP **932**
- 実行ファイル: `D:\Build\Windows\Unity NativeToolkit.exe`（`52aae58` 相当のコードから 2026-09-09 11:08 ビルド）
- 手順: `artifact/designs/clipboard/2026-09-08-windows-clipboard-sample-scene-design-v3.md` 6 節・9 節
- 判定基準: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v8.md` 9.3（M-1 〜 M-24）
- サンプル実装: `artifact/results/clipboard/2026-09-08-windows-clipboard-implement-sample-scene-result-v3.md`
- ログ: `artifact/results/clipboard/logs/2026-09-09-windows-clipboard-verify-manual-session1.log`
  および `-session2.log`。**1 〜 5 節の主張はすべてこの 2 本から確認できる**。
  8 節（対応後の再確認）は `-session3.log` と `-session4.log`

## 0. 状態サマリー

| 区分 | 結果 |
|---|---|
| **公開 API の実行** | **33 / 33**（実施前 5 / 33） |
| **ボタンの押下** | **67 / 67**（計画 9.3 のカバレッジ照合） |
| 予期しない失敗 | **0 件**。発生した失敗はすべて意図して起こしたもの |
| 新規に判明した事項 | 6 件（4 節） |
| 見つかった問題 | A 1 / B 2 / C 2（5 節） |

**「初回は落ちる前提」としていたが、落ちなかった。** 28 種の API が初めてネイティブに対して実行され、
すべて設計の期待どおりに動いた。

---

## 1. ブロック別の結果

### ブロック A: 履歴 ON・フォアグラウンド

| M | 内容 | 結果 |
|---|---|---|
| M-1 | `Initialize` 成功 | **OK**。`apartment: MainSta`。`WrongApartment` / `ApartmentUnavailable` は返らない（V-1 実証） |
| M-23 | 隠し dispatch ウィンドウ | **OK**。タスクバー / Alt+Tab に出ない（**ネイティブ側でも未検証だった項目**） |
| M-2 | Copy Plain Text → メモ帳 | **OK**。末尾に余分な改行なし |
| M-3 | メモ帳でコピー → Paste | **OK**。`length=15 match=n/a`（事前に `Clear` してアンカーを落とした） |
| M-4 | Copy Html → Word / ブラウザ | **OK**。`length=31 match=match`。CF_HTML のヘッダ・ラッパーを剥がしたフラグメントが返る |
| M-5 | Copy Files → エクスプローラ | **OK**。`count=2 match=match`。**V-2（パス区切り）解消** |
| M-6 | Copy Image → Paste Image → ペイント | **OK**。`size=296 match=match`。**DIB フィクスチャがネイティブの `ValidateDib` を通過** |
| M-7 | Copy Multiple Formats → 複数アプリ | **OK**。メモ帳は平文、Word は HTML を選択 |
| M-8 | CF_TEXT + 非 ASCII | **OK**。`acp=932`。UTF-8 ACP でないため観測条件を満たす |
| M-9 | Copy (Sensitive) / (Exclude History) | **OK**。どちらも Win+V に出ない |
| M-10 | イベントの時系列 | **OK**。外部コピーを検知。`HistoryChanged` 発火 |
| M-11 | Get History | **OK**。`count=14 newestAgeSec=44 newestMatch=match` |
| M-14 | Restore / Delete | **OK**。件数が 14 → 13 |
| M-15 | Restore Twice In One Frame | **OK**。2 本目が `requestId=0` + `OperationBusy`、**1 本目は完走** |
| M-16 | Issue And Cancel In One Frame | **OK**。`Canceled` が 1 回だけ。二重配送なし |
| M-21 | 1 MB の往復 | **OK**。`length=1048576 match=match` |
| M-24 | Restore と ClipboardChanged の帰属 | **OK**。`done` の直後に隣接 |

### ブロック B: 非フォアグラウンド

| M | 結果 |
|---|---|
| M-13 | **OK**。別ウィンドウが前面のまま `NotForeground`。`Application.runInBackground = true` が効いている（レビュー v2 B-1 の対応が奏功） |

### ブロック C: 履歴 OFF

| M | 結果 |
|---|---|
| M-12 | **OK**。`history=False` / `HistoryDisabled` |
| M-17 | **OK**。予約が既存内容（`sample 154`）を破棄し、provider の内容（`sample 157`）に置換 |
| M-18 | **OK**。**provider は 1 形式につき 1 回**。`text=1 image=0` が消費者 2 つ（自 Paste・メモ帳）をまたいで不変 |
| M-19 | **OK**。**予約したまま Quit → 終了後もメモ帳に貼れた**。ネイティブ F2 の回帰確認 |
| M-20a | **OK**。`the manager is not active; draining in one attempt`。リトライ行なし。**成功で終わるのが正常** |

### ブロック D: 異常系

8 種すべて期待値どおり。**すべてに `ErrorMessage` が付いている**（S-3）。

| ボタン | 結果 |
|---|---|
| Copy Plain Text (null) | `InvalidArgument: text was null` |
| Copy Files (empty) | `InvalidArgument: paths was null or empty` |
| Copy Custom Format (blank name) | `InvalidArgument: formatName was null or blank` |
| Copy Multiple Formats (empty) | `InvalidArgument: items was null or empty` |
| Restore (blank id) | `InvalidArgument`。**`requestId=0`（受付前拒否）** |
| Restore (unknown id) | **`ItemDeleted`。`requestId=1`（受理されネイティブまで到達）** |
| Cancel (unknown id) | `InvalidParameter` |
| Copy After Shutdown | `NotInitializedByHost`。`TryShutdown` は `completed=True` |

### 9 章: History (Await)

**Awaitable 5 種はリポジトリ全体で Windows Clipboard にしかない。** 6 ボタンすべて `accept` → `done` が 1:1。

| ボタン | 結果 |
|---|---|
| Get History (Await) | OK `count=16` |
| **Get History (Await + Cancel)** | **`NG code=Canceled ... cancelledWithoutThrowing`**（S-9） |
| Get Availability (Await) | OK |
| Restore (Await) | OK |
| Delete (Await) | OK |
| Clear Unpinned (Await) | OK |

### S 観点

| S | 結果 |
|---|---|
| S-1 | **OK**。TopMenu → Clipboard で本画面が開く |
| S-3 | **OK**。失敗時に必ず `ErrorMessage`、成功時に `ErrorCode == None` |
| S-5 / S-6 | **OK**。2 往復しても `OnDisable` → `OnDestroy` が対で出て購読が二重にならない |
| S-7 | **OK**。ワーカーから呼ぶと `MainThreadRequired`、**`callbackOnMainThread=True`**（設計 7.1 実証） |
| S-9 | **OK**。Await のキャンセルが例外ではなく結果として返る |
| S-2 / S-4 / S-8 | **合格**（9 節。2026-09-10 にログから事後確認） |

---

## 2. 8.8 `GetPreferredFormat` の期待値

| クリップボードの状態 | 期待 | 結果 |
|---|---|---|
| custom のみ | 空文字 | **OK** `empty=True format=(none)` |
| text + HTML | `CF_UNICODETEXT`（HTML Format は返らない） | **OK** |
| files のみ | `CF_HDROP` | **OK** |
| image のみ | `CF_DIB` | **OK** |

ネイティブから「機能側への報告事項」として上がっていた仕様が、こちら側でも確認された。

---

## 3. カバレッジ照合（計画 9.3 / N-3）

**67 / 67 押下。未操作 0。**

ネイティブ側は 55 シナリオを全消化したあとで **49 ボタン中 9 ボタンの未操作**が判明した。
本件は照合を独立した工程として置いたため、同じ漏れを起こしていない。

M 項目だけを追った時点では **55 / 67** で、**12 ボタンが残っていた**。
残っていたのは `Home` / `Try Shutdown` / `Shutdown With Drain` / `Copy Plain Text (empty)` /
`Paste Plain Text (after Clear)` / `Paste Html (text only)` / `Clear Unpinned` / `Cancel Last` /
`Request + Immediate Shutdown` / `Copy From Worker Thread` / `Get History From Worker Thread` /
`Delete Temp Files`。**S-7 の 2 ボタンと 8.5 の 9 種目が含まれていた。**

**照合工程が無ければ、S-7 は未実施のまま「完了」と記録されていた。**

---

## 4. 実機で初めて判明した事項

| # | 内容 |
|---|---|
| 1 | **D-8 / ネイティブ F1 が Unity でも再現。** 履歴 ON で予約すると `Render: text=1 image=1`、OFF では `image=0`。誰も要求していない画像 provider が呼ばれており、**実体化しているのは履歴サービス**と特定できる。ブロック C を履歴 OFF で実施した判断が正しかったことの実証 |
| 2 | **重複フォーマットは `InvalidParameter` で拒否される。** `WindowsClipboardPayloads.cs` のコメントが述べていただけで、コードで確認されたのは初 |
| 3 | **`CanShutdownNow` が `False` でも shutdown は成功しうる。** `value=False` の直後の `Shutdown While Disabled` が成功した。設計の「次の shutdown が成功する保証ではない」が逆向きにも成り立つ |
| 4 | **`ClipboardChanged` の回数は書き込み方に依存する。** メモ帳の Ctrl+C では 1 回、別の場面では 2 回。設計 2.2 の「実測 3 回」は PowerShell `Set-Clipboard` での観測であり、**普遍的な回数ではない** |
| 5 | **`Draining` は平常時 1 フレーム未満。** リトライ（`attempt 1 not finished yet`）が観測されたのは `Request + Immediate Shutdown` の 1 例のみで、そのとき `Canceled` が返った |
| 6 | **`roaming=False`。** R-5（Exclude Roaming の効果は確認不能）の根拠が実機で確定 |

### 4.1 特筆: `Request + Immediate Shutdown`

1 フレームで要求とシャットダウンを出したときの並びが、設計の保証を 4 つ同時に示した。

```
#70 [accept] getClipboardHistory requestId=2
#71 [accept] uninitClipboardManager awaited
[DrainRequestRegistry] draining 1 request(s)          ← 保留要求を同期配送
#73 [done]  getClipboardHistory call=#70 NG code=Canceled   ← 捨てられずに完了
[AdvanceDrain] attempt 1 not finished yet: Canceled          ← 唯一のリトライ観測
[OnRequestCompletedNative] unknown request id 2              ← 遅れて来た完了を破棄
#75 [done]  uninitClipboardManager call=#71 OK code=None
```

- 要求が捨てられない（`Awaitable` の永久未完了が起きない）
- `TryShutdown` 経路が保留要求を**同期配送**する（**レビュー v2 A-4 の訂正が実機で裏付けられた**）
- 競合下でも exactly-once（遅れて来たネイティブ完了を破棄）
- `Draining` が複数フレーム続く唯一の実例

---

## 5. 見つかった問題

| # | 内容 | 区分 |
|---|---|---|
| 1 | **`Force Initialize While Draining` が意図した経路を踏まない。** `Draining` が 1 フレームに閉じているため、同一フレームでは早すぎ、次フレームでは遅すぎる。ボタンの説明文は「expected ShuttingDown」と表示するが `OK` が返る | **A** |
| 2 | **他形式のコピー 1 回で `Copy Files` が使えなくなる。** `ReplacedClipboard` が全アンカーを落とす際に `_lastFilePaths` も消える。ディスク上のファイルは残っているのに `noTempFiles` になる。**アンカー（往復判定用）とフィクスチャの所在を分けていない** | **B** |
| 3 | **S-5 / S-6 の手順に Manager 状態の前提が無い。** Manager は `DontDestroyOnLoad` なので画面を出入りしても `ShutDown` のまま。`Initialize` を挟まないと `ClipboardChanged` が来ず、判定が成立しない | **B** |
| 4 | 失敗した読み出しで `empty=False count=0` と表示される。矛盾ではないが「空でないのに 0 件」と読める。`match` は `n/a` に落としてあるので、`empty` も同様にすべき | **C**（**解消済み**。8 節） |
| 5 |（**解消済み**。8 節）終了時に Manager が再生成される。`OnDisable` が破棄済みの `Instance` を無条件に触るため、`GameObject` が 1 つ余分に作られる。`Recreated after destruction; all operations are rejected` として無害だが、`IsTerminated` を見れば避けられる | **C** |

**1 はレビュー v2 の A-1 に対する私の修正が不十分だったもの。**
「同一フレームでは早すぎる」までは正しかったが、**次フレームでは遅すぎる**ことを確認していなかった。

**2 は今朝の A-3 修正（アンカーの寿命を 1 本の規則に）の副作用。** 規則自体は正しいが、
`_lastFilePaths` は往復判定のアンカーではなくフィクスチャの所在であり、同じ規則に載せるべきではなかった。

**1 の「`Draining` が 1 フレームに閉じている」は誤りだった。** 対応の過程で判明したもので、
実際には 1 フレームまるごと開いている。詳細と訂正は 8.1。1 〜 3 の対応と再確認は 8 節。

---

## 6. 未実施項目

**Windows Clipboard の残件はこの表が唯一の置き場。**
サンプル実装結果 v3 の 7.1 にも同じ項目が並ぶが、あれは実装工程時点のもので更新しない。
**追記するならここ。** 2 箇所に分けると片方だけが更新される。

| # | 内容 | 理由 |
|---|---|---|
| M-20b | `ShutdownTimeout` の観測 | ネイティブに `NotYet` を返させる条件が要る。外部プロセスにクリップボードを握らせ続ける道具立て（層 3） |
| Exclude Roaming の効果 | — | 同一 Microsoft アカウントの 2 台目デバイスと「デバイス間で同期」有効が必要。本機は `roaming=False`。ネイティブ側も同じ理由で未実施 |
| D-9 | 予約失敗時の世代管理 | `EmptyClipboard` 失敗や個別 `SetClipboardData` 失敗をサンプルから起こす手段が無い |
| M-22 | IL2CPP での再実施（実機） | 本回はすべて Mono。**IL2CPP ビルドは通した**（10 節）が、実機では動かしていない。 ただし `MonoPInvokeCallback` 自体は iOS / Android の実機確認で AOT 上を通っている（2026-09-10 訂正）。残るのは Windows 固有の代理検証であり、優先度は下がった |

**確認していないものを OK として扱っていない。**

---

## 7. 次

1. ~~**5 節の A 1 件と B 2 件の対応を決める。**~~ **対応済み。8 節。**
   ボタンの撤去も外部ツール前提への分類も不要だった
2. **M-22（IL2CPP）** — ~~`[MonoPInvokeCallback]` の実挙動はここでしか確認できない~~
   **これは誤り**（2026-09-10）。iOS / Android の実機確認が IL2CPP 上で行われており、
   属性の実挙動はそちらで通っている。Windows の IL2CPP は**代理検証**であり、
   ブロック A 〜 C 全部ではなく**逆 P/Invoke に触れる 7 〜 8 ボタンで足りる**。
   先に IL2CPP ビルドだけ通せば、AOT に敵対するコードの有無は実機作業なしで分かる
3. ~~**S-2 / S-4 / S-8 の事後確認**~~ **実施済み。9 節。**
   残るのは画面の未消化カウンタ表示のみ（9.3）
4. ネイティブ側への共有: **M-23（隠しウィンドウが出ない）はネイティブでも未検証だった項目**であり、
   Unity Player 上で初めて確認された

---

## 8. 5 節への対応と再確認（同日）

対応後に全ゲート（EditMode 807 / PlayMode 181 / Win64 ビルド）を通し、
**同じ実機で再確認した**。実行ファイルは 2026-09-09 14:19 ビルド。

| # | 対応 | 再確認 |
|---|---|---|
| 1 | 保留要求を先に出して drain を 1 回で終わらせない。**Initialize を `LateUpdate` へ移す** | **到達**。`#7 [call] initClipboardManager NG code=ShuttingDown frame=1`（session4） |
| 2 | `_lastFilePaths` を `_tempFilePaths`（フィクスチャの所在）と `_lastWrittenFilePaths`（往復アンカー）に分割 | **解消**。`copyPlainText` を挟んでも `copyFiles OK count=2` / `pasteFiles match=match`（session3） |
| 3 | 計画 v3 の S 表の下と、画面の Events 注記に前提を明記 | 文書のみ |
| 4 | 失敗した読み出しは `empty` も長さ・件数も **`n/a`** を出す。4 書式すべて | **解消**（2026-09-10。`cf19a94`） |
| 5 | `OnDisable` が `IsTerminated` を見てから `Instance` を取る | **解消**（同上） |

### 8.1 1 が 2 度外れた理由

**窓は狭くなかった。覗き穴の位置が 1 フレームずれていた。**

```
frame N   : クリック → ShutdownWithDrain（session を作るだけ。state は Running）
frame N   : Update → AdvanceDrain 試行 1 → NotYet → state = Draining   ← 窓はここから
frame N+1 : Update → AdvanceDrain 試行 2 → 成功 → state = ShutDown     ← ここまで
frame N+1 : Update の後にコルーチンが再開                              ← もう閉じている
```

`Draining` は **1 フレームまるごと**開いていた。5 節で「1 フレームに閉じている」と書いたのは誤り。
届かなかったのは `yield return null` が **Update の後・次フレーム**に再開するためで、
クリックしたフレーム N には戻れない。コルーチンが最初に見られるのは**必ず試行 2 の後**になる。

`LateUpdate` は **Update の後・同じフレーム**であり、この窓の中に入る唯一の点。

3 フレーム分の予算を残したのは、**クリックが Update の前に処理される保証が無い**ため。
`Running` 中の `Initialize` は冪等成功を返すだけで drain を壊さないので、空振りしても害が無い。

### 8.2 M-20b は依然として未実施

1 が到達したのは `ShuttingDown`（`Draining` 中の拒否）であって、
`ShutdownTimeout`（ネイティブが `NotYet` を返し続ける）ではない。**6 節の M-20b は残る。**

---

## 9. S-2 / S-4 / S-8 の事後確認（2026-09-10）

6 節が「ログから事後確認は可能だが、実施中に個別の目視判定を行っていない」としていた
3 項目を、保全済みログ 4 本（900 行）から判定した。**実機は使っていない。**

```
python3 scripts/check_windows_clipboard_sample_log.py
```

**判定はスクリプトに残した。** M-22（IL2CPP での再実施）で新しいログが出たとき、
同じ 3 項目を同じ基準で再判定できる。手で書き直すと前回と同じ判定をした保証が無い。
Windows Clipboard 専用である（`[call]` / `[accept]` / `[done]` の書式は
`WindowsClipboardSampleResult.cs` にしかなく、他 15 本のサンプルは持たない）。

| # | 観点 | 結果 |
|---|---|---|
| S-2 | 全ボタンが例外を出さず、ログの操作名が押したボタンと一致する | **合格**（67/67） |
| S-4 | クリップボードの内容が画面にもログにも出ていない | **合格** |
| S-8 | `[accept]` と `[done]` の未消化が 0 に戻る | **合格**（32/32） |

### 9.1 S-2 — 前提の確認から

まず**ログに全ボタンが載っているか**を確かめた。載っていなければ、載っている分の判定にしかならない。

- ログ中の `[OnXxxClicked]` は 68 種。うち 1 つは TopMenu の入口（`OnClipboardClicked`）
- 残る 67 が UXML の 67 ボタンと**過不足なく一致**した（未押下 0、未定義 0）

判定は **UXML のボタン文言と、ログの操作名**の対で行った。
`result.Operation` は Manager が返す値であり、サンプルが付けたラベル
（`copy.files` など）とは別に出ている。**ラベル同士を比べても結線ミスは出ない。**

ハンドラ名と操作名の文字列一致では判定にならない。
`Try Shutdown` → `uninitClipboardManager` は名前が違うが**正しい**（Manager の操作名定数）。
67 行すべてを目視し、**文言と操作が食い違う行は無かった。**

例外・`LogError` は 0 件。

### 9.2 S-4 — 出ているのは長さと件数だけ

フィクスチャの文字列と、書き出したファイルの所在をログ全体から検索した。**すべて 0 件。**

| 探した文字列 | 件数 |
|---|---|
| `NativeToolkit clipboard sample` | 0 |
| `<b>Hello</b> from NativeToolkit` | 0 |
| `native-toolkit-sample-payload` | 0 |
| `한글` / `🚀` | 0 |
| `nativetoolkit-clipboard-sample-`（一時ファイル名） | 0 |
| `AppData` / `\Temp\` / `.tmp` / `.txt` | 0 |

読み出し行が出しているのは次の形だけである。

```
[call] pastePlainText OK code=None empty=False length=1048576 match=match
[call] pasteImage     OK code=None empty=False size=296       match=match
[call] pasteFiles     OK code=None empty=False count=2        match=match
```

**`match` は往復一致の bool であり、内容ではない。**

### 9.3 S-8 — 総数の一致は 1:1 を意味しない

`[accept]` 32 / `[done]` 32 で総数は合う。ただしそれだけでは
「2 回配送された 1 件と、来なかった 1 件」を見逃す。**call id ごとに突き合わせた。**

| セッション | accept / done | 未消化 | accept 無しの done | done が 2 回 | 操作名の不一致 |
|---|---|---|---|---|---|
| session1 | 18 / 18 | なし | なし | なし | なし |
| session2 | 10 / 10 | なし | なし | なし | なし |
| session3 | 2 / 2 | なし | なし | なし | なし |
| session4 | 2 / 2 | なし | なし | なし | なし |

4 セッションとも**終了時点で未消化 0**。exactly-once（設計 8.4）と整合する。

**画面の未消化カウンタ自体はログに出ていない。** 判定したのは `[accept]` と `[done]` の
対応であって、画面表示が 0 に戻る様子ではない。表示の確認は目視のまま残る。

### 9.4 6 節の更新

S-2 / S-4 / S-8 の行は**未実施ではなくなった**。6 節の表と併せて本節を読むこと。

---

## 10. IL2CPP ビルド（2026-09-10）

**Windows を IL2CPP でビルドしたのは初めて。** 実機は使っていない。
目的は「AOT に敵対するコードが無いか」の 1 点で、これはビルドだけで分かる。

| | |
|---|---|
| 結果 | **`Succeeded`** |
| 所要 | 16 分 54 秒 |
| 成果物 | `GameAssembly.dll` が生成された（IL2CPP であることの確認） |
| エラー 2 件 | **ライセンスのハンドシェイク**。バッチ起動で毎回出るもので、Mono ビルドのログにも同じ 2 行がある。コンパイルエラーではない |

### 10.1 何が言えて、何が言えないか

**言える**: 逆 P/Invoke の宣言・属性・マーシャリング指定が AOT のコード生成を通る。
IL2CPP が扱えない構文はこのパッケージに無い。

**言えない**: コールバックが実際に発火するか。これはビルドではなく実行の問題であり、
`GameAssembly.dll` を起動して初めて分かる。

### 10.2 ただし、この検証の価値は下がっている

同日の調査で、**iOS / Android の実機確認が IL2CPP 上で実施済み**と判明した
（`artifact/DEVICE_VERIFICATION_RECORDS_MISSING.md`）。
`[MonoPInvokeCallback]` の実挙動はそちらで通っている。
本節は **Windows 固有の代理検証**であり、AOT 全般の初回確認ではない。

### 10.3 手順（再実行するとき）

バックエンドは `PlayerSettings.SetScriptingBackend` で切り替えた。
**`ProjectSettings.asset` を直接書き換えない。**
Windows のビルドプロファイルが `Standalone: 0` の写しを持っており、引き戻される。

検証用の Editor スクリプトは実行後に削除し、`ProjectSettings/` と
`Assets/Settings/Build Profiles/` を `git checkout` で戻した。
`scriptingBackend` に `Standalone` の行が無いことを確認済み。

