# iOS Clipboard の C ABI `2.0.0` への移行の実装のレビュー v1

- 記録日: 2026-11-15
- 対象: コミット `2670503`（設計 [../designs/2026-11-15-ios-clipboard-cabi-design-v4.md](../designs/2026-11-15-ios-clipboard-cabi-design-v4.md)）
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。実装のレビューなので全体を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した（サブエージェントの出力の先頭の大見出しだけ、この記録の見出しと重なるので省いた）。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

ファイルは編集していません。差分だけでなく、Manager・CApi・ポンプの全体を読みました。そのうえで 1.x（`3cae23e^` の C# と native-toolkit `1.13.0` の `UnityIosClipboardManager.swift`・`UnityIosClipboardJsonParser.swift`）、ヘッダー、native-toolkit の `ClipboardReceiver`・`ClipboardMapping`・`ClipboardHandles`・`Registry`（`EventLedger`）と照らしました。

**問題が無かったところ**
- P/Invoke の 58 の宣言の型と、4 つの構造体の大きさ・位置（24/32/136/24）。
- コールバックの型 7 つと `[MonoPInvokeCallback]` の対応。
- 確保したメモリはすべて `CallNative` の `finally` で `FreeAll` されます。ハンドルは、捨てる経路も含めて `finally` で 1 回だけ解放されます。逆 P/Invoke の外に例外は漏れません。
- `user_data` は操作の種類、0 は破棄の停止です。単一の実行と合わせて、取り違えは起きません。
- エラーの文言は `ClipboardError.swift` と一致します。GetSnapshot の空の配列、copy_options の既定、Remove の General も 1.x と同じです。
- CancelLoads の順（cancel → load）は、`cancel_load` の完了が主の待ち行列を回るので成り立ちます。
- D-12 の `s_deliverInline` は、`RunLater` の中で最初の `Dispatch` だけが使い、`finally` で戻ります。そのため、受け手の中からの呼び直しはまた一覧を通ります。
- 破棄・リセットの後に一覧から出た結果は、`DiscardIfTerminated` と `Lifetime` で捨てられます。
- `HandOverLaterResults` の扱い（ネイティブの結果は渡し、呼び出しの中で決めた結果は捨てる）は D-12 のとおりです。

以下が指摘です。

### R-1 [A2] 観点 2・3・4: 届いた時と配る時の間に登録の世代が変わると、その間の変化が呼び出しごとの `onChanged` に届かない（1.x は届いた）
- **根拠**
  - `IosClipboardManager.cs:821-833`: `FireClipboardChanged` が、配る時に `s_onChangedGeneration == generation` を見直します。
  - 世代を変える所は 3 つあります。
    - 受け付けられた stop・失敗した start の完了は、ネイティブの完了の時点で `ReleaseChangeRegistrationIfOwned` が世代を 0 にします（`:1702-1705`、`:841-848`）。
    - 新しい StartObserving は、呼んだ時点で世代を進めます（`:1104-1106`）。
  - 1.x は `Dispatch(changeEvent, _instance?.ClipboardChanged, s_onChanged)` で、受け手を届いた時に取っていました（`3cae23e^` の `IosClipboardManager.cs:805-808`）。
  - 設計レビュー v3 のサブ R-2 の直し案（同じフォルダの `../reviews/2026-11-15-ios-clipboard-cabi-design-review-v3.md:71`）は「**一覧に並べる時は**世代を持たせる」でした。実装はすべての変化に当てはめていて、範囲が広がっています。
  - native-toolkit の `EventLedger.receive` は、変化をその場で（同期で）リスナーに配ります（`Registry.swift:369-380`）。そのため、同じフレームに続けて積んだ Copy と stop では、変化が stop の完了より先に C# に届きます。
