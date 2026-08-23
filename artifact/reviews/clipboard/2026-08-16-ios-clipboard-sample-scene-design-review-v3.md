# レビュー結果

- 日付: 2026-08-16
- 対象ファイル: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v3.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- owner token により、Busy Start の非所有 callback が進行中 Start の `ControlPending` を解除できなくなった。`NonOwningToken` と owner 一致判定の二重防御、および競合 + teardown テストは v2 の高優先度指摘を適切に解消している。
- callback を「ログ → 必須 state / cleanup → screen guard → UI・後続操作」に分けたことで、画面破棄後にも必要な observation teardown と file cleanup を通常の UI 更新から分離できている。
- `RestartObservingButton` と `onChanged: null` により、Stop を挟まない registration replacement と、共通 `ClipboardChanged` だけによる event count を実機で確認できる。
- `public.data` と独自 UTI の copy / load 対応が分離され、M-13 本体を V-6 の成否から独立させた判断が妥当である。
- M-23 の parser ベンチを補助測定へ格下げし、native → `DllImport` → parser → decoded bytes を本体の測定経路として残したことで、元のメモリ契約との対応が回復した。
- file size 取得と directory cleanup の4経路、固定 seed の `LargePng`、UTF-16 length の定義が具体化され、再現性と所有権が明確になった。
- 55ボタンは多いが、M項目・V項目との対応が追跡できる検証ハーネスとして許容範囲である。

## 改善点

### 高優先度

該当なし。

### 中優先度

#### 1. Restart 失敗時に旧 observation が残るかは native source 上で確定できる

- セクション: 1.3、5.4、6.5.2
- 対象行: 70〜80、523、737〜748
- O-5 / V-7 は「Unity から判別できない」として `IsObserving = true` を維持するが、native `IosClipboardManager.startObserving` は最初に `stopObservingInternal()` を呼び、その後で新 scope を解決している。
- native source の `IosClipboardManager.swift:319-334` と `:377-385` により、scope 解決失敗を含む native Start failure では旧 notification token、scope、callback が既に破棄されている。native sample も Start failure で `isObserving = false` にしている。
- 現計画のまま Restart が `CLIPBOARD_UNAVAILABLE` で失敗すると、native は未観測なのにサンプルは `Observing: on`、Restart / Stop enabled のままとなり、status が実態と一致しない。

改善提案:

- native が返した Start failure では、初回 / Restart を問わず `IsObserving = false`、`ControlPending = false`、`StopRequestedAfterStart = false` にする。
- bridge 呼び出し前の managed rejection と native failure を分ける必要があるなら、error code ごとの方針を表にする。ただし観測中の Restart は main-thread・有効 scope・非 terminated Manager から発行するため、通常の実機導線では native 契約を基準にできる。
- V-7 を未確定事項から削除し、`Restart native failure → 未観測 → Start enabled / Stop disabled` の状態テストを追加する。

#### 2. `_observedScope` が開始時 scope ではなく callback 時の `_activeScope` を記録している

- セクション: 4.7、6.6
- 対象行: 449、805〜823
- 4.7 は観測開始時の scope を capture すると定めているが、擬似コードは成功 callback で `_observedScope = _activeScope` としている。
- 計画自身が V-5 で認識しているとおり、Start 前に始まった `CreatePasteboard` が Start pending 中に完了すると `_activeScope` が変わりうる。この場合 native は開始時 scope A を観測しているのに、status は callback 時の scope B を表示する。
- Restart でも同様に、状態所有者と観測対象 scope を同じ呼び出しへ対応付ける必要がある。

改善提案:

- `IssueStartObserving` で `IosPasteboardScope targetScope = _activeScope` を capture し、callback へ owner token と一緒に渡す。
- owner が一致し Start が成功した場合だけ `_observedScope = targetScope` とする。非所有 Busy callback は変更しない。
- `Create pending → Start(scope A) → Create success(active B) → Start success` で status が `active B (observing A)` になるテストを純粋な ownership helper または Controller seam に追加する。

#### 3. 画面外 deferred Stop の発行 helper と retry 終端が未確定

- セクション: 6.2、6.5.5、6.6
- 対象行: 579〜586、772〜778、804〜827
- `BeginResult` は必ず `SetResult(FormatRunning(...))` を呼ぶ。一方、画面破棄後の Start callback から呼ぶ `IssueStopObserving("observe.stop.deferred")` の定義がなく、通常の `BeginResult` を使うと mandatory cleanup の途中で破棄済み UI に触れる。
- `CompleteObservationControl` の deferred Stop 条件は `isStart` に限定されていない。`CompleteStop` failure 後に `StopRequestedAfterStart` が残る実装だと、同じ callback から Stop を再発行し続ける可能性がある。状態表も Stop failure 時にこの flag をどうするかを定義していない。

改善提案:

- `IssueStopObserving` に `showRunning` または screen-aware な context 作成経路を設け、画面外では sequence / marker をログ用に作るだけで `SetResult` しない。
- deferred Stop の発行条件を `owned && isStart && StopRequestedAfterStart && ShouldIssueStopNow()` に限定する。
- `CompleteStop` は成功・失敗の両方で deferred request を消費して `StopRequestedAfterStart = false` にする。Stop failure を再試行するなら無制限 callback retry ではなく、回数上限と最終ログを明記する。native Stop は現行契約上常に成功するため、サンプルでは再試行なしでもよい。
- `Start success after destroy → exactly one Stop issued → Stop failure → no second Stop` を状態テストへ追加する。

### 低優先度

#### 1. Errors note の「全ボタンが native layer に到達する」は `ErrDetectEmptyPatternsButton` と矛盾する

- セクション: 1.4、4.4
- 対象行: 88前後、345前後
- `DetectPatterns` / `DetectValues` の空配列は Manager が `CLIPBOARD_EMPTY_PATTERNS` を native 到達前に返すと記載されているが、確定文言は `Every button here reaches the native layer` としている。

改善提案:

- 文言を `Every button here demonstrates a stable CLIPBOARD_* result contract. C# constructor exceptions that occur before a Manager call are not represented.` などへ変更し、Manager-side rejection も正しく含める。

#### 2. S-25 系の開始状態を明記する

- セクション: 7.2
- 対象行: 947〜949
- S-25a の完了後は observing on なので、そのままでは S-25b の `Start Observing` が disabled である。S-25c は S-25a 直後を前提とする一方、表では S-25b の後に並んでいる。

改善提案:

- S-25a → S-25c を連続手順にし、再入場後の未観測状態から S-25b を開始するか、各手順の precondition / cleanup を明記する。

## 不足項目

- native Start failure が旧 observation を停止する契約を反映した Restart failure 遷移とテスト。
- owner token と同じ call context に開始時 `targetScope` を保持する契約。
- 画面外 deferred Stop が UI に触れず、Stop failure で無制限再発行しないことを固定する helper 契約とテスト。

## 総合評価

要修正。ただし高優先度の指摘はない。

v2 の observation ownership 問題は owner token と callback lifetime の分離によって解消されている。残件は Restart failure の native 契約、開始時 scope capture、deferred Stop の終端という中優先度の整合性であり、設計の基本構造を変更するものではない。

上記3点を明文化すれば、implement-sample-scene へ引き継げる状態になる。55ボタンという規模は問題ではない。
