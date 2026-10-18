# Android の C ABI `2.0.0` への移行の段 4（旧ブリッジの撤去、改名、文書、リリース）v3

## 基本情報

| 項目 | 内容 |
|---|---|
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（5 章の段 4、4 章の J-10〜J-12） |
| 改名の課題 | `artifact/topics/os-prefix-violations/README.md`（案 1。この版で範囲を Android に絞った。2 章） |
| レビュー | `artifact/topics/android-c-abi-2/reviews/2026-10-13-android-c-abi-stage4-design-review-v1.md`（v2 を見た。サブエージェントの S1-*、Codex の X1-*） |
| 前提 | 段 3 が終わった（4 機能がすべて C ABI を呼ぶ。`cb428dd`）。1.x のブリッジのクラスを呼ぶ実行時のコードは無い（`Runtime/` を `AndroidJavaObject` / `AndroidJavaProxy` で検索すると、Clipboard のサンプルが FileProvider を引く所だけ。`android.unity.` の文字列は、1.x のアクション ID を保つ定数〈`AndroidNotificationManager.cs:63,68`、`AndroidNotificationCApi.cs:57,60`〉に残るが、クラスの参照ではない。S1-13） |
| 出す版 | 版 `1.13.0`（メジャーにしない。改名とビルダーの削除は破壊的変更として書く） |
| native-toolkit | dist `1.13.0` の `android/` に AAR 2 本（`android-native-toolkit-2.0.0.aar` の md5 9e068bc9…、`android-native-toolkit-capi-2.0.0.aar` の md5 c706a8a6…。同梱のものと一致。`m2/` にも同じもの）。**native-toolkit の版 `1.13.0` のタグはまだ無い**（2026-10-13。最新のタグは版 `1.12.0`。HEAD は `a7684dee`） |

## v2 からの主な変更（v3。レビュー v1 と利用者の判断を受けて。2026-10-13）

- **改名の範囲を Android だけにし、Payload も含めた**（2 章。S1-1。利用者の判断）: Android の接頭辞の無い public 型 37（結果型など 14、Payload 23）。`IconConfiguration` と iOS・macOS の型は、macOS（と iOS）の対応の作業で行う。そのため S1-2（`MacIconConfiguration` のガード）と X1-7（macOS のサンプル）は、この段では起きない
- **改名した型に `[MovedFrom]` を付ける。効くかを先に確かめる**（2.3。S1-14。利用者の判断）
- **移行ガイドは各機能のページの「Changes in `1.13.0`」の節（3 言語）、リリースノートに「Breaking Changes」の節を必須にする**（4 章。S1-6、X1-5。利用者の判断）
- 層 2b のテストの 1.x の分岐を消す（1.4。S1-3、X1-10）
- 削除と改名の `.meta`、置き換える先と置き換えない先、旧名が 0 件になる検査（3.2、3.3。X1-1、X1-2、S1-5、S1-7）。ほかの OS のファイルのコメントの直し（X1-3）
- APK の確かめ方、`--as-package-user` の実行の独立、ピンの読み方（1.2、5 章。S1-10、X1-11、X1-9）
- マニュアルの 1.x のクラス名、移行ガイドの中身（4 章。S1-8、S1-9）
- リリースのチェックリストの Android の行（6 章。S1-11、X1-14）、API の docs の記述（4 章。S1-12、X1-6）、事実の食い違い（S1-13、X1-12、X1-13）

## 1. 何をするか

| # | 中身 | 破壊的変更 |
|---|---|---|
| S4-1 | 旧ブリッジの AAR を外す。層 2b のテストの 1.x の分岐を消す | なし（公開 API は変わらない） |
| S4-2 | 使われていない JSON ビルダーを消す（J-12） | あり（2 型） |
| S4-3 | Android の接頭辞の無い public 型 37 を改名する | あり（37 型） |
| S4-4 | マニュアル（版 `1.13.0`、英・日・韓）と移行の節、README、`Documentation~` の index、リリースの手順の直し | なし |
| S4-5 | リリース | - |

## 2. 改名（S4-3）

### 2.1 対象（37 型。`Android` を前に付ける）

