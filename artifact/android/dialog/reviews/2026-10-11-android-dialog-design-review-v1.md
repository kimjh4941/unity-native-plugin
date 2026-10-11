# レビュー結果

- 日付: 2026-10-11
- 対象ファイル: artifact/android/dialog/designs/2026-10-11-android-dialog-design-v1.md（あわせて artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md）
- 機能名: dialog
- プラットフォーム: Android
- レビュアー: サブエージェント 1 本（この節から「総合評価」まで）と Codex 1 本（末尾の「Codex の指摘」）。どちらも出力をそのまま載せた

---

## 強み

- **構造体の表（4.1）は一次資料と一致する。** 6 つの構造体の大きさと全フィールドの位置を `StructLayoutTest.cpp`:25-94 と 1 行ずつ照らし、すべて一致した。`extern` の数 17（show 6、cancel 1、読み取り 10）も `Dialog.h`:137-172 と一致する。
- **旧の値の表（2 章）は 1.12.0 の実装と一致する。**
  - 肯定 / 否定: `OldFrag`:217-312
  - 閉じたとき: `OldFrag`:376-393（Multi の `?: BooleanArray(0)` を含む）
  - 旧の失敗: `OldBridge`:204-210（`"Failed to <method>: ..."` と `"Failed to cast context to FragmentActivity"`）
  - C# の既定値と `??=`: `AndroidDialogManager.cs`:251, 308-309, 368-369, 498-500, 560-563
- **見落としやすい点を出典つきで押さえている。**
  - `DISMISSED` のとき `button` が 0 になること（`DialogBridge.kt`:199）
  - 結果の所有権が受け手に渡ること（`Dialog.cpp`:106-113）
  - 入口で拒むと `release` が呼び出しスレッドで呼ばれること（`Dialog.cpp`:124-129）
  - 入力は戻る前に Java の配列へ写されること（`Dialog.cpp`:176-196, 357-369）
- **J-D4 の根拠が正しい。** 複数選択の `checked` は長さを持たず、`item_count` 個を読む（`Dialog.cpp`:364-368、`Dialog.h`:98）。旧で長さが合わないと、`OldFrag`:252/260 の `checkedItems!![which]` で範囲外になりうる。
- **J-D1 は Dialog に合っている。** 種類ごとのイベントで、呼び出しごとの状態が無い。そのため `user_data` に種類を入れれば、完了が関数の戻る前に来ても正しいイベントに届く（第 1 部 1.3 の「完了の時刻」、`Accept.h`:37 は受け付けた後に ID を書く）。
- **移動を別のコミットにして GUID を保つ手順**（5.1）は、Windows の v6 と同じ形。

## 改善点

### 高優先度

**H-1**
- 区分: A1
- 観点: (P) P1c
- 節: 5.1
- 問題点: Dialog だけを `Runtime/Android/Dialog/` に移す計画である。Android の Clipboard / Notification / Share は `Runtime/<Feature>/` に残るので、1 つの OS の中で 2 つの形が混ざる。5.1 で直す `common.md` の節は「Unity Bridge パターン > Android」だけで、混在を禁じる節に手当てが無い。
- 根拠:
  - `common.md`:339「Android / iOS / macOS は `Runtime/<Feature>/` のまま…移すまでは…1 つの OS の中で形を混ぜない」
  - review-document `workflow.md`:41（P1c）
  - 親課題 README:102「機能を 1 つずつ移す」
  - `common.md`:378 の `Runtime/<Platform>/Common/` の表に、Android の行が無い（スパイクで `Runtime/Android/Common/` は既にある）
- 改善提案: 次のどちらかを決め、5.1 に載せる。
  - (a) Windows と同じく、Android の 4 機能の移動（`git mv` と名前空間）を実装の前の 1 コミットでまとめて行う。接頭辞の無い Android の結果型（`common.md` の既知の逸脱の 11 件のうち Android の 10 件）をどう扱うかも書く。
  - (b) `common.md`:339 と P1c に「Android の C ABI 移行の間（段 3〜4）の一時的な例外」を書く。
  - どちらでも `common.md`:378 の表に `AndroidNativeToolkitCApi.cs` の行を足す。
  - 5.4 の新しい internal の型（`DialogAnswer`、`DialogKind`）は、`AndroidDialogCApi` の入れ子にするか `Android` 接頭辞を付けると明記する（P1）。

