# iOS の Player のテストと UI の自動化の設計書 v3 のレビュー v3（差分のレビュー）

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-ui-automation-design-v3.md](../designs/2026-11-14-ios-ui-automation-design-v3.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。3 回目の差分のレビュー
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

v3 から A1 は出なかった。差分から B が 2 件、C が 1 件見つかった。直すと決めた項目のうち、R2-3 と「古い行の注」は直りきっていない。

### 1. 判定

| 指摘 | 判定 | 一行 |
|---|---|---|
| Codex #1 A1（glob） | 直った | v3:106 は `Assets/Resources/PerformanceTestRun*`。verify_unity_android.sh:145 と一致する |
| R2-1 | 直った | v3:171 で、Unity の段の後に戻しを走らせてから照らし直し、HEAD の木と clean で比べる。ただし照らす範囲に穴がある（R3-2） |
| R2-2 | 直った | v3:88 で `-ntkIosPlayerSdk` の引数があるときだけ働く。置き場所も `Assets/` の下。PlayerLauncher.cs:347-349 は、UnityEditor.TestRunner を参照するアセンブリならどれからでも属性を集めるので、ここに置いて効く |
| R2-5 | 直った | v3:173 に 4 つのアプリを並べ、`.xctrunner` の形と、消えたことの確認も書いた。v3:170 とも合う |
| R2-3 / Codex #2 | 一部 | 「ログに出る」という誤りは消えた。しかし新しい確かめ方も、実際には観測できない（R3-1） |
| 11 章の点 1（R-11 の後半） | 直った | v3:173 に「(B) も入れる前に必ず消す」が入った |
| 11 章の点 2（古い行の注） | 一部 | 注が付いたのは v3:72 と v3:75 だけ。v3:40「終わると自分で終わる」、v3:68「自分で終わる」、v3:69「アプリが終わるのを待つ」、v3:64「書き出すだけ」には付いていない。それなのに v3:19 は「古い記述の行に注を付けた」と書いている |

### 2. 差分の新しい欠陥

#### R3-1 [B] V-5 の新しい確かめ方は、読む時点でシーンのファイルが消えており、名前の文字列でも見つからない
- 根拠:
  - TaskList.cs:37 で、Player の流れの最後に `DeleteBootstrapSceneTask` が走る。
  - DeleteBootstrapSceneTask.cs:10-21 は `RunOnError = RunAlways`、`RunOnCancel = true` で、`AssetDatabase.DeleteAsset(InitTestScenePath)` を呼ぶ。つまり `Execute` が終わる前に、Test Framework 自身が `Assets/InitTestScene*.unity` を消す。
  - 部品は `AddComponent<RemoteTestResultSender>()` で付く（RuntimeTestLauncherBase.cs:50、PlayerLauncher.cs:110）。シーンの YAML には型の名前ではなく `m_Script` の guid（RemoteTestResultSender.cs.meta の `20793418366caf14293b29c55df5e9ec`）で書かれる。
- 壊れ方: v3:73・v3:202 のとおり「Unity の段の後、3.4 の前にファイルを読む」と、ファイルが無いか、名前で grep して 0 件になる。どちらも「部品が無い」と読めてしまい、V-5 は何も確かめないまま通る。前回の A1 の直しが効いているかを、ここで確かめられない。
- 直し案: 書き出しの途中で確かめる。v3:88 の Editor アセンブリに `IProcessSceneWithReport` を置くか、modifier・cleanup の中で書き出し中のシーンを読み、`RemoteTestResultSender` の型の数を明示の行としてログに出す。スクリプトはその行が出たことと、数が 0 であることを要件にする。

#### R3-2 [B 中心契約] 承認で照らす範囲に、新しい置き場所 `Assets/NativeToolkitIosPlayerTests` が入っていない
- 根拠: v3:88 で、書き出しを変える処理（SDK を替える、`ConnectToHost` を外す）を `Assets/NativeToolkitIosPlayerTests/Editor/` に移した。ところが v3:171 の照らす範囲は、パッケージ、ハーネス、`Tools~`、Build Profiles、`ProjectSettings`、ランナーだけで、この道が無い。「未コミットなら止まる」もこの範囲に限っている。
- 壊れ方: 承認の後に、(A) の実機ビルドを変えるコードを未コミットで変えても、照らし直しをすり抜ける。端末に入るものが、承認した中身と違ってしまう。
- 直し案: v3:171 の一覧に `Assets/NativeToolkitIosPlayerTests` を足す。あるいは、clean の確認をリポジトリ全体に広げ、Unity が書く既知のファイルだけを除く。

#### R3-3 [C] 細部
- 11 章の見出し（v3:223）と導入の文（v3:225）が「レビュー v1」のままで、R2 の行と合わない。
- 11 章の R-11 の行（v3:235）に、後半を v3 で直したことが書かれていない。
- `--keep-changes` と実機を一緒に渡すと、R2-1 の照らし直しで必ず止まる。実機のときは `--keep-changes` を断る、と一行書くとよい。

### 3. 11 章の網羅

ラウンド 2 で直さないと決めたのは R2-4 と R2-6 の後半。どちらも v3:237・238 に理由と扱う場所つきで載っている。R2-6 の前半は Codex #1 と同じ件で、v3 で直した。直さなかった項目の抜けは無い。残るのは R3-3 の見出しと R-11 の行の更新だけ。

### 件数

| 区分 | 件数 |
|---|---|
| A1 | 0 |
| A2 | 0 |
| B | 2（R3-1、R3-2 中心契約） |
| C | 1（R3-3） |

R3-1 は A1 にしなかった。この確かめ方が空振りしても、Player の動きそのものは壊れず、V-5 の XML の読み取りは別に確かめられるため、条件 (a) を満たさない。主な一次ソースは `/Users/jonghyunkim/Desktop/unity-native-plugin/Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEditor.TestRunner/TestRun/TaskList.cs` と、同じ Tasks フォルダの `DeleteBootstrapSceneTask.cs`。

## 2. Codex

差分の静的照合では、A1 は 0 件、B は 1 件です。ファイルは変更していません。

1. 前回 finding 1（glob）は修正済みです。v3:106 の `Assets/Resources/PerformanceTestRun*` は scripts/verify_unity_android.sh:146 と一致します。
2. 前回 finding 2（V-5）は一部修正です。ログ出力という誤認は解消しました。文字列が画面表示である点は TestResultRenderer.cs:103 と一致します。ただし代替のシーン検査に、下記の B の欠陥が残っています。

**[B] 書き出し完了後には、検査対象のシーンが削除される**
- 設計箇所は v3:73 と、同202行付近の V-5 です。
- 一次ソースは `Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEditor.TestRunner/` 配下です。
  - `AutoRunPlayer` を外すと、WaitForPlayerRunTask.cs:12 は待機せず終了します。
  - その後 TaskList.cs:37 が削除処理を実行します。実際の削除は Tasks/DeleteBootstrapSceneTask.cs:21 です。
- 壊れ方は次のとおりです。Unity の書き出し完了後に、シェル側から `Assets/InitTestScene*.unity` を読む手順では、対象が残っていません。「3.4 の復元前」という指定だけでは、検査時点として足りません。
- 修正案は、シーンがまだ存在する `IPostBuildCleanup` 内で検査し、結果を保存することです。この呼び出しは TestLaunchers/PlayerLauncher.cs:80 にあり、シーン削除より前です。
- 分類は B です。V-5 を実施すれば表面化する観測手順の欠陥であり、A1 の条件（V-1..V-8 や第8章のテストで拾われない）を満たしません。

## 3. 判断

- **ラウンド 3 で、2 人とも A1 が 0 になった。レビューを止める**（`review-document` の止める基準。差分のレビュー、レビュアーを替えて通した〈Codex〉、直さない残件は設計書の 11 章に理由つきで書いてある）
- 止めた後の v4 で直すもの（新しいレビューには回さない）:
  - R3-2（中心契約。承認で照らす範囲に `Assets/NativeToolkitIosPlayerTests` を足す）
  - R3-1・Codex の B（V-5 の確かめ方。2 人とも同じ件。`IPostBuildCleanup` の中で、書き出し中のテストのシーンの `RemoteTestResultSender` の数をログに 1 行出し、スクリプトは「その行があり、数が 0」を要件にする）
  - R3-3（11 章の見出しと導入の文、R-11 の行、古い行の注、実機のときは `--keep-changes` を断る）
