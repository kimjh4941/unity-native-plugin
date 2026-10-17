# レビュー結果

- 日付: 2026-10-13
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v2.md
- 機能名: android-c-abi-2（段 4）
- プラットフォーム: android（改名は macOS の 1 型を含む）
- レビュアー: サブエージェント（ラウンド 1）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力。文言には手を加えていない（見出しの段を下げ、サブエージェントの先頭の題を外した）
- A1: サブエージェント 5 件（S1-1、S1-2、S1-3、S1-6、S1-8）、Codex 6 件（X1-1〜X1-6）

---

## 1. サブエージェント（Claude）


対象: `artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v2.md`

---

### S1-1 ｜ A1 ｜ 高 ｜ 1.3 / 1.2 / 4 章 Q-1
**問題**: 改名の対象が「ファイル名に接頭辞が無い型」に限られている。S4-3 の 15 型を改名したあとも、接頭辞の付いたファイルの中に、接頭辞の無い public の型が 31 個残る。
- Android の Payload（23）
  - Notification の 11: `NotificationPayload`、`ChannelPayload`、`NotificationActionPayload` ほか
  - Clipboard の 4: `CopyPlainTextPayload` ほか
  - Share の 8: `ShareTextPayload`、`ChooserActionPayload`、`RemoveDirectShareTargetsPayload` ほか
- iOS（5）: `NotificationContentPayload`、`TimeIntervalTriggerPayload`、`CalendarTriggerPayload`、`LocationTriggerPayload`、`NotificationAttachmentPayload`
- macOS（3）: `DialogButton`、`DialogButtonsWrapper`、`DialogOptions`（`MacDialogManager.cs` の中）

設計の中で次の 3 点が食い違う。
- Q-1 の理由「残すと接頭辞の規則の逸脱が残る」は、この 31 型にもそのまま当てはまる。
- 1.2 は Payload を「`Android*Payload`」と書いているが、事実と違う。接頭辞が付いているのはファイル名（`AndroidSharePayloads.cs` など）だけで、型名には付いていない。
- 1.3 は `common.md` の「既知の逸脱」の表を**消す**としている。31 型が残ったまま消すと、規則が満たされているように見えてしまう。

**根拠**: `common.md:407`（「ファイル名と、その中の public / internal な型名には…接頭辞」）。`Runtime/Android/*/Android*Payloads.cs`、`Runtime/Notification/IosNotificationPayloads.cs`、`Runtime/Dialog/MacDialogManager.cs` を grep した結果。

**提案**: 利用者に範囲を決めてもらう。
- (a) Android の Payload 23 型も 1.13.0 に含める（Android はこの版で破壊的変更がすでにある）
- (b) 残す。この場合は「既知の逸脱」の表を消さずに書き換え、os-prefix-violations の README に「残り」として記録する

どちらでも、1.2 の「`Android*Payload`」は直す。

---

### S1-2 ｜ A1 ｜ 中〜高 ｜ 1.3（`MacIconConfiguration`）
**問題**: 移したあとのコンパイルガードが決まっていない。
- 今の `IconConfiguration.cs` にはガードが無い。
- 一方、P5 の規則では新しいコードに `#if UNITY_STANDALONE_OSX || UNITY_EDITOR` を付ける。付けると、iOS・Android・Windows の Player からこの型が消える。macOS のガードの外でこの型を参照している利用者のコードは、追加で壊れる。
- 付けなければ、実装のレビューで P5 の違反（A1）として止まる。

利用者に見える文字列の扱いも決まっていない。
- `IconConfiguration.cs:81` の `ToString()`（`"IconConfiguration(...)"`）
- `MacDialogManager.cs:219-277` のエラーの文言 7 種。`AlertDialogResult` の `errorMessage` として利用者に届く。

**提案**: 次の 2 つを設計に明記する。
- ガードは今のまま付けない。P5 の例外として理由を書く。
- 文字列は変えるか残すかを決める。変えるなら既知の差分に書く。

---

