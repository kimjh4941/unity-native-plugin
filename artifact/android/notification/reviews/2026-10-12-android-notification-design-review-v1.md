# レビュー結果

- 日付: 2026-10-12
- 対象ファイル: artifact/android/notification/designs/2026-10-12-android-notification-design-v1.md
- 機能名: notification
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 1）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない）
- A1: サブエージェント 5 件（R1-1〜R1-5。R1-2・R1-3 は利用者の判断が要る）、Codex 4 件（X1-1〜X1-4）

---

## 1. サブエージェント（Claude）

### 範囲と照らした一次資料

- 対象は `artifact/android/notification/designs/2026-10-12-android-notification-design-v1.md`（427 行）の全章。ファイルは変更していません。
- 規則として次を読みました。
  - `agent-rules/workflows/review-document/workflow.md`（手順 4 と手順 8）
  - `agent-rules/coding-rules/common.md:37-110`（Android C ABI、Share の例外、Clipboard の形、ログ）
  - `testing.md`
  - 親課題 README の J-9 と J-11
- 照らした一次資料は次のとおりです。
  - 今の C#: `Mgr`、`ActRes`、`NotificationReceivedResult.cs`、`NotificationResult.cs`、`AndroidNotificationJsonBuilder.cs`、`AndroidNotificationPayloads.cs`、サンプルの Controller、`manual/1.12.0/notification.ja.md`
  - 前例: Clipboard の `AndroidClipboardManager.cs`、`AndroidNativeCallbackQueue.cs`、`AndroidNativeToolkitCApi.cs`、`WindowsNotificationJsonReader.cs`
  - native-toolkit:
    - `Notification.h`（関数 86 個を grep で確認）、`Builders.cpp`、`Builders.h`、`Notification.cpp`、`StructSize.h`、`NotificationBridge.kt`
    - `K/presentation/event/*`、`K/data/repository/{NotificationRepositoryImpl,ScheduledNotificationReceiver,AndroidNotificationSettingsGateway}.kt`
    - `StructLayoutTest.cpp:96-116`、P2 の AP-8 / AP-16 / AP-18 / AP-21 / AP-22、8.3 の NT-01〜NT-27、16 章、申し送りの 164〜175 行
  - 1.x（タグ 1.12.0）: `UnityNotificationJsonParser.kt`、`UnityAndroidNotificationManager.kt`、`NotificationActionReceiver.kt`、`NotificationShownSupport.kt`、`ScheduledNotificationReceiver.kt`、`NotificationRepositoryImpl.kt`、`NotificationSchedulerSupport.kt`、`NotificationUseCases.kt`、build.gradle の minSdk（1.x も 2.0.0 も 31）
- 作業メモは参考にとどめ、主張はすべて一次資料で確かめました。
- 行番号の引用は約 45 か所を照らし、ほぼ正確でした。違いは下の C に書きました。

### 強み

- 「1.x と同じ」と書いた行に、ほぼすべて 1.12.0 の行番号が付いています。
  - 4.2 の表の `OP:` の行番号は、照らした範囲ですべて正確でした（`:54-55`、`:58,65,68-69,73-74,77`、`:64,75,78-81`、`:94-114`、`:142-203`、`:263-273` など）。
- J-N5（保つ補正）と J-N6（保たない補正）を分けて理由を書いており、J-11 の枠組みに沿っています。
- 構造体 2 つの大きさと全フィールドの位置が `StructLayoutTest.cpp:96-116` と一致しています。delegate の形も `H:85-92` と一致しています。
- 層の分け方が良いです。
  - JSON のリーダーとマッピングを純粋な関数にし、層 1 に置いています。
  - 「Builder が書いた JSON を読み、元の Payload と同じ値になる」往復のテストがあります（7.1）。
- スレッドの形は Clipboard の前例を写しています。
  - リスナーは 1 回だけ登録して外さない。
  - 届け先は取り出した時点の `_instance`。
  - どのスレッドでも待たない。
  - 互いに待つ形は見当たりませんでした。`add_*_listener` は `MainTasks.post` を積むだけです（`NB:414-433`）。
