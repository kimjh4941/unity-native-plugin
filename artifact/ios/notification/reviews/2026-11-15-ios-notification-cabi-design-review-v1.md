# iOS Notification の C ABI への写し方の設計 v1 のレビュー v1

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-notification-cabi-design-v1.md](../designs/2026-11-15-ios-notification-cabi-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

設計書 `artifact/ios/notification/designs/2026-11-15-ios-notification-cabi-design-v1.md` の 1 回目のレビューです。設計書を含め、ファイルは何も編集していません。

先に一点だけ。作業ツリーに未コミットの `Runtime/iOS/Notification/IosNotificationCApi.cs`（14:51 に作られたもの）と、`IosClipboardJsonReader.cs` の変更（`RawNumber` の追加）があります。実装はレビューの範囲外なので読んでいません。読み手の性質は変更後のファイルで確かめました。

### R-1 [A1] 観点 5: 状態の列挙が `#if UNITY_IOS` だけで組まれ、iOS 以外を対象にした Editor でパッケージがコンパイルできなくなる
- **根拠**:
  - `Runtime/iOS/Notification/IosNotificationAuthorizationStatus.cs:3` は `#if UNITY_IOS` だけで組まれる。ほかの iOS の型は `UNITY_IOS || UNITY_EDITOR`。
  - 設計は、変換を `IosNotificationCApi.cs`（`UNITY_IOS || UNITY_EDITOR`）に置く（D-1、36 行）。その中に「状態の写し方」を入れ（4 章 78 行）、列挙を `IosNotificationAuthorizationStatus` に写す（D-8）。D-2 の表にも状態の受け手（`Action<IosNotificationAuthorizationStatus>`）が入る。EditMode のテストも「状態」を照らす。
  - V-1 の `scripts/verify_unity_ios.sh:76` は iOS Profile でしか組まない。
- **壊れ方**: 書いてあるとおりに実装すると、Editor の対象が Android・Windows・Standalone のとき `UNITY_IOS` が定義されない。`IosNotificationCApi.cs`、`IosNotificationPendingCalls.cs`、EditMode のテストが CS0246 で止まり、Runtime の asmdef ごと組めなくなる。iOS 以外の利用者を含めて、パッケージ全体が使えなくなる。5 章の確かめは iOS の対象だけなので捕まらない。
- **直し案**: 4 章に「`IosNotificationAuthorizationStatus.cs` の guard を `UNITY_IOS || UNITY_EDITOR` にする」を足す（Parser も同じ guard になる）。5 章に、iOS 以外の対象でのコンパイル（例: `verify_unity_android.sh` の EditMode か、Standalone の Profile での compile）を足す。

### R-2 [A1] 観点 5・2: パッケージのサンプルが 1.x の失敗の文で拒否を見分けていて、D-6 で黙って壊れる
- **根拠**:
  - `Runtime/UI/iOS/Notification/IosNotificationManagerExampleController.cs:882-893` の `IsPermissionDeniedError` は、`notificationerror` と `error4`／`エラー4` を含むかで拒否を判定している。
  - 205 行（RequestPermission）と 917-921 行（`FormatResult`）がこれを使い、拒否のときに設定の案内を出す。
  - D-6 は拒否の文を `Notification permission was denied.` に替える。4 章にこのサンプルは無い。
- **壊れ方**: サンプルの画面で RequestPermission を拒否する。1.x では「✗ RequestPermission」と設定の案内が出た。2.0.0 では判定が false になり、「Error: Notification permission was denied.」としか出ない。案内が黙って出なくなる。このサンプルは利用者が写す手本でもある。V-1〜V-4 にサンプルの確かめは無いので捕まらない。
- **直し案**: 4 章に `IosNotificationManagerExampleController` を足す。判定は新しい文言の定数（internal const を共有する）か `GetAuthorizationStatus == Denied` で行う。5 章に「サンプルで拒否して案内が出ること」を足す（Share の V-6 と同じく、利用者に頼む項目でよい）。移行ガイド（段 4）に「失敗の文で分岐していたコードは直す」を渡す。

### R-3 [A2] 観点 1・4: NU-10（前面で非活性）の 2.0.0 の見込みが一次ソースと合わない
- **根拠**:
  - native-toolkit の `ios/IosLibrary/IosLibrary/Common/Presentation/ForegroundLocator.swift:61-63` は、`foregroundInactive` の scene も前面の候補に数える。
  - 同 `PresentationProcedure.swift:153-154` は `.foreground` をこの候補で判定する。
  - `Notification/Presentation/Permission/NotificationPresentationGate.swift:61-76` は、前面と判定すれば `requestAuthorization` を呼ぶ。`NOT_FOREGROUND` になるのは背景のときだけ。
  - ハーネスの NU-10 は `state=foregroundInactive`（`NotificationUITests.swift:344-356`）。
- **壊れ方**: D-8（43 行）と 3 章の NU-10 の行（68 行）は「`The app is not in the foreground.` がすぐ来る」としている。実際は 1.x と同じく許可のダイアログが出る見込みが高い。その後に結果が来るかは分からない（1.x は来なかった）。来なければ C ABI の「ちょうど 1 回」が成り立たず、D-2 の表に要素が残る。期待をこのまま書けば V-3 で落ちるので、実装中に気づく。
- **直し案**: D-8 の NU-10 の理由と、3 章の NU-10 の行を直す。NU-10 は「ダイアログが出る。結果が来るかは記録する」とする。結果が来なければ native-toolkit への報告の候補にする。`NOT_FOREGROUND` になるのは NU-14（背景）だけ、と分けて書く。

### R-4 [A2] 観点 4: NU-16 の入れ子の null は、権限が未決なので「成功」にならない
- **根拠**:
  - `NotificationUITests.swift:551-566` の `unmeasured` は、`grant()` を呼ばずに Schedule する（権限は未決）。
  - 3 章の NU-16 の行（70 行）は「null は取り除いて成功」としている。
  - D-7 と F-10 では、権限が無ければ `PERMISSION_DENIED` になる。
- **壊れ方**: 正しく実装すると `[unmeasured@fail] nested-null error=Notification permission was denied.` になり、3 章の期待とずれる。V-3 で気づく。なお、null を取り除かずに渡せば `The notification request is invalid.` になるので、`PERMISSION_DENIED` を期待にすれば D-5(a) の確かめとしては効く。
- **直し案**: NU-16 の nested-null の期待を `PERMISSION_DENIED` の文にする。NP-6 の行と同じく「権限が無いので D-7」と書く。あわせて、Player の NP-6 の主張の文（`"2.0.0 rejects an unknown trigger"`）も D-4 に合わせて直す。

### R-5 [A2] 観点 3: 既存のテストの 2 つの値が D-4・D-5 のどちらにも入っていない
- **根拠**:
  - `IosNotificationPlayerTests.cs` の NP-8 に `schedule-empty-identifier` がある（引数の identifier が ""、content の id は "ntkp-8a"）。1.x は成功（記録 24 行）。2.0.0 は `requiredIdentifier` で拒む（`NotificationReceiver.swift:208`、`Notification.h:247`）。
  - 同じく NP-16 の (b) `empty-action-id`（action の identifier が ""）がある。1.x の Swift は "" を「欠けた」とは見ずに残し、成功した（記録 29 行）。2.0.0 は `NotificationMapping.swift:122, 137-143` で category ごと拒む。
  - D-4 が捨てるのは「欠けた action」だけで、D-5(b) にも 3 章にも、この 2 つは無い。3 章の NP-16 の行は「成功」としている。
- **壊れ方**: このまま実装すると、NP-16(b) は `The notification request is invalid.` になり、3 章の「成功」と食い違う。NP-8 の empty-identifier は 3 章に期待が無い。実装する人がその場で「捨てるか、失敗にするか」を決めることになる。テストがあるので気づきはする。
- **直し案**: D-4 か D-5 で決める。案は、空の identifier の action・text input action は「欠けた」として捨てる（1.x の成功を保つ）。Schedule・Update の空の identifier は (b) の失敗にし、3 章の NP-8 の行に足す。F-9 にも、action の識別子の空、引数の identifier の空、NAMED の空の名前（R-6）の拒みを足す。

### R-6 [B] 観点 3・1: 入口や setter で拒まれるのに、D-5 で扱いが決まっていない値と、setter の失敗の写し方
- **根拠**:
  - `NotificationContentBuilder.swift:184-187`: NAMED の空の名前は INVALID_PARAMETER。
  - `NotificationMapping.swift:128`: text input の空の identifier は拒む。
  - 1.x の `UnityIosNotificationJsonParser.swift:43-48` は `"sound":""` を `.named("")` として通した。67-69 行は `URL(string:)` が nil の添付（例: `"fileURL":""`）を捨てて成功した。
  - D-5 は (a)・(b) を並べるだけで、ここに挙げていない setter の失敗をどう結果にするかを書いていない。申し送り L217 は「setter の戻り値も今の失敗のコールバックに写す」としている。
- **壊れ方**: 次の入力は 1.x では成功したが、2.0.0 では失敗に変わりうる。どれも 3 章に無く、テストも無い。
  - `{"id":"a","title":"t","sound":""}`
  - `"attachments":[{"identifier":"x","fileURL":""}]`
  - text input action の identifier が ""
- **直し案**: D-5 に 3 つを足す（案: 空の sound は省略〈既定〉扱い、`fileURL` が空の要素は 1.x どおり捨てる、空の識別子の text input は捨てる）。もう 1 行足して、setter と `content_create` が NONE を返さないときの写し方を決める。案は「操作を呼ばず、D-6 の文で失敗、ビルダーは free、次の Update で受け手 → イベントの順」。

### R-7 [B] 観点 1・3: D-13 は Awake でも delegate を入れ直すことになり、根拠の I-3・NT-01 と食い違う
- **根拠**:
  - README の I-3（67 行）は「公開の `Initialize()` を入れ直しの口にする（**`Awake` は受け手の登録だけ**）」。
  - part2 の NT-01（557 行）は「C# からは呼ばない」。
  - 今の `IosNotificationManager.cs:107-108` では、Awake が `Initialize()` を呼ぶ。D-12 は「Initialize（Awake から呼ばれる）」、D-13 は「呼ぶと `ntk_notification_install`」としている。
  - `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` の下の `Assets/NativeToolkitIosPlayerTests/Probe/IosNotificationDelegateProbeTests.cs:39-40` は、delegate が proxy で**ない**ことを照らしている。
- **壊れ方**: 書いてあるとおりに実装すると、Manager が作られた時点で必ず install される。入れない設定（I-2）が実質効かなくなり、ホストの delegate の前に proxy が入る。入れない設定で Player のテストを流すと、Notification のテストが先に `Instance` を作った回では、プローブのテストが落ちる。
- **直し案**: Awake は private の「受け手の登録だけ」を呼び、公開の `Initialize()` だけが install と登録をする、と D-12・D-13 に書き分ける。今の Awake → `Initialize()` の呼び出しを替えることを 4 章にも書く。

### R-8 [B] 観点 2: キーの並べ替えと平らな読み手が組み合わさって、userInfo の残るキーが変わる（3 章に無い）
- **根拠**:
  - `NotificationJson.swift:123` はキーを並べ替える（`.sortedKeys`）。
  - `IosNotificationActionResult.cs:55` は、文字列でない値に当たると読むのを止める。
  - 1.x の `UnityIosNotificationManager.swift:220-223` は、並べ替えずに NSDictionary の列挙順で書いた。
  - F-13 は並べ替えにふれているが、3 章は NU-4 の `/` しか挙げていない。
  - NU-4 は文字列の値だけで、ハーネスも結果を並べ替えて出す（`NotificationScenario.cs:165-166`）。このため、どのテストもこの差を見ない。
- **壊れ方**: `{"count":1,"url":"app://x"}` は、2.0.0 では常に `count` が先になり、`UserInfo == null` になる。1.x では列挙順しだいで `{url=app://x}` が得られることがあった。どのキーが残るかが、1.x の「順しだい」から 2.0.0 の「辞書順で非文字列より前だけ」に黙って変わる。
- **直し案**: 3 章に行を足して既知の差分にする。あるいは D-12 で、平らな読み手を「文字列でない値は飛ばして続ける」に直すかを決める（直すなら、それ自体が差分になる）。どちらにしても、文字列と数の混ざる userInfo の期待を EditMode と NU-4 に足す。

### R-9 [C] 観点 1: D-5 の (a)・(b) の理由と、その根拠にした 1.x の測り方
- 予約のキー（NP-8 の `userinfo-reserved-key`）は、1.x では利用者のデータとしてそのまま届いた（ライブラリに印は無かった）。D-5(b) の「1.x でも意味のある通知にならなかった値」には当たらない。理由を「2.0.0 が予約したキーなので失敗にする（既知の差分）」に直す。
- NP-8 と NU-16 の「1.x で成功」は、どれも権限が未決のときの値（記録 12 行、`NotificationUITests.swift` の unmeasured）。権限があるときに 1.x が、負の badge・1.5 の relevance・入れ子の null をどう扱ったかは測っていない。(a) の「1.x と同じ結果になる直し」は見込みだと明記する。

### R-10 [C] 観点 2: Swift の型変換の性質を C# の読み手が写していない
- 1.x は `as? Int`／`as? Bool`／`as? Double` で、JSON の bool と数を行き来させた（NSNumber の橋渡し）。例: `"repeats":1` は true、`"badge":true` は 1。`IosClipboardJsonReader.TryGetBool`・`TryGetInt` は互いに受けない。
- `as? [[String: Any]]`・`as? [String]` は全か無か（`UnityIosNotificationJsonParser.swift:64, 160, 166, 173, 188`）。要素に 1 つでも型違いがあると、1.x は attachments・actions・options を**全部**捨てた。D-4 の「要素ごとに捨てる」とは結果が違う。
- int32 を超える badge や暦の値は、1.x の `Int` なら通るが、`TryGetInt` は捨てる。
- 直し案: D-4 に、どこまで写すかを一行で決める（Builder の出力には出ない値なので、「写さない、既知の差分」でもよい）。

### R-11 [C] 観点 2: D-9 の「1.x と同じ形」と「`/` はエスケープしない」が両立しない
- 1.x の一覧は `JSONSerialization` の既定で書かれ、`/` を `\/` にした。NU-4 の 1.x の `slash=a\/b` が同じ書き手の証拠になる。
- 直し案: 3 章に既知の差分として書く。あわせて、D-9 で文字列のエスケープの規則（制御文字の `\uXXXX` の大小文字、U+2028 など）を決め、EditMode で照らす。1.x の列挙順はもともと不定だったので、キーの順は今の案のままでよい。

### R-12 [C] 観点 2: 3 章に無い、そのほかの小さな差分
- 権限が provisional のときの RequestPermission: 1.x は OS に再要求し、昇格のダイアログが出た。2.0.0 は求めずに 1 を返す（`NotificationPresentationGate.swift:63-68`、part2 NT-13）。
- D-3 の別々の `try`: 受け手が投げても、イベントは出るようになる。
- Manager の破棄で D-2 の表を空にする: 1.x の受け手は static で、破棄の後にも呼ばれた。
- Manager を作り直すと、その間のタップが後から届く: 1.x では失われた。
- Update が添付で失敗しても、予約が残る: 1.x は先に予約を消した（2.0.0 の `NotificationRepositoryImpl.update`）。
- 印の無い通知（1.x のときに予約されたものなど）: 前の delegate があれば、そちらへ回されてイベントが来ない（`NotificationDelegateRouter.didReceive`）。
- 直し案: 3 章に 1 行ずつ足すか、「段 4 の移行ガイドに渡す」とまとめる。

### R-13 [C] 観点 1・3: 細部
- V-2 の「Notification 14 件」: `NTK_IOS_CABI_2_NOTIFICATION` の下では NP-9 も組まれるので 15 件。
- D-6 は「ライブラリの英文を写す」としながら、NOT_INITIALIZED の文の後半（`Call IosNotificationManager.installDelegate() first.`）を黙って落としている。落とすなら理由を書く。
- D-10 の「静的な空の受け口」と「失敗は LogWarning に出す」は言葉が食い違う。「何も呼ばず失敗だけ記録する受け口」と書く。
- D-12 に、未知の kind（ヘッダー 39 行の「列挙は増えうる」）の既定の枝（例: 捨てて記録する）が無い。
- D-6 の INVALID_PARAMETER の文 `The notification request is invalid.` が、SetBadgeCount と RegisterCategory にも出る。文が操作に合っているか、一言判断を書く。

件数: A1 2 件、A2 3 件、B 3 件、C 5 件

## 2. Codex

**R-1 — 区分：A1：同期拒否の再呼び出しで、同じ Update が終わらなくなる**

設計書:38 は、入口の拒否も dispatcher に積めば「次の Update」になるとしています。しかし Runtime/Common/UnityMainThreadDispatcher.cs:55 は、処理中に追加された項目も同じ `while` で実行します。C ABI の `SetBadgeCount(-1)` は native-toolkit の NotificationReceiver.swift:362 で同期拒否されます。

- 失敗例・検証漏れ：失敗コールバックから同じ操作を再試行すると、拒否結果が現在の Update に追加され続け、Unity が停止します。有限回の呼び出しでも、後続の結果が同じフレームに届きます。既存 NP-13 は値を順に試すだけで、この再入を検証しません（IosNotificationPlayerTests.cs:397）。
- 修正：同期で決まった結果はフレーム境界を保証する待ち行列に置き、後のフレームで dispatcher に渡してください。受け手から再呼び出ししても同じフレームには届かない検証を追加します。

**R-2 — 区分：A1：Manager の破棄で、1.x では届いていた未完了コールバックが消える**

設計書:37 は、Manager の破棄で呼び出し表を空にします。一方、1.x の C#（`3cae23e^` の IosNotificationManager.cs:224–235）は、破棄時に操作の受け手を消しません。同ファイル :594–608 の一覧コールバックも、Manager の有無と関係なく保存した受け手を呼びます。

- 失敗例・検証漏れ：`GetScheduledNotifications(callback)` を呼び、native の結果より先に Manager を破棄すると、新設計では `callback` が一度も呼ばれません。native 側の exactly-once は成立していても、公開 API の受け手には届きません。この差分は3章に無く、既存の Player/UI テストにも、未完了状態での Manager 破棄はありません。
- 修正：未完了操作の受け手は Manager と独立して完了まで保持し、破棄で解除するのはイベント購読に限定してください。破棄後も各呼び出しの受け手が一度だけ呼ばれる検証を追加します。

**R-3 — 区分：A1：Manager 不在中の通常イベントが、再生成後に再生される**

設計書:47 は「破棄でリスナーを外す」「Manager がいなければ捨てる」としています。しかし native は、リスナー不在中のイベントを保持します。native-toolkit の IosLibraryCApi/Common/Registry.swift:319、:398、:452 は、最大32件を保存して次の登録へ渡す実装です。

- 失敗例・検証漏れ：通知を予約した後に Manager を破棄し、その間に通知へ応答してから Manager を再生成すると、通常のアクションや返信まで後から届きます。1.x の C# は破棄時に native のイベント受け手を解除するため、この間の応答は失われました（1.x 側 :229–232、1.x native 1.13.0:ios/IosLibrary/IosLibrary/Notification/IosNotificationManager.swift:345–348）。3章で認めている差分は起動時のタップだけです。NU-8 のプロセス再起動試験では、同一プロセス内の破棄・再生成を検出できません。
- 修正：初回の起動イベント受領と、破棄後の不在期間を区別してください。不在中は破棄する契約なら、native リスナーを残してイベントを読み捨てる方式などが必要です。33件を発生させた場合の保持・破棄と順序も検証します。

**R-4 — 区分：A1：JSON 内の U+0000 が、C ABI への変換で文字列を途中切断する**

設計書:39 は JSON の読み方を移しますが、デコード後の U+0000 の扱いがありません。1.13.0 のパーサーは JSON を Swift の文字列へ読み、そのまま渡しています（1.13.0:ios/UnityIosPlugin/UnityIosPlugin/Notification/UnityIosNotificationJsonParser.swift:34–37,72–86）。1.13.0 の repository もタイトルを直接代入します（NotificationRepositoryImpl.swift:189–193）。

- 失敗例・検証漏れ：`{"id":"n","title":"A\u0000B"}` は、1.x ではタイトル全体が Swift へ渡ります。新経路では Runtime/iOS/Clipboard/IosClipboardJsonReader.cs:155 がエスケープを解き、Runtime/iOS/Common/IosNativeToolkitCApi.cs:64 の UTF-8 変換が内部のゼロを保持します。その結果、native の CApiString.swift:62 には `"A"` だけが渡ります。JSON 内の id や action identifier でも同様です。3章にも、予定する JSON fixture にもこの差分がありません。
- 修正：C 文字列へ渡す各フィールドで U+0000 を検査し、識別子の拒否・表示文字列の置換などを明示的に決めて、既知差分へ追加してください。接尾部が黙って失われないことを確かめる検証も必要です。

**R-5 — 区分：A1：settings request を無視すると、1.x の設定画面遷移が失われる**

設計書:47 は「公開 API が無い」ことを理由に settings request を購読しません。しかし 1.x の native は `openSettingsFor` を受けると `openNotificationSettings()` を実行していました（1.13.0:ios/IosLibrary/IosLibrary/Notification/IosNotificationManager.swift:354–360）。2.0.0 は IosNotificationManager.swift:980 のとおり、イベントを渡すだけで画面を開きません。

- 失敗例・検証漏れ：ホストが初回の権限要求で `providesAppNotificationSettings` を指定しているアプリでは、OS の設定導線から要求が来ても、移行後は何も起きません。この native 側からの指定も Notification.h:305 の対象です。NU-9 は公開メソッドを直接呼ぶ試験なので捕まえられません（NotificationUITests.swift:334）。
- 修正：1.x の遷移を残す内部リスナーを設けるか、この副作用の廃止を3章の既知差分として明記してください。OS からの settings request を直接検証対象に加えます。

**R-6 — 区分：A2：NP-16 の空 action identifier は、設計どおりでは成功しない**

設計書:64 は NP-16 を従来どおり成功とします。しかし D-4 が捨てるのは必須項目の欠けた action で、空文字の識別子は含まれません。1.x のパーサーも空文字を保持します（UnityIosNotificationJsonParser.swift:160–171）。

- 失敗例・検出：`{"identifier":"c","actions":[{"identifier":"","title":"Open"}]}` をそのまま変換すると、NotificationMapping.swift:122 と :138 により category 全体が拒否されます。この入力は IosNotificationPlayerTests.cs:445 にあり、予定する成功期待で検出されるため A2 です。
- 修正：空識別子の action を落として登録を成功させるか、登録失敗を既知差分にしてください。同じ制約を持つ text input action も明記します。

**R-7 — 区分：A2：NP-8 の空の予約 identifier に、2.0.0 の期待が無い**

設計書:40 と :60 は、content の空 id を扱っていますが、`ScheduleNotification` の引数 `identifier=""` を扱っていません。これは別の制約です。

- 失敗例・検出：正常な content と trigger でも、予約 identifier が空なら NotificationReceiver.swift:207 の `requiredIdentifier` により拒否されます。1.x の実測では成功しており、既存 NP-8 にも独立したケースがあります（IosNotificationPlayerTests.cs:323）。
- 修正：空の予約 identifier と空の更新 identifier を既知差分へ追加し、NP-8 の該当ケースを `INVALID_PARAMETER` の文言で照合してください。

**R-8 — 区分：B：Awake の自動 install が、親の初期化方針と矛盾する**

設計書:47 は Awake から `Initialize()` を呼び、:48 はそのたびに install します。一方、参照先の artifact/topics/ios-c-abi-2/README.md:67 は「Awake は受け手の登録だけ」と明記しています。

- 問題になる状態・検証漏れ：`NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` で起動時 install を外し、ホストが明示的な `Initialize()` の時点を管理する構成でも、Manager の生成だけで delegate が置き換わります。native の install は現在の delegate を前任として取り込む操作です（NotificationDelegateInstaller.swift:112）。通常構成の NP/NU ではこの差が見えません。
- 修正：Awake の登録処理と、公開 `Initialize()` の再 install を分離してください。未 install 時の登録拒否、出力ハンドルが NULL のままであること、明示 install 後の再登録を検証します。公開 `Initialize()` の主スレッド制約と失敗時の扱いも明記が必要です。

**R-9 — 区分：B：空の sound 名を、1.x どおりに変換できない**

設計書:17 は、既定名以外の文字列を名前付き sound とします。1.x の実装は `""` も `.named("")` にします（UnityIosNotificationJsonParser.swift:42–50）。対して C ABI は NotificationContentBuilder.swift:184 で空の名前を拒否します。

- 問題になる入力・検証漏れ：`{"id":"n","title":"t","sound":""}` は 1.x のパーサーでは受理されますが、同じ対応付けでは新ビルダーが必ず拒否します。D-5 に変換方針が無く、NP-8 にもこの入力はありません。1.x の OS 上での最終結果の実測が無いため、成功から失敗への変更とは断定せず B とします。
- 修正：1.x の結果を確認したうえで、対応可能な sound へ変換するか、拒否を既知差分にしてください。setter の拒否を無視して既定音のまま成功させる実装も防ぐ必要があります。

**R-10 — 区分：B：配列全体の型変換失敗と、要素単位の除外が区別されていない**

設計書:39 の「欠けた action は捨てる」だけでは、1.x の Swift の配列変換規則を再現できません。1.x の実装は、先に `[[String: Any]]` または `[String]` として配列全体を変換しています（UnityIosNotificationJsonParser.swift:64–65,160–166,173–174,188–192）。

- 曖昧になる入力・検証漏れ：`"actions":[{"identifier":"a","title":"A"},42]` は 1.x では配列全体が使われず、action は0件です。`"options":["foreground",42]` も option 全体が無効です。C# で不正要素だけを飛ばすと、それぞれ action や foreground 指定が残ります。既存 NP-16 の「オブジェクト内の必須項目欠落」「未知の文字列 option」では、この差を捕まえられません。
- 修正：配列全体の型不一致と、型が揃った配列内の必須項目欠落を別規則として明記してください。attachments、actions、textInputActions、options それぞれに混在型の fixture を追加します。

**R-11 — 区分：B：release と native コールバック境界の所有権検証が未定義**

設計書:24 は C ABI の原則を挙げますが、D-2 の連番 `user_data` に対して `release` を NULL にするのか、何を release 時に破棄するのかを決めていません。C ABI は拒否時に、呼び出しスレッドで戻る前に release し、完了は呼びません（Notification.h:25、EntryError.swift:20）。

- 問題になる経路・検証漏れ：release で呼び出し表を消す実装を選ぶと、拒否を処理する時点では受け手が消えている可能性があります。また、一覧・interaction ハンドルは受け取り側所有です（Notification.h:136）。通常の受け手がいない経路や変換例外でも解放が必要ですが、4・5章にはその回数を照合する検証がありません。
- 修正：Share と同じ連番方式なら `release=NULL` と明記し、表の除去責任を完了・同期拒否・呼び出し前例外に割り当ててください。ハンドルの managed 値へのコピーと `finally` 解放、逆コールバックから例外を漏らさないことも検証対象にします。公開 `onResult=null` でも native には非 NULL の固定コールバックを渡し、操作と共通イベントが成立する試験が必要です。

件数：A1 5件、A2 2件、B 4件、C 0件、合計11件。

(注：パスのうち Runtime/…、Tests/… はいずれも /Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/ 配下、NotificationUITests.swift は /Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/ 配下、ios/… の native 側ファイルは /Users/jonghyunkim/Desktop/native-toolkit/ 配下です。)
## 3. 判断

**A1 が両方にあるので止めない。** v2 で A1 と中心の約束の B をすべて直し、2 回目は差分のレビューにする。安い A2・C も入れる。指摘の番号は、サブを「サブ R-n」、Codex を「Codex R-n」と書く。

| 指摘 | 判断 | v2 での直し |
|---|---|---|
| Codex R-1（呼び出しの中で決まった結果を受け手の中から呼び直すと、同じ `Update` が終わらない） | 直す（A1） | 呼び出しの中で決まった結果（入口の拒み、C# の検査の失敗、使えないとき、例外）は、ディスパッチャーの GameObject の常駐の部品が「積んだフレームより後」にディスパッチャーへ渡す共通の待ち行列（`IosNextFrameQueue`）を通す。Editor の PlayMode で確かめる |
| Codex R-2（破棄で表を空にすると、1.x なら届いた受け手が呼ばれない） | 直す（A1） | 表は破棄で空にしない。受け手は完了まで持ち、破棄の後でも呼ぶ（イベントは生きている Manager がいれば出す） |
| Codex R-3、サブ R-12（Manager がいない間のイベントが、作り直した後に届く） | 直す（A1） | interaction の受け手はプロセスの間 1 つだけ登録したまま外さない。Manager がいない間のイベントは解放して捨てる（1.x と同じ）。最初の登録の前に待っていた分（起動時のタップなど）だけが届く（既知の差分） |
| Codex R-4（JSON の中の U+0000 で C の文字列が切れる） | 直す（A1） | 識別子（content の id、引数の identifier、category・thread・target content・action・text input・添付・location の識別子）に U+0000 があれば `The notification request is invalid.`。表示の文字列（title・subtitle・body・ボタンの題・placeholder・filter criteria・音の名前）は値を出さない警告を出して渡す（切れる）。userInfo は JSON のまま渡すのでエスケープのまま届く |
| Codex R-5（settings request を無視すると、1.x の設定の画面への遷移が失われる） | 直す（A1） | settings request の受け手もプロセスの間 1 つ登録し、届いたら設定の画面を開く（1.x と同じ） |
| サブ R-1（状態の列挙が `UNITY_IOS` だけで組まれる） | 直す（A1） | `IosNotificationCApi`・表は iOS だけの型を使わない（状態は 1.x の名前の文字列で返し、Manager が今の Parser で写す。表は受け手を `Delegate` で持つ）。V-1 に Android の Profile の EditMode を足す |
| サブ R-2（サンプルが 1.x の失敗の文で拒否を見分けている） | 直す（A1） | `IosNotificationManagerExampleController.IsPermissionDeniedError` に新しい文を足す（1.x の判定も残す）。サンプルの拒否の確かめは、段 4 の利用者の手の確かめに入れる |
| Codex R-6、サブ R-5（空の action の識別子、空の予約の identifier） | 直す | 空の識別子の action・text input action は「欠けた」として捨てる（1.x の成功を保つ）。Schedule・Update の空の identifier は D-5 (b) の失敗（既知の差分） |
| Codex R-7 | 直す | 同上 |
| Codex R-8、サブ R-7（Awake で delegate を入れ直してしまう） | 直す（B） | Awake は受け手の登録だけ。公開の `Initialize()` だけが install と登録をする（I-3 のとおり） |
| Codex R-9、サブ R-6（空の音の名前、空の `fileURL`、setter の失敗） | 直す（B） | 空の音の名前は省略（既定）として扱う、`fileURL` が空の添付は捨てる。setter と `content_create` が NONE でなければ、操作を呼ばずにその文で失敗（ビルダーは解放、後のフレームに出す） |
| Codex R-10、サブ R-10（配列の全か無か、Swift の型の橋渡し） | 直す（B） | 1.x と同じく、attachments・actions・textInputActions は全要素がオブジェクトでなければ配列ごと無視、options は全要素が文字列でなければ無視。bool と数は行き来させる（`"repeats":1` は true、`"badge":true` は 1）。小数の badge は無視。int32 に入らない値は無視（既知の差分） |
| Codex R-11（release と所有の分担） | 直す（B） | `release` は NULL。表から取り出すのは、完了・入口の拒み・呼ぶ前の例外。ハンドルは値に写してから `finally` で解放。受け口から例外を出さない。`onResult` が null でも C ABI には固定の受け口を渡す |
| サブ R-3（NU-10 の見込み） | 直す | 前面で非活性は前面に数えられ、ダイアログが出る。NU-10 は「ダイアログが出る。結果が来るかを記録する」。`NOT_FOREGROUND` は NU-14（背景）だけ |
| サブ R-4（NU-16 の入れ子の null は権限が未決） | 直す | NU-16 は `PERMISSION_DENIED` の文を期待にする。NP-6 の主張の文も直す |
| サブ R-8（並べ替えと平らな読み手） | 直す（B） | 平らな読み手を「文字列でない値は飛ばして続ける」に直す（1.x は止めていた。既知の差分）。混ざった userInfo の期待を EditMode と NU-4 に足す |
| サブ R-9（理由と測り方） | 直す | 予約のキーの理由を直し、(a) が見込みであることを書く |
| サブ R-11（一覧の `/`） | 直す | 3 章に既知の差分。D-9 にエスケープの規則を書き、EditMode で照らす |
| サブ R-12（小さな差分） | 直す | 3 章に行を足す（provisional の再要求、別々の `try`、添付の失敗で予約が残る、印の無い通知） |
| サブ R-13（細部） | 直す | NP は 15 件、NOT_INITIALIZED の文の理由、D-10 の書き方、未知の kind は捨てて記録、INVALID の文はどの操作でも同じでよいと書く |
