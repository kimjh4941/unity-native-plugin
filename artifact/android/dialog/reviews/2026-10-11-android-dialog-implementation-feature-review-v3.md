# 実装レビュー v3（ラウンド 3）: Android Dialog の C ABI 2.0.0 への移行

- 対象: ブランチ `feature/UNT-13`、基点 `d9714e4` の上の未コミットの差分（`git diff` と `git status --short` の未追跡ファイル。全文を読んだ）
- 実装計画: `artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`、UI テスト計画: `artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v1.md`
- 実装結果: `artifact/android/dialog/results/2026-10-11-android-dialog-implementation-feature-result-v3.md`
- 前回までのレビュー: `artifact/android/dialog/reviews/2026-10-11-android-dialog-implementation-feature-review-v1.md`、`-v2.md`
- 一次資料として確かめたもの: native-toolkit の `Dialog.h`、`Common.h`、`src/Dialog/Dialog.cpp`（ログに入力の値が出ないこと）、Unity Test Framework（`com.unity.test-framework@76560ee600cb`）の `EnumeratorHelper.cs`、`BeforeAfterTestCommandBase.cs`、`TestResultExtensions.cs`、`CoroutineRunner.cs`、`ProjectSettings/EditorSettings.asset`、`ProjectSettings/ProjectSettings.asset`
- レビュアー: サブエージェント（Claude。ラウンド 1・2 とは別のインスタンス）と Codex を並行で回した。どちらも読むだけで、ファイルの変更と Unity・adb・実機の実行はしていない。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない）

## 1. サブエージェント（Claude）

### レビュー概要

- 目的: Android Dialog を 1.x の Kotlin ブリッジ（`AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher`）から、native-toolkit の C ABI 2.0.0（`libntk.so`、`ntk_dialog_*`）の P/Invoke に移す。公開 API（メソッド、イベント、既定値）は変えない。
- 変更の構成: Bridge（`AndroidDialogCApi`）、共通部（`AndroidNativeToolkitCApi.ReadUtf8OrNull`）、取り出し口（`AndroidNativeCallbackQueue`）、Manager の書き直し、層 1（62 本）、層 2a（13 本）、層 2b（34 本。`Safely` を使う後片付け、AD-27/28 の状態をフィールドに持つ形）、`verify_unity_android.sh` に Editor の PlayMode を追加、ルール 3 ファイル。
- ラウンド 2 の修正の確認:
  - `OnEnable` / `OnDisable` の旗: 正しい。
  - `Post` / `OnDisable` の順序（Dekker 型）: 推論は成り立つ。`Post` 側のフェンスは `ConcurrentQueue.Enqueue` の中の Interlocked 操作に頼っている（R3-6）。
  - 重複の判定、`Application.quitting` の購読と解除、`EnsureCreated` による有効化: 正しい。
  - `Safely`: Unity Test Framework（以下 UTF）の `EnumeratorHelper.ProgressOnEnumerator` と同じ平らにし方をしている。`WaitForSecondsRealtime` は `CustomYieldInstruction` で `IEnumerator` なので、`Safely` 自身が `MoveNext` で回す。`AsyncOperation` と `null` は上に渡し、`CoroutineRunner` 経由で `StartCoroutine` が処理する。`ExceptionDispatchInfo` で投げ直しても例外の型は変わらない。`RecordPrefixedException` は `ResultStateException` の `ResultState` をそのまま使うので、`Assert.Ignore` は Ignore のまま残る（ただし R3-4）。
  - AD-27 のスレッドの止め方、AD-28 の向きの戻し、`Request` を呼び出しの後に数えること: 意図どおり動く。
- 新しい振る舞いの欠陥（A）は見つからなかった。残りは検証の穴（B）と記述（C）。

### 重大な問題（high）

なし

### 改善提案（medium）