- 既知の差分の主要部分は押さえています。
  - `LegacyScheduleCleaner`、32 件の保持、`Data` のエスケープの残り、起動の Intent の違い。
  - J-N8（不正確な予約に下げて呼び直す）は、1.x の `scheduleAlarm`（`OL/NotificationRepositoryImpl.kt:326-356`）の振る舞いとも合っています。
- 8 章で、OS の画面・許可・前提の設定を挙げ、確かめていないものに「要検証」を付けています。

### 改善点

#### A1（実装フェーズで気づけない）

**R1-1｜A1｜severity: medium｜groupName の無い groupId**

- 該当箇所: 4.2 のチャンネルの表 `groupId / groupName`（172 行）、J-N6（269 行）、5.3（299 行「Kotlin の扱いは未確認」）
- 根拠:
  - 1.x の `OL/data/repository/NotificationRepositoryImpl.kt:99-104` は `val groupName = channel.groupName ?: groupId` です。groupId をそのまま名前にしてグループを作り、成功していました。
  - 2.0.0 の Kotlin も同じ形です（`K/data/repository/NotificationRepositoryImpl.kt:101-102`）。
  - 拒むのは C ABI の setter だけです（`BLD:535-537`）。
- 影響:
  - `ChannelPayload.groupName` は公開の省略可のフィールドです。ビルダーは groupId だけでも書きます（`AndroidNotificationJsonBuilder.cs:68-69`）。
  - このため、groupId だけを指定すると Show / Schedule / CreateChannel が丸ごと失敗に変わります。テストでは気づけません。
- 提案:
  - 「未確認」を事実に直してください。
  - groupName が無いとき、C# が groupId を名前として `set_group(groupId, groupId)` に渡す形にし、J-N5（保つ補正）に移してください。1.x と完全に同じ結果になります。

**R1-2｜A1｜severity: medium（要判断）｜引けないリソースの名前を失敗にする判断が、C ABI の申し送りと逆**

- 該当箇所: J-N6（269 行）、5.3（296 行）
- 根拠: 次の 3 か所で、1.x の代わりの値（小さいアイコンの既定、大きいアイコンとアクションのアイコンの省略、引けない view ID のクリックを捨てる、BigPicture や layout の Default）は「Unity の包みが保つ」と申し送られています。
  - P2 の AP-8（`2026-10-05-...-part2.md:32`「Unity から見た今の動作は 2c の包みが保つ（16 章）」）
  - P2 の 16 章の「値の補正と代わりの値」の行
  - 申し送り `2026-10-10-...-handoff.md:164`
- 問題:
  - 設計はこの申し送りに触れずに逆の判断をしています。
  - 理由の「C# から引けるかを知る手段が無い」は半分しか正しくありません。`RESOURCE_NOT_FOUND` のときに代わりの値で呼び直す方法や、native-toolkit に問い合わせ API を求める方法が検討されていません。
  - 開発中のビルドでは名前が引けるため、テストを通ります。リソースの縮小（R8 の shrinkResources、`MAN:1235-1240` の tools:keep）が効いた出荷ビルドでだけ、通知が丸ごと出なくなる恐れがあります（Unity の既定で shrinkResources が有効かは要検証）。
- 提案:
  - 申し送りと違う判断であることを明記し、J-N7 と同じく利用者の判断として記録してください。
  - 保つなら、例えば `RESOURCE_NOT_FOUND` のときに、名前の群（小さいアイコン → 大きいアイコン → アクションのアイコン → style の画像と layout → view のクリック）を順に外して呼び直す案があります。

**R1-3｜A1｜severity: medium（要判断）｜通知をオフにしただけの場合も失敗になる**

- 該当箇所: J-N7（270 行）、4.5 の `PERMISSION_DENIED`（229 行）、5.3（292 行）
- 根拠:
  - C ABI は `hasPermission() && areNotificationsEnabled()` で判定します（`NB:132-136`）。
  - 通知の権限はあるのに、利用者が設定で通知をオフにした場合も `PERMISSION_DENIED` になります。
  - Android 12（minSdk 31）には実行時の権限が無いので、そこではこの経路しかありません。