### S1-3 ｜ A1 ｜ 中 ｜ 1.1（S4-1 の範囲）
**問題**: 層 2b のテストに残っている 1.x の分岐を消す作業が抜けている。
- 6 ファイルに `OnCApi` が 56 か所ある（`AndroidNotificationPlayerTests` 24、`AndroidClipboardManagerIntegrationTests` 13、`AndroidSharePlayerTests` 10 ほか）。`NTK-*-RECORD` のログも残っている。
- Share の実装結果 v2 の 7 章は「1.x のピンを外すときに消す」と約束している。
- S4-1 の後、この分岐は死んだコードになる。テスト用の口（seam）が誤って無くなると、1.x の期待値で黙って判定してしまう。

**提案**: S4-1 のコミットで、`OnCApi` が偽の側を消すか、`Assert.IsTrue(OnCApi)` に置き換える。1.x の記録は UI テスト計画に残っているので、それで足りる。

---

### S1-4 ｜ B ｜ 高 ｜ 1.3「確かめ」、V-S4-2
**問題**: 「4 つの build target でコンパイルする」の実際のやり方が書かれていない。
- Editor でのコンパイルは `UNITY_EDITOR` が定義されるので、`!UNITY_EDITOR` の中は一度もコンパイルされない。改名した型を `!UNITY_EDITOR` の中で使っている所:
  - `Runtime/UI/macOS/Dialog/MacDialogManagerExampleController.cs:172-196`
  - パッケージの外にある **`Assets/Scripts/Runtime/UI/macOS/Dialog/MacDialogManagerTest.cs:5-7, 38-58`**（`IconConfiguration` と `using ...Runtime.Common`。この参照の置き換え自体も 1.3 の一覧から漏れている）
- Android の分は `verify_unity_android.sh` の Player のビルドで確かめられる。
- macOS の Player を検証するスクリプトは無い。
- このマシンには Windows Build Support が入っていない（PlaybackEngines は AndroidPlayer、iOSSupport、Unity 同梱の MacStandaloneSupport だけ）。

**根拠**: `scripts/verify_unity_windows.sh:5-9`（同じ落とし穴で、レビューを 3 回通った前例）。

**提案**: target ごとに方法を書く。
- macOS・iOS: `-executeMethod` から `PlayerBuildInterface.CompilePlayerScripts`（target が StandaloneOSX / iOS）を呼ぶ。macOS は macOS Profile で Player をビルドしてもよい。
- Windows: Windows 機の `verify_unity_windows.sh` の層 0 で確かめる。省くなら、「Windows のコードは改名・削除した型を参照していない（`<c>` のコメントだけ）」ことを grep で示し、理由として書く。

---

### S1-5 ｜ A2 ｜ 中 ｜ 1.3「参照の置き換え」の経路
**問題**: 置き換える先の一覧が足りず、置き換えない先も書かれていない。

一覧に無い置き換え先:
- `Assets/Scripts/...`（S1-4 で挙げたファイル）
- `agent-rules/workflows/review-document/workflow.md:46`、`review-implementation-feature:67`、`review-implementation-sample-scene:62`（「既知の逸脱 11 件」。型名で grep しても見つからない）
- `design-feature/workflow.md:52`（`IconConfiguration` を Common の例に挙げている）と `:54`（Android を未移行と書いている）
- `common.md:45`（「旧ブリッジの AAR は段 4 で外す」）
- `artifact/README.md:59, 65`
- `Runtime/Clipboard/IosClipboardOperationResult.cs:15` のコメント（1.2 と同じ方針で残すかどうかを決める）

置き換えない先（明記が要る）:
- `artifact/` の過去の文書
- `manual/` と `docs/` の 1.12.0 以前
- `Plugins/iOS/*.xcframework` の `.swiftinterface` と `.abi.json`。同じ名前の Swift の型があるので、repo 全体に sed をかけると同梱しているフレームワークの中身まで書き換わる。

