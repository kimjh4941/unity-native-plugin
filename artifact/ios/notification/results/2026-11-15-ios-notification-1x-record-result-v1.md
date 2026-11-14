# iOS Notification の 1.x の記録（自動テスト計画 v4 の 5 章）v1

- 記録日: 2026-11-15
- 計画: [../designs/2026-11-14-ios-notification-ui-test-plan-v4.md](../designs/2026-11-14-ios-notification-ui-test-plan-v4.md)（5 章「1.x の記録」をこのファイルに書く。計画の版は上書きしない）
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3-R
- 対象: 1.x（`Plugins/iOS/VERSION.txt` の dist 1.13.0、`ios-native-toolkit-1.3.0` と `unity-ios-native-toolkit-1.3.0`）
- 実行: `scripts/verify_unity_ios.sh --skip-editmode`（iOS の Player だけ、iPhone 16 / iOS 18.3 のシミュレーター、Xcode 26.3）。テストは `Tests/PlayMode/IosNotificationPlayerTests.cs`（14 件。NP-9 は 1.x を落とすので `NTK_IOS_CABI_2` の定義があるときだけ組む）
- UI のテスト（NU-1〜16 の 22 件）は `scripts/test_ios_ui.sh`（同じシミュレーター）。テストは `Tools~/IosUiTests/NtkUiTests/NotificationUITests.swift`、ハーネスは `Assets/NativeToolkitIosUiHarness/Runtime/Scenarios/NotificationScenario.cs`。実機はまだ（ランナーの「入れ直さない」実行が先に要る。設計は [artifact/topics/ios-c-abi-2/designs/2026-11-15-ios-ui-keep-installed-design-v4.md](../../../topics/ios-c-abi-2/designs/2026-11-15-ios-ui-keep-installed-design-v4.md)）

## 1. Player のテストの記録（2026-11-15）

Player のアプリは許可を一度も求めないので、権限は `NotDetermined` のまま。1 回目は値を記録する形で流し、2 回目は実測を照らす形にして流した。どちらも IosPlayer の 59 件（Notification 14、Clipboard 35、Share 6、Dialog 4）がすべて成功した。

| ID | 1.x の実測 | 計画の予想との関係 |
|---|---|---|
| NP-1 | `NotDetermined` | 予想どおり |
| NP-2 | `false` | 予想どおり |
| NP-3 | 10 回とも A は 0 回、B は 1 回 | 予想どおり（A = 1、B = 1 の形は出なかった） |
| NP-4 | Schedule は成功、予約の一覧に載らない | 予想どおり（M2） |
| NP-5 | Show は成功、配信済みに**載る** | 予想どおり（M2: 未決の即時の分は載る） |
| NP-5b | 60 秒の trigger の Update は成功、どちらの一覧にも載らない | 予想どおり |
| NP-6 | 未知の trigger の Show は成功（即時に落ちる） | 予想どおり |
| NP-7 | 未知の trigger の Schedule は失敗、`Failed to parse notification content or trigger JSON` | 予想どおり（文言を記録） |
| NP-8 | 13 の値のうち、**`https://` の添付だけ失敗**（`The operation couldn’t be completed. (IosLibrary.NotificationError error 1.)`）。空の id、`badge = -2`、`relevanceScore = 1.5`、項目の無い calendar、空の識別子の location、オブジェクトでない userInfo、予約のキー、未知の `interruptionLevel` は、どれも成功 | 予想どおり（M4・M5） |
| NP-9b | NaN、+∞、-∞ は、C# の Builder が JSON として解けない値を書くので、Schedule は `Failed to parse notification content or trigger JSON` で失敗 | 予想どおり |
| NP-13 | **`SetBadgeCount(-1)` は 1.x でも失敗**（`The operation couldn’t be completed. (IosLibrary.NotificationError error 2.)`） | **予想と違った**（「成功の見込み」）。`2.0.0` の `INVALID_PARAMETER` は既知の差分ではなく、文言だけの差になる |
| NP-14 | `SetBadgeCount(0)` は成功 | 記録 |
| NP-15 | 成功。受け手は入口から戻った後、1 フレーム後 | 予想どおり |
| NP-16 | `title` の無い action、空の action の id、未知の option、どれも成功 | 予想どおり |
| NP-18 | 10 回とも A は 0 回、B は 1 回、`NotificationOperationCompleted` は 2 回 | 予想どおり |

