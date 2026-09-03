# 実装結果レポート

## 基本情報

- 日付: 2026-09-03
- 機能名: clipboard
- 対象プラットフォーム: macOS
- ブランチ: `feature/UNT-10`
- 実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md`
- **対象範囲: 段 2（Manager 骨格 + Copy / Append / Read / ReadData / Clear）**
- 前版: `2026-09-03-macos-clipboard-implementation-feature-result-v2.md`（段 1）

## 0. 位置づけ

design-v10 の 12 章の 3 段構成のうち **段 2**。設計上のリスク（5.6.1 / 5.6.3 / 5.6.4 / 5.6.6 / 5.6.8）が集中する段である。

**監視には一切触れていない。** 12 章の指示どおり、公開 `StartObserving` / `StopObserving`、変更コールバック、active / pending 登録、D-16 の再発行規則はすべて段 3 に送った。段 2 には監視を開始する手段が存在しないため、`OnDestroy` の `stop` に空実装を渡しても実害が無い。

## 1. 実装サマリー

### 1.1 計画書由来の実装

| 項目 | 計画書 | 実装 |
| --- | --- | --- |
| Singleton / `Instance` / `DontDestroyOnLoad` / 重複破棄 | 5.6 | `MacShareManager` と同形 |
| tombstone（`s_isTerminated` / `IsTerminated` / 再生成時 `LogError`） | 5.6.1 | 実装 |
| 公開イベント 4 本 | 12 章 | `OwnershipChanged` / `ReadCompleted` / `ReadDataCompleted` / `ClearCompleted`。残り 9 本は段 3 |
| 決定 2（拒否経路は `this` の event、通常経路は `_instance?.Xxx`） | 5.6.2 | `commonSelector` に `() => this.XxxCompleted` を渡す形で実装 |
| 決定 1（事前検証はネイティブに対応コードが無いものだけ） | 5.6.3 | `content` / `ownership` の null → 9005、サイズ超過 → 9007。`utType` は検証しない |
| `ValidateRequestSize` / `EffectiveMaxRequestBytes` | 5.6.3 | `long` 累積、null 要素は 0 バイト扱い |
| `TryPassGuards`（段階 1〜4）と `TryBeginOperation`（段階 6）の分割 | 5.6.4 | 実装。段階 5 の JSON 構築を段階 6 の**前**に置いた |
| 段階 5・6 の拒否 dispatch を呼び出し側が行うための `commonSelector` / `failure` のローカル持ち上げ | 5.6.4 | 5 操作すべてで実装 |
| `TakeOwnershipCallback`（copy / append の 2 スロット） | 5.6.4 / 5.6.12 | 実装 |
| `DispatchRejectedResult` / `DispatchOffThreadRejection` / `Dispatch` / `InvokeInOrder` | 5.6.4 / 5.6.7 | `IosClipboardManager` と同形。try/catch は共通と個別で分離 |
| `s_dispatcher` の所有・`OnDestroy` で null にしない・off-thread は `(object?)` 判定 | 5.6.6 | 実装 |
| `RunDestroyCleanup(stop:, managedCleanup:)` の 2 引数版 | 5.6.8 | 新設。`stop` は空実装（段 3 で差し替え） |
| per-call スロット 5 本 + `ClearAllPendingCallbacks` | 5.6.12 | 実装。残り 12 本は対応する操作と同じ段で追加 |
| `DiscardIfTerminated` | 5.6.1 | 5 操作すべてに適用 |
| IL2CPP 制約（`static` コールバック・例外を漏らさない・`static readonly` delegate・実処理を guard 外へ切り出し） | 5.6.9 | 実装 |
| テストシーム 9 本 | 5.6.10 / 12 章 | `ResetForTests` / `BridgeAvailableOverrideForTests` / `MaxRequestBytesOverrideForTests` / `CompleteOwnershipForTests` / `CompleteReadForTests` / `CompleteReadDataForTests` / `CompleteClearForTests` / `IsInFlightForTests` / `InFlightCountForTests` / `HasAnyPendingCallbackForTests` |
| ログ方針（shape / count / flag のみ・ガードチェーンの後） | 5.6.11 | 実装。逸脱理由をクラス `<summary>` の `<para>` に明記 |

### 1.2 実装時の追加判断 3 件

**1. `InvokeNative` の `onNativeFailureResult` を必須引数にした（計画書からの逸脱）**

計画書 5.6.4 は `Action<string>? onNativeFailureResult = null` とし、null のとき `MacClipboardOperationResult` を組み立てるフォールバックを持つ設計だった。**段 2 には `MacClipboardOperationResult` を返す操作が 1 つも無い**（removePasteboard / startObserving / stopObserving はすべて段 3）ため、そのフォールバックは段 2 では到達不能な分岐になる。到達不能な分岐に挙動を発明するより、引数を必須にしてコンパイラに保証させる方が、計画書が防ごうとしていたリスク（省略するとスロットがリークする）に対して強い。

同じ理由で `inFlightKey` と `onNativeFailure` も段 2 の引数から外した。前者はフォールバック専用、後者は監視の pending 破棄専用で、どちらも段 2 に使い道が無い。**段 3 で 3 引数とも復活させ、計画書のシグネチャに戻す。** その旨をメソッドの XML コメントに書いた。

**2. `ReadData` のログに `utType` を出さない**

`IosClipboardManager` は `utType` をログに出しているが、計画書 5.6.11 のログ許可リスト（`itemCount` / `representationCount` / `totalBytes` / `hasScope` / `scopeKind` / `hasCallback` / `operation` / `errorCode`）に `utType` は含まれない。uniform type identifier は利用者データではないが、**リストは列挙形式で書かれており、そこにない項目を足すのは黙った逸脱になる。** `hasUtType` に留めた。リストを広げるなら計画書側で決める。

**3. ネイティブ側の未検証事項を XML コメントへ引き写した**

計画書 10 章の DoD 項目。1.6 から段 2 の公開メンバに関わるものを 4 件移した。

- `Copy`: `LocalOnly` の Universal Clipboard への効果が実機未検証であること
- `Copy`: **10 MiB 超の単一 item は lazy data provider 経路に入り、Copy 成功が貼り付け可能を意味しない**こと（複数 item に分ければ回避できることも併記）
- `Read`: 読み出しは書き込みの鏡ではないこと（型が派生する）
- `Read` / `ReadData`: どの読み出しも「ユーザーに通知されない保証は無い」こと

## 2. 変更ファイル

### 2.1 新規作成（Runtime）— 1 ファイル / 1,294 行

| ファイル | 行 |
| --- | ---: |
| `Runtime/Clipboard/MacClipboardManager.cs` | 1,294 |

### 2.2 新規作成（Tests）— 2 ファイル / 1,003 行

| ファイル | 行 | 件数 |
| --- | ---: | ---: |
| `Tests/Runtime/MacClipboardManagerDispatchTests.cs`（EditMode・純粋関数のみ） | 215 | 15 |
| `Tests/PlayMode/MacClipboardManagerIntegrationTests.cs` | 788 | 36 |

### 2.3 既存変更

**なし。** `git status` 上、パッケージ内の既存ファイルは 1 件も変更されていない。iOS / Android / Share / Notification には一切触れていない。

### 2.4 作業前から存在した未コミット変更（本実装とは無関係）

1.2.0 xcframework の削除、1.3.0 xcframework の未追跡ファイル。**一切触れていない。**

## 3. エラー契約反映

段 2 で初めてエラーが**返却される**。6.1 の 8 コードのうち、段 2 の 5 操作に到達しうるものはすべて実装・検証した。

| code | 条件 | 実装 | PlayMode 検証 |
| --- | --- | --- | --- |
| 9001 `Busy` | 同一操作が pending | 段階 6 | あり |
| 9002 `BridgeUnavailable` | macOS Standalone Player 以外 | 段階 4 | あり（5 操作すべて） |
| 9002 `BridgeUnavailable` | 段階 5 / 7 の例外 | 段階 5 は try/catch、段階 7 は `InvokeNative` | 段階 5 はあり。段階 7 は Editor で P/Invoke が消えるため駆動不能 |
| 9003 `MainThreadRequired` | 別スレッドからの呼び出し | 段階 1 | あり |
| 9004 `ManagerDestroyed` | `OnDestroy` 後 | 段階 2 | あり |
| 9005 `InvalidRequest` | `content` / `ownership` が null | 段階 3 | あり |
| 9006 `ResponseParseFailed` | 成功応答を解析できない | 各 `HandleXxxCallback` | あり |
| 9007 `RequestTooLarge` | 送信 payload が上限超過 | 段階 3 | あり |

- 9001 の observation 専用文言（`Another observation control call is already in progress.`）は段 3。段 2 に共有キーを使う操作が無い
- ネイティブのエラーコード（1501 など）はそのまま透過することを PlayMode で検証した（`NativeFailure_IsReportedWithTheNativeCode`）

## 4. ビルド結果

| 項目 | 結果 |
| --- | --- |
| コンパイル | **成功**（`error CS` 0 件） |
| Unity | 6000.4.2f1 |

`warning CS0618`（`FindObjectsByType` の `FindObjectsSortMode` 引数）が PlayMode テストで 2 件出るが、**既存 `IosClipboardManagerIntegrationTests.cs` と同一の記述**であり、周囲のコードに合わせた結果である。

## 5. テスト結果

| プラットフォーム | 総数 | 成功 | 失敗 | 判定 |
| --- | ---: | ---: | ---: | --- |
| EditMode | **517** | 517 | 0 | **Passed** |
| PlayMode | **80** | 80 | 0 | **Passed** |

段 2 開始前は EditMode 502 / PlayMode 44。**51 件を追加**（EditMode 15 / PlayMode 36）し、**既存 546 件も全て pass**。

### 5.1 テストが空回りしていないことの確認（変異テスト 3 件）

「全部 pass した」だけでは、テストが実際に不具合を捕まえるかは分からない。実装に既知の欠陥を注入して、**狙ったテストが落ちること**を確認した。

| # | 注入した欠陥 | 結果 |
| --- | --- | --- |
| A | `TakeOwnershipCallback` が operation を無視して常に `s_onCopy` を返す（計画書 5.6.4 が名指しで警告している誤り） | **2 件が失敗**（`AppendCompletion_InvokesOnlyTheAppendCallback` / `OwnershipResult_CarriesTheOperationThatProducedIt`）。検出できている |
| B | `FireReadResult` で `EndOperation` を `Dispatch` の**後**に移動 | **0 件が失敗。下記参照** |
| C | `FireReadResult` から `EndOperation` を削除 | **2 件が失敗**（`CallbackMayRestartTheSameOperation` / `UnparsableSuccessPayload_...`）。検出できている |

**B が示したこと（計画書 7.2 の 1 項目は原理的に検証できない）**

計画書 7.2 は「**`EndOperation` が dispatch より前に走ること**」を検証項目に挙げているが、**この順序は外から観測できない。** `Dispatch` は結果を `UnityMainThreadDispatcher` に enqueue するだけで、購読者のコードは次の `Update` まで動かない。したがって `EndOperation` が `Dispatch` の前でも後でも、購読者が動く時点ではどちらもマーカーは解放済みになる。

意味があるのは順序ではなく**解放そのもの**で、それは C が示すとおり検証できている。実装は計画書どおり「退避 → `EndOperation` → `Dispatch`」の順に書いてあるが、テストで守れるのは「解放されること」までである。テスト側のコメントにその旨を書いた。

### 5.2 実装中に検出した自分のバグ

**0 件。** 段 1 で Builder / Parser / 結果型が固まっていたため、段 2 は初回のコンパイル・テストとも通った。

### 5.3 未実施ケース

| ケース | 理由 |
| --- | --- |
| 段階 7（P/Invoke 例外）の 9002 | Editor では `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` により P/Invoke がコンパイルされず、例外を起こす手段が無い。実機確認（7.5）で担保する |
| ネイティブ実機動作 | 段 3 完了後に 7.5 の手動確認 32 項目でまとめて実施する |
| 監視まわり全般 | 段 3 |

## 6. Definition of Done（計画書 10 章）

| 条件 | 判定 |
| --- | --- |
| 対象範囲の Runtime ファイルが作成され、コンパイルエラーが無い | 満たす（1 ファイル） |
| `public` メンバに英語の XML コメント | 満たす |
| ログが shape / count / flag のみ、本文と pasteboard 名を出していない | 満たす（1.2 の判断 2 を含む） |
| 逸脱の宣言が既存の使い分けに従っている | 満たす（Manager はクラス `<summary>` 内の `<para>`。ログ内容とログ位置の 2 件を宣言） |
| 対象範囲の新規テストが pass | 満たす（51 件） |
| **既存テストが全件 pass** | **満たす（EditMode 517 / PlayMode 80、失敗 0）** |
| EditMode テストが Manager インスタンスを生成していない | 満たす（`MacClipboardManagerDispatchTests` は純粋関数のみ） |
| PlayMode テストが `[TearDown]` で `ResetForTests()` を呼ぶ | 満たす |
| `.meta` を新規作成していない | 満たす（Unity が自動生成） |
| ネイティブ側が未検証と明記している事項を XML コメントに引き写す | 満たす（1.2 の判断 3、4 件） |
| single-flight の公開トレードオフをクラス XML コメントに書く | 満たす（`<b>Concurrency.</b>` の `<para>`） |
| **新規ファイルに OS 接頭辞**（`common.md`） | 満たす（Runtime 1 / Tests 2 すべて `Mac` 接頭辞） |
| **他プラットフォームのファイルを変更していない** | 満たす（2.3） |
| 要検証事項の更新 | 段 2 で実測できた要検証事項は無い。V-3（サイズ上限の実測）は実機作業のため段 3 以降 |
| （段 3 のみ）5.6.8 の teardown 全体実装と `stop` の差し替え | **未実施。段 3 の条件** |

## 7. 次のステップ

**段 3 の着手前に、監視部分に絞った再レビューを行う。** これは計画書 12 章の指示であり、段 1 → 段 2 の間のレビューとは別である。

対象は 5.6.5（active / pending 分離）、5.6.8 の監視部分（teardown 競合と D-16 の再発行規則）、5.6.10 の監視シーム、7.2 の `(4)` 項目。v5 → v6 → v7 と 3 ラウンド連続で監視領域から A1 が出ており、**v8 の D-16 と v10 の段の切り直しはまだレビューを通っていない**。

段 3 で差し替える必要があるもの（実装時に見落とさないための一覧）:

| 箇所 | 現在 | 段 3 |
| --- | --- | --- |
| `OnDestroy` の `stop` | 空実装 | `StopObservingForTeardown` |
| `InvokeNative` | `onNativeFailureResult` 必須・`inFlightKey` / `onNativeFailure` 無し | 計画書 5.6.4 のシグネチャへ戻す |
| `ClipboardCallback` / `ClipboardChangeCallback` delegate | 未宣言 | 宣言する |
| per-call スロット | 5 本 | 17 本（`ClearAllPendingCallbacks` / `HasAnyPendingCallbackForTests` も同時に更新） |
| 公開イベント | 4 本 | 13 本 |
