# レビュー結果

- 日付: 2026-10-12
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v13.md と artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v2.md（合わせて見た）
- 機能名: notification
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 2）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力。文言には手を加えていない。見出しの段を 1 つ下げ、サブエージェントの先頭の題（「レビュー結果（ラウンド 2）」）だけを外した
- A1: サブエージェント 1 件（R2-1）、Codex 1 件（X2-1）
- 2 人の判定が分かれた点: X1-7（fixture のクラスに `[Order(1)]` を付けられるか）
  - サブエージェントは「解決」、Codex は「未解消（コンパイルできない）」とした。
  - 同梱の NUnit（`Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll`）の `OrderAttribute` の `AttributeUsage` をメタデータから読んだ。`AttributeTargets` の値は 64（Method だけ）。Codex が正しい。
  - 計画 v3 では、順番に頼るテストを 1 つの fixture にまとめ、メソッドに `[Order]` を付ける。

---

## 1. サブエージェント（Claude）


- 対象
  - 補助 v13: `artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v13.md`（v12 との差分と、関わる章）
  - 計画 v2: `artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v2.md`（全文と v1 との差分）
- 照らし合わせたもの
  - `scripts/android_ui_driver.py`、`scripts/verify_unity_android.sh`
  - `AndroidUiDriver.cs`、`AndroidSharePlayerTests.cs`、Share の計画 v6 の 3 章
  - Unity Test Framework 1.6.0（`CompositeWorkItem.cs`、`RuntimeTestRunnerFilter.cs`、`TestExtensions.cs`、`SettingsBuilder.cs`）
  - native-toolkit の `NotificationShade.kt`、`UiDriver.kt`、`NotificationEventTest.cpp`、`NotificationEventReceivers.kt`、`NotificationEventIntents.kt`、`NotificationEvents.kt`、`NotificationBridge.kt`、`AndroidNotificationSettingsGateway.kt`
  - サンプルの `AndroidNotificationManagerExampleController.cs`
- ファイルは何も変えていません。

### 1. 前のレビューの指摘の判定

| ID | 判定 | 根拠・残り |
|---|---|---|
| R1-1 | 部分 | 「無い」をアプリの `getActiveNotifications()` で確かめる形にした。ただし読むのはイベントを受けた直後の 1 回だけで、時間の幅が無い。偽の「無い」が別の形で残る（R2-1） |
| R1-2 | 解決 | パッケージで判定する（3.6 の 309 行）。`collapse` は条件なしで呼ぶ（298 行） |
| R1-3 | 解決 | HELLO の 3 つの値を検査し、`shlex.quote` で引用し、`--user` を付けた。`close-screen` は 1 つの値だけを許す。残りは `--package` の出どころが書かれていないこと（R2-7） |
| R1-4 | 解決 | 計画 57 行 |
| R1-5 | 解決 | ファイルごと後で足す。ただし、既定のゲートとの時期が食い違う（R2-2） |
| R1-6 | 解決 | 保持のテストだけの実行を既定に足した（7 章 368 行） |
| R1-7 | 部分 | 通知が消えたことは、タップが処理された印にならない。保持を守っているのは、実際には 3 秒の待ちだけ（R2-9） |
| R1-8 | 解決 | HOME と `STOPPED` の確かめを前置きにした。答えは `ok front` / `ok not-front` |
| R1-9 | 部分 | 権限のダイアログには `force-stop` を足した。設定の画面がタスクに残ったときの次の手が無い（R2-13） |
| R1-10 | 部分 | 7 段にしたが、Share の 7 段と中身が違い、抜けもある（R2-4） |
| R1-11 | 部分 | 内訳は書いたが、151 秒に収まることが示されていない（R2-6） |
| R1-12 | 解決 | |
| R1-13 | 解決 | |
| R1-14 | 解決 | |
| R1-15 | 解決 | |
| R1-16 | 解決 | |
| R1-17 | 解決（小さい残り） | `front` の前置きで `STOPPED` にならないときの語が無い（R2-8） |
| R1-18〜R1-21 | 解決 | |
| 不足 1〜6 | 解決 | V-27、V-28、V-25、AN-44 を外したこと、AN-02b と AN-02c、7 章 |
| X1-1 | 解決 | R1-3 と同じ |
| X1-2 | 解決 | |
| X1-3 | 解決 | ただし「題名の行が残っていること」の条件は、前例（`NotificationShade.kt:76`）と違う（R2-8） |
| X1-4 | 部分 | ダンプの名前を毎回変える形で答えた。3.4・6 章・8 章が古いままで食い違い、終わりの削除は doctest の外にある（R2-5） |
| X1-5、X1-6 | 解決 | |
| X1-7 | 解決 | `CompositeWorkItem.cs:237-240,269-272` で、`Order` を持つ子が同じ suite の中で先に並べ替えられることを確かめた |
| X1-8 | 部分 | R1-10 と同じ |
| X1-9 | 解決 | ただし AN-25 の数え方が矛盾する（R2-10） |
| X1-10 | 解決 | |
| X1-11 | 解決（要検証あり） | Android 14 以降の Settings では、Activity の名前で見分けられないかもしれない（R2-3） |
| X1-12 | 解決 | |
| X1-13 | 部分 | R2-1 と同じ |
| X1-14 | 解決 | 代表の発火は AN-37 |

