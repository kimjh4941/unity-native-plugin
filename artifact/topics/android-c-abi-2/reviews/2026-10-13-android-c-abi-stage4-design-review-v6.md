# レビュー結果

- 日付: 2026-10-17
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v7.md
- 機能名: android-c-abi-2（段 4）
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 6）と Codex（ラウンド 6）を並行で回した。どちらも読むだけ。止め方を見直した `agent-rules/workflows/review-document/workflow.md`（2026-10-13）に従う、初めての差分レビュー（v6 → v7 の差分と、レビュー v5 の指摘の判定だけ）。1 章はサブエージェントの出力、2 章は Codex の出力。文言には手を加えていない（見出しの段を下げ、先頭の題を外した）
- A1: サブエージェント 0 件、Codex 1 件（X6-1。差分で変えた 7 章のステップ 5 への指摘で、A1 の 3 条件を満たす）。止める基準には達していない

---

## 1. サブエージェント（Claude）

**結論: A1 は 0 件です。** v7 で入れた S5-1 の直し（37 型は第 1 引数を `false`、5 型は `true`）は、Unity 6000.4.2f1 の DLL を IL で読んで正しいことを確かめました。新しい指摘は 4 件で、内訳は B 2 件、C 2 件です。

リポジトリのファイルは変えていません。IL を読む道具（Mono.Cecil を使う小さなプログラム）は scratchpad の `ildump/` に置きました。`git status` はレビューの前と同じです。`python3 scripts/check_design_consistency.py <v7>` は終了コード 0 でした（V-*、件数、擬似コードの検査は SKIP）。

---

### 1. レビュー v5 の指摘の判定

| ID | 判定 | 根拠・コメント |
|---|---|---|
| S5-1 | 直した | 2.3（v7:98,102-104）、3.4 の照合（:141）、V-S4-4 の (a0)(a)(b)(d)（:242-243）。IL で確かめた結果は 2 章 |
| S5-2 | 直した（一部は記録漏れ） | 3 段とも `-buildTarget Android` で開く（:234）。タグ `1.12.0` の Payload はガードが無く（`git show 1.12.0:…/AndroidClipboardPayloads.cs`）、`MakeAsset.cs` もコンパイルできる。提案の後半（`$W/proj` の git に `Library/` などを ignore する）は採っておらず、8 章にも無い。差分を見る範囲は (e) で `Assets/` に絞っているので実害は無い（S6-4） |
| S5-3 / X5-3 | 直した | :219。`run_device_tests` が呼ばれるのは `verify_unity_android.sh:286,290,295`、最後のアンインストールは `:300`。v7 の記述と合う。実装では、取る処理を `run_device_tests` の `run_tests` の直後（`:216` の後）に置くと、3 つの実行に 1 か所で効く |
| S5-4 | 直した | :13 に `commit-msg/workflow.md:51` を足し、誤っていた `:126` の前提を外した。:279 でパスを指定して `git add` する、と書いた。パスの指定漏れは、リリースのチェックリストの「`git status --porcelain` が空」（`release/workflow.md:38`）で捕まる |
| S5-5 / X5-7 | 直した（一部は記録漏れ） | ステップ 5（:274）。S5-5 の後半（S4-0 の `--pull-apk` のスクリプトと、S4-4 の実装のレビューを置くか、置かない理由）は、本文にも 8 章にも無い（S6-4）。やり直しの手順に穴がある（S6-2） |
| S5-6 | 8 章に記録。理由は成り立つ | 除き方が効かなければ、今も公開しているアクション ID が検査に当たって落ちる。黙って通る向きには壊れない |
| S5-7 | 8 章に記録。理由は成り立つ | Breaking Changes の節は、移行の見出しがあれば出る（4.4、:202） |
| S5-8 | 8 章に記録。**理由が成り立たない** | `verify-manual` の言語差分の検査は、画像と見出しの数しか見ない（`verify-manual/workflow.md:26`）。3 言語とも直し忘れた文は捕まらない（S6-3） |
| S5-9 | 8 章に記録。理由は成り立つ | リリースノートを書く時点で決めることなので、そこで扱えばよい |
| S5-10 | 8 章に記録。理由は成り立つ | (a)〜(d) とも、扱う時点と決め方が書かれている。(d) は、漏れても ❌ が誤って出る（安全な側に倒れる） |
| X5-1 | 8 章に記録。理由は成り立つ | 補足: 正規表現は `android.unity.` の後に `[a-z]+\.` を求めるので、共通の破壊的変更の文の `android.unity.*` 自体には当たらない。当たるのは、Notification の「`<service>` の行を消す」の説明に旧いクラス名を書いたとき。どちらにしても検査が落ちる向きで、黙っては通らない |
| X5-2・X4-4 の残り | 8 章に記録。理由は成り立つ | `tar -C` は、先のフォルダが無いと失敗する。展開がぶつかっても、「dex が 0 本は失敗」（:222）で捕まる |
| X5-4 | 8 章に記録。理由は成り立つ | Player のビルドでは `UNITY_ANDROID && !UNITY_EDITOR` が真になり、旧名は解決できない。移行の節に書く文（Player のビルドでエラーになる）は安全な側の記述 |
| X5-5 | 8 章に記録。理由は成り立つ | 7 章の順で行う、と決めてある |
| X5-6 | 8 章に記録。理由は成り立つ | 範囲が広すぎても、誤って止まる側に倒れる |

