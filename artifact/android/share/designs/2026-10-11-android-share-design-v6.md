# Android Share 実装計画 v6（C ABI 2.0.0 への移行）

## 基本情報

| 項目 | 内容 |
|---|---|
| 機能 | Share（Android） |
| 種別 | **移行。機能追加ではない。** 1.x の Unity 向けブリッジ（`android.unity.share.UnityAndroidShareManager`、`AndroidJavaObject` / `AndroidJavaProxy`、JSON）から、C ABI 2.0.0（`libntk.so`、`Share.h`）の P/Invoke に置き換える |
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（4 機能の目次。決定 J-1〜J-11、Windows と違うところ A-1〜A-6。移す順は Dialog → **Share** → Clipboard → Notification） |
| 前例 | Android Dialog の移行（`artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md`、実装結果 `artifact/android/dialog/results/2026-10-11-android-dialog-implementation-feature-result-v4.md`）。共通部（`AndroidNativeToolkitCApi`、`AndroidNativeCallbackQueue`）と、UI の補助、後片付けの形（`Safely`）はここで作った |
| 保つ範囲 | **親課題の J-11「ふつうの使い方を保つ」**（2026-10-11、利用者と相談）。保つのは、公開 API と、UnityMain から 1 回ずつ正しい入力で呼んだときの結果・時点・文言、Editor の結果。複数のスレッドからの同時の呼び出し、Manager を壊す直前・直後の結果、変わった入力は、いちばん単純な形にして 5.3 に書く。**「1.x と同じ」と書く行には、タグ `1.12.0` のコードの位置（または今の C# の位置）を付ける** |
| 方針 | **公開 API（`Instance`、`Initialize()`、メソッド 9 本、イベント 3 本、`Operation*` 定数 9 個、Payload 8 型、結果 3 型、`ShareChooserActionCallbackCoordinator`、引数、既定値、エラーの文言）を保つ。** 変わるのは 5.3「既知の差分」に列挙したものだけ |
| AAR | `android-native-toolkit-2.0.0.aar` と `android-native-toolkit-capi-2.0.0.aar`（dist 1.13.0）。旧ブリッジは移行の間 `extra_*`（dist 1.12.0）に残す |
| 出典 | native-toolkit の `dist/1.13.0/android/include/NativeToolkitC/Share.h`（以下 `Share.h`）、`android/android_library_capi/src/main/cpp/src/Share/Share.cpp`（以下 `S.cpp`）、`.../capi/jni/ShareBridge.kt`（以下 `Bridge`）、`android/android_library/.../share/`（以下 `Lib`。`data/repository/ShareRepositoryImpl.kt` は `Repo`）。旧ブリッジは native-toolkit のタグ `1.12.0` の `android/unity_android_plugin/src/main/java/android/unity/share/`（以下 `OldBridge`。`UnityAndroidShareManager.kt` と `UnityShareJsonParser.kt`）。C ABI の約束は native-toolkit の `artifact/topics/android-c-abi/designs/` の第 1 部（以下 P1）と第 2 部（以下 P2。8.3 の SH-01〜SH-14 が 1.x のブリッジとの対応表）、Unity 向けの申し送りは `artifact/topics/android-c-abi/results/2026-10-10-android-c-abi-stage2c-handoff.md`（以下 `Handoff`）の 4.2〜4.4 |
| UI の補助 | `artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v5.md`。Share の分は v6 にする（8.3） |
| UI テスト計画 | `artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v1.md`（この設計のレビューの後に作る。7.3 の中身に番号を振る） |
| レビュー | `artifact/android/share/reviews/2026-10-11-android-share-design-review-v1.md`、`...-review-v2.md`、`...-review-v3.md`、`...-review-v4.md`、`...-review-v5.md`（どれもサブエージェントと Codex。v5 でサブエージェントの A1 が 0 になり、v6 は v5 との差分だけを確かめる） |
| 1.x の記録 | 1.x の設計 `artifact/android/share/designs/2026-06-21-android-share-design-v1.md`、`-v2.md`、`2026-06-27-android-share-design-v3.md`。**1.x の実機の UI テストの記録は無い**（`artifact/topics/device-verification-records/README.md`）。Dialog と同じく、移行の前に 1.x のまま流して記録する（5.5 の手順 3） |

## v5 からの主な変更

レビュー v5 でサブエージェントの A1 は 0 件。Codex の A1 2 件（サブエージェントは同じ所を C とした）と B・C を直した。

- **Chooser Action の per-call を読む時点を 1.x と同じにした**（C5-2、R5-4。ラウンド 3 から「ふつうの使い方の外」とみる理由が揺れていた論点）。今の Manager の coordinator への参照を static に置き（`Awake` で入れ、`OnDestroy` で消す）、アクションの受け口（Android の main）がその coordinator の `Fire` を呼ぶ。coordinator の `dispatch` は取り出し口の `Post`。1.x の proxy と同じく、タップを受けた時点の per-call が閉じ込められ、Manager がいなければ捨てる。5.3 の行は消した
- 2.2・2.3・J-S2 の「1.x の結果はどれも Kotlin を通り、後のフレームに出た」を、端末の Kotlin を通った結果に限った（C5-1、R5-3）
- Sharesheet の命令は、失敗・期限切れでも Sharesheet を閉じてから `fail` を返す（R5-1）。期限の後の最悪の時間を書いた（R5-2）
- 引用の範囲と変更の記録を揃えた（R5-5）

## v4 からの主な変更（v5）

（v5 で、親課題の行を J-1〜J-11 に、UI の補助の行に v6 を書いたことも含む。R5-5）

レビュー v4 の A1 は 2 件で、どちらも記述の範囲の誤りだった（仕組みの誤りは出ていない）。

- 5.3 の「UnityMain の処理の中から呼んだときの結果のフレーム」と J-S2 を、Editor（と端末の C# 側の即時の失敗）に限った（R4-1）。端末で Kotlin を通った 1.x の結果は、もとから次のフレーム以降だった
- 6 個以上の警告は `ShareText` だけ（R4-4、C4-1）
- R3-1 のテストの継ぎ目 `AddSelectionIdForTests`、Editor の `OnDestroy` は F-9 を呼ばない、1 フレーム待つ（R4-2）
- 5.1 の `lock` の記述を J-S6 に合わせた（R4-3、C4-2）
- 補助の Sharesheet の命令に 60 秒の期限を持たせる。本文の確かめは「在ることを確かめて Back」の 1 つの命令（R4-5、R4-6）
- Chooser Action の per-call を読む時点を J-11 の外とみた理由を 5.3 に書いた（R4-7）

## v3 からの主な変更（v4）

レビュー v3 の A1 は、どれも軽く、新しい仕組みを要らないものだった（サブエージェント 4 件、Codex 2 件）。

- `OnDestroy` は自分が `_instance` のときだけ static の状態を空にする（R3-1）。テストを足した
- 5.3 に行を足した・直した: Chooser Action の per-call を読む時点（R3-2、C3-1。v3 は「直した」と書いたが本文に無かった）、取り消しの成功を出す時点（C3-2）、同期に決まる成功も含めた結果の順（R3-4）、UnityMain の処理の中から呼んだときのフレーム（R3-5）
- J-S2 の「1.x の dispatcher も同じ」を直した（R3-5）
- `CallNative` が受け付けたかを返す（R3-10、C3-3）
- 層 2a に完了の受け口の `onSelected` のテスト（R3-3）、static を戻す継ぎ目（R3-9）。層 2b に `Initialize()` を 2 回呼ぶテスト（R3-9、C3-4）、Direct Share の後片付け、本文の確かめ、依頼の時間の上限（R3-8）、表が 0 に戻るのを待つ形（R3-7）
- 引用の行を直した（R3-6）。6 個以上の警告を生の個数で検査の前に出す、`ShareWithCallback` の要素の null、coordinator の XML コメント、補助の設計書の版、登録の印の `lock`（R3-11）

## v2 からの主な変更（v3）

レビュー v2 でも、「1.x と同じ」と書いた所が 1.12.0 のコードの細部と違う指摘（A1）が続いた。2 回続いたので、個別に直す前に保つ範囲を利用者と決めた（親課題 J-11「ふつうの使い方を保つ」）。その方針で、v2 で細部を写すために足した仕組みを外し、単純にした。

- **外した仕組み**: C# のアクションの世代（J-S17）、重複した ID の印（`#ntk-dup-<n>`）、API レベルの読み取り（J-S20）、寛容な Base64（J-S18）。これらが守ろうとした場面（失敗した Share の後や Manager を壊した後の前のアクション、重複した ID、API 33 以下の端末での壊れたアイコン、パディングの無い Base64）は、ふつうの使い方ではないので 5.3 に書く
- **Editor は 1.x と同じく、入力によらず `<op> could not be started.`**（R2-1）。null の検査の後、ほかの検査の前に Editor の判定を置く
- **取り消しは、受け付けた選択の要求の ID をすべて取り消す**（R2-3、C2-1）。どれが今の待ちかを C# は知らなくてよい（F-9 は今の待ちの ID でなければ何もしない）
- **`onSelected` は `ShareWithCallback` のどの失敗でも消す**（R2-7）
- **Sharesheet が出ている間は Unity が止まる見込み**（R2-2）。テストは、補助への依頼を先に出してから同じフレームで開く。8.3
- `ShareText` と `ShareWithCallback` の流れを 5.4 に書いた（R2-4）
- 層 2a の継ぎ目を具体にした（R2-8、C2-6）。`extern` を呼ぶ部分を差し替えられる関数にし、例外を注げるようにした
- 5.4 の擬似コードを `returned` の旗にした（R2-14、C2-4）
- 受け口が per-call の枠を取り出すことを、`common.md` と `Handoff` の決まりの例外として書く（R2-9）
- 5.3 の各行に 1.x の根拠の列を足し、記述を直した（R2-11、R2-12、R2-13、R2-5、R2-6）

