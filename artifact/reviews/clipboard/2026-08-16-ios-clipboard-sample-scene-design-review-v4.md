# レビュー結果

- 日付: 2026-08-16
- 対象ファイル: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v4.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- native `startObserving` の実装順を根拠に、owned Start failure を初回 / Restart とも未観測へ落とす契約が確定した。状態遷移、enabled 状態、テストが同じ結論に揃っている。
- owner token と `targetScope` を同じ call context で capture し、owner 一致 + 成功時だけ `_observedScope` を更新することで、active scope と実際の observed scope を正しく分離できている。
- `BeginResult` を screen-aware にし、deferred Stop の発行条件を Start success に限定したため、画面破棄後 cleanup が UI に触れず、Stop callback から再帰的に Stop を発行しない。
- `CompleteStop` が deferred request を成功・失敗の両方で消費し、再試行しない方針は、native Stop が常に成功する現契約と整合している。
- owner 競合、Start failure、Restart failure、target scope、deferred Stop の発行回数をテスト対象に含め、主要な状態バグに回帰防止が付いた。
- Errors note と S-25 系の開始状態も修正され、v3 の低優先度指摘は解消された。
- 55ボタンは検証項目との対応が明確であり、規模自体は問題ではない。

## 改善点

### 高優先度

該当なし。

### 中優先度

#### 1. `ErrObserveMissingNamedButton` が missing scope を `IssueStartObserving` へ渡せない

- セクション: 6.5.4、7.2
- 対象行: 786〜817、1057
- `IssueStartObserving(string marker, int owner)` は内部で必ず `targetScope = _activeScope` を capture する。
- 一方、計画は `ErrObserveMissingNamedButton` も同 helper と `BeginStart()` を経由するとしているが、missing named scope を渡す引数がない。未観測時の active scope は通常 general なので、S-25d は `CLIPBOARD_UNAVAILABLE` ではなく Start 成功になりうる。
- 万一成功した場合を owner state で追跡する方針自体は正しいが、対象 scope を明示できなければエラー導線として成立しない。

改善提案:

- helper を `IssueStartObserving(string marker, int owner, IosPasteboardScope targetScope)` とし、通常 Start / Restart / Busy は呼び出し側で `_activeScope` を capture して渡す。
- `ErrObserveMissingNamedButton` はクリックごとに生成した missing named scope を明示的に渡す。pasteboard 名は引き続きログへ出さない。
- 状態 helper のテストに加え、wiring または純粋な発行 helper のテストで、error ボタンが general ではなく supplied target scope を使うことを固定する。

#### 2. S-25b は deferred Stop の完了前に外部変更すると teardown 判定が race する

- セクション: 7.2
- 対象行: 1053〜1055
- S-25a 完了後の Back To Home では Stop が非同期に発行される。再入場時の新 Controller は既定値として `Observing: off` を表示するため、その表示だけでは旧 Controller の Stop callback 完了を証明しない。
- Stop 完了前に他アプリで copy すると旧 native observation が正当にイベントを発生させ、新 Controller が共通 `ClipboardChanged` を受け取る可能性があり、teardown 漏れとの区別がつかない。

改善提案:

- S-25b に「`observe.stop.teardown` の成功ログを待つ」手順を追加してから外部変更する。
- 画面外 callback は UI に出せないため、marker / sequence / success を shape-only log に残す契約を明記する。
- timeout 内に Stop completion が来なければ teardown failure として記録する。

### 低優先度

#### 1. main-thread rejection のコード名が実装と一致しない

- セクション: 6.5.3
- 対象行: 778
- 表では `CLIPBOARD_NOT_MAIN_THREAD` としているが、実装の公開定数と返却コードは `CLIPBOARD_MAIN_THREAD_REQUIRED` である。

改善提案:

- コード名を `CLIPBOARD_MAIN_THREAD_REQUIRED` に修正する。

#### 2. `CLIPBOARD_MANAGER_DESTROYED` 時の旧 observation の説明が OnDestroy cleanup を反映していない

- セクション: 6.5.3
- 対象行: 777
- 表は旧 observation が「残る」としているが、Manager の `OnDestroy` は tombstone 設定後に native Stop を試行する。通常経路では observation は cleanup される。
- cleanup 例外時には残る可能性があるものの、以後の callback は tombstone で破棄され、サンプルは Manager を破棄しないため実動作への影響はない。

改善提案:

- 「OnDestroy が native Stop を試行。例外時は native subscription が残る可能性があるが callback は破棄され、Manager 操作は以後拒否」と正確に記載する。

## 不足項目

- Start helper が caller-supplied scope を受け取り、missing named error 導線を active scope から独立させる契約。
- teardown 実機確認で旧 Stop completion を待ってから変更イベントを発生させる同期点。

## 総合評価

要修正。ただし高優先度の指摘はなく、基本設計は実装可能な状態に達している。

v3 で残っていた native failure、scope capture、deferred Stop の終端はすべて適切に解消された。残る主な修正は `ErrObserveMissingNamedButton` へ explicit target scope を渡すことと、S-25b の非同期 Stop 完了待ちであり、いずれも局所的である。

この2点と記述上のコード名を修正すれば、implement-sample-scene へ引き継げる。
