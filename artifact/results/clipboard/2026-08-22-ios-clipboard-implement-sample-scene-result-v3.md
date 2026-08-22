# iOS Clipboard サンプルシーン実装結果 v3

## 基本情報

- 日付: 2026-08-22
- 機能名: clipboard
- 対象プラットフォーム: iOS
- 計画ファイル: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v5.md`（承認済み）
- 前版: `artifact/results/clipboard/2026-08-16-ios-clipboard-implement-sample-scene-result-v2.md`（レビュー v2 で LGTM）
- 実装対象ブランチ: `feature/UNT-9`
- 出力言語: 日本語（**コード内の文言・コメントはすべて英語**）

v2 以降の変更点と、**実機確認（M-1〜M-24 / V-1〜V-6）の結果**をまとめる。

---

## 0. v2 からの変更

### 0.1 レイアウトと Editor 導線（実機の見た目に基づく修正）

実機ではヘッダーが画面の 8 割以上を占め、ボタン領域が数行しか残らなかった。

| 変更 | 内容 |
|---|---|
| 説明文2つを About セクションへ移設 | 固定ヘッダーから外し、スクロール領域の先頭に置いた。`SubtitleValidationLabel` は名前を保持（wiring テストはツリー全体を検索するため通る） |
| ヘッダーに上限、スクロールに下限 | `.ios-clipboard-header { max-height: 50% }` / `.ios-clipboard-scroll { min-height: 50% }` |
| 結果枠を 72px → 64px | 固定高 + 内部スクロールは維持 |
| 未使用スタイルの削除 | `.ios-clipboard-subtitle` |
| Copy Options の注記に但し書きを追加 | native サンプルと同じく「localOnly が近隣デバイスへの転送を抑止するかはこの画面では示せない」を明記。長さで判別できることと、転送可否を検証できることは別である旨を利用者へ伝える |
| **Editor 導線の撤回** | Clipboard の Editor ダイアログを他3機能と同じ OK 1つに戻した。**Editor から sample screen へ遷移しない** |

Back To Home とタイトルは他画面と共通のため変更していない。

### 0.2 Editor 導線撤回の影響

計画 v5 の S-1〜S-6（Editor 手動確認）は**実施不可**になった。検証は実機に一本化される。UXML と Controller の name 一致・Resources 解決は wiring テスト7件が引き続き自動で担保する。

### 0.3 実機で発見・修正した不具合（4件）

| # | 内容 | 対象 | 検証 |
|---|---|---|---|
| 1 | **base64 に `/` が含まれるとデコードに失敗**。native の `JSONSerialization` が `/` を `\/` にエスケープするのに対し、C# の `TryGetBase64Bytes` がエスケープ付き文字列を一律 `Malformed` としていた。`LoadItem(Image)` と `ReadData` が実質常に失敗する（4 MiB では成功確率 0%） | `IosClipboardJsonReader.cs` | EditMode 3件追加 + **実機確認済み** |
| 2 | `DetectPatterns` の結果が件数のみで、どの種別が検出されたか判別できない | `IosClipboardSampleResult.cs` / Controller | EditMode 1件追加 + 実機確認済み |
| 3 | **正常な打ち切り `CLIPBOARD_CANCELLED` が `Debug.LogError` で出力**され、利用者のエラー監視に誤検知が乗る | `IosClipboardJsonParser.cs` | EditMode 2件（既存1件を回帰ガード化 + 新規1件）+ 実機確認済み |
| 4 | native の `JSONSerialization` が `/` をエスケープし、C# 側でゼロコピー経路を外れる（64 MiB で約 170 MB 余分） | native `UnityIosClipboardJsonParser.swift` | xcframework 再ビルド・配置済み |

1 は計画・レビューの全工程で検出できず、**実機の 1x1 PNG の load で初めて露見した**。EditMode テストが C# 側で JSON を組み立てており、スラッシュのエスケープを再現していなかったことが原因。

---

## 1. 変更ファイル

### 1.1 新規（v2 から変更なし、7ファイル）

`Runtime/UI/iOS/Clipboard/` の Controller と純粋 helper 2件、`Runtime/Resources/UI/iOS/Clipboard/` の UXML / USS、`Tests/Runtime/` の wiring / state テスト。

### 1.2 v2 以降に変更したファイル

| パス | 内容 |
|---|---|
| `Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml` | About セクションの新設と説明文の移設 |
| `Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExampleStyle.uss` | ヘッダー上限・スクロール下限・結果枠の高さ |
| `Runtime/UI/Top/TopMenuExampleController.cs` | Editor ダイアログを OK 1つへ戻す |
| `Runtime/Clipboard/IosClipboardJsonReader.cs` | **base64 の `\/` を受理**（`TryUnescapeSlashes`） |
| `Runtime/Clipboard/IosClipboardJsonParser.cs` | **cancelled を `Debug.Log` へ**（`CancelledErrorCode`） |
| `Runtime/UI/iOS/Clipboard/IosClipboardSampleResult.cs` | `FormatPatternKinds` |
| `Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs` | `kinds=` 表示 |
| `Tests/Runtime/IosClipboardJsonReaderTests.cs` | エスケープ付き base64 の往復と上限判定（2件） |
| `Tests/Runtime/IosClipboardJsonParserTests.cs` | 実機で失敗した応答の再現、cancelled の非エラー化（3件） |
| `Tests/Runtime/IosClipboardSampleStateTests.cs` | `FormatPatternKinds`（1件） |
| `Plugins/iOS/*.xcframework` | `.withoutEscapingSlashes` 適用版の再ビルド |

---

## 2. ビルド / 自動テスト結果

| 項目 | 結果 |
|---|---|
| コンパイル | エラー 0 |
| EditMode | **396 / 396 passed**（v2 の 391 から +5） |
| PlayMode | 55 件中 44 passed / 0 failed / 11 skipped（iOS 専用 fixture は Android ターゲットで除外。既知） |
| アクティブビルドターゲット | Android のまま |
| `unity-native-plugin.slnx` | 差分なしを確認 |

---

## 3. 実機確認の結果

端末: iOS 18 実機。Xcode のデバイスログと画面の結果行・status 行で判定した。

### 3.1 合格（22 件）

| M | 内容 | 主な確認結果 |
|---|---|---|
| M-1 | Copy Plain Text の文字化け | 日本語・絵文字・サロゲートペアが化けない |
| M-2 | Copy HTML Text | 合格 |
| M-3 | URL / ImageFile / ImageData / Color | color は `Snapshot` の `colors=True` で確認。**native 側の `com.apple.uikit.color` UTI の要検証も解消** |
| M-5 | 30 秒失効 | 合格 |
| M-6 | Append | `items=2` |
| M-7 | Snapshot と Read の貼り付け許可 | **`Snapshot` では許可ダイアログが出ず、`Read` でのみ出る**。検出系 API が内容を読まないことの裏付け |
| M-8 | 空 scope の ReadData | `hasData=false` の成功 |
| M-9 | Named の作成・読み書き・削除 | `named(len=44)` |
| M-10 | general の削除拒否 | `CLIPBOARD_CANNOT_REMOVE_GENERAL` |
| M-11 | Unique | `unique(len=36)` |
| M-12 | 検出 | 複合 fixture は 9 種、`Number` / `ProbableWebSearch` は専用 fixture でのみ検出。**検出値はログに出ない** |
| M-13 | Load 各種 | `fileSize=64 cleanup=ok`、`textLen=16`、独自 UTI も往復 |
| M-14 | Seed And Cancel | `CLIPBOARD_CANCELLED`。`isPending: true` で進行中に届いたことを確認 |
| M-15 | 監視 | **フォアグラウンドの変更のみ配送**（下記 3.3） |
| M-16 | 監視の二重起動と置換 | busy 拒否が進行中を壊さない。Restart で通知が二重にならない |
| M-17 | Stop 後 | イベントが増えない |
| M-18 | Check Foreground Change | バックグラウンド変更を `changed=True` で検知 |
| M-19 | 画面遷移・再起動 | 例外なし。Manager は `Instance` 参照時に遅延生成、native 初期化は初回呼び出し時 |
| M-20 | ログ秘匿 | 本文・base64・検出値・pasteboard 名・パスがどのログにも出ない |
| M-22 | 数 MiB のメモリ | 下記 3.4 |
| M-24 | Busy: Load Item Twice | 別 sequence で表示。拒否は専用 dispatch |

### 3.2 合格以外

| 項目 | 扱い | 理由 |
|---|---|---|
| **M-4**（localOnly の他端末への転送） | **制限事項** | 端末2台が必要。native サンプルも同じ立場で、`ClipboardSampleView.swift:395-399` に `Whether localOnly actually suppresses transfer to nearby devices is not something this screen can show.` と明記されている。Unity 側の Copy Options セクションにも同じ但し書きを追記した |
| **M-21**（失効の失敗系） | **代替で確認** | fixture は常に未来の時刻を渡すため、過去日時での失敗はサンプルからは再現不可。`Err Copy Invalid URL` を代替とした |
| **M-23**（64 MiB 近傍） | 対象外 | 計画 7.4 のとおり別 artifact へ引き継ぎ |
| V-1 | 実施不可 | Editor 導線を撤回したため。実機では画面へ到達できることを確認済み |
| V-5 | 再現不可 | Start pending 中に `CreatePasteboard` が完了する状況は、Scope ボタンが観測中に無効化されるため通常操作で作れない。`targetScope` の対応は EditMode テストで固定済み |

### 3.3 実機で判明した iOS の挙動（記録）

| 事項 | 内容 |
|---|---|
| 変更通知の配送範囲 | **自アプリのフォアグラウンド変更では `ClipboardChanged` が届く**が、**他アプリがバックグラウンド中に行った変更は届かない**。復帰後に `CheckForegroundChange` を呼ぶ必要がある。`StartObserving` と `CheckForegroundChange` の併用が前提 |
| `LoadItem(Image)` のサイズ | PNG として**再エンコードされる**ため copy 時と一致しない（70 バイトの 1x1 PNG が 248 バイトになる）。バイト一致が必要なら `ReadData` を使う |
| 表示順の逆転 | 自アプリの copy では、`changedNotification` が copy の完了 callback より先に届くことがある。結果欄は 1 行のため `event.changed` が copy の完了行に上書きされる。sequence 番号で区別できる |
| 初回の一時停止 | 初回の `Copy Image Data` で `Hang detected: 1.07s`（デバッガ接続中）を観測。コールドスタート後の `Copy Plain Text` では再現せず、画像コーデックの初回利用に起因する可能性。実害は確認されていない |
| 破棄後の callback | `OnDestroy` の後に `CompleteStopObserving` が到着し、状態遷移が完了することを確認。例外なし |

### 3.4 M-22 のメモリ計測

fixture: 固定 seed の 1024x1024 ノイズ PNG、**3,694,822 バイト（3.52 MiB）**。V-4 の受入範囲（3〜5 MiB）内。

Instruments Allocations の Generation Analysis（1トレース内で連続マーク）。

| Generation | 対応 | Growth |
|---|---|---|
| A | baseline | 318.11 MiB（アプリ全体） |
| B | 1回目の Copy + ReadData 後 | 1.36 MiB |
| C | 10 秒後 | 0 Bytes |
| D | 2回目 | 33.58 KiB |
| E | 10 秒後 | 0 Bytes |
| F | 3回目 | 4.64 MiB |
| G | 10 秒後 | 768 Bytes |

判定: **リークなし**。

- 各回 4 MiB ずつ積み上がっていない（2回目は 33.58 KiB のみ = 既存ヒープの再利用）
- F の 4.64 MiB は最後の `byte[]`（3.52 MiB）が未回収なだけで、ペイロードの2重コピー（7.04 MiB）にはなっていない
- 待機中の増加はほぼ 0

Generation の Growth は生存分のみを示すため、**操作中の一時的なピークは含まない**。厳密なピーク値は未計測。

---

## 4. 未確認・残課題

| 項目 | 内容 |
|---|---|
| M-4 | 制限事項。端末2台での localOnly 検証 |
| M-23 | 別 artifact（native fixture の追加要否を含む） |
| xcframework のバージョン | 1.3.0 のままバイナリが2回差し替わっている。配布済みの場合、利用者が新旧を区別できない |
| native の cancelled ログ | `UnityIosClipboardManager.swift:333` が `CLIPBOARD_CANCELLED` を `Log.e` で出力。Unity Console には出ないため優先度は低い |
| Editor GUI 確認 | 導線撤回により S-1〜S-6 は実施不可 |

---

## 5. 実行確認

- 提示文:
  - 「このサンプル実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して終了 → review-implementation-sample-scene スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま終了
- ユーザー回答:
  - 未回答