- 問題:
  - 利用者の判断の文言は「権限が無いとき」です。オフの場合まで含むかは決まっていません。
  - 文言の「notification permission is not granted.」も、オフの場合には誤りです。
- 提案:
  - J-N7 と 5.3 に「権限が無い、または通知がオフ」と明記し、利用者に確かめてください。
  - 文言は両方を含む形にしてください（例: `notifications are not allowed for this app (permission or app notification setting).`）。

**R1-4｜A1｜severity: low｜tag の空・空白の扱いを「1.x と同じ」と書いているが違う**

- 該当箇所: 4.3 の表 1 行目（200 行）
- 根拠:
  - 1.x で空白を「タグ無し」に寄せていたのは `cancel` だけです（`OL/.../NotificationRepositoryImpl.kt:85`）。
  - 予約の側は寄せていません。
    - キーは `"${tag ?: "untagged"}::$id"` です（`1.12.0 NotificationSchedulerSupport.kt:39`）。
    - `isScheduled` は `content.tag == tag` で比べます（`1.12.0 NotificationUseCases.kt:40-43`）。
  - 1.x の `CancelScheduledNotification(id, "")` は、タグの無い予約を消しませんでした。`IsNotificationScheduled(id, "")` は `false` でした。
- 提案:
  - C# は null だけを NULL にし、それ以外の tag はそのまま渡す形にしてください。
  - 2.0.0 の Kotlin は 1.x と同じ規則を持つので（`K/.../NotificationRepositoryImpl.kt:87` と `NotificationSchedulerSupport.kt:21-25`）、3 つのメソッドとも 1.x と同じになります。

**R1-5｜A1｜severity: low（要判断）｜丸めれば 1.x と見た目が同じ数を、失敗にしている**

- 該当箇所: J-N6 の「範囲の外の値」（269 行）、4.2 の `progress`（154 行）と `priority`（147 行）
- 根拠:
  - `set_progress` は `0 ≤ current ≤ max` でなければ拒みます（`BLD:179-180`）。1.x はそのまま渡し、OS がはみ出しを丸めて表示しました。
  - Progress の update で、ダウンロードの大きさから計算すると current が max を超えることは、ふつうの使い方で起こりえます。
  - `priority` は API 26 以降、表示に効きません（重要度はチャンネルで決まる）。minSdk 31 なので、-2〜2 に丸めても 1.x と同じ見た目になります。
  - J-N5 の理由「JSON の段で決まり、C# で安く写せる」は、これらにもそのまま当てはまります。
- 提案:
  - progress（current を [0, max] に丸める）と priority（-2〜2 に丸める）は J-N5 に移してください。
  - 残すなら、ふつうの入力ではない理由を書いてください。

#### A2（実装で気づける）

**R1-6｜A2｜low｜構造体名 `Action` が `System.Action` を隠す**

- 該当箇所: 4.1（122 行）
- 内容: `AndroidNotificationCApi` の入れ子に `internal struct Action` を置くと、その中で型引数の無い `Action`（`AndroidNativeCallbackQueue.Post(Action)` など）がこの構造体に解決され、コンパイルエラーになります。
- 提案: `NotificationActionNative` などに改名してください。

**R1-7｜A2｜low｜`NotificationActionResult` の内部コンストラクタが曖昧になる**

- 該当箇所: 5.1（250 行）
- 内容: `(string, int, string?)` と `(string, int, IReadOnlyList<…>)` を並べると、第 3 引数に `null` を渡す呼び出しが曖昧になります。
- 提案: 古い方を消す判断を設計で決めてください。

#### B（検証手段）

**R1-8｜B｜medium｜アプリを終了した状態でのタップを、実機の自動テストで再現できるか書かれていない**

- 該当箇所: 7.3（391 行）
- 内容:
  - C# のリスナーは外さないので、プロセスの中で一度 Manager を作ると「受け手がいない」状態に戻せません。
  - アプリを落とすと PlayMode のテストも終わります。
- 提案:
  - スクリプトで「アプリを止める → タップ → 起動して、Manager を最初に作るテストだけを流す」別の起動の手順を書いてください。
  - あわせて、保持されたイベントは Manager を作った直後のフレームで出るので、購読は `Instance` を最初に取った同じフレームで行う必要があります。この約束を XML コメントに書いてください。

