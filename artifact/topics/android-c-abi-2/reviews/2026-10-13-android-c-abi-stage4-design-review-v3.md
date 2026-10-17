# レビュー結果

- 日付: 2026-10-13
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v4.md
- 機能名: android-c-abi-2（段 4）
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 3）と Codex（ラウンド 3）を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力。文言には手を加えていない（見出しの段を下げ、先頭の題を外した）
- A1: サブエージェント 0 件、Codex 2 件（X3-1、X3-7）。止める基準（両方の A1 が 0）には達していない

---

## 1. サブエージェント（Claude）


対象は `artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v4.md` です。読むだけで、リポジトリのファイルは変えていません。一時ファイル（タグ 1.12.0 の展開、AAR の manifest、反射のプローブ、検査の模擬スクリプト）は scratchpad に置きました。

**結論: A1 は 0 件です。止める基準（A1=0）に達しました。** ただし B が 6 件あり、そのうち S3-1 は重いです（高）。

---

### 1. 前のラウンドの指摘の判定

| ID | 判定 | 根拠 |
|---|---|---|
| S2-1 | 解決 | v4:16、2.1 の表（「タグ `1.12.0` の名前空間」の列）、2.3（`sourceNamespace`）、4 章の移行の節（v4:151）、V-S4-4（1.12.0 の書き方）。名前空間は 44 型すべてを `git show 1.12.0` と HEAD で照らし、表と一致した。ただし検査が効いていない（X2-4 と S3-1） |
| S2-2 | 解決 | v4:142（`index{,.ja,.ko}.md:185`、`Documentation~/index*.md:95`、画像）。検査 `Config\w* Gradle Project` は「Configue」も韓国語版の「…Project를」も拾う。続く手順の行と、パッケージ側の画像が残る（S3-10） |
| S2-3 | 解決 | V-S4-1b（v4:176）を、`git status --porcelain`、`Deleted AAR not in the pin`（`PreBuildProcessor.cs:447`）、`git ls-files` が 6 つ、の 3 つで判定する形にした。`androidlib` の `.meta` は `.gitignore:92` で無視され、フォルダは空なので、status には出ない。いつ流すかに穴がある（S3-6） |
| S2-4 | 解決 | 道具のパス（v4:175）は実在し、動いた: `build-tools/36.0.0/dexdump`、`cmdline-tools/16.0/bin/apkanalyzer`、`OpenJDK`。陽性の対照も置いた。順と細部は S3-7 |
| S2-5 | 部分 | `<verified>` の行を足した（v4:191）。しかし置き場所が「実装結果」で、汎用のリリース手順からは読めない。作業ツリーが clean だったことの記録も無い（S3-5） |
| S2-6 | 解決 | v4:167 で出す条件を決めた。commit-msg の決まりとのずれは S3-13 |
| S2-7 | 部分 | 置き場所を決め（v4:150）、1.x の AAR の項目も足した（v4:154）。しかし置き場所に指定した「Android の動作環境」の節が、`manual/1.12.0/index.md` に無い（S3-8） |
| S2-8 | 解決 | v4:55（「足す」、9 件）、v4:67（手で直す） |
| S2-9 | 解決 | 3.4 を段ごとに分け、許す行をファイルと節で決め、途中のコミットは除外した。言語ごとの見出しの扱いは S3-9 |
| S2-10 | 解決 | `VERSION.txt:15-16,22-24,30-32`、`PreBuildProcessor.cs:220-229,231-235,416,186-188,100-105` は実ファイルと一致した。2.2 で直した後の文言も決めた |
| S2-11 | 解決 | `common.md:45` は 7 章の S4-1、`testing.md` は 7 章のステップ 3、`package.json` は S4-5 だけ（v4:169、195） |
| S2-12 | 解決 | v4:114 |
| S2-13 | 部分 | 確かめ方と合否の (a)〜(d) は決まった。しかし実施の時点が「S4-3 の前」で、確かめる対象がまだ無い。`#if` のガードも考えていない（S3-2、S3-3） |
| X2-1 | 解決 | v4:17、2.3。結果によらず 42 型に付ける。enum にも付けられることを確かめた（S3-2） |
| X2-2 | 解決 | V-S4-1b、V-S4-3、6 章で完全なパスにした |
| X2-3 | 解決 | `--pull-apk` で、テストが入れた Player を取る（v4:175）。`TEST_PLAYER_ID` は `verify_unity_android.sh:164` と一致する。各実行の頭の `uninstall_app` の前に取る、という順も正しい |
| X2-4 | **未解決** | 検査の決まり（v4:125）は「ブロックの中に `Android[A-Z]\w*` があること」を条件にしている。1.12.0 の文書では、旧い `using` を持つ Android のコードブロック 45 個のうち、検査に掛かるのは 3 個だけだった。X2-4 が挙げた `Documentation~/index.md:137-147` も掛からない（S3-1） |
| X2-5 | 解決 | 3.4 の表の段ごとの行 |
| X2-6 | 解決 | 4 章の順の 1（起点のコミット）。push の扱いは S3-14 |

