# レビュー結果

- 日付: 2026-10-11
- 対象ファイル: artifact/android/dialog/designs/2026-10-11-android-dialog-design-v2.md（あわせて artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v2.md）
- 機能名: dialog
- プラットフォーム: Android
- レビュアー: サブエージェント 1 本（この節から「総合評価」まで）と Codex 1 本（末尾の「Codex の指摘」）。どちらも出力をそのまま載せた

---

## v1 の指摘の解決状況

| ID | 判定 | 根拠（v2 の節） |
|---|---|---|
| H-1 | 解決 | 5.1（:251-260）と J-D11（:297）で 4 機能 21 ファイルを 1 コミットで移す。`common.md` の Common 表の行（:276）、型は入れ子の `Kind` / `Answer`（:267） |
| H-2 | 解決 | 基本情報「方針」（:11）に `Instance`・`Initialize()` を含めた。J-D10（:296）、5.3（:321） |
| H-3 | 解決 | J-D12（:298）、4.4（:229）、5.3（:312-313）、E-4、7.1（:390）、7.2（:400） |
| H-4 | 解決 | J-D2（:288）で `ConcurrentQueue` と Manager の `Update` にした。別課題 `dispatcher-runs-under-lock` も作った。ただし配送の形が変わったことによる新しい問題がある（R2-H-1） |
| H-5 | 解決 | 5.3 の `CANCELED_BY_SYSTEM` の行（:316）、2 章の時点（:139-141）、J-D8（:294）、サロゲートの行（:314） |
| M-1 | 解決 | 4.1 `ReadUtf8OrNull`（:189）、4.3（:216）、7.1（:394）、7.3（:411） |
| M-2 | 解決 | J-D13（:299）、4.4 のログの列（:226-242）、5.1 の `csharp.md`（:277） |
| M-3 | 解決 | 5.1（:272-273）が外 `UNITY_ANDROID`、中 `!UNITY_EDITOR` の 2 段になった |
| M-4 | 一部 | READY / Ignore / Fail（UI 3.2 :96-98）と、ゲートの「HELLO が 1 回以上」（UI 7 :138）は入った。ただし READY を書けなかった場合と遅れた場合、`--as-package-user` の 2 回目、`skipped` の数を判定していない（R2-M-2） |
| M-5 | 一部 | (1) tmp と mv（UI :90）、(2) run の GUID（:63）、(4) trap と finally（:111-117）、(5) キーボード（:78）、(6) `[a-z0-9]`（:77）、(7) V-5〜V-7 は解決。(3) は `svc power stayon false`（:114）で戻すので、元の値を戻していない（R2-M-6） |
| M-6 | 解決 | UI 8 章（:145）で層 1 をやめ、doctest と層 2b にした |
| M-7 | 解決 | 別スレッドの呼び出しのテストを削った（J-D14 :300）。取り消しのテストは `Instance` を先に作り、受け口と `Kind.Confirm` を使う（7.3 :408） |
| M-8 | 解決 | 層 2a（7.2）、外側のタップと `not_cancelable`（7.3 :412）、E-8 の自動化の試み（:418）。ただし E-8 の手順が実行できる形になっていない（R2-M-3） |
| M-9 | 解決 | 7.3（:417）、1.5（:100）、V-7。手動の項目はなくなった |
| M-10 | 解決 | 7.1（:388）で delegate の型を反射で調べる |
| M-11 | 一部 | Manager は長さだけを出す（4.4 :245）。サンプルのコントローラーはまだ入力とパスワードを出している（R2-L-4） |
| M-12 | 解決 | 7.3（:406, :421-423）。ただし Ignore の条件が広い（R2-M-5） |
| L-1 | 解決 | 5.1 の表（:268-278）。マニュアルは段 4 に回すと明記（:260） |
| L-2 | 解決 | 4.1（:187）で受け口を Manager に置き、delegate の型だけを Bridge に置く |
| L-3 | 解決 | 1.1 の OP-19（:46）、1.3 の `NOT_INITIALIZED` の行（:81）、出典は「第 1 部の設計 1.3」（:62） |
| L-4 | 解決 | J-D1（:287）で種類を 1〜6 にし、外れる理由を書いた |
| L-5 | 解決 | 2 章（:128）に互換のための例外と明記した |
| L-6 | 解決 | 2 章（:144）に推定として書いた |
| L-7 | 解決 | `check_design_consistency.py` で両方の文書が SKIP / FAIL なし（「new-file types are described」は OK）。コードの囲みは ` ```csharp `（:333） |
| Codex 1 | 解決 | M-1 と同じ |
| Codex 2 | 解決 | J-D14（:300）と 5.4 手順 5（:329）で「UnityMain から呼ぶ」を契約にした。列を介すので、戻る前にイベントは出ない |
| Codex 3 | 一部 | M-4 と同じ（R2-M-2） |
| Codex 4 | 解決 | UI 3（:63-64）、3.2（:90） |
| Codex 5 | 解決 | UI 1（:39）と 2 章（:50）で `run-as` に一本化した。ただし代わりの手段が無い（R2-M-4） |
| Codex 6 | 解決 | `back <選び方>`（UI :78）。待つのか、1 回調べるだけなのかが曖昧（R2-M-6） |
| Codex 7 | 解決 | 1000 バイトの上限と `ArgumentException`（UI :66）、V-4 |
| Codex 8 | 解決 | `[a-z0-9]` の 1〜64 文字（UI :77） |
| Codex 9 | 解決 | finally、シグナル、trap と、`settings delete` で戻す（UI :111-117） |

## 強み

- **21 ファイルの移動の一覧は、完全で正しい。** 以下を確かめた。
  - 数の内訳は Clipboard 7、Dialog 1、Notification 6、Share 7 で、合計 21。
  - 接頭辞の無い 10 個の型を参照するのは、移すファイル自身、Android のコントローラー 3 本、`Tests/Runtime/Android*` 3 本、`Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs` だけ。
  - Android 以外での出現は 3 か所で、すべてコメントの中の文字列（`IosClipboardOperationResult.cs`:15、`IosShareJsonBuilder.cs`:12、`WindowsClipboardJsonBuilder.cs`:18）。
  - 文字列や直列化で型を参照している箇所は無い（`.unity`、`.asset`、`Type.GetType` を検索した）。
  - 移した先の名前空間 `…Runtime.Android.*` の中の `UnityEngine.Android.Permission*` は、完全修飾で書かれている（`AndroidNotificationManager.cs`:270-274）。そのため名前の解決は変わらない。
  - 移した後も、旧の名前空間を `using` する iOS / macOS 側のファイルは壊れない。旧の名前空間は、ガードされていない iOS / macOS のファイルか、同じガードの型によって残る。
    - 該当: `IosNotificationJsonBuilder.cs`、`MacShareJsonBuilder.cs` など。
    - `MacNotificationTests.cs`:4 のガードの無い `using` も、`IosNotificationJsonBuilder` が残るので通る。
- **J-D13 が既存のテストとサンプルを壊さないことを確かめた。**
  - `AndroidDialogManager` を参照するテストは 0 件。
  - `LogAssert.Expect(LogType.Error, …)` は Clipboard のテストの中だけにある（`AndroidClipboardManagerDispatchTests.cs`:76/92、`AndroidClipboardJsonParserTests.cs`:94-251）。
  - サンプル（`AndroidDialogManagerExampleController.cs`）とマニュアルは、`LogError` の有無に依存していない。
- **否定ボタンの値（4.3）が一次資料と一致する。**
  - 否定はどの種類も `DialogValue.None` を返す（`AndroidDialogFragment.kt`:251, 268, 285, 303, 339）。
  - そのため、単一選択は -1、複数選択は `checked` が NULL で `count == 0`、入力は NULL になる（`DialogBridge.kt`:214-223、`Dialog.cpp` の読み取り）。
  - `DISMISSED` のボタンの文字は NULL（`DialogBridge.kt`:199）なので、C# の固定の `"Cancel"` が要る。これも正しい。
- **J-D2 は、v1 の H-4 の原因を正しく取り除いている。** 原因は `UnityMainThreadDispatcher.cs`:57-63 で、ロックを持ったまま `Invoke` していた。新しい別課題の README も、コードの事実と一致する。
- **7.3 の 64 / 32 ビットの分け方**は、スパイクの `AndroidNativeToolkitCApiPlayerTests.cs`:41-79 と同じ形。

## 改善点

### 高優先度

**R2-H-1**
- 区分: A1
- 観点: (T)(B)(N)
- 節: J-D2、5.4（:346-347）、5.3
- 問題点: 配送を dispatcher から「Manager の `Update` で static な列を空にする」に変えたことで、次の振る舞いが変わる。どれも 5.3 に無く、コンパイルもテストも通る。
  - (a) **Manager の component や GameObject を無効にすると、完了が届かなくなる。** 旧は別の GameObject の dispatcher が `Instance.X?.Invoke` を呼ぶので、無効でも届いた。
  - (b) **Manager が壊れると、完了は static な列に残り、後で別の Manager の購読者に届く。** 届くのは、次に誰かが `Instance` を呼んで Manager ができた後の `Update`。イベントは種類ごとで、呼び出しとの対応を持たない。そのため、例えば Confirm を開いたまま Manager が壊れ、利用者が「Yes」を押したとする。その完了は、後で新しく出した Confirm の「Yes」として届く。旧は、届いた直後の dispatcher のフレームで `Instance` を作り直して `Invoke` していた。購読者のいない新しい実体に出るので、実際には捨てられていた。5.4 の「どちらも、完了が捨てられないことは同じ」は誤り。
  - (c) `Update` に `_instance != this` の守りが無い。Windows の前例にはある。
- 根拠:
  - `AndroidDialogManager.cs`:56-64, 127-130
  - `UnityMainThreadDispatcher.cs`:28-33（dispatcher は Manager と別の GameObject）
  - `WindowsClipboardManager.cs`:2672-2675（`if (session == null || _instance != this) return;`）
  - 設計 v2 :288, :346-347
- 改善提案:
  - `Update` は `_instance == this` のときだけ列を空にし、イベントは `_instance` に出す。
  - `_instance` の `OnDestroy` で列を空にして、旧と同じく「その時点の購読者がいなければ失われる」にする。または、世代番号を `Answer` に入れて、古い世代を捨てる。
  - (a) は、5.3 に「無効のあいだは届かない」と書く。保つなら、component の有効・無効に依らない取り出し口（`PlayerLoop` への登録など）にする。
  - 層 2a に「破棄して作り直した後、古い完了が出ない」テストを足す。

### 中優先度

**R2-M-1**
- 区分: A2
- 観点: (C)(P)
- 節: 5.1（:266-267）、5.4（:345）
- 問題点: 書き方の決まっていない箇所が 2 つある。
  - **ガード**: Manager は `UNITY_ANDROID || UNITY_EDITOR` でコンパイルされる。一方、受け口は `ntk_dialog_result_free` と結果の読み取り（`extern`）を呼ぶ。`extern` は `UNITY_ANDROID && !UNITY_EDITOR` の中にある。受け口・関数ポインタ・`Read(kind, error, result)` をどのガードに置くかが書かれていないので、そのまま書くと Editor でコンパイルエラーになる。
  - **ログ**: `csharp.md` は、`Update` を含む全メソッドの先頭で `Debug.Log` を求めている。従うと毎フレームのログになり、補助が `-s Unity:I` で読む logcat を埋める。また J-D2 は「受け口は Unity の API に触らない」と言いながら、受け口の中で `LogError` を出すと書いており、食い違っている。
- 根拠:
  - `AndroidNativeToolkitCApi.cs`:128-227（`extern` と `#else` のスタブの分け方の前例）
  - `csharp.md`:9, :29
  - 設計 v2 :288, :345
