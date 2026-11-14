# iOS の下の層が C ABI 2.0.0（`IosLibraryCApi`）に置き換わり、ブリッジを P/Invoke に書き直す

- 記録日: 2026-11-14
- 分類: 横断課題（移行）。**機能追加ではない。** clipboard / dialog / notification / share の 4 機能と、iOS のビルドの処理にまたがる
- 発見経緯: native-toolkit の段階 2c の申し送り（`artifact/topics/ios-c-abi/results/2026-11-07-ios-c-abi-stage2c-handoff.md`、2026-11-14 改訂）。iOS 2.0.0 で Unity 向けのブリッジ（`unity-ios-native-toolkit-*.xcframework`、`UnityIosPlugin`）を廃し、汎用の C ABI（`ios-native-toolkit-capi-2.0.0.xcframework`、`IosLibraryCApi.framework`）にした。配布物は native-toolkit の `dist/1.14.0/ios`（`3aeeaf3c`）
- 対応方針: 各 Manager の公開 API を保ち、下の層（ブリッジの 43 関数と JSON）だけを C ABI（`ntk_*`、134 関数）の P/Invoke に置き換える。進め方は [android-c-abi-2](../android-c-abi-2/README.md) に倣うが、**4 機能を一度に切り替える**（1.x と 2.0.0 の `IosLibrary.framework` は同じ名前で、同じアプリに入れられない。native-toolkit の README D-12、D-20）
- 進捗: **方針を決めた（2026-11-14）。次は段 1（スパイク）。** 段 0（移す前の基準）は済み（6.1）。進め方は設計書 [designs/2026-11-14-ios-c-abi-2c-plan-design-v4.md](designs/2026-11-14-ios-c-abi-2c-plan-design-v4.md)（v1〜v3 とレビュー 2 回の後、利用者の判断を書き込んだ版）。**Android の移行とほぼ同じ方針**。レビュー v1 で見つかった、利用者の環境で壊れていたもの 3 件は先に直した（6.2）
- **2c の成功が、native-toolkit の次のリリース（iOS 2.0.0）の条件**。native-toolkit は 2c が終わるまで出さない
- 対象: `Runtime/{Clipboard,Dialog,Notification,Share}/Ios*.cs`、`Runtime/UI/iOS/`、`Editor/Build/`（`PreBuildProcessor`、`PostBuildProcessor`、`Tools/iOS/`）、`Editor/iOS/`、`Plugins/iOS/`、対応するテストとサンプル
- ブランチ: `feature/UNT-14`（利用者の指示。1a の下調べと、dist/1.14.0 の前の固定 `95905d1` と同じブランチ）
- 相手側: native-toolkit `artifact/topics/ios-c-abi/`。問い合わせと結果の報告は native-toolkit のセッションへ
- 前段: [ios-c-abi-handoff](../ios-c-abi-handoff/README.md)（1a の Unity と実機での下調べ、dist/1.14.0 の前に入れた固定）

---

## 1. 何が変わるか

申し送りの 1 章の要約。

| | 1.x（今） | 2.0.0 |
|---|---|---|
| 呼ぶもの | ブリッジの 43 関数（`DllImport("__Internal")`。Clipboard 15、Dialog 6、Notification 21、Share 1。`[DllImport(` の行を数えた） | `ntk_*`（134 関数。同じく `__Internal`） |
| 結果 | `const char*`（JSON か文言）。`user_data` は無い | C の関数ポインタ（`[MonoPInvokeCallback]`）、構造体、**所有権ごと渡すハンドル**。登録ごとに `user_data` と `release` |
| JSON | C# とブリッジの間はすべて JSON | 残るのは Notification の公開 API、userInfo、`ntk_clipboard_detect_values_async` の結果の 3 つ |
| リスナー | 差し替え（static の上書き） | 足す・外す。差し替えは包みが保つ |
| 通知の delegate | `IosNotificationManager.Initialize()`（`Awake`）が `notificationSetup` を呼ぶ | `.mm` が `didFinishLaunching` で `ntk_notification_install` を呼ぶ |
| xcframework | `ios-native-toolkit-1.3.0` と `unity-ios-native-toolkit-1.3.0` | `ios-native-toolkit-2.0.0` と `ios-native-toolkit-capi-2.0.0`（両方を Embed & Sign。`-debug` は無い） |
| ヘッダー | 無い | 公開ヘッダー（C99）。C# の宣言はこれから書いて照らす |

