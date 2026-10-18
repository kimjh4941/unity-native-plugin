# レビュー結果

- 日付: 2026-10-12
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v12.md と artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v1.md（合わせて見た）
- 機能名: notification
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 1）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力（手を加えていない）。2 章は Codex の出力を指摘ごとに写したもの（文言はそのまま、行の区切りだけ詰めた）
- A1: サブエージェント 3 件（R1-1〜R1-3）、Codex 7 件（X1-1、X1-5、X1-6、X1-10〜X1-13）。R1-3 と X1-1 は同じ（HELLO の値のシェル注入）

---

## 1. サブエージェント（Claude）

### 範囲

- 補助 v12（`artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v12.md`）は、v11 からの差分を見た。3.1 の 224-226 行、5 章の 331 行、3.5 の 272-289 行、3.6 の 291-298 行、7 章の 351 行、8 章の 366 行、9 章の 392-398 行、11 章の 442-456 行。
- 計画 v1（`artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v1.md`）は全文を見た。
- 照らし合わせたもの
  - Notification 設計 v4: 4.3〜4.5、5.2、5.3、7.3、8.2、8.3、9 章
  - Share の UI テスト計画 v6、`scripts/android_ui_driver.py`、`AndroidUiDriver.cs`、`verify_unity_android.sh`、`AndroidSharePlayerTests.cs`、`AndroidSampleScreenDriver.cs`
  - 1.x の Manager とサンプル
  - native-toolkit の `Notification.h`、`Android.h`、`NotificationBridge.kt`、Kotlin の Progress サービスと設定の画面の Gateway、`UiDriver.kt`、`NotificationEventTest.cpp`、1.12.0 の `UnityAndroidNotificationManager.kt` と `UnityNotificationJsonParser.kt`
  - AAR の manifest
  - 前回の実機の結果 XML（`$TMPDIR/unity-android-verify/Android.xml`）
- ファイルは何も変えていない。

### 強み

- **K-3 の答え（11 章）は筋が通っている。**
  - 権限の取り消しはプロセスを止める。そこで取り消しを使わず、新しいインストールの最初の状態を順に使う。
  - 保持のテストと権限のテストが 1 つのプロセスで両立しない理由も正しい。Manager はリスナーを外さない（設計 J-N12）。
- **fixture を名前の順に流す前提には実測の裏付けがある。**
  - 前回の `Android.xml` で、fixture の開始時刻は Clipboard → Dialog → NativeToolkitCApi → ShareManager → Share の順で、名前の順だった。
  - 実機で流れるアセンブリは `NativeToolkit.Runtime.PlayModeTests.dll` 1 つで、名前空間も 1 つ。
  - 前提が崩れたときは、計画が `Assert.Fail` で表に出す（1 章の前提、5 章）。
- **`grant` / `appop-allow` の引数を厳しく検査している**（`[A-Z_]{1,64}`）。取り消しの命令を持たない判断も正しい。
- **シェードの題名を固有にしている**（`NTK-AN-<番号>-<GUID>`）。Sharesheet と同じプライバシーの決まりをシェードにも広げた方向も正しい。
- **計画の期待の文言は設計 4.5 と合う。**
  - 合うことを確かめた行: AN-02、AN-17〜21、AN-43、AN-44。
  - AN-21 の 1.x の文言は 1.12.0 `UnityAndroidNotificationManager.kt:381,405` と一致した。
  - AN-18 の 1.x の文言は同じファイルの `:423` の形と一致した。
- **AAR の manifest は前提どおり。** `SCHEDULE_EXACT_ALARM` があり、`USE_EXACT_ALARM` は無い。V-24 の前提に合う。

### 改善点

#### A1（実装フェーズで気づけない）

**R1-1｜A1｜severity 中〜高｜補助 3.5 の `notification-absent`（279 行）、計画 AN-38（82 行）**

- 問題: 「無い」の確かめが、読み取りの欠けで偽の `ok` になる。
- 根拠:
  - `uiautomator dump` は、画面に見えていない部品を木に出さない（要検証）。シェードを下に送る手順も無い。
  - そのため、ほかのアプリの通知が多い端末で行が画面の外にあると、「無い」と判定される。
  - Sharesheet の `absent-in-sheet` は、同じ穴をアンカーで塞いだ（v7、U1-1）。3.5 にはその手立てが無い。
  - 0 件のシェードで `notification_stack_scroller` が木に出るかも確かめていない。
  - AN-38 は、取り消しが効かなくても通りうる。`doctest` も同じ前提の模擬の端末なので通る。