**R1-9｜B｜medium｜権限と正確なアラームの切り替えでプロセスが止まる**

- 該当箇所: 8.2（406-407 行）、K-2（425 行）、E-6 / E-7
- 内容:
  - 「`pm revoke` でアプリが再起動されないか（要検証）」には、P2 の AP-16 が答えを書いています（`part2.md:40`）。通知の権限や `SCHEDULE_EXACT_ALARM` を取り消すと、アプリのプロセスが止まります。
  - `RequestPermission` の許可と拒否を繰り返すには、権限の状態を戻す手順も要ります（`pm revoke` と `pm clear-permission-flags … user-set user-fixed`、要検証）。
- 提案: これらは起動の前に設定する別の起動にする、と手順に書いてください。

**R1-10｜B｜medium｜J-N8 の呼び直しを層 2a で数える継ぎ目を持たない**

- 該当箇所: K-3（426 行）、E-7（359 行）
- 内容: Android 14 以降では正確なアラームが既定で不許可の端末が多く、ふつうの使い方の中心の振る舞いです。
- 提案: Clipboard の `SetNativeForTests` と同じ形で、schedule の呼び出しを差し替える継ぎ目を持ってください。層 2a で次を確かめられます。
  - 1 回だけ、`Inexact = 1` で呼び直すこと
  - ほかのエラーでは呼び直さないこと
  - 呼び直しの失敗の文言

**R1-11｜B｜medium｜リスナー 2 つの登録の「済み」の印が 1 つか 2 つか決まっていない**

- 該当箇所: J-N12（275 行）、7.2 の最後の行（384 行）
- 内容:
  - 印を 1 つにすると、片方だけ失敗して次の `Initialize()` でやり直したときに、成功した側が 2 重に登録されます。イベントも 2 回出ますが、テストでは見えません。
  - やり直しの機会が `Initialize()` だけなのも弱いです。
- 提案:
  - 印を 2 つにすると明記し、「片方だけ失敗 → やり直しで失敗した側だけ登録する」を層 2a のテストに足してください。
  - Clipboard の `EnsureChangeListener` のように、各メソッドからもやり直すか検討してください。

**R1-12｜B｜low｜チャンネルのハンドルを誰が解放するかが曖昧**

- 該当箇所: 5.4 の手順 5（324 行）
- 内容: `BuildContent(spec, allocated)` の中で作るチャンネルのハンドルを、外の `finally` で `channel_free` すると書いています。しかし署名からハンドルが外に出ません。漏れはテストで見えません。
- 提案: `set_channel` はハンドルを写すので（`BLD:162-169`）、`BuildContent` の中で `set_channel` の直後に、その場の try/finally で解放すると書いてください。

**R1-13｜B｜low｜1.x の記録の条件が書かれていない**

- 該当箇所: 5.5 の手順 3（341 行）
- 内容:
  - 今のビルドには 2.0.0 の AAR が同梱されています。「1.x の列」は、2.0.0 の `LegacyScheduleCleaner`（1 インストールに 1 回）と起動完了の受信機が同居した状態での記録になります。
  - `OpenNotificationSettings` がどの画面を開いたかも記録してください（R1-17）。
- 提案: 上の 2 点を記録の手順に書き足してください。

**R1-14｜B｜low｜未知の kind の扱いが無い**

- 該当箇所: 4.4（211 行）
- 内容: `ActionIdOf` に未知の kind や、ACTION なのに action_id が NULL の場合の扱いがありません。
- 提案: ログを出して捨てる、と決めて層 1 で押さえてください。

#### C（記述の整合）

**R1-15｜C｜low｜ビルダーの setter の数**

- 該当箇所: 1.1（34 行）
- 内容: `_content_set_*（28）` は 26 です。header にある `content_set_` 34 個から、style の 5 個と set_tap / set_dismiss_event / set_full_screen の 3 個を引いた数です。合計の 40 と 86 は正しいです。

**R1-16｜C｜low｜JSON の読み方の差分で 5.3 に無いもの**

