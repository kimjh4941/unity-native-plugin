# Android Clipboard 実装計画 v3（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Clipboard（Android） |
| 種別 | **移行。機能追加ではない。** 1.x の Unity 向けブリッジ（`android.unity.clipboard.UnityAndroidClipboardManager`、`AndroidJavaObject` / `AndroidJavaProxy`、JSON）から、C ABI 2.0.0（`libntk.so`、`Clipboard.h`）の P/Invoke に置き換える |
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（決定 J-1〜J-11、Windows と違うところ A-1〜A-6。移す順は Dialog → Share → **Clipboard** → Notification） |
| 前例 | Android Share の移行（`artifact/android/share/designs/2026-10-11-android-share-design-v7.md`、実装結果 `artifact/android/share/results/2026-10-12-android-share-implementation-feature-result-v2.md`）。届け先を `_instance` にする形（J-S4）、`Initialize()` でリスナーを 1 回だけ登録する形（J-S6）、`AndroidShareJsonBuilder` を残す形（J-S11）を写す。Windows Clipboard の移行（`artifact/windows/clipboard/designs/2026-09-27-windows-clipboard-design-v12.md`）。内部の JSON の parser とそのテストを消した前例 |
| 保つ範囲 | **親課題の J-11「ふつうの使い方を保つ」**。保つのは、公開 API と、UnityMain から 1 回ずつ正しい入力で呼んだときの結果・時点・文言、Editor の結果。同時の呼び出し、Manager を壊す前後、変わった入力は、いちばん単純な形にして 5.3 に書く。「1.x と同じ」と書く行には、タグ `1.12.0` のコードの位置（または今の C# の位置）を付ける |
| 方針 | **公開 API（`Instance`、`Initialize()`、メソッド 10 本、イベント 2 本、`Operation*` 定数 6 個、Payload 4 型、結果の型 7 個〈`ClipboardOperationResult`、`ClipboardReadStatus`、`ClipItem`、`ClipContents`、`ClipboardReadResult`、`ClipDescriptionInfo`、`ClipboardDescriptionResult`〉、`AndroidClipboardJsonBuilder`、引数、既定値、エラーの文言とコード）を保つ。** 変わるのは 5.3「既知の差分」に列挙したものだけ。型の名前は変えない（接頭辞の無い 3 型の改名は、4 機能の移行の後にまとめて行う。`artifact/topics/os-prefix-violations/README.md`） |
| AAR | `android-native-toolkit-2.0.0.aar` と `android-native-toolkit-capi-2.0.0.aar`（dist 1.13.0）。旧ブリッジは移行の間 `extra_*`（dist 1.12.0）に残す |
| 出典 | native-toolkit の `dist/1.13.0/android/include/NativeToolkitC/Clipboard.h`（以下 `Clipboard.h`）、`android/android_library_capi/src/main/cpp/src/Clipboard/Clipboard.cpp`（以下 `C.cpp`）、`.../capi/jni/ClipboardBridge.kt`（以下 `Bridge`）、`android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/clipboard/`（以下 `Lib`）。旧ブリッジはタグ `1.12.0` の `android/unity_android_plugin/src/main/java/android/unity/clipboard/`（以下 `OldBridge` = `UnityAndroidClipboardManager.kt`、`OldParser` = `UnityClipboardJsonParser.kt`）と、1.x のライブラリ `android/android_library/src/main/java/android/library/clipboard/`（以下 `OldLib`）。今の C# は `Runtime/Android/Clipboard/AndroidClipboardManager.cs`（以下 `Mgr`）、`AndroidClipboardJsonBuilder.cs`（以下 `Json`）、`AndroidClipboardJsonParser.cs`（以下 `JParser`）。C ABI の約束は native-toolkit の `artifact/topics/android-c-abi/designs/` の第 1 部（以下 P1）と第 2 部（以下 P2。8.3 の CL-01〜CL-15 が 1.x のブリッジとの対応表）、申し送りは `artifact/topics/android-c-abi/results/2026-10-10-android-c-abi-stage2c-handoff.md`（以下 `Handoff`） |
| 調べた事実 | 設計の前に、事実と出典だけの一覧を作った（作業メモ。リポジトリには置かない）。この設計書の 1 章・2 章はその要約 |
| UI の補助 | **使わない**（J-C17）。Clipboard は OS の画面を出さない |
| レビュー | `artifact/android/clipboard/reviews/2026-10-12-android-clipboard-design-review-v1.md`、`...-review-v2.md`（どちらもサブエージェントと Codex。v2 で両方の A1 が 0） |
| 1.x の記録 | 1.x の設計 `artifact/android/clipboard/designs/2026-07-26-android-clipboard-design-v1.md`〜`-v4.md`、実装結果 `artifact/android/clipboard/results/2026-07-26-android-clipboard-implementation-feature-result-v3.md`。実機の PlayMode `AndroidClipboardManagerIntegrationTests` の 11 本は 1.x で通っている（親課題 README の段 0 の基準の記録）。それ以外の 1.x の実測の記録は無い。移行の前に 7.3 の表を 1.x のまま流して記録する（5.5 の手順 2） |

## v2 からの主な変更（v3。レビュー v2 を受けて。2026-10-12）

- **監視の開始の直後の変更を、J-11 の例外として受け入れた**（R2-1。2026-10-12、利用者の判断）。保たない理由を正しく書き直した（待たずに順を知る手段〈仮のリスナーの `release` を柵にする〉はあるが、native-toolkit の内部の順序に頼り、コピーを後に回す仕組みが要るので採らない）。親課題の README の J-11 に例外として書く（5.1）。差分の書き方を「Android の main が積んだ処理を走らせるまで（時間の保証は無い）」に揃えた（R2-2）。AC-21b は両方の版で記録だけにした（R2-7）
- **公開の `StopObserving()` も、`OnDestroy` と同じ停止の関数を通す**（X2-2）。F-10 を呼び忘れても印が変更を捨てるので、ふつうのテストでは気づけない。テストの側で自分のネイティブのリスナーを足し、Unity の `StopObserving` と Manager の破棄の後に、変更が来ないことを確かめる（AC-34）
- **C ABI の版だけのテスト（AC-32〜AC-34）を `UNITY_ANDROID && !UNITY_EDITOR` の中に置く**（X2-1。Android ターゲットの Editor では `extern` が無い）。AC-33 は `DllImportAttribute` が読めなければ失敗にする（R2-6）
- **層 2a の継ぎ目の記述を直した**（R2-3、R2-4）: 判断の関数を直接呼ぶ。`Initialize` / `StartObserving` が呼ぶこと（配線）はコードレビュー。Reset の範囲、層 2b からは Reset を呼ばないこと、`OnDestroy` の中の例外の扱い
- **記述**（R2-8〜R2-10）: `StartObserving` の C# の例外は `LogError`（1.x と同じ）。同期の読み取りのメモリ不足の差分。非同期の `READ_NOT_ALLOWED` の文言
- **直さない残件の節を足した**（R2-11。9 章）

## v1 からの主な変更（v2。レビュー v1 を受けて。2026-10-12）

- **監視の開始の直後の変更（R1-1、X1-1。A1）**: F-9 は Android の main に積むだけで、コピーは呼び出しの中で終わる。`StartObserving` と同じ流れでコピーすると、その変更が届かないことがある（1.x は両方を同じ main に順に積んだので届いた）。C# は Android の main を待てない（親課題 A-4）ので、保たずに 5.3 と `StartObserving` の XML コメントに書いた。AC-21 を、待ってからの URI のコピー（ちょうど 1 回）、同じフレームのコピー（回数を記録）、テキストのコピー（回数を記録）の 3 つに分けた
- **確保の失敗とメモリ不足（X1-2。A1）**: 1.x は C# の JSON の組み立ての例外を呼び出し側に投げ、Kotlin の `OutOfMemoryError` は捕まえなかった。2.0.0 はどちらも結果の失敗になる。5.3 に足した
- **`texts` の要素（R1-2、X1-3）**: 要素ごとに `AsPassed` を通してから `AllocStringArray` に渡す（5.4）。`ReadFailure` の 6〜8（R1-3）は想定外の値と同じ扱い（4.4）
- **テスト（R1-4〜R1-6、X1-4〜X1-6）**: 作り直した Manager の監視（AC-31）、ほかの利用者の監視が `ClipboardChanged` に出ないこと（AC-32）、`plainText` の null と `Read` のラベル（AC-26、AC-17、AC-18）、`extern` の署名の反射の照合（7.1）、リスナーを 1 回だけ登録することと、`OnDestroy` が F-10 を呼ぶ条件（層 2a。登録と停止を差し替えられる継ぎ目を足した）
- **記述（R1-7〜R1-12、X1-7）**: logcat の行を直した（ラベルは 1.x のライブラリも出していた）。Kotlin の `IllegalArgumentException` も `INVALID_PARAMETER` になること、Kotlin の利用者の停止が Unity にも効くこと、`s_state` の符号化、XML コメントの更新の一覧、`HasClip` の C# の例外のログ（1.x と同じ `LogWarning`）

## 1. native-toolkit 側（2.0.0 の C ABI）

### 1.1 関数

