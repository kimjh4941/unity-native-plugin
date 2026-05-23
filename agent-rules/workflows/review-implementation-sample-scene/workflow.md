引数: $ARGUMENTS

以下の手順を実行してください。

1. `$ARGUMENTS` を解析する（最小限）
   - PR番号が含まれていれば `pr=<number>` として扱う
   - `lang=ja` または指定なしは出力言語を日本語として扱う
   - `lang=en` が明示的にあれば英語

2. レビュー対象のコードを確定する（必須）
   - `git rev-parse --abbrev-ref HEAD` を実行して現在のブランチ名を取得する
   - `git diff main...HEAD` を実行して main ブランチとの差分を取得する
   - 差分が空の場合はユーザーに「main との差分がありません。ローカルの未コミット差分を対象にしますか？」と確認し、承認された場合は `git diff` を実行する
   - PR番号が `$ARGUMENTS` に含まれている場合は `gh pr view <number>` と `gh pr diff <number>` を優先して使用する

3. インタラクティブ入力で参照ファイルを確定する（必須）
   - ダイアログで「レビュー対象のサンプルシーン計画ファイルを指定してください」と促す
   - 入力がない場合は次を候補として提示する
     - `artifact/plans/<feature>/` 配下の `*-sample-scene-plan*.md`（改訂版がある場合は最新バージョンのみ）
   - ダイアログで「レビュー対象のサンプルシーン実装結果ファイルを指定してください」と促す
   - 入力がない場合は次を候補として提示する
     - `artifact/results/<feature>/` 配下の `*-sample-scene-result*.md`（改訂版がある場合は最新バージョンのみ）
   - ダイアログで「対象プラットフォームを選択してください」と促す（ラジオボタン: Android / iOS / macOS / Windows）

4. プロジェクトルールを読み込む（必須）
   - `agent-rules/coding-rules/common.md` を読み込み、共通実装方針を把握する
   - `agent-rules/coding-rules/csharp.md` を読み込み、C# コーディングルールを把握する

5. サンプルシーン計画・実装結果ファイルを読み込む（必須）
   - 指定されたサンプルシーン計画ファイルを読み込み、次を把握する:
     - 実装対象のボタン一覧と各ボタンの API 呼び出し仕様
     - コンパイルガード方針（`#if UNITY_<PLATFORM> && !UNITY_EDITOR` / Editor フォールバック）
     - 権限ガード方針（`ExecuteIfNotificationPermissionGranted` 等）
     - ライフサイクル方針（OnEnable/OnDisable vs Start/OnDestroy）
     - 変更ファイル一覧
   - 指定された実装結果ファイルを読み込み、次を把握する:
     - 計画との差分（設計上の選択・変更理由）
     - 未実施項目と理由

6. コードレビューを実施する（必須）
   以下の観点で分析する:

   **計画との整合性**
   - ボタン一覧の実装漏れ・追加（計画記載のボタンがすべて実装されているか）
   - UXML のボタン name 属性と Controller の `Q<Button>("name")` の一致
   - API 呼び出し仕様（メソッド名・引数・コールバック形式）の計画との一致
   - 変更ファイル一覧の正確性（計画記載と実際の変更が一致しているか）

   **ライフサイクル管理**
   - イベント購読・解除のタイミング（OnEnable/OnDisable または Start/OnDestroy の選択と一貫性）
   - `Button.clicked` の登録（`+=`）と解除（`-=`）の対称性
   - Manager イベント（`NotificationActionReceived` 等）の OnEnable/OnDisable での管理

   **コンパイルガード**
   - プラットフォームガード（`#if UNITY_STANDALONE_OSX || UNITY_EDITOR` 等）の正確性
   - ハンドラ内部の実行ガード（`#if UNITY_<PLATFORM> && !UNITY_EDITOR`）の網羅性
   - Editor フォールバック（`SetResult("macOS Standalone only...")` 等）の実装漏れ

   **権限ガード**
   - 権限が必要な API 呼び出しに `ExecuteIfNotificationPermissionGranted` 等のガードがあるか
   - 権限確認系ボタン（RequestPermission / HasPermission 等）はガード不要であることの確認

   **ナビゲーション統合**
   - `NativeToolkitSampleNavigator` に `Show<Platform><Feature>()` が追加されているか
   - `RemoveExistingControllers` に当該 Controller の `RemoveIfExists<T>()` が追加されているか
   - `TopMenuExampleController` のコンパイルガードと遷移分岐が正確に追加されているか

   **プロジェクトルール準拠**
   - `agent-rules/coding-rules/common.md` への適合
   - `agent-rules/coding-rules/csharp.md` への適合
   - 命名規則（フィールド名・定数名・メソッド名）の既存 Controller との一貫性
   - namespace の有無（既存 Controller 群との一貫性）

   **USS / UXML 品質**
   - CSS クラス名が USS と UXML で一致しているか
   - `horizontal-scroller-visibility="Hidden"` 等のスクロール設定の適切性
   - ボタンスタイルの既存プラットフォーム画面との一貫性

   **既存 API 互換性**
   - 破壊的変更の有無（`NativeToolkitSampleNavigator` / `TopMenuExampleController` の既存動作に影響がないか）

   **コード品質**
   - 重複コード・不要な複雑さの有無
   - 定数の適切な使用（SampleNotificationId / SampleCategoryId 等）
   - `SetResult` / `FormatResult` 等ユーティリティの一貫した使用

7. レビュー結果を出力する（必須）
   以下の構造で出力する:

   ### レビュー概要
   - 差分の目的と変更内容のサマリー

   ### 重大な問題（high）
   - 修正必須の問題を列挙する（なければ「なし」）

   ### 改善提案（medium）
   - 推奨する改善点を列挙する

   ### 軽微な指摘（low）
   - スタイル・命名などの軽微な指摘を列挙する

   ### 計画整合性チェック
   - ボタン一覧の実装網羅性: ○ / △ / ×
   - UXML name と Controller の一致: ○ / △ / ×
   - API 呼び出し仕様の一致: ○ / △ / ×
   - 変更ファイル一覧との一致: ○ / △ / ×

   ### プロジェクトルール適合チェック
   - common.md 準拠: ○ / △ / ×
   - csharp.md 準拠: ○ / △ / ×
   - ライフサイクル管理（登録・解除の対称性）: ○ / △ / ×
   - コンパイルガードの網羅性: ○ / △ / ×
   - 権限ガードの網羅性: ○ / △ / ×
   - ナビゲーション統合: ○ / △ / ×
   - 既存 API 互換性: ○ / △ / ×

   ### 総合評価
   - LGTM / 要修正（軽微） / 要修正（重大） のいずれかで示す

8. レビュー結果ファイルを保存する（必須）
   - 保存先: `artifact/reviews/<feature>/`
   - ファイル名: `YYYY-MM-DD-<feature>-sample-scene-implementation-review-vN.md`
   - 同名が存在する場合は `vN` をインクリメントし、既存ファイルを上書きしない
   - 最低限、次を含める:
     - レビュー対象（ブランチ名 / PR番号 / diff）
     - サンプルシーン計画・実装結果ファイルのパス
     - レビュー結果（step 7 の出力全体）
     - 総合評価

9. 修正対応を確認する（必須）
   ユーザーに次を確認する:
   - 「このレビュー結果をもとに修正を行いますか？」
   - 選択肢:
     - 修正する: 指摘内容を反映して `implement-sample-scene` workflow に準じて修正する
     - レビュー結果のみで終了: 修正は行わず終了する

10. 出力ルール

- 指摘は具体的なファイルパスと行番号を可能な限り示す
- プロジェクトルール（common.md / csharp.md）由来の指摘は明示する
- 不確実な指摘は「要確認」として明記する
- 文章は簡潔に、箇条書き中心で書く
- 絵文字は使用しない
