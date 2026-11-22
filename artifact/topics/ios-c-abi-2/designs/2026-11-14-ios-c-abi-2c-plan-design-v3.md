# iOS の C ABI 移行（2c）の進め方と、移行全体で決めること v3

- 記録日: 2026-11-14
- 対象: [../README.md](../README.md)（トピック ios-c-abi-2）
- 前の版: [v1](2026-11-14-ios-c-abi-2c-plan-design-v1.md)、[v2](2026-11-14-ios-c-abi-2c-plan-design-v2.md)。レビュー: [review v1](../reviews/2026-11-14-ios-c-abi-2c-plan-design-review-v1.md)、[review v2](../reviews/2026-11-14-ios-c-abi-2c-plan-design-review-v2.md)
- 根拠: native-toolkit の 2c の申し送り（`artifact/topics/ios-c-abi/results/2026-11-07-ios-c-abi-stage2c-handoff.md`。以下「申し送り」）の 3〜6 章、[android-c-abi-2](../../android-c-abi-2/README.md) の決定と進め方、1a の結果（[ios-c-abi-handoff](../../ios-c-abi-handoff/README.md)）
- 範囲: **4 機能に共通する事柄と段の分け方だけ**。機能ごとの写し方（対応表 8.3 の各行、エラーの写し、既知の差分）は、段 3 の機能ごとの設計書で決める
- 状態: **レビューを止めた**（review v2 で両方の A1 が 0。直したのは review v2 の 3 章の分。0.2）。利用者の判断待ち（5 章）

## 0. 前の版からの変更

### 0.1 v1 から v2

| レビューの指摘 | v2 |
|---|---|
| Codex 1（`NativeToolkit.Editor` がテストのアセンブリ） | **v1 の外でもう壊れていた**ので先に直した（`c1aeca0`。README 6.2 の U-1・U-2）。F-13 |
| A1-1（パッケージの置き場所） | iOS のメニューの道は直した（`c1aeca0`、U-3）。I-1・I-2 に「`Path.GetFullPath("Packages/<name>/...")` で解く」と書いた。各段の確かめに **git の URL で入れた形**（I-17）を足した |
| A2-2（3-0 の `#if` の内側の `using`） | I-10 に足した。各コミットの確かめに **iOS の Unity の書き出し（IL2CPP まで）** を足した（I-11） |
| A2-3（完了をその場で出すと時点が変わる） | I-7 を「受けて積み、次の `Update` で出す」に改めた（Android の J-9 と同じ形） |
| B-4（定義シンボルの読み方） | I-2 に決めた |
| B-5（Xcode の API のコンパイルの条件） | I-1 に決めた |
| B-6（`Initialize()` の意味） | I-3 を書き直した |
| B-7（案 A では 134 関数が S まで通らない） | **案 D（先に切り替える）を推奨に改めた**（I-11） |
| B-8（名前の照合） | I-15 を足した |
| B-9（共通部と版の確かめ） | I-14 を足した |
| B-10（最小 iOS） | I-16 を足した |
| B-11（Simulator.app とクリップボード） | I-13 に入れた |
| B-12（Validate App） | 段 3-E に入れた（Personal Team ではしない） |
| B-13、B-14（通知パッケージとの共存、F-5 の見込み） | F-5 を分け、Notification の段に確かめを足した |
| B-15（冪等） | I-1 に決めた |
| Codex 2（macOS が通知の型を使う） | I-10 を書き直した。**利用者の判断が要る**（5 型の扱い） |
| Codex 3（今の PlayMode のテストは Player で流せない） | 裏を取った（F-16）。段 2 で分ける |
| C の各件 | 該当する所に入れた（F-12、I-1 の Append と `PluginImporter`、I-10 の `[MovedFrom]`、申し送り 5 章の最後の行、結果の返し方、規則の更新、シミュレーターのアーキテクチャ、worktree の但し書き、development の警告） |

### 0.2 v2 から v3（review v2）

| 指摘 | v3 |
|---|---|
| Codex 2.2-1、N-6（I-7 の「呼んだ順」） | 「列に入った順（来た順）に出す。1.x と同じ」に直した |
| Codex 2.2-2（Notification に失敗の経路が無い） | I-11 に、3-S で Notification にも結果を必ず返す仮の経路を足すと書いた |
| N-1（シミュレーターの SDK とアーキテクチャ） | I-13 に決め方を足した。I-16 の文言を直した |
| N-2（名前の照合が何も照らさずに通る） | I-15 を書き直した |
| N-3（クリップボードの隔たりを事実として書いた） | I-13 の最後を書き直した |
| N-4（段 1 の git の URL の確かめにコミットが要る） | 段 1 と I-17 に書いた |
| N-5（F-15 が欠けている） | F-15 と I-10 の案 3 に足した |
| N-10（3-S で段 2 のテストが落ちる） | I-11 と 3-S に書いた |
| N-7・N-8・N-9、余談 | 直さない（段 1・2・4 の作業で扱う。review v2 の 3 章） |

