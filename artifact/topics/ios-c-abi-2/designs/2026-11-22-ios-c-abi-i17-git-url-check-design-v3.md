# I-17: git の URL で入れた利用者の形での確かめ（設計 v3）

- 日付: 2026-11-22
- 元: 段 4 の計画（[2026-11-15-ios-c-abi-stage4-plan-v1.md](2026-11-15-ios-c-abi-stage4-plan-v1.md)）の項目 8、設計書 v4 の I-17、3-E の残り（[../results/2026-11-15-ios-c-abi-3e-resign-result-v1.md](../results/2026-11-15-ios-c-abi-3e-resign-result-v1.md) の 4 章）、マニュアル 1.14.0 の `notification*.md` の最後の要確認
- v2 からの変更（レビューしていない）: レビュー v2（[../reviews/2026-11-22-ios-c-abi-i17-git-url-check-design-review-v2.md](../reviews/2026-11-22-ios-c-abi-i17-git-url-check-design-review-v2.md)、A1 は 0 で止めた）の R2-1〜R2-5 と Codex の M-2、R1-13 の残りを直した
- v1 からの変更: レビュー v1（[../reviews/2026-11-22-ios-c-abi-i17-git-url-check-design-review-v1.md](../reviews/2026-11-22-ios-c-abi-i17-git-url-check-design-review-v1.md)）の A1（R1-1）と中心の B（R1-7・R1-8・R1-9・R1-10・R1-13・R1-14）、文が誤っていた A2（R1-2・R1-4・R1-6）を直した。残りは 7 章
- 利用者の判断（2026-11-21 夜。トピックの README の段 4 の行）:
  - Android は端末なし。git の URL の形で、ビルドとビルドの段が動くことまで。Android の本体は 1.13.0 から変わっていない
  - iOS は実機で起動する。使い捨てのアプリは承認の仕組みに足す
  - 3-E の残りは Personal Team で、端末なし
  - Mobile Notifications との共存は、native-toolkit 2.0.0 でシミュレーターで測る

## 1. 確かめること

このリポジトリでは、パッケージが `Packages/` に埋め込まれていて、git の URL で入れた利用者と振る舞いが違う（README 6.2。`NativeToolkit.Editor` の asmdef の件、`c1aeca0`）。使い捨ての Unity プロジェクトに、`git+file:///<このリポジトリ>?path=Packages/com.jonghyunkim.nativetoolkit#<HEAD>` でパッケージを入れて確かめる。プロジェクトに define は足さない（`NATIVETOOLKIT_ENABLE_BUILD_STEPS` も無い。利用者の形）。

使い捨てのプロジェクトは 2 つ作る。

- **基本のプロジェクト**: パッケージだけ。G-1〜G-7
- **共存のプロジェクト**: 基本に `com.unity.mobile.notifications` 2.5.0（1a の U-4 と同じ版）を足したもの。M-1〜M-3。シミュレーターだけで使い、実機には決して入れない（R1-1: Mobile Notifications は既定で起動時に通知の許可を求める）

| 番号 | 確かめ | 端末 | 合格の条件 |
|---|---|---|---|
| G-1 | 組み立て | なし | `Library/ScriptAssemblies` に `NativeToolkit.Runtime`・`NativeToolkit.Editor`・`NativeToolkit.BuildTools.Editor` があり、`NativeToolkit.BuildProcessors.Editor` と 2 つの Tests のアセンブリが無い（R1-2）。コンパイルのエラーが 0 |
| G-2 | iOS の書き出し | なし | 書き出しのログに `linked and embedded: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework; installer entries: 1 (skip: False)` の行がある。書き出した Xcode のプロジェクトに `NtkNotificationInstaller.mm` があり、`project.pbxproj` がそれを Sources で指す（ゲートの `check_installer` と同じ） |
| G-3 | シミュレーターでの起動 | シミュレーター | 結果のファイルに `started`、`version=0x020000`、`delegate=IosLibrary.NotificationDelegateProxy`、最後に `done` |
| G-4 | Player Settings の define | なし | Player Settings（iOS）の Scripting Define Symbols に `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` を足す Unity の起動と、書き出す Unity の起動を分ける（R1-7: 同じ起動では Editor のアセンブリが組み直されない）。書き出しのログが `installer entries: 0 (skip: True)`、`.mm` のファイルが無く、`project.pbxproj` に `NtkNotificationInstaller.mm` が無い |
| G-5 | Release のアーカイブ（3-E の残り） | なし | 3.3 |
| G-6a | Android のビルド（ARM64 だけ） | なし | APK のビルドが成功し、ログに `[AndroidGradleDependencies] ... native-toolkit runtime dependencies in` の行がある（R1-4） |
| G-6b | Android の CPU アーキテクチャの確かめ | なし | ARMv7 を足したビルドが、`Native Toolkit for Android supports ARM64` の文で止まる（成功のときは何も出さない段なので、止まることで動いていると見る。R1-4） |
| G-7 | iOS の実機での起動 | 実機（承認） | G-3 と同じ条件を、iPhone XS で。基本のプロジェクトだけを使う |
| M-1〜M-3 | Mobile Notifications との共存 | シミュレーター | 2 章 |

