# サンプルシーン実装結果レポート

## 基本情報

- 日付: 2026-07-05
- 機能名: share
- 対象プラットフォーム: iOS
- ブランチ: feature/UNT-6

参照計画書: `artifact/designs/share/2026-07-05-ios-share-sample-scene-design-v2.md`

## 1. 実装サマリー

### 1.1 計画書由来の実装

- `IosShareManagerExampleController`（`Runtime/UI/iOS/Share/`）を新規作成し、計画書 §2.1 の
  セクション構成（Text / URL / Image / File / Combination / Error Handling）どおりにボタンを配置。
- UXML（`IosShareManagerExample.uxml`）/ USS（`IosShareManagerExampleStyle.uss`）を計画書 §5.1・§5.7の
  とおり、iOS Notification USS（`ios-notif-*`）をベースに `ios-share-*` クラスとして新規作成。
- 各操作の payload 構築は計画書 §5.2 のマッピングをそのまま反映（Text/URL/URL+Preview/Image/
  MultipleImages/File/MultipleFiles/Multiple/WithSubject/ExcludingActivities/Empty/InvalidURL/
  MissingFile/MissingImage）。
- サンプル画像/ファイル生成は計画書 §5.3 のとおり `AndroidShareManagerExampleController` の
  `CreateSampleImage` / `CreateSampleTextFile` をそのまま踏襲（`persistentDataPath` へ書き出し）。
- Manager 呼び出し・イベント購読は計画書 §5.4 のとおり実装:
  - `OnEnable`/`OnDisable` で `IosShareManager.Instance.ShareCompleted` を購読/解除。
  - 各ボタンハンドラは `SetPendingOperation(title)` → payload 構築 → `Share(payload)`（per-call
    callback は使用せず、共通イベント一元化）。
  - `OnShareCompleted` で `_pendingOperationTitle` を用いて結果文言を組み立て、`ResultTextBlock` 更新。
- Navigator / TopMenu 変更は計画書 §5.6 のコード例をそのまま反映:
  - `NativeToolkitSampleNavigator.ShowIosShare` を追加。
  - `RemoveExistingControllers` の iOS ブロックに `IosShareManagerExampleController` の除去を追加。
  - `TopMenuExampleController.OnShareClicked` を `#if UNITY_EDITOR && UNITY_IOS` / `#elif UNITY_EDITOR`
    / `#elif UNITY_ANDROID` / `#elif UNITY_IOS` の4分岐に変更（v2 で確定した Editor ビルドターゲット
    分岐方針どおり）。
  - Share ボタン表示ガードを `#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR` に拡張。

### 1.2 実装時の追加判断

- 計画書のコード例・設計方針をそのまま反映し、追加のロジック判断は発生しなかった。
- Share ボタン非表示時のログメッセージ文言を「Android and iOS」に更新した（従来「Android のみ」の
  文言だったが、iOS も対応対象になったため実態に合わせて修正。計画書に明記はないが、既存パターンの
  ログ文言を実態に追従させる軽微な追加判断）。

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/iOS/Share/IosShareManagerExampleController.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Share/IosShareManagerExample.uxml`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Share/IosShareManagerExampleStyle.uss`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs`:
  `ShowIosShare` メソッド追加、`RemoveExistingControllers` に `IosShareManagerExampleController` の
  除去を追加。
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs`:
  `OnShareClicked` の分岐拡張、Share ボタン表示ガードの拡張。

### 2.3 非変更（対象だが未変更）

- `Assets/Samples/Native Toolkit/1.0.0/Native Toolkit Example/NativeToolkitExampleScene.unity`:
  計画書どおり、TopMenu 起点の動的 AddComponent 構成のため変更不要。
- `Assets/Resources/Images/share_sample_image.png`: 既存リソースをそのまま流用。
- `Runtime/NativeToolkit.Runtime.asmdef`: 既存 Runtime asmdef 配下のため変更不要。

