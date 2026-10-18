# レビュー結果

- 日付: 2026-10-13
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v6.md
- 機能名: android-c-abi-2（段 4）
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 5）と Codex（ラウンド 5）を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力。文言には手を加えていない（見出しの段を下げ、先頭の題を外した）
- A1: サブエージェント 1 件（S5-1）、Codex 0 件。止める基準（両方の A1 が 0）には達していない。打ち切りの上限（5 ラウンド）に達した
- S5-1 の裏取り（メインのエージェント、2026-10-13）: `Resources/BuildPipeline/Compilation/ApiUpdater/APIUpdater.Framework.dll` の UTF-16 の文字列に「cannot be used to request the 'API Updater' to rename a type」「Use a [Obsolete()] instead」があることを確かめた（IL の判定の順は確かめていない）

---

## 1. サブエージェント（Claude）

リポジトリのファイルは変更していません。一時ファイル（3.4 の検査を写した Python、Unity の Updater の DLL を Mono.Cecil で読む IL スキャナ）は、すべて scratchpad に置きました。

**結論: A1 が 1 件あります（S5-1）。止める基準（A1=0）には達していません。** 新しい指摘は 10 件で、内訳は A1 1、B 3、C 6 です。前のラウンドの指摘 23 件は、すべて解決しています。

---

### 1. 前のラウンドの指摘の判定

| ID | 判定 | 根拠（v6 の行） |
|---|---|---|
| S4-1 | 解決 | v6:134。「段を問わず、同じ段以上の次の見出しまで」になり、index の `## Changes in 1.13.0 (Android)`、`## 1.13.0 での変更（Android）`、`## 1.13.0 변경 사항 (Android)`（v6:168）が入る |
| S4-2 | 解決 | v6:223-233（3 段、`git archive \| tar -x`、(e) の言い換え、(c) は見込みとして記録）。ただしビルドターゲットの穴が新たに見つかった（S5-2） |
| S4-3 | 解決 | v6:19、92。ただし根拠の言い方に弱さが残る（S5-1 の末尾） |
| S4-4 | 解決（release の側） | v6:13、241。commit-msg の push は漏れている（S5-4） |
| S4-5 | 解決 | v6:247 |
| S4-6 | 解決 | v6:261（S4-3 のコミットの前に V-S4-2）、v6:263-264（レビューを先にし、その後に verified を書く） |
| S4-7 | 解決 | v6:28、192、256（S4-0 に移した） |
| S4-8 | 解決 | v6:104。今のファイルの `grep -niE 'extra'` が当たるのは :104、:186、:197-235、:416、:424-425。消した後は :104 と、直した Android の注記だけになる |
| S4-9 | 解決 | v6:215 |
| S4-10 | 解決 | (a) v6:168、(b) v6:162、(c) v6:167、(d) v6:153、(e)(f) v6:183 |
| S4-11 | 解決 | v6:118（`testing.md:406-407`）、v6:136（`:212`）、v6:207（`:82-89`）。どれも実ファイルと一致した |
| S4-12 | 解決 | v6:255 |
| S4-13 | 解決 | v6:243 |
| S4-14 | 解決 | v6:132。除き方の解釈に穴がある（S5-6） |
| X4-1 | 解決 | v6:89 |
| X4-2 | 解決 | v6:130。ただし第 1 引数を一律 `true` で照らすところは、S5-1 で直す必要がある |
| X4-3 | 解決 | v6:132。式を 1.12.0 の文書に流すと、`notification{,.ja,.ko}.md:621` の 3 件にだけ当たった |
| X4-4 | 解決 | v6:224 |
| X4-5 | 解決 | v6:233 |
| X4-6 | 解決 | v6:207-208。ただし取る実行の範囲に漏れがある（S5-3） |
| X4-7 | 解決 | v6:263-264 |
| X4-8 | 解決（A1 は解消） | v6:153-164（共通の破壊的変更を 4 ページに置く）、v6:166-168（index は動作環境と案内だけ）、v6:179（リリースノートは各ページへ直接リンクする） |
| X4-9 | 解決 | v6:214、259 |

---

### 2. v6 の新しい指摘

