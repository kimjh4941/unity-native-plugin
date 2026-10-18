# Android の C ABI 2.0.0 への移行の段 4（旧ブリッジの撤去、改名、文書、リリース）v6

## 基本情報

| 項目 | 内容 |
|---|---|
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（5 章の段 4、4 章の J-10〜J-12） |
| 改名の課題 | `artifact/topics/os-prefix-violations/README.md`（案 1。範囲を Android に絞った。2 章） |
| レビュー | `artifact/topics/android-c-abi-2/reviews/2026-10-13-android-c-abi-stage4-design-review-v1.md`（v2 を見た）、`...-review-v2.md`（v3）、`...-review-v3.md`（v4）、`...-review-v4.md`（v5。サブエージェントの S4-*、Codex の X4-*） |
| 前提 | 段 3 が終わった（4 機能がすべて C ABI を呼ぶ。`cb428dd`）。1.x のブリッジのクラスを呼ぶ実行時のコードは無い（`android.unity.` の文字列は、1.x のアクション ID を保つ定数〈`AndroidNotificationManager.cs:63,68`、`AndroidNotificationCApi.cs:57,60`〉に残るが、クラスの参照ではない） |
| 出す版 | 版 `1.13.0`（メジャーにしない。名前空間の移動、改名、ビルダーの削除は破壊的変更として書く） |
| native-toolkit | dist `1.13.0` の `android/` に AAR 2 本（`android-native-toolkit-2.0.0.aar` の md5 9e068bc9…、`android-native-toolkit-capi-2.0.0.aar` の md5 c706a8a6…。同梱のものと一致）。作業ツリーは clean で、dist は git で管理されている（LFS ではない）。**native-toolkit の版 `1.13.0` のタグはまだ無い**（2026-10-13。最新のタグは版 `1.12.0`、HEAD は `a7684dee`） |
| push | **このリポジトリでは、エージェントは push しない**（利用者の指示。2026-10-11 からの約束で、agent-rules には書かれていない）。手順に push があるところ（`agent-rules/workflows/write-manual/workflow.md:17`、`release/workflow.md` のステップ 5〈`:103`、`:126` の `gh pr create` はブランチの push が前提〉、`:133`、`:145`）では、エージェントはその手前で止まり、利用者に push を頼む（X3-10、S3-14、S4-4） |

## v5 からの主な変更（v6。レビュー v4 を受けて。2026-10-13）

- **全機能に共通の破壊的変更も、各機能のページの移行の節に書く**（X4-8。A1）: 1.x の AAR を同梱しないこと、「Configure Gradle Project」が無くなったこと、32 ビットのビルドが止まること、Mono が使えないこと。index には動作環境と、各機能のページへの案内だけを置く。リリースノートの Breaking Changes は、各機能のページの節へ直接リンクする（4.2）
- **V-S4-4 を 3 段に分けた**（S4-2、X4-4、X4-5）: Updater の差分（5 型だけ、37 型だけ、ガード付きのスクリプトを別にする）→ エラーが残るスクリプトと旧名を参照するスクリプトを消す → 旧名を参照しない Editor のスクリプトでアセットを読み直す。旧版は `git archive … | tar -x` で作業ツリーの外に展開する。(e) は「リポジトリの作業ツリーとパッケージのコピーが変わらない」にした
- **2.3 の根拠を直した**（S4-3）: `AffectsAPIUpdater` は Updater から使われていない。Updater の本体の文字列からの見込みに置き換え、「直らない」は V-S4-4 で確かめる見込みとして揃えた
- 3.4 の許す節は、段を問わず「`1.13.0` の移行の見出しから、同じ段以上の次の見出しまで」にした（S4-1）。1.x のクラス名の正規表現を表の外に出し、`|` のエスケープをやめ、アクション ID の値を除いた（X4-3、S4-14）。`[MovedFrom]` の照合に第 1 引数と `sourceAssembly` を足した（X4-2）。属性のファイルに `using UnityEngine.Scripting.APIUpdating;` を足す（X4-1）
- 7 章の順: 各段のコミットの前に検証する。実装のレビューを最後の通しの検証より前にし、`verified_native_toolkit_commit` はレビューの後に書く（S4-6、X4-7、X4-9）。リリースとコミットの手順の直しを S4-0 に移した（S4-7）
- `--pull-apk` の出力先は、空か無いことを求め、実行と APK ごとに分ける（X4-6）。チェックリストの差分の範囲を AAR と `m2` に絞った（S4-5）。そのほかの細部（S4-8〜S4-13）

## 1. 何をするか

