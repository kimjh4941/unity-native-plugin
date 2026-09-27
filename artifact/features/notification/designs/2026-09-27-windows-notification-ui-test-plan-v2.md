# Windows Notification UI 自動テスト計画 v2

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Windows Notification のサンプル（`Runtime/UI/Windows/Notification/WindowsNotificationManagerExampleController.cs`）と `WindowsNotificationManager` |
| 同梱 DLL | C ABI 2.0.0（`Plugins/Windows/VERSION.txt`: dist 1.12.0 / `windows-native-toolkit-capi-2.0.0.dll`）。移行の間は 1.x（dist 1.11.0）も追加のピンで置く（Clipboard のため） |
| 目的 | Windows C ABI 2.0.0 への移行（`artifact/topics/windows-c-abi-2/`）で、W-01〜W-10 が期待値を変えずに通ること、保留していた W-04 / W-11 が通ることを確かめ、移行で足した振る舞いを固定する |
| 実行 | `scripts/verify_unity_windows.sh --include-destructive` の Player テスト（層 2b）。**破壊的な実行のときだけ走らせる**（3 章）。作り直しの流れだけは別の Player の実行（1.2 章） |
| 設計 | `artifact/features/notification/designs/2026-09-27-windows-notification-design-v8.md`（7.3 が層 2b） |

## v1 からの主な変更

- 1 章: W-04 / W-11 の保留を解いた。2.0.0 のライブラリが `CustomActivator` を書くので、通知のボタンのクリックが動いている Player に届く
- 1.1 章を新設: Manager を直接呼ぶテスト（1.x で記録したものと、移行後だけのもの）
- 1.2 章を新設: 作り直しの流れ（別の fixture、別の Player の実行）
- 1.3 章を新設: 全キーの fixture の中身
- 2 章: 通知センターを操作する部分を `WindowsNotificationCenter`（同じファイルの internal クラス）に切り出した。SetUp の Initialize の直後に通知の設定を確かめる
- 4 章: 「2.0.0 への移行で変わる期待値」を設計 v8 に合わせて改めた。**2 回目の初期化は成功のまま、タイムスタンプは秒のまま**（Manager が 1.x の形を保つ）

## 1. 何を確かめるか

観点は v1 と同じ（サンプル計画書 `2026-06-06-windows-notification-sample-scene-design-v2.md` 6 章の観点表と、native-toolkit の N-01〜N-23）。
**W-01〜W-10 の期待値は 1.x で取ったもので、2.0.0 への移行でも変えない。** これが移行の検証になる。

| # | 操作 | 期待 | 1.x | 2.0.0 |
|---|---|---|---|---|
| W-01 | Initialize | `✓ Initialize` | 合格 | 合格 |
| W-02 | GetSetting | `✓ NotificationSetting: Enabled` | 合格 | 合格 |
| W-03 | ShowNotification | `✓`。通知センターの「Unity NativeToolkit」にタイトル `Energy Refilled` と本文 | 合格 | 合格 |
| W-04 | W-03 の通知を展開して Open を押す | `ResultTextBlock` が `NotificationInvoked: ` で始まり、`action` と `open` を含む。別の Player は起動しない | 保留（v1 の 2 章） | **合格** |
| W-05 | ScheduleNotification | 約 1 分後に `Guild Battle Starts Soon` が出る | 合格 | 合格 |
| W-06 | ScheduleNotification → すぐ CancelScheduled | 1 分を過ぎても出ない | 合格 | 合格 |
| W-07 | ShowProgressNotification → UpdateProgress | 進捗バーが 30 → 50 | 合格 | 合格 |
| W-08 | 進捗の通知が無いまま UpdateProgress | `✗ UpdateProgress` と「Progress notification not found」 | 合格 | 合格 |
| W-09 | ShowNotification → RemoveByTag | 通知センターから消える | 合格 | 合格 |
| W-10 | ShowNotification → RemoveAll | 自アプリのグループが消える | 合格 | 合格 |
| W-11 | Home → もう一度 Notification 画面 → Initialize → W-03 / W-04 | 入り直した画面でも `NotificationInvoked` が届く | 保留 | **合格** |

