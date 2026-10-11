# Android Share 実装計画 v2（C ABI 2.0.0 への移行）

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
| レビュー | `artifact/android/share/reviews/2026-10-11-android-share-design-review-v1.md`（サブエージェントと Codex） |
| 1.x の記録 | 1.x の設計 `artifact/android/share/designs/2026-06-21-android-share-design-v1.md`、`-v2.md`、`2026-06-27-android-share-design-v3.md`。**1.x の実機の UI テストの記録は無い**（`artifact/topics/device-verification-records/README.md`）。Dialog と同じく、移行の前に 1.x のまま流して記録する（5.5 の手順 3） |

## v1 からの主な変更

レビュー v1 の A1 を直した（サブエージェントの R1-*、Codex の C1-*）。多くは「1.x と同じ」と書いた所が 1.12.0 のコードと違っていたもの。

- **per-call の枠を読む時点を 1.x に合わせた**（R1-1）。同期に決まる結果は呼んだ時点で枠を取り出し、ネイティブの完了は受け口（Android の main）で取り出す（1.x は listener の中、dispatcher に積む前に読んでいた）。J-S5
- **選ばれたアプリを C# で照らすのをやめた**（R1-6、R1-7、C1-4）。1.x は照らしておらず、古い選択はネイティブが捨てる（1.5）。要求の ID は取り消し（F-9）のためだけに持つ。J-S12
- **Editor の `CancelPendingShareCallback` は失敗**（R1-2）。`onSelected` は Editor の判定の前に消す。J-S13
- **Chooser Action を 1.x の表示と通知に合わせた**（R1-4、C1-6、C1-7、C1-8、R1-8、R1-16）: 重複した ID も印を付けて表示し、通知では印を外す。SEND の通知は捨てる。API 33 以下ではアクションを渡さない。明らかに画像でないアイコン（先頭のバイト列）と読めない Base64 は捨てる。Base64 は 1.x と同じく寛容に解く。C# の世代で、前の Share・失敗した Share・壊れた Manager のアクションを捨てる。4.2.1、J-S17、J-S18
- **null の 1.x の振る舞いを正しく書き、例外は `ArgumentNullException` に揃えた**（R1-5、C1-9、C1-10）。J-S7、5.3
- **空白の判定を Kotlin の `isBlank` に合わせた**（R1-17）。J-S19
- **`user_data` の表に入れるのを `extern` を呼ぶ直前にし、呼べなかったら消す**（R1-18、C1-2）。5.4
- **受け口に `[MonoPInvokeCallback]` を書いた**（C1-1）。選択の文字列を `finally` で解放する（C1-5）
- リスナーの登録をロックで 1 回にした（R1-10）。`AllocBytes` を Share の Bridge に置いた（R1-9。P4）
- **テストの共有先は、このプロジェクトの既存の androidlib（`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib`。Unity の Activity に `SEND` と `<share-target>`）でアプリ自身を選ぶ**（R1-3）。Direct Share の表示も層 2b で試す
- UI の補助のプライバシーの決まりを足した（R1-12）。層 2a の継ぎ目（R1-13）と、`user_data` と `release` の順のテスト（C1-3）を足した
- 5.3 に行を足した（結果の順、U+0000、サロゲート、Base64、null、空白）
- C の指摘（R1-20、R1-21、C1-12）を直した

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
- `ShortcutManagerCompat.pushDynamicShortcut`（`setLongLived(true)`）で登録する。同じ ID の再登録は更新（`pushDynamicShortcut` の仕様。C ABI の版での端末の確かめは無い）
- 削除は `removeLongLivedShortcuts`。知らない ID の扱いは OS に任せる（確かめていない）
- **Sharesheet に出すには、アプリ側が `<share-target>` を宣言する必要がある。AAR は宣言していない**（1.x と同じ）。**このリポジトリは宣言済み**: `Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/`（`1fbc349`）が `UnityPlayerGameActivity` に `SEND`（`*/*`）の intent-filter と `android.app.shortcuts` を付け、`res/xml/shortcuts.xml` に `<share-target>`（`android.shortcut.conversation`）を持つ。マニュアルも説明している（`docs/1.11.0/share.ja.md:76-104`）

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

- 操作の結果: 操作名ごとに 1 つの枠（`Dictionary<string, Action<ShareOperationResult>?>`）。呼ぶときに上書き（null も上書き。last-registered wins）。**結果を作った時点で枠を読んで消し**、読んだ callback を dispatcher に積む（`FireOperationResult`。`AndroidShareManager.cs:399-406`。コメントは「後から同じ操作を呼んでも、この結果が別の callback を呼ばないように」）。同期の失敗（Android 以外、`pluginInstance` が null、`Call` の例外）は呼んだその場で、ネイティブの結果は Android の main の listener の中で読む。そのため、Editor で同じ操作を同じフレームに 2 回呼ぶと、それぞれの callback がそれぞれの失敗を受ける
- Kotlin は parser を含めすべてを main の上で呼んだ順に処理する（`UnityAndroidShareManager.kt:317-321`）。結果の順は呼んだ順
- `onSelected`: 1 つの枠。`ShareWithCallback` の再呼び出し、`CancelPendingShareCallback`（Editor でも、呼んですぐ消す）、`shareWithCallback` の失敗で消える。選択の結果は dispatcher の中（UnityMain）でその時点の枠を読み、1 回で消す（`FireCallbackResult`。`:422-431`）。**C# は選択を照らさない**（`ShareCallbackReceived` はいつも出る）
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
- **表示は ID の重複も含めてすべて**（`buildChooserActions`。タグ `1.12.0` の `ShareRepositoryImpl.kt:245-266`）。受け手（receiver）に登録する action の文字列だけを、空白・SEND を除いて `distinct` にした（`ShareChooserActionInputs.kt:16-24`）。そのため、同じ ID のアクションはどれを押しても同じ ID で 1 回通知され、`android.intent.action.SEND` のアクションは表示されるが、押しても通知は来ない
- Base64 として読めない・画像にできないアイコンのアクションは黙って飛ばす。Base64 は Android の `Base64.decode(..., DEFAULT)` で解いた（パディングの無い入力も受けると見られる。要確認）
- API 33 以下では、アクションの処理そのものを飛ばした（タグ `1.12.0` の `ShareRepositoryImpl.kt:226-229`）

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

`Runtime/Android/Share/AndroidShareCApi.cs`（Bridge）。`[DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]`。

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
- **受け口は Manager の private static メソッドに `[MonoPInvokeCallback(typeof(...))]` を付け**、delegate は `static readonly` に保ち、関数ポインタは `Marshal.GetFunctionPointerForDelegate` で 1 回だけ作る（IL2CPP。`Handoff`:142-143、Dialog の `AndroidDialogManager` と同じ）。受け口は 4 つ: 完了（`DoneCallback`）、`release`（`ReleaseCallback`）、アクション（`ChooserActionCallback`）、選択（`SelectionCallback`）
- Chooser Action の配列は、`ChooserAction` を `count` 個並べた 1 つのブロックを確保し、`Marshal.StructureToPtr` で書く
- 文字列の配列（`const char* const*`）は `AllocStringArray`。今は `AndroidDialogCApi` にあるのを共通部 `AndroidNativeToolkitCApi` に移す（Dialog、Share、Clipboard の `ntk_clipboard_copy_texts`、Notification の `lines` が使う。`common.md` の `Runtime/<Platform>/Common/` の条件。R1-9）
- アイコンのバイト列は `AllocBytes(byte[], List<IntPtr>)` で作る。`const uint8_t*` を取るのは `Share.h` だけなので、**Share の Bridge に置く**（P4。R1-9）。`Alloc` を使い、記録に失敗しても漏れない

### 4.2 引数の変換（公開 API は変えない）

1.x の parser と同じ結果になるように、C# が先に整える。整えた後の値だけを C に渡す。「空白」は J-S19 の判定（Kotlin の `isBlank` と同じ）。

