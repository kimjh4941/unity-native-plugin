# iOS Share UI 自動テスト計画 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | `IosShareManager`（`Runtime/iOS/Share/IosShareManager.cs`）の `Share(IosShareContentPayload, Action<IosShareResult>)` と `ShareCompleted` イベント |
| 目的 | iOS の C ABI `2.0.0` への移行（[artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md)）の段 3-R。**1.x のまま記録**し、段 3 で Share を移した後に同じテストを流して、1.x と同じか、既知の差分かを確かめる |
| 実行 | UI の部分は `scripts/test_ios_ui.sh`（ハーネスの `share` のシナリオと `Tools~/IosUiTests/NtkUiTests/ShareUITests.swift`）。画面を出さない部分は `scripts/verify_unity_ios.sh` の iOS の Player のテスト（`Tests/PlayMode/IosSharePlayerTests.cs`） |
| 仕組みと書き方 | [UI の自動化の設計書 v4](../../../topics/ios-c-abi-2/designs/2026-11-14-ios-ui-automation-design-v4.md)。結果の行の書き方は Dialog の計画 v3（`artifact/ios/dialog/designs/2026-11-14-ios-dialog-ui-test-plan-v3.md`）の 1 章と同じ（null は `<null>`、空文字は `""`、全文一致、押した後 3 秒待って本数を数える） |
| 1.x の振る舞いの出典 | native-toolkit のタグ `1.13.0` の `ios/IosLibrary/IosLibrary/Share/IosShareManager.swift`（キャンセルは失敗でなく `completed == false`）と `Share/Domain/Model/ShareError.swift`（失敗の文言）、`ios/UnityIosPlugin/UnityIosPlugin/Share/` |
| `2.0.0` との差 | native-toolkit の `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 の SH-01（「前面でなければ `NOT_FOREGROUND`」「不正な項目を捨てず `INVALID_PARAMETER`」「エラーに入力の URL やパスを含まない」）と `Share.h`（コールバックはちょうど 1 回） |
| 移行前の基準 | 無い。1.x の列は予想で、実測が違えば記録の文書に書く（Dialog と同じ） |

## 1. 共有のシートを押すテスト（UI のハーネス、`ShareUITests`）

**結果の行の形**: `[share@<ok|fail>] completed=<True|False> activity=<値>`。`activity` は選ばれた活動の識別子（`ActivityType`）で、null は `<null>`。失敗のときは ` error=<ErrorMessage>` を足す。**文言の中のアプリの中の道は `<data>` に置き換える**（`Application.persistentDataPath` を置き換える。端末の container の道を記録に残さない）。シートの中の部品（ほかのアプリや連絡先）は読まず、押すのは「閉じる」と、シミュレーターでの「コピー」だけ。

| ID | ハーネスの行（呼ぶもの） | XCUITest がすること | 1.x の予想（結果の行） | `2.0.0` の期待 | 実機 |
|---|---|---|---|---|---|
| SH-1 | 文字列 1 つ | シートが出たのを見て「閉じる」 | `[share@ok] completed=False activity=<null>` | 同じ | 流す |
| SH-2 | URL 1 つ（`https://example.com/ntk`） | 同じ | 同じ | 同じ | 流す |
| SH-3 | 文字列と URL の 2 つ | 同じ | 同じ | 同じ | 流す |
| SH-4 | 画像のファイル 1 つ（ハーネスが `persistentDataPath` に小さな PNG を書いてから渡す） | 同じ | 同じ | 同じ | 流す |
| SH-5 | ファイル 1 つ（ハーネスが書いたテキストのファイル） | 同じ | 同じ | 同じ | 流す |
| SH-6 | 文字列 1 つ | 「コピー」を押す | `[share@ok] completed=True activity=com.apple.UIKit.activity.CopyToPasteboard` | 同じ | **シミュレーターだけ**（一般のクリップボードに書く） |
| SH-7 | 文字列 1 つ、除く活動に `com.apple.UIKit.activity.CopyToPasteboard` | シートに「コピー」が**無い**ことを確かめ（アンカーとして「閉じる」が出たのを見てから）、「閉じる」 | `[share@ok] completed=False activity=<null>` | 同じ | シミュレーターだけ（シートの中を読む） |
| SH-8 | 文字列 1 つ、`previewTitle = "NTK Preview"` | シートの上に `NTK Preview` が出ていることを確かめ、「閉じる」 | 同じく `completed=False` | 同じ | シミュレーターだけ（同上） |
| SH-9 | 有効でない URL だけ（`not a url`） | シートが出れば「閉じる」。3 秒後に結果の行を数える | 記録する（1.x は不正な項目を捨てるので、項目が無くなり `[share@fail] completed=False activity=<null> error=No shareable items were provided.` の見込み） | **既知の差分の候補**: `INVALID_PARAMETER` で、文言に入力の URL を含まない | 流す |
| SH-10 | 文字列と、有効でない URL | 同じ | 記録する（1.x は有効でない方を捨てて、文字列だけでシートを出す見込み。閉じれば `completed=False`） | **既知の差分の候補**: 捨てずに `INVALID_PARAMETER`（シートは出ない） | 流す |
| SH-11 | 無い画像のファイル（`<data>/missing.png`） | 同じ | 記録する（`ShareError.imageLoadFailed` で `[share@fail] … error=Failed to load image at path: <data>/missing.png.` の見込み） | **既知の差分の候補**: `IMAGE_LOAD_FAILED`。文言に道を含まない | 流す |
| SH-12 | 無いファイル（`<data>/missing.txt`） | 同じ | 記録する（`ShareError.fileNotFound` で `… error=File not found at path: <data>/missing.txt.` の見込み） | **既知の差分の候補**: `FILE_NOT_FOUND`。文言に道を含まない | 流す |
| SH-13 | `Share` を同じフレームで 2 回（文字列） | 出ているシートを「閉じる」。もう 1 つ出ていれば、それも閉じる。3 秒後に数える | 記録する（Dialog の ID-14a では 1.x は 2 つとも出した。シートでも同じかを見る） | 1.x と同じか、Share の写し方の設計で決めた差分 | 流す |
| SH-14 | シートを出したまま、ホームに戻して 3 秒後に戻る | シートが残っていれば「閉じる」 | 記録する（残って、閉じれば `completed=False` の見込み） | 同じ | シミュレーターだけ |
| SH-15 | 3 秒後にネイティブの側で `Share` を呼ぶ行（Dialog の ID-17 と同じ）。呼んだ時点の状態を `[late@event] state=…` に出す | 行を押してすぐ Control Center を開き、5 秒後に閉じる。`state=foregroundInactive` を確かめ、シートが出ていれば閉じる | 記録する（1.x は前面で活性の画面だけを探すので、`ShareError.noRootViewController` で `… error=No root view controller available to present the share sheet.` が 1 回の見込み） | **既知の差分の候補**: 前面で非活性も受け付けて出す | シミュレーターだけ |

