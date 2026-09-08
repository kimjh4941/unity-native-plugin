# Windows Clipboard サンプルシーン 実装結果 v1

## 基本情報

- 日付: 2026-09-08
- 機能名: clipboard / 対象プラットフォーム: Windows
- サンプル計画: `artifact/designs/clipboard/2026-09-08-windows-clipboard-sample-scene-design-v3.md`
- 機能実装結果: `artifact/results/clipboard/2026-09-08-windows-clipboard-implementation-feature-result-v6.md`
- ブランチ: `feature/UNT-11`

## 0. 状態サマリー

| 区分 | 状態 |
|---|---|
| 層 1 EditMode | **792 / 792 passed**（本実装で **+43**） |
| 層 2a PlayMode | **181 / 181 passed** |
| **層 0 Win64 Player ビルド** | **成功** |
| 実機動作確認 | **未実施。** 本工程はサンプルを作るところまで |

一括実行: `scripts/verify_unity_windows.sh` → `all gates passed`

**このサンプルはデモではなく検証装置である。** 公開 API 33 種のうちネイティブに対して
実行されたことがあるのは 5 種だけで、残り 28 種はコードレビューでも Editor テストでも
到達できない。**このサンプルのボタンを押すことが唯一の実行手段**になる。

---

## 1. 変更ファイル

### 1.1 新規作成（8）

| ファイル | 行 | 内容 |
|---|---|---|
| `Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExample.uxml` | 157 | 画面定義。66 ボタン |
| `Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExampleStyle.uss` | 177 | macOS 版から複製し、`windows-` 接頭辞に変更。状態行と入力欄のスタイルを追加 |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardManagerExampleController.cs` | 1519 | 操作と結果表示 |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardSampleResult.cs` | 312 | 結果整形・状態追跡 |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardSampleFixtures.cs` | 223 | フィクスチャ生成と後始末 |
| `Tests/Runtime/WindowsClipboardSampleSceneWiringTests.cs` | 332 | 結線検証（14 件） |
| `Tests/Runtime/WindowsClipboardSampleResultTests.cs` | 244 | 整形・状態遷移（17 件） |
| `Tests/Runtime/WindowsClipboardSampleFixtureTests.cs` | 215 | フィクスチャの形（12 件） |

すべて `Windows` 接頭辞。クラスガードは `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`（P1 / P5 適合）。

### 1.2 既存変更（2）

| ファイル | 変更 |
|---|---|
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowWindowsClipboard` を追加。`RemoveExistingControllers` の Windows ブロックに `RemoveIfExists<WindowsClipboardManagerExampleController>` を追加 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | 購読ガードに `UNITY_STANDALONE_WIN` を追加、ログ文言 2 箇所、`OnClipboardClicked` に `#elif UNITY_STANDALONE_WIN` 分岐を追加 |

### 1.3 非変更

- 他プラットフォームの `Android*` / `Ios*` / `Mac*` — **0 件**（P2 適合）
- `Runtime/Clipboard/` の実装本体 — **サンプルのために製品コードへ試験用の細工を入れていない**
- `Runtime/Common/` — 0 件（P4 適合）
- `ProjectSettings/` — 0 件。`runInBackground` はコントローラの `OnEnable` で設定し `OnDisable` で戻す（計画 7.9）

---

## 2. 計画どおりに実装したもの