- 提案:
  - 「無い」の判定はシェードを使わず、テストの側で自分のアプリの `NotificationManager.getActiveNotifications()` を読む形にする。この形ならプライバシーにも触れない。
  - シェードで判定を続けるなら、同時に出したアンカー通知が同じ木に在ることを条件にし、一覧を送って探す手順を足す。
  - V-19 に次の 2 つを足す: 画面の外の行が木に出ないこと、0 件のときの一覧の印の出方。

**R1-2｜A1｜severity 中｜補助 3.6（293-298 行）と 3.5 の失敗の経路（286 行）**

- 問題: シェードの木を見分ける条件が「`com.android.systemui` の通知の一覧が出ている木」で、未確認の resource-id に頼る読み方ができる。
- 根拠:
  - `notification_stack_scroller` は V-19 に挙がっていない。
  - ID が Android 16 で違うと、シェードの木が「シェードでない」と判定され、`--dump-dir` に残る（ほかのアプリの本文と差出人）。
  - クイック設定（Wi-Fi の名前、Bluetooth の機器名）を含む木も漏れる。
  - 失敗の経路は「シェードが開いていれば閉じ」なので、判定が外れると閉じずに終わる。
  - 同じ前提の `ScriptedDevice` の上では `doctest` が通るので、実装の段では気づけない。
- 提案:
  - 3.4 と同じく、**パッケージで判定する**。`com.android.systemui` の部品が 1 つでもあれば残さない。
  - 画面の命令の失敗の経路では、`cmd statusbar collapse` を**条件なしで**呼ぶ（何度呼んでも害が無い）。
  - V-19 に `notification_stack_scroller` を足す。

**R1-3｜A1｜severity 中｜補助 3.1 の `grant` / `appop-allow`（225-226 行）、3.5 の `close-screen`（284 行）、3 章の HELLO の形（192-197 行）**

- 問題: 引数を検査しているのは権限と操作の名前だけで、同じ行に入る `<パッケージ名>` を誰も検査していない。
- 根拠:
  - `<パッケージ名>` は logcat の HELLO から来る。タグ `Unity` には端末上のどのアプリも書ける。
  - 今の実装も、HELLO の 3 つの値を検査していない（`scripts/android_ui_driver.py:147-166` の `parse_request`、`:433-435`）。
  - パッケージ名は引用なしでシェルの行に入る（`:422-424` の `run-as {self.package} --user {self.user} sh -c ...`）。
  - v12 では、shell の権限で状態を変える `pm grant` / `appops set` にも同じ値が入る。
  - `close-screen` の Base64 のパッケージ名にも、検査の決まりが無い。
  - `doctest` は 3.5 が求める「権限・操作の名前の拒否」だけを見るので、穴が残ったまま通る。
- 提案:
  - HELLO の値を検査する。`<run>` は `^[0-9a-f]{32}$`、パッケージは Java のパッケージ名の形、ユーザーは `^\d{1,5}$`。
  - パッケージはスクリプトが `--package com.UnityTestRunner.UnityTestRunner` で渡す値と一致することを条件にする。合わなければ捨てる。
  - `close-screen` の引数は、許す一覧（`com.android.settings` と permissioncontroller の 2 つ）に限る。
  - 3.3 と同じ流れで、`pm grant --user <user>`、`appops set --user <user>` にする。`run-as` では v3 から `--user` を付けている。

#### A2（実装フェーズで気づける。実装結果ファイルのチェックリストへ）

**R1-4｜A2｜severity 中｜計画 0 章（18 行）、1 章（29 行）、5 章（107 行）**

- 問題: 前例どおりに書くと、保持のテストの前に Manager ができてしまう。
- 根拠: Share の fixture は `[SetUp]` で Manager のイベントを購読する（`AndroidSharePlayerTests.cs:58-70`）。`AndroidNotificationFreshInstallTests` が同じ形をとると、AN-01 の前に Manager ができる。
- 気づける理由: AN-01 の前提の確かめが `Assert.Fail` で表に出す。
- 提案: 「この fixture の `SetUp` / `OneTimeSetUp` と AN-01 の後片付けまでは、Manager に触れない」と計画に書く。

**R1-5｜A2｜severity 低｜計画 1 章 AN-01 と 1.x の列（12 行）**