#### S5-1 ｜ A1 ｜ 高 ｜ 2.3 の `[MovedFrom(true, …, sourceClassName: …)]`（v6:88）と、3.4 の照合（v6:130）
- **問題**: 改名した 37 型に、`autoUpdateAPI=true` と `sourceClassName` を同時に渡しています。Unity 6000.4.2f1 の Script Updater は、この組み合わせを見ると例外を投げます。その結果、Updater の実行そのものが失敗します。
  - 5 型の名前空間も直らなくなります。
  - 利用者のプロジェクトで同じ実行にかかる、ほかの API の自動更新も止まる見込みです（要確認）。
  - コンパイルもテストも通るので、実装の段階では気づけません。
  - V-S4-4 の (a) が「直らない」と出ても、2.3 の既定（「手で直す」）にそのまま収まってしまいます。原因が見過ごされたまま出荷される形です。
- **根拠**（`Resources/BuildPipeline/Compilation/ApiUpdater/` を Mono.Cecil で読んだ）:
  - `APIUpdater.Framework.dll` の `ConfigurationCollector::CollectTypeIfAppropriated` は、次の順に判定する。
    - 第 1 引数が false なら、何もしないで戻る（IL_0077）。
    - 第 4 引数（className）が null でなければ（IL_014f）、例外を投げる。`sourceAssembly` が null のときは `ArgumentException("[MovedFrom] attribute cannot be used to request the 'API Updater' to rename a type. Use a [Obsolete()] instead to rename '…'")`（IL_020e-0261）。
    - 例外を投げる前に、名前が本当に変わったかは比べていない。
  - 呼び出しの経路は `ScriptUpdater.Program::Main` → `ConfigurationProviderFor` → `ComputeConfigurationProvider` → `ObsoleteAttributeBasedConfigurationFactory..ctor` → `ScanConfigurationsFrom` → `CollectFrom` → `CollectTypeIfAppropriated`。
    - 途中に catch は無い（finally だけ）。
    - `Main` の `catch (Exception)` が stderr に書き、終了コード 130 を返す。これは `CSharpUpdater` を作る前の段階。
  - `ApiUpdater.MovedFromExtractor.dll` の `ShouldUpdate` は、第 1 引数をそのまま返す。そのため `true` の型は「更新が要るかもしれない型」に載り、旧名を使うスクリプトがあると Script Updater が呼ばれる。
  - 利用者のどの範囲（アセンブリ）まで止まるかは、要確認。
- **提案**:
  1. 37 型は `[MovedFrom(false, sourceNamespace: "…", sourceClassName: "<旧名>")]` にする。5 型は `true` のまま。利用者の決定「`[MovedFrom]` を付ける」は変わらない。
  2. 3.4 の照合（v6:130）の第 1 引数の期待を、5 型は `true`、37 型は `false` に分ける。
  3. V-S4-4 の段 2 に次の記録を足す。
     - Editor.log に「[MovedFrom] attribute cannot be used」と Script Updater の失敗が無いこと。
     - (a) で 5 型の名前空間が直ること（直らなければ原因を記録する）。
  4. `false` でも `[SerializeReference]` のデータが新しい型に移るかは、(d) で確かめる（要確認）。
  5. 2.3 の根拠（v6:92）を、上の IL の事実に置き換える。
     - 今の「`sourceClassName` の文字列が無い」は弱い根拠。`MovedFromExtractor` には `sourceTypeName` の文字列がある。
     - 「直らない」は見込みではなく、「Updater が拒否する」が事実になる。

#### S5-2 ｜ B ｜ 中 ｜ V-S4-4 の段 1 のビルドターゲット（v6:223-230）
- **問題**: 段 1 は旧版のパッケージで、ガードの無い `Ns5.cs`（`AndroidClipboardManager` など）のコンパイルが通ることを求めています。しかしタグ `1.12.0` では、次の型が `#if UNITY_ANDROID` の中にあります。
  - 4 つの Manager
  - `NotificationResult`、`NotificationActionResult`、`NotificationReceivedResult`

  ビルドターゲットを指定しない batchmode（新しいプロジェクトの既定は Standalone）では、これらの型が存在しないので、段 1 がコンパイルエラーで止まります。
