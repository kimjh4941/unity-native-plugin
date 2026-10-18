# 実装レビュー結果

- 日付: 2026-10-12
- 機能名: share
- プラットフォーム: android
- レビュー対象: ブランチ `feature/UNT-13` の未コミットの差分（`git diff` と新しいファイル `AndroidShareCApi.cs`、`AndroidShareCApiTests.cs`、`AndroidShareManagerEditorTests.cs`）と、移行の確かめに使った実機テスト（`b20cd1c` の `AndroidSharePlayerTests.cs`、`AndroidShareManagerPlayerTests.cs`、`AndroidShareTestSupport.cs`）
- 実装計画: `artifact/android/share/designs/2026-10-11-android-share-design-v6.md`
- 実装結果: `artifact/android/share/results/2026-10-12-android-share-implementation-feature-result-v1.md`
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェント、2 章は Codex の出力（どちらも手を加えていない。3 章では「S:」「X:」を前に付けて区別する）

---

## 1. サブエージェント（Claude）


レビューの対象:
- 未コミットの差分: `AndroidShareManager.cs`、`AndroidNativeToolkitCApi.cs`、`AndroidDialogCApi.cs`、`AndroidShareJsonBuilder.cs`、`ShareChooserActionCallbackCoordinator.cs`、`AndroidNativeToolkitCApiTests.cs`、common.md、testing.md、親課題の README
- 新しいファイル: `AndroidShareCApi.cs`、`AndroidShareCApiTests.cs`、`AndroidShareManagerEditorTests.cs`
- b20cd1c で入った実機テスト 3 本

比べた資料:
- 設計 v6、K-1 / K-2、実装結果 v1、UI テスト計画 v6
- `Share.h` / `Common.h`（dist 1.13.0）
- 1.x の Manager（`git show HEAD:`）、Dialog の前例、共通部

ワークフローの手順 4〜7 に沿って行った。手順 8 のファイル保存はしていない（読み取り専用の指示のため）。

---

#### レビュー概要

**何をした差分か**
- Share を 1.x の Kotlin ブリッジ（`AndroidJavaObject`、`AndroidJavaProxy`、JSON）から、`libntk.so` の P/Invoke に置き換えた。
- `AndroidShareManager` のガードを `#if UNITY_ANDROID` から `UNITY_ANDROID || UNITY_EDITOR` に広げた（B 群から A 群へ）。
- Bridge として `AndroidShareCApi` を足した。中身は構造体、`extern`、純粋な検査と変換、エラーの文言。
- `AllocStringArray` を共通部（`AndroidNativeToolkitCApi`）に移した。

**状態の持ち方と配送**
- 状態は static に置き、1 つのロック `s_lock` で守る: 操作ごとの per-call の枠、`onSelected`、`user_data` の表 `s_calls`、取り消す ID の一覧。
- リスナーの登録には別のロック `s_listenerLock` を使う。
- 結果はどれも `AndroidNativeCallbackQueue` を通り、呼び出しの後に UnityMain で出る。

**確かめて問題が無かった所**

