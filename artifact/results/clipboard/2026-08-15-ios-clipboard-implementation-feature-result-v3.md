# 実装結果レポート v3

## 基本情報

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- ブランチ: feature/UNT-9
- 基準計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 前版: `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v2.md`
- 対象レビュー: `artifact/reviews/clipboard/2026-08-15-ios-clipboard-implementation-feature-review-v2.md`（総合評価「要修正（軽微）」）

---

## 0. v2 レビュー指摘への対応

| # | 指摘 | severity | 対応 | 状態 |
|---|---|---|---|---|
| M-1 | snapshot の配列**内要素**の型不一致が除外され成功扱いになる（`ReadStringList` が非 string を、`matchingItemIndexes` の loop が非 int を黙って落とす） | medium | snapshot 専用の strict reader を新設し、要素が 1 つでも期待型でなければ B-6。要素型不一致のテストを 4 種追加 | 解消 |
| L-1 | active build target が Android に戻されておらず、Android テストが未実行 | low | **Android へ復元し、EditMode / PlayMode を全件再実行**（3 章） | 解消 |

---

## 1. 実装サマリー（v2 からの差分のみ）

### 1.1 snapshot 配列の要素型を厳格化

`IosClipboardJsonParser.cs` に snapshot 専用の strict reader を追加した。

| 追加 | 適用先 |
|---|---|
| `TryReadStrictStringList` | `typeIdentifiers`、`allTypeIdentifiers` の各行 |
| `TryReadStrictIntList` | `matchingItemIndexes` |

いずれも要素が 1 つでも期待型でなければ `false` を返し、呼び出し側が `IosClipboardSnapshotResult.Failure(MalformedResponse())` を返す。

**lenient な `ReadStringList` は残し、適用先を明示的に限定した**（コメントにも記載）。

| reader | 適用先 | 方針 |
|---|---|---|
| strict | snapshot の 3 配列 | 必須フィールドのため、要素型不一致は E-11 として全体を失敗 |
| lenient | `read` items の `typeIdentifiers`、`detectValues` の `links`、変更イベントの `typesAdded` / `typesRemoved` | 任意 / ベストエフォートのため要素単位でスキップ（設計 5.5.1 が要素単位スキップを認める範囲） |

これで「既定値で成功を合成しない」契約が snapshot の内側配列にも及ぶ。v2 では `["public.text", 7]` が 1 要素の成功値へ縮退していた。

### 1.2 コード以外の変更

なし。v2 で追加した Editor-only seam、dispatcher 判定、JSON reader の厳格化はそのまま。

---

## 2. 変更ファイル

### 2.1 新規作成

