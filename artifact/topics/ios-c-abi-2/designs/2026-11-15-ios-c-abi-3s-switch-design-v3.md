# iOS の C ABI 移行の段 3-S（切り替え）の設計 v3

**レビュー v2（[../reviews/2026-11-15-ios-c-abi-3s-switch-design-review-v2.md](../reviews/2026-11-15-ios-c-abi-3s-switch-design-review-v2.md)）で 2 人とも A1 が 0 になり、止めた。** v3 は止めた後の直しで、レビューに出していない（7 章）。

## 0. v1 からの変更

[レビュー v1](../reviews/2026-11-15-ios-c-abi-3s-switch-design-review-v1.md) の 3 章の判断による（一覧は 6 章）。**親の決定からの変更**: I-15 のヘッダーは `dist/<dist_version>` ではなく、パッケージが持つ capi の xcframework のヘッダーを読む（リンクする版そのものと照らせ、利用者の形でもヘッダーがある。dist の `include` と、framework の ios-arm64 とシミュレーター向けの `Headers/NativeToolkitC/*.h` は同じバイト列だとレビューで確かめた）。

## 基本情報

| 項目 | 内容 |
|---|---|
| 親 | [artifact/topics/ios-c-abi-2](../README.md)。進め方は [2c-plan-design-v4](2026-11-14-ios-c-abi-2c-plan-design-v4.md) の 3 章の 3-S の行と、決定 I-1・I-2・I-4・I-7・I-11・I-13〜I-17 |
| 目的 | パッケージの iOS を native-toolkit の C ABI `2.0.0`（`dist/1.14.0/ios`）に切り替える。4 機能はまだ移さず、「使えない」に落とす（Dialog と Notification には仮の経路）。どのコミットでも iOS の Player が組めるようにし、段 3 で機能を 1 つずつ移す |
| 前提 | 段 1 の結果（README の `6.3`）: Unity が `Plugins/iOS` の xcframework をリンクし、`.meta` の `AddToEmbeddedBinaries: true` で Embed する。口が足すのは `.mm` だけ。`UserNotifications.framework` は要らない。2.0.0 の framework は `minos 18.0` で、17.x では黙って終わる |
| **実装の時機** | **実機の Notification の回（`test_ios_ui.sh --device --keep-installed`、利用者の準備が要る）の後**。決定 I-12 は「1.x の実機の記録は 3-S の前に」 |

## 1. 変えるもの

### 1.1 xcframework と版の固定（I-4）

- `Plugins/iOS/` の 1.3.0 の 2 つと `.meta` を消し、`dist/1.14.0/ios` の `ios-native-toolkit-2.0.0.xcframework`・`ios-native-toolkit-capi-2.0.0.xcframework` を置く。2 つの `.meta` は iOS だけ有効、iOS の設定に `AddToEmbeddedBinaries: true`（S-2）
- `VERSION.txt` を `dist_version: 1.14.0`、`xcframeworks: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework` にし、頭のコメント（PostBuildProcessor が読む、1.x の Swift の library と Unity の bridge）を今の形に直す。`PreBuildProcessor` の `IsDevelopmentBuildInProgress` の説明も直す

### 1.2 Xcode のプロジェクトに入れる口（I-1・I-2）

- `Editor/iOS/IosXcodeProjectSetup.cs`（`NativeToolkit.Editor`、`IPostprocessBuildWithReport`、`#if UNITY_IOS`）:
  - 書き出したプロジェクトで、2 つの framework が UnityFramework のリンクとアプリの Embed に入っていることを確かめる。無ければ（`.meta` の設定が落ちたなど）分かりやすい文言でビルドを失敗にする（足しはしない）
  - 1.x の framework（`ios-native-toolkit-1.*`、`unity-ios-native-toolkit-*`）の参照が残っていれば止める（1.x で書き出したプロジェクトに Append したとき。F-1: 同じ名前の `IosLibrary.framework` は同じアプリに入れられない）
  - `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` が無ければ、`Editor/iOS/NativeSource~/NtkNotificationInstaller.mm` を書き出したプロジェクトの `Libraries/com.jonghyunkim.nativetoolkit/` に写し、UnityFramework に足す（既にあれば足さない。冪等）。**定義があれば、既にある `.mm` の参照とファイルを外す**（`.mm` 付きで書き出した後に入れない設定にして Append したとき）
  - 前後の数（リンク・Embed・`.mm`）をログに出す
  - 判定（数から「足りている・止める・足す・外す」を決める部分）は Unity の型に依らない純粋な関数にし、EditMode で確かめる
