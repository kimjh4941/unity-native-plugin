# Android Dialog UI 自動テスト計画 v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Android Dialog のサンプル（`Runtime/UI/Android/Dialog/AndroidDialogManagerExampleController.cs`）と `AndroidDialogManager` |
| 同梱 AAR | C ABI 2.0.0（`Plugins/Android/VERSION.txt`: dist 1.13.0 の `android-native-toolkit-2.0.0.aar` と `android-native-toolkit-capi-2.0.0.aar`）。移行の間は 1.x（dist 1.12.0）も追加のピンで置く（親課題の「スパイク」） |
| 目的 | Android の C ABI 2.0.0 への移行（`artifact/topics/android-c-abi-2/`）で、公開 API の振る舞いが設計どおりであることを、実機の画面を操作して確かめる |
| 実行 | `scripts/verify_unity_android.sh` の実機の PlayMode（層 2b）。テストは `Tests/PlayMode/AndroidDialogPlayerTests.cs` |
| 設計 | `artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`（7.3 が層 2b。この計画は 7.3 の中身に番号を振り、期待値を具体にしたもの） |
| 画面の操作 | ホストの adb の補助（`artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v5.md`）。命令の名前（`tap`、`back` など）はその 3.1 |
| 移行前の基準 | 無い。Android の Dialog の UI テストは 1.x には無かった。期待値は、1.x のブリッジの値（Dialog 設計 v4 の 2 章の表）と、既知の差分（Dialog 設計 v4 5.3）から決めた |

Windows の UI テスト計画（`artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md`）と同じく、サンプル画面を通すテスト（1 章）と、Manager を直接呼ぶテスト（2 章）に分ける。

**移行の前に、1.x のまま流して記録する**（Dialog 設計 v4 5.5 の手順 3。Windows の移行と同じ順）。各表の「1.x」の列は、1.x で流すかと、予想される値。「同じ」は 2.0.0 の期待と同じ。実測が予想と違えば、その結果をこの列に書き、Dialog 設計の 2 章と 5.3 を直す。移行の後は、2.0.0 の結果が「1.x と同じ」か「Dialog 設計 v4 5.3 の既知の差分」のどちらかになることを確かめる。

## 1. サンプル画面を通すテスト

サンプルのトップメニューを読み込み、Dialog の画面を開いてからサンプルのボタンを押す（Windows の `WindowsDialogSamplePlayerTests` の `LoadAtTopMenu` と同じ形）。サンプルのボタンは C# から押す（Unity の UI は OS の部品の木に出ないので、補助では押せない）。ダイアログは補助で押す。

サンプルは固定の値で呼ぶ（Dialog 設計 v4 の 2 章。どのボタンも `cancelable = false`、`cancelableOnTouchOutside = false`）。結果は画面の `ResultTextBlock` で照らす。パスワードは画面に長さだけを出し、入力と利用者名はそのまま出す（Dialog 設計 v4 5.1。`(<n> characters)`）。

| # | 操作 | 期待（移行後。`ResultTextBlock`） | 1.x |
|---|---|---|---|
| AD-01 | ShowDialog → 補助が `tap resource-id=android:id/button1` | `OK\nShowDialog buttonText: OK, isSuccess: True, errorMessage: null` | 流す。同じ |
| AD-02 | ShowConfirmDialog → 肯定（`button1`） | `OK\nShowConfirmDialog buttonText: Yes, isSuccess: True, errorMessage: null` | 流す。同じ |
| AD-03 | ShowConfirmDialog → 否定（`button2`） | `OK\nShowConfirmDialog buttonText: No, isSuccess: True, errorMessage: null` | 流す。同じ |
| AD-04 | ShowSingleChoiceItemDialog → `tap text=Option 2` → 補助が `expect text=Option 2 checked true` → 肯定 | `OK\nShowSingleChoiceItemDialog buttonText: OK, checkedItem: 1, isSuccess: True, errorMessage: null` | 流す。同じ |
| AD-05 | ShowMultiChoiceItemDialog（初期値 `[F, T, F, T]`）→ `tap text=Item 1` → 肯定 | `OK\nShowMultiChoiceItemDialog buttonText: OK, checkedItems: [True,True,False,True], isSuccess: True, errorMessage: null` | 流す。同じ |
| AD-06 | ShowTextInputDialog → 肯定が `enabled false`（空を許さない設定）→ `type class=EditText abc123` → 肯定が `enabled true` → 肯定 | `OK\nShowTextInputDialog buttonText: OK, inputText: abc123, isSuccess: True, errorMessage: null` | 流す。同じ |
| AD-07 | ShowLoginDialog → 肯定が `enabled false` → 利用者名に `user01` → まだ `enabled false` → パスワードに `pass02` → 肯定（`Login`） | `OK\nShowLoginDialog buttonText: Login, username: user01, password: (6 characters), isSuccess: True, errorMessage: null` | 流す。表示はパスワードそのもの（`password: pass02`）。長さにする変更は移行のコミット（Dialog 設計 v4 5.5 の手順 6）で、期待値が変わるのはここだけ |
| AD-08 | ShowConfirmDialog → `back` → `count resource-id=android:id/alertTitle` が 1（閉じない）→ `tap-outside` → まだ 1 → 肯定 | AD-02 と同じ。イベントは肯定の 1 回だけ | 流す。同じ |
| AD-09 | Home を押す | トップメニューに戻る。もう一度 Dialog の画面を開いて AD-01 が通る | 流す。同じ |

