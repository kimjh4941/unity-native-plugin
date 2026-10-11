# 実装結果レポート v4（Android Dialog の C ABI 2.0.0 への移行）

## 基本情報

- 日付: 2026-10-11
- 機能名: dialog
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-13
- 計画ファイル: `artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`
- UI テスト計画: `artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v2.md`（v1 に AD-36・AD-37 を足し、カテゴリと後片付けの約束を実装に合わせた版）
- UI の補助の設計: `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v5.md`
- 親課題: `artifact/topics/android-c-abi-2/README.md`
- 前の版: v1・v2・v3（それぞれ実装レビュー v1・v2・v3 の対象）。この版は、レビュー v3（`artifact/android/dialog/reviews/2026-10-11-android-dialog-implementation-feature-review-v3.md`）の指摘を直した後の状態。レビュー v1 への対応は 9 章、v2 は 10 章、v3 は 11 章
- 同梱: native-toolkit dist 1.13.0（`android-native-toolkit-2.0.0.aar`、`android-native-toolkit-capi-2.0.0.aar`）。移行の間は dist 1.12.0 の 1.3.0 の 2 本も追加のピンで置く

## 0. 状態サマリー

設計 v4 の 5.5 の手順 1〜9 を終え、実装レビュー v1〜v3 の指摘を直した（9〜11 章）。レビュー v3 で A は 0 件になった。手順 4〜9 はまだコミットしていない（再レビューと利用者の確認待ち）。

| 手順 | 内容 | コミット | 状態 |
|---|---|---|---|
| 1 | Android の 4 機能の型を `Runtime/Android/<Feature>/` に移す（21 ファイル） | `44f206d` | 済み |
| 2 | UI の補助（`android_ui_driver.py`、`AndroidUiDriver.cs`、`verify_unity_android.sh` の起動と判定） | `dcf7760` | 済み |
| 3 | 1.x のまま `AndroidDialogPlayerTests` を流して記録（42 / 42） | `dcf7760`、`d9714e4` | 済み |
| 4・5 | `AndroidDialogCApi`（構造体、純粋な関数、`extern`）、共通部の `ReadUtf8OrNull` と `AndroidNativeCallbackQueue`、層 1 | 未コミット | 済み |
| 6 | `AndroidDialogManager` の置き換え、層 2a、サンプルのコントローラーのログと画面 | 未コミット | 済み |
| 7 | 2.0.0 の列のテスト（AD-21、26、27、32、34）を足し、全体を流す | 未コミット | 済み |
| 8 | `common.md`、`csharp.md`、`testing.md` | 未コミット | 済み |
| 9 | 実装結果ファイル（この文書） | 未コミット | 済み |

## 1. 実装サマリー

### 1.1 計画ファイル由来の実装

- **2 層（Bridge + Manager）と共通部。** `Runtime/Android/Common/AndroidNativeToolkitCApi.cs`（スパイクで作った共通部。`ReadUtf8OrNull` を足した）、
  `Runtime/Android/Common/AndroidNativeCallbackQueue.cs`（取り出し口）、`Runtime/Android/Dialog/AndroidDialogCApi.cs`（Bridge）、`AndroidDialogManager`
- **ガード。** 型と純粋な関数は `UNITY_ANDROID || UNITY_EDITOR`。`DllImport`、結果の読み取り `Read`、受け口とその関数ポインタは `UNITY_ANDROID && !UNITY_EDITOR`
- **J-D1** `user_data` に種類（`Kind`、1〜6）を入れ、`release` は NULL。要求の ID は使わない（AD-26 のテストだけが取り消しに使う）
- **J-D2** 受け口は全体を `try/catch` で囲み、`Read` → `ntk_dialog_result_free` → `AndroidNativeCallbackQueue.Post(() => Instance.Raise(answer))`。取り出し口は `DontDestroyOnLoad` の `MonoBehaviour` と `ConcurrentQueue<Action>` で、`Update` は始めの件数だけ走らせる。`UnityMainThreadDispatcher` は使わない
- **J-D3** `checkedItem` が `-1` 未満か項目数以上 → 同期の失敗 `"checkedItem is out of range."`
- **J-D4** `checkedItems` の長さを項目数に合わせる（足りない分は `false`、余りは捨てる）
- **J-D5** 閉じたときの選択は単一・複数とも `null`
- **J-D6** Editor では検査に通れば `LogWarning`（`"X can only be called on an Android device."`）だけでイベントは出さない
- **J-D7** ネイティブを呼ぶ前と入口の失敗は `Show*` の中で同期に、受け付けた後の失敗は取り出し口の `Update` で
- **J-D8** `NOT_FOREGROUND` の文言（AD-27 で実機で起こした）
- **J-D9** ネイティブが使えないときの同期の失敗。32 ビットのビルドはスパイクの `AndroidArchitectureCheck`（`5d80d62`）で止まる
- **J-D10** `Initialize()` は使えるかを `Debug.Log` するだけ。何度呼んでもよい
- **J-D12** 項目の `null` の要素 → 同期の失敗 `"<引数名> cannot contain null."`
- **J-D13** 結果として返す失敗は `LogWarning`。`LogError` は入口の `INVALID_PARAMETER` / `NOT_SUPPORTED`、C# の例外、受け口の例外だけ
- **J-D14** 公開 API は UnityMain から呼ぶ前提を XML コメントに書いた
- **結果のログ**（4.4）: ボタンの文字・添字・真偽の配列は出し、入力・利用者名・パスワードは長さだけ。サンプルのコントローラーも同じで、画面はパスワードだけ長さ（`(<n> characters)`）

### 1.2 実装時の追加判断