- **根拠**: `git show 1.12.0:…/Runtime/Clipboard/AndroidClipboardManager.cs` の 3 行目が `#if UNITY_ANDROID`（Dialog、Share、Notification の Manager と Notification*Result も同じ）。HEAD は `#if UNITY_ANDROID || UNITY_EDITOR`。
- **提案**: 3 段とも `-buildTarget Android` を渡す、と書く。`Ns5.cs` は Android ターゲットでの「`#if UNITY_ANDROID` だけのコード」を代表する、と位置づける。あわせて、`$W/proj` の git に `Library/`、`Temp/`、`Logs/`、`UserSettings/` の ignore を置く（差分を `Assets/` に絞るため）。

#### S5-3 ｜ B ｜ 低〜中 ｜ `--pull-apk` が取る実行（v6:208）
- **問題**: V-S4-1 は `--as-package-user` で走らせます。このときスクリプトは、実機の実行を 3 回します: `Android`、`Android-retention`、`Android-package-user`（`verify_unity_android.sh:286,290,295`）。しかし v6 が APK を取るのは前の 2 つだけです。
  - 前の 2 つは Android™ Profile の実行です。`PreBuildProcessor` がピンどおりに AAR を写し、ピンに無い AAR を消してから組みます（`PreBuildProcessor.cs:438-454`）。
  - 同梱の `Plugins/Android` そのもので組むのは、`Android-package-user` だけです。「出荷する形の APK に 1.x のクラスが無い」を直接見られるのは、この実行です。
  - また最後の実行の後は、次の頭のアンインストールが無く、`:300` とトラップがアンインストールします。v6 の「次の実行の頭のアンインストールの前」という時点の決め方では、この APK を取れません。
- **提案**: 3 つの実行すべてを取る。最後の実行は `:300` のアンインストールの前に取る、と書く。

#### S5-4 ｜ B ｜ 低 ｜ コミットの手順と push（v6:13、255-268）
- **問題**: `agent-rules/workflows/commit-msg/workflow.md:51` の「実行する」は、`git add .` → `git commit` → `git push` です。
  - v6:13 の「手順に push があるところ」の一覧に、この行がありません。7 章ではどのコミットもこの手順を通るので、push が最も多く起きる場所です。
  - `git add .` では、S4-0 の 3 つのコミット（docs(artifact)、docs(agent-rules)、test(android)）に分けられません。さらに、v6:255 が「含めない」とする `AndroidNotificationJsonBuilder.cs` の未コミットの変更（今の作業ツリーに `M` で残っている）も、1 つ目のコミットに入ってしまいます。
  - commit-msg の確認でファイルの一覧は見せるので、気づける可能性はあります。
- **提案**:
  - v6:13 の一覧に `commit-msg/workflow.md:51` を足す。
  - 7 章に「各コミットはパスを指定して `git add` する（`git add .` は使わない）。push はしない」と書く。
  - あわせて、v6:13 の「`:126` の `gh pr create` はブランチの push が前提」は誤りなので直す。ステップ 6 の develop は、ステップ 5 のマージで remote にあり（`:125` は pull だけ）、push は要らない。止まる場所は `:103` の前、`:133`、`:145` で足りる。

#### S5-5 ｜ C ｜ 低 ｜ 7 章のレビューの後のやり直し（v6:237、263）
- ステップ 5 は、直しが S4-1〜S4-3 に及んだら V-S4-2 だけをやり直す、としています。しかし次のことが書かれていません。
  - 3.4 の S4-3 の 2 行（V-S4-5。旧名と `[MovedFrom]` の照合）のやり直し。
  - S4-3 が変わったときの V-S4-4 のやり直し（`git archive <S4-3 のコミット>` が古くなる）。
  - 直しのコミット。
- S5-1 を直すと、この経路を必ず通ります。
- v6:237 は「各段の実装の後に、実装のレビューを回す」としています。しかし 7 章には、S4-0 の `--pull-apk`（陽性の対照に使う道具）と S4-4（文書）のレビューの段がありません。
- **提案**: ステップ 5 に「V-S4-5（S4-3 の行）、V-S4-2、S4-3 が変わったら V-S4-4 もやり直し、直しを 1 コミットにする」と書く。S4-0 のスクリプトと S4-4 のレビューを置くか、置かない理由を書く。

