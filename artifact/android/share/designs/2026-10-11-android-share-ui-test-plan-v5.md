# Android Share UI 自動テスト計画 v5

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Android Share のサンプル（`Runtime/UI/Android/Share/AndroidShareManagerExampleController.cs`）と `AndroidShareManager` |
| 同梱 AAR | C ABI 2.0.0（`Plugins/Android/VERSION.txt`: dist 1.13.0）。移行の間は 1.x（dist 1.12.0）も追加のピンで置く |
| 目的 | Android Share の C ABI 2.0.0 への移行で、ふつうの使い方の振る舞いが 1.x と同じであること、違いが設計 v6 5.3 の既知の差分にあることを、実機の画面を操作して確かめる |
| 実行 | `scripts/verify_unity_android.sh` の実機の PlayMode（層 2b）。テストは `Tests/PlayMode/AndroidSharePlayerTests.cs`（UI を使う。カテゴリ `AndroidUiDriver` をクラスに付ける）と `Tests/PlayMode/AndroidShareManagerPlayerTests.cs`（UI を使わない。カテゴリを付けない） |
| 設計 | `artifact/android/share/designs/2026-10-11-android-share-design-v6.md`（7.3 が層 2b。この計画は 7.3 に番号を振り、期待値を具体にしたもの）。保つ範囲は親課題の J-11（ふつうの使い方を保つ） |
| 画面の操作 | ホストの adb の補助（`artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v10.md`）。命令の名前はその 3.1、Sharesheet の命令の期限と閉じ方は 3.3、プライバシーは 3.4 |
| 移行前の基準 | 無い。Android の Share の UI テストは 1.x には無かった（`artifact/topics/device-verification-records/README.md`）。期待値は、1.x のコード（設計 v6 の 2 章と、設計 v6 4.2 の根拠の列）と既知の差分（設計 v6 5.3）から決めた |
| レビュー | `artifact/android/share/reviews/2026-10-11-android-share-ui-test-plan-review-v1.md`、`...-review-v2.md`、`...-review-v3.md`、`...-review-v4.md`（どれも補助の設計と補助のコードと一緒に見た） |

Dialog の UI テスト計画（`artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v2.md`）と同じく、サンプル画面を通すテスト（1 章）と、Manager を直接呼ぶテスト（2 章）に分ける。

**移行の前に、1.x のまま流して記録する**（設計 v6 5.5 の手順 3）。各表の「1.x」の列は、1.x で流すかと、予想される値。「同じ」は 2.0.0 の期待と同じ。実測が予想と違えば、その結果をこの列に書き、設計の 2 章と 5.3 を直す（設計書の新しい版にする）。移行の後は、2.0.0 の結果が「1.x と同じ」か「設計 v6 5.3 の既知の差分」のどちらかになることを確かめる。

**補助の要検証 V-11〜V-16 は確かめた**（V-11〜V-14 は 2026-10-11、V-15・V-16 は 2026-10-12。Pixel 6a、1.x の Manager。補助の設計 v10 の 9 章）。

- Sharesheet が出ている間は Unity が止まる。依頼を先に出す形で正しい
- このアプリ自身は、Player の名前で共有先に出る（Android™ Profile では「Unity NativeToolkit」）。押すと選択のイベントが届き、Player が戻る
- `uiautomator dump` は、Sharesheet のプレビューの本文と Chooser Action のラベルを読める
- 登録した Direct Share の共有先は Sharesheet に出る
- 8 回スワイプした後も Back で閉じる
- Back が効かないときは、Sharesheet のプロセスを止めて閉じられる

**前提: 端末に、テストの Player と同じ名前のアプリが無いこと。** あると補助がそちらを押すことがある（1 回目の実行で、6 月に入れたサンプルアプリ `com.jonghyunkim.nativetoolkit.android` を押した。Android™ Profile でビルドしたもので、名前が同じだった。そのアプリは消した）。補助は、押した後に別のアプリが前面に来たら、このアプリを前に戻して `fail wrong-app` で答える（補助の設計 v10 3.1）。テストは止まらずに失敗する。

## v4 からの主な変更（レビュー v4 を受けて。2026-10-12）

