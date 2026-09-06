# サンプルシーン実装結果レポート

## 基本情報

- 日付: 2026-09-05
- 機能名: clipboard
- 対象プラットフォーム: macOS
- ブランチ: `feature/UNT-10`
- サンプル計画: `artifact/designs/clipboard/2026-09-05-macos-clipboard-sample-scene-design-v3.md`
- 参照する実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v13.md`（以降 `v13`）
- 対象レビュー: `artifact/reviews/clipboard/2026-09-05-macos-clipboard-sample-scene-design-review-v1.md`（A1 6 件）、`-v2.md`（中断。回収 3 件）

## 0. このサンプルの位置づけ

**実機確認 32 項目（`v13 §7.5`）を macOS Standalone Player で駆動するための道具である。** 見た目のデモではない。

**P/Invoke 境界はまだ一度も実行されていない。** Editor では `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` により P/Invoke がコンパイルされないため、既存の 676 テストはすべてその手前で止まっている。**このサンプルをビルドして動かすことがブリッジの初回実行になる。**

## 1. 実装サマリー

### 1.1 計画書由来の実装

| 項目 | 計画 | 実装 |
| --- | --- | --- |
| 画面構成 | `§4.1` | 固定ヘッダー（Home / タイトル / 結果履歴 ScrollView / ステータス行）+ 縦スクロールの操作領域 |
| ボタン | `§4.3` | **43 個**。UXML は計画書のボタン表から生成し、名前のずれを排除した |
| セクション | `§4.3` | About / Scope / Copy / Options / Append / Read / Detect / Observe / Clear / Errors |
| per-call callback のみで画面更新 | `§6.2` | 実装。共通イベント 12 本は shape のみのログ。`ClipboardChanged` だけが画面を更新する |
| 結果の採番と対応付け | `§3.2` | `MacClipboardSampleResultContext` を呼び出しごとに closure で捕捉 |
| 監視状態機械 | `§6.3` | `MacClipboardSampleObservationState` |
| エラー正規化 | `§4.6` | `MacClipboardSampleResult`。**29 コード全件に token** |
| 到達コード表示 | `§3.4` | ステータス行に `Codes: 1508,1512 (2/10)` |
| 判定値 4 種 | `§6.6` | `derived` / `behavior` / 登録別カウンタ / `roundTrip` |
| fixture | `§6.4` | 12 MiB は単一 item・単一 representation・`public.utf8-plain-text`。押下時に生成し保持しない |
| `elapsedMs` | `§7.4` / D-15 | 全結果行に付与 |
| ナビゲーション | `§5.4` | `ShowMacClipboard` 追加、TopMenu の macOS 分岐追加 |

### 1.2 レビュー指摘 3 点の実装（最重要）

| 指摘 | 実装 | テスト |
| --- | --- | --- |
| **A1-4** 失敗した再 Start 後の deferred stop | `TakeDeferredStop()` が**成否ではなく `IsObserving`** で判断する。iOS の `IsSuccess && StopRequestedAfterStart` は写していない | `DeferredStop_AfterAFailedRestart_IsStillIssued` |
| **A1-5** ネイティブ message の露出 | `FormatFailure` は `code` と自前の `reason` token のみ出す。**raw message はどこにも渡らない** | `FailureLine_CarriesTheCodeAndATokenButNoNativeMessage`（1507 の pasteboard 名が出ないことを検証） |
| **R-3** `changeCount` ゲート | `Copy` 成功時に `Ownership.ChangeCount` を退避し、`Read` で一致しなければ `derived` / `roundTrip` を `n/a` にする | `Derived_WhenThePasteboardChangedSinceOurWrite_IsNotApplicable` ほか |

### 1.3 実装時の追加判断 4 件

**1. UXML をボタン表から生成した。** 43 ボタン × 6 箇所（フィールド / `Q<Button>` / 購読 / 解除 / UXML / テスト）で約 260 箇所の同期が要る。手書きだと 1 文字の食い違いが**サイレントな no-op** になる（Controller はログを出して続行する）。計画書の表を正本にして UXML を生成し、テストが全 43 件の存在と総数を検証する形にした。

**2. ハッシュに FNV-1a を使った。** `roundTrip` は比較結果しか表示しないため暗号学的強度は不要で、`System.Security.Cryptography` を持ち込むとサンプル 1 画面のために依存が増える。**ハッシュ値そのものは表示もログもしない。**

**3. `RefreshInteractivity()` で前提が崩れているボタンを無効化した。** scope 変更は監視中に不可、`Append` は ownership 取得後のみ、など。**「順序を間違えただけの拒否」を「ネイティブの契約」と読み違えないようにするため。**

**4. `_activeRegistrationMarker` を実装途中で削除した。** 書き込むだけで読まれない状態になっていた。登録の識別は結果行の `* onChanged {registration} count=N` が担っており、フィールドは不要だった。

### 1.4 計画からの逸脱

**なし。** `§4.3` の 43 ボタン、`§5.1`〜`§5.4` のファイル構成とも計画どおり。

## 2. 変更ファイル

### 2.1 新規作成（Runtime）— 3 ファイル / 1,576 行

| ファイル | 行 |
| --- | ---: |
| `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs` | 1,149 |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleResult.cs` | 268 |
| `Runtime/UI/macOS/Clipboard/MacClipboardSampleObservationState.cs` | 159 |

