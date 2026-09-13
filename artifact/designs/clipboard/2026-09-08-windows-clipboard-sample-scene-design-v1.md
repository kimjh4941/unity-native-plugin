# Windows Clipboard サンプルシーン 実装計画 v1

## 1. 基本情報

- 日付: 2026-09-08
- 機能名: clipboard / 対象プラットフォーム: Windows
- 実装結果ファイル: `artifact/results/clipboard/2026-09-08-windows-clipboard-implementation-feature-result-v6.md`
- 実装計画（機能側）: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v8.md`
- ブランチ: `feature/UNT-11`

### 1.1 このサンプルの位置づけ

**デモではなく検証装置として作る。**

結果 v6 のとおり、公開 API 26 種のうち**ネイティブに対して実行されたのは 5 種だけ**である
（`Initialize` / `CopyPlainText` / `PastePlainText` / `GetFormats` / quit ドレイン）。
残り 21 種はコードレビューでも Editor テストでも検証できず、**このサンプルが唯一の実行手段**になる。

したがって画面設計は「見栄え」ではなく **M-1 〜 M-24（設計 9.3）を実施できること**を要件とする。
6 節に対応表を置き、どのボタンがどの M 項目を担うかを固定する。

## 2. 前提の抽出（実装結果 v6 由来）

### 2.1 実装済みの公開 API（26 種）

| 分類 | API |
|---|---|
| ライフサイクル | `Initialize` / `TryShutdown` / `ShutdownWithDrain` / `CanShutdownNow` |
| 書き込み | `CopyPlainText` / `CopyHtml` / `CopyFiles` / `CopyImage` / `CopyCustomFormat` / `CopyMultipleFormats` / `Clear` |
| 読み出し | `PastePlainText` / `PasteHtml` / `PasteFiles` / `PasteImage` / `PasteCustomFormat` |
| 内容確認 | `HasFormat` / `GetFormats` / `GetPreferredFormat` |
| 履歴（callback） | `GetHistory` / `GetHistoryAvailability` / `RestoreHistoryItem` / `DeleteHistoryItem` / `ClearUnpinnedHistory` / `CancelRequest` |
| 履歴（Awaitable） | 上記 5 種の `*Async`（`CancellationToken` 対応） |
| イベント登録 | `SetHistoryEventsEnabled` |
| 遅延レンダリング | `ReserveDeferredFormats` / `RecoverDeferredState` |

書き込み系は全て `WindowsClipboardWriteOptions options = None` を第 2 引数に持つ。

### 2.2 エラー契約（設計 8.4）

- `IsSuccess == true` のとき `ErrorMessage == null`
- `IsSuccess == false` のとき `ErrorMessage != null`
- 拒否（受付前失敗）でも共通イベントと per-call callback は必ず発火
- 同期 API は戻り値で即時、イベント / callback は呼び出し元のスタック外

**サンプルは per-call callback ではなく戻り値でも結果を受け取れるが、
本計画では両方を画面に出す**（同期 API は戻り値、非同期は callback）。

### 2.3 不足前提（勝手に補わない）

| # | 内容 | 扱い |
|---|---|---|
| P-1 | `CopyImage` に渡す DIB のバイト列仕様（ヘッダ形式・行の向き） | 実装時にネイティブ側 `CopyImage` の受け入れ形式を確認する。**要検証** |
| P-2 | `CopyCustomFormat` / `PasteCustomFormat` のフォーマット名の慣習 | サンプルでは `NativeToolkitSample` を使う。命名規約は無いものとして扱う |
| P-3 | M-20（ドレインのタイムアウト）を画面から起こす手段 | 製品コードに試験用の細工は入れない。**サンプルからは再現できない**。7.6 参照 |

## 3. 既存サンプルコードの深掘り

### 3.1 Unity 側の共通パターン（維持する）

`Runtime/UI/macOS/Clipboard/` と `Runtime/UI/Windows/Notification/` を確認した。

| 要素 | 現状 |
|---|---|
| UXML | `Runtime/Resources/UI/<Platform>/<Feature>/<X>ManagerExample.uxml` |
| Controller | `Runtime/UI/<Platform>/<Feature>/<X>ManagerExampleController.cs`（`MonoBehaviour` + `UIDocument`） |
| 画面上部 | `HomeButton` → タイトル → About → `ResultScrollView` / `ResultTextBlock` / `StatusTextBlock` |
| 操作 | 機能カテゴリごとにセクション見出し + ボタン群 |
| 命名 | `<Thing>Button`、要素は `root.Q<Button>(name)` で取得 |
| 束ね方 | `Bind(root, name, handler)` ヘルパーで購読を一元化 |
| ライフサイクル | イベント購読は `OnEnable` / `OnDisable`。`Start` で `UIDocument` を解決 |
| 遷移 | `NativeToolkitSampleNavigator.Show<Platform><Feature>(uiDocument)` |
| 分割 | macOS Clipboard は 3 ファイル（Controller 1173 行 / ObservationState 159 / SampleResult 329） |

**macOS の 3 ファイル構成を踏襲する。** Windows の操作数は macOS を上回るため、
Controller 1 本にすると 1500 行を超える。

### 3.2 native-toolkit の Windows サンプルとの差分

`native-toolkit/windows/WindowsLibraryExample/ClipboardPage.xaml`（102 行 / 約 50 操作）を確認した。

**採用する（Unity 側にも同じ操作を置く）:**

| native の操作群 | 採用方針 |
|---|---|
| Initialize / SetHistoryCallbacks / Uninitialize / CanDestroy | そのまま対応（`Uninitialize` は `TryShutdown` と `ShutdownWithDrain` の 2 つに分ける） |
| Copy 各種（plain / empty / html / files / image / custom / multi / multi+image） | そのまま採用 |
| Copy のオプション別（SENSITIVE / EXCLUDE_HISTORY / EXCLUDE_ROAMING） | そのまま採用。M-9 に必要 |
| Paste 各種 / HasFormat / GetFormats / GetPreferredFormat / Clear | そのまま採用 |
| ReserveDeferredFormats / RecoverDeferredState | そのまま採用 |
| 履歴 6 種 | そのまま採用 |
| 異常系（null / after Clear / text-only html / 空配列 / duplicate format / after Uninitialize / unknown id） | 採用。C# 側は引数検証が入るため**期待値が異なる**（4.4） |
| ワーカースレッド版（reserve / uninitialize / delayed check） | **形を変えて採用**。C# 契約はメインスレッド限定なので、期待値は `MainThreadRequired` |

**採用しない:**

| native の操作 | 理由 |
|---|---|
| Cleanup Temp Files | native サンプル固有のファイル管理。Unity 側は `Application.temporaryCachePath` に作り、同等のボタンは置くが役割が異なる |
| CF_BITMAP / CF_DIB 指定の CopyMultipleFormats | C# の `WindowsClipboardFormatPayload` は `Text` / `Html` / `Base64` / `Bytes` の 4 ファクトリで、フォーマット名は文字列。native の列挙と 1 対 1 にならない。**同等の意図をカバーする形に読み替える**（4.2） |

**native に無く Unity 側で必要なもの:**

| 追加 | 理由 |
|---|---|
| `*Async` + `CancellationToken` の操作 | C# 固有の API。M-16 の確認に必要 |
| イベント 12 種の購読状況と発火カウンタ | M-10 / M-24 の確認に必要。native は callback を直接持つため画面表示が無い |
| `CanShutdownNow` | C# 側で独立した API になっている |

## 4. 画面要件

### 4.1 セクション構成

上から順に。既存サンプルの並び（Back → タイトル → About → 結果表示 → 操作群）を維持する。

| # | セクション | 目的 |
|---|---|---|
| 0 | ヘッダ | `HomeButton` / タイトル / About / `ResultScrollView` / `StatusTextBlock` |
| 1 | Lifecycle | `Initialize` / `TryShutdown` / `ShutdownWithDrain` / `CanShutdownNow` |
| 2 | Copy | 6 API + 空文字 + 複数形式 |
| 3 | Options | `Sensitive` / `ExcludeHistory` / `ExcludeRoaming` を付けた copy |
| 4 | Paste | 5 API |
| 5 | Inspect | `HasFormat` / `GetFormats` / `GetPreferredFormat` / `Clear` |
| 6 | Deferred | `ReserveDeferredFormats` / `RecoverDeferredState` / provider 呼び出しログ |
| 7 | History | callback 5 種 + `CancelRequest` |
| 8 | History (Await) | `*Async` + `CancellationToken` |
| 9 | Events | `SetHistoryEventsEnabled` on/off + 12 イベントの発火カウンタ |
| 10 | Threading | ワーカースレッドからの呼び出し（`MainThreadRequired` の確認） |
| 11 | Errors | 引数検証・状態違反の異常系 |
| 12 | Fixtures | 一時ファイルの作成 / 削除、直近の履歴 id 表示 |

### 4.2 操作導線

すべて「ボタン → 実行 → 結果表示」。入力欄は最小限にする。

- **入力欄を置くもの**: カスタムフォーマット名（既定 `NativeToolkitSample`）、履歴 item id（直近取得分を自動補完）
- **入力欄を置かないもの**: それ以外はすべて固定のフィクスチャを使う

理由: 実機確認の手順を短くするため。入力を要求すると M 項目 24 個の実施が重くなる。

### 4.3 結果表示

| 表示 | 内容 |
|---|---|
| `StatusTextBlock` | 直近 1 操作の要約（成功 / 失敗 + `ErrorCode`） |
| `ResultTextBlock` | 追記式のログ。操作名・`IsSuccess`・`ErrorCode`・`ErrorMessage`・戻り値の形（長さ / 件数 / サイズ） |

**クリップボードの内容そのものは画面に出さない。**
設計 3.2 のログ規約（機微情報を残さない）をサンプルにも適用する。
`PastePlainText` の結果は「長さ」と「先頭 16 文字」までとし、
`PasteImage` / `PasteCustomFormat` はサイズのみ表示する。

**この方針は native サンプルとの差分である**（native は内容を出している）。

### 4.4 エラー表示

`ErrorMessage` をそのまま出す。設計 8.4 により失敗時は必ず非 null なので、
**空欄になったらそれ自体が不具合**である。その旨を About に書く。

## 5. 変更ファイル一覧

### 5.1 新規作成（4）

| ファイル | 内容 |
|---|---|
| `Packages/.../Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExample.uxml` | 画面定義 |
| `Packages/.../Runtime/UI/Windows/Clipboard/WindowsClipboardManagerExampleController.cs` | 操作と結果表示 |
| `Packages/.../Runtime/UI/Windows/Clipboard/WindowsClipboardSampleFixtures.cs` | 一時ファイル / DIB / カスタムデータの生成 |
| `Packages/.../Runtime/UI/Windows/Clipboard/WindowsClipboardSampleLog.cs` | 結果整形（内容を出さない整形を 1 箇所に集約） |

すべて `Windows` 接頭辞、クラスガードは `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`（P1 / P5）。

### 5.2 既存変更（2）

| ファイル | 変更 | P2 の扱い |
|---|---|---|
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowWindowsClipboard` を追加 | **横断ナビゲーション基盤**であり、既に 4 プラットフォーム分の入口を持つ。プラットフォーム固有ファイルではないため P2 の対象外 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | Clipboard ボタンの対応プラットフォームに Windows を追加（現在は Android / iOS / macOS のみ。114 行目のログ文言も更新） | 同上 |