- **`ReadUtf8OrNull(IntPtr, UIntPtr)`。** C ABI の `size_t` をそのまま受け、`IntPtr.Zero` なら `null`、そうでなければ長さ 0 でも `""`。否定の入力が `null`、閉じたときが `""`（4.3）の区別をここで守る
- **関数ポインタと種類を `internal` にした。** `AndroidDialogManager.s_callbackPointer` と `KindData(Kind)`。層 2b の AD-26（取り消し）と AD-27（別のスレッドから Bridge を直接呼ぶ）が、Manager と同じ受け口を通すため（設計 5.1 の表のとおり）
- **ログの入口。** Windows の移行と同じく、`internal static` の Bridge と共通部の変換には入口の `Debug.Log` を付けず、公開 API の Manager に置いた。`csharp.md` にこの例外（純粋な変換）と、取り出し口の空のフレームの例外（5.4）を書いた
- **後片付け（設計 7.3）。** v1 では 1 秒待って列を空にする形にし、その理由に「購読を先に外すので、遅れた完了は次のテストに数えられない」と書いたが、誤りだった（レビュー v1 の R1-1）。v2 で、各テストが受け付けさせた要求を `Request` で数え、イベントの数と照らす形にし、片付けを `finally` に置いたが、**その `finally` は失敗のときに走らなかった**（Unity Test Framework は入れ子の手順を平らにして回し、入れ子が投げると外側のイテレーターを再開も `Dispose` もしない。レビュー v2 の R2-1。`EnumeratorHelper.ProgressOnEnumerator` で確認）。v3 の形: UI の手順は `Safely`（入れ子を自分で辿り、投げたものを貯める）で回し、購読を外す・列を空ける・シーンを下ろすは必ず行い、最後に最初の失敗を投げ直す。AD-27 のスレッドと AD-28 の向きはフィールドに持ち、後片付けの先頭（`yield` の前）で止める・戻す。`SetUp` でも購読を張り直して列を空ける。要求は呼び出しが戻った後に数える（投げた呼び出しを数えない）
- **カテゴリ `AndroidUiDriver` はクラスに付けた。** UI テスト計画 v1 の 3 章は AD-01〜AD-28 に付けるとしたが、AD-29〜AD-34 も補助の `count` で「ダイアログが出ない」を確かめ、後片付けも補助を使う。全 36 本がゲートの skip の数え方に入る（UI テスト計画 v2 で直した）
- **AD-28 の回し方。** 補助の `rotate`（`user_rotation`）では Unity の自動回転が回らなかったので、テストが `Screen.orientation` で回し、補助の `rotation` で表示を確かめる形にした（1.x の記録のときに決めた。UI テスト計画 AD-28）
- **AD-27 は確実に起こせた。** 補助の `background 3000` で Activity が `STOPPED` になったのを待ってから、別のスレッドが Bridge を呼ぶ。Test Runner の接続は一時停止の間も切れなかった（補助の設計の V-8）。層 1 だけに下げる必要はなかった。v3 で、スレッドを起こす前に補助の準備を済ませ（単独で流しても答えのファイルの場所が決まっている）、スレッドの待ちは中止の合図を見る
- **層 2a の配送のテストを `verify_unity_android.sh` に入れた。** スクリプトが Editor の PlayMode（`-testPlatform PlayMode`）も流すようにした（設計 5.1 の表）。あわせて doctest を `PYTHONDONTWRITEBYTECODE=1` で流し、`scripts/__pycache__` を作業ツリーに残さないようにした
- **`csharp.md` の同期の決まりを消した。** 決まりが挙げる `.github/instructions/csharp-coding.instructions.md` は、2026-05-23 の `51b60ac`（入口を薄くしたとき）で消えていた。利用者の確認を経て、決まりの方を消した（2026-10-11）

## 2. 変更ファイル

### 2.1 新規作成

- `artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v2.md`（AD-36、AD-37。カテゴリと後片付けの約束）

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeCallbackQueue.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Dialog/AndroidDialogCApi.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidDialogCApiTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidNativeToolkitCApiTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidDialogManagerEditorTests.cs`
- （手順 2・3 でコミット済み）`scripts/android_ui_driver.py`、`Tests/PlayMode/AndroidUiDriver.cs`、`Tests/PlayMode/AndroidSampleScreenDriver.cs`、`Tests/PlayMode/AndroidDialogPlayerTests.cs`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Dialog/AndroidDialogManager.cs`（`AndroidJavaObject` / `AndroidJavaProxy` を消して書き直し。公開 API・イベント・既定値は同じ）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeToolkitCApi.cs`（`ReadUtf8OrNull`）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Android/Dialog/AndroidDialogManagerExampleController.cs`（ログは長さ、画面はパスワードだけ長さ）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidDialogPlayerTests.cs`（既知の差分の期待値と、2.0.0 の列の 7 本〈AD-21、26、27、32、34、36、37〉。後片付けで要求とイベントの数と、イベントのスレッドを照らし、失敗しても片付ける）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidUiDriver.cs`（`WaitForAnswerFile` に `CancellationToken`）
- `artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v1.md`（5 章の実行結果の追記だけ）、`artifact/topics/android-c-abi-2/README.md`（5 章の段の状況と 6.6）
- `scripts/verify_unity_android.sh`（Editor の PlayMode、doctest の `PYTHONDONTWRITEBYTECODE`）
- `agent-rules/coding-rules/common.md`（「Unity Bridge パターン > Android（native-toolkit の C ABI）」、Runtime のディレクトリ、`Runtime/Android/Common/` の表、既知の逸脱の場所、閉じたときの `errorMessage == ""` の例外）
- `agent-rules/coding-rules/csharp.md`（`LogWarning` の規則を Android の C ABI に、ログの例外 2 つ、古い同期の決まりの削除）
- `agent-rules/coding-rules/testing.md`（2 節の Android の行、3 節のガードの群、7 節の層 0・1・2a・2b・3）

### 2.3 非変更（対象だが未変更）

- `Runtime/Resources/UI/Android/Dialog/*`、`NativeToolkitSampleNavigator.cs`、`NativeToolkitEditorWindow.cs`: コントローラーの型名で参照するだけ（設計 5.1）
- `UnityMainThreadDispatcher`: 使わなくしただけで変えていない。ロックを持ったまま処理を走らせる問題は `artifact/topics/dispatcher-runs-under-lock/README.md`
- 設計書 v4・UI テスト計画 v1: 版の付いた文書は上書きしない。ただし UI テスト計画の 5 章（実行結果）は追記の欄なので、移行後の結果を書き足した

## 3. エラー契約反映

### 3.1 エラーケース実装反映（設計 v4 の 6 章）

