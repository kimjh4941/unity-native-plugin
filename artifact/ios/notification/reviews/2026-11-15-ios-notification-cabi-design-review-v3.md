# iOS Notification の C ABI への写し方の設計 v3 のレビュー v3

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-notification-cabi-design-v3.md](../designs/2026-11-15-ios-notification-cabi-design-v3.md)
- 前の回: [2026-11-15-ios-notification-cabi-design-review-v2.md](2026-11-15-ios-notification-cabi-design-review-v2.md)
- レビュアー: サブエージェント（general-purpose）と `codex:codex-rescue` の 2 本を並行で回した。3 回目（決まりの上限）なので差分を見た。`codex:codex-rescue` の出力は今回も「`codex:codex-rescue` は利用できず、補助のレビュアー 1 名と照合した」と書いている（2 章の末尾）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

設計書 v3 の 3 回目のレビュー（差分のレビュー）です。ファイルは編集していません。

レビューの途中で、作業ツリーの `IosNextFrameQueue.cs` が書き換わりました（15:18 の更新。`EnsurePump`・`EnqueueBehind` が足されています）。実装について書いた内容は、15:20 時点のファイルに基づきます。

確かめ方は次のとおりです。
- Swift の型の橋渡しは、手元の macOS で `swiftc` を使い、`JSONSerialization` の結果を実際に動かして照らしました。
- `URL(string:)` が nil を返す文字列も、同じく動かして確かめました。
- native-toolkit は HEAD `1df83126` と、タグ `1.13.0` を見ました。

## (1) 2 回目の A1・B は v3 で直ったか

| 2 回目の指摘 | v3 | 判定 |
|---|---|---|
| 2 章 R2-1 [A1]（`fileURL` の U+0000） | D-16（63 行） | 直っている。`fileURL` と音の名前を拒む。<br>補足: 1.x の `URL(string:)` は U+0000 を `%00` に符号化して受けていた（macOS で確認）。だから 3 章 94 行の「全文が届いた」は正しい |
| 2 章 R2-2 [A1]（空の識別子の添付） | D-4（51 行） | 直っている。2.0.0 の `addAttachment` も、空の識別子を拒まない |
| 2 章 R2-3 [B]（ハンドルの所有と例外の境） | D-2（49 行） | 直っている |
| 2 章 R2-4 [B]（直したライフサイクルの確かめ） | 4 章 114 行 | 一部だけ。「起動時の install を無効にした状態から、明示の `Initialize()` で登録し直す」の確かめが、判断の表にも 4 章にも無い（R-4） |
| 1 章 R-1 [B]（抱えられた settings request） | D-12 | 直っている。補足: 1.x の要求の options は `[.alert, .sound, .badge]`（`1.13.0` の `IosNotificationManager.swift:272`）。だから「ホストが native で求めたアプリだけ」は正しい。3 章の文言には R-3 がある |
| 1 章 R-2 [B]（値の飛ばし方） | D-12、4 章 | 直っている |
| 1 章 R-3 [B]（呼んだ順と結果の順） | D-3 | 直っている。ただし、ディスパッチャーが壊れた後に新しい誤りがある（R-1） |
| 1 章 R-4 [B]（型の橋渡し） | D-4 | 直っている（下に実測）。残りは R-6 |
| 1 章 R-5 [B]（スレッド） | D-2・D-3 | 直っている。`Notification.h` の冒頭にも「install 以外はどのスレッドから呼んでもよい」とある |

型の橋渡しの実測（`as? Bool` / `as? Int` / `as? Double`）は次のとおり。v3 の D-4 と一致しました。

| 値 | Bool | Int | Double |
|---|---|---|---|
| `0` | false | 0 | 0.0 |
| `0.0` | false | 0 | 0.0 |
| `-0.0` | false | 0 | -0.0 |
| `1.0` | true | 1 | 1.0 |
| `2147483648` | nil | 2147483648 | 2147483648.0 |
| `1e2` | nil | 100 | 100.0 |
| `100.0` | nil | 100 | 100.0 |
| `0.1`・`35.6812` | nil | nil | そのまま |
| `true` | true | 1 | 1.0 |

