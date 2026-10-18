# 実装結果レポート v1（Android Notification の C ABI 2.0.0 への移行）

## 基本情報

- 日付: 2026-10-12
- 機能名: notification
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-13
- 計画ファイル: `artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md`（以下「設計」）
- 設計のレビュー: `artifact/android/notification/reviews/2026-10-12-android-notification-design-review-v1.md`〜`-v3.md`
- UI テスト計画: `artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v5.md`（以下「計画」。レビュー v1〜v3）
- UI の補助: `artifact/topics/android-c-abi-2/designs/2026-10-12-android-ui-driver-design-v15.md`（以下「補助の設計」）
- 親課題: `artifact/topics/android-c-abi-2/README.md`
- 同梱: native-toolkit dist 1.13.0（`android-native-toolkit-2.0.0.aar`、`android-native-toolkit-capi-2.0.0.aar`）。旧ブリッジの AAR（1.3.0 の 2 本）は段 4 まで残す（変えていない）

## 0. 状態サマリー

設計 5.5 の手順 1〜8 を終えた。手順 4〜8 はまだコミットしていない（実装のレビューの後に利用者の確認を取る）。

| 手順 | 内容 | コミット | 状態 |
|---|---|---|---|
| 1 | 設計（v1〜v4。レビュー 3 回） | `a080baf` | 済み |
| 2 | UI の補助をシェードと画面に広げる（補助の設計 v12〜v15、計画 v1〜v4。レビュー 3 回） | `92f518a`、`86dc814` | 済み |
| 3 | 1.x のまま 43 件を流して記録（43/43。計画 v5 の 8 章） | `b2850e9` | 済み |
| 4 | JSON のリーダーとマッピング、層 1 | 未コミット | 済み |
| 5 | `AndroidNotificationCApi`、層 1 | 未コミット | 済み |
| 6 | `AndroidNotificationManager` の置き換え、層 2a、結果の型 3 つ | 未コミット | 済み |
| 7 | 実機で計画の C ABI の列を流す（43/43）と保持のテスト（1/1） | 未コミット | 済み |
| 8 | `common.md`、`testing.md`、親課題の README | 未コミット | 済み |
| 9 | この実装結果 | 未コミット | 実装のレビューを待つ |

## 1. 実装サマリー

### 1.1 計画ファイル由来の実装

- **JSON の読み取り**（設計 4.2、J-N4、J-N5）: `AndroidNotificationJsonReader`（Windows のリーダーの写し。重複したキーは後の値、入れ子 512）と `AndroidNotificationJsonMapping`（1.x の `UnityNotificationJsonParser` の規則）。必須のキーを見る順は、1.x の Kotlin の引数の評価の順に合わせた（`channel` の有無 → `id` → `title` → `message` → チャンネルの `id`・`name` → style の `bigText`。予約は `notification`・`schedule` の有無 → 通知 → `triggerAtMillis`）
- **Bridge**（設計 4.1、4.4、4.5）: `AndroidNotificationCApi`。構造体 2 つ（32 / 56 バイト）、コールバック 3 つ、エラー値 0〜15 の文言、`ActionIdOf`、`ShownTag`、予約の options、アクション、`BuildContent` / `BuildChannel`（setter を順に呼び、拒んだ setter の名前を返す）、イベントの読み取り、`extern`
- **Manager**（設計 5.2 の J-N1〜J-N20、5.4）: `AndroidJavaObject` / `AndroidJavaProxy` / `UnityMainThreadDispatcher` を消し、Bridge を呼ぶ。ガードを `UNITY_ANDROID || UNITY_EDITOR` にした（結果の型 3 つも）。`NotificationActionResult` の古いコンストラクタ（`dataJson` を読む）を、読んだ組から作るものに置き換えた。継ぎ目: `SetNativeForTests`、`ScheduleWithFallback`、`ReadAndFree`、`EnsureListeners`、`OnInteractionCore` / `OnShownCore` / `OnSettingsCore`、`ResetStaticStateForTests`、`ListenersRegisteredForTests`、`PendingCallCountForTests`、`LastSettingsResultForTests`
- **層 2b**（計画）: `AndroidNotificationPlayerTests`（43 本。手順 3 で足した）と `AndroidNotificationRetentionTests`（AN-01。この段で足した）。`verify_unity_android.sh` に保持のテストだけの実機の実行を足し、ほかの実行から外した（補助の設計 7 章、計画 2 章）

### 1.2 実装時の追加判断

