# iOS Notification: 起動時の install を外したビルドで Initialize() が delegate を入れることの確かめ（設計 v1）

- 日付: 2026-11-21
- 元: 段 4 の計画（[../../../topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-stage4-plan-v1.md](../../../topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-stage4-plan-v1.md)）の項目 5 の「起動時の install を無効にした（`NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`）状態から `Initialize()` で入れ直すことの確かめ」。マニュアル 1.14.0 の `notification*.md` に残る最後の要確認
- 範囲: シミュレーターだけ。実機の回は作らない（4 章）

## 1. 確かめたいこと

マニュアルは利用者に次のように書いている。

1. Player Settings（iOS）の Scripting Define Symbols か、ビルドに使うビルドプロファイルの Scripting Defines に `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` を足すと、ビルドは `NtkNotificationInstaller.mm` を書き出した Xcode プロジェクトに足さない
2. そのとき、起動時に delegate は入らない。Manager はイベントの受け手を登録できず、`NotificationActionReceived` などは発生しない
3. `Initialize()` を呼ぶと delegate が入り、受け手が登録される

どれも実測が無い。特に 1 は、define を読むのが Editor のフック（`Editor/iOS/IosXcodeProjectSetup.cs` の `#if`）なので、**ビルドプロファイルの Scripting Defines が Editor のアセンブリの組み立てにも効くか**が要。Player のテストの回が足す define（`IosPlayerBuildSetup` の `extraScriptingDefines`）は Player のスクリプトにだけ効き、Editor のフックには届かないので、この確かめには使えない。

| 番号 | 確かめ | 合格の条件 |
|---|---|---|
| OO-1 | 書き出し | define を持つプロファイルで書き出した Xcode プロジェクトに `Libraries/com.jonghyunkim.nativetoolkit/NtkNotificationInstaller.mm` が無く、`project.pbxproj` もそのファイルを指さない |
| OO-2 | 起動の後、`Initialize()` の前 | 通知センターの delegate がライブラリの proxy（`IosLibrary.NotificationDelegateProxy`）ではない。C ABI の `ntk_notification_add_interaction_listener` が `NOT_INITIALIZED` を返す |
| OO-3 | `Initialize()` の後 | delegate が proxy になる。`ntk_notification_add_interaction_listener` が `NONE` を返す（登録した受け手はテストの中で外す） |
| OO-4 | 2 回目の `Initialize()` | delegate は proxy のまま。エラーのログが出ない |

確かめないこと（理由）:

- 通知のタップが `NotificationActionReceived` に届くこと。Manager のイベントは、受け手の登録（OO-3）が済めば、既定のビルドと同じ経路（proxy → 受け手 → Manager）を通る。既定のビルドでは UI テスト（NU 系）で確かめ済み。届くことを見るには通知の許可と UI のハーネスの define 版が要り、手間に見合わない
- 起動の後に入れたとき、アプリを起動したタップが届くか。ライブラリは「起動の後の install では起動のタップが失われうる」とログに出し、`noteFirstInstall` で起動の判定を閉じる作り（native-toolkit の `NotificationDelegateInstaller.swift`、`NotificationDelegateRouter.swift`）。マニュアルは 2026-11-21 に「届かないことがある」と書いた（`910a043`）。確かめるにはコールドスタートのタップが要り、シミュレーターの UI ハーネスの define 版が要る。今回は外す
- Mobile Notifications との共存。1a の試作での実機の実測（U-4）がある。2.0.0 での再測定は今回の範囲の外

## 2. 仕組み

### 2.1 ビルドプロファイル

`Assets/Settings/Build Profiles/Test iOS SkipInstall Profile.asset` を足す。`Test iOS Profile.asset` の写しで、違いは次の 2 つだけ。

- `m_ScriptingDefines: [NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL]`
- 名前と GUID（`.meta`）

`Test iOS Profile` を元にする理由: `NATIVETOOLKIT_ENABLE_BUILD_STEPS` が無く、dist からの写しが走らない（`--as-package-user` の回と同じ形）。サンプルのシーンも入らないが、この回は使わない。

利用者が「ビルドプロファイルの Scripting Defines に足す」と書いたとおりの形で確かめるので、Player Settings の Scripting Define Symbols は使わない（変えると `ProjectSettings` を元に戻す手間と危険がある）。

### 2.2 `verify_unity_ios.sh --skip-install`

新しい選択肢。シミュレーターだけ（`--device` と一緒には断る）。

- `PROFILE` を 2.1 のプロファイルにする
- Player の回は 1 つだけ。カテゴリ `IosNotificationOptOut` だけを流す。手伝い役（UI ドライバー）は要らない（`--category` と同じく、手伝い役のテストを要求しない）。Notification のサンプルの回は流さない
- EditMode と Editor の PlayMode は、既定どおり流す（`--skip-editmode` で外せる）
- 書き出しの後、Xcode のビルドの前に OO-1 を確かめる。`Libraries/com.jonghyunkim.nativetoolkit/NtkNotificationInstaller.mm` があるか、`project.pbxproj` に `NtkNotificationInstaller.mm` の文字があれば、回を落とす
- 既定の回（選択肢なし）にも逆の確かめを足す: 書き出した Xcode プロジェクトに installer があり、`project.pbxproj` がそれを指す。OO-1 の確かめ方そのものが正しいことの照らしになる

