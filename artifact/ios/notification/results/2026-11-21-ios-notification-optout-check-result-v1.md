# iOS Notification: 起動時の install を外したビルドで Initialize() が delegate を入れることの確かめ 結果 v1

- 記録日: 2026-11-21（2026-11-22 に書いた）
- 設計: [../designs/2026-11-21-ios-notification-optout-check-design-v2.md](../designs/2026-11-21-ios-notification-optout-check-design-v2.md)（OO-1〜OO-4）
- 設計のレビュー: [../reviews/2026-11-21-ios-notification-optout-check-design-review-v1.md](../reviews/2026-11-21-ios-notification-optout-check-design-review-v1.md)
- 実装のレビュー: [artifact/topics/ios-c-abi-2/reviews/2026-11-21-ios26-ui-and-optout-implementation-review-v1.md](../../../topics/ios-c-abi-2/reviews/2026-11-21-ios26-ui-and-optout-implementation-review-v1.md)（iOS 26.2 の手当てと一緒に。サブエージェントと Codex）
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 4（段 4 の計画の項目 5）。[実装の記録](2026-11-15-ios-notification-cabi-implementation-result-v1.md) の 6 章の残り
- コミット: `808ec8c`

## 1. 何を確かめたか

`NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` で起動時の install を外したビルドで、次の 4 つ（設計 v2 の 1 章）。

| # | 時点 | 見ること |
|---|---|---|
| OO-1 | 書き出し | フックが define を読み、`.mm` を足さない |
| OO-2 | 起動の後、`Initialize()` の前 | delegate が proxy ではない。受け手の登録は `NOT_INITIALIZED` |
| OO-3 | `Initialize()` の後 | delegate が proxy。Manager の受け手が登録される（警告が出ない） |
| OO-4 | 2 回目の `Initialize()` | proxy のまま。エラーが出ない |

## 2. どう確かめたか

- `scripts/verify_unity_ios.sh --skip-install`。新しい `Test iOS SkipInstall Profile`（Scripting Defines に `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`）で書き出し、カテゴリ `IosNotificationOptOut` だけを流す
- Profile は Unity の API で作った。`scriptingDefines` を入れた後も `m_HasScriptingDefines` が false のままで、それも入れる必要があった（設計 v2 の 6 章が挙げた直列化のフラグ）
- OO-1 はスクリプトが書き出しの後に見る（書き出しのログの行、`NtkNotificationInstaller.mm` のファイル、`project.pbxproj`）。`--skip-install` でない回では逆を見る（installer が入っている）
- OO-2〜OO-4 は `Assets/NativeToolkitIosPlayerTests/Probe/IosNotificationOptOutProbeTests.cs` の 1 つのテスト（`OO_InitializeInstallsTheDelegate`）で順に見る

## 3. 結果

シミュレーター（iPhone 16 / iOS 18.3）で成功した。2026-11-21 22:43 と、dist の写し直し（`1b3c424`）の後の 2026-11-22 00:02 の 2 回。

- 書き出しのログ: `installer entries: 0 (skip: True)`（OO-1）
- Player のログ:

| 段 | ログ |
|---|---|
| 起動の後（OO-2） | `delegate=<none>`。Manager の警告「the interaction listener was not registered」。`listener=2`（`NOT_INITIALIZED`） |
| `Initialize()` の後（OO-3） | `delegate=IosLibrary.NotificationDelegateProxy`、`listener=0`（`NONE`）。Manager の警告は出ない |
| 2 回目の `Initialize()`（OO-4） | `delegate=IosLibrary.NotificationDelegateProxy` |

- ログの形は設計 2.4 と違う。設計は 1 段に 1 行（`optout <段> delegate=… listener=…`）。実装は delegate の行（`optout <段> delegate=…`）と listener の行（`optout listener=…`）の 2 行に分かれ、listener の行に段の名前が無い（実装のレビュー v1 の I-11。設計は直さず、ここに書く）
- Player Settings の Scripting Define Symbols に入れる経路は、I-17 の G-4 で確かめた（[artifact/topics/ios-c-abi-2/results/2026-11-22-ios-c-abi-i17-git-url-result-v1.md](../../../topics/ios-c-abi-2/results/2026-11-22-ios-c-abi-i17-git-url-result-v1.md)。`installer entries: 0 (skip: True)`、`.mm` が無い）

## 4. 残ること

- 実機では流していない（設計 v2 の 4 章。delegate を入れる時期と方法はシミュレーターと実機で違わないので、作らないと決めた）
- 設計 v2 の 1 章で外したもの: 通知のタップが `NotificationActionReceived` に届くこと、起動の後に入れたときにアプリを起動したタップが届くか。Mobile Notifications との共存は、I-17 の M-1〜M-3 で 2.0.0 でも確かめた（シミュレーター）
