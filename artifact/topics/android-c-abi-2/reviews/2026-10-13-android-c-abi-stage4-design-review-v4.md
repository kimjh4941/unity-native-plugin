# レビュー結果

- 日付: 2026-10-13
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v5.md
- 機能名: android-c-abi-2（段 4）
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 4）と Codex（ラウンド 4）を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力。文言には手を加えていない（見出しの段を下げ、先頭の題を外した）
- A1: サブエージェント 0 件、Codex 1 件（X4-8）。止める基準（両方の A1 が 0）には達していない

---

## 1. サブエージェント（Claude）

対象は `artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v5.md`（ラウンド 4）です。リポジトリのファイルは変えていません。一時ファイルはすべて scratchpad に置きました（タグ 1.12.0 の Runtime の展開、3.4 の検査を写した Python、Unity の DLL を読む IL スキャナ）。

**結論: A1 は 0 件で、止める基準（A1=0）に達しました。** 新しい指摘は 14 件（A2 1、B 4、C 9）です。重いのは S4-1 と S4-2 です。

---

### 1. 前のラウンドの指摘の判定

| ID | 判定 | 根拠 |
|---|---|---|
| S3-1 | 解決 | v5:130。決まりを Python に写し、1.12.0 の文書に流した。旧い `using` を持つ Android のブロック 45 個（Documentation~ 18、dialog 18、share・clipboard・notification 各 3）を全部拾い、Android 以外のブロックは 0 件 |
| S3-2 | 解決 | v5:89-94（enum に付けられること、ガード付きのスクリプト、手で直す形を既定にすること）。ただし根拠に引いた `AffectsAPIUpdater` が実際の Updater に使われていない（S4-3） |
| S3-3 | 解決 | v5:197、222（S4-3 の後に移し、手順を 2 段に分けた）。手順の中身の穴は S4-2 |
| S3-4 | 解決 | v5:195、223（ステップ 5 の通しの実行） |
| S3-5 | 解決 | v5:207-211（`VERSION.txt` のキーと、clean だったことの記録）。`ReadPinValues` が知らないキーを無視することも確かめた（`PreBuildProcessor.cs:145-160`） |
| S3-6 | 解決 | v5:194（実行の前後の比較、ログ名）。文言の誤りは S4-9 |
| S3-7 | 解決 | v5:193（`--pull-apk=`、陽性の対照、「旧い component」の定義）、v5:218（別のコミット）。道具 3 つのパスが実在することも確かめた |
| S3-8 | 解決 | v5:151-152。ただし `## Android requirements` の日本語版・韓国語版の見出しが無い（S4-10） |
| S3-9 | 解決 | v5:129。ただし index の `##` の節が除外に入っていない（S4-1） |
| S3-10 | 解決 | v5:127、155、157-158、179。細部の残り（`Documentation~` の :99 と :7）は S4-10 |
| S3-11 | 解決 | v5:69。`design-feature/workflow.md:54,57` は実ファイルと一致した |
| S3-12 | 解決 | v5:146 |
| S3-13 | **部分** | v5:187 で `!` を足した。しかし commit-msg の変更は S4-4（7 章のステップ 7）にあり、`!` が要る S4-2・S4-3 のコミット（ステップ 2・3）より後（S4-7） |
| S3-14 | 解決 | v5:13（利用者の指示が出典） |
| S3-15 | 解決 | v5:218。細部は S4-12 |
| S3-16 | 解決 | v5:102。確かめの grep が成り立たない（S4-8） |
| X3-1 | 解決 | v5:17、89（42 型すべてに付ける。enum の例外は消えた） |
| X3-2 | 解決 | v5:126 |
| X3-3 | 解決 | v5:197、222 |
| X3-4 | 解決 | v5:193（`pm path` の全行を取る、展開、dex 全部、失敗の条件） |
| X3-5 | 解決 | v5:194 |
| X3-6 | 解決 | v5:208-211（完全なパス、`rev-parse -q --verify refs/tags/<dv>`） |
| X3-7 | 解決 | v5:152-167（各機能のページに中身を置き、index は共通の項目とリンクだけ） |
| X3-8 | 解決 | v5:146 |
| X3-9 | 解決 | v5:140-146、223 |
| X3-10 | 解決 | v5:13。ただしリリースの手順の push には触れていない（S4-4） |

