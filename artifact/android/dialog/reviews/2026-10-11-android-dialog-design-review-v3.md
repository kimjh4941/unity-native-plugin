# レビュー結果

- 日付: 2026-10-11
- 対象ファイル: artifact/android/dialog/designs/2026-10-11-android-dialog-design-v3.md（あわせて artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v3.md）
- 機能名: dialog
- プラットフォーム: Android
- レビュアー: サブエージェント 1 本（この節から「総合評価」まで）と Codex 1 本（末尾の「Codex の指摘」）。どちらも出力をそのまま載せた。v2 との差分の確認

---

## v2 の指摘の解決状況

| ID | 判定 | 根拠（v3 の節） |
|---|---|---|
| R2-H-1 | 解決 | J-D2（Dialog :304）と 5.4（:361-363）で、完了は `Post(() => Instance.Raise(answer))` にした。旧の `PostToMainThread(() => Instance.X?.Invoke(...))`（`AndroidDialogManager.cs`:127-130）と同じく、取り出した時点で `Instance` を解決する。このため、無効な Manager にも届く。壊れた Manager の代わりには、UnityMain の `Instance` が作り直されて、そこに出る（購読者がいなければ失われる）。購読者の例外は残りの完了を次のフレームに回すが、これも旧（`UnityMainThreadDispatcher.cs`:57-63）と同じ。同期の失敗は、旧も新も `this` のイベントに直接出す（`AndroidDialogManager.cs`:237-273）ので、順序は変わらない。(c) は `Instance` に出すので、守りが要らない。テストは 7.2（:416）に入ったが、ゲートでは流れない（N-4） |
| R2-M-1 | 解決 | 5.1（:280, :283）で、受け口・関数ポインタ・`Read` は `UNITY_ANDROID && !UNITY_EDITOR` に置き、変換はガードの外の純粋な関数にした。ログの例外は 5.4（:362）と J-D2 |
| R2-M-2 | 解決 | UI 7 章（:154-160）。終了コード 3、実行ごとの補助、カテゴリ `AndroidUiDriver` の skip が 0、付け忘れの検出。XML の形の裏取りは残る（L-8） |
| R2-M-3 | 一部 | UI 3.1 の `background`（:93）、5 章の `WaitForResultFile`、V-8（:179）。ただし「止まった」の判定がネイティブの判定とずれている（N-2） |
| R2-M-4 | 解決 | V-1（:172）に `DEBUGGABLE` の確認を足し、代わりの手段として `adb reverse` を書いた（:59） |
| R2-M-5 | 解決 | 7.3（:424, :426）。Ignore の条件は `IntPtr.Size == 4` だけになり、`UnityTearDown` も足された。後片付けに競合が残る（N-3） |
| R2-M-6 | 解決 | UI :77, :86, :88, :72, :101, :121-129。`tap-outside` の基準の部品（N-5）と、時刻の引用（L-4）が新しく残る |
| R2-L-1 | 解決 | J-D2 の「購読者のコードが動いている間に、受け口が待たされるロックは無い」 |
| R2-L-2 | 一部 | J-D13（:315）は直った。5.1 の `csharp.md` の行（:293）は、まだ「Android にも」のまま |
| R2-L-3 | 解決 | 5.3（:326, :328）に「Editor でも同じ」 |
| R2-L-4 | 解決 | 5.1（:282） |
| R2-L-5 | 解決 | 8.3（:467）、5.1（:274）、親課題 README の J-8、dispatcher の README（4 機能）、5.1 の `internal`（:280）。ただし「補助の設計書 v2」への参照が残っている（L-7） |
| R2-L-6 | 解決 | 5.4（:362）で、始めの `Count` だけを取り出す。旧と同じとする根拠の書き方に誤りがある（L-2） |
| Codex #3（持ち越し） | 解決 | UI 7 章（:155-158） |
| Codex 新規 1 | 解決 | R2-H-1 と同じ |
| Codex 新規 2 | 解決 | UI :68, :46, 図の probe（:32-38）、V-1。ユーザー ID の取り方が弱い（L-3） |
| Codex 新規 3 | 解決 | UI 4 章の表（:121-126）で、値が 7 で、元の値に戻す。`svc power` は使わない |
| Codex 新規 4 | 解決 | UI :91-92 と Dialog :437。ただし、既定のゲートの Player は縦に固定されている（N-1） |

