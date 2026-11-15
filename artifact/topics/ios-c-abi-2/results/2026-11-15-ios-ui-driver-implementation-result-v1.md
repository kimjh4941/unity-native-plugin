# iOS の UI の手伝い役の実装の記録 v1

- 記録日: 2026-11-15（夜。利用者が不在の間に、推奨案で進めた）
- 設計: [../designs/2026-11-15-ios-ui-driver-design-v3.md](../designs/2026-11-15-ios-ui-driver-design-v3.md)（レビュー 2 回で 2 人とも A1 が 0。v3 は止めた後の直しで、レビューしていない）
- 設計のレビュー: [v1](../reviews/2026-11-15-ios-ui-driver-design-review-v1.md)、[v2](../reviews/2026-11-15-ios-ui-driver-design-review-v2.md)
- 親: [段 4 の計画](../designs/2026-11-15-ios-c-abi-stage4-plan-v1.md) の項目 1・2
- コミット: `311f4df`（設計 v1・v2、レビュー v1）、`812b267`（レビュー v2、設計 v3）、`e34c6fe`（実装）

## 1. 作ったもの

| もの | 中身 |
|---|---|
| `Tools~/IosUiTests/NtkUiTests/DriverUITests.swift`（新） | 手伝い役。`127.0.0.1` の 0 番に結び、番号を合図の行で出す。`HELLO`・`READY`・番号つきの依頼・`BYE` を 1 本の接続で受ける。10 の命令（設計 3.2）。実機では `background`・`banner-tap`・秘密の入力欄を含むアラートへの入力を断る。同じ番号の出し直しには覚えた答えを返す。画面から読んだものは出さない |
| `Tests/PlayMode/IosUiDriverProtocol.cs`（新） | 行の組み立てと答えの読み方。Unity の API を使わず、ガードも無い。Editor の PlayMode の回でテストする（`IosUiDriverProtocolTests`、41 件） |
| `Tests/PlayMode/IosUiDriver.cs`（新） | Player の側の口（`Do`・`Send`・`Wait`・`WaitForPending`）。つなぎ直し（同じ run の `HELLO`、答えの無い依頼の出し直し）。Run の終わりの受け手（`IosUiDriverRunCallback`）が `BYE` を送る |
| `Tests/PlayMode/IosSampleScreenDriver.cs`（新） | サンプルのシーンを読み、画面を開き、ボタンを押し、表示を読む。実機の Clipboard の画面の 2 段の見張り |
| `Tests/PlayMode/IosUiDriverConnectionTests.cs`（新） | サンプルを使わない 1 往復（`front`）。どの Profile でも `IosUiDriver` のテストが 1 本はある |
| `Tests/PlayMode/IosDialogSamplePlayerTests.cs`（新） | 最初の利用者: Dialog のサンプルの ID01〜03（ボタン → アラート → 手伝い役が押す → 結果の表示） |
| `scripts/verify_unity_ios.sh` | 手伝い役のビルド、Player の前に起こす、番号を渡す、`.done` の後に終わりを待つ、判定、2 つのアプリの後片付け。`.done` が来なければ止める。実機の道にも同じものを入れた（`device_guard` の後） |
| `scripts/ios_player_results.py` | `--require`（そのカテゴリが 1 本以上、skip が無い）と `--allow-skip`（Test iOS Profile のサンプル画面のテスト）。doctest を足した |
| `scripts/ios_ui_runner.py` | Player の回の承認の表示に、手伝い役の 2 つのアプリと、手伝い役が実機ですること・しないことを足した |

## 2. 設計との違い

- 行の組み立ての置き場所は `Tests/PlayMode`（設計 v3 の D-8 のとおり。v2 の `Tests/Runtime` からは変えた）
- 実機の判定（`IosSampleScreenDriver.OnDevice`）は、シミュレーターのアプリに付く環境変数 `SIMULATOR_UDID` の有無で見る。無ければ実機とみなす（迷ったら守りの側）。今の Player のテストには実機とシミュレーターを見分ける口が無かった
- 4.5 の Notification のサンプルの別の回と、配置の確かめ（9 章）は、まだ作っていない（3 章）

## 3. 残っていること

- Share・Clipboard・Notification のサンプルのテスト。Notification は 4.5 の別の回（スクリプトの変更を含む）
- V-11（つなぎ直したときに押し直さない）のテスト。手伝い役の `NTK_DRIVER_DROP_BEFORE_ANSWER` は入れてある
- 配置の確かめ: スパイクで、iOS のサンプルの画面がセーフエリアに合わせていないことが分かった（設計 v3 の 9 章）。サンプルを直すかは利用者の判断を待つ。それまで、配置の確かめのテストはゲートに入れない
- 実装のレビュー（サブエージェントと Codex）
- `test_ios_ui.sh` との組み立ての手順の共有（設計 4.1 の 1。今は `verify_unity_ios.sh` に `build_driver` を持った）

## 4. 確かめたこと

### 4.1 シミュレーター（iPhone 16 / iOS 18.3）

| 時点 | EditMode | Editor の PlayMode | Player（手伝い役つき） | 手伝い役 |
|---|---|---|---|---|
| `e34c6fe` の直前 | 1625 件（成功 1624、前からの skip 1） | 387 件成功 | 78 件成功（`IosUiDriver` 4 件、skip 0） | `DriverUITests/test_drive` 成功。依頼 4 件すべて `ok`、接続 1 本、`BYE` で終わった |

### 4.2 実機（iPhone XS / iOS 18.7.2）

承認 ID を出してから流した（`--dry-run` の 4 点と ID は会話に書いた）。端末の識別子、ログ、結果の束は残していない。

| 時点 | Player（手伝い役つき） | 手伝い役 | 時刻 |
|---|---|---|---|
| `e34c6fe` | 78 件成功（`IosUiDriver` 4 件、skip 0） | `DriverUITests/test_drive` 成功 | 21:50〜21:55 |

- **V-1・V-2・V-5 は実機でも通った**: 手伝い役の合図の行は 10 秒で届いた。IL2CPP の `TcpClient` から XCUITest のランナーの 127.0.0.1 につながり、Local Network の許可のダイアログは出なかった。`devicectl` で起動した Player に、起動し直さずに付き、Dialog のサンプルのアラート 3 つを押せた
- 終わりに 3 つのアプリ（Player、UI テストの空のホスト、ランナー）を消し、消えたことを確かめた
- 回の途中で、手伝い役のログのファイルがまだ無いという grep の警告が 1 回出た（背景の子がファイルを開く前に読んだ競合。害は無い）。起こす前にファイルを作る形に直した

## 5. 途中で起きたこと

- `IReadOnlyCollection<string>` に `Contains` が無く、別の拡張メソッド（`MemoryExtensions.Contains`）が選ばれて組めなかった。`HashSet<string>` にした
- `IosPlayerTestSupport.OnDevice` が無いのに使い、Player の書き出しで組めなかった（事前の grep の結果が空だったのを見落とした）。`IosSampleScreenDriver.OnDevice` を作った
- スパイクの最初の回は、写しの隣に `native-toolkit/dist` が無く止まった（`NATIVE_TOOLKIT_DIST_ROOT` で本物の dist を指した）