- 改善提案:
  - 受け口と `Read` は `UNITY_ANDROID && !UNITY_EDITOR` の中に置く。`Answer` → イベントの引数への変換だけを、ガードの外の純粋な関数にする（層 1 で確かめられる）。これを 5.1 に書く。
  - `Update` は「列から取り出したときだけログ」とし、規則の例外として明記する。
  - 受け口の中の `Debug.Log*` はスレッド安全なので例外として許す、と J-D2 に書く。

**R2-M-2**
- 区分: B
- 観点: (Q)(A)
- 節: UI 補助 3.2（:96-97）、7 章（:138）、Dialog 8.3（:447）
- 問題点: ゲートは「HELLO を 1 回以上受けた」と「補助の終了コードが 0」だけを見ている。次の場合は、UI のテストが全部 Ignore でも緑になる。
  - 補助が HELLO を受けたが、`ready` を書けなかった（V-1 の失敗、`run-as` のエラー）
  - `ready` が 5 秒より遅れた（冷えた状態の最初の `run-as` など）
  - `--as-package-user` の 2 回目の実行（補助を実行ごとに起動するのかが書かれていない。1 回目の HELLO で条件を満たしてしまう）

  `report_tests` は `failed="0"` しか見ないので、`skipped` が数えられない。Dialog 8.3 の「ゲートでは skip にならない」は言い過ぎ。
