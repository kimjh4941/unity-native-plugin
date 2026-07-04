# レビュー結果

- 日付: 2026-06-21
- 対象ファイル: `artifact/designs/share/2026-06-21-android-share-sample-scene-design-v1.md`
- 機能名: share
- プラットフォーム: Android

---

## 強み

- `AndroidShareManager` の全公開操作をカテゴリ別ボタンへ対応付けており、機能一覧の網羅性が高い。
- 実装結果由来の公開 API、入力制約、エラー契約、未実施確認をセクション1へ分離している。
- 既存の `NativeToolkitSampleNavigator`、TopMenu、Notification サンプルの UXML / USS / Controller パターンを具体的に再利用している。
- 画像とファイルを `Application.temporaryCachePath` に生成する方針は、FileProvider の `cache-path` と整合する。
- `shareWithCallback` の起動結果と選択結果を二段階で扱い、キャンセル時に選択通知が来ない正常系も手動確認へ含めている。

## 改善点

### 高優先度

#### 実装レビューの重大な前提不足を反映する

- 対象セクション: 1.5、4.3、7
- 問題点: 計画は「AAR 差し替え後」とだけ記載しているが、実装レビューでは両 AAR に Share クラスがないこと、Minimum API Level が23のままであること、AndroidX依存が未解決であることが重大問題として確定している。現在の状態ではサンプル画面を実装しても全操作を実機確認できない。
- 改善提案: `artifact/reviews/share/2026-06-21-android-share-implementation-feature-review-v1.md` を前提資料に追加し、次をサンプル実装開始条件として明記する。
  - `android_library` と `unity_android_plugin` の両 AAR差し替え
  - Android Minimum API Level 31
  - AndroidX依存の自動解決
  - Android Gradle buildとmanifest/resource merge成功
  - Manager callback管理の重大・中優先度修正

#### 動作しない Custom Action を機能一覧に含めない

- 対象セクション: 1.5、3.1、6.1、6.4、7、8
- 問題点: `Share with Custom Action` ボタンを提供する一方、`intentAction` を受信する BroadcastReceiver とmanifest宣言をスコープ外にしている。ChooserAction自体が表示されても、押下後の動作と結果表示を完結できず、サンプル機能として不完全になる。
- 改善提案: v1ではボタンを削除するか、Receiver実装・manifest登録・受信結果表示まで変更一覧へ含める。表示だけを確認する場合は操作不能なデモであることをUI上でも明示し、通常機能と分離する。

### 中優先度

#### イベント購読を OnEnable / OnDisable で管理する

- 対象セクション: 2.1、5.1、5.2、6.2、8
- 問題点: `design-sample-scene` workflow は Managerイベントを `OnEnable` / `OnDisable` で管理するよう必須指定しているが、計画は既存コードを理由に `Start` / `OnDestroy` を採用している。Controllerが無効化されたまま残る場合にもイベントを受信し続ける。
- 改善提案: UI初期化は `Start`、Managerイベント購読・解除は `OnEnable` / `OnDisable` に分離する。pending callbackのcancelは `OnDisable` または `OnDestroy` の責務として明確化する。

#### Editor導線と手動確認内容を一致させる

- 対象セクション: 3.3、6.5、7
- 問題点: TopMenuのEditor分岐は `EditorUtility.DisplayDialog` を表示する計画なのでShare画面へ遷移しない。一方、手動確認ではEditorでControllerの `Android device only.` 表示を確認するとしており、同じ導線では到達できない。
- 改善提案: Editorでも `ShowAndroidShare` へ遷移してUIプレビューを可能にするか、Editor確認項目をTopMenuダイアログ確認へ変更する。

#### 共通イベントと per-call callback の両発火を識別可能にする

- 対象セクション: 3.2、5.1、6.2、7
- 問題点: `ShareCallbackReceived` と `onSelected` の両方が単一 `ResultTextBlock` を更新すると、後の更新が前の結果を上書きし、両経路が発火したことを画面から検証できない。
- 改善提案: 経路名を付けて履歴を追記する、別Labelを用意する、または一方を画面・他方を明示的なログで確認するなど、二経路を識別できる設計にする。

#### Texture2D の破棄契約を追加する

- 対象セクション: 6.3
- 問題点: サンプル画像とBase64アイコン生成で作成する `Texture2D` の破棄が記載されていない。ボタンを繰り返し押すとnative textureメモリが蓄積する。
- 改善提案: `try/finally` で `UnityEngine.Object.Destroy(texture)` を実行し、PNG byte配列を得た後に確実に解放する。

### 低優先度

#### Direct Share の確認条件を端末依存として扱う

- 対象セクション: 7
- 問題点: 「システムの共有メニューにターゲットが現れる」はlauncher、共有先、OS実装に依存し、登録成功でも必ず表示されるとは限らない。
- 改善提案: Managerの登録成功を必須判定とし、Sharesheetへの表示は対応端末での要検証項目として分ける。

## 不足項目

- 実装レビュー結果を参照資料および開始条件として扱う記述。
- `ErrorMessage` 表示を実際に確認するための異常系操作。存在しないファイルまたはFileProvider範囲外パスを1件用意すると確認しやすい。
- 共通イベントとper-call callbackがそれぞれ発火したことを識別する表示またはログ仕様。
- サンプル生成ファイルの上書き・削除方針と `Texture2D` の解放方針。
- Direct Shareで登録・削除に同じ固定IDを使うことの明示。

## 総合評価

API網羅性、既存UIとの整合、ファイル生成、結果表示の基本設計は良好で、サンプル画面の構成は具体的である。ただし、基盤実装レビューで判明した重大問題が開始条件へ反映されておらず、Custom Actionも受信処理なしでは機能として完結しない。まず実装基盤の修正条件を取り込み、Custom Actionの扱いと購読ライフサイクルを確定してからサンプル実装へ進むべきである。