### 5.3 非変更

- 他プラットフォームの `Android*` / `Ios*` / `Mac*`（P2）
- `Runtime/Clipboard/` の実装本体（**サンプルのために製品コードへ試験用の細工を入れない**）
- `Runtime/Common/`（P4）

## 6. M-1 〜 M-24 との対応

**この表が本計画の主目的である。** 空欄が残る M 項目は、サンプルでは実施できない。

| M | 担当する操作 | 外部で見るもの |
|---|---|---|
| M-1 | 1. `Initialize` | — |
| M-2 | 2. `Copy Plain Text` | メモ帳に貼り付け |
| M-3 | 4. `Paste Plain Text` | メモ帳でコピーしてから |
| M-4 | 2. `Copy Html` | Word / ブラウザに貼り付け |
| M-5 | 2. `Copy Files` + 12. フィクスチャ作成 | エクスプローラに貼り付け |
| M-6 | 2. `Copy Image` | ペイントに貼り付け |
| M-7 | 2. `Copy Multiple Formats` | 複数アプリに貼り付けて比較 |
| M-8 | 2. `Copy Multiple Formats (non-ASCII)` | 貼り付け先で欠落を確認 |
| M-9 | 3. `Copy Plain Text (Sensitive)` | Win+V に出ないこと |
| M-10 | 9. イベントカウンタ | 他アプリでコピー |
| M-11 | 7. `Get History` | Win+V と突き合わせ |
| M-12 | 7. `Get History Availability` / `Get History` | 履歴を OS 設定で無効化してから |
| M-13 | 7. 各操作 | 別ウィンドウを前面にしてから |
| M-14 | 7. `Restore` / `Delete` / `Clear Unpinned` | Win+V の状態変化 |
| M-15 | 7. `Restore` 連打 | — |
| M-16 | 8. `Get History (Await + Cancel)` / 7. `Cancel Request` | — |
| M-17 | 6. `Reserve Deferred Formats` | 予約前後で貼り付け内容を比較 |
| M-18 | 6. `Reserve` + provider ログ | 他アプリに貼り付け |
| M-19 | 6. `Reserve` → アプリ終了 | 終了後に貼り付け |
| M-20 | **担当なし（P-3）** | — |
| M-21 | 2. `Copy Large Text` / `Copy Image` | — |
| M-22 | **画面ではなくビルド構成**（IL2CPP でサンプルを再ビルドし M-1 〜 M-21 を再実施） | — |
| M-23 | 1. `Initialize` 後 | タスクバー / Alt+Tab |
| M-24 | 7. `Restore` + 9. イベントカウンタ | — |

