# レビュー結果

- 日付: 2026-10-12
- 対象ファイル: artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v14.md と artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v3.md（合わせて見た）
- 機能名: notification
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 3）だけ。読むだけ。1 章はサブエージェントの出力。文言には手を加えていない。見出しの段を 1 つ下げ、先頭の題（「レビュー結果（ラウンド 3。補助 v14 と計画 v3）」）だけを外した
- Codex: 並行で回したが、利用上限のエラー（"You've hit your usage limit"）で途中で終わり、判定も指摘も出なかった。利用者の指示（2026-10-12）で、このラウンドはサブエージェントだけで判定する
- A1: 0 件。止める基準（A1=0）に達したので、レビューを止める。残りの指摘（A2 1 件、B 2 件、C 7 件）は、補助 v15 と計画 v4 で直す
- 判定の補足（レビューの後に確かめた事実）: R3-3 の「`TimeoutAttribute` をクラスに付けられるか」は、同梱の NUnit のメタデータで `AttributeTargets` が 69（Assembly、Class、Method）だった。クラスに付けたときに Unity の runner が各テストに引き継ぐかは確かめていない

---

## 1. サブエージェント（Claude）


ファイルは変更していません。

照らし合わせたもの:
- `scripts/android_ui_driver.py`、`scripts/verify_unity_android.sh`
- `AndroidUiDriver.cs`、`AndroidSharePlayerTests.cs:71-144`
- Test Framework の `CompositeWorkItem.cs`、`RuntimeTestRunnerFilter.cs`、`UnityWorkItem.cs`、`EnumerableTestMethodCommand.cs`、`UnityTestExecutionContext.cs`
- 生成された manifest（`Library/Bee/artifacts/Android/Manifest/unityLibrary/src/main/AndroidManifest.xml`）
- native-toolkit の `AndroidNotificationSettingsGateway.kt`、`NotificationBridge.kt:319-344`、`ProgressForegroundNotifications.kt`、`NotificationEventReceivers.kt`

### 1. 前のラウンドの指摘の判定

| ID | 判定 | 根拠 |
|---|---|---|
| R2-1 | 解決 | 計画 46-49 で 3 つの形に分けた（「在る」「消える」は 5 秒の読み直し、「出ない」はアンカー）。順の前提は補助 V-29（435）で、崩れたときの代わり（2 秒の読み直し）もある。1 章で弱いことは明記されている。アンカーの ID の残りは R3-5 |
| R2-2 | 解決 | 補助 382、計画 69 |
| R2-3 | 部分 | 2 回読みと action は入った（補助 309）。ただし action は「前面の記録」から読む。転送の後では、アプリが送った Intent を表さない見込み（要検証）。AN-40 の代わりの手も無い（R3-1） |
| R2-4 | 解決 | 計画 134-141 は Share の段（`PendingCount`、シーンを下ろす、`PendingCallCountForTests`、起点）を写した |
| R2-5 | 解決 | 補助 293、320-323、371-372、397、399 |
| R2-6 | 解決 | 補助 313。60+10+10+15+10+25+30+1+10＝171 で、算術は合う。テストの側の待ちの効き方は R3-3 |
| R2-7 | 解決 | 補助 324、369、380 |
| R2-8 | 解決 | 補助 259（`ok` の値と `$`）、305（`not-stopped`）、306（`ambiguous`、全体から探す）、312 |
| R2-9 | 解決 | 計画 73、補助 488、V-30（436） |
| R2-10 | 解決 | 計画 94 |
| R2-11 | 解決 | 計画 81 |
| R2-12 | 解決 | 計画 104-105 |
| R2-13 | 部分 | CLEAR_TOP を足した（補助 309、311、V-25 430）。ただし起動の Activity が singleTask なので、`front` と同じ働きになり、次の手にならない見込み（R3-2） |
| R2-14 | 解決 | 計画 144、補助 390 |
| R2-15 | 解決 | 補助 431、491 |
| R2-16 | 解決 | 補助 427。Unity 6 での効き目は V-22 のまま |
| X2-1 | 解決 | 計画 139。期待の数がそろった時刻、最後の答え、`OnApplicationPause(false)` の遅いものを起点にした |
| X2-2 | 解決 | 計画 20、144、補助 390 |
| X1-4 | 部分 | 補助 323 と V-17（434）。時間切れの後に端末の `uiautomator` が走り続ける経路は、受け入れた危険として明記したまま |
| X1-7 | 解決 | 計画 38、41、補助 491。`CompositeWorkItem.cs:231-245` は、`Order` を持つ子を先頭に入れる。`:268-271` で、その範囲だけを番号で並べ替える。1 つの fixture の中では、1 章が先に流れる |
| X1-11 | 部分 | 計画 112-117。見分けられるかは V-25 しだい。見分けられなかったときの AN-40 の手が無い（R3-1） |
| R1-9 | 解決（中身は manifest による） | singleTask の起動で、タスクの上に積まれた画面は `front` で消える見込み（要検証）。文書はこの理由を書いていない（R3-2） |
| R1-17 | 解決 | 補助 306、312 |
| R1-18 | 解決 | 補助 372 |
| R1-7 | 部分 | 計画 73、補助 488。保持の確かめは、今も 3 秒の待ちだけに頼る。V-30 で 1 度測る形 |
| R1-10 | 解決 | 計画 134-141 |
| R1-11 | 解決 | 補助 313、397。テストの側の待ちの効き方は R3-3 |