---

### 2. v5 の新しい指摘

#### S4-1 ｜ B ｜ 中 ｜ 3.4 の許す行（v5:129）と、4.2 の index の共通の節（v5:152-155）
- **問題**: 3.4 が検査から除くのは、`### Changes in 1.13.0`、`### 1.13.0 での変更`、`### 1.13.0 변경 사항` の節だけです（「次の同じ段の見出しまで」）。一方で 4.2 は、index に段も形も違う節を置きます: `## Changes in 1.13.0 (Android)`、日本語版 `## 1.13.0 での変更（Android）`、韓国語版 `## 1.13.0 변경 사항 (Android)`。しかもその節に「Configure Gradle Project が無くなった」と書くよう求めています（v5:155）。3.4 の `Config\w* Gradle Project` はこの行に必ず当たるので、決まりどおりに書くと S4-4 の検査が 0 件になりません。
- **根拠**: v5:127、129、152、155。
- **提案**: 許す見出しに index の 3 つを足す。または「`Changes in 1.13.0` / `1.13.0 での変更` / `1.13.0 변경 사항` で始まる見出し（段を問わない）から、同じ段以上の次の見出しまで」と決める。

#### S4-2 ｜ B ｜ 中 ｜ V-S4-4 の手順（v5:197）
- **(i) 直らないスクリプトがあると、(d) を測れない。**
  - (b) は「37 型は直らない」と見込んでいるので、(2) で開き直すと `Assets/` にコンパイルエラーが残ります。
  - コンパイルエラーがある間、Editor は前のアセンブリ（1.12.0 の型）を持ち続けます。batchmode の `-executeMethod` は「Scripts have compiler errors」で走りません（どちらも要確認）。
  - そのため (d) の Payload の読み直しは、走らないか、1.12.0 の型で読んで偽の PASS になります。
  - (1) でアセットを作る Editor のスクリプトも旧名を参照するので、同じ理由で残せません。
- **(ii) (e) が (a)(c) と食い違う。** v4 の「`Assets/` の外が変わらない」が、v5 では「パッケージのコピーの外のファイルが変わらないか」に変わりました。(a)(c) が成り立てば `Assets/` のスクリプトは書き換わるので、(e) は必ず「変わった」になります。
- **(iii) 例の 1 本のスクリプトに、5 型（`AndroidClipboardManager`）と 37 型（`ClipboardReadResult`）が混ざっている。** これでは (a) と (b) を分けて記録しにくいです。
- **(iv) `-accept-apiupdate` の要確認は閉じられる。** Unity 6000.4.2f1 の `Contents/MacOS/Unity` に、文字列 `accept-apiupdate` と `APIUpdaterManager::DoesCommandLineIndicateAPIUpdatingShouldHappenWithoutConsent` がある。
- **(v) (c) は batchmode で開くだけでは結果が決まってしまう。** Editor では `!UNITY_EDITOR` の中はコンパイルされないので、(c) は必ず「変わらない」になります。
- **提案**: 次の 3 段に分ける。
  1. Updater: 5 型だけのスクリプト、37 型だけのスクリプト、ガード付きのスクリプトの差分を記録する。
  2. 残ったエラーのスクリプトと、アセットを作るスクリプトを消す。
  3. 旧名を参照しない Editor のスクリプトで、アセットを読み直して値を比べる。

  (e) は「パッケージのコピーと、リポジトリの作業ツリーが変わらない」に直す。(c) は「Editor では直らず、Player のビルドで初めてエラーになる」と記録する形にする。

