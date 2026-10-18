# Windows の `Plugins/Windows/VERSION.txt.meta` がリポジトリに無い

- 記録日: 2026-10-18
- 分類: 同梱物（パッケージの中身）。Windows
- 状態: **未着手**（要確認から）
- 発見経緯: Android の C ABI 移行の段 4 の V-S4-4（`artifact/topics/android-c-abi-2/results/2026-10-17-android-c-abi-stage4-result-v2.md` の 4 章・5 章）。作業ツリーの外に展開したパッケージを Unity で開くと、Unity が `Plugins/Windows/VERSION.txt.meta` を作った

## 事実

- `.gitignore:93` が `/Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/VERSION.txt.meta` を除外している。見出しのコメントは「Auto-generated Unity meta files for AAR-derived Android library folders and Windows plugin files」。入ったのは `1fbc349`（2026-07-04、UNT-5）
- タグ `1.12.0` にもこの `.meta` は無い
- Android の `Plugins/Android/VERSION.txt.meta` はコミットしてある。2 つの OS で扱いがそろっていない
- なぜ除外したかの記録は見つかっていない

## 影響（要確認）

- git URL で入れたパッケージは `Library/PackageCache` の書き換えられない場所に置かれる。`.meta` の無いアセットは、Unity が「meta が無いので無視する」旨の警告を出すことがある。実際に出るか、出ても害がないか（`VERSION.txt` はビルドの手順が読むだけで、Unity のアセットとして使わない）は確かめていない

## 対応案

1. git URL で入れたプロジェクトで、警告が出るかを確かめる
2. 出るなら、除外をやめて `.meta` をコミットする（Android と同じ扱い）。除外した理由が見つかれば、それと照らす