- `NtkNotificationInstaller.mm`: 1a と同じ形（ただし 1a の雛形から、delegate のログ・`kUnityWillFinishLaunchingWithOptions` の経路・`didBecomeActive` の経路を外し、`UserNotifications` を import しない。S-7 で `UserNotifications.framework` が要らないのは、`.mm` がそれを直に使わないとき）。`NSObject<AppDelegateListener>` のクラス `NtkNotificationInstaller` を `+load` で `UnityRegisterAppDelegateListener` に登録し、`didFinishLaunching:` の通知で `ntk_notification_install()` を呼び、戻り値を `NSLog` する（UnityAppController の分類で `application:didFinishLaunchingWithOptions:` を定義すると、本来の実装を置き換えて Unity が起動しない）
- `PostBuildProcessor` の iOS の部分（framework を写してリンクと Embed を足す処理）は消す。macOS の部分は残す。`PreBuildProcessor` の dist からの写しは開発の道具として残す（1.14.0 を写す）
- メニュー「Configure Xcode Project」は段 4 で消す（3-S では触らない）

### 1.3 共通部と版の確かめ（I-14）

- **宣言の置き場所の決まり**（段 3 の各機能も同じ）: C ABI の宣言は、`#if UNITY_IOS || UNITY_EDITOR` のファイル（`IosNativeToolkitCApi` か、機能ごとの CApi のクラス）の、`!UNITY_EDITOR` の外に置く（今の `IosDialogManager.cs`・`IosNotificationManager.cs` はファイルごと `#if UNITY_IOS` なので、そこに置くと iOS 以外の Editor の EditMode で照合が崩れる）
- `Runtime/iOS/Common/IosNativeToolkitCApi.cs`（`#if UNITY_IOS || UNITY_EDITOR`。Android の `AndroidNativeToolkitCApi.cs` と同じ形）: `[DllImport("__Internal")] uint ntk_version()` の宣言は `!UNITY_EDITOR` の**外**に置く（Editor でも組まれ、照合のテストが見られる。呼ぶのは iOS の Player だけ）。`IsAvailable` は iOS の Player で `ntk_version() >> 16 == 2`、それ以外は false。一度だけ読んで覚え、食い違えば 1 回だけ `LogWarning`
- 3-S の時点で使うのは版の確かめだけ

### 1.4 宣言の照合（I-15）

- EditMode のテスト `IosCApiDeclarationTests`（今の EditMode の asmdef、パッケージの中）: 名前空間が `JonghyunKim.NativeToolkit.Runtime.iOS` か、`JonghyunKim.NativeToolkit.Runtime.iOS.` で始まる型の `DllImport("__Internal")` の宣言をリフレクションで集め、(1) 集合がちょうど `{ntk_version}`（数 1。0 では通らない）、(2) どの名前も、パッケージの `Plugins/iOS/<VERSION.txt の capi の名前>/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/*.h` に `NTK_CALL <名前>(` としてあることを照らす。道は `Path.GetFullPath("Packages/com.jonghyunkim.nativetoolkit/...")`（利用者の形では `Library/PackageCache` を指す）
- 宣言は Editor でも組むことを、段 3 の各機能の決まりにする（`!UNITY_EDITOR` の中に置くと照合から消える）。期待の集合は機能を移すたびに足す

### 1.5 最小 iOS（I-16）

- Player Settings の iOS の最小を 18.0 にする（`iOS Profile` はもう 18.0）
- `Editor/iOS/IosMinimumVersionCheck.cs`（`IPreprocessBuildWithReport`、`#if UNITY_IOS`）: ビルドの時点の `PlayerSettings.iOS.targetOSVersionString`（Profile の上書きがあれば、その値が入る見込み）を `System.Version` で 18.0 と比べ（メジャーだけの値は `.0` を補い、読めない値は「最小 iOS が読めない」の文言で止める）、低ければ「native-toolkit 2.0.0 は iOS 18.0 以上。17.x では起動直後に黙って終わる」の文言で止める。比べる関数は EditMode で確かめる（`9.0` < `18.0`、`18` = `18.0`、`17.6.1` < `18.0`、空と読めない値は止める）