1. `data`（160 行）: 「1.x と同じにする点」の列が `:90,256-261` を指していますが、1.x は `optString` ですべての型を文字列に変えていました。JSON の null は `"null"` になります（要検証）。ビルダーは `entry.value` が null なら `null` を書きます（`AndroidNotificationJsonBuilder.cs:270`）。`lines` の null の要素も同じです。
2. 重複したキー（192 行、300 行）: 「org.json は重複を拒む」は JSON-java（stleary 版）の振る舞いです。Android の libcore 版は後の値を残す見込みです（要検証）。1.x は緩い構文（引用符の無いキー、単引用符、コメント、`=` / `=>`）も受けました。
3. 入れ子の上限 512（192 行）: Windows.Data.Json の値です（`WindowsNotificationJsonReader.cs:128-131`）。org.json の値ではありません。採る理由を書いてください。
4. 数（138 行、142 行、152 行）について、1.x は次のように扱いました。
   - `intValue()` で切り詰め・巻き戻しました。符号無しで書いた ARGB の `color` / `lightColor`（> int.MaxValue）も、正しい色になりました。
   - 小数を切り捨てました。数の文字列や `"true"` を変換しました。
   - `id = Int.MIN_VALUE` は「無い」扱いでした。
   - 設計の「int に収まらない数は無い」が color にも当たるかを決めてください。
5. importance -1000（166 行）: 1.x は Android の「Invalid importance level」で失敗しました。2.0.0 は 3 に丸めて成功します。

**R1-17｜C｜low｜5.3 に足す行**

1. アクションボタンの `launchApp`: 1.x は受信機から `startActivity`（12 以降は止められる見込み）、2.0.0 は Activity から開きます。今の行は本文のタップだけです。
2. full screen の起動の Intent: `NEW_TASK | CLEAR_TOP` から `NEW_TASK | RESET_TASK_IF_NEEDED` に変わります（`OB:607-620`、`NotificationEventIntents.kt:106-116`）。
3. 通知の設定の画面: 1.x は `package:` の data のせいで、いつもアプリの詳細の画面に落ちていた見込みです（`K/data/repository/AndroidNotificationSettingsGateway.kt:34-36` のコメント）。2.0.0 は通知の設定の画面を開きます。ふつうの使い方で、見える画面が変わります（要検証）。
4. 設定の画面の結果の時点: 2.0.0 では、画面から戻った後に届く見込みです（要検証）。
5. Kotlin の `SecurityException`: 1.x の「Security restriction…」が「Failed to {op}: UNKNOWN」に変わります。
6. J-N8 で下げた予約: 再起動の後も不正確のまま残ります。1.x は復元のときに正確にできるかを判定し直しました（`1.12.0 NotificationRepositoryImpl.kt:170-181,326`）。
7. `Data` の `/`: Android の `JSONObject.toString()` は `/` を `\/` にするので、URL の値に `\/` が残っていた見込みです（要検証）。307 行に明記してください。

**R1-18｜C｜low｜エラーの文言（4.5、232-233 行）**

- `STORAGE_FAILED` は cancel_scheduled / cancel_all_scheduled / is_scheduled でも返ります。「could not be saved.」は保存以外の場面には合いません。
- `SERVICE_START_NOT_ALLOWED` は stop_progress でも返ります（P2 の NT-27）。「cannot be started」は stop には合いません。

**R1-19｜C（規則との不一致、要判断）｜low｜ログに JSON をそのまま出す**

- 該当箇所: 4.5（238 行）、2.2（88 行）
- 内容:
  - `common.md` の「ログ」は「入力の値は長さだけ」です。Share の J-S10 もそう決めました。
  - 2.0.0 のライブラリ自身も、メッセージの本文と data の値を伏せています（`BLD:285,353-355`）。
- 提案: 例外として明記するか、長さとキーだけのログに替えてください。

**R1-20｜C｜low｜Android 12 以前の端末**

- 該当箇所: 7.4（396 行）
- 内容: minSdk は 1.x も 2.0.0 も 31 です。対象は Android 12（API 31/32）だけ、と書いてください。

### 不足項目

