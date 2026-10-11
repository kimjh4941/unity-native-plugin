# レビュー結果

- 日付: 2026-10-11
- 対象ファイル: artifact/android/share/designs/2026-10-11-android-share-design-v1.md
- 機能名: share
- プラットフォーム: android
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない。Codex の ID は統合のときに付けた）

---

## 1. サブエージェント（Claude）

## 強み

- C ABI についての記述は、確かめた範囲ではすべて一次資料と合っていた。対象は 12 関数、17 個のエラー値、3 つの構造体の大きさと位置（`StructLayoutTest.cpp:118-141` と一致）、入口の検査の順（初期化の確認が後。`Share.cpp:319-328`、P2:613）、`out_request_id` を先に 0 にすること（`Share.cpp:453`）、Direct Share の同期と `category` の既定（`Share.cpp:499-534`）、取り消しが未初期化でも `NONE` を返すこと（`Share.cpp:485`）、AP-7 の世代、AP-17 の対応表、`ShareBridge.kt` の開く処理と前面の判定の順、FileProvider の 3 つの範囲（`native_toolkit_share_file_paths.xml`。1.12.0 と同じ）。
- 1.x の文言（parser の `require` の文言、`executeOperation` の写しの表）を 1.12.0 のタグから正しく写している。
- 完了とイベントの扱いは、Handoff 4.3 の要求をすべて押さえている。具体的には、`user_data` を呼ぶ前に表に入れる、表から消すのは `release` だけ、要求の ID を `ulong` で持つ、所有権ごと渡される `ntk_string` を `_free` する、どのスレッドでも完了を待たない。
- 「C# の検査の失敗も Editor の失敗も、取り出し口を通して後のフレームに出す」（J-S2）とした。1.x の非同期の時点を保つという方向は正しい。
- 本文と subject のログを長さだけにする（J-S10）。UI の補助が Sharesheet の画面の構造を書き出さないという決まりも、native-toolkit の規則に合わせて先に決めている。
- 移行の前に 1.x のまま UI テストを流して記録する手順（5.5 の手順 3）があり、推定と書いた箇所を実測で置き換える流れになっている。
- 機械照合（`scripts/check_design_consistency.py`）はすべて OK だった（SKIP を除く）。

## 改善点

### 高優先度

**R1-1 per-call の枠を読む時点が 1.x と違う。7.2 のテストがその違いを「1.x と同じ」として固めてしまう**
- 節: 2.2、J-S5、5.4 の手順 3・4・12、7.2 の「per-call の枠」
- 問題:
  - 計画では、per-call の枠をいつも UnityMain の取り出しの時点（`RaiseOperation`）で読んで消す。
  - 1.x は、結果を作った時点で枠を読んで消していた。同期の失敗（Editor、`pluginInstance` が null、`Call` の例外）では呼んだその場で読み、ネイティブの結果では Android の main の上、dispatcher に積む前に読んでいた。コードのコメントにも「後から同じ操作を呼んでも、この結果が別のコールバックを呼ばないように」とある。
  - そのため、Editor で同じ操作を同じフレームに 2 回呼ぶと、1.x では cb1 と cb2 がそれぞれ自分の失敗を受けた。計画のとおりに作ると、1 回目の結果が cb2 を呼び、cb1 は一度も呼ばれない。
  - 7.2 はこの形を「1.x と同じ」として層 2a で固めるので、テストは通り、振る舞いだけが変わる。
  - 加えて、C# の検査の失敗、入口の拒否、同期の Direct Share は呼んだ時点で積まれる。一方、先に呼んだ開く操作の完了は後から来る。このため、操作をまたいだ `ShareOperationCompleted` の順が 1.x（Kotlin の main を通るので呼んだ順）と変わりうる。この差は 5.3 に無い。
- 根拠:
  - `AndroidShareManager.cs:331`（枠への書き込み）、`:333-336`（同期の失敗は `FireOperationResult` をその場で呼ぶ）、`:399-406`（読んで消す時点とコメント）
  - 1.x の `UnityAndroidShareManager.kt:317-321`（parser を含め、すべて main の上で呼んだ順に処理する）
  - 計画の 7.2:495
- 区分: A1
- 修正案:
  - 同期に決まる結果（C# の検査、Editor、ネイティブが使えない、入口の拒否、C# の例外）は、呼んだスレッドでロックの中で枠を取り出し、取り出したデリゲートを積む処理に閉じ込める（1.x の `FireOperationResult` と同じ）。
  - 非同期の完了は、Handoff 4.2 に従って取り出しの時点で読む。1.x（Android の main で読んでいた）との狭い差は 5.3 に書く。
  - 同期の Direct Share は、呼んだ時点で取る形と取り出しの時点で読む形のどちらにするかを決める。どちらにしても、1.x の「次の結果が後の per-call を呼ぶ」との違いを 5.3 に書く。
  - 7.2 の期待値を、同期の失敗（Editor）では「各呼び出しが自分の per-call を受ける」に直す。
  - 5.3 に「結果の順（同期に決まる失敗が、先に呼んだ開く操作の完了より先に出ることがある）」の行を足す。

