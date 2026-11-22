# iOS Clipboard 自動テスト計画 v2 のレビュー v2

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-clipboard-ui-test-plan-v2.md](../designs/2026-11-14-ios-clipboard-ui-test-plan-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。2 回目なので、v2 で変えた所と 1 回目の指摘の直りだけを見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

見たのは v2 の 6 章に挙がった変更と、1 回目の指摘が直ったかだけです。ファイルは編集していません。

### 裏を取った結果、合っていたもの（指摘なし）

- **CP-25**: 消した pasteboard の Read は、`PasteboardResolver.resolve` の `UIPasteboard(name:create:false)` が nil になり `pasteboardUnavailable` になる（PasteboardResolver.swift:26-29）。native-toolkit の U-8（remove の後に probe）はシミュレーターで passed（results/2026-08-11-…-result-v2.md:130）。実機の結果はまだ無い。
- **CP-26**: `firstMatchingProvider(.image)` に当たる provider が無く、`noMatchingItem` になる（ClipboardItemLoaderImpl.swift:72-75,125-126）。
- **CP-27**: C# は有限かどうかしか見ない（IosClipboardPayloads.cs:330）。1.5 はネイティブに届き、`validateColor` で `INVALID_COLOR` になる（ClipboardContentValidator.swift:99-104）。
- **CP-28**: C# は型を確かめない。`ntkcustom` は `UTType` で解けず、区切りが 1 つしかないので `isValidCustomIdentifier` も false になり、`INVALID_TYPE` になる（ClipboardTypeIdentifierValidator.swift:18-23,37-39）。
- **CP-29**: `public.png` は `validateImage` を通る（image に適合する）ので、`INVALID_TYPE` にはならない。その後 `makeItems` が `validateImageData` を呼び、`UIImage(data:)` が nil なら `INVALID_IMAGE_DATA` になる（CopyContentUseCase.swift:32-35、ClipboardRepositoryImpl.swift:161-163、ClipboardImageCoder.swift:32-34）。バイト列の選び方は R-6 を参照。
- **CP-30**: iOS 17 以降は `URL(string:"not a url")` が scheme の無い相対 URL を返し、17 より前は nil を返す。どちらでも `INVALID_URL` になる（ClipboardContentValidator.swift:80-84）。
- **CP-12 の添字**: MultipleText の項目は `public.plain-text` だけ、Url の項目は `public.url` だけを持つ（ClipboardMappers.swift:73,94）。`Append` は `addItems` で後ろに足す（RepositoryImpl.swift:66）。`itemSet(withPasteboardTypes:)` は `[1]` になる。
- **CP-22 と CP-22a**: 期限の確かめは書く前に行われる（CopyContentUseCase.swift:33）。C# は期限を秒に切り捨てて送る（IosClipboardJsonBuilder.cs:276-280）ので、「今 + 3 秒」は実際には 2〜3 秒先になる。すぐの Read には十分間に合う。
- **CP-34**: Start と Stop は同じキー `observation` を使う（IosClipboardManager.cs:1030,1074）。Start の完了の前の Stop は `CLIPBOARD_BUSY` になる。
- **CB-2b**: native-toolkit の CB2 が、拒否で `CLIPBOARD_LOAD_FAILED` になると実測で確かめている（ClipboardPasteDenyUITests.swift:41-45）。
- **CB-5 の 1.x 側**: `OnDestroy` は `s_isTerminated` を立ててから `clipboardStopObserving(null)` を呼ぶ（IosClipboardManager.cs:342,407）。ブリッジは NULL でも停止を実行する（Bridge.m:174-180）。変化のイベントは `DiscardIfTerminated(startObserving)` で警告を出して捨てる（IosClipboardManager.cs:1505,1599）。ハーネスはテストごとに入れ直す（test_ios_ui.sh:10,295-307）。



### R-1 [A1] 観点: CP-31 の逃げ道が CL-12 の退行を通す
- **根拠**: 計画 v2:68「読み込みが取り消し前に終われば成功が 1 回として記録する」。1.x では順序が決まっている。
  - LoadItem と CancelLoads は、どちらも `Task { @MainActor }` で main の列に積まれる（UnityIosClipboardManager.swift:257-264,269-274）。
  - 読み込みは発行のときに `requests` に登録され、provider の読み込みは非同期に始まる（ClipboardItemLoaderImpl.swift:55-58,77-79）。
  - 取り消しの Task は、読み込みの Task が動き出す前にもう積まれている。`cancelAll` は同期で `.cancelled` を返す（同 83-90,104-114）。
  - このため 1.x は必ず `CLIPBOARD_CANCELLED` になり、「先に終わる」経路は起きない。
- **壊れ方**: `2.0.0` の包みが ID を覚えず、CancelLoads が `true` を返すだけになったとする（CL-12 の、まさに移行の危険）。名前付きの pasteboard の文字列はすぐ読めるので、読み込みは成功で終わる。計画どおり成功も受け入れると、このテストは通ってしまい、CL-12 は確かめられない。
- **直し案**: 1.x の予想を「`CLIPBOARD_CANCELLED` がちょうど 1 回（成功は 0 回）」と決める。成功はテストの失敗にする。順序だけを記録にとどめる。

### R-2 [A2] 観点: CP-12 の「型の一覧は全体」が実物と違う
- **根拠**: `TypeIdentifiers` は最初の項目の型だけで、全項目の型は `AllTypeIdentifiers`（`[[String]]`）にある（ClipboardSnapshot.swift:16-19、ClipboardRepositoryImpl.swift:95-96、IosClipboardSnapshotResult.cs:35-39）。
- **壊れ方**: 「型の一覧は全体」を `TypeIdentifiers` に `public.url` が入ることとして照らすと、1.x で落ちる。
- **直し案**: 予想を次のように直す。
  - `NumberOfItems = 2`
  - `TypeIdentifiers` は最初の項目の型だけ（plain text）
  - `AllTypeIdentifiers` は 2 要素で、2 つ目に `public.url`
  - `MatchingItemIndexes = [1]`

### R-3 [A2] 観点: CB-5 の「破棄 → 書く」の間の待ちが決まっていない
- **根拠**: `Destroy` はフレームの終わりまで遅れ、その時点で `OnDestroy` が呼ばれる。1.x の停止も、すぐには効かず `Task { @MainActor }` で後に回る（UnityIosClipboardManager.swift:303-309）。計画 v2:95 は順序を書いているが、書き込みを始めてよい条件を書いていない。
- **壊れ方**: 次の 2 通りがある。
  - `OnDestroy` の前に書くと、イベントは普通に届いて `late=0` になる。ただし破棄の後を何も試していない空振りで、`2.0.0` の退行も通す。
  - `OnDestroy` の後でも、停止の Task より前に書くと、1.x で `late=1` になる。計画の「実測が違えば記録」に従うと、誤った基準が記録される。
- **直し案**: CB-5 の手順に「`IosClipboardManager.IsTerminated == true` を確かめ、1 フレーム以上（例: 0.5 秒）待ってから `.mm` が書く」と書く。行には、書いた時点の `terminated=True` も出す。

### R-4 [A2] 観点: CB-5 の `late` の数え方が `2.0.0` の正しい包みでも落ちる
- **根拠**: `DiscardIfTerminated` はどの操作の遅い完了にも「Discarded a late {operation} callback…」を出す（IosClipboardManager.cs:1499-1507）。停止の完了も同じ道を通る（同 1635-1644）。設計書 part2:528 と P2-28（:55）は「包みは**空の受け手**を渡す」としている。一方、計画 v2:95 は「必ずコールバックを渡す」とだけ書き、警告をすべて数える。
- **壊れ方**: 包みが OnDestroy で今のデリゲートを渡すと、停止と取り消しの完了が破棄の後に届き、`late=1〜2` になる。この場合はネイティブの監視が正しく止まっていても落ち、計画の「渡し忘れると late=1」と取り違える。
- **直し案**: 次の 2 点を書く。
  - 数えるのは変化のイベントを捨てた分だけにする（今の文言では `Discarded a late startObserving callback`。Start の完了は破棄の前に済んでいる前提）。または、行を `late-change=<n> late-other=<n>` に分ける。
  - `2.0.0` の列は「包みは空の受け手を渡す（8.3、P2-28）」にする。

### R-5 [B] 観点: CP-17 が 0 回のとき、CP-32・33・35 の扱いが無い
- **根拠**: 計画 v2:69,97 は、0 回のときの扱いを CP-17 と CB-5 にしか書いていない。ネイティブは `object: pasteboard` で通知を受け、その pasteboard は Copy とは別に解決したものになる（IosClipboardManager.swift:344-345,388-394）。1 回目の R-10 が懸念した点。
- **壊れ方**: 名前付きの pasteboard で通知が来なかった場合:
  - CP-33（停止の後は 0 回）と CP-35 は、何も確かめないまま通る。
  - `2.0.0` で停止が効かない退行（CL-14）を見逃す。
  - CP-32 は B=1 で落ちる。そのまま「実測に合わせる」と、CL-13 の確かめも消える。
- **直し案**: CP-17 が 0 回なら CP-32・33・35 も「確かめられない」と記録して外す（CB-5 と同じ扱い）。CL-13 と CL-14 を別の手段で確かめるかは、記録の文書で決める。

### R-6 [C] 観点: CP-29 のバイト列があいまい
- **根拠**: ネイティブが見るのは `UIImage(data:)` で解けるかだけで、形式と UTI が合っているかは見ない（ClipboardImageCoder.swift:32-34）。
- **壊れ方**: 「PNG でないバイト列」として JPEG を選ぶと Copy は成功し、`INVALID_IMAGE_DATA` の経路を試さない。
- **直し案**: 「どの画像の形式としても解けない、空でないバイト列（例: ASCII の `not an image`）」と書く。

### R-7 [C] 観点: 3 章の後始末に監視の停止が無い
- **根拠**: CP-32・34・35 は監視を開いたまま終わる（計画 v2:70,72,73、107）。
- **壊れ方**: 前のテストの `s_onChanged` と通知の登録が、次のテストまで残る。今の項目では害は小さいが、テストどうしが独立している前提が崩れる。
- **直し案**: 後始末に「監視を始めたテストは `StopObserving` の完了を待つ」を足す。

### R-8 [C] 観点: 3 章の「manager を直に呼ばない」に、scope を取らない操作の例外が無い
- **根拠**: 計画 v2:106。`CancelLoads`、`StopObserving`、`CreatePasteboard(Unique)` は scope を取らない。また、未完了を見る `IsInFlightForTests` と `InFlightCountForTests` は Editor だけにある（IosClipboardManager.cs:1691,1704）。
- **直し案**: 例外の一覧に上の 3 つを足す。「未完了の確かめは、補助の関数が呼んだ数と完了の数を自分で数える（Player では in-flight の集合を読めない）」と 1 行書く。



**件数**: A1 1 件（R-1）、A2 3 件（R-2、R-3、R-4）、B 1 件（R-5）、C 3 件（R-6、R-7、R-8）。合計 8 件。

## 2. Codex

- **B／CP-29 — エラー経路を確定できない入力です。** v2:57 の「PNGでないバイト列」には、正常なJPEGも含まれます。タグ `1.13.0` の `ios/IosLibrary/IosLibrary/Clipboard/Data/Image/ClipboardImageCoder.swift:27–39` は `UIImage(data:)` でデコード可能かを検証し、指定UTIと画像形式の一致は検証しません。また空データは `Application/UseCase/ClipboardContentValidator.swift:41–43` で `CLIPBOARD_EMPTY_CONTENT` になります。**修正:** 非空で、画像としてデコードできない固定バイト列を明示してください。予想の `INVALID_IMAGE_DATA` のCode・Message自体は正しいです。

- **B／CP-31 — 初回R-5の取消検証が未解消です。** v2:68 は、同じフレームで呼ぶだけで、取消前に成功した場合も許容しています。タグ `1.13.0` の `ios/IosLibrary/IosLibrary/Clipboard/Data/Repository/ClipboardItemLoaderImpl.swift:83–113` が取り消すのは未完了要求だけです。成功側だけを通れば、要求IDの保存・個別取消が欠落しても検出できません。**修正:** 完了を保留できるfixtureを用意し、未完了を確認してから取消を呼び、`CLIPBOARD_CANCELLED` と取消操作の成功を各1回必須にしてください。配送順序は固定不要です。

- **B／CB-5 — 初回R-7のネイティブ監視停止を証明できません。** v2:95 の「NULLを渡すと `late=1`」は保証されません。`/Users/jonghyunkim/Desktop/native-toolkit/dist/1.14.0/ios/include/NativeToolkitC/Clipboard.h:291–293` では、リスナーを解除しても監視は続きます。そのため、リスナーだけ解除し、停止がNULLコールバックで拒否されても `late=0` で通ります。`Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:1499–1506` の警告は、実際に届いた呼び出しだけを数えます。**修正:** 破棄後も残る独立したネイティブリスナー、または破棄処理が発行した停止要求の受付・完了を直接観測してください。

CP-12の`[1]`、CP-21・22・25〜28・30の検証経路とCode・Message、CP-22a、CP-32〜35、CB-2bの修正は一次資料と整合しています。3章の共通ルールも実装可能です。ファイルは編集していません。

件数：A1 0、A2 0、B 3、C 0。

（v2 は `/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-14-ios-clipboard-ui-test-plan-v2.md`）

## 3. 判断

A1 が 1 件（サブエージェントの R-1。Codex の CP-31 と同じ件）あるので、止めずに v3 を作り、3 回目のレビュー（差分だけ）に出す。3 回目でも A1 が残れば、利用者に聞く（止める基準の上限）。

- **A1（R-1、Codex の CP-31）**: CP-31 の 1.x の予想を「`CLIPBOARD_CANCELLED` がちょうど 1 回、読み込みの成功は 0 回」に決め、成功はテストの失敗にする（1.x では取り消しの Task が読み込みの Task より先に main の列に積まれ、必ず取り消される）。順序は記録だけ
- **B（Codex の CB-5）**: `2.0.0` ではリスナーを外しても監視は続く（`Clipboard.h` の `ntk_clipboard_listener_remove`）ので、C# が捨てた数だけでは、停止がされたかを証明できない。ハーネスの `.mm` が、`dlsym` で `ntk_clipboard_add_change_listener` が在れば（`2.0.0`）自分の受け手を別に足し、破棄の後の書き込みで、その受け手に届いた数も行に書く。1.x では `native-listener=none`
- **B（R-5）**: CP-17 が 0 回なら CP-32・33・35 も「確かめられない」と記録して外す
- **A2・C だが、手順や予想の行を直すだけで済むので直す**: R-2（CP-12 の `TypeIdentifiers` と `AllTypeIdentifiers`）、R-3（CB-5 は `IsTerminated` を確かめ 0.5 秒待ってから書く）、R-4（CB-5 は変化のイベントを捨てた分だけ数え、`2.0.0` の包みは空の受け手を渡す）、R-6 と Codex の CP-29（`not an image` の ASCII）、R-7（監視を始めたテストは `StopObserving` の完了を待つ）、R-8（scope を取らない操作の例外と、未完了の数え方）
