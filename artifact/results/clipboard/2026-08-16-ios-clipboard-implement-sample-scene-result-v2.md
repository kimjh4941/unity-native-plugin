# iOS Clipboard サンプルシーン実装結果 v2

## 基本情報

- 日付: 2026-08-16
- 機能名: clipboard
- 対象プラットフォーム: iOS
- 計画ファイル: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v5.md`（承認済み）
- 前版: `artifact/results/clipboard/2026-08-16-ios-clipboard-implement-sample-scene-result-v1.md`
- 対象レビュー: `artifact/reviews/clipboard/2026-08-16-ios-clipboard-implement-sample-scene-review-v1.md`（総合評価「要修正（軽微）」）
- 実装対象ブランチ: `feature/UNT-9`
- 出力言語: 日本語（**コード内の文言・コメントはすべて英語**）

---

## 0. レビュー v1 への対応

| 指摘 | severity | 対応 | 対象 |
|---|---|---|---|
| observation 発行直後に status / enabled が更新されない | medium | **修正**。`RefreshObservationUI()` を新設し、`IssueStartObserving` / `IssueStopObserving` の発行直後に呼ぶ。`BeginStart` / `BeginStop` で pending になった時点で `Observing: starting` / `on (pending)` を表示し、Scope 6 ボタン・Start・Restart・Stop・Busy Start・missing-scope error を無効化する | `IosClipboardManagerExampleController.cs` |
| `ex.Message` 経由で一時ファイルパスがログへ出る可能性 | medium | **修正**。`IosClipboardSampleResult.DescribeException(Exception)` を新設し、**例外の型名のみ**をログする。`ConsumeLoadedFile` / `TryDeleteRequestDirectory` / `OnCopyImageFileClicked` の 3 箇所に適用。パス文字列を含む例外を渡してもパスが出ないことを EditMode で固定 | `IosClipboardSampleResult.cs`、`IosClipboardManagerExampleController.cs`、`IosClipboardSampleStateTests.cs` |
| `.slnx` 差分が残り BuildProcessors が外れている | medium | **修正**。`git checkout unity-native-plugin.slnx` で復元し、**全テスト実行後**に `git status` / `git diff` で差分ゼロを再確認した（v1 では復元後に再度 Unity が走って再生成されていた） | `unity-native-plugin.slnx` |
| missing-scope の handler → 発行 helper の受け渡しが未固定 | low | **修正**。計画 6.8.2 どおり `(marker, owner, targetScope)` を表す純粋 seam を切り出した。`IosClipboardSampleStartRequest` と `IosClipboardSampleObservationRequests`（`Start` / `Restart` / `BusyPair` / `MissingNamed`）を追加し、handler は request を組み立てて渡すだけにした。`MissingNamed` が active scope を参照せず毎回新しい `Named` を返すこと、`BusyPair` の 2 本目のみ非所有であることをテストで固定 | `IosClipboardSampleObservationState.cs`、`IosClipboardManagerExampleController.cs`、`IosClipboardSampleStateTests.cs` |
| `ShowIosClipboard` に XML コメントがない | low | **修正**。`<summary>` と `<param>` を追加 | `NativeToolkitSampleNavigator.cs` |

### 0.1 発行時 UI 更新の設計メモ

`RefreshObservationUI()` は現在の状態から status と enabled を**再計算**するだけなので、Editor のように callback が同期到着する場合（発行 → 即完了 → その後に発行直後の更新が走る）でも最終状態と一致する。順序に依存しない。

Busy デモの 2 本目は非所有だが、1 本目が作った pending 状態をそのまま再計算して表示する（レビュー提案どおり）。

---

## 1. 変更ファイル

### 1.1 新規作成（7 ファイル）

`.meta` は Unity が自動生成したもので、本実装では作成していない。

| パス | 行数 | 内容 |
|---|---|---|
| `Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs` | 1,517 | Controller。57 ボタン、10 イベント購読、per-call 結果対応付け、observation lifecycle |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleResult.cs` | 136 | `IosClipboardSampleResultContext`、結果／status 整形、`DescribeException` |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleObservationState.cs` | 186 | owner token 付き状態機械、`IosClipboardSampleStartRequest`、`IosClipboardSampleObservationRequests` |
| `Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml` | 130 | 画面定義（11 セクション） |
| `Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExampleStyle.uss` | 152 | スタイル（`ios-clipboard-*` / `ios-secondary-button`） |
| `Tests/Runtime/IosClipboardSampleSceneWiringTests.cs` | 208 | UXML / Controller の name 不一致検出（7 テスト） |
| `Tests/Runtime/IosClipboardSampleStateTests.cs` | 465 | 結果 context・observation 状態・発行 request・ログ秘匿（28 テスト） |

パスはすべて `Packages/com.jonghyunkim.nativetoolkit/` 配下。

### 1.2 既存変更（2 ファイル）

| パス | 変更内容 |
|---|---|
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowIosClipboard`（XML コメント付き）を追加、`RemoveExistingControllers` に Controller を登録 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | 配線ガードを iOS へ拡張、`#elif UNITY_IOS` 分岐、Editor を 2 択ダイアログ（`Open Sample Screen` / `Close`）にして `ShowIosClipboard` へ遷移 |