## 3. 実装したサンプル機能

| セクション | ボタン | payload |
| --------- | ------ | ------- |
| Text | Share Text | items=[Text("Shared from Unity Native Toolkit")] |
| URL | Share URL | items=[Url("https://unity.com")] |
| URL | Share URL with Preview | items=[Url] + previewTitle="Unity" |
| Image | Share Image | items=[Image(生成パス)] |
| Image | Share Multiple Images | items=[Image, Image]（2枚生成） |
| File | Share File | items=[File(生成パス)] |
| File | Share Multiple Files | items=[File, File]（2ファイル生成） |
| Combination | Share Text + URL | items=[Text, Url] |
| Combination | Share with Subject | items=[Text] + subject="Sample Subject" |
| Combination | Share Excluding Activities | items=[Url] + excludedActivityTypes=[CopyToPasteboard, PostToFacebook] |
| Error Handling | Share Empty (error) | items=[]（`"No shareable items were provided."`） |
| Error Handling | Share Invalid URL | items=[Url("not a valid url")] |
| Error Handling | Share Missing File | items=[File("/nonexistent/share-missing.txt")] |
| Error Handling | Share Missing Image | items=[Image("/nonexistent/share-missing.png")] |

- ヘッダ: Back To Home ボタン、タイトル、サブタイトル、結果表示領域（`ResultTextBlock`）。
- 結果表示: 成功（`completed=true, activityType=...`）/ キャンセル（`completed=false (cancelled)`）/
  失敗（`Error: {ErrorMessage}`）を判別して表示。

## 4. 共通実装パターンの維持・拡張

### 4.1 維持した点

- TopMenu → Navigator → ExampleController の導線パターン。
- ヘッダ（Back To Home / タイトル / サブタイトル / 結果表示ボーダー）+ ScrollView + セクション構成。
- `OnEnable`/`OnDisable` でのイベント購読ライフサイクル管理（`AndroidShareManagerExampleController` 準拠）。
- 全メソッド先頭の `Debug.Log`（`csharp.md` 準拠）。
- `OnDestroy` でのボタン購読解除。

### 4.2 拡張した点（計画書 §4.2 どおり）

- 結果表示を共通イベント `ShareCompleted` に一元化し、per-call callback は使用しない
  （`_pendingOperationTitle` パターンで直前操作ラベルを保持）。
- ExampleController 全体を `#if UNITY_IOS || UNITY_EDITOR` とし、ボタンハンドラはプラットフォーム
  ガードなしで統一的に `Share()` を呼ぶ（iOS Notification/Dialog の `#if UNITY_IOS && !UNITY_EDITOR` +
  `#else` 固定メッセージパターンとは異なる。Manager が Editor でも即時 Failure を返す設計のため）。
- TopMenu の Editor 遷移先をビルドターゲットで分岐（iOS ターゲットの Editor → iOS Share、
  それ以外の Editor → Android Share）。

## 5. ビルド・実行結果

- 実行コマンド（EditMode）:
  ```
  /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -projectPath <repo> \
    -runTests -testPlatform EditMode -testResults <scratchpad>/sample-editmode.xml -logFile <scratchpad>/sample-editmode.log
  ```
- 実行コマンド（PlayMode）:
  ```
  /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -projectPath <repo> \
    -runTests -testPlatform PlayMode -testResults <scratchpad>/sample-playmode.xml -logFile <scratchpad>/sample-playmode.log
  ```
- 結果: SUCCESS（コンパイルエラー 0件）
  - EditMode: 110件実行、110件成功、0件失敗（既存テストからの回帰なし。サンプル UI は
    EditMode/PlayMode テスト対象外だが、新規コードによるコンパイルエラーがないことを確認）。
  - PlayMode: 5件実行、5件成功、0件失敗（既存 `IosShareManagerIntegrationTests` からの回帰なし）。