| # | 中身 | 破壊的変更 |
|---|---|---|
| S4-0 | 段 4 の設計とレビュー、README の J-12 をコミットする。リリースとコミットの手順を直す（4.4）。検証のスクリプトに `--pull-apk` を足す | なし |
| S4-1 | 旧ブリッジの AAR を外す。層 2b のテストの 1.x の分岐を消す | なし（公開 API は変わらない） |
| S4-2 | 使われていない JSON ビルダーを消す（J-12） | あり（2 型） |
| S4-3 | Android の接頭辞の無い public 型 37 を改名し、42 型に `[MovedFrom]` を付ける | あり（37 型の名前。42 型の名前空間は段 3-0 で動いた） |
| S4-4 | マニュアル（版 `1.13.0`、英・日・韓）と移行の節、README、`Documentation~` | なし |
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

- `Runtime/Android/` の public 型は 44（`Android` で始まらないもの 37、始まるもの 7。7 のうち 2 は 3.3 で消す）。タグ `1.12.0` では、44 型すべてが `Runtime.<Feature>` にあり（Clipboard 13、Dialog 1、Notification 16、Share 14）、アセンブリはどちらも `NativeToolkit.Runtime`（レビュー v3、v4 で確かめた）。入れ子の public の型は無い。新しい名前は、パッケージと `Assets/` のどの型ともぶつからない
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
| `agent-rules/workflows/design-feature/workflow.md:54,57` | 「Android / iOS / macOS は `Runtime/<Feature>/`（未移行の形）」を、Android を移行済みに入れる形に直す（`:52` の `Common/` の説明は正しいので変えない） | S4-3 |
| `artifact/topics/android-c-abi-2/README.md`（5 章の段 4 の「11 型」）、`artifact/topics/os-prefix-violations/README.md`、`artifact/README.md:59,65` | 範囲と状況。**手で直す**（`os-prefix-violations/README.md` の旧名の多くは経緯の記録で、機械的に置き換えない） | S4-3 |
| `manual/1.13.0/`、`Packages/com.jonghyunkim.nativetoolkit/Documentation~/index{,.ja,.ko}.md`、`README{,.ja,.ko}.md` | 旧名、旧い名前空間、旧い手順（4 章） | S4-4 |

| 置き換えない | 理由 |
|---|---|
| `artifact/` の過去の設計・計画・結果・レビュー | 過去の記録 |
| `manual/` と `docs/` の版 `1.12.0` 以前 | 出した版の文書 |
| `Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/*.xcframework`、`Plugins/macOS` のフレームワークの `.swiftinterface`、`.abi.json` | Swift の同じ名前の型。置き換えると同梱のフレームワークの中身まで変わる |

**ほかの OS のファイル**: 振る舞いは変えず、コメントだけを直す（`common.md` の P2「ほかの OS のファイルを変えない」の例外。存在しない型を指したままにしないため）。

| ファイル | 今 | 直した後 | 段 |
|---|---|---|---|
| `Runtime/Clipboard/IosClipboardOperationResult.cs:15` | `ClipboardOperationResult`（`<c>`） | `AndroidClipboardOperationResult` | S4-3 |
| `Runtime/Share/IosShareJsonBuilder.cs:12` | `AndroidShareJsonBuilder`（`<c>`） | 「1.x の Android の builder」（型は消えるので、名前を出さない） | S4-2 |
| `Runtime/Windows/Clipboard/WindowsClipboardJsonBuilder.cs:18` | `AndroidClipboardJsonBuilder`（地の文） | 同上 | S4-2 |

### 2.3 `[MovedFrom]`（利用者の判断、2026-10-13）

- **enum を含む 42 型すべてに付ける**（X3-1）。改名した 37 型は `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>", sourceClassName: "<旧名>")]`、名前を残した 5 型は `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>")]`（`sourceClassName` を渡さない）。`<Feature>` はタグ `1.12.0` の名前空間（2.1 の表）。`sourceAssembly` は渡さない（アセンブリ名は変わっていない）。属性は 1 行で書く（3.4 の検査が行で見るため）
- 属性を置くファイル（2.1 の表のファイルと、5 型のファイル）に `using UnityEngine.Scripting.APIUpdating;` を足す（今のリポジトリに `MovedFrom` の使い道は無い。X4-1）
- **Unity の仕様**（Unity 6000.4.2f1。レビュー v3 の S3-2、X3-1、レビュー v4 の S4-3）:
  - コンストラクタは `(bool autoUpdateAPI, string sourceNamespace = null, string sourceAssembly = null, string sourceClassName = null)`。`AttributeUsage` は Class・Struct・Enum・Interface・Delegate（`UnityEngine.CoreModule.dll` を反射で読んだ）
  - スクリプトを書き換える API Updater の本体は、`Resources/BuildPipeline/Compilation/ApiUpdater/` の `ApiUpdater.MovedFromExtractor.dll` と `CSharpUpdater.dll` で、属性を自分で読む。その文字列には `sourceNamespace`、`movedFromNamespaces`、`MoveToNamespaceConfig` があり、`sourceClassName` が無い。**Updater が直すのは名前空間だけで、改名した 37 型の名前は直らない見込み**（要確認。V-S4-4 で確かめる）。属性の `AffectsAPIUpdater`（`sourceClassName` を渡すと false）は、Updater からは呼ばれていない（IL で確かめた）ので、根拠にしない
  - 属性は、シリアライズしたデータの移行にも使われる。`[Serializable]` の Payload（どれも class。`AndroidClipboardPayloads.cs:10-11` ほか）を旧名で `[SerializeReference]` に保存したデータに効くかを、V-S4-4 で確かめる
