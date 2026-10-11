# Android Share UI 自動テスト計画 v2

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Android Share のサンプル（`Runtime/UI/Android/Share/AndroidShareManagerExampleController.cs`）と `AndroidShareManager` |
| 同梱 AAR | C ABI 2.0.0（`Plugins/Android/VERSION.txt`: dist 1.13.0）。移行の間は 1.x（dist 1.12.0）も追加のピンで置く |
| 目的 | Android Share の C ABI 2.0.0 への移行で、ふつうの使い方の振る舞いが 1.x と同じであること、違いが設計 v6 5.3 の既知の差分にあることを、実機の画面を操作して確かめる |
| 実行 | `scripts/verify_unity_android.sh` の実機の PlayMode（層 2b）。テストは `Tests/PlayMode/AndroidSharePlayerTests.cs`（UI を使う。カテゴリ `AndroidUiDriver` をクラスに付ける）と `Tests/PlayMode/AndroidShareManagerPlayerTests.cs`（UI を使わない。カテゴリを付けない） |
| 設計 | `artifact/android/share/designs/2026-10-11-android-share-design-v6.md`（7.3 が層 2b。この計画は 7.3 に番号を振り、期待値を具体にしたもの）。保つ範囲は親課題の J-11（ふつうの使い方を保つ） |
| 画面の操作 | ホストの adb の補助（`artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v7.md`）。命令の名前はその 3.1、Sharesheet の命令の期限と閉じ方は 3.3、プライバシーは 3.4 |
| 移行前の基準 | 無い。Android の Share の UI テストは 1.x には無かった（`artifact/topics/device-verification-records/README.md`）。期待値は、1.x のコード（設計 v6 の 2 章と、設計 v6 4.2 の根拠の列）と既知の差分（設計 v6 5.3）から決めた |
| レビュー | `artifact/android/share/reviews/2026-10-11-android-share-ui-test-plan-review-v1.md`（補助の設計 v6 と補助のコードと一緒に見た） |

Dialog の UI テスト計画（`artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v2.md`）と同じく、サンプル画面を通すテスト（1 章）と、Manager を直接呼ぶテスト（2 章）に分ける。

**移行の前に、1.x のまま流して記録する**（設計 v6 5.5 の手順 3）。各表の「1.x」の列は、1.x で流すかと、予想される値。「同じ」は 2.0.0 の期待と同じ。実測が予想と違えば、その結果をこの列に書き、設計の 2 章と 5.3 を直す（設計書の新しい版にする）。移行の後は、2.0.0 の結果が「1.x と同じ」か「設計 v6 5.3 の既知の差分」のどちらかになることを確かめる。

**補助の要検証 V-11〜V-16 は確かめた**（2026-10-11、Pixel 6a、1.x の Manager。補助の設計 v7 の 9 章）。

- Sharesheet が出ている間は Unity が止まる。依頼を先に出す形で正しい
- このアプリ自身は、Player の名前で共有先に出る（Android™ Profile では「Unity NativeToolkit」）。押すと選択のイベントが届き、Player が戻る
- `uiautomator dump` は、Sharesheet のプレビューの本文と Chooser Action のラベルを読める
- 登録した Direct Share の共有先は Sharesheet に出る
- 8 回スワイプした後も Back で閉じる
- Back が効かないときは、Sharesheet のプロセスを止めて閉じられる

**前提: 端末に、テストの Player と同じ名前のアプリが無いこと。** あると補助がそちらを押すことがある（1 回目の実行で、6 月に入れたサンプルアプリ `com.jonghyunkim.nativetoolkit.android` を押した。Android™ Profile でビルドしたもので、名前が同じだった。そのアプリは消した）。補助は、押した後に別のアプリが前面に来たら、このアプリを前に戻して `fail wrong-app` で答える（補助の設計 v7 3.1）。テストは止まらずに失敗する。

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

**Sharesheet の操作の約束**（3 章）: テストは補助への依頼を先に `Send` し、同じフレームでボタンを押し、`Wait`（120 秒）する。

