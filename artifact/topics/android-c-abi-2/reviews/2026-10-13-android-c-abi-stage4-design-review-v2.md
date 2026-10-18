# レビュー結果

- 日付: 2026-10-13
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v3.md
- 機能名: android-c-abi-2（段 4）
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 2）と Codex（ラウンド 2）を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力。文言には手を加えていない（見出しの段を下げ、先頭の題を外した）
- A1: サブエージェント 2 件（S2-1、S2-2）、Codex 1 件（X2-1）

---

## 1. サブエージェント（Claude）


対象は `artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v3.md` です。読むだけで、ファイルは変えていません。

**結論: A1 は 2 件（S2-1、S2-2）です。止める基準（A1=0）には達していません。**

検証の中で、AAR を `<scratchpad>/aar` に展開しました。一時ファイルで、リポジトリの外です。

---

### 1. 前のラウンドの指摘の判定

| ID | 判定 | 根拠 |
|---|---|---|
| S1-1 | 解決 | 2.1 に Payload 23 型を含む 37 型を載せた。「既知の逸脱」の表は消さずに書き換える（v3:53）。ただし「残す」の書き方は事実と違う（S2-8） |
| S1-2 | 範囲の外 | `IconConfiguration` は今回扱わない（利用者の判断） |
| S1-3 | 解決 | 3.2。`OnCApi` を 6 ファイルで数え直すと 56 か所で、v3 と一致した |
| S1-4 | 解決 | V-S4-2。改名する型には `#if` のガードが無い。Android 以外のコードからの参照はコメントだけで、grep で確かめた |
| S1-5 | 解決 | 2.2 に、置き換える先と置き換えない先の 2 つの表がある。`Assets/` の分は範囲の外になった |
| S1-6 | 解決 | 4 章に「Changes in 1.13.0」の節と「Breaking Changes」を置いた。ただし中身が足りない（S2-1）、出す条件も決まっていない（S2-6） |
| S1-7 | 部分 | 3.4 で 37 の旧名は調べる。`Documentation~/index*.md` の Android Dialog の古い `using`（index.md:146〜481 の 6 か所）は調べない（S2-1、S2-9） |
| S1-8 | 解決 | 4 章（notification.md:619-624 を消す。移行の節にも書く） |
| S1-9 | 解決 | 4 章（Mono、Gradle の依存、既知の差分の出典 4 つ、Import の節） |
| S1-10 | 部分 | APK の作り方と dexdump の使い方は書いた。しかし 2 つの実行は独立しない（S2-3）。ツールの場所も書いていない（S2-4） |
| S1-11 | 解決 | 6 章。ただし `.module` の照合に穴がある（S2-5） |
| S1-12 | 解決 | 4 章。`.gitignore:86,88` と `publish_docs.sh` の記述が事実と合う |
| S1-13 | 部分 | #1〜3、5〜7、9 は解決。#4 は `ls` に androidlib が出るので部分（S2-3）。#8 の `common.md:518` は手つかず（S2-12） |
| S1-14 | 部分 | 2.3 で付けると決めた。ただし引数に `sourceNamespace` が無い（S2-1） |
| X1-1 | 解決 | 3.3（`.cs.meta` を対で消す） |
| X1-2 | 解決 | 2.2 と 3.4 |
| X1-3 | 解決 | 2.2 の「ほかの OS のファイル」。書き方の細部は S2-10 |
| X1-4 | 解決 | 2.2（workflow 4 本） |
| X1-5 | 部分 | X1-5 は「Android 名前空間の変更」を載せるよう求めた。v3 の移行の節とリリースノートの中身に、これが無い（S2-1） |
| X1-6 | 解決 | 4 章 |
| X1-7 | 範囲の外 | macOS のサンプルの改名は今回扱わない |
| X1-8 | 解決 | V-S4-2 |
| X1-9 | 解決（別の手段で） | 読む処理そのものを消し、grep で確かめる（3.1）。`ReadPinValues` は知らないキーを無視する（PreBuildProcessor.cs:136-160）ので筋が通る |
| X1-10 | 解決 | 3.2（`SetUp` に `Assert.IsTrue(OnCApi)` を置く）。6 ファイルとも `#if UNITY_ANDROID` と `!UNITY_EDITOR` の中なので、Editor では走らない |
| X1-11 | 解決 | 5 章。ツールの場所は S2-4 |
| X1-12 | 解決 | 2.2 で、両方の README を直す対象にした |
| X1-13 | 解決 | `check_design_consistency.py` で v3 を見て、FAIL は 0 |
| X1-14 | 解決 | 6 章。md5 9e068bc9… と c706a8a6… を手元で確かめた |