- **壊れ方**
  - 例 1（stop）: 監視中に、同じフレームで `Copy(...)` と `StopObserving()` を呼ぶ。主の待ち行列は copy、変化（`OnChange`、世代 G を持ってディスパッチャーへ）、stop の完了（世代を 0 に）の順に進みます。次の `Update` で `ClipboardChanged` は出ますが、`onChanged` は呼ばれません。1.x では両方に届きました。
  - 例 2（置き換えの start）: ディスパッチャーに変化が待っている同じ `Update` の中で、先の受け手が `StartObserving(cb2)` を呼ぶ。元の `cb1` にも `cb2` にも届きません。
  - 例 3（受け付けた後の start の失敗）: 前の監視の変化が、同じように per-call から落ちます。
  - Editor のテストで再現する手順: start 成功 → yield → `DeliverChangeEventForTests` → `StopObserving()` → `CompleteObservationControlForTests(Stop, true)` → yield×2。今の実装では `onChanged` が 0 回です（1.x は 1 回）。
  - この差は、3 章の既知の差分にありません。
- **直し案**
  - 外すのは「入口で拒まれた start の登録」だけに絞ります。例えば `CompleteWithError` の start の経路（`acceptedByLibrary: false`）で、外した世代を `s_revokedGeneration` に記録します。配る時は `generation == s_revokedGeneration` のときだけ per-call を外し、それ以外は届いた時の `registered` をそのまま呼びます（1.x と同じ）。
  - 上の再現の手順をテストに足します。

### R-2 [B] 観点 5: 「呼び出しのフレームでは配らない」というフレームの関門を、どのテストも照らしていない
- **根拠**
  - `IosClipboardManagerIntegrationTests.cs:839-863`（`RejectedAtEntry_KeepsTheMarkUntilALaterFrame`）は、`DeliverLater(Time.frameCount)` の直後に `results.Count == 0` と `IsInFlight` を見ています。
  - `DeliverLater` はディスパッチャーに積むだけで、ディスパッチャーはその同期の続きでは回りません。そのため、`IosClipboardManager.cs:1817, 1820` の `Frame < frame` を消しても、この 2 つは通ります。
  - `:865-887` と Player の CP-37（`IosClipboardPlayerTests.cs:741-761`）の「フレームが増える」も、関門なしで通ります。ポンプは 1 フレームに 1 回しか回らないので、受け手の中で積んだものは、どのみち次の `Update` 以降になります。
  - 関門が守るのは「ポンプの `Update` より前に、同じフレームで呼ばれた呼び出し」です。この場面はどこにもありません。
  - `ChangeQueuedBefore_IsDeliveredBeforeALaterDecidedResult`（`:906-930`）が R4-1（追い越し）を捕まえるのは、ポンプの `Update` がディスパッチャーより先に回る場合だけです。同じ GameObject の 2 つの部品の `Update` の順は決まっていません。
- **壊れ方**: 関門や「末尾に渡す」を壊す変更（例えば `<` を `<=` にする、ポンプの中でその場で配る）が入っても、EditMode・Editor PlayMode・Player のどれも落ちない見込みがあります。
- **直し案**
  - `LaterCountForTests` を足します。`DeliverLater(Time.frameCount)` の後は 1 のまま、`DeliverLater(Time.frameCount + 1)` の後は 0、と直接照らします。
  - 追い越しのテストは、`DeliverLater` を明示して呼び、ディスパッチャーの中の順を見る形にして、`Update` の順に頼らないようにします。

### R-3 [B] 観点 5: `InvokeNative` の例外の経路（D-12 の一部）を照らすテストが無い
- **根拠**
  - 設計 4 章の Editor PlayMode の項目「受け手の中で `InvokeNative` が失敗しても呼び出しの中では返らない」に当たるテストがありません（新しい 17 本に含まれていません）。
  - Editor では `nativeCall` の中身が `#if UNITY_IOS && !UNITY_EDITOR` で空になるので、`IosClipboardManager.cs:1615-1638` の catch に入る手段がありません。
  - StartObserving・StopObserving の `onNativeFailure`（世代の後片付けを配る時に回す）と、CancelLoads の `cancel_load` が投げたとき（D-5）も、照らされていません。
