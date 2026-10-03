# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/windows/clipboard/designs/2026-09-27-windows-clipboard-design-v9.md
- 機能名: clipboard
- プラットフォーム: Windows
- レビュー: 3 本を並行で行い、指摘を重ねて整理した。サブエージェント 2 本（観点を分担: ネイティブ境界 / 公開契約・テスト・自動化の前提）と Codex（全観点）。どれも読み取りのみ
- 出典の略記: [N] = ネイティブ境界、[T] = 契約・テスト、[X] = Codex

---

## 強み

- `extern` 40 本の型・`ref` / `out`・`byte[]`・`UIntPtr` と本数、構造体 2 つの大きさとフィールドの位置、コールバック 7 種のシグネチャは、ヘッダーと `CApiLayoutTest.cpp:79-91` と一致 [N][X]
- `user_data` / `release` が 0 なのは合法（`ReleaseGuard.cpp:12`）[N]
- 遅延描画を 1 段にしても provider を呼ぶ回数は 1.x と同じで、1.x の「2 回目で大きさが変わると黙って捨てる」危険が無くなる [N]
- J-3 の規則の出典（1.11.0 の `Base64Decode`）と、`Convert.FromBase64String` を使わない判断は正しい [T][X]
- J-5（close が成功するまで free しない）は、2.0.0 の放棄の仕様と整合する [N][T][X]
- `paste_text` には途中の NUL が無い（`wcsnlen`）ので、最初の NUL で切っても害は無い [N]
- 公開の数（メソッド 33、イベント 12、定数 27）と引用の行番号は実物と一致する [T]
- 1.x で記録する新しいテストは、1.x で実行できる [T]

## 改善点

### 高優先度

**A1（この通り実装すると契約が変わり、しかもコンパイルもテストも通る。直して再レビュー）**

1. **base64 の規則に「3 文字目が `=` なら 4 文字目も `=`」が抜けている** [N]
   - 1.x は `pad2 && !pad3` で `XY=Z` を弾く（`1.11.0:WindowsClipboardFormats.cpp:463-466`）。`X=Y=` は、その前の「`=` は最後の 2 文字だけ」で既に落ちる
   - v9 の箇条書きでは `"QQ=Q"` が通る
   - 修正: 規則に足し、`X=Y=` の行は消す。層 1 と 7.3（1.x で記録）に `"QQ=Q"` を足す
2. **`CopyMultipleFormats` で誤りが 2 つ以上あるとき、返るコードの優先順位が変わる** [N][T]
   - 1.x は要素を 1 件ずつ、名前の解決 → 重複 → 種類 → base64 → DIB / HDROP の検証の順に見て、最初に失敗したものを返した（`1.11.0:WindowsClipboardManager.cpp:500-590`）。flags は最後に Core で見た
   - v9 は base64 を C# で先に全部復号する。2.0.0 は flags を最初に見る（`ClipboardItemsCApi.cpp:88`）
   - 例: `[Base64("CF_DIB", 形は正しいが DIB として不正), Base64("X", "@@@@")]` は 1.x で `InvalidData`、移行後は `InvalidParameter`
   - 修正: 5.4 に書き、この例を 7.3 の「1.x で記録」に入れる
3. **履歴の結果を写している途中で例外が起きると、完了が届かない** [X][T]
   - v9 は写す処理をコールバックの中に移すが、受け口の catch はログを出すだけ（`WindowsClipboardManager.cs:2619-2623`）。ticket が AwaitingNative のまま残り、イベントも per-call も Awaitable も終わらず、同時実行ガードも外れない
   - 1.x は JSON を保存するだけで、壊れていれば配送のときに `ResultParseFailed` だった（`:2545-2553`）。native も変換の失敗を `OUT_OF_MEMORY` の完了として必ず届ける（`ClipboardHistoryCApi.cpp:69-87`）
   - 修正: 写す処理を別の try で囲み、失敗したら `OutOfMemory` / `Unknown` を保存して、`MarkUndelivered` と `QueueDelivery` を必ず通す。層 1 / 2a に注入のテストを足す
4. **セッションが無いときの終了の経路が決まっていない** [T]
   - `OnDestroy`（`:752-755`）と `OnWantsToQuit`（`:3127-3128`）は、状態を見ずに `RunShutdownAttempt` を通る。1.x の uninit は未初期化なら TRUE を返した（`1.11.0:...WindowsClipboardManager.cpp:252`）
   - v9 の 4.4 の書き方だと終端の失敗になり、quit で `ResumeQuit` が `LogError` を出す（`:3151-3155`）。`close(NULL)` も `INVALID_PARAMETER` で同じ
   - 修正: 4.7 に「`s_session == IntPtr.Zero` ならネイティブを呼ばず、`completed = true` の成功」と書く。4.4 の判定は終了以外の操作に限る