## 1. 前提（事実）

| # | 事実 | 出典 |
|---|---|---|
| F-1 | 1.x と 2.0.0 の `IosLibrary.framework` は同じ名前なので、同じアプリに入れられない | 申し送り 1 章（D-12、D-20） |
| F-2 | 2.0.0 の配布物は `dist/1.14.0/ios` の `ios-native-toolkit-2.0.0.xcframework` と `ios-native-toolkit-capi-2.0.0.xcframework`、`include/NativeToolkitC/` の 5 つ。どちらにも `-debug` は無い。ブリッジは無い | native-toolkit-d9 の報告（`3aeeaf3c`）、`ls` で確かめた |
| F-3 | `PreBuildProcessor`・`PostBuildProcessor` は `NATIVETOOLKIT_ENABLE_BUILD_STEPS` の付いた asmdef にあり、利用者のビルドでは動かない。マニュアルは iOS の手順でメニュー「Configure Xcode Project」を案内する。**そのメニューは git の URL で入れた利用者の環境で道を見つけられなかった**（1.x から。`c1aeca0` で iOS だけ直した） | asmdef、`Documentation~/index.md:103`、README 6.2 の U-3 |
| F-4 | 2.0.0 は UnityFramework に `UserNotifications.framework` を足さないと `.mm` がリンクで止まる | 1a の 2 回目の結果 2 章 |
| F-5 | **確かめたこと**: `.mm` を `didFinishLaunching` で呼ぶと、既定のリンクの順で Unity の通知パッケージより後に入り、コールドスタートのタップにも間に合った。パッケージの `AuthorizationRequest` は delegate を上書きし、以後ライブラリの通知は失われた。**見込み（確かめていない）**: リンクの順を逆にしても `didFinishLaunching` ならパッケージの後になる。`AuthorizationRequest` の後に `ntk_notification_install` を呼び直せば戻る | 1a の 2 回目の結果 U-3・U-4（71、77 行） |
| F-6 | コールバックは Unity と同じ main で、関数が戻った後に来る。入口で拒むと、完了は来ず `release` が戻る前に来る | 第 1 部 1.3、1a の U-1 |
| F-7 | コールバックが例外を投げるとアプリが落ちる | 1a の U-9 |
| F-8 | 公開の JSON Builder: `IosClipboardJsonBuilder`・`IosShareJsonBuilder`（各 Manager とテストだけが使う）、`IosNotificationJsonBuilder`（Notification の公開 API は JSON の文字列を受け、サンプルがこのビルダーで組む）。`IosClipboardJsonParser`・`IosClipboardJsonReader` は `internal` | `Runtime/` |
| F-9 | iOS の型は `Runtime/<Feature>/`（名前空間 `JonghyunKim.NativeToolkit.Runtime.<Feature>`）にある。`common.md` は「iOS / macOS はその OS の C ABI 移行で移す」とし、接頭辞の無い 5 型（`IosNotificationPayloads.cs` の `NotificationContentPayload` ほか）は「iOS の C ABI 移行で直す」（利用者の判断、2026-10-13） | `agent-rules/coding-rules/common.md` |
| F-10 | このリポジトリには iOS の Player のテストを流す仕組みが無い | README 6.1 |
| F-11 | 実機（iPhone XS / iOS 18.7.2）は利用者の私物。流す前に毎回 4 項目を示して許可を得る | native-toolkit `agent-rules/device-testing.md` |
| F-12 | capi は Xcode 26.4 以上（Swift 6.3）で組まれている。C ABI だけを使う Unity の利用者は Xcode 26 のどの版でもよいとされる（D-16）。1a で 26.3 で組めたのは雛形で、`dist/1.14.0` を 26.3 のアプリで使ったことはまだ無い。手元の既定の Xcode は 26.3 | 申し送り 3.5、7.1 |
| F-13 | `NativeToolkit.Editor` は、`c1aeca0` から git の URL で入れた利用者の環境でも組まれる（それまでは `TestAssemblies` の条件で組まれなかった） | README 6.2 |
| F-14 | 1.x の iOS は、ネイティブからの結果とイベントを `UnityMainThreadDispatcher` に積み、次の `Update` で C# のコールバックとイベントに出す | `Runtime/Notification/IosNotificationManager.cs:479, 666-697`、`Runtime/Dialog/IosDialogManager.cs:151`、`Runtime/Common/UnityMainThreadDispatcher.cs:43-61` |
| F-15 | 接頭辞の無い 5 型のうち 3 つ（`NotificationContentPayload`、`TimeIntervalTriggerPayload`、`CalendarTriggerPayload`）は、macOS も使う: `MacNotificationJsonBuilder`（3 か所）、macOS のサンプル `MacNotificationManagerExampleController`（10 か所）、macOS の EditMode のテスト `MacNotificationTests`（4 か所）。どれも Editor でも組まれる | `Runtime/Notification/MacNotificationJsonBuilder.cs:3, 21, 41, 56`、`Runtime/UI/macOS/Notification/MacNotificationManagerExampleController.cs:248-445`、`Tests/Runtime/MacNotificationTests.cs:95-145` |
| F-16 | 今の iOS の PlayMode のテスト（36 件）は Editor 専用。`IosClipboardManagerIntegrationTests` は `#if UNITY_IOS \|\| UNITY_EDITOR` だが、`#if UNITY_EDITOR` の中の口（`ResetForTests` など）を使い、iOS の Player では組めない。`IosShareManagerIntegrationTests` は「iOS 以外か Editor では失敗する」ことを確かめる | `Tests/PlayMode/IosClipboardManagerIntegrationTests.cs:3, 37`、`Runtime/Clipboard/IosClipboardManager.cs:427`、`Tests/PlayMode/IosShareManagerIntegrationTests.cs:57` |
| F-17 | 利用者の Apple のアカウントは Personal Team で、Apple Developer Program に登録していない。配布用の書き出しと Validate App はできない（native-toolkit 側でも残っている） | native-toolkit `results/2026-11-03-ios-c-abi-stage2-working-notes.md` 17.15 |
| F-18 | この Mac では Simulator.app を開いていると、シミュレーターのクリップボードが利用者の Mac のクリップボードと同期する。native-toolkit の決まりでは、agent はシミュレーターを動かす間 Simulator.app を開かない | native-toolkit `agent-rules/device-testing.md` |
| F-19 | Player Settings の iOS の最小は 15.0（`iOSTargetOSVersionString`）。18.0 は Build Profile の側だけ。2.0.0 の xcframework の最小は 18.0 | `ProjectSettings/ProjectSettings.asset:197`、申し送り 3.5 |
| F-20 | 1.x の `IosNotificationManager.Initialize()` は公開で、XML コメントは「delegate として登録する。`Awake` で自動で呼ぶ。やり直すときだけ手で呼ぶ」。中で `notificationSetup()` と受け手の設定を行う | `Runtime/Notification/IosNotificationManager.cs:237-254` |

