# PC が重いと、Clipboard の終了が上限を超えて Manager が戻れなくなる

- 記録日: 2026-10-03
- 分類: Windows Clipboard 固有。設計どおりの振る舞いが、負荷の高い環境で表に出たもの
- 発見経緯: native-toolkit 1.12.0 の修正版 DLL で Player テストを流し直したとき（2026-09-30、IL2CPP）。
  PC の負荷が高く、履歴の要求が終わらないうちに終了処理が走った
- 対応方針: **リリースは止めない。** 放棄を避けるための設計（Clipboard 設計 v12 の J-5、E-16）どおりの動きで、
  利用者向けにはマニュアル 1.12.0 の Windows の「Manager lifetime and shutdown」に、起きることとやり直し方を書いた。
  上限の決め方や、自動のやり直しを入れるかは、次の版の設計で扱う
- 進捗: 未着手（マニュアルへの記載のみ）

---

## 何が起きたか

2026-09-30 の IL2CPP の Player テスト（`verify_unity_windows.sh --skip-build --include-destructive --il2cpp`）で、
Clipboard のテスト 35 本が `ShuttingDown` で落ちた。順番は次のとおり。

1. 履歴の取得が 5 秒以内に終わらなかった（`ClearUnpinnedHistoryAsync_RemovesUnpinnedItems`）
2. 続くテストの終了処理で、close が `Busy` を返し続けた。履歴の要求の WinRT の処理が走っている間、close は `Busy` になる
   （native-toolkit の Clipboard.h の説明。2026-09-30 に native-toolkit 側で文書を直した）
3. drain の上限（60 フレームか 2 秒）を超えて `ShutdownTimeout`（1006）になり、状態は `ShutdownFailed` に残った
   （`[WindowsClipboardManager][AdvanceDrain] shutdown exceeded its budget after 2 attempts.`）
4. その Player の中では、以後の `Initialize` がすべて `ShuttingDown`（1011）で断られ、後続のテストが連鎖して落ちた

同じ実行では、PowerShell の起動に 30 秒以上かかるほど PC が重かった（Dialog の閉じ役が 30 秒以内に起動しなかった）。
2026-10-03 に PC が空いた状態で同じ DLL を流し直すと、Mono と IL2CPP の両方で Player 93 / 93 だった。

## 今の振る舞い（実装を読んだ結果）

- `ShutdownFailed` の間、`Initialize` と各 API は `ShuttingDown` を返す（`WindowsClipboardManager.cs` の `Initialize` と状態の確認）
- ネイティブのセッションは放棄せずに保持する。close が成功しないまま free すると、そのプロセスでは二度と作れないため
- `TryShutdown` / `ShutdownWithDrain` をもう一度呼べば、終了を試み直す（`Uninitialized` と `ShutDown` 以外の状態から走る）。
  close が成功すれば `ShutDown` になり、`Initialize` できる
- 失敗のメッセージ（`TearDown : Initialize: ShuttingDown`）のとおり、テストの後片付けは終了をやり直さずに `Initialize` を呼んでいたので、その Player の中では戻れなかった

## 案

| # | 内容 | 影響 |
|---|---|---|
| 1 | 今のまま（マニュアルにやり直し方を書いた） | 変化なし |
| 2 | 上限を時間でなく「履歴の要求が終わるまで」に寄せる、または上限を延ばす | 終了を待つ時間が長くなりうる。quit の待ち時間との兼ね合いを決める必要がある |
| 3 | `ShutdownFailed` から、次のフレーム以降に自動でやり直す | 利用者が気付かないうちに回復する。やり直しの回数と間隔を決める必要がある |
| 4 | テストの TearDown で、`ShuttingDown` なら終了のやり直しを待ってから `Initialize` する | 製品の振る舞いは変わらない。重い環境でも 1 本の失敗が連鎖しなくなる |

**推奨は 4 を先に。** テストの連鎖を止めるだけで、製品の契約に触れない。2 / 3 は次の版の設計で、quit の扱いと合わせて決める。

## 参照

- 設計: `artifact/windows/clipboard/designs/2026-09-27-windows-clipboard-design-v12.md`（4.7、J-5、E-16）
- 結果: `artifact/windows/clipboard/results/2026-09-29-windows-clipboard-implementation-feature-result-v7.md`
- マニュアル: `manual/1.12.0/clipboard.md`（Windows の「Manager lifetime and shutdown」）
- native-toolkit の記録: `artifact/topics/windows-architecture/results/2026-09-30-windows-clipboard-close-busy-finding.md`
- 検証の前提: `artifact/topics/cross-platform-testing/README.md` の「検証を流すときの前提」