確かめたもの（変更は要らない）:
- `Samples~`: シーンだけで、改名する型は MonoBehaviour ではない
- `apiFilter.yml`: 型名を持たない
- `Localization`: Android や Gradle のキーが無い

---

### S1-6 ｜ A1 ｜ 中 ｜ 1.4 と冒頭の「CHANGELOG と移行ガイド」
**問題**: どちらも、どこに何を書くかが決まっていない。
- CHANGELOG のファイルは repo に無い。`package.json` の `changelogUrl` は GitHub Releases を指している。
- リリースの手順の 4（`release/workflow.md:59-91`）は、コミットの一覧から Features・Fixes・Documentation・Chores だけを作る。破壊的変更の節は出てこない。
- 移行ガイドの前例も無い。1.12.0 は各機能のページに「### Changes in 1.12.0」の節を置いた（`dialog.md:831`、`clipboard.md:2208`、`notification.md:908`）。
- どの検査もこれを捕まえない。

**提案**: 次の 2 つを決めて書く。
- 移行ガイドの形: 各ページの「Changes in 1.13.0」の節を 3 言語で書くか、`manual/1.13.0/migration{,.ja,.ko}.md` を作って index からリンクするか。
- リリースノートに「Breaking Changes」の節を足すこと。手順 4 で直すか、workflow を変える。

---

### S1-7 ｜ B ｜ 高 ｜ 1.4（`verify_manual.sh` に頼る確かめ）
**問題**: `verify_manual.sh` の検査 2 は、接頭辞の付いた名前しか調べない。
- 正規表現が `PACKAGE_TYPE = ^(?:Ios|Android|Mac|Windows)[A-Z]`（`verify_manual.sh:211`）なので、旧名（`IconConfiguration`、`ClipboardReadStatus` など）は調べられない。
- しかも検査 2 は警告で、止める項目ではない。
- 名前空間は調べない。

manual/1.12.0 には旧名が 96 か所ある（`IconConfiguration` 42、`ClipboardReadStatus` 18、`ClipContents` 6 ほか）。Android の節の `using ...Runtime.{Notification,Share,Clipboard,Dialog}` もある。これらは 1.13.0 にそのまま写され、検査を素通りする。

**提案**: `manual/1.13.0`、`README*`、`Documentation~` を対象に、15 の旧名と旧い名前空間を単語境界で grep する。移行の表だけを除外し、0 件を止める条件にする。

---

### S1-8 ｜ A1 ｜ 中 ｜ 1.4（マニュアルと移行ガイド）
**問題**: Notification のマニュアルの manifest の例が、1.x のクラス名のまま残る。
- `manual/1.12.0/notification.md:619-624`（ja・ko も同じ）は、利用者に `<service android:name="android.library.notification.presentation.progress.ProgressForegroundService">` を足すよう指示している。
- 2.0.0 の AAR は、この service を `com.jonghyunkim.nativetoolkit.notification.presentation.progress.ProgressForegroundService` として自分で宣言している（AAR の manifest で確認）。
- 検査は xml のブロックを読まないので、この記述はそのまま出荷される。

**提案**:
- 1.13.0 のマニュアルから、この例を消すか直す。
- 移行ガイドに「1.12.0 の手順で足した `<service>` の行を消す」と書く。

---

### S1-9 ｜ A2 ｜ 中 ｜ 1.4（移行ガイドの中身）
**問題**: 次が抜けている。
- Android の Mono は使えない（J-10、6.3。ビルドが止まる）。1.4 にあるのは 32 ビットだけ。
- `unityLibrary/build.gradle` に実行時の依存を 7 つ足すようになった（kotlin-stdlib 2.2.21、coroutines 1.9.0 ほか）。KGP 2.0.21 の適用は無くなった。独自の Gradle テンプレートや EDM4U を使う利用者は、依存がぶつかる見込みがある。
- 既知の差分の出典。1.4 は「各設計の 5.3」と、Notification の実装結果 3.4 しか挙げていない。Dialog・Share・Clipboard の実装結果にも「3.4 既知の差分（マニュアルへ引き継ぐ）」がある（Dialog 結果 v4:128、Share 結果 v2:127、Clipboard 結果 v3:135）。
- マニュアルの各「Import the namespace」の節。

