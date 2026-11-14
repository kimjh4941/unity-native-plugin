# iOS Share UI 自動テスト計画 v3

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | `IosShareManager`（`Runtime/iOS/Share/IosShareManager.cs`）の `Share(IosShareContentPayload, Action<IosShareResult>)` と `ShareCompleted` イベント |
| 目的 | iOS の C ABI `2.0.0` への移行（[artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md)）の段 3-R。**1.x のまま記録**し、段 3 で Share を移した後に同じテストを流して、1.x と同じか、既知の差分かを確かめる |
| 実行 | UI の部分は `scripts/test_ios_ui.sh`（ハーネスの `share` のシナリオと `Tools~/IosUiTests/NtkUiTests/ShareUITests.swift`）。画面を出さない部分は `scripts/verify_unity_ios.sh` の iOS の Player のテスト（`Tests/PlayMode/IosSharePlayerTests.cs`） |
| 仕組みと書き方 | [UI の自動化の設計書 v4](../../../topics/ios-c-abi-2/designs/2026-11-14-ios-ui-automation-design-v4.md)。結果の行の書き方は Dialog の計画 v3（`artifact/ios/dialog/designs/2026-11-14-ios-dialog-ui-test-plan-v3.md`）の 1 章と同じ（null は `<null>`、空文字は `""`、全文一致、押した後 3 秒待って本数を数える） |
| 1.x の振る舞いの出典 | native-toolkit のタグ `1.13.0` の `ios/IosLibrary/IosLibrary/Share/IosShareManager.swift`（キャンセルは失敗でなく `completed == false`）と `Share/Domain/Model/ShareError.swift`（失敗の文言）、`ios/UnityIosPlugin/UnityIosPlugin/Share/` |
| `2.0.0` との差 | native-toolkit の `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 の SH-01（「前面でなければ `NOT_FOREGROUND`」「不正な項目を捨てず `INVALID_PARAMETER`」「エラーに入力の URL やパスを含まない」）と `Share.h`（コールバックはちょうど 1 回） |
| レビュー | [../reviews/2026-11-14-ios-share-ui-test-plan-review-v1.md](../reviews/2026-11-14-ios-share-ui-test-plan-review-v1.md)、[...-review-v2.md](../reviews/2026-11-14-ios-share-ui-test-plan-review-v2.md)（どちらもサブエージェントと Codex）。**レビュー v2 で 2 人とも A1 が 0 になり、止めた** |
| 移行前の基準 | 無い。1.x の列は予想で、実測が違えば記録の文書に書く（Dialog と同じ） |

**v2 からの変更（2026-11-14、レビュー v2 を受けて。止めた後の直し）:** SH-17 は「シートが出れば」にした（`2.0.0` では出ない）。SH-18 は背景に入った知らせを受けてから呼ぶ形にした。SH-16・17・23 の `2.0.0` の列に、C# の写し方で決まることを書いた。

**v1 からの変更（2026-11-14、レビュー v1 を受けて）:** SH-13 は呼び出しごとのコールバックとイベントを別の行で数える（A1）。1.x が黙って捨てる経路（未知の type、null）の項目を足した（SH-16・17、SH-23〜25）。不正な URL の 1.x の予想を直した（捨てずに失敗）。背景からの呼び出し（SH-18）を足した。`activity` の書き方を、Apple の活動だけそのまま書く形にした。直さなかった指摘は 6 章。

## 1. 共有のシートを押すテスト（UI のハーネス、`ShareUITests`）

**結果の行の形**: `[share@<ok|fail>] completed=<True|False> activity=<値>`。行は**呼び出しごとのコールバック**から書く（SH-13 だけは下の表のとおり、イベントも別の行で書く）。`activity` は選ばれた活動の識別子（`ActivityType`）で、**`com.apple.UIKit.activity.` で始まるものだけそのまま、ほかは `<other>`**（ほかのアプリの拡張の識別子を記録に残さない）、null は `<null>`。失敗のときは ` error=<ErrorMessage>` を足す。**文言の中のアプリの中の道は `<data>` に置き換える**（`Application.persistentDataPath` を置き換える。端末の container の道を記録に残さない）。シートの中の部品（ほかのアプリや連絡先）は読まず、押すのは「閉じる」と、シミュレーターでの「コピー」だけ。

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
| SH-9 | 有効でない URL だけ（`not a url`） | シートが出れば「閉じる」。3 秒後に結果の行を数える | `[share@fail] completed=False activity=<null> error=Invalid URL: not a url.`（1.x は捨てずに全体を失敗にする。`1.13.0` の `ShareRepositoryImpl.swift` の 44・76〜91 行。シートは出ない） | 同じく失敗でシートは出ない。**既知の差分**: `INVALID_URI` で、文言に入力の URL を含まない | 流す |
| SH-10 | 文字列と、有効でない URL | 同じ | SH-9 と同じ行（文字列があっても全体が失敗） | SH-9 と同じ | 流す |
| SH-11 | 無い画像のファイル（`<data>/missing.png`） | 同じ | 記録する（`ShareError.imageLoadFailed` で `[share@fail] … error=Failed to load image at path: <data>/missing.png.` の見込み） | **既知の差分の候補**: `IMAGE_LOAD_FAILED`。文言に道を含まない | 流す |
| SH-12 | 無いファイル（`<data>/missing.txt`） | 同じ | 記録する（`ShareError.fileNotFound` で `… error=File not found at path: <data>/missing.txt.` の見込み） | **既知の差分の候補**: `FILE_NOT_FOUND`。文言に道を含まない | 流す |
| SH-13 | `Share` を同じフレームで 2 回（文字列）。**行は、1 つ目のコールバックを `share1`、2 つ目を `share2`、`ShareCompleted` のイベントを `share-event` として別に書く** | 出ているシートを「閉じる」。3 秒後に、3 つの id の行をそれぞれ数え、**順でなく集合で**比べる（`Share.h` は複数の共有の順を約束しない） | `[share2@fail] … error=Failed to present the share sheet: The root view controller is not in a stable state to present the share sheet..`（`1.13.0` の `ShareSheetPresenter` は表示中なら 2 つ目を断る。文言のピリオドは 2 つ）が 1 本、`[share1@…]` は **0 本**（1.x の C# の `s_onShare` は後勝ちで、先に届いた失敗が 2 つ目のコールバックを取って消すので、閉じた結果はイベントにしか来ない）、`share-event` は 2 本（失敗と、閉じた結果） | **既知の差分の候補**: 呼び出しごとにちょうど 1 回（`share1` と `share2` が 1 本ずつ） | 流す |
| SH-14 | シートを出したまま、ホームに戻して 3 秒後に戻る | シートが残っていれば「閉じる」 | 記録する（残って、閉じれば `completed=False` の見込み） | 同じ | シミュレーターだけ |
| SH-15 | 3 秒後にネイティブの側で `Share` を呼ぶ行（Dialog の ID-17 と同じ）。呼んだ時点の状態を `[late@event] state=…` に出す | 行を押してすぐ Control Center を開き、5 秒後に閉じる。`state=foregroundInactive` を確かめ、シートが出ていれば閉じる | 記録する（1.x は前面で活性の画面だけを探すので、`ShareError.noRootViewController` で `… error=No root view controller available to present the share sheet.` が 1 回の見込み） | **既知の差分の候補**: 前面で非活性も受け付けて出す | シミュレーターだけ |

| SH-16 | 文字列と、未知の `type`（`IosShareItem` の `type = "unknown"`） | シートが出れば「閉じる」 | 記録する（1.x のパーサーは未知の type を黙って捨て、文字列だけでシートを出す見込み。閉じれば `[share@ok] completed=False activity=<null>`） | **既知の差分**: 捨てずに `INVALID_PARAMETER` の見込み（シートは出ない。SH-01、P2-15）。C# で先に断るかは Share の写し方の設計で決める | 流す |
| SH-17 | 文字列、除く活動に `[null, "com.apple.UIKit.activity.CopyToPasteboard"]` | **シートが出れば**、「コピー」が出ているかを確かめて「閉じる」。3 秒後に結果の行を数える | 記録する（1.x のパーサーは `as? [String]` に失敗して除く一覧をまるごと捨て、「コピー」が出る見込み。閉じれば `[share@ok] completed=False activity=<null>`） | **既知の差分**: 配列の NULL 要素を断り、シートは出ず、失敗が 1 回（`INVALID_PARAMETER` の見込み。C# で先に断るかは写し方の設計で決める） | シミュレーターだけ（シートの中を読む） |
| SH-18 | ハーネスが `UISceneDidEnterBackgroundNotification` を受けたら、`beginBackgroundTask` の中で `Share` を呼ぶ（`dispatch_after` の遅れは、背景に入るとアプリが止まり走らない見込み。レビュー v2 の R2-1）。呼んだ時点の状態を `[late@event] state=…` に出す | 行を押してからホームに戻り、5 秒後にハーネスを前に戻す。`state=background` を確かめ、シートが出ていれば「閉じる」。戻った後の状態も記録する | 記録する（1.x は前面で活性の画面を探せず、`No root view controller available to present the share sheet.` で 1 回の見込み。1.x は Task の中で画面を探すので、探すのが戻った後になればシートが出うる。そのときは結果をそのまま記録する） | **既知の差分**: `NOT_FOREGROUND`（`Share.h` の 98〜100 行） | シミュレーターだけ |

## 2. 画面を出さないテスト（iOS の Player、`IosSharePlayerTests`）

| ID | 呼ぶもの | 1.x の予想 | `2.0.0` の期待 |
|---|---|---|---|
| SH-20 | 項目なし（今の `ShareWithoutItemsFailsWithoutASheet`） | C# の確かめで `IsSuccess = false`、`"No shareable items were provided."`。シートは出ない | 同じ（C# の確かめを保つ） |
| SH-21 | `payload` が `null` | 同じ | 同じ |
| SH-22 | `ShareCompleted` イベントと、呼び出しごとのコールバックの順 | イベントが先、コールバックが後（`IosShareManager.cs` の説明）。どちらもちょうど 1 回 | 同じ |
| SH-23 | 未知の `type` の項目だけ | 記録する（ネイティブで捨てて項目が無くなり、`No shareable items were provided.` の見込み） | **既知の差分**: `INVALID_PARAMETER` の見込み。C# で先に断るかは写し方の設計で決める |
| SH-24 | 項目の配列が `[null]` | 記録する（C# の長さの確かめは通り、`IosShareJsonBuilder` が除き、ネイティブで `No shareable items were provided.` の見込み） | 記録して、写し方の設計で決める（`EMPTY_CONTENT` か、C# で断るか） |
| SH-25 | `value` が null の文字列の項目だけ | 記録する（パーサーが捨てて `No shareable items were provided.` の見込み） | **既知の差分**: `INVALID_PARAMETER` |

## 3. テストの共通の約束

- テストの名前は `ShareUITests/test_<ID>`（例: `test_SH1`）
- 予期しないアラートやシートが残れば失敗（`HarnessUITestCase`）
- 実機の区分は、「閉じる」だけのものは `sharesheet`（シートを開き、共有先を選ばずに閉じる）、入れ直しは `reinstall`。**SH-6・7・8・14・15・17・18 は `simulatorOnly`**
- 端末ではシートの中の部品を読まない（シミュレーターだけの ID は除く）。失敗の文に、シートの中のラベルを書かない
- 段 2 のパイロットのテスト `ShareUITests/test_dismiss` は SH-1 に置き換える

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| iPad の基点（popover） | iPad の実機もシミュレーターの回も無い。段 4 で iPad のシミュレーターがあれば足す（トピックの `README` の `6.6`） |
| 実在のアプリへの共有（メール、メッセージなど） | 私物の端末で、実在の相手やアプリに送りうる。シミュレーターの「コピー」だけで `completed=True` を見る |
| 画像の中身、共有された中身の見た目 | native-toolkit でも手で見る（HC7・HC8） |
| 外から閉じられたときの `CANCELED_BY_SYSTEM` | 外から閉じる操作が XCUITest に無い（Dialog と同じ） |

## 5. 1.x の記録

記録は別の文書に書く（Dialog と同じく、計画の版は上書きしない）。

## 6. レビュー v1 で直さなかったもの

| 指摘 | 区分 | 理由 | どこで扱うか |
|---|---|---|---|
| R-4（SH-7 の「コピーが無い」が、一覧が描かれる前に見ると偽って通る） | A2 | 実装で表に出る | 実装で native-toolkit の S12 と同じ待ち方（`UIActivityContentView` が出てから 2 秒）にし、同じ実行の SH-6 で同じ問い合わせがコピーを見つけることを対照にする |
| R-5（SH-6・SH-8 に要る SystemUI の部品が無い） | A2 | 実装で表に出る | native-toolkit の U-13（コピーは 1 件だけのときに押す）、ヘッダーの題の読み方、iOS 26 の閉じ方（`PopoverDismissRegion`）を写す。シミュレーターは iOS 18.3 に固定する（iOS 26 は段 4） |
| R-7（`persistentDataPath` の伏せ方、端末のログ） | C | 文言 | 実装で、行と `Debug.Log` の前に `<data>` へ置き換える。端末のログは記録に写さない（段 2 の決まり） |
| R-8（区分の文と SH-6） | C | 文言 | SH-6 は `simulatorOnly` にしてある。`DeviceRequirements.json` の文は実装で直す |

