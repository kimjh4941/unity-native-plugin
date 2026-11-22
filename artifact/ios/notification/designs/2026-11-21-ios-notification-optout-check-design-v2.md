# iOS Notification: 起動時の install を外したビルドで Initialize() が delegate を入れることの確かめ（設計 v2）

- 日付: 2026-11-21
- 元: 段 4 の計画（[../../../topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-stage4-plan-v1.md](../../../topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-stage4-plan-v1.md)）の項目 5 の「起動時の install を無効にした（`NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`）状態から `Initialize()` で入れ直すことの確かめ」。マニュアル 1.14.0 の `notification*.md` に残る最後の要確認
- 範囲: シミュレーターだけ。実機の回は作らない（4 章）
- v1 からの変更: レビュー v1（[../reviews/2026-11-21-ios-notification-optout-check-design-review-v1.md](../reviews/2026-11-21-ios-notification-optout-check-design-review-v1.md)、A1 は 0）の中心の B の 3 件（R1-4、R1-5、R1-6）を直し、残件を 7 章に書いた

## 1. 確かめたいこと

マニュアルは利用者に次のように書いている。

1. Player Settings（iOS）の Scripting Define Symbols か、ビルドに使うビルドプロファイルの Scripting Defines に `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` を足すと、ビルドは `NtkNotificationInstaller.mm` を書き出した Xcode プロジェクトに足さない
2. そのとき、起動時に delegate は入らない。Manager はイベントの受け手を登録できず、`NotificationActionReceived` などは発生しない
3. `Initialize()` を呼ぶと delegate が入り、受け手が登録される

どれも実測が無い。特に 1 は、define を読むのが Editor のフック（`Editor/iOS/IosXcodeProjectSetup.cs` の `#if`）なので、**ビルドプロファイルの Scripting Defines が Editor のアセンブリの組み立てにも効くか**が要。Player のテストの回が足す define（`IosPlayerBuildSetup` の `extraScriptingDefines`）は Player のスクリプトにだけ効き、Editor のフックには届かないので、この確かめには使えない。

| 番号 | 確かめ | 合格の条件 |
|---|---|---|
| OO-1 | 書き出し | 書き出しのログに `[NativeToolkit][iOS][Setup]` の `installer entries: 0 (skip: True)` がある（フックが走り、define を読んだ）。書き出した Xcode プロジェクトに `Libraries/com.jonghyunkim.nativetoolkit/NtkNotificationInstaller.mm` が無く、`project.pbxproj` もそのファイルを指さない |
| OO-2 | 起動の後、`Initialize()` の前 | 通知センターの delegate がライブラリの proxy（`IosLibrary.NotificationDelegateProxy`）ではない。C ABI の `ntk_notification_add_interaction_listener` が `NOT_INITIALIZED` を返す |
| OO-3 | `Initialize()` の後 | delegate が proxy になる。`Initialize()` の間に、Manager の警告「the interaction listener was not registered」が出ない（Manager 自身の受け手が登録された）。テストからの `ntk_notification_add_interaction_listener` が `NONE` を返す（登録した受け手はテストの中で外す） |
| OO-4 | 2 回目の `Initialize()` | delegate は proxy のまま。エラーのログが出ない |

確かめないこと（理由）:

- 通知のタップが `NotificationActionReceived` に届くこと。Manager のイベントは、受け手の登録（OO-3）が済めば、既定のビルドと同じ経路（proxy → 受け手 → Manager）を通る。既定のビルドでは UI テスト（NU 系）で確かめ済み。届くことを見るには通知の許可と UI のハーネスの define 版が要り、手間に見合わない
- 起動の後に入れたとき、アプリを起動したタップが届くか。ライブラリは「起動の後の install では起動のタップが失われうる」とログに出し、`noteFirstInstall` で起動の判定を閉じる作り（native-toolkit の `NotificationDelegateInstaller.swift`、`NotificationDelegateRouter.swift`）。マニュアルは 2026-11-21 に「届かないことがある」と書いた（`910a043`）。確かめるにはコールドスタートのタップが要り、シミュレーターの UI ハーネスの define 版が要る。今回は外す
- Mobile Notifications との共存。1a の試作での実機の実測（U-4）がある。2.0.0 での再測定は今回の範囲の外