| ファイル（`Runtime/Android/`） | 今 → 改名後 |
|---|---|
| `Clipboard/ClipboardOperationResult.cs` → `AndroidClipboardOperationResult.cs` | `ClipboardOperationResult` → `AndroidClipboardOperationResult` |
| `Clipboard/ClipboardReadResult.cs` → `AndroidClipboardReadResult.cs` | `ClipboardReadStatus`、`ClipItem`、`ClipContents`、`ClipboardReadResult` → `AndroidClipboardReadStatus`、`AndroidClipItem`、`AndroidClipContents`、`AndroidClipboardReadResult` |
| `Clipboard/ClipboardDescriptionResult.cs` → `AndroidClipboardDescriptionResult.cs` | `ClipDescriptionInfo`、`ClipboardDescriptionResult` → `AndroidClipDescriptionInfo`、`AndroidClipboardDescriptionResult` |
| `Clipboard/AndroidClipboardPayloads.cs`（名前は変えない） | `CopyPlainTextPayload`、`CopyHtmlTextPayload`、`CopyUriPayload`、`CopyMultipleTextPayload` → `Android` を前に付ける |
| `Notification/NotificationResult.cs`、`NotificationActionResult.cs`、`NotificationReceivedResult.cs` → `AndroidNotification*.cs` | `NotificationResult`、`NotificationActionResult`、`NotificationReceivedResult` → `AndroidNotificationResult`、`AndroidNotificationActionResult`、`AndroidNotificationReceivedResult` |
| `Notification/AndroidNotificationPayloads.cs`（名前は変えない） | `NotificationResourcePayload`、`ChannelPayload`、`NotificationPayload`、`NotificationActionPayload`、`ScheduledNotificationEnvelopePayload`、`NotificationSchedulePayload`、`NotificationProgressPayload`、`NotificationStylePayload`、`NotificationMessagePayload`、`NotificationDataEntryPayload`、`NotificationViewActionPayload` → `Android` を前に付ける（`AndroidChannelPayload` など） |
| `Share/ShareOperationResult.cs`、`ShareCallbackResult.cs`、`ShareChooserActionResult.cs`、`ShareChooserActionCallbackCoordinator.cs` → `AndroidShare*.cs` | `ShareOperationResult`、`ShareCallbackResult`、`ShareChooserActionResult`、`ShareChooserActionCallbackCoordinator` → `Android` を前に付ける |
| `Share/AndroidSharePayloads.cs`（名前は変えない） | `ChooserActionPayload`、`ShareTextPayload`、`ShareImagePayload`、`ShareImagesPayload`、`ShareFilePayload`、`ShareFilesPayload`、`DirectShareTargetPayload`、`RemoveDirectShareTargetsPayload` → `Android` を前に付ける |

- 2026-10-13 に `Runtime/Android/` の public 型を数えた（`public (sealed|static|readonly)* (class|struct|enum)` で、`Android` で始まらないもの）。37 で、数え漏れは無い（X1-12 の 15 と Payload の 23 の和から、範囲の外の `IconConfiguration` を除いたもの）。新しい名前は、パッケージのどの型ともぶつからない（`grep -w` で確かめた）
- 名前空間は変えない（`JonghyunKim.NativeToolkit.Runtime.Android.<Feature>`。段 3-0 で移した）
- ファイルの改名は `git mv` で `.cs` と `.cs.meta` を対で行い、GUID を保つ
- **範囲の外**（利用者の判断、2026-10-13）: `IconConfiguration`、iOS の 5 型（`NotificationContentPayload` ほか）、macOS の 3 型（`DialogButton` ほか）。macOS（と iOS）の対応の作業で行う。`common.md` の「既知の逸脱」の表と `os-prefix-violations/README.md` は、**消さずに残りに書き換える**（Android の行を消し、iOS・macOS の 9 型を残す。S1-1）

### 2.2 参照の置き換え（3.2 の検査で 0 件にする）