**R1-2 Editor の `CancelPendingShareCallback` の期待値が矛盾している（7.2 は成功、J-S3・E-6・1.x は失敗）**
- 節: 4.3、J-S3、J-S13、E-6、7.2:498
- 問題:
  - 7.2 は「`CancelPendingShareCallback`: 成功が後のフレームに出て」と書いている。一方、層 2a は Editor で流し、J-S3 と E-6 は Editor では 9 操作すべてが `<op> could not be started.` になるとしている。
  - 1.x も、Editor では `TryPrepareCall` が偽になり失敗を返した。
  - 7.2 に合わせて実装すると、Editor での取り消しが成功に変わる。テストは通るので、振る舞いの変化に気づけない。
  - Editor でも `onSelected` を先に消すか（1.x は `CallOperation` の前に消していた）が、5.4 の手順の順からは読み取れない。
- 根拠:
  - `AndroidShareManager.cs:322-326`（`_pendingShareSelectedCallback = null` の後で `CallOperation`）、`:358-362`
  - 計画の 7.2:498、J-S3:360、E-6:456
- 区分: A1
- 修正案:
  - 7.2 の期待値を「Editor では失敗 `cancelPendingShareCallback could not be started.`、`onSelected` は消える」に直す。
  - J-S13 に、Editor・ネイティブが使えないときの扱い（1.x は `could not be started.`）と、「`onSelected` を消すのは Editor の判定より前」を書く。

**R1-3 テスト用の共有先（`<activity-alias>`）の前提が、このプロジェクトの実際と合っていない。Direct Share の表示も層 2b で確かめられる**
- 節: 1.7、5.1 の「テスト用の共有先」、7.3、7.4、8.3、8.4
- 問題:
  - このプロジェクトには、もう `Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/AndroidManifest.xml` がある。そこで `UnityPlayerGameActivity` に `ACTION_SEND`（`*/*`）の intent-filter と `android.app.shortcuts` を宣言しており、`res/xml/shortcuts.xml` に `<share-target>`（`android.shortcut.conversation`）がある。直近の Android のビルドでも、`settings.gradle` にこのモジュールが含まれている。したがって、次のことが言える:
  - (a) アプリ自身がアプリ名で Sharesheet に出ているはずなので、alias を足すと同じアプリが 2 つ並ぶ。alias を足さずに、アプリ自身の項目を押せば足りる可能性が高い。
  - (b) `Assets/Plugins/Android/` の下に置いたものは、このプロジェクトのすべての Android のビルド（サンプル用の Android™ Profile を含む）に入る。「テスト用の Player だけ」にはならない。
  - (c) `useCustomMainManifest: 0` なので、`AndroidManifest.xml` を `Assets/Plugins/Android/` の直下に置いても使われない。`.androidlib` にする必要がある。
  - (d) `<share-target>` は宣言済みなので、「Direct Share が Sharesheet に出ること」を手動に回す理由（7.3、7.4、8.4）が成り立たない。
  - (e) 「作り直されないか」について、生成された manifest では `launchMode="singleTask"` になっている（alias の起動モードは元の Activity に従う）。
  - (f) マニュアルは `<share-target>` の設定をもう説明している（docs/1.11.0/share.ja.md:76-104）。
- 根拠:
  - 上の androidlib の `AndroidManifest.xml:9-18`、`res/xml/shortcuts.xml:3-6`
  - `ProjectSettings/ProjectSettings.asset:262`（`useCustomMainManifest: 0`）、`:82`（`androidApplicationEntry: 2`）
  - `Library/Bee/artifacts/Android/Gradle/settings.gradle`（`include 'unityLibrary:com.jonghyunkim.nativetoolkit.androidlib'`）
  - `Library/Bee/artifacts/Android/Manifest/unityLibrary/src/main/AndroidManifest.xml:18`（`singleTask`）
- 区分: B（中心の検証手段）
- 修正案:
  - 8.3 の案を「既存の androidlib の SEND の filter により、アプリ自身が共有先に出る。その項目（アプリ名）を押す」に改め、5.5 の手順 2 では「アプリ自身が出るか、一覧の何番目か」を確かめる。
  - 別のラベルが必要な場合は、既存の androidlib に alias を足すことになるので、それがすべてのビルドに入ることと、外部のアプリから SEND で Unity の Activity を起動できることを、意図した変化として書く。
  - Direct Share は層 2b で「Sharesheet の direct share の行に short label が出る」を試す。OS のキャッシュで遅れるなら理由を書いて手動に戻す。
  - 1.7 と 8.4 の「アプリ側が宣言する必要がある」には、「このリポジトリのサンプルは宣言済み」を書き添える。
  - 要確認: Sharesheet が呼び出し元のアプリ自身の共有先を一覧から除くかどうか。

**R1-4 Chooser Action の重複を除く処理と、画像として読めないアイコンの扱いについて、「1.x と同じ」とした記述が誤っており、5.3 にも漏れがある**
- 節: 1.6、2.3、4.2.1 の手順 3、5.3:382
- 問題:
  - (a) 1.x の parser とライブラリは、表示するアクションの重複を除いていなかった。`buildChooserActions` は、ラベルとアイコンが読めるアクションを ID の重複に関係なくすべて表示している。`distinct` を通すのは、受け手（receiver）に登録する action 文字列だけだった（`normalizeActionIds`）。4.2.1 の手順 3 は 2 つ目以降を表示から消すので、表示が変わる。C ABI が重複を入口で拒む（`Share.cpp:160-162`）以上避けられないが、5.3 に行が無い。
  - (b) 2.0.0 は、アイコンを画像として読む処理を SDK の判定より前に行う。そのため、アクションが表示されない API 33 以下でも、読めないアイコンがあると Share 全体が `INVALID_CHOOSER_ACTION` で失敗する。1.x は API 33 以下ではアクションの処理そのものを飛ばしていた。5.3:382 と 1.6 の「API 33 以下では付けない」だけでは、この影響の範囲が伝わらない。