- 4 章に、受け入れる危険として「依頼から 10 秒ほどを過ぎて出る Sharesheet は、補助が閉じない」を足した（S:C-8）。表と数は変えていない（レビュー v4 で、数はサンプルと 1.x の Kotlin に合うと確かめられた）

## v3 からの主な変更（レビュー v3 を受けて。2026-10-12）

- 1 章のテストは、イベントの数を照らす前にシーンを下ろす。サンプルの `OnDisable` が呼ぶ `CancelPendingShareCallback` の結果（`Op` 1）を、1 章のすべての行の数に含めた（X:A1-5、S:B-1）
- 「来ない」の確かめと後片付けの照らしで、起点の `OnApplicationPause(false)` が無いテストの扱いを書いた（S:C-9）
- AS-52 の `no-sheet` の待ちは、前の依頼（`background` の戻し）の後に始まることを書いた（S:C-3）
- Sharesheet の命令の答えの待ちは 180 秒（補助の設計 v9 3.3）

## v2 からの主な変更（レビュー v2 を受けて。2026-10-12）

- 補助への文字列の引数は Base64 で渡すことを、表の前に書いた（表では読みやすさのため生で書く。Codex の A2-1）
- イベントの数を、すべての行に `Op` / `Selected` / `Action` の 3 つで書いた。後片付けの `RemoveDirectShareTargets` は、登録したテストでいつも呼び、その結果を表の数に含める（サブエージェントの B-5、Codex の A2-2）
- 待ち時間の 5 秒と 10 秒の使い分けを書いた（Codex の A2-3）。Sharesheet の命令の答えの待ちは 150 秒（補助の設計 v8 3.3）
- AS-51 のラベルの接尾辞は、テストが自分で作る（補助の実行の ID はテストから読めない。サブエージェントの B-6）。AS-34 の ID を `ntk_as34` と書いた（C-9）
- 後片付けの `close-sheet` は念のための手順で、ふつうは働かないことを書いた。`Do` は Sharesheet の命令に 150 秒を使う（B-7）
- 「来ない」の確かめの起点を、テストが記録した `OnApplicationPause(false)` の時刻にした（C-1）
- AS-41b が押さえるのは `preview_title` だけと書き直した（C-10、Codex の B-3）
- 後片付けの手順の重複を除き、`PendingCount` の確かめを `WaitForPending` の前に移した（C-8）

## v1 からの主な変更（レビュー v1 を受けて。2026-10-11）

- Sharesheet を閉じるのに `back` を使わず、`close-sheet` を使う（U1-2）。Sharesheet が開かないはずの UI のテストは、先に `no-sheet` を出す（U1-17）
- `absent-in-sheet` は在るはずの文言（アンカー）を一緒に渡す（U1-1、CX-7）。
  - AS-43 は、捨てる理由ごとにラベルを分け、2 つのテスト（AS-43a、AS-43b）にした
  - AS-44 は、残る方を押してアクションが 1 回届くことも確かめる（AS-44b）
  - AS-44 の 1.x の列は `present-in-sheet` で記録する
- イベントの数は種類ごと（`Op` / `Selected` / `Action`）に期待を持つ。「来ない」の確かめは、Player が戻ってから 2 秒待って照らす（U1-4）
- AS-52 は、1.x と 2.0.0 で同じテストにした（別のスレッドから Manager を呼ぶ。`no-sheet` で、戻った後に遅れて出る Sharesheet を見張る。U1-7）
- AS-41 にプレビューの題を足した（AS-41b。U1-8）
- AS-09、AS-13、AS-29、AS-51、AS-12 の期待と後片付けを直した（U1-10〜U1-13）
- `PendingCallCountForTests` と補助の `PendingCount` が 0 に戻ることを、両方のクラスの後片付けで確かめる（U1-15）
- 後片付けの順を直した。Direct Share の共有先を消すのは、イベントの数を照らす前にする（CX-8）
- 1.x の列の「見込み」を、1.12.0 のコードで確定させた（C-5）
- 待ち時間を補助の設計 v7 に合わせた（Sharesheet の命令は 120 秒。C-4、C-7）

## 1. サンプル画面を通すテスト