### 2.2 新規作成（Resources）— 2 ファイル / 267 行

| ファイル | 行 |
| --- | ---: |
| `Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExample.uxml` | 113 |
| `Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExampleStyle.uss` | 154 |

USS は iOS 版からの**複製**である（`common.md` によりプラットフォーム間でファイルを共有しない）。クラス名は `mac-` 接頭辞に置換し、複製である旨をファイル先頭に明記した。

### 2.3 新規作成（Tests）— 2 ファイル / 556 行 / 40 件

| ファイル | 行 | 件数 |
| --- | ---: | ---: |
| `Tests/Runtime/MacClipboardSampleStateTests.cs` | 391 | 33 |
| `Tests/Runtime/MacClipboardSampleSceneWiringTests.cs` | 165 | 7 |

### 2.4 既存変更（Runtime）— 2 ファイル

| ファイル | 変更 |
| --- | --- |
| `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowMacClipboard` を追加。`RemoveExistingControllers` の macOS ブロックに追加 |
| `Runtime/UI/Top/TopMenuExampleController.cs` | Clipboard ボタンのガードに `UNITY_STANDALONE_OSX` を追加。`OnClipboardClicked` に macOS 分岐を追加。Editor ダイアログ文言を更新 |

**この 2 ファイルは横断ナビゲーション基盤であり、`common.md`「共通ファイルを作らない方針」の対象外である。** 変更内容は macOS 専用ガード付きの入口追加のみで、Clipboard ロジックの共有化ではない。iOS Clipboard 追加時も同じ 2 ファイルを変更している。

**変更前は macOS Player から Clipboard 画面に到達できなかった**（ボタンが非表示で、分岐も存在しなかった）。

### 2.5 非変更

`Runtime/Clipboard/Mac*`（Manager 含む 18 ファイル）、`Runtime/UI/iOS/Clipboard/*`、`Runtime/Resources/UI/Top/TopMenuExample.uxml`、`Tests/**/*.asmdef`、サンプルシーン `.unity`。

## 3. ビルド・実行結果

| プラットフォーム | 総数 | 成功 | 失敗 | 判定 |
| --- | ---: | ---: | ---: | --- |
| EditMode | **560** | 560 | 0 | **Passed** |
| PlayMode | 116 | 116 | 0 | **Passed** |

- サンプル実装前は EditMode 520。**40 件を追加**して 560
- `error CS` 0 件、新規ファイルに `warning CS` 0 件
- **Unity Editor でのコンパイル確認は完了。macOS Standalone Player のビルドは未実施**（4.2）

## 4. 手動確認観点

### 4.1 32 項目とボタンの対応

計画書 `§4.3` の対応表がそのまま手順になる。判定値は `§6.6` のとおり結果行に出る。

**代表的な手順:**