---

### 2. v4 の新しい指摘

#### S3-1 ｜ B ｜ 高 ｜ 3.4「旧い名前空間の組」（v4:125）
**問題**: 0 件にする条件が「`Android` で始まる型を使い、`Runtime.Android.` の `using` が無く、旧い `using` があるブロック」です。しかし Android の「Import the namespace」のブロックは、`using` の 1 行と、ガードと、コメントしか持ちません。コメントは「Guard: Android (Player) only」で、`Android` の後が空白なので、`Android[A-Z]` に当たりません。旧い `using` を残したままでも、検査は通ります。

**根拠**（v4 の決まりを Python で写し、1.12.0 の文書に流した）:
- 旧い `using` を持つ Android のブロック 45 個のうち、掛かるのは 3 個（`clipboard*.md` の Setup だけ）。漏れる 42 個は次のとおり:
  - `dialog{,.ja,.ko}.md:35,97,162,232,302,375,…`
  - `share*.md:67`
  - `notification*.md:82`
  - `Documentation~/index{,.ja,.ko}.md:143,204,268,337,406,478`
- 45 個はすべて `UNITY_ANDROID` を含む。iOS・macOS・Windows のブロック 105 個は 1 個も含まない。両方を含むブロックは無い。

**提案**: 判定を「`UNITY_ANDROID` を含み、旧い `using JonghyunKim.NativeToolkit.Runtime.(Dialog|Share|Clipboard|Notification);` を含むブロック」が 0 件、に変える。1.12.0 の文書では 45/45 を拾い、誤検出は 0 です。

#### S3-2 ｜ B ｜ 中 ｜ 2.3 (a)・(d)、v4:17、V-S4-4、移行の節
**問題**: 3 点あります。
- **37 型には API Updater がまったく効かない見込みが高い。** v4 は「名前空間の移動は直る」と見込み、37 型の名前空間の部分も (a) で確かめるとしていますが、この見込みは外れる可能性が高いです。
- **(d) は確かめ済み。** enum には付けられるので、2.3 の「要確認」は閉じられます。
- **V-S4-4 の試しのスクリプトがガードを持たない。** マニュアルが勧める書き方は `#if UNITY_ANDROID && !UNITY_EDITOR` で、`using` と呼び出しを Editor のコンパイルから外します（`dialog.md:37-39` ほか）。API Updater は Editor のコンパイルのエラーを起点に動くので、ガードの中のコードは直らない見込みです（要確認）。ガードの無いスクリプトで (a) が PASS しても、実際の利用者のコードには当てはまりません（偽の PASS）。

**根拠**: Unity 6000.4.2f1 の `UnityEngine.CoreModule.dll` を、同梱の mono の反射で読みました。
- `MovedFromAttribute.AffectsAPIUpdater` は、`sourceClassName` を渡すと `false`、`sourceNamespace` だけなら `true` を返す。
- `AttributeUsage` は 5148（Class、Struct、Enum、Interface、Delegate）。
- コンストラクタは `(bool autoUpdateAPI, string sourceNamespace = null, string sourceAssembly = null, string sourceClassName = null)`。
- アセンブリ名は 1.12.0 も HEAD も `NativeToolkit.Runtime` なので、`sourceAssembly` は要らない。2.3 の引数は正しい。

**提案**:
- 2.3 に上の事実を書き、(d) を閉じる。
- (a) は 2 本のスクリプトで見る: ガード無しと、マニュアルのガード付き。
- 移行の節は「37 型は `using` も名前も手で直す」を既定にして書いておき、V-S4-4 の結果で「自動で直るもの」を足す。