---

### S1-10 ｜ B ｜ 中 ｜ V-S4-1、1.1「確かめ」
**問題 1**: 2 回の実行が独立していない。
- Android™ Profile の実行で、`PreBuildProcessor` がピンに無い AAR を作業ツリーから消す（`PreBuildProcessor.cs:438-447`）。
- スクリプトは `Plugins/Android` を元に戻さない（`verify_unity_android.sh:112-116`）。
- そのため後の `--as-package-user` は、1.3.0 の 2 本の `git rm` をコミットし忘れていても PASS する。

**問題 2**: 調べる APK の取り先が無い。
- スクリプトは Player をアンインストールし、`Library/Bee`・`Build`・`Temp` に APK は残っていない。
- APK 全体の文字列を grep すると、`AndroidNotificationManager.cs:63, 68` の `"android.unity.notification.ACTION_*"`（global-metadata に入る）に当たり、誤検出になる。

**提案**:
- コミットの後に `git ls-files Plugins/Android` がちょうど 6 ファイル（VERSION.txt と AAR 2 本、それぞれの .meta）であることを確かめる。
- package-user の実行は、`Plugins/Android` に差分が無いクリーンな状態から流す。
- APK は `BuildPlayer` で `Build/Android/` に出すか、アンインストールの前に `adb shell pm path` と `adb pull` で取る。
- `classes*.dex` を `dexdump` し、クラスの記述子 `Landroid/unity/` と `Landroid/library/` が無いことを見る。

---

### S1-11 ｜ A2 ｜ 中 ｜ 1.5（リリース、チェックリストの行）
**事実（確認済み）**:
- AAR は `dist/1.13.0/android/android-native-toolkit-2.0.0.aar`（md5 9e068bc9…）と `android-native-toolkit-capi-2.0.0.aar`（c706a8a6…）。同梱しているものと md5 が一致する。
- `m2/` の下にも同名・同じ md5 の写しがある。
- native-toolkit のタグは最新が 1.12.0 で、1.13.0 はまだ無い。

**問題**:
- (a) 1.5 の「置き場所は足すときに確かめる」は、もう事実で置き換えられる。
- (b) タグを打つ時点で native-toolkit が AAR を作り直していた場合の手順が無い。md5 が合わなければ、AAR の差し替え、`AndroidGradleDependencies` と m2 の `.module` の照合、通しの検証のやり直しが要る。
- (c) Windows の行は `source` がタグを指すことも確かめる。Android の VERSION.txt には `source:` が無い。
- (d) この行は `/release` を走らせる前（S4-4 までのコミット）で workflow.md に入れておく必要がある。

**提案**:
- 置き場所を事実で書く。
- md5 に加えて `git -C ../native-toolkit diff --quiet <dv> -- dist/<dv>/android` を使う。AAR、m2、ヘッダーをまとめて確かめられる。
- `source:` を足すか、Android では使わないと書く。
- md5 が食い違ったときの手順を書く。

---

### S1-12 ｜ C ｜ 低 ｜ 1.4「API の docs」
**問題**: 書かれている仕組みと事実が違う。
- `publish_docs.sh` が写すのは `manual/<v>` だけ（`:44-57`）。`docs/1.12.0` にも API の docs は無い。
- `build-docs.sh` が出すのは `Documentation~/site` で、これは gitignore されている（`.gitignore:88`）。

**提案**: 意図を書き直す。

---

### S1-13 ｜ C ｜ 低 ｜ 記述と事実の食い違い

1. **9 行目**: `Runtime/` を `android.unity.` で検索すると、Clipboard のサンプルのほかに次にも当たる。
   - `AndroidNotificationManager.cs:63, 68`
   - `AndroidNotificationCApi.cs:57, 60`（1.x のアクション ID の定数を保っている所）
