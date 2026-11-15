# iOS Dialog の C ABI `2.0.0` への写し方の設計 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 親 | [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（Dialog → Share → Clipboard → Notification の 1 つ目） |
| 目的 | `IosDialogManager` を、3-S の仮の失敗の経路から native-toolkit の C ABI `2.0.0`（`Dialog.h`）の呼び出しに替える。公開の API（6 つの `Show*` と 6 つのイベント）は変えない |
| 一次ソース | パッケージの `Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Dialog.h`。native-toolkit の `ios/IosLibraryCApi/IosLibraryCApi/Dialog/{DialogEntry,DialogReceiver,DialogMapping}.swift`、`ios/IosLibrary/IosLibrary/Dialog/Presentation/AlertDialogPresenter.swift`、`Common/Presentation/{ForegroundLocator,PresentationProcedure}.swift`、設計 `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` の 8.3（DL-01〜DL-06） |
| 1.x の記録 | [../results/2026-11-14-ios-dialog-1x-record-result-v1.md](../results/2026-11-14-ios-dialog-1x-record-result-v1.md)（UI の ID 1〜17、Player の ID 20〜22。実機あり） |
| 手本 | Android の Dialog の包み（`Runtime/Android/Dialog/AndroidDialogCApi.cs`、`AndroidDialogManager.cs`） |

## 1. C ABI の要点（一次ソースから）

| # | 事実 | 出典 |
|---|---|---|
| F-1 | 要求の文字列と選択肢の配列は、呼び出しの中で Swift の `String` に写される。呼び出しが返った直後に解放してよい（受け付けでも拒みでも） | `DialogReceiver.swift:26-30`、`DialogMapping.swift:24-77`、`CApiString.swift:37-59` |
| F-2 | 入口の拒み（コールバックは来ない。`release` は呼んだスレッドで済んでいる）: NULL のコールバック、NULL の要求、`struct_size` の誤り、0 でない `reserved*` → `INVALID_PARAMETER`。`options` が NULL で数が 1 以上、**要素に NULL**、不正な UTF-8 → `INVALID_PARAMETER`。`option_count == 0` は受け付ける（取り消しだけのシート）。空文字は空として受け付ける | `DialogReceiver.swift:22-24`、`StructReader.swift:36-119`、`CApiString.swift:43-67` |
| F-3 | NULL のボタン文言は既定（"OK"・"Cancel"・"Delete"・"Login"）になり、`button_text` は**表示した文言**（既定を埋めた後）を返す | `DialogMapping.swift:28-75`、`AlertDialogPresenter.swift:388-391` |
| F-4 | 結果: アラートは POSITIVE。確認と破壊の取り消しは NEGATIVE と取り消しの文言。シートの選択肢は OPTION、`option_index`、`button_text` は選択肢の文字列。シートの取り消し（と外側のタップ）は NEGATIVE と取り消しの文言。テキスト入力の確定は `_text`（空なら `""`）、取り消しは NULL。ログインの確定は 2 つとも非 NULL（空なら `""`）、取り消しは 2 つとも NULL | `DialogMapping.swift:84-114`、`DialogAnswer.swift:14-28`、`AlertDialogPresenter.swift:467-470` |
| F-5 | 完了のエラー: `NOT_FOREGROUND`（`foregroundActive` か `foregroundInactive` の scene に key window と root が無い）、`SHOW_FAILED`（落ち着かない、UIKit が断った）、`CANCELED_BY_SYSTEM`（表示の後にボタン無しで消えた）、`CANCELED`（`ntk_dialog_cancel` だけ）。`NOT_INITIALIZED` と `UNKNOWN` は来ない。`system_code` は iOS ではいつも 0。エラーのとき `result` は NULL | `ForegroundLocator.swift:55-116`、`PresentationProcedure.swift:117-247`、`ReleaseWatch.swift:42-46`、part2:730-732 |
| F-6 | 主スレッドで呼べば、完了は呼び出しの中では来ず、後の主スレッドの回で来る。`release` は完了の後に主スレッドで 1 回。`release` は NULL でよい。`user_data` はそのまま返る（読まれない） | `Dialog.h:26-29`、`MainQueue.swift:12-18`、`Registry.swift:189-277` |
| F-7 | 続けて呼ぶと、待ち行列にせず、前のものの上に出す（2 つとも出る）。遷移の途中なら 1 回待って出し直し、なお落ち着かなければ `SHOW_FAILED` | `IosDialogManager.swift:141-145`、`PresentationProcedure.swift:189-214` |
| F-8 | iPad のシートは、最上位の画面の中央を基点にした popover（基点を渡す口は無い） | `PresentationProcedure.swift:303-317`、part2:332 |
| F-9 | 初期化は要らない（`NOT_INITIALIZED` は返らない）。Info.plist のキーも要らない | part2:730、`Dialog.h:5-7` |

## 2. 決めること