- 公開 API の XML コメントを直す計画が 5.1 にありません。`common.md` は「振る舞いの差分は XML コメントへ」としています。直す対象は次のとおりです。
  - J-N7、`NOT_FOREGROUND`、保持されたタップが Manager の作成後に出ること、どのスレッドからでも呼べること
  - `RequestPermission` は Unity の権限 API なので UnityMain から呼ぶこと（J-N15 の例外）
- J-N6 が申し送り（AP-8、16 章、申し送り 164 行）と逆の判断であることの記録（R1-2）。
- 4.2 にない規則: `customViewLayout` の空白の扱い（1.x は `isNotEmpty` だけで判定）と、`isGroupConversation` が null の扱い（1.x は `has` が真なら `false`）。

### 総合評価

- **A1 は 5 件**です（R1-1〜R1-5）。うち R1-2 と R1-3 は、利用者の判断が要ります。
- 1.x のコード、C ABI、前例との照合は丁寧で、行番号の引用もほぼ正確です。構造体、スレッド、届け先の設計に大きな誤りはありません。
- ただし、次の 2 つで 1.x の確かめが足りていません。
  - J-N6 の切り分けに、事実の誤りと、C ABI の申し送りとの食い違いがあります。groupName は「未確認」ではなく、1.x は groupId を名前にしていました。リソースの代わりの値は、包みが保つと申し送られていました。
  - 予約の tag の扱いを「同じ」と書いていますが、違います。
- 加えて、丸めれば同じ見た目を保てる値（progress、priority）や、通知をオフにした場合を、失敗に変えています。
- テストの側では、終了した状態でのタップ、権限の取り消し（プロセスが止まる）、J-N8 の呼び直しの 3 つの自動化の前提を詰める必要があります。
- 判定: A1 を直して再レビューが要ります。C 区分は `scripts/check_design_consistency.py` での照合を勧めます。R1-16 と R1-17 は 5.3 の行の追加なので、手で足す必要があります。

---

## 2. Codex

X1-1 — A1

- 設計: §4.2、142行
- 欠陥: `id = -2147483648` を有効な整数として受理する設計だが、1.x は欠落として `"id is required."` を返す。C ABI はこのIDを無検査で受理するため、静かな契約変更になる。
- 根拠:
  - native-toolkit@1.12.0:android/unity_android_plugin/src/main/java/android/unity/notification/UnityNotificationJsonParser.kt:54-55
  - /Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Notification/Builders.cpp:74
- 修正: `int.MinValue` を欠落扱いにする。変更を意図するなら §5.3 に明記する。

X1-2 — A1

- 設計: §4.2、134行、180行
- 欠陥: `isGroupConversation: null` を「無い」として `-1` にする設計だが、1.x はキーが存在すれば `optBoolean` を呼ぶため `false` になる。欠落時だけ未指定になる。
- 根拠: native-toolkit@1.12.0:android/unity_android_plugin/src/main/java/android/unity/notification/UnityNotificationJsonParser.kt:183-187
- 修正: missing は `-1`、present-null は `0` と規定し、両ケースをマッピングテストに追加する。

X1-3 — A1

- 設計: §4.2、166行、§5.3 298行
- 欠陥: `importance` の0～4以外を3へ丸め、「1.x は5も3にした」としているが、1.x の正規化は `IMPORTANCE_MAX` をそのまま通す。さらに§5.3は範囲外を移行後に失敗させると書いており、§4.2の丸めとも矛盾する。
- 根拠:
  - native-toolkit@1.12.0:android/unity_android_plugin/src/main/java/android/unity/notification/UnityAndroidNotificationManager.kt:793-803
  - C ABIが0～4だけを受理すること: /Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Notification/Builders.cpp:421
- 修正: 1.xで通った `IMPORTANCE_MAX` 等を個別に§5.3へ列挙し、「3へ丸める」か「INVALID_PARAMETERにする」かを一貫して規定する。

X1-4 — A1

