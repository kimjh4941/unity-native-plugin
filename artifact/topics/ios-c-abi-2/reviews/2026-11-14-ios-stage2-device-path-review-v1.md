# 段 2 の実機の経路（`verify_unity_ios.sh --device` と共通化）の実装のレビュー v1

- 記録日: 2026-11-14
- 対象: `scripts/verify_unity_ios.sh` に足した実機の経路（`--device --dry-run` / `--device --approved <ID>`）、`scripts/ios_script_common.sh` に移した端末と Team の選び方、`scripts/test_ios_ui.sh` の共通化、`scripts/ios_ui_runner.py` と `DeviceRequirements.json` の `PlayerTests/IosPlayer`（`5e86b34` に対するコミット前の差分）
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。実機の経路は一度も流していない（読むだけのレビュー）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

確かめたこと: 3 本とも `bash -n` が通る。`ios_ui_runner.py` と `ios_player_results.py` の doctest も通る。`runner tests` の出力に PlayerTests は出ない。DeviceRequirements.json は JSON として読める。実機ではまだ一度も流していないので、devicectl の挙動は下の「未確認」にまとめた。

### R-1 [中] test_ios_ui.sh は `--tests PlayerTests/IosPlayer` を受け付けてしまい、承認の一覧と実際に入れるアプリが食い違う
- 根拠: `test_ios_ui.sh:87-88` は `--tests` をそのまま配列にしている。`runner tests` の出力（PlayerTests を隠した一覧）とは照らしていない。`ios_ui_runner.py:205-208` は `requirements()` の全項目で検証するので、PlayerTests も「既知」として通る。`approve` は `:224` で PLAYER_APPS（playertests）だけを一覧に出す。ところが実行時の `test_ios_ui.sh:281-292` は harness を入れ、xcodebuild も uitesthost と xctrunner を入れる。DeviceRequirements.json の about にある「test_ios_ui.sh never selects it」は、この経路では成り立たない。
- 壊れ方: 利用者が承認するのは「playertests を入れて消す」という一覧だが、実機には承認していない 3 つのアプリが入る（テストは did not run で失敗する）。UI テストと混ぜて指定した場合も、一覧には「player tests を流す」と出るのに実際には流れない。
- 直し案: test_ios_ui.sh で、`--tests` の各項目が `runner tests` の出力に含まれるかを確かめて拒む。あるいは `approve`/`check-approval`/`guard` に種別の引数（ui|player）を足し、runner の側で混在と取り違えを拒む。doctest も 1 件足す。

### R-2 [中] copy from と terminate の前に guard が無い
- 根拠: guard を呼んでいるのは `verify_unity_ios.sh:350`（uninstall の前）、`:354`（install の前）、`:357`（launch の前）だけ。ポーリングの copy（`:366-369`、最大 60 回）、xml の copy（`:375-378`）、terminate（`:381-382`）の前では呼んでいない。
- 壊れ方: 実行中に HEAD や guarded tree が変わっても、端末への操作が続く。「端末に触る手順ごとに照らす」という説明（`:38-39`）と食い違う。copy は読むだけの操作なので device-testing.md:35 の扱いに入る余地はあるが、terminate は端末の状態を変える。
- 直し案: 各 copy と terminate の前に `device_guard || break/return 1` を置く。terminate を guard なしで許すなら、後片付けの uninstall と同じように「この実行が起動したものを止める」例外として説明に書く。

### R-3 [軽微] 入れる前に .app の bundle id を確かめていない
- 根拠: `:300-306` から `:355` の install まで、`CFBundleIdentifier` を見ていない。後片付けが消すのは PLAYER_ID だけ（`:209-210`）。いまは `IosPlayerBuildSetup.cs:73` が bundle id を設定し、`:272` の export ログの確認で間接的に守られている。
- 直し案: `PlistBuddy -c 'Print CFBundleIdentifier'` が PLAYER_ID と一致することを、install の前に確かめる（多重の守り）。test_ios_ui.sh:248-253 も同じ。

### R-4 [軽微] ポーリングの時間の数え方と、結果の判定
- 根拠: `:371` は 5 秒しか待たないのに `waited` に 10 を足している。1 回の copy は最低 2 秒、長いと 60 秒かかる。そのため 600 は実時間と一致せず、最悪で約 65 分になる。プレイヤーが落ちたことにも気づけない（シミュレーターの `kill -0` に当たるものが無い）。copy の成否は、ファイルがあるかどうかだけで見ている（`:370`、`:384`）。
- 壊れ方: PASSED に誤って着く経路は無い。xml が壊れていれば judge が失敗にするし、`:399` で数えている。ただし待ち時間が長すぎたり、誤って早く止まったりはありうる。
- 直し案: 開始時刻からの実時間で上限を決める。copy の終了コードと `[ -s ]` の両方を条件にする。

### R-5 [軽微] trap を張る前に signal が来ると一時フォルダーが残る
- 根拠: `mktemp` は `:149`、trap は `:240`。select の失敗は自分で消している（`:151`）が、`select_device` の最中の Ctrl-C では、識別子を含む devices.json ごと `$TMPDIR` に残る。test_ios_ui.sh:140-205 も同じ。
- 直し案: mktemp の直後に、一時的な `trap 'rm -rf "$OUT_DIR"' EXIT INT TERM HUP` を張る。

### R-6 [軽微] 流す前に開発用の接続を確かめていない
- 根拠: native-toolkit の device-testing.md:48 は、`devicectl device info details` などで tunnel と DDI を事前に確かめるよう求めている。どちらのスクリプトもしていない。切れていると、600 秒ポーリングし続けてから失敗する。

