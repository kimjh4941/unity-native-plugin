# 段 3-S（native-toolkit 2.0.0 への切り替え）の記録 v1

- 記録日: 2026-11-15
- 設計: [../designs/2026-11-15-ios-c-abi-3s-switch-design-v3.md](../designs/2026-11-15-ios-c-abi-3s-switch-design-v3.md)
- 親: [README](../README.md) の段 3-S
- コミット: `3cae23e`（切り替え）、`ea824a8`・`c5c8b7c`・`798be89`（確かめの `.mm` のテストの直し）

## 1. 作ったもの

| もの | 中身 |
|---|---|
| `Plugins/iOS/` | 1.3.0 の 2 つを消し、dist 1.14.0 の `ios-native-toolkit-2.0.0.xcframework`・`ios-native-toolkit-capi-2.0.0.xcframework` を置いた（dist と同一）。`.meta` は iOS だけ有効、`AddToEmbeddedBinaries: true`。`VERSION.txt` は `dist_version: 1.14.0` |
| `Editor/iOS/IosXcodeProjectSetup.cs`・`IosXcodeProjectPlan.cs` | 書き出した後、2 つの framework のリンクと Embed がそれぞれ 1 回かを照らし、1.x の framework が残っていれば止める。`NtkNotificationInstaller.mm` を `Libraries/com.jonghyunkim.nativetoolkit/` に写して UnityFramework に足す（冪等）。`NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` があれば足さず、前に足したものは外す。判定は純粋な関数で EditMode のテストあり |
| `Editor/iOS/NativeSource~/NtkNotificationInstaller.mm` | `+load` で `AppDelegateListener` を登録し、`didFinishLaunching:` で `ntk_notification_install()` を呼んで戻り値を `NSLog` |
| `PostBuildProcessor`・`PreBuildProcessor` | iOS の部分（framework を写してリンクと Embed を足す）と `IsDevelopmentBuildInProgress` を消した。macOS の部分は残した |
| `Runtime/iOS/Common/IosNativeToolkitCApi.cs` | `ntk_version` の宣言（Editor でも組む）、`IsSupportedVersion`、`IsAvailable`（一度だけ読み、食い違えば 1 回だけ警告） |
| 4 機能の Manager | 旧いブリッジの `DllImport` 43 個と、ネイティブの呼び出し（`Initialize`・`OnDestroy` の中も）を消した。Clipboard は Editor と同じ `CLIPBOARD_BRIDGE_UNAVAILABLE`、Share は `iOS share is only available on an iOS device.`、Dialog は呼び出しの中で `Dialog is not available yet on this build.`、Notification は次の `Update` に固定の値（設計 1.6 の表のとおり） |
| テスト（EditMode） | `IosCApiDeclarationTests`（宣言の集合がちょうど `{ntk_version}`、名前がパッケージの見出しにある）、`IosXcodeProjectPlanTests` |
| テスト（Player） | `IosPlayerBuildSetup` が `NTK_IOS_CABI_SWITCHED` と `NTK_IOS_PLAYER_PROBE` を足す。ネイティブに届くテストは `#if !NTK_IOS_CABI_SWITCHED \|\| NTK_IOS_CABI_2_<FEATURE>`（Share の SH-23〜25、Clipboard と Notification は fixture ごと）。各機能の `UnavailableUntilMoved`、`IosCApiVersionPlayerTests.VersionIsTwo`、`IosNotificationDelegateProbeTests`（`Assets/NativeToolkitIosPlayerTests/Probe`、確かめの native は `NtkPlayerTestsProbe.mm` を `IosPlayerProbeInstaller` が書き出した後に足す） |
| UI のテスト | `DeviceRequirements.json` の `notMovedYet`（4 クラス）。`ios_ui_runner.py`: 既定と `--device` の一覧から外す、`--keep-installed` は重なれば止める、承認などは名指しを拒む、`not-moved` の下位命令、`check_requirements` でクラスの実在を照らす（doctest あり）。`test_ios_ui.sh`: 名指しは「Not moved yet」で拒み、一覧が空ならビルドの前に止める。ハーネスの Dialog・Share・Clipboard にも `[abi@event] 1\|2`、ハーネスのビルドにも `NTK_IOS_CABI_SWITCHED` |
| `verify_unity_ios.sh` | `--as-package-user`（`Test iOS Profile`、ビルドの段なし）を使えるようにした |