サンプルのトップメニューを読み込み、Share の画面を開いてからサンプルのボタンを押す（Dialog の `AndroidDialogPlayerTests` と同じ形。`AndroidSampleScreenDriver`）。サンプルのボタンは C# から押す。Sharesheet は補助で押す。Test Android™ Profile の Player にはサンプルのシーンが無いので、この章のテストは `Assert.Ignore`（Dialog と同じ。ゲートの skip の判定は Android™ Profile の実行だけ）。

サンプルは固定の値で呼ぶ（設計 v6 2.6）。結果は画面の `ResultTextBlock` で照らす。サンプルは最初に `Requested: <題>` を出し、結果のイベントで `[event] <題>: Success`（失敗なら `Failed` と `\nError: <文言>`）に変える。Sharesheet が出ている間は Unity が止まり、途中の表示が描かれないことがあるので、照らすのは `ResultTextBlock` の**最後の値**と、Manager のイベントの数（3 章）。

**Sharesheet の操作の約束**（3 章）: テストは補助への依頼を先に `Send` し、同じフレームでボタンを押し、`Wait`（180 秒）する。

**補助への文字列の引数は Base64 の UTF-8 で渡す**（`AndroidUiDriver.Base64(...)`。補助の設計 v10 の 3 章）。この計画の表では、読みやすさのため生の文字列で書く（`Send("tap-in-sheet", "Save")` は `Send("tap-in-sheet", AndroidUiDriver.Base64("Save"))` のこと）。`no-sheet` のミリ秒は生で渡す。

**イベントの数**は、表の「期待」の列の終わりに `Op / Selected / Action` の順で書く（3 章）。後片付けの削除の結果と、この章では後片付けでシーンを下ろすときの取り消しの結果（`Op` 1。サンプルの `OnDisable` が `CancelPendingShareCallback` を呼ぶ。`AndroidShareManagerExampleController.cs:81-90`）を含めた数。

| # | 操作 | 期待（移行後。`ResultTextBlock` の最後の値） | 1.x |
|---|---|---|---|
| AS-01 | `Send("close-sheet")` → ShareTextButton | `[event] Share plain text: Success`。依頼の答えが `ok`（Sharesheet が出て、閉じた）。2 / 0 / 0 | 流す。同じ |
| AS-02 | 同上 → ShareUrlButton | `[event] Share URL: Success`。2 / 0 / 0 | 流す。同じ |
| AS-03 | 同上 → ShareWithSubjectTitleButton | `[event] Share with subject and title: Success`。2 / 0 / 0 | 流す。同じ |
| AS-04 | 同上 → ShareRichPreviewButton | `[event] Share with rich preview: Success`。2 / 0 / 0 | 流す。同じ |
| AS-05 | 同上 → ShareImageButton | `[event] Share image: Success`。2 / 0 / 0 | 流す。同じ |
| AS-06 | 同上 → ShareImagesButton | `[event] Share multiple images: Success`。2 / 0 / 0 | 流す。同じ |
| AS-07 | 同上 → ShareFileButton | `[event] Share file: Success`。2 / 0 / 0 | 流す。同じ |
| AS-08 | 同上 → ShareFilesButton | `[event] Share multiple files: Success`。2 / 0 / 0 | 流す。同じ |
| AS-09 | `Send("tap-in-sheet", "Save")` → ShareCustomActionButton（API 34 以上） | 最後の値が `[event] ChooserAction: com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_SAVE`。途中の成功は、Manager の `ShareOperationCompleted` の成功 1 回で確かめる。2 / 0 / 1 | 流す。同じ |
| AS-10 | `Send("tap-in-sheet", <このアプリの名前>)` → ShareWithCallbackButton | `[onSelected] Selected: <このアプリのパッケージ名>`（サンプルの per-call が共通イベントの表示を上書きする）。Player が前に戻る（補助の前面の確かめが `ok`）。2 / 1 / 0 | 流す。同じ |
| AS-11 | `Send("no-sheet", "5000")` → ShareInvalidFileButton | `[event] Share invalid file (error handling demo): Failed\nError: File not found: /invalid/path/that/does/not/exist/sample.txt`。依頼の答えが `ok`（Sharesheet は出ない）。2 / 0 / 0 | 流す。同じ |
| AS-12 | RegisterDirectShareTargetButton → RemoveDirectShareTargetButton（Sharesheet は出ない） | `[event] Register Direct Share target: Success`、続いて `[event] Remove Direct Share targets: Success`。4 / 0 / 0（後片付けの削除とシーンを下ろすときの取り消しを含む） | 流す。同じ |
| AS-13 | Home を押す | トップメニューに戻る。サンプルの `OnDisable` が `CancelPendingShareCallback` を呼ぶので、`Op`（`cancelPendingShareCallback` の成功）が 1 回増える（`AndroidShareManagerExampleController.cs:81-90`）。もう一度 Share の画面を開いて AS-01 が通る。3 / 0 / 0（Home の取り消し、AS-01、シーンを下ろすときの取り消し） | 流す。同じ |