| # | 関数 | 形 | 入口で拒むもの（`C.cpp`） |
|---|---|---|---|
| F-1 | `ntk_clipboard_copy_text(text, options)` | 同期。呼び出しスレッド | `text` が NULL・不正な UTF-8、`options` の検査（`C.cpp:209-210`） |
| F-2 | `ntk_clipboard_copy_html(html, plain_text, options)` | 同期 | `html` が NULL・不正な UTF-8、`plain_text` が不正な UTF-8（NULL は可で `""` になる）（`C.cpp:227-229,240-241`） |
| F-3 | `ntk_clipboard_copy_uri(uri, options)` | 同期 | `uri` が NULL・不正な UTF-8（`C.cpp:250`）。空とスキームは Kotlin が見る |
| F-4 | `ntk_clipboard_copy_texts(texts, count, options)` | 同期 | `count == 0` は最初に `EMPTY_ITEMS`。`texts` が NULL、要素の NULL・不正な UTF-8 は `INVALID_PARAMETER`（`C.cpp:269-274`） |
| F-5 | `ntk_clipboard_clear()` | 同期 | なし（`C.cpp:302-310`） |
| F-6 | `ntk_clipboard_read(out_content)` | 同期 | `out_content` が NULL。空か入力のフォーカスが無いときは `NONE` と `*out_content == NULL`（`Clipboard.h:65`、`C.cpp:332`） |
| F-7 | `ntk_clipboard_has_clip(out_has_clip)` | 同期 | `out_has_clip` が NULL。失敗はエラー値で返す（出力は入口で 0 にしてから書く。`C.cpp:361-376`） |
| F-8 | `ntk_clipboard_get_description(out_description)` | 同期 | 空なら `NONE` と NULL（`Clipboard.h:68`、`C.cpp:399`） |
| F-9 | `ntk_clipboard_start_observing()` | Android の main に積む。`NONE` は「積めた」で、監視が始まったことではない。冪等（`Clipboard.h:71-73`）。システムのリスナーは、積んだ処理が Android の main で走ったときに付く（`ClipboardBridge.kt:161-171`、`Lib/presentation/ClipboardChangeMonitor.kt:37-52`） | 積めなければ `UNKNOWN`（`C.cpp:430-434`） |
| F-10 | `ntk_clipboard_stop_observing()` | 同上 | 同上 |
| F-11 | `ntk_clipboard_add_change_listener(callback, user_data, release, out_listener)` | 登録の受け付け。挿入は Android の main（`C.cpp:447-480`） | `callback` か `out_listener` が NULL |
| F-12 | `ntk_clipboard_listener_remove(listener)` | 外すだけ。監視は止めない（P2 AP-11） | - |
| F-13 | 読み取り関数（内容 8 本、説明 5 本）と `_content_free` / `_description_free` | ハンドルを free するまで有効。値が無い・範囲外は NULL（`C.cpp:143-171`）。**NULL（値なし）と大きさ 0（空文字列）は区別される**。分類は OS が報告しなければ -1（`Clipboard.h:103`） | - |

- どの関数も Android の main を待たない（`Clipboard.h:5-9`）。F-1〜F-8 は呼び出しスレッドで Binder を呼ぶ（P2 AP-2、AP-3）
- 文字列は C → Kotlin で厳密な UTF-8、Kotlin → C で対になっていないサロゲートと U+0000 を U+FFFD に置き換える（`JNI/Utf8.kt:28-38`）
- エラー値の `INVALID_PARAMETER` は、入口の検査のほかに Kotlin の `IllegalArgumentException` からも来る（`JNI/Errors.kt:26-32`）
- 2.0.0 の `Lib` の検査と写しは、`OldLib` とパッケージ名・TAG・コメント以外は同じ（`CopyUriUseCase`、`CopyHtmlTextUseCase`、`CopyMultipleTextUseCase`、`ClipboardMappers`、`ClipboardRepositoryImpl`、`ClipboardDomainError`、`ClipboardChangeMonitor`、`ClipItemData` を `diff` で確かめた）。**空の HTML、URI のスキーム、読み取りの写しの判定は 1.x と同じコードが行う**

### 1.2 型と値

- エラー値 `int32_t`: 0 `NONE`、1 `INVALID_PARAMETER`、2 `NOT_INITIALIZED`、3 `NOT_SUPPORTED`、4 `UNKNOWN`、5 `OUT_OF_MEMORY`、6 `EMPTY_CONTENT`、7 `EMPTY_ITEMS`、8 `INVALID_URI`、9 `UNAVAILABLE`、10 `READ_NOT_ALLOWED`、11 `SECURITY`（`Clipboard.h:20-34`）。6〜11 は Kotlin の `ClipboardErrorCode` の宣言順（`Bridge:23-29,40-52`）
- **C ABI は例外のメッセージを返さない**（`Bridge:59`）
- `ntk_clipboard_copy_options`（`#pragma pack(8)`、24 バイト）: `struct_size` 0、`reserved0` 4、`label` 8、`sensitive` 16、`reserved1` 20（`StructLayoutTest.cpp:18-23`）。`label` の NULL は Kotlin で `""`（`Bridge:70,79,88,97`）
- 変更のコールバック `void (*)(void* user_data)`。Android の main で呼ばれる。要求の ID は無い（`Clipboard.h:43-44`）

### 1.3 監視

- 監視は**プロセスに 1 つ**で、Kotlin API の利用者と C ABI の利用者が共有する。`ntk_clipboard_stop_observing` は Kotlin のリスナーに対しても止める（`Lib/presentation/ClipboardObserver.kt:7-12`、マニュアル 1.13.0 の `clipboard.ja.md:469,507`）
- 登録しただけでは監視しない。外しても監視は続く。start を 2 回でも stop 1 回で止まる（`ClipboardTest.cpp:293,345,356-366`）
- 1 回のコピーで変更が 2 回以上来ることがある（Android 12 以降、テキストの分類の後にもう一度報告する。`ClipboardTest.cpp:269-271`）。1.x も同じ `OnPrimaryClipChangedListener` を使っていた（`OldLib/presentation/ClipboardChangeMonitor.kt:37-52`）ので、1.x でも同じと見込む（**要確認**: 5.5 の手順 2 の AC-21）
- 受け手の無い間の変更は保たれない（`Lib/presentation/ClipboardEvents.kt:10-11`）
- 入口で拒んだ登録の `release` は、呼び出しスレッドで関数が戻る前に来る（`C.cpp:460-467`）。この設計は `release` に NULL を渡す（J-C9）

### 1.4 前面と入力のフォーカス

- 書き込み・消去・問い合わせは前面が要らない。**読み取りと監視は入力のフォーカスが要り**、無ければ読み取りは例外にならず空（`NONE` と NULL）になる（P1 の設計 5.8、`ClipboardTest.cpp:224-240`）
- 1.x の読み取りも同じ `ClipboardRepositoryImpl` を通っていた（`OldLib/data/repository/ClipboardRepositoryImpl.kt:30-39`）ので、フォーカスの無いときの振る舞いは 1.x と変わらない

## 2. 既存の C# 実装（1.x）

### 2.1 公開 API

`Mgr`（`#if UNITY_ANDROID`。`|| UNITY_EDITOR` が無い。`testing.md` の B 群）。

| メンバー | 形 | 出典 |
|---|---|---|
| `Instance`、`Initialize()` | 無ければ GameObject を作る。`Initialize` は `Awake` から | `Mgr:77-89,127-153` |
| `CopyPlainText`、`CopyHtmlText`、`CopyUri`、`CopyMultipleText`（payload、`Action<ClipboardOperationResult>? onResult = null`） | 非同期（共通イベント + per-call） | `Mgr:165-219` |
| `Clear(onResult)`、`StopObserving(onResult)` | 同上 | `Mgr:225-239` |
| `Read()`、`HasClip()`、`GetDescription()` | 同期。イベントを出さない | `Mgr:248-331` |
| `StartObserving()` | 結果なし | `Mgr:343-379` |
| `event ClipboardOperationCompleted`、`event ClipboardChanged` | 共通イベント → per-call の順 | `Mgr:61-71` |
| `Operation*` 定数 6 個 | `copyPlainText`、`copyHtmlText`、`copyUri`、`copyMultipleText`、`clear`、`stopObserving` | `Mgr:26-42` |
| `InvokeInOrder`（internal static） | 共通 → per-call を別々の `try/catch` | `Mgr:487-511` |

### 2.2 per-call の枠と配送

- 操作名ごとの辞書の枠に、呼ぶときに上書き（`Mgr:431`）。結果を出すときに取り出して消し、`UnityMainThreadDispatcher` に積む（`Mgr:470-477`）。**準備の失敗も dispatcher を通るので、結果はいつも呼び出しの後**
- Kotlin の結果は Android の main の proxy から来て、そのスレッドで辞書（ロックなし）を読み書きした（`Mgr:574-580`。`Handoff` 4.2 が指摘した形）
- 変更は proxy から dispatcher に積み、購読者の例外を `LogError` で握った（`Mgr:513-526`）

### 2.3 1.x の文言

| 原因 | 1.x の結果 | 出典 |
|---|---|---|
| Editor・Android 以外 | 非同期 6 種: `LogWarning("<op> can only be called on Android.")`、失敗 `<op> could not be started.`。`Read` / `GetDescription`: `Failed("CLIPBOARD_BRIDGE_UNAVAILABLE", "<op> could not be started.")`。`HasClip`: 2 つの `LogWarning` と `false`。`StartObserving`: `LogWarning` だけ | `Mgr:251-254,282-286,311-314,350-354,395-399,433-437` |
| `pluginInstance` / `currentActivity` が null | `LogError` と、上と同じ `could not be started.` | `Mgr:401-405,414-419` |
| `Call` の例外 | 非同期: `LogError` と失敗 `ex.Message`。`Read` / `GetDescription`: `Failed("CLIPBOARD_BRIDGE_UNAVAILABLE", ex.Message)` と `LogError`。`HasClip`: `LogWarning` と `false` | `Mgr:265-269,295-299,325-329,446-450` |
| Kotlin の例外 | 下の表（`errorInfoOf`）。非同期の結果は message だけ、読み取りはコードと message | `OldBridge:321-336` |

| Kotlin の例外 | コード | message |
|---|---|---|
| `EmptyContent` | `CLIPBOARD_EMPTY_CONTENT` | `Clipboard content is empty. Please provide text or HTML.` |
| `EmptyItemList` | `CLIPBOARD_EMPTY_ITEMS` | `No items provided for clipboard copy.` |
| `InvalidUri` | `CLIPBOARD_INVALID_URI` | `Invalid URI: <入力の uri>`（`OldLib/application/usecase/CopyUriUseCase.kt:25-26` が入力のまま渡す） |
| `ClipboardUnavailable` | `CLIPBOARD_UNAVAILABLE` | `Clipboard service is unavailable.` |
| `ReadNotAllowed` | `CLIPBOARD_READ_NOT_ALLOWED` | `Clipboard read is not allowed. The app must be in the foreground.` |
| `SecurityException` | `CLIPBOARD_SECURITY` | `Security restriction while accessing clipboard: <例外の message>` |
| ほか | `CLIPBOARD_UNKNOWN` | `Failed: <例外の message か型名>` |

