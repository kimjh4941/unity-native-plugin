# 段 2 (B) iOS の UI の自動化の実装のレビュー v1

- 記録日: 2026-11-14
- 対象: `Assets/NativeToolkitIosUiHarness/**`、`Tools~/IosUiTests/**`、`scripts/test_ios_ui.sh`、`scripts/ios_ui_runner.py`、`scripts/ios_script_common.sh`、`scripts/verify_unity_ios.sh` の整理（コミット前の作業木）。実装結果は [../results/2026-11-14-ios-stage2b-ui-automation-result-v1.md](../results/2026-11-14-ios-stage2b-ui-automation-result-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。実機の経路はまだ流していない（読むだけのレビュー）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

`bash -n` は 3 本とも通った。`python3 -I -m doctest scripts/ios_ui_runner.py` も通った。手元の `env bash` は 3.2.57。

### R-1 [重大] 実機の経路は、1 件目のテストの前で必ず止まる
- 根拠: `scripts/test_ios_ui.sh:300` の `use-approval` が、記録を `used=True` に書き換える（`ios_ui_runner.py:206-209`）。そのあと `run_one` が呼ぶ `device_guard`（`test_ios_ui.sh:166`、279、286）は `check-approval` を使う。ところが `approval_problems` は `used` を問題として数える（`ios_ui_runner.py:116-117`）。
- 壊れ方: すべてのテストが「stopped: … the approval was already used」で止まり、結果は FAILED になる。止まる向きなので安全側ではある。ただ、承認は組み立ての後に使い切られ、実機の確かめは一度もできない。さらに `DEVICE_TOUCHED=1` は立つので、`on_exit` は照らし直しなしで 3 つの uninstall と `info apps` を端末に送る（174、186-192 行）。設計 4.6 の「端末に触れる手順の直前ごとに照らし直す」も後片付けでは守られていない。
- 直し案: `use-approval` のときに、実行ごとの印（ランダムな値）を記録に書く。照らし直しは別の子コマンド `guard <ID> <印>` にして、「使い済み、かつ印が一致、かつ HEAD・木・テストが同じ」を見る（native-toolkit の `device-gate` と同じ形）。後片付けの uninstall は承認した 3 つの bundle id に限ったうえで照らしなしに許すのか、設計の文として決めて書く。doctest に「use の後でも guard が通る」1 件を足す。

### R-2 [中・未確認の危険] `-destination id=` に devicectl の識別子を渡している
- 根拠: `test_ios_ui.sh:133` が取るのは `identifier`（CoreDevice の UUID）で、210 行と 288 行で xcodebuild に渡している。native-toolkit は両者を分けている。`artifact/topics/ios-c-abi/results/probes/stage0a/run-device.sh:4` には「hardware UDID … not the devicectl identifier」とあり、`scripts/ios_ui_runner.py:299-306` は `hardwareProperties.udid` と照らしている。
- 壊れ方: xcodebuild が宛先を見つけられず、テストがすべて落ちる見込みが高い。
- 直し案: 同じ JSON から `hardwareProperties.udid` も取り、xcodebuild には `platform=iOS,id=<UDID>` を渡す。どちらの値も出力しない。

### R-3 [中] Team の取り方が当て推量で、署名のエラー行から個人の値が出うる
- 根拠: `test_ios_ui.sh:145-146` は、Provisioning Profiles の中で名前順の最初のファイルの Team を使う。期限切れのものや別の Team のものでも、そのまま使われる。242 行と 260 行は `error:` 行を表示し、伏せるのはその `$TEAM` だけ。
- 壊れ方: Team が複数あると違う Team で署名して失敗する。そのとき、エラー行に別の Team ID や証明書の名前（本名と ID）がそのまま出る。設計の「Team ID を書かない」に触れる。自動署名で `-allowProvisioningUpdates` を付けない形も、S-9 で確かめたのはアプリの archive だけ。`.xctrunner` の署名と、Xcode が管理するワイルドカード profile の期限は確かめていない（未確認）。
- 直し案: 期限内の profile から Team を集め、ちょうど 1 つのときだけ使う（または環境変数で受ける）。実機のときは、エラー行を出さずに件数と種類だけを出す。

### R-4 [中] 共有シートを閉じる処理が、座標を当てずっぽうで押す
- 根拠: `SystemUI.swift:27`。「閉じる」が 3 秒で見つからないと、画面の (0.5, 0.05) を押す。
- 壊れ方: 実機でシートの形が違うと、座標が共有先の行（連絡先やアプリ）に当たりうる。device-testing.md の「実在のアプリへ送りうる操作」にあたり、native-toolkit が D22 を実機から外した理由（座標の操作）とも同じ。
- 直し案: 座標で押す処理は消す。見つからなければ失敗にする。

### R-5 [中・未確認] `test-without-building` が一時フォルダーの外に書きうる
- 根拠: `test_ios_ui.sh:287-289` には `-derivedDataPath` が無い。native-toolkit の実機の呼び出しには付いている（`test_ios.sh:778`）。
- 壊れ方: 既定の DerivedData の下に、端末の画面を含むログや添付が残る可能性がある。そうなると「終わりに消す」が効かない。
- 直し案: `-derivedDataPath "$OUT_DIR/test-dd"` を足す。

### R-6 [軽微] 後片付けの devicectl に時間の上限が無く、確かめが失敗を見逃す
- 根拠: `on_exit` はシグナルを無視したまま（184 行）、devicectl を上限なしで呼ぶ（174、191 行）。191 行は devicectl が失敗すると `left=0` になる。
- 壊れ方: 端末が切れると終わらない。アプリが残っていても「消えた」ことになる。
- 直し案: `run_limited` で上限を付ける。`info apps --bundle-id <id> --json-output` で読み、読めなかったときは警告する。

### R-7 [軽微] 判定の穴
- 根拠: `run_one` は xcodebuild の終了コードを見ていない（287-295 行）。テスト一覧が空のとき、bash 4.4 以降では 312 行のループが 0 回で回り、PASSED になる（3.2 では unbound で落ちるので、いまは表に出ない）。
- 直し案: `${#TESTS[@]} -eq 0` なら止める。xcodebuild のコードが 0 であることも条件に足す。

### R-8 [軽微] 承認で照らす範囲と、端末の選び方
- 根拠: `ios_ui_runner.py:64-76`。`Assets/Scripts`・`Assets/Editor`・`Assets/Plugins`・`Packages/manifest.json`・`packages-lock.json` もハーネスのバイナリに入るが、照らしていない。`--untracked-files=all` は無視されたファイルを見ない。
- ほかに:
  - 承認がどの端末か（ハッシュ）を持たない。
  - check と use がロックなしで、同じ ID の同時実行を防げない（native-toolkit は fcntl でロックしている）。
  - ペア済みの iPhone が 2 台あると、繋いでいない方があっても必ず断る（133-137 行。止まる向き）。
- 空白を含む道（`Assets/Settings/Build Profiles`）は argv で渡しているので正しい。

### R-9 [軽微] ハーネスの細部
- `IosUiHarness.Start` に try/catch が無い（53-64 行）。ネイティブには届かないが、失敗すると「launch 行が無い」としか分からない。
- `.mm:67` は `%s` を書式に使うので、UTF-8 ではなく既定の C 文字列のエンコーディングで読まれる。日本語の値は文字化けする。`%@` と `stringWithUTF8String:` に替える。
- tearDown は `springboard.alerts` しか見ない。アプリ自身のアラートが残っても見逃す（`HarnessUITestCase.swift:33`）。

### R-10 [軽微] 通知のテストの 5 秒が実機では際どい
- 根拠: `NotificationScenario.cs:20`、`NotificationUITests.swift:11-14`。予約から 5 秒後に通知が出る。
- 壊れ方: 実機の XCUITest は遅く、Home を押す前に通知が出てバナーを取り逃しうる。
- 直し案: 間隔を 10〜15 秒にする。

### 問題が無かったもの
- **Clipboard**: 実行中の印は操作の種類ごとに持ち、コールバックより前に外す（`IosClipboardManager.cs:705-708`、`FireReadResult`）。コールバックは try/catch で包まれている。Read のコールバックの中で RemovePasteboard を呼んでも問題は無い。
- **イベント**: 購読は 1 回の起動で 1 回だけ。
- **ビルド**: `IosUiHarnessBuild` は finally で Player Settings を戻す。定義の制約と `extraScriptingDefines` で、ほかのビルドには入らない。
- **bundle id**: 3 つとも合っている（`.xctrunner` も含む）。
- **一時フォルダー**: trap を張った後の終わり方では、すべて消える。trap の前に止まる経路も、自分で消している。
- **承認を組み立ての前に使い切る点**: device-testing.md の「止まった後の流し直しにも新しい許可が要る」に合っており、意図どおり。
- **割り込みのモニター**: 押す操作を邪魔されたときにしか呼ばれない。アラートが出ただけでは呼ばれず、tearDown の確かめが補っている。
- **verify_unity_ios.sh の整理**: ふるまいは同じ。違いは、STOPPED の文が watched のファイル名だけになったこと（basename）の 1 点。

| 区分 | 件数 |
|---|---|
| 重大 | 1（R-1） |
| 中 | 4（R-2〜R-5。R-2 と R-5 は未確認の危険） |
| 軽微 | 5（R-6〜R-10） |

## 2. Codex

1. **重大 — 消費した承認を再確認で必ず拒否する**
   対象: test_ios_ui.sh:300。`use-approval` が ios_ui_runner.py:207 で `used=True` にした後、各テストの 279行 が再び `check-approval` を呼びます。しかし ios_ui_runner.py:116 は使用済み承認を無条件で拒否します。ビルドが成功しても実機テストは開始できません。
   **修正:** 開始時の「未使用確認・消費」と、実行中の「この実行が消費した承認との照合」を分離してください。

2. **重大 — 承認確認に失敗しても、終了処理が実機アプリを削除する**
   対象: test_ios_ui.sh:187。311行 は最初の承認再確認より前に `DEVICE_TOUCHED=1` を設定します。そのため上記の拒否時にも、終了処理は remove_app:174 を通して3アプリの削除を実行します。終了処理には承認の再確認がなく、HEADや対象コードが変更されて承認不一致になった場合も同じです。
   **修正:** 実機の削除処理にも現在の実行に対する承認照合を適用し、操作対象は承認確認後に記録してください。

3. **中 — `devicectl` の識別子を `xcodebuild` の宛先に流用している**
   対象: test_ios_ui.sh:210。値の取得元は 133行 の `identifier` です。参照元は、native-toolkit の ios_ui_runner.py:300 で `hardwareProperties.udid` と対応を確認し、test_ios.sh:995 ではそのUDIDを `-destination` に渡しています。CoreDevice識別子とUDIDは別の値です。
   **修正:** `devicectl` 用識別子とハードウェアUDIDを分け、`xcodebuild` には後者を渡してください。

4. **中 — テスト実行の異常終了・タイムアウトを合否から落としている**
   対象: test_ios_ui.sh:287。`run_limited` は ios_script_common.sh:114 でタイムアウト時に124を返しますが、呼び出し側は終了コードを保存せず、295行 の結果JSON判定だけを返します。成功したTest Caseを含むバンドルが残っていれば、実行処理が異常終了しても最終的に `PASSED` になります。
   **修正:** 終了コード0と結果JSONの成功を両方必須にし、タイムアウトは無条件で失敗にしてください。

（注）これは Codex の出力をそのまま転記したものです。中継したサブエージェントは、コードを検証していません。

## 3. 判断

利用者の指示（2026-11-14、「判断は推奨案でいい」）に従い、次のとおり決めた。

- **直す**:
  - R-1・Codex の 1（重大）: `use-approval` が実行ごとの印を記録に書き、照らし直しは新しい子コマンド `guard`（使い済み、印が一致、HEAD・木・テストが同じ）で行う。doctest を足す
  - Codex の 2（重大）・R-1 の後半: 端末に触れた印は、照らし直しが通って最初に入れる直前に立てる。**後片付けの uninstall は照らし直しなしで行う**（承認の一覧に「入れて、終わりに消す」と書いた 3 つの bundle id だけ。消さずに残すほうが、利用者の端末に承認していないものを残す）。この決めをスクリプトの説明に書く
  - R-2・Codex の 3: xcodebuild には `hardwareProperties.udid` を渡し、devicectl には `identifier` を渡す。どちらも出力しない
  - R-3: 期限内の profile から Team を集め、ちょうど 1 つのときだけ使う。実機のときは `error:` 行を出さず、件数だけを出す
  - R-4: 共有シートを座標で閉じる処理を消す。閉じるボタンが無ければ失敗
  - R-5: `test-without-building` にも `-derivedDataPath`（一時フォルダの中）
  - R-6: 後片付けの devicectl に時間の上限。残りの確かめはアプリごとに読み、読めなければ警告
  - R-7・Codex の 4: xcodebuild の終了コード 0 も要件にする。テストの一覧が空なら止まる
  - R-8 の一部: 照らす範囲に `Packages/manifest.json`、`Packages/packages-lock.json`、`Assets/Editor`、`Assets/Plugins` を足す
  - R-9: `Start` を try/catch で包む。`.mm` は `%@` と `stringWithUTF8String:`。tearDown でアプリ自身のアラートも見る
  - R-10: 通知の間隔を 10 秒にする
- **直さない（実装結果に書く）**: R-8 の残り（承認に端末のハッシュを持たない: dry-run は端末に触れないので端末を知らない。同じ ID の同時実行のロック: 利用者 1 人の手元の実行で、同時に 2 つ流さない。2 台目のペア済みの iPhone で断る: 止まる向き）
