# iOS Notification の C ABI `2.0.0` への移行の実装のレビュー v1

- 記録日: 2026-11-15
- 対象: コミット `daa62ef`（設計 [../designs/2026-11-15-ios-notification-cabi-design-v4.md](../designs/2026-11-15-ios-notification-cabi-design-v4.md)）
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。実装のレビューなので全体を見た
- 1 章・2 章はレビュアーの出力をそのまま写した（サブエージェントの出力の先頭の大見出しだけ、この記録の見出しと重なるので省いた）。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

**結論:** 実害のある不具合（A1）は見つかりませんでした。直すべき小さな不具合が 1 件、抜けが 4 件、細部が 10 件です。ファイルは編集していません。

**照らして問題が無かったもの:**
- **構造体と宣言:** 4 つの構造体の大きさ・位置（96/40/40/56）、46 の宣言、コールバックの署名はヘッダーと一致しています。
- **メモリとハンドル:**
  - content のビルダーは呼び出しの中で写されます（`NotificationReceiver.swift:138-146`）。
  - そのため `finally` で解放して問題ありません。category と trigger も入口で写されます。
  - list と interaction のハンドルは `finally` で 1 回だけ解放されます。
- **user_data・IL2CPP:** `user_data` の番号の扱い、`[MonoPInvokeCallback]` とポインターの保持は正しいです。
- **待ち行列:** 時点・順・リセットを机上で追い、正しいことを確かめました（Unstamped・Due・`Frame < frame`、ロックの順）。
- **JSON の読み方:** `UnityIosNotificationJsonParser.swift`（1.13.0）と項目ごとに照らし、R-2 以外は一致しています。
- **1.x の削除の意味:** 1.x の cancel・cancelScheduled（どちらも予約だけを消す）と、2.0.0 の対応づけは一致しています。

### R-1 [B] 観点 2: 生成で delegate を入れ直さない違いが、既知の差分の表に無い
- **根拠:**
  - 2.0.0: `IosNotificationManager.cs:129-133`（Awake は登録だけ）、`:164-174`、`:673-682`。
  - 1.x: `3cae23e^` の `IosNotificationManager.cs:206-222`（Awake → `Initialize()` → `notificationSetup()`）。
  - 設計 v4 の 3 章の表にこの行がありません。クラスの doc の「Differences from 1.x」の列挙にもありません。
- **壊れ方:**
  - 1.x では、Manager を作るたびにライブラリの delegate が前に戻りました。
  - 起動の後に別の SDK（通知・プッシュ系）が `UNUserNotificationCenter.delegate` を差し替えるアプリでは、2.0.0 に上げるだけでタップが C# に届かなくなり、何も出ません。
  - Awake の登録が `NOT_INITIALIZED` だった場合（ホストが後から native で install する、`AfterAssembliesLoaded` で Manager を作る、など）、`Initialize()` を呼ばない限りプロセスの間ずっと再試行しません。
- **直し案（どちらか）:**
  - 3 章と移行の手引き（CHANGELOG・manual）に「生成では入れ直さない。必要なら `Initialize()`」を書く。
  - または、`#if !NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` のときだけ Awake で `ntk_notification_install` を呼ぶ（C# から見える define です）。
  - あわせて、`s_interactionListener == Zero` のときは公開の呼び出しのついでに `RegisterListeners()` をやり直す。

### R-2 [A2] 観点 3: D-4 の「2^53 を超える整数は写さない」が実装されておらず、`1e400` の扱いは実行環境の振る舞い頼み
- **根拠:**
  - `IosNotificationRequests.cs:355-365`（`TryDoubleLike` は `TryGetDouble` の結果をそのまま採る）。
  - 使う所は `:146-147`（relevanceScore）、`:198`（interval）、`:214-216`（location の値）。
- **実測:** Unity 6000.4.2f1 の Mono で `double.TryParse` を走らせました。
  - `9007199254740993` → `True 9007199254740992`。丸めて写ります。1.x の `as? Double` は nil でした。
  - `1e400` → `False`。今は無視されますが、.NET Core の規則では Infinity を返します。
- **壊れ方:**
  - relevanceScore は 1 に収められて渡ります。
  - interval・location は丸めた値で届きます。
  - 実行環境が変わると、`1e400` が無視ではなく `The notification request is invalid.` や 1 への丸めに変わります。
  - EditMode に、この 2 つの値のテストがありません。
- **直し案:**
  - `TryDoubleLike` で `double.IsFinite` を明示して判定する。
  - 小数点も指数も無い整数の字句で `|x| > 2^53` のものは、double に往復させて一致しなければ無視する。
  - EditMode に `1e400`・`-1e400`・`9007199254740993`・`9007199254740992` を足す。

