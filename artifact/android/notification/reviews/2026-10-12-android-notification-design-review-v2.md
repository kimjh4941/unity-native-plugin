# レビュー結果

- 日付: 2026-10-12
- 対象ファイル: artifact/android/notification/designs/2026-10-12-android-notification-design-v2.md
- 機能名: notification
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 2。前のラウンドとは別のインスタンス）と Codex を並行で回した。どちらも v1 との差分を中心に、読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない）
- A1: サブエージェント 1 件（R2-1）、Codex 0 件

---

## 1. サブエージェント（Claude）

私が加えた変更はありません。ファイルはすべて読んだだけです。`check_design_consistency.py` は読むだけの検査なので v2 に対して流し、全項目 OK でした。ただし、この検査は J-N / E- / K- の ID や、章をまたいだ中身の整合までは見ていません。

### 1. 判定表（レビュー v1 の指摘）

| ID | 判定 | 根拠（v2 の行） |
|---|---|---|
| R1-1 groupName の無い groupId | 解決 | 182、J-N5 278。1.x の `OL/NotificationRepositoryImpl.kt:100` と一致 |
| R1-2 引けないリソース | 解決 | J-N6 279。利用者の判断と、申し送りとの違いを明記 |
| R1-3 通知オフ | 解決 | 239、280、302 |
| R1-4 予約の tag | 解決 | 210。1.x の `NotificationSchedulerSupport.kt:39`・`NotificationUseCases.kt:40-43`、NB:159-161,207-211（変換は `decodeOrNull` だけ）と一致 |
| R1-5 priority / progress | 解決 | 157、164。`BLD:142`（-2〜2）、`BLD:180`（不定なら検査なし）と一致 |
| R1-6 構造体名 | 解決 | 132 `ActionEntry` |
| R1-7 古いコンストラクタ | 解決 | 260 |
| R1-8 終了した状態のタップ | **部分** | 手順は書かれた（413）。ただし実現できるかが未検証で、K にも無い（R2-2） |
| R1-9 権限の取り消しでプロセスが止まる | 解決 | 428-429、K-2。`part2.md:40` で確認 |
| R1-10 J-N8 の継ぎ目 | 解決。ただし新しい欠陥 | 継ぎ目の署名に穴がある（R2-1） |
| R1-11 リスナーの印を 2 つに | 解決 | 285、404 |
| R1-12 チャンネルのハンドルの解放 | **部分** | E-17（387）は直った。5.4 の手順 5（342）に外側の `finally` で `channel_free` が残っている（R2-5） |
| R1-13 1.x の記録の条件 | **未解決** | 5.5 の手順 3（359）に、LegacyScheduleCleaner との同居も、開いた設定の画面の記録も無い（R2-11） |
| R1-14 未知の kind | 解決 | 221、E-16 |
| R1-15 setter の数 | 解決 | 26。header の `content_set_` 34 個から 5 と 3 を引いた数 |
| R1-16 JSON の読み方の差分 | **部分** | 4.2 と 5.3 は直った。E-2（372）に「重複」が残り、5.3 に重複した行がある（R2-4、R2-7） |
| R1-17 5.3 に足す行 | 解決 | 303、316-320、325 |
| R1-18 文言 | 解決 | 242-243 |
| R1-19 ログ | 解決 | 248 |
| R1-20 Android 12 | 解決 | 418 |
| X1-1 id の MIN | 解決 | 152 |
| X1-2 isGroupConversation | 解決 | 190。OP:183-187 と一致 |
| X1-3 importance | 解決 | 176、309。OB:793-804、OL:600-608 と一致 |
| X1-4 Manager を壊す前後の届け先 | 解決 | 321、406 |
| X1-5 往復のテスト | 解決 | 395 |
| X1-6 6・7・9 の文言 | 解決 | 237。NB:392-402 で権限の要求だけの値と確認 |
| X1-7 解放の検証 | 解決 | E-17。content とチャンネルはコードレビューに割り当てた |
| X1-8 片方だけ失敗 | 解決 | 404 |
| X1-9 保持の範囲 | 解決 | 305。保持は NB:52,436-453 と Kotlin の `NotificationEvents.kt:58-60` で、どちらも 32 件 |
| 不足: XML コメント | 解決 | 259 |
| 不足: J-N6 と申し送りの違い | 解決 | 279 |
| 不足: customViewLayout の空白、isGroupConversation の null | **部分** | isGroupConversation は解決。customViewLayout は未解決（R2-8） |

