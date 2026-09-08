# Windows Clipboard サンプルシーン 実装計画 v3

## 1. 基本情報

- 日付: 2026-09-08
- 機能名: clipboard / 対象プラットフォーム: Windows
- 実装結果ファイル: `artifact/results/clipboard/2026-09-08-windows-clipboard-implementation-feature-result-v6.md`
- 実装計画（機能側）: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v8.md`
- 前版: `2026-09-08-windows-clipboard-sample-scene-design-v2.md`
- レビュー: v1（Claude サブエージェント 4 名。A1 13 / A2 2 / B 20 / C 8）→
  v2（Codex `gpt-5.5` high。**A1 0** / B 4 / C 1）
- **ネイティブ側の実機確認結果**:
  `native-toolkit/artifact/results/clipboard/2026-08-20-windows-clipboard-implement-sample-app-result-v1.md`
  （2026-09-05 完了。OK 24 / NG 1 / 未実施 1、49 ボタン中 48 確認。**v3 で突き合わせた。1.3 節**）

**章番号は v2 から変えていない。** 既存の相互参照を壊さないため、
v3 の追加分は 1.0 / 1.3 / 7.9 / 8.2.1 / 8.8 / 9.3 として既存章の内側に置いた。

### 1.0 v2 からの変更

| # | 出所 | 内容 |
|---|---|---|
| B-1 | レビュー v2 | M-13 に `Application.runInBackground` の前提を追加（7.9） |
| B-2 | レビュー v2 | **M-20 を M-20a / M-20b に分割。** `ShutdownTimeout` に到達するとは限らない（7.6） |
| B-3 | レビュー v2 | M-8 の ACP 前提とフィクスチャ選定を追加（8.2.1） |
| B-4 | レビュー v2 | DIB フィクスチャの機械検査を自動テストに追加（7.7） |
| C-1 | レビュー v2 | M-18 の判定条件を 8.3 に統一 |
| N-1 | ネイティブ実機 | `Copy (Exclude Roaming)` を**未実施**として明記（11 節 R-5） |
| N-2 | ネイティブ実機 | **設定変更イベントが発火しないのは正常**（F3）。7.4 |
| N-3 | ネイティブ実機 | **ボタン単位のカバレッジ照合**を工程に追加（9.3） |
| N-4 | ネイティブ実機 | M-19 はネイティブ F2 の回帰確認であると明記（1.3.1） |
| N-5 | ネイティブ実機 | M-6 の往復判定に上書き注意を追加（10 節 V-5）。**要検証** |
| N-6 | ネイティブ実機 | `GetPreferredFormat` の期待値 4 件を明記（8.8） |

### 1.1 v1 からの変更

レビュー v1 の A1 13 件と B の主要項目を反映した。**v1 の最大の問題は、計画が実装の内部構造を誤って想定していたこと**である。

| v1 の誤り | 実際 |
|---|---|
| provider は二相で呼ばれる | **1 回だけ**呼ばれ、結果はキャッシュされる（D-5 のため再生成は禁止） |
| provider 内でログを出す | provider は **Unity API を呼んではいけない**。シャットダウン中にも走る |
| 1 MB のテキストで再試行経路に入る | 再試行はサイズと無関係。**2 回の呼び出しの間に内容が増えたときだけ** |
| `GetHistory` の戻り値で `MainThreadRequired` が分かる | 戻り値は `uint requestId` で拒否時は 0 のみ |
| macOS サンプルは内容を出している | macOS は内容を一切出さない。**より厳しい方針** |
| 公開 API は 26 種 | **33 種**（v1 の表自体が 33 を列挙していた） |

サンプルシーンは実装の観測装置である。**観測対象の構造を誤ると、装置が嘘の観測結果を出す。**

### 1.2 このサンプルの位置づけ

**デモではなく検証装置として作る。**

公開 API 33 種のうち、ネイティブに対して実行されたのは 5 種だけである
（`Initialize` / `CopyPlainText` / `PastePlainText` / `GetFormats` / quit ドレイン。スモーク結果 v1）。
残りはコードレビューでも Editor テストでも検証できず、**このサンプルが唯一の実行手段**になる。

画面設計は **M-1 〜 M-24（設計 9.3）を実施できること**を要件とする。6 節に対応表を置く。

### 1.3 ネイティブ側の実機確認結果との突き合わせ

ネイティブ（`unity-windows-native-toolkit` の WinUI 3 サンプル）は 2026-09-05 に実機確認を完了している。
**同じ OS の同じ API を先に触った記録であり、Unity 側で同じ発見を繰り返す必要はない。**

機能設計 v8 は F1〜F3 / O1 / O2 / 9.1〜9.9 を取り込み済みだが、
**本計画側に落ちていなかったものが 6 件あった**（1.0 の N-1 〜 N-6）。

#### 1.3.1 そのまま持ち込むもの

| ネイティブの記録 | 本計画での扱い |
|---|---|
| **F1** 履歴有効時は予約直後に provider が発火する（呼ぶのは履歴サービス。実装は正常） | D-8。ブロック C を履歴 OFF で実施する根拠（6 節） |
| **F2** 予約したままアプリを終了すると内容が失われる。`WM_RENDERALLFORMATS` は `DestroyWindow` 時にしか来ず、それは uninit 経路からしか到達しない | **M-19 はこの欠陥の回帰確認である**（N-4）。ネイティブでは実際に起きて修正された。Unity の等価物は quit ドレイン。**省略可能な項目ではない** |
| **F3** 履歴の設定変更イベントは信頼できない（イベントソース側の制約） | 7.4。**発火しないのが期待結果**。不具合として報告しない |
| **O1** スクリーンショット撮影がクリップボードを上書きする | 4 節 7 番。ブロックの区切りでのみ撮影する |
| **O2** `Restore` 直後に `Monitor` が発火する（履歴サービスが書き込むため正常） | 設計 2.2 / 7.6.4。M-14 / M-24 で失敗と読まない |
| **項目 25 / A-9** `EXCLUDE_ROAMING` は 2 台目デバイスと同期有効が要り未実施 | **同じ制約なので Unity でも未実施**（11 節 R-5） |
| **§9 報告事項** `GetPreferredFormat` は `HTML Format` を返さない（候補 4 形式固定） | 8.8 に期待値として明記 |

#### 1.3.2 工程として持ち込むもの（N-3）

ネイティブは **55 シナリオを全消化したあとで、49 ボタン中 9 ボタンが一度も押されていない**ことに気づいた。

> 原因は実施漏れではない。§8 はシナリオの一覧であってボタンの一覧ではないため、
> シナリオに登場しない操作は誰も押さない構造になっている。

**本計画は同じ形をしている。** M-1 〜 M-24 はシナリオであり、8.1 は約 50 ボタンを並べている。
S-2 の一行では同じ穴が開く。**9.3 にボタン単位の照合を独立した工程として置く。**

#### 1.3.3 参考にとどめるもの

| ネイティブの記録 | 理由 |
|---|---|
| 項目 5 の往復で `width=1887 height=820` が返った | 8x8 を置いたはずの往復で別の画像が返っている。O1（撮影による上書き）が原因の可能性が高いが、記録からは確定できない。**要検証**（10 節 V-5）。M-6 の判定に影響する |
| `GlobalSize` が 4KB 境界へ切り上げる | バイト数の差を異常と読まないための知識。Unity 側の判定には使わない |
| 自動 UI テスト 33 件 | 層 3 の道具立て。Unity 側は層 2b までであり、同じ枠組みは持ち込まない |

---

## 2. 前提の抽出

### 2.1 実装済みの公開 API（33 種）

| 分類 | 数 | API |
|---|---|---|
| ライフサイクル | 4 | `Initialize` / `TryShutdown` / `ShutdownWithDrain` / `CanShutdownNow` |
| 書き込み | 7 | `CopyPlainText` / `CopyHtml` / `CopyFiles` / `CopyImage` / `CopyCustomFormat` / `CopyMultipleFormats` / `Clear` |
| 読み出し | 5 | `PastePlainText` / `PasteHtml` / `PasteFiles` / `PasteImage` / `PasteCustomFormat` |
| 内容確認 | 3 | `HasFormat` / `GetFormats` / `GetPreferredFormat` |
| 履歴（callback） | 6 | `GetHistory` / `GetHistoryAvailability` / `RestoreHistoryItem` / `DeleteHistoryItem` / `ClearUnpinnedHistory` / `CancelRequest` |
| 履歴（Awaitable） | 5 | `GetHistoryAsync` / `GetHistoryAvailabilityAsync` / `RestoreHistoryItemAsync` / `DeleteHistoryItemAsync` / `ClearUnpinnedHistoryAsync`（`CancelRequestAsync` は**存在しない**） |
| イベント登録 | 1 | `SetHistoryEventsEnabled` |
| 遅延レンダリング | 2 | `ReserveDeferredFormats` / `RecoverDeferredState` |

**加えて `public event` が 12 種。**

#### 引数の並び（v1 で誤っていた箇所）

| API | `options` の位置 |
|---|---|
| `CopyPlainText` / `CopyFiles` / `CopyImage` / `CopyMultipleFormats` | 第 2 引数 |
| `CopyHtml(string htmlFragment, string? plainText = null, options, onResult)` | **第 3 引数** |
| `CopyCustomFormat(string formatName, byte[] data, options, onResult)` | **第 3 引数** |
| `Clear(Action<...>? onResult = null)` | **持たない** |

### 2.2 エラー契約（設計 8.4）

- `IsSuccess == true` → `ErrorCode == None` かつ `ErrorMessage == null`
- `IsSuccess == false` → `ErrorMessage != null`
- 拒否でも共通イベントと per-call callback は必ず発火。**ただし `TryShutdown` は例外**で、event も callback も発火しない（設計 5.3 / 7.4 段階 6）
- 読み出しは `IsSuccess == true` でも `IsEmpty == true` があり得る。**戻り値 0 は空を意味しない**
- `HasFormat` の `IsSuccess` と `HasFormat` は独立
- 配送は呼び出し元スタック外。**同期発火は 2 つの場合に限る**（teardown ドレイン / quit を再開する直前の settle）

### 2.3 不足前提

| # | 内容 | 扱い |
|---|---|---|
| P-1 | `CopyMultipleFormats` に非 ASCII の `CF_TEXT` を含めたときの欠落挙動 | 実機観測項目（M-8）。**要検証** |
| P-2 | `Application.temporaryCachePath` が返す区切り文字 | Unity は `/` を返すとして扱い、`Path.GetFullPath` で正規化する（8.2）。**要検証** |
| P-3 | M-20（ドレインのタイムアウト）の再現手段 | v1 は「不可能」としたが誤り。7.6 に候補を記す |

## 3. 既存サンプルコードの深掘り

### 3.1 Unity 側の共通パターン

**v1 の表は macOS Clipboard と Windows/Notification を混同していた。** 分けて記す。

| 要素 | macOS Clipboard | Windows Notification |
|---|---|---|
| UXML | `Runtime/Resources/UI/<Platform>/<Feature>/<X>ManagerExample.uxml` | 同左 |
| **USS** | `<X>ManagerExampleStyle.uss`（**既存 15 画面すべてが対で持つ**） | 同左 |
| Controller | `Runtime/UI/<Platform>/<Feature>/<X>ManagerExampleController.cs` | 同左 |
| ヘッダ | `HomeButton` → title → `ResultScrollView`(`ResultTextBlock`) → `StatusTextBlock`。**About は本文先頭のセクション** | `HomeButton` と `ResultTextBlock` のみ。**`ResultScrollView` も `StatusTextBlock` も About も無い** |
| 束ね方 | `Bind(root, name, handler)` ヘルパー | 個別フィールドを手書き |
| 内側ガード | **無し**（Editor でも Manager を呼び拒否コードを見せる） | **あり**（Editor では固定文字列） |
| ボタン解除 | `OnDestroy` | `OnDestroy` |
| イベント購読 | `OnEnable` / `OnDisable` | 同左 |
| 分割 | 3 ファイル（Controller 1173 / SampleResult 329 / ObservationState 159） | 1 ファイル |
| 自動テスト | `Tests/Runtime/MacClipboardSampleSceneWiringTests.cs`（282 行）+ `MacClipboardSampleStateTests.cs`（484 行） | 無し |

**本計画は macOS Clipboard 型を採る。** 理由は 7.3 に記す。

### 3.2 native-toolkit の Windows サンプルとの差分

`native-toolkit/windows/WindowsLibraryExample/ClipboardPage.xaml`（102 行 / 49 ボタン）を確認した。

**採用する:**

| native の操作群 | 採用方針 |
|---|---|
| Initialize / SetHistoryCallbacks / Uninitialize / CanDestroy | 対応（`Uninitialize` は `TryShutdown` と `ShutdownWithDrain` に分ける） |
| Copy 各種 + オプション別（SENSITIVE / EXCLUDE_HISTORY / EXCLUDE_ROAMING） | そのまま採用 |
| Paste 各種 / HasFormat / GetFormats / GetPreferredFormat / Clear | そのまま採用 |
| ReserveDeferredFormats / RecoverDeferredState | そのまま採用 |
| 履歴 6 種 | そのまま採用 |
| 異常系 7 種（null / after Clear / text-only html / 空配列 / duplicate format / after Uninitialize / unknown id） | **全 7 種を 8.1 の 11 節に置く**（v1 は 3 種が落ちていた） |
| `Force Initialize while shutting down` / `Request + Immediate Uninitialize` | **採用**（v1 は分類漏れ）。ドレイン中の再入で `ShuttingDown` / `OperationBusy` を見る |
| `Delayed Worker Check (5s)` | **遅延実行として採用**（v1 はワーカースレッド版に読み替えて仕組みごと落としていた）。M-13 に必要 |
| ワーカースレッド版（reserve / uninitialize） | 形を変えて採用。C# はメインスレッド限定契約なので期待値は `MainThreadRequired` |

**採用しない:**

| native の操作 | 理由 |
|---|---|
| Cleanup Temp Files | Unity 側は `Application.temporaryCachePath` を使い、同等のボタンは置くが役割が異なる |

**native に無く Unity 側で必要:**

| 追加 | 理由 |
|---|---|
| `*Async` 5 種 + `CancellationToken` | C# 固有の API |
| イベント 12 種の観測 | M-10 / M-24 |
| `CanShutdownNow` | C# 側で独立した API |
| lifecycle 状態表示 | `Draining` と `ShutdownFailed` が同じ `ShuttingDown` に畳まれるため（4.1 §0） |

## 4. 画面要件

### 4.1 セクション構成

| # | セクション | 内容 |
|---|---|---|
| 0 | ヘッダ | `HomeButton` / タイトル / **`StateTextBlock`（lifecycle 状態）** / `ResultScrollView`(`ResultTextBlock`) / `StatusTextBlock` |
| 1 | About | 読み方の注意（4.5） |
| 2 | Lifecycle | Initialize / Try Shutdown / Shutdown With Drain / Can Shutdown Now / **Quit** / **Force Initialize While Draining** |
| 3 | Copy | 7 API + バリエーション |
| 4 | Options | Sensitive / Exclude History / Exclude Roaming |
| 5 | Paste | 5 API + 往復判定 |
| 6 | Inspect | HasFormat / GetFormats / GetPreferredFormat / Clear |
| 7 | Deferred | Reserve / Recover / provider 呼び出し回数 |
| 8 | History | callback 6 種 + 同一フレーム版 |
| 9 | History (Await) | `*Async` 5 種 + Cancel 付き |
| 10 | Events | 購読 on/off + 12 イベントの時系列表示 |
| 11 | Threading | ワーカースレッド / **遅延実行（N 秒後）** |
| 12 | Errors | 異常系 9 種（期待値表つき） |
| 13 | Fixtures | 一時ファイル作成 / 削除、直近履歴 id |

**§0 の `StateTextBlock` は v2 で追加。** `Draining`（進行中・回復し得る）と `ShutdownFailed`（終端・アプリ再起動以外に復帰手段なし）は
どちらも `ShuttingDown`(1011) を返すため、`ErrorCode` だけでは区別できない。
製品コードは変更せず、`Initialize` / `ShutdownWithDrain` の結果からサンプル側で追跡する。

### 4.2 操作導線

すべて「ボタン → 実行 → 結果表示」。入力欄は 2 つのみ（カスタムフォーマット名、履歴 item id）。

### 4.3 結果表示

**すべてのログ行に単調増加のシーケンス番号を付ける**（macOS の `MacClipboardSampleResultContext.Sequence` に倣う）。

| 表示 | 内容 |
|---|---|
| `StateTextBlock` | Manager の lifecycle 状態（サンプル側で追跡） |
| `StatusTextBlock` | 直近 1 操作の要約 |
| `ResultTextBlock` | 追記式。`#seq [種別] 操作名 IsSuccess ErrorCode ErrorMessage 形` |