---

### 2. v3 の新しい指摘

#### S2-1 ｜ A1 ｜ 高 ｜ 2.1「名前空間は変えない」、2.3、4 章の移行の節、リリースノート
**問題**: 利用者から見た 1.12.0 → 1.13.0 では、Android の public 型の名前空間がすべて変わります。v3 は段 3-0 で済んだ前提で書いており、次の 3 つが抜けています。

- (a) 移行の節（4 章）とリリースノートの Breaking Changes の中身に、名前空間の変更が無い。
- (b) `[MovedFrom]` が `sourceClassName` だけ。旧名で調べたとき、API Updater は `Runtime.Android.<Feature>.<旧名>` を探す。これは利用者の手元には一度も無かった名前なので、何も直らない。
- (c) 改名しない 5 型も名前空間は動いた。`AndroidDialogManager`、`AndroidShareManager`、`AndroidClipboardManager`、`AndroidNotificationManager`、`AndroidNotificationJsonBuilder` で、どこにも書かれていない。

**根拠**:
- タグ `1.12.0` の名前空間（`git show 1.12.0:…` で確かめた）:
  - `ShareOperationResult` などの Share の型と `AndroidShareManager` は `JonghyunKim.NativeToolkit.Runtime.Share`
  - Clipboard の型は `Runtime.Clipboard`、Notification の型は `Runtime.Notification`、`AndroidDialogManager` は `Runtime.Dialog`
  - HEAD はすべて `Runtime.Android.<Feature>`。`git log 1.12.0..HEAD` は 29 コミットで、段 3-0（`44f206d`）はタグの後にある
- `common.md:404` は「名前空間の変更は公開 API の破壊的変更になるので、その版の既知の差分とマニュアルに書く」と定めている。
- 親 README:82 は「移行ガイドには、名前空間、…を書く」と書いている。
- 前例: `manual/1.12.0/dialog.md:835` は、Windows の「Changes in 1.12.0」の先頭に **Namespace** の項を置いている。
- `Documentation~/index.md` の Android Dialog の 6 か所が、`using JonghyunKim.NativeToolkit.Runtime.Dialog;` のまま。3.4 の名前空間の検査は `manual/1.13.0` しか見ないので、ここは捕まらない。
- 2.3 のとおりに V-S4-4 で確かめる場合、試しのスクリプトを今の名前空間で書くと PASS する。それでも実際の利用者には効かない（偽の PASS）。

**提案**:
- 2.3 の属性を `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>", sourceClassName: "<旧名>")]` にする。
- 改名しない 5 型にも `[MovedFrom(true, sourceNamespace: …)]` を付けるかを決める。付けない場合も、名前空間の変更は移行の節に書く。
- V-S4-4 の試しのスクリプトは、1.12.0 の書き方にする（例: `using JonghyunKim.NativeToolkit.Runtime.Clipboard;` と `ClipboardReadResult`、`AndroidClipboardManager`）。
- 移行の節とリリースノートに「Android の 42 型（改名した 37 と、残した 5）の名前空間が `Runtime.Android.<Feature>` に変わった」と書く。
- 3.4 の古い名前空間の検査の対象に、`Documentation~/index*.md` を足す。

