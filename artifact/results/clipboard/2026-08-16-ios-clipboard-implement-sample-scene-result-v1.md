# iOS Clipboard サンプルシーン実装結果 v1

## 基本情報

- 日付: 2026-08-16
- 機能名: clipboard
- 対象プラットフォーム: iOS
- 計画ファイル: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v5.md`（レビュー v5 で LGTM、承認済み）
- 実装対象ブランチ: `feature/UNT-9`
- 出力言語: 日本語（**コード内の文言・コメントはすべて英語**）

---

## 1. 変更ファイル

### 1.1 新規作成（7 ファイル）

`.meta` は Unity が自動生成したもので、本実装では作成していない。

| パス | 行数 | 内容 |
|---|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs` | 1,508 | Controller。57 ボタン、10 イベント購読、per-call 結果対応付け、observation lifecycle |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardSampleResult.cs` | 125 | `IosClipboardSampleResultContext` と結果／status 整形の純粋関数 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardSampleObservationState.cs` | 118 | observation の owner token 付き状態機械（純粋） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml` | 130 | 画面定義（11 セクション） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExampleStyle.uss` | 152 | スタイル（`ios-clipboard-*` / `ios-secondary-button`） |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosClipboardSampleSceneWiringTests.cs` | 208 | UXML / Controller の name 不一致検出（7 テスト） |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosClipboardSampleStateTests.cs` | 393 | 結果 context と observation 状態の検証（24 テスト） |

### 1.2 既存変更（2 ファイル）

| パス | 変更内容 |
|---|---|
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowIosClipboard` を追加（`#if UNITY_IOS \|\| UNITY_EDITOR`）、`RemoveExistingControllers` に `IosClipboardManagerExampleController` を登録 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | ①配線ガードを `#if UNITY_ANDROID \|\| UNITY_IOS \|\| UNITY_EDITOR` へ拡張 ②`OnClipboardClicked` に `#elif UNITY_IOS` 分岐を追加 ③Editor 分岐を 2 択ダイアログ（`Open Sample Screen` / `Close`）にし、選択時に `ShowIosClipboard` へ遷移 |

### 1.3 変更していないもの

- `Runtime/Clipboard/Ios*.cs`（実装済みの公開 API。サンプルのための変更なし）
- Android サンプル一式、`Resources/UI/Top/TopMenuExample.uxml`、`Assets/Samples/.../NativeToolkitExampleScene.unity`
- `unity-native-plugin.slnx`: Unity のプロジェクト再生成で並び順が変わったため `git checkout` で復元済み（意図しない差分を残さない）

---

## 2. 実装したサンプル機能

### 2.1 セクションとボタン（計 57 ボタン）

| セクション | 数 | 内容 |
|---|---|---|
| Scope | 6 | General / Named 作成 / Named 選択のみ / Unique 作成 / active 削除 / 削除済み scope への Read |
| Copy | 11 | PlainText / 空 / HTML / URL / ImageFile / ImageData / Color / CustomData(`public.data`) / MultipleText / MultiRepresentation / 検出 fixture |
| Copy Options | 4 | localOnly true / false / device baseline / 30 秒失効 |
| Append | 2 | PlainText（毎回異なる 24 文字マーカー） / URL |
| Read | 4 | Read / ReadData(`public.png`) / Snapshot / Snapshot(matching) |
| Load | 6 | Text / URL / Image / File(`public.data`) / File(custom UTI) / CancelLoads |
| Detect | 4 | number fixture / search fixture / DetectPatterns / DetectValues |
| Observe | 4 | Start / **Restart（Stop を挟まない置換）** / Stop / CheckForegroundChange |
| Clear | 1 | active scope の Clear |
| Busy / Memory | 4 | LoadItem 二重 / seed+cancel / StartObserving 二重 / 約 4 MiB の ImageData |
| Errors | 10 | 計画 4.3 の 10 コード |
| Home | 1 | TopMenu へ戻る |

**ボタン数の差異（実装時の判断）**: 計画 v5 の見出しは「55」だが、4.3 の各セクション表を合計すると 56（+ `HomeButton` で 57）になる。セクション表が正であると判断し、**表どおり 56 個 + Home を実装**した。見出しの 55 は v2 からの加算時の計算違いであり、機能追加や削除ではない。

### 2.2 結果の対応付け（計画 6.2 / 6.4）

- 各呼び出しが `BeginResult(marker)` で `{Sequence, Marker}` を発番し、**その呼び出しの per-call callback が capture** して表示する
- 共通イベント 10 個は **shape-only ログのみ**。UI・scope 状態には触れない
- 例外は `ClipboardChanged`。呼び出しに紐づかない継続イベントなので、**`Events` カウントと status 更新の唯一の発生源**（`onChanged` には常に `null` を渡し二重計上を防止）
- `CreatePasteboard` / `RemovePasteboard` は開始時 scope を capture し、`ReferenceEquals(_activeScope, target)` のときだけ active を差し替える

### 2.3 observation の owner token（計画 6.5）

