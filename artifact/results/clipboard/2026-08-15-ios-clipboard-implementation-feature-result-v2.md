# 実装結果レポート v2

## 基本情報

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- ブランチ: feature/UNT-9
- 基準計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 前版: `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v1.md`
- 対象レビュー: `artifact/reviews/clipboard/2026-08-15-ios-clipboard-implementation-feature-review-v1.md`（総合評価「要修正（重大）」）

---

## 0. v1 レビュー指摘への対応

| # | 指摘 | severity | 対応 | 状態 |
|---|---|---|---|---|
| H-1 | 1.3.0 XCFramework の `.meta` に `PluginImporter` がなく、M-25 も未実施 | high | `.meta` を PluginImporter 構成で整備し、Unity の正規 import 経路（`ConfigureIosXcframeworkImporter`）で再生成。**M-25 を実施し BUILD SUCCEEDED を確認**（4 章） | 解消 |
| M-1 | JSON reader が不正 escape・制御文字・number 文法を受理 | medium | `ParseString` / `ParseNumber` を JSON 文法どおりに厳格化。テスト 5 件追加 | 解消 |
| M-2 | single-flight / lifetime / observation のテストが production 経路を通らない | medium | Editor 限定の seam を追加し、PlayMode テストを**実際の pending slot / `s_inFlight` / 世代**経由に置換（15 件追加） | 解消 |
| M-3 | main-thread dispatch が破棄済み dispatcher を判定できない | medium | `Dispatch` / `DispatchRejectedResult` を Unity の null 演算子に変更。off-thread のみ plain reference 判定を維持 | 解消 |
| M-4 | snapshot の配列型不一致を空配列として成功扱い | medium | `allTypeIdentifiers` の行と `matchingItemIndexes` の型を必須検証し B-6 へ | 解消 |
| L-1 | `unity-native-plugin.slnx` の意図しない差分 | low | 復元済み（`git status` に差分なし） | 解消 |
| L-2 | 公開 factory のログ例外説明が不足 | low | payload / result の全 10 ファイルに逸脱理由を明記 | 解消 |

---

## 1. 実装サマリー

### 1.1 v1 からの変更（レビュー対応）

**JSON reader の厳格化**（`IosClipboardJsonReader.cs`）

- `ParseString`: 許可 escape（`" \ / b f n r t u`）のみ受理、`\u` は 4 桁 16 進を検証、raw U+0000〜U+001F を拒否。旧実装は `\` の次の 1 文字を無条件にスキップしていたため `\q` や `\u12` を通していた
- `ParseNumber`: `-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?` の状態遷移に置換。旧実装は先頭 `+`、`01`、`1.`、`1e`、`1.2.3` を通していた
- いずれも設計 5.5.1 の E-2（parse 不能 → B-6）契約に一致させたもの

**dispatcher 生存判定**（`IosClipboardManager.cs`）

- `Dispatch` / `DispatchRejectedResult` は main thread 上でのみ呼ばれるため、Unity の null 演算子で破棄済み wrapper を検出する
- `DispatchOffThreadRejection` のみ `(object?)dispatcher == null` を維持（off-thread で Unity のオーバーロードを呼ばないため）

**snapshot の型検証**（`IosClipboardJsonParser.cs`）

- `allTypeIdentifiers` の各行が array であることを必須検証（非 array は B-6）
- `matchingItemIndexes` は array / null / 欠落のみ受理。**その他の型は B-6**。null（未指定）と空配列（該当なし）の区別が公開契約であるため、どちらかへ寄せると「何を問い合わせたか」を偽ることになる

**テスト seam**（`IosClipboardManager.cs`、すべて `#if UNITY_EDITOR`）

| seam | 目的 |
|---|---|
| `BridgeAvailableOverrideForTests` | guard 連鎖を platform 判定より先へ進め、**production の pending slot / in-flight / 世代**を通す。native 呼び出しは Editor で空になるため、operation は seam で結果を届けるまで pending のまま |
| `CompleteOperationForTests` / `CompleteObservationControlForTests` / `CompleteReadForTests` / `CompleteSnapshotForTests` / `DeliverChangeEventForTests` | native コールバック到着を再現。いずれも `DiscardIfTerminated` と single-flight 解放を**迂回しない** |
| `IsInFlightForTests` / `InFlightCountForTests` / `HasChangeRegistrationForTests` / `PendingObservationGenerationForTests` / `HasAnyPendingCallbackForTests` | 状態の観測 |