| # | 操作 | 期待（移行後。`ResultTextBlock` の最後の値） | 1.x |
|---|---|---|---|
| AS-01 | `Send("close-sheet")` → ShareTextButton | `[event] Share plain text: Success`。依頼の答えが `ok`（Sharesheet が出て、閉じた） | 流す。同じ |
| AS-02 | 同上 → ShareUrlButton | `[event] Share URL: Success` | 流す。同じ |
| AS-03 | 同上 → ShareWithSubjectTitleButton | `[event] Share with subject and title: Success` | 流す。同じ |
| AS-04 | 同上 → ShareRichPreviewButton | `[event] Share with rich preview: Success` | 流す。同じ |
| AS-05 | 同上 → ShareImageButton | `[event] Share image: Success` | 流す。同じ |
| AS-06 | 同上 → ShareImagesButton | `[event] Share multiple images: Success` | 流す。同じ |
| AS-07 | 同上 → ShareFileButton | `[event] Share file: Success` | 流す。同じ |
| AS-08 | 同上 → ShareFilesButton | `[event] Share multiple files: Success` | 流す。同じ |
| AS-09 | `Send("tap-in-sheet", "Save")` → ShareCustomActionButton（API 34 以上） | 最後の値が `[event] ChooserAction: com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_SAVE`。途中の成功は、Manager の `ShareOperationCompleted` の成功 1 回で確かめる（`Op` 1、`Action` 1） | 流す。同じ |
| AS-10 | `Send("tap-in-sheet", <このアプリの名前>)` → ShareWithCallbackButton | `[onSelected] Selected: <このアプリのパッケージ名>`（サンプルの per-call が共通イベントの表示を上書きする）。Player が前に戻る（補助の前面の確かめが `ok`）。`Op` 1、`Selected` 1 | 流す。同じ |
| AS-11 | `Send("no-sheet", "5000")` → ShareInvalidFileButton | `[event] Share invalid file (error handling demo): Failed\nError: File not found: /invalid/path/that/does/not/exist/sample.txt`。依頼の答えが `ok`（Sharesheet は出ない） | 流す。同じ |
| AS-12 | RegisterDirectShareTargetButton → RemoveDirectShareTargetButton（Sharesheet は出ない） | `[event] Register Direct Share target: Success`、続いて `[event] Remove Direct Share targets: Success`。`Op` 2 | 流す。同じ |
| AS-13 | Home を押す | トップメニューに戻る。サンプルの `OnDisable` が `CancelPendingShareCallback` を呼ぶので、`Op`（`cancelPendingShareCallback` の成功）が 1 回増える（`AndroidShareManagerExampleController.cs:81-90`）。もう一度 Share の画面を開いて AS-01 が通る | 流す。同じ |

- AS-10 の `<このアプリの名前>` は Player の名前。テストは実行の中で `PackageManager.getApplicationLabel` で読む（Android™ Profile では「Unity NativeToolkit」、Test Android™ Profile では「unity-native-plugin」。V-12）
- サンプルの画像はプロジェクトの `Assets/Resources/Images/share_sample_image.png` から作る（Android™ Profile の Player にだけある）
- AS-12 は、途中で失敗しても、後片付けでサンプルの共有先 `native_toolkit_sample_target`（`AndroidShareManagerExampleController.cs:16`）を消す

## 2. Manager を直接呼ぶテスト

サンプルでは出せない値（失敗の経路、Chooser Action の整え方、時点）を確かめる。Manager の公開 API を直接呼ぶ。イベントの記録は Dialog と同じく文字列（`Op|<operation>|<isSuccess>|<errorMessage>`、`Selected|<package>`、`Action|<actionId>`）で照らす。

### 2.1 Sharesheet を開く操作の失敗（`AndroidShareManagerPlayerTests`。UI を使わない）