- 根拠: `verify_unity_android.sh`:113-118, :163-170、UI v2 :96, :138
- 改善提案:
  - 補助は `ready` を書けなかったとき、または HELLO から READY まで 5 秒を超えたとき、終了コードを 0 以外にする。
  - 補助を `run_tests` の 1 回ごとに起動・停止する。
  - IL2CPP の実行では、結果の XML から `AndroidDialogPlayerTests` の `skipped == 0`（または期待する passed の数）を判定に足す。
  - Dialog 8.3 の文言を、判定の内容に合わせる。

**R2-M-3**
- 区分: B
- 観点: (Q)(A)
- 節: 7.3「後ろ（E-8）」（:418）
- 問題点: 手順に 3 つの穴がある。
  - (1) **後ろに回ったことを知る手段が無い。** 別のスレッドが「アプリが後ろに回った」を知る手段が書かれていない。早く呼ぶと、まだ前面なのでダイアログが出てしまう。`Foreground.isForeground()` は `ForegroundActivityTracker.current()` で判定する。そのダイアログは残り、テストは期限切れになる。
  - (2) **前に戻す依頼を出せない。** 「補助がアプリを前に戻す」依頼は、UnityMain が止まっている間は出せない。`AndroidUiDriver.Do` は UnityMain のコルーチンで、番号の採番もスレッド安全ではない。
  - (3) **一時停止中の Test Runner との接続が確かめられていない。** Unity が一時停止している間、Test Runner の接続（PlayerConnection）が保たれるかを確かめていない（**要検証**）。