v1 / v2 と同一（Runtime 15 + Tests 6 = 21 ファイル）。今回の修正は `IosClipboardJsonParser.cs` と `IosClipboardJsonParserTests.cs` の 2 ファイル内で完結。

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/AssemblyInfo.cs`: `InternalsVisibleTo("NativeToolkit.Runtime.PlayModeTests")` の 1 行追加（v1 §2.2 の理由）

**これが唯一の tracked な変更である**（`git status` で確認済み）。

### 2.3 workspace 状態の復元

| 項目 | 状態 |
|---|---|
| active build target | **Android へ復元済み**（`NativeToolkit.Runtime.Tests.rsp` に `define:UNITY_ANDROID` を確認） |
| `unity-native-plugin.slnx` | Unity の再生成による行順変更を復元済み（差分なし） |
| M-25 用の一時 Editor スクリプト / `Build/iOS-linkcheck` | v2 で削除済み |
| `git diff --check` | 問題なし |

---

## 3. テスト結果（Android target で全件再実行）

- 実行コマンド:
  - `Unity -batchmode -nographics -projectPath . -buildTarget Android -runTests -testPlatform EditMode -testResults <xml>`
  - `同上 -testPlatform PlayMode`
- Unity 6000.4.2f1

| | 実行 | 成功 | 失敗 | スキップ |
|---|---|---|---|---|
| EditMode | 356 | **356** | 0 | 0 |
| PlayMode | 55 | **44** | 0 | 11 |

PlayMode の skip 11 件はすべて `AndroidClipboardManagerIntegrationTests`（device-only、既定どおり）。

### 3.1 クリップボード関連の内訳

| ファイル | 件数 | 結果 |
|---|---|---|
| `AndroidClipboardJsonBuilderTests` | 16 | Passed |
| `AndroidClipboardJsonParserTests` | 31 | Passed |
| **`AndroidClipboardManagerDispatchTests`** | **8** | **Passed**（v2 のランでは未実行） |
| `AndroidClipboardSampleSceneWiringTests` | 7 | Passed |
| `AndroidClipboardManagerIntegrationTests`（PlayMode） | 11 | Skipped（device-only） |
| `IosClipboardJsonReaderTests` | 32 | Passed |
| `IosClipboardJsonBuilderTests` | 30 | Passed |
| `IosClipboardJsonParserTests` | **44** | Passed（v2 の 40 から +4） |
| `IosClipboardResultTests` | 21 | Passed |
| `IosClipboardManagerDispatchTests` | 17 | Passed |
| `IosClipboardManagerIntegrationTests`（PlayMode） | 31 | Passed |

**Android の既存テストに退行はない。** iOS の C# は `UNITY_IOS || UNITY_EDITOR` ガードのため Android target でもコンパイル・実行される。

### 3.2 追加したテスト

| 観点 | テストケース | 結果 |
|---|---|---|
| snapshot の string 配列の要素型 | `ParseSnapshotResult_NonStringElementInAnyTypeArray_Fails`（`typeIdentifiers` に数値 / null、`allTypeIdentifiers` の行に数値 / オブジェクト） | ○ |
| `matchingItemIndexes` の要素型 | `ParseSnapshotResult_NonIntegerElementInMatchingItemIndexes_Fails`（文字列 / null / 小数 / 配列） | ○ |
| 正常系の非退行 | `ParseSnapshotResult_WellFormedArrays_StillSucceed` | ○ |
| lenient 方針の境界 | `ParseReadResult_NonStringTypeIdentifier_IsStillSkipped`（`read` items は従来どおりスキップ） | ○ |

最後の 1 件は「strict 化を snapshot に限定した」ことを固定するためのもので、設計 5.5.1 の要素単位スキップの適用範囲が変わっていないことを示す。

### 3.3 未実施ケース（v2 から変更なし）

| 観点 | 未実施理由 |
|---|---|
| B-2（`DllImport` 例外） | Editor では native を呼ばないため到達しない |
| M-1〜M-24（実機手動確認） | 実機 iOS 18 以降が必要。**未実施** |
| 計画 9.1〜9.6 / 9.9 の要検証事項 | 実機・特定 Editor 設定が必要 |

M-25（iOS build / link smoke test）は v2 で実施済み（`** BUILD SUCCEEDED **`、未定義 15 = 定義 15）。今回のコード変更は parser のみで、リンク面に影響しない。

---

## 4. ビルド結果

- `dotnet build`（`UnityEngine.CoreModule.dll` 参照）で `UNITY_EDITOR` 定義時・`UNITY_IOS` 定義時の両方: **0 Warning / 0 Error**
- Unity バッチ実行のログに `error CS` なし
- M-25: v2 で実施済み（再実行なし。今回の差分は parser 内に閉じており、P/Invoke 宣言・XCFramework・ビルド処理に変更なし）

---

## 5. Definition of Done（計画 v5 の 26 項目）

| # | 項目 | v2 | v3 |
|---|---|---|---|
| 17 | E-1〜E-16 と変更イベント破棄規則 | ○ | ○（snapshot の要素型まで E-11 を適用） |
| 21 | 層 1 / 層 2a のテスト全 passed | ○ | ○（**Android target で 356 / 44、Android 8 件を含む**） |
| その他 24 項目 | v2 と同一 | | |

**総括: ○ 22 / △ 4 / × 0。** △ 4 件の内訳は v2 と同じで、いずれも実機（層 2b / 層 3）または既知の設計判断:

- #4 既存ファイル変更 0 件（`AssemblyInfo.cs` の 1 行）
- #20 response 側メモリの実機実測（M-22 / M-23）
- #26 M-1〜M-24 の実機確認
- #18 B-2 のテスト到達（実機のみ）

---

## 6. 要確認事項（断定しない）

- 計画 9.1〜9.6 / 9.9 の要検証事項は未検証
- M-25 は署名を無効化したリンク確認であり、実機インストール・実行は未確認

---

## 7. ステップ9 実行確認

- 提示文:
  - 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して終了 → review-implementation-feature スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答
