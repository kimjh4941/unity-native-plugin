# Windows Notification UI 自動テスト計画 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Windows Notification のサンプル（`Runtime/UI/Windows/Notification/WindowsNotificationManagerExampleController.cs`）と `WindowsNotificationManager` |
| 同梱 DLL | 1.x（`Plugins/Windows/VERSION.txt`: dist 1.11.0 / `windows-native-toolkit-1.2.0.dll`） |
| 目的 | Windows C ABI 2.0.0 への移行（`artifact/topics/windows-c-abi-2/`）の前に、今の動きを自動テストで固定する |
| 実行 | `scripts/verify_unity_windows.sh --include-destructive` の Player テスト（層 2b）。**破壊的な実行のときだけ走らせる**（3 章） |

## 1. 何を確かめるか

実機確認は行われたが、記録（日付・端末・消化項目・失敗）は残っていない（`artifact/topics/device-verification-records/README.md`）。
**正本は、このリポジトリのサンプル計画書の観点表**（`2026-06-06-windows-notification-sample-scene-design-v2.md` 6 章、16 行）とし、
native-toolkit の Notification UI テスト（`windows/WindowsLibraryExampleUITest/Tests/Notification/`、N-01〜N-23）のうち、このサンプルで確かめられるもので補う。

サンプルの通知はどれも同じ tag / group（`win-sample-notification` / `win-sample-group`）で出る。後から出したものが前のものを置き換える。
通知の送り元は `Application.productName` で、テスト用 Player ではビルドプロファイル（`Assets/Settings/Build Profiles/`）の「Unity NativeToolkit」。
通知センターでは、この名前のグループに並ぶ。

| # | 操作 | 期待（1.x） | 観点表 / native |
|---|---|---|---|
| W-01 | Initialize | `✓ Initialize` | 観点 1 / N-01 |
| W-02 | GetSetting | `✓ NotificationSetting: Enabled` | 観点 11（設定を変える側は対象外）/ N-02 |
| W-03 | ShowNotification | `✓`。通知センターの「Unity NativeToolkit」に、タイトル `Energy Refilled` と本文の通知がある | 観点 2 / N-03 |
| W-04 | W-03 の通知を展開して Open を押す（**保留**。2 章） | `ResultTextBlock` が `NotificationInvoked: ` で始まり、`"action":"open"` を含む | 観点 3（バナーでなく通知センターから）/ N-04 |
| W-05 | ScheduleNotification | `✓`。**約 1 分後**に通知センターに `Guild Battle Starts Soon` が出る（観点表は 30 秒だが、実装は 1 分） | 観点 4 / N-11 |
| W-06 | ScheduleNotification → すぐ CancelScheduled | `✓`。1 分を過ぎても通知センターに出ない | 観点 5 / N-12 |
| W-07 | ShowProgressNotification → UpdateProgress | 通知センターの進捗バーが 0.3 → 0.5（30% → 50%） | 観点 6 / N-07 |
| W-08 | 進捗の通知が無いまま UpdateProgress | `✗ UpdateProgress` と「Progress notification not found」（エラーコード 4） | — / N-08 |
| W-09 | ShowNotification → RemoveByTag | `✓`。通知センターから消える | 観点 7 / N-15 |
| W-10 | ShowNotification → RemoveAll | `✓`。自アプリのグループが消える | 観点 9 / N-17 |
| W-11 | Home → もう一度 Notification 画面 → W-03 / W-04（**保留**。W-04 と同じ理由） | 入り直した画面でも `NotificationInvoked` が届く | 観点 16 / N-23 |

確かめないもの:

| 観点 | 理由 |
|---|---|
| RemoveNotificationById、GetAllNotifications、SetBadge（3 行） | ボタンが 2026-06-13 に外された（`e29968e`）。さらに unpackaged では 1.x が `NOT_SUPPORTED`（8）を返す（native-toolkit `1.11.0` の `WindowsClassicActivator.cpp`） |
| バナーの表示とバナーからの操作 | 集中モード（応答不可）がオンだと出ない。この PC はオン。切り替えるには OS の設定を触る必要がある。通知センターからの操作（W-04）で代える |
| GetSetting の Enabled 以外、アプリの通知 OFF | 設定アプリの画面操作でしか変えられず（レジストリは効かない。native-toolkit の設計書 付録 C）、途中で失敗すると自動で戻せない |
| Editor 上の「Windows Standalone only.」 | Editor では画面の `Awake` が `EditorUtility.DisplayDialog` を出して止まる。層 2a の別課題 |
| 見た目（アイコン、画像）、音 | native-toolkit も computer use に回すか対象外 |

## 2. 自動化の前提

