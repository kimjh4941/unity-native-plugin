# iOS Notification の C ABI `2.0.0` への移行の実装の記録 v1

- 記録日: 2026-11-15
- 設計: [../designs/2026-11-15-ios-notification-cabi-design-v5.md](../designs/2026-11-15-ios-notification-cabi-design-v5.md)
  - v1〜v3 はレビューを 3 回通した。3 回目で 2 人とも A1 が 0 になって止めた。
  - v4（止めた後の直し）と v5（実装のレビューの直し）はレビューしていない（決まり）。
- 設計のレビュー: [../reviews/2026-11-15-ios-notification-cabi-design-review-v1.md](../reviews/2026-11-15-ios-notification-cabi-design-review-v1.md)、[v2](../reviews/2026-11-15-ios-notification-cabi-design-review-v2.md)、[v3](../reviews/2026-11-15-ios-notification-cabi-design-review-v3.md)
  - 2・3 回目の `codex:codex-rescue` は、出力に「Codex が使えず、補助のレビュアーと照合した」と書いている（記録の 2 章）。
- 実装のレビュー: [../reviews/2026-11-15-ios-notification-cabi-implementation-review-v1.md](../reviews/2026-11-15-ios-notification-cabi-implementation-review-v1.md)
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（最後の機能）
- コミット: `475f68f`（設計とレビューの記録）、`daa62ef`（移行）、`e3d3fbc`（実装のレビュー v1 の直し）

## 1. 作ったもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Notification/IosNotificationCApi.cs`（新） | 4 つの構造体（`Pack = 8`。96・40・40・56 バイト）、46 の関数の宣言、5 つのコールバックの型、2.0.0 のライブラリの固定の英文、状態の名前、1.x と同じキーと日付の形の一覧の JSON |
| `IosNotificationRequests.cs`（新） | 公開の JSON（content・trigger・category）を 1.13.0 のブリッジと同じ規則で読む。<ul><li>配列は全か無か</li><li>Swift の `as?` と同じ型の橋渡し</li><li>先頭の BOM、512 段</li></ul>1.x で通り 2.0.0 で拒まれる値のうち、同じ結果になるものは直して渡す。識別子・`fileURL`・音の名前の U+0000 は拒む |
| `IosNotificationUserInfo.cs`（新） | イベントの userInfo の平らな読み手。文字列でない値は飛ばして続ける |
| `IosNotificationPendingCalls.cs`（新） | 呼び出しごとの受け手の表（`user_data` の番号）。Manager の破棄でも空にしない |
| `Runtime/iOS/Common/IosNextFrameQueue.cs`・`IosNextFrameQueuePump.cs`（新、共通） | 呼び出しの中で決まった結果を後のフレームに渡す。<ul><li>ディスパッチャーの GameObject のポンプが末尾へ渡す</li><li>待ちがある間は、ネイティブの結果とイベントも後ろに並べる</li><li>どのスレッドからでも積める</li></ul> |
| `IosNotificationManager.cs` | C ABI を呼ぶ形に書き直した。<ul><li>interaction の受け手はプロセスの間 1 つ。settings request の受け手は登録しない</li><li>Awake は受け手の登録だけ。`Initialize()` が delegate を入れ直す</li><li>完了の芯（番号の取り出しと結果の組み立て）は Editor でも組む</li></ul> |
| サンプル | 拒否の判定に 2.0.0 の文を足した |
| テスト | <ul><li>EditMode: `IosNotificationCApiTests`（構造体、文言、一覧の JSON、読み方の各規則、U+0000、BOM と深さ、数の端、userInfo）。`IosCApiDeclarationTests` に +46 と 5 つの型</li><li>Editor の PlayMode: `IosNotificationManagerEditorTests`（待ち行列、完了の芯、拒みと例外、破棄の後、ディスパッチャーの消失、受け手が投げてもイベントが出ること）</li><li>Player: NP の 2c 待ちの期待をすべて書き、NP-19 を足した</li><li>UI: NU の 2c 待ちの期待をすべて書き、NU-4n を数と文字列の混ざる userInfo にした</li></ul> |
| ビルドの設定 | `NTK_IOS_CABI_2_NOTIFICATION`。`notMovedYet` は空になった |
| 文書 | `agent-rules/coding-rules/common.md`、`testing.md` |

## 2. 1.x との違い（設計 v5 の 3 章）

- 実測で同じだったもの:
  - NP-1・2・7・9b・15・16
  - NU-1・3・3b・5・6・7・9・11・13・14b・15
  - 一覧の JSON の形
- 既知の差分（実測で確かめたもの）:
  - 呼び出しごとに結果が届く（NP-3・18 は 10 回とも `A=1,B=1`）
  - 権限が無いときの Show・Schedule・Update は `Notification permission was denied.` で失敗する（NP-4・5・5b・6、NU-2b・16）
  - 失敗の文言が 2.0.0 の固定の英文になる（NU-2、NP-8・9・13）
  - 起動時のタップが届く（NU-8）
  - 背景で未決の要求は、すぐ `The app is not in the foreground.` になる（NU-14）
  - category の登録し直しは置き換わる（NU-12）
  - userInfo の `/` がそのまま届き、数の後ろの文字列も残る（NU-4）
  - 前面で非活性のときの許可の要求は、ダイアログが出て、答えた後に結果が 1 本届く（NU-10。3 章。1.x の「来ない」はテストの作りによる見え方だったとみられる）
