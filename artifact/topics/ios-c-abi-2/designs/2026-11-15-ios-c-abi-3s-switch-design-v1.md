# iOS の C ABI 移行の段 3-S（切り替え）の設計 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 親 | [artifact/topics/ios-c-abi-2](../README.md)。進め方は [2c-plan-design-v4](2026-11-14-ios-c-abi-2c-plan-design-v4.md) の 3 章の 3-S の行と、決定 I-1・I-2・I-4・I-11・I-14〜I-17 |
| 目的 | パッケージの iOS を native-toolkit の C ABI `2.0.0`（`dist/1.14.0/ios`）に切り替える。4 機能はまだ移さず、「使えない」に落とす（Dialog と Notification には仮の経路）。どのコミットでも iOS の Player が組めるようにし、段 3 で機能を 1 つずつ移す |
| 前提 | 段 1 の結果（README の `6.3`）: Unity が `Plugins/iOS` の xcframework をリンクし、`.meta` の `AddToEmbeddedBinaries: true` で Embed する。口が足すのは `.mm` だけ。`UserNotifications.framework` は要らない。2.0.0 の framework は `minos 18.0` で、17.x では黙って終わる |
| **実装の時機** | **実機の Notification の回（`test_ios_ui.sh --device --keep-installed`、利用者の準備が要る）の後**。決定 I-12 は「1.x の実機の記録は 3-S の前に」。3-S の後に 1.x を記録するには基準のコミットの worktree が要り、同じ bundle id で通知の権限が引き継がれる（I-12 の但し書き） |

## 1. 変えるもの

### 1.1 xcframework と版の固定（I-4）

- `Plugins/iOS/` の `ios-native-toolkit-1.3.0.xcframework`・`unity-ios-native-toolkit-1.3.0.xcframework` と `.meta` を消し、`dist/1.14.0/ios` の `ios-native-toolkit-2.0.0.xcframework`・`ios-native-toolkit-capi-2.0.0.xcframework` を置く
- 2 つの `.meta` は iOS だけ有効、iOS の設定に `AddToEmbeddedBinaries: true`（S-2）。ほかの設定は付けない
- `VERSION.txt` を `dist_version: 1.14.0`、`xcframeworks: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework` にする
- `dist/1.14.0/ios/include` のヘッダーは写さない（C# の宣言の照合は I-15 のテストが native-toolkit のヘッダーを読む。1.3 を見る）

### 1.2 Xcode のプロジェクトに入れる口（I-1・I-2）

- `Editor/iOS/IosXcodeProjectSetup.cs`（`NativeToolkit.Editor`、`IPostprocessBuildWithReport`、`#if UNITY_IOS`）: 書き出したプロジェクトで、2 つの framework が UnityFramework のリンクとアプリの Embed に入っていることを確かめ、無ければ分かりやすい文言でビルドを失敗にする（足しはしない。S-1・S-2）。`NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` が無ければ `Editor/iOS/NativeSource~/IosNotificationInstaller.mm` を UnityFramework に足す（冪等。道は `Path.GetFullPath("Packages/...")`。git の URL で入れた形でも `Library/PackageCache` の道で動く）
- `IosNotificationInstaller.mm`: `didFinishLaunchingWithOptions` の中で `ntk_notification_install()` を呼び、戻り値を `NSLog` する（段 1 の写しと同じ形。UnityAppController の分類で差し込む）
- 前後の数（リンク・Embed・`.mm`）をログに出す
- `PostBuildProcessor` の iOS の部分（framework を写してリンクと Embed を足す処理）は消す（Unity がする）。macOS の部分は残す。`PreBuildProcessor` の dist からの写しは開発の道具として残す（1.14.0 を写す）
- メニュー「Configure Xcode Project」は段 4 で消す（3-S では触らない）

### 1.3 共通部と版の確かめ（I-14）

- `Runtime/iOS/Common/IosNativeToolkitCApi.cs`: `[DllImport("__Internal")] uint ntk_version()` と、`IsAvailable`（iOS の Player で、`ntk_version() >> 16 == 2`。それ以外は false）。結果は一度だけ読んで覚える
- 3-S の時点で使うのは版の確かめだけ（機能は段 3 でこれを通して C ABI を呼ぶ）