| 置き換える | 中身 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/` | Android の Manager、Bridge、サンプル（`Runtime/UI/Android/`） |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/` | 層 1、層 2a、層 2b |
| `Runtime/Clipboard/IosClipboardOperationResult.cs:15` | コメントの旧名（X1-3。下の「ほかの OS のファイル」） |
| `manual/1.13.0/`（S4-4 で版 `1.12.0` から写した後）、`Documentation~/index{,.ja,.ko}.md`、`README{,.ja,.ko}.md` | 旧名と、旧い名前空間の `using`（4 章） |
| `agent-rules/coding-rules/common.md`（「既知の逸脱」の表を残りに書き換える、`:45` の「旧ブリッジの AAR は段 4 で外す」を過去形に）、`testing.md`（層 1 の表の Android の Share・Clipboard の builder、`:405-406`） | 規則の記述 |
| `agent-rules/workflows/design-feature/workflow.md:52,54`、`review-document/workflow.md:46`、`review-implementation-feature/workflow.md:67`、`review-implementation-sample-scene/workflow.md:62` | 「既知の逸脱 11 件」と、それを前例に引く記述（X1-4、S1-5）。件数と型を、残り（iOS・macOS の 9 型と `IconConfiguration`）に直す |
| `artifact/topics/android-c-abi-2/README.md`（5 章の段 4 の「11 型」）、`artifact/topics/os-prefix-violations/README.md`、`artifact/README.md:59,65` | 範囲と状況 |

| 置き換えない | 理由 |
|---|---|
| `artifact/` の過去の設計・計画・結果・レビュー | 過去の記録 |
| `manual/` と `docs/` の版 `1.12.0` 以前 | 出した版の文書 |
| `Plugins/iOS/*.xcframework` の `.swiftinterface`、`.abi.json` | Swift の同じ名前の型。repo 全体に置き換えをかけると、同梱のフレームワークの中身まで書き換わる（S1-5）。置き換えは 2.2 の上の表のパスだけに当てる |

**ほかの OS のファイル**（X1-3）: `IosClipboardOperationResult.cs:15`（改名する `ClipboardOperationResult` に触れる）、`IosShareJsonBuilder.cs:12`（消す `AndroidShareJsonBuilder`）、`WindowsClipboardJsonBuilder.cs:18`（消す `AndroidClipboardJsonBuilder`）。どれも `<c>` の地の文で、コンパイルでは気づけない。**コメントだけを、存在する新しい名前に直す**（`common.md` の P2「ほかの OS のファイルを変えない」の例外。振る舞いは変えない。v2 は残すとしたが、存在しない型を指したままになる）。

### 2.3 `[MovedFrom]`（利用者の判断、2026-10-13。S1-14）

- 改名した 37 型に `UnityEngine.Scripting.APIUpdating.MovedFromAttribute`（`autoUpdateAPI: true`、`sourceClassName: "<旧名>"`）を付け、Unity の API Updater が利用者のスクリプトの旧名を直せるようにする
- **先に確かめる**（V-S4-4）: Unity 6000.4 で、UPM パッケージのアセンブリの型に付けたとき、利用者のスクリプト（`Assets/` の、旧名を使うスクリプト）が API Updater で直るか。直らなければ付けない（移行ガイドの表だけにする）。確かめは使い捨てのプロジェクトか、作業ツリーに残さない一時のスクリプトで行う
- 付けても付けなくても、破壊的変更として移行ガイドとリリースノートに書く（4 章）

## 3. 削除（S4-1、S4-2）

### 3.1 S4-1: 旧ブリッジの AAR

- `Plugins/Android/` の `android-native-toolkit-1.3.0.aar`、`unity-android-native-toolkit-1.3.0.aar` と、その `.meta` を `git rm` する
- `Plugins/Android/VERSION.txt` の `extra_dist_version`、`extra_aars` の行と、その説明のコメント（`:15-16`、`:22-24`、`:31-33`）を、Windows の `VERSION.txt` と同じく過去形の注記に直す（S1-13）
- `PreBuildProcessor` の `AndroidPin` から、追加の組（`ExtraDistVersion`、`ExtraAars`）、それを読む処理（`:220-227`）、それを写す処理、ログの追加の部分（`:416`）を消す。残った `extra_*` のキーは、どのコードも読まないので無視される。XML コメント（`:186-188`）を Windows（`:100-105`）と同じ形に直す。`AndroidPin` は define の要るアセンブリの private の型で、EditMode のテストは無い（S1-13）。追加のキーを読むコードが無いことは、`grep -n extra_ PreBuildProcessor.cs` が XML コメントのほかに当たらないことで確かめる（X1-9）
- 旧 2 本の manifest、クラス、リソース、`proguard.txt`（空）は AAR の中にあり、AAR を消せば消える。パッケージの `androidlib` は git で管理しない空のローカルのフォルダで、関わらない（X1-14 の確かめ、S1-13）
- `2.0.0` の AAR の `LegacyScheduleCleaner`（1.x の予約を起動時に 1 回だけ消す）は `2.0.0` の側にあるので残る（移行の節に書く。Notification の J-N19）