5. **捕まえる例外を「そろえる」の向きが決まっていない** [T]
   - 今は API ごとに違う（`InvokeWrite` / `HasFormat` / `CanShutdownNow` は DllNotFound と EntryPointNotFound だけ、`ReadRaw` はそれと OOM、`Initialize` / `StartRequest` / `InvokeNativeShutdown` は全部）。それ以外の例外は呼び出し側に出て、イベントもコールバックも来ない
   - 全部捕まえると、例外が「Failure ＋イベント＋コールバック＋`LogError`」に変わり、J-9 とぶつかる
   - 修正: API ごとに、捕まえる例外・コード・`LogError` を表で決める（今の分け方を保ち、`BadImageFormatException` だけ足すのが最小）
6. **1.x のパーサーの項目ごとの規則が落ちる** [N][T]
   - id が空の項目は `LogWarning` を出して捨てていた（`WindowsClipboardJsonParser.cs:117-125`）。2.0.0 は id が無ければ `""` を入れる（`WindowsClipboardApi.cpp:603`）
   - 修正: この規則を保つと 4.6 に書き、層 1 に入れる

### 中優先度

**A2（実装やテストで必ず表に出る）**

1. **5.4 の 2 行が事実と違う**（この表はマニュアルに流れる）[N][T]
   - 最後の行と E-19: `OnDestroy` は `s_isTerminated` を立て（`:746`）、その後の `Initialize` は `ManagerDestroyed`（`:801-806`）。`ShutdownFailed` なら `ShuttingDown`（`:815-820`）。ネイティブの `NOT_SUPPORTED` には届かない
   - 「`TryShutdown` の 1 回目」: 1.x の uninit も、待つ要求が無ければ 1 回目で TRUE（`WindowsClipboardHistoryCoordinator.cpp:468`、`expected.py:173` も `completed=True`）。差が出るのは、履歴の要求が待機中のときだけ
2. **S-2 のブロック D の「ForceInitializeWhileDraining」が、落ちるか不安定になる** [T]
   - 期待値は `initClipboardManager NG ShuttingDown` を必須にしている（`scripts/windows_clipboard_sample_expected.py:154-157`）
   - これは、待っている要求があると 1.x の 1 回目の uninit が必ず「まだ終わっていない」を返すことに頼っている
   - 2.0.0 の close は自分の drain を呼び出しの中で処理する（`WindowsClipboardApi.cpp:268-287`）ので、1 回目で終わりうる。WinRT の処理が始まっていれば `BUSY` で、結果はタイミング次第
   - v9 の「サンプルと期待値は変えない」と矛盾する。扱いを決める必要がある（開発者の判断）
3. **`pending.Json` は拒否の詳細の文言も運んでいる** [T]（`:2436,2543,2564,2585`）。消すと `itemId was null or blank` が消え、`WindowsClipboardPlayerTests:159-160` が落ちる。文言の欄を別に持つ
4. **`CanShutdownNow` の扱いが 4.2 と 4.4 で食い違う** [T]（`can_close(Zero)` を呼ぶ／ネイティブを呼ばない）。値はどちらも真（1.x は `1.11.0:...cpp:291-296`、2.0.0 は `ClipboardCApi.cpp:57-60`）
5. **`render_target_set` の失敗の値を捨てない** [X][N]。INVALID / OOM を `NONE` で上書きすると、最終的に別のエラーに化ける（`ClipboardConvert.cpp:143-163`）。戻り値をそのまま返す

**B（検証手段の穴）**

1. **成功で件数 0 の扱いを書く** [N]。1.x の `GetFormats` は空のクリップボードでも `[]` で `Success`・`IsEmpty=false`（`WindowsClipboardCore.cpp:523-527`、`WindowsClipboardManager.cs:1399-1402`）。`paste_text` の `""` は `Success("")`（`Empty` は `GetPreferredFormat` だけ）。「失敗のコードを空の成功にしない」（`ClassifyFirstRead_AFailureWithZeroSize…`）も移す [N][T]
2. **close の中で完了のコールバックが走る** [N]。待機中の要求の `CANCELED` は `session_close` の中で届く（`CH:150-152`）ので、受け口が `TryShutdown` / `OnDestroy` の中から、`FinishShutdownAttempt` より前に呼ばれる。今の受け口は `MarkUndelivered` と `QueueDelivery` だけなので安全。4.6 に書き、層 2a に注入のテストを足す
3. **7.3 の「`CopyCustomFormat` に空のバイト列」は何も検証しない** [N][T]。C# が先に `InvalidArgument` で拒む（`:1281-1288`）。`CopyMultipleFormats([Bytes("X", new byte[0])])` に替える（base64 は `""` で、1.x も J-3 も `InvalidParameter`）。4.2 の「`Bytes` は常に通る」も空の配列では誤り
4. **7.2 の「今の 65 本を通す」は成り立たない** [T]
   - `Integration:1373-1386` は `ResultParseFailed` を確かめている（A1-3 のテストに置き換える）
   - `WindowsClipboardJsonParserTests` を消すと、公開のまま残る `ToUtcTime` のテスト（`:185,200`）、id を捨てる・text が無い・contentTypes が無いのテストも消える。新しいファイルに移す
   - Dispatch のテストのうち約 30 本が 1.x 固有の仕組み（`ClassifyFirst/SecondRead`、`ReadRawForTests`、`RenderForTests` と cache）を確かめている。消すものと移すものを列挙する