安全ガードを無効化する経路は追加していない。`ResetCore` は `BridgeAvailableOverrideForTests` も初期化する。

`OnStartObservingResult` / `OnStopObservingResult` の重複ロジックは `HandleObservationControlCallback` に集約し、Editor seam も同じ関数を通る。

### 1.2 v1 からの実装判断の変更

| # | 判断 | 理由 |
|---|---|---|
| B-1 | v1 の A-7「任意フィールドの型不一致は null 扱い」を **snapshot の 2 フィールドには適用しない** | `allTypeIdentifiers` は必須フィールド（E-11）。`matchingItemIndexes` は null と空の区別が公開契約であり、型不一致を黙って片方へ寄せると呼び出し側に誤情報を返す。他の任意フィールドは従来どおり lenient |
| B-2 | callback 内の assertion をやめ、外側でカウンタを assert | production の `InvokeInOrder` が例外を catch するため、callback 内の `Assert.Fail` はテスト失敗にならない（レビュー指摘のとおり v1 のテストは callback が届いても pass しえた） |

### 1.3 v1 から引き継いだ判断

- `AssemblyInfo.cs` への `InternalsVisibleTo("NativeToolkit.Runtime.PlayModeTests")` 追加（唯一の既存ファイル変更）
- `Awaitable` 版は計画 5.7 のとおり初期実装から除外
- サンプルアプリは `design-sample-scene` の担当

---

## 2. 変更ファイル

### 2.1 新規作成

