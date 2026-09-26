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
     - **ファイル名は `agent-rules/coding-rules/common.md`「命名: OS 接頭辞と、共通ファイルを作らない方針」に従う。** 機能ディレクトリの新規ファイルは必ず `Android` / `Ios` / `Mac` / `Windows` の接頭辞を付ける。**他プラットフォームの既存実装を共有化する案は採らない。** 同じロジックが必要なら、そのプラットフォーム用に複製して持たせる（`Runtime/Common/` の横断インフラのみが例外）
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
   - **自動化の前提**（必須。層の定義は `agent-rules/coding-rules/testing.md`）
     - **検証の層**: 各操作・各エラーケースを、層 1（EditMode）/ 層 2a（PlayMode・Editor 内）/
       層 2b（PlayMode・Player 上）/ 層 3（OS 境界）/ 手動・computer use のどれで確かめるかを表にする。
       手動に残す項目には、自動化できない理由を書く（「実装が大変」は理由にしない）
     - **OS が出す画面・求める許可・前提の OS 設定**: 実行時に OS が出しうるダイアログ（アクセス許可、
       ファイアウォール、プライバシーの確認など）と、前提になる OS 設定（クリップボード履歴、通知の許可など）を列挙し、
       無人で実行する前にどう満たすか（事前の設定、実行前のチェック）を書く。出ない・要らない場合も「なし」と明記する
     - **呼び出し側を止める OS の画面**: OS の画面を同期で出し、閉じられるまで戻らない API があるかを書く。
       ある場合、その操作は外から画面を閉じる手段（UI Automation など）がないと自動化できない。
       現在のハーネスにはこの手段がないので、その旨と代わりの検証方法を書く
     - 背景: 2026-09、Windows のテスト用 Player を初めて回したときに、実装が済んだ後で障害が見つかった
       （実行のたびに出るファイアウォールのダイアログ、Player に入らない Editor 専用のテストフック）。
       設計の段階で書いておけば、実装と同時に準備できる。詳細は `artifact/topics/cross-platform-testing/README.md`

   保存先: `artifact/features/<feature>/designs/`
   ファイル名: `YYYY-MM-DD-<os>-<feature>-design-vN.md`
   同名が存在する場合は `vN` をインクリメントし、既存ファイルを上書きしない。

   - **保存後に機械照合を実行する（必須）**: `python3 scripts/check_design_consistency.py <保存した設計書>`
     - Windows で実行する場合、`python3` は Microsoft Store のエイリアスに解決されることがある。これはコードを実行せず終了するため、検査が走らないまま成功に見える。`python3 -c "print(1)"` が `1` を出さない環境では `python` を使う
     - FAIL が 1 件でもある間は次のステップへ進まない。修正して再実行する
     - SKIP は「その検査の対象が文書に存在しない」意味であり、合格ではない。設計書に本来あるべき節が欠けていないか確認する
     - 件数・ID 順・表の列数・依存先の実在といった機械的な整合は、レビュー（review-document）へ持ち込まずここで潰す
     - `cited sections exist` の `unresolvable here` は、他文書の節（`（設計 2.9）`）や節番号に見える値（`progress 0.5`）の件数であり、**この文書からは照合できない**という意味。合格でも不合格でもないので、件数が想定より多いときは他文書参照の書き方を疑う

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