### 2. 新しい指摘

**R2-1｜A1｜severity 中｜計画 0 章 39 行（「アクティブ」の定義）、AN-02（47 行）、AN-19、AN-20（77〜78 行）。在る側は AN-10、AN-25、1.x の AN-20**

- 問題: 「在る / 無い」を、イベントを受けた直後に 1 回読むだけ。待つ時間も、アンカーも無い。
- 根拠（要検証）:
  - Notification Assistant が有効な端末では、`NotificationManagerService` が通知の post を `DELAY_FOR_ASSISTANT_TIME`（200 ms）遅らせる。Pixel は Android System Intelligence が既定の Assistant。
  - `getActiveNotifications()` が返すのは post 済みの一覧（`mNotificationList`）だけ。
  - 成功・失敗のイベントは、次のフレーム（数十 ms）で届く。
- 影響:
  - 「無い」の確かめは、後から出る通知を見逃して通る。AN-20 で「失敗を返したのに通知を出す」という退行が静かに通る。X1-13 の穴が、時間の側に残る。
  - 「在る」の確かめは、ときどき落ちる。
- 気づけない理由: 「無い」は偽の成功になる。層 2a にも doctest にも、これを見るものが無い。
- 提案:
  - 「在る」は、期限（例: 5 秒）まで読み直す。
  - 「無い」は `absent-in-sheet` と同じくアンカーで確かめる。失敗の呼び出しの後に固有の印の通知を出し、それがアクティブに現れてから、対象が無いことを見る。NMS は 1 つの handler で順に処理する見込み（要検証）。または、1 秒以上の静置と、その間の読み直し。
  - 9 章に V として足す。

**R2-2｜A2｜severity 中｜補助 7 章 368 行 と 計画 2 章 58 行**

- 問題: 既定にした保持の実行は、1.x の段（手順 3）ではテストが 0 本になる。
  - `report_tests` は `total="0"` で失敗にする（`verify_unity_android.sh:131`）。7 章の「1 本以上」のゲートでも落ちる。
  - 計画は AN-01 のファイルを手順 5 以降に足す。スクリプトにいつ足すかは、どちらの文書にも無い。
- 気づける理由: ゲートが落ちる。
- 提案: 保持の実行は、AN-01 と同じ変更（手順 5）でスクリプトに足すと 7 章に書く。

**R2-3｜B｜severity 中（要検証）｜補助 3.5 の `close-screen`（296 行）、計画 AN-40〜AN-42（101〜103 行）**

- 問題: 前面の Activity の名前で 3 つの画面を見分ける形は、Android 14 以降の Settings で崩れうる。
- 根拠（要検証）:
  - アプリの詳細の画面と正確なアラームの画面は、SPA（`com.android.settings/.spa.SpaActivity`）に転送される見込み。そうなると AN-41 と AN-42 の「名前が違う」が成り立たない。
  - 補助は「前面に出るのを待って覚える」ので、転送の途中の橋渡しの Activity を覚えて揺れることもある。
  - fallback で詳細の画面に落ちた場合（`AndroidNotificationSettingsGateway.kt:45`）は、AN-40 で分かる。