### R-3 [B] 観点 5: 入口の拒みと例外の経路（`Begin`・`DeferFailure`）が Editor で照らされていない
- **根拠:**
  - `IosNotificationManager.cs:574-616`。`Begin` は private で、呼び出しのラムダは Player だけで組まれます。
  - 手本の Share は `IosShareManagerEditorTests` で偽のネイティブ呼び出しを差し込み、拒み・例外・戻る前の完了を照らしています。
- **壊れ方:**
  - 種類ごとの失敗の写し方（`Action<bool>` は false、状態は `Unknown`、一覧は `"[]"`、値の操作はイベントを出さない）は、どこでも実行されません。Player の NP-8・NP-13 は操作の拒みだけです。
  - 呼ぶ前の例外（`LogError` と `An unknown error occurred.`）も、どこでも実行されません。
  - 「表から取り出すのは拒みと例外のとき」（D-2）の約束が、テストで裏付けられていません。
- **直し案:** `Begin` を `internal` にし、呼び出しを差し替えられる形にして Editor の PlayMode に足す。
  - 拒みのとき、種類ごとの値が後のフレームに 1 回届く。
  - 例外のとき、失敗が 1 回届く。
  - 呼び出しの中で `CompleteDone` が先に来たとき、1 回だけ届く。

### R-4 [B] 観点 5（1）: NU-10 の 2.0.0 の期待が結果の有無を問わず、C ABI の「ちょうど 1 回」が破れる場合が残る
- **根拠:**
  - `NotificationUITests.swift:363-364`（`results.isEmpty || results == [...]`）。
  - 記録は「ダイアログは出るが、拒否の後に結果が来ない」。
  - `Notification.h:30-31`（受け付ければ完了はちょうど 1 回）。
  - クラスの doc `IosNotificationManager.cs:23-24` は「once」と書いています。
- **壊れ方:**
  - `RequestPermission` の受け手が永久に呼ばれず、`s_pending` に 1 件残ります（サンプルの画面も結果待ちのままです）。
  - テストはどちらでも通るので、native-toolkit 側が直っても、悪化しても気付けません。
- **直し案:**
  - 報告の前に、native のログ（`CApiLog` の `[finishPermission]`）で完了が出たかを確かめ、C# 側の取りこぼしでないことを分ける（欠陥と断じる前に裏を取る）。
  - 確かめた後は、テストを `results.isEmpty`（報告への参照つき）に固定する。
  - doc か manual に「非活性中に呼んだ要求は完了しないことがある」と書く。

### R-5 [C] 観点 1: `OnIgnoredDone` が例外を止めず、どの操作の失敗かも分からない
- **根拠:** `IosNotificationManager.cs:841-845`。`Guard` で包んでおらず、`user_data` は 0 です。
- **壊れ方:**
  - 例外が逃げると、IL2CPP ではネイティブ側へ漏れます（現実には起きにくい）。
  - 警告のログが取り消し・削除・設定のどれの失敗かを示しません。
- **直し案:** `Guard` で包む。`user_data` に操作の小さな番号を渡し、ログに名前を出す。

### R-6 [C] 観点 1: 表から取り出した後の例外で受け手が失われる
- **根拠:**
  - `CompleteList`（`:988-994`）は `TryTake` の後に `ListJson` を呼びます。
  - `IsoSeconds`（`IosNotificationCApi.cs:289-294`）の `DateTimeOffset.FromUnixTimeSeconds` は、9999 年を超える値で `ArgumentOutOfRangeException` を投げます。C ABI は Int64 に入る値を返します。
- **壊れ方:** 例外は `OnList` の catch で止まりますが、受け手は呼ばれません。
- **直し案:** JSON 化を try で包み、失敗したら `"[]"` で受け手を呼ぶ。

### R-7 [C] 観点 4: 待ち行列の端の場合
- **根拠:** `IosNextFrameQueue.cs:38-45`。`EnsurePump` が呼ばれるたびに `s_mainThreadId` を上書きし、主スレッドかの検査がありません。
- **壊れ方:**
  - 将来、主スレッドの外から呼ぶと、主スレッドの判定が狂います。
  - ポンプが消えた後に主スレッドの外からだけ積まれたものは、次に主スレッドで積まれるまで出ません。
  - ディスパッチャーへ渡した後でディスパッチャーが壊れると、渡したものは失われます（1.x も同じ）。