**記録する項目**（v1 から追加した 3 つを太字）:

- 操作名 — **`result.Operation` から出す**（ボタンのラベルではない。結線ミスを S-2 で捕まえるため）
- `IsSuccess` / `ErrorCode` / `ErrorMessage`
- **`IsEmpty`**（読み出し系。「空クリップボードの正常成功」と「0 バイト読み出し」を区別するため）
- **`HasFormat`**（`HasFormat` の結果。`IsSuccess` と独立）
- **`completed`**（`TryShutdown` の `out` 引数）
- 戻り値の形（長さ / 件数 / サイズ）
- 種別 — `[call]`（同期の戻り値）/ `[accept]`（非同期の受付）/ `[done]`（非同期の完了）/ `[event]`（共通イベント）

**種別と seq を持つことで、次が目視できる:**

| 契約 | 見え方 |
|---|---|
| 配送は呼び出し元スタック外（設計 7.1） | `[call]` の seq が `[event]` より小さい |
| 共通イベント → 個別 callback の順 | `[event]` の直後に `[done]` |
| 非同期 exactly-once（設計 8.4） | `[accept]` と `[done]` が 1:1。**未消化件数を画面に常時表示する** |
| M-24 の帰属 | restore の `[done]` と `ClipboardChanged` の `[event]` が隣接する |