**R3-1** C2-1 と R1-5 の修正の一部にテストが無い。2 つの変異が全テストを通り抜ける
- 場所:
  - `Runtime/Android/Common/AndroidNativeCallbackQueue.cs:120-123`（`OnDisable` が列を見る側）
  - `Runtime/Android/Dialog/AndroidDialogManager.cs:412`（`ShowNative` の `EnsureCreated()`）
- 観点: テストカバレッジ（壊すと落ちるか）
- 区分: B（範囲。A の修正の確かめの抜け）
- 内容:
  1. `OnDisable` の `if (!s_actions.IsEmpty) ReportStranded(...)` を消しても、どのテストも落ちない。層 2a の 2 本は、取り出し口を壊す・無効にするのを `Post` の前に行う（`AndroidDialogManagerEditorTests.cs:200`、`:225`）。そのため列はいつも空で、`Post` 側だけを確かめている。結果 v3 10.2 は「C2-1 の競合そのもの（スレッドの交わりを起こせない）」を未確認に挙げている。しかし `OnDisable` 側の分岐は、同じスレッドだけで決まった順に起こせる。
  2. `ShowNative` の `AndroidNativeCallbackQueue.EnsureCreated()` を消しても、どのテストも落ちない。Editor は `ShowNative` に届かない（`OnDevice` で戻る）。実機のテストは取り出し口を壊さない。Editor のテストは `EnsureCreated()` を直に呼び（`:233`）、コメントで「(on a device, so does every Show*)」と書いているだけ。
- 失敗の筋書き: 後の変更で `OnDisable` の確認か `ShowNative` の再作成が消える。すると、取り出し口が無効にされた・壊された後の完了が `LogError` なしに残るか、次の `Show*` でも戻らない。R2-3 / C2-1 / R1-5 で A として直した振る舞いに、回帰を検出する手段が無い。
- 提案:
  - 層 2a: `AndroidDialogManager.PostAnswer(...)` の直後、同じフレームのうちに `pump.enabled = false` とし、`LogAssert.Expect(LogType.Error, Stranded)` を 1 回期待する。`EnsureCreated()` の後に 1 回届くことを見る。
  - 層 2b（1 本）: 取り出し口の GameObject を `DestroyImmediate` してから `Manager.ShowConfirmDialog` を呼び、補助で肯定を押し、`Confirm|Yes|True|null` が 1 回届き `LogError` が出ないことを見る。足したら、それぞれ上の変異で落ちることを確かめる。

### 軽微な指摘（low）

**R3-2** `s_quitting` を戻す処理が無い。Editor で「ドメインを再読み込みしない」設定だと、2 回目以降の Play で取り出し口の `LogError` が出なくなる
- 場所: `AndroidNativeCallbackQueue.cs:45`、`:93`、`:134`
- 観点: 正確性（Editor の Play のライフサイクル）、テストの独立性
- 区分: B（範囲）
- 内容: `OnQuitting` は `s_quitting = true` にするだけで、戻す処理がどこにも無い。Editor では Play を止めると `Application.quitting` が呼ばれる（Unity のスクリプトリファレンスの記述。この版での発火の時点は要確認）。Enter Play Mode Options で「Reload Domain」を切ると static が残るので、それ以後の Play では `ReportStranded` が黙って戻る。同じ条件で `s_actions` も前の Play から持ち越す（`ResetForTests` を呼ぶテストは影響を受けない）。
- 失敗の筋書き: ドメインの再読み込みを切ったプロジェクトで、一度 Play を止めた後に PlayMode のテストを流す。`Completion_WhenTheQueuesObjectWasDestroyed_*` と `Completion_WhenTheQueuesObjectWasDisabled_*` が「Expected log message not received」で落ちる。
- このリポジトリへの影響: `EditorSettings.asset:27-28` は `m_EnterPlayModeOptions: 0`（再読み込みする）なので、ここでは起きない。製品への影響も無い。Editor にはネイティブの完了が来ず、Player は終了とともにプロセスが終わる。
- 提案: 取り出し口の `Awake` で `s_quitting = false` にするか、`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` で static を初期化する。

