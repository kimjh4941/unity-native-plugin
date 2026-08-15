# 実装結果レポート

## 基本情報

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- ブランチ: feature/UNT-9
- 基準計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`（review v5 で承認済み）
- 出力言語: 日本語（実装コード内の文言・コメントは英語）

---

## 1. 実装サマリー

### 1.1 native-toolkit 確認由来の実装（計画書どおり）

| 計画書 | 実装 |
|---|---|
| 1.1 公開 C 関数 15 個 | `IosClipboardManager` に `[DllImport("__Internal")]` 15 宣言。`clipboardCancelLoads` / `clipboardStopObserving` のみ requestJson なし |
| 1.2 コールバック型 3 種 | `ClipboardOperationCallback` / `ClipboardJsonCallback` / `ClipboardChangeCallback` を `[UnmanagedFunctionPointer(Cdecl)]` で定義 |
| 1.2 C `bool` は 1 byte | `isSuccess` に **`[MarshalAs(UnmanagedType.I1)]`** を付与（3.2） |
| 1.3 全 15 関数が非同期 | 同期メソッドを 1 つも作らず、全操作を「共通 event + 任意 per-call callback」に統一 |
| 1.4 scope はキー省略時のみ general | `IosClipboardJsonBuilder.AddScope` が null のときキーごと省略。`BuildRemovePasteboardJson` は常に scope を出力 |
| 1.5 content 9 種 | `IosClipboardContent` の 9 ファクトリ + builder の 9 分岐 |
| 1.5 append は options 不可 | `Append` / `BuildAppendJson` が options 引数を持たない（構造的に保証） |
| 1.7 patterns rawValue | `IosClipboardDetectionPatternExtensions` の対応表。`probableWebURL` の大文字を含め完全一致 |
| 1.8 封筒形式 | `IosClipboardJsonParser.TryReadEnvelope` |
| 1.8.1 `number` / `moneyAmounts[].amount` は Double | `double? Number` / `double Amount` |
| 1.9 エラーコード 24 種 | native の code / message をそのまま `IosClipboardErrorInfo` に載せる（C# 側で再解釈しない） |
| 1.10 制限値 64MiB | `IosClipboardJsonParser.MaxDataByteCount` |
| 1.11 変更イベントは封筒なし | `ParseChangeEvent` が封筒を通さず直接解析 |

### 1.2 実装時の追加判断

| # | 判断 | 理由 |
|---|---|---|
| A-1 | **`AssemblyInfo.cs` に `InternalsVisibleTo("NativeToolkit.Runtime.PlayModeTests")` を追加**（計画では「既存変更 0 件」だった） | PlayMode テストが Editor 限定の `ResetForTests()` を呼ぶために必須。追加のみで既存 API に影響しない。詳細は 2.2 |
| A-2 | `RunDestroyCleanup` の `managedCleanup` を `try { ClearAllPendingCallbacks(); } finally { _instance = null; }` にした | v5 レビュー最終確認での提案。`ClearAllPendingCallbacks` が投げても `_instance = null` を保証する |
| A-3 | ガード連鎖を `TryStartOperation<TResult>` に共通化（段階 1〜5 を 1 メソッドに集約） | 15 操作で同じ順序を必ず守らせるため。順序の実装ミスを構造的に防ぐ |
| A-4 | `IsBridgeAvailable()` を `#if UNITY_IOS && !UNITY_EDITOR` で分岐させ、Editor では常に false を返す | Editor では in-flight を取得する前に必ず段階 4 で return するため、native を呼ばずに in-flight が残る経路が生じない |
| A-5 | 拒否時に `Debug.LogWarning` / `Debug.LogError` を追加（計画書に明記なし） | B-0 / B-1 / B-11 が結果しか残さないと診断できないため |
| A-6 | `IosClipboardPayloads.cs` のプロパティを `init` ではなく `private set` にした | `init` は `IsExternalInit` を要求し Unity の C# 環境で不安定なため |
| A-7 | パーサの任意フィールドは型不一致でも null 扱い（失敗にしない） | 計画 5.5.1 の E-12 は欠落 / null のみ規定。前方互換のため型不一致も同じ扱いに寄せた（必須フィールドは E-11 どおり失敗） |
| A-8 | EditMode パーサテストの log 抑止を `[SetUp]` からテスト本体へ移した | Unity Test Framework は per-test log scope を SetUp 後に開くため `[SetUp]` での `ignoreFailingMessages` が効かない（初回実行で 20 件失敗して判明。4 章参照） |