- 問題: 1.x の段で、このテストのコードがコンパイルできない。
- 根拠: 手順 3（1.x の記録）の時点では `AndroidNotificationCApi` が無い（手順 5 で作る）。C ABI を直接呼ぶ AN-01 と、前提の確かめのコードがコンパイルエラーになる。
- 提案: 1.x の段での置き方を書く。たとえば、手順 5 まで AN-01 の本体を置かない。

#### B（検証手段を変える）

**R1-6｜B（中心契約）｜severity 高｜計画 1 章（31-34 行）、補助 7 章（351、359 行）、11 章（451 行）**

- 問題: 保持のテスト（AN-01）は、既定の実行では一度も流れない。
- 根拠:
  - AN-01 は Test Android™ Profile の実行でだけ流れるが、その実行は任意の `--as-package-user` のときだけ走る（`verify_unity_android.sh:272-274`。`testing.md:379` も「流せる」）。
  - Android™ Profile の実行では、AN-01 は短絡して Pass になる。
  - Test Profile の実行では、カテゴリの判定を当てていない（`run_device_tests ... 0`、補助 7 章 359 行）。補助がいないときに AN-01 が Ignore になっても、ゲートで落ちない。
- 提案: 次のどちらかにする。
  - 保持のテストだけを流す実機の実行を、スクリプトの既定に足す（`-testFilter` かカテゴリで AN-01 だけ）。
  - Notification の作業中は `--as-package-user` を必須にし、Test Profile の実行でも `AndroidNotificationFreshInstallTests` の skip が 0 であることを判定する。
  - 補助 7 章 359 行の「Android™ Profile の実行だけ」も直す。

**R1-7｜B（中心契約）｜severity 高｜計画 AN-01（38 行）**

- 問題: AN-01 は、保持が壊れていても通りうる。
- 根拠:
  - `notification-tap ... stay` が答えた時点で、タップのブロードキャストがアプリに届いているとは限らない。
  - 届く前に Manager が登録すると、イベントは保持ではなく直接届く。それでも「1 回」で通る。
- 提案:
  - タップの後、一定の時間（例: 3 秒）待ってから `Instance` を取る、と期待に書く。
  - または、届いたことを補助側で確かめる。例: 自分のアプリの logcat のタグ（Kotlin の受信機のログ）を補助が見て答える。

**R1-8｜B（中心契約）｜severity 高｜補助 3.5 の `notification-tap` / `notification-action` の `front`（280-281 行）、計画 AN-31・AN-34（75、78 行）、1.x の列（K-1）**

- 問題: 「このアプリが前面に来るのを待つ」は、押す前から満たされている。
- 根拠:
  - シェードを開くのは窓の重なりで、アプリは前面のまま（V-18 の見込み）。そのため、`launchApp` でアプリが開くこと（設計 5.3 の 2 行目）が壊れていても通る。
  - 1.x の trampoline の制限（K-1）も測れない。
  - native-toolkit の前例は、押す前に `UiHome()` でアプリを後ろに回している（`NotificationEventTest.cpp:302`）。
  - さらに 1.x で開かない場合、`fail not-in-front` で `Wait` が `Assert.Fail` する。計画の「記録する」とも両立しない。
- 提案:
  - `front` のときは、補助が先に HOME を押し、`STOPPED` を確かめてからシェードを開く。
  - 1.x を記録できるよう、`fail` ではなく `ok front` / `ok not-front` の値で答える形にする。

**R1-9｜B｜severity 中｜補助 3.5 の失敗の経路（286 行）、`close-screen`（284 行）**

- 問題: Back が効かなかったときに、アプリのタスクの中の画面を取り除く手段が無い。
- 根拠:
  - 権限のダイアログは、`requestPermissions` の `startActivityForResult` でアプリのタスクの中に積まれる。
  - 設定の画面も、Activity から `NEW_TASK` なしで開く（`AndroidNotificationSettingsGateway.kt` の `start`）。
  - 次の 2 つで消えるかは、Unity の Activity の launchMode に依る（要検証）:
    - `front`（`am start -n`）を呼ぶ
    - Sharesheet の「プロセスを止める」手順（3.5 は設定の画面では止めないと決めた）
  - 消えなければ、Unity は止まったまま実行の時間切れまで進まない。
- 提案:
  - 9 章に「`front` で、タスクの中の権限のダイアログ・設定の画面が消えるか」を足す。
  - 消えないなら、権限のダイアログの段 2 に `am force-stop <permissioncontroller>` を、設定の画面の段 2 に CLEAR_TOP 付きの起動を置く。
  - Back を押す前に毎回読み、対象が無ければ押さないことも明記する（Back が Player に届くのを防ぐ）。

