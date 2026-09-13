# レビュー結果

- 日付: 2026-09-08
- 対象ファイル: `artifact/designs/clipboard/2026-09-08-windows-clipboard-sample-scene-design-v1.md`
- 機能名: clipboard
- プラットフォーム: Windows
- レビュー体制: 独立した 4 レビュアー（実装結果整合 / 既存サンプル整合・P1〜P5 / 画面要件 / 手動確認観点）
- 実施方法: 全員読み取り専用。計画書の記述を鵜呑みにせず、実装本体・既存 ExampleController・native-toolkit の実物と突き合わせ

---

## 強み

- **M-1 〜 M-24 の対応表を持っている。** 24 項目を 1 件も落とさず掲載しており、「作ったのに確認できない項目」を防ぐ意図が構造として実装されている
- **native-toolkit の Windows サンプル（`ClipboardPage.xaml`、実測 102 行 / 49 ボタン）を実物と突き合わせている。** 採用・不採用の分類が実在の操作と一致
- **クリップボード内容を出さない方針**（4.3 の主旨）は `testing.md` の redaction 規約と実装の意図的逸脱記録に整合
- **P1（`Windows` 接頭辞）/ P4（`Runtime/Common/` 非変更）は問題なし**
- **P2 の判断が妥当。** `NativeToolkitSampleNavigator` と `TopMenuExampleController` を「横断ナビゲーション基盤なので対象外」とした判断は、両ファイルが既に 4 プラットフォーム分の入口を持つ実態と一致する（利用箇所ベースで確認済み）
- 既知の逸脱 11 件を前例として引用していない
- 「12 イベント」の件数は実装の `public event` 12 個と正確に一致
- M-22 を「画面ではなくビルド構成」と整理した点は設計 9.4 と整合

---

## 改善点

### 高優先度（A1: 実装フェーズで気づけない）

この計画どおり実装すると**コンパイルもテストも通り、実機確認だけが原因不明で失敗する**もの。

#### A1-1. TopMenu の Clipboard 遷移に Windows 分岐が無い（5.2）
**3 名が独立に指摘。** 5.2 は「対応プラットフォームに Windows を追加（114 行目のログ文言も更新）」とだけ書くが、遷移先を決めるのは `OnClipboardClicked` の `#elif` 連鎖（`TopMenuExampleController.cs:186-193`）で、そこに `UNITY_STANDALONE_WIN` が無い。106 行のガードだけ直すと、Windows Player でボタンは表示・クリックできるが**押しても何も起きない**。`Dialog` / `Notification` は 135 / 155 行に `#elif UNITY_STANDALONE_WIN` を持っており、Clipboard だけ欠ける。既存の wiring テストは UXML にボタン名があるかしか見ないため捕まらない。

#### A1-2. Navigator の `RemoveExistingControllers` への登録が計画に無い（5.2）
**3 名が独立に指摘。** `NativeToolkitSampleNavigator.cs:197-222` は画面遷移のたびに旧 Controller を型ごとに破棄する。Windows ブロック（212-215 行）には Dialog / Notification しか無い。登録漏れのまま Home に戻ると Controller が残り、`OnDisable` が走らず**12 イベントの購読が解除されない**。さらに `ApplyScreen` は `GetComponent == null` のときだけ `AddComponent` するため再訪時も `Start` が走らず、**全ボタンが無反応**になる。S-5 は必ず失敗する。

#### A1-3. USS ファイルが新規作成一覧に無い（5.1）
**3 名が独立に指摘。** `Runtime/Resources/UI/` 配下の既存 15 画面はすべて `<X>ManagerExample.uxml` と `<X>ManagerExampleStyle.uss` の対。`ApplyScreen` は `Resources.Load<StyleSheet>` が null のとき**エラーも警告も出さずスキップ**し、`styleSheets.Clear()` すら走らないため、**TopMenu のスタイルを被ったまま描画される**。

#### A1-4. 「provider が二相で呼ばれる」は実装と異なる（8.3 / 6 節 M-18）
**4 名全員が独立に指摘。** `RenderDeferredFormat`（`WindowsClipboardManager.cs:2246-2288`）は**サイズ問い合わせ相（`buffer == IntPtr.Zero`）でのみ `provider()` を呼び**、結果を `s_renderCache` に格納する。充填相はキャッシュを `Marshal.Copy` するだけで provider を呼ばない。D-5（2 回目のサイズが 1 回目と完全一致）を守るための設計であり、再生成は禁止されている。

