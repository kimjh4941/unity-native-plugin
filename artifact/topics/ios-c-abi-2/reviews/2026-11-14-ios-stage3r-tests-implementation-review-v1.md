# 段 3-R の Share・Clipboard・Dialog のテストの実装のレビュー v1

- 記録日: 2026-11-14
- 対象: コミット `4457ae2`（Share の UI と Player のテスト、Clipboard と Dialog の Player のテスト、`verify_unity_ios.sh` の後片付け）
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント


ファイルは編集していません。1.x の Share の振る舞いは native-toolkit のタグ `1.13.0` の `ShareSheetPresenter.swift`、`ShareRepositoryImpl.swift`、`IosShareManager.swift` で確かめました。C# 側は `IosShareManager.cs` と `IosClipboardJsonParser.cs` を読みました。



### R-1 [A1] 観点 (1)(2): ネイティブから返る Share の結果で、`ShareCompleted` の回数と順をどこも照らしていない
- 根拠:
  - `ShareScenario.cs:26` は、イベントが来るたびに `share-event` の行を書きます。
  - ところが `ShareUITests.swift` で `share-event` を数えるのは SH-13（:117〜118）だけです。`dismissCase`（:37〜41）、SH-6〜12、14〜18 は `expectLines("share", …)` で、`share` の id しか数えません。
  - Player の SH-22（`IosSharePlayerTests.cs` の `SH22_EventComesBeforeCallbackOnce`）は空の payload で呼びます。これは C# の中だけで失敗する経路（`IosShareManager.Share` の項目なしの分岐）で、ネイティブには届きません。
- 壊れ方: 2.0.0 で書き直すのは、ネイティブの結果を `FireResult` に流すところです。そこでイベントを出し忘れる、2 回出す、コールバックより後に出す、といった誤った移行でも、どのテストも通ります。計画 SH-22 の「イベントが先、コールバックが後、どちらもちょうど 1 回」は、移す方の経路では確かめられていません。
- 直し案:
  - SH-1〜12 と 14〜18 を `expectLines(ofIds: ["share-event", "share"], [eventLine, callbackLine])` に変えます。`finishedLines` は上から下の順なので、イベントが先であることも一緒に照らせます。
  - Player にも、ネイティブまで届く失敗（SH-23 の形）でイベントの回数と順を照らす項目を足します。

### R-2 [A1] 観点 (1)(2)(5): 「シートは出ない」をどこでも確かめていない（SH-9〜12、15、18）
- 根拠:
  - `ShareUITests.swift:20〜25` の `closeIfShown` は、シートが出ても黙って閉じて先へ進みます。SH-9〜12（:81〜103）、SH-15（:144）、SH-18（:176）がこれを使っています。
  - 計画 v3 の SH-9 と SH-10 の列は、1.x も 2.0.0 も「シートは出ない」です。記録 v1 の表も、SH-9 を「シートは出ない」、SH-15 を「シートは出ない」と書いています。
  - `HarnessUITestCase.tearDown`（:34）は `app.alerts` と `app.sheets` しか見ません。共有のシートは `SystemUI.shareSheet` が identifier（`ActivityListView` か `UIActivityContentView`）で探しているもので、`sheets` の型には入らない見込みです。シートが残っても後片付けでは気づけません。
- 壊れ方: 2.0.0 で C# が先に断る形にしたとき（計画が「写し方の設計で決める」としている所）、失敗を出した後に `return` を忘れてネイティブも呼ぶと、シートが出ます。後から来るネイティブの結果は「ちょうど 1 回」の守りで捨てられるので、`share` の行は失敗 1 本だけです。この誤った移行は全部通ります。記録の「シートは出ない」も、テストが成功したことからは言えません。
- 直し案:
  - 「出ない」が期待の項目は `XCTAssertFalse(sheet(timeout: 3), "a share sheet appeared")` にし、出てしまったら閉じてから落とします。
  - tearDown でも `SystemUI.shareSheet(app, timeout: 0)` を見ます。

### R-3 [A1] 観点 (1)(5): SH-14 の「シートが残る」を確かめていない
- 根拠:
  - `ShareUITests.swift:121〜131` は前面に戻した後、`closeIfShown(wait: 5)` を呼ぶだけです。
  - 記録 v1 の SH-14 は「戻ると、シートが残っていた」と書いています。
- 壊れ方: 背景に入るときにシートを閉じてしまい、`completed=False` を返す 2.0.0 の実装（計画は「同じ」を期待）でも、行は 1.x と同じなので通ります。記録の「残っていた」も、このテストからは言えません。
- 直し案: `XCTAssertTrue(sheet(timeout: 5), "the sheet did not survive backgrounding")` の後で閉じます。記録の文は、その確かめを入れてから流し直して書きます。

