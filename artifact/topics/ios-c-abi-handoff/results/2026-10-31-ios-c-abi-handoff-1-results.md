# iOS の C ABI: native-toolkit の申し送り 1 回目の結果（U-1・U-2・U-5・U-8・U-9・U-10）

- 記録日: 2026-10-31
- 依頼元: native-toolkit の `artifact/topics/ios-c-abi/results/2026-10-31-ios-c-abi-unity-handoff-1.md`（セッション native-toolkit-75 から）
- このリポジトリ: `feature/UNT-14`、`30459b6` の上（この結果はまだコミットしていない）
- 雛形: `ios/Build/stage1a-handoff1/` の `BUILD-INFO.txt` が `built: 2026-10-31T05:25:42Z`、`commit: fbb03f70 (with uncommitted probe or IosLibrary changes)`。ASan の版（`asan/`）は `built: 2026-10-31T05:26:29Z`、同じ commit
- Unity 6000.4.2f1（IL2CPP、Release、Development Build なし）
- アプリを組んだ Xcode: 26.3（17C529）。ASan の版だけ 26.6（17F113）。理由は 4 章の U-10
- 端末: iPhone XS / iOS 18.7.2（利用者の私物。`native-toolkit/agent-rules/device-testing.md` に従い、段階ごとに 4 項目を示して許可を得た）。シミュレーターは iPhone 16 / iOS 18.3

## 1. 要約（native-toolkit への返事）

```
handoff-1 results (unity-native-plugin, 30459b6 + uncommitted results on feature/UNT-14)
probe: built 2026-10-31T05:25:42Z (asan 05:26:29Z), commit fbb03f70+uncommitted   Unity 6000.4.2f1   Xcode 26.3 (ASan build: 26.6)   iPhone XS / iOS 18.7.2
U-1: pass  version=0x020000  read: error=6 release=1  dialog: error=0 button=0 release=1  main=yes  after-return=yes  notes=completion and release arrive after the call returns but in the same Unity frame (Time.frameCount unchanged); integration done with the PBXProject API, no warnings from the probe
U-2: pass  verify(IosLibrary/IosLibraryCApi/app)=ok/ok/ok  team-same=yes  validate=not run  notes=development signing (Apple Development), not distribution
U-5: dialog frames advanced=yes (1266 / ~30 per s for 20 s / 1879)  share frames advanced=yes (1991 / 4 frames in the first second, then ~30 per s / 2681)  completions=dialog error=0 button=1 release=1, share error=0 completed=0 release=1
U-8: start=shown  before-scene-load=shown (called at frame 0)  two-in-one-frame=both top=second
U-9: crashed  "libc++abi: terminating due to uncaught exception of type Il2CppExceptionWrapper" -> signal 6; no release; same on the simulator
U-10: tsan-on-device=not supported (developer.apple.com/documentation/xcode/diagnosing-memory-thread-and-crash-issues-early)  asan reports=0  notes=the app must be built with the same Xcode as the ASan xcframework (26.6), otherwise dyld fails on ___asan_version_mismatch_check_apple_clang_2100
blocked: none. Validate App not run (talks to App Store Connect; not asked for)
```

## 2. 組み込み方（申し送りの 2 章との違い）

試すプロジェクトは、どのリポジトリにも入れない使い捨てのもの（`/Users/jonghyunkim/Desktop/ntk-ios-cabi-probe`）。このパッケージは使わない（1.x の xcframework が混ざるため。最小 iOS も 15.0 のまま）。C# とビルドの手順の写しは `../probe/` にある。

- 手で入れる代わりに、`PostProcessBuild` で PBXProject の API を使って入れた（`ProbeBuild.cs`）。結果の `project.pbxproj` は 2 章のとおり:
  - UnityFramework: 2 つの xcframework を Frameworks のフェーズに入れるだけ（Do Not Embed）
  - Unity-iPhone: 2 つとも Frameworks のフェーズと Embed Frameworks に入る。`ATTRIBUTES = (CodeSignOnCopy, RemoveHeadersOnCopy, )`
  - `LD_RUNPATH_SEARCH_PATHS = "@executable_path/Frameworks"` は Unity の既定にあった
