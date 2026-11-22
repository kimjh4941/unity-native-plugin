# iOS の C ABI: native-toolkit の申し送り 2 回目の結果（通知: U-3・U-4・U-6・U-7）

- 記録日: 2026-10-31
- 依頼元: native-toolkit の `artifact/topics/ios-c-abi/results/2026-10-31-ios-c-abi-unity-handoff-2.md`（セッション native-toolkit-75 から）
- このリポジトリ: `feature/UNT-14`、`30459b6` の上（この結果はまだコミットしていない）
- 雛形: `ios/Build/stage1a-handoff2/` の `BUILD-INFO.txt` が `built: 2026-10-31T07:15:40Z`、`commit: ac83ad3b (with uncommitted probe or IosLibrary changes)`
- Unity 6000.4.2f1（IL2CPP、Release、Development Build なし）、アプリを組んだ Xcode 26.3（17C529）
- 端末: iPhone XS / iOS 18.7.2（利用者の私物。段階ごとに 4 項目を示して許可を得た。利用者が集中モードで通知をこのアプリだけに絞った）
- U-4 は Unity の通知パッケージ `com.unity.mobile.notifications` 2.5.0（Unity が 6000.4.2f1 に選んだ版）を入れた版で行った
- 状態: **U-3・U-4・U-6・U-7 すべて済み**

## 1. 要約（native-toolkit への返事）

```
handoff-2 results (unity-native-plugin, 30459b6 + uncommitted results on feature/UNT-14)
probe: built 2026-10-31T07:15:40Z, commit ac83ad3b+uncommitted   Unity 6000.4.2f1   Xcode 26.3   iPhone XS / iOS 18.7.2
U-3: at=1 -> install=0 cold-tap event=launch=1 once (after Start registration) getter=u3-cold   at=2 -> install=0 cold-tap event=launch=1 once getter=u3-cold   startup-order=kUnityWillFinishLaunching (at=1 installs here) -> didFinishLaunching (at=2 installs here) -> [C# process start] -> didBecomeActive -> BeforeSceneLoad -> Start
U-4: front-delegate(no reinstall)=IosLibraryCApi.NotificationDelegateProxy with the default link order (package 2.5.0 assigns its delegate first, the library installs after it at both at=1 and at=2)  at=1/2/3: package-notif=to package (via the proxy, library=false)  library-notif=to library  cold-tap(at=2)=ok (launch=1 once, getter=u3-cold)  link-order-changes=yes (at=1 with the installer linked before the package: package in front, library-notif=lost to package, no foreground banner)  unity-sets-delegate=no  package-forwards=no (the package never forwards; it keeps no previous delegate)  extra: a C# iOS AuthorizationRequest of the package assigns the package delegate again -> library-notif lost from then on
U-6: (1) bg action event=once state=bg (focused=False, Unity frames stopped; C# callback ran 1 ms after the proxy didReceive)  (2) from terminated=delivered on next foreground, with launch=1 (see notes)  killed=no (process launched in the background lived >= 50 s until the user opened it)  (3) reply event=once text-length=3 state=bg
U-7: 1s=<willPresent yes, banner yes> (app still active)  2s=<yes, yes> (app still active)  3s=<willPresent yes 0.33 s after resign-active, banner yes>  5s=<willPresent no at ~2.9 s after resign-active, banner yes>
blocked: none. U-4 at=2 with the reversed link order was not built (expected to be the same as at=2, see 3)
```

## 2. 組み込み方（申し送りの 2 章との違い）

1 回目の結果の 2 章と同じ（PBXProject の API。`../probe/ProbeBuild.cs`）。加えて:

- **UnityFramework に `UserNotifications.framework` を足す必要がある**。無いと `NtkHandoff2Installer.mm` の `_OBJC_CLASS_$_UNUserNotificationCenter` が未定義で、UnityFramework のリンクに失敗する
- `.mm` の Unity の名前（`AppDelegateListener.h`、`UnityRegisterAppDelegateListener`、`kUnityWillFinishLaunchingWithOptions`、`didFinishLaunching:`）は、6000.4.2f1 の `Classes/PluginBase/AppDelegateListener.h` に見本のままあった。`#include <IosLibraryCApi/NativeToolkitC/Notification.h>` も、xcframework をリンクするだけで通った
- `.mm` は `Libraries/Plugins/iOS/` に置き、UnityFramework のターゲットに足した。入れる時期は、ファイルごとのフラグ `-DNTK_HANDOFF2_INSTALL_AT=$(NTK_INSTALL_AT)` と、UnityFramework のビルド設定 `NTK_INSTALL_AT` で、`xcodebuild NTK_INSTALL_AT=2` と渡して書き出し直さずに切り替えた
- **書き出したプロジェクトに写した `.mm` の先頭に 2 行足した**（`#import "NtkProbeLog.h"` と `#define NSLog NtkProbeNSLog`。native-toolkit の元のファイルはそのまま）。バナーのタップでコールドスタートするとコンソールが付かないので、`[NTK-HANDOFF2]` の行をアプリの `Documents/probe-log.txt` にも書かせるため（`../probe/NtkProbeLog.mm`）。初めはファイルごとのフラグ `-include` で入れようとしたが、Unity のプリコンパイル済みヘッダーと合わずに組めなかった
- proxy の os_log（`willPresent` / `didReceive`）は、こちらから起動するときに `OS_ACTIVITY_DT_MODE=YES` を渡し、このアプリの stderr に写して読んだ（`devicectl device process launch --console`）。端末全体のログは集めていない。そのため、コンソールの付いていないコールドスタートの間の proxy の行は無い
- C# の宣言（3 章）は書かれた形のままで、IL2CPP で組めて動いた