#### S2-2 ｜ A1 ｜ 中 ｜ 4 章（マニュアルと `Documentation~`）
**問題**: 消えたメニュー「Tools → Native Toolkit → Android → Configure Gradle Project」を使う手順が、Getting Started に残っています。v3 は、移行の節に「Configure Gradle Project の削除」と書くだけで、この手順を直す行がありません。どの検査もこれを捕まえません。

**根拠**:
- 手順が残っている所: `manual/1.12.0/index.md:185`（ko も。画像 `ConfigureGradleProject.png` を付けている）と、`Documentation~/index{,.ja,.ko}.md:95`。
- `Editor/` と `Runtime/` に `Configure Gradle Project`、`AndroidGradlePatcher`、`AndroidConfigureProjectWindow` は 0 件。メニューはもう無い。
- 親 README:115 は「`Documentation~/index.md` の『Configure Gradle Project』を直す（段 4）」と明記している。
- `verify_manual.sh` は `Documentation~` も README も調べない。
- S1-8（manifest の例）と同じ種類の指摘で、前のラウンドはそれを A1 にした。

**提案**:
- 4 章の「マニュアル」と「`Documentation~`」に、この手順を消すか書き直すことを足す。不要になる画像の扱いも決める。
- 3.4 の検査に `Configure Gradle Project` と `android\.(library|unity)\.` を足す。

#### S2-3 ｜ B ｜ 中 ｜ V-S4-1b、V-S4-3
**問題**: v3 は、`git status` が clean な状態から `--as-package-user` を流せば独立すると考えています。しかし `--as-package-user` は、同じ起動の中で Android™ Profile の実行の後に流れます。

- 先に走る Android™ Profile の実行で、`PreBuildProcessor` がピンに無い AAR を消し、dist の AAR を写す。
- そのため、1.3.0 の 2 本の `git rm` を忘れていても、後の実行は PASS する。

V-S4-3 の「実行の後の `ls`」には、git で管理していない `com.jonghyunkim.nativetoolkit.androidlib` とその `.meta` も出ます。

**根拠**:
- `verify_unity_android.sh:18-21` と `:286-295`（`AS_PACKAGE_USER` は、既定の実行の後に足して走る）
- `PreBuildProcessor.cs:438-447`
- スクリプトが元に戻すのは `SIDE_EFFECT_PATHS`（`:112-116`）だけで、`Plugins/Android` は含まない
- `ls -la Plugins/Android` の実際の中身

**提案**: V-S4-1b と V-S4-3 の合否を次にする。
- スクリプトの後に、`git status --porcelain -- Packages/com.jonghyunkim.nativetoolkit/Plugins/Android` が空であること。PreBuildProcessor が何も変えなかった、つまりコミットした中身がそのまま使われたことが分かる。
- ログに `Deleted AAR not in the pin` が無いこと。
- `ls Plugins/Android/*.aar` がちょうど 2 本であること。

`git ls-files` が 6 ファイルという確かめは、このまま残します。

#### S2-4 ｜ B ｜ 低 ｜ V-S4-1（APK の確かめ方）
**問題**: 道具の場所とビルドの条件が書かれていません。

- `dexdump`、`apkanalyzer`、`aapt2` は PATH に無い。Unity が同梱している:
  - `…/6000.4.2f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/{dexdump,aapt2}`
  - `…/SDK/cmdline-tools/16.0/bin/apkanalyzer`（Java が要る。同じ所の `OpenJDK` を `JAVA_HOME` にする）
- `BuildPipeline.BuildPlayer` で使う profile、シーン、`locationPathName` が決まっていない。
- 出し先の `Build/Android/` には、8 月に書き出した古い Gradle のプロジェクトが残っている。1.x のクラスを含むかもしれない。
- 陽性の対照が無い。

minify は切ってあります（`AndroidMinifyRelease: 0`）。1.x の AAR が残っていれば、そのクラスは dex に入るはずなので、検査には意味があります。2.0.0 の `classes.jar` には `android.library.…ScheduledNotificationReceiver` が文字列として入っているので、調べるのは `Class descriptor` の行だけにします。