- `BeginStart()` / `BeginStop()` が owner を発行し、pending 中は `NonOwningToken`（= 0）を返す
- `CompleteStart(owner, …)` / `CompleteStop(owner, …)` は **owner 一致時のみ**状態を変更する
- Busy デモの 2 本目は `NonOwningToken` で明示的に発行（`BeginStart()` の戻り値と二重の防御）
- **owned な Start failure は未観測へ**（native の `startObserving` は scope 解決前に `stopObservingInternal()` を実行するため）
- deferred Stop は `owned && isStart && isSuccess && StopRequestedAfterStart && ShouldIssueStopNow()` のときだけ **1 回**発行。`CompleteStop` は成功・失敗いずれでも要求を消費し、**再試行しない**

### 2.4 画面破棄後 callback（計画 6.6）

すべての完了ハンドラを「ログ → 画面外でも必須の処理 → `IsScreenAlive()` → UI」の順に統一した。

| callback | 画面外でも実行する処理 |
|---|---|
| `StartObserving` / `Restart` | 状態遷移、`_observedScope` 更新、deferred Stop 発行 |
| `StopObserving` | 状態遷移、`_observedScope` 解除 |
| `LoadItem(File)` | file size 取得と **request directory 削除** |
| `SeedAndCancelLoad` の seed Copy | なし（画面外では後続の Load + Cancel を**開始しない**） |

`BeginResult` も screen-aware にしたため、deferred Stop が破棄済み UI に触れることはない。

### 2.5 表示・ログ規約

- 本文 / base64 / 検出値 / pasteboard 名 / **一時ファイルパス**は表示にもログにも出さない
- `Read` は `items` / `firstItemTypes` / `textLen`、`DetectValues` は件数のみ、scope は `named(len=N)` 形式
- `LoadItem(File)` は `fileSize=<n or -1> cleanup=<ok|failed>` の 4 経路すべてを表示

### 2.6 入力バリデーション / エラー表示

- 入力欄なし。値は固定 fixture（計画 4.5）
- C# 例外になる入力（空 pasteboard 名 / 非有限 color）はボタン化せず、subtitle と Errors note に確定文言を表示
- 画面側の前提チェックは `ProbeRemovedScopeButton` の 1 箇所のみ（`#n [scope.probeRemoved] -- local=no removed scope yet`）
- fixture 生成に失敗した場合（画像書き出し失敗、4 MiB PNG が 3〜5 MiB を外れた場合）も同じ `local=` 形式で表示し、Manager を呼ばない

---

## 3. 共通実装パターン: 維持と拡張

### 3.1 維持

TopMenu 導線 / ヘッダー（Back To Home・タイトル・subtitle・結果 ScrollView）/ セクション単位のボタン群 / `Start` での `UIDocument` 解決 / `OnEnable`・`OnDisable` での購読管理 / `OnDestroy` での `clicked` 解除 / 全メソッド先頭の `Debug.Log` / 結果 ScrollView のオフセットリセット / `ios-*` クラス命名。

### 3.2 拡張（iOS clipboard 固有）

| 拡張 | 内容 |
|---|---|
| per-call 結果 context | 異種操作が並行するため、`_pendingOperationTitle` 方式（Android / iOS Share）を採用しない |
| status 行 | `Scope: … (observing …) \| Observing: … \| Events: n` |
| enabled 制御 | Scope 6 ボタン / Start / Restart / Stop / 観測系エラーボタンを observation 状態に連動 |
| 純粋 helper の分離 | 結果整形と状態遷移を Controller から切り出し、EditMode で検証 |
| ハンドラ内のプラットフォームガード無し | `IosShareManagerExampleController` と同じ。Editor では Manager が `CLIPBOARD_BRIDGE_UNAVAILABLE` を返す |
| `SetResult` はログしない | 各呼び出し元が shape-only ログを出しており二重になるため |

---

## 4. ビルド / 実行結果

| 項目 | 結果 |
|---|---|
| コンパイル | エラー 0（`error CS` の出力なし）。新規ファイル起因の警告なし |
| EditMode テスト | **387 / 387 passed**（実装前 356 → 新規 31: wiring 7 + state 24）、failed 0 |
| PlayMode テスト | 55 件中 **44 passed / 0 failed / 11 skipped** |
| アクティブビルドターゲット | Android のまま（`-buildTarget Android` で実行、変更なし） |
| 生成物の復元 | Unity が並べ替えた `unity-native-plugin.slnx` を `git checkout` で復元 |