### R-4 [A1] 観点 (1)(2): CP-13 と CP-14 は検出が壊れていても通る
- 根拠:
  - `IosClipboardPlayerTests.cs:318〜347` が照らすのは、URL の項目に対する「空」（:325）と「null」（:341）だけです。
  - 文字列で置いた対照（見つかるはずの方）は `Record` に書くだけで、照らしていません（:330、:346）。
- 壊れ方: 2.0.0 の包みで、模様を写し忘れる、値を読み落とす、いつも空を返す、といった誤った移行でも、どちらも成功します。しかも、実機の回では `verify_unity_ios.sh` の `on_exit` が `OUT_DIR` を消すので、`Record` の行は残りません。
- 直し案: 1.x の実測（記録 v1 の「文字列で置くと `ProbableWebUrl` が見つかり、`https://example.com/ntk`」）を Assert にします。記録の文書の「次の回で照らす」は、このコミットのうちに済ませます。

### R-5 [A1] 観点 (1)(2)(5): CP-4 のバイト列の一致を照らしていない
- 根拠:
  - `IosClipboardPlayerTests.cs:192〜202` は `HasData` を見た後、送った分と読んだ分の長さとハッシュを `Record` に書くだけです。
  - 計画 CP-4 は「長さとハッシュが同じ」、記録 v1 は「長さもハッシュも同じ（再圧縮されない）」と書いています。
- 壊れ方: 2.0.0 で画像を作り直して書く、base64 の写しを誤ってバイト列が壊れる、といった誤った移行でも通ります。
- 直し案: `CollectionAssert.AreEqual(png, data.V.Data)` を足します。

### R-6 [A2] 観点 (2): 1.x の内部のログの文に縛られていて、正しく移しても落ちる
- 根拠:
  - Clipboard の `ExpectErrorLog`（:68〜69）は、`[IosClipboardJsonParser][ReadErrorInfo] errorCode: …` の文を `LogAssert.Expect` で待ちます。CP-15、19、25、26 がこれを使っています。
  - Dialog の ID-20〜22（`IosDialogPlayerTests.cs:81〜82, 89〜90, 105`）も、1.x の `Debug.LogError` の文と回数を待ちます。
- 壊れ方: 2.0.0 で JSON のパーサーが無くなる、あるいはログの文が変わると、待っていたログが来ないので落ちます。振る舞いが正しくても落ちます。流せば気づけます。
- 直し案:
  - ログは 1.x の実装の細部なので、照らす対象から外します。
  - その間だけ `LogAssert.ignoreFailingMessages = true` にし、予期しないエラーのログは `Application.logMessageReceived` で数えて `Record` に書く形にします。
  - あるいは、「2.0.0 で消してよい既知の差分」として記録の文書に挙げておきます。

### R-7 [B] 観点 (1)(2): 実測した値を照らさず、記録するだけの項目が残っている
- 根拠:
  - CP-2: plain の型があることを見ていません（:172〜173）。
  - CP-5: 型の一覧を見ていません（:213）。
  - CP-9: `public.plain-text` があることを見ていません（:263〜264）。
  - CP-12: `TypeIdentifiers` は url を含まないことだけを見ています（:307）。実測は `public.plain-text` です。
  - CP-35: 何も照らしていません（:654）。実測は `Kind = Removed` が 1 回です。
  - 加えて、実機の回は `OUT_DIR` を消すので、これらの値は実機では取れません。
- 壊れ方: 2.0.0 が型を落としても、`Removed` のイベントを落としても通ります。
- 直し案: 記録 v1 の実測を Assert にします。UIKit が足す型（rtf など）は環境で変わりうるので、Contains で照らします。

### R-8 [B] 観点 (1): SH-7 の「コピーが無い」に、そのテストの中の目印も対照も無い
- 根拠:
  - `ShareUITests.swift:59〜67` は `sheet()` から 2 秒待ちます。`sheet()` は `ActivityListView` と `UIActivityContentView` のどちらか早く出た方で返ります。
  - 計画は、`UIActivityContentView` と「閉じる」を目印にしてから 2 秒待つ、と書いています（6 章 R-4）。
  - `test_ios_ui.sh` の `run_one` は 1 テストずつ別に流すので、SH-6 の対照が同じ回にあるとは限りません。
- 壊れ方: 動作の一覧が描かれるのが遅い回では、除外を無視する誤った移行でも 0 件のまま通ります。
- 直し案:
  - `UIActivityContentView` と「閉じる」を待ってから 2 秒待ちます。
  - そのうえで、ほかの動作のセルが 1 件以上あることを、同じテストの中の対照として照らします。