5. **`TryShutdown` の 1 回目のテストは回帰テストにならない** [T]。待つものが無ければ 1.x でも真。「要求を待たせたとき」に何を確かめるかを決める
6. **Player では本当の未初期化の状態を作れない** [T]（`ResetForTests` は Editor だけ）。「セッションが無いとき（`ShutdownWithDrain` の後）」と書き直す
7. **`extern` の結び付けの「ほかの関数は既存のテストで毎回通る」は言い過ぎ** [T]。多くは `Destructive` のサンプルの実行でしか通らない。手順 5 の `--include-destructive` が前提と書く
8. **IL2CPP で流す仕組みが無い** [X]。`verify_unity_windows.sh` は backend を設定しない（`:231-235`）。ProjectSettings の IL2CPP は Android だけ。`--il2cpp` の実行（Editor スクリプトで Standalone の backend を IL2CPP、x86_64 に固定し、終わったら戻す、専用の出力・XML）を 5.1 に足す
9. **8.1 に足す** [T]: close が呼び出しの中で drain を処理すること、変換の失敗、セッションが無いときの `OnDestroy` と quit、自分の Paste の中で render の受け口が走ること（M-17 で間接的に見る）

### 低優先度

**C（記述の整合）**

- 5.4「close を試したあとの変化の通知」の 1.x の欄は「未確認」ではなく「同じ」（1.x も試行のたびに `watcher_.Stop()`、`1.11.0:...cpp:255-257`）。差分の表から外す。履歴のイベントも止まる（`StopWatch`）[N][T]
- 5.4 の `BadImageFormat` の行: 1.x の `Initialize` はすべての例外を捕まえて `Unknown` にしていた（`:838-853`）。例外が呼び出し側に出たのは、初期化の前の `CanShutdownNow` など DllNotFound / EntryPointNotFound だけを捕まえる API [N]
- 4.2 の `WriteOptions` の値は確認済み（None=0、ExcludeHistory=1、ExcludeRoaming=2、Sensitive=3、`WindowsClipboardPayloads.cs:17-30`）。「実装時に確かめる」を消す [N][T]
- Timestamp の変換は、範囲外を 0 にするか `unchecked` にするかを決める。`unix_ms == 0` がちょうど 1970-01-01 のときも 0 になると注記する [N]
- 1.4 の `CANCELED`: 「drain が 4 回で終わらない」のほかに、`DispatchPendingDrain() == 0` でもすぐ返る（`WindowsClipboardApi.cpp:271-272`）[N]
- 1.1 の OP の番号は本計画だけのもので、親課題 README の OP-21〜47 と紛らわしい [N]
- 4.7 の drain の中の recover は、1.x も 2.0.0 も `NOT_INITIALIZED` で何もしない。そう書く [T]
- 5.1 に古い XML コメントを足す: `StringListResult.cs:11-13`、`HistoryItem.cs:22-24,30-31`、`ErrorCode.cs:20`、`WindowsClipboardManager.cs:1400-1401`、`verify_unity_windows.sh:13-15` の debug DLL の注記、`testing.md:376` の本数 [T]
- 5.1 にガードの列を足す（Dialog v5・Notification v7 と同じ形）[X]

## 不足項目

- なし（B-9 に含めた）

## 総合評価

移行の骨格（P/Invoke の層だけを置き換え、状態機械・台帳・drain・quit を残す）と、ネイティブ境界の型は正しい。

一方で **A1 が 6 件**ある（3 本の重なりを除いた数）。
- base64 の規則の抜け
- `CopyMultipleFormats` のエラーの優先順位
- 履歴の変換の失敗で完了が届かない
- セッションが無いときの終了
- 捕まえる例外の決め方
- id が空の項目

A2 のうち「ForceInitializeWhileDraining」は、テストの期待値の扱いについて開発者の判断が要る。

**このまま実装に入ることは勧めない。** A1 を直した v10 で再レビューする。