- **直し案:**
  - `s_mainThreadId` は `ResetStaticState` だけで決め、`EnsurePump` は主スレッドの外では何もしないようにする。
  - この限界を doc に書く。

### R-8 [C] 観点 1: リスナーの登録が排他されていない
- **根拠:** `:676-680`。`s_interactionListener` を見てから代入するまでが守られていません。
- **壊れ方:** `Initialize()` を別のスレッドから Awake と同時に呼ぶと、2 つ登録され、イベントが 2 回届きえます。
- **直し案:** 主スレッドでないときの `Initialize()` は install の失敗で終える。または lock で守る。

### R-9 [C] 観点 3: JSON リーダーと JSONSerialization の差（珍しい入力）
- **根拠:** `IosClipboardJsonReader.cs:444`（`MaxDepth = 64`）、`:645-658`（対の無い `\uD800` も通す）。
- **壊れ方:**
  - 64 段より深い userInfo は content ごと `Failed to parse` になります。1.x は読めたはずです（JSONSerialization の上限は 512 段程度。要確認）。
  - 対の無いサロゲートは、1.x では読めなかったはずのものが U+FFFD で通ります（未確認）。
- **直し案:** 実測して 3 章に足すか、深さの上限を合わせる。

### R-10 [C] 観点 5: EditMode・Editor のテストの穴
- **U+0000:** thread・targetContent・category の識別子・action・text input の識別子の拒みが照らされていません。照らしているのは content id・category・sound・添付・location だけです（`IosNotificationCApiTests.cs:165-171, 234-235`）。
- **`ParseFlat`:** 入れ子の中の文字列に `}`・`]` を含む場合（例 `{"a":{"x":"}"},"b":"c"}`）がありません（`:270-277`）。
- **D-3 の「受け手が投げてもイベントは出る」:** Notification の Editor のテストにありません。
- **直し案:** 上の 3 つを足す。

### R-11 [C] 観点 5: NP-19 が「破棄の後に完了が来た」ことを確かめていない
- **根拠:** `IosNotificationPlayerTests.cs:498-507`。`Destroy` はフレームの終わりまで遅れます。
- **壊れ方:** 完了が破棄より先に来ても通ってしまいます。
- **直し案:** 古い Manager を変数に取り、受け手の中で `old == null` を記録して主張する。

### R-12 [C] 観点 6: コメントが設計 v3 を指している（実装は v4）
- **根拠:**
  - `IosNotificationPlayerTests.cs:29, 48, 494`
  - `NotificationUITests.swift:12, 22, 262, 519, 585`
  - `IosNotificationCApiTests.cs:14`、`IosNotificationManagerEditorTests.cs:18`
  - サンプル `IosNotificationManagerExampleController.cs:889`、`IosCApiDeclarationTests` の追加分
- **直し案:** v4 に揃える。

### R-13 [C] 観点 6: `csharp.md` の入口のログが無い internal のメソッド
- **根拠:**
  - Manager の `CompleteDone`・`CompletePermission`・`CompleteStatus`・`CompleteList`・`DeliverInteraction`・`InvokeOperation`・`InvokeValue`（`:537-994`）。
  - `IosNextFrameQueue.EnsurePump`・`EnqueueBehind`、`IosNotificationPendingCalls.Add`・`TryTake`。
- **直し案:** 値（userInfo・本文）を出さずに番号・操作名・種類だけを出す入口のログを足すか、除外の理由を doc に書く。

### R-14 [C] 観点 2・6: `UpdateNotification` の doc が実際の振る舞いと違う
- **根拠:** `IosNotificationManager.cs:302` は「null to keep the original trigger」と書いています。
- **実際:** 1.x（1.13.0 の `NotificationRepositoryImpl.update`）も 2.0.0（`Notification.h:239-240`）も、null なら即時に出します。
- **直し案:** 「null（または読めない trigger）なら即時に出す」に直す。

### R-15 [C] 観点 6: 命名と置き場所
- **入れ子の型名:** `IosNotificationRequests.Action`（`:53`）が `System.Action` を隠します。`NotificationAction` などに改名する。
- **リーダーの置き場所:** Notification が Clipboard の名前空間の `JsonValue` に依存しています。`Members`・`RawNumber` も Notification のために Clipboard 側へ足しました。共通の部品なら `iOS/Common` へ移す。
- **`EmptyMembers`:** 書き換えられる静的な Dictionary を `IReadOnlyDictionary` として返しています。

件数: A1 0 件、A2 1 件、B 4 件、C 10 件

**主なファイル:**
- `/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationManager.cs`
- `/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationRequests.cs`
- `/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Common/IosNextFrameQueue.cs`
- `/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/NotificationUITests.swift`

