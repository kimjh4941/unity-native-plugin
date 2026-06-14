引数: $ARGUMENTS

以下の手順を実行してください。

1. `$ARGUMENTS` を解析する
   - `lang=ja` が含まれていれば言語を日本語、それ以外（`lang=en` または指定なし）は英語に設定する
   - `lang=...` 以外の部分をディレクトリパスとして扱う
   - ディレクトリパスがあれば `cd <path>` を実行する

2. 変更ファイルを確認する
   - `git add -N .` を実行して untracked ファイルを index に追加する
   - `git status --short` を実行して変更ファイル一覧を表示する
   - 変更が何もない場合は「コミットする変更がありません」と表示して終了する

3. diff を取得する
   - `git diff` を実行して diff 内容を取得する
   - diff が 500 行を超える場合は、ファイル一覧（`git diff --stat`）のみを使って分析する
   - その後 `git reset HEAD .` を実行して index をリセットする

4. diff 内容を分析して、設定した言語でコミットメッセージを生成し、表示する

   **英語の場合（デフォルト）:**
   - 1行目：`<type>(<scope>): <subject>` 形式
     - type: `feat` / `fix` / `docs` / `refactor` / `chore` など
     - subject: 命令形（Add ..., Fix ..., Update ... など）
   - 必要に応じて空行を挟み、箇条書きで変更の詳細を追記する
   - 絵文字は使用しない
   - 出力例:

     ```
     feat(ios-notification): add scheduled notification support to IosNotificationManager

     - add IosNotificationManager.ScheduleNotification() with JSON payload builder
     - register OnNotificationReceived callback via DllImport and MonoPInvokeCallback
     - add IosNotificationManagerExampleController scene with schedule / cancel buttons
     ```

   **日本語の場合:**
   - 1行目：タイトル（〜を追加する・〜を修正するなど）
   - 必要に応じて空行を挟み、箇条書きで変更の詳細を追記する
   - 絵文字は使用しない

5. ユーザーに「このコミットメッセージでコミット・プッシュしますか？」と確認する
   - 選択肢:
     - 実行する: `git add .` → `git commit -m "生成したメッセージ"` → `git push` を実行する
     - メッセージを修正する: 修正内容をユーザーに入力してもらい、反映した上で実行する
     - キャンセル: メッセージの表示のみで終了する
