# I-17 の git の URL の形での確かめの設計 v2 のレビュー v2（差分）

- 記録日: 2026-11-22
- 対象: [../designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v2.md](../designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v2.md)（v1 からの差分と、ラウンド 1 の指摘の判定）
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`。今回は xcodebuild を起こしうる命令を使わないよう頼んだ）
- 1 章と 2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

### 1. ラウンド 1 の指摘の判定

| 指摘 | 判定 | 理由 |
|---|---|---|
| R1-1 | 直った | プロジェクトを基本と共存の 2 つに分けた（v2:16-19）。実機には基本のプロジェクトだけを使う（G-7、v2:30・59）。共存のプロジェクトでは起動時の要求を false にし、Info.plist で確かめる手順も入った（v2:37） |
| R1-2 | 直った | G-1 の条件を「あるもの 3 つ、無いもの 3 つ」に直し、define を足さないことも明記した（v2:14・23）。asmdef の中身とも合う |
| R1-4 | 直った | G-6 を 6a と 6b に分けた。6a は Gradle の段のログの行（AndroidGradleDependencies.cs:72 と一致）、6b は ARMv7 で止まる文（AndroidArchitectureCheck.cs:51 と一致）を見る |
| R1-6 | 直った | `tests` からの除外、`mixed_kinds` を広げること、`GITURL_APPS`、`about`、doctest、5 章の手順 2 の比べ方がすべて入った。`approve` のアプリの一覧の選び方だけは書かれていない（R2-5） |
| R1-7 | 直った | define を設定する起動と書き出す起動を分けた。G-4 の条件は `check_installer` と同じになった（verify_unity_ios.sh:463-483 と一致）。6 章には「まず手順を疑う」と S-4 のことを書いた |
| R1-8 | 直った | M-2 は「クラスが `UnityNotificationManager`、nil ではない」、M-3 は「proxy に戻る」と「ログの行」で決める形になった。`NONE` は外した。ただし M-2 の読み方そのものが動かない（R2-1） |
| R1-9 | 直った | 読み方を `SIMCTL_CHILD_OS_ACTIVITY_DT_MODE=YES` と `simctl launch --console-pty` に決めた。合格の条件にも `UnityNotificationManager` を入れた。ログの行は NotificationDelegateInstaller.swift:155 で、型の名前は `.public` なので読める |
| R1-10 | 直った | 3.3 で、G-5 のログを専用の一時フォルダーに置いて必ず消すこと、エラーは数だけ出すこと、`Authority=` は `:` までで切ること、`TeamIdentifier=` は shasum の頭 8 文字だけ出すことを明記した |
| R1-13 | 部分的 | 4 つの項目の枠は入った（v2:79-84）。ただし準備に device-testing.md:53 の「給電する」が無い。:60 の「流す直前に開発用の接続を確かめる」（`devicectl device info details`）も無く、「ケーブルの接続」だけになっている |
| R1-14 | 直った | 最後の行を `done` にし、スクリプトは上限つきで待つ。実機では写し出す前ごとに `device_guard` を通す（v2:64） |
| Codex 高（G-7 で通知の許可のダイアログが出る） | 直った | R1-1 と同じ |
| Codex 中（G-1） | 直った | R1-2 と同じ |
| Codex 中（G-6 の成功ログ） | 直った | R1-4 と同じ |
| Codex 中（仮のテストが UI のテストの一覧に混ざる） | 直った | R1-6 と同じ |
| Codex 中（M-3 の `NONE` は観測できない） | 直った | 条件から外し、ログの行とクラス名に替えた |

### 2. 新しい指摘

**R2-1**
- 区分: A2
- 場所: v2 の 2 章の 37 行（「ダイアログは、シミュレーターでも出さない」）と M-2（42 行）
- 指摘: 2 つの文が食い違い、M-2 は書いたとおりには終わらない。
  - 起動時の要求を切っても、M-2 で C# の `AuthorizationRequest` を呼べば、許可がまだ決まっていないシミュレーターでは許可のダイアログが出る。`simctl privacy` で前もって許すこともできない（R1-3）。
  - ダイアログが出るとアプリは inactive になり、Unity は `UnityPause(1)` で Player のループを止める。そのため「1 秒後に読む」は来ない。M-3 の `Initialize()` も `done` も書かれず、上限つきの待ちが切れて M は失敗する。
  - 黙って通るのではなく失敗として出るので A2 とした。ただし 37 行の「出さない」は事実として誤り。
- 根拠:
  - `/Applications/Unity/Hub/Editor/6000.4.2f1/PlaybackEngines/iOSSupport/Trampoline/Classes/UnityAppController.mm:606-646`（`applicationWillResignActive` で `UnityPause(1)`）
  - 同 `Classes/UI/UnityScene.mm:20-25`（`sceneWillResignActive` がここに渡る）
  - `Assets/NativeToolkitIosPlayerTests/Editor/IosPlayerBuildSetup.cs:82-84`（「The prompt makes the app inactive, Unity pauses, and the run never finishes」）
  - `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosUiDriver.cs:35`
- 提案: 次のどちらかにし、37 行は「ダイアログは出るが答えない。アプリを消すと消える」と直す。
  - (a) `AuthorizationRequest` を作った同じフレームの中で、M-2 の読み、M-3 の `Initialize()` と読み、`done` の書き込みと flush まで済ませる。delegate の上書きが `_RequestAuthorization` の中で同期に起きることは handoff-2:57 からの見込みなので、記録には「見込み」と書く。
  - (b) R1-3 の (b) のとおり、UI の手伝い役に許可のダイアログを押させる。

**R2-2**
- 区分: B
- 場所: M-1（41 行）
- 指摘: 合格の条件を「記録する」だけに弱めた理由が誤っている。
  - パッケージの `.mm` が delegate を入れるのは `didFinishLaunching` の時期。Mobile Notifications が上書きするのは `kUnityWillFinishLaunchingWithOptions` の時期で、必ずその前に来る。リンクの順で入れ替わるのは、両方が同じ通知に登録している場合（U-4 の時期 1）だけ。
  - U-4 の時期 2（今の `.mm` と同じ形）では、どちらのリンクの順でも、起動の後の delegate は proxy だった。
  - M-1 に条件が無いと、「別の通知パッケージがあっても起動時の取り付けで proxy が前に立つ」というマニュアルの前提が確かめられないまま通る。
- 根拠: `Packages/com.jonghyunkim.nativetoolkit/Editor/iOS/NativeSource~/NtkNotificationInstaller.mm:21-31`、`artifact/topics/ios-c-abi-handoff/results/2026-10-31-ios-c-abi-handoff-2-results.md:56`（`+load` で kUnityWillFinish に登録）と表の「時期 2」の行
- 提案: M-1 の合格の条件を「`IosLibrary.NotificationDelegateProxy`」にし、理由を「U-4 の時期 2」に替える。

**R2-3**
- 区分: B
- 場所: 7 章（121 行）と 3.1（61-64 行）
- 指摘: 7 章は R1-5 の「段の順」を「3.1 に書いた」とするが、3.1 には段の順が無い。
  - G-4 の define と G-6b の ARMv7 は、基本のプロジェクトの ProjectSettings に残る。
  - G-4 が G-3 より前に流れると、G-3 は delegate が nil で落ちる。
  - G-4 が G-5 より前に流れると、インストーラーの無いアーカイブで G-5 が通ってしまう。G-5 の条件はインストーラーを見ないので、ここは気づけない。
  - G-6b が G-6a より前に流れると、G-6a は落ちる。
- 根拠: v2:121 と v2:54-66 の食い違い。`IosXcodeProjectSetup.cs:83-87`（define で `skipInstall` が決まる）
- 提案: 3.1 に段の順を書く（G-1→G-2→G-3→G-5→G-4、G-6a→G-6b）。G-4 と G-6b は「最後に流すか、終わったら設定を戻す」と書く。

**R2-4**
- 区分: C
- 場所: 2 章（45 行）の「U-4 と同じ写し方」
- 指摘: U-4 は実機で `devicectl device process launch --console` を使った。`OS_ACTIVITY_DT_MODE=YES` は同じだが、`simctl launch --console-pty` で写すのは今回が初めて。
- 根拠: handoff-2 results:32
- 提案: 「U-4 と同じ `OS_ACTIVITY_DT_MODE=YES` を、シミュレーターでは `SIMCTL_CHILD_` を付けて渡す」とする。

**R2-5**
- 区分: B
- 場所: 3.2（74-77 行）
- 指摘: `GITURL_APPS` を足すとは書いてあるが、`approve` の「入れて消すアプリ」の選び方を直すとは書いていない。今の式のままだと、giturlapp の承認にも UI のテストの 3 つのアプリが出る。利用者はその誤った一覧で許可することになる。doctest の中身も、5 章の手順 2（`tests` の出力を比べるだけ）も、`approve` の出力を見ていない。
- 根拠: `scripts/ios_ui_runner.py:482`（`UI_APPS if set(tests) - {PLAYER_TESTS}`）
- 提案: 3.2 に「`approve` は `GitUrlApp/IosLaunch` のときは `GITURL_APPS` だけを出す」と書く。それを確かめる doctest か、`approve` の出力を目で確かめる手順を 5 章に足す。

変わっていない部分には、3 つの条件をすべて満たす新しい A1 は無かった。

### 3. 件数

- 判定: 直った 14、部分的 1（R1-13）、直っていない 0
- 新しい指摘: A1 0、A2 1（R2-1）、B 3（R2-2・R2-3・R2-5）、C 1（R2-4）

## 2. Codex

### 1. 前回5点の判定

- G-7：修正済み。Mobile Notifications を含む共存プロジェクトを実機から除外（v2:19）。
- G-1：修正済み。BuildProcessors は「存在しない」が合格条件（v2:23）。
- G-6：修正済み。アーキテクチャ検査は ARMv7 を加えた場合の期待する失敗で確認（v2:29）。
- 仮テスト／mixed_kinds：修正済み。UI 一覧からの除外と承認混在の拒否を明記（v2:75）。
- M-3：修正済み。観測できない `NONE` を外し、delegate と取得方法を明記したログで判定（v2:43、v2:45）。

### 2. 新規の不備

- **中：M-2 の「ダイアログを出さない」条件に要求オプションの指定がない。** v2:37 は起動時要求を止めるが、v2:42 では明示的に要求する。許可未決定の状態で通常の Alert 等を要求すると、完了を待たなくてもダイアログが出る。実際の発生は未指定のオプション次第（Unity 公式: https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.5/manual/Settings.html#request-authorization-on-app-launch 、Apple 公式: https://developer.apple.com/documentation/usernotifications/asking-permission-to-use-notifications?language=swift）。
  **修正：** M-2 は `AuthorizationOption.Provisional` を含め、`registerForRemoteNotifications=false` で要求すると明記する。

## 3. 判断

- A1 は 2 人とも 0。決まりにより、レビューはここで止める
- 止めた後の直し（v3。レビューしていない）: R2-1 と Codex の M-2（`Provisional` で頼み、遠隔通知の登録をしない。仮の許可はダイアログを出さないので、アプリは止まらない）、R2-3（段の順。G-4 と G-6b は最後）、R2-5（`approve` のアプリの一覧）、R2-2（M-1 の条件を proxy に）、R1-13 の残り（給電と接続の確かめ）、R2-4（文）