## 3. 結果（実機）

最初に 1 回 `ntk_probe_notification_request_permission` を呼び、利用者が「許可」を押した: `done error=0 granted=1`、`release` 1 回、main、戻った後。

### U-3: 合格（時期 1・2 とも）

| 時期 | install | コールドスタートのタップ | 起動のきっかけの取得（`OnApplicationFocus(true)` の 1 秒後） |
|---|---|---|---|
| 1（`kUnityWillFinishLaunchingWithOptions`） | `-> 0`。前は `nil`、後は `IosLibraryCApi.NotificationDelegateProxy` | `event kind=0 id=u3-cold action=com.apple.UNNotificationDefaultActionIdentifier launch=1` が 1 回。`Start()` の登録（frame 1）の 446 ms 後、main、戻った後 | `u3-cold` / 既定のアクション |
| 2（`didFinishLaunching`） | `-> 0`。前は `nil`、後は proxy | 同じイベントが 1 回。登録の 434 ms 後 | `u3-cold` / 既定のアクション |

- 起動の順（実機、時期 1 のコールドスタートのファイルの時刻）: `kUnityWillFinishLaunchingWithOptions`（install）→ 4 ms 後に `didFinishLaunching` → 200 ms 後に C# の最初の行（`SubsystemRegistration`）→ 330 ms 後に `didBecomeActive` → 1.8 秒後に `BeforeSceneLoad` → 30 ms 後に `Start`
- Unity 6000.4 の書き出したプロジェクトは UIScene を使う（Info.plist の `UIApplicationSceneManifest`、delegate は `UnityScene`、`UIApplicationSupportsMultipleScenes=false`）。こちらから起動した回のトランポリンの出力では: `applicationDidFinishLaunching` →（時期 2 の install）→ `sceneWillEnterForeground` → C# の最初の行 → `applicationWillEnterForeground` → `sceneDidBecomeActive` → `applicationDidBecomeActive`。`willConnectToSession` は出力を持たないので時刻は無いが、UIKit の順では `didFinishLaunching` と `sceneWillEnterForeground` の間
- どちらの時期も、`didBecomeActive` の時点の delegate は proxy のまま。パッケージ無しでは、Unity の本体は delegate を設定しない（U-4 (e) の答え: どの時点でも入れる前は `nil`）
- 時期 2 でも間に合う。`didReceive` は `didBecomeActive` より前に来たと見られる（`launch=1` になった）。コンソールが付かないので proxy の行の時刻は無い
- イベントは登録の処理の中でなく、登録から約 0.44 秒後に来た。登録の後に main のキューに積まれ、Unity の最初のフレームの処理が終わってから動いたと見られる

### U-4: 合格（ホストが順を固定できる。ただし条件つき）

**パッケージ 2.5.0 の作り**（`Runtime/iOS/Plugins/` のソースで確かめた）:

- `UnityNotificationLifeCycleManager` の `+load` で、`kUnityWillFinishLaunchingWithOptions` の受け手（`queue: mainQueue`）を登録し、その中で `[UNUserNotificationCenter currentNotificationCenter].delegate = manager` と**上書き**する。前の delegate は覚えない
- C# の `AuthorizationRequest`（`_RequestAuthorization`）でも、そのたびに `center.delegate = manager` と**上書き**する
- `willPresent` / `didReceive` は**前の delegate へ転送しない**。どの通知も自分で受ける。`willPresent` は userInfo の `showInForeground` が無い通知（ライブラリの通知）を `None` で返す（表示しない）。`didReceive` はどの通知も `lastReceivedNotification` に入れるだけ
- 既定の設定は `UnityNotificationRequestAuthorizationOnAppLaunch=true`。起動時の許可の要求は delegate を設定し直さない（済んでいればダイアログも出ない）

