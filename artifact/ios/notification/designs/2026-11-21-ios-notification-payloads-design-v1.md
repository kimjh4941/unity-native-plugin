# iOS の通知の 5 型を iOS 用の新しい型にする（段 4、I-10 の案 2）v1

## 基本情報

- 記録日: 2026-11-21
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 4、[段 4 の計画](../../../topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-stage4-plan-v1.md) の項目 3
- 決定済み（2026-11-14、利用者の判断。[計画の設計 v4](../../../topics/ios-c-abi-2/designs/2026-11-14-ios-c-abi-2c-plan-design-v4.md) の I-10）: 接頭辞の無い 5 型は案 2。iOS 用の新しい型を作って iOS の公開 API・サンプル・テストを替え、旧い 5 型は macOS 用に今の場所と名前空間に残す。iOS の利用者には破壊的変更で、移行ガイドに旧名 → 新名の表を載せる
- 利用者の判断（2026-11-21）: 段 4 の破壊的変更を計画どおり進める

## 1. 今の形

- 5 型は `Packages/com.jonghyunkim.nativetoolkit/Runtime/Notification/NotificationPayloads.cs`（名前空間 `JonghyunKim.NativeToolkit.Runtime.Notification`）: `NotificationContentPayload`、`NotificationAttachmentPayload`、`TimeIntervalTriggerPayload`、`CalendarTriggerPayload`、`LocationTriggerPayload`
- 使っているところ:

| 使い手 | 使う型 |
|---|---|
| iOS: `IosNotificationJsonBuilder`（公開。`BuildContentJson`・`BuildTimeIntervalTriggerJson`・`BuildCalendarTriggerJson`・`BuildLocationTriggerJson`） | 5 型すべて |
| iOS: サンプル `IosNotificationManagerExampleController` | 5 型すべて |
| iOS: UI のハーネス `NotificationScenario`、Player のテスト `IosNotificationPlayerTests` | `TimeIntervalTriggerPayload` |
| macOS: `MacNotificationJsonBuilder`（`BuildContentJson`・`BuildTimeIntervalTriggerJson`・`BuildCalendarTriggerJson`）、macOS のサンプル、`MacNotificationTests` | content・time interval・calendar の 3 つ |
| 規則: `agent-rules/coding-rules/common.md` | 名前を挙げている |
| マニュアル: `manual/<版>/notification*.md`、`docs/<版>/` | 過去の版は変えない。1.14.0 の版で書き直す（段 4 の項目 7） |

- iOS の Manager の公開の API は JSON の文字列を受けるので、Manager の口は変わらない。変わるのは、JSON を組む道具（`IosNotificationJsonBuilder`）の引数の型

## 2. 決めること

| # | 決めること | 決定 | 理由 |
|---|---|---|---|
| D-1 | 新しい型の名前と場所 | `IosNotificationContentPayload`、`IosNotificationAttachmentPayload`、`IosTimeIntervalTriggerPayload`、`IosCalendarTriggerPayload`、`IosLocationTriggerPayload`。`Runtime/iOS/Notification/IosNotificationPayloads.cs`（今の `IosNotificationCategoryPayload` などと同じファイル）、名前空間 `JonghyunKim.NativeToolkit.Runtime.iOS.Notification` | iOS の型の今の名前の付け方（`IosNotificationCategoryPayload`、`IosNotificationActionPayload`）と、Android の段 4 の付け方（`AndroidNotification…`）にそろえる。trigger は `IosTimeInterval…` のように機能名を挟まない（I-10 に挙げた名前のまま） |
| D-2 | 新しい型の中身 | 旧い型と同じ項目・同じ名前・同じ既定値・同じ `[Serializable]`。`IosNotificationContentPayload.attachments` は `IosNotificationAttachmentPayload[]` | 利用者の直しは型の名前だけで済む。Builder が書く JSON は変わらない |
| D-3 | `[MovedFrom]` | 付けない | 旧い型が残るので、Unity の Script Updater が旧名を新名に書き換えると、macOS のコードまで壊す。I-10 のとおり |
| D-4 | `IosNotificationJsonBuilder` | 4 つの `Build*Json` の引数を新しい型にする。旧い型を受ける口は残さない | 決定どおり破壊的変更。旧い型を受ける口を残すと、iOS が macOS 用の型に縛られたままになり、macOS の移行で旧い型を改名するときにもう一度壊れる |
| D-5 | 旧い 5 型 | 今の場所・名前空間・中身のまま残す。XML の説明を「macOS の通知」に直す（今は「iOS の通知」と書いている）。`NotificationAttachmentPayload`・`LocationTriggerPayload` は macOS の Builder が使っていないが、消さない（macOS の利用者のコードが使っているかもしれず、macOS の移行で決める） | 「macOS は外す」（I-10）。macOS に変更を持ち込まない |
| D-6 | テスト | `IosNotificationJsonBuilder` に EditMode のテストを足す（今は無い。`testing.md`）: 新しい 5 型それぞれの JSON のキーと値、既定値、null の項目を省くこと、`attachments` の配列 | 型の差し替えで項目の写しを落としても、今は何も落ちない |
| D-7 | 使い手の直し | iOS のサンプル、ハーネス、`IosNotificationPlayerTests`、`common.md` を新しい型に。macOS の使い手は変えない | — |
| D-8 | 移行ガイド | 旧名 → 新名の 5 行の表と、`using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;` が要ることを、段 4 の項目 7（マニュアル）で書く | 破壊的変更（I-10） |

## 3. 変更ファイル一覧

| 区分 | ファイル |
|---|---|
| 新規作成 | `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosNotificationJsonBuilderTests.cs` |
| 既存変更 | `Runtime/iOS/Notification/IosNotificationPayloads.cs`（5 型を足す）、`Runtime/iOS/Notification/IosNotificationJsonBuilder.cs`（引数の型）、`Runtime/Notification/NotificationPayloads.cs`（説明を macOS に）、`Runtime/UI/iOS/Notification/IosNotificationManagerExampleController.cs`、`Assets/NativeToolkitIosUiHarness/Runtime/Scenarios/NotificationScenario.cs`、`Tests/PlayMode/IosNotificationPlayerTests.cs`、`agent-rules/coding-rules/common.md`、`agent-rules/coding-rules/testing.md`（iOS の Notification の行に builder のテスト） |
| 非変更 | macOS の Builder・サンプル・テスト、Android、マニュアルの過去の版 |

## 4. 確かめ

| # | 内容 | 確かめ方 |
|---|---|---|
| V-1 | EditMode の `IosNotificationJsonBuilderTests` が通り、旧い型の名前で書いた JSON と同じ文字列になる | 新しいテストに、旧い型の `MacNotificationJsonBuilder` ではなく、今の `IosNotificationJsonBuilder` が書いていた JSON（直す前に記録した文字列）を期待として書く |
| V-2 | iOS の書き出し（IL2CPP まで）で組める（`#if UNITY_IOS && !UNITY_EDITOR` の中のサンプルとテストを含む） | `verify_unity_ios.sh` の Player の回 |
| V-3 | iOS の Notification のサンプルのテスト（NS01〜NS07）とハーネスの UI テストが今までどおり通る | 同上と `test_ios_ui.sh --tests NotificationUITests/...` |
| V-4 | macOS の組み立てと `MacNotificationTests` が変わらず通る | Editor の EditMode（macOS の型は Editor で組まれる） |