- **壊れ方**: catch の中の `CompleteLater` を同期の `FireOperationResult` に戻しても、どのテストも落ちません。戻すと、受け手の中から呼び直したとき、同じ `Update` が終わらない形が戻ります。
- **直し案**: Editor だけの継ぎ目（例 `ThrowOnNativeCallForTests`）を設けて `InvokeNative` に例外を起こし、次の 3 つを照らします。
  - 呼び出しのフレームでは配らないこと
  - その間は BUSY であること
  - start の失敗で登録が外れるのは配る時であること

### R-4 [B] 観点 6: `common.md` の決まりが v3 の仕組みのままで、実装（v4）と食い違う
- **根拠**: `agent-rules/coding-rules/common.md:134-135` は、設計 v3 を指し、「ディスパッチャーではなく Manager 自身の一覧に入れ、Manager の `Update` で…配る」と書いています。実装は v4 のとおりです。
  - 一覧は Manager の外の静的なものです。
  - 境を見るのはディスパッチャーの GameObject に付けた `IosClipboardLaterPump` です（`IosClipboardLaterPump.cs:15-18`、`IosClipboardManager.cs:1798-1808`）。
  - 塊はディスパッチャーの**末尾に**渡します（`:1815-1826`）。
- **壊れ方**: この決まりを手本に Notification などを移すと、R4-1（先に積まれた変化を追い越す）と R4-2（Manager を無効にすると止まり、BUSY が残る）が再び入ります。
- **直し案**: v4 を指すように直し、次の 3 点を明記します。
  - 境を見る役はディスパッチャーの GameObject の常駐の部品であること
  - ディスパッチャーの末尾に渡すこと
  - その場で配るのは塊 1 つの `Dispatch` の 1 回だけであること

### R-5 [B] 観点 3: D-12 の理由（受け手の中から呼び直すと同じ `Update` が終わらない）が、BUSY・破棄・検査・Editor の拒みには当てはめられていない（1.x と同じ。今回悪くなったものではない）
- **根拠**: `DispatchRejectedResult`（`IosClipboardManager.cs:540-555`）と `TryStartOperation` の 2〜5 段（`:657-701`）は、`dispatcher.Enqueue` に直接積みます。`UnityMainThreadDispatcher.Update`（`UnityMainThreadDispatcher.cs:55-64`）は、空になるまで回します。
- **壊れ方**
  - Player: Read が実行中のとき、BUSY の受け手で「BUSY なら呼び直す」と書いた利用者のコードは、BUSY が同じ `Update` で積まれ続けて主スレッドが止まります。ネイティブの完了は `Update` の間は来ないので、抜けられません。
  - Editor: `CLIPBOARD_BRIDGE_UNAVAILABLE` の受け手で呼び直すと、同じことが起きます。
- **直し案**: 枠の前の拒みも、`Later`（`Operation` を null にした、印を持たない種類）で一覧に通すと、時点が D-12 とそろいます。BUSY が先に届く 1.x の順は、一覧の中の順で保てます。直さないなら、3 章に「1.x と同じく残る」と書きます。

### R-6 [C] 観点 3: ディスパッチャーの GameObject が壊されると、一覧に積まれたものとその後のネイティブの結果が、ログ無しで永久に残る
- **根拠**
  - `DeliverLater` は、`dispatcher == null` なら何もせず返ります（`IosClipboardManager.cs:1818-1819`）。
  - 一覧が空でない間は、`Dispatch` がネイティブの結果も一覧に足します（`:741-746`）。
  - 一覧が空なら `Dispatch` は「No dispatcher; result dropped」と出して捨てます（`:748-753`。1.x の `DestroyedDispatcher_IsDetectedAndTheResultIsNotSilentlyQueued` が守っていた約束）。一覧が空でない時は、この約束が外れます。
- **壊れ方**
  - 呼び出しの中で決めた失敗が待っている間に、ディスパッチャーの GameObject が壊されると、その操作は以後ずっと BUSY になります。
  - ほかの操作のネイティブの結果も、黙って一覧に溜まり続けます。
