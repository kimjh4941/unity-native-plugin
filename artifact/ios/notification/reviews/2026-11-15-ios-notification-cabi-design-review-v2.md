# iOS Notification の C ABI への写し方の設計 v2 のレビュー v2

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-notification-cabi-design-v2.md](../designs/2026-11-15-ios-notification-cabi-design-v2.md)
- 前の回: [2026-11-15-ios-notification-cabi-design-review-v1.md](2026-11-15-ios-notification-cabi-design-review-v1.md)
- レビュアー: サブエージェント（general-purpose）と `codex:codex-rescue` の 2 本を並行で回した。2 回目なので差分を見た。`codex:codex-rescue` の出力は「Codex が使えず、自分とサブエージェント 1 本の結果をまとめた」と書いている（2 章の末尾）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

設計書 v2 の 2 回目のレビュー（差分のレビュー）です。ファイルは何も編集していません。

作業ツリーでは、レビューの最中にも実装が進んでいました。`IosNextFrameQueue.cs`・`IosNextFrameQueuePump.cs`・`IosNotificationPendingCalls.cs` が新しくでき、`IosNotificationActionResult.cs` も変わっています。いずれも対象外なので読んでいません。行番号は HEAD のものです。

Swift の型の扱い（R-4）は手元の macOS で `swiftc` を使い、実際に動かして確かめました。`JSONSerialization` のエスケープ（D-9）も同じく動かして確かめ、D-9 の規則と `/` 以外は一致しました。

## (1) 1 回目の A1 と B は直ったか

| 1 回目の指摘 | v2 | 判定 |
|---|---|---|
| サブ R-1（`UNITY_IOS` だけの列挙） | D-1、V-1 | 直っている。新しい 2 ファイルは iOS だけの型を使わない。テストの asmdef（`Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`）にも制約は無い |
| サブ R-2（サンプルの拒否の判定） | D-17、V-5 | 直っている。サンプルは Runtime と同じ assembly |
| Codex R-1（`Update` が終わらない） | D-3 | 直っている。ただし Clipboard の D-12 にある「順を保つ規則」が抜けた（R-3） |
| Codex R-2（破棄で表を空にする） | D-2、3 章 | 直っている |
| Codex R-3（いない間のイベントが後から届く） | D-12 | 直っている。`Registry.swift:319-327` と `NotificationReceiver.swift:440-450` のとおり、抱えておくのは最初の登録の前の分だけ |
| Codex R-4（U+0000） | D-16 | 直っている。ただし 3 章の 1.x の欄に誤りがある（R-6） |
| Codex R-5（設定の画面） | D-12 | 直っているが、「1.x と同じ」と言えない場面がある（R-1） |
| B: Awake の install（サブ R-7、Codex R-8） | D-13 | 直っている。テスト・サンプル・ハーネスに `Initialize()` を呼ぶ箇所は無い |
| B: 空の音・`fileURL`・setter（Codex R-9、サブ R-6） | D-5 (a)(c) | 直っている |
| B: 全か無か・型の橋渡し（Codex R-10、サブ R-10） | D-4 | 全か無かは直っている。型の橋渡しの規則は 1.x と違う（R-4） |
| B: release と所有（Codex R-11） | D-2 | 直っている |
| B: 平らな読み手（サブ R-8） | D-12 | 方針は直っているが、飛ばし方が決まっていない（R-2） |

## (2) v2 で持ち込まれた誤りと抜け

### R-1 [B] settings request を抱えたまま、最初の Manager を作った時点で設定の画面が開く（「1.x と同じ」が成り立たない）
- **根拠**:
  - v2 の D-12（55 行）は「settings request の受け手も 1 つ登録し、届いたら `open_settings_async` で開く（1.x と同じ）」としている。
  - native では、受け手が無い間の settings request を最大 32 件抱えておき、最初の登録に渡す（`NotificationReceiver.swift:447-450`、`Registry.swift:319-327, 386-399`）。
  - 2.0.0 の delegate は、起動時に `NtkNotificationInstaller.mm` が入れる（F-12）。
  - 1.x の delegate は、Manager の `Initialize()` が呼ぶ `notificationSetup()` で初めて入った（`3cae23e^` の `IosNotificationManager.cs` の Initialize、`1.13.0` の `IosNotificationManager.swift:134-138`）。だから Manager より前の settings request は、1.x には届かなかった。