例外が 1 つあります。`9007199254740993` の `as? Double` は nil でした（R-6）。

## (2) v3 で持ち込まれた誤りと抜け

### R-1 [B] D-3: ディスパッチャーが壊れた後にポンプを付け直す手順が無く、待ちの後ろに並んだ結果とイベントが止まる
- **根拠**:
  - v3 の D-3（50 行）は「ポンプは Manager の Awake で作る。ディスパッチャーが壊れたら、新しいディスパッチャーに付くポンプが続きを渡す」としている。
  - しかし Awake は Manager 1 つにつき 1 回しか走らない。
  - `UnityMainThreadDispatcher.Instance`（`Runtime/Common/UnityMainThreadDispatcher.cs:24-35`）は、新しいディスパッチャーを作るだけで、ポンプは付けない。
  - 新しいポンプを誰が付けるのかが、設計に無い。
  - 一方で、同じ D-3 の「待ち行列に何か待っている間は、ネイティブの完了の結果もその後ろに並べる」と、D-12 の「イベントも同じ経路」によって、待ちが 1 件でも残っていると、後から来るすべての結果とイベントがその後ろに並ぶ。
  - ディスパッチャーの破棄は、このリポジトリで既に考えている場面。Clipboard はテストを持ち（`Tests/PlayMode/IosClipboardManagerIntegrationTests.cs:817-838`）、壊れたら「捨ててログ」にしている（`IosClipboardManager.cs:1809-1815, 1858-1880`）。
- **壊れ方**:
  1. フレーム N に呼び出しが拒まれ、待ちに入る。
  2. 同じフレームでディスパッチャーが壊される（テストの `DestroyImmediate`、利用者のコード）。ポンプも一緒に消える。
  3. その後の呼び出しのネイティブの完了と、通知のタップのイベントが、すべて待ちの後ろに積まれ続ける。
  4. 主スレッドで C# の拒みがもう一度起きるまで、どれも届かない（今の実装では、主スレッドの `Enqueue` だけがポンプを付け直す）。
  5. その後の呼び出しがすべてネイティブで受け付けられると、受け手もイベントも二度と呼ばれない。D-2 の「完了はちょうど 1 回」が成り立たなくなる。
  - 4 章の Editor の PlayMode の確かめに、ディスパッチャーの破棄は無いので捕まらない。
- **直し案**: 次のどちらかを D-3 に書き、Editor の PlayMode に Clipboard と同じ破棄のテスト（「待ちを残して破棄 → ネイティブの完了を流す → 届く、または捨てた旨のログ」）を足す。
  - 主スレッドで待ち行列に触れる所（主スレッドの `Enqueue`、ネイティブの結果を並べる所、ポンプの代わりの見張り）では、毎回ポンプを確かめて付け直す。ネイティブの完了と interaction は F-8 により主スレッドで来るので、そこで付け直せる。
  - または Clipboard と同じく「壊れたら待ちを捨ててログ」。

### R-2 [C] D-16 と D-5 (a): Schedule・Update の content の `id` の U+0000 を拒む理由が成り立たず、「引数は 1.x と同じく切れる」とも食い違う
- **根拠**:
  - `Notification.h` の update・schedule は「The id of content is not used」と書いている。
  - 1.x も、update・schedule では引数の `identifier` を使っていた（`1.13.0` の `NotificationRepositoryImpl.swift:38-55`）。
  - v3 の D-16 は、JSON の中の識別子に content の `id` を含めて拒む。D-5 (a) は、空の `id` を引数の `identifier` で埋める。実装も同じ形（`IosNotificationRequests.cs:100, 161`）。
- **壊れ方**:
  - `ScheduleNotification({"id":"a\u0000b",…}, trigger, "x")` は、1.x では成功、v3 では `The notification request is invalid.`。content の `id` はどこにも使われないので、「別の通知を指す」という D-16 の理由が当てはまらない。
  - `ScheduleNotification({"id":"",…}, trigger, "a\0b")` は、埋めた `id` に U+0000 が入って拒まれる。3 章 96 行の「引数の U+0000 は切れる、1.x と同じ」と食い違う。