### R-9 [B] 観点 (1): SH-18 で、戻った後の状態を記録していない
- 根拠: 計画 SH-18 は「戻った後の状態も記録する」と書いていますが、`ShareUITests.swift:168〜178` と `ShareScenario` のどちらにも、それを書く行がありません。記録の「前に戻した後に失敗が 1 回」という時点の主張にも、裏付けがありません。
- 直し案: 結果の受け手の中で `ActivationState()` を行に足すか、戻った後に状態を読む行を足します。

### R-10 [B] 観点 (1)(2): Dialog の ID-20 と ID-21 で、計画の「(null…, …)」と「アラートは出ない」を照らしていない
- 根拠: `IosDialogPlayerTests.cs:33〜38` は、最初の引数（押したボタン、入力した値）を `_` で捨てています。
- 壊れ方: 2.0.0 が null の代わりに `""` を返しても通ります。C# で断った後にネイティブにも渡してアラートを出してしまう形でも通ります。この場合、Player の回の画面にアラートが残ります。
- 直し案: 最初の引数が null であることを照らします。アラートについては、少なくとも 1 秒待ってもそれ以上のイベントが来ないことを照らします。

### R-11 [B] 観点 (3): 2.0.0 で初めて流すときに、実機より先にシミュレーターで流す決まりが無い
- 根拠: CP-21（`IosClipboardPlayerTests.cs:422〜428`）は、実機でも `RemovePasteboard(General)` を呼びます。1.x はこれを断ります。
- 壊れ方: 一般の pasteboard を断る処理を落とした 2.0.0 を、いきなり実機で流すと、私物の端末の一般の pasteboard に `remove` が届きます。何が起きるかは測っていません。SH-23〜25 も同じで、誤った移行ではシートが Player の回の中に出えます。
- 直し案: 段 3 の手順か `DeviceRequirements` の about に、「2.0.0 の Player のテストは、シミュレーターで全件成功してから実機で流す」と書きます。CP-21 を実機の回から外す選択肢もあります。

### R-12 [C] 観点 (5): SH-13 の記録が、結果の届き先の取り違えを書いていない
- 根拠:
  - 推定です。1.13.0 の `ShareSheetPresenter` の守り（`root.isBeingPresented`）は、`viewWillAppear` と `viewDidAppear` の中でしか YES を返しません。
  - そのため 2 つ目の呼び出しは、表示の途中の 1 つ目のシートの上にもう一つ出そうとし、UIKit に黙って断られます。この場合、continuation は再開されないままになります。
  - そうだとすると、出たシートは 1 つ目の呼び出しのもので、その結果が `share2` の受け手に届き、失われたのは 2 つ目の呼び出しの方になります。
- 直し案: 記録の 3 章に、この推定と「どちらのシートかは読んでいない」ことを書き分けます。2.0.0 の写し方の設計の入力として残すためです。

### R-13 [C] 観点 (5): 記録の 2 章の「シミュレーターでだけ」と、実装の範囲が合わない
- 根拠: `harnessLines()` を失敗の文に出す SH-13 は実機でも流れます（`DeviceRequirements.json` の `simulatorOnly` に入っていません）。出す中身はハーネス自身の行だけなので、私物の値が出る心配はありません。
- 直し案: 文書を「実機でも、ハーネスの行だけを出す」に直します。

### R-14 [C] 観点 (4): `ntkh_overlay_on_background` が、同じ background task を 2 回終えうる
- 根拠: `NtkUiHarnessOverlay.mm` の `ntkh_overlay_on_background` は、期限切れのハンドラーと 5 秒後のブロックの両方で `endBackgroundTask:task` を呼びます。
- 壊れ方: 2 回目の呼び出しは警告のログになります。害は小さいです。
- 直し案: 終えた後に `task = UIBackgroundTaskInvalid` にし、2 回目は終えないようにします。

### R-15 [C] 観点 (4): `verify_unity_ios.sh` の後片付けの直しが、確かめられないときも成功とみなす
- 根拠: :221 の `get_app_container … || return 0` は、シミュレーターが止まっている場合や `SIM_ID` が空の場合にも失敗し、そのまま「何も無い」扱いで成功を返します。
- 壊れ方: 本当に残った Player を見逃します。シミュレーターの上なので、影響は小さいです。
- 直し案: 先にシミュレーターが Booted かどうかを見て、Booted なのに container が無いときだけ `return 0` にします。

