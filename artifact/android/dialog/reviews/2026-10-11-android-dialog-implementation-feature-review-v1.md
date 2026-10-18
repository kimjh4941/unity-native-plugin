# Android Dialog 実装レビュー v1（review-implementation-feature）

- 対象: ブランチ `feature/UNT-13`。`d9714e4` の上にある未コミットの差分（`git diff` で取れる追跡済みファイルと、`git status` に出る未追跡ファイルの全文）
- 実装計画: `artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`
- UI テスト計画: `artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v1.md`
- 実装結果: `artifact/android/dialog/results/2026-10-11-android-dialog-implementation-feature-result-v1.md`
- 一次資料: native-toolkit の `android/android_library_capi/src/main/cpp/include/NativeToolkitC/Dialog.h`、`src/Dialog/Dialog.cpp`、`src/Common/{Accept.h,StructSize.h,Registry.cpp}`、`.../capi/jni/DialogBridge.kt`
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけで、ファイルの変更と実機・Unity の実行はしていない。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない）

## 1. サブエージェント（Claude）

### レビュー概要

Android Dialog の呼び出し先を、1.x の Unity ブリッジ（`AndroidJavaObject` / `AndroidJavaProxy` と `UnityMainThreadDispatcher`）から、C ABI 2.0.0（`libntk.so`、`DllImport("ntk")`）に移す変更。

- Bridge として `AndroidDialogCApi` を新設した。中身は構造体、`extern`、純粋な変換と検査、`Read`
- `AndroidDialogManager` を書き直した。公開 API は 1.x のまま。完了の受け口は `[MonoPInvokeCallback]` の static メソッドに置いた
- 共通部の取り出し口 `AndroidNativeCallbackQueue` を新設し、共通部 `AndroidNativeToolkitCApi` に `ReadUtf8OrNull` を足した
- テストは 4 層: 層 1（59 本）、層 2a（9 本）、層 2b（34 本。1.x の記録 29 本を引き継ぎ、2.0.0 だけの 5 本を追加）
- あわせて、サンプルのログと画面、`verify_unity_android.sh`、`agent-rules` の 3 文書、artifact を更新した

ヘッダーと実装に照らして確かめ、問題が無かった点:

