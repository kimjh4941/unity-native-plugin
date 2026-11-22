# iOS の C ABI 移行（2c）の進め方と、移行全体で決めること v1

- 記録日: 2026-11-14
- 対象: [../README.md](../README.md)（トピック ios-c-abi-2）
- 根拠: native-toolkit の 2c の申し送り（`artifact/topics/ios-c-abi/results/2026-11-07-ios-c-abi-stage2c-handoff.md`。以下「申し送り」）の 5 章「2c で決めること」、[android-c-abi-2](../../android-c-abi-2/README.md) の決定と進め方、1a の結果（[ios-c-abi-handoff](../../ios-c-abi-handoff/README.md)）
- 範囲: **4 機能に共通する事柄と段の分け方だけ**。機能ごとの写し方（対応表 8.3 の各行、エラーの写し、既知の差分）は、段 3 の機能ごとの設計書で決める（Android と同じ）
- 状態: レビュー前

## 1. 前提（事実）

| # | 事実 | 出典 |
|---|---|---|
| F-1 | 1.x と 2.0.0 の `IosLibrary.framework` は同じ名前なので、同じアプリに入れられない。4 機能を一度に切り替える | 申し送り 1 章（D-12、D-20） |
| F-2 | 2.0.0 の配布物は `dist/1.14.0/ios` の `ios-native-toolkit-2.0.0.xcframework` と `ios-native-toolkit-capi-2.0.0.xcframework`。どちらにも `-debug` は無い。ブリッジは無い | native-toolkit-d9 の報告（`3aeeaf3c`） |
| F-3 | `PreBuildProcessor`・`PostBuildProcessor` は `NATIVETOOLKIT_ENABLE_BUILD_STEPS` の付いた asmdef にあり、UPM で入れた利用者のビルドでは動かない。1.x の利用者は、Embed & Sign をメニュー「Configure Xcode Project」（`IosFrameworkPatcher`）か手で行う | `Editor/Build/NativeToolkit.BuildProcessors.Editor.asmdef`、申し送り 3.3、マニュアル `Documentation~/index.md` 103 行（iOS の手順で「Tools -> Native Toolkit -> iOS -> Configure Xcode Project」を案内） |
| F-4 | 2.0.0 は UnityFramework に `UserNotifications.framework` を足さないと、`.mm` がリンクで止まる | 1a の 2 回目の結果（2 章） |
| F-5 | `.mm` は `didFinishLaunching` で `ntk_notification_install` を呼ぶと、リンクの順に関わらず Unity の通知パッケージより後に入り、コールドスタートのタップにも間に合う。パッケージの `AuthorizationRequest` は delegate を上書きするので、その後に入れ直す | 1a の 2 回目の結果 U-3・U-4。申し送り 3.4（H2-2・H2-3） |
| F-6 | コールバックは Unity と同じ main で、関数が戻った後に来る。入口で拒むと、完了は来ず `release` が戻る前に来る | 第 1 部 1.3、1a の U-1 |
| F-7 | コールバックが例外を投げるとアプリが落ちる | 1a の U-9 |
| F-8 | 今の公開の JSON Builder: `IosClipboardJsonBuilder`・`IosShareJsonBuilder`（各 Manager とテストだけが使う）、`IosNotificationJsonBuilder`（Notification の公開 API は JSON の文字列を受け、サンプルがこのビルダーで組む）。`IosClipboardJsonParser`・`IosClipboardJsonReader` は `internal` | `Runtime/` を読んだ |
| F-9 | iOS の型は `Runtime/<Feature>/`（名前空間 `JonghyunKim.NativeToolkit.Runtime.<Feature>`）にあり、接頭辞の無い公開型が 5 つある（`IosNotificationPayloads.cs` の `NotificationContentPayload` ほか）。どちらも「その OS の C ABI 移行で直す」と決まっている | `agent-rules/coding-rules/common.md` |
| F-10 | このリポジトリには iOS の Player のテストを流す仕組みが無い。iOS の PlayMode は Editor の 36 件だけ（ネイティブを呼ばない） | README 6.1 |
| F-11 | 実機（iPhone XS / iOS 18.7.2）は利用者の私物。流す前に毎回 4 項目を示して許可を得る | native-toolkit `agent-rules/device-testing.md` |
| F-12 | capi は Xcode 26.4 以上で組まれている。C ABI だけを使う Unity の利用者は Xcode 26 のどの版でもよい。1a は Xcode 26.3 で組めた | 申し送り 3.5、7.1 |

