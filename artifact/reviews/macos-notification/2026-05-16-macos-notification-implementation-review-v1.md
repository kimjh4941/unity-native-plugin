# コードレビュー結果

## 基本情報

- 日付: 2026-05-16
- ブランチ: feature/UNT-3（アンコミット・untracked 9 ファイル）
- 対象プラットフォーム: macOS
- 実装計画: artifact/plans/macos-notification/2026-05-16-macos-notification-implementation-plan-v2.md
- 実装結果: artifact/results/macos-notification/2026-05-16-macos-notification-implementation-result-v1.md

## レビュー対象ファイル

- Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationResult.cs
- Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationJsonResult.cs
- Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationActionResult.cs
- Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationTextInputActionResult.cs
- Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationAuthorizationStatus.cs
- Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationPayloads.cs
- Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationJsonBuilder.cs
- Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/MacNotificationManager.cs
- Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/MacNotificationTests.cs

---

## レビュー概要

macOS 通知機能の C# 実装。DllImport 21 関数・5 種コールバック delegate・Singleton Manager・EditMode テスト 14 件を新規追加。全体的な設計・Bridge パターン準拠・エラー契約実装は計画書に沿っており、重大な問題は見つからない。csharp.md の一部要件（ログのパラメータ出力）と try-catch の配置範囲に軽微な不一致がある。

---

## 重大な問題（high）

なし

---

## 改善提案（medium）

### M-1: [MonoPInvokeCallback] の try-catch 範囲が不完全

計画書 4.7「全 MonoPInvokeCallback 内を try-catch(Exception) で囲む」要件との不一致。

- `OnActionReceived`（MacNotificationManager.cs:661-662）: `new MacNotificationActionResult(...)` が try-catch 外
- `OnTextInputActionReceived`（MacNotificationManager.cs:677-678）: `new MacNotificationTextInputActionResult(...)` が try-catch 外

`ParseFlatJsonObject` の実装は例外を投げない設計のため実害リスクは低いが、将来の変更で破綻する可能性がある。

修正方針: 各 [MonoPInvokeCallback] メソッド全体を try-catch で囲み、Enqueue 前の処理も保護する。

### M-2: csharp.md 違反 — public メソッドのログに全パラメータが含まれていない

csharp.md 要件「全メソッドの先頭1行目に、全パラメータを含む Debug.Log を必ず入れる」に違反。

| ファイル:行 | メソッド | 省略パラメータ |
|---|---|---|
| MacNotificationManager.cs:247 | RequestPermission | onResult |
| MacNotificationManager.cs:255 | HasPermission | onResult |
| MacNotificationManager.cs:263 | GetAuthorizationStatus | onResult |
| MacNotificationManager.cs:272 | OpenSettings | onResult |
| MacNotificationManager.cs:282 | ShowNotification | triggerJson, onResult |
| MacNotificationManager.cs:291 | ScheduleNotification | triggerJson, onResult |
| MacNotificationManager.cs:299 | UpdateNotification | contentJson, triggerJson, onResult |
| MacNotificationManager.cs:340 | GetScheduledNotifications | onResult |
| MacNotificationManager.cs:349 | GetDeliveredNotifications | onResult |
| MacNotificationManager.cs:374 | RegisterCategory | onResult |
| MacNotificationManager.cs:382 | RemoveCategory | onResult |
| MacNotificationManager.cs:391 | SetBadgeCount | onResult |

修正方針: Action 型パラメータは `onResult != null` 等で対応。例: `Debug.Log($"[{LogTag}][{nameof(RequestPermission)}] onResult: {onResult != null}");`

### M-3: テストファイルのパスが計画書と異なる

- 計画書(3.1): `Tests/Editor/Notification/MacNotificationResultTests.cs`
- 実装: `Tests/Runtime/MacNotificationTests.cs`

ディレクトリ・ファイル名の両方が異なる。実装結果ファイルには記録済みだが、計画書の変更ファイル一覧を実態に合わせて更新するか、意図的な選択として理由を補足することを推奨。

---

## 軽微な指摘（low）

### L-1: MacNotificationPayloads.cs フィールドスタイルが計画書と異なる

計画書(4.5): PascalCase プロパティ `public string Id { get; set; } = ""`
実装(MacNotificationPayloads.cs:16): 小文字フィールド `public string id = string.Empty`

既存の IosNotificationPayloads.cs パターンに合わせた意図的な選択であれば問題なし。テストコードも同じ命名を使用しており一貫している（要確認）。

### L-2: ParseFlatJsonObject の重複実装

MacNotificationActionResult.cs:29 と MacNotificationTextInputActionResult.cs:30 に同一ロジックが重複。バグ修正時に2箇所変更が必要になる。現状は許容範囲。

### L-3: per-call static コールバックフィールドの同時呼び出し動作が未記述

`s_onShow` 等の `static` フィールドは同一操作の並行呼び出し時に後発が先発を上書きする（iOS Manager と同様のパターン）。API ドキュメントコメントに「concurrent calls are not supported」等の記述を追加すると良い。

### L-4: MacNotificationActionResult / MacNotificationTextInputActionResult のテストなし

コンストラクタ・UserInfo パース（正常 JSON・空 JSON・null・エスケープシーケンス含む JSON）のテストがない。計画書のテスト方針に記載なしだが、EditMode テストに追加推奨。

---

## 実装計画整合性チェック

| 項目 | 評価 |
|---|---|
| Manager + Bridge パターン準拠 | ○ |
| 変更ファイル一覧との一致 | △（テストファイルのパス・名称が異なる） |
| テスト方針の網羅性 | △（EditMode テストは充実、PlayMode は未実装） |
| エラーケース全実装 | ○ |
| 返却仕様との整合 | ○ |

---

## プロジェクトルール適合チェック

| 項目 | 評価 |
|---|---|
| common.md 準拠 | ○（Singleton・スレッド契約・JSON 転送・Manager 設計） |
| csharp.md 準拠 | △（M-2: 複数メソッドでログの全パラメータ出力が不完全） |
| Bridge 実装品質（スレッド安全性・メモリ管理） | △（M-1: try-catch 範囲が不完全。メモリ管理は P/Invoke 自動で問題なし） |
| 既存 API 互換性 | ○（新規ファイルのみ、既存変更なし） |

---

## テストカバレッジ

### カバーできている観点

- MacNotificationResult.Success / Failure の全フィールド検証
- MacNotificationJsonResult.Success / Failure の全フィールド検証
- MacNotificationAuthorizationStatus の 5 種パース・未知文字列・JSON パース
- MacNotificationJsonBuilder.BuildContentJson の必須・省略フィールド
- MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson の "seconds" キー確認
- MacNotificationJsonBuilder.BuildCalendarTriggerJson の日時フィールド
- MacNotificationJsonBuilder.BuildCategoryJson の "id" キー・TextInput・空 actions

### 不足している観点

- MacNotificationActionResult / MacNotificationTextInputActionResult の構築・UserInfo パース（正常・異常・空・エスケープシーケンス）
- ParseFlatJsonObject のエスケープシーケンス含む JSON の境界値テスト
- PlayMode: Singleton 生成・DontDestroyOnLoad・コールバック経路（計画書に記載あり、未実装）

---

## 総合評価

**要修正（軽微）**

重大な問題はない。M-1（try-catch 範囲）と M-2（ログのパラメータ不足）は計画書要件および csharp.md との不一致のため修正推奨。L-1〜L-4 は機能的に問題なし。