### R-16 [C] 観点 (4): Clipboard の Player のテストの細部
- `Await`（:56〜60）は入れ子の enumerator の中で Assert しています。これは `IosPlayerTestSupport` の説明が避けるように言っている形です。そこで失敗すると、CP-23 と `CountChanges` の `finally` が飛び、受け手が外れずに残りえます。残った受け手は使われていないリストに書くだけなので、今は害はありません。
- CP-19 は、途中で落ちると `Unique` の pasteboard が後片付けされません。
- CP-31 と CP-23 は、計画 3 章の「補助の関数だけを通す」から外れて manager を直に呼んでいます。scope は渡しているので、安全上の問題はありません。

### R-17 [C] 観点 (1)(2): SH-3〜5 は、項目が落ちても通る
- 根拠: `dismissCase` は、シートが出て閉じた結果しか見ません。
- 壊れ方: 例えば SH-3 で URL を落とし、文字列だけでシートを出す誤った移行でも通ります。計画 4 章が「共有された中身の見た目」を確かめないものとしているので、計画どおりではあります。
- 直し案: シミュレーターだけの項目として、見出しの副題などで項目の数を読むかを、次の版の計画で検討します。

### R-18 [C] 観点 (1): 計画 6 章 R-5 の iOS 26 の閉じ方（`PopoverDismissRegion`）を写していない
- 根拠: `SystemUI.closeShareSheet` は「閉じる」のボタンだけを押します。
- 影響: 今の実機は iOS 18.7.2 なので、すぐの害はありません。端末を iOS 26 に上げると、シートを閉じられずに落ちます。テストが落ちるだけで、ほかのものを押すことはありません。
- 直し案: 段 4 の残りとして、トピックの README に書いておきます。



### 件数
| 等級 | 件数 | 指摘 |
|---|---|---|
| A1 | 5 | R-1〜R-5 |
| A2 | 1 | R-6 |
| B | 5 | R-7〜R-11 |
| C | 7 | R-12〜R-18 |
## 2. Codex

`git show 4457ae2` と指定資料を照合しました。行番号は対象コミット基準です。ファイル変更・Unity／端末での実行は行わず、cleanup の分岐だけモックで確認しました。

1. **A1／R-01／cleanup：照会障害を削除成功として返す**  
   [verify_unity_ios.sh:221](/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_ios.sh:221) は、`get_app_container` の失敗をすべて「未インストール」と扱います。照会を終了コード 70 にしたモックでは、terminate／uninstall を呼ばず `cleanup_status=0` になりました。通常の成功実行では検出されず、照会障害時に削除を省略できます。  
   **修正：** この実行でのインストール有無を追跡し、インストール済みなら削除を試す。不在が確認できない照会エラーは失敗として返す。

2. **B／R-02／Clipboard 共通：完了の合計では未完了を判定できない**  
   [IosClipboardPlayerTests.cs:48](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:48) は完了を共通の `_done` に加算し、[同:129](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:129) で合計だけを比較します。ある操作の重複完了が、別の操作、特に teardown の未完了を相殺できます。  
   **修正：** 全操作の `Box` を保持し、操作ごとに完了がちょうど1回か確認する。

3. **B／R-03／Clipboard teardown：失敗した後片付けでも通る**  
   [IosClipboardPlayerTests.cs:122](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:122) は Stop／Remove のコールバック到着だけを待ち、結果の成功を検証しません。失敗結果も `_done` に数えるため、監視や pasteboard が残っても当該テストは成功できます。  
   **修正：** Stop の成功と、Remove の成功または明示的な不存在を検証する。未完了操作がある場合のキャンセル・再試行も必要です。

4. **B／R-04／CP-4・5・13・14・35：記録値が比較に使われていない**  
   PNG の長さ・ハッシュは [IosClipboardPlayerTests.cs:201](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:201)、色の型は [同:213](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:213)、文字列からの検出結果は [同:330](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:330)・[346](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:346)、削除通知は [同:654](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:654) でログ出力するだけです。想定退行として、画像のバイト列変更、検出結果の常時空、Removed 通知の欠落を許します。  
   **修正：** 採取済みの実測を assertion にする。CP-4 は入力との長さ・ハッシュ一致、CP-13／14 は成功と検出値、CP-35 は Removed の本数を検証する。

5. **B／R-05／CP-2・9：複数表現の片方が消えても通る**  
   CP-2 は [IosClipboardPlayerTests.cs:173](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:173) で HTML 型だけ、CP-9 は [同:264](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:264) で custom 型だけを要求します。両方とも plain-text 表現を落とす移行を検出できません。  
   **修正：** 必須の2型と、それぞれの内容を確認する。UIKit が追加する型とは分けて判定する。

