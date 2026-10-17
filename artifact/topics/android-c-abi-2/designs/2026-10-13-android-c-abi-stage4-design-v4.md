# Android の C ABI 2.0.0 への移行の段 4（旧ブリッジの撤去、改名、文書、リリース）v4

## 基本情報

| 項目 | 内容 |
|---|---|
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（5 章の段 4、4 章の J-10〜J-12） |
| 改名の課題 | `artifact/topics/os-prefix-violations/README.md`（案 1。範囲を Android に絞った。2 章） |
| レビュー | `artifact/topics/android-c-abi-2/reviews/2026-10-13-android-c-abi-stage4-design-review-v1.md`（v2 を見た）、`...-review-v2.md`（v3 を見た。サブエージェントの S2-*、Codex の X2-*） |
| 前提 | 段 3 が終わった（4 機能がすべて C ABI を呼ぶ。`cb428dd`）。1.x のブリッジのクラスを呼ぶ実行時のコードは無い（`android.unity.` の文字列は、1.x のアクション ID を保つ定数〈`AndroidNotificationManager.cs:63,68`、`AndroidNotificationCApi.cs:57,60`〉に残るが、クラスの参照ではない） |
| 出す版 | 版 `1.13.0`（メジャーにしない。名前空間の移動、改名、ビルダーの削除は破壊的変更として書く） |
| native-toolkit | dist `1.13.0` の `android/` に AAR 2 本（`android-native-toolkit-2.0.0.aar` の md5 9e068bc9…、`android-native-toolkit-capi-2.0.0.aar` の md5 c706a8a6…。同梱のものと一致）。**native-toolkit の版 `1.13.0` のタグはまだ無い**（2026-10-13。最新のタグは版 `1.12.0`、HEAD は `a7684dee`） |

## v3 からの主な変更（v4。レビュー v2 を受けて。2026-10-13）

- **利用者から見た名前空間の移動を、破壊的変更として扱う**（S2-1。A1）: 段 3-0（`44f206d`）はタグ `1.12.0` の後なので、`1.12.0` の利用者から見ると、Android の public 型 42 個（改名する 37 と、名前を残す 5: `AndroidDialogManager`、`AndroidShareManager`、`AndroidClipboardManager`、`AndroidNotificationManager`、`AndroidNotificationJsonBuilder`）の名前空間が `Runtime.<Feature>` から `Runtime.Android.<Feature>` に変わる（`git show 1.12.0:...` で確かめた）。移行の節、リリースノート、`[MovedFrom]` の引数、検査の対象に入れた（2.1、2.3、3.4、4 章）
- **`[MovedFrom]` は、確かめの結果によらず 42 型に付ける**（X2-1。A1）: 付ける理由は 2 つある。1 つは API Updater による利用者のスクリプトの書き換え（名前空間の移動は直り、クラス名の変更は直らない見込み。Codex が UnityCsReference から引いた）。もう 1 つは `[Serializable]` の Payload のシリアライズの移行。確かめるのは、どちらがどこまで効くかで、結果は移行の節に書く（「自動で直るもの」と「手で直すもの」）。v3 は「直らなければ付けない」としていたが、利用者の判断（付ける）と属性の用途に合わない
- **消えたメニュー「Configure Gradle Project」の手順を、マニュアルと `Documentation~` から消す**（S2-2。A1）。日本語版は綴りが「Configue」なので、検査は揺れを拾う形にする
- 検査（3.4）を段ごとに分け、許す行をファイルと節で決め、旧い名前空間・旧い手順・1.x のクラス名を足した（S2-9、X2-4、X2-5）。パスをパッケージからの完全なものにした（X2-2）
- APK は、検証のスクリプトに `--pull-apk` を足して、テストの Player を端末から取る（X2-3、S2-4）。陽性の対照を置く。`Plugins/Android` の確かめ方を、`git status` と `PreBuildProcessor` のログで見る形にした（S2-3）
- リリースのチェックリストの行に、検証に使った native-toolkit のコミットとの差を足した（S2-5）。Breaking Changes の節を出す条件を決めた（S2-6）。共通の移行の項目の置き場所を決めた（S2-7）
- マニュアルの起点のコミットを順に入れた（X2-6）。件数・参照・コミットの割り当ての食い違いを直した（S2-8、S2-10〜S2-13）