v1 と同一（Runtime 15 + Tests 6 = 21 ファイル）。今回の修正はすべて既存の新規ファイル内で行った。

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/AssemblyInfo.cs`: `InternalsVisibleTo("NativeToolkit.Runtime.PlayModeTests")` の 1 行追加（v1 の A-1 と同じ。理由は v1 §2.2）

### 2.3 成果物に含める Unity 生成ファイル

- `Plugins/iOS/ios-native-toolkit-1.3.0.xcframework.meta`
- `Plugins/iOS/unity-ios-native-toolkit-1.3.0.xcframework.meta`

いずれも `PluginImporter` を持ち、`Any: 0` / `Editor: 0` / `iOS: 1`。**Unity の `ConfigureIosXcframeworkImporter` が実行され `Import settings updated (iOS only)` をログ出力したことを確認済み**（4 章）。

### 2.4 意図しない変更の解消

- `unity-native-plugin.slnx`: Unity のプロジェクト再生成による行順変更。**復元済み**（`git status` に差分なし）
- 一時作成した `Assets/Editor/TempIosLinkSmokeBuild.cs`（M-25 用の export 入口）と `Build/iOS-linkcheck`（5.7 GB）は**検証後に削除済み**

---

## 3. エラー契約反映

v1 §3 から変更のない部分は省略し、差分のみ記載する。

### 3.1 追加・変更したケース

| ケース | 変更 | テスト |
|---|---|---|
| E-2（parse 不能） | 不正 escape / raw 制御文字 / 不正 number を新たに拒否 | `Parse_InvalidStringEscapes_AreRejected` / `Parse_RawControlCharactersInStrings_AreRejected` / `Parse_MalformedNumbers_AreRejected` |
| E-11（必須フィールド） | `allTypeIdentifiers` の行が array でない場合を追加 | `ParseSnapshotResult_NestedRowThatIsNotAnArray_Fails` |
| B-6 | `matchingItemIndexes` の型不一致を追加 | `ParseSnapshotResult_MatchingItemIndexesOfWrongType_Fails` |

### 3.2 B-0〜B-11 の到達状況（v1 から改善）

| # | コード | v1 | v2 |
|---|---|---|---|
| B-0 | `CLIPBOARD_BUSY` | 状態モデルのみ | **production 経路で検証**（`SecondCall_WhileOnePending_IsRejectedAsBusy` ほか 3 件） |
| B-2 | `DllImport` 例外 | 未到達 | 未到達（Editor では native を呼ばないため。実機のみ） |
| B-11 | `CLIPBOARD_MANAGER_DESTROYED` | 2 操作のみ | **全 15 操作で検証**（`AfterDestroy_AllFifteenOperationsAreRejected`） |

その他（B-1 / B-3 / B-4 / B-5 / B-7 / B-8 / B-9 / B-10）は v1 と同じく検証済み。

### 3.3 success 時契約

v1 から変更なし。`IsSuccess == true` ⇔ `Error == null` を全 9 result 型で検証（21 ケース）。

---

## 4. ビルド結果

### 4.1 コンパイル検証

- `dotnet build`（`UnityEngine.CoreModule.dll` 参照）で `UNITY_EDITOR` 定義時・`UNITY_IOS` 定義時の両方: **0 Warning / 0 Error**
- Unity バッチ実行のログに `error CS` なし

### 4.2 M-25: iOS build / link smoke test（**新規実施・成功**）

実行手順:

1. 一時 Editor スクリプトから `BuildPipeline.BuildPlayer`（`BuildTarget.iOS`、`extraScriptingDefines: NATIVETOOLKIT_ENABLE_BUILD_STEPS`）で Xcode プロジェクトを export
2. `xcodebuild -project Unity-iPhone.xcodeproj -target Unity-iPhone -configuration Release -sdk iphoneos -arch arm64 CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO ENABLE_BITCODE=NO build`

結果: **`** BUILD SUCCEEDED **`**（`error:` / `Undefined symbol` の出力なし）

検証できた内容:

| 確認項目 | 結果 |
|---|---|
| PreBuildProcessor が 1.3.0 を選択・コピー | `Selected XCFramework: ios-native-toolkit-1.3.0.xcframework` / `unity-ios-native-toolkit-1.3.0.xcframework` |
| **`ConfigureIosXcframeworkImporter` が PluginImporter を設定** | `Import settings updated (iOS only)` を両 XCFramework で出力 |
| PostBuildProcessor が Xcode プロジェクトへ追加 | `Added unity-ios-native-toolkit-1.3.0.xcframework to Xcode project.` ほか。`project.pbxproj` に 1.3.0 参照が 12 箇所 |
| IL2CPP が clipboard 関数を参照 | 生成 C++ に 15 関数すべての参照（`NativeToolkit.Runtime__1.cpp` ほか） |
| UnityFramework の未定義シンボル | `clipboard*` が 15 個（P/Invoke の解決対象） |
| 埋め込み XCFramework の定義シンボル | `UnityIosPlugin.framework` に `T _clipboard*` が 15 個 |
| リンク関係 | `otool -L` で `@rpath/UnityIosPlugin.framework/UnityIosPlugin` と `@rpath/IosLibrary.framework/IosLibrary` を確認 |
| 埋め込み Frameworks | `IosLibrary.framework` / `UnityIosPlugin.framework` / `UnityFramework.framework` / `UnityRuntime.framework` |

**未定義シンボル 15 個と定義シンボル 15 個が一致し、リンクが成功している。** これは 1.3.0 XCFramework が Unity import → Xcode export → link まで通ることの実証であり、計画 0 節の残リスク（import 設定・target membership・embedding 起因の失敗）も同時に解消した。

補足: Xcode 26.3 / iPhoneOS 26.2 SDK。署名は無効化しているため**実機インストールと実行は未確認**（M-1〜M-24 は引き続き未実施）。

---

## 5. テスト結果

- 実行コマンド:
  - `Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults <xml>`
  - `同上 -testPlatform PlayMode`
- Unity 6000.4.2f1

| | 実行 | 成功 | 失敗 | スキップ |
|---|---|---|---|---|
| EditMode | 344 | **344** | 0 | 0 |
| PlayMode | 44 | **44** | 0 | 0 |

新規追加分の内訳:

| 層 | ファイル | v1 | v2 |
|---|---|---|---|
| EditMode | `IosClipboardJsonReaderTests` | 27 | **32** |
| EditMode | `IosClipboardJsonBuilderTests` | 30 | 30 |
| EditMode | `IosClipboardJsonParserTests` | 38 | **40** |
| EditMode | `IosClipboardResultTests` | 21 | 21 |
| EditMode | `IosClipboardManagerDispatchTests` | 17 | 17 |
| PlayMode | `IosClipboardManagerIntegrationTests` | 15 | **31** |

**要注意: アクティブビルドターゲットが iOS に変わっているため、`#if UNITY_ANDROID` のテストが今回のランに含まれていない。**