**提案**:
- 道具は上のパスで呼ぶと書く。
- 出し先は空のフォルダの `.apk` のファイル名にする。
- `-activeBuildProfile "Assets/Settings/Build Profiles/Android™ Profile.asset"` を明記する。
- Unity 6 のコマンドラインの `-build <path>` を使えば一時のスクリプトが要らない（要確認）。
- S4-1 の前の APK で、同じコマンドの件数が 0 より多いことを一度確かめる（陽性の対照）。

#### S2-5 ｜ B ｜ 中 ｜ 6 章（チェックリストの Android の行）
**問題**: md5 で照らすのは AAR だけです。依存の `.module` の照合（`AndroidGradleDependenciesTests`）は、native-toolkit の**作業ツリー**の `dist` を読みます（同テスト:103-112, 154）。

- 確かめたとき（`a7684dee`）からタグまでの間に `.module` だけが変わっても、AAR の md5 は一致するので気づけない。
- `git diff --quiet <dv> -- dist/<dv>/android` が見るのは、タグと**今の作業ツリー**の差だけ。確かめに使ったコミットとの差は見ない。

**提案**: 次のどちらかを行にする。
- 最後の通しの検証で使った native-toolkit のコミットを記録しておき、`git -C ../native-toolkit diff --quiet <verified> <dv> -- dist/<dv>/android` を見る。
- native-toolkit をタグにした状態で、`AndroidGradleDependenciesTests` を毎回流す。

#### S2-6 ｜ A2 ｜ 中 ｜ 4 章（リリースの手順の直し）
**問題**: 「Breaking Changes を必須で足す」の条件が決まっていません。

- 必須が、毎回のリリースなのか、破壊的変更があるときだけなのかが分からない。
- あるかどうかを何で判断するかも分からない。ステップ 4 は `git log --oneline` の件名しか見ない。
- 既存の手順には「該当するコミットがないセクションは省略する」とある（release/workflow.md:95）。
- repo にも agent-rules にも `!:` や `BREAKING CHANGE` の約束は無い（grep で 0 件）。

**提案**: 判断の材料を決めて手順に書く。例えば、`manual/<version>/` に「Changes in <version>」の節があるとき、または件名に `!` があるときに必須とする。S4-2 と S4-3 のコミットの件名には `!` を付ける。

#### S2-7 ｜ A2 ｜ 中 ｜ 4 章「共通（…write-manual の決まりに従う）」
**問題**: 決めることを、無い決まりに預けています。

- `write-manual/workflow.md` には、移行の節も「Changes in」も、共通の節の置き場所の決まりも無い（grep で 0 件）。
- 共通の項目（Gradle の依存、Mono、32 ビット、`<service>`、1.x の予約と通知）をどこに置くかが未決のまま。
- 1.x の 2 本の AAR（`android.library.*`、`android.unity.*`）を同梱しなくなったことも、項目に無い。そのクラスを自分のコードから直接使っていた利用者に関わる。

**提案**: 置き場所を設計で決める。例えば、`index` の Android の動作環境の下に「Changes in 1.13.0 (Android)」を置き、各ページの節からリンクする。1.x の AAR を同梱しなくなったことも項目に足す。

#### S2-8 ｜ C ｜ 低 ｜ 2.1 の末尾、2.2
**問題**: 次の 4 点です。

- 「既知の逸脱」の表（`common.md:455-462`）に今あるのは、Android の 3 行と `IconConfiguration` だけ。iOS の 5 型と macOS の 3 型は、もともと載っていない。「残す」ではなく「足す」が正しい。
- 件数が食い違う。2.1 は「iOS・macOS の 9 型」（`IconConfiguration` を含めて 9）、2.2 は「iOS・macOS の 9 型と `IconConfiguration`」（10 になる）。
- `common.md:455` の見出し「Runtime 11 件」と、`os-prefix-violations/README.md` の数（81 型、13%）も直す要る。
- `os-prefix-violations/README.md` には旧名が 25 か所あり、多くは経緯の記録。2.2 はこれを「置き換える」表に入れているが、機械的に置き換えると記録が書き換わる。