| 入力 | C# がすること | 1.x |
|---|---|---|
| `payload` が null | `ArgumentNullException` を呼び出し側に投げる（J-S7） | `NullReferenceException` |
| `filePaths` / `ids` / `chooserActions` の配列そのものが null（`chooserActions` は null なら「無し」で、例外ではない） | `filePaths` / `ids`: `ArgumentNullException`（J-S7） | `NullReferenceException`（JSON を組むとき） |
| `chooserActions` の要素が null | `ArgumentNullException`（J-S7） | `NullReferenceException`（JSON を組むとき。coordinator の登録の後） |
| `text` が空・空白（`ShareText`、`ShareWithCallback`） | 検査の失敗 `Failed to <op>: text is required` | 同じ（parser） |
| `text` が null | 同上 | 文字列 `"null"` を共有した見込み（JSON の `null` を `getString` が `"null"` にする。要確認。5.5 の手順 3） |
| `title`、`subject`、`previewTitle`、`previewThumbnailPath` が null・空・空白 | NULL を渡す（無し） | 同じ（JSON で省いた） |
| `mimeType` が null・空・空白 | NULL を渡す（既定値: text は `text/plain`、image は `image/*`） | 同じ（parser が既定値にした） |
| `chooserActions`（`ShareText`） | 4.2.1 | |
| `chooserActions`（`ShareWithCallback`） | 渡さない（F-8 に引数が無い）。1 個以上なら今と同じ `LogWarning` | 同じ |
| `filePath` が空・空白（`ShareImage`、`ShareFile`） | 検査の失敗 `Failed to <op>: filePath is required` | 同じ |
| `filePath` が null | 同上 | `File not found: null` の見込み（要確認） |
| `filePaths` が空 | 検査の失敗 `Failed to <op>: filePaths must not be empty` | 同じ |
| `filePaths` の要素が null | 検査の失敗 `Failed to <op>: filePaths must not contain null.`（J-S8） | `File not found: null` の見込み（要確認） |
| Direct Share の `id` / `label` / `iconBase64` が空・空白 | 検査の失敗 `Failed to registerDirectShareTarget: id is required`（`label is required`、`iconBase64 is required`） | 同じ |
| 同上が null | 同上 | `id` / `label` は文字列 `"null"` で登録した見込み、`iconBase64` は `Invalid icon data for Direct Share target: <id>` の見込み（要確認） |
| Direct Share の `iconBase64` が Base64 として読めない（J-S18 の寛容な解き方で） | 検査の失敗 `Invalid icon data for Direct Share target: <id>` | 同じ文言（ライブラリの `InvalidBase64Icon`） |
| Direct Share の `category` が null・空・空白 | NULL を渡す（既定値 `android.shortcut.conversation`） | 同じ |
| `ids` が空 | 検査の失敗 `Failed to removeDirectShareTargets: ids must not be empty` | 同じ |
| `ids` の要素が null | 検査の失敗 `Failed to removeDirectShareTargets: ids must not contain null.`（J-S8） | 文字列 `"null"` を消そうとした見込み（要確認） |

文字列はすべて共通部の `AllocUtf8`（NUL 終端の UTF-8。対になっていないサロゲートは U+FFFD）で渡す。途中の U+0000 はそこで切れる（C ABI の形。5.3）。

#### 4.2.1 Chooser Action の整え方（1.x の表示と通知に合わせる）

`AndroidShareCApi.NormalizeActions(ChooserActionPayload[], int sdkInt)` が、C に渡す組の並びと、通知で印を外す規則を返す（純粋な関数。層 1）。

1. **端末が API 33 以下なら、アクションを 1 つも渡さない**（1.x はアクションの処理そのものを飛ばした。C ABI の版のライブラリは画像を読んでから SDK を見るので、渡すと読めないアイコンで Share 全体が失敗しうる）。`sdkInt` は Manager が `Initialize()`（UnityMain）で 1 回読んで覚える（`android.os.Build$VERSION.SDK_INT`。J-S20）
2. `label`・`iconBase64`・`intentAction` のどれかが空・空白（J-S19）のアクションを捨てる（1.x の parser と同じ。表示もされない）。null の値も同じく捨てる（1.x は文字列 `"null"` で表示した見込み。5.3）
3. `iconBase64` を J-S18 の寛容な解き方で解く。読めなければそのアクションを捨てる（1.x のライブラリと同じ）
4. 解いたバイト列の先頭が、PNG・JPEG・WebP・GIF・BMP のどれの印（マジックナンバー）にも当たらなければ、そのアクションを捨てる（1.x の「画像にできないものは飛ばす」に近づける。印は当たるが中身が壊れている画像は、Share 全体の失敗になる。5.3）
5. ID を決める: C に渡す文字列の形（U+0000 で切り、対になっていないサロゲートを U+FFFD に置き換えた後）で比べ、空になれば捨てる。**同じ ID がすでにあれば、2 つ目以降には印を付けて別の ID にする**（`<intentAction>#ntk-dup-<n>`。`n` は 2 から）。C ABI は重複を入口で拒むが、1.x は重複も表示していた（R1-4、C1-6）
6. 通知（4.3）では、ID の末尾の `#ntk-dup-<数字>` を外してから出す（1.x と同じく、どれを押しても元の `intentAction`）。**元の `intentAction` が `android.intent.action.SEND` なら通知を捨てる**（1.x は通知しなかった）
7. 6 個以上なら、今と同じ `LogWarning` を出す（切り詰めない。表示の上限は OS が決める）

保てないもの（5.3）: 印は当たるが中身が読めない画像は、Share 全体が `INVALID_CHOOSER_ACTION` で失敗する。元の `intentAction` が `#ntk-dup-<数字>` で終わる ID は、通知でその部分が外れる。

### 4.3 結果と完了

- 開く操作（F-1〜F-5、F-8）: 戻り値が `NONE` なら受け付けた。完了の `error` が `NONE` なら成功（**Sharesheet が開いた、という意味。1.x の「起動できた」と同じ**）、それ以外は失敗
- Direct Share（F-6、F-7）: 戻り値が結果
- 取り消し（F-9）: 戻り値は見ない（ログだけ）。1.x と同じく、Android の上ではいつも成功を返す（SH-14）。Editor・Android 以外では失敗（J-S13）
- 選ばれたアプリ（F-11）: 照らさずに出す（J-S12）。`ShareCallbackResult("shareWithCallback", packageName)`（`package_name` が NULL なら `null`）
- Chooser Action（F-10）: 4.2.1 の手順 6 の後、J-S17 の世代が今のもののときだけ、`ShareChooserActionResult(actionId)` を coordinator に渡す

**どの結果も、取り出し口 `AndroidNativeCallbackQueue` を通して、後のフレームに出す。呼び出しの中ではイベントを出さない**（J-S2。1.x と同じ）。per-call の枠を取り出す時点は J-S5。

### 4.4 エラーの文言とログ

`<op>` は 2.1 の `Operation*` の値。