#### S3-3 ｜ B ｜ 中 ｜ V-S4-4 の時点（v4:179「S4-3 の前」、7 章のステップ 1）
**問題**: S4-3 の前のパッケージには、改名した型も `[MovedFrom]` もありません。`file:` で「このパッケージ」を参照しても、(a)〜(c) は確かめられません。

(c) には、さらに次の手順が要りますが、書かれていません:
1. タグ `1.12.0` のパッケージで、旧名の Payload を `[SerializeReference]` に保存する。
2. 改名後のパッケージに差し替えて、読み直す。

X2-1 で「結果によらず付ける」と決めたので、確かめを前に置く理由はもうありません。

**提案**:
- V-S4-4 を S4-3 の実装の後、S4-4 の前に移す。
- 手順を書く:
  1. `git archive 1.12.0` のパッケージで使い捨てのプロジェクトを作り、スクリプトとアセットを保存する。
  2. manifest を、S4-3 のパッケージのコピー（リポジトリの外）に向けて開き直す。
- 参照先をリポジトリの外のコピーにすれば、Updater がパッケージを書き換えても作業ツリーは汚れない。

#### S3-4 ｜ B ｜ 中 ｜ V-S4-2 と 7 章のステップ 4（S4-3 の後の検証）
**問題**: S4-3 では、層 2b の 6 ファイルの旧名も置き換えます。旧名は 6 ファイルで計 158 か所あります。これらは `#if UNITY_ANDROID`（Share と Notification は `!UNITY_EDITOR` も）の中にあり、テストのアセンブリ（`optionalUnityReferences: TestAssemblies`）に入ります。

そのため、次の 2 つではコンパイルされません:
- V-S4-2 の「Editor（EditMode・PlayMode）」
- ふつうの「Player のビルド」（テストのアセンブリは入らない）

しかも V-S4-2 は Player のビルドのやり方を書いていません（Android™ Profile は書き出しの設定）。S4-3 の後に実機での実行が無いまま S4-4（文書）を経てリリースに進むので、改名した 2b のテストは、一度もコンパイルされずに出ていきます。

**提案**: 7 章のステップ 4 に `verify_unity_android.sh --as-package-user` の通しの実行を足す。これを「最後の通しの検証」とし、`<verified>` を記録する（S3-5）。

#### S3-5 ｜ B ｜ 中 ｜ 6 章の `<verified>`（v4:191）
**問題**: 3 点あります。
- `<verified>` は「実装結果」に書くとしていますが、`release/workflow.md` は汎用の手順で、課題ごとの実装結果のパスを知りません。
- Android を触らない今後のリリースでは、何を `<verified>` にするかが決まっていません。
- `AndroidGradleDependenciesTests` は native-toolkit の**作業ツリー**を読みます（`DistRoot()`、同テストの 150 行付近）。検証のときに `dist/<dv>/android` が未コミットで変わっていると、コミットの ID だけでは、テストしたものを表せません。

**提案**: 次のどちらかにする。
1. `<verified>` を固定の場所に置く。例えば `Plugins/Android/VERSION.txt` の新しいキー（`ReadPinValues` は知らないキーを無視する。`PreBuildProcessor.cs:136-160`）。あわせて、検証のときに `git -C ../native-toolkit status --porcelain -- dist/<dv>/android` が空だったことを記録する。
2. リリースのときに native-toolkit をタグにして、EditMode の `AndroidGradleDependenciesTests` を流す（S2-5 の第 2 案）。`<verified>` は要らなくなる。

#### S3-6 ｜ B ｜ 低〜中 ｜ V-S4-1b の時点（v4:176、7 章のステップ 2）
**問題**: 「スクリプトの後に `git status --porcelain -- …/Plugins/Android` が空」は、S4-1 をコミットした後でしか成り立ちません。コミットの前だと、次の 2 つで空になりません:
- `git rm` した 4 ファイル（`D `）
- 直した `VERSION.txt`（`M `）

7 章のステップ 2 は「V-S4-1、V-S4-1b、V-S4-3。1 コミット」と書いていて、検証が先とも読めます。