**通知の操作は Dialog と違い、呼び出しを止めない。** 通知を出す呼び出しは同期で結果を返し、Open のクリックは COM の activator から
`UnityMainThreadDispatcher` を通って次のフレーム以降に届く（`WindowsNotificationManager.cs` の `FireResult` と `NotificationInvoked`）。
テストは、外の PowerShell で通知センターを操作している間、フレームを回して待てばよい。

通知センターの扱い（2026-09-27 に PowerShell から確かめた）:

- **開き方**: `Start-Process 'ms-actioncenter:'`
- **見つけ方**: 通知センターは ShellExperienceHost の `Windows.UI.Core.CoreWindow`（名前「通知センター」）。
  **`EnumWindows` にも、UI Automation のルートの子にも出てこない。** 開いた直後の `GetForegroundWindow()` で取り、
  プロセスとクラスに加えて、中に `DoNotDisturbButton` があることで確かめる（開き切る前は、同じプロセスの別の窓が前面にあった）
- **読み方**: その HWND から `AutomationElement.FromHandle`。Windows PowerShell の UI Automation（managed クライアント）で中身を全部読める。
  AutomationId は native-toolkit と同じ（アプリのグループの `Title`、通知の ListItem の `Title` / `Content` / `ExpandButton`、展開後の `VerbButton`）
- **Open**: 展開前は `VerbButton` が無い。`ExpandButton` を Invoke してから、名前が `Open` の `VerbButton` を Invoke する
- **閉じ方**: 通知センターが前面にある間に Esc。前面でないと効かず、開いたまま残る（1 回、開発者の画面に残した）
- **片付け**: 各テストの前後で RemoveAll を押し、通知センターが開いていれば閉じる

PowerShell の側は Dialog の閉じる役と同じ形にする（テストに埋め込み、手順を Base64 で渡し、行ごとに出力する）。

確かめたこと:

- 通知センターに出るまでの時間: W-03 は Show から 10 秒以内に見つかった
- 進捗バーは `RangeValuePattern` で読める。値は 0〜100（0.3 は 30 と読める）
- **Open を押しても、動いている Player に届かない。W-04 と W-11 は保留にする**（次の節）

### W-04 / W-11 を保留にした理由（2026-09-27）

通知センターで Open を押しても、`NotificationInvoked` がアプリに届かない。**自動テストの押し方の問題ではない。**

**原因（native-toolkit が確かめた、2026-09-27）: 1.x のライブラリが `HKCU\Software\Classes\AppUserModelId\<AUMID>\CustomActivator` を書かない。**
Windows 11 は、パッケージ化しないアプリのトーストのボタンを、この値が指す COM クラスへ届ける。スタートメニューのショートカットの
ToastActivatorCLSID だけでは届かない。native-toolkit は Unity を使わない小さなアプリ（2.0.0 の C++ API、メインスレッドは STA、
この PC に履歴の無い新しい AUMID）で、この値だけを変えて 3 回試した: 無し → 届かない、受け取り口の CLSID を書く → `{"action":"open"}` が届く、消す → また届かない。
2.0.0 も同じ経路で、同じく壊れていた。**native-toolkit が `c9f4071b`（`feature/NTKIT-16`）で直し、dist 1.12.0 も作り直した。**
初期化（`RegisterActivation`）が DisplayName / IconUri と同じキーに `CustomActivator` も書き、書けなければ初期化を失敗にする。
上書きなので、古い値が残った PC も最初の初期化で直る。値を事前に書かない状態で試し、クリックが届くことを確かめてある（向こうの 4 回目の試し）。
記録は native-toolkit の `artifact/topics/windows-architecture/results/2026-09-27-windows-unpackaged-activation-finding.md`。

- 上書きなので、**同じ表示名（= AUMID）を使う 2 つのアプリはクリックを奪い合う。** パッケージ化しないアプリでは表示名がそのまま AUMID になり、
  ショートカットの名前、CLSID の元、レジストリのキーになる。native-toolkit はヘッダーに注意書きを入れた。Unity では `Application.productName` がそれにあたる
- STA は原因ではなかった。`SetCurrentProcessExplicitAppUserModelID` も要らなかった。MTA とコールドスタートは試していない
- **1.x には修正版を出さない**（2026-09-27、開発者の判断）。今の同梱 DLL（dist 1.11.0）を使う間は、パッケージ化しない Unity アプリに通知のボタンのクリックは届かない。
  1.x ベースのパッケージのマニュアルやリリースノートに書くなら「1.x では、通知のボタンのクリックがパッケージ化しないアプリに届かない。native-toolkit 2.0.0（C ABI）で修正済みで、このパッケージは <版> で取り込む」

以下は原因が分かるまでに確かめたこと。

| 試したこと | 結果 |
|---|---|
| 自動テスト（UI Automation の Invoke）、テスト用 Player | 届かない |
| 開発者がマウスで押す、`-buildWindows64Player` で作った Player（`D:\Build\Windows`） | 届かない |
| PC を再起動してから、開発者がマウスで押す | 届かない |