- `hasClip` の Kotlin の例外は `"false"`（`OldBridge:205-213`）。`stopObserving` は必ず成功（`OldBridge:270-276`）。`startObserving` は結果を報告しない（`OldBridge:262-265`）

### 2.4 読み取りの正規化（`JParser`）

- 空文字列と欠けたキーをどちらも null にする（`JParser:163`）。配列が無ければ空（`JParser:165`）。`ClipItem` の XML コメントが「空文字列は返さない」と約束している（`ClipboardReadResult.cs:24-28`）
- 分類の 0（キーが無いときの既定値）を null にする（`JParser:62-63,159`）。Android の `CLASSIFICATION_*` は 1〜3

### 2.5 ログ

- 本文は出さない。長さ・件数・有無・`isSensitive`・`hasCallback` だけ（`Mgr:167-172,185-187,199-201,214-216`）。`Read` / `GetDescription` は status と errorCode だけ（`Mgr:262,322`）

### 2.6 テストとサンプル

| ファイル | 件数 | ガード |
|---|---|---|
| `Tests/Runtime/AndroidClipboardJsonBuilderTests.cs` | 16 | なし |
| `Tests/Runtime/AndroidClipboardJsonParserTests.cs` | 39 | なし |
| `Tests/Runtime/AndroidClipboardManagerDispatchTests.cs` | 8 | `UNITY_ANDROID` |
| `Tests/Runtime/AndroidClipboardSampleSceneWiringTests.cs` | 7 | `UNITY_EDITOR` |
| `Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs` | 11 | `UNITY_ANDROID`。Player 以外は `Assert.Ignore` |

- サンプル `Runtime/UI/Android/Clipboard/AndroidClipboardManagerExampleController.cs` は `UNITY_ANDROID || UNITY_EDITOR`、Manager を呼ぶ箇所は `UNITY_ANDROID && !UNITY_EDITOR`。`OnDisable` で `StopObserving()` を呼ぶ。読み取りはボタンの押下の中で同期に呼ぶ。この移行では変えない

## 3. 実装制約

- `agent-rules/coding-rules/common.md` の「Unity Bridge パターン > Android（native-toolkit の C ABI）」: 2 層（Bridge + Manager）、共通部は `Runtime/Android/Common/`、`DllImport` と受け口は `UNITY_ANDROID && !UNITY_EDITOR`、型と純粋な関数は `UNITY_ANDROID || UNITY_EDITOR`、受け口は `AndroidNativeCallbackQueue` に入れるだけ、`UnityMainThreadDispatcher` は使わない、どのスレッドでも待たない
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、`LogTag`、`[{LogTag}][{nameof(Method)}]`、結果として返す失敗は `LogWarning`
- 公開 API を変えない
- 32 ビットと Mono は対象外（親課題 J-10）

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

### 4.1 `DllImport` の署名と構造体

`Runtime/Android/Clipboard/AndroidClipboardCApi.cs`（Bridge）。`[DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]`。

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct CopyOptions { public uint StructSize; public uint Reserved0; public IntPtr Label; public int Sensitive; public uint Reserved1; }   // 24

internal delegate void ChangeCallback(IntPtr userData);