### 2. 新しい指摘

#### R3-1｜B｜severity 中（要検証）
- **場所**: 補助 3.5 の `close-screen`（309）、V-25（430）。計画 AN-40〜42（112-114）、117
- **問題**: action を「前面の Activity の記録」から読む。
  - Android 14 以降の Settings が SPA の Activity に転送すると、前面の記録の Intent は Settings が作り直したもので、action が無い（`-`）見込み（要検証）。
  - そうなると、AN-40（通知の設定）と、アプリの詳細の画面に落ちたときの組が同じになりうる。
  - 計画 117 の代わりの手は、AN-41・AN-42 の「違う」の確かめを外すことだけ。AN-40 の「詳細に落ちたら失敗」は、手段が無いまま残る。X1-11 の見逃しが、実装の中で静かに戻りうる。
- **根拠**:
  - 補助 309 の「その記録の Intent の action」。
  - R2-3 の提案は「アプリの直後に積まれた記録」だった。
  - C ABI は `OPENED` と `OPENED_FALLBACK` を区別して返す（`NotificationBridge.kt:336-340`、設計 v4 の 56 行、235 行）。
- **提案**: どちらかを選ぶ。
  - (a) アプリが送った Intent を、logcat の `ActivityTaskManager: START u0 {act=… cmp=…} from uid <アプリ>` から読む。転送されても残る。
  - (b) C ABI の版では、AN-40・AN-42 で `OnSettingsCore` に届く result が `OPENED`（`OPENED_FALLBACK` でない）であることを、層 2a の継ぎ目か内部の記録で照らす。
  - さらに計画 117 に「AN-40 の組が AN-41 と同じなら、AN-40 は (b) で照らす。それも無ければ手動」を足す。

#### R3-2｜C｜severity 低
- **場所**: 補助 309、311、V-25（430）。計画 117
- **問題**: 「Back と `front` で消えなければ CLEAR_TOP」は、次の手にならない見込み。
  - テストの Player の起動の Activity は `android:launchMode="singleTask"`（生成された manifest の 18 行）。
  - singleTask の既存のインスタンスを `am start -n` で起こすと、AOSP の ActivityStarter は CLEAR_TOP と同じく上の画面を消す（推測。要検証）。
  - そのため CLEAR_TOP は `front` と同じ働きで、`front` で消えない画面には効かない。害は無い。
  - V-25 の「Unity の Activity が 2 つ目に作られないか」も、この manifest で答えが出ている。
  - 1.x は設定の画面を `NEW_TASK` で開く（`AndroidNotificationSettingsGateway.kt:18-19,58`）。画面は Settings の別のタスクに積まれ、`front` で隠れるが消えない。
- **提案**: V-25 と 3.5 に singleTask を書く。CLEAR_TOP を「`front` と同じ」と明記する（または外す）。消えない場合の次の手は手動だけ、とそろえる。

