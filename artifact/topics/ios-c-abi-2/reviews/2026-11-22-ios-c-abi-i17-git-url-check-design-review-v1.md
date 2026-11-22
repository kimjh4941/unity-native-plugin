# I-17 の git の URL の形での確かめの設計 v1 のレビュー v1

- 記録日: 2026-11-22
- 対象: [../designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v1.md](../designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）を並行で
- 1 章と 2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く
- Codex の呼び出し役の注記: 読み取りのために呼んだ `/usr/bin/strings` が `xcodebuild` を間接に起動した（ファイルの変更は無い）。同じ時間にゲートの回が流れていた

## 1. サブエージェント

対象: `artifact/topics/ios-c-abi-2/designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v1.md`
読むだけで進めた。ファイルの編集はしていない。Unity・xcodebuild・simctl・devicectl・scripts/ も動かしていない。

**R1-1**
- 区分: A1
- 観点: 端末の決まり
- 場所: 3.1 の `Packages/manifest.json`（53 行）、`I17_STEPS`（57 行）、3.2 の区分の文（65 行）、G-7（23 行）
- 指摘: G-7 の実機のアプリにも Mobile Notifications 2.5.0 が入る。使い捨てのプロジェクトは 1 つで、manifest に Mobile Notifications がある。この版は既定で `UnityNotificationRequestAuthorizationOnAppLaunch=true` なので、起動したときに通知の許可を自分から求める。`I17_STEPS=launch` で止められるのは C# の `AuthorizationRequest` だけで、起動時の要求は止まらない。その結果、利用者の iPhone に許可のダイアログが出る。それでも G-7 の合格の条件（結果のファイルの `started`、版、delegate のクラス）は満たされ、5 章の流し方では気づけない。承認の文の「通知の許可を求めず」とも合わない。
- 根拠:
  - `artifact/topics/ios-c-abi-handoff/results/2026-10-31-ios-c-abi-handoff-2-results.md:59`（「既定の設定は `UnityNotificationRequestAuthorizationOnAppLaunch=true`。…済んでいればダイアログも出ない」。つまり、済んでいなければダイアログが出る）
  - 同 :56（`+load` で `kUnityWillFinishLaunching` に delegate を上書きする）
  - `native-toolkit/agent-rules/device-testing.md:29`（権限に関わる操作はその都度許可を得る）
- 提案: 端末向けの書き出し（`--device`）では、manifest から `com.unity.mobile.notifications` を外す。どうしても残すなら、Editor の API でこのパッケージの iOS の「Request Authorization on App Launch」を false にし、それを書き出したプロジェクトの Info.plist で確かめてから入れる。シミュレーターの M でも同じ設定にしておくと、R1-3 で書く起動時のダイアログも出なくなる。

**R1-2**
- 区分: A2
- 観点: 正しさ
- 場所: G-1（17 行）
- 指摘: 合格の条件に「BuildProcessors のアセンブリが `Library/ScriptAssemblies` にある」とあるが、利用者の形ではこのアセンブリは組まれない。`NativeToolkit.BuildProcessors.Editor` には `defineConstraints: NATIVETOOLKIT_ENABLE_BUILD_STEPS` があり、使い捨てのプロジェクトにはこの define が無いからだ。書いてあるとおりに流すと G-1 は落ちる。G-1 を通すために define を足すと、今度は利用者の形ではなくなる。dist から写す段が、存在しない `../native-toolkit/dist` を探して動き出す。
- 根拠:
  - `Packages/com.jonghyunkim.nativetoolkit/Editor/Build/NativeToolkit.BuildProcessors.Editor.asmdef`（`defineConstraints`）
  - `artifact/topics/ios-c-abi-2/README.md:138`（git の URL の形で「3 つのアセンブリが組まれ」たという記録）
  - `Editor/Build/Tools/NativeToolkit.BuildTools.Editor.asmdef`（制約なし）
- 提案: G-1 を次の形に書き直す。
  - あるもの: `NativeToolkit.Runtime`、`NativeToolkit.Editor`、`NativeToolkit.BuildTools.Editor`
  - 無いもの: `NativeToolkit.BuildProcessors.Editor` と 2 つの Tests のアセンブリ
  - 「define は足さない」と明記する

**R1-3**
- 区分: A2
- 観点: 実現性
- 場所: M-2（35 行）
- 指摘: `xcrun simctl privacy <udid> grant notifications <bundle id>` は使えない。simctl privacy の対象に通知が無く、native-toolkit の 0a で「Operation not permitted」になったと記録されている。そのため M-2 の「終わるまで待つ」は、許可のダイアログが残ったまま終わらない。Mobile Notifications の起動時の要求（R1-1）で、起動した直後にもダイアログが出ている。
- 根拠:
  - `native-toolkit/artifact/topics/ios-c-abi/results/2026-10-18-ios-c-abi-stage0a-spike-result.md:45`
  - `native-toolkit/artifact/topics/ios-c-abi/designs/2026-10-18-ios-c-abi-ui-test-design.md:73`（「`simctl privacy` に通知は無い」）
  - U-4 の handoff-2 results:57（`_RequestAuthorization` が呼ばれるたびに `center.delegate = manager` と上書きする）
- 提案: 次の 2 案のどちらかにする。
  - (a) `AuthorizationRequest` を作った直後に delegate を読み、完了は待たない。上書きは `_RequestAuthorization` の中で起きる。ただしこれはソースの読みなので、記録には「見込み」と書く。
  - (b) 今の UI の手伝い役（`DriverUITests/test_drive`）に Allow を押させる。

  どちらの場合も、起動時の要求は R1-1 のとおり止めておく。

**R1-4**
- 区分: A2
- 観点: 実現性
- 場所: G-6（22 行）
- 指摘: 「CPU アーキテクチャの確かめのログが出る」は満たせない。`AndroidArchitectureCheck` は、合格のときに何もログに書かない。書くのは、対応しないアーキテクチャで `BuildFailedException` を投げるときだけだ。ログが出るのは Gradle の段だけ（`[AndroidGradleDependencies] 7 native-toolkit runtime dependencies in …`）。
- 根拠: `Packages/com.jonghyunkim.nativetoolkit/Editor/Android/AndroidArchitectureCheck.cs:33-39`、`AndroidGradleDependencies.cs:72`
- 提案: 確かめを 2 つに分ける。
  - ARM64 だけのビルド: 成功し、Gradle の段の行が出る
  - ARMv7 を足したビルド: `[NativeToolkit] Native Toolkit for Android supports ARM64` で止まる。これで、アーキテクチャの確かめが利用者の形でも動くことが分かる

**R1-5**
- 区分: A2
- 観点: 実現性
- 場所: 3.1 の `Assets/Editor/I17Build.cs`（56 行）、G-2・G-3・G-6
- 指摘: 新しく作ったプロジェクトでは、このリポジトリで設定済みの値が既定値に戻る。設計にはそれをどう設定するかが書かれていない。次の値はどれも、合っていないとビルドで止まる。
  - 最小 iOS が 18.0 未満だと、`IosMinimumVersionCheck` が止める
  - AAR は `minSdkVersion=31` なので、Android の最小 API が 31 未満だと Gradle の manifest の統合が落ちる
  - IL2CPP と ARM64 でないと、アーキテクチャの確かめが止める

  ほかにも次の点が書かれていない。
  - bundle id（`com.jonghyunkim.nativetoolkit.giturlapp`）
  - Simulator SDK と Device SDK の切り替え（G-3 と G-5・G-7 で要る）
  - Unity の起動ごとに `-buildTarget` を分けること。iOS の口は `#if UNITY_IOS`、Gradle の段は `#if UNITY_ANDROID` で、どちらも Editor のビルドの対象が合っている起動でしか組まれない。同じ起動の中で Android の `BuildPlayer` を呼んでも、Gradle の段は読み込まれていない
- 根拠:
  - `ProjectSettings/ProjectSettings.asset:179`（`AndroidMinSdkVersion: 31`）、`:197`（`iOSTargetOSVersionString: 18.0`）、`:270`（`AndroidTargetArchitectures: 2`）、`:844`（IL2CPP）
  - AAR の `AndroidManifest.xml` の `minSdkVersion="31"`（2 つとも）
  - `IosXcodeProjectSetup.cs:3`、`AndroidGradleDependencies.cs:129`、`IosMinimumVersionCheck.cs:32-37`
- 提案: 3.1 に次を書き足す。
  - I17Build が設定する Player Settings の一覧
  - 「1 回の Unity の起動は 1 つのビルドの対象（`-buildTarget iOS` / `-buildTarget Android`）」
  - 段の順（G-4 は最後、など）

  最小 iOS を既定値のまま書き出し、止まることを見る確かめを足すと、利用者の形での I-16 の確かめにもなる。

**R1-6**
- 区分: A2
- 観点: 実現性
- 場所: 3.2（64〜66 行）、4 章
- 指摘: `DeviceRequirements.json` の `tests` に `GitUrlApp/IosLaunch` を足すと、`ios_ui_runner.py tests` がそれを UI のテストとして出力する。この出力から除かれているのは `PLAYER_TESTS` だけだからだ。すると `test_ios_ui.sh` は、存在しない XCUITest を流そうとして「did not run」で落ちる。さらに、`mixed_kinds` を giturlapp まで広げると、`--device` で全部の UI のテストを承認しようとしたときに拒まれ、`test_ios_ui.sh --device` が使えなくなる。`approve` が出すアプリの一覧（482 行）も、今のままでは giturlapp に `UI_APPS` を出す。どれも 5 章の流し方には入っておらず、次に `test_ios_ui.sh` を流したときに分かる。
- 根拠: `scripts/ios_ui_runner.py:312-314,417-419,482`、`scripts/test_ios_ui.sh:95-99,116-118`、`DeviceRequirements.json` の `about`（「scripts/test_ios_ui.sh never selects it」）
- 提案: 4 章の変更に次を足す。
  - `tests`・`approve`・`mixed_kinds` で、Player のテストと同じように giturlapp も除く・分ける
  - `GITURL_APPS` を足す
  - `about` の文を書き直す
  - doctest を 1 つ足す

  合格の条件に「`test_ios_ui.sh` の一覧（`tests` と `tests --device`）が変わらない」を加える。

**R1-7**
- 区分: B
- 観点: 正しさ
- 場所: G-4（20 行）、6 章（94 行）
- 指摘: 一番の危険は、define を足すのと書き出すのを同じ Unity の起動で行うことだ。I17Build の中で `PlayerSettings.SetScriptingDefineSymbols` を呼んだ直後に `BuildPlayer` を呼ぶと、`NativeToolkit.Editor` は組み直されない。すると口の `#if NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` は古いままで、ログに `skip: False` が出る。これは偽の失敗だが、6 章の筋書きでは「マニュアルから Player Settings の経路を外す」に向かってしまう。Player Settings の経路は、段 1 の S-4 で写しの口を使って効くことを確かめている。もう 1 つ、合格の条件が、今のゲートの `check_installer` より弱い。ゲートは、ファイルが無いことと pbxproj に参照が無いことも確かめている。
- 根拠: `scripts/verify_unity_ios.sh:463-483`、`artifact/topics/ios-c-abi-2/README.md` の 6.3 の S-4、`IosXcodeProjectSetup.cs:63-67`
- 提案:
  - define を設定する起動と、書き出す起動を分ける
  - G-4 の条件を `check_installer` と同じにする（ログの行、`.mm` のファイルが無い、pbxproj に `NtkNotificationInstaller.mm` が無い）
  - 6 章に「まず手順（組み直しが済んでいるか）を疑う。S-4 では効いた」と書き足す

**R1-8**
- 区分: B
- 観点: 正しさ
- 場所: M-2・M-3（35〜36 行）
- 指摘: M-2 と M-3 の条件の一部は、正しく動いても壊れていても同じ結果になる。
  - M-3 の「受け手の登録が `NONE`」は見分けにならない。受け手の登録の前提は「このプロセスで一度でも入ったか」で、これは一度 true になると戻らない。起動時の `.mm` で入った後なら、`Initialize()` が何もしなくても `NONE` になる。
  - M-2 の「proxy ではなくなる」は、delegate が nil になった場合でも通る。
- 根拠: `native-toolkit/ios/IosLibrary/IosLibrary/Notification/Presentation/Event/NotificationEventHub.swift:77`、`…/Delegate/NotificationDelegateInstaller.swift:108,136-160`、U-4 の handoff-2 results:56（パッケージの delegate のクラスは `UnityNotificationManager`）
- 提案:
  - M-2 は「クラスが `UnityNotificationManager`」とする
  - M-3 の決め手は「delegate が proxy に戻る」と「R1-9 のログの行に `UnityNotificationManager` が出る」の 2 つにする
  - `NONE` は記録に残すだけにするか、外す

**R1-9**
- 区分: B
- 観点: 抜け
- 場所: M-3（36 行）
- 指摘: `installing the proxy again in front of it` をどうやって読むのかが書かれていない。この行は `logger.info`（os_log の info）で、アプリの stdout には出ず、結果のファイルにも入らない。U-4 では `OS_ACTIVITY_DT_MODE=YES` を渡して stderr に写して読んだ。
- 根拠: `NotificationDelegateInstaller.swift:148`、U-4 の handoff-2 results:32
- 提案: どちらかの読み方を設計に書く。
  - 起動の環境に `SIMCTL_CHILD_OS_ACTIVITY_DT_MODE=YES` を渡し、`simctl launch --console-pty` の出力から読む
  - `simctl spawn <udid> log stream --level info --predicate 'subsystem == "com.jonghyunkim.nativetoolkit.IosLibrary"'` で読む

  どちらにしても、出力に `UnityNotificationManager` が含まれることを合格の条件にする。

**R1-10**
- 区分: B
- 観点: 端末の決まり
- 場所: G-5（21 行）、3.1 の `--simulator`（49 行）、3.2（73 行）
- 指摘: G-5 は既定の `--simulator` の回の中で流れる。ところが、ログを消す決まりは実機の節（3.2）にしか書かれていない。今のゲートのシミュレーターの回は、`OUT_DIR` を残してその道を出力する（693 行）。ビルドが失敗したときは、エラーの行もそのまま見せる（`show_build_errors` の第 2 引数が 0）。アーカイブのログには、署名の名前と Team ID が入る。また「出し方は 3-E と同じ」とだけ書くと、3-E の 2 章の手順を指して読める。その手順の 30 文字で切る方法で、証明書の名前の頭 1 文字が出た（3-E の 5 章）。
- 根拠: `scripts/verify_unity_ios.sh:693`、`scripts/ios_script_common.sh:199-208`、`artifact/topics/ios-c-abi-2/results/2026-11-15-ios-c-abi-3e-resign-result-v1.md:50`
- 提案: G-5 について次を明記する。
  - ログは専用の一時フォルダーに置き、終わりに消す
  - エラーは数だけ出す（`show_build_errors <log> 1`）
  - `Authority=` は `:` までで切る（3-E の 5 章）
  - `TeamIdentifier=` は `shasum` の頭 8 文字だけを出す

**R1-11**
- 区分: B
- 観点: 抜け
- 場所: 3.1
- 指摘: 今のスクリプトが守っている仕組みが、設計に書かれていない。
  - 実行の鍵（`take_run_lock`）。今はゲートのシミュレーターの回が流れていて、そちらは自分で起動したシミュレーターを終わりに落とす
  - Simulator.app が開いていないことの確かめ（`pgrep -x Simulator`）
  - 長い段の上限（`run_limited`）
  - 使うシミュレーター（`SIM_NAME` / `SIM_RUNTIME`、`find_simulator`）
  - シミュレーターでアプリを入れる前と終わりに消すこと
  - 実機で `select_device`・`select_team`・`remove_from_device`・`device_guard` を使うこと
- 根拠: `scripts/verify_unity_ios.sh:195-202,277-304,324-334`、`scripts/ios_script_common.sh:113-156,241-279`、`native-toolkit/agent-rules/device-testing.md:63-64`
- 提案: 「`ios_script_common.sh` を source し、実行の鍵・Simulator.app の確かめ・`run_limited`・`device_guard` は `verify_unity_ios.sh` と同じ形にする」と書き、既定のシミュレーターを指定する（iPhone 16 / iOS 18.3）。

**R1-12**
- 区分: B
- 観点: 正しさ
- 場所: 3.1 の `Packages/manifest.json`（53 行）
- 指摘: 「スクリプトが一時フォルダーに書く」manifest が「パッケージと Mobile Notifications」だけだと、利用者の形にならない。利用者の manifest には Unity の既定の組み込みモジュールが並んでいる。このパッケージの `package.json` はモジュールへの依存を書いていない。一方で `UnityEngine.UIElements`（19 ファイル）と AndroidJNI を使っている。手で書いた manifest でコンパイルのエラーが出たり出なかったりしても、それは利用者には起きないことかもしれない。
- 根拠: `Packages/com.jonghyunkim.nativetoolkit/package.json`（`dependencies` は 3 つ）、`Packages/manifest.json`（`com.unity.modules.*`）、README の 6.3 V-8（「Unity 6000.4.2f1 の新規」）
- 提案: `-createProject` で既定の manifest を作り、そこに 2 つを足す形にする。足した後の manifest を、記録に添えるか要約する。

**R1-13**
- 区分: B
- 観点: 端末の決まり
- 場所: 3.2（71 行）
- 指摘: 端末の決まりでは、許可を得る前に 4 つを知らせることになっている（一覧と ID、端末に起きること、利用者の準備、時間の見込み）。設計に書かれているのは前の 2 つだけだ。今の `approve` は、準備と時間を keep-installed の回にしか出さない。
- 根拠: `native-toolkit/agent-rules/device-testing.md:10-15`、`scripts/ios_ui_runner.py:485-489`
- 提案: giturlapp の承認の出力（または承認を頼むときの文）に、次を書く。
  - 準備: ロックの解除と自動ロック「なし」、給電、開発用の接続の確かめ
  - 時間の見込み
  - 区分に `reinstall` を加えるか、「入れる前に消す」と書く

**R1-14**
- 区分: B
- 観点: 抜け
- 場所: 3.1 の結果のファイル（54・58 行）
- 指摘: 結果のファイルを 1 行ずつ書いていくのに、書き終わったことを示す印が無い。M-2 は待つ段を含むので、スクリプトは途中まで書かれたファイルを読みかねない。今のゲートは `.done` を待ってから結果を読み、実機ではその `.done` を繰り返し写し出している。
- 根拠: `scripts/verify_unity_ios.sh:566-588,623-647`
- 提案: 次の 3 つを設計に書く。
  - 最後の行を `done`（または別のファイル）にする
  - スクリプトはそれが現れるまで、上限つきで待つ
  - 実機では、写し出す前ごとに `device_guard` を通す

**R1-15**
- 区分: C
- 観点: 整合
- 場所: 9 行、95 行
- 指摘: 「2.0.0」が、native-toolkit の版なのか Mobile Notifications の版なのか読み分けにくい（Mobile Notifications は 2.5.0）。95 行の「2.0.0 では Mobile Notifications が上書きしない形になった」は、Mobile Notifications の振る舞いを native-toolkit の版に結びつけている。M-2 で delegate が替わらなかったら、まず疑うのは、読む時期、`AuthorizationRequest` が実際に呼ばれたか、パッケージの版の 3 つだ。
- 根拠: 設計の 9・30・95 行、U-4 の handoff-2 results:57
- 提案: 「native-toolkit 2.0.0」と「Mobile Notifications 2.5.0」と書き分ける。6 章の M-2 の行は、疑う順を書く形に直す。

**R1-16**
- 区分: C
- 観点: 整合
- 場所: G-2（18 行）
- 指摘: 口のログは 1 行で出る。2 つの名前と `installer entries` が同じ行にある。設計の書き方（「2 つの xcframework の `linked and embedded`」）だと、名前を照らす相手が決まらない。また、`-debug` の版を受け入れるかどうかも曖昧だ（`ResolvePresentIn` は、release の版が無ければ `-debug` の版を返す）。
- 根拠: `IosXcodeProjectSetup.cs:93-94`、`IosXcframeworkPin.cs:157-173`、`Plugins/iOS/VERSION.txt`
- 提案: 照らす行をそのまま書く: `linked and embedded: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework; installer entries: 1 (skip: False)`

**R1-17**
- 区分: C
- 観点: 整合
- 場所: 4 章（81 行）
- 指摘: マニュアルの要確認のコメントには 3 つのことが書かれている。
  - (1) Player Settings の経路
  - (2) 起動の後に入れると、起動のタップが失われうること
  - (3) Mobile Notifications との共存

  G-4 と M で消せるのは (1) と、(3) のうち「delegate が入れ替わる」部分だけだ。(2) と、「このパッケージが出していない通知のタップが前の delegate に渡る」という文（2 章で確かめないと決めたもの）は残る。`notification.ja.md` と `notification.ko.md` も同じだ。
- 根拠: `manual/1.14.0/notification.md:909,923`、設計の 38〜41 行
- 提案: 「要確認を外す」を「測ったところだけを外し、残りは根拠（native-toolkit の 1b、ソースの読み）を添えて書き直す」に変え、3 言語で揃える。

件数: A1 1、A2 5（R1-2〜R1-6）、B 8（R1-7〜R1-14）、C 3（R1-15〜R1-17）

## 2. Codex

- **高：G-7 で通知の許可ダイアログが出る。**
  §3.1 は実機にも Mobile Notifications を入れ、`I17_STEPS=launch` で要求を防ぐ設計ですが、同パッケージの起動時自動要求は既定で有効です。プローブの分岐では止まりません。根拠：[既定値の確認記録:59](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-handoff/results/2026-10-31-ios-c-abi-handoff-2-results.md:59)、[Unity 公式の起動時要求設定](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.5/manual/Settings.html#request-authorization-on-app-launch)。
  **修正：** 書き出す前に `NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false` を明示する。

- **中：G-1 は正常な利用者プロジェクトを不合格にする。**
  `BuildProcessors` の存在を必須にしていますが、このアセンブリは `NATIVETOOLKIT_ENABLE_BUILD_STEPS` がある場合だけコンパイルされます。設計にはその定義がありません。根拠：[asmdef:5](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Editor/Build/NativeToolkit.BuildProcessors.Editor.asmdef:5)。
  **修正：** 通常の利用者構成では `BuildProcessors` の不在を許容し、必須アセンブリから外す。

- **中：G-6 が要求するアーキテクチャ検査の成功ログは存在しない。**
  依存追加は `[AndroidGradleDependencies] 7 native-toolkit runtime dependencies in …` を出しますが、アーキテクチャ検査は成功時に何も出さず、不正な構成だけ例外にします。根拠：[依存追加:72](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Editor/Android/AndroidGradleDependencies.cs:72)、[検査:33](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Editor/Android/AndroidArchitectureCheck.cs:33)。
  **修正：** 成功ログの要求を依存追加だけに限定し、検査の動作は不正アーキテクチャで期待した例外が出る別ケースで確認する。

- **中：§3.2 の仮テストが通常の UI テスト一覧に混入する。**
  `requirements()` は JSON の全テストを返し、`tests` コマンドが特別に除外するのは `PlayerTests/IosPlayer` だけです。追加予定の `GitUrlApp/IosLaunch` も列挙されます。計画どおり `mixed_kinds` を拡張すると、通常の実機 UI テストの承認が混在として拒否されます。根拠：[requirements:251](/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/ios_ui_runner.py:251)、[一覧の除外条件:419](/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/ios_ui_runner.py:419)、[混在拒否:466](/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/ios_ui_runner.py:466)。
  **修正：** 承認用には仮テストを保持し、UI テスト列挙からは Player の仮テストとともに除外する。

- **中：M-3 の「受け手登録が `NONE`」を予定された観測手段で取得できない。**
  `Initialize()` は `void` で、内部の登録処理は成功値を返さず、成功ログも出しません。予定されたネイティブプローブも delegate のクラス名しか観測しません。根拠：[Initialize:165](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationManager.cs:165)、[登録処理:683](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationManager.cs:683)、[プローブ:12](/Users/jonghyunkim/Desktop/unity-native-plugin/Assets/NativeToolkitIosPlayerTests/NativeSource~/NtkPlayerTestsProbe.mm:12)。
  **修正：** 合格条件を Manager の登録ハンドル非ゼロに変更し、使い捨てプローブから観測する。

## 3. 判断

- A1 は 1 件（R1-1。Codex の「高」と同じ）。直して、差分のレビュー（ラウンド 2）を回す
- 設計に直し込む（A1 と中心の契約の B）: R1-1、R1-7、R1-8、R1-9（Codex の M-3 も）、R1-10、R1-13、R1-14
- 文が間違ったまま残ると紛らわしいので、あわせて直す A2: R1-2、R1-4（Codex の G-1・G-6 も）、R1-6（Codex の仮テストの混入も）
- 実装のチェックリストへ（A2）: R1-3（simctl privacy は通知に使えない → 起動時の要求を切り、`AuthorizationRequest` を作った直後に delegate を読む）、R1-5（Player Settings の一覧、起動ごとの `-buildTarget`、段の順）
- 残件として書く（網羅系の B）: R1-11（`ios_script_common.sh` の仕組みを使い回す）、R1-12（`-createProject` の既定の manifest）
- C は v2 の書き直しの中で直る分だけ直す（R1-15、R1-16、R1-17）