int     ntk_clipboard_copy_text(IntPtr text, ref CopyOptions options);
int     ntk_clipboard_copy_html(IntPtr html, IntPtr plainText, ref CopyOptions options);
int     ntk_clipboard_copy_uri(IntPtr uri, ref CopyOptions options);
int     ntk_clipboard_copy_texts(IntPtr texts, UIntPtr count, ref CopyOptions options);
int     ntk_clipboard_clear();
int     ntk_clipboard_read(out IntPtr content);
int     ntk_clipboard_has_clip(out int hasClip);
int     ntk_clipboard_get_description(out IntPtr description);
int     ntk_clipboard_start_observing();
int     ntk_clipboard_stop_observing();
int     ntk_clipboard_add_change_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);
IntPtr  ntk_clipboard_content_label(IntPtr content, out UIntPtr size);
UIntPtr ntk_clipboard_content_mime_type_count(IntPtr content);
IntPtr  ntk_clipboard_content_mime_type_at(IntPtr content, UIntPtr index, out UIntPtr size);
UIntPtr ntk_clipboard_content_item_count(IntPtr content);
IntPtr  ntk_clipboard_content_item_text_at(IntPtr content, UIntPtr index, out UIntPtr size);
IntPtr  ntk_clipboard_content_item_html_at(IntPtr content, UIntPtr index, out UIntPtr size);
IntPtr  ntk_clipboard_content_item_uri_at(IntPtr content, UIntPtr index, out UIntPtr size);
IntPtr  ntk_clipboard_content_item_coerced_text_at(IntPtr content, UIntPtr index, out UIntPtr size);
void    ntk_clipboard_content_free(IntPtr content);
IntPtr  ntk_clipboard_description_label(IntPtr description, out UIntPtr size);
UIntPtr ntk_clipboard_description_mime_type_count(IntPtr description);
IntPtr  ntk_clipboard_description_mime_type_at(IntPtr description, UIntPtr index, out UIntPtr size);
int     ntk_clipboard_description_is_styled_text(IntPtr description);
int     ntk_clipboard_description_classification_status(IntPtr description);
void    ntk_clipboard_description_free(IntPtr description);
```

- `ntk_clipboard_listener_remove` は宣言しない（登録を外さない。J-C9）
- 受け口は 1 つ（変更）。Manager の private static メソッドに `[MonoPInvokeCallback(typeof(ChangeCallback))]` を付け、delegate は `static readonly` に保ち、関数ポインタは 1 回だけ作る（Share と同じ）
- 文字列は共通部の `AllocUtf8`、文字列の配列は共通部の `AllocStringArray`（Share の移行で共通部に移した）。読み取りの文字列は `ReadUtf8OrNull(ptr, size)`（NULL と空を分ける）

### 4.2 引数の変換（公開 API は変えない）

C# は検査をしない（J-C8）。空・空白・URI のスキーム・件数 0 の判定は、1.x と同じ Kotlin のコードと C の入口が行う（1.1）。C# がするのは null の扱いと、構造体の組み立てだけ。

| 入力 | C# がすること | 1.x（根拠） |
|---|---|---|
| `payload` が null（4 つの copy） | `ArgumentNullException`（J-C7） | `NullReferenceException`（ログの `payload.text` など。`Mgr:170,185,199,214`） |
| `CopyMultipleTextPayload.texts` が null | `ArgumentNullException`（J-C7） | `NullReferenceException`（`Json:88-96` の `values.Length`） |
| 必須の文字列（`text`、`htmlText`、`uri`）と `plainText`、`texts` の要素が null | `""` として渡す（J-C7）。結果は `""` を渡したときと同じ（`text`・要素は成功、`htmlText` は `EMPTY_CONTENT`、`uri` は `INVALID_URI`） | 違う（5.3。JSON の `null` を Kotlin の `getString` / `optString` が文字列 `"null"` として読んだ見込み。`Json:109-111`、`OldParser:15,25-26,36,54`） |
| `label` が null・空・空白 | `Label = NULL`（Kotlin で `""` になる） | 同じ（`Json:80-86` が `IsNullOrWhiteSpace` で省き、`OldParser:16,27,37,48` の `optString` が `""` にした） |
| `label` がそれ以外 | そのまま | 同じ |
| `isSensitive` | `Sensitive = 1 / 0` | 同じ（`OldParser:17,28,38,49`） |
| `texts` が空の配列 | `count = 0` で F-4 を呼ぶ（入口が `EMPTY_ITEMS`） | 同じ文言（`OldBridge:324-325`） |

- `StructSize = 24`、`Reserved0 = Reserved1 = 0`。`options` はいつも渡す（NULL にしない）
- 文字列の途中の U+0000 は `AllocUtf8` の後、C の側でそこで切れる。対になっていないサロゲートは U+FFFD になる（5.3）

### 4.3 結果と時点

- copy 4 種と `Clear`: F-1〜F-5 の戻り値が結果。`NONE` で成功
- `StopObserving`: F-10 の戻り値が `NONE` なら成功（積めた。J-C10）
- `StartObserving`: 結果を出さない（J-C11）
- **非同期 6 種の結果は、どれも取り出し口 `AndroidNativeCallbackQueue` を通して出す。呼び出しの中ではイベントを出さない**（J-C1。1.x と同じ。`Mgr:470-477`）
- `Read` / `GetDescription`: F-6 / F-8。`NONE` で NULL なら `Empty`、ハンドルなら読んで `HasContent`、ほかは `Failed`（4.4）。ハンドルは `finally` で `_free`
- `HasClip`: F-7。`NONE` なら値、ほかは `false`（J-C5）
- 読み取った値の写し（J-C4）: 文字列の `""` と NULL を null にする（`label`、`text`、`htmlText`、`uri`、`coercedText`）。MIME の一覧は写すだけ。分類の -1 と 0 を null にする。`IsStyledText` は 0 以外で真

### 4.4 エラーの文言とログ

`<op>` は操作名（`Operation*` の値と、`read`、`hasClip`、`getDescription`、`startObserving`）。名前と文言への変換は Bridge の純粋な関数にし、層 1 で固める。`<名前>` はエラーの列挙の名前（想定外の値は `ERROR_<値>`）。

| 原因 | 非同期 6 種の `ErrorMessage` | `Read` / `GetDescription` の `ErrorCode` / `ErrorMessage` | ログ |
|---|---|---|---|
| Editor・Android 以外（J-C6） | `<op> could not be started.` | `CLIPBOARD_BRIDGE_UNAVAILABLE` / `<op> could not be started.` | `LogWarning("<op> can only be called on Android.")`（1.x と同じ。`Mgr:395-399`） |
| ネイティブが使えない（共通部の `NativeUnavailable`） | `<op> could not be started.`（1.x の `pluginInstance` が null のときと同じ文言） | 同上 | `LogWarning`（ネイティブが使えないことと理由） |
| `NOT_INITIALIZED` | `<op> could not be started.` | 同上 | `LogWarning("the native library is not initialized yet")` |
| `INVALID_PARAMETER` / `NOT_SUPPORTED` | `Failed: <名前>` | `CLIPBOARD_UNKNOWN` / `Failed: <名前>` | `LogError`（ふつうは C# の組み立ての誤り。`INVALID_PARAMETER` は Kotlin の `IllegalArgumentException` からも来うるが、Clipboard の経路で投げる所は見当たらない） |
| `UNKNOWN` / `OUT_OF_MEMORY` / 想定外の値 | `Failed: <名前>` | `CLIPBOARD_UNKNOWN` / `Failed: <名前>` | `LogWarning` |
| `EMPTY_CONTENT` | `Clipboard content is empty. Please provide text or HTML.` | 起きない。来たら想定外の値と同じ（`CLIPBOARD_UNKNOWN` / `Failed: EMPTY_CONTENT`） | `LogWarning` |
| `EMPTY_ITEMS` | `No items provided for clipboard copy.` | 同上（`Failed: EMPTY_ITEMS`） | `LogWarning` |
| `INVALID_URI` | `Invalid URI: <payload.uri>`（null は `""`） | 同上（`Failed: INVALID_URI`） | `LogWarning`（URI は出さない。J-C15） |
| `UNAVAILABLE` | `Clipboard service is unavailable.` | `CLIPBOARD_UNAVAILABLE` / 同じ文言 | `LogWarning` |
| `READ_NOT_ALLOWED` | 起きない。来たら 1.x と同じ文言 `Clipboard read is not allowed. The app must be in the foreground.`（`OldBridge:330-331`） | `CLIPBOARD_READ_NOT_ALLOWED` / `Clipboard read is not allowed. The app must be in the foreground.` | `LogWarning` |
| `SECURITY` | `Security restriction while accessing clipboard.`（新しい。5.3） | `CLIPBOARD_SECURITY` / 同じ文言 | `LogWarning` |
| C# の例外（確保や `extern` の失敗） | `ex.Message`（1.x の `Call` の例外と同じ扱い。`Mgr:446-450`。確保の例外は 1.x では呼び出し側に出た。5.3） | `CLIPBOARD_BRIDGE_UNAVAILABLE` / `ex.Message`（1.x と同じ。`Mgr:265-269,325-329`） | `LogError`（`HasClip` は 1.x と同じ `LogWarning`。`Mgr:295-299`） |
| 受け口の中の例外 | 変更のイベントは出ない | - | `LogError` |

- 6〜10 の文言とコードは 1.x と同じ（`OldBridge:321-331`）
- `HasClip` は上のどの原因でも `false`。ログは同じ列（C# の例外だけ `LogWarning`）。Editor では 1.x と同じく `can only be called on Android.` と `hasClip could not be started.` の 2 つの `LogWarning`（`Mgr:282-286,395-399`）
- `StartObserving` は上のどの原因でもログだけ。重さは上の表のとおり（ネイティブが使えないときは `LogWarning`。1.x は `pluginInstance` が null などで `LogError` だった。5.3。C# の例外は 1.x と同じ `LogError`。`Mgr:374-377`）

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | 置き場所 | ガード | 内容 |
|---|---|---|---|---|
| 新規作成 | `AndroidClipboardCApi.cs` | `Runtime/Android/Clipboard/` | 型と純粋な関数は `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport` は `UNITY_ANDROID && !UNITY_EDITOR` | Bridge。4.1 の構造体・delegate・`extern`、エラー定数 0〜11、`AsPassed(string?)`（null を `""`）、`BuildOptions(string? label, bool sensitive, List<IntPtr>)`、`ErrorName`、`OperationFailureMessage(op, error, uri)`、`ReadFailure(op, error)`（コードと文言の組）、`IsRequestDefect(error)`（`LogError` にする値）、`NotStartedMessage(op)`、`ToClipContents(label, mimeTypes, items)` と `ToDescriptionInfo(label, mimeTypes, styled, classification)`（4.3 の写し。純粋）、`ReadContent(IntPtr)` と `ReadDescription(IntPtr)`（読み取り関数で値を集め、上の純粋な関数に渡す。`extern` 側） |
| 既存変更 | `AndroidClipboardManager.cs` | `Runtime/Android/Clipboard/` | 型は `UNITY_ANDROID \|\| UNITY_EDITOR`（今は `UNITY_ANDROID`）。受け口・delegate・関数ポインタは `UNITY_ANDROID && !UNITY_EDITOR` | `AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher` / per-call の辞書を消し、`AndroidClipboardCApi` を呼ぶ。5.2 の J-C1〜J-C13。static の状態: 監視の印（`volatile bool`）、リスナーのハンドルと登録専用の `lock`。`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で監視の印を戻す。XML コメント（R1-11）: クラスの要約（今は「`UnityAndroidClipboardManager` を `AndroidJavaObject` で包む」）、`Initialize()` の要約、呼べるスレッド（J-C13）、`Read` に入力のフォーカスが要ること（1.4）、`StartObserving` の監視が後で始まること（5.3）、`StopObserving` の成功の意味（止める処理を積めた）、監視の共有（1.3）。**層 2a の継ぎ目**（`internal`）: `OnChangedCore()`（変更の受け口の本体）、`IsObservingForTests`、`SetObservingForTests(bool)`、`ResetStaticStateForTests()`（`SubsystemRegistration` と同じ中身: 監視の印、差し替えた関数、リスナーの登録済みの印〈ハンドル〉を戻す。Player では `SubsystemRegistration` が最初の登録より前に走る。**層 2b からは呼ばない**〈呼ぶと登録が 2 つになる〉）、`SetNativeForTests(Func<int>? addListener, Func<int>? stopObserving)`（リスナーの登録と F-10 を数える関数に差し替える。null で戻す。Editor でも登録と `OnDestroy` の停止の判断を通すため。J-C9、J-C12）、`ListenerRegisteredForTests`、判断の関数 `EnsureChangeListener()`（未登録なら登録する）と `StopIfObserving()`（印が真なら下ろして F-10 を呼ぶ。`OnDestroy` から呼ぶ）。F-10 は `StopObserving()` と `StopIfObserving()` の両方で、同じ差し替えられる関数を通す。Editor の既定は、登録も F-10 も「何もしない」（`NONE` を返す）。`OnDestroy` の中の例外は握って `LogWarning`（1.x と同じ。`Mgr:545-559`） |
| 既存変更 | `AndroidNativeToolkitCApi.cs` | `Runtime/Android/Common/` | 今のまま | `s_state` を、どのスレッドから読んでも壊れない形（`volatile int`。未確認を 0）にする（J-C16）。振る舞いは変えない |
| 既存変更 | `AndroidClipboardJsonBuilder.cs` | `Runtime/Android/Clipboard/` | 今のまま | 中身は変えない。XML コメントに「2.0.0 では Manager は使わない。4 機能の移行の後に消す（親課題 README の段 4）」と書く（J-C14） |
| 削除 | `AndroidClipboardJsonParser.cs`、`AndroidClipboardJsonParserTests.cs` | `Runtime/Android/Clipboard/`、`Tests/Runtime/` | - | internal で、使う所が無くなる（J-C14） |
| 既存変更 | `AndroidClipboardManagerDispatchTests.cs` | `Tests/Runtime/` | `UNITY_ANDROID \|\| UNITY_EDITOR`（今は `UNITY_ANDROID`） | 中身は変えない（`InvokeInOrder` は残す） |
| 新規作成 | `AndroidClipboardCApiTests.cs` | `Tests/Runtime/` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（7.1） |
| 新規作成 | `AndroidClipboardManagerEditorTests.cs` | `Tests/PlayMode/` | `UNITY_EDITOR` | 層 2a（7.2） |
| 既存変更 | `AndroidClipboardManagerIntegrationTests.cs` | `Tests/PlayMode/` | 今のまま | 層 2b（7.3）。1.x と C ABI の両方で流せるように、違う所は `OnCApi`（`IsObservingForTests` の有無を反射で見る。Share の `AndroidShareTestSupport.OnCApi` と同じ形）で分ける |
| 既存変更 | `common.md`、`testing.md` | `agent-rules/coding-rules/` | - | `common.md`: 「まだ移していない機能」の行から Clipboard を外す。Android の節に Clipboard の形（同期の C を呼んで結果を取り出し口から出す、届け先は `_instance`、監視の印、文字列の配列の null の要素は検査の失敗ではなく `` にすること〈「NULL の要素を含む配列は Manager が先に検査」の Clipboard 版。R1-12〉）を書く。`testing.md`: ガードの表（`AndroidClipboardManager` を B 群から A 群へ）、7 節の Android の Clipboard の層 |
| 既存変更 | `README.md` | `artifact/topics/android-c-abi-2/` | - | 決定 J-9 に Clipboard の届け先（J-C3）。**決定 J-11 に Clipboard の例外（J-C11。監視の開始の直後の変更。2026-10-12、利用者の判断）**。段 3 の状態 |
| 既存変更 | `ClipboardReadResult.cs` | `Runtime/Android/Clipboard/` | 今のまま | `ClipItem` の XML コメントの「The native layer omits JSON keys」を、JSON に寄らない言い方に直す（約束〈空文字列は返さない〉は変えない。R1-11） |
| 非変更 | Payload 4 型（XML コメントを含む。`CopyPlainTextPayload.label` の「Omitted from the JSON」は JSON を前提にした書き方だが、振る舞い〈null・空白はラベル無し〉は同じなので変えない）、`ClipboardReadResult.cs` 以外の結果の型、`AndroidClipboardJsonBuilderTests`、`AndroidClipboardSampleSceneWiringTests`、`AndroidClipboardManagerExampleController`、`Runtime/Resources/UI/Android/Clipboard/*` | - | - | |

### 5.2 判断