**R3-3** 後片付けの `SettleRotation` は期限が来ると黙って終わる。「向きが戻ったこと」を確かめていない
- 場所: `Tests/PlayMode/AndroidDialogPlayerTests.cs:134-142`、`:82-95`
- 観点: テストの独立性
- 区分: B（範囲）
- 内容: 後片付けは `Screen.orientation` を元の値に戻す。`ProjectSettings.asset:11` の `defaultScreenOrientation: 4` から、元の値は AutoRotation と推定できる。AutoRotation はセンサーに従うので、自動回転がオンの端末を机に置いたままだと、表示が元の回転に戻る保証は無い（要確認。AOSP はセンサーが向きを出さないとき直前の回転を保つ、と理解している）。そのとき `SettleRotation` は 10 秒待って何も記録せずに終わる。
- 失敗の筋書き: AD-28 の後のテストが、横向きのまま何の失敗も出さずに走る。今は AD-28 が名前順で最後の UI テストに近いので表に出ないが、テストを足すと順が変わる。
- 提案: 期限切れのときは `Assert.Fail("the display did not return to rotation ...")` を投げる（`Safely` が `errors` に貯める）。

**R3-4** 後片付けの Ignore が本体の失敗を上書きする。失敗が 1 つだけ報告され、後片付けの照合結果が消える
- 場所: `AndroidDialogPlayerTests.cs:113-114`。UTF `TestResultExtensions.cs:38-43`
- 観点: テストの信頼性
- 区分: B（範囲）
- 内容:
  - (a) `Ui.Do` は補助が答えないと `Assert.Ignore` を投げる（`AndroidUiDriver.cs:72`）。本体に UI の手順が無い `[Test]` の AD-29〜31 が本体で失敗し、補助が答えないと、後片付けの `CloseLeftoverDialog` の `IgnoreException` が `ExceptionDispatchInfo` で投げ直される。`RecordPrefixedException` は `ResultStateException` の `ResultState` で結果を上書きするので、失敗が Ignored になる。既定の実行は `AndroidUiDriver` の skip 0 を見る（`verify_unity_android.sh:200-206`）ので捕まる。しかし `--as-package-user` の実行は `check_ui=0` なので、補助の終了コード以外では捕まらない。
  - (b) `errors` があると `mismatch`（要求とイベントの数の違い）は報告されない（`:113` で先に投げる）。
- 提案: `errors` と `mismatch` を 1 つの `Assert.Fail` にまとめる。Ignore は、後片付けの中では失敗の記録に変えて扱う（または `Ui` の準備ができていないときは後片付けの UI の手順を飛ばす）。

**R3-5** `csharp.md` の「先頭の `Debug.Log`」から外れている所
- 場所:
  - `AndroidNativeCallbackQueue.cs:67-76`（`EnsureCreated` の速い経路。`internal` で、`Show*` のたびに呼ばれる）
  - `:109-112`、`:116-119`（`OnEnable` / `OnDisable` は判定がログより先）
- 観点: ルール（`csharp.md`「対象: public / internal メソッド、MonoBehaviour イベント関数」）
- 区分: C
- 内容: 結果 v3 1.2 とルールの除外にあるのは、`Update` の空のフレームと純粋な変換の 2 つだけ。`EnsureCreated` は R1-5 と R2-3 で要になった関数だが、正常な経路ではログが 1 行も出ない。
- 提案: 先頭に `Debug.Log` を置くか、除外の理由を `csharp.md` に書く。

