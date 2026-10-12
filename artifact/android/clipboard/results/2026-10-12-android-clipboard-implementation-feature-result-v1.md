# 実装結果レポート v1（Android Clipboard の C ABI 2.0.0 への移行）

## 基本情報

- 日付: 2026-10-12
- 機能名: clipboard
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-13
- 計画ファイル: `artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md`（以下「設計」）
- 設計のレビュー: `artifact/android/clipboard/reviews/2026-10-12-android-clipboard-design-review-v1.md`、`-v2.md`
- 親課題: `artifact/topics/android-c-abi-2/README.md`
- 同梱: native-toolkit dist 1.13.0（`android-native-toolkit-2.0.0.aar`、`android-native-toolkit-capi-2.0.0.aar`）。移行の間は dist 1.12.0 の 2 本も追加のピンで置く（変えていない）

## 0. 状態サマリー

設計 5.5 の手順 1〜9 を終えた。手順 3〜9 はまだコミットしていない（実装のレビューの後に利用者の確認を取る）。

| 手順 | 内容 | コミット | 状態 |
|---|---|---|---|
| 1 | 設計（v1〜v3。レビュー 2 回） | `7d062ee` | 済み |
| 2 | 1.x のまま 37 件を流して記録（37/37。設計 v4 の 7.3） | `8d71881` | 済み |
| 3 | 共通部の `s_state`（J-C16） | 未コミット | 済み |
| 4・5 | `AndroidClipboardCApi`（options、純粋な関数、`extern`、読み取り）、層 1 | 未コミット | 済み |
| 6 | `AndroidClipboardManager` の置き換え、層 2a。`AndroidClipboardJsonParser` とそのテストを消す | 未コミット | 済み |
| 7 | 実機で 7.3 を流す（C ABI の版。40/40） | 未コミット | 済み |
| 8 | `common.md`、`testing.md`、親課題の README | 未コミット | 済み |
| 9 | 実装結果ファイル（この文書） | 未コミット | 済み |

## 1. 実装サマリー

### 1.1 計画ファイル由来の実装

- **2 層（Bridge + Manager）。** `Runtime/Android/Clipboard/AndroidClipboardCApi.cs`（Bridge）と `AndroidClipboardManager`。共通部（`AndroidNativeToolkitCApi`、`AndroidNativeCallbackQueue`）は Dialog と Share で作ったものを使う
- **ガード。** Manager の型と Bridge の純粋な関数は `UNITY_ANDROID || UNITY_EDITOR`（Manager は B 群から A 群へ）。`DllImport`、読み取りの関数、受け口とその関数ポインタは `UNITY_ANDROID && !UNITY_EDITOR`
- **J-C1、J-C2** copy・clear・stopObserving は C の関数を呼び出しスレッドで同期に呼び、戻り値から作った結果を、その呼び出しの `onResult` を閉じ込めて取り出し口に `Post` する（`Invoke`）。per-call の辞書は消した。順は `InvokeInOrder`（今のまま）
- **J-C3** 結果と変更は、取り出した時点の `_instance` に出し、いなければ捨てる（`Deliver`、`RaiseChanged`）
- **J-C4** 読み取りは呼び出しスレッドで C を呼び、ハンドルを読み（`ReadContent`、`ReadDescription`）、`finally` で `_free`。`""` と NULL を null に、分類の -1 と 0 を null にする（`ToClipContents`、`ToDescriptionInfo`）
- **J-C5** `HasClip` の失敗は `false`
- **J-C6** Editor・Android 以外は 1.x と同じ結果（`CanCall`、`CanRead`）
- **J-C7** `payload` と `texts` の null は `ArgumentNullException`。文字列の null は `""`（`AsPassed`。配列は要素ごと）
- **J-C8** C# は入力を検査しない。エラー値を 1.x の文言とコードに写す（`OperationFailureMessage`、`ReadFailure`）
- **J-C9** 変更のリスナーはプロセスで 1 回だけ登録し、外さない（`EnsureChangeListener`。登録専用の `lock`。失敗は `LogWarning`、例外は `LogError` で、次の呼び出しでやり直す）。`Initialize()` と `StartObserving` から呼ぶ
- **J-C10** `s_observing`（`volatile`）。`StartObserving` が F-9 の前に真にし、失敗なら偽に戻す。`StopObserving` は F-10 の前に偽にする。受け口は偽なら捨てる
- **J-C11** `StartObserving` は結果を出さない。監視が後で始まることを XML コメントに書いた
- **J-C12** `OnDestroy` は自分が `_instance` のときだけ `StopIfObserving`（印が真なら下ろして F-10）。例外は `LogWarning`
- **J-C13** どのスレッドから呼んでもよい。`Awake` と各メソッドで `EnsureCreated()`
- **J-C14** `AndroidClipboardJsonBuilder` は公開のまま残し、Manager からは使わない（XML コメントに書いた）。`AndroidClipboardJsonParser` とそのテスト 39 本を消した
- **J-C15** 今の C# のログ（長さ・有無）を保つ。失敗のログはエラー値の名前だけ（`INVALID_URI` の文言の URI を出さない）
- **J-C16** `AndroidNativeToolkitCApi.s_state` を `volatile int`（0 が未確認、ほかは `(int)state + 1`）にした
- **J-C17** UI の補助と UI テスト計画は作らず、設計 7.3 の表を使った