`Deleted AAR not in the pin` を探すログも決まっていません。build steps が動くのは Android™ Profile の実行（`$OUT_DIR/Android.log`、`Android-retention.log`）だけで、Test の profile は build steps を持ちません（`verify_unity_android.sh:9-14`）。

**提案**: V-S4-1b は S4-1 のコミットの後に流す。または、実行の前の status を控えて比べる（スクリプトの `CHANGED_BEFORE` と同じ形）。ログのファイル名も書く。

#### S3-7 ｜ C ｜ 低 ｜ V-S4-1 の細部と順
- **順の食い違い**: 陽性の対照（7 章のステップ 1）は `--pull-apk` を使いますが、7 章は `--pull-apk` を S4-1 のコミット（ステップ 2）に入れています。スクリプトの変更を S4-1 より前の別コミットにするか、ステップ 1 は未コミットのスクリプトで流すと書く。
- **引数の解析**: 今の解析は `for arg in "$@"; case …` で、値を取る引数を受けません（`:83-90`）。`--pull-apk <dir>` の `<dir>` は「unknown argument」で exit 2 になります。`--pull-apk=<dir>` にするか、解析を直す。
- **「旧い component」の定義が無い**: `apkanalyzer manifest print` の出力の `android\.(unity|library)\.` と定義する。2.0.0 の AAR 2 本の manifest では 0 件、1.x は 5 件と 1 件でした（AAR を展開して確かめた）。

#### S3-8 ｜ A2 ｜ 低 ｜ 4 章の共通の移行の節の置き場所（v4:141、150）
**問題**: 「index の Android の動作環境の下」に置くとしていますが、`manual/1.12.0/index.md` にはその節がありません。見出しは次の 13 個だけです:
- Version、Supported OS Versions
- Features（Android ほか）
- Getting Started（Installation、Samples）
- API Usage

「Android の動作環境: ARM64/IL2CPP」（v4:141）をどこに新しく作るかも決まっていません。

**提案**: 例えば「# Supported OS Versions」の下に「## Android requirements」を新しく作り、その直後に「## Changes in `1.13.0` (Android)」を置く。3 言語の見出しと、各機能のページからのアンカーも決めておく。

#### S3-9 ｜ C ｜ 低 ｜ 3.4 の許す行と、Breaking Changes を出す条件（v4:124、167）
**問題**: 許す行は「Changes in `1.13.0`」の節で決めていますが、日本語版と韓国語版の見出しは訳されています。前例は `dialog.ja.md:831`「### 1.12.0 での変更」と `dialog.ko.md:831`「### 1.12.0 변경 사항」です。英語の見出しだけで見分けると、日本語版と韓国語版の移行の節がすべて当たります。

**提案**: 言語ごとの見出しを決めて、検査に書く。リリースの条件は英語のファイルで判断すると書く。

#### S3-10 ｜ C ｜ 低 ｜ 「Configure Gradle Project」を消した後の残り（v4:142、161）
**問題**: 次の 3 つが残ります。
- **続く手順の行**: 手順は 185 行の後も続きます（「Click "Browse"…」「Click "Run: Add Kotlin Dependencies"…」。`index.md:190-191`、`Documentation~/index.md:98-99`）。検査の正規表現はこの行を拾いません。
- **パッケージ側の画像**: `Documentation~/images/editor/ConfigureGradleProject.png` は git で管理され、パッケージに入ります。参照を消すと、どこからも使われなくなりますが、扱いが決まっていません。
- **古くなる紹介文**: 「Editor windows help integrate … Gradle/Xcode settings」が古くなります（`README.md:5`、`manual/1.12.0/index.md:7`、`package.json` の description）。

**提案**:
- 検査に `Add Kotlin Dependencies` を足す。
- `Documentation~` の画像の扱いを決める。
- 紹介文の直しを 4 章に書く。
- 1.12.0 の public の Editor の型 `AndroidConfigureProjectWindow`（グローバルの名前空間）が消えたことを、移行の節の同じ項目に一言書く。

#### S3-11 ｜ C ｜ 低 ｜ 2.2 の `design-feature/workflow.md:52,54`
**問題**: この 2 行は「既知の逸脱 11 件」の記述ではありません。
- `:52` は `Common/` の説明で、正しい。
- `:54` と `:57` は「Android / iOS / macOS は `Runtime/<Feature>/`（未移行の形）」と書いている。段 3-0 から古くなっている（`common.md:403` は「Windows と Android は移行済み」）。

