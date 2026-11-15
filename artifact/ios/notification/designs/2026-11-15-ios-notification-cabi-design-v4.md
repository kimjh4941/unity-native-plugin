# iOS Notification の C ABI `2.0.0` への写し方の設計 v4

## 基本情報

| 項目 | 内容 |
|---|---|
| 親 | [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（Dialog・Share・Clipboard の次、最後） |
| 目的 | `IosNotificationManager` の公開の API（JSON の文字列を受ける操作、結果の型、2 つのイベント、一覧の JSON）を変えずに、3-S の「使えない」の経路から native-toolkit の C ABI `2.0.0`（`Notification.h`）の呼び出しに替える。2c で決めることになっていた期待（テストの `PendingOn2`・`pendingOn2`）をここで決める |
| 一次ソース | パッケージの `Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Notification.h`。native-toolkit の作業ツリー（HEAD `1df83126`）の `ios/IosLibraryCApi/IosLibraryCApi/Notification/`（`NotificationReceiver`・`NotificationMapping`・`NotificationContentBuilder`・`NotificationJson`）と `ios/IosLibrary/IosLibrary/Notification/`、1.x はタグ `1.13.0` の `ios/UnityIosPlugin/UnityIosPlugin/Notification/`（`UnityIosNotificationJsonParser.swift`・`UnityIosNotificationManager.swift`）と `ios/IosLibrary/IosLibrary/Notification/`、申し送り `artifact/topics/ios-c-abi/results/2026-11-07-ios-c-abi-stage2c-handoff.md`、設計 part2 の 8.3（NT-01〜NT-21）と 11.2。1.x の C# は `3cae23e^` |
| 1.x の記録 | [../results/2026-11-15-ios-notification-1x-record-result-v1.md](../results/2026-11-15-ios-notification-1x-record-result-v1.md)（Player の NP-1〜18、UI の NU-1〜16。実機あり） |
| 手本 | Share の呼び出しごとの表（`IosSharePendingCalls`）、Clipboard の JSON の読み手（`IosClipboardJsonReader`）と後のフレームに渡す部品（`IosClipboardLaterPump`） |
| レビュー | [../reviews/2026-11-15-ios-notification-cabi-design-review-v1.md](../reviews/2026-11-15-ios-notification-cabi-design-review-v1.md)（1 回目。A1 がサブ 2 件、Codex 5 件）、[../reviews/2026-11-15-ios-notification-cabi-design-review-v2.md](../reviews/2026-11-15-ios-notification-cabi-design-review-v2.md)（2 回目。`codex:codex-rescue` の A1 が 2 件）、[../reviews/2026-11-15-ios-notification-cabi-design-review-v3.md](../reviews/2026-11-15-ios-notification-cabi-design-review-v3.md)（3 回目。2 人とも A1 が 0 で止めた。v4 は止めた後の直しで、レビューしていない） |

### v3 からの差分（レビューしていない）

レビュー v3 の 3 章の表のとおり: D-3（ネイティブの結果を並べる所でもポンプを付け直す）、D-16・D-5 (a)（Schedule・Update の content の `id` は U+0000 で拒まない）、D-4（読めない数と 2^53 を超える整数は写さない）、3 章の settings request の行と 2 行、4 章のファイル、V-2 の件数。解放の回数と、起動時の install 無効から `Initialize()` で登録し直すことは確かめない（判断）

### v2 からの差分

レビュー v2 の 3 章の表のとおり。主なもの: D-16（`fileURL`・音の名前も拒む、引数は 1.x と同じく切れる）、D-4（添付は空の識別子でも残す、型の橋渡しを 1.x の Swift に合わせる）、D-2（ハンドルの所有、受け口の芯を Editor で組む）、D-3（ネイティブの結果も待ちの後ろ、主スレッドの外）、D-12（settings request は受けない、値の飛ばし方、イベントの時点）、3 章・4 章・5 章の行

### v1 からの差分

レビュー v1 の 3 章の表のとおり。主なもの: D-3（後のフレームに渡す共通の待ち行列）、D-2（破棄で表を空にしない）、D-12（受け手は外さない、settings request で設定を開く）、D-13（Awake は登録だけ）、D-16（U+0000）、D-4・D-5（配列の全か無か、型の橋渡し、空の識別子・音・`fileURL`、setter の失敗）、D-1（iOS だけの型を使わない）、D-17（サンプル）、3 章の行

## 1. 1.x と C ABI の要点

| # | 事実 | 出典 |
|---|---|---|
| F-1 | 1.x のブリッジは JSON を Swift で読んだ。content は `id`・`title`（文字列、必須。無いか型が違えば `Failed to parse notification content JSON`）。ほかの項目は型が違えば黙って無視。`sound` は省略で既定、`"default"`・`"defaultCritical"`、ほかの文字列は名前付き（鳴らさない指定は無い）。`interruptionLevel` は `passive`・`timeSensitive`・`critical` 以外の文字列が `active`。`userInfo` はオブジェクトでなければ `{}`。`attachments` は `{identifier, fileURL}` で、欠けたか URL でない要素は捨てた | `1.13.0` の `UnityIosNotificationJsonParser.swift:32-88` |
| F-2 | trigger は `timeInterval`（`interval`、`repeats`）、`calendar`（`year`〜`second`、どれも省略可）、`location`（`identifier`・`latitude`・`longitude`・`radius` が必須、`notifyOnEntry` 既定 true、`notifyOnExit` 既定 false、`repeats` は常に false）。読めなければ nil で、Show と Update は即時に出し、Schedule は `Failed to parse notification content or trigger JSON`。範囲は見ず、0 以下の interval や繰り返しで 60 未満はプロセスが落ちた | 同 `98-140`、part2 6.3 の M5 |
| F-3 | category は `identifier` 必須（無ければ `Failed to parse category JSON`）。`identifier`・`title` の欠けた action、4 項目の欠けた text input action は黙って捨てた。未知の option は無視。RegisterCategory は書き込みの前に入口の中で成功を返した。同じ識別子の登録し直しは置き換わらなかった（NU-12） | 同 `151-200`、`UnityIosNotificationManager.swift:200-201` |
| F-4 | 1.x の失敗の文は、JSON の読み違いが固定の英文、ライブラリの失敗が `The operation couldn’t be completed. (IosLibrary.NotificationError error N.)` の汎用の文（端末の言語で訳される）。実測は 1 = 追加の失敗、2 = badge、4 = 権限の拒否 | 1.x の記録 1 章、NU-2 |
| F-5 | 1.x の C# は、操作の結果を次の `Update` に「その呼び出しの受け手 → `NotificationOperationCompleted`」の順で出した。受け手は操作ごとに 1 つの枠で、続けて呼ぶと後が勝った（NP-3・18） | `3cae23e^` の `IosNotificationManager.cs`（474〜492 行）、1.x の記録 |
| F-6 | 1.x のイベント: テキスト入力の応答はテキスト入力の受け手へ、ほか（本文のタップ・アクション・削除）はアクションの受け手へ。actionId はシステムの識別子もそのまま。userInfo は C# の平らな読み手（エスケープを解かず、文字列でない値で読むのを止める）。起動時のタップは届かなかった（NU-8） | `1.13.0` の `IosNotificationManager.swift:335-352`、`3cae23e^` の `IosNotificationActionResult.cs`（36〜79 行） |
| F-7 | 1.x の一覧の JSON: 要素は `identifier`・`title`・`categoryIdentifier`（常にある）、`subtitle`・`body`（空でないときだけ）、配信済みだけ `date`（UTC、小数秒なしの ISO 8601）。userInfo と trigger は返さない | `UnityIosNotificationManager.swift:118-156`、NU-7 |
| F-8 | C ABI: すべての `_async` は `user_data` と `release` を取り、受け付ければ完了はちょうど 1 回（主スレッド。主スレッドで呼べば呼び出しの中では来ない）、拒めば完了は来ない。NULL のコールバックは拒む。エラーは数だけで `system_code` は 0 | `Notification.h:25-39`、`NotificationReceiver.swift:78, 118-136` |
| F-9 | C ABI の入口と値の検査: content の id、Schedule・Update の引数の identifier、action・text input action の識別子は空を拒む、NAMED の音は空の名前を拒む、未知の interruption level・範囲外の relevance score・負の badge（-1 は「設定しない」）・`file://` でない添付・null を含むか予約のキー `com.jonghyunkim.nativetoolkit` を持つ userInfo を拒む。trigger は interval が有限で 0 より大きく繰り返しなら 60 以上、calendar のビットが 0 でない、location の値の範囲と「入る・出るの少なくとも一方」。category は識別子の空を拒み、未知のビットを拒む。すべて `INVALID_PARAMETER` | `NotificationContentBuilder.swift:73-94, 167-259`、`NotificationMapping.swift:48-135`、`NotificationJson.swift:15-91` |
| F-10 | 権限: Show・Schedule・Update は権限（authorized・provisional・ephemeral）が無ければ `PERMISSION_DENIED`（10）で完了し、何も足さない（1.x は黙って成功した。NP-4・5・5b、NU-2b）。RequestPermission の拒否は `NONE` で granted 0、未決で前面でなければ `NOT_FOREGROUND`、要求そのものの失敗は `AUTHORIZATION_FAILED`。HasPermission は ephemeral を 0 | `NotificationReceiver.swift:159-178, 286-339`、`NotificationMapping.swift:162-173` |
| F-11 | category は同じ識別子を置き換え、完了は書き込みと 1 回の読み返しの後 | `NotificationCategoryQueue.swift:152-172`、`Notification.h:291` |
| F-12 | イベントは 1 つの interaction の受け手にまとまる（kind 0 本文のタップ、1 アクション、2 削除、3 テキスト入力。action_id はシステムの識別子もそのまま）。受け手が無い間は最大 32 件を保ち、次の受け手 1 つに届ける（起動時のタップは `is_launch` 1 で届く）。受け手の登録は、delegate が一度も入っていなければ `NOT_INITIALIZED`。delegate はパッケージの `NtkNotificationInstaller.mm` が起動時に入れる（3-S） | `NotificationReceiver.swift:440-502`、`Registry.swift:318-459`、`Notification.h:300-314` |
| F-13 | userInfo の JSON は、C ABI がキーを並べ替え、`/` をエスケープしない | `NotificationJson.swift:123` |
| F-15 | 前面で非活性（`foregroundInactive`）の scene は前面に数えられ、権限の要求はダイアログを出す。`NOT_FOREGROUND` は背景のときだけ | native-toolkit `ForegroundLocator.swift:61-63`、`NotificationPresentationGate.swift:61-76` |
| F-16 | 1.x のライブラリは settings request（`openSettingsFor`）を受けると設定の画面を開いた。`2.0.0` はイベントを渡すだけ | `1.13.0` の `IosNotificationManager.swift:354-360`、`2.0.0` の `IosNotificationManager.swift:980` |
| F-17 | 1.x の配列の読み方は全か無か（`as? [[String: Any]]`・`as? [String]`）。数と bool は NSNumber で行き来する | `UnityIosNotificationJsonParser.swift:64, 160, 166, 173, 188` |
| F-14 | `2.0.0` のライブラリの固定の英文（`NotificationError.errorDescription`）がある（例: `Notification permission was denied.`、`Failed to add the notification request.`）。C ABI はこれを返さない | `IosLibrary/Notification/Domain/Model/NotificationError.swift:74-98` |

## 2. 決めること

| # | 決定 | 理由 |
|---|---|---|
| D-1 | **置き場所**: 宣言・構造体・変換は新しい `Runtime/iOS/Notification/IosNotificationCApi.cs`（`#if UNITY_IOS \|\| UNITY_EDITOR`、`extern` は `!UNITY_EDITOR` の外）、表は `IosNotificationPendingCalls.cs`（同じ guard）。この 2 つは `#if UNITY_IOS` だけの型（`IosNotificationAuthorizationStatus`）を使わない: 状態は 1.x の名前の文字列（`authorized` など）で返し、Manager が今の `IosNotificationAuthorizationStatusParser` で写す。表は受け手を `Delegate` で持つ。コールバックの本体と呼び出しは `UNITY_IOS && !UNITY_EDITOR`。Manager は今の `#if UNITY_IOS` のまま | iOS 以外を対象にした Editor でも組める（サブ R-1） |
| D-2 | **呼び出しごとの受け手**: `user_data` に呼び出しの番号を渡し、番号から受け手を引く表を持つ（操作の結果、`bool`、状態、一覧の受け手を種類ごとに）。番号は使い回さない。`release` は NULL。表から取り出すのは、完了・入口の拒み・呼ぶ前の例外のとき。**Manager の破棄で表を空にしない**（1.x の受け手は static で、破棄の後にも呼ばれた）。`onResult` が null でも、C ABI には固定の受け口を渡す（NULL のコールバックは拒まれる）。単一の実行（BUSY）は入れない。表は lock で守る（どのスレッドから呼ばれてもよい）。**受け口**は、ハンドル（一覧・interaction）を値に写してから `finally` で 1 回だけ解放し、例外は受け口の中で止める（ログに残す）。番号から受け手を取り出して結果を組み立てる芯は、Editor でも組まれる internal の関数にし、Editor の PlayMode で照らす | F-8。1.x は 1 つの枠の後勝ち（NP-3・18）、`2.0.0` は呼び出しごとに 1 回（NT-19） |
| D-3 | **時点と順**: 結果は「その呼び出しの受け手 → `NotificationOperationCompleted`」の順で、別々の `try` で出す（3-S の今の形。1.x は同じ `try`）。イベントは生きている Manager がいれば出す。ネイティブの完了の結果は `UnityMainThreadDispatcher` で次の `Update` に出す。**呼び出しの中で決まった結果**（入口の拒み、C# の検査の失敗、使えないとき、呼ぶ前の例外）は、共通の新しい待ち行列 `Runtime/iOS/Common/IosNextFrameQueue.cs` に積み、ディスパッチャーの GameObject に付けた常駐の部品が「積んだフレームより後」にディスパッチャーの末尾へ渡す（Clipboard の D-12 と同じ考え）。待ち行列に何か待っている間は、ネイティブの完了の結果もその後ろに並べる（呼んだ順と結果の順が入れ替わらない。Clipboard と同じ）。待ち行列は lock で守り、主スレッドの外から積んだものには次のポンプの回でフレームの番号を付ける（`Time.frameCount` は主スレッドでしか読めない）。ポンプは Manager の Awake（主スレッド）で作り、主スレッドで待ち行列に積む所（呼び出しの中で決まった結果、ネイティブの結果、イベント）でも確かめて付け直す（ディスパッチャーが壊れて作り直されると、ポンプも消えるため）。Play の始めとアプリの起動で空にする | F-5。ディスパッチャーは `Update` の中で積まれたものも同じ回で回すので、呼び出しの中で積むと、受け手の中から呼び直す利用者のコードで `Update` が終わらない（Codex R-1）。1.x の C ABI の拒みにあたるもの（`SetBadgeCount(-1)` など）は、Task を回ってから届いた |
| D-4 | **JSON の読み方**: 1.x の Swift の読み方を C# に写す（`IosClipboardJsonReader` を使う）。<br>content: `id`・`title` が無いか文字列でなければ `Failed to parse notification content JSON`（Schedule は `Failed to parse notification content or trigger JSON`）。ほかの項目は型が違えば無視。<br>trigger: 読めなければ、Show と Update は trigger 無し（即時）、Schedule は `Failed to parse notification content or trigger JSON`。<br>category: `identifier` が無ければ `Failed to parse category JSON`。<br>配列（F-17）: `attachments`・`actions`・`textInputActions` は全要素がオブジェクトでなければ配列ごと無視、`options` は全要素が文字列でなければ無視。そのうえで、必須の項目が無い要素（action は `identifier`・`title`、text input は 4 項目、添付は `identifier`・`fileURL`）と、空の識別子の action・text input action と、`fileURL` が空の添付（1.x は URL として読めずに捨てた）は捨てる。添付の識別子は空でも残す（1.x も残し、`2.0.0` も受ける）。未知の option は無視。<br>型の橋渡し（1.x の `as? Bool`・`as? Int`・`as? Double` を macOS で動かして確かめた値。レビュー v2 の 1 章 R-4）: bool の項目は true・false と、値が 0 か 1 の数（`1.0` を含む）だけを受け、ほかは無視（既定）。整数の項目は、値がちょうど整数の数（`2.0`、`1e2`）と bool（true が 1）を受け、小数は無視。小数の項目は数と bool を受ける。int32 に入らない整数は無視（1.x の `Int` は 64 ビットだったが、C ABI は int32。既知の差分）。`JSONSerialization` が読めない数（`1e400` など。1.x は content ごと読めなかった）と、Double でちょうど表せない 2^53 を超える整数（1.x の `as? Double` は nil）は写さない（珍しい入力） | F-1〜F-3、F-17。文言と「読めなければ即時」と全か無かを保つ |
| D-5 | **1.x では通り `2.0.0` で拒まれる値**（F-9）の扱い: <br>(a) 1.x と同じ結果になる見込みの直しを C# でして渡す（1.x の記録は権限が未決のときの成功で、権限があるときの結果は測っていない）: 未知の `interruptionLevel` は `active`、負の `badge` は「設定しない」、範囲外の `relevanceScore` は 0〜1 に収める、オブジェクトでない `userInfo` は `{}`、`userInfo` の中の `null` は取り除く、空の音の名前は省略（既定の音）、location の `repeats` は false、Schedule と Update の content の `id` は C ABI が使わないので、U+0000 で拒まず、U+0000 の前で切った値を `content_create` に渡す（空なら引数の `identifier` を U+0000 の前で切った値）<br>(b) 失敗にする（**既知の差分**）: 範囲外の interval（1.x はプロセスが落ちた）、項目の無い calendar、範囲外の location、空の location の識別子、入る・出るの両方が false、`file://` でない添付（1.x も追加で失敗）、Show の空の `id`、Schedule と Update の空の `identifier`、最上位に予約のキー `com.jonghyunkim.nativetoolkit` を持つ userInfo（`2.0.0` が予約したキー。1.x では利用者のデータとして届いた）、空の category の識別子<br>(c) setter と `content_create` が `NONE` でなければ、操作を呼ばず、その値の文（D-6）で失敗にする。ビルダーは解放する。結果は D-3 の待ち行列 | 1.x の利用者の JSON が黙って失敗に変わらないように（Codex R-6・R-7・R-9、サブ R-5・R-6・R-9） |
| D-6 | **失敗の文言**: 1.x の汎用の文（端末の言語で訳され、番号しか意味を持たない）は写さず、`2.0.0` のライブラリの固定の英文（F-14）を C# に写して使う: `PERMISSION_DENIED`・RequestPermission の拒否 → `Notification permission was denied.`、`ATTACHMENT_LOAD_FAILED` → `Failed to create a notification attachment.`、`ADD_REQUEST_FAILED` → `Failed to add the notification request.`、`SET_BADGE_COUNT_FAILED` → `Failed to set the badge count.`、`AUTHORIZATION_FAILED` → `Failed to request notification permission.`、`NOT_FOREGROUND` → `The app is not in the foreground.`、`SETTINGS_NOT_OPENED` → `Failed to open the notification settings.`、`NOT_INITIALIZED` → `The library's notification delegate is not installed.`（ライブラリの文の後半は Swift の API の名前なので落とす）、`INVALID_PARAMETER` と C# の拒み → `The notification request is invalid.`（新しい文。SetBadgeCount・RegisterCategory でも同じ文でよい）、ほか → `An unknown error occurred.`。JSON の読み違いの 3 つの文は 1.x のまま | F-4、F-14。**既知の差分** |
| D-7 | **権限が無いときの Show・Schedule・Update**: `PERMISSION_DENIED` の失敗（`Notification permission was denied.`）を返す。1.x は成功を返して何も足さなかった | F-10。**既知の差分**（NP-4・5・5b、NU-2b、NU-16） |
| D-8 | **権限の要求**: `options` は 0（alert・sound・badge。1.x と同じ）。granted 1 → 成功、0 → `Notification permission was denied.`、ほかのエラー → その文。前面で非活性はダイアログが出る（F-15。NU-10 は結果が来るかを記録する。来なければ native-toolkit への報告の候補）。背景で未決なら `The app is not in the foreground.` がすぐ来る（NU-14。1.x は前に戻ってからダイアログ）。provisional のときは OS に求めずに成功（1.x は再要求した）。HasPermission は granted、GetAuthorizationStatus は名前の文字列（D-1）。どちらも失敗なら false と `unknown` | F-10、F-15 |
| D-9 | **一覧**: ハンドルから 1.x と同じキーの JSON の配列を作る（F-7）。キーの順は `identifier`・`title`・`subtitle`・`body`・`categoryIdentifier`・`date`。`date` は Unix ミリ秒を秒に切り捨てた UTC の `yyyy-MM-ddTHH:mm:ssZ`。文字列のエスケープは `\"`・`\\`・`\b`・`\f`・`\n`・`\r`・`\t`、ほかの U+0000〜U+001F は `\u00xx`（小文字）、`/` と U+2028 はそのまま（1.x は `/` を `\/` にした。既知の差分）。失敗なら `"[]"`（1.x と同じ） | F-7（サブ R-11） |
| D-10 | **完了を返さない公開の操作**（取り消し・削除・`RemoveCategory`・`OpenNotificationSettings`）: 結果をどこにも出さず、失敗だけ `LogWarning` に記録する固定の受け口を渡す。`RemoveCategory` の空文字は C ABI が何もせず成功 | F-8 |
| D-11 | **SetBadgeCount**: 負の値は C ABI の `INVALID_PARAMETER` で失敗（1.x も -1 は失敗。文言だけが違う） | NP-13 |
| D-12 | **イベント**: interaction の受け手を**プロセスの間 1 つだけ**登録し、外さない（Manager の破棄でも）。kind 0・1・2 → `NotificationActionReceived`（actionId はそのまま）、3 → `NotificationTextInputActionReceived`、未知の kind は解放して捨て、ログに残す。Manager がいないときのイベントは解放して捨てる（1.x と同じ）。最初の登録の前に待っていた分（起動時のタップを含む）は、最初の Manager に届く（**既知の差分**。1.x は届かなかった。NU-8）。`get_launch_response_async` は使わない（同じタップが 2 回になる）。受け口は interaction のハンドルを値に写して解放し、結果を次の `Update` に出す（D-3 のネイティブの結果と同じ経路）。Manager がいるかは、受け口と出す時の両方で見る。**settings request の受け手は登録しない**: 受け手が無い間の要求はライブラリが最初の登録まで抱えるので、登録すると、抱えられた要求で後から突然設定の画面が開く。1.x は settings request で設定の画面を開いた（F-16）ので、**既知の差分**（ホストが native で `providesAppNotificationSettings` を求めたアプリだけに関わる）。userInfo の平らな読み手は、文字列でない値を**飛ばして続ける**ように直す（1.x は止めた。`2.0.0` はキーを並べ替えるので、止めると残るキーが変わる。**既知の差分**）。飛ばすのは値 1 つで、文字列の中を見ながら `{}`・`[]` の深さを数える（入れ子のキーを上の段に出さない） | F-6、F-12、F-13、F-16（Codex R-3・R-5、サブ R-8、レビュー v2 の 1 章 R-1・R-2・R-8） |
| D-13 | **`Initialize` と Awake**: Awake は受け手の登録だけ（delegate が入っていなければ `NOT_INITIALIZED` で、ログに残して登録しない）。公開の `Initialize()` は `ntk_notification_install`（主スレッドだけ。ほかのスレッドなら `LogError`）を呼び、受け手が無ければ登録する（README の I-3）。Unity の通知パッケージの許可の要求の後に呼び直す使い方を doc に書く | 申し送り L215。`NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` を効かせる（Codex R-8、サブ R-7） |
| D-14 | **category の置き換え**: `2.0.0` は同じ識別子を置き換え、完了は書き込みの後（**既知の差分**。1.x は置き換わらず、入口の中で成功した。NU-12） | F-11 |
| D-15 | **使えないとき**（iOS の Player で `IosNativeToolkitCApi.IsAvailable` が false）: 3-S の今の経路（`Notification is not available yet on this build.`、false、`Unknown`、`"[]"`）を残す。時点は D-3 の待ち行列 | 3-S と同じ |
| D-16 | **U+0000**: JSON の中の識別子（Show の content の `id`、category・thread・target content・action・text input・添付・location の識別子）と `fileURL` と音の名前に U+0000 があれば `The notification request is invalid.`（C の文字列で切れると、別の通知・別のファイル・別の音を指す。`fileURL` のファイルは追加の時に移される）。表示の文字列（title・subtitle・body・action の題・ボタンの題・placeholder・filter criteria・SF Symbols の名前）は値を出さない警告を出して渡す（U+0000 の前で切れる）。引数の文字列（Schedule・Update の `identifier`、取り消し・削除・`RemoveCategory` の引数）は、1.x と同じく C の文字列で渡して切れる（1.x のブリッジも `const char*` で受けた）。userInfo は C# が D-9 の規則（制御文字は `\u00xx`）で書き直すので、エスケープのまま届く | Codex R-4、レビュー v2 の 2 章 R2-1、1 章 R-6 |
| D-17 | **サンプル**: `IosNotificationManagerExampleController.IsPermissionDeniedError` に D-6 の文を足す（1.x の判定も残す） | サブ R-2 |

## 3. 振る舞いの対応（1.x の記録と `2.0.0` の見込み）

| 場面 | 1.x（記録） | `2.0.0`（この設計） | 区分 |
|---|---|---|---|
| NP-3・18（続けて 2 回） | A 0 回、B 1 回、イベント 2 回 | A 1 回、B 1 回、イベント 2 回 | 既知の差分（呼び出しごとに届く） |
| NP-4・5・5b、NU-2b（権限が無い Show・Schedule・Update） | 成功、何も足さない（即時のものは配信済みに載る） | `PERMISSION_DENIED` の失敗、何も足さない | 既知の差分（D-7） |
| NP-6（未知の trigger の Show） | 即時に出す | 同じ（D-4）。ただし権限が無ければ D-7 の失敗 | 同じ |
| NP-7（未知の trigger の Schedule） | `Failed to parse notification content or trigger JSON` | 同じ | 同じ |
| NP-8（13 の値） | `https://` の添付だけ失敗 | D-5 (a) の値は権限が無いので D-7 の失敗。(b) の値（空の id の Show、空の予約の identifier、`https://` の添付、項目の無い calendar、空の location の識別子、予約のキー）は `The notification request is invalid.` | 既知の差分（D-5 (b)） |
| NP-9（範囲外の interval） | プロセスが落ちる | `The notification request is invalid.` | 既知の差分 |
| NP-9b（有限でない interval） | `Failed to parse notification content or trigger JSON` | 同じ（C# の Builder が JSON を書けない） | 同じ |
| NP-13（`SetBadgeCount(-1)`） | 汎用の文の失敗（error 2） | `The notification request is invalid.` | 文言だけの差分 |
| NP-16（category の欠けた action、空の action の識別子、未知の option） | 成功 | 同じ（D-4 で捨てる・無視する） | 同じ |
| NU-2（拒否） | 汎用の文（error 4） | `Notification permission was denied.` | 文言の差分（D-6） |
| NU-4（userInfo の `/`、数の混ざる userInfo） | `slash=a\/b`、数の値で読むのを止める | `slash=a/b`（F-13）、数の値を飛ばして続ける（D-12） | 既知の差分 |
| NU-8（起動時のタップ） | 届かない | 届く | 既知の差分（D-12） |
| NU-10（前面で非活性の要求） | ダイアログが出て、拒否しても結果が来ない | ダイアログが出る。結果が来るかを記録する（F-15） | 記録 |
| NU-14（背景で未決の要求） | 前に戻ってからダイアログ | `The app is not in the foreground.` の失敗がすぐ来る | 既知の差分（D-8） |
| NU-12（category の登録し直し） | 置き換わらない（揺れる） | 置き換わる | 既知の差分（D-14） |
| NU-16（範囲外の location、入る・出るが両方 false、userInfo の入れ子の null。権限は未決） | 成功 | location の 2 つは `The notification request is invalid.`、null は取り除いた後に D-7 の失敗 | 既知の差分（D-5、D-7） |
| 失敗の文言 | 汎用の文（言語で訳される） | 固定の英文（D-6） | 既知の差分 |
| 一覧の JSON の `/` | `\/` | `/`（D-9） | 既知の差分 |
| 受け手が投げたとき | イベントが出ない | イベントも出る（D-3） | 既知の差分 |
| Manager の破棄の後の完了 | 受け手が呼ばれる | 同じ（D-2） | 同じ |
| Manager を作り直す間のタップ | 失われる | 同じ（D-12） | 同じ |
| provisional のときの権限の要求 | OS に再要求 | 求めずに成功（D-8） | 既知の差分 |
| 添付で失敗した Update | 前の予約が先に消える | 前の予約が残る（`2.0.0` の `NotificationRepositoryImpl.update`） | 既知の差分 |
| 印の無い通知（ホストのもの、1.x で予約したもの）へのタップ | 1.x の delegate が受ける | 前の delegate があればそちらへ回り、C# には来ない | 既知の差分 |
| JSON の中の識別子・`fileURL`・音の名前の U+0000 | 全文が届いた | 拒む（D-16） | 既知の差分 |
| JSON の中の表示の文字列の U+0000 | 全文が届いた | 切れる（警告） | 既知の差分 |
| 引数の文字列の U+0000 | 切れる | 同じ | 同じ |
| settings request（ホストが native で求めたアプリ） | 設定の画面が開いた | 前の delegate があればそちらへ回り、無ければ何もしない（D-12） | 既知の差分 |
| JSON として読めない数（`1e400`）、2^53 を超える整数 | content ごと読めない・無視 | 場所で変わる（D-4） | 既知の差分（珍しい入力） |
| bool・整数の項目の型（`"repeats":2` など） | Swift の `as?` | 同じ規則（D-4） | 同じ |
| int32 に入らない整数 | 通った | 無視（D-4） | 既知の差分 |
| 呼び出しの中で決まった結果の時点 | ディスパッチャーの中から呼べば同じ `Update` | 必ず後のフレーム（D-3） | 既知の差分 |
| NP-16 の空の action の識別子 | action が残る | action を捨てる（結果は同じ成功） | 既知の差分（category の中身） |
| ほかの NP・NU（許可、状態、表示、タップ、返信、一覧、削除、設定） | 記録 | 同じ見込み | 同じ（実測で確かめる） |

## 4. 変えるもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Notification/IosNotificationCApi.cs`（新） | 構造体 4 つ（`trigger` 96・`action` 40・`text_input_action` 40・`category` 56 バイト、`Pack = 8`）、使う関数の宣言、コールバックの型、JSON の読み方（D-4・D-5・D-16）、エラーの写し方と文言（D-6）、一覧の JSON（D-9）、状態の名前、ハンドルから読んだ値で結果を作る純粋な部分 |
| `Runtime/iOS/Notification/IosNotificationPendingCalls.cs`（新） | D-2 の表 |
| `Runtime/iOS/Notification/IosNotificationRequests.cs`（新） | JSON の読み方（D-4・D-5・D-16） |
| `Runtime/iOS/Notification/IosNotificationUserInfo.cs`（新） | イベントの平らな読み手（D-12） |
| `Runtime/iOS/Common/IosNextFrameQueuePump.cs`（新） | D-3 の常駐の部品 |
| `Runtime/iOS/Common/IosNextFrameQueue.cs`（新） | D-3 の待ち行列と常駐の部品 |
| `Runtime/iOS/Notification/IosNotificationManager.cs` | 仮の経路を C ABI の呼び出しに。D-2・D-3・D-8・D-10・D-12・D-13・D-15。Awake は `Initialize()` を呼ばない。クラスの doc |
| `IosNotificationActionResult.cs`・`IosNotificationTextInputActionResult.cs` | 平らな読み手を「文字列でない値は飛ばす」に（D-12） |
| `Runtime/UI/iOS/Notification/IosNotificationManagerExampleController.cs` | D-17 |
| テスト | EditMode: 構造体の大きさと位置、JSON の読み方（D-4・D-5・D-16 の各値、全か無か、型の橋渡し）、文言、一覧の JSON とエスケープ、状態、平らな読み手、`IosCApiDeclarationTests` の名前と署名、待ち行列。EditMode に、平らな読み手の入れ子・配列・`,` や `}` を含む文字列、型の橋渡しの各値、`fileURL`・音の名前の U+0000、空の識別子の添付が残ること。Editor の PlayMode: 待ち行列（受け手の中から呼び直しても同じフレームでは配らない、ネイティブの結果が待ちの後ろに並ぶ、主スレッドの外から積んだもの）、受け口の芯（待っている呼び出しを残して Manager を壊しても受け手が呼ばれる、Manager がいないときのイベントは出ない）。Player: `NTK_IOS_CABI_2_NOTIFICATION` を定義し、`PendingOn2` を 3 章の期待に置き換える。待っている一覧の呼び出しの後に Manager を壊しても受け手が呼ばれる。NP-6 の主張の文を D-4・D-7 に合わせる。Notification の `UnavailableUntilMoved` を消す |
| ビルドの設定 | `IosPlayerBuildSetup` と UI のハーネスのビルドに `NTK_IOS_CABI_2_NOTIFICATION` |
| UI のテスト | `notMovedYet` を空にする。`pendingOn2` を 3 章の期待に置き換える（NU-2・2b・8・10・14・16、NU-4 の `slash`、NU-12）。ハーネスの NU-4n を、数と文字列の混ざる userInfo にする（`2.0.0` は文字列の値が残る） |
| 文書 | `agent-rules/coding-rules/common.md`、`testing.md`、実装の記録、トピックの README |

## 5. 確かめ

| # | 確かめること | どこで |
|---|---|---|
| V-1 | EditMode と Editor の PlayMode（`iOS Profile`）。iOS 以外を対象にしても組めること: Unity を `-activeBuildProfile`（`Android™ Profile`）で EditMode だけ起動する（`verify_unity_android.sh` の `run_tests` と同じ引数。端末は使わない） | `verify_unity_ios.sh`、Unity の batchmode |
| V-2 | Player のテスト（Notification 16 件を 3 章の期待で） | シミュレーター、実機（許可を得て） |
| V-3 | `NotificationUITests` の 22 件 | シミュレーター |
| V-4 | 実機の `--keep-installed` の回（NU-1・4・5・7 と、流せれば NU-11） | 実機（利用者の準備と許可を得て） |
| V-5 | サンプルの画面で権限を拒否して、設定の案内が出ること | 段 4 の利用者の手の確かめ |
