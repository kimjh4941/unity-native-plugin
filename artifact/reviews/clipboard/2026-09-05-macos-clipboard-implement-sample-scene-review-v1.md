# macOS Clipboard サンプルシーン実装レビュー v1

## レビュー概要

- 対象ブランチ: `feature/UNT-10`
- 対象実装: working tree の未コミット差分（新規 7 ファイル + 既存変更 2 ファイル）
  - 新規: `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs`（1,149 行） / `MacClipboardSampleResult.cs`（268 行） / `MacClipboardSampleObservationState.cs`（159 行） / `Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExample.uxml`（113 行） / `MacClipboardManagerExampleStyle.uss`（154 行） / `Tests/Runtime/MacClipboardSampleSceneWiringTests.cs`（165 行） / `MacClipboardSampleStateTests.cs`（391 行）
  - 既存変更: `Runtime/UI/Common/NativeToolkitSampleNavigator.cs` / `Runtime/UI/Top/TopMenuExampleController.cs`
- 対象計画: `artifact/designs/clipboard/2026-09-05-macos-clipboard-sample-scene-design-v3.md`
- 対象実装結果: `artifact/results/clipboard/2026-09-05-macos-clipboard-implement-sample-scene-result-v1.md`
- 参照（API の正本）: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v13.md`
- 対象プラットフォーム: macOS（Standalone Player）
- レビュー日: 2026-09-05

`git diff main...HEAD` には現れないため、working tree の実ファイルを直接読んでレビューした。ボタン名・イベント購読・USS クラス名の 3 種は目視ではなく機械照合（`grep` + `diff`）で突き合わせている。

このサンプルは実機確認 32 項目（`v13 §7.5`）を駆動する道具であり、「押せる」ではなく「期待結果を画面から判定できる」ことが要件である。その観点を軸に、設計レビューで A1 として指摘された 3 点を重点的に確認した。

---

## 重点 3 点の確認結果

### 重点 1. deferred stop の判断基準 — 意図を満たしている

**結論: 成否ではなく `IsObserving` で判断している。iOS の条件は写していない。**

- `MacClipboardSampleObservationState.cs:145-157` `TakeDeferredStop()` は `StopRequestedAfterControl && !ControlPending` を通過条件にし、**フラグを成否に関わらず消費したうえで `IsObserving` を返す**。`isSuccess` を一切参照していない。
- `MacClipboardSampleObservationState.cs:86-99` `CompleteStart(owner, false)` は `IsObserving` を変更しない（`if (isSuccess) IsObserving = true;` のみ）。`CompleteStop(owner, false)`（`:107-118`）も同様に維持する。`IosClipboardSampleObservationState` の「失敗した Start で無条件に false」は複製されていない。
- Controller 側の `AfterControlCompletion`（`MacClipboardManagerExampleController.cs:1036-1045`）は、`Succeed` / `Fail` のどちらを通った後でも `if (owned)` で必ず呼ばれる（`:993` / `:1024` / `:1092`）。成功パスにしか置かれていない、といった漏れは無い。

**2 経路とも漏れが無いことを確認した。**

| 経路 | 動き |
| --- | --- |
| `OnDisable` 直発行（`Controller.cs:212-216`） | `RequestStop()` は非 pending 時に何もせず、`ShouldIssueStopNow()`（`State.cs:134` = `IsObserving && !ControlPending`）が true なら `IssueStopObserving("observe.stop.teardown")` を発行する |
| 完了側が肩代わり | pending 中は `RequestStop()`（`State.cs:124-130`）が `StopRequestedAfterControl` を立て、`ShouldIssueStopNow()` は false。所有トークンを持つ完了が `AfterControlCompletion` → `TakeDeferredStop()` → `IssueStopObserving("observe.stop.deferred")` を発行する |

- 「旧監視あり → Restart 開始 → pending のまま離脱 → Restart 失敗」の S-4 経路は、`IsObserving` が true のまま残るため stop が発行される。回帰テスト `MacClipboardSampleStateTests.cs:119-134` `DeferredStop_AfterAFailedRestart_IsStillIssued` が固定している。
- `Controller.cs:209-211` のコメントが「stop を先、購読解除を後」の順序理由（per-call callback はイベントではないので後から届く）を明示しており、順序も意図どおり。

残る限界（下の M-2 / M-3 と low L-7 を参照）:

- deferred stop 自体が失敗した場合、`StopRequestedAfterControl` は既に消費済みなので再試行されない。画面は既に無く再試行の主体が無いため、設計上やむを得ない。
- `StartIntervalProbe` からの完了も `AfterControlCompletion` を通るので deferred stop は正しく発行されるが、probe のチェーン自体は teardown 後も継続する（M-2）。

### 重点 2. ネイティブ `Error.Message` の非表示 — 意図を満たしている

**結論: raw message は画面にもログにも一切出ていない。`Read` / `Snapshot` の型名も出ていない。**

`Runtime/UI/macOS/Clipboard/` 配下を `Message` で全文検索した結果、`Error.Message` の参照は **0 件**（唯一のヒットは `MacClipboardSampleResult.cs:163` `DescribeException` で、`exception.GetType().Name` のみを返す実装）。

全経路の内訳:

| 経路 | 出力内容 | 判定 |
| --- | --- | --- |
| 画面（失敗行） | `MacClipboardSampleResult.cs:143-145` `FormatFailure` が `code=` と `reason=ReasonFor(code)` のみ | 安全 |
| `Debug.Log`（失敗） | `Controller.cs:389` `errorCode: {info.Code}` のみ | 安全 |
| `Debug.LogError`（error が null） | `Controller.cs:382` メッセージを持たない | 安全 |
| 共通イベントのログ | `Controller.cs:529-535` `LogEvent` が `error.Value.Code` のみ | 安全 |
| 局所拒否 | `Controller.cs:397` の `detail` は固定文字列 / 型名 / `FormatScopeLabel`（kind + 長さ） | 安全 |
| 呼び出し発行ログ | `Controller.cs:362` `scope: {Kind}` のみ | 安全 |
| 変更イベント | `Controller.cs:544-547` scopeKind と changeCount のみ | 安全 |

型名の非表示も確認した。

- `Read`（`Controller.cs:817-820`）: `items` / `changeCount` / `writtenTypes` / `readTypes` / `derived` / `roundTrip`。型名は無い
- `Snapshot`（`Controller.cs:866-868`）: `items` / `totalTypes` / `matching` / `changeCount` の数のみ。`ItemTypes` の中身は展開していない
- `ReadData`（`Controller.cs:841-842`）: `hasData` / `dataLength`
- `DetectMetadata`（`Controller.cs:926-928`）: `types` 数と `hasContentType` bool。`ContentTypeIdentifier` は出していない

29 定数の網羅も成立している。`MacClipboardErrorInfo.cs` の `public const int` は 29 件で、`MacClipboardSampleResult.cs:83-114` の `ReasonTokens` も 29 件。`MacClipboardSampleStateTests.cs:197-211` `EveryDefinedErrorCode_HasAReasonToken` がリフレクションで網羅を不変条件に変えており、D-17 の意図どおり。1507 / 1508 の redaction テスト（`:214-243`）も実在する。

1 点だけ、意図の確認が要る箇所を low（L-1）に挙げた（`DetectPatterns` の `kinds=` 表示）。

### 重点 3. `changeCount` によるゲート — 基本は満たしているが scope をまたぐ穴がある

**結論: 同一 scope 内では意図どおり。`Clear` 後の無効化も正しい。ただしアンカーが scope で修飾されていないため、scope を切り替えると誤判定しうる（M-1）。**

満たしている部分:

- `Controller.cs:748-749`: `Copy` 成功時に `Ownership.ChangeCount` を `_lastWrittenChangeCount` へ、型数 / 型名 / バイト列ハッシュを `RememberWrite` で退避する
- `Controller.cs:801`: `Read` 成功時に `_lastWrittenChangeCount != null && contents.ChangeCount == _lastWrittenChangeCount` を `fresh` として算出
- `MacClipboardSampleResult.cs:194-216`: `fresh` でなければ `FormatDerived` / `FormatRoundTrip` はともに `n/a` を返す。`sameTypeFound` が false のときも `differ` ではなく `n/a`（往復失敗との取り違えを避ける、§6.6 の 4 のとおり）
- `Copy` を経ていない起動直後は `_lastWrittenChangeCount == null` なので常に `n/a`
- `Controller.cs:1109`: `Clear` 成功で `_lastWrittenChangeCount = null`。**アンカーは正しく無効化される**
- 回帰テスト: `MacClipboardSampleStateTests.cs:301-347` に `Derived_WhenThePasteboardChangedSinceOurWrite_IsNotApplicable` / `RoundTrip_WhenThePasteboardChanged_IsNotApplicable` ほか 8 件

穴（M-1 で詳述）: `UseGeneral`（`Controller.cs:569`） / `UseFixedNamedScope`（`:579`） / `CreatePasteboard`（`:598`） / `RemoveActivePasteboard`（`:612`）はいずれも `_activeScope` を変更するが、`_lastWrittenChangeCount` を無効化しない。changeCount は pasteboard ごとに独立した数列なので、**別の pasteboard の changeCount と比較して `fresh` を判定してしまう経路が残っている。**

---

## 重大な問題（high）

なし。

A 区分（画面の振る舞いを変える: 操作が効かない / 結果が表示されない / ガードが誤っている / 購読が漏れる・二重になる）に該当する指摘は 0 件である。

---

## 改善提案（medium）

### M-1. `changeCount` アンカーが scope で修飾されていない

- `MacClipboardManagerExampleController.cs:110`（`_lastWrittenChangeCount` の宣言）
- `MacClipboardManagerExampleController.cs:748`（記録） / `:801`（比較）
- 無効化していない箇所: `:573`（`UseGeneral`） / `:586`（`UseFixedNamedScope`） / `:605`（`CreatePasteboard` 成功） / `:624`（`RemoveActivePasteboard` 成功）

`_lastWrittenChangeCount` は「どの pasteboard に書いたときの値か」を保持していない。`Copy` は `_activeScope` へ書き、`Read` も `_activeScope` から読むが、その間に scope を切り替えても比較はそのまま通る。macOS の changeCount は pasteboard ごとに独立した数列なので、**別の pasteboard の値どうしを比較していることになる。**

数値が偶然一致すると `fresh = true` となり、他の pasteboard の内容に対して `derived` / `roundTrip` を出す。R-3 が塞ごうとした「気づかないまま誤った結論を出す」と同じ種類の欠陥である。

発生確率は低い（General は起動後すぐ大きな値になり、新規作成の named / unique は 0 付近から始まる）。ただし失敗の仕方がサイレントで、`n/a` が出ないぶん誤りに気づけない。

修正案（どちらでも 1〜2 行）:

- `_lastWrittenChangeCount` を `(MacPasteboardScope Scope, long ChangeCount)?` の組にし、`Read` で `_activeScope` との一致も条件に入れる
- あるいは scope を変更する 4 箇所で `_lastWrittenChangeCount = null` にする

### M-2. `StartIntervalProbe` に teardown ガードが無い

- `MacClipboardManagerExampleController.cs:1071-1095`

`StartIntervalProbe` は callback の末尾で `StartIntervalProbe(index + 1)` を呼ぶチェーンで、停止条件は `index >= InvalidIntervals.Length`（`:1073`）だけである。**`OnDisable` / `OnDestroy` を経てもチェーンは止まらない。**

結果として、破棄済みの Controller から `MacClipboardManager.Instance` を叩き（アプリ終了時などに singleton を再生成しうる）、剥がれた `VisualElement` に `AppendResult` で追記し続ける。

4 値すべてが不正 interval なので「監視が残る」ことは無く A 区分にはならないが、画面を離れたあとに native 呼び出しが続くのは teardown の意図に反する。`OnDisable` で `_teardownRequested = true` を立て、`StartIntervalProbe` の先頭で return するだけで解消する。

### M-3. interval probe が `NonOwningToken` でも `StartObserving` を発行する

- `MacClipboardManagerExampleController.cs:1077-1079`

`int owner = _observation.BeginStart();` の戻り値を検査せずに `StartObserving` を発行している。`BeginStart()` が `NonOwningToken` を返すのは他の制御呼び出しが pending のときで、その状態で発行すると Manager の共有 single-flight キーが **9001 を返す**。期待している 1523 が出ない。

具体的に起きうる並び:

1. probe 実行中に画面を離れる → 完了側の `AfterControlCompletion` が deferred stop を発行して `ControlPending` が立つ
2. 直後の `StartIntervalProbe(index + 1)` が `NonOwningToken` のまま発行 → 9001

また `ErrObservingIntervalMatrixButton` は `RefreshInteractivity`（`:438-451`）の対象外なので、制御呼び出しが pending の最中でも押せる。到達コード表示（`_reachedCodes`）にも 9001 が混ざり、`v13 §7.5 #17` の消化状況が読みにくくなる。