- **構造体の配置**: 6 つすべてで、フィールドの順・型・`#pragma pack(8)` が `Dialog.h` と一致する。64 ビットでの大きさは 40 / 48 / 64 / 64 / 64 / 72。`struct_size` は `Marshal.SizeOf`、`reserved*` は 0。`structs::Read` の条件（`size >= sizeof` かつ `reserved0 == 0`）を満たす。単一選択・入力・ログインの `reserved1 == 0` の検査も通る
- **値と型**: `ntk_dialog_error` の 0〜10、answer、button の値が一致する。callback の型 `(void*, uint64_t, int32_t, uint32_t, ntk_dialog_result*)` は `ResultCallback` と一致する。`checked` は `const int32_t*` で、C# も 4 バイトの int を書く。C の `bool` はどこにも無い
- **所有権**: `Dialog.cpp:107-113` で、`error == NONE` の結果は callback に所有権ごと渡る。受け口は `Read` を `try/finally` で囲み、`ntk_dialog_result_free` をちょうど 1 回呼ぶ（`Read` が例外を投げても呼ぶ）。`release` は NULL。入口で拒まれた場合は callback が来ない
- **NULL と空**: `OptionalText` は、値が無ければ NULL・長さ 0、空なら非 NULL・長さ 0 を返す。`ReadUtf8OrNull` はこの 2 つを区別する。`size_t` から int への変換は `ToByteCount` で、`int.MaxValue` を超えると例外にする。DISMISSED のときは answer を先に読み、button を読まない
- **入力の寿命**: 文字列と配列は、`Accept` の前に Java の配列へ写される（`Dialog.cpp:250-264` ほか）。呼び出しの直後に `FreeAll` してよい。確保は 1 つの一覧で追い、`finally` で解放している
- **`user_data`**: `KindData` は `new IntPtr((int)kind)` で、受け口は `ToInt32()` で戻す。値は 1〜6 なので往復で崩れない
- **IL2CPP**: `[MonoPInvokeCallback]` は static。delegate は `static readonly` に保ち、`GetFunctionPointerForDelegate` は 1 回だけ。フィールドの初期化順（`s_callback` が先）も正しい
- **ガード**: 型は `UNITY_ANDROID || UNITY_EDITOR`、`extern`・`Read`・受け口は `UNITY_ANDROID && !UNITY_EDITOR`
- **J-D14（`Show*` が戻る前にイベントを出さない）**: 実装は構造として満たす。同期の失敗だけが `Show*` の中でイベントを出す。受け付けた後の完了は `ConcurrentQueue` に入るだけで、UnityMain の `Update` で出る。`Update` は始めの件数だけを走らせるので、購読者の中から `Show*` を呼んでも同じ回では走らない
- **1 回の呼び出しにイベント 1 回**: 受け付けた要求の完了はネイティブが 1 回だけ渡す（registry）。`Post` も 1 回、`Raise` も 1 回。同期の失敗では、`fail` がネイティブの受け付け後に呼ばれる経路は無い
- **`isSuccessful == true` のときの `errorMessage`**: ボタンなら `null`、閉じたときは `""`。1.x と同じ
- **Editor での取り出し口**: ドメインの再読み込みを切っていても、`s_pump` は `OnDestroy` で消え、Unity の `==` でも null と判定される。`s_actions` は static のまま残るが、Editor で `Post` するのはテストだけで、テストは `SetUp` で `ResetForTests` を呼ぶ。実害は無い
- **ログに秘密を出さない**: `Debug.Log*` をすべて見た。対象は Manager、受け口、取り出し口、サンプル、テスト。入力・利用者名・パスワードは長さだけを出す。受け口は種類・ID・エラーだけを出す。テストが出すのはテスト用の固定値だけ

### 重大な問題（high）

なし

### 改善提案（medium）

**R1-1** 後片付けを設計 7.3 から変えた結果、完了が次のテストに漏れうる。結果ファイルにある、この変更が安全だという理由も事実と違う

- 場所: `Tests/PlayMode/AndroidDialogPlayerTests.cs:57-81`、結果ファイル 1.2（57 行目）・5.2（216 行目）・DoD（226 行目）
- 観点: テストカバレッジ / 計画との整合
- 区分: B（テストの分離と、「1 回の呼び出しにイベント 1 回」の確認。中心の契約に関わる）と C（結果ファイルの記述）
- 何が起きるか: NUnit は fixture の 1 つのインスタンスで全テストを走らせる。次の `SetUp` は同じ `_events` を空にして購読し直す。そのため、`ResetForTests()` の後に Android の main から入った完了は、次のテストのイベントとして数えられる。結果ファイル 1.2 の「購読を先に外すので、遅れて届いた完了は次のテストの購読者には数えられない」は誤り。先に購読を外すことで防げるのは、TearDown の間に入る分だけ
- 具体例: テストが失敗して TearDown に入る。受け付け済みのダイアログが遅れて出て、`count` が 0 と読まれる。または、閉じた後の完了が 1 秒より遅れる。どちらも 1 秒後に列を空にした後で完了が届き、次のテストの `_events` に入る
  - 次のテストが完全一致で比べる場合（AD-25 の `IsEmpty`、`FailsAndShowsNothing`、AD-29〜31）は、失敗が連鎖し、原因を取り違える
  - `Contains` で比べるテスト（AD-10〜AD-24 など）は、余分なイベントがあっても通る
  - 残ったダイアログは、次のテストの `Ui.Do("wait"/"tap", Res(Title))` が押してしまう。補助は題名の文字ではなく resource-id でダイアログを探すため