**H-2**
- 区分: A1
- 観点: (B)(N)
- 節: 基本情報「方針」、2 章、5.1
- 問題点: 「公開 API はメソッド 6 本」とあるが、公開メソッドの `Initialize()` が抜けている。中身（`AndroidJavaClass`、`set*Listener` 6 本）は移行で無くなるので、実装者は消すか残すかを自分で決めることになる。消しても、このリポジトリのコンパイルとテストは通る（呼ぶ側は `Awake` だけ）。利用者のコードだけが壊れる。
- 根拠: `AndroidDialogManager.cs`:75（`public void Initialize()`）、:85-109。旧の `getInstance` / `set*Listener` は第 2 部 8.3 の DL-01〜DL-07 で、C ABI に対応が無い。
- 改善提案: 公開 API の一覧に `Instance` と `Initialize()` を加える。
  - `Initialize()` は「何もしない（ネイティブが使えるかを `Debug.Log` するだけ）」として残す方針を J-D に足す。
  - 消すなら 5.3 に行を足す。

**H-3**
- 区分: A1
- 観点: (E)(N)
- 節: 4.2、4.4、6 章、5.3
- 問題点: 項目の配列に `null` の要素があると、移行後は次の経路で失敗する。
  - `AllocUtf8(null)` が NULL ポインタを入れる。
  - ネイティブが入口で `INVALID_PARAMETER` を返す。
  - 4.4 の行に当たり、「C# の構造体の誤り」として `LogError` と `"the request was rejected (INVALID_PARAMETER)."` を返す。

  呼び出し側の入力の誤りを内部の欠陥として報告することになる。6 章にも 5.3 にもこのケースが無い。旧はそのまま Java に渡しており、空の行が出たと思われる（**要検証**）。同じく、文字列の中の U+0000 は NUL 終端で切れる。旧の Java はそのまま運んでいた。
- 根拠:
  - `AndroidDialogManager.cs`:361, 422（配列が null か空かだけを検査）
  - `AndroidNativeToolkitCApi.cs`:85-87（`null` → `IntPtr.Zero`）
  - `Dialog.cpp`:147-153（`items[i] == nullptr` を拒む）
  - 第 1 部 5.10（C → Kotlin は NUL 終端）
- 改善提案: 次を加える。
  - C# の検査「要素に `null` がある」を足す。同期の失敗にする（文言を決める）か、`""` に置き換える。
  - E 行と層 1 のテストを足す。
  - 5.3 に「要素の `null`」と「U+0000 で切れる」の 2 行を足す。

**H-4**
- 区分: A1
- 観点: (T)
- 節: J-D2、5.4
- 問題点: 受け口は「dispatcher に積むだけ」とあるが、`UnityMainThreadDispatcher.Enqueue` はロックを取る。`Update` は、そのロックを持ったまま積まれた処理（購読者のコード）を実行する。そのため購読者が動いている間、Android の main は `Enqueue` で待たされる。これは第 1 部 1.3.2 の「main を長く塞ぐ処理（ほかのスレッドを待つこと）をしない」と申し送りの「コールバックの中で main を長く塞がない（ANR）」に反する。重い購読者がいれば UI スレッドが止まる。UnityMain の処理が Android の main を同期で待つ経路があれば、デッドロックになる（**要検証**）。旧の `AndroidJavaProxy` も同じ形だが、テストでは見えない。
- 根拠:
  - `UnityMainThreadDispatcher.cs`:45-48, 57-63
  - 第 1 部 1.3.2
  - 申し送り :115
  - Windows 側にも同じ注記がある（`WindowsClipboardManager.cs` の `QueueDelivery` 付近のコメント）
- 改善提案: 受け口は、Manager が持つロックの無い `ConcurrentQueue<DialogAnswer>` に入れるだけにする。UnityMain 側で dispatcher（または Manager の `Update`）から取り出して出す。`Runtime/Common` の dispatcher を直すなら全 OS に及ぶので、別課題として理由を書く。どちらにしても J-D2 に「受け口が待つのは managed の軽いロックだけ」と書く。

**H-5**
- 区分: A1
- 観点: (E)(N)
- 節: 5.3、2 章、J-D8
- 問題点:
  - (1) **5.3 から漏れている振る舞いの変更がある。** 宿主や Activity が壊れたとき、旧はイベントを出さなかった。新は `CANCELED_BY_SYSTEM` の失敗イベント `(null, …, false, "…closed by the system.")` を出す。方針（:11）は「変わるのは 5.3 だけ」と言っている。
  - (2) **旧の時刻の書き方が食い違う。** 2 章の表（:115-116）は「ブリッジ。後のフレーム」、J-D8（:248）と 5.3（:261）は「同期の失敗」と書いている。実際は、Kotlin のリスナー → `AndroidJavaProxy` → dispatcher なので、`Show*` の中では出ていない。
  - (3) 5.3 の「対になっていないサロゲート」の行が、ボタンの文字だけを挙げている。
- 根拠:
  - 新 `AndroidDialogFragment.kt`:441-452（`onDestroy` → `Canceled(HOST_DESTROYED)`）。`OldFrag` には `onDestroy` の報告が無い。
  - `AndroidDialogManager.cs`:127-130, 599-605
  - 第 1 部 5.10（Kotlin → C も U+FFFD に置き換える）