**R1-10｜B｜severity 中｜計画 5 章（105-106 行）**

- 問題: 後片付けの結果が、次のテストの数に混ざる。
- 根拠:
  - 後片付けの `CancelAll*`・`stop` の結果は、取り出し口を通って後のフレームに出る。
  - Share の計画 v6 3 章（173-180 行）は、7 段（結果を待つ、`PendingCount`、数がそろうのを 5 秒待つ、0.5 秒の静置、列を空にする）でこれを防いでいる。計画 v1 には、待つことも空にすることも無い。
  - 「来ない」の確かめ（例: `stay` の後に余分なイベントが無い）の起点も無い。シェードが Unity を止めない場合（V-18）、`OnApplicationPause(false)` も起点にならない。
- 提案: Share の 7 段を写し、起点（最後の答えを受けた時刻）を決める。

**R1-11｜B｜severity 中｜補助 3.5（274 行の「同じ値を使う」）、8 章（366 行）**

- 問題: 画面の命令の時間の上限が、数として示されていない。
- 根拠:
  - 閉じ方は Sharesheet と違う（collapse → Back、ダイアログ・設定の画面は Back だけ）。
  - それでも上限は「Sharesheet と同じ値」とだけ書いてあり、151 秒に収まる内訳を示していない。
  - 8 章の `doctest` の一覧にも、「どの adb の呼び出しも時間切れになる端末で、画面の命令が上限の中で諦める」が無い。
- 提案: 3.3 の内訳を、画面の命令の段に写して書く。上の `doctest` を足す。

**R1-12｜B｜severity 低｜補助 3.5 の `notification-dismiss`（282 行）**

- 問題: スワイプの形が、前例で分かったことから外れている。
- 根拠:
  - 前例は、画面の 1/10 から 9/10 へ速く払い（5 step）、2 回まで試す。注記は「遅いスワイプは消えないことがある」（`UiDriver.kt:136-150`）。
  - 3.5 は、行の中央から右端まで 300 ミリ秒で 1 回だけ。
- 提案: 前例に合わせる。

**R1-13｜B｜severity 低｜計画 0 章（19 行）、2 章（47 行）**

- 問題: 「補助を使わない」はずの fixture が、補助に頼っている。
- 根拠: カテゴリなしの `AndroidNotificationManagerPlayerTests` の前提（権限と正確なアラームの許可）は、1 章の `grant` / `appop-allow`、つまり補助が作る。補助のいない実行では、2 章が全部 `Assert.Fail` になる。
- 提案: カテゴリを付ける。または、許可が無ければ Ignore にして理由を書く。

**R1-14｜B｜severity 低｜計画 AN-25（66 行）、AN-51（97 行）**

- 問題: サンプルの予約が、テストの中で発火しうる。
- 根拠: サンプルの予約は 15 秒後に発火する（`AndroidNotificationManagerExampleController.cs:884`）。AN-25 で Payload を順に出すと、15 秒を超えたときに `Recv` が 1 増え、「件数 / 0 / 0」が崩れる。
- 提案: AN-25 では予約の時刻を 10 分後に置き換える。

#### C（記述の整合だけ）

**R1-15｜C**

- 対象: 計画 AN-43・AN-44 の 1.x の列（87-88 行）。
- 問題: 「成功の見込み」とあるが、これらは別のスレッドから呼ぶ（AD-27 と同じ形）。
- 根拠:
  - 1.x の別のスレッドからの呼び出しは、Share で `could not be started.` と実測済み（Share の計画 v6 の 132 行、AS-35）。
  - 計画の AN-22 の見込みとも食い違う。
- 提案: 見込みを `openNotificationSettings could not be started.` に直す。

**R1-16｜C**

- 対象: 計画 AN-40 の「どの画面が開いたかを記録」（84 行）。
- 問題: `close-screen` は `ok` しか返さず、記録する手段が無い。
- 提案: 前面の Activity のクラス名を `ok <値>` で返す。

**R1-17｜C**

- 対象: 補助 3.5（287 行）。
- 問題: 決まった語が足りない。
  - シェードが開かないとき、権限のダイアログが出ないときの語が無い。
  - `notification-absent` のミリ秒が範囲の外のとき、`bad-span` か `bad-arg` かが決まっていない。
  - `<前面>` が `front` / `stay` 以外のときの語も無い。
- 提案: 語を足す。