- AS-10 の `<このアプリの名前>` は Player の名前。テストは実行の中で `PackageManager.getApplicationLabel` で読む（Android™ Profile では「Unity NativeToolkit」。V-12。Test Android™ Profile の名前は、1.x の記録で確かめる）
- サンプルの画像はプロジェクトの `Assets/Resources/Images/share_sample_image.png` から作る（Android™ Profile の Player にだけある）
- AS-12 は、後片付けでいつもサンプルの共有先 `native_toolkit_sample_target`（`AndroidShareManagerExampleController.cs:16`）を消す（途中で失敗したときの残りを消すため。消すものが無くても呼び、結果を数える）

## 2. Manager を直接呼ぶテスト

サンプルでは出せない値（失敗の経路、Chooser Action の整え方、時点）を確かめる。Manager の公開 API を直接呼ぶ。イベントの記録は Dialog と同じく文字列（`Op|<operation>|<isSuccess>|<errorMessage>`、`Selected|<package>`、`Action|<actionId>`）で照らす。

### 2.1 Sharesheet を開く操作の失敗（`AndroidShareManagerPlayerTests`。UI を使わない）

| # | 呼び方 | 期待（移行後。`ShareOperationCompleted` と per-call。数は `Op / Selected / Action`） | 1.x |
|---|---|---|---|
| AS-21 | `ShareFile(filePath = "/data/local/tmp/ntk-none.txt")` | `Op|shareFile|False|File not found: /data/local/tmp/ntk-none.txt` が 1 回。1 / 0 / 0 | 流す。同じ |
| AS-22 | `ShareFile(filePath = "/system/etc/hosts")`（在るが FileProvider の範囲外） | `Op|shareFile|False|File cannot be shared: /system/etc/hosts. Ensure the file is in a supported directory.`。1 / 0 / 0 | 流す。同じ |
| AS-23 | `ShareFiles([persistentDataPath の在るファイル, "/data/local/tmp/ntk-none.txt"])` | `Op|shareFiles|False|File not found: /data/local/tmp/ntk-none.txt`（存在しない最初のパス。設計 J-S9）。1 / 0 / 0 | 流す。同じ（1.x も原因のパス） |
| AS-24 | `ShareFiles([在るファイル, "/system/etc/hosts"])` | `Op|shareFiles|False|File cannot be shared: <在るファイル>, /system/etc/hosts. Ensure ...`（全部のパス。設計 5.3）。1 / 0 / 0 | 流す。`File cannot be shared: /system/etc/hosts. Ensure ...`（原因のパス。既知の差分） |
| AS-25 | `ShareText(text = "")` | `Op|shareText|False|Failed to shareText: text is required`。1 / 0 / 0 | 流す。同じ |
| AS-26 | `ShareText(text = "   ")` | 同上。1 / 0 / 0 | 流す。同じ |
| AS-27 | `ShareFile(filePath = "")` | `Op|shareFile|False|Failed to shareFile: filePath is required`。1 / 0 / 0 | 流す。同じ |
| AS-28 | `ShareFiles(filePaths = new string[0])` | `Op|shareFiles|False|Failed to shareFiles: filePaths must not be empty`。1 / 0 / 0 | 流す。同じ |
| AS-29 | `ShareFiles(filePaths = [persistentDataPath の在るファイル, null])` | `Op|shareFiles|False|Failed to shareFiles: filePaths must not contain null`。1 / 0 / 0 | 流す。記録する（設計 5.3 の「要確認」。1.x は先頭から順に変換する〈`ShareRepositoryImpl.kt`（タグ `1.12.0`）:107〉ので、在るファイルを先に置いて null の要素まで届かせる） |
| AS-30 | `RegisterDirectShareTarget(id = "", label = "L", iconBase64 = <PNG>)` | `Op|registerDirectShareTarget|False|Failed to registerDirectShareTarget: id is required`。1 / 0 / 0 | 流す。同じ |
| AS-31 | `RegisterDirectShareTarget(iconBase64 = "!!!")` | `Op|registerDirectShareTarget|False|Invalid icon data for Direct Share target: <id>`。1 / 0 / 0 | 流す。同じ |
| AS-32 | `RegisterDirectShareTarget(iconBase64 = <"abc" の Base64>)`（Base64 は読めるが画像でない） | 同上（`INVALID_ICON`）。1 / 0 / 0 | 流す。同じ |
| AS-33 | `RemoveDirectShareTargets(ids = new string[0])` | `Op|removeDirectShareTargets|False|Failed to removeDirectShareTargets: ids must not be empty`。1 / 0 / 0 | 流す。同じ |
| AS-34 | `RegisterDirectShareTarget(id "ntk_as34")`（正しい値）→ `RemoveDirectShareTargets(["ntk_as34"])` | 成功が 1 回ずつ。UnityMain から呼び、戻った時点では 0 回（設計 J-S2）。3 / 0 / 0（後片付けの削除を含む） | 流す。同じ |
| AS-35 | 別のスレッドから `RegisterDirectShareTarget(id "ntk_as35")`（正しい値） | 呼び出し側に例外が出ず、成功が UnityMain で 1 回（設計 J-S16）。2 / 0 / 0（後片付けの削除を含む） | 流す。記録する（1.x の `AndroidJavaObject` を、アタッチしていないスレッドから呼べるかの裏が無い。例外が出たら、そのスレッドで `AndroidJNI.AttachCurrentThread` / `DetachCurrentThread` を呼ぶ形で記録し直す） |
| AS-36 | `ShareFile(payload = null!)` | `ArgumentNullException` が呼び出し側に出る。イベントは出ない。0 / 0 / 0 | 流す。`NullReferenceException`（既知の差分） |