- **壊れ方**: 起こるのは、ホストが native で `providesAppNotificationSettings` を求めたアプリだけ（`Notification.h:303-304`）。ユーザーが Settings の「通知の設定」の導線からアプリを開くと、その要求が抱えられる。後でゲームのどこかが初めて `Instance` に触れた時点で、突然 Settings に飛ばされる。複数抱えていれば、その数だけ開く。3 章にも確かめにも無い。
- **直し案**: D-12 で、最初の登録で受け取った分（抱えられていた分）は開かずに捨てる（ログに残す）と決める。開くのは、登録の後に届いたものだけにする。あるいは、遅れて開くことを 3 章の既知の差分に書く。Manager がいないときの扱い（interaction と同じく捨てるのか）も 1 行書く。

### R-2 [B] 「文字列でない値は飛ばして続ける」の飛ばし方が決まっておらず、素直に書くと入れ子のキーが上の段に漏れる
- **根拠**:
  - 今の平らな読み手は、文字列でない値で `break` する（HEAD の `IosNotificationActionResult.cs:55`）。値の後は `,` か `}` まで読み進めるだけ（同 60 行）。
  - 2.0.0 の userInfo の JSON には入れ子のオブジェクトと配列がありうる（`NotificationJson.swift:99-112`）。キーは並べ替えられる（同 123 行）。
- **壊れ方**: 55 行の `break` を 60 行の読み進めに替えるだけの実装だと、次のようになる。
  - `{"a":{"x":"1","y":"2"},"b":"c"}` は `{y=2, b=c}` になる。入れ子の `y` が上の段に出る。
  - `{"a":["p","q"],"z":"1"}` は `{q=1}` になる。キーと値の組がずれる。
  - 1.x は止まるだけで、誤ったキーは出さなかった。NU-4 も NU-4n も入れ子を使わないので捕まらない。
- **直し案**: D-12 に「飛ばすのは値 1 つ。文字列の中を見ながら `{}`・`[]` の深さを数えて飛ばす」と書く。4 章の EditMode に、入れ子のオブジェクト（キーが 2 つ以上）、文字列を含む配列、`,` や `}` を含む文字列の値を足す。

### R-3 [B] D-3 に、Clipboard の D-12 の「待ち行列に何かあればネイティブの結果もその後ろに並べる」が無く、呼んだ順と結果の順が入れ替わる
- **根拠**:
  - v2 の D-3（46 行）は「Clipboard の D-12 と同じ考え」と書く。しかし、ネイティブの完了はディスパッチャーに直接積み、呼び出しの中で決まった結果だけを `IosNextFrameQueue` に積む。
  - Clipboard では、待ちがある間はネイティブの結果も一覧の後ろに並べている（`IosClipboardManager.cs:266-270, 762-767`、Clipboard 設計 v4 の D-12 の「CP-31」）。
  - 1.x の JSON の読み違いは、呼び出しの中で同期で返っていた（`1.13.0` の `UnityIosNotificationManager.swift:48-52`）。ディスパッチャーに積まれるのはネイティブの完了より必ず先だった。
- **壊れ方**: 同じフレームで `ShowNotification(壊れた JSON, cb1)` を呼び、次に `ShowNotification(正しい JSON, cb2)` を呼ぶ。2 回目の完了がフレームの間に届くと、N+1 で cb2 とイベントが先に出る。cb1 は早くても N+1 で、その後ろに出る。`NotificationOperationCompleted` の流れが呼んだ順でなくなる（1.x では常に cb1 が先）。3 章に無く、NP-3・NP-18 は両方ともネイティブの結果なので捕まらない。
- **直し案**: D-3 に Clipboard と同じ規則を足し、3 章に「同じ」と書く。あるいは入れ替わりを既知の差分として 3 章に書く。Editor の PlayMode の確かめに、拒まれる呼び出しの後に受け付けられる呼び出しを続けて、順を照らすものを足す。

### R-4 [B] D-4 の型の橋渡し「0 以外が true」が 1.x の Swift と違う
- **根拠**:
  - v2 の D-4（47 行）は「bool の項目は数も受け（0 以外が true）、小数…は無視」としている。
  - 1.x は `as? Bool`・`as? Int` を使っていた（`1.13.0` の `UnityIosNotificationJsonParser.swift:111, 120, 130-131`）。
  - macOS で `JSONSerialization` の結果を実際に動かして確かめた結果は次のとおり。
    - `2 as? Bool` は nil、`0.5 as? Bool` は nil。`1`・`1.0`・`true` は true。
    - `2.0 as? Int` は 2、`1e2 as? Int` は 100、`1.5 as? Int` は nil、`true as? Int` は 1。
    - `true as? Double` は 1.0。