W-11 の Initialize は、同じ Manager が残っているので 2 回目になり、ネイティブを呼ばずに成功する（設計 v8 の J-3）。

### 1.1 Manager を直接呼ぶテスト

「1.x で記録」は設計 v8 の 5.6 の手順 1 で、1.x のまま流して期待値を決めたもの。移行後も同じ期待値で通る。

| テスト | 確かめること | 1.x | 2.0.0 |
|---|---|---|---|
| `ShowNotification_FixtureA_EveryKey_ReachesTheCenter` ほか B〜D | 全キーの fixture（1.3 章）の Show が成功し、通知センターにタイトルが出る | 4 本とも合格 | 合格 |
| `ShowNotification_BrokenJson_Is3` | JSON の構文の誤り → 3 | 合格 | 合格 |
| `ShowNotification_SixButtons_Is7` | ボタン 6 個 → 7 | 合格 | 合格（ネイティブの show が返す） |
| `BadgeRemoveByIdAndGetAll_AreNotSupportedUnpackaged` | `SetBadge(1)` / `RemoveNotificationById(1)` / `GetAllNotifications` → 8。GetAll は `GetAllNotificationsCompleted` が 1 回、`NotificationOperationCompleted` は出ない | 合格 | 合格 |
| `SetBadge_BelowTheLowestGlyph_Is7` | `SetBadge(-7)` → 7 | 合格 | 合格 |
| `APerCallCallbackThatThrows_IsLogged_AndTheEventDoesNotCome` | per-call callback が投げると `LogError`、`NotificationOperationCompleted` は出ない | 合格 | 合格 |
| `APerCallCallbackThatThrows_OnGetAll_IsLogged_AndItsEventDoesNotCome` | GetAll でも同じく、`GetAllNotificationsCompleted` は出ない | 合格 | 合格 |
| `ShowNotification_AMissingKey_AWrongType_OrNull_Is7`（7 通り） | 必須キーの欠落、型の違い、`null` の値、配列の要素の `null`、`audio.type` が `"uri"` で `uri` が無い、`timestamp` が範囲外 → 7 | 流さない（5 になる。設計 v8 5.4） | 合格 |
| `ShowNotification_NullPayload_Is3` | `null` の payload → 3 | 流さない（プロセスが落ちる） | 合格 |
| `ShowNotification_ALoopingSoundWithoutLongDuration_Is7_FromTheNativeShow` | mute ＋ `loop: true` で `duration` なし → 7（ネイティブの検証） | — | 合格 |
| `ScheduleNotification_BeyondTheRange_Is7`（2 通り） | 予約時刻が ±922,337,203,685,477 ms を超える → 7 | 流さない（検査しなかった） | 合格 |
| `TheExternsThatTakeNull_Bind` | NULL を渡しても安全な `extern` を直接呼び、結び付けと NULL の扱いを確かめる | — | 合格 |

### 1.2 作り直しの流れ（`WindowsNotificationRecreatePlayerTests`）

カテゴリ `RecreatesTheNotificationManager`。検証スクリプトは本体の実行から除き、`--include-destructive` のときだけ別の Player の実行で流す。
失敗すると Manager が未初期化のまま残り、以後の SetUp の Initialize も失敗し続けるので、プロセスごと分ける（設計 v8 の 7.3）。

1. サンプルの Initialize を押して初期化する（SetUp）
2. `DestroyImmediate(Instance.gameObject)`
3. 新しい `Instance` で Show → 1、`GetNotificationSetting` → `Unknown`（E-1）
4. `Initialize(false, "", "")` → 7（E-2）
5. サンプルと同じ引数で `Initialize` → 成功（J-3、新しい Manager は初回）。設定は `Enabled`
6. Show → 成功し、通知センターに `Recreated manager` が出る

| 1.x | 2.0.0 |
|---|---|
| 合格 | 合格（bootstrap の shutdown のあとの再初期化も通った） |

### 1.3 全キーの fixture

1 つの JSON には全部入らない（`audio.type` は 1 値）ので 4 つに分ける。タイトルは通知センターで探す文字列。