- `.app/Frameworks/` の `IosLibrary.framework` と `IosLibraryCApi.framework` には `Headers/` と `Modules/` が無い。UnityFramework とアプリは `@rpath/IosLibraryCApi.framework` と `@rpath/IosLibrary.framework` にリンクし、capi の公開シンボルは 17 個
- 警告: 雛形とこちらのコードからは 0 件。Unity 自身の非推奨の警告と、`UnityRuntime` の `no platform load command found`（シミュレーター）が出る
- 署名: 利用者の Mac にある Team のワイルドカードの profile（Xcode が管理するもの）を使った。手動の署名（`CODE_SIGN_STYLE=Manual` と profile の指定）は「Xcode managed」の profile と組めずに失敗した。自動の署名を `-allowProvisioningUpdates` なしで使い、Apple のアカウントに App ID を登録させずに組んだ
- C# の宣言（3 章）は書かれた形のままで、IL2CPP で組めて動いた。`byte[]` の渡し方、`MonoPInvokeCallback`、`Marshal.PtrToStringUTF8`、`out ulong` で困ったことは無い。コールバックが関数の戻った後に来たかは、呼ぶ間だけ `true` にする static の印で確かめた

## 3. 結果（実機）

### U-1: 合格

| 手順 | 記録 |
|---|---|
| (1) `ntk_version()` | `0x020000`（131072） |
| (2) read | 戻り値 0、`id=1`（frame 919）。`error=6 system=0 text=null` が 1 回、続いて `release tag=1` が 1 回。どちらも main で、関数が戻った後。frame は 919 のまま |
| (3) アラートの「Yes」 | 戻り値 0、`id=2`。`error=0 button=0` が 1 回、続いて `release tag=2` が 1 回。main、戻った後 |

- 来た順は、完了 → `release`。落ちない、`dyld` のエラーも無い
- read の完了は、呼んだのと同じ `Time.frameCount` の間に来た（シミュレーターも同じ）。関数が戻った後に main のキューで動き、Unity の次のフレームが始まる前に来るためと見られる。「呼んだフレームより後」を `frameCount` で確かめるのでなく、「関数が戻った後」として読むのがよい

### U-2: 合格

- Release のアーカイブ（Xcode 26.3）。`codesign --verify --deep --strict --verbose=2` は `Frameworks/IosLibrary.framework`、`Frameworks/IosLibraryCApi.framework`、`.app` の 3 つとも `valid on disk` と `satisfies its Designated Requirement`
- 2 つの framework の `TeamIdentifier` はアプリと同じ。署名の証明書は 3 つとも同じ種類（Apple Development。Authority は 3 段）
- Validate App はしていない（App Store Connect とやり取りするため）。署名は開発用で、配布用の証明書での書き出しはしていない

### U-5: 合否なし

| 場面 | 前 | 間 | 後 | 完了 |
|---|---|---|---|---|
| アラート（約 20 秒そのままにしてから「No」） | frame 1266（17:14:56） | 1 秒ごとに約 30 ずつ増え続けた（17:15:15 に 1837） | 1879（17:15:16） | `error=0 button=1`、`release` 1 回 |
| 共有シート（約 20 秒そのままにしてから「×」） | 1991（17:15:20） | 最初の 1 秒は 4 フレーム（シートが出る間）、その後は 1 秒ごとに約 30 | 2681（17:15:44） | `error=0 completed=0`（`activity_type` は NULL）、`release` 1 回 |

- 試すシーンには音が無いので、音は確かめていない。動きはフレームの数で記録した（画面の棒の目視は記録していない）
- 利用者が「U-5 dialog」をもう 1 回押し、「Yes」で閉じた（`button=0`、`release` 1 回）。手順の外だが、結果は変わらない

### U-8: 合否なし

| 手順 | 記録 |
|---|---|
| (1) 止めた状態から起動して、最初のシーンの `Start()` で呼ぶ | 出た。`Start` が frame 1 で呼び（プロセスの開始から約 2.2 秒）、「Yes」で `error=0 button=0`、`release` 1 回 |
| (2) `BeforeSceneLoad` で呼ぶ | 出た。frame 0 で呼んだ（`Start` より 38 ms 前）。「Yes」で `error=0 button=0`、`release` 1 回 |
| (3) 同じフレームで `first` と `second` | 2 つとも出た。上は `second`。上から押した（second に Yes、first に No）。`tag=832 button=0` → release、`tag=831 button=1` → release の順 |

- (1)・(2) は、利用者がアプリを App スイッチャーで終了し、アイコンから起動した。シミュレーターでも 3 つとも同じだった
- 画面に出るまでの時間は測っていない（利用者から「出なかった・遅かった」という報告は無い）

