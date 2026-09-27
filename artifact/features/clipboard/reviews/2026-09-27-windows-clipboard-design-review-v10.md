# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/features/clipboard/designs/2026-09-27-windows-clipboard-design-v10.md
- 前回: artifact/features/clipboard/reviews/2026-09-27-windows-clipboard-design-review-v9.md
- 機能名: clipboard
- プラットフォーム: Windows
- レビュー: v9 との差分に絞り、3 本を並行で行った。サブエージェント 2 本（ネイティブ境界 / 公開契約・テスト・自動化の前提）と Codex。どれも読み取りのみ
- 出典の略記: [N] = ネイティブ境界、[T] = 契約・テスト、[X] = Codex

---

## v9 の指摘の扱い

| v9 | 判定 | 理由 |
|---|---|---|
| A1-1 base64 | 解消 | 1.11.0 の `Base64Decode` を 1 行ずつ照合して同じ（「`=` は最後の 2 文字だけ」と「3 文字目が `=` なら 4 文字目も」で漏れなし）[N][X] |
| A1-2 CopyMultiple の優先順位 | 解消 | 列挙に CF_TEXT が抜けている（C）[N][X] |
| A1-3 履歴の変換の失敗 | 解消 | フックの形は B-2 [N][T][X] |
| A1-4 セッションが無いときの終了 | 一部 | 規則は正しいが、置き場所が層 2a とぶつかる（新 A2-1）[N][T][X] |
| A1-5 捕まえる例外 | 解消 | 4.3 の表は今のコードと一致 [T][X] |
| A1-6 id が空の項目 | 解消 | [N][T][X] |
| A2-1〜A2-5 | 解消（A2-2 は一部） | J-12 の範囲は新 A2-2 [N][T][X] |
| B-1〜B-9 | 解消（B-4、B-8 は一部） | Dispatch のテストの個別の一覧は手順 4 に先送り。`--il2cpp` の範囲と仕組みは B-3 [T][X] |
| C | 解消 | [N][T][X] |

## 強み

- 新しい A1 は 3 本とも無い [N][T][X]
- J-12 の期待値は、今のチェッカーの書き方（`?` と ` | `）で表せる。`?` の付いた行は何回でも一致し、押下の中の行の順は見ない（`check_windows_clipboard_sample_log.py:341-344,416-424`）。サンプルの注記の行は NG にも警告にもならない [T]
- 7.3 の「1.x で記録」のテストは 1.x で実行できる（DIB と `@@@@` の組は 1.x で `InvalidData`）[T]
- 5.4 の行は、1.x の実際のログ（`session3.log:64` の `attempt 1 not finished yet: Canceled`）とも合う [T]
- `--il2cpp` は実現できる（この Editor に win64 の IL2CPP のモジュールがあり、`ProjectSettings` は後片付けの対象）[T]
- close が待っている要求をその場で配送する流れと、`BUSY` になる条件（lease が WinRT の継続に渡ったあと）は、v10 の記述どおり [N]

## 改善点

### 高優先度

- A1: なし

### 中優先度

**A2（実装やテストで必ず表に出る）**

1. **セッションが無いときの判定の置き場所** [N][T]
   - v10 は「ガード（`#if`）の外に置く」とした。Editor ではハンドルを持たないので、試行のたびにこの分岐に入り、`NativeShutdownForTests`（`WindowsClipboardManager.cs:2716-2724`）より前で成功になる。フックを使う層 2a の既存テスト（Busy、PartialState、時間切れの drain など）が働かなくなる
   - 4.4 の「終了以外の操作はハンドルが Zero なら `NotInitializedByHost`」も同じで、`SetStateForTests(Running)` で動く受付のテストが拒否に変わる
   - 修正: どちらの判定も `UNITY_STANDALONE_WIN && !UNITY_EDITOR` の中に置く。終了の判定は `InvokeNativeShutdown` の中の close の直前。`FinishShutdownAttempt`（と `DrainRequestRegistry`）は今どおり通す