- 判定: すべて通る実行（47/47）に限れば、テストが自分で完了を待っているので漏れは起きない。設計からの逸脱として受け入れられるのは、その範囲だけ。失敗の後の分離と、1 回だけの照合が失われている
- 直し方:
  1. 設計どおりに数を照らす。各テストが受け付けさせた要求の数を `_accepted` に足す。TearDown の間も外さない別の購読で、出たイベントの数を数える。TearDown は、2 つが等しくなるまで（期限 5 秒）待ってから `ResetForTests()` を呼ぶ。等しくならなければ `Assert.Fail` にする
  2. 少なくとも、結果ファイル 1.2 の理由を正す

**R1-2** 「1 回だけ」を確かめているのは Confirm の肯定と取り消しだけ。ほかの種類は `Contains` で比べている

- 場所: `AndroidDialogPlayerTests.cs:473-479`（`ShowAndAct`）、`491-495`（`ExpectEvent`）
- 観点: テストカバレッジ
- 区分: B（中心の契約）
- 何が起きるか: イベントの数を照らすのは AD-08、AD-25、AD-26、AD-28 の 4 本だけで、どれも Confirm。Back、外側のタップ、否定、ほかの 5 種類（AD-10〜AD-24）は `Does.Contain` だけで比べている。イベントが 2 回出るように壊れても、これらは通る。たとえば、受け口が 2 回 `Post` する変更や、Kotlin 側で onCancel と onDismiss の両方が報告する変更
- 直し方: `ShowAndAct` の終わりに、0.5 秒待ってから `Assert.AreEqual(1, _events.Count)` を足す（AD-25 と同じ形）。R1-1 の照合を入れれば、それでも代わりになる

**R1-3** AD-25 は J-D14 を空振りで確かめている。「`Post` が中で処理を走らせない」ことを直接確かめるテストも無い

- 場所: `AndroidDialogPlayerTests.cs:272-281`、`AndroidDialogManagerEditorTests.cs:128-170`
- 観点: テストカバレッジ / 正確性
- 区分: B（中心の契約。J-D14）
- 何が起きるか: AD-25 は `ShowConfirmDialog` の直後にイベントが 0 回であることを見る。しかし、補助がボタンを押すまで完了は起きないので、実装が「`Show*` の中で完了を出す」ように壊れても 0 回になる
  - 起こすべき状況は、`Show*` が戻る前に Android の main から完了が入ること
  - 層 2a の配送のテストも、`Post` の直後にイベントが 0 回かを見ていない。2 フレーム待ってから見ている
  - そのため、`Post` を「UnityMain なら中で走らせる」最適化に変えても、どのテストも落ちない
- 直し方: 層 2a に 1 本足す。`PostCompletion(...)` の直後に `Assert.IsEmpty(_events)` を書き、1 フレーム後に 1 回になることを見る。層 2b の AD-26 では、`ntk_dialog_cancel` の後に UnityMain で `Thread.Sleep(300)` を挟み、まだ 0 回であることを見てから `yield` すると、空振りでなくなる

**R1-4** 層 2a の配送のテストは、本番の受け口が積む式を通っていない

- 場所: `AndroidDialogManagerEditorTests.cs:125-126`、`140-151`。本番は `AndroidDialogManager.cs:455`
- 観点: テストカバレッジ
- 区分: B（範囲）
- 何が起きるか: `PostCompletion` は `() => AndroidDialogManager.Instance.Raise(answer)` をテストの中で書き直している。本番の `OnResult` の中の式（`!UNITY_EDITOR`）とは別物。たとえば、本番を `_instance?.Raise(...)` やキャプチャしたインスタンスに変えて、壊れた Manager や新しい `Instance` に届かなくしても、層 2a は落ちない
  - 層 2b の AD-26 / AD-27 は本番の式を通る。ただし、無効や破棄の条件では流していない
  - `Completion_AfterTheManagerIsDestroyed_GoesToTheNewInstance` も、名前と違い、新しい `Instance` にイベントが出たことは見ていない。見ているのは、新しいインスタンスが作られたことだけ