`StartIntervalProbe` は `BeginStart()` / `CompleteStart` を経由せずに（監視状態を変更しない読み捨ての呼び出しとして）扱うか、`NonOwningToken` のときは発行を見送って次に進むのが素直である。

### M-4. `elapsedMs` に 12 MiB のハッシュ計算が含まれる

- `MacClipboardManagerExampleController.cs:748-751`（`Copy` の成功パス）
- `MacClipboardManagerExampleController.cs:464-473`（`HashOf`） / `:369`（`ElapsedMs` の確定点）

`Copy` の callback は `RememberWrite`（`HashOf` が 12,582,912 バイトを 1 バイトずつ走査する）を実行してから `Succeed` を呼び、`Succeed` の中で `ElapsedMs(startedAt)` を確定させている。したがって **`copy.largeSingleItem` の `elapsedMs` にはサンプル自身のハッシュ計算時間が加算される。**

`elapsedMs` は D-15 / `§7.4` で 7.5 #22（10 MiB 超の Copy の所要時間）の記録手段として追加されたものなので、この混入は測定値を直接汚す。`Succeed` を先に呼ぶか、`ElapsedMs` を `RememberWrite` の前に確定させれば解消する。

なお `Read` 側は `fresh` のときしかハッシュを取らず、7.5 #24（他アプリの 50 MiB を Read）は `fresh == false` になるため、#24 の `elapsedMs` は汚れない。

