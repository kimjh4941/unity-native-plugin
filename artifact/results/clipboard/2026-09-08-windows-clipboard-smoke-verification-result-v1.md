# スモーク検証結果 v1（ネイティブ境界の初回実行）

## 基本情報

- 日付: 2026-09-08
- 機能名: clipboard / 対象プラットフォーム: Windows
- ブランチ: `feature/UNT-11`（コミット `b4bb9ef` 時点）
- ビルド種別: **Development Build**（`BuildOptions.Development`）
- Unity: 6000.4.2f1 / Mono / Windows 11 Home 10.0.26200
- 実行方法: 使い捨てスパイク（`WindowsClipboardSmokeProbe` + `WindowsClipboardSmokeBuild`）を
  1 シーンの Player としてビルドし、そのまま実行。**確認後に削除済み**
- ログ: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\unity-native-plugin\Player.log`

## なぜ実施したか

ネイティブ境界（`#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`）は、
**この時点まで一度も実行されたことがなかった。**

- 層 1（EditMode）と層 2a（PlayMode-in-Editor）は `UNITY_EDITOR` 定義下でしか動かず、当該分岐をコンパイルすらしない
- 層 0（Player ビルドゲート）は 2026-09-08 に導入したばかりで、**コンパイルの確認まで**
- 正規の確認手段であるサンプルアプリは、Windows Clipboard 版が未実装（`Runtime/UI/Windows/Clipboard/` が無い）

実機確認 M-1 〜 M-24 の前に「P/Invoke が根本的に動くのか」だけを確かめる目的で、
最小 5 項目のスパイクを実施した。

## 結果: 5 項目すべて成功

| # | 確認内容 | 結果 |
|---|---|---|
| S-1 | `Initialize` が成功するか（アパートメント判定・隠しウィンドウ・DLL 解決） | **成功**（`code=None`） |
| S-2 | `CopyPlainText` が成功するか | **成功**（`code=None`、length 26） |
| S-3 | `PastePlainText` が二段階読み出しで往復するか | **成功**（`roundTrip=True`、length 26、`IsEmpty=False`） |
| S-4 | `GetFormats` がリスト系読み出しを返すか | **成功**（`count=4`） |
| S-5 | **アプリが終了できるか**（quit ドレイン） | **成功**（プロセス終了コード 0） |

**エラー・警告・例外はログ全体で 0 件。**

### S-1 の詳細

```
[WindowsClipboardManager][EnsureStaApartment] apartment: MainSta
[SMOKE] S-1 initialize: success=True code=None message=-
```

2026-09-05 のスパイク（V-1）が測った `APTTYPE_MAINSTA` を、**製品コードの経路で再確認**した。
専用 STA スレッドと自前のメッセージポンプが不要という設計の前提が、実装後も成立している。

### S-5 の詳細

```
[WindowsClipboardManager][OnWantsToQuit] state: None      ← 1 回目。quit を保留して drain を開始
[WindowsClipboardManager][OnWantsToQuit] state: Resumed   ← 2 回目。drain が再開させた
[WindowsClipboardManager][OnDestroy]
PROCESS_EXIT=0
```

`DrainSession` 化（`d627d09`）した quit 経路が、実機で意図どおりに動いた。
**プロセスが実際に終了したこと**が確認の実体であり、ハングすればここで止まっていた。

## 判明したこと

### 成功した shutdown はログに何も残さない（観測性の指摘）

ログに `AdvanceDrain` / `FinishShutdownAttempt` / `ResumeQuit` の行が **1 つも無い**。
これは異常ではなく、次の設計の帰結である。

- `AdvanceDrain` は「未完了」のときだけ `attempt N not finished yet` を出す
- `FinishShutdownAttempt` は終端失敗のときだけエラーを出す
- `ResumeQuit` は `progress != Completed` のときだけエラーを出す

したがって「ログが無い」＝「初回試行で完了した」と読める。実際そう判断した。

**ただし、これは不在による証明である。** 実機確認の記録としては弱い。
shutdown の完了を 1 行（`Debug.Log` 相当）で残すことを検討する価値がある。
本スモークの範囲では変更していない。

### Development ビルドは作業ツリーの DLL を入れ替える（既知の再確認）

2026-09-05 のスパイク F-2 と同じ現象を再確認した。

| ファイル | 変化 |
|---|---|
| `unity-windows-native-toolkit.dll` / `.meta` | **削除された** |
| `unity-windows-native-toolkit-debug.dll` / `.meta` | 生成された |
| `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` | Unity が再シリアライズ |

いずれも `PreBuildProcessor` と Unity によるもので、コミット対象ではない。**すべて復元済み。**

## 本スモークで確認していないこと

| 範囲 | 理由 |
|---|---|
| 履歴 API（非同期 5 種）、遅延レンダリング、イベント（`ClipboardChanged` ほか） | スモークの対象外。M-1 〜 M-24 の担当 |
| HTML / 画像 / ファイル / カスタムフォーマット | 同上 |
| IL2CPP | 本スモークは Mono。`MonoPInvokeCallback` の実挙動は別途（V-7） |
| 他アプリとの相互運用（貼り付け先での実内容） | プロセス内で往復させただけ。層 3 の担当 |

**S-1 〜 S-5 が通ったことは「基盤が動く」ことしか意味しない。** 機能の検証ではない。

## 次

1. 実装レビュー v5（レビュー v4 の A 6 件を修正済み。A が 0 になったことは未確認）
2. `design-sample-scene` → `implement-sample-scene` で Windows Clipboard のサンプルを作る
   （**M-1 〜 M-24 はこれが無いと実施できない**）
3. 実機確認 M-1 〜 M-24