### 1.6 ブリッジの宣言を消し、4 機能を「使えない」に落とす（I-11 の案 D）

旧いブリッジの `DllImport` の宣言は **43**（Clipboard 15、Dialog 6、Notification 21、Share 1。属性の数。README のとおり）。どれも消し、ネイティブの呼び出し（`Initialize`・`OnDestroy` の中を含む）も消す。

| 機能 | 3-S の後の iOS の Player の振る舞い（回数・順・時点） |
|---|---|
| Clipboard | 今の Editor の経路（`IsBridgeAvailable()` が false）と同じ。`IsBridgeAvailable()` は iOS の Player でも false。`CLIPBOARD_BRIDGE_UNAVAILABLE`、`<操作> is only available on an iOS device.`。共通のイベント → 受け手の順で、次の `Update` に 1 回ずつ（今の `DispatchRejectedResult` のまま）。引数の確かめ（プラットフォームより前）は今のまま |
| Share | 今の iOS 以外の経路と同じ `iOS share is only available on an iOS device.`。`ShareCompleted` → 受け手の順で、次の `Update` に 1 回ずつ（今の `FireResult` のまま）。項目なしと `null` の確かめは今のまま |
| Dialog | **仮の失敗の経路を足す**（3-S を 4 機能から切り離して確かめるため）。6 つの `Show*` は、今の C# の確かめ（題・本文・選択肢）の後、各イベントを `(null…, false, "Dialog is not available yet on this build.")` で、**同じ呼び出しの中で 1 回**出す（今の C# の確かめの失敗と同じ時点。ID-20〜22 と同じ形）。後から何も来ない |
| Notification | **結果を返すすべての API に仮の経路を足す**。呼び出しごとに受け手を写し（1.x の「操作ごとに 1 つの枠を上書き」はしない）、次の `Update` に `UnityMainThreadDispatcher` で 1 回返す（仮の経路なので今の積み先を使う）。順は 1.x と同じ「受け手 → `NotificationOperationCompleted`」。値: `RequestPermission`・`ShowNotification`・`ScheduleNotification`・`UpdateNotification`・`SetBadgeCount`・`RegisterCategory` は `IosNotificationResult.Failure(<操作>, "Notification is not available yet on this build.")`、`HasPermission` は `false`、`GetAuthorizationStatus` は `Unknown`、`GetScheduledNotifications`・`GetDeliveredNotifications` は `[]`（この 4 つにはイベントが無い）。取り消し・消去・`RemoveCategory`・`OpenNotificationSettings`・`Initialize` は何もしない（delegate は `.mm` が入れる） |

文言は仮のもので、段 3 で各機能を移すときに消える。

### 1.7 テスト

