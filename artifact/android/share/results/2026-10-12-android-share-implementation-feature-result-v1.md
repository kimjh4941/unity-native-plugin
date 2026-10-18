# 実装結果レポート v1（Android Share の C ABI 2.0.0 への移行）

## 基本情報

- 日付: 2026-10-12
- 機能名: share
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-13
- 計画ファイル: `artifact/android/share/designs/2026-10-11-android-share-design-v6.md`（以下「設計」）
- UI テスト計画: `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v6.md`
- UI の補助の設計: `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v11.md`
- 親課題: `artifact/topics/android-c-abi-2/README.md`
- 同梱: native-toolkit dist 1.13.0（`android-native-toolkit-2.0.0.aar`、`android-native-toolkit-capi-2.0.0.aar`）。移行の間は dist 1.12.0 の 2 本も追加のピンで置く（Dialog の移行から変えていない）

## 0. 状態サマリー

設計 5.5 の手順 1〜10 を終えた。手順 4〜10 はまだコミットしていない（実装のレビューの後に利用者の確認を取る）。

| 手順 | 内容 | コミット | 状態 |
|---|---|---|---|
| 1 | UI テスト計画（v1〜v5。レビュー 5 回） | `5729578` | 済み |
| 2 | UI の補助を Share に広げる（補助の設計 v6〜v11） | `c427a9c`、`5729578` | 済み |
| 3 | 1.x のまま 45 件を流して記録（45/45。UI テスト計画 v6） | `b20cd1c`、`19e6d8b` | 済み |
| 4 | 共通部（`AllocStringArray` を Dialog から移す） | 未コミット | 済み |
| 5・6 | `AndroidShareCApi`（構造体、純粋な関数、`extern`）、層 1 | 未コミット | 済み |
| 7 | `AndroidShareManager` の置き換え、層 2a | 未コミット | 済み |
| 8 | 2.0.0 の列のテストを流す（実機 45/45） | 未コミット | 済み |
| 9 | `common.md`、`testing.md`、親課題の README | 未コミット | 済み |
| 10 | 実装結果ファイル（この文書） | 未コミット | 済み |

## 1. 実装サマリー

### 1.1 計画ファイル由来の実装

- **2 層（Bridge + Manager）。** `Runtime/Android/Share/AndroidShareCApi.cs`（Bridge）と `AndroidShareManager`。共通部（`AndroidNativeToolkitCApi`、`AndroidNativeCallbackQueue`）は Dialog の移行で作ったものを使い、`AllocStringArray` だけを Dialog から移した
- **ガード。** Manager の型と Bridge の純粋な関数は `UNITY_ANDROID || UNITY_EDITOR`（Manager は B 群から A 群へ）。`DllImport`、受け口とその関数ポインタ、リスナーの登録は `UNITY_ANDROID && !UNITY_EDITOR`
- **J-S1** 呼び出しの記録（`ShareCall`: 操作、パス、MIME、Direct Share の ID）を static の表に連番のキーで入れ、キーを `user_data` にする（`CallNative`）。完了の受け口は表を読むだけ、`release` の受け口と「`extern` に入れなかった例外」だけが消す
- **J-S2** どの結果も取り出し口を通して、呼び出しの後に出す（Editor、検査の失敗、入口の拒否、同期の Direct Share も）
- **J-S3** Editor・Android 以外は、入力によらず `LogWarning("<op> can only be called on Android.")` と `<op> could not be started.`。null の検査の直後、ほかの検査の前
- **J-S4** 結果は取り出した時点の `_instance` に出し、いなければ捨てる。Chooser Action は受け口の時点の Manager の coordinator に閉じ込める（持ち越し K-1）
- **J-S5、J-S14** 操作ごとの per-call の枠（last-registered wins、null も）。同期に決まる結果は呼んだスレッドで、ネイティブの完了は完了の受け口（Android の main）で、`lock` の中で取り出す。`shareWithCallback` の失敗はどこで作っても `onSelected` を消す
- **J-S6** `Initialize()` はリスナー 2 つ（Chooser Action、選択）を、登録専用の `lock` の中でプロセスに 1 回だけ登録する。失敗したら `LogWarning` を出し、次の `Initialize()`・`ShareText`・`ShareWithCallback` でやり直す
- **J-S7、J-S8** null の payload・配列・Chooser Action の要素は `ArgumentNullException`（何も登録する前）。配列の要素の null は `... must not contain null`
- **J-S9** 複数のパスの `FILE_NOT_FOUND` は、UnityMain で「ファイルでもディレクトリでもない最初のパス」（無ければ全部）、`ILLEGAL_FILE_ACCESS` は全部のパス
- **J-S10** 本文と `subject` は長さだけ、アイコンは文字数だけをログに出す。JSON は出さない
- **J-S11** `AndroidShareJsonBuilder` は公開のまま残し、Manager からは使わない（XML コメントに書いた）
- **J-S12** 選ばれたアプリは照らさずに出す（`ShareCallbackReceived` → その時点の `onSelected`、1 回で消す）
- **J-S13** 受け付けた選択の要求の ID を、古い順に 16 個まで残す。取り消しはすべてに F-9 を呼ぶ
- **J-S15** `OnDestroy` は自分が `_instance` のときだけ、枠・`onSelected`・coordinator を空にし、残した ID を取り消す。coordinator の static の参照は、`Awake` で自分が `_instance` になった後に入れ、`OnDestroy` で `Clear()` の前に消す（持ち越し K-2）
- **J-S16** どのスレッドから呼んでもよい。`EnsureCreated()` は UnityMain の外では何もしない
- **J-S17** 必須の値は Kotlin の `isBlank` と同じ判定（`IsBlank`）、任意の値は `IsAbsent`
- **4.2.1** Chooser Action の整え方（`NormalizeActions`）: 空白・null を捨てる、Base64 として読めない・画像の印が無いアイコンを捨てる、C に渡す形で比べて重複を捨てる、順を保つ、SEND は残して通知しない

