# Android Share 実装計画 v1（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Share（Android） |
| 種別 | **移行。機能追加ではない。** 1.x の Unity 向けブリッジ（`android.unity.share.UnityAndroidShareManager`、`AndroidJavaObject` / `AndroidJavaProxy`、JSON）から、C ABI 2.0.0（`libntk.so`、`Share.h`）の P/Invoke に置き換える |
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（4 機能の目次。決定 J-1〜J-10、Windows と違うところ A-1〜A-6。移す順は Dialog → **Share** → Clipboard → Notification） |
| 前例 | Android Dialog の移行（`artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`、実装結果 `artifact/android/dialog/results/2026-10-11-android-dialog-implementation-feature-result-v4.md`）。共通部（`AndroidNativeToolkitCApi`、`AndroidNativeCallbackQueue`）と、UI の補助、後片付けの形（`Safely`）はここで作った |
| 方針 | **公開 API（`Instance`、`Initialize()`、メソッド 9 本、イベント 3 本、`Operation*` 定数 9 個、Payload 8 型、結果 3 型、`ShareChooserActionCallbackCoordinator`、引数、既定値、エラーの文言）を保つ。** 変わるのは 5.3「既知の差分」に列挙したものだけ |
| AAR | `android-native-toolkit-2.0.0.aar` と `android-native-toolkit-capi-2.0.0.aar`（dist 1.13.0）。旧ブリッジは移行の間 `extra_*`（dist 1.12.0）に残す |
| 出典 | native-toolkit の `dist/1.13.0/android/include/NativeToolkitC/Share.h`（以下 `Share.h`）、`android/android_library_capi/src/main/cpp/src/Share/Share.cpp`（以下 `S.cpp`）、`.../capi/jni/ShareBridge.kt`（以下 `Bridge`）、`android/android_library/.../share/`（以下 `Lib`。`data/repository/ShareRepositoryImpl.kt` は `Repo`）。旧ブリッジは native-toolkit のタグ `1.12.0` の `android/unity_android_plugin/src/main/java/android/unity/share/`（以下 `OldBridge`。`UnityAndroidShareManager.kt` と `UnityShareJsonParser.kt`）。C ABI の約束は native-toolkit の `artifact/topics/android-c-abi/designs/` の第 1 部（以下 P1）と第 2 部（以下 P2。8.3 の SH-01〜SH-14 が 1.x のブリッジとの対応表）、Unity 向けの申し送りは `artifact/topics/android-c-abi/results/2026-10-10-android-c-abi-stage2c-handoff.md`（以下 `Handoff`）の 4.2〜4.4 |
| UI の補助 | `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v5.md`。Share には足す命令がある（8.3） |
| UI テスト計画 | `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v1.md`（この設計のレビューの後に作る。7.3 の中身に番号を振る） |
| 1.x の記録 | 1.x の設計 `artifact/android/share/designs/2026-06-20-android-share-design-v1.md`〜`v3.md`。**1.x の実機の UI テストの記録は無い**（`artifact/topics/device-verification-records/README.md`）。Dialog と同じく、移行の前に 1.x のまま流して記録する（5.5 の手順 3） |

## 1. native-toolkit 側（2.0.0 の C ABI）

### 1.1 関数

| # | 関数 | 同期 / 非同期 | 用途（1.x のブリッジ） |
|---|---|---|---|
| F-1 | `ntk_share_text(content, actions, action_count, callback, user_data, release)` | 非同期（完了 1 回） | `shareText`（SH-06） |
| F-2 | `ntk_share_image(path, mime_type, callback, user_data, release)` | 非同期 | `shareImage`（SH-07） |
| F-3 | `ntk_share_images(paths, count, callback, user_data, release)` | 非同期 | `shareImages`（SH-08） |
| F-4 | `ntk_share_file(path, callback, user_data, release)` | 非同期 | `shareFile`（SH-09） |
| F-5 | `ntk_share_files(paths, count, callback, user_data, release)` | 非同期 | `shareFiles`（SH-10） |
| F-6 | `ntk_share_register_direct_target(target)` | 同期 | `registerDirectShareTarget`（SH-11） |
| F-7 | `ntk_share_remove_direct_targets(ids, count)` | 同期 | `removeDirectShareTargets`（SH-12） |
| F-8 | `ntk_share_text_for_selection(content, callback, user_data, release, out_request_id)` | 非同期。選ばれたアプリは F-11 のイベント | `shareWithCallback`（SH-13） |
| F-9 | `ntk_share_cancel_selection(request_id)` | 同期（取り消しを main に積むだけ。結果の通知は無い） | `cancelPendingShareCallback`（SH-14） |
| F-10 | `ntk_share_add_chooser_action_listener(callback, user_data, release, out_listener)` | 登録 | `setShareChooserActionListener`（SH-04） |
| F-11 | `ntk_share_add_selection_listener(callback, user_data, release, out_listener)` | 登録 | `setShareOperationListener` の `onShareResult` の部分（SH-02） |
| F-12 | `ntk_share_listener_remove(listener)` | 解除 | `clear*Listener`（SH-03、SH-05） |

共通部（`Common.h`）からは `ntk_string_data` / `ntk_string_size` / `ntk_string_free`（選択とアクションのイベントの文字列）を使う。C# は `AndroidNativeToolkitCApi` にもう宣言がある。

### 1.2 型と値

コールバックの型（`NTK_CALL` は Android では空。C# は `CallingConvention.Cdecl`）:

- `ntk_share_done_fn(void* user_data, ntk_share_error error, uint32_t system_code)`。`system_code` は常に 0（P2:123）
- `ntk_share_chooser_action_fn(void* user_data, ntk_string* action_id)`。`action_id` は所有権ごと渡る
- `ntk_share_selection_fn(void* user_data, uint64_t request_id, ntk_string* package_name)`。`package_name` は分からなければ NULL。所有権ごと渡る
- `ntk_release_fn(void* user_data)`

構造体（`#pragma pack(push, 8)`。大きさと位置は native-toolkit の `android_library_capi_test` の `StructLayoutTest.cpp:118-141`。arm64-v8a と x86_64 で同じ）:

| 構造体 | 大きさ | フィールド（位置） |
|---|---|---|
| `ntk_share_text_content` | 56 | `struct_size` 0、`reserved0` 4、`text` 8、`title` 16、`subject` 24、`mime_type` 32、`preview_title` 40、`preview_thumbnail_path` 48 |
| `ntk_share_chooser_action`（`struct_size` なし。組は固定） | 32 | `id` 0、`label` 8、`icon` 16、`icon_size`（`size_t`）24 |
| `ntk_share_direct_target` | 48 | `struct_size` 0、`reserved0` 4、`id` 8、`label` 16、`category` 24、`icon` 32、`icon_size`（`size_t`）40 |

`ntk_share_error`（P2:695-718）: `NONE` 0、`INVALID_PARAMETER` 1、`NOT_INITIALIZED` 2、`NOT_SUPPORTED` 3、`UNKNOWN` 4、`OUT_OF_MEMORY` 5、`NOT_FOREGROUND` 6、`EMPTY_CONTENT` 7、`NO_SHARE_TARGET` 8、`FILE_NOT_FOUND` 9、`ILLEGAL_FILE_ACCESS` 10、`INVALID_MIME_TYPE` 11、`DIRECT_SHARE_REGISTRATION_FAILED` 12、`EMPTY_ID_LIST` 13、`EMPTY_FILE_LIST` 14、`INVALID_ICON` 15、`INVALID_CHOOSER_ACTION` 16。`CANCELED` は無い（開く操作は Sharesheet が開いた時点で完了する）。

### 1.3 入口で拒まれるもの（同期）と、完了で来るもの（非同期）

原則（P2:43 の AP-19、P2:613）: **C の入口だけで決まる検査は、非同期の操作でも戻り値で返す。** 入口で拒んだときは完了を呼ばず、`release` を呼び出しスレッドで関数が戻る前に 1 回呼ぶ（P1:149）。受け付けた後の失敗は、すべて完了の引数で来る（P1:150）。

