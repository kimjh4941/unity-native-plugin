# iOS の UI の手伝い役の設計 v2 のレビュー v2

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-ui-driver-design-v2.md](../designs/2026-11-15-ios-ui-driver-design-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。2 回目なので差分（前の指摘の判定と、v1 から変えた部分）を見た
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

新しい指摘の件数: A1 0 / A2 3 / B 2 / C 0

## 1 回目の指摘はどうなったか

| 指摘 | 判定 | 理由 |
|---|---|---|
| S-1 | 一部 | `background` と `banner-tap` は、3.2 の「実機」の列、7 章の表、`IosSimulatorOnly`、`device-only-refused` の二重の守りで解決した。ログインのダイアログは、手伝い役の守りが「秘密の入力欄に入れない（どこでも）」だけになっている。7 章の「同上」は、実機でユーザー名の欄に打つことを断らない（S2-4） |
| S-2 | 一部 | 7 章は決めたとおりに直った。ただし `Press` の見張りが、名前付きに切り替えるボタンまで止めてしまう（S2-2） |
| S-3 | 解決 | 4.4 に 3 つのアプリ、承認の表示、別の PID と `on_exit`、止まりの見張りを当てないことがある。4.1 の 2 に `device_guard`、4.3 に「Player の上限 + 120 秒」がある。`.done` が来ない道は S2-5 で別に挙げた |
| S-4 | 解決 | 決めたとおり 12 章（実装）に送った。Profile での件数の判定の穴は S2-3 |
| S-5 | 解決 | 置き場所はパッケージの側（4.2、12 章）に移った。順は、`.done` の後にスクリプトが手伝い役の終わりを待つ（4.1 の 5、4.3）ので、呼ばれる順に頼らなくなった |
| S-6 | 解決 | 4.5 で、入れ直した別の回と `[Order]` に分け、`deny` はサンプルでは見ない、と決めた。今の UI のハーネスも「入れ直せば許可が戻る」を前提にしている（`DeviceRequirements.json` の `reinstall`）ので、前提は合っている |
| S-7 | 解決 | 3.1 に、同じ run の `HELLO` でつなぎ直すことと、覚えた答えを返すことがある。V-4 に 1 往復が足された |
| S-8 | 解決 | D-1・D-3 で、0 番のポート、標準エラー、127.0.0.1 に結ぶことが決まった。V-1 に、合図の行と実機を確かめることが残っている |
| S-9 | 解決 | 決めたとおり、7 章で「汎用を読むテストは、Player が先に書いてから読む」とした |
| S-10 | 解決 | 4.3 に、上限の関係と、60 秒待っても終わらないときは止めて失敗にすることがある |
| S-11 | 一部 | 5 項目のうち 4 つは入った。「行の組み立ては別のファイル」の置き場所が組めない場所になっている（S2-1） |
| C-1 | 解決 | 12 章に送った。`banner-tap` はシミュレーターだけになった |
| C-2 | 解決 | S-4 と同じく 12 章 |
| C-3 | 解決 | S-6 と同じく 4.5 |
| C-4 | 解決 | S-5 と同じく 4.3 |

## 新しい指摘

### S2-1 [A2] D-8 の `Tests/Runtime/IosUiDriverProtocol.cs` は Player のアセンブリから参照できない
- **根拠**
  - `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef` は `includePlatforms: ["Editor"]` で、`NativeToolkit.Editor` を参照している。
  - `IosUiDriver` が入る `Tests/PlayMode/NativeToolkit.Runtime.PlayModeTests.asmdef` は、`NativeToolkit.Runtime` しか参照していない。
- **壊れ方**
  - `IosUiDriver` が行の組み立てを呼べない。
  - 参照を足せば、Player が Editor 専用のアセンブリを参照することになり、iOS の書き出しのコンパイルで止まる（`error CS` が出て、表に出る）。
- **直し方の案**
  - `IosUiDriverProtocol.cs` を `Tests/PlayMode` に置き、`#if` の外に出す。こうすれば Editor でも組まれる。
  - EditMode のテストの asmdef（`Tests/Runtime`）の参照に `NativeToolkit.Runtime.PlayModeTests` を足す。
  - asmdef を変えるので、利用者の形（git の URL）でも組めるかを確かめる。

### S2-2 [A2] Clipboard の見張りが、最初に押す「名前付きに切り替える」ボタンも止めてしまう
- **根拠**
  - v2 の 7 章（192 行）には「実機・Clipboard の画面・表示の対象が汎用のとき、押す前に `Assert.Fail`」とある。同じ段落で「開いたら最初に `UseFixedNamedScopeButton` か `CreateNamedPasteboardButton` に切り替え」ともある。
  - 開いた直後の表示は `Scope: general`（`IosClipboardManagerExampleController.cs:101` の初期値、`:1420-1423` の `UpdateStatus`、`IosClipboardSampleResult.cs:120`）。
  - また `ErrRemoveGeneralButton` は、今の対象に関係なく General を消しにいく（`:1262-1268`）。対象の表示で見張る形では、名前付きに切り替えた後に押されても捕まらない。7 章の「カテゴリの付け忘れでも触らない」は、このボタンについては成り立たない。
- **壊れ方**
  - 設計どおりに書いた実機のテストが、最初の `Press(UseFixedNamedScopeButton)` で必ず落ちる（表に出る）。
- **直し方の案**
  - 見張りを 2 段に分ける。
    - 実機では、`UseGeneralButton` と `ErrRemoveGeneralButton` を名前でいつも断る。
    - 表示が汎用の間は、切り替えのボタン（`UseFixedNamedScopeButton`、`CreateNamedPasteboardButton`、`CreateUniquePasteboardButton`）だけを通す。

### S2-3 [A2] Profile の回（`--as-package-user`）では、6 章の「`IosUiDriver` が 1 本も無い」で必ず落ちる
- **根拠**
  - v2 の 6 章（177〜179 行）は、Profile では `IosSampleScreen` を skip と件数の 2 つの判定から外す。
  - ところが、11 章の 4 に挙がっている利用者は、すべてサンプル画面のテストになっている。
  - 4.5 で足した 2 回目の回（`IosNotificationSample` だけ）は、Profile ではすべて Ignore になる。D-5 は Profile にも手伝い役を付けるとしているが、4.5 は Profile でも 2 回目を回すのかを書いていない。
- **壊れ方**
  - Profile の 1 回目も 2 回目も、外した後の件数が 0 になり、失敗する（表に出る）。
- **直し方の案**
  - Profile では件数の判定を当てない。または、サンプルを使わない `IosUiDriver` のテスト（例: `HELLO`・`BYE` だけの 1 本）を必ず置く。
  - 4.5 に「Profile では 2 回目を回さない（シーンが無いため）」と書く。

### S2-4 [B] 実機でログインのダイアログに打つことを、手伝い役が断らない。シミュレーターでは逆に Login まで届かない
- **根拠**
  - v2 の 3.2（89 行）は、秘密の入力欄を「どこでも」`bad-arg` で断る。7 章（188 行）の守り方は「同上」となっている。しかし `device-only-refused` は命令の単位で断るもので、`alert-type` そのものは実機でも「使う」になっている。そのため、実機でログインのダイアログのユーザー名の欄（`textFields`）に打つことは断られない。
  - 一方、サンプルは `enableLoginWhenEmpty = false`（`IosDialogManagerExampleController.cs:299`）で、ハーネスの ID-12 はパスワードまで打って Login を押している（`DialogUITests.swift:92-102`）。
  - また、`textFields` は秘密の入力欄を含まない。そのため、番号で指す `alert-type` が `bad-arg` になる場面は書かれた形では起きない。
- **壊れ方**
  - カテゴリを付け忘れると、実機で利用者の端末のログインのダイアログに文字が打たれる（自動入力の候補が出る）。手伝い役の守りが二重になっていない。
  - シミュレーターでは、サンプルのログインのテストが Login を押す道を通れない。網羅が欠ける。
- **直し方の案**
  - `TEST_RUNNER_NTK_DRIVER_DEVICE=1` のときは、秘密の入力欄を含むアラートへの `alert-type` を、どの欄でも `device-only-refused` で断る。
  - シミュレーターでは、秘密の入力欄に入れられるようにする（引数で欄の種類を指す）。

### S2-5 [B] `.done` が来ずに終わった回で、手伝い役を止める手順が無い。4.5 の 2 回目と同じ端末でぶつかりうる
- **根拠**
  - v2 の 4.1 の 5 と 4.3（130・142 行）は、`.done` が来た道しか書いていない。4.4（147 行）が止めるのは `on_exit` だけ。
  - 今の流れは、失敗しても先へ進む（`verify_unity_ios.sh:438` の `run_player_tests || failures=...`）。Player が落ちたときや 600 秒で切れたときも、そのまま次へ行く（`:376-381`）。
- **壊れ方**
  - 1 回目の Player が落ちたり 600 秒で切れたりしても、その回の手伝い役は上限（Player の上限 + 120 秒）まで端末の上で生きている。
  - その間に 4.5 の 2 回目が、同じ端末に同じランナー（`...uitests.xctrunner`）を入れて xcodebuild を起こす。すると 2 本の UI テストの回が重なる。あるいは、6 章の判定がその上限まで待たされる。
- **直し方の案**
  - 4.1 に「Player の回が `.done` 無しで終わったら、手伝い役を `stop_tree` してから後片付けと次の回へ進む。手伝い役の判定は『止めた』として失敗にする」を足す。

## 止めてよいか

A1 は 0 件なので、設計はここで止めてよい。S2-1〜S2-3（A2）は実装で直し、S2-4・S2-5（B）は設計の 3.2・4.1 に 1 行ずつ足すのが望ましい。

## 2. Codex

A1 0 / A2 3 / B 1 / C 0

- C-1：一部 — 既知の A2 として実装修正へ移管済みですが、通知題名の照合方式は未確定です。[v2:248](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:248)
- C-2：解決 — `IosPlayer` の付与、Profile 限定の skip 許容引数、その doctest が計画に明記されました。[v2:249](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:249)
- C-3：解決 — Notification サンプルを再インストールする別実行へ分離し、単一 fixture 内で許可前→allow→許可後の順を定義しました。[v2:156](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:156)（再要求では再表示しない仕様：[Apple](https://developer.apple.com/documentation/usernotifications/asking-permission-to-use-notifications.md)）
- C-4：解決 — `.done` 検出後もドライバー終了まで Player を維持し、コールバックの呼出順に依存しなくなりました。[v2:142](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:142)

**C2-1［A2］プロトコルの配置先が Editor 専用アセンブリです。**
[D-8](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:56) の `Tests/Runtime/IosUiDriverProtocol.cs` は、[asmdef:8](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef:8) により Editor 限定です。また、[PlayMode 側の参照:4](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/NativeToolkit.Runtime.PlayModeTests.asmdef:4) にも含まれません。この配置の関数を iOS の `IosUiDriver` から呼ぶとコンパイルできません。Player と EditMode の双方から参照できる配置・アセンブリ構成が必要です。

**C2-2［A2］Clipboard のガードが最初の名前付き対象への切り替えも拒否します。**
[v2:192](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:192) は、実機で対象が汎用なら `Press` 前に無条件で失敗させます。初期値は [General:101](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:101) で、名前付きへの変更は [ボタンのハンドラー:448](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Clipboard/IosClipboardManagerExampleController.cs:448) の中です。したがって指定された最初の切り替え操作で失敗します。名前付き対象への切り替えボタンをガードの例外にする必要があります。

**C2-3［A2］package-user の Notification 専用回が UI 件数ゼロで失敗します。**
新設した [第2回:158](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:158) は Notification サンプルだけを選びます。一方、[ゲート:178](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:178) は UI テストを1件以上要求し、直後の Profile 例外はサンプルを件数から除きます。`--as-package-user` が選ぶ Profile は [サンプルを含まないシーン構成:21](/Users/jonghyunkim/Desktop/unity-native-plugin/Assets/Settings/Build%20Profiles/Test%20iOS%20Profile.asset:21) なので、この回は全件 Ignore→集計対象ゼロになります。§12 の skip 許容だけでは解消しません。Profile では第2回を実行対象から外すなどの扱いが必要です。

**C2-4［B］再接続後の「押し直さない」契約を検証する手順がありません。**
新設した [再送契約:80](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:80) に対し、[V-4:229](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-driver-design-v2.md:229) は画面操作後の往復確認だけです。接続が維持された実行では、応答キャッシュと同番号再送が検証されません。例えば [共有シートを閉じる処理:23](/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/SystemUI.swift:23) は、閉じた後の再実行では同じ成功を返せません。「操作完了後・返信前」に接続を切り、同じ番号の再送で操作回数1回・元の応答の再返却を確認する検証が必要です。

A1 基準では設計レビューを停止可（A1 = 0）。

## 3. 判断（こちら）

**2 人とも A1 が 0 になったので、レビューはここで止める**（`agent-rules/workflows/review-document/workflow.md` の止める基準）。止めた後の直しは v3（レビューしない）に入れた。

| 指摘 | 区分 | 判断 | v3 の場所 |
|---|---|---|---|
| S2-1・C2-1 行の組み立ての置き場所 | A2 | 実装で直した: `Tests/PlayMode/IosUiDriverProtocol.cs` にガード無しで置き、テストは同じアセンブリの `#if UNITY_EDITOR` のファイル（Editor の PlayMode の回で流す）。EditMode のアセンブリの参照は足さない（asmdef を変えない） | D-8、12 章 |
| S2-2・C2-2 Clipboard の見張りが切り替えのボタンも止める | A2 | 実装で直す: 名前で断るボタン（`UseGeneralButton`、`ErrRemoveGeneralButton`）と、汎用の間に通すボタン（切り替えの 3 つ）の 2 段にする | 7 章、12 章 |
| S2-3・C2-3 Profile の回で件数が 0 | A2 | 実装で直す: サンプルを使わない `IosUiDriver` のテスト（つないで 1 往復）を 1 本置く。Profile では 4.5 の 2 回目を回さない | 4.5、6 章、12 章 |
| S2-4 実機でのログインの入力 | B | 受け入れる。実機では、秘密の入力欄を含むアラートへの `alert-type` を手伝い役が断る。シミュレーターでは引数で欄の種類（普通・秘密）を指せるようにする | 3.2、7 章 |
| S2-5 `.done` が来ない回 | B | 受け入れる。`.done` 無しで終わったら手伝い役を止めてから次へ進み、手伝い役の判定を失敗にする | 4.1 |
| C2-4 押し直さないことの確かめ | B | 受け入れる。V-11 を足した（処理の後・答えの前に接続を切り、同じ番号の出し直しで、押す回数が 1 回で元の答えが返ること）。手伝い役の中に試験用の切り方を持つ | V-11 |
| C-1 の照らし方 | — | 12 章のまま（実装で決める） | 12 章 |