- **直し案**: `DeliverLater`・`Dispatch` で、ディスパッチャーが無ければ一覧の結果を「dropped」のログで捨て、印を外します（`complete()` を回す）。

### R-7 [C] 観点 3: リセットの説明と照らしが実際とずれており、テストの間に結果が持ち越される
- **根拠**
  - `ResetCore` の説明は「Resets every mutable static except `_instance`」（`IosClipboardManager.cs:438-440`）です。実際には `s_pump`・`s_deliverInline`・`s_listener`（意図あり）・`s_listenerReleaseCount`・`s_createKind` を戻しません。
  - `ResetForTests_ClearsEveryMutableStatic`（`IosClipboardManagerIntegrationTests.cs:759-789`）は、`s_later` が空になること・`s_isObserving` が下りることを照らしていません。
  - TearDown（`:31-38`）は、破棄の `HandOverLaterResults`（`:1854-1869`）でネイティブの結果をディスパッチャーに渡した後に、リセットします。`Operation == null` の塊は `RunLater` が一生の番号を見ない（`:1830-1834`）ので、次のテストの最初のフレームに、前のテストの受け手が呼ばれます。
- **壊れ方**: 今は無害です。ただし、前のテストの受け手が static の状態や `LogAssert` に触れると、後のテストが不安定になります。
- **直し案**
  - 説明を実際に合わせます。
  - リセットの照らしに `LaterCountForTests == 0` と `!IsObservingForTests` を足します。
  - ネイティブの塊にも一生の番号を見させるか、TearDown で数フレーム流してからリセットします。

### R-8 [C] 観点 5: テストはあるが製品の経路を通っていないもの
- **根拠と壊れ方**
  - `IosClipboardCApi.OperationResultOf`（`IosClipboardCApi.cs:513-516`）は `IosClipboardCApiTests.cs:250-256` で照らされていますが、製品では使われていません。`OnDone`（`IosClipboardManager.cs:1965-1985`）は、`HandleOperationCallback` で別に結果を組みます。テストが通っても、done の写し方は保証されません。
  - `NulInAValue_WarnsWithoutTheValueAndGoesOn`（`IosClipboardManagerIntegrationTests.cs:1172-1182`）は、名前のとおり「値を出さない」ことを照らしていません。正規表現 `U\+0000` しか見ていないので、警告に `secret` が入っても通ります。
- **直し案**
  - `OnDone` から `OperationResultOf` を使うか、使わないなら関数とテストを消します。
  - 警告の全文が `NulValueWarning` と等しいこと（または `secret` を含まないこと）を照らします。

### R-9 [C] 観点 2・6: `OUT_OF_MEMORY`・`NOT_INITIALIZED` のコードと文言が対にならない
- **根拠**: `ErrorCode` は `CLIPBOARD_OUT_OF_MEMORY`・`CLIPBOARD_NOT_INITIALIZED` を返します（`IosClipboardCApi.cs:234-235`）。`ErrorMessage` には行が無く、「An unknown error occurred.」になります（`:266-293`）。設計 D-3 は「UNKNOWN とほか → `CLIPBOARD_UNKNOWN`」で、どちらも 1.x に無いコードです。
- **壊れ方**: 今の native-toolkit はどちらも返しません（`EntryError` は 1・2・3 だけで、2 は返らない。F-10）。そのため実害はありません。将来返ると、1.x に無いコードと UNKNOWN の文言の組み合わせになります。
- **直し案**: D-3 に合わせて両方を `CLIPBOARD_UNKNOWN` にするか、それぞれに文言を足し、D-3 を更新します。

### R-10 [C] 観点 6: 古い注釈と、設計の版の引き方のばらつき
- **根拠**
  - `IosClipboardManager.cs:1643-1651` の `DiscardIfTerminated`: 「The native ABI carries no request or lifetime identifier」が残っています。設計 4 章が書き換えを求めた文言です。
  - `:1716` の見出しは「C ABI design v2」です。
  - `IosClipboardCApi.cs:352` は「design v3」、`IosClipboardJsonParser.cs:264` は「design v2」、`IosClipboardCApiTests.cs:14` は「design v3」です。