## 2. 決めること

Android の J-n と区別して、iOS は **I-n** とする。

### I-1 xcframework を Xcode のプロジェクトに入れる口

- **案**: 2 つの xcframework のリンク（UnityFramework）、Embed & Sign（アプリ）、`UserNotifications.framework` の追加、`.mm` の追加（I-2）を、**`NativeToolkit.Editor` の `IPostprocessBuildWithReport`**（新しい `Editor/iOS/IosXcodeProjectSetup.cs`）で行う。`NATIVETOOLKIT_ENABLE_BUILD_STEPS` の外なので、利用者のビルドでも動く（F-13。Android の J-1 と同じ考え）
- **パッケージの道**: `Path.GetFullPath("Packages/com.jonghyunkim.nativetoolkit/...")` で解く（`c1aeca0` の `IosFrameworkPatcher` と同じ。埋め込みでも `Library/PackageCache` でも実際の場所になる）。`Application.dataPath` からは組まない
- **コンパイルの条件**: Xcode の API（`UnityEditor.iOS.Xcode`）に触る受け口と処理は `#if UNITY_IOS` で囲む（iOS のモジュールが無い Editor でも `NativeToolkit.Editor` が組めるように。Android の `#if UNITY_ANDROID` と同じ）。入れる名前・入れるかどうかの判断は、条件の外の純粋な関数にして EditMode で確かめる
- **名前**: `Plugins/iOS/VERSION.txt`（`IosXcframeworkPin`）の 2 つ。2.0.0 に `-debug` は無いので development を区別せず、`ResolvePresentIn`（フォルダーにある形）を使う（Test Runner のビルドのたびの警告を出さない）
- **冪等**: 何度流しても 1 回。足す前に、同じ道のファイルが Xcode のプロジェクトにあるか（`PBXProject.FindFileGuidByProjectPath`）、Embed と `UserNotifications.framework` と `.mm` がもう足されているかを見る。Unity の書き出しの Append（既にある書き出しに足す）で 2 回流して、重ならないことを確かめる
- `PostBuildProcessor` の iOS の部分は、切り替え（段 3-S）で消す。`PreBuildProcessor` の dist からの写しは、このリポジトリの開発の道具として残す
- **メニュー「Configure Xcode Project」**: 段 1 の結果で決める。Unity が `Plugins/iOS` の xcframework を自分でリンクするなら、Embed だけをする上の口で足りるのでメニューを消す（移行ガイドに書く）。書き出し済みのプロジェクトに後から入れる口が要るなら、メニューは `IosXcodeProjectSetup` の同じ処理を呼ぶだけにする
- **段 1 で確かめること**: (a) Unity は `Plugins/iOS` の xcframework（`PluginImporter` で iOS が有効）を書き出したプロジェクトに写し、UnityFramework にリンクするか。するなら、この口は Embed だけを足し、同じものを 2 回足さない（1.x の `PostBuildProcessor` は同じ道 `Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS` に写し直していた）。(b) `PluginImporter` の iOS の設定（「Add to Embedded Binaries」に当たるもの）で Embed までできるか（申し送り 5 章の選択肢）。できるなら、PBXProject で足すのは `UserNotifications.framework` と `.mm` だけになる