| # | 内容 | 決定 |
|---|---|---|
| J-C1 | 非同期 6 種の結果の時点 | **C の関数を呼び出しスレッドで同期に呼び、戻り値から結果を作り、取り出し口に入れる。呼び出しの中ではイベントを出さない**（1.x は準備の失敗も Kotlin の結果も dispatcher を通した。`Mgr:433-437,470-477`）。C ABI は同期なので Android の main を通らない（P2 AP-2）。C# の公開の形は非同期のまま保つ（`common.md` の「Bridge 層で同期・非同期を変換しない」は、公開 API を変えないこの移行では、Share の J-S2 と同じく結果を後で出す形を優先する。P2 8.3 の CL-06〜CL-13 の「結果を listener で返す」） |
| J-C2 | per-call | **呼び出しごとに、その呼び出しの `onResult` を結果の処理に閉じ込める**（辞書の枠を消す）。結果が呼び出しの中で決まるので、Share の J-S5 の「同期に決まる結果」と同じ形になる。順は共通イベント → per-call で、`InvokeInOrder`（今のまま。両方の例外を `LogError` で握る。`Mgr:487-511`）。1.x の「同じ操作は後勝ち」は、同じ操作を結果の前にもう一度呼んだときだけ違う（5.3） |
| J-C3 | 結果と変更を誰に出すか | **取り出した時点で生きている Manager（`_instance`）に出す。いなければ捨てる（ログだけ）**。Share の J-S4 と同じ。親課題の J-9（`Instance` を作って出す）から外れるので README と `common.md` に書く。1.x は `OnDestroy` でブリッジのリスナーを外し、その後の結果と変更を捨てた（`Mgr:109-121`） |
| J-C4 | 読み取りの写し | 4.3 の末尾のとおり。1.x の公開の約束（空文字列を返さない、MIME と項目は null にならない、分類の無いときは null）を保つ（`JParser:62-63,159-165`、`ClipboardReadResult.cs:24-28`）。読み取り関数は呼び出しスレッドで呼び、`finally` で `_free` |
| J-C5 | `HasClip` の失敗 | `false` を返し、4.4 のログを出す（1.x と同じく失敗と空は区別できない。`OldBridge:205-213`、`Mgr:277-278`） |
| J-C6 | Editor・Android 以外 | 1.x と同じ結果（2.3 の表の 1 行目）。null の検査（J-C7）の直後、ほかより前に判定する。ガードに `UNITY_EDITOR` を足したので、ほかのターゲットの Editor でも同じになる（5.3） |
| J-C7 | null | `payload` と `texts` の配列そのものが null なら、何もする前に `ArgumentNullException`（Share の J-S7 と同じ。1.x も呼び出し側に例外が出た。型だけが変わる）。**文字列の null は `""` として渡す**（4.2）。Share は必須の文字列の null を検査の失敗にしたが、Clipboard の C# は検査を持たない（J-C8）ので、`""` に寄せていちばん単純にする。`""` を渡したときの結果（1.x と同じ）になる |
| J-C8 | 検査の置き場所 | **C# は入力を検査しない**。空の HTML（`EMPTY_CONTENT`）、URI の空白とスキーム（`INVALID_URI`）は 1.x と同じ Kotlin のコードが（1.1）、件数 0（`EMPTY_ITEMS`）は C の入口が判定する。C# はエラー値を 1.x の文言に写すだけ（4.4）。Kotlin の検査を C# に写さない（`design-feature` の「ネイティブ側にすでに存在する実装をこのプロジェクトで再実装しない」） |
| J-C9 | 変更のリスナー | **プロセスで 1 回だけ登録し、外さない**（static の受け口、`user_data` と `release` は NULL。Share の J-S6 と同じ）。登録は登録専用の `lock` の中で、ハンドルが無いときだけ行う。`Initialize()`（`Awake` から）と `StartObserving` で呼ぶ。登録の戻り値が `NONE` 以外なら `LogWarning`、例外なら `LogError` で、次の `Initialize()` / `StartObserving` でやり直す。Editor では登録しない。登録の判断（ハンドルがあれば呼ばない、失敗なら次でやり直す）は、層 2a から差し替えた関数で確かめる（`SetNativeForTests`） |
| J-C10 | 監視の印 | C# は static の印 `s_observing`（`volatile bool`）を持つ。**変更の受け口（Android の main）は、印が偽なら捨てる**。印は `StartObserving` が F-9 を呼ぶ前に真にし、F-9 が `NONE` 以外なら偽に戻す。`StopObserving` は F-10 を呼ぶ前に偽にする（F-10 は `StopIfObserving` と同じ差し替えられる関数を通す。X2-2）。1.x の約束「`StartObserving` の前には出ない」（`Mgr:67-71`）を、Kotlin の利用者と共有の監視（1.3）の下でも保つため。`StopObserving` の結果は F-10 が `NONE` なら成功、それ以外は 4.4 の失敗（1.x はいつも成功。`OldBridge:270-276`。5.3） |
| J-C11 | `StartObserving` | 結果を出さない（1.x と同じ。`OldBridge:262-265`、`Mgr:343-379`）。未登録ならリスナーを登録してから F-9 を呼ぶ。失敗はログだけ（4.4）。**監視は F-9 が積んだ処理を Android の main が走らせてから始まる（時間の保証は無い）。それより前のコピーの変更は届かないことがある**（1.x は start と copy を同じ main に順に積んだので、`StartObserving` の後に呼んだコピーの変更は届いた。`OldBridge:262-265,297-301,363-365`）。**親課題の J-11 の例外として受け入れる**（2026-10-12、利用者の判断。R2-1）。待たずに順を知る手段はある（native-toolkit は main の仕事を 1 つの Handler に積んだ順に走らせる〈`MainPoster.kt:9-13`〉ので、F-9 の後に仮のリスナーを足してすぐ外すと、その `release` が監視の開始より後に来る。それを柵にしてその間のコピーを後に回す）。しかしこれはヘッダーの約束ではなく native-toolkit の内部の順序に頼り、コピーを後に回す仕組みも要るので採らない。Android の main の完了を待つこともしない（親課題 A-4）。5.3 と XML コメントに書く（R1-1、X1-1） |
| J-C12 | `OnDestroy` | **自分が `_instance` のときだけ**（1.x と同じ。`Mgr:112`）。印が真なら偽にし、端末（`UNITY_ANDROID && !UNITY_EDITOR`）でネイティブが使えるなら F-10 を呼ぶ（1.x の `clearClipboardChangeListener` が監視を止めた。`OldBridge:105-111`、`Mgr:114`）。**印が偽なら F-10 を呼ばない**（Unity が始めていない監視を、ほかの利用者のために止めない）。リスナーは外さない（J-C9）。`_instance = null`。F-10 を呼ぶかの判断は Editor でも通し、層 2a は差し替えた F-10 の呼ばれた回数で確かめる（Editor の既定の関数は何もしない） |
| J-C13 | 呼ぶスレッド | どのスレッドから呼んでもよい（C ABI がどのスレッドでも呼べる。1.x の同期 3 種の XML コメントは UnityMain を求めていた〈`Mgr:245,275,305`〉。非同期 6 種は別のスレッドで `currentActivity` を読めず失敗した見込み〈Share の 1.x の実測、UI テスト計画 v6 の AS-35〉。5.3。**要確認**: AC-24、AC-25）。ただし `Instance` の最初の取得は GameObject を作るので UnityMain で行う（Share の J-S16 と同じ）。`AndroidNativeCallbackQueue.EnsureCreated()` は `Awake` と各メソッドの null の検査の直後に呼ぶ（UnityMain の外では何もしない）。結果と変更はいつも UnityMain で出る |
| J-C14 | JSON の層 | `AndroidClipboardJsonBuilder`（public）は残し、Manager からは使わない。`[Obsolete]` は付けない（Share の J-S11 と同じ）。`AndroidClipboardJsonParser`（internal）とそのテスト 39 本は消す（Windows の前例。`artifact/windows/clipboard/results/2026-09-29-windows-clipboard-implementation-feature-result-v7.md:79-82`）。写しの約束（J-C4）は層 1 の `ToClipContents` / `ToDescriptionInfo` のテストに移す |
| J-C15 | ログ | 今の C# のログ（長さ・件数・有無・`isSensitive`・`hasCallback`。`Mgr:167-172` ほか）を保つ。読み取った内容・ラベル・URI は出さない（`INVALID_URI` の文言は結果にだけ入れ、ログには出さない）。`Read` / `GetDescription` は status と errorCode だけ。Editor の `Initialize()` は 1.x と同じ `Debug.Log("Not running on Android. Skipping initialization.")`（`Mgr:130-134`） |
| J-C16 | 共通部の状態 | `AndroidNativeToolkitCApi.s_state`（`NativeState?`）を `volatile int` にする（0 を「未確認」、ほかは `(int)state + 1`。`NativeState.Available` が 0 なので、そのまま入れない。R1-10）。Clipboard と Share はどのスレッドからでも呼ばれ、`Nullable<T>` の書き込みは分かれて見えうる（Share の移行からの持ち越し）。最初の確認が 2 つのスレッドで同時に走っても、どちらも同じ答えを書くので害は無い |
| J-C17 | UI テスト計画と UI の補助 | **作らない。** Clipboard は OS の画面を出さず（8.2、8.3）、7.3 のテストは UI の補助なしで流せる。Share のときの UI テスト計画の役（番号と 1.x の列）は、この設計書の 7.3 の表が持つ。1.x の記録は、設計書の次の版の 7.3 の「1.x」の列に書く |

### 5.3 既知の差分（意図したもの）

XML コメントに書き、実装結果ファイル経由で次の版のマニュアル（移行ガイド）へ引き継ぐ。「要確認」の行は 1.x の実測（5.5 の手順 2）で直す。