## 2. 仕組み

### 2.1 ビルドプロファイル

`Assets/Settings/Build Profiles/Test iOS SkipInstall Profile.asset` を足す。`Test iOS Profile.asset` の写しで、違いは Scripting Defines（`NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`）と名前だけ。

YAML を手で写さない。Unity 6000.4 の `iOS Profile.asset` には `m_HasScriptingDefines` があり、`Test iOS Profile.asset` には無い。手で `m_ScriptingDefines` だけを埋めると、フラグが偽のままで define が使われないおそれがある（レビュー v1 の R1-5）。Unity をバッチモードで 1 回起こし、`Test iOS Profile` を `AssetDatabase.CopyAsset` で複製し、`BuildProfile.scriptingDefines` を設定して保存する。Unity が書いた YAML をコミットする。そのための Editor の手伝いは一度きりで、リポジトリには残さない（スクラッチの場所に置き、`-executeMethod` で呼ぶために一時的に Assets に置いたら、作った後に消す）

`Test iOS Profile` を元にする理由: `NATIVETOOLKIT_ENABLE_BUILD_STEPS` が無く、dist からの写しが走らない（`--as-package-user` の回と同じ形）。サンプルのシーンも入らないが、この回は使わない。

利用者が「ビルドプロファイルの Scripting Defines に足す」と書いたとおりの形で確かめるので、Player Settings の Scripting Define Symbols は使わない（変えると `ProjectSettings` を元に戻す手間と危険がある）。

### 2.2 `verify_unity_ios.sh --skip-install`

新しい選択肢。シミュレーターだけ（`--device` と一緒には断る）。

- `PROFILE` を 2.1 のプロファイルにする
- Player の回は 1 つだけ。カテゴリ `IosNotificationOptOut` だけを流す。手伝い役（UI ドライバー）は要らない（`--category` と同じく、手伝い役のテストを要求しない）。Notification のサンプルの回は流さない
- EditMode と Editor の PlayMode は、既定どおり流す（`--skip-editmode` で外せる）
- 書き出しの後、Xcode のビルドの前に OO-1 を確かめる。書き出しのログに `installer entries: 0 (skip: True)` が無いか、`Libraries/com.jonghyunkim.nativetoolkit/NtkNotificationInstaller.mm` があるか、`project.pbxproj` に `NtkNotificationInstaller.mm` の文字があれば、回を落とす
- `--skip-install` ではない回（既定、`--as-package-user`、`--category`、実機、Notification のサンプルの回）には逆の確かめを足す: 書き出しのログに `installer entries: 1 (skip: False)` があり、書き出した Xcode プロジェクトに installer があり、`project.pbxproj` がそれを指す。OO-1 の確かめ方そのものが正しいことの照らしになる
- `--skip-install` は `--as-package-user`・`--category`・`--device` と一緒には断る

書き出しはいつもどおり `-activeBuildProfile` でプロファイルを渡す。Unity は起動時にそのプロファイルの Scripting Defines で Editor のアセンブリも組み直すはずで、OO-1 が通ればそれが確かめられる。通らなければ、マニュアルの「ビルドプロファイルの Scripting Defines に足す」は誤りで、書き直しが要る（6 章）。Unity 6000.4 の文書は、プロファイルの symbols が "apply for all Editor and Player code when the given build profile is active" で、`-activeBuildProfile` は "the specified build profile and its custom scripting symbols applicable from startup" と書く（https://docs.unity3d.com/6000.4/Documentation/Manual/custom-scripting-symbols.html 、https://docs.unity3d.com/6000.4/Documentation/Manual/build-profiles-reference.html 、レビュー v1 の R1-10）。OO-1 はそのとおり動くかの実測。

### 2.3 テスト

