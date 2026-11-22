# iOS Clipboard の C ABI への写し方の設計 v2 のレビュー v2

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-clipboard-cabi-design-v2.md](../designs/2026-11-15-ios-clipboard-cabi-design-v2.md)
- 前の回: [2026-11-15-ios-clipboard-cabi-design-review-v1.md](2026-11-15-ios-clipboard-cabi-design-review-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。2 回目なので差分を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した（サブエージェントの出力の先頭の大見出しだけ、この記録の見出しと重なるので省いた）。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

対象は `artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v2.md` です。ファイルは編集していません。

レビューの途中で、作業ツリーの `IosClipboardManager.cs` と `IosClipboardJsonParser.cs` が書き換わっていきました（実装が並行して進んでいます）。今の C# の行番号は、すべて **HEAD**（`git show HEAD:…`）のものです。`IosClipboardCApi.cs` は見ていません。

**確かめて合っていたこと（1 回目の A1・B の直り）**

- **R-2・R2-3（Remove の General）**: D-8 (3) で直っています。`requiredScope`（`ClipboardMapping.swift:67-70`）に `kind = GENERAL` を渡すと、ライブラリの `cannotRemoveGeneral` まで届きます。
- **R-3（模様のビット）**: D-8 (4) で直っています。範囲外の値の文言「The request is invalid.」は、1.x の `deliverInvalidRequest`（1.13.0 `UnityIosClipboardManager.swift:370-373`）と同じです。
- **R2-1・R-4（入口の拒みと CancelLoads）**: D-4・D-5・D-12 で直っています。CP-31 の順（cancel → load）も成り立ちます。`cancel_load` の取り消しは `enqueueRemoval`→`MainQueue.async` を通るので（`Registry.swift:148-151, 282-291`）、C# が呼び出しの中で積んだ CancelLoads の配りより後に積まれます。ディスパッチャーは、Update の中で新しく積まれたものも同じ回で回します（`UnityMainThreadDispatcher.cs:55-63`）。そのため、D-12 の「積み直さない」は正しい判断です。
- **R2-2・R-5（停止の後のイベント）**: D-6 の状態の門で、基本は直っています。listener の add と start は同じ主の待ち行列にその順に積まれ（`Registry.swift:257, 364`）、start の完了は start の塊の中で同期に来ます（`ClipboardReceiver.swift:321-327`）。状態が立つのは Copy による変化より前です。CP-17・32・33 は保たれる見込みです。
- **2.1 の表**:
  - Read の `numberOfItems` は 1.x でも `items.count` でした（`ClipboardRepositoryImpl.swift:75`）。
  - 空の `matchingTypes` は、1.x でも鍵を省いて null でした（Builder の HEAD:102、`ClipboardRepositoryImpl.swift:87-89`）。
  - DetectPatterns の順は、1.x では `Set.map` の不定の順だったので、退行ではありません。
  - C# の enum の並び（content 0〜8、load 0〜3）は C の値と一致します。
- **R-14（期限）**: 1.x の `HH:mm:ss'Z'` は秒未満を切り捨てていました。D-8 (2) と同じです。
- **R-11・R-12・R-13・R-15**: それぞれ反映されています。

---

### R-1 [B] D-6 (2): StartObserving が入口で拒まれたときにも状態を下ろすので、ネイティブで続いている前の監視の変化が黙って捨てられる

**根拠**
- 設計 D-6（54 行）は「StartObserving の失敗（入口の拒みを含む）で下ろす」としています。
- C ABI の入口の拒みは `accept` の中で返ります（`ClipboardReceiver.swift:58-69`）。start の塊（`ClipboardReceiver.swift:318-327`）も、前の監視を止める `stopObservingInternal()`（native-toolkit `IosLibrary/…/Clipboard/IosClipboardManager.swift:502`）も通りません。つまり、前の監視はネイティブで続いています。止まるのは、受け付けた後のライブラリの失敗（UNAVAILABLE など）のときだけです（`Clipboard.h:274-275`）。
- 1.x も、読めない要求は `deliverInvalidRequest` で返し、ライブラリを呼びませんでした（1.13.0 `UnityIosClipboardManager.swift:285-288`）。このとき前の監視の変化は、条件なしの `FireClipboardChanged` で共通の `ClipboardChanged` に出続けていました（1.13.0 の Manager の `OnClipboardChanged`）。
- 公開の約束は「A second **successful** start replaces the previous observation」です（HEAD `IosClipboardManager.cs:905`）。

**壊れ方**
- `Named("a")` を監視しているとします（状態は立っています）。
- この間に `StartObserving(IosPasteboardScope.Named("x\0y"))` を呼ぶと、D-8 (5) で `CLIPBOARD_INVALID_REQUEST` になり、D-6 で状態が下ります。
- ネイティブは `"a"` の監視を続け、変化はリスナーに届きます。ところが D-6 (3) で解放だけされて捨てられます。
- 1.x では、`ClipboardChanged` は前の監視のぶん出続けていました。
- 利用者からは、監視が止まったようにしか見えません。StopObserving を呼ぶまで、ネイティブの監視が裏で動き続けます。3 章にも 5 章にもありません。

**直し案**
- D-6 (2) を次のように直す。
  - 状態を下ろすのは、受け付けた後の StartObserving の失敗の完了と、StopObserving の結果と、破棄だけにする。
  - 入口の拒み（C# の D-8 (4)(5)、`NONE` 以外の戻り値、登録の拒み）では、状態を変えない。
  - 世代の後片付け（`ReleaseChangeRegistrationIfOwned`）は今のまま通す。1.x と同じく、拒まれた start の `onChanged` は外れます。
- 4 章の Editor の PlayMode に、次の確かめを足す。
  - 「監視中に拒まれた start の後も、共通のイベントが出る」
  - 「受け付け後の失敗では出ない」

### R-2 [B] D-8 (5): 画像の道を「値」に入れているので、U+0000 で切れた道が別の実在のファイルを指し、そのファイルの中身が成功で pasteboard に載る

**根拠**
- 設計 D-8（56 行）は、道を「値（…URL、道…）」として、警告だけで渡しています。名前と型を拒む理由は「`"x\0y"` が別の pasteboard `"x"` を指すのを防ぐ」です。道もファイルを指す識別子なので、同じ理由が当てはまります。
- C ABI は、道を NUL まで読みます（`Clipboard.h:8`、`ClipboardMapping.swift:170-173`）。
- POSIX の道には U+0000 を含められません。U+0000 を含む道を拒んでも、正しい要求を拒むことにはなりません。
- 手本の Share（`IosShareCApi.cs:154`）も道を警告で渡しています。ただ Share は、利用者が共有シートで中身を見て選びます。Copy は確かめ無しに、他のアプリから読める pasteboard に書きます。

**壊れ方**
- `Copy(IosClipboardContent.ImageFile("/…/Documents/avatar.png\0.tmp"))` が、`/…/Documents/avatar.png` をコピーして成功を返します。
- 1.x では、全文の道がライブラリに届いていました。その名前のファイルは実在しえないので、成功にはならなかったはずです（`FILE_NOT_FOUND` か `IMAGE_LOAD_FAILED` の見込み。UIKit・Foundation が NUL をどう扱うかは確かめていないので、推測です）。
- 外から来た文字列で道を組む利用者では、意図しないファイルが pasteboard に漏れます。3 章の「書き込む値の中の U+0000（成功のまま）」の行では、この形は読み取れません。

**直し案**
- D-8 (5) の拒む側に「ImageFile の道」を移す（`CLIPBOARD_INVALID_REQUEST`「The request is invalid.」）。
- 3 章の「名前と型の中の U+0000」の行を「名前・型・道」に直す。
- Share と扱いを分ける理由（確かめの有無、他のアプリに見えること）を、理由の欄に 1 行書く。

### R-3 [C] D-12: 積んだ塊の一生が「破棄の後なら捨てる」だけでは足りない（Editor の後片付けと、ディスパッチャーが無いとき）

**根拠**
- D-12（60 行）の塊は、配るときに枠と印を取ります。ここが、今の `DispatchRejectedResult`（HEAD:442-457）との違いです。今の拒みの経路は、枠にも印にも触りません。
- Editor の結合テストの `TearDown` は、`DestroyImmediate` の後に `ResetForTests()` を呼びます（HEAD `IosClipboardManagerIntegrationTests.cs:31-38`）。`ResetCore` は `s_isTerminated = false` に戻します（HEAD `IosClipboardManager.cs:348-357`）。
- ディスパッチャーが壊れていると、`Dispatch` は「No dispatcher; result dropped」で積みません（HEAD:633-644）。これを確かめるテストもあります（HEAD `IosClipboardManagerIntegrationTests.cs:811-830`）。

**壊れ方**
- **Editor の PlayMode**: 4 章で足す「同じフレームの 2 回目が BUSY」のテストを考えます。yield せずに終わると、塊がディスパッチャーに残ります。後片付けで `s_isTerminated` が false に戻るので、次のテストの最初の Update で塊が「破棄の後ではない」と判定されます。塊は次のテストの `s_onCopy` を奪い、そのテストの印を外し、前のテストの失敗をそのテストの受け手に配ります。順によって落ちる、不安定なテストになります。
- **ディスパッチャーが無いとき**: D-12 の塊が積めないと、印を外す者がいなくなります。その種類は以後ずっと `CLIPBOARD_BUSY` になります。今の通常の経路は、`EndOperation` を `Dispatch` の前に済ませているので、こうはなりません。

**直し案**
- `ResetCore` と `OnDestroy` で増やす一生の番号を、塊に持たせる。配るときに番号が違えば捨てる。
- 積めなかったら、その場で枠と印を外してログを出す（今の `Dispatch` の落とし方に合わせる）。

### R-4 [C] D-12 は「次のフレーム以降」を保証しないので、3 章の「入口で拒まれる要求の時点と BUSY: 同じ」は言い過ぎ

**根拠**
- 1.x の入口の拒みは `deliverOnMain`（GCD の主の待ち行列）を回ってから C# に来ました（1.13.0 `UnityIosClipboardManager.swift:370-373`）。早くても、フレームとフレームの間です。
- D-12 は、呼び出しの中でディスパッチャーに積みます。ディスパッチャーは、Update の中で新しく積まれたものも同じ回で回します（`UnityMainThreadDispatcher.cs:59-62`）。

**壊れ方**
- 呼び出しがディスパッチャーの Update より前に回るスクリプトにあると、同じフレームのうちに配られ、印も外れます。結果の受け手の中で、続けて別の操作を呼ぶような形のことです。
- 同じフレームの後のスクリプトで 2 回目を呼ぶと、1.x の BUSY ではなく受け付けになります。
- 1 回目が D-12 で拒まれ、2 回目が BUSY になる場合の配りの順も変わります。1.x は BUSY が先でした（BUSY はその場で積まれ、1 回目の失敗は主の待ち行列の後）。D-12 では 1 回目の失敗が先です。
- CP では捕まりません。影響は小さいです。

**直し案**
- 3 章のその行を「ほぼ同じ（同じフレームの後のスクリプトやディスパッチャーの中からの呼び出しでは、同じフレームで配られる）」とし、既知の差分にする。
- 次のフレームを保証したいなら、ディスパッチャーではなく Manager 自身の待ちの一覧に入れ、次のフレームの頭で回す。「同じフレームなら積み直す」はやめる。ディスパッチャーは空になるまで回すので、`Time.frameCount` が変わらないまま回り続けて止まります。

### R-5 [C] D-4・D-12 の言い方が今の `Handle*` と食い違う。Editor のテストが本番の門を通るかが書かれていない

**根拠**
- 今の `HandleObservationControlCallback`→`HandleOperationCallback`→`FireOperationResult` は、最後に `Dispatch` で積み直します（HEAD:611-616, 633-644, 1385-1405）。
- D-4（52 行）は「`HandleObservationControlCallback` を通る」と書いています。D-12 は「`Handle*`（…受け手を**その場で**呼ぶ）。積み直さない」と書いています。そのまま今の関数を呼ぶと、D-12 が禁じた積み直しになります。
- 変化の Editor の入口 `DeliverChangeEventForTests` は、そのまま `FireClipboardChanged` を呼びます（HEAD:1433-1439）。D-10 では、受け口の本体は Player だけで組まれます。

**壊れ方**
- 実装する人が今の `Handle*` を使えば、D-12 の理由（追い越し）が崩れます。
- 4 章の Editor の PlayMode「D-6: 状態が下りている間の変化は出ない」は、門が Player だけの受け口に置かれると、門を通らない入口を照らすだけになります。R-10 と同じ形です。

**直し案**
- D-12 に「15 の結果の型ごとに、印と枠を外して**その場で** `InvokeInOrder` する `Deliver*Now` を足す。観測の後片付けはそれと組み合わせる」と書く。
- D-6 の門（破棄・状態の判定）と D-12 の塊の中身は、Editor でも組まれる関数に置く。`DeliverChangeEventForTests` もその関数を通す、と書く。

### R-6 [C] D-8 (5) の一覧の抜けと、3 章の「成功のまま」

**根拠と壊れ方**
- D-8 (5)（56 行）の「名前と型」の一覧に、**ImageData の `utType`** がありません。CustomData の型は入っています。値の一覧にも無いので、実装する人の判断になります（`write_content.ut_type` は IMAGE_DATA と CUSTOM_DATA で共用です。`Clipboard.h:193`）。
- 3 章 87 行の「U+0000 の前で切れ、警告が出る（成功のまま）」は、いつもは成り立ちません。
  - `PlainText("\0abc")` は空になるので、ライブラリの `CLIPBOARD_EMPTY_CONTENT` になります。
  - `Url("\0…")` は `CLIPBOARD_INVALID_URL` になります。
  - 空の道は入口で `INVALID_PARAMETER` になります（`ClipboardMapping.swift:172`）。

**直し案**
- ImageData の型を拒む側に入れる。
- 3 章は「切れた値で処理される（空になればその失敗）」とする。

### R-7 [C] 3 章の 1.x の列の事実

**根拠と壊れ方**
- 90 行「他の部品が同じプロセスで監視を置き換えたとき | 1.x: 起きない（ブリッジの監視は別）」は誤りです。
  - 1.x のライブラリも、プロセスで 1 つの `shared` で（1.13.0 `IosLibrary/…/IosClipboardManager.swift:54`）、start のたびに前の監視を止めていました（同 325-330）。
  - IosLibrary を直接使う部品があれば、1.x でもブリッジの監視は知らずに止まっていました。
  - 差分は「v2 では置き換えた先の変化も Manager に出る」の 1 点だけです。
- 86 行「入口で拒まれた要求のログ | 1.x: 出ない」は、読み取り系の操作では誤りです。
  - 1.x は、読めない要求を `invalidRequestJSON()` の封筒で返していました（1.13.0 `UnityIosClipboardManager.swift:214, 375-378`）。
  - C# の `ReadErrorInfo` がこれを `LogError` に出していました（HEAD `IosClipboardJsonParser.cs:449-484`）。
  - 「出ない」が言えるのは、done の系（Copy など）だけです。

**直し案**
- 1.x の列を、上の事実に合わせて直す。

### R-8 [C] 出典と参照

- **D-7**（55 行）の「リスナーの `release` が呼ばれたら…（D-13 で照らす）」の参照先が違います。D-13 は EditMode の純粋な部分です。照らしは 4 章 105 行の UI のハーネス（`listener-released=`）です。
  - レビュー v1 の判断（「Player のテスト（破棄の後に印が立つ）」）からの変更でもあります。Player のテストで Manager を壊すと墓標が残り、後のテストがすべて拒まれます。この理由を 1 行書いておくとよいです。
  - `release` は `listener_remove` の中では呼ばれません。`enqueueRemoval`→`runRemoval`→`tryRelease` と、主の待ち行列を回ってから呼ばれます（`Registry.swift:148-151, 212-218, 295-301`）。ハーネスは破棄の後に読むので問題ありませんが、D-7 に書いておくと実装する人が迷いません。
- **F-11**（43 行）の「LoadItem の画像はライブラリの `maxLoadByteCount` で止まる」の出典 `ClipboardRepositoryImpl.swift:78-82` は readData の箇所です。`maxLoadByteCount` は `ClipboardProviderLoadExecutor.swift:165-217` にあります。
- **差分の表**（18 行）の「3 章に既知の差分 2 行」は、実際には 3 行（87〜89 行）です。

---

件数: A1 0 件、A2 0 件、B 2 件、C 6 件

## 2. Codex

前回の A1/B の修正状況は、設計上、次のとおりです。

| 前回の指摘 | 判定 |
|---|---|
| R-1（U+0000） | D-8 と既知の差分への記載で対応済み（設計 v2:56、同87〜89行）。 |
| R2-1（入口拒否での早期解除） | 同一スタックの二重呼び出しには対応。ただし、D-12 に下記 R3-1 がある。 |
| R2-2・R-5（停止後の通知） | 状態による遮断を追加。ただし、状態更新の対象には下記 R3-2 の曖昧さが残る。 |
| R-6（ログ・監視制御の後処理） | D-4 に対応済み（設計 v2:52）。 |
| R-7・R-8・R-9（結果変換・例外・64 MiB） | D-9 と表2.1に対応済み（設計 v2:57、同67〜76行）。 |
| R-10・R2-4（変換と完了順の検証） | D-13 と追加検証に対応済み。CP-31 の順序照合も明記（設計 v2:61、同103行）。 |
| R2-5（解除漏れの検証） | `release` の回数を観測する計画で対応済み（設計 v2:55、同105行）。 |

設計 v2 のパス: /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v2.md

**R3-1 — 区分：A1**

D-12 は、ディスパッチャーの実行中に呼ばれると「次の Update」へ遅延しません。

- **根拠**：設計 v2:60 は、合成完了をディスパッチャーへ積み、取り出した場所で直接配送します。しかし /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Common/UnityMainThreadDispatcher.cs:55 のループは、実行中に追加された処理も同じ Update 内で消化します。1.x の `native-toolkit@1.13.0:ios/UnityIosPlugin/UnityIosPlugin/Clipboard/UnityIosClipboardManager.swift:269–274` は、空の CancelLoads でも `Task { @MainActor … }` を経由していました。
- **失敗シナリオ**：CancelLoads の完了コールバックから、再び CancelLoads を呼びます。読み込み中の要求がなければネイティブを経由せず、D-12 の成功処理が同じループに追加され、その Update 内で再び完了します。繰り返す利用者コードでは Update が終了せず、画面が停止します。有限回でも、複数回の完了が同じフレームに集中し、設計83行の時点の約束が成立しません。
- **検証の不足**：設計102行の「同じフレームの2回目が BUSY」だけでは、完了コールバックから再度呼ぶ経路を捕まえません。
- **修正**：Clipboard の合成完了に、実行中の Update を越える配送境界を設けてください。完了コールバックから再要求しても同じ Update では完了しない検証を追加します。D-5 の順序も保つため、取消しの成功と対応する LoadItem の完了を同じ順序管理下で配送してください。単に既存 `Handle* → Fire* → Dispatch` を二重に積む修正では、D-12 が説明する追い越しを再発させます。

**R3-2 — 区分：B**

D-6 は、監視状態を下ろす「失敗」に、単一実行の枠を取得していない拒否も含めるかが未定義です。

- **根拠**：設計 v2:54 は「StopObserving の結果（成功も失敗も）・StartObserving の失敗」で状態を下ろします。一方、既存の /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:595 では、BUSY は `DispatchRejectedResult` へ進み、保留中の要求には触れません。これは同ファイル30〜32行の公開契約でもあります。
- **失敗シナリオ（文言を全失敗へ適用した場合）**：StartObserving の直後に StopObserving を呼び、後者が BUSY になります。開始のネイティブ完了で状態が立った後、先にキューへ積まれた BUSY 結果の配送で状態を下ろす実装が可能です。その後は開始成功が利用者へ届いても、変化をすべて捨て続けます。CP-34（/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:679）は開始成功と停止の BUSY しか照合せず、その後の通知を確認しません。
- **修正**：状態更新は、監視制御の枠を所有する要求の完了経路に限定すると明記してください。BUSY・スレッド違反など、枠取得前の拒否では状態も世代も変更しません。CP-34 相当の操作後に、共通イベントと登録コールバックが引き続き届く検証を追加してください。

件数：A1 1件、A2 0件、B 1件、C 0件。
## 3. 判断

**Codex の R3-1 が A1 なので止めない。** v3 で A1 と B を直し、3 回目（決まりの上限）は差分のレビューにする。C も安いので入れる。

| 指摘 | 判断 | v3 での直し |
|---|---|---|
| Codex の R3-1、サブの R-4（D-12 が同じフレームで配られる） | 直す（A1） | D-12 を書き直す。ディスパッチャーに積まず、Manager 自身の一覧に入れ、Manager の `Update` で「積んだフレームより後」のものだけを順に配る。配る間に積まれたもの（受け手の中からの呼び出し）は次のフレーム。一覧に何か待っている間は、ネイティブの完了の結果も同じ一覧の後ろに並べる（CP-31 の順）。Editor の PlayMode で、受け手の中から呼び直しても同じフレームでは配られないことを確かめる |
| Codex の R3-2（BUSY で状態が下りうる） | 直す（B） | D-6 に「状態を変えるのは、監視の枠を持つ要求の完了の経路だけ。BUSY・スレッド違反など枠の前の拒みは状態も世代も変えない」。Editor の PlayMode と Player（CP-40）で、BUSY の stop の後も変化が届くことを確かめる |
| サブの R-1（入口で拒まれた start で状態が下りる） | 直す（B） | D-6 (2) を「受け付けた後の start の失敗・stop の結果・破棄で下ろす。入口の拒みでは変えない」に。Editor の PlayMode で両方を確かめる |
| サブの R-2（画像の道の U+0000） | 直す（B） | 道を拒む側に移す。理由の欄に Share との違い（確かめが無く、他のアプリから読める） |
| サブの R-3（積んだ塊の一生） | 直す | 塊に一生の番号を持たせ、違えば捨てる。リセットで一覧を空にする。ディスパッチャーに頼らないので「積めない」は無くなる |
| サブの R-5（`Handle*` の積み直し、Editor の門） | 直す | D-12 に「配る間は `Dispatch` がその場で `InvokeInOrder` する」。D-6 の門は Editor でも組まれる関数に置き、`DeliverChangeEventForTests` も通す |
| サブの R-6（ImageData の型、「成功のまま」） | 直す | ImageData の型を拒む側に明記。3 章は「切れた値で処理される（空になればその失敗）」 |
| サブの R-7（3 章の 1.x の列） | 直す | 監視の置き換えと、入口で拒まれた読み取り系のログの行 |
| サブの R-8（出典と参照） | 直す | D-7 の参照、`release` の時点、F-11 の出典、差分の表の行数 |