| 差分 | 1.x | 2.0.0 移行後 | 根拠 |
|---|---|---|---|
| copy・clear が動く時点（J-C1） | Android の main に積んで、後で動いた | 呼んだスレッドで、呼び出しの中で動く。**呼び出しが戻った時点でクリップボードは変わっている**（直後の `Read` が新しい内容を読む。1.x は読めないことがあった見込み。**要確認**: AC-28）。UnityMain から呼ぶと Binder の分だけ呼び出しが長くなる | `OldBridge:297-301`、P2 AP-2 |
| `StartObserving` の直後の変更（J-C11） | start と copy を同じ Android の main に順に積んだので、`StartObserving` の後に呼んだコピーの変更は届いた | 監視は、Android の main が積んだ処理を走らせてから始まる（時間の保証は無い）。それより前のコピー（たとえば `StartObserving` と同じ流れで呼んだもの）の変更は届かないことがある。J-11 の例外（利用者の判断） | `OldBridge:262-265,297-301,363-365`、`ClipboardBridge.kt:161-171` |
| 確保の失敗とメモリ不足（X1-2） | C# の JSON の組み立ての例外は、`try` の外なので呼び出し側に出た。Kotlin の `OutOfMemoryError` は `Exception` でないので捕まえず、Android の main で落ちた見込み | C# の確保の例外は結果の失敗 `ex.Message`（`LogError`）。ネイティブのメモリ不足は `Failed: OUT_OF_MEMORY`。同期の読み取りでは、1.x は C# が `Call` の例外として捕まえ `CLIPBOARD_BRIDGE_UNAVAILABLE` / `ex.Message`（`HasClip` は `false`）だったのが、`CLIPBOARD_UNKNOWN` / `Failed: OUT_OF_MEMORY`（`HasClip` は `false` のまま）になる（R2-9。`Mgr:265-269,295-299,325-329`） | `Mgr:173,188,202,217`、`OldBridge:303-311`、`ClipboardBridge.kt:40-52` |
| `currentActivity` | 要った（null なら `<op> could not be started.`） | 要らない（C ABI は Application の Context を使う） | `Mgr:414-419`、`Bridge:34-37` |
| 別のスレッドから呼ぶ（J-C13） | 非同期 6 種は失敗した見込み。同期 3 種は不明（**要確認**: AC-24、AC-25） | 受け付ける。結果は UnityMain で出る | 同上 |
| 同じ操作を結果の前にもう一度呼ぶ（J-C2） | 後の呼び出しの per-call が先の結果を受けることがあった（枠は後勝ち） | それぞれが自分の per-call を受ける | `Mgr:431,474-475` |
| 文字列の null（J-C7） | 文字列 `"null"` として扱った見込み（`text` が null なら `"null"` をコピー。**要確認**: AC-26） | `""` として扱う | `Json:109-111`、`OldParser:15,26,36,54` |
| `payload` / `texts` が null（J-C7） | `NullReferenceException` | `ArgumentNullException` | `Mgr:170`、`Json:88-96` |
| 文字列の途中の U+0000 | JSON の `\u0000` で運んだ | そこで切れる | `Json` の `AppendEscapedString` |
| 対になっていないサロゲート（書き込み） | JSON でそのまま運んだ | U+FFFD に置き換わる | 共通部の `AllocUtf8` |
| U+0000 と対になっていないサロゲート（読み取り） | 記録が無い | U+FFFD に置き換わる | `JNI/Utf8.kt:28-38` |
| `SECURITY` の文言 | `Security restriction while accessing clipboard: <例外の message>` | `Security restriction while accessing clipboard.`（C ABI は例外の中身を返さない） | `OldBridge:332-333` |
| `UNKNOWN` などの文言 | `Failed: <例外の message か型名>`（Kotlin の `IllegalArgumentException` も同じ） | `Failed: <名前>`（`Failed: UNKNOWN` など。Kotlin の `IllegalArgumentException` は `Failed: INVALID_PARAMETER` と `LogError`） | `OldBridge:334-335`、`JNI/Errors.kt:26-32` |
| 未初期化（`NOT_INITIALIZED`） | その状態が無かった | `<op> could not be started.`（アプリの起動の直後だけ） | 共通部の XML コメント |
| `StopObserving` の結果（J-C10） | いつも成功。Android の main で止めた後に出た | F-10 が積めれば成功。止める前（積んだ時点）に出る。積めなければ失敗 | `OldBridge:270-276` |
| 監視の共有（1.3） | ブリッジ専用のモニターで、ほかの利用者とは別だった | プロセスに 1 つ。Unity の `StopObserving` と Manager の破棄（Unity が始めた監視があるとき）は、同じアプリの中の Kotlin の native-toolkit の利用者の監視も止める。逆に、Kotlin の利用者が始めた監視の変更は、Unity が `StartObserving` を呼ぶまで `ClipboardChanged` に出ない（J-C10）。Kotlin の利用者が `stopObserving()` を呼ぶと、Unity の印が真のままでも `ClipboardChanged` が止まる（R1-9） | `OldBridge:42`、`Lib/presentation/ClipboardObserver.kt:7-12` |
| `StopObserving` の後の変更 | Android の main で止まるまでの変更は届いた | 呼んだ時点で印を下ろすので、その後の受け口では捨てる | J-C10 |
| 結果と変更が Manager を壊す前後に来る（J-C3） | 壊す前に積まれた結果は、壊した Manager のイベントに出た。壊した後は捨てた | 取り出した時点で生きている Manager に出し、いなければ捨てる（作り直していれば新しい Manager に出る） | `Mgr:109-121,476` |
| UnityMain の処理の中から呼んだときの結果のフレーム | Editor と C# 側の即時の失敗は、同じループで出ることがあった | どれも次のフレーム以降（取り出し口は始めにあった件数だけを走らせる。Share の 5.3 と同じ） | `UnityMainThreadDispatcher.cs:45,57-62` |
| `StartObserving` などのログの重さ | `pluginInstance` / `currentActivity` が null で `LogError` | ネイティブが使えないときは `LogWarning` | `Mgr:356-368` |
| logcat の中身 | 1.x のブリッジが失敗の message（URI や例外の中身）を `Log.e` に出した。ラベルは 1.x のライブラリも出していた（`OldLib/data/repository/ClipboardRepositoryImpl.kt:63-74`） | 失敗はクラス名だけ（`Bridge:59`）。ラベルは同じく出る（`Lib/AndroidClipboardManager.kt:41,51,61,71` でも出る） | `OldBridge:194,236,309` |
| Editor（J-C6） | Android ターゲットのときだけ型があった | どのターゲットでも型があり、同じ失敗を返す | `Mgr:3` |
| `Initialize()`（J-C9） | ブリッジを取り、リスナーを差し替えた | リスナーが無ければ登録するだけ。何度呼んでもよい | `Mgr:127-153` |
| 32 ビット（ARMv7）と Mono | 動いた | ビルドが止まる（親課題 J-10） | 親課題 6.4 |
| `AndroidClipboardJsonBuilder`（J-C14） | Manager が使った | 使わない。後の版で消す | `Mgr:173,188,202,217` |

### 5.4 呼び出しの流れと契約

**copy 4 種**（`CopyPlainText` の例）:

1. `payload` が null なら `ArgumentNullException`（J-C7）
2. `AndroidNativeCallbackQueue.EnsureCreated()`（J-C13）
3. 今と同じログ（J-C15）
4. Editor・Android 以外なら `LogWarning` と `Post(<op> could not be started.)` で終わる（J-C6）
5. `EnsureNativeAvailable()` が `Available` でなければ `LogWarning` と `Post(<op> could not be started.)` で終わる
6. `try`: 確保の一覧を作り、`AllocUtf8(AsPassed(payload.text))`、`BuildOptions(payload.label, payload.isSensitive)`、`int code = ntk_clipboard_copy_text(text, ref options)`。`catch`: `LogError` と `Post(ex.Message)`。`finally`: 確保を解放
7. `code` から結果を作り（4.4。`NOT_INITIALIZED` は `could not be started.`、`INVALID_URI` は `payload.uri` を入れる）、ログを出して `Post`

`CopyMultipleText` は 6 で、`texts` の要素ごとに `AsPassed` を通した新しい配列を作ってから `AllocStringArray` に渡す（共通部の `AllocUtf8(null)` は NULL を返し、入口が `INVALID_PARAMETER` で拒むため。R1-2、X1-3）。

`Post(result, onResult)` は `AndroidNativeCallbackQueue.Post(() => Deliver(result, onResult))`。`Deliver`（UnityMain）は `_instance` が null なら捨て（`Debug.Log`）、そうでなければ `InvokeInOrder(result, _instance.ClipboardOperationCompleted, onResult)`。

**`Clear`**: 1 の null の検査が無いほかは同じ（F-5）。**`StopObserving`**: 2、3、`s_observing = false`、4、5、F-10、7。

**`Read` / `GetDescription`**:

1. `EnsureCreated()`、ログ
2. Editor・Android 以外 → `LogWarning` と `Failed(CLIPBOARD_BRIDGE_UNAVAILABLE, "<op> could not be started.")`
3. ネイティブが使えない → 同じ `Failed` と `LogWarning`
4. `try`: `int code = ntk_clipboard_read(out IntPtr handle)`。`NONE` で `handle == IntPtr.Zero` なら `Empty`。`NONE` でハンドルなら `try { ReadContent(handle) } finally { ntk_clipboard_content_free(handle) }` を `FromContents`。ほかは `ReadFailure(op, code)` から `Failed`。`catch`: `LogError` と `Failed(CLIPBOARD_BRIDGE_UNAVAILABLE, ex.Message)`
5. status と errorCode をログに出して返す

**`HasClip`**: 同じ前半（失敗は `false`）。`ntk_clipboard_has_clip(out int value)` が `NONE` なら `value != 0`、ほかは `false` と 4.4 のログ。

**`StartObserving`**: `EnsureCreated()`、ログ、Editor なら `LogWarning` で終わる、ネイティブが使えなければ `LogWarning` で終わる、リスナーの登録（J-C9。失敗ならログを出して終わる）、`s_observing = true`、F-9。`NONE` 以外なら `s_observing = false` と 4.4 のログ。

**変更の受け口**（Android の main。`[MonoPInvokeCallback]`）: `try { OnChangedCore(); } catch (Exception e) { LogError }`。`OnChangedCore`: `if (!s_observing) return; AndroidNativeCallbackQueue.Post(RaiseChanged);`。`RaiseChanged`（UnityMain）: `_instance` が null なら捨てる。そうでなければ `ClipboardChanged` を呼び、例外を `LogError` で握る（1.x と同じ。`Mgr:513-526`）。

- **どのスレッドでも Android の main を待たない**（親課題 A-4）。ロックの中で `extern` を呼ぶのは登録だけで、登録は Android の main を待たない（`C.cpp:447-480`）

### 5.5 実装順

各段階の終わりに `scripts/verify_unity_android.sh` を流す。