### M-5. 計画 `§7.3` S-5「Editor で実行」が UI から実行できない

- `Runtime/UI/Top/TopMenuExampleController.cs:178-194`（`OnClipboardClicked`）
- `Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExample.uxml:19`（About の注記）

`OnClipboardClicked` は `#if UNITY_EDITOR` を最初の分岐に置いており、Editor では `EditorUtility.DisplayDialog` を出して終わる（`:182-186`）。`#elif UNITY_STANDALONE_OSX` の `ShowMacClipboard`（`:191-192`）に到達するのは Player のみである。

`NativeToolkitSampleNavigator.ShowMacClipboard`（`NativeToolkitSampleNavigator.cs:155`）も Controller 本体も Editor でコンパイルはされるが、**Editor から画面に到達する導線が無い。** UXML の About 注記（`uxml:19`）は「In the Editor every operation fails with 9002; that is what this screen is meant to show」と書いているが、その画面を Editor で開けない。

Dialog / Share / Android Clipboard / iOS Clipboard も同じ書き方なので**コードの欠陥ではなく既存の慣習**である。指摘は計画との整合の問題で、`§7.3` の S-5「Editor で実行 → 全操作が 9002 で失敗し、その旨が結果行に出る」が現状のままでは消化できない。実装結果 4.3 の「S-5 の Editor 実行のみ PlayMode テストが同等の経路を通っている」を、S-5 の書き換え（またはガード順の変更）として計画側で決着させるのが望ましい。