## 1. 何をするか

| # | 中身 | 破壊的変更 |
|---|---|---|
| S4-1 | 旧ブリッジの AAR を外す。層 2b のテストの 1.x の分岐を消す | なし（公開 API は変わらない） |
| S4-2 | 使われていない JSON ビルダーを消す（J-12） | あり（2 型） |
| S4-3 | Android の接頭辞の無い public 型 37 を改名し、42 型に `[MovedFrom]` を付ける | あり（37 型の名前。42 型の名前空間は段 3-0 で動いた） |
| S4-4 | マニュアル（版 `1.13.0`、英・日・韓）と移行の節、README、`Documentation~` の index、リリースの手順の直し | なし |
| S4-5 | リリース | - |

## 2. 改名と名前空間（S4-3）

### 2.1 対象

**改名する 37 型**（`Android` を前に付ける。名前空間は今の `JonghyunKim.NativeToolkit.Runtime.Android.<Feature>` のまま）:

| ファイル（`Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/`） | 今 → 改名後 | タグ `1.12.0` の名前空間 |
|---|---|---|
| `Clipboard/ClipboardOperationResult.cs` → `AndroidClipboardOperationResult.cs` | `ClipboardOperationResult` → `AndroidClipboardOperationResult` | `Runtime.Clipboard` |
| `Clipboard/ClipboardReadResult.cs` → `AndroidClipboardReadResult.cs` | `ClipboardReadStatus`、`ClipItem`、`ClipContents`、`ClipboardReadResult` → `AndroidClipboardReadStatus`、`AndroidClipItem`、`AndroidClipContents`、`AndroidClipboardReadResult` | 同上 |
| `Clipboard/ClipboardDescriptionResult.cs` → `AndroidClipboardDescriptionResult.cs` | `ClipDescriptionInfo`、`ClipboardDescriptionResult` → `AndroidClipDescriptionInfo`、`AndroidClipboardDescriptionResult` | 同上 |
| `Clipboard/AndroidClipboardPayloads.cs`（ファイル名は変えない） | `CopyPlainTextPayload`、`CopyHtmlTextPayload`、`CopyUriPayload`、`CopyMultipleTextPayload` → `Android` を前に付ける | 同上 |
| `Notification/NotificationResult.cs`、`NotificationActionResult.cs`、`NotificationReceivedResult.cs` → `AndroidNotification*.cs` | `NotificationResult`、`NotificationActionResult`、`NotificationReceivedResult` → `AndroidNotificationResult`、`AndroidNotificationActionResult`、`AndroidNotificationReceivedResult` | `Runtime.Notification` |
| `Notification/AndroidNotificationPayloads.cs`（ファイル名は変えない） | `NotificationResourcePayload`、`ChannelPayload`、`NotificationPayload`、`NotificationActionPayload`、`ScheduledNotificationEnvelopePayload`、`NotificationSchedulePayload`、`NotificationProgressPayload`、`NotificationStylePayload`、`NotificationMessagePayload`、`NotificationDataEntryPayload`、`NotificationViewActionPayload` → `Android` を前に付ける | 同上 |
| `Share/ShareOperationResult.cs`、`ShareCallbackResult.cs`、`ShareChooserActionResult.cs`、`ShareChooserActionCallbackCoordinator.cs` → `AndroidShare*.cs` | `ShareOperationResult`、`ShareCallbackResult`、`ShareChooserActionResult`、`ShareChooserActionCallbackCoordinator` → `Android` を前に付ける | `Runtime.Share` |
| `Share/AndroidSharePayloads.cs`（ファイル名は変えない） | `ChooserActionPayload`、`ShareTextPayload`、`ShareImagePayload`、`ShareImagesPayload`、`ShareFilePayload`、`ShareFilesPayload`、`DirectShareTargetPayload`、`RemoveDirectShareTargetsPayload` → `Android` を前に付ける | 同上 |

