# 実装レビュー結果 v5（判定ラウンド）

## 基本情報

- 日付: 2026-09-08
- 機能名: clipboard / 対象プラットフォーム: Windows
- レビュアー: **Codex CLI 0.153.4（`gpt-5.5`、reasoning effort: high）** — v3 / v4 とは別モデル
- レビュー対象: `git diff 284a607..HEAD -- Packages/ scripts/`（修正 4 ラウンド分すべて）
- 実行方法: `codex exec -s read-only -c model_reasoning_effort="high"`

## このラウンドの位置づけ

事前に判定基準を宣言して臨んだ。

- A が 0、または shutdown / drain **以外**に散在 → 仕組みの修正が効いた
- **また shutdown / drain に A が集中** → アプローチが誤り。コードレビューの反復を打ち切る

## 結果（実装側で全件をコードと突き合わせ、事実として確認済み）

**A 4 件。内訳は shutdown・drain 3 / 非同期・配送 1。読み出し・遅延レンダリング・その他は 0。**

レビュアー自身の判定:

> shutdown / drain の再発は止まっていません。原因は `DrainSession` を導入したものの、
> **終端遷移の所有がまだ `OnWantsToQuit` / `OnDestroy` / `AbortDrain` / `QueueDelivery` に分散している**ことです。

---

**レビュー結果**

### A-1（振る舞い / shutdown・drain）
`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:927`, `:2833`

- **事実**: `ShutdownWithDrain` 呼び出し時点で非アクティブなら `AdvanceDrain(force: true)` しますが、drain 開始後に `gameObject.SetActive(false)` / `enabled = false` された場合の `OnDisable` 遷移がありません。以後 `Update()` が来ないため `s_drain` は `Running` のまま残ります。
- **再現シナリオ**: `ShutdownWithDrain(callback)` → 1 フレーム進めて native が `Busy` を返す → Manager を非アクティブ化 → 以後 callback は届かず、`ShutdownWithDrain` の再呼び出しも既存 drain に合流して戻るだけになる。
- **根拠**: 設計 7.4 / 8.4。`ShutdownWithDrain` は最終結果を配送する契約であり、8.4 は未配送を残さないことを要求している。v4 A-2 の「起動後に runner が死ぬ」系が、Update 化後も `OnDisable` で残っています。

### A-2（振る舞い / shutdown・drain）
`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2937`, `:2945`, `:3079`

- **事実**: drain が `DeliveryQueued` に入った後で quit が来ると、`OnWantsToQuit` は `WaitingForDrain` にしますが、既存 session が `Running` でないため何も進めず `false` を返します。その後 queued delivery は `ClaimDrainDelivery()` → `SettleDrain()` だけを通り、`ResumeQuit()` を呼びません。
- **再現シナリオ**: `ShutdownWithDrain(callback)` → `StepDrainForTests()` 相当で `DeliveryQueued` まで進める → dispatcher が配送する前に quit → 次フレームで callback は届くが `s_quitState` は `WaitingForDrain` のまま、次の quit も `false`。
- **根拠**: 設計 7.4。quit は成功・タイムアウト・終端失敗のすべてで必ず再開する契約。`ShutdownWithDrain` に quit が後から合流した場合も配送する、と明記されている。

### A-3（振る舞い / shutdown・drain）
`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:734`, `:745`, `:2962`

- **事実**: `OnDestroy()` は独自に `RunShutdownAttempt()` しますが、その結果を既存 `DrainSession` に反映せず、直後の `AbortDrain()` が古い `session.Progress == NotYet` を見て `ShutdownTimeout` に上書きします。
- **再現シナリオ**: drain が 1 回目で `Busy` → destroy 時の best-effort shutdown が `completed == true` → native は実際には完了済みなのに、drain waiter には `ShutdownTimeout` が配送される。
- **根拠**: 設計 7.4。shutdown の後始末を起点ごとに分けず、1 つの終端で状態・配送・quit を決める契約。現状は `OnDestroy` の試行結果と drain session の結果所有が分離している。

### A-4（振る舞い / 非同期・配送）
`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2400`, `:2435`, `:3249`

- **事実**: `s_deliveringBeforeQuit` は `Dispatch()` だけが見ています。非同期 API の受付前拒否・キャンセル済み token は `RejectRequest()` → `QueueDelivery()` 経路なので、quit 直前 settle 中でも dispatcher に enqueue されます。
- **再現シナリオ**: quit に合流した drain の callback 内で `GetHistoryAsync()` または `GetHistory(callback)` を呼ぶ → state は shutdown 済みなので拒否結果が作られる → `QueueDelivery()` が enqueue → `ResumeQuit()` 後は次の `Update` が保証されず、callback / Awaitable が未完了になり得る。
- **根拠**: 設計 8.4。「quit を再開する直前の drain settle」では、settle 中に始まった操作の配送も同期になる、と明記されている。

### B-1（検証手段 / 遅延レンダリング）
`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:1993`

- **事実**: `s_renderStaging` が有効な「`ReserveDeferredFormats` の P/Invoke 呼び出し中に render callback が同期再入する」状態を作る seam がありません。設計 9.2 にある `InvokeRenderDuringReserveForTests` 相当も実装されていません。
- **再現シナリオ**: `s_renderStaging = Merge(...)` を削除しても、現在の EditMode / PlayMode テストはその再入窓に入れないため落ちない。一方 Player では履歴サービス等が予約直後に新 format を materialize すると provider 解決に失敗する。
- **根拠**: 設計 7.7 / 9.2。呼び出し中に旧・新どちらの形式が要求されても解決できることを検証対象としている。

**問題なしと判断した観点**

- 読み出し分類、二段階 read、空文字と empty の分離: 問題なし。
- `RaiseIsolated` による Windows 共通イベント購読者隔離: 問題なし。
- Player 専用 P/Invoke 宣言の明白な `nameof` 取り残し・コンパイル破壊: 見当たりません。
- `StepDrainForTests` は production の `DeliveryQueued` 窓を作っており、seam として妥当。
- `SuppressRegistrationDisposalForTests` は production の遅延 dispose 窓を拡大する seam として妥当。
- `NativeShutdownForTests` / `PlatformAvailableForTests` は Editor で native 境界を代替する seam として妥当。ただし上記 A の経路は未検証。

`git diff --check 284a607..HEAD -- Packages/ scripts/` は問題なし。Unity は指示どおり起動していません。ファイル作成・編集もしていません。

1. **総合評価**: 要修正（重大）
2. **A 区分の件数と領域別内訳**: A 4 件。shutdown・drain 3 件、非同期・配送 1 件、読み出し 0、遅延レンダリング 0、その他 0。
3. **判定**: shutdown / drain の再発は止まっていません。原因は `DrainSession` を導入したものの、終端遷移の所有がまだ `OnWantsToQuit` / `OnDestroy` / `AbortDrain` / `QueueDelivery` に分散していることです。
4. **v4 A-1〜A-6 の未解消**: A-2 は未解消。A-4 は非同期 request 再入分が未解消。A-5 の元指摘は概ね解消していますが、`DeliveryQueued` に quit が合流する派生欠陥が残っています。