## 2. Mobile Notifications との共存（M-1〜M-3）

マニュアルは「別の通知パッケージが自分の delegate を入れたら、その後で `Initialize()` を呼ぶ。このパッケージの delegate を入れ直し、今の delegate を前の delegate として残す。このパッケージが出していない通知のタップは、前の delegate に渡る」と書く。1a の試作（U-4）では、Mobile Notifications 2.5.0 の `+load` が `kUnityWillFinishLaunching` で delegate を上書きし（`UnityNotificationManager`）、C# の `AuthorizationRequest` が呼ばれるたびに上書きし直すことが分かっている。

共存のプロジェクトでは、書き出す前に Mobile Notifications の iOS の設定「Request Authorization on App Launch」を false にする（Editor の API。R1-1）。書き出した `Info.plist` で `UnityNotificationRequestAuthorizationOnAppLaunch` が false であることを確かめてから、シミュレーターに入れる。通知の許可のダイアログは、シミュレーターでも出さない（`simctl privacy` は通知に使えない。R1-3）。M-2 の `AuthorizationRequest` は、仮の許可（`AuthorizationOption.Provisional`。Alert・Badge・Sound と合わせて）で、遠隔通知の登録をせずに（`registerForRemoteNotifications: false`）頼む。仮の許可はダイアログを出さずに認められるので、アプリは inactive にならず、Unity は止まらない（レビュー v2 の R2-1、Codex の M-2）。ダイアログが出てしまったときは答えず、アプリを消して閉じ、M を失敗にする

| 番号 | 段 | 合格の条件 |
|---|---|---|
| M-1 | 起動の後 | delegate のクラスが `IosLibrary.NotificationDelegateProxy`（パッケージの `.mm` は `didFinishLaunching` で入れ、Mobile Notifications の `kUnityWillFinishLaunching` での上書きより後になる。U-4 の時期 2 では、どちらのリンクの順でも proxy だった。R2-2） |
| M-2 | `AuthorizationRequest` を作り、完了を待たずに 1 秒後に delegate を読む | delegate のクラスが `UnityNotificationManager`（R1-8。nil ではない） |
| M-3 | `IosNotificationManager.Instance.Initialize()` | delegate のクラスが `IosLibrary.NotificationDelegateProxy` に戻る。ライブラリのログに `installing the proxy again in front of it` と `UnityNotificationManager` がある（R1-8・R1-9） |

ライブラリのログ（os_log の info）は、U-4 と同じ `OS_ACTIVITY_DT_MODE=YES` を、シミュレーターでは `SIMCTL_CHILD_` を付けて起動の環境に渡し、`xcrun simctl launch --console-pty` の出力から読む（シミュレーターでこの写し方を使うのは今回が初めて。R1-9、R2-4）。

確かめないこと（理由）:

- タップの転送（このパッケージが出していない通知のタップが、前の delegate に届くこと）。確かめるには通知のバナーのタップが要る。転送は native-toolkit の 1b のシミュレーターのテスト（転送しない偽の delegate を使ったもの）で確かめ済みで、Unity の側の関わりは `Initialize()` が `ntk_notification_install` を呼ぶことだけ（M-3 で見る）
- 実機での共存。シミュレーターで delegate の入れ替わりを見れば、端末で変わる理由が無い。また、共存のプロジェクトを実機に入れない（R1-1）

## 3. 仕組み

### 3.1 スクリプト `scripts/verify_unity_ios_git_url.sh`

新しく足す。`scripts/ios_script_common.sh` を source し、実行の鍵（`take_run_lock`）、Simulator.app が開いていないことの確かめ、長い段の上限（`run_limited`）、シミュレーターの選び方（`SIM_NAME` / `SIM_RUNTIME`、既定は iPhone 16 / iOS 18.3）、実機の `select_device`・`select_team`・`remove_from_device`・`device_guard` は `verify_unity_ios.sh` と同じ形にする（R1-11）。

