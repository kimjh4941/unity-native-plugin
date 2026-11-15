# iOS Clipboard の C ABI `2.0.0` への写し方の設計 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 親 | [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（Dialog・Share の次、3 つ目） |
| 目的 | `IosClipboardManager` の 15 の操作と `ClipboardChanged` を、3-S の「使えない」の経路から native-toolkit の C ABI `2.0.0`（`Clipboard.h`）の呼び出しに替える。公開の API（操作、イベント、結果の型、`IosClipboardErrorInfo` の `Code`・`Message`）は変えない |
| 一次ソース | パッケージの `Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Clipboard.h`・`Common.h`。native-toolkit の `ios/IosLibraryCApi/IosLibraryCApi/Clipboard/{ClipboardReceiver,ClipboardMapping,ClipboardHandles,ClipboardValuesJson}.swift`、`ios/IosLibrary/IosLibrary/Clipboard/**`（`Domain/Model/ClipboardError.swift`、`IosClipboardManager.swift`）、テスト `ios/IosLibraryCApiTests/{StructLayoutTests.m,ClipboardObservationTests.swift}`、設計 part2 の 11 章と CL-01〜CL-15、引き継ぎ `results/2026-11-07-ios-c-abi-stage2c-handoff.md` |
| 1.x の記録 | [../results/2026-11-14-ios-clipboard-1x-record-result-v1.md](../results/2026-11-14-ios-clipboard-1x-record-result-v1.md)（Player の CP-1〜35、UI の CB-1〜5。実機あり） |
| 手本 | Dialog と Share の移行（`IosDialogCApi.cs`、`IosShareCApi.cs`、`IosNativeToolkitCApi` の UTF-8 の部品） |

## 1. C ABI の要点（一次ソースから）

| # | 事実 | 出典 |
|---|---|---|
| F-1 | 15 の操作のうち 14 に `ntk_clipboard_*_async` がある（copy・append・clear・remove_pasteboard・start_observing・stop_observing・read・read_data・get_snapshot・create_pasteboard・detect_patterns・detect_values・load_item・check_foreground_change）。**一括の CancelLoads は無い**（要求ごとの `ntk_clipboard_cancel_load(uint64_t)` だけ） | `Clipboard.h:215-283`、part2 CL-12 |
| F-2 | 監視は 2 つに分かれる: `ntk_clipboard_add_change_listener`（登録だけ、複数可）と `start_observing_async`（1 つの pasteboard だけ、新しい start が前を置き換える）。**リスナーを外しても監視は止まらない**。`stop_observing_async` はプロセスのすべての監視を止め、失敗しない | `Clipboard.h:274-293`、part2 CL-13、`ClipboardObservationTests.swift:188` |
| F-3 | 構造体（`pack 8`）: `ntk_clipboard_scope` 24、`ntk_clipboard_representation` 32、`ntk_clipboard_write_content` 136、`ntk_clipboard_copy_options` 24（各項目の位置は `StructLayoutTests.m:26-66`）。scope が NULL なら general。copy_options が NULL かゼロ埋めなら local only・期限なし | `Clipboard.h:162-212` |
| F-4 | すべての `_async` は `user_data` と `release` を取り、受け付ければ完了がちょうど 1 回（主スレッド、主スレッドで呼べば呼び出しの中では来ない）、拒めば完了は来ない。要求の ID は load_item だけ（`uint64_t`、1 から増え、使い回さない） | `Clipboard.h:25-36, 154, 266`、part2:223 |
| F-5 | 結果はハンドル（`read_result`・`snapshot`・`loaded_item`・`change`）と `ntk_string`・`ntk_bytes`。受け手が `_free` で解放する。エラーのとき NULL | `Clipboard.h:124-160, 295-348`、`Common.h:46-61` |
| F-6 | エラーは数だけ（`NONE 0`〜`DETECTION_FAILED 29`、10 と 11 は欠番）。**文言も `Domain`・`NativeCode`（`system_code` はいつも 0）も無い**。ライブラリの文言は場合ごとの固定の英文で、入力の値を含まない | `Clipboard.h:40-70`、`ClipboardError.swift:48-127`、part2:530 |
| F-7 | 入口の拒み: NULL の引数、不正な UTF-8、`struct_size`・`reserved*`、範囲外の enum、知らない模様のビット、空か NULL の pasteboard の名前、create に GENERAL、remove に NULL の scope、representation の ut_type の重複、有限でない期限、NULL のコールバック → `INVALID_PARAMETER`。数が 0 の texts・representations → `EMPTY_ITEMS`。模様が 0 → `EMPTY_PATTERNS` | `ClipboardMapping.swift:9-23, 281-293` |
| F-8 | ペーストの許可: Read・ReadData は答えを待つ間、主スレッドが止まる。拒まれても失敗ではない（Read は型も文字も無い 1 項目、ReadData は NULL）。LoadItem は拒まれると `LOAD_FAILED`、15 秒で `TIMED_OUT`。DetectValues は 5 秒で `TIMED_OUT`、拒まれると `DETECTION_FAILED`。GetSnapshot と CheckForegroundChange は許可を求めない | `Clipboard.h:224-265`、part2:70, 143 |
| F-9 | DetectValues は封筒の無い JSON（すべての鍵がいつもある）。DetectPatterns はビットの集合を返す | `ClipboardValuesJson.swift:27-52`、`Clipboard.h:149-151` |
| F-10 | 初期化は要らない（`NOT_INITIALIZED` は返らない）。どの関数もどのスレッドからでも呼べ、主スレッドを待たない | part2:770、`Clipboard.h:5-6` |

