# iOS の C ABI 移行（2c）の進め方の設計書 v1 のレビュー v1

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-c-abi-2c-plan-design-v1.md](../designs/2026-11-14-ios-c-abi-2c-plan-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した
- この文書の 1 章・2 章の中身は、レビュアーの出力をそのまま写したもの。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

### A1

**1. [risk] A1 — I-1・I-2（パッケージの置き場所の解決）**
- 問題: I-1 は「UPM の利用者のビルドでも動く」と書いているが、パッケージの実際の場所をどう解決するかが無い。今 `VERSION.txt` を読む 3 か所は、どれも `Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS")`。マニュアルの導入方法は Git URL で、パッケージは `Library/PackageCache/...@hash/` に入るので、この道は無い。今の形を写すと `IosXcframeworkPin.Read` が「VERSION.txt not found」の `BuildFailedException` を投げ、利用者全員の iOS のビルドが止まる。I-2 の `Editor/iOS/NativeSource~/*.mm` も同じ。計画した確かめはどれも埋め込みのパッケージを使うので捕まえられない（段 1 は「写しのプロジェクト」、I-13 の「パッケージの利用者の条件」は define を外すだけ、3-S はこのリポジトリ）
- 根拠: `Editor/Build/PostBuildProcessor.cs:47-48`、`Editor/Build/Tools/iOS/IosFrameworkPatcher.cs:38-39`、`Editor/Build/PreBuildProcessor.cs:475-476`。`Documentation~/index.md:73-76`（Git URL での導入）。`scripts/verify_unity_android.sh:10-20`（`--as-package-user` は Test Profile に切り替えるだけ）。同じ理由で、F-3 の「1.x の利用者はメニューで Embed する」も、Git URL で入れた利用者には動いていない可能性が高い（1.13.0 のタグの `IosFrameworkPatcher` も同じ式）
- 直し: I-1 に「パッケージの場所は `UnityEditor.PackageManager.PackageInfo.FindForAssembly(...)` の `resolvedPath`（か `Path.GetFullPath("Packages/<name>/...")`）で解く」と書く。段 1 か段 2 に、別の空のプロジェクトへ `file:`・tarball・Git URL のどれかで入れて iOS を書き出す確かめを足す。F-3 に注記を足す

### A2

**2. [risk] A2 — 段 3-0 と I-10（「サンプルは動かさない」）**
- 問題: iOS の Player でだけ組まれる `using` が旧い名前空間を指している。3-0 で `IosDialogManager` を `...Runtime.iOS.Dialog` に移すと、iOS の Player のビルドでコンパイルエラーになる。3-0 の確かめは「EditMode と Editor の PlayMode」だけ（`UNITY_EDITOR` が定義されるので、このコードは組まれない）。表に出るのは 3-S で、移行のほかの失敗と混ざる。Android の 3-0 は実機まで流した
- 根拠: `Runtime/UI/iOS/Dialog/IosDialogManagerExampleController.cs:7-9`（`#if UNITY_IOS && !UNITY_EDITOR` の中に `using JonghyunKim.NativeToolkit.Runtime.Dialog;`）。`Assets/Scripts/Runtime/UI/iOS/Dialog/IosDialogManagerTest.cs:4-5, 36-40`。`agent-rules/coding-rules/testing.md:20-48`（層 0）。`android-c-abi-2/README.md:80`
- 直し: I-10 の「サンプルは動かさない」に「フォルダは動かさないが、`using` は直す（`#if` の内側を含む）」と足す。3-0 の確かめに iOS の Player のビルド（Unity の書き出し、IL2CPP まで）を足す

**3. [gap] A2 — I-7（完了を受けたその場で出すこと）が、I-6 と 1.x の時点に反する**
- 問題: 1.x の iOS は、どの機能もコールバックを `UnityMainThreadDispatcher` に積み、次の `Update`（プレイヤーループの中）で出している。I-7 は受けたその場（プレイヤーループの外）で利用者のコールバックとイベントを呼ぶので、I-6 が保つとした「時点」が変わる。通知の背景のアクションでは、Unity のフレームが止まっている間に C# のコールバックが動いたことを 1a で確かめており、I-7 の形では背景にいる間に利用者のハンドラーが走る（1.x は前面に戻った後の `Update`）。入口で拒んだものだけを列に通すので、受け付けたものと拒んだものの順も 1.x と変わりうる。申し送りは「Android と同じ『受けて積む』形のままでも動く」としており、I-7 はそれを採らない理由を書いていない
- 根拠: `Runtime/Notification/IosNotificationManager.cs:479, 666-697`。`Runtime/Common/UnityMainThreadDispatcher.cs:43-61`。`Runtime/Dialog/IosDialogManager.cs:151`。`ios-c-abi-handoff/results/2026-10-31-ios-c-abi-handoff-2-results.md:83`（U-6 (1)）。申し送り 4.1（143 行）
- 直し: 既定を「受け口は写して列に積み、次の `Update` で出す」（Android の J-9 と同じ形）にする。その場で出すなら、変わる時点（背景の間、フレームの外、順）を既知の差分として I-7 に書き、利用者の判断を得る

### B

4. **[risk] I-2**: このリポジトリの iOS Profile と Test iOS Profile は定義シンボルを Profile の側で上書きしている。Build Profile を使う利用者が Player Settings に足しても無視されうる。ビルドの処理が定義をどう読むか（Editor のアセンブリの `#if` か `PlayerSettings.GetScriptingDefineSymbols` か）も書いていない。根拠: `Assets/Settings/Build Profiles/iOS Profile.asset:863-864`、`Test iOS Profile.asset:851-852`。直し: 読み方を I-2 で決める（推奨: Editor のコンパイルの `#if`）。段 1 で、Profile の上書きの有り無しの両方で `.mm` が外れることを確かめる
5. **[risk] I-1**: `UnityEditor.iOS.Xcode` を使う処理を条件の無い `NativeToolkit.Editor` に置くのに、コンパイルの条件を書いていない。iOS のモジュールが無い Editor（Windows で Android だけを組む利用者など）では組めず、`NativeToolkit.Editor` ごとエラーになりうる（Android のビルドの処理も止まる）。根拠: `PostBuildProcessor.cs:3-6`、`IosFrameworkPatcher.cs:1`、`android-c-abi-2/README.md:114`。直し: Xcode の API に触る受け口は `#if UNITY_IOS` で囲み、純粋な判断は条件の外の関数にして EditMode で確かめる
6. **[gap] I-3**: 1.x の公開 `Initialize()` は XML コメントで delegate の登録とやり直しを約束し、`notificationSetup()` を呼ぶ。I-3 は重なる口を足すが、2.0.0 での `Initialize()` の意味を決めていない。`Awake` からも呼ばれるので、入れ直すなら I-2 の入れない設定とぶつかる。根拠: `IosNotificationManager.cs:237-254`、申し送り 4.4。直し: `Initialize()` を入れ直しの口にするか、受け手だけにするかを I-3 で決める
7. **[stage] I-11 案 A**: (a) P/Invoke の宣言・構造体の配置・IL2CPP の `MonoPInvokeCallback` は、4 機能・134 関数とも S まで一度も通らない。「組めないのは S までの間だけ」は、実際には段 3 のほぼ全部。(b) `testing.md` は、ネイティブの境界を変えたら Player のビルド（層 0）を通すと決めている。iOS の Unity の書き出し（IL2CPP まで）は Xcode のリンクの前なのでこの期間も組めるが、各コミットの確かめに入っていない。直し: (b) を足す。(a) について案 D: 最初に S を行い、まだ移していない機能は今の Editor の経路（「使えない」）に落とし、その後で 1 つずつ移す。どのコミットもシミュレーターと実機で通せ、切り分けられる
8. **[gap] 名前の照合**: `DllImport` の名前の誤りや欠けが S のリンクまで分からない。C# の `extern` の名前がすべて dist の `include/NativeToolkitC/*.h` にあることを照らす EditMode のテストを置く（dist が無ければ飛ばす）
9. **[gap] 共通部と版の確かめ**: Android の J-4 に当たる `Runtime/iOS/Common/IosNativeToolkitCApi.cs`（`"__Internal"`、`Common.h` の宣言、UTF-8、版の確かめと食い違ったときの扱い）を決めていない。根拠: `common.md:51, 438-444`、申し送り 194 行
10. **[gap] 最小 iOS**: Player Settings の iOS の最小は 15.0 で、18.0 は Profile の側だけ。根拠: `ProjectSettings/ProjectSettings.asset:197`、`iOS Profile.asset:226`、`android-c-abi-2/README.md:98`。直し: 段 1 で Player Settings を 18.0 にし、18.0 未満で止める確かめ（Android の `AndroidArchitectureCheck` に当たるもの）を置くかを決める
11. **[rules] I-13**: native-toolkit の決まりでは、シミュレーターを動かす間 agent は Simulator.app を開かない（開いているとシミュレーターのクリップボードが利用者の Mac と同期する）。`-runTests -testPlatform iOS` が Simulator.app を開くか、Clipboard の Player テストが Mac のクリップボードに届かないかに触れていない。Test Framework がシミュレーターから結果を返せるかも確かめていない。根拠: native-toolkit `agent-rules/device-testing.md:55, 63-66`。直し: 段 2 に、`pgrep -x Simulator` の確かめ、`xcodebuild` と `simctl` だけで入れて起動すること、Simulator.app を開かずに結果を受け取れるかのスパイクを入れる
12. **[stage] 3-S の Validate App**: 利用者のチームは Personal Team で、Apple Developer Program に登録していない。native-toolkit 側でも配布用の書き出しと Validate App は残っている。根拠: native-toolkit `results/2026-11-03-ios-c-abi-stage2-working-notes.md:3192`（17.15）、`:3163`。直し: 登録していなければ行わず、開発用の署名の `codesign` で代える。行う場合も利用者に聞く
13. **[stage] 通知パッケージとの共存**: 1a では、パッケージの `AuthorizationRequest` の後の入れ直しで戻ることを「試していない」。I-3 はこれに頼るが、確かめる段が無い。このリポジトリは `com.unity.mobile.notifications` に依存しないので別のプロジェクトが要る。根拠: `handoff-2-results.md:77`、申し送り 6.2（312 行）。直し: Notification の段か 3-S に、使い捨てのプロジェクトでの実機の確かめ（許可を得て）を足す
14. **[fact] F-5**: 「リンクの順に関わらずパッケージより後に入る」は、1a では `didFinishLaunching` の逆の順を組んでおらず見込み。「`AuthorizationRequest` の後に入れ直す」が効くことも試していない。根拠: `handoff-2-results.md:71, 77`。直し: F-5 を「確かめたこと」と「見込み」に分け、見込みを 13 につなぐ
15. **[risk] I-1 の冪等**: Android の J-1 は「何度流しても 1 回」と決めたが、I-1 には無い。Append での書き出しや、メニューとビルドの口の両方で、`.mm`・Embed・`UserNotifications.framework` が 2 重に入りうる。直し: 冪等の決まりと、Append での確かめを足す

### C

- [fact] README 22 行の「Clipboard 15、Dialog 6、Notification 22、Share 1」は足すと 44。実物の `[DllImport(` は Notification 21 で、計 43
- [fact] F-12 の「1a は Xcode 26.3 で組めた」は雛形の話で、`dist/1.14.0`（Xcode 26.6 で組んだ）を 26.3 のアプリで使った裏付けではない。段 1 で使った Xcode を記録する
- [stage] I-1 の「書き出し（Export）でも走るか」: iOS は毎回が Xcode のプロジェクトの書き出し。確かめる中身を Replace と Append の違いに替える。段 1 に、`PluginImporter` の iOS の設定で埋め込めるか（申し送り 5 章の選択肢）も足せる
- [rules] I-10: 3-0 の `[MovedFrom(true, …)]` は段 4 で置き換える（1 つの型に 1 つだけ）。`sourceNamespace` は 1.x の `JonghyunKim.NativeToolkit.Runtime.Notification` と明記する（Android の例: `Runtime/Android/Notification/AndroidNotificationPayloads.cs:12`）。申し送り 5 章の `Runtime/Ios/` を規則（`common.md:402`）に従って `iOS` にした理由も一言添える
- [gap] 申し送り 5 章の最後の行（1.x の Swift の `setup()` を呼ぶネイティブのコード）は、このリポジトリに無いので当てはまらないと書いておく
- [stage] 申し送り 8 章のとおり native-toolkit に結果を返す手順が段に無い。外へ出す操作なので、行う前に利用者に聞くと書く
- [rules] `common.md` の「iOS / macOS」の節と、`testing.md` のガードの表（224-237 行）の iOS の更新が、どの段にも無い
- [risk] Test iOS Profile と Player Settings の `iOSSimulatorArchitecture: 0` は x86_64 と読める（要確認）。I-13 で arm64 に決める
- [stage] I-12 の 1.x の worktree: 1.x と 2.0.0 のアプリは同じ bundle id なので、通知の権限の状態が引き継がれて比べた結果が変わりうる（消すには毎回の許可が要る）。worktree は Library の取り込みにも時間がかかる。「S の後でもよい」の但し書きとして書く
- [risk] I-1 の development の扱い: 2.0.0 に `-debug` は無いので、`ResolveIn` と同じにすると Test Runner のビルドのたびに `LogWarning` が 2 件出る

### 指摘の無い観点

- [rules] の命名（`Ios` の接頭辞、`Editor/iOS/`、`Runtime/iOS/Common/`）、`[MovedFrom]` の第 1 引数、実機の前に許可を得ること（F-11、段 1・3・3-S）は規則と合っていた
- 確かめて正しかった事実: F-1、F-2、F-4、F-6、F-7、F-8、F-9、F-10、F-11

## 2. Codex

1. **I-1, stages 1 and 3-S**: The target assembly `NativeToolkit.Editor` is itself a test assembly. A normal UPM consumer who has not set `testables` will not get the hook compiled in, so Embed & Sign and the `.mm` addition never run. Evidence: `NativeToolkit.Editor.asmdef:16` has a `TestAssemblies` setting. Unity 6000.4 `CustomScriptAssembly.cs:180` turns it into a test constraint, and line 380 excludes packages that are not under test. Fix: remove the test setting from the production Editor assembly, or move the hook and its helpers into a non-test assembly. Verify with a UPM-consuming project that has no `testables`.
2. **I-10, stages 3-0 and 4**: The notification payload types being moved are also used by macOS code. Moving the namespace as planned breaks Editor compilation at stage 3-0, because `MovedFrom` does not resolve type references in source. Evidence: `IosNotificationPayloads.cs:5` defines the types in the old namespace. `MacNotificationJsonBuilder.cs:21`, `41` and `56` reference 3 of them, and that file is active in the Editor (line 3). Fix: before the move, separate the macOS code from these types, or keep compatibility types for macOS in the old namespace.
3. **I-13, stage 2**: The existing PlayMode tests cannot be used unchanged as the 1.x Player baseline. The Clipboard test fails to compile because it references Editor-only members, and the Share test contains an expectation that cannot hold on a Player. Evidence: `IosClipboardManagerIntegrationTests.cs:3` is active on iOS too. Lines 37 and 55 use members that sit inside `UNITY_EDITOR` at `IosClipboardManager.cs:427` and `:500`. `IosShareManagerIntegrationTests.cs:57` expects an "available only on iOS" failure even for a valid share request. Fix: make stage 2 include splitting out the Editor-only tests and adding native-Player tests for 1.x, and take the baseline only after that.

The relaying subagent added this caveat: Codex produced these 3 findings and the subagent did not check them independently. It also did not see Codex compare the document's I-numbers against the hand-off in detail, so the hand-off comparison may be thinner than the other checks.

## 3. こちらで確かめたことと扱い

- **Codex 1 とサブエージェント A1-1 は、v1 の計画の外でもう壊れていた**。使い捨てのプロジェクトに `git+file` で入れて確かめた（README 6.2 の U-1〜U-3）。`NativeToolkit.Editor` の `TestAssemblies` は 1.13.0 から、iOS のメニューの道は 1.x から。利用者の判断で先に直した（`NativeToolkit.Editor.asmdef` の行を消し、`IosFrameworkPatcher` の道を `Path.GetFullPath("Packages/...")` にした）。1.13.1 は出さず 1.14.0 で直す
- Codex 2 は確かめた（`MacNotificationJsonBuilder.cs:21, 41, 56`）。Codex 3 はまだ確かめていない
- ほかの指摘は、設計書 v2 で扱う