- **壊れ方**:
  - `{"type":"timeInterval","interval":3600,"repeats":2}` は、1.x では 1 回だけの通知（nil で false）。v2 では 1 時間ごとに繰り返す通知になる。`interval` が 60 未満なら、1.x の成功が D-5 (b) の失敗に変わる。
  - `notifyOnExit: 2` も同じく false から true に変わる。
  - 「小数」を JSON の字面で判定すると、`"badge":2.0` が 1.x の 2 から「無視」に変わる。
  - どれも 3 章と確かめに無い。
- **直し案**: D-4 を次のように直し、EditMode の型の橋渡しの照らしに入れる。
  - bool の項目は 0・1（`1.0` を含む）と true・false だけを受け、ほかは無視（既定）。
  - 整数の項目は、値がちょうど整数の数（`2.0`、`1e2`）と bool を受ける。
  - 小数の項目は bool も受ける。

### R-5 [B] 呼び出しのスレッドの約束が無い。新しい待ち行列はフレームの番号と静的な一覧に頼っている
- **根拠**:
  - 1.x の C# には、主スレッドの確かめが無い。ディスパッチャーへの `Enqueue` は lock 付き（`UnityMainThreadDispatcher.cs:43-49`）。読み違いの結果は呼んだスレッドから積まれていた。
  - v2 の D-3 の待ち行列は「積んだフレーム」を記録する。D-2 の表は静的。どちらもスレッドに触れていない。
  - Clipboard は主スレッドを確かめている（`IosClipboardManager.cs:518, 660`）。
- **壊れ方**: 利用者がワーカースレッドから呼ぶと（`await` の後など）、入口の拒み・読み違い・使えないときの結果が、主スレッドでない所から待ち行列と表に触れる。`Time.frameCount` をワーカーから読めるかは確かめていない。読めなければ公開のメソッドが例外を投げる。読めても `List` の競合になる。1.x ではどのスレッドからでも動いた。
- **直し案**: D-2・D-3 に、主スレッドの外の呼び出しをどうするかを決める。案は「表と待ち行列は lock。フレームの番号は主スレッドで数え直す（例: 主スレッド以外から積んだものは、次のポンプの回で番号を付ける）」。あるいは「主スレッドだけ。ほかは失敗」にして、既知の差分として 3 章に書く。

### R-6 [C] D-16 と 3 章の U+0000 の行
- 3 章 90 行の「1.x: 全文が届いた」は、JSON の中の識別子にしか当てはまらない。引数の文字列（Schedule・Update の `identifier`、Cancel・Remove・RemoveCategory の引数）は、1.x でも `const char*` で渡されて切れていた（`1.13.0` の `UnityIosNotificationManagerBridge.h:71, 77`）。行を「JSON の中」と「引数」に分ける。
- D-16 は「引数の identifier」を拒むとしているが、D-10 の操作（取り消し・削除）の引数をどうするかが書かれていない（結果を返す口が無い）。「呼ばずに LogWarning」か、1.x と同じく切るかを書く。
- `fileURL` と音の名前を「表示の文字列」に入れて「切って渡す」と、別のファイル・別の音を指しうる。識別子と同じ扱い（拒む）のほうが D-16 の理由に合う。
- D-5 (a) で userInfo を C# が書き直す以上、「エスケープのまま届く」には、書き手が制御文字を `\u00xx` にする必要がある。D-9 と同じ規則を使うと一言書く。

### R-7 [C] 待ち行列の後片付けが無い
- Clipboard では次の 2 つが要った（`IosClipboardManager.cs:256-259, 431-455, 1856-1870`）。
  - リセット: `SubsystemRegistration` で一覧を空にし、番号を進める。Reload Domain が無効のとき、前の Play の結果が次に出ないようにするため。
  - ディスパッチャーが壊れたとき: 一覧の結果を「dropped」で捨てる。
- v2 の D-15 は「時点は D-3 の待ち行列」とするので、Editor（iOS が対象）の「使えない」の結果もこの待ち行列を通る。D-3 か 4 章に、リセットとディスパッチャーが無いときの扱いを足す。