### 1.3 変更していないもの

- `Runtime/Clipboard/Ios*.cs`、Android サンプル一式、`TopMenuExample.uxml`、`NativeToolkitExampleScene.unity`
- `unity-native-plugin.slnx`: Unity 実行のたびに並び順が再生成されるため、**最終テスト実行後**に復元し差分ゼロを確認済み

---

## 2. 実装したサンプル機能

### 2.1 セクションとボタン（機能 56 + Home = 57）

| セクション | 数 | 内容 |
|---|---|---|
| Scope | 6 | General / Named 作成 / Named 選択のみ / Unique 作成 / active 削除 / 削除済み scope への Read |
| Copy | 11 | PlainText / 空 / HTML / URL / ImageFile / ImageData / Color / CustomData(`public.data`) / MultipleText / MultiRepresentation / 検出 fixture |
| Copy Options | 4 | localOnly true / false / device baseline / 30 秒失効 |
| Append | 2 | PlainText（毎回異なる 24 文字マーカー） / URL |
| Read | 4 | Read / ReadData(`public.png`) / Snapshot / Snapshot(matching) |
| Load | 6 | Text / URL / Image / File(`public.data`) / File(custom UTI) / CancelLoads |
| Detect | 4 | number fixture / search fixture / DetectPatterns / DetectValues |
| Observe | 4 | Start / Restart（Stop を挟まない置換） / Stop / CheckForegroundChange |
| Clear | 1 | active scope の Clear |
| Busy / Memory | 4 | LoadItem 二重 / seed+cancel / StartObserving 二重 / 約 4 MiB の ImageData |
| Errors | 10 | 計画 4.3 の 10 コード |
| Home | 1 | TopMenu へ戻る |

計画 v5 の見出しは「55」だが 4.3 のセクション表の合計は 56。表を正とした（v1 から変更なし）。

### 2.2 結果の対応付け

各呼び出しが `BeginResult(marker)` で `{Sequence, Marker}` を発番し、その呼び出しの per-call callback が capture して表示する。共通イベント 10 個は shape-only ログのみで UI・scope に触れない。例外は `ClipboardChanged` で、`onChanged: null` としているため `Events` カウントと status 更新の唯一の発生源になる。`CreatePasteboard` / `RemovePasteboard` は開始時 scope を capture し、`ReferenceEquals` で所有権を確認してから active を差し替える。

### 2.3 observation の owner token と発行 request

- `BeginStart()` / `BeginStop()` が owner を発行し、pending 中は `NonOwningToken` を返す
- `CompleteStart` / `CompleteStop` は owner 一致時のみ状態を変更する
- **発行 request の生成は純粋 helper に集約**（v2）。Start / Restart は active scope、Busy は 1 本目のみ所有で 2 本とも同じ scope、missing-scope error は毎回新しい `Named` を対象にする
- owned な Start failure は未観測へ（native が scope 解決前に `stopObservingInternal()` を実行するため）
- deferred Stop は `owned && isStart && isSuccess && StopRequestedAfterStart && ShouldIssueStopNow()` のときだけ 1 回発行し、再試行しない
- **発行直後に status / enabled を再計算**（v2）

