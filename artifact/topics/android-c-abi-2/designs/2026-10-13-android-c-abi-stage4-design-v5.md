# Android の C ABI 2.0.0 への移行の段 4（旧ブリッジの撤去、改名、文書、リリース）v5

## 基本情報

| 項目 | 内容 |
|---|---|
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（5 章の段 4、4 章の J-10〜J-12） |
| 改名の課題 | `artifact/topics/os-prefix-violations/README.md`（案 1。範囲を Android に絞った。2 章） |
| レビュー | `artifact/topics/android-c-abi-2/reviews/2026-10-13-android-c-abi-stage4-design-review-v1.md`（v2 を見た）、`...-review-v2.md`（v3）、`...-review-v3.md`（v4。サブエージェントの S3-*、Codex の X3-*） |
| 前提 | 段 3 が終わった（4 機能がすべて C ABI を呼ぶ。`cb428dd`）。1.x のブリッジのクラスを呼ぶ実行時のコードは無い（`android.unity.` の文字列は、1.x のアクション ID を保つ定数〈`AndroidNotificationManager.cs:63,68`、`AndroidNotificationCApi.cs:57,60`〉に残るが、クラスの参照ではない） |
| 出す版 | 版 `1.13.0`（メジャーにしない。名前空間の移動、改名、ビルダーの削除は破壊的変更として書く） |
| native-toolkit | dist `1.13.0` の `android/` に AAR 2 本（`android-native-toolkit-2.0.0.aar` の md5 9e068bc9…、`android-native-toolkit-capi-2.0.0.aar` の md5 c706a8a6…。同梱のものと一致）。作業ツリーは clean で、dist は git で管理されている（LFS ではない）。**native-toolkit の版 `1.13.0` のタグはまだ無い**（2026-10-13。最新のタグは版 `1.12.0`、HEAD は `a7684dee`） |
| push | **このリポジトリでは、エージェントは push しない**（利用者の指示。2026-10-11 からの約束で、agent-rules には書かれていない。`agent-rules/workflows/write-manual/workflow.md:17` の「コピー直後にコミット・プッシュ」の push は、利用者が行う。X3-10、S3-14） |

## v4 からの主な変更（v5。レビュー v3 を受けて。2026-10-13）

- **`[MovedFrom]` は enum を含む 42 型すべてに付ける**（X3-1。A1）: enum に付けられることは、Unity 6000.4.2f1 の `UnityEngine.CoreModule.dll` で確かめられた（`AttributeUsage` は Class・Struct・Enum・Interface・Delegate。S3-2）。v4 の「付けられなければ付けない」を消した
- **移行の中身は各機能のページに置く**（X3-7。A1）: 各機能のページの「Changes in `1.13.0`」に、その機能の旧い名前空間、旧名 → 新名、`[MovedFrom]` で直るもの、手で直すもの、既知の差分を置く。index には共通の項目と、4 ページへのリンクだけを置く（4.2）
- **API Updater は、改名した 37 型を直さない**（S3-2）: `MovedFromAttribute.AffectsAPIUpdater` は、`sourceClassName` を渡すと false を返す。移行の節は「37 型は `using` も名前も手で直す」を既定にして書き、V-S4-4 の結果で「自動で直るもの」を足す（2.3）
- 旧い名前空間の検査を、`UNITY_ANDROID` を含むコードブロックで判定する形にした（S3-1。v4 の決まりは、`1.12.0` の文書の 45 個のうち 3 個しか拾わなかった）。`[MovedFrom]` の 42 型の引数を照らす検査を足した（X3-2）
- V-S4-4 を S4-3 の後に移した（X3-3、S3-3）。S4-3 の後に通しの検証を足した（S3-4）。V-S4-1b は実行の前後の差で見る（X3-5、S3-6）
- `--pull-apk=<dir>` の形と、APK から dex を取り出す手順（X3-4、S3-7）。リリースのチェックリストの行のパスと、`<verified>` の置き場所（X3-6、S3-5）
- 文書の順（`publish_docs.sh` が `verify_manual.sh` より前。X3-8、S3-12）、後半のコミット（X3-9）、言語ごとの見出し（S3-9）、Android の動作環境の節の置き場所（S3-8）、「Configure Gradle Project」の手順の残り（S3-10）、設計の文書の先のコミット（S3-15）、細部（S3-11、S3-13、S3-16）

