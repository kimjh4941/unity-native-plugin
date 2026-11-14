# iOS の C ABI 移行の段 3-S（切り替え）の設計 v1 のレビュー v1

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-c-abi-3s-switch-design-v1.md](../designs/2026-11-15-ios-c-abi-3s-switch-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

設計書 `artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-3s-switch-design-v1.md` の 1 回目のレビュー（全体）です。ファイルは編集していません。行番号は設計書の行です。

### R-1 [A2] 観点 4: テストの定義を Player Settings に入れても、Build Profile の上書きで消える
- **根拠**
  - 設計書 59 行は「Player Settings（iOS）の Scripting Define Symbols に `NTK_IOS_CABI_SWITCHED` を足す」としている。
  - しかし `Assets/Settings/Build Profiles/iOS Profile.asset:863-864` は、Player Settings の上書き（`m_PlayerSettingsYaml`）の中で `scriptingDefineSymbols: iPhone: NATIVETOOLKIT_ENABLE_BUILD_STEPS` だけを持つ。`Test iOS Profile.asset:851-852` は空。
  - `scripts/verify_unity_ios.sh:75, 201, 292` は、いつも `-activeBuildProfile "iOS Profile"` を渡す。
  - README 6.3 の S-5（155 行）は「Profile が Player Settings ごと上書きする形は確かめていない」と書いている。
  - `Test iOS Profile` が、ProjectSettings にある `NATIVETOOLKIT_ENABLE_BUILD_STEPS` 無しで組まれていることから、上書きの形では Profile の定義が Player Settings の定義を置き換えると読める。
- **壊れ方**
  - Player のコンパイルに `NTK_IOS_CABI_SWITCHED` が入らず、1.x のテストが組まれる。
  - V-3 で、SH-23〜25（期待は `No shareable items…`、実際は `iOS share is only available…`）、Clipboard、Notification が落ちる。
  - V-4 の `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` も、同じ理由で効かない見込み。
- **直し案**
  - テストの定義は、`Assets/NativeToolkitIosPlayerTests/Editor/IosPlayerBuildSetup.cs:53` の `ModifyOptions` で `options.extraScriptingDefines` に足す。前例は `IosUiHarnessBuild.cs:80` の `NTK_UI_HARNESS`。Profile に左右されず、`Assets` の中なので利用者にも配らない。
  - V-4 の定義は、使う Profile の YAML に入れると明記する。上書きの形での効き方を V-4 で確かめる。

### R-2 [A2] 観点 2・5: `.mm` の差し込み方が、段 1 や 1a で確かめた形と違う
- **根拠**
  - 設計書 24 行は「`didFinishLaunchingWithOptions` の中で…UnityAppController の分類で差し込む」としている。
  - 1a の雛形（`native-toolkit/artifact/topics/ios-c-abi/results/probes/stage1a/handoff2/unity/NtkHandoff2Installer.mm:5, 12, 31-39, 52`）は、`NSObject<AppDelegateListener>` を `+load` の `UnityRegisterAppDelegateListener` で登録し、`didFinishLaunching:`（通知）で呼ぶ。
  - 1a の結果（`ios-c-abi-handoff/results/...-1-results.md:95`）も、親の I-2（101 行）も、この形を前提にしている。
- **壊れ方**
  - 分類（category）で `application:didFinishLaunchingWithOptions:` を定義すると、UnityAppController の本来の実装が置き換わり、Unity が起動しない。
  - V-2・V-3 で気づけるが、書き方が二通りに読める。
- **直し案**
  - 1a と同じ「`AppDelegateListener` と `+load` での登録、`didFinishLaunching:`」と明記する。
  - クラス名には接頭辞を付ける（例: `NtkNotificationInstaller`）。利用者のクラスと名前がぶつかるのを避けるため。

### R-3 [B] 観点 1・5: I-15 の照合のテストが、ヘッダーの場所と「無いときは失敗」で親の決定から外れ、利用者の形の前提も誤っている
- **根拠**
  - 設計書 36 行。
  - 親の I-15（198 行）は「`dist/<VERSION.txt の dist_version>`、dist が無い環境では飛ばす」。
  - 設計書は `dist/1.14.0` を固定で書き、「`Tests/EditMode` は利用者に配らない」とする。しかし実際の EditMode テストは `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime`（asmdef `NativeToolkit.Runtime.Tests`、`TestAssemblies`）にあり、git の URL で配られる。
  - このテストは `verify_unity_android.sh:381` と `verify_unity_windows.sh:138` の EditMode でも組まれる。
  - dist のルート（`NATIVE_TOOLKIT_DIST_ROOT` か隣のリポジトリか。`PreBuildProcessor.cs:20-32`）が書かれていない。
- **壊れ方**
  - dist を次の版に上げたとき、固定の 1.14.0 を照らし続けても気づけない。
  - native-toolkit が隣に無い環境や、利用者が `testables` に入れた形では、EditMode が落ちる。
- **直し案**
  - パッケージが持つ capi の xcframework のヘッダー（`Plugins/iOS/<VERSION.txt の capi の名前>/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/*.h`）を `Path.GetFullPath("Packages/...")` で読む。
  - `cmp` で、dist の `include` の 5 つと、ios-arm64 とシミュレーター向けの両方が同じバイト列であることを確かめた。
  - こうすると、リンクする版そのものと照らせて、どの形でもヘッダーがある。無いときの失敗も正当になる。
  - 0 章で、親からの変更として記録する。

### R-4 [B] 観点 1: 宣言の置き場所と数の確かめが抜け、親の N-2 の直しに戻っている
- **根拠**
  - 設計書 31 行と 36 行（「宣言の数も出す」）。
  - 親の I-15（198 行）は「`#if UNITY_IOS && !UNITY_EDITOR` の外に置き」「集めた数が期待の数と等しい（0 では通らない）」。
  - 今の形は宣言を `!UNITY_EDITOR` の中に置いている（`IosShareManager.cs:57-63`、`IosClipboardManager.cs:208-276`）。
  - macOS の Manager も `__Internal` を使う（`MacDialogManager.cs` ほか。`UNITY_STANDALONE_OSX`）。
- **壊れ方**
  - 今の形を写すと、反射で 0 件を集め、名前の照合も「旧い宣言 0」も何も照らさずに通る。V-1 は通る。
- **直し案**
  - `IosNativeToolkitCApi` を `#if UNITY_IOS || UNITY_EDITOR` で囲み（Android の `AndroidNativeToolkitCApi.cs:3` と同じ）、宣言は `!UNITY_EDITOR` の外に置く。
  - テストは数が `== 1` であることを照らし、集める対象を名前空間 `JonghyunKim.NativeToolkit.Runtime.iOS` で絞る、と明記する。

### R-5 [B] 観点 6・1: 利用者の条件（`Test iOS Profile`）での Player のテストが確かめに無い
- **根拠**
  - 親の I-13（187 行）は「`NATIVETOOLKIT_ENABLE_BUILD_STEPS` 無し、`Test iOS Profile` でも流せるようにする」。
  - `verify_unity_ios.sh:28-32, 92-93` は `--as-package-user` を「2.0.0 の切り替え（3-S）まで」拒んでいる。
  - 設計書の V-1〜V-7 に、この拒みを外すことが無い。
- **壊れ方**
  - ビルドの処理無しで `.meta` の Embed と口だけで動くことを、このリポジトリでは確かめないまま進む。V-6 は手で 1 回行うだけ。
- **直し案**
  - 3-S で `--as-package-user` を使えるようにする。
  - V-3 に「`Test iOS Profile` でもシミュレーターで通る」を足す。

### R-6 [B] 観点 6: 冪等（Append）と、入れない設定を後から付けた場合が確かめに無い
- **根拠**
  - 親の I-1（94 行）は「Append で 2 回流して重ならないことを確かめる」。
  - S-3 で確かめたのはスパイクの口（リンクと Embed も足す実装）で、3-S の口とは違う。
  - 設計書 23 行の口は「足す」だけで、外さない。
- **壊れ方**
  - `.mm` の重複を見ないと、2 回目の Append で重複シンボルになり、リンクが止まる。
  - 一度 `.mm` 付きで書き出した後に `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` を付けて Append すると、古い `.mm` が残り、入れない設定が黙って効かない（確かめていない）。
  - V-2〜V-6 はどれも新しく書き出すので、気づけない。
- **直し案**
  - V に「Append を 2 回して `.mm` が 1」を足す。
  - 入れない設定のときは、既にある `.mm` を外すか、分かりやすい文言で止めると書き、それも V に足す。

### R-7 [B] 観点 4: `.mm` がテストのための値を `NSUserDefaults` に書き、それが利用者のアプリに入る
- **根拠**
  - 設計書 61 行。`.mm` は `Editor/iOS/NativeSource~` にあり、利用者の全アプリに入る（V-6 の形）。
  - iOS の `PlayerPrefs` は `NSUserDefaults` を使うので、利用者の PlayerPrefs に知らないキーが毎回の起動で書かれる。
  - V-4 で、テストが入れない設定かどうかをどう知るかも書かれていない。
- **壊れ方**
  - テストの仕組みを製品に配る。`NONE` は「呼ばれたこと」を示すだけで、delegate が入ったことまでは示さない。
- **直し案**
  - 確かめのための native は、`Assets/NativeToolkitIosPlayerTests` に置き、`IosPlayerBuildSetup` が足す（ハーネスの `IosUiHarnessBuild.cs:95-104` と同じ形）。
  - 中身は `UNUserNotificationCenter.currentNotificationCenter.delegate` のクラス名を返すもの。1a の結果では `IosLibraryCApi.NotificationDelegateProxy`。`UserNotifications` のリンクを避けるなら `NSClassFromString` で引く（F-4）。
  - テストは `#if NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` で期待を替える（S-5 で、Player のコンパイルにもこの定義が入ると確かめた）。

### R-8 [B 中心契約] 観点 3: Notification の仮の経路で、出す順・受け手の扱い・積む先が決まっていない
- **根拠**
  - 設計書 53 行は「`NotificationOperationCompleted` も出す」だけ。
  - 1.x は「受け手 → イベント」の順（`IosNotificationManager.cs:484-485`）で、1.x のテストもこの順を照らす（`IosNotificationPlayerTests.cs:150`）。Share と Clipboard は逆の順。
  - 1.x の受け手は、操作ごとに 1 つの枠を上書きする（`IosNotificationManager.cs:265, 313` ほか）。
  - 親の I-7（134 行）は `UnityMainThreadDispatcher` を使わないとする。
- **壊れ方**
  - 実装者が Share の形（イベントが先）を写すと、`UnavailableUntilMoved` も同じ人が書くので、1.x とずれたまま通る。
  - 2 回続けて呼んだときに、結果が何回返るかが決まらない。
- **直し案**
  - 「受け手 → イベント（1.x と同じ）」「呼び出しごとに受け手を写して 1 回返す」「積む先は `UnityMainThreadDispatcher`（仮の経路なので）か `IosNativeCallbackQueue` か」を表に足す。

### R-9 [B] 観点 6・1: 最小 iOS の確かめ（V-5）が Profile の上書きを考えていない
- **根拠**
  - 設計書 42 行と 73 行（V-5 は「Player Settings を 17.0 に」）。
  - review v2 の N-8（Profile の上書きのとき実際の値をどう読むか）は、v4 でも 3-S でも答えていない。`iOS Profile.asset:226` は 18.0。
- **壊れ方**
  - `-activeBuildProfile "iOS Profile"` のまま ProjectSettings を 17.0 にしても、チェックは止まらない。それが正しいのか不具合なのか判断できない。
  - 文字列で比べると `"9.0" > "18.0"` になる。
- **直し案**
  - V-5 を「Profile の上書きの有り無しの両方」で行い、どの値を読むかを書く。
  - 比べるときは `System.Version` で数として比べると明記する。

### R-10 [B] 観点 1・6: 純粋な関数の EditMode テストと、「止める」経路の確かめが無い
- **根拠**
  - 親の I-1（92 行）は「入れる名前・入れるかどうかの判断は条件の外の純粋な関数にして EditMode で確かめる」。
  - 設計書 1.2、1.5、1.7 には、`IosXcodeProjectSetup` と `IosMinimumVersionCheck` のテストが無い。
  - 「Embed が無ければ文言で止める」を確かめる V も無い。
- **壊れ方**
  - `.meta` の `AddToEmbeddedBinaries` が落ちたときの失敗の経路が、一度も動かない。
- **直し案**
  - EditMode のテストを足す（PBX の数から判定する関数、名前の選び方、版の比べ方）。
  - V に「`.meta` から `AddToEmbeddedBinaries` を外すとビルドが文言で止まる」を足す。

### R-11 [C] 観点 6: 「シミュレーターで全件成功してから実機」の決まりが V に無い
- **根拠**: README 94 行（段 3-R の実装レビュー R-11）。V-3 と V-6（71、74 行）。
- **直し案**: V-3 と V-6 の実機の回の前提として、その決まりを書く。

### R-12 [C] 観点 3・4: ファイルごと 1.x のテストを外すと、3-S の後も正しいテストまで消える
- **根拠**
  - 設計書 60 行。
  - Dialog の ID-20〜22 は、3-S の後も同じ時点・同じ文言（設計書 52 行が自分でそう書いている）。
  - Share の SH-20・21 と、Clipboard の引数の確かめ（`IosClipboardManager.cs:648-660`。プラットフォームの確かめより前）も同じ。
- **直し案**: 定義はテストごと（ネイティブに届くものだけ）にかける。C# の確かめのテストは 3-S の後も流す。

### R-13 [C] 観点 4・6: 「UI のテストは流さない」の仕組みが無く、理由の書き方も不正確
- **根拠**
  - `test_ios_ui.sh` は、`--tests` が無ければ全件を流す。
  - `[abi@event]` を出すのは `NotificationScenario.cs:37` だけで、Dialog・Share・Clipboard のシナリオには無い（62 行の理由と合わない）。
  - `[abi@event]` は全体で 1 つの値なので、機能ごとの「移した・移していない」は表せない。
- **直し案**: 3-S から最後の機能までの間、既定でどのテストを流すか（除く一覧か、ランナーの引数か）を決めて書く。

### R-14 [C] 観点 1: `DllImport` の数え方が誤っている
- **根拠**
  - 設計書 46 行は「Notification 22、計 44」とする。
  - `grep -c '\[DllImport('` では Notification は 21。22 は `IosNotificationManager.cs:97` のコメント行 `// ── DllImport declarations` を数えたもの。
- **直し案**: README の「43（Notification 21）」が正しい。46 行を直し、「README は数え直す」を消す。

### R-15 [C] 観点 1: 古くなる記述と参照の誤り
- **根拠**
  - 設計書 19 行の「1.3 を見る」は 1.4 の誤り。
  - `Plugins/iOS/VERSION.txt` の頭のコメント（`PostBuildProcessor … READ the keys`、`The 1.x Swift library and Unity bridge`）が古くなる。
  - `PreBuildProcessor.cs:231` の `IsDevelopmentBuildInProgress` の説明（PostBuildProcessor が読む）も古くなる。
  - 親の I-14 の「食い違いは 1 回だけ `LogWarning`」が 1.3 に無い。
- **直し案**: 3-S のコミットで直すものとして書き足す。

### R-16 [C] 観点 5: 1.x で書き出したプロジェクトに Append で上書きする利用者
- **根拠**
  - 1.x の `PostBuildProcessor` とメニュー（`IosFrameworkPatcher.cs:56-80`）は、同じ `Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS` に 1.3.0 の 2 つを足していた。
  - F-1（同じ名前の `IosLibrary.framework` は同じアプリに入れられない）。
- **壊れ方**: Append で 1.3.0 の参照が残ると、1.x と 2.0.0 の framework が両方入る（Unity の Append がそれを残すかは確かめていない）。
- **直し案**: 口の確かめで、1.x の名前（`unity-ios-native-toolkit-*`、`ios-native-toolkit-1.*`）の参照も見て止める。段 4 の移行ガイドの項目にする。

### R-17 [C] 観点 5: `.mm` を写すのか参照するのかが無い
- **根拠**: 親の I-2（101 行）は「`Libraries/com.jonghyunkim.nativetoolkit/` に写して」。設計書 23 行は「UnityFramework に足す」だけ。
- **壊れ方**: `Library/PackageCache` の絶対の道で参照すると、書き出したプロジェクトをほかの Mac に移したときや Library を消したときに組めない。
- **直し案**: 写すと明記する。

## 等級ごとの件数
- A1: 0
- A2: 2（R-1、R-2）
- B: 8（R-3、R-4、R-5、R-6、R-7、R-8 は B 中心契約、R-9、R-10）
- C: 7（R-11〜R-17）

## 観点ごとの所見
- **(2) Player が組めるか**: `Assets` の UI ハーネスの native（`NtkUiHarnessOverlay.mm`）は 1.x の記号を `dlsym` で引くので、2.0.0 でもリンクは壊れない。`__Internal` の旧い宣言は、4 つの Manager にしか無いことを grep で確かめた。xcframework を替えるのと同じ 1 コミットにする分け方（79 行）は正しい。
- **(3) 「使えない」の振る舞い**: Share と Clipboard の文言・エラーコードは今のコード（`IosShareManager.cs:118, 133`、`IosClipboardManager.cs:60, 622`）と合っている。Dialog の時点（呼び出しの中で 1 回）も、ID-20〜22 と合っている。
## 2. Codex

**A1は確認できませんでした。A2が1件、B（検証・契約の不足）が5件、C（記述誤り）が1件あります。**

切り替えを1コミットにまとめ、旧宣言と呼び出しを同時に除去する方針、通知の結果APIを必ず完了させる方針は、親決定に整合しています。以下は修正・補足が必要な点です。

1. **A2／R1：テスト用定義を `ProjectSettings` だけに追加すると、ランナーのビルドに反映されない。**
   - 設計:59は定義の変更先を `ProjectSettings` だけに限定しています。
   - しかし `scripts/verify_unity_ios.sh:292` は Build Profile を指定します。その `Assets/Settings/Build Profiles/iOS Profile.asset:863` は独自の `scriptingDefineSymbols` を持ち、`Test iOS Profile.asset:851` も同様です。
   - グローバル設定だけを変更すると 1.x 側のテストが残り、V-3 で失敗します。Profile による上書きは Unity の仕様です。
   - 修正: 両 Profile の有効な Player Settings にも定義を反映するか、Profile の追加定義へ統一してください。定義なし、開発用 Profile、利用者用 Profile の各場合で、期待するテスト名が実際に実行されたことを確認します。定義を利用者へ配らない方針自体は妥当です。

2. **B 中心契約／R2：unavailable テストの合格条件に、回数・順序・時点が不足している。**
   - 設計:60 は既存テストを外し、値の全文比較へ置き換えます。
   - 既存テストには次の確認があります。
     - Share: イベント→callback 各1回（`IosSharePlayerTests.cs:31`）
     - Notification: callback→イベント 各1回（`IosNotificationPlayerTests.cs:143`）
     - Dialog: 同期完了と後続通知なし（`IosDialogPlayerTests.cs:73`）
   - 値だけの比較では、同期化、二重通知、順序変更を検出できません。
   - 修正: Dialog は 6 API すべてについて、呼び出し内で1回完了し後続通知なしと明記してください。他の3機能は、呼び出し直後は0回、次の Update で各1回、追加通知なしと明記してください。既存の配送順も維持します。Notification は同じ API を連続2回呼び、各 callback が1回ずつ完了する検証も必要です。

3. **B 中心契約／R3：宣言照合が0件でも成功できる。**
   - 設計:36 は件数を「出す」だけです。一方、親 I-15（`2c-plan-design-v4.md:198`）は期待件数との一致を要求しています。
   - 現行 Share は宣言を `!UNITY_EDITOR` 内に置いており（`IosShareManager.cs:57`）、反射対象から消えます。Player のリンクが成功しても、Editor の照合は空で通り得ます。
   - 修正: 3-S では宣言集合が厳密に `{ntk_version}`、件数1であることを assert してください。宣言は Editor でもコンパイルする、という契約も明記します。

4. **B 中心契約／R4：ヘッダー照合テストを開発環境だけで組む仕組みが未定義。**
   - 設計:36 は「`Tests/EditMode` は利用者に配らない」としますが、配置、asmdef、配布除外の仕組みがありません。
   - 既存の Editor テスト用 asmdef は別の `Tests/Runtime` にあります（`Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef:1`）。
   - パッケージのテストは、利用者も `testables` で有効化できます。通常の git-URL 導入だけを試す V-6 では、この条件を検証できません。
   - 修正: 本当に配らないなら、リポジトリの `Assets` 配下に専用の Editor テストアセンブリを置いてください。V-1 では、そのテストが検出・実行されたことも確認します。

5. **B 中心契約／R5：`.mm` の実行判定に必要な観測経路が不足している。**
   - 設計:61 は `NSUserDefaults` の戻り値0を確認しますが、C# からの読み取り方法と、正常側でのキー存在確認を定めていません。
   - 整数取得だけで実装すると、キー未作成でも0になります（SDK の `NSUserDefaults.h:98`）。
   - 修正: 読み取り手段、キーの存在、今回の起動の記録、戻り値0を別々に検査してください。V-4 では記録の不在を検査します。読み取り用ネイティブ関数を追加する場合は、installer を外したビルドでもリンクできる配置にします。
   - これは未実装部分の検証上の穴であり、A1 とは判定していません。

6. **B／R6：V-1〜V-7 に Append での冪等性確認がない。**
   - 設計:23 は冪等性を要求しますが、V-2・V-4（設計:70）は再書き出し条件を指定していません。
   - 既存ランナーは毎回出力先を削除します（`verify_unity_ios.sh:288`）。そのため、重複追加や、通常ビルド後に skip へ切り替えた際の既存 `.mm` 残存は検証されません。
   - 修正: 同一出力先への Append を2回行い、通常→skip→通常でも登録数とソースの有無を確認してください。既存 installer を除去するか、出力先の作り直しを要求するかも決めます。

7. **C／R7：旧宣言数は44ではなく43。**
   - 設計:46 の Notification「22」は、`IosNotificationManager.cs:97` のコメントも数えています。実際の属性は21個で、合計は 15＋6＋21＋1＝43 です。
   - 修正: 属性または extern 宣言だけを数え、README の 43 を維持してください。

Codex は読み取り専用で実行され、ファイルは変更していません。`check_design_consistency.py` の機械照合は通りましたが、実際の宣言数などは検査対象外でした。Unity／Xcode のビルドと端末実行は行っていません。

**件数：A1＝0、A2＝1、B＝5（うち B 中心契約4）、C＝1。**
## 3. 判断

2 人とも A1 は 0。1 回目（全体）なので止めず、A2 と B を直した v2 を作り、2 回目（差分だけ）に出す。

- **テストの定義**（サブエージェントの R-1、Codex の R1）: Player Settings ではなく、`IosPlayerBuildSetup.ModifyOptions` が `extraScriptingDefines` で足す（`IosUiHarnessBuild` の `NTK_UI_HARNESS` と同じ形。Profile に左右されず、利用者に配らない）。V-4 の入れない設定は、使う Profile の YAML に入れて確かめる
- **`.mm` の形**（R-2、R-17）: 1a と同じ `AppDelegateListener` を `+load` で登録し、`didFinishLaunching:` の通知で呼ぶ。クラス名に接頭辞（`NtkNotificationInstaller`）。書き出したプロジェクトの `Libraries/com.jonghyunkim.nativetoolkit/` に写して足す
- **宣言の照合**（R-3、R-4、Codex の R3・R4）: パッケージが持つ capi の xcframework のヘッダーを読む（親の I-15 からの変更として 0 章に書く）。宣言は `!UNITY_EDITOR` の外、`IosNativeToolkitCApi` は `#if UNITY_IOS || UNITY_EDITOR`。名前空間で絞って、宣言の集合がちょうど `{ntk_version}` であることを照らす。テストは今の EditMode の asmdef（パッケージの中）に置く（ヘッダーもパッケージの中なので、利用者の形でも通る）
- **利用者の条件**（R-5）: 3-S で `verify_unity_ios.sh --as-package-user` を使えるようにし、`Test iOS Profile` でもシミュレーターで通ることを V に足す
- **冪等と入れない設定**（R-6、Codex の R6）: Append を 2 回しても `.mm` が 1、`.mm` 付きの後に入れない設定で Append すると `.mm` を外す（参照とファイル）。どちらも V に足す
- **`.mm` の確かめ方**（R-7、Codex の R5）: 利用者のアプリに値を書かない。確かめの native は `Assets/NativeToolkitIosPlayerTests` に置いて `IosPlayerBuildSetup` が足し、`UNUserNotificationCenter` の delegate のクラス名を `NSClassFromString` で引いて返す。期待は 1a の `IosLibraryCApi.NotificationDelegateProxy`（実測で決め直す）。入れない設定のときは、その名前でないことを照らす
- **「使えない」の振る舞い**（R-8、Codex の R2）: 機能ごとに、回数・順・時点を表に書く。Notification は「受け手 → イベント」（1.x と同じ）、呼び出しごとに受け手を写して 1 回返す、積む先は今の `UnityMainThreadDispatcher`（仮の経路なので）。同じ API を続けて 2 回呼んでもそれぞれ 1 回
- **最小 iOS**（R-9）: 比べるのは `System.Version`。読む値は、ビルドの時点の `PlayerSettings.iOS.targetOSVersionString`（Profile の上書きがあればその値が入る見込み。V-5 で Profile の有り無しの両方を確かめる）
- **純粋な関数のテストと止める経路**（R-10）: 判定の関数を EditMode で確かめ、`AddToEmbeddedBinaries` を外すとビルドが文言で止まることを V に足す
- **C**: R-11（シミュレーターで全件成功してから実機）、R-12（定義はテストごとにかけ、C# の確かめのテストは 3-S の後も流す）、R-13（移していない機能の UI のテストは、`DeviceRequirements.json` の一覧で既定から外す）、R-14・Codex の R7（数は 43）、R-15（古くなる記述と I-14 の `LogWarning`）、R-16（1.x の framework の参照が残っていれば口が止める）は取り込む