- 既知の差分（設計で決めたもの。実測していない）:
  - settings request で設定の画面を開かない
  - Manager を作っても delegate を入れ直さない（必要なら `Initialize()`）
  - そのほか、3 章の行

## 3. NU-10 の「結果が来ない」は、テストの作りの問題だった（2026-11-15 に調べ直した）

- 実装の時点では、前面で非活性（`foregroundInactive`）のときに `RequestPermission` を呼ぶと、許可のダイアログは出るが、「許可しない」を押して 15 秒待っても結果が来なかった（1.x と同じ形）。native-toolkit への報告の候補にしていた。
- 報告の前に、シミュレーターで NU-10 を 1 件だけ流し、ライブラリの `.debug` のログ（`log stream --level debug`）を取った（3 回）:
  - native は呼び出しの約 9 秒後（「許可しない」を押したころ）に `[finishPermission]` を出し、`[complete] state: active, completed: false` → `[runCallback] depth: 0` で受け手を呼んでいた。**native-toolkit は約束どおり完了を 1 回返している**
  - 同じ時刻のアプリの非活性の理由（UIKit の `Deactivation reason`）は、コントロールセンターの分（2 と 12）が残ったままで、外れたのはテストの終わり（20:27:45）だった。テストの `openControlCenterBriefly()` は 5 秒後に下から上へなぞって閉じるが、許可のダイアログがその前（約 2.6 秒後）に出ていて、閉じる操作が効いていなかった
  - アプリが非活性の間は Unity のループが止まり、ディスパッチャーを通る結果は流れない。だから 15 秒の待ちの中では来なかった
- **直し**: `NotificationUITests.test_NU10` で、ダイアログに答えた後にコントロールセンターを閉じる（`closeControlCenter()`）。そのうえで、拒否の行がちょうど 1 本であることを照らす。シミュレーターで成功した（`NU-10 2.0.0 prompt=true results=["[permission@fail] error=Notification permission was denied."]`）
- 1.x の記録（「答えた後に結果が来ない」）も同じ作りで取ったので、同じ見え方だったとみられる（1.x はもう流せないので確かめていない）
- **native-toolkit への報告はしない**

## 4. 確かめたこと

### 4.1 シミュレーター（iPhone 16 / iOS 18.3）

| 時点 | EditMode | Editor の PlayMode | Player | UI |
|---|---|---|---|---|
| `daa62ef` | 1618 件（成功 1617、既にある skip 1） | 343 件成功 | 74 件成功 | `NotificationUITests` 22 件・`ClipboardUITests` 6 件成功 |
| `e3d3fbc` | 1625 件（成功 1624、同じ skip） | 346 件成功 | 74 件成功 | `NotificationUITests` 22 件成功 |

### 4.2 実機（iPhone XS / iOS 18.7.2）

利用者の指示に従い、`--dry-run` の 4 点と承認 ID を会話に書いてから流した。端末の識別子、ログ、結果の束は残していない。

| 時点 | Player | `--keep-installed` の UI（NU-1・4・5・7・11） |
|---|---|---|
| `daa62ef` | 74 件成功 | — |
| `e3d3fbc` | 74 件成功 | 5 件すべて成功（NU-1・4・5・7・11） |

`e3d3fbc` の実機の回では、次の 2 回は流れなかった（どちらもテストの不具合ではない）。
- 1 回目: Player のテストはビルドの後に出力が 10 分止まり、見張りが止めた。続けて流した `--keep-installed` の回も、UI テストのランナーが起動できずに止まった（`Failed (not asked for)`）。iPhone の側でアプリの起動が止まっていたとみられる。利用者が端末を確かめた後は、Player の 74 件が成功した
- 2 回目: `--keep-installed` の回は NU-1 が成功した後、準備の待ちの 30 分が切れ、残りは流れなかった
- 3 回目（19:37〜20:01）で 5 件すべて成功した

## 5. 途中で起きたこと

- テストの中に U+2028 の文字をそのまま書き、C# が行の終わりとみなして組めなかった。` ` にした。
- `9007199254740993` の判定で `>` と書き、2^53 に丸めた値を通していた。`>=` にした。
- 設計のレビューは 3 回目で止まった。止めた後の直し（v4）と、実装のレビューの直し（v5）はレビューしていない。

## 6. 残っていること

- 段 4 の候補:
  - 起動時の install を無効にした状態から、`Initialize()` で登録し直すことの確かめ → 済み（2026-11-21、`808ec8c`。[記録](2026-11-21-ios-notification-optout-check-result-v1.md)）
  - JSON の読み手を Common に移す → 済み（2026-11-21、`029ad32`。`Runtime/iOS/Common` の `IosJsonReader`）
  - サンプルの画面の手の確かめ（V-5）。あわせて、iOS のサンプルの画面の自動 UI テストを作る（Android と同じく Player のテストの中でサンプルのシーンのボタンを C# から押す。システムの画面は今のハーネスで確かめる）
    - 自動 UI テスト → 済み（`e34c6fe`〜`44349ce`。Notification のサンプルは別の回の 8 件。[手伝い役の記録](../../../topics/ios-c-abi-2/results/2026-11-15-ios-ui-driver-implementation-result-v1.md)）
    - 手の確かめ（V-5）: 実機で手で見た記録は無い（未記録）。配置（ボタンがセーフエリアとスクロールの窓の中にあるか、文字の切れ）は自動のテストで見る形にした（`01a1545`、`a75d802`）
