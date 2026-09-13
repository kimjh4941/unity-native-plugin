# 実装結果レポート v5（レビュー v2 の指摘対応）

## 基本情報

- 日付: 2026-09-07
- 機能名: clipboard
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-11
- 実装計画: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v8.md`
- 対応したレビュー: `artifact/reviews/clipboard/2026-09-07-windows-clipboard-implementation-feature-review-v2.md`
- 前回の実装結果: `artifact/results/clipboard/2026-09-07-windows-clipboard-implementation-feature-result-v4.md`（ステップ 1〜7 通し）

## 0. 状態サマリー

| 検証 | v4 時点 | 本対応後 |
|---|---|---|
| EditMode | 712 / 712 | **745 / 745 passed** |
| PlayMode | 140 / 140 | **163 / 163 passed** |
| コンパイルエラー / 本実装由来の警告 | 0 / 0 | **0 / 0** |
| 機械照合 `check_design_consistency.py` | FAIL 0 | **FAIL 0** |

| レビュー v2 の区分 | 指摘数 | 解消 | 対応せず（理由つき） |
|---|---|---|---|
| A（振る舞いを変える） | 15 | **14** | 1（A-14） |
| B（検証手段を変える） | 21 | **21** | 0 |
| C（記述だけを変える） | 15 | **15** | 0 |

## 1. 実行コマンド

```
Unity.exe -batchmode -nographics -projectPath . -runTests \
  -testPlatform EditMode|PlayMode -buildTarget Win64 -testResults <xml> -logFile <log>