| 観点 | 確かめた内容 |
|---|---|
| `user_data` と `release` の寿命 | `CallNative`（`AndroidShareManager.cs:520-548`）は `AddCall` を `invoke` の直前に置く。表から消すのは 2 か所だけ: `OnReleaseCore`（:585）と、`extern` が戻らなかったときの `catch`（:544、`key != 0 && !returned`）。<br>完了の受け口（:569-582）は表を読むだけで、取り出した `ShareCall` を閉じ込めて `Post` する。そのため、完了と `release` が `extern` の戻りより先に来ても成り立つ。<br>入口で拒まれたときは、`release` が呼び出しスレッドで先に来て表から消し、失敗は手元の `call` で出る（設計 1.3、`Common.h` の「exactly once」と一致） |
| スレッド | `s_lock` の中では辞書と一覧の操作だけをし、`extern` も `Debug.Log` も呼ばない。<br>`s_listenerLock` は登録の `extern` の間だけ持つが、C ABI は main を待たないので、互いに待って止まることは無い。<br>`s_coordinator` は `volatile`。代入は `Awake` の重複チェックで `return` した後（:160）。`OnDestroy` では `Clear()` より前に null にする（:172、:180）。K-2 のとおり。<br>`OnDestroy` は `_instance != this` なら何もしない（J-S15）。<br>別のスレッドからの `Application.platform` と `EnsureCreated` は、AS-35 / AS-53 が実機で通っている |
| per-call の枠を取り出す時点 | 同期に決まる結果は呼んだスレッドで、完了は Android の main で、それぞれ `TakePending` が取り出す（:484-493）。<br>`shareWithCallback` が失敗すると `onSelected` も消す（J-S14）。どちらも 1.x の `FireOperationResult`（1.x の :399-420）と同じ時点 |
| IL2CPP | 受け口 4 つはどれも `private static` で `[MonoPInvokeCallback]` が付く。delegate と関数ポインタは `static readonly` で、宣言の順に初期化される（:774-781）。<br>`ref content` と `out requestId` はクロージャのフィールドを参照で渡しているが、どちらも blittable で、GC は動かさない型なので問題ない |
| 構造体と `extern` の型 | 56 / 32 / 48 バイトとフィールドの位置、`uint64_t` → `ulong`、`int32_t` → `int`、`size_t` → `UIntPtr` は、どれも `Share.h` と一致 |
| アンマネージドのメモリ | `OpenNative` と `CallDirect` は、確保したものを `finally` の `FreeAll` で解放する。`Alloc` は、記録に失敗しても漏れない |
| `ntk_string` の所有権 | アクションの受け口は、`ReadString` が例外を投げても `finally` で `ntk_string_free` を呼ぶ。選択の受け口は、null でないときだけ同じようにする（:899-944） |
| P1〜P5 | 新しいファイルと型には `Android` の接頭辞がある。名前空間は `JonghyunKim.NativeToolkit.Runtime.Android.Share`。`.meta` の GUID は重複していない。<br>`git diff --name-only HEAD` に `Ios` / `Mac` / `Windows` のファイルは無い。<br>ガードは 2 段になっている（クラスは `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport` と受け口は `UNITY_ANDROID && !UNITY_EDITOR`）。テストのガードも対象の型と合っている |

#### 重大な問題（high）

- **なし（A は 0 件）。**
- 振る舞いを変える欠陥は見つからなかった。設計 v6 の J-S1〜J-S18、K-1、K-2 からの逸脱も無い。
- 調べたがシナリオが成り立たず、A にしなかったもの:
  - 完了が `AddCall` より前に来る: 起きない。表に入れてから `extern` を呼ぶ。
  - `release` が完了より先に来て「no call for key」になる: 契約上、起きない。
  - `ShareWithCallback` の `out` に ID が書かれる前に完了が来る: 起きても、受け付けた後に ID を足すので取り消しに影響しない。
  - ロックを互いに待って止まる: 起きない。ロックの中で `extern` を呼ばない。

#### 改善提案（medium）

**B-1 「共通イベント → per-call」の順が、どのテストでも固まっていない**
- 該当箇所: `AndroidShareManager.cs:508-509`。公開 XML コメント（:94-97）は「Always fired before the per-call callback」と約束している。
- 2a のテストは、`Op` を `_events` に、per-call を `_calls` に別々に積んでいる。そのため、2 つのリストの間の順を見ていない（`AndroidShareManagerEditorTests.cs:62-70`、:83-108、:148-158）。
- 2b の AS-47 は `onStarted|True` を数を付けずに記録している（`AndroidSharePlayerTests.cs:326`）。AS-10 もサンプルの最後の表示が `onSelected` なので、この順は見ていない。
- 見逃すミューテーション: `Deliver` の 2 行を入れ替える（`callback?.Invoke` を先にする）。それでも全テストが通る。
- 直し方: `Completion_TakesTheSlotWhenItArrives` か `Editor_EveryOperationFailsAfterTheCall` で、`ShareOperationCompleted` の購読者も `_calls` に書き、`["event|…", "before|…"]` の順を照らす。選択（`Selection_EventThenOnSelected_Once`）と Chooser Action（`ChooserAction_EventThenPerCall_KeptForTheNextTap`）は、すでにこの形で固めている。

