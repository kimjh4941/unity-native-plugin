# iOS Clipboard の C ABI `2.0.0` への写し方の設計 v4

## 基本情報

| 項目 | 内容 |
|---|---|
| 親 | [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（Dialog・Share の次、3 つ目） |
| 目的 | `IosClipboardManager` の 15 の操作と `ClipboardChanged` を、3-S の「使えない」の経路から native-toolkit の C ABI `2.0.0`（`Clipboard.h`）の呼び出しに替える。公開の API（操作、イベント、結果の型、`IosClipboardErrorInfo` の `Code`・`Message`）は変えない |
| 一次ソース | パッケージの `Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Clipboard.h`・`Common.h`。native-toolkit の `ios/IosLibraryCApi/IosLibraryCApi/Clipboard/{ClipboardReceiver,ClipboardMapping,ClipboardHandles,ClipboardValuesJson}.swift`、`ios/IosLibraryCApi/IosLibraryCApi/Common/{Registry,MainQueue,CApiString}.swift`、`ios/IosLibrary/IosLibrary/Clipboard/**`（`Domain/Model/ClipboardError.swift`、`IosClipboardManager.swift`）、テスト `ios/IosLibraryCApiTests/{StructLayoutTests.m,ClipboardObservationTests.swift}`、設計 part2 の 8.3 の CL の表（CL-01〜CL-15）と 11 章、引き継ぎ `results/2026-11-07-ios-c-abi-stage2c-handoff.md` |
| 1.x の記録 | [../results/2026-11-14-ios-clipboard-1x-record-result-v1.md](../results/2026-11-14-ios-clipboard-1x-record-result-v1.md)（Player の CP-1〜35、UI の CB-1〜5。実機あり） |
| 手本 | Dialog と Share の移行（`IosDialogCApi.cs`、`IosShareCApi.cs`、`IosNativeToolkitCApi` の UTF-8 の部品） |
| レビュー | [../reviews/2026-11-15-ios-clipboard-cabi-design-review-v1.md](../reviews/2026-11-15-ios-clipboard-cabi-design-review-v1.md)（v1 の 1 回目。A1 が 3 件）、[../reviews/2026-11-15-ios-clipboard-cabi-design-review-v2.md](../reviews/2026-11-15-ios-clipboard-cabi-design-review-v2.md)（v2 の 2 回目。Codex の A1 が 1 件）、[../reviews/2026-11-15-ios-clipboard-cabi-design-review-v3.md](../reviews/2026-11-15-ios-clipboard-cabi-design-review-v3.md)（v3 の 3 回目。Codex の A1 が 2 件。決まりの上限の 3 回に達したので、v4 はレビューしていない。利用者に確かめる） |

### v3 からの差分（レビューしていない）

| 指摘（レビュー v3） | v4 で変えたところ |
|---|---|
| Codex の R4-1（一覧の結果が、ディスパッチャーに先に積まれた変化を追い越す） | D-12: 期限に来た塊は、ディスパッチャーの末尾に順に渡す（先に積まれたものより後に出る） |
| Codex の R4-2（Manager を無効にすると一覧が止まり BUSY が残る） | D-12: フレームの境を見る役を、Manager ではなくディスパッチャーの GameObject に付けた常駐の部品に置く |
| サブの R-1（その場で配る範囲、`InvokeNative` の失敗が同期） | D-12: `InvokeNative` の例外も一覧に。その場で配るのは塊 1 つの 1 回だけ |
| サブの R-2〜R-5 | D-12（変化の世代）、D-4、3 章、4 章 |

### v2 からの差分

| 指摘（レビュー v2） | v3 で変えたところ |
|---|---|
| Codex の R3-1、サブの R-4・R-3・R-5（D-12 が同じフレームで配られる、塊の一生、積み直し） | D-12 を書き直し（Manager 自身の一覧と `Update`）、3 章の時点の行 |
| Codex の R3-2、サブの R-1（状態を変える経路） | D-6 (2) |
| サブの R-2・R-6（画像の道と ImageData の型の U+0000） | D-8 (5)、3 章 |
| サブの R-7（1.x の列）、R-8（出典） | 3 章、D-7、F-11 |

### v1 からの差分

| 指摘（レビュー v1） | v2 で変えたところ |
|---|---|
| サブの R-1（U+0000） | D-8 に U+0000 の扱い、3 章に既知の差分 2 行 |
| Codex の R2-1（入口の拒みで印が早く外れる）、サブの R-4（CancelLoads） | D-4 を書き直し（あとで配る経路 D-12）、D-5 に印の扱い |
| Codex の R2-2、サブの R-5（停止の後のイベント） | D-6 に Manager 自身の監視の状態 |
| サブの R-2、Codex の R2-3（Remove の General） | D-8 |
| サブの R-3（模様のビット） | D-8 |
| サブの R-6（`LogError`） | D-4（警告に下げる）、3 章 |
| サブの R-7（結果の型） | D-9 を表に |
| サブの R-8（例外）、R-9（64 MiB） | D-9 |
| サブの R-10、Codex の R2-4・R2-5（テストで捕まらない） | D-13（純粋な部分を分ける）、4 章・5 章 |
| サブの R-11〜R-15 | F-1・F-7 の出典、基本情報、D-2 の表、D-7、D-8 の期限、4 章 |

（v1 からの差分。3 章の既知の差分は U+0000 の 3 行）

## 1. C ABI の要点（一次ソースから）

| # | 事実 | 出典 |
|---|---|---|
| F-1 | 15 の操作のうち 14 に `ntk_clipboard_*_async` がある（copy・append・clear・remove_pasteboard・start_observing・stop_observing・read・read_data・get_snapshot・create_pasteboard・detect_patterns・detect_values・load_item・check_foreground_change）。**一括の CancelLoads は無い**（要求ごとの `ntk_clipboard_cancel_load(uint64_t)` だけ） | `Clipboard.h:215-285`、part2 CL-12 |
| F-2 | 監視は 2 つに分かれる: `ntk_clipboard_add_change_listener`（登録だけ、複数可）と `start_observing_async`（1 つの pasteboard だけ、新しい start が前を置き換える。`UNAVAILABLE` で失敗したときは前のものは既に止まっている）。**リスナーを外しても監視は止まらない**。`stop_observing_async` はプロセスのすべての監視を止め（他の部品が始めたものも）、失敗しない。届いた変化は、生きているリスナーのすべてに配られる | `Clipboard.h:274-293`、part2 CL-13、`ClipboardObservationTests.swift:188`、`Registry.swift:369-376, 429-441` |
| F-3 | 構造体（`pack 8`）: `ntk_clipboard_scope` 24、`ntk_clipboard_representation` 32、`ntk_clipboard_write_content` 136、`ntk_clipboard_copy_options` 24（各項目の位置は `StructLayoutTests.m:26-66`）。scope が NULL なら general（remove だけは NULL を拒む）。copy_options が NULL かゼロ埋めなら local only・期限なし | `Clipboard.h:162-212, 248-251` |
| F-4 | すべての `_async` は `user_data` と `release` を取り、受け付ければ完了がちょうど 1 回（主スレッド、主スレッドで呼べば呼び出しの中では来ない）、拒めば完了は来ない。要求の ID は load_item だけ（`uint64_t`、1 から増え、使い回さない）。cancel_load の取り消しの完了も主の待ち行列に積まれ、呼び出しの中では来ない | `Clipboard.h:25-36, 154, 266-273`、part2:223、`Registry.swift:282-292`、`MainQueue.swift:12-18` |
| F-5 | 結果はハンドル（`read_result`・`snapshot`・`loaded_item`・`change`）と `ntk_string`・`ntk_bytes`。受け手が `_free` で解放する。エラーのとき NULL。ReadData の `ntk_bytes` は型の項目が無ければ `NONE` でも NULL。返す文字列の U+0000 は U+FFFD になる | `Clipboard.h:37, 124-160, 295-348`、`Common.h:46-61` |
| F-6 | エラーは数だけ（`NONE 0`〜`DETECTION_FAILED 29`、10 と 11 は欠番）。**文言も `Domain`・`NativeCode`（`system_code` はいつも 0）も無い**。ライブラリの文言は場合ごとの固定の英文で、入力の値を含まない。値と文言は 1 対 1（重なるのは UNKNOWN だけ） | `Clipboard.h:40-70`、`ClipboardError.swift:48-127`、`ClipboardMapping.swift:313-338`、part2:530 |
| F-7 | 入口の拒み: NULL の引数、不正な UTF-8、`struct_size`・`reserved*`、範囲外の enum、知らない模様のビット、空か NULL の pasteboard の名前、create に GENERAL、remove に NULL の scope、representation の ut_type の重複、有限でない期限、空の画像の道、NULL のコールバック、配列の中の NULL → `INVALID_PARAMETER`。数が 0 の texts・representations → `EMPTY_ITEMS`。模様が 0 → `EMPTY_PATTERNS` | `ClipboardMapping.swift:9-23, 58-98, 153-211, 225-233, 253-262, 281-293`、`ClipboardReceiver.swift:58-61`、`CApiString.swift`（`requiredArray`） |
| F-8 | ペーストの許可: Read・ReadData は答えを待つ間、主スレッドが止まる。拒まれても失敗ではない（Read は型も文字も無い 1 項目、ReadData は NULL）。LoadItem は拒まれると `LOAD_FAILED`、15 秒で `TIMED_OUT`。DetectValues は 5 秒で `TIMED_OUT`、拒まれると `DETECTION_FAILED`。GetSnapshot と CheckForegroundChange は許可を求めない | `Clipboard.h:224-265`、part2:70, 143 |
| F-9 | DetectValues は封筒の無い JSON（すべての鍵がいつもある）。DetectPatterns はビットの集合を返す | `ClipboardValuesJson.swift:27-52`、`Clipboard.h:149-151` |
| F-10 | 初期化は要らない（`NOT_INITIALIZED` は返らない）。どの関数もどのスレッドからでも呼べ、主スレッドを待たない | part2:770、`Clipboard.h:5-6` |
| F-11 | ReadData の大きさに上限は無い（`The bytes are returned as they are`）。LoadItem の画像はライブラリの `maxLoadByteCount` で止まる | `Clipboard.h:230-231`、native-toolkit `ClipboardRepositoryImpl.swift:78-82`（ReadData）、`ClipboardProviderLoadExecutor.swift:165-217`（`maxLoadByteCount`） |

## 2. 決めること

| # | 決定 | 理由 |
|---|---|---|
| D-1 | **今の Manager の骨組みはそのまま使う**: 主スレッドの確かめ、破棄の印（`CLIPBOARD_MANAGER_DESTROYED`）、操作ごとの単一の実行（`CLIPBOARD_BUSY`）、`DispatchRejectedResult`、`InvokeNative`、`Handle*Callback`・`Fire*Result`（次の `Update`）、監視の世代。`InvokeNative` に渡す呼び出しを C ABI に替え、`IsBridgeAvailable()` は iOS の Player で `IosNativeToolkitCApi.IsAvailable` を返す。**例外**: CancelLoads は C ABI の完了を待たない（D-5） | 1.x の記録の振る舞い（CP-23 の BUSY、CP-34、CP-31 の順、時点）を保つ。C ABI は呼び出しごとに `user_data` を持てるので単一の実行の理由（要求の ID が無い）は消えたが、公開の振る舞いとして残す |
| D-2 | `user_data` は**操作の種類**（`Op` の 1〜14）。0 は破棄の後片付けの停止（完了は捨てる）。`release` は NULL（リスナーだけ D-6）。**静的なコールバックは C ABI のコールバックの型ごとに 1 つ**で、`user_data` で操作を分ける: done（Copy・Append・Clear・RemovePasteboard・StartObserving・StopObserving と破棄の停止）、read（Read）、data（ReadData）、snapshot（GetSnapshot）、name（CreatePasteboard）、patterns（DetectPatterns）、values（DetectValues）、load（LoadItem）、foreground（CheckForegroundChange）、change（リスナー） | 単一の実行で、同じ種類の要求は同時に 1 つだけ。Dialog の D-1 と同じ |
| D-3 | **エラーの写し方**: C ABI のエラーの値 → 1.x の文字列のコード（`CLIPBOARD_<名前>`。ただし `CANCELED` → `CLIPBOARD_CANCELLED`、`INVALID_URI` → `CLIPBOARD_INVALID_URL`、`INVALID_PARAMETER` → `CLIPBOARD_INVALID_REQUEST`、`UNKNOWN` とほか → `CLIPBOARD_UNKNOWN`）と、ライブラリの固定の文言（`ClipboardError.swift` の `errorDescription` を C# に写す。`1.13.0` と今の HEAD で同じ）。`Domain` と `NativeCode` は null | part2 の 11.3 の逆。文言を同じにして 1.x の記録の全文の照らし（CP-21・22・25〜30）が保てる見込み。`Domain`・`NativeCode` は取れない（**既知の差分**。CB-2b・CB-3） |
| D-4 | **入口の拒み**（`NONE` 以外）は、D-3 のコードと文言の失敗を **D-12 のあとで配る経路**で出す。要求の枠と単一の実行の印は配るまで持つ（同じフレームの 2 回目は 1.x と同じく `CLIPBOARD_BUSY`）。ログは `LogWarning`（`rejected: <エラーの名前>`。値は出さない）。`EMPTY_ITEMS` や配列の中の null のように利用者の入力から来るものがあるので `LogError` にしない。拒まれた StartObserving・StopObserving は `HandleObservationControlCallback`（世代の後片付け。D-6 の状態は変えない。D-6 (2)）を通る | 1.x は入口で拒まず、ネイティブの完了（次のフレーム以降）で失敗を返し、その間は印を持っていた（Codex の R2-1）。1.x は失敗にログを出さなかった（3 章） |
| D-5 | **CancelLoads**: 待っている LoadItem の要求の ID（単一の実行なので 0 か 1 つ。LoadItem の完了と拒みで 0 に戻す）があれば `ntk_clipboard_cancel_load(id)` を呼び、CancelLoads の成功を D-12 の経路で出す（印は配るまで持つ）。読み込みは `CANCELED` → `CLIPBOARD_CANCELLED` で後から完了する。`cancel_load` が投げたら `InvokeNative` と同じく「could not be started」 | F-1、F-4。CancelLoads の配りが先に積まれ、読み込みの完了は主の待ち行列を回ってから積まれるので、1.x の順（cancel → load。CP-31）になる。同じフレームの 2 回目は BUSY（1.x と同じ） |
| D-6 | **監視**: (1) リスナーは最初の StartObserving で `start_observing_async` より**先に** 1 つだけ登録し（2 つは主の待ち行列にその順に積まれる）、Manager の破棄まで持つ。`user_data` は 0、`release` は D-7 の数を増やす静的な関数。登録が拒まれたら StartObserving はその失敗（D-3）を D-12 の経路で出し、`start_observing_async` は呼ばない。(2) **C# が自分の監視の状態**（`s_isObserving`）を持つ: StartObserving の成功の完了で立て、**ライブラリが受け付けた後の** StartObserving の失敗の完了・StopObserving の結果（成功も失敗も）・破棄で下ろす。**状態を変えるのは、監視の枠を持つ要求の完了の経路だけ**: BUSY・主スレッドの外・破棄の後のような枠の前の拒みは、状態も世代も変えない。入口の拒み（D-8 (5) の C# の拒み、`NONE` 以外の戻り値、登録の拒み）も状態を変えない（ライブラリは何もしておらず、前の監視はネイティブで続いている）。世代の後片付け（拒まれた start の `onChanged` を外す）は今のまま通す。(3) 変化は、破棄の後か状態が下りていれば解放だけして捨てる。そうでなければ `IosClipboardChangeEvent` に写して今の世代の経路（`FireClipboardChanged`）に渡す。この門は Editor でも組まれる関数に置き、Editor のテストの入口（`DeliverChangeEventForTests`）も同じ関数を通る | F-2。他の部品が同じプロセスで監視を始めても、Manager が始めていない間（停止の後を含む）の変化は出ない（公開の約束「Never raised before StartObserving succeeds」「No further change events … once the result arrives」）。入口の拒みは 1.x でもライブラリを呼ばず、前の監視の変化は出続けた（`1.13.0` `UnityIosClipboardManager.swift:285-288`） |
| D-7 | **破棄**: 破棄の印を立ててから、`RunDestroyCleanup` で次の 3 つを**それぞれ別の `try`** で呼ぶ: `stop_observing_async`（done、`user_data` 0）、`ntk_clipboard_listener_remove`（呼んだらすぐハンドルを 0 に）、待っている読み込みの `cancel_load`。そのあと C# の後片付け（`s_isObserving` を下ろす、枠を空にする）は必ず走る。リスナーの `release` が呼ばれたら内部の静的な数を 1 増やす。`release` は `listener_remove` の中では呼ばれず、主の待ち行列を回ってから呼ばれる（`Registry.swift:148-151, 212-218, 295-301`）。照らすのは UI のハーネスの CB-5（4 章。破棄の後に読む）: Player のテストで Manager を壊すと墓標が残り、後のテストがすべて拒まれるため | 1.x の `OnDestroy` は監視を止めた。リスナーを外すだけでは止まらない（F-2、引き継ぎ 209 行）。2 回外すのは未定義（引き継ぎの `4.4`） |
| D-8 | **要求の写し方**: (1) 内容: `IosClipboardContent` の種類 → `write_content` の `kind`（C# の enum の 0〜8 と C の値は同じ並び）と項目（文字列は UTF-8、バイト列は確保して `data`・`data_size`、色は double 4 つ、文字列の配列、representation の配列）。(2) オプション → `copy_options`: `not_local_only` は `!LocalOnly`、期限があれば `has_expiration = 1` と、`ToUniversalTime()` の後の Unix 秒を**整数に切り捨てた**値（1.x と同じ）。(3) scope → `ntk_clipboard_scope`: General は NULL。**ただし RemovePasteboard だけは General も `kind = GENERAL` の構造体**（NULL は入口で拒まれる。CP-21 の `CLIPBOARD_CANNOT_REMOVE_GENERAL` を保つ）。(4) 模様: **表**（`IosClipboardDetectionPattern` の 11 の値 → ビット）で写し、表に無い値は C# で `CLIPBOARD_INVALID_REQUEST`「The request is invalid.」（1.x はネイティブが読めずに同じコード）。(5) **U+0000**: 名前・型・道（scope の名前、CreatePasteboard の名前、ReadData の `utType`、GetSnapshot の `matchingTypes`、LoadItem の型、representation の型、ImageData と CustomData の型、ImageFile の道）に U+0000 があれば、C# で `CLIPBOARD_INVALID_REQUEST`「The request is invalid.」にする（`"x\0y"` が別の pasteboard `"x"` や別の実在のファイルを指すのを防ぐ。POSIX の道は U+0000 を含めないので正しい要求は拒まない。Share は道を警告で渡すが、共有は利用者がシートで中身を見て選ぶのに対し、Copy は確かめ無しに他のアプリから読める pasteboard に書くので扱いを分ける）。値（文字列、HTML、URL、`texts`）は Share の D-5 と同じく、値を出さない警告を出して渡す（U+0000 の前で切れる）。(4)・(5) の失敗は単一の実行の印を取った後に決め、D-12 の経路で出す | F-3、F-7、1.x の `IosClipboardJsonBuilder.cs:271-283, 443-447` |
| D-9 | **結果の写し方**: ハンドルを受けた回の中で読み、結果の型を作ってから `_free`。写し方は 2.1 の表。次の約束を守る: (1) 読み取りが投げたら `CLIPBOARD_UNKNOWN`「An unknown error occurred.」で完了する（印が残って以後ずっと BUSY になるのを防ぐ。逆 P/Invoke の外に例外を出さない）。(2) `NONE` なのにハンドルが NULL なら UNKNOWN（ReadData の NULL は「データ無し」の成功なので除く）。(3) ハンドルは、破棄の後で捨てる経路も含めて `finally` で解放する。(4) ReadData は `ntk_bytes_size` が 64 MiB（1.x の `MaxDataByteCount`）を超えたら、写さずに 1.x と同じ `CLIPBOARD_CONTENT_TOO_LARGE`「The clipboard content exceeds the configured size limit.」 | F-5、F-11、`Clipboard.h:36`、Share と Dialog の完了の受け口 |
| D-10 | 宣言と変換は新しい `Runtime/iOS/Clipboard/IosClipboardCApi.cs`（`#if UNITY_IOS \|\| UNITY_EDITOR`、`extern` は `!UNITY_EDITOR` の外）。使う関数だけ宣言する。コールバックの本体と呼び出し、ハンドルから値を取る部分は `UNITY_IOS && !UNITY_EDITOR` | Dialog・Share と同じ決まり |
| D-11 | JSON の Builder は段 4 まで残す（使われなくなる）。JSON のパーサーは DetectValues（封筒の無い JSON を読む入口を足す）と、テストのための入口だけに使う | README の段 4 |
| D-12 | **あとで配る経路**: C# が呼び出しの中で決めた結果（入口の拒み、D-8 の失敗、CancelLoads の成功、登録の拒み、`InvokeNative` が例外で始められなかったとき〈D-5 の `cancel_load` を含む。StartObserving・StopObserving の世代の後片付けも配る時に回す〉）は、積んだフレームの番号と一生の番号を付けて**Manager の外の静的な一覧**に入れる。フレームの境を見る役は、ディスパッチャーの GameObject に付ける常駐の部品（`IosClipboardLaterPump`。Manager の有効・無効や GameObject と関係なく `Update` が来る）で、その `Update` が、一覧の頭から「積んだフレームより後」の塊だけを取り出し、**ディスパッチャーの末尾に順に渡す**（先にディスパッチャーに積まれた結果・変化より後に出る）。積んだフレームの塊（配っている間に受け手の中から呼ばれたものを含む）はそこで止まり、次のフレームを待つ。ディスパッチャーが塊を回す時は、破棄の後か一生の番号（リセットと破棄で増える）が違えば捨て、そうでなければその操作の通常の完了の経路（枠を取り出し、印を外し、監視の後片付けをする）を通り、`Dispatch` は積み直さずにその場で `InvokeInOrder` する（積み直すと、同じ回で後ろに渡したネイティブの結果に追い越される）。その場で配るのは塊 1 つの `Dispatch` の 1 回だけで、利用者の受け手の中からの呼び出しの結果は、また一覧を通る。変化の結果は、届いた時の登録の世代を持ち、配る時に世代が変わっていれば登録の受け手を外して共通のイベントだけに出す（拒まれた start の受け手に、失敗の後で変化が届かない）。**一覧に何か待っている間は、ネイティブの完了の結果も一覧の後ろに並べる**（読み込みの完了が CancelLoads の結果を追い越さない。CP-31）。破棄のとき、一覧に残った結果のうちネイティブの完了のものはディスパッチャーに順に渡し、呼び出しの中で決めたものは捨てる。リセットで一覧を空にする | Codex の R2-1・R3-1・R4-1・R4-2、サブの R-4。ディスパッチャーは 1 回の `Update` で、その間に積まれたものも空になるまで回すので、呼び出しの中で積むと同じフレームで配られ、受け手の中から呼び直す利用者のコードでは `Update` が終わらない。1.x の入口の拒みは主の待ち行列を回ってから C# に来た（早くてもフレームとフレームの間）。結果と変化は 1.x と同じく 1 本のディスパッチャーの順で出る |
| D-13 | **テストのための分け方**: ハンドルから値を取る P/Invoke の部分（Player だけ）と、取った値から結果の型を作る純粋な部分（`IosClipboardCApi` の internal の関数。Editor でも組む）に分ける。純粋な部分は EditMode で、2.1 の表の行ごとに照らす | サブの R-10。Share の `ReadResult` と同じ考え |

### 2.1 結果の写し方の表

| 操作 | C ABI から | 公開の型へ |
|---|---|---|
| Copy・Append・Clear・RemovePasteboard・StartObserving・StopObserving | done の `error` | `NONE` → `IosClipboardOperationResult.Success(op)`、ほか → `Failure(op, D-3)` |
| Read | `read_result`: 項目の数、項目ごとの型の一覧・`text`・`url`・`image_type` | `IosClipboardReadResult.Success(numberOfItems: 項目の数, items)`。項目は `IosClipboardItem(TypeIdentifiers, Text, UrlString, ImageDataUtType)`。NULL の文字列は null |
| ReadData | `ntk_bytes`（NULL は無し） | NULL → `NoData()`。あれば `Success(utType: 要求の utType, data)`（C ABI は型を返さないので要求から覚えておく）。64 MiB 超は D-9 (4) |
| GetSnapshot | `snapshot`: `has_*` 4 つ、`number_of_items`、`type_at`、項目ごとの型、`has_matching`・`matching_at` | `IosClipboardSnapshot(HasStrings, HasUrls, HasImages, HasColors, NumberOfItems, TypeIdentifiers, AllTypeIdentifiers, MatchingItemIndexes)`。`AllTypeIdentifiers` は `number_of_items` 行（項目ごとの型の数を返す関数が行の数を持たないため）。`has_matching` が 0 なら `MatchingItemIndexes` は null、そうでなければ `matching_at` の一覧（空もある） |
| CreatePasteboard | `ntk_string`（名前） | `IosPasteboardScopeResult.Success(scope)`。scope の種類は**要求から**（Named → `IosPasteboardScope.Named(名前)`、Unique → `Unique(名前)`） |
| DetectPatterns | `ntk_clipboard_patterns` | 表で 0〜10 のビット → `IosClipboardDetectionPattern` の一覧（小さいビットから）。表に無いビットは捨てる |
| DetectValues | `ntk_string`（封筒の無い JSON） | パーサーの新しい入口で `IosClipboardDetectedValues` に。読めなければ 1.x と同じパーサーの失敗 |
| LoadItem | `loaded_item`: kind と kind ごとの値 | TEXT → `FromText`、URL → `FromUrl`、IMAGE → `FromImageData(data, image_type)`、FILE → `FromFile(path)`、-1 と知らない値 → `UnknownKind()`。道のファイルの持ち主は利用者（1.x と同じ） |
| CheckForegroundChange | `changed` | `Success(changed != 0)` |
| 変化（リスナー） | `change`: kind、scope の kind と名前、足された型・消えた型 | kind 0 → Changed、1 → Removed、ほか → `Unknown`。scope は GENERAL → `IosPasteboardScope.General`、NAMED → `Named(名前)`、UNIQUE → `Unique(名前)`、-1 か名前が無い → null |

## 3. 振る舞いの対応（1.x の記録と `2.0.0` の見込み）

| 場面 | 1.x（記録） | `2.0.0`（この設計） | 区分 |
|---|---|---|---|
| CP-1〜35 の値・回数・順 | 記録 | 同じ見込み（D-1・D-3・D-5・D-6・D-8・D-12）。CP-17 の「1 回の Copy で変化が 2 回」はライブラリが重ねない（引き継ぎ）ので同じ見込み | 同じ（実測で確かめる） |
| 入口で拒まれる要求の時点と BUSY | 主の待ち行列を回ってから（早くてもフレームとフレームの間）、その間は BUSY | 呼び出しのフレームより後の Manager の `Update`、その間は BUSY（D-4・D-12） | 同じ（細かな時点は既知の差分: 1 回目が拒まれ 2 回目が BUSY のとき、1.x は BUSY が先に届いた。v3 は拒みが先のこともある） |
| 失敗の `Domain`・`NativeCode` | CB-2b・CB-3 で値がある | null | 既知の差分 |
| 読み込み系の失敗のエラーのログ | `[IosClipboardJsonParser][ReadErrorInfo]` の `LogError` | 出ない | 既知の差分（テストのログの予期を外す） |
| 入口で拒まれた要求のログ | done の系（Copy など）は出ない。読み取り系は `IosClipboardJsonParser.ReadErrorInfo` の `LogError` | `LogWarning` の 1 行（値なし） | 既知の差分（テストは落ちない） |
| 書き込む値の中の U+0000 | JSON で全文がライブラリまで届いた | U+0000 の前で切れた値で処理され（空になれば `CLIPBOARD_EMPTY_CONTENT` などその失敗）、警告が出る | 既知の差分 |
| 名前・型・画像の道の中の U+0000 | ネイティブ任せ | `CLIPBOARD_INVALID_REQUEST` | 既知の差分 |
| 読み取った文字列の中の U+0000 | U+0000 | U+FFFD | 既知の差分 |
| 他の部品が同じプロセスで監視を置き換えたとき | ライブラリはプロセスで 1 つで、Manager の監視は知らずに止まった（`1.13.0` `IosClipboardManager.swift:54, 325-330`）。置き換えた先の変化は Manager に出なかった | 監視が知らずに止まるのは同じ。別の C ABI の利用者が置き換えたときは、Manager の状態が立っている間、置き換えた先の変化も出る。ライブラリを直接使って置き換えたときは 1.x と同じく出ない | 既知の差分（C ABI の仕組み） |
| CB-5（破棄の後） | `abi=none`、`native-listener=none` | `abi=present`、`native-listener=0`、リスナーの `release` が 1 回（D-7） | 計画のとおり |
| Editor | `CLIPBOARD_BRIDGE_UNAVAILABLE` | 同じ | 同じ |

## 4. 変えるもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Clipboard/IosClipboardCApi.cs`（新） | 4 つの構造体、enum の値、コールバックの型、使う関数の宣言、内容・オプション・scope の組み立て、模様の表、U+0000 の判定、エラーの写し方と文言、取った値から結果を作る純粋な部分（D-13）、ハンドルから値を取る部分（Player だけ） |
| `Runtime/iOS/Clipboard/IosClipboardManager.cs` | `InvokeNative` の呼び出しを C ABI に、型ごとの静的なコールバック（D-2）、`IsBridgeAvailable`、D-4〜D-7・D-12。クラスの doc の「Unavailable for now」「The native ABI carries no request identifier」を書き換える |
| `IosClipboardJsonParser.cs` | 封筒の無い DetectValues の JSON を読む入口 |
| テスト（EditMode） | 構造体の大きさと位置、組み立て（Remove の General、期限の切り捨て、U+0000、模様の表と範囲外）、エラーの写し方と文言、2.1 の表の行ごとの写し方、`RunDestroyCleanup` の段、`IosCApiDeclarationTests` の名前と署名（コールバックの型を含む） |
| テスト（Editor の PlayMode） | D-12: 呼び出しのフレームでは配らず 2 回目は BUSY、受け手の中から呼び直しても同じフレームでは配らない、ネイティブの完了が後ろに並ぶ、先にディスパッチャーに積まれた変化が先に出る、Manager を無効にしても配られる、破棄の後は呼び出しの中で決めたものを捨ててネイティブの完了だけ届く、受け手の中で `InvokeNative` が失敗しても呼び出しの中では返らない、拒まれた start の受け手に後から変化が届かない。D-6: start の成功の前と stop の結果の後の変化は出ない、BUSY の stop と入口で拒まれた start は状態を変えない、受け付けた後の start の失敗は下ろす。D-8: U+0000 の名前と画像の道、範囲外の模様、値の U+0000 の警告 |
| テスト（Player） | `NTK_IOS_CABI_2_CLIPBOARD` で `IosClipboardPlayerTests` を組み、1.x の期待のうち既知の差分（ログ、`Domain`）だけ分ける。CP に `Scope`（変化、CreatePasteboard の Named）・ReadData の `UtType`・Snapshot の `Has*` の照らしを足す。CP-31 に `order` が `["cancel", "load"]` の照らしを足す。2.0.0 だけの CP-36〜40（入口の拒みと BUSY、受け手の中からの呼び直し、U+0000、範囲外の模様、BUSY の stop の後も変化が届く）。Clipboard の `UnavailableUntilMoved` を消す |
| ビルドの設定 | `Assets/NativeToolkitIosPlayerTests/Editor/IosPlayerBuildSetup.cs` の `Defines` と、UI のハーネスのビルドに `NTK_IOS_CABI_2_CLIPBOARD` |
| UI のテスト | `Tools~/IosUiTests/DeviceRequirements.json` の `notMovedYet` から `ClipboardUITests` を外す。CB-2b・CB-3 は `[abi@event] 2` のとき `Domain`・`NativeCode` が null の期待。CB-5 は計画のとおりに加え、ハーネスがリスナーの `release` の数（内部の静的な値を反射で読む）を `listener-released=` として出し、1 を照らす |
| 文書 | `agent-rules/coding-rules/common.md`（iOS の節）、`testing.md`（ガードの表、層 1、層 2a）、実装の記録、トピックの README |

## 5. 確かめ

| # | 確かめること | どこで |
|---|---|---|
| V-1 | EditMode と Editor の PlayMode（`iOS Profile`。`Android™ Profile` の EditMode でも宣言の照合） | `verify_unity_ios.sh` |
| V-2 | Player のテスト（Clipboard 35 件と足した照らしを含む）。3 章の「同じ」が 1.x の記録と同じ値で通る | シミュレーター、実機（許可を得て） |
| V-3 | `ClipboardUITests` の 6 件（CB-1〜5、2b） | シミュレーター |
| V-4 | 実機で CB-5（名前付きの pasteboard だけ） | 実機（許可を得て） |