**提案**:
- 「8 型（iOS 5、macOS 3）と `IconConfiguration` の計 9」にそろえる。
- 表に足すと書く。
- artifact の README は、機械的な置き換えではなく手で直すと明記する。

#### S2-9 ｜ C ｜ 低 ｜ 3.4（旧名の検査）
**問題**: 次の 4 点です。

- (a) S4-3 のコミットの時点では `manual/1.13.0` がまだ無い。rg は「No such file」で終了コード 2 を返し、「0 件」と区別しにくい。
- (b) 当たってよい行の決まりは「旧名 → 新名の表」と `MovedFrom` だけ。移行の節が消したビルダーの名前を書く行は、表の行ではないので落ちる。`MovedFrom` を複数の行に分けて書くと、`sourceClassName` の行に `MovedFrom` が含まれない。
- (c) S4-4 は「文書ごとにコミット」なので、途中のコミットでは 0 件にならない。
- (d) 調べる対象に `Documentation~` の古い名前空間が無い（S2-1）。`Configure Gradle Project` と `android.library` も無い（S2-2）。

**提案**:
- 段ごとに調べるパスを決める。
- 当たってよい行を、ファイルと節で決める。
- 0 件の条件は、S4-4 の最後のコミットに掛ける。

#### S2-10 ｜ C ｜ 低 ｜ 参照の位置と書き方
- v3:19 の「1.4」は 3.2、v3:21 の「1.2」は 3.1 が正しい。2.2 の見出しの「3.2 の検査」は 3.4 が正しい。
- `VERSION.txt` の `:31-33` は、ファイルが 32 行なので `:30-32` が正しい。
- `PreBuildProcessor` の「`:220-227`」は、例外が 229 行まで続き、組み立ては 231-235 行にある。
- `WindowsClipboardJsonBuilder.cs:18` は `<c>` ではなく地の文。
- 消したビルダーには「存在する新しい名前」が無い。`IosShareJsonBuilder.cs:12` と `WindowsClipboardJsonBuilder.cs:18` を何に直すかを書く（例: 「1.x の Android の builder」）。

#### S2-11 ｜ C ｜ 低 ｜ 7 章（どのコミットに入れるか）
- `common.md:45` の「旧ブリッジの AAR は段 4 で外す」を過去形にする直しは、内容から S4-1 のコミットに入るべき。2.2 と 7 章は S4-3 に入れている。
- `testing.md:405-406` は 3.3 では S4-2 の作業だが、7 章のステップ 3 に載っていない。
- `package.json` の版を上げるのが、4 章（S4-4）と 6 章（S4-5）の両方にある。どちらか 1 つにする。

#### S2-12 ｜ C ｜ 低 ｜ S1-13 #8 の残り
`common.md:518` の「`XxxJsonBuilder` クラスで JSON 組み立てを担い、Manager から呼び出す」は、v3 で手つかずです。段 4 の後、Android の Share と Clipboard には builder が無く、Notification の builder も Manager からは呼ばれません。
- **提案**: 直すか、Android の例外として書くかを決める。

#### S2-13 ｜ B ｜ 低 ｜ V-S4-4（`[MovedFrom]` の確かめ方）
**問題**: 確かめ方が決まっていません。
- batchmode で API Updater を走らせるには `-accept-apiupdate` が要る（要確認）。
- パッケージを埋め込んだときと、`file:` や git で入れたとき（`Library/PackageCache`）で、振る舞いが同じかは分からない（要確認）。
- `MovedFromAttribute` を enum（`ClipboardReadStatus`）に付けられるか（要確認）。
- 合否の基準が書かれていない。

**提案**: 使い捨てのプロジェクトから、パッケージを `file:` で参照する。1.12.0 の書き方のスクリプト（S2-1）を置き、次を合格とする。
- スクリプトが書き換わる。
- エラー無しでコンパイルが通る。
- `Assets/` の外のファイルが変わらない。