- **直し案**: v4 にそろえ、`DiscardIfTerminated` の説明を「`user_data` は種類だけで、一生を持たない」に直します。

### R-11 [C] 観点 6: 新しいメソッドに入口のログが無い
- **根拠**: `csharp.md:5` は「全メソッドの先頭に全パラメータの `Debug.Log`」を求めます。次のメソッドに入口のログがありません。
  - `CompleteLater`・`EnsurePump`・`DeliverLater`・`RunLater`・`HandOverLaterResults`・`CompleteWithError`・`CallNative`（`IosClipboardManager.cs:1755-1909`）
  - `IosClipboardLaterPump.Update`
  - `csharp.md:34` の例外は「することが無いフレーム」だけです。`DeliverLater` は渡す塊があるフレームでもログを出さないので、一覧が詰まったときに追えません。
- **直し案**: 渡す塊がある時だけ件数を `DeliverLater` のログに出し、ほかの補助にも入口のログを足します。値は出しません。

### R-12 [C] 観点 1: `AllocStringArray` の前提「要素は null でない」を呼び出し側が守っていない
- **根拠**
  - `IosNativeToolkitCApi.cs:111-121` の説明は「The caller makes sure no item is null」です。
  - `GetSnapshot` の `matchingTypes`（`IosClipboardManager.cs:1324-1330`）と MultipleText の `texts`（`IosClipboardCApi.cs:443-447`）は、null を確かめずに渡しています。
- **壊れ方**: null の要素は NULL のポインターになり、ライブラリが `INVALID_PARAMETER` で拒みます（F-7）。結果は 1.x と同じ `CLIPBOARD_INVALID_REQUEST`「The request is invalid.」なので、振る舞いは正しいです。ただ、共通の部品の約束に頼らずに通っている形です。
- **直し案**: 呼び出し側の注釈で「NULL の要素はライブラリが拒む前提」と明記するか、C# で先に拒みます（時点は D-12 の一覧）。

件数: A1 0 件、A2 1 件、B 4 件、C 7 件
## 2. Codex

総合判定は **要修正** です。対象は `3c650c8..2670503`。ファイルは変更していません。以下はコードと一次ソースの照合によるレビューで、Unity・実機テストは実行していません。

**R-1 — 区分：A1（実害のある不具合）— 世代の再確認が、配送順では有効な観測通知まで消す**

**根拠:** Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:825 は配送時に現在の世代と比較します。一方、StartObserving（同:1104）は呼び出し中に世代を更新し、停止完了は同:1702 から登録を解除します。親コミットの `IosClipboardManager.cs:710–713` は、受信時の登録をそのまま `Dispatch` に渡していました。

**発生例:** 登録 A の変化を配送し、その共通イベントの購読者が次の `StartObserving` を呼ぶと、同じ通知の A 向けコールバックが世代不一致で省略されます。また、dispatcher に「変化 → 停止完了」の順で積まれていても、停止のネイティブ完了が先に登録を解除すると、変化は共通イベントだけに届きます。例外がなくても通知先が欠ける、1.x からの変更です。

**修正:** 受信済み通知の登録を保持し、失敗した start の登録を無効にする処理と、後続の正常な start／stop を区別してください。両シナリオの回帰テストが必要です。設計 D-12（artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v4.md:80）の世代比較にも同じ問題があり、3 章の既知の差分にも記載されていません。

**R-2 — 区分：A2（修正価値のある小さな不具合）— reset 後に前テストのネイティブ結果を配送できる**

**根拠:** Dispatch（IosClipboardManager.cs:744）はネイティブ結果を `Operation == null` の `Later` にします。RunLater（同:1830）は、その場合に lifetime を検査しません。さらに HandOverLaterResults（同:1867）は検査なしの `later.Deliver` を dispatcher に渡します。ResetCore（同:441）が消すのは `s_later` だけです。