**M-20 と M-22 は画面では担えない。**
M-20 は製品コードに細工を入れない方針のため実施できず、残件として据え置く。
M-22 はビルド構成の切り替えであり、サンプル自体は共通。

## 7. 実装方針

### 7.1 再利用する既存コンポーネント

- `NativeToolkitSampleNavigator`（入口追加のみ）
- `UnityMainThreadDispatcher`（Manager 内部で使用済み。サンプルは直接触らない）
- macOS Clipboard の UXML 構造・USS クラス・`Bind` ヘルパーの形

### 7.2 追加するコンポーネント

| 型 | 役割 |
|---|---|
| `WindowsClipboardManagerExampleController` | 画面と操作 |
| `WindowsClipboardSampleFixtures` | 一時ファイル / DIB / バイト列の生成と後始末 |
| `WindowsClipboardSampleLog` | 結果整形。**内容を出さない整形をここに閉じ込める** |

### 7.3 共通パターンの維持と拡張

| パターン | 扱い |
|---|---|
| ヘッダ構成・`Bind`・命名・ナビゲーション | **維持** |
| `OnEnable` / `OnDisable` でのイベント購読 | **維持**。ただし対象が 12 イベントと多いため、購読/解除を 1 メソッドに集約する |
| 結果表示 | **拡張**。非同期操作があるため「受付（requestId）」と「完了」を別行で出す |
| 内容の非表示 | **拡張**。macOS サンプルは内容を出しているが、Windows は出さない（4.3） |