### B-1. wiring テストの照合相手が Controller ではない（検証手段の穴）

- `Tests/Runtime/MacClipboardSampleSceneWiringTests.cs:30-75`（`RequiredButtonNames`） / `:112-120`
- `Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:296-338`（`Bind` 呼び出し 43 件） / `:344-353`（`Bind` 本体）

テストは **テストファイル内のハードコード配列**と UXML を突き合わせている。Controller の `Q<Button>("...")` 文字列は参照していない。したがって:

| 壊し方 | テスト | 実行時の見え方 |
| --- | --- | --- |
| UXML の name だけを書き換える | **落ちる** | — |
| テスト配列の名前だけを書き換える | **落ちる** | — |
| UXML とテスト配列を同じ名前に揃えて書き換える | **通る**（Controller だけが取り残される） | 当該ボタンが無反応 |
| **Controller の `Bind` 引数だけを書き換える** | **通る** | 当該ボタンが無反応 |

3 番目と 4 番目が穴である。いずれも Controller が UXML と食い違う状態だが、テストは Controller を参照していないため検出できない。実行時は `Bind` の `Debug.LogError`（`:349`）が 1 行出るだけで、`InitializeUI` はそのまま継続し、当該ボタンは押しても何も起きない **サイレント no-op** になる。実装結果 1.3-1 が「1 文字の食い違いがサイレントな no-op になる」として避けようとしたものが、Controller 側の変更に対しては塞げていない。