- **ネイティブの中まで届いていない。** 配布版の DLL も `OutputDebugString` にログを出す。DebugView と同じ仕組み（`DBWIN_BUFFER`）で拾うと、
  `[RegisterActivation] CoRegisterClassObject ok`（CLSID はショートカットの ToastActivatorCLSID と一致）と `[Deliver]` は出るが、
  Open を押したあと `[Activate] COM activator called` は一度も出ない。Unity 側（`UnityMainThreadDispatcher` は押す前からある）の問題でもない
- **Windows は活性化を試みていない。** `Microsoft-Windows-PushNotification-Platform/Operational` には配信（2416 / 2418 / 3052 / 3153）が残り、
  押した時刻は 3055「Some toast notifications have been cleared」だけ。ほかのプロセスも起動しない
- **この PC には古い登録が残っていた。** `HKCU\Software\Classes\AppUserModelId\Unity NativeToolkit` の `CustomActivator` が
  `{B4008734-…}`（LocalServer32 は `d:\build\windows\unity nativetoolkit.exe ----AppNotificationActivated:`）を指していた。
  native-toolkit の設計 v3「背景」にある、v2 時代（unpackaged でも WinAppSDK の `Register()` を使っていた）の登録と見られる。1.x の classic 方式はこれを書きも消しもしない。
  開発者の了承を得て、この値だけ消した（ほかの値はそのまま。消す前のキーは作業フォルダに控えた）。消しても、再起動しても直らなかった。
  **これは見立て違いだった。** この値こそがクリックの届け先を決める仕組みで、古い CLSID を指していたために届かず、消したことで届け先が無くなった
- 記録を見る限り、パッケージ化しない 1.x で「ボタン → callback」が動いたことを確かめた記録は、どちらのリポジトリにも無い
  （native-toolkit の implement-feature-result-v1 は「要実機確認」、同 review v2 は cold start が未解決。こちらの観点表も「実機待ち」のまま）

STA かどうかは関係なかった（native-toolkit の試しは STA で、値を書けば届いた）。

**どちらのリポジトリでも確かめられていなかった理由**: native-toolkit のサンプルはパッケージ化（MSIX）されていて、89 本の UI テストはどれも
AppNotificationManager の登録を通り、classic の受け取り口に届かない。誰かが受け入れた抜けではなく、テストの仕組みからは届かなかった。

**W-04 / W-11 は、dist 1.12.0 へ移行するまで保留。** テスト（`ShowNotification_OpenInTheCenter_ComesBackAsInvoked`）は `[Ignore]` にして残す。
テストを通すために手でレジストリに値を書くことはしない。利用者が出荷版の 1.x で当たる不具合を、テストから隠すことになるため。
テストは押す前に、`CustomActivator` があり、その CLSID の `LocalServer32` がこの Player の exe を指すことを確かめ、違えば理由を出して失敗する。
届かなかったときの記録（dispatcher の有無、マネージャーの event、Player の数）も入れてある。

**これは利用者に影響する。** 少なくともこの PC では、パッケージ化しない 1.x の通知のボタンがアプリに届かない。2.0.0 でも同じ経路なら、移行後も同じ。

## 3. この PC に触れるもの

通知のテストは、すべて `--include-destructive` のときだけ走らせる。

- **通知センター**: 開発者の通知センターに実際に通知が並び、通知センターが開閉する。RemoveAll で消すのは自アプリの通知だけ
- **レジストリ・スタートメニュー**: 1.x は初期化のたびに HKCU（`Software\Classes\CLSID\{clsid}\LocalServer32`、`Software\Classes\AppUserModelId\Unity NativeToolkit`）を書き、
  スタートメニューに `Unity NativeToolkit.lnk` を作る。**この PC には手動確認のときのもの（`D:\Build\Windows\Unity NativeToolkit.exe`）が既にある。**
  lnk は作り直されないが、`LocalServer32` はテストのたびにテスト用 Player（`Temp/` の下で、実行後に消える）を指すように書き換わる。
  開発用ビルドを起動すれば書き戻る。それまでの間、アプリが動いていないときに古い通知の Open を押しても何も起きない
- 管理者権限では動かさない（1.x は、Show が黙って失敗しうるという警告を出す）

## 4. 2.0.0 への移行で変わる期待値

`artifact/topics/windows-c-abi-2/README.md` 5.3 のとおり。特にテストに効くもの:

- 2 回目の初期化は、何もしない成功ではなく `NOT_SUPPORTED` になる。テストをまたいで Manager が残る（DontDestroyOnLoad）ので、W-11 と各テストの Initialize の扱いが変わる
- タイムスタンプが秒からミリ秒になる
- 閉じたあとに、飛んでいた活性化が 1 件だけ届くことがある