- **直し案**:
  - D-16 で拒む content の `id` を「Show のとき」に絞る。
  - Schedule・Update では C# は `id` を見ず、`content_create` には空でない固定の値（または引数から U+0000 以降を落とした値）を渡す、と D-5 (a) に書く。
  - 3 章 94 行を合わせる。

### R-3 [C] 3 章 97 行: settings request の「何もしない」は、前の delegate がいないときだけ正しい
- **根拠**: 2.0.0 の proxy は、印の無い通知の要求と、Settings アプリからの要求（notification が nil）を、前の delegate が `openSettingsFor` を持っていればそちらに回す。ライブラリが自分で扱う（受け手に渡す、抱える）のは、それ以外のときだけ（`IosLibrary/Notification/Presentation/Delegate/NotificationDelegateProxy.swift:117-123`、`NotificationDelegateRouter.swift:319-339`）。
- **直し案**: 93 行のタップの行と同じく、「前の delegate があればそちらへ回り、無ければ何もしない」と書く。ホストが native で求めたアプリは、自分の delegate で受けていることが多いので、差分の中身も変わる。

### R-4 [C] 2 章 R2-4 の「起動時の install 無効 → 明示の `Initialize()`」の確かめが落ちている
- **根拠**: 2 回目の判断の表（3 章 173 行）は、R2-4 のうち、破棄・不在・settings request だけを扱っている。D-13 の「Awake の登録が `NOT_INITIALIZED` なら、`Initialize()` で install して登録し直す」経路は、4 章にも 5 章にも無い。既存の probe（`Assets/NativeToolkitIosPlayerTests/Probe/IosNotificationDelegateProbeTests.cs:35-44`）が見るのは delegate のクラスだけ。
- **壊れ方**: `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` を使うアプリで登録し直しが壊れても、イベントが一切来なくなるだけで、計画した確かめはすべて通る。
- **直し案**: 次のどちらかにする。
  - define 付きの probe の回に「Awake では未登録 → `Initialize()` の後は登録済み」を 1 件足す。
  - 確かめないことを判断として書く。

### R-5 [C] 4・5 章と D-1 の書き残し
- 4 章の表（108-116 行）に、`IosNotificationRequests.cs`・`IosNotificationUserInfo.cs`・`IosNextFrameQueuePump.cs` が無い。D-1（48 行）は「変換は `IosNotificationCApi.cs`」「この 2 つは iOS だけの型を使わない」としている。V-1 で iOS 以外を対象にして組めることを確かめる対象が、実際には 5 ファイルある。
- V-2（124 行）の「Notification 15 件」: 4 章で足すテスト（作業ツリーでは `NP19_ACallWaitingAtDestroyStillGetsItsResult`）を入れ、`UnavailableUntilMoved` を消すと 16 件になる。

### R-6 [C] D-4 の「1.x の Swift の読み方を写す」で残る差
- `JSONSerialization` が読めない数（例 `1e400`。macOS で「Number wound up as NaN」）が 1 つでもあれば、1.x では content 全体が読めず `Failed to parse notification content JSON` になった。
- `IosClipboardJsonReader` は数を字面でしか見ない（`IosClipboardJsonReader.cs:687-748`）。v3 での結果は場所で変わる。
  - `badge` なら無視して成功。
  - userInfo の中なら、そのまま書き戻し、ネイティブが拒んで `The notification request is invalid.` になる。
- 小数の項目（`interval`・緯度経度・`radius`・`relevanceScore`）で、2^53 を超えて Double でちょうど表せない整数（例 `9007199254740993`）は、`as? Double` が nil。1.x では無視・読めない扱いだった。
- どちらも珍しい入力。3 章に 1 行足すか、写さないことを D-4 に一言書けば足りる。

## 実装について（設計の指摘とは別）

