引数: $ARGUMENTS

以下の手順を実行してください。

1. `$ARGUMENTS` を解析する
   - `lang=ja` が含まれていれば言語を日本語、それ以外（`lang=en` または指定なし）は英語に設定する
   - `lang=...` 以外の部分をディレクトリパスとして扱う
   - ディレクトリパスがあれば `cd <path>` を実行する

2. `git add -N .` を実行する

3. `git diff` を実行してdiff内容を取得する

4. `git reset HEAD .` を実行する

5. diff内容を分析して、設定した言語でコミットメッセージを生成し、表示する

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

6. ユーザーに「このコミットメッセージでコミット・プッシュしますか？」と確認する
   - 「実行する」を選んだ場合: `git add .` → `git commit -m "生成したメッセージ"` → `git push` を実行する
   - 「キャンセル」を選んだ場合: メッセージの表示のみで終了する