- 設計: §5.2 J-N3/J-N20、266行、283行
- 欠陥: Manager破棄前にキュー済みの結果・interaction・shownについて、1.xは元のManagerへ配送するが、新設計は取り出し時の `_instance` へ配送し、不在なら破棄する。この差分が§5.3にない。
- 根拠:
  - 1.xのキューとowner保持: /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Notification/AndroidNotificationManager.cs:579、同ファイル 614-661
  - Dispatcherの独立した永続キュー: /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Common/UnityMainThreadDispatcher.cs:24、同ファイル 43-63
- 修正: §5.3に、1.xは旧owner、新実装はdequeue時の現 `_instance`（不在なら破棄）と明記する。destroy、およびdestroy→再生成の配送先テストを追加する。

X1-5 — A2

- 設計: §4.2 157行、§7.1 375行
- 欠陥: マッピングは `launchAction` を読まない一方、BuilderのJSONを「元のPayloadと同じ値」に戻すテストを要求している。既存fixtureは実際に `launchAction` を設定するため、記述どおりのround-tripテストは失敗する。
- 根拠:
  - /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Notification/AndroidNotificationJsonBuilder.cs:111
  - /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidNotificationJsonBuilderTests.cs:40
- 修正: 比較対象を「1.xが利用する正規化後の射影」に限定し、`launchAction` や無視するresource typeを除外する。または中間型では保持し、Bridgeだけで無視する。

X1-6 — A2

- 設計: §4.5、219–235行、§7.1 373行
- 欠陥: 全エラー値の文言テストを要求する一方、既知の `CANCELED`（6）、`CANCELED_BY_SYSTEM`（7）、`HOST_START_FAILED`（9）の文言が定義されていない。
- 根拠: /Users/jonghyunkim/Desktop/native-toolkit/dist/1.13.0/android/include/NativeToolkitC/Notification.h:21
- 修正: 3値の行を追加するか、「個別指定のない既知値は `Failed to {op}: {ErrorName}`」という包括規則を明記する。

X1-7 — B

- 設計: §5.4 324行、329–332行、§7.1–7.2
- 欠陥: content/channel、およびinteraction/shownイベントを成功時・例外時に正確に1回解放することを検証するテストシームがない。イベントの解放漏れは機能テストが通っても発見できない。
- 根拠:
  - receiver所有: /Users/jonghyunkim/Desktop/native-toolkit/dist/1.13.0/android/include/NativeToolkitC/Notification.h:80
  - callbackごとにヒープ上のコピーを生成: /Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Notification/Notification.cpp:123
- 修正: reader/freeの注入可能な継ぎ目またはカウンターを設け、読取成功、読取例外、`Post` 失敗、BuildContent途中失敗で対応するfreeが1回だけ呼ばれることを検証する。

X1-8 — B

- 設計: §7.2、384行
- 欠陥: interactionとshownの片方だけ登録に成功した場合の再試行を検証していない。単一の登録済みフラグを使った実装でも、重複登録または片方の永久欠落を見逃せる。
- 根拠: 2本は別関数・別registrationである: /Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Notification/Notification.cpp:623
- 修正: 種別ごとの登録済み状態を規定し、「interaction成功／shown失敗」とその逆から再初期化して、失敗側だけ再試行されるテストを追加する。

X1-9 — C

- 設計: §5.3、295行
- 欠陥: 「受け手のいない間は32件保持」は範囲が広すぎる。32件保持はACTIVEなC registrationが0の期間だけで、J-N12どおり一度登録して永久に外さないなら、Manager破棄後はこの保持の対象にならない。
- 根拠:
  - 保持条件と上限: /Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/java/com/jonghyunkim/nativetoolkit/capi/jni/NotificationBridge.kt:52、同ファイル 436-468
  - managed queueは上限なし: /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Common/AndroidNativeCallbackQueue.cs:36
- 修正: 「最初のC listener登録前」に限定する。登録後のManager不在期間はmanaged queueとdequeue時の `_instance` 規則に従うと記載する。

補足: 構造体のサイズ・オフセットは StructLayoutTest.cpp と一致しており、欠陥は見つかっていない。

A1件数: 4

補足（取りまとめ役の転記）: Codex の出力は、Codex を呼んだ転送役のエージェントがそのまま返したもの。転送役は一次資料を確かめ直していない。