- **ビルダーの文字列の setter は、値があるときだけ呼ぶ**: NULL を渡したときの扱いが `Notification.h` に書かれていないため。真偽と数の setter は既定の値でも呼ぶ
- **dismiss のイベントはいつも付ける**（`set_dismiss_event(1)`）: 1.x はいつも付けた（設計 2.3）
- **設定の画面の継ぎ目を 2 つ足した**: `PendingCallCountForTests`（ライブラリに渡して完了していない設定の呼び出しの数。計画 6 章の段 6）と `LastSettingsResultForTests`（最後の完了の `OPENED` / `OPENED_FALLBACK`。計画 AN-40、AN-42）
- **`IsNotificationScheduled` は `persistAcrossBoot` の予約だけを見る**: 1.x も 2.0.0 も同じ（手順 3 で見つけた。計画 v5 の 0 章）。XML コメントに書いた
- **保持のテストは C ABI で直接通知を出す**: `BuildContent` と `ntk_notification_show` を使い、Manager を作らない

## 2. 変更ファイル

### 2.1 新規作成

| ファイル | 内容 |
|---|---|
| `Runtime/Android/Notification/AndroidNotificationJsonReader.cs` | JSON のリーダー |
| `Runtime/Android/Notification/AndroidNotificationJsonMapping.cs` | 中間の値の型と、1.x の規則のマッピング |
| `Runtime/Android/Notification/AndroidNotificationCApi.cs` | Bridge |
| `Tests/Runtime/AndroidNotificationJsonReaderTests.cs` | 層 1（35 本） |
| `Tests/Runtime/AndroidNotificationJsonMappingTests.cs` | 層 1（97 本） |
| `Tests/Runtime/AndroidNotificationCApiTests.cs` | 層 1（30 本） |
| `Tests/PlayMode/AndroidNotificationManagerEditorTests.cs` | 層 2a（22 本） |
| `Tests/PlayMode/AndroidNotificationRetentionTests.cs` | 層 2b の AN-01 |

### 2.2 既存変更

| ファイル | 内容 |
|---|---|
| `Runtime/Android/Notification/AndroidNotificationManager.cs` | C ABI への置き換え（公開の形は変えていない） |
| `Runtime/Android/Notification/NotificationResult.cs`、`NotificationReceivedResult.cs` | ガードだけ |
| `Runtime/Android/Notification/NotificationActionResult.cs` | ガード、コンストラクタの置き換え（internal） |
| `Runtime/Android/Notification/AndroidNotificationJsonBuilder.cs` | XML コメント（Manager は使わない。段 4 で消す） |
| `scripts/verify_unity_android.sh` | 保持のテストだけの実行、ほかの実行の `-testCategory "!AndroidNotificationRetention"` |
| `agent-rules/coding-rules/common.md`、`testing.md` | Notification の形、ガードの群、層の状況 |
| `artifact/topics/android-c-abi-2/README.md` | 段の状況 |

### 2.3 削除

なし（`NotificationActionResult` の `dataJson` の読み方は置き換えた）。

### 2.4 非変更

Payload 10 型、`AndroidNotificationJsonBuilderTests`、サンプルの Controller と UXML、`Assets/Plugins/Android/*`（設計 5.1）。

## 3. エラー契約反映

### 3.1 エラーケース実装反映（設計 6 章）

| # | 実装 | 確かめた層 |
|---|---|---|
| E-1 | マッピングが 1.x の文言を返す | 1（`Required_AsWordedAndOrderedIn1x` ほか）、2b（AN-17） |
| E-2 | `Failed to {op}: the JSON could not be read.` | 1、2b（AN-18） |
| E-3 | `CanCall` が JSON を読む前に判定 | 2a（`Editor_*`） |
| E-4 | `NotStartedMessage` | 1 |
| E-5 | `INVALID_PARAMETER` の文言、拒んだ setter の名前のログ | 1、2b（AN-19） |
| E-6 | `PERMISSION_DENIED` の文言 | 1、2b（AN-02〜AN-02c） |
| E-7 | `ScheduleWithFallback` | 2a（4 本）、2b（AN-05） |
| E-8 | `RESOURCE_NOT_FOUND` の文言 | 1、2b（AN-20） |
| E-9 | 各文言 | 1 |
| E-10 | `NOT_FOREGROUND` の文言 | 1、2a、2b（AN-43） |
| E-11 | `No Activity found ...` | 1、2a |
| E-12 | 問い合わせは `false` と `LogWarning` | コードレビュー |
| E-13 | `ex.Message` と `LogError` | コードレビュー |
| E-14 | 受け口の `try/catch` と `LogError` | コードレビュー |
| E-15 | 種類ごとの印とやり直し | 2a（3 本） |
| E-16 | `ActionIdOf` が null、ログを出して捨てる | 1、2a |
| E-17 | `ReadAndFree`、`BuildContent` / `BuildChannel` の解放 | 2a（2 本）、コードレビュー |

### 3.2 イベントの返却仕様

- 操作の結果は、どれも取り出し口を通り、呼び出しの後に出る（2a の `Editor_EveryOperationFailsAfterTheCall`、2b の AN-10）
- タップ・アクション・dismiss の action ID と data（2a、2b の AN-31〜AN-35、AN-39）、shown（2a、2b の AN-37）

### 3.3 success 時契約

`NotificationResult.Success` は `ErrorMessage` が null（今のまま）。