### 2. 新しい指摘

**R2-1｜A1｜low〜medium｜`ScheduleWithFallback` が最初の inexact の値を持たない**
- 該当: 5.1 の Manager の行（259）`ScheduleWithFallback(Func<int, int> schedule)`（引数は inexact）、J-N8（281）、7.1（393）、E-7（377）
- 根拠:
  - 4.2（197）は `Inexact = !exact` と決めている。
  - NB:195-198 は、未来の正確な予約のときにだけ `EXACT_ALARM_NOT_ALLOWED` を返す。
  - 署名どおりに「`schedule(0)` → 11 なら `schedule(1)`」と書き、ラムダで `options.Inexact = inexact` とすると、`exact=false` の予約が最初の呼び出しで正確に変わる。正確なアラームが許可された端末では、そのまま正確な予約になる。
  - これは層 1 のテスト（呼び直しの回数、options の写し）も層 2b も通ってしまう。
- 提案:
  - 署名を `ScheduleWithFallback(int inexact, Func<int,int> schedule)` にする。最初の呼び出しは `schedule(inexact)`、呼び直すのは `inexact == 0 && code == 11` のときだけにする。
  - テストに「inexact=1 のときは 1 回だけ、1 で呼ぶ」を足す。
  - 置き場所も 1 つに決める。5.1 は Manager の層 2a の継ぎ目、7.1 は `AndroidNotificationCApiTests`（層 1）、E-7 は「1 / 2a」で、3 か所がそろっていない。

**R2-2｜B｜medium｜終了した状態のタップの「別の起動」が、書かれた形では回せない見込み（要検証）**
- 該当: 7.3（413）、K の一覧（446-448）
- 根拠:
  - タップで起動するときの Intent は `getLaunchIntentForPackage` に `NEW_TASK | RESET_TASK_IF_NEEDED` を足しただけで、extra が無い（`NotificationEventReceivers.kt:44-46`）。Intent で「流すテスト」を指定できない。
  - device テストは Editor 主導で回る。ビルド・インストール・起動を Editor が行い、終わりにアンインストールする（`verify_unity_android.sh:41-44,279`）。通知のタップで起動した Player は、どの実行にもつながっていない。
  - 「アプリを止める」を `am force-stop` で行うと、そのパッケージの通知と Alarm も消える見込み（OS が ACTION_PACKAGE_RESTARTED で消す。要検証）。
- 提案:
  - K に「要検証」として足す。
  - 仕組みを決める。例: 止め方（`am force-stop` は使わない。HOME の後の `am kill` か `run-as kill`）、選び方（補助が `run-as` で書く印のファイル）、結果の返し方（補助のファイルの経路）。
  - 決められなければ、理由を付けて手動か層 3 に移す。

**R2-3｜B｜low｜E-5 の層 2b の入力「priority 3」は、もう失敗しない**
- 該当: E-5（375）と、priority を丸める 157・J-N5 278
- 内容: priority を -2〜2 に丸めるので、`INVALID_PARAMETER` が起きない。テストは落ちるので実装で気づける。
- 提案: `groupAlertBehavior` 3 か `semanticAction` 13 に替える（`BLD:144,400`）。

**R2-4｜C｜low｜E-2 が「重複したキー → the JSON could not be read.」のまま**
- 該当: 372 と 202（後の値が勝つ）
- 根拠: libcore の `JSONObject.put` は後の値で上書きする。手本の `WindowsNotificationJsonReader.cs:213-221` も後勝ち。
- 提案: E-2 から「重複」を消す。残すと、実装者がどちらを採るかで振る舞いが分かれる。