#### S4-3 ｜ A2 ｜ 低 ｜ 2.3 の根拠と v5:19 の断定（v5:19、92）
- **問題**: 2.3 は `AffectsAPIUpdater` が false になることを根拠に、「37 型は直らない」としています。しかしこのプロパティは、Updater からは使われていないようです。また v5:19 は「API Updater は、改名した 37 型を直さない」と事実として書き、2.3 と V-S4-4 の (b) は「見込み」としていて、強さが揃っていません。
- **根拠**:
  - `Resources/Scripting/Managed` で名前 `AffectsAPIUpdater` を含む DLL は、`UnityEngine.CoreModule.dll`、`UnityEngine.MultiplayerModule.dll`、`UnityEngine.dll` だけ（`UnityEditor*.dll` には無い）。
  - IL スキャンで、`get_AffectsAPIUpdater` を呼ぶ箇所は CoreModule にも MultiplayerModule にも無かった。
  - Updater の本体は `Resources/BuildPipeline/Compilation/ApiUpdater/`（`ApiUpdater.MovedFromExtractor.dll`、`CSharpUpdater.dll`）で、属性を自分で読む。文字列に `sourceNamespace`、`movedFromNamespaces`、`MoveToNamespaceConfig` はあるが、`sourceClassName` は無い。結論（名前空間だけ直す）は当たっている可能性が高いが、根拠が違う（要確認）。
- **提案**: 根拠を上の事実に置き換える。v5:19 は「直らない見込み（V-S4-4 で確かめる）」に揃える。

#### S4-4 ｜ B ｜ 低〜中 ｜ 6 章と push の約束（v5:13、205）
- **問題**: 6 章は `release/workflow.md` に従うとしています。しかしその手順には、エージェントの push が含まれます:
  - ステップ 5 の `gh pr create` は、ブランチが push 済みであることが前提。
  - ステップ 7.3 は `git push origin <version>`（`release/workflow.md:103,145`）。

  基本情報の「エージェントは push しない」とどう両立させるかが書かれていません。
- **提案**: 6 章に「ブランチとタグの push は利用者が行う。エージェントはステップ 5 の前と 7.3 で止まって頼む」と書く。または release の手順にその分岐を足す。

#### S4-5 ｜ B ｜ 低 ｜ 6 章のチェックリストの差分の範囲（v5:211）
- **問題**: `git diff --quiet <verified> <dv> -- dist/<dv>/android` の範囲には、パッケージが使わないものも入ります。`dist/1.13.0/android` は `build-2.0.0.log` と `include/` を含むからです。タグを打つときの作り直しでログだけが変わっても ❌ になり、v5:213 の手順（AAR の差し替えと通しの検証のやり直し）に入ってしまいます。
- **根拠**: `ls native-toolkit/dist/1.13.0/android`（AAR 2 本、`build-2.0.0.log`、`include`、`m2`）。
- **提案**: 範囲を `dist/<dv>/android/*.aar` と `dist/<dv>/android/m2` に絞る。`m2` は、`AndroidGradleDependenciesTests` が読む `.module` のある場所です。

#### S4-6 ｜ C ｜ 低 ｜ 7 章の順（v5:221-224、195）
- 5 章の V-S4-2 は「いつ: S4-2、S4-3」ですが、7 章のステップ 3 には V-S4-2 がありません。S4-3 は一度もコンパイルされないままコミットされ、V-S4-4 もそのパッケージを使います。
- 「最後の通しの検証」（ステップ 5）が、実装のレビュー（ステップ 6）の前にあります。レビューで直しが出ると、もう「最後」ではなくなり、`verified_native_toolkit_commit` も書き直しになります。
- **提案**: ステップ 3 のコミットの前に、少なくとも EditMode（Editor のコンパイル）を流す。レビューをステップ 5 の前に移すか、「直しが出たらステップ 5 をやり直す」と書く。