### 1.3 計画から意図的に外したもの

- **`Awaitable` 版（`XxxAsync`）15 メソッド**: 計画 5.7 のとおり初期実装から除外。`OnDestroy` の pending 破棄と両立しないため（欠落ではない）
- **サンプルアプリ**: `design-sample-scene` の担当

---

## 2. 変更ファイル

### 2.1 新規作成

`Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/`（15 ファイル、計画 4.1 と完全一致）:

- `IosClipboardManager.cs` — Bridge + Manager 本体
- `IosClipboardPayloads.cs` — scope / creation request / content / copy options / load request / detection pattern
- `IosClipboardJsonBuilder.cs` — 13 リクエストビルダー
- `IosClipboardJsonReader.cs` — 最小 JSON リーダー（`JsonValue` / `JsonValueKind` / `JsonBase64Status` を含む）
- `IosClipboardJsonParser.cs` — 封筒検証 + 各 data マッピング
- `IosClipboardErrorInfo.cs`
- `IosClipboardOperationResult.cs`
- `IosClipboardReadResult.cs`
- `IosClipboardReadDataResult.cs`
- `IosClipboardSnapshotResult.cs`
- `IosPasteboardScopeResult.cs`
- `IosClipboardDetectionResults.cs`
- `IosClipboardLoadedItemResult.cs`
- `IosClipboardForegroundChangeResult.cs`
- `IosClipboardChangeEvent.cs`

`Packages/com.jonghyunkim.nativetoolkit/Tests/`（6 ファイル、計画 4.2 と完全一致）:

- `Runtime/IosClipboardJsonReaderTests.cs`
- `Runtime/IosClipboardJsonBuilderTests.cs`
- `Runtime/IosClipboardJsonParserTests.cs`
- `Runtime/IosClipboardResultTests.cs`
- `Runtime/IosClipboardManagerDispatchTests.cs`
- `PlayMode/IosClipboardManagerIntegrationTests.cs`

`.meta` は Unity が自動生成（エージェントは作成していない）。

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/AssemblyInfo.cs`
  - 追加内容: `[assembly: InternalsVisibleTo("NativeToolkit.Runtime.PlayModeTests")]` の 1 行 + 理由コメント
  - **計画からの逸脱**（計画 4.3 は「既存変更なし」）
  - 理由: PlayMode テストが `IosClipboardManager.ResetForTests()`（`internal` かつ `#if UNITY_EDITOR`）を呼ぶため。これがないと破棄系テスト 1 件で以降の PlayMode テストが全て `CLIPBOARD_MANAGER_DESTROYED` になる（計画 5.6.1 / 7.2 が求めるテスト隔離が実現できない）
  - 影響範囲: 追加のみ。既存の `NativeToolkit.Runtime.Tests` 向け宣言は変更せず、公開 API・実行時挙動に影響しない
  - 代替案（不採用）: `ResetForTests` を `public` にする → 公開 API 面が増えるため見送り

### 2.3 非変更（対象だが未変更）

- `Runtime/Clipboard/AndroidClipboard*.cs`、`ClipboardOperationResult.cs`、`ClipboardReadResult.cs`、`ClipboardDescriptionResult.cs`: 計画 2.1 / 4.4 のとおり Android の公開 API を壊さないため一切変更なし
- `Runtime/Common/*`: 変更不要
- `Editor/Build/PreBuildProcessor.cs` / `PostBuildProcessor.cs` / `Tools/iOS/IosFrameworkPatcher.cs`: `*.xcframework` をサフィックス一致で探索しており、1.3.0 への差し替えでコード変更が不要（計画 4.3 の確認どおり）
- `Runtime/NativeToolkit.Runtime.asmdef` / 各 Tests asmdef: 新規ファイルは既存アセンブリ配下のため変更不要
- `package.json`: バージョン更新は release スキルの担当

### 2.4 意図しない変更と対応

- `unity-native-plugin.slnx`: Unity のバッチ実行がプロジェクトを再生成した際にプロジェクト行の順序が入れ替わった。実装とは無関係のため `git checkout` で**復元済み**（差分なし）
- `Plugins/iOS/*-1.3.0.xcframework.meta`: Unity の import で自動生成。計画 4.0 のとおり**本実装の成果物に含める**

---

## 3. エラー契約反映

### 3.1 エラーケース実装反映