## 2. 決めること

| # | 決定 | 理由 |
|---|---|---|
| D-1 | **今の Manager の骨組みはそのまま使う**: 主スレッドの確かめ、破棄の印（`CLIPBOARD_MANAGER_DESTROYED`）、操作ごとの単一の実行（`CLIPBOARD_BUSY`）、`DispatchRejectedResult`、`InvokeNative`、`Handle*Callback`・`Fire*Result`（次の `Update`）、監視の世代。`InvokeNative` に渡す呼び出しを C ABI に替え、`IsBridgeAvailable()` は iOS の Player で `IosNativeToolkitCApi.IsAvailable` を返す | 1.x の記録の振る舞い（CP-23 の BUSY、CP-34、CP-31 の順、時点）を保つ。C ABI は呼び出しごとに `user_data` を持てるので単一の実行の理由（要求の ID が無い）は消えたが、公開の振る舞いとして残す |
| D-2 | `user_data` は**操作の種類**（1〜14 の enum）。`release` は NULL | 単一の実行で、同じ種類の要求は同時に 1 つだけ。Dialog の D-1 と同じ |
| D-3 | **エラーの写し方**: C ABI のエラーの値 → 1.x の文字列のコード（`CLIPBOARD_<名前>`。ただし `CANCELED` → `CLIPBOARD_CANCELLED`、`INVALID_URI` → `CLIPBOARD_INVALID_URL`、`INVALID_PARAMETER` → `CLIPBOARD_INVALID_REQUEST`、`UNKNOWN` とほか → `CLIPBOARD_UNKNOWN`）と、ライブラリの固定の文言（`ClipboardError.swift` の `errorDescription` を C# に写す）。`Domain` と `NativeCode` は null | part2 の 11.3 の逆。文言を同じにして 1.x の記録の全文の照らし（CP-21・22・25〜30）が保てる見込み。`Domain`・`NativeCode` は取れない（**既知の差分**。CB-2b・CB-3） |
| D-4 | 入口の拒み（`NONE` 以外）は、D-3 のコードと文言で `Fire*Result` に出す（`InvokeNative` が今している「呼べなかった」の経路と同じ時点、次の `Update`）。C# が組んだ要求の誤りなので `LogError` も出す | 1.x は入口で拒まず、ネイティブの完了で失敗を返していた。時点は同じ |
| D-5 | **CancelLoads**: 待っている LoadItem の要求の ID（単一の実行なので 0 か 1 つ）を覚えておき、`ntk_clipboard_cancel_load(id)` を呼んでから、CancelLoads の成功を出す。読み込みは `CANCELED` → `CLIPBOARD_CANCELLED` で後から完了する | F-1。1.x の順（cancel → load。CP-31）と同じ |
| D-6 | **監視**: リスナーは最初の StartObserving のときに 1 つだけ登録し、Manager の破棄まで持つ。StartObserving は `start_observing_async(scope)`、StopObserving は `stop_observing_async`。変化はリスナーの受け口で `IosClipboardChangeEvent` に写して、今の世代の経路に渡す | F-2。今の C# の世代の仕組み（CP-32・33・34）をそのまま使える |
| D-7 | **破棄**: `OnDestroy` の後片付けで、`stop_observing_async`（空のコールバック）、`ntk_clipboard_listener_remove`、待っている読み込みの `cancel_load` を呼ぶ | 1.x の `OnDestroy` は監視を止めた。リスナーを外すだけでは止まらない（F-2、引き継ぎ 209 行）。UI の CB-5 の `native-listener=0` |
| D-8 | **内容の写し方**: `IosClipboardContent` の種類 → `write_content` の `kind` と項目（文字列は UTF-8、バイト列は確保して `data`・`data_size`、色は double 4 つ、文字列の配列、representation の配列）。オプション → `copy_options`（`localOnly` の反転 `not_local_only`、期限があれば `has_expiration` と Unix 秒）。scope → `ntk_clipboard_scope`（general は NULL） | F-3 |
| D-9 | **結果の写し方**: ハンドルを受けた回の中で読み、今の結果の型（`IosClipboardReadResult.Success` など）を作ってから `_free`。DetectPatterns はビット → `IosClipboardDetectionPattern` の一覧。DetectValues は封筒の無い JSON を今のパーサーで読めるようにする（封筒が無い形を受ける入口を足す） | F-5、F-9。結果の型は公開の API |
| D-10 | 宣言と変換は新しい `Runtime/iOS/Clipboard/IosClipboardCApi.cs`（`#if UNITY_IOS \|\| UNITY_EDITOR`、`extern` は `!UNITY_EDITOR` の外）。使う関数だけ宣言する。コールバックの本体と呼び出しは `UNITY_IOS && !UNITY_EDITOR` | Dialog・Share と同じ決まり |
| D-11 | JSON の Builder は段 4 まで残す（使われなくなる）。JSON のパーサーは DetectValues と、テストのための入口だけに使う | README の段 4 |