- 直し方: `OnResult` の `Post` の部分を、ガードの外の `internal static void PostAnswer(AndroidDialogCApi.Answer)` に切り出す。受け口とテストの両方がそれを呼ぶようにする。破棄のテストでは、新しい `Instance` を `FindFirstObjectByType` で取ってから購読し、そこに 1 回出ることを確かめる（`Instance` を作るのは取り出し口の処理なので、手順は工夫が要る）

### 軽微な指摘（low）

**R1-5** 取り出し口の GameObject が利用者のコードで壊されると、以後の完了は何のログも無く失われる

- 場所: `AndroidNativeCallbackQueue.cs:51-59`、`79-83`。`EnsureCreated` を呼ぶのは `AndroidDialogManager.cs:97` の `Awake` だけ
- 観点: Bridge の品質 / 正確性
- 区分: A（ふつうでない条件。シーンの読み込みでは `DontDestroyOnLoad` なので壊れない）
- 何が起きるか: DDOL のオブジェクトを全部消す「リセット」処理などで `AndroidNativeCallbackQueue` だけが壊され、Manager が残ったとする。`Post` は列に積み続けるが、誰も取り出さない。イベントは永久に出ず、ログも無い。列は伸び続ける
  - 1.x も dispatcher が壊れると失ったが、そのときは `LogError` が出た
- 直し方: `ShowNative` の先頭で `AndroidNativeCallbackQueue.EnsureCreated()` を呼ぶ。UnityMain から呼ぶ約束（J-D14）があり、ほとんど費用はかからない

**R1-6** 受け口では、種類への変換が内側の `try/finally` より前にある

- 場所: `AndroidDialogManager.cs:444`
- 観点: Bridge の品質（メモリ）
- 区分: A（潜在。今の呼び出しでは起きない）
- 何が起きるか: `userData.ToInt32()` が例外を投げると、結果のハンドルは解放されない。今は自分で渡す 1〜6 しか来ないので起きない
- 直し方: 変換を内側の `try` に入れれば、ハンドルの解放がコードの形で保証される

**R1-7** 層 1 の網羅が、計画 7.1 と結果ファイルの記述に届いていない

- 場所: `Tests/Runtime/AndroidDialogCApiTests.cs:193-280`
- 観点: テストカバレッジ / 計画との整合
- 区分: B（範囲）と C（結果ファイル 5.1 の「6 種類 × 肯定・否定・閉じた・失敗」「全値と想定外の値」）
- 足りないもの:
  - Confirm の `ToButtonArgs` が 1 件も無い
  - TextInput の失敗が無い。`ToTextInputArgs` が失敗のときに入力を返すように壊れても、層 1 でも 2b でも落ちない
  - MultiChoice の失敗は `isSuccessful` しか見ていない
  - `BuildConfirm` が無い。選択・入力・ログインの肯定・否定の文字のポインタも読み戻していない。並びの入れ替えは 2b の AD-03 / AD-13 / AD-19 で検出できる
  - `CompletionFailureMessage` の想定外の値（42 など）と、完了の `NOT_INITIALIZED` が無い
- 直し方: 不足分を足すか、結果ファイルの記述を実態に合わせる

**R1-8** 層 2a の網羅が計画 7.2 に届いていない

- 場所: `AndroidDialogManagerEditorTests.cs`
- 観点: テストカバレッジ
- 区分: B（範囲）
- 足りないもの: 計画 7.2 の「項目の空」が無い（層 1 の `CheckItems` と 2b の AD-31 にはある）。正しい引数で警告だけが出ることは Confirm でしか見ていない

**R1-9** AD-27 は、失敗したときにスレッドを止めない