v3 をまだ反映していないと伝えられた項目（待ちの後ろ、主スレッドの外、settings の受け手、`fileURL`・音の名前、引数の切り捨て、0・1 の規則、受け口の芯）は除きました。15:18 の書き換えで入った部分は読んでいます。

- **I-1（R-1 の実装側）**: `IosNextFrameQueue.EnqueueBehind`（`Runtime/iOS/Common/IosNextFrameQueue.cs:61-72`）と `HandOver` は、ポンプを確かめない。ポンプを付け直すのは、主スレッドの `Enqueue`（51 行）だけ。ディスパッチャーが壊れた後に待ちが 1 件でも残ると、ネイティブの結果とイベントは `Due` のまま積まれ続ける。`EnqueueBehind` は常に主スレッド（ネイティブの完了と interaction）なので、そこで `EnsurePump()` を呼べば塞がる。
- **I-2**: `IosNotificationManager.OnInteraction` の上に `<summary>` が 2 つある（片方は `DeliverInteraction` の写し）。
- **I-3**: 次の doc がまだ「design v2」を指している。
  - `IosNotificationPendingCalls.cs:11`
  - `IosNotificationUserInfo.cs:11`
  - `IosNotificationRequests.cs:14`
  - `IosNextFrameQueue` は v3 に更新済み。

件数: A1 0 件、A2 0 件、B 1 件、C 5 件
## 2. Codex（`codex:codex-rescue`）

差分レビューでは、**設計の A1 はありません**。R2-1・R2-2 は解消、R2-3・R2-4 は検証面が一部残っています。ファイルは変更していません。

| 前回の指摘 | v3 の判定 |
|---|---|
| R2-1・R2-2 | 解消。`fileURL` の U+0000 を拒否し、空の添付識別子を保持する。D-4・D-16（設計書 v3:51） |
| R2-3 | 所有権の方針は解消。検証の残件は R3-1 |
| R2-4 | 破棄後の完了・不在中イベントの試験を追加。登録経路の残件は R3-2（設計書 v3:114） |
| 第1章の B：R-1〜R-5 | settings request、値のスキップ、配送順、数値変換、スレッドの修正を反映（D-2〜D-4、D-12。設計書 v3:49〜59） |

設計書: /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/notification/designs/2026-11-15-ios-notification-cabi-design-v3.md

**R3-1 — 区分：B：R2-3 の解放回数・例外境界を検証できない**

- **根拠：** D-2（v3:49）はコピー・`finally` 解放・例外隔離を要求しています。しかし試験計画（v3:114）が対象とするのは結果組み立ての芯と配送です。受信側所有の契約は Notification.h:138、例外漏出禁止は同:36 にあります。
- **失敗シナリオ：** ハンドル変換中の例外や Manager 不在時に、解放漏れ・二重解放があっても、コピー済みの値を渡す試験は通ります。
- **修正：** 読み取り・解放を差し替えられる受信処理を検証対象にします。正常・変換例外・Manager 不在・利用者コールバック null の各場合で、解放が各1回、例外漏出が0回であることを確認します。

**R3-2 — 区分：B：R2-4 の初回登録拒否→明示 Initialize の回帰試験が残る**

- **根拠：** D-13（v3:60）は再登録を要求していますが、試験計画（v3:114）にその遷移がありません。既存の Assets/NativeToolkitIosPlayerTests/Probe/IosNotificationDelegateProbeTests.cs:35 は起動時の delegate を確認するだけです。native は未 install の登録を拒否します（/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Notification/NotificationReceiver.swift:497）。
- **失敗シナリオ：** 初回の `NOT_INITIALIZED` 後に登録済みフラグを誤って立て、`Initialize()` 後も interaction が届かなくても、予定した試験では検出できません。
- **修正：** 「Awake で登録拒否→明示 install→登録成功→再 Initialize／Manager 再生成でも重複登録なし」を試験に追加します。変更した D-12 に合わせ、settings request の登録回数が0であることも照合します。

以下は**部分実装側の確認事項**です。設計の誤決定とは分けています。動作実行ではなく、コードの静的照合による指摘です。