#### S5-6 ｜ C ｜ 低 ｜ 3.4 の 1.x のクラス名の除き方（v6:132）
- `android\.(?:library|unity)\.[a-z]+\.[A-Za-z.]*[A-Z]` が `android.unity.notification.ACTION_BODY_TAP` に当たったとき、一致した文字列は `android.unity.notification.ACTION` です（`_` を含まない）。
- そのため、「一致した文字列が `android.unity.notification.ACTION_` で始まるか」で除くと、例外が効きません。
- **提案**: 「一致した位置から始まる本文が `android.unity.notification.ACTION_` で始まるものを除く」と書く。または、式に否定先読み `(?!ACTION_)` を入れる。

#### S5-7 ｜ C ｜ 低 ｜ S4-1 の破壊的変更の扱い（v6:29、161、192）
- 1 章の表では、S4-1 の破壊的変更は「なし」です。一方で 4.2 は、「1.x の AAR を同梱しなくなった。その中のクラスを直接使っていたコードは動かない」を、共通の破壊的変更として 4 ページに書きます。
- 4.4 が件名に `!` を付けるのは S4-2 と S4-3 だけです。
- リリースノートは見出しの条件で Breaking Changes が出るので、実害は小さいです。それでも表と件名の決まりを揃えるべきです（S4-1 にも `!` を付ける。または表を「利用者に見える変更あり」にする）。

#### S5-8 ｜ C ｜ 低 ｜ 1.12.0 の Windows の移行の節の一文（v6:175）
- 「The Android, iOS and macOS types keep their namespace.」は dialog だけでなく、`clipboard.md:2212` と `notification.md:912` にもあります。3 言語で計 9 か所です（`dialog.md:835` を含む）。
- 3.4 の検査では拾えません。
- **提案**: v6:175 の「例」を、9 か所の一覧にする。

#### S5-9 ｜ C ｜ 低 ｜ リリースノートのリンクの形（v6:179、191）
- GitHub Release の本文から `#changes-in-1130` だけでは届きません。README は相対リンク（`README.md:178-180` の `docs/latest/…`）です。
- **提案**: 絶対 URL の形を決める（例: タグ `1.13.0` の `docs/1.13.0/share.md#changes-in-1130`）。

#### S5-10 ｜ C ｜ 低 ｜ 細部
- (a) 7 章のステップ 6（v6:264）は「この後に `Plugins/Android` が変わったら、V-S4-2 をやり直す」としています。しかしステップ 6 自身が `Plugins/Android/VERSION.txt` を変えます。「`verified_native_toolkit_commit` の行とその注記を除く」と書く。
- (b) v6:102 の「写す処理、ログの追加の部分（`:416`、`:424-425`）」では、`:424-425` は写す元を足す処理で、ログではありません。「写す処理（`:424-425`）、ログ（`:416`）」と書く。
- (c) V-S4-4 の (a)〜(e) をどこに記録するか（実装結果のどの章か）が書かれていません。
- (d) 6 章のチェックリストの `rev-parse` の前に、`git -C ../native-toolkit fetch --tags` が要ります。手元にタグが無いと、❌ が誤って出ます。

---

### 3. 確かめたこと（指摘なし）
- **行番号**: 次の引用は、実ファイルと一致しました。
  - `AndroidNotificationManager.cs:63,68`、`AndroidNotificationCApi.cs:57,60`
  - release の `:103`、`:126`、`:133`、`:145`、write-manual の `:17`、commit-msg の `:23`
  - `common.md:45,455-462,518`、`testing.md:406-407`、各 workflow の `:46/:67/:62`、design-feature の `:52,54,57`
  - `artifact/README.md:59,65`、`os-prefix-violations/README.md:112`
  - ほかの OS のコメント 3 か所、`AndroidClipboardCApi.cs:103`、`AndroidClipboardPayloads.cs:10-11`
  - `PreBuildProcessor.cs` の `:100-105`、`:136-160`、`:145-160`、`:186-188`、`:197-235`、`:416`、`:424-425`、`:447`、`:450-454`
  - `VERSION.txt` の `:11-16`、`:15-16`、`:22-24`、`:30-32`
  - `verify_unity_android.sh:82-89`、`verify_manual.sh:212`、`publish_docs.sh:44-57,86-93`、`.gitignore:86-88`
  - マニュアルの `dialog.md:19-20,37-39,829-831,835`、`clipboard.md:2206-2208`、`notification.md:619-624,621,904-908`、`index.md:7,185-191`
  - `Documentation~/index.md:7,95-99,137-147`、`README.md:5`、`AndroidGradleDependencies.cs:46-52`（7 行）
  - 内容が誤っていたのは `:126` の前提だけ（S5-4）。