### 1.1 設計と違えたところ

| 所 | 設計 | 実際 | 理由 |
|---|---|---|---|
| `.mm` の確かめのテストの置き場所 | `Assets/NativeToolkitIosPlayerTests/Runtime`（iOS だけの asmdef） | 新しい asmdef `NativeToolkit.IosPlayerTests.Probe`（すべての環境で組み、iOS の Player で `NTK_IOS_PLAYER_PROBE` のときだけ中身がある）と、NUnit を参照し続けるための型 | Test Framework は、Player で探すテストの assembly を **Editor に読み込まれたテストの assembly の名前**から決める（`RuntimeTestLauncherBase`）。iOS だけの asmdef は Editor に無いので、1 回目はテストが流れなかった（13 件）。次に、Editor で中身が全部消える assembly は NUnit を参照しないので、テストの assembly と見なされなかった（`EditorLoadedTestAssemblyProvider`。2 回目も 13 件） |
| `NTK_IOS_PLAYER_PROBE` | 無し（`-ntkIosPlayerSdk` のときに `.mm` を足す、とだけ） | 定義を足した | 確かめの native の無いビルド（Test Runner の窓から iOS の Player のテストを流すなど）でリンクが通るように |
| delegate のクラス名 | `IosLibraryCApi.NotificationDelegateProxy`（実測で決め直す） | `IosLibrary.NotificationDelegateProxy` | 実測。proxy は Swift の `IosLibrary` の module にある |
| Notification の Player のテストのかけ方 | テストごと | fixture ごと | すべてのテストがネイティブに届く（Clipboard と同じ形）。`UnavailableUntilMoved` は別の fixture |
| Dialog の公開の delegate 型、Notification の 2 つのイベント | 記載なし | 残した（Notification のイベントは CS0067 を抑える） | 公開の API を 3-S で壊さない。段 3 で移すときに決める |

## 2. 確かめたこと（2026-11-15、シミュレーターは iPhone 16 / iOS 18.3）