| # | 状況 | 実装 | 検証 |
|---|---|---|---|
| E-1 | 題名・本文・項目が空 | 同期、今の文言、`LogWarning` | 1（`CheckTitleAndMessage`、`CheckItems`）、2a（`EmptyTitle_*`、`EmptyMessage_*`）、2b（AD-29〜31）合格 |
| E-2 | `checkedItem` の範囲外（J-D3） | 同期 `"checkedItem is out of range."` | 1（`-1`・`0`・項目数 - 1 は通り、`-2` と項目数は失敗）、2a、2b（AD-33）合格 |
| E-3 | `checkedItems` の長さ違い（J-D4） | 項目数に合わせて渡す | 1（`FitChecked`）、2b（AD-21 で `[T,F,F,F]`、AD-22 で `[T,F]`）合格 |
| E-4 | 項目の `null` の要素（J-D12） | 同期 `"... cannot contain null."` | 1、2a（`NullItem_Fails`）、2b（AD-32、AD-34）合格 |
| E-5 | ネイティブが使えない | 同期、4.4 の文言 | 1（例外の分類、版判定、文言）合格。実物は起こしていない |
| E-6 | 未初期化 | 同期、4.4 | 1（文言）合格。起こせない |
| E-7 | 入口の `INVALID_PARAMETER` / `NOT_SUPPORTED` | 同期、`LogError` | 1（文言、`IsRequestDefect`）合格。起こせない（構造体の位置は 1 で固定） |
| E-8 | `NOT_FOREGROUND` | 非同期、4.4 | 1（文言）、2b（AD-27）合格 |
| E-9 | `HOST_START_FAILED` / `SHOW_FAILED` / `CANCELED_BY_SYSTEM` / 完了の `NOT_INITIALIZED` | 非同期、4.4 | 1（文言）合格。起こせない |
| E-10 | `CANCELED` | 非同期、4.4 | 2b（AD-26。`ntk_dialog_cancel` で 1 回）合格 |
| E-11 | 閉じた（Back・外側） | `("Cancel", …, true, "")`、選択は `null` | 1（写し）、2b（AD-10〜12、14、16、18）合格 |
| E-12 | C# の例外 | 同期、`"Internal error: ..."`、`LogError` | 形はコードレビュー |
| E-13 | 受け口の中の例外 | イベントは出ない、`LogError` | コードレビュー。起こせない |

### 3.2 イベントの返却仕様

- 1 呼び出しにイベント 1 回。同期の失敗は `Show*` の中、受け付けた後の完了は取り出し口の `Update`（UnityMain）で出る
- `Show*` が戻った時点ではイベントは 0 回（AD-25）
- 購読者の例外は捕まえない。同期の失敗では呼び出し側に出る（2a の `SubscriberException_*`）。完了では取り出し口の `Update` から上に出る（旧の dispatcher と同じ）
- Manager を無効にしても完了は届く。Manager を壊してから完了が来ると、新しく作られた `Instance` に出る（2a の `Completion_*`。旧の dispatcher の経路と同じ）

### 3.3 success 時契約

- `isSuccessful == true` のとき `errorMessage` は、ボタンで閉じれば `null`、Back・外側で閉じれば `""`（1.x と同じ）。2b の AD-01〜AD-24、AD-28 で確認

### 3.4 既知の差分（マニュアルへ引き継ぐ）

設計 v4 の 5.3 の表に、1.x の記録（UI テスト計画 5 章の 4 回目）と移行後の実測を反映したもの。

| 差分 | 1.x | 2.0.0 移行後 | 実測 |
|---|---|---|---|
| 名前空間とフォルダ | `JonghyunKim.NativeToolkit.Runtime.Dialog`、`Runtime/Dialog/` | `JonghyunKim.NativeToolkit.Runtime.Android.Dialog`、`Runtime/Android/Dialog/`。利用者は `using` を書き換える。Clipboard・Notification・Share の Android の型も同じ | - |
| 閉じたときの選択（J-D5） | 単一選択は今の選択、複数選択は今の状態 | どちらも `null` | AD-12（1.x は `1`）、AD-14（1.x は `[T,F,F]`） |
| 範囲外の `checkedItem`（J-D3） | ダイアログが出て何も選ばれず、肯定で範囲外の値（`2`）がそのまま返った | 同期の失敗 `"checkedItem is out of range."`。ダイアログは出ない。Editor でも同じ | AD-33 |
| 長い `checkedItems`（J-D4） | 渡した長さのまま返った（項目 2 つ・長さ 4 で `[T,F,T,F]`） | 項目数に切る（`[T,F]`） | AD-22 |
| 短い `checkedItems`（J-D4） | 流していない（main で例外になりアプリが落ちると推定） | `false` で埋める | AD-21 |
| 項目の `null` の要素（J-D12） | 流していない（Java に渡り落ちうる）。Editor では検査に通り `LogWarning` だけ | 同期の失敗 `"singleChoiceItems cannot contain null."` / `"multiChoiceItems cannot contain null."`。Editor でも同じ | AD-32、AD-34 |
| 文字列の途中の U+0000 | そのまま運んだ | そこで切れる | 確かめていない（C ABI の形） |
| 対になっていないサロゲート | そのまま | 入力は C# の UTF-8 の変換で、出力はライブラリで U+FFFD | 層 1（入力の側） |
| 後ろから呼ぶ（J-D8） | `"Failed to <method>: <例外の文言>"` | `"Failed to <method>: the app is not in the foreground."` | AD-27 |
| 宿主や Activity が壊れた | イベントは出ない | `"Failed to <method>: the dialog was closed by the system."` | 起こせない |
| 前面が `FragmentActivity` でない | `"Failed to cast context to FragmentActivity"` | 透明な宿主に出る | 起きない（Unity の Activity は `FragmentActivity`） |
| ネイティブが使えない（J-D9） | 無い（ブリッジは Java なので 32 ビットでも動いた） | **32 ビット（ARMv7）を含むビルドはできない**（Gradle の前に止まる）。Mono も ARMv7 だけなので使えない。対応の範囲は IL2CPP + ARM64（と x86_64） | `AndroidArchitectureCheckTests`、ARMv7 のビルドが新しい文言で止まる（親課題 README） |
| エラーの文言 | 例外の文言を含む | 下の表の固定の文言 | 層 1 |
| ログ（J-D13） | 検査の失敗は `LogError`、入力とパスワードを出した | 検査の失敗は `LogWarning`、入力・利用者名・パスワードは長さだけ | AD-29〜31（`LogAssert` なしで合格） |
| サンプルの画面 | パスワードをそのまま出した | パスワードだけ長さ（`(6 characters)`）。入力と利用者名はそのまま | AD-07 |
| `Initialize()`（J-D10） | ブリッジを取り、リスナーを登録した | 使えるかをログに出すだけ。呼ばなくてよい | - |