## 2. 決めること

Android の J-n と区別して、iOS は **I-n** とする。

### I-1 xcframework を Xcode のプロジェクトに入れる口

- **案**: 2 つの xcframework のリンク（UnityFramework）、Embed & Sign（アプリ）、`UserNotifications.framework` の追加、`.mm` の追加（I-2）を、**`NativeToolkit.Editor` の `IPostprocessBuildWithReport`**（新しい `Editor/iOS/IosXcodeProjectSetup.cs`）で行う。`NATIVETOOLKIT_ENABLE_BUILD_STEPS` の外なので、UPM の利用者のビルドでも動く（Android の J-1 と同じ考え）
- 名前は `Plugins/iOS/VERSION.txt`（`IosXcframeworkPin`）から取る。development の扱いは `IosXcframeworkPin.ResolveIn` と同じ。**ただし `NativeToolkit.Editor` からは `PreBuildProcessor.IsDevelopmentBuildInProgress` が見えない**ので、`report.summary.options` を自分で読む（`IPostprocessBuildWithReport` は報告を受ける）
- `PostBuildProcessor` の iOS の部分は消す（同じことを 2 回しない）。`PreBuildProcessor` の dist からの写しは、このリポジトリの開発の道具として残す
- メニュー「Configure Xcode Project」（`IosFrameworkPatcher`、`IosConfigureProjectWindow`）: ビルドで自動になるので**消す**（Android の「Configure Gradle Project」と同じ。移行ガイドに書く）。書き出し済みのプロジェクトに後から入れる口が要るなら、`IosXcodeProjectSetup` の同じ処理をメニューから呼ぶ形で残す（段 1 で、Unity の書き出し（Export）でも `IPostprocessBuildWithReport` が走るかを確かめて決める）
- **段 1 で確かめること**: Unity は `Plugins/iOS` の xcframework（`PluginImporter` で iOS が有効）を、自分で書き出したプロジェクトに写して UnityFramework にリンクするか。するなら、この口は Embed だけを足し、同じ xcframework を 2 回足さない。1.x の `PostBuildProcessor` は同じ道（`Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS`）に写し直していた

### I-2 `.mm` の置き場所と、入れない設定

- **案**: `.mm`（`didFinishLaunching` で `ntk_notification_install` を呼ぶだけ。1a の雛形からログの仕掛けを外したもの）は、`PluginImporter` の効く `Plugins/iOS/` には置かず、パッケージの中のソースとして持ち（例: `Editor/iOS/NativeSource~/IosNotificationInstaller.mm`。`~` で Unity が取り込まない）、I-1 の口が書き出したプロジェクトの UnityFramework に写して足す
  - 理由: UPM のパッケージは利用者の側から `PluginImporter` を変えられない（読み取り専用）。`Plugins/iOS/` に置くと外す手段が無くなる
- **入れない設定**: スクリプトの定義シンボル `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`（Player Settings の iOS）。定義があれば I-1 の口は `.mm` を足さない（`UserNotifications.framework` も足さない）。既定は「入れる」（入れないと前面の通知の表示もコールドスタートのタップも失う。申し送り 3.4）
  - 外したときは、利用者が自分のネイティブのコードで `ntk_notification_install` を `didFinishLaunching` の中で呼ぶ契約。マニュアルに書く
  - 別案: Project Settings の画面の項目（`ScriptableObject`）。画面は作り・保つ手間が大きく、ほかの OS にも設定の画面は無いので採らない