- **移行の節の既定**: 「37 型は `using` と型の名前を手で直す（旧名 → 新名の表）」「5 型は `using` を手で直す」。V-S4-4 で API Updater が直すと確かめられたものだけ、「自動で直る」と書き足す。利用者のコードの多くは `#if UNITY_ANDROID && !UNITY_EDITOR` の中にあり（マニュアルの勧め。`dialog.md:37-39` ほか）、Editor のコンパイルに入らないので、Updater は見ない見込み。その場合、Editor では何も起きず、Android の Player のビルドで初めてエラーになる（V-S4-4 の (c)）

## 3. 削除（S4-1、S4-2）と検査

### 3.1 S4-1: 旧ブリッジの AAR

- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/` の `android-native-toolkit-1.3.0.aar`、`unity-android-native-toolkit-1.3.0.aar` と、その `.meta` を `git rm` する
- `Plugins/Android/VERSION.txt` の `extra_dist_version`、`extra_aars` の行と、その説明のコメント（`:15-16`、`:22-24`、`:30-32`）を、Windows の `VERSION.txt` と同じく過去形の注記に直す
- `Editor/Build/PreBuildProcessor.cs` の `AndroidPin` から、追加の組を消す: プロパティとコンストラクタ（`:197-207`）、`<summary>` の「or when only one of the two extra_* keys is given」（`:210-212`）、読む処理（`:220-229`）と組み立て（`:231-235`）、写す処理、ログの追加の部分（`:416`、`:424-425`）。クラスの XML コメント（`:186-188`）を Windows（`:100-105`）と同じ形に直す
  - 残った `extra_*` のキーは、どのコードも読まないので無視される。ピンのファイルを読む `ReadPinValues`（`:136-160`）は知らないキーを無視する（`AndroidPin` は define の要るアセンブリの private の型で、EditMode のテストの仕組みが無い）
  - 確かめ: `grep -niE 'extra' Editor/Build/PreBuildProcessor.cs` が、Windows と Android のピンの XML コメントの過去形の注記だけに当たること（`ExtraDistVersion`、`ExtraAars` も拾う。S4-8）
- 旧 2 本の manifest、クラス、リソース、`proguard.txt`（空）は AAR の中にあり、AAR を消せば消える。1.x の AAR のクラスは `android/library`（245）と `android/unity`（60）だけで、2.0.0 のクラスは `com/jonghyunkim` だけ
- 2.0.0 の AAR の `LegacyScheduleCleaner` は 2.0.0 の側にあるので残る（移行の節。Notification の J-N19）
- `agent-rules/coding-rules/common.md:45` の「旧ブリッジの AAR は段 4 で外す」を過去形にする

### 3.2 S4-1: 層 2b のテストの 1.x の分岐

- 6 ファイルの `OnCApi` の分岐（56 か所）から、1.x の側を消し、C ABI の期待だけにする。`OnCApi` の判定は、各 fixture の `SetUp` の `Assert.IsTrue(OnCApi, "...")` に置き換える（継ぎ目が消えたり名前が変わったりしたら、黙って通るのではなく落ちる）。`NTK-*-RECORD` のログは消す
- 1.x の記録は、各機能の UI テスト計画と設計の 1.x の列に残っている。Share の実装結果 v2 の 7 章の約束を果たす

### 3.3 S4-2: JSON ビルダー（J-12）

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareJsonBuilder.cs`、`Runtime/Android/Clipboard/AndroidClipboardJsonBuilder.cs`、`Tests/Runtime/AndroidShareJsonBuilderTests.cs`、`Tests/Runtime/AndroidClipboardJsonBuilderTests.cs` を、それぞれの `.cs.meta` と対で `git rm` する
- 消した型に触れる Android のコメント（`AndroidClipboardCApi.cs:103`）と、ほかの OS のファイルの 2 か所（2.2）を直す
- `agent-rules/coding-rules/testing.md:406-407` の Android の Clipboard・Share から「builder」を消す。EditMode のテストは 37 本減る（Share 21、Clipboard 16）
- `agent-rules/coding-rules/common.md:518` の「`XxxJsonBuilder` クラスで JSON 組み立てを担い、Manager から呼び出す」に、Android の例外を書く（Share・Clipboard の Manager は Payload を C ABI に渡し、ビルダーを持たない。`AndroidNotificationJsonBuilder` は利用者が Manager に渡す JSON を組む道具で、Manager は呼ばない）
- **残す**: `AndroidNotificationJsonBuilder`（J-12）。未コミットの XML コメントの直し（2026-10-13）は、このコミットに入れる