### 4.4 クリップボード内容は一切出さない

**v1 の「先頭 16 文字まで表示」は撤回する。** 設計 3.2 の規約は「値を伏せて**派生値**を出す」であり、
原文の部分文字列は派生値ではない。一般的なパスワードは 8〜16 文字で、16 文字なら全体が露出する。
9 節 S-4 とも矛盾していた。

**内容の一致判定は次で行う:**

| 用途 | 方法 |
|---|---|
| copy → paste の往復（M-2 / M-3 / M-21 / M-6） | **直前に書いたフィクスチャと一致するか（bool）+ 長さ** |
| 履歴の内容確認（M-11） | 同じ一致判定 + `ToUtcTime()` の相対秒数 |
| 画像 / カスタム | サイズ + 一致 bool |

これは macOS の方針の**維持**であり拡張ではない（macOS はネイティブのエラーメッセージまで伏せる、より厳しい方針）。

**`ErrorMessage` はそのまま出してよい。** `WindowsClipboardResult.Failure` の detail は全て固定リテラルで、
クリップボード内容を埋め込む経路が無い（macOS が伏せるのは pasteboard 名 / UTI が入るため）。

### 4.5 About に書く注意

1. **失敗時に `ErrorMessage` が空欄なら、それ自体が不具合**（設計 8.4）
2. **成功時に `ErrorCode` が `None` 以外なら不具合**（設計 8.4）
3. **`TryShutdown` は event も callback も発火しない**（設計 5.3。カウンタが増えないのは正常）
4. **`ClipboardChanged` は 1 回の外部コピーで複数回発火する**（実測 3 回。設計 2.2 / 12 章）
5. **`RecoverDeferredState` の `IsSuccess == true` は「回復した」を意味しない。**「何も partial でなかった」「予約が既に消えた」も同じ成功になる（設計 7.7）
6. **`ReserveDeferredFormats` は実行前のクリップボード内容を破棄する**（D-1）
7. 確認中の**スクリーンショット撮影はクリップボードを上書きする**。撮影はブロックの区切りで行う（設計 9.3 O1）