### I-2 `.mm` の置き場所と、入れない設定

- **案**: `.mm`（`didFinishLaunching` で `ntk_notification_install` を呼ぶだけ。1a の雛形からログの仕掛けと `kUnityWillFinishLaunching` の経路を外したもの）は、`PluginImporter` の効く `Plugins/iOS/` には置かず、パッケージの中のソースとして持つ（`Editor/iOS/NativeSource~/IosNotificationInstaller.mm`。`~` で Unity が取り込まない）。I-1 の口が、書き出したプロジェクトの `Libraries/com.jonghyunkim.nativetoolkit/` に写して UnityFramework に足す。道は I-1 と同じく `Path.GetFullPath("Packages/...")`
  - 理由: UPM のパッケージは利用者の側から `PluginImporter` を変えられない。`Plugins/iOS/` に置くと外す手段が無くなる
- **入れない設定**: スクリプトの定義シンボル `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`。**読み方は Editor のコンパイルの `#if`**（`IosXcodeProjectSetup` の中）。Editor のアセンブリは、有効な Build Profile（無ければ Player Settings）の定義で組まれるので、ビルドに使う定義と一致する。`PlayerSettings.GetScriptingDefineSymbols` は Build Profile の上書きを見落とすので使わない。定義があれば `.mm` を足さない（`UserNotifications.framework` も足さない）。既定は「入れる」
  - 外したときは、利用者が自分のネイティブのコードで `ntk_notification_install` を `didFinishLaunching` の中で呼ぶ契約。マニュアルに書く
  - 段 1 で、Build Profile の上書きの有り無しの両方で、定義が効く（`.mm` が外れる）ことを確かめる
  - 別案: Project Settings の画面の項目。画面を作り保つ手間が大きく、ほかの OS にも設定の画面は無いので採らない

### I-3 `Initialize()` と、通知パッケージの後の入れ直し（H2-3）

- **案**: 新しい口は足さず、1.x の公開 `Initialize()`（F-20）を 2.0.0 の入れ直しの口にする。中で `ntk_notification_install` を呼び、受け手を登録する（登録は差し替えとして扱う。申し送り 4.4）。XML コメントの約束（「やり直すときに手で呼ぶ」）と合う。マニュアルの「Unity の通知パッケージと一緒に使う」の節で、パッケージの `AuthorizationRequest` の後に `Initialize()` を呼ぶよう案内する
- `Awake` から呼ぶのは受け手の登録だけにし、`ntk_notification_install` は呼ばない（起動の後に呼んでも `NONE` を返してログを出すだけで、コールドスタートのタップは失う。delegate は `.mm` が入れる）。こうすると I-2 の入れない設定（`.mm` を外した利用者）でも、`Awake` が delegate を奪わない
- 入れない設定のときに利用者が `Initialize()` を手で呼んだ場合は、入れる（利用者の明示の操作）
- 戻り値（`NONE` / `WRONG_THREAD`）の扱いは Notification の設計書で決める

### I-4 xcframework の版の固定

- **決まり**（`95905d1`）: `Plugins/iOS/VERSION.txt` で 2 つの完全な名前を固定し、完全一致で選ぶ。切り替え（段 3-S）で `dist_version: 1.14.0` と `xcframeworks: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework` にし、`Plugins/iOS` の 1.3.0 の 2 つと `.meta` を消し、2.0.0 の 2 つを置く
- `PreBuildProcessor` が xcframework を消すときに `.meta` を消さない（前から）。切り替えでは手で消す。直すかは段 4 で決める

### I-5 `NOT_FOREGROUND`、`PERMISSION_DENIED` などの写し方

- **案**: Android の J-6 と同じ。対応表 8.3 の「Unity の包みが保つこと」の列のとおりに保ち、保てないものは既知の差分と移行ガイドに書く。機能ごとの設計書で 1 つずつ決める

### I-6 1.x の振る舞いをどこまで保つか

- **案**: Android の J-11 と同じ。**ふつうの使い方を保つ**（公開 API と、main から 1 回ずつ正しい入力で呼んだときの結果・時点・文言、Editor の結果）。複数のスレッドからの同時の呼び出し、Manager を壊す直前・直後、変わった入力は、いちばん単純な形にして既知の差分に書く。設計で「1.x と同じ」と書く行には、タグ `1.13.0` のコードの位置を付ける

### I-7 C ABI の完了の配送