### I-3 Unity の通知パッケージの `AuthorizationRequest` の後の入れ直し（H2-3）

- **案**: `IosNotificationManager` に公開の入れ直しの口（例: `ReinstallNotificationDelegate()`。中で `ntk_notification_install` を呼ぶ）を足し、マニュアルの「Unity の通知パッケージと一緒に使う」の節で「パッケージの `AuthorizationRequest` の後に呼ぶ」と案内する。パッケージの API を包む口は持たない（このパッケージは Unity の通知パッケージに依存しない）
- 戻り値の扱い（`NONE` か `WRONG_THREAD`）と名前は Notification の設計書で決める

### I-4 xcframework の版の固定

- **決まり**（`95905d1`）: `Plugins/iOS/VERSION.txt` で 2 つの完全な名前を固定し、完全一致で選ぶ。切り替えのコミット（段 3 の S）で `dist_version: 1.14.0` と `xcframeworks: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework` にし、`Plugins/iOS` の 1.3.0 の 2 つと `.meta` を消し、2.0.0 の 2 つを置く
- `PreBuildProcessor` が xcframework を消すときに `.meta` を消さない（前から）。切り替えのコミットでは手で消す。直すかは段 4 で決める

### I-5 `NOT_FOREGROUND`、`PERMISSION_DENIED` などの写し方

- **案**: Android の J-6 と同じ。対応表 8.3 の「Unity の包みが保つこと」の列のとおりに保ち、保てないものは既知の差分と移行ガイドに書く。機能ごとの設計書で 1 つずつ決める

### I-6 1.x の振る舞いをどこまで保つか

- **案**: Android の J-11 と同じ。**ふつうの使い方を保つ**（公開 API と、main から 1 回ずつ正しい入力で呼んだときの結果・時点・文言、Editor の結果）。複数のスレッドからの同時の呼び出し、Manager を壊す直前・直後、変わった入力は、いちばん単純な形にして既知の差分に書く。設計で「1.x と同じ」と書く行には、タグ `1.13.0` のコードの位置を付ける

### I-7 C ABI の完了の配送

- **案**: コールバックは Unity と同じ main で関数が戻った後に来る（F-6）ので、**受け口（`[MonoPInvokeCallback]` の static メソッド）で `user_data` の表から枠を取り出し、その場で今の C# のコールバック・イベントに出す**。受け口の本体はすべて try / catch で囲み、利用者のコールバックの例外は `Debug.LogException` にして C へ出さない（F-7）
- 入口で拒んだとき（完了が来ず `release` だけが戻る前に来る）は、今のコールバックに失敗を出すのを**次の `Update` に送る**（同期のうちに利用者のコールバックを呼ばない。申し送り 4.5）。そのための小さな列を `Runtime/iOS/Common/` に置く（Android の `AndroidNativeCallbackQueue` の形を写す。Android と違い、列を通すのはこの場合だけ）
- `UnityMainThreadDispatcher` は使わない（`dispatcher-runs-under-lock`）
- Manager が壊れた後の結果の扱い（Android の J-9 の Share・Clipboard の例外）は、機能ごとの設計書で決める

### I-8 公開の JSON Builder

- **案**: Android の J-12 と同じ。`IosClipboardJsonBuilder` と `IosShareJsonBuilder`（とそのテスト）は**段 4 で消す**（移行の後はどこからも使われない公開の型。破壊的変更として CHANGELOG と移行ガイドに書く）。**`IosNotificationJsonBuilder` は残す**（Notification の公開 API は JSON の文字列を受け、利用者とサンプルがこのビルダーで組む）

### I-9 内部の JSON の型