**R2-5｜C（B 寄り）｜low｜チャンネルの解放の場所が 2 つある**
- 該当: 5.4 の手順 5（342）の `finally: content_free、channel_free` と、E-17（387）の「`BuildContent` の中で `set_channel` の直後に解放」
- 根拠: 両方に従うと 2 回解放になる。`BuildContent(spec, allocated)` の署名からは、チャンネルのハンドルが外に出ない。`set_channel` は中身を写すので（`BLD:162-169`）、中で解放してよい。
- 提案: 5.4 の手順 5 を E-17 に合わせる。

**R2-6｜C｜low｜5.3 の「要確認」の付け方**
- 298 は「要確認の行は 5.5 の手順 3（1.x の実測）で直す」としている。しかし:
  - 手順 3（359）が挙げるのは 5 行のうち 2 つだけ（タップ、別のスレッド）。316 と 325 が漏れている。
  - 317 の要確認は 2.0.0 側の主張なので、手順 3 では確かめられない（手順 7 の担当）。
  - 326 と 328 は 1.x 側で測れるのに「推測」のまま、どの手順でも測る予定が無い。
  - 309「-1000 は Android が拒んだ」には出典も「要確認」も無い（要検証）。
- 提案: 手順 3 は「5.3 の 1.x 側の要確認・推測のすべての行」を測るとし、2.0.0 側は手順 7 に振る。

**R2-7｜C｜low｜5.3 と 4.2 の記述の重なり・ずれ**
- 5.3 の 312 と 314 が重複している（314 は v1 の残り）。
- 148 の一般規則「型が違う値は無い」と、190 の isGroupConversation（真偽でない値は 0）がずれている。190 は例外だと明記する。
- color の受ける範囲: 162 は「int を uint に写す」、312 は「uint の範囲まで」。「int.MinValue〜uint.MaxValue」と 1 つの書き方にする。fixture に負の値があるので（`AndroidNotificationJsonBuilderTests.cs:36`、サンプル:864）、往復のテストで気づける。
- 242 の `STORAGE_FAILED` は「問い合わせで来る」と書いているが、問い合わせは文言を出さず `false` を返す（J-N11）。

**R2-8｜C｜low｜customViewLayout / bigCustomViewLayout の空白・null が未規定（v1 の不足項目が残っている）**
- 該当: 191
- 根拠:
  - 1.x は `optString(...).takeIf{isNotEmpty()}` で判定した（OP:195-196）。空白や JSON の null（`"null"` になる）も名前として引き、引けずに Default になるか、大きいレイアウトを省いた（OB:717-729）。
  - C# がこれをそのまま渡すと、J-N6 により `RESOURCE_NOT_FOUND` で失敗する。
  - Builder は空白を書かないので、手で書いた JSON だけに起こる。
- 提案: 「空白・null は無い」と書き、7.1 に足す。

**R2-9｜C｜low｜範囲の外の値の列挙に、メッセージの時刻が抜けている**
- `add_message` は `timestamp_ms < 0` を拒む（`BLD:290-292`）。J-N6（279）と 5.3（308）の列挙に足すか、丸めを決める。

**R2-10｜B｜low｜8.2 の前提の抜け（要検証）**
1. 毎回の実行の後に Player をアンインストールする（`verify_unity_android.sh:279`）。そのため、次の実行はいつも新しいインストールから始まる。
   - POST_NOTIFICATIONS は、許可も質問もされていない状態になる。
   - Android 14 以降の新しいインストールでは、SCHEDULE_EXACT_ALARM が既定で不許可（manifest:7。USE_EXACT_ALARM ではない）。
   - 「テストの前に pm grant」をインストールと起動の間に差し込める場所が無い。テストから補助に頼む形（grant はプロセスを止めない）を書く。
2. 7.3 の「予約の発火」は、正確なアラームの許可が前提。不許可だと J-N8 で不正確に下がり、発火の時刻が定まらない。
3. full screen: USE_FULL_SCREEN_INTENT（manifest:11）は Android 14 以降で不許可になりうる。画面が点いている間は heads-up になるので、7.3 の期待値が決まらない。