- **案**: Android の J-9 と同じ形。**受け口（`[MonoPInvokeCallback]` の static メソッド）は、渡された値を写して（ハンドルは読んで `_free` する）`user_data` の表の枠と一緒に列に積むだけ**にし、C# のコールバックとイベントは**次の `Update` で出す**（1.x と同じ時点。F-14）。背景で受けたイベント（1a の U-6）は、1.x と同じく前面に戻った後の `Update` で出る
- 入口で拒んだとき（完了が来ず `release` が戻る前に来る）も同じ列に積む。**出るのは列に入った順（来た順）で、呼んだ順ではない**: 拒んだものは関数が戻る前に積まれ、受け付けたものの完了は後で来るので、先に呼んで受け付けられたものより後に呼んで拒まれたものが先に出ることがある。1.x も両方がディスパッチャーを通ったので同じ。呼んだ順は保証しない
- 列は `Runtime/iOS/Common/IosNativeCallbackQueue.cs`（Android の `AndroidNativeCallbackQueue` の形を写す。iOS ではコールバックが Unity の main で来るのでロックは要らないが、Android と同じ作りにして、例外を投げた処理が残りを止めないことを保つ）
- 受け口の本体はすべて try / catch で囲み、例外を C へ出さない（F-7）。利用者のコールバックの例外は、列から出す側で `Debug.LogException` にする
- `UnityMainThreadDispatcher` は使わない（`dispatcher-runs-under-lock`）
- Manager が壊れた後の結果の扱い（Android の J-9 の Share・Clipboard の例外）は、機能ごとの設計書で決める

### I-8 公開の JSON Builder

- **案**: Android の J-12 と同じ。`IosClipboardJsonBuilder` と `IosShareJsonBuilder`（とそのテスト）は**段 4 で消す**（移行の後はどこからも使われない公開の型。破壊的変更として CHANGELOG と移行ガイドに書く）。**`IosNotificationJsonBuilder` は残す**

### I-9 内部の JSON の型

- **案**: `IosClipboardJsonReader` は、`ntk_clipboard_detect_values_async` の結果と Notification の JSON の解析に要るので残す。`IosClipboardJsonParser` は `DetectValues` の解析の分だけ残す。どこまで残すかと、126 件の JSON のテストの行き先（申し送り 6.3）は、Clipboard と Notification の設計書で決める
- Notification が Clipboard の名前の Reader を使うのは名前が合わないので、Notification の設計書で、Reader を `Runtime/iOS/Common/` に移して改名するか、Notification 用に写すかを決める

### I-10 名前空間とフォルダ、接頭辞の無い 5 型

- **フォルダと名前空間**: 段 3-0 で、iOS の 4 機能の型を `Runtime/iOS/<Feature>/`（`JonghyunKim.NativeToolkit.Runtime.iOS.<Feature>`）にまとめて移す 1 コミットを置く（Android の段 3-0）。`.cs` と `.meta` を `git mv` し、公開の型には `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>")]` を付ける。振る舞いは変えない
  - ディレクトリ名は `common.md` の規則（402 行）の `iOS`（申し送り 5 章は `Ios` と書くが、規則と `UI/iOS/` に合わせる）
  - サンプル（`Runtime/UI/iOS/`）と `Assets/Scripts/Runtime/UI/iOS/` はフォルダを動かさないが、**`using` を直す**（`#if UNITY_IOS && !UNITY_EDITOR` の内側を含む。例: `IosDialogManagerExampleController.cs:7-9`、`Assets/Scripts/Runtime/UI/iOS/Dialog/IosDialogManagerTest.cs:4-5`）
  - 3-0 の確かめに **iOS の Unity の書き出し（IL2CPP まで）** を入れる（`#if UNITY_IOS && !UNITY_EDITOR` の中は Editor では組まれない）
- **接頭辞の無い 5 型（`IosNotificationPayloads.cs`）**: 3 つを macOS も使う（F-15）。**利用者の判断が要る**:
  - **案 1（推奨）**: 5 型は今回は動かさず、改名もしない（`Runtime/Notification/` と今の名前空間に残す）。iOS と macOS が共有している型として `common.md` の「既知の逸脱」に書き直し、**macOS の C ABI 移行で、iOS 用と macOS 用に分けて接頭辞を付ける**。iOS の `IosNotificationPayloads.cs` のほかの型は 3-0 で移す（同じファイルの中の 5 型は、別ファイルに分けて残す）
  - 案 2: 今回、iOS 用に `IosNotificationContentPayload` などを作って iOS の API をそれに替え、旧い 5 型は macOS 用として残す。iOS の利用者には破壊的変更（`[MovedFrom]` は旧い型が残るので使えない）になり、移行ガイドに書く
  - 案 3: 今回 5 型を改名して移し、macOS の `MacNotificationJsonBuilder`・サンプル・EditMode のテスト（F-15 の 17 か所）も新しい名前を使うよう直す。macOS のコードに触る（利用者の判断「macOS は外す」に反する）
  - 2026-10-13 の判断（「iOS の C ABI 移行で直す」）を変えることになるので、利用者に決めてもらう
- 改名する場合の `[MovedFrom]` の `sourceNamespace` は、途中の名前空間でなく 1.x の `JonghyunKim.NativeToolkit.Runtime.Notification`（Android の段 4 と同じ。`Runtime/Android/Notification/AndroidNotificationPayloads.cs:12`）。1 つの型に `[MovedFrom]` は 1 つ