## v1 からの主な変更（v2）

v2 の変更は v2 の文書にある。v3 でそのうち次を取り消した: C# の世代、重複した ID の印、API レベル、寛容な Base64、選択の要求の ID を 1 つだけ持つこと。

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
- `Handoff` 4.3: 要求の ID は manager の表（UnityMain）に置き、選択のイベントは UnityMain に積んだ後で「直前の要求の ID」と照らす。**この計画は照らさない**（1.x も照らしておらず、ネイティブが今の待ちの選択だけを届ける。J-S12）

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
- **すべての結果は `UnityMainThreadDispatcher` を通って、呼び出しの後に出る**（端末の操作の結果は Kotlin が main に積み、listener が dispatcher に積むので、次のフレーム以降。Editor と端末の C# 側の即時の失敗は C# が直接 dispatcher に積むので、dispatcher のループの中から呼べば同じフレームのこともある。5.3）。呼び出しの中でイベントが出ることは無い
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
| C# の `TryPrepareCall` が false（Android 以外。ターゲットが Android の Editor を含む） | `<op> could not be started.`（`LogWarning("... can only be called on Android.")`）。**イベントは出る**（呼び出しの後。同じフレームのこともある） |
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
- **受け口は Manager の private static メソッドに `[MonoPInvokeCallback(typeof(...))]` を付け**、delegate は `static readonly` に保ち、関数ポインタは `Marshal.GetFunctionPointerForDelegate` で 1 回だけ作る（IL2CPP。`Handoff`:142-143、Dialog の `AndroidDialogManager` と同じ）。受け口は 4 つ: 完了、`release`、アクション、選択
- Chooser Action の配列は、`ChooserAction` を `count` 個並べた 1 つのブロックを確保し、`Marshal.StructureToPtr` で書く
- 文字列の配列（`const char* const*`）は `AllocStringArray`。今は `AndroidDialogCApi` にあるのを共通部 `AndroidNativeToolkitCApi` に移す（Dialog、Share、Clipboard の `ntk_clipboard_copy_texts`、Notification の `lines` が使う。`common.md` の `Runtime/<Platform>/Common/` の条件）
- アイコンのバイト列は `AllocBytes(byte[], List<IntPtr>)`。`const uint8_t*` を取るのは `Share.h` だけなので、**Share の Bridge に置く**（P4）。`Alloc` を使い、記録に失敗しても漏れない

### 4.2 引数の変換（公開 API は変えない）

C# が先に整え、整えた後の値だけを C に渡す。「空白」は J-S17 の判定。「1.x」の列の根拠は、今の C# の `AndroidShareManager.cs` / `AndroidShareJsonBuilder.cs`（以下 `Mgr` / `Json`）とタグ `1.12.0` の `UnityShareJsonParser.kt`（以下 `Parser`）。

| 入力 | C# がすること | 1.x（根拠） |
|---|---|---|
| `payload` が null | `ArgumentNullException`（J-S7） | `NullReferenceException`（`Json` の各 `Build*` が `payload.` を読む） |
| `filePaths` / `ids` の配列そのものが null | `ArgumentNullException`（J-S7） | `NullReferenceException`（`Json:134-141` の `BuildStringList`） |
| `chooserActions` の要素が null | `ArgumentNullException`（J-S7） | `NullReferenceException`（`Json:120-129`。coordinator に登録した後〈`Mgr:207-209`〉） |
| `text` が空・空白（`ShareText`、`ShareWithCallback`） | 検査の失敗 `Failed to <op>: text is required` | 同じ（`Parser:15` の `require`、`UnityAndroidShareManager.kt:390-399` の写し） |
| `title`、`subject`、`mimeType`、`previewTitle`、`previewThumbnailPath`、Direct Share の `category` が null・空・空白 | NULL を渡す（無し。`mimeType` と `category` はライブラリの既定値） | 同じ（`Json:24-28,41,90,105-111` が `IsNullOrWhiteSpace` で省き、`Parser:19-24,35,76` が既定値にした） |
| `chooserActions`（`ShareText`） | 4.2.1 | |
| `chooserActions`（`ShareWithCallback`） | 渡さない（F-8 に引数が無い）。1 個以上なら今と同じ `LogWarning` | 同じ（`Mgr:303-316`。`UnityAndroidShareManager.kt:271-302` は使わない） |
| `filePath` が空・空白（`ShareImage`、`ShareFile`） | 検査の失敗 `Failed to <op>: filePath is required` | 同じ（`Parser:31,50`） |
| `filePaths` が空 | 検査の失敗 `Failed to <op>: filePaths must not be empty` | 同じ（`Parser:43,59`） |
| `filePaths` の要素が null | 検査の失敗 `Failed to <op>: filePaths must not contain null`（J-S8） | 違う（5.3） |
| Direct Share の `id` / `label` / `iconBase64` が空・空白 | 検査の失敗 `Failed to registerDirectShareTarget: id is required`（`label is required`、`iconBase64 is required`） | 同じ（`Parser:66-71`） |
| Direct Share の `iconBase64` が Base64 として読めない（前後の空白を除いて `Convert.FromBase64String`） | 検査の失敗 `Invalid icon data for Direct Share target: <id>` | 同じ文言（`UnityAndroidShareManager.kt:237-241` の `InvalidBase64Icon`）。読める範囲は違う（5.3） |
| `ids` が空 | 検査の失敗 `Failed to removeDirectShareTargets: ids must not be empty` | 同じ（`Parser:83-84`） |
| `ids` の要素が null | 検査の失敗 `Failed to removeDirectShareTargets: ids must not contain null`（J-S8） | 違う（5.3） |
| 必須の文字列が null（`text`、`filePath`、Direct Share の `id` / `label` / `iconBase64`） | 空と同じ検査の失敗 | 違う（5.3） |

文字列はすべて共通部の `AllocUtf8`（NUL 終端の UTF-8。対になっていないサロゲートは U+FFFD）で渡す。途中の U+0000 はそこで切れる（5.3）。

#### 4.2.1 Chooser Action の整え方

`AndroidShareCApi.NormalizeActions(ChooserActionPayload[])` が、C に渡す組の並びを返す（純粋な関数。層 1）。正しい入力（ラベル・読めるアイコン・一意の `intentAction`）では 1.x と同じものが表示される。

1. `label`・`iconBase64`・`intentAction` のどれかが null・空・空白のアクションを捨てる（1.x の parser は空白を捨てた。`Parser:88-100`。null の扱いは 5.3）
2. `iconBase64` を、前後の空白を除いて `Convert.FromBase64String` で解く。読めなければそのアクションを捨てる（1.x のライブラリは読めないものを飛ばした。タグ `1.12.0` の `android/library/.../ShareRepositoryImpl.kt:254-255`）
3. 解いたバイト列の先頭が、PNG・JPEG・WebP・GIF・BMP・ICO・HEIF / AVIF（4 バイト目からの `ftyp`）のどの印にも当たらなければ、そのアクションを捨てる（1.x の「画像にできないものは飛ばす」に近づける。印は当たるが中身が壊れた画像は、Share 全体の失敗になる。5.3）
4. `intentAction` を C に渡す形（U+0000 で切り、対になっていないサロゲートを U+FFFD に置き換えた後）で比べ、空になれば捨てる。前のアクションと同じなら捨てる（C ABI が重複を入口で拒むため。1.x は重複も表示した。5.3）
5. 残ったアクションを、`id = intentAction`、`label`、アイコンのバイト列で渡す
6. 6 個以上の警告（今と同じ `LogWarning`。切り詰めない）は、ここではなく 5.4 の前半の 3 で、整える前の生の個数で出す（1.x は検査の前、Editor でも出した。`Mgr:203-206`）

通知（4.3）では、ID が `android.intent.action.SEND` なら捨てる（1.x は SEND を受け手の登録から除いたので通知しなかった。`ShareChooserActionInputs.kt:16-24`。`AndroidSharePayloads.cs:19-26` の XML コメントもそう書いている）。

### 4.3 結果と完了

- 開く操作（F-1〜F-5、F-8）: 戻り値が `NONE` なら受け付けた。完了の `error` が `NONE` なら成功（Sharesheet が開いた、という意味。1.x の「起動できた」と同じ。`UnityAndroidShareManager.kt:323-400` の `executeOperation`）、それ以外は失敗
- Direct Share（F-6、F-7）: 戻り値が結果。**呼んだスレッドで同期に動く**（`pushDynamicShortcut` はビットマップを読み Binder を呼ぶ。1.x は Android の main だった。5.3）
- 取り消し（F-9）: 戻り値は見ない（ログだけ）。Android の上ではいつも成功（1.x と同じ。`UnityAndroidShareManager.kt:309-315`）。Editor・Android 以外では失敗（J-S3）
- 選ばれたアプリ（F-11）: 照らさずに出す（J-S12）。`ShareCallbackResult("shareWithCallback", packageName)`（`package_name` が NULL なら `null`）
- Chooser Action（F-10）: SEND を捨てた後、アクションの受け口（Android の main）が、今の Manager の coordinator（static の参照。`Awake` で入れ、`OnDestroy` で消す）の `Fire(new ShareChooserActionResult(actionId))` を呼ぶ。`Fire` はその時点の per-call を閉じ込めて、`dispatch`（取り出し口の `Post`）で UnityMain に送る（1.x の proxy と同じ時点。`Mgr:52-53,433-436,483-485`、`ShareChooserActionCallbackCoordinator.cs:54-58`）。参照が null（Manager がいない）なら捨てる（1.x はリスナーを外していた）