計画どおり「provider の呼び出し回数」を M-18 の合否基準にすると、**正しい実装（常に 1 回）を不合格と判定する。**

#### A1-5. provider から Unity API を呼ぶ設計が provider 契約に違反（8.3）
**4 名全員が独立に指摘。** 8.3 は「provider 内で呼び出しをログに残す」「何回呼ばれたかを画面に出す」とするが、`ReserveDeferredFormats` の XML doc（`:1949-1962`）は provider に対し「**Unity API を呼ぶな**」「MonoBehaviour / Texture を参照せず値でキャプチャせよ」「**providers also run while the application is shutting down**」と明記している。

`Debug.Log` は Unity API、`VisualElement` 更新は UI オブジェクト参照。設計 2.8 D-7 のとおり provider は `uninit` の `DestroyWindow` から同期送出され、Unity のシャットダウン進行中に走る。**M-19（予約したままアプリ終了）でクラッシュまたは無言の形式欠落を招く。** しかも `RenderDeferredFormat` は例外を握り潰して `Unknown` を返すため、失敗が「M-19 が通らない」という別の症状に化ける。

#### A1-6. 内側コンパイルガードの指定が無く、S-6 が観測不能になり得る（5.1）
既存 ExampleController は 2 流派に割れている。

| Controller | 内側ガード |
|---|---|
| Windows/Notification, Windows/Dialog, Android/Clipboard | **あり**（Editor では固定文字列を表示し Manager に到達しない） |
| macOS/Clipboard, iOS/Clipboard | **なし**（Editor でも Manager を呼び、拒否コードを見せる） |

計画は S-6（Editor で `PlatformUnavailable` を確認）を置いているので意図は macOS 型だが、5.1 はクラスガードしか書いていない。**3.1 で「Windows/Notification を確認した」と書いたため、実装者が最も近い Windows 前例をなぞると内側ガードを入れ、S-6 は永久に観測不能になる。**

#### A1-7. 4.3 の「先頭 16 文字表示」が設計 3.2 と自分の S-4 に矛盾（4.3 / 9.1）
4.3 は「内容そのものは画面に出さない」と書きながら、同じ段落で `PastePlainText` の結果を「先頭 16 文字まで」出すとしている。

- 設計 3.2 の規約は「値を伏せて**派生値**（`textLength` 等）を出す」。原文の部分文字列は派生値ではない
- 実装本体は `// Clipboard content may hold passwords or tokens, so the value never reaches the log.` と書いて長さだけを出している（`:1142`）
- 一般的なパスワードは 8〜16 文字で、**16 文字なら全体が露出する**。「16 文字なら安全」を支える基準はリポジトリにも設計にも無い
- **9.1 S-4「内容が画面にもログにも出ていない」と直接矛盾する。** 計画どおり実装すると S-4 は必ず不合格になる

#### A1-8. `ClipboardChanged` のカウンタ表示が設計 12 章の申し送りと正面から矛盾（4.1 §9 / 7.4）
設計 v8 12 章は本計画あての申し送りとして「**`ClipboardChanged` は 1 回の外部コピーで複数回発火（実測 3 回）。回数をそのまま表示せず、最終検知時刻か debounce 済み表示にする**」を明記している。計画は「12 イベントの発火カウンタ」を採っており、**外部コピー 1 回でカウンタが 3 増える正常動作を、テスタが毎回不具合として報告する。**

#### A1-9. DIB のバイト列は「要検証」ではなく確定できる（8.2 / 2.3 P-1 / 10 節 V-1）
native-toolkit に**正解の生成コードが既にある**。`ClipboardPage.xaml.cpp:261-286` の `BuildSampleDib`: 8x8 / 32bpp / `BI_RGB`、`BITMAPINFOHEADER`(40B) + 画素 256B = **296 バイト**、`BITMAPFILEHEADER` なし、`biHeight > 0`（bottom-up）、`biSizeImage` 必須。受け入れ側の拒否条件も `WindowsClipboardFormats.cpp:269-345` の `ValidateDib` で確定している。

