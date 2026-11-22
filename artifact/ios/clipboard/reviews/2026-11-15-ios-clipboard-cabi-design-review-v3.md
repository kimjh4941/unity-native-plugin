# iOS Clipboard の C ABI への写し方の設計 v3 のレビュー v3

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-clipboard-cabi-design-v3.md](../designs/2026-11-15-ios-clipboard-cabi-design-v3.md)
- 前の回: [2026-11-15-ios-clipboard-cabi-design-review-v2.md](2026-11-15-ios-clipboard-cabi-design-review-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。3 回目（決まりの上限）なので差分を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

対象は `artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v3.md` の 2 回目からの差分です。ファイルは編集していません。C# の行番号は、断りが無ければ作業ツリーの `Runtime/iOS/Clipboard/IosClipboardManager.cs` のものです。

**確かめて合っていたこと（2 回目の A1・B の直り）**

- **Codex の R3-1（A1）は直っています。**
  - 呼び出しの中で決めた結果は `CompleteLater` で `s_later` に入ります（1773-1774）。その結果はフレームの番号を持ちます。
  - `DeliverLater` は `Frame < frame` のものだけを配り、今のフレームのもので止まります（1786）。受け手の中から呼び直したものは同じフレームの番号になるので、次のフレームに回ります。
  - ディスパッチャーの「空になるまで回す」（`UnityMainThreadDispatcher.cs:59-62`）には載らないので、`Update` が終わらなくなることはありません。
  - CP-31 の順も保たれます。`cancel_load` の取り消しは `Registry.cancel`→`enqueueRemoval`→`MainQueue.async`→`runRemoval`→`completeAsCanceled` を通ります（`Registry.swift:148-151, 212-218, 282-292`）。つまり読み込みの完了は主の待ち行列を回ってから来ます。そのときには CancelLoads の結果がもう一覧にあるので、`Dispatch` の 740-745 で後ろに並びます。一覧が空になった後に来ても、cancel が先です。
  - Editor の PlayMode（`RejectedAtEntry_CalledAgainFromItsCallback_WaitsForAnotherFrame`）と CP-37 で照らせています。
- **Codex の R3-2（B）は直っています。** BUSY は `TryStartOperation` の 5 段目（694-701）で、状態にも世代にも触らずに返ります。CP-40 と `BusyStop_LeavesTheObservationOn` もあります。
- **サブの R-1（B）は直っています。** 入口の拒みは `acceptedByLibrary: false` で状態を変えません（1696-1697, 1834）。受け付けた後の失敗で下ろすのも正しいです。ライブラリの `startObserving` は解決の前に必ず `stopObservingInternal()` を呼ぶので（native-toolkit `IosLibrary/…/IosClipboardManager.swift:502-505`）、受け付けた後の失敗では、どの値でも前の監視は止まっています。
- **サブの R-2（B）は直っています。** `ContentTypeHasNul` が `Path` を見ています（`IosClipboardCApi.cs:354-365`）。
- **v2 の C のうち塊の一生（R-3）、時点の言い過ぎ（R-4）、`Handle*` の積み直しと Editor の門（R-5）も、設計と実装の両方で直っています。**
  - 一生の番号と、リセットで一覧を空にすること（443, 482）。
  - `DropsChange` が Editor でも組まれ、`DeliverChangeEventForTests` も通ること（1871, 2168）。
- **D-12 を、時点・順・印・破棄・リセットで追いました。** 通常の使い方では成り立っています。
  - 一覧の頭が待っている間、後ろに並んだネイティブの結果は `int.MinValue` です。頭が配られれば、同じ回で配られます。
  - 毎フレーム拒まれ続けても、ネイティブの結果は 1 フレームずつ遅れるだけで、止まり続けることはありません。
  - 破棄で、呼び出しの中で決めた結果は捨てられます（1823）。リセットでは一覧が空になります。
- 以下は、その上で見つけたものです。

---

### R-1 [B] D-12: 「配っている間は `Dispatch` がその場で `InvokeInOrder`」の範囲が広すぎ、D-12 の一覧には `InvokeNative` の「could not be started」が入っていない。受け手の中からの呼び出しで、結果が呼び出しの中で同期に返る

**根拠**
- 設計 D-12（71 行）は、あとで配るものを「入口の拒み、D-8 の失敗、CancelLoads の成功、登録の拒み」としています。そのうえで「配っている間は `Dispatch` が積み直さず、その場で `InvokeInOrder` する」と書いています。
- 実装では、`s_deliverInline` が `DeliverLater` の回全体で立っています（1783-1808）。利用者の受け手が動いている間もです。
- `InvokeNative` が例外を受けると、`FireOperationResult`／`Fire*Result`→`Dispatch` で失敗を出します（1609-1624）。たとえば `AllocHGlobal` の `OutOfMemoryException`、記号の欠けた xcframework での `EntryPointNotFoundException` です。D-5 の「`cancel_load` が投げたら」も同じ経路です。
- `Dispatch` は `s_deliverInline` を最初に見て、その場で呼びます（735-739）。

**壊れ方**
- R の受け手（例: Copy の `CLIPBOARD_EMPTY_ITEMS`）の中で、利用者が `Copy(ImageData(巨大なバイト列))` を呼び直したとします。
  - `InvokeNative` の失敗は、`Copy()` が返る前に、同じ受け手へ同期に届きます。
  - 「失敗したら呼び直す」受け手では再帰が止まらず、スタックが尽きます。例外が毎回出る場合（記号の欠け）です。
- 1.x（HEAD:633-644）では、この経路は必ずディスパッチャーに積まれていました。呼び出しの中で受け手が呼ばれることは、ありませんでした。
- ディスパッチャーの中から呼んだ場合も問題です。一覧が空なら同じ `Update` で配られるので、R3-1 と同じ形がこの経路に残ります（これは 1.x からの既存）。
- 起きるのはまれです。それでも D-12 の中心の約束「呼び出しの中で決めた結果は、そのフレームでは配らない」の外にあり、v3 で初めて同期になります。

**直し案**
- D-12 の一覧に「`InvokeNative` が例外で始められなかったとき（D-5 の `cancel_load` を含む）」を足し、`CompleteLater` を通す。この呼び出しは枠と印を既に持っているので、「通常の完了の経路」がそのまま使えます。StartObserving・StopObserving の `onNativeFailure`（世代の後片付け）も、配る時に回します。
- 「配っている間」は「一覧の 1 つを配るその `Fire*` の中だけ」に狭める。実装なら、`Dispatch` が `s_deliverInline` を見たらすぐに下ろす（1 回だけ使う）形です。
- 4 章の Editor の PlayMode に「R の受け手の中で `InvokeNative` が失敗しても、呼び出しの中では返らない」を足す。

### R-2 [C] D-12 と D-6: 入口で拒まれた start の `onChanged` に、その失敗の**後**で変化が届く（世代を外すのは配る時、変化が受け手を取るのは届いた時）

**根拠**
- StartObserving は、呼び出しの時に `s_onChanged` を新しい受け手に替えます（1094-1096。1.x も同じで HEAD:931）。
- 入口の拒みで世代を外すのは、R が**配られる時**です（`CompleteWithError`→`HandleObservationControlCallback`→`ReleaseChangeRegistrationIfOwned`。1832-1834, 1688-1691）。
- 変化は、**届いた時**の `s_onChanged` を取って一覧の後ろに並びます（`FireClipboardChanged` 820-823、`Dispatch` 740-745）。
- 1.x の拒みは、呼び出しの時に `Task { @MainActor }` で主に積まれました（1.13.0 `UnityIosClipboardManager.swift:285-287, 365-373`）。それより後に来た変化は、登録が外れた後に処理されるので、拒まれた受け手には届きませんでした。

**壊れ方**
- `Named("a")` を `onA` で監視中に、`StartObserving(Named("x\0y"), onB)` を呼びます。D-8 (5) の R になります。
- 次の `Update` までの間に `"a"` が変わると、その変化は `onB` を取って R の後ろに並びます。
- 次のフレームで、まず `onB` の start の失敗（`CLIPBOARD_INVALID_REQUEST`）が届きます。その後で、変化が `ClipboardChanged` と **`onB`** に届きます。失敗した登録が、失敗の後で呼ばれます。
- 今の入口の拒みのうち、利用者の入力で起きるのは U+0000 の名前だけで、それはもともと既知の差分です（99 行）。そのため影響は小さいです。

**直し案**
- 変化を一覧に並べる時は、受け手と一緒に世代（`s_onChangedGeneration`）を持たせる。配る時に世代が変わっていれば、受け手を外して共通のイベントだけ出す。
- D-12 に「一覧の変化の受け手は、配る時に今の登録か確かめる」の 1 行を書き、Editor の PlayMode に 1 件足す。

### R-3 [C] D-4 の「拒まれた StartObserving・StopObserving は `HandleObservationControlCallback`（世代の後片付けと **D-6 の状態**）を通る」が D-6 (2) と食い違う

**根拠**
- D-4（63 行）は v2 のままです。一方 D-6 (2)（65 行）は「入口の拒み…も状態を変えない」です。
- 実装は `acceptedByLibrary: false` で状態に触りません（1696, 1834）。

**壊れ方**
- D-4 だけを読んで実装すると、v2 の R-1 が戻ります。

**直し案**
- D-4 の括弧を「世代の後片付け。D-6 の状態は変えない（D-6 (2)）」にする。

### R-4 [C] 3 章 101 行「Manager の状態が立っている間は、置き換えた先の変化も出る」は、他の部品が C ABI で start したときだけ正しい

**根拠**
- C ABI の start は、ライブラリの `startObserving(scope:) { receive($0) }` に自分の受け口を渡します（native-toolkit `ClipboardReceiver.swift:320-322`）。
- ライブラリは 1 つの `onEvent` だけを持ち、新しい start で置き換えます（`IosLibrary/…/IosClipboardManager.swift:502, 512`）。

**壊れ方**
- `IosLibrary` を直接使う部品が start すると、`onEvent` がその部品のものに替わります。C ABI のリスナーには何も来ません。これは 1.x と同じ振る舞いです。
- 「出る」のは、別の C ABI の利用者が `ntk_clipboard_start_observing_async` を呼んだときだけです。

**直し案**
- 2.0.0 の列を「C ABI を通して置き換えたときは置き換えた先の変化も出る。ライブラリを直接使って置き換えたときは 1.x と同じく出ない」にする。

### R-5 [C] 4 章のテストに、v3 で変えたことの照らしが 2 つ無い

**根拠と壊れ方**
- **D-8 (5) の画像の道**: v3 で拒む側に移しました（67 行）。ところが 4 章（112-114 行）の照らしは「U+0000 の名前」「組み立て（…U+0000…）」だけで、道は書かれていません。Player の CP-38 も名前と ReadData の型だけです（`IosClipboardPlayerTests.cs:760-776`）。v2 の R-2 の壊れ方（別の実在のファイルが成功で載る）を照らす 1 件が無いことになります。
- **D-12 の「破棄のとき、ネイティブの完了のものはディスパッチャーに渡す」**: Editor の PlayMode の「破棄の後は捨てる」（`RejectedAtEntry_AfterDestroy_IsDropped`）は、捨てる側しか照らしていません。渡す側（R の後ろに並んだ結果が、破棄の後も 1 回届く）が照らされていません。

**直し案**
- EditMode の組み立ての照らしに `ImageFile("…\0…")`→`ContentTypeHasNul` を、Editor の PlayMode に Copy の `CLIPBOARD_INVALID_REQUEST` を足す。
- Editor の PlayMode に「R と、その後ろの完了を積んで `DestroyImmediate` すると、完了だけが 1 回届く」を足す。

---

## 実装について（設計の指摘とは別）

- **R-1 の実装の側**: `Dispatch` の 735-739 と `DeliverLater` の 1783 です。設計の直し案に合わせて、`s_deliverInline` を 1 回だけ使う形にし、`InvokeNative` の catch（1609-1624）を `CompleteLater` に寄せるとよいです。
- **`DropsChange`（1871）のログ**: 破棄の後の変化 1 件ごとに `DiscardIfTerminated(OperationStartObserving)` が呼ばれます。そのため「Discarded a late startObserving callback from a destroyed lifetime.」が出て、変化なのに start の完了と読めます。破棄の判定を `s_isTerminated` で直接見るか、名前に `"change"` を渡すとよいです。
- **`HandOverLaterResults`（1819-1820）**: ディスパッチャーが無いときに、ネイティブの完了を何も言わずに捨てます。`Dispatch`（750）と同じく `LogError` を 1 行出すとよいです。
- **クラスの doc（21-27）**: 「C ABI design v2」のままです。また、U+0000 で拒むのは「a pasteboard name or a type identifier」と書かれていて、v3 の画像の道が入っていません。
- 確かめたが問題なかったもの:
  - `OnLoad` が `DiscardIfTerminated` の前に要求の ID を 0 に戻す（2076）。
  - 登録が拒まれたら `start_observing_async` を呼ばずに R を積む（1111-1119）。
  - `ResetCore` が一覧を空にしてから一生の番号を進める（443, 482）。
  - 破棄の印、`ClearAllPendingCallbacks`、`HandOverLaterResults` の順（318-332）。
  - `DeliverLater` の再入の止め（1782）と、`finally` で印を下ろすこと（1805-1808）。

件数: A1 0 件、A2 0 件、B 1 件、C 4 件
## 2. Codex

前回の **R3-1・R3-2・サブ R-1・R-2 は修正済み**です。[設計 v3:71](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v3.md:71) のフレーム境界、[D-6:65](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v3.md:65) の状態更新経路の限定、[D-8:67](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v3.md:67) の画像パスの U+0000 拒否に反映されています。ただし、書き直した D-12 に新しい問題が 2 件あります。

**R4-1 — 区分：A1**

D-12 の配送先の分離により、停止結果が先に待っていた変化通知を追い越し、停止後通知禁止の公開契約が破れます。

- **根拠**：[設計 v3:71](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v3.md:71) は、一覧ができた後の結果だけを一覧に追加し、Manager の `Update` から直接配送します。既存ディスパッチャーに積まれた結果との順序は維持しません。現実装も [Dispatch:735](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:735) でこの分岐を行います。変化の遮断は [OnChange:2113](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:2113) の受信時だけなので、既に積まれた通知には働きません。公開契約は [StopObserving:1135](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:1135) の「結果到着後は変化を配送しない」です。
- **失敗シナリオ**：Manager の後に別の部品が C ABI の変化リスナーを登録し、そのコールバックから `CancelLoads(); StopObserving();` を呼びます。ネイティブは[登録順にリスナーを呼ぶ](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Common/Registry.swift:429)ため、まず Manager の変化通知が既存ディスパッチャーへ入り、その後 CancelLoads が自前一覧に入ります。停止完了も次の Unity フレームより前に届けば一覧へ追加されます。次のフレームで Manager が先に更新されると、公開配送は **cancel → stop → 古い変化通知**になります。1.x の単一 FIFO では古い変化通知が停止結果より先でした。
- **検証の不足**：[設計 v3:113](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v3.md:113) は、合成結果より後に届くネイティブ結果と、停止後に受信する変化を扱っています。既存ディスパッチャーに先行通知が残る場合を扱っていません。
- **修正**：期限に達した一覧の処理を既存ディスパッチャーの末尾へ順に渡すなど、先行通知も含めて配送順を維持してください。そこで通常完了処理を同期実行し、再要求は自前一覧で次フレームを待たせれば R3-1 の再発も防げます。上記の先行通知を含む順序検証を追加します。

**R4-2 — 区分：A1**

Manager の無効化で自前一覧が停止し、結果が届かず BUSY が残り続けます。

- **根拠**：[設計 v3:71](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v3.md:71) は配送を Manager 自身の `Update` に依存させています。現実装でも唯一の自動配送入口は [Update:305](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:305) です。一方、[要求の受け付け:693](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:693) に有効状態の条件はありません。従来の配送先は[別 GameObject のディスパッチャー](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Common/UnityMainThreadDispatcher.cs:28)でした。
- **失敗シナリオ**：取得済み Manager に `manager.enabled = false` を設定してから、読み込みのない `CancelLoads()` を呼びます。成功処理は一覧へ入りますが `Update` が来ず、コールバックも単一実行の印の解除も行われません。再要求は BUSY になります。さらに一覧が残るため、別操作のネイティブ完了もそこに滞留します。要求直後に無効化する場合や GameObject の非アクティブ化でも同様です。
- **検証の不足**：[設計 v3:113](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v3.md:113) に無効化・非アクティブ化のケースはありません。
- **修正**：フレーム境界を守る配送処理を、Manager の有効状態から独立した常駐処理へ置いてください。無効化の前後に要求しても、結果が届き、印が解除され、後続結果も配送されることを検証します。

両件とも設計の指摘で、現実装にも同じ問題があります。実装固有の追加指摘はありません。破棄・リセットについても、今回の差分内で独立した追加問題は確認していません。

ファイルは変更していません。Unity・実機テストは未実行です。整合性スクリプトは終了コード 1 で、外部参照の `1.13.0` と `4.4` を内部の未定義節として検出しました。

件数：設計 A1 2件、A2 0件、B 0件、C 0件。実装固有 0件。
## 3. 判断

**Codex の R4-1・R4-2 が A1 で、3 回で止まらなかった。** 決まりにより、ここでレビューを止めて利用者に確かめる。利用者が不在の間は推奨案で進めるので、A1 と B を直した v4（レビューしていない）を作り、実装に入れる。v4 の D-12 の仕組みは、後の実装のレビュー（レビュアーを替えて全体を見る）で照らす。

| 指摘 | 判断 | v4 での直し |
|---|---|---|
| Codex の R4-1（一覧の結果がディスパッチャーに先に積まれた変化を追い越す） | 直す（A1） | 期限に来た塊は、ディスパッチャーの末尾に順に渡す。Editor の PlayMode で、先に積まれた変化が先に出ることを確かめる |
| Codex の R4-2（Manager を無効にすると BUSY が残る） | 直す（A1） | フレームの境を見る役を、ディスパッチャーの GameObject に付けた常駐の部品に置く。Editor の PlayMode で、Manager を無効にしても配られることを確かめる |
| サブの R-1（その場で配る範囲が広い、`InvokeNative` の失敗が同期） | 直す（B） | `InvokeNative` の例外（D-5 の `cancel_load` を含む）も一覧に入れる。その場で配るのは、塊 1 つの `Fire*` の中の 1 回だけ。Editor の PlayMode で確かめる |
| サブの R-2（拒まれた start の受け手に、失敗の後で変化が届く） | 直す（C、安い） | 変化に世代を持たせ、配る時に世代が変わっていれば受け手を外す |
| サブの R-3（D-4 の書き方） | 直す | D-4 の括弧 |
| サブの R-4（3 章の置き換えの行） | 直す | C ABI を通した置き換えとライブラリを直接使った置き換えを分ける |
| サブの R-5（テストの抜け） | 直す | EditMode に道（既にある）、Editor の PlayMode に道の Copy と、破棄でネイティブの完了だけが届くこと |
| サブの「実装について」 | 直す | `DropsChange` のログの名前、`HandOverLaterResults` の `LogError`、クラスの doc |