- 既定（シミュレーター）: G-1〜G-6、M-1〜M-3
- `--device --dry-run` / `--device --approved <ID>`: G-7（基本のプロジェクトを端末向けに書き出して起動する）
- パッケージは `git+file:///<repo>?path=Packages/com.jonghyunkim.nativetoolkit#<HEAD のハッシュ>`。パッケージに未コミットの変更があれば断る
- 使い捨てのプロジェクトは一時フォルダーに作り、終わりに消す。リポジトリの作業ツリーには何も書かない。manifest は Unity の `-createProject` が作る既定のものに、パッケージ（と共存のプロジェクトでは Mobile Notifications）を足す（R1-12）
- 1 回の Unity の起動は 1 つのビルドの対象（`-buildTarget iOS` / `-buildTarget Android`）。iOS の口は `#if UNITY_IOS`、Android の段は `#if UNITY_ANDROID` で、Editor のビルドの対象が合っている起動でしか組まれない（R1-5）
- Player Settings はスクリプトが Editor の API で設定する: bundle id `com.jonghyunkim.nativetoolkit.giturlapp`、最小 iOS 18.0、Android の最小 API 31、IL2CPP、ARM64（R1-5）
- 段の順（R2-3）: 基本のプロジェクトで G-1 → G-2 → G-3 → G-5 → G-6a → G-6b → G-4。G-4 の define と G-6b の ARMv7 は ProjectSettings に残るので、最後に流す（G-4 を G-5 より前に流すと、インストーラーの無いアーカイブで G-5 が通ってしまう）。共存のプロジェクト（M）は別のプロジェクトなので、順に関わらない。G-7 は別の回で、基本のプロジェクトを作り直す
- アプリ（`Assets/Scripts/I17Probe.cs`）は起動時の環境変数 `I17_STEPS`（`launch` か `launch,coexist`）に従って段を行い、`Application.persistentDataPath/i17-result.txt` に 1 行 1 段で書く。書くのは決まった語、数字、クラス名だけ。最後の行は `done`。スクリプトは `done` が現れるまで上限つきで待つ。実機では写し出す前ごとに `device_guard` を通す（R1-14）
- delegate のクラス名と `ntk_version()` は `Assets/Plugins/iOS/I17Probe.mm` が返す（Player のテストのプローブと同じ読み方）
- 結果は、シミュレーターでは `xcrun simctl get_app_container ... data` から、実機では `devicectl device copy from --domain-type appDataContainer` で読む（ゲートと同じ）。アプリは入れる前に消し、終わりにも消す

### 3.2 承認の仕組み

`scripts/ios_ui_runner.py` と `Tools~/IosUiTests/DeviceRequirements.json` に次を足す。

- 仮のテストの名前 `GitUrlApp/IosLaunch`（`tests` に、区分 `giturlapp` と `reinstall` で）
- 区分の文 `giturlapp`: 「このリポジトリの HEAD を git の URL で入れた使い捨てのアプリ（Unity。ほかのパッケージは入れない）を入れて起動する。アプリは通知の delegate のクラス名とライブラリの版を自分のデータのフォルダーに書くだけで、通知の許可を求めず、画面に触らない。結果を写し出したら、アプリを消す」
- 入れて消すアプリの一覧 `GITURL_APPS`: `com.jonghyunkim.nativetoolkit.giturlapp`
- `approve` が出す「入れて消すアプリ」は、`GitUrlApp/IosLaunch` のときは `GITURL_APPS` だけ（今の式は Player のテストのほかを UI のテストとみなすので、直さないと UI のテストの 3 つのアプリが出る。R2-5）。これを確かめる doctest を足す
- `tests` の出力（UI のテストの一覧）からは、`PlayerTests/IosPlayer` と同じく除く。`approve` と `check-approval` では、ほかの承認と混ぜない（`mixed_kinds` を、Player のテストと giturlapp のどちらかがほかと一緒に頼まれたら断る形に広げる）。`about` の文も直す（R1-6）
- 守る木（`GUARDED`）に `scripts/verify_unity_ios_git_url.sh` を足す
- doctest を足し、`test_ios_ui.sh` の一覧（`tests` と `tests --device`）が変わらないことを確かめる