**R1-18｜C**

- 対象: 補助 3.2 の表（241 行）、3.3（259 行）。
- 問題: 180 秒の対象を「Sharesheet の命令」と書いたまま。
- 提案: 画面の命令を加える。

**R1-19｜C**

- 対象: 補助 3.5（274 行と 286 行の関係）。
- 問題: 見ていないシートの見張り（10 秒）が `permission-answer` / `close-screen` に当たるかが書かれていない。
- 提案: 当たるかどうかを明記する。

**R1-20｜C**

- 対象: 補助 7 章（351 行）。
- 問題: `trap` での順（補助を止めてからアンインストール）が書かれていない。
- 補足: 冒頭の「7 章の最初の行」は、実際には 2 つ目の箇条。

**R1-21｜C**

- 対象: 計画 1 章の表（40-41 行）。
- 問題: AN-03・AN-04 に「Android™ Profile の実行だけ」の印が無い（本文の 33 行と食い違う）。

### 不足項目（要検証に挙げるべきもの）

1. **Unity の Build and Run が `adb install -g` で実行時の権限を与えないか**
   - 与えると、新しいインストールでも POST_NOTIFICATIONS が許可済みになる。最初の状態を使う設計が成り立たない。
   - Unity の DLL の文字列からは、確かめられなかった。
2. **`notification_stack_scroller`、`android:id/expand_button` の ID**（V-19・V-20 に名前を足す）
   - あわせて、通知 0 件のときの一覧の印と、画面の外の行の扱い（R1-1）。
3. **Android 16 で同じアプリの通知が複数あるときに、自動でまとめられて題名が見えなくならないか**
   - AN-25、AN-54、サンプルで複数を出す場面。
4. **`front` と launchMode**
   - `front` でタスクの中の画面が消えるか（R1-9）。
   - 本文のタップのランチャーの Intent で、Unity の Activity が 2 つ目に作られないか。
5. **Progress の失敗の経路でプロセスが落ちないか**
   - `startForegroundService` の後、`startForeground` の前に `stopSelf` すると、プロセスが落ちる（`ProgressForegroundService.kt` の `showProgress` → `stopProgress`）。
   - AN-44 でここを通ると、テストの実行ごと終わる。
6. **J-N7 の残りの行が無い**
   - 権限が無いときの `UpdateNotification` と、過ぎた時刻の予約の `PERMISSION_DENIED` を AN-02 の並びに足す。
   - 「アプリの通知がオフ」の経路は、6 章「確かめないもの」に理由を付けて書く。

### 総合評価

- **A1 は 3 件**（R1-1、R1-2、R1-3）。
  - どれも補助の画面の命令の約束が静かに破れるもの: 偽の「無い」、シェードの木の保存、HELLO 経由のシェル注入。
  - `ScriptedDevice` の `doctest` は同じ前提で組むので、実装の段では気づけない。
- そのほかの件数: A2 は 2 件、B は 9 件（うち中心契約は R1-6〜R1-8 の 3 件。どれも直すべき）、C は 7 件。
- 計画の期待は、設計 v4 の 4.4・4.5・5.3 とおおむね一致している。1.x の列の食い違いは R1-15 の 1 件。
- 新しいインストールの最初の状態を使う順の設計は、成り立つ。根拠は、fixture が名前の順に流れる実測と、前提の確かめ。
- ただし、保持のテストの扱いが弱い:
  - 既定の実行では一度も流れない（R1-6）。
  - 流れても保持を証明しない（R1-7）。
- **止める基準には達していない。** A1 と中心契約の B を直して、もう一度レビューすることを勧める。

---

## 2. Codex

