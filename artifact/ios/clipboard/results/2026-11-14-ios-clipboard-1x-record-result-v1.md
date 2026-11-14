# iOS Clipboard の 1.x の記録（自動テスト計画 v4 の 5 章）v1

- 記録日: 2026-11-14
- 計画: [../designs/2026-11-14-ios-clipboard-ui-test-plan-v4.md](../designs/2026-11-14-ios-clipboard-ui-test-plan-v4.md)（5 章「1.x の記録」をこのファイルに書く。計画の版は上書きしない）
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3-R
- 対象: 1.x（`Plugins/iOS/VERSION.txt` の dist 1.13.0、`ios-native-toolkit-1.3.0` と `unity-ios-native-toolkit-1.3.0`）
- 実行: `scripts/verify_unity_ios.sh --skip-editmode`（iOS の Player だけ、iPhone 16 / iOS 18.3 のシミュレーター、Xcode 26.3）。テストは `Tests/PlayMode/IosClipboardPlayerTests.cs`（35 件。名前付きの pasteboard だけ）
- この版は 1 章（Player のテスト）だけ。2 章の UI のテスト（CB-1〜5）はまだ作っていない。実機もまだ

## 1. Player のテストの記録（2026-11-14）

1 回目の回は 46 件のうち 8 件が落ちた（どれも Clipboard）。4 件はテストの誤り（2 章）、4 件は 1.x の実測が予想と違った（表の「予想と違った」）。直して流し直し、IosPlayer の 46 件（Clipboard 35、Share 6、Dialog 4、Notification 1）がすべて成功した。

| ID | 1.x の実測 | 計画の予想との関係 |
|---|---|---|
| CP-1、3、6〜8、10、11 | 予想どおり | - |
| CP-2 | 1 項目。型は `public.plain-text,public.rtf,public.html,com.apple.flat-rtfd,public.utf8-plain-text`（UIKit が rtf と rtfd を足す）。html のバイト列は同じ | 予想どおり（型の一覧は記録） |
| CP-4 | 送った PNG（80 バイト）と読んだものが、長さもハッシュも同じ（再圧縮されない） | 予想どおり |
| CP-5 | 型は `com.apple.uikit.color` の 1 つ | 記録 |
| CP-9 | 型は `public.plain-text,com.jonghyunkim.ntk.custom` | 予想どおり |
| CP-12 | `NumberOfItems = 2`、`TypeIdentifiers = public.plain-text`（最初の項目だけ）、`AllTypeIdentifiers = public.plain-text｜public.url`、`MatchingItemIndexes = [1]` | 予想どおり（v3 で直した予想） |
| CP-13 | **URL の項目（`IosClipboardContent.Url`）では、`DetectPatterns` は成功で模様が空**。同じ URL を文字列（`PlainText`）で置くと `ProbableWebUrl` が見つかる | **予想と違った**（「その模様が見つかる」）。検出は文字列の表現にだけ効く |
| CP-14 | 同じく、URL の項目では `ProbableWebUrl` が null。文字列で置くと `https://example.com/ntk` | **予想と違った**（同上） |
| CP-15 | Text は文字列が返る。**Url は `CLIPBOARD_NO_MATCHING_ITEM`**（文字列の項目から URL を作らない）。失敗のときは `[IosClipboardJsonParser][ReadErrorInfo]` のエラーのログも出る | 予想の候補の片方 |
| CP-18 | 1 回目は `false`、間に Copy して 2 回目は `true` | 予想どおり |
| CP-19 | `Unique` の名前の scope が返り、消せる。消した後の Read は `CLIPBOARD_UNAVAILABLE` | 予想どおり（名前は記録しない） |
| CP-22a | 3 秒先の期限で Copy、すぐの Read は 1 項目、6 秒後は 0 項目 | 予想どおり |
| CP-20 | C# が同期で `ArgumentException` | 予想どおり |
| CP-21、22、25〜30 | `Code` と `Message` が予想の全文どおり。`Domain` と `NativeCode` はどれも null（1.x の操作のコールバックには details が無い） | 予想どおり |
| CP-23 | 2 つ目は `CLIPBOARD_BUSY`、1 つ目は成功、`ReadCompleted` は 2 回 | 予想どおり |
| CP-16 | 成功 | 予想どおり |
| CP-31 | 読み込みは `CLIPBOARD_CANCELLED` がちょうど 1 回、成功は 0 回。CancelLoads の成功が 1 回。順は **cancel → load** | 予想どおり（v4 で決めた予想） |
| CP-17 | **1 回の Copy で変化のイベントが 2 回**: 型の無い `Changed` の後に、`public.plain-text` を足した `Changed`。`ClipboardChanged` と `onChanged` はどちらも 2 回 | **予想と違った**（「ちょうど 1 回」）。名前付きの pasteboard でも通知は来る（CP-17 が 0 回のときの「外す」は起きなかった） |
| CP-32 | 2 つ目の Start の後、A は 0 回、B は 2 回、`ClipboardChanged` は 2 回 | 予想どおり（回数は CP-17 の 2 回に合わせた） |
| CP-33 | 停止の後の Copy で 0 回 | 予想どおり |
| CP-34 | Start の完了の前の Stop は `CLIPBOARD_BUSY`、Start は成功 | 予想どおり |
| CP-35 | 監視中に RemovePasteboard すると、`Kind = Removed` が 1 回 | 記録 |

## 2. 計画 v4 との違い（計画の版は上書きしない）

- **エラーのログ**: 1.x は読み込み系の失敗（Read、LoadItem など）で `Debug.LogError`（`[IosClipboardJsonParser][ReadErrorInfo] errorCode: …`）を出す。Unity のテストはこれを予期しないエラーとして落とすので、その失敗を期待するテスト（CP-15・19・25・26）は `LogAssert.Expect` でログも予期する形にした。Copy などの操作の失敗ではログは出ない
- CP-13・14 は、URL の項目で空になることを照らし、文字列で置いたときの結果は記録だけにした（次の回で照らす形にする）
- CP-17・32 は実測の 2 回に合わせた
- Player のテストの後片付け（`verify_unity_ios.sh` の `clean_up_player`）が、ビルドに失敗して Player が入っていないときに「消せたか確かめられない」と誤って出していた。入っていなければ成功とするように直した

## 3. 分かったこと

- 1.x の検出（`DetectPatterns`、`DetectValues`）は、URL の項目には効かず、文字列の表現にだけ効く。`2.0.0` でも UIKit の同じ API なので同じ見込みだが、包みの写し方の比べはこの形で行う
- 1 回の Copy で変化のイベントが 2 回来る（1 回目は型が空）。`2.0.0` の包みが受け手を 1 つにする（CL-13）とき、回数は 1.x と同じ 2 回を期待する
- `Domain` と `NativeCode` は、1.x の操作の失敗ではどれも null。D-13 の確かめは CB-3（検出の失敗）で行う

## 4. 残っていること

- 2 章の UI のテスト（CB-1〜5）をハーネスと `ClipboardUITests` に作り、シミュレーターで記録する（CB-5 の `.mm` の `dlsym` を含む）
- 実機での記録（Player のテストは名前付きの pasteboard だけなので、`PlayerTests/IosPlayer` の承認で流せる。許可を得てから）