**R3-6** `Post` 側の順序の保証は `ConcurrentQueue` の中の実装に頼っている
- 場所: `AndroidNativeCallbackQueue.cs:56-59`
- 観点: Bridge の品質（スレッド安全性）
- 区分: C（コメント）。潜在的な B
- 内容: `OnDisable` は「旗の書き込み → `MemoryBarrier` → `IsEmpty`」で、こちら側は明示している。`Post` は「`Enqueue` → volatile の読み」だけで、書き込みと次の読みの間のフェンスは `Enqueue` の中の Interlocked（referencesource は `Interlocked.Increment(ref m_high)`、corefx は tail の CAS）に頼っている。どちらの実装でも Dekker の形は成り立つので、コメントの「どちらの順で交わっても、少なくとも片方が見つける」は正しい。ただし Unity（Mono / IL2CPP）の BCL がどちらの実装かは、このレビューでは確かめていない（要確認）。
- 提案: `s_actions.Enqueue(action);` の後に `Interlocked.MemoryBarrier();` を置いて対称にする（コストはほぼ無い）。

**R3-7** 結果 v3 の記述の抜け
- 場所: 結果 v3 2.2（76-85 行）、10.2（325 行）
- 区分: C
- 内容:
  - 2.2 の既存変更に、`Tests/PlayMode/AndroidUiDriver.cs`（`WaitForAnswerFile` の `CancellationToken`）と `artifact/topics/android-c-abi-2/README.md`（6.6）が無い。前者は 10.1 の R2-2 の行にだけある。
  - 10.2 の「C2-1 の競合そのもの（スレッドの交わりを起こせない）」は、競合については正しい。しかし `OnDisable` 側の報告はスレッドなしで確かめられる（R3-1）。

**R3-8** 後片付けの、`Safely` の外の先頭の手順が投げると、それ以後の片付けがすべて飛ぶ
- 場所: `AndroidDialogPlayerTests.cs:81-86`、`:145-154`、`:522-549`
- 区分: B（範囲。起きる見込みは低い）
- 内容: `_background` は `Start()` の前に代入される（`:522`、`:549`）。`Start()` が投げた場合（スレッドを作れない等）、後片付けの `_background.Join()` が起動していないスレッドに対して `ThreadStateException` を投げる。購読の解除、列の空け、シーンを下ろすことがすべて飛び、次のテストに持ち越す。
- 提案: `Start()` が成功してからフィールドに入れる。または先頭の 2 手順を `try/catch` で `errors` に入れる。

**R3-9** 別のスレッドから `Show*` を呼ぶと、イベントではなく例外が呼び出し側に出る（要確認）
- 場所: `AndroidDialogManager.cs:412`（`try` の外）、`AndroidNativeCallbackQueue.cs:71`（`isActiveAndEnabled`）
- 観点: 既存 API の互換
- 区分: C（記述）。1.x の実際の振る舞いによっては A
- 内容: `isActiveAndEnabled` は UnityMain 以外から呼ぶと `UnityException` を投げる。`EnsureCreated()` は `ShowNative` の `try` の外にあるので、検査を通った呼び出しを別のスレッドから行うと、その例外が利用者のコードに出る。J-D14 は「公開 API は UnityMain から呼ぶ（今も同じ前提）」とし、XML コメントにも書いてある。ただし 1.x が実際に別のスレッドから動いたか（`AndroidJavaObject` の自動アタッチ）は確かめられておらず、5.3 の既知の差分の表にも無い。
- 提案: 1.x で別のスレッドから動いていたなら、5.3 と移行ガイドに「別のスレッドからは例外」と書く。

**R3-10** `EnsureCreated` で戻せない、ふつうでない条件（記録だけでよい）
- 場所: `AndroidNativeCallbackQueue.cs:69-76`、`:141`
- 区分: B（範囲）。`LogError` が出るので無言ではない
- 内容:
  - (a) 同じフレームで `Destroy(pump.gameObject)`（遅延の破棄）の後に `Show*` を呼ぶと、`s_pump != null` かつ `isActiveAndEnabled` なので何もせずに戻る。フレームの終わりに取り出し口が壊れ、完了は次の `Show*` まで残る（`LogError` は出る）。
  - (b) 利用者が取り出し口を、無効な親の下に付け替えると、`SetActive(true)` では有効にならず、呼び出しのたびに警告が出たまま戻らない。
  - (c) 重複した取り出し口の `Update` は `s_pump == this` を確かめないので、破棄される前のフレームに列を回しうる。動作の上の害は無い。