2. **1.1「EditMode のテストがあれば」**: テストは無い。`AndroidPin` は define が要るアセンブリの中の private で、Tests を grep しても当たらない。そう書く。
3. **1.1**: VERSION.txt の説明のコメントも直す要る（`:15-16`、`:22-24`、`:31-33`）。Windows と同じく、過去形の注記にする。
4. **V-S4-3「2 本と androidlib だけ」**: `Plugins/Android` の androidlib は、git で管理していない空のローカルのフォルダ。.meta は `.gitignore:92` で無視されていて、ビルドが作るものでもない。期待の内容は「VERSION.txt と 2 本の AAR（と .meta）」と書く。
5. **1.3「testing.md の表」**: 改名する型を載せた表は testing.md に無い。直す要るのは、S4-2 の影響で層 1 の表の Android の Share・Clipboard の「builder」（`testing.md:405-406`）。EditMode の件数は 37 減る（21 + 16）。
6. **親 README 5 章の段 4 の行**: 「11 型」のままで、設計の 15 型と合っていない。
7. **コミットの割り当て**: `AndroidNotificationJsonBuilder.cs` の未コミットの XML コメントの変更を、S4-2 のコミットに入れると明記する。
8. **1.13.0 にそのまま写すと事実と食い違う箇所**:
   - `dialog.md:835`「The Android, iOS and macOS types keep their namespace.」
   - `common.md:518`「Manager から呼び出す」
9. **v2 は記録済み**: 冒頭の native-toolkit の行（11 行目）が「手元の最新は `49b99bc4`」だが、HEAD は `a7684dee`（`49b99bc4` は dist を最後に変えたコミット）。

**文書の出典**: 引用されている行のうち、`PreBuildProcessor.cs:100-105 / 186-188 / 211-227`、`AndroidClipboardCApi.cs:103`、`IosShareJsonBuilder.cs:12`、`WindowsClipboardJsonBuilder.cs:18`、`MacDialogManager.cs:4` は、すべて正しかった。

---

### S1-14 ｜ A2（提案） ｜ 低 ｜ 1.3「[Obsolete] の別名は置かない」
**問題**: 理由の「C# には型の別名を公開する手段が無い」は C# としては正しい。ただ、Unity の `UnityEngine.Scripting.APIUpdating.MovedFromAttribute` に触れていない。これは改名や名前空間の移動に使う属性で、Unity の API Updater が利用者のスクリプトを自動で直す手掛かりになり、`[SerializeReference]` で保存したデータの移行にも効く。URP などのパッケージでも使われている。

**要確認**: Unity 6000.x で、パッケージのアセンブリの型に付けたときにも効くか。

**位置づけ**: 破壊的変更として書くという決定は変えない。軽くする手段の 1 つとして、利用者が判断する材料に挙げる。

---

### 総評
**A1 は 5 件**: S1-1、S1-2、S1-3、S1-6、S1-8。

- 構成、段の順、1 段 1 コミット、Windows の前例に合わせた方針は妥当。引用している行番号も正確だった。
- 一方で、範囲の数え方がファイル名を基準にしているため、接頭辞の無い型が 31 個残る（S1-1）。
- 検証は、Editor のコンパイルと `verify_manual` が見ない範囲に穴がある（S1-4、S1-7、S1-10）。
- 文書は、CHANGELOG と移行ガイドの置き場所が無い（S1-6）。1.x のクラス名の残り（S1-8）とテストの 1.x の分岐（S1-3）も漏れている。

v3 で、S1-1 と S1-2 の利用者の判断を取り込み、S1-4・S1-7・S1-10 の具体的な検証の手段を書くことを勧める。

---

## 2. Codex

読み取り専用で一次ソースと照合しました。ファイル・Git 状態は変更していません。

