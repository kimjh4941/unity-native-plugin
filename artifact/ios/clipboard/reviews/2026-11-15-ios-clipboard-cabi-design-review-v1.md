# iOS Clipboard の C ABI への写し方の設計 v1 のレビュー v1

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-clipboard-cabi-design-v1.md](../designs/2026-11-15-ios-clipboard-cabi-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した（サブエージェントの出力の先頭の大見出しだけ、この記録の見出しと重なるので省いた）。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

対象は `artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v1.md` です。ファイルは編集していません。

**確かめて合っていたこと:**
- D-3 の文言は `1.13.0` の `ClipboardError.swift` と今の HEAD で差分がなく、1.x の写しとして使えます。
- エラーの値と文言は 1 対 1 で逆に引けます。`ClipboardMapping.swift:313-338` で値が重なるのは UNKNOWN だけで、文言は `unknownMessage` の 1 つです。
- 構造体の大きさは `StructLayoutTests.m` と一致します（24/32/136/24）。
- CP-31 の順（cancel → load）は D-5 のとおり保たれる見込みです。`Registry.cancel` は取り消しの完了を `MainQueue.async` に積むので、呼び出しの中では来ません（`Registry.swift:282-292`、`MainQueue.swift:12-18`）。
- `changedDetectedOnForeground` はライブラリのどこでも作られていません。C ABI がこれを捨てても、CP-17 の 2 回は変わらない見込みです。

レビューの途中で、作業ツリーに `Runtime/iOS/Clipboard/IosClipboardCApi.cs` が新しく現れました。実装が並行して進んでいるようです。このファイルはレビューしていません。

---

### R-1 [A1] 観点 2: U+0000 を含む文字列が、書き込みでは途中で切れ、読み取りでは U+FFFD になる。3 章に書かれていない
- 根拠
  - `Clipboard.h:7-8`（文字列は NUL で終わる UTF-8）、`Clipboard.h:37`（返す文字列の U+0000 は U+FFFD になる）
  - `IosNativeToolkitCApi.cs:64-70`（`ToUtf8WithTerminator` は NUL をそのまま書く）
  - 1.x は JSON で `\u0000` に逃がしていた（`IosClipboardJsonBuilder.cs:443-447`）
  - Share は同じ問題を D-5 と `IosShareCApi.HasNul` で扱っている（`IosShareCApi.cs:177-188`）
- 壊れ方
  - `PlainText("a\0b")` は 1.x では全文が渡りましたが、2.0.0 では `"a"` だけがコピーされ、成功で返ります。
  - `IosPasteboardScope.Named("x\0y")` は、別の pasteboard `"x"` を読み書きします。
  - 読み取りでは、1.x で U+0000 が返っていた文字が U+FFFD になります。
  - 5 章の確かめに NUL を含む入力は無いので、捕まりません。UIKit が NUL をそのまま保つかは確かめていません（推測）。ただ 1.x では、少なくともライブラリまでは全文が届いていました。
- 直し案
  - Share の D-5 に倣って、D-8 に扱いを決めて書く。候補は 3 つです。
    - (a) NUL を含む値を C# で `CLIPBOARD_INVALID_REQUEST` にする
    - (b) Share と同じく警告を出して渡す
    - (c) 既知の差分として受け入れる
  - 名前と型（scope 名、ut_type、matchingTypes）は (a) にするのが安全です。
  - 読み取りの U+FFFD は、3 章に既知の差分として足す。

### R-2 [A2] 観点 3: RemovePasteboard(General) に D-8 の「general は NULL」を当てると、CP-21 が INVALID_REQUEST に変わる
- 根拠
  - `Clipboard.h:248-251`（remove の scope は NULL にできない）
  - `ClipboardMapping.swift:67-70`（`requiredScope`）
  - part2 の CL-08「省略を general にして `CANNOT_REMOVE_GENERAL` を返す」
  - 設計 D-8（39 行）
- 壊れ方
  - remove に NULL を渡すと入口で `INVALID_PARAMETER` になり、結果は `CLIPBOARD_INVALID_REQUEST` に変わります。D-4 の `LogError` も出ます。
  - 1.x の `CLIPBOARD_CANNOT_REMOVE_GENERAL` / "The general pasteboard cannot be removed." にはなりません。
  - CP-21（`IosClipboardPlayerTests.cs:460`）が全文を照らしているので、V-2 で捕まります。
- 直し案
  - D-8 に「RemovePasteboard だけは General も `kind = GENERAL` の構造体で渡す」と書く。

### R-3 [A2] 観点 3: 模様のビットの作り方が決まっておらず、`1u << (int)pattern` で書くと、範囲外の enum 値が別の模様になる
- 根拠
  - 公開の enum の値 0〜10 は、ビットの位置と同じ並びです（`IosClipboardPayloads.cs:437-475`、`Clipboard.h:109-121`）。
  - C# のシフトは数を 32 で割った余りで回すので、`(IosClipboardDetectionPattern)35` は `1u<<3`（LINK）になります。
  - 1.x では、範囲外の値は `ToRawValue` の `"35"` をネイティブが読めず `CLIPBOARD_INVALID_REQUEST` でした。
  - C ABI でも、知らないビットは `INVALID_PARAMETER` です（`ClipboardMapping.swift:281-293`）。
- 壊れ方
  - 範囲外の値を渡すと、黙って別の模様を調べて成功します。
  - 5 章の EditMode の「模様のビット」が既知の値だけを見ていると捕まりません。影響は小さいです。
- 直し案
  - D-9（または D-8）に、表引きで写すことと、表に無い値は C# で `CLIPBOARD_INVALID_REQUEST` "The request is invalid." にすることを書く。
  - EditMode に範囲外の値の確かめを足す。

### R-4 [A2] 観点 2: CancelLoads が同期で終わるので、同じフレームに 2 回呼んだときの BUSY が変わる
- 根拠
  - 1.x の `cancelLoads` は、main actor の Task を回ってから完了していました（native-toolkit `UnityIosClipboardManager.swift:269-275`）。
  - 今の C# は、完了まで `s_inFlight` に印を持ちます（`IosClipboardManager.cs:878-899`）。
  - 設計 D-5（36 行）は、`cancel_load` を呼んでから成功を出すと書いています。
- 壊れ方
  - その場で `FireOperationResult` を呼ぶと印もその場で外れます。2 回目の CancelLoads は 1.x の `CLIPBOARD_BUSY` ではなく成功になります。
  - D-1 の「BUSY を公開の振る舞いとして残す」と食い違います。これを確かめる CP はありません。
- 直し案
  - D-5 で、どちらにするかを決めて書く。
    - 保つなら、印を次の `Update` の配送で外す。
    - 変えるなら、3 章に既知の差分として載せる。
  - あわせて「`InvokeNative` を使わない操作」として D-1 の例外に書く。

### R-5 [B] 観点 3・2: リスナーを Manager の一生のあいだ持つと、Manager が始めていない監視の変化も `ClipboardChanged` に出る
- 根拠
  - `Clipboard.h:274-281`：開始はプロセスで 1 つで、停止は「another component」のぶんも止める。プロセスの中に複数の部品がいる前提です。
  - `Clipboard.h:287-293`：リスナーは監視に紐づかない。
  - `Registry.swift:369-376, 429-441`：届いた変化は、生きている受け手のすべてに配る。
  - 今の `FireClipboardChanged` は条件なしで配送します（`IosClipboardManager.cs:710-713`）。
  - 公開の約束「Never raised before StartObserving succeeds」（`IosClipboardManager.cs:162-167`）と「No further change events are delivered once the result arrives」（同 948-950）。
  - 引き継ぎ 4.4 は、配送ごとに「今のハンドルか」で捨てる設計でした。
- 壊れ方
  - Manager の StopObserving の後でも、同じプロセスの別の部品（ホストのネイティブ、CB-5 の `.mm` のような計測、将来の native-toolkit の利用者）が `start_observing_async` を呼べば、`ClipboardChanged` が出ます。その pasteboard を Manager は頼んでいません。
  - 1.x では、ブリッジの受け手がブリッジ自身の監視にしか呼ばれなかったので、この形は起きませんでした。
  - 5 章では捕まりません。
- 直し案
  - D-6 に「C# が自分の監視の状態を持ち、自分が始めた監視の間だけ配送する」と書く。状態は start の成功で立て、stop の完了・start の失敗・破棄で下ろす。
  - ほかに書いておくこと:
    - `add_change_listener` は `start_observing_async` より先に呼ぶ。両方とも main の待ち行列に積まれ、その順に動きます（`Registry.swift:257, 364`）。
    - `add_change_listener` が `NONE` 以外を返したときの StartObserving の結果。
    - リスナーの `user_data`。

### R-6 [B] 観点 2・5: D-4 の「C# が組んだ要求の誤り」という前提が成り立たず、利用者の入力でも `LogError` が出る
- 根拠
  - 入口の拒みには利用者の入力から来るものがあります。
    - `MultipleText(new string[0])` と空の `MultiRepresentation` は `EMPTY_ITEMS`（`ClipboardMapping.swift:183, 186`）。公開の doc でも「An empty array fails with CLIPBOARD_EMPTY_ITEMS natively」（`IosClipboardPayloads.cs:300`）。
    - `matchingTypes` や `texts` に入った null は `INVALID_PARAMETER`（`CApiString.swift` の `requiredArray`）。
  - 1.x では、Copy などの失敗にログは出ませんでした（1.x の記録 2 章 54 行）。
  - Dialog の D-6 は「D-4 の後では (b) は起きない見込み」という前提で `LogError` を選んでいました。
- 壊れ方
  - 1.x では文書どおりの失敗だったものが、2.0.0 では Console のエラーになります。利用者の Unity テストは「予期しないエラーのログ」で落ちます。
  - 3 章に載っておらず、5 章でも捕まりません。
  - D-4 は「`Fire*Result` に出す」とだけあり、StartObserving・StopObserving が拒まれたときに `ReleaseChangeRegistrationIfOwned` と `s_pendingObservationGeneration = 0` を通すかも書いていません（今は `InvokeNative` の `onNativeFailure` が担っています。`IosClipboardManager.cs:941-945, 979-983`）。
- 直し案
  - `LogError` は、C# の組み立ての誤りでしか起きない値（`struct_size`、`reserved`、kind の範囲外）だけにする。`EMPTY_ITEMS` と利用者の null は Warning か無しにする。
  - 拒まれた StartObserving・StopObserving は `HandleObservationControlCallback` と同じ後片付けを通す、と書く。

### R-7 [B] 観点 3・5: 結果の型への写し方が値ごとに決まっていない。C ABI が返さない値は要求から補う必要がある
- 根拠
  - CreatePasteboard の完了は名前の文字列だけです（`Clipboard.h:144-146`、`ClipboardReceiver.swift:196-197`）。1.x は `{"scope":{"kind","name"}}` でした。
  - ReadData は `ntk_bytes` だけです（`Clipboard.h:139-141`）。1.x は `utType` を返し、`IosClipboardReadDataResult.Success(utType, bytes)` を作っていました（`IosClipboardJsonParser.cs:103-124`）。
  - 変化の scope は kind と名前で、取れなければ -1 です（`Clipboard.h:337-341`）。1.x の C# は、読めない scope を null、知らない kind を `Unknown` にしていました（`IosClipboardJsonParser.cs:385-401`）。
  - 読み込んだ項目の kind は IMAGE、-1 が NULL です。1.x の C# は `"imageData"` と `UnknownKind()` を使っていました（同 314-339）。
  - Snapshot の項目ごとの型の行数を返す関数はありません（`Clipboard.h:313-319`）。
- 壊れ方（実装する人がその場で決めることになる点）
  - CreatePasteboard の scope の kind を要求から写すこと。
  - ReadData の `UtType` を要求から覚えておくこと。
  - 変化の scope が GENERAL なら `IosPasteboardScope.General`、-1 なら null にすること。
  - change kind の 0・1 以外を `Unknown` にすること。
  - load kind の -1 と知らない値を `UnknownKind` にすること。
  - `AllTypeIdentifiers` の行数を `number_of_items` から取ること。
  - どれも CP で照らしていない値なので、間違えても 5 章で捕まりません（R-10）。
- 直し案
  - D-9 に、操作ごとの写し方の表（C ABI の値 → 公開の型の各プロパティ）を足す。

### R-8 [B] 観点 3: コールバックの中の例外と、読めなかったときの扱いが書かれていない
- 根拠
  - `Clipboard.h:36`（コールバックから例外を出してはいけない）
  - Share と Dialog は、読み取りの失敗を UNKNOWN の完了に写し、`finally` で解放しています（`IosShareManager.cs:205-231`、`IosDialogManager.cs:301-326`）。
  - D-9（40 行）は「読んでから `_free`」とだけ書いています。
- 壊れ方
  - 大きなバイト列の確保（R-9）や `ReadUtf8OrNull` の `OutOfMemoryException` で読み取りが投げると、`Fire*Result` が呼ばれません。その種類は `s_inFlight` に残り、以後ずっと `CLIPBOARD_BUSY` になります。IL2CPP では、逆 P/Invoke の外に例外が出ると落ちる恐れもあります。
  - `DiscardIfTerminated` で早く戻る経路では、ハンドル（特に `ntk_clipboard_change`）を解放しなければ漏れます。1.x は「解く前に捨てる」形でした。
- 直し案
  - D-9 に次の 3 つを書く。
    - 読み取りが投げたら `CLIPBOARD_UNKNOWN` "An unknown error occurred." で完了する。
    - `NONE` なのにハンドルが NULL なら UNKNOWN とする（ReadData の NULL は成功の「無い」なので除く）。
    - ハンドルは捨てる経路も含めて `finally` で解放する。

### R-9 [B] 観点 2: ReadData の 64 MiB の上限が消える
- 根拠
  - 1.x は C# で `MaxDataByteCount = 64 MiB` を超えた ReadData を `CLIPBOARD_CONTENT_TOO_LARGE` "The clipboard content exceeds the configured size limit." にしていました（`IosClipboardJsonParser.cs:29-30, 822-841`）。
  - ライブラリの `readData` に上限はありません（native-toolkit `ClipboardRepositoryImpl.swift:78-82`）。
  - C ABI は「The bytes are returned as they are」です（`Clipboard.h:230-231`）。
- 壊れ方
  - 64 MiB を超える型の ReadData が、1.x の失敗から成功に変わります。2 GiB を超えると `int` に収まらず、R-8 の例外の経路に入ります。
  - 3 章に無く、5 章でも捕まりません。
- 直し案
  - D-9 で、`ntk_bytes_size` が 64 MiB を超えたら C# で 1.x と同じコードと文言を返すと決める。外すなら 3 章に既知の差分として書く。
  - LoadItem の画像はライブラリが `maxLoadByteCount` で止めるので、同じです。

### R-10 [B] 観点 4: 5 章では、ハンドルから結果への写し間違いと D-5 の順が捕まらない
- 根拠
  - D-10 で、ハンドルの読み取りは Player だけで組まれます（`UNITY_IOS && !UNITY_EDITOR`）。
  - Editor の結合テストの完了の入口（`Complete*ForTests`）は JSON を受けるので（`IosClipboardManager.cs:1421-1439`）、本番では使われなくなる経路を通ります。
  - Player の CP は、次を照らしていません。
    - 変化の `Scope`（`Describe` は Kind と TypesAdded だけ）
    - CreatePasteboard(Named) の `Scope`
    - ReadData の `UtType`
    - Snapshot の `HasStrings`・`HasUrls`・`HasImages`・`HasColors`
    - LoadItem の Image と File
  - CP-31 は順を `Record` に書くだけで照らしていません（`IosClipboardPlayerTests.cs:571-588`）。
- 壊れ方
  - R-7 の写し間違いも、D-5 の順（cancel → load）の崩れも、V-2 を通ってしまいます。
- 直し案
  - Share の `ToResult` のように、「P/Invoke で値を取る部分」と「取った値から結果を作る純粋な部分」に分ける。後者を EditMode で照らす。
  - CP に `Scope`・`UtType`・`Has*` の照らしを足す。CP-31 に `order` の照らしを足す。

### R-11 [C] 観点 1: 出典の行のずれ
- F-1 の「`Clipboard.h:215-283`」は、check_foreground_change が 285 行まであるので 215-285 です。
- F-7 の「`ClipboardMapping.swift:9-23, 281-293`」は、拒みの一覧の大半（scope 58-98、内容 153-211、期限 225-233、load 253-262）を含みません。NULL のコールバックの拒みは `ClipboardReceiver.swift:58-61` です。F-7 には、空の画像の道（同 172）も挙がっていません。
- 基本情報の「設計 part2 の 11 章と CL-01〜CL-15」の CL の表は、11 章ではなく 8.3（part2 536-550 行）にあります。

### R-12 [C] 観点 3: D-2 と 4 章で、コールバックの数え方が食い違う
- D-2 は「`user_data` は操作の種類」で、4 章は「静的なコールバック（種類ごと）」です。
- 「コールバックの型ごとに 1 つ（done は 7 操作で共有）で、`user_data` で種類を分ける」のか「操作ごとに 1 つ」のかが読み取れません。
- 破棄のときの停止（D-7）にどの `user_data` を渡すかも書かれていません。
- 直し案: 型ごとの表（done・read・data・snapshot・name・patterns・values・load・foreground・change）と、各型を使う操作を書く。

### R-13 [C] 観点 3: D-7 の細部
- 次の 2 つが書かれていません。
  - `listener_remove` を `RunDestroyCleanup` のどの段に置くか。今は stop と cancel の 2 段で、stop の段が投げると残りが飛びます（`IosClipboardManager.cs:282-315`）。
  - 外した後にハンドルをすぐ `IntPtr.Zero` にすること。引き継ぎ 4.4 は、2 回外すのは未定義としています。
- 直し案: 3 段にするか、各呼び出しを別々の `try` にする。

### R-14 [C] 観点 2: D-8 の期限の写し方
- 1.x は `ToUniversalTime()` の後、秒の単位に切り捨てて送っていました（`IosClipboardJsonBuilder.cs:271-283`）。
- 「Unix 秒」とだけあるので、次が実装する人の判断になります。
  - `DateTimeKind.Unspecified` を地方時として扱うか
  - 秒未満を切り捨てるか
- 秒未満を残すと、1 秒以内の未来の期限で `INVALID_EXPIRATION` と成功が入れ替わることがあります。
- 直し案: 1.x と同じく、`ToUniversalTime()` の後に整数の秒にすると書く。

### R-15 [C] 観点 5: 4 章の「変えるもの」に足りない行
- `Assets/NativeToolkitIosPlayerTests/Editor/IosPlayerBuildSetup.cs` の `Defines` に `NTK_IOS_CABI_2_CLIPBOARD` を足す行。無いと Player のテストが組まれません（V-2 の「35 件」で気づく）。
- `Tools~/IosUiTests/DeviceRequirements.json` の `notMovedYet` を直す行。
- `agent-rules/coding-rules/common.md` の iOS の節。Dialog と Share は直しています。
- `IosClipboardManager` のクラスの doc の「Unavailable for now」と「The native ABI carries no request identifier」の書き換え。

---

件数: A1 1 件、A2 3 件、B 6 件、C 5 件


## 2. Codex

**R2-1 — A1：入口拒否で単一実行の印を早く解除し、1.xのBUSY契約が変わる**

- **根拠**：[設計書:35](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v1.md:35)は入口拒否を`Fire*Result`へ渡します。しかし[Manager:611](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:611)は配送前に単一実行の印を解除します。`ImageFile("")`は公開ファクトリーで生成でき、ABIでは[ClipboardMapping.swift:170](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Clipboard/ClipboardMapping.swift:170)により同期的に拒否されます。旧版は`native-toolkit@1.13.0:ios/UnityIosPlugin/UnityIosPlugin/Clipboard/UnityIosClipboardManager.swift:51`の`Task @MainActor`内で完了します。
- **失敗シナリオ**：同じ呼び出しスタックで`Copy(ImageFile(""))`を2回実行すると、設計どおりでは両方`CLIPBOARD_INVALID_REQUEST`になります。1.xでは最初の完了が保留されるため、2回目は`CLIPBOARD_BUSY`です。
- **5章での検出**：[CP-23:538](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:538)は`Read`の連続呼び出しだけで、同期的な入口拒否を通りません。
- **修正**：入口拒否の合成完了も非同期に処理し、`Fire*Result`実行まで要求のスロットと単一実行の印を保持する検証を追加してください。

**R2-2 — A1：停止後も保持するリスナーから、共通イベントが再び発火する**

- **根拠**：[設計書:37](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v1.md:37)はリスナーを破棄まで保持します。ABIは[ClipboardReceiver.swift:35](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Clipboard/ClipboardReceiver.swift:35)と[Registry.swift:429](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Common/Registry.swift:429)で全リスナーへ通知します。一方、停止時の[Manager:1398](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:1398)は登録コールバックを消すだけで、[Manager:710](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:710)は共通`ClipboardChanged`を発火できます。
- **失敗シナリオ**：Unity側で`StopObserving`が成功した後、別のネイティブコンポーネントが同じpasteboardの監視を開始すると、停止済みManagerにも`ClipboardChanged`が届きます。「停止結果後は通知しない」という[公開契約:948](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:948)に反します。
- **5章での検出**：[CP-33:650](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:650)は停止後にCopyするだけで、別コンポーネントによる監視再開を試しません。
- **修正**：Manager自身の監視状態で共通イベントも遮断するか、停止時にリスナーを解除して次の開始時に再登録してください。`onChanged`は省略可能なので、そのnull判定だけでは不足します。

**R2-3 — A2：Generalを常にNULLへ写すと、削除時のエラーが変わる**

- **根拠**：[設計書:39](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v1.md:39)はGeneralをNULLにしますが、[Clipboard.h:248](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Clipboard.h:248)は`remove_pasteboard_async`についてNULLを禁止しています。[ClipboardMapping.swift:67](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Clipboard/ClipboardMapping.swift:67)も必須構造体として読みます。
- **失敗シナリオ**：`RemovePasteboard(IosPasteboardScope.General)`が`CLIPBOARD_CANNOT_REMOVE_GENERAL`ではなく、入口拒否の`CLIPBOARD_INVALID_REQUEST`になります。
- **5章での検出**：V-2の[CP-21:460](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:460)で検出されます。
- **修正**：削除操作ではGeneralも`kind=GENERAL`の非NULL構造体として渡してください。

**R2-4 — B：CP-31は「cancel → load」の順序を検証していない**

- **根拠**：[設計書:36](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v1.md:36)は順序維持を要求しますが、[CP-31:575](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:575)は順序を収集・記録するだけです。[同:582](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:582)以降のassertは回数・エラー・成功だけです。
- **失敗シナリオ／5章での検出**：完了順が`load → cancel`へ逆転してもV-2は成功します。これは検証の欠落であり、現在の`cancel_load`実装が逆順になるという指摘ではありません。
- **修正**：`order`が`["cancel", "load"]`と一致するassertを追加してください。

**R2-5 — B：CB-5の`native-listener=0`ではリスナー解除漏れを検出できない**

- **根拠**：[設計書:38](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v1.md:38)は破棄時のリスナー解除にCB-5を対応させています。しかし[NtkUiHarnessOverlay.mm:147](/Users/jonghyunkim/Desktop/unity-native-plugin/Assets/NativeToolkitIosUiHarness/NativeSource~/NtkUiHarnessOverlay.mm:147)のプローブは監視停止を調べる別リスナーで、[同:175](/Users/jonghyunkim/Desktop/unity-native-plugin/Assets/NativeToolkitIosUiHarness/NativeSource~/NtkUiHarnessOverlay.mm:175)が返す値は受信イベント数です。登録は[Registry.swift:363](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Common/Registry.swift:363)で保持され、[同:295](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Common/Registry.swift:295)の解除処理で解放されます。
- **失敗シナリオ／5章での検出**：Managerの`listener_remove`を省略して登録を残しても、`stop_observing_async`が成功すれば受信数は0になり、[CB-5:125](/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/ClipboardUITests.swift:125)は通ります。V-3・V-4では解除漏れを判定できません。
- **修正**：Manager所有リスナーのrelease通知など、解除そのものを観測する検証を追加してください。

判定：A1が2件、A2が1件、中心契約の検証不足Bが2件あり、設計の修正が必要です。

注記：上記は Codex の出力をそのまま転記したものです。私自身は一次資料での再検証をしていません。

## 3. 判断

**サブエージェントの R-1、Codex の R2-1・R2-2 が A1 なので止めない。** v2 で A1 と中心の約束の B をすべて直し、2 回目は差分のレビューにする。安く直せる A2・C も入れる。

| 指摘 | 判断 | v2 での直し |
|---|---|---|
| サブの R-1（U+0000） | 直す（A1） | D-8 に足す。名前と型（scope の名前、`ut_type`、`matchingTypes`、representation の型）に U+0000 があれば、C# で `CLIPBOARD_INVALID_REQUEST`「The request is invalid.」にする。値（文字列、`texts`、HTML、URL、道）は Share と同じく警告を出して渡す（切れる）。読み取りの U+FFFD とあわせて 3 章の既知の差分に |
| Codex の R2-1（入口の拒みで印が早く外れる） | 直す（A1） | D-4 に足す。入口の拒みと C# で決めた失敗は、印と要求の枠を持ったまま次の `Update` に積み、配る時に外す（1.x は完了が次のフレーム以降だった）。Editor の PlayMode で「同じフレームの 2 回目は BUSY」を確かめる |
| Codex の R2-2、サブの R-5（停止の後のイベント） | 直す（A1） | D-6 に足す。C# が自分の監視の状態を持ち、start の成功で立て、stop の結果・start の失敗・破棄で下ろす。下りている間に届いた変化は捨てる（解放だけ）。リスナーは start より先に登録し、登録が拒まれれば StartObserving はその失敗を出す |
| サブの R-2、Codex の R2-3（Remove の General） | 直す | D-8 に「RemovePasteboard だけは General も `kind = GENERAL` の構造体」 |
| サブの R-3（模様のビット） | 直す | 表引き。表に無い値は C# で `CLIPBOARD_INVALID_REQUEST`。EditMode で範囲外を確かめる |
| サブの R-4（CancelLoads の BUSY） | 直す | D-5 に、印を次の `Update` の配送で外す（1.x と同じく、同じフレームの 2 回目は BUSY） |
| サブの R-6（`LogError`） | 直す（B） | 入口の拒みは `LogError` にしない。C# の組み立ての誤りでしか起きない値（`struct_size`・`reserved`・範囲外の kind。C# からは起きない）でも `LogWarning` にとどめる。拒まれた StartObserving・StopObserving は `HandleObservationControlCallback` と同じ後片付けを通す |
| サブの R-7（結果の型への写し方） | 直す（B） | D-9 に操作ごとの写し方の表 |
| サブの R-8（コールバックの中の例外） | 直す（B） | D-9 に: 読み取りが投げたら `CLIPBOARD_UNKNOWN`、`NONE` で NULL のハンドルは UNKNOWN（ReadData は除く）、ハンドルは捨てる経路も含めて `finally` で解放 |
| サブの R-9（64 MiB） | 直す（B） | D-9 に、`ntk_bytes_size` が 64 MiB を超えたら 1.x と同じ `CLIPBOARD_CONTENT_TOO_LARGE` と文言 |
| サブの R-10、Codex の R2-4（テストで捕まらない） | 直す（B） | 値を取る部分と結果を作る純粋な部分を分け、後者を EditMode で照らす。CP に `Scope`・`UtType`・`Has*` を足し、CP-31 に順の照らし |
| Codex の R2-5（CB-5 で外し忘れが見えない） | 直す（B） | リスナーの `release` に関数を渡し、外したら呼ばれる印を Player のテスト（破棄の後に印が立つ）で照らす。CB-5 は監視が止まった確かめとして残す |
| サブの R-11（出典） | 直す | F-1 は 215-285、F-7 の出典を足す、CL の表は part2 8.3 |
| サブの R-12（コールバックの数え方） | 直す | 型ごとの表と使う操作 |
| サブの R-13（破棄の細部） | 直す | 各呼び出しを別々の `try`、外した後すぐハンドルを 0 |
| サブの R-14（期限） | 直す | `ToUniversalTime()` の後に整数の秒（切り捨て）。1.x と同じ |
| サブの R-15（4 章の行） | 直す | `IosPlayerBuildSetup`、ハーネスのビルド、`notMovedYet`、`common.md`、クラスの doc |