### I-11 切り替えの順（案 D に改めた）

F-1 のため、xcframework を 2.0.0 にしたコミットから 1.x のブリッジは無い。

- **案 D（推奨）**: 段 3-0 の後に、**先に切り替える（段 3-S）**: xcframework を 2.0.0 にし、ブリッジの `DllImport` の宣言を消し、まだ移していない機能の iOS の Player の経路は、今の Editor の経路と同じ「iOS 以外か Editor では使えない」失敗に落とす（Clipboard・Notification・Share は `Application.platform` か `#if UNITY_IOS && !UNITY_EDITOR` でその経路を持つ。**Dialog は持たず、いつもネイティブを呼ぶ**（`IosDialogManager.cs` はファイル全体が `#if UNITY_IOS` で、`IPhonePlayer` の分岐が無い）ので、3-S で仮の失敗の経路を足すか、Dialog を 3-S と同じコミットで移す。どちらにするかは Dialog の設計書で決める。**Notification の Editor の経路は失敗を返さず、コールバックを保って戻るだけ**（`IosNotificationManager.cs:264, 312, 417`）なので、そのまま使うと `RequestPermission`・`ShowNotification`・一覧の取得などの結果が返らない。3-S で、結果を返す API が必ず「使えない」で完了する仮の経路を足し、結果の型ごとの「使えない」の値を決める。旧いネイティブの呼び出しは `Initialize`・`OnDestroy` を含めて消す）。その後で機能を 1 つずつ C ABI に移す。**どのコミットでも iOS の Player が組めて**、移した機能はシミュレーター（と許可を得て実機）で確かめられ、問題が出たときに機能ごとに切り分けられる
  - 3-S から最後の機能までの間、このブランチの iOS の Player では、移していない機能が「使えない」になる。ブランチの途中だけで、リリースはしない
  - 1.x の実機の記録（I-12）は 3-S の前に取る
- 案 A（v1 の推奨）: 機能ごとに C# の包みを書き、最後にまとめて切り替える。P/Invoke の宣言・構造体・IL2CPP の `MonoPInvokeCallback` が、4 機能・134 関数とも最後まで Player で通らない（レビュー B-7）ので採らない
- **各コミットの確かめ**: EditMode、Editor の PlayMode、**iOS の Unity の書き出し（IL2CPP まで。`testing.md` の層 0）**。3-S 以降は、移した機能の iOS の Player のテスト（シミュレーター、I-13）も
- **段 2 の Player のテストの扱い**: 段 2 で足す 1.x の「各機能を呼べる」テストは、3-S で「使えない」を期待する形に替え（機能ごとに印を付ける）、機能を移すたびに「呼べる」に戻す。こうして各コミットで Player のテストも通るようにする

### I-12 1.x の実機での振る舞いの記録

- iOS は Unity のアプリを外から動かす仕組みが無く（F-10）、実機の操作は利用者の手による
- **案**: 機能ごとに「確かめる項目の表」を作り（機能ごとの設計書）、**3-S の前に、1.x のまま実機で利用者に操作してもらって記録する**（Android の Clipboard の「設計の 7.3 の表を使う」と同じ形）。実機は毎回許可を得る（F-11）
- 3-S の後でも、基準のコミット `95905d1` の worktree で 1.x を組めるが、1.x と 2.0.0 のアプリは同じ bundle id なので、入れ替えると通知の権限の状態が引き継がれ、比べた結果が変わりうる（消すには毎回の許可が要る）。worktree は Library の取り込みにも時間がかかる。後からの記録は但し書きつきの代わりの手段とする
- UI の自動化（XCUITest で Unity のアプリを動かす）は作らない。Notification で手間が大きいと分かったら、その時に提案する

### I-13 iOS の Player のテストを流す仕組み