#### R3-3｜B｜severity 中
- **場所**: 補助 3.5 の 313（「テストの側の待ちは 200 秒」）、5 章 354・360・361。計画 98
- **問題**: Test Framework は、`[UnityTest]` を既定で 180 秒で打ち切る。
  - 根拠: `UnityWorkItem.cs:18` の `k_DefaultTimeout = 1000 * 180`。数え始めはテストの開始（`UnityWorkItem.cs:94` の `StartTicks`）。判定は `EnumerableTestMethodCommand.cs:91-95` と `UnityTestExecutionContext.cs:160-164`。
  - 200 秒の `Wait` は、どのテストでも届かない。テストの頭の `EnsureGranted`（補助の往復 2 回）なども、この 180 秒に入る。
  - 最悪の 171 秒になったとき、補助の `fail <理由>` より先に、Test Framework の時間切れのエラーになる。
  - 偽の成功にはならない。後片付けの `WaitForPending` は打ち切りの対象外なので、補助の答えも回収できる。ただし、R2-6 の直しの狙い（200 秒＞171 秒）は成り立っていない。
  - Share の 180 秒も同じ。
- **提案**: 画面の命令を使うテストに `[Timeout(...)]` を付け、計画と補助 5 章に書く（例: 待ちの和と前後の処理より長く）。前例は `WindowsNotificationSamplePlayerTests.cs:221` の `[UnityTest, Timeout(...)]`。クラスに付けられるかは、同梱の NUnit の `TimeoutAttribute` の `AttributeUsage` を、Order と同じくメタデータで確かめる（要検証）。確かめるまでは、メソッドに付ける。

#### R3-4｜C｜severity 低
- **場所**: 計画 6 章 138（「1 章の順の付いたテストでは呼ばない（何も出していない…）」）、AN-05（63）
- **問題**: 理由の書き方が事実と合わない。
  - AN-05 は 10 分後の予約を残す。権限も AN-04 で与えた後で、権限が無いのは AN-02〜AN-03 だけ。
  - ふつうは、次の順の無いテストの後片付けの `CancelAllScheduledNotifications()` が消す（そのテストの 3 回の中で数える）。
  - 1 章だけを流したときは、アンインストールまで予約が残る。
- **提案**: AN-05 の中で `CancelScheduledNotification` を呼ぶ（期待を 2 / 0 / 0 に）。または、段 4 を `[Order(5)]` 以降に当てる。

#### R3-5｜C｜severity 低
- **場所**: 計画 0 章 49（アンカー）、AN-19・AN-20（88-89）
- **問題**: アンカーの ID（と tag）を対象と別にすることが書かれていない。同じ ID だとアンカーが対象を置き換え、「対象の ID が無い」が成り立たない（落ちるので気づける）。
- **提案**: 「アンカーは対象と違う ID と固有の題名」と書く。

#### R3-6｜C｜severity 低
- **場所**: 補助 3.5 の `notification-tap` の `front`（305）、`notification-action`（306）
- **問題**:
  - 成功の経路の「来なければ、シェードを閉じて前に戻してから `ok not-front`」で、前に戻せなかったときの答えが決まっていない。`ok not-front` を書くと、Player は STOPPED のまま。Unity は止まり、実行の時間切れまで進まない（気づけるが原因が分かりにくい）。
  - この戻しが 60 秒の中か、別の 25 秒かも書かれていない。
- **提案**: 戻せなければ `fail not-in-front`（Sharesheet の決まりと同じ）。時間の上限のどの項に入るかを 313 に書く。

#### R3-7｜A2｜severity 低
- **場所**: 計画 AN-25（94）
- **問題**: Payload の出どころが無い。
  - `AndroidNotificationJsonBuilderTests` は Editor だけのアセンブリにあり（`Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef` の `includePlatforms: Editor`）、Payload はメソッドの中に直接書いてある。
  - サンプルの JSON を組むメソッドは private（例: `AndroidNotificationManagerExampleController.cs:247` の `BuildImmediateNotificationJson`）。
  - PlayMode のアセンブリは `NativeToolkit.Runtime` しか参照しない。
- **提案**: 写すのか、共有の internal クラスに移すのかを計画に書く。写すなら、元とずれないことの確かめ方も書く。

#### R3-8｜C｜severity 低
- **場所**: 補助 11 章 493、計画 6 章 144（「補助がいなければ、この fixture は `Assert.Ignore`」）
- **問題**: AN-02〜AN-02c と AN-05 は補助を使わない。補助がいないと AN-03・AN-04 だけが Ignore になり、権限が無いまま走る AN-05 は失敗になる。Editor から流したときだけの話で、ゲートには影響しない。
- **提案**: 記述を「補助を使うテストは Ignore」に直す。