### 1.2 実装時の追加判断

- **AC-33 からライブラリ名と呼び出し規約の照合を外した**（設計 9 章の K-6 が実際に起きた）。IL2CPP の Player では、`DllImportAttribute` が反射で取れても `Value`（ライブラリ名）が空だった（2026-10-12、1 回目の通しの検証で AC-33 が落ちた）。名前・戻り値・引数・`internal static`・`PinvokeImpl` の照合は残した。ライブラリ名は、実機のすべての呼び出しが使っている
- **公開の `StopObserving()` も、`OnDestroy` と同じ F-10 の関数（`StopNative`）を通す**（設計 v3 の X2-2）。層 2a の差し替えは `OnDestroy` の判断で数え、`StopObserving` の F-10 は実機の AC-34 で確かめた
- **Editor の既定の登録と F-10 は「何もしない」（`NONE`）**。層 2a は `SetNativeForTests` で数える関数に替える
- **`ReadContent` / `ReadDescription` は Bridge の `extern` 側に置いた**（設計 5.1 のとおり）。MIME の NULL は `""` にした（ライブラリは MIME を NULL で返さないが、`List<string>` に null を入れないため）

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Clipboard/AndroidClipboardCApi.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardCApiTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidClipboardManagerEditorTests.cs`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Clipboard/AndroidClipboardManager.cs`（`AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher` / per-call の辞書を消して書き直し。公開 API・イベント・既定値・文言は同じ。変わったのは 3.4 の既知の差分だけ）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeToolkitCApi.cs`（`s_state`）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Clipboard/AndroidClipboardJsonBuilder.cs`（XML コメントだけ）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Clipboard/ClipboardReadResult.cs`、`ClipboardDescriptionResult.cs`（XML コメントから JSON の言い回しを外した。約束は変えない）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardManagerDispatchTests.cs`（ガードを `UNITY_ANDROID || UNITY_EDITOR` に）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs`（AC-32〜AC-34 を `UNITY_ANDROID && !UNITY_EDITOR` の中に足した。AC-12〜AC-31 は手順 2 でコミット済み）
- `agent-rules/coding-rules/common.md`（Android の節に「Clipboard の形」）、`agent-rules/coding-rules/testing.md`（ガードの表、層 1・2a・2b、機能の表）
- `artifact/topics/android-c-abi-2/README.md`（J-9 に Clipboard、段の状況）

### 2.3 削除

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Clipboard/AndroidClipboardJsonParser.cs`、`Tests/Runtime/AndroidClipboardJsonParserTests.cs`（J-C14）

### 2.4 非変更（対象だが未変更）

- Payload 4 型、`ClipboardOperationResult`、`AndroidClipboardJsonBuilderTests`、`AndroidClipboardSampleSceneWiringTests`、`AndroidClipboardManagerExampleController`、`Runtime/Resources/UI/Android/Clipboard/*`