未対応は無し。8 章の理由が成り立たないのが 1 件（S5-8）、提案の一部が記録から漏れているのが 2 件（S5-2、S5-5）です。

---

### 2. 差分で変えた部分の確認

#### 2.1 S5-1 の直し（Unity 6000.4.2f1 の DLL を IL で読んだ）

**Script Updater の側**（`Resources/BuildPipeline/Compilation/ApiUpdater/`）

- `APIUpdater.Framework.dll` の `ConfigurationCollector::CollectTypeIfAppropriated` は、次の順に判定する。
  - 第 1 引数が `false` なら、何もせずに戻る（IL_0072-0079）。
  - `true` で `sourceClassName` があれば、`ArgumentException` を投げる。`sourceAssembly` が無いときは IL_021d-0261 の文言で、あるときは IL_01c9 の「rename a type *and* move to a new assembly」の文言。
  - `true` で `sourceClassName` が無ければ、`sourceNamespace` から `MoveToNamespaceConfig` を作る（IL_0153→IL_02fc-0353）。
  - 入れ子の型に `true` を付けると例外になる（IL_0262）。2.1 に「入れ子の public の型は無い」とあるので、5 型は当たらない。
- 呼び出しの経路: `CollectFrom` は `GetAllTypes` の全型について `CollectTypeIfAppropriated` を呼ぶ。`ScriptUpdater.dll` の `Program::Main` は `catch (Exception)` で受けて `ldc.i4 130` を返す。v7:102 の記述と一致する。
- `ApiUpdater.MovedFromExtractor.dll` の `ShouldUpdate` は第 1 引数を返す。`CollectMovedFromTypeNamesFromAssembly` が書き出すのは、`true` の型の今の名前と、`sourceNamespace` を `.` で分けた各部分だけ。
  - そのため、`Ns5.cs` の CS0246（`AndroidClipboardManager` が見つからない）で Updater を起動する候補になる。
  - 37 型（`false`）は載らない。V-S4-4 の (b) の見込み「変わらず、エラーが残る」は IL と合う。
- `APIUpdater.ConfigurationValidator.dll`、`AssemblyUpdater.dll`、`CSharpUpdater.dll`、`UnityEditor.CoreModule.dll` に、`false` と `sourceClassName` の組を拒む処理は見当たらない（`MovedFrom` の文字列と呼び出しを走査した）。

**第 1 引数が `false` で `[SerializeReference]` のデータ移行が効くか（一次ソースで分かること）**

- `UnityEngine.CoreModule.dll` の `UnityEngine._Scripting.APIUpdating.APIUpdaterRuntimeHelpers::GetMovedFromAttributeDataForType(Type, out assembly, out nsp, out klass)` は `[RequiredByNativeCode]` で、ネイティブから呼ばれる。
  - 処理は、型の `MovedFromAttribute` が 1 つなら、`data.className`、`data.nameSpace`、`data.assembly` を返して `true` を返すだけ。
  - **`autoUdpateAPI` は読まない。**
