# サンプルシーン計画レビュー結果

## 基本情報

- 日付: 2026-05-16
- 対象プラットフォーム: macOS
- レビュー対象: artifact/plans/macos-notification/2026-05-16-macos-notification-sample-scene-plan-v1.md

---

## 重大な問題 (High)

### H-1: OnEnable/OnDisable vs Start/OnDestroy のライフサイクル選択根拠が未記述

- Section 1.4「Manager イベントを `OnEnable`/`OnDisable` で購読」は iOS の `Start`/`OnDestroy` パターンと異なる
- 既存 ExampleController で統一されていない場合、実装者が混乱する
- 修正方針: Section 4 か Section 1 に「本機能では OnEnable/OnDisable を選択する理由」を 1 行追記する

### H-2: Editor 環境の fallback メッセージの実装パターンが未定義

- Section 2.3「macOS Standalone 以外では "macOS Standalone のみ..." と表示」とあるが、どのコードパスで出力するか未記述
- Manager 呼び出し前の実行時ガードと、`#if UNITY_STANDALONE_OSX` コンパイルガードの違いを明確にする必要がある
- 修正方針: Section 5 の実装詳細に、Editor 時の処理フロー（コンパイルガード or 実行時ガード）を追記する

### H-3: SimpleCallback 系操作でのダブル発火リスク

- `RequestPermission`, `ShowNotification`, `ScheduleNotification` 等は per-call コールバック (`onResult`) と `NotificationOperationCompleted` グローバルイベントの両方が発火する
- `NotificationOperationCompleted` を購読しつつ `onResult` も使用する場合、同一操作結果が 2 回 UI に反映される
- Section 5.6 で `RegisterCategory` 成功時のみ抑制記述があるが、他の操作は未対応
- 修正方針: 各操作セクションで「per-call callback のみを使用し、グローバルイベントでは重複チェックまたは非購読」と明記する

---

## 改善提案 (Medium)

### M-1: JsonBuilder メソッドの具体的な呼び出し例が不足

- Section 5.2/5.3 に「BuildContentJson / BuildTimeIntervalTriggerJson を呼ぶ」とあるが、入力欄とパラメータのマッピングが未記述
- 例: `_titleField.value` → `contentJson` へのマッピング方法を補足する

### M-2: ShowNotification と ScheduleNotification の identifier 命名規則が未定義

- `ShowNotification` は identifier 不要だが `ScheduleNotification` は `notificationId` (string) が必要
- サンプルから呼ぶ際に GUID / 固定文字列 / ユーザー入力のどれを使うか未記述

### M-3: namespace 宣言が計画書に記載されていない

- 新規ファイル `MacNotificationExampleController.cs` の namespace が Section 6 の変更ファイル一覧に含まれていない

---

## 軽微な指摘 (Low)

### L-1: 権限拒否シナリオの UI 応答が未記述

- Permission denied 時のボタン有効/無効制御が Section 5.1 に記載なし

### L-2: 状態遷移シナリオ（操作中 → 完了）の UI 挙動が未記述

- 操作中にボタンを二重押しした場合の挙動が未定義

### L-3: MacNotificationManager の Operation Kind 定数一覧が未参照

- `ShowNotification` / `ScheduleNotification` の kind 定数が計画書に列挙なし

### L-4: macOS Focus モード前提条件が未記述

- 権限があっても Focus モードで通知が抑制される場合のユーザー向け注釈なし

---

## ユーザー指摘

### U-1: 通知内容が iOS と重複していた

- Section 5.3 の通知タイトル・ボディが iOS と異なる独自内容で記述されていた
- iOS ExampleController と同一のゲーム向け内容（"Energy Refilled" / "Guild Battle Countdown" 等）に統一する
- 修正済み（v2 に反映）

### U-2: Permission ガードが一部ハンドラに抜けていた

- `CancelAll` / `RemoveDeliveredById` / `RemoveAllDelivered` / `GetScheduled` / `GetDelivered` / `SetBadgeCount` / `RegisterCategory` / `RemoveCategory` の 9 ハンドラに `ExecuteIfNotificationPermissionGranted` が未記載だった
- iOS ExampleController は Permission 系 4 つを除く全ボタンにガードを適用しており、macOS 計画もこれに準拠すべき
- 修正済み（v2 に反映）

### U-3: UXML の horizontal-scroller-visibility 属性が抜けていた

- Section 5.7 の ScrollView に `horizontal-scroller-visibility="Hidden"` が未記載だった
- iOS の `IosNotificationManagerExample.uxml` には同属性が明示されており、macOS も同一にすべき
- 修正済み（v2 に反映）

---

## 不足情報一覧

| # | 不足項目 | 対応状況 |
|---|----------|----------|
| 1 | OnEnable/OnDisable 選択根拠 | v2 修正済み |
| 2 | Editor fallback の実装コードパス | v2 修正済み |
| 3 | ダブル発火の回避戦略（per-call vs global の使い分け） | v2 修正済み |
| 4 | BuildContentJson/BuildTimeIntervalTriggerJson の入力マッピング | v2 修正済み |
| 5 | ScheduleNotification の identifier 生成方針 | v2 修正済み |
| 6 | namespace 宣言 | v2 修正済み |
| 7 | 権限拒否時のボタン制御 | v2 修正済み |
| 8 | 二重押し防止の状態遷移 | 未対応（実装時判断） |
| 9 | MacNotificationManager の Operation kind 定数一覧 | 未対応（実装時参照） |
| 10 | 通知内容が iOS と重複（U-1） | v2 修正済み |
| 11 | Permission ガード漏れ 9 ハンドラ（U-2） | v2 修正済み |
| 12 | UXML horizontal-scroller-visibility 未記載（U-3） | v2 修正済み |

---

## 実装計画整合性チェック

| 項目 | v1 評価 | v2 評価 |
|---|---|---|
| API 参照の正確性 | ○ | ○ |
| 既存パターン踏襲 | ○（OnEnable/OnDisable の根拠以外） | ○ |
| ダブル発火リスク対策 | × | ○ |
| Editor fallback パターン | × | ○ |
| 変更ファイル一覧の完全性 | △（namespace 未記載） | ○ |
| 手動確認観点の網羅性 | △（権限拒否・二重押しシナリオ未記載） | △（二重押し未対応） |
| iOS との実装一貫性（通知内容・ガード・スクロール） | × | ○ |

---

## 総合評価

**v1: 要修正 → v2: 実装可**

H-1/H-2/H-3 および U-1/U-2/U-3 はすべて v2 で解消済み。未対応の L-2（二重押し）と L-3（Operation kind 定数）は機能的に影響なく、実装時の判断で対処できる範囲。