- 根拠: `Foreground.kt`:16-19、`DialogBridge.kt`:172-174、UI v2 :82-83, :121-125
- 改善提案: 補助に、まとめて行う命令を 1 つ足す。
  - 命令: `background <ms>`
  - 補助の動き: `home` → `dumpsys activity` でアプリが停止したことを確かめる → `ok` を書く → `<ms>` 待つ → `front`
  - テストの動き: UnityMain から依頼する → 別のスレッドが結果のファイルの `ok` を自分の期限つきで待ってから Bridge を呼ぶ
  - V-8 として「一時停止の間も Test Runner が切れない」を足す。

**R2-M-4**
- 区分: B
- 観点: (A)
- 節: UI 補助 1 章（:39）、2 章（:50）、V-1
- 問題点:
  - 結果の受け渡しを `run-as` に一本化したので、V-1 が端末で通らなければ方式全体が成り立たない。代わりの手段が無い。
  - 「テスト用の Player は development ビルドで debuggable」は確かめていない（**要検証**）。
- 根拠: UI v2 :39, :151
- 改善提案:
  - V-1 に `dumpsys package <pkg>` の `DEBUGGABLE` の確認を足す。
  - 代わりの手段を書いておく。例えば `adb reverse tcp:<port>` でホストへソケットをつなぐ方式なら、保存領域の制限にも logcat の解析にも依らない。