### 1.2 実装時の追加判断

- **操作は列挙にせず、`Operation*` 定数の文字列のまま使った。** 設計 5.1 は Bridge に入れ子の `Operation` 列挙（9 個）を置くとしたが、文言の `<op>` も per-call の枠のキーも結果の `Operation` も同じ文字列なので、写しの関数を持たない方が単純だった
- **ネイティブの引数を組んで呼ぶ所を `OpenNative` にまとめた。** 組み立て（確保）の例外は `ex.Message` と `LogError` で失敗にし、確保したものは `finally` で解放する。`CallNative`（層 2a の継ぎ目）は設計 5.4 のまま
- **Direct Share（同期）は `CallDirect`。** 戻り値から結果を作って `PostImmediateResult`。`INVALID_PARAMETER` は完了と同じ文言で `LogWarning`
- **層 2b のテストは、Manager の版を見分ける**（C ABI の試験用の口 `PendingCallCountForTests` の有無。反射）。1.x の記録（手順 3）と移行の後の確かめを、同じテストで流した。既知の差分の行は版ごとの期待で照らす。移行が済んだので、1.x の分岐は後で消してよい（7 章）
- **層 2a の配送のテストは、取り出し口を実際に回す**（`yield return null`）。Dialog の層 2a と同じく、`AndroidNativeCallbackQueue` の GameObject が Editor の PlayMode で回る

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareCApi.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidShareCApiTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidShareManagerEditorTests.cs`
- （手順 3 でコミット済み）`Tests/PlayMode/AndroidSharePlayerTests.cs`、`AndroidShareManagerPlayerTests.cs`、`AndroidShareTestSupport.cs`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareManager.cs`（`AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher` を消して書き直し。公開 API・イベント・既定値・文言は同じ。変わったのは 3.4 の既知の差分だけ）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeToolkitCApi.cs`（`AllocStringArray`）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Dialog/AndroidDialogCApi.cs`（`AllocStringArray` を共通部の呼び出しに。振る舞いは変えない）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/ShareChooserActionCallbackCoordinator.cs`（XML コメントだけ）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareJsonBuilder.cs`（XML コメントだけ）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidNativeToolkitCApiTests.cs`（`AllocStringArray` の 2 本）
- `agent-rules/coding-rules/common.md`（Android の節に「Share の例外」）、`agent-rules/coding-rules/testing.md`（ガードの表、層 1・2a・2b・3）
- `artifact/topics/android-c-abi-2/README.md`（J-9 に Share の例外、段の状況）

### 2.3 非変更（対象だが未変更）

- Payload 8 型、結果 3 型、`AndroidShareManagerExampleController`、`AndroidShareResultTests`、`AndroidShareJsonBuilderTests`、`AndroidShareChooserActionCallbackCoordinatorTests`、`AndroidShareSampleSceneWiringTests`、`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/*`（設計 5.1 の表のとおり）

## 3. エラー契約反映

### 3.1 エラーケース実装反映（設計 6 章）

| # | 実装 | 検証 |
|---|---|---|
| E-1 | `CheckNotNull`、`CheckActionsNotNull` | 2a `NullArguments_ThrowAndRegisterNothing`、2b AS-36 |
| E-2〜E-4 | `CheckText`、`CheckPath`、`CheckPaths`、`CheckIds`、`CheckDirectTarget` | 1 `Checks_UseThe1xMessages`、`CheckDirectTarget_InThe1xOrder`、2b AS-25〜AS-33 |
| E-5 | `NormalizeActions`、`IsNotifiedActionId` | 1、2b AS-42〜AS-45 |
| E-6 | `OnDevice` | 2a `Editor_EveryOperationFailsAfterTheCall`、`Editor_InvalidInput_FailsTheSameWay` |
| E-7〜E-9、E-14〜E-19 | `FailureMessage`、`NativeUnavailableMessage` | 1 `FailureMessage_EveryValue` |
| E-10 | `NOT_FOREGROUND` | 2b AS-52（実機で起こした） |
| E-11、E-12 | `FILE_NOT_FOUND`、`ILLEGAL_FILE_ACCESS`、`MissingPath` | 1、2b AS-21〜AS-24 |
| E-13 | 完了の `INVALID_CHOOSER_ACTION` | 2b AS-46（実機で起こした） |
| E-15 | `INVALID_ICON` | 2b AS-32 |
| E-20 | `CallNative` の `catch` | 2a `CallNative_Throws` |
| E-21、E-22 | 受け口の `try/catch`、`EnsureListeners` の `LogWarning` | コードレビュー（起こせない） |
| E-23 | `OnReleaseCore`、表から消す | 2a `CallNative_CompletionAndReleaseBeforeItReturns`、`CallNative_RejectedAtTheEntry`、2b の後片付け（完了待ちが 0 に戻る） |

### 3.2 イベントの返却仕様

- `ShareOperationCompleted` → per-call の順。どれも呼び出しの後、UnityMain で 1 回
- `ShareCallbackReceived` → `onSelected`（1 回で消す）の順
- `ShareChooserActionTapped` → `onChooserAction`（消さない）の順。SEND は通知しない

### 3.3 success 時契約

開く操作の成功は「Sharesheet が開いた」（1.x と同じ）。Direct Share と取り消しは同期の戻り値。取り消しは Android の上ではいつも成功。

### 3.4 既知の差分（マニュアルへ引き継ぐ）

設計 5.3 の表を写し、1.x の実測（UI テスト計画 v6）で決まった行を直した。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 後ろから呼ぶ | （UnityMain からは測っていない。別のスレッドからは受け付けなかった。下の行） | `Failed to <op>: the app is not in the foreground.`（AS-52 で実測） |
| **別のスレッドから呼ぶ（新しく見つかった差分）** | 受け付けなかった。エラーのログを 2 行出し（`Failed to get currentActivity`、`<op> failed: currentActivity is null.`）、`<op> could not be started.` で失敗した（AS-35・52・53 で実測。設計 J-S16 の「1.x と同じ」は誤りだった） | 受け付ける。結果は UnityMain で出る |
| 同期に決まる結果の per-call、操作をまたいだ順、取り消しの成功の時点、UnityMain の処理の中から呼んだときのフレーム | 設計 5.3 のとおり | 設計 5.3 のとおり |
| Direct Share の呼び出し | Android の main で非同期 | 呼んだスレッドで同期 |
| 画像として読めない Chooser Action のアイコン | 飛ばして開いた（AS-46 で実測） | 印が画像でないものは飛ばす。印は当たるが壊れたものは Share 全体の失敗（AS-46） |
| 同じ `intentAction` のアクション | すべて表示した（AS-44a で実測） | 最初の 1 つだけ |
| 前の Share の Chooser Action、古い Chooser で選んだアプリ、複数のスレッドから同時 | 設計 5.3 のとおり | 設計 5.3 のとおり |
| 結果が Manager を壊す前後に来る（J-S4） | 壊す前に積まれた結果は壊した Manager に出た。壊した後は捨てた | 取り出した時点で生きている Manager に出し、いなければ捨てる。**例外: Chooser Action は、受け口の時点の Manager に閉じ込め、その後に Manager を壊しても、その購読者と per-call に 1 回出る**（1.x と同じ。持ち越し K-1） |
| `Instance` の最初の取得 | （1.x も UnityMain だけ） | GameObject を作るので UnityMain で行う。その後はどのスレッドからでも呼べる（実装レビュー v1 の X:A-1 で XML コメントに書いた） |
| 複数のファイルの文言 | 原因のパス（AS-24 で実測） | 存在しない最初のパス / 全部のパス |
| Direct Share の登録の失敗、`SecurityException`、そのほかの例外の文言 | 理由や例外の文言を含む | 固定の文言 |
| null の payload・配列・Chooser Action の要素 | `NullReferenceException`（AS-36 で実測） | `ArgumentNullException` |
| 配列の要素の null | 文字列 `"null"` として扱った（AS-29 で実測: `File not found: null`） | `Failed to <op>: filePaths must not contain null` |
| 必須の文字列が null、Chooser Action の値が null | `"null"` として扱った見込み（測っていない） | 空と同じ検査の失敗 / そのアクションを捨てる |
| Base64 の読める範囲、文字列の途中の U+0000、対になっていないサロゲート、Editor、ログ、`Initialize()`、per-call の枠の競合、32 ビットと Mono、`AndroidShareJsonBuilder` | 設計 5.3 のとおり | 設計 5.3 のとおり |

## 4. ビルド結果

- Editor のコンパイル: 成功（EditMode と Editor の PlayMode の実行で確かめた）
- 実機の Player（Android™ Profile、IL2CPP、ARM64）: 成功（手順 8 の実行）

## 5. テスト結果

| 層 | 対象 | 結果 |
|---|---|---|
| 1（EditMode） | `AndroidShareCApiTests`、`AndroidNativeToolkitCApiTests`、`AndroidDialogCApiTests`、既存の Share の層 1 | 153/153 |
| 2a（Editor の PlayMode） | `AndroidShareManagerEditorTests` 19、`AndroidDialogManagerEditorTests` 16 | 35/35 |
| 2b（実機。Pixel 6a、IL2CPP） | `AndroidSharePlayerTests` 29、`AndroidShareManagerPlayerTests` 16、`AndroidDialogPlayerTests` 36（共通部を変えたので） | 81/81（722 秒。補助の終了コード 0） |

### 5.1 テスト詳細

- 2b は C ABI の版で、既知の差分の行（AS-24、29、36、44a、46）が 2.0.0 の期待どおり。AS-52 は `Failed to shareText: the app is not in the foreground.`、`no-sheet` は `ok`。AS-35・53 は別のスレッドから受け付けた
- 2b の後片付けで、`Op` / `Selected` / `Action` の数、イベントが UnityMain で出たこと、`PendingCallCountForTests` が 0 に戻ること、補助の依頼の回収を照らした

### 5.2 未実施ケース詳細

- `scripts/verify_unity_android.sh` の通しの実行（EditMode 全体、Editor の PlayMode 全体、実機の全体、パッケージの利用者の条件）は、実装のレビューの後に流す
- E-9、E-16、E-17、E-19、E-21、E-22 は起こせない（文言は層 1、形はコードレビュー）
- API 33 以下の端末（Chooser Action が出ない）は手動（手元に無い）

## 6. Definition of Done

| 項目 | 状態 |
|---|---|
| 公開 API を保つ | 済み |
| 設計の J-S1〜J-S18 と持ち越し K-1・K-2 | 済み（K-3 は補助、K-4・K-5 は 7 章） |
| 層 1・2a・2b | 済み |
| 既知の差分を XML コメントと実装結果に書く | 済み |
| 実装のレビュー | 未（次） |
| 通しの検証スクリプト | 未（レビューの後） |

## 7. 残作業

- **実装のレビュー**（サブエージェントと Codex を並行。`agent-rules/workflows/review-implementation-feature/workflow.md`）
- **Share 設計の次の版**: 持ち越し K-4（8.2 の「止まる見込み」、8.3 の「今の `back` で足りる」を補助の設計 v11 に合わせる）と K-5（J-S16 の「1.x と同じ」を直し、5.3 に「別のスレッドから呼ぶ」の行を足す）。3.4 の表には先に反映した
- 層 2b のテストの 1.x の分岐（`OnCApi` が偽の側）は、1.x のピンを外すときに消す
- 通しの `verify_unity_android.sh`