- 提案:
  - 2 回続けて同じ名前が読めてから覚える。
  - 名前で見分けられないときは、`dumpsys activity activities` で、アプリの直後に積まれた記録の Intent の action（`android.settings.APP_NOTIFICATION_SETTINGS` など）を返す。アプリが送った Intent そのものを照らせる。
  - V-25 に足す。

**R2-4｜B｜severity 中｜計画 6 章 121〜128 行**

- 問題: 「Share の 7 段と同じ形」と書いてあるが、中身が違い、抜けがある（Share の計画 v6 の 3 章、`AndroidSharePlayerTests.cs:74-140`）。
- 根拠:
  - `WaitForPending` の前に `PendingCount` を記録して 0 を見る段が無い。補助の 5 章（348 行）が求めている。
  - 5 章のテストで、サンプルのシーンを下ろす段が無い。
  - C ABI の版で `PendingCallCountForTests` を見る段が無い。
  - 起点を「最後の補助の答え」だけで決めている。補助を使わない 3 章の全部と、AN-02〜AN-02c には起点が無い。Share は「最後の結果のイベント」を起点にした。
- 提案:
  - Share の段をそのまま写す。
  - 起点は、最後の答え、最後のイベント、`OnApplicationPause(false)` のうち遅いもの。

**R2-5｜B｜severity 低〜中｜補助 3.6 の 310 行 と 3.4 の 280 行、6 章 357〜358 行、8 章 383 行・385 行**

- 問題: v13 の「ダンプの名前を毎回変え、終わりに消し損ねの印によらずすべて消す」と、古い記述が食い違う。
- 根拠:
  - 3.4 は、固定のパスと「終わりの削除は消し損ねたときだけ」のまま。
  - 6 章は `uiautomator dump /data/local/tmp/ntk-ui-dump.xml` のまま。
  - 8 章 385 行は、終わりの削除（`main` の `finally`）を doctest の外とする。一方 383 行は、「終わりにすべて消す」を doctest で見るとする。
- 影響: 古い記述どおりに書くと、V-17 の経路で残ったシェードのダンプが端末に残り、誰も気づかない。
- 提案:
  - 3.4・6 章・8 章を v13 に合わせる。
  - 終わりの削除を `Driver` のメソッドにして、doctest から呼ぶ。
  - 6 章 358 行の「シートの無いもの」を「Sharesheet と `com.android.systemui` の部品の無いもの」に直す。
  - 3.6 の 307 行の「通知の一覧が出ている木」も、パッケージでの判定に直す。

**R2-6｜A2｜severity 低｜補助 3.5 の時間の上限（300 行）**

- 問題: 「151 秒に収まる」とあるが、内訳が合っていない。
- 根拠:
  - 3.3 の 151 秒には、`collapse` と確かめ（10 秒）が入っていない。
  - 確かめの読み取りで、期限を越える削除が 1 回増える。
  - 60 秒で時間切れになった場合を足すと、151 秒を超えうる。
- 気づける理由: 8 章の doctest（「151 秒の中で諦める」）が落ちる。
- 提案: 画面の命令の和を、3.3 と同じ形で数字で書く。

**R2-7｜C｜補助 3.6 の 311 行**

- 問題: 「`--package` で渡す値」とあるが、補助には `--package` の引数が無い（`android_ui_driver.py:1413-1415`）。スクリプトも渡していない（`verify_unity_android.sh:196`）。
- 提案: 6 章に引数を、7 章に渡すことを足す。

**R2-8｜C｜補助 3.2 の 246 行、3.5 の 292〜293 行**

- `ok <値>` の例が `count` と `rotation` のまま。`front` / `not-front` と、`close-screen` の `<パッケージ>/<クラス>`（`$` を含みうる。`write_result` の `shlex.quote`〈`:428`〉で守られる）を足す。
- `front` の前置きで `STOPPED` にならないときの語が無い。
- `notification-action` は「題名の行が同じ木に残ること」を条件にした。しかし引いている前例は「広げると題名が置き換わりうるので、シェードの全体から探す」としている（`NotificationShade.kt:76`）。前例と同じ、とは書けない。

**R2-9｜B｜severity 低｜計画 AN-01（62 行）、補助 11 章 472 行**

- 問題: 「アクティブから消えた＝タップが処理された印」は正しくない。
- 根拠:
  - autoCancel で通知を消すのはシステム。
  - 受信機（`NotificationEventReceivers.kt:16-20`）は通知を消さず、受信機が走ったことの印にもならない。
  - 保持を守っているのは、実際には 3 秒の待ちだけ。