**提案**: 直す行を `:54,57` にし、中身は「Android を移行済みに入れる」と書き直す。

#### S3-12 ｜ C ｜ 低 ｜ 4 章の順 5（v4:138）
**問題**: 「3.4 の検査、`verify_manual.sh`、`publish_docs.sh`」の順では、検査 7（docs の同期。止める項目）が落ちます。`docs/1.13.0` のコミットも書かれていません。

**提案**: `publish_docs.sh`、`verify_manual.sh` の順にし、`docs/1.13.0` のコミットを足す。

#### S3-13 ｜ C ｜ 低 ｜ 件名の `!`（v4:167）
**問題**: `agent-rules/workflows/commit-msg/workflow.md:23` は件名の形を `<type>(<scope>): <subject>` に決めていて、`!` を持ちません。`/commit-msg` で作ると `!` が落ちるおそれがあります。

**提案**: S4-4 で、リリースの手順と一緒に commit-msg の手順にも `!` の書き方を足す。

#### S3-14 ｜ C ｜ 低 ｜ 「push はしない: このリポジトリの約束」（v4:134）
**問題**: agent-rules にこの約束は見つかりませんでした。`write-manual/workflow.md:17` は「コピー直後にコミット・プッシュ」を求め、`agent-rules/index.md:51` は「push の前に確かめる」です。

**提案**: 出典を書くか、「push は利用者に確かめる（`index.md:51`）」と書き直す。

#### S3-15 ｜ C ｜ 低 ｜ 7 章（設計の文書と README の未コミットの変更）
**問題**: `artifact/topics/android-c-abi-2/README.md` に、J-12 の行を足した未コミットの変更があります。S4-3 で README を直すと、J-12 がそのコミットに混ざります。設計 v1〜v4 とレビューを、いつコミットするかも書かれていません。

**提案**: ステップ 0 に `docs(artifact)` のコミットを置く。

#### S3-16 ｜ C ｜ 低 ｜ 3.1 の `PreBuildProcessor`
**問題**: 消す対象に、次の 2 つが挙がっていません。
- `<summary>` の `:210-212`（「or when only one of the two extra_* keys is given」）
- プロパティとコンストラクタの `:197-207`

`grep extra_` は XML コメントを除外するので、`:211` の残りに気づけません。

**提案**: 上の 2 つを直す対象に入れる。

---

### 3. 確かめたこと（指摘なし）
- **44 型**: `Runtime/Android` の public の型は 44（`Android` で始まらないもの 37、始まるもの 7）。
  - タグ `1.12.0` では、すべてが `Runtime.<Feature>` の 1 ファイルに 1 回だけ宣言されている。public で、同じ名前の型はほかに無い。
  - HEAD はすべて `Runtime.Android.<Feature>`。2.1 の表の名前空間の列と、42 型（37 と 5）の内訳は正しい。
  - アセンブリはどちらも `NativeToolkit.Runtime`。
  - `Runtime.Android.Common` と `Editor.Android` は、新しい internal の型だけ。
- **S4-3 の検査で残るもの**: Runtime、Tests、agent-rules で、37 の旧名の当たり（単語の境界）は 2.2 の対象とコメント 1 か所（`IosClipboardOperationResult.cs:15`）、`common.md:459-461`、`review-document/workflow.md:46` だけ。2 ビルダーの名前は、`AndroidClipboardCApi.cs:103`、`WindowsClipboardJsonBuilder.cs:18`、`IosShareJsonBuilder.cs:12` だけ。どれも設計の対象に入っている。
- **1.x のクラス名の正規表現**: 1.12.0 の文書で当たるのは、`notification*.md:621` の 3 か所（消す対象）だけ。
- **native-toolkit**: 作業ツリーは clean。`dist/1.13.0/android` の AAR と `m2/` は git で管理されていて、LFS ではない。タグの名前は dist の版と同じ形（`1.12.0`）。
- 残り 9 型（iOS 5、macOS 3、`IconConfiguration`）の数え方は正しい（入れ子の型を除く）。

---