### 3.4 検査（段ごと）

| 段 | パス | 0 件にするもの |
|---|---|---|
| S4-3 の後 | `Packages/com.jonghyunkim.nativetoolkit/Runtime`、`Packages/com.jonghyunkim.nativetoolkit/Tests`、`agent-rules` | 37 の旧名（単語の境界）、消した 2 ビルダーの名前。`[MovedFrom]` の行は除く |
| S4-3 の後 | `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android` | **`[MovedFrom]` の照合**（下） |
| S4-4 の最後のコミット | `manual/1.13.0`、`Packages/com.jonghyunkim.nativetoolkit/Documentation~/index{,.ja,.ko}.md`、`README{,.ja,.ko}.md` | 37 の旧名、消した 2 ビルダーの名前、旧い手順、1.x のクラス名、旧い名前空間の組（どれも下） |

- **`[MovedFrom]` の照合**（X3-2、X4-2）: 2.1 の表を写した小さな Python で照らす。消した後の public 型がちょうど 42。各型に `[MovedFrom]` がちょうど 1 つ。42 型すべてで、第 1 引数が `true`、`sourceAssembly` が無く、`sourceNamespace` が 2.1 の表のタグ `1.12.0` の名前空間と一致する。改名した 37 型は `sourceClassName` が旧名と一致し、名前を残した 5 型には `sourceClassName` が無い。各ファイルに `using UnityEngine.Scripting.APIUpdating;` がある
- **旧い手順**: 正規表現 `Config\w* Gradle Project`、`ConfigureGradleProject`、`Add Kotlin Dependencies`（日本語版の綴りの揺れ「Configue」も拾う）
- **1.x のクラス名**（X4-3、S4-14）: Python の正規表現 `android\.(?:library|unity)\.[a-z]+\.[A-Za-z.]*[A-Z]`。ただし `android.unity.notification.ACTION_` で始まるもの（今も公開しているアクション ID の値。`AndroidNotificationManager.cs:63,68`）は除く。`1.12.0` の文書では `notification{,.ja,.ko}.md:621` の 3 件に当たる（レビュー v4 の X4-3）
- **旧い名前空間の組**（S3-1）: コードのブロック（コードフェンスの間）ごとに、`UNITY_ANDROID` を含み、かつ `using JonghyunKim.NativeToolkit.Runtime.(Dialog|Share|Clipboard|Notification);` を含むものを 0 件にする（`1.12.0` の文書では、旧い `using` を持つ Android のブロック 45 個をすべて拾い、ほかの OS のブロックは拾わない。レビュー v3、v4 で確かめた）
- **許す節**（S3-9、S4-1）: `manual/1.13.0/*.md` の、見出しの文言が英 `Changes in 1.13.0`、日 `1.13.0 での変更`、韓 `1.13.0 변경 사항` で始まる見出し（段を問わない）から、同じ段以上の次の見出しまで。旧名 → 新名の表、消したビルダーの説明、旧い名前空間と旧い手順の説明はここにだけ書く
- S4-4 の途中のコミット（起点のコピー、ページごとの直し）では 0 件を求めない
- `scripts/verify_manual.sh` の検査 2 は接頭辞の付いた名前しか調べない（`:212` の `PACKAGE_TYPE`）ので、この検査を代わりにする

## 4. 文書（S4-4）

`agent-rules/workflows/write-manual` と `verify-manual` に従う。

### 4.1 順とコミット

1. **起点**: `manual/1.12.0` を `manual/1.13.0` に写し、すぐにコミットする（`chore(manual): copy manual/1.12.0 to manual/1.13.0 as baseline`。push は利用者が行う。基本情報）
2. index と、各機能のページ（dialog、share、clipboard、notification）を、ページごとに 3 言語をまとめて直し、1 コミットずつ
3. README（3 言語）と `package.json` の `description` / `keywords`。1 コミット
4. `Documentation~/index{,.ja,.ko}.md` と、使われなくなる画像（4.3）。1 コミット
5. 3.4 の検査（S4-4 の行）
6. `scripts/publish_docs.sh 1.13.0`（`docs/1.13.0` と `docs/latest` を書く。`:44-57,86-93`）。そのあと `scripts/verify_manual.sh 1.13.0` の停止項目 0 件（`docs/<版>` が無いと止まるので、この順）。`docs/1.13.0` と `docs/latest` を 1 コミット

### 4.2 中身