- このクラスは補助を使わない（補助のいない実行でも走る）。退行で Sharesheet が開くと、Unity が止まったまま誰も閉じない。この危険は受け入れる（4 章）
- Direct Share のテスト（AS-34、AS-35）は、後片付けでいつも登録した共有先を消す（3 章の順）

### 2.2 Sharesheet を開く操作（`AndroidSharePlayerTests`。UI を使う）

| # | 呼び方 | 操作 | 期待（移行後） | 1.x |
|---|---|---|---|---|
| AS-41a | `ShareText(text = "ntk share preview 41", previewTitle = "ntk preview title 41")` | `Send("present-in-sheet", "ntk share preview 41")` を先に | 成功。依頼の答えが `ok`（プレビューにテストの本文が出た）。1 / 0 / 0 | 流す。同じ |
| AS-41b | AS-41a と同じ呼び方 | `Send("present-in-sheet", "ntk preview title 41")` | 同上（プレビューの題が出た。構造体の `preview_title` の写しを、端末の上の Kotlin まで押さえる。`title` と `subject` は画面に出ないので、ここでは押さえない。位置は層 1 の大きさと位置の確かめで押さえる）。1 / 0 / 0 | 流す。同じ。**Android 16 の Sharesheet が `EXTRA_TITLE` をプレビューの題として出すかを、ここで確かめる**。出なければ、AS-41b はやめて、計画の次の版に書く |
| AS-42 | `ShareText(text, chooserActions = [{label "A42", icon <PNG>, intentAction "ntk.a42"}])`（API 34 以上） | `Send("tap-in-sheet", "A42")` | 成功、`Action|ntk.a42` が `ShareChooserActionTapped` → `onChooserAction` の順に 1 回。1 / 0 / 1 | 流す。同じ |
| AS-43a | `ShareText` に 4 つのアクション: 空白のラベル、Base64 でないアイコン（ラベル「B43」）、画像の印の無いアイコン（ラベル「C43」）、正しいもの（ラベル「A43」） | `Send("absent-in-sheet", "B43", "A43")` | 成功。依頼の答えが `ok`（「A43」が出て、「B43」は出ない）。1 / 0 / 0 | 流す。同じ（`ShareRepositoryImpl.kt`（タグ `1.12.0`）:250 で空白のラベル、:254 で Base64 でないもの、:255 で画像でないものを `continue` で飛ばす） |
| AS-43b | AS-43a と同じ呼び方 | `Send("absent-in-sheet", "C43", "A43")` | 同上（「C43」は出ない）。1 / 0 / 0 | 流す。同じ |
| AS-44a | `ShareText` に同じ `intentAction`（`ntk.a44`）の 2 つ（ラベル「A44a」「A44b」） | `Send("absent-in-sheet", "A44b", "A44a")` | 2 つ目は出ない（設計 5.3）。1 / 0 / 0 | 流す。操作を `Send("present-in-sheet", "A44b")` に替えて記録する。**2 つとも出る**（既知の差分。1.x は重複を除かない。`ShareRepositoryImpl.kt`（タグ `1.12.0`）:248-265） |
| AS-44b | AS-44a と同じ呼び方 | `Send("tap-in-sheet", "A44a")` | `Action|ntk.a44` が 1 回。1 / 0 / 1 | 流す。同じ |
| AS-45 | `ShareText` に `intentAction = "android.intent.action.SEND"` のアクション（ラベル「S45」） | `Send("tap-in-sheet", "S45")` | 押しても `ShareChooserActionTapped` は来ない（1.x と同じ。設計 4.2.1。3 章の「来ない」の確かめ）。1 / 0 / 0 | 流す。同じ |
| AS-46 | `ShareText` に、PNG の印だけで中身が壊れたアイコンのアクション | `Send("no-sheet", "5000")` | `Op|shareText|False|Failed to shareText: a chooser action icon could not be read as an image.`。依頼の答えが `ok`。1 / 0 / 0 | 流す。操作を `Send("close-sheet")` に替える。**そのアクションだけ飛ばして開く**（既知の差分。`ShareRepositoryImpl.kt`（タグ `1.12.0`）:255） |
| AS-47 | `ShareWithCallback(text, onStarted, onSelected)` | `Send("tap-in-sheet", <このアプリの名前>)` | `onStarted` に成功、`ShareCallbackReceived` に自分のパッケージ名が 1 回、`onSelected` に 1 回。1 / 1 / 0 | 流す。同じ |
| AS-48 | `ShareWithCallback` | `Send("close-sheet")` | `onStarted` に成功。選択のイベントは来ない（3 章の「来ない」の確かめ）。1 / 0 / 0 | 流す。同じ |
| AS-49 | `ShareWithCallback` → 同じフレームで `CancelPendingShareCallback()` | `Send("tap-in-sheet", <このアプリの名前>)` | 取り消しの成功と `onStarted` の成功が 1 回ずつ（順は仮定しない。設計 5.3）。選択のイベントは来ない（取り消しに渡した `ulong` の ID が正しいことの確かめ。3 章の「来ない」の確かめ）。2 / 0 / 0 | 流す。同じ（1.x も取り消しの後は来ない） |
| AS-50 | `Initialize()` を 2 回呼んだ後、AS-42 と AS-47 と同じ操作 | 同上 | アクションと選択のイベントが 1 回ずつ（リスナーが 1 回だけ登録されている。設計 J-S6）。2 / 1 / 1 | 流す。同じ |
| AS-51 | 最初に `RemoveDirectShareTargets(["ntk_as51"])`（前の実行の残り）→ `RegisterDirectShareTarget(id "ntk_as51", label "NTK AS51 <tag>", icon <PNG>)` → `ShareText` | `Send("present-in-sheet", "NTK AS51 <tag>")` | 登録した共有先が Sharesheet に出る。`<tag>` はテストがその実行で作る GUID の先頭 6 桁（前の実行の残りでは通らない）。4 / 0 / 0（最初の削除、登録、共有、後片付けの削除） | 流す。同じ |
| AS-52 | 補助の `background 3000` と `no-sheet 10000` を同じフレームで `Send` する。別のスレッドが `background` の答えのファイルを待ち（`WaitForAnswerFile`）、後ろに回っている間に `AndroidShareManager.Instance.ShareText` を呼ぶ（Dialog の AD-27 と同じ形。設計 J-S16） | `Wait(background)`、`Wait(no-sheet, 180)` | `Op|shareText|False|Failed to shareText: the app is not in the foreground.`（設計 5.3）。`no-sheet` の答えが `ok`（戻った後も Sharesheet は出ない）。1 / 0 / 0 | 流す。記録する（`no-sheet` は補助が `background` の戻しを済ませてから始まるので、その答えは `background` の 3 秒と戻しの分だけ遅い。ふつうは 15 秒ほど。1.x は OS が起動を黙って止めるか、戻った後に遅れて Sharesheet が出るか〈そのときは `no-sheet` が閉じて `fail sheet-opened` を返す〉。どちらでもテストは止まらない。予想は設計 v6 5.3 の「要確認」） |
| AS-53 | 別のスレッドから `ShareText` | `Send("close-sheet")` | 呼び出し側に例外が出ず、成功が UnityMain で 1 回。1 / 0 / 0 | 流す。AS-35 と同じく記録する |