## 5. 変更ファイル一覧

### 5.1 新規作成（7）

| ファイル | 内容 |
|---|---|
| `Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExample.uxml` | 画面定義 |
| `Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExampleStyle.uss` | **v2 で追加。** 既存 15 画面すべてが UXML と対で持つ。無いと `ApplyScreen` が `styleSheets.Clear()` すら行わず、TopMenu のスタイルを被ったまま描画される |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardManagerExampleController.cs` | 操作と結果表示 |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardSampleResult.cs` | 結果整形。**内容を出さない整形をここに閉じ込める**。命名は既存前例（`MacClipboardSampleResult` / `IosClipboardSampleResult`）に合わせる |
| `Runtime/UI/Windows/Clipboard/WindowsClipboardSampleFixtures.cs` | フィクスチャ生成と後始末 |
| `Tests/Runtime/WindowsClipboardSampleSceneWiringTests.cs` | **v2 で追加**（B-1）。7.6 参照 |
| `Tests/Runtime/WindowsClipboardSampleResultTests.cs` | **v2 で追加**。整形とシーケンス番号の検証 |
| `Tests/Runtime/WindowsClipboardSampleFixtureTests.cs` | **v3 で追加**（B-4）。DIB の構造を固定する。8.2.1 |

すべて `Windows` 接頭辞。クラスガードは `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`（P1 / P5）。

**macOS の 3 ファイル構成に倣うが、3 本目の役割は異なる。** macOS の `ObservationState` は状態機械、
本計画の `Fixtures` はフィクスチャ生成である。

### 5.2 既存変更（2）

**v1 は変更箇所を過小に書いていた。** 実物の該当箇所を全て挙げる。

#### `Runtime/UI/Top/TopMenuExampleController.cs`（4 箇所）

| 行 | 変更 |
|---|---|
| 106 | ボタン購読のガードに `\|\| UNITY_STANDALONE_WIN` を追加 |
| 114 | ログ文言を更新 |
| 185 | Editor 用 `DisplayDialog` の本文を更新 |
| **186-193** | **`OnClipboardClicked` の `#elif` 連鎖に `#elif UNITY_STANDALONE_WIN` を追加**（v1 では欠落。これが無いと Windows Player でボタンは押せるが何も起きない） |

#### `Runtime/UI/Common/NativeToolkitSampleNavigator.cs`（2 箇所）

| 箇所 | 変更 |
|---|---|
| 入口メソッド群 | `ShowWindowsClipboard` を追加 |
| **`RemoveExistingControllers`（197-222 行、Windows ブロックは 212-215）** | **`RemoveIfExists<WindowsClipboardManagerExampleController>` を追加**（v1 では欠落。無いと Home に戻っても Controller が残り、イベント購読が解除されず、再訪時に全ボタンが無反応になる） |

両ファイルとも既に 4 プラットフォーム分の入口を持つ横断ナビゲーション基盤であり、P2 の対象外。

### 5.3 非変更

- 他プラットフォームの `Android*` / `Ios*` / `Mac*`（P2）
- `Runtime/Clipboard/` の実装本体（**サンプルのために製品コードへ試験用の細工を入れない**）
- `Runtime/Common/`（P4）

## 6. M-1 〜 M-24 との対応

**前提条件ごとにブロック化する。** 条件の切り替え回数を最小化し、
D-8（履歴有効時は予約直後に全形式が実体化される）による誤判定を避けるため。

### ブロック A: 履歴 ON・フォアグラウンド（既定）

| M | 担当 | 外部で見るもの |
|---|---|---|
| M-1 | 2. Initialize | — |
| M-23 | 2. Initialize 直後 | タスクバー / Alt+Tab |
| M-2 | 3. Copy Plain Text | メモ帳に貼り付け |
| M-3 | 5. Paste Plain Text | メモ帳でコピーしてから |
| M-4 | 3. Copy Html | Word / ブラウザ |
| M-21 | 3. Copy Large Text → 5. Paste Plain Text の**往復**、3. Copy Image → 5. Paste Image の往復 | — |
| M-6 | 3. Copy Image → 5. Paste Image（往復一致）→ ペイント | ペイント。**コピーと貼り付けの間にクリップボードへ何も書かないこと**（撮影を含む。1.3.3 / V-5） |
| M-5 | 3. Copy Files（13. でファイル作成後） | エクスプローラ |
| M-7 | 3. Copy Multiple Formats | 複数アプリで比較 |
| M-8 | 3. Copy Multiple Formats (CF_TEXT + 非 ASCII) | **`CF_UNICODETEXT` は一致し、`CF_TEXT` は不一致または `?` へ lossy 変換**（8.2 の ACP 前提つき） |
| M-9 | 4. Copy (Sensitive) | Win+V に出ないこと |
| M-11 | 8. Get History | Win+V と突き合わせ |
| M-14 | 8. Restore / Delete / Clear Unpinned | Win+V の状態変化 |
| M-24 | 8. Restore + 10. の時系列 | — |
| M-15 | **8. Restore Twice In One Frame** | — |
| M-16 | **8. Issue And Cancel In One Frame** / 9. Get History (Await + Cancel) | — |
| M-10 | 10. の時系列 | 他アプリでコピー |

### ブロック B: 非フォアグラウンド

| M | 担当 |
|---|---|
| M-13 | **11. Delayed History Call (5s)** を押し、5 秒以内に別ウィンドウを前面にする。**判定は「別ウィンドウが前面のまま `[done]` が出て `NotForeground` であること」**（7.9） |

### ブロック C: 履歴 OFF（OS 設定で無効化）

**先に 10. で購読を解除しておくこと**（`SetHistoryEventsEnabled` の停止に失敗すると
`MonitorRegisterFailed` が sticky になり、以後の再有効化が拒否され続ける。設計 2.9）。

| M | 担当 | 備考 |
|---|---|---|
| M-12 | 8. Get History Availability / Get History | `HistoryDisabled` を確認 |
| M-17 | 7. Reserve（実行前後で 5. Paste を比較） | D-1 |
| M-18 | 7. Reserve → 他アプリに貼り付け | **判定は「provider 呼び出し回数が 1、かつ貼り付け先に内容が届く」**（8.3） |
| M-19 | 7. Reserve → **2. Quit** → 終了後に貼り付け | 履歴 ON だと D-8 で実体化されてしまい、M-19 が通ったことにならない。**ネイティブ F2 の回帰確認（1.3.1）。省略しない** |
| M-20a | **2. Shutdown While Disabled**（7.6） | **再試行せずに 1 回で終わること**。`ShutdownTimeout` が返るとは限らない |
| M-20b | — | **未実施**。`ShutdownTimeout` には native が `NotYet` を返す条件が要る（7.6） |