---

### 3. 確かめたこと（指摘なし）
- **37 型の一覧と新しい名前は、実ソースと一致した**: Notification 3+11、Share 4+8、Clipboard 7+4。`Runtime/Android` で、`Android` で始まらない public の型は、この 37 だけ（入れ子の public の型は無い）。`Android<旧名>` という名前は、`Packages/` と `Assets/` のどの `.cs` ともぶつからない。
- **単語の境界で置き換えても、型の外を壊さない**: 旧名と同じ名前のメンバーやプロパティ（`.旧名`）は無い。実行時の文字列リテラルや `nameof` にも無い。`link.xml`、シーン、アセットにも無い（当たるのは xcframework だけで、置き換えない先に入っている）。
- **1.12.0 と HEAD で、public の型の集合は同じ**: 段 3 で public の型は 1 つも消えていない。
- **1.x の AAR と dex**: クラスは `android/library`（245）と `android/unity`（60）だけ。2.0.0 のクラスは `com/jonghyunkim` だけ。FileProvider の authority は 1.x と 2.0.0 で同じなので、Clipboard のサンプルは影響を受けない。
- **3.3 の件数**: ビルダーのテストは Share 21 本、Clipboard 16 本。未コミットの `AndroidNotificationJsonBuilder` の直しは、XML コメントだけ。
- **docfx**: `docfx` はこの環境にある。`api/` と `site/` は gitignore されている。

---

**A1 は 2 件（S2-1、S2-2）です。止める基準（A1=0）には達していません。**

---

## 2. Codex


読み取り専用で確認しました。ファイルおよび Git 状態は変更していません。

結論は、前回28件のうち「解消22、部分解消3、未解消1、対象外2」です。新規指摘は6件で、A1は1件です。

37型の内訳（結果型等14＋Payload 23）と新名称、`OnCApi` の6ファイル・56箇所は実ソースと一致し、新名称の衝突もありません。

### 前回指摘の判定

