# I-17 の確かめのスクリプトと承認の仕組みの実装のレビュー v1

- 記録日: 2026-11-22
- 対象: 未コミットの変更（03:05 の時点）。`scripts/verify_unity_ios_git_url.sh`（新規）、`scripts/ios_ui_runner.py` と `Tools~/IosUiTests/DeviceRequirements.json`（`GitUrlApp/IosLaunch` の承認）、`scripts/verify_unity_ios.sh`（on_exit の 1 行）、`manual/1.14.0/notification*.md`（コメントだけ）、結果の記録
- 設計: [../designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v3.md](../designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v3.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）を並行で
- 1 章と 2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. Codex

- **実機アプリの削除に失敗しても成功終了する。**
  根拠: scripts/verify_unity_ios_git_url.sh:512 で削除前に `PASSED`・`exit 0` を確定し、同:108 の終了トラップでは削除失敗を警告するだけです。アプリが残っても成功扱いになります。
  修正: 成功判定の前に削除と削除確認を実施し、失敗を終了コードへ反映する。

- **インストール前の削除失敗を無視する。**
  根拠: scripts/verify_unity_ios_git_url.sh:472 の uninstall の終了値を確認せず、同:475 でインストールします。削除失敗時にも進み、既存データが残ったまま更新インストールになります。
  修正: 削除後の不在も確認する `remove_from_device "$BUNDLE_ID" || return 1` に置き換える。

- **終了時の実機削除が承認ガードを迂回する。**
  根拠: scripts/verify_unity_ios_git_url.sh:470 で `DEVICE_TOUCHED=1` にした後、次のガードが失敗しても、同:108 は無条件に削除関数を呼びます。その関数は scripts/ios_script_common.sh:215 でガードなしに uninstall を実行します。各実機操作の前に検証する scripts/ios_ui_runner.py:27 の契約と一致しません。
  修正: 復旧時の削除にも、承認済みの端末・bundle・実行マーカーを検証する専用ガードを設ける。

- **コマンド置換で後片付け用の状態が失われる。**
  根拠: scripts/verify_unity_ios_git_url.sh:546 と同:547 は関数を子シェルで実行します。同:394 の `SIM_BOOTED_BY_US=1` と scripts/ios_script_common.sh:122 の `RUNNING_PID` が親シェルに戻りません。そのため同:106 の終了処理は子プロセスを追跡できず、同:118 は自分で起動したシミュレーターを終了しません。
  修正: 両関数を親シェルで直接呼び、パスと結果は出力変数またはファイルで受け取る。

（注: この指摘は Codex の出力です。Codex は変更なしの読み取り専用で実施し、スクリプトは実行していません。指摘のうち、ios_ui_runner.py の check-approval / use-approval / approve との引数の不一致、実機識別子・Team ID・証明書名の出力、tests/mixed_kinds/apps_for の GitUrlApp/IosLaunch の扱いについては、具体的な欠陥の報告がありませんでした。）

## 2. サブエージェント

実行したのは次の 3 つだけ。
- `python3 -I -m doctest scripts/ios_ui_runner.py`: 47 件すべて通った
- `bash -n` を 2 本のスクリプトに: どちらも構文エラーなし
- `tests` / `tests --device` / `tests --keep-installed` の一覧を HEAD 版（runner と JSON をスクラッチに書き出したもの）と比べた: 3 つとも同じ

`approve` は承認の記録をホームの下に書くので、走らせていない。

### 指摘

**I-1**
- 区分: A
- 場所: scripts/verify_unity_ios_git_url.sh:511-518, 107-110
- 指摘: 実機の回で、アプリが消えたことを確かめる前に `PASSED` を出し、`exit 0` している。端末からの削除は EXIT トラップの中だけで行う。削除を確かめられないときも `WARNING` を `PASSED` の後に出すだけで、終了コードは 0 のまま。
- 根拠: verify_unity_ios.sh は、本流の中で `clean_up_player` を呼び、結果を出す前に済ませる。消えたと確かめられなければ `failures` を増やす（「a run that leaves the player behind fails」、:272-276, 677-681）。決まり「終わりに消し、消えたと確かめる」を結果に反映していない。
- 提案: `run_on_device` の後、本流で `remove_from_device "$BUNDLE_ID"` を呼ぶ。失敗なら FAILED にする。成功したら `DEVICE_TOUCHED=0` にして、トラップで二度消さないようにする。PASSED はその後に出す。

**I-2**
- 区分: A
- 場所: scripts/verify_unity_ios_git_url.sh:394, 547, 651, 118
- 指摘: `run_on_simulator` は `result="$(...)"` のサブシェルで動く。中で立てる `SIM_BOOTED_BY_US=1` が親に返らないので、`on_exit` はこの回が起動したシミュレーターを止めない（シミュレーターの回）。シミュレーターでの 2 回の合格の後も、iPhone 16 が起動したまま残っているはず。
- 根拠: bash のコマンド置換はサブシェルで動く。verify_unity_ios.sh は同じ判定を本流で行う（:546-548）。
- 提案: 起動済みかどうかの判定と `bootstatus` を、置換の外の本流に出す。

**I-3**
- 区分: A
- 場所: :472-473, 486-492（実機）、:396-398, 405（シミュレーター）
- 指摘: 入れる前の削除が、本当に消えたかを確かめていない。結果のファイル名も回ごとに変わらない。前の回の後片付けが失敗し、今回の削除も黙って失敗すると、install が上書きになってデータのフォルダーが残る。すると、新しいアプリの `Start()` がファイルを空にする前の最初の写しで、古い `started / version / delegate / done` を読み、合格になりうる。
- 根拠: verify_unity_ios.sh は、回ごとの `NTK_RUN` を名前に付けたファイルを待つので、この形の古いファイルで合格しない。
- 提案: `I17_RUN=<uuid>` を起動の環境で渡し、プローブに `run=<uuid>` の行を書かせて、`expect_lines` で求める。あわせて、入れる前の削除に `remove_from_device`（確かめ付き）を使う。

**I-4**
- 区分: B
- 場所: :448-455
- 指摘: 実機のビルドの `run_limited ... >/dev/null` が、`run_limited` 自身の `STOPPED`（時間切れ・止まり）と進み具合の表示まで捨てている。時間切れのときも `build: failed (0 error line(s); not shown ...)` としか出ず、原因が分からない。
- 根拠: verify_unity_ios.sh の実機ビルド（:519-522）は、`>/dev/null` を付けていない。xcodebuild の出力は、どちらの場合も `run_limited` がログに書く。
- 提案: 末尾の `>/dev/null` を外す。`run_limited` が出すのはラベルと秒数だけなので、端末の情報は漏れない。

**I-5**
- 区分: B
- 場所: :40-45
- 指摘: `DEVELOPER_DIR` を export していない。`xcodebuild` は `XCODE_APP` のものを使うが、`xcrun devicectl` と `simctl` は xcode-select のものを使う。`UNITY_EXE` と `XCODEBUILD` があるかも確かめていない。
- 根拠: verify_unity_ios.sh:182-192 は両方行っている。いまは xcode-select が Xcode26.3 を指すので表に出ていない。`XCODE_APP` を変えると、devicectl とビルドの Xcode が食い違う。
- 提案: verify_unity_ios.sh と同じ 3 つの確かめと export を足す。

**I-6**
- 区分: B
- 場所: :375-378, 546, 650（シミュレーターのみ）
- 指摘: `simulator_app` と `run_on_simulator` は置換のサブシェルで `run_limited` を呼ぶので、`RUNNING_PID` が親に見えない。
  - Ctrl-C のとき: サブシェルは死ぬが、背景の xcodebuild は SIGINT を無視するので残る。`on_exit` の `stop_running_step` も止められない。
  - TERM をスクリプトだけに送ったとき: 置換が終わるまで（最長 2400 秒）トラップが走らない。
  - 実機の経路にこの形は無い。
- 根拠: bash のコマンド置換と、非対話シェルでの `&` の SIGINT の扱い。
- 提案: アプリの道などの値は、標準出力でなく変数かファイルで返す形にして、置換をやめる。

**I-7**
- 区分: B
- 場所: :572-590
- 指摘: 設計 3.3 の G-5 の合格条件「`Frameworks/` に 2.0.0 の framework が 1 つずつ」を確かめていない。版も数も見ていない。framework が無ければ codesign が空を返して落ちるが、版が違っても通る。
- 根拠: 設計 v3 3.3 の「合格」の行。
- 提案: 2 つの framework の `Info.plist` の `CFBundleShortVersionString` を確かめる。`Frameworks/*.framework` の数も確かめる。

**I-8**
- 区分: B
- 場所: artifact/topics/ios-c-abi-2/results/2026-11-22-ios-c-abi-i17-git-url-result-v1.md 1 章
- 指摘: 記録にあるシミュレーターの回は 02:37〜02:50 の 1 回だけ。一方、スクリプトの最終の更新は 02:51 で、その回の後にある。いまのスクリプトで通ったことを、記録が示していない（依頼の「2 回通った」の 2 回目が無い）。M-2 の「許可のダイアログは出なかった」も、ヘッドレスでは直接は見ていない。根拠（`done` まで書けたこと。ダイアログが出れば Unity は止まる）を添えるべき。
- 根拠: ファイルの更新時刻。スクリプトにダイアログを見る仕組みは無い。
- 提案: 2 回目の時刻と、そのときのスクリプトの状態を書き足す。M-2 の根拠の書き方を直す。

**I-9**
- 区分: C
- 場所: scripts/ios_ui_runner.py:211, 195、DeviceRequirements.json:3（about）
- 指摘: 「only this repository's HEAD ... (no other package)」は事実と違う。`-createProject` の既定の manifest（com.unity.modules.* や ugui などの Unity の既定のパッケージ）がそのまま入る（設計 R1-12）。
- 提案: 「no third-party package besides this one (Unity's default template packages only; no Mobile Notifications)」のように書く。

**I-10**
- 区分: C
- 場所: scripts/ios_ui_runner.py:522-524、verify_unity_ios_git_url.sh:447, 462
- 指摘: approve の準備の文に 2 つの問題がある。
  - 「keep its screen on during the export and build」は、その間だけでよいと読める。実際には、その後の install・launch・写しの間（回の終わりまで）も画面が点いてロックが外れている必要がある。設計の「書き出しとビルドの間**も**」の「も」が落ちている。
  - 「The run checks the development connection first」とあるが、確かめるのはプロジェクト作成・書き出し・ビルド（約 10 分）の後。`select_device` と `select_team` も後なので、iPhone をつないでいなければ、約 10 分たってから失敗する（verify_unity_ios.sh は最初に選ぶ）。
- 提案: 文を「keep it unlocked with the screen on until the run ends (about 10 minutes of export and build come before anything reaches the iPhone) ... The run checks the development connection right before the install」に直す。必要なら、`select_device` を作成の前に移す。

**I-11**
- 区分: C
- 場所: manual/1.14.0/notification{,.ja,.ko}.md:923
- 指摘: 「Player Settings の Scripting Define Symbols に足す経路と、… 共存 … は、… シミュレーターで確かめた」とある。しかし G-4 は書き出しだけを見る確かめで、シミュレーターでは起動していない（設計の表でも端末は「なし」）。
- 提案: Player Settings の経路は「書き出しで確かめた（G-4。インストーラーが外れる）」、共存は「シミュレーターで確かめた（M-1〜M-3）」と分けて書く。

**I-12**
- 区分: C
- 場所: scripts/ios_ui_runner.py:338-340
- 指摘: `apps_for` の doctest `(3, 3)` では、`PLAYER_APPS` と `UI_APPS`（どちらも 3 件）の取り違えを捕まえられない。
- 提案: 例えば先頭の bundle id で比べる。

**I-13**
- 区分: C
- 場所: verify_unity_ios_git_url.sh:90-100
- 指摘: 実機の回でも、Simulator.app が開いていると断り、`find_simulator` も呼んでいる（結果は捨てている）。害はないが要らない。
- 提案: シミュレーターの回の中に入れる。

**I-14**
- 区分: C
- 場所: 作業ツリーの Assets/Settings/UniversalRenderPipelineGlobalSettings.asset、unity-native-plugin.slnx
- 指摘: この変更とは関係のない Unity の副作用で、更新は 00:36 と 01:44。git の URL の回（02:37〜）より前に起きている。コミットに混ぜないこと。

### 確かめて問題がなかったこと（指摘ではない）

- **実機の段の順**: 次の順になっていて、端末に触る前に承認を使っている。
  check-approval → take_run_lock → OUT_DIR とトラップ → 作成・書き出し・ビルド → select_device → use-approval → guard → 接続の確かめ → `DEVICE_TOUCHED=1` → guard と削除 → guard とインストール → guard と起動 → 写すたびに guard → guard と終了
- **device_guard**: 端末に触る段のうち、guard を通らないのは EXIT の後片付けだけ。verify_unity_ios.sh と同じ扱い。
- **承認の呼び方**: approve・check-approval・use-approval・guard に渡す引数の形が、runner のものと合っている。
- **混ぜない扱いと一覧**: `mixed_kinds` と `OWN_RUNS` の変更は正しい。test_ios_ui.sh からは `GitUrlApp/IosLaunch` を選べない。
- **実機の回で出力に出るもの**: ビルドのエラーは数だけ、devicectl の出力は OUT_DIR のログへ、details/launch/devices の JSON はすぐ消している。TEAM は使った後に空にしている。端末・Team・証明書の名前が出る経路は見当たらない。OUT_DIR は、失敗や信号のときもトラップの中で、`remove_from_device` の後に消している。
- **set -u**: 未設定のまま参照する変数は無い。
- **I17Probe（C#）**: `#nullable` と `string?` の戻り値、IL2CPP の文字列の戻り値（strdup で確保し、IL2CPP が free する）、`uint ntk_version()`（パッケージの宣言と同じ型）、名前空間はいずれも問題ない。
- **I17Probe.mm**: `objc_msgSend` をキャストして使っていて問題ない。
- **verify_unity_ios.sh の on_exit の 1 行**: 正しい。`stop_driver` が定義されるのは :419 で、トラップを掛ける :337 より後にある。

### 件数

- A: 3（I-1, I-2, I-3）
- B: 5（I-4〜I-8）
- C: 6（I-9〜I-14）
- 計: 14

## 3. 判断

すべて直したうえで、シミュレーターの回を流し直した（結果は結果の記録に書く）。

| 指摘 | 扱い |
|---|---|
| I-1、Codex 1 | 実機の回の本流で、アプリを消して消えたことを確かめてから結果を出す。確かめられなければ FAILED |
| Codex 2、I-3 | 入れる前の削除を `remove_from_device`（消えたことの確かめ付き。シミュレーターは `listapps` で確かめる）にした。回ごとの `I17_RUN` をアプリに渡し、結果のファイルに `run=<id>` が無ければ合格にしない |
| Codex 3 | 回が途中で止まったときの後片付けの削除は、ゲート（`verify_unity_ios.sh`）と同じく承認の確かめを通らない。入れたアプリを消すことは承認の文に含まれる。その旨をコメントに書いた |
| Codex 4、I-2、I-6 | `simulator_app` は変数 `APP` で、`run_on_simulator` は `<name>/result.txt` で返し、`$(...)` をやめた。I-2 の指摘どおり、それまでの 2 回の回が起動したシミュレーター（iPhone 16 / iOS 18.3）が残っていたので、落とした |
| I-4 | アーカイブと実機のビルドの `run_limited` の後の `>/dev/null` を外した |
| I-5 | `UNITY_EXE` と `XCODEBUILD` があることを確かめ、`DEVELOPER_DIR` を export する |
| I-7 | アーカイブの `Frameworks/` に `IosLibrary.framework` と `IosLibraryCApi.framework` が 1 つずつあり、どちらも版が 2.0.0 であることを確かめる（Unity の framework もあるので、一覧の全体は比べない） |
| I-8 | 結果の記録に、直した後の回と、M-2 でダイアログが出なかったことの根拠（`done` まで書けた。ダイアログが出れば Unity は止まる）を書く |
| I-9、I-10、I-12、I-13 | 文、準備の文、doctest、シミュレーターの確かめの置き場所を直した。実機は最初に選ぶ（つないでいなければ、ビルドの前に止まる） |
| I-11 | マニュアルのコメントで、G-4 は「書き出しで確かめた」、M は「シミュレーターで確かめた」と分けた |
| I-14 | コミットに混ぜない |