| 計画 | 実装 |
|---|---|
| 内側コンパイルガードを置かない（7.3） | 置いていない。Editor でも Manager を呼び `PlatformUnavailable` が観測できる |
| シーケンス番号つきの時系列ログ（4.3） | `#seq [kind] operation ...`。kind は `call` / `accept` / `done` / `event` / `local` |
| 操作名は `result.Operation` から出す（S-2） | `Call` / `Done` は結果の `Operation` を出力。ボタンのラベルは使わない |
| クリップボード内容を一切出さない（4.4） | 長さ・サイズ・件数・一致 bool のみ。`ErrorMessage` は出す（固定リテラルのため） |
| provider は Unity API を呼ばない（8.3） | `Interlocked.Increment(ref s_renderCount)` と値キャプチャした `byte[]` のみ。表示は `Update` |
| DIB は 8x8 / 32bpp / `BI_RGB` / 296 バイト（8.2） | `BuildDib()`。機械検査つき |
| パスは `Path.GetFullPath` で正規化（8.2） | `CreateTempFiles()`。機械検査つき |
| Await の cancel は例外を投げない（7.4.1） | 全 6 await 地点が `Canceled` で早期 return。機械検査つき |
| `StateTextBlock` で lifecycle を追跡（4.1） | `WindowsClipboardSampleResult.Advance` の純粋関数。単体テストつき |
| UI を無効化しない（7.8） | 全ボタン常時有効。`OperationBusy` と `Copy After Shutdown` を観測可能に保つ |
| `runInBackground` を有効化（7.9） | `OnEnable` で `true`、`OnDisable` で元の値へ |
| 未消化件数を常時表示（4.3） | `Pending:` として状態行に表示 |

---

## 3. 実装時に決めたこと（計画に無かった判断）

**計画由来と区別するため独立させる。**

| # | 判断 | 理由 |
|---|---|---|
| I-1 | `Paste Plain Text (after Clear)` を **Paste セクションに置き、Errors セクションには置かない** | 計画 8.5 は Errors を 9 種としつつ、同じ表で「**失敗ではない**」と書いている。両方に置くと同一操作のボタンが 2 つになる。Errors は 8 ボタン、9 種目は Paste 側にあると Errors セクションの説明文に明記した |
| I-2 | ACP は `CultureInfo.CurrentCulture.TextInfo.ANSICodePage` で表示 | 計画 8.2.1 は「`GetACP()` 相当」を要求。`GetACP` は P/Invoke になり、サンプルのためだけにネイティブ境界を増やすことになる。**両者は通常一致するが同一ではない**（ネイティブは*システム* ACP を使う）。**要検証**（V-6） |
| I-3 | M-8 のフィクスチャに**ハングル + 非 BMP 文字（絵文字）**を使用 | 計画は「現在の ACP で表現できない文字」を要求。ハングルは 932 / 1252 の外だが 949 では表現できる。非 BMP 文字は**どの ANSI コードページでも表現できない**ため、機種に依存しない。両方入れた |
| I-4 | `Begin` は開始行を出さない | macOS 版は `FormatRunning` で 1 行出すが、本画面は 1 操作で複数行（accept / event / done）出るためログが倍になる。**開始 seq は結果行が持つ**ので順序は読める |
| I-5 | 遅延実行はコルーチン | ワーカースレッドのタイマーだと `MainThreadRequired` になり `NotForeground` に到達しない。フレームで待つ必要があるため `WaitForSeconds` |
| I-6 | メインスレッド id は `Awake` で捕捉 | 「今この場で聞いたスレッド」ではどこからでも true になる。ワーカー観測が成立しない |
| I-7 | `Clear` の per-call callback 行は `Done` を通さない | `Accept` していないため、`Done` の `_pending--` が未消化件数を負に振る |

---

## 4. 自動テスト（43 件）

計画 7.7 は 3 本を要求。すべて実装した。

### 4.1 `WindowsClipboardSampleSceneWiringTests`（14 件）