- 根拠:
  - 1.12.0 の `ShareRepositoryImpl.kt:226-229`（API 34 未満は return）、`:245-266`（重複を除かず、読めないものだけ飛ばす）、`ShareChooserActionInputs.kt:16-24`
  - 2.0.0 の `ShareRepositoryImpl.kt:51-59`（読む処理の後に SDK の判定）
- 区分: A1
- 修正案:
  - 2.3 の「ID は空白と重複を除き」を「受け手に登録する ID からだけ除く。表示は重複も含めてすべて」に直す。
  - 5.3 に「同じ `intentAction` のアクションは、2 つ目以降が表示されない」の行を足す。
  - 5.3:382 に「API 33 以下（アクションが出ない端末）でも Share 全体が失敗する」を書き足す。
  - 任意: C# が PNG・JPEG・WebP・GIF・BMP の先頭のバイト列を見て、明らかに画像でないものを捨てれば、1.x の「黙って飛ばす」に近づけられる（SH-06 の「保てない」を狭める）。

### 中優先度

**R1-5 必須の値が null のときの 1.x の振る舞いを「1.x と同じ」と書いているが、実際は違う**
- 節: 4.2 の表（`filePath`、`filePaths`、`ids`、Direct Share の `id` / `label` / `iconBase64`、Chooser Action の要素と `label` / `iconBase64`）、5.3
- 問題: 1.x の `AndroidShareJsonBuilder` は null を JSON の `null` にし、配列の null は C# の例外にしていた。その結果、1.x の振る舞いは次のとおりだった:
  - `filePaths` / `ids` が null: `BuildStringList` で `NullReferenceException` が呼び出し側に出た
  - `chooserActions` の要素が null: `NullReferenceException` が呼び出し側に出た（`Register` の後）
  - `filePath` が null: `getString` が `"null"` を返し、`File not found: null` になった
  - Direct Share の `id` / `label` が null: `"null"` の名前で登録された
  - `iconBase64` が null: `"null"` を Base64 として解くと 3 バイトになり、画像として読めず `Invalid icon data ...` になった
  - アクションの `label` が null: ラベル `"null"` で表示された

  計画はこれらを「`... is required`（1.x と同じ）」や「捨てる（1.x の parser と同じ）」と書いており、5.3 に行が無い。
- 根拠:
  - `AndroidShareJsonBuilder.cs:23,40,63,84-88,121-126,134-141,155-157`
  - 1.12.0 の `UnityShareJsonParser.kt:14,31,50,66-71,92-94,103-105`
  - Android の `org.json` の `getString` / `optString` は `JSONObject.NULL` を `"null"` にする（要確認。計画自身も本文の null について同じ推定を置いている）
- 区分: A1
- 修正案:
  - 4.2 の「1.x との関係」の列を、null の行だけ上の実際の振る舞いに直す。
  - 5.3 に「必須の文字列が null」「配列そのものが null」「Chooser Action の要素・ラベル・アイコンが null」の行を足す（推定のものは手順 3 で実測する）。

**R1-6 J-S12 の「受け付けていなければ 0 にする」が、ネイティブの状態と 1.x の振る舞いのどちらにも合わない**
- 節: J-S12、J-S14
- 問題:
  - F-8 が入口で拒まれた場合（未初期化、確保の失敗、main に積めなかった）、要求は Kotlin に届かない。そのため、前の `ShareWithCallback` の待ちはネイティブ側にそのまま残っている。
  - ここで C# 側の「直前の ID」を 0 にすると、前の待ちの選択が来ても C# が捨ててしまう。
  - 1.x では、Kotlin に届く前の失敗（parser）なら前の待ちが残り、選択が来れば `ShareCallbackReceived` が出た（`onSelected` だけ消える）。
  - 計画の中でも、C# の検査で失敗したときは ID を変えないので、扱いが揃っていない。
- 根拠:
  - `Share.cpp:453-458`
  - `ShareBridge.kt:192-196`（印を置き換えるのは開けた後だけ）
  - 1.12.0 の `UnityAndroidShareManager.kt:273-281`（parser は `register` より前）
  - P2:41
- 区分: A1
- 修正案: 戻り値が `NONE` 以外のときは、直前の ID を変えない（前の値のまま）。J-S14 にも同じことを書く。

**R1-7 選択の照合を取り出しの時点で行うと、1.x が届けていた選択を捨てることがある。ID のフィールドのスレッドの約束も書かれていない**
- 節: J-S12、J-S13、J-S16、5.4 の選択の受け口
- 問題:
  - (a) 選択のイベントを取り出し口に積んだ後、取り出される前に `ShareWithCallback`（ID が新しくなる）か `CancelPendingShareCallback`（ID が 0 になる）が呼ばれると、その選択は捨てられる。1.x は C# で照合せず、`ShareCallbackReceived` を出していた（`onSelected` は消えていても）。
  - アプリが後ろにあって Unity が止まっている間に選択が届き、前に戻った最初のフレームで呼ぶ、という形で起きうる。
  - 古い選択はネイティブがもう捨てている（`selectionIds.remove`、coordinator の印の照合）。C# の照合は二重になっているうえに、時点が遅い。
  - (b) ID は呼んだスレッドが書き、UnityMain が読む（J-S16 で、どのスレッドから呼んでもよいとした）。J-S5 のロックがこのフィールドを守るのかが書かれていない。複数のスレッドが同時に F-8 を呼ぶと、ネイティブの待ちと C# の ID の順が食い違いうる（ID は単調に増えるが、main に積む順とは一致しない）。