| 原因 | 時点 | 値 |
|---|---|---|
| 構造体・配列・必須の文字列が NULL、厳密でない UTF-8、`struct_size` が足りない・4096 を超える、`reserved0` が 0 でない、`icon_size` が `INT32_MAX` を超える | 入口 | `INVALID_PARAMETER` |
| `struct_size` の知らない部分に 0 でない値 | 入口 | `NOT_SUPPORTED` |
| 本文が `""` | 入口 | `EMPTY_CONTENT` |
| 件数 0（images / files） | 入口 | `EMPTY_FILE_LIST` |
| 件数 0（remove） | 入口 | `EMPTY_ID_LIST` |
| Chooser Action の ID が NULL・空・重複、アイコンが NULL・大きさ 0 | 入口 | `INVALID_CHOOSER_ACTION` |
| Direct Share の `id` / `label` が NULL | 入口 | `INVALID_PARAMETER` |
| Direct Share のアイコンが NULL・大きさ 0 | 入口 | `INVALID_ICON` |
| 初期化の前（入口の検査はすべてこれより先。P2:613） | 入口 | `NOT_INITIALIZED` |
| main に積めなかった（`Accept.h:21-38`） | 入口 | `UNKNOWN`（JNI の失敗なら写した値） |
| 本文が空白だけ（Kotlin の `isBlank`） | 完了 | `EMPTY_CONTENT` |
| text / image の MIME が空白 | 完了 | `INVALID_MIME_TYPE`。**`ntk_share_text_for_selection` は MIME を確かめない**（`ShareForSelectionUseCase.kt`）。空白の MIME で何が起きるかは未確認 |
| ファイルが無い（`File.exists` が偽） | 完了 | `FILE_NOT_FOUND` |
| FileProvider の範囲の外（`getUriForFile` の `IllegalArgumentException`） | 完了 | `ILLEGAL_FILE_ACCESS` |
| Chooser Action のアイコンが画像として読めない | 完了 | `INVALID_CHOOSER_ACTION`（Share 全体が失敗する） |
| 前面でない（main の上の `ForegroundActivityTracker.current() == null`） | 完了 | `NOT_FOREGROUND` |
| `startActivity` の `ActivityNotFoundException` | 完了 | `NO_SHARE_TARGET`（端末では起こせない。2b の結果:156） |
| 写しの表に無い例外、JNI の失敗 | 完了 | `UNKNOWN`（`IllegalArgumentException` は `INVALID_PARAMETER`、`OutOfMemoryError` は `OUT_OF_MEMORY`） |
| Direct Share（F-6、F-7）のアイコンが読めない | 同期 | `INVALID_ICON` |
| Direct Share の `pushDynamicShortcut` が false、または `RuntimeException`（`IllegalArgumentException` を含む） | 同期 | `DIRECT_SHARE_REGISTRATION_FAILED`（`Repo:151-157`。理由の文字列は C ABI から来ない） |

`ntk_share_cancel_selection` は初期化の前でも `NONE` を返す（P2:619）。

### 1.4 スレッド、寿命、`user_data`

- 関数はどのスレッドからでも呼べ、main を待たない（P1:144-146）
- **完了、イベント、受け付けた後の `release` は Android の main（UI スレッド）で来る。UnityMain ではない**（P1:147、`Handoff`:110）
- **UnityMain から呼ぶと、完了は関数が戻る前に来うる**（P1:148）。`out_request_id` が書かれる前に完了が来うる
- 開く操作（F-1〜F-5、F-8）は main に積まれ、前面の判定を受ける（P2:27 の AP-3）。Direct Share（F-6、F-7）は呼び出しスレッドで同期に動き、前面は要らない（P2:26 の AP-2）
- 完了つきの操作は、受け付けたら**ちょうど 1 回**完了し、その直後に同じ main のメッセージの中で `release` が来る（P1:151、P1:474）。プロセスが終わるときは、完了も `release` も来ない（P1:154）
- `ntk_share_done_fn` は要求の ID を持たない。**対応付けは `user_data` で行う**（P2:288、`Handoff` 4.3）
- イベントの登録（F-10、F-11）: `release` は `ntk_share_listener_remove` の後にだけ来る。登録の前に起きたイベントは失われる（Share のイベントは保持しない。`ShareEvents.kt:12,15`）
- **コールバックが受け取った `ntk_string*` は所有権ごと渡される。** 受け手が `ntk_string_free` で解放する（P1:292 の AC-18）
- 利用者のコールバックから例外が C へ抜けると、プロセスが終わる（P1:539）

### 1.5 選ばれたアプリ（selection）

- F-8 は受け付けの時点で 64 ビットの要求の ID（再利用しない）を `out_request_id` に書く（受け付けた後にだけ書く。先に 0 を書く。`S.cpp:453`）
- 選ばれたアプリは F-11 のイベントで、その要求の ID と一緒に来る。**届くのは直前に開いた選択の待ちの分だけ**（新しい F-8 は前の待ちを消す。P1:296 の AC-22、P2:41 の AP-17）
- 来ないとき: 閉じた、API 35 以降の Copy・Edit など選択でない結果、取り消した、後の F-8 が待ちを置き換えた、`NOT_FOREGROUND` で開かなかった、プロセスが作り直された
- `package_name` が NULL になるとき: API 34 以下で `EXTRA_CHOSEN_COMPONENT` が無い、API 35 以上で選ばれたのに `selectedComponent` が null（`ShareCallbackCoordinator.kt:27-56`）
- 取り消し（F-9）は、今の待ちの ID のときだけ効く。必ず main に積まれ、開く処理より後に動く。知らない ID では何もしない。結果の通知は無い（P2:469 の SH-14）
- 前の待ちの扱い: 開く前の失敗（`NOT_FOREGROUND`、空白の本文）では前の待ちが残る。開いた後の失敗（`NO_SHARE_TARGET`、`UNKNOWN`）では前の待ちの選択は届かない（P2:41）
- **古い Chooser の選択は届かない**（1b で直した 1.x の不具合。SH-13）
- `Handoff` 4.3: 要求の ID は manager の表（UnityMain）に置き、選択のイベントは UnityMain に積んだ後で「直前の要求の ID」と照らす

### 1.6 Chooser Action

- **API 34 以上でだけ表示される。** API 33 以下では付けない（`Repo:59`。端末では確かめていない。試験の端末は API 35 と 36 だけ）
- `ntk_share_text`（F-1）は、アクションが 0 個でも世代を上げ、前の Share のアクションを止める（P2:31 の AP-7）。F-8 と画像・ファイルの Share は世代を変えない（直前の F-1 のアクションは押せるまま）
- `label` の NULL は `""` になる。アイコンは `BitmapFactory` で読める形式（PNG、JPEG、WebP）のバイト列。大きさと個数の上限はライブラリには無い（Android の Sharesheet は 5 個までを表示すると 1.x の設計 v3 が書いている。要検証）
- `android.intent.action.SEND` の ID もふつうの ID として扱う（P2:461）
- アクションが押されると F-10 のイベントに `action_id` が来る。世代が今のものでなければ捨てる（`ShareChooserActionReceiver.kt:22-34`）。世代の照合と配送の間に新しい Share が入ると、古いアクションが 1 度だけ届くことがある（1b の結果:247）

### 1.7 Direct Share

- 同期。前面は要らない。`category` の NULL は `"android.shortcut.conversation"` になる（`S.cpp:515`）
- `ShortcutManagerCompat.pushDynamicShortcut`（`setLongLived(true)`）で登録する。同じ ID の再登録は更新（1.x の設計 v3:588-596。C ABI の版での端末の確かめは無い）
- 削除は `removeLongLivedShortcuts`。知らない ID の扱いは OS に任せる（確かめていない）
- **Sharesheet に出すには、アプリ側が `<share-target>` を宣言する必要がある。AAR は宣言していない**（1.x と同じ）

### 1.8 ファイルと FileProvider

- `android-native-toolkit-2.0.0.aar` の manifest が `androidx.core.content.FileProvider` を authority `${applicationId}.native_toolkit.share.fileprovider` で宣言し、`res/xml/native_toolkit_share_file_paths.xml` を持つ。利用者の設定は要らない（manifest のマージで入る。1.x と同じ）
- 許される場所は `files-path "."`、`cache-path "."`、`external-files-path "."` だけ。**`external-cache-path` は含まれない**（Unity の `Application.temporaryCachePath` は外部のキャッシュにあるので範囲外になりうる。要検証。サンプルは `persistentDataPath` を使う）
- キャッシュへの写しはしない。content URI をそのまま渡す
- MIME: `ntk_share_image` は引数の値（NULL は `"image/*"`）。file / files / images は拡張子の表から決める（無ければ `*/*`）。複数で型が混ざると、images は `image/*`、files は `*/*`
- プレビューのサムネイル（`preview_thumbnail_path`）が無い・範囲外のときは、エラーにせず黙って省く（`Repo:226-235`。1.x と同じ）

### 1.9 Sharesheet の画面

- API 34 以上の Sharesheet のパッケージは `com.android.intentresolver`（native-toolkit の `UiDriver.kt:30`）。native-toolkit の UI テストは resource-id を使わず、パッケージと文言で探す（共有先の名前、Chooser Action の `label`、Direct Share の short label）
- 見つからなければ、画面の中央で上へスワイプする（1 回目はシートを広げ、2 回目からは一覧をスクロールする。8 回まで）。試験の共有先は一覧の下の方に出る（Pixel で 16 番目）
- **Sharesheet は前面を奪わない**（半透明のシートで、下のアプリの Activity は started のまま）。続けて 2 つ開くと、Sharesheet は 1 つしか出ない
- native-toolkit の UI テストの決まり: **失敗したときも、Sharesheet の画面の構造を書き出さない**（ほかのアプリの共有先・連絡先が出る。native-toolkit の `designs/2026-10-03-android-c-abi-ui-test-design.md:33`）