| 検証 | 何を捕まえるか |
|---|---|
| UXML / **USS** の Resources パス存在 | USS が無いと `ApplyScreen` が `styleSheets.Clear()` を行わず、TopMenu のスタイルを被る。**レビュー v1 A1-3 の再発防止** |
| 全ボタン名が UXML にある | 結線ミスは実行時に 1 行ログを出して黙って続く |
| **UXML に未結線のボタンが無い** | 押せて反応しないボタンは、カバレッジ照合で「操作済み」と記録される |
| ボタン総数 = 66 | 画面と計画の乖離 |
| 1 ボタン 1 ハンドラ（命名規約照合） | `DeleteLastButton` を `OnRestoreLastClicked` に繋いでも名前検査は全部通る |
| **`catch (OperationCanceledException` が存在しない** | 投げない実装に対する catch は永久に来ない（R-8） |
| **await 地点の数 = `Cancelled` ガードの数** | ガードを書かない await が増えたら数がずれる |
| **provider 内に Unity API が無い** | `Debug.` / `AppendResult` / `_result` / `RefreshStatus` / `Instance` を禁止 |
| Navigator の `RemoveIfExists` 登録 | 無いと再訪で購読が二重になる |
| TopMenu の `#elif UNITY_STANDALONE_WIN` 分岐と購読ガード | 無いとボタンは押せるが何も起きない |

### 4.2 `WindowsClipboardSampleResultTests`（17 件）

整形（内容を出さない・矛盾した結果をそのまま出す・アンカー無しは `n/a`）と、
`Advance` の状態遷移（`Draining` と `ShutdownFailed` の区別、無関係な失敗で状態を動かさない）。

### 4.3 `WindowsClipboardSampleFixtureTests`（12 件）

DIB の全ヘッダフィールド、bottom-up、`BITMAPFILEHEADER` を付けないこと、
ANSI フィクスチャが非 BMP とハングルを含むこと、パスが絶対かつ `/` を含まないこと、
2 回目の削除が 0 件で成功すること、フィクスチャのダイジェストが 0 にならないこと。

---

## 5. 実装中に見つけたこと

### 5.1 テストの誤り 1 件（自分の書いたテスト）

`AnEmptyReadIsDescribedAsEmptyRatherThanAsAFailure` が失敗した。原因は実装ではなくテストで、
`WindowsClipboardTextResult.Success(op, "")` を「空の読み出し」と思い込んでいた。実物には
**専用の `Empty(operation)` ファクトリ**があり、`Success` は `IsEmpty == false` を返す。

これは設計 8.4 の「**戻り値 0 は空を意味しない**」そのものだったので、
テストを **2 つの成功を区別する**形に書き直した（`AnEmptyClipboardIsNotTheSameAsAnEmptyString`）。

| 経路 | `IsEmpty` | `length` |
|---|---|---|
| `Empty(op)` — クリップボードが空 | `True` | `-1`（Text は null） |
| `Success(op, "")` — 空文字を読んだ | `False` | `0` |

長さだけを出していたら両者は同じに見えていた。

### 5.2 コンパイルエラー 1 件

`using System;` を置いたテストファイルで `Object` が `UnityEngine.Object` と `object` の間で
曖昧になった（CS0104）。`UnityEngine.Object.DestroyImmediate` に明示。
**過去にも同じ組み合わせで踏んでいる。**

### 5.3 検証手順の運用ミス（記録）

`verify_unity_windows.sh` を前回の実行が終わる前に起動し、Unity のプロジェクトロックにより
2 回連続で「EditMode: NO RESULT FILE (Unity exited 1)」になった。
**ログにはコンパイルエラーが 1 件も無く、`Successfully changed project path` の直後に終了する。**
コンパイル失敗と見分けが付きにくい。ログサイズが 1170 バイト固定なのが目印。

---

## 6. ボタン単位のカバレッジ照合表（計画 9.3 / N-3）

**ネイティブ側は 55 シナリオを全消化したあとで 49 ボタン中 9 ボタンが未操作だったことに気づいた。**
シナリオ一覧はボタン一覧ではないため、シナリオに登場しない操作は誰も押さない。
**実機確認では必ずこの表で照合すること。** 空欄は「M 項目に現れない＝カバレッジ工程で押す」を意味する。