1. この設計のレビュー
2. **1.x のまま** 7.3 の新しいテスト（AC-12〜AC-31。AC-32〜AC-34 は C ABI の版だけ）を `AndroidClipboardManagerIntegrationTests` に書いて流し、結果を記録する（7.3 の「1.x」の列。設計書の次の版）。「要確認」の行（AC-21a〜AC-21c、AC-24〜AC-26、AC-28）を実測で埋め、違えば 5.3 を直す
3. 共通部の `s_state`（J-C16）。Dialog と Share のテストが同じに通ること
4. `AndroidClipboardCApi` の構造体と純粋な関数、層 1
5. `AndroidClipboardCApi` の `extern` と読み取り
6. `AndroidClipboardManager` の置き換え（J-C1〜J-C13）と層 2a。`AndroidClipboardJsonParser` とそのテストを消す
7. 実機で 7.3 を流し、1.x の列と比べ、違いが 5.3 の表にあることを確かめる
8. `common.md`、`testing.md`、親課題の README
9. 実装結果ファイル（`artifact/android/clipboard/results/`）に 5.3 と 4.4、7.3 の 2 列の結果を書き写す

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | `payload`・`texts` が null | C# | `ArgumentNullException`（何もしない） | 2a |
| E-2 | 文字列の null | C# | `""` と同じ結果 | 1（`AsPassed`）、2b（AC-26） |
| E-3 | Editor・Android 以外（J-C6） | C# | 2.3 の表の 1 行目 | 2a（10 メソッド） |
| E-4 | ネイティブが使えない | C# | `<op> could not be started.` | 1（文言）。起こせない |
| E-5 | `NOT_INITIALIZED` | ネイティブ（入口） | 同上 | 1（文言）。起こせない |
| E-6 | `INVALID_PARAMETER` / `NOT_SUPPORTED` | ネイティブ（入口） | `Failed: <名前>`、`LogError` | 1（文言と `IsRequestDefect`）。正しい実装では起きない |
| E-7 | `EMPTY_CONTENT`（空・空白の HTML） | ネイティブ（Kotlin） | 1.x の文言 | 1、2b（AC-09） |
| E-8 | `EMPTY_ITEMS`（空の配列） | ネイティブ（入口） | 1.x の文言 | 1、2b（AC-10） |
| E-9 | `INVALID_URI`（空白、`content` / `file` 以外のスキーム） | ネイティブ（Kotlin） | `Invalid URI: <uri>` | 1、2b（AC-11、AC-15） |
| E-10 | `UNAVAILABLE` | ネイティブ（Kotlin） | 1.x の文言 | 1（文言）。起こせない |
| E-11 | `READ_NOT_ALLOWED` | ネイティブ（Kotlin） | 1.x の文言とコード | 1（文言）。起こせない（Android 10 以降のフォーカスの無い読み取りは空になる。1.4） |
| E-12 | `SECURITY` | ネイティブ（Kotlin） | 4.4 | 1（文言）。起こせない |
| E-13 | `UNKNOWN` / `OUT_OF_MEMORY` / 想定外の値 | ネイティブ | `Failed: <名前>` | 1（文言）。起こせない |
| E-14 | `HasClip` の失敗 | ネイティブ | `false` | 1（ログの分類）。起こせない |
| E-15 | `StopObserving` が積めない | ネイティブ | 失敗 `Failed: UNKNOWN` | 1（文言）。起こせない |
| E-16 | `StartObserving` の失敗・リスナーの登録の失敗 | ネイティブ | ログだけ。次でやり直す | コードレビュー。起こせない |
| E-17 | C# の例外（確保、`extern`） | C# | `ex.Message`、`LogError` | コードレビュー。起こせない |
| E-18 | 受け口の中の例外 | C# | 出ない、`LogError` | コードレビュー。起こせない |
| E-19 | 空のクリップボード、フォーカスの無い読み取り | ネイティブ | `Empty`（失敗ではない） | 2b（AC-06、AC-19） |

## 7. テスト方針

### 7.1 層 1（EditMode、`AndroidClipboardCApiTests`）

- `CopyOptions` の大きさ 24 と全フィールドの位置（0 / 4 / 8 / 16 / 20）
- `ChangeCallback` の引数が `IntPtr` 1 つ（反射）
- `extern` の署名（反射。X1-4）: 4.1 の全 `extern` について、名前、戻り値、引数の型（`size_t` は `UIntPtr`、出力のハンドルは `out IntPtr`、`options` は `ref CopyOptions`）、`static` で `internal` であること、`DllImport` のライブラリ名と `Cdecl`（`DllImportAttribute` が反射で読めなければ、黙って通さず失敗にする。IL2CPP で読めるかは要検証。R2-6）。`UNITY_ANDROID && !UNITY_EDITOR` の中なので、層 1 では型が無い。**層 2b で反射して確かめる**（Player の中で、AC-33）
- `BuildOptions`: `StructSize`、予約の 0、`label` の null・`""`・空白・U+3000 が NULL、ふつうのラベルの読み戻し、`Sensitive` の 1 / 0
- `AsPassed`: null が `""`、ほかはそのまま。要素の配列への適用（null の要素が `""`、順が保たれる）
- 文言（4.4）: 0〜11 と想定外の値について、6 つの操作の `OperationFailureMessage` と、`read` / `getDescription` の `ReadFailure`（コードと文言）。`INVALID_URI` の URI の差し込み（null の URI は `""`）。`NOT_INITIALIZED` が `could not be started.`。`IsRequestDefect` は 1 と 3 だけ真
- `ToClipContents`: `""` と null の `label` / `text` / `htmlText` / `uri` / `coercedText` が null、ふつうの値はそのまま、MIME と項目の順、項目 0 個
- `ToDescriptionInfo`: 分類の -1 と 0 が null、1〜3 はそのまま、`IsStyledText`、`label` の `""` が null

`AndroidNativeToolkitCApiTests`（共通部）: J-C16 は振る舞いを変えないので、今のテストのまま。

### 7.2 層 2a（PlayMode・Editor、`AndroidClipboardManagerEditorTests`）

各テストの `SetUp` で `ResetStaticStateForTests()` と `AndroidNativeCallbackQueue.ResetForTests()` を呼ぶ。

- Editor の非同期 6 種（J-C6）: 呼び出しの中ではイベントが出ず、取り出し口が回った後に `ClipboardOperationCompleted` → per-call の順に 1 回、`<op> could not be started.`。`""` の `htmlText` や空の `texts` でも同じ
- Editor の同期 3 種: `Read` / `GetDescription` が `Failed`、`CLIPBOARD_BRIDGE_UNAVAILABLE`、`<op> could not be started.`。`HasClip` が `false`。どれもイベントを出さない
- Editor の `StartObserving`: 例外もイベントも出ず、`IsObservingForTests` が偽のまま
- per-call（J-C2）: 同じ操作を同じフレームに 2 回、別の callback で呼ぶと、それぞれが自分の失敗を 1 回ずつ受ける
- null（E-1）: 4 つの copy の `payload` と `texts` で `ArgumentNullException`。イベントは出ない
- 届け先（J-C3）: Editor で `CopyPlainText` を呼び、取り出し口が回る前に Manager を壊すと、何も出ず、新しい Manager も作られない
- 変更（J-C10）: `OnChangedCore()` を印が偽で呼ぶと何も出ない。`SetObservingForTests(true)` の後は、取り出し口が回った後に `ClipboardChanged` が 1 回。購読者の例外は `LogError` で握られる。Manager を壊した後は何も出ず、Manager も作られない
- `StopObserving` の印（J-C10）: Editor でも、呼んだ後は `IsObservingForTests` が偽
- `OnDestroy`（J-C12）: `SetObservingForTests(true)` の後に 2 つ目の Manager を作る（`Awake` で壊れる）と、印は真のまま。生きている Manager を壊すと偽。**`SetNativeForTests` で F-10 を数える関数に替え、生きている Manager と 2 つ目の Manager、印の真と偽の 4 つの組で、F-10 が呼ばれるのは「生きている Manager で印が真」の 1 回だけ**（X1-6。`OnDestroy` は Editor の判定を通らずに `StopIfObserving` を呼ぶので、Editor でも数えられる）。F-10 の関数が例外を投げても、`OnDestroy` は `LogWarning` で終わり、`_instance` は null になる
- リスナーの登録（J-C9、X1-5）: `SetNativeForTests` で登録を数える関数に替え、判断の関数 `EnsureChangeListener()` を直接呼ぶ（`Initialize` と `StartObserving` は Editor の判定で登録より前に戻るため。R2-3）。2 回呼んで登録は 1 回。登録の関数が失敗を返すと、次の呼び出しでもう一度呼ばれ、成功の後は呼ばれない。**`Initialize` / `StartObserving` が `EnsureChangeListener` を呼ぶこと（配線）と、公開の `StopObserving` が F-10 の関数を呼ぶことは、層 2a では確かめられない**。配線はコードレビュー、二重登録は実機の AC-21a（ちょうど 1 回）、`StopObserving` の F-10 は実機の AC-34 で押さえる

`AndroidClipboardManagerDispatchTests`（今の 8 本）は、ガードを広げて Editor でも走らせる。

### 7.3 層 2b（実機の Player、`AndroidClipboardManagerIntegrationTests`）

**IL2CPP の ARM64 だけ。** 補助は使わない（J-C17）。Player が前面にいて入力のフォーカスがある状態で流す（今の 11 本と同じ。8.2）。各テストの後片付けは今のまま（`StopObserving` と `Clear` の完了を待つ。`AndroidClipboardManagerIntegrationTests.cs:64-83`）。加えて、各テストが受けたイベントの数を照合し、イベントが UnityMain で出たことを確かめる（Share の `AndroidShareEvents` と同じ形）。「1.x」の列は 5.5 の手順 2 で埋める。