- 根拠:
  - `AndroidShareManager.cs:422-431`
  - `ShareBridge.kt:268-273`
  - 2.0.0 の `ShareCallbackCoordinator.kt:177-190`
  - `Registry.cpp:37,95`、`Accept.h:25-30`
- 区分: A1
- 修正案:
  - 照合を受け口（Android の main）で行う。ID を `Interlocked.Read` で読むだけで、状態は変えない。その結果だけを積む。この形を採らないなら、上の差を 5.3 に書く。
  - ID の読み書きは J-S5 と同じロックで行うと書く。
  - 複数のスレッドから同時に `ShareWithCallback` を呼んだときは、後に書いた方が勝ち、選択が届かないことがある、と XML コメントと 5.3 に書く。

**R1-8 Base64 の解き方の違い（Android の `Base64.DEFAULT` と .NET の `Convert.FromBase64String`）が 5.3 に無い**
- 節: 4.2（Direct Share の `iconBase64`）、4.2.1 の手順 2
- 問題:
  - 1.x は、Chooser Action のアイコンも Direct Share のアイコンも Android の `android.util.Base64.decode(..., DEFAULT)` で解いていた。これはパディングの無い入力を受け付ける（要確認。AOSP の `Base64.Decoder` は、終わりの状態が 2 か 3 なら残りのバイトを出す）。
  - .NET の `Convert.FromBase64String` は、長さが 4 の倍数でないと `FormatException` を投げる。
  - そのため、パディングの無いアイコンは、1.x では表示・登録されたが、2.0.0 ではアクションが黙って消え、Direct Share は `Invalid icon data ...` になる。
- 根拠:
  - 1.12.0 の `UnityAndroidShareManager.kt:237-241`、`ShareRepositoryImpl.kt:254`
- 区分: A1（要確認）
- 修正案:
  - 1.x と同じ寛容さで解く関数（空白を除き、`=` で 4 の倍数まで埋めてから解く）を Bridge の純粋な関数にして、層 1 で固める。
  - 5.5 の手順 3 の 1.x の列に「パディングの無い Base64」を足して実測する。

**R1-9 P4: `AllocBytes` を `Runtime/Android/Common/` に置く根拠が無い**
- 節: 4.1、5.1（`AndroidNativeToolkitCApi.cs`）
- 問題:
  - `Runtime/<Platform>/Common/` には、そのプラットフォームの全機能（少なくとも複数の機能）が使うものだけを置く。
  - `const uint8_t*` を入力に取るのは `Share.h` だけで、Clipboard.h、Dialog.h、Notification.h には無い。
  - `AllocStringArray` は Dialog、Clipboard（`ntk_clipboard_copy_texts`）、Notification（`lines`）も使うので、共通部に置いてよい。
- 根拠:
  - `dist/1.13.0/android/include/NativeToolkitC/*.h` を grep した結果（`Clipboard.h:62`、`Dialog.h:82,96`、`Notification.h:221`）
  - workflow の P4
- 区分: A1（P 違反）
- 修正案: `AllocBytes` は `AndroidShareCApi` に置く。`AllocStringArray` を移す理由として、ほかの機能でも使うこと（上の行）を 5.1 に書く。

**R1-10 リスナーの登録のやり直し（J-S6）がスレッドで守られていない。二重に登録すると、イベントが 2 回届く**
- 節: J-S6、J-S16
- 問題:
  - 登録をやり直す口は `Initialize`（UnityMain）、`ShareText`、`ShareWithCallback`（どのスレッドからでも）の 3 つで、同時に走りうる。
  - `ntk_share_add_*_listener` を 2 回呼ぶと 2 つの登録ができ、Kotlin は登録ごとにイベントを配る。その結果、`ShareChooserActionTapped` と `ShareCallbackReceived` が 2 回ずつ出る。
- 根拠:
  - `Share.cpp:247-268`
  - `ShareBridge.kt:264,272`（`Ledger.idsOf(...)` のすべての登録に配る）
- 区分: A1（起きる条件は狭い）
- 修正案:
  - 登録をロックの中（または `Interlocked.CompareExchange` で 1 つのスレッドだけが行う形）にし、成功した登録のハンドルを保持すると書く。
  - 層 2b で、タップ 1 回に対してイベントが 1 回であることを数える（7.3 の Chooser Action の項）。

**R1-11 Manager を壊して作り直すと、前の Share のアクションが新しい Manager に届く。1.x では届かなかった**
- 節: J-S6、J-S15、5.3:395
- 問題:
  - 1.x の `OnDestroy` は `clearShareChooserActionListener` を呼び、受け手の登録（receiver）を外していた。そのため、それより後に前の Share のアクションを押しても通知は来なかった。
  - 計画ではリスナーを外さず、`_instance` に届ける。作り直した Manager には、前の Share（世代はまだ今のもの）のタップが届く。
  - 5.3:395 は「捨てる（同じ）」としているが、これは Manager がいない間の話で、作り直した後のこの差を含んでいない。
- 根拠:
  - 1.12.0 の `UnityAndroidShareManager.kt:127-134`
  - `AndroidShareManager.cs:123-136`
- 区分: A1（軽い）
- 修正案: `OnDestroy` で「次の `ShareText` までアクションのイベントを捨てる」印を立てる。この形にしないなら、5.3 に行を足す。