| # | セクション | ボタン | 担当 |
|---|---|---|---|
| 1 | Header | Back To Home | S-5 / S-6 |
| 2 | Lifecycle | Initialize | M-1 / M-23 |
| 3 | Lifecycle | Try Shutdown | About 3（発火しない契約） |
| 4 | Lifecycle | Shutdown With Drain | — |
| 5 | Lifecycle | Can Shutdown Now | — |
| 6 | Lifecycle | Shutdown While Disabled | **M-20a** |
| 7 | Lifecycle | Force Initialize While Draining | — |
| 8 | Lifecycle | Quit | **M-19** |
| 9 | Copy | Copy Plain Text | M-2 |
| 10 | Copy | Copy Plain Text (empty) | — |
| 11 | Copy | Copy Large Text | M-21 |
| 12 | Copy | Copy Html | M-4 |
| 13 | Copy | Copy Files | M-5 |
| 14 | Copy | Copy Image | M-6 |
| 15 | Copy | Copy Custom Format | — |
| 16 | Copy | Copy Multiple Formats | M-7 |
| 17 | Copy | Copy Multiple Formats (with image) | — |
| 18 | Copy | Copy Multiple Formats (CF_TEXT + non-ASCII) | **M-8**（ACP 前提つき） |
| 19 | Copy | Copy Multiple Formats (duplicate format) | — |
| 20 | Options | Copy (Sensitive) | M-9 |
| 21 | Options | Copy (Exclude History) | — |
| 22 | Options | Copy (Exclude Roaming) | **未実施**（R-5。押して `None` までは見る） |
| 23 | Paste | Paste Plain Text | M-3 / M-21 |
| 24 | Paste | Paste Plain Text (after Clear) | 8.5 の 9 種目 |
| 25 | Paste | Paste Html | — |
| 26 | Paste | Paste Html (text only) | — |
| 27 | Paste | Paste Files | M-5 |
| 28 | Paste | Paste Image | M-6 / M-21 |
| 29 | Paste | Paste Custom Format | — |
| 30 | Inspect | Has Format (CF_UNICODETEXT) | — |
| 31 | Inspect | Get Formats | — |
| 32 | Inspect | Get Preferred Format | **8.8 の期待値 4 件** |
| 33 | Inspect | Clear | M-17 / 共通イベント→callback の順序 |
| 34 | Deferred | Reserve Deferred Formats | M-17 / M-18 / M-19 |
| 35 | Deferred | Recover Deferred State | — |
| 36 | History | Get History Availability | M-12 |
| 37 | History | Get History | M-11 / M-12 |
| 38 | History | Restore Last | M-14 / M-24 |
| 39 | History | Delete Last | M-14 |
| 40 | History | Clear Unpinned | M-14 |
| 41 | History | Cancel Last | — |
| 42 | History | Restore Twice In One Frame | M-15 |
| 43 | History | Issue And Cancel In One Frame | M-16 |
| 44 | History | Request + Immediate Shutdown | — |
| 45 | History (Await) | Get History (Await) | — |
| 46 | History (Await) | Get History (Await + Cancel) | **M-16 / S-9** |
| 47 | History (Await) | Get Availability (Await) | — |
| 48 | History (Await) | Restore (Await) | — |
| 49 | History (Await) | Delete (Await) | — |
| 50 | History (Await) | Clear Unpinned (Await) | — |
| 51 | Events | Enable History Events | M-10 |
| 52 | Events | Disable History Events | ブロック C の前提 |
| 53 | Events | Reset Event Counters | — |
| 54 | Threading | Copy From Worker Thread | S-7 |
| 55 | Threading | Get History From Worker Thread | S-7 |
| 56 | Threading | Delayed History Call (5s) | **M-13** |
| 57 | Errors | Copy Plain Text (null) | 8.5 |
| 58 | Errors | Copy Files (empty) | 8.5 |
| 59 | Errors | Copy Custom Format (blank name) | 8.5 |
| 60 | Errors | Copy Multiple Formats (empty) | 8.5 |
| 61 | Errors | Restore (blank id) | 8.5 |
| 62 | Errors | Restore (unknown id) | 8.5 |
| 63 | Errors | Cancel (unknown id) | 8.5 |
| 64 | Errors | Copy After Shutdown | 8.5。**最後に押す** |
| 65 | Fixtures | Create Temp Files | M-5 の前提 |
| 66 | Fixtures | Delete Temp Files | — |