- PlayMode の skip 11 件は、iOS 専用 fixture が `#if UNITY_ANDROID` 相当のターゲット条件で除外されるもので、実装前と同じ既知の挙動。本変更による回帰ではない
- 実行コマンド: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode|PlayMode -buildTarget Android -testResults <xml> -logFile <log>`

### 4.1 追加したテストの検証内容

**wiring（7）**: Resources パス（uxml / uss）、AssetDatabase パス、必須ボタン 57 個、必須ラベル 4 個（`ResultTextBlock` / `StatusTextBlock` / `SubtitleValidationLabel` / `ErrorsSectionNote`）、`ResultScrollView` 内に `ResultTextBlock` があること、TopMenu の `ClipboardFeatureButton`。

**state（24）**: 結果整形（OK / NG / `--` / details / running）、**完了順を入れ替えても各 context が自分の sequence・marker で出力されること**、file cleanup の 4 経路、scope ラベルと status、observation の owner 競合（拒否された 2 本目が pending を解除しないこと）、stale owner、Start pending 中の離脱と deferred Stop、**deferred Stop がちょうど 1 回で Stop 失敗後も再発行しないこと**、Start failure 時に Stop を発行しないこと、Restart 成功 / 失敗、Stop 失敗、重複 Stop 要求、enabled 契約の全状態、error ボタンが毎回新しい Named scope を対象にすること。

---

## 5. 手動確認観点

### 5.1 Editor（未実施 — 理由付き）

batchmode ではボタン操作を行えないため、以下は**未実施**。UXML / Controller の name 一致と Resources 解決は wiring テストで機械的に担保済み。

| # | 確認 | 状態 |
|---|---|---|
| S-1 | iOS ターゲットで TopMenu に Clipboard ボタンが出る | 未実施（GUI 操作が必要） |
| S-2 | Clipboard → `Open Sample Screen` で画面遷移 | 未実施（同上） |
| S-3 | Back To Home | 未実施（同上） |
| S-4 | 任意操作が `CLIPBOARD_BRIDGE_UNAVAILABLE` を返す | 未実施（同上） |
| S-5 | 画面往復でイベントが二重購読されない | 未実施（同上） |
| S-6 | Start Observing → 離脱で例外が出ず未観測へ戻る | 未実施（同上） |

### 5.2 実機（未実施 — 実機が必要）

計画 7.2 の S-10〜S-32（M-1〜M-24 対応）はすべて**未実施**。特に次は実機でのみ判定できる。

- S-13a〜c: 2 台による localOnly の載る／載らない（`textLen=14 / 31` で判別）
- S-22 / S-22b: `fileSize=64 cleanup=ok`、独自 UTI の往復（**V-6**）
- S-25a〜d: busy 拒否、teardown（`marker: observe.stop.teardown` の成功ログを同期点にする。計画 7.2.1）、Stop を挟まない Restart、Start failure が未観測になること
- S-31: 約 4 MiB fixture のメモリ計測（M-22）

### 5.3 要検証（計画 5.4 の V-1〜V-6）

| # | 状態 |
|---|---|
| V-1 | シーンファイル未変更で到達できること — **未確認**（Editor GUI が必要）。Navigator の登録と Resources 解決までは自動テストで確認済み |
| V-2 | `LoadItem(File)` の親ディレクトリ削除の妥当性 — **未確認**（実機） |
| V-3 | 範囲外 color が C# 例外にならず `CLIPBOARD_INVALID_COLOR` になること — **未確認**（実機） |
| V-4 | 4 MiB PNG の生成コストと 3〜5 MiB への収まり — **未確認**（実機）。範囲外なら計測せず `fixture=out-of-range bytes=<n>` を表示する実装は入っている |
| V-5 | Start pending 中の `CreatePasteboard` 完了で `active` と `observing` が食い違う表示 — **未確認**（実機） |
| V-6 | `MultiRepresentation` の独自 UTI を `LoadItem(File(custom))` で取得できること — **未確認**（実機） |

### 5.4 M-23（本サンプル対象外）

計画 7.4 のとおり、native → `DllImport` → parser → decoded `byte[]` を通す実機計測は別 artifact へ引き継ぐ。本サンプルでは実施しない。

---

## 6. 計画からの差異（実装時の判断）

| # | 差異 | 理由 |
|---|---|---|
| 1 | ボタン数 55 → **56（+ Home で 57）** | 計画 4.3 のセクション表の合計が 56。見出しの数値が計算違いで、セクション表を正とした |
| 2 | `IosClipboardSampleResultContext` を `IosClipboardSampleResult.cs` に同居 | 1 行の struct で単独ファイルにする利点がない。計画のファイル一覧（7 ファイル）は維持 |
| 3 | 前提エラーの表示を `-- local=<reason>` 形式に統一 | 計画は `ProbeRemovedScope` の注意文のみ規定していた。fixture 生成失敗と範囲外にも同じ形式を使い、native 由来の `NG` と区別できるようにした |
| 4 | 発行 seam を `CreateMissingNamedScope()`（internal static）とした | 計画 6.8.2 は「(marker, owner, targetScope) を記録する seam」を求めていたが、Controller は EditMode で生成できない（`testing.md` 層 1 制約）。**「error ボタンが active scope ではなく毎回新しい Named scope を対象にする」**という契約だけを、インスタンス化なしで固定できる形にした |
| 5 | observation control の結果に `owned=<bool>` を表示 | S-25a で「どちらの呼び出しが状態所有者か」を画面から判別するため |
| 6 | `Load URL` の payload に `urlLen` を追加 | 計画 4.9 は Text / ImageData / File のみ規定。URL 本文を出さずに成否を確認するために長さのみ表示 |
| 7 | `LoadItem(File)` の `Path` が null の場合を追加 | `fileSize=-1 cleanup=failed` として表示。計画の 4 経路表に含まれない経路だが、契約上ありうる |

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
  - 未回答