- **案**: `IosClipboardJsonReader` は、`ntk_clipboard_detect_values_async` の結果と Notification の JSON の解析に要るので残す。`IosClipboardJsonParser` は `DetectValues` の解析（`ParseDetectedValuesResult_*`）の分だけ残す。どこまで残すかと、126 件の JSON のテストの行き先（申し送り 6.3）は、Clipboard と Notification の設計書で決める
- Notification が Clipboard の名前の Reader を使うのは名前が合わないので、Notification の設計書で、Reader を `Runtime/iOS/Common/` に移して改名するか、Notification 用に写すかを決める（`common.md` の「共通化しない理由」は OS の間の話で、同じ OS の機能の間は Common に置ける）

### I-10 名前空間とフォルダ、接頭辞の無い 5 型

- **案**:
  - **フォルダと名前空間**: 段 3 の最初に、iOS の 4 機能の型を `Runtime/iOS/<Feature>/`（`JonghyunKim.NativeToolkit.Runtime.iOS.<Feature>`）にまとめて移す 1 コミットを置く（Android の段 3-0、J-8）。`.cs` と `.meta` を `git mv` し、公開の型には `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>")]` を付ける。振る舞いは変えない
  - **接頭辞の無い 5 型**（`NotificationContentPayload` ほか）: 段 4 で `Ios` を前に付けて改名し、`[MovedFrom(false, sourceNamespace: ..., sourceClassName: ...)]` を付ける（Android の段 4 と同じ）
  - サンプル（`Runtime/UI/iOS/`）はもう正しい形なので動かさない

### I-11 途中のコミットで iOS のアプリが組めない期間

F-1 のため、xcframework を 2.0.0 に替えたコミットから 4 機能の移行が終わるまでの間は、iOS の Player のビルドが Xcode のリンクで止まる（ブリッジの関数が無い）。逆に、2.0.0 に替える前は、C ABI の関数を呼ぶコードはリンクできない。

- **案 A（推奨）**: 段 3 の各機能は、**C# の包みとテストを先に書き、ネイティブを差し込める口で EditMode と Editor の PlayMode で確かめる**（Android の層 2a の `*ManagerEditorTests` の形）。4 機能が揃ったら、**xcframework の切り替えとブリッジの宣言の削除を 1 つのコミット（段 3 の S）**にし、その直後に iOS の Player（シミュレーター）と実機で 4 機能を通して確かめる。各コミットで EditMode と Editor の PlayMode は通る。iOS の Player が組めないのは、各機能の「1.x の呼び出しを C ABI の呼び出しに替えた」コミットから S までの間だけ
- 案 B: 機能ごとに、1.x の呼び出しと C ABI の呼び出しをスクリプトの定義シンボルで切り替える。どのコミットでも iOS の Player が組めるが、4 機能分の二重の経路と、その切り替えのテストが要る。段 4 で消すコードが増える
- 案 C: 段 3 の間は iOS の Player を組まない（案 A と同じだが、S の前に機能ごとの確かめをしない）
- 案 A の弱み: 機能ごとの実機での確かめが S の後にまとまる。S の後に見つかった問題は、どの機能の変更が原因かを切り分ける手間が増える。これを減らすため、段 1 のスパイクで、Unity のパッケージを通した形（I-1・I-2）と `ntk_version`・`.mm` だけは先に実機で確かめる

### I-12 1.x の実機での振る舞いの記録

- Android は、機能ごとに「1.x のまま UI テストを流して記録」してから移した（UI の補助は adb で動かした）。iOS は Unity のアプリを外から動かす仕組みが無く（F-10）、実機の操作は利用者の手による
- **案**: 1.x の記録は、機能ごとの設計書で「確かめる項目の表」を作り、**1.x のまま実機で利用者に操作してもらって記録する**（Android の Clipboard の「設計の 7.3 の表を使う」と同じ形）。1.x は S の後でも、基準のコミット `95905d1` の worktree で組めるので、記録の時期は S の前に限らない。実機は毎回許可を得る（F-11）
- UI の自動化（XCUITest で Unity のアプリを動かす）は作らない。項目が多くなる Notification で手間が大きいと分かったら、その時に提案する