- **型と数**: `Runtime/Android` の public 型は 44 で、Runtime 全体に同じ名前の型は無い。ビルダーのテストは Share 21 本、Clipboard 16 本。
- **旧名の出どころ**: 1.12.0 のマニュアルにある 37 の旧名と、2 つのビルダーの名前は、すべて `## Android` の節の中にある（clipboard 24、notification 40、share 18。各言語）。`Documentation~` と README には無い。agent-rules で当たるのは `common.md:459-461` と `review-document/workflow.md:46` だけ。
- **旧い名前空間**: Android の節の外の地の文や、ガードの無いブロックに、旧い Android の名前空間は無い（3.4 の組の検査で足りる）。
- **4 ページの分担**: 4 ページの `## Android` の最初の小節に移行の節を置ける。日本語の目次のアンカーには前例がある（`dialog.ja.md:20` の `#1120-での変更`）。
- **道具**: Unity 同梱の `dexdump`（build-tools 36.0.0）、`apkanalyzer`（cmdline-tools 16.0）、OpenJDK は実在する。テストのプロジェクトの `androidlib` の manifest に `android.unity`、`android.library` は無い。
- **`Configure Gradle Project`**: `AndroidConfigureProjectWindow` は HEAD に無く、タグ 1.12.0 にはある。段 3 で消えたことと合う。

---

**A1: 1 件（S5-1）。止める基準（A1=0）には達していません。** S5-1 を直すときは、3.4 の照合（第 1 引数）と V-S4-4 の記録項目も一緒に直す必要があります。B の 3 件（S5-2〜S5-4）も、実装の前に直すことを勧めます。

関連するファイル:
- /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v6.md
- /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/Resources/BuildPipeline/Compilation/ApiUpdater/APIUpdater.Framework.dll（`ConfigurationCollector::CollectTypeIfAppropriated`）
- /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/Resources/BuildPipeline/Compilation/ApiUpdater/ScriptUpdater.dll（`Program::Main`）
- /Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_android.sh（`:286-300`）
- /Users/jonghyunkim/Desktop/unity-native-plugin/agent-rules/workflows/commit-msg/workflow.md（`:51`）

---

## 2. Codex

### Part 1：前回指摘の判定

#### X4-1〜X4-9

| ID | 判定 | 根拠 |
|---|---|---|
| X4-1 | 解決 | 属性を置く各ファイルへ `using UnityEngine.Scripting.APIUpdating;` を追加すると明記された（design-v6.md:89）。照合条件にも含まれる（同:130）。 |
| X4-2 | 解決 | 42 型について、第1引数 `true`、`sourceAssembly` 不在、旧名前空間、37 型の旧名、5 型の `sourceClassName` 不在を照合する（design-v6.md:130）。 |
| X4-3 | 解決 | 正規表現が `android\.(?:library\|unity)...` に修正され、公開中の `ACTION_` 値も除外された（design-v6.md:132）。 |
| X4-4 | 部分解決 | `git archive … \| tar -x` と参照先は具体化された。ただし `tar -x -C $W/old` / `$W/new` の前に両ディレクトリを作る手順がない（design-v6.md:224）。`tar -C` は対象ディレクトリが存在しないと失敗する。 |
| X4-5 | 解決 | `$W/proj/Assets` の期待差分と、元作業ツリー・新版パッケージコピーの不変条件が分離された（design-v6.md:231-233）。 |
| X4-6 | 部分解決 | 指定先を空または不存在に限定し、実行別・APK 別に分けた（design-v6.md:207-208）。ただし pull した APK と展開先が同じパスになり得る（X5-2）。 |
| X4-7 | 解決 | 実装レビューを `verified_native_toolkit_commit` の記録より前に移し、コード・テスト修正後の V-S4-2 再実行も必須化した（design-v6.md:263-264）。V-S4-4 の再実行漏れは X5-7。 |
| X4-8 | 解決 | 共通の4項目を全機能ページに同文で置き（design-v6.md:160-164）、リリースノートから各機能ページへ直接リンクする（同:179,191）。 |
| X4-9 | 解決 | 基本順序が「実装→status 保存→検証→比較→コミット→`git ls-files`」に固定された（design-v6.md:214）。ただし V-S4-3 を挿入する位置は 7 章と不一致（X5-5）。 |

