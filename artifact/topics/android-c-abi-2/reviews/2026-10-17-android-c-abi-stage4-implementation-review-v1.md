# 実装レビュー結果

- 日付: 2026-10-17
- 対象: Android の C ABI 移行の段 4 の S4-1〜S4-3（`df3eb78`、`37f182c`、`97f98c6`）と、未コミットの `scripts/verify_unity_android.sh` の trap の直し
- 設計: `artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v8.md`
- レビュアー: サブエージェント（ラウンド 1）と Codex（ラウンド 1）を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力。文言には手を加えていない（見出しの段を下げ、先頭の題を外した）
- A: サブエージェント 0 件、Codex 0 件。止める基準（`agent-rules/workflows/review-implementation-feature/workflow.md`、A=0）に達した

---

## 1. サブエージェント（Claude）

ファイルと Git の状態は変えていません。一時ファイルは scratchpad/rv/ にだけ置きました。

### 結論
**A は 0 件。** 指摘は B が 4 件、C が 2 件です。

### 照らして問題が無かったこと
- **42 型と [MovedFrom]**: `git show 1.12.0` の型の宣言から独自に照らしました（scratchpad/rv/cmp.py）。
  - 改名した 37 型はすべて `false`。`sourceClassName` は旧名と一致し、`sourceNamespace` もタグの名前空間と一致します。class・struct・enum の種類も 1.12.0 と同じです。
  - 名前を残した 5 型は `true` で、`sourceClassName` はありません。
  - どの型も `sourceAssembly` を渡していません。属性は 1 行で書かれています。
  - `using UnityEngine.Scripting.APIUpdating;` は 18 ファイルのすべてにあります（Manager では namespace の中、`#if UNITY_ANDROID || UNITY_EDITOR` の内側）。
  - 入れ子の public 型はありません。今のパッケージと `Assets/` に、[MovedFrom] の旧い完全名とぶつかる型はありません。
- **git mv と GUID**: 10 ファイルが rename として記録され、`.cs.meta` は 100% 一致で GUID は保たれています。改名した型に MonoBehaviour と ScriptableObject は無く、その GUID をシーン・prefab・asset・uxml から参照しているものもありません。消したビルダー 2 本・テスト 2 本・AAR 2 本の GUID も、どこからも参照されていません。
- **置き換えの中身**: S4-3 の Packages の差分を単語単位で見ました（scratchpad/rv/wd.py）。変わったのは「旧名 → Android+旧名」の置き換え、`[MovedFrom]` の 42 行、`using` の 18 行だけです。
  - 文字列リテラル、ログ、nameof、typeof、フィールド名・プロパティ名は変わっていません。
  - 新しい名前を含む文字列リテラルはありません。サンプルの UI の文言も変わっていません。
  - 結果の型は `ToString` も `GetType().Name` も使っておらず、表示は変わりません。
  - ほかの OS のファイルで変わったのはコメント 3 か所だけです。
- **旧名の残り**: リポジトリ全体（artifact・manual・docs を除く、Editor・Assets・Samples・README・Documentation~ を含む）で、旧名と消したビルダー名は 0 件でした。
- **S4-1**:
  - PreBuildProcessor の追加の組の削除は設計どおりで、XML コメントは Windows と同じ形になりました。`extra` の grep は過去形の注記 2 か所だけに当たります。
  - VERSION.txt と common.md:45 も設計どおりです。`git ls-files Plugins/Android` は 6 つです。
  - 1.x の参照は、アクション ID の定数のほかに残っていません。
- **層 2b のテストの 1.x の分岐**:
  - 消す前の `OnCApi` は 13+24+1+7+10+1 = 56 か所で、設計の数と一致します。
  - C ABI の期待は、三項演算子と if の C ABI 側をそのまま残しています。teardown の PendingCallCount の確認は、条件なしで常に走るようになりました。
  - 前は記録だけだった AC-28 も、今は assert しています。弱くなったテストはありません。
  - SetUp の `Assert.IsTrue(OnCApi, …)` は Clipboard・Notification・Share の 4 つの fixture にあり、購読の前に置かれています。
  - 消した `LogAssert.ignoreFailingMessages = false` は、それを true にするコードがもう無いので問題ありません。