| # | 呼び方 | 期待（移行後。`ShareOperationCompleted` と per-call） | 1.x |
|---|---|---|---|
| AS-21 | `ShareFile(filePath = "/data/local/tmp/ntk-none.txt")` | `Op|shareFile|False|File not found: /data/local/tmp/ntk-none.txt` が 1 回 | 流す。同じ |
| AS-22 | `ShareFile(filePath = "/system/etc/hosts")`（在るが FileProvider の範囲外） | `Op|shareFile|False|File cannot be shared: /system/etc/hosts. Ensure the file is in a supported directory.` | 流す。同じ |
| AS-23 | `ShareFiles([persistentDataPath の在るファイル, "/data/local/tmp/ntk-none.txt"])` | `Op|shareFiles|False|File not found: /data/local/tmp/ntk-none.txt`（存在しない最初のパス。設計 J-S9） | 流す。同じ（1.x も原因のパス） |
| AS-24 | `ShareFiles([在るファイル, "/system/etc/hosts"])` | `Op|shareFiles|False|File cannot be shared: <在るファイル>, /system/etc/hosts. Ensure ...`（全部のパス。設計 5.3） | 流す。`File cannot be shared: /system/etc/hosts. Ensure ...`（原因のパス。既知の差分） |
| AS-25 | `ShareText(text = "")` | `Op|shareText|False|Failed to shareText: text is required` | 流す。同じ |
| AS-26 | `ShareText(text = "   ")` | 同上 | 流す。同じ |
| AS-27 | `ShareFile(filePath = "")` | `Op|shareFile|False|Failed to shareFile: filePath is required` | 流す。同じ |
| AS-28 | `ShareFiles(filePaths = new string[0])` | `Op|shareFiles|False|Failed to shareFiles: filePaths must not be empty` | 流す。同じ |
| AS-29 | `ShareFiles(filePaths = [persistentDataPath の在るファイル, null])` | `Op|shareFiles|False|Failed to shareFiles: filePaths must not contain null` | 流す。記録する（設計 5.3 の「要確認」。1.x は先頭から順に変換する〈`ShareRepositoryImpl.kt`（タグ `1.12.0`）:107〉ので、在るファイルを先に置いて null の要素まで届かせる） |
| AS-30 | `RegisterDirectShareTarget(id = "", label = "L", iconBase64 = <PNG>)` | `Op|registerDirectShareTarget|False|Failed to registerDirectShareTarget: id is required` | 流す。同じ |
| AS-31 | `RegisterDirectShareTarget(iconBase64 = "!!!")` | `Op|registerDirectShareTarget|False|Invalid icon data for Direct Share target: <id>` | 流す。同じ |
| AS-32 | `RegisterDirectShareTarget(iconBase64 = <"abc" の Base64>)`（Base64 は読めるが画像でない） | 同上（`INVALID_ICON`） | 流す。同じ |
| AS-33 | `RemoveDirectShareTargets(ids = new string[0])` | `Op|removeDirectShareTargets|False|Failed to removeDirectShareTargets: ids must not be empty` | 流す。同じ |
| AS-34 | `RegisterDirectShareTarget`（正しい値）→ `RemoveDirectShareTargets`（同じ ID） | 成功が 1 回ずつ。UnityMain から呼び、戻った時点では 0 回（設計 J-S2） | 流す。同じ |
| AS-35 | 別のスレッドから `RegisterDirectShareTarget`（正しい値） | 呼び出し側に例外が出ず、成功が UnityMain で 1 回（設計 J-S16） | 流す。記録する（1.x の `AndroidJavaObject` を、アタッチしていないスレッドから呼べるかの裏が無い。例外が出たら、そのスレッドで `AndroidJNI.AttachCurrentThread` / `DetachCurrentThread` を呼ぶ形で記録し直す） |
| AS-36 | `ShareFile(payload = null!)` | `ArgumentNullException` が呼び出し側に出る。イベントは出ない | 流す。`NullReferenceException`（既知の差分） |

- このクラスは補助を使わない（補助のいない実行でも走る）。退行で Sharesheet が開くと、Unity が止まったまま誰も閉じない。この危険は受け入れる（4 章）
- Direct Share のテストは、後片付けで登録した共有先を消す（3 章の順）

### 2.2 Sharesheet を開く操作（`AndroidSharePlayerTests`。UI を使う）