### R-8 [C] interaction のイベントを出す時点が v2 で消えた
- v1 の D-12 にあった「イベントは次の `Update` に出し」が、v2 の D-12 に無い。ハンドルを値に写して解放した後、ディスパッチャーで出すのか、受け口の中で出すのかを書く。「Manager がいない」を、受け口の時点と出す時点のどちらで見るのかも書く。

### R-9 [C] 既知の差分なのに 3 章に無いもの
- D-4 の「int32 に入らない整数は無視（既知の差分）」と、型の橋渡しの差（R-4 を直した後に残るもの）。
- 呼び出しの中で決まった結果の時点。1.x の読み違いは、ディスパッチャーの中から呼べば同じ `Update` で出た。v2 では必ず後のフレームになる。
- NP-16 の `empty-action-id` は 3 章で「同じ」としているが、1.x では action が残り、v2 では捨てる。結果は成功で同じでも、登録される category の中身が違う。

### R-10 [C] 判断の表で約束したことが 4・5 章に入っていない、ほか
- 1 回目の判断の表の「NP-6 の主張の文も直す」が 4 章に無い（今の文は `"2.0.0 rejects an unknown trigger"`、`IosNotificationPlayerTests.cs:278`）。期待は「`PERMISSION_DENIED` の文」にすると、D-4 の確かめとして効く。
- 判断の表の「混ざった userInfo の期待を NU-4 に足す」が、4 章の UI のテストの行（105 行）に無い。今の NU-4n は `{"n":1}` だけ（`NotificationScenario.cs:58`）で、1.x でも v2 でも `<null>` になるので、3 章の NU-4 の行の差は観測されない。
- V-1 の `verify_unity_android.sh` には EditMode だけを走らせる手段が無い。Editor の回の後、そのまま端末の回に進む（同 380-415 行）。Android の端末が要るのか、`-activeBuildProfile` を Android にした EditMode だけの起動にするのかを書く。

件数: A1 0 件、A2 0 件、B 5 件、C 5 件
## 2. Codex（`codex:codex-rescue`）

差分レビューの結果、**A1 が 2 件、B が 2 件残ります**。ファイルは編集していません。作成途中の `IosNotificationCApi.cs` は参照していません。

以下、`Runtime/…` はパッケージ配下、`ios/…` は `/Users/jonghyunkim/Desktop/native-toolkit` 配下です。

Round 1 の第 2 章にある A1／B の対応状況は次のとおりです。

| Round 1 | 判定 | v2 の根拠・残件 |
|---|---|---|
| R-1：同期拒否の再入 | 解消 | D-3（46 行）でフレーム境界を保証し、103 行に再入試験を追加。Clipboard の `DeliverLater`（1836 行）とも整合する |
| R-2：破棄後の未完了コールバック | 方針は解消 | D-2（45 行）で表を保持。回帰検証は R2-4 |
| R-3：不在中イベントの再生 | 方針は解消 | D-12（55 行）で受け手を常駐させ、不在中は破棄。回帰検証は R2-4 |
| R-4：U+0000 による切断 | 対応を明示 | D-16（59 行）で拒否／切断を決定。ただし、新たに加えた `fileURL` の扱いに R2-1 |
| R-5：settings request | 方針は解消 | D-12（55 行）で購読と設定画面遷移を追加。回帰検証は R2-4 |
| R-8：Awake の自動 install | 方針は解消 | D-13（56 行）、変更一覧（100 行）で公開 `Initialize()` と分離。回帰検証は R2-4 |
| R-9：空の sound 名 | 方針は解消 | D-5（48 行）で既定音へ変換し、setter の失敗も処理。1.x との OS 上の同等性は未実測と明記されている |
| R-10：配列全体の型不一致 | 解消 | D-4（47 行）とテスト一覧（103 行）に全か無かの規則を追加。ただし、除外対象の拡張に R2-2 |
| R-11：所有権・例外境界 | 一部未解消 | `release=NULL`、表の除去責任、固定コールバックは反映。残件は R2-3 |

**R2-1 — 区分：A1：`fileURL` の切断が、別ファイルの添付・移動を許す**

