# iOS Clipboard 自動テスト計画 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | `IosClipboardManager`（`Runtime/iOS/Clipboard/IosClipboardManager.cs`）の 15 の操作（Copy、Append、Clear、RemovePasteboard、CancelLoads、StartObserving、StopObserving、Read、ReadData、GetSnapshot、CreatePasteboard、DetectPatterns、DetectValues、LoadItem、CheckForegroundChange）と 10 のイベント |
| 目的 | iOS の C ABI `2.0.0` への移行（[artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md)）の段 3-R。**1.x のまま記録**し、段 3 で Clipboard を移した後に同じテストを流して、1.x と同じか、既知の差分かを確かめる |
| 実行 | **ほとんどは画面を出さない iOS の Player のテスト**（`scripts/verify_unity_ios.sh`、`Tests/PlayMode/IosClipboardPlayerTests.cs`）。**名前付きの pasteboard だけ**を使うので、実機でも流せる。ペーストの許可のダイアログを押すものだけ UI のハーネス（`scripts/test_ios_ui.sh`、`ClipboardUITests`）で、シミュレーターだけ |
| 書き方 | 結果は、各結果の型の値（`IsSuccess`、エラーの `Code`、項目の数と型の一覧、文字列、バイト列の長さとハッシュ）を NUnit で照らす。UI の行の書き方は Dialog の計画 v3 の 1 章と同じ |
| 1.x の振る舞いの出典 | native-toolkit のタグ `1.13.0` の `ios/UnityIosPlugin/UnityIosPlugin/Clipboard/` と `ios/IosLibrary/IosLibrary/Clipboard/`。C# の 1 回に 1 つの規則（`TryStartOperation`）は `IosClipboardManager.cs` |
| `2.0.0` との差 | native-toolkit の `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 の CL-01〜CL-15 と、その前書き（エラーの文言は値から写す。`error.details` の domain と code は保てない〈D-13〉。`callback` の `NULL`）、`Clipboard.h` |
| 移行前の基準 | 段 2 の (A) の `NamedPasteboardRoundTripsPlainText` だけ。ほかは無い。1.x の列は予想で、**実測が違えば記録の文書に書く**（Dialog の ID-14a で、一次ソースの読みが 2 人とも外れた。読みより流して決める） |

**プライバシー**: 一般の pasteboard の中身はテストの中で読まない・書かない（UI のシミュレーターだけの項目を除く）。結果の行と NUnit の失敗の文に、テストが決めた値以外の中身を出さない。

## 1. 画面を出さないテスト（iOS の Player、名前付きの pasteboard）

どのテストも、自分の名前付きの pasteboard（`com.jonghyunkim.nativetoolkit.playertests.<テストの名前>`）を作り、終わりに消す。

| ID | 何をするか | 1.x の予想 | `2.0.0` の期待 |
|---|---|---|---|
| CP-1 | PlainText を Copy して Read | 1 項目、`Text` が同じ（今の `NamedPasteboardRoundTripsPlainText`） | 同じ |
| CP-2 | HtmlText（plain と html）を Copy して Read、`ReadData("public.html")` | 1 項目に `public.utf8-plain-text` と `public.html` の 2 つの型。html のバイト列が同じ | 同じ |
| CP-3 | Url を Copy して Read | `UrlString` が同じ | 同じ |
| CP-4 | ImageData（小さな PNG）を Copy して `ReadData("public.png")` | バイト列の長さとハッシュが同じ（再圧縮されれば、型だけを記録する） | 同じ |
| CP-5 | Color を Copy して Read | 記録する（型の一覧） | 同じ |
| CP-6 | CustomData（`com.jonghyunkim.ntk.custom`）を Copy して `ReadData` | バイト列が同じ | 同じ |
| CP-7 | 無い型で `ReadData` | 成功で、データが null | 同じ（`ntk_bytes` が `NULL`、CL-04） |
| CP-8 | MultipleText（2 つ）を Copy して Read | 2 項目 | 同じ |
| CP-9 | MultiRepresentation（2 つの型）を Copy して Read | 1 項目に 2 つの型 | 同じ |
| CP-10 | Copy の後に Append | 項目が 2 つになる | 同じ。`options` を持つ Append を包みが断る（CL-02） |
| CP-11 | Clear して Read | 成功で 0 項目（空は失敗ではない） | 同じ |
| CP-12 | 2 種類を Copy して `GetSnapshot(matchingTypes: ["public.url"])` | url の型だけが入る | 同じ |
| CP-13 | URL を Copy して `DetectPatterns([ProbableWebURL])` | その模様が見つかる。ペーストの許可は求めない | 同じ |
| CP-14 | URL を Copy して `DetectValues([ProbableWebURL])` | 値として URL が返る | 同じ（封筒は包みが付ける、CL-10） |
| CP-15 | 文字列を Copy して `LoadItem(Text)`、`LoadItem(Url)` | Text は文字列、Url は記録する（文字列から URL を作らないなら失敗か null） | 同じ |
| CP-16 | 読み込み中でないときに `CancelLoads` | 成功 | 同じ（包みが未完了を覚えて 1 つずつ取り消す、CL-12） |
| CP-17 | `StartObserving`、Copy、`ClipboardChanged` を待つ、`StopObserving` | 変化のイベントがちょうど 1 回 | 同じ（受け手を 1 つにする、CL-13） |
| CP-18 | `CheckForegroundChange` を 2 回（間に Copy） | 1 回目は記録する、2 回目は `changed = true` の見込み | 同じ |
| CP-19 | `CreatePasteboard(Unique)` して `RemovePasteboard` | 空でない名前の scope が返り、消せる | 同じ |
| CP-20 | `CreatePasteboard(Named("   "))`（空白だけ） | 完了で `INVALID_NAME` の見込み | 同じ（今の `INVALID_NAME` は完了で来る、CL-07） |
| CP-21 | `RemovePasteboard(General)` | `CANNOT_REMOVE_GENERAL` の見込み | 同じ（CL-08） |
| CP-22 | 期限を過ぎた `Copy(options: Create(localOnly: true, expirationDate: 過去))` して Read | 記録する（0 項目の見込み） | 同じ |
| CP-23 | 同じ種類の操作を 2 つ続けて呼ぶ（1 つ目の完了の前） | 2 つ目は C# の 1 回に 1 つの規則で失敗（`IosClipboardManager.cs` の `TryStartOperation`）。1 つ目は成功 | 同じ（C# の規則を保つ） |
| CP-24 | 失敗の結果のエラーの値 | 失敗した項目（CP-20、CP-21、CP-23）で、`Code`、文言、`Details` を記録する | **既知の差分**: `Details` の domain と code は保てない（D-13）。文言は値から写す |

## 2. ペーストの許可を押すテスト（UI のハーネス、シミュレーターだけ）

一般の pasteboard に、XCUITest のプロセスから別のアプリとして文字列を書き（`UIPasteboard.general.string = "ntk ui harness paste"`）、ハーネスがそれを読む。iOS 16 以降は、ほかのアプリが書いた値を読むときに許可のダイアログを出す。**実機では流さない**（一般の pasteboard を書き換える。native-toolkit の CB1 では、実機で許可のダイアログが出なかった）。

| ID | ハーネスの行 | XCUITest がすること | 1.x の予想（結果の行） | `2.0.0` の期待 |
|---|---|---|---|---|
| CB-1 | 一般の pasteboard を Read | ペーストの許可で「ペーストを許可」 | `[read@ok] items=1 text-length=20` | 同じ |
| CB-2 | 同じ | 「ペーストを許可しない」 | 記録する（失敗か 0 項目か。文言の値） | 1.x と同じか、写し方の設計で決めた差分 |
| CB-3 | 一般の pasteboard に `DetectValues([ProbableWebURL])`（XCUITest が URL を書いておく） | 許可しない | 記録する（native-toolkit の C ABI では検出の失敗で値は NULL） | 同じか差分 |
| CB-4 | 一般の pasteboard に `DetectPatterns` | 許可のダイアログが出ないことを確かめる | 模様が返り、ダイアログは出ない見込み | 同じ |

結果の行には、読んだ文字列の中身ではなく長さだけを書く（XCUITest が書いた値でも、行の形をそろえるため）。

## 3. テストの共通の約束

- Player のテストのカテゴリーは `IosPlayer`。名前は `IosClipboardPlayerTests.CP<番号>_<意味>`
- UI のテストの名前は `ClipboardUITests/test_CB<番号>`。4 件とも `simulatorOnly`
- 段 2 のパイロットの UI のテスト `ClipboardUITests/test_namedRoundTrip` は、CP-1 と同じものなので消す
- 実機の区分は、Player のテストは `PlayerTests/IosPlayer`（`clipboard` の名前付きの pasteboard だけ）

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| 実機での一般の pasteboard | 私物の端末のクリップボードを書き換える。許可のダイアログも出ないことがある（native-toolkit の CB1） |
| ペーストのコントロール（`UIPasteControl`） | 1.x の API に無い |
| 背景でのクリップボードの変化の通知 | アプリが背景にある間の変化は、前に戻ったときの `CheckForegroundChange` で見る（CP-18） |
| 画像のファイルの読み込みの中身の見た目 | 手で見る（native-toolkit でも同じ） |

## 5. 1.x の記録

記録は別の文書に書く（計画の版は上書きしない）。
