# レビュー結果

- 日付: 2026-07-05
- 対象ファイル: `artifact/designs/share/2026-07-05-ios-share-design-v2.md`
- 機能名: share
- プラットフォーム: ios
- レビュー種別: 再レビュー

---

## 強み

- v1 レビューの高優先指摘だった非 iOS / Editor 時の無応答問題が、即時 Failure 方針として明確化されている。
- `IosShareManager` の platform guard 方針が説明され、Editor 参照と native 呼び出し可否を分けて扱う意図が読み取れる。
- `payload` を `IosShareContentPayload?` に変更し、null-safe logging と defensive guard を明示したことで nullable 契約のずれが解消されている。
- builder の null item / empty value 契約が固定され、EditMode テスト項目にも反映されている。
- dispatch 順序を `InvokeInOrder` に切り出して EditMode 検証する方針が追加され、前回不足していた Manager callback 検証への道筋ができている。
- XML コメント要件と `_shareContent` の再現可能な確認コマンドが追記されている。

## 改善点

### 高優先度

- セクション: `4.2 新規作成（Tests）` / `5.4 IosShareManager` / `7.1 EditMode`
  - 問題点: `IosShareManager.cs` は `#if UNITY_IOS` 専用だが、`IosShareManagerDispatchTests.cs` は `#if UNITY_IOS || UNITY_EDITOR` と計画されている。通常の Editor テスト実行で iOS build target ではない場合、`UNITY_EDITOR` は true なのでテストコードはコンパイル対象になる一方、`IosShareManager` 型は存在せずコンパイルエラーになる可能性が高い。
  - severity: high
  - 改善提案: `IosShareManagerDispatchTests.cs` の guard は `#if UNITY_IOS` に揃える。Editor で常に dispatch テストを動かしたいなら、`IosShareManager` 側も `#if UNITY_IOS || UNITY_EDITOR` に変更し、native `DllImport` 呼び出し部分だけ `UNITY_IOS && !UNITY_EDITOR` で閉じる設計にする。現 v2 の「Manager は `#if UNITY_IOS`」方針を維持するなら、テスト側も `UNITY_EDITOR` 単独では参照しない方が安全。

### 中優先度

- セクション: `2.2 再利用・非再利用の判断` / `5.4 結果 dispatch`
  - 問題点: `2.2` では「asmdef の変更は不要」としているが、`InvokeInOrder` を `internal static` にしてテストから直接検証するには `InternalsVisibleTo` または asmdef の friend assembly 設定が必要と書かれている。現状の `NativeToolkit.Runtime.asmdef` にはその設定がないため、変更ファイル一覧と設計判断が矛盾している。
  - severity: medium
  - 改善提案: `Runtime/NativeToolkit.Runtime.asmdef` を既存変更に追加し、テスト assembly `NativeToolkit.Runtime.Tests` への internal 可視化を明記する。別案として `InvokeInOrder` を public にしない限り、asmdef または assembly attribute 用ファイルの変更は必要になる。

- セクション: `5.3 IosShareJsonBuilder` / `7.1 EditMode`
  - 問題点: `items` 内の null 要素を正式に許容する方針になったが、`IosShareContentPayload.items` の型は `IosShareItem[]` のまま。`#nullable enable` 環境では、テストで null 要素を入れる際に nullable 警告または null-forgiving が必要になる。
  - severity: medium
  - 改善提案: null 混入を API として許容するなら `IosShareItem?[] items` にするか、「外部/Unity serialization 由来の不正状態として defensive に skip する。通常コードでは `null!` を使ったテストで固定する」と明記する。

### 低優先度

- セクション: `5.6 依存関係の実装順序`
  - 問題点: v2 で `IosShareManagerDispatchTests` が追加されたが、依存関係の実装順序は v1 のまま `IosShareJsonBuilderTests` と `ShareResultTests` への追加だけを挙げている。
  - severity: low
  - 改善提案: 手順 5 を `IosShareJsonBuilderTests`、`IosShareManagerDispatchTests`、`ShareResultTests` への追加に更新する。

- セクション: `4.5 別途対応`
  - 問題点: `_shareContent` の期待アドレス `00000000000065c0` はビルドやバイナリ差し替えで変わり得るため、固定値として残すと将来の確認で不要な差分扱いになりやすい。
  - severity: low
  - 改善提案: 期待値は `T _shareContent` の存在確認に留め、アドレスは例示扱いにする。

## 不足項目

- `IosShareManagerDispatchTests` と `IosShareManager` の compile guard 整合。
- `InvokeInOrder` を internal 検証するための asmdef / InternalsVisibleTo 変更ファイルの明記。
- null item 許容に対する `IosShareItem[]` 型の扱い。
- v2 追加テストを含めた実装順序の更新。

## 総合評価

v2 は前回の主要指摘をかなりきれいに反映できています。非 iOS 時の即時 Failure、nullable payload、builder 入力契約、Manager dispatch テスト方針が明文化され、実装計画としての完成度は上がりました。

残る主なリスクは、`#if UNITY_IOS` の Manager と `#if UNITY_IOS || UNITY_EDITOR` の dispatch tests の不整合です。ここは実装時にコンパイルを止める可能性があるため、v2 内で guard 方針を揃えてから実装へ進むのが安全です。