**各機能のページ（dialog、share、clipboard、notification。3 言語）**: 移行の節（英 `### Changes in 1.13.0`、日 `### 1.13.0 での変更`、韓 `### 1.13.0 변경 사항`）を、`## Android` の直下（最初の小節）に置き、目次の `Android` の下に行を足す（版 `1.12.0` の前例: `dialog.md:19-20` の目次と `:829-831`、`clipboard.md:2206-2208`、`notification.md:904-908` は、`## Windows` の直下に置いた）。中身は**その機能の利用者が読む移行の中身をすべて**（X3-7、X4-8）:
- その機能の型の旧い名前空間と新しい名前空間（`using` の直し方の例）
- その機能の改名した型の旧名 → 新名の表（2.1。Dialog は改名する型が無く、`AndroidDialogManager` の名前空間だけ）
- `[MovedFrom]` で直るもの・手で直すもの（2.3 の既定と V-S4-4 の結果）
- その機能の既知の差分（設計 5.3 と、各実装結果の 3.4: Dialog の結果 v4、Share の結果 v2、Clipboard の結果 v3、Notification の結果 v2）
- 消したビルダー（share、clipboard）
- Notification: `tools:keep`（J-N6）、`IsNotificationScheduled` は `persistAcrossBoot` の予約だけを見る、権限が無いときの失敗（J-N7）、1.x が保存した予約は起動時に消える（J-N19）、1.x が出した通知は 2.0.0 にした後は反応しない、1.12.0 の手順で足した `<service>` の行を消す
- **全機能に共通の破壊的変更**（4 ページに同じ文を置く。X4-8）:
  - 1.x の AAR（`android-native-toolkit-1.3.0.aar`、`unity-android-native-toolkit-1.3.0.aar`）を同梱しなくなった。その中のクラス（`android.unity.*`、`android.library.*`）を直接使っていたコードは動かない
  - 「Configure Gradle Project」（エディタのウィンドウ `AndroidConfigureProjectWindow`）が無くなった。`unityLibrary/build.gradle` に実行時の依存を足すようになった（kotlin-stdlib `2.2.21`、coroutines `1.9.0` を含む計 7 つ。`AndroidGradleDependencies.cs:46-52`。KGP の適用は無くなった。独自の Gradle のテンプレートや EDM4U を使う利用者は、依存がぶつかることがある）
  - 32 ビットのビルドが止まる（J-5）。IL2CPP だけで、Mono は使えない（J-10）
  - 詳しい動作環境は index の「Android requirements」へのリンク

**index（3 言語）**:
- 「# Supported OS Versions」（日「# 対応 OS バージョン」、韓「# 지원 OS 버전」）の下に、新しい節を作る（S3-8、S4-10）。見出しは英 `## Android requirements`、日 `## Android の要件`、韓 `## Android 요구 사항`。中身: ARM64（と x86_64）だけ、IL2CPP だけ（J-10）。Mono は使えない（Unity の Android の Mono は ARMv7 しか組めず、ビルドが止まる。親課題の README の記録 `6.3`、`6.4`）
- その直後に、案内の節（英 `## Changes in 1.13.0 (Android)`、日 `## 1.13.0 での変更（Android）`、韓 `## 1.13.0 변경 사항 (Android)`）を置く。中身は 1〜2 文の要約（Android の型の名前空間が `Runtime.<Feature>` から `Runtime.Android.<Feature>` に移り、`Android` の接頭辞を持たない型が改名された。1.x の AAR と「Configure Gradle Project」が無くなった）と、4 ページの移行の節へのリンクだけ。移行の中身は書かない
- 「Getting Started」の Android サンプルの手順から、「Configure Gradle Project」とそれに続く手順（「Browse」「Run: Add Kotlin Dependencies」。`index.md:185-191`、日本語版は「Configue」）を消す。Gradle の依存は `IPostGenerateGradleAndroidProject` が足すので、利用者の手順は要らない（J-1）
- 冒頭の紹介文「Editor windows help integrate native libraries and Gradle/Xcode settings」（`index.md:7`）を事実に合わせる

**そのほかの直し**:
- 各「Import the namespace」の節を `Runtime.Android.<Feature>` にする
- Notification の manifest の例（`notification.md:619-624`、3 言語）の、1.x のクラス名の `<service>` の行を消す
- 版 `1.12.0` のマニュアルの、版 `1.13.0` で事実でなくなる記述を直す（例: `dialog.md:835`「The Android, iOS and macOS types keep their namespace.」）

**README**（3 言語）: 機能、動作環境、冒頭の紹介文（`README.md:5`）。`package.json` の `description`（同じ紹介文）と `keywords`。版は S4-5 で上げる。

**リリースノート**（S4-5）: Breaking Changes は、各機能のページの移行の節（`#changes-in-1130`）へ直接リンクする（4.4）。

### 4.3 `Documentation~`