- **根拠：** [設計 v2:59](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/notification/designs/2026-11-15-ios-notification-cabi-design-v2.md:59) は、`fileURL` を表示文字列として扱い、U+0000 で切れても渡すとしています。しかし、`Runtime/iOS/Common/IosNativeToolkitCApi.cs:64–69` は内部ゼロを保持し、native の `ios/IosLibraryCApi/IosLibraryCApi/Common/CApiString.swift:62–67` は C 文字列の接頭部だけを読みます。`NotificationContentBuilder.swift:236–258` はその接頭部を添付 URL として受理します。[Notification.h:346](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Notification.h:346) は、通知追加時に元ファイルが移動されると明記しています。
- **失敗シナリオ：** 権限があり、実在する PNG を接頭部に持つ `fileURL:"file:///…/a.png\u0000suffix"` を渡すと、`a.png` が添付され、移動されます。URL 全体とは異なるファイルを操作するため、表示文字列の短縮と同じ扱いにはできません。
- **計画した検証で捕まらない理由：** 103 行の D-16 テストが設計どおり「切れる」を期待すれば通ります。接頭部の実ファイルが添付・移動されないことを確認する試験はありません。
- **修正：** `fileURL` は識別子と同様に U+0000 を拒否し、native setter を呼ばずに失敗させてください。接頭部のファイルが残ることも確認します。

**R2-2 — 区分：A1：空の添付識別子まで除外し、添付と添付エラーを黙って消す**

- **根拠：** [設計 v2:47](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/notification/designs/2026-11-15-ios-notification-cabi-design-v2.md:47) は、空の識別子を持つ添付も捨てる規則になっています。Round 1 の判断表:213 が決めたのは action・text input action の除外です。`1.13.0:ios/UnityIosPlugin/UnityIosPlugin/Notification/UnityIosNotificationJsonParser.swift:66–70` は空の添付識別子を保持し、同タグの `NotificationRepositoryImpl.swift:215,244–248` もそのまま渡します。現在の `NotificationContentBuilder.swift:236–239` も空の添付識別子を拒みません。
- **失敗シナリオ：** 権限がある状態で、空の識別子と正常な画像 URL を渡すと、添付が消えた通知として成功します。存在しないファイルなら、native の `ios/IosLibrary/IosLibrary/Notification/Data/Repository/NotificationRepositoryImpl.swift:397–403` が返すべき添付失敗まで消えて成功します。
- **計画した検証で捕まらない理由：** D-4 の単体テストは、誤った除外規則を期待値にできてしまいます。既存 NP-8（`IosNotificationPlayerTests.cs:320–335`）にも、この入力はありません。
- **修正：** 空の識別子による除外を action・text input action に限定してください。添付の識別子は、文字列として存在すれば空でも保持します。

**R2-3 — 区分：B：Round 1 R-11 のハンドル所有権・例外境界が未反映**

- **根拠：** [設計 v2:45](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/notification/designs/2026-11-15-ios-notification-cabi-design-v2.md:45) は呼び出し表の責任を決めましたが、97–116 行には、正常受信時のハンドルのコピー・`finally` 解放・逆コールバックからの例外漏出防止・解放回数の検証がありません。`Notification.h:36` は例外漏出を禁止し、138–143 行は list・interaction・settings request を受け取り側所有、352 行は読み取りポインターの寿命をハンドル解放までとしています。
- **失敗シナリオ：** 一覧の変換中に例外が出る、settings request の処理が失敗する、受け手が null である、といった経路で、解放漏れや例外の native 境界への漏出を通常の成功試験では検出できません。D-3 の利用者コールバック用 `try` だけでは、変換処理の境界を覆えません。
- **修正：** 各受信ハンドルを managed 値へコピーし、`finally` で一度だけ解放する責任を明記してください。正常・破棄・変換例外・受け手 null の各経路で、解放回数と例外隔離を検証します。

**R2-4 — 区分：B：修正したライフサイクル・登録経路の回帰検証が追加されていない**

- **根拠：** [設計 v2:103](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/notification/designs/2026-11-15-ios-notification-cabi-design-v2.md:103) と 112–116 行で追加された中心的な動作試験は、待ち行列の再入とサンプルの拒否表示です。既存 Player の後処理（`IosNotificationPlayerTests.cs:80–92`）は Manager を破棄せず、NU-8（`NotificationUITests.swift:317–329`）はプロセス再起動、NU-9（同:334–340）は公開 API の直接呼び出しです。これらでは Round 1 R-2・R-3・R-5 の修正を検証できません。R-8 の既存プローブ（`IosNotificationDelegateProbeTests.cs:35–42`）も、明示 install 後の再登録までは確認しません。
- **失敗シナリオ：** 破棄後のコールバック消失、不在中イベントの再生、settings request の無反応、初回登録拒否後の再登録不能が残っても、現在の NP／NU の期待値更新だけでは通過します。
- **修正：** 未完了要求を残した破棄、同一プロセス内の不在・再生成、settings request の受信、起動時 install 無効から明示 `Initialize()` への遷移を検証対象へ追加してください。native 呼び出し・受信を差し替える試験で、配送回数・登録回数・不在中の破棄を照合できます。