- 提案: 直さないなら、結果ファイルの残件に理由つきで書く（止める基準の 3）。

### 実装計画整合性チェック

| 項目 | 判定 | 根拠 |
|---|---|---|
| Manager + Bridge パターン準拠 | ○ | Bridge（`AndroidDialogCApi`）は構造体・`extern`・純粋な変換だけ。受け口・イベント・検査は Manager。共通部と取り出し口は `Runtime/Android/Common/`。依存は Manager → Bridge → 共通部の一方向 |
| 変更ファイル一覧との一致 | △ | 設計 5.1 と一致する。結果 v3 2.2 に 2 ファイルが無い（R3-7） |
| テスト方針の網羅性 | △ | 7.1〜7.3 の観点はそろっている。7.3 の「失敗しても持ち越さない」は `Safely` でほぼ成り立つ（R3-3、R3-4、R3-8 は残る）。取り出し口の作り直しの一部が未検証（R3-1） |
| エラーケース全実装 | ○ | E-1〜E-13 がすべてコードにある。E-5〜E-7、E-9、E-12、E-13 は起こせないので層 1 とコードレビュー（結果 5.2 に理由がある） |
| 返却仕様との整合 | ○ | 同期の失敗は `(null, …, false, 文言)`。ボタンで閉じたときは `(label, …, true, null)`。閉じたときは `("Cancel", …, true, "")`（`common.md` に例外として書かれた）。否定の入力は `null`、閉じたときの入力は `""` |

構造体の配置: `Dialog.h` の `#pragma pack(push, 8)` と、6 つの構造体のフィールドの順・型が一致する。層 1 の大きさと位置（40/48/64/64/64/72）も 64 ビットでの計算と一致する。

**結果 v3 の主張の照合**

| 章 | 主張 | 判定 |
|---|---|---|
| 1.2 | `ReadUtf8OrNull` で NULL と空を分ける | 成り立つ（`AndroidNativeToolkitCApi.cs:142-143`。層 1 あり） |
| 1.2 | `Safely` で回し、購読の解除・列の空け・シーンを下ろすを必ず行い、最初の失敗を投げ直す | 成り立つ。UTF のソースと照らした。例外は R3-4、R3-8 |
| 1.2 | AD-27 / AD-28 はフィールドに持ち、後片付けの先頭で止める・戻す | 成り立つ。戻った後の確かめは R3-3 |
| 1.2 | 要求は呼び出しの後に数える | 成り立つ（`:565-570`。AD-26 は `:490`） |
| 5 | 層 1 は 62 本（48 + 14）、層 2a は 13 本 | 数え直して一致（`[Test]` 21 + `[TestCase]` 27 = 48、6 + 8 = 14、層 2a は 13） |
| 6 | 全 34 本で 1 回の呼び出しにイベント 1 回を照らし、失敗しても片付ける | おおむね成り立つ。R3-3、R3-4、R3-8 が残る |
| 9.1 | R1-5「`Show*` のたびに `EnsureCreated`」 | コードは成り立つ。テストが無い（R3-1） |
| 10.1 | C2-1「どちらの順でも、少なくとも片方が見つける」 | 成り立つ（R3-6 の前提つき） |
| 10.1 | R2-3「旗は `OnEnable` / `OnDisable`、`EnsureCreated` が有効に戻す」 | 成り立つ |
| 10.1 | 重複した取り出し口は旗に触らない、終了の間は出さない | 成り立つ。`s_quitting` を戻さない点は R3-2 |
| 10.2 | 変異 5 件で落ちた | 筋は合う（実行は確かめていない）。未確認の欄の C2-1 は R3-1、R3-7 |

