# Android Share 実装レビュー結果

## レビュー対象

- 日付: 2026-06-21
- ブランチ: `feature/UNT-5`
- diff: ローカル未コミットの Share 実装ファイル
- 実装計画: `artifact/designs/share/2026-06-21-android-share-design-v2.md`
- 実装結果: `artifact/results/share/2026-06-21-android-share-implementation-feature-result-v1.md`
- 対象プラットフォーム: Android

## レビュー概要

- Share Payload、JSON Builder、Result、Android Manager、EditMode テストを追加している。
- Manager + Bridge の基本構造、Unity メインスレッドへの転送、`isSuccess == true` 時の `ErrorMessage == null` は設計に沿っている。
- C# 単体部分は実装されているが、配布 AAR、Android 最小 API、AndroidX 依存が未統合のため、Android アプリでは現状動作しない。

## 重大な問題（high）

### 1. 同梱 AAR に Share 実装が含まれていない

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/android-native-toolkit-1.1.0.aar`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/unity-android-native-toolkit-1.1.0.aar`
- 関連コード: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs:92`
- `classes.jar` を確認したところ、両 AAR とも `/share/` クラスを含んでいない。
- `AndroidShareManager.Initialize()` が要求する `android.unity.share.UnityAndroidShareManager` が存在しないため、初期化時にクラス解決で失敗する。
- FileProvider 対応だけでなく Share domain 実装もないため、画像・ファイル以外の Share API も利用できない。
- 設計書の「`unity-android-native-toolkit` AAR は差し替え対象外」という記述は実物と不一致。
- 修正: `/Users/jonghyunkim/Desktop/native-toolkit` の最新ソースから `android_library` と `unity_android_plugin` の両 AAR を再ビルドして差し替え、各 `classes.jar` と Android library の manifest / paths リソースを検証する。

### 2. Unity Player の Minimum API Level が AAR 要件を満たしていない

- 対象: `ProjectSettings/ProjectSettings.asset:177`
- 現在は `AndroidMinSdkVersion: 23` だが、native-toolkit AAR の `minSdkVersion` とプロジェクトルールの最小 Android バージョンは API 31。
- 更新済み AAR を入れると manifest merge で失敗するか、サポート外構成になる。
- 実装結果は EditMode テストのみで、Android build を実行していないため検出されていない。
- 修正: Android Minimum API Level を 31 に更新し、実際の Android Gradle build を通す。

### 3. FileProvider が必要とする AndroidX 依存を解決していない

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Editor/Build/PostBuildProcessor.cs:438`
- 現在の Gradle patch は Kotlin stdlib / reflect のみ追加し、`androidx.core` など native-toolkit AAR の依存を追加しない。
- `Dependencies.xml`、Gradle template、生成済み Gradle プロジェクトにも AndroidX 依存宣言がない。
- 更新済み AAR の manifest は `androidx.core.content.FileProvider` をアプリ起動時に生成するため、依存がなければ provider のクラス解決でビルドまたは起動に失敗する。
- 修正: native-toolkit の version catalog と整合する AndroidX 依存を自動導入し、利用側の追加作業なしで Android build・起動できることを確認する。

## 改善提案（medium）

### 1. 同一 operation の per-call callback が別呼び出しの結果を受け取る

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs:168`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs:236`
- callback は operation 名だけをキーに保存され、結果処理は `UnityMainThreadDispatcher` へ遅延される。
- 1件目の結果を enqueue した後、処理前に同じ operation を再度呼ぶと、1件目の結果が2件目の callback を取り出して削除する。1件目失敗・2件目成功のような場合に誤った結果を通知する。
- 修正: 同一 operation の並行呼び出しを禁止して明示的に failure を返すか、呼び出し単位のキュー / ID で結果と callback を対応付ける。設計にある連続呼び出しテストも追加する。

### 2. `shareWithCallback` 起動失敗時に選択 callback が残る

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs:155`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs:170`
- `_pendingShareSelectedCallback` は native 呼び出し前に設定されるが、準備失敗、`Call` 例外、native の起動失敗通知で解除されない。
- 選択結果が来ない失敗経路で delegate と捕捉オブジェクトを次回呼び出しまたは破棄まで保持する。
- 修正: `shareWithCallback` の起動失敗が確定した時点で、該当呼び出しの selected callback を解除する。古い失敗通知が新しい呼び出しを消さないよう呼び出し世代も管理する。

### 3. 新規公開 API が C# コーディングルールに適合していない

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs:11`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareJsonBuilder.cs:9`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidSharePayloads.cs:8`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareOperationResult.cs:5`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareCallbackResult.cs:5`
- `csharp.md` が要求する public 型・メソッド・非自明なプロパティの英語 XML コメントがない。
- Manager の public メソッドログが `payload` / callback 引数を含まず、全パラメータを記録するルールに違反する（例: `AndroidShareManager.cs:107`）。
- `AndroidShareManager.cs:288` のコードコメントが日本語で、コメントは英語というルールに違反する。
- 修正: 既存 Notification API と同水準の XML コメント、引数付きログ、英語コメントへ統一する。

## 軽微な指摘（low）

### 1. JSON テストが部分一致のみ

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidShareJsonBuilderTests.cs:11`
- 大部分が `StringAssert.Contains` のため、余分なキー、重複、壊れた全体構造を見逃せる。
- 期待 JSON 全体との一致、空配列、制御文字、null 配列などの境界値を追加すると回帰検出力が上がる。

### 2. 実装結果に含まれない未追跡ファイルがある

- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/com.jonghyunkim.nativetoolkit.androidlib.meta`
- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/VERSION.txt.meta`
- Share 実装と無関係な生成物であれば、コミット対象から除外する必要がある。

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: ×（両 AAR の差し替えが必要）
- テスト方針の網羅性: △（EditMode は実装、Android build / 実機 / callback 連続呼び出しは未実施）
- エラーケース全実装: △（Bridge failure は実装、callback 状態の失敗時解除が不足）
- 返却仕様との整合: ○（`IsSuccess == true` 時の `ErrorMessage == null` を保証）

## プロジェクトルール適合チェック

- common.md 準拠: △（Bridge 構造は準拠、Android 12 最小要件と実機確認が未達）
- csharp.md 準拠: ×（XML コメント、ログ引数、英語コメントが不足）
- Bridge 実装品質（スレッド安全性・メモリ管理）: △（メインスレッド転送は準拠、callback の世代・失敗時管理に問題）
- 既存 API 互換性: ○（既存公開 API の破壊的変更なし）

## テストカバレッジ

- カバー済み: Payload の主要 JSON 出力、optional 項目、文字列エスケープ、配列、Result の成功・失敗契約。
- 未カバー: Manager 初期化、Java listener 登録、共通イベントとper-call callbackの順序、同一operation連続呼び出し、起動失敗後のcallback解放、破棄処理。
- 未実施: 更新済み両 AAR を使った Android Gradle build、manifest / resource merge、AndroidX解決、API 31実機での全Share操作。
- 実装結果では57件成功と報告されている。レビュー時の再実行はUnity Licensing Clientのprotocol不一致で開始できず、独立再確認は未完了。

## 総合評価

**要修正（重大）**

C# Bridge の骨格とEditMode対象は整っているが、現在の配布AARにはShare実装がなく、Android build要件も満たしていない。両AARの更新、API 31設定、AndroidX依存解決、Android build確認を完了するまで機能としてリリースできない。