- 提案:
  - 文言を直す。
  - より確かにするなら、補助が logcat で受信機の `[onReceive]` を見てから答える（R1-7 の 2 つ目の案）。

**R2-10｜A2｜severity 低｜計画 AN-25（83 行）**

- 問題: 「各 1 / 0 / 0（消す結果は数えない）」とあるが、段 3・段 5 は購読した数をそのまま照らす。本体の中で消した結果も数に入るので、表と合わない。
- 提案: 期待を「即時の数 × 2 ＋ 予約の数 / 0 / 0」のように書く。

**R2-11｜B｜severity 低｜計画 AN-12（70 行）**

- 問題: 出していない通知を消して「アクティブに無い」を見ても、何も確かめていない。
- 提案: 先に `ShowNotification` を呼ぶ（3 / 0 / 0）。

**R2-12｜B｜severity 低｜計画 AN-32、AN-33（93〜94 行）**

- 問題: `stay` は押す前から前面なので、「launchApp=false でアプリを開かない」を確かめていない。
- 提案: v13 で `ok not-front` が返るようになった。`front` で押して `ok not-front` を期待すれば確かめられる（native-toolkit の `AnEventOnlyTapOpensNothing` に当たる）。

**R2-13｜B｜severity 低〜中｜補助 V-25（416 行）、3.5 の 296 行・298 行**

- 問題: 設定の画面がアプリのタスクに残り、Back でも `front` でも消えないときの次の手が無い。消えなければ、Unity は止まったまま実行の時間切れまで進まない。
- 提案: V-25 に「通らなければ」を書く（CLEAR_TOP 付きの起動など）。

**R2-14｜B｜severity 低｜計画 6 章 131 行**

- 問題: 「補助がいない実行では、ゲートで落ちる」とあるが、Test Android™ Profile の実行ではカテゴリの判定を当てていない（`verify_unity_android.sh:274` は `0`）。v2 で権限のテストを両方の Profile で流すので、その実行で 1 章が Ignore になっても緑になる。
- 提案: 記述を直すか、サンプル画面以外の `AndroidUiDriver` の skip を Test Profile の実行でも見る。

**R2-15｜C（情報）｜補助 V-26（417 行）**

- 一次資料で、次の裏が取れる。
  - `-testCategory` の `!` は `NotFilter` になる（`RuntimeTestRunnerFilter.cs:75-77`）。
  - カテゴリは fixture から継ぐ（`TestExtensions.cs:13-22`）。
  - `[Order]` は、同じ suite の子を先に並べ替える（`CompositeWorkItem.cs:237-240`）。
- 「効かなければ」の案は保持のテストの名前空間を動かすが、`[Order]` が効かないときに動かすのは権限の fixture の側。

**R2-16｜B｜severity 低｜補助 V-22（413 行）。v12 からある**

- 問題: 「起動時に権限を求めるなら、最初のテストで `permission-answer` を先に処理する」は成り立たない。
- 根拠: 起動時にダイアログが出ると Player が止まる。テストは HELLO も依頼も出せず、補助は依頼が無いと動かない。
- 提案: 求める場合は、テストの Player の manifest に `unityplayer.SkipPermissionsDialog` を置く。Unity 6 が起動時に求めるかは要検証。

### 3. 総合評価

- **A1 は 1 件（R2-1）。** 前のラウンドの A1（サブエージェント 3 件、Codex 7 件）は、R1-1 / X1-13 を除いてすべて直った。R1-1 / X1-13 は R2-1 として残る。
  - HELLO の検査と引用、パッケージでの判定と条件なしの `collapse`、`front` の前置きと `ok front` / `ok not-front`、`close-screen` の許す一覧、保持のテストだけの実行、`[Order(1)]` は、どれも筋が通っている。
  - `-testCategory` の否定と fixture の `[Order]` は、Test Framework の一次資料で裏が取れた。