### 3.2 S4-1: 層 2b のテストの 1.x の分岐（S1-3、X1-10）

- 6 ファイルの `OnCApi` の分岐（56 か所。`AndroidNotificationPlayerTests` 24、`AndroidClipboardManagerIntegrationTests` 13、`AndroidSharePlayerTests` 10 ほか）から、1.x の側を消し、C ABI の期待だけにする。`OnCApi` の判定そのものを、各 fixture の `SetUp` の `Assert.IsTrue(OnCApi, "...")` に置き換える（継ぎ目が消えたり名前が変わったりしたら、1.x の期待で黙って通るのではなく落ちる）。`NTK-*-RECORD` のログは消す
- 1.x の記録は、各機能の UI テスト計画と設計の 1.x の列に残っている
- Share の実装結果 v2 の 7 章の約束（「1.x のピンを外すときに消す」）を果たす

### 3.3 S4-2: JSON ビルダー（J-12）

- `Runtime/Android/Share/AndroidShareJsonBuilder.cs`、`Runtime/Android/Clipboard/AndroidClipboardJsonBuilder.cs`、`Tests/Runtime/AndroidShareJsonBuilderTests.cs`、`Tests/Runtime/AndroidClipboardJsonBuilderTests.cs` を、それぞれの `.cs.meta` と対で `git rm` する（X1-1）
- 消した型に触れる Android のコメント（`AndroidClipboardCApi.cs:103`、`AndroidClipboardJsonBuilder.cs` を指すほかの所）を直す。ほかの OS のファイルは 2.2 のとおり
- `testing.md` の層 1 の表（`:405-406`）の Android の Share・Clipboard から「builder」を消す。EditMode のテストは 37 本減る（Share 21、Clipboard 16。S1-13）
- **残す**: `AndroidNotificationJsonBuilder`（J-12）。未コミットの XML コメントの直し（「段 4 で消す」を消した。2026-10-13）は、このコミットに入れる

### 3.4 旧名が 0 件になる検査（S1-7、X1-2）

`scripts/verify_manual.sh` の検査 2 は接頭辞の付いた名前しか調べない（`:211`）ので、別に次を流す。0 件を、S4-3 と S4-4 のコミットの条件にする。

```
rg -nw '<37 の旧名を | でつないだもの>|AndroidShareJsonBuilder|AndroidClipboardJsonBuilder' \
  Packages/com.jonghyunkim.nativetoolkit/Runtime Packages/com.jonghyunkim.nativetoolkit/Tests \
  Packages/com.jonghyunkim.nativetoolkit/Documentation~/index*.md README*.md manual/1.13.0 agent-rules \
  -g '!*.meta'
```

- 当たってよいのは、移行の節の旧名 → 新名の表と、`[MovedFrom]` の `sourceClassName` だけ。その行は、表の行の印（例えば `| 旧名 |`）か `MovedFrom` を含むことで見分け、残りを 0 件とする
- 旧い名前空間（段 3-0 より前の `JonghyunKim.NativeToolkit.Runtime.{Dialog,Share,Clipboard,Notification}` を Android の型に使う `using`）も、`manual/1.13.0` の Android の節で 0 件にする

## 4. 文書（S4-4）

`agent-rules/workflows/write-manual` と `verify-manual` に従う。