### 1.4 宣言の照合（I-15）

- EditMode のテスト `IosCApiDeclarationTests`: パッケージの `Runtime/iOS` の中の `DllImport("__Internal")` のうち `ntk_` で始まる宣言の名前を、リフレクションで集め、native-toolkit のヘッダー（`dist/1.14.0/ios/include/NativeToolkitC/*.h` の `NTK_CALL <名前>(`）にあるかを照らす。宣言の数も出す（3-S では `ntk_version` の 1 つ）。ヘッダーが見つからないとき（native-toolkit が隣に無い利用者の形）は `Assert.Ignore` ではなく失敗にし、テストはこのリポジトリの開発の場でだけ組む（`Tests/EditMode` は利用者に配らない）
- ブリッジの旧い宣言（`DllImport("__Internal")` で `ntk_` で始まらないもの）が残っていないことも照らす（3-S で 0）

### 1.5 最小 iOS（I-16）

- Player Settings の iOS の最小を 18.0 にする（`ProjectSettings`。`iOS Profile` はもう 18.0）
- `Editor/iOS/IosMinimumVersionCheck.cs`（`IPreprocessBuildWithReport`、`#if UNITY_IOS`）: iOS のビルドで `PlayerSettings.iOS.targetOSVersionString` が 18.0 より低ければ、「native-toolkit 2.0.0 は iOS 18.0 以上。17.x では起動直後に黙って終わる（README の `6.3` の S-8）」の文言で止める

### 1.6 ブリッジの宣言を消し、4 機能を「使えない」に落とす（I-11 の案 D）

旧いブリッジの `DllImport` の宣言は、Clipboard 15、Dialog 6、Notification 22、Share 1 の **44**（`grep -c DllImport` で数えた。README の「43」は数え直す）。どれも消し、ネイティブの呼び出し（`Initialize`・`OnDestroy` の中を含む）も消す。

| 機能 | 3-S の後の iOS の Player の振る舞い |
|---|---|
| Clipboard | 今の Editor の経路（`IsBridgeAvailable()` が false のとき）と同じ「使えない」の失敗（`CLIPBOARD_BRIDGE_UNAVAILABLE`、`<操作> is only available on an iOS device.`）。`IsBridgeAvailable()` は iOS の Player でも false を返すようにする |
| Share | 今の iOS 以外の経路と同じ `iOS share is only available on an iOS device.` の失敗。`#if UNITY_IOS && !UNITY_EDITOR` の中のネイティブの呼び出しを、その失敗に替える |
| Dialog | **仮の失敗の経路を足す**（案 1。Dialog の設計で 3-S と同じコミットで移す案 2 もあるが、3-S を 4 機能から切り離して確かめるため案 1 を推す）。6 つの `Show*` は、引数の確かめ（今の C# の確かめ）の後、各イベントを `(null…, false, "Dialog is not available yet on this build.")` で、同じ呼び出しの中で 1 回出す（今の C# の確かめの失敗と同じ時点。ID-20〜22 と同じ形） |
| Notification | **結果を返すすべての API に仮の経路を足す**。今の Editor の経路は受け手を保って戻るだけで結果が返らない（`IosNotificationManager.cs`）。3-S では、次の値で次の `Update` に 1 回返す（I-7 と同じ時点）: `RequestPermission`・`ShowNotification`・`ScheduleNotification`・`UpdateNotification`・`SetBadgeCount`・`RegisterCategory` は `IosNotificationResult.Failure(<操作>, "Notification is not available yet on this build.")`（`NotificationOperationCompleted` も出す）、`HasPermission` は `false`、`GetAuthorizationStatus` は `Unknown`（`NotDetermined` だと、アプリが許可を求めにいくと読める）、`GetScheduledNotifications`・`GetDeliveredNotifications` は `[]`。取り消し・消去・`RemoveCategory`・`OpenNotificationSettings` は何もしない。`Initialize` は何もしない（delegate は `.mm` が入れる） |

文言は仮のもので、段 3 で各機能を移すときに消える。