承認を頼むときに知らせる 4 つ（端末の決まり。R1-13）:

- 流す一覧と承認 ID（`--dry-run` の出力）
- 端末に起きること（区分の文と、入れて消すアプリ）
- 利用者の準備: ロックの解除、書き出しとビルドの間（約 10 分）も画面が消えないようにする、ケーブルの接続と給電。流す直前に、スクリプトが開発用の接続を確かめる（`devicectl device info details`。今のスクリプトの `device_connected` と同じ）（R1-13）
- 時間の見込み: 約 20 分

端末の決まり: 承認は 1 回限り。端末の設定は変えない。通知の許可を求めない（基本のプロジェクトには Mobile Notifications が無く、パッケージは自分から許可を求めない）。端末の識別子・Team ID・証明書の名前を出さない。ログと結果は一時フォルダーに置き、終わりに消す。

### 3.3 Release のアーカイブ（G-5、3-E の残り）

- 基本のプロジェクトを端末向け（Device SDK）に書き出し、`xcodebuild archive -configuration Release -destination generic/platform=iOS`。署名は自動、チームは `select_team` で選び、出さない
- 出すもの: アプリと 2 つの framework の `codesign -dvv` の `Authority=` の行の、`:` までの部分（`Apple Development:` など。証明書の名前は出さない。3-E の 5 章）、`TeamIdentifier=` の行の `shasum` の頭 8 文字（同じか違うかだけ）、`codesign --verify --deep --strict --verbose=2` の最後の 2 行
- 合格: 3 つとも `Apple Development:` で team の要約が同じ。`--verify` が `valid on disk` と `satisfies its Designated Requirement`。`Frameworks/` に 2.0.0 の framework が 1 つずつ
- ログとアーカイブは専用の一時フォルダーに置き、回の終わりに、成功でも失敗でも消す。ビルドのエラーは数だけ出す（`show_build_errors <log> 1`）（R1-10）

## 4. 変えるファイル

| 種類 | ファイル |
|---|---|
| 追加 | `scripts/verify_unity_ios_git_url.sh` |
| 変更 | `scripts/ios_ui_runner.py`（区分、アプリ、守る木、一覧と混ぜない扱い、doctest）、`Tools~/IosUiTests/DeviceRequirements.json`（仮のテスト、`about`） |
| 変更 | `manual/1.14.0/notification*.md`（測ったところだけ要確認から外し、残りは根拠を添えて書き直す。3 言語。R1-17） |
| 追加 | 結果の記録 `results/2026-11-22-ios-c-abi-i17-git-url-result-v1.md` |

パッケージ（`Packages/`）は変えない。

## 5. 流し方と合格

1. `scripts/verify_unity_ios_git_url.sh`（シミュレーター）: G-1〜G-6、M-1〜M-3
2. `python3 -I scripts/ios_ui_runner.py tests .` と `tests . --device` の出力が、変更の前と同じ。`approve` の doctest が、giturlapp には `GITURL_APPS` だけを出す
3. `scripts/verify_unity_ios_git_url.sh --device --dry-run` → 利用者の許可 → `--approved <ID>`: G-7

## 6. 結果が食い違ったとき

- G-1・G-2・G-6a が落ちる: git の URL の形でだけ壊れている。直してから 1.14.0 を出す
- G-4 が落ちる: まず手順（define を設定した起動の後に、別の起動で書き出したか）を疑う。段 1 の S-4 では、写しの口で Player Settings の define が効いた。手順が正しくて落ちるなら、マニュアルから Player Settings の経路を外すか、口の読み方を直す。利用者に聞く
- M-2 で delegate が `UnityNotificationManager` にならない: 読む時期、`AuthorizationRequest` が実際に呼ばれたか、Mobile Notifications 2.5.0 の版の順に疑う
- M-3 で proxy に戻らない: `Initialize()` か native-toolkit 2.0.0 の不具合。ライブラリの側なら native-toolkit に根拠を添えて知らせる

## 7. 残件（レビュー v1 で直し込まなかったもの）

- 実装のチェックリスト（A2）: R1-3（`simctl privacy` は通知に使えない。起動時の要求を切り、`AuthorizationRequest` の完了は待たない）、R1-5（Player Settings の一覧、起動ごとの `-buildTarget`、段の順。3.1 に書いた）
- 最小 iOS を既定のまま書き出して止まることを見る確かめ（利用者の形での I-16）は、今回は足さない（I-16 はゲートの EditMode のテストで見ている）