**R2-11｜C｜low｜9 章の残件の記録（workflow 手順 8 の条件 3）**
- R1-13 が直っておらず、K にも無い。
- J-N6 の約束（移行ガイドとマニュアルに `tools:keep` を書く）と J-N19 の移行ガイドに、引き受け手が無い（K-4 にあるのはマニュアルだけ）。
- 提案: K の行として足す。

**R2-12｜C｜low｜起動の Intent の書き方**
- 57 と 304 の「action も extra も付かない」は不正確。実際は ACTION_MAIN と CATEGORY_LAUNCHER が付く。正しくは「actionId の action と extra が付かない」。

### 3. 総合評価

- **A1 は 1 件**（R2-1。low〜medium）。B は 3 件（R2-2 が medium、R2-3 と R2-10 が low）、C は 8 件。
- v1 の A1（R1-1〜R1-5、X1-1〜X1-4）はすべて解決しました。一次資料と照らして、新しい規則は次のとおり正しいと確かめました。
  - `id` の MIN、`isGroupConversation`、groupName、tag
  - priority と progress の範囲（`BLD:142,180`）
  - color と lightColor の uint（`BLD:149,474`）
  - 空の data の値が通ること（`RequiredText` は NULL だけを拒む。`BLD:47-49,357`）
  - 重複は後勝ち（libcore と Windows のリーダー）
  - importance の説明
  - AP-16（`part2.md:40`）
- 新しく生まれた欠陥は、ほとんどが直し漏れで、新しい規則と古い記述が同じ文書に並んでいる形です（E-2、E-5、5.4 の手順 5、5.3 の重複した行）。
- 実質的な穴は 2 つです。
  - J-N8 の継ぎ目の署名（R2-1）
  - 終了した状態のタップを、Editor 主導の device テストで回せるか（R2-2）
- 判定: R2-1 を直せば A1 は 0 になります。B と C は、9 章に理由を付けて残すことで止める条件に届きます。R2-1 は 1 行の署名の修正なので、直した上でもう 1 回通さずに止めるかは、取りまとめ役が決めてください。

---

## 2. Codex

レビュー完了です。ファイルは変更していません。J-N6／J-N7 の決定自体は再検討せず、いずれも「利用者の判断」「1.x／申し送りとの差分」として適切に記録されていることだけ確認しました。

### 前回指摘の解消状況

#### X1

| ID | 判定 | 根拠・残件 |
|---|---|---|
| X1-1 | 解消 | `int.MinValue` を欠落扱いとし、専用テストも追加（§4.2:152、§7.1:395） |
| X1-2 | 解消 | missing=-1、null=false、bool=値を規定（§4.2:190、§7.1:395） |
| X1-3 | 解消 | importance 5／-1000 の1.x挙動と移行後差分を整理（§4.2:176、§5.3:309） |
| X1-4 | 解消 | Manager 再生成時を含む配送先を差分表・テストへ追加（§5.3:321、§7.2:406） |
| X1-5 | 解消 | round-trip 比較から `launchAction` と resource type を除外（§7.1:395） |
| X1-6 | 一部解消 | 6／7／9 の文言は規定済み。ただし「想定外の値」の具体的な文字列表現は未定義のまま（§4.5:237、§7.1:393） |
| X1-7 | 一部解消 | interaction／shown は `ReadAndFree` で検証可能。content／channel はコードレビューのみで、解放位置にも記述矛盾が残る（§5.4:342、§6:387） |
| X1-8 | 解消 | 種類別フラグと片側失敗時の再試行テストを追加（§5.2:285、§7.2:404） |
| X1-9 | 解消 | 32件保持を「最初の登録前」に限定（§5.3:305） |

#### R1

R1-1〜R1-11、R1-14〜R1-16、R1-18〜R1-20 は解消。