**R2-M-5**
- 区分: B
- 観点: (Q)
- 節: 7.3「64 ビット」（:406）、2b のテストの後片付け
- 問題点:
  - (1) **Ignore の条件が広い。** 64 ビットで `EnsureNativeAvailable() != Available` のときも `Assert.Ignore` にしている。そのため、AAR が写らなかった回帰（親課題 README :116 で実際に起きた）が Dialog の側では skip に隠れる。共通部のテスト（`AndroidNativeToolkitCApiPlayerTests.cs`:46-52）が落ちるので、影響は限られる。
  - (2) **後片付けが無い。** 2b のテストに `TearDown` が無い。テストが失敗や期限切れで終わると、次のことが起きる。
    - ダイアログが画面に残る。
    - その完了は、static な列を通って次のテストのイベントとして数えられる。
    - 補助が、次のテストで残ったダイアログのボタンを押しうる。
- 根拠: `AndroidNativeToolkitCApiPlayerTests.cs`:41-53、`AssemblyInfo.cs`:5-7（`ResetForTests` の前例）、設計 v2 :406-419
- 改善提案:
  - Ignore の条件は `IntPtr.Size == 4` だけにする（前例と同じ）。
  - `UnityTearDown` を足す。補助の `wait-gone resource-id=android:id/alertTitle`（残っていれば `back`）で画面を片付け、internal の `ResetForTests` で列を空にする。

**R2-M-6**
- 区分: B
- 観点: (A)
- 節: UI 補助 3.1（:76, :78）、3 章（:63）、4 章（:114）、6 章（:130）
- 問題点: 補助の細部に、次の穴が残っている。
  - `tap-outside` は「画面の高さの 1/12」に固定している。長い一覧や、回転の後のダイアログでは、上端がそれより上に来て、ダイアログの中を押しうる。
  - `back <選び方>` が、部品が出るまで待つのか、1 回調べるだけなのかが分からない。
  - 新しい `<run>` の HELLO が来たとき、補助が今の run を切り替えるのかが書かれていない。
  - `svc power stayon false` で戻すので、`stay_on_while_plugged_in` の元の値を壊す。
  - `-T <時刻>` は端末の時計で取る必要がある。
  - `fail <理由>` を `adb shell run-as … sh -c '…'` で書くときの、引用の規則が無い。
- 根拠: UI v2 :63, :76, :78, :114, :130
- 改善提案:
  - 外側の座標は `bounds` から計算する（画面の上端とダイアログの上端の中点）。余白が足りなければ `fail no-outside` にする。
  - `back` は `wait` と同じ期限で待つ、と書く。
  - HELLO が来たら run を切り替え、前の run のフォルダを消す。
  - `stay_on_while_plugged_in` を覚えて戻す。
  - 時刻は `adb shell date` で取る。
  - 理由は英数字と `-` だけにする。

### 低優先度

**R2-L-1**
- 区分: C
- 観点: (N)
- 節: J-D2（:288）
- 問題点: 「待つのは `ConcurrentQueue` の中だけ（ロックを取らない）」は正確ではない。.NET の `ConcurrentQueue<T>` は、セグメントを足すときに内部のロックを短く取る。言いたいこと（購読者のコードの間は待たない）は正しい。
- 根拠: 設計 v2 :288
- 改善提案: 「購読者のコードが動いている間に保持されるロックを待たない」と書き直す。

**R2-L-2**
- 区分: C
- 観点: (P)
- 節: J-D13（:299）、5.1 の `csharp.md` の行（:277）
- 問題点: 「`LogWarning` の規則を Android にも」と書くと、範囲が広すぎる。既存の Android Clipboard の JSON の層は、結果として返す失敗に `LogError` を出していて、テストもそれを期待している。規則に反する状態になってしまう。
- 根拠: `AndroidClipboardJsonParserTests.cs`:94-251、`csharp.md`:11-12
- 改善提案: 「Android の C ABI の Bridge / Manager では」と Windows と同じ書き方にする。

**R2-L-3**
- 区分: C
- 観点: (N)
- 節: 5.3、J-D6
- 問題点: Editor（J-D6）では、次の 2 つで振る舞いが変わるが、5.3 の行は Android 実機のことしか書いていない。
  - 範囲外の `checkedItem` と、項目の `null` の要素で、失敗のイベントが出るようになる。
  - 旧の Editor では、検査を通って `LogWarning` を出すだけだった。