### ブロック D: 破壊的（最後）

| M | 担当 |
|---|---|
| — | 12. Errors 全 9 種。`Copy After Shutdown` は最後。押した後は 2. Initialize からやり直す |

### ブロック E: IL2CPP

| M | 担当 |
|---|---|
| M-22 | Scripting Backend を IL2CPP に切り替えて再ビルドし、ブロック A 〜 C を再実施 |

**実行契約**（`testing.md` 層 2b が明示を必須とする）: Scripting Backend = IL2CPP、
Target architecture = x86_64、ビルド種別 = Development Build。
スモークは Mono + Development Build だった。

### 実施順序の助言

**先に 1 回ずつ叩くべき 5 項目**（計画側の穴で落ちやすい順）:
M-6（DIB フィクスチャ）→ M-5（パス区切り）→ M-8（`CF_TEXT`）→ M-18 / M-19（provider 契約と履歴の干渉）→ M-13（非フォアグラウンドの作り方）。

**各ブロックの区切りでのみスクリーンショットを撮る**（設計 9.3 O1）。

## 7. 実装方針

### 7.1 再利用する既存コンポーネント

- `NativeToolkitSampleNavigator`（入口追加 + `RemoveExistingControllers` 登録）
- macOS Clipboard の UXML 構造・`Bind` ヘルパー・`Sequence` の考え方

### 7.2 追加するコンポーネント

| 型 | 役割 |
|---|---|
| `WindowsClipboardManagerExampleController` | 画面と操作 |
| `WindowsClipboardSampleResult` | 結果整形。内容を出さない整形とシーケンス番号をここに閉じ込める |
| `WindowsClipboardSampleFixtures` | フィクスチャ生成と後始末 |

### 7.3 内側コンパイルガードは置かない

**明示的な決定。** 既存 Windows の ExampleController（Notification / Dialog）は内側ガードを持ち、
Editor では固定文字列を表示して Manager に到達しない。**本計画はこれを採らない。**

理由: `ClassifyOperationGuard` は状態より先に `PlatformUnavailable` を返すため、
Editor でも Manager を呼べば拒否コードが観測できる。内側ガードを入れると
**S-6（Editor で `PlatformUnavailable` を確認）が永久に観測不能になる。**

macOS / iOS Clipboard と同じ形にする。

### 7.4 コールバック購読方針

| 対象 | 方針 |
|---|---|
| 同期 API | 戻り値を `[call]` 行に出す。**`Clear` のみ per-call callback も併用**し、共通イベント → 個別 callback の順序を見せる |
| 非同期 API（callback 版） | 戻り値の `requestId` を `[accept]` 行に、callback を `[done]` 行に出す |
| 非同期 API（Await 版） | `requestId` を返さないため `[accept]` 行は作れない。`[done]` のみ |
| 共通イベント 12 種 | `OnEnable` で購読し、`[event]` 行として**時系列に混ぜる**（カウンタだけにしない） |

**`ClipboardChanged` は回数を主表示にしない。** 1 回の外部コピーで実測 3 回発火するため、
カウンタ表示は正常動作を毎回「不具合」に見せる（設計 12 章の申し送り）。
**最終検知の seq と時刻**を出し、回数は副次表示にとどめる。

**`HistoryEnabledChanged` / `RoamingEnabledChanged` は運ぶ `bool` も出す**（設計 2.2 の「値は信頼できない」の実挙動確認のため）。

**発火しないことが期待結果である。** ネイティブ側の実機確認（native-toolkit F3）は次を測っている。

| セッション | 購読時の履歴状態 | 設定変更イベント |
|---|---|---|
| A | オン | 最初のオフで 1 回だけ発火。以後は何度切り替えても発火せず |
| B（再起動後） | オフ | 一度も発火せず |

原因は `Clipboard.HistoryEnabledChanged` のイベントソース側にあり、購読基盤と配送は正常である
（同セッションで履歴追加イベントは発火した）。**ブロック C で OS 設定の履歴を切っても
これらが発火しないのは正常であり、不具合として報告してはならない。**
設定の現在値が要る時点では `GetHistoryAvailability` を呼ぶ（設計 2.2）。

### 7.5 入力バリデーションと期待値

**サンプル側では検証しない。** 不正値はそのまま Manager に渡す。
ただし**期待値は操作ごとに異なる**ので 8.5 に表を置く。

`Copy Plain Text (null)` は `#nullable enable` 下で CS8625 になるため、
`null!` で明示的に抑制し、その行に理由コメントを置く。

### 7.6 M-20a / M-20b の扱い

**v1 の「サンプルからは起こせない」は誤り。** 製品コードを変えずに到達できる候補が 2 つある。

| 候補 | 内容 | 採否 |
|---|---|---|
| **(a) 強制 1 回打ち切り** | `ShutdownWithDrain` は `isActiveAndEnabled == false` のとき `AdvanceDrain(force: true)` を 1 回だけ回す。`enabled` は公開プロパティであり試験用細工ではない | **採用（M-20a）。** 2. に `Shutdown While Disabled` ボタンを置く |
| (b) 予算消尽 | 外部プロセスに `OpenClipboard` を握らせ続ければ `Busy` が返り続ける可能性がある | 見送り。小さな外部ツールが要る（層 3 の道具立て）。**要検証** |

**(a) が `ShutdownTimeout` になるとは限らない。v2 の記述は誤りだった。**

```
WindowsClipboardManager.cs:2886  if (progress != WindowsClipboardShutdownProgress.NotYet)
                          2888      FinishDrain(session);   // ← force 分岐に入る前に返る
                          2889      return;
```

`force || spent` で `ShutdownTimeout` にするのは `progress == NotYet` のときだけ（`2905-2921`）。
正常な実機では 1 回目の native uninit が完了するため、`Shutdown While Disabled` は**成功で終わる**。

したがって確認対象を 2 つに分ける。

| # | 何を見るか | 実施 |
|---|---|---|
| **M-20a** | disabled 時に **再試行せずに 1 回で終わること**。成功でも失敗でもよい | **実施する** |
| **M-20b** | `ShutdownTimeout` そのものの観測 | **未実施。** native が `NotYet` を返す条件が要り、(b) と同じ道具立てになる |

**M-20a を「成功したから失敗」と読まないこと。** ここで見るのは終端の種類ではなく試行回数である。

**(a) と (b) は別分岐である。** 実装はログ文言を書き分けており、
(a) は「no attempts left」、(b) は「exceeded its budget」になる。