| テスト | 状況 |
|---|---|
| `AndroidClipboardManagerDispatchTests`（EditMode 8 件） | **未実行**（コンパイル対象外）。v1 のラン（ターゲット Android）では 8 件 passed |
| `AndroidClipboardManagerIntegrationTests`（PlayMode 11 件） | **未実行**（同上）。v1 では device-only として 11 件 skipped |

これは `testing.md` 3 節の B 群 Manager の既知の性質であり、実装による退行ではない。Android 側の再確認が必要な場合はビルドターゲットを Android へ戻して再実行する必要がある。

### 5.1 追加したテストの観点

| 観点 | テストケース | 結果 |
|---|---|---|
| JSON escape の厳格化 | `Parse_InvalidStringEscapes_AreRejected`（`\q` / `\x41` / `\u12` / `\u12g4` / 末尾 `\`） | ○ |
| raw 制御文字 | `Parse_RawControlCharactersInStrings_AreRejected`（改行 / タブ / NUL） | ○ |
| 正常な escape | `Parse_ValidEscapesAndSpaces_AreAccepted` | ○ |
| number 文法 | `Parse_MalformedNumbers_AreRejected`（`+1` / `01` / `1.` / `.5` / `1e` / `1e+` / `--1` / `1.2.3` / `-`） | ○ |
| number 正常系 | `Parse_WellFormedNumbers_AreAccepted`（11 パターン） | ○ |
| nested 配列の型不一致 | `ParseSnapshotResult_NestedRowThatIsNotAnArray_Fails` | ○ |
| optional 配列の型不一致 | `ParseSnapshotResult_MatchingItemIndexesOfWrongType_Fails` | ○ |
| **busy が pending を奪わない** | `SecondCall_WhileOnePending_IsRejectedAsBusy`（1 本目の callback / marker が保持され、後から正しい payload が届く） | ○ |
| 連続 busy 拒否 | `BusyRejection_DoesNotStealThePendingCallbackEvenAfterRepeatedRejections` | ○ |
| busy の共通 event | `BusyRejection_StillFiresTheCommonEvent` | ○ |
| 異なる操作の並行 | `DifferentOperations_RunConcurrently` | ○ |
| **rejected 経路の不変条件** | `RejectedCalls_NeverTouchPendingOrInFlightState`（B-3 / B-9 が pending / in-flight を変更しない） | ○ |
| callback からの再開 | `CallbackMayRestartTheSameOperation` | ○ |
| observation 共有キー | `StartObserving_ReportsItsOwnOperationNameOverTheSharedKey` / `StopObserving_WhileStartPending_IsBusyOnTheSharedKey` | ○ |
| **observation 世代** | `FailedStart_ReleasesItsOwnChangeRegistration` / `SuccessfulStart_KeepsItsChangeRegistration` / `StopObserving_ReleasesTheChangeRegistration` / `OlderStopCompletion_DoesNotClearANewerRegistration`（同一 delegate インスタンスを 2 回登録） | ○ |
| change event 配送 | `ChangeEvents_ReachTheCommonEventAndTheRegistration` / `UnparsableChangeEvent_IsDropped` | ○ |
| **全 15 操作の B-11** | `AfterDestroy_AllFifteenOperationsAreRejected` | ○ |
| **late callback の破棄** | `LateNativeResult_AfterDestroy_IsDiscarded` / `LateChangeEvent_AfterDestroy_IsDiscarded` | ○ |
| **複合 lifetime シナリオ** | `OldResult_CannotReachACallStartedAfterRecreation`（旧 A 開始 → Destroy → 再取得 → 新 A（B-11）→ 旧 A callback 後着） | ○ |
| `ResetCore` 全 static | `ResetForTests_ClearsEveryMutableStatic` | ○ |
| dispatcher 破棄 | `DestroyedDispatcher_IsDetectedAndTheResultIsNotSilentlyQueued` | ○ |

### 5.2 失敗と対応

初回 PlayMode ランで `DifferentOperations_RunConcurrently` が 1 件失敗。エラー封筒を使ったため parser の `Debug.LogError` が未 expect だったもの。正常な snapshot 封筒に置き換え、in-flight 解放の assertion を追加して再実行し passed。

### 5.3 未実施ケース

| 観点 | 未実施理由 |
|---|---|
| B-2（`DllImport` 例外） | Editor では native を呼ばないため到達しない。実機で XCFramework 未リンク時のみ発生 |
| M-1〜M-24（実機手動確認） | 実機 iOS 18 以降が必要。**未実施** |
| M-21（ABI 成否両系統） | 同上。`[MarshalAs(UnmanagedType.I1)]` の実機確認は未 |
| M-22 / M-23（大容量メモリ実測） | 実機 Instruments 計測が必要 |
| Android テスト（EditMode 8 / PlayMode 11） | ビルドターゲットが iOS のため今回のランでは未実行（5 章） |
| 計画 9.9（Reload Domain / Scene Reload 両無効構成） | 当該 Editor 設定での実測が必要 |

---

## 6. Definition of Done（計画 v5 の 26 項目）

| # | 項目 | v1 | v2 | 備考 |
|---|---|---|---|---|
| 1 | 1.3.0 XCFramework の import と `.meta` | ○ | ○ | `PluginImporter` を含む正規 meta を確認 |
| 2 | **M-25（iOS build / link smoke test）** | × | **○** | BUILD SUCCEEDED、未定義 15 = 定義 15 |
| 3 | 新規ファイル 21 件 | ○ | ○ | |
| 4 | 既存 C# ファイルへの変更 0 件 | △ | △ | `AssemblyInfo.cs` 1 行（理由は §2.2） |
| 5 | `[MarshalAs(UnmanagedType.I1)]` | ○ | ○ | 実機確認は M-21（未） |
| 6 | single-flight と `CLIPBOARD_BUSY` | ○ | ○ | v2 で production 経路のテストを追加 |
| 7 | rejected 専用 dispatch と不変条件 1〜4 | ○ | ○ | v2 で production 経路のテストを追加 |
| 8 | `XxxAsync` が実装されていない | ○ | ○ | |
| 9 | 誤配送しない | ○ | ○ | v2 で複合シナリオを追加 |
| 10 | main thread 契約と B-9 | ○ | ○ | |
| 11 | 破棄・再生成が非対応、B-11 | ○ | ○ | v2 で全 15 操作を検証 |
| 12 | `OnDestroy` の順序と例外非伝播 | ○ | ○ | |
| 13 | cleanup seam が引数渡し | ○ | ○ | |
| 14 | 全 native コールバックが `DiscardIfTerminated` | ○ | ○ | v2 で late callback を検証 |
| 15 | observation の共有キーと世代判定 | ○ | ○ | v2 で世代テストを追加 |
| 16 | 公開型仕様どおり | ○ | ○ | |
| 17 | E-1〜E-16 と変更イベント破棄規則 | ○ | ○ | v2 で reader の厳格化を反映 |
| 18 | B-0〜B-11 | ○ | ○ | B-2 のみテスト未到達 |
| 19 | `TryGetDecodedLength` と `byteCount` 検証 | ○ | ○ | |
| 20 | response 側メモリ削減策 1〜4 | △ | △ | 実装済み、実機実測は M-22 / M-23 |
| 21 | 層 1 / 層 2a のテスト全 passed | ○ | ○ | EditMode 344 / PlayMode 44 |
| 22 | 複合テスト（旧 callback 後着） | △ | **○** | `OldResult_CannotReachACallStartedAfterRecreation` |
| 23 | 本文・base64・検出値・pasteboard 名を出力しない | ○ | ○ | |
| 24 | `public` メンバの英語 XML コメント | ○ | ○ | |
| 25 | `ResetStaticState` / `ResetForTests` の共通化 | ○ | ○ | v2 で全 static の検証を追加 |
| 26 | M-1〜M-25 の実機確認 | × | △ | **M-25 のみ実施済み**。M-1〜M-24 は未実施 |

**総括: ○ 22 / △ 4 / × 0**（v1 は ○ 21 / △ 3 / × 2）。残る △ 4 件はいずれも実機（層 2b / 層 3）または既知の設計判断に関するもの。

---

## 7. 要確認事項（断定しない）

- 計画 9.1〜9.6 / 9.9 の要検証事項は**いずれも未検証のまま**（実機が必要）
- M-25 は署名を無効化したリンク確認であり、**実機インストール・実行は未確認**
- Android 側テストは今回のビルドターゲットでは実行されていない（5 章）

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