## 1. 何をするか

| # | 中身 | 破壊的変更 |
|---|---|---|
| S4-0 | 段 4 の設計とレビュー、README の J-12 をコミットする。検証のスクリプトに `--pull-apk` を足す | なし |
| S4-1 | 旧ブリッジの AAR を外す。層 2b のテストの 1.x の分岐を消す | なし（公開 API は変わらない） |
| S4-2 | 使われていない JSON ビルダーを消す（J-12） | あり（2 型） |
| S4-3 | Android の接頭辞の無い public 型 37 を改名し、42 型に `[MovedFrom]` を付ける | あり（37 型の名前。42 型の名前空間は段 3-0 で動いた） |
| S4-4 | マニュアル（版 `1.13.0`、英・日・韓）と移行の節、README、`Documentation~`、リリースとコミットの手順の直し | なし |
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

**名前を残し、名前空間だけが動いた 5 型**（段 3-0）: `AndroidDialogManager`（`Runtime.Dialog` → `Runtime.Android.Dialog`）、`AndroidShareManager`（`Runtime.Share` → `Runtime.Android.Share`）、`AndroidClipboardManager`（`Runtime.Clipboard` → `Runtime.Android.Clipboard`）、`AndroidNotificationManager` と `AndroidNotificationJsonBuilder`（`Runtime.Notification` → `Runtime.Android.Notification`）。

- `Runtime/Android/` の public 型は 44（`Android` で始まらないもの 37、始まるもの 7。7 のうち 2 は 3.3 で消す）。タグ `1.12.0` では、44 型すべてが `Runtime.<Feature>` にあり、アセンブリはどちらも `NativeToolkit.Runtime`（レビュー v3 で 2 人が確かめた）。入れ子の public の型は無い。新しい名前は、パッケージと `Assets/` のどの型ともぶつからない
- ファイルの改名は `git mv` で `.cs` と `.cs.meta` を対で行い、GUID を保つ
- **範囲の外**（利用者の判断、2026-10-13）: `IconConfiguration`、iOS の 5 型（`NotificationContentPayload` ほか）、macOS の 3 型（`DialogButton` ほか）の計 9。`agent-rules/coding-rules/common.md` の「既知の逸脱」の表（今は Android の 3 行と `IconConfiguration`。`:455-462`）は、Android の 3 行を消し、iOS の 5 型と macOS の 3 型の行を足す。見出しの「Runtime 11 件」を 9 件に直す。`os-prefix-violations/README.md:112` の「公開 81 型・13%」も、改名の後の数に直す

### 2.2 参照の置き換え

置き換えは下の「置き換える」パスだけに当てる（repo 全体にはかけない）。

| 置き換える | 中身 | 段 |
|---|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/`、`Packages/com.jonghyunkim.nativetoolkit/Tests/` | Android の Manager、Bridge、サンプル（`Runtime/UI/Android/`）、層 1・2a・2b（2b の 6 ファイルに旧名が 158 か所。`#if UNITY_ANDROID` の中なので、確かめは実機の実行。5 章の V-S4-2） | S4-3 |
| ほかの OS のファイルの 3 か所（下） | コメントだけ | S4-2、S4-3 |
| `agent-rules/coding-rules/common.md`（「既知の逸脱」の表。2.1） | 規則 | S4-3 |
| `agent-rules/workflows/review-document/workflow.md:46`、`review-implementation-feature/workflow.md:67`、`review-implementation-sample-scene/workflow.md:62` | 「既知の逸脱 11 件」と、それを前例に引く記述を、残りの 9 件に直す | S4-3 |
| `agent-rules/workflows/design-feature/workflow.md:54,57` | 「Android / iOS / macOS は `Runtime/<Feature>/`（未移行の形）」を、Android を移行済みに入れる形に直す（`:52` の `Common/` の説明は正しいので変えない。S3-11） | S4-3 |
| `artifact/topics/android-c-abi-2/README.md`（5 章の段 4 の「11 型」）、`artifact/topics/os-prefix-violations/README.md`、`artifact/README.md:59,65` | 範囲と状況。**手で直す**（`os-prefix-violations/README.md` の旧名の多くは経緯の記録で、機械的に置き換えない） | S4-3 |
| `manual/1.13.0/`、`Packages/com.jonghyunkim.nativetoolkit/Documentation~/index{,.ja,.ko}.md`、`README{,.ja,.ko}.md` | 旧名、旧い名前空間、旧い手順（4 章） | S4-4 |