v1 が代替案とした「ログで分岐の存在を確認する」は実行を伴わないコード確認であり、
結果 v6 が「コードレビューでは検証できない」と結論づけた対象そのもの。**採らない。**

### 7.7 自動テスト

**v1 には自動テストが 1 本も無かった。** macOS Clipboard には wiring テスト（282 行）と
状態テスト（484 行）があり、同種の wiring テストがリポジトリに 5 本ある。

**フィクスチャは managed 側の検証を素通りする。** `CopyImage` は
`dib == null || dib.Length == 0` しか見ない（`WindowsClipboardManager.cs:1245-1248`）。
既存テストも `CopyImage(new byte[0])` の拒否までで DIB 構造を固定していない
（`WindowsClipboardManagerIntegrationTests.cs:1113-1119`）。
つまり**フィクスチャ生成を後から壊しても C# は通り、実機で「画像機能が動かない」に見える。**
機械検査で固定する。

| テスト | 検証内容 |
|---|---|
| `WindowsClipboardSampleSceneWiringTests` | UXML / **USS** の Resources パス存在、全ボタン名が UXML にあるか、ボタン総数が計画と一致するか、1 ボタン 1 ハンドラか、フィクスチャ literal |
| `WindowsClipboardSampleResultTests` | 整形が内容を出さないこと、シーケンス番号が単調増加すること |
| `WindowsClipboardSampleFixtureTests` | **v3 で追加（B-4）。** DIB の構造を固定する。8.2.1 参照 |

**USS の存在検査は今回のレビューで見つかった欠落（A1-3）を実装フェーズで捕まえる。**

### 7.8 UI の有効・無効について

設計 12 章は「更新系履歴操作は in-flight 中ボタン無効化が望ましい」「shutdown 開始後は UI も操作不能にする」を申し送っている。
**本計画はどちらも採らない。** M-15（`OperationBusy` の確認）と 12. Errors（`Copy After Shutdown`）が
実施できなくなるため。

代わりに **`StateTextBlock` で状態を示し、押せる状態を保つ**。この判断を About にも書く。

### 7.9 `Application.runInBackground`

**M-13 の前提。** `ProjectSettings/ProjectSettings.asset:85` は `runInBackground: 0` である。
この設定のまま別ウィンドウを前面にすると、**Player はフレームを回さない。**

そうなると `Delayed History Call (5s)` の遅延発火も callback 表示もフォアグラウンド復帰後に動き、
`NotForeground` を踏まないまま**成功として観測される**。M-13 が確認になっていない状態で
「確認済み」と記録されるため、未確認より悪い。

**方針**: `WindowsClipboardManagerExampleController.OnEnable` で `Application.runInBackground = true` を
設定し、`OnDisable` で元の値へ戻す。ProjectSettings は変更しない（他サンプルへ波及するため）。

判定条件を「**別ウィンドウが前面のまま** `[done]` 行が出て、`NotForeground` であること」とする。
復帰後に出たものは M-13 の結果として採らない。

## 8. 実装詳細

### 8.1 セクション別のボタン

| セクション | ボタン |
|---|---|
| 2. Lifecycle | Initialize / Try Shutdown / Shutdown With Drain / Can Shutdown Now / **Quit** / **Shutdown While Disabled** / **Force Initialize While Draining** |
| 3. Copy | Copy Plain Text / (empty) / Copy Large Text / Copy Html / Copy Files / Copy Image / Copy Custom Format / Copy Multiple Formats / (with image) / **(CF_TEXT + non-ASCII)** / **(duplicate format)** |
| 4. Options | Copy (Sensitive) / (Exclude History) / (Exclude Roaming) |
| 5. Paste | Paste Plain Text / **(after Clear)** / Paste Html / **(text only)** / Paste Files / Paste Image / Paste Custom Format |
| 6. Inspect | Has Format (CF_UNICODETEXT) / Get Formats / Get Preferred Format / Clear |
| 7. Deferred | Reserve Deferred Formats / Recover Deferred State |
| 8. History | Get History Availability / Get History / Restore Last / Delete Last / Clear Unpinned / Cancel Last / **Restore Twice In One Frame** / **Issue And Cancel In One Frame** / **Request + Immediate Shutdown** |
| 9. History (Await) | Get History (Await) / (Await + Cancel) / **Get Availability (Await)** / **Restore (Await)** / **Delete (Await)** / **Clear Unpinned (Await)** |
| 10. Events | Enable History Events / Disable History Events / Reset |
| 11. Threading | Copy From Worker Thread / Get History From Worker Thread / **Delayed History Call (5s)** |
| 12. Errors | 8.5 の 9 種 |
| 13. Fixtures | Create Temp Files / Delete Temp Files |

太字は v2 で追加。入力欄は `CustomFormatNameField`（既定 `NativeToolkitSample`）と `HistoryItemIdField`（自動補完）。

### 8.2 フィクスチャ

| 種類 | 定義 |
|---|---|
| Plain text | `"NativeToolkit clipboard sample <連番>"` |
| Large text | 約 1 MB の反復文字列。**再試行経路のためではなく、大きなペイロードの往復確認のため**（8.6） |
| HTML | `<b>` を含む断片 + 平文フォールバック。text-only 版は `plainText` のみ |
| Files | `Path.GetFullPath(Path.Combine(Application.temporaryCachePath, ...))` で **`\` 区切りの絶対パス**に正規化した 2 ファイル。ネイティブは区切りを正規化せず `DROPFILES` へそのまま書くため、`/` のままだと `CopyFiles` は成功するのにエクスプローラへの貼り付けだけが失敗する |
| Image | **8x8 / 32bpp / `BI_RGB` の DIB、296 バイト**（`BITMAPINFOHEADER` 40B + 画素 256B）。`BITMAPFILEHEADER` は付けない。`biHeight > 0`（bottom-up）、`biSizeImage` を設定する。ネイティブの `ValidateDib` は `biPlanes != 1` / `biWidth <= 0` / `biHeight == 0` / 想定外の `biBitCount`・`biCompression` を拒否する |
| Custom | UTF-8 バイト列 |
| Multiple（通常） | `Text("CF_UNICODETEXT", ...)` + `Html("HTML Format", ...)` |
| Multiple（画像入り） | `Text("CF_UNICODETEXT", ...)` + **`Bytes("CF_DIB", dib)`**（`Bytes` ファクトリを通す唯一の操作） |
| Multiple（M-8 用） | **`Text("CF_UNICODETEXT", 非 ASCII)` + `Text("CF_TEXT", 同一内容)`**。文字列は**現在の ACP で表現できないもの**を選ぶ（8.2.1） |
| Multiple（重複） | 同一フォーマット名を 2 件 |

#### 8.2.1 `CF_TEXT` の欠落は環境依存である

実装の契約は「欠落し**得る**」であって「非 ASCII なら必ず欠落」ではない。

```
WindowsClipboardPayloads.cs:55  CF_TEXT is encoded by the native layer with the system ANSI code page,
                          56    so non-ASCII text can be lost for that format.