#### S4-7 ｜ C ｜ 低 ｜ commit-msg の `!` を入れる時点（v5:187、220-221）
- S3-13 の残りです。commit-msg の手順の変更は S4-4 にあり、`!` を使う S4-2・S4-3 のコミットより後です。
- **提案**: commit-msg の変更を S4-0 に移す。または「S4-2・S4-3 は件名に手で `!` を付ける」と書く。

#### S4-8 ｜ C ｜ 低 ｜ 3.1 の確かめの grep（v5:102-103）
- `grep -n extra_` は大文字と小文字を区別するので、残ったプロパティを拾えません: `ExtraDistVersion`、`ExtraAars`（`PreBuildProcessor.cs:198-206,416,424-425`）。
- 3.1 は Android の XML コメントを「Windows（:100-105）と同じ形」に直すとしています。Windows の文言には「an extra_* key left in the file is ignored」が入るので、正しく直すと 2 か所目が当たり、「:104 のほかに当たらない」と食い違います。
- **提案**: `grep -niE 'extra'` にし、期待は「過去形の注記 2 か所だけ」とする。

#### S4-9 ｜ C ｜ 低 ｜ V-S4-1b の文言（v5:194）
- 「`PreBuildProcessor` が何も消さず、何も写さなかった」は事実と違います。Android™ Profile の実行では、毎回上書きで写し、「Copied」をログに出します（`PreBuildProcessor.cs:450-454`）。status の比較で分かるのは、「バイトが同じものだけが写された」ことです。
- **提案**: 文言をそう直す。

#### S4-10 ｜ C ｜ 低 ｜ 4.2・4.3 の細部
- (a) v5:153「名前空間を持たない型の一部が改名された」は、「Android の接頭辞を持たない型」の誤りです。
- (b) v5:155「kotlin-stdlib 2.2.21、coroutines 1.9.0 ほか 7 つ」は、「計 7 つ」の誤りです（`AndroidGradleDependencies.cs:46-52` は 7 行）。
- (c) `## Android requirements` の日本語版・韓国語版の見出しが決まっていません。
- (d) 各機能のページで移行の節を置く場所（`## Android` の直下か）と、目次の行が決まっていません。前例の `dialog.md:19-20` は、目次に `[Changes in 1.12.0](#changes-in-1120)` を載せています。
- (e) 4.3 の `Documentation~/index.md:95-99` は、:99「Install the sample app from Android Studio」まで含みます。この行は残すべきで、マニュアルの側の範囲（`index.md:185-191`）は :192 を含みません。正しくは :95-98 です。
- (f) `Documentation~/index{,.ja,.ko}.md:7` の紹介文（「Gradle/Xcode settings」）が、4.3 の直す対象に入っていません。

#### S4-11 ｜ C ｜ 低 ｜ 行番号のずれ
- `testing.md:405-406` は、正しくは `:406-407` です（405 は表の区切りの行。Clipboard は 406、Share は 407）。v4:113 から残っています。
- v5:132 の `verify_manual.sh:211` は空の行で、`PACKAGE_TYPE` は `:212` です。
- v5:193 の `verify_unity_android.sh:83-90` は、実際は `:82-89` です。
- ほかの引用は実ファイルと一致しました: `PreBuildProcessor.cs`、`VERSION.txt`、`common.md:45,455,518`、各 workflow の行、`index.md:7,185-191`、`notification.md:619-624`、`dialog.md:37-39,831`、`publish_docs.sh:44-57,86-93`、`AndroidClipboardCApi.cs:103`、ほかの OS のコメント 3 か所。

#### S4-12 ｜ C ｜ 低 ｜ 7 章のステップ 0（v5:218）
- 「設計 v1〜v5、レビュー v1〜v3」では、このラウンドのレビュー v4（と、もしあれば設計 v6）が漏れます。
- 文が崩れています（「…XML コメントの直しは除く（S4-2 に入れる）を…」）。
- **提案**: 「その時点の段 4 の設計とレビューのすべてと、README の J-12。`AndroidNotificationJsonBuilder.cs` は除く」と書き直す。