**B-2 `Destroying_TheManager_ClearsItsState` は、per-call の枠と `onSelected` を消すことについては落ちない**
- 該当箇所: `AndroidShareManagerEditorTests.cs:222-235`。
- Manager を壊した後は、`Deliver`（:501-506）が Manager のいない結果をすべて捨てる。そのため `Assert.IsEmpty(_calls)` は、`OnDestroy` が枠を消したかどうかに関係なく通る。このテストで意味のある確かめは `SelectionIdCountForTests == 0` だけ。
- 見逃すミューテーション: `AndroidShareManager.cs:176` の `s_pendingOperations.Clear()` か、:177 の `s_pendingSelected = null` を消す。
- 壊れたときの実害: 作り直した Manager に、前の Manager で登録した per-call と `onSelected` が出る（J-S15 と 1.x の `OnDestroy`〈1.x の :132-133〉の契約違反）。
- 直し方: 壊した後に `AndroidShareManager.Instance` で作り直し、`AddCall` → `OnDoneCore`、`OnSelectionCore` を入れ、前の callback が呼ばれないことを照らす。

#### 軽微な指摘（low）

- **C-1 テストの穴: null 引数のテスト**
  - `NullArguments_ThrowAndRegisterNothing`（`AndroidShareManagerEditorTests.cs:239-260`）は、`ShareFiles`、`ShareImages`、`RemoveDirectShareTargets`、`RegisterDirectShareTarget`、`ShareWithCallback` に callback を渡していない。そのため、この 5 つの経路では「何も登録しない」を確かめていない。
  - 見逃すミューテーション: `SharePaths`（`AndroidShareManager.cs:734-737`）で `SetPendingOperation` を `CheckNotNull(filePaths)` の前に移す。または `ShareWithCallback` で `s_pendingSelected` に書くのを `CheckActionsNotNull` より前に移す。
  - 中心の経路（`ShareText` と coordinator）は固まっているので、C にした。
- **C-2 起こりにくい例外: `EnsureListeners` の `extern`**（`AndroidShareManager.cs:848-867`）
  - `try` の外にある。ここで例外が出ると、per-call の枠を入れた後に `ShareText` / `ShareWithCallback` / `Awake` から呼び出し側へ抜け、結果は出ない。
  - `EnsureNativeAvailable` で major の版が合うことを確かめた後なので、実際には起きない。
- **C-3 共通部（差分の外）: `s_state` の読み書き**
  - `AndroidNativeToolkitCApi.s_state`（`NativeState?`）は、`volatile` もロックも無しに複数のスレッドから読み書きされる。J-S16 で別のスレッドからの呼び出しを正式に受けるようになった。
  - 最悪でも、ちぎれた値を読んで `extern` が `DllNotFoundException` を投げ、`CallNative` が捕まえて失敗を返すだけ。
- **C-4 ログとの不一致**: `RegisterDirectShareTarget` のログは `iconBase64: (N characters)`（:347）で、Base64 の文字数を出している。設計 4.4 の J-S10 は「アイコンはバイト数だけ」と書いている。実装結果 1.1 は「文字数」で、実装とは合っている。設計の次の版で揃える。
- **C-5 common.md の「ログ」**: 「入力…の値は長さだけを出す」と書いているが、Share は題名・パス・ID・ラベルをそのまま出す（J-S10 で決めたこと）。「Share の例外」の一覧にこの項が無い。
- **C-6 K-1 の書き写し漏れ**
  - 実装結果 3.4 の「Manager を壊す前後: 設計 5.3 のとおり」の行は、移行ガイドへ引き継ぐ表に Chooser Action の例外を持ち込んでいない。設計 5.3 の J-S4 の行にも、この例外は無い。
  - K-1 は「5.3 の J-S4 の行と、実装結果の 5.3」に書くように求めていた。common.md には書いてある。
- **C-7 XML の remarks が足りない**（`AndroidShareManager.cs:19-37`）
  - 次の 5.3 の行が無い: Direct Share が呼んだスレッドで同期に動くこと、読める Base64 の範囲、U+0000 とサロゲートの扱い。
  - 利用者に配らない `artifact/` のパスを参照している。