**A1 は 0 件です。止める基準（A1=0）に達しました。** 実装に入る前に、B の 6 件（S3-1〜S3-6）、特に S3-1（名前空間の検査が 45 個中 42 個を見逃す）を直すことを勧めます。
---

## 2. Codex

### 1. 前回指摘の判定

42型の集合は一次ソースと一致しました。現行 `Runtime/Android` の public 型は44型で、削除対象の `AndroidShareJsonBuilder` と `AndroidClipboardJsonBuilder` を除くと42型です。タグ `1.12.0` でも対象型はそれぞれ `Runtime.Dialog` / `Share` / `Clipboard` / `Notification` に存在します。

| ID | 判定 | 根拠 |
|---|---|---|
| X2-1 | 一部解消 | 42型へ結果にかかわらず付ける方針と、スクリプト更新・シリアライズの分離は追加された。`design-v4.md:86-89`。ただし enum だけ「付けられなければ付けない」として42型の決定を破っている。`:90`（X3-1） |
| X2-2 | 解消 | `git status` と `git ls-files` は package を含む完全パスになった。`design-v4.md:176` |
| X2-3 | 一部解消 | APKの取得元をインストール済みテストPlayerに変更し、profile依存は解消した。`:175`。ただし `pm path` の解析・APKからDEXを取り出す工程がない（X3-4） |
| X2-4 | 解消 | manual、`Documentation~`、READMEを対象とし、Android型を含むコードブロック単位の旧名前空間検査を追加した。`:122,125` |
| X2-5 | 解消 | S4-3とS4-4で検査パスを分離し、S4-3では未作成の`manual/1.13.0`を参照しない。`:119-126` |
| X2-6 | 一部解消 | baselineコピー直後のコミットは追加された。`:133-135`。ただし必須のpushを明示的に省き、後半の変更にもコミットが割り当てられていない。`write-manual/workflow.md:13-19`、X3-9・X3-10 |
| S2-1 | 一部解消 | 42型の名前空間移動、旧名前空間を含む`MovedFrom`引数、移行説明を追加した。`design-v4.md:16,40-51,86`。enumを属性対象外にできる分岐だけ残る。`:90` |
| S2-2 | 解消 | manual・`Documentation~`から手順と画像参照を消し、表記揺れを含む検査を追加した。`:122,142` |
| S2-3 | 一部解消 | 完全パス、削除ログ、tracked 6件の条件を追加した。`:176`。しかし検証をコミット前に置くため、正しいS4-1変更自身が`git status`に出る（X3-5） |
| S2-4 | 一部解消 | Unity同梱の`dexdump`・`apkanalyzer`・OpenJDKと陽性対照を指定した。`:175`。DEX展開工程等は未確定（X3-4） |
| S2-5 | 解消 | `<verified>`とタグ候補間の`dist/<dv>/android`差分検査を追加した。`:189-194` |
| S2-6 | 解消 | Breaking Changesを出す条件と`!`付きコミットを定めた。`:165-167` |
| S2-7 | 解消 | 共通項目の置き場所と旧AAR削除の説明を具体化した。`:149-158`。ただし今回の利用者決定との不一致は別途X3-7 |
| S2-8 | 一部解消 | 残す9型、表への追加、見出し9件、履歴の手修正を明記した。`:55,67`。一方、`os-prefix-violations/README.md:112`の「公開81型・13%」を更新する指示は明記されていない |
| S2-9 | 解消 | 段ごとのパス、許可するファイル・節、最後のコミットだけに課す条件を定めた。`design-v4.md:117-127` |
| S2-10 | 解消 | 章番号、`VERSION.txt`、`PreBuildProcessor`の範囲、他OSコメントの表現を修正した。`:80-82,97-99,161` |
| S2-11 | 解消 | `common.md:45`をS4-1、builder関連規則をS4-2、版上げをS4-5へ割り当てた。`:102,113-115,169,199-202` |
| S2-12 | 解消 | Share・Clipboardはbuilderなし、Notification builderはManagerから呼ばないというAndroid例外を追加する。`:114` |
| S2-13 | 一部解消 | `file:`、`-accept-apiupdate`、旧記法、結果の分離、`Assets/`外不変を追加した。`:179`。ただし属性実装前に走らせる順序では提案属性を検証できない（X3-3） |

### 2. 新規指摘 X3-*