**C# Bridge 層（計画 6.3 の B-0 〜 B-11）: 全 12 ケース実装済み**

| # | コード | 実装箇所 | dispatch 経路 | テスト |
|---|---|---|---|---|
| B-0 | `CLIPBOARD_BUSY` | `TryStartOperation` 段階 5 | rejected | EditMode（状態モデル）※ |
| B-1 | `CLIPBOARD_BRIDGE_UNAVAILABLE` | `TryStartOperation` 段階 4 | rejected | PlayMode 15 操作すべて |
| B-2 | `CLIPBOARD_BRIDGE_UNAVAILABLE` | `InvokeNative` の catch | 通常 | 未実施（device 必須）|
| B-3 | `CLIPBOARD_INVALID_REQUEST` | 各操作の `validate` | rejected | PlayMode |
| B-4 | `CLIPBOARD_EMPTY_PATTERNS` | `ValidatePatterns` | rejected | PlayMode |
| B-5 | `CLIPBOARD_UNKNOWN` / no data | `TryReadEnvelope` | 通常 | EditMode E-1 |
| B-6 | `CLIPBOARD_UNKNOWN` / parse failed | `MalformedResponse` | 通常 | EditMode E-2/3/8/9/10/11/15 |
| B-7 | `CLIPBOARD_UNKNOWN` フォールバック | `ReadErrorInfo` | 通常 | EditMode E-4/5/6 |
| B-8 | `CLIPBOARD_UNKNOWN` / decode failed | `TryDecodeBase64` | 通常 | EditMode E-16 |
| B-9 | `CLIPBOARD_MAIN_THREAD_REQUIRED` | `TryStartOperation` 段階 1 | **off-thread rejected** | PlayMode（別スレッド実行）|
| B-10 | `CLIPBOARD_CONTENT_TOO_LARGE` | `TryDecodeBase64` の TooLarge | 通常 | EditMode（reader 境界値）|
| B-11 | `CLIPBOARD_MANAGER_DESTROYED` | `TryStartOperation` 段階 2 | rejected | PlayMode（破棄後）|

※ B-0 は Editor では到達しない（段階 4 の platform 判定が先に成立するため）。単一 flight の不変条件は `TryBeginOperation` / `EndOperation` の純粋関数テストと状態モデルテストで検証。実 native 経路の確認は M-24（実機）。

**パーサ検証規則（計画 5.5.1 の E-1 〜 E-16）: 全 16 ケース実装・テスト済み**

- E-14（上限超過）/ E-15（`byteCount` 不一致）/ E-16（非正準 base64）を含む
- `TryGetDecodedLength` は padding を減算し、64MiB ちょうどの合法値を誤拒否しない

**変更イベント破棄規則（計画 5.5.2）: 実装・テスト済み**

- parse 不能 / `kind` 欠落 → **破棄**（`ClipboardChanged` を発火しない、ログのみ）
- 未知の `kind` 文字列 → `Unknown` として**発火**
- `scope` 欠落・不正 → `Scope = null` で**発火**

**native 層のエラー（計画 6.1 / 6.2）**: C# 側は code / message を再解釈せず、`IosClipboardErrorInfo` にそのまま載せる。パーサテストで `CLIPBOARD_UNAVAILABLE` / `CLIPBOARD_LOAD_FAILED`（details 付き）/ `CLIPBOARD_CANCELLED` の透過を確認。

### 3.2 コールバック返却仕様反映

- Operation 系 7 操作: `ClipboardOperationCompleted`（共通）→ per-call callback の順で `IosClipboardOperationResult` を返す
- Json 系 8 操作: 個別 event → per-call callback の順で型付き結果を返す
- **共通 event は per-call callback の指定有無に関わらず常に発火**（PlayMode で確認）
- 例外分離: `InvokeInOrder<TResult>` が common / perCall を別々の try/catch で包み、native へ例外を出さない（EditMode で確認）
- 結果は必ず `UnityMainThreadDispatcher` 経由（PlayMode の `ResultsAreNotDeliveredSynchronously` で確認）

### 3.3 success 時契約

**`IsSuccess == true` のとき `Error == null`**（本実装では `ErrorMessage` を result 直下に持たせず `Error` に一本化。計画 3.5 の規約）

- 全 9 result 型で `IosClipboardResultTests` が検証（21 ケース、全 passed）
- 逆方向（`IsSuccess == false` のとき `Error != null`、かつ code / message が非 null・非空白へ正規化される）も同ファイルで検証
- payload の非 null 契約（失敗時に collection は空、参照型は null）も検証