**エラーの文言（設計 v4 4.4。マニュアルの表に写す）。** `<method>` は 1.x のブリッジのメソッド名（`showDialog`、`showConfirmDialog`、`showSingleChoiceItemDialog`、`showMultiChoiceItemDialog`、`showTextInputDialog`、`showLoginDialog`）。

| 原因 | 時点 | `errorMessage` |
|---|---|---|
| 題名・本文・項目が空 | 同期 | `"Title cannot be null or empty."` / `"Message cannot be null or empty."` / `"singleChoiceItems cannot be null or empty."` / `"multiChoiceItems cannot be null or empty."` |
| 項目の `null` の要素 | 同期 | `"singleChoiceItems cannot contain null."` / `"multiChoiceItems cannot contain null."` |
| `checkedItem` の範囲外 | 同期 | `"checkedItem is out of range."` |
| ネイティブが使えない | 同期 | `"Failed to <method>: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only)."` |
| 未初期化 | 同期 | `"Failed to <method>: the native library is not initialized yet."` |
| 要求が拒まれた（実装の誤り） | 同期 | `"Failed to <method>: the request was rejected (INVALID_PARAMETER)."`（または `NOT_SUPPORTED`） |
| 入口のそのほか | 同期 | `"Failed to <method>: <名前>."`（`OUT_OF_MEMORY`、`UNKNOWN` など） |
| 後ろから呼んだ | 非同期 | `"Failed to <method>: the app is not in the foreground."` |
| 宿主を起こせない | 非同期 | `"Failed to <method>: the dialog host could not be started."` |
| 出せない | 非同期 | `"Failed to <method>: the dialog could not be shown."` |
| システムが閉じた | 非同期 | `"Failed to <method>: the dialog was closed by the system."` |
| 取り消し（公開 API では起きない） | 非同期 | `"Failed to <method>: the dialog was canceled."` |
| 完了のそのほか | 非同期 | `"Failed to <method>: <名前>."` |
| C# の例外 | 同期 | `"Internal error: <例外の文言>"` |

## 4. ビルド結果

- 実行コマンド:
  - `bash scripts/verify_unity_android.sh`（doctest、Editor の EditMode / PlayMode、Android™ Profile の IL2CPP ARM64 のテスト用 Player のビルドと実機の PlayMode、UI の補助の起動と判定）
  - `bash scripts/verify_unity_android.sh --as-package-user`（Test Android™ Profile。define とサンプルのシーンが無い、パッケージの利用者の条件）
- 結果: SUCCESS（最終回 2026-10-11、レビュー v3 の修正の後。出力は作業用フォルダの `verify-fix3`）
- 補足:
  - 実機: Pixel 6a（Android 16）、IL2CPP、ARM64
  - `!UNITY_EDITOR` 側（`extern`、`Read`、受け口）は Player のビルドでだけコンパイルされる。層 2b の実行が層 0 を兼ねる
  - UI の補助: 依頼 118 件、終了コード 0。実行の後、アニメーション・画面の点灯・自動入力の設定は元に戻った

## 5. テスト結果

- 実行したテスト: EditMode 全体、Editor の PlayMode 全体、実機の PlayMode（Android™ Profile と Test Android™ Profile）
- 結果サマリー（最終回）:
  - EditMode: 1229 / 1230、skip 1（`WindowsClipboardSampleFixtureTests.TemporaryFilePathsAreAbsoluteAndUseBackslashes`。Windows の Editor だけで意味のある既存の skip）。うち Android の Dialog の層 1 は 62: `AndroidDialogCApiTests` 48、`AndroidNativeToolkitCApiTests` 14
  - Editor の PlayMode: 226 / 237、skip 11（`AndroidClipboardManagerIntegrationTests` の 11 本。実機専用で Editor では skip する既存のもの。同じ 11 本は実機で通った）。うち `AndroidDialogManagerEditorTests` 16 / 16
  - 実機（Android™ Profile）: **49 / 49**、skip 0。`AndroidDialogPlayerTests` 36、`AndroidClipboardManagerIntegrationTests` 11、`AndroidNativeToolkitCApiPlayerTests` 2
  - 実機（Test Android™ Profile）: 40 / 49、skip 9（AD-01〜AD-09。サンプルのシーンが無いので Ignore。想定どおり）
- 1.x との照合（設計 v4 5.5 の手順 7）: 1.x で流した 29 件のうち、期待値を変えたのは AD-07、AD-12、AD-14、AD-22、AD-23、AD-33 と、AD-29〜31 の `LogAssert` の削除。どれも 3.4 の表にある。残りの 20 件は期待値を変えずに通った。2.0.0 だけの 5 件（AD-21、26、27、32、34）も通った
  - AD-23（`checkedItem = -1`）: 1.x も 2.0.0 も `null`。1.x の記録のテストは値を記録するだけだったので、期待値を書いたテストに直した（振る舞いの差分ではない）