- 題名と本文も、AD-01〜AD-07 のそれぞれで `expect resource-id=android:id/alertTitle text <題名>` と `android:id/message` で照らす（サンプルの固定の値。ASCII）
- AD-08 はサンプルが `cancelable = false` なので、閉じないことの確かめに使う。閉じることの確かめは 2 章（Manager を直接、`cancelable = true` で呼ぶ）

## 2. Manager を直接呼ぶテスト

サンプルでは出せない値（閉じられる設定、非 ASCII、範囲外の値）と、失敗の経路を確かめる。Manager の公開 API を直接呼ぶ。

### 2.1 閉じたとき・否定・外側のタップ

| # | 呼び方 | 操作 | 期待（移行後。イベントの引数） | 1.x |
|---|---|---|---|---|
| AD-10 | `ShowDialog`（既定の `cancelable = true`） | `back` | `("Cancel", true, "")` | 流す。同じ |
| AD-11 | `ShowConfirmDialog`（既定） | `tap-outside resource-id=android:id/alertTitle` | `("Cancel", true, "")` | 流す。同じ |
| AD-12 | `ShowSingleChoiceItemDialog`（既定。`checkedItem = 0`） | 2 番目を押してから `back` | `("Cancel", null, true, "")`（閉じたときの選択は返らない。設計 5.3） | 流す。`("Cancel", 1, true, "")`（今の選択が返る。既知の差分） |
| AD-13 | `ShowSingleChoiceItemDialog` | 否定 | `("Cancel", null, true, null)`（否定のラベルは既定の `"Cancel"`） | 流す。同じ |
| AD-14 | `ShowMultiChoiceItemDialog`（既定） | `back` | `("Cancel", null, true, "")`（閉じたときの状態は返らない。設計 5.3） | 流す。`("Cancel", [true, false, …], true, "")`（今の状態が返る。既知の差分） |
| AD-15 | `ShowMultiChoiceItemDialog` | 否定 | `("Cancel", null, true, null)` | 流す。同じ |
| AD-16 | `ShowTextInputDialog`（既定） | `back` | `("Cancel", "", true, "")` | 流す。同じ |
| AD-17 | `ShowTextInputDialog` | 否定 | `("Cancel", null, true, null)`（`null`。`""` ではない） | 流す。同じ |
| AD-18 | `ShowLoginDialog`（既定） | `back` | `("Cancel", "", "", true, "")` | 流す。同じ |
| AD-19 | `ShowLoginDialog` | 否定 | `("Cancel", null, null, true, null)` | 流す。同じ |
| AD-20 | `ShowTextInputDialog`（`enablePositiveButtonWhenEmpty = true`） | 何も入れずに肯定 | `("OK", "", true, null)`（空の入力は `""`） | 流す。同じ |

`back` は、入力欄にキーボードが出ていれば補助が先に閉じる（補助の設計 v4 3.1）。

### 2.2 値の写し

| # | 呼び方 | 操作 | 期待（移行後） | 1.x |
|---|---|---|---|---|
| AD-21 | `ShowMultiChoiceItemDialog`（項目 4 つ、`checkedItems` が 2 つ `[true, false]`） | そのまま肯定 | 返る配列の長さが 4 で `[true, false, false, false]`（短い配列は `false` で埋める。設計 J-D4） | **流さない**。短い配列は main で例外になりアプリが落ちると推定（Dialog 設計 v4 の 2 章）。落ちると後のテストが止まる |
| AD-22 | `ShowMultiChoiceItemDialog`（項目 2 つ、`checkedItems` が 4 つ） | そのまま肯定 | 長さ 2（余りは捨てる） | 流す。**実測（2026-10-11）: 長さ 4 のまま `[true, false, true, false]` が返る**（推定どおり） |
| AD-23 | `ShowSingleChoiceItemDialog`（`checkedItem = -1`） | そのまま肯定 | `checkedItem` が `null` | 流す。**実測（2026-10-11）: 何も選ばれずに出て、肯定で `checkedItem` は `null`** |
| AD-24 | 題名・本文・肯定のラベル・項目に `ダイアログ-𠮷`（日本語とサロゲートペア）を入れた `ShowConfirmDialog` と `ShowSingleChoiceItemDialog` | 補助が `expect` で題名・本文・ボタン・項目の文字を照らしてから肯定 | 画面の文字が一致し、`buttonText` が `ダイアログ-𠮷` と等しい | 流す。同じ |