- 根拠: `AndroidDialogManager.cs`:353-397（範囲と要素は検査せず、Editor では `LogWarning` だけ）
- 改善提案: 5.3 の該当行に「Editor でも同じ」と足す。

**R2-L-4**
- 区分: C
- 観点: (E)
- 節: 4.4（:245）、5.1
- 問題点: サンプルのコントローラーが、入力とパスワードをそのまま `Debug.Log` と画面に出している。サンプルは移動のコミットで `using` を直すので、ついでに直せる。
- 根拠: `AndroidDialogManagerExampleController.cs`:605, 629-630
- 改善提案: 長さだけを出すように直すか、直さない理由を 5.1 に書く。

**R2-L-5**
- 区分: C
- 観点: (F)
- 節: 5.1、8.3
- 問題点: 次の文言と記録が揃っていない。
  - Dialog 8.3 は「補助が応答したこと」、UI 7 は「HELLO を受けたこと」と書いている。
  - 5.1 は「`Assets/Scripts/Runtime/UI/Android/*/`」と書いているが、実在するのは Dialog の 1 本だけ。
  - J-D11（4 機能をまとめて移す）は、後の 3 機能の設計の前提になる。それなのに、親課題 README の「決定」に載っていない。
  - `dispatcher-runs-under-lock` の README に、「Android の残りの 3 機能も同じ受け口の形にする」が無い。
  - 取り消しのテストで使う Manager の関数ポインタが `internal` であることが書かれていない。
- 根拠: 設計 v2 :260, :447、UI v2 :138、親課題 README :58-66
- 改善提案: 各所の文言と記録を揃える。

**R2-L-6**
- 区分: C
- 観点: (T)
- 節: 5.4（:346）
- 問題点: `while (TryDequeue)` は、取り出している間に Android の main が入れた完了も、同じループで出す。旧の dispatcher では、ロックのために次のフレームに回っていた。購読者が `Show*` を呼び直す作りで、1 フレームの仕事に上限が無くなる。
- 根拠: `UnityMainThreadDispatcher.cs`:57-63
- 改善提案: `Update` の始めの `Count` だけを取り出す、と書く。

## 不足項目

- Manager の無効・破棄・重複のときの配送の規則（`_instance == this`、`OnDestroy` で列を空にするか世代番号を使うか）と、それを 5.3 に書くこと（R2-H-1）
- 受け口と `Read` のガードの置き場所。`Update` と受け口のログの規則の例外（R2-M-1）
- ゲートの判定の補強（READY の失敗で終了コードを変える、実行ごとの補助、`skipped == 0` か期待する件数）（R2-M-2）
- E-8 を確かめる手順（後ろに回ったことの同期、`background <ms>` の命令、V-8）（R2-M-3）
- `run-as` が使えないときの代わりの手段と、debuggable の確認（R2-M-4）
- 2b の後片付け（`UnityTearDown`、残ったダイアログを閉じる、`ResetForTests`）と、Ignore の条件を `IntPtr.Size == 4` だけにすること（R2-M-5）

## 総合評価

v1 の指摘は、41 件中 36 件が解決、5 件（M-4、M-5、M-11、Codex 3、Codex 1 の派生ではない Codex 3 相当）が一部の解決です。21 ファイルの移動の一覧、J-D13 の影響、否定と閉じたときの値の写し方は、一次資料で裏が取れました。

一方で、H-4 の直し方（Manager の `Update` で列を空にする）から、新しい A1 が 1 件出ています（R2-H-1）。無効の Manager には完了が届かず、壊れた Manager の完了は後で別の購読者に届きます。後者は、呼び出しと対応しない種類ごとのイベントなので、新しい Confirm に古い「Yes」が届く形で表に出ます。5.3 にも無く、テストでも見つかりません。実装の前に、この 1 件を直す必要があります。

UI の補助は、v1 の穴の多くを塞ぎました。ただし、次の 3 点が中優先度で残っています。
- 全部 skip でもゲートが緑になる経路がまだある（R2-M-2）
- E-8 の手順が実行できる形になっていない（R2-M-3）
- 2b の後片付けが無い（R2-M-5）