### 1.7 テスト

- **定義**: 3-S で Player Settings（iOS）の Scripting Define Symbols に `NTK_IOS_CABI_SWITCHED` を足す。機能を移す段で `NTK_IOS_CABI_2_<FEATURE>`（`DIALOG`・`SHARE`・`CLIPBOARD`・`NOTIFICATION`）を足す（README の段 3 の行の決まり）。どちらもこのリポジトリの `ProjectSettings` だけで、利用者には配らない
- **Player のテスト**: 各機能のテストのファイルは `#if NTK_IOS_CABI_SWITCHED && !NTK_IOS_CABI_2_<FEATURE>` のとき、1.x のテストを組まず、代わりに「使えない」を照らすテストを 1 つずつ組む（例: `IosSharePlayerTests.UnavailableUntilMoved`。1.6 の値を全文で照らす。Notification はすべての結果の API を 1 回ずつ）。機能を移したら、その機能の 1.x のテスト（`NTK_IOS_CABI_2_<FEATURE>` の側の期待つき）に戻る
- **版のテスト**: `IosCApiVersionPlayerTests.VersionIsTwo`（`ntk_version() == 0x020000`）と、`.mm` が動いたことの確かめ（`.mm` の `NSLog` は読めないので、`.mm` が `ntk_notification_install` の戻り値を `NSUserDefaults` の決まったキーに書き、テストがそれを読む。`NONE`（0）を照らす）
- **UI のテスト**: 移していない機能の UI のテストは流さない（ハーネスの最初の行が `[abi@event] 2` になり、1.x の期待で落ちるため）。Dialog・Share・Clipboard の各シナリオにも `[abi@event] 1|2` の行を足し、機能を移したときに使う
- **EditMode と Editor の PlayMode**: 今のまま通る（iOS の経路は Editor では組まれない）。`IosCApiDeclarationTests` を足す

## 2. 確かめ

| # | 確かめること | どこで |
|---|---|---|
| V-1 | EditMode、Editor の PlayMode が通る（`IosCApiDeclarationTests` を含む） | `verify_unity_ios.sh` |
| V-2 | iOS の書き出し（IL2CPP）と Xcode のビルドが通り、口のログでリンク 2、Embed 2、`.mm` 1 | シミュレーター |
| V-3 | iOS の Player のテスト: `VersionIsTwo`、`.mm` の `NONE`、4 機能の `UnavailableUntilMoved` が通る | シミュレーター、実機（許可を得て） |
| V-4 | `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` で `.mm` が入らない（口のログで `.mm` 0、`.mm` の確かめのテストは「書かれていない」） | シミュレーター |
| V-5 | 最小 iOS を 17.0 にすると `IosMinimumVersionCheck` がビルドを止める（Player Settings を戻す） | Editor の CLI |
| V-6 | I-17: 使い捨てのプロジェクトに `git+file:///<このリポジトリ>?path=Packages/com.jonghyunkim.nativetoolkit#<3-S のコミット>` で入れ、コンパイル、iOS の書き出し、Embed と `.mm`、シミュレーターで起動（`ntk_version`）。実機でも起動（許可を得て） | シミュレーター、実機 |
| V-7 | Android と macOS のビルドの処理に影響が無い（`PostBuildProcessor` の macOS の部分、Android の書き出し） | Editor の CLI |

## 3. コミットの分け方

- 3-S は 1 つのコミットにする（xcframework を替えると旧い宣言はリンクできず、`.mm` は 1.x にリンクできないので、途中で組めない形を作らない）。文書は別のコミット
- その前に、xcframework に関わらない足し物（`IosMinimumVersionCheck` と Player Settings の 18.0）を 1 つ前のコミットにしてもよい（1.x でも組める）

## 4. 決めないこと・後で決めること

| もの | どこで |
|---|---|
| 各機能の写し方（I-5〜I-9） | 段 3 の機能ごとの設計 |
| メニュー「Configure Xcode Project」と `IosFrameworkPatcher` の扱い | 段 4 |
| native-toolkit への結果の返し | 3-S の終わりに、利用者に聞いてから |