- **定義**: `IosPlayerBuildSetup.ModifyOptions` が `options.extraScriptingDefines` に `NTK_IOS_CABI_SWITCHED` を足す（`IosUiHarnessBuild` の `NTK_UI_HARNESS` と同じ形。Build Profile の Scripting Define Symbols に左右されず、`Assets` の中なので利用者に配らない）。機能を移す段で `NTK_IOS_CABI_2_<FEATURE>` を同じ所に足す。UI のハーネスも `IosUiHarnessBuild` で同じ定義を足す
- **Player のテスト**: 定義は**テストごと**にかける。ネイティブに届くテストだけ `#if !NTK_IOS_CABI_SWITCHED || NTK_IOS_CABI_2_<FEATURE>` で 1.x の期待を残し、C# の確かめのテスト（Dialog の ID-20〜22、Share の SH-20・21・22、Clipboard の CP-20）は 3-S の後も流す。**Clipboard の今の fixture は SetUp と TearDown がネイティブに届く**（`CreatePasteboard`・`RemovePasteboard`。Test Framework は `[Test]` にも `[UnitySetUp]` をかける）ので、クラスごと `#if !NTK_IOS_CABI_SWITCHED || NTK_IOS_CABI_2_CLIPBOARD` で囲み、CP-20 と Clipboard の `UnavailableUntilMoved` は SetUp を持たない別の fixture に置く。各機能に `UnavailableUntilMoved` を足す（`#if NTK_IOS_CABI_SWITCHED && !NTK_IOS_CABI_2_<FEATURE>`）: 1.6 の値を全文で照らし、回数・順・時点も照らす（Dialog は 6 つとも呼び出しの中で 1 回、1 秒待って後から来ない。ほかは呼び出しの直後は 0 回、次の `Update` の後に 1 回ずつ、1 秒待って増えない。Notification は結果の API をすべて、同じ API を続けて 2 回呼んでそれぞれ 1 回）
- **版のテスト**: `IosCApiVersionPlayerTests.VersionIsTwo`（`ntk_version() == 0x020000`、`IsAvailable == true`）
- **`.mm` の確かめ**: 確かめの native（`Assets/NativeToolkitIosPlayerTests/NativeSource~/NtkPlayerTestsProbe.mm`）を、定義を足すのと同じ条件（`-ntkIosPlayerSdk` のとき）で、書き出した後（`IPostprocessBuildWithReport`）に書き出したプロジェクトに足す（ハーネスの overlay と同じ形）。呼ぶ側のテストは `Assets/NativeToolkitIosPlayerTests/Runtime`（iOS だけの asmdef）に置く（パッケージの `Tests` に置くと、確かめの native の無いビルドで Undefined symbol になる）。中身は `UNUserNotificationCenter.currentNotificationCenter.delegate` のクラス名を返す関数（`NSClassFromString` で引き、`UserNotifications` を直にリンクしない）。テストは、入れない設定が無ければクラス名が 1a の `IosLibraryCApi.NotificationDelegateProxy`（実測で決め直す）、あれば `NULL` かそれ以外であることを照らす（Player のコンパイルにもこの定義が入る。S-5）
- **UI のテスト**: 移していない機能の UI のテストは既定で流さない。`DeviceRequirements.json` に `notMovedYet`（クラス名の一覧。3-S で 4 つ、機能を移すたびに外す）を足す。効く範囲: `ios_ui_runner.py tests` の既定と `--device` の一覧から外す。`--keep-installed` は `keepInstalled` と重なれば理由の文言で止める。`--tests` で名指しすれば「not moved yet」の文言で拒む。`check_requirements` でクラス名の実在を確かめる（doctest を足す）。`test_ios_ui.sh` は一覧が空なら、ビルドの前に「流せる UI テストが無い（すべて notMovedYet）」の文言で止める（空の名前を配列に入れない）。Dialog・Share・Clipboard の各シナリオにも `[abi@event] 1|2` の行を足す（Notification は済み）
- **EditMode と Editor の PlayMode**: `IosCApiDeclarationTests`、`IosXcodeProjectSetup` の判定の関数、`IosMinimumVersionCheck` の比べる関数のテストを足す
- **利用者の条件**: `verify_unity_ios.sh --as-package-user`（`Test iOS Profile`、`NATIVETOOLKIT_ENABLE_BUILD_STEPS` 無し）の拒みを 3-S で外す

## 2. 確かめ

実機の回は、シミュレーターで全件成功してから流す（README の段 3 の行の決まり）。

| # | 確かめること | どこで |
|---|---|---|
| V-1 | EditMode、Editor の PlayMode が通り、`IosCApiDeclarationTests` と判定・比べる関数のテストが組まれて流れた。iOS 以外の EditMode（`verify_unity_android.sh` の EditMode）でも `IosCApiDeclarationTests` が通る | `verify_unity_ios.sh`、`verify_unity_android.sh` |
| V-2 | iOS の書き出し（IL2CPP）と Xcode のビルドが通り、口のログでリンク 2、Embed 2、`.mm` 1 | シミュレーター |
| V-3 | iOS の Player のテスト: `VersionIsTwo`、`.mm` の delegate、4 機能の `UnavailableUntilMoved`、C# の確かめのテストが通る。`iOS Profile` と `Test iOS Profile`（`--as-package-user`）の両方 | シミュレーター、実機（許可を得て） |
| V-4 | 入れない設定を使う Profile の YAML に入れると、口のログで `.mm` 0、`.mm` の確かめは delegate が入っていない | シミュレーター |
| V-5 | 最小 iOS を 17.0 にすると `IosMinimumVersionCheck` がビルドを止める。Player Settings と Profile の上書きの両方で（どちらも戻す） | Editor の CLI |
| V-6 | Append: 同じ出力先に 2 回書き出しても `.mm` が 1。`.mm` 付きの後に入れない設定で Append すると `.mm` が外れる | Editor の CLI と Xcode |
| V-7 | `.meta` から `AddToEmbeddedBinaries` を外して書き出すと、口が文言でビルドを止める（戻す） | Editor の CLI |
| V-8 | I-17: 使い捨てのプロジェクトに `git+file:///<このリポジトリ>?path=Packages/com.jonghyunkim.nativetoolkit#<3-S のコミット>` で入れ、コンパイル、iOS の書き出し、Embed と `.mm`、シミュレーターで起動（`ntk_version`）。実機でも起動（許可を得て） | シミュレーター、実機 |
| V-9 | Android と macOS のビルドの処理に影響が無い（`PostBuildProcessor` の macOS の部分、Android の書き出し） | Editor の CLI |
| V-10 | ハーネスの書き出しだけの確かめ（`IosUiHarnessBuild.Export` とシミュレーター向けの xcodebuild。UI のテストは流さない）。`test_ios_ui.sh` が「すべて notMovedYet」で止まる | シミュレーター |