### プロジェクトルール適合チェック

| 項目 | 判定 | 根拠 |
|---|---|---|
| common.md 準拠 | ○ | 下の P1〜P5 の表。ルールの更新（Android の C ABI の節、`Runtime/Android/Common/` の例外、閉じたときの `""` の例外）もコードと一致する |
| csharp.md 準拠 | △ | `#nullable enable`、`LogTag`、`[{LogTag}][{nameof}]`、`LogWarning` と `LogError` の分け方は守っている。先頭のログの抜けは R3-5 |
| Bridge 実装品質（スレッド安全性・メモリ管理） | ○ | 確保は `Alloc` で一覧に入れ、`finally` で解放する。結果のハンドルは `finally` で 1 回だけ解放する。受け口は static で `[MonoPInvokeCallback]` を付け、delegate は `static readonly`。ロックを持ったまま購読者を走らせない。ガードは二重。潜在の点は R3-6 |
| 既存 API 互換性 | ○ | メソッド・イベントのシグネチャと既定値は 1.x と同じ。`Initialize()` も残した。ガードが `UNITY_ANDROID \|\| UNITY_EDITOR` に広がり、Editor では全ターゲットで型が見えるようになったが、壊れるものは無い。名前空間は前のコミットで承認済み（J-D11）。別のスレッドからの呼び出しは R3-9（要確認） |

**プラットフォーム独立性**

| 観点 | 判定 | 根拠 |
|---|---|---|
| P1 | ○ | 新しいファイルと型はすべて `Android` で始まる（テスト 3 ファイルを含む） |
| P1b | ○ | テストは 1 ファイルにつき Android の型だけ。ガードは対象の型と合う（Editor のテストは `UNITY_EDITOR`、Player のテストは `UNITY_ANDROID` かつ `!UNITY_EDITOR`） |
| P1c | ○ | `Runtime/Android/{Common,Dialog}/`、名前空間は `JonghyunKim.NativeToolkit.Runtime.Android.{Common,Dialog}`。新しい `.meta` の GUID 5 つは、それぞれリポジトリ内で 1 件だけ |
| P2 | ○ | `git diff --name-only HEAD` に `Ios` / `Mac` / `Windows` を含むパスは無い（Windows の同じ形の問題は結果 7 章の残件） |
| P3 | ○ | `UnityMainThreadDispatcher` は使わなくしただけで変えていない。共通部は Android の機能だけが使う |
| P4 | ○ | `Runtime/Common/` への追加は無い |
| P5 | ○ | クラスは `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport`・受け口・`Read` は `UNITY_ANDROID && !UNITY_EDITOR` |
| 既知の逸脱の引用 | ○ | 前例として引いていない（`common.md` の逸脱の表のパスを更新しただけ） |

**セキュリティ（ログの中の秘密）**: 入力・利用者名・パスワードは、Manager でも（`AndroidDialogManager.cs:347`、`:354`）サンプルのログでも、長さだけを出す。サンプルの画面はパスワードだけ長さで出す。native 側の `NTK_LOGD` もポインタと番号だけで、文字は出さない（`Dialog.cpp:47-177`）。ヒントと題名はログに出すが、秘密ではない。

### テストカバレッジ

カバーできている観点:
- 層 1: 構造体の大きさと全フィールドの位置、64 ビットの要求 ID、`Build*`（`struct_size`、`reserved`、向きを逆にした旗、UTF-8 の読み戻し、`checked` の短い・長い・`null`・空）、検査の文言と境界（-2/-1/0/2/3）、6 種類 × 肯定・否定・閉じた・失敗の写し、全エラーの名前と文言、版の判定、例外の分類、サロゲート、NULL と空。
- 層 2a: 検査の失敗が呼び出しの中で 1 回、Editor の 6 種類の警告、購読者の例外、`Post` の中ではイベントを出さないこと、無効・壊れた Manager、取り出し口が壊された・無効にされたときの `Post` 側の `LogError` と作り直し、1 フレームで走る件数。
  - 依頼 2 の答え（取り出し口を壊す・無効にする 2 本）:
    - 正しい実装なら、名前順のどの順でも通る。直前のテストが途中で落ちて取り出し口が無効のまま残っても、次のテストの `Instance`（Manager の `Awake`）→ `EnsureCreated` で戻る。
    - 壊したとき: 旗を下ろさない、有効に戻さない、のどちらでも落ちる。
    - 後に残す状態: 取り出し口の GameObject（DDOL、有効）と、テストの外の `s_quitting`（R3-2）だけ。後のテストへの影響は無い。