### 7.4 コールバック購読方針

- **同期 API**: 戻り値を使う。per-call callback は使わない（二重表示を避ける）
- **非同期 API（履歴）**: per-call callback を使い、戻り値の `requestId` は「受付」行に出す
- **共通イベント 12 種**: `OnEnable` で購読し発火カウンタを更新。**結果表示の主役にはしない**
  （どの操作の結果か判別できないため。設計 7.1 の「共通イベントは常に発火」の確認用）

### 7.5 入力バリデーション方針

**サンプル側では行わない。** 不正値はそのまま Manager に渡し、
`InvalidArgument` が返ることを見せる（11. Errors セクションの目的）。

例外は履歴 id のみで、直近の `GetHistory` 結果から補完する（手入力の手間を省くため）。

### 7.6 M-20 について

ドレインのタイムアウトは、ネイティブが `uninit` で完了を返さない状況が必要である。
**サンプルからは起こせない。** 製品コードに試験用の分岐を入れれば可能だが、
それは出荷物に検証専用の経路を残すことになるため採らない。

代替として、実機確認時に**ログで `ShutdownTimeout` の分岐が存在することを確認する**にとどめ、
M-20 は「未実施」として記録する。

## 8. 実装詳細

### 8.1 セクション別の UI 要素

| セクション | ボタン |
|---|---|
| 1. Lifecycle | Initialize / Try Shutdown / Shutdown With Drain / Can Shutdown Now |
| 2. Copy | Copy Plain Text / Copy Plain Text (empty) / Copy Large Text / Copy Html / Copy Files / Copy Image / Copy Custom Format / Copy Multiple Formats / Copy Multiple Formats (with image) / Copy Multiple Formats (non-ASCII) |
| 3. Options | Copy (Sensitive) / Copy (Exclude History) / Copy (Exclude Roaming) |
| 4. Paste | Paste Plain Text / Paste Html / Paste Files / Paste Image / Paste Custom Format |
| 5. Inspect | Has Format (CF_UNICODETEXT) / Get Formats / Get Preferred Format / Clear |
| 6. Deferred | Reserve Deferred Formats / Recover Deferred State |
| 7. History | Get History Availability / Get History / Restore Last Item / Delete Last Item / Clear Unpinned / Cancel Last Request |
| 8. History (Await) | Get History (Await) / Get History (Await + Cancel) |
| 9. Events | Enable History Events / Disable History Events / Reset Counters |
| 10. Threading | Copy From Worker Thread / Get History From Worker Thread |
| 11. Errors | Copy Plain Text (null) / Copy Files (empty) / Copy Custom Format (blank name) / Copy Multiple Formats (empty) / Restore (unknown id) / Cancel (unknown id) / Copy After Shutdown |
| 12. Fixtures | Create Temp Files / Delete Temp Files |