「実装時に確認する」と先送りすると、**M-6 が `InvalidData` で止まったときにフィクスチャとライブラリのどちらが原因か切り分けられない。**

#### A1-10. `Application.temporaryCachePath` は `CopyFiles` が期待するパス形式と合わない（8.2）
Unity の `temporaryCachePath` は Windows でも `/` 区切りを返す。ネイティブ側は**区切りを一切正規化せず**、受け取った文字列をそのまま `DROPFILES` へ `wcscpy_s` する（`WindowsClipboardFormats.cpp:202-235`）。C# 側の検証も `IsNullOrWhiteSpace` のみ。

結果、**`CopyFiles` は成功を返すのにエクスプローラへの貼り付け（M-5）だけが失敗し**、フィクスチャ側の問題がライブラリの不具合として記録される。native サンプルは `GetTempPathW`（`\` 区切り）を使っている。`Path.GetFullPath` での正規化を明記すべき。

#### A1-11. V-3「1 MB でバッファ再試行経路に入る」は成立しない（10 節 / 6 節 M-21）
**2 名が独立に指摘。** `ReadRaw` の再試行は **2 回目の呼び出しが `BufferTooSmall` を返したときだけ**、すなわち 2 回の呼び出しの間にクリップボード内容が増えた競合時のみ発生する（`:1623-1651` / `:1051-1060`）。**ペイロードサイズは分岐条件に一切入らない。**「入らない場合はサイズを上げる」は何 MB にしても入らない。

さらに 6 節は M-21 の担当を Copy ボタンのみとしているが、二段階プロトコルは読み出し側（`ReadRaw`）にしかない。**Copy を押しただけでは対象経路を 1 行も通らない。**

#### A1-12. `Task.Run` 内で `Instance` に触るとワーカースレッドで `GameObject` を生成する（8.4）
`Instance` getter は `_instance == null` のとき `new GameObject` + `AddComponent` + `DontDestroyOnLoad` を行う（`:269-286`）。設計 7.2 は「`Instance` getter は GameObject を生成するためスレッドガードできない」と明記している。Manager 未生成の状態で「Copy From Worker Thread」を押すと `MainThreadRequired` ではなく **Unity 例外**になり、S-2 に反する。

#### A1-13. 8.4 は `GetHistory` の戻り値から `MainThreadRequired` を読もうとしている（8.4）
`GetHistory` の戻り値は `uint requestId` で、拒否時は 0 のみ。**エラーコードは戻り値に載らない。** 実装は `StartRequest` 冒頭で `CanRunOperation` に落ち、`RejectRequest` 経由で共通イベント / per-call callback にだけ結果を渡す。「Manager が返した結果を保持」しても `MainThreadRequired` は表示できず、ボタンが「何も表示しない」ように見える。

なおその `RejectRequest` はワーカースレッドからでも `Dispatch` → `dispatcher.Enqueue` を通ってメインスレッドで配送されるため、**8.4 の「次の `Update` で表示する」仕掛け自体が不要。**

### 中優先度（B: 検証手段を変える）

#### B-1. 自動テストが 1 本も計画されていない（5 節全体）
踏襲対象としている macOS Clipboard には `MacClipboardSampleSceneWiringTests`（282 行）と `MacClipboardSampleStateTests`（484 行）があり、同種の wiring テストがリポジトリに 5 本ある。計画は 9 節が手動確認のみ。

wiring テストは「UXML / USS の Resources パス存在」「`Bindings` の全ボタン名が UXML にあるか」「1 ボタン 1 ハンドラか」を検査しており、**A1-3（USS 欠落）は実装フェーズで自動検出できたはず。** macOS の doc コメントも「ボタン名の欠落は実行時の無言 no-op としてしか表面化しない」と明記している。

#### B-2. `*Async` 5 種のうち 4 種にボタンが無い（2.1 / 8.1）
**2 名が指摘。** 8.1 の「8. History (Await)」は `GetHistoryAsync` だけ。`GetHistoryAvailabilityAsync` / `RestoreHistoryItemAsync` / `DeleteHistoryItemAsync` / `ClearUnpinnedHistoryAsync` を起動する手段が無い。1.1 の「このサンプルが唯一の実行手段」という前提の例外なので、明記が要る。特に更新系 3 種は in-flight ガード + `RegisterCancellation` を通るため callback 版で代替できない。

**併せて**: `WindowsClipboardFormatPayload` の 4 ファクトリのうち `Bytes` を通すボタンが 8.2 に無い。

#### B-3. M-13（非フォアグラウンド）を作る導線が無い（6 節 / 8.1）
**3 名が指摘。** 8.1 の History は全て即時実行ボタンで、押した瞬間はアプリがフォアグラウンドである。native サンプルが持っていた "Delayed Worker Check (5s)" を、3.2 は「ワーカースレッド版」へ読み替えて採用したため、**遅延実行の仕組みごと落ちている。**「N 秒後にメインスレッドで実行」ボタンが要る。

#### B-4. M-15 / M-16 が人力クリックでは起こせない（6 節）
**3 名が指摘。** `OperationBusy` は `s_inFlight` に鍵がある間だけ、`CancelRequest` は `requestId` が生きている間だけ意味を持つ。人手の 2 回クリックがネイティブ往復より速い保証は無い。「1 ボタン = 同一フレームで 2 回発行」「1 ボタン = 発行 + 即キャンセル」の専用ボタンが要る。

#### B-5. M-19 の手順が 3 点欠けている（6 節）
1. **終了手段が 8.1 に無い**（Quit ボタンが無い）。強制終了では `wantsToQuit` のドレインが走らず、M-19 は必ず失敗に見える
2. **履歴が有効だと D-8 により履歴サービスが予約直後に全形式を実体化する。** その状態では `WM_RENDERALLFORMATS` を通らずに貼り付けられてしまい、**M-19 が通ったことにならない。** M-17 / M-18 / M-19 は履歴 OFF を前提条件にすべき
3. ドレインに最大 2 秒かかる（60 フレーム / 2 秒予算）。ハングと誤認しない旨の記載が要る

#### B-6. M-8 の `CF_TEXT` フィクスチャが定義されていない（8.2）
**2 名が指摘。** 8.2 の「Multiple」行は「Text + Html の 2 件」でフォーマット名が無い。自然に選ぶのは `CF_UNICODETEXT` で、それでは `CP_ACP` 変換が起きず**欠落は再現しない＝ M-8 が「問題なし」と誤って合格記録される。** `CF_UNICODETEXT`（非 ASCII）+ `CF_TEXT`（同一内容）の 2 件と明記すべき。画像入りの Base64 側も `CF_DIB` と明記が要る。

#### B-7. 11. Errors の期待値表が無い（7.5 / 8.1）
**2 名が指摘。** 7.5 は一律「`InvalidArgument` が返ることを見せる」とするが、実際は操作ごとに違う。

| ボタン | 実際の期待 |
|---|---|
| Copy Plain Text (null) / Copy Files (empty) / Copy Custom Format (blank) / Copy Multiple Formats (empty) | `InvalidArgument` |
| Restore (unknown id) | `ValidateItemId` は空白のみ弾く。非空白の未知 id はネイティブへ届き `ItemDeleted` / `Unknown` が**コールバックで**返る |
| Cancel (unknown id) | `requestId == 0` のときだけ `InvalidArgument`。0 以外は別の結果 |
| Copy After Shutdown | 状態依存で `ShuttingDown` または `NotInitializedByHost` |

**実施順序も未定義。** `CopyPlainText` は状態ガードを引数検証より先に通すため、`Initialize` 前や Shutdown 後に `(null)` を押すと `InvalidArgument` にならない。

#### B-8. S-6 が 3 点で成立しない（9.1）
- `TryShutdown` / `ShutdownWithDrain` は `CanRunOperation` を通さず、`Uninitialized` では**冪等成功**を返す
- 10. Threading の 2 ボタンは `MainThreadRequired`（判定順が platform より先）
- そもそも**Editor では TopMenu が `DisplayDialog` を出すだけで本画面に到達しない**（`TopMenuExampleController.cs:178-194`）

#### B-9. 結果表示の項目に `IsEmpty` / `completed` / `HasFormat` が無い（4.3）
**2 名が指摘。** 設計 8.4 の「読み出しは `IsSuccess == true` でも `IsEmpty == true` があり得る。戻り値 0 は空を意味しない」「`HasFormat` と `IsSuccess` は独立」、および `TryShutdown` の `out bool completed` が画面から判別できない。

#### B-10. 配送順序・スタック外・exactly-once が観測できない（7.4 / 7.3）
- 設計 7.1 の「呼び出し元スタック外」「共通イベント → 個別 callback の順」は、カウンタでは**発火したかしか分からず、いつ・どの順かが分からない**
- 受付前拒否は必ず `requestId == 0` を返すため、拒否を 2 回起こすと受付行が両方 0 になり**完了行との相関が取れない**
- macOS には `MacClipboardSampleResultContext.Sequence`（呼び出しごとのローカル連番）の前例がある。同じ仕組みで受付・完了・イベントを 1 本の時系列に並べれば、順序も exactly-once も目視できる

#### B-11. M-24 の帰属が判定できない（7.4 / 6 節）
`RestoreHistoryItem` は非同期、`ClipboardChanged` はネイティブ発の別経路。設計 9.3 の O1 が「スクリーンショット撮影でも `ClipboardChanged` が余分に発火する」と明記しているとおり、**カウンタだけでは増分が restore 由来か区別できない。**

#### B-12. 設計 12 章の申し送り 8 件中 4 件が未反映（計画全体）
A1-8（`ClipboardChanged` の表示方法）に加えて、次の 3 件が計画書に見当たらない。

| 申し送り | 計画の状態 |
|---|---|
| `ReserveDeferredFormats` は既存内容を破棄するので UI で事前警告する（D-1） | 記載なし |
| 更新系履歴操作は in-flight 中ボタン無効化が望ましい（7.6.4） | 記載なし。かつ M-15 の「連打」と**両立しない** |
| shutdown 開始後は UI も操作不能にする | 記載なし。かつ 11.Errors の「Copy After Shutdown」と**両立しない** |

後 2 件は「無効化する / しない」を計画が決めていないため、実装者がどちらを選んでも M-15 か 11.Errors のどちらかが実施できなくなる。**M-15 / 11.Errors 用に押せる経路を残す、と明記するのが解。**

#### B-13. 設計 9.3 の O1（スクリーンショット注意）が未反映（6 節 / 9 節）
撮影がクリップボードを画像で上書きし、履歴件数も `ClipboardChanged` 回数も変える。M-2 〜 M-11 が丸ごと偽陰性になり得る。

#### B-14. M-12 の順序制約が無い（6 節）
`SetHistoryEventsEnabled` の停止に失敗すると `MonitorRegisterFailed` が sticky になり、以後の再有効化が拒否され続ける（設計 2.9）。**9. Events を触った後に M-12 をやると、復旧不能な状態と履歴無効を区別できない。**

#### B-15. M-20 を「実施不可」とした判断は言い過ぎ（7.6 / P-3）
**2 名が指摘。** 製品コードを変えずに `ShutdownTimeout` に到達できる候補が 2 つある（いずれも要検証）。

- **(a) 強制 1 回打ち切り経路**: `ShutdownWithDrain` は `isActiveAndEnabled == false` のとき `AdvanceDrain(force: true)` を 1 回だけ回し、完了しなければ `ShutdownTimeout` にする（`:944-953` / `:2905-2921`）。`enabled` は公開プロパティであり試験用細工ではない。ただしこれは `force` 分岐で、設計 M-20 が想定する `spent`（予算消尽）とは**別分岐**。どちらを M-20 と定義するか決める必要がある
- **(b) 予算消尽経路**: 外部プロセスに `OpenClipboard` を握らせ続ければ `BUSY` が返り続ける可能性がある。小さな外部ツールが要る（層 3 の道具立て）

**代替案として挙げた「ログで分岐の存在を確認する」は実行を伴わないコード確認であり、結果 v6 が「コードレビューでは検証できない」と結論づけた対象そのもの。検証としての価値がない。**

#### B-16. 10 節 V-1 〜 V-4 の切り分けが無い
着手前に決めるもの / 実装しながら決めてよいもの / そもそも検証項目でないものが同列に並んでいる。

| # | 判定 |
|---|---|
| V-1（DIB） | **着手前に確定できる。要検証から外す**（A1-9） |
| V-2（非 ASCII 欠落） | 実機観測項目として妥当。前提の `CF_TEXT` フィクスチャ追加は着手前（B-6） |
| V-3（1 MB） | **前提が誤り。M-21 の判定基準を決め直す**（A1-11） |
| V-4（ワーカースレッド） | **すでに確定しており実機検証項目ではない。** `ClassifyOperationGuard` が最初に `isMainThread` を見る。層 1 テストもある |

#### B-17. D-1 〜 D-9 の確認可能性が整理されていない（8.3）
D-1 は M-17 で確認できるが、D-2〜D-5 は間接的のみ、D-6 は確認できない（かつ 8.3 自体が違反）、D-8 は観点が無い（B-5 の 2）、D-9 は起こす手段が無く P-3 と同様に「起こせない」と明記すべき。

**併せて**: 設計 7.7 は `RecoverDeferredState` について「**`NONE` は『回復した』ことも『予約が消えた』ことも意味しない**」と明記しているが、8.1 はボタンを置くだけで結果の読み方に触れていない。テスタは `IsSuccess == true` を「回復した」と読む。

#### B-18. 3.2 が「採用」と宣言した異常系 3 件が 8.1 に無い
`after Clear` / `text-only html` / `duplicate format` が 8.1 の 11.Errors に存在しない。さらに native の `Force Initialize while shutting down` と `Request + Immediate Uninitialize` が採用表にも不採用表にも無い。この 2 つはドレイン中の再入という `ShuttingDown` / `OperationBusy` 分岐に直結する。

#### B-19. S-2 / S-5 の穴（9.1）
- **S-2** は「例外を出さない」だけで、ボタン→ハンドラの取り違え（`Copy Html` が `CopyPlainText` を呼ぶ等）を捕まえられない。ログの操作名を **`WindowsClipboardResult.Operation` から出す**と規約化すれば捕まる
- **S-5** は解除だけを見て「**再入場で二重購読しない**」が無い。イベントは `DontDestroyOnLoad` の Manager に生えており画面より寿命が長いため、出入りのたびに購読が増えてカウンタが 2 倍・3 倍になる。例外は出ないので S-2 でも捕まらない

#### B-20. 8.4 の観点自体を書き換えるべき
A1-13 のとおり戻り値では確認できない。「ワーカーから per-call callback 付きで呼び、callback がメインスレッドで走ることを確認する（設計 7.1 の『拒否も dispatcher 経由』の実証を兼ねる）」に改めるべき。

### 低優先度（A2: 実装フェーズで気づける）

**直さない。実装結果ファイルにチェックリストとして転記する。**

- **A2-1**: 2.1 の「書き込み系は全て `options` を第 2 引数に持つ」は 3 つで不成立。`CopyHtml(string, string?, options, onResult)` と `CopyCustomFormat(string, byte[], options, onResult)` は**第 3 引数**、`Clear` は `options` を**持たない**。計画どおり書くとコンパイルエラー
- **A2-2**: `CopyPlainText(null)` は `#nullable enable` 下で CS8625 警告。`null!` で明示的に抑制するか、null ケースを落とすかを 7.5 に明記する