---

## 4. ビルド結果

- 実行コマンド:
  - `/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath <repo> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`
  - `同上 -testPlatform PlayMode`
  - 事前確認: `dotnet build`（`UnityEngine.CoreModule.dll` 参照）で `UNITY_EDITOR` 定義時・`UNITY_IOS` 定義時の両方をコンパイル検証
- 結果: **SUCCESS**
- 補足ログ:
  - `dotnet build`: 両定義とも `0 Warning(s) / 0 Error(s)`
  - Unity バッチ実行: `error CS` の出力なし（コンパイルエラー 0）
  - Unity バージョン: 6000.4.2f1（`ProjectSettings/ProjectVersion.txt` と一致）

---

## 5. テスト結果

- 実行したテスト:
  - EditMode: `IosClipboardJsonReaderTests` / `IosClipboardJsonBuilderTests` / `IosClipboardJsonParserTests` / `IosClipboardResultTests` / `IosClipboardManagerDispatchTests`
  - PlayMode: `IosClipboardManagerIntegrationTests`
  - 既存テスト（Android / Share / Notification 等）も同時実行
- 結果サマリー:
  - 実行件数: **EditMode 345 / PlayMode 39**
  - 成功: **EditMode 345 / PlayMode 28**
  - 失敗: **0 / 0**
  - スキップ: EditMode 0 / PlayMode 11（すべて既存の `AndroidClipboardManagerIntegrationTests`。device-only のため元から skip）
- 新規追加分の内訳: EditMode **133 件**（reader 27 / builder 30 / parser 38 / result 21 / dispatch 17）、PlayMode **15 件**
- 既存テストへの影響: なし（既存 212 件はすべて passed のまま）
- 失敗時の対応:
  - 初回 EditMode 実行で `IosClipboardJsonParserTests` の 20 件が失敗。原因は `LogAssert.ignoreFailingMessages` を `[SetUp]` に置いていたこと（Unity Test Framework は per-test の log scope を SetUp 後に開くため無効化される）。各テスト本体の先頭で設定する `IgnoreExpectedParserErrorLogs()` に変更し、再実行で全 passed（追加判断 A-8）

### 5.1 テスト詳細