## 3. 振る舞いの対応（1.x の記録と `2.0.0` の見込み）

| 場面 | 1.x（記録） | `2.0.0`（この設計） | 区分 |
|---|---|---|---|
| CP-1〜35 の値・回数・順 | 記録 | 同じ見込み（D-1・D-3・D-5・D-6）。CP-17 の「1 回の Copy で変化が 2 回」はライブラリが重ねない（引き継ぎ）ので同じ見込み | 同じ（実測で確かめる） |
| 失敗の `Domain`・`NativeCode` | CB-2b・CB-3 で値がある | null | 既知の差分 |
| 読み込み系の失敗のエラーのログ | `[IosClipboardJsonParser][ReadErrorInfo]` の `LogError` | 出ない | 既知の差分（テストのログの予期を外す） |
| CB-5（破棄の後） | `abi=none`、`native-listener=none` | `abi=present`、`native-listener=0`（D-7） | 計画のとおり |
| Editor | `CLIPBOARD_BRIDGE_UNAVAILABLE` | 同じ | 同じ |

## 4. 変えるもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Clipboard/IosClipboardCApi.cs`（新） | 4 つの構造体、enum の値、コールバックの型、使う関数の宣言、内容・オプション・scope の組み立て、エラーの写し方と文言、結果の読み取り（Player だけ） |
| `Runtime/iOS/Clipboard/IosClipboardManager.cs` | `InvokeNative` の呼び出しを C ABI に、静的なコールバック（種類ごと）、`IsBridgeAvailable`、D-5・D-6・D-7 |
| `IosClipboardJsonParser.cs` | 封筒の無い DetectValues の JSON を読む入口 |
| テスト | EditMode: 構造体の大きさと位置、組み立て、エラーの写し方と文言、模様のビット、`IosCApiDeclarationTests` の名前と署名。Player: `NTK_IOS_CABI_2_CLIPBOARD` で `IosClipboardPlayerTests` を組み、1.x の期待のうち既知の差分（ログ、`Domain`）だけ分ける。Clipboard の `UnavailableUntilMoved` を消す |
| UI のテスト | `notMovedYet` から `ClipboardUITests` を外す。CB-2b・CB-3 は `[abi@event] 2` のとき `Domain`・`NativeCode` が null の期待。CB-5 は計画のとおり |
| 文書 | `agent-rules/coding-rules/testing.md`（ガードの表、層 1）、実装の記録、トピックの README |

## 5. 確かめ

| # | 確かめること | どこで |
|---|---|---|
| V-1 | EditMode と Editor の PlayMode（`iOS Profile`。`Android™ Profile` の EditMode でも宣言の照合） | `verify_unity_ios.sh` |
| V-2 | Player のテスト（Clipboard 35 件を含む）。3 章の「同じ」が 1.x の記録と同じ値で通る | シミュレーター、実機（許可を得て） |
| V-3 | `ClipboardUITests` の 6 件（CB-1〜5、2b） | シミュレーター |
| V-4 | 実機で CB-5（名前付きの pasteboard だけ） | 実機（許可を得て） |