## 2. 画面を出さないテスト（iOS の Player、`IosSharePlayerTests`）

| ID | 呼ぶもの | 1.x の予想 | `2.0.0` の期待 |
|---|---|---|---|
| SH-20 | 項目なし（今の `ShareWithoutItemsFailsWithoutASheet`） | C# の確かめで `IsSuccess = false`、`"No shareable items were provided."`。シートは出ない | 同じ（C# の確かめを保つ） |
| SH-21 | `payload` が `null` | 同じ | 同じ |
| SH-22 | `ShareCompleted` イベントと、呼び出しごとのコールバックの順 | イベントが先、コールバックが後（`IosShareManager.cs` の説明）。どちらもちょうど 1 回 | 同じ |

## 3. テストの共通の約束

- テストの名前は `ShareUITests/test_<ID>`（例: `test_SH1`）
- 予期しないアラートやシートが残れば失敗（`HarnessUITestCase`）
- 実機の区分は、「閉じる」だけのものは `sharesheet`（シートを開き、共有先を選ばずに閉じる）、入れ直しは `reinstall`。**SH-6・7・8・14・15 は `simulatorOnly`**
- 端末ではシートの中の部品を読まない（シミュレーターだけの ID は除く）。失敗の文に、シートの中のラベルを書かない
- 段 2 のパイロットのテスト `ShareUITests/test_dismiss` は SH-1 に置き換える

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| iPad の基点（popover） | iPad の実機もシミュレーターの回も無い。段 4 で iPad のシミュレーターがあれば足す（README 6.6） |
| 実在のアプリへの共有（メール、メッセージなど） | 私物の端末で、実在の相手やアプリに送りうる。シミュレーターの「コピー」だけで `completed=True` を見る |
| 画像の中身、共有された中身の見た目 | native-toolkit でも手で見る（HC7・HC8） |
| 外から閉じられたときの `CANCELED_BY_SYSTEM` | 外から閉じる操作が XCUITest に無い（Dialog と同じ） |

## 5. 1.x の記録

記録は別の文書に書く（Dialog と同じく、計画の版は上書きしない）。