| # | 決定 | 理由 |
|---|---|---|
| D-1 | **`user_data` は種類（`Kind`、1〜6）**。`release` は NULL。呼び出しごとの状態は持たない | 1.x の公開の API は種類ごとのイベントだけで、呼び出しごとの受け手が無い（Android の J-D1 と同じ）。同じ種類を続けて呼んでも、各完了がその種類のイベントを 1 回出せば 1.x と同じ（ID-14a・14b） |
| D-2 | 文字列は UTF-8 で `Marshal.AllocHGlobal` に写し、呼び出しが返った直後に解放する（受け付けでも拒みでも） | F-1 |
| D-3 | **ボタン文言と placeholder は、今の C# の既定を埋めてから渡す**（NULL を渡さない）。題と本文は今の C# の確かめで空を断っているので、いつも非 NULL | 1.x と同じ文言を出し、ライブラリの既定に頼らない。テキスト入力の placeholder の既定は `""`（1.x と同じ） |
| D-4 | `ShowActionSheet` の C# の確かめに **「選択肢に null を含む」**（`"Options cannot contain null."`、呼び出しの中で 1 回）を足す。順は今の 3 つ（題・本文・選択肢が空）の後 | F-2 で C ABI が `INVALID_PARAMETER` で拒むものを、1.x の確かめと同じ形で先に断る。1.x はネイティブに届いて `Internal error` の見込み（計画 v3 の R-7）。**既知の差分** |
| D-5 | `ShowTextInputDialog` の `enableConfirmWhenEmpty` は `confirm_disabled_when_empty = !enableConfirmWhenEmpty`、`ShowLoginDialog` の `enableLoginWhenEmpty` は `login_disabled_when_empty = !enableLoginWhenEmpty` | DL-05（名前の反転）。既定（false）は「空なら押せない」で 1.x と同じ（ID-10・13） |
| D-6 | **入口の失敗は、呼び出しの中でイベントを 1 回出す**: (a) C ABI が使えない（`IosNativeToolkitCApi.IsAvailable` が false。Editor もここ）→ `"Failed to <1.x の関数名>: the native library is not available."`、(b) C ABI が拒んだ（`NONE` 以外）→ `"Failed to <関数名>: the request was rejected (<名前>)."` と `Debug.LogError`（C# が誤った要求を組んだ印）。値はすべて null、成否は false | 1.x の例外の経路（`catch` でイベントを呼び出しの中で出す）と同じ時点。D-4 の後では (b) は起きない見込み |
| D-7 | **完了は主スレッドのコールバックで結果を読み、`ntk_dialog_result_free` で解放してから、`UnityMainThreadDispatcher` で次の `Update` にイベントを出す** | 1.x の時点（F-14: 次の `Update`）と同じ。結果の handle は受け取った回の中で解放する |
| D-8 | 完了の写し方: NONE → `(button_text, true, null)`。テキスト入力は `(button_text, text, true, null)`（取り消しは text が null）。ログインは `(button_text, username, password, true, null)`（取り消しは 2 つとも null）。シートは選択肢でも取り消しでも `button_text` | F-4。1.x の記録と同じ値（ID 1〜13、16）。ボタンの種類（`ntk_dialog_result_button`）と添字は読まない（公開の API に無い） |
| D-9 | 完了のエラーの写し方: 値はすべて null、成否は false、文言は `"Failed to <関数名>: <理由>"`。理由は `NOT_FOREGROUND` → `the app is not in the foreground.`、`SHOW_FAILED` → `the dialog could not be shown.`、`CANCELED_BY_SYSTEM` → `the dialog was closed by the system.`、`CANCELED` → `the dialog was canceled.`、ほか → `<名前>.` | Android の文言の形（`CompletionFailureMessage`）に合わせる。1.x の `Failed to get root view controller` は消える（**既知の差分**） |
| D-10 | 取り消し（`ntk_dialog_cancel`）は使わない。`out_request_id` は NULL | 1.x の公開の API に取り消しが無い。Manager の破棄でも 1.x は何もしなかった |
| D-11 | 宣言と変換は新しい `Runtime/iOS/Dialog/IosDialogCApi.cs`（`#if UNITY_IOS \|\| UNITY_EDITOR`）。`extern` は `!UNITY_EDITOR` の**外**（3-S の決まり）。結果の読み取りの呼び出しとコールバックの本体は `UNITY_IOS && !UNITY_EDITOR` | 3-S 設計 1.3。EditMode で照合と変換を確かめる |
| D-12 | 宣言する関数は使うものだけ: 6 つの `ntk_dialog_show_*_async`、`ntk_dialog_result_button_text`・`_text`・`_username`・`_password`・`_free` | `IosCApiDeclarationTests` の期待の集合に足す（11 個 + `ntk_version`） |
| D-13 | 公開の delegate 型（`DialogCallback` など 6 つ）は残す | 3-S と同じ。消すかは段 4（版を上げる時の移行の案内）で決める |
| D-14 | iPad のシートはこの段で確かめない（iPad の試験の環境が無い）。基点は画面の中央（F-8）を**既知の差分**として書く | 計画 v3 の 70 行（設計で決める）。1.x の基点は root の view で `sourceRect` 無し |

## 3. 振る舞いの対応（1.x の記録と `2.0.0` の見込み）