### I-13 iOS の Player のテストを流す仕組み

- **案**: 段 2 で `scripts/verify_unity_ios.sh` を作る（Android の `verify_unity_android.sh` に倣う）。シミュレーター（iPhone 16 / iOS 18.3。最小 iOS の 18 に近い）で、`-runTests -testPlatform iOS`（Unity の Test Runner の Player のテスト）を流し、結果の XML を読む。パッケージの利用者の条件（`NATIVETOOLKIT_ENABLE_BUILD_STEPS` 無し。`Test iOS Profile`）でも流せるようにする。実機の実行は、許可を得たうえで同じスクリプトに機器を渡す形にする
- 段 2 は S の前に作れる（1.x の Player の PlayMode を流す基準にもなる）

## 3. 段の分け方

| 段 | 中身 | 実機 |
|---|---|---|
| 0 | 移す前の基準（EditMode、Editor の PlayMode）。**済み**（README 6.1） | - |
| 1 | スパイク（コミットしない写しで）: I-1 の口と I-2 の `.mm` を写しのプロジェクトで作り、`dist/1.14.0` の 2 つを入れて、シミュレーターと実機で `ntk_version() == 0x020000`、2 つの framework が Embed & Sign で入って起動すること、`.mm` が `NONE` を返すことを確かめる（申し送り 6 章の「最初に確かめること」）。I-1 の確かめ（Unity が自分でリンクするか、Export でも動くか）。結果を README の 6 章に書き、I-1・I-2 を決める | 1 回（許可を得る） |
| 2 | `scripts/verify_unity_ios.sh`（I-13）。1.x のまま、Editor の PlayMode と iOS の Player の PlayMode（シミュレーター）を流す基準を取る | - |
| 3-0 | 4 機能の型を `Runtime/iOS/<Feature>/` に移す（I-10）。振る舞いは変えない。EditMode と Editor の PlayMode が基準と同じに通ることを確かめる | - |
| 3 | 機能ごとに: 設計 → レビュー → 1.x の実機の記録（I-12）→ C# の包みとテストの実装（I-11 の案 A。ネイティブを差し込む口で確かめる）→ 実装のレビュー。順は Dialog → Share → Clipboard → Notification（Android と同じ。小さいものから） | 1.x の記録で各 1 回 |
| 3-S | 切り替え: I-1 の口と I-2 の `.mm` を入れ、`VERSION.txt` を 1.14.0 にし、xcframework を替え、ブリッジの宣言と `PostBuildProcessor` の iOS の部分を消す。シミュレーターの Player のテストと、実機で 4 機能を通す（1.x の記録と比べる）。Release のアーカイブの署名（1a の U-2 の形）と、Validate App（native-toolkit が 2c に回したもの。App Store Connect とやり取りするので、行う前に利用者に聞く） | 1〜数回 |
| 4 | 後片付け: 公開の JSON Builder の 2 つを消す（I-8）、5 型の改名（I-10）、メニューの扱い（I-1）、マニュアル（動作環境、`.mm` と入れない設定、通知パッケージとの共存、移行ガイド）、リリース。版は native-toolkit の次の版に合わせる（Android の 1.13.0 と同じ） | - |

- 各段の終わりに、結果を `results/` に書き、README の 5 章と 6 章を直す
- 設計書のレビューは、サブエージェントと Codex の 2 本（このリポジトリの運用）

## 4. この設計書で決めないこと

- 機能ごとの写し方（対応表 8.3 の各行）、エラーの文言、既知の差分（段 3 の機能ごとの設計書）
- 保ったイベントとコールドスタートのタップを C# にどう出すか（申し送り 5 章の NT-20、1a の U-6 の `launch=1` の扱いを含む。Notification の設計書）
- `ReinstallNotificationDelegate` の名前と戻り値（Notification の設計書）
- マニュアルの章立て（段 4）