#### S4-13 ｜ C ｜ 低 ｜ 6 章の新しいキー
- `VERSION.txt` の頭のコメントは、「PreBuildProcessor READS the keys below」としてキーを並べています（:3、:11-16）。`verified_native_toolkit_commit` を足すなら、その一覧に「ビルドは読まない。リリースのチェックリストが読む」と書くべきです。

#### S4-14 ｜ C ｜ 低 ｜ 3.4 の 1.x のクラス名の正規表現（v5:127）
- `android\.(library|unity)\.[a-z]+\.[A-Za-z.]*[A-Z]` は、今も公開しているアクション ID の値 `android.unity.notification.ACTION_BODY_TAP` にも当たります（`AndroidNotificationManager.cs:63,68`）。1.13.0 のマニュアルが移行の節の外でこの値を書くと、誤検出になります（1.12.0 の文書には無かった）。
- **提案**: 許す例外として書いておく。

---

### 3. 確かめたこと（指摘なし）
- **型の数と名前空間**: `Runtime/Android` の public 型は 44（接頭辞の無いもの 37）。2.1 の表のファイルと型の対応は実ファイルと一致した。タグ 1.12.0 の名前空間は Clipboard 13、Dialog 1、Notification 16、Share 14 で、表と一致した。
- **名前のぶつかり**: 37 の旧名は、パッケージと `Assets/` のほかの型とぶつからない。
- **置き換えの対象の外**: 旧名が残るのは agent-rules の 2 ファイル（対象に入っている）と、macOS の xcframework のバイナリ（置き換えない側）だけ。`Assets/` と `Samples~` には無い。
- **範囲の外の 9 型**: iOS 5、macOS 3、`IconConfiguration` で、数は正しい。
- **GUID**: 4 つの Manager の `.cs.meta` の GUID は、1.12.0 と HEAD で同じ。シーンの参照は壊れない。
- **ビルダーのテスト**: Share 21 本、Clipboard 16 本（3.3 の 37 本と一致）。
- **native-toolkit の状態**: HEAD `a7684dee`、タグの最新は 1.12.0、`dist/1.13.0/android` は clean、AAR の md5 は同梱のものと一致。
- **`Documentation~` の生成物**: `api/`、`obj/`、`site/` は gitignore されている（`.gitignore:86-88`）。

---

**A1: 0 件。止める基準（A1=0）に達しました。** 実装に入る前に、B の 4 件を直すことを勧めます。特に S4-1（このままでは S4-4 の検査が 0 件にならない）と、S4-2（V-S4-4 の (d) が偽の PASS になりうる）です。

---

## 2. Codex

結論: 前回の X3-1〜X3-10、S3-1〜S3-16 は、指摘された範囲ではすべて解消されています。v5 の新規指摘は 9 件で、A1 は 1 件（X4-8）です。

以下、v5 は design-v5.md の略称です。実体は /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v5.md です。

### 強み
- 現行 `Runtime/Android` の public は 44 型です。削除するビルダー 2 型を除くと `[MovedFrom]` 対象は 42 型で、設計の内訳（改名 37、名前維持 5）と一致します。
- Unity 6000.4.2f1 同梱の `dexdump`、`apkanalyzer`、OpenJDK は実在します。
- `AndroidNotificationJsonBuilder` を残し、Share・Clipboard のビルダーだけを削除する利用者決定が明確です。
- Android AAR の照合は、AAR 本体だけでなく `dist/<dv>/android` 全体の差分まで確認する構成です。

### 1. 前回指摘の判定

#### X3-1〜X3-10（すべて解消）