- 層 2b: 34 本すべてで、後片付けが「呼び出し 1 回にイベント 1 回」を照らす。2.0.0 だけの経路（取り消し、`NOT_FOREGROUND`、null の要素）も実機で通す。

不足している観点:
- `OnDisable` 側の取り残しの報告、`ShowNative` の `EnsureCreated`（R3-1）
- `s_quitting` を戻さないこと（R3-2）
- 回転が戻ったかの確かめ（R3-3）
- 後片付けで Ignore が失敗を上書きすること、失敗の報告が 1 件に絞られること（R3-4）
- `ShowNative` の入口の失敗（`code != NONE`）と `catch` の経路。ネイティブが使えない経路。受け口の `catch` の経路。どれも結果 5.2 に「起こせない」として理由が書かれている。入口の失敗の文言と分類は層 1 で確かめたが、Manager が `fail` を呼ぶことは確かめていない（変異を入れても残る）

### 総合評価

**要修正（軽微）**

- A（振る舞いを変える）: **0 件**
- B: 範囲の 6 件（R3-1、R3-2、R3-3、R3-4、R3-8、R3-10）。R3-1 は、A として直した取り出し口の作り直しの確かめが抜けている。テスト 2 本で埋まるので、直すことを勧める
- C: 4 件（R3-5、R3-6、R3-7、R3-9。R3-9 は 1.x の実際の振る舞いによっては A になりうるので要確認）
- 止める基準との関係: A は 0 になった。止めるには次の 2 つも必要。
  - (1) レビュアーを替えて 1 回通していること: 並行の Codex の結果と合わせて判断する
  - (3) 直さない残件（R3-2〜R3-4、R3-8〜R3-10 のうち直さないもの、結果 5.2 の起こせない経路）を、実装結果ファイルの残件に理由つきで書くこと
- R3-1 のテストを足すなら、上に書いた 2 つの変異で落ちることを確かめる（止める基準の 2）。

## 2. Codex

（ID は統合のときに付けた。本文は Codex の出力のまま）

A は 0 件です。確認済みは B 5 件、C 5 件です。総合評価は「要修正」です。

**B — 検証手段の欠陥**

- **C3-1** `AndroidNativeCallbackQueue.cs:134`: `Application.quitting` で立てた `s_quitting` を次の Play session 開始時に戻していません。Unity 公式仕様では、Domain Reload 無効時は static field が Play session 間で保持されます。具体例: Domain Reload 無効で PlayMode テストを再実行すると `s_quitting == true` のままです。queue 破棄・無効化テストが期待する `LogError` は `AndroidNativeCallbackQueue.cs:93` で抑止され、正しい実装でも 2 回目のテストが失敗します。
- **C3-2** `AndroidDialogManagerEditorTests.cs:219`: 無効化テストは component の `enabled = false` だけです。具体例: `AndroidNativeCallbackQueue.cs:73` の `gameObject.SetActive(true)` を削除してもテストは通ります。しかし GameObject 自体を `SetActive(false)` にされると復旧せず、保留した完了が配送されません。
- **C3-3** `AndroidDialogManagerEditorTests.cs:233`: テストが `EnsureCreated()` を直接呼び、製品の `ShowNative → EnsureCreated` の配線を通していません。具体例: `AndroidDialogManager.cs:412` を削除してもこのテストは通ります。実機では、queue 無効化後の次の有効な `Show*` が復旧させなくなります。
- **C3-4** `AndroidDialogPlayerTests.cs:149`: `StopBackgroundCall` が Unity main thread で無期限の `Thread.Join()` を行います。CancellationToken が止められるのは回答ファイル待ちだけです。具体例: AD-27 の背景スレッドが `AndroidDialogPlayerTests.cs:536` のネイティブ入口で戻らない回帰を起こすと、teardown とテスト実行全体がハングし、テスト失敗として収束しません。
- **C3-5** `AndroidDialogPlayerTests.cs:136`: `SettleRotation` は期限切れ後に元の向きへ戻ったことを assert しません。具体例: AD-28 後に表示回転が元へ戻らなくても 10 秒後に正常終了し、後続テストを誤った向きで実行します。