- 失敗時の対応: 移行後の 1 回目で全件合格。失敗は無い（1.x の記録の 1〜3 回目の失敗と対応は UI テスト計画 5 章）

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 | 備考 |
|---|---|---|---|---|
| 構造体の大きさと全フィールドの位置 | `Tests/Runtime/AndroidDialogCApiTests.cs` | 6 つの要求の構造体 | ○ | 期待値は native-toolkit の C ヘッダの並び（Pack 8） |
| 要求の ID が 64 ビット | 同上 | `ResultCallback` の 2 番目の引数 | ○ | 反射。`extern` は Editor でコンパイルされないので delegate で見る |
| 構造体の組み立て | 同上 | `Build*` | ○ | `struct_size`、`reserved*`、`not_cancelable` の向き、UTF-8 の読み戻し、`checked` の短い・長い・`null`・空 |
| 検査の文言 | 同上 | `CheckTitleAndMessage`、`CheckItems`、`IsCheckedItemInRange` | ○ | |
| 結果の写し | 同上 | `To*Args` | ○ | 6 種類 × 肯定・否定・閉じた・失敗 |
| エラーの名前と文言 | 同上 | `ErrorName`、`EntryFailureMessage`、`CompletionFailureMessage`、`NativeUnavailableMessage` | ○ | 全値と想定外の値 |
| 版判定・例外の分類・UTF-8・NULL と空 | `Tests/Runtime/AndroidNativeToolkitCApiTests.cs` | 全 14 | ○ | サロゲートペア、対になっていないサロゲート |
| 検査・Editor・購読者の例外・配送 | `Tests/PlayMode/AndroidDialogManagerEditorTests.cs` | 全 16 | ○ | 項目の空、6 種類の Editor の警告、`Post` の中でイベントを出さない、無効・壊れた Manager（新しい `Instance` に届く）、取り出し口が壊された・無効にされた・GameObject ごと非アクティブにされたときの `LogError` と作り直し、列に入った後で無効にされたときの `OnDisable` の報告、別のスレッドからの `EnsureCreated`、1 フレームの件数。配送は本番と同じ `PostAnswer` を通る |
| サンプル画面 | `Tests/PlayMode/AndroidDialogPlayerTests.cs` | AD-01〜AD-09 | ○ | AD-07 は期待値を変えた（3.4） |
| 閉じたとき・否定・外側 | 同上 | AD-10〜AD-20 | ○ | AD-12、AD-14 は期待値を変えた（3.4） |
| 値の写し | 同上 | AD-21〜AD-24 | ○ | AD-21 は 2.0.0 だけ、AD-22 は期待値を変えた |
| 時点と完了の経路 | 同上 | AD-25〜AD-28 | ○ | AD-26、AD-27 は 2.0.0 だけ |
| 同期の失敗 | 同上 | AD-29〜AD-34 | ○ | AD-32、AD-34 は 2.0.0 だけ、AD-33 は期待値を変えた |
| 取り出し口とスレッド | 同上 | AD-36、AD-37 | ○ | 2.0.0 だけ。取り出し口を壊した後の `Show*`、別のスレッドからの `Show*` |
| C ABI の共通部の結び付け | `Tests/PlayMode/AndroidNativeToolkitCApiPlayerTests.cs` | 2 本 | ○ | スパイクのテスト |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
|---|---|---|---|
| ネイティブが使えない実物（E-5） | - | - | `libntk.so` を抜いた Player を作る仕組みが無い。分類・版・文言は層 1 |
| 起こせない完了・入口の失敗（E-6、E-7、E-9） | - | - | 実機で起こせない。文言は層 1 |
| C# の例外・受け口の例外（E-12、E-13） | - | - | 起こせない。形はコードレビュー |
| 非 ASCII の入力 | - | - | 補助の `type` が英小文字と数字だけ。入力の UTF-8 は層 1、表示は AD-24 |
| 文字列の途中の U+0000 | - | - | C ABI の形で、C# では直さない |
| 後片付けの要求とイベントの照合（設計 7.3） | - | - | v2 で実装した（1.2）。未実施ではない |

## 6. Definition of Done

- 判定基準: ○ 実装・コード・テスト確認の範囲では OK / △ 一部 OK だが追加確認が必要 / × 未達 / - 対象外
- ○ 公開 API（メソッド、イベント、既定値）を変えずに C ABI 2.0.0 へ移した（名前空間の変更は J-D11 として承認済み）
- ○ 1.x で流した 29 件が、期待値を変えずに通るか、変えた所がすべて 3.4（設計 5.3）にある
- ○ J-D1〜J-D14 を実装し、層 1 / 2a / 2b で確かめた（E-5〜E-7、E-9、E-12、E-13 は層 1 とコードレビュー）
- ○ パッケージの利用者の条件（Test Android™ Profile）でもビルドでき、サンプルを使わないテストが通る
- ○ 既知の差分とエラーの文言を XML コメントとこの文書に書いた（3.4）
- ○ 後片付けで、1 回の呼び出しにイベント 1 回であること、イベントが UnityMain で出たことを全 36 本で照らし、テストが失敗しても片付けを済ませる（設計 7.3。v3・v4。後片付けの手順をわざと失敗させて確かめた。10.2）
- ○ `common.md`、`csharp.md`、`testing.md` を更新した

## 7. 残作業

- 移行のコミット（利用者の確認の後。push はしない）
- **直さない残件（理由つき。止める基準の 3）:**
  - R3-10 (a) 同じフレームで取り出し口を `Destroy`（遅延の破棄）した直後の `Show*` は、まだ有効に見えるので作り直さない。完了は次の `Show*` まで残るが、`OnDisable` が `LogError` を出すので無言ではない。利用者のコードが取り出し口を壊すこと自体がふつうでない
  - R3-10 (b) 利用者が取り出し口を無効な親の下に付け替えると、`SetActive(true)` では有効にならない。呼び出しのたびに警告が出る。同じくふつうでない条件で、無言ではない
  - R3-4 の「後片付けの Ignore はテストの失敗を隠さない」、R3-3 の「向きが戻らなければ後片付けで失敗」は、壊して落ちることを確かめていない（補助を止める・センサーを固める手段が無い）。形はコードレビュー
  - C2-1 の競合そのもの（スレッドの交わり）は起こせない。`OnDisable` 側の報告は 11.2 で、`Post` 側は 10.2 で確かめた
  - 設計 v4 7.3 の記述（向きを補助の `rotate` で回し `finally` で戻す、`ResetForTests` の後に購読を外す）は、実装（アプリから回して後片付けで戻す、購読を外してから列を空ける）と違う（レビュー v3 の C3-6、C3-7）。設計書は版の付いた文書なので直さず、実装の形は UI テスト計画 v2 の 3 章とこの文書の 1.2 にある
- **Windows にも C1-1 と同じ形（確保の後に一覧へ足す）がある**（2026-10-11 に確認。`Runtime/Windows/Common/WindowsNativeToolkitCApi.cs:84`、`Runtime/Windows/Dialog/WindowsDialogCApi.cs:511`、`Runtime/Windows/Clipboard/WindowsClipboardCApi.cs:648`）。この段では他のプラットフォームのファイルを変えない（P2）ので、Windows の別の作業で直す（Android と同じく共通部に `Alloc` を置く形）
- 次の機能: Share → Clipboard → Notification（親課題 README の段）。どれも 1.x で記録してから移す
- 4 機能を移し終えたら、追加のピン（dist 1.12.0 の 1.3.0 の 2 本）と JSON の層を消す
- マニュアル: Android の要件（ARM64 と IL2CPP だけ）、移行ガイド（名前空間、3.4 の差分、Configure Gradle Project の削除、32 ビットのビルドの停止）、`Documentation~/index*.md` の `using` とサンプルの手順。次の版の write-manual でこの文書の 3.4 を読む
- 4 機能の移行が終わったら native-toolkit-1d に報告する