- **S4-2**: 消したテストはビルダーだけを見ていました（Share 21 本、Clipboard 16 本）。コメント 4 か所、testing.md、common.md:518 の直しは設計どおりです。
- **s4-3-check.py**: もう一度流して `OK 0 | public types: 42` でした。

### 指摘

**S1-1（B）trap: シグナルで終わると、副作用の復元（:403-414）が走らない**
- 場所: `scripts/verify_unity_android.sh:206-209`（未コミット）
- 問題: 前の trap は、シグナルで後始末をした後も先に進み、最後に復元していました（それ自体が直した不具合です）。今はシグナルで `exit` するので、`Assets/Settings`、`ProjectSettings`、`.slnx` の再シリアライズが残ります。さらに、次の実行は残ったものを `CHANGED_BEFORE`（実行前からの変更）として数え、「前からある変更」として復元しません。そのため、手で戻すまで残り続け、誤ってコミットされるおそれがあります。
- 根拠: scratchpad/rv/t1.sh で確かめました。TERM で終わると EXIT trap だけが走り、終了コードは 143 で、復元に当たる処理は走りません。
- 提案（どちらか）:
  - 復元を関数にし、EXIT trap から呼ぶ。`CHANGED_BEFORE` を取った後だけ、`KEEP_CHANGES=0` のときだけ、1 回だけ走るようにする。
  - ヘッダの「restores what a run changed」に、割り込んだ実行は戻さないことを書き足す。

**S1-2（B）trap: 後始末の途中に 2 つ目のシグナルが来ると、後始末が切れる**
- 場所: 同じく `:206-209`
- 問題: EXIT trap が `stop_driver` の `wait` にいる間に HUP、TERM、Ctrl-C が来ると、`exit` で即座に終わります。`adb uninstall` が飛び、ドライバは親の無いプロセスとして設定を戻し続けます。
- 根拠: TERM の 0.3 秒後に HUP を送ると、「EXIT trap: uninstall」が出ないまま終了コード 129 で終わりました。
- 提案: EXIT trap の頭で `trap '' INT TERM HUP` とし、後始末を割り込めなくする。

**S1-3（B）3.4 の検査（s4-3-check.py）の穴**
1. 検査 1 の `\b旧名\b` は、`_` が後に続く識別子に当たりません。そのため、テストのメソッド名に旧い型名が 9 つ残っているのを見逃しています。
   - `Tests/Runtime/AndroidShareResultTests.cs:14,24,34,43,52,62,71,79`（`ShareOperationResult_Success_…` など）
   - `Tests/Runtime/AndroidClipboardManagerDispatchTests.cs:103`（`ClipboardOperationResult_Success_…`）
   - 提案: 後ろの境界を `(?![A-Za-z0-9])` にする。メソッド名は直す（C）か、許す理由を記録する。
2. 検査 1 は `.cs` と `.md` しか見ず、パスも Runtime・Tests・agent-rules だけです。`Editor/`、`Assets/`、`.uxml`、`.uss`、`.json` は見ません。今回は全体の grep で 0 件だったので実害はありませんが、範囲の穴として記録してください。
3. 検査 2 は、旧名と名前空間を、設計の表を手で写した `s4-3-names.py` と照らしています。タグ `1.12.0` と直接照らしていないので、表と属性が同じ写し間違いをしても通ります。今回は 1.12.0 との独自の照合で 42 型すべて一致しました。

**S1-4（B、軽微）V-S4-4 は `file:` の参照だけで確かめている**
- 場所: scratchpad/v-s4-4-record.md
- 問題: 「Updater はこのパッケージの [MovedFrom] を候補にしない」は、`file:` で参照したパッケージで見た結果です。利用者が入れる git URL（Library/PackageCache の書き換えられないパッケージ）では確かめていません。
- 根拠: どちらもソースからコンパイルされるので、同じになる見込みは高いです。移行の節が既定の「手で直す」だけを書くなら、誤った方向には倒れません。
- 提案: 実装結果の V-S4-4 の節に、この前提を 1 行書く。