```

ACP と文字の組み合わせによっては表現できる。**Windows 11 には ACP を UTF-8 にする設定があり、
その環境では欠落しない。** 断定した判定条件を置くと正常な実装を失敗扱いする。

- **前提条件**: ACP が UTF-8 でないこと。`GetACP()` 相当を実施前に確認する
- **フィクスチャ**: 現在の ACP で表現できない文字を含める。日本語 ACP（932）なら
  ハングルやキリル文字が該当する。**ASCII + 日本語だけの文字列は 932 環境で欠落しない**
- ACP が UTF-8 の環境では **M-8 を未実施（要検証）として記録する**

**DIB の仕様は native-toolkit の `BuildSampleDib`（`ClipboardPage.xaml.cpp:261-286`）と
`ValidateDib`（`WindowsClipboardFormats.cpp:269-345`）から確定した。** v1 の「要検証」は解消済み。

### 8.3 遅延レンダリングの provider

**provider は純粋関数にする。Unity API を一切呼ばない。**

`ReserveDeferredFormats` の契約（`WindowsClipboardManager.cs:1949-1962`）は
「Unity API を呼ぶな」「MonoBehaviour / Texture を参照せず値でキャプチャせよ」
「**providers also run while the application is shutting down**」と定めている。
D-7 のとおり provider は `uninit` の `DestroyWindow` から同期送出され、
Unity のシャットダウン進行中に走る（= M-19 の経路そのもの）。

| 許される | 許されない |
|---|---|
| 値でキャプチャした `byte[]` を返す | `Debug.Log` |
| `Interlocked.Increment(ref s_renderCount)`（static int） | `VisualElement` の更新 |
| — | MonoBehaviour / Texture の参照 |

**表示は `Update` で `s_renderCount` を読んで行う。**

**provider は 1 形式につき 1 回しか呼ばれない。** `RenderDeferredFormat` は
サイズ問い合わせ相でのみ provider を呼び、結果を `s_renderCache` に格納する。
充填相はキャッシュを使う（D-5 の「2 回目のサイズは 1 回目と完全一致」を守るため再生成は禁止）。

**したがって M-18 の判定は「provider が 1 回呼ばれ、貼り付け先に内容が届くこと」である。**
呼び出し回数が 2 になることは無い。

### 8.4 D-1 〜 D-9 の確認可能性

| D | 確認できるか |
|---|---|
| D-1（Reserve が既存内容を破棄） | **できる**（M-17） |
| D-2 〜 D-5（規約違反で形式が黙って落ちる） | **間接的のみ**。Manager が規約を守るため正常系では破られず、「守れていること」しか示せない |
| D-6（provider の制約） | **確認できない**。8.3 のとおり守る側に回る |
| D-7（シャットダウン中の provider 実行） | **M-19 で経路は通る**。観点として「provider が Unity オブジェクトに触っていないこと」を加える |
| D-8（履歴有効時に予約直後に全形式が実体化） | **ブロック C の前提条件として扱う**。M-17 / M-18 / M-19 は履歴 OFF で実施する |
| D-9（失敗地点で renderer 世代が変わる） | **起こせない**。`EmptyClipboard` 失敗や個別 `SetClipboardData` 失敗をサンプルから起こす手段が無い。**未実施として記録する** |

### 8.5 12. Errors の期待値と実施順序

**前提: `Running`。`Copy After Shutdown` は最後に押し、その後は Initialize からやり直す。**
`CopyPlainText` は状態ガードを引数検証より先に通すため、順序を守らないと期待値が変わる。

| ボタン | 期待 |
|---|---|
| Copy Plain Text (null) | `InvalidArgument` |
| Copy Files (empty) | `InvalidArgument` |
| Copy Custom Format (blank name) | `InvalidArgument` |
| Copy Multiple Formats (empty) | `InvalidArgument` |
| Restore (blank id) | `InvalidArgument` |
| **Restore (unknown id)** | **`ItemDeleted` または `Unknown` が `[done]` 行で返る**（`ValidateItemId` は空白のみ弾くため、ネイティブまで届く） |
| **Cancel (unknown id)** | **`requestId == 0` のときだけ `InvalidArgument`。0 以外は 0 / 2 / 19 のいずれか** |
| **Copy After Shutdown** | **`ShutdownFailed` / ドレイン中なら `ShuttingDown`、完了後なら `NotInitializedByHost`** |
| Paste Plain Text (after Clear) | `IsSuccess == true` かつ `IsEmpty == true`（**失敗ではない**） |

### 8.6 M-21 の判定基準

**v1 の「1 MB でバッファ再試行経路に入る」は誤り。** `ReadRaw` の再試行は
**2 回目の呼び出しが `BufferTooSmall` を返したときだけ**発生する。
すなわち 2 回の呼び出しの間にクリップボード内容が増えた競合時のみで、
**ペイロードサイズは分岐条件に入らない。** 何 MB にしても入らない。

**M-21 の判定は「大きなペイロードでも 2 回呼び出しが正しく往復すること」とする。**
Copy → Paste の往復で長さと一致 bool を確認する。再試行分岐そのものは層 1 で検証済み。

### 8.7 ワーカースレッド呼び出し

**v1 は 2 点誤っていた。**

1. **`GetHistory` の戻り値からは `MainThreadRequired` が読めない。** 戻り値は `uint requestId` で拒否時は 0。
   エラーコードは `RejectRequest` 経由で共通イベント / per-call callback にだけ渡る
2. **`RejectRequest` はワーカースレッドからでも dispatcher 経由でメインスレッドに配送される。**
   「次の `Update` で表示する」仕掛けは不要

**方針**: ワーカーから **per-call callback 付きで呼び**、callback がメインスレッドで走ることを確認する。
これは設計 7.1 の「拒否も dispatcher 経由」の実証を兼ねる。

**`Instance` はメインスレッドで捕まえてローカル変数に入れ、`Task.Run` のクロージャにはその参照だけを渡す。**
`Instance` getter は `_instance == null` のとき `GameObject` を生成するため、
ワーカースレッドから触ると Unity 例外になる（設計 7.2）。前提として「`Initialize` 済み」とする。

### 8.8 `GetPreferredFormat` の期待値

ネイティブ側が実機で確定した 4 件をそのまま期待値に使う（1.3.1）。
**`PickPreferredFormat` の候補は `{CF_UNICODETEXT, CF_HDROP, CF_DIB, CF_BITMAP}` 固定で、
`HTML Format` を含まない**（設計 2.9）。

| クリップボードの状態 | 期待する戻り |
|---|---|
| text + HTML | `CF_UNICODETEXT`。**`HTML Format` は返らない** |
| files のみ | `CF_HDROP` |
| image のみ | `CF_DIB` |
| custom のみ | **空文字**。`Empty` エラーは返らない（設計 2.9 / 1072 行） |

**空文字を失敗と読まないこと。** `IsSuccess == true` かつ内容が空文字が正常な結果である。

## 9. 手動確認観点

### 9.1 サンプルシーン自体（S-1 〜 S-8）

| # | 観点 |
|---|---|
| S-1 | TopMenu から Clipboard を選ぶと **Windows Player で**本画面が開く |
| S-2 | 全ボタンが例外を出さずに動作する。**かつログの操作名が `result.Operation` 由来で、押したボタンと一致する**（結線ミスの検出） |
| S-3 | 失敗時に `ErrorMessage` が必ず表示される。成功時に `ErrorCode` が `None` である |
| S-4 | クリップボードの内容が画面にもログにも出ていない |
| S-5 | `HomeButton` で戻れる。**戻った後にイベント購読が解除されている**（`RemoveExistingControllers` の確認） |
| S-6 | **再入場してもイベント購読が二重にならない**（カウンタが 2 倍にならない） |
| S-7 | Editor 実行時の期待値: **`TryShutdown` / `ShutdownWithDrain` は冪等成功**、11. Threading は `MainThreadRequired`、**それ以外は `PlatformUnavailable`**。例外で落ちない |
| S-8 | `[accept]` と `[done]` の未消化件数が、操作を止めれば 0 に戻る（exactly-once の目視） |

**S-7 の注記**: Editor では TopMenu が `DisplayDialog` を出すだけで本画面に到達しない。
S-7 を実施するには 5.2 の Editor 分岐も本画面へ遷移させる必要がある。**要検討事項**（10 節 V-3）。

### 9.2 M 項目

6 節のブロック A 〜 E に従う。**M-20a は 7.6 (a) で実施、M-20b は未実施。D-9 は未実施。**
**M-8 は ACP が UTF-8 の環境では未実施**（8.2.1）。

### 9.3 ボタン単位のカバレッジ照合（N-3）

**M 項目を全消化したあとで、独立した工程として実施する。**

ネイティブ側は 55 シナリオを全消化したあとに 49 ボタン中 9 ボタンが未操作であることを発見した。
シナリオ一覧はボタン一覧ではないため、シナリオに登場しない操作は誰も押さない（1.3.2）。

手順:

1. 8.1 の全ボタンを表にし、「自動テストで操作」「M 項目で操作」「どちらでもない」に分ける
2. どちらでもないものを 1 つずつ押し、**結果と `ErrorCode` を記録する**
3. 押せなかったものは**理由つきで未実施として記録する**（環境制約か、再現条件が無いか）

**確認していないものを OK として扱わない。**

現時点で M 項目に現れないと分かっているボタン:

| ボタン | 扱い |
|---|---|
| `Copy (Exclude Roaming)` | **未実施**（R-5）。押して `ErrorCode` が `None` であることまでは見るが、効果は観測できない |
| `Copy (Exclude History)` | 押して Win+V に出ないことを見る。M-9（Sensitive）と同じ判定 |
| `Recover Deferred State` | 「復旧すべき状態がないときに安全に呼べ、正常な予約を破壊しないこと」まで。`PartialState` は再現手段が無い（R-1 / D-9） |
| `Delete Temp Files` | M-5 のあと。2 回目が 0 件で成功することを見る |

**この表は網羅ではない。** 実装後に 8.1 の全ボタンで作り直す。

## 10. 要検証事項

v1 は 4 件を同列に並べていたが、**着手前に決めるもの / 実機観測項目 / 検証項目でないもの**を分ける。

### 着手前に決めるもの（なし）

v1 の V-1（DIB）は 8.2 で確定した。V-3（1 MB）は 8.6 で判定基準を変更した。
V-4（ワーカースレッド）は `ClassifyOperationGuard` が最初に `isMainThread` を見ることが
コードで確定しており、層 1 テストもあるため検証項目から外した。

### 実機観測項目

| # | 内容 |
|---|---|
| V-1 | `CF_TEXT` の欠落挙動（M-8）。**まず `GetACP()` 相当で ACP を確認する**（8.2.1）。UTF-8 なら未実施 |
| V-2 | `Application.temporaryCachePath` が返す区切り文字。`Path.GetFullPath` で正規化する前提だが、実機で確認する |
| V-5 | 画像の往復で置いたものと違う画像が返らないか（1.3.3）。ネイティブは 8x8 を置いた往復で `1887x820` を得ている。O1 による上書きが原因と推測できるが確定していない。**M-6 が落ちたらまずこれを疑う** |

### 要検討事項

| # | 内容 |
|---|---|
| V-3 | S-7（Editor での拒否コード確認）を実施するために、TopMenu の Editor 分岐を本画面へ遷移させるか。現状は `DisplayDialog` のみで到達できない |
| V-4 | M-20b（予算消尽経路）を層 3 の道具立てとして用意するか |

## 11. 直さない残件

| # | 内容 | 理由 |
|---|---|---|
| R-1 | D-9（予約失敗時の世代管理） | サンプルから失敗を起こす手段が無い。未実施として記録 |
| R-2 | M-20b（`ShutdownTimeout` の観測） | native が `NotYet` を返す条件が要り、外部ツールの道具立てになる。**(a) では確認できない**（7.6。v2 の記述は誤りだった） |
| R-3 | D-2 〜 D-6 | Manager が規約を守る側なので、正常系では「守れていること」しか示せない |
| R-4 | 設計 12 章の「UI 無効化」の申し送り 2 件 | 採らない（7.8）。M-15 と 12. Errors が実施できなくなるため |
| R-5 | `Copy (Exclude Roaming)` の効果 | **同一 Microsoft アカウントの 2 台目 Windows デバイスと、両機での「デバイス間で同期」有効が要る。**ネイティブ側も同じ理由で未実施（項目 25 / A-9）。本機は `roamingEnabled: false`。9.3 で「押した」までを記録する |
| R-6 | `HistoryEnabledChanged` / `RoamingEnabledChanged` の発火 | **イベントソース側の制約**（ネイティブ F3）。発火しないことが期待結果であり、Unity 側で直せるものではない（7.4） |

## 12. 実装時のチェックリスト（A2 区分）

**コンパイルエラーとして必ず表面化するため計画では直さないが、実装時に注意する。**

- [ ] `CopyHtml` / `CopyCustomFormat` の `options` は**第 3 引数**。`Clear` は `options` を持たない
- [ ] `CopyPlainText(null)` は CS8625。`null!` で抑制し理由コメントを置く