- `index{,.ja,.ko}.md`: 旧名、Android の例の旧い `using`（`index.md:137-147` ほか。3.4 の組の検査で拾う）、冒頭の紹介文（`:7`）、「Configure Gradle Project」と続く手順（`:95-98`。`:99` の「Install the sample app from Android Studio」は残す）
- 画像 `Documentation~/images/editor/ConfigureGradleProject.png` は、参照が無くなるので `git rm` する（パッケージに入る、git で管理されたファイル。`manual/1.12.0` の同じ画像は残る）
- **API の docs**（`build-docs.sh` が出す `Documentation~/site`）は gitignore されていて（`.gitignore:86-88`）、`docs/<版>` には写さない（`publish_docs.sh` が写すのはマニュアルだけ）。`build-docs.sh` は確かめに 1 度流し、生成された `api/` に新しい 37 型があり、旧名が無いことを見る（`docfx.json` の `allowCompilationErrors: true` のため、成功だけでは足りない）

### 4.4 リリースとコミットの手順の直し（S4-0 で入れる。S4-7）

- `agent-rules/workflows/release/workflow.md`:
  - ステップ 3 のチェックリストに Android の AAR の行を足す（6 章）
  - ステップ 4 のリリースノートに「### Breaking Changes」の節を足す。**出す条件**: 英語の `manual/<version>/*.md` のどれかに「Changes in `<version>`」で始まる見出しがあるとき、またはこの版のコミットの件名に `!` があるとき（Conventional Commits の破壊的変更の印）。そのときは必須。中身は移行の節の要約と、各機能のページの移行の節へのリンク
- `agent-rules/workflows/commit-msg/workflow.md:23` の件名の形に、破壊的変更の `!`（`<type>(<scope>)!: <subject>`）を足す。S4-2 と S4-3 のコミットの件名には `!` を付ける

## 5. 検証

| # | 何を | いつ |
|---|---|---|
| V-S4-1 | 旧 2 本を外した後、1.x のクラスが APK に入っていないこと。2.0.0 の AAR だけで全テストが通ること | S4-0 の後（陽性の対照）、S4-1 |
| V-S4-1b | `--as-package-user` の実行が、コミットする `Plugins/Android` だけで通ること | S4-1 |
| V-S4-2 | 改名と削除の後のコンパイルとテスト | S4-1、S4-2、S4-3 のコミットの前。レビューの直しの後 |
| V-S4-3 | `Plugins/Android` の AAR がちょうど 2 本 | S4-1 |
| V-S4-4 | `[MovedFrom]` の効く範囲（2.3） | S4-3 のコミットの後 |
| V-S4-5 | 3.4 の検査 | S4-3、S4-4 |
| V-S4-6 | マニュアル | S4-4（4.1 の 6） |

**V-S4-1**: `verify_unity_android.sh --as-package-user --pull-apk=<dir>`。
- `--pull-apk` は S4-0 で足す（値は `=` で渡す。今の引数の読み方は値を取る引数を受けない。`:82-89`）。`<dir>` は、無いか空であること（中身があれば終了コード 2 で止まる。X4-6）
- 各実機の実行（`Android`、`Android-retention`）の後、次の実行の頭のアンインストールの前に、`adb shell pm path com.UnityTestRunner.UnityTestRunner` の全行から `package:` を取り、すべての APK を `<dir>/<実行の名前>/` に `adb pull` し、APK ごとに別のフォルダ（`<dir>/<実行の名前>/<APK の名前>/`）に展開する
- 各 APK の `classes*.dex` をすべて Unity 同梱の `PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/dexdump` に渡し、`Class descriptor` の行の `Landroid/unity/` と `Landroid/library/` が 0 件
- base の APK の merged manifest を同梱の `SDK/cmdline-tools/16.0/bin/apkanalyzer manifest print`（`JAVA_HOME` に同梱の `OpenJDK`）で読み、`android\.(unity|library)\.` のクラス名が 0 件（アクション ID の `android.unity.notification.ACTION_` は除く）
- pull の失敗、APK が 0 本、dex が 0 本は失敗
- **陽性の対照**: S4-0 の後、S4-1 の前（1.3.0 の 2 本がある状態）で同じ手順が 0 より多いこと（AAR の展開で、1.x の manifest は `android.unity` などが 5 件と 1 件、2.0.0 の 2 本は 0 件と確かめてある）

**V-S4-1b**（S4-9、X4-9）: 順は「S4-1 の実装 → `git status --porcelain -- Packages/com.jonghyunkim.nativetoolkit/Plugins/Android` を控える → V-S4-1 の実行 → status を比べ、ログを見る → コミット → `git ls-files` を見る」。
- 実行の前後で status が同じ（`PreBuildProcessor` は Android™ Profile の実行で毎回 AAR を上書きで写し、`Copied` をログに出す〈`PreBuildProcessor.cs:450-454`〉。status が同じなら、写したものはバイトが同じで、何も消されていない。S4-1 の `git rm` と `VERSION.txt` の変更は前後どちらにも出る）
- Android™ Profile の実行のログ（`$OUT_DIR/Android.log`、`$OUT_DIR/Android-retention.log`。build steps が動くのはこの 2 つだけ）に `Deleted AAR not in the pin`（`PreBuildProcessor.cs:447`）が無い
- コミットの後に `git ls-files Packages/com.jonghyunkim.nativetoolkit/Plugins/Android` が `VERSION.txt`、AAR 2 本と、それぞれの `.meta` の 6 つ