#### X1-1
- 分類: A1: 実装で気づけない
- 場所: 設計 1.2（/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v2.md:38）
- 問題: JSON ビルダーとテストの削除対象に、対応する 4 本の `.cs.meta` が含まれていない。
- 証拠: `AndroidShareJsonBuilder.cs.meta`、`AndroidClipboardJsonBuilder.cs.meta`、`AndroidShareJsonBuilderTests.cs.meta`、`AndroidClipboardJsonBuilderTests.cs.meta` が実在する。孤立した `.meta` はコンパイルでもテストでも検出されない。AAR 削除は設計 31 行、改名は 62 行で `.meta` を明記している。
- 修正: 4 組とも `.cs` と `.cs.meta` を対で削除すると明記する。

#### X1-2
- 分類: A1: 実装で気づけない
- 場所: 設計 1.3〜1.4（同ファイル:64）
- 問題: 旧 15 型を含む非コンパイル文書の置換経路が列挙されていない。`verify_manual.sh` は停止項目が 0 件でも旧名を見逃す。
- 証拠:
  - Documentation~/index.md:1309 と日本語版・韓国語版に `IconConfiguration` がある。
  - manual/1.12.0/clipboard.md:334、share.md:430、notification.md:173、dialog.md:1263 と各翻訳版に旧型名がある。
  - `write-manual` は前版をコピーする（agent-rules/workflows/write-manual/workflow.md:13）。明示的に置換しないと 1.13.0 に旧名が残る。
  - `verify_manual.sh:211` の検査対象は `Ios|Android|Mac|Windows` 接頭辞の型に限られ、接頭辞の無い旧名は対象外。
- 修正: `Documentation~/index{,.ja,.ko}.md` と `manual/1.13.0/{clipboard,dialog,notification,share}{,.ja,.ko}.md` を変更対象に明記する。旧 15 型、旧 Android 名前空間、旧 `Common.IconConfiguration` が 0 件になる `rg` 検査を DoD に加える。

#### X1-3
- 分類: A1: 実装で気づけない
- 場所: 設計 1.2 の「ほかの OS は変えない」（同ファイル:44）
- 問題: 削除後も、他 OS の XML コメントに存在しない型名が残る。改名される Android 型への iOS 側の参照も明示されていない。
- 証拠:
  - Runtime/Share/IosShareJsonBuilder.cs:12 は、削除される `AndroidShareJsonBuilder` を参照している。
  - Runtime/Windows/Clipboard/WindowsClipboardJsonBuilder.cs:18 は、削除される `AndroidClipboardJsonBuilder` を参照している。
  - Runtime/Clipboard/IosClipboardOperationResult.cs:15 は、改名前の `ClipboardOperationResult` を参照している。
  - いずれも `<c>` の地の文なので、コンパイルでは検出されない。
- 修正: 動作ロジックの変更ではなく、存在する新名への文書参照の修正として、この 3 箇所を変更対象に含める。

#### X1-4
- 分類: A1: 実装で気づけない
- 場所: 設計 64 行の agent-rules 更新対象
- 問題: `common.md` と、該当記述が実際には無い `testing.md` だけを挙げている。改名後に虚偽になるワークフローが漏れている。
- 証拠:
  - agent-rules/workflows/design-feature/workflow.md:52 は `IconConfiguration` を共通ユーティリティの例として挙げている。
  - review-document/workflow.md:46 は旧型と「既知の逸脱 11 件」を現存するものとして扱っている。
  - review-implementation-feature/workflow.md:67 と review-implementation-sample-scene/workflow.md:62 も同様。
- 修正: 上記 4 ワークフローを変更対象に追加する。解消済みの逸脱を前提にした検査や例示は、削除するか更新する。

#### X1-5
- 分類: A1: 実装で気づけない
- 場所: 設計 8、63、67〜81 行
- 問題: 「CHANGELOG に書く」とあるが、リポジトリに CHANGELOG ファイルは無い。実際の公開先である GitHub Release notes に破壊的変更を書く要件も無い。
- 証拠:
  - package.json:13 の `changelogUrl` は GitHub Releases を指す。
  - release workflow（agent-rules/workflows/release/workflow.md:70）は `Features/Fixes/Documentation/Chores` の分類だけで、`Breaking Changes` も移行ガイドへのリンクも要求していない。
  - S4-5 で追加する計画は AAR の照合だけ。