- **マニュアル**（`manual/1.13.0/`、英・日・韓。版 `1.12.0` から写して直す）
  - Android の動作環境: ARM64（と x86_64）だけ、IL2CPP だけ（J-10）。Mono は使えない（Unity の Android の Mono は ARMv7 しか組めず、ビルドが止まる。親課題の README の記録 `6.3`、`6.4`。S1-9）
  - 改名した 37 型、消した 2 ビルダー（2 章、3.3）
  - 4 機能の既知の差分: 各機能の設計の 5.3 と、各実装結果の 3.4（Dialog の結果 v4、Share の結果 v2、Clipboard の結果 v3、Notification の結果 v2。S1-9）
  - Notification: `tools:keep`（J-N6）、`IsNotificationScheduled` は `persistAcrossBoot` の予約だけを見る、権限が無いときの失敗（J-N7）
  - **Notification の manifest の例**（`manual/1.12.0/notification.md:619-624`、ja・ko も）が、利用者に 1.x のクラス名の `<service android:name="android.library.notification.presentation.progress.ProgressForegroundService">` を足すよう指示している。`2.0.0` の AAR は、このサービスを `com.jonghyunkim.nativetoolkit.notification.presentation.progress.ProgressForegroundService` として自分で宣言する。版 `1.13.0` のマニュアルからこの例を消す（S1-8）
  - 各「Import the namespace」の節（S1-9）
  - 版 `1.12.0` のマニュアルの、版 `1.13.0` で事実でなくなる記述を直す（例: `dialog.md:835`「The Android, iOS and macOS types keep their namespace.」。S1-13）
- **移行の節**（利用者の判断、2026-10-13。S1-6）: 各機能のページに「Changes in `1.13.0`」の節を 3 言語で置く（版 `1.12.0` の前例: `dialog.md:831`、`clipboard.md:2208`、`notification.md:908`）。中身:
  - 改名の旧名 → 新名の表（その機能の型）と `[MovedFrom]` の有無
  - 消したビルダー（Share、Clipboard）
  - 既知の差分（上）
  - 共通（各ページから 1 つの節へリンクするか、各ページに同じ文を置くかは write-manual の決まりに従う）: 「Configure Gradle Project」の削除、`unityLibrary/build.gradle` に実行時の依存を足すようになったこと（kotlin-stdlib `2.2.21`、coroutines `1.9.0` ほか 7 つ。KGP の適用は無くなった。独自の Gradle のテンプレートや EDM4U を使う利用者は、依存がぶつかることがある。S1-9）、32 ビットのビルドが止まること（J-5）、Mono が使えないこと、1.x が保存した予約は起動時に消えること（J-N19）、1.x が出した通知は `2.0.0` にした後は反応しないこと、`1.12.0` の手順で足した `<service>` の行を消すこと（S1-8）
- **README**（英・日・韓）の機能と動作環境
- **`Documentation~/index{,.ja,.ko}.md`**: 旧名と動作環境（2.2）。**API の docs**（`build-docs.sh` が出す `Documentation~/site`）は gitignore されていて、`docs/<版>` には写さない（`publish_docs.sh` が写すのはマニュアルだけ。`:44-57`）。版 `1.12.0` と同じく、API の docs は公開の作業の外（S1-12、X1-6）。`build-docs.sh` は、旧名が残っていないかの確かめに 1 度流す（`docfx.json` の `allowCompilationErrors: true` のため、成功しても型が欠けていないことは別に確かめる。生成された `api/` に新しい 37 型があり、旧名が無いこと）
- **リリースの手順の直し**（`agent-rules/workflows/release/workflow.md`。S1-11、X1-5。S4-4 のコミットで入れる。リリースより前に要るため）
  - ステップ 3 のチェックリストに Android の AAR の行を足す（6 章）
  - ステップ 4 のリリースノートに「### Breaking Changes」の節を、`feat` などの分類とは別に必須で足す。中身は移行の節の要約と、そのページへのリンク
- `package.json` の版、`description` / `keywords`

## 5. 検証