**V-S4-2**: `verify_unity_android.sh --as-package-user`（Editor の EditMode・PlayMode と、実機の実行。層 2b のテストのアセンブリと `UNITY_ANDROID && !UNITY_EDITOR` の中は、実機の実行でしかコンパイルされない）。ほかの build target は要らない: 改名・削除した型を Android 以外のコードは参照していない（コメント 3 か所だけ。2.2）。S4-1 では V-S4-1 の実行がこれを兼ねる。

**V-S4-3**: `ls Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/*.aar` が 2 本（androidlib のフォルダは git で管理しないローカルのもので、数えない）。

**V-S4-4**（S4-2、X4-4、X4-5）: すべて作業ツリーの外の一時フォルダ `$W` で行う。Unity は 6000.4.2f1 の batchmode（`-accept-apiupdate` は、この版の Editor の実行ファイルにある引数。レビュー v4 の S4-2 で確かめた）。
- **準備**: `git archive 1.12.0 Packages/com.jonghyunkim.nativetoolkit | tar -x -C $W/old`、`git archive <S4-3 のコミット> Packages/com.jonghyunkim.nativetoolkit | tar -x -C $W/new`。使い捨てのプロジェクト `$W/proj` の `Packages/manifest.json` から、`file:$W/old/Packages/com.jonghyunkim.nativetoolkit` を参照する。`$W/proj` を git で初期化し、差分を git で見る
- **段 1（旧版で作る）**: `Assets/` に次を置き、開いてコンパイルが通ることを確かめ、`-executeMethod` でアセットを作り、作った値を `$W/expected.json` に書く。そのあと作るスクリプトを消し、コミットする
  - `Holder.cs`: `ScriptableObject` を継ぎ、`[SerializeReference] public List<object> payloads;` だけを持つ（旧名を参照しない）
  - `Editor/MakeAsset.cs`: 旧名の Payload（`CopyPlainTextPayload`、`ChannelPayload`、`ShareTextPayload`）に値を入れて `Holder` のアセットに保存する（旧名を参照する。段 1 の最後に消す）
  - `Ns5.cs`（ガード無し）: 旧い `using` と、名前を残した型（`AndroidClipboardManager` など）だけを参照する
  - `Renamed37.cs`（ガード無し）: 旧い `using` と、改名した型（`ClipboardReadResult` など）だけを参照する
  - `Guarded.cs`: `#if UNITY_ANDROID && !UNITY_EDITOR` の中で、旧い `using` と 5 型・37 型を参照する
- **段 2（新版で開き直す）**: manifest を `file:$W/new/Packages/com.jonghyunkim.nativetoolkit` に変え、`-accept-apiupdate -quit` で開く。記録すること: (a) `Ns5.cs` の差分（名前空間が直るか）、(b) `Renamed37.cs` の差分とコンパイルエラー（37 型が直らない見込み）、(c) `Guarded.cs` の差分（Editor では `!UNITY_EDITOR` の中はコンパイルされないので、変わらない見込み。その場合は Android の Player のビルドで初めてエラーになる、と移行の節に書く）
- **段 3（読み直す）**: コンパイルエラーが残るスクリプト（`Renamed37.cs` など）を消し、旧名を参照しない `Editor/ReadAsset.cs`（各要素の `GetType().FullName` と `JsonUtility.ToJson` を書き出す）を足し、`-executeMethod` で読む。記録すること: (d) 3 つの Payload が新しい型の名前で、`$W/expected.json` と同じ値で読めるか（エラーのあるまま走らせると、前のアセンブリの型で読んで偽の PASS になりうるので、段 3 はコンパイルエラーが 0 のときだけ見る）
- (e) リポジトリの作業ツリー（`git status --porcelain` が V-S4-4 の前後で同じ）と、`$W/new` のパッケージのコピー（V-S4-4 の前に取ったファイルの一覧と hash と同じ）が変わらない。`$W/proj/Assets` の差分は (a)〜(c) で記録する

**V-S4-5**: 3.4。**V-S4-6**: 4.1 の 6。

各段の実装の後に、実装のレビュー（サブエージェントと Codex）を回す。S4-1〜S4-3 は機械的な変更が多いので、1 回にまとめてよい（7 章のステップ 5）。

## 6. リリース（S4-5）