- そのほかの件数: A2 は 3 件（R2-2、R2-6、R2-10）。B は 9 件（R2-3、R2-4、R2-5、R2-9、R2-11〜R2-14、R2-16）。どれも中心契約ではない。C は 3 件（R2-7、R2-8、R2-15）。
- **止める基準には、まだ達していない。** R2-1 は「無い」の確かめに時間の幅かアンカーを足せば直る。直した後のレビューは、その差分だけで足りる見込み。
- そのほかの直したい点:
  - B の中では、R2-3（Settings の SPA）と R2-4（後片付けが Share と違う）を直すと、実装の段の手戻りが減る。
  - R2-5 は文書の整合の修正で済む。

---

## 2. Codex

レビュー結果は、既存35件中「解消 29件、一部解消 5件、未解消 1件」です。新規欠陥は2件で、A1は1件です。ファイル・Git状態は変更していません。

パス略称は次のとおりです。
- 補助 v13: /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v13.md
- 計画 v2: /Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v2.md

### 既存指摘の判定

#### X1-1〜X1-14

| ID | 判定 | 根拠 |
|---|---|---|
| X1-1 | 解消 | HELLO の run/package/user 検証、`--package` との一致、`shlex.quote` が規定された。補助 v13:305 |
| X1-2 | 解消 | dismiss は幅1/10→9/10、100ms、行再取得、最大3回になった。補助 v13:294 |
| X1-3 | 解消 | 展開後に10秒間再読込し、`text` / `content-desc` を検索する。補助 v13:293 |
| X1-4 | 一部解消 | 固有dump名と終了時の全削除は追加された。しかし、timeout後も走る端末側 `uiautomator` の終了待ちはない。V-17の未確認事項として残る。補助 v13:310、補助 v13:420 |
| X1-5 | 解消 | retention 専用の既定実機runが追加された。補助 v13:368 |
| X1-6 | 解消 | retention run は1件以上、skip 0、全成功をゲートする。補助 v13:368 |
| X1-7 | 未解消 | クラスへの `[Order(1)]` は同梱 NUnit ではコンパイルできない。`OrderAttribute` の対象は Method のみ。runner も直接 child の `Order` property しか並べ替えない。計画 v2:30、/Users/jonghyunkim/Desktop/unity-native-plugin/Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEngine.TestRunner/NUnitExtensions/Runner/CompositeWorkItem.cs:226 |
| X1-8 | 解消 | cleanup結果待ちを含む7段teardownが追加された。計画 v2:121 |
| X1-9 | 解消 | 全行に `Op / Tap / Recv` が記載された。計画 v2:38 |
| X1-10 | 解消 | HOME→`STOPPED` 後に押し、`ok front` / `ok not-front` を返す。補助 v13:292 |
| X1-11 | 一部解消 | Activity名の返却と Settings package allowlist は入った。しかし API は app-details fallback を許すのに、AN-40/42 はそれを許容せず、3画面すべてが異なることを要求している。計画 v2:101、/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/notification/data/repository/AndroidNotificationSettingsGateway.kt:43 |
| X1-12 | 解消 | 不安定な AN-44 を外し、理由付きで層1へ移した。計画 v2:139 |
| X1-13 | 解消 | AN-20 で active notifications に存在しないことも確認する。計画 v2:78 |
| X1-14 | 解消 | 即時 Payload の active 状態、予約 Payload の scheduled 状態、代表予約の発火を検証する。計画 v2:83、計画 v2:98 |

#### R1-1〜R1-21