構造的な直し方: Controller に `internal static readonly string[] BoundButtonNames` を置き、`InitializeUI` の `Bind` ループとテストの両方をそこから駆動する。3 者（Controller / UXML / テスト）が 1 つの正本に閉じ、どこを壊しても落ちるようになる。

---

## 軽微な指摘（low）

### L-1. `DetectPatterns` が一致した pattern 種別を表示している（要確認）

- `MacClipboardManagerExampleController.cs:888-889`

`kinds={string.Join(",", result.Patterns)}` は `MacClipboardDetectionPattern` の列挙名を並べる。値そのものではないが、「クリップボードにメールアドレスと電話番号が入っていた」というカテゴリ情報は出る。

計画 `§6.5` の許可リストに挙がっている enum は `MacClipboardAccessBehavior` のみで、これは含まれていない。7.5 #11 の「一致パターンが返る」を判定するには必要な情報なので**意図的な追加として妥当**だが、コード中のコメント（`:886-887`）だけでなく `§6.5` の許可リストにも追記して、判断を記録に残すべきである。

### L-2. `_registrationCounts` は解放されない

- `MacClipboardManagerExampleController.cs:105` / `:962-963`

`IssueStartObserving` は呼び出しの直前に `_registrationCounts[registration] = 0;` を入れてから `StartObserving` を発行するので、`onChanged` closure の `_registrationCounts[registration]` が `KeyNotFoundException` になる経路は無い（キーは `marker#sequence` で一意）。**キー欠落の問題は無い。**

ただし辞書のエントリは削除されず、Start の回数だけ増え続ける。1 セッションで数十件が上限なので実害は無い。記録のみ。

### L-3. private クリックハンドラの `Debug.Log` が不揃い

- `MacClipboardManagerExampleController.cs:652-698` ほか（式形式のハンドラ約 20 件）

`OnCopyPlainTextClicked` / `OnCopyHtmlClicked` / `OnSnapshotClicked` / `OnStartObservingClicked` などの式形式ハンドラにはメソッド名のログが無い。一方 `OnReadClicked`（`:790`）や `OnDetectValuesClicked`（`:895`）には入っている。

`csharp.md` の必須対象（`public` / `internal` / `override` / `MonoBehaviour` イベント関数）ではないため**ルール違反ではない**。また `Begin()`（`:362`）が全呼び出しについて `issue #N {marker} scope:` を出すので追跡は可能である。ただし前例の `IosClipboardManagerExampleController`（`:541-545` ほか）は全ハンドラに入れているので、一貫性の点で揺れている。

### L-4. 7.5 #4 の文言とサンプルの手順が食い違う

- `v13 §7.5 #4`: 「`Read`（**他アプリがコピーした**テキスト）→ 書いた型以外の派生型も含まれることを確認」
- 実装: `derived` は `fresh`（自分の `Copy` の直後）でないと `n/a` になる（`MacClipboardSampleResult.cs:194-199`）
- 実装結果 4.1 の手順表: 「#4 `CopyHtml` → `Read` → `derived=true`」

サンプル計画 `§6.6` / D-9 が「呼べる」を「判定できる」に置き換えた結果の意図的な読み替えであり、**判定可能性の観点ではこちらが正しい**（他アプリのコピーでは `writtenTypes` が分からず判定できない）。ただし 7.5 #4 の文言のままだと実施者が `n/a` を見て戸惑う。7.5 側か実装結果側のどちらかに注記を入れるのが望ましい。

### L-5. 結果履歴の表示領域が 64px 固定

- `Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExampleStyle.uss:40-45`

`.mac-clipboard-result-border { height: 64px; }` は iOS 版からの複製で、font-size 12px では 4 行程度しか見えない。採番付きの履歴を追う用途（`§3.2`）では狭い。auto-scroll（`Controller.cs:413-419`）が入っているので追従はするが、直前の数行を見比べにくい。