#### S4-1〜S4-14

| ID | 判定 | 根拠 |
|---|---|---|
| S4-1 | 解決 | 許可見出しを段に依存せず、指定文言で始まる見出しから同段以上の次見出しまでとした（design-v6.md:134）。manual の index の `## Changes in 1.13.0 (Android)` も含まれる。1.x クラス名の除外漏れは X5-1。 |
| S4-2 | 解決 | Updater、エラー除去、アセット再読込の 3 段に分離された（design-v6.md:225-233）。ガード付きコードの Player 側確認漏れは X5-4。 |
| S4-3 | 解決 | `AffectsAPIUpdater` を根拠から外し、Updater 実体の観測に基づく「見込み」と V-S4-4 での要確認に統一した（design-v6.md:90-94）。 |
| S4-4 | 解決 | feature/tag の push は利用者が行い、エージェントが止まる位置を明記した（design-v6.md:13,241）。一次資料は release/workflow.md:101-106,125-133,139-146。 |
| S4-5 | 解決 | 比較範囲を top-level AAR と `m2` に絞った（design-v6.md:247）。ただし `m2` 全体はまだ過剰（X5-6）。 |
| S4-6 | 解決 | S4-3 のコミット前に V-S4-2 を置き（design-v6.md:261）、実装レビュー後の修正にも再実行を要求した（同:263）。 |
| S4-7 | 解決 | `commit-msg` と release workflow の変更が S4-0 に移った（design-v6.md:28,187-192,256）。 |
| S4-8 | 解決 | 大文字小文字を無視する `grep -niE 'extra'` とし、期待する 2 か所も明記した（design-v6.md:104）。 |
| S4-9 | 解決 | AAR は毎回上書きコピーされ、status 一致が示す内容も「同じバイトが写された」に修正された（design-v6.md:215）。一次資料は PreBuildProcessor.cs:450-454。 |
| S4-10 | 解決 | 接頭辞表現、依存「計 7 つ」、3 言語の requirements 見出し、移行節の配置、Documentation の削除範囲・紹介文が明記された（design-v6.md:153,162,167-170,183）。 |
| S4-11 | 解決 | `testing.md:406-407`（design-v6.md:118）、`PACKAGE_TYPE` の `:212`（同:136）、引数解析 `:82-89`（同:207）へ修正された。 |
| S4-12 | 解決 | 「その時点の設計とレビューのすべて」という将来版も含む表現になり、XML コメント除外文も修正された（design-v6.md:255）。 |
| S4-13 | 解決 | `VERSION.txt` のキー一覧へ「ビルドは読まず、リリースチェックリストが読む」と追加する（design-v6.md:243）。現行一覧は VERSION.txt:11-16。 |
| S4-14 | 解決 | `ACTION_` で始まる公開アクション ID を 1.x クラス名検査から除外した（design-v6.md:132）。 |

解消 21 件、一部解消 2 件（X4-4、X4-6）、未解消 0 件。

### Part 2：v6 の新規指摘

#### X5-1｜B｜3.4 の許可節から 1.x クラス名が漏れている
- 場所: design-v6.md:132-134,160-162
- 問題: 3.4 は `android.unity.*` / `android.library.*` を 0 件にする。一方、許可節の内容として列挙されるのは「旧名、削除ビルダー、旧名前空間、旧手順」だけである。各機能ページには「1.x のクラスを直接使うコードは動かない」と書く要件があるため、正しく書いても検査が失敗する。
- 証拠: クラス名の検査は design-v6.md:132、許可内容は同:134、必須の共通説明は同:160-162。
- 修正: design-v6.md:134 の許可内容に「1.x のクラス名」を明記する。許可節内では検査語を一貫して除外する。

#### X5-2｜B｜APK の pull 先と展開先が同一パスになる
- 場所: design-v6.md:208
- 問題: APK を `<dir>/<実行名>/` へ pull すると、ファイルは `<dir>/Android/base.apk` になる。展開先の `<dir>/<実行名>/<APK の名前>/` も `<dir>/Android/base.apk/` となり、ファイルとディレクトリが衝突する。
- 証拠: pull 先と展開先の両方が design-v6.md:208 にある。
- 修正: pull 先を `<run>/apks/<basename>.apk`、展開先を `<run>/expanded/<basename-without-ext>/` のように別階層にする。