- 改善提案:
  - 5.3 に `CANCELED_BY_SYSTEM` の行を足す。
  - J-D8 と 5.3 の「同期」を「`Show*` が戻った後、dispatcher 経由（同じか次のフレーム）」に直し、2 章と揃える。
  - サロゲートの行を、全ての文字列（題名・本文・項目・ヒント・入力の戻り値）に広げる。

### 中優先度

**M-1**
- 区分: A2
- 観点: (E)(N)
- 節: 4.1、4.3
- 問題点: 4.1 は、文字列の読み取りを `ReadUtf8(ptr, ToByteCount(size))` で写すと書いている。ところが `ReadUtf8` は `IntPtr.Zero` に `""` を返す。そのまま使うと、否定ボタンの TextInput / Login が `null` ではなく `""` になる（旧は `null`: `OldFrag`:272, 306）。読み取りは `!UNITY_EDITOR` の側なので、層 1 では見つからない。
- 根拠: `AndroidNativeToolkitCApi.cs`:108-110、`Dialog.cpp`:471-474（値が無いと NULL、空なら長さ 0 の非 NULL）
- 改善提案: 4.1 / 4.3 に「`ptr == IntPtr.Zero ? null : ReadUtf8(...)`」と書く。層 2b の否定のテストで、`null` であることを厳密に確かめる。

**M-2**
- 区分: A2
- 観点: (T)(Q)
- 節: 3 章、4.4、7.3
- 問題点: ログの段階が決まっていない。
  - 既存の C# の検査の失敗は `LogError`（`AndroidDialogManager.cs`:239 ほか）。4.4 も、入口の `INVALID_PARAMETER` で `LogError` を出す。
  - 3 章は `common.md` の「結果として返す失敗は `LogWarning`」を引いているが、既存の `LogError` を変えるかを書いていない。
  - 層 2b の「同期の失敗: J-D3、空の題名」（:352）は、想定していない `LogError` で Test Framework に落とされる。
- 根拠: `common.md`:152、`csharp.md`:11-12（`LogWarning` の規則は Windows だけ）
- 改善提案: J-D を 1 つ足し、各失敗のログの段階を表にする。結果として返す失敗は `LogWarning` に下げることを推奨する。それを 5.3 と `csharp.md` に反映する。残す場合は、テストに `LogAssert.Expect` を書く。

**M-3**
- 区分: A2
- 観点: (C)(P)
- 節: 5.1、UI 補助 5 章
- 問題点: `Tests/PlayMode/AndroidDialogPlayerTests.cs` と `AndroidUiDriver.cs` のガードが `UNITY_ANDROID` だけになっている。Bridge の `extern` は `UNITY_ANDROID && !UNITY_EDITOR` の中にあるので、Editor（Android ターゲット）でコンパイルエラーになる。
- 根拠: 設計 5.1 :229、UI 補助 :85。Windows の前例は `WindowsDialogSamplePlayerTests.cs`:5 の `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`。Android の前例は `AndroidNativeToolkitCApiPlayerTests.cs` の外 `UNITY_ANDROID`・内 `!UNITY_EDITOR` の 2 段。
- 改善提案: ガードを `UNITY_ANDROID && !UNITY_EDITOR`（または 2 段）と明記する。

**M-4**
- 区分: B
- 観点: (Q)(A)
- 節: UI 補助 1 章、3.2
- 問題点: 15 秒で結果が来なければ `Assert.Ignore` にする設計になっている。この形だと、補助が途中で落ちたり固まったり、`uiautomator dump` が遅かったりしたときも skip になる。`verify_unity_android.sh` は `failed="0"` で PASS にするので、Dialog の層 2b が全部 skip でも緑になる。
- 根拠: UI 補助 :27, :75、`verify_unity_android.sh`:113-118
- 改善提案:
  - 補助は HELLO に ack のファイルを返す。ack が無いときだけ Ignore にし、ack の後の期限切れは Fail にする。
  - スクリプトが補助を起動した実行では、skip が 0 であることと、補助の終了コードを判定に加える。
  - テストの期限は「補助の期限（10 秒）＋ dump と adb の往復」より長く取り、それぞれの値の根拠を書く。

