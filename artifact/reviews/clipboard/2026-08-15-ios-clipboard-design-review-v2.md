# レビュー結果

- 日付: 2026-08-15
- 対象ファイル: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v2.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- v1 レビューの全指摘に対し、採否と根拠が冒頭の対応表で追跡可能になっている。
- C `bool` の ABI を `[MarshalAs(UnmanagedType.I1)]` で確定し、成功／失敗双方の IL2CPP/ARM64 実機確認を追加した。
- 同一操作の結果誤配送を single-flight で防止し、FIFO を採れない理由と `LoadItem` の制約を公開契約として整理した。
- native で確定済みの `number`／`moneyAmounts[].amount` を `double` に修正した。
- public 型、malformed response、change event、大容量 response、XCFramework 成果物境界が具体化され、v1 の主要な曖昧さは解消された。
- `IosClipboardCopyOptions` を sealed class + factory とし、privacy 既定値を構造的に保護した。
- テストが ABI、busy、遅延完了、parser 不正入力、実機メモリ、build/link まで拡張されている。

## 改善点

### 高優先度

#### 5.6.3 / 5.6.6 busy 結果の dispatch

- 問題点: busy 時は「pending スロットと in-flight を変更しない」と定義している一方、通常の `FireXxxResult` は pending callback を取得・消去し、`EndOperation` も実行する。busy 結果をこの経路へ流すと、進行中 A の callback を busy 呼び出し B が奪い、A の in-flight marker も解除してしまう。v1 で指摘した誤配送が別経路で再発する。
- severity: high
- 改善提案: busy 専用の `DispatchRejectedResult(result, rejectedCallCallback)` を定義し、共通 event と B 自身の callback だけを dispatch する。この経路では pending スロット、`TakeOperationCallback`、`EndOperation` に一切触れないことをコード例とテストで固定する。

#### 5.6.1 / 5.7 `Awaitable` の Manager 破棄時契約

- 問題点: `XxxAsync` の completion source は per-call callback にしか保持されないが、`OnDestroy` は `ClearAllPendingCallbacks()` でそれを破棄する。native callback が後着しても source へ到達できず、進行中の `Awaitable` は永久未完了になる。「成功・失敗・busy のいずれでもハングしない」という 7.2 の保証に Manager 破棄が含まれていない。
- severity: high
- 改善提案: 初期リリースでは `Awaitable` 15 メソッドを外し、callback 版に限定することを推奨する。`Awaitable` を維持する場合は、破棄時に全 pending source を専用失敗結果または cancellation で必ず完了させ、native callback 後着を無害化する lifecycle coordinator と PlayMode テストを追加する。

### 中優先度

#### 3.4 公開 `Awaitable` API 一覧

- 問題点: 「15 操作すべて」としながら署名例は 3 メソッドのみで、callback 版と同じ完全な公開 API 表になっていない。特に `StartObservingAsync` の永続 callback、`CancelLoadsAsync`／`StopObservingAsync`、各引数順の契約を実装者が補完する必要がある。
- severity: medium
- 改善提案: `Awaitable` を残す場合は 15 メソッドすべての完全な署名を列挙する。外す場合は 3.4、5.7、関連テスト、DoD 項目 7 を削除する。

#### 5.8 C# public API の呼び出しスレッド

- 問題点: `s_inFlight` は main thread 専用とする一方、public メソッドを main thread からのみ呼べる契約が明記されていない。background thread から呼ばれると `HashSet`、callback スロット、`Debug.Log`、`UnityMainThreadDispatcher.Instance` の生成が競合または Unity API 違反になりうる。native façade が any-thread safe であることと C# Manager の安全性は別である。
- severity: medium
- 改善提案: 全 public API を Unity main thread 限定と明記し XML コメントに反映するか、入口を main thread へ marshal してから single-flight 状態を操作する。main-thread assertion または background-call rejection のテスト方針も追加する。

#### 5.9.2 `byteCount` の信頼境界

- 問題点: `byteCount` を先に確認して割り当てを防ぐ方針は、JSON 内の宣言値と base64 の実デコード長が一致する前提である。malformed response に対する防御を掲げるなら、過小な `byteCount` で大きな base64 を送るケースを防げない。また成功結果の `ByteCount` と `Data.Length` の不一致契約もない。
- severity: medium
- 改善提案: base64 span の長さから最大 decoded length を割り当て前に計算して上限確認し、デコード後は `Data.Length == byteCount` を必須にする。不一致は B-6 とし、境界値テストを追加する。

### 低優先度

#### 5.6.4 Start/Stop observation の交差

- 問題点: `StartObserving` と `StopObserving` は別 single-flight key のため並行可能だが、`StopObserving` 成功時は `s_onChanged` を無条件に消す。callback 順序が呼び出し順と異なる場合、古い Stop の完了が新しい Start の登録を消す可能性がある。
- severity: low
- 改善提案: observation generation を C# 側にも持たせ、Stop が対象としていた世代だけをクリアするか、Start/Stop を同一の observation-control key で直列化する。交差順序テストを追加する。

## 不足項目

- busy rejection が進行中 callback／in-flight marker を変更しない専用 dispatch 経路。
- Manager 破棄時に pending `Awaitable` を必ず完了または cancel する契約。
- C# Manager public API の呼び出しスレッド契約。
- response の宣言 `byteCount` と base64 実サイズの整合性検証。
- Start／Stop observation の交差完了に対する世代管理。

## 総合評価

v1 の高・中・低優先度指摘は実質的にすべて反映され、native 契約と公開型の精度は大きく向上した。ただし busy 結果を通常 dispatch から分離しない限り、進行中 callback の喪失が再発する。また、現在の lifecycle では `Awaitable` が Manager 破棄時に永久未完了となるため、15 個の `XxxAsync` をこの初期実装へ含めるのは推奨しない。callback 版に絞り、busy 専用 dispatch と thread／observation 契約を修正した v3 で実装へ進むのが安全である。総合評価は「要修正（高優先度 2 件）」とする。