- 場所: `AndroidDialogPlayerTests.cs:404-444`（`thread.Start()` は 436、待ちは 438）
- 観点: テストカバレッジ（分離）
- 区分: B
- 何が起きるか: 10 秒の `Eventually` が切れたとき、スレッドは最大 20 秒まで答えのファイルを待ち続ける。その後に Bridge を呼ぶと、次のテストの最中にダイアログか完了が出る（R1-1 と同じ漏れ）
- 直し方: `finally` でスレッドに中止を知らせて `Join` するか、TearDown で数を照らす（R1-1）

**R1-10** 取り出し口の `Update` が毎フレーム `Count` を読む

- 場所: `AndroidNativeCallbackQueue.cs:90-93`
- 観点: コード品質（毎フレームの費用）
- 何が起きるか: `ConcurrentQueue<T>.Count` は、セグメントをまたぐと重くなる（ロックやスピン）。空かどうかを見るだけなら `IsEmpty` の方が軽い
- 直し方: `if (s_actions.IsEmpty) return;` を先に置き、件数は空でないときだけ読む

**R1-11** 文字列の読み取りで、毎回 delegate を作っている

- 場所: `AndroidDialogCApi.cs:482-488`
- 観点: コード品質
- 何が起きるか: `ReadString(ntk_dialog_result_text, result)` は、`extern` メソッドから delegate を作るので、完了 1 回につき最大 4 回の確保がある。実機で通っているので IL2CPP の互換性の問題ではない
- 直し方: `ReadUtf8OrNull(ntk_dialog_result_text(result, out var size), size)` と直接書けば、確保が無くなり形も単純になる

**R1-12** XML コメントが古い版の設計書を指している

- 場所: `AndroidDialogCApi.cs:28`、`219`、`234`、`284`、`400`
- 観点: 計画との整合
- 区分: C
- 内容: 「design v3」と書いてある。計画は v4

**R1-13** 閉じたときの `errorMessage == ""` という例外が、規則にも XML コメントにも書かれていない

- 場所: `agent-rules/coding-rules/common.md:237`、`AndroidDialogManager.cs:15-37`
- 観点: ルール
- 区分: C
- 内容: common.md の「`isSuccess == true` なら `errorMessage == null`」に対し、Android Dialog が閉じたときに `""` を返す例外（計画 2 章）が、common.md にも Manager の XML コメントにも無い

**R1-14** testing.md 3 節の Manager の数が古い

- 場所: `agent-rules/coding-rules/testing.md:228`、`239`
- 観点: ルール
- 区分: C
- 内容: `AndroidDialogManager` を A 群に移したが、表の行ではなく括弧書きで足している。239 行目の「12 個中 8 個が B 群」は、今は 7 個

**R1-15** ログの書式が csharp.md と違う箇所がある

- 場所: `AndroidDialogManager.cs:363`（`Rejected`）、`372`（`OnDevice`）
- 観点: ルール（csharp.md）
- 内容: `Rejected` は `[{LogTag}] {error}` で、`[{nameof(Method)}]` が無い。`OnDevice` にはタグが無い。ただし `OnDevice` の文言は、1.x と層 2a の `LogAssert` が文字列として照らしているので、変えるならテストもいっしょに直す

**R1-16** 共通部の説明と Manager の実装が食い違っている（要確認）

- 場所: `AndroidNativeToolkitCApi.cs:28-31`（コミット済みのスパイクの説明）
- 観点: 計画との整合
- 区分: C
- 内容: 説明は「callers check IsInitialized first」と書く。Manager は事前に確かめず、入口が返す `NOT_INITIALIZED` に頼っている（計画 4.4 の E-6 どおり）。説明の側を直すのが正しいと思われるが、意図を確かめてほしい

### 実装計画整合性チェック

