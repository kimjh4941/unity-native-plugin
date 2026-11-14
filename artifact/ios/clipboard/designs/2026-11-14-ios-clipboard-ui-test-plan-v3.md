# iOS Clipboard 自動テスト計画 v3

v1 からの変更は [レビュー v1](../reviews/2026-11-14-ios-clipboard-ui-test-plan-review-v1.md)、v2 からの変更は [レビュー v2](../reviews/2026-11-14-ios-clipboard-ui-test-plan-review-v2.md) の 3 章の判断による（6 章と 7 章に一覧）。

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | `IosClipboardManager`（`Runtime/iOS/Clipboard/IosClipboardManager.cs`）の 15 の操作（Copy、Append、Clear、RemovePasteboard、CancelLoads、StartObserving、StopObserving、Read、ReadData、GetSnapshot、CreatePasteboard、DetectPatterns、DetectValues、LoadItem、CheckForegroundChange）と 10 のイベント |
| 目的 | iOS の C ABI `2.0.0` への移行（[artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md)）の段 3-R。**1.x のまま記録**し、段 3 で Clipboard を移した後に同じテストを流して、1.x と同じか、既知の差分かを確かめる |
| 実行 | **ほとんどは画面を出さない iOS の Player のテスト**（`scripts/verify_unity_ios.sh`、`Tests/PlayMode/IosClipboardPlayerTests.cs`）。**名前付きの pasteboard だけ**を使うので、実機でも流せる。ペーストの許可のダイアログを押すものと、破棄の後を見るものだけ UI のハーネス（`scripts/test_ios_ui.sh`、`ClipboardUITests`） |
| 書き方 | 結果は、各結果の型の値（`IsSuccess`、エラーの `Code` と `Message`、項目の数と型の一覧、文字列、バイト列の長さとハッシュ）を NUnit で照らす。UI の行の書き方は Dialog の計画 v3 の 1 章と同じ |
| 1.x の振る舞いの出典 | native-toolkit のタグ `1.13.0` の `ios/UnityIosPlugin/UnityIosPlugin/Clipboard/` と `ios/IosLibrary/IosLibrary/Clipboard/`（エラーのコードと文言は `Domain/Model/ClipboardError.swift`、確かめは `Application/UseCase/ClipboardContentValidator.swift` と `Data/Repository/ClipboardTypeIdentifierValidator.swift`）。C# の 1 回に 1 つの規則（`TryStartOperation`）と、引数の確かめ（`IosClipboardPayloads.cs`）は、このリポジトリ |
| `2.0.0` との差 | native-toolkit の `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 の CL-01〜CL-15 と、その前書き（エラーの文言は値から写す。`error.details` の domain と code は保てない〈D-13〉。`callback` の `NULL`）、`Clipboard.h` |
| 移行前の基準 | 段 2 の (A) の `NamedPasteboardRoundTripsPlainText` だけ。ほかは無い。1.x の列は予想で、**実測が違えば記録の文書に書く**（Dialog の ID-14a で、一次ソースの読みが 2 人とも外れた。読みより流して決める） |

**プライバシー**: 一般の pasteboard の中身はテストの中で読まない・書かない（2 章の、シミュレーターだけの項目を除く）。結果の行と NUnit の失敗の文に、テストが決めた値以外の中身を出さない。`Unique` で作った pasteboard の名前は記録に書かない。

## 1. 画面を出さないテスト（iOS の Player、名前付きの pasteboard）

どのテストも、自分の名前付きの pasteboard（`com.jonghyunkim.nativetoolkit.playertests.<テストの名前>`）を作り、終わりに消す（3 章の約束）。エラーは `Code` と `Message` を全文で照らす（文言は `ClipboardError.swift` の固定の英文）。

### 1.1 読み書き

| ID | 何をするか | 1.x の予想 | `2.0.0` の期待 |
|---|---|---|---|
| CP-1 | PlainText を Copy して Read | 1 項目、`Text` が同じ（今の `NamedPasteboardRoundTripsPlainText`） | 同じ |
| CP-2 | HtmlText（plain と html）を Copy して Read、`ReadData("public.html")` | 1 項目に plain の型（`UTType.plainText` の `public.plain-text`。実測の型の一覧を記録する）と `public.html`。html のバイト列が同じ | 同じ |
| CP-3 | Url を Copy して Read | `UrlString` が同じ | 同じ |
| CP-4 | ImageData（小さな PNG）を Copy して `ReadData("public.png")` | バイト列の長さとハッシュが同じ（再圧縮されれば、型だけを記録する） | 同じ |
| CP-5 | Color を Copy して Read | 記録する（型の一覧） | 同じ |
| CP-6 | CustomData（`com.jonghyunkim.ntk.custom`）を Copy して `ReadData` | バイト列が同じ | 同じ |
| CP-7 | 無い型で `ReadData` | 成功で、データが null | 同じ（`ntk_bytes` が `NULL`、CL-04） |
| CP-8 | MultipleText（2 つ）を Copy して Read | 2 項目 | 同じ |
| CP-9 | MultiRepresentation（2 つの型）を Copy して Read | 1 項目に 2 つの型 | 同じ |
| CP-10 | Copy の後に Append（公開の `Append` には options の引数が無い） | 項目が 2 つになる | 同じ |
| CP-11 | Clear して Read | 成功で 0 項目（空は失敗ではない） | 同じ |
| CP-12 | MultipleText（1 つ）を Copy し、Url を Append して 2 項目にしてから `GetSnapshot(matchingTypes: ["public.url"])` | `NumberOfItems = 2`。`TypeIdentifiers` は最初の項目の型だけ（plain text）、`AllTypeIdentifiers` は 2 要素で 2 つ目に `public.url`。`MatchingItemIndexes` が url の項目の添字（`[1]`）だけ（`ClipboardRepositoryImpl.snapshot`、`ClipboardSnapshot.swift`） | 同じ |
| CP-13 | URL を Copy して `DetectPatterns([ProbableWebURL])` | その模様が見つかる。ペーストの許可は求めない | 同じ |
| CP-14 | URL を Copy して `DetectValues([ProbableWebURL])` | 値として URL が返る | 同じ（封筒は包みが付ける、CL-10） |
| CP-15 | 文字列を Copy して `LoadItem(Text)`、`LoadItem(Url)` | Text は文字列。Url は記録する（成功なら値は null でない。無ければ `CLIPBOARD_NO_MATCHING_ITEM` の見込み、`firstMatchingProvider`） | 同じ |
| CP-18 | `CheckForegroundChange` を 2 回（間に Copy） | 1 回目は `changed = false`（追跡の初期化）、2 回目は `changed = true` の見込み | 同じ |
| CP-19 | `CreatePasteboard(Unique)` して `RemovePasteboard`、その後にその scope を Read | 空でない名前の scope が返る。消した後の Read は `CLIPBOARD_UNAVAILABLE`（CP-25 と同じ） | 同じ |
| CP-22a | 期限を数秒先（3 秒）にして `Copy(options: Create(localOnly: true, expirationDate: 今 + 3 秒))`、すぐ Read、6 秒待って Read | すぐは 1 項目。6 秒後は記録する（0 項目の見込み） | 同じ |

### 1.2 エラーの写し（ネイティブまで届く失敗）

| ID | 何をするか | 1.x の予想（`Code`、`Message`） | `2.0.0` の期待 |
|---|---|---|---|
| CP-20 | `IosPasteboardCreationRequest.Named("   ")`（空白だけ） | C# が**同期で `ArgumentException`** を投げる（`IosClipboardPayloads.cs`）。ネイティブには届かない | 同じ（C# の確かめを保つ） |
| CP-21 | `RemovePasteboard(General)` | `CLIPBOARD_CANNOT_REMOVE_GENERAL`、`The general pasteboard cannot be removed.` | 同じ（CL-08） |
| CP-22 | 先に PlainText を Copy し、次に期限を過去にした Copy、その後に Read | 2 つ目の Copy が `CLIPBOARD_INVALID_EXPIRATION`、`expirationDate must be in the future.`。Read は**元の 1 項目が残る**（`validateExpirationDate` は書く前に断る） | 同じ |
| CP-25 | 作って消した名前付きの pasteboard に Read | `CLIPBOARD_UNAVAILABLE`、`The requested pasteboard is unavailable.` | 同じ |
| CP-26 | 文字列を Copy して `LoadItem(Image)` | `CLIPBOARD_NO_MATCHING_ITEM`、`No clipboard item matches the requested type.` | 同じ |
| CP-27 | `Color(1.5, 0, 0, 1)` を Copy | `CLIPBOARD_INVALID_COLOR`、`Color components must be finite and within 0.0...1.0.` | 同じ |
| CP-28 | CustomData の型をドットの無い `ntkcustom` にして Copy | `CLIPBOARD_INVALID_TYPE`、`The uniform type identifier is invalid.` | 同じ |
| CP-29 | ImageData に、どの画像の形式としても解けない空でないバイト列（ASCII の `not an image`。`public.png` と名乗る）を Copy | `CLIPBOARD_INVALID_IMAGE_DATA`、`The provided image data could not be decoded.` | 同じ |
| CP-30 | Url に `not a url` を Copy | `CLIPBOARD_INVALID_URL`、`The URL is invalid.`（`validateURL` は scheme が取れなければ断る） | 同じ |
| CP-23 | 同じ種類の操作を 2 つ続けて呼ぶ（1 つ目の完了の前） | 2 つ目は C# の 1 回に 1 つの規則で失敗（`CLIPBOARD_BUSY`、`TryStartOperation`）。1 つ目は成功。共通のイベントは 2 回 | 同じ（C# の規則を保つ） |

どの失敗でも `Domain` と `NativeCode` を記録する。1.x の操作のコールバックには details が無いので、ここでは null の見込み。D-13（domain と code を保てない）は、details が付く検出の失敗（CB-3）で確かめる。

### 1.3 取り消しと監視

| ID | 何をするか | 1.x の予想 | `2.0.0` の期待 |
|---|---|---|---|
| CP-16 | 読み込み中でないときに `CancelLoads` | 成功 | 同じ |
| CP-31 | 同じフレームで `LoadItem(Text)` と `CancelLoads` | 読み込みは `CLIPBOARD_CANCELLED`（`The clipboard load was cancelled.`）がちょうど 1 回、**読み込みの成功は 0 回**、CancelLoads の成功が 1 回。読み込みが成功したらテストの失敗（1.x は取り消しの Task が読み込みの Task より先に main の列に積まれ、必ず取り消す。`UnityIosClipboardManager.swift` と `ClipboardItemLoaderImpl.swift`）。**順序は記録だけ**（1.x は CancelLoads の完了が先の見込み） | 同じ（包みが未完了を覚えて 1 つずつ取り消す、CL-12。順序は約束しない） |
| CP-17 | `StartObserving`、Copy、3 秒待つ、`StopObserving` | `ClipboardChanged` と `onChanged` を**別々に数え**、どちらもちょうど 1 回の見込み。`Kind` と `TypesAdded` を記録する。0 回なら「名前付きの pasteboard では通知が来ない」と記録し、テストは 1.x の実測に合わせる。そのときは CP-32・33・35 と CB-5 も「確かめられない」と記録して外す（何も確かめないまま通るため）。CL-13・14 を別の手段で確かめるかは記録の文書で決める | 同じ（受け手を 1 つにする、CL-13） |
| CP-32 | `StartObserving(onChanged: A)` の完了の後に `StartObserving(onChanged: B)`、Copy、3 秒待つ | A は 0 回、B は 1 回、`ClipboardChanged` は 1 回（2 つ目の Start が前の監視を止める） | 同じ（包みが前の登録を外す、CL-13） |
| CP-33 | Start、Stop（完了を待つ）、Copy、3 秒待つ | 変化のイベントは 0 回 | 同じ（CL-14） |
| CP-34 | Start の完了の前に Stop | Stop が `CLIPBOARD_BUSY`（Start と同じキー `observation`）。Start は成功 | 同じ（C# の規則） |
| CP-35 | 監視中に、その pasteboard を `RemovePasteboard` | 記録する（`Kind = Removed` が来るか、何も来ないか） | 同じ |

## 2. UI のハーネスのテスト（`ClipboardUITests`）

### 2.1 ペーストの許可（シミュレーターだけ）

一般の pasteboard に、XCUITest のプロセスから別のアプリとして値を書き（`UIPasteboard.general.string = "ntk ui harness paste"`）、ハーネスがそれを読む。iOS 16 以降は、ほかのアプリが書いた値を読むときに許可のダイアログを出す。**実機では流さない**（一般の pasteboard を書き換える。native-toolkit の CB1 では、実機で許可のダイアログが出なかった）。

ダイアログを押す前に、出どころが UI テストのランナーであることを照らす（native-toolkit の `pasteAlertNamesKnownSource` と同じ。ダイアログの文に、ランナーの名前が入っていることを見る）。結果の行には、読んだ文字列の中身ではなく長さと型の数だけを書く。

| ID | ハーネスの行 | XCUITest がすること | 1.x の予想（結果の行） | `2.0.0` の期待 |
|---|---|---|---|---|
| CB-1 | 一般の pasteboard を Read | 「ペーストを許可」 | `[read@ok] items=1 types=<n> text-length=20`（`n` は実測を記録する） | 同じ |
| CB-2 | 同じ | 「ペーストを許可しない」 | 記録する（`Clipboard.h` には「失敗ではなく、型も文字も無い 1 項目」とある。`[read@ok] items=1 types=0 text-length=<null>` の見込み） | 同じ |
| CB-2b | 一般の pasteboard に `LoadItem(Text)` | 「ペーストを許可しない」 | 記録する（`CLIPBOARD_LOAD_FAILED` の見込み。`Domain` と `NativeCode` も行に書く） | 文言は値から写す。domain と code は保てない（D-13） |
| CB-3 | 一般の pasteboard に `DetectValues([ProbableWebURL])`（XCUITest が URL を書いておく） | 「ペーストを許可しない」。**ダイアログが出てから 5 秒以内に押す**（1.x の検出の待ち時間は 5 秒で、答える時間も含む） | 記録する（`CLIPBOARD_DETECTION_FAILED` の見込み。`Domain` と `NativeCode` を行に書く）。**`CLIPBOARD_TIMED_OUT` ならテストの失敗**にして流し直す | 検出の失敗で値は NULL。domain と code は保てない（D-13）。文言は値から写す |
| CB-4 | 一般の pasteboard に `DetectPatterns` | 許可のダイアログが出ないことを確かめる | 模様が返り、ダイアログは出ない見込み | 同じ |

### 2.2 破棄の後（シミュレーターと実機、名前付きの pasteboard だけ）

| ID | ハーネスの行 | XCUITest がすること | 1.x の予想（結果の行） | `2.0.0` の期待 |
|---|---|---|---|---|
| CB-5 | 名前付きの pasteboard で `StartObserving`（完了を待つ）→ manager の GameObject を破棄 → `IosClipboardManager.IsTerminated == true` を確かめ、0.5 秒待つ → オーバーレイ（`.mm`）がネイティブでその pasteboard に書く → 3 秒後に行を書く。行は `[after-destroy@event] terminated=True late-change=<n> native-listener=<n|none>`。`late-change` は C# が捨てた**変化のイベント**の数（`Discarded a late startObserving callback`。停止や取り消しの遅い完了の警告は数えない）。`native-listener` は、`.mm` が `dlsym` で `ntk_clipboard_add_change_listener` を見つけたとき（`2.0.0`）だけ、破棄の前に足した自分の受け手に、書き込みの後で届いた数。1.x では見つからず `none` | 行を押し、結果の行を待つ | `[after-destroy@event] terminated=True late-change=0 native-listener=none`（`OnDestroy` が `clipboardStopObserving(null)` でネイティブの監視を止める） | `late-change=0 native-listener=0`。`2.0.0` ではリスナーを外しても監視は続く（`Clipboard.h` の `ntk_clipboard_listener_remove`）ので、止まったことは `.mm` の受け手で見る。包みは停止に**空の受け手**を渡す（part2 の 8.3 と P2-28。`NULL` は `INVALID_PARAMETER` で止める処理が行われない） |

CB-5 は、名前付きの pasteboard への書き込みで変化のイベントが来るとき（CP-17 で 1 回以上）にだけ意味を持つ。CP-17 が 0 回なら、CB-5 は記録に「確かめられない」と書いて外す。`.mm` の `dlsym` は、1.x のまま組んでもリンクの誤りにしないため（1.x の framework にその関数は無い）。警告は `Application.logMessageReceivedThreaded` で数える（ネイティブのコールバックのスレッドに依らない）。

manager を破棄すると、その起動の間はどの操作も断られる（`s_isTerminated`）。ハーネスはテストごとに入れ直して起動するので、ほかのテストには響かない。Player のテストには入れない（同じ起動の後のテストがすべて断られる）。

## 3. テストの共通の約束

- Player のテストのカテゴリーは `IosPlayer`。名前は `IosClipboardPlayerTests.CP<番号>_<意味>`
- UI のテストの名前は `ClipboardUITests/test_CB<番号>`。CB-1〜4（2b を含む）は `simulatorOnly`、CB-5 は実機でも流す
- 段 2 のパイロットの UI のテスト `ClipboardUITests/test_namedRoundTrip` は、CP-1 と同じものなので消す
- **scope を書き忘れない**: どの API も scope の省略は一般の pasteboard になる。Player のテストは、scope を必ず引数に取る補助の関数（例: `Copy(scope, content)`）だけを通して manager を呼び、テストの本文から manager を直に呼ばない。例外は、CP-21 の `General`（読みも書きもしない `RemovePasteboard`）と、scope を取らない `CancelLoads`、`StopObserving`、`CreatePasteboard(Unique)`
- **後始末**: 各テストは `finally`（`[UnityTearDown]`）で自分の pasteboard の `RemovePasteboard` の完了を待つ。監視を始めたテストは `StopObserving` の完了を待つ。呼んだ操作の完了を待ち時間（10 秒）で待ち、来なかった操作が 1 つでもあれば、そのテストを失敗にする（残った操作は次のテストを `CLIPBOARD_BUSY` にするので、原因をそのテストに帰す）。未完了は、補助の関数が呼んだ数と完了の数を自分で数えて見る（manager の未完了の集合は Editor でしか読めない）
- **観測の窓**: イベントを数える項目は、最後の操作の後 3 秒待ってから数える（Dialog の計画 v3 の 1 章と同じ）
- 実機の区分は、Player のテストは `PlayerTests/IosPlayer`（名前付きの pasteboard だけ）。CB-5 は `clipboard`。`DeviceRequirements.json` の `clipboard` の説明は「名前付きの pasteboard だけを書き、一般の pasteboard を読み書きしない」に直し、CB-1〜4 は `simulatorOnly` に入れる

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| 実機での一般の pasteboard | 私物の端末のクリップボードを書き換える。許可のダイアログも出ないことがある（native-toolkit の CB1） |
| CL-02（options を持つ Append の拒否） | 公開の `Append` に options の引数が無く、C# は `options` のキーを書かない。C# から届かない |
| CL-07（空白だけの名前の `CLIPBOARD_INVALID_NAME`） | C# の `Named` が先に `ArgumentException` を投げる（CP-20） |
| CL-08 の scope の省略 | C# の `scope must not be null` で止まる |
| CL-09 の未知の種類の値 | C# の enum で止まる |
| ペーストのコントロール（`UIPasteControl`） | 1.x の API に無い |
| 背景でのクリップボードの変化の通知 | アプリが背景にある間の変化は、前に戻ったときの `CheckForegroundChange` で見る（CP-18） |
| 画像のファイルの読み込みの中身の見た目 | 手で見る（native-toolkit でも同じ） |

## 5. 1.x の記録

記録は別の文書に書く（計画の版は上書きしない）。

## 6. v1 からの変更

| 指摘 | 変更 |
|---|---|
| R-1、Codex の CP-20・CP-10 | CP-20 は C# の `ArgumentException`。CP-10 の `2.0.0` の列から options を外した。CL-02・07・08・09 を 4 章へ |
| R-2、Codex の CP-22 | CP-22 は「過去の期限の Copy が失敗し、元の内容が残る」。期限そのものは CP-22a に分けた |
| R-3、Codex の CP-12 | 2 項目にして、`MatchingItemIndexes` を照らす |
| R-4、Codex の CP-24 | 1.2 にネイティブまで届く失敗（CP-25〜30）を足し、`Code` と `Message` を全文で照らす。`Domain`・`NativeCode` は CB-2b と CB-3 で記録する。表記を実物に合わせた |
| R-5、Codex の CP-16 | CP-31（読み込み中の CancelLoads） |
| R-6、Codex の CP-17 | CP-32〜35 |
| R-7 | CB-5（破棄の後）。破棄の後の C# は操作を断るので、Player のテストでは Copy できない。ハーネスのネイティブの側で書き、C# が捨てた遅い呼び出しの数を見る |
| R-8 | CB-2 の行に型の数。CB-2b を足した。CB-3 は 5 秒以内に押し、`TIMED_OUT` は失敗 |
| R-9 | 3 章に、scope を取る補助の関数、後始末、未完了の確かめ |
| R-10 | CP-17 は 3 秒の窓で、イベントとコールバックを別々に数える。0 回のときの扱い |
| R-11 | CP-2、CP-15、CP-18、CP-19、CP-23 の予想を直した |
| R-12 | 出どころの確かめ、`clipboard` の区分の説明、`Unique` の名前を書かない |

## 7. v2 からの変更

| 指摘 | 変更 |
|---|---|
| レビュー v2 の R-1（A1）、Codex の CP-31 | CP-31 の 1.x の予想を「取り消しがちょうど 1 回、成功は 0 回」に決め、成功はテストの失敗にした |
| Codex の CB-5 | CB-5 の行に `native-listener`（`2.0.0` で `.mm` が足す自分の受け手）を足した |
| R-2 | CP-12 の予想を `TypeIdentifiers` と `AllTypeIdentifiers` に分けた |
| R-3、R-4 | CB-5 は `IsTerminated` を確かめて 0.5 秒待ってから書く。数えるのは変化のイベントを捨てた分だけ。`2.0.0` の包みは空の受け手を渡す |
| R-5 | CP-17 が 0 回なら CP-32・33・35 も外す |
| R-6、Codex の CP-29 | CP-29 のバイト列を `not an image` にした |
| R-7、R-8 | 3 章に、監視の停止の後始末、scope を取らない操作の例外、未完了の数え方 |