単独で呼んだ、操作のイベントを持つ操作（Show、Schedule、Update、SetBadgeCount、RegisterCategory）では、受け手のあとにイベントがちょうど 1 回ずつ、同じ結果で来た（続けて 2 回呼んだ NP-3・18 は表のとおり）。1.x の失敗の文言は、`NotificationError` に説明が無いので `localizedDescription` の汎用の文（`… error <番号>.`）になる。**この文は端末の言語で訳される**（実機の日本語では英語と違う文。NP-8・13 は実機で落ちた）ので、テストは `IosLibrary.NotificationError` とその番号で照らす。

## 2. UI のテストの記録（2026-11-15）

ハーネスはテストごとに入れ直す（許可が未決から始まる）。バナーと通知センターのセルは、題（`NTK UI Harness`）とテストごとの本文で 1 件に絞ってから押した。1 回目は 22 件のうち 19 件が成功し、NU-10・11・12 が落ちた（3 章）。直して流し直し、最後に 22 件を通して流して、すべて成功した（両方のランナーの直しの後。Player の 59 件も成功）。

| ID | 1.x の実測（結果の行） | 計画の予想との関係 |
|---|---|---|
| NU-1 | `[permission@ok] granted`、`[status@ok] Authorized`、`[has@ok] True` | 予想どおり |
| NU-2 | `[permission@fail] error=The operation couldn’t be completed. (IosLibrary.NotificationError error 4.)`、`Denied`、`False` | 予想どおり（汎用の文。番号は 4） |
| NU-2b | 拒否の後の Show・Schedule・Update はどれも成功、予約と配信済みの一覧はどちらも空 | 予想どおり（M2） |
| NU-3 | 2 回目の要求でダイアログは出ず、`granted` が 2 本目 | 予想どおり |
| NU-3b | 2 回目の要求でダイアログは出ず、1 回目と同じ失敗の行 | 予想どおり |
| NU-4 | `[tap@event] id=ntkh-4 action=com.apple.UNNotificationDefaultActionIdentifier userInfo=jp=日本,k=v,q=x\"y,slash=a\/b`。`{"n":1}` は `userInfo=<null>` | 予想どおり（`slash` は `2.0.0` で `a/b` になる既知の差分の候補） |
| NU-5 | 長押しして `Open`（foreground）で `[tap@event] id=ntkh-5 action=open userInfo=<null>` が 1 本、ハーネスが前に来る | 予想どおり（userInfo が空の辞書は `<null>`） |
| NU-6 | `Reply` に `ntk reply` と打って送り、3 秒後に前に戻すと `[reply@event] id=ntkh-6 action=reply text=ntk reply` が 1 本、`tap` は 0 本 | 予想どおり |
| NU-7 | 前面のまま即時の通知を出すと、配信済みに `ntkh-7`。時刻の形は `9999-99-99T99:99:99Z`（小数秒なしの ISO 8601、UTC） | 予想どおり |
| NU-8 | ハーネスを終わらせて通知から起こすと、`source=file` で起動し、**タップは届かない** | 予想どおり（`2.0.0` は届く見込みで、既知の差分の候補） |
| NU-9 | 設定のアプリが前に来る | 記録 |
| NU-10 | 前面で非活性（`foregroundInactive`）のときの `RequestPermission` で、**許可のダイアログは出る。「許可しない」を押しても、結果が来ない**（15 秒待った。2 回とも同じ） | **予想と違った**（「ダイアログが出るか、失敗か」の記録だったが、結果が失われる形） |
| NU-11 | 2.3 の各段は予想どおり。段 2b（trigger が null の Update）は、予約から `b` を消してすぐ配信する。段 4（`CancelAllScheduled` の直後、同じフレームで予約）は 5 回とも予約が残った | 予想どおり（段 4 は記録） |
| NU-12 | `X` を `[A]` の後に `[B]` で登録すると、`X` は `A` のまま。**`Y` を登録した後の `X` は、3 回のうち 2 回は `A`、1 回は `B`**（揺れる）。`X` を消した後、`X` の通知に action は無く、`Y` の通知には `C` が出る | 予想どおり（置き換わらない）。揺れは、1.x の category の読み書きが待たない Task どうしで競合するため（計画の注のとおり） |
| NU-13 | 通知センターで消すと `[tap@event] id=ntkh-13 action=com.apple.UNNotificationDismissActionIdentifier userInfo=<null>` が 1 本、`reply` は 0 本 | 予想どおり |
| NU-14 | 未決のまま背景で `RequestPermission`（`state=background`）。前に戻すと許可のダイアログが出て、「許可しない」で失敗の行が 1 本 | 記録（ダイアログは前に戻った後に出る） |
| NU-14b | 許可の後と拒否の後は、背景から呼んでもダイアログは出ず、今の状態の行が 1 本 | 予想どおり |
| NU-15 | 背景からの `OpenNotificationSettings` で、設定のアプリは前に来ない | 予想どおり |
| NU-16 | 範囲外の緯度（91）、入ると出るの両方が `false`、userInfo の入れ子の `null` は、どれも落ちずに Schedule が成功 | 記録（NP-8 に移せる。移すのは段 3 で） |