計画 `§9.2` SV-4（実機のウィンドウサイズでの収まりは未検証）として既に記録済みなので、実機確認時に併せて判断すればよい。

### L-6. `BeginStart` と `BeginStop` の本体が同一

- `MacClipboardSampleObservationState.cs:61-78`

両者とも `if (ControlPending) return NonOwningToken; _controlOwner = ++_nextToken; return _controlOwner;` で、実装差が無い。呼び出し側の意図を名前で区別するための分割であることは XML コメントから読み取れるが、片方を変更したときにもう片方の同期が要る形になっている。記録のみ。

### L-7. deferred stop が失敗しても再試行されない

- `MacClipboardSampleObservationState.cs:145-157`

`TakeDeferredStop()` は `StopRequestedAfterControl` を成否に関わらず消費するため、肩代わりで発行した stop 自体が失敗すると監視が残る。コメント（`:152-155`）が「消費しないと存在しない監視に対して stop を出すことになる」と理由を明示しており、画面が既に無い以上、再試行の主体も無い。**設計どおりの割り切りとして妥当**であり、修正は求めない。記録のみ。

---

## そのほかの依頼確認事項（機械照合の結果）

| 観点 | 結果 |
| --- | --- |
| `Button.clicked` の `+=` / `-=` の対称性（43 個） | **なし。** `InitializeUI` の `Bind` 43 件（`Controller.cs:296-338`）と `OnDestroy` の `-=` 43 件（`:237-279`）を「フィールド名 + ハンドラ名」の組で `diff` した結果、完全一致・重複なし |
| Manager イベント 13 本の購読・解除の対称性 | **なし。** `OnEnable`（`:190-202`）と `OnDisable`（`:219-231`）はともに 13 本で対象も完全一致。`MacClipboardManager` の `public event` も 13 本（`MacClipboardManager.cs:132-213`）で漏れなし |
| closure キャプチャの誤り | **なし。** `context` / `startedAt` / `registration` / `target` / `owner` / `content` / `itemCount` はすべて呼び出しごとのローカルで、フィールド経由の共有は無い。`MacClipboardSampleResult.cs:17-22` が「単一の pending marker フィールドでは Read と Snapshot が取り違わる」理由を明記しており、その方針どおりに実装されている |
| `_registrationCounts` の扱い | **キー欠落の問題は なし**（L-2 に詳述）。発行前に必ずエントリを作っている |
| `StartIntervalProbe` の再帰が止まること | **止まる。** `:1073` の `index >= InvalidIntervals.Length` で終了し、callback は必ず 1 回発火する（single-flight 拒否も 9001 の結果として届く）ため 4 回で終わる。共有 single-flight キーとの整合も、前の完了を待って次を出す逐次実行になっており正しい。ただし M-2 / M-3 |
| UXML の name と `Q<Button>` の一致 | **なし。** 43 / 43 が完全一致（`Bind` 抽出と UXML 抽出を `diff` して差分 0） |
| USS のクラス名が UXML と一致 | **なし。** 双方向で過不足 0（UXML 使用 14 種 = USS 定義 14 種）。USS は iOS 版の完全な複製で、差分は `ios-` → `mac-` の置換と冒頭の由来コメントのみ。`common.md` の「共有しない」方針どおり |
| API 呼び出し仕様の一致 | **なし。** 15 操作すべてについて Manager の実シグネチャ（`MacClipboardManager.cs:998` / `1088` / `1184` / `1251` / `1318` / `1392` / `1463` / `1536` / `1610` / `1685` / `1761` / `1829` / `1898` / `1990` / `2075`）と引数順・callback 形状を突合し、齟齬なし |

---

## 追加確認: 止める基準 条件 2（wiring テストが UXML の name 破壊で落ちるか）

**判定: 落ちる。条件 2 は満たしている。**

根拠（`Tests/Runtime/MacClipboardSampleSceneWiringTests.cs`）:

