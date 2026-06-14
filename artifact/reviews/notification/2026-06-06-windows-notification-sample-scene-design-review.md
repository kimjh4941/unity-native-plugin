# レビュー結果

- 日付: 2026-06-06
- 対象ファイル: artifact/designs/notification/2026-06-06-windows-notification-sample-scene-design-v1.md
- 機能名: notification
- プラットフォーム: Windows

---

## 強み

- macOS Notification の header固定+ScrollView 構造、Windows Fluent Design カラーを再利用する方針が明確で UI の一貫性が保たれている
- OnEnable/OnDisable 購読管理・OnDestroy clicked -= アンバインド・Debug.Log 全ハンドラ先頭・ResultTextBlock パターンなど既存 ExampleController 規約を踏襲している
- Initialize の必須パラメータ空文字チェックと early return 方針が明示されている
- NotificationOperationCompleted / NotificationInvoked / GetAllNotificationsCompleted の 3 イベント購読と UnityMainThreadDispatcher 経由のメインスレッド更新方針が実装契約と整合している

## 改善点

### 高優先度

**[H-1] Initialize: appUserModelId 引数が存在しない**
- セクション: 4.2 / 5.4 Initialize 入力の状態管理
- 問題: 計画書は appUserModelId TextField・必須チェック・デフォルト値を規定しているが、実 API は `Initialize(bool isPackaged, string? clsid, string? launchUri, Action? onResult)` であり appUserModelId は存在しない
- 改善案: appUserModelId の入力欄・バリデーションを削除し、isPackaged(Toggle) / clsid(TextField) / launchUri(TextField) の 3 入力に修正する

**[H-2] ShowNotification: tag 引数が API に存在しない**
- セクション: 2.1 機能一覧 / 5.3 サンプルペイロード
- 問題: `ShowNotification(payload, tag?)` は誤り。実 API は `ShowNotification(string jsonPayload, Action? onResult)` で tag 引数を持たない。tag/group は WindowsNotificationPayload.Tag / .Group に含めて BuildNotificationPayload で JSON 化する
- 改善案: サンプルペイロードに Tag=SampleTag, Group=SampleGroup を設定し、ShowNotification(json, onResult) を呼ぶよう修正する

**[H-3] UpdateNotificationProgress: シグネチャが完全に異なる**
- セクション: 5.3 サンプルペイロード
- 問題: `UpdateNotificationProgress(SampleTag, progressPayload)` は誤り。実 API は `UpdateNotificationProgress(tag, group, value, valueStr, status, sequenceNumber, Action? onResult)` で WindowsNotificationProgressPayload を受け取らず個別引数 + group + sequenceNumber が必須
- 改善案: UpdateNotificationProgress(SampleTag, SampleGroup, 0.5, "50%", "Downloading...", sequenceNumber, onResult) の形式に修正。sequenceNumber は単調増加が必要なためコントローラ保持フィールドを明記する

**[H-4] Remove セクション: RemoveNotification(tag) は存在しない**
- セクション: 2.1 機能一覧 / Remove セクション
- 問題: `RemoveNotification(tag)` は実 API に存在しない。実際の API は `RemoveNotificationById(uint)` と `RemoveNotificationsByTag(string tag, string group)` の 2 つで group 引数が必須
- 改善案: RemoveNotificationsByTag(SampleTag, SampleGroup) と RemoveAllNotifications() と RemoveNotificationById(uint) の 3 ボタン構成に置き換える。SampleGroup 定数を導入する

**[H-5] Focus Assist セクション: SetFocusAssist は Manager に存在しない**
- セクション: 2.1 機能一覧 / Focus Assist セクション
- 問題: `SetFocusAssist(bool)` は WindowsNotificationManager の公開 API に存在しない（DllImport にも含まれない）
- 改善案: Focus Assist セクションを削除する。代わりに ScheduleNotification / CancelScheduledNotification を Show セクションに追加することで実装済み API を網羅する

**[H-6] TopMenuExampleController: UNITY_STANDALONE_WIN が購読ガードに含まれていない**
- セクション: 3.2 既存変更
- 問題: TopMenuExampleController L65 の `#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR` に UNITY_STANDALONE_WIN が含まれておらず、Windows スタンドアロンビルドでは Notification ボタンの clicked += OnNotificationClicked が登録されない。`#elif UNITY_STANDALONE_WIN` を OnNotificationClicked に追加しても反応しない
- 改善案: TopMenu の購読ガードに `|| UNITY_STANDALONE_WIN` を追加することを変更内容に明記する。加えて Editor フォールバック文言が「Android, iOS, or macOS」のままなので Windows を追記する修正も必要

### 中優先度

**[M-1] Badge: SetBadge の型と ClearBadge の扱い**
- セクション: 2.1 Badge セクション
- 問題: `SetBadge(WindowsBadgeValue)` は誤り。実 API は `SetBadge(int)` で enum のキャストが必要。ClearBadge 独立 API は存在せず `SetBadge(0)` または `SetBadge((int)WindowsBadgeValue.Clear)` で代替
- 改善案: SetBadge((int)WindowsBadgeValue.Alert) のキャスト方針と ClearBadge=SetBadge(0) であることを明記する

**[M-2] Query: GetNotificationSetting / GetAllNotifications の契約区別が未明記**
- セクション: 2.2 操作導線 / Query セクション
- 問題: GetNotificationSetting は enum 同期返却で NotificationOperationCompleted を発火しない特例 API。GetAllNotifications は (json, result) 2引数コールバック契約。この区別が SetResult パターン記述に反映されていない
- 改善案: GetNotificationSetting は同期戻り値 enum を直接 SetResult、GetAllNotifications は onResult(json, result) コールバックから SetResult する旨を操作導線に区別して明記する

**[M-3] IsInitialized property が存在しない**
- セクション: レビュー観点1 / 3.3 非変更
- 問題: IsInitialized public property は Manager に存在しない（private bool _initialized のみ）
- 改善案: サンプルからは Initialize の結果コールバックでのみ状態表示する旨を明記し、IsInitialized への言及を削除する

**[M-4] 手動確認観点: SetFocusAssist を含み Schedule 系が欠落**
- セクション: 6 手動確認観点
- 問題: 実在しない SetFocusAssist の確認観点を含み、実装済みの ScheduleNotification / CancelScheduledNotification / RemoveNotificationById の確認観点が欠落している
- 改善案: SetFocusAssist を除去し、Schedule/CancelScheduled/RemoveById の確認観点を追加する

## 不足項目

- ShowNotification/UpdateProgress/Remove で共有する SampleGroup 定数と tag/group 一致の設計記述
- UpdateNotificationProgress の sequenceNumber 単調増加管理（コントローラ保持フィールド `_sequenceNumber`）の記述
- ScheduleNotification(jsonPayload, scheduledTimeUnixMs) / CancelScheduledNotification(tag, group) のサンプル反映
- TopMenu 購読ガードへの UNITY_STANDALONE_WIN 追加と Editor フォールバック文言への Windows 追記

## 総合評価

UI 構造・ライフサイクル規約・イベント購読方針は高水準で踏襲できているが、実 API との不整合が 6 件（high）ある。Initialize（appUserModelId 非存在）、UpdateNotificationProgress（payload でなく個別引数+sequenceNumber）、Remove 系（ById/ByTag で group 必須）、ShowNotification（tag 引数非存在）、SetFocusAssist（API 自体が非存在）、TopMenu 購読ガード（UNITY_STANDALONE_WIN 欠落）はいずれも実装着手時にコンパイルエラーまたは導線不通になる。改善版ドキュメントへの修正後に implement-sample-scene へ進むことを推奨する。