### 2.3 呼び出しの時点と完了の経路

| # | 呼び方 | 期待（移行後） | 1.x |
|---|---|---|---|
| AD-25 | UnityMain から `ShowConfirmDialog` | `Show*` が戻った時点でイベントが 0 回。補助が肯定を押した後に 1 回、UnityMain で（設計 J-D14） | 流す。同じ（旧も dispatcher を通るので戻る前には出ない） |
| AD-26 | Manager の受け口と `Kind.Confirm` で `ntk_dialog_show_confirm_async` を要求の ID つきで直接呼び、`ntk_dialog_cancel` | `ConfirmDialogResult` に `(null, false, "Failed to showConfirmDialog: the dialog was canceled.")` が 1 回。UI の操作は無い | 流さない（C ABI にしか無い） |
| AD-27 | 後ろに回したまま呼ぶ（補助の `background 3000` と別のスレッド。Dialog 設計 v4 7.3 の E-8） | `DialogResult` に `(null, false, "Failed to showDialog: the app is not in the foreground.")` が 1 回。**試す**: 確実に起こせなければ外し、理由を実装結果ファイルに書く | 流さない（C ABI の Bridge を直接呼ぶテスト。旧は後ろからの呼び出しが別の経路で失敗する） |
| AD-28 | `ShowConfirmDialog` → テストが `Screen.orientation` で向きを変える（今が横なら縦、縦なら `LandscapeLeft`）→ 補助の `rotation` で表示が回ったことを確かめる | ダイアログが残り（`count` が 1）、肯定で `ConfirmDialogResult` が 1 回。終わりに向きを戻し、表示が戻るまで待つ。**初めは補助の `rotate`（`user_rotation`）で回す計画だったが、Unity の自動回転はセンサーに従い、机に置いた端末では回らなかった（2026-10-11）ので、アプリから回す形にした** | 流す。同じ |

### 2.4 同期の失敗（UI 不要）

イベントは `Show*` の中で 1 回、`isSuccessful == false`。ダイアログは出ない（補助の `count resource-id=android:id/alertTitle` が 0）。ログは `LogWarning`。

| # | 呼び方 | 期待する `errorMessage`（移行後） | 1.x |
|---|---|---|---|
| AD-29 | `ShowDialog("", "message")` | `"Title cannot be null or empty."` | 流す。同じ。ログは `LogError`（既知の差分。`LogAssert.Expect` を付ける） |
| AD-30 | `ShowConfirmDialog("title", "")` | `"Message cannot be null or empty."` | 同上 |
| AD-31 | `ShowSingleChoiceItemDialog("title", new string[0])` | `"singleChoiceItems cannot be null or empty."` | 同上 |
| AD-32 | `ShowSingleChoiceItemDialog("title", { "a", null })` | `"singleChoiceItems cannot contain null."`（設計 J-D12） | **流さない**。`null` の要素はそのまま Java に渡り、落ちうる |
| AD-33 | `ShowSingleChoiceItemDialog("title", { "a", "b" }, checkedItem: 2)` | `"checkedItem is out of range."`（設計 J-D3） | 流す。**実測（2026-10-11）: ダイアログは出て（何も選ばれていない）、肯定で `checkedItem` は `2`（範囲外の値がそのまま返る）**。移行後は同期の失敗（既知の差分） |
| AD-34 | `ShowMultiChoiceItemDialog("title", { "a", null })` | `"multiChoiceItems cannot contain null."` | **流さない**。AD-32 と同じ |

### 2.5 32 ビット（対象外）

32 ビット（ARMv7）と Mono は対象外で、32 ビットを含むビルドは Gradle の前に止まる（親課題の J-5、J-10）。実機の 32 ビットのテストは持たない（初めは AD-35 として置いていたが、ビルドの段で止まると分かり外した。AD-35 は欠番）。ビルドを止める確認は EditMode の `AndroidArchitectureCheckTests` で固める。

