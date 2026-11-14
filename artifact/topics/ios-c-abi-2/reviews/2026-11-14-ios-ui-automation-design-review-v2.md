# iOS の Player のテストと UI の自動化の設計書 v2 のレビュー v2（差分のレビュー）

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-ui-automation-design-v2.md](../designs/2026-11-14-ios-ui-automation-design-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。2 回目なので、前の指摘の判定と、v1 から変えた部分だけを見る差分のレビュー
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

### 1. 直すと決めた指摘の判定

| 指摘 | 判定 | 理由 |
|---|---|---|
| Codex #1 [A1]（R-1） | 直った | 引数を Editor の起動に移し（54・64 行）、`ConnectToHost` を外し、V-4 から消した。PlayerLauncher.cs:105-109 と合う。ただし V-5 の確かめ方が観測できない（R2-3） |
| R-5 | 直った | アセンブリ単位の属性は、`UnityEditor.TestRunner` を参照するすべてのアセンブリから集められる（modifier は PlayerLauncher.cs:347-349、cleanup は AttributeFinderBase.cs:41-43）。iOS だけで働かせる点と戻す範囲も入った。働かせる条件と置き場所に別の欠陥がある（R2-2）。パスの誤りは R2-6 |
| Codex #4 | 一部 | 照らす範囲と照らし直しは入った。しかし Unity が書き換える木（`ProjectSettings`・Build Profiles）を「未コミットなら止まる」範囲に入れたため、実機の回が必ず途中で止まる（R2-1） |
| R-10 | 一部 | (A) にも承認を掛けた。端末に入るアプリの一覧も足した。ただし 161 行は「どれも終わりに消す」と約束し、後片付け（164 行）はハーネスしか消さない（R2-5）。Team ID は「記録やログに」と書き分けた |
| R-7 | 直った（仕組みに未確認が残る） | `.mm` を `NativeSource~` に移し、定義の制約を足した。定義の入れ方は Profile と衝突しうる（R2-4。V-8 で表に出る） |
| Codex #5 / R-4 前半 | 直った | iOS Profile.asset:225 は 1、Test iOS Profile.asset:221 は 0、両方とも 988。記述と一致する |

### 2. 差分の新しい欠陥

A1 はありません。どれも段 2 のスパイクか手順 4 で表に出ます。

#### R2-1 [B 中心契約] 承認の照らし直しが、Unity が自分で書き換えたファイルで必ず止まる
- 根拠: 設計 162 行は `ProjectSettings` と `Assets/Settings/Build Profiles` をハッシュの範囲に入れ、「未コミットなら止まる」「端末に触れる直前ごとに照らし直す」とする。3.4（97 行）の戻しは、Android と同じく実行の終わりにだけ走る（verify_unity_android.sh:160-180、restore は一度だけ）。写し元の native-toolkit の device_guard（test_ios.sh:215 付近）と tree_problems（ios_ui_runner.py:2233 付近）は、毎回 dirty を見る。Android の `SIDE_EFFECT_PATHS` がある時点で、Unity がこれらを書き換えることは分かっている。v2 では modifier・cleanup と `NTK_UI_HARNESS` の出し入れも、ここに書く。
- 壊れ方: (A)・(B) とも、Unity での書き出しの後、最初の `devicectl install` の前の照らし直しで止まる。止まる向き（fail-closed）なので安全側だが、実機の回は完走できない。逆に、照らしから外す形で「直す」と、手で入れた変更がすり抜ける。
- 直し案: Unity の段の直後に 3.4 の戻しを走らせ、そのあとで照らし直す、と順番を書く。ハッシュは作業木ではなく、`git rev-parse HEAD:<path>` の木と「clean」で取る。

#### R2-2 [B] iOS 向けの modifier が、対象の種類を見分けずに Simulator SDK へ替える。置き場所もパッケージの中
- 根拠: 79 行は「対象が iOS のときだけ働く」とだけ書く。163 行・6 章の 4 では、(A) を実機でも流す。置き場所の `Tests/Editor/...asmdef` はパッケージの `Tests/` の下（既存の Tests/Runtime・PlayMode と並ぶ場所）。
- 壊れ方: 実機向けの (A) も Simulator SDK で書き出され、入れられない。Test Runner の窓から人が iOS の Player を流しても、黙って SDK が替わる。testables に入れた利用者にも同じことが起きる。
- 直し案: 働かせる条件を、スクリプトが渡す明示の引数（例: `-ntkIosPlayerSdk simulator|device`）にする。asmdef は `Assets/` の下（このリポジトリだけ）に置く。

#### R2-3 [B] V-5 の「Player のログに `No RemoteTestResultSender` が出る」は観測できない
- 根拠: この文字列は TestResultRenderer.cs:103 の `GUILayout.Label` で、画面に描くだけでログには出ない。
- 壊れ方: A1 の直しが効いていても V-5 が「通らない」になる。その結果、誤った代わりの案（`PlaymodeTestsController` に足す）へ進む。
- 直し案: 書き出しの直後（3.4 の戻しの前）に、`Assets/InitTestScene*.unity` に `RemoteTestResultSender` が無いことを確かめる。シミュレーターでは `simctl io screenshot` も使える。

#### R2-4 [B] `NTK_UI_HARNESS` の入れ方が、Profile の定義の上書きとぶつかりうる
- 根拠: iOS Profile.asset:863-864 は、Profile の中で `scriptingDefineSymbols: iPhone: NATIVETOOLKIT_ENABLE_BUILD_STEPS` を持つ。109 行は、V-2 と同じく Player Settings を替えて戻す形をとる。V-2 が「効くか確かめていない」と書く問題が、ここにもそのまま掛かる。さらに、シーンが Editor で組まれないアセンブリの MonoBehaviour を参照する形になる（裏は取れていない）。
- 壊れ方: ハーネスが Player に入らず、V-8 が全部落ちる。原因を取り違えやすい。
- 直し案: `BuildPlayerOptions.extraScriptingDefines` で渡す（ファイルに書かないので R2-1 も軽くなる）。コントローラーは `[RuntimeInitializeOnLoadMethod]` で作り、シーンから参照しない。

#### R2-5 [B] 承認の一覧と後片付けが食い違う（実機の安全）
- 根拠: 161 行は「(A) の Player、ハーネス、`NtkUiTestHost`、`NtkUiTests-Runner`。どれも終わりに消す」とする。164 行が消すのはハーネスだけ。
- 壊れ方: 承認した内容と違い、私物の iPhone にアプリが 3 つ残る。
- 直し案: 164 行に 4 つの bundle id を列挙する。Runner の id は `<テストの bundle id>.xctrunner` の形。

#### R2-6 [C] 細部
- 3.4 は Android と「同じ範囲」と言いながら、`Assets/PerformanceTestRun*` と書いている。Android は `Assets/Resources/PerformanceTestRun*`（verify_unity_android.sh:145）。
- 「ConnectToHost なし」に加えて、`WaitForPlayerConnection` も外すべき。Editor の設定によっては付く（PlayerLauncher.cs:248-249）。付くと Player が起動時に止まる。

### 3. 11 章（止める基準）

直さなかった R-2・Codex 3、R-3・Codex 2、R-6、R-8、R-4 後半、R-9、R-11、R-12 は、すべて理由と扱う場所つきで書かれている。基準は満たしている。ただし次の 2 点は直したい。
- R-11 の後半「(B) も入れる前に必ず消す」が、11 章にも本文（4.6）にも無い。
- R-2・R-3 は「読み替える」としているが、本文の 31・59・60・63・66 行は古いまま残っている。実装する人が誤読しないよう、各行に「11 章参照」の注を 1 つ付けるだけでよい。

### 件数

| 区分 | 件数 |
|---|---|
| A1 | 0 |
| A2 | 0 |
| B | 5（R2-1 中心契約、R2-2、R2-3、R2-4、R2-5） |
| C | 1（R2-6） |

## 2. Codex

静的照合の結果、変更行に A1 が1件、B が1件ありました。

前回指摘の修正確認（レビュー第2節の番号）：

- **1：修正済み。** v2:64 で引数を Editor 側へ移動。一次ソースも Editor の引数で送信部品の追加を分岐しています（PlayerLauncher.cs:107）。検証方法には下の B が残ります。
- **4：修正済み。** v2:162 にハーネス・パッケージ・設定・ランナーの照合と、端末操作直前の再検査を追加。参照元の契約（ios_ui_runner.py:2225、test_ios.sh:215）を反映しています。
- **5：修正済み。** v2:74 の値は実ファイルの 1／0 と一致します（iOS Profile.asset:225、Test iOS Profile.asset:221）。

変更行の指摘：

1. **[A1] 中断時の復元 glob が生成先を拾わない。**
   - 設計：v2:97。`Assets/PerformanceTestRun*` としていますが、参照元は `Assets/Resources/PerformanceTestRun*` です（scripts/verify_unity_android.sh:146）。実際の生成先も Resources 配下です（Library/PackageCache/com.unity.test-framework.performance@aa81a99c4a75/Runtime/Utils.cs:12）。
   - 壊れ方：生成後、正常 cleanup の前に Unity が中断すると、誤った glob では生成物を回収できません。v2:79 の途中停止時の復元契約を満たせなくなります。正常時は Framework 自身が削除するため、通常実行は通ります（TestRunBuilder.cs:29, 84）。V-1〜V-8 と第8章（v2:189–202）に中断テストはありません。
   - 修正：glob を `Assets/Resources/PerformanceTestRun*` に訂正する。

2. **[B] V-5 が要求する文字列はログに出力されない。**
   - 設計：v2:64、v2:193。一次ソースの `No RemoteTestResultSender...` は `GUILayout.Label` による画面表示です（com.unity.test-framework@76560ee600cb/UnityEngine.TestRunner/TestRunner/Callbacks/TestResultRenderer.cs:103）。
   - 壊れ方：Player ログを検索しても、送信部品を正しく除外できたかを確認できません。V-5 を実行すると不備が表面化するため B です。
   - 修正：書き出すシーン内に送信部品がないことを直接 assert し、その結果を明示的にログへ出す。

（注）Codex が実際に走らせたのは静的照合までで、Unity の実行は行っていません。（中継したサブエージェントの注）

## 3. 判断

- **A1 は Codex の 1 の 1 件**（サブエージェントの R2-6 の前半と同じ件で、サブエージェントは C とした）。v3 で glob を直す
- **中心契約の B として v3 で直すもの**:
  - R2-1（照らし直しの順番。Unity の段の直後に戻しを走らせてから照らす。ハッシュは HEAD の木と clean で取る）
  - R2-2（modifier を明示の引数で働かせる。置き場所を `Assets/` の下にする。パッケージの利用者に効かせない）
  - R2-5（承認の一覧と後片付けを合わせる。実機の安全）
  - R2-3・Codex の 2（V-5 の確かめ方。誤った代わりの案へ進まないように）
  - 11 章の 2 点（R-11 の後半を足す、古い行に 11 章を指す注）
- **直さないもの**（v3 の 11 章に足す）: R2-4（V-8 で表に出る。代わりの案として `extraScriptingDefines` と `[RuntimeInitializeOnLoadMethod]` を書く）、R2-6 の後半（`WaitForPlayerConnection`。V-1 で確かめる項目に足すだけ）
- ラウンド 2 で A1 が 1 件出たので、ラウンド 3（差分）を回す。ラウンド 3 で止まらなければ利用者に確かめる