| ID | 判定 | 根拠 |
|---|---|---|
| S1-1 | 解消 | Androidの14結果型等＋23 Payloadを列挙し、iOS/macOS/IconConfigurationを明示的に除外している。`design-v3.md:16,37-53` |
| S1-2 | 対象外 | IconConfiguration/macOS型を今回変更しない決定になった。`design-v3.md:16,53` |
| S1-3 | 解消 | 6ファイル56箇所の1.x側、RECORDログを削除し、fixtureのSetUpでC ABIを必須化する。`design-v3.md:91-95` |
| S1-4 | 解消 | Android Playerで`!UNITY_EDITOR`部分をコンパイルし、他targetが不要な検索根拠を記載した。`design-v3.md:148` |
| S1-5 | 解消 | 置換対象、非対象、他OSコメント3箇所を具体化した。`design-v3.md:55-73` |
| S1-6 | 解消 | 各機能ページ・3言語の「Changes in 1.13.0」とRelease notesのBreaking Changesを確定した。`design-v3.md:130-139` |
| S1-7 | 部分解消 | 旧37名とbuilderの`rg`は追加されたが、旧Android名前空間の機械検査はmanualだけで、Documentation/READMEを覆わない。`design-v3.md:104-116` |
| S1-8 | 解消 | 1.x service例の削除と、利用者側manifestから削除する案内を含む。`design-v3.md:127,134` |
| S1-9 | 解消 | Mono/32bit、Gradle依存、KGP廃止、4機能の既知差分、namespace importを網羅した。`design-v3.md:122-134` |
| S1-10 | 部分解消 | 実行独立性、dex/manifest検査は具体化したが、APKを確実に生成する方法が未確定。`design-v3.md:146-149` |
| S1-11 | 解消 | dist位置、タグ照合、不一致時の差替え・再検証、workflow更新時期を明記した。`design-v3.md:12,137-139,160-165` |
| S1-12 | 解消 | API docsの生成先と非公開方針を実態に合わせた。`design-v3.md:136` |
| S1-13 | 解消 | 検索例外、VERSIONコメント、trackedファイル、testing表、README、未コミットコメントの割当などを反映した。`design-v3.md:10,65,86-88,101-102,129,147-149,172` |
| S1-14 | 解消 | `[MovedFrom]`を実装前にUPM利用者スクリプトで検証する工程を設けた。`design-v3.md:75-79,150,170` |
| X1-1 | 解消 | builder本体・テストを各`.meta`と対で削除する。`design-v3.md:97-102` |
| X1-2 | 部分解消 | manual/Documentation/READMEを置換対象にしたが、旧名前空間の停止条件がmanualに限定されている。`design-v3.md:55-65,104-116` |
| X1-3 | 解消 | iOS Clipboard、iOS Share、Windows Clipboardのコメントを限定修正する。`design-v3.md:61,73` |
| X1-4 | 解消 | 該当する4 workflowを更新対象に追加した。`design-v3.md:63-65` |
| X1-5 | 解消 | GitHub Release notesにBreaking Changesと移行ページへのリンクを要求した。`design-v3.md:137-139` |
| X1-6 | 解消 | API docsを`docs/<版>`へ公開しない実態と、生成APIの新旧型検査を記載した。`design-v3.md:136` |
| X1-7 | 対象外 | macOS/IconConfigurationの改名を今回から外した。`design-v3.md:16,53` |
| X1-8 | 解消 | Android Editor/Playerの検証経路と、他targetを省く根拠を示した。`design-v3.md:148` |
| X1-9 | 未解消 | `extra_*`無視の確認が依然`grep`だけで、片側のみ・両方ありの挙動テストがない。現行は片側のみを例外にする。`design-v3.md:87`、`PreBuildProcessor.cs:213-235` |
| X1-10 | 解消 | C ABI期待へ固定し、seam欠落時にSetUpで失敗させる。`design-v3.md:91-95` |
| X1-11 | 解消 | dexのclass descriptorとmerged manifestを検査対象に固定した。`design-v3.md:146` |
| X1-12 | 解消 | Android 37型と残存iOS/macOS/IconConfigurationを分離し、両READMEを更新対象にした。`design-v3.md:50,53,65` |
| X1-13 | 解消 | `check_design_consistency.py`を実行し、FAIL 0を確認した。版表記も誤認されない。`design-v3.md:11,70` |
| X1-14 | 解消 | タグ側の照合位置を`dist/<dv>/android/<aar>`に確定した。`design-v3.md:160-165` |

### 新規指摘

#### X2-1｜A1｜`[MovedFrom]`の適用条件
- 場所: `design-v3.md:75-79,150,170`
- 問題: API Updaterで利用者スクリプトを改名できなければ属性自体を付けない設計であり、「37型へ`[MovedFrom]`を追加する」という今回の前提に反する。23 Payloadは`[Serializable]`だが、シリアライズ済み参照の移行も検証していない。
- 証拠:
  - Payloadは実際にSerializableである。`AndroidClipboardPayloads.cs:9-11,28-30,51-53,68-70`、`AndroidNotificationPayloads.cs:9-11,22-24,68-70`、`AndroidSharePayloads.cs:9-11,34-36`
  - Unityの一次ソースは、`sourceClassName`によるクラス名変更をAPI Updaterがサポートせず、属性はシリアライズ移行には利用できると明記している（UnityCsReference `UpdatedFromAttribute.cs`）。
- 修正: スクリプト自動更新とシリアライズ移行を分離する。API Updater非対応でも37型へ`[MovedFrom(false, sourceClassName: "...")]`を付け、旧Payloadを保存した`SerializeReference` fixtureを再読込して型と値を確認する。コード移行は表による手動変更として案内する。