### R-7 [軽微] シミュレーター経路の小さな差
- 根拠: ビルド失敗の文が basename だけになった（`ios_script_common.sh:184`。最後の `Logs and results` で補える）。.app の glob が `*/*.app` に広がり、boot より前に移った。見つからないときの文はパスの一覧から件数に変わった。合否は変わらない。

### 問題が無かったもの
- 1（端末の安全）: `check-approval` は `:118`、`use-approval` は `:393` で、どちらも端末に触る前。`DEVICE_TOUCHED=1` は最初の guard が通った直後で、最初の uninstall の直前（`:350-351`）。後片付けで消すのは playertests だけ（`:209-218`）。識別子と Team は出していない（`show_build_errors`、run_limited の出力は `>/dev/null`、guard の文はパスだけ）。trap より後の終わり方では、`:228-229` で一時フォルダーが消える。
- 2（失敗の数え方）: launch の失敗、xml が無い場合、judge が 0 以外を返した場合は、どれも `failures` に入る。
- 3（共通化）: `select_device` と `select_team` の中身は元と同じで、失敗時の exit 2 と rm も残っている。`DEVICE_TOUCHED` の位置（`test_ios_ui.sh:280-281`）、UDID と identifier の使い分け（`:213`）、後片付けを INSTALLED_IDS に限る点も残っている。
- 4（runner）: `tests` は PlayerTests を隠している。`approve` のアプリの一覧の出し分けも正しい。承認は tests の一覧を完全に照らすので、UI テストの承認を verify で使い回すことはできない。

### 未確認の危険（実機でしか分からない）
- `device process launch --environment-variables '<JSON>'` が受け付けられ、Unity の `Environment.GetEnvironmentVariable` まで NTK_RUN が届くか。
- launch の JSON に `result.process.processIdentifier` があるか。無ければ pid が空になり、terminate を飛ばす（後片付けの uninstall が止める）。
- copy from の `--source` が container の根からの相対か。`persistentDataPath` は Documents なので `Documents/ntk-player-tests/...` で合っているはずで、シミュレーター側の `:326` とも一致する。
- copy from の `--destination` がファイルとディレクトリのどちらとして扱われるか。ファイルが無いときに空のファイルを残すか。
- Release 構成で Automatic 署名した場合に get-task-allow が付き、appDataContainer から copy できるか。
- team の wildcard profile が playertests の bundle id に当たるか。
- 実行中の自動ロックでプレイヤーが止まらないか。

件数: 重大 0、中 2（R-1、R-2）、軽微 5（R-3〜R-7）、未確認の危険 7。

## 2. Codex

- **重大 — Player承認で未承認のUIアプリを操作できる。** scripts/ios_ui_runner.py:224 は `PlayerTests/IosPlayer` だけならPlayerアプリだけを承認一覧に出します。しかし `test_ios_ui.sh --tests PlayerTests/IosPlayer` は拒否されず、承認照合を通過し、282～284行でUIハーネスを削除・インストールします。結果判定で失敗する前に、一覧にないアプリを操作します。修正: UIランナーでPlayerエントリを明示的に拒否し、承認を実行するランナーにも紐付けてください。

- **中 — 起動後の端末操作にguardがない。** scripts/verify_unity_ios.sh:366 の完了マーカー取得、375行のXML取得、381行のプロセス終了には `device_guard` がありません。最後の照合は357行なので、起動後にHEADや保護対象が変わって承認が無効になっても、これらを続行します。修正: ポーリングの各回を含め、通常の端末操作の直前にguardを追加してください。

- **中 — アプリが残ってもPASSED・終了コード0になる。** scripts/verify_unity_ios.sh:209 は終了時アンインストールの失敗を無視します。214～217行で残存を確認しても警告だけで、407行の `PASSED` は後始末より先に出ます。テスト成功後に削除が失敗すると、アプリを残したまま成功扱いになります。修正: 成功表示前に削除と不在確認を完了し、削除失敗・確認不能なら非ゼロで終了してください。

- **中 — 端末選択中の中断で一時ファイルが残る。** scripts/verify_unity_ios.sh:149 で一時ディレクトリを作り、150行から端末選択しますが、終了・シグナルのtrap登録は240～243行です。その間のSIGINT/SIGTERMでは後始末されず、選択処理が書いた端末識別情報入りの `devices.json` も残り得ます。修正: 一時ディレクトリ作成直後、端末選択より前に削除用trapを登録してください。

（注）いずれも Codex によるレビュー結果。読み取り専用で、スクリプトや Unity 等は実行していない（中継したサブエージェントの注）。

## 3. 判断

利用者の指示（2026-11-14、「判断は推奨案でいい」）に従い、次のとおり決めた。

- **直す**:
  - Codex の重大・R-1: 承認の種類を分ける。runner は `PlayerTests/IosPlayer` と UI テストの混在を断り、`test_ios_ui.sh` は UI テストの一覧に無い名前を断る
  - Codex の中・R-2: copy と terminate の前ごとに照らし直す。照らし直しに通らなければ止め、後片付けの uninstall（この実行が入れたもの）に任せる
  - Codex の中（アプリが残っても PASSED）: 両方のスクリプトで、後片付け（消す、消えたことを読む）を結果の表示より前に行い、消せなかった・読めなかったときは失敗にする
  - Codex の中・R-5: 両方のスクリプトで、一時フォルダを作った直後に、消すための trap を張る
  - R-3: 両方のスクリプトで、入れる前に `.app` の `CFBundleIdentifier` を確かめる
  - R-4: ポーリングの上限を実時間で数え、copy の終了コードと空でないことを条件にする
- **直さない（実装結果に書く）**: R-6（流す前の接続の確かめ。切れていれば上限で止まる向きなので、実機の 1 回目で様子を見る）、R-7（ふるまいは同じ）、未確認の危険 7 件（実機の 1 回目で確かめる）