### 低優先度（C: 記述の整合だけ）

**機械照合に回す。手で直さない。**

- **C-1**: 1.1 / 2.1 の「公開 API **26 種**」が自分の表（4+7+5+3+6+5+1+2 = **33**）と実装の public メソッド数（33）に合わない。**3 名が指摘。** 26 は結果 v6 由来だが v6 の内訳も 21 にならず、元資料の時点で不整合。この分母のずれが B-2（`*Async` 未カバー）を見落とす原因になっている
- **C-2**: 3.2 の参照先 2 か所が誤り。「読み替える（4.2）」の 4.2 は操作導線、「期待値が異なる（4.4）」の 4.4 はエラー表示。後者の実体は 7.5
- **C-3**: 3.1 の「既存の共通パターン」表が macOS と Windows/Notification を混同。Windows/Notification には `ResultScrollView` も `StatusTextBlock` も About セクションも**存在しない**。`Bind` ヘルパーも macOS Clipboard のみ。ボタン解除が `OnDestroy` である点も未記載
- **C-4**: 7.3 の「macOS サンプルは内容を出している」は**事実と逆**。macOS は内容を一切出さず、ネイティブのエラーメッセージまで伏せる**より厳しい方針**。「拡張」ではなく「維持」
- **C-5**: 3.2 の `CF_DIB` 不採用理由が誤り。`Format` は「CF_* 定数名または登録名」を受ける文字列で、設計 2.4 が `{"format":"CF_DIB","base64":"..."}` を明示している。`Bytes("CF_DIB", dib)` で表現できる
- **C-6**: 5.1 / 7.2 の「macOS の 3 ファイル構成を踏襲」が命名・役割で前例と違う。既存は `<Prefix>ClipboardSampleResult.cs` と `<Prefix>ClipboardSampleObservationState.cs`。計画は `SampleLog` と `SampleFixtures`
- **C-7**: 10 節 V-2 の参照先「設計 2.x」は **2.4** が正しい
- **C-8**: M-22 の実行契約（Scripting Backend / Target architecture / ビルド種別）が未記載。`testing.md` 層 2b は明示を必須としている