**どの結果も、取り出し口 `AndroidNativeCallbackQueue` を通して出す。呼び出しの中ではイベントを出さない**（J-S2。1.x と同じ。`Mgr:399-420` が dispatcher に積んだ）。per-call の枠を取り出す時点は J-S5。

### 4.4 エラーの文言とログ

`<op>` は 2.1 の `Operation*` の値。新しい文言は、1.x の parser の文言に合わせて末尾に `.` を付けない（4.2 の `must not contain null`）。ただし 4.4 の C ABI の失敗の文言は Dialog と同じ形（末尾に `.`）にする。

| 原因 | 時点 | `ErrorMessage` | ログ |
|---|---|---|---|
| Editor・Android 以外（J-S3） | 呼び出し | `<op> could not be started.`（入力によらない） | `LogWarning("<op> can only be called on Android.")`（1.x と同じ。`Mgr:353-362`） |
| C# の検査（4.2 の表） | 呼び出し | 4.2 の文言 | `LogWarning` |
| ネイティブが使えない（共通部の `NativeUnavailable`） | 呼び出し | `Failed to <op>: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).` | `LogWarning` |
| `NOT_INITIALIZED`（入口だけ。`Share.cpp:117`） | 呼び出し | `Failed to <op>: the native library is not initialized yet.` | `LogWarning` |
| `INVALID_PARAMETER` / `NOT_SUPPORTED` / `INVALID_CHOOSER_ACTION`（入口） | 呼び出し | `Failed to <op>: the request was rejected (<名前>).` | `LogError`（C# の組み立ての誤り。4.2.1 の後では起きない） |
| `INVALID_PARAMETER`（完了・Direct Share。Kotlin の `IllegalArgumentException`） | 完了 / 同期 | `Failed to <op>: the request was rejected (INVALID_PARAMETER).` | `LogWarning` |
| `EMPTY_CONTENT` | 呼び出し / 完了 | `Failed to <op>: text is required`（J-S17 の検査が先に拒むので、ふつうは来ない） | `LogWarning` |
| `EMPTY_FILE_LIST` / `EMPTY_ID_LIST` | 呼び出し | 4.2 の文言（C# が先に拒む） | `LogWarning` |
| `NO_SHARE_TARGET` | 完了 | `No app available to handle this share request.`（1.x と同じ。`UnityAndroidShareManager.kt:323-400`） | `LogWarning` |
| `FILE_NOT_FOUND` | 完了 | `File not found: <path>`。1 つのパスの操作はそのパス（1.x と同じ）。複数のときは J-S9 | `LogWarning` |
| `ILLEGAL_FILE_ACCESS` | 完了 | `File cannot be shared: <path>. Ensure the file is in a supported directory.`。1 つのパスの操作はそのパス（1.x と同じ）。複数のときは J-S9 | `LogWarning` |
| `INVALID_MIME_TYPE` | 完了 | `Invalid MIME type: <渡した値>`（C# が空白を NULL にするので、ふつうは来ない） | `LogWarning` |
| `DIRECT_SHARE_REGISTRATION_FAILED` | 同期 | `Failed to register Direct Share target: the system did not accept the shortcut.`（理由は C ABI から来ない。5.3） | `LogWarning` |
| `INVALID_ICON` | 同期 | `Invalid icon data for Direct Share target: <id>`（1.x と同じ文言） | `LogWarning` |
| `INVALID_CHOOSER_ACTION`（完了） | 完了 | `Failed to shareText: a chooser action icon could not be read as an image.`（新しい。5.3） | `LogWarning` |
| `NOT_FOREGROUND` | 完了 | `Failed to <op>: the app is not in the foreground.`（新しい。5.3） | `LogWarning` |
| `OUT_OF_MEMORY` / `UNKNOWN` / 想定外の値 | 呼び出し / 完了 | `Failed to <op>: <名前>.` | `LogWarning` |
| C# の例外（変換・確保の失敗など） | 呼び出し | `ex.Message`（1.x の `Call` の例外と同じ扱い。`Mgr:341-349`） | `LogError` |
| 受け口の中の例外 | - | イベントは出ない（その完了は失われる） | `LogError` |

`<名前>` はエラーの列挙の名前。名前と文言への変換は純粋な関数にし、層 1 で固める。

**ログ（J-S10）**: JSON を丸ごと出すのをやめる。本文（`text`）と `subject` は長さだけを出す（native-toolkit の `logSafeDescription` と同じ考え方。UI の補助が logcat を読むので、値が成果物に残りうる）。題名、MIME、パス、ID、ラベル、カテゴリはそのまま出す（1.x もライブラリも出している）。アイコンはバイト数だけ。選ばれたアプリのパッケージ名と、押されたアクションの ID はそのまま出す。Editor の `Initialize()` は `Debug.Log`（毎回の `Awake` で警告を出さない）。

## 5. 実装詳細

### 5.1 新規作成・既存変更・非変更