| 項目 | 判定 | 根拠 |
|---|---|---|
| Manager + Bridge パターン準拠 | ○ | Bridge は Manager に依存しない。受け口・delegate・関数ポインタは計画どおり Manager にある |
| 変更ファイル一覧との一致 | ○ | 5.1 の表と一致し、余分なソースの変更は無い。artifact 2 件の追記は記録の欄 |
| テスト方針の網羅性 | △ | 7.3 の後片付けが違う（R1-1）。「1 回だけ」と J-D14 の確かめが弱い（R1-2、R1-3）。層 1 と 2a に抜けがある（R1-7、R1-8） |
| エラーケース全実装 | ○ | E-1〜E-13 は 4.4 の文言とログの段階どおり。起こせないもの（E-5〜E-7、E-9、E-12、E-13）は、文言を層 1 で固め、形をコードレビューで確かめた（このレビューで確認済み） |
| 返却仕様との整合 | ○ | 4.3 の表（肯定・否定・閉じた・失敗）と、`isSuccessful == true` のときの `null` / `""` が実装と一致する |

### プロジェクトルール適合チェック

| 項目 | 判定 | 根拠 |
|---|---|---|
| common.md 準拠 | ○ | 下の P1〜P5 を参照。記述の抜けは R1-13 / R1-14 |
| csharp.md 準拠 | △ | `#nullable enable`、`LogTag`、XML コメント、LogWarning / LogError の段階は守っている。ログの書式に例外がある（R1-15） |
| Bridge 実装品質（スレッド安全性・メモリ管理） | ○ | ロックを持ったまま処理を走らせない。結果のハンドルは 1 回だけ解放し、確保したものも全部解放する。IL2CPP の形も正しい。潜在の指摘は R1-5、R1-6 |
| 既存 API 互換性 | ○ | 公開 API は 1.x と同じ: `Instance`、`Initialize()`、メソッド 6 本の引数と既定値（`??=` の置き換えを含む）、イベント 6 本の型。変わるのは名前空間（J-D11、移動のコミットで済み）と 5.3 の既知の差分だけ。ガードを `UNITY_ANDROID` から `|| UNITY_EDITOR` に広げたのは追加で、壊す変更ではない |

**プラットフォーム独立性:**

| 項目 | 判定 | 根拠 |
|---|---|---|
| P1 | ○ | 新しいファイルと型はすべて `Android` で始まる（テストも同じ） |
| P1b | ○ | テストは Android の型だけを扱う。ガードは計画どおり: 層 1 は `UNITY_ANDROID \|\| UNITY_EDITOR`、層 2a は `UNITY_EDITOR`、層 2b は `UNITY_ANDROID` の中に `!UNITY_EDITOR` |
| P1c | ○ | 置き場所と名前空間は `Runtime/Android/{Common,Dialog}/` と `JonghyunKim.NativeToolkit.Runtime.Android.{Common,Dialog}`。`.meta` もある |
| P2 | ○ | 変更・追加されたファイルに、`Ios*` / `Mac*` / `Windows*` のソースは無い |
| P4 | ○ | `Runtime/Common/` への追加は無い。`Runtime/Android/Common/` への追加は、common.md に足した条件の範囲 |
| P5 | ○ | 二重のガードになっている |
| 既知の逸脱 11 件 | ○ | 前例として引いた箇所は無い |

### テストカバレッジ

確かめられているもの:

- **層 1**: 6 つの構造体の配置（ヘッダーと一致）、要求の ID が 64 ビット、組み立ての主な項目（`not_*` の向き、UTF-8 の読み戻し、`checked` の短い・長い・null・空）、検査の文言と `checkedItem` の境界（-2、-1、0、末尾、項目数）、結果の写しの大半、エラーの名前、共通部の版の判定・UTF-8・NULL と空の区別
- **層 2a**: 同期の失敗がイベント 1 回であること、Editor の J-D6、購読者の例外、無効な Manager への配送、1 フレームの件数の上限。上限のテストは空振りでない（全部を一度に流す実装なら落ちる）
- **層 2b**: 6 種類の肯定・否定・Back と外側のタップ、閉じられない設定、値の写し（J-D4 の短い・長い、-1、非 ASCII）、回転、取り消し（本番の受け口を通る）、後ろから呼んだとき（E-8）、同期の失敗とダイアログが出ないこと