## 8. ステップ 9 実行確認

- 提示文:
  - 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して次工程へ進む
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答

## 9. 実装レビュー v1 への対応

レビュー v1 は、サブエージェントが A 2 件（R1-5、R1-6）・B 7 件・C 5 件・区分なし 2 件、Codex が A 1 件（C1-1）・C 1 件（R1-12 と同じ）を出した。止める基準で A が 0 でないので、直して再レビューする。

### 9.1 直したもの

| 指摘 | 区分 | 対応 |
|---|---|---|
| C1-1 確保の後に一覧へ足すので、足すときの例外で漏れる | A | 共通部に `AndroidNativeToolkitCApi.Alloc(byteCount, allocated)` を足した。足すのに失敗したら解放してから投げる。`AllocUtf8`、`AllocStringArray`、`AllocInt32Array` がこれを使う |
| R1-5 取り出し口が壊されると、以後の完了が無言で失われる | A | ライブラリを呼ぶたびに（`ShowNative`。検査と端末の判定を通った後） `AndroidNativeCallbackQueue.EnsureCreated()` を呼ぶ（UnityMain）。取り出し口が無いときの `Post` は `LogError` を出し、列に残す（次に作られたときに出る）。取り出し口の有無はスレッドをまたいで読める `volatile bool` で持つ |
| R1-6 種類への変換が `try/finally` の外 | A（潜在） | 変換を内側の `try` に入れた |
| R1-1 後片付けで完了が次のテストに漏れうる。理由の記述も誤り | B（中心）・C | 1.2 のとおり、設計どおりの照合にした |
| R1-2 「1 回だけ」を確かめるテストが少ない | B（中心） | R1-1 の照合が全 34 本で 1 回だけを確かめる |
| R1-3 J-D14 の確かめが空振り | B（中心） | 層 2a に `Completion_IsRaisedOnALaterFrame_NotInsideThePost`（`PostAnswer` の直後は 0 回）。AD-26 は取り消しの後に UnityMain を 300 ms 止め、その間に Android の main で受け口が走っても、イベントが 0 回であることを見る |
| R1-4 層 2a が本番の受け口の式を通らない。破棄のテストが新しい `Instance` に届いたことを見ていない | B（範囲） | 受け口の `Post` の部分を `AndroidDialogManager.PostAnswer`（ガードの外）に切り出し、受け口とテストの両方が呼ぶ。破棄のテストは、新しい `Instance` に購読して 1 回届くことを見る |
| R1-7 層 1 の抜け | B（範囲）・C | `BuildConfirm`、選択・入力・ログインの肯定・否定の文字の読み戻し、Confirm の写し、TextInput と MultiChoice の失敗の全引数、完了の `NOT_INITIALIZED` と想定外の値（42）、入口の想定外の値を足した（45 → 48 本） |
| R1-8 層 2a の抜け | B（範囲） | 項目の空（単一・複数）、6 種類すべての Editor の警告 |
| R1-9 AD-27 が失敗したときスレッドを止めない | B | `CancellationTokenSource` で中止を知らせ、`finally` で `Join` する。受け付けられていれば要求の数に入れる |
| R1-10 毎フレーム `Count` を読む | - | `IsEmpty` を先に見る |
| R1-11 完了ごとに delegate を作る | - | `extern` を直接呼んで `ReadUtf8OrNull` に渡す |
| R1-12 / C1-2 コメントが設計 v3 を指す | C | v4 に直した |
| R1-13 閉じたときの `errorMessage == ""` の例外が書かれていない | C | Manager の XML コメントと `common.md` に書いた |
| R1-14 `testing.md` 3 節の表と数 | C | `AndroidDialogManager` を A 群の表の行にし、「12 個中 7 個」に直した |
| R1-15 ログの書式 | C | 検査の失敗のログに `[{nameof(Method)}]` を付けた。`OnDevice` の文言は 1.x と同じ文字列を利用者とテストが照らすので変えず、コメントで理由を書いた |
| R1-16 共通部の説明（「呼ぶ側が先に `IsInitialized` を見る」） | C | 実装に合わせ、「`NOT_INITIALIZED` は呼び出しの失敗として返す。`IsInitialized` はログ用」に直した |

### 9.2 壊すと落ちることの確認

足したテストと照合が、守る対象のコードを壊したときに落ちることを確かめた（作業用フォルダの `mutate.py`。1 つ壊し、対象のテストを流し、元に戻す）。

| 壊し方 | 対象の指摘 | 流したテスト | 結果 |
|---|---|---|---|
| `PostAnswer` が列に入れず、その場でイベントを出す | R1-3 | 層 2a（`AndroidDialogManagerEditorTests`） | 落ちた（`Completion_IsRaisedOnALaterFrame_NotInsideThePost` ほか 1 本） |
| `PostAnswer` が、その時点の Manager を覚えて出す | R1-4 | 層 2a | 落ちた（`Completion_AfterTheManagerIsDestroyed_GoesToTheNewInstance`: 壊れた Manager の購読者に届いた） |
| 完了ごとにイベントを 2 回出す | R1-1、R1-2 | 層 2a | 落ちた（3 本） |
| 同上 | R1-1、R1-2 | 実機（AD-10、AD-13、AD-29） | **AD-10 と AD-13 が後片付けの照合で落ちた**（`1 call(s) reported 2 event(s)`）。v1 ではこの 2 本は `Contains` で比べており通っていた。AD-29 は同期の失敗で完了を通らないので通る（想定どおり） |
| 失敗したテキスト入力が `""` を返す | R1-7 | 層 1（`AndroidDialogCApiTests`） | 落ちた（`TextInput_TextNegativeNullDismissedEmpty`） |
| `BuildConfirm` が否定と肯定の文字を入れ替える | R1-7 | 層 1 | 落ちた（`BuildConfirm_SetsSizeTextsAndInvertedFlags`） |

