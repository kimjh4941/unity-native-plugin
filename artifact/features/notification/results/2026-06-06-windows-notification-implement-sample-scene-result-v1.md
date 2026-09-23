# 実装結果レポート

> **2026-09-10 訂正**: 本文が「実機確認は未実施」としている箇所は**古い**。
> **実機確認は実施済み。ただし詳細記録は未作成。**
> 実施日・端末・OS バージョン・消化した項目・残った失敗は、この文書には無い。
> 痕跡として `manual/1.10.0/images/windows/notification/` に、
> サンプル画面を実機で撮った `Example_*` のスクリーンショットが 4 枚ある。
> **本文の「未実施」を、そのまま未消化として扱わないこと。**
> 経緯: `artifact/topics/device-verification-records/README.md`

## 基本情報

- 日付: 2026-06-06
- 機能名: notification
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-4
- 計画書: artifact/features/notification/designs/2026-06-06-windows-notification-sample-scene-design-v2.md

---

## 1. 変更ファイル

### 1.1 新規作成

| ファイル | 内容 |
|---------|------|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Windows/Notification/WindowsNotificationManagerExampleController.cs` | MonoBehaviour サンプルコントローラ（全 16 ボタン + 3 event 購読）|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Windows/Notification/WindowsNotificationManagerExample.uxml` | UI レイアウト（Initialize / Show / Update Progress / Cancel Scheduled / Remove / Query / Badge の 7 セクション）|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Windows/Notification/WindowsNotificationManagerExampleStyle.uss` | Windows Fluent Design スタイル（win-notif-* クラス）|

### 1.2 既存変更

| ファイル | 変更内容 |
|---------|---------|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs` | `ShowWindowsNotification` 追加、`RemoveExistingControllers` に `WindowsNotificationManagerExampleController` を追加 |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs` | 購読ガードに `UNITY_STANDALONE_WIN` を追加、`OnNotificationClicked` に `#elif UNITY_STANDALONE_WIN` ルートを追加、Editor フォールバック文言に Windows を追記 |

---

## 2. 実装したサンプル機能

### 2.1 計画由来の実装

| セクション | ボタン | Manager API |
|-----------|-------|------------|
| Initialize | Initialize (isPackaged toggle + clsid + launchUri) | `Initialize(isPackaged, clsid?, launchUri?)` |
| Show | ShowNotification / ScheduleNotification (+30s) | `ShowNotification` / `ScheduleNotification` |
| Update Progress | UpdateNotificationProgress (seq 単調増加) | `UpdateNotificationProgress(tag, group, value, valueStr, status, seq)` |
| Cancel Scheduled | CancelScheduledNotification | `CancelScheduledNotification(tag, group)` |
| Remove | RemoveByTag / RemoveById (id=1) / RemoveAll | `RemoveNotificationsByTag` / `RemoveNotificationById` / `RemoveAllNotifications` |
| Query | GetAllNotifications / GetNotificationSetting / OpenSettings | `GetAllNotifications` / `GetNotificationSetting` / `OpenNotificationSettings` |
| Badge | SetBadge(Alert/NewMessage/1) / ClearBadge | `SetBadge(int)` × 4 |

### 2.2 実装時の追加判断

- `BuildSamplePayload()` を static ヘルパーに分離し、ShowNotification / ScheduleNotification で共通利用
- `_sequenceNumber` フィールドをボタン押下ごとにプリインクリメント（`++_sequenceNumber` でコール時点の値を渡す）
- `OnGetAllNotificationsCompleted` グローバル event ハンドラは per-call callback に委譲し、event 自体は空実装（計画書どおり「supplemental」扱い）
- `InitializeUI` でデフォルト値を設定: clsid=`{00000000-0000-0000-0000-000000000000}`, launchUri=`myapp://`

---

## 3. ビルド結果

- Unity Editor コンパイル確認: **手動実行が必要**（Unity Editor が必要）
- 補足: `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` ガードにより Editor でも型が有効。ネイティブ呼び出しは `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` でガードされ Editor では実行されない

---

## 4. 手動確認観点

**この表は 6 章と併せて読むこと。** うち 5 行は操作するボタンが後日取り外されたため、
「実機待ち」ではなく**実施不能**になっている（6.2）。