### X3-1｜A1｜42型という決定をenumだけ撤回できる

- 場所: `design-v4.md:30,86,90`
- 問題: 42型すべてに`[MovedFrom]`を付けるとした直後に、`AndroidClipboardReadStatus`だけ「付けられなければ付けない」としている。実装者がこの分岐に従えば、41型でもコンパイルと通常テストは通る。
- 証拠:
  - 対象は実際にenum。`Runtime/Android/Clipboard/ClipboardReadResult.cs:12`
  - Unity 6000.4.2f1の一次バイナリ `UnityEngine.CoreModule.dll` の`MovedFromAttribute`は`AttributeUsage`にEnum・Class・Structを含む。
  - コンストラクターは`(bool autoUpdateAPI, string sourceNamespace = null, string sourceAssembly = null, string sourceClassName = null)`であり、v4の名前付き引数は有効。`sourceAssembly`省略も同一assemblyなので妥当。
- 修正: `:90`の条件分岐を削除し、enumを含む42型すべてを必須にする。改名37型は旧`sourceNamespace`＋旧`sourceClassName`、名前を残す5型は旧`sourceNamespace`だけと固定する。

### X3-2｜B｜3.4は`MovedFrom`42件と引数を保証しない

- 場所: `design-v4.md:117-127,179-180`
- 問題: 3.4は旧名の残存を探す負の検査であり、属性の欠落、誤った`sourceNamespace`、37型の`sourceClassName`取り違え、5型への不要な`sourceClassName`を検出しない。V-S4-4も例示型だけで、42型の網羅検査ではない。
- 証拠: 属性で旧値を許すことは書かれているが、属性数や期待引数を照合する検査はない。`:124`。機械照合もv4に対して`FAIL 0`だが、`V-S4-*`と反復件数は`SKIP`した。
- 修正: 3.4に次の機械検査を追加する。
  - 削除後の対象public型がちょうど42型。
  - 各型に`MovedFrom`がちょうど1個。
  - 全42型の`sourceNamespace`がタグ`1.12.0`と一致。
  - 改名37型の`sourceClassName`が旧名と一致。
  - 名前を残す5型には`sourceClassName`を指定しない。

### X3-3｜B｜V-S4-4を属性実装前に実行している

- 場所: `design-v4.md:179,199-202`
- 問題: V-S4-4はS4-3の前だが、参照する`file:` packageはその時点ではまだ改名も`MovedFrom`追加もされていない。提案する属性の効果を検証できない。
- 証拠: 現行packageは既に新名前空間だが旧クラス名のままである。例: `AndroidClipboardManager.cs:4`、`ClipboardReadResult.cs:12`。S4-3の変更は順序7章のステップ4。`:202`
- 修正: 作業ツリー外にpackageのコピーを作り、そこで予定する改名・属性を適用してV-S4-4を実行するか、S4-3実装後・コミット前へ移す。旧版fixtureと新版packageの二段階を明記する。

### X3-4｜B｜`--pull-apk`後のDEX検査工程が成立していない

- 場所: `design-v4.md:175,199-200`
- 問題: `adb shell pm path`の`package:`除去、複数APKへの対応、取得したAPKから`classes*.dex`を取り出す工程がない。`dexdump`へAPKを直接渡す設計にもなっていない。
- 証拠:
  - Unity同梱`dexdump`のusageは入力を`dexfile...`としており、APK内DEXを自動列挙する仕様ではない。
  - 現行スクリプトは各run前と最後にPlayerをアンインストールする。`scripts/verify_unity_android.sh:208-217,299-300`
  - `apkanalyzer`は`manifest print <apk>`および`dex list <apk>`を提供する。
- 修正: `pm path`の全行を解析して全APKをpullし、各APKのDEXを`apkanalyzer dex list`で列挙して展開後に`dexdump`へ渡す。manifestをどのAPKで確認するか、pull失敗・DEXなしを失敗にする条件もスクリプト化する。

### X3-5｜B｜V-S4-1bの`git status`は正しい変更でも必ず非空になる