| # | 呼び方 | 操作 | 期待（移行後） | 1.x |
|---|---|---|---|---|
| AS-41a | `ShareText(text = "ntk share preview 41", previewTitle = "ntk preview title 41")` | `Send("present-in-sheet", "ntk share preview 41")` を先に | `Op` 1（成功）。依頼の答えが `ok`（プレビューにテストの本文が出た） | 流す。同じ |
| AS-41b | AS-41a と同じ呼び方 | `Send("present-in-sheet", "ntk preview title 41")` | 同上（プレビューの題が出た。構造体の `text` より後ろの位置〈`title`、`subject`、`preview_title`〉の写しを、端末の上の Kotlin まで押さえる） | 流す。同じ。**Android 16 の Sharesheet が `EXTRA_TITLE` をプレビューの題として出すかを、ここで確かめる**。出なければ、AS-41b はやめて、計画の次の版に書く |
| AS-42 | `ShareText(text, chooserActions = [{label "A42", icon <PNG>, intentAction "ntk.a42"}])`（API 34 以上） | `Send("tap-in-sheet", "A42")` | `Op` 1（成功）、`Action|ntk.a42` が `ShareChooserActionTapped` → `onChooserAction` の順に 1 回 | 流す。同じ |
| AS-43a | `ShareText` に 4 つのアクション: 空白のラベル、Base64 でないアイコン（ラベル「B43」）、画像の印の無いアイコン（ラベル「C43」）、正しいもの（ラベル「A43」） | `Send("absent-in-sheet", "B43", "A43")` | `Op` 1（成功）。依頼の答えが `ok`（「A43」が出て、「B43」は出ない） | 流す。同じ（`ShareRepositoryImpl.kt`（タグ `1.12.0`）:250 で空白のラベル、:254 で Base64 でないもの、:255 で画像でないものを `continue` で飛ばす） |
| AS-43b | AS-43a と同じ呼び方 | `Send("absent-in-sheet", "C43", "A43")` | 同上（「C43」は出ない） | 流す。同じ |
| AS-44a | `ShareText` に同じ `intentAction`（`ntk.a44`）の 2 つ（ラベル「A44a」「A44b」） | `Send("absent-in-sheet", "A44b", "A44a")` | `Op` 1。2 つ目は出ない（設計 5.3） | 流す。操作を `Send("present-in-sheet", "A44b")` に替えて記録する。**2 つとも出る**（既知の差分。1.x は重複を除かない。`ShareRepositoryImpl.kt`（タグ `1.12.0`）:248-265） |
| AS-44b | AS-44a と同じ呼び方 | `Send("tap-in-sheet", "A44a")` | `Op` 1、`Action|ntk.a44` が 1 回 | 流す。同じ |
| AS-45 | `ShareText` に `intentAction = "android.intent.action.SEND"` のアクション（ラベル「S45」） | `Send("tap-in-sheet", "S45")` | `Op` 1。押しても `ShareChooserActionTapped` は来ない（`Action` 0。1.x と同じ。設計 4.2.1。3 章の「来ない」の確かめ） | 流す。同じ |
| AS-46 | `ShareText` に、PNG の印だけで中身が壊れたアイコンのアクション | `Send("no-sheet", "5000")` | `Op|shareText|False|Failed to shareText: a chooser action icon could not be read as an image.`。依頼の答えが `ok` | 流す。操作を `Send("close-sheet")` に替える。**そのアクションだけ飛ばして開く**（既知の差分。`ShareRepositoryImpl.kt`（タグ `1.12.0`）:255） |
| AS-47 | `ShareWithCallback(text, onStarted, onSelected)` | `Send("tap-in-sheet", <このアプリの名前>)` | `onStarted` に成功、`ShareCallbackReceived` に自分のパッケージ名が 1 回、`onSelected` に 1 回（`Op` 1、`Selected` 1） | 流す。同じ |
| AS-48 | `ShareWithCallback` | `Send("close-sheet")` | `onStarted` に成功。選択のイベントは来ない（`Selected` 0。3 章の「来ない」の確かめ） | 流す。同じ |
| AS-49 | `ShareWithCallback` → 同じフレームで `CancelPendingShareCallback()` | `Send("tap-in-sheet", <このアプリの名前>)` | 取り消しの成功と `onStarted` の成功が 1 回ずつ（`Op` 2。順は仮定しない。設計 5.3）。選択のイベントは来ない（`Selected` 0。取り消しに渡した `ulong` の ID が正しいことの確かめ。3 章の「来ない」の確かめ） | 流す。同じ（1.x も取り消しの後は来ない） |
| AS-50 | `Initialize()` を 2 回呼んだ後、AS-42 と AS-47 と同じ操作 | 同上 | アクションと選択のイベントが 1 回ずつ（リスナーが 1 回だけ登録されている。設計 J-S6） | 流す。同じ |
| AS-51 | 最初に `RemoveDirectShareTargets(["ntk_as51"])`（前の実行の残り）→ `RegisterDirectShareTarget(id "ntk_as51", label "NTK AS51 <run>", icon <PNG>)` → `ShareText` | `Send("present-in-sheet", "NTK AS51 <run>")` | 登録した共有先が Sharesheet に出る。`<run>` は補助の実行の ID の先頭 6 桁（前の実行の残りでは通らない） | 流す。同じ |
| AS-52 | 補助の `background 3000` と `no-sheet 10000` を同じフレームで `Send` する。別のスレッドが `background` の答えのファイルを待ち（`WaitForAnswerFile`）、後ろに回っている間に `AndroidShareManager.Instance.ShareText` を呼ぶ（Dialog の AD-27 と同じ形。設計 J-S16） | `Wait(background)`、`Wait(no-sheet, 120)` | `Op|shareText|False|Failed to shareText: the app is not in the foreground.`（設計 5.3）。`no-sheet` の答えが `ok`（戻った後も Sharesheet は出ない） | 流す。記録する（1.x は OS が起動を黙って止めるか、戻った後に遅れて Sharesheet が出るか〈そのときは `no-sheet` が閉じて `fail sheet-opened` を返す〉。どちらでもテストは止まらない。予想は設計 v6 5.3 の「要確認」） |
| AS-53 | 別のスレッドから `ShareText` | `Send("close-sheet")` | 呼び出し側に例外が出ず、成功が UnityMain で 1 回 | 流す。AS-35 と同じく記録する |