| 確認内容 | 操作 | 期待結果 | 状態 |
|---------|------|---------|------|
| Initialize が成功すること | clsid 入力 → Initialize ボタン | ResultTextBlock に "✓ Initialize" | 実機待ち |
| 通知バナーがアクションセンターに届くこと | Initialize → ShowNotification | Windows アクションセンターに通知表示 | 実機待ち |
| バナークリックで NotificationInvoked が届くこと | バナークリック | ResultTextBlock に "NotificationInvoked: {...}" | 実機待ち |
| ScheduleNotification が 30 秒後に配信されること | ScheduleNotification ボタン | 30 秒後に通知表示 | 実機待ち |
| CancelScheduledNotification が配信を止めること | Schedule → Cancel | 30 秒後に通知が来ない | 実機待ち |
| UpdateNotificationProgress が進捗バーを更新すること | UpdateProgress ボタン | 通知センターの進捗バーが変化 | 実機待ち |
| RemoveNotificationsByTag が通知を削除すること | RemoveByTag ボタン | 通知センターから消去 | 実機待ち |
| RemoveNotificationById が指定 ID を削除すること | RemoveById ボタン | 通知センターから消去 | 実機待ち |
| RemoveAllNotifications が全通知を削除すること | RemoveAll ボタン | 通知センターから全消去 | 実機待ち |
| GetAllNotifications が JSON を表示すること | GetAll ボタン | ResultTextBlock に JSON 配列 | 実機待ち |
| GetNotificationSetting が enum を返すこと | GetSetting ボタン | "✓ NotificationSetting: Enabled" 等 | 実機待ち |
| SetBadge(Alert) がグリフバッジを表示すること | SetBadge(Alert) ボタン | タスクバーアイコンにグリフ | 実機待ち |
| SetBadge(1) が数値バッジを表示すること | SetBadge(1) ボタン | タスクバーアイコンに "1" | 実機待ち |
| ClearBadge でバッジが消えること | ClearBadge ボタン | タスクバーのバッジ消去 | 実機待ち |
| Editor でボタンを押すと fallback メッセージが出ること | Editor で各ボタン押下 | "Windows Standalone only..." | Editor 確認待ち |
| TopMenu の Notification ボタンが Windows で画面遷移すること | Windows スタンドアロンで TopMenu → Notification | WindowsNotification 画面へ遷移 | 実機待ち |

### 未実施項目

| 項目 | 理由 |
|------|------|
| 全手動確認 | Windows 実機 + native DLL が必要 |
| RemoveById / GetAll / SetBadge / ClearBadge の確認 | **実施不能**。2026-06-13 にボタンごと取り外された（6 章） |
| Editor での Awake ダイアログ確認 | Unity Editor での実行が必要 |
| TopMenu 遷移確認 | Unity スタンドアロンビルドが必要 |

---

## 5. 実行確認

- 提示文: 「このサンプル実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して終了 → review-implementation-sample-scene スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答: 未回答

---

## 6. 後日の削除（2026-06-13 追記）

本レポートの 2 章が「実装した」と書いた機能のうち **6 ボタン分が、1 週間後に取り外された**。
コミット `e29968e`「feat(sample): overhaul Windows notification sample for unpackaged apps」。

| 取り外したボタン | 対応 API |
|---|---|
| `RemoveByIdButton` | `RemoveNotificationById` |
| `GetAllButton` | `GetAllNotifications` |
| `SetBadgeAlertButton` / `SetBadgeNewMessageButton` / `SetBadge1Button` | `SetBadge` |
| `ClearBadgeButton` | `SetBadge(0)` |

`OnGetAllNotificationsCompleted` の購読も同時に外れ、現在の画面は
`NotificationOperationCompleted` と `NotificationInvoked` の 2 イベントのみを購読する。

### 6.1 理由と、その根拠の弱さ

コミットメッセージは **`Remove unsupported APIs for unpackaged apps`** と述べる。
Unity の Windows スタンドアロンビルドは常に unpackaged（MSIX なし）であり
（機能設計 v2 の 8 章）、その前提と整合する。

**ただし根拠はコミットメッセージ 1 行しかない。**
設計 v2 の 8 章「要検証事項」は `getAllNotifications` のバッファ不足を挙げているが、
**3 API が unpackaged で動かないという結論はどの成果物にも書かれていない。**
実機で確認した結果なのか、ドキュメントから判断したのかは追跡できない。
**未確認事項として扱うこと。**

### 6.2 4 章の 5 項目は実施不能になった

操作するボタンが存在しないため、次の 5 行は「実機待ち」ではなく**実施不能**である。

- `RemoveNotificationById が指定 ID を削除すること`
- `GetAllNotifications が JSON を表示すること`
- `SetBadge(Alert) がグリフバッジを表示すること`
- `SetBadge(1) が数値バッジを表示すること`
- `ClearBadge でバッジが消えること`

**待っていれば来る確認ではない。** 4 章の表はこの追記と併せて読むこと。

### 6.3 残っている不整合

| 場所 | 状態 |
|---|---|
| サンプルシーン計画 v2 | 取り外した 6 ボタンを載せたまま |
| `WindowsNotificationManager` | `SetBadge` / `RemoveNotificationById` / `GetAllNotifications` を公開したまま。**unpackaged で動かないという注記が無い** |
| マニュアル | 3 API を記載していない（結果として整合している） |

**サンプルから到達できない公開 API が 3 つある。** 利用者は XML コメントだけを見て呼び出せる。
注記を入れるか、非対応を明記するかは未決定。

**別タスクとして `artifact/features/notification/issues/unreachable-notification-apis.md` に切り出した。**
判断には実機で 3 API を叩く確認が要るため、本レポートでは結論を出さない。