| テスト観点 | テストファイル | テストケース | 結果 | 備考 |
|---|---|---|---|---|
| JSON リーダー構文網羅 | `Tests/Runtime/IosClipboardJsonReaderTests.cs` | 27 件 | ○ | 配列の配列 / サロゲートペア / 深さ上限 / 不正入力で例外を投げない |
| base64 の正確な長さ算出 | 同上 | `TryGetDecodedLength_AccountsForPadding` ほか 3 件 | ○ | padding 0/1/2、4 の倍数でない長さ、padding 3 文字を検証 |
| base64 上限の境界 | 同上 | `TryGetBase64Bytes_ExactlyAtLimit_Succeeds` / `_OneByteOverLimit_ReportsTooLarge` / `_JustUnderLimit_Succeeds` | ○ | limit-1 / limit / limit+1。超過時にバッファを確保しないことも検証 |
| リクエスト JSON 13 種 | `Tests/Runtime/IosClipboardJsonBuilderTests.cs` | 30 件 | ○ | `EveryBuilderOutput_IsParseableJson` で 13 本すべてを解析可能と確認 |
| scope キー省略規則 | 同上 | `Scope_Null_OmitsTheKeyEntirely` ほか | ○ | general に name を出さない / remove は必ず scope を出す |
| append の options 禁止 | 同上 | `BuildAppendJson_NeverEmitsOptions` | ○ | 引数を持たない構造で保証 |
| copy options 4 パターン | 同上 | `_NullOptions_OmitsTheKey` / `_PrivacyPreservingDefault_` / `_ExplicitLocalOnlyValues_` | ○ | class + factory により `default` 反転が起こらないことも検証 |
| ISO8601 出力 | 同上 | `_ExpirationDate_IsUtcIso8601WithoutFractionalSeconds` / `_LocalExpirationDate_IsConvertedToUtc` | ○ | |
| 日本語・絵文字の往復 | 同上 | `Strings_JapaneseAndEmoji_SurviveARoundTripThroughTheReader` | ○ | C# 内での往復。UTF-8 マーシャリングは実機確認（M-1） |
| 封筒検証 E-1〜E-16 | `Tests/Runtime/IosClipboardJsonParserTests.cs` | 38 件 | ○ | 構造不正を既定値で成功に合成しないことを全件確認 |
| `matchingItemIndexes` の null と空 | 同上 | `ParseSnapshotResult_MatchingItemIndexes_DistinguishesNullFromEmpty` | ○ | |
| loadItem の 5 kind | 同上 | `ParseLoadedItemResult_AllKinds` / `_UnknownOrFutureKind_IsSuccess` / `_KindWithoutItsValue_Fails` | ○ | |
| 変更イベント破棄規則 | 同上 | `ParseChangeEvent_UnparsableOrKindless_IsDropped` / `_NativeUnknownKind_IsDeliveredAsUnknown` / `_MissingOrMalformedScope_StillDelivers` | ○ | 破棄と Unknown 発火の分岐を確認 |
| 結果型の不変条件 | `Tests/Runtime/IosClipboardResultTests.cs` | 21 件 | ○ | 全 9 result 型 + error info 正規化 |
| dispatch 順序・例外分離 | `Tests/Runtime/IosClipboardManagerDispatchTests.cs` | `InvokeInOrder_*` 5 件 | ○ | 片方が例外でも他方が呼ばれる |
| single-flight | 同上 | `TryBeginOperation_*` 4 件 + `StartAndStopObserving_ShareOneObservationKey` | ○ | 別操作が干渉しないこと、共有キーを確認 |
| 誤配送の再現防止 | 同上 | `SingleFlight_OutOfOrderCompletion_CannotDeliverOneCallsResultToAnother` | ○ | production に無効化フラグを置かず、純粋な状態モデルで再現 |
| 破棄 cleanup の例外安全性 | 同上 | `RunDestroyCleanup_*` 5 件 | ○ | stop / cancel / managed のいずれが投げても managed 実行・例外非伝播 |
| 15 操作の B-1 拒否 | `Tests/PlayMode/IosClipboardManagerIntegrationTests.cs` | `EveryOperation_InEditor_FailsWithBridgeUnavailable` | ○ | Operation 系 7 + Json 系 8 を 1 テストで網羅 |
| dispatcher 経由の順序 | 同上 | `Operation_CommonEventFiresBeforePerCallCallback` ほか 3 件 | ○ | 同期発火しないことも確認 |
| 引数検証 B-3 / B-4 | 同上 | `NullContent_*` / `EmptyPatterns_*` | ○ | native と同一の code / 文言 |
| observation | 同上 | `StartObserving_ReportsItsOwnOperationName` / `Observation_InEditor_NeverRaisesClipboardChanged` | ○ | |
| main thread 契約 B-9 | 同上 | `OffThreadCall_OnACachedInstance_IsRejected` / `_DoesNotPolluteTheInFlightState` | ○ | 別スレッドから実行。in-flight 汚染がないことも確認 |
| lifetime 契約 B-11 | 同上 | `AfterDestroy_EveryOperationIsRejected` / `_PendingCallbacksAndInFlightStateAreCleared` | ○ | 再生成しても tombstone が解除されないことを確認 |
| テスト隔離 seam | 同上 | `ResetForTests_RestoresAUsableManager` | ○ | reset 後に Awake が再取得することを含めて確認 |

### 5.2 未実施ケース詳細

| テスト観点 | テストファイル | テストケース | 未実施理由 |
|---|---|---|---|
| B-2（`DllImport` 例外） | - | - | Editor では native を呼ばないため到達しない。実機で XCFramework 未リンク時にのみ発生 |
| B-0（busy）の実 native 経路 | - | - | Editor では段階 4（platform）で先に拒否されるため到達しない。不変条件は純粋関数・状態モデルで検証済み。実機確認は M-24 |
| 複合 lifetime シナリオ（旧 callback 後着） | - | - | Editor では native コールバックが発生しないため end-to-end 再現不可。構成要素（破棄後の B-11 拒否、`DiscardIfTerminated`）は個別に検証済み。完全な再現は層 2b（実機） |
| 手動確認 M-1 〜 M-25 | - | - | 実機 iOS 18 以降 / IL2CPP / ARM64 が必要（計画 7.3）。**本実装では未実施** |
| 大容量ペイロード（M-22 / M-23） | - | - | 実機メモリ計測が必要。EditMode では上限判定ロジックのみ縮小スケールで検証 |
| Reload Domain / Scene Reload 両無効構成（計画 9.9） | - | - | 当該 Editor 設定での実測が必要。今回は既定設定で実行 |