- `MovedFromAttributeData.Set` は、第 1 引数を別のフィールド `autoUdpateAPI` に入れるだけ。`className`、`nameSpace`、`assembly` と `*HasChanged` は第 1 引数に関係なく決まる。
- Editor の本体 `MacOS/Unity` の文字列には `APIUpdaterRuntimeHelpers`、`GetMovedFromAttributeDataForType`、`MovedFromAttribute` があり、`autoUdpateAPI` は無い。ネイティブがこのフィールドを名前で読む経路は見当たらない。
- **見込み: 第 1 引数が `false` でも、シリアライズの移行は効く。**
  - 確かめられていない点: ネイティブが候補の型をどう集めるか（TypeCache など）は機械語の中なので見ていない。最終の確かめは V-S4-4 の (d)。
  - (d) で使う 3 つの Payload（`CopyPlainTextPayload`、`ChannelPayload`、`ShareTextPayload`）はどれも 37 型（`false`）なので、(d) はちょうど `false` の経路を試す。この組み方は適切。
  - 旧い完全名（`Runtime.<Feature>.<旧名>`）の型が新版に残っていると、移行が効かずにその型に解決されてしまう。37 の旧名はどれも `Runtime/Android/` の中にしか無く、改名で消えるので、この心配は無い（今の Runtime の public 型を走査した）。

#### 2.2 そのほかの差分

- **`-buildTarget Android`、(a0)(a)(b)(d)**: 欠陥は無い。(a0) の文言は、IL の 2 つの例外の文言（IL_01c9、IL_021d）のどちらにも当たる。
- **`--pull-apk` の 3 つの実行**: 欠陥は無い（1 章の S5-3）。
- **7 章のステップ 5 とコミットの決まり**: S6-2 を除いて欠陥は無い。

---

### 3. 新しい指摘

#### S6-1 ｜ C ｜ 2.3（v7:102）「37 型の名前は、Updater では直せない（Unity の文言のとおり、型の改名は Updater の役目ではない）」
- **問題**: 事実と違います。Unity の文言は「[MovedFrom] では改名できない。[Obsolete()] を使え」です。Updater 自体は `[Obsolete("… (UnityUpgradable) -> 新名")]` を使った型の改名に対応しています。
- **根拠**:
  - `APIUpdater.Framework.dll` の `StringExtensions..cctor` に正規表現 `.*\(UnityUpgradable\)\s*->\s*(?<target>.*)` がある。
  - `CollectTypeIfAppropriated` の最初の分岐（IL_0001 `UpdateTargetAsStringFromObsoleteAttribute`→IL_0359）がその経路。
- **提案**: 「37 型の名前は `[MovedFrom]` では直せない。Updater で直すには旧名の `[Obsolete]` 型を残す必要があり、この段では採らない（利用者の決定）」と書き直す。移行の節に「Unity は改名を自動で直せない」と書かないように注意する。決定そのものは変えない。

#### S6-2 ｜ B ｜ 低 ｜ 7 章のステップ 5（v7:274）と V-S4-4 の準備（:235）
- **問題**: ステップ 5 は「S4-3 に及んだら V-S4-4 もやり直す」としています。しかし V-S4-4 の準備は `git archive <S4-3 のコミット>` と書かれたままです。
  - 文字どおりに控えたハッシュを使うと、直す前のコードで V-S4-4 が通ってしまいます。
  - V-S4-4 はコミットを `git archive` するので、直しのコミットの後でないと走らせられません。この順もステップ 5 に書かれていません。
- **根拠**: v7:235（`<S4-3 のコミット>`）、v7:274（やり直しの条件と「1 コミットにする」だけ）。
- **A1 にしない理由**: 「やり直す」の意図ははっきりしています。直すのは、プレースホルダーの読み方と順だけです。
- **提案**: ステップ 5 に、次の順を 1 文で書く。「直す → V-S4-2・V-S4-5 → 直しを 1 コミット → V-S4-4（`git archive` には直しのコミットを使う）」。または 8 章の残件に足す。

#### S6-3 ｜ B ｜ 低 ｜ 8 章の S5-8 の理由（v7:294）
- **問題**: 「`verify-manual` の言語差分の検査…で拾う」は成り立ちません。この検査は、3 言語の画像と見出しの数しか比べません。4.2（:186）が例に挙げるのは `dialog.md:835` だけです。`clipboard`、`notification` の同じ文を 3 言語とも直し忘れると、V-S4-6 でも 3.4 でも捕まりません。
- **根拠**:
  - `agent-rules/workflows/verify-manual/workflow.md:26`（「3 言語で画像・見出し数が揃っているか」）
  - `grep -rn "keep their namespace" manual/1.12.0` の結果: `clipboard.md:2212`、`dialog.md:835`、`notification.md:912`