**S1-5（C）`os-prefix-violations/README.md` の「公開 81 型・13%」を数え直していない**
- 場所: 97f98c6
- 問題: 設計 2.1 は「改名の後の数に直す」としていますが、実装は「数え直していない」と注記を足しただけです。
- 提案: 数え直すか、設計からの逸脱として実装結果に残す。

**S1-6（C）S1-3 の 1 で見つかったテストのメソッド名に旧い型名が残っている（9 か所）**
- 振る舞いは変わりません。直すなら `AndroidShareOperationResult_…` などに改名する。

### 参考（範囲の外、前からあるもの）
V-S4-4 の記録にある `Plugins/Windows/VERSION.txt.meta` の欠け（.gitignore:93 で除外、1.12.0 からある）は、書き換えられない UPM パッケージで「meta が無い」の警告になりえます。段 4 とは関係ありません。

**A の件数: 0**

---

## 2. Codex

### レビュー結果

#### X1-1

- class: B
- location: /private/tmp/claude-501/-Users-jonghyunkim-Desktop-unity-native-plugin/d6aa18a8-7e96-4283-b9ed-67fa4e87eb02/scratchpad/v-s4-4-record.md:10
- problem: V-S4-4 の「新版パッケージコピーが変化しない」という条件を満たしていません。
- evidence:
  - 設計は実行前後のファイル一覧と hash の一致を要求しています（design-v8.md:252）。
  - 実際の `diff -u new-hashes-before.txt new-hashes-after.txt` では、`Plugins/Windows/VERSION.txt.meta` が追加されています。
  - 記録自身も「Unity added」と認めながら「every file same」としており、判定が矛盾しています。
- fix: V-S4-4 を、初回 import 後のパッケージを基準に再実行するか、Unity が生成する未追跡 `.meta` を明示的に除外するよう設計を改訂したうえで再測定してください。現状の記録を完全な PASS と扱わないでください。

#### X1-2

- class: B
- location: scratchpad/v-s4-4/stage1.log:5515、v-s4-4-record.md:11
- problem: V-S4-4 段1は「コンパイルが通ること」が前提ですが、実行ログにコンパイルエラーが残っています。
- evidence:
  - 設計は旧版で開いてコンパイル成功後に資産を作るよう要求しています。
  - `stage1.log:5515-5516` に ShaderGraph の `GUID` に対する `CS0246` が2件あります。
  - Unity は `MakeAsset.Run` を実行して終了コード0になっていますが、プロジェクト全体がコンパイル成功したことにはなりません。記録は原因を URP 側と説明するだけで、合格条件を調整していません。
- fix: 不要な URP／ShaderGraph 依存を除いた最小 manifest、または互換性のある依存関係で段1を再実行し、意図した旧版スクリプトを含めて `error CS` が0件であることを記録してください。

### 確認済みで指摘なし

- 42個の `[MovedFrom]` は、タグ `1.12.0` の名前空間・クラス名と一致。
- `sourceAssembly` の省略は、旧版・新版ともアセンブリ名が `NativeToolkit.Runtime` で不変であることと一致。
- 18個の型定義ファイルの `.meta` GUID は 1.12.0 とすべて一致。
- Editor、サンプル、資産、テストに、属性の `sourceClassName` 以外の旧型参照なし。
- 層 2b では C ABI 側の期待が維持され、各 fixture に test seam の存在検査が追加済み。
- `PreBuildProcessor` は 2.0.0 AAR だけを解決・コピーし、旧 AAR は追跡対象から削除済み。
- trap 変更は `bash -n` を通過し、INT/TERM/HUP でそれぞれ 130/143/129 となり、後続処理へ進みません。

総合評価: 要修正（検証手段）。実装の振る舞いを変える欠陥は確認できませんでした。

**A の件数: 0**（B: 2、C: 0）