R-2 の実測に使った小さなプログラムは `/private/tmp/claude-501/-Users-jonghyunkim-Desktop-unity-native-plugin/e79ff5e3-40bb-4aaf-b68b-47a27ab860c6/scratchpad/dbl/t.cs` にあります。
## 2. Codex

**R-1 — 区分：A1**

旧パーサーが受理する trigger を読めず、予約通知を即時配信する場合があります。

- **根拠：** [IosNotificationRequests.cs:190](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationRequests.cs:190) は Clipboard 用リーダーを使います。このリーダーは先頭の BOM を読み飛ばさず、深さも 64 に制限します（[IosClipboardJsonReader.cs:469](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardJsonReader.cs:469)）。1.13.0 の `UnityIosNotificationJsonParser.swift:101–102` は Foundation の `JSONSerialization` を使っていました。Foundation での受理と、C# リーダーでの拒否をメモリ上の実行でも確認しました。
- **失敗例：** 権限取得済みで、`ShowNotification` に `"﻿{\"type\":\"timeInterval\",\"interval\":3600}"` を渡すと、新実装は読めない trigger を無指定として扱い、1 時間後ではなく即時に通知します（[IosNotificationManager.cs:511](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationManager.cs:511)）。未知の項目に 70 段の配列を持つ有効な trigger でも同様です。設計 3 章には、この互換性差分がありません。
- **修正：** Notification の読み取りでは先頭 BOM を処理し、深さの上限も旧パーサーの受理範囲に合わせてください。これらの入力が即時配信へ変わらないことをテストする必要があります。

**R-2 — 区分：A2**

ログ処理からの例外が、ネイティブコールバックの外へ抜けます。

- **根拠：** [IosNotificationManager.cs:842](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationManager.cs:842) の `OnIgnoredDone` は、保護せず `Debug.LogWarning` を呼びます。また、`Guard` の catch 内のログも保護されていません（[同ファイル:890](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationManager.cs:890)）。実際の Unity アセンブリで、独自 `ILogHandler` の例外が `Debug.LogWarning` から伝播することを確認しました。
- **失敗例：** ログハンドラーが例外を投げる構成で、背景からの設定画面表示が `NOT_FOREGROUND` で完了すると、`OnIgnoredDone` の警告出力から例外が reverse P/Invoke の外へ抜けます。これは同梱 `Notification.h:36` の例外禁止契約に反し、クラッシュの原因になります。
- **修正：** ログ出力自体にも例外を外へ出さない保護を設け、各ネイティブ受け口の catch 内から使ってください。`OnIgnoredDone` に既存の `Guard` を付けるだけでは不十分です。

**R-3 — 区分：A2**

待ち行列からディスパッチャーへの移送中に、別スレッドの結果が追い越せます。

- **根拠：** [IosNextFrameQueue.cs:90](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Common/IosNextFrameQueue.cs:90) は待機項目を削除してロックを解放し、その後にディスパッチャーへ追加します。一方、[同ファイル:65](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Common/IosNextFrameQueue.cs:65) は待機表が空なら直接ディスパッチャーへ追加します。
- **失敗例：** `HandOver` が A を取り出して追加する前に、別スレッドが `EnqueueBehind(B)` を呼ぶと、配送順は B → A になります。実コードをメモリ上でコンパイルし、この実行順を固定して再現しました。影響範囲はクラスコメントが認める複数スレッド利用です。通常の通知ネイティブ完了は主スレッドです。
- **修正：** 移送中の項目も含めて順序を管理し、単一の配送経路にしてください。二つのロックを単純に入れ子にすると、購読者からの再入時に逆順取得が生じるため避ける必要があります。

**R-4 — 区分：B**

NU-10 は、権限要求の完了が消失しても成功します。

- **根拠：** [NotificationUITests.swift:364](/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/NotificationUITests.swift:364) は、結果が空でも合格にします。設計 3 章も意図的に「記録」扱いですが、同梱 `Notification.h:30–33` は受理した要求の完了をちょうど 1 回保証し、ネイティブの `NotificationReceiver.finishPermission` も完了を返す実装です。
- **見逃す例：** 非活性状態からの要求でダイアログだけ表示され、回答後の完了が失われる不具合があってもテストは通ります。その場合、C# の pending 表にも受け手が残ります。これは見逃しの指摘であり、現実に完了消失が発生したという報告ではありません。
- **修正：** 回答後の完了を期限内にちょうど 1 回要求してください。観測記録は、合否判定とは別に残せます。

