# レビュー結果

- 日付: 2026-06-21
- 対象ファイル: `artifact/designs/share/2026-06-21-android-share-design-v1.md`
- 機能名: share
- プラットフォーム: Android

---

## 強み

- `AndroidJavaObject` と `AndroidJavaProxy` を使う方針は、既存の `AndroidNotificationManager` と native-toolkit の Singleton / listener 方式に沿っている。
- C# 側の責務を Payload、JsonBuilder、Result、Manager に分け、Sharesheet、Direct Share、FileProvider 処理をネイティブ側に委ねる境界が明確である。
- Android メインスレッドと Unity メインスレッドを区別し、すべてのイベントを `UnityMainThreadDispatcher` 経由で通知する契約が明示されている。
- `pluginInstance`、`currentActivity`、`AndroidJavaProxy` のライフサイクルと所有権が具体的に記述されている。
- サンプルシーンを別設計に分離しており、Bridge 実装の範囲が明確である。

## 改善点

### 高優先度

#### `shareWithCallback` の通知契約を実装に合わせる

- 問題点: native 実装の `executeOperation` は、`shareWithCallback` の起動成功時にも `onShareOperation("shareWithCallback", true, null)` を通知し、その後アプリが選択された場合に `onShareResult` を追加通知する。設計書には、成功時は `onShareResult` のみ通知されるように読める記述がある。
- 改善提案: 契約を「起動成否は `ShareOperationCompleted`、選択結果は `ShareCallbackReceived` の追加通知」に統一する。成功時の二段階通知と、キャンセル、Copy、Edit では `onShareResult` が通知されないケースをイベント説明と手動テストに追加する。

#### FileProvider の配置責任を確定する

- 問題点: 同梱されている `android-native-toolkit-1.1.0.aar` と `unity-android-native-toolkit-1.1.0.aar` の manifest には FileProvider 宣言がない。native-toolkit の Example アプリだけが host manifest と `res/xml/file_paths.xml` を提供しているため、現状のパッケージでは画像、ファイル、preview thumbnail の共有前提が満たされない。
- 改善提案: FileProvider を「要検証」ではなく必須前提として扱い、本パッケージに manifest 断片と `file_paths.xml` を同梱するか、利用側の必須設定にするかを設計段階で決定する。決定したファイルとドキュメントを変更ファイル一覧に追加する。

### 中優先度

#### Unity から観測されるエラー契約を修正する

- 問題点: 空の `text`、`ids`、`filePaths` や必須フィールド欠落は、Share use case より前に JSON parser の `IllegalArgumentException` または `JSONException` になる。そのため、表に記載された個別の `ShareDomainError` 文言ではなく `Failed to {operation}: ...` が返るケースがある。
- 改善提案: エラーを parser、use case、repository、C# Bridge の各層に分け、Unity が実際に受け取る文言を操作別に整理する。「native が返す全ケース」という表現も、到達可能なケースに限定する。

#### テスト関連ファイルと Manager の検証方法を具体化する

- 問題点: `AndroidShareJsonBuilder` と Result の EditMode テストを作成する方針がある一方、テストファイルが変更ファイル一覧にない。また、既存テスト asmdef は Editor 限定であり、`#if UNITY_ANDROID` の Manager 初期化、listener、イベント転送をどの環境で自動検証するかが定義されていない。
- 改善提案: `Tests/Runtime/AndroidShareJsonBuilderTests.cs` と Result テスト、および各 `.meta` を変更一覧へ追加する。Manager はテスト可能な処理を EditMode 対象として切り出すか、Android Player テストと実機手動確認へ寄せる範囲を明記する。

#### 非 Android 時の失敗通知を既存 Manager と統一する

- 問題点: セクション 3.2 は準備失敗時に `ShareOperationCompleted` を発火する設計だが、セクション 4 は非 Android 時にイベントを発火しない方針としており、文書内で矛盾している。既存の `AndroidNotificationManager` は非 Android 時も `Failure(operation, "{operation} could not be started.")` を通知する。
- 改善提案: 非 Android、未初期化、`currentActivity == null` のすべてを既存 Manager と同じ failure イベントに統一し、未確定事項から削除する。

### 低優先度

- なし。

## 不足項目

- API 34+ の `chooserActions` で `intentAction` を受けるアプリ内 receiver または activity の配置責任。同梱 AAR に汎用 receiver は含まれていない。
- `file_paths.xml` の具体的な公開範囲。native-toolkit Example は `files-path`、`cache-path`、`external-files-path` を公開しているため、Unity の `persistentDataPath` と `temporaryCachePath` がどこに対応するかを明記する必要がある。
- `shareWithCallback` でキャンセル、Copy、Edit 時に `onShareResult` が通知されない場合の状態遷移と UI 側の扱い。
- IL2CPP / AOT では `AndroidJavaProxy` のため `MonoPInvokeCallback` は不要だが、Java から呼ばれる proxy メソッドを `public` にし、メソッド名とシグネチャを正確に一致させる制約。
- Unity が生成する `Share/` フォルダー、各 C# ファイル、各テストファイルの `.meta` を含む完全な変更ファイル一覧。

## 総合評価

Manager + Bridge の骨格、責務分割、スレッド・メモリ契約は明確で、実装へ進むための基礎は整っている。一方、`shareWithCallback` の実際の二段階通知、FileProvider の必須配置、parser を含む実際のエラー契約、テスト資産の変更一覧には native 実装との差分がある。特に高優先度の2点は機能の公開契約とファイル共有の成否に直接影響するため、実装開始前に設計へ反映すべきである。
