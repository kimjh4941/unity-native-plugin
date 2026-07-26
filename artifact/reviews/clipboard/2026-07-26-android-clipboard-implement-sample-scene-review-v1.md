# Android Clipboard サンプルシーン実装レビュー v1

## レビュー概要

- 対象実装結果: `artifact/results/clipboard/2026-07-26-android-clipboard-implement-sample-scene-result-v1.md`
- 対象計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v3.md`
- 対象プラットフォーム: Android
- ブランチ: `feature/UNT-8`
- レビュー日: 2026-07-26

`AndroidClipboardManagerExampleController`、UXML / USS、TopMenu 導線、Navigator 導線、EditMode wiring test を中心に確認した。全21ボタンの配置、Controller 側の `root.Q<Button>` / `clicked +=` / `clicked -=`、同期 API と非同期 event の表示経路、ログ秘匿のための `SetResult(logMessage: false)` は計画と概ね整合している。

## 重大な問題（high）

なし。

## 改善提案（medium）

### 1. `PasteCodeButton` が URI クリップを「コード貼り付け成功」と表示する可能性がある

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Android/Clipboard/AndroidClipboardManagerExampleController.cs:585`
- 関連計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-sample-scene-design-v3.md:755`

計画 7.2 では、`Copy Screenshot` 後に `Paste Code From Clipboard` を押すと `Info: Clipboard has no text item` になることを期待している。一方、実装の `ExtractFirstText` は `item.Text` が空の場合に `item.CoercedText` を返す。Android の URI clip は `coerceToText` 相当の経路で URI 文字列に変換される可能性があるため、スクリーンショットや URI の clip に対して `Success: Pasted code: content://...` のように表示され、計画上の「テキスト以外の貼り付け」確認を満たせない恐れがある。

ゲーム内コード入力欄の疑似再現として「テキストのみ」を示すなら、`PasteCodeButton` は `item.Text` のみを採用し、`CoercedText` は診断用の `Read Clipboard` 表示に留めるのが安全。HTML の plain text fallback も貼り付け対象にしたい場合は、計画の期待値側を「URI は `CoercedText` になりうる」前提で更新し、実機確認項目もそれに合わせる必要がある。

## 軽微な指摘（low）

### 1. `CaptureAndCopyScreenshot` の先頭ログがコーディングルールより弱い

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Android/Clipboard/AndroidClipboardManagerExampleController.cs:352`
- 関連ルール: `agent-rules/coding-rules/csharp.md:7`

`OnCopyScreenshotClicked` ではログが出ているが、実際に `WaitForEndOfFrame`、`ScreenCapture.CaptureScreenshotAsTexture()`、ファイル書き込み、`CreateContentUri` を行うコルーチン本体には開始ログがない。実害は小さいが、スクリーンショット経路は今回の中では比較的重い処理なので、`CaptureAndCopyScreenshot` 冒頭にも状態ログを置くと `Debug.Log` 方針と調査しやすさにより合う。

## 計画整合性チェック

- 全21ボタン（操作ボタン20個 + `HomeButton`）は UXML と Controller の両方に存在する。
- TopMenu の `ClipboardFeatureButton` 追加、Android / Editor のみの導線、`NativeToolkitSampleNavigator.ShowAndroidClipboard()` 追加は計画どおり。
- `Read` / `HasClip` / `GetDescription` は event を介さず同期戻り値を表示している。
- `StartObserving` は成功断定ではなく `Info:` 表示になっている。
- `OnEnable` / `OnDisable` の event 購読解除、`OnDisable` の `StopObserving()` 呼び出しは計画どおり。
- `CopyUri` / `CopyScreenshot` の `content://` 生成は `Application.persistentDataPath` と同梱 FileProvider authority の前提に沿っている。ただし計画どおり実機確認は未解消。
- `PasteCodeButton` の `CoercedText` 利用は計画 6.3 のコード例には沿っているが、計画 7.2 の「テキスト以外の貼り付け」期待と衝突しうる。

## プロジェクトルール適合チェック

- Manager + Bridge パターン自体への追加変更はなく、サンプル Controller は Manager の公開 API / event にのみ依存している。
- `#if UNITY_ANDROID && !UNITY_EDITOR` により実機専用 API 呼び出しが分離されている。
- `Read` / `GetDescription` / `PasteCode` の本文入り表示は `SetResult(logMessage: false)` でログ出力を抑制しており、Clipboard のログ秘匿方針に合っている。
- UI 文言・ログ・コメントは英語で、絵文字は使われていない。
- `.meta` ファイルは実装結果では Unity 自動生成と説明されている。`common.md` の「AI エージェントは作成しない」ルールとの整合は、生成経路の扱いに依存するため、最終コミット前に含める `.meta` を確認すること。

## 総合評価

修正推奨。

サンプルシーンとしての構成と配線は十分に整っており、静的チェック対象の範囲では大きな欠落は見当たらない。ただし `PasteCodeButton` の `CoercedText` fallback は、Game Use Cases の「コード入力欄」と「URIのみ clip は no text」の説明を曖昧にするため、実機確認前に挙動を明確化しておくのが望ましい。