**R1-12 Sharesheet のプライバシーの決まりで、塞がれていない漏れ口がある**
- 節: 1.9、8.3 のプライバシー、5.1（`android_ui_driver.py`）
- 問題:
  - (a) 補助は、毎回の `uiautomator dump` を端末の `/data/local/tmp/ntk-ui-dump.xml` に残す（実行が終わっても消さない）。Sharesheet を読んだ後には、ほかのアプリの共有先と連絡先が端末に残る。
  - (b) `expect` が失敗すると、理由に読み取った属性の値が入り、結果のファイルを通してテストのログに出る。
  - (c) native-toolkit の `UiDriver.kt` の「見つからなかったら画面の文言をすべて logcat に出す」部分（`:229-231`）を写すと、決まりに反する。計画は「`UiDriver.kt:219-234` と同じやり方」と書いている。
- 根拠: `android_ui_driver.py:47,347-353,422-427`、native-toolkit の `UiDriver.kt:229-231`
- 区分: B
- 修正案: 8.3 に次の 3 つを足す。
  - Sharesheet が出ている間は、読み取りの後に端末上のダンプを消す。
  - Sharesheet の中の要素に対する `expect` の失敗の理由には値を入れない。
  - `UiDriver.kt` の失敗時の文言の書き出しは写さない。

**R1-13 層 2a の選択と取り消しのテストに必要な継ぎ目が決まっていない**
- 節: 7.2 の「選ばれたアプリ」「`CancelPendingShareCallback`」
- 問題:
  - Editor では `ShareWithCallback` が F-8 を呼ばずに失敗する（J-S3）。そのため「直前の ID」は 0 のままで、`onSelected` も失敗で消える（J-S14）。
  - つまり、公開 API だけでは「一致する ID と、登録した `onSelected`」を用意できない。`PostSelection` だけでは 7.2 の 2 つのテストを書けない。
- 区分: B
- 修正案:
  - `internal` の継ぎ目を 5.1 と 7.2 に書く。例: `SetPendingSelectionForTests(ulong id, Action<ShareCallbackResult>?)`。
  - 静的な状態（`user_data` の表、ID、登録済みの印）を、テストの間と Editor の再生の開始で消す口（`SubsystemRegistration`）も書く。

**R1-14 5.3 に U+0000 と対になっていないサロゲートの行が無い**
- 節: 4.2 の末尾、5.3
- 問題:
  - 4.2 は「途中の U+0000 はそこで切れる」と書いているが、5.3 に無い。1.x は JSON の `\u0000` で値をそのまま運んでいた。
  - 本文が `"\0abc"` のときは入口で `EMPTY_CONTENT`（「text is required」）になり、`"abc\0def"` のときは `"abc"` だけが共有される。
  - Dialog の設計 v4 は、この 2 つを 5.3 に書いている（J-D12、5.3:345 とサロゲートの行）。
- 区分: A1（軽い）
- 修正案: 5.3 に、Dialog と同じ 2 行（すべての文字列が対象）を足す。

### 低優先度

**R1-15 検査の失敗と Editor の失敗を、`EnsureCreated` の前に積んでいる**
- 節: 5.4 の手順 3〜5
- 問題:
  - common.md:65 は「ライブラリを呼ぶたびに `EnsureCreated`」と定めている。しかし、手順 3（検査の失敗）と手順 4（Editor）は手順 5 より前に `Post` している。
  - 取り出し口が壊されていると、結果は列に残ったまま `LogError` を出し、検査の失敗が続く限り戻らない。
- 根拠: `AndroidNativeCallbackQueue.cs:70-79`
- 区分: A1（軽い）
- 修正案: 引数の null の検査の直後で `EnsureCreated()` を呼ぶ。

**R1-16 入口の `INVALID_CHOOSER_ACTION` が 4.4 の表に無い。整えた後の値と、C に届く値が違いうる**
- 節: 4.2.1、4.4:321
- 問題:
  - C# は、UTF-8 にする前の文字列で重複と空を除く。そのため、U+0000 の後ろだけが違う ID 同士、対になっていないサロゲート同士、`"\0x"` のような ID は C に届くと重複や空になり、入口で `INVALID_CHOOSER_ACTION` が返る。
  - 4.4 はこの値を完了の行（「アイコンが画像として読めない」）にしか書いていないので、誤った文言の `LogWarning` が出る。
- 根拠: `Share.cpp:156-163`
- 区分: A1（軽い）
- 修正案:
  - `NormalizeActions` は、U+0000 で切り、U+FFFD に置き換えた後の値で比べる。
  - 4.4 に「入口の `INVALID_CHOOSER_ACTION`」の行を足し、`LogError`（正しい実装では起きない）にする。

**R1-17 空白の判定が Kotlin と C# で違う**
- 節: 4.2、E-14
- 問題:
  - Kotlin の `isBlank` は `Character.isWhitespace || isSpaceChar` で、U+001C〜U+001F を空白とみなし、U+0085 はみなさない。
  - .NET の `IsNullOrWhiteSpace` は、U+0085 を空白とみなし、U+001C〜U+001F はみなさない。
  - そのため、本文が U+0085 だけのとき、1.x は共有したが 2.0.0 は「text is required」になる。
  - E-14 の「U+3000 など」はどちらも空白とみなすので起こせない。U+001C なら完了の `EMPTY_CONTENT` を起こせる。