### 2.3 欠番

AS-14〜AS-20、AS-37〜AS-40 は欠番（章の区切りのため）。

## 3. テストの共通の約束

- カテゴリ `AndroidUiDriver` は `AndroidSharePlayerTests` のクラスに付ける（AS-01〜AS-13、AS-41〜AS-53）。`AndroidShareManagerPlayerTests`（AS-21〜AS-36）には付けない（補助がいない実行でも走る）。ゲートは、64 ビットの Android™ Profile の実行で、このカテゴリの skip が 0 であることを見る
- **Sharesheet の操作の約束**（設計 v6 7.3、補助の設計 v10 3.3・5 章）:
  - Sharesheet が出ている間は Unity が止まる（V-11）。テストは補助への依頼を先に `Send` し、同じフレームで Share を開き、`Wait`（180 秒）する。`Wait` は開いたのと同じフレームで始まり、Sharesheet が閉じて Unity が戻ってから進む。期限は `Wait` を始めた時点から数える
  - 1 つの依頼は、それだけで終わる操作（`tap-in-sheet`、`absent-in-sheet`、`present-in-sheet`、`close-sheet`、`no-sheet`）。補助は、失敗でも Sharesheet を閉じ、このアプリを前面に戻してから答える（Back、それでも閉じなければ Sharesheet のプロセスを止める。補助の設計 v10 3.3）。Sharesheet に `back` は使わない
  - UI のクラスで、Sharesheet が開かないはずの呼び出し（AS-11、C ABI の版の AS-46、AS-52）は、先に `no-sheet` を `Send` する。退行で開いても補助が閉じ、テストは失敗で終わる
  - `Send` した依頼は、失敗の経路でも必ず回収する（後片付けの `WaitForPending`。途中の失敗で止まらず、全部を回収してから失敗する）