**M-5**
- 区分: B
- 観点: (A)
- 節: UI 補助 3〜6 章
- 問題点: 端末の操作に穴がいくつかある。
  - (1) 結果のファイルを `adb shell` で書くと、テストが中身の書かれる前のファイルを読みうる。
  - (2) `<番号>` が「テストの中で増やす整数」なので、前のテストの期限切れの後に遅れて書かれたファイルを、次のテストが読みうる。
  - (3) 画面の消灯を防いでいない。長い実行の途中で画面が消えると、Unity が止まる。
  - (4) 設定を戻すのは `SIGTERM` のときだけで、Ctrl-C（`SIGINT`）や異常終了では戻らない。
  - (5) `type` の後はソフトキーボードが出ている。そのため `back` の 1 回目はキーボードを閉じるだけで、ダイアログが閉じない（**要検証**）。
  - (6) `input text` はシェルを通るので、`& ; ( ' "` などを escape する必要がある。IME の自動修正・大文字化で文字が変わることもある（**要検証**）。
  - (7) Unity が毎フレーム描いている間に、`uiautomator dump` が「could not get idle state」で失敗しないか確かめていない（**要検証**。V-2 に足す）。
- 根拠: UI 補助 :47, :57, :66, :79-80, :96
- 改善提案:
  - (1) 一時ファイルに書いてから `mv` で置く。
  - (2) 番号はプロセス全体で増やし、HELLO で結果のフォルダを空にする。
  - (3) 起動時に `svc power stayon usb` をかけ、終了時に戻す。
  - (4) スクリプトに `trap … EXIT INT TERM` を張り、補助も `SIGINT` を扱う。
  - (5) `back` の前に `dumpsys input_method` でキーボードが出ているかを見て閉じる。
  - (6) 入力は英小文字と数字だけにし、escape の規則を書く。
  - (7) を V-5〜V-7 として足す。dump の置き場所は `/data/local/tmp` を推奨する。

**M-6**
- 区分: B
- 観点: (Q)(P)
- 節: UI 補助 7 章
- 問題点: 「依頼の行の組み立てを C# の純粋な関数にし、層 1 で固める」とある。しかし `AndroidUiDriver` は PlayMode のアセンブリにあり、EditMode のテストのアセンブリはそれを参照していない。このままでは層 1 で書けない。
- 根拠: `NativeToolkit.Runtime.Tests.asmdef`（references は `NativeToolkit.Runtime` と `NativeToolkit.Editor` だけ、`includePlatforms: Editor`）
- 改善提案: 純粋な関数の置き場所を決める。
  - 組み立てを `Tests/Runtime` 側に複製しない形にする。
  - たとえば行の組み立てを `Tests/PlayMode` の `#if UNITY_ANDROID || UNITY_EDITOR` の型にし、PlayMode（Editor 内、層 2a）で確かめる。
  - または層 1 をやめ、doctest と層 2b だけにする。

**M-7**
- 区分: B
- 観点: (Q)(T)
- 節: 7.3
- 問題点:
  - 「UnityMain 以外から `ShowConfirmDialog` を呼ぶ」テストは、目的を果たさない。UnityMain はもともと Android の main ではないので（1.4）、普通の呼び出しでも「戻る前に完了が来る」経路に乗る。しかもこの競合は決定的に起こせない。さらに、別のスレッドから呼ぶと、同期の失敗のイベントがそのスレッドで出る。`Instance` がまだ無ければ、getter が UnityMain 以外で GameObject を作ろうとする。この呼び方は公開 API が約束していない。
  - 取り消しのテストは、どのコールバックと `user_data` を使うかが書かれていない。Manager の受け口を使う場合、Manager の `Awake` が済む前は `s_dispatcher` が null になる。J-D2 ではその完了が `LogError` で捨てられる。
- 根拠: 設計 :66, :242, :345、`UnityMainThreadDispatcher.cs`:28-32
- 改善提案:
  - 前者は「UnityMain から呼び、`Show*` が戻る前にイベントが出ないこと、後でちょうど 1 回出ること」に置き換える。別スレッドからの呼び出しは、契約に入れないなら試さない。
  - 後者は、先に `Instance` を作ってから Manager の受け口と `user_data = Confirm` を使い、`ConfirmDialogResult` に `CANCELED` の文言が 1 回来ることを確かめると書く。
  - あわせて J-D2 に「`s_dispatcher` が null か破棄済みのときは `LogWarning` して捨てる」を書く。

**M-8**
- 区分: B
- 観点: (Q)
- 節: 7.2、7.3、6 章
- 問題点: 確かめる層が欠けている操作がある。
  - (1) 層 2a を「無い」としている。しかし Manager は `|| UNITY_EDITOR` になり、Editor の契約（J-D6: 検査の失敗は同期でイベント、通れば `LogWarning` だけ）は層 2a でしか確かめられない。Manager を作るテストは EditMode に書かない規則がある。Windows の前例は 2a を 12 本持つ。
  - (2) 「外側のタップ」と `cancelable=false`・`cancelableOnTouchOutside=false` の実機の確かめが無い。名前の向きが逆の値は、層 1 の組み立てで値を見るだけになっている。
  - (3) `NOT_FOREGROUND`（E-7）を「テストが進まない」として層 2b から外している。しかし止まるのは UnityMain だけで、managed のスレッドは動く。補助の `input keyevent KEYCODE_HOME` の後に、別のスレッドから Bridge を呼び、`am start` で前面に戻せば、dispatcher 経由で届く。