## 新しい指摘

**新しい A1 はありません。** 配送の振る舞い（無効・破棄・作り直し・同期の失敗との順序・例外のあとの残り）は、旧の経路と一致していることを一次資料で確かめました。

### 高優先度

なし

### 中優先度

**N-1**
- 区分: B
- 観点: 回転
- 節: Dialog 7.3（:437）、UI 3.1 `rotate`（:92）
- 問題点: 既定のゲートの実機の実行は `Android™ Profile` で、この Profile は Player Settings を上書きして `defaultScreenOrientation: 0`（Portrait 固定）にしている。前面の Activity が縦に固定されていると、`user_rotation` を変えても表示は回らない。そのため `rotate 1` は 10 秒で `fail timeout` になり、テストは Fail になる。一方 ProjectSettings と Test Profile は 4（AutoRotation）なので、`--as-package-user` の実行だけが通る。
- 根拠: `Assets/Settings/Build Profiles/Android™ Profile.asset`:28-37、`ProjectSettings/ProjectSettings.asset`:11、`scripts/verify_unity_android.sh`:164
- 改善提案:
  - 回転のテストの中で `Screen.orientation = ScreenOrientation.AutoRotation` にしてから回し、`finally` で元に戻す。
  - 向きは、補助の終了時ではなく、そのテストの中で元の値に戻す。戻さないと、後のテストが横向きで走り、`tap-outside` の 48px の判定などに影響する。
  - V に「既定の Profile の Player で表示が回ること」を足す。

**N-2**
- 区分: B
- 観点: background / E-8
- 節: UI 3.1 `background`（:93）、Dialog 7.3（:438）
- 問題点: 補助は、Activity が「`RESUMED` でない」（PAUSED を含む）時点で `ok` を書く。ところがネイティブの前面の判定は `started && !stopped && !stateSaved` で、`onActivityStopped` か `onSaveInstanceState` が来るまでは「前面」のままになる。PAUSED から STOPPED までの間に別のスレッドが Bridge を呼ぶと、`NOT_FOREGROUND` にならずにダイアログが出る。その場合、テストは期限切れになり、ダイアログも残る（不安定になる）。
- 根拠: `native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/common/presentation/ForegroundActivityTracker.kt`:42-51、:107-115、`android_library_capi/.../jni/Foreground.kt`:16-19
- 改善提案: 条件を `dumpsys activity activities` の `state=STOPPED` を待つことにする。

**N-3**
- 区分: B
- 観点: 2b の後片付け
- 節: Dialog 7.3 の後片付け（:426）
- 問題点: 後片付けは `back` → `wait-gone` → `ResetForTests()` の順に動く。ところが、Back で閉じたダイアログの完了は、画面の木から消えた後に Android の main から非同期に `Post` される。そのため `Reset` の後に入り、次のテストの購読者に 1 回多く届きうる。
- 根拠: J-D2（:304。受け口は Android の main で `Post`）、7.3（:426）
- 改善提案: 受け付けた要求は必ず 1 回完了する。これを使い、テストの側で「受け付けた数 − `Raise` された数」を数え、0 になるまで（期限つきで）待ってから `Reset` する。

**N-4**
- 区分: B
- 観点: ゲート / R2-H-1 の裏付け
- 節: 7.2（:416）、5.5（:367）
- 問題点: R2-H-1 の直しを確かめる配送のテストは、層 2a（Editor の PlayMode）にある。しかし `verify_unity_android.sh` が流すのは EditMode と実機だけなので、このテストはどのゲートでも流れない。Windows のスクリプトは、Editor の PlayMode を流している。
- 根拠: `verify_unity_android.sh`:159, :164、`verify_unity_windows.sh`:139
- 改善提案: 5.1 の `verify_unity_android.sh` の行に、Editor の PlayMode の実行を足す。