| 区分 | ファイル | 置き場所 | ガード | 内容 |
|---|---|---|---|---|
| 新規作成 | `AndroidShareCApi.cs` | `Runtime/Android/Share/` | 型と純粋な関数は `UNITY_ANDROID \|\| UNITY_EDITOR`、`DllImport` は `UNITY_ANDROID && !UNITY_EDITOR` | Bridge。4.1 の構造体・delegate・`extern`、`AllocBytes`、入れ子の `Operation` 列挙（9 個。`Operation*` 定数と対応）、エラー定数 0〜16、`ShareCall`（1 回の呼び出しの記録: 操作、パス、MIME、Direct Share の ID）、4.2 の検査と整え（`IsBlank`〈J-S17〉、`IsAbsent`〈J-S17〉、`CheckText`、`CheckPath`、`CheckPaths`、`CheckIds`、`CheckDirectTarget`、`DecodeIcon`、`LooksLikeImage`、`NormalizeActions`、`IsNotifiedActionId`）、構造体の組み立て（`BuildTextContent`、`BuildActions`、`BuildDirectTarget`）、4.4 の文言（`ErrorName`、`FailureMessage`、`NativeUnavailableMessage`、`MissingPath`）、取り消す ID の一覧の扱い（`AddSelectionId`、`TakeSelectionIds`。J-S13） |
| 既存変更 | `AndroidShareManager.cs` | `Runtime/Android/Share/` | 型は `UNITY_ANDROID \|\| UNITY_EDITOR`（今は `UNITY_ANDROID`）。受け口・delegate を保つフィールド・関数ポインタは `UNITY_ANDROID && !UNITY_EDITOR` | `AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher` を消し、`AndroidShareCApi` を呼ぶ。5.2 の J-S1〜J-S18。**per-call の枠、`onSelected`、`user_data` の表、取り消す ID の一覧は static に置き、1 つの `lock` で守る**（受け口が Android の main から触るため。R2-9）。リスナーの登録の印は static に置き、J-S6 の登録専用の `lock` で守る。今の Manager の coordinator への参照（`volatile` の static。Unity の `==` は使わない）を `Awake` で入れ、`OnDestroy`（自分が `_instance` のとき）で消す。`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で static を戻す（Editor でドメインを再読み込みしない設定のため）。**層 2a の継ぎ目**（`internal`）: 受け口の本体（`OnDoneCore(IntPtr userData, int error)`、`OnReleaseCore(IntPtr userData)`、`OnChooserActionCore(string actionId)`、`OnSelectionCore(ulong requestId, string? packageName)`）、`AddCall(ShareCall)`、`SetPendingOperationForTests(string op, Action<ShareOperationResult>?)`、`SetPendingSelectedForTests(Action<ShareCallbackResult>?)`、`PendingCallCountForTests`、`ResetStaticStateForTests()`（`SubsystemRegistration` と同じ中身。層 2a の `SetUp` で呼ぶ）、`SelectionIdCountForTests`、`AddSelectionIdForTests(ulong)`、`CallNative(ShareCall, List<IntPtr>, Func<IntPtr, int> invoke)`（受け付けたら真を返す）（確保の後の「`AddCall` → `extern` → 戻り値の扱い → 呼べなかったときの後片付け」を 1 つにした関数。テストは `invoke` に例外を投げる関数を渡す。R2-8、C2-6） |
| 既存変更 | `AndroidNativeToolkitCApi.cs` | `Runtime/Android/Common/` | 今のまま | `AllocStringArray` を `AndroidDialogCApi` から移す（4.1） |
| 既存変更 | `AndroidDialogCApi.cs`、`AndroidDialogCApiTests.cs` | `Runtime/Android/Dialog/`、`Tests/Runtime/` | 今のまま | `AllocStringArray` を共通部の呼び出しに替える（振る舞いは変えない） |
| 既存変更 | `AndroidNativeToolkitCApiTests.cs` | `Tests/Runtime/` | 今のまま | 移した `AllocStringArray` のテスト（`AndroidDialogCApiTests` から移す） |
| 既存変更 | `AndroidShareJsonBuilder.cs` | `Runtime/Android/Share/` | 今のまま | 中身は変えない。Manager から使わなくなる。XML コメントに「2.0.0 では Manager は使わない。4 機能の移行の後に消す（親課題 README の段 4）」と書く（J-S11） |
| 新規作成 | `AndroidShareCApiTests.cs` | `Tests/Runtime/` | `UNITY_ANDROID \|\| UNITY_EDITOR` | 層 1（7.1） |
| 新規作成 | `AndroidShareManagerEditorTests.cs` | `Tests/PlayMode/` | `UNITY_EDITOR` | 層 2a（7.2） |
| 新規作成 | `AndroidSharePlayerTests.cs` | `Tests/PlayMode/` | 外 `UNITY_ANDROID`、中 `!UNITY_EDITOR` | 層 2b の UI を使うテスト（カテゴリ `AndroidUiDriver` をクラスに付ける。7.3） |
| 新規作成 | `AndroidShareManagerPlayerTests.cs` | `Tests/PlayMode/` | 同上 | 層 2b の UI を使わないテスト（7.3） |
| 既存変更 | `android_ui_driver.py`、`AndroidUiDriver.cs` | `scripts/`、`Tests/PlayMode/` | - | 8.3（依頼を先に出して後で答えを待つ形、Sharesheet の中で探して押す命令、プライバシーの決まり） |
| 既存変更 | `common.md`、`testing.md` | `agent-rules/coding-rules/` | - | `common.md` の Android の節に、Share の例外として: 呼び出しごとに `user_data` を表で付ける形、受け口で per-call の枠を取り出し `onSelected` を消すこと（ロックで守る。1.x の時点を保つため）、選択を照らさないこと、取り出した時点で生きている Manager に出し、いなければ捨てること（J-S4）。`testing.md`: 3 節のガードの表（`AndroidShareManager` を B 群から A 群へ）、7 節の Android の Share の層 |
| 既存変更 | `README.md` | `artifact/topics/android-c-abi-2/` | - | 決定 J-9 に Share の例外（J-S4）。J-11 は記録済み |
| 既存変更 | `ShareChooserActionCallbackCoordinator.cs` | `Runtime/Android/Share/` | 今のまま | XML コメントだけ（`:27-30` の「In production this is `UnityMainThreadDispatcher.Instance.Enqueue`」を、`AndroidNativeCallbackQueue.Post` に直す）。振る舞いは変えない（`Fire` は今と同じく、呼ばれた時点で per-call を写してから `dispatch` に渡す。Manager は `dispatch` に取り出し口の `Post` を渡し、アクションの受け口〈Android の main〉から `Fire` を呼ぶ） |
| 新規作成 | `2026-10-11-android-ui-driver-design-v6.md` | `artifact/topics/android-c-abi-2/designs/` | - | 補助の設計書の新しい版（8.3 の口と命令、プライバシー、時間の上限） |
| 非変更 | Payload 8 型（XML コメントを含む。SEND は 1.x と同じく通知されないので、`AndroidSharePayloads.cs:19-26` の記述は正しいまま）、結果 3 型、`AndroidShareManagerExampleController`、`AndroidShareResultTests`、`AndroidShareJsonBuilderTests`、`AndroidShareChooserActionCallbackCoordinatorTests`、`AndroidShareSampleSceneWiringTests`、`Runtime/Resources/UI/Android/Share/*`、`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib/*`（テストの共有先として使う。変えない） | - | - | |

### 5.2 判断

| # | 内容 | 決定 |
|---|---|---|
| J-S1 | 完了と呼び出しの対応付け | **呼び出しごとに `user_data` を付ける**（`Handoff` 4.3）。static な表に、連番のキーで、**`extern` を呼ぶ直前に**入れ、キーを `IntPtr` にして渡す（`CallNative`）。完了の受け口（Android の main）は表を**読むだけ**で、引いた `ShareCall` と `error` から作った処理を取り出し口に入れる（完了の直後、同じ main のメッセージの中で `release` が来て表から消えるため、キーのまま積まない）。表から消すのは `release` の受け口と、`extern` が戻らなかったとき（呼べなかった例外）の `CallNative` の `catch` だけ。入口で拒まれたときは `release` が戻る前に来て消し、`CallNative` が手元の `ShareCall` で失敗を出す。Dialog（J-D1）のように種類だけを渡さないのは、文言にパス（4.4）を入れるのに呼び出しごとの記録が要るため |
| J-S2 | 結果の時点 | **呼び出しの中ではイベントを出さない。** C# の検査の失敗、Editor、入口の拒否、同期の Direct Share も、取り出し口を通して出す（取り出し口の `Update` より前に走る `Update` から呼べば同じフレームに出ることもある。一方、取り出し口が走らせている処理〈利用者のイベントの購読者や per-call〉の中から呼ぶと、結果は次のフレームになる。1.x の dispatcher は、Editor と端末の C# 側の即時の失敗に限って同じループで走らせた。端末で Kotlin を通った結果は、1.x でももとから次のフレーム以降。5.3）。1.x はどの結果も dispatcher を通っていた（端末の操作の結果は Kotlin も通った。`Mgr:333-349,399-420`）。Dialog の「同期に出す」とは違う。操作をまたいだ結果の順は 1.x と変わりうる（5.3） |
| J-S3 | Editor・Android 以外 | 1.x と同じく、**入力によらず** `LogWarning("<op> can only be called on Android.")` と失敗 `<op> could not be started.`（`CancelPendingShareCallback` も同じ）。1.x は JSON を組んだ後で `TryPrepareCall` が偽になり、検査は Kotlin にあったので Editor からは届かなかった（`Mgr:329-337,353-362`。R2-1）。そのため、Editor の判定は null の検査（J-S7）の直後、ほかの検査の前に置く。ガードに `UNITY_EDITOR` を足したので、ほかのターゲットの Editor でも同じになる（5.3） |
| J-S4 | 結果を誰に出すか | **取り出した時点で生きている Manager（`_instance`）に出す。いなければ捨てる（ログだけ）。** 1.x は Manager の `OnDestroy` でブリッジのリスナーを外し、その後の結果を捨てた（`Mgr:123-136`）。壊す直前にできた結果の届き先は 1.x と違いうる（5.3。J-11 の範囲）。親課題の J-9 と Dialog（J-D2）の「新しい `Instance` を作って出す」から外れる（終了の間に GameObject を作り直さない）ので、README と `common.md` に書く。Manager が無効（`enabled = false`）でも届く。Chooser Action は、受け口の時点で Manager がいなければ捨てる（4.3。1.x と同じ） |
| J-S5 | per-call の枠 | 1.x の規則を保つ: 操作名ごとの枠に、呼ぶときに上書き（null も。`Mgr:331`）。**枠を取り出す時点も 1.x に合わせる**（`Mgr:399-406`）: 同期に決まる結果（Editor、C# の検査、ネイティブが使えない、入口の拒否、C# の例外、同期の Direct Share）は、呼んだスレッドで結果を作るときに取り出し、取り出した callback を閉じ込めた処理を取り出し口に入れる（`PostImmediateResult`）。ネイティブの完了は、完了の受け口（Android の main）で取り出す。枠は static で、`lock` で守る（ロックの中では辞書の読み書きだけ）。`common.md` と `Handoff` 4.2 の「受け口は写して積むだけ」の例外として書く（5.1）。順は共通イベント → per-call（`Mgr:415-419`）。共通イベントの購読者の例外は捕まえない（1.x と同じ） |
| J-S6 | 公開の `Initialize()` とリスナー | 残す。中身は、ネイティブが使えるかを確かめ、リスナー 2 つ（F-10、F-11）が未登録なら登録する。**登録は登録専用の `lock` の中で 1 回だけ行い、成功したハンドルを static に持つ**（2 回登録するとイベントが 2 回届く。受け口と共有の `lock` を使わないのは、登録の `extern` の間に Android の main を待たせないため）。プロセスで 1 回だけ登録し、外さない（static の受け口、`user_data` と `release` は NULL）。登録に失敗したら `LogWarning` を出し、次の `Initialize()`・`ShareText`・`ShareWithCallback` でやり直す。`Awake` から呼ぶ |
| J-S7 | null の引数 | `payload`、`filePaths` / `ids` の配列そのもの、`chooserActions` の要素（`ShareText` と `ShareWithCallback` の両方。1.x は `ShareWithCallback` でも JSON を組むときに例外になった。`Mgr:312`、`Json:120-129`）が null なら、`ArgumentNullException` を呼び出し側に投げる（1.x も呼び出し側に例外が出た。型だけが変わる）。**何も登録する前に**投げる（1.x は `chooserActions` の要素の null のとき、coordinator に登録した後で投げた。5.3） |
| J-S8 | 配列の要素の null | 検査の失敗 `Failed to <op>: filePaths must not contain null` / `... ids must not contain null`。C ABI は入口で `INVALID_PARAMETER` にするので、C# が先に言う |
| J-S9 | 複数のパスの文言 | `FILE_NOT_FOUND` は UnityMain で「`File.Exists(p) \|\| Directory.Exists(p)` が偽の最初のパス」（見つからなければ全部のパスを `, ` でつなぐ）、`ILLEGAL_FILE_ACCESS` は全部のパスを `, ` でつなぐ（C# から範囲を判定できない。5.3）。存在の確かめは文言のためだけ |
| J-S10 | ログ | 4.4 の末尾のとおり |
| J-S11 | `AndroidShareJsonBuilder` | 公開のまま残し、Manager からは使わない。4 機能の移行の後に JSON の層をまとめて消す。`[Obsolete]` は付けない |
| J-S12 | 選ばれたアプリ | **C# では照らさない**（1.x も照らさなかった。`Mgr:422-431`）。ネイティブが今の待ちの選択だけを届ける（1.5）。選択の受け口は、`package_name` を写して `finally` で解放し、処理を取り出し口に入れる。取り出し口の中で `ShareCallbackReceived` → その時点の `onSelected`（1 回で消す）の順に出す（`Mgr:422-431` と同じ）。`Handoff` 4.3 の「UnityMain で照らす」の例外として書く |
| J-S13 | 取り消し（`CancelPendingShareCallback`） | まず `onSelected` を消す（1.x と同じく Editor の判定より前。`Mgr:322-327`）。Editor・Android 以外・ネイティブが使えないときは失敗（J-S3、4.4）。Android の上では、**受け付けた選択の要求の ID をすべて取り消す**: `ShareWithCallback` が F-8 の戻り値 `NONE` を受けたら、その ID を static の一覧に足す（`AddSelectionId`。`lock`。古い順に 16 個まで）。取り消しは一覧を取り出して空にし（`TakeSelectionIds`）、それぞれに F-9 を呼ぶ。F-9 は今の待ちの ID でなければ何もしないので、どれが今の待ちかを C# は知らなくてよい（R2-3、C2-1。1.x は ID を使わず今の待ちを外した。`ShareCallbackCoordinator.kt` の `cancel`）。結果は Android の上ではいつも成功 |
| J-S14 | `ShareWithCallback` の失敗 | **どこで作った失敗でも**（`PostImmediateResult` と完了の受け口の両方）、per-call の枠を取り出すのと同じ `lock` の中で `onSelected` を消す（1.x と同じ。`Mgr:408-413`。R2-7） |
| J-S15 | `OnDestroy` | **自分が `_instance` のときだけ**（1.x と同じ。`Mgr:126`。シーンを読み直して 2 つ目の Manager が `Awake` で壊れたときに、生きている Manager の static の状態を消さないため。R3-1）、per-call の枠、`onSelected`、coordinator（`Clear()`）を空にし、取り消す ID の一覧のすべてに F-9 を呼ぶ（F-9 を呼ぶのは `UNITY_ANDROID && !UNITY_EDITOR` のときだけ。Editor では一覧を空にするだけ）（1.x の `clearShareOperationListener` が待ちを外したのと同じ。`Mgr:123-136`）。リスナーは外さない（J-S6）。前の Share のアクションは、作り直した Manager に届きうる（5.3） |
| J-S16 | 呼ぶスレッド | どのスレッドから呼んでもよい（1.x と同じ）。`EnsureCreated()` は UnityMain の外では何もしない（Dialog の R3-9）。結果はいつも UnityMain で出る。`EnsureCreated()` は null の検査の直後に呼ぶ。複数のスレッドから同時に呼んだときの per-call・アクション・取り消しの対応は 1.x と違いうる（5.3。J-11 の範囲） |
| J-S17 | 空白の判定 | 必須の値（`text`、`filePath`、Direct Share の `id` / `label` / `iconBase64`、アクションの `label` / `iconBase64` / `intentAction`）は `IsBlank`（Kotlin の `isBlank` と同じ: Unicode の Zs・Zl・Zp と U+0009〜U+000D、U+001C〜U+001F）。1.x は必須の値を C# では省かず、parser の `isBlank` だけで見た。任意の値（`title`、`subject`、`mimeType`、`previewTitle`、`previewThumbnailPath`、`category`）は `IsAbsent`（`IsBlank` か `string.IsNullOrWhiteSpace`）。1.x は C# の `IsNullOrWhiteSpace` で省き、parser の `isBlank` でも見た（`Json:24-28`、`Parser:19-24`。R2-12）。アクションの `intentAction` は 1.x では両方で見たので `IsAbsent` |
| J-S18 | 前の Share の Chooser Action | C# は世代を持たない（v2 の J-S17 を外した。J-11）。ネイティブが新しい `ShareText` の成功で前のアクションを止める（AP-7）。新しい `ShareText` が失敗したとき（画像として壊れたアイコン、`NOT_FOREGROUND`）と、Manager を壊して作り直したときは、前のアクションが押せるまま残り、今の per-call に届く（1.x は受け手を外していた。5.3） |

### 5.3 既知の差分（意図したもの）

XML コメントに書き、実装結果ファイル経由で次の版のマニュアル（移行ガイド）へ引き継ぐ。「要確認」の行は、1.x の実測（5.5 の手順 3）で直す。「根拠」は 1.x の振る舞いの出典。

| 差分 | 1.x | 2.0.0 移行後 | 根拠 |
|---|---|---|---|
| 後ろから呼ぶ | OS が黙って止めた見込み（起動は成功を返した。要確認） | `Failed to <op>: the app is not in the foreground.`。前面の判定は Sharesheet が出ている間も「前面」（1.9） | 手順 3 で実測 |
| 同期に決まる結果の per-call（J-S5） | 同じ操作の先の完了が、後の呼び出しの per-call を受けることがあった（検査も Kotlin の main で順に処理した） | 同期に決まる結果は、呼んだ時点の自分の per-call を受ける | `UnityAndroidShareManager.kt:317-321`、`Mgr:331` |
| 操作をまたいだ結果の順（J-S2） | 呼んだ順（Kotlin の main） | 同期に決まる結果（C# の検査などの失敗、Direct Share、取り消しの成功）が、先に呼んだ開く操作の完了より先に出ることがある（同じフレームで `ShareWithCallback` と `CancelPendingShareCallback` を呼ぶと、取り消しの成功が `onStarted` より先に出うる） | `UnityAndroidShareManager.kt:309-321` |
| 取り消しの成功を出す時点（C3-2） | Android の main で待ちを外した後 | 取り消しを Android の main に積んだ時点（外す前）。取り消しと同時に選ばれたアプリの通知が、取り消しの成功の後に出ることがある | `UnityAndroidShareManager.kt:309-326`、`Share.cpp:481` |
| UnityMain の処理の中から呼んだときの結果のフレーム（J-S2） | **Editor（と端末の C# 側の即時の失敗）では**、利用者のイベントの購読者や per-call の中から呼ぶと、同じループ（同じフレーム）で結果が出た。端末で Kotlin を通った結果は、Android の main の `Enqueue` がループの終わりを待つので、もとから次のフレーム以降 | どれも次のフレーム（取り出し口は、始めにあった件数だけを走らせる）。端末の正しい入力の操作では変わらない | `UnityMainThreadDispatcher.cs:45,57-62`、`Mgr:333-349`、`AndroidNativeCallbackQueue.cs:168-179` |
| Direct Share の呼び出し（4.3） | Android の main で非同期 | 呼んだスレッドで同期（ビットマップと Binder の分だけ呼び出しが長くなる）。結果は同じく後で出る | `UnityAndroidShareManager.kt:233-262` |
| 画像として読めない Chooser Action のアイコン | そのアクションを黙って飛ばした | 先頭の印が画像でないものは飛ばす（同じ）。印は当たるが中身が壊れている画像は、Share 全体が失敗 `Failed to shareText: a chooser action icon could not be read as an image.`。API 33 以下の端末（アクションが出ない）でも失敗する | タグ `1.12.0` の `android/library/.../ShareRepositoryImpl.kt:226-229,245-266` |
| 4.2.1 の印に無い画像の形式（WBMP など） | 表示した | 飛ばす | 同上 |
| 同じ `intentAction` のアクション | すべて表示し、どれを押しても同じ ID で通知した | 最初の 1 つだけ表示する | 同上、`ShareChooserActionInputs.kt:16-24` |
| 前の Share の Chooser Action（J-S18） | 新しい `shareText` を呼ぶと（parser を通った後）前の受け手を外した。Manager を壊すと外した | 新しい `ShareText` が成功すると止まる（同じ）。新しい `ShareText` が失敗したときと、Manager を作り直したときは、前のアクションが押せて、今の per-call に届く | `UnityAndroidShareManager.kt:127-134,145-167`、`ShareChooserActionReceiverRegistry.kt:43-64` |
| 古い Chooser で選んだアプリ | 届いた（不具合。SH-13） | 届かない | P2 の SH-13 |
| 複数のスレッドから同時に呼ぶ（J-S16） | Kotlin の main の順に処理した | per-call・アクションの届き先・取り消しの対応が、呼んだ順と違うことがある | `UnityAndroidShareManager.kt:317-321` |
| 結果が Manager を壊す前後に来る（J-S4） | 壊す前に積まれた結果は、壊した Manager のイベントと per-call に出た。壊した後は捨てた | 取り出した時点で生きている Manager に出し、いなければ捨てる（作り直していれば新しい Manager に出る） | `Mgr:123-136,399-420` |
| 複数のファイルの `FILE_NOT_FOUND` / `ILLEGAL_FILE_ACCESS` の文言（J-S9） | 原因のパス | 存在しない最初のパス / 全部のパス | `UnityAndroidShareManager.kt:323-400` |
| Direct Share の登録の失敗の文言 | `Failed to register Direct Share target: <理由>`（`push_failed` か例外の文言） | `Failed to register Direct Share target: the system did not accept the shortcut.` | 同上 |
| `SecurityException` の文言 | `Security restriction while executing <op>: ...` | 来ない（C ABI は `UNKNOWN` に写す）。`Failed to <op>: UNKNOWN.` | 同上 |
| そのほかの例外の文言 | 例外の文言を含む | 4.4 の固定の文言 | 同上 |
| `payload`・配列・`chooserActions` の要素が null（J-S7） | `NullReferenceException`（要素の null のときは coordinator に登録した後） | `ArgumentNullException`（何も登録しない） | `Json:120-141`、`Mgr:207-209` |
| 必須の文字列が null | JSON の `null` を parser が文字列 `"null"` として扱った見込み（`"null"` を共有、`File not found: null`、`"null"` の名前で登録など。要確認） | 空と同じ検査の失敗 | `Json:23,40,63,84-88,155-157`、`Parser:14-15,31,50,66-71` |
| 配列の要素の null（J-S8） | 文字列 `"null"` として扱った見込み（要確認） | 検査の失敗 | 同上 |
| Chooser Action の `label` / `iconBase64` / `intentAction` が null | `label` は `"null"` で表示した見込み（要確認） | そのアクションを捨てる | `Json:120-129` |
| Base64 の読める範囲 | Android の `Base64.DEFAULT`（パディングが無くても、アルファベット以外の文字が混じっても読む。要確認） | 前後の空白を除いた `Convert.FromBase64String`（パディングと、正しい文字だけを受ける。Unity の `Convert.ToBase64String` の出力は読める） | `UnityAndroidShareManager.kt:237-241`、`ShareRepositoryImpl.kt:254-255` |
| 文字列の途中の U+0000 | JSON の `\u0000` でそのまま運んだ | そこで切れる（本文が U+0000 で始まると `text is required`） | `Json:211-238` |
| 対になっていないサロゲート | そのまま運んだ | U+FFFD に置き換わる | 同上 |
| Editor（J-S3） | Android ターゲットのときだけ型があり、失敗のイベント | どのターゲットでも型があり、失敗のイベント（入力によらない。同じ） | `Mgr:3,353-362` |
| ログ（J-S10） | JSON を丸ごと出した（本文、パス、Base64 のアイコン） | 本文と `subject` は長さ、アイコンはバイト数 | `Mgr:218-285` |
| `Initialize()`（J-S6） | ブリッジを取り、リスナーを登録した | リスナーを登録するだけ。何度呼んでもよい | `Mgr:143-177` |
| per-call の枠の競合（J-S5） | ロックが無かった | ロックで守る | `Mgr:84` |
| 32 ビット（ARMv7）と Mono | 動いた（ブリッジは Java） | ビルドが止まる（親課題 J-10） | 親課題 6.4 |
| `AndroidShareJsonBuilder`（J-S11） | Manager が使った | 使わない。後の版で消す | `Mgr:197-316` |

### 5.4 呼び出しの流れと契約

**共通の前半**（9 操作）:

1. 引数の null（J-S7）: `ArgumentNullException`。ここでは何も登録していない
2. `AndroidNativeCallbackQueue.EnsureCreated()`（J-S16）
3. ログ（`ShareText` は、アクションの生の個数が 6 個以上なら今と同じ `LogWarning`。`ShareWithCallback` はアクションが 1 個以上なら今と同じ「使わない」の `LogWarning` だけ。`Mgr:203-206,308-311`）。per-call の枠に `onResult` / `onStarted` を入れる（`lock`。上書き）
4. Editor・Android 以外なら、`LogWarning` と `<op> could not be started.` を `PostImmediateResult(op, error)` で出して終わる（J-S3。`PostImmediateResult` は `lock` の中で枠を取り出し、`shareWithCallback` なら `onSelected` も消し、取り出した callback を閉じ込めた処理を取り出し口に入れる。J-S5、J-S14）
5. 4.2 の検査。失敗なら `LogWarning` と `PostImmediateResult` で終わる
6. `EnsureNativeAvailable()` が `Available` でなければ、4.4 の失敗を `PostImmediateResult` で出して終わる

**`ShareFiles`**（開く操作の例）: 7. `ShareCall`（操作、パス）を作る。確保の一覧を空に作り、`AllocStringArray` で配列を作る。8. `CallNative(call, allocated, key => ntk_share_files(paths, count, s_donePointer, key, s_releasePointer))`。9. `finally` で確保したものを全部解放する。

**`ShareText`**: 前半の 3 で、per-call の枠に加えて coordinator に `onChooserAction` を登録する（1.x と同じく、検査の前。`Mgr:207-209`。null も登録として扱う）。6 の後に `NormalizeActions`（4.2.1）、`BuildTextContent`、`BuildActions` を作り、`CallNative(call, allocated, key => ntk_share_text(ref content, actions, count, s_donePointer, key, s_releasePointer))`。

**`ShareWithCallback`**: 前半の 3 で、`onStarted` を枠に、`onSelected` を static の枠に入れる（1.x と同じく、呼ぶときに上書き。`Mgr:303-316`）。アクションがあれば `LogWarning`。6 の後に `BuildTextContent` と `bool accepted = CallNative(call, allocated, key => ntk_share_text_for_selection(ref content, s_donePointer, key, s_releasePointer, out requestId))`。`accepted` なら、`CallNative` の外（`try` の外）で `AddSelectionId(requestId)`（J-S13）。

**`CancelPendingShareCallback`**: `onSelected` を消す（Editor の判定より前）→ 前半の 2〜4、6 → `TakeSelectionIds()` のそれぞれに F-9 → 成功を `PostImmediateResult`。

**`RegisterDirectShareTarget` / `RemoveDirectShareTargets`**: 前半の後、構造体か配列を作って `extern` を呼び、戻り値から結果を作って `PostImmediateResult`（同期に決まる結果。`user_data` は使わない）。

**`CallNative`**（開く操作の共通部。層 2a から `invoke` を差し替えられる）:

```csharp
internal static bool CallNative(ShareCall call, List<IntPtr> allocated, Func<IntPtr, int> invoke)
{
    string? error = null;
    long key = 0;
    bool returned = false;
    try
    {
        key = AddCall(call);
        int code = invoke(new IntPtr(key));   // 完了と release は、これが戻る前に Android の main で来うる
        returned = true;
        if (code != AndroidShareCApi.ErrorNone) error = AndroidShareCApi.FailureMessage(call, code, entry: true);
    }
    catch (Exception e)
    {
        error = e.Message;
        // extern が戻らなかった（関数に入れなかった。ネイティブは managed の例外を投げない）なら、release は来ない
        if (key != 0 && !returned) RemoveCall(key);
    }
    if (error != null) PostImmediateResult(call.Operation, error);
    return error == null;   // 受け付けた（完了はこの後、またはもう来ている）
}
```

- `AddCall(ShareCall)` は表に入れてキーを返し、`RemoveCall(long)` は表から消す（どちらも Manager の static、`lock`）
- `NONE` の後の処理（`AddSelectionId` やログ）は `CallNative` の外に置く（受け付けた後に例外で失敗を出すと、完了でも結果が出て、枠を 2 回取り出すため。R2-14）
- 完了の受け口（Android の main。`OnDoneCore`）: 表から key を**読み**、`lock` の中で枠を取り出し（`shareWithCallback` の失敗なら `onSelected` も消す）、`Post(() => 生きている Manager の RaiseOperation(call, error, callback))` を入れる
- `release` の受け口（`OnReleaseCore`。Android の main か、入口の拒否なら呼んだスレッド）: 表から key を消す
- `RaiseOperation`（UnityMain）: 結果を作り、`ShareOperationCompleted` → 閉じ込めた per-call の順に呼ぶ
- 選択の受け口（`[MonoPInvokeCallback]`）: `try { string? pkg = null; if (package_name != IntPtr.Zero) { try { pkg = ReadString(package_name); } finally { ntk_string_free(package_name); } } OnSelectionCore(requestId, pkg); } catch { LogError }`
- アクションの受け口: `try { string id; try { id = ReadString(action_id); } finally { ntk_string_free(action_id); } OnChooserActionCore(id); } catch { LogError }`。`OnChooserActionCore` は SEND を捨て、static の参照から今の coordinator を読み、null でなければその場で（Android の main で）`Fire(...)` を呼ぶ（`Fire` が per-call を閉じ込めて `Post` する）
- **どのスレッドでも完了も `release` も待たない**（親課題 A-4）

### 5.5 実装順

各段階の終わりに `scripts/verify_unity_android.sh`（EditMode、Editor の PlayMode、実機の IL2CPP、パッケージの利用者の条件）を流す。

1. UI テスト計画 v1（7.3 に番号を振る）をこの設計のレビューの後に作る
2. **UI の補助を Share に広げる**（8.3）。そのうえで次を確かめる（要検証）: Sharesheet が出ている間に Unity が止まるか（`OnApplicationPause`）、依頼を先に出して同じフレームで開く形で押せるか、Sharesheet にこのアプリ自身が出るか・何番目か、選ぶと Player が前に戻るか（`singleTask`）、Direct Share の共有先が出るか
3. **1.x のまま** `AndroidSharePlayerTests` と `AndroidShareManagerPlayerTests` のうち 1.x で流せるもの（UI テスト計画の「1.x」の列）を書いて流し、結果を記録する。5.3 の「要確認」の行（後ろから呼ぶ、null の各項目、Base64 の範囲）のうち、ふつうの使い方に当たるもの（後ろから呼ぶ）を実測する。違えば設計書の新しい版にする
4. 共通部（`AllocStringArray` の移動）と Dialog の呼び出しの差し替え。Dialog のテストが同じに通ること
5. `AndroidShareCApi` の構造体と純粋な関数、層 1
6. `AndroidShareCApi` の `extern`
7. `AndroidShareManager` の置き換え（J-S1〜J-S18）と層 2a
8. 2.0.0 の列のテスト（C ABI にしか無いもの）を足し、全体を流す。1.x の列と比べ、違いが 5.3 の表にあることを確かめる
9. `common.md`、`testing.md`、親課題の README
10. 実装結果ファイル（`artifact/android/share/results/`）に 5.3 と 4.4、UI テスト計画の 2 列の結果を書き写す

## 6. エラーケース一覧

| # | 状況 | 発生元 | 返り方 | 検証の層 |
|---|---|---|---|---|
| E-1 | `payload`・`filePaths` / `ids` の配列・`chooserActions` の要素が null | C# | `ArgumentNullException`（何も登録しない） | 2a |
| E-2 | 必須の値が null・空・空白 | C# | 呼び出しの後、1.x の文言、`LogWarning` | 1（検査、`IsBlank`）、2b（`AndroidShareManagerPlayerTests`） |
| E-3 | 配列の要素の null（J-S8） | C# | 同上 | 1、2b |
| E-4 | Direct Share の `iconBase64` が Base64 でない | C# | 同上、`Invalid icon data for Direct Share target: <id>` | 1、2b |
| E-5 | Chooser Action の整え（捨てる、重複、画像の印、SEND の通知） | C# | 失敗にしない | 1（`NormalizeActions`、`IsNotifiedActionId`）、2b（表示と通知の ID） |
| E-6 | Editor・Android 以外（J-S3） | C# | 呼び出しの後、`<op> could not be started.`（入力によらない。取り消しも） | 2a（9 操作、不正な入力でも同じ） |
| E-7 | ネイティブが使えない | C# | 4.4 | 1（例外の分類と版、文言） |
| E-8 | 入口の `INVALID_PARAMETER` / `NOT_SUPPORTED` / `INVALID_CHOOSER_ACTION` | ネイティブ（入口） | `LogError` | 1（文言）。正しい実装では起きない |
| E-9 | `NOT_INITIALIZED` | ネイティブ（入口） | 4.4 | 1（文言）。起こせない |
| E-10 | `NOT_FOREGROUND` | ネイティブ（完了） | 4.4 | 1（文言）、2b（試す。Dialog の AD-27 と同じ形） |
| E-11 | `FILE_NOT_FOUND`（1 つ・複数） | ネイティブ（完了） | パスを含む文言 | 1（文言と `MissingPath`。ディレクトリを含む）、2b |
| E-12 | `ILLEGAL_FILE_ACCESS`（1 つ・複数） | ネイティブ（完了） | 同上 | 1、2b（`/system/etc/hosts` のような範囲外の実在のファイル） |
| E-13 | `INVALID_CHOOSER_ACTION`（印は当たるが壊れた画像のアイコン） | ネイティブ（完了） | 4.4 | 1（文言）、2b（PNG の印だけのバイト列） |
| E-14 | `EMPTY_CONTENT`（完了） | ネイティブ（完了） | `text is required` | 1（文言）。J-S17 で同じ判定にしたので起きない |
| E-15 | `INVALID_ICON`（Direct Share、Base64 は読めるが画像でない） | ネイティブ（同期） | 4.4 | 1、2b |
| E-16 | `DIRECT_SHARE_REGISTRATION_FAILED` | ネイティブ（同期） | 4.4 | 1（文言）。起こせない |
| E-17 | `NO_SHARE_TARGET` | ネイティブ（完了） | 4.4 | 1（文言）。起こせない |
| E-18 | `INVALID_MIME_TYPE` | ネイティブ（完了） | 4.4 | 1（文言）。起きない |
| E-19 | `UNKNOWN` / `OUT_OF_MEMORY` | ネイティブ | 4.4 | 1（文言）。起こせない |
| E-20 | C# の例外（`extern` を呼べない） | C# | `ex.Message`、`LogError`。表から消す | 2a（`CallNative` の `invoke` に例外を投げる関数を渡し、失敗が 1 回、`PendingCallCountForTests` が 0） |
| E-21 | 受け口の中の例外 | C# | 出ない、`LogError` | コードレビュー。起こせない |
| E-22 | リスナーの登録の失敗（J-S6） | ネイティブ（入口） | `LogWarning`、次の呼び出しでやり直す | コードレビュー。起こせない |
| E-23 | 入口の拒否の `release` が戻る前に来る / 完了の直後に `release` が来る（J-S1） | ネイティブ | 表から消える。完了は表を読んだ時点の記録で出る | 2a（`CallNative` の `invoke` の中で `OnDoneCore` と `OnReleaseCore` を続けて呼ぶ、`OnReleaseCore` だけを呼んで `NONE` 以外を返す）、2b（ふつうの完了で表が 0 に戻る） |

## 7. テスト方針

### 7.1 層 1（EditMode、`AndroidShareCApiTests`）

- 3 つの構造体の大きさと全フィールドの位置（56 / 32 / 48）
- 要求の ID が 64 ビットであること: `SelectionCallback` の 2 番目の引数の型が `ulong`（反射）。`out ulong` と取り消しの `ulong` は層 2b
- 構造体の組み立て: `struct_size`、`reserved0 = 0`、任意の値の空白が NULL になること、文字列の読み戻し、Chooser Action の配列の並びとアイコンのバイト列と大きさ、Direct Share の `category` の NULL
- 検査（4.2）: 各操作の必須の値の null・空・空白、配列の空と要素の null、Base64 でないアイコン。文言
- `IsBlank` と `IsAbsent`（J-S17）: U+0020、U+3000、U+001C、U+0085、U+00A0、ふつうの文字
- `DecodeIcon`: 正しい Base64、前後の空白、読めないもの
- `LooksLikeImage`: 7 つの印、短すぎる、ほかの印
- `NormalizeActions`（4.2.1）: 空白・null の項目を捨てる、Base64 でないもの・画像の印が無いものを捨てる、重複は最初だけ、C に渡す形（U+0000 で切る、サロゲート）で比べる、順を保つ、SEND を残す
- `IsNotifiedActionId`: SEND は偽、ほかは真
- `AddSelectionId` / `TakeSelectionIds`（J-S13）: 足した順、16 個を超えたら古いものから落ちる、取り出すと空になる
- 文言（4.4）: 全エラー値と想定外の値、入口と完了の別、9 操作の `<op>`、`MissingPath`（1 つ、複数、どれも在る、ディレクトリ）
- `AllocBytes` の読み戻し

`AndroidNativeToolkitCApiTests`（共通部）: 移した `AllocStringArray` の読み戻し。

### 7.2 層 2a（PlayMode・Editor、`AndroidShareManagerEditorTests`）

- Editor の 9 操作（J-S3）: 呼び出しの中ではイベントが出ず、取り出し口が回った後に `ShareOperationCompleted` → per-call の順に 1 回、`<op> could not be started.`。**不正な入力（空の本文、空の `filePaths`）でも同じ文言**（1.x と同じ）。`CancelPendingShareCallback` も失敗で、`onSelected` は消える
- per-call の枠（J-S5）: Editor で同じ操作を同じフレームに 2 回、別の callback で呼ぶと、それぞれが自分の失敗を 1 回ずつ受ける。null の上書き
- 完了の時点の枠（J-S5）: `SetPendingOperationForTests` で callback を入れ、`AddCall` した記録に `OnDoneCore` で完了を入れた後、取り出し口が回る前に同じ操作の枠を上書きしても、その完了は前の callback を呼ぶ
- `ShareWithCallback` の失敗（J-S14）: Editor で `ShareWithCallback(..., onSelected)` を呼んだ後に `OnSelectionCore` を入れても、`onSelected` は呼ばれない（`ShareCallbackReceived` は出る）
- 完了の受け口の `onSelected`（J-S14、R3-3）: `AddCall`（`shareWithCallback` の `ShareCall`）→ `SetPendingSelectedForTests(cb)` → `OnDoneCore(key, NOT_FOREGROUND)` → 取り出し口を回す → `OnSelectionCore(id, "pkg")`: `cb` は呼ばれず、`ShareCallbackReceived` は出る。同じ手順で `OnDoneCore(key, NONE)` なら `cb` が 1 回
- `OnDestroy`（J-S15、R3-1）: `SetPendingOperationForTests`、`SetPendingSelectedForTests`、`AddSelectionIdForTests` で状態を入れ、2 つ目の Manager を作り（`Awake` で壊れる）、1 フレーム待ってから、per-call・`onSelected`・取り消す ID の一覧（`SelectionIdCountForTests`）が残ること
- 各テストの `SetUp` で `ResetStaticStateForTests()` を呼ぶ（static の状態を持ち越さない）
- null（E-1）: `ArgumentNullException`。per-call も coordinator も登録されない
- `user_data` と `release`（E-20、E-23）: `CallNative` に次の `invoke` を渡す。(a) 中で `OnDoneCore(key, NONE)` と `OnReleaseCore(key)` を続けて呼んで `NONE` を返す: 表が空になっても成功が 1 回、正しい操作で出る。(b) 中で `OnReleaseCore(key)` だけを呼んで `INVALID_PARAMETER` を返す: 失敗が 1 回、表が空。(c) `DllNotFoundException` を投げる: 失敗が 1 回、表が空
- 配送（J-S4）: 結果を取り出し口に入れ、Manager を無効にしても届く。Manager を壊すと捨てる（新しい Manager を作らない）
- 選ばれたアプリ（J-S12）: `SetPendingSelectedForTests` で `onSelected` を入れ、`OnSelectionCore(id, "pkg")` を入れると、`ShareCallbackReceived` → `onSelected` の順に 1 回。もう一度入れると `ShareCallbackReceived` だけ。`package_name` が NULL（`null`）でも出る
- Chooser Action: `OnChooserActionCore(id)` は coordinator を通って共通イベント → per-call の順に出る。per-call は消えない。SEND は出ない。**`OnChooserActionCore` を呼んだ後、取り出し口が回る前に `ShareText(…, 新しい per-call)` を呼んでも、そのアクションは前の per-call に届く**（受け口の時点で閉じ込める。C5-2）。Manager を壊した後の `OnChooserActionCore` は何も出さない

### 7.3 層 2b（実機の Player）

個々のテストと期待値は UI テスト計画 v1 に番号を振る。**IL2CPP の ARM64 だけ。**

**Sharesheet の操作の約束（R2-2、R3-8、R4-5）**: 補助の Sharesheet の命令（`tap-in-sheet`、`absent-in-sheet`、`present-in-sheet`）は、命令の全体に 60 秒の期限を持ち、超えたら探すのをやめて `fail` を返す（8.3）。失敗でも Sharesheet を閉じてから答える。テストの `Wait` の期限はそれより長い 90 秒にする。出した依頼は、失敗の経路でも必ず `Wait` する（後片付けで回収する）。 Sharesheet が出ている間、Unity は止まる見込み（5.5 の手順 2 で確かめる）。そのため、テストは**補助への依頼を先に出し、同じフレームで Share を開く**。補助は Sharesheet が出るのを待ってから押し、答えのファイルを書く。Unity が戻った後に、テストが答えを読む（`AndroidUiDriver` に「依頼だけを出す」と「答えを待つ」を分けた口を足す。8.3）。1 つの依頼は、それだけで終わる操作にする（「出たことを確かめて Back」「探して押す」）。Sharesheet が出ている間に C# から呼ぶ必要がある操作（取り消し）は、開く呼び出しと同じフレームで呼ぶ（F-9 は main で開く処理の後に動く。1.5）。

**`AndroidSharePlayerTests`（UI を使う。カテゴリ `AndroidUiDriver`）。** 後片付けは Dialog の形（`Safely`、要求の数とイベントの数の照合、イベントのスレッドの照合）を写す。Sharesheet が残っていれば Back で閉じる。

- 開く操作（テキスト、URL、題名と件名、プレビュー、画像、複数の画像、ファイル、複数のファイル）: 依頼「Sharesheet が出たら Back」を出して開く。成功の結果が 1 回、依頼の答えが ok。テキストの 1 件は、依頼「本文が在ることを確かめて Back」（`present-in-sheet`）で、Sharesheet のプレビューにテストの本文が出ることも確かめる（本文はプレビューで切れない短い自分の値。構造体の写しを端から端まで押さえる。R3-9、R4-6）
- `Initialize()` を 2 回呼んだ後のアクションと選択（J-S6、R3-9、C3-4）: イベントが 1 回ずつ
- Chooser Action（API 34 以上の端末）: 依頼「アクションのラベルを探して押す」を出して開く。`ShareChooserActionTapped` → per-call の順に 1 回、ID が `intentAction`。捨てたアクション（空白、Base64 でない、画像の印が無い、重複の 2 つ目）は出ない（依頼「このラベルが無いことを確かめて Back」）。SEND のアクションは出るが通知は来ない。タップ 1 回にイベント 1 回
- 選ばれたアプリ: 依頼「このアプリ自身を探して押す」を出して `ShareWithCallback` を開く。`ShareCallbackReceived` に自分のパッケージ名が 1 回、`onSelected` が 1 回。Player は前に戻る。依頼「Back」なら何も来ない。開く呼び出しと同じフレームで `CancelPendingShareCallback` を呼び、依頼「このアプリ自身を押す」なら何も来ない（`ulong` の ID が正しく渡ったことの確かめ）
- Direct Share の表示: 登録した共有先の short label が Sharesheet に出ることを試す（このリポジトリは `<share-target>` を宣言済み）。出るまでに時間がかかるなら期限を決め、出なければ理由を書いて手動に回す。登録した共有先は後片付けで消す（残ると Sharesheet の並びが変わり、ほかのテストの探索に響く）
- 取り消しを同じフレームで呼ぶテストは、`onStarted` と取り消しの成功の順を仮定しない（5.3）
- 失敗で Sharesheet が出ないもの: 画像として壊れた Chooser Action のアイコン（E-13）
- 後ろから呼ぶ（E-10）: Dialog の AD-27 と同じ形

**`AndroidShareManagerPlayerTests`（UI を使わない。カテゴリを付けない）。** 補助がいない実行でも走る。

- Direct Share の登録・削除の成功が 1 回。UnityMain から呼び、戻った時点で 0 回
- 無いファイル（E-11）、範囲外の実在のファイル（E-12）、複数のときの文言、画像でない Direct Share のアイコン（E-15）、C# の検査の失敗（E-2〜E-4）。Sharesheet が開かない経路だけ
- 呼ぶスレッド（J-S16）: 別のスレッドから `RegisterDirectShareTarget` を呼び、例外が出ず、結果が UnityMain で 1 回
- 表の後片付け（E-23）: 各テストの後、数フレームのうちに `PendingCallCountForTests` が 0 になる（完了の受け口が積んだ後で `release` が来るので、結果を受けた直後は 1 のことがある。R3-7）
- Direct Share の登録のテストは、後片付けで共有先を消す

### 7.4 層 3・手動

- 層 3: 7.3 の画面の照合が層 3 に当たる
- 手動: API 33 以下の端末（Chooser Action が出ないこと）。手元に端末が無い。Direct Share の表示が自動で確かめられなかったとき

## 8. 自動化の前提

### 8.1 検証の層

6 章の「検証の層」の列と 7 章のとおり。起こせない失敗（E-9、E-16、E-17、E-19、E-21、E-22）は文言の変換を層 1 で押さえ、形をコードレビューで押さえる。

### 8.2 OS が出す画面・許可・前提の OS 設定

- OS が求める許可: なし（Share に固有の権限は無い）
- 前提の OS 設定: 画面が点いていて、ロックが解けていること（補助が点け、ロックを外す。PIN のある端末では止める）。アニメーションと自動入力は補助が切る
- **Sharesheet が出ている間、アプリの Activity は `PAUSED` になり、Unity は止まる見込み**（要確認。5.5 の手順 2）。7.3 の約束はこれを前提にする
- Chooser Action は API 34 以上の端末でだけ確かめられる（Pixel 6a は Android 16 で可）
- Sharesheet の共有先の一覧は端末に入っているアプリで変わる。テストは、このアプリ自身とラベルの文言だけを押す

### 8.3 呼び出し側を止める OS の画面

Sharesheet は非同期で、呼び出し側を止めない。ただし選ばれたアプリの結果は人が押すまで来ず、Sharesheet が出ている間は Unity が止まる見込み。**外から押す手段は、Dialog で作ったホストの adb の補助を広げる**（補助の設計書の新しい版にする）:

- **依頼を先に出し、後で答えを待つ口**（`AndroidUiDriver` に `Send(command, args)` で番号を返し、`Wait(number)` で答えを待つ。今の `Do` はこの 2 つをつなげたもの）
- **Sharesheet の命令はどれも、命令の全体に 60 秒の期限を持つ**（スワイプの回数より期限を先に見る。補助の `dump` は 1 回で最大 15 秒かかるため、回数だけでは上限にならない。R4-5）。超えたら `fail` を返す。**失敗・見つからない・期限切れのどれでも、Sharesheet のパッケージ（`com.android.intentresolver`）が画面にあれば Back で閉じてから `fail` を返す**（Sharesheet が出ている間は Unity が止まる見込みなので、C# の後片付けでは閉じられない。R5-1）。期限は手順の合間に見るので、最悪の時間は 60 秒と、読み取り 1 回（adb の呼び出し 3 つ）と Back の分。テストの `Wait` の期限 90 秒はその余裕を見た値（R5-2）
- **Sharesheet の中の文言を探して押す命令**（例: `tap-in-sheet text=<Base64>`。Sharesheet のパッケージ `com.android.intentresolver` が出るのを待ち、その中で文言を探し、無ければ上へスワイプして 8 回まで探し直す。native-toolkit の `UiDriver.kt:219-228` のやり方。**`:229-231` の「見つからなければ画面の文言を全部ログに出す」は写さない**）。ラベルが無いことを確かめる命令（`absent-in-sheet`）と、文言が在ることを確かめる命令（`present-in-sheet`。テストの本文の確かめ。R4-6）も同じ形で、確かめた後に Back で Sharesheet を閉じる
- 出たことを確かめて閉じるのは、今の `back`（`package=` のセレクタ）で足りる。出ているかを数えるのも、今の `count`（`package=`）で足りる
- **プライバシー**（native-toolkit の UI テストの決まりと同じ）: Sharesheet にはほかのアプリの共有先と連絡先が出る。
  - Sharesheet のパッケージが画面にある間は、失敗のときの画面の構造（`--dump-dir`）を書き出さない
  - 読み取りの後に、端末の上のダンプ（`/data/local/tmp/ntk-ui-dump.xml`）を消す
  - Sharesheet の中の要素の探索や `expect` が失敗したとき、理由に読み取った値を入れない
  - 読み取った構造は判定にだけ使い、ログに出さない
- **テストの共有先**: このアプリ自身を使う。既存の androidlib（`Assets/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib`）が `UnityPlayerGameActivity` に `SEND`（`*/*`）を宣言しているので、Sharesheet にアプリ名で出る見込み。選ぶと Player が `SEND` で前に戻る（生成された manifest では `launchMode="singleTask"`）。この宣言は既存で、この移行では変えない。**要検証**（5.5 の手順 2）: Sharesheet が呼び出し元のアプリ自身を一覧から除かないこと。除くなら、既存の androidlib に別のラベルの `<activity-alias>` を足す案を検討する（その場合は、すべてのビルドに入ることを意図した変化として書く）

補助がいない実行では、UI を使うテストは `Assert.Ignore` になる。`verify_unity_android.sh` のゲート（カテゴリ `AndroidUiDriver` の skip が 0）は Dialog と同じ。

### 8.4 アプリ側の設定（利用者の責任）

- FileProvider: 要らない（AAR に入っている。1.8）
- Direct Share を Sharesheet に出すには、アプリの manifest に `<share-target>` が要る。AAR は宣言していない（1.x と同じ）。マニュアルは説明している（`docs/1.11.0/share.ja.md:76-104`）。このリポジトリのサンプルは宣言済み