## 2. 既存の C# 実装（1.x）

`Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Share/`。名前空間は `JonghyunKim.NativeToolkit.Runtime.Android.Share`（移動のコミット `44f206d` で済み）。`AndroidShareManager.cs` だけが `#if UNITY_ANDROID`、ほかはガード無し。

### 2.1 公開 API

| 種類 | 名前 | 内容 |
|---|---|---|
| 定数 | `OperationShareText` ほか 9 個 | `"shareText"`、`"shareImage"`、`"shareImages"`、`"shareFile"`、`"shareFiles"`、`"registerDirectShareTarget"`、`"removeDirectShareTargets"`、`"shareWithCallback"`、`"cancelPendingShareCallback"`。`ShareOperationResult.Operation` の値と、エラーの文言の `<op>` を兼ねる |
| イベント | `ShareOperationCompleted`（`Action<ShareOperationResult>`） | すべての操作の成功と失敗。`shareWithCallback` の起動を含む。per-call より先 |
| イベント | `ShareCallbackReceived`（`Action<ShareCallbackResult>`） | `ShareWithCallback` でアプリが選ばれたときだけ |
| イベント | `ShareChooserActionTapped`（`Action<ShareChooserActionResult>`） | `ShareText` の Chooser Action が押されたとき（API 34 以上）。`add` / `remove` は coordinator に任せる |
| メソッド | `ShareText(ShareTextPayload, Action<ShareOperationResult>? onResult = null, Action<ShareChooserActionResult>? onChooserAction = null)` | アクションが 6 個以上なら `LogWarning`（切り詰めない）。`onChooserAction` を coordinator に登録（null も登録。前の登録を消す） |
| メソッド | `ShareImage`、`ShareImages`、`ShareFile`、`ShareFiles`、`RegisterDirectShareTarget`、`RemoveDirectShareTargets`（それぞれ Payload と `Action<ShareOperationResult>? onResult = null`） | |
| メソッド | `ShareWithCallback(ShareTextPayload, Action<ShareOperationResult>? onStarted = null, Action<ShareCallbackResult>? onSelected = null)` | アクションがあれば `LogWarning`（使わない。表示もされない） |
| メソッド | `CancelPendingShareCallback(Action<ShareOperationResult>? onResult = null)` | 常に成功 |
| メソッド | `Initialize()` | ブリッジを取り、リスナーを 2 つ登録 |
| 型 | `ShareOperationResult`（`Operation`、`IsSuccess`、`ErrorMessage`）、`ShareCallbackResult`（`Operation`、`SelectedPackageName`）、`ShareChooserActionResult`（`ActionId`。null は `""`） | `public readonly struct`。**接頭辞が無い**（`artifact/topics/os-prefix-violations/README.md`。この移行では改名しない。親課題 J-8） |
| 型 | `ShareChooserActionCallbackCoordinator` | 共通イベントと per-call の順（共通 → per-call）、last-registered wins、発火後も消さない、各呼び出しを空の catch で囲む |
| 型 | Payload 8 型（`ShareTextPayload`、`ChooserActionPayload`、`ShareImagePayload`、`ShareImagesPayload`、`ShareFilePayload`、`ShareFilesPayload`、`DirectShareTargetPayload`、`RemoveDirectShareTargetsPayload`） | `[Serializable] public sealed class`、public フィールド |
| 型 | `AndroidShareJsonBuilder`（public static、7 メソッド） | Payload から JSON |

### 2.2 per-call の callback と配送

- 操作の結果: 操作名ごとに 1 つの枠（`Dictionary<string, Action<ShareOperationResult>?>`）。呼ぶときに上書き（null も上書き。last-registered wins）。最初の完了が枠を読んで消す
- `onSelected`: 1 つの枠。`ShareWithCallback` の再呼び出し、`CancelPendingShareCallback`、`shareWithCallback` の失敗で消える。届いたら 1 回で消える
- **すべての結果は `UnityMainThreadDispatcher` を通って後のフレームに出る**（Kotlin が main に積み、listener が dispatcher に積む）。呼び出しの中でイベントが出ることは無い
- 共通イベントの購読者が例外を投げると、per-call は呼ばれない（`FireOperationResult` に try/catch が無い。`artifact/topics/event-subscriber-isolation/README.md` で未着手）
- 枠の辞書は、UnityMain が書き、Android の main が読んで消す。ロックは無い（潜在の競合）

### 2.3 1.x の検査と文言

C# 側の検査は無い（6 個以上のアクションの警告と、`ShareWithCallback` にアクションを渡した警告だけ）。`payload` が null なら呼び出し側に `NullReferenceException` が出る。検査は Kotlin の parser（`UnityShareJsonParser.kt`）とドメイン層が行い、文言は `OldBridge` の表で決まる:

| 原因 | 1.x の `ErrorMessage` |
|---|---|
| 本文が空・空白（parser） | `Failed to shareText: text is required`（`shareWithCallback` も同じ形） |
| `filePath` が空・空白（parser） | `Failed to shareImage: filePath is required`、`Failed to shareFile: filePath is required` |
| `filePaths` が空（parser） | `Failed to shareImages: filePaths must not be empty`、`Failed to shareFiles: filePaths must not be empty` |
| `ids` が空（parser） | `Failed to removeDirectShareTargets: ids must not be empty` |
| Direct Share の `id` / `label` / `iconBase64` が空（parser） | `Failed to registerDirectShareTarget: id is required`（`label is required`、`iconBase64 is required`） |
| `mimeType` が空白 | 既定値を使う（text は `text/plain`、image は `image/*`）。失敗しない |
| `ShareDomainError.NoShareTarget`、`ActivityNotFoundException` | `No app available to handle this share request.` |
| `ShareDomainError.FileNotFound(path)` | `File not found: ${path}` |
| `ShareDomainError.IllegalFileAccess(path)` | `File cannot be shared: ${path}. Ensure the file is in a supported directory.` |
| `ShareDomainError.InvalidMimeType(m)` | `Invalid MIME type: ${m}`（parser が空白を既定値にするので、ふつうは出ない） |
| `ShareDomainError.DirectShareRegistrationFailed(reason)` | `Failed to register Direct Share target: ${reason}`（`push_failed` か例外の文言） |
| `ShareDomainError.InvalidBase64Icon(id)` | `Invalid icon data for Direct Share target: ${id}` |
| `SecurityException` | `Security restriction while executing $name: ${message}` |
| そのほかの例外 | `Failed to $name: ${message ?: simpleName}` |
| C# の `TryPrepareCall` が false（Android 以外。ターゲットが Android の Editor を含む） | `<op> could not be started.`（`LogWarning("... can only be called on Android.")`）。**イベントは出る**（後のフレーム） |
| `pluginInstance` / `currentActivity` が null | `<op> could not be started.`（`LogError`） |
| `AndroidJavaObject.Call` の例外 | `ex.Message`（`LogError`） |

Chooser Action（parser と 1.x のライブラリ）:

- `label`・`iconBase64`・`intentAction` のどれかが空白のアクションは丸ごと捨てる（表示もされない）
- ID（`intentAction`）は空白と重複を除き、`android.intent.action.SEND` は通知の対象から除く（表示はされ、押しても通知は来ない）
- Base64 として読めない・画像にできないアイコンのアクションは黙って飛ばす

### 2.4 ログ

`ShareText` 以外は、`json: {json}` で JSON を丸ごとログに出す（本文、パス、`iconBase64` を含む）。`ShareText` は件数だけ。

### 2.5 テスト

- 層 1: `AndroidShareChooserActionCallbackCoordinatorTests`（8）、`AndroidShareJsonBuilderTests`（21）、`AndroidShareResultTests`（8）、`AndroidShareSampleSceneWiringTests`（6）
- 層 2a / 2b: 無い（Manager は `#if UNITY_ANDROID` で、`AndroidJavaObject` に頼るため）

### 2.6 サンプル

`Runtime/UI/Android/Share/AndroidShareManagerExampleController.cs`（`UNITY_ANDROID || UNITY_EDITOR`。Manager の呼び出しは `UNITY_ANDROID && !UNITY_EDITOR` の中）。ボタン 15 個、結果は `ResultTextBlock` の 1 つ。画像はプロジェクトの `Assets/Resources/Images/share_sample_image.png` から `persistentDataPath` に書く（パッケージには無い。Test Android™ Profile の Player では使えない）。サンプルの設計はこの計画の範囲外（`design-sample-scene`）。この移行で変える所は 5.1 の表に書く。

## 3. 実装制約

