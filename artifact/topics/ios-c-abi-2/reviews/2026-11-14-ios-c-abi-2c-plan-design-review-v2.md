# iOS の C ABI 移行（2c）の進め方の設計書 v2 のレビュー v2（差分のレビュー）

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-c-abi-2c-plan-design-v2.md](../designs/2026-11-14-ios-c-abi-2c-plan-design-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。2 回目なので、前の指摘の判定と、変えた部分だけを見る差分のレビュー
- 1 章・2 章はレビュアーの出力の要点をそのまま写した（判定の表はサブエージェントの出力のまま）。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

### 1.1 review v1 の指摘の判定

| v1 の指摘 | 判定 | 補足 |
|---|---|---|
| A1-1（パッケージの置き場所） | 直った | `c1aeca0` がメニューの道を直した。I-1・I-2 は同じ解き方。I-17 が段 1・3-S・4 に git の URL の確かめを足した。段 1 の確かめの回し方に穴がある（N-4） |
| A2-2（3-0 で `#if` の中の `using`） | 直った | |
| A2-3（その場で出すと時点が変わる） | 直った | 文言の誤りが 1 つ残る（N-6） |
| B-4（定義の読み方） | 直った | 端の場合が 1 つ残る（N-7） |
| B-5（Xcode の API のコンパイルの条件） | 直った | `AndroidGradleDependencies.cs:129` と同じ形 |
| B-6（`Initialize()` の意味） | 直った | 申し送り 125 行と合う |
| B-7（案 A） | 直った | 案 D を推奨に。Dialog の例外は正しい（`IosDialogManager.cs:3`） |
| B-8（名前の照合） | 一部 | テストが何も照らさずに通りうる（N-2） |
| B-9（共通部と版の確かめ） | 直った | |
| B-10（最小 iOS） | 一部 | Build Profile が上書きするときに実際の値をどう読むかが無い（N-8） |
| B-11（Simulator.app とクリップボード） | 一部 | 測っていないことを事実として書いた（N-3） |
| B-12（Validate App） | 直った | |
| B-13（通知パッケージとの共存） | 直った | |
| B-14（F-5） | 直った | |
| B-15（冪等） | 直った | |
| C の各件 | 直った | `iOSSimulatorArchitecture` は一部（N-1） |
| Codex 1（Editor のアセンブリがテスト扱い） | 直った | F-13 は正しい |
| Codex 2（macOS が 5 型を使う） | 直った（利用者の判断待ち） | F-15 が欠けている（N-5） |
| Codex 3（PlayMode のテストを Player で流せない） | 直った | F-16 は正しい |

新しい事実の F-13、F-14、F-16、F-17、F-19、F-20 は正しく、F-18 は決まりと合う。F-15 は欠けている（N-5）。

### 1.2 新しい指摘

- **N-1 [B] I-13・I-16**: スクリプトは `-activeBuildProfile` を渡す見込み（Android のスクリプトは必ず渡し、I-13 は Test iOS Profile を挙げる）。その Profile は Player Settings を上書きし、`iOSSimulatorArchitecture: 0`（x86_64）のまま。さらに 3 か所とも `iPhoneSdkVersion: 988`（Device SDK）で、シミュレーターで流すときに Simulator SDK（989）を選ぶ方法が I-13 に無い。`-testPlatform iOS` は `simctl` で入れられない実機用の Player を組む。I-16 の「CLI のテストは Build Profile でなく Player Settings を使う」は `-activeBuildProfile` が無いときだけ正しく、I-13 と食い違う。根拠: `ProjectSettings/ProjectSettings.asset:195-196`、`Test iOS Profile.asset:220-221`、`iOS Profile.asset:224-225`、`scripts/verify_unity_android.sh:15-16, 203`、`android-c-abi-2/README.md:93, 98`。直し: シミュレーターは Simulator SDK と arm64、実機は Device SDK を得る方法を I-13 で決める（`ITestPlayerBuildModifier`、実行の前の `-executeMethod`、シミュレーター用の Profile のどれか）。Test iOS Profile も arm64 にする。段 2 のスパイクで両方の Profile を確かめる
- **N-2 [B] I-15**: このリポジトリの形では `DllImport` の宣言とネイティブだけの補助を `#if <OS> && !UNITY_EDITOR` の中に置く（1.x の iOS も同じ）。反射で `extern` の名前を集める EditMode のテストは 1 つも見つけずに通る。`Marshal.SizeOf` / `OffsetOf` も構造体が Editor で組まれることが要る。「Android の J-2 と同じ形」も誤り（J-2 は Gradle の依存の版の照合で、ヘッダーとの名前の照合の前例は Android に無い）。根拠: `Runtime/Android/Clipboard/AndroidClipboardCApi.cs:192, 236`、`Runtime/Clipboard/IosClipboardManager.cs:207-275`、`Runtime/Share/IosShareManager.cs:56-62`、`android-c-abi-2/README.md:61`。直し: iOS の `extern` の宣言と構造体を Editor でも組む（`!UNITY_EDITOR` の外）か、`.cs` のソースから `static extern … ntk_\w+` を読むかを I-15 で決め、どちらでも数が 0 より大きい（か期待の数）ことを確かめる
- **N-3 [B] I-13 の最後**: 「Simulator.app を開いていなければ Mac のクリップボードには届かない」は、決まりが「測っていない」とするもの。根拠: native-toolkit `agent-rules/device-testing.md:59, 63-66`。直し: Simulator.app を閉じておくのは決まりの手当てで、隔てられることは確かめていない、と書き直す。既定は名前付きの pasteboard にし、Player のテストが一般の pasteboard に書いてよいかを明示して決める
- **N-4 [B] 段 1 と I-17**: 段 1 はコミットしない写しで行うが、I-17 は `git+file:///<このリポジトリ>?path=…#<commit>` で入れる。そのままだと、段 1 の I-1 の口も `.mm` も含まないコミットを確かめ、A1-1 の種類の失敗を捕まえるはずの道の解き方を通らずに通る。根拠: 設計 v2 の 175、183 行、README 6.2 の 98 行。直し: 段 1 の写しは git のリポジトリで、使い捨てのローカルのコミット（push しない）を作り、`git+file:///<写し>…#<そのコミット>` で入れる、と書く
- **N-5 [C] F-15 が欠けている**: macOS のサンプル（`Runtime/UI/macOS/Notification/MacNotificationManagerExampleController.cs:248-445` の 10 か所。`UNITY_STANDALONE_OSX || UNITY_EDITOR`）と macOS の EditMode のテスト（`Tests/Runtime/MacNotificationTests.cs:95-145` の 4 か所）も使う。I-10 の案 2・案 3 の手間を小さく見せる。直し: F-15 と案 3 に足す
- **N-6 [C] I-7 の「呼んだ順に出る」**: 拒んだものは関数が戻る前に積まれ、受け付けたものの完了は後で来るので、出るのは来た順で、呼んだ順ではない（1.x も両方がディスパッチャーを通ったので同じ）。根拠: F-6、`IosClipboardManager.cs:536-575`。直し: 「来た順に出る（1.x と同じ）」と書く
- **N-7 [C] I-2**: `BuildPlayerOptions.extraScriptingDefines` で渡した定義は Player のスクリプトにだけ効き、Editor のアセンブリには効かない。直し: マニュアルに「Player Settings か有効な Build Profile で定義する」と書く
- **N-8 [C] I-16**: Build Profile が上書きするとき（`iOS Profile.asset:226`）に、`IosMinimumVersionCheck` が実際の最小をどう読むかが無い。「段 1 で Player Settings を 18.0 にする」は、段 1 がコミットしない写しであることと食い違う。直し: I-2 と同じく、Profile の上書きの有り無しで段 1 で確かめる。`ProjectSettings.asset` を変えるコミット（3-S か別のコミット）を書く
- **N-9 [C] 段 2 の「パッケージの利用者の条件」は 1.x では動かない**: ビルドの処理の定義が無いと 1.x に Embed の処理は無く、利用者はメニューで足す（F-3）。I-1 (b) で Unity が埋め込むと分からない限り、`IosLibrary.framework` が埋め込まれずに起動で止まる見込み。直し: その条件は 3-S からだけ流すか、1.x ではスクリプトが `IosFrameworkPatcher.Apply` を呼ぶ
- **N-10 [C] 段 3-S と段 3 で段 2 の Player のテストが落ちる**: 段 2 は「各機能を呼べる」1.x の最小の Player のテストを足すが、3-S でその機能は「使えない」になって落ちる。案 D の「どのコミットも確かめが通る」が成り立たない。直し: 3-S でそれらのテストを「使えない」を期待する形に替えるか、機能ごとに印を付け、機能を移すたびに戻す
- 余談（C、新しい事実の範囲の外）: 1a の雛形の `.mm` が `UserNotifications.framework` を要るのはログの行のため（`NtkHandoff2Installer.mm:11, 20`）。ログを外すと（I-2）リンクに要らなくなるかもしれない。足しても害は無い。段 1 で、まだ要るかを記録できる

新しい A1: 0。新しい A2: 0。新しい B: 4（N-1〜N-4）。新しい C: 6（N-5〜N-10）。

## 2. Codex

### 2.1 前回の Codex の指摘

1. 直った: Editor のアセンブリのテスト扱い。今の `NativeToolkit.Editor.asmdef` に `TestAssemblies` の制約は無い。F-13 と I-17 は直しと利用者の環境での確かめを反映している
2. 直った（設計上）: macOS が使う通知の型。推奨の案 1 なら 5 型を元の名前空間に残すので、`MacNotificationJsonBuilder.cs:21` などの参照は壊れない。採る案の決定は残っている
3. 直った（設計上）: 今の PlayMode のテストを Player の基準に使えない。v2 の 155 行と段 2 で、Editor 専用のテストを分け、1.x のネイティブの Player のテストを足してから基準を取るよう直っている

### 2.2 新しい指摘

1. **I-7: 同じ列に積んでも、呼んだ順に出ることは保証できない**。v2 の 105 行。A が非同期で受け付けられ、その完了の前に B が入口で拒まれると、B の失敗が先に列に入り、A より先に出る。根拠: 申し送り 145 行（受け付けた後の完了は後で来る）、167 行（入口で拒むのは同期）、`AndroidNativeCallbackQueue.cs:76`（積んだ順の列）。直し: 「結果が列に入った順に出す」と決める。呼んだ順が要るなら、要求の番号での並べ替えと、先の要求がまだ終わっていないときの扱いが別に要る
2. **I-11・段 3-S: Notification には、使える「Editor の失敗の経路」が無い**。v2 の 137 行、段 3-S の 187 行。Notification の Editor の経路を使うと、失敗が返らず何も起きない。`RequestPermission`、`ShowNotification`、一覧の取得などの結果を待つものが終わらない。根拠: `IosNotificationManager.cs:264, 312, 417`（コールバックを保って `return` するだけ）。直し: Dialog と同じく、Notification にも段 3-S で明示的な仮の実装を足す。結果を返す API は必ず完了させ、結果の型ごとに「使えない」の値を決める。旧いネイティブの呼び出しは `Initialize`・`OnDestroy` を含めて消す

I-1 の道の解き方は、Unity の Mono に `MonoIO.RemapPath` を呼ぶ処理がある（https://github.com/Unity-Technologies/mono/blob/unity-main/mcs/class/corlib/System.IO/Path.cs#L400）。有効な Build Profile の定義は Editor のスクリプトにも効く（https://docs.unity3d.com/kr/6000.0/ScriptReference/Build.Profile.BuildProfile-scriptingDefines.html）。どちらも一般の .NET の振る舞いから欠陥とは判定していない。

## 3. こちらの扱い

- 新しい A1 は両方とも 0。設計のレビューの止め方（A1 が 0 で止める。直すのは A1 と中心の契約の B だけ）により、**レビューはこの回で止める**
- v3 に直すもの: Codex 2.2 の 1・2（I-7 の契約、I-11 の Notification）、サブエージェントの N-1〜N-4（B）、N-10（案 D の「どのコミットも通る」に関わる）、N-5（I-10 の利用者の判断の材料）
- N-6 は Codex 2.2-1 と同じなので、あわせて v3 で直す
- v3 に直さないもの: N-7・N-8・N-9（C。段 1・2・4 の作業で扱う）、余談（段 1 で記録する）