**名前を残し、名前空間だけが動いた 5 型**（段 3-0。S2-1）: `AndroidDialogManager`（`Runtime.Dialog` → `Runtime.Android.Dialog`）、`AndroidShareManager`（`Runtime.Share` → `Runtime.Android.Share`）、`AndroidClipboardManager`（`Runtime.Clipboard` → `Runtime.Android.Clipboard`）、`AndroidNotificationManager` と `AndroidNotificationJsonBuilder`（`Runtime.Notification` → `Runtime.Android.Notification`）。消す 2 ビルダー（3.3）も動いたが、消すので扱わない。

- 2026-10-13 に `Runtime/Android/` の public 型を数えた（`Android` で始まらないもの 37、始まるもの 7。7 のうち 2 は 3.3 で消す）。入れ子の public の型は無い。新しい名前は、パッケージと `Assets/` のどの型ともぶつからない（`grep -w`）。旧名と同じ名前のメンバー、`nameof`、文字列のリテラル、シーン、アセット、`link.xml` は無い（レビュー v2 の S2 の「確かめたこと」）
- ファイルの改名は `git mv` で `.cs` と `.cs.meta` を対で行い、GUID を保つ
- **範囲の外**（利用者の判断、2026-10-13）: `IconConfiguration`、iOS の 5 型（`NotificationContentPayload` ほか）、macOS の 3 型（`DialogButton` ほか）の計 9。macOS（と iOS）の対応の作業で行う。`agent-rules/coding-rules/common.md` の「既知の逸脱」の表（今は Android の 3 行と `IconConfiguration`。`:455-462`）は、Android の 3 行を消し、iOS の 5 型と macOS の 3 型の行を**足す**。見出しの「Runtime 11 件」も 9 件に直す（S2-8）

### 2.2 参照の置き換え

置き換えは下の「置き換える」パスだけに当てる（repo 全体にはかけない）。