- **案**: 段 2 で `scripts/verify_unity_ios.sh` を作る（Android の `verify_unity_android.sh` に倣う）
- **シミュレーター**: iPhone 16 / iOS 18.3（最小 iOS の 18 に近い）。**Simulator SDK と arm64** で組む。Player Settings・`iOS Profile`・`Test iOS Profile` はどれも `iPhoneSdkVersion: 988`（Device SDK）と `iOSSimulatorArchitecture: 0`（x86_64 と読める）で、スクリプトが `-activeBuildProfile` を渡すと Profile の値が使われる。シミュレーターの回は Simulator SDK（989）と arm64、実機の回は Device SDK になるよう、段 2 のスパイクで決める（候補: `ITestPlayerBuildModifier`、実行の前の `-executeMethod` で設定する、シミュレーター用の Profile を足す）。両方の Profile で確かめる。Unity の x86_64 のシミュレーターでの実行は確かめていない（申し送り 7.2）
- **Simulator.app を開かない**（F-18）: 流す前に `pgrep -x Simulator` で開いていないことを確かめ、`xcodebuild` と `simctl` だけで入れて起動する。Unity の Test Runner の `-runTests -testPlatform iOS` は Build and Run で Simulator.app を開きうるので、**段 2 のスパイク**で、Player のテストを組むだけにして（Unity の書き出し）、`simctl` で動かし、結果をアプリの中のファイル（`persistentDataPath`）から読む形が作れるかを確かめる
- **今の PlayMode のテスト（F-16）**: Editor 専用のものは `#if UNITY_EDITOR` で囲み（Editor の 36 件はそのまま）、iOS の Player で流すテストは別に作る（1.x のまま、ネイティブを通す最小のもの: 各機能の呼べること）。Player の基準はこの後に取る
- パッケージの利用者の条件（`NATIVETOOLKIT_ENABLE_BUILD_STEPS` 無し、`Test iOS Profile`）でも流せるようにする。実機は許可を得たうえで同じスクリプトに機器を渡す
- **クリップボード**: Simulator.app を閉じておくのは決まりの手当てで、閉じていればシミュレーターのクリップボードが Mac に届かないことは**確かめていない**（native-toolkit `agent-rules/device-testing.md`）。Player のテストは**既定で名前付きの pasteboard を使い**、一般の pasteboard に書くテストを置くかは Clipboard の設計書で明示して決める

### I-14 iOS の共通部と版の確かめ

- **案**: `Runtime/iOS/Common/IosNativeToolkitCApi.cs`（Android の J-4 と `AndroidNativeToolkitCApi.cs` に倣う）: ライブラリ名 `"__Internal"` の定数、`Common.h` の宣言（`ntk_version`、`ntk_string_*`、`ntk_bytes_*` ほか）、UTF-8 の読み書き、版の確かめ
- **版の確かめ**: `ntk_version()` の上位（major）が 2 でなければ「ネイティブが使えない」とし、各機能は失敗を返す（`AndroidNativeToolkitCApi.EnsureNativeAvailable` と同じ。食い違いは 1 回だけ `LogWarning`）。期待値は iOS の分として持つ（申し送り 3.1 の RP-1）
- Android の初期化の確かめ（`ntk_android_is_initialized`）に当たるものは iOS に無い

### I-15 C# の宣言とヘッダーの照合

- **案**: iOS の C ABI の `extern` の宣言と構造体の定義は、`#if UNITY_IOS && !UNITY_EDITOR` の**外**に置き、Editor でも組まれるようにする（呼び出しだけを `!UNITY_EDITOR` の中に置く）。EditMode のテストで、反射で集めた `[DllImport("__Internal")]` の名前（`ntk_` で始まるもの）が、すべて `dist/<VERSION.txt の dist_version>/ios/include/NativeToolkitC/*.h` にあることを照らし、**集めた数が期待の数と等しい**（0 では通らない）ことも確かめる。dist が無い環境では飛ばす。構造体の配置は、機能ごとに `Marshal.SizeOf` / `OffsetOf` を `StructLayoutTests.m` の値と照らす（申し送り 4.3）
- Android にヘッダーとの名前の照合の前例は無い（Android の J-2 は Gradle の依存の版の照合）。宣言を Editor で組めない事情が機能の設計で出たら、`.cs` のソースから `static extern … ntk_\w+` を読む形に替える

### I-16 最小 iOS

- **案**: Player Settings の iOS の最小を 18.0 にする（F-19。`-activeBuildProfile` を渡さない CLI のテストは Player Settings を使う。Android の minSdk と同じ罠。Profile はもう 18.0）。変えるのは 3-S のコミット（段 1 は写しで確かめるだけ）。`NativeToolkit.Editor` に `IosMinimumVersionCheck`（`IPreprocessBuildWithReport`。Android の `AndroidArchitectureCheck` に当たる）を置き、iOS のビルドで最小が 18.0 より低ければ、分かりやすい文言で止める。18.0 未満で 2.0.0 がどこで止まるかは確かめていない（申し送り 3.5）ので、止めておく

### I-17 利用者の形での確かめ

- **案**: このリポジトリでは、パッケージが `Packages/` に埋め込まれていて、git の URL で入れた利用者と振る舞いが違う（README 6.2）。**確かめるコミットに、確かめたい変更が入っていること**: 段 1 のようにコミットしない写しで行うときは、写しを git のリポジトリにして使い捨てのローカルのコミット（push しない）を作り、`git+file:///<写し>?path=...#<そのコミット>` で入れる。**ビルドの処理・asmdef・パッケージの道に触る段の終わり（段 1、3-S、4）に**、使い捨てのプロジェクトに `git+file:///<このリポジトリ>?path=Packages/com.jonghyunkim.nativetoolkit#<コミット>` で入れて、コンパイル、iOS の書き出し、Embed と `.mm` を確かめる。3-S と 4 では、その形でシミュレーターと（許可を得て）実機でも起動する
- 段 2 のスクリプトに、この確かめを入れるかは段 2 で決める