### 2.3 欠番

AS-14〜AS-20、AS-37〜AS-40 は欠番（章の区切りのため）。

## 3. テストの共通の約束

- カテゴリ `AndroidUiDriver` は `AndroidSharePlayerTests` のクラスに付ける（AS-01〜AS-13、AS-41〜AS-53）。`AndroidShareManagerPlayerTests`（AS-21〜AS-36）には付けない（補助がいない実行でも走る）。ゲートは、64 ビットの Android™ Profile の実行で、このカテゴリの skip が 0 であることを見る
- **Sharesheet の操作の約束**（設計 v6 7.3、補助の設計 v7 3.3・5 章）:
  - Sharesheet が出ている間は Unity が止まる（V-11）。テストは補助への依頼を先に `Send` し、同じフレームで Share を開き、`Wait`（120 秒）する。`Wait` は開いたのと同じフレームで始まり、Sharesheet が閉じて Unity が戻ってから進む。期限は `Wait` を始めた時点から数える
  - 1 つの依頼は、それだけで終わる操作（`tap-in-sheet`、`absent-in-sheet`、`present-in-sheet`、`close-sheet`、`no-sheet`）。補助は、失敗でも Sharesheet を閉じてから答える（Back、それでも閉じなければ Sharesheet のプロセスを止める。補助の設計 v7 3.3）。Sharesheet に `back` は使わない
  - UI のクラスで、Sharesheet が開かないはずの呼び出し（AS-11、C ABI の版の AS-46、AS-52）は、先に `no-sheet` を `Send` する。退行で開いても補助が閉じ、テストは失敗で終わる
  - `Send` した依頼は、失敗の経路でも必ず回収する（後片付けの `WaitForPending`。途中の失敗で止まらず、全部を回収してから失敗する）