- 根拠: `common.md` の「テスト（TDD）」、`WindowsDialogManagerEditorTests.cs`（12 本）、設計 :338, :373
- 改善提案:
  - `AndroidDialogManagerEditorTests` を層 2a に足す。
  - 補助で外側のタップ（ダイアログの `bounds` の外の座標）と、Back で閉じないこと（`count` が 1 のまま）を足す。
  - E-7 を層 2b の候補にする。できなければ、手動に残す理由を具体的に書く。

**M-9**
- 区分: B
- 観点: (Q)(A)
- 節: 7.4
- 問題点: 手動の「回転（構成の変更で fragment が作り直されても完了が 1 回）」は前提が成り立たない。Unity の Activity と透明な宿主は、どちらも `orientation|screenSize` を `configChanges` に持つ。回転では作り直されない。
- 根拠: `Library/Bee/artifacts/Android/Manifest/unityLibrary/src/main/AndroidManifest.xml`:18、native-toolkit `android_library/src/main/AndroidManifest.xml`:52
- 改善提案: 「回転してもダイアログが残り、完了が 1 回」を層 2b で確かめるだけにする。`settings put system accelerometer_rotation 0` と `user_rotation` を使い、終了時に戻す。作り直しの経路は対象外と書く。

**M-10**
- 区分: B
- 観点: (Q)(N)
- 節: 7.1
- 問題点: 申し送りは「要求の ID が `ulong` であること」のテストを求めているが、7.1 に無い。
- 根拠: 申し送り :198
- 改善提案: 層 1 に反射のテストを足す。`ResultCallback` の 2 番目の引数と `ntk_dialog_cancel` の引数が `ulong` であることを確かめる。`extern` は Editor ではコンパイルされないので、delegate の型を見る。

**M-11**
- 区分: B
- 観点: (E)(A)
- 節: 4.3、5.4
- 問題点: 結果をどこまでログに出すかが書かれていない。旧の Manager はパスワードと入力を `Debug.Log` に出していた。新しいライブラリは「秘密なので出さない」としている。今回は UI 補助が logcat を読むので、ログが成果物に残りやすい。
- 根拠: `AndroidDialogManager.cs`:216、`DialogBridge.kt`:208、`testing.md` 6 節
- 改善提案: 「入力・ユーザー名・パスワードは長さだけをログに出す」を 5.4 に書く。

**M-12**
- 区分: B
- 観点: (C)
- 節: 7.3
- 問題点: 64 ビットの層 2b（取り消し、UI）が、`--mono`（ARMv7）の実行でどう飛ばされるかが書かれていない。
- 根拠: `verify_unity_android.sh`:146-155
- 改善提案: `IntPtr.Size == 4` か `EnsureNativeAvailable() != Available` なら `Assert.Ignore` にし、32 ビットの専用テストだけを流す、と書く。

### 低優先度

**L-1**
- 区分: C
- 観点: (F)
- 節: 5.1
- 問題点: 変更ファイルの一覧から漏れているものがある。
  - `Tests/PlayMode/AndroidUiDriver.cs`
  - `scripts/android_ui_driver.py`
  - `scripts/verify_unity_android.sh`（補助の起動と停止）
  - `agent-rules/coding-rules/testing.md` 2 節の Android の行（adb の方式を足す。:158）と 7 節の適用状況
  - `csharp.md`:12（`LogWarning` の規則を Android にも）
  - `common.md`:339 / :378
  - `Documentation~/index*.md` の Android の `using JonghyunKim.NativeToolkit.Runtime.Dialog;`（index.md:146 ほか 6 か所）。ここは段 4 に回すなら、その旨を書く。
- 根拠: 左記
- 改善提案: 5.1 に行を足す。

**L-2**
- 区分: C
- 観点: (B)
- 節: 5.1 と 5.4
- 問題点: 5.1 は受け口を Bridge（`AndroidDialogCApi`）に置くとしている。一方 5.4 の受け口は Manager の `s_dispatcher` と `Instance` に触る。これでは Bridge が Manager に依存し、向きが逆になる。
- 根拠: `common.md`:11-31。Windows は受け口を Manager に置いている（`WindowsClipboardManager.cs`:3617 以降）。
- 改善提案: delegate の型と関数ポインタは Bridge、`[MonoPInvokeCallback]` の受け口と dispatcher は Manager に置く、と揃える。