| fixture | タイトル | 中身 |
|---|---|---|
| A | `Fixture A: every key` | title、body、tag、group、`scenario: "reminder"`、`duration: "long"`、buttons 2 個（`args` 2 組 / `invokeUri`）、textBoxes 2 個（placeholder と title / id だけ）、comboBoxes 1 個（title、defaultSelection、items 2 個）、`appLogo`（`crop: "circle"`）、`heroImage`、`inlineImage`、`audio`（`type: "event"`、`event: "reminder"`、`loop: true`）、`attribution`、`timestamp`（今の Unix 秒）、`expiration: 3600` |
| B | `Fixture B: mute and progress` | `audio: {type: "mute"}`、`progress`（title、value 0.4、valueStr、status） |
| C | `Fixture C: audio from a uri` | `audio: {type: "uri", uri: "file:///C:/Windows/Media/Windows%20Notify%20Email.wav"}` |
| D | `Fixture D: expires on reboot` | `expiresOnReboot: true` |

- 画像はどれも `StreamingAssets/app-icon.png` の `file:///`
- `expiration` は 60 秒以上にする（すぐ消えると通知センターで見つからない）
- `scenario` は `reminder` だけ。`alarm` / `incomingCall` / `urgent` は応答不可を越えてバナーが残り、通知センターを読む PowerShell が頼る前面の窓を奪いうる
- **確かめるのは Show の結果とタイトルだけ。** ボタン・入力・画像・音の見え方は確かめない（v1 の 1 章の「確かめないもの」と同じ理由）

## 2. 自動化の前提

v1 の 2 章のとおり（通知センターの開き方・見つけ方・読み方・閉じ方）。変えたところ:

- 通知センターを操作する PowerShell と、その起動・待ち・ログを `WindowsNotificationCenter`（`Tests/PlayMode/WindowsNotificationSamplePlayerTests.cs` の internal クラス）に移した。2 つの fixture が使う
- **SetUp の Initialize の直後に `GetNotificationSetting` を見て、`Enabled` でなければ理由を書いてすぐ失敗させる**（設計 v8 の 8.2）。初期化の前は `Unknown` になるので、前には置かない

## 3. この PC に触れるもの

v1 の 3 章のとおり。加えて、2.0.0 は初期化のたびに `HKCU\Software\Classes\AppUserModelId\Unity NativeToolkit` の `CustomActivator` も書く（上書き）。
**表示名が同じ別のアプリとクリックを奪い合う**ので、テスト中に「Unity NativeToolkit」という名前のアプリ（開発用ビルドなど）を動かさない。

## 4. 2.0.0 への移行で変わる期待値

**UI テストの期待値は変えない。** v1 の 4 章に挙げた違いは、Manager が公開 API の形に戻すので、テストには出ない。

| v1 の 4 章の項目 | 移行後 |
|---|---|
| 2 回目の初期化は `NOT_SUPPORTED` | Manager があればネイティブを呼ばずに成功（設計 v8 の J-3）。各テストの SetUp の Initialize も W-11 も成功のまま |
| タイムスタンプが秒からミリ秒 | 公開の JSON は秒のまま。C# で 1000 倍する（J-2） |
| 閉じたあとに活性化が 1 件届くことがある | 1.x と同じ。`_instance` が無ければ捨てる |

公開 API で変わる振る舞いは設計 v8 の 5.4 にまとめてある。UI テストで見えるのは、W-04 / W-11 が通るようになったことと、1.1 章の「移行後だけ」の行。

## 5. 実行結果

| 日付 | 実行 | Player | Notification |
|---|---|---|---|
| 2026-09-27 | 1.x、1.1 章の「1.x で記録」を足して（`--include-destructive`） | 68/69（W-04 はスキップ）、作り直し 1/1 | 19 本合格、W-04 スキップ |
| 2026-09-27 | 2.0.0（`--include-destructive`） | 81/82、作り直し 1/1 | **33 本すべて合格**（W-04 / W-11 を含む）。落ちた 1 本は Clipboard の `DeleteHistoryItemAsync_RemovesTheItem`（1.x のまま。削除した ID は消えたが、同じ文字の項目が履歴に 1 つあった。2026-09-26 にも 1 回あった） |