```

`-runTests` と `-quit` は併用不可（既知。併用するとテスト完了前に終了し XML が出ない）。

## 2. 修正の構成

レビュー v2 が「A 区分 15 種のうち過半はひとつの原因に帰着する」と判定したため、
**個別対応ではなく仕組みから直す**方針を採り、4 スライスに分けた。

| スライス | コミット | 内容 |
|---|---|---|
| 1 | `dad06ad` | shutdown の試行と終端処理を不可分にする + ガードを純関数へ + シーム 4 点 |
| 2 | `7531c9a` | キャンセルと配送（レジストリ所有権 / デッドロック / 例外経路） |
| 3 | `47abc39` | 空文字列の扱い + 読み出しプロトコルをガードの外へ |
| 4 | `5dcfe1b` | payload と配送の残穴 + 未検証だった 4 契約 |
| 5 | 本コミット | 記述の不一致（C 区分）と読み取り専用化 |

## 3. サーキットブレーカーへの対応

レビュー v2 は「shutdown 系 A1 級指摘が 4 ラウンド続いた」としてサーキットブレーカーに該当と判定した。
個別修正ではなく、それを生む 2 つの仕組みを直した。

### 仕組み 1: 集約したのは「起点」であって「試行」ではなかった

設計 7.4 は「1 回の試行が終わるたびに必ず終端処理を通す」と書いていたが、
実装は起点（`TryShutdown` / `OnDestroy` / `Drain` / `Quit`）だけを集約し、
`DrainRoutine` の**ループ内の中間試行**が集約の外に残っていた。

**対応**: `RunShutdownAttempt` がネイティブ試行と終端処理の両方を行い、
`InvokeNativeShutdown` は private でそこからしか呼べない。
「試行したら必ず終端処理が走る」を呼び出し側の規律ではなく構造で保証する。

これで A-1 / A-3 / A-8 / A-9 が同時に解消した。

### 仕組み 2: テストシームが状態遷移そのものを飛ばしていた

`SetStateForTests` で `Running` を直接作れたため、
`Initialize` / `TryShutdown` / `ShutdownWithDrain` / `CancelRequest` /
`ReserveDeferredFormats` / `RecoverDeferredState` の 6 つが一度も呼ばれていなかった。

**対応**: シームを 5 点追加・修正した。

| シーム | 何を可能にしたか |
|---|---|
| `NativeShutdownForTests` | リトライ / 予算超過 / `PartialState` 回復。Editor は初回で完了するため、これ無しでは到達不能 |
| `PlatformAvailableForTests` | ガードの入力だけを差し替える。ネイティブ境界はコンパイルアウトのまま |
| `SetMainThreadIdForTests` | スレッドガード。実際に別スレッドを作らずに検証できる |
| `InvokeWantsToQuitForTests` + origin 付き `InjectShutdownResultForTests` | quit 再開経路。従来は origin 固定で構造的に到達不能だった |
| `ReadRawForTests` / `ReadTextForTests` | 二段階読み出しプロトコル |
| `NextNativeRequestIdForTests`（改名・0 許容） | 受付後の並行 2 リクエストと、受付拒否の防御分岐 |

PlayMode の `[TearDown]` が `OnDestroy` の shutdown 分岐を能動的に潰していた点も外した。

## 4. A 区分の対応一覧

| # | 内容 | 対応 |
|---|---|---|
| A-1 | ドレイン中の中間試行が終端関数を通らず `Draining` に入らない | 仕組み 1 |
| A-2 | `PartialState` で `recoverDeferredState` を試さず、回復手段も到達不能 | ドレインが 1 回だけ回復を試みる |
| A-3 | `OnDestroy` が特定状態でしかドレインしない | 仕組み 1（状態ゲートを撤去、ドレインを無条件化） |
| A-4 | `TryClaim` が native id を所有者確認なしに削除 | 所有者一致時のみ削除 |
| A-5 | `CancellationTokenRegistration.Dispose()` が dispatcher ロック内 | 破棄をスレッドプールへ委譲 |
| A-6 | キャンセル済みトークンでもネイティブに到達 | 5 本の `*Async` で事前に `Canceled` 配送 |
| A-7 | `StartRequest` の例外フィルタが狭く in-flight がリーク | 全例外を捕捉して配送 |
| A-8 | `Initialize` が tombstone より状態を先に見る | 順序を `CanRunOperation` と揃えた |
| A-9 | quit がコルーチン起動失敗から復帰できない | 起動失敗時は同期試行 1 回のうえ終了を通す |
| A-10 | `CanRunOperation` にプラットフォーム判定が無い | 純関数 `ClassifyOperationGuard` に集約し順序を明示 |
| A-11 | 空文字列が全テキスト API で `IsEmpty` になる | `GetPreferredFormat` に限定 |
| A-12 | dispatcher が null のとき呼び出し元スタックで配送 | teardown ドレインに委ねる |
| A-13 | `OnRenderFormatNative` に try/catch と `out` 設定が無い | 追加（**テスト不能**、5 節） |
| A-14 | イベント購読者どうしが隔離されていない | **対応せず**（5 節） |
| A-15 | `Initialize` の例外フィルタが狭く COM が漏れる | 全例外を捕捉（**テスト不能**、5 節） |

## 5. 対応しなかった / 検証できなかったもの

| # | 内容 | 理由 |
|---|---|---|
| A-14 | イベント購読者の隔離（`GetInvocationList` 未使用） | Android / iOS / macOS の既存 Manager と共通の慣行であり、Windows 固有の退行ではない。ここで直すと 4 プラットフォームに手が入り P2 に触れる。**横断課題として別途起票が必要** |
| A-13 | render コールバックの try/catch | `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` の内側にあり、Editor から到達できない。**ミューテーションで実証していない**。目視と実機確認 M-19 / M-20 で担保する |
| A-15 | `Initialize` の例外フィルタ | 同上。ネイティブ初期化ブロック全体が Editor ではコンパイルされない |
| B-13 | `s_historyEventsEnabled` | 本番の分岐条件として読まれない死にフィールドだったため**削除**した。監視状態を問い合わせる公開 API は設計にないため、追加はしていない |

## 6. ミューテーション検証

23 変異を 7 バッチで適用した。**3 件が最初は検出されず、いずれも実際の穴を示していた。**

| バッチ | 変異 | 結果 |
|---|---|---|
| A | ドレイン条件 / `Initialize` 順序 / プラットフォーム判定 | 8 テストで検出 |
| B | quit 再開 / `OnDestroy` ゲート / 予算超過の状態 | 2 検出、**1 未検出** |
| C | 予算超過の状態 / `PartialState` 回復 | 2 検出（B の未検出分を解消） |
| D | native id 所有権 / キャンセル済み / 配送 / dispatcher ホップ / 登録解放 | 4 検出、**1 未検出** |
| E | 登録解放 / 例外フィルタ | 2 検出（D の未検出分を解消） |
| F | 空文字列の扱い / render キャッシュ | 2 検出 |
| G | サロゲート / キー照合 / サイズ / 受付拒否 / TryShutdown / 履歴イベント / 予約キャッシュ / チケット | 8 検出 |

### 未検出だった 3 件が示していたもの

| 変異 | 生存の原因 | 対応 |
|---|---|---|
| B3: 予算超過で `ShutdownFailed` にしない | Editor は初回試行で完了するため、リトライもタイムアウトも到達不能 | `NativeShutdownForTests` を追加。A-2 の検証もこれで可能になった |
| D5: キャンセル登録を解放しない | **私が書いたテストが死んでいた**。配送で `s_pending` からエントリが消えるため、`HasCancellationRegistrationForTests` は解放の有無に関わらず false を返す | 実際の `Dispose` を数えるシームに変更 |
| G2 の一部: キー照合を部分一致に戻す | **私が書いたテストの入力に判別力が無かった**。`{"note":"historyEnabled roamingEnabled"}` は修正前のチェックでも弾かれる | 両方のキー名を値として完全に引用符で囲む入力へ変更 |

### v4 の記録の訂正

v4 §4 で「11 変異すべて検出 = テストが有効である証拠」と記録したが、
これは**変異を注入した箇所についてのみ正しい**。
今回のレビューは、変異を注入しなかった領域（ライフサイクル遷移 / quit / キャンセル /
メインスレッドガード）に穴が集中していることを示した。

**自分で選んだ箇所に自分で変異を入れる限り、変異の選定が自分の盲点を継承する。**
今回さらに、上記のとおり**自分の書いたテストが 2 件死んでいた**ことをミューテーションが見つけている。
ミューテーション検証は「注入した箇所のテストが生きているか」しか答えない。

## 7. v4 の記述の訂正

| 箇所 | 誤り | 正 |
|---|---|---|
| v4 §3 | 「Editor では `PlatformUnavailable` が返る」 | `Initialize` とネイティブ境界の `#else` 枝については正しいが、**事前チェック経路では `NotInitializedByHost` が返っていた**（A-10）。本対応で設計どおり `PlatformUnavailable` になった |
| v4 §5 #8 | 「名前空間直下の `internal enum` 5 種」 | Manager 内 6 種、`RequestTable` を含めて 7 種 |
| v4 §2.3 / §6 | 既存変更 1 件、ログ規約の逸脱注記あり | 既存変更は 2 件（`scripts/check_design_consistency.py`）。逸脱注記は Manager に無かったため追加した |

