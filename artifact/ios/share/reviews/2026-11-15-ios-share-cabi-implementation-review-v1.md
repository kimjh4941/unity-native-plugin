# iOS Share の C ABI 2.0.0 への移行の実装のレビュー v1

- 記録日: 2026-11-15
- 対象: `cc8be3a`。設計 [../designs/2026-11-15-ios-share-cabi-design-v3.md](../designs/2026-11-15-ios-share-cabi-design-v3.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`。バックグラウンドのジョブで走り、結果を別の呼び出しで取った）の 2 本を並行で回した（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した（見出しの段だけこの文書に合わせて下げた）。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

### 1.1 結論
- 実の不具合（A）は見つかりませんでした。
- 次の点は設計（D-1〜D-13）と一次ソースのとおりで、問題ありません。
  - P/Invoke の署名と構造体: `Share.h` と一致（24 / 56 バイト、`size_t` → `UIntPtr`、コールバックに要求の ID が無い）。
  - 項目の配列と文字列の確保・解放: `finally` の `FreeAll` で、例外の経路も含めて漏れません。
  - 結果の handle: `finally` で 1 回だけ解放します。
  - 番号の表: 呼ぶ前に入れ、拒み・例外で取り出し、`OnDestroy` で空にし、番号は戻しません。
  - IL2CPP: `MonoPInvokeCallback` と、static readonly の delegate から作った関数ポインター。
  - 1.x の補正の再現: null・未知の type・null 値の項目を捨てる。除く一覧に null があれば一覧ごと捨てる。subject / previewTitle は `IsNullOrWhiteSpace` で省く。1.x の Builder と `UnityIosShareJsonParser.swift` と同じです。
- 指摘は B（文書と記録の食い違い、テストの抜け）と C（小さな改善）だけです。

#### R-1 [B] 観点 1・3: 「呼び出しごとに結果が 1 回」という記述が SH-13 の実測と食い違ったまま
- 根拠
  - `Runtime/iOS/Share/IosShareManager.cs:22-23` の XML remarks: "Each call gets its own result, also when two are made in a row (1.x kept one slot and lost one of them)"
  - `IosSharePendingCalls.cs:11-12` も同じ書き方です。
  - `agent-rules/coding-rules/common.md:133` の「（1.x の 1 つの枠の後勝ちで結果が失われていた）」は、直ったように読めます。
  - 設計 v3 の 3 章 SH-13 の行（「呼び出しごとにちょうど 1 回」）と F-9 も直っていません。
- 壊れ方
  - 公開 API の文書が、実際には起きる結果の欠落（同じフレームで 2 回呼ぶと 1 つは結果が来ない）を「起きない」と書いています。
  - 利用者は、2 つ目の受け手を待ち続けるコードを書きえます。
- 直し案
  - XML と `common.md` を次の 2 点に書き直す。
    - 届いた結果は、その呼び出しの受け手に届く（1.x の取り違えは直った）。
    - 表示中のシートに重ねた共有は、native-toolkit 2.0.0 では完了が来ないことがある。
  - 設計（F-9、3 章 SH-13、D-1 の理由）と 1.x との差分の表も、実測に合わせて直す。
  - 1.x との差分は「失われる 1 本が戻る」ではなく、「出た結果は正しい受け手に届く。1 本は来ないまま」です。

#### R-2 [B] 観点 1・4: SH-13 の期待が設計 4 章と違い、テストが指す記録がまだ無い
- 根拠
  - `Tools~/IosUiTests/NtkUiTests/ShareUITests.swift:142-158` は `calls.count == 1`、イベント 1 本を期待しています。
  - 設計 4 章は「`share1`・`share2` が 1 本ずつ、`share-event` が 2 本」です。
  - コメントが指す "recorded in the Share C ABI implementation result" は、`artifact/ios/share/results/` にまだありません。
  - 設計 4 章の「実装の記録とトピックの README の段 3 の行」も未着手です。
- 壊れ方
  - 期待を欠陥に合わせた理由と、実測の行が辿れません。
  - 行がテストの出力に出るだけで、どちらの呼び出し（share1 か share2）に届いたかの記録が残りません。
- 直し案
  - 実装の記録に次を書く。
    - SH-13 の実測（どちらの id に届いたか、30 秒待ったこと）
    - 下の「SH-13 の原因の見立て」
    - native-toolkit への申し送り
  - 設計を v4 にするか、記録の中で 4 章からの外れとして書く。

#### R-3 [B] 観点 4: `Share()` が出す警告（D-2・D-3・D-5）を確かめるテストが無い
- 根拠
  - `IosShareManager.cs:126-127` の `foreach … Debug.LogWarning` を確かめるテストがありません。
  - Editor では整える前に `Unavailable` で返るので、この行に届きません（`IosShareManagerEditorTests.cs:45-52`）。
  - Player の SH-23〜25（`IosSharePlayerTests.cs:88-100`）は警告を照らしていません。
  - `IosShareCApiTests.cs:75` は "left out 3 item(s)" を含むことだけを見ています。D-2・D-5 の「値や道は出さない」を照らしていません。
- 壊れ方
  - 警告のループを消しても、警告に値（"x" など）が混ざっても、どのテストも落ちません。
  - 3 章の「警告のログが増える」は確かめられていません。
- 直し案
  - Player の SH-23〜25 に `LogAssert.Expect(LogType.Warning, …)` を足す。
  - `IosShareCApiTests` で次を照らす。
    - 警告の文に値 `"x"` や道が含まれない（`StringAssert.DoesNotContain`）。
    - 除く一覧の警告の文。
    - U+0000 の警告が値を含まない。

#### R-4 [B] 観点 4: `OnDestroy` で表を空にすることを確かめていない
- 根拠
  - `IosShareManagerEditorTests.cs:116-117` は `s_pending.Clear()` を直接呼んで破棄の代わりにしています。
  - `IosShareManager.cs:97` を消しても通ります。
  - D-1 の「`Instance` で作り直さない」も照らしていません。
- 壊れ方
  - 破棄の後に来た完了が、破棄の前の受け手に届く退行を捕まえられません。
- 直し案
  - Manager を `Object.Destroy` して 1 フレーム待つ。
  - その後 `s_pending.Count == 0` と、`Complete` の後に IosShareManager の GameObject が増えないことを照らす。
  - 後片付けで `Instance` を作り直す。

#### R-5 [B] 観点 5・1: `testing.md` の層 1 の表の iOS Share の欄が古いまま
- 根拠
  - `agent-rules/coding-rules/testing.md:407` の iOS の欄が「実装済み（builder / dispatch）」のままです。
  - Dialog の行（409）は `IosDialogCApiTests` に更新済みです。
  - 設計 4 章は「ガードの表と層 1・2a の表の Share の iOS」を直すとしています。今回はガードの表（224）だけを直しています。
- 壊れ方: 層 1 の現状の表が、`IosShareCApiTests`（構造体の位置、整え、組み立て、文言、表）を含みません。
- 直し案
  - 407 の iOS の欄に「Bridge の変換（構造体の大きさと全項目の位置、1.x と同じ整え、組み立て、文言、呼び出しの表）。`IosShareCApiTests`。2026-11-15」を足す。
  - 2a（378）に `IosShareManagerEditorTests` を足す。

#### R-6 [C] 観点 4: 結合テストのコメントが 1.x の実装を指している
- 根拠: `Tests/PlayMode/IosShareManagerIntegrationTests.cs:76-81`（"Last-registered-wins (s_onShare overwritten …) … native shareContent() call …"）
- 壊れ方: 今の実装に `s_onShare` も `shareContent` もありません。読み手を誤らせます。
- 直し案: 「Editor では次の Update に Unavailable。実機の各呼び出しの受け手は `IosShareManagerEditorTests` と Player のテストで確かめる」に直す。

#### R-7 [C] 観点 4: SH-13 の 2 つ目のシートの確かめで、失敗の文が意図どおりに出ない
- 根拠: `ShareUITests.swift:149-151` の `XCTAssertTrue(SystemUI.closeShareSheet(app), "a second share sheet appeared - update the expectation")`
- 壊れ方
  - 2 つ目のシートが出て閉じられた場合、この assert は通ります。
  - 落ちるのは次の `calls.count == 1` で、文は行の一覧だけです。
  - native-toolkit が直ったときに「期待を直せ」という意図が伝わりません。
- 直し案: `if sheet(timeout: 3) { XCTFail("a second share sheet appeared - update the expectation"); _ = SystemUI.closeShareSheet(app) }` にする。

#### R-8 [C] 観点 2: 拒みの後で表に番号が無いと、イベントも出ない
- 根拠: `IosShareManager.cs:177`（`error != ErrorNone && s_pending.TryTake(...)`）
- 壊れ方
  - 起こるのは、別スレッドからの `Share` と主スレッドの `OnDestroy` が重なったときだけです（実害は極小）。
  - その場合は `ShareCompleted` も出ません。
  - `Complete` は、表に無い番号でもイベントを出す作りです。両者の扱いが違います。
- 直し案: 拒みでは `TryTake` の結果にかかわらず `FireResult(…, rejected)` を呼ぶ（無ければ受け手は null）。

#### R-9 [C] 観点 1: コードとテストのコメントが「design v2」を指している
- 根拠
  - `IosShareCApi.cs:14, 44`、`IosShareManager.cs:20, 61`、`IosSharePendingCalls.cs:11`
  - `IosShareCApiTests.cs:17`、`IosShareManagerEditorTests.cs:17`、`ShareUITests.swift:11`
- 壊れ方: v2 から変わった点（D-1 の番号を戻さない、D-13 の完了の配り）を、v2 で調べることになります。
- 直し案: v3 に直す（ファイル名まで書いてある `ShareUITests.swift:11` を含む）。

#### R-10 [C] 観点 5: `testing.md` のガードの行の「項目が空・null」があいまい
- 根拠: `testing.md:224`
- 壊れ方
  - 実装では、`items = [null]` のような「null の要素だけの配列」も Editor では Unavailable になります。
  - 「項目が null」がこれを含むと読めます。
- 直し案: 「payload が null、`items` が null か空のときは `No shareable items were provided.`。それ以外は、要素が null や未知の type でも `iOS share is only available on an iOS device.`」と書く。

#### R-11 [C] 観点 5: `common.md` の Share の行に、D-1 の細かい決まりが 2 つ抜けている
- 根拠: `common.md:133`
- 壊れ方
  - 次の 2 つが書かれていません。後の機能（Clipboard、Notification）で同じ型を写すときに落ちえます。
    - 番号を戻さない（戻すと、破棄の前の遅れた完了が別の呼び出しに届く）
    - 表に無い番号の完了は、生きている Manager のイベントだけに出す
- 直し案: この 2 つを足す。R-1 の「来ないことがある」もここに書く。

### 1.2 SH-13 で 1 つの呼び出しの完了が来ない原因の見立て

#### この包みの誤りではない（根拠は強い）
- `ShareCompleted` のイベントは、番号が表にあるかどうかに関係なく、完了ごとに必ず出ます（`IosShareManager.cs:193-197`、`FireResult:234-237`）。
- ハーネスは起動時にイベントを 1 回だけ購読しています（`ShareScenario.cs:30`）。
- 実測ではイベントが 1 本だけでした。つまり、ネイティブのコールバック（`OnResult`）は 1 回しか呼ばれていません。
- この包みの側で行が 1 本も出ない経路は、次のどちらかだけです。
  1. 1 つ目の `Share()` が `Begin` の前に投げて、2 つ目が呼ばれない。
  2. `OnResult` か `Complete` が投げて `"share completion error"` になる。
- コードを読む限り、どちらの経路もこの payload では起きません。
- 拒み・例外・使えないときは、どれも行が出ます。
- 念のための確認（推奨）: Player のログで次を数える。
  - `[IosShareManager][Share] itemCount: 1` が 2 回出ていること。
  - `LogError` が出ていないこと。
  - native の `subsystem == "com.jonghyunkim.nativetoolkit.IosLibraryCApi"`（category `Share`/`Common`）の `[share] request: provided` と `[start] id:` が 2 回出ていること。

#### native-toolkit 側の原因の候補（推測）
- 前提
  - 2 つ目の要求が受け付けられた後、完了が来ない経路は「`ShareSheetPresenter.present` の continuation が再開されない」だけです。
  - `Registry.complete` と `ShareReceiver.finish` は、箱が閉じれば必ず 1 回呼びます。
  - 箱を閉じるものは 3 つです: `completionWithItemsHandler`、手順の失敗、`ReleaseWatch.deinit`。
  - したがって、「シート 2 が解放されない」か「手順が途中で止まる」のどちらかです。
- (a) シート 2 が UIKit に保持されたまま、表示も解放もされない（いちばん有力）
  - MS-03（native の stage1b 作業記録 7.7）に次の実測があります。
    - `UIActivityViewController` の `presentingViewController` は、`present` から 0.08〜0.9 秒後に初めて入る。表示が遅れて始まる。
  - そのため、同じフレームの 2 つ目の `.foreground` の手順は、次のように進むと見ます。
    1. ルートがまだ何も出していないように見え、`isSettled(root)` が true になる。
    2. 待たずにルートへ `present(sheet2)` する。
    3. 遅れて始まる 2 つの表示が競合し、2 つ目が断られる。
  - SR-16 の「断られたシートは約 0.04 秒で解放」は、すでに出し終えた VC へ出した場合の実測です。遅れて始まる表示同士が競合する場合は測られていません（F-9 も「測られていない」）。
  - この場合に UIKit か `UIActivityViewController` の読み込みがシート 2 を持ち続けると、`ReleaseWatch` が働かず、完了が来ません。
  - 1.x の記録（1.13.0 でも、もう 1 つの呼び出しはイベントも来なかった。「UIKit に黙って断られた」という推定）と同じ UIKit の振る舞いに見えます。2.0.0 の見張りでも救えていない、という読みと合います。
- (b) 手順の待ちで止まる
  - 2 つ目の手順がシート 1 を最上位と見て `waitForTransition` に入る場合があります（`PresentationProcedure.swift:212`、`283-294`）。
  - その coordinator の completion が呼ばれないと、`again` も表示も起きません。
  - この経路では見張りがまだ作られていないので、何も箱を閉じません。待ちに時間の上限はありません。
- (a) と (b) の見分け方: native の `.debug` のログ（`log stream --level debug --predicate 'subsystem BEGINSWITH "com.jonghyunkim.nativetoolkit"'`）で、2 つ目の要求の流れを見る。
  - `[UIKit.waitForTransition]` の後に `[attempt] retried: true` が来なければ (b)。
  - `[show]` と `ReleaseWatch [init]` の後に `[deinit]` が来なければ (a)。
  - あわせて、UIKit の "Attempt to present … which is already presenting" の行と、Xcode のメモリーグラフでシート 2 を持っているものを見る。
- native-toolkit の単体テスト `testTwoSharesCompletedInTheOtherOrder`（`IosLibraryCApiTests/ShareTests.swift:392-422`）は、偽の `sharing` を使っています。本物の UIKit で 2 つの共有が重なる場合は確かめていません。
- `Share.h` が約束の外にしているのは「ホストが window / root を替えた」「システムが scene を切った」場合だけです。この場面は約束の中なので、native-toolkit の不具合として申し送るのが妥当です。
- native 側の直し方の候補
  - 表示が保留中・表示中のときは 2 つ目を待ち行列に入れる。
  - または `SHOW_FAILED` で早く返す。
  - または、待ちと遅れた表示に時間の上限を付ける。

#### この包みへの影響と当面の扱い
- 来ない呼び出しの受け手は、`s_pending` に残り続けます。D-1 の「既知」の範囲ですが、ホストの窓の差し替えだけでなく、普通の連続呼び出しで起こり、起きるたびに 1 つずつ増えます。
- C# で 2 つ目を待たせる回避は、1.x にも無い振る舞い（シートが続けて出る）になるので勧めません。R-1 の文書化と、native-toolkit への申し送りを先にするのがよいと考えます。

件数: A 0 件、B 5 件、C 6 件

## 2. Codex

`git show cc8be3a` と指定された一次ソースを確認しました。C# 本体の署名・構造体配置・所有権・IL2CPP 対応・1.x 互換性には、具体的な不具合を確認できませんでした。以下は native 側の問題1件と、テストの検証不足3件です。ファイル変更・テスト実行は行っていません。

**R2-1 — A：同一フレームの二重 Share で、native 側が完了を保証できない**

証拠：C ABI は [ShareReceiver.swift:49](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Share/ShareReceiver.swift:49) で各要求を受け付け、同ファイル16行で `.foreground` を選びます。[ForegroundLocator.swift:72](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Common/Presentation/ForegroundLocator.swift:72) は表示中の controller をたどりますが、共有要求同士を直列化しません。[PresentationProcedure.swift:212](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Common/Presentation/PresentationProcedure.swift:212) は遷移の完了を待ち、[ShareSheetPresenter.swift:94](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Share/Presentation/ShareSheetPresenter.swift:94) は表示拒否の検出を `.releaseWatchOnly` に限定しています。その代替完了は [ReleaseWatch.swift:42](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Common/Presentation/ReleaseWatch.swift:42) の `deinit` 頼みです。[Registry.swift:194](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Common/Registry.swift:194) の `isCompleted` は重複通知を防ぐだけで、来ない完了を生成しません。

失敗場面：提示された SH-13 の実測では、一方が閉じられても他方の完了が30秒後にも届きません。これは [Share.h:30](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Share.h:30) の契約に反します。未完了の C# コールバックも表に残ります。

原因層は **native-toolkit と判断します**。ラッパーは [IosShareManager.cs:163](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Share/IosShareManager.cs:163) で呼ぶ前に個別登録し、216行で返された番号を `ToInt64()` で取り出すため、旧版の共有枠の上書きでは説明できません。ただし、今回止まった場所が「遷移待ち」か「シートの解放待ち」か、UIKit 内部の保持者が誰かは未確定です。

修正：native 側で表示開始中・表示中の要求を管理し、競合要求を `SHOW_FAILED` で完了させるか直列化する。表示拒否の終了を controller の解放だけに依存させない。

**R2-2 — B：SH-13 が契約違反を成功条件にしている**

証拠：[ShareUITests.swift:154](/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/ShareUITests.swift:154) は呼び出し結果1本、156行はイベント1本を要求します。一方、[設計書:80](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/designs/2026-11-15-ios-share-cabi-design-v3.md:80) は各IDに1本、イベント2本を要求しています。

失敗場面：現在の通知欠落で成功し、native 側が正しく2件完了すると失敗します。また、どちらのIDでもよいため、一方の結果を誤った受け手に届ける破損も検出できません。

修正：各ID1本・イベント2本と許容結果を検証する。既知不具合は、その契約の検証に限定した `XCTExpectFailure` 等で明示する。

**R2-3 — B：破棄時の表のクリアをテストしていない**

証拠：[IosShareManagerEditorTests.cs:116](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosShareManagerEditorTests.cs:116) は直接登録し、117行でテスト自身が `s_pending.Clear()` を呼びます。実際の [IosShareManager.cs:97](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Share/IosShareManager.cs:97) は通りません。

失敗場面：`OnDestroy` から `Clear()` が消えても成功します。破棄・再生成後に旧コールバックへ通知する破損を検出できません。

修正：保留要求を作り、実際に Manager を破棄して表が空になることを確認する。再生成後の要求と旧要求の遅延完了も照合する。

**R2-4 — B：ネイティブ呼び出しが戻る前の完了をテストしていない**

証拠：[IosShareManagerEditorTests.cs:90](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosShareManagerEditorTests.cs:90) の偽呼び出しは番号を保存して戻るだけで、完了は `Begin` 後の102行以降に発生させています。[Share.h:28](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Share.h:28) は、別スレッドから呼んだ場合に戻る前の完了を許しています。

失敗場面：登録をネイティブ呼び出し後へ移す破損が入っても、このテストでは検出できません。早い完了では受け手が見つからず、その後に登録だけが残ります。現在の実装順は正しいため、検証不足の指摘です。

修正：偽呼び出し内から `Complete(userData.ToInt64(), ...)` を実行し、受け手への通知が次の `Update` に1回届き、表に残件がないことを確認する。

## 3. 判断

この包みの側の実の不具合は、2 人とも 0。Codex の R2-1（A）は native-toolkit の側の不具合で、この包みでは直さない（利用者の判断で native-toolkit に申し送る。記録に書く）。

| 指摘 | 判断 | 直し |
|---|---|---|
| サブの R-1（文書と実測） | 直す | XML、`IosSharePendingCalls` のコメント、`common.md` を「届いた結果は正しい受け手に届く。表示中のシートに重ねた共有は完了が来ないことがある（native-toolkit 2.0.0）」に。設計 v3 との外れは実装の記録に書く |
| サブの R-2・R-7、Codex の R2-2（SH-13 の期待） | 直す | 約束（各 id 1 本、イベント 2 本）を照らし、その部分だけを `XCTExpectFailure`（既知の不具合、strict）で包む。直れば「期待した失敗が起きない」で落ちて気づける。2 つ目のシートが出たら `XCTFail`。届いた行の値は閉じた成功であることを照らす |
| サブの R-3（警告） | 直す | Player の SH-23〜25 に警告の `LogAssert.Expect`。EditMode で警告に値が含まれないこと、除く一覧と U+0000 の警告の文 |
| サブの R-4、Codex の R2-3（破棄） | 直す | Editor の PlayMode で、受け付けた呼び出しを作り、Manager を本当に破棄して番号が表から消えること、遅れた完了が受け手に出ず Manager も作られないこと |
| Codex の R2-4（戻る前の完了） | 直す | 偽の呼び出しの中で `Complete` を呼び、次の `Update` に 1 回、表に残らないこと |
| サブの R-5・R-10・R-11（文書） | 直す | `testing.md` の層 1・2a・ガードの行、`common.md` の Share の行 |
| サブの R-6・R-9（コメント） | 直す | 結合テストのコメント、design v2 → v3 |
| サブの R-8（拒みで表に無いとき） | 直す | 拒みでは `TryTake` の結果にかかわらず結果を出す |