## 2. 参照

native-toolkit のパス（このマシン上。`feature/NTKIT-18`）。

| 見るもの | 場所 |
|---|---|
| **こちらへの申し送り** | `artifact/topics/ios-c-abi/results/2026-11-07-ios-c-abi-stage2c-handoff.md` |
| ブリッジの 43 関数と C ABI の対応（「Unity の包みが保つこと」の列） | `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 |
| 包みに渡す事柄 | 同 16 章（申し送りの 4 章に書き直しがある） |
| スレッド、寿命、`release`、通知の delegate | `designs/2026-10-31-ios-c-abi-c-abi-design.md`（第 1 部）1.3、1.4、5.7、5.12 |
| 構造体の配置の期待値 | `ios/IosLibraryCApiTests/StructLayoutTests.m` |
| 配布物 | `dist/1.14.0/ios/`（xcframework 2 つ、`include/NativeToolkitC/` の 5 つ） |
| C から呼ぶ手本 | `ios/IosLibraryCApiSmoke/` |
| Unity と実機での下調べの結果 | このリポジトリの `artifact/topics/ios-c-abi-handoff/results/` |

## 3. Android と違うところ（写してはいけないもの）

申し送り 4 章と 1a の結果の要点。各機能の設計書はこれを前提にする。

| # | Android | iOS |
|---|---|---|
| B-1 | ライブラリ名 `"ntk"` | `"__Internal"`（UnityFramework にリンクした `ntk_*` を引く） |
| B-2 | コールバックは Android の main（UnityMain ではない）。列が必須 | **Unity と同じ main**。完了は関数が戻った後に来る（1a の U-1）。列は必須ではない |
| B-3 | 初期化の確かめ `ntk_android_is_initialized` | 無い。代わりに `.mm` の `ntk_notification_install` |
| B-4 | 1.x と 2.0.0 の AAR は 1 つの APK に同居できた（Android の 6.2） | **同居できない**。4 機能を一度に切り替える |
| B-5 | 32 ビット・Mono は対象外 | iOS は IL2CPP と 64 ビットだけ（もともと）。最小 iOS は 18.0 |
| B-6 | エラーの値・構造体の配置 | Android と同じ名前でも値や配置が違うものがある（申し送り 4.3）。期待値は iOS の `StructLayoutTests.m` から取る |
| B-7 | コールバックの例外 | 投げるとアプリが落ちる（1a の U-9。`Il2CppExceptionWrapper` で `std::terminate`）。**コールバックの本体は必ず try / catch** |

## 4. 決定

設計書 v4 の I-1〜I-17（2026-11-14）。要点だけを写す。理由と細部は設計書。

| # | 事柄 | 決定 |
|---|---|---|
| I-1 | xcframework を入れる口 | `NativeToolkit.Editor` の `IPostprocessBuildWithReport`（`Editor/iOS/IosXcodeProjectSetup.cs`）。利用者のビルドでも動く。道は `Path.GetFullPath("Packages/...")`。Xcode の API は `#if UNITY_IOS`。冪等。Unity が自分でリンクするか、`PluginImporter` で Embed できるかは段 1 で確かめて決める |
| I-2 | `.mm` と入れない設定 | `.mm` は `Editor/iOS/NativeSource~/` に持ち、I-1 の口が UnityFramework に足す。入れない設定は定義シンボル `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`（Editor のコンパイルの `#if` で読む） |
| I-3 | 入れ直し | 公開の `Initialize()` を入れ直しの口にする（`Awake` は受け手の登録だけ）。通知パッケージの `AuthorizationRequest` の後に呼ぶとマニュアルに書く |
| I-4 | 版の固定 | `Plugins/iOS/VERSION.txt`（`95905d1`）。3-S で 1.14.0 と 2.0.0 の 2 つに |
| I-5・I-6 | 写し方・保つ範囲 | Android の J-6・J-11 と同じ（ふつうの使い方を保つ） |
| I-7 | 完了の配送 | 受けて積み、次の `Update` で出す（Android の J-9 と同じ。1.x と同じ時点）。出るのは来た順 |
| I-8 | 公開の JSON Builder | `IosClipboardJsonBuilder`・`IosShareJsonBuilder` は段 4 で消す。`IosNotificationJsonBuilder` は残す |
| I-9 | 内部の JSON | 機能ごとの設計で決める |
| I-10 | フォルダと 5 型 | 3-0 で `Runtime/iOS/<Feature>/` に移す（`[MovedFrom(true, …)]`）。**接頭辞の無い 5 型は案 2**（段 4 で iOS 用の新しい型を作って替える。旧い型は macOS 用に残す。iOS の利用者には破壊的変更。利用者の判断） |
| I-11 | 切り替えの順 | **案 D**: 3-S で先に切り替え、移していない機能は「使えない」に落とし（Dialog と Notification には仮の経路）、1 つずつ移す（利用者の判断） |
| I-12 | 1.x の記録と UI の自動化 | **段 2 で、ほぼ自動の UI テストを作る**（Unity のシナリオの口 + XCUITest）。1.x の記録は 3-S の前に（利用者の判断） |
| I-13 | Player のテストの仕組み | `scripts/verify_unity_ios.sh`。Simulator.app を開かない。シミュレーターは Simulator SDK と arm64 |
| I-14 | 共通部 | `Runtime/iOS/Common/IosNativeToolkitCApi.cs`。major が 2 でなければ「使えない」 |
| I-15 | 宣言の照合 | 宣言と構造体を Editor でも組み、名前をヘッダーと照らす（数も確かめる） |
| I-16 | 最小 iOS | 18.0。3-S で Player Settings を直し、低ければビルドを止める確かめを置く |
| I-17 | 利用者の形での確かめ | 段 1・3-S・4 の終わりに、git の URL で入れた形で確かめる |