- **イベントの数**: 各テストは、`Op`（`ShareOperationCompleted`）、`Selected`（`ShareCallbackReceived`）、`Action`（`ShareChooserActionTapped`）の期待の数を持つ（表の「`Op` 1」など。書いていないものは 0）。後片付けは 3 つを別々に照らす。`Op` の期待には、後片付けの `RemoveDirectShareTargets` の結果と、AS-13 の取り消しの結果を含める
- **「来ない」の確かめ**（AS-45、AS-48、AS-49 など）: 選択とアクションのブロードキャストは、Player が戻った後のフレームで届くことがある。`Wait` が戻った後、Player が `OnApplicationPause(false)` を受けるまで待ち（期限 10 秒）、そこから 2 秒待ってから数を照らす
- 後片付け（Dialog の UI テスト計画 v2 の 3 章と同じ形。順に）:
  1. 補助を使わない手順（AS-52 のスレッドを止める）
  2. このテストで登録した Direct Share の共有先を消し（AS-12 は `native_toolkit_sample_target`、AS-34、AS-51 は `ntk_as51`）、その結果を待つ（数を照らす前に。結果が次のテストに流れ込まないように）
  3. 補助を使う手順は `Safely` を通す: 回収していない依頼の `WaitForPending`。`count package=com.android.intentresolver` が 0 でなければ `close-sheet`。シーンを下ろす
  4. 3 種のイベントの数が期待に等しくなるまで（期限 5 秒）待ち、Player が戻った時点から 0.5 秒以上たってから照らす。結果がすべて UnityMain で出たことも照らす
  5. `PendingCallCountForTests` が 0（C ABI の版だけ。1.x の列では見ない）、補助の `PendingCount` が 0。どちらのクラスでも見る
  6. 購読を外し、取り出し口の列を空にし、シーンを下ろす。これらは手順が失敗しても必ず行う
- 押す文言は、テストが決めた値（このアプリの名前、テストの Chooser Action のラベル、テストの本文）だけにする。ほかのアプリの共有先と連絡先には触らない（補助の設計 v7 3.4）
- 待ち時間: 補助の Sharesheet の命令の答えは 120 秒（補助の設計 v7 3.3）、ほかの命令は 20 秒（`background` を含む）、結果のイベントは 10 秒

## 4. 確かめないもの

| もの | 理由 |
|---|---|
| ほかのアプリへの共有（本当に届くか、届いた中身） | ほかのアプリのデータに触れる。共有の中身は AS-41a・AS-41b のプレビューと、自分を選ぶ AS-10 / AS-47 で押さえる |
| API 33 以下の端末（Chooser Action が出ないこと） | 手元に端末が無い。手動（設計 v6 7.4） |
| `NO_SHARE_TARGET`、`DIRECT_SHARE_REGISTRATION_FAILED`、`UNKNOWN`、`OUT_OF_MEMORY`、未初期化 | 実機で起こせない。文言は層 1（設計 v6 6 章） |
| 複数のスレッドから同時に呼ぶ、Manager を壊す前後、変わった入力（U+0085 だけの値、パディングの無い Base64、HEIF のアイコンなど） | 親課題の J-11 の範囲の外。設計 v6 5.3 に書いた。層 1 と層 2a で押さえられるものはそこで押さえる |
| Sharesheet の見た目（並び、色） | 期待値を決められない。端末のアプリで変わる |
| `AndroidShareManagerPlayerTests`（補助を使わない）で、退行により Sharesheet が開いたとき | 補助がいないので閉じられず、Unity のテストの実行が時間切れになるまで止まる。このクラスは補助のいない実行でも走ることを優先した。2.0.0 のこれらの経路は、Sharesheet を開く前に失敗を返す（設計 v6 4.2）。止まったときは、実行の記録（logcat）で分かる |

## 5. 実行結果

（1.x の記録と移行の後に書く）

| 日付 | 版（移行前 / 移行後） | バックエンド | Player | Share |
|---|---|---|---|---|