- **イベントの数**: 各テストは、`Op`（`ShareOperationCompleted`）、`Selected`（`ShareCallbackReceived`）、`Action`（`ShareChooserActionTapped`）の期待の数を持つ（表の `Op / Selected / Action`）。後片付けは 3 つを別々に照らす。`Op` の数には、後片付けの `RemoveDirectShareTargets`（登録したテストでいつも呼ぶ）の結果と、1 章のテストでシーンを下ろすときの取り消しの結果（AS-13 は Home のときの分も）を含める
- **「来ない」の確かめ**（AS-45、AS-48、AS-49 など）: 選択とアクションのブロードキャストは、Player が戻った後のフレームで届くことがある。テストは `OnApplicationPause` を受ける MonoBehaviour を置き、Sharesheet を開いた後に受けた `OnApplicationPause(false)` の時刻を記録する（`Wait` が戻る時点では、ふつう既に受けている。まだなら期限 10 秒で待つ）。その時刻から 2 秒たってから数を照らす。Sharesheet が開かないテスト（AS-11、AS-12、C ABI の版の AS-46、AS-21〜AS-36）は起点が無いので、最後の結果のイベントを受けた時刻を起点にする。AS-52 の起点は `background` から戻ったときの `OnApplicationPause(false)`
- 後片付け（Dialog の UI テスト計画 v2 の 3 章と同じ形。順に）:
  1. 補助を使わない手順（AS-52 のスレッドを止める）
  2. このテストで登録した Direct Share の共有先を、いつも消す（AS-12 は `native_toolkit_sample_target`、AS-34 は `ntk_as34`、AS-35 は `ntk_as35`、AS-51 は `ntk_as51`）。その結果を待つ（数を照らす前に。結果が次のテストに流れ込まないように）
  3. 補助の `PendingCount` を記録する（正しく進んだテストでは 0。回収の漏れは、ここで分かる）
  4. 補助を使う手順は `Safely` を通す: 回収していない依頼の `WaitForPending`。念のため、`count package=com.android.intentresolver` が 0 でなければ `close-sheet`（`Do` は Sharesheet の命令に 180 秒を使う）。1 章のテストは、ここでシーンを下ろす（サンプルの取り消しの結果が、数を照らす前に出るように）。Sharesheet が出ている間は Unity が止まるので、この手順が走る時点ではふつう 0 で、働くのは補助が閉じられなかった後だけ
  5. 3 種のイベントの数が期待に等しくなるまで待ち（期限 5 秒。テストの本体の中で 1 つのイベントを待つ期限は 10 秒）、`OnApplicationPause(false)` の時刻（無いテストは最後の結果のイベントの時刻）から 0.5 秒以上たってから照らす。結果がすべて UnityMain で出たことも照らす
  6. `PendingCallCountForTests` が 0（C ABI の版だけ。1.x の列では見ない）、手順 3 で記録した `PendingCount` が 0。どちらのクラスでも見る
  7. 購読を外し、取り出し口の列を空にし、まだ下ろしていなければシーンを下ろす。これらは手順が失敗しても必ず行う