**発生例:** Copy の入口拒否の後ろに Append の完了を保留し、配送前にテストを終了します。TearDown（Tests/PlayMode/IosClipboardManagerIntegrationTests.cs:31）は destroy → reset の順で、destroy が Append を存続中の dispatcher に移し、その直後に reset しても、次テストの `Update` で前テストの Append コールバックが走ります。

**修正:** reset 用の世代を配送クロージャにも持たせてください。destroy 後に既知の結果を届ける方針は維持しつつ、reset をまたいだ結果は破棄し、dispatcher を空にせず次テストへ進む条件で検証してください。

**R-3 — 区分：A2（修正価値のある小さな不具合）— dispatcher 消失時に保留列と BUSY が残る**

**根拠:** CompleteLater（IosClipboardManager.cs:1787）の代替処理は、その呼び出しの `complete()` しか実行しません。既存の保留列は残り、DeliverLater（同:1819）も dispatcher がなければ戻ります。Dispatch（同:741）は dispatcher の生存確認より先に保留列へ追加します。

**発生例:** 入口拒否の Copy を `s_later` に入れてから dispatcher の GameObject を破棄すると、同居する pump も失われ、Copy の枠と BUSY が解放されません。後続のネイティブ完了も保留列に吸収され、「No dispatcher」のログすら出ずに保持されます。

**修正:** dispatcher 消失時は既存の保留処理も終了・破棄して枠を解放するか、配送基盤を復旧してください。既存の DestroyedDispatcher テスト（IosClipboardManagerIntegrationTests.cs:811）は「届かない」ことしか照合しておらず、無言の保留や BUSY 残留を検出できません。

**R-4 — 区分：B（検証の穴）— 「呼び出しフレームでは配送しない」ことをテストできていない**

**根拠:** RejectedAtEntry_KeepsTheMarkUntilALaterFrame（IosClipboardManagerIntegrationTests.cs:848）は同一フレームの `DeliverLater` 直後に結果数を確認しますが、その時点で dispatcher を実行していません。dispatcher がクロージャを実行するのは UnityMainThreadDispatcher.cs:55 の Update です。

**見逃す例:** `DeliverLater` を誤って同一フレームでも enqueue する条件に変えても、直後の結果数は 0、BUSY は true のままで、この検査を通ります。CP36 も同期配送の有無だけを確認しています。これはコードからの判定で、変更を加えた変異テストは実行していません。

**修正:** 同一フレームに pump と dispatcher の両方を回して未配送を確認し、最初のコールバックでも `配送フレーム > 呼び出しフレーム` を照合してください。CP37 の再呼び出し間の比較だけでは最初の境界を保証できません。

**R-5 — 区分：B（検証の穴）— `InvokeNative` の例外を D-12 経由で処理する経路が未検証**

**根拠:** InvokeNative（IosClipboardManager.cs:1619）は例外時の失敗と観測登録の後片付けを `CompleteLater` に延期します。しかし Editor の bridge override（同:512）はネイティブ呼び出しを空にする仕組みです。追加された再入テスト（IosClipboardManagerIntegrationTests.cs:870）は RejectAtEntryForTests を呼んでいるだけで、この catch を通りません。

**見逃す例:** 例外時だけ同期完了する退行や、観測世代の後片付けを省略する退行が入っても、入口拒否のテストは成功します。実際の不具合発生を主張するものではなく、設計の検証項目（設計 v4:122）が未充足です。

**修正:** 枠を取得した状態で実際の `InvokeNative` に例外を投げる Action を渡し、別フレーム配送、完了までの BUSY 維持、観測登録の解放、配送前 destroy による破棄を確認してください。

**R-6 — 区分：C（記述の不一致）— 共通規則が実装前の D-12 を指示している**

**根拠:** 今回追加された agent-rules/coding-rules/common.md:134 は設計 v3 を参照し、「Manager 自身の一覧」「Manager の Update で配る」と記載しています。実装は IosClipboardLaterPump.cs:14 の pump と dispatcher への引き渡しです。