| # | 何を | どうやって | いつ |
|---|---|---|---|
| V-S4-1 | 旧 2 本を外した後、1.x のクラスが APK に入っていないこと。`2.0.0` の AAR だけで全テストが通ること | `verify_unity_android.sh`（Android™ Profile と `--as-package-user`）。APK は、使い捨ての `-executeMethod` で `BuildPipeline.BuildPlayer` を呼んで `Build/Android/` に出す（作業ツリーに残さない）。`classes*.dex` を `dexdump` して、クラスの記述子 `Landroid/unity/` と `Landroid/library/` が 0 件。merged manifest にも旧い component（`android.unity.*`、`android.library.*`）が 0 件（`apkanalyzer manifest print`）。APK 全体の文字列は見ない（1.x のアクション ID の定数が当たる。S1-10、X1-11） | S4-1 |
| V-S4-1b | `--as-package-user` の実行が、コミットした `Plugins/Android` だけで通ること | コミットの後に `git ls-files Plugins/Android` が `VERSION.txt`、AAR 2 本と、それぞれの `.meta` の 6 つ。`--as-package-user` の実行は `git status` で `Plugins/Android` に差分の無い状態から始める（Android™ Profile の実行の `PreBuildProcessor` が作業ツリーの AAR を消し、スクリプトは戻さないので、2 つの実行は独立していない。S1-10） | S4-1 |
| V-S4-2 | 改名の後のコンパイル | Editor（EditMode と PlayMode。`verify_unity_android.sh` の前半）と、Android の Player のビルド（同じスクリプトの実機の実行。`UNITY_ANDROID && !UNITY_EDITOR` の中はここでしかコンパイルされない）。**ほかの build target は要らない**: 改名・削除した型を Android 以外のコードは参照していない（2026-10-13 に `grep -w` で確かめた。当たるのは `IosClipboardOperationResult.cs:15` のコメントだけ）。`IconConfiguration` は範囲の外（S1-4、X1-8） | S4-2、S4-3 |
| V-S4-3 | Android™ Profile の実行の後、`Plugins/Android` に `VERSION.txt` と 2 本の AAR（と `.meta`）だけが残ること | 実行の後の `ls` | S4-1 |
| V-S4-4 | `[MovedFrom]` が UPM パッケージの型で効くか | 2.3 | S4-3 の前 |
| V-S4-5 | 旧名が 0 件 | 3.4 | S4-3、S4-4 |
| V-S4-6 | マニュアル | `verify_manual.sh 1.13.0` の停止項目 0 件（警告も見て、旧名に関わるものは直す） | S4-4 |

各段の実装の後に、実装のレビュー（サブエージェントと Codex）を回す。S4-1〜S4-3 は機械的な変更が多いので、1 回にまとめてもよい。

## 6. リリース（S4-5）

`agent-rules/workflows/release/workflow.md` に従う。**前提**: native-toolkit の版 `1.13.0` がタグになっていること。S4-1〜S4-4 を先に進め、この段だけをタグまで待つ（利用者の判断、2026-10-13）。

- **チェックリストの Android の行**（利用者の判断、2026-10-13。S1-11、X1-14。4 章で S4-4 のコミットに入れる）: `Plugins/Android/VERSION.txt` の `dist_version` を `<dv>`、`aars` の 2 本を `<aar>` として:
  - `git -C ../native-toolkit diff --quiet <dv> -- dist/<dv>/android` が 0 で終わる（タグと作業ツリーの dist が同じ。AAR、`m2/`、ヘッダーをまとめて照らせる）
  - 同梱の `Plugins/Android/<aar>` の md5 が、`git -C ../native-toolkit show <dv>:dist/<dv>/android/<aar>` の md5 と一致する
  - タグが無ければ ❌
  - Android の `VERSION.txt` には Windows の `source:` に当たる行が無い。足さない（dist の版とタグの名前が同じなので、`dist_version` で足りる）
- **md5 が合わなかったとき**（native-toolkit がタグの前に AAR を作り直したとき）: AAR を差し替え、`AndroidGradleDependencies` の版を `m2/` の `.module` と照らし（J-2 の EditMode のテスト）、通しの検証をやり直す
- `package.json` の版を `1.13.0` にする

## 7. 順とコミット

1. V-S4-4（`[MovedFrom]` の確かめ。作業ツリーに残さない）
2. S4-1（AAR、ピン、テストの 1.x の分岐）。V-S4-1、V-S4-1b、V-S4-3。1 コミット
3. S4-2（ビルダー。`AndroidNotificationJsonBuilder` の XML コメントの直しも）。通しの検証。1 コミット
4. S4-3（改名、`[MovedFrom]`、参照の置き換え、ほかの OS のコメント、agent-rules、artifact の README）。V-S4-2、V-S4-5。1 コミット（参照の置き換えを分けると、途中のコミットがコンパイルできない）
5. 実装のレビュー（S4-1〜S4-3）
6. S4-4（マニュアル、移行の節、README、`Documentation~`、リリースの手順）。V-S4-5、V-S4-6。文書ごとにコミット
7. S4-5（リリース）。native-toolkit の版 `1.13.0` のタグの後