| 原因 | 時点 | `ErrorMessage` | ログ |
|---|---|---|---|
| C# の検査（4.2 の表） | 呼び出し | 4.2 の文言（1.x と同じ。要素の null だけ新しい） | `LogWarning` |
| Editor・Android 以外（J-S3） | 呼び出し | `<op> could not be started.` | `LogWarning("<op> can only be called on Android.")`（1.x と同じ） |
| ネイティブが使えない（共通部の `NativeUnavailable`） | 呼び出し | `Failed to <op>: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).` | `LogWarning` |
| `NOT_INITIALIZED`（入口・完了） | 呼び出し / 完了 | `Failed to <op>: the native library is not initialized yet.` | `LogWarning` |
| `INVALID_PARAMETER` / `NOT_SUPPORTED` / `INVALID_CHOOSER_ACTION`（入口） | 呼び出し | `Failed to <op>: the request was rejected (<名前>).` | `LogError`（C# の組み立ての誤り。4.2.1 の整えの後では起きない） |
| `INVALID_PARAMETER`（完了・Direct Share。Kotlin の `IllegalArgumentException`） | 完了 / 同期 | `Failed to <op>: the request was rejected (INVALID_PARAMETER).` | `LogWarning` |
| `EMPTY_CONTENT` | 呼び出し / 完了 | `Failed to <op>: text is required`（1.x の parser の文言。J-S19 の検査が先に拒むので、ふつうは来ない） | `LogWarning` |
| `EMPTY_FILE_LIST` | 呼び出し | `Failed to <op>: filePaths must not be empty`（C# が先に拒む） | `LogWarning` |
| `EMPTY_ID_LIST` | 呼び出し | `Failed to removeDirectShareTargets: ids must not be empty`（同上） | `LogWarning` |
| `NO_SHARE_TARGET` | 完了 | `No app available to handle this share request.`（1.x と同じ） | `LogWarning` |
| `FILE_NOT_FOUND` | 完了 | `File not found: <path>`。1 つのパスの操作はそのパス。複数のときは、存在しない最初のパス（J-S9） | `LogWarning` |
| `ILLEGAL_FILE_ACCESS` | 完了 | `File cannot be shared: <path>. Ensure the file is in a supported directory.`。1 つのパスの操作はそのパス。複数のときは全部のパスを `, ` でつないだもの（J-S9） | `LogWarning` |
| `INVALID_MIME_TYPE` | 完了 | `Invalid MIME type: <渡した値>`（C# が空白を NULL にするので、ふつうは来ない） | `LogWarning` |
| `DIRECT_SHARE_REGISTRATION_FAILED` | 同期 | `Failed to register Direct Share target: the system did not accept the shortcut.`（理由は C ABI から来ない。5.3） | `LogWarning` |
| `INVALID_ICON` | 同期 | `Invalid icon data for Direct Share target: <id>`（1.x と同じ） | `LogWarning` |
| `INVALID_CHOOSER_ACTION`（完了） | 完了 | `Failed to shareText: a chooser action icon could not be read as an image.`（新しい。5.3） | `LogWarning` |
| `NOT_FOREGROUND` | 完了 | `Failed to <op>: the app is not in the foreground.`（新しい。5.3） | `LogWarning` |
| `OUT_OF_MEMORY` / `UNKNOWN` / 想定外の値 | 呼び出し / 完了 | `Failed to <op>: <名前>.` | `LogWarning` |
| C# の例外（変換・確保の失敗など） | 呼び出し | `ex.Message`（1.x の `Call` の例外と同じ扱い） | `LogError` |
| 受け口の中の例外 | - | イベントは出ない（その完了は失われる） | `LogError` |

`<名前>` はエラーの列挙の名前。名前と文言への変換は純粋な関数にし、層 1 で固める。