## 3. 段の分け方

| 段 | 中身 | 実機 |
|---|---|---|
| 0 | 移す前の基準（EditMode、Editor の PlayMode）。**済み**（README 6.1） | - |
| 1 | スパイク（コミットしない写しで。I-17 の確かめは写しの使い捨てのローカルのコミットで）: `dist/1.14.0` の 2 つを入れ、I-1 の口と I-2 の `.mm` を作り、シミュレーターと実機で `ntk_version() == 0x020000`、2 つの framework が Embed & Sign で入って起動すること、`.mm` が `NONE` を返すことを確かめる。I-1 の (a)・(b)、冪等と Append、I-2 の定義の効き方（Build Profile の上書きの有り無し）、I-16、使った Xcode の版（26.3 と 26.6）。I-17 の形でも書き出す。結果を README の 6 章に書き、I-1・I-2 を決める | 1 回（許可を得る） |
| 2 | `scripts/verify_unity_ios.sh`（I-13）。Editor 専用の PlayMode のテストを分け、1.x の Player の最小のテストを足して、シミュレーターの Player の基準を取る | - |
| 3-0 | 4 機能の型を `Runtime/iOS/<Feature>/` に移す（I-10。5 型の扱いは利用者の判断の後）。`using` を直す。EditMode・Editor の PlayMode・iOS の書き出し・シミュレーターの Player が基準と同じ | - |
| 3-R | 機能ごとの設計（写し方、確かめる項目の表）→ レビュー → **1.x のまま実機で記録**（I-12）。4 機能分を 3-S の前に済ませる | 機能ごとに 1 回 |
| 3-S | 切り替え（I-11 の案 D）: I-1 の口・I-2 の `.mm`・I-14 の共通部・I-15 の照合のテスト・I-16 を入れ、`VERSION.txt` を 1.14.0 にし、xcframework を替え、ブリッジの宣言と `PostBuildProcessor` の iOS の部分を消し、Player Settings の最小 iOS を 18.0 にする。4 機能は「使えない」に落ちる（Dialog と Notification には仮の経路を足す。I-11）。段 2 の Player のテストを「使えない」を期待する形に替える。`ntk_version` と `.mm` をシミュレーターと実機で確かめ、I-17 の形でも確かめる | 1 回 |
| 3 | 機能ごとに: C ABI の包みの実装 → EditMode・Editor の PlayMode・シミュレーターの Player → 実機で 1.x の記録と比べる → 実装のレビュー。順は Dialog → Share → Clipboard → Notification（Android と同じ）。**Notification では、Unity の通知パッケージとの共存（F-5 の見込みと I-3 の `Initialize()` での入れ直し）を、使い捨てのプロジェクトで実機で確かめる**。各機能で `common.md`（「iOS / macOS」の節）と `testing.md`（ガードの表の iOS）を直す | 機能ごとに 1 回以上 |
| 3-E | Release のアーカイブで 2 つの framework がアプリの証明書で署名し直されること（1a の U-2 の形、`codesign`）。**Validate App と配布用の書き出しは、Apple Developer Program に登録していなければ行わない**（F-17）。行う場合も、App Store Connect とやり取りするので前に利用者に聞く | - |
| 4 | 後片付け: 公開の JSON Builder の 2 つを消す（I-8）、5 型（I-10 の判断による）、メニューの扱い（I-1）、マニュアル（動作環境 iOS 18.0、`.mm` と入れない設定、通知パッケージとの共存と `Initialize()`、移行ガイド、git の URL での導入）、リリース 1.14.0（README 6.2 の直しを含む）。リリースの前に I-17 の形で Android と iOS を実機で通す | 1 回以上 |

- 各段の終わりに、結果を `results/` に書き、README の 5 章と 6 章を直す
- 設計書と実装のレビューは、サブエージェントと Codex の 2 本（このリポジトリの運用）
- **native-toolkit への結果の返し**（申し送り 8 章。`results/` の写しと要約）は、段 3-S、段 3 の各機能、段 4 の終わりに行う。外へ出す操作なので、行う前に利用者に聞く
- 申し送り 5 章の最後の行（1.x の Swift の `IosNotificationManager.setup()` を呼ぶ利用者のネイティブのコード）は、このリポジトリにネイティブのソースが無いので当てはまらない

## 4. この設計書で決めないこと

- 機能ごとの写し方（対応表 8.3 の各行）、エラーの文言、既知の差分（段 3 の機能ごとの設計書）
- 保ったイベントとコールドスタートのタップを C# にどう出すか（申し送り 5 章の NT-20、1a の U-6 の `launch=1` の扱いを含む。Notification の設計書）
- `Initialize()` の戻り値（Notification の設計書）
- マニュアルの章立て（段 4）

## 5. 利用者に決めてもらうこと

1. **I-10 の 5 型の扱い**（案 1 / 案 2 / 案 3）
2. **I-11 の切り替えの順**（案 D / 案 A）
3. ほかの I-n は推奨のままでよいか