- `agent-rules/coding-rules/common.md` の「Unity Bridge パターン > Android（native-toolkit の C ABI）」（Dialog の移行で書いた）: 2 層（Bridge + Manager）、共通部は `Runtime/Android/Common/`、`DllImport` は `UNITY_ANDROID && !UNITY_EDITOR`、型と純粋な関数は `UNITY_ANDROID || UNITY_EDITOR`、完了は `AndroidNativeCallbackQueue` に入れるだけ、`UnityMainThreadDispatcher` は使わない、どのスレッドでも完了を待たない、コールバックが受け取ったハンドルは写して `_free`
- `agent-rules/coding-rules/csharp.md`: `#nullable enable`、`LogTag`、`[{LogTag}][{nameof(Method)}]`、結果として返す失敗は `LogWarning`（Android の C ABI の層）
- 公開 API を変えない（親課題 J-4 の例外は名前空間だけで、それは済んだ）
- 32 ビットと Mono は対象外（親課題 J-10）

## 4. 実装対象 API 一覧（C# 側の呼び出し方針）

### 4.1 `DllImport` の署名と構造体

`Runtime/Android/Share/AndroidShareCApi.cs`（Bridge）。`[DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = Cdecl)]`。

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct TextContent { public uint StructSize; public uint Reserved0; public IntPtr Text, Title, Subject, MimeType, PreviewTitle, PreviewThumbnailPath; }   // 56
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ChooserAction { public IntPtr Id, Label, Icon; public UIntPtr IconSize; }                                                               // 32
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct DirectTarget { public uint StructSize; public uint Reserved0; public IntPtr Id, Label, Category, Icon; public UIntPtr IconSize; }      // 48

internal delegate void DoneCallback(IntPtr userData, int error, uint systemCode);
internal delegate void ChooserActionCallback(IntPtr userData, IntPtr actionId);
internal delegate void SelectionCallback(IntPtr userData, ulong requestId, IntPtr packageName);
internal delegate void ReleaseCallback(IntPtr userData);