### 2.4 画面破棄後 callback

「ログ → 画面外でも必須の処理 → `IsScreenAlive()` → UI」の順に統一。画面外でも実行するのは、observation の状態遷移・`_observedScope` 更新・deferred Stop 発行・`LoadItem(File)` の size 取得と request directory 削除。seed Copy 完了後の Load + Cancel は画面外では開始しない。`BeginResult` も screen-aware。

### 2.5 表示・ログ規約

- 本文 / base64 / 検出値 / pasteboard 名 / 一時ファイルパスは表示にもログにも出さない
- **file API の例外は型名のみログする**（v2）。`FileNotFoundException` などの message はパスを含むため
- `Read` は `items` / `firstItemTypes` / `textLen`、`DetectValues` は件数のみ、scope は `named(len=N)`
- `LoadItem(File)` は `fileSize=<n or -1> cleanup=<ok|failed>` の全経路を表示

---

## 3. 共通実装パターン: 維持と拡張

### 3.1 維持

TopMenu 導線 / ヘッダー構造 / セクション単位のボタン群 / `Start` での `UIDocument` 解決 / `OnEnable`・`OnDisable` の購読管理 / `OnDestroy` の `clicked` 解除 / 全メソッド先頭の `Debug.Log` / 結果 ScrollView のオフセットリセット / `ios-*` クラス命名。

### 3.2 拡張（iOS clipboard 固有）

per-call 結果 context（`_pendingOperationTitle` 方式を採用しない）/ status 行 / observation 連動の enabled 制御と**発行時の即時反映** / 純粋 helper への分離（結果整形・状態遷移・発行 request・例外整形）/ ハンドラ内のプラットフォームガード無し / `SetResult` はログしない。

---

## 4. ビルド / 実行結果

| 項目 | 結果 |
|---|---|
| コンパイル | エラー 0、新規ファイル起因の警告なし |
| EditMode テスト | **391 / 391 passed**（v1 の 387 から +4）、failed 0 |
| PlayMode テスト | 55 件中 **44 passed / 0 failed / 11 skipped** |
| アクティブビルドターゲット | Android のまま（変更なし） |
| `unity-native-plugin.slnx` | **テスト実行後に復元し、差分ゼロを確認** |