- `:112-120` `ClipboardUxml_ContainsEveryButtonTheControllerBinds` が `RequiredButtonNames` の 43 件を `root.Q<Button>(name)` で引き、`Assert.IsNotNull` で検査する。UXML のボタン name を 1 つでも書き換えれば、その名前の `Q<Button>` が null になり **必ず失敗する**
- `:135-145` `ClipboardUxml_ButtonCountMatchesThePlan` が UXML 内の Button 総数と配列長（43）を比較するので、name の書き換えではなくボタンの追加・削除でも落ちる
- `:157-162` `Instantiate` は `Resources.Load<VisualTreeAsset>` の結果を `Assert.IsNotNull` してから `CloneTree()` するので、UXML の破損や移動も検出する
- `RequiredButtonNames` の 43 件は Controller の `Bind` 43 件と完全一致していることを機械照合で確認済み

**ただしテストの穴が 1 つある（B-1 に詳述）。** 照合の相手は Controller ではなくテスト内のハードコード配列なので、**Controller 側の `Bind` 引数だけを壊した場合は 7 件すべて pass する。** UXML → テスト方向の機械照合は成立しているが、Controller → UXML 方向は成立していない。

---

## 計画整合性チェック

| 観点 | 判定 | 根拠 |
| --- | --- | --- |
| ボタン一覧の実装網羅性 | **○** | `§4.3` の 43 個がすべて実装されている。ボタン名も表と完全一致。追加・欠落なし |
| UXML name と Controller の一致 | **○** | 43 / 43 機械照合済み |
| API 呼び出し仕様の一致 | **○** | 15 操作の引数順・callback 形状を Manager 定義と突合。`§4.5` の結果表示書式（`...` / `OK` / `NG code= reason=` / `-- local=`）も `MacClipboardSampleResult.cs:129-152` が字面まで一致。`§3.4` のステータス書式 `Codes: 1508,1512,9007 (3/10)` も `:229-238` で一致 |
| 変更ファイル一覧との一致 | **○** | `§5.1`（Runtime 3） / `§5.2`（Resources 2） / `§5.3`（Tests 2） / `§5.4`（既存変更 2）と実際の差分が一致。`§5.5` の非変更（`Runtime/Clipboard/Mac*` / `Runtime/UI/iOS/Clipboard/*` / `TopMenuExample.uxml` / asmdef）も `git status` で未変更を確認 |

補足: 実装結果 1.4 の「計画からの逸脱: なし」は妥当である。1.3 の追加判断 4 件（UXML のボタン表からの生成 / FNV-1a / `RefreshInteractivity` / `_activeRegistrationMarker` の削除）はいずれもコード上で確認でき、記述と実装が一致している。

---

## プロジェクトルール適合チェック