**N-5**
- 区分: B
- 観点: tap-outside の座標
- 節: UI 3.1（:86）、Dialog 7.3（:432）
- 問題点: 「画面の上端と部品の上端の中点」の「部品」が、ダイアログの窓の根でない場合（例: `alertTitle`）がある。このとき、ダイアログの上端が上の余白より小さいと、中点はダイアログの内側に落ちる。すると `cancelableOnTouchOutside = false` のテストは、何も確かめずに緑になる（`count` は 1 のまま）。
- 根拠: UI :86、Dialog :432（選び方が書かれていない）
- 改善提案:
  - 基準は「対象を含むダイアログの窓の最上位の部品の `bounds`」と決める。
  - 押す点がその外にあることを `doctest` で固める。

### 低優先度

- **L-1（C、配置）** 5.1 の `common.md` の行（:292）は、`Runtime/<Platform>/Common/` の表に `AndroidNativeToolkitCApi.cs` だけを足している。`AndroidNativeCallbackQueue` の行が無い。規則（`common.md`:372-383「ネイティブライブラリの共通部」「DLL 名など」）は、`MonoBehaviour` の取り出し口を明示していない。Dialog の時点で使うのも Dialog だけである。
  - 改善: 表に行を足す。「C ABI の完了を UnityMain に運ぶもの」も条件に含めると書く。あわせて `hideFlags` は `HideInHierarchy` にし、`DontSave` を含めない（Editor で Play を抜けた後に残り、取り出し口が 2 つになるのを防ぐ）と書く。
- **L-2（C）** 5.4（:362）の「旧の dispatcher と同じく、1 フレームの仕事に上限がある」は正確ではない。旧の `lock` は再入できるので、UnityMain の処理が中で `Enqueue` したものは、同じループで走っていた（`UnityMainThreadDispatcher.cs`:43-63）。Dialog は Android の main からしか `Post` しないので、観測できる差は無い。
  - 改善: 「別スレッドからの分は旧と同じ。同じスレッドから入れたものは次のフレームに回す（新しい仕様）」と書く。
- **L-3（B）** ユーザー ID を `/data/user/<id>/…` のパスから取る方法（UI :73）は、adoptable storage（`/mnt/expand/<uuid>/user/<id>/…`）で外れる。
  - 改善: `android.os.Process.myUid() / 100000` で取る。
- **L-4（C）** `adb shell date +%m-%d\ %H:%M:%S.000`（UI :77）は、`subprocess` の argv で渡すと adb が空白で連結するので、端末側で引数が 2 つに割れる。
  - 改善: `adb shell "date '+%m-%d %H:%M:%S.000'"` と書く。
- **L-5（C）** `background` の範囲 100〜10000 ms（:93）と、1 命令の打ち切り 10 秒（:148）がぶつかる。
  - 改善: 上限を下げるか、`background` を打ち切りの対象から外す。
- **L-6（C）** 向きの設定を「`rotate` の命令のとき」に覚える（:125）と、2 回目の `rotate` で元の値が上書きされる。
  - 改善: 「最初の 1 回だけ覚える」と書く。
- **L-7（C）** 古い参照と文言が残っている。
  - Dialog v3 :289-291, :374, :459, :465 が「補助の設計書 v2」を指している。
  - 5.1 の `csharp.md` の行（:293）が「Android にも」のまま（R2-L-2 の残り）。
  - UI :159 の「上の 2 つと 3 つ目」は、「2 つ目と 3 つ目」の誤り。
- **L-8（B）** ゲートが前提にしている XML の形を確かめていない。
  - Unity の結果の XML で、`test-case` にカテゴリが `<property name="Category">` として出るかは裏が取れていない。
  - fixture に付けたカテゴリは `test-suite` の側に出る。
  - 改善: V に足し、判定は親の suite のカテゴリも継ぐ形にする。