## 3. UI のテストの直し（計画の版は上書きしない）

- NU-10: 結果が来ないことを照らす形にした
- NU-11: 段のあいだに行のボタンまで巻く操作と画面の読み取りで数十秒かかり、60 秒の予約が段の途中で発火した。予約を 1 時間にし、結果の行は id で絞った問い合わせで読む形にした。段 4 の繰り返しは 10 回から 5 回に減らした（1 件 600 秒の上限に収めるため）
- NU-12: 2 つの通知を同時に出すと、1 つ目のバナーを取り逃がした。1 つずつ出す形にした
- ハーネスの行が 40 を超えたので、`HarnessUITestCase.run` は押す前に行のボタンまで巻く

## 4. 計画 v4 との違い（計画の版は上書きしない）

- NP-9 は、計画の `[Explicit]` ではなく `NTK_IOS_CABI_2` の定義で組むかを分けた。`[Explicit]` のテストは結果に「流していない」として残り、`scripts/ios_player_results.py` が区分のテストの成功に数えないため
- `2.0.0` で既知の差分になる期待（NP-3・5・18 など）は、同じ `NTK_IOS_CABI_2` で分けて書いた
- `[UnityTest]` に引数付きのテストを組み合わせず、NP-8・9・9b・13・14・16 は 1 つのテストの中で値を順に流す

### 4.1 実機の Player のテスト（2026-11-15）

iPhone XS（iOS 18.7.2、言語は日本語）で `IosPlayer` の 59 件を流した。1 回目は、走っている間にハーネス（守っている木）を書き換えたため、照らしで止まった。2 回目は 57 件成功、NP-8 と NP-13 が落ちた（1.x の汎用の失敗の文が日本語に訳されるため。番号で照らす形に直した）。3 回目も同じ 2 件が落ちた（日本語の文の括弧が全角のため）。括弧は半角と全角のどちらも認める形に直し、**4 回目（04:03〜）で 59 件すべて成功した**。どの回も、利用者の指示（「実機も使っていいので、自動で進めて」）に従い、`--dry-run` の 4 点と承認 ID を会話に書いてから流した。端末の識別子、ログと結果の束は残していない。

## 5. 残っていること

- 実機の `--keep-installed` の回（NU-1・4・5・7・11）。ランナーは実装済み（`artifact/topics/ios-c-abi-2/results/2026-11-15-ios-ui-keep-installed-result-v1.md`）。利用者の準備（集中モードで通知を許すアプリをハーネスだけにする）が要る