- 場所: `design-v4.md:176,200`
- 問題: S4-1を実装し、検証し、その後1コミットする順である。このためV-S4-1b時点では旧AAR4ファイルの削除と`VERSION.txt`変更が未コミットであり、`git status ... Plugins/Android`は空にならない。
- 証拠:
  - 現在はtracked 10ファイルで、完成後は6ファイル。旧AAR2本と各`.meta`の削除が検証時点で表示される。
  - コミットはV-S4-1bの後。`design-v4.md:200`
- 修正: 検証前後のstatus差分を比較し、S4-1で予定した変更以外が増えていないことを確認する。またはS4-1を一旦コミットしてから検証し、修正があればamendする。

### X3-6｜B｜リリースチェックリストのローカルAARパスが不完全

- 場所: `design-v4.md:189-191`
- 問題: `VERSION.txt`には完全パスを使う一方、md5対象を`Plugins/Android/<aar>`と書いている。リポジトリルートからは存在しない。
- 証拠: 実体は`Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/<aar>`。既存release workflowもPluginsディレクトリ確認には完全パスを使う。`release/workflow.md:39-40`
- 修正: 追加する行の全ローカルパスを`Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/...`に統一し、`refs/tags/<dv>`の存在確認も具体的なコマンドにする。

### X3-7｜A1｜移行内容の本体が各機能ページに置かれない

- 場所: `design-v4.md:149-159,167`
- 問題: 利用者決定は各機能ページの「Changes in 1.13.0」に移行を書くことだが、v4は名前空間42型と改名37型の本体をindexの共通節に置き、機能ページにはリンク・既知差分・builder削除だけを書く。各機能ページだけを読んだ利用者は、その機能の改名表を得られない。
- 証拠: 共通indexに全改名表を置く。`:150-157`。機能ページの必須内容は共通節へのリンク等だけ。`:158`
- 修正: dialog/share/clipboard/notification各ページの「Changes in 1.13.0」に、その機能の旧名前空間、該当型の旧名→新名、`MovedFrom`で自動化される範囲、手修正、既知差分を置く。indexは要約と4ページへのリンクに留める。Breaking Changesからも該当ページへリンクする。

### X3-8｜B｜`verify_manual`と`publish_docs`の順序が逆

- 場所: `design-v4.md:138,204`
- 問題: `verify_manual.sh 1.13.0`を`publish_docs.sh 1.13.0`より先に実行するため、新規`docs/1.13.0`が存在せず必ず停止する。
- 証拠:
  - `verify_manual.sh`は`docs/<version>`がなければblocking failureを返す。`scripts/verify_manual.sh:428-433,447-482`
  - `docs/1.13.0`を作るのは`publish_docs.sh`。`scripts/publish_docs.sh:44-57,96-97`
- 修正: 3.4のsource検査 → `publish_docs.sh 1.13.0` → `verify_manual.sh 1.13.0`の順にする。

### X3-9｜A2｜S4-4後半と公開結果にコミットが割り当てられていない

- 場所: `design-v4.md:133-138,204`
- 問題: index・4機能のコミットはあるが、README、`Documentation~`、release workflow、`docs/1.13.0`、`docs/latest`をいつコミットするかがない。そのままS4-5へ進むとrelease workflowの未コミット変更チェックで止まる。
- 証拠:
  - `publish_docs.sh`は`docs/<version>`と`docs/latest`を書き換える。`scripts/publish_docs.sh:51-57,86-93`
  - release前提は`git status --porcelain`が空。`release/workflow.md:37-40`
- 修正: README・`Documentation~`・workflow変更のコミットと、publish後の`docs/1.13.0`・`docs/latest`同期コミットを順序7章に明記する。

### X3-10｜C｜baselineをpushしない説明が一次ルールと矛盾する

- 場所: `design-v4.md:134`
- 問題: 「このリポジトリの約束でpushは利用者が行う」としているが、その約束は一次ルールにない。write-manualはbaselineコピー直後のcommit・pushを必須としている。
- 証拠: `agent-rules/workflows/write-manual/workflow.md:13-19`。commit workflowも利用者確認後にagentがcommit・pushする。`agent-rules/workflows/commit-msg/workflow.md:43-51`
- 修正: 「利用者承認後にbaselineをcommit・pushする」とする。pushを利用者だけに限定するなら、先に正本workflowを変更し理由を記録する。

**A1 件数: 2件（X3-1、X3-7）**

読み取り専用で実施し、ファイルおよびGit状態は変更していません。