**C — 文書不一致**

- **C3-6** `design-v4.md:458`: `AutoRotation`＋UI 補助の `rotate`＋テスト内 `finally` と記載されています。実装は Portrait/LandscapeLeft の直接設定＋`UnityTearDown` での復元です。
- **C3-7** `design-v4.md:447`: `ResetForTests` 後に購読解除と記載されていますが、実装は `AndroidDialogPlayerTests.cs:109` の購読解除後に reset です。
- **C3-8** `android-dialog-ui-test-plan-v1.md:99`: `AndroidUiDriver` カテゴリを AD-01〜28 だけとしています。実装はクラス単位で AD-01〜34 すべてが対象です。
- **C3-9** `implementation-result-v3.md:175`: 同じ最終 `verify-fix2` 実行の UI 補助依頼数が 113 件ですが、同文書の 331 行目では 114 件です。
- **C3-10** `implementation-result-v3.md:261`: 「`Show*` のたびに `EnsureCreated`」とありますが、実装は検査と端末判定を通過してライブラリを呼ぶ場合だけです。`common.md` の「ライブラリを呼ぶたび」が実装と一致します。

欠陥なしを確認した領域:

- `Post` の enqueue→flag read と、`OnDisable` の flag clear→barrier→queue check の順序論: 欠陥なし。
- 正規 pump と重複 pump を区別する `OnEnable`/`OnDisable`: 欠陥なし。
- 現在の `EnsureCreated` 実装自体による component/GameObject の再有効化: 欠陥なし。
- `Safely` の nested IEnumerator 走査と `ExceptionDispatchInfo` 再送出: 欠陥なし。
- `WaitForAnswerFile` の取消対応、AD-27 の準備順・期待値・要求数計上: 欠陥なし。
- destroyed queue object の Editor テスト: 欠陥なし。
- C ABI 署名、構造体、結果所有権、`finally` での result free: 欠陥なし。

## 3. 総合評価

**要修正（軽微）**。サブエージェントも Codex も、**A は 0 件**。

止める基準の区分での集計:

- A: 0 件。ただし R3-9（別のスレッドからの `Show*` が例外を出す）は、1.x が別のスレッドから動いていたなら回帰になる。統合のときの判断: 1.x の `Show*` は検査の後に `AndroidJavaObject.Call` を呼ぶだけで、Unity の JNI は呼んだスレッドを自動でアタッチするので、別のスレッドからも動いた見込みが高い。安く直せるので、A 相当として直す
- B: 範囲の B は、R3-1 = C3-2・C3-3、R3-2 = C3-1、R3-3 = C3-5、R3-4、R3-8、R3-10、C3-4
- C: R3-5、R3-6、R3-7、C3-6〜C3-10

止める基準の 1（レビュアーを替えて 1 回通す）は、ラウンド 3 のサブエージェント（別のインスタンス）と Codex の両方が A 0 件で満たす。2（足したテストが、壊すと落ちること）と 3（直さない残件を理由つきで書くこと）は、この後の修正と実装結果 v4 で満たす。