- 修正: GitHub Release notes に `Breaking Changes` 節を必須で設ける。載せる内容は次のとおり。
  - 15 型の改名
  - public な builder 2 つの削除
  - Android 名前空間の変更
  - Mono と ARMv7 の非対応
  - Gradle 設定手順の変更
  - 移行ガイドへのリンク
  ローカルに CHANGELOG を新設するなら、具体的なパスを指定する。

#### X1-6
- 分類: A1: 実装で気づけない
- 場所: 設計 73 行「API の docs」
- 問題: `docs/1.13.0` を API docs の出力先としているが、引用した 2 スクリプトは別の成果物を生成する。両方を実行しても、API docs は `docs/1.13.0` に公開されない。
- 証拠:
  - Documentation~/build-docs.sh:10 は `Documentation~/api` と `Documentation~/site` を生成する。
  - scripts/publish_docs.sh:44 は `manual/1.13.0` を `docs/1.13.0` へコピーするだけ。
  - Documentation~/docfx.json:24 は `allowCompilationErrors: true` なので、API が欠落しても成功し得る。
- 修正: API サイトの公開先を確定し、コピー手順を追加する。生成後の manifest と toc について、次を検査する。
  - 新 15 型がある。
  - 旧 15 型は無い。
  - Mac 型は `Runtime.Dialog` にある。
  - Android 型は `Runtime.Android.*` にある。

#### X1-7
- 分類: A2: 実装で気づける
- 場所: 設計 64〜65 行
- 問題: 参照の置換を「サンプル（`Runtime/UI/`）」に限定しており、macOS のサンプルが漏れている。
- 証拠: Assets/Scripts/Runtime/UI/macOS/Dialog/MacDialogManagerTest.cs:6 が旧名前空間を import している。51〜58 行で `IconConfiguration` と入れ子 enum を使っている。macOS target のコンパイルで表面化する。
- 修正: このファイルの `using` と型参照を、変更一覧に追加する。

#### X1-8
- 分類: B: 検証手段
- 場所: 設計 65、87、98 行
- 問題: 「4 つの build target でコンパイル」と書くだけで、コマンド、profile、成功判定、サンプルを含める方法が無い。
- 証拠: agent-rules/coding-rules/testing.md:376 は、Player ビルドの自動化が Android と Windows だけで、iOS と macOS は未整備だと明記している。既存の Android スクリプトは、他の 3 target を検証しない。
- 修正:
  - Android は通常 profile と `--as-package-user` で行う。
  - 他の 3 target は Unity batch 実行で、有効な `-buildTarget` を指定する（Windows は既存スクリプトと同じ `Win64`）。あわせて `-runTests -testPlatform EditMode` などを具体化する。
  - 合格条件は、終了コード、結果 XML、`error CS` と `CompilerError` が無いこと、対象アセンブリとサンプルがコンパイルされることにする。

#### X1-9
- 分類: B: 検証手段
- 場所: 設計 32 行、V-S4-3
- 問題: 「残った `extra_*` キーを無視する」という新しい契約の検証が無い。V-S4-3 は、キーを削除した実ファイルしか使わない。
- 証拠: Editor/Build/PreBuildProcessor.cs:220 は現在、片方だけの `extra_*` で例外を投げる。Tests には `AndroidPin` や `extra_*` の解析テストが無い。
- 修正: EditMode テストを必須にする。`extra_dist_version` だけ、`extra_aars` だけ、両方ありの 3 つの fixture が、すべて無視される。選ばれるのは primary の 2 AAR だけ、という内容にする。