文書の機械照合は FAIL 0 件でした。複数項目は SKIP であり、上記の動作・所有権を保証する結果ではありません。`codex:codex-rescue` は利用できなかったため、私とサブエージェント 1 本の結果を統合しました。

件数：A1 2件、A2 0件、B 2件、C 0件、合計4件。
## 3. 判断

**2 章の R2-1・R2-2 が A1 なので止めない。** v3 で A1 と B を直し、3 回目（決まりの上限）は差分のレビューにする。

| 指摘 | 判断 | v3 での直し |
|---|---|---|
| 2 章 R2-1（`fileURL` の U+0000 で別のファイルが添付・移動される） | 直す（A1） | `fileURL` と音の名前は識別子と同じく拒む（D-16） |
| 2 章 R2-2（空の識別子の添付を捨てる） | 直す（A1） | 空の識別子で捨てるのは action・text input action だけ。添付は `fileURL` が空のときだけ捨てる（D-4。実装は既にこの形） |
| 2 章 R2-3（ハンドルの所有と例外の境） | 直す（B） | D-2 に「受け口はハンドルを値に写し、`finally` で 1 回だけ解放、例外は受け口の中で止める」を書き、受け口の芯（番号の取り出しと結果の組み立て）を Editor でも組まれる関数にして Editor の PlayMode で照らす |
| 2 章 R2-4（直したライフサイクルの確かめが無い） | 直す（B） | Editor の PlayMode: 待っている呼び出しを残して Manager を壊しても受け手が呼ばれる、Manager がいないときのイベントは出ない。Player: 待っている一覧の呼び出しの後に Manager を壊しても受け手が呼ばれる。settings request は D-12 の直しで受けなくなる |
| 1 章 R-1（抱えられた settings request で、後から設定の画面が開く） | 直す（B） | settings request の受け手は登録しない。設定の画面を自動で開くのは 1.x だけ（**既知の差分**）。ホストが native で求めたアプリだけに関わる |
| 1 章 R-2（飛ばし方） | 直す（B） | D-12 に「値 1 つを、文字列を見ながら `{}`・`[]` の深さを数えて飛ばす」。EditMode に入れ子・配列・`,` や `}` を含む文字列の値 |
| 1 章 R-3（呼んだ順と結果の順） | 直す（B） | 待ち行列に何かあれば、ネイティブの結果もその後ろに並べる（Clipboard と同じ）。Editor の PlayMode で順を照らす |
| 1 章 R-4（型の橋渡し） | 直す（B） | bool の項目は 0・1（`1.0` を含む）と true・false だけ、整数の項目はちょうど整数の数（`2.0`、`1e2`）と bool、小数の項目は bool も受ける |
| 1 章 R-5（スレッド） | 直す（B） | 表と待ち行列は lock。主スレッドの外から積んだものは、次のポンプの回でフレームの番号を付ける。ポンプは Manager の Awake で作る |
| 1 章 R-6（U+0000 の行） | 直す | 引数の文字列（Schedule・Update の `identifier`、取り消し・削除の引数）は 1.x と同じく切れる（1.x も C の文字列で渡した）。拒むのは JSON の中の識別子・`fileURL`・音の名前。3 章の行を分ける。userInfo は D-9 の規則で書く |
| 1 章 R-7（待ち行列の後片付け） | 直す | リセットで空にする（実装済み）。ディスパッチャーが壊れたら、新しいディスパッチャーのポンプが続きを渡す |
| 1 章 R-8（イベントの時点） | 直す | D-12: 受け口でハンドルを値に写して解放し、次の `Update` に出す。Manager がいるかは受け口と出す時の両方で見る |
| 1 章 R-9・R-10 | 直す | 3 章に行を足す。NP-6 の主張の文、NU-4 の混ざった userInfo（ハーネスの NU-4n）、V-1 の Android の Profile の EditMode の起動のしかた |
