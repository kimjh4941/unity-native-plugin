# iOS Dialog UI 自動テスト計画 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | `IosDialogManager`（`Runtime/iOS/Dialog/IosDialogManager.cs`）の 6 つの `Show*` と 6 つのイベント |
| 目的 | iOS の C ABI `2.0.0` への移行（[artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md)）の段 3-R。**1.x のまま実機で流して記録**し、段 3 で Dialog を移した後に同じテストを流して、1.x と同じか、既知の差分かを確かめる |
| 実行 | UI の部分は `scripts/test_ios_ui.sh`（UI のハーネスの `dialog` のシナリオと `Tools~/IosUiTests/NtkUiTests/DialogUITests.swift`）。画面を出さない部分は `scripts/verify_unity_ios.sh` の iOS の Player のテスト（`Tests/PlayMode/IosDialogPlayerTests.cs`） |
| 仕組み | [UI の自動化の設計書 v4](../../../topics/ios-c-abi-2/designs/2026-11-14-ios-ui-automation-design-v4.md)。ハーネスの行（`row.<id>.run`）を押すと Manager を呼び、イベントを結果の行（`[<id>@<状態>] <値>`）に書く。アラートは XCUITest が押す |
| `2.0.0` との差 | native-toolkit の `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 の DL-01〜DL-06（「今は 0 回だった場面でコールバックが来る」「前面でなければ `NOT_FOREGROUND`」「アクションシートの基点」「空のときに押せるかの名前の反転」） |
| 移行前の基準 | 無い。iOS の Dialog の UI テストは 1.x には無かった。1.x の列は予想で、実測が違えばこの列を直す（Android の Dialog の UI テスト計画 v2 と同じ） |

Android の Dialog の UI テスト計画 v2（`artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v2.md`）と違い、サンプルの画面を通すテストは置かない。iOS のサンプルの画面（UI Toolkit）は XCUITest から見えず、ハーネスが Manager を直に呼ぶ。サンプルの画面は手で見る（4 章）。

## 1. アラートを押すテスト（UI のハーネス、`DialogUITests`）

どのテストもハーネスを入れ直してから流す（`test_ios_ui.sh`）。押す文言はテストが決めた値で、結果の行にそのまま出してよい。**パスワードは結果の行に長さだけを出す**（`password-length=<n>`）。

| ID | ハーネスの行（呼ぶもの） | XCUITest がすること | 1.x の予想（結果の行） | `2.0.0` の期待 | 実機 |
|---|---|---|---|---|---|
| ID-1 | `ShowDialog("NTK", "basic", "OK")` | アラートの `OK` を押す | `[dialog@ok] button=OK` | 同じ | 流す |
| ID-2 | `ShowDialog("NTK", "custom", "了解")` | `了解` を押す | `[dialog@ok] button=了解`（日本語の文言がそのまま返る） | 同じ | 流す |
| ID-3 | `ShowConfirmDialog("NTK", "confirm", "OK", "Cancel")` | `OK` | `[confirm@ok] button=OK` | 同じ | 流す |
| ID-4 | 同じ | `Cancel` | `[confirm@ok] button=Cancel`（1.x はキャンセルも成功として押した文言を返す見込み） | 同じ | 流す |
| ID-5 | `ShowDestructiveDialog("NTK", "destructive", "Delete", "Cancel")` | `Delete` | `[destructive@ok] button=Delete` | 同じ | 流す |
| ID-6 | 同じ | `Cancel` | `[destructive@ok] button=Cancel` | 同じ | 流す |
| ID-7 | `ShowActionSheet("NTK", "sheet", ["A","B","C"], "Cancel")` | `B` | `[sheet@ok] button=B` | 同じ（`2.0.0` は添字を返し、包みが文言に戻す） | 流す |
| ID-8 | 同じ | `Cancel` | `[sheet@ok] button=Cancel` | 同じ | 流す |
| ID-9 | `ShowTextInputDialog("NTK", "text", "placeholder", "OK", "Cancel", enableConfirmWhenEmpty: false)` | テキスト欄に `ntk42` を打ち、`OK` | `[text@ok] button=OK input=ntk42` | 同じ | 流す |
| ID-10 | 同じ | 何も打たずに `OK` が押せないことを見る（`isEnabled == false`）。`Cancel` を押す | `[text@ok] button=Cancel input=`（空） | 同じ（C の `confirm_disabled_when_empty` に反転して渡る） | 流す |
| ID-11 | `ShowTextInputDialog(..., enableConfirmWhenEmpty: true)` | 何も打たずに `OK` | `[text@ok] button=OK input=` | 同じ | 流す |
| ID-12 | `ShowLoginDialog("NTK", "login", "Username", "Password", "Login", "Cancel", enableLoginWhenEmpty: false)` | 利用者名に `user1`、パスワードに `pw1234` を打ち、`Login` | `[login@ok] button=Login username=user1 password-length=6` | 同じ | 流す |
| ID-13 | 同じ | 何も打たずに `Login` が押せないことを見る。`Cancel` | `[login@ok] button=Cancel username= password-length=0` | 同じ | 流す |
| ID-14 | `ShowConfirmDialog` を 2 回続けて呼ぶ（1 回目が出ている間に 2 回目） | 出ているアラートの数を数え、`OK` を押す | 記録する（UIKit は表示中に次を出せない。1.x が 2 回目を黙って捨てるか、失敗を返すかを記録） | 1.x と同じか、設計書で決めた差分 | 流す |
| ID-15 | `ShowDialog` を出したまま、ホームに戻して 3 秒後に戻る | アラートが残っていれば `OK` を押す | 記録する（アラートが残り、押せば `[dialog@ok] button=OK` の見込み） | 同じ | シミュレーターだけ（native-toolkit の D17 と同じく、背景への移動は実機で押さない） |

ハーネスの結果の行の形は、`Manager` のイベントの引数をそのまま写す: `button=<buttonText>`、`input=<inputText>`、`username=<username>`、`password-length=<長さ>`。`isSuccess` が偽なら状態を `fail` にし、値に `error=<errorMessage>` を書く。

## 2. 画面を出さないテスト（iOS の Player、`IosDialogPlayerTests`）

段 2 の (A) のテストに足す。1.x の引数の確かめは C# にあり、イベントを同じ呼び出しの中で返す（`IosDialogManager.cs` の 272〜282 行ほか）。

| ID | 呼ぶもの | 1.x の予想 | `2.0.0` の期待 |
|---|---|---|---|
| ID-20 | 6 つの `Show*` を、空の `title` で呼ぶ | 各イベントが同じ呼び出しの中で `(null…, false, "Title cannot be null or empty.")`。アラートは出ない | 同じ（C# の確かめを保つ） |
| ID-21 | 6 つの `Show*` を、空の `message` で呼ぶ | 同じく `"Message cannot be null or empty."` | 同じ |
| ID-22 | `ShowActionSheet` を、空の選択肢と `null` の選択肢で呼ぶ | `"Options cannot be null or empty."`（今の `ActionSheetWithoutOptionsIsRejected` を広げる） | 同じ |

## 3. テストの共通の約束

- テストの名前は `DialogUITests/test_<ID>`（例: `test_ID3`）と `IosDialogPlayerTests.<ID の意味の名前>`
- 予期しないアラートが出たら失敗（`HarnessUITestCase` の見張り）。テストの終わりにアラートが残っていても失敗
- 実機で流すものは `DeviceRequirements.json` に `systemui` と `reinstall` で書く
- 1.x の記録は、全部の ID を 1 回流した結果の行を、この文書の 5 章に写す

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| iPad のアクションシートの基点（DL-04、P1-13） | iPad の実機もシミュレーターの回も無い。`2.0.0` の移行の後に、iPad のシミュレーターで足すかを Dialog の写し方の設計で決める |
| 外から閉じたときの `CANCELED_BY_SYSTEM`（DL-01〜06） | アラートを外から閉じる操作が XCUITest に無い（native-toolkit の D20 は座標の操作で、シミュレーターだけ）。1.x はこの場面でコールバックが来ない（0 回）。移行の設計で扱う |
| 前面でないときの `NOT_FOREGROUND` | アプリが背景にある間に、ハーネスの行を押せない |
| サンプルの画面（`IosDialogManagerExampleController`） | XCUITest から見えない。移行の後に手で一度見る |

## 5. 1.x の記録

（段 3-R の実機の回で埋める。実機は利用者の許可を得てから）