| # | 結果 |
|---|---|
| V-1 | `verify_unity_ios.sh`: EditMode 1448 件（成功 1447、既にあった 1 件の skip）、Editor の PlayMode 300 件成功。`Android™ Profile` の EditMode も 1448 件（同じ）で、`IosCApiDeclarationTests` の 7 件が成功 |
| V-2 | 書き出しと Xcode のビルドが通った。口のログ `linked and embedded: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework; installer entries: 1`。`project.pbxproj` でもリンク 1・Embed 1（2 つとも）、`.mm` 1 |
| V-3 | iOS の Player のテスト 14 件がすべて成功（`VersionIsTwo`、delegate の確かめ、4 機能の `UnavailableUntilMoved`、Dialog の ID-20〜22（4 件）、Share の SH-20〜22、Clipboard の CP-20）。`iOS Profile` と `Test iOS Profile`（`--as-package-user`）の両方。起動時のログ `ntk_notification_install -> 0`。**実機（iPhone XS / iOS 18.7.2、`iOS Profile`）でも 14 件すべて成功**（delegate は proxy、`ntk_version` は `0x020000`。アプリは消えたことを読み直した） |
| V-4 | `iOS Profile` に入れない設定を入れて Append すると、口のログ `installer entries: 0 (skip: True)`、`.mm` の参照もファイルも外れた。新しく書き出しても 0。`Test iOS Profile` に入れて Player のテストを流すと 14 件成功し、delegate は `<none>`（`ntk_notification_install` のログも出ない） |
| V-5 | 最小 iOS 17.0 で、Player Settings（API で設定）と `iOS Profile` の上書きの両方で、`IosMinimumVersionCheck` が「needs iOS 18.0 or later」の文言でビルドを止めた（どちらも戻した） |
| V-6 | 同じ出力先に Replace → Append で `.mm` 1、リンク 1、Embed 1 のまま。入れない設定で Append すると `.mm` が外れた（V-4） |
| V-7 | capi の `.meta` から `AddToEmbeddedBinaries` を消して書き出すと、「`ios-native-toolkit-capi-2.0.0.xcframework is linked 1 and embedded 0 time(s)`」の文言で止まった（戻した） |
| V-8 | 使い捨てのプロジェクト（Unity 6000.4.2f1 の新規、scratchpad）に `git+file:///<このリポジトリ>?path=Packages/com.jonghyunkim.nativetoolkit#798be89…` で入れた（`Library/PackageCache/com.jonghyunkim.nativetoolkit@…`）。コンパイル、iOS の書き出し（口のログでリンク 2、Embed 2、`.mm` 1）、シミュレーター向けのビルドが通り、起動すると `[I17] ntk_version=0x020000`（プロジェクトの側で `ntk_version` を宣言して呼んだ）と `ntk_notification_install -> 0` が出た。アプリは消した。実機は、承認の仕組み（`ios_ui_runner.py`）が Player のテストと UI のテストのアプリしか扱わないので流していない（3 章） |
| V-9 | `Android™ Profile` で Android の書き出しが成功。`macOS Profile` で macOS のビルドが成功し、`PostBuildProcessor` の macOS の部分が今までどおり動いた（XCFramework を選び、`.app` のビルドなので Xcode のプロジェクトは無いと出す）。macOS のビルドの段が写した framework は戻した |
| V-10 | `IosUiHarnessBuild.Export`（シミュレーター）とシミュレーター向けの xcodebuild が成功（口のログは V-2 と同じ）。`test_ios_ui.sh` は「No UI test can run: every UI test class is in notMovedYet」でビルドの前に止まった（終了コード 2）。`--tests DialogUITests/test_ID1` は「Not moved yet」で拒み、`--device --dry-run --keep-installed` は notMovedYet の文言で止まった |

### 2.1 途中で起きたこと

- `IosXcodeProjectPlanTests` が CS0051 でコンパイルできなかった（internal の enum を public のテストの引数にした）。期待を名前の文字列で渡す形に直した（`3cae23e` の前）
- `--as-package-user` の 1 回目で、Editor の PlayMode の起動中に Unity が落ちた（終了コード 139、Burst のコンパイルの中の SIGSEGV。`Burst.Compiler.IL.Server.SharedLibraryCompilationState:CreateResult`）。Profile を切り替えた直後の 1 回だけで、同じ条件の 2 回目は 300 件成功した。パッケージの変更とは関係ない
- 実機の 1 回目は、署名つきの Xcode のビルドが失敗した（エラーは 1 行。実機の回はログを出さずに消すので、中身は分からない）。端末には何も入っていない。同じ書き出しを端末に触れずに作り直すと、署名なしでも署名つき（自動署名）でもビルドが通ったので、その時点だけの失敗（自動署名か keychain）とみなし、新しい承認で流し直して成功した
- `verify_unity_ios.sh` は `Plugins/iOS` に commit していない変更があると始まらない（ビルドの段がそのファイルを置き換えるため）。切り替えを先に commit してから流した

## 3. まだ確かめていないこと

- 実機の V-8（git の URL で入れた形での起動）。今の承認の仕組みは決まったアプリ（Player のテスト、UI のハーネス）しか扱わない。使い捨てのアプリを実機に入れるには、承認の一覧にそのアプリを足す手当てが要る。利用者の判断を仰ぐ（推奨: 実機の V-3 は同じ framework と `.mm` を同じ口で入れて起動しているので、段 4 の I-17 の確かめでまとめて行う）→ 済み（2026-11-22、I-17 の G-7。使い捨てのアプリを承認の仕組みに足し〈`GitUrlApp/IosLaunch`、`f92dbd5`〉、iPhone XS で起動した。[I-17 の結果](2026-11-22-ios-c-abi-i17-git-url-result-v1.md) の 2 章）