**L-3**
- 区分: C
- 観点: (N)
- 節: 1.1、1.3
- 問題点:
  - `ntk_dialog_cancel` は OP-19 である（`Dialog.cpp`:454）。
  - 完了の表に `NOT_INITIALIZED`（`DialogBridge.kt`:231。第 2 部 11.1 では「起きない」）が無い。
  - 入口の並びの出典は、第 2 部 11 章では「第 1 部 1.3」である。
- 根拠: 左記
- 改善提案: 表記を直す。

**L-4**
- 区分: C
- 観点: (T)
- 節: J-D1
- 問題点: J-D1 は、親課題 A-1 と申し送り 4.3（:127）の「登録ごとに別の `user_data`」から外れている。外れる理由を明記していない。また Alert が 0 なので、`user_data` が NULL になる。
- 根拠: 左記
- 改善提案: 種類を 1〜6 にし、外れる理由（呼び出しごとの状態が無い、取り消しを公開しない）を 1 行書く。

**L-5**
- 区分: C
- 観点: (E)
- 節: 4.3
- 問題点: `DISMISSED` の `("Cancel", true, "")` は、`common.md`:207 の「`isSuccess == true` なら `errorMessage == null`」に反する。
- 根拠: 左記
- 改善提案: 互換のための例外として明記する。

**L-6**
- 区分: C
- 観点: (N)
- 節: 2 章 :119
- 問題点: 旧で長さの違う `checkedItems` を渡した場合は「記録が無い」とあるが、短い配列は `OldFrag`:252/260 で範囲外になり、main で例外になったと読める（**要検証**）。
- 根拠: 左記
- 改善提案: 推定として書き足す。