## 5. 進め方

設計書 v4 の 3 章。

| 段 | 中身 | 状態 |
|---|---|---|
| 0 | 移す前の基準（EditMode、Editor の PlayMode） | 完了（6.1） |
| 1 | スパイク（写しで）: 2.0.0 を入れる口と `.mm`、Unity の扱い、定義の効き方、最小 iOS、Xcode の版、git の URL の形。シミュレーターと実機で `ntk_version` と起動と `.mm` | 未着手 |
| 2 | `verify_unity_ios.sh`、Editor 専用の PlayMode のテストを分ける、1.x の Player の基準、**UI の自動化の仕組み**（設計 → レビュー → 実装） | 未着手 |
| 3-0 | 4 機能の型を `Runtime/iOS/<Feature>/` に移す（5 型は動かさない） | 未着手 |
| 3-R | 機能ごとの確かめる項目の表 → レビュー → 1.x のまま実機で記録（4 機能） | 未着手 |
| 3-S | 切り替え（2.0.0、ブリッジの宣言を消す、「使えない」に落とす） | 未着手 |
| 3 | 機能ごとに: 写し方の設計 → レビュー → 実装 → 実機で 1.x と比べる → 実装のレビュー。Dialog → Share → Clipboard → Notification | 未着手 |
| 3-E | 署名し直しの確かめ（Validate App は Personal Team ではしない） | 未着手 |
| 4 | JSON Builder 2 つの削除、5 型の iOS 用の新しい型、メニュー、マニュアル、リリース 1.14.0 | 未着手 |

## 6. 記録

### 6.1 移行前の基準（2026-11-14、`95905d1`）

**移行前の基準: `95905d1`**（`feature/UNT-14`。リリース 1.13.0 の後に、1a の記録と iOS の xcframework の固定を足したもの。iOS の振る舞いは 1.13.0 と同じ）。