#### X5-3｜B｜`--as-package-user` の APK が pull・検査対象に入らない
- 場所: design-v6.md:206-212
- 問題: `--as-package-user` は 3 つ目の実機実行 `Android-package-user` を行う。設計の pull 対象は `Android` と `Android-retention` の 2 つだけである。コミット済み `Plugins/Android` を使う package-user の APK で、旧クラス混入を検査できない。
- 証拠: scripts/verify_unity_android.sh:293-297。設計の列挙は design-v6.md:208。
- 修正: `run_device_tests` の全呼び出しを共通の pull 処理に通し、`Android-package-user` も保存して同じ DEX・manifest 検査を行う。

#### X5-4｜B｜V-S4-4 の段 2 はガード付きコードの Player エラーを実測しない
- 場所: design-v6.md:230-232
- 問題: `Guarded.cs` について「Android Player ビルドで初めてエラーになる」と記録する。しかし 3 段の手順には Player ビルドがなく、Editor で差分がないことしか観測できない。
- 証拠: design-v6.md:230（ガード付きスクリプト）、同:231（段 2 の判定）、同:232（段 3 は Editor の `-executeMethod` による読込みのみ）、同:219（`UNITY_ANDROID && !UNITY_EDITOR` は実機実行でしかコンパイルされない）。
- 修正: 段 2 の後、`Guarded.cs` を残した状態で Android Player のスクリプトコンパイルを行い、旧型による失敗を記録する。その後、段 3 用のエラースクリプトを削除する。

#### X5-5｜C｜V-S4-1b と V-S4-3 の位置が 6 章と 7 章で不一致
- 場所: design-v6.md:214,259
- 問題: V-S4-1b の固定順は status・ログ確認の直後にコミットする。7 章は V-S4-3 も見てからコミットする。V-S4-3 の挿入位置が一致しない。
- 証拠: design-v6.md:214 と同:259。
- 修正: design-v6.md:214 を「…status・ログ確認→V-S4-3→コミット→`git ls-files`」に統一する。

#### X5-6｜B｜チェックリストが使用しない `m2` 成果物まで比較する
- 場所: design-v6.md:247
- 問題: 説明ではパッケージが参照するのは `m2` の `.module` だけとしているが、コマンドは `dist/<dv>/android/m2` 全体を比較する。POM、AAR の複製、checksum、`maven-metadata.xml` だけが変わっても不一致になり、リリースを誤って止める。
- 証拠: design-v6.md:247。`/Users/jonghyunkim/Desktop/native-toolkit/dist/1.13.0/android/m2/` には `.module` 以外に `.pom`、`.aar`、`.md5`、`.sha1`、`.sha256`、`.sha512`、`maven-metadata.xml` がある。パッケージ側のテスト対象も `.module` である（VERSION.txt:18-20）。
- 修正: top-level の 2 AAR と、必要な 2 つの `.module` だけを明示的な pathspec で比較する。

#### X5-7｜B｜レビュー修正後に V-S4-4 を再実行しない
- 場所: design-v6.md:262-264
- 問題: V-S4-4 は 7 章のステップ 4、実装レビューはステップ 5 である。レビューで `[MovedFrom]`、型名、名前空間、Payload、API Updater 関連を修正しても、再実行が必須なのは V-S4-2 だけである。移行結果の検証が修正前の実装に対するものになり得る。
- 証拠: design-v6.md:262（ステップ 4）、同:263-264（レビューと V-S4-2 再実行）。
- 修正: 実装レビューを V-S4-4 より前へ移す。または、レビュー修正が S4-3、`[MovedFrom]`、公開型、Payload に及んだ場合は V-S4-4 も再実行する。

#### X4-4 の残件（X5-8 として扱う場合）
- 分類: B
- 場所: design-v6.md:224
- 問題: `tar -x -C $W/old` / `$W/new` の前に `mkdir -p` がない。
- 修正: `mkdir -p $W/old $W/new` を手順に加える。

補足: `python3 scripts/check_design_consistency.py <design-v6>` は終了コード 0 だった。ただし V-S4-*、件数、擬似コードは SKIP で、上記の検証穴は検出対象外である。ファイルと Git の状態は変更していない。

**A1 件数: 0 件**（B 6 件＋X4-4 残件、C 1 件）