**R3-3 — 区分：B：dispatcher 破棄後のポンプ復旧が実装・検証されていない**

- **根拠：** D-3（v3:50）は復旧を要求しています。しかし Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Common/IosNextFrameQueue.cs:61 の `EnqueueBehind` は待機項目があればポンプを確保せず戻ります。Runtime/Common/UnityMainThreadDispatcher.cs:28 の dispatcher 再生成もポンプを付けません。試験計画（v3:114）に dispatcher 破棄はありません。
- **失敗シナリオ：** 拒否結果が待機中に dispatcher の GameObject を破棄し、Manager を生存させたまま native 完了が来ると、後続の主スレッド `Enqueue` がない限り、両方の結果が滞留します。dispatcher だけを再生成しても配送は再開しません。
- **修正：** Manager の再 Awake に依存しないポンプ復旧経路を実装し、この条件で結果が順番どおり各1回届く PlayMode 試験を追加します。

**R3-4 — 区分：B：空の content.id を補完すると、引数の U+0000 を拒否してしまう**

- **根拠：** Runtime/iOS/Notification/IosNotificationRequests.cs:100 は引数を空の `content.Id` に代入し、同:162 でその U+0000 を拒否します。D-16（v3:63）は引数を切って渡す決定です。
- **失敗シナリオ：** 有効な trigger と `{"id":"","title":"t"}`、引数 `"a\0suffix"` で Schedule／Update を呼ぶと、設計上は `"a"` を使うところが、C# の invalid になります。
- **修正：** JSON 由来の識別子の検査と引数からの補完を分け、補完には C 文字列として渡る接頭部を使います。D-5 と D-16 の組み合わせとして試験に追加します。

文書の機械照合は FAIL 0 件でした。Unity／実機試験は実行していません。補助レビュアー1名と照合し、`codex:codex-rescue` は利用できませんでした。

件数：A1 0件、A2 0件、B 4件（設計・検証2件、部分実装2件）、C 0件、合計4件。
## 3. 判断

**2 人とも A1 が 0 なので、ここで止める。** 止めた後の直しは v4 に入れ、v4 はレビューしない（決まり）。中心の約束に関わる B と、安い C を直す。

| 指摘 | 判断 | v4 での直し |
|---|---|---|
| 1 章 R-1、2 章 R3-3（ディスパッチャーが壊れた後にポンプが付かない） | 直す（B） | ネイティブの結果を並べる所（主スレッド）でもポンプを確かめて付け直す（実装済み）。Editor の PlayMode に、待ちを残してディスパッチャーを壊した後も結果が届くことを足す |
| 2 章 R3-4、1 章 R-2（content の `id` を空から埋めると引数の U+0000 で拒まれる、Schedule・Update の `id` の U+0000） | 直す | Schedule・Update では content の `id` は使われないので U+0000 で拒まない。`content_create` には `id` を U+0000 の前で切った値（空なら引数を U+0000 の前で切った値）を渡す。拒むのは Show の `id` だけ |
| 2 章 R3-1（ハンドルの解放の回数と例外の境の確かめ） | 確かめない（判断） | ハンドルの読み取りと解放は Player だけで組まれる。解放は `finally` の 1 か所で、受け口の芯は Editor で照らす。解放の回数を数える仕組みは足さない |
| 2 章 R3-2、1 章 R-4（起動時の install 無効から `Initialize()` で登録し直す確かめ） | 確かめない（判断） | `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` の回は今の probe だけ。登録し直しは段 4 の確かめの候補として記録に残す |
| 1 章 R-3（settings request の行） | 直す | 3 章の行を「前の delegate があればそちらへ回り、無ければ何もしない」に |
| 1 章 R-5（4・5 章の書き残し） | 直す | 4 章にファイルを足し、V-2 を 16 件に |
| 1 章 R-6（読めない数、2^53 を超える整数） | 直す | D-4 に「写さない」と一言、3 章に 1 行 |
| 1 章 I-2・I-3（重なった summary、版の参照） | 直す | 実装で直す |