**L-7**
- 区分: C
- 観点: (C)
- 節: 全体
- 問題点: 機械照合が `new-file types…: no file names in 5.1` を SKIP した。5.1 の表の書き方を検査が拾えていない。コードの囲み（:277）には言語の指定が無い。
- 根拠: `check_design_consistency.py` の出力
- 改善提案: 表の形と、` ```csharp ` の指定を直す。

## 不足項目

- 公開の `Initialize()` と `Instance` の扱い（H-2）
- 項目の `null` の要素と、U+0000 のエラーケース（H-3）
- 受け口が待たない形（ロックの無い列）と、dispatcher が null / 破棄済みのときの扱い（H-4、M-7）
- 5.3 の `CANCELED_BY_SYSTEM`、旧の時刻の訂正、サロゲートの範囲（H-5）
- NULL と `""` を区別する読み取りの規則（M-1）
- ログの段階の表（`LogError` / `LogWarning`）と、入力値をログに出さない規則（M-2、M-11）
- 層 2a のテスト、外側のタップと `not_cancelable` の層 2b、`NOT_FOREGROUND` の自動化の検討、`ulong` のテスト、32 ビットで飛ばす条件（M-8、M-10、M-12）
- UI 補助の HELLO の ack、skip の判定、原子的な書き込み、番号の衝突、画面の点灯、`trap`、IME と Back、`input text` の escape、V-5〜V-7（M-4、M-5）
- 変更ファイルの一覧の漏れ（L-1）

## 総合評価

一次資料との照合（構造体の配置、`extern`、入口の検査、完了のエラー、旧の値の表）はおおむね正確です。C ABI の取り込み方として土台はできています。

ただし、A1 が 5 件あります。
- P1c（1 つの OS の中で形が混ざる）
- 公開 `Initialize()` の扱い
- 項目の `null` の要素
- dispatcher のロックによる main の待ち
- 5.3 の漏れ（`CANCELED_BY_SYSTEM`）

この 5 件を直して再レビューが必要です。

UI 補助の設計書は、方式（adb の `uiautomator dump` と `input`）の選び方には根拠があります。一方で、補助がいない・壊れたときに skip が緑になる点と、端末の操作の穴（M-4、M-5）があります。このままだと、Dialog の層 2b が実際には何も確かめずに通りうるので、実装の前に直すことを勧めます。

---

## Codex の指摘

（`codex:codex-rescue` の出力。そのまま載せる）

指定された設計書を一次資料と照合した結果、具体的な欠陥は以下の9件です。ファイルは変更していません。

## 指摘

### 1. native の `NULL` と空文字を区別できない

- severity: medium
- 対象: Dialog 設計 4.1「DllImport の署名と構造体」、4.3「結果の変換」
- 証拠:
  - 設計は返却文字列を既存の `ReadUtf8` で読むとしています。[設計:163](artifact/android/dialog/designs/2026-10-11-android-dialog-design-v1.md:163)
  - `ReadUtf8` は `IntPtr.Zero` と長さ0を両方 `""` に変換します。[AndroidNativeToolkitCApi.cs:108](Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeToolkitCApi.cs:108)
  - 一方、否定ボタンでは TextInput の入力値、Login の username/password は native 側で未設定になり、reader は `NULL` を返します。[Dialog.cpp:471](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Dialog/Dialog.cpp:471)、[Dialog.cpp:508](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Dialog/Dialog.cpp:508)
  - 設計上の期待値は `null` です。[設計:191](artifact/android/dialog/designs/2026-10-11-android-dialog-design-v1.md:191)
- 影響: TextInput/Login の否定結果が旧契約の `null` ではなく `""` になる。
- 修正: `ptr == IntPtr.Zero ? null : ReadUtf8(ptr, size)` の nullable reader を仕様化し、`NULL` と「非NULL・0バイト」を別々に層1テストする。

### 2. off-main では show の復帰前にイベントが発火し得る

- severity: medium
- 対象: Dialog 設計 5.4、7.3
- 証拠:
  - 設計は dispatcher に積むため「手順5〜7の間にイベントは出ない」と断定しています。[設計:273](artifact/android/dialog/designs/2026-10-11-android-dialog-design-v1.md:273)
  - C ABI は main 以外からの呼び出しでは、関数復帰前の完了を明示的に許します。[C ABI設計:148](/Users/jonghyunkim/Desktop/native-toolkit/artifact/topics/android-c-abi/designs/2026-10-04-android-c-abi-c-abi-design.md:148)
  - dispatcher の `Update` は別の UnityMain 上で並行してキューを排出できます。[UnityMainThreadDispatcher.cs:43](Packages/com.jonghyunkim.nativetoolkit/Runtime/Common/UnityMainThreadDispatcher.cs:43)、[同:55](Packages/com.jonghyunkim.nativetoolkit/Runtime/Common/UnityMainThreadDispatcher.cs:55)
  - off-main テストはスレッドだけを検査し、復帰順を検査しません。[設計:345](artifact/android/dialog/designs/2026-10-11-android-dialog-design-v1.md:345)
- 影響: 呼び出し後に設定する managed 状態へ callback が依存すると競合する。テストもこの順序違反を見逃す。
- 修正: 復帰前イベントを許容する契約へ直し、受け口を呼び出し後の状態に依存させない。復帰後を保証するなら、per-call の return gate が必要。

### 3. helper 障害が `Assert.Ignore` となり、UI 未検証でもゲートが成功する

- severity: high
- 対象: helper 設計 1、3.2
- 証拠:
  - 無応答をすべて「補助がいない」として Ignore します。[helper設計:27](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:27)、[同:75](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:75)
  - 現行ゲートは `failed="0"` だけで成功し、`skipped` / `inconclusive` を拒みません。[verify_unity_android.sh:113](scripts/verify_unity_android.sh:113)
- 影響: helper の起動失敗、クラッシュ、logcat解析失敗がすべて緑になる。
- 修正: session nonce 付き `HELLO/READY` を必須化する。READY 後の無応答は `Assert.Fail`、対話的な手動実行だけ Ignore とする。ゲートでも期待UIテスト件数と `skipped=0` を検査する。

### 4. 結果ファイルに session 分離と atomic write がない

- severity: high
- 対象: helper 設計 3、3.2、5
- 証拠:
  - 識別子は「テストの中で増やす整数」だけです。[helper設計:47](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:47)
  - 永続ディレクトリに番号だけのファイルを置きます。[同:57](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:57)、[同:75](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:75)
  - C# はファイルが現れた時点で読み、削除します。[同:87](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:87)
- 影響:
  - 前回中断時の `ok` を新しい要求の結果と誤認できる。
  - shell のリダイレクトがファイルを作成してから内容を書く間に、C# が空・途中の内容を読める。
- 修正: 実行ごとの UUID ディレクトリと単調増加IDを使用する。HELLO 時に残留物を除去し、結果は一時ファイルへ書いて close 後に同一ディレクトリ内で rename する。

### 5. Android 11+ 用 `run-as` fallback の読取先が定義されていない

- severity: medium
- 対象: helper 設計 3、8 V-1
- 証拠:
  - Player は external files directory を監視しますが、fallback は「内部の領域に書く」とだけ規定されています。[helper設計:54](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:54)、[同:57](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:57)
  - C# の監視先を内部領域へ切り替える handshake がありません。[同:85](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:85)
  - Android 11 以降では app-specific external directory へのアクセス制約が強化されています。[Android公式資料](https://developer.android.com/about/versions/11/privacy/storage#other-apps-data)
- 影響: fallback が選ばれる端末では、host が内部へ書いても Player は external path を待ち続ける。
- 修正: 最初から `run-as` と内部 files/cache に統一するか、READY で transport/path を交渉し、Player 側も同じパスへ切り替える。開始時に round-trip probe を行う。

### 6. `back` がダイアログ表示前に Unity Activity へ届く競合

- severity: medium
- 対象: helper 設計 3.1、Dialog 設計 7.3
- 証拠:
  - `back` は表示対象を待たず即座に `KEYCODE_BACK` を送ります。[helper設計:63](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:63)
  - show は非同期で、UI処理は後から Android main に積まれます。[Dialog設計:65](artifact/android/dialog/designs/2026-10-11-android-dialog-design-v1.md:65)
  - Back による dismissal を自動テスト対象にしています。[同:347](artifact/android/dialog/designs/2026-10-11-android-dialog-design-v1.md:347)
- 影響: Back がダイアログではなく下の Activity を閉じ、テストがタイムアウトまたは別挙動になる。
- 修正: `back <selector>` として `alertTitle` 等の出現後に送るか、必ず `wait` のACK後に `back` を実行する。

### 7. logcat protocol に長さ上限・分割規則がない

- severity: medium
- 対象: helper 設計 3、6、8 V-4
- 証拠:
  - 任意長の Base64 を1行で運ぶ設計です。[helper設計:41](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:41)
  - parser は1行ずつ処理します。[同:93](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:93)
  - 分割・切詰めは仕様化せず V-4 の試行に委ねています。[同:111](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:111)
  - AOSP の log payload 上限は4076バイトで、超過は切り詰められます。[AOSP `logger.h`](https://android.googlesource.com/platform/system/core/+/a8be627/include/log/logger.h)
- 影響: 長い selector/text が途中で切れ、要求消失または誤解析になる。
- 修正: protocol のUTF-8バイト上限を固定し、送信側で拒否する。長文が必要なら session/id/part-count/checksum を持つ chunk framing を定義する。

### 8. `input text` の「ASCII対応」が広すぎる

- severity: medium
- 対象: helper 設計 3.1
- 証拠:
  - 設計はASCII全般を扱えるとして、空白だけを `%s` に変換します。[helper設計:66](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:66)
  - AOSP の `input text` は `%s` 自体を空白に変換し、仮想キーボードで生成可能な文字に依存します。[AOSP `Input.java`](https://android.googlesource.com/platform/prebuilts/fullsdk/sources/android-30/+/refs/heads/androidx-main-release/com/android/commands/input/Input.java)
  - さらに adb remote shell では `$`, `&`, quotes, backslash 等の quoting が必要です。
- 影響: ASCIIの `%s` や shell metacharacter を含む入力が変形・欠落し得る。
- 修正: 今回のfixtureを `[A-Za-z0-9._-]+` 等に限定して範囲外を明示失敗にするか、remote-shell escaping と `%` の変換規則を実装し、境界テストを追加する。

### 9. device settings の復元が正常な SIGTERM 経路に限定されている

- severity: medium
- 対象: helper 設計 4、6
- 証拠:
  - 3つの animation scale を変更します。[helper設計:80](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:80)
  - 復元は verify script が送る SIGTERM のみ規定されています。[同:96](artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v1.md:96)
  - 現行 verify script には helper cleanup 用の `trap` がありません。[verify_unity_android.sh:44](scripts/verify_unity_android.sh:44)
- 影響: Unity失敗、SIGINT/SIGHUP、helper例外、起動途中の失敗で animation scale が0のまま残る。また元値が未設定のとき、文字列 `"null"` を `put` すると元状態へ戻らない。
- 修正: Python の `finally` と TERM/INT/HUP handler、親shellの EXIT trapを併用する。元値の存在状態も保存し、未設定なら `settings delete global <key>` で戻す。

## No finding

以下は一次資料と一致していました。

- 6構造体のサイズ・全offset。[Dialog.h:55](/Users/jonghyunkim/Desktop/native-toolkit/dist/1.13.0/android/include/NativeToolkitC/Dialog.h:55)、[StructLayoutTest.cpp:25](/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi_test/src/main/cpp/tests/StructLayoutTest.cpp:25)
- `uint64_t → ulong`、`size_t → UIntPtr`、`int32_t → int`、callback引数順、cancel署名。
- `not_cancelable` と `not_cancelable_on_touch_outside` の反転。
- result handle を managed 値へコピー後、必ず `ntk_dialog_result_free` する所有権方針。
- Android main で受け、UnityMain dispatcher へ渡す方針。
- callback 全体の例外封じ込めと、`static readonly delegate`＋`MonoPInvokeCallback` によるIL2CPP寿命。
- C ABIエラー値、入口拒否と非同期完了の分類、未知値を含むエラーmapping。
- 旧1.12.0の肯定・否定・Back/外側タップ結果表。`OldFrag:217-312,372-393` と一致。
- framework `AlertDialog` を通常の `uiautomator dump` で読む基本方針。固定dumpファイルを使う場合は、毎回古いファイルを削除し、新しいXMLが生成されたことを確認する実装が前提です。

