# 段 2 (A) iOS の Player のテストの実装のレビュー v1

- 記録日: 2026-11-14
- 対象: `scripts/verify_unity_ios.sh`、`scripts/ios_player_results.py`、`Assets/NativeToolkitIosPlayerTests/**`、`Tests/PlayMode/Ios*PlayerTests.cs` と `IosPlayerTestSupport.cs`、古い 2 つのテストの `#if`（コミット前の作業木）。実装結果は [../results/2026-11-14-ios-stage2a-player-tests-result-v1.md](../results/2026-11-14-ios-stage2a-player-tests-result-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

読むだけで確かめた。ファイルは変えていない。`/bin/bash -n`（3.2.57）は通り、`python3 -I -m doctest` も 7 件通った。前回の実行の記録（scratchpad の `stage2/verify-run`, `verify-run2`）も根拠に使った。

**結論:** 重大は無い。失敗した実行が PASSED と判定される経路は見つからなかった。ただし「時間の上限と止まった判定」と「シグナルで止めたとき」の 2 点は、スクリプトが説明どおりに動いていない。

### R-1 [中] Unity の手順では「止まった判定」が空のファイルを見ている
- **根拠:** `verify_unity_ios.sh:203` と `:240` で `run_limited` に渡すログは `*.console.log` だけ。Unity は `-logFile "$log"`（`:205` と `:244`）にしか書かない。前回の実行では、`verify-run/editmode.console.log`、`playmode-editor.console.log`、`ios-player-export.console.log`、`verify-run2/ios-player-export.console.log` がすべて 0 バイトだった。
- **壊れ方:** `last_change` が最初の値のまま動かない。そのため、ログが伸びない時間の上限が、実際には実行全体の上限になる（Editor の 2 回は 3600 秒ではなく 900 秒、書き出しは 1800 秒ではなく 600 秒）。Android から iOS へ切り替えた直後の読み込み直しや、Library が無い状態からの実行では、正常でも 15 分を超えて STOPPED になりうる。逆に、本当に止まった実行を 3 分で見つける（設計 3.1）こともできない。
- **直し案:** `run_limited` に「見張るファイル」の引数を足し、Unity では `-logFile` のファイルを渡す。

### R-2 [中] シグナルで止めても、裏で動いている Unity と xcodebuild が止まらない
- **根拠:** `:161` で子をバックグラウンドで起動している。job control の無い bash では、バックグラウンドの子は SIGINT を無視する（`signal.getsignal(SIGINT)==SIG_IGN` を実際に確かめた）。`on_exit`（`:212-220`）は子を止めない。Android 版は Unity を前面で動かしている（`verify_unity_android.sh:203`）ので、Ctrl-C が Unity に届く。
- **壊れ方:** Ctrl-C や TERM の後も Unity が書き出しを続ける。その横で `restore_side_effects` が動くので、戻したファイルを Unity がまた書き換える。Unity がプロジェクトのロックを持ったまま残り、次の実行が失敗する。
- **直し案:** `run_limited` が子の pid をグローバル変数に入れる。`on_exit` の最初で `pkill -P` と `kill` を送り、待ってから戻す。

### R-3 [軽微] 時間切れのとき、止め方が弱い
- **根拠:** `:169` は TERM を 1 回送って `wait` するだけ。
- **壊れ方:** Unity が TERM で終わらないと `wait` が戻らず、上限を設けた意味がなくなる。xcodebuild の孫（clang など）も残る。
- **直し案:** 数秒待っても終わらなければ KILL を送る。

### R-4 [軽微] 書き出しの成否と、アプリが落ちたことを見ていない
- **根拠:** `:240` の戻り値を使っていない。cleanup はビルドが失敗しても呼ばれる（`PlayerLauncher.cs:79-80`。失敗した実行のログにも `remote-sender-count` の行が出ていた）。成否は、ログの行と `Unity-iPhone.xcodeproj` があるかだけで決めている。
- **壊れ方:** Xcode のプロジェクトを書いた後の処理（Embed など）で失敗しても先へ進む。アプリは起動で落ち、`:288` が 600 秒待ってから「結果が無い」と報告する。PASSED にはならないが、原因を取り違えるし時間もかかる。
- **直し案:** 書き出しのログで `Build Finished, Result: Success` を確かめる。待つループに `kill -0 "$launcher"` を足し、起動したプロセスが終わったら待つのをやめる。

### R-5 [軽微] 判定は「1 件以上流れた」だけなので、一部だけ流れた実行も通る
- **根拠:** `ios_player_results.py:69-74`。
- **壊れ方:** テストの `[Category]` を消し忘れる、`#if` を誤るなどでテストが黙って外れても、残りが成功すれば PASSED になる。カテゴリーの外のテストが失敗しても数えない。カテゴリーの受け継ぎは正しかった（fixture とテストケースの両方を確かめた）。空の実行と XML が壊れた実行は正しく失敗になる。
- **直し案:** 流れた件数が根の `testcasecount`（または `total`）と同じで、根の `failed="0"` であることも要件にする。あるいは期待する件数を引数で渡す。

### R-6 [軽微] Clipboard のテストで、時間切れのときは `finally` の `RemovePasteboard` が呼ばれない
- **根拠:** UTF の `TestEnumerator.cs` の `Execute` は、入れ子の `WaitFor` の中で例外が出ると、外側の enumerator を Dispose せずに `break` する。
- **壊れ方:** 時間切れ（`Assert.Fail`）のときだけ、名前付きの pasteboard が消されずに残る。外側の `Assert` で失敗したときは `finally` が走る。アプリは最後にアンインストールされるので、実際の害は小さい。
- **直し案:** コメントを「時間切れでは呼ばれない」に直す。または `WaitFor` を例外ではなく bool を返す形にして、外側で `Assert` する。

### R-7 [軽微] 戻す処理は状態の文字列しか比べていない
- **根拠:** `:144`。今の時点で `UniversalRenderPipelineGlobalSettings.asset` と `.slnx` が、実行前から ` M` になっている。
- **壊れ方:** 実行前から変わっていたファイルを実行がさらに書き換えても、戻らず、報告もされない（Player Settings の戻しが飛んだときの `ProjectSettings` も同じ）。理屈の上では、`printf | grep -q` と `pipefail` の組み合わせで SIGPIPE が起き、利用者の変更を消しうる（CHANGED_BEFORE が 64KB を超える場合だけ）。
- **直し案:** 実行前に `git hash-object` で中身の値を控え、変わっていれば警告する。比べ方は `grep -qxF ... <<< "$CHANGED_BEFORE"` にする。

### R-8 [軽微] `-derivedDataPath` を消していないので、古い .app を入れうる
- **根拠:** `:238` で消す対象に `$derived` が入っていない。`:273` は `ls ... | head -1`。
- **壊れ方:** productName が変わると、前の回の古い .app が先に選ばれ、古いコードのまま PASSED になりうる。
- **直し案:** `rm -rf "$derived"` するか、.app がちょうど 1 つであることを確かめる。ついでに、`simctl` が xcode-select の Xcode を使う点は `DEVELOPER_DIR="$XCODE_APP/Contents/Developer"` を渡してそろえる。

### R-9 [軽微] 表示名を替えていないのに、設計からの違いの表に無い
- **根拠:** 設計 3.2 と 11 章の R-12 は「表示名 `NTK Player Tests` を `PlistDocument` で」としている。`IosPlayerBuildSetup` は bundle id しか替えていない。実装結果の 2 章にも書かれていない。
- **壊れ方:** (A) の 4 件には害が無い。ただ、サンプルと同じ名前なので、後で共有シートを出すテストを足すと取り違えのもとになる。
- **直し案:** 表示名を替えるか、2 章に違いとして書く。

### R-10 [軽微] `Plugins/iOS` を戻すと、3-S の版上げで新しい XCFramework を消す
- **根拠:** `:124`。PreBuildProcessor は VERSION.txt に無い XCFramework を消し、dist から写す。
- **壊れ方:** VERSION.txt を先に変えてから実行すると、新しい XCFramework（`??`）は消され、古いもの（` D`）は戻る。作業木は、VERSION.txt と中身が食い違った状態になる。今の 1.x では問題にならない。
- **直し案:** 3-S の前に、戻すのは mode の変更だけにする。あるいは PreBuildProcessor が mode を保つようにする。

### 問題が無いと確かめたこと
- **IosPlayerBuildSetup:** `-ntkIosPlayerSdk` があって対象が iOS のときだけ働く。Android・Windows の Player の実行や Test Runner の窓では、`s_Active` が立たず何もしない。ビルドが失敗しても cleanup は呼ばれる（`PlayerLauncher.cs:79-80`。失敗した実行のログの 4132 行と 4152 行）。cleanup が飛ぶのは、ビルド中に例外が外へ出た場合だけで、そのときは R-7 の範囲でスクリプトの戻しが拾う。Test Framework は今回、Profile ではない方の経路（`BuildPlayerOptions`）でビルドしており、修飾が効いていることをログで確かめた。
- **Exit の時機:** ビルドが失敗したときは、Test Framework が先に終了コード 3 で終える。スクリプトは終了コードを見ていない。
- **IosPlayerResultWriter:** `NTK_RUN` の検査（空と 16 進以外を断る）は正しい。書く順は、一時ファイル → 名前変更 → `.done`。スクリプトは `.done` を待ってから `.xml` を確かめる。`RunFinished` は 1 回だけで、アセンブリは iOS 専用なので Editor では組まれない。`[Preserve]` でも消されずに動いた（4 件実行）。
- **4 件のテスト:** Share と Dialog はネイティブを通らない。これは結果ファイルに書かれているとおり。`LogAssert` の文言は `IosDialogManager.cs:388` と一致する。Clipboard の「同時に 1 つ」の制限は操作ごとの鍵なので、`finally` の `RemovePasteboard` とはぶつからない。Notification は、毎回アンインストールしてから入れる前提が守られている。
- **古い 2 ファイルを Editor 専用にしたこと:** 中に別の `#if` は無い。Editor では `UNITY_EDITOR` が常に定義されるので、件数は変わらない（PlayMode は 300 件で同じ）。iOS の Player ではもともとコンパイルが通らなかったので、失った網羅は無い。
- **bash 3.2:** 構文は通る。配列は空でないので、`set -u` でも問題ない。

| 区分 | 件数 |
|---|---|
| 重大 | 0 |
| 中 | 2（R-1, R-2） |
| 軽微 | 8（R-3〜R-10） |

## 2. Codex

- **重大 — 実行前の XCFramework の編集内容が失われる。** 対象: /Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_ios.sh:136。保存するのは変更のステータスとパスだけです。一方、Packages/com.jonghyunkim.nativetoolkit/Editor/Build/PreBuildProcessor.cs:498 は既存 XCFramework を削除し、505 行で dist からコピーします。元から変更済みのファイルは、上書き後も同じステータスなら verify_unity_ios.sh:144 で復元を省略され、元の内容を取り戻せません。**修正:** `Plugins/iOS` が変更済みなら開始前に拒否するか、実行前の内容を退避して復元する。

- **中 — シグナル終了時に実行中の子プロセスが残る。** 対象: verify_unity_ios.sh:212。161 行で起動したコマンドの PID は `run_limited` のローカル変数ですが、終了ハンドラーには停止・回収処理がありません。抽出した実関数で `sleep` を起動し、親だけに TERM を送ると、親の終了コード 143 の後も子が生存しました。Unity／xcodebuild の終了を待たずにファイル復元へ進む構造です。**修正:** 実行中の PID を終了ハンドラーから参照できるようにし、子プロセス群を停止・回収してから復元する。

- **中 — スイートの失敗を Player の成功として扱う。** 対象: /Users/jonghyunkim/Desktop/unity-native-plugin/scripts/ios_player_results.py:61。判定するのは `test-case` だけです。Test Framework は子テスト実行後に OneTimeTearDown を実行し、その例外をスイートの結果へ記録します（Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEngine.TestRunner/NUnitExtensions/Runner/CompositeWorkItem.cs:102、322 行）。`test-suite result="Failed"`、配下のケースは `Passed` という XML を実際の `judge` に渡すと、成功を返しました。**修正:** ケース集計に加えて、ルートとスイートの失敗も拒否する。

- **中 — Editor のタイムアウトを成功扱いできる。** 対象: verify_unity_ios.sh:182。206 行で取得した終了コードは、XML が存在する場合の判定には使われません。実関数に「成功件数を持つ XML の集計値」と終了コード `124` を渡すと、戻り値は `0` でした。結果保存後に Editor が停止して監視側がタイムアウトさせても、最終判定を通過します。**修正:** `run_limited` の非ゼロ終了を必ず失敗に反映する。

- **中 — Editor の全件スキップを成功扱いする。** 対象: verify_unity_ios.sh:190。拒否するのは `total="0"` だけで、その後は `failed="0"` なら成功です。実関数で `total="1" passed="0" failed="0" inconclusive="0" skipped="1"` を判定すると、戻り値は `0` でした。検証を一件も実行していない Editor の回がゲートを通過します。**修正:** 許容するスキップは維持しつつ、少なくとも一件の成功を要求する。

## 3. 判断

利用者の指示（2026-11-14、「判断は推奨案でいい」）に従い、次のとおり決めた。

- **直す**: Codex の 5 件すべて、サブエージェントの R-1〜R-8（R-2 は Codex の 2 つ目と同じ件）
  - Codex の重大: 実行の前に `Plugins/iOS` に変更があれば止まる
  - R-2・Codex: 動いている子を、終わりの trap で止めてから戻す
  - R-1: Unity の手順は `-logFile` のファイルを見張る
  - R-3: 時間切れで TERM の後に KILL
  - R-4: 書き出しのログで `Build Finished, Result: Success` を見る。起動したプロセスが終われば待つのをやめる
  - R-5・Codex: 落ちたスイートを拒み、流れた件数が根の件数と同じで、根に失敗が無いことも要件にする
  - Codex: Editor の回で、Unity の終了コードが 0 でなければ失敗。成功が 1 件も無ければ失敗
  - R-6: `WaitFor` を例外を投げない形にし、外側で確かめる
  - R-7: 比べ方を here-string にする（中身の値の比較は足さない）
  - R-8: `-derivedDataPath` を消してから組み、`.app` がちょうど 1 つであることを確かめる。`DEVELOPER_DIR` をそろえる
- **直さない（実装結果に書く）**: R-9（表示名は替えない。(A) の 4 件は共有のシートを出さない。シートを出すテストは (B) のハーネスで、ハーネスは表示名を替えている）、R-10（3-S の作業で、`Plugins/iOS` の戻し方を見直す）
