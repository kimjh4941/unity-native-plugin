# I-17: git の URL で入れた利用者の形での確かめの結果 v1

- 記録日: 2026-11-22
- 設計: [../designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v3.md](../designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v3.md)（レビュー 2 回。v1 の A1 は 1、v2 で 0）
- スクリプト: `scripts/verify_unity_ios_git_url.sh`（シミュレーター、`--device --dry-run` / `--device --approved <ID>`）
- 時点: パッケージは `808ec8c`（git の URL の `#` に HEAD のハッシュ）。dist は native-toolkit の `68ca7674`（IosLibrary 20、capi 4）。Unity 6000.4.2f1、Xcode 26.3、シミュレーターは iPhone 16 / iOS 18.3

## 1. シミュレーターの結果

3 回流した。

- 1 回目: 02:37〜02:50（下の表）
- 2 回目: 02:51〜03:03。実機の部分を足した後。結果は同じ
- 3 回目: 03:12〜03:24。実装のレビュー v1（[../reviews/2026-11-22-ios-c-abi-i17-implementation-review-v1.md](../reviews/2026-11-22-ios-c-abi-i17-implementation-review-v1.md)）を直した後の、今のスクリプト。結果は同じ。加えて、結果のファイルに回ごとの `run=<id>` があり、G-5 の `Frameworks/` に `IosLibrary.framework` と `IosLibraryCApi.framework` が 1 つずつ、どちらも版 2.0.0（ほかに Unity の `UnityFramework`・`UnityRuntime`）。回の後、起動したままのシミュレーターは無い

1 回目と 2 回目は、回が起動したシミュレーターを止めない不具合があった（レビューの I-2）。2 回目の後に手で落とした

| 番号 | 確かめ | 結果 |
|---|---|---|
| G-1 | 組み立て | 成功。`NativeToolkit.BuildTools.Editor`・`NativeToolkit.Editor`・`NativeToolkit.Runtime` が組まれ、`BuildProcessors` と Tests のアセンブリは無い。コンパイルのエラー 0 |
| G-2 | iOS の書き出し | 成功。`linked and embedded: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework; installer entries: 1 (skip: False)`。`.mm` があり、pbxproj の Sources が指す |
| G-3 | シミュレーターでの起動 | 成功。`started`、`version=0x020000`、`delegate=IosLibrary.NotificationDelegateProxy` |
| G-5 | Release のアーカイブ（3-E の残り） | 成功。アプリ・`IosLibrary.framework`・`IosLibraryCApi.framework` がすべて `Apple Development:` で team の要約が同じ。`codesign --verify --deep --strict` が `valid on disk`、`satisfies its Designated Requirement`。証明書の名前と team の値は出していない。アーカイブとそのログは回の中で消した |
| G-6a | Android のビルド（ARM64） | 成功。`[AndroidGradleDependencies] ... native-toolkit runtime dependencies in` の行が出た |
| G-6b | Android の CPU アーキテクチャの確かめ | 成功。ARMv7 を足したビルドが `Native Toolkit for Android supports ARM64` で止まった |
| G-4 | Player Settings の define | 成功。define を設定する起動と書き出す起動を分けて、`installer entries: 0 (skip: True)`、`.mm` が無く、pbxproj にも無い |
| M-1 | 共存: 起動の後 | 成功。`delegate=IosLibrary.NotificationDelegateProxy` |
| M-2 | 共存: Mobile Notifications 2.5.0 の `AuthorizationRequest`（仮の許可、遠隔通知の登録なし） | 成功。`m2 delegate=UnityNotificationManager`。許可のダイアログは出なかったと見る: 画面は見ていないが、アプリは M-3 と `done` まで書けた（ダイアログが出るとアプリは inactive になり、Unity が止まって `done` まで進まない） |
| M-3 | 共存: `Initialize()` | 成功。`m3 delegate=IosLibrary.NotificationDelegateProxy`。ライブラリのログ: `[NotificationDelegateInstaller] [install] the delegate was replaced by UnityNotificationManager; installing the proxy again in front of it` |

分かったこと:

- git の URL の形でも、Editor のアセンブリ（1.13.0 までは組まれなかった）が組まれ、Android のビルドの段（Gradle の依存の追加、CPU アーキテクチャの確かめ）と iOS の口（xcframework、通知の `.mm`）が動く
- define は、ビルドプロファイルの Scripting Defines（opt-out の確かめ）でも、Player Settings の Scripting Define Symbols（G-4）でも効く。ただし、Player Settings を変えた同じ Unity の起動の中では Editor のアセンブリが組み直されないので、変えた後の書き出しで効く
- Mobile Notifications 2.5.0 は `AuthorizationRequest` で通知の delegate を上書きする（1a の U-4 と同じ）。`Initialize()` で proxy に戻り、上書きした delegate を前の delegate として残す。マニュアルの「別の通知パッケージが delegate を入れた後で `Initialize()` を呼ぶ」は、native-toolkit 2.0.0 でもそのとおり

## 2. 実機（G-7）

2026-11-22 08:18〜08:22、iPhone XS / iOS 18.7.2、HEAD `a764789`。利用者の許可を得て、承認 ID を 1 回使った。

- 成功。結果のファイルは `started`、回ごとの `run=<id>`、`version=0x020000`、`delegate=IosLibrary.NotificationDelegateProxy`、`done`
- アプリ（`com.jonghyunkim.nativetoolkit.giturlapp`）は回の終わりに消え、消えたことを確かめた（`cleanup: the app is removed`）
- 回の一時フォルダーは残っていない。手元のログも消した

同じ朝、同じ HEAD で、いつもの実機の回も流した（今夜の手伝い役の直しを iOS 18 の実機でも確かめるため）: ゲート（Player 112 件、Notification のサンプル 8 件）と UI テスト 25 件がすべて成功。SH-13 は、押してから失敗の行が見えるまで 2.56 秒（上限の値）。

## 3. 途中で起きたこと

- 最初の回の前に、スクリプトが標準出力に返す値（アプリの道、結果のファイル）に、`run_limited` の進み具合の表示が混ざる作りだった。流す前に読み返して直した（表示とエラーを標準エラーへ）