| ID | 判定 | 根拠（design-v5.md の行） |
|---|---|---|
| X3-1 | 解消 | enum を含む 42 型すべてを必須化し、37 型と 5 型の引数も固定した（17、89） |
| X3-2 | 解消 | 42 型の個数、属性数、旧名前空間、37 型の旧名、5 型の `sourceClassName` 不在を照合対象にした（126）。引数検査の穴は X4-2 |
| X3-3 | 解消 | V-S4-4 を S4-3 実装後へ移し、旧版から新版の二段階にした（197、222） |
| X3-4 | 解消 | `pm path` 全行、`package:` 除去、全 APK 取得、`classes*.dex` 展開、DEX 0 件で失敗、base manifest を規定した（193）。出力先管理の穴は X4-6 |
| X3-5 | 解消 | 「空であること」ではなく、実行前後の `git status --porcelain` が同じことを条件にした（194） |
| X3-6 | 解消 | 完全な package パス、タグ確認、AAR md5、`<verified>` とタグの dist 差分を規定した（208） |
| X3-7 | 解消 | 型ごとの名前空間、改名表、手修正、既知差分を各機能ページへ移した（160）。共通項目の分担の不一致は X4-8 |
| X3-8 | 解消 | `publish_docs.sh` の次に `verify_manual.sh` の順になった（146）。この順の必要性は scripts/verify_manual.sh:428 と scripts/publish_docs.sh:44 に一致 |
| X3-9 | 解消 | README、`Documentation~`、workflow、公開後の `docs/1.13.0`・`docs/latest` に個別コミットを割り当てた（142、146） |
| X3-10 | 解消 | push しないのはリポジトリ規則ではなく利用者指示だと明記した（13、140） |

#### S3-1〜S3-16（すべて解消）

| ID | 判定 | 根拠（design-v5.md の行） |
|---|---|---|
| S3-1 | 解消 | `UNITY_ANDROID` と旧 using が同居するコードフェンスを検査する形になった（130） |
| S3-2 | 解消 | `AffectsAPIUpdater`、enum 対応、ガードあり・なしの試験、37 型は手修正という既定を明記した（90、197） |
| S3-3 | 解消 | V-S4-4 を S4-3 後へ移し、1.12.0 で保存してから新版で再読込する手順を加えた（197） |
| S3-4 | 解消 | S4-3 後に `--as-package-user` を含む実機通し検証を置いた（195、223） |
| S3-5 | 解消 | `<verified>` を `VERSION.txt` の固定キーにし、dist 作業ツリーが clean だったことも記録する（207）。未知キーが無視されることは Editor/Build/PreBuildProcessor.cs:145 で確認 |
| S3-6 | 解消 | 前後の status 比較と、確認対象ログを具体化した（194） |
| S3-7 | 解消 | `--pull-apk=<dir>`、S4-0 でのスクリプト先行コミット、旧 component のパターンを定めた（193、218） |
| S3-8 | 解消 | Supported OS Versions の直下に Android requirements を新設する（151） |
| S3-9 | 解消 | 英・日・韓の移行見出しを個別に定義した（129） |
| S3-10 | 解消 | Browse/Add Kotlin、画像、紹介文、公開 Editor 型を対象に入れた（155、157、179） |
| S3-11 | 解消 | `design-feature` の修正行を `:54,57` に限定した（69） |
| S3-12 | 解消 | 公開から検査の順と、`docs/1.13.0`・`docs/latest` のコミットを追加した（146） |
| S3-13 | 解消 | `commit-msg` の件名形式に `!` を追加する計画になった（187） |
| S3-14 | 解消 | push 禁止の出典を利用者指示と明記した（13） |
| S3-15 | 解消 | 設計・レビュー・J-12 を S4-0 の `docs(artifact)` コミットへ分離した（218） |
| S3-16 | 解消 | `AndroidPin` のプロパティ・コンストラクター・summary も削除対象になった（102） |

### 2. v5 の新規指摘