- **提案**: 理由を「S4-4 で、`keep their namespace` と、日本語版・韓国語版で同じ意味の文を grep し、9 か所を直したことを確かめる」に替える。直す場所の情報は 8 章の行にすでにあるので、本文は直さなくてよい。

#### S6-4 ｜ C ｜ 低 ｜ 8 章の記録漏れ
- S5-2 の後半（`$W/proj` の git に `Library/`、`Temp/`、`Logs/`、`UserSettings/` の ignore を置く）と、S5-5 の後半（S4-0 の `--pull-apk` のスクリプトと、S4-4 の実装のレビューの置き場所）が、本文にも 8 章にもありません。
- 止める基準の 3 つ目（直さない残件を、理由つきで文書に書く）に当たります。
- **提案**: 8 章に 1 行ずつ足す。
  - S5-2: 「(e) で `Assets/` だけを見るので不要」
  - S5-5: 「`--pull-apk` は陽性の対照の結果で確かめる。S4-4 は `verify-manual` と 3.4 で見る」など

---

**A1: 0 件**（B 2 件: S6-2、S6-3。C 2 件: S6-1、S6-4）。差分の外で、A1 の 3 条件を満たす新しい指摘はありませんでした。

関連するファイル:
- /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v7.md
- /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/Resources/BuildPipeline/Compilation/ApiUpdater/APIUpdater.Framework.dll（`ConfigurationCollector::CollectTypeIfAppropriated`、`StringExtensions..cctor`）
- /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/Resources/BuildPipeline/Compilation/ApiUpdater/ApiUpdater.MovedFromExtractor.dll（`ShouldUpdate`、`CollectMovedFromTypeNamesFromAssembly`）
- /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine/UnityEngine.CoreModule.dll（`APIUpdaterRuntimeHelpers::GetMovedFromAttributeDataForType`、`MovedFromAttributeData::Set`）
- /Users/jonghyunkim/Desktop/unity-native-plugin/agent-rules/workflows/verify-manual/workflow.md（:26）
- /private/tmp/claude-501/-Users-jonghyunkim-Desktop-unity-native-plugin/d6aa18a8-7e96-4283-b9ed-67fa4e87eb02/scratchpad/ildump/（IL を読む道具。`dotnet out/ildump.dll dump <dll> <型> <メソッド>`）

---

## 2. Codex

### 結論

A1 は **1 件（X6-1）**です。停止基準にはまだ達していません。

機械照合は成功しました。ファイルおよび Git 状態への書き込みは行っていません。

### 前回指摘の判定

| ID | v7 の判定 | 8章の理由 |
|---|---|---|
| X5-1 | 8章の残件として理由付きで記録した | 成立。検査実装時に顕在化する |
| X5-2 | 8章の残件として理由付きで記録した | 成立。ファイル・ディレクトリ衝突で実行時に必ず失敗する |
| X5-3 | 直した | 3実行すべてを pull 対象にした |
| X5-4 | 8章の残件として理由付きで記録した | 成立。旧型不在による Player コンパイルエラーは決定的 |
| X5-5 | 8章の残件として理由付きで記録した | 成立。7章の順を正とした |
| X5-6 | 8章の残件として理由付きで記録した | 成立。過剰検出は安全側 |
| X5-7 | 直した | S4-3 修正後の V-S4-5、V-S4-4 再実行を追加 |
| X4-4 の残り | 8章の残件として理由付きで記録した | 成立。`tar -C` が失敗して顕在化する |
| S5-1 | 直した | 37型を `false`、5型を `true` に分離し、照合と V-S4-4 も更新 |
| S5-2 | 直した | 3段とも `-buildTarget Android` |
| S5-3 | 直した | `Android-package-user` を含む3実行を対象化 |
| S5-4 | 直した | パス指定の `git add`、push 禁止、停止位置を明記 |
| S5-5 | **未対応（一部のみ修正）** | 再検証と修正コミットは直ったが、S4-0・S4-4 のレビュー工程は未対応（X6-3） |
| S5-6 | 8章の残件として理由付きで記録した | 成立 |
| S5-7 | 8章の残件として理由付きで記録した | 成立。Breaking Changes は見出し条件でも生成される |
| S5-8 | 8章の残件として理由付きで記録した | **一部不成立**。`verify-manual` が本文を照合するという部分は誤り（X6-4） |
| S5-9 | 8章の残件として理由付きで記録した | 成立 |
| S5-10 | 8章の残件として理由付きで記録した | 成立。(a)〜(d) とも処理時点が指定され、残件記録として成立 |

### 指摘