## 8. 設計書の訂正（C 区分）

| 節 | 内容 |
|---|---|
| 6.1 | 「原則 1 ファイル 1 主型、例外は 1 ファイルのみ」→ 実態（複数型を持つ 4 ファイル）に合わせた。`common.md` が課すのはファイル名と主型名の一致まで |
| 6.3 | 既存変更に `scripts/check_design_consistency.py` を追加 |
| 7.4 段階 3 | 「delegate の GC ルートを解放する」→ 解放しない。終端失敗のときネイティブはポインタを保持し続けるため、解放できる方が危険 |
| 7.11 の表 | `[MonoPInvokeCallback]` の実体と `static readonly` delegate は外側ガードにある。P5 の目的（他プラットフォームの Player ビルドに入らない）は外側ガードで達成済み |

## 9. 変更ファイル

Runtime 5（`WindowsClipboardManager` / `WindowsClipboardRequestTable` / `WindowsClipboardJsonBuilder` /
`WindowsClipboardJsonParser` / `WindowsClipboardHistoryItem`）、
テスト 5（`Runtime/` の Dispatch / RequestTable / JsonBuilder / JsonParser / Result、`PlayMode/` の Integration）。
**新規ファイルなし。他プラットフォームのファイル変更 0 件。**

## 10. 残作業

| # | 内容 | 備考 |
|---|---|---|
| 1 | 実機確認 M-1 〜 M-24 | A-13 / A-15 の担保を含む。Player ビルドが必要 |
| 2 | `agent-rules/coding-rules/testing.md` 適用状況表の更新 | Windows Clipboard を層 1・2a に追加 |
| 3 | A-14（イベント購読者の隔離）の横断課題起票 | 4 プラットフォーム共通 |
| 4 | 再レビュー | 止める基準は「A が 0」。本対応で A は 14/15 解消、残り 1 は理由つきで見送り |
| 5 | サンプルシーン | `design-sample-scene` の対象（本計画のスコープ外） |