`agent-rules/workflows/release/workflow.md` に従う。**前提**: native-toolkit の版 `1.13.0` がタグになっていること。S4-1〜S4-4 を先に進め、この段だけをタグまで待つ（利用者の判断、2026-10-13）。**push**: ブランチとタグの push は利用者が行う。エージェントは、ステップ 5 の `gh pr create` の前、`:133` の push の前、ステップ 7 の `git push origin <version>`（`:145`）の前で止まり、利用者に頼む（基本情報。S4-4）。

- **`<verified>` の置き場所**（S3-5）: 最後の通しの検証（7 章）のときの native-toolkit のコミットを、`Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/VERSION.txt` に新しいキー `verified_native_toolkit_commit:` で書く。`ReadPinValues` は知らないキーを無視する（`PreBuildProcessor.cs:145-160`）。`VERSION.txt` の頭のキーの一覧（`:11-16`）に、「ビルドは読まない。リリースのチェックリストが読む」と書いて足す（S4-13）。その検証のときに `git -C ../native-toolkit status --porcelain -- dist/1.13.0/android` が空だったことを、実装結果に書く。Android を触らない後のリリースでも、`dist_version` が同じならこの値のまま使える
- **チェックリストの Android の行**（X3-6、S4-5）: `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/VERSION.txt` の `dist_version` を `<dv>`、`aars` の 2 本を `<aar>`、`verified_native_toolkit_commit` を `<verified>` として:
  - `git -C ../native-toolkit rev-parse -q --verify refs/tags/<dv>` が成功する（タグがある）。無ければ ❌
  - 同梱の `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/<aar>` の md5 が、`git -C ../native-toolkit show <dv>:dist/<dv>/android/<aar>` の md5 と一致する
  - `git -C ../native-toolkit diff --quiet <verified> <dv> -- 'dist/<dv>/android/*.aar' dist/<dv>/android/m2` が 0 で終わる（パッケージが使うのは AAR と、`AndroidGradleDependencies` が照らす `m2` の `.module` だけ。`build-*.log` と `include/` の差は見ない）
  - Android の `VERSION.txt` には Windows の `source:` に当たる行を足さない（dist の版とタグの名前が同じ）
- **合わなかったとき**: AAR を差し替え、`AndroidGradleDependencies` の版を `m2/` の `.module` と照らし（J-2 のテスト）、通しの検証をやり直し、`verified_native_toolkit_commit` を書き直す
- `package.json` の版を `1.13.0` にする（この段だけ）

## 7. 順とコミット

0. **S4-0**:
   - 段 4 のその時点の設計とレビューのすべてと、親課題の README の J-12 を `docs(artifact)` で 1 コミット（`AndroidNotificationJsonBuilder.cs` の XML コメントの直しは含めない。S4-2 に入れる。S4-12）
   - リリースとコミットの手順の直し（4.4）を `docs(agent-rules)` で 1 コミット
   - `verify_unity_android.sh` の `--pull-apk` を `test(android)` で 1 コミット
   - V-S4-1 の陽性の対照（版 `1.3.0` の 2 本がある状態で、旧いクラスが見つかること）
1. **S4-1**（AAR、ピン、`VERSION.txt`、`PreBuildProcessor`、テストの 1.x の分岐、`common.md:45`）。V-S4-1b の順（5 章）で、V-S4-1、V-S4-3 を見てから 1 コミット
2. **S4-2**（ビルダー、`AndroidNotificationJsonBuilder` の XML コメント、ほかの OS のコメント 2 か所、`testing.md:406-407`、`common.md:518`）。V-S4-2 の後に 1 コミット（件名に `!`）
3. **S4-3**（改名、`[MovedFrom]`、参照の置き換え、`IosClipboardOperationResult.cs` のコメント、agent-rules、artifact の README）。V-S4-5（S4-3 の 2 行）と V-S4-2 の後に 1 コミット（件名に `!`。参照の置き換えを分けると、途中のコミットがコンパイルできない）。この V-S4-2 のときの native-toolkit のコミットと、`dist/1.13.0/android` が clean だったことを控える
4. **V-S4-4**（作業ツリーの外）
5. **実装のレビュー**（S4-1〜S4-3。サブエージェントと Codex）。直しが S4-1〜S4-3 のコードやテストに及んだら、直した後に V-S4-2 をやり直し、控える native-toolkit のコミットを書き換える
6. `verified_native_toolkit_commit` に、最後の V-S4-2（ステップ 3 か 5）のときの native-toolkit のコミットを書き、`VERSION.txt` の頭のコメントを足して 1 コミット（6 章）。この後に `Runtime`、`Tests`、`Editor`、`Plugins/Android` が変わったら、V-S4-2 をやり直し、この値を書き直す
7. **S4-4**（4.1 の順）。V-S4-5（S4-4 の行）、V-S4-6
8. **S4-5**（リリース）。native-toolkit の版 `1.13.0` のタグの後

コミットはどれも、利用者に確かめてから行う。
