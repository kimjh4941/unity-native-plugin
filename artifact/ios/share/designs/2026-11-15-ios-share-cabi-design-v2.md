# iOS Share の C ABI `2.0.0` への写し方の設計 v2

**v1 からの変更**: [レビュー v1](../reviews/2026-11-15-ios-share-cabi-design-review-v1.md) の 3 章の判断による（一覧は 6 章）。

## 基本情報

| 項目 | 内容 |
|---|---|
| 親 | [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（Dialog の次、2 つ目） |
| 目的 | `IosShareManager` を、3-S の「使えない」の経路から native-toolkit の C ABI `2.0.0`（`Share.h`）の呼び出しに替える。公開の API（`Share(payload, onResult)`、`ShareCompleted`、`IosShareResult`、`IosShareContentPayload`・`IosShareItem`）は変えない |
| 一次ソース | パッケージの `Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Share.h`・`Common.h`。native-toolkit の `ios/IosLibraryCApi/IosLibraryCApi/Share/{ShareReceiver,ShareMapping,ShareHandles}.swift`、`Common/{StructReader,CApiString,Registry,EntryError}.swift`、`ios/IosLibrary/IosLibrary/Share/Data/Repository/ShareRepositoryImpl.swift`、`Share/Presentation/ShareSheetPresenter.swift`、`Share/Domain/Model/ShareError.swift`、テスト `ios/IosLibraryCApiTests/{ShareTests.swift,StructLayoutTests.m}`、設計 `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md`（SH-01、P2-11、P2-15）、引き継ぎ `artifact/topics/ios-c-abi/results/2026-11-07-ios-c-abi-stage2c-handoff.md` |
| 1.x の記録 | [../results/2026-11-14-ios-share-1x-record-result-v1.md](../results/2026-11-14-ios-share-1x-record-result-v1.md)（UI の SH-1〜18、Player の SH-20〜25。実機あり） |
| 手本 | [Dialog の写し方の設計 v2](../../dialog/designs/2026-11-15-ios-dialog-cabi-design-v2.md) と、その実装（`IosDialogCApi.cs`、`IosNativeToolkitCApi` の UTF-8 の部品） |

## 1. C ABI の要点（一次ソースから）

| # | 事実 | 出典 |
|---|---|---|
| F-1 | 関数は `ntk_share_content_async(const ntk_share_request*, ntk_share_result_fn, void* user_data, ntk_release_fn)`、読み取りは `ntk_share_result_completed`（`int32_t`）、`ntk_share_result_activity_type`（`const char*`、`size_t* out_size`）、`ntk_share_result_free`。取り消しも要求の ID も無い | `Share.h:99-106` |
| F-2 | 構造体（`pack 8`）: `ntk_share_item` 24 バイト（`struct_size` 0、`reserved0` 4、`kind` 8、`reserved1` 12、`value` 16）、`ntk_share_request` 56 バイト（`struct_size` 0、`reserved0` 4、`items` 8、`item_count` 16、`subject` 24、`preview_title` 32、`excluded_activity_types` 40、`excluded_activity_type_count` 48）。項目の配列は各要素の `struct_size` で進み、大きさがそろわなければ `INVALID_PARAMETER` | `Share.h:76-96`、`StructLayoutTests.m:178-193`、`StructReader.swift:52-80` |
| F-3 | 種類は `TEXT 0`・`URL 1`・`IMAGE_FILE 2`・`FILE 3`。エラーは `NONE 0`、`INVALID_PARAMETER 1`、`NOT_INITIALIZED 2`、`NOT_SUPPORTED 3`、`UNKNOWN 4`、`OUT_OF_MEMORY 5`、`NOT_FOREGROUND 6`、`EMPTY_CONTENT 7`、`FILE_NOT_FOUND 9`、`INVALID_URI 17`、`IMAGE_LOAD_FAILED 18`、`SHOW_FAILED 19`、`CANCELED_BY_SYSTEM 20`、`ACTIVITY_FAILED 21`。`CANCELED` は無い（利用者が閉じたのは成功で `completed = 0`） | `Share.h:40-64`、part2 P2-11 |
| F-4 | 入口の拒み（コールバックは来ない。`release` は呼んだスレッドで済む）: `item_count == 0` は `EMPTY_CONTENT`。NULL のコールバック・要求、`struct_size`・`reserved*` の誤り、NULL の `items`（数が 1 以上）、範囲外の `kind`、NULL の `value`、NULL の除く要素、不正な UTF-8 は `INVALID_PARAMETER` | `ShareMapping.swift:37-79`、`ShareTests.swift:84-126`。`struct_size` が大きく、知らない部分に 0 でない値があれば `NOT_SUPPORTED`（`StructReader.swift:115-119`。C# は `Marshal.SizeOf` ちょうどを渡すので来ない） |
| F-5 | 完了のエラー: URL は前後の空白を除いて読み、http/https は host が要る、`file` は file URL なら受け付ける、それ以外の scheme は `INVALID_URI`。画像は `UIImage(contentsOfFile:)` で読めなければ（無いファイルも）`IMAGE_LOAD_FAILED`。ファイルは無ければ `FILE_NOT_FOUND`。項目は順に変換し、最初の失敗でシートを出さずに終わる。項目の確かめは前面の確かめより先。空の文字列は入口で受け付け、文字列はそのまま共有する。エラーに入力の URL や道は含まれない | `ShareRepositoryImpl.swift:33-95`、`ShareTests.swift:172-175`、part2 SH-01 |
| F-6 | 完了: `NONE` なら `result` があり、`completed` と、活動の種類（無ければ NULL、size 0）。エラーなら `result` は NULL。`NOT_FOREGROUND`（前面の view controller が無い）、`SHOW_FAILED`（落ち着かない、UIKit が断った）、`CANCELED_BY_SYSTEM`（外から消された。約 0.55 秒後。native-toolkit の `results/2026-10-31-ios-c-abi-stage1b-result.md:56, 91`）、`ACTIVITY_FAILED`（活動が失敗した）。`UNKNOWN` は通常は来ない（`ShareMapping.swift:98-100` の `@unknown default`）。`system_code` はいつも 0 | `Share.h:66-74`、`ShareMapping.swift:87-104`、`ShareSheetPresenter.swift:48-114` |
| F-7 | 文字列・配列・構造体は呼び出しの中で写される。返った直後に解放してよい | part1:59、`ShareTests.swift:257-268` |
| F-8 | 主スレッドで呼べば、完了は呼び出しの中では来ない。受け付けた呼び出しは完了がちょうど 1 回、その直後に `release`（NULL でよい）。`user_data` は読まれずに返る。ホストが window や root を替えてシートが解放されなければ、完了も `release` も来ない | `Share.h:26-33, 69-72`、`Registry.swift` の `complete`（`isCompleted` の印） |
| F-9 | 続けて呼ぶと、呼び出しごとに完了が来る（待ち行列も入口の拒みも無い）。順は約束しない。表示中のシートの上に 2 つ目を出せるか、`SHOW_FAILED` になるかは測られていない | `Share.h:72`、`ShareTests.swift:392-422`、1b の作業記録 512 |
| F-10 | iPad の popover は表示する view の中央が基点（渡す口は無い。1.x の `1.13.0` も中央）。初期化も Info.plist のキーも要らない | `PresentationProcedure.swift:303-317`、part2:807 |
| F-12 | 1.x（native-toolkit のタグ `1.13.0`）の失敗の文: `Invalid URL: <URL>.`、`Failed to load image at path: <道>.`、`File not found at path: <道>.`、`No root view controller available to present the share sheet.`、`Failed to present the share sheet: <理由>.`（表示の失敗も活動の失敗もこれ。落ち着いていない画面には待たずにすぐ失敗し、理由は `The root view controller is not in a stable state …`）。外から消されたときと UIKit が断ったときは完了が来なかった | `1.13.0` の `ShareError.swift:44-45`、`ShareSheetPresenter.swift:52-55, 81-84`、1b の作業記録 484-492 |
| F-13 | 文字列は NUL 終端の C 文字列として読まれる。途中の U+0000 から後ろは届かない（1.x は JSON で `\u0000` にして全文が届いた） | `CApiString.swift:62`、`93e6b8f` の `IosShareJsonBuilder.cs` の 141〜159 行 |
| F-11 | 選べるものは 1.x と同じ 3 つ（`subject`、`preview_title`、`excluded_activity_types`）。NULL は無し、`""` は値。`subject` か `preview_title` があると 1 つ目の項目を `ShareItemSource` で包む | `Share.h:91-94`、`CApiString.swift:20-25`、`ShareRepositoryImpl.swift:47-52` |

## 2. 決めること

| # | 決定 | 理由 |
|---|---|---|
| D-1 | **呼び出しごとに受け手を持つ**。`user_data` は呼び出しの番号（1 から増える `long`）、`release` は NULL。番号から受け手を引く表（internal の純粋なクラス `IosSharePendingCalls`。lock の中では辞書の読み書きだけ）に、要求を組み立て終えた後、**呼ぶ前に**入れ、完了で取り出す。拒まれたら（`NONE` 以外）と例外のときは自分で取り出す。**`OnDestroy` で表を空にする**（1.x が `s_onShare` を消したのと同じ）。表に無い番号の完了（破棄の後に来たもの）は、生きている Manager の `ShareCompleted` だけに出し、無ければ捨てる（`Instance` で作り直さない）。ホストがシートを持ち続けると完了が来ず、その番号は表に残る（F-8、既知） | 1.x の `s_onShare` の後勝ちで、2 回続けた呼び出しの結果が失われていた（SH-13）。F-9 で呼び出しごとに完了が来るので、それぞれの受け手に届ける。別のスレッドから呼ばれると完了が先に来うる（F-8）ので、表には呼ぶ前に入れる |
| D-2 | **項目は 1.x と同じに整える**（native-toolkit の P2-15 は「包みも補正しない」だが、Android の J-N5〈`common.md`〉と同じく、1.x の JSON の段の補正は保つ）: null の項目、`type` が `text`・`url`・`image`・`file` 以外の項目、`value` が null の項目を C# で捨て、`Debug.LogWarning` を 1 行出す（捨てた件数と `type` だけ。値や道は出さない）。`type` の比べ方は大文字と小文字を区別する ordinal（1.x の Swift の `switch` と同じ）。残りが 0 なら `"No shareable items were provided."`（1.x の文言） | 1.x は Builder が null の項目を、Swift のパーサーが未知の `type` と値の無い項目を、黙って捨てていた（SH-16・23・24・25 の記録）。2.0.0 は捨てずに `INVALID_PARAMETER`（F-4）なので、C# で 1.x と同じに整える。互換を取り、警告で気づけるようにする |
| D-3 | **除く活動の一覧に null の要素が 1 つでもあれば、一覧をまるごと捨てる**（1.x と同じ）。`Debug.LogWarning` を 1 行出す | 1.x はパーサーの `as? [String]` が失敗して一覧をまるごと捨てていた（SH-17）。2.0.0 は null の要素を `INVALID_PARAMETER` で断る。D-2 と同じく 1.x の JSON の段の補正を保ち、警告で気づけるようにする。SH-17 は「同じ」 |
| D-4 | `subject`・`previewTitle` は、`string.IsNullOrWhiteSpace` の値を NULL にして渡す | 1.x の Builder と同じ（空白だけを省いた）。2.0.0 では `""` が値になり 1 つ目の項目を包む（F-11）ので、1.x と同じく省く |
| D-5 | 種類は `text` → `TEXT`、`url` → `URL`、`image` → `IMAGE_FILE`、`file` → `FILE`。`value` はそのまま UTF-8 で渡す（空文字も）。**値・`subject`・`previewTitle`・除く活動に U+0000 があれば `Debug.LogWarning` を出す**（そこで切れる。Android と同じく切れる形を既知の差分にする） | F-3。空の文字列は 2.0.0 が判断する（F-5）。C の文字列は途中の U+0000 で切れる（F-13）。黙って切らないよう警告にする |
| D-6 | **順は「payload が null か項目が空（今の C# の確かめ）→ `IsAvailable` → 整え（D-2〜D-5）→ 組み立て → 表に入れる → 呼ぶ」**。C# で断った失敗、使えないとき、拒まれたとき、例外のときの結果は、今と同じく次の `Update` に出す（`ShareCompleted` → 受け手の順）。使えない（Editor、別の版）→ `"iOS share is only available on an iOS device."`（今の Editor の文言。整えより前なので、どんな項目でもこれ）。拒まれた → `"Failed to share: the request was rejected (<名前>)."` と `Debug.LogError`。例外 → 表から取り出し、`Debug.LogError` と `"Internal error: <ex.Message>"`（1.x と同じ） | 1.x の時点（`FireResult` で次の `Update`）と同じ。Editor の文言は今の Editor のテストと同じ |
| D-7 | **完了は主スレッドで読み、`ntk_share_result_free` で解放してから、次の `Update` に出す**（`ShareCompleted` → その呼び出しの受け手）。`ShareCompleted` は完了の時点で `_instance?.ShareCompleted` から写す（1.x と同じ）。`NONE` → `IosShareResult.Success(completed != 0, activityType)`。読めなければ `UNKNOWN` の失敗にする | 1.x の時点と順。結果の handle は受け取った回の中で解放する |
| D-8 | 完了のエラーの文言は、1.x（F-12）の文から入力の URL や道を除いたもの: `INVALID_URI` → `Invalid URL.`、`IMAGE_LOAD_FAILED` → `Failed to load image.`、`FILE_NOT_FOUND` → `File not found.`、`NOT_FOREGROUND` → `No root view controller available to present the share sheet.`、`SHOW_FAILED` → `Failed to present the share sheet.`、`ACTIVITY_FAILED` → `Failed to present the share sheet: the share activity failed.`（1.x の前置きを保つ）、`CANCELED_BY_SYSTEM` → `The share sheet was dismissed without a result.`（1.x には無かった）、ほか → `Failed to share: <名前>.` | 2.0.0 のエラーは数だけで、URL や道や OS の理由を取り戻せない。文の形と前置きは 1.x に合わせ、利用者の文字列の照合が壊れにくいようにする。**既知の差分**（3 章） |
| D-9 | 宣言と変換は新しい `Runtime/iOS/Share/IosShareCApi.cs`（`#if UNITY_IOS \|\| UNITY_EDITOR`）。`extern` は `!UNITY_EDITOR` の外。宣言は使う 4 つ（`ntk_share_content_async`、`ntk_share_result_completed`、`ntk_share_result_activity_type`、`ntk_share_result_free`）。コールバックの本体と呼び出しは `UNITY_IOS && !UNITY_EDITOR`。C# の型: `ShareItem` は `uint, uint, int, uint, IntPtr`、`ShareRequest` は `uint, uint, IntPtr, UIntPtr, IntPtr, IntPtr, IntPtr, UIntPtr`、`ntk_share_content_async` は `Int32(ref ShareRequest, IntPtr, IntPtr, IntPtr)`、コールバックは `Void(IntPtr, Int32, UInt32, IntPtr)`（**Dialog と違い要求の ID が無い**） | Dialog と同じ決まり（`common.md` の iOS の節）。`IosCApiDeclarationTests` の名前と署名の表に足す |
| D-10 | 項目の配列は、`ntk_share_item` を数だけ並べた 1 つの塊を `IosNativeToolkitCApi.Alloc` で確保し、`Marshal.StructureToPtr` で書く。各要素の `struct_size` は同じ 24 | F-2 |
| D-11 | `IosShareJsonBuilder` とそのテストは段 4 まで残す（使われなくなる） | README の段 4 の行（JSON Builder の削除） |
| D-13 | Manager の流れ（表に入れる → 呼ぶ → 拒み・例外で取り出して出す）は、ネイティブの呼び出しを引数で受ける internal の静的な関数にする（Player では本物の `extern` を、Editor の PlayMode のテストでは偽の呼び出しを渡す）。完了の受け口と `s_resultCallbackPointer` も internal にし、Player のテストで使う | 拒みと例外の後処理（表から取り出す、次の `Update` に 1 回）を、画面を出さずに確かめるため |
| D-12 | 購読者の例外は今の `InvokeInOrder` のまま（`ShareCompleted` か受け手が投げたら、残りを呼ばずに `LogError`） | 1.x と同じ。dispatcher の `Update` からは漏れない |

## 3. 振る舞いの対応（1.x の記録と `2.0.0` の見込み）

| 場面 | 1.x（記録） | `2.0.0`（この設計） | 区分 |
|---|---|---|---|
| SH-1〜8、14、17、20〜22 | 記録の行 | 同じ（SH-17 は D-3。警告のログが増える） | 同じ |
| SH-16・23・24・25（未知の `type`、`[null]`、値が null） | 捨てる | 同じ（D-2。警告のログが増える） | 同じ |
| SH-9・10（有効でない URL） | `Invalid URL: not a url.` | `Invalid URL.`（D-8） | 既知の差分（文言） |
| SH-11（無い画像） | `Failed to load image at path: <道>.` | `Failed to load image.` | 既知の差分（文言） |
| SH-12（無いファイル） | `File not found at path: <道>.` | `File not found.` | 既知の差分（文言） |
| SH-13（同じフレームで 2 回） | 1 つの呼び出しの結果が失われる | 呼び出しごとにちょうど 1 回（D-1）。値は閉じた成功、`Failed to present the share sheet.`、`The share sheet was dismissed without a result.` のどれか。実測で記録する（F-9） | 既知の差分 |
| SH-15（前面で非活性） | `No root view controller …` が 1 回、出ない | 出て、閉じれば `completed=False`（前面で非活性は前面） | 既知の差分 |
| SH-18（背景で呼ぶ） | 前に戻した後に `No root view controller …` が 1 回 | 同じ文言で 1 回（`NOT_FOREGROUND`。完了は前に戻った後に来る見込み） | 同じ見込み（実測で確かめる） |
| 外から消された | 0 回 | `The share sheet was dismissed without a result.` が 1 回（シートが解放されたとき） | 既知の差分。UI のテストは無い |
| 活動の失敗 | `Failed to present the share sheet: <OS の理由>.` | `Failed to present the share sheet: the share activity failed.`（D-8） | 既知の差分（文言）。UI のテストは無い |
| 表示が断られた・落ち着かない画面の上 | 待たずにすぐ `Failed to present the share sheet: The root view controller is not in a stable state …`。UIKit が断れば完了が来ない | 1 回待って出すか、`Failed to present the share sheet.` が 1 回 | 既知の差分。SH-13 で一部を見る |
| 値などの途中の U+0000 | 全文が届く | そこで切れる。`LogWarning` が出る（D-5） | 既知の差分（Android と同じ） |
| iPad の基点 | 画面の中央 | 同じ（F-10） | 同じ（確かめない） |
| Editor（iOS のターゲット） | `iOS share is only available on an iOS device.` | 同じ（D-6） | 同じ |

## 4. 変えるもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Share/IosShareCApi.cs`（新） | 2 つの構造体、種類とエラーの値、`ResultCallback`、D-9 の `extern`、項目の整え（D-2〜D-5）、要求の組み立て（D-10）、文言（D-6・D-8）、結果の読み取り（`UNITY_IOS && !UNITY_EDITOR`） |
| `Runtime/iOS/Share/IosSharePendingCalls.cs`（新） | D-1 の表（入れる、取り出す、空にする、1 から増える番号） |
| `Runtime/iOS/Share/IosShareManager.cs` | 3-S の経路を消す。`s_onShare` を番号の表に替える（D-1）。D-6 の順、D-13 の流れ。静的な `ResultCallback`（`[MonoPInvokeCallback]`、例外を外に出さない。読めなければ `UNKNOWN`）。XML ドキュメントを直す |
| `Tests/Runtime`（EditMode） | 構造体の大きさと全項目の位置、整え（null・未知の `type`・値が null・除く一覧の null・空白の subject）、組み立て、文言、`IosCApiDeclarationTests` の名前と署名（+4）とコールバックの署名、表（入れる → 取り出す、無い番号は null、空にする、番号が増える）、U+0000 の警告の判定 |
| `Tests/PlayMode`（Editor） | 今の `IosShareManagerIntegrationTests` が通ること（Editor の経路は文言も時点も同じ）。足す: 未知の `type` だけの payload も Editor では `iOS share is only available …`。D-13 の流れに偽の呼び出しを渡し、拒み（`INVALID_PARAMETER`）・例外のときに表から取り出され、次の `Update` にイベント → その受け手が 1 回ずつ、ほかの保留の呼び出しは残る。破棄の後の完了（表に無い番号）は受け手に出ない |
| Player のテスト | `IosPlayerBuildSetup` に `NTK_IOS_CABI_2_SHARE`。SH-23〜25 は 1.x と同じ期待で流れる。Share の `UnavailableUntilMoved` を消す。足す: `struct_size` 0 の要求は `INVALID_PARAMETER` で完了が来ない。表に 2 つ入れ（internal の口）、完了の受け口を順を逆に呼ぶと、それぞれの受け手に 1 回ずつ、次の `Update` に |
| UI のテスト | `IosUiHarnessBuild` に `NTK_IOS_CABI_2_SHARE`。`notMovedYet` から `ShareUITests` を外す。`ShareUITests` に `[abi@event]` の読み取りを足し、`2` のとき 3 章の既知の差分の期待で照らす（SH-9〜13、15、17）。SH-13 は、出ているシートをすべて閉じ、`share1`・`share2` が 1 本ずつ、`share-event` が 2 本で、各値が 3 章の 3 つのどれかであることを照らし、実際の行をテストの出力に書く |
| 文書 | `agent-rules/coding-rules/testing.md`（ガードの表と層 1・2a の表の Share の iOS）、`common.md` の iOS の節（Share は呼び出しごとの表を持ち、すべての結果を次の `Update` に出す。Dialog と違う）。実装の記録とトピックの README の段 3 の行 |

## 5. 確かめ

実機の回は、シミュレーターで全件成功してから流す。

| # | 確かめること | どこで |
|---|---|---|
| V-1 | EditMode と Editor の PlayMode が通る（`iOS Profile`。`Android™ Profile` の EditMode でも宣言の照合が通る） | `verify_unity_ios.sh`、Unity の CLI |
| V-2 | Player のテスト（SH-20〜25、4 章で足す 2 つ、ほかの機能）。`iOS Profile` と `--as-package-user` | シミュレーター、実機（許可を得て） |
| V-3 | `ShareUITests` の 18 件が `2.0.0` の期待で通る。3 章の「同じ」の行が 1.x の記録と全文で同じ。SH-13 の 2 つ目の値を記録する | シミュレーター |
| V-4 | 実機で `ShareUITests` の実機の 11 件（SH-1〜5、9〜13、16） | 実機（許可を得て） |
| V-5 | `test_ios_ui.sh` の既定の一覧が `DialogUITests` と `ShareUITests` になる | ランナー |
| V-6 | サンプルの画面（`IosShareManagerExampleController`。`subject` と複数の画像・ファイル）を実機で手で一度見る。利用者に頼む項目として結果に書く | 実機（利用者） |

## 6. v1 からの変更

レビュー v1 の 3 章の判断による。F-5（`file:`）、F-4・F-6・F-8（出典）、F-12・F-13（新）、D-1（破棄、表に無い番号、純粋なクラス）、D-2（P2-15 と J-N5）、D-3（1.x と同じく一覧をまるごと捨てる）、D-4（`IsNullOrWhiteSpace`）、D-5（U+0000 の警告）、D-6（順と例外）、D-7（`_instance` から写す）、D-8（1.x の出典と `ACTIVITY_FAILED` の前置き）、D-9（C# の型とコールバック）、D-13（新）、3 章（SH-17、活動の失敗、表示が断られたとき、U+0000、iPad、SH-13）、4 章、V-6