6. **B／R-06／CP-33：監視が始まっていなくても「停止後0回」が成立する**  
   [IosClipboardPlayerTests.cs:618](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:618) は Start の成功を確認せず、Copy の成功も確認しません。Start が失敗結果を返す想定退行では、通知0回を満たせます。  
   **修正：** Start／Copy の成功を検証し、同じ登録で停止前には通知が届くことも確認してから、停止後0回を判定する。

7. **B／R-07／SH-14：背景移行でシートが消える退行を許す**  
   [ShareUITests.swift:129](/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/ShareUITests.swift:129) は復帰後に `closeIfShown` を使います。背景移行時に自動キャンセルされ、同じ `completed=False` が返れば通ります。しかし記録は「復帰後もシートが残った」です（[結果:25](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/share/results/2026-11-14-ios-share-1x-record-result-v1.md:25)）。  
   **修正：** 復帰後のシート存在と、Close 前には未完了であることを確認してから閉じる。

8. **B／R-08／SH-22・Clipboard：ネイティブ経路の共通イベント契約が薄い**  
   SH-22 の呼び出しは [IosSharePlayerTests.cs:58](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosSharePlayerTests.cs:58) の空 payload で、C# の検査経路です。通常の Share UI は [ShareUITests.swift:40](/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/ShareUITests.swift:40) で per-call 行だけを比較します。Clipboard の共通ヘルパーも [IosClipboardPlayerTests.cs:52](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:52) では per-call だけを追跡します。移行時に共通イベントだけが欠落・重複・順序逆転する場合の検出が不足します。  
   **修正：** ネイティブを通る各操作で、対応イベントと per-call の値・本数・順序を照合する。

9. **B／R-09／ID-20〜22：拒否結果の null 引数と回数が不十分**  
   [IosDialogPlayerTests.cs:33](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosDialogPlayerTests.cs:33)〜38 は button／input／username／password を捨てています。計画の `(null…, false, error)` に反して値を返しても通ります。また ID-22 は [同:100](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosDialogPlayerTests.cs:100) で最後の結果を上書きするため、同じ拒否イベントの重複を検出できません。  
   **修正：** 全引数を記録し、各呼び出し直後に「1回・全対象値が null・期待する失敗」を確認する。

件数：A1＝1、A2＝0、B＝8、C＝0、合計＝9。


## 3. 判断

実装のレビューなので、A1 と、比べるときに誤った移行を通すもの（B）を直し、直した後にシミュレーターで流し直す。

- **A1**:
  - サブエージェントの R-1、Codex の R-08: Share の UI のテストは、どの項目も共通イベント（`share-event`）と受け手（`share`）の行を、その順にちょうど 1 本ずつ照らす。Player の SH-20〜25 も、ネイティブに届く経路を含めてイベント → 受け手の順と回数を照らす
  - R-2: 失敗が期待の項目（SH-9〜12、15、18）は「シートが出ない」を照らす。後片付けで共有のシートが残っていないかも見る
  - R-3、Codex の R-07: SH-14 は、前に戻った後にシートが残り、閉じる前には結果が無いことを照らす
  - R-4・R-5・R-7、Codex の R-04・R-05: 記録だけだった実測を照らす（CP-2・4・5・9・12・13・14・35）
  - Codex の R-01: `verify_unity_ios.sh` の後片付けは、この回で入れたかを覚え、入れた後は一覧を読んで消えたことを確かめる（照会の失敗は失敗）。サブエージェントの R-15 も同じ件
- **B**: Codex の R-02・R-03（操作ごとに完了がちょうど 1 回、後片付けの成功）、R-06（CP-33 は停止の前に同じ監視で変化が届くことを確かめてから）、R-09・サブエージェントの R-10（Dialog は値が null で、呼び出しごとに 1 回、1 秒の間に後から来ない）、サブエージェントの R-8（SH-7 は見出しと「閉じる」を目印にし、ほかのセルが在ることを同じテストで照らす）、R-9（SH-18 は結果が来たときの状態を行に書く）
- **A2（R-6）**: 1.x のエラーのログの文を待つ形をやめ、その間は失敗のログを無視する（ログは 1.x の内部で、`2.0.0` で消えてよい）
- **B（R-11）**: 「`2.0.0` の Player のテストは、シミュレーターで全件成功してから実機で流す」を `DeviceRequirements.json` の説明とトピックの README に書く
- **C**: R-12・R-13 は記録の文書を直す。R-14 は background task を 1 回だけ終える。R-16 は `Await` で Assert しない形と、`Unique` の pasteboard の後片付け。R-18 は README の段 4 の残りに書く。R-17 は計画 4 章のとおり確かめない