（v3 の注: 下の R1-5 と AD-26 の 300 ms は、10.2 で確かめた。AD-26 の「壊し方として成り立たない」は誤りだった）

確かめていないもの: C1-1（`List.Add` の `OutOfMemoryException` は起こせない。形はコードレビュー）、R1-5（取り出し口を壊すテストは足していない。`LogError` と作り直しはコードレビュー）、AD-26 の 300 ms の確かめ（受け口が UnityMain の外でイベントを出すように壊すと、Unity の API を別のスレッドから呼ぶことになり、壊し方として成り立たない。同じ契約は層 2a の 1 本目で押さえた）。

最初の実行で、壊し方 2 つが私の書き誤り（コンパイルが通らない式、字下げの違う文字列）で流れなかった。直して流し直した結果が上の表。壊した実行の後、Unity が書き換えた `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` と `unity-native-plugin.slnx` は元に戻した。

## 10. 実装レビュー v2 への対応

レビュー v2 は、サブエージェントが A 1 件（R2-3）・B 中心 1 件（R2-1）・B 範囲 3 件・C 3 件・区分なしと要確認 2 件、Codex が A 1 件（C2-1）・B 2 件（C2-2、C2-3）・C 2 件を出した。A が 0 でないので、直して再レビューする。

### 10.1 直したもの

| 指摘 | 区分 | 対応 |
|---|---|---|
| R2-3 取り出し口が無効にされると、完了が無言で止まる | A | 旗を `OnEnable` / `OnDisable` で持つ（`OnDisable` は `OnDestroy` の前にも走る）。無効のときの `Post` は `LogError`。`EnsureCreated`（Manager の `Awake` と `Show*` のたび）は、無効な取り出し口を有効に戻す |
| C2-1 取り出し口の破棄と `Post` が競合すると、ログが出ない | A | `Post` は「列に入れてから旗を読む」、`OnDisable` は「旗を下ろし、メモリバリアの後で列を見る」。どちらの順で交わっても、少なくとも片方が残った完了を見つけて `LogError` を出す。重複した取り出し口の `OnEnable` / `OnDisable` は旗に触らない。終了の間（`Application.quitting` の後）は出さない（R2-8） |
| R2-1 入れ子の手順が投げると、後片付けと AD-27 の `finally` が走らない | B（中心）・C | 1.2 のとおり、`Safely` で回す形にし、AD-27 と AD-28 の片付けを後片付けの先頭に移した。AD-28 の向きの戻しも同じ穴だったので直した |
| R2-2 / C2-2 / C2-3 AD-27 のスレッドが補助の準備の前に動き、待ちが中止を見ない | B | スレッドを起こす前に `Ui.Do("count")` で補助を準備させる。`WaitForAnswerFile` に `CancellationToken` を足した（`AndroidUiDriver.cs`）。C2-3 の「テストが 10 秒で諦める」は、`Ui.Do("background")` が答えを待ってから 10 秒を数え始めるので、補助が 20 秒以内に答えれば起きない。中止が効くので、失敗のときの `Join` も長く止まらない |
| R2-6 `Request` が呼ぶ前に数える | B | 呼び出しが戻った後に数える。AD-26 も、受け付けたかを確かめる前に数える |
| R2-4 9.2 の AD-26 の理由 | C | 壊して流した（10.2）。落ちた |
| R2-5 / C2-4 / C2-5 記述のずれ | C | `testing.md` の本数（13 本）と内容、`common.md` の取り出し口を確かめる時点（`Awake` とライブラリを呼ぶたび）、取り出し口の説明（1.x の dispatcher と違い、始めの件数だけ）、`verify_unity_android.sh` の冒頭（層 2a と `--skip-editmode`）、UI テスト計画 5 章と親課題 README 6.6（修正後の回と、この版を指す） |
| R2-7 使われていない定数と `extern` | - | `AnswerButton`、`ButtonPositive`、`ButtonNegative`、`ntk_dialog_result_button` を消し、なぜ読まないかをコメントに書いた。`PostAnswer` の入口のログは、中の `Post` が出すので足していない |
| R2-8 終了の間の `LogError` | 要確認 | C2-1 の対応で、`Application.quitting` の後は出さないようにした。実機で終了の順序は確かめていない |

### 10.2 壊すと落ちることの確認

| 壊し方 | 対象の指摘 | 流したテスト | 結果 |
|---|---|---|---|
| 取り出し口が無いときの `Post` の `LogError` を消す | R1-5、R2-3 | 層 2a | 落ちた（取り出し口が壊された・無効にされたの 2 本。期待した `LogError` が出ない） |
| 無効な取り出し口を `EnsureCreated` が有効に戻さない | R2-3 | 層 2a | 落ちた（無効にされたのテストと、後に続くテストが `LogError` で） |
| 受け口が列に入れず、Android の main でその場でイベントを出す | R1-3、R2-4 | 実機の AD-26 | 落ちた（`the completion was raised outside Unity's main thread's queue`）。9.2 の「壊し方として成り立たない」は誤りだった |
| 後片付けの最初の UI の手順がいつも失敗する（`Safely` あり。v3 の形） | R2-1 | 実機の AD-01、AD-02、AD-29 | 3 本とも後片付けの失敗だけで落ち、本体はすべて通った（前のテストのシーンが下ろされ、購読と列も片付いた） |
| 同上、ただし `Safely` を通さず直に `yield return`（v2 までの形） | R2-1 | 同上 | **AD-02 の本体が「トップメニューを待って期限切れ」で落ちた**（AD-01 のシーンが下ろされずに残り、連鎖した）。v3 の形がこれを防いでいることが分かる |

確かめていないもの: C2-1 の競合そのもの（スレッドの交わりを起こせない。形はコードレビュー）、R2-8（終了の間の完了の順序）。

壊した実行の後、Unity が書き換えた `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` と `unity-native-plugin.slnx` は元に戻し、壊したコードがすべて元に戻っていることを確かめた。

### 10.3 修正後の検証

`verify_unity_android.sh --as-package-user`（`verify-fix2`）: EditMode 1229 / 1230、Editor の PlayMode 223 / 234、実機 47 / 47（UI の補助の依頼 114 件、終了コード 0、`AndroidUiDriver` の 34 本は skip 0）、パッケージの利用者の条件 38 件合格・9 件 skip。skip はどれも既存のもの（5 章）。