**手順**: 版ごとに、前面で「パッケージの通知（C# の `iOSNotificationCenter.ScheduleNotification`、`ShowInForeground=true`、8 秒）」と「ライブラリの通知（`schedule_marked`、8 秒）」を 1 つずつ、続けてホームに戻ってそれぞれのバナーをタップした。パッケージ側の受け取りは `iOSNotificationCenter.OnNotificationReceived`（前面）と、`OnApplicationFocus(true)` の 1 秒後の `GetLastRespondedNotification()`（タップ）で見た。各時点の delegate は `[NTK-HANDOFF2]` の行と、C# から呼ぶ `NtkProbeLogDelegate`（`../probe/NtkProbeLog.mm`）で記録した。

| 版 | (a) 起動時の delegate の並び | 前面: パッケージの通知 | 前面: ライブラリの通知 | タップ: パッケージの通知 | タップ: ライブラリの通知 |
|---|---|---|---|---|---|
| 時期 1、既定のリンクの順（パッケージの `.o` が先） | `kUnityWillFinish` の前 `UnityNotificationManager` → install `0` → 後 proxy。以後ずっと proxy | proxy の `willPresent library=false` → 2 ms 後にパッケージの `OnNotificationReceived`。バナー出た | proxy の `willPresent library=true`。バナー出た | proxy の `didReceive library=false` → パッケージの `GetLastRespondedNotification` が `u4-pkg-3` | proxy の `didReceive library=true` → C# に `event` 1 回 |
| 時期 2 | `didFinishLaunching` の前 `UnityNotificationManager` → install `0` → 後 proxy | 同上（2 ms 後）。バナー出た | 同上。バナー出た | 同上 | 同上 |
| 時期 3 | `kUnityWillFinish` で install（前はパッケージ）→ `didFinishLaunching` の install は proxy が前にいるので何もせず `0` | 同上（3 ms 後） | 同上 | 同上 | 同上 |
| **時期 1、リンクの順を逆に**（試作の `.o` が先） | `kUnityWillFinish` の前 `nil` → install `0` → 後 proxy → **`didFinishLaunching` ではもう `UnityNotificationManager`**（パッケージが後から上書き） | パッケージの `OnNotificationReceived`（proxy を通らない） | **パッケージの `willPresent` が受け、バナーは出ない**（利用者の目視）。ライブラリには何も来ない | パッケージ | **パッケージに渡り（`GetLastRespondedNotification` が `u4-lib-4`）、ライブラリのイベントは来ない** |

- (c) 時期 2 で U-3 のコールドスタート: パッケージの delegate を前の delegate にした状態で、`event id=u3-cold launch=1` が 1 回（登録の 474 ms 後）、取得も `u3-cold`。パッケージの `GetLastRespondedNotification` は `null`（ライブラリの通知はパッケージに渡らない）
- (d) リンクの順は結果を変える。時期 1 は、2 つの `+load` が `kUnityWillFinishLaunchingWithOptions` の受け手を登録した順に動くので、`.o` の並び（`UnityFramework.LinkFileList`）で前に立つ方が決まる。既定の書き出しでは、Unity がパッケージの `.mm` を Sources の先に、`PostProcessBuild` で足したファイルを後に並べたので、たまたまライブラリが後になった。時期 2 は `didFinishLaunching`（`kUnityWillFinish` より後）なので、リンクの順に関わらずライブラリが後になる見込み（**逆の順の時期 2 は組んでいない**）
- (e) パッケージ無しでは、Unity の本体は delegate を設定しない（U-3 の版で、どの時点でも入れる前は `nil`）
- 循環・固まり: どの版でも、落ちる・固まることは無かった。パッケージは前の delegate を持たないので、P1-21 の循環（前の delegate がライブラリへ転送し直す）は起きない。合格の条件にある「入れ直し（3）の順なら、パッケージが前の delegate へ同期に転送していること」は、**パッケージが転送しない**ので当てはまらない（時期 3 では 1 回目でライブラリが前に立ち、2 回目は何もしなかった）
- **手順の外で足した確かめ: パッケージの許可の API**。時期 2 の版で、起動の後に C# から `new AuthorizationRequest(Alert | Badge | Sound, false)` を呼ぶと（許可済みなのでダイアログは出ず `Granted=True`）、その直後の delegate は `UnityNotificationManager` に戻った。以後のライブラリの通知のタップはパッケージに渡り、ライブラリのイベントは来なかった（次の起動までずっと）。**時期 2 で入れても、ホストが起動の後にパッケージの許可の API を呼ぶと、ライブラリの通知は失われる**
- バナーの目視（利用者の報告）: 時期 1・2 の前面の 2 つは「出た」。時期 3 と逆の順の、前面のパッケージの通知も「出た」。逆の順のライブラリの通知は「出ない」。時期 3 の前面のライブラリの通知は報告をもらっていない（proxy の `willPresent library=true` はある）