| 観点 | 判定 | 根拠 |
| --- | --- | --- |
| `common.md` 準拠 | **○** | P1: 3 Runtime ファイル・2 Resources ファイル・2 テストファイルすべてに `Mac` 接頭辞。型名も一致。配置: `Runtime/UI/macOS/Clipboard/` と `Runtime/Resources/UI/macOS/Clipboard/`（`UI/` の例外規定どおり）。P2: `Ios*` / `Android*` / `Windows*` のサンプル・UXML・USS を一切変更していない（`git status` で確認）。USS は共有せず複製し、冒頭に理由を明記（`uss:1-3`）。P4: `UI/Common/` への追加は macOS ガード付きの `ShowMacClipboard`（`NativeToolkitSampleNavigator.cs:155`）と `RemoveIfExists`（`:220`）のみで、Clipboard の機能ロジックは入っていない。既知の逸脱 11 件を前例として引用していない。`.meta` の新規作成なし |
| `csharp.md` 準拠 | **○** | `MonoBehaviour` イベント関数（`Awake` / `Start` / `OnEnable` / `OnDisable` / `OnDestroy`）と `internal` メソッドに `Debug.Log`。`LogTag` 定数あり。`public class` / `public static void ShowMacClipboard` に英語の XML ドキュメントコメント。コメント・UI 文言はすべて英語。`MacClipboardSampleResult.cs:8-11` と `MacClipboardSampleObservationState.cs:5-7` のログ省略は、理由を明記した意図的逸脱として妥当（純粋関数・純粋状態機械で、呼び出し側が全遷移をログしている） |
| ライフサイクル管理（登録・解除の対称性） | **○** | `clicked` 43 / 43、Manager イベント 13 / 13。`OnEnable` / `OnDisable` でイベント、`Start` / `OnDestroy` でボタン、という既存 Controller と同じ分担。`OnDisable` の「stop 発行 → 購読解除」の順序も理由つき（`Controller.cs:209-211`） |
| コンパイルガードの網羅性 | **○** | Runtime 3 ファイルと `MacClipboardSampleStateTests` が `#if UNITY_STANDALONE_OSX \|\| UNITY_EDITOR`（対象型と一致、`common.md` の規定どおり）。`MacClipboardSampleSceneWiringTests` は `#if UNITY_EDITOR`（`UnityEditor.AssetDatabase` を使うため必然で、既存 5 本の wiring テストすべてと同じ）。ハンドラ内部の実行ガードは意図的に**置いていない**が、これは `Controller.cs:28-31` が明記するとおり「Editor では Manager が 9002 で拒否し、それを見せるのがこの画面の目的」という設計判断であり、Editor フォールバックの実装漏れではない |
| 権限ガードの網羅性 | **該当なし** | macOS Clipboard は Unity 側に権限ゲートを持たない。検出系の許可は OS のダイアログが担い、拒否は 1514 として結果行に出る（`MacClipboardSampleResult.cs:73` で追跡対象コードに含む）。`ExecuteIfNotificationPermissionGranted` 相当のガードが必要な API は無い |
| ナビゲーション統合 | **○** | `ShowMacClipboard` 追加（`NativeToolkitSampleNavigator.cs:155-162`、`#if UNITY_STANDALONE_OSX \|\| UNITY_EDITOR` でガード）、`RemoveExistingControllers` の macOS ブロックに `RemoveIfExists<MacClipboardManagerExampleController>` 追加（`:220`）、`TopMenuExampleController` の購読ガードに `UNITY_STANDALONE_OSX` 追加（`:106`）と `OnClipboardClicked` の `#elif UNITY_STANDALONE_OSX` 分岐追加（`:191-192`）、ダイアログ文言更新。**変更前は macOS Player でボタンが非表示だった**という実装結果 2.4 の記述も差分から裏が取れる。ただし M-5 |
| 既存 API 互換性 | **○** | `TopMenuExampleController` の `_clipboardButton.clicked -=`（`:55`）はガードの外にあり、`+=` のガードを広げても対称性は保たれる。既存 3 分岐（Editor / Android / iOS）の挙動は不変で、変更はガード条件の追加とログ・ダイアログ文言のみ。`NativeToolkitSampleNavigator` は追加のみで既存メソッドを変更していない |

---

## 総合評価

**要修正（軽微）**

- **A 区分（画面の振る舞いを変える）: 0 件。** 43 ボタンの結線、13 イベントの購読・解除、コンパイルガード、ナビゲーション統合はいずれも完全で、機械照合でも差分が出なかった
- **重点 3 点はいずれも意図を満たしている。** deferred stop は成否ではなく `IsObserving` で判断し 2 経路とも漏れが無い。ネイティブ `Error.Message` は全経路で遮断され、`Read` / `Snapshot` の型名も出ていない。`changeCount` ゲートは同一 scope 内で正しく機能し、`Clear` 後の無効化も正しい
- 残す指摘は medium 5 件 + B 区分 1 件 + low 7 件。**M-1（scope をまたぐ誤判定）と M-4（`elapsedMs` へのハッシュ時間混入）は、この画面が「判定と計測の道具」であることに直結するため、実機確認に入る前に直す価値がある。** どちらも数行で済む
- **B-1（wiring テストが Controller を見ていない）は「中心導線」に当たる。** 43 ボタンの結線を機械照合に寄せるのがこのテストの存在理由であり、片方向しか閉じていない。ただし止める基準 条件 2 が要求する「UXML の name を壊すと落ちる」こと自体は満たしている
- M-2 / M-3 / M-5 と low 7 件は、直さない場合は実装結果ファイル（`-v2`）に理由つきで明記すること（止める基準 条件 3）

**次のラウンドの扱い:** A が 0 件なので、止める基準の 3 条件を確認したうえで実機確認へ進んでよい。条件 1（レビュアーを替えて 1 回）は本レビューで 1 巡目、条件 2 は満たし済み（穴は B-1 として記録）、条件 3 は実装結果ファイルの更新が必要である。