- **X1-1 — A1** 補助 v12 §3.1・§6: `grant` / `appop-allow` の操作引数は制限されるが、同じ shell 行へ入る HELLO の package/user は無検証・無引用。偽の `Unity` logcat 行から shell injection が可能（`scripts/android_ui_driver.py:147,422,433,1432`）。修正: run を 32 桁 hex、package を Android package-name 文法、user を非負整数として検証し、shell へ渡す全値を `shlex.quote`。偽 HELLO の拒否テスト
- **X1-2 — A2** 補助 §3.5: dismiss が「行中央から右端へ 300ms、1 回」。前例では遅い座標 swipe が dismiss にならない実測があり、高速・全幅・再試行（`native-toolkit/.../testing/NotificationShade.kt:98`、`capitest/UiDriver.kt:135`、`android-c-abi-stage0c-result.md:81`）。修正: 画面幅 10%→90% の高速 fling、毎回 row を再取得して最大 3 回
- **X1-3 — A2** 補助 §3.5: action の展開後に UI の安定を待たず 1 回だけ探し直す。展開で題名が置換される場合も扱えない（`NotificationShade.kt:64` は展開後 `waitForIdle()` し SystemUI 全体から再検索）。修正: 期限内で読み直し、`text` と `content-desc` の両方から待つ
- **X1-4 — B** 補助 §3.6・V-17: host 側の timeout 後も端末の `uiautomator dump` が走り、削除後に固定パスへ再生成する経路が残る。終了時の再削除も `dump_left` が真の場合だけ（`android_ui_driver.py:330,491,1455`）。修正: dump ごとに固有パス、timeout 後に remote process の終了を待つ、終了時は全 dump を削除して成功をゲート
- **X1-5 — A1** 計画 §1: AN-01 を Test Android Profile だけで実処理するが、その run は既定では実行されない（`verify_unity_android.sh:71,272`）。Android Profile では `Pass` するので保持を一度も検証せず全体が成功する。修正: 両 profile を必須にし、対象外 profile で `Pass` させず filter/category で分ける
- **X1-6 — A1** 補助 §7・§11、計画 §5: Test Android Profile では `AndroidUiDriver` の skip/0 件ゲートを無効にしている（`verify_unity_android.sh:207,272`）。AN-01 が `Ignore` のまま成功しうる。修正: AN-01 専用 category か test filter を設け、Test Profile でも「1 件以上・skip 0」を必須に
- **X1-7 — A2** 補助 §11、計画 §0: fixture 名の辞書順を実行順の保証としているが、Unity Test Framework は fixture 名をソートしない。method の `[Order]` も別 fixture より先の保証にならない（runner は suite の既存順を列挙し、`Order` を持つ直接 child だけを並べ替える。`Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEngine.TestRunner/NUnitExtensions/Runner/CompositeWorkItem.cs:226`）。修正: fresh-install fixture を filter で独立 run にするか、依存する全テストを同一 fixture 内で `[Order]` 管理
- **X1-8 — A2** 計画 §5: teardown の `CancelAll*` と Progress stop の非同期結果を待って drain する順序がない（Share 計画 v6:173 は明記）。修正: cleanup の完了を待ち、queue/pending が 0 になってから購読解除・カウンタ初期化
- **X1-9 — B** 計画 §0・§2〜§5: `Op / Tap / Recv` を照合すると宣言しているのに、AN-18〜25、AN-39、AN-44、AN-50〜55 の件数が未定義か「件数」。修正: 各行に 3 値。同期の問い合わせは `0 / 0 / 0`
- **X1-10 — A1** 計画 AN-31・AN-34: テストの時点で Player が既に前面。シェードは Activity ではない見込みなので、起動処理が壊れていても `front` が通る（前例は押す前に Home。`NotificationEventTest.cpp:294`）。修正: 通知を出した後に Player を後ろに送り、非前面を確かめてからシェードを開く。押下後はシェードの消失と Player の前面復帰の両方を待つ
- **X1-11 — A1** 計画 AN-40〜42: `close-screen com.android.settings` は Settings の package しか見ないので、3 つの API が同じ誤った画面を開いても成功する（`AndroidNotificationSettingsGateway.kt:31` は別の Intent を使う）。修正: 期待の component か画面固有の selector を渡し、primary か許された fallback かを区別
- **X1-12 — A1** 計画 AN-44: `SERVICE_START_NOT_ALLOWED` が起きなくても「記録して Pass」。修正: C ABI の列は指定の文言以外を失敗に。安定して起こせないなら層 1 か手動へ
- **X1-13 — A1** 計画 AN-20: 無い resource のエラー文言だけを見て、「何も表示しない」を確かめない（設計 v4 §5.3）。修正: 固有題名を与え、エラーの後に表示されていないことを確かめる
- **X1-14 — B** 計画 AN-25: 設計 v4 の「全 Payload が表示できること」を操作の成功だけで代用（`notification-design-v4.md:429`）。修正: immediate の Payload は表示を確かめ、schedule は予約状態と代表の発火まで

A1 件数: 7（X1-1、X1-5、X1-6、X1-10、X1-11、X1-12、X1-13）

補足（取りまとめ役の転記）: Codex の出力は、Codex を呼んだ転送役のエージェントがそのまま返したもの。転送役は一次資料を確かめ直していない。