**まとめ（マニュアルと 1a-13 への材料）**: 両方が届く順はある（ライブラリが後に入る順。時期 2 ならホストの側で固定できる）。ただし、パッケージは転送せず、しかも許可の API のたびに上書きするので、(1) ライブラリは必ずパッケージより後に入る、(2) 起動の後にパッケージの許可の API を呼んだら、もう一度入れ直す（入れる関数の 2 回目は、差し替えた delegate を新しい前の delegate にして入れ直す契約なので、これで戻る見込み。**試していない**）、の 2 つをホストに求めることになる。差し替えの見張り（KVO、雛形には無い）があれば、(2) を検知できる

### U-6: 合格

| 手順 | 記録 |
|---|---|
| (1) 受け手を登録したまま、ホームに戻ってバナーを長押し →「BgAct」 | proxy の `didReceive id=u6-a action=ntk.probe.bg library=true main=true`（17:49:59.953）の 1 ms 後に、C# に `event kind=0 id=u6-a action=ntk.probe.bg launch=0` が 1 回。そのとき `Application.isFocused=False`、`Time.frameCount` は背景に移ったときの値のまま（Unity のフレームは止まっているが、C# のコールバックは動いた）。約 50 秒後に利用者が開くまで、止められていない |
| (2) 終了した状態から同じこと | 「BgAct」でアプリが背景で起動した（17:51:57.403 に `didFinishLaunching`、install `-> 0`）。**C# は初期化されず**（`SubsystemRegistration` の行が無い）、約 50 秒後に利用者がアイコンから開いたときに初めて C# が動いた（同じプロセス。`didFinishLaunching` は 2 回目が無い）。`Start()` の登録の 454 ms 後に、保たれていたイベント `action=ntk.probe.bg` が 1 回、**`launch=1`**。取得も `u6-a` / `ntk.probe.bg` を返した。プロセスは背景で 50 秒生きていて、止められていない |
| (3) 「Reply」で文字を送る | 背景のまま、`didReceive ... action=ntk.probe.reply` の 3 ms 後に `event action=ntk.probe.reply text-length=3 launch=0` が 1 回 |

- **判断が要る点**: (2) の背景のアクションは、P1-22 の定義（プロセスで最初の `didReceive` が最初の `didBecomeActive` より前）どおり起動のきっかけ（`launch=1`）になった。アプリを前に出さないアクションで背景で起動し、後で利用者が自分でアプリを開いた場合も「起動のきっかけ」として C# に届くので、C# がそれを「通知のタップで起動した」と読むと誤る。定義をこのままにするか（例: `action` が既定のアクションのときだけにする、`didBecomeActive` までの時間で区切る）は native-toolkit の判断
- completion を呼んだ時期は、proxy の行に無いので記録していない。前の delegate が completion を呼ばない場合は試していない（依頼のとおり、新しい delegate は作っていない）

### U-7: 記録

`focus=False` は Unity の `OnApplicationFocus(false)`（アプリが前面でなくなった時点。ホームに戻る操作の始め）。予約はそれぞれ「押す → すぐホームへ」。

| 秒数 | 通知の時刻 | 前面でなくなった時刻との差 | proxy の `willPresent` | バナー（利用者の目視） |
|---|---|---|---|---|
| 1 | 17:55:58.987 | 0.25 秒**前**（まだ前面） | 呼ばれた（`library=true`） | 出た |
| 2 | 17:56:34.973 | 1.24 秒**前**（まだ前面） | 呼ばれた | 出た |
| 3 | 17:56:54.419 | 0.33 秒**後** | **呼ばれた** | 出た |
| 5 | 約 17:57:10.3（予約の 5 秒後） | 約 2.9 秒**後** | **呼ばれなかった** | 出た |

- 実機では、前面でなくなった直後（0.33 秒後）にはまだ `willPresent` が呼ばれ、約 2.9 秒後には呼ばれなかった。シミュレーターの約 3 秒より短い（境目は 0.33〜2.9 秒の間で、細かくは測っていない）
- 1 と 2 は利用者の操作より先に通知が出たので、「後ろに移った直後」の例にはなっていない

## 4. 私物の扱い

- 端末の識別子、Team ID、証明書の名前は書いていない
- 読んだログは、このアプリの stdout / stderr（`OS_ACTIVITY_DT_MODE` で写した自分の os_log を含む）と、アプリの中の `Documents/probe-log.txt` だけ。端末全体のログは集めていない。ほかのアプリの通知は記録していない
- Reply の本文は記録していない（長さだけ）
- 実機のスクリーンショット・録画・`.xcresult` は作っていない。コンソールとファイルの写しは、書き写した後に消す