`Assets/NativeToolkitIosPlayerTests/Probe/` に `IosNotificationOptOutProbeTests.cs` を足す（プローブの `ntkpt_notification_delegate_class` を使うため、既存のプローブのテストと同じアセンブリ）。

- `#if UNITY_IOS && !UNITY_EDITOR && NTK_IOS_PLAYER_PROBE && NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`
- `[Category("IosNotificationOptOut")]`。`IosPlayer` は付けない（既定の回では define が無く、コンパイルで消えるが、念のため既定のゲートのカテゴリと分ける）
- テストは 1 つ（`OO_InitializeInstallsTheDelegate`）。delegate の入れ直しはプロセスに 1 回しか戻せないので、OO-2 → OO-3 → OO-4 を 1 つのテストの中で順に行う。NUnit の順序に頼らない
- C ABI は `[DllImport("__Internal")]` で直接呼ぶ（`ntk_notification_add_interaction_listener`、`ntk_notification_remove_interaction_listener`）。パッケージの internal（`IosNotificationCApi`）には触らない。値は `Notification.h` から写し、写した行をコメントに書く
- OO-2 で `IosNotificationManager.Instance` を先に作る（Awake が受け手の登録を試み、`NOT_INITIALIZED` で警告を出すのが正しい形）。その警告は `LogAssert.Expect` で受ける
- OO-3 で `IosNotificationManager.Instance.Initialize()` を呼ぶ。その間だけ `Application.logMessageReceived` を拾い、Manager の「the interaction listener was not registered」の警告が出ないことを見る。そのあと delegate のクラス名と、テストからの受け手の登録の結果を見る。Manager の internal の受け手のハンドルは、テストのアセンブリ（Assets）から見えないので使わない
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
3. `scripts/verify_unity_ios.sh --as-package-user --skip-editmode`。逆の確かめが、プロファイルの違う回でも通る
4. 実機の回は、この変更のコミットの後に承認を取り直す（守る木が変わるので、前の承認 ID は使えない）。今回は実機の回を流さない

## 6. 結果がマニュアルと食い違ったとき

- OO-1 が落ちる（プロファイルの define が Editor のフックに届かない）: まずプロファイルの直列化（`m_HasScriptingDefines` などのフラグ）が正しいかを確かめる。それでも落ちるなら、マニュアルから「ビルドプロファイルの Scripting Defines」を外し、Player Settings だけにするか、フックの読み方を変える（`#if` ではなく、ビルドのときの define の一覧を読む）。どちらにするかは利用者に聞く
- OO-2 が落ちる（define があっても起動時に入る）: 別の経路で入っている。原因を調べてから決める
- OO-3・OO-4 が落ちる: `Initialize()` の不具合。直す

## 7. 残件（レビュー v1 で直し込まなかったもの）

- 実装のチェックリスト（A2）: Probe の asmdef に `NativeToolkit.Runtime` の参照を足す（R1-1。Probe は全プラットフォームで組まれる）。受け手を外す関数は `ntk_notification_listener_remove`（void、R1-2）。受け手の callback は非 NULL の static メソッド（`MonoPInvokeCallback`、delegate は static の欄で保つ）で、release は NULL（R1-3。NULL の callback は `INVALID_PARAMETER` になり、`NOT_INITIALIZED` を見られない）
- 確かめないこと（R1-9）:
  - Player Settings の Scripting Define Symbols に足す経路。このリポジトリのプロファイルは Player Settings を上書きしていて、上書きのあるプロファイルが有効なときに Player Settings 側の define が効くかは、プロファイルの作りに依る。マニュアルの Note（プロファイルの define は Player Settings に加わる）と合わせて、マニュアルの要確認として残す
  - 前のビルドが足した Xcode プロジェクトから installer を外す経路（Remove）。書き出し先は毎回消されるので、ゲートでは通らない。`IosXcodeProjectPlan` の EditMode のテストが判定を見ている
- 実装で書き分ける（R1-11）: `--skip-install` の回でも手伝い役は起動するが、そのテストの実行は要求しない（`--category` と同じ）