## 11. 実装レビュー v3 への対応

レビュー v3 は、サブエージェント（ラウンド 1・2 とは別のインスタンス）も Codex も **A が 0 件**だった。サブエージェントは B 6 件・C 4 件、Codex は B 5 件・C 5 件。止める基準の 1（レビュアーを替えて A が 0）は満たした。R3-9 は「1.x の振る舞いによっては A」とされた。1.x の `Show*` は呼んだスレッドで `AndroidJavaClass` と `Call` を使っており（Unity の JNI は呼んだスレッドを自動でアタッチする）、別のスレッドからも動いた見込みが高いので、回帰として直した。B と C も、安いものは直した。

### 11.1 直したもの

| 指摘 | 区分 | 対応 |
|---|---|---|
| R3-9 別のスレッドから `Show*` を呼ぶと、`isActiveAndEnabled` の例外が呼び出し側に出る | A 相当 | 取り出し口が UnityMain のスレッドを覚え（`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`）、`EnsureCreated` は UnityMain の外では何もしない。Manager の XML コメントに「別のスレッドからも 1.x と同じく動く。検査の失敗はそのスレッドで出る」と書いた。層 2a と実機（AD-37）で確かめた |
| R3-1 / C3-2 / C3-3 `OnDisable` 側の報告、`SetActive(true)`、`ShowNative` の `EnsureCreated` にテストが無い | B | 層 2a に 2 本（列に入った後で無効にする、GameObject ごと非アクティブにする）、実機に AD-36（取り出し口を壊した後の `Show*`） |
| R3-2 / C3-1 `s_quitting` を戻さない（ドメインの再読み込みを切った Editor） | B | 起動のとき（`SubsystemRegistration`）に static を戻す。このリポジトリは再読み込みする設定なので、壊して確かめてはいない |
| R3-3 / C3-5 後片付けが、向きが戻ったかを確かめない | B | 期限が来ても戻らなければ失敗にする（`Safely` が受け止め、片付けは続く） |
| R3-4 後片付けの Ignore がテストの失敗を上書きする。失敗が 1 つしか報告されない | B | 後片付けの失敗と数の照合をまとめて 1 つの `Assert.Fail` にする。Ignore は、テストがすでに失敗していなければ Ignore にする |
| R3-8 / C3-4 後片付けの先頭の手順が投げると、片付けが飛ぶ。`Join` が無期限 | B | AD-27 のスレッドは `Start` が成功してからフィールドに入れる。先頭の手順は `try/catch` で失敗を貯める。`Join` は 30 秒までで、戻らなければ失敗にする |
| R3-10 (c) 重複した取り出し口の `Update` | - | `s_pump == this` のときだけ列を回す。(a)・(b) は直さない（7 章） |
| R3-5 `csharp.md` の先頭のログ | C | `EnsureCreated`、`OnEnable`、`OnDisable` の先頭に `Debug.Log` を置いた |
| R3-6 `Post` の順序の保証が `ConcurrentQueue` の中に頼る | C | `Enqueue` の後に `Interlocked.MemoryBarrier()` を置いた |
| R3-7 結果ファイル 2.2 の抜け | C | 足した |
| C3-6 / C3-7 設計 v4 7.3 の記述 | C | 7 章の残件に、違いと実装の形の置き場所を書いた |
| C3-8 UI テスト計画のカテゴリの範囲 | C | UI テスト計画 v2 で直した |
| C3-9 UI の補助の依頼の数（113 と 114） | C | 4 章を最終回（`verify-fix3`、118 件）に直した。10.3 の 114 件は `verify-fix2` の記録でそのまま |
| C3-10 「`Show*` のたびに」 | C | 9.1 を「ライブラリを呼ぶたびに（検査と端末の判定を通った後）」に直した |

あわせて、後片付けで「イベントがすべて UnityMain で出たこと」も照らすようにした（AD-37 のために足したが、全テストに効く。J-D2 の確かめ）。

### 11.2 壊すと落ちることの確認

| 壊し方 | 対象の指摘 | 流したテスト | 結果 |
|---|---|---|---|
| `OnDisable` が取り残しを報告しない | R3-1 | 層 2a | 落ちた（`Completion_QueuedBeforeTheQueuesObjectIsDisabled_IsReportedByTheDisable`） |
| `EnsureCreated` が GameObject をアクティブに戻さない | C3-2 | 層 2a | 落ちた（`Completion_WhenTheQueuesGameObjectWasDeactivated_RunsOnceActivatedAgain` と、非アクティブのまま残った取り出し口に続くテスト 4 本） |
| `EnsureCreated` が UnityMain の判定をしない | R3-9 | 層 2a | 落ちた（`EnsureCreated_OffTheMainThread_DoesNothingAndDoesNotThrow`） |
| 同上 | R3-9 | 実機の AD-37 | 落ちた（`UnityEngine.UnityException: get_isActiveAndEnabled can only be called from the main thread.`。直す前の形で、別のスレッドからの `Show*` が実際に例外を出していたことが分かる） |
| `ShowNative` が `EnsureCreated` を呼ばない | R3-1、R1-5 | 実機の AD-36 | 落ちた（取り残しの `LogError`） |

壊した実行の後、Unity が書き換えた `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` と `unity-native-plugin.slnx` は元に戻し、壊したコードがすべて元に戻っていることを確かめた。

### 11.3 修正後の検証

`verify_unity_android.sh --as-package-user`（`verify-fix3`）: EditMode 1229 / 1230、Editor の PlayMode 226 / 237、実機 49 / 49（UI の補助の依頼 118 件、終了コード 0、`AndroidUiDriver` の 36 本は skip 0）、パッケージの利用者の条件 40 件合格・9 件 skip。skip はどれも既存のもの（5 章）。

### 11.4 止める基準の判定

1. レビュアーを替えて 1 回通した: ラウンド 3 のサブエージェント（別のインスタンス）と Codex がどちらも A 0 件
2. 追加したテストが、壊すと落ちる: 9.2、10.2、11.2。確かめていないものは 7 章に理由つき
3. 直さない残件を理由つきで書いた: 7 章

ラウンド 3 の後に直した所（11.1）は、全体の検証（11.3）と壊すと落ちることの確認（11.2）で確かめた。もう 1 ラウンドのレビューを回すかは、利用者が決める。