- **C-8 testing.md の表現**: A 群の `AndroidShareManager` の行に「入力によらず `<op> could not be started.`」とある。null の payload では `ArgumentNullException` になるので、言い過ぎ。
- **C-9 実機テストの細部**
  - C ABI かどうかを `PendingCallCountForTests` の反射で見分けている（`AndroidShareTestSupport.cs:30-36`）。`InternalsVisibleTo("NativeToolkit.Runtime.PlayModeTests")` があるので、1.x の分岐を消すときに直接参照へ替えられる。
  - 反射で見分けるのは、コードの stripping に弱い。ただし外れたときは、既知の差分の行が大きく落ちるので気づける。
  - XML コメントが UI テスト計画 v5 を指している（`AndroidShareTestSupport.cs:19`、`AndroidSharePlayerTests.cs:25`、`AndroidShareManagerPlayerTests.cs:24`）。今は v6。
- **C-10 設計 5.1 / 5.4 との形の違い**: 次の 3 つは実装結果 1.2 に記録済み。設計の次の版に反映するだけ。
  - 入れ子の `Operation` 列挙が無い。
  - `CallNative` の引数に `allocated` が無い。
  - `OpenNative` と `CallDirect` に分けた。
  - なお、設計 5.1 の「`AndroidDialogCApiTests` から移す」は、HEAD に移すテストが無かったので、該当しない。

#### 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: ○
  - 違いは 2 つ。どちらも害は無い。
  - `AndroidDialogCApiTests` は、移すテストが無かったので変えていない。
  - UI の補助と設計書 v6 は、過去のコミットで済んでいる。
- テスト方針の網羅性: △（B-1、B-2）
- エラーケース全実装: ○（E-1〜E-23。起こせないものは、文言を層 1 で固めている）
- 返却仕様との整合: ○
  - 文言は 4.4 とすべて一致する。層 1 の `FailureMessage_EveryValue` で固めている。
  - 成功のときは `ErrorMessage == null`（`ShareOperationResult.Success`）。

#### プロジェクトルール適合チェック

- common.md 準拠: ○（「Share の例外」に沿っている。C-5 の記述の漏れだけ）
- csharp.md 準拠: ○
  - `#nullable enable`、`LogTag`、結果として返す失敗は `LogWarning`、正しい実装で起きないものは `LogError`。
  - `[{LogTag}] … can only be called on Android.` は 1.x の文言に合わせたもの。
- Bridge 実装品質（スレッド安全性・メモリ管理）: ○
- 既存 API 互換性: ○
  - メソッド 9 本、イベント 3 本、定数 9 個、`Initialize()`、既定値は変わっていない。
  - 例外の型の変更（NRE → `ArgumentNullException`）は 5.3 と XML コメントに書いてある。
- P1〜P5: 違反なし

#### テストカバレッジ

**カバーできている観点**
- 層 1: 構造体の大きさと全フィールドの位置、要求の ID が 64 ビット、組み立て、`IsBlank` / `IsAbsent`、Base64 と画像の印、`NormalizeActions`（重複と U+0000 / サロゲートの比べ方）、取り消す ID の 16 個の上限、すべてのエラー値の文言、`MissingPath`。
- 層 2a: Editor の 9 操作が呼び出しの後に失敗すること、per-call の枠を取り出す時点（同期と完了）、J-S14、2 つ目の Manager（J-S15、K-2）、`CallNative` の 3 経路（完了と `release` が先に来る、入口の拒否、例外）、無効・壊れた Manager への配送、選択、Chooser Action（per-call は残る、受け口の時点、K-1、Manager がいないと捨てる）。
- 層 2b: 実機 45 本（Sharesheet の表示、押す、無いことの確認、選択、取り消しの `ulong`、`Initialize` 2 回、後ろから呼ぶ、別のスレッド、Direct Share）。後片付けで、数・UnityMain・`PendingCallCount` が 0 に戻ることを照らす。

**足りない観点**
- 共通イベント → per-call の順（B-1）
- 生きている Manager を壊したときに、枠と `onSelected` が消えること（B-2）
- null 引数で、Share 以外の経路が何も登録しないこと（C-1）
- `OpenNative` の組み立ての例外と `CallDirect` の例外の経路（実機専用で、起こしにくい。コードレビューで確かめた）

#### 総合評価

**要修正（軽微）**
- A は無い。B-1 と B-2 は中心の契約（公開 XML で約束した順と、J-S15）の検証の穴なので、テストを足すことを勧める。
- 足したテストは、上のミューテーションを入れて落ちることを確かめる。

---

件数: A 0 件 / B 2 件 / C 10 件

A は 0 件

## 2. Codex