## 3. エラー契約反映

### 3.1 エラーケース実装反映（設計 6 章）

| # | 実装 | 検証 |
|---|---|---|
| E-1 | 各 copy の先頭の `ArgumentNullException` | 2a `NullPayload_ThrowsAndRaisesNothing`、2b AC-27 |
| E-2 | `AsPassed` | 1 `AsPassed_*`、2b AC-26a〜e |
| E-3 | `CanCall`、`CanRead`、`StartObserving` の判定 | 2a `Editor_*` |
| E-4〜E-6、E-10〜E-13、E-15 | `OperationFailureMessage`、`ReadFailure`、`IsRequestDefect` | 1 `OperationFailureMessage_EveryValue_EveryOperation`、`ReadFailure_EveryValue`、`IsRequestDefect_*` |
| E-7 | `EMPTY_CONTENT` の写し | 1、2b AC-09、AC-26c |
| E-8 | `EMPTY_ITEMS` の写し | 1、2b AC-10 |
| E-9 | `INVALID_URI` の写し（入力の URI） | 1、2b AC-11、AC-15、AC-26d |
| E-14 | `HasClip` の `false` | コードレビュー（起こせない） |
| E-16 | `EnsureChangeListener`、`StartObserving` の失敗の扱い | 2a `Listener_*`。開始の失敗はコードレビュー |
| E-17、E-18 | `Invoke` と読み取りの `catch`、受け口の `try/catch` | コードレビュー（起こせない） |
| E-19 | 空は `Empty` | 2b AC-06、AC-19 |

### 3.2 イベントの返却仕様

- `ClipboardOperationCompleted` → per-call の順。どれも呼び出しの後、UnityMain で 1 回（2a、2b AC-20）
- `ClipboardChanged`: `StartObserving` の後、印が真の間だけ。UnityMain（2a、2b AC-21a〜c、AC-22、AC-23、AC-31、AC-32）

### 3.3 success 時契約

copy・clear の成功は、呼び出しが戻った時点でクリップボードが変わっていること（AC-28）。`StopObserving` の成功は、止める処理を Android の main に積めたこと（AC-34 で、その後に変更が来ないことを確かめた）。

### 3.4 既知の差分（マニュアルへ引き継ぐ）

設計 5.3 の表を写し、1.x の実測（設計 v4 の 7.3）で決まった行を書いた。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| copy・clear が動く時点 | Android の main に積んで後で動いた。ただし直後の `Read` は 10 回すべてで新しい本文を読んだ（AC-28） | 呼び出しの中で動く。直後の `Read` は必ず新しい本文を読む（AC-28: 10/10） |
| `StartObserving` の直後の変更（J-11 の例外。利用者の判断） | 同じフレームの URI のコピーで 1 回届いた（AC-21b） | 監視は Android の main が積んだ処理を走らせてから始まる（時間の保証は無い）。それより前のコピーの変更は届かないことがある。今回の実機では 2 回とも届いた（AC-21b: 1） |
| 確保の失敗とメモリ不足 | C# の確保の例外は呼び出し側に出た。Kotlin の `OutOfMemoryError` は捕まえなかった | 結果の失敗（`ex.Message` / `Failed: OUT_OF_MEMORY`） |
| `currentActivity` | 要った | 要らない |
| 別のスレッドから呼ぶ | 受け付けなかった（AC-24: `copyPlainText could not be started.`、AC-25: `CLIPBOARD_BRIDGE_UNAVAILABLE` / `read could not be started.`。どちらもエラーのログ 2 行） | 受け付ける（AC-24: 成功が UnityMain で 1 回、AC-25: 読めた）。`Instance` の最初の取得は UnityMain で行う |
| 同じ操作を結果の前にもう一度呼ぶ | 枠は後勝ち | それぞれが自分の per-call を受ける |
| 文字列の null | 文字列 `"null"` として扱った（AC-26） | `""` として扱う（`text`・要素は成功して `Read` で null、`htmlText` は `EMPTY_CONTENT`、`uri` は `Invalid URI: `） |
| `payload` / `texts` が null | `NullReferenceException`（AC-27） | `ArgumentNullException` |
| 文字列の途中の U+0000、対になっていないサロゲート | JSON で運んだ | 切れる / U+FFFD |
| `SECURITY` の文言 | `...: <例外の message>` | `Security restriction while accessing clipboard.` |
| `UNKNOWN` などの文言 | `Failed: <例外の message>` | `Failed: <名前>` |
| 未初期化 | その状態が無かった | `<op> could not be started.` |
| `StopObserving` の結果 | いつも成功。止めた後に出た | 積めれば成功。積んだ時点で出る |
| 監視の共有 | ブリッジ専用のモニター | プロセスに 1 つ。Unity の停止と破棄は Kotlin の利用者の監視も止める。Kotlin の利用者の監視は `ClipboardChanged` に出ない（AC-32）。Kotlin の利用者の停止は Unity にも効く |
| 結果と変更が Manager を壊す前後に来る、UnityMain の処理の中から呼んだときのフレーム、ログの重さ、logcat、Editor、`Initialize()`、32 ビットと Mono、`AndroidClipboardJsonBuilder` | 設計 5.3 のとおり | 設計 5.3 のとおり |