- **L-9（B）** 後片付け（:426）が全テストで補助を呼ぶと、カテゴリの無いテスト（取り消し、同期の失敗、Mono の J-D9）でも `Do` が Ignore になりうる。そうなると、ゲートの skip の判定の外で skip に化ける。
  - 改善: 補助を使う後片付けは、カテゴリ `AndroidUiDriver` のテストだけにする。

## 総合評価

v2 の指摘 18 件（R2 の 13 件と Codex の 5 件）のうち、解決が 16 件、一部が 2 件（R2-M-3、R2-L-2）です。

A1 だった R2-H-1 は、取り出した時点で `Instance` に出す形にしたことで、旧の経路と観測上同じになりました。無効・破棄・作り直し・同期の失敗との順序・例外のあとの残りを一次資料で確かめ、一致しています。新しい A1 はありません。

残りは検証の穴（B）です。実装の前に、次の 3 点を直すことを勧めます。
- N-1: 既定の Profile が縦に固定されているので、回転のテストはゲートで必ず落ちる。
- N-2: `background` が PAUSED で `ok` を書くので、ネイティブの前面の判定（STOPPED）とずれ、E-8 が不安定になる。
- N-4: 配送のテスト（層 2a）がゲートで流れない。

N-3 と N-5 は、2b のテストの信頼性に関わります。テストが不安定になる経路（N-3）と、何も確かめずに緑になる経路（N-5）があるので、あわせて直すのが望ましいです。低優先度は文言と細部です。次は、この差分の確認だけで足りる見込みです。

---

## Codex の指摘

（`codex:codex-rescue` の出力。そのまま載せる）

新規の高 severity 指摘はありません。Round 2 の Codex 指摘は、#3・新規1〜3が修正済み、新規4の元の問題も修正済みですが、回転先の選び方に新しい低 severity 欠陥があります。

### Round 2 指摘の解決状況

以下の「ui-driver v3」は artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v3.md、「dialog v3」は artifact/android/dialog/designs/2026-10-11-android-dialog-design-v3.md です。

| 項目 | 判定 | v3 の根拠 |
|---|---|---|
| #3: helper 障害でも UI テストが Ignore され、ゲートが成功する | 修正済み | helper は `HELLO` 後に `READY` を返せなければ終了コード 3 です。端末準備・復元・前面復帰の失敗も非 0 です (ui-driver v3:149)。ゲートは PlayMode の実行ごとに helper を起動し、64-bit では `AndroidUiDriver` カテゴリの「存在」と skip 0 を要求します (ui-driver v3:152)。Mono ARMv7 ではカテゴリ判定だけを外し、helper の終了コードは引き続き判定します (ui-driver v3:159)。 |
| 新規1: Manager の無効化・破棄で配送停止／古い結果を別世代へ配送 | 修正済み | `AndroidNativeCallbackQueue` は Manager とは別の hidden `DontDestroyOnLoad` MonoBehaviour です。action は排出時に `AndroidDialogManager.Instance` を解決します (dialog v3:304)。Manager が無効でも pump は動きます。破棄済みなら旧 dispatcher と同じく新しい `Instance` を作って配送し、購読者がなければ結果は失われます (dialog v3:361)。無効化・破棄・フレーム境界の層2aテストも追加されています (dialog v3:416)。 |
| 新規2: `run-as` が user 0 固定 | 修正済み | `HELLO` に user ID を追加し、`<files>` の user ID と `run-as <package> --user <user>` を対応させています (ui-driver v3:67)。READY 前に host 書込み→Player 読取り・削除→host 確認の probe を行います (ui-driver v3:134)。probe または `run-as` の失敗は終了コード 3 です (ui-driver v3:150)。この引数順は AOSP の run-as.cpp の構文とも一致します。 |
| 新規3: `stay_on_while_plugged_in` を元値に戻さない | 修正済み | `stay_on_while_plugged_in`、アニメーション3値、回転2値について、値と存在状態を保存します (ui-driver v3:114)。終了時は元値を戻し、元が absent なら `settings delete`、復元失敗は終了コード 1 です (ui-driver v3:127)。 |
| 新規4: `rotate` が既に目的方向の場合に完了しない | 元の問題は修正済み。ただし新規欠陥あり | `rotate` の成功条件は「実回転が指定値と一致」に変更されました。事前に `rotation` を読み、現在値とは異なる値を指定します (ui-driver v3:91、dialog v3:437)。ただし現在値 2 の場合だけ 0 を選ぶため、下記の新規指摘2があります。 |

