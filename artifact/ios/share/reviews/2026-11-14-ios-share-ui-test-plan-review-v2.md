# iOS Share UI 自動テスト計画 v2 のレビュー v2（差分のレビュー）

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-share-ui-test-plan-v2.md](../designs/2026-11-14-ios-share-ui-test-plan-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。2 回目なので差分のレビュー
- 1 章・2 章はレビュアーの出力の要点をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント（A1 0、A2 1、B 0、C 2）

- 判定: A1 の SH-13 は直った（`share1`・`share2`・`share-event` を別の id で数え、集合で比べる。予想〈share2@fail が 1 本、share1 が 0 本、event が 2 本、`..`〉はタグ `1.13.0` と合う）。黙って捨てる経路（SH-16・17・23〜25）は直った（5 件とも予想がタグと合う）。不正な URL（SH-9・10）は直った。`activity` の書き方は直った。背景からの呼び出し（SH-18）は一部（予想の文言は合うが、遅らせた呼び出しが背景の間に本当に走るかが怪しい）
- R2-1 [A2] SH-18: 遅らせる仕組みは `NtkUiHarnessOverlay.mm` の `dispatch_after(main_queue)` だけで、背景で走り続ける処理（`beginBackgroundTask` など）が無い。ホームに戻すとアプリは数秒以内に止められ、3 秒のタイマーは戻した後に発火しうる。戻した後にシートが出ると閉じる手順が無い。直し案: きっかけを `UISceneDidEnterBackgroundNotification` にするか、遅れを `beginBackgroundTask` で包む。「シートが出ていれば閉じる」を足す。戻った後の `state` も記録する
- R2-2 [C] SH-16・23・17 の `2.0.0` の列が C# の写し方より先に決まっている。「`INVALID_PARAMETER` の見込み。C# で先に断るなら写し方の設計で決める」と書く
- R2-3 [C] SH-17 は 2.0.0 ではシートが出ないので「シートが出れば」とする。1.x の予想を全文 `[share@ok] completed=False activity=<null>` で書き添える
- 6 章: 直さなかった R-4・R-5・R-7・R-8 が理由と扱う場所つきで載っている

## 2. Codex（A1 0、A2 1）

- 前回の 4 件はすべて修正済み（SH-9・10、破棄経路、SH-13、背景呼出）
- A2: SH-17 は、移行後の操作が成立しない。移行後は NULL を含む要素が表示前に拒否され（`ShareMapping.swift:54`、`CApiString.swift:13`・`55–56`）、表示処理が登録されない（`ShareReceiver.swift:48`・`55–56`）。修正: シートが出た場合だけコピーを確かめて閉じる。移行後はシートが出ないことと、`INVALID_PARAMETER` の失敗が 1 回返ることを確かめる

## 3. 判断

- **ラウンド 2 で、2 人とも A1 が 0 になった。レビューを止める**
- 止めた後の v3 で直すもの（新しいレビューには回さない）: SH-17 の操作を「シートが出れば」にし、1.x の全文を書く（2 人の A2・C）。SH-18 の呼び出しを、`UISceneDidEnterBackgroundNotification` を受けてから行う形にし、`beginBackgroundTask` で包み、「シートが出ていれば閉じる」と戻った後の `state` を足す（R2-1）。SH-16・17・23 の `2.0.0` の列に「C# で先に断るなら写し方の設計で決める」を足す（R2-2）