- PlayMode の skip 11 件は iOS 専用 fixture が Android ターゲットで除外される既知の挙動。本変更による回帰ではない
- 実行コマンド: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode|PlayMode -buildTarget Android -testResults <xml> -logFile <log>`
- テスト実行には Unity Editor を閉じる必要があり、ユーザーに閉じてもらったうえで実行した

### 4.1 テスト内訳（35 件）

**wiring（7）**: Resources / AssetDatabase パス、必須ボタン 57 個、必須ラベル 4 個、`ResultScrollView` 内の `ResultTextBlock`、TopMenu の `ClipboardFeatureButton`。

**state（28）**:

- 結果整形 8 件（OK / NG / `--` / details / running / 完了順の入れ替え / file cleanup 4 経路 / scope ラベル / status / observing 表記）
- observation 状態 11 件（owner 競合、stale owner、Start pending 中の離脱、deferred Stop が 1 回で Stop 失敗後も再発行しない、Start failure で Stop を発行しない、Restart 成功／失敗、Stop 失敗、重複 Stop 要求、enabled 契約の全状態）
- **発行 request 4 件（v2 追加）**: Start が active scope と所有権を取ること、Restart が scope を capture し直すこと、BusyPair の 2 本目のみ非所有で同一 scope であること、MissingNamed が active scope を使わず毎回新しい `Named` を返すこと
- **ログ秘匿 1 件（v2 追加）**: パスを含む例外を渡しても型名だけが返り、`/` を含まないこと

---

## 5. 手動確認観点

### 5.1 Editor（未実施 — 理由付き）

batchmode ではボタン操作ができないため S-1〜S-6 は**未実施**。UXML / Controller の name 一致と Resources 解決は wiring テストで担保済み。v2 で追加した「発行直後の enabled / status 反映」は状態計算をテストで固定しているが、**実際の画面表示は S-1〜S-6 で確認が必要**。

| # | 確認 | 状態 |
|---|---|---|
| S-1 | iOS ターゲットで TopMenu に Clipboard ボタンが出る | 未実施（GUI 操作が必要） |
| S-2 | Clipboard → `Open Sample Screen` で画面遷移 | 未実施（同上） |
| S-3 | Back To Home | 未実施（同上） |
| S-4 | 任意操作が `CLIPBOARD_BRIDGE_UNAVAILABLE` を返す | 未実施（同上） |
| S-5 | 画面往復でイベントが二重購読されない | 未実施（同上） |
| S-6 | Start Observing → 離脱で例外が出ず未観測へ戻る | 未実施（同上） |

### 5.2 実機（未実施 — 実機が必要）

計画 7.2 の S-10〜S-32（M-1〜M-24 対応）はすべて**未実施**。特に S-13a〜c（localOnly を `textLen` で判別）、S-22 / S-22b（`fileSize=64 cleanup=ok`、独自 UTI）、S-25a〜d（busy / teardown / Restart / Start failure）、S-31（約 4 MiB のメモリ計測）は実機でのみ判定できる。S-25b は `marker: observe.stop.teardown` の成功ログを同期点にする（計画 7.2.1）。

### 5.3 要検証（計画 5.4 の V-1〜V-6）

| # | 状態 |
|---|---|
| V-1 | シーン未変更で到達できること — 未確認（Editor GUI）。Navigator 登録と Resources 解決は自動テスト済み |
| V-2 | `LoadItem(File)` の親ディレクトリ削除の妥当性 — 未確認（実機） |
| V-3 | 範囲外 color が `CLIPBOARD_INVALID_COLOR` になること — 未確認（実機） |
| V-4 | 4 MiB PNG の生成コストと 3〜5 MiB への収まり — 未確認（実機）。範囲外なら計測せず `fixture=out-of-range` を表示する実装は入っている |
| V-5 | Start pending 中の `CreatePasteboard` 完了で active と observing が食い違う表示 — 未確認（実機） |
| V-6 | `MultiRepresentation` の独自 UTI を `LoadItem(File(custom))` で取得できること — 未確認（実機） |

### 5.4 M-23（本サンプル対象外）

計画 7.4 のとおり、native → `DllImport` → parser → decoded `byte[]` を通す実機計測は別 artifact へ引き継ぐ。

---

## 6. 計画からの差異（実装時の判断）

| # | 差異 | 理由 |
|---|---|---|
| 1 | ボタン数 55 → 56（+ Home で 57） | 計画 4.3 のセクション表の合計が 56。見出しの数値が計算違い |
| 2 | `IosClipboardSampleResultContext` を `IosClipboardSampleResult.cs` に、`IosClipboardSampleStartRequest` / `...Requests` を `IosClipboardSampleObservationState.cs` に同居 | 計画の新規 7 ファイル構成を維持しつつ、関心事の近いものをまとめた |
| 3 | 前提エラー表示を `-- local=<reason>` 形式に統一 | `ProbeRemovedScope` に加え fixture 生成失敗・範囲外にも適用し、native 由来の `NG` と区別する |
| 4 | observation control の結果に `owned=<bool>` を表示 | S-25a で状態所有者を画面から判別するため |
| 5 | `Load URL` の payload に `urlLen` を追加 | 計画 4.9 は Text / ImageData / File のみ規定。本文を出さずに成否を確認するため |
| 6 | `LoadItem(File)` の `Path` が null の経路を追加 | `fileSize=-1 cleanup=failed` として表示 |
| 7 | 例外ログを型名のみに統一（v2） | 計画 3.3 の「パスは表示・ログしない」を message 経由の漏洩まで含めて満たすため |

いずれも設計の構造・契約を変更するものではない。

---

## 7. 実行確認

- 提示文:
  - 「このサンプル実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して終了 → review-implementation-sample-scene スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま終了
- ユーザー回答:
  - **実行する**（2026-08-16）。再レビュー v2 は LGTM、新規指摘なし
  - 基準ファイル: 本ファイル（v2）
  - 残作業: Editor GUI 確認（S-1〜S-6）と iOS 実機確認（S-10〜S-32 / V-1〜V-6）は後続検証として継続。M-23 は別 artifact