**M 項目に現れないボタンは 27 / 66。** 計画 9.3 の予測（Await 5 ボタンほか）より多い。
**照合工程を省くと、この 27 ボタンは一度も押されない。**

---

## 7. 手動確認観点

`artifact/designs/clipboard/2026-09-08-windows-clipboard-sample-scene-design-v3.md` の
6 節（ブロック A 〜 E）と 9 節（S-1 〜 S-9）に従う。**本工程では 1 件も実施していない。**

### 7.1 未実施項目（理由つき）

| # | 内容 | 理由 |
|---|---|---|
| M-1 〜 M-24 | 実機動作確認 | **本工程の範囲外。** サンプルを作るところまで |
| S-1 〜 S-9 | サンプルシーン自体の観点 | 同上 |
| M-20b | `ShutdownTimeout` の観測 | native が `NotYet` を返す条件が要る（計画 R-2） |
| M-8 | ACP が UTF-8 の環境 | 欠落しないため観測不能（計画 8.2.1） |
| `Copy (Exclude Roaming)` の効果 | 2 台目デバイスと同期有効が必要 | ネイティブ側も同じ理由で未実施（計画 R-5） |
| D-9 | 予約失敗時の世代管理 | サンプルから失敗を起こす手段が無い（計画 R-1） |
| M-22 | IL2CPP | 本検証はすべて Mono |

---

## 8. 要検証事項

| # | 内容 |
|---|---|
| V-1 | `CF_TEXT` の欠落挙動（M-8）。**まず状態行の `ACP:` を確認する**。65001 なら未実施 |
| V-2 | `Application.temporaryCachePath` の区切り文字。`Path.GetFullPath` で正規化しているが実機で確認する |
| V-5 | 画像の往復で置いたものと違う画像が返らないか。ネイティブは 8x8 を置いた往復で `1887x820` を得ている |
| **V-6** | **状態行の `ACP:` は `CultureInfo` 由来であり、ネイティブが使う*システム* ACP と一致する保証はない**（I-2）。食い違う環境があるなら `GetACP` の P/Invoke が要る |
| V-3 | S-7 を実施するには TopMenu の Editor 分岐を本画面へ遷移させる必要がある。**現状は `DisplayDialog` のみで到達できない**（計画 10 節から未解決） |

---

## 9. 依存方向チェック

| 確認項目 | 結果 |
|---|---|
| UI 層 → Manager 層の一方向か | **適合。** `Runtime/Clipboard/` は UI を知らない |
| 製品コードに試験用の細工を入れていないか | **適合。** `enabled` は MonoBehaviour の公開プロパティ |
| 他プラットフォームのファイルに触れていないか | **適合。** 0 件 |
| OS 接頭辞（P1） | **適合。** 新規 8 ファイルすべて `Windows` 接頭辞。テストのクラス名も同様 |
| 二重ガード（P5） | **適合。** クラスガードのみ。サンプルは P/Invoke を持たない |

---

## 10. 次

1. **実機確認**。計画 6 節のブロック A → B → C → D → E の順。
   **初回は落ちる前提**（28 種を初めて動かすため）
2. **6 節の照合表で全 66 ボタンを潰す**。M 項目だけでは 27 ボタンが残る
3. `review-implementation-sample-scene`

## 11. 実行確認

このサンプル実装結果を採用して、次工程へ進めますか？

- 実行する: この実装結果を採用して `review-implementation-sample-scene` へ
- 修正する: 指摘内容を反映して再実装
- キャンセル: ここまでの差分は保持したまま終了