- 押す文言は、テストが決めた値（このアプリの名前、テストの Chooser Action のラベル、テストの本文）だけにする。ほかのアプリの共有先と連絡先には触らない（補助の設計 v10 3.4）
- 待ち時間: 補助の Sharesheet の命令の答えは 180 秒（補助の設計 v10 3.3）、ほかの命令は 20 秒（`background` を含む）。テストの本体で結果のイベントを 1 つ待つのは 10 秒、後片付けで数がそろうのを待つのは 5 秒

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| ほかのアプリへの共有（本当に届くか、届いた中身） | ほかのアプリのデータに触れる。共有の中身は AS-41a・AS-41b のプレビューと、自分を選ぶ AS-10 / AS-47 で押さえる |
| API 33 以下の端末（Chooser Action が出ないこと） | 手元に端末が無い。手動（設計 v6 7.4） |
| `NO_SHARE_TARGET`、`DIRECT_SHARE_REGISTRATION_FAILED`、`UNKNOWN`、`OUT_OF_MEMORY`、未初期化 | 実機で起こせない。文言は層 1（設計 v6 6 章） |
| 複数のスレッドから同時に呼ぶ、Manager を壊す前後、変わった入力（U+0085 だけの値、パディングの無い Base64、HEIF のアイコンなど） | 親課題の J-11 の範囲の外。設計 v6 5.3 に書いた。層 1 と層 2a で押さえられるものはそこで押さえる |
| Sharesheet の見た目（並び、色） | 期待値を決められない。端末のアプリで変わる |
| `AndroidShareManagerPlayerTests`（補助を使わない）で、退行により Sharesheet が開いたとき | 補助がいないので閉じられず、Unity のテストの実行が時間切れになるまで止まる。このクラスは補助のいない実行でも走ることを優先した。2.0.0 のこれらの経路は、Sharesheet を開く前に失敗を返す（設計 v6 4.2）。止まったときは、実行の記録（logcat）で分かる |
| 依頼から 10 秒ほどを過ぎて出る Sharesheet（UI のクラス） | 補助は、シートを見ないまま失敗したとき 10 秒見張ってから閉じ、出るのを待つのも 10 秒（補助の設計 v10 3.3）。それより遅く出るシートは閉じず、Unity は止まったまま、テストの実行の時間切れまで進まない。実機では Sharesheet は 1 秒ほどで出る（V-11）ので、受け入れる |

## 5. 実行結果

（1.x の記録と移行の後に書く）

| 日付 | 版（移行前 / 移行後） | バックエンド | Player | Share |
|---|---|---|---|---|