**R-5 — 区分：B**

「後のフレームに完了する」という Player テストの主張が assertion にありません。

- **根拠：** [IosNotificationPlayerTests.cs:427](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosNotificationPlayerTests.cs:427) の `NP15_RegisterCategoryCompletesInALaterFrame` は、呼び出し時と完了時のフレーム番号を取得しますが、差をログに出すだけです。assertion は成功と「呼び出し内部ではないこと」だけです（[同ファイル:437](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosNotificationPlayerTests.cs:437)）。
- **見逃す例：** 呼び出しから戻った後、同じフレームのディスパッチャー処理中に完了する実装でも合格します。Editor テストは使用不能経路を確認しますが、Player のカテゴリー登録経路の保証にはなりません。
- **修正：** `Assert.Greater(callbackFrame, frame)` を追加し、ディスパッチャーの実行中から呼ぶ条件でも検証してください。

件数：A1＝1、A2＝2、B＝2、C＝0。

（補足：Codex は、ネイティブ側が呼び出し中に入力をコピーするため、C# 側が戻り直後にビルダーと文字列を解放する順序には問題がないと報告しています。）
## 3. 判断

A1・A2・B を直し、安い C も直す。直した後の設計は v5（[../designs/2026-11-15-ios-notification-cabi-design-v5.md](../designs/2026-11-15-ios-notification-cabi-design-v5.md)。レビューしていない）。

| 指摘 | 判断 | 直し |
|---|---|---|
| Codex R-1（BOM と 64 段で trigger が読めず、即時に出る） | 直す（A1） | Notification の読み手は先頭の BOM を飛ばし、512 段まで読む（JSONSerialization に合わせる）。EditMode で確かめる |
| Codex R-2、サブ R-5（受け口のログから例外が漏れる、`OnIgnoredDone`） | 直す | 受け口の中のログは例外を止める補助を通す。`OnIgnoredDone` の文を「結果の無い呼び出しの失敗」に |
| Codex R-3（別のスレッドの結果が渡している途中を追い越す） | 直す | 渡している間は、後ろに並べる結果も一覧に入れ、渡し終えたらもう一度回す（2 つの lock を入れ子にしない） |
| Codex R-4、サブ R-4（NU-10 は結果が来なくても通る） | 直さない（記録） | 結果が来ないのは 1.x と同じで、C ABI の「ちょうど 1 回」と食い違う。native-toolkit への報告の候補として結果の記録に書く（報告の前に native の `[finishPermission]` のログで裏を取る）。テストは記録のまま |
| Codex R-5（NP-15 がフレームを照らさない） | 直す | `Assert.Greater(callbackFrame, frame)` |
| サブ R-1（生成で delegate を入れ直さない違いが表に無い、登録の再試行） | 直す（B） | 設計 3 章とクラスの doc に既知の差分として書く。受け手の登録は、公開の呼び出しのたびに（主スレッドで）やり直す |
| サブ R-2（2^53 を超える整数、`1e400`） | 直す（A2） | 有限でない値と、double に戻して一致しない整数は読まない。EditMode に 4 値 |
| サブ R-3（拒みと例外の経路が照らされない） | 直す（B） | `Begin` を internal にし、Editor の PlayMode で種類ごとの失敗、例外、戻る前の完了を照らす |
| サブ R-6（一覧の JSON で投げると受け手が失われる） | 直す | JSON 化を try で囲み、失敗は `"[]"` |
| サブ R-7・R-8（主スレッドの判定、登録の排他） | 直す | 主スレッドの番号は Play の始めにだけ決める。`Initialize()` は主スレッドの外なら何もしない。登録は主スレッドだけ |
| サブ R-9（深さ、対の無いサロゲート） | 一部直す | 深さは Codex R-1 で直した。サロゲートは珍しい入力として直さない |
| サブ R-10（テストの穴） | 直す | thread・target content・category・action・text input の U+0000、入れ子の文字列の中の括弧、受け手が投げてもイベントが出ること |
| サブ R-11（NP-19 が破棄の後の完了を確かめない） | 直す | 受け手の中で古い Manager が壊れていることを照らす（`DestroyImmediate`） |
| サブ R-12・R-14・R-15（版の参照、`UpdateNotification` の doc、入れ子の型名、読み手の置き場所） | 一部直す | 版を v4 に、doc を「null なら即時」に、`Action` を `ActionItem` に。読み手を Common に移すのは段 4 の候補 |
| サブ R-13（入口のログ） | 直す | 完了の芯と `Begin` に入口のログ（値は出さない） |