| 置き換えない | 理由 |
|---|---|
| `artifact/` の過去の設計・計画・結果・レビュー | 過去の記録 |
| `manual/` と `docs/` の版 `1.12.0` 以前 | 出した版の文書 |
| `Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/*.xcframework` の `.swiftinterface`、`.abi.json` | Swift の同じ名前の型。置き換えると同梱のフレームワークの中身まで変わる |

**ほかの OS のファイル**: 振る舞いは変えず、コメントだけを直す（`common.md` の P2「ほかの OS のファイルを変えない」の例外。存在しない型を指したままにしないため）。

| ファイル | 今 | 直した後 | 段 |
|---|---|---|---|
| `Runtime/Clipboard/IosClipboardOperationResult.cs:15` | `ClipboardOperationResult`（`<c>`） | `AndroidClipboardOperationResult` | S4-3 |
| `Runtime/Share/IosShareJsonBuilder.cs:12` | `AndroidShareJsonBuilder`（`<c>`） | 「1.x の Android の builder」（型は消えるので、名前を出さない） | S4-2 |
| `Runtime/Windows/Clipboard/WindowsClipboardJsonBuilder.cs:18` | `AndroidClipboardJsonBuilder`（地の文） | 同上 | S4-2 |

### 2.3 `[MovedFrom]`（利用者の判断、2026-10-13）

- **enum を含む 42 型すべてに付ける**（X3-1）。改名した 37 型は `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>", sourceClassName: "<旧名>")]`、名前を残した 5 型は `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>")]`（`sourceClassName` を渡さない）。`<Feature>` はタグ `1.12.0` の名前空間（2.1 の表）。`sourceAssembly` は渡さない（アセンブリ名は変わっていない）。属性は 1 行で書く（3.4 の検査が行で見るため）
- **Unity の仕様**（Unity 6000.4.2f1 の `UnityEngine.CoreModule.dll` を反射で読んだ。レビュー v3 の S3-2、X3-1）:
  - コンストラクタは `(bool autoUpdateAPI, string sourceNamespace = null, string sourceAssembly = null, string sourceClassName = null)`。`AttributeUsage` は Class・Struct・Enum・Interface・Delegate
  - `AffectsAPIUpdater` は、`sourceClassName` を渡すと false、`sourceNamespace` だけなら true。**API Updater が利用者のスクリプトを直す見込みがあるのは、名前を残した 5 型の名前空間だけ**。改名した 37 型は、名前空間の部分も含めて直らない見込み
  - 属性は、シリアライズしたデータの移行にも使われる。`[Serializable]` の Payload（`AndroidClipboardPayloads.cs:9-11` ほか）を旧名で `[SerializeReference]` に保存したデータに効くかを確かめる
- **移行の節の既定**: 「37 型は `using` と型の名前を手で直す（旧名 → 新名の表）」「5 型は `using` を手で直す」。V-S4-4 で API Updater が直すと確かめられたものだけ、「自動で直る」と書き足す。利用者のコードの多くは `#if UNITY_ANDROID && !UNITY_EDITOR` の中にあり（マニュアルの勧め。`dialog.md:37-39` ほか）、Editor のコンパイルに入らないので、API Updater が見ない見込み（V-S4-4 で確かめる）

## 3. 削除（S4-1、S4-2）と検査

### 3.1 S4-1: 旧ブリッジの AAR

- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/` の `android-native-toolkit-1.3.0.aar`、`unity-android-native-toolkit-1.3.0.aar` と、その `.meta` を `git rm` する
- `Plugins/Android/VERSION.txt` の `extra_dist_version`、`extra_aars` の行と、その説明のコメント（`:15-16`、`:22-24`、`:30-32`）を、Windows の `VERSION.txt` と同じく過去形の注記に直す
- `Editor/Build/PreBuildProcessor.cs` の `AndroidPin` から、追加の組を消す: プロパティとコンストラクタ（`:197-207`）、`<summary>` の「or when only one of the two extra_* keys is given」（`:210-212`）、読む処理（`:220-229`）と組み立て（`:231-235`）、写す処理、ログの追加の部分（`:416`）。クラスの XML コメント（`:186-188`）を Windows（`:100-105`）と同じ形に直す（S3-16）
  - 残った `extra_*` のキーは、どのコードも読まないので無視される。ピンのファイルを読む `ReadPinValues`（`:136-160`）は知らないキーを無視する（`AndroidPin` は define の要るアセンブリの private の型で、EditMode のテストの仕組みが無い）。確かめは、`grep -n extra_ Editor/Build/PreBuildProcessor.cs` が、Windows の過去形の注記（`:104`）のほかに当たらないこと
- 旧 2 本の manifest、クラス、リソース、`proguard.txt`（空）は AAR の中にあり、AAR を消せば消える。1.x の AAR のクラスは `android/library`（245）と `android/unity`（60）だけで、2.0.0 のクラスは `com/jonghyunkim` だけ
- 2.0.0 の AAR の `LegacyScheduleCleaner` は 2.0.0 の側にあるので残る（移行の節。Notification の J-N19）
- `agent-rules/coding-rules/common.md:45` の「旧ブリッジの AAR は段 4 で外す」を過去形にする

### 3.2 S4-1: 層 2b のテストの 1.x の分岐

- 6 ファイルの `OnCApi` の分岐（56 か所）から、1.x の側を消し、C ABI の期待だけにする。`OnCApi` の判定は、各 fixture の `SetUp` の `Assert.IsTrue(OnCApi, "...")` に置き換える（継ぎ目が消えたり名前が変わったりしたら、黙って通るのではなく落ちる）。`NTK-*-RECORD` のログは消す
- 1.x の記録は、各機能の UI テスト計画と設計の 1.x の列に残っている。Share の実装結果 v2 の 7 章の約束を果たす

### 3.3 S4-2: JSON ビルダー（J-12）

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareJsonBuilder.cs`、`Runtime/Android/Clipboard/AndroidClipboardJsonBuilder.cs`、`Tests/Runtime/AndroidShareJsonBuilderTests.cs`、`Tests/Runtime/AndroidClipboardJsonBuilderTests.cs` を、それぞれの `.cs.meta` と対で `git rm` する
- 消した型に触れる Android のコメント（`AndroidClipboardCApi.cs:103`）と、ほかの OS のファイルの 2 か所（2.2）を直す
- `agent-rules/coding-rules/testing.md:405-406` の Android の Share・Clipboard から「builder」を消す。EditMode のテストは 37 本減る（Share 21、Clipboard 16）
- `agent-rules/coding-rules/common.md:518` の「`XxxJsonBuilder` クラスで JSON 組み立てを担い、Manager から呼び出す」に、Android の例外を書く（Share・Clipboard の Manager は Payload を C ABI に渡し、ビルダーを持たない。`AndroidNotificationJsonBuilder` は利用者が Manager に渡す JSON を組む道具で、Manager は呼ばない）
- **残す**: `AndroidNotificationJsonBuilder`（J-12）。未コミットの XML コメントの直し（2026-10-13）は、このコミットに入れる

### 3.4 検査（段ごと）