R2-H-1 を直し、中優先度を反映したら、再レビューは差分の確認だけで足りる見込みです。

---

## Codex の指摘

（`codex:codex-rescue` の出力。そのまま載せる）

指定された一次資料と v2 を照合しました。ファイルは変更していません。機械照合は両設計書とも `FAIL` なしでしたが、多くの項目が `SKIP` のため、以下の意味論上の欠陥は検出対象外です。

### Round 1 の9件

| # | 判定 | v2 の根拠 |
|---|---|---|
| 1. `NULL` と空文字を区別できない | 修正済み | `ReadUtf8OrNull` で NULL と0バイト非NULLを分けています。Dialog設計:189、結果変換も否定時 `null`、空文字 `""` を明記しています。Dialog設計:216、テストも両者を分けています。Dialog設計:394 |
| 2. off-main で復帰前にイベントが発火し得る | 修正済み | 公開 API を UnityMain 限定とし、callback はキュー投入だけ、イベントは Manager の `Update` に限定しています。Dialog設計:300、Dialog設計:329、復帰前に出ない実機テストも追加されています。Dialog設計:409 |
| 3. helper 障害が Ignore になりゲートが成功する | 未修正 | READY 後の無応答を Fail にする点は直りました。UI補助設計:94 しかし READY が来ない場合は依然 Ignore であり、ゲートが数えるのは「READY を返した回数」ではなく「HELLO を受けた回数」だけです。UI補助設計:96、UI補助設計:138 HELLO の解析後、READY 書込みだけが失敗した場合に UI 未検証のまま緑にできます。ゲートは成功した READY 数と UI 命令数、またはテスト結果の `skipped=0` を検査する必要があります。 |
| 4. session 分離と atomic write がない | 修正済み | GUID の run と通し番号を導入しています。UI補助設計:63 結果は同一フォルダの `.tmp` から `mv` します。UI補助設計:89 |
| 5. `run-as` fallback と Player の読取先が一致しない | 修正済み(ただしマルチユーザー欠陥あり、新規2) | 外部領域を廃止し、host と Player の双方を内部 `files` に統一しています。UI補助設計:39、UI補助設計:124 AOSP の `run-as` はアプリ UID・SELinux context に切り替えてデータディレクトリへ `chdir` するため、同一ユーザーなら所有権と読取りは成立します。 |
| 6. `back` が表示前に Activity へ届く | 修正済み | selector の出現を確認し、IME が表示中なら先に閉じてからダイアログへ Back を送る契約になりました。UI補助設計:78 |
| 7. logcat protocol に長さ上限がない | 修正済み | UTF-8 1000バイトを上限とし、送信側で `ArgumentException` にします。UI補助設計:66 |
| 8. `input text` の ASCII 対応範囲が広すぎる | 修正済み | 入力を `[a-z0-9]` の1〜64文字に限定しています。UI補助設計:77 Dialog の入力テストも英小文字・数字だけです。Dialog設計:415 |
| 9. 設定復元が正常な SIGTERM に限定される | 修正済み(ただし stayon の復元に別欠陥、新規3) | Python の `finally`、TERM/INT/HUP、親スクリプトの trap が定義され、元値が `null` なら `settings delete` で戻します。UI補助設計:111、UI補助設計:117 |