## 4. ビルド結果

- Editor のコンパイル: 成功（EditMode と Editor の PlayMode の実行で確かめた）
- 実機の Player（Android™ Profile と Test Android™ Profile、IL2CPP、ARM64）: 成功

## 5. テスト結果

`scripts/verify_unity_android.sh --as-package-user` の通しの実行（2026-10-12、Pixel 6a、Android 16）。

| 実行 | 結果 |
|---|---|
| UI の補助の doctest | passed |
| EditMode（Android™ Profile） | 1263 通過、1 スキップ（前は 1278。parser のテスト 39 本を消し、層 1 を 24 本足した） |
| Editor の PlayMode（層 2a） | 264 通過、37 スキップ（`AndroidClipboardManagerEditorTests` 18 本を足した。スキップは実機専用の Clipboard の 37 本） |
| 実機（IL2CPP、Android™ Profile） | 123/123（Clipboard 40、Share 45、Dialog 36、共通部 2。補助の終了コード 0、`AndroidUiDriver` のテスト 65、スキップ 0） |
| 実機（パッケージの利用者の条件） | 101 通過、22 スキップ（サンプル画面のケース） |
| 全体 | `== PASS ==` |

### 5.1 テスト詳細

- 1 回目の通しの実行では、AC-33 だけが 2 つの実機の実行で落ちた（1.2 の 1 つ目）。直した後の 2 回目がこの表
- 2b の `[ClipboardRecord]`（C ABI の版）: AC-21a 1、AC-21b 1、AC-21c 2、AC-24 成功、AC-25 `HasContent`、AC-26a〜e `""` の扱い、AC-27 `ArgumentNullException`、AC-28 10/10、AC-29 分類 3、AC-31 1
- 2b の各テストは、受けたイベントが UnityMain で出たことを照らした（AC-20、AC-21a、AC-24）

### 5.2 未実施ケース詳細

- E-14、E-17、E-18 と、`StartObserving` の失敗は起こせない（文言は層 1、形はコードレビュー）
- サンプルアプリの手動確認（設計 7.4）は、利用者の確認で行う

## 6. Definition of Done

| 項目 | 状態 |
|---|---|
| 公開 API を保つ | 済み |
| 設計の J-C1〜J-C17 | 済み |
| 層 1・2a・2b | 済み |
| 既知の差分を XML コメントと実装結果に書く | 済み |
| 通しの検証スクリプト | 済み（PASS） |
| 実装のレビュー | 未（次） |

## 7. 残作業

- **実装のレビュー**（サブエージェントと Codex を並行。`agent-rules/workflows/review-implementation-feature/workflow.md`）
- 設計 9 章の残件 K-1〜K-5（K-6 は 1.2 で片付けた）を実装のレビューで見る
- 層 2b のテストの 1.x の分岐（`OnCApi` が偽の側）は、1.x のピンを外すときに消す
- サンプルアプリの手動確認（設計 7.4）
