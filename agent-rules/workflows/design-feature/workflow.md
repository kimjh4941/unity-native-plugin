引数: $ARGUMENTS

以下の手順を実行してください。

1. `$ARGUMENTS` を解析する（最小限）
   - `lang=ja` または指定なしは出力言語を日本語として扱う
   - `lang=en` が明示的にあれば英語

2. インタラクティブ入力でパラメータを確定する（必須）
   - ダイアログで「実装対象の機能名を入力してください」と促す（例: `notification`, `dialog`）
   - ダイアログで「対象プラットフォームを選択してください」と促す（ラジオボタン: Android / iOS / macOS / Windows）

3. native-toolkit の既存 UnityPlugin 実装を確認する（必須）

   対象プラットフォームに対応する native-toolkit の UnityPlugin 実装を読み込み、すでに公開されている API・コールバック・定数を把握する。

   | プラットフォーム | 参照パス |
   | -------------- | ------- |
   | Android  | `/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/` |
   | iOS      | `/Users/jonghyunkim/Desktop/native-toolkit/ios/UnityIosPlugin/UnityIosPlugin/` |
   | macOS    | `/Users/jonghyunkim/Desktop/native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/` |
   | Windows  | `C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary` |

   - 公開されている関数名・コールバック型・定数を一覧化する
   - C# 側で `[DllImport]` / `AndroidJavaObject` で呼び出す対象を確定する
   - エラーケースと返却仕様（`isSuccess` / `errorMessage`）をネイティブ実装から把握する
   - **ネイティブ側にすでに存在する実装をこのプロジェクトで再実装しない**

   Android の場合は `android_library` も追加で参照する:

   - `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/android/library/`
   - domain error 定義・repository 実装など、Unity 公開層（`unity_android_plugin`）の背後にある実装を把握する

   Android の場合は同梱 AAR の内容も確認する:

   - `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/` 配下の AAR を展開し、`AndroidManifest.xml` と `res/` に何が内包されているかを把握する
   - manifest 宣言やリソース（FileProvider の `res/xml/` 等）が AAR に含まれていない場合、利用アプリ側に追加設定が必要になる可能性がある。設計段階で配置責任を確定する

4. 既存の C# 実装を確認する（必須）

   `Packages/com.jonghyunkim.nativetoolkit/Runtime/` 配下の既存実装を読み込み、パターン・命名・構造を把握する。

   | ディレクトリ | 確認内容 |
   | ----------- | ------- |
   | `Common/`       | `UnityMainThreadDispatcher`、`IconConfiguration` などの共通ユーティリティ |
   | `Dialog/`       | 各プラットフォームの Manager 実装（Singleton・イベント・Bridge 呼び出しパターン） |
   | `Notification/` | 各プラットフォームの Manager・Payload・JsonBuilder 実装 |

   - 既存の Singleton パターン・イベントシグネチャ・namespace を把握する
   - すでに実装済みのクラス・メソッドを重複追加しない
   - 新規実装は既存パターンに準拠する

5. 実装制約を固定する（必須）
   - `agent-rules/coding-rules/common.md` を読み込み、共通実装方針を固定する
   - `agent-rules/coding-rules/csharp.md` を読み込み、C# コーディングルールを固定する

6. 実装計画をファイルに作成する（必須）

   ステップ3〜5の調査結果をもとに、以下の内容を含む実装計画ファイルを作成する。

   - **実装対象 API 一覧**
     - ネイティブ関数名・コールバック型・定数（ステップ3の確認結果）
     - C# 側の `[DllImport]` / `AndroidJavaObject` 呼び出し方針
   - **変更ファイル一覧**
     - 新規作成 / 既存変更 / 非変更を分類して列挙する（`Packages/com.jonghyunkim.nativetoolkit/Runtime/` 配下）
     - テストファイル（`Tests/Runtime/` 配下）も変更一覧に含める
     - `.meta` ファイルは Unity が自動生成するため記載しない
     - **サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は対象外とする。** これらは `design-sample-scene` スキルで別途設計するため、本計画書には含めない
   - **実装詳細**（implement-feature ステップ3で行う内容）
     - クラス・メソッド・イベント設計（Singleton・delegate・event シグネチャ）
     - **callback 提供方針**（既存 Manager パターンに準拠）
       - 共通イベント（`event Action<Result>`）: 常に発火。横断的なハンドリング用
       - 個別 callback（`Action<Result>? onResult = null`）: 任意。各操作メソッドの引数として提供し、未指定でも共通イベントは発火する
       - 個別 callback は `IosNotificationManager` の per-call callback 方式に準拠し、同一操作の連続呼び出しは last-registered wins
       - 設計書には共通イベントと個別 callback の両方のシグネチャ、発火タイミング、dispatch 順序（共通 → 個別）を明記する
     - スレッド契約・メモリ契約・エラー契約の実装方針
     - 依存関係の実装順序（Bridge → Manager → テスト）
     - Android で `AndroidJavaProxy` を使うコールバックがある場合、IL2CPP 制約を明記する
       - proxy メソッドは `public` 非 static にする
       - メソッド名・引数型を native インターフェースに完全一致させる
       - `[MonoPInvokeCallback]` は不要（`AndroidJavaProxy` 方式のため）
   - **エラーケース一覧と返却仕様（層別に分けて記載する）**
     - parser 層: JSON 検証失敗（`Failed to {operation}: ...` 形式で返る）
     - use case / repository 層: ドメインエラー（個別の errorMessage 文言）
     - C# Bridge 層: 非対応プラットフォーム・未初期化・`Call` 例外（`{operation} could not be started.` 等）
   - **テスト方針**（EditMode / PlayMode / 手動確認の分担）

   保存先: `artifact/designs/<feature>/`
   ファイル名: `YYYY-MM-DD-<os>-<feature>-design-vN.md`
   同名が存在する場合は `vN` をインクリメントし、既存ファイルを上書きしない。

7. 出力ルール

- native-toolkit 確認結果と既存 C# 実装の確認結果を明確に分離して記載する
- 変更対象ファイルは可能な限り具体パスで示す
- エラーケース一覧は全ケースを列挙する
- 不確実な事項は断定せず、要検証として明記する
- サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）の設計は含めない（`design-sample-scene` スキルで別途実施）
- 文章は簡潔に、箇条書き中心で書く
- 絵文字は使用しない

8. 実装計画をユーザーに確認する（必須）

   ユーザーに次を確認する: 「この実装計画をレビューしますか？」
   - 選択肢:
     - 承認する: 計画を確定し終了 → review-document スキルへ引き継ぐ
     - 修正する: 指摘内容を反映して計画ファイルを更新 → ステップ6へ戻る
     - キャンセル: 計画ファイルは保持したまま終了