int  ntk_share_text(ref TextContent content, IntPtr actions, UIntPtr actionCount, IntPtr callback, IntPtr userData, IntPtr release);
int  ntk_share_image(IntPtr path, IntPtr mimeType, IntPtr callback, IntPtr userData, IntPtr release);
int  ntk_share_images(IntPtr paths, UIntPtr count, IntPtr callback, IntPtr userData, IntPtr release);
int  ntk_share_file(IntPtr path, IntPtr callback, IntPtr userData, IntPtr release);
int  ntk_share_files(IntPtr paths, UIntPtr count, IntPtr callback, IntPtr userData, IntPtr release);
int  ntk_share_register_direct_target(ref DirectTarget target);
int  ntk_share_remove_direct_targets(IntPtr ids, UIntPtr count);
int  ntk_share_text_for_selection(ref TextContent content, IntPtr callback, IntPtr userData, IntPtr release, out ulong requestId);
int  ntk_share_cancel_selection(ulong requestId);
int  ntk_share_add_chooser_action_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);
int  ntk_share_add_selection_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);
void ntk_share_listener_remove(IntPtr listener);
```

- 要求の ID は `ulong`（`Handoff` 4.3。`uint` にすると C が 8 バイトを書いて壊す）
- Chooser Action の配列は、`ChooserAction` を `count` 個並べた 1 つのブロックを確保し、`Marshal.StructureToPtr` で書く
- 文字列の配列（`const char* const*`）は `AllocStringArray`（今は `AndroidDialogCApi` にある。共通部に移す。5.1）、アイコンのバイト列は共通部に足す `AllocBytes` で作る

### 4.2 引数の変換（公開 API は変えない）

1.x の parser と同じ結果になるように、C# が先に整える。整えた後の値だけを C に渡す。

| 入力 | C# がすること | 1.x との関係 |
|---|---|---|
| `payload` が null | `ArgumentNullException` を呼び出し側に投げる（J-S7） | 1.x は `NullReferenceException`（5.3） |
| `text` が null・空・空白（`ShareText`、`ShareWithCallback`） | 検査の失敗 `Failed to <op>: text is required` | 1.x と同じ文言 |
| `title`、`subject`、`previewTitle`、`previewThumbnailPath` が null・空・空白 | NULL を渡す（無し） | 1.x の JSON は空白の任意項目を省いた |
| `mimeType` が null・空・空白 | NULL を渡す（ライブラリの既定値: text は `text/plain`、image は `image/*`） | 1.x と同じ（parser が既定値にした） |
| `chooserActions`（`ShareText`） | 4.2.1 のとおり整える | |
| `chooserActions`（`ShareWithCallback`） | 渡さない（F-8 に引数が無い）。1 個以上なら今と同じ `LogWarning` | 1.x と同じ |
| `filePath` が null・空・空白（`ShareImage`、`ShareFile`） | 検査の失敗 `Failed to <op>: filePath is required` | 1.x と同じ |
| `filePaths` が null・空（`ShareImages`、`ShareFiles`） | 検査の失敗 `Failed to <op>: filePaths must not be empty` | 1.x と同じ |
| `filePaths` の要素が null | 検査の失敗 `Failed to <op>: filePaths must not contain null.`（J-S8） | 1.x は JSON の `null` を文字列 `"null"` として扱った見込み（未確認。5.3） |
| Direct Share の `id` / `label` / `iconBase64` が null・空・空白 | 検査の失敗 `Failed to registerDirectShareTarget: id is required`（`label is required`、`iconBase64 is required`） | 1.x と同じ |
| Direct Share の `iconBase64` が Base64 として読めない | 検査の失敗 `Invalid icon data for Direct Share target: <id>` | 1.x と同じ文言（1.x はライブラリの `InvalidBase64Icon`） |
| Direct Share の `category` が null・空・空白 | NULL を渡す（既定値 `android.shortcut.conversation`） | 1.x と同じ |
| `ids` が null・空（`RemoveDirectShareTargets`） | 検査の失敗 `Failed to removeDirectShareTargets: ids must not be empty` | 1.x と同じ |
| `ids` の要素が null | 検査の失敗 `Failed to removeDirectShareTargets: ids must not contain null.`（J-S8） | 5.3 |

文字列はすべて共通部の `AllocUtf8`（NUL 終端の UTF-8。対になっていないサロゲートは U+FFFD）で渡す。途中の U+0000 はそこで切れる（C ABI の形）。

#### 4.2.1 Chooser Action の整え方（1.x の parser とライブラリに合わせる）

1. 要素が null、または `label`・`iconBase64`・`intentAction` のどれかが null・空白のアクションを捨てる（1.x の parser と同じ。表示もされない）
2. `iconBase64` を `Convert.FromBase64String` で解く。読めなければそのアクションを捨てる（1.x のライブラリと同じ）
3. `intentAction` が前のアクションと同じなら捨てる（1.x の distinct と同じ。最初のものを残す）
4. 残ったアクションを、`id = intentAction`、`label`、アイコンのバイト列で渡す
5. 6 個以上なら、今と同じ `LogWarning` を出す（切り詰めない。表示の上限は OS が決める）

保てないもの（5.3）: Base64 は読めるが画像として読めないアイコンがあると、Share 全体が `INVALID_CHOOSER_ACTION` で失敗する（C# は画像として読めるかを前もって確かめられない。SH-06）。`android.intent.action.SEND` の ID のアクションは、押すと通知が来る（1.x は来なかった）。

### 4.3 結果と完了

- 開く操作（F-1〜F-5、F-8）: 戻り値が `NONE` なら受け付けた。完了の `error` が `NONE` なら成功（**Sharesheet が開いた、という意味。1.x の「起動できた」と同じ**）、それ以外は失敗
- Direct Share（F-6、F-7）: 戻り値が結果
- 取り消し（F-9）: 戻り値は見ない（ログだけ）。1.x と同じく、いつも成功を返す（SH-14）
- 選ばれたアプリ（F-11）: `request_id` が manager の「直前の選択の要求の ID」と同じなら、`ShareCallbackResult("shareWithCallback", packageName)`（`package_name` が NULL なら `null`）を出す。違えば捨てる（ログだけ）
- Chooser Action（F-10）: `ShareChooserActionResult(actionId)` を coordinator に渡す

**どの結果も、取り出し口 `AndroidNativeCallbackQueue` を通して、後のフレームに出す。呼び出しの中ではイベントを出さない**（J-S2。1.x と同じ。同期の Direct Share、入口の拒否、C# の検査の失敗、Editor もすべて）。

### 4.4 エラーの文言とログ

`<op>` は 2.1 の `Operation*` の値。

| 原因 | 時点 | `ErrorMessage` | ログ |
|---|---|---|---|
| C# の検査（4.2 の表） | 呼び出し | 4.2 の文言（1.x と同じ。要素の null だけ新しい） | `LogWarning` |
| Editor・Android 以外（J-S3） | 呼び出し | `<op> could not be started.` | `LogWarning("<op> can only be called on Android.")`（1.x と同じ） |
| ネイティブが使えない（共通部の `NativeUnavailable`） | 呼び出し | `Failed to <op>: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).` | `LogWarning` |
| `NOT_INITIALIZED`（入口・完了） | 呼び出し / 完了 | `Failed to <op>: the native library is not initialized yet.` | `LogWarning` |
| `INVALID_PARAMETER` / `NOT_SUPPORTED`（入口） | 呼び出し | `Failed to <op>: the request was rejected (<名前>).` | `LogError`（C# の組み立ての誤り。正しい実装では起きない） |
| `INVALID_PARAMETER`（完了・Direct Share。Kotlin の `IllegalArgumentException`） | 完了 / 同期 | `Failed to <op>: the request was rejected (INVALID_PARAMETER).` | `LogWarning` |
| `EMPTY_CONTENT` | 呼び出し / 完了 | `Failed to <op>: text is required`（1.x の parser の文言。C# の検査が先に拒むので、来るのは Kotlin の `isBlank` が C# の `IsNullOrWhiteSpace` と違う空白だけ） | `LogWarning` |
| `EMPTY_FILE_LIST` | 呼び出し | `Failed to <op>: filePaths must not be empty`（C# が先に拒む） | `LogWarning` |
| `EMPTY_ID_LIST` | 呼び出し | `Failed to removeDirectShareTargets: ids must not be empty`（同上） | `LogWarning` |
| `NO_SHARE_TARGET` | 完了 | `No app available to handle this share request.`（1.x と同じ） | `LogWarning` |
| `FILE_NOT_FOUND` | 完了 | `File not found: <path>`。1 つのパスの操作はそのパス。複数のときは、`File.Exists` が偽の最初のパス（見つからなければ全部のパスを `, ` でつないだもの）（J-S9） | `LogWarning` |
| `ILLEGAL_FILE_ACCESS` | 完了 | `File cannot be shared: <path>. Ensure the file is in a supported directory.`。1 つのパスの操作はそのパス。複数のときは全部のパスを `, ` でつないだもの（J-S9） | `LogWarning` |
| `INVALID_MIME_TYPE` | 完了 | `Invalid MIME type: <渡した値>`（C# が空白を NULL にするので、ふつうは来ない） | `LogWarning` |
| `DIRECT_SHARE_REGISTRATION_FAILED` | 同期 | `Failed to register Direct Share target: the system did not accept the shortcut.`（理由は C ABI から来ない。5.3） | `LogWarning` |
| `INVALID_ICON` | 同期 | `Invalid icon data for Direct Share target: <id>`（1.x と同じ） | `LogWarning` |
| `INVALID_CHOOSER_ACTION` | 完了 | `Failed to shareText: a chooser action icon could not be read as an image.`（新しい。5.3） | `LogWarning` |
| `NOT_FOREGROUND` | 完了 | `Failed to <op>: the app is not in the foreground.`（新しい。5.3） | `LogWarning` |
| `OUT_OF_MEMORY` / `UNKNOWN` / 想定外の値 | 呼び出し / 完了 | `Failed to <op>: <名前>.` | `LogWarning` |
| C# の例外（変換・確保の失敗など） | 呼び出し | `ex.Message`（1.x の `Call` の例外と同じ扱い） | `LogError` |
| 受け口の中の例外 | - | イベントは出ない（その完了は失われる） | `LogError` |

`<名前>` はエラーの列挙の名前。名前と文言への変換は純粋な関数にし、層 1 で固める。

**ログ（J-S10）**: JSON を丸ごと出すのをやめる。本文（`text`）と `subject` は長さだけを出す（native-toolkit の `logSafeDescription` と同じ考え方。UI の補助が logcat を読むので、値が成果物に残りうる）。題名、MIME、パス、ID、ラベル、カテゴリはそのまま出す（1.x もライブラリも出している。秘密ではない）。アイコンはバイト数だけ。選ばれたアプリのパッケージ名と、押されたアクションの ID はそのまま出す。

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | 置き場所 | ガード | 内容 |
|---|---|---|---|---|
| 新規作成 | `AndroidShareCApi.cs` | `Runtime/Android/Share/` | 型と純粋な関数は `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport` は `UNITY_ANDROID && !UNITY_EDITOR` | Bridge。4.1 の構造体・delegate・`extern`、入れ子の `Operation` 列挙（9 個。`Operation*` 定数と対応）、エラー定数 0〜16、`ShareCall`（1 回の呼び出しの記録: 操作、パス、MIME、Direct Share の ID）、4.2 の検査と整え（`CheckText`、`CheckPath`、`CheckPaths`、`CheckIds`、`CheckDirectTarget`、`NormalizeActions`、`NullIfBlank`）、構造体の組み立て（`BuildTextContent`、`BuildActions`、`BuildDirectTarget`）、4.4 の文言（`ErrorName`、`FailureMessage`、`NativeUnavailableMessage`、`MissingPath`） |
| 既存変更 | `AndroidShareManager.cs` | `Runtime/Android/Share/` | 型は `UNITY_ANDROID \|\| UNITY_EDITOR`（今は `UNITY_ANDROID`）。受け口・delegate を保つフィールド・関数ポインタ・`user_data` の表は `UNITY_ANDROID && !UNITY_EDITOR` | `AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher` を消し、`AndroidShareCApi` を呼ぶ。公開 API・既定値・文言は 2 章のとおり保つ（5.3 を除く）。per-call の枠（2.2）を保ち、ロックで守る（J-S5）。受け口（J-S1、J-S4）。`Initialize()` は J-S6 |
| 既存変更 | `AndroidNativeToolkitCApi.cs` | `Runtime/Android/Common/` | 今のまま | `AllocStringArray` を `AndroidDialogCApi` から移し、`AllocBytes(byte[], List<IntPtr>)` を足す（どちらも `Alloc` を使い、漏れない形） |
| 既存変更 | `AndroidDialogCApi.cs`、`AndroidDialogCApiTests.cs` | `Runtime/Android/Dialog/`、`Tests/Runtime/` | 今のまま | `AllocStringArray` を共通部の呼び出しに替える（振る舞いは変えない） |
| 既存変更 | `AndroidShareJsonBuilder.cs` | `Runtime/Android/Share/` | 今のまま | 中身は変えない。Manager から使わなくなる。XML コメントに「2.0.0 では Manager は使わない。4 機能の移行の後に消す（親課題の「移行時にやること」）」と書く（J-S11） |
| 既存変更 | `ShareChooserActionCallbackCoordinator.cs` | `Runtime/Android/Share/` | 今のまま | 変えない。Manager は `dispatch` に「その場で走らせる」（`a => a()`）を渡す（Fire は取り出し口の中、UnityMain で呼ぶので） |
| 既存変更 | `AndroidShareManagerExampleController.cs` | `Runtime/UI/Android/Share/` | 今のまま | 変えない（画面とログに本文は出していない）。要検証: Editor でのボタンの表示（今は「Android device only」で Manager を呼ばない。変えない） |
| 新規作成 | `AndroidShareCApiTests.cs` | `Tests/Runtime/` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（7.1） |
| 新規作成 | `AndroidShareManagerEditorTests.cs` | `Tests/PlayMode/` | `UNITY_EDITOR` | 層 2a（7.2） |
| 新規作成 | `AndroidSharePlayerTests.cs` | `Tests/PlayMode/` | 外 `UNITY_ANDROID`、中 `!UNITY_EDITOR` | 層 2b（7.3、UI テスト計画） |
| 既存変更 | `android_ui_driver.py`、`AndroidUiDriver.cs` | `scripts/`、`Tests/PlayMode/` | - | 8.3 の命令（Sharesheet の中の文言を、スクロールしながら探して押す）と、Sharesheet が出ている間は画面の構造を書き出さないこと |
| 新規作成（要検証） | テスト用の共有先 | `Assets/Plugins/Android/` の下（パッケージの外。テスト用の Player だけ） | - | 8.3。`com.unity3d.player.UnityPlayerGameActivity` を指す `<activity-alias>` に `SEND` の intent-filter を付け、ラベルを `NTK test target` にする。選ぶとテスト用の Player 自身が前に戻る。マージした manifest で使えるかを 5.5 の手順 2 で確かめる |
| 既存変更 | `common.md`、`testing.md` | `agent-rules/coding-rules/` | - | `common.md` の Android の節に「呼び出しごとに `user_data` を表で付ける形（Share）と、種類だけを渡す形（Dialog）」を足す。`testing.md` の Android の Share の層 |
| 非変更 | Payload 8 型、結果 3 型、`AndroidShareResultTests`、`AndroidShareJsonBuilderTests`、`AndroidShareChooserActionCallbackCoordinatorTests`、`AndroidShareSampleSceneWiringTests`、`Runtime/Resources/UI/Android/Share/*` | - | - | 公開の型と、そのテストは変えない |

Manager を `|| UNITY_EDITOR` にするのは、層 2a で Editor の契約（J-S3）と配送（J-S2、J-S4、J-S5）を確かめるため。

### 5.2 判断

| # | 内容 | 決定（案） |
|---|---|---|
| J-S1 | 完了と呼び出しの対応付け | **呼び出しごとに `user_data` を付ける**（`Handoff` 4.3）。Manager の static な `ConcurrentDictionary<long, ShareCall>` に、連番のキーで、**呼ぶ前に**入れる。キーを `IntPtr` にして渡す。完了の受け口（Android の main）は表を読むだけで、引いた `ShareCall` と `error` を取り出し口に入れる。表から消すのは `release` の受け口だけ。入口で拒まれたときは `release` が戻る前に来て消し、Manager が手元の `ShareCall` で失敗を出す。Dialog（J-D1）のように種類だけを渡す形にしないのは、文言にパス（4.4）を入れるのに呼び出しごとの記録が要るため |
| J-S2 | 結果の時点 | **すべて取り出し口を通して後のフレームに出す。** C# の検査の失敗、Editor、入口の拒否、同期の Direct Share もそうする。1.x はどの結果も Kotlin と dispatcher を通って後のフレームに出ており、`Show*` の中で同期にイベントを出す Dialog とは違う。公開 API の振る舞いを保つ。呼んだスレッドがどれでも、イベントは UnityMain で出る |
| J-S3 | Editor・Android 以外 | 1.x と同じく `LogWarning("<op> can only be called on Android.")` を出し、失敗 `<op> could not be started.` を後のフレームに出す（1.x は Android ターゲットの Editor で同じだった）。`|| UNITY_EDITOR` にしたので、ほかのターゲットの Editor でも同じになる（型が無かった所に型ができる。5.3） |
| J-S4 | 結果を誰に出すか | **取り出した時点で生きている Manager（`_instance`）に出す。いなければ捨てる（ログだけ）。** 1.x は Manager の `OnDestroy` でブリッジのリスナーを外し、その後の結果を捨てていた。Dialog（J-D2）の「新しい `Instance` を作って出す」にはしない（Share は 1.x の振る舞いがはっきりしており、終了の間に GameObject を作り直さない）。Manager が無効（`enabled = false`）でも届く（取り出し口は別の GameObject） |
| J-S5 | per-call の枠 | 1.x の規則（2.2）を保つ: 操作名ごとの枠に、呼ぶときに上書き（null も）、最初の結果が読んで消す。枠は呼んだスレッドが書き、UnityMain が読んで消すので、`lock` で守る（1.x の競合を直す。ロックの中で利用者のコードを走らせない）。順は 1.x と同じく、共通イベント → per-call。共通イベントの購読者の例外は捕まえない（1.x と同じ。`event-subscriber-isolation` の範囲） |
| J-S6 | 公開の `Initialize()` とリスナー | 残す。中身は、ネイティブが使えるかを確かめ、リスナー 2 つ（F-10、F-11）が未登録なら登録する（何度呼んでもよい）。リスナーは**プロセスで 1 回だけ**登録し、外さない（static の受け口、`user_data` と `release` は NULL）。登録に失敗したら `LogWarning` を出し、次の `Initialize()`・`ShareText`・`ShareWithCallback` でやり直す。`Awake` から呼ぶ（UnityMain）。Manager の `OnDestroy` では外さない（J-S4 で、Manager がいなければ捨てるので同じ結果になる）。外さない理由: 外した直後の配送と `release` の時点の扱い（P1:153）を持ち込まずに済み、Manager を作り直しても登録の前に起きたイベントを失わない |
| J-S7 | `payload` が null | `ArgumentNullException` を呼び出し側に投げる（呼び出し側の誤り）。1.x は中で `NullReferenceException` だった（5.3） |
| J-S8 | 配列の要素の null | 検査の失敗 `Failed to <op>: filePaths must not contain null.` / `... ids must not contain null.`。C ABI は入口で `INVALID_PARAMETER` にするので、C# が先に言う（Dialog の J-D12 と同じ考え方） |
| J-S9 | 文言のパス | 1 つのパスの操作（`shareImage`、`shareFile`）は、`ShareCall` のパスを入れる（1.x と同じ文言）。複数のとき、`FILE_NOT_FOUND` は UnityMain で `File.Exists` が偽の最初のパス（ライブラリはパスを順に調べるので、ふつうはこれが原因のパス）、`ILLEGAL_FILE_ACCESS` は C# から範囲を判定できないので、全部のパスを `, ` でつなぐ（5.3）。`File.Exists` は文言のためだけに使い、検査には使わない（ネイティブの検査を C# で重ねない） |
| J-S10 | ログ | 4.4 の末尾のとおり。JSON を出すのをやめ、本文と `subject` は長さだけ |
| J-S11 | `AndroidShareJsonBuilder` | 公開のまま残し、Manager からは使わない（消すと公開 API が壊れる）。4 機能の移行の後に JSON の層をまとめて消す（親課題の「移行時にやること」、移行ガイドに書く）。`[Obsolete]` は付けない（利用者とテストに警告が出るため。消す版で付ける） |
| J-S12 | 選ばれたアプリの照合 | Manager が「直前の選択の要求の ID」（`ulong`、0 は無し）を UnityMain の側で持つ。`ShareWithCallback` は F-8 が戻った後、戻り値が `NONE` なら `out` の ID を入れる（受け付けていなければ 0 にする）。選択のイベントは取り出し口の中で照らし、同じなら `ShareCallbackReceived` → `onSelected`（1 回で消す）の順に出す。同じでなければ捨てる。照らす前に ID が入っていないことは無い（ID を入れるのは呼んだスレッドが戻った直後で、選択は人が選ぶので必ず後。呼んだのが UnityMain でないときは要検証。7.3） |
| J-S13 | `CancelPendingShareCallback` | `onSelected` を消し、直前の選択の要求の ID が 0 でなければ F-9 を呼び、ID を 0 にする。結果は 1.x と同じくいつも成功を、後のフレームに出す（SH-14） |
| J-S14 | `ShareWithCallback` の失敗 | 1.x と同じく `onSelected` を消す（入口の拒否と完了の失敗の両方。取り出し口の中で）。直前の選択の要求の ID は、受け付けたなら入れたまま（ライブラリの待ちは、開いた後の失敗では消えている。開く前の失敗では前の待ちが残るが、ID が新しいので前の選択は照らされずに捨てられる。1.x の「失敗したら待ちを消す」と同じ結果） |
| J-S15 | `OnDestroy` | per-call の枠、`onSelected`、coordinator（`Clear()`）を空にし、直前の選択の要求の ID があれば F-9 で取り消す（1.x の `clearShareOperationListener` が待ちを外したのと同じ）。リスナーは外さない（J-S6） |
| J-S16 | 呼ぶスレッド | 1.x と同じく、どのスレッドから呼んでもよい（Dialog の R3-9 と同じ。`AndroidNativeCallbackQueue.EnsureCreated()` は UnityMain の外では何もしない）。結果はいつも UnityMain で出る（J-S2） |

### 5.3 既知の差分（意図したもの）

XML コメントに書き、実装結果ファイル経由で次の版のマニュアル（移行ガイド）へ引き継ぐ。1.x の実測（5.5 の手順 3）で、推定の行を直す。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 後ろから呼ぶ（J-S2） | OS が黙って止めた見込み（起動は成功を返した。推定。手順 3 で確かめる） | `Failed to <op>: the app is not in the foreground.`。前面の判定は Sharesheet が出ている間も「前面」（1.9） |
| 画像として読めない Chooser Action のアイコン | そのアクションを黙って飛ばした | Share 全体が失敗 `Failed to shareText: a chooser action icon could not be read as an image.` |
| `android.intent.action.SEND` の ID の Chooser Action | 表示され、押しても通知は来なかった | 押すと通知が来る |
| 古い Chooser で選んだアプリ | 届いた（不具合。SH-13） | 届かない |
| 複数のファイルの `FILE_NOT_FOUND` / `ILLEGAL_FILE_ACCESS` の文言（J-S9） | 原因のパス | `File.Exists` が偽の最初のパス / 全部のパス |
| Direct Share の登録の失敗の文言 | `Failed to register Direct Share target: <理由>`（`push_failed` か例外の文言） | `Failed to register Direct Share target: the system did not accept the shortcut.` |
| `SecurityException` の文言 | `Security restriction while executing <op>: ...` | 来ない（C ABI は `UNKNOWN` に写す）。`Failed to <op>: UNKNOWN.` |
| そのほかの例外の文言 | 例外の文言を含む | 4.4 の固定の文言 |
| `payload` が null（J-S7） | `NullReferenceException` | `ArgumentNullException` |
| 配列の要素の null（J-S8） | 文字列 `"null"` として扱った見込み（未確認） | 検査の失敗 |
| 本文が null | 文字列 `"null"` を共有した見込み（未確認） | 検査の失敗 `Failed to <op>: text is required` |
| Editor（J-S3） | Android ターゲットのときだけ型があり、失敗のイベント | どのターゲットでも型があり、失敗のイベント |
| ログ（J-S10） | JSON を丸ごと出した（本文、パス、Base64 のアイコン） | 本文と `subject` は長さ、アイコンはバイト数 |
| `Initialize()`（J-S6） | ブリッジを取り、リスナーを登録した | リスナーを登録するだけ。何度呼んでもよい |
| 結果が Manager の破棄の後に来る（J-S4） | ブリッジのリスナーを外していたので捨てた | 捨てる（同じ） |
| per-call の枠の競合（J-S5） | ロックが無かった | ロックで守る |
| 32 ビット（ARMv7）と Mono | 動いた（ブリッジは Java） | ビルドが止まる（親課題 J-10） |
| `AndroidShareJsonBuilder`（J-S11） | Manager が使った | 使わない。後の版で消す |

### 5.4 呼び出しの流れと契約（`ShareFiles` の例）

1. `payload` が null なら `ArgumentNullException`
2. ログ（パスの件数とパス）。per-call の枠に `onResult` を入れる（`lock`。上書き）
3. 4.2 の検査。失敗なら `LogWarning` を出し、失敗の結果を取り出し口に入れて終わる
4. Editor・Android 以外なら、`LogWarning` と `<op> could not be started.` を取り出し口に入れて終わる（J-S3）
5. `AndroidNativeCallbackQueue.EnsureCreated()`。`EnsureNativeAvailable()` が `Available` でなければ、4.4 の失敗を取り出し口に入れて終わる
6. `ShareCall`（操作、パス）を作り、連番のキーで `user_data` の表に入れる（Manager の private static な `AddCall(ShareCall)`。キーを返す）
7. 確保の一覧を try の外で空に作り、`AllocStringArray` で配列を作り、`ntk_share_files(paths, count, s_donePointer, key, s_releasePointer)` を呼ぶ。完了と `release` は、この呼び出しが戻る前に Android の main で来うる
8. `finally` で確保したものを全部解放する（ライブラリは呼び出しの間に写す）
9. 戻り値が `NONE` 以外なら、4.4 の失敗を取り出し口に入れる（Manager の private な `PostFailure(ShareCall, string)`。`release` はもう来ていて、表から消えている）。C# の例外は `ex.Message` の失敗にする
10. 完了の受け口（Android の main）: `try { if (表から key を読めたら) Post(() => 生きている Manager の RaiseOperation(call, error)) } catch (Exception e) { LogError }`
11. `release` の受け口（Android の main か、入口の拒否なら呼んだスレッド）: 表から key を消す
12. `RaiseOperation`（UnityMain）: 結果を作り、枠から per-call を取り出して消す（`lock`）、`shareWithCallback` の失敗なら `onSelected` を消す、`ShareOperationCompleted` → per-call の順に呼ぶ

```csharp
string? error = null;
var allocated = new List<IntPtr>();
long key = AddCall(call);                       // 6
try
{
    try { /* 7 */ }
    finally { AndroidNativeToolkitCApi.FreeAll(allocated); }   // 8
}
catch (Exception e) { error = e.Message; }
if (error != null) PostFailure(call, error);   // 9
```

- 選択の受け口（Android の main、static）: `try { pkg = package_name が NULL でなければ ReadString して ntk_string_free; Post(() => 生きている Manager の RaiseSelection(requestId, pkg)) } catch { LogError }`
- アクションの受け口: `try { id = ReadString(action_id); finally ntk_string_free; Post(() => 生きている Manager の coordinator.Fire(new ShareChooserActionResult(id))) } catch { LogError }`
- **どのスレッドでも完了も `release` も待たない**（親課題 A-4）

### 5.5 実装順

各段階の終わりに `scripts/verify_unity_android.sh`（EditMode、Editor の PlayMode、実機の IL2CPP、パッケージの利用者の条件）を流す。

1. UI テスト計画 v1（7.3 に番号を振る）をこの設計のレビューの後に作る
2. **UI の補助を Share に広げる**（8.3）。Sharesheet の中の文言を探して押す命令と、Sharesheet が出ている間は画面の構造を書き出さないこと。テスト用の共有先（5.1。`<activity-alias>`）がテスト用の Player で Sharesheet に出て、選ぶと Player が前に戻ることを確かめる（要検証。だめなら 8.3 の代わりの案）
3. **1.x のまま** `AndroidSharePlayerTests` のうち 1.x で流せるもの（UI テスト計画の「1.x」の列）を書いて流し、結果を記録する。推定と書いた所（5.3 の「後ろから呼ぶ」「要素の null」「本文が null」、1.6 の表示の上限）を実測に置き換え、違えば設計書の新しい版にする
4. 共通部（`AllocStringArray` の移動、`AllocBytes`）と Dialog の呼び出しの差し替え。Dialog のテストが同じに通ること
5. `AndroidShareCApi` の構造体と純粋な関数、層 1
6. `AndroidShareCApi` の `extern`
7. `AndroidShareManager` の置き換え（J-S1〜J-S16）と層 2a
8. 2.0.0 の列のテスト（C ABI にしか無いもの）を足し、全体を流す。1.x の列と比べ、違いが 5.3 の表にあることを確かめる
9. `common.md`、`testing.md`
10. 実装結果ファイル（`artifact/android/share/results/`）に 5.3 と 4.4、UI テスト計画の 2 列の結果を書き写す

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | `payload` が null | C# | `ArgumentNullException`（呼び出し側） | 2a |
| E-2 | 必須の値が null・空・空白（本文、`filePath`、`filePaths`、`ids`、Direct Share の `id` / `label` / `iconBase64`） | C# | 後のフレーム、1.x の文言、`LogWarning` | 1（検査の純粋な関数）、2a（イベントが呼び出しの中で出ず、次のフレームに 1 回）、2b |
| E-3 | 配列の要素の null（J-S8） | C# | 後のフレーム | 1、2a |
| E-4 | Direct Share の `iconBase64` が Base64 でない | C# | 後のフレーム、`Invalid icon data for Direct Share target: <id>` | 1、2b |
| E-5 | Chooser Action の整え（捨てる、重複、Base64 でない） | C# | 失敗にしない | 1（`NormalizeActions`）、2b（表示されるラベル） |
| E-6 | Editor・Android 以外（J-S3） | C# | 後のフレーム、`<op> could not be started.` | 2a（9 操作） |
| E-7 | ネイティブが使えない | C# | 後のフレーム、4.4 | 1（例外の分類と版、文言） |
| E-8 | 入口の `INVALID_PARAMETER` / `NOT_SUPPORTED` | ネイティブ（入口） | 後のフレーム、`LogError` | 1（文言）。正しい実装では起きない。構造体の位置は 1 で固定 |
| E-9 | `NOT_INITIALIZED` | ネイティブ | 後のフレーム | 1（文言）。起こせない |
| E-10 | `NOT_FOREGROUND` | ネイティブ（完了） | 後のフレーム | 1（文言）、2b（試す。Dialog の AD-27 と同じ形） |
| E-11 | `FILE_NOT_FOUND`（1 つ・複数） | ネイティブ（完了） | 後のフレーム、パスを含む文言 | 1（文言と `MissingPath`）、2b |
| E-12 | `ILLEGAL_FILE_ACCESS`（1 つ・複数） | ネイティブ（完了） | 同上 | 1、2b（`/system/etc/hosts` のような範囲外の実在のファイル） |
| E-13 | `INVALID_CHOOSER_ACTION`（画像でないアイコン） | ネイティブ（完了） | 後のフレーム | 1（文言）、2b |
| E-14 | `EMPTY_CONTENT`（Kotlin だけが空白と見る文字） | ネイティブ（完了） | 同上 | 1（文言）。2b は試す（U+3000 など。C# の `IsNullOrWhiteSpace` と Kotlin の `isBlank` が両方とも空白と見るなら起こせない） |
| E-15 | `INVALID_ICON`（Direct Share、Base64 は読めるが画像でない） | ネイティブ（同期） | 後のフレーム | 1、2b |
| E-16 | `DIRECT_SHARE_REGISTRATION_FAILED` | ネイティブ（同期） | 後のフレーム | 1（文言）。起こせない（レート制限など） |
| E-17 | `NO_SHARE_TARGET` | ネイティブ（完了） | 後のフレーム | 1（文言）。起こせない |
| E-18 | `INVALID_MIME_TYPE` | ネイティブ（完了） | 後のフレーム | 1（文言）。C# が空白を NULL にするので起きない |
| E-19 | `UNKNOWN` / `OUT_OF_MEMORY` | ネイティブ | 後のフレーム | 1（文言）。起こせない |
| E-20 | C# の例外 | C# | 後のフレーム、`ex.Message`、`LogError` | 形はコードレビュー |
| E-21 | 受け口の中の例外 | C# | 出ない、`LogError` | コードレビュー。起こせない |
| E-22 | リスナーの登録の失敗（J-S6） | ネイティブ（入口） | `LogWarning`、次の呼び出しでやり直す | コードレビュー。起こせない |
| E-23 | 選ばれたアプリの ID が直前の要求と違う | ネイティブ（イベント） | 捨てる | 2a（取り出し口に偽のイベントを入れる） |

## 7. テスト方針

### 7.1 層 1（EditMode、`AndroidShareCApiTests`）

- 3 つの構造体の大きさと全フィールドの位置（56 / 32 / 48。`StructLayoutTest.cpp` の値）
- 要求の ID が 64 ビットであること: `SelectionCallback` の 2 番目の引数の型が `ulong`（反射。`extern` は Editor ではコンパイルされないので delegate で見る）
- 構造体の組み立て: `struct_size`、`reserved0 = 0`、任意の値の空白が NULL になること、文字列が NUL 終端の UTF-8 であること（読み戻す）、Chooser Action の配列の並びとアイコンのバイト列と大きさ、Direct Share の `category` の NULL
- 検査（4.2）: 各操作の必須の値の null・空・空白、配列の空と要素の null、Base64 でないアイコン。文言が 1.x と同じであること
- Chooser Action の整え（4.2.1）: 空白の項目を捨てる、Base64 でないものを捨てる、重複は最初を残す、順を保つ、SEND を残す
- 文言（4.4）: 全エラー値と想定外の値、9 操作の `<op>`、`MissingPath`（1 つ、複数、どれも在る）

`AndroidNativeToolkitCApiTests`（共通部）: `AllocStringArray`（移したもの）と `AllocBytes` の読み戻し。

### 7.2 層 2a（PlayMode・Editor、`AndroidShareManagerEditorTests`）

Manager が Editor でもコンパイルされる（5.1）ので、Editor の契約と配送を確かめる。

- Editor の 9 操作（J-S3）: 呼び出しの中ではイベントが出ず（呼んだ直後は 0 回）、次のフレームで `ShareOperationCompleted` → per-call の順に 1 回、`<op> could not be started.`。`LogWarning` が出る
- 検査の失敗（E-2、E-3）: 同じく後のフレームに 1 回、1.x の文言
- `payload` が null（E-1）: `ArgumentNullException`
- per-call の枠（J-S5）: 同じ操作を 2 回続けて呼ぶと、1 回目の結果に 2 回目の per-call が呼ばれ、2 回目の結果には per-call が無い（1.x と同じ）。null の上書き
- 配送（J-S4）: 結果を取り出し口に入れ、Manager を無効にしても届く。Manager を壊すと捨てる（新しい Manager を作らない）
- 選ばれたアプリ（J-S12、E-23）: テストが受け口と同じ形の処理（`internal` の `PostSelection(requestId, pkg)`）を、ID が一致するものと違うもので取り出し口に入れる。一致したものだけが `ShareCallbackReceived` → `onSelected` の順に 1 回出る。`onSelected` は 2 回目には呼ばれない
- `CancelPendingShareCallback`（J-S13）: 成功が後のフレームに出て、その後の選択（一致する ID）に `onSelected` が呼ばれない
- Chooser Action（`PostChooserAction(id)`）: coordinator を通って共通イベント → per-call の順に出る。per-call は消えない（何度でも）

### 7.3 層 2b（実機の Player、`AndroidSharePlayerTests`）

個々のテストと期待値は UI テスト計画 v1 に番号を振る（この設計のレビューの後）。この節は方針と共通の約束を書く。

**IL2CPP の ARM64 だけ。** UI の補助を使うので、カテゴリ `AndroidUiDriver` をクラスに付ける。後片付けは Dialog の形（`Safely`、要求の数とイベントの数の照合、イベントのスレッドの照合。UI テスト計画 v2 の 3 章）を写す。Sharesheet が残っていれば Back で閉じる（補助が `com.android.intentresolver` のパッケージの有無で判定する）。

- **開く操作**（テキスト、URL、題名と件名、プレビュー、画像、複数の画像、ファイル、複数のファイル）: Sharesheet が開き（補助が `com.android.intentresolver` を見る）、成功の結果が 1 回、Sharesheet を Back で閉じる。画像とファイルは `persistentDataPath` に書いたものを使う
- **失敗の経路**: 無いファイル（E-11）、範囲外の実在のファイル（E-12）、複数のときの文言、画像でない Chooser Action のアイコン（E-13）、画像でない Direct Share のアイコン（E-15）、C# の検査の失敗（E-2〜E-4）。Sharesheet が出ないこと
- **Chooser Action**（API 34 以上の端末）: アクションのラベルが Sharesheet に出て、押すと `ShareChooserActionTapped` → per-call の順に 1 回、ID が `intentAction`。捨てたアクション（空白、Base64 でない、重複）が出ないこと。SEND のアクションの通知（C ABI の版の列）
- **選ばれたアプリ**: `ShareWithCallback` で Sharesheet を開き、テスト用の共有先（`NTK test target`）を押す。`ShareCallbackReceived` に自分のパッケージ名が 1 回、`onSelected` が 1 回。Back で閉じたときは何も来ない。`CancelPendingShareCallback` の後に選んでも来ない
- **Direct Share**: 登録・削除の成功が後のフレームに 1 回。Sharesheet に出るかは確かめない（アプリ側の `<share-target>` が要る。1.7。8.4）
- **後ろから呼ぶ**（E-10）: Dialog の AD-27 と同じ形（補助の `background`、別のスレッドが Bridge を直接呼ぶ）。`Failed to <op>: the app is not in the foreground.`
- **呼ぶスレッド**（J-S16）: 別のスレッドから `ShareText` を呼び、例外が出ず、結果が UnityMain で 1 回
- **Direct Share の同期の結果が呼び出しの中で出ないこと**（J-S2）: UnityMain から呼び、戻った時点で 0 回、次のフレームで 1 回
- 選択の照合の時点（J-S12）: 別のスレッドから `ShareWithCallback` を呼んだとき、ID を入れる前に選択が照らされないこと（人が押すので実際には起きない。設計上の確かめ）

### 7.4 層 3・手動

- 層 3: 7.3 の画面の照合（補助が Unity の外から OS の画面を読む）が層 3 に当たる
- 手動: Direct Share が Sharesheet に出ること（アプリ側の `<share-target>` の宣言が要る。8.4）、API 33 以下の端末（Chooser Action が出ないこと）。手元に端末が無い

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。起こせない失敗（E-9、E-16、E-17、E-19〜E-22）は文言の変換を層 1 で押さえ、形をコードレビューで押さえる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- OS が求める許可: なし（Share に固有の権限は無い。1.8）
- 前提の OS 設定: 画面が点いていて、ロックが解けていること（補助が点け、ロックを外す。PIN のある端末では止める。補助の設計書 v5 の 4 章）。アニメーションと自動入力は補助が切る
- Chooser Action は API 34 以上の端末でだけ確かめられる（Pixel 6a は Android 16 で可）
- Sharesheet の共有先の一覧は端末に入っているアプリで変わる。テストは自分の共有先（`NTK test target`）とラベルの文言だけを押す

### 8.3 呼び出し側を止める OS の画面

Sharesheet は非同期で、呼び出し側を止めない。ただし選ばれたアプリの結果は、人が押すまで来ない。**外から押す手段は、Dialog で作ったホストの adb の補助を広げる**（補助の設計書 v5 に足す）:

- **Sharesheet の中の文言を探して押す命令**（例: `tap-in-sheet text=<label>`）。`com.android.intentresolver` のパッケージの中で文言を探し、無ければ上へスワイプして（8 回まで）探し直す（native-toolkit の `UiDriver.kt:219-234` と同じやり方）
- **Sharesheet が出ているかの命令**（`count pkg=com.android.intentresolver` のようにパッケージで数える）
- **プライバシー**: Sharesheet にはほかのアプリの共有先と連絡先が出る。**補助は、Sharesheet のパッケージが画面にある間は、失敗のときの画面の構造（`--dump-dir`）を書き出さない**（native-toolkit の UI テストの決まりと同じ）。読み取った構造は判定にだけ使い、ログにも出さない
- **テスト用の共有先**: 選ばれたアプリのテストには、押してもほかのアプリのデータに触れない共有先が要る。案は 5.1 の `<activity-alias>`（`Assets/Plugins/Android/` の下に置く。パッケージの利用者には入らない）。選ぶとテスト用の Player が `SEND` で前に戻る。**要検証**（5.5 の手順 2）: alias がマージした manifest で使えること、Unity の Activity が SEND で作り直されないこと（`singleTask` なら `onNewIntent`）。だめなときの案: テスト用の小さな Android ライブラリ（`.androidlib`）に空の Activity を置き、選ばれたらすぐ `finish()` する

補助がいない実行（Unity の窓から流すなど）では、UI を使うテストは `Assert.Ignore` になる。`verify_unity_android.sh` のゲート（カテゴリ `AndroidUiDriver` の skip が 0）は Dialog と同じ。

### 8.4 アプリ側の設定（利用者の責任）

- FileProvider: 要らない（AAR に入っている。1.8）
- Direct Share を Sharesheet に出すには、アプリの manifest に `<share-target>`（`res/xml/shortcuts.xml` と、ランチャーの Activity の `<meta-data android:name="android.app.shortcuts">`）が要る。AAR は宣言していない（1.x と同じ）。マニュアルに書く（今も書いているかは手順 10 で確かめる）