#### X1-10
- 分類: B: 検証手段
- 場所: S4-1 後の Android Player テスト
- 問題: 旧 AAR を撤去しても、test seam の存在で 1.x と C ABI の期待値を切り替える二重オラクルが残る。seam が欠落したり改名されたりすると、テストが 1.x の期待値へ黙ってフォールバックする。
- 証拠:
  - Tests/PlayMode/AndroidShareTestSupport.cs:27
  - Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs:73
  - Tests/PlayMode/AndroidNotificationTestSupport.cs:53
  - 各所の `OnCApi ? 2.0 期待値 : 1.x 期待値` は、1.x が実行できなくなった後も残る。
- 修正: 期待値を C ABI 2.0.0 に固定する。履歴用に分岐を残す場合でも、fixture の setup で `OnCApi == true` を必須の assert にする。

#### X1-11
- 分類: B: 検証手段
- 場所: 設計 34 行、97 行
- 問題: 「dexdump か文字列」では判定対象が固定されておらず、クラス定義、互換用の文字列、manifest の参照を区別できない。
- 証拠: 2.0 側にも `android.unity.notification.*` の互換 action ID が残っている（Runtime/Android/Notification/AndroidNotificationCApi.cs:57）。旧 AAR の manifest には `android.library.*` の receiver と service が実在する。
- 修正: `classes*.dex` の class descriptor 定義 `Landroid/unity/` と `Landroid/library/` だけを `dexdump` などで検査するコマンドに固定する。merged manifest も `apkanalyzer manifest print` などで確認し、旧 component への参照が 0 件であることを見る。

#### X1-12
- 分類: C: 記述の整合
- 場所: 設計の「15 型」と、参照元の README
- 問題: 設計の 15 型は正しい。しかし親文書が「Runtime の 11 型」と書いており、ファイル数と型数が混同されたままになる。
- 証拠:
  - 実ソースは、Clipboard 7、Notification 3、Share 4、macOS Dialog 1 の計 15 のトップレベル public 型。
  - android-c-abi-2/README.md:82 は 11 型と記載している。
  - os-prefix-violations/README.md:112 も「11 件はすべて public 型」と記載している。実際は 11 の物理ファイル。
- 修正: 「11 ファイル、15 public 型」に統一し、両 README を更新対象に含める。

#### X1-13
- 分類: C: 記述の整合
- 場所: 設計全体の機械照合
- 問題: 必須の整合検査が失敗する。
- 証拠: `PYTHONDONTWRITEBYTECODE=1 python3 scripts/check_design_consistency.py <対象>` の結果は `FAIL cited sections exist` だった。8 行の `1.13.0` と 72 行の `1.12.0` を、節参照と誤認している。
- 修正: バージョン表記を、検査器が節参照と解釈しない形式に直す。または検査器を修正する。いずれも FAIL 0 件を確認する。

#### X1-14
- 分類: C: 記述の整合
- 場所: 設計 81 行
- 問題: AAR の配置を「足すときに確かめる」と未確定にしているが、指定された一次ソースですでに確定できる。
- 証拠: /Users/jonghyunkim/Desktop/native-toolkit/dist/1.13.0/android/ に対象の 2 AAR が実在する。親 README:38 にも同じ配置が書かれている。
- 修正: リリース照合のパスを `dist/<dv>/android/<aar>` に確定する。

確認済みで指摘なし:
- 改名対象の 15 public 型に数え漏れは無い。
- `Samples~/NativeToolkitExampleScene.unity`、README の 3 言語、`apiFilter.yml` には、旧 15 型の直接参照が無い。
- package 側の `.androidlib` は空で、独立した manifest や proguard のファイルも無い。
- 旧 2 AAR の `proguard.txt` は空。旧 manifest、class、resource は AAR に内包されているので、AAR 本体と `.meta` の削除で撤去される。
- 設計が引用する次の行は、現物と一致する。PreBuildProcessor.cs:100-105 / 186-188 / 211-227、AndroidClipboardCApi.cs:103、IosShareJsonBuilder.cs:12、WindowsClipboardJsonBuilder.cs:18、MacDialogManager.cs:4。

A1 の件数: 6 件（X1-1 から X1-6）。