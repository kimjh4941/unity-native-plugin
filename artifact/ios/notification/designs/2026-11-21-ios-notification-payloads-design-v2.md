# iOS の通知の 5 型を iOS 用の新しい型にする（段 4、I-10 の案 2）v2

## 基本情報

- 記録日: 2026-11-21
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 4、[段 4 の計画](../../../topics/ios-c-abi-2/designs/2026-11-15-ios-c-abi-stage4-plan-v1.md) の項目 3
- 決定済み（2026-11-14、利用者の判断。[計画の設計 v4](../../../topics/ios-c-abi-2/designs/2026-11-14-ios-c-abi-2c-plan-design-v4.md) の I-10）: 接頭辞の無い 5 型は案 2。iOS 用の新しい型を作って iOS の公開 API・サンプル・テストを替え、旧い 5 型は macOS 用に今の場所と名前空間に残す。iOS の利用者には破壊的変更で、移行ガイドに旧名 → 新名の表を載せる
- 利用者の判断（2026-11-21）: 段 4 の破壊的変更を計画どおり進める

## v1 からの主な変更（v2。レビュー v1 の後。**レビューはしていない**）

レビュー v1（[../reviews/2026-11-21-ios-notification-payloads-design-review-v1.md](../reviews/2026-11-21-ios-notification-payloads-design-review-v1.md)。Codex は使えず、サブエージェントだけ）で A1 が 0 になり、止めた。止めた後の直しを入れた。

- 「同じ JSON」の確かめ方（D-6、V-1。S-1）: 旧い型のままテストを書いて緑にしてから、型の名前だけを替える
- `[MovedFrom]` を付けない理由（D-3。S-2）をリポジトリの実測に合わせた
- 使われなくなる `using` を消す（D-7、V-5。S-3）
- 移行ガイドへの申し送り（D-8。S-4）、macOS の説明と規則の範囲（D-1、D-5、3 章、V-4。S-5）

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
| D-1 | 新しい型の名前と場所 | `IosNotificationContentPayload`、`IosNotificationAttachmentPayload`、`IosTimeIntervalTriggerPayload`、`IosCalendarTriggerPayload`、`IosLocationTriggerPayload`。`Runtime/iOS/Notification/IosNotificationPayloads.cs`（今の `IosNotificationCategoryPayload` などと同じファイル）、名前空間 `JonghyunKim.NativeToolkit.Runtime.iOS.Notification`。`#if` は付けない（今のファイルの型と Builder にそろえる。`review-document` の P5） | iOS の型の今の名前の付け方（`IosNotificationCategoryPayload`、`IosNotificationActionPayload`）と、Android の段 4 の付け方（`AndroidNotification…`）にそろえる。trigger は `IosTimeInterval…` のように機能名を挟まない（I-10 に挙げた名前のまま） |
| D-2 | 新しい型の中身 | 旧い型と同じ項目・同じ名前・同じ既定値・同じ `[Serializable]`。`IosNotificationContentPayload.attachments` は `IosNotificationAttachmentPayload[]` | 利用者の直しは型の名前だけで済む。Builder が書く JSON は変わらない |
| D-3 | `[MovedFrom]` | 付けない | (1) Unity の API Updater は、このパッケージの `[MovedFrom]` で利用者のスクリプトを直さない（6000.4.2f1 の実測。`common.md` の段 4 の記述）ので、付けても得が無い。(2) `[MovedFrom]` が効くのは `[SerializeReference]` のデータだけで、旧い型が残る間は旧名のデータは旧い型として読まれる（実測はしていない）。(3) macOS の移行で旧い型を `Mac*` に改名するとき、同じ旧名を `sourceClassName` にする `[MovedFrom]` が要る。今 iOS の型に付けると、1 つの旧名を 2 つの型が名乗る |
| D-4 | `IosNotificationJsonBuilder` | 4 つの `Build*Json` の引数を新しい型にする。旧い型を受ける口は残さない | 決定どおり破壊的変更。旧い型を受ける口を残すと、iOS が macOS 用の型に縛られたままになり、macOS の移行で旧い型を改名するときにもう一度壊れる |
| D-5 | 旧い 5 型 | 今の場所・名前空間・中身のまま残す。ファイルの頭のコメント（「iOS と macOS で共用」「段 4 で iOS に専用の型」）と XML の説明を「macOS の通知」に直し、macOS の Builder が読まない項目（`subtitle`・`body`・`badge`・`categoryIdentifier` 以外。`MacNotificationJsonBuilder.cs` の `BuildContentJson`）があることを 1 行添える。`NotificationAttachmentPayload` は `NotificationContentPayload.attachments` の型なので消せない。`LocationTriggerPayload` は macOS の Builder が使っていないが、macOS の移行で決める | 「macOS は外す」（I-10）。macOS に変更を持ち込まない |
| D-6 | テスト | `IosNotificationJsonBuilder` に EditMode のテストを足す（今は無い。`testing.md`）。型ごとに 2 本: `new T()` の既定値だけの JSON と、全項目を既定値以外にした JSON（`attachments` は 2 つ）。あわせて新しい 5 型が `typeof(T).IsSerializable`。**順番**: (1) 旧い型のままテストを書いて EditMode で緑にする、(2) Builder とテストの型の名前だけを替える（期待の文字列には触らない）、(3) もう一度緑 | 型の差し替えで、既定値（`radius = 100.0`、`notifyOnEntry = true`、`attachments` の初期化）や項目の型（`double?` を `float?` に写すと、数ではなく引用符の付いた文字列になる）を写し違えても、今は何も落ちない |
| D-7 | 使い手の直し | iOS のサンプル、ハーネス、`IosNotificationPlayerTests` を新しい型に。この 3 つと `IosNotificationJsonBuilder` から、使われなくなる `using JonghyunKim.NativeToolkit.Runtime.Notification;` を消す。macOS の使い手は変えない | 残すと、後で macOS の型に `#if` を付けるか改名したときに、iOS の Player でだけ組めなくなる（Editor では気づけない） |
| D-8 | 移行ガイド | 段 4 の項目 7（マニュアル）で書く: 旧名 → 新名の 5 行の表、`using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;` に替える（旧い `using` は消す）。申し送り: (a) 1 つの payload を iOS と macOS の両方の Builder に渡していた人は、値を 2 つ作るか写す、(b) 型を `[SerializeReference]` で持っていたデータは新しい型に移らない、(c) `#if UNITY_IOS && !UNITY_EDITOR` の中の誤りは iOS の書き出しで初めて出る | 破壊的変更（I-10） |