### 指摘

#### A-1: 「どのスレッドからでも呼べる」が、初回の `Instance` 取得では成立しない

AndroidShareManager.cs:125（/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/AndroidShareManager.cs）

`Instance` は同期も main-thread 判定もなく、`_instance == null` の場合に `GameObject` の生成と `AddComponent` を実行します。さらに `_instance == null` 自体が `UnityEngine.Object` の演算です。

具体的な端末上の失敗経路:
1. アプリ開始後、Unity main が一度も `AndroidShareManager.Instance` に触れていない。
2. worker thread から通常の公開 API 使用形で `AndroidShareManager.Instance.ShareText(...)` を呼ぶ。
3. worker thread 上で `new GameObject` / `AddComponent` が実行され、Unity の main-thread 制約による例外が呼び出し側へ漏れる。
4. `ShareOperationCompleted` も per-call callback も発火しない。

複数 worker が同時に初回取得すると、二重生成の競合もあります。実機テストはすべて事前に main thread で `Manager` を取得しており、この経路を避けています（AndroidShareManagerPlayerTests.cs:201、AndroidSharePlayerTests.cs:398、AndroidSharePlayerTests.cs:452）。

発生するのは「最初の Share 呼び出しを background thread から行う利用者」に限られますが、その場合は高確率で再現します。J-S16 と Manager の XML コメントが無条件に別スレッド呼び出しを保証しているため、A です。

#### B-1: `volatile s_coordinator` は per-call callback のデータ競合を防いでいない

AndroidShareManager.cs:71、ShareChooserActionCallbackCoordinator.cs:16 / 38 / 54

`ShareText` は任意の caller thread から `_pending` を書き、Chooser Action の受け口は Android main から同じフィールドを読みます。`s_coordinator` が `volatile` でも、その後に書かれる coordinator 内部の `_pending` には可視性や排他性を与えません。

古い Sharesheet のタップと background thread 上の新しい `ShareText` が重なると、タップ時点の callback が新旧どちらになるか、C# メモリモデル上で保証されません。`OnDestroy.Clear()` と Android main の `Fire()` にも同じ競合があります。端末での発生窓は狭く、設計 5.3 も複数スレッド時の配送差を一部許容しているため A ではなく B としました。J-S16 と K-1 の「受け口時点の snapshot」を明確に実現するなら、coordinator 内部にも同期が必要です。

#### B-2: K-1 の破棄後配送テストが per-call callback を検証していない

AndroidShareManagerEditorTests.cs:376

`ChooserAction_TakenThenManagerDestroyed_StillReachesItsSubscribers` は global event しか購読しておらず、`ShareText(..., onChooserAction: ...)` を登録していません。K-1 は、受け口で閉じ込めた「イベントと per-call」の両方が Manager 破棄後にも一度届く契約です。

現在の実装は逐次実行なら `_pending` を `Fire` 内で snapshot するため正しく動きます。ただしテストは、`Clear()` の位置変更などで per-call だけ失われても成功します。中心契約の片側を検出できないため B です。

#### C-1: Direct Share アイコンのログ単位が設計と異なる

AndroidShareManager.cs:347

設計 4.4/J-S10 はアイコンを「バイト数」でログする契約ですが、実装は Base64 文字列の `Length` を `"characters"` として出しています。実装結果も「文字数」と記載しており、設計との不一致を既知差分にしていません。動作や秘匿性への影響は小さいため C です。

#### C-2: 実装結果の 5.3 相当部分に K-1 の例外が反映されていない

artifact/android/share/results/2026-10-12-android-share-implementation-feature-result-v1.md:127

K-1 は、Chooser Action だけは通常の J-S4 と異なり「受け口時点の古い Manager に閉じ込め、破棄後もそこへ出す」と設計 5.3 に明記する持ち越し事項です。実装結果の 1.1 では触れていますが、3.4 は「設計 5.3 のとおり」とだけ書いています。参照先の設計 5.3 は、まだ全結果を「queue 取り出し時の live instance」に配送すると記載しています。移行資料に誤った一般則が渡るため C です。

### 総合評価

要修正（重大）です。