| 7.5 | 操作 | 期待 |
| --- | --- | --- |
| 1 | `CopyPlainText` → 他アプリで Cmd+V | 貼り付けできる。結果行が `OK itemCount=1 changeCount=N` |
| 2 | `CopyPlainText` → `AppendWithLastOwnership` | 成功し、**`changeCount` が変わらない** |
| 3 | 他アプリでコピー → `AppendWithStaleOwnership` | `NG code=1511 reason=ownershipLost` |
| 4 | `CopyHtml` → `Read` | `derived=true`（HTML から plain text が派生） |
| 10 | `ErrRemoveGeneral` | `NG code=1508 reason=standardPasteboard`。**pasteboard 名が出ないこと** |
| 16 | `StartObserving` → `RestartObserving` → 他アプリでコピー | `* onChanged observe.restart#N count=1` のみ増え、**旧登録は 0 のまま** |
| 17 | `ErrObservingIntervalMatrix` | 4 行すべてが `NG code=1523`（0 / 61 / -1 / NaN） |
| 21 | `CopyLargeSingleItem` → Player 終了 → 貼り付け | **貼り付けできない**（lazy data provider） |
| 23 | `ErrCopyOversize` | `NG code=9007 reason=requestTooLarge` |
| 25 | `CopyUnicode` → `Read` | `roundTrip=match` |
| 27 | Player.log 確認 | clipboard 本文・pasteboard 名・検出値が出ていない |
| **28** | 何か 1 つ操作して Player.log | **`callbackOnMainThread: true` が出る**（`v13 §5.6.14`） |

### 4.2 サンプル自体の確認（計画書 `§7.3`）

S-1〜S-7 の 7 項目。とくに **S-4（再 Start 失敗後に画面を離れて stop が発行される）** と **S-7（1507 / 1508 で pasteboard 名が出ない）** は、レビュー指摘の回帰確認にあたる。

### 4.3 未実施項目

| 項目 | 理由 |
| --- | --- |
| **実機確認 32 項目すべて** | **macOS Standalone Player のビルドが未実施。** 本レポートは Editor でのコンパイルとテストまでを扱う |
| S-1〜S-7 | 同上（S-5 の Editor 実行のみ、PlayMode テストが同等の経路を通っている） |
| 画面の見え方（SV-4） | 実機のウィンドウサイズでの収まりは未確認。最小ウィンドウサイズも未決 |
| App Sandbox 有効ビルド（SV-3 / V-5） | 手順は計画書 `§9.2` にあるが未実施 |

**自動テストで担保した範囲は「純粋ロジックと結線」までである。** 画面が実際に描画され、ボタンがネイティブに届くことは実機でしか確認できない。

## 5. Definition of Done（計画書 `§8`）

| 条件 | 判定 |
| --- | --- |
| `§5.1`〜`§5.3` のファイル作成、コンパイルエラー無し | 満たす |
| `§5.4` の 2 ファイル変更、TopMenu から到達できる | **コード上は満たす。実機での到達確認は未実施**（S-1） |
| `public` メンバに英語 XML コメント | 満たす |
| 表示・ログにネイティブ `Error.Message` が出ていない | 満たす（テストで検証） |
| 本文 / pasteboard 名 / 検出値 / base64 / `Read` の型名が出ていない | 満たす |
| 失敗した再 Start で `IsObserving` を維持 | 満たす（テスト） |
| 失敗した再 Start 後に deferred stop が発行される | 満たす（テスト） |
| 非所有トークンの完了が状態を変えない | 満たす（テスト） |
| エラー正規化が 1507 / 1508 で pasteboard 名を出さない | 満たす（テスト） |
| 未知 code が `unmapped` に落ちる | 満たす（テスト） |
| `MacClipboardErrorCodes` 全定数に token | 満たす（リフレクションで検証） |
| `changeCount` 不一致で `n/a` | 満たす（テスト） |
| 43 ボタンすべての名前を検証 | 満たす（総数も検証） |
| 既存テスト全件 pass | 満たす（EditMode 560 / PlayMode 116） |
| `Ios*` / `Android*` / `Windows*` を変更していない | 満たす |
| `Runtime/Clipboard/Mac*` を変更していない | 満たす |
| `.meta` を新規作成していない | 満たす（Unity が自動生成） |

## 6. 次のステップ

1. **`review-implementation-sample-scene`** — 実装レビュー
2. **macOS Standalone Player をビルドし、7.5 の 32 項目と S-1〜S-7 を実施する**
3. V-2 / V-3 / V-5 / V-6 / V-7 / V-8 / V-9 / V-10 / V-13 と、**v13 で計測手段を用意した V-4** を実測値で更新する
4. `write-manual` → `release`

**2 が本作業の目的である。** ここまではその準備であり、ブリッジが動くことはまだ何も確認できていない。