### U-9: 落ちた

- try / catch の無いコールバックから `InvalidOperationException("U-9")` を投げると、「Yes」を押した直後に落ちた
- コンソールの最後: `libc++abi: terminating due to uncaught exception of type Il2CppExceptionWrapper`、続いて `App terminated due to signal 6.`
- 例外を投げる前のログ（main、関数が戻った後）は出た。`release` は来ていない。シミュレーターでも同じ結果（そこでは取り消しの完了から投げた）
- IL2CPP の managed の例外は C++ の例外（`Il2CppExceptionWrapper`）として C と Swift の層に出て、誰も捕まえずに `std::terminate` になる。マニュアルの「コールバックは必ず try / catch」は、この結果で裏付けられる

### U-10

- TSan: 実機では使えない。Apple の文書「Diagnosing memory, thread, and crash issues early」の原文: "You can't use Thread Sanitizer to diagnose iOS, iPadOS, tvOS, visionOS, and watchOS apps running on a device. Use Thread Sanitizer only on your 64-bit macOS app, or to diagnose your 64-bit iOS, iPadOS, tvOS, visionOS, or watchOS app running in Simulator."（https://developer.apple.com/documentation/xcode/diagnosing-memory-thread-and-crash-issues-early）
- ASan: `asan/` の 2 つに入れ替え、`xcodebuild -enableAddressSanitizer YES` で組んだ（scheme の Diagnostics と同じ）。U-1 (1)〜(3) と U-5 (1)・(2) で `AddressSanitizer` の報告は 0。完了と `release` は普通の版と同じ（read `error=6`、dialog `button=0`、U-5 dialog `button=1`、share `completed=0`、どれも `release` 1 回、main、戻った後）。U-5 の間もフレームは 1 秒ごとに約 30 ずつ増えた
- **申し送りの文書と違う所**: 初め Xcode 26.3 で組むと、起動で `dyld: Symbol not found: ___asan_version_mismatch_check_apple_clang_2100`（`libclang_rt.asan_ios_dynamic.dylib` に無い）で落ちた。アプリに入る ASan の実行時ライブラリは組んだ Xcode のものなので、雛形を組んだ Xcode（26.6）でアプリも組む必要がある。「Unity のプロジェクトを組む Xcode は 26.3 以上ならどれでもよい」は、ASan の版には当てはまらない
- 終わった後、普通の版に戻した。戻す途中に、止めるつもりで使った `devicectl device process launch --terminate-existing` が ASan の版を 1 回起動した（許可の一覧の外。すぐ入れ替えた）

## 4. シミュレーターで先に確かめたこと

- U-1・U-5・U-8・U-9 の配線（アラートは押せないので取り消しで確かめた。取り消しは `error=6 button=-1`、`release` 1 回）
- 2 回目の版（`stage1a-handoff2`）: `NtkHandoff2Installer.mm` を UnityFramework に入れるには、**UnityFramework に `UserNotifications.framework` を足す必要がある**（無いと `_OBJC_CLASS_$_UNUserNotificationCenter` が未定義でリンクに失敗する。申し送りの 2 回目の 2 章に無い手順）。`AppDelegateListener.h`、`UnityRegisterAppDelegateListener`、`kUnityWillFinishLaunchingWithOptions` は Unity 6000.4.2f1 の `Classes/PluginBase/AppDelegateListener.h` に、見本の名前のままあった（`didFinishLaunching:` は `LifeCycleListener` から）。入れる時期は、ファイルごとのフラグ `-DNTK_HANDOFF2_INSTALL_AT=$(NTK_INSTALL_AT)` にして、`xcodebuild NTK_INSTALL_AT=2` で書き出し直さずに切り替えられた
- 通知パッケージ無しの版では、`kUnityWillFinishLaunchingWithOptions`・`didFinishLaunching`・`didBecomeActive` のどこでも、入れる前の delegate は `nil`（シミュレーター。実機の U-4 (e) でもう一度見る）

## 5. 私物の扱い

- 端末の識別子は、ファイルにもログの写しにも書いていない（コマンドの中では変数で渡した）。Team ID と証明書の名前も書いていない
- 実機のスクリーンショット・画面の録画・`.xcresult` は作っていない。共有シートの共有先は記録していない（`activity_type` は NULL）
- 実機のコンソールの写しは、書き写した後に消す