#### X4-1｜A2｜`MovedFrom` の名前解決方法が変更一覧にない
- 場所: design-v5.md:87
- 問題: `[MovedFrom(...)]` を追加するとしていますが、`using UnityEngine.Scripting.APIUpdating;` または完全修飾名の追加が変更内容に含まれていません。
- 証拠: 現行ファイルに該当 using はありません。例えば Runtime/Android/Clipboard/ClipboardReadResult.cs:3 は `System.Collections.Generic` だけです。リポジトリ全体に既存の `MovedFrom` 利用もありません。
- 修正: S4-3 に「属性を置く各ファイルへ using を追加する」、または完全修飾で書く、と明記する。
- 分類理由: 漏れてもコンパイルエラーで必ず発覚するため A2。

#### X4-2｜B｜42 型照合が属性の全引数を検査しない
- 場所: design-v5.md:126
- 問題: 検査対象が `sourceNamespace` と `sourceClassName` だけです。契約で必須の第 1 引数 `true` と、`sourceAssembly` の省略を検査しません。
- 証拠: 契約は `[MovedFrom(true, ...)]` で `sourceAssembly` を渡さない形です（design-v5.md:89）。アセンブリ名は Runtime/NativeToolkit.Runtime.asmdef:2 の `NativeToolkit.Runtime` です。
- 修正: 42 型すべてで `autoUpdateAPI == true`、期待する旧名前空間、`sourceAssembly` が null または省略、37 型だけ旧 `sourceClassName`、を完全照合する。

#### X4-3｜B｜1.x クラス名の正規表現が 3 件を見逃す
- 場所: design-v5.md:127
- 問題: `android\.(library\|unity)\...` の `\|` は、Python や `rg` の正規表現では選択ではなく文字 `|` を表します。そのため `android.library...` と `android.unity...` に一致しません。
- 証拠: 現行の式では 0 件です。`android\.(library|unity)...` なら manual/1.12.0/notification.md:621、notification.ja.md:621、notification.ko.md:621 の 3 件に一致します。
- 修正: `android\.(?:library|unity)\.[a-z]+\.[A-Za-z.]*[A-Z]` にする。

#### X4-4｜B｜V-S4-4 の旧版 package 生成手順が実行不能
- 場所: design-v5.md:197
- 問題: 「`git archive ...` を `file:` で参照」とありますが、`git archive` の出力は tar ストリームです。展開先ディレクトリがなければ Unity manifest の `file:` package にできません。
- 証拠: 同じ行で新版は「パッケージのコピー」を参照しますが、旧版には展開工程と参照先がありません。
- 修正: `git archive ... | tar -x -C <old-package-root>` など、作業ツリー外への展開手順と、`file:<old-package-root>/Packages/com.jonghyunkim.nativetoolkit` を明記する。

#### X4-5｜B｜V-S4-4 の判定 (e) が API Updater の期待動作と矛盾する
- 場所: design-v5.md:197
- 問題: 「(e) パッケージのコピーの外のファイルが変わらないか」を記録するとしています。しかし (a)・(c) で API Updater が成功すれば、使い捨てプロジェクトの `Assets` 内スクリプトは package コピーの外で変更されます。
- 証拠: 設計自身が、ガードなし・ありの利用者スクリプトを置き、API Updater による書換えを観測対象にしています（design-v5.md:94）。
- 修正: 次の 2 条件に分ける。
  - 使い捨てプロジェクトの `Assets` は期待した差分だけになる。
  - 元の unity-native-plugin 作業ツリーと参照用 package コピーは変わらない。

#### X4-6｜B｜`--pull-apk` の出力先再利用で古い DEX を検査できる
- 場所: design-v5.md:193
- 問題: `<dir>` を実行前に空にする条件と、複数 APK を APK 別ディレクトリへ展開する条件がありません。再実行時の古い `classes*.dex` や、複数 APK 内の同名 `classes.dex` が混ざる可能性があります。
- 証拠: V-S4-1 は陽性対照と削除後の検査を同じ新オプションで行います。現行スクリプトも 1 つの `OUT_DIR` を複数 device run で共有します（scripts/verify_unity_android.sh:208、285）。
- 修正: 各 run で新しい空ディレクトリを作り、APK ごとに別サブディレクトリへ展開する。既存の非空な指定先は失敗させるか、run 固有ディレクトリを内部生成する。