| 置き換える | 中身 | 段 |
|---|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/`、`Packages/com.jonghyunkim.nativetoolkit/Tests/` | Android の Manager、Bridge、サンプル（`Runtime/UI/Android/`）、層 1・2a・2b | S4-3 |
| ほかの OS のファイルの 3 か所（下） | コメントだけ | S4-2、S4-3 |
| `agent-rules/coding-rules/common.md`（「既知の逸脱」の表。2.1） | 規則 | S4-3 |
| `agent-rules/workflows/design-feature/workflow.md:52,54`、`review-document/workflow.md:46`、`review-implementation-feature/workflow.md:67`、`review-implementation-sample-scene/workflow.md:62` | 「既知の逸脱 11 件」と、それを前例に引く記述を、残りの 9 件に直す | S4-3 |
| `artifact/topics/android-c-abi-2/README.md`（5 章の段 4 の「11 型」）、`artifact/topics/os-prefix-violations/README.md`、`artifact/README.md:59,65` | 範囲と状況。**手で直す**（`os-prefix-violations/README.md` の旧名 25 か所の多くは経緯の記録で、機械的に置き換えない。S2-8） | S4-3 |
| `manual/1.13.0/`、`Packages/com.jonghyunkim.nativetoolkit/Documentation~/index{,.ja,.ko}.md`、`README{,.ja,.ko}.md` | 旧名、旧い名前空間、旧い手順（4 章） | S4-4 |

| 置き換えない | 理由 |
|---|---|
| `artifact/` の過去の設計・計画・結果・レビュー | 過去の記録 |
| `manual/` と `docs/` の版 `1.12.0` 以前 | 出した版の文書 |
| `Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/*.xcframework` の `.swiftinterface`、`.abi.json` | Swift の同じ名前の型。置き換えると同梱のフレームワークの中身まで変わる |

**ほかの OS のファイル**（X1-3、S2-10）: 振る舞いは変えず、コメントだけを直す（`common.md` の P2「ほかの OS のファイルを変えない」の例外。存在しない型を指したままにしないため）。

| ファイル | 今 | 直した後 | 段 |
|---|---|---|---|
| `Runtime/Clipboard/IosClipboardOperationResult.cs:15` | `ClipboardOperationResult`（`<c>`） | `AndroidClipboardOperationResult` | S4-3 |
| `Runtime/Share/IosShareJsonBuilder.cs:12` | `AndroidShareJsonBuilder`（`<c>`） | 「1.x の Android の builder」（型は消えるので、名前を出さない） | S4-2 |
| `Runtime/Windows/Clipboard/WindowsClipboardJsonBuilder.cs:18` | `AndroidClipboardJsonBuilder`（地の文） | 同上 | S4-2 |

### 2.3 `[MovedFrom]`（利用者の判断、2026-10-13。S1-14、S2-1、X2-1）

- 42 型に `UnityEngine.Scripting.APIUpdating.MovedFromAttribute` を付ける。改名した 37 型は `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>", sourceClassName: "<旧名>")]`、名前を残した 5 型は `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>")]`。`<Feature>` はタグ `1.12.0` の名前空間（2.1 の表）
- **確かめの結果によらず付ける**（X2-1）。効く範囲は 2 つに分けて確かめ（V-S4-4）、移行の節に書く:
  - (a) API Updater が利用者のスクリプトを書き換えるか。名前空間の移動（5 型と、37 型の名前空間の部分）と、クラス名の変更（37 型の名前の部分）を分けて見る
  - (b) シリアライズの移行。`[Serializable]` の Payload（`AndroidClipboardPayloads.cs:9-11` ほか）を、旧名で `[SerializeReference]` に保存したデータが、改名の後に同じ値で読めるか
- enum（`AndroidClipboardReadStatus`）に付けられるかも確かめる（要確認。付けられなければ、その 1 型は付けず、移行の節に書く）

## 3. 削除（S4-1、S4-2）と検査

### 3.1 S4-1: 旧ブリッジの AAR

- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/` の `android-native-toolkit-1.3.0.aar`、`unity-android-native-toolkit-1.3.0.aar` と、その `.meta` を `git rm` する
- `Plugins/Android/VERSION.txt` の `extra_dist_version`、`extra_aars` の行と、その説明のコメント（`:15-16`、`:22-24`、`:30-32`）を、Windows の `VERSION.txt` と同じく過去形の注記に直す
- `Editor/Build/PreBuildProcessor.cs` の `AndroidPin` から、追加の組（`ExtraDistVersion`、`ExtraAars`）、それを読む処理（`:220-229` と、組み立ての `:231-235`）、それを写す処理、ログの追加の部分（`:416`）を消す。XML コメント（`:186-188`）を Windows（`:100-105`）と同じ形に直す
  - 残った `extra_*` のキーは、どのコードも読まないので無視される。ピンのファイルを読む `ReadPinValues`（`:136-160`）は知らないキーを無視する（X1-9。`AndroidPin` は define の要るアセンブリの private の型で、EditMode のテストの仕組みが無い）。確かめは、`grep -n extra_ PreBuildProcessor.cs` が XML コメントのほかに当たらないこと
- 旧 2 本の manifest、クラス、リソース、`proguard.txt`（空）は AAR の中にあり、AAR を消せば消える。1.x の AAR のクラスは `android/library`（245）と `android/unity`（60）だけで、2.0.0 のクラスは `com/jonghyunkim` だけ（レビュー v2 の S2 の「確かめたこと」）
- 2.0.0 の AAR の `LegacyScheduleCleaner` は 2.0.0 の側にあるので残る（移行の節。Notification の J-N19）
- `agent-rules/coding-rules/common.md:45` の「旧ブリッジの AAR は段 4 で外す」を過去形にする（S2-11）

### 3.2 S4-1: 層 2b のテストの 1.x の分岐（S1-3、X1-10）

- 6 ファイルの `OnCApi` の分岐（56 か所）から、1.x の側を消し、C ABI の期待だけにする。`OnCApi` の判定は、各 fixture の `SetUp` の `Assert.IsTrue(OnCApi, "...")` に置き換える（継ぎ目が消えたり名前が変わったりしたら、黙って通るのではなく落ちる）。`NTK-*-RECORD` のログは消す
- 1.x の記録は、各機能の UI テスト計画と設計の 1.x の列に残っている。Share の実装結果 v2 の 7 章の約束を果たす

### 3.3 S4-2: JSON ビルダー（J-12）

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareJsonBuilder.cs`、`Runtime/Android/Clipboard/AndroidClipboardJsonBuilder.cs`、`Tests/Runtime/AndroidShareJsonBuilderTests.cs`、`Tests/Runtime/AndroidClipboardJsonBuilderTests.cs` を、それぞれの `.cs.meta` と対で `git rm` する
- 消した型に触れる Android のコメント（`AndroidClipboardCApi.cs:103`）と、ほかの OS のファイルの 2 か所（2.2）を直す
- `agent-rules/coding-rules/testing.md:405-406` の Android の Share・Clipboard から「builder」を消す。EditMode のテストは 37 本減る（Share 21、Clipboard 16）
- `agent-rules/coding-rules/common.md:518` の「`XxxJsonBuilder` クラスで JSON 組み立てを担い、Manager から呼び出す」に、Android の例外を書く（Share・Clipboard の Manager は Payload を C ABI に渡し、ビルダーを持たない。Notification の `AndroidNotificationJsonBuilder` は、利用者が Manager に渡す JSON を組む道具で、Manager は呼ばない。S2-12）
- **残す**: `AndroidNotificationJsonBuilder`（J-12）。未コミットの XML コメントの直し（2026-10-13）は、このコミットに入れる

### 3.4 検査（段ごと。S2-9、X2-4、X2-5、S2-2）

| 段 | パス | 0 件にするもの |
|---|---|---|
| S4-3 の後 | `Packages/com.jonghyunkim.nativetoolkit/Runtime`、`Packages/com.jonghyunkim.nativetoolkit/Tests`、`agent-rules` | 37 の旧名（単語の境界）、消した 2 ビルダーの名前 |
| S4-4 の最後のコミット | `manual/1.13.0`、`Packages/com.jonghyunkim.nativetoolkit/Documentation~/index{,.ja,.ko}.md`、`README{,.ja,.ko}.md` | 37 の旧名、消した 2 ビルダーの名前、旧い手順（`Config\w* Gradle Project`、`ConfigureGradleProject`。日本語版の綴りの揺れを拾う）、1.x のクラス名（`android\.(library\|unity)\.[a-z]+\.[A-Za-z.]*[A-Z]`）、Android の型と旧い名前空間の組（下） |

- **許す行**（ファイルと節で決める）: `manual/1.13.0/*.md` の「Changes in `1.13.0`」の節の中（旧名 → 新名の表、消したビルダーの説明、旧い名前空間の説明）と、`Runtime/` の `[MovedFrom]` の引数（`sourceClassName` と `sourceNamespace` の値）。`[MovedFrom]` は 1 行で書く（検査が行で見分けられるように）
- **旧い名前空間の組**（X2-4）: `using JonghyunKim.NativeToolkit.Runtime.{Dialog,Share,Clipboard,Notification};` は iOS・macOS・Windows の型では正しいので、ファイル全体の 0 件にはしない。コードのブロック（```` ``` ```` の間）ごとに、`Android` で始まる型（`Android[A-Z]\w*`）を使い、`Runtime.Android.` の `using` が無く、旧い `using` があるものを 0 件にする。小さな Python の検査にして、S4-4 のコミットの前に流す（作業の手順に書く。スクリプトとしてリポジトリに足すかは S4-4 で決める）
- S4-4 の途中のコミット（起点のコピー、機能ごとの直し）では 0 件を求めない（S2-9）
- `scripts/verify_manual.sh` の検査 2 は接頭辞の付いた名前しか調べない（`:211`）ので、この検査を代わりにする

## 4. 文書（S4-4）

`agent-rules/workflows/write-manual` と `verify-manual` に従う。

**順**（X2-6）:
1. **起点**: `manual/1.12.0` を `manual/1.13.0` に写し、すぐにコミットする（`chore(manual): copy manual/1.12.0 to manual/1.13.0 as baseline`。write-manual の手順。**push はしない**: このリポジトリの約束で、push は利用者が行う）
2. 機能ごと（index、dialog、share、clipboard、notification）に、3 言語をまとめて直し、1 コミットずつ
3. README、`Documentation~/index*`
4. リリースの手順の直し（下）
5. 3.4 の検査、`verify_manual.sh 1.13.0`、`scripts/publish_docs.sh 1.13.0`

**中身**:
- **Android の動作環境**: ARM64（と x86_64）だけ、IL2CPP だけ（J-10）。Mono は使えない（Unity の Android の Mono は ARMv7 しか組めず、ビルドが止まる。親課題の README の記録 `6.3`、`6.4`）
- **「Configure Gradle Project」の手順を消す**（S2-2）: `manual/1.12.0/index{,.ja,.ko}.md:185` の手順（日本語版は「Configue」）と、`Documentation~/index{,.ja,.ko}.md:95`。画像 `images/editor/ConfigureGradleProject.png` は `manual/1.13.0` から消す（`manual/1.12.0` の画像は残る）。Gradle の依存は `IPostGenerateGradleAndroidProject` が足すので、利用者の手順は要らない（J-1）
- **Notification の manifest の例を消す**（S1-8）: `notification.md:619-624`（3 言語）の、1.x のクラス名の `<service>` の行
- 4 機能の既知の差分（各機能の設計 5.3 と、各実装結果の 3.4: Dialog の結果 v4、Share の結果 v2、Clipboard の結果 v3、Notification の結果 v2）
- Notification の `tools:keep`（J-N6）、`IsNotificationScheduled` は `persistAcrossBoot` の予約だけを見る、権限が無いときの失敗（J-N7）
- 各「Import the namespace」の節を `Runtime.Android.<Feature>` にする（S1-9）
- 版 `1.12.0` のマニュアルの、版 `1.13.0` で事実でなくなる記述を直す（例: `dialog.md:835`「The Android, iOS and macOS types keep their namespace.」）

**移行の節**（利用者の判断、2026-10-13。S1-6、S2-7）:
- **共通の節**: `index{,.ja,.ko}.md` の Android の動作環境の下に「Changes in `1.13.0` (Android)」を置く。中身:
  - **名前空間**: Android の 42 型（2.1）が `Runtime.<Feature>` から `Runtime.Android.<Feature>` に移った。iOS・macOS・Windows の型は動いていない。`using` の直し方の例（S2-1）
  - **改名**: 37 型の旧名 → 新名の表
  - `[MovedFrom]` で自動で直るもの・手で直すもの（V-S4-4 の結果）
  - 1.x の AAR（`android-native-toolkit-1.3.0.aar`、`unity-android-native-toolkit-1.3.0.aar`）を同梱しなくなった。その中のクラス（`android.unity.*`、`android.library.*`）を直接使っていたコードは動かない（S2-7）
  - 「Configure Gradle Project」が無くなったこと。`unityLibrary/build.gradle` に実行時の依存を足すようになったこと（kotlin-stdlib `2.2.21`、coroutines `1.9.0` ほか 7 つ。KGP の適用は無くなった。独自の Gradle のテンプレートや EDM4U を使う利用者は、依存がぶつかることがある）
  - 32 ビットのビルドが止まること（J-5）、Mono が使えないこと
  - Notification: 1.x が保存した予約は起動時に消える（J-N19）、1.x が出した通知は 2.0.0 にした後は反応しない、1.12.0 の手順で足した `<service>` の行を消す（S1-8）
- **機能ごとの節**: 各機能のページ（dialog、share、clipboard、notification）に「Changes in `1.13.0`」を置き、共通の節へのリンクと、その機能の既知の差分・消したビルダー（share、clipboard）を書く（版 `1.12.0` の前例: `dialog.md:831`、`clipboard.md:2208`、`notification.md:908`）
- 3 言語（英・日・韓）

**`Documentation~`**: `index{,.ja,.ko}.md` の旧名、Android の Dialog の例の旧い `using`（`index.md:137-147` ほか）、「Configure Gradle Project」（上）。**API の docs**（`build-docs.sh` が出す `Documentation~/site`）は gitignore されていて、`docs/<版>` には写さない（`publish_docs.sh` が写すのはマニュアルだけ。`:44-57`）。`build-docs.sh` は確かめに 1 度流す（`docfx.json` の `allowCompilationErrors: true` のため、生成された `api/` に新しい 37 型があり、旧名が無いことを別に見る）。

**README**（英・日・韓）の機能と動作環境。

**リリースの手順の直し**（`agent-rules/workflows/release/workflow.md`。リリースより前に要るので S4-4 で入れる）:
- ステップ 3 のチェックリストに Android の AAR の行を足す（6 章）
- ステップ 4 のリリースノートに「### Breaking Changes」の節を足す。**出す条件**（S2-6）: `manual/<version>/` のどれかに「Changes in `<version>`」の節があるとき、またはコミットの件名に `!` があるとき（Conventional Commits の破壊的変更の印）。そのときは必須で、中身は移行の節の要約とリンク。S4-2 と S4-3 のコミットの件名には `!` を付ける（例: `refactor(android)!: ...`）

`package.json` の版は S4-5 だけで上げる（S2-11）。`description` / `keywords` は S4-4 で見直す。

## 5. 検証

| # | 何を | どうやって | いつ |
|---|---|---|---|
| V-S4-1 | 旧 2 本を外した後、1.x のクラスが APK に入っていないこと。2.0.0 の AAR だけで全テストが通ること | `verify_unity_android.sh`（Android™ Profile と `--as-package-user`）。APK は、スクリプトに足す `--pull-apk <dir>` で、各実機の実行の後（アンインストールの前）に `adb shell pm path com.UnityTestRunner.UnityTestRunner` と `adb pull` で取る（X2-3。Android™ Profile は Gradle のプロジェクトを書き出す設定〈`m_ExportAsGoogleAndroidProject: 1`〉なので、`BuildPlayer` で APK を作るより、テストで入れた Player を取る方が確か）。`classes*.dex` を Unity 同梱の `PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/dexdump` で読み、`Class descriptor` の行の `Landroid/unity/` と `Landroid/library/` が 0 件。merged manifest は同梱の `SDK/cmdline-tools/16.0/bin/apkanalyzer manifest print`（`JAVA_HOME` に同梱の `OpenJDK`）で、旧い component が 0 件（S2-4）。APK の文字列全体は見ない（1.x のアクション ID の定数が当たる）。**陽性の対照**: S4-1 の前（今の HEAD）の APK で、同じコマンドが 0 より多いことを 1 度確かめる | S4-1 |
| V-S4-1b | `--as-package-user` の実行が、コミットした `Plugins/Android` だけで通ること | スクリプトの後に `git status --porcelain -- Packages/com.jonghyunkim.nativetoolkit/Plugins/Android` が空（`PreBuildProcessor` が何も消さず、何も写さなかった）。Unity のログに `Deleted AAR not in the pin`（`PreBuildProcessor.cs:447`）が無い。`git ls-files Packages/com.jonghyunkim.nativetoolkit/Plugins/Android` が `VERSION.txt`、AAR 2 本と、それぞれの `.meta` の 6 つ（S2-3、X2-2。2 つの実行は同じ起動の中で順に走るので、後の実行だけでは `git rm` の忘れに気づけない） | S4-1 |
| V-S4-2 | 改名の後のコンパイル | Editor（EditMode と PlayMode）と Android の Player のビルド（`UNITY_ANDROID && !UNITY_EDITOR` の中はここでしかコンパイルされない）。ほかの build target は要らない: 改名・削除した型を Android 以外のコードは参照していない（`grep -w`。当たるのはコメント 3 か所。2.2） | S4-2、S4-3 |
| V-S4-3 | `Plugins/Android` の AAR がちょうど 2 本 | `ls Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/*.aar`（androidlib のフォルダは git で管理しないローカルのもので、数えない） | S4-1 |
| V-S4-4 | `[MovedFrom]` の効く範囲（2.3） | 使い捨てのプロジェクト（作業ツリーの外）で、このパッケージを `file:` で参照する。`1.12.0` の書き方のスクリプト（`using JonghyunKim.NativeToolkit.Runtime.Clipboard;` と `ClipboardReadResult`、`AndroidClipboardManager` など）を置き、batchmode（`-accept-apiupdate`。要確認）で開く。合否を分けて記録する: (a) 名前空間の部分が直るか、(b) クラス名の部分が直るか、(c) 旧名で `[SerializeReference]` に保存した Payload が改名の後に同じ値で読めるか、(d) enum に付けられるか。`Assets/` の外のファイルが変わらないこと（S2-13） | S4-3 の前 |
| V-S4-5 | 3.4 の検査 | 3.4 の表 | S4-3、S4-4 |
| V-S4-6 | マニュアル | `verify_manual.sh 1.13.0` の停止項目 0 件 | S4-4 |

各段の実装の後に、実装のレビュー（サブエージェントと Codex）を回す。S4-1〜S4-3 は機械的な変更が多いので、1 回にまとめてもよい。

## 6. リリース（S4-5）

`agent-rules/workflows/release/workflow.md` に従う。**前提**: native-toolkit の版 `1.13.0` がタグになっていること。S4-1〜S4-4 を先に進め、この段だけをタグまで待つ（利用者の判断、2026-10-13）。

- **チェックリストの Android の行**（利用者の判断、2026-10-13。S2-5）: `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/VERSION.txt` の `dist_version` を `<dv>`、`aars` の 2 本を `<aar>` として:
  - 同梱の `Plugins/Android/<aar>` の md5 が、`git -C ../native-toolkit show <dv>:dist/<dv>/android/<aar>` の md5 と一致する
  - **最後の通しの検証で使った native-toolkit のコミット**を `<verified>` として、`git -C ../native-toolkit diff --quiet <verified> <dv> -- dist/<dv>/android` が 0 で終わる（AAR の md5 が合っても、依存の `.module` だけが変わったときに気づくため。`AndroidGradleDependenciesTests` は native-toolkit の作業ツリーの dist を読む）。`<verified>` は、通しの検証の記録（実装結果）に書いておく
  - タグが無ければ ❌
  - Android の `VERSION.txt` には Windows の `source:` に当たる行を足さない（dist の版とタグの名前が同じ）
- **合わなかったとき**: AAR を差し替え、`AndroidGradleDependencies` の版を `m2/` の `.module` と照らし（J-2 のテスト）、通しの検証をやり直す
- `package.json` の版を `1.13.0` にする（この段だけ）

## 7. 順とコミット

1. V-S4-4（`[MovedFrom]` の確かめ。作業ツリーの外）。V-S4-1 の陽性の対照（今の HEAD の APK）
2. S4-1（AAR、ピン、`VERSION.txt`、テストの 1.x の分岐、`common.md:45`、`verify_unity_android.sh` の `--pull-apk`）。V-S4-1、V-S4-1b、V-S4-3。1 コミット
3. S4-2（ビルダー、`AndroidNotificationJsonBuilder` の XML コメント、ほかの OS のコメント 2 か所、`testing.md:405-406`、`common.md:518`）。通しの検証。1 コミット（件名に `!`）
4. S4-3（改名、`[MovedFrom]`、参照の置き換え、`IosClipboardOperationResult.cs` のコメント、agent-rules、artifact の README）。V-S4-2、V-S4-5（S4-3 の行）。1 コミット（件名に `!`。参照の置き換えを分けると、途中のコミットがコンパイルできない）
5. 実装のレビュー（S4-1〜S4-3）
6. S4-4（4 章の順）。V-S4-5（S4-4 の行）、V-S4-6
7. S4-5（リリース）。native-toolkit の版 `1.13.0` のタグの後