#### X2-2｜B｜Plugin検査のrepo-relative pathが誤っている
- 場所: `design-v3.md:147,149,160`
- 問題: `git ls-files Plugins/Android`と`git status ... Plugins/Android`はリポジトリルートから実行すると対象が0件になる。実体はpackage配下である。
- 証拠: 実ファイルは`Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/VERSION.txt:1-32`にあり、既存release workflowも完全なpackageパスを使う。`agent-rules/workflows/release/workflow.md:39-40`
- 修正: すべて`Packages/com.jonghyunkim.nativetoolkit/Plugins/Android`に統一する。`git ls-files`の期待値は旧AAR削除後に6件とする。

#### X2-3｜B｜APKを確実に生成・検査できない
- 場所: `design-v3.md:146`
- 問題: 「使い捨ての`-executeMethod`」について、実在するメソッド、scene、target、出力形式、active profile、成功判定がない。また通常のAndroid ProfileはAPKではなくGradle Project出力に設定されている。
- 証拠:
  - Android Profileは`m_ExportAsGoogleAndroidProject: 1`。`Assets/Settings/Build Profiles/Android™ Profile.asset:990-1008`
  - active profileはLibraryに残るため毎回明示が必要。`scripts/verify_unity_android.sh:7-21,72-77,153-154`
- 修正: Test Android Profileを明示し、APK出力用設定を一時的に固定して復元するメソッド、`BuildPlayerOptions`、終了条件を設計する。またはGradle Projectを出力後、`gradlew assembleRelease`でAPKを作る。APK展開、`classes*.dex`への`dexdump`、manifest検査までコマンド化する。

#### X2-4｜B｜旧Android名前空間の停止検査が不足
- 場所: `design-v3.md:62,104-116`
- 問題: 旧37名の`rg`には名前空間が含まれず、旧Android名前空間の0件条件はmanualだけである。`Documentation~/index{,.ja,.ko}.md`のAndroid節を検査しない。
- 証拠: 現在のAndroid Dialog例にも旧usingが残る。`Documentation~/index.md:137-147`。一方、macOSでは同じ旧名前空間が正当である。`Documentation~/index.md:1260-1268`
- 修正: manualとDocumentation indexのAndroid節だけを抽出し、`Runtime.Android.{Dialog,Share,Clipboard,Notification}`になっていることを検査する。単純な全ファイル0件検査にはしない。

#### X2-5｜B｜S4-3の停止条件が、まだ存在しないmanualを検索する
- 場所: `design-v3.md:106-116,151,168-175`
- 問題: V-S4-5はS4-3のコミット条件だが、検索対象に`manual/1.13.0`を含む。設計順ではそのディレクトリを作るS4-4が後なので、S4-3時点の`rg`は「0件」ではなくパス不存在の終了コード2になる。
- 証拠: 新版manualはwrite-manualが最大版をコピーして作る。`agent-rules/workflows/write-manual/workflow.md:13-16`
- 修正: V-S4-5を二分し、S4-3ではRuntime/Tests/rules等だけ、S4-4ではmanual/Documentation/READMEを検査する。またはmanual baseline作成をS4-3より前へ移す。

#### X2-6｜C｜manual baselineの必須コミットが順序にない
- 場所: `design-v3.md:120,175`
- 問題: `write-manual`に従うとしているが、同workflowが要求する`manual/1.12.0`→`1.13.0`コピー直後のbaseline commit/pushが、v3の「文書ごとにコミット」に現れない。
- 証拠: baselineの即時commit/pushは必須。`agent-rules/workflows/write-manual/workflow.md:13-19`
- 修正: S4-4冒頭にbaselineコピー用コミットを明記し、その後に4機能×3言語の編集、公開、docs同期の順を置く。「文書ごと」の単位も明確にする。

### 総合評価
37型の選定、JSON builderの扱い、1.x分岐の削除、移行節とRelease notesの配置は正確です。一方、`[MovedFrom]`の失敗時分岐は今回の決定とUnity側の用途を取り違えており、A1として修正が必要です。APK・旧名前空間・パス・実行順の4件は、実装内容ではなく検証ゲートが成立しない問題です。

**A1 findings: 1件（X2-1）**