- 区分: A1（軽い）+ B
- 修正案:
  - 検査を Kotlin と同じ判定の純粋な関数にする（または 5.3 に書く）。
  - E-14 の層 2b を U+001C で行う。

**R1-18 5.4 の形では、呼ぶ前の例外で `user_data` の表に要素が残る**
- 節: 5.4 の手順 6〜9 と擬似コード
- 問題: `AddCall` が確保より前にあるため、確保や変換で例外が出るとネイティブを呼ばず、`release` も来ない。表の要素が消えないまま残る。
- 区分: A1（軽い漏れ）
- 修正案: `AddCall` を `extern` を呼ぶ直前に移すか、ネイティブを呼ぶ前に例外が出たときは `catch` の中で消す。

**R1-19 J-S9 で、`File.Exists` は ディレクトリ に対して偽を返す**
- 節: J-S9
- 問題:
  - Kotlin の `File.exists()` は ディレクトリ に対して真を返す。C# が最初の「無いパス」を選ぶとき、実際に無いファイルより前にある ディレクトリ を誤って選ぶ。
- 根拠: 2.0.0 の `ShareRepositoryImpl.kt:238-239`
- 区分: A1（文言だけ）
- 修正案: `File.Exists(p) || Directory.Exists(p)` を使う。

**R1-20 J-S4 が親課題の J-9 と common.md:64 から外れていることを記録していない**
- 節: J-S4、5.1（`common.md` の変更）
- 問題:
  - 親課題の J-9 と common.md:64 は「取り出した時点の `Instance` に出す（4 機能とも同じ）」と定めている。
  - J-S4 は「`_instance` に出し、いなければ捨てる」とした。理由は書いてあるが、親課題と common.md を直す手順が 5.1 に無い。
- 区分: C
- 修正案: 5.1 の `common.md` の行と、親課題の README の J-9 に、Share の例外を書く。

**R1-21 記述の整合（機械照合に回す）**
- 区分: C
- 1.x の設計の名前が違う。`2026-06-20-...-v1.md〜v3.md` は実在せず、実際は `2026-06-21-...-v1.md`、`-v2.md`、`2026-06-27-...-v3.md`。
- 「1.x の設計 v3:588-596」は存在しない（v3 は 376 行）。
- 「親課題の『移行時にやること』」という節は無い（README の段 4 の行）。
- 5.1 で、`ShareChooserActionCallbackCoordinator.cs` と `AndroidShareManagerExampleController.cs` が「既存変更」なのに、内容は「変えない」になっている（非変更か、中身を明記するか）。
- 7.1 で足す `AndroidNativeToolkitCApiTests.cs` が 5.1 に無い。`testing.md` のガードの表（:234 の `AndroidShareManager` の B 群）を変えることも 5.1 に書く。
- 8.3 の `count pkg=...` は、補助のセレクタのキー `package=` と、値を Base64 で渡す決まりに合わせる（`android_ui_driver.py:85-103`）。
- 8.4 の「今も書いているか」は確かめ済み（docs/1.11.0/share.ja.md:76-104）。

## 不足項目

- 5.3 に足りない行: 結果の順（R1-1）、表示する Chooser Action の重複（R1-4）、API 33 以下で読めないアイコンがあると全体が失敗すること（R1-4）、必須の値・配列・アクションが null のとき（R1-5）、Base64 の寛容さ（R1-8）、作り直した Manager にアクションが届くこと（R1-11）、U+0000 とサロゲート（R1-14）、空白の判定（R1-17）、選択の照合の時点（R1-7、受け口での照合にしない場合）。
- 1.x のまま流すテスト（5.5 の手順 3）の観点の追加: パディングの無い Base64、`ID` が重複したアクションの表示、null の各項目、同じフレームでの 2 回の呼び出しと per-call の対応、本文が U+0085 だけのとき。
- 層 1 では `extern` の `out ulong` と `ntk_share_cancel_selection(ulong)` を確かめられない（Editor ではコンパイルされない）。層 2b で、F-8 が書いた ID を取り消しと選択の照合に使えること（上位 32 ビットが壊れていないこと）を確かめる項目が無い。
- 選ばれたアプリの `package_name` が NULL の場合（`SelectedPackageName == null`）を層 2a で確かめる項目が無い。
- 7.3 で、カテゴリ `AndroidUiDriver` をクラス全体に付けると、補助の要らないテスト（Direct Share の登録と削除、`FILE_NOT_FOUND` など）も、補助がいない実行では skip される。分けるかどうかを決めていない。
- J-S3 の Editor での `Initialize()` のログの段階（毎回の `Awake` で `LogWarning` を出さないこと）が書かれていない。

## 総合評価

C ABI の事実関係（入口と完了の区別、スレッド、`release` の時点、所有権、構造体の配置、選択と取り消しの意味、AP-7、FileProvider）は正確で、Handoff の要求も押さえている。移行の骨格はこのまま使える。

一方、「1.x と同じ」と書いた箇所のいくつかは、1.12.0 のコードと照らすと違っていた。具体的には、per-call の枠を読む時点（R1-1）、Editor での取り消し（R1-2）、表示する Chooser Action と読めないアイコンの範囲（R1-4）、null の扱い（R1-5）、Base64（R1-8）、作り直した後のアクション（R1-11）。どれもテストが通ったまま公開の振る舞いが変わる A1 なので、5.3 への追記か設計の修正が要る。

選択の ID の扱い（R1-6、R1-7）とリスナーの登録（R1-10）にも、実装すると気づけない契約の穴がある。

検証の面では、テスト用の共有先の前提（R1-3）が、このプロジェクトに既にある androidlib の SEND の filter と `<share-target>` を見落としている。8.3、7.3、7.4、8.4 を組み直す必要がある。

区分ごとの件数は、A1 が 15 件（R1-1、R1-2、R1-4〜R1-11、R1-14〜R1-19。うち R1-9 は P4 違反）、B が 3 件（R1-3、R1-12、R1-13。R1-17 は A1 と B の両方にかかる）、C が 2 件（R1-20、R1-21）。A1 が残っているので、止める基準には達していない。改訂版（v2）で直した後、レビュアーを替えて再レビューすることを勧める。

## 2. Codex

読み取り専用で確認し、ファイル変更・Unity・adb・端末・クリップボード・デスクトップ操作は行っていません。

**C1-1（A2）IL2CPP 用 reverse P/Invoke 属性が設計から欠落**
- セクション: 4.1、5.1、5.2、7.1
- 問題: 4 種の managed callback を関数ポインタとして渡す設計ですが、受け口の static メソッドに必要な `[MonoPInvokeCallback(typeof(...))]` が記載されていません。
- 根拠: 計画書:234-237,337-338 は delegate だけを宣言し、属性要件を記載していません。handoff:142-143 は、全 callback を static readonly で保持し、IL2CPP では `[MonoPInvokeCallback]` を付けるよう明記しています。既存 Dialog は実際に属性を付けています（`Runtime/Android/Dialog/AndroidDialogManager.cs`:396-401,452-458）。Unity 公式の Scripting restrictions も、AOT 上の native-to-managed callback に同属性を要求しています。
- シナリオ: 属性なしで `DoneCallback` 等を `Marshal.GetFunctionPointerForDelegate` に渡すと、対象の IL2CPP Player で reverse P/Invoke がビルドまたは実行時に失敗します。

**C1-2（A1）ネイティブ呼出前の例外で `user_data` 表の項目が永久に残る**
- セクション: J-S1、5.4
- 問題: `AddCall` は確保処理より前です。確保または P/Invoke が例外になった場合の `catch` は失敗を通知するだけで、表からキーを除去しません。`release` はネイティブ関数を呼べた場合にしか来ません。
- 根拠: 計画書:407-425、`AndroidNativeToolkitCApi.cs`:72-113、`Share.cpp`:108-112。
- シナリオ: `AddCall` 後の `AllocUtf8` が `OutOfMemoryException` を投げる、または P/Invoke が `EntryPointNotFoundException` を投げる場合、`ConcurrentDictionary` の `ShareCall` が残り続けます。

**C1-3（B）`user_data` と `release` の最重要順序を Unity 側で検証しない**
- セクション: 7.1〜7.3
- 問題: 入口拒否時の「callback なし・release が戻る前に 1 回」と、「completion が表を読んだ直後に release が表を消す」を直接検証する seam／ケースがありません。
- 根拠: handoff:128-138、`Accept.h`:25-38、計画書:477-515。
- シナリオ: 実装者が completion のキーだけを queue に積み、UnityMain で表を引く実装にしても、通常の Editor テストは通ります。実機では直後の `release` が先に表を消し、完了が無通知になります。

**C1-4（A1）並行する `ShareWithCallback` で直前の request ID が逆転する**
- セクション: J-S12、J-S16
- 問題: F-8 が戻った呼出スレッドで「直前の ID」を代入する一方、どのスレッドからも呼べるとしています。2 呼出しの native-main 上の順序と、各ワーカースレッドが戻って代入する順序は一致しません。また handoff の「manager の表は UnityMain だけが触る」に反します。
- 根拠: 計画書:369、373、handoff:129-132,140、`Accept.h`:30-38、`ShareBridge.kt`:192-196。
- シナリオ: worker A、B の順で native main に要求が積まれ、B が現在の待ちになります。B が先に戻って ID-B を保存した後、停止していた A が ID-A を上書きします。B の選択イベントは ID-B なので、不一致として捨てられます。

**C1-5（A1）selection の `ntk_string` が読取り例外時に解放されない**
- セクション: 5.4
- 問題: Chooser Action 側は `finally ntk_string_free` です。selection 側は読取り後に通常文として free しており、`ReadString` が投げると解放されません。
- 根拠: 計画書:428-429、`AndroidNativeToolkitCApi.cs`:124-153,194-196、`Share.h`:47-50、`Common.h`:44-51。
- シナリオ: package name のコピー中に managed allocation が失敗すると、その selection の native string handle がリークします。

**C1-6（A1）重複 ID の Chooser Action を表示ごと削除してしまう**
- セクション: 4.2.1、5.3
- 問題: 計画は重複 ID の後続アクション全体を捨てます。しかし 1.x は表示用 JSON には重複アクションを残し、callback receiver に登録する ID だけを `distinct()` にしていました。5.3 にこの表示差がありません。
- 根拠: 計画書:282-285、1.x の `UnityShareJsonParser.kt`:88-100、`UnityAndroidShareManager.kt`:153-159,438-450、`ShareChooserActionInputs.kt`:16-24、1.x の `ShareRepositoryImpl.kt`:245-266。
- シナリオ: 同じ `intentAction` でラベルとアイコンが異なる 2 項目を渡すと、1.x は 2 項目とも表示します。移行後は最初の 1 項目しか表示されません。

**C1-7（A1）読めない新規アイコンの失敗後、古い action が新しい callback に配送される**
- セクション: 4.2.1、4.3、J-S5、5.3
- 問題: 新しい C ABI は画像を読めない場合、generation を進める前に失敗するため、古い Sharesheet の action が生き残ります。一方、C# coordinator は `ShareText` 呼出時点で新しい per-call callback に置換され、失敗時に戻す／消す規則がありません。
- 根拠: `AndroidShareManager.cs`:197-209、計画書:342、C ABI の `ShareRepositoryImpl.kt`:49-58、1.x の `ShareChooserActionReceiverRegistry.kt`:43-64 と `UnityAndroidShareManager.kt`:155-166。
- シナリオ: Share A の action が表示中に、Share B を画像として読めないアイコン付きで呼ぶと、B は失敗しますが A の action は押せるままです。押すと、B に渡した `onChooserAction` が呼ばれます。

**C1-8（A1）SEND の新挙動に対して公開 XML コメントを更新しない**
- セクション: 5.1、5.3
- 問題: 移行後は `android.intent.action.SEND` も callback を発火すると明記している一方、Payload 8 型を「非変更」としています。現行の公開コメントは、SEND が除外され callback を発火しないと明記しています。
- 根拠: 計画書:288,383,350、`AndroidSharePayloads.cs`:19-26。
- シナリオ: 利用者が公開 API コメントに従い、SEND では callback が来ない前提で処理しますが、移行後はイベントと per-call callback が発火します。

**C1-9（A1）`filePaths`／`ids` 配列自体が null のときの契約を未記載のまま変更する**
- セクション: 4.2、J-S8、5.3
- 問題: 計画は null 配列を非同期の検査失敗へ変換します。現行実装は JSON 構築中に同期 `NullReferenceException` を投げます。5.3 は「配列要素の null」しか差分に挙げていません。
- 根拠: 計画書:270,275,298、`AndroidShareJsonBuilder.cs`:50-53,71-74,99-102,134-141、`AndroidShareManager.cs`:231-235,257-261,281-285、計画書:390。
- シナリオ: `new ShareFilesPayload { filePaths = null! }` を渡すと、1.x は呼出し中に NRE です。移行後は後フレームに `Failed to shareFiles: filePaths must not be empty` が届きます。

**C1-10（A1）null の Chooser Action 要素を例外ではなく黙って削除する**
- セクション: 4.2.1、5.3
- 問題: 計画は `chooserActions` の null 要素を捨てて共有を続行します。現行 C# は JSON 構築時にその要素を参照して同期 NRE になります。この差は 5.3 にありません。
- 根拠: 計画書:282、`AndroidShareJsonBuilder.cs`:120-129、`AndroidShareManager.cs`:197-209。
- シナリオ: `[validAction, null]` を渡すと、1.x は同期 NRE です。移行後は validAction だけで Sharesheet が開きます。

**C1-11（A1）埋め込み U+0000 で共有内容が無通知に切り詰められる**
- セクション: 4.2、5.3
- 問題: 計画は C 文字列への変換で U+0000 以降を切り捨てると明記していますが、既知差分に含めていません。1.x の JSON 経路は U+0000 を `\u0000` として表現して Kotlin String に渡します。
- 根拠: 計画書:278、`Common.h`:7-10、`AndroidShareJsonBuilder.cs`:211-238、1.x の `UnityShareJsonParser.kt`:11-25。
- シナリオ: `text = "A\u0000B"` を共有すると、1.x は Kotlin に全 3 文字を渡します。計画どおりの P/Invoke は `"A"` だけを native 側へ渡します。

**C1-12（C）J-S3 の Markdown 表が壊れている**
- セクション: 5.2 J-S3
- 問題: 表内の `` `|| UNITY_EDITOR` `` の `|` がエスケープされておらず、4 列の表が 6 列として解釈されます。
- 根拠: 計画書:360。`scripts/check_design_consistency.py` も「line 360, expected 4, actual 6」と検出しています。

欠陥なしの領域:

- 構造体の Pack、サイズ、offset は `Share.h`:52-81 および `StructLayoutTest.cpp`:118-141 と一致しています。
- extern の引数型そのもの（`UIntPtr`＝`size_t`、selection の `ulong`／`out ulong`、listener の `out IntPtr`）は `Share.h`:83-112 と一致しています。
- C ABI の入口拒否と完了エラーの分類、および 0〜16 のエラー値は `Share.cpp`／P2 11.4 と一致しています。
- AP-7 の「action 0 件でも generation を進める」、空 ID、Base64 不正、SEND のネイティブ規則自体は一次実装と一致しています。
- FileProvider の authority と公開パスは、manifest および `native_toolkit_share_file_paths.xml`:2-6 と一致し、`external-cache-path` は含まれていません。
- 既存 1.x 文言からの変換表は、上記 null 入力の返却時点差を除き、確認した範囲で一致しています。

## 3. 統合（止める基準の判定）

A1 は、サブエージェント 15 件・Codex 8 件（重なり: R1-4 と C1-6、R1-5 と C1-9・C1-10、R1-7 と C1-4、R1-14 と C1-11、R1-18 と C1-2）。A2 は C1-1 の 1 件（`[MonoPInvokeCallback]` の書き漏れ。IL2CPP のビルドで落ちる）。B は R1-3、R1-12、R1-13、C1-3。C は R1-20、R1-21、C1-12。

**A1 が残っているので止めない。設計 v2 で直し、レビュアーを替えて再レビューする。**