- 補足: `unity-native-plugin.slnx` が Unity Editor 実行のたびに自動再生成され、PlayMode テスト用
  `.csproj` 参照行の CRLF が再発することを確認。都度末尾の `\r` を除去し、`git diff --check` を
  クリア（実装結果 result-v3 の既知事象と同一。恒久対応は別途 `.gitattributes` 検討が必要）。
- UI の実描画確認（ボタン配置・スタイル・遷移の見た目）は Unity Editor の Play Mode / シーン再生での
  目視確認が別途必要（バッチモードのテスト実行では UI 描画の見た目は確認できない）。要検証として残す。

## 6. 手動確認観点 / 未実施項目

### 6.1 手動確認観点（計画書 §6 に準拠。実機 iOS 18+ が必要）

- TopMenu で Share ボタンが表示され、タップで iOS Share サンプル画面へ遷移する。
- Back To Home で TopMenu へ戻る。
- Text / URL / URL+Preview: 共有シートが提示され、活動選択で `completed=true, activityType=...`、
  キャンセルで `completed=false (cancelled)`。
- URL+Preview: 共有シートヘッダにプレビュータイトル "Unity" が表示される。
- Image / MultipleImages: サンプル画像が共有シートに表示され共有できる。
- File / MultipleFiles: サンプルファイルが共有できる。
- Multiple: テキストと URL が両方共有対象になる。
- WithSubject: Mail 等で件名 "Sample Subject" が反映される。
- ExcludingActivities（主合否条件）: Copy（CopyToPasteboard）が共有シートに表示されない。
  補足: Facebook も非表示であることを確認するが、端末環境依存のため主合否にしない。
- ShareEmpty: `Error: No shareable items were provided.` が表示される。
- ShareInvalidURL: `Error: Invalid URL: ...`（native 文言）が表示される。
- ShareMissingFile / ShareMissingImage: `Error: File not found ...` / `Failed to load image ...` が表示される。
- iPad: 共有シートが popover で提示され、クラッシュしない。
- Editor 実行（iOS ビルドターゲット選択中）: TopMenu の Share ボタンから iOS Share 画面へ遷移でき、
  各ボタンで `Error: iOS share is only available on an iOS device.` が表示される。

### 6.2 未実施項目（理由付き）

- 上記手動確認観点はすべて未実施。実機 iOS 18+ および Xcode ビルド環境が必要なため
  （実装結果 result-v3 と同様の制約）。
- Editor 上での UI 実描画確認（レイアウト崩れ、スタイル適用、ボタンタップ動作）: バッチモードでの
  自動テストでは確認できないため未実施。Unity Editor を起動しての目視確認が必要。
- iOS ビルドターゲットへの切り替えを伴う Editor 確認（TopMenu → iOS Share 遷移）: 本セッションでは
  ビルドターゲットを切り替えていないため未実施（現在の Editor ビルドターゲットは Android と推定される
  ため、`OnShareClicked` の `#elif UNITY_EDITOR` 分岐＝Android Share 経路のみを実質的に検証している）。

## 7. Definition of Done

- 判定基準:
  - ○: 実装・コード・ビルド確認の範囲では OK
  - △: 一部 OK だが、追加確認が必要
  - ×: 未達
  - -: 対象外
- ○ 画面要件（機能一覧・操作導線・エラー表示）を計画書どおり実装
- ○ 変更ファイル一覧との一致
- ○ 共通実装パターンの維持・拡張方針の反映
- ○ コンパイルエラーなし（EditMode/PlayMode ともにテスト実行で確認）
- ○ 既存テストの回帰なし（EditMode 110件、PlayMode 5件、全成功）
- △ UI 実描画・実機共有シート挙動は未確認（実機 + Xcode 環境、Unity Editor 目視確認が必要）
- - Runtime 機能実装（`IosShareManager` 等）は対象外（変更なし）

## 8. ステップ8 実行確認

- 提示文:
  - 「このサンプル実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して終了 → review-implementation-sample-scene スキルへ引き継ぐ
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答