#### Callback queue と旧 dispatcher の等価性

指定された観点では、v3 は旧配送を再現しています。

- 旧実装も action 実行時に `Instance` を評価していました (Packages/com.jonghyunkim.nativetoolkit/Runtime/Dialog/AndroidDialogManager.cs:124)。dispatcher 自体は Manager と別の `DontDestroyOnLoad` オブジェクトです (Runtime/Common/UnityMainThreadDispatcher.cs:24)。
- 旧 dispatcher は購読者例外を捕捉せず、dequeue 後の `Invoke` から例外が抜けます (UnityMainThreadDispatcher.cs:55)。v3 も例外を捕捉せず、残件を次フレームへ残します (dialog v3:362)。
- 旧経路は排出中ずっと enqueue と同じロックを保持します。そのため、Android callback から途中で追加された仕事は排出完了後になります (UnityMainThreadDispatcher.cs:43, 55)。v3 の「Update 開始時の `Count` 件だけ」は、この callback 経路について同じフレーム境界をロックなしで再現しています (dialog v3:362)。

### 新規の具体的欠陥

#### 1. `background` は「Activity が stopped」を保証していない

- severity: medium
- section: UI 補助 3.1 `background`、Dialog 7.3 E-8
- evidence:
  - helper は「Activity が `RESUMED` でない」だけで stopped と判定し、直ちに `ok` を返します (ui-driver v3:93)。
  - E-8 のテストは、その `ok` を受けた時点で Bridge を呼びます (dialog v3:438)。
  - ネイティブ側の前面判定は resumed ではなく「started かつ not stopped」です (native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/common/presentation/ForegroundActivityTracker.kt:15)。`onActivityPaused` では状態を変えず、`onActivityStopped` で初めて stopped にします (同ファイル:99)。
- impact: `PAUSED` だが未 `STOPPED` の短い区間で `ok` が返ると、Bridge はまだ foreground と判定します。そのため、E-8 が期待する `NOT_FOREGROUND` を再現できません (native-toolkit/android/android_library_capi/src/main/java/com/jonghyunkim/nativetoolkit/capi/jni/Foreground.kt:15、DialogBridge.kt:168)。
- concrete fix: `background` は単なる「not RESUMED」ではなく、対象 Activity の `STOPPED`／`mStopped=true` を `dumpsys activity` で確認してから `ok` を書く。可能なら `ForegroundActivityTracker.onActivityStopped` の logcat 到達も照合する。

#### 2. 現在回転が 2 の場合、90 度の向き変更にならない

- severity: low
- section: Dialog 7.3「回転」
- evidence:
  - v3 の選択規則は「現在が 0 なら 1、それ以外なら 0」です (dialog v3:437)。
  - したがって現在値 2 では 0 を選び、回転値は異なるものの差は2段階です。Round 2 が要求した「必ず異なる90度の値」を満たしません (reviews/2026-10-11-android-dialog-design-review-v2.md:348)。
  - このテストの対象は `orientation|screenSize` を伴う回転時に Activity が再生成されない契約です (dialog v3:109)。
- concrete fix: 目的値を `(current + 1) % 4` にする。これなら開始値 0〜3 のすべてで必ず1段階、90度だけ変更できます。
