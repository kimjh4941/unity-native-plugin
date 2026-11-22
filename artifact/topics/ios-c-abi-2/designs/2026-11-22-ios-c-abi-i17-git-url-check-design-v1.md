# I-17: git の URL で入れた利用者の形での確かめ（設計 v1）

- 日付: 2026-11-22
- 元: 段 4 の計画（[2026-11-15-ios-c-abi-stage4-plan-v1.md](2026-11-15-ios-c-abi-stage4-plan-v1.md)）の項目 8、設計書 v4 の I-17、3-E の残り（[../results/2026-11-15-ios-c-abi-3e-resign-result-v1.md](../results/2026-11-15-ios-c-abi-3e-resign-result-v1.md) の 4 章）、マニュアル 1.14.0 の `notification*.md` の最後の要確認
- 利用者の判断（2026-11-21 夜。トピックの README の段 4 の行）:
  - Android は端末なし。git の URL の形で、ビルドとビルドの段が動くことまで。Android の本体は 1.13.0 から変わっていない
  - iOS は実機で起動する。使い捨てのアプリは承認の仕組みに足す
  - 3-E の残りは Personal Team で、端末なし
  - Mobile Notifications との共存は、2.0.0 でシミュレーターで測る

## 1. 確かめること

このリポジトリでは、パッケージが `Packages/` に埋め込まれていて、git の URL で入れた利用者と振る舞いが違う（README 6.2。`NativeToolkit.Editor` の asmdef の件、`c1aeca0`）。使い捨ての Unity プロジェクトに、`git+file:///<このリポジトリ>?path=Packages/com.jonghyunkim.nativetoolkit#<HEAD>` でパッケージを入れて確かめる。

| 番号 | 確かめ | 端末 | 合格の条件 |
|---|---|---|---|
| G-1 | 組み立て | なし | パッケージの Runtime・Editor・BuildProcessors などのアセンブリが `Library/ScriptAssemblies` にあり、コンパイルのエラーが 0 |
| G-2 | iOS の書き出し | なし | 書き出しのログに `installer entries: 1 (skip: False)` と、2 つの xcframework の `linked and embedded` がある。Xcode のプロジェクトが 2 つの framework を Embed する |
| G-3 | シミュレーターでの起動 | シミュレーター | アプリが起動し、結果のファイルに `started`、`ntk_version=0x020000`、通知の delegate のクラスが `IosLibrary.NotificationDelegateProxy` と書かれる |
| G-4 | Player Settings の define | なし | Player Settings（iOS）の Scripting Define Symbols に `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` を足して書き出すと、ログが `installer entries: 0 (skip: True)`。使い捨てのプロジェクトはビルドプロファイルを使わない（Player Settings だけが効く形） |
| G-5 | Release のアーカイブ（3-E の残り） | なし | `xcodebuild archive -configuration Release`、署名は自動（Personal Team）。アプリと 2 つの framework の `Authority=` が `Apple Development:` で、team の要約が同じ。`codesign --verify --deep --strict` が通る。出し方は 3-E と同じ（証明書の名前と team の値は出さない） |
| G-6 | Android のビルド | なし | Android の APK のビルドが成功し、Android のビルドの段（`NativeToolkit.Editor` の Gradle の依存の追加と CPU アーキテクチャの確かめ）のログが出る |
| G-7 | iOS の実機での起動 | 実機（承認） | G-3 と同じ条件を、iPhone XS で |
| M-1〜M-3 | Mobile Notifications との共存 | シミュレーター | 2 章 |

## 2. Mobile Notifications との共存（M-1〜M-3）

マニュアルは「別の通知パッケージが自分の delegate を入れたら、その後で `Initialize()` を呼ぶ。このパッケージの delegate を入れ直し、今の delegate を前の delegate として残す。このパッケージが出していない通知のタップは、前の delegate に渡る」と書く。1a の試作（U-4）では、Mobile Notifications 2.5.0 の `AuthorizationRequest` が delegate を上書きし、前の delegate に転送しないことが分かっている。

使い捨てのプロジェクトに `com.unity.mobile.notifications`（2.5.0。U-4 と同じ版）を足し、G-3 のアプリに次の段を足す。delegate のクラス名は、Player のテストのプローブと同じ `.mm`（`UNUserNotificationCenter.currentNotificationCenter.delegate` のクラス名を返す）を使い捨てのプロジェクトに置いて読む。

| 番号 | 段 | 合格の条件 |
|---|---|---|
| M-1 | 起動の後 | delegate が proxy |
| M-2 | Mobile Notifications の `AuthorizationRequest` を作って終わるまで待つ | delegate が proxy ではなくなる（Mobile Notifications のもの）。1a の U-4 と同じ。シミュレーターの許可のダイアログは `xcrun simctl privacy <udid> grant notifications <bundle id>` で前もって許しておく（ダイアログを出さない） |
| M-3 | `IosNotificationManager.Instance.Initialize()` | delegate が proxy に戻る。ライブラリの受け手の登録が `NONE`。前の delegate（Mobile Notifications）が proxy の中に残っていることは、ライブラリのログ（`installing the proxy again in front of it`）で見る |

確かめないこと（理由）:

- タップの転送（このパッケージが出していない通知のタップが、前の delegate に届くこと）。確かめるには通知のバナーのタップが要り、UI の手伝い役を使い捨てのアプリに足すことになる。転送は native-toolkit の 1b のシミュレーターのテスト（転送しない偽の delegate を使ったもの）で確かめ済みで、Unity の側の関わりは `Initialize()` が `ntk_notification_install` を呼ぶことだけ（M-3 で見る）
- 実機での共存。シミュレーターで delegate の入れ替わりを見れば、端末で変わる理由が無い

## 3. 仕組み

### 3.1 スクリプト `scripts/verify_unity_ios_git_url.sh`

新しく足す。使い捨てのプロジェクトは一時フォルダーに作り、終わりに消す。リポジトリの作業ツリーには何も書かない。

- `--simulator`（既定）: G-1〜G-6 と M-1〜M-3
- `--device --dry-run` / `--device --approved <ID>`: G-7。G-1・G-2 は端末向けの書き出しでもう一度通る
- パッケージは `git+file:///<repo>?path=Packages/com.jonghyunkim.nativetoolkit#<HEAD のハッシュ>`。HEAD のコミットに確かめたい変更が入っていること（未コミットの変更がパッケージにあれば断る）
- 使い捨てのプロジェクトの中身（スクリプトが一時フォルダーに書く）:
  - `Packages/manifest.json`: パッケージと、M のための `com.unity.mobile.notifications` 2.5.0
  - `Assets/Scripts/I17Probe.cs`: 起動したら各段を行い、結果を `Application.persistentDataPath/i17-result.txt` に書く（1 行 1 段、決まった語と数字とクラス名だけ）
  - `Assets/Plugins/iOS/I17Probe.mm`: delegate のクラス名と `ntk_version()` を返す
  - `Assets/Editor/I17Build.cs`: バッチモードの入口（iOS の書き出し、Android のビルド、Player Settings の設定）
- 段の進め方は、アプリが起動時の環境変数 `I17_STEPS`（`launch` か `launch,coexist`）で受け取る。実機（G-7）は `launch` だけ（Mobile Notifications の許可を端末で求めないため）
- 結果は、シミュレーターでは `xcrun simctl get_app_container ... data` から、実機では `devicectl device copy from --domain-type appDataContainer` で読む（ゲートと同じ）

### 3.2 承認の仕組み

`scripts/ios_ui_runner.py` に次を足す。

- 仮のテストの名前 `GitUrlApp/IosLaunch`（`Tools~/IosUiTests/DeviceRequirements.json` の `tests` に、区分 `giturlapp` で）
- 区分の文 `giturlapp`: 「このリポジトリの HEAD を git の URL で入れた使い捨てのアプリ（Unity）を入れて起動する。アプリは通知の delegate のクラス名とライブラリの版を自分のデータのフォルダーに書くだけで、通知の許可を求めず、画面に触らない。結果を写し出したら、アプリを消す」
- 入れて消すアプリ: `com.jonghyunkim.nativetoolkit.giturlapp`（使い捨てのアプリ）。ほかの承認（Player のテスト、UI のテスト）とは混ぜない（`mixed_kinds` と同じ扱い）
- 守る木（`GUARDED`）に `scripts/verify_unity_ios_git_url.sh` を足す

端末の決まり（native-toolkit の `agent-rules/device-testing.md`）:

- 承認は 1 回限り。`--dry-run` で端末に起きることと承認 ID を出し、利用者の許可を得てから `--approved`
- 端末の設定は変えない。通知の許可を求めない。端末の識別子・Team ID・証明書の名前を出さない
- ログと結果は一時フォルダーに置き、終わりに消す

## 4. 変えるファイル

| 種類 | ファイル |
|---|---|
| 追加 | `scripts/verify_unity_ios_git_url.sh` |
| 変更 | `scripts/ios_ui_runner.py`（区分、アプリ、守る木）、`Tools~/IosUiTests/DeviceRequirements.json`（仮のテスト） |
| 変更 | `manual/1.14.0/notification*.md`（G-4 と M の結果で要確認を外す） |
| 追加 | 結果の記録 `results/2026-11-22-ios-c-abi-i17-git-url-result-v1.md` |

パッケージ（`Packages/`）は変えない。

## 5. 流し方と合格

1. `scripts/verify_unity_ios_git_url.sh`（シミュレーター）: G-1〜G-6、M-1〜M-3
2. `scripts/verify_unity_ios_git_url.sh --device --dry-run` → 利用者の許可 → `--approved <ID>`: G-7

## 6. 結果が食い違ったとき

- G-1・G-2・G-6 が落ちる: git の URL の形でだけ壊れている。直してから 1.14.0 を出す
- G-4 が落ちる: マニュアルから Player Settings の経路を外すか、フックの読み方を直す。利用者に聞く
- M-2 で delegate が替わらない: 2.0.0 では Mobile Notifications が上書きしない形になった。マニュアルの書き方はそのままでよいが、記録に書く
- M-3 で proxy に戻らない: `Initialize()` かライブラリの不具合。ライブラリの側なら native-toolkit に根拠を添えて知らせる