#### X4-7｜B｜`verified_native_toolkit_commit` の記録後に実装レビューがある
- 場所: design-v5.md:223、224
- 問題: 最後の通し検証と `<verified>` の記録の後に実装レビューを行います。レビューで S4-1〜S4-3 を修正しても、再検証と `<verified>` 更新が必須になっていません。
- 証拠: `<verified>` は「最後の通しの検証のとき」の native-toolkit commit を表す契約です（design-v5.md:207）。
- 修正: 実装レビューと修正を先に行い、その後に最後の通し検証、dist clean 確認、`verified_native_toolkit_commit` 記録の順にする。以後 S4-1〜S4-3 が変わったら再検証する。

#### X4-8｜A1｜共通の破壊的変更が各機能ページに置かれない
- 場所: design-v5.md:152、160
- 問題: 利用者決定は「各機能ページの Changes in 1.13.0＋リリースノートの Breaking Changes」です。しかし v5 は旧 AAR 削除、Configure Gradle Project 削除、32 ビット停止、Mono 不可を index だけに置き、各機能ページにはリンクしか置きません。
- 証拠: index 側の専有項目は design-v5.md:153〜157 です。機能ページの内容一覧（161〜167）にはこれらがありません。
- 修正: 各機能ページの Changes 節に、その機能にも影響する共通の破壊的変更を含める。index は requirements とナビゲーションに留め、リリースノートの Breaking Changes から各機能ページの該当アンカーへ直接リンクする。
- 分類理由: この分担のままでも文書生成、リンク検査、リリースは通るため、実装段階では発覚しない。

#### X4-9｜C｜V-S4-1b とコミットの前後関係が章間で異なる
- 場所: design-v5.md:194、219
- 問題: V-S4-1b は `git ls-files` を「コミットの後」に行う一方、7 章は「V-S4-1、V-S4-1b、V-S4-3。1 コミット」と書いており、検証後にコミットする順にも読めます。
- 証拠: 実行前後の status 比較はコミット前、tracked 6 件の確認はコミット後でなければならず、V-S4-1b がコミットをまたいでいます。
- 修正: 「実装、status 保存、スクリプト実行、status 比較とログ確認、コミット、`git ls-files` で 6 件確認」と順序を 1 行で固定する。

### 指摘なしと判断した重点項目
- V-S4-1b の検査内容（前後 status 比較、削除ログなし、コミット後の tracked 6 件）自体は妥当です。指摘は章間の実行順だけです。
- Unity 同梱ツールの指定は実在します。
- 6 章のタグ存在確認、AAR md5、`<verified>` とタグ間の `dist/<dv>/android` 差分というチェックリストの内容は妥当です。問題は `<verified>` 記録後にレビューが置かれている順序だけです（X4-7）。
- `AndroidNotificationJsonBuilder` を残し、Share・Clipboard のビルダーだけを消す範囲は一次ソースと一致します。

### 機械照合
`python3 scripts/check_design_consistency.py <v5>` は終了コード 0 でした。ただし `V-S4-*` は検査スクリプトが認識する `V-<数字>` 形式ではないため、verification ID、件数、擬似コード等は `SKIP` です。この成功は X4-2、X4-3、X4-9 を保証しません。

### 総合評価
- 前回指摘: 26 件すべて解消
- 新規指摘: 9 件（A1 が 1、A2 が 1、B が 6、C が 1）
- **A1 は 1 件（X4-8）**。A1 が残るため、停止基準には達していません。

ファイルおよび Git の状態は変更していません。