**ログ（J-S10）**: JSON を丸ごと出すのをやめる。本文（`text`）と `subject` は長さだけを出す（native-toolkit の `logSafeDescription` と同じ考え方。UI の補助が logcat を読むので、値が成果物に残りうる）。題名、MIME、パス、ID、ラベル、カテゴリはそのまま出す（1.x もライブラリも出している。秘密ではない）。アイコンはバイト数だけ。選ばれたアプリのパッケージ名と、押されたアクションの ID はそのまま出す。Editor の `Initialize()` は `Debug.Log`（`LogWarning` にしない。毎回の `Awake` で警告を出さない）。

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | 置き場所 | ガード | 内容 |
|---|---|---|---|---|
| 新規作成 | `AndroidShareCApi.cs` | `Runtime/Android/Share/` | 型と純粋な関数は `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport` は `UNITY_ANDROID && !UNITY_EDITOR` | Bridge。4.1 の構造体・delegate・`extern`、`AllocBytes`、入れ子の `Operation` 列挙（9 個。`Operation*` 定数と対応）、エラー定数 0〜16、`ShareCall`（1 回の呼び出しの記録: 操作、パス、MIME、Direct Share の ID、`ShareText` の世代）、4.2 の検査と整え（`IsBlank`〈J-S19〉、`CheckText`、`CheckPath`、`CheckPaths`、`CheckIds`、`CheckDirectTarget`、`DecodeBase64Leniently`〈J-S18〉、`LooksLikeImage`、`NormalizeActions`、`ToPublicActionId`、`NullIfBlank`）、構造体の組み立て（`BuildTextContent`、`BuildActions`、`BuildDirectTarget`）、4.4 の文言（`ErrorName`、`FailureMessage`、`NativeUnavailableMessage`、`MissingPath`） |
| 既存変更 | `AndroidShareManager.cs` | `Runtime/Android/Share/` | 型は `UNITY_ANDROID \|\| UNITY_EDITOR`（今は `UNITY_ANDROID`）。受け口・delegate を保つフィールド・関数ポインタは `UNITY_ANDROID && !UNITY_EDITOR` | `AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher` を消し、`AndroidShareCApi` を呼ぶ。公開 API・既定値・文言は 2 章のとおり保つ（5.3 を除く）。5.2 の J-S1〜J-S20。受け口の本体（表を読む、枠を取り出す、世代を見る、取り出し口に入れる）は、ガードの外の `internal static` の関数にし、層 2a から呼べるようにする（`OnDoneCore(IntPtr userData, int error)`、`OnReleaseCore(IntPtr userData)`、`OnChooserActionCore(string actionId)`、`OnSelectionCore(ulong requestId, string? packageName)`。C1-3、R1-13）。テストの継ぎ目 `internal void SetPendingSelectedForTests(Action<ShareCallbackResult>?)`、`internal static int PendingCallCountForTests`。static の状態（`user_data` の表、世代、取り消し用の ID、登録の印、`sdkInt`）は `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で戻す（Editor でドメインを再読み込みしない設定のため） |
| 既存変更 | `AndroidNativeToolkitCApi.cs` | `Runtime/Android/Common/` | 今のまま | `AllocStringArray` を `AndroidDialogCApi` から移す（4.1） |
| 既存変更 | `AndroidDialogCApi.cs`、`AndroidDialogCApiTests.cs` | `Runtime/Android/Dialog/`、`Tests/Runtime/` | 今のまま | `AllocStringArray` を共通部の呼び出しに替える（振る舞いは変えない） |
| 既存変更 | `AndroidNativeToolkitCApiTests.cs` | `Tests/Runtime/` | 今のまま | 移した `AllocStringArray` のテスト（`AndroidDialogCApiTests` から移す） |
| 既存変更 | `AndroidShareJsonBuilder.cs` | `Runtime/Android/Share/` | 今のまま | 中身は変えない。Manager から使わなくなる。XML コメントに「2.0.0 では Manager は使わない。4 機能の移行の後に消す（親課題 README の段 4）」と書く（J-S11） |
| 新規作成 | `AndroidShareCApiTests.cs` | `Tests/Runtime/` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（7.1） |
| 新規作成 | `AndroidShareManagerEditorTests.cs` | `Tests/PlayMode/` | `UNITY_EDITOR` | 層 2a（7.2） |
| 新規作成 | `AndroidSharePlayerTests.cs` | `Tests/PlayMode/` | 外 `UNITY_ANDROID`、中 `!UNITY_EDITOR` | 層 2b の UI を使うテスト（カテゴリ `AndroidUiDriver` をクラスに付ける。7.3） |
| 新規作成 | `AndroidShareManagerPlayerTests.cs` | `Tests/PlayMode/` | 同上 | 層 2b の UI を使わないテスト（Direct Share の登録と削除、Sharesheet を開かない失敗、時点。補助がいない実行でも走る。7.3） |
| 既存変更 | `android_ui_driver.py`、`AndroidUiDriver.cs` | `scripts/`、`Tests/PlayMode/` | - | 8.3 の命令とプライバシーの決まり |
| 既存変更 | `common.md`、`testing.md` | `agent-rules/coding-rules/` | - | `common.md`: Android の節に「呼び出しごとに `user_data` を表で付ける形（Share）と、種類だけを渡す形（Dialog）」、取り出し口の節の「取り出した時点の `Instance` に出す」に Share の例外（J-S4）。`testing.md`: 3 節のガードの表（`AndroidShareManager` を B 群から A 群へ）、7 節の Android の Share の層 |
| 既存変更 | `README.md` | `artifact/topics/android-c-abi-2/` | - | 決定 J-9 に Share の例外（J-S4。R1-20） |
| 非変更 | Payload 8 型（XML コメントを含む。SEND の通知は 1.x と同じく来ないので、`AndroidSharePayloads.cs:19-26` の記述は正しいまま。C1-8）、結果 3 型、`ShareChooserActionCallbackCoordinator`（Manager は `dispatch` に「その場で走らせる」を渡す。`Fire` は取り出し口の中、UnityMain で呼ぶので）、`AndroidShareManagerExampleController`（画面とログに本文は出していない）、`AndroidShareResultTests`、`AndroidShareJsonBuilderTests`、`AndroidShareChooserActionCallbackCoordinatorTests`、`AndroidShareSampleSceneWiringTests`、`Runtime/Resources/UI/Android/Share/*`、`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/*`（テストの共有先として使う。変えない） | - | - | |

Manager を `|| UNITY_EDITOR` にするのは、層 2a で Editor の契約（J-S3）と配送（J-S2、J-S4、J-S5）を確かめるため。

### 5.2 判断

| # | 内容 | 決定 |
|---|---|---|
| J-S1 | 完了と呼び出しの対応付け | **呼び出しごとに `user_data` を付ける**（`Handoff` 4.3）。Manager の static な `ConcurrentDictionary<long, ShareCall>` に、連番のキーで、**`extern` を呼ぶ直前に**入れ、キーを `IntPtr` にして渡す。完了の受け口（Android の main）は表を**読むだけ**で、引いた `ShareCall` と `error` から作った処理を取り出し口に入れる（キーのまま積まない。完了の直後、同じ main のメッセージの中で `release` が来て表から消えるため）。表から消すのは `release` の受け口と、`extern` を呼べなかったとき（呼ぶ前の例外）の `catch` だけ。入口で拒まれたときは `release` が戻る前に来て消し、Manager が手元の `ShareCall` で失敗を出す。Dialog（J-D1）のように種類だけを渡す形にしないのは、文言にパス（4.4）を入れるのと、アクションの世代（J-S17）を完了に結ぶのに、呼び出しごとの記録が要るため |
| J-S2 | 結果の時点 | **すべて取り出し口を通して後のフレームに出す。** C# の検査の失敗、Editor、入口の拒否、同期の Direct Share もそうする。1.x はどの結果も Kotlin と dispatcher を通って後のフレームに出ており、`Show*` の中で同期にイベントを出す Dialog とは違う。呼んだスレッドがどれでも、イベントは UnityMain で出る。**操作をまたいだ結果の順は、1.x（Kotlin の main で呼んだ順）と変わりうる**（同期に決まる失敗が、先に呼んだ開く操作の完了より先に出る。5.3） |
| J-S3 | Editor・Android 以外 | 1.x と同じく `LogWarning("<op> can only be called on Android.")` を出し、失敗 `<op> could not be started.` を後のフレームに出す（`CancelPendingShareCallback` も同じく失敗。R1-2）。1.x は Android ターゲットの Editor で同じだった。ガードに `UNITY_EDITOR` を足したので、ほかのターゲットの Editor でも同じになる（型が無かった所に型ができる。5.3） |
| J-S4 | 結果を誰に出すか | **取り出した時点で生きている Manager（`_instance`）に出す。いなければ捨てる（ログだけ）。** 1.x は Manager の `OnDestroy` でブリッジのリスナーを外し、その後の結果を捨てていた。親課題の J-9 と Dialog（J-D2）の「新しい `Instance` を作って出す」から外れる（Share は 1.x の振る舞いがはっきりしており、終了の間に GameObject を作り直さない）。この例外を親課題の README と `common.md` に書く（5.1）。Manager が無効（`enabled = false`）でも届く（取り出し口は別の GameObject） |
| J-S5 | per-call の枠 | 1.x の規則（2.2）を保つ: 操作名ごとの枠に、呼ぶときに上書き（null も）。**枠を取り出す時点も 1.x に合わせる**: 同期に決まる結果（C# の検査、Editor、ネイティブが使えない、入口の拒否、C# の例外、同期の Direct Share）は、呼んだスレッドで結果を作るときに取り出し、取り出した callback を閉じ込めた処理を取り出し口に入れる。ネイティブの完了は、完了の受け口（Android の main）で取り出す（1.x の listener と同じ時点）。枠は `lock` で守る（呼んだスレッド・Android の main・UnityMain が触る。ロックの中では辞書の読み書きだけをし、利用者のコードを走らせない。1.x の競合を直す）。順は 1.x と同じく、共通イベント → per-call。共通イベントの購読者の例外は捕まえない（1.x と同じ。`event-subscriber-isolation` の範囲） |
| J-S6 | 公開の `Initialize()` とリスナー | 残す。中身は、ネイティブが使えるかを確かめ、`sdkInt` を読み（J-S20）、リスナー 2 つ（F-10、F-11）が未登録なら登録する。**登録は `lock` の中で 1 回だけ行い、成功したハンドルを static に持つ**（同時に 2 回登録すると、イベントが 2 回届く。R1-10）。リスナーは**プロセスで 1 回だけ**登録し、外さない（static の受け口、`user_data` と `release` は NULL）。登録に失敗したら `LogWarning` を出し、次の `Initialize()`・`ShareText`・`ShareWithCallback` でやり直す。`Awake` から呼ぶ（UnityMain）。外さない理由: 外した直後の配送と `release` の時点の扱い（P1:153）を持ち込まずに済み、Manager を作り直しても登録の前に起きたイベントを失わない。Manager がいない間の結果は J-S4 で捨て、作り直した後の前の Share のアクションは J-S17 で捨てる |
| J-S7 | null の引数 | `payload`、`filePaths` / `ids` の配列そのもの、`chooserActions` の要素が null なら、`ArgumentNullException` を呼び出し側に投げる（呼び出し側の誤り。1.x も呼び出し側に例外が出た〈`NullReferenceException`〉。型だけが変わる。5.3）。検査は per-call の枠や coordinator に登録する前に行う（1.x は `chooserActions` の要素の null のとき、coordinator に登録した後で投げた。5.3）。そのほかの null は 4.2 の表のとおり |
| J-S8 | 配列の要素の null | 検査の失敗 `Failed to <op>: filePaths must not contain null.` / `... ids must not contain null.`。C ABI は入口で `INVALID_PARAMETER` にするので、C# が先に言う（Dialog の J-D12 と同じ考え方） |
| J-S9 | 文言のパス | 1 つのパスの操作（`shareImage`、`shareFile`）は、`ShareCall` のパスを入れる（1.x と同じ文言）。複数のとき、`FILE_NOT_FOUND` は UnityMain で「`File.Exists(p) \|\| Directory.Exists(p)` が偽の最初のパス」（Kotlin の `File.exists()` はディレクトリでも真。R1-19。見つからなければ全部のパスを `, ` でつなぐ）、`ILLEGAL_FILE_ACCESS` は C# から範囲を判定できないので、全部のパスを `, ` でつなぐ（5.3）。存在の確かめは文言のためだけに使い、検査には使わない |
| J-S10 | ログ | 4.4 の末尾のとおり。JSON を出すのをやめ、本文と `subject` は長さだけ |
| J-S11 | `AndroidShareJsonBuilder` | 公開のまま残し、Manager からは使わない（消すと公開 API が壊れる）。4 機能の移行の後に JSON の層をまとめて消す（親課題 README の段 4、移行ガイド）。`[Obsolete]` は付けない（利用者とテストに警告が出るため。消す版で付ける） |
| J-S12 | 選ばれたアプリ | **C# では照らさない**（1.x と同じ。R1-6、R1-7、C1-4）。ネイティブが「直前に開いた選択の待ちの分」だけを届け、古い Chooser・取り消した待ち・置き換えた待ちの選択を捨てる（1.5）。選択の受け口は、`package_name` を写して解放し（`finally` で `ntk_string_free`。C1-5）、処理を取り出し口に入れる。取り出し口の中で `ShareCallbackReceived` → その時点の `onSelected`（1 回で消す）の順に出す（1.x の `FireCallbackResult` と同じ）。要求の ID は**取り消し（F-9）のためだけに**持つ: `ShareWithCallback` は F-8 の戻り値が `NONE` のときだけ、`out` の ID を static のフィールドに入れる（`lock` の中。今の値より大きいときだけ置き換える。拒まれたときは前の値のまま）。複数のスレッドから同時に `ShareWithCallback` を呼ぶと、ネイティブの待ちと取り消しの ID が食い違いうる（ID を付ける順と main に積む順は一致しない）。XML コメントと 5.3 に書く |
| J-S13 | `CancelPendingShareCallback` | まず `onSelected` を消す（1.x と同じく、Editor の判定より前）。Editor・Android 以外・ネイティブが使えないときは、1.x と同じく失敗 `cancelPendingShareCallback could not be started.`（R1-2。ネイティブが使えないときは 4.4 の文言）。Android の上では、取り消し用の ID が 0 でなければ F-9 を呼び、ID を 0 にし、1.x と同じくいつも成功を後のフレームに出す（SH-14） |
| J-S14 | `ShareWithCallback` の失敗 | 1.x と同じく `onSelected` を消す（入口の拒否と完了の失敗の両方。結果を作るときに、per-call の枠と同じ時点で）。取り消し用の ID は J-S12 のとおり（拒まれたときは前の値のまま） |
| J-S15 | `OnDestroy` | per-call の枠、`onSelected`、coordinator（`Clear()`）を空にし、取り消し用の ID があれば F-9 で取り消し（1.x の `clearShareOperationListener` が待ちを外したのと同じ）、アクションの世代を進める（J-S17。1.x の `clearShareChooserActionListener` が受け手を外したのと同じ。R1-11）。リスナーは外さない（J-S6） |
| J-S16 | 呼ぶスレッド | 1.x と同じく、どのスレッドから呼んでもよい（Dialog の R3-9 と同じ。`AndroidNativeCallbackQueue.EnsureCreated()` は UnityMain の外では何もしない）。結果はいつも UnityMain で出る（J-S2）。`EnsureCreated()` は、引数の null の検査の直後、ほかのどの結果を積むよりも前に呼ぶ（R1-15） |
| J-S17 | Chooser Action の世代 | 1.x は新しい `shareText` のたびに前の受け手を外し、起動に失敗したら新しい受け手も外した（`ShareChooserActionReceiverRegistry.kt:43-64`、`UnityAndroidShareManager.kt:155-166`）。2.0.0 のライブラリは、アイコンが読めないとき世代を進める前に失敗し、開く前の `NOT_FOREGROUND` でも世代は進まないので、前の Share のアクションが押せるまま残る（C1-7）。そこで C# が static な世代を持つ: `ShareText` を呼ぶたびに「最新の世代」を 1 進め（`Interlocked`）、`ShareCall` に入れる。完了の受け口が成功を受けたとき、その呼び出しの世代が最新なら「生きている世代」をそれにする。失敗なら何もしない。アクションの受け口は「生きている世代 == 最新の世代」のときだけ通知する。`OnDestroy` は最新の世代を 1 進める。これで、新しい `ShareText` を呼んだ後・失敗した後・Manager を壊した後は、次の `ShareText` が成功するまで前のアクションを捨てる（1.x と同じ） |
| J-S18 | Base64 | 1.x は Android の `Base64.decode(..., DEFAULT)` で解いた。.NET の `Convert.FromBase64String` はパディングの無い入力を拒むので、`DecodeBase64Leniently` で空白（改行を含む）を除き、`=` で長さを 4 の倍数にしてから解く（R1-8）。1.x がどこまで受けたかは 5.5 の手順 3 で確かめ、違えば合わせる |
| J-S19 | 空白の判定 | `IsBlank` を Kotlin の `isBlank`（`Character.isWhitespace` か `Character.isSpaceChar`）と同じにする: Unicode の Zs・Zl・Zp と、U+0009〜U+000D、U+001C〜U+001F。.NET の `IsNullOrWhiteSpace` は U+0085 を空白とし U+001C〜U+001F を空白としないので使わない（R1-17）。1.x の parser と同じ判定になる |
| J-S20 | 端末の API レベル | `Initialize()`（UnityMain）で `new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT")` を 1 回読んで static に覚える（Share で `AndroidJavaClass` を使うのはここだけ）。読めなかったら 34 とみなし、`LogWarning` を出す（アクションを渡す側に倒す。1.x の多くの端末と同じ）。`Initialize()` の前に別のスレッドから `ShareText` を呼んだときは、そのスレッドで読む（Unity の JNI は呼んだスレッドをアタッチする） |

### 5.3 既知の差分（意図したもの）

XML コメントに書き、実装結果ファイル経由で次の版のマニュアル（移行ガイド）へ引き継ぐ。「見込み」の行は、1.x の実測（5.5 の手順 3）で直す。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 後ろから呼ぶ | OS が黙って止めた見込み（起動は成功を返した。要確認） | `Failed to <op>: the app is not in the foreground.`。前面の判定は Sharesheet が出ている間も「前面」（1.9） |
| 操作をまたいだ結果の順（J-S2） | 呼んだ順（Kotlin の main） | 同期に決まる失敗（C# の検査、Editor など）が、先に呼んだ開く操作の完了より先に出ることがある。同じ操作の per-call の対応は 1.x と同じ（J-S5） |
| 画像として読めない Chooser Action のアイコン | そのアクションを黙って飛ばした | 先頭のバイト列が画像の印に当たらないものは飛ばす（同じ）。印は当たるが中身が壊れている画像は、Share 全体が失敗 `Failed to shareText: a chooser action icon could not be read as an image.` |
| `#ntk-dup-<数字>` で終わる `intentAction` | そのまま通知した | 通知でその部分が外れる（4.2.1 の印と区別できない） |
| 古い Chooser で選んだアプリ | 届いた（不具合。SH-13） | 届かない |
| 複数のスレッドから同時に `ShareWithCallback`（J-S12） | 受け手は後に登録した方（Kotlin の main の順） | 取り消しの ID が、ネイティブの待ちと食い違うことがある（取り消しが効かない） |
| 複数のファイルの `FILE_NOT_FOUND` / `ILLEGAL_FILE_ACCESS` の文言（J-S9） | 原因のパス | 存在しない最初のパス / 全部のパス |
| Direct Share の登録の失敗の文言 | `Failed to register Direct Share target: <理由>`（`push_failed` か例外の文言） | `Failed to register Direct Share target: the system did not accept the shortcut.` |
| `SecurityException` の文言 | `Security restriction while executing <op>: ...` | 来ない（C ABI は `UNKNOWN` に写す）。`Failed to <op>: UNKNOWN.` |
| そのほかの例外の文言 | 例外の文言を含む | 4.4 の固定の文言 |
| `payload`・配列・`chooserActions` の要素が null（J-S7） | `NullReferenceException`（要素の null のときは coordinator に登録した後） | `ArgumentNullException`（何も登録しない） |
| 必須の文字列が null（本文、`filePath`、Direct Share の `id` / `label` / `iconBase64`、配列の要素） | 文字列 `"null"` として扱った見込み（`"null"` を共有、`File not found: null`、`"null"` の名前で登録など。要確認） | 検査の失敗（4.2 の文言） |
| Chooser Action の `label` / `iconBase64` / `intentAction` が null | `label` は `"null"` で表示した見込み（要確認） | そのアクションを捨てる |
| 空白の判定（J-S19） | Kotlin の `isBlank` | 同じ（差分なし。.NET の判定にしない） |
| パディングの無い Base64 のアイコン（J-S18） | 受けた見込み（要確認） | 受ける（寛容に解く） |
| 文字列の途中の U+0000 | JSON の `\u0000` でそのまま運んだ | そこで切れる（本文が U+0000 で始まると `text is required`） |
| 対になっていないサロゲート | そのまま運んだ | U+FFFD に置き換わる |
| Editor（J-S3） | Android ターゲットのときだけ型があり、失敗のイベント | どのターゲットでも型があり、失敗のイベント |
| ログ（J-S10） | JSON を丸ごと出した（本文、パス、Base64 のアイコン） | 本文と `subject` は長さ、アイコンはバイト数 |
| `Initialize()`（J-S6） | ブリッジを取り、リスナーを登録した | リスナーを登録し、API レベルを読む。何度呼んでもよい |
| 結果が Manager の破棄の後に来る（J-S4） | ブリッジのリスナーを外していたので捨てた | 捨てる（同じ） |
| per-call の枠の競合（J-S5） | ロックが無かった | ロックで守る |
| 32 ビット（ARMv7）と Mono | 動いた（ブリッジは Java） | ビルドが止まる（親課題 J-10） |
| `AndroidShareJsonBuilder`（J-S11） | Manager が使った | 使わない。後の版で消す |

### 5.4 呼び出しの流れと契約（`ShareFiles` の例）

1. `payload` か `filePaths` が null なら `ArgumentNullException`（J-S7）
2. `AndroidNativeCallbackQueue.EnsureCreated()`（J-S16）
3. ログ（パスの件数とパス）。per-call の枠に `onResult` を入れる（`lock`。上書き）
4. 4.2 の検査。失敗なら `LogWarning` を出し、Manager の private な `PostImmediateResult(op, error)` で結果を出して終わる（`lock` の中で枠を取り出し、取り出した callback を閉じ込めた処理を取り出し口に入れる。J-S5）
5. Editor・Android 以外なら、`LogWarning` と `<op> could not be started.` を `PostImmediateResult` で出して終わる（J-S3）
6. `EnsureNativeAvailable()` が `Available` でなければ、4.4 の失敗を `PostImmediateResult` で出して終わる
7. `ShareCall`（操作、パス）を作る。確保の一覧を空に作り、`AllocStringArray` で配列を作る
8. Manager の private static な `AddCall(ShareCall)` で `user_data` の表に入れ（キーを返す）、すぐに `ntk_share_files(paths, count, s_donePointer, key, s_releasePointer)` を呼ぶ。完了と `release` は、この呼び出しが戻る前に Android の main で来うる
9. `finally` で確保したものを全部解放する（ライブラリは呼び出しの間に写す）
10. 戻り値が `NONE` 以外なら、4.4 の失敗を `PostImmediateResult` で出す（`release` はもう来ていて、表から消えている）。C# の例外は `ex.Message` の失敗にし、`extern` を呼べなかった例外なら、Manager の private static な `RemoveCall(long)` で表からキーを消す（`release` は来ないので。C1-2、R1-18）
11. 完了の受け口（Android の main。`OnDoneCore`）: 表から key を**読み**、`lock` の中で枠を取り出し、`Post(() => 生きている Manager の RaiseOperation(call, error, callback))` を入れる。`ShareText` の成功なら世代を進める（J-S17）。`shareWithCallback` の失敗なら `onSelected` を消す（J-S14）
12. `release` の受け口（`OnReleaseCore`。Android の main か、入口の拒否なら呼んだスレッド）: 表から key を消す
13. `RaiseOperation`（UnityMain）: 結果を作り、`ShareOperationCompleted` → 閉じ込めた per-call の順に呼ぶ

```csharp
string? error = null;
long key = 0;
bool called = false;
var allocated = new List<IntPtr>();
try
{
    try
    {
        IntPtr paths = AndroidNativeToolkitCApi.AllocStringArray(filePaths, allocated);   // 7
        key = AddCall(call);                                                             // 8
        called = true;
        int code = AndroidShareCApi.ntk_share_files(paths, (UIntPtr)filePaths.Length, s_donePointer, (IntPtr)key, s_releasePointer);
        if (code != AndroidShareCApi.ErrorNone) error = AndroidShareCApi.FailureMessage(Operation.ShareFiles, code, call);
    }
    finally { AndroidNativeToolkitCApi.FreeAll(allocated); }                              // 9
}
catch (Exception e)
{
    error = e.Message;
    if (key != 0 && !called) RemoveCall(key);                                            // 10
}
if (error != null) PostImmediateResult(Operation.ShareFiles, error);                     // 10
```

（`called` は `extern` を呼ぶ直前に立てる。`extern` の中で例外が出た〈`DllNotFoundException` など〉ときは、呼べていないので `release` は来ない。そのため、呼べなかった例外かどうかは `called` ではなく例外の型〈共通部の `IsNativeUnavailable`〉で見て、その場合も消す。実装で 1 つの関数にまとめる）

- 選択の受け口（Android の main、static、`[MonoPInvokeCallback]`）: `try { string? pkg = null; if (package_name != 0) { try { pkg = ReadString(package_name); } finally { ntk_string_free(package_name); } } OnSelectionCore(requestId, pkg); } catch { LogError }`
- アクションの受け口: `try { string id; try { id = ReadString(action_id); } finally { ntk_string_free(action_id); } OnChooserActionCore(id); } catch { LogError }`。`OnChooserActionCore` は 4.2.1 の手順 6 で ID を戻し（SEND なら捨てる）、J-S17 の世代を見て、取り出し口に入れる
- **どのスレッドでも完了も `release` も待たない**（親課題 A-4）

### 5.5 実装順

各段階の終わりに `scripts/verify_unity_android.sh`（EditMode、Editor の PlayMode、実機の IL2CPP、パッケージの利用者の条件）を流す。

1. UI テスト計画 v1（7.3 に番号を振る）をこの設計のレビューの後に作る
2. **UI の補助を Share に広げる**（8.3）。Sharesheet の中の文言を探して押す命令、Sharesheet が出ているかを見る命令、プライバシーの決まり。そのうえで次を確かめる（要検証）: Sharesheet にこのアプリ自身（既存の androidlib の `SEND`）が出ること、何番目か、選ぶと Player が前に戻ること（`singleTask` で `onNewIntent`）。Direct Share の共有先が Sharesheet の上の段に出るか
3. **1.x のまま** `AndroidSharePlayerTests` と `AndroidShareManagerPlayerTests` のうち 1.x で流せるもの（UI テスト計画の「1.x」の列）を書いて流し、結果を記録する。推定と書いた所（5.3 の「見込み」の行: 後ろから呼ぶ、null の各項目、パディングの無い Base64。2.3 の重複した ID と SEND のアクションの表示と通知。1.6 の表示の上限）を実測に置き換え、違えば設計書の新しい版にする
4. 共通部（`AllocStringArray` の移動）と Dialog の呼び出しの差し替え。Dialog のテストが同じに通ること
5. `AndroidShareCApi` の構造体と純粋な関数、層 1
6. `AndroidShareCApi` の `extern`
7. `AndroidShareManager` の置き換え（J-S1〜J-S20）と層 2a
8. 2.0.0 の列のテスト（C ABI にしか無いもの）を足し、全体を流す。1.x の列と比べ、違いが 5.3 の表にあることを確かめる
9. `common.md`、`testing.md`、親課題の README
10. 実装結果ファイル（`artifact/android/share/results/`）に 5.3 と 4.4、UI テスト計画の 2 列の結果を書き写す

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | `payload`・`filePaths` / `ids` の配列・`chooserActions` の要素が null | C# | `ArgumentNullException`（呼び出し側。何も登録しない） | 2a |
| E-2 | 必須の値が null・空・空白（本文、`filePath`、`filePaths`、`ids`、Direct Share の `id` / `label` / `iconBase64`） | C# | 後のフレーム、1.x の文言、`LogWarning` | 1（検査の純粋な関数、`IsBlank`）、2a（イベントが呼び出しの中で出ず、次のフレームに 1 回）、2b |
| E-3 | 配列の要素の null（J-S8） | C# | 後のフレーム | 1、2a |
| E-4 | Direct Share の `iconBase64` が Base64 でない | C# | 後のフレーム、`Invalid icon data for Direct Share target: <id>` | 1（`DecodeBase64Leniently`）、2b |
| E-5 | Chooser Action の整え（捨てる、重複に印、画像の印が無い、API 33 以下、SEND の通知を捨てる、印を外す） | C# | 失敗にしない | 1（`NormalizeActions`、`ToPublicActionId`）、2b（表示されるラベルと通知の ID） |
| E-6 | Editor・Android 以外（J-S3） | C# | 後のフレーム、`<op> could not be started.`（取り消しも） | 2a（9 操作） |
| E-7 | ネイティブが使えない | C# | 後のフレーム、4.4 | 1（例外の分類と版、文言） |
| E-8 | 入口の `INVALID_PARAMETER` / `NOT_SUPPORTED` / `INVALID_CHOOSER_ACTION` | ネイティブ（入口） | 後のフレーム、`LogError` | 1（文言）。正しい実装では起きない。構造体の位置は 1 で固定、ID の一意は 1 の `NormalizeActions` で固定 |
| E-9 | `NOT_INITIALIZED` | ネイティブ | 後のフレーム | 1（文言）。起こせない |
| E-10 | `NOT_FOREGROUND` | ネイティブ（完了） | 後のフレーム | 1（文言）、2b（試す。Dialog の AD-27 と同じ形） |
| E-11 | `FILE_NOT_FOUND`（1 つ・複数） | ネイティブ（完了） | 後のフレーム、パスを含む文言 | 1（文言と `MissingPath`。ディレクトリを含む）、2b |
| E-12 | `ILLEGAL_FILE_ACCESS`（1 つ・複数） | ネイティブ（完了） | 同上 | 1、2b（`/system/etc/hosts` のような範囲外の実在のファイル） |
| E-13 | `INVALID_CHOOSER_ACTION`（印は当たるが壊れた画像のアイコン） | ネイティブ（完了） | 後のフレーム | 1（文言）、2b（PNG の印だけのバイト列） |
| E-14 | `EMPTY_CONTENT`（C# と Kotlin の空白の判定がずれたとき） | ネイティブ（完了） | 同上 | 1（文言）。J-S19 で同じ判定にしたので起きない |
| E-15 | `INVALID_ICON`（Direct Share、Base64 は読めるが画像でない） | ネイティブ（同期） | 後のフレーム | 1、2b |
| E-16 | `DIRECT_SHARE_REGISTRATION_FAILED` | ネイティブ（同期） | 後のフレーム | 1（文言）。起こせない（レート制限など） |
| E-17 | `NO_SHARE_TARGET` | ネイティブ（完了） | 後のフレーム | 1（文言）。起こせない |
| E-18 | `INVALID_MIME_TYPE` | ネイティブ（完了） | 後のフレーム | 1（文言）。C# が空白を NULL にするので起きない |
| E-19 | `UNKNOWN` / `OUT_OF_MEMORY` | ネイティブ | 後のフレーム | 1（文言）。起こせない |
| E-20 | C# の例外（`extern` を呼ぶ前・呼べない） | C# | 後のフレーム、`ex.Message`、`LogError`。表から消す | 2a（`PendingCallCountForTests` が 0 に戻る。継ぎ目で例外を起こす）、形はコードレビュー |
| E-21 | 受け口の中の例外 | C# | 出ない、`LogError` | コードレビュー。起こせない |
| E-22 | リスナーの登録の失敗（J-S6） | ネイティブ（入口） | `LogWarning`、次の呼び出しでやり直す | コードレビュー。起こせない |
| E-23 | 入口の拒否の `release` が戻る前に来る / 完了の直後に `release` が来る（J-S1） | ネイティブ | 表から消える。完了は表を読んだ時点の記録で出る | 2a（`OnDoneCore` の直後に `OnReleaseCore` を呼び、取り出し口が回る前に表が空でも結果が正しく出る。`OnReleaseCore` だけ〈入口の拒否〉なら表が空になり結果は Manager が出す）、2b（入口の拒否は起こせないので、ふつうの完了で表が 0 に戻る） |

## 7. テスト方針

### 7.1 層 1（EditMode、`AndroidShareCApiTests`）

- 3 つの構造体の大きさと全フィールドの位置（56 / 32 / 48。`StructLayoutTest.cpp` の値）
- 要求の ID が 64 ビットであること: `SelectionCallback` の 2 番目の引数の型が `ulong`（反射。`extern` は Editor ではコンパイルされないので delegate で見る）。`out ulong` と取り消しの `ulong` は層 2b（7.3）
- 構造体の組み立て: `struct_size`、`reserved0 = 0`、任意の値の空白が NULL になること、文字列が NUL 終端の UTF-8 であること（読み戻す）、Chooser Action の配列の並びとアイコンのバイト列と大きさ、Direct Share の `category` の NULL
- 検査（4.2）: 各操作の必須の値の null・空・空白、配列の空と要素の null、Base64 でないアイコン。文言が 1.x と同じであること
- `IsBlank`（J-S19）: U+0020、U+3000、U+001C は空白、U+0085 と U+00A0 の扱い（Kotlin と同じ。U+00A0 は Zs なので空白）、ふつうの文字は空白でない
- `DecodeBase64Leniently`（J-S18）: パディングあり・なし、改行入り、読めないもの
- `LooksLikeImage`: PNG・JPEG・WebP・GIF・BMP の印、短すぎる、ほかの印
- `NormalizeActions`（4.2.1）: API 33 なら空、空白の項目を捨てる、null の値を捨てる、Base64 でないもの・画像の印が無いものを捨てる、重複に `#ntk-dup-2`・`#ntk-dup-3` の印、C に渡す形（U+0000 で切る、サロゲート）で比べる、順を保つ
- `ToPublicActionId`: 印を外す、SEND なら捨てる（null を返す）、印の無いものはそのまま
- 文言（4.4）: 全エラー値と想定外の値、入口と完了の別、9 操作の `<op>`、`MissingPath`（1 つ、複数、どれも在る、ディレクトリ）
- `AllocBytes` の読み戻し

`AndroidNativeToolkitCApiTests`（共通部）: 移した `AllocStringArray` の読み戻し。

### 7.2 層 2a（PlayMode・Editor、`AndroidShareManagerEditorTests`）

Manager が Editor でもコンパイルされる（5.1）ので、Editor の契約と配送を確かめる。

- Editor の 9 操作（J-S3）: 呼び出しの中ではイベントが出ず（呼んだ直後は 0 回）、次のフレームで `ShareOperationCompleted` → per-call の順に 1 回、`<op> could not be started.`。`LogWarning` が出る。`CancelPendingShareCallback` も失敗で、`onSelected` は消える（継ぎ目で入れた `onSelected` が、その後の選択で呼ばれない）
- per-call の枠（J-S5、R1-1）: Editor で同じ操作を同じフレームに 2 回、別の callback で呼ぶと、**それぞれが自分の失敗を 1 回ずつ受ける**（1.x と同じ）。null の上書き（2 回目が null なら、2 回目の結果に per-call は無い）
- 完了の時点の枠（J-S5）: `OnDoneCore` で完了を入れた後、取り出し口が回る前に同じ操作の per-call を上書きしても、その完了は前の per-call を呼ぶ（1.x と同じ。完了の受け口で取り出す）
- 検査の失敗（E-2、E-3）: 後のフレームに 1 回、1.x の文言
- null（E-1）: `ArgumentNullException`。per-call も coordinator も登録されない
- `user_data` と `release`（E-23、C1-3）: `AddCall` → `OnDoneCore` → `OnReleaseCore` を取り出し口が回る前に続けて呼び、表が空になっても結果が 1 回、正しい操作とパスで出る。`OnReleaseCore` だけなら表が空になる。`PendingCallCountForTests` が 0 に戻る
- 配送（J-S4）: 結果を取り出し口に入れ、Manager を無効にしても届く。Manager を壊すと捨てる（新しい Manager を作らない）
- 選ばれたアプリ（J-S12）: 継ぎ目で `onSelected` を入れ、`OnSelectionCore(id, "pkg")` を入れると、`ShareCallbackReceived` → `onSelected` の順に 1 回。もう一度入れると `ShareCallbackReceived` だけ。`package_name` が NULL（`null`）でも出る。ID は照らさない（どの ID でも出る）
- Chooser Action（J-S17、4.2.1）: 世代が生きているときの `OnChooserActionCore(id)` は、coordinator を通って共通イベント → per-call の順に出る。per-call は消えない（何度でも）。印付きの ID は元の ID で出る。SEND は出ない。世代が古いとき（`ShareText` を呼んだ後に成功が来ていない、`OnDestroy` の後）は出ない

### 7.3 層 2b（実機の Player）

個々のテストと期待値は UI テスト計画 v1 に番号を振る（この設計のレビューの後）。この節は方針と共通の約束を書く。**IL2CPP の ARM64 だけ。**

**`AndroidSharePlayerTests`（UI を使う。カテゴリ `AndroidUiDriver` をクラスに付ける）。** 後片付けは Dialog の形（`Safely`、要求の数とイベントの数の照合、イベントのスレッドの照合。Dialog の UI テスト計画 v2 の 3 章）を写す。Sharesheet が残っていれば Back で閉じる（補助が Sharesheet のパッケージの有無で判定する）。

- **開く操作**（テキスト、URL、題名と件名、プレビュー、画像、複数の画像、ファイル、複数のファイル）: Sharesheet が開き、成功の結果が 1 回、Sharesheet を Back で閉じる。画像とファイルは `persistentDataPath` に書いたものを使う
- **Chooser Action**（API 34 以上の端末）: アクションのラベルが Sharesheet に出て、押すと `ShareChooserActionTapped` → per-call の順に 1 回、ID が `intentAction`。捨てたアクション（空白、Base64 でない、画像の印が無い）が出ないこと。同じ `intentAction` の 2 つが両方出て、どちらを押しても元の ID で 1 回。SEND のアクションは出るが通知は来ない。タップ 1 回にイベント 1 回（リスナーの二重登録が無い。R1-10）
- **前の Share のアクション**（J-S17）: アクション付きで開いて Back で閉じ、別の `ShareText`（壊れた画像のアイコンで失敗させる）の後、前の Sharesheet は閉じているので、開き直さずに確かめる方法が無い。代わりに層 2a で固める（7.2）。実機では「新しい `ShareText` で開いた Sharesheet のアクションは届く」を確かめる
- **選ばれたアプリ**: `ShareWithCallback` で Sharesheet を開き、このアプリ自身（既存の androidlib の `SEND`。ラベルはアプリ名）を押す。`ShareCallbackReceived` に自分のパッケージ名が 1 回、`onSelected` が 1 回。Player は前に戻る。Back で閉じたときは何も来ない。`CancelPendingShareCallback` の後に選んでも来ない（取り消しに使った `ulong` の ID が正しく渡ったことの確かめ）
- **Direct Share の表示**: 登録した共有先（short label）が Sharesheet の上の段に出ることを試す（このリポジトリは `<share-target>` を宣言済み。1.7）。OS のキャッシュで出るまでに時間がかかるなら、期限を決めて待ち、出なければ理由を書いて手動に回す
- **失敗の経路で Sharesheet が出ないもの**: 画像として壊れた Chooser Action のアイコン（E-13）。Sharesheet が出ないこと
- **後ろから呼ぶ**（E-10）: Dialog の AD-27 と同じ形（補助の `background`、別のスレッドが Bridge を直接呼ぶ）。`Failed to <op>: the app is not in the foreground.`

**`AndroidShareManagerPlayerTests`（UI を使わない。カテゴリを付けない）。** 補助がいない実行でも走る。

- Direct Share の登録・削除の成功が後のフレームに 1 回。UnityMain から呼び、戻った時点で 0 回（J-S2）
- 無いファイル（E-11）、範囲外の実在のファイル（E-12）、複数のときの文言、画像でない Direct Share のアイコン（E-15）、C# の検査の失敗（E-2〜E-4）。Sharesheet が開かない経路だけ（開く経路は UI を使うクラスに置く）
- 呼ぶスレッド（J-S16）: 別のスレッドから `RegisterDirectShareTarget` を呼び、例外が出ず、結果が UnityMain で 1 回
- 表の後片付け（E-23）: 各テストの後に `PendingCallCountForTests` が 0

### 7.4 層 3・手動

- 層 3: 7.3 の画面の照合（補助が Unity の外から OS の画面を読む）が層 3 に当たる
- 手動: API 33 以下の端末（Chooser Action が出ないこと、壊れた画像のアイコンでも Share が失敗しないこと）。手元に端末が無い。Direct Share の表示が自動で確かめられなかったとき（7.3）

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。起こせない失敗（E-9、E-16、E-17、E-19、E-21、E-22）は文言の変換を層 1 で押さえ、形をコードレビューで押さえる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- OS が求める許可: なし（Share に固有の権限は無い。1.8）
- 前提の OS 設定: 画面が点いていて、ロックが解けていること（補助が点け、ロックを外す。PIN のある端末では止める。補助の設計書 v5 の 4 章）。アニメーションと自動入力は補助が切る
- Chooser Action は API 34 以上の端末でだけ確かめられる（Pixel 6a は Android 16 で可）
- Sharesheet の共有先の一覧は端末に入っているアプリで変わる。テストは、このアプリ自身とラベルの文言だけを押す

### 8.3 呼び出し側を止める OS の画面

Sharesheet は非同期で、呼び出し側を止めない。ただし選ばれたアプリの結果は、人が押すまで来ない。**外から押す手段は、Dialog で作ったホストの adb の補助を広げる**（補助の設計書に足す。補助の設計書の新しい版にする）:

- **Sharesheet の中の文言を探して押す命令**（例: `tap-in-sheet text=<Base64 の値>`。セレクタのキーと Base64 の決まりは補助の今の形〈`android_ui_driver.py:85-103`〉に合わせる）。Sharesheet のパッケージ（`com.android.intentresolver`）の中で文言を探し、無ければ上へスワイプして（8 回まで）探し直す（native-toolkit の `UiDriver.kt:219-228` のやり方。**`:229-231` の「見つからなければ画面の文言を全部ログに出す」は写さない**）
- **Sharesheet が出ているかの命令**（`count package=<Base64 の値>` のように、パッケージで数える）
- **プライバシー**（native-toolkit の UI テストの決まりと同じ。R1-12）: Sharesheet にはほかのアプリの共有先と連絡先が出る。
  - Sharesheet のパッケージが画面にある間は、失敗のときの画面の構造（`--dump-dir`）を書き出さない
  - 読み取りの後に、端末の上のダンプ（`/data/local/tmp/ntk-ui-dump.xml`）を消す
  - Sharesheet の中の要素の `expect` や探索が失敗したとき、理由に読み取った値を入れない（結果のファイルを通してテストのログに出るため）
  - 読み取った構造は判定にだけ使い、ログに出さない
- **テストの共有先**: このアプリ自身を使う。既存の androidlib（`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib`）が `UnityPlayerGameActivity` に `SEND`（`*/*`）を宣言しているので、Sharesheet にアプリ名で出る見込み。選ぶとテスト用の Player が `SEND` で前に戻る（生成された manifest では `launchMode="singleTask"`）。これはこのプロジェクトのすべての Android のビルドに入っている既存の宣言で、この移行では変えない。**要検証**（5.5 の手順 2）: Sharesheet が呼び出し元のアプリ自身を一覧から除かないこと。除くなら、既存の androidlib に別のラベルの `<activity-alias>` を足す案を検討する（その場合は、すべてのビルドに入ることを意図した変化として書く）

補助がいない実行（Unity の窓から流すなど）では、UI を使うテストは `Assert.Ignore` になる。`verify_unity_android.sh` のゲート（カテゴリ `AndroidUiDriver` の skip が 0）は Dialog と同じ。

### 8.4 アプリ側の設定（利用者の責任）

- FileProvider: 要らない（AAR に入っている。1.8）
- Direct Share を Sharesheet に出すには、アプリの manifest に `<share-target>`（`res/xml/shortcuts.xml` と、ランチャーの Activity の `<meta-data android:name="android.app.shortcuts">`）が要る。AAR は宣言していない（1.x と同じ）。マニュアルは説明している（`docs/1.11.0/share.ja.md:76-104`）。このリポジトリのサンプルは、`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib` で宣言済み