入力欄: `CustomFormatNameField`（既定 `NativeToolkitSample`）、`HistoryItemIdField`（自動補完、読み取り専用ではない）。

### 8.2 フィクスチャ

| 種類 | 内容 |
|---|---|
| Plain text | `"NativeToolkit clipboard sample <連番>"` |
| Large text | 約 1 MB の反復文字列（M-21 のバッファ再試行を通すため） |
| HTML | 小さな `<b>` を含む断片 + 平文フォールバック |
| Files | `Application.temporaryCachePath` に 2 ファイル作成 |
| Image | 小さな DIB を生成。**バイト列仕様は P-1 として要検証** |
| Custom | UTF-8 バイト列 |
| Multiple | Text + Html の 2 件。画像版は Text + Base64 の 2 件 |

### 8.3 遅延レンダリングの provider

`ReserveDeferredFormats` に 2 つのフォーマットを登録し、provider 内で**呼び出しをログに残す**。
二相で呼ばれること（1 回目サイズ問い合わせ、2 回目充填）が M-18 の確認点であり、
provider が何回呼ばれたかを画面に出す。

### 8.4 ワーカースレッドからの呼び出し

`System.Threading.Tasks.Task.Run` で `CopyPlainText` / `GetHistory` を呼び、
`MainThreadRequired` が返ることを確認する。
**結果表示はメインスレッドへ戻してから行う**（`UnityMainThreadDispatcher` ではなく、
Manager が返した結果を保持し、次の `Update` で表示する）。

## 9. 手動確認観点

### 9.1 サンプルシーン自体の確認（S-1 〜 S-6）

| # | 観点 |
|---|---|
| S-1 | TopMenu から Clipboard を選ぶと Windows Player で本画面が開く（現在は Windows で非表示） |
| S-2 | 全ボタンが `NullReferenceException` を出さずに動作する |
| S-3 | 失敗時に `ErrorMessage` が必ず表示される（空欄なら不具合。4.4） |
| S-4 | クリップボードの内容が画面にもログにも出ていない（4.3） |
| S-5 | `HomeButton` で戻れる。戻った後にイベント購読が解除されている |
| S-6 | Editor 実行時は全操作が `PlatformUnavailable` になり、例外で落ちない |

### 9.2 M 項目の実施

6 節の対応表に従う。**M-20 は実施不可、M-22 は IL2CPP 再ビルド後に M-1 〜 M-21 を再実施。**

## 10. 要検証事項

| # | 内容 |
|---|---|
| V-1 | `CopyImage` の DIB バイト列仕様（P-1） |
| V-2 | `CopyMultipleFormats` に非 ASCII を含めたときの欠落挙動（M-8）。設計 2.x の記述と実挙動の一致 |
| V-3 | 1 MB のテキストでバッファ再試行経路に入るか。入らない場合はサイズを上げる（M-21） |
| V-4 | ワーカースレッドからの呼び出しで、`MainThreadRequired` が返る前にネイティブへ到達しないこと |