| 何を | 結果 | 条件 |
|---|---|---|
| EditMode | 1419 件中 1418 件が通過。**iOS の 234 件はすべて通過**（1.x の 212 件と、`95905d1` で足した `IosXcframeworkPinTests` 22 件） | macOS、Unity 6000.4.2f1、`-buildTarget iOS`。通らなかった 1 件は `WindowsClipboardSampleFixtureTests.TemporaryFilePathsAreAbsoluteAndUseBackslashes` の skip（Windows の Editor だけで確かめるテスト） |
| Editor の PlayMode | 300 件すべて通過。**iOS の 36 件**（`IosClipboardManagerIntegrationTests` 31、`IosShareManagerIntegrationTests` 5）を含む | 同上 |

iOS の EditMode の内訳（1.x の 212 件）:

| ファイル | 件数 |
|---|---|
| `IosClipboardJsonParserTests` | 46 |
| `IosClipboardJsonReaderTests` | 34 |
| `IosClipboardJsonBuilderTests` | 30 |
| `IosClipboardSampleStateTests` | 29 |
| `IosClipboardResultTests` | 21 |
| `IosClipboardManagerDispatchTests` | 17 |
| `IosShareJsonBuilderTests` | 16 |
| `IosClipboardSampleSceneWiringTests` | 7 |
| `IosShareManagerDispatchTests` | 6 |
| `IosShareResultTests` | 6 |

- Dialog と Notification の EditMode・PlayMode のテストは無い
- iOS の Player（シミュレーター・実機）でのテストは無い。1.x の実機での振る舞いの記録は、段 0 の残り（設計書 v1 の 3 章）

### 6.2 git の URL で入れた利用者の環境で壊れていたもの（2026-11-14）

設計書 v1 のレビュー（Codex とサブエージェント）の指摘を、使い捨てのプロジェクトに `git+file:///<このリポジトリ>?path=Packages/com.jonghyunkim.nativetoolkit#<コミット>` で入れて確かめた。このリポジトリの中では、パッケージが `Packages/` に埋め込まれていて、どちらも起きない。

| # | 何が | どの版で | 直し |
|---|---|---|---|
| U-1 | `NativeToolkit.Editor` のアセンブリが組まれない。asmdef の `"optionalUnityReferences": ["TestAssemblies"]` を Unity は `UNITY_INCLUDE_TESTS` の条件にし（UnityCsReference 6000.4 の `CustomScriptAssembly.cs`）、テストの対象でないパッケージでは組まない。中の `AndroidGradleDependencies`（Gradle の依存の追加）と `AndroidArchitectureCheck` が利用者の Android のビルドで動かない見込み（実機では確かめていない） | **1.13.0 から**（タグ 1.13.0 で `NativeToolkit.Editor.dll` が無いことを確かめた） | その行を消した。アセンブリは NUnit もテストの API も使っていない |
| U-2 | `95905d1` で `NativeToolkit.BuildTools.Editor` が `NativeToolkit.Editor` を参照したので、U-1 のため `IosFrameworkPatcher` が `CS0246` で止まり、利用者のプロジェクトがスクリプトのエラーになる | `95905d1`（未 push、未リリース） | U-1 の直しで解けた |
| U-3 | メニュー「Configure Xcode Project」（iOS）が、xcframework を `<プロジェクト>/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS` で探す。git の URL の利用者ではパッケージは `Library/PackageCache/...` にあり、その道は無い | **1.x から**（タグ 1.13.0 の `IosFrameworkPatcher.cs:37`。macOS の `MacFrameworkPatcher.cs:37` も同じ） | iOS だけ `Path.GetFullPath("Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS")`（Unity が実際の場所に解く）にした。**macOS は範囲の外で残っている** |

- 直した後の確かめ: このリポジトリの EditMode 1419 件中 1418 件成功（1 件は Windows だけのテストの skip）。git の URL で入れた使い捨てのプロジェクトで、iOS と Android のどちらでも 3 つのアセンブリが組まれ、エラーは 0 件。iOS を書き出してメニューの処理（`IosFrameworkPatcher.Apply`）を呼ぶと、`Library/PackageCache` の 2 つの xcframework を足し、Embed & Sign の印が付いた
- **1.13.1 は出さず、1.14.0（2c の後のリリース）で直す**（2026-11-14、利用者の判断）。1.14.0 のリリースの前に、git の URL で入れた形で Android と iOS を実機で通す（実機は許可を得て）