#### R3-9｜C（A2 寄り）｜severity 低
- **場所**: 計画 1 章 54（前提）、補助 7 章 381・390、6 章 369
- **問題**: 今のスクリプトには、v14 が前提にする変更が 3 つとも無い。
  - 実機の実行の前のアンインストールが無い。今は最後だけ（`verify_unity_android.sh:279`）。
  - Test Profile の実行で `AndroidSampleScreen` を外して判定する形が無い（`:274` は `0`）。
  - 補助に `--package` を渡していない（`:196`）。

  どちらの文書も、いつスクリプトに足すかを書いていない。1 章は 1.x の段（手順 3）から要る。アンインストールが無いと、Test Profile の実行は上書きで入るので、`[Order(1)]` の「not a fresh install」で落ちる（気づける）。
- **提案**: 「1 章を足す変更（手順 3）と同じ変更で、スクリプトに 3 つを足す」と書く。`--package` を必須にするのも、同じ変更にする。

#### R3-10｜C（情報）
- **場所**: 計画 6 章 138（消す呼び出しを回収の後に移した）
- **影響**: 失敗の経路で、`WaitForPending` が依頼 1 つにつき最大 200 秒待つ。その間に予約（AN-37 の 5 秒など）が発火し、余分な `Recv` が失敗の文言に足されうる。雑音だけで、失敗を隠しはしない。成功の経路では影響しない（照合の起点は消す結果の後になる）。
- **提案**: 不要。気になるなら、段 3 の前に `CancelAllScheduledNotifications()` だけを呼ぶ。

#### 確かめて問題が無かった点
- **1 章が本当に先に流れるか**: `[Order]` を持つ子は先頭に入り、番号の順に並ぶ（`CompositeWorkItem.cs:231-245,268-271`）。`randomOrderSeed` と `orderedTestListFile` は、スクリプトが渡さない。3〜5 章の `EnsureGranted` はテストの本体で呼ぶので、1 章より前には走らない。ほかの fixture や Runtime から、通知の権限や Manager に触れる経路も見つからなかった。
- **アンカーの順**: C ABI の失敗の呼び出しは同期で返る（`NotificationBridge` の `guarded` は戻り値で返す）。退行して `notify` していても、それはアンカーの enqueue より前になる。順の前提は V-29 で確かめる。
- **後片付けの段**: Share（`AndroidSharePlayerTests.cs:74-140`）の段と、内容がそろっている。照合の起点も、補助を使わないテストと順の付いたテストで成り立つ。
- **後片付けの Progress の stop**: `startService` なので、前面なら落ちない（`ProgressForegroundNotifications.kt:47-50`）。
- **171 秒の内訳**: 算術は正しい。3.2・3.3・3.5・5 章・計画 98 の「200 秒」もそろっている（効き方は R3-3）。

### 3. 総合評価

- **A1 は 0 件。** 止める基準（A1=0）に達した。
  - 前のラウンドの A1（R2-1、X2-1）は、どちらも解決した。
  - X1-7 は、1 つの fixture と メソッドの `[Order]` の形で、Test Framework の実装で裏が取れた。
- **そのほかの件数**: A2 は 1 件（R3-7）。B は 2 件（R3-1、R3-3）。C は 7 件（R3-2、R3-4、R3-5、R3-6、R3-8、R3-9、R3-10）。
- **実装の前に直したい点**:
  - **R3-1**: V-25 の結果しだいで、AN-40 の「詳細の画面に落ちたら失敗」が静かに効かなくなる経路がある。前提は要検証なので B とした。(b) の `OPENED_FALLBACK` で照らす形を足せば閉じられる。
  - **R3-3**: Test Framework の既定の打ち切り（180 秒）は、補助の 200 秒の前提と食い違う。偽の成功にはならないが、R2-6 の直しの狙いが成り立たない。
- **部分のまま残るもの**: R2-3 / X1-11、R2-13、X1-4、R1-7。どれも要検証（V-17、V-25、V-30）か、受け入れた危険として明記されている。
---

## 2. Codex

利用上限で途中で終わり、出力なし（上の記載）。
