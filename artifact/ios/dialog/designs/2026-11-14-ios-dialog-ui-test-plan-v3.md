# iOS Dialog UI 自動テスト計画 v3

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | `IosDialogManager`（`Runtime/iOS/Dialog/IosDialogManager.cs`）の 6 つの `Show*` と 6 つのイベント |
| 目的 | iOS の C ABI `2.0.0` への移行（[artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md)）の段 3-R。**1.x のまま実機で流して記録**し、段 3 で Dialog を移した後に同じテストを流して、1.x と同じか、既知の差分かを確かめる |
| 実行 | UI の部分は `scripts/test_ios_ui.sh`（UI のハーネスの `dialog` のシナリオと `Tools~/IosUiTests/NtkUiTests/DialogUITests.swift`）。画面を出さない部分は `scripts/verify_unity_ios.sh` の iOS の Player のテスト（`Tests/PlayMode/IosDialogPlayerTests.cs`） |
| 仕組み | [UI の自動化の設計書 v4](../../../topics/ios-c-abi-2/designs/2026-11-14-ios-ui-automation-design-v4.md)。ハーネスの行（`row.<id>.run`）を押すと Manager を呼び、イベントを結果の行（`[<id>@<状態>] <値>`）に書く。アラートは XCUITest が押す |
| `2.0.0` との差 | native-toolkit の `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 の DL-01〜DL-06（「今は 0 回だった場面でコールバックが来る」「前面でなければ `NOT_FOREGROUND`」「アクションシートの基点」「空のときに押せるかの名前の反転」） |
| レビュー | [../reviews/2026-11-14-ios-dialog-ui-test-plan-review-v1.md](../reviews/2026-11-14-ios-dialog-ui-test-plan-review-v1.md)、[...-review-v2.md](../reviews/2026-11-14-ios-dialog-ui-test-plan-review-v2.md)（どちらもサブエージェントと Codex）。**レビュー v2 で 2 人とも A1 が 0 になり、止めた** |
| 移行前の基準 | 無い。iOS の Dialog の UI テストは 1.x には無かった。1.x の列は予想で、実測が違えばこの列を直す（Android の Dialog の UI テスト計画 v2 と同じ） |

**v2 からの変更（2026-11-14、レビュー v2 を受けて。止めた後の直し）:** ID-14b は 1 つの行の中で 2 つを呼ぶ形にした（アラートの下の行は押せない）。ID-17 は遅れをネイティブの側で作り、呼んだ時点の状態を結果の行に出す（Control Center を開くと Unity が止まる）。ID-17 の 1.x の予想を、出荷物の `1.13.0` の行から直した。

**v1 からの変更（2026-11-14、レビュー v1 を受けて）:** 結果の行で null と空文字を書き分け、全文一致で比べ、各 ID の結果がちょうど 1 本であることを数える（A1、R-3）。1.x のキャンセルの値を null に直した（ID-10・13）。2 つ続けたときを 2 通りに分けた（ID-14a・14b）。ログインの ID はシミュレーターだけにした（R-4）。ログインの反転の真の側（ID-16）と、前面で非活性のときの受け付け（ID-17）を足した。直さなかった指摘は 6 章。

Android の Dialog の UI テスト計画 v2（`artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v2.md`）と違い、サンプルの画面を通すテストは置かない。iOS のサンプルの画面（UI Toolkit）は XCUITest から見えず、ハーネスが Manager を直に呼ぶ。サンプルの画面は手で見る（4 章）。

## 1. アラートを押すテスト（UI のハーネス、`DialogUITests`）

どのテストもハーネスを入れ直してから流す（`test_ios_ui.sh`）。押す文言はテストが決めた値で、結果の行にそのまま出してよい。

**結果の行の形**（v2。レビュー v1 の A1）: イベントの引数を `button=<値>`、`input=<値>`、`username=<値>` と書き、**null は `<null>`、空文字は `""`**、それ以外は値をそのまま書く（例: `input=<null>` と `input=""` は別の行）。**パスワードは中身を出さず、null なら `password=<null>`、そうでなければ `password-length=<n>`**。`isSuccess` が偽なら状態を `fail` にし、値に `error=<errorMessage>` を書く。XCUITest は結果の行を**全文一致**で比べ、押した後に 3 秒待ってから、**その行 id の結果の行がちょうど 1 本**であることを数える（`Dialog.h` の「the callback comes exactly once」。1.x で 0 回だった場面は表に書く。ID-14a・14b は表に書いた本数を数える）。

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
| ID-10 | 同じ | 何も打たずに `OK` が押せないことを見る（`isEnabled == false`）。`Cancel` を押す | `[text@ok] button=Cancel input=<null>`（1.x はキャンセルで null を返す。`UnityIosDialogManager.swift` の 233 行） | 同じ（C の `confirm_disabled_when_empty` に反転して渡る。確定でないときは NULL、`Dialog.h` の 167〜172 行） | 流す |
| ID-11 | `ShowTextInputDialog(..., enableConfirmWhenEmpty: true)` | 何も打たずに `OK` | `[text@ok] button=OK input=""`（空文字） | 同じ | 流す |
| ID-12 | `ShowLoginDialog("NTK", "login", "Username", "Password", "Login", "Cancel", enableLoginWhenEmpty: false)` | 利用者名に `user1`、パスワードに `pw1234` を打ち、`Login` | `[login@ok] button=Login username=user1 password-length=6` | 同じ | **シミュレーターだけ**（パスワードの保存の確認や AutoFill が窓を奪いうる。native-toolkit の U-9 の `credentials`。手で一度確かめるまで実機に載せない） |
| ID-13 | 同じ | 何も打たずに `Login` が押せないことを見る。`Cancel` | `[login@ok] button=Cancel username=<null> password=<null>`（`UnityIosDialogManager.swift` の 280 行） | 同じ | シミュレーターだけ（ID-12 と同じ） |
| ID-14a | `ShowConfirmDialog` を**同じフレームで** 2 回呼ぶ（ボタンは `OK1` と `OK2`） | 出ているアラートを上から押して閉じ、3 秒後に `confirm` の結果の行を数える | 記録する（1.x は最上位の画面の上に 2 つ目を出そうとするが、1 つ目の表示の途中なので UIKit に断られ、2 つ目は 0 回の見込み。レビュー v1 の R-2） | **既知の差分**: 両方出て 2 回（native-toolkit の第 1 部 P1-12、G-4 の実測） | 流す |
| ID-14b | 1 つの行の中で、1 つ目（`OK1`）を呼び、1 秒後に C# の側で 2 つ目（`OK2`）を呼ぶ（1 つ目が出ている間は、アラートの下にある行を押せない。アラートは Unity を止めない） | `OK2` が出るのを待ち、上から押して閉じ、3 秒後に結果の行を数える | 記録する（1.x は 1 つ目の上に重ねて出し、両方 1 回ずつの見込み） | 同じ | 流す |
| ID-15 | `ShowDialog` を出したまま、ホームに戻して 3 秒後に戻る | アラートが残っていれば `OK` を押す | 記録する（アラートが残り、押せば `[dialog@ok] button=OK` の見込み） | 同じ | シミュレーターだけ（native-toolkit の D17 と同じく、背景への移動は実機で押さない） |
| ID-16 | `ShowLoginDialog(..., enableLoginWhenEmpty: true)` | 何も打たずに `Login` | `[login@ok] button=Login username="" password-length=0` | 同じ（C の `login_disabled_when_empty` に反転して渡る。反転の真の側） | シミュレーターだけ（ID-12 と同じ） |
| ID-17 | 行を押すと、**ネイティブの側で** 3 秒後に `ShowDialog` を呼ぶ（重ね表示の `.mm` の `dispatch_after` から行のコールバックを呼ぶ。Control Center を開くと Unity は `applicationWillResignActive` で止まり、C# の遅れは進まない）。呼んだ時点の画面の `activationState` を結果の行に出す（`[late@event] state=foregroundInactive`） | 行を押してすぐ Control Center を開き、5 秒後に閉じる。`state=foregroundInactive` の行があることを確かめ、アラートがあれば `OK` を押す。結果の行を数える | ちょうど 1 回 `[dialog@fail] error=Failed to get root view controller`（`1.13.0` の `IosDialogManager.swift` の 147〜153 行は前面で活性の画面だけを探し、無ければ 92〜95 行で失敗を返す。ブリッジは `UnityIosDialogManager.swift` の 76〜80 行でそのまま渡す） | **既知の差分の候補**: `2.0.0` は前面で非活性も受け付けて出し、押せば `[dialog@ok] button=OK`（`DialogReceiver.swift` の 45 行） | シミュレーターだけ（Control Center を座標で開く。native-toolkit の `Springboard.openControlCenter` と同じ） |


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
- 実機で流すものは `DeviceRequirements.json` に `systemui` と `reinstall` で書く。**表の「シミュレーターだけ」の ID（12、13、15、16、17）は実機の一覧に載せない**。ランナーに「シミュレーターだけ」の印を足すことを実装の作業にする（レビュー v1 の R-5。native-toolkit の `SIMULATOR_ONLY_TESTS`）
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

## 6. レビュー v1 で直さなかったもの

| 指摘 | 区分 | 理由 | どこで扱うか |
|---|---|---|---|
| R-6 の残り（利用者名だけ入れたとき、入れてから消したときに押せるか） | B | 1.x と `2.0.0` で同じ見込みで、移行の差分を見つける項目ではない | Dialog の写し方の設計で、`2.0.0` の振る舞いとして足すかを決める |
| R-7（ID-22 の「null の選択肢」が、配列が null と要素が null の 2 通りに読める） | A2 | 実装で表に出る | 実装で 2 項目に分ける。要素が null の方は、1.x は `Internal error`、`2.0.0` は `INVALID_PARAMETER` の見込みで、既知の差分の候補 |
| R-8（パスワードをログに出さない） | C | 1.x のブリッジは平文で出す（`UnityIosDialogManagerBridge.m` の 223 行付近）。移行で直るもの | Dialog の写し方の設計で確かめ方を決める |
| R-9（アクションシートは `app.sheets`。iOS 26 系では Popover） | C | 実装の細部。既定のシミュレーター 18.3 と実機 18.7.2 では問題ない | 実装で、tearDown にシートの数も足す |
| R-10（`Show*` が戻った時点でイベントが 0 回） | C | 時機の契約は Dialog の写し方の設計で扱う | 同上 |