| 段 | パス | 0 件にするもの |
|---|---|---|
| S4-3 の後 | `Packages/com.jonghyunkim.nativetoolkit/Runtime`、`Packages/com.jonghyunkim.nativetoolkit/Tests`、`agent-rules` | 37 の旧名（単語の境界）、消した 2 ビルダーの名前。`[MovedFrom]` の行は除く |
| S4-3 の後 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android` | **`[MovedFrom]` の照合**（X3-2）: 消した後の public 型がちょうど 42。各型に `[MovedFrom]` がちょうど 1 つ。`sourceNamespace` が 2.1 の表のタグ `1.12.0` の名前空間と一致。改名した 37 型の `sourceClassName` が旧名と一致。名前を残した 5 型に `sourceClassName` が無い。2.1 の表を写した小さな Python で照らす |
| S4-4 の最後のコミット | `manual/1.13.0`、`Packages/com.jonghyunkim.nativetoolkit/Documentation~/index{,.ja,.ko}.md`、`README{,.ja,.ko}.md` | 37 の旧名、消した 2 ビルダーの名前、旧い手順（`Config\w* Gradle Project`、`ConfigureGradleProject`、`Add Kotlin Dependencies`。日本語版の綴りの揺れを拾う）、1.x のクラス名（`android\.(library\|unity)\.[a-z]+\.[A-Za-z.]*[A-Z]`）、旧い名前空間の組（下） |

- **許す行**: `manual/1.13.0/*.md` の移行の節の中（旧名 → 新名の表、消したビルダーの説明、旧い名前空間の説明）。移行の節の見出しは言語ごとに決める（版 `1.12.0` の前例: 英 `### Changes in 1.12.0`、日 `### 1.12.0 での変更`、韓 `### 1.12.0 변경 사항`。版 `1.13.0` も同じ形。S3-9）。検査はこの 3 つの見出しから次の同じ段の見出しまでを除く
- **旧い名前空間の組**（S3-1）: コードのブロック（コードフェンスの間）ごとに、`UNITY_ANDROID` を含み、かつ `using JonghyunKim.NativeToolkit.Runtime.(Dialog|Share|Clipboard|Notification);` を含むものを 0 件にする（`1.12.0` の文書では、旧い `using` を持つ Android のブロック 45 個をすべて拾い、iOS・macOS・Windows のブロック 105 個はどれも `UNITY_ANDROID` を含まない。レビュー v3 の S3-1 が確かめた）
- S4-4 の途中のコミット（起点のコピー、ページごとの直し）では 0 件を求めない
- `scripts/verify_manual.sh` の検査 2 は接頭辞の付いた名前しか調べない（`:211`）ので、この検査を代わりにする

## 4. 文書（S4-4）

`agent-rules/workflows/write-manual` と `verify-manual` に従う。

### 4.1 順とコミット

1. **起点**: `manual/1.12.0` を `manual/1.13.0` に写し、すぐにコミットする（`chore(manual): copy manual/1.12.0 to manual/1.13.0 as baseline`。push は利用者が行う。基本情報）
2. index と、各機能のページ（dialog、share、clipboard、notification）を、ページごとに 3 言語をまとめて直し、1 コミットずつ
3. README（3 言語）と `package.json` の `description` / `keywords`。1 コミット
4. `Documentation~/index{,.ja,.ko}.md` と、使われなくなる画像（4.3）。1 コミット
5. リリースとコミットの手順（4.4）。1 コミット
6. 3.4 の検査（S4-4 の行）
7. `scripts/publish_docs.sh 1.13.0`（`docs/1.13.0` と `docs/latest` を書く。`:44-57,86-93`）。そのあと `scripts/verify_manual.sh 1.13.0` の停止項目 0 件（`docs/<版>` が無いと止まるので、この順。X3-8）。`docs/1.13.0` と `docs/latest` を 1 コミット（X3-9）

### 4.2 中身

**index（3 言語）**:
- 「# Supported OS Versions」（日「# 対応 OS バージョン」）の下に「## Android requirements」を新しく作る: ARM64（と x86_64）だけ、IL2CPP だけ（J-10）。Mono は使えない（Unity の Android の Mono は ARMv7 しか組めず、ビルドが止まる。親課題の README の記録 `6.3`、`6.4`）（S3-8）
- その直後に、共通の移行の節（英 `## Changes in 1.13.0 (Android)`、日 `## 1.13.0 での変更（Android）`、韓 `## 1.13.0 변경 사항 (Android)`）を置く。中身は**全機能に共通の項目と、4 ページへのリンクだけ**（X3-7）:
  - Android の型の名前空間が `Runtime.<Feature>` から `Runtime.Android.<Feature>` に、名前空間を持たない型の一部が改名された（要約と、各機能のページへのリンク）
  - 1.x の AAR（`android-native-toolkit-1.3.0.aar`、`unity-android-native-toolkit-1.3.0.aar`）を同梱しなくなった。その中のクラス（`android.unity.*`、`android.library.*`）を直接使っていたコードは動かない
  - 「Configure Gradle Project」（エディタのウィンドウ `AndroidConfigureProjectWindow`）が無くなった。`unityLibrary/build.gradle` に実行時の依存を足すようになった（kotlin-stdlib `2.2.21`、coroutines `1.9.0` ほか 7 つ。KGP の適用は無くなった。独自の Gradle のテンプレートや EDM4U を使う利用者は、依存がぶつかることがある）（S3-10）
  - 32 ビットのビルドが止まる（J-5）、Mono が使えない
- 「Getting Started」の Android サンプルの手順から、「Configure Gradle Project」とそれに続く手順（「Browse」「Open」「Run: Add Kotlin Dependencies」など。`index.md:185-191`、日本語版は「Configue」）を消す。Gradle の依存は `IPostGenerateGradleAndroidProject` が足すので、利用者の手順は要らない（J-1）
- 冒頭の紹介文「Editor windows help integrate native libraries and Gradle/Xcode settings」（`index.md:7`）を事実に合わせる（S3-10）

**各機能のページ（dialog、share、clipboard、notification。3 言語）**: 移行の節（英 `### Changes in 1.13.0`、日 `### 1.13.0 での変更`、韓 `### 1.13.0 변경 사항`。版 `1.12.0` の前例: `dialog.md:831`、`clipboard.md:2208`、`notification.md:908`）に、**その機能の移行の中身をすべて**置く（X3-7）:
- その機能の型の旧い名前空間と新しい名前空間（`using` の直し方の例）
- その機能の改名した型の旧名 → 新名の表（2.1。Dialog は改名する型が無く、`AndroidDialogManager` の名前空間だけ）
- `[MovedFrom]` で直るもの・手で直すもの（2.3 の既定と V-S4-4 の結果）
- その機能の既知の差分（設計 5.3 と、各実装結果の 3.4: Dialog の結果 v4、Share の結果 v2、Clipboard の結果 v3、Notification の結果 v2）
- 消したビルダー（share、clipboard）
- Notification: `tools:keep`（J-N6）、`IsNotificationScheduled` は `persistAcrossBoot` の予約だけを見る、権限が無いときの失敗（J-N7）、1.x が保存した予約は起動時に消える（J-N19）、1.x が出した通知は 2.0.0 にした後は反応しない、1.12.0 の手順で足した `<service>` の行を消す
- index の共通の節へのリンク

**そのほかの直し**:
- 各「Import the namespace」の節を `Runtime.Android.<Feature>` にする
- Notification の manifest の例（`notification.md:619-624`、3 言語）の、1.x のクラス名の `<service>` の行を消す
- 版 `1.12.0` のマニュアルの、版 `1.13.0` で事実でなくなる記述を直す（例: `dialog.md:835`「The Android, iOS and macOS types keep their namespace.」）

**README**（3 言語）: 機能、動作環境、冒頭の紹介文（`README.md:5`）。`package.json` の `description`（同じ紹介文）と `keywords`。版は S4-5 で上げる。

### 4.3 `Documentation~`

- `index{,.ja,.ko}.md`: 旧名、Android の例の旧い `using`（`index.md:137-147` ほか。3.4 の組の検査で拾う）、「Configure Gradle Project」と続く手順（`:95-99`）
- 画像 `Documentation~/images/editor/ConfigureGradleProject.png` は、参照が無くなるので `git rm` する（パッケージに入る、git で管理されたファイル。`manual/1.12.0` の同じ画像は残る。S3-10）
- **API の docs**（`build-docs.sh` が出す `Documentation~/site`）は gitignore されていて、`docs/<版>` には写さない（`publish_docs.sh` が写すのはマニュアルだけ）。`build-docs.sh` は確かめに 1 度流し、生成された `api/` に新しい 37 型があり、旧名が無いことを見る（`docfx.json` の `allowCompilationErrors: true` のため、成功だけでは足りない）

### 4.4 リリースとコミットの手順の直し（リリースより前に要るので S4-4 で入れる）

- `agent-rules/workflows/release/workflow.md`:
  - ステップ 3 のチェックリストに Android の AAR の行を足す（6 章）
  - ステップ 4 のリリースノートに「### Breaking Changes」の節を足す。**出す条件**: 英語の `manual/<version>/*.md` のどれかに「Changes in `<version>`」の見出しがあるとき、またはコミットの件名に `!` があるとき（Conventional Commits の破壊的変更の印）。そのときは必須。中身は移行の節の要約と、各機能のページへのリンク
- `agent-rules/workflows/commit-msg/workflow.md:23` の件名の形に、破壊的変更の `!`（`<type>(<scope>)!: <subject>`）を足す（S3-13）。S4-2 と S4-3 のコミットの件名には `!` を付ける

## 5. 検証

| # | 何を | どうやって | いつ |
|---|---|---|---|
| V-S4-1 | 旧 2 本を外した後、1.x のクラスが APK に入っていないこと。2.0.0 の AAR だけで全テストが通ること | `verify_unity_android.sh --as-package-user --pull-apk=<dir>`。`--pull-apk` は S4-0 で足す（値は `=` で渡す。今の引数の読み方は値を取る引数を受けない。`:83-90`）。各実機の実行の後、次の実行の頭のアンインストールの前に、`adb shell pm path com.UnityTestRunner.UnityTestRunner` の全行から `package:` を取り、すべての APK を `adb pull` する。各 APK を展開し、`classes*.dex` をすべて Unity 同梱の `PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/dexdump` に渡し、`Class descriptor` の行の `Landroid/unity/` と `Landroid/library/` が 0 件。base の APK の merged manifest を同梱の `SDK/cmdline-tools/16.0/bin/apkanalyzer manifest print`（`JAVA_HOME` に同梱の `OpenJDK`）で読み、`android\.(unity\|library)\.` が 0 件。pull の失敗、APK が 0 本、dex が 0 本は失敗（X3-4、S3-7）。**陽性の対照**: S4-0 の後、S4-1 の前（1.3.0 の 2 本がある状態）で同じ手順が 0 より多いこと（AAR の展開で、1.x の manifest は `android.unity` などが 5 件と 1 件、2.0.0 の 2 本は 0 件と確かめてある） | S4-0 の後、S4-1 |
| V-S4-1b | `--as-package-user` の実行が、コミットする `Plugins/Android` だけで通ること | 実行の前の `git status --porcelain -- Packages/com.jonghyunkim.nativetoolkit/Plugins/Android` を控え、実行の後と同じであること（`PreBuildProcessor` が何も消さず、何も写さなかった。S4-1 の `git rm` と `VERSION.txt` の変更は前後どちらにも出る。X3-5、S3-6）。Android™ Profile の実行のログ（`$OUT_DIR/Android.log`、`$OUT_DIR/Android-retention.log`。build steps が動くのはこの 2 つだけ）に `Deleted AAR not in the pin`（`PreBuildProcessor.cs:447`）が無い。コミットの後に `git ls-files Packages/com.jonghyunkim.nativetoolkit/Plugins/Android` が `VERSION.txt`、AAR 2 本と、それぞれの `.meta` の 6 つ | S4-1 |
| V-S4-2 | 改名と削除の後のコンパイル | `verify_unity_android.sh --as-package-user`（Editor の EditMode・PlayMode と、実機の実行。層 2b のテストのアセンブリと `UNITY_ANDROID && !UNITY_EDITOR` の中は、実機の実行でしかコンパイルされない。S3-4）。ほかの build target は要らない: 改名・削除した型を Android 以外のコードは参照していない（コメント 3 か所だけ。2.2） | S4-2、S4-3 |
| V-S4-3 | `Plugins/Android` の AAR がちょうど 2 本 | `ls Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/*.aar`（androidlib のフォルダは git で管理しないローカルのもので、数えない） | S4-1 |
| V-S4-4 | `[MovedFrom]` の効く範囲（2.3） | **S4-3 の実装の後、S4-4 の前**（X3-3、S3-3）。(1) 作業ツリーの外に使い捨てのプロジェクトを作り、`git archive 1.12.0 Packages/com.jonghyunkim.nativetoolkit` を `file:` で参照する。`1.12.0` の書き方のスクリプトを 2 本置く（ガード無し、マニュアルのガード付き〈`#if UNITY_ANDROID && !UNITY_EDITOR`〉。例: `using JonghyunKim.NativeToolkit.Runtime.Clipboard;` と `ClipboardReadResult`、`AndroidClipboardManager`）。旧名の Payload を `[SerializeReference]` で持つアセットを保存する。(2) manifest を、S4-3 のパッケージのコピー（作業ツリーの外）に向けて開き直す（batchmode。`-accept-apiupdate` は要確認）。記録すること: (a) 5 型の名前空間が直るか、(b) 37 型が直らないこと（2.3 の見込み）、(c) ガード付きのスクリプトが直るか、(d) アセットの Payload が同じ値で読めるか、(e) パッケージのコピーの外のファイルが変わらないか | S4-3 の後 |
| V-S4-5 | 3.4 の検査 | 3.4 の表 | S4-3、S4-4 |
| V-S4-6 | マニュアル | 4.1 の 7 | S4-4 |

各段の実装の後に、実装のレビュー（サブエージェントと Codex）を回す。S4-1〜S4-3 は機械的な変更が多いので、1 回にまとめてもよい。

## 6. リリース（S4-5）

`agent-rules/workflows/release/workflow.md` に従う。**前提**: native-toolkit の版 `1.13.0` がタグになっていること。S4-1〜S4-4 を先に進め、この段だけをタグまで待つ（利用者の判断、2026-10-13）。

- **`<verified>` の置き場所**（S3-5）: 最後の通しの検証（7 章のステップ 5）のときの native-toolkit のコミットを、`Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/VERSION.txt` に新しいキー `verified_native_toolkit_commit:` で書く（`ReadPinValues` は知らないキーを無視する）。その検証のときに `git -C ../native-toolkit status --porcelain -- dist/1.13.0/android` が空だったことを、実装結果に書く。Android を触らない後のリリースでも、`dist_version` が同じならこの値のまま使える
- **チェックリストの Android の行**（X3-6）: `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/VERSION.txt` の `dist_version` を `<dv>`、`aars` の 2 本を `<aar>`、`verified_native_toolkit_commit` を `<verified>` として:
  - `git -C ../native-toolkit rev-parse -q --verify refs/tags/<dv>` が成功する（タグがある）。無ければ ❌
  - 同梱の `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/<aar>` の md5 が、`git -C ../native-toolkit show <dv>:dist/<dv>/android/<aar>` の md5 と一致する
  - `git -C ../native-toolkit diff --quiet <verified> <dv> -- dist/<dv>/android` が 0 で終わる（AAR の md5 が合っても、依存の `.module` だけが変わったときに気づくため）
  - Android の `VERSION.txt` には Windows の `source:` に当たる行を足さない（dist の版とタグの名前が同じ）
- **合わなかったとき**: AAR を差し替え、`AndroidGradleDependencies` の版を `m2/` の `.module` と照らし（J-2 のテスト）、通しの検証をやり直し、`verified_native_toolkit_commit` を書き直す
- `package.json` の版を `1.13.0` にする（この段だけ）

## 7. 順とコミット

0. **S4-0**: 段 4 の設計 v1〜v5、レビュー v1〜v3、README の J-12、`AndroidNotificationJsonBuilder` の XML コメントの直しは除く（S4-2 に入れる）を `docs(artifact)` で 1 コミット（S3-15）。`verify_unity_android.sh` の `--pull-apk` を `test(android)` で 1 コミット。V-S4-1 の陽性の対照
1. **S4-1**（AAR、ピン、`VERSION.txt`、`PreBuildProcessor`、テストの 1.x の分岐、`common.md:45`）。V-S4-1、V-S4-1b、V-S4-3。1 コミット
2. **S4-2**（ビルダー、`AndroidNotificationJsonBuilder` の XML コメント、ほかの OS のコメント 2 か所、`testing.md:405-406`、`common.md:518`）。V-S4-2。1 コミット（件名に `!`）
3. **S4-3**（改名、`[MovedFrom]`、参照の置き換え、`IosClipboardOperationResult.cs` のコメント、agent-rules、artifact の README）。V-S4-5（S4-3 の 2 行）。1 コミット（件名に `!`。参照の置き換えを分けると、途中のコミットがコンパイルできない）
4. **V-S4-4**（作業ツリーの外）
5. **最後の通しの検証**: `verify_unity_android.sh --as-package-user`（V-S4-2）。native-toolkit のコミットを `verified_native_toolkit_commit` に書いて 1 コミット（6 章）
6. **実装のレビュー**（S4-1〜S4-3）
7. **S4-4**（4.1 の順）。V-S4-5（S4-4 の行）、V-S4-6
8. **S4-5**（リリース）。native-toolkit の版 `1.13.0` のタグの後

コミットはどれも、利用者に確かめてから行う。