結論は、8件修正済み、1件(#3)未修正です。

### 新規の具体的欠陥

#### 1. Manager が無効・破棄状態だと完了が停止し、後の利用者へ古い結果を配送し得る
- severity: high
- section: Dialog 設計 5.2 J-D2、5.4
- evidence:
  - v2 は static `ConcurrentQueue` を、Manager 自身の `Update` だけで排出します。Dialog設計:288、Dialog設計:346
  - Manager 破棄中の完了を保持し、次に作られた Manager から出すことまで明記しています。Dialog設計:347
  - Unity は無効な `MonoBehaviour` の `Update` を呼びません。
  - 現行実装では callback は独立した dispatcher に入り、実行時に `Instance` を解決します。AndroidDialogManager.cs:599、UnityMainThreadDispatcher.cs:55
- impact:
  - `manager.enabled = false` または GameObject 非アクティブ中に完了すると、イベントが無期限に来ません。
  - Manager を破棄すると、古い完了が static queue に残り、新しい Manager の購読者が前世代の結果を最初に受け取る可能性があります。
  - 7.3 は常に Manager を先に作って有効なまま使うため、この欠陥を検出しません。Dialog設計:408
- fix:
  - 排出を Manager の有効状態に依存しない専用の persistent pump に移す。
  - キュー要素に Manager generation を持たせ、破棄済み世代の結果を捨てるか、旧実装同様 `Instance` を再生成して即時配送するかを契約化する。
  - Manager 無効化、GameObject 非アクティブ、破棄後再生成、複数完了の FIFO を PlayMode テストに追加する。

#### 2. `run-as` が常にユーザー0を使い、secondary user/work profile の Player と別の `files` を読む
- severity: medium
- section: UI補助設計 1章、3章、5章
- evidence:
  - HELLO が運ぶのは run と package だけで、Android user ID がありません。UI補助設計:29、UI補助設計:59
  - host は `run-as <package>`、Player は現在実行中ユーザーの `currentActivity.getFilesDir()` を使います。UI補助設計:39、UI補助設計:124
  - AOSP の `run-as` は `--user` が無ければ `userId = 0` とし、`/data/user/<userId>/<package>` へ移動します。
- impact: owner user のテストでは通りますが、secondary user や work profile では host が `/data/user/0/...` に書き、Player は `/data/user/N/...` を待つため全 UI テストが Ignore または timeout になります。
- fix:
  - HELLO に `UserHandle.myUserId()` を追加する。
  - 全ての mkdir、tmp 書込み、同一ディレクトリ内 `mv`、削除を `run-as <package> --user <id> ...` の内側で実行する。
  - READY の前に host 書込み→Player 読取り→Player 削除までの round-trip probe を必須化する。

#### 3. `svc power stayon false` は元の設定を復元しない
- severity: medium
- section: UI補助設計 4章
- evidence:
  - 起動時に無条件で `svc power stayon usb` を設定します。UI補助設計:107
  - 終了時は元値ではなく、常に `svc power stayon false` としています。UI補助設計:114
  - AOSP 実装では `usb` は USB のビット値、`false` は値0を設定します。元が `true`、`ac` などなら同じ状態には戻りません。
- impact: テスト自体は通っても、終了後に利用者の「充電中は画面を点灯」設定を壊します。
- fix:
  - 起動時に `settings get global stay_on_while_plugged_in` と存在状態を保存する。
  - `finally` で数値をそのまま `settings put global` し、元が未設定なら `settings delete global stay_on_while_plugged_in` を使う。
  - 元値0、USB、AC、複数ビット、未設定をシェルテストに含める。

#### 4. `rotate` の完了条件が「変化」で、既に目的方向なら成立しない
- severity: low
- section: UI補助設計 3.1、Dialog 設計 7.3
- evidence:
  - `rotate` は指定値を書いた後、「`wm` の表示が変わるまで待つ」としています。UI補助設計:81
  - Dialog テストは単に「横にする」とし、開始時の回転値と異なる値を選ぶ契約がありません。Dialog設計:417
  - Android は `accelerometer_rotation=0` と `user_rotation` を監視して回転を決めます。
- impact: 端末が既に指定した横向きなら値も表示も変わらず、正常状態なのに timeout します。逆に「現在値が目標値なら成功」にすると、回転遷移を一度も起こさずテストが通ります。
- fix:
  - 成功条件を「現在の実回転が指定値と一致」にする。
  - Dialog テストは開始回転を取得し、必ず異なる90度の値を選び、変更前後が異なることを確認してからダイアログを検証する。
  - 終了時は保存した `accelerometer_rotation` と `user_rotation` の存在状態まで復元する。

21ファイルの移動一覧、`type` 制限、Back/IME、tmp→同一ディレクトリ `mv`、`settings delete`、旧ブリッジとの結果対応も再照合しましたが、上記以外の新規の具体的欠陥は確認できませんでした。