**問題になる例と修正:** 次の実装者が規則に合わせて Manager の `Update` に戻すと、無効化した Manager の配送が止まります。参照を v4 に更新し、pump の配置、dispatcher 末尾への引き渡し、その場で配送する範囲を実装と一致させてください。

P/Invoke 58 宣言の型幅・引数数、および 4 構造体の 64 bit 配置は、ヘッダーとネイティブの静的配置検査値に一致しました。読んだコールバックでは static delegate の保持、`MonoPInvokeCallback`、ハンドル解放の `finally` があり、具体的な署名不一致・二重解放は確認していません。

件数：A1＝1、A2＝2、B＝2、C＝1、合計＝6。
## 3. 判断

Codex の R-1（A1）とサブの R-1（A2）は同じ不具合。A1・A2・B を直し、安い C も直す。直した後の設計は v5（[../designs/2026-11-15-ios-clipboard-cabi-design-v5.md](../designs/2026-11-15-ios-clipboard-cabi-design-v5.md)。レビューしていない）。

| 指摘 | 判断 | 直し |
|---|---|---|
| Codex の R-1、サブの R-1（世代の見直しで、1.x なら届いた変化が登録に届かない） | 直す | 外すのは入口で拒まれた start の登録だけ（その世代を記録する）。ほかは届いた時の登録に届ける。Editor の PlayMode に「stop の結果の前に届いた変化は登録に届く」「新しい start の前に届いた変化は、届いた時の登録に届く」 |
| Codex の R-2、サブの R-7（リセットをまたいで結果が届く、リセットの説明） | 直す | ネイティブの結果の塊もリセットの番号を持ち、違えば捨てる（破棄で渡したものも）。リセットの説明を実際に合わせ、リセットのテストに一覧と監視の状態を足す |
| Codex の R-3、サブの R-6（ディスパッチャーが壊れると一覧と BUSY が残る） | 直す | 一覧を「dropped」で捨て、呼び出しの中で決めたものは完了の経路を回して印を外す。Editor の PlayMode で確かめる |
| Codex の R-4、サブの R-2（フレームの境を照らしていない、追い越しのテストが `Update` の順に頼る） | 直す | `LaterCountForTests` で境を直接照らし、受け手のフレームが呼び出しのフレームより後なことも照らす。追い越しのテストは `DeliverLater` を明示して呼ぶ |
| Codex の R-5、サブの R-3（`InvokeNative` の例外の経路が照らされていない） | 直す | Editor だけの `NativeCallThrowsForTests` を足し、後のフレームに配ること、その間の BUSY、start の登録が配る時に外れることを確かめる |
| Codex の R-6、サブの R-4（`common.md` が v3 のまま） | 直す | v5 を指し、常駐の部品・末尾に渡すこと・その場で配る範囲・Manager の `Update` に戻さない理由を書く |
| サブの R-5（BUSY などの枠の前の拒みを受け手の中から呼び直すと、同じ `Update` が終わらない） | 直さない | 1.x と同じで、今回悪くなったものではない。設計 v5 の 3 章に「1.x から残る」と書く |
| サブの R-8（`OperationResultOf` が製品で使われていない、警告の照らしが弱い） | 直す | `OnDone` が `OperationResultOf` を使う。警告の全文を照らす |
| サブの R-9（`OUT_OF_MEMORY`・`NOT_INITIALIZED`） | 直す | D-3 に合わせて `CLIPBOARD_UNKNOWN`。EditMode に 2 件 |
| サブの R-10（古い注釈と版の引き方） | 直す | v5 にそろえ、`DiscardIfTerminated` の説明を直す |
| サブの R-11（入口のログ） | 直す | 補助に入口のログ（値なし）。`DeliverLater` は渡した時だけ件数を出す。ポンプの `Update` は毎フレームなので出さない |
| サブの R-12（`AllocStringArray` の前提） | 直す | 呼び出し側に「NULL の要素はライブラリが拒む」と注釈 |