#### X6-1｜A1｜レビュー後に S4-1 固有の検証を再実行しない

- 場所: /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v7.md の 270 行、274 行
- 問題: 実装レビュー後、S4-1 の AAR、ピン、`PreBuildProcessor` を修正しても、再実行するのは V-S4-2 だけです。V-S4-1、V-S4-1b、V-S4-3 は修正前の結果のまま残ります。
- 具体的な壊れ方: レビュー修正によって旧 1.x AAR が再びコピー・同梱されても、2.0.0 の C ABI は別パッケージなので、コンパイルと機能テストは通り得ます。そのまま旧 `android.unity.*` / `android.library.*` クラスを含む APK を出荷します。
- 証拠:
  - V-S4-2 の通常実行はテスト結果とコンパイルエラーを見るだけで、APK の DEX・manifest を検査しません（/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_android.sh の 145 行、208 行）。
  - 現行ピンには旧 AAR 2本があり（Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/VERSION.txt の 26 行）、`PreBuildProcessor` はそのピンをコピー対象にします（Packages/com.jonghyunkim.nativetoolkit/Editor/Build/PreBuildProcessor.cs の 413 行）。
  - 旧クラス混入を検出するのは `--pull-apk` を使う V-S4-1 です。
- 修正: 7章ステップ5に、S4-1 の AAR・ピン・`PreBuildProcessor`・`Plugins/Android` を直した場合は、修正コミット前に **V-S4-1、V-S4-1b、V-S4-3** を再実行する、と追加する。

#### X6-2｜B（要確認）｜`Renamed37.cs` が「変わらない」という期待が強すぎる

- 場所: design-v7.md の 242 行
- 問題: `Ns5.cs` と `Renamed37.cs` は同じ旧名前空間の `using` を持ちます。5型の `MovedFrom(true, …)` から名前空間移動設定が作られるなら、`Renamed37.cs` も `using` だけ更新され、旧型名のコンパイルエラーだけが残る可能性があります。「ファイル全体が変わらない」は適切な期待値ではありません。
- 証拠: Unity 6000.4.2f1 の `APIUpdater.Framework.dll` と `CSharpUpdater.dll` に `MoveToNamespaceConfig`、後者に `movedFromNamespaces` が存在します。実際の書換え単位は V-S4-4 で観測する必要があります。
- A1 でない理由: V-S4-4 自身が差分とコンパイルエラーを観測するため、期待との違いはそこで捕捉されます。
- 修正: (b) を「`Renamed37.cs` の `using` の差分を記録し、旧型名によるコンパイルエラーが残ることを確認する」にする。

#### X6-3｜C｜S5-5 の S4-0・S4-4 レビュー工程が残っている

- 場所: design-v7.md の 248 行、265 行
- 問題: 5章は「各段の実装後にレビュー」としますが、7章のレビューは S4-1〜S4-3 だけです。S4-0 の `--pull-apk` と S4-4 の文書レビューを置くか、置かない理由を書く、という S5-5 の一部が未対応です。
- 証拠: 7章ステップ5の対象が明示的に S4-1〜S4-3 に限定されています。
- 修正: S4-0・S4-4 のレビュー工程を追加するか、5章を「S4-1〜S4-3 の実装後」に限定する。

#### X6-4｜B｜S5-8 の `verify-manual` による検出理由が成立しない

- 場所: design-v7.md の 294 行
- 問題: 8章は9か所の修正を「`verify-manual` の言語差分の検査と、ページごとの直しで拾う」としますが、言語差分検査は本文を比較しません。
- 証拠: /Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_manual.sh の 374 行以降が比較するのは画像集合と見出し数だけで、本文の当該文は検出対象外です。しかも warning 扱いです。
- 修正: 理由を「既知の9ファイルをページ単位のチェックリストで直す」に限定するか、V-S4-6 に旧文言の件数検査を追加する。

### 確認できた強み

- `MovedFrom(false, …, sourceClassName)` は Unity 6000.4.2f1 同梱コードにも同形の実例があります。
- `-buildTarget Android` と `-accept-apiupdate` は Unity 実行ファイルに存在します。
- 3回目の `Android-package-user` は、最終アンインストール前に取得できる制御フローです。
- パス指定 `git add` と push 禁止は利用者の決定に一致しています。
- `check_design_consistency.py` は v7 に対して成功しました。ただし V-*、件数、擬似コード関連は SKIP です。

**A1 件数: 1件（X6-1）**
