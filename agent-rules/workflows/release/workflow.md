引数: $ARGUMENTS

以下の手順を実行してください。

## ステップ1: 引数解析

`$ARGUMENTS` を解析する:
- `version=X.Y.Z` が含まれていればリリースバージョンとして使用する
- `lang=ja` が含まれていれば日本語、`lang=en` または指定なしは英語に設定する

## ステップ2: インタラクティブ入力

ダイアログで以下を確認する:

1. **リリースバージョン** — ステップ1 で取得済みの場合はスキップ
   - 例: `1.3.0`

2. **マージ方式** — デフォルト: squash
   - `squash`: コミットをまとめてマージ
   - `merge commit`: コミット履歴をそのままマージ

## ステップ3: リリース前チェックリスト

以下を順に確認し、結果を ✅ / ❌ で一覧表示する:

| 項目 | 確認方法 |
|---|---|
| ブランチ | `git branch --show-current` が `feature/` 系であること |
| 未コミット変更 | `git status --porcelain` の出力が空であること |
| Plugins | `Packages/com.jonghyunkim.nativetoolkit/Plugins/` に `Android` `iOS` `macOS` `Windows` ディレクトリが存在すること |
| manual | `manual/<version>/` に `*.md` `*.ja.md` `*.ko.md` の各マニュアルが存在すること |
| docs | `docs/<version>/` が存在すること |
| package.json バージョン | `Packages/com.jonghyunkim.nativetoolkit/package.json` の `"version"` フィールドが `<version>` と一致すること |
| package.json 内容の鮮度 | `package.json` の `"description"` / `"keywords"` が、`README.md` の `## Features` 見出しに列挙された機能（Dialog / Notification / Share / Clipboard 等）を漏れなく反映していること |

- `package.json` のバージョンが異なる場合は自動的に更新し、コミットしてから続行する
- `package.json` の `description` / `keywords` が `README.md` の機能一覧と食い違う場合（新機能追加時にこれらの更新が漏れているケースが多い）は自動修正せず、不足している機能名を提示してユーザーに更新するか確認する
- ❌ が1件でもある場合はユーザーに「チェックに未通過の項目があります。続行しますか？」を確認する:
  - 続行する: 次へ
  - キャンセル: 終了

## ステップ4: リリースノート生成

1. `git log main..HEAD --oneline` を実行してコミット一覧を取得する
2. conventional commits の type 別に分類する:
   - `feat` → Features
   - `fix` → Fixes
   - `docs` → Documentation
   - `chore` / `refactor` / その他 → Chores
3. 以下のフォーマットで Markdown を生成する:

```
## [X.Y.Z] - YYYY-MM-DD

### Features
- ...

### Fixes
- ...

### Documentation
- ...

### Chores
- ...
```

- 該当するコミットがないセクションは省略する
- 日付は今日の日付を使用する

4. 生成内容を表示し、「このリリースノートで進めますか？」を確認する:
   - 進める: 次へ
   - 修正する: 修正内容をテキスト入力で受け付け、内容を更新して再確認
   - キャンセル: 終了

## ステップ5: feature → develop PR を作成する

1. `gh pr create --base develop --title "feature: <現在のブランチ名>" --body "<ステップ4のリリースノート>"` を実行する
2. PR URL を表示する
3. 「PR をマージしますか？」を確認する:
   - マージする: `gh pr merge <PR番号> --<squash|merge> --delete-branch` を実行する
   - あとで手動でマージする: PR URL を表示して終了

## ステップ6: develop → main PR を作成する

（ステップ5 でマージした場合のみ実行）

- **マージ方式は必ず `--merge`（merge commit）を使用する。squash は使用しない**
  - 理由: develop→main を squash すると、main 側のコミットが develop の履歴と親子関係を持たなくなる
    - 次回リリース時に `git merge-base develop main` が実際より古いコミットまで遡ってしまい、develop 側で既に取り込み済みの変更が「両側で別々に変更された」と誤認されて偽コンフリクトが発生する
    - feature→develop 側（ステップ5）は squash のままで問題ない（トピックブランチ整理が目的で、develop 自身の履歴の連続性には影響しないため）

1. `git fetch origin && git checkout develop && git pull origin develop` を実行する
2. `gh pr create --base main --title "Release v<version>" --body "<ステップ4のリリースノート>"` を実行する
3. PR URL を表示する
4. 「develop → main PR をマージしますか？」を確認する:
   - マージする: `gh pr merge <PR番号> --merge` を実行する
     - コンフリクトが発生した場合:
       - `gh pr checkout <PR番号> && git fetch origin main` を実行する
       - コンフリクトファイルについて、`origin/main` 側の blob ハッシュが develop の過去コミット（前回リリース時点など）の blob ハッシュと一致するか確認する（squash-merge 由来の偽コンフリクトである可能性が高いため）
       - 一致する場合: `git merge -X ours origin/main` で develop 側の内容を採用してマージし、push する
       - 一致しない場合（真のコンフリクト）: 内容を確認しユーザーに解決方針を確認する
   - あとで手動でマージする: PR URL を表示し、「手動マージ後にタグ・GitHub Release を作成しますか？」を確認する
     - 作成する: ステップ7 へ
     - スキップ: 終了

## ステップ7: タグを打って GitHub Release を作成する

（ステップ6 で main へマージした、またはユーザーが手動マージ後に続行を選択した場合のみ実行）

1. `git checkout main && git pull origin main` を実行する
2. `git tag <version>` を実行する
3. `git push origin <version>` を実行する
4. `gh release create <version> --title "<version>" --notes "<ステップ4のリリースノート>"` を実行する
5. Release URL を表示して終了する
