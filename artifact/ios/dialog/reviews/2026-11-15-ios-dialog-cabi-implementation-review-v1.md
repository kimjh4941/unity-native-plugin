# iOS Dialog の C ABI 2.0.0 への移行の実装のレビュー v1

- 記録日: 2026-11-15
- 対象: `2464bed`（移行）、`672fc0a`（`test_ios_ui.sh` の bash 3.2 の直し）。設計 [../designs/2026-11-15-ios-dialog-cabi-design-v2.md](../designs/2026-11-15-ios-dialog-cabi-design-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

不具合が 1 件あります。`IosDialogManager` のメソッドに `Start` という名前を付けたため、Unity が iOS ターゲットの Editor でも Player でも、起動のたびにエラーを出しています。手元の実行ログで確かめました。ほかの指摘は、設計との小さな外れ、1.x からの記録されていない差分、テストと文書の抜けです。P/Invoke の署名はヘッダーと合っていました（`ref` の構造体、`out UIntPtr`、関数ポインターの `IntPtr`、戻り値の `int`）。静的な delegate の寿命、確保した文字列の解放（例外の経路を含む）、結果の handle を 1 回だけ解放することも問題ありません。ファイルは編集していません。

### R-1 [A] 観点 2: 内部メソッドの名前 `Start` を Unity が引数付きの `Start()` メッセージとみなし、毎回エラーを出す
- 根拠:
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Dialog/IosDialogManager.cs:254` の `private void Start(Kind kind, Func<List<IntPtr>, IntPtr, int> call)`。これは MonoBehaviour のメソッドです。
  - 今回の確かめのログに `Script error (IosDialogManager): Start() can not take parameters.` が出ています。
    - `$TMPDIR/unity-ios-verify/ios-player-run.log:66-67`（シミュレーターの Player の起動時）
    - `$TMPDIR/unity-ios-verify/editmode.log:662-666`
    - `$TMPDIR/unity-ios-verify/ios-player-export.log:669-673`
    - `$TMPDIR/unity-ios-ui-test/harness-export.log`（4 件）
  - 手本の Android は同じ役のメソッドを `ShowNative` と名付けています（`AndroidDialogManager.cs:412`）。
- 壊れ方:
  - 利用者が iOS ターゲットにすると、Editor のコンソールに赤いエラーがドメインの読み込みのたびに出ます。
  - iOS の Player では、起動のたびにエラーのログが 2 行出ます。`Application.logMessageReceived` やクラッシュ・エラーの収集を使うアプリには、アプリ側の誤りのように見えます。
  - テストはこれを捕まえていません。起動時やドメインの読み込み時に出るので、`LogAssert` の範囲の外だからです。
- 直し案:
  - `ShowNative` などに名前を変えます（Unity のメッセージ名 Awake / Start / Update / OnEnable などを避ける）。
  - 再発を防ぐ確かめを足します。たとえば EditMode のテストで「パッケージの MonoBehaviour が、Unity のメッセージ名で引数を持つメソッドを宣言していない」ことを reflection で見る。または `verify_unity_ios.sh` がログの `Script error` で失敗にする。

### R-2 [B] 観点 3: 呼び出しの中で出すイベントで、購読者の例外を握りつぶすようになった（1.x の差分として記録されていない）
- 根拠:
  - `IosDialogManager.cs:241-247`（`RejectedInCSharp`）と `:256-259`・`:278-283`（D-6 の (a)(b)）は、どちらも `Raise`（`:319-349`、`try/catch` で `LogError`）を通ります。
  - 1.x（`93e6b8f` の `IosDialogManager.cs:274, 294, 307, 376` など）は、`DialogResult?.Invoke(...)` を `try` の外か `catch` の中で呼んでいました。このため購読者の例外は `ShowDialog` などの呼び出し元へ伝わっていました。
  - 設計 D-7 で `try/catch` を求めているのは「積むラムダの中」（次の `Update` の経路）だけです。3 章の表にこの差分はありません。
- 壊れ方: 1.x で、検査が失敗したときの購読者の例外を `ShowX` の呼び出し元で捕まえていたコードは、例外を受け取らなくなります。代わりに `"<Event> callback error: …"` の `LogError` になります。改善とも言えますが、公開の振る舞いが変わっており、移行の案内にも載りません。
- 直し案: 次のどちらかにします。
  - 3 章に既知の差分として足し、結果のファイルと XML コメントに書く。
  - 呼び出しの中の経路だけ `try/catch` を外して 1.x と同じにする。

  どちらにしても、Editor の PlayMode に「呼び出しの中の経路で購読者が投げたとき」のテストを足します。

### R-3 [B] 観点 1・4: D-7 と 4 章の「購読者が例外を投げても後の購読者とログ」の確かめが足りない
- 根拠:
  - `Tests/PlayMode/IosDialogManagerEditorTests.cs:95-110` は、`Raise` を直接呼んで、ログが出ることと例外が漏れないことだけを見ています。
  - `Raise`（`IosDialogManager.cs:328,334,340`）はマルチキャストの `Invoke` を使っています。このため、投げた購読者より後に登録した購読者は呼ばれません（1.x も同じ）。
- 壊れ方:
  - 設計の「後の購読者」が「後に登録した購読者にも届く」という意味なら、実装は満たしていません（推測。設計の意図を確かめてください）。
  - D-7 が `try/catch` を置く理由（dispatcher の `Update` で同じフレームのほかの積み荷を遅らせない）も、テストされていません。
- 直し案:
  - 設計の意図を決める。後の購読者にも届けるなら `GetInvocationList()` で 1 つずつ呼ぶ。届けないなら設計の文言を直す。
  - `UnityMainThreadDispatcher.Instance.Enqueue(() => m.Raise(…))` を 2 つ積み、1 つ目の購読者が投げても 2 つ目が同じフレームで出ることを確かめるテストを足す。

### R-4 [B] 観点 4: 拒まれた経路（D-6 (b)）と結果の読み取り（`ReadAnswer`・`OnResult`）を、組み立てた形で通すテストが無い
- 根拠:
  - `IosDialogManager.cs:278-283` の (b) は、文言の単体テストしかありません（`IosDialogCApiTests.cs:189-196`）。
  - `IosDialogCApi.cs:365-382` の `ReadAnswer` は Player だけで組まれます。単体のテストはありません。「NONE なのに result が NULL のとき UNKNOWN にする」分岐（`:367-368`）は、どの層でも通っていません。
  - `UnavailableUntilMoved` を消したので、Player のテストでネイティブまで届く呼び出しは 0 件です。
  - Android は `s_callbackPointer` を internal にして、Player のテストから直接呼べるようにしています（`AndroidDialogManager.cs:403`）。
- 壊れ方: (b) の経路（`LogError` と、呼び出しの中で 1 回のイベント）、または `Read` が NULL と空を取り違える退行は、UI のテストか実機でしか見つかりません。(b) は UI のテストでも通りません。
- 直し案:
  - Player のテストを足す。(b) は、`StructSize` を誤らせた要求（例: 0）で `ntk_dialog_show_alert_async` を呼び、`INVALID_PARAMETER` が返りコールバックが来ないことを見る。
  - `ReadAnswer` の NULL の分岐を、EditMode から呼べる形に分けてテストする（例: 読み取りの関数を引数で渡す）。

### R-5 [B] 観点 1・5: 確かめ V-1 の Android Profile と、V-2 の `--as-package-user` が結果に記録されていない
- 根拠:
  - 設計 5 章の V-1 は「`Android™ Profile` の EditMode でも `IosCApiDeclarationTests` が通る」、V-2 は「`iOS Profile` と `--as-package-user`」です。
  - `artifact/ios/dialog/results/2026-11-15-ios-dialog-cabi-implementation-result-v1.md` の 2.1 節には、どちらもありません。2.2 節（実機）も「記入待ち」のままです。
- 壊れ方: 確かめたかどうかが記録から分かりません。
- 直し案: 実施して結果のファイルに書く。

### R-6 [C] 観点 1: ID-16 の `password-match` が設計と違う
- 根拠:
  - 設計 4 章は「ID-12 と ID-16（空なら `password-match` は空と比べる）で照らす」です。
  - `Assets/NativeToolkitIosUiHarness/Runtime/Scenarios/DialogScenario.cs:74` は、空のとき `password-length=0` だけを出します。`Tools~/IosUiTests/NtkUiTests/DialogUITests.swift:148` もそれを期待しています。
- 壊れ方: 長さ 0 なら空と同じなので、情報としては同じです。設計とは字面が違います。
- 直し案: 設計の文言を実装に合わせる（または `password-match=True` を出す）。

### R-7 [C] 観点 5: `DialogScenario.cs` のコメントが古い・場所がずれている
- 根拠:
  - `DialogScenario.cs:11` は「a password shows only its length」のままです。実際には一致も出します。
  - `:64` の「A success carries no error; …」は `Line` の説明ですが、間に `PasswordFixture` と `Password` が入りました。
- 直し案: 1 行目を直し、`:64` のコメントを `Line`（`:77`）の直前へ移す。

### R-8 [C] 観点 4: 宣言の照合が名前だけで、署名を見ていない
- 根拠: `Tests/Runtime/IosCApiDeclarationTests.cs:84-86` は、`NTK_CALL\s+name\s*\(` の一致だけを見ています。
- 壊れ方: `out UIntPtr` の抜け、`ref` と値渡しの取り違え、引数の数の誤りがあっても EditMode は通ります。今回は手で照らして合っていましたが、退行は防げません。
- 直し案: 宣言ごとに引数の数と各引数の C# の型（`ref` / `out` を含む）の期待を表にし、`MethodInfo.GetParameters()` と比べるテストを足す。

### R-9 [C] 観点 2・3: 例外の経路の文言が 1.x とも Android とも違う
- 根拠:
  - `IosDialogManager.cs:270-271` の例外の経路は、``"Failed to {MethodName}: {ex.Message}"`` と LogError ``"{MethodName} error: …"`` です（例: `showDialog error:`）。
  - 1.x は ``"Internal error: {ex.Message}"`` / `"ShowDialog error: …"`、Android は ``"Internal error: {e.Message}"`` でした。
  - 設計（D-6）はこの経路に触れていません。
- 直し案: 1.x と Android に合わせて `Internal error: …` にするか、設計に書く。

### R-10 [C] 観点 2: 完了が 0 回になる経路がある
- 根拠:
  - `IosDialogManager.cs:295-304` は、`ReadAnswer` が投げると（例: `IosNativeToolkitCApi.cs:141-142` の `OutOfMemoryException`）`LogError` だけで、イベントを出しません。
  - `Raise` の `default`（`:337-342`）は、知らない種類を何も言わずに捨てます（Android は `LogError`）。
- 壊れ方: 「受け付けた呼び出しは 1 回だけ完了する」が崩れます。ただし実際に起きる見込みはほぼありません。
- 直し案:
  - 読み取りに失敗したら `new Answer(kind, ErrorUnknown)` を積む。
  - 知らない種類は `LogError` にする。

### R-11 [C] 観点 5: agent-rules に古い記述が残っている
- 根拠:
  - `agent-rules/coding-rules/testing.md:317` は「Dialog の層 1 対象は Windows の Bridge だけ（Android / iOS / macOS には純粋ロジックが無い）」のままです（411 行と矛盾）。
  - `testing.md:378` の層 2a の一覧に `IosDialogManagerEditorTests` がありません。
  - `common.md:402` は「iOS / macOS は `Runtime/<Feature>/` のまま」です。しかし今回足した `common.md:122` は `Runtime/iOS/Dialog/IosDialogCApi.cs` を例に挙げており、ここでも食い違います（402 行の古さは以前からのもの）。
  - `common.md:112-114` の iOS / macOS 共通の箇条は 1.x の形（delegate を直接渡す）のままです。C ABI で要る「静的な delegate の欄を持ち続け、`GetFunctionPointerForDelegate` で渡す」が書かれていません。
- 直し案: 各行を今の形に直す。

### R-12 [C] 観点 1: 設計との細かい字面の違いと、使われない define
- 根拠:
  - 設計 4 章は `ReadUtf8(IntPtr, UIntPtr)`、実装は `ReadUtf8OrNull`（`IosNativeToolkitCApi.cs:136`）です。
  - D-7 は「EditMode で確かめる」ですが、テストは Editor の PlayMode（`IosDialogManagerEditorTests`）にあります。Manager が `#if UNITY_IOS` なので妥当ですが、設計と違います。
  - `NTK_IOS_CABI_2_DIALOG`（`IosPlayerBuildSetup.cs:54`、`IosUiHarnessBuild.cs:82`）は、どのコードからも参照されなくなりました。`UnavailableUntilMoved` を囲まずに消したためで、ID-17 は `[abi@event]` で分岐しています。
- 直し案:
  - 設計か結果のファイルに、実装に合わせたと書く。
  - define は残す理由（後の機能と同じ形にする）を、コメントか結果のファイルに 1 行書く。

### R-13 [C] 観点 3: Editor の経路でログが消えた（差分の記録は文言だけ）
- 根拠:
  - 1.x の Editor では、`LogError("ShowDialog error: …EntryPointNotFound…")` とイベントが出ていました。
  - 新しい形（`IosDialogManager.cs:256-259`）はイベントだけで、ログを出しません。
  - 設計 3 章の「Editor」の行は「既知の差分（文言）」としか書いていません。
- 直し案: 3 章に「ログが出なくなる」を足す（Android の (a) は `LogWarning` を出すので、合わせるかも決める）。
- 同じく、1.x のシートの完了で出ていた `"Memory for ActionSheet options freed in callback."` も消えています（実害なし）。

### `test_ios_ui.sh` の bash 3.2 の直し（観点 5）
漏れは見つかりませんでした。`/bin/bash` 3.2.57 で `set -u` のもとで試した結果です。
- 空になりうるのは `KEEP_FLAG`（`:69`）と `rest`（`:436`）で、どちらも直っています。
- `INSTALLED_IDS`（`:64`）と `SIGNING`（`:262/267`）は空になりません。直しは無害ですが、コミットメッセージの「空になりうる配列」は少し言い過ぎです。
- `"${!rest[@]}"`（`:464`）、`"${rest[@]:$((i + 1))}"`（`:467`）、`"${TESTS[@]:1}"`、`${#TESTS[@]}`、空の `"$@"` は bash 3.2 でも unbound になりません。
- 空の `TESTS_ARG` は `[ -n ]` で除かれるので、`read -a` の結果が 0 件になる経路はありません（`--tests ","` は `Not a UI test` で exit 2）。
- `ios_script_common.sh` の `SIDE_EFFECT_PATHS` と `verify_unity_ios.sh` の `APPROVAL_TESTS` も空になりません。

件数: A 1 件、B 4 件、C 8 件

## 2. Codex

Codex による静的レビューの結果です。A に該当する実不具合は確認されませんでした。B が 2 件あります。

確認した範囲では、次の点に問題は見つかっていません。
- P/Invoke の署名と構造体の配置は、ヘッダーと一致しています。
- 入力文字列のリークと、結果ハンドルの二重解放は、通常経路にも例外経路にもありません。
- bash 3.2 の `set -u` で残る空配列スライスは、再現コードで確認されました。Codex の報告にはこれを finding として挙げた記載がなく、重大度は付いていません。

- **R2-1（B）ヘッダー照合テストが P/Invoke の署名を検査していない**
  - 根拠: /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosCApiDeclarationTests.cs:47 と :85。収集して照合しているのはライブラリ名と関数名だけです。引数、戻り値、delegate の型は読んでいません。
  - 失敗シナリオ: 要求を値渡しにしたり、`size_t*` の型を誤ったりしても、関数名が同じならテストは通ります。
  - 修正: 追加した 11 関数と `ResultCallback` について、リフレクションで型を検査します。対象は `ref` 構造体、`out UIntPtr`、関数ポインターの `IntPtr`、`int` 戻り値です。

- **R2-2（B）完了イベントを次の Update へ遅延する契約（設計 D-7）が未検証**
  - 根拠:
    - /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosDialogManagerEditorTests.cs:115 は `Raise` を直接呼びます。
    - /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosDialogPlayerTests.cs:106 以降は入力拒否だけを検査します。
    - /Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/HarnessUITestCase.swift:95 は結果ラベルを比較するだけです。
  - 失敗シナリオ: /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Dialog/IosDialogManager.cs:299 のキュー投入を直接の `Raise` に替えると、D-7 に違反します。いまのテストはこの差を検出できません。
  - 修正: Player テストで完了コールバックを制御して呼びます。そして「コールバックから戻るまで 0 回、次の Update で 1 回」を検査します。

なお、Codex は破壊変更によるテスト実行も、Unity や実機テストの再実行もしていません。ファイルも変更していません。

## 3. 判断

| 指摘 | 判断 | 直し |
|---|---|---|
| サブの R-1（`Start` の名前） | 直す（A） | `ShowNative` に。EditMode に「パッケージの MonoBehaviour が Unity のメッセージ名で引数を持つメソッドを宣言しない」の確かめを足す |
| サブの R-2（呼び出しの中の購読者の例外） | 残す（改善）。既知の差分として結果と XML コメントに書く | 呼び出しの中の経路のテストを Editor の PlayMode に足す |
| サブの R-3（後の積み荷） | 直す | 「後の購読者」は dispatcher の後の積み荷の意味。dispatcher に 2 つ積み、1 つ目の購読者が投げても 2 つ目が同じフレームで出るテストを足す |
| サブの R-4、Codex の R2-2（(b) と完了の経路） | 直す | Player のテスト: `StructSize` 0 の要求で `INVALID_PARAMETER` が返りコールバックが来ないこと。完了の受け口（internal にする）を `NOT_FOREGROUND` で呼び、呼んだ直後は 0 回、次の `Update` の後に 1 回 |
| サブの R-5（V-1・V-2 の記録） | 直す | `Android™ Profile` の EditMode と `--as-package-user` を流して書く。実機も書く |
| サブの R-6・R-12（設計との字面） | 結果に書く | ID-16 は長さ 0 で空と同じ。`ReadUtf8OrNull`。D-7 のテストは Editor の PlayMode。`NTK_IOS_CABI_2_DIALOG` は後の機能と同じ形のため残す（コメント） |
| サブの R-7（コメント） | 直す | |
| サブの R-8、Codex の R2-1（署名） | 直す | 宣言ごとの引数の型と戻り値の表を `IosCApiDeclarationTests` に足す |
| サブの R-9（例外の文言） | 直す | 1.x と Android に合わせて `Internal error: …` |
| サブの R-10（0 回の経路） | 直す | 読み取りに失敗したら `UNKNOWN` を積む。知らない種類は `LogError` |
| サブの R-11（agent-rules） | 直す | |
| サブの R-13（Editor のログ） | 直す | Android の (a) に合わせて `LogWarning` を出す。結果に書く |