- R1-12 は一部解消。§5.4:342 は外側の `finally` で `channel_free` するとしています。§6:387 は `set_channel` 直後に `BuildContent` 内で解放するとしており、両者が不一致です。`set_channel` はコピーします（`native-toolkit/.../Notification/Builders.cpp:157-169`）。
- R1-13 は未解消。§5.5:359 に次の2点の記録がありません。
  - 1.x の記録時も2.0.0 AAR と LegacyScheduleCleaner が同居する条件。
  - `OpenNotificationSettings` が実際に開いた画面。
- R1-17 は一部解消。追加された「設定画面の結果時点」が一次資料と一致しません（X2-1）。

### v2 の新規欠陥

#### X2-1｜C｜設定画面の完了は「画面から戻った後」ではない

- 設計: §5.3、317行
- 問題: 2.0.0 の結果を「画面から戻った後になる見込み」としています。実装は `startActivity` が成功した直後に結果を通知し、画面から戻るのを待ちません。
- 根拠:
  - `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/java/com/jonghyunkim/nativetoolkit/capi/jni/NotificationBridge.kt:336-340`
  - `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/notification/data/repository/AndroidNotificationSettingsGateway.kt:43-48,57-61`
- 修正: 「Android main 上で `startActivity` の成功／fallback 判定直後に完了する。設定画面からの復帰は待たない」と直し、§7.3 の期待時点も合わせる。

#### X2-2｜C｜重複キーの扱いが章間で矛盾する

- 設計: §4.2、202行／§6 E-2、372行
- 問題: §4.2 は重複キーを last-wins で受理します。E-2 は重複を構文エラーとして `the JSON could not be read.` に分類しています。
- 根拠:
  - 1.x は `JSONObject(json)` を使用: `native-toolkit@1.12.0:android/unity_android_plugin/src/main/java/android/unity/notification/UnityNotificationJsonParser.kt:11-13,16-21`
  - Android libcore は後の `put` で既存値を上書き: `/Users/jonghyunkim/Library/Android/sdk/sources/android-31/org/json/JSONTokener.java:394`、`JSONObject.java:210-217`
- 修正: E-2 から「重複」を外す。§7.1 の重複キーテストは「後の値が勝つ」と明記する。

#### X2-3｜B｜E-5 の `priority 3` では INVALID_PARAMETER に到達しない

- 設計: §4.2、157行／§6 E-5、375行
- 問題: mapping が priority を -2〜2 に丸めるため、`priority: 3` は2として渡されます。ネイティブの範囲エラーを検証できません。
- 根拠: `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Notification/Builders.cpp:142`
- 修正: E-5 の実機 fixture を、丸めない `groupAlertBehavior: 3` などへ変更する。`priority: 3` は丸め成功のテストに移す。

#### X2-4｜B｜uint 色の追加範囲を検証できない

- 設計: §5.3、312行／§7.1、395行
- 問題: 手書き JSON では `color`／`lightColor` に `uint` 全域を受ける契約ですが、専用の境界テストがありません。既存 Payload は `int?` なので、Builder round-trip だけでは `2147483648..4294967295` を生成できません。
- 根拠:
  - Payload は `int?`: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Notification/AndroidNotificationPayloads.cs:45,112`
  - C ABI は `uint32_t`: `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Notification/Builders.cpp:149-154,474-479`
- 修正: 両フィールドについて `-1`、`2147483648`、`4294967295` と範囲外値のマッピングテストを明記する。

#### X2-5｜C｜5.3 に同じ JSON 型差分が二重掲載されている

- 設計: §5.3、312行／314行
- 問題: 312行の詳細な「JSON の型の違い・数の扱い」と、314行の旧「JSON の型の違い」が重複しています。
- 根拠: 1.x の変換入口は `native-toolkit@1.12.0:android/unity_android_plugin/src/main/java/android/unity/notification/UnityNotificationJsonParser.kt:275-292`。
- 修正: 314行を削除し、312行を唯一の差分記述にする。

機械照合 `scripts/check_design_consistency.py` は FAIL なしでした。

A1件数: 0

補足（取りまとめ役の転記）: Codex の出力は、Codex を呼んだ転送役のエージェントがそのまま返したもの。転送役は一次資料を確かめ直していない。