---

## 6. Definition of Done（計画 v5 の 26 項目）

判定基準: ○ 実装・コード・テスト確認の範囲で OK / △ 一部 OK・追加確認要 / × 未達 / - 対象外

| # | 項目 | 判定 | 備考 |
|---|---|---|---|
| 1 | 1.3.0 XCFramework の import と `.meta` | ○ | Unity 生成の `.meta` 2 件が成果物に含まれる |
| 2 | M-25（iOS build / link smoke test） | × | **未実施**。実機 / Xcode ビルドが必要 |
| 3 | 4.1 / 4.2 の新規ファイル | ○ | 15 + 6 = 21 ファイル、計画と完全一致 |
| 4 | 既存 C# ファイルへの変更 0 件 | △ | `AssemblyInfo.cs` に 1 行追加（2.2 の理由。追加のみ・非破壊） |
| 5 | `[MarshalAs(UnmanagedType.I1)]` | ○ | |
| 6 | 全 15 操作の single-flight と `CLIPBOARD_BUSY` | ○ | 実 native 経路の確認は M-24 |
| 7 | rejected 専用 dispatch と不変条件 1〜4 | ○ | `DispatchRejectedResult` / `DispatchOffThreadRejection` |
| 8 | `XxxAsync` が実装されていない | ○ | 意図的な除外 |
| 9 | 共通 event → per-call の順序、誤配送なし | ○ | |
| 10 | main thread 限定の明記と B-9 拒否 | ○ | `Instance` getter の制約も XML コメントに記載 |
| 11 | 破棄・再生成が非対応、B-11 拒否 | ○ | |
| 12 | `OnDestroy` の順序と例外非伝播 | ○ | tombstone 先行 → 独立 try/catch → finally |
| 13 | cleanup seam が引数渡し、mutable static フックなし | ○ | `RunDestroyCleanup(Action, Action, Action)` |
| 14 | 全 native コールバックが `DiscardIfTerminated` から始まる | ○ | 変更イベントを含む 16 経路 |
| 15 | observation の共有キーと世代判定 | ○ | |
| 16 | 3.5 の公開型仕様どおり | ○ | nullability / collection 非 null を含む |
| 17 | E-1〜E-16 と変更イベント破棄規則 | ○ | |
| 18 | B-0〜B-11 | ○ | B-2 のみテスト未到達（5.2） |
| 19 | `TryGetDecodedLength` と `Data.Length == byteCount` | ○ | |
| 20 | response 側メモリ削減策 1〜4 | △ | 1〜3 は実装済み（オフセット保持・遅延実体化・確保前の上限判定）。4（推奨サイズの明記）は XML コメントに記載済みだが実機実測は未（M-22 / M-23） |
| 21 | 層 1 / 層 2a のテスト全 passed | ○ | EditMode 345 / PlayMode 28 |
| 22 | 複合テスト（旧 callback 後着） | △ | 構成要素は検証済み。end-to-end は Editor で再現不可（5.2） |
| 23 | 本文 / base64 / 検出値 / pasteboard 名を出力しない | ○ | Manager・builder・parser すべてで形状と件数のみログ |
| 24 | `public` メンバの英語 XML コメント | ○ | |
| 25 | `ResetStaticState` / `ResetForTests` の共通 `ResetCore` | ○ | |
| 26 | M-1〜M-25 の実機確認 | × | **未実施**（5.2） |

**総括**: ○ 21 / △ 3 / × 2。× 2 件と △ の一部はいずれも**実機（層 2b / 層 3）が必要**な項目であり、計画 7.3 で手動確認として定義済みの範囲。

---

## 7. 要確認事項（実機確認前に断定しない）

- 計画 9.1（UTF-8 マーシャリング）/ 9.2（scope null と General の等価性）/ 9.3（`Read` の貼り付け許可 UI）/ 9.4（大容量の実効上限）/ 9.5（`matchingItemIndexes` の実出力）/ 9.6（single-flight 粒度）/ 9.9（Reload Domain 両無効構成）は**いずれも未検証のまま**
- B-2 の文言（`{operation} could not be started.`）は実機で `DllImport` が失敗する状況を再現できていないため、実際の到達確認は未実施

---

## 8. ステップ9 実行確認

- 提示文:
  - 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して終了 → review-implementation-feature スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答