---

## 不足項目

1. **USS ファイル**（A1-3）
2. **自動テスト（wiring テスト相当）**（B-1）
3. **`*Async` 4 種と `Bytes` ファクトリを通すボタン**（B-2）
4. **遅延実行ボタン**（M-13 用、B-3）
5. **同一フレーム 2 回発行 / 発行 + 即キャンセルボタン**（M-15 / M-16 用、B-4）
6. **Quit ボタン**（M-19 用、B-5）
7. **DIB のバイト列定義**（A1-9）
8. **`CF_TEXT` / `CF_DIB` を含むフィクスチャ定義**（B-6）
9. **11.Errors の期待値表と実施順序**（B-7）
10. **Manager の lifecycle 状態表示**（`Draining` と `ShutdownFailed` が同じ `ShuttingDown` に畳まれるため、テスタが復帰不能状態を判別できない）
11. **シーケンス番号による時系列表示**（B-10 / B-11）
12. **前提条件（履歴 ON/OFF、フォアグラウンド）ごとの実施ブロック**（B-5 / B-14）
13. **O1（スクリーンショット注意）**（B-13）

---

## 総合評価

**要修正（重大）。A1 が 13 種。**

止める基準は「A1 が 0」なので、この計画のままでは実装に進めない。

### 指摘の性質