2. **J-12 で緩めるのが 1 行だけでは、次の押下が落ちうる** [T]
   - 2.0.0 で drain が 1 回目で終わると、サンプルの `LateUpdate` は成功が続く限り 3 フレームの間 `Initialize` を呼ぶ（`WindowsClipboardManagerExampleController.cs:801-813`）
   - テストは押下のあと 1 フレーム待ち、応答が出そろったらさらに 1 フレーム待って次を押す（`WindowsClipboardSampleRunPlayerTests.cs:268,290-292`）。dispatcher と Manager の `Update` の順は決まっていないので、frame=3 の `initClipboardManager OK` が次の押下「Initialize」（`expected.py:158`）の行として数えられうる
   - 修正: 次の押下の期待値に `?initClipboardManager OK frame=3` を足す。記述を「2 か所」に直す

**B（検証手段の穴）**

1. **E-21 の層 2a のテストは回帰テストにならない** [N][T]。Editor の `InvokeNativeShutdown` はフックが無ければ今でも成功を返す（`:2725-2731`）。判定を `WindowsClipboardCApiMapping` の純粋な関数（例: `ShouldSkipNativeClose(IntPtr)`）にし、層 1 で確かめる。分岐の置き場所はコードレビューで見ると 8.1 に書く
2. **注入のフックの形を決める** [T]。今の `InjectCompletionForTests(id, err, json)` は JSON を受ける（Integration `:1381`）。E-20（写す途中の例外）と E-22（close の中の完了）を注入する形を 5.1 で決める
3. **`--il2cpp` の範囲と仕組み** [T][X]
   - `report_tests` だけで判定すると、Quit の実行、S-2 の期待値のチェッカー、層 3 が IL2CPP では流れない。どこまで流すかを書く
   - 仕組みは Test Framework の `-testSettingsFile`（`scriptingBackend`）でも足りうる。Editor のスクリプトを使うなら、それも 5.1 に載せる
   - VS の C++ のツールチェーンが要ることを前提に書く

### 低優先度

**C（記述の整合）**

- `getClipboardHistory` の行は `NG Canceled` を必須のまま残す [N]。close は完了より前に `Canceled` で配送する（`WindowsClipboardHistoryCoordinator.cpp:447-452`）ので、移行後に OK が出る可能性は 1.x と変わらない
- 5.4 の CopyMultipleFormats の行: 結果が変わって見えるのは、前の要素が `InvalidData`（DIB / DIBV5 / HDROP / **CF_TEXT の ANSI 変換**）のときか、flags が不正なときだけ。名前・重複・種類の誤りは 1.x も移行後も `InvalidParameter` [N]
- 5.4 の `BadImageFormat` の行: 1.x で `Unknown` になったのは `Initialize` だけでなく、`StartRequest` と終了の試行も同じ [T]
- 4.2 の「base64 を先に全部復号」は、`TryValidate` の全件のループ（`InvalidArgument`）より後と書く [T]
- 4.2 の「空白」は U+0020 だけ。`char.IsWhiteSpace` を使わないと書き、層 1 に `\v` / `\f` / NBSP を含む入力は不正、を足す [N]
- 4.3 の「`PasteFiles` の 0 件は `Success`」は起こりえない（1 件も無ければ Core が `INVALID_DATA`、`WindowsClipboardCore.cpp:347`）。`GetFormats` だけにする [N]
- 4.3 の表に「`CancelRequest`、`SetHistoryEventsEnabled`、`ReserveDeferredFormats`、`RecoverDeferredState` も `InvokeWrite` を通る」と書く [T]
- 4.4 の「Draining 中は `ShuttingDown`」は `CanShutdownNow` には当てはめない（今も状態を見ずにネイティブを呼ぶ、`:976-1001`）[T]
- 近道の経路では、サンプルの注記「expected ShuttingDown while the drain runs」が実際と食い違う。`expected.py` のコメントで説明する [T]
- `BUSY` の場合でも、`Canceled` は 1 回目の close の中で届く（1.x はメッセージポンプを待った）と 4.7 に足す [N]
- 5.4 の `render_target_set` の失敗の発生元は「ネイティブ（C ABI。C# の受け口が返す）」[X]

## 不足項目

- なし

## 総合評価

v9 の A1 6 件はすべて解消し、**新しい A1 は無い**。A2 は 2 件（セッションが無いときの判定の置き場所、J-12 の次の押下）で、どちらも直し方は決まっている。

**v11 で A2・B・C を直し、再レビューはしない。** 実装に進める状態になる。