書き出しはいつもどおり `-activeBuildProfile` でプロファイルを渡す。Unity は起動時にそのプロファイルの Scripting Defines で Editor のアセンブリも組み直すはずで、OO-1 が通ればそれが確かめられる。通らなければ、マニュアルの「ビルドプロファイルの Scripting Defines に足す」は誤りで、書き直しが要る（6 章）。

### 2.3 テスト

`Assets/NativeToolkitIosPlayerTests/Probe/` に `IosNotificationOptOutProbeTests.cs` を足す（プローブの `ntkpt_notification_delegate_class` を使うため、既存のプローブのテストと同じアセンブリ）。

- `#if UNITY_IOS && !UNITY_EDITOR && NTK_IOS_PLAYER_PROBE && NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`
- `[Category("IosNotificationOptOut")]`。`IosPlayer` は付けない（既定の回では define が無く、コンパイルで消えるが、念のため既定のゲートのカテゴリと分ける）
- テストは 1 つ（`OO_InitializeInstallsTheDelegate`）。delegate の入れ直しはプロセスに 1 回しか戻せないので、OO-2 → OO-3 → OO-4 を 1 つのテストの中で順に行う。NUnit の順序に頼らない
- C ABI は `[DllImport("__Internal")]` で直接呼ぶ（`ntk_notification_add_interaction_listener`、`ntk_notification_remove_interaction_listener`）。パッケージの internal（`IosNotificationCApi`）には触らない。値は `Notification.h` から写し、写した行をコメントに書く
- OO-2 で `IosNotificationManager.Instance` を先に作る（Awake が受け手の登録を試み、`NOT_INITIALIZED` で警告を出すのが正しい形）。その警告は `LogAssert.Expect` で受ける
- OO-3 で `IosNotificationManager.Instance.Initialize()` を呼び、delegate のクラス名と、受け手の登録の結果を見る
- 既存の `IosNotificationDelegateProbeTests.InstallerSetTheDelegate`（カテゴリ `IosPlayer`）には `IosNotificationOptOut` を足さない。足すと、同じ回の中で OO テストより後に流れたときに delegate が proxy になっていて落ちる。その `#if NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` の分岐（proxy ではない）は、OO テストの最初の段（OO-2）が同じことを確かめる

### 2.4 ログ

テストは各段で `[NTK-IOS-PLAYER] optout <段> delegate=<クラス名> listener=<結果>` を出す。クラス名はシステムの型の名前で、利用者のデータではない。

## 3. 変えるファイル

| 種類 | ファイル | 中身 |
|---|---|---|
| 追加 | `Assets/Settings/Build Profiles/Test iOS SkipInstall Profile.asset`（と `.meta`） | 2.1 |
| 追加 | `Assets/NativeToolkitIosPlayerTests/Probe/IosNotificationOptOutProbeTests.cs`（と `.meta`） | 2.3 |
| 変更 | `scripts/verify_unity_ios.sh` | `--skip-install`、OO-1 の確かめと、既定の回の逆の確かめ |
| 変更 | `manual/1.14.0/notification*.md` | 結果に合わせて要確認のコメントを外す（6 章） |
| 変更 | 段 4 の計画の記録、トピックの README | 項目 5 の完了 |

パッケージ（`Packages/`）は変えない。

## 4. 実機

作らない。delegate を入れる時期と方法は、シミュレーターと実機で違わない（3-S の V-3・V-4 は、既定の形でシミュレーターと実機の両方で同じ結果だった）。define の有無は Editor の書き出しの差で、端末には関係しない。

## 5. 流し方と合格

1. `scripts/verify_unity_ios.sh --skip-install --skip-editmode`（シミュレーター、iPhone 16 / iOS 18.3）。OO-1〜OO-4 が通る
2. `scripts/verify_unity_ios.sh`（既定のゲート全体）。既定の回の逆の確かめが通り、ほかも今までどおり通る。2.1 のプロファイルを足しても既定の回に影響しないことの確かめを兼ねる

## 6. 結果がマニュアルと食い違ったとき

- OO-1 が落ちる（プロファイルの define が Editor のフックに届かない）: マニュアルから「ビルドプロファイルの Scripting Defines」を外し、Player Settings だけにするか、フックの読み方を変える（`#if` ではなく、ビルドのときの define の一覧を読む）。どちらにするかは利用者に聞く
- OO-2 が落ちる（define があっても起動時に入る）: 別の経路で入っている。原因を調べてから決める
- OO-3・OO-4 が落ちる: `Initialize()` の不具合。直す