A1 の 13 件はいずれも**コンパイルもテストも通り、実機確認だけが原因不明で失敗する**類である。しかも失敗の見え方が誤誘導する。

- A1-4 / A1-5: 正しい実装を「不合格」と判定させる
- A1-9 / A1-10: フィクスチャ側の問題をライブラリの不具合として記録させる
- A1-8: 正常動作（1 回のコピーで 3 回発火）を毎回不具合として報告させる
- A1-1 / A1-2 / A1-3: 画面が壊れているのに原因が計画書に書かれていない

### 独立した複数名が到達した指摘

| 指摘 | 到達人数 |
|---|---|
| A1-4（provider の二相誤解）/ A1-5（provider 契約違反） | **4 / 4** |
| A1-1（TopMenu 分岐）/ A1-2（Navigator 登録）/ A1-3（USS） | **3 / 4** |
| B-3（M-13 導線）/ B-4（M-15/M-16）/ C-1（API 件数） | 3 / 4 |

### 根本的な傾向

**計画が実装の内部構造を誤って想定している箇所が多い。** 特に A1-4 / A1-11 / A1-13 は、いずれも「実装がそうなっていると思い込んだ」もので、実装本体を読めば分かるものだった。サンプルシーンは実装の観測装置なので、**観測対象の構造を誤ると、装置が嘘の観測結果を出す。**

計画側の穴で M 項目が落ちる箇所（A1-9 / A1-10 / B-6）も同種で、いずれも native-toolkit 側に正解が既にあった。

### 次

改善版 v2 を作る場合、A1 の 13 件と B の主要項目（B-1 〜 B-8）を反映する必要がある。C 区分は機械照合に回す。

止める基準の残り 2 条件（レビュアーを替えて 1 回通す / 直さない残件を文書に明記）は、v2 作成後に判定する。
