# iOS UI テストの「入れ直さない」実行の実装の記録 v1

- 記録日: 2026-11-15
- 設計: [../designs/2026-11-15-ios-ui-keep-installed-design-v4.md](../designs/2026-11-15-ios-ui-keep-installed-design-v4.md)
- 親: [README](../README.md) の段 3-R（実機の Notification の記録のため）

## 1. 作ったもの

| もの | 中身 |
|---|---|
| `scripts/ios_ui_runner.py` | `tests --keep-installed`（`keepInstalled` をその順に出す）。`tests --device` は `keepInstalled` を出さない。`approve`・`check-approval`・`use-approval`・`guard` は `--keep-installed` を取り、承認の記録に `keepInstalled` を入れ、形の違いを両方向で断る。入れ直さない形ではテストを順ごと比べる。`DeviceRequirements.json` を読むときに、`keepInstalled` の名前が実在する・重複しない・`simulatorOnly` と重ならない・区分がすべて既知かを照らす。承認の消費は `fcntl` の排他の中で読み・照らし・書き込み（一時ファイルからの rename）を行う。`run-lock` は実行の lock を持つ子（TERM・HUP・INT を無視し、親の終了か SIGUSR1 でだけ終わる） |
| `scripts/ios_script_common.sh` | `take_run_lock`（取れたかを子の 1 行で受ける）、`run_lock_held`、`release_run_lock`（子が終わるまで待つ） |
| `scripts/test_ios_ui.sh` | `--keep-installed`。実行の lock を、引数を照らした直後（`require_clean_ios_plugins`・baseline・EXIT の trap より前）に取る。`run_keep_installed`: 照らし → 前に残ったものを消す（実機は消えたことを読み直す）→ ハーネスを 1 回だけ入れる → 接続を確かめる → NU-1 → 準備の印を待つ（30 分、15 秒ごとに状態、`caffeinate -i -w $$`、普通のファイルだけ認める、`.cancel` で止める）→ 照らしと接続 → 残りを `TEST_RUNNER_NTK_KEEP_INSTALLED=1` で順に。止まったら流さなかったテストを `did not run (理由)` と出して失敗に数える。`device_guard` は lock を持っていることも照らす |
| `scripts/verify_unity_ios.sh` | 同じ実行の lock を取り、`device_guard` で照らす |
| `Tools~/IosUiTests/DeviceRequirements.json` | `keepInstalled`（NU-1・4・5・7・11）、区分 `focus`・`longpress`。5 件を `simulatorOnly` から外した |
| `NotificationUITests` | `NTK_KEEP_INSTALLED` が `1` なら `grant()` はダイアログが出ないことを照らす（前のコミットで入れた） |

## 2. 確かめたこと（2026-11-15）

| 確かめ | 結果 |
|---|---|
| doctest（`python3 -I -m doctest scripts/ios_ui_runner.py`） | 成功。承認の形の両方向、順の違い、`DeviceRequirements.json` の照らし、**同じ承認を 8 本の thread で同時に消費して成功は 1 本だけ** |
| 実行の lock（scratchpad の写しで、持ち主のシェルをまねて） | 2 つ目の実行は断られる。持ち主を KILL すると lock は離れ、次の実行が取れる。**グループに TERM を送っても、後片付け（3 秒）が終わるまで lock は離れない**。続けて 5 回流すと 5 回とも取れる |
| `verify_unity_ios.sh --skip-editmode`（シミュレーター） | 59 件成功（lock を取る形で） |
| `test_ios_ui.sh --keep-installed`（シミュレーター） | NU-1 が通った後に止まり、15 秒ごとに状態を出し、準備の印（agent が作った）を 30 秒後に認め、NU-4・5・7・11 を入れ直さずに流して**すべて成功**。終わりに印のファイルも消えた |
| 続けて流した `test_ios_ui.sh`（今までの形） | 1 回目は「別の実行が lock を持っている」で断られた。前の実行の lock の持ち主が、解放の合図から 1 秒以内に終わるまでの間に始まったため。`release_run_lock` が持ち主の終わりを待つように直し、続けて 5 回取れることを確かめた |

## 3. まだ確かめていないこと

- 実機での `--keep-installed`（利用者の準備: 集中モードで通知を許すアプリをハーネスだけにする。承認の一覧に準備と所要時間が出る）→ 済み（1.x で NU-1・4・5・7 が成功〈[README](../README.md) の段 3-R の行〉。2.0.0 で 5 件が成功〈[Notification の実装の記録](../../../ios/notification/results/2026-11-15-ios-notification-cabi-implementation-result-v1.md)〉）
- 接続の確かめで見る欄（今は `devicectl device info details` が 60 秒以内に成功することだけ）。実機の出力で決める
- 偽の印・場所の印を認めないこと、待ちの間の HEAD の変化、時間切れ、`.cancel` は、シェルを端末の操作をまねる関数に差し替えて流す確かめを、まだ作っていない（設計 v4 の 3 章）