| # | テスト | 期待（C ABI の版） | 1.x |
|---|---|---|---|
| AC-01 | `CopyPlainText_OnDevice_Succeeds` | 成功が 1 回 | 通った（今の 11 本） |
| AC-02 | `CopyPlainText_BlankText_Succeeds` | 成功 | 通った |
| AC-03 | `CopyPlainText_ThenRead_ReturnsCopiedText` | 結果の後の `Read` が同じ本文 | 通った |
| AC-04 | `CopyPlainText_ThenHasClip_ReturnsTrue` | `true` | 通った |
| AC-05 | `CopyPlainText_ThenGetDescription_ReturnsHasContent` | `HasContent`、`text/plain` | 通った |
| AC-06 | `Clear_ThenRead_ReturnsEmptyNotFailed` | `Empty` | 通った |
| AC-07 | `StopObserving_ZeroArgumentJniCall_Resolves` | 成功が 1 回（名前は 1.x の経路のままにする） | 通った |
| AC-08 | `StartObserving_DoesNotFireOperationEvent` | 1 秒の間に結果のイベントが無い | 通った |
| AC-09 | `CopyHtmlText_BlankHtml_FailsWithNativeMessage` | `Clipboard content is empty. Please provide text or HTML.` | 通った |
| AC-10 | `CopyMultipleText_EmptyArray_FailsWithNativeMessage` | `No items provided for clipboard copy.` | 通った |
| AC-11 | `CopyUri_BlankUri_FailsWithNativeMessage` | `Invalid URI:` で始まる | 通った |
| AC-12 | HTML を書いて読む | `Text` が `plainText`、`HtmlText` が `htmlText`、MIME に `text/html` | 未 |
| AC-13 | `plainText` が `""` の HTML | `Text` が null（`""` の正規化）、`HtmlText` | 未 |
| AC-14 | `content://` の URI を書いて読む（存在しない authority の固定の値） | `Uri` が同じ、`CoercedText` が同じ URI、`Text` が null | 未 |
| AC-15 | `http://` の URI | 失敗 `Invalid URI: http://example.com`（全文） | 未 |
| AC-16 | `["a", "", "c"]` を書いて読む | 項目が 3 つ、`Text` が `a`、null、`c` | 未 |
| AC-17 | ラベル付き（`isSensitive` も真）を書いて `GetDescription` と `Read` | どちらも `Label` が同じ | 未 |
| AC-18 | 空白のラベル | `GetDescription` と `Read` の `Label` が null | 未 |
| AC-19 | `Clear` の後の `HasClip` と `GetDescription` | `false`、`Empty` | 未 |
| AC-20 | 結果の時点と順 | 呼び出しの中では 0 回。後で共通イベント → per-call の順に 1 回ずつ、UnityMain | 未 |
| AC-21a | `Initialize()` を 2 回呼び、`StartObserving` の後 0.5 秒待ってから、`content://` の URI を 1 回コピー | 3 秒のうちに `ClipboardChanged` が**ちょうど 1 回**（URI は分類されないので 1 回。`ClipboardTest.cpp:311-312`。リスナーの二重登録なら 2 回になる） | 未（要確認） |
| AC-21b | `StartObserving` と同じフレームで `content://` の URI をコピー | 回数を記録する（0 か 1。両方の版で、値では落とさない。5.3） | 未（要確認。1 回の見込み） |
| AC-21c | `StartObserving` の後 0.5 秒待ってから、テキストを 1 回コピー | 1 回以上。数を記録する（Android 12 以降の分類の後の 2 回目。1.3） | 未（要確認） |
| AC-22 | `StopObserving` の結果の後にコピー | 3 秒の間 `ClipboardChanged` が無い | 未 |
| AC-23 | `StartObserving` を呼ばずにコピー | 3 秒の間 `ClipboardChanged` が無い | 未 |
| AC-24 | 別のスレッドから `CopyPlainText` | 例外が出ず、成功が UnityMain で 1 回 | 未（要確認。失敗の見込み） |
| AC-25 | 別のスレッドから `Read` | 例外が出ず、コピーした本文 | 未（要確認） |
| AC-26 | 文字列の null（`text`、`plainText`、`htmlText`、`uri`、`texts` の要素） | `text`: 成功し、`Read` の `Text` が null。`plainText`: 成功し、`Text` が null、`HtmlText` が渡した値。`htmlText`: `EMPTY_CONTENT` の文言。`uri`: `Invalid URI: `。要素: 成功し、その項目の `Text` が null | 未（要確認。`"null"` の見込み） |
| AC-27 | `payload` が null | `ArgumentNullException` | 未（`NullReferenceException` の見込み） |
| AC-28 | コピーの直後（結果を待たず、同じ呼び出しの流れ）の `Read` | 新しい本文 | 未（要確認） |
| AC-29 | 分類 | 本文をコピーして 1 秒後の `GetDescription` の `ClassificationStatus` を記録する（null か 1〜3） | 未 |
| AC-30 | 日本語と絵文字の本文 | `Read` が同じ文字列 | 未 |
| AC-31 | 作り直した Manager の監視（R1-4） | `StartObserving` → Manager を `Destroy` → `Instance` で作り直す → `StartObserving` → 0.5 秒後に URI のコピー。新しい Manager に `ClipboardChanged` がちょうど 1 回 | 未（1 回の見込み） |
| AC-32 | ほかの利用者の監視（C ABI の版だけ。R1-5） | `ntk_clipboard_start_observing()` を直接呼び（Unity の `StartObserving` は呼ばない）、0.5 秒後に URI のコピー。3 秒の間 `ClipboardChanged` が無い。`finally` で `ntk_clipboard_stop_observing()` | 対象外 |
| AC-33 | `extern` の署名（C ABI の版だけ。X1-4） | 7.1 の反射の照合 | 対象外 |
| AC-34 | ネイティブの監視が本当に止まること（C ABI の版だけ。X2-2） | テストが自分のネイティブのリスナー（`[MonoPInvokeCallback]` の static の受け口で数える）を `ntk_clipboard_add_change_listener` で足す。(1) Unity の `StartObserving` → 0.5 秒後に URI のコピー → テストのリスナーが 1 回。(2) Unity の `StopObserving` の結果を待ち、0.5 秒後に URI のコピー → 3 秒の間テストのリスナーが 0 回。(3) `StartObserving` → Manager を `Destroy` → 1 フレーム後に URI のコピー → 0 回。`finally` でリスナーを外し、作り直した Manager に購読し直す | 対象外 |

- 1.x と 2.0.0 で期待が違う行（AC-24〜AC-28）は、`OnCApi` で分けて両方の期待を書く（Share の 1.x の分岐と同じ形）
- C ABI の版だけのテスト（AC-32〜AC-34）と、そのための `extern` を呼ぶ補助は、テストファイルの中で `UNITY_ANDROID && !UNITY_EDITOR` で囲む（Android ターゲットの Editor では `extern` が無い。X2-1）
- 層 2b からは `ResetStaticStateForTests()` を呼ばない（R2-4）
- 監視のテスト（AC-21a〜AC-23、AC-31、AC-32、AC-34）は、テストの前後で `StopObserving` の完了を待つ（今の後片付け）。変更が遅れて届くことを考え、待つ時間は 3 秒
- AC-21b、AC-21c、AC-29 は値を記録するだけで、値で落とさない（端末と OS の版で変わる。AC-21b は競合）

### 7.4 層 3・手動

- 層 3: クリップボードの中身は Player の中の `Read` で確かめられる（`testing.md` の「Android では層 3 を狭くできる」）。ほかのアプリに貼ったときの見え方と、`isSensitive` のプレビューの抑制は、この移行で変わらない（同じ Kotlin のコード）ので確かめない
- 手動: サンプルアプリで、copy 9 種・Paste Code・Read・HasClip・GetDescription・Clear・監視の開始と停止・エラー 4 種のボタンを一通り押し、結果の表示が 1.x と同じこと（実装の後、利用者の確認で）

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。起こせない失敗（E-4〜E-6、E-10〜E-18）は、文言の変換を層 1 で、形をコードレビューで押さえる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- OS が求める許可: なし（Clipboard に固有の権限は無い）
- OS の画面: Android 12 以降、ほかのアプリが書いた内容を読むと「貼り付けました」のトーストが出る。テストは自分で書いた内容を読むので出ない見込み。出ても操作を止めない
- 前提: **Player が前面にいて入力のフォーカスがあること**（1.4。無いと `Read` が `Empty` になり、監視の変更が届かない）。画面が点いていて、ロックが解けていること。今の 11 本はこの条件で通っている（親課題 README の段 0 の基準の記録）。`verify_unity_android.sh` が画面を点けるかを、5.5 の手順 2 で確かめる（点けないなら、Dialog の補助と同じ点け方を足す）
- テストは端末のクリップボードを書き換える（今の 11 本と同じ）。既定の IME は変えない（Appium は使わない）

### 8.3 呼び出し側を止める OS の画面

なし。Clipboard の関数はどれも OS の画面を出さない。

### 8.4 アプリ側の設定（利用者の責任）

- なし。`content://` の URI をほかのアプリに渡すときの FileProvider と権限の付与は、1.x と同じく利用者の責任（サンプルは同梱の FileProvider を使う）

## 9. 直さない残件

止める基準の 3（`agent-rules/workflows/review-document/workflow.md`）。実装のときに確かめ、実装結果ファイルのチェックリストに写す。

| # | 区分 | 中身 | 直さない理由 |
|---|---|---|---|
| K-1 | A2 | AC-31・AC-34 の手順の細部: `Destroy` はフレームの終わりまで遅れるので、作り直す前に 1 フレーム待つ。今の後片付けは最初の `Instance` に購読しているので、作り直した Manager に購読し直す（`AndroidClipboardManagerIntegrationTests.cs:53,69-80`）（R2-5） | 書いたとおりに作らないとテストが落ちるので、実装で気づける |
| K-2 | A2 | 4.4 の文言を、6 つの操作と 0〜11 の全値の表として層 1 で固める（R1-3、R2-10） | 層 1 のテストを書く時点で決まる |
| K-3 | B | `extern` の宣言をヘッダーの文面と Editor で照らす（native-toolkit の `check_c_abi_contract_android.py` の形）（R2-6 の案） | AC-33 の反射と、層 2b の実機の呼び出しで足りる。Share も採っていない |
| K-4 | B | `Initialize` / `StartObserving` が `EnsureChangeListener` を呼ぶ配線（R2-3） | 層 2a では Editor の判定で通らない。コードレビューで押さえ、二重登録は AC-21a、登録の抜けは AC-21a と AC-31 が落ちる |
| K-5 | 要検証 | Unity as a Library で `Application.Unload()` の後も、外さないリスナーに、ほかの Kotlin の利用者の監視の変更が来うる（レビュー v1 の不足項目） | Share の J-S6 と同じ前提。受け口は印が偽なら捨てる。Unity as a Library の対応はこのリポジトリの範囲の外 |
| K-6 | 要検証 | AC-33 の `DllImportAttribute` が IL2CPP で反射で読めるか（R2-6） | 読めなければ失敗にするので、実機で分かる。読めなければ、その項目だけを外して理由を書く |