## 3. コミットの分け方

- 3-S は 1 つのコミットにする（xcframework を替えると旧い宣言はリンクできず、`.mm` は 1.x にリンクできないので、途中で組めない形を作らない）。文書は別のコミット
- その前に、xcframework に関わらない足し物（`IosMinimumVersionCheck` と Player Settings の 18.0、ランナーの `notMovedYet`）を 1 つ前のコミットにしてもよい（1.x でも組める）

## 4. 決めないこと・後で決めること

| もの | どこで |
|---|---|
| 各機能の写し方（I-5〜I-9） | 段 3 の機能ごとの設計 |
| メニュー「Configure Xcode Project」と `IosFrameworkPatcher` の扱い | 段 4 |
| native-toolkit への結果の返し | 3-S の終わりに、利用者に聞いてから |

## 5. 段 4 に送るもの

- 移行ガイド: 1.x で書き出した Xcode のプロジェクトには Append せず、作り直す（口が止める）。入れない設定は Player Settings に入れたものを Profile で外せない（S-5）

## 6. v1 からの変更の一覧

| 指摘 | 変更 |
|---|---|
| サブエージェントの R-1、Codex の R1 | テストの定義は `IosPlayerBuildSetup` の `extraScriptingDefines` |
| R-2、R-17 | `.mm` は `AppDelegateListener` と `+load`、接頭辞のクラス名、`Libraries/` に写す |
| R-3、R-4、Codex の R3・R4 | ヘッダーはパッケージの framework から。宣言は Editor でも組み、集合がちょうど `{ntk_version}` |
| R-5 | `--as-package-user` と `Test iOS Profile` |
| R-6、Codex の R6 | Append の冪等と、入れない設定の後の `.mm` を外す（V-6） |
| R-7、Codex の R5 | `.mm` の確かめはテストの側の native で delegate のクラス名を読む |
| R-8、Codex の R2 | 「使えない」の回数・順・時点を表に。Notification は呼び出しごとの受け手 |
| R-9 | 最小 iOS は `System.Version`、Profile の有り無しで確かめる |
| R-10 | 判定・比べる関数の EditMode のテスト、`AddToEmbeddedBinaries` を外したときに止まること（V-7） |
| R-11 | シミュレーターで全件成功してから実機 |
| R-12 | 定義はテストごと。C# の確かめのテストは残す |
| R-13 | `notMovedYet` |
| R-14、Codex の R7 | 数は 43 |
| R-15 | 古くなる記述、I-14 の `LogWarning` |
| R-16 | 1.x の framework の参照が残れば止める、移行ガイドへ |

## 7. v2 からの変更（止めた後の直し）

| 指摘 | 変更 |
|---|---|
| レビュー v2 のサブエージェントの R-1、Codex の R2-1 | Clipboard の fixture をクラスごと定義で囲み、CP-20 と `UnavailableUntilMoved` を別の fixture に。SH-22 を残す一覧に |
| R-2、Codex の R2-2 | `notMovedYet` の効く範囲と、一覧が空のときの止め方 |
| R-3 | V-10（ハーネスの書き出しだけの確かめ） |
| R-4 | `.mm` の確かめの native を足す時点と、呼ぶ側のテストの置き場所 |
| R-5 | 宣言の置き場所の決まりと、iOS 以外の EditMode での照合（V-1） |
| R-6 | 集める名前空間 |
| R-7 | 最小 iOS の値の読み方とテストの値 |
| R-8 | `.mm` で 1a から外すもの |