| 場面 | 1.x（記録） | `2.0.0`（この設計） | 区分 |
|---|---|---|---|
| ID 1〜13、14a、14b、15、16 | 記録の行 | 同じ行（F-3・F-4・F-7、D-3・D-8） | 同じ |
| ID-17（前面で非活性のときの呼び出し） | `[dialog@fail] button=<null> error=Failed to get root view controller` が 1 回、出ない | 出て、押せば `[dialog@ok] button=OK`（F-5: `foregroundInactive` は前面） | 既知の差分（DL-01） |
| 外から消された（表示の後にボタン無し） | 0 回 | 失敗が 1 回 `the dialog was closed by the system.`（D-9） | 既知の差分（DL-02）。UI のテストは無い（作る手が無い） |
| 背景からの呼び出し | 失敗（`Failed to get root view controller`） | 失敗 `the app is not in the foreground.` | 既知の差分（文言） |
| 選択肢に null の要素 | ネイティブに届く（`Internal error` の見込み） | 呼び出しの中で `"Options cannot contain null."`（D-4） | 既知の差分 |
| ID 20〜22 | 呼び出しの中で 1 回 | 同じ（C# の確かめは変えない） | 同じ |
| iPad のシートの基点 | root の view、`sourceRect` 無し | 画面の中央（F-8） | 既知の差分（確かめない。D-14） |

## 4. 変えるもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Dialog/IosDialogCApi.cs`（新） | `Kind`、`ntk_dialog_error` の値、6 つの要求の構造体（`[StructLayout(Sequential, Pack = 8)]`、`StructSize` は `Marshal.SizeOf`）、`ResultCallback` の delegate、D-12 の `extern`、`MethodName`・`ErrorName`・`EntryFailureMessage`・`CompletionFailureMessage`・`NativeUnavailableMessage`、`OptionsContainNull` の確かめ、`Build*`（D-3・D-5）、答え（`Answer`）からイベントの引数への変換（D-8・D-9）。文字列の確保は呼び出し側の `List<IntPtr>` に集めて `finally` で解放 |
| `Runtime/iOS/Dialog/IosDialogManager.cs` | 3-S の仮の経路（`UnavailableMessage`）を消す。各 `Show*` は今の C# の確かめ（と D-4）の後、D-6 → 要求を組んで呼ぶ → 拒みなら D-6 (b)。静的な `ResultCallback`（`[MonoPInvokeCallback]`、例外を外に出さない）が結果を読み（D-7）、`UnityMainThreadDispatcher` で種類のイベントを出す |
| `Tests/Runtime`（EditMode） | 構造体の大きさ（alert 32、confirm 40、destructive 40、action sheet 48、text input 56、login 64。`Dialog.h` の `pack(8)` の並びから）、D-3 の既定、D-5 の反転、D-4 の確かめ、D-8・D-9 の変換と文言、`IosCApiDeclarationTests` の期待の集合 |
| Player のテスト | `IosPlayerBuildSetup` に `NTK_IOS_CABI_2_DIALOG` を足す（`UnavailableUntilMoved` は組まれなくなる）。`IosDialogPlayerTests` に ID-22b（選択肢に null の要素、呼び出しの中で 1 回、1 秒待って増えない）を足す |
| UI のテスト | `IosUiHarnessBuild` に `NTK_IOS_CABI_2_DIALOG`。`DeviceRequirements.json` の `notMovedYet` から `DialogUITests` を外す。`DialogUITests.swift` の ID-17 は、ハーネスの `[abi@event] 2` のとき `2.0.0` の期待（出て `OK` を押し、`[dialog@ok] button=OK` が 1 回）で照らす。ほかの ID は 1.x と同じ期待 |
| 文書 | `agent-rules` の `common.md`（iOS / macOS の節）と `testing.md`（ガードの表の iOS）を、Dialog が C ABI に移ったことに合わせて直す（2c 計画 v4 の段 3 の行） |

## 5. 確かめ

実機の回は、シミュレーターで全件成功してから流す（README の段 3 の決まり）。

| # | 確かめること | どこで |
|---|---|---|
| V-1 | EditMode（4 章のテスト）と Editor の PlayMode が通る。`Android™ Profile` の EditMode でも `IosCApiDeclarationTests` が通る | `verify_unity_ios.sh`、Unity の CLI |
| V-2 | Player のテスト: ID 20〜22、ID-22b、`VersionIsTwo`、delegate の確かめ、Share・Clipboard・Notification の `UnavailableUntilMoved` が通り、Dialog の `UnavailableUntilMoved` は組まれない。`iOS Profile` と `--as-package-user` | シミュレーター、実機（許可を得て） |
| V-3 | UI のテスト `DialogUITests` の 18 件（ID 1〜17）が `2.0.0` の期待で通る（ID-17 は 3 章の既知の差分の行）。結果の行を 1.x の記録と並べ、3 章の「同じ」の行が全文で同じこと | シミュレーター |
| V-4 | 実機で `DialogUITests` の実機の 13 件（ID 1〜11、14a、14b） | 実機（許可を得て） |
| V-5 | `test_ios_ui.sh` の既定の一覧に `DialogUITests` が入り、ほかの 3 クラスは入らない | ランナー |