- C ABI の署名、`Pack = 8` の構造体、`ulong` の request ID、`MonoPInvokeCallback`、delegate の static lifetime は `Share.h` と一致しています。
- `user_data` 表は extern 直前に登録され、release-only の入口拒否、完了直後の release、入力用 unmanaged memory の `finally` 解放に対応しています。
- `ntk_string` の所有権移譲と `finally` の解放も適切です。
- Editor/device の二重ガードと公開メソッド・イベントのシグネチャは保たれています。
- adb と Unity は実行しておらず、ファイルも変更していません。

A は 1 件

## 3. 統合（止める基準の判定）

| 分類 | サブエージェント（S:） | Codex（X:） |
|---|---|---|
| A | 0 | 1（X:A-1） |
| B | 2（S:B-1、S:B-2） | 2（X:B-1、X:B-2） |
| C | 10（S:C-1〜S:C-10） | 2（X:C-1、X:C-2） |

同じ所を指すもの: X:C-1 と S:C-4（Direct Share のアイコンのログ）、X:C-2 と S:C-6（K-1 の書き写し）。

- X:A-1 は、最初の `Instance` の取得を別のスレッドから行うと、GameObject を作れずに失敗する（1.x も Dialog も同じ前提）。公開の XML コメントが「どのスレッドからでも呼べる」と無条件に書いていたのが誤り。前提として書く形で直した
- どちらのレビュアーも、C ABI の署名、構造体の配置、`user_data` と `release` の寿命、`ntk_string` の所有権、ロックの使い方、ガード、公開 API の互換に問題を見つけなかった

**A が 0 でないので止めない。** 直して再レビューする。

### 反映（2026-10-12）

| 指摘 | 直したところ |
|---|---|
| X:A-1 | `AndroidShareManager` の XML コメント（クラスと `Instance`）に「`Instance` は最初に UnityMain で取る。その後はどのスレッドからでも呼べる」と書いた。`common.md` の「Share の例外」と実装結果 3.4 にも書いた |
| X:B-1 | `ShareChooserActionCallbackCoordinator` の per-call の欄を `volatile` にした（呼んだスレッドで書き、Android の main の `Fire` が読む）。振る舞いは変えない |
| X:B-2 | K-1 のテストに per-call を足した（Manager を壊した後も、受け口の時点の per-call に 1 回出る） |
| S:B-1 | 共通イベント → per-call の順を、すぐ決まる結果と完了の両方で照らすテストを足した |
| S:B-2 | Manager を壊した後に作り直し、前の Manager の per-call と `onSelected` が呼ばれないことを照らすようにした |
| S:C-1 | null の引数のテストで、すべての操作に callback を渡し、どの操作の完了も per-call を見つけないこと、`onSelected` が待っていないことを照らした |
| S:C-2 | `EnsureListeners` の `extern` を `try` で囲み、例外を `LogWarning` にした |
| S:C-3 | 共通部の `s_state` は差分の外。ちぎれて読んでも失敗を返すだけなので、Clipboard の移行で共通部を触るときに見る（持ち越し） |
| S:C-4、X:C-1 | Direct Share のアイコンは、Base64 を解いた後にバイト数でログに出すようにした |
| S:C-5 | `common.md` の「Share の例外」にログの項を足した |
| S:C-6、X:C-2 | 実装結果 3.4 に、J-S4 の行と Chooser Action の例外（K-1）を書いた |
| S:C-7 | XML コメントに、Direct Share が呼んだスレッドで同期に動くこと、Base64 の範囲、U+0000 とサロゲートを足した。`artifact/` のパスは Dialog と同じく残した |
| S:C-8 | `testing.md` の A 群の行を「null の引数を除き」に直した |
| S:C-9 | 実機テストの XML コメントの参照を UI テスト計画 v6 にした。反射で版を見分ける形は、1.x の分岐を消すときに直接の参照に替える（実装結果 7 章） |
| S:C-10 | 設計の次の版（K-4、K-5 と一緒）に反映する |

足したテストが守るべき所を壊したときに落ちることを、壊した版で確かめた（2026-10-12）: `Deliver` で per-call を先に呼ぶ版、`OnDestroy` で per-call の枠を消さない版、`onSelected` を消さない版、`ShareFiles` / `ShareImages` で null の確かめの前に枠を入れる版、coordinator の `Fire` が per-call を後から読む版の 5 通りすべてで、足したテストが落ちた。層 1 は 153/153、層 2a は 36/36。