## 3. 変更ファイル一覧

| 区分 | ファイル |
|---|---|
| 新規作成 | `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosNotificationJsonBuilderTests.cs` |
| 既存変更 | `Runtime/iOS/Notification/IosNotificationPayloads.cs`（5 型を足す）、`Runtime/iOS/Notification/IosNotificationJsonBuilder.cs`（引数の型、`using`）、`Runtime/Notification/NotificationPayloads.cs`（頭のコメントと説明を macOS に）、`Runtime/UI/iOS/Notification/IosNotificationManagerExampleController.cs`、`Assets/NativeToolkitIosUiHarness/Runtime/Scenarios/NotificationScenario.cs`、`Tests/PlayMode/IosNotificationPlayerTests.cs`（それぞれ型と `using`）、`agent-rules/coding-rules/common.md`（5 型の所属の表の道と所属を macOS に、件数の数え方）、`agent-rules/coding-rules/testing.md`（iOS の Notification の行に builder のテスト）、`agent-rules/workflows/review-document/workflow.md`（`NotificationContentPayload` を「iOS / macOS 専用」と書く行）、`artifact/topics/os-prefix-violations/README.md`（5 型の行） |
| 非変更 | macOS の Builder・サンプル・テスト、Android、マニュアルの過去の版 |

## 4. 確かめ

| # | 内容 | 確かめ方 |
|---|---|---|
| V-1 | EditMode の `IosNotificationJsonBuilderTests` が、差し替えの前後で同じ期待の文字列のまま通る | D-6 の順番（旧い型で緑 → 型の名前だけ替える → 緑）。前の緑は EditMode の回で記録する |
| V-2 | iOS の書き出し（IL2CPP まで）で組める（`#if UNITY_IOS && !UNITY_EDITOR` の中のサンプルとテストを含む） | `verify_unity_ios.sh` の Player の回 |
| V-3 | iOS の Notification のサンプルのテスト（NS01〜NS07）とハーネスの UI テストが今までどおり通る | 同上と `test_ios_ui.sh --tests NotificationUITests/...` |
| V-4 | macOS の組み立てと `MacNotificationTests` が変わらず通る | Editor の EditMode で `MacNotificationTests`。macOS のサンプルの `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` の中は Editor で組まれないので、macOS の側の差分が `NotificationPayloads.cs` のコメントの行だけであることを `git diff` で確かめる |
| V-5 | iOS のファイルに旧い名前と旧い `using` が残っていない | 識別子の境界で grep（`\bNotificationContentPayload\b` など 5 つと `using JonghyunKim.NativeToolkit.Runtime.Notification;`）を `Runtime/iOS`・`Runtime/UI/iOS`・`Tests/PlayMode/Ios*`・`Assets/NativeToolkitIosUiHarness` に当て、0 件 |