## 3. テストの共通の約束

- UI の補助を使うテスト（AD-01〜AD-28）には NUnit のカテゴリ `AndroidUiDriver` を付ける。ゲートは、64 ビットの実行でこのカテゴリの skip が 0 であることを見る（補助の設計書 v4 の 7 章）
- 後片付け（Dialog 設計 v4 7.3）: ダイアログが残っていれば `back` で閉じ、受け付けた要求の数と出たイベントの数が等しくなるまで待ってから、取り出し口の列を空にする。カテゴリの無いテストでは補助を呼ばない
- 入力の文字は英小文字と数字だけ（補助の `type` の制限）。非 ASCII の入力は確かめない（2.2 の AD-24 で表示は確かめる）
- 待ち時間: 補助の結果は 20 秒、イベントは 10 秒

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| 見た目（ボタンの色、配置、テーマ） | 期待値を決められない。native-toolkit のテストの範囲 |
| `HOST_START_FAILED` / `SHOW_FAILED` / `CANCELED_BY_SYSTEM` / 未初期化 | 実機で起こせない。文言は層 1 で固める（設計 6 章の E-6、E-9） |
| 非 ASCII の入力 | `input text` が運べない。入力の UTF-8 は層 1 と、表示の AD-24 で押さえる |
| 前面が `FragmentActivity` でないとき（透明な宿主） | Unity の GameActivity は `FragmentActivity` なので起きない |

## 5. 実行結果

（実装の後に書く）

| 日付 | 版（移行前 / 移行後） | バックエンド | Player | Dialog |
|---|---|---|---|---|
| 2026-10-11 | 移行前（1.x、Android™ Profile）1 回目 | IL2CPP | 23/42 | AD-01〜07 は合格。AD-07 の後、Google の自動入力の「パスワードを保存しますか」が操作の対象の窓を取り、`uiautomator dump` がダイアログを読めず AD-08 以降が落ちた。補助は自動入力を切るようにした（補助の設計 v5） |
| 2026-10-11 | 移行前（1.x）2 回目 | IL2CPP | 20/42 | 外側のタップの基準の誤り（ダイアログの窓を画面と取り違えた）、後片付けの失敗で購読が積み重なった、閉じられないダイアログが残って連鎖した、自動回転が回らない。テストと補助を直した |
| 2026-10-11 | 移行前（1.x）3 回目 | IL2CPP | 41/42 | AD-28 だけ落ちた（`user_rotation` では回らない）。アプリから回す形にした |
| 2026-10-11 | **移行前（1.x）4 回目** | IL2CPP | **42/42** | **1.x で流す 29 件すべて合格**（AD-01〜20、22〜25、28〜31、33）。補助は依頼 101 件、終了コード 0。パッケージの利用者の条件（Test Android™ Profile）は 33 件合格・9 件 skip（サンプルのシーンが無く、AD-01〜09 が Ignore。想定どおり） |
| 2026-10-11 | **移行後（C ABI 2.0.0、Android™ Profile）1 回目** | IL2CPP | **47/47** | **AD-01〜AD-34 の 34 件すべて合格**。1.x の 29 件のうち期待値を変えたのは AD-07、12、14、22、23（記録から期待値へ）、33 と、AD-29〜31 の `LogAssert` の削除で、どれも Dialog 設計 v4 5.3 の既知の差分。2.0.0 だけの AD-21、26、27、32、34 も合格（AD-27 は層 1 に下げずに起こせた）。補助は依頼 113 件、終了コード 0。Test Android™ Profile は 38 件合格・9 件 skip（AD-01〜09。想定どおり）。詳細は `artifact/android/dialog/results/2026-10-11-android-dialog-implementation-feature-result-v1.md` |
| 2026-10-11 | 移行後、実装レビュー v1 の修正の後（`verify-fix1`） | IL2CPP | **47/47** | 後片付けで要求とイベントの数を照らす形にした後。イベントを 2 回出すように壊すと AD-10 と AD-13 が後片付けで落ちることも確かめた（実装結果 v2 の 9.2） |
| 2026-10-11 | 移行後、実装レビュー v2 の修正の後（`verify-fix2`） | IL2CPP | **47/47** | 後片付けの手順が失敗しても片付けを済ませる形（`Safely`）にした後。補助の依頼 114 件、終了コード 0。Test Android™ Profile は 38 件合格・9 件 skip。詳細は実装結果 v3（`artifact/android/dialog/results/2026-10-11-android-dialog-implementation-feature-result-v3.md`）の 10 章 |
