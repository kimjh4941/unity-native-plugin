# iOS Share の C ABI への写し方の設計 v1 のレビュー v1

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-share-cabi-design-v1.md](../designs/2026-11-15-ios-share-cabi-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

対象は `/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md` です。以下、行番号はこの設計書の行です。ファイルは編集していません。1.x の文言を確かめるため、`93e6b8f` の 1.x の IosLibrary のバイナリを scratchpad（`/private/tmp/claude-501/-Users-jonghyunkim-Desktop-unity-native-plugin/e79ff5e3-40bb-4aaf-b68b-47a27ab860c6/scratchpad/iosl13/IosLibrary`）に取り出して `strings` で読みました。

先に答え: **A1 は 0 件です。B が 5 件、A2 が 6 件、C が 3 件あります。** 1 章の事実と出典は、構造体の 24/56 とエラーの値を含めてほぼヘッダーと実装に合っていました。直すべき中心は次の 3 点です。
- D-8 が「1.x の文言」として引いている出典が 1.x のものではない（R-1）。
- D-2・D-3 の C# で捨てる判断が、native-toolkit の設計 P2-15 と食い違っているのに触れていない（R-2）。
- Manager の破棄と例外のときの約束が書かれていない（R-4・R-5）。

---

### R-1 [B] 観点 2: D-8 の「1.x の library の文」は 1.x ではなく、文言の既知の差分が 3 章に足りない
- 根拠
  - D-8（40 行）は「文の形は 1.x の library（`ShareError.swift:57-75`）に合わせ」と書いています。しかしこの行番号は今の IosLibrary（2.0.0 側）のファイルのものです。
  - 1.x（native-toolkit のタグ `1.13.0`）の `ios/IosLibrary/IosLibrary/Share/Domain/Model/ShareError.swift:44-45` は `presentationFailed(Error)` → `"Failed to present the share sheet: \(error.localizedDescription)."` です。`activityFailed` と `dismissedBySystem` の case はありません。
  - 1.x では活動の失敗も `presentationFailed(error)` になっていました（`1.13.0:.../ShareSheetPresenter.swift:81-84`）。
  - 1.x では、表示中の画面が落ち着いていないと、待たずにすぐ失敗していました（同 52-55）。文言は `...: The root view controller is not in a stable state to present the share sheet..` です。
  - `93e6b8f` の `ios-native-toolkit-1.3.0.xcframework` の IosLibrary のバイナリを `strings` で読むと、`Failed to present the share sheet: ` と `...not in a stable state...` があります。`The share activity failed.` はありません。
  - UI テスト計画 v3 の 11 行目も、1.x の出典を「タグ 1.13.0 の ShareError.swift」としています。
- 壊れ方
  - `ACTIVITY_FAILED` の文言が、1.x の `Failed to present the share sheet: <OS の文>.` から `The share activity failed.` に変わります。`SHOW_FAILED` の文言は、`: <理由>` が落ちて `Failed to present the share sheet.` になります。
  - 落ち着いていない画面の上に出すときの振る舞いも変わります。1.x は待たずに失敗しました。2.0.0 は `.foreground` で 1 回待ち、出すか `SHOW_FAILED` になります。
  - どちらも 3 章（46-60 行）に行がありません。このままでは移行ガイドに載りません。`StartsWith("Failed to present the share sheet:")` で照合している利用者のコードは、黙って合わなくなります。
  - 5 章にこれを確かめる項目はありません。A1 の 3 条件にほぼ当たりますが、文言だけの差分で、ふだんの経路では起きにくいので B にしました。
- 直し案
  - D-8 の出典を `1.13.0` に直します。
  - 3 章に次の 3 行を足します。「活動の失敗（1.x: `Failed to present the share sheet: <OS の文>.`）」「表示が断られた・落ち着かない（1.x: 待たずにすぐ失敗、`...: The root view controller is not in a stable state ...`）」「遷移中の画面の上に出す（Dialog v2 の 3 章と同じ形）」。
  - 文言を 1.x に寄せるなら、`SHOW_FAILED`・`ACTIVITY_FAILED` も `Failed to present the share sheet: <理由>.` の形にする案もあります。どちらにするかは決定として書きます。

### R-2 [B] 観点 1・2: D-2・D-3 は P2-15 と食い違うのに触れておらず、D-3 は 1.x とも P2-15 とも違う第 3 の振る舞いになっている
- 根拠
  - native-toolkit の設計 part2 の P2-15（42 行）は「C ABI は補正も代わりの値も行わず、エラーで返す。**Unity の包みも補正を持たない**」としています。同じく 410 行と 578 行の SH-01 は「不正な項目を捨てず `INVALID_PARAMETER`」です。
  - 設計書は 9 行目で P2-15 を出典に挙げていますが、D-2（34 行）は C# で捨てます。
  - 一方で `agent-rules/coding-rules/common.md:101`（J-N5）には「1.x の補正のうち、JSON の段で決まるものを保つ」という前例があります。1.x のパーサーでの捨て方（`UnityIosShareJsonParser.swift:36-51`）は、まさに JSON の段の補正です。
  - D-3（35 行）は null だけを捨てます。1.x は一覧をまるごと捨てていました（`as? [String] ?? []`、同 51 行）。P2-15 なら失敗です。
- 壊れ方
  - 上流の決定（P2-15）と食い違う理由が書かれていません。次の機能（Clipboard・Notification）の設計で、同じ判断がまた割れます。
  - D-3 は、1.x の互換（D-2 の理由）も P2-15 も理由にできない独自の補正です。「利用者の意図に近い」は推測です。
- 直し案
  - D-2 に「P2-15 の例外として J-N5 と同じく 1.x の JSON の段の補正を保つ」と書きます。
  - D-3 は次の 3 案から選び、利用者の判断として書きます。(a) 1.x と同じく、null が 1 つでもあれば一覧をまるごと捨てる（SH-17 は「同じ」になる）。(b) P2-15 どおり C# で失敗にする。(c) 今の案。
  - 3 章の SH-17 の行と UI テストの切り替えは、選んだ案に合わせます。

### R-3 [B] 観点 2・3: 「使えない」の判定と D-2 の整えのどちらが先かが決まっておらず、Editor の行の「同じ」が崩れうる
- 根拠
  - 1.x の C# は、null か空の確かめの後、Editor では `"iOS share is only available on an iOS device."` を返していました。Builder とパーサーは通りません（`93e6b8f` の `IosShareManager.cs:109-134`）。
  - 3 章の Editor の行（60 行）は「同じ（D-6）」としています。
  - Android の Share も、Editor では入力によらず同じ失敗にしています（`agent-rules/coding-rules/testing.md:229`）。
  - D-2・D-6 は順を書いていません。
- 壊れ方
  - 自然な順（整えてから使えないかを見る）で実装すると、Editor で `[null]`・未知の `type` だけ・値が null の payload を渡したとき、`No shareable items were provided.` と警告が出ます。1.x は `iOS share is only available...` でした。
  - `IosShareManagerIntegrationTests` には、この入力の項目がありません（文字列 1 つだけ）。5 章では捕まりません。
- 直し案
  - D-6 に順を書きます。「null か空（今の C# の確かめ）→ `IsAvailable` → D-2〜D-4 の整え → 組み立て」。
  - Editor の PlayMode に、未知の `type` だけの payload が `iOS share is only available...` になる項目を足します。

### R-4 [B] 観点 3・5: Manager の破棄、表に無い番号、`ShareCompleted` をいつ取り出すかが決まっていない
- 根拠
  - 1.x は `OnDestroy` で `s_onShare = null` にしていました。`ShareCompleted` は、完了の時点の `_instance?.ShareCompleted` を写して使っていました（`93e6b8f` の `IosShareManager.cs:86-93, 148-154`）。
  - Android の Share は「結果は、取り出した時点で生きている Manager に出し、いなければ捨てる（J-S4）」と決めています（`common.md:83`）。
  - Dialog は `Instance.Raise` で作り直して出します（`IosDialogManager.cs:318`）。
  - D-1（33 行）と 4 章（67 行）は、表を static にすることしか書いていません。
- 壊れ方
  - 破棄の後に来た完了を、受け手（1.x では消えていた）に出すのか、作り直した Manager に出すのか、捨てるのかが、実装した人によって変わります。
  - `Instance` で作り直すと、終了の途中で GameObject が作られえます（J-S4 が避けた形）。
  - 1.x のテストにも 5 章にも、この項目はありません。
  - F-8（24 行）の「ホストが持ち続けるとシートも完了も来ない」場合、表の項目が残り続けることも書かれていません。
- 直し案
  - D-1 か D-7 に次の 3 つを決めて書きます。
    - `OnDestroy` で表を空にするか（1.x と同じなら空にする）。
    - 表に無い番号の完了は、`ShareCompleted` だけを出すか捨てるか。
    - `ShareCompleted` は完了の受け口の時点で `_instance` から写す（1.x と同じ）。`Instance` は作らない。
  - F-8 の取り残しは「既知。ホストが持ち続ける間だけ」と書きます。

### R-5 [B] 観点 2・3: 例外の経路（1.x の `Internal error: …`）が無く、表に入れた後の例外で結果が 0 回になりうる
- 根拠
  - 1.x は、ネイティブの呼び出しの例外を捕まえて `FireResult(Failure($"Internal error: {ex.Message}"))` にしていました（`93e6b8f` の `IosShareManager.cs:123-131`）。
  - Dialog も同じ文言で捕まえています（`IosDialogManager.cs:276-281`）。
  - Android の J-S1（`common.md:80`）は「消すのは…`extern` に入れなかった例外のときだけ」です。
  - D-1 は「呼ぶ前に入れる」「拒まれたら取り出す」しか書いておらず、D-6 にも例外がありません。
- 壊れ方
  - 組み立て（`AllocHGlobal`、`StructureToPtr`）や `extern` で例外が出ると、表に項目が残ります。受け手も `ShareCompleted` も呼ばれず（「ちょうど 1 回」が崩れる）、例外が `Share` の呼び出し元に漏れます。
  - 起きるのはまれで、5 章では捕まりません。
- 直し案
  - D-6 に「例外は捕まえて表から取り出し、`LogError` と `Internal error: <ex.Message>` を次の `Update` に出す（1.x と同じ）」を足します。
  - 組み立ては表に入れる前に済ませ、入れた後は `extern` だけにするのが安全です。

### R-6 [A2] 観点 1: F-5 の「それ以外の scheme は `INVALID_URI`」は誤りで、`file:` の URL は受け付けられる
- 根拠
  - `native-toolkit/ios/IosLibrary/IosLibrary/Share/Data/Repository/ShareRepositoryImpl.swift:87-90` は `case "file": guard url.isFileURL` です。1.x の `1.13.0` も同じです。
- 壊れ方
  - 1 章の事実が誤っています。C# は確かめないので、実装への影響は小さいです。ただ、`file://` の URL の項目で D-8 の文言を期待するテストを書くと外れます。
- 直し案
  - F-5（21 行）を「http/https は host が要る。file は file URL なら受け付ける。それ以外は `INVALID_URI`」に直します。

### R-7 [A2] 観点 3・4: Share のコールバックには要求の ID が無いのに、その型と署名を照らすテストが 4 章に無い
- 根拠
  - `Share.h:73-74` の型は `void (*)(void* user_data, ntk_share_error, uint32_t system_code, ntk_share_result*)` です。Dialog（`IosDialogCApi.cs:51`）と違い、`uint64_t request_id` がありません。
  - Dialog には `IosCApiDeclarationTests.TheDialogCallbackHasTheHeadersSignature`（140-144 行）があります。4 章（68 行）の EditMode の項目は「名前と署名（+4）」だけで、コールバックの型が入っていません。
- 壊れ方
  - Dialog を写して `(IntPtr, ulong, int, uint, IntPtr)` にすると、arm64 では `error` が `system_code`（いつも 0）として読まれます。いつも `NONE` になり、`result` は不定のレジスタになるので、落ちるか誤った結果になります。
  - Player の追加テストは C# の受け口を直接呼ぶので捕まえません。UI テスト（V-3）で初めて捕まります。
- 直し案
  - D-9 か 4 章に、`ResultCallback` の署名 `Void(IntPtr, Int32, UInt32, IntPtr)` を照らす EditMode のテストを足します。

### R-8 [A2] 観点 4: SH-13 の 2.0.0 の期待で「2 つ目の値」を 2 つに絞っていて、どう記録するかも書いていない
- 根拠
  - 4 章（71 行）は「2 つ目の値（成功か `Failed to present the share sheet.`）を記録する」としています。
  - `ShareSheetPresenter.swift:36-42`（今のライブラリ）の表には、「見張りが、ハンドラー無しで解放されたシートを見つけた → `dismissedBySystem`」もあります。
  - V-4（83 行）では、SH-13 を実機でも流します。
- 壊れ方
  - 推測です。2 つのシートが重なったときに、XCUITest の `closeShareSheet` が下のシートの「閉じる」に当たると、上のシートはハンドラー無しで消えます。その場合 `The share sheet was dismissed without a result.` になり、期待の 2 値に入らず落ちます。
  - シミュレーターと実機で値が違うこともありえます。
  - 「記録する」の手段（失敗の文に出すのか、`XCTContext` の添付か）が決まっていません。
- 直し案
  - 次を期待として書きます。
    - `share1` と `share2` が 1 本ずつ、`share-event` が 2 本。
    - 各行の値は {閉じた成功, `Failed to present the share sheet.`, `The share sheet was dismissed without a result.`} のどれか。
    - 実際の値は、テストの出力（`harnessLines()` と同じ形）に必ず書く。
  - 決めた値は結果ファイルに書き、移行ガイドへ渡します。

### R-9 [A2] 観点 4: 「完了の受け口を 2 つの番号で順を逆に呼ぶ」テストに要る、表への入口が書かれていない
- 根拠
  - Player のテストは、システムの画面を出せません（`IosPlayerTestSupport.cs:11-14`）。そのため `Share` を呼んで表に入れることができません。
  - 4 章（70 行）は、受け口を直接呼ぶことしか書いていません。Dialog は `user_data` が種類なので、表が要りませんでした（`IosDialogPlayerTests.cs:201, 212`）。
  - 「拒まれたら取り出す」「呼ぶ前に入れる」を確かめる項目もありません。`struct_size` 0 のテストは、ライブラリの拒みしか見ません。
- 壊れ方
  - 実装のときに試験用の口を勝手に作るか、テストを諦めることになります。
  - D-1 の「拒みで取り出す」が抜けても（項目が残るだけで、結果は出るので）どのテストも落ちません。
- 直し案
  - 表を純粋な internal のクラスにし、EditMode で確かめます。確かめるのは「入れる → 取り出す」「拒みで取り出す」「無い番号は null」「1 から増える」です。
  - Player のテスト用に `internal` の登録の口と `s_resultCallbackPointer` を出すことを、4 章に書きます。

### R-10 [A2] 観点 2: 値の途中の U+0000 で切れることが既知の差分にない
- 根拠
  - 1.x の Builder は U+0000 を `\u0000` にしていました（`93e6b8f` の `IosShareJsonBuilder.cs:156-160`）。Swift の `String` には全文が届きます。
  - 2.0.0 は `IosNativeToolkitCApi.ToUtf8WithTerminator`（`IosNativeToolkitCApi.cs:64-70`）で NUL 終端の C 文字列になるので、そこで切れます。
  - Android は `common.md:71` に「途中の U+0000 で切れる」と書いています。
- 壊れ方
  - U+0000 を含む文字列・URL・道が、黙って途中で切れて共有されます。1.x では全文でした。
- 直し案
  - 3 章に既知の差分として 1 行足します（切る、断る、置き換えるのどれにするかも決めます）。

### R-11 [A2] 観点 5: `common.md` の iOS の節と D-6 が食い違うのに、4 章の文書の欄に `common.md` が無い
- 根拠
  - `agent-rules/coding-rules/common.md:132` は「`IsAvailable` が false（Editor、別の版）なら、呼び出しの中で失敗を返す」です（Dialog の決まり）。
  - D-6（38 行）は、1.x と同じく次の `Update` に出します。
  - 4 章の文書（72 行）は `testing.md` だけです。
- 壊れ方
  - `common.md` に従う実装やレビューが、Share でも呼び出しの中で出す形にしてしまいます。そうすると 1.x の時点（次の `Update`）が崩れます（今の Editor のテストは `yield return null` の後に見るので、捕まりません）。
- 直し案
  - 4 章に `common.md` の iOS の節を足します。中身は「Share は呼び出しごとの表（D-1）を持ち、すべての結果を次の `Update` に出す（Dialog と違う）」です。

### R-12 [C] 観点 3: 実装の細部を明記したい
- 根拠: 33-42 行。Dialog v2 の 4 章は C# の型まで書いていました。
- 直し案
  - C# の型を書きます。`ShareItem` は `uint, uint, int, uint, IntPtr`、`ShareRequest` は `uint, uint, IntPtr, UIntPtr, IntPtr, IntPtr, IntPtr, UIntPtr` です。
  - `ntk_share_content_async` は `(ref ShareRequest, IntPtr, IntPtr, IntPtr)` です。
  - 表の lock の中では辞書の読み書きだけにし、`extern` は lock の外で呼びます（Android の J-S5 と同じ）。
  - D-2 の比べ方は大文字と小文字を区別する ordinal にします（1.x の Swift の `switch` と同じ）。D-4 は `string.IsNullOrWhiteSpace` と書きます（1.x の Builder と同じ）。
  - D-2 の警告には、捨てた件数と `type` だけを出し、値や道は出しません（`common.md` のログの決まり）。

### R-13 [C] 観点 1: 出典の細部
- 根拠と直し案
  - F-6 の「約 0.55 秒」の出典が表にありません。native-toolkit の `artifact/topics/ios-c-abi/results/2026-10-31-ios-c-abi-stage1b-result.md:56, 91` を足します。
  - F-8 の `Registry.swift:19-20` は「ちょうど 1 回」の根拠になっていません。`Share.h:30-31` と `Registry.swift` の `complete`（`isCompleted` の印）を挙げます。
  - F-4 に、`struct_size` が大きく、知らない部分が 0 でないときの `NOT_SUPPORTED`（`StructReader.swift:115-119`）が抜けています。C# は `Marshal.SizeOf` ちょうどを渡すので来ない、と一言添えます。
  - F-6 の「`UNKNOWN` は来ない」は、`ShareMapping.swift:98-100` の `@unknown default` があるので「通常は来ない」が正確です。

### R-14 [C] 観点 5: 5 章と 4 章に書いていない確かめと記録
- 根拠
  - Dialog v2 には V-6（サンプルの画面を実機で手で見る）と、実装の記録がありました。
  - Share のサンプル（`Runtime/UI/iOS/Share/IosShareManagerExampleController.cs:159-295`）は、`subject`（`ShareItemSource` で包む）と複数の画像・ファイルを使います。UI テストはこれらを通りません。
  - iPad の基点は、1.x も中央です（`1.13.0` の `ShareSheetPresenter.swift:70-75`）。3 章に「同じ（確かめない）」の行がありません。
- 直し案
  - V-6 としてサンプルの手の確かめ（利用者に頼む項目）を足します。
  - 3 章に iPad の行を「同じ、確かめない（計画 v3 の 4 章）」で足します。
  - 4 章に、実装結果ファイルと、トピックの README の段 3 の行を直すことを足します。

---

A1: 0 件 / A2: 6 件 / B: 5 件 / C: 3 件

## 2. Codex

1. **R2-1 — A1：内部NULで共有値の後半が失われる**

   **根拠：** [設計 D-5:37](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:37)は値をそのままUTF-8で渡します。共通部の[UTF-8変換:64](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Common/IosNativeToolkitCApi.cs:64)は内部NULもそのまま格納しますが、Swiftは[C文字列として読み:62](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Common/CApiString.swift:62)、[共有項目へ変換:68](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Share/ShareMapping.swift:68)します。対して旧Builderの `93e6b8f:Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Share/IosShareJsonBuilder.cs:141–159` はNULを `\u0000` にエスケープし、[旧Swiftパーサー:28](/Users/jonghyunkim/Desktop/native-toolkit/ios/UnityIosPlugin/UnityIosPlugin/Share/UnityIosShareJsonParser.swift:28)が復元します。

   **失敗シナリオ：** `Text("A\0B")` が、旧経路では `.text("A\0B")`、新経路では `.text("A")` になります。[検証対象:68](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:68)と[第5章:80](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:80)には内部NULのケースがなく、この欠落を検出できません。

   **修正：** 内部NULを明示的に拒否するか置換する方針を決め、既知差分として記録する。途中で黙って切れないことを検証する。

2. **R2-2 — B：Manager破棄後の保留呼び出しの契約がない**

   **根拠：** [D-1:33](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:33)は登録・完了・拒否時の表操作だけを規定しています。旧Managerの `93e6b8f:Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Share/IosShareManager.cs:86–93` は破棄時に受け手を解除します。一方、[Share.h:69](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Share.h:69)では完了が長期間保留され得ます。

   **失敗シナリオ：** 受付後、完了前にManagerを破棄・再生成すると、旧受け手への配送、表の解除、イベントの配送先が決まりません。これは実装済みの誤動作の断定ではなく、中心契約の欠落です。[予定の検証:68](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:68)にも破棄後の遅延完了がありません。

   **修正：** 破棄時の受け手解除と遅延完了の扱いを明記する。表から消えた番号でも、受領した結果は[所有権規則:66](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Share.h:66)に従って解放し、破棄→再生成→旧呼び出し完了を検証する。

3. **R2-3 — B：拒否テストがManager側の後処理を保証しない**

   **根拠：** [追加テスト:70](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:70)が要求するのは、不正な構造体に対する `INVALID_PARAMETER` と完了不発です。[D-1:33](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:33)の表からの除去と、[D-6:38](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:38)の拒否結果配送を検証しません。ネイティブの拒否処理は[EntryError.swift:20](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Common/EntryError.swift:20)で終了します。

   **失敗シナリオ：** Managerの拒否分岐から表の除去や結果配送を落としても、このテストは通ります。拒否後に完了は来ないため、登録した受け手は完了処理では回収されません。

   **修正：** Manager経由で拒否を注入し、表の件数が戻ること、次のUpdateでイベント→当該受け手が各1回呼ばれること、別の保留呼び出しが維持されることを確認する。

4. **R2-4 — C：F-5が `file:` URLを誤って無効としている**

   **根拠：** [F-5:21](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v1.md:21)はhttp/https以外を `INVALID_URI` としていますが、[ShareRepositoryImpl.swift:82](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Share/Data/Repository/ShareRepositoryImpl.swift:82)は `file` schemeも受け付けます。

   **失敗シナリオ：** `Url("file:///tmp/example.txt")` は、このURL検証では拒否されません。F-5から拒否を期待すると実装と食い違います。

   **修正：** 「http/httpsはhost必須、fileは `isFileURL` 必須、それ以外は無効」と訂正する。

補足：構造体の24／56バイトと各フィールド位置は、ヘッダーおよびネイティブのレイアウト検査と一致していました。呼び出し前の表への登録と `release = NULL` の方針には、一次資料との矛盾は見つかっていません。ファイルは変更していません。

## 3. 判断

**Codex の R2-1 が A1 なので止めない。** v2 で A1 と中心の約束の B を直し、2 回目は差分のレビューにする。安く直せる A2・C も入れる。

| 指摘 | 判断 | v2 での直し |
|---|---|---|
| Codex の R2-1、サブの R-10（途中の U+0000） | 直す（A1） | Android と同じく切れる形を既知の差分として 3 章に書き、C# で U+0000 を含む値を見つけたら `LogWarning` を出す（黙って切らない）。EditMode で、警告の判定を確かめる |
| サブの R-1（1.x の文言の出典） | 直す（B） | 出典を `1.13.0` に。`ACTIVITY_FAILED` は 1.x の前置きを保ち `Failed to present the share sheet: the share activity failed.`。3 章に「活動の失敗」「表示が断られた・落ち着かない」の行 |
| サブの R-2（P2-15） | 直す（B） | D-2 に J-N5 の前例を書く。D-3 は (a) 1.x と同じく一覧をまるごと捨てる（警告つき）。SH-17 は「同じ」 |
| サブの R-3（順） | 直す（B） | D-6 に順: null か空 → `IsAvailable` → 整え → 組み立て。Editor の PlayMode に未知の `type` だけの項目を足す |
| サブの R-4、Codex の R2-2（破棄） | 直す（B） | `OnDestroy` で表を空にする（1.x と同じ）。表に無い番号の完了は、生きている Manager の `ShareCompleted` だけに出す（`Instance` は作らない）。`ShareCompleted` は完了の時点で `_instance` から写す |
| サブの R-5（例外） | 直す（B） | 組み立てを表に入れる前に済ませ、例外は表から取り出して `Internal error: <ex.Message>` を次の `Update` に |
| サブの R-9、Codex の R2-3（拒みの後処理のテスト） | 直す | 表を internal の純粋なクラスにし、Manager の流れ（入れる → 呼ぶ → 拒み・例外で取り出す → 出す）を、ネイティブの呼び出しを引数で受ける internal の関数にする。Editor の PlayMode で偽の呼び出しを渡して確かめる |
| サブの R-6、Codex の R2-4（`file:`） | 直す | F-5 |
| サブの R-7（コールバックの署名） | 直す | EditMode のテストを足す |
| サブの R-8（SH-13 の値） | 直す | 3 つの値のどれか、実際の値をテストの出力に書く |
| サブの R-11（`common.md`） | 直す | 4 章に足す |
| サブの R-12〜R-14（細部） | 直す | 型、lock の外、ordinal、`IsNullOrWhiteSpace`、ログ、出典、V-6、iPad の行、README |