### 3.4 既知の差分（マニュアルへ引き継ぐ）

設計 5.3 の全行。実機で確かめたもの（計画 v5 の 8 章と、この段の 5 章）:

| 差分 | 1.x | C ABI |
|---|---|---|
| 権限が無いときの表示（J-N7） | 成功（通知は出ない） | `Failed to {op}: notifications are not allowed ...` |
| 範囲の外の値（J-N6） | 成功 | `Invalid argument for {op}: Please verify the input parameters.` |
| 引けないアイコン（J-N6） | 成功（アプリのアイコン） | `Failed to {op}: a resource named in the notification was not found.` |
| JSON の構文の誤り | org.json の文言 | `Failed to {op}: the JSON could not be read.` |
| 別のスレッドから | `{op} could not be started.` | 受け付ける |
| 通知からアプリを開く | 開かない（Android 16 の Pixel 6a で、`front` で押して前面に来なかった） | 開く |
| data の値 | JSON のエスケープが残る（`a\"b`、`c\/d`） | 元の値 |
| 通知の設定の画面 | アプリの詳細の画面に落ちた | 通知の設定の画面 |
| 設定の画面を後ろから | `{op} could not be started.`（別のスレッド） | `Failed to {op}: the app is not in the foreground.` |
| `DeleteChannel(null)` | NullPointerException の文言で失敗 | 成功（`""` を消す） |
| Manager を作る前のタップ | 捨てた | 保って、最初の作成で届く（AN-01） |

## 4. ビルド結果

- EditMode（Android™ Profile）: 1425/1426（skip 1）。Editor の PlayMode: 298/335（skip 37）
- IL2CPP ARM64 の Player: ビルドが通った（`extern` の宣言は Player のビルドでだけコンパイルされる）

## 5. テスト結果

`scripts/verify_unity_android.sh`（2026-10-12、Pixel 6a、Android 16）: **PASS**。

| 実行 | 結果 |
|---|---|
| EditMode | 1425/1426（skip 1）。Notification は 165 本（リーダー 35、マッピング 97、Bridge 30、Builder 3） |
| Editor の PlayMode | 298/335（skip 37）。`AndroidNotificationManagerEditorTests` 22/22 |
| 実機（Android™ Profile） | 166/166。`AndroidUiDriver` のテスト 108 本、skip 0。補助の命令 269、終了コード 0。Notification の 43 本はすべて C ABI の期待で流れた（`NTK-NOTIFICATION-RECORD` が `capi`） |
| 実機（保持のテストだけ） | 1/1（AN-01）。補助の命令 2、終了コード 0 |

### 5.1 テスト詳細

- 層 1 のマッピング: `AndroidNotificationJsonBuilder` が書いた JSON（Builder のテストの 3 件と、サンプルの種類〈bigText、progress、messaging、inbox、custom view、full screen〉）を読み、元の Payload の値と比べた。必須の順、型の違い、丸め、色の範囲、捨てる要素、style ごと、予約、チャンネル
- 実機の C ABI の値: 計画 v5 の各行の「期待（C ABI の版）」のとおり。AN-31・AN-34 は `ok front`（補助が押す前に HOME で後ろに回し、押した後に前面に来た）、AN-32・AN-33 は `ok not-front`。AN-40 は `com.android.settings/.Settings$AppNotificationSettingsActivity android.settings.APP_NOTIFICATION_SETTINGS` と `OPENED`、AN-42 は `OPENED`

### 5.2 未実施ケース詳細

| ケース | 理由 |
|---|---|
| アプリの通知がオフのときの `PERMISSION_DENIED` | 通知をオフにすると権限が取り消され、プロセスが止まる（計画 7 章）。権限が無いときと同じ値・文言 |
| Progress を後ろから（`SERVICE_START_NOT_ALLOWED`） | 安定して起こせない（計画 7 章）。文言は層 1 |
| `NOT_INITIALIZED`、`STORAGE_FAILED` など | 起こせない。文言は層 1 |
| プロセスが死んだ状態からのタップ | 手動（補助の設計 11 章） |
| 「壊すと落ちる」の確かめ（ワークフローの止める基準の 2） | まだ行っていない。実装のレビューの後に行う |

## 6. Definition of Done

| 項目 | 状態 |
|---|---|
| 設計 5.1 の新規作成・既存変更 | 済み（2 章） |
| 設計 7.1 の層 1 | 済み |
| 設計 7.2 の層 2a | 済み |
| 計画の層 2b（C ABI の列） | 済み（43/43、AN-01 1/1） |
| 公開 API を変えない | 済み（型・メンバー・イベントの形は同じ。ガードが広がった） |
| 実装のレビュー | 未 |

## 7. 残作業

- 実装のレビュー（サブエージェントと Codex）
- 段 4: 旧ブリッジの AAR を外す、11 型の改名、`AndroidNotificationJsonBuilder` を消す、マニュアルと移行ガイド（3.4 の全行、`tools:keep`、1.x の予約の破棄）