| ID | 判定 | 根拠 |
|---|---|---|
| R1-1 | 解消 | absence 判定を `getActiveNotifications()` の ID/tag 照合へ移した。計画 v2:39 |
| R1-2 | 解消 | SystemUI package で privacy 判定し、失敗時は無条件 collapse。補助 v13:298、補助 v13:309 |
| R1-3 | 解消 | HELLO 検証、shell 引用、`--user`、`close-screen` allowlist が追加された。補助 v13:238、補助 v13:296、補助 v13:311 |
| R1-4 | 解消 | retention fixture の SetUp から終了までManagerに触れない契約がある。計画 v2:57 |
| R1-5 | 解消 | retention test file は C ABI 実装段階で追加する。計画 v2:58 |
| R1-6 | 解消 | 専用 run とゲートが追加された。Unity CLI の `!` 除外も実装上サポートされる。/Users/jonghyunkim/Desktop/unity-native-plugin/Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEngine.TestRunner/TestRunner/RuntimeTestRunnerFilter.cs:68 |
| R1-7 | 解消 | active から消えるまで待ち、さらに3秒後に Manager を作る。計画 v2:62 |
| R1-8 | 解消 | HOME→`STOPPED` と結果値が追加された。補助 v13:292 |
| R1-9 | 一部解消 | Back前の画面確認と permissioncontroller の force-stop は追加された。しかし、Settings が Back と `front` で消えない場合の確実な除去方法はなく、V-25 の未確認事項のまま。補助 v13:296、補助 v13:416 |
| R1-10 | 解消 | 7段 teardown と照合起点が追加された。ただし新規欠陥 X2-1 あり。計画 v2:121 |
| R1-11 | 解消 | 60秒・各 cleanup 上限・151秒の内訳と doctest が追加された。補助 v13:300 |
| R1-12 | 解消 | 高速・全幅寄り・最大3回に修正された。補助 v13:294 |
| R1-13 | 解消 | Manager fixture に `AndroidUiDriver` category と権限付与前提が追加された。計画 v2:32 |
| R1-14 | 解消 | AN-25 は10分後、AN-51 は15秒の発火前に取消す。計画 v2:83、計画 v2:113 |
| R1-15 | 解消 | AN-43 の 1.x 見込みを修正し、AN-44 を除外した。計画 v2:104 |
| R1-16 | 解消 | `close-screen` が Activity 名を返す。補助 v13:296 |
| R1-17 | 一部解消 | 理由語は増えた。しかし `notification-action` の `<前面>` は依然「生」としか書かれず、不正値を `bad-arg` にする契約がない。補助 v13:293 |
| R1-18 | 一部解消 | §3.2/§3.3/§5 は画面命令を180秒対象にした。しかし §6 はなお「Sharesheet以外は10秒」として画面命令を除外していない。補助 v13:347、補助 v13:358 |
| R1-19 | 解消 | unseen watch の適用先が明記された。補助 v13:300 |
| R1-20 | 解消 | trap では補助停止後にアンインストールする。補助 v13:367 |
| R1-21 | 解消 | AN-03/04 の Profile 限定記述は消えた。ただし両 Profile を必須実行するという主張には新規欠陥 X2-2 がある。計画 v2:18 |

### 新規欠陥

#### X2-1 — A1
- 文書: 計画 v2
- 箇所: §6「後片付け」121–128行
- 問題: 余分なイベントを監視する0.5秒の起点が「最後の補助の答え」だけになっている。§3 の非UIテストや AN-51/52/54/55 には、そのテスト中の補助回答がない。そのため前のテストや `OneTimeSetUp` の古い時刻を使い、期待件数が揃った直後の0.5秒を待たずに照合できる。遅れて届く二重イベントが cleanup で捨てられ、壊れていてもテストが通りうる。
- 証拠: 計画 v2:64、計画 v2:121、計画 v2:125
- 修正: 各テストで「期待件数が揃った時刻」を起点にし、そこから必ず0.5秒待つ。UIテストでは「最後の補助回答時刻」と「期待件数が揃った時刻」の遅い方を使う。

#### X2-2 — B
- 文書: 計画 v2 と 補助 v13
- 箇所: 計画§0 18行、補助§7 368・376行
- 問題: 計画は権限テストを両 Profile で流すとしている。しかし現行スクリプトの Test Android Profile 実行は `--as-package-user` 指定時だけで、v13 が既定追加するのは Android Profile の retention run だけ。また Test Profile では `AndroidUiDriver` の件数・skip ゲートを無効にするため、任意実行しても fresh-install 権限テストが全 Ignore のまま成功しうる。
- 証拠: 計画 v2:18、補助 v13:368、補助 v13:376、/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_android.sh:71、/Users/jonghyunkim/Desktop/unity-native-plugin/scripts/verify_unity_android.sh:272
- 修正: Test Android Profile を既定 run にする。サンプルテストの意図的 Ignore だけを除外し、`AndroidNotificationFreshInstallTests` など必須 fixture は「1件以上・skip 0」を両 Profile でゲートする。

機械照合(`scripts/check_design_consistency.py`)は両文書とも FAIL なしです。ただし複数項目が SKIP 対象でした。review-document スキルが求める別レビュアーは、この環境では使えなかったため、サブレビュアー1名との突き合わせで完了しています。

**A1件数: 1**(X2-1)