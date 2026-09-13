引数: $ARGUMENTS

以下の手順を実行してください。

1. `$ARGUMENTS` を解析する（最小限）
   - `lang=ja` または指定なしは出力言語を日本語として扱う
   - `lang=en` が明示的にあれば英語

2. インタラクティブ入力で対象を確定する（必須）
   - ダイアログで「対象機能の実装結果ファイルを指定してください」と促す
   - 実装結果ファイルの入力がない場合は次を候補として提示する:
     - `artifact/results/<feature>/` 配下の `*-implementation-feature-result*.md`
   - ダイアログで「対象プラットフォームを選択してください」と促す（ラジオボタン: Android / iOS / macOS / Windows）

3. 前提情報を抽出する（必須）
   - 実装結果ファイルから次を抽出する:
     - 実装済みの公開 API と入力制約
     - エラー契約（isSuccess / errorMessage）
     - 手動確認観点
   - 不足がある場合は「不足前提」として明記し、勝手に要件追加しない

4. 既存サンプルコードを確認し、深掘りする（必須）
   - 実装前に次の既存サンプルコードを必ず確認する:
     - `Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/` 配下の ExampleController 群
     - `Assets/Samples/Native Toolkit/` 配下のサンプルシーン
   - native-toolkit 側のサンプルアプリ構成も必ず参照する（機能の見せ方・操作導線・結果表示の整合性確認のため）:
     - リポジトリルート: `/Users/jonghyunkim/Desktop/native-toolkit/`
     - プラットフォーム別サンプルアプリ:
       - Android: `android/AndroidLibraryExample/app/src/main/java/.../example/`（例: `ShareSampleScreen.kt`, `ReceivedShareScreen.kt`, `ShareChooserActionReceiver.kt`）
       - iOS: `ios/IosLibraryExample/`
       - macOS: `mac/MacLibraryExample/`
       - Windows: `windows/WindowsLibraryExample/`
     - native サンプルアプリで提供している機能一覧・操作単位・結果/エラー表示の見せ方を Unity サンプルシーンに反映できないか検討し、差分があれば明記する
   - 次の観点で深掘りし、差分方針を先に確定する:
     - 画面構成（どの ExampleController / シーンで機能を見せるか）
     - 状態管理（実行中・結果表示・実行時にしか値が無いものの保持方法）
     - 既存 UI パターン（ボタン配置、結果表示、エラー表示）。
       **既存サンプル画面の UXML に `TextField` は 1 つも存在しない。** これは慣習ではなく既定である
     - コールバック購読パターン（Manager イベントの購読・解除）
   - 深掘り結果として、次を短く整理してから実装計画に進む:
     - 再利用する既存コンポーネント
     - 追加するコンポーネント
     - 変更するファイルと変更理由

5. 共通実装パターンを固定する（必須）
   - 既存 ExampleController で共通して維持するパターンを優先する:
     - TopMenu → 機能別 ExampleController の導線
     - ExampleController 先頭にタイトルと結果表示領域を置く
     - 操作は機能カテゴリ単位でボタン群を整理する（Permission / Show / Schedule / Query など）
     - 実行結果は成功/失敗が一目で分かる文言で更新する
     - コールバック結果で UI 状態を更新する際は `UnityMainThreadDispatcher` 経由でメインスレッドに反映する
     - Manager イベント購読は `OnEnable` / `OnDisable` でライフサイクル管理する
     - 公開 API 呼び出し前後のログを残し、再現手順を追えるようにする

6. 実装制約を固定する（必須）
   - `agent-rules/coding-rules/common.md` を読み込み、共通方針を適用する
   - `agent-rules/coding-rules/csharp.md` を読み込み、C# コーディングルールを適用する
   - 既存サンプルの UI / 構成 / 命名規約を優先し、不要な構造変更を行わない

7. 実装計画をファイルに作成する（必須）

   ステップ2〜6の調査結果をもとに、以下の内容を含む実装計画ファイルを作成する。

   - **画面要件**
     - 機能一覧
     - 操作導線（操作 → 実行 → 結果表示）
     - エラー表示（errorMessage）
   - **変更ファイル一覧**
     - 新規作成 / 既存変更 / 非変更を分類して列挙する
     - `.meta` ファイルは Unity が自動生成するため記載しない
   - **実装方針**
     - 再利用する既存コンポーネントと追加するコンポーネント
     - 共通実装パターンのどこを維持しどこを拡張するか
   - **実装詳細**（implement-sample-scene ステップ3で行う内容）
     - 機能ごとに追加する UI 要素（ボタン・結果表示）を列挙する
     - **入力欄（`TextField`）は既定では置かない**（`agent-rules/coding-rules/common.md`
       「サンプルシーン（ExampleController + UXML）」）。値は固定 fixture、操作はボタンで完結させる。
       実行時にしか値が無いものは Controller の内部状態に持ち、任意文字列が分岐条件になる場合は
       代表値ごとのボタンにする
     - **例外を採るなら、この計画書に「なぜ固定 fixture で足りないのか」を注意書きとして書く。**
       理由の無い入力欄は `review-document` で **A1 区分**（実装フェーズでは気づけない欠陥）として扱われる
     - 各 Manager API の呼び出し方針とコールバック購読方法
     - 固定 fixture の内容と、それを選んだ理由
   - **手動確認観点**

   保存先: `artifact/designs/<feature>/`
   ファイル名: `YYYY-MM-DD-<os>-<feature>-sample-scene-design-vN.md`
   同名が存在する場合は `vN` をインクリメントし、既存ファイルを上書きしない。

   - **保存後に機械照合を実行する（必須）**: `python3 scripts/check_design_consistency.py <保存した設計書>`
     - Windows で実行する場合、`python3` は Microsoft Store のエイリアスに解決されることがある。これはコードを実行せず終了するため、検査が走らないまま成功に見える。`python3 -c "print(1)"` が `1` を出さない環境では `python` を使う
     - FAIL が 1 件でもある間は次のステップへ進まない。修正して再実行する
     - SKIP は「その検査の対象が文書に存在しない」意味であり、合格ではない。設計書に本来あるべき節が欠けていないか確認する
     - 件数・ID 順・表の列数・依存先の実在といった機械的な整合は、レビュー（review-document）へ持ち込まずここで潰す
     - `cited sections exist` の `unresolvable here` は、他文書の節（`（設計 2.9）`）や節番号に見える値（`progress 0.5`）の件数であり、**この文書からは照合できない**という意味。合格でも不合格でもないので、件数が想定より多いときは他文書参照の書き方を疑う

8. 出力ルール

- 実装結果由来の内容と、サンプル計画時の追加判断を明確に分離する
- 既存サンプルコードの深掘り結果（再利用/追加/変更方針）を必ず含める
- 共通実装パターンに対して、どこを維持しどこを拡張するかを明記する
- 変更対象ファイルは可能な限り具体パスで示す
- 不確実な事項は断定せず、要検証として明記する
- 文章は簡潔に、箇条書き中心で書く
- 絵文字は使用しない

9. 実装計画をユーザーに確認する（必須）

   ユーザーに次を確認する: 「この実装計画を採用して、次工程へ進めますか？」
   - 選択肢:
     - 承認する: 計画を確定し終了 → review-document スキルへ引き継ぐ
     - 修正する: 指摘内容を反映して計画ファイルを更新 → ステップ7へ戻る
     - キャンセル: 計画ファイルは保持したまま終了