足りないもの:

- 1 回の呼び出しにイベント 1 回であること（R1-1、R1-2）
- J-D14 を空振りでなく確かめること（R1-3）
- 本番の受け口の式で、無効・壊れた Manager への配送を確かめること（R1-4）
- 層 1 と 2a の抜け（R1-7、R1-8）
- 失敗した後のテストの分離（R1-1、R1-9）
- 起こせない失敗（E-5、E-6、E-7、E-9、E-12、E-13）は、文言の層 1 とこのコードレビューで確かめた。実物は無い（計画どおり）

### 総合評価

**要修正（軽微）**

製品のコードに、利用者から見える欠陥（A）で直すべきものは見つからなかった。C ABI のヘッダーと実装に照らし、構造体の配置、所有権、スレッド、NULL と空の区別、エラーの値はすべて一致した。

直してほしいのは、主に検証の穴（B）。後片付けの設計からの逸脱と、それを正当化する結果ファイルの誤った理由（R1-1）、「1 回だけ」と J-D14 の確かめの弱さ（R1-2、R1-3）、層 2a が本番の経路を通らないこと（R1-4）。コミットの前に、少なくとも R1-1 の記述を正すことと、R1-2 / R1-3 の 1〜2 行の確かめを足すことを勧める。R1-5 は 1 行で直せるので、あわせて直すのがよい。

## 2. Codex

（ID は統合のときに付けた。本文は Codex の出力のまま）

**C1-1（A）アンマネージドメモリが例外時にリークする**

- 対象は AndroidNativeToolkitCApi.cs:91、AndroidDialogCApi.cs:383、AndroidDialogCApi.cs:393 です(すべて Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/ 以下)。
- `Marshal.AllocHGlobal` が成功した後に `allocated.Add` を呼んでいます。`List<IntPtr>.Add` が容量拡張で `OutOfMemoryException` を投げると、確保済みのポインターは一覧に登録されません。そのため外側の `FreeAll` でも解放できません。
- メモリが逼迫した状態で `Show*` を呼ぶと、失敗のたびにネイティブヒープがリークします。項目配列や文字列を多数確保する呼び出しで起きやすいです。

**C1-2（C）実装コメントが最新版ではない設計書を参照している**

- AndroidDialogCApi.cs:28、219、234、284、400 に該当します。実装の基準は design v4 ですが、J-D1、4.4、J-D4、4.3 の根拠がいずれも「design v3」と書かれています。保守時に旧版を参照させる、文書上の不一致です。

確認できる欠陥がなかった領域:

- 構造体の配置・型幅・cdecl・コールバック署名: ヘッダーと一致しています(Pack、フィールド順、`size_t`、`uint64_t`)。
- 結果ハンドルの所有権と `finally` での解放: 確認できる欠陥なし。
- NULL と空文字列の扱い、同期エラーと完了時エラーの区別: 確認できる欠陥なし。
- コールバックのスレッド、呼び出し復帰前の完了、キューのライフサイクル: 確認できる欠陥なし。
- イベントの二重発火・未発火の経路: 確認できる欠陥なし。
- テスト(分類 B): 確認できる欠陥なし。

## 3. 総合評価

**要修正（軽微）**（サブエージェントの評価。Codex は A 1 件・C 1 件）

止める基準の区分での集計: A は 3 件（C1-1、R1-5、R1-6。どれもふつうでない条件か潜在のもの）、中心の契約の B は 3 件（R1-1、R1-2、R1-3）、範囲の B は 4 件（R1-4、R1-7、R1-8、R1-9）、C は 6 件（R1-12 = C1-2、R1-13、R1-14、R1-15、R1-16 と R1-1 の記述）、区分なしのコード品質 2 件（R1-10、R1-11）。A が 0 でないので止めない。
