# 実装結果レポート v3（Android の C ABI 移行の段 4: 旧ブリッジの撤去、ビルダーの削除、改名、マニュアル、リリース）

## 基本情報

- 日付: 2026-10-18（v1・v2 は 2026-10-17）
- 対象プラットフォーム: Android
- ブランチ: feature/UNT-13
- 設計: `artifact/topics/android-c-abi-2/designs/2026-10-13-android-c-abi-stage4-design-v8.md`（以下「設計」）
- 設計のレビュー: `artifact/topics/android-c-abi-2/reviews/2026-10-13-android-c-abi-stage4-design-review-v1.md`〜`-v7.md`（ラウンド 6・7 は見直した止め方での差分レビュー。ラウンド 7 で両方の A1 が 0）
- 実装のレビュー: `artifact/topics/android-c-abi-2/reviews/2026-10-17-android-c-abi-stage4-implementation-review-v1.md`（サブエージェントと Codex。両方とも A は 0 件）
- 親課題: `artifact/topics/android-c-abi-2/README.md`
- 同梱: native-toolkit dist 1.13.0 の `android-native-toolkit-2.0.0.aar`、`android-native-toolkit-capi-2.0.0.aar` だけ（1.3.0 の 2 本は外した）
- push: 2026-10-18 に、利用者の指示で `feature/UNT-13` を origin に push した（タグはまだ）

## 0. 状態サマリー

設計 7 章のステップ 0〜7 を終え、ステップ 8（S4-5、リリース）の途中にいる（8 章）。v2 からの変更: native-toolkit のタグが打たれ、S4-5 を進めた（8 章を足した）。

| ステップ | 内容 | コミット | 状態 |
|---|---|---|---|
| 0 | 設計とレビュー | `bebf7c1` | 済み |
| 0 | 設計レビューの止め方の見直し（`review-document`） | `0e2445b` | 済み |
| 0 | リリースとコミットの手順（Android の AAR の行、Breaking Changes、件名の `!`） | `d435f8f` | 済み |
| 0 | `verify_unity_android.sh --pull-apk` と陽性の対照 | `1df97de` | 済み |
| 1 | S4-1: 1.x の AAR の撤去、ピン、層 2b の 1.x の分岐 | `df3eb78` | 済み |
| 2 | S4-2: Share・Clipboard の JSON ビルダーの削除 | `37f182c` | 済み |
| 3 | S4-3: 37 型の改名と 42 型の `[MovedFrom]` | `97f98c6` | 済み |
| 4 | V-S4-4（作業ツリーの外） | - | 済み（4 章） |
| 5 | 実装のレビューと直し | `5c3c68a` | 済み |
| 6 | `verified_native_toolkit_commit` | `e4eb039` | 済み |
| 7 | S4-4: マニュアル（起点のコピー、index、4 機能のページ、README と `package.json`、`Documentation~`、`docs`） | `498cc5b`、`11d2245`、`0c2edb2`、`cd74b3b`、`7cbf7d5`、`325dbf5`、`b8a049e`、`4a9efb3`、`0ad05dd` | 済み（7 章） |
| 8 | S4-5: リリース（チェックリスト、版、Unity の動作環境の追記、PR） | `a8a7af8`、`b3fc87f`、PR #31 | 途中（8 章） |

## 1. 実装サマリー

- **S4-1**: `Plugins/Android` から 1.3.0 の AAR 2 本と `.meta` を外した。`VERSION.txt` の `extra_*` のキーと説明を過去形の注記にした。`PreBuildProcessor` の `AndroidPin` から追加の組（プロパティ、読む処理、写す処理、ログ）を消し、XML コメントを Windows と同じ形にした。層 2b の 6 ファイルの `OnCApi` の分岐（56 か所）から 1.x の側を消し、各 fixture の `SetUp` で継ぎ目があることを `Assert.IsTrue` で確かめる形にした。1.x と C ABI を比べるためだけの記録のログを消した（Clipboard の、毎回値が変わるので記録だけする AC-21b、AC-21c、AC-29 は残した）。`common.md:45` を過去形にした
- **S4-2**: `AndroidShareJsonBuilder`、`AndroidClipboardJsonBuilder` と、そのテスト（Share 21 本、Clipboard 16 本）を `.meta` と対で消した。消した型に触れるコメント（Android 1、iOS 1、Windows 1）を、型の名前を出さない書き方にした。`testing.md` の Android の行から「builder」を消し、`common.md` の JSON ビルダーの決まりに Android の例外を足した。`AndroidNotificationJsonBuilder` は残し、XML コメントで役目（利用者が Manager に渡す JSON を組む道具）を書いた
- **S4-3**: 接頭辞の無い Android の public 型 37 に `Android` を付けた（ファイル 10 を `git mv`、`.meta` の GUID は保った）。参照の置き換えは 38 ファイル、651 か所。42 型に `[MovedFrom]` を付け、各ファイルに `using UnityEngine.Scripting.APIUpdating;` を足した（改名した 37 型は第 1 引数 `false` と `sourceClassName`、名前を残した 5 型は `true`。`sourceNamespace` はタグ 1.12.0 の名前空間）。`agent-rules` の「既知の逸脱」を 9 件（iOS 5、macOS 3、`IconConfiguration`）にし、レビュー・設計の手順の記述を直した。artifact の README 3 つを手で直した
- **実装のレビューの直し**（`5c3c68a`）: テストのメソッド名に残っていた旧い型名 9 か所を直した。`verify_unity_android.sh` は、シグナルで後始末をして終わるようにし（それまでは後始末の後も次の実行に進んだ）、副作用の復元を EXIT の trap からも 1 回だけ走らせ、Unity のテストの実行の残りもの（`Assets/InitTestScene*`、`Assets/Resources/PerformanceTestRun*`）を復元の対象に足し、後始末の間は次のシグナルを無視するようにした

## 2. 検証

| # | 内容 | 結果 |
|---|---|---|
| V-S4-1 の陽性の対照 | S4-1 の前、`--as-package-user --pull-apk` | PASS。3 実行とも APK 1 本、dex 3、1.x のクラス 384、manifest の 1.x の名前 5（AAR の展開での 5 と一致） |
| V-S4-1 | S4-1 の後、同じ | PASS。3 実行とも 1.x のクラス 0、manifest の 1.x の名前 0 |
| V-S4-1b | `Plugins/Android` の `git status` が実行の前後で同じ。`Deleted AAR not in the pin` 0 件。コミットの後の `git ls-files` が 6 | PASS |
| V-S4-2 | S4-2、S4-3、直しの後、`--as-package-user` | すべて PASS（EditMode 1397、PlayMode 337、実機 166 / 1 / 166〈package-user は 138 通過、28 はサンプル画面の無い player で飛ぶ〉）。S4-2 の 1 回目は 6 章のとおり止めてやり直した |
| V-S4-3 | `Plugins/Android/*.aar` が 2 本 | PASS |
| V-S4-4 | `[MovedFrom]` の効く範囲 | 4 章 |
| V-S4-5 | 設計 3.4 の S4-3 の 2 行 | PASS（旧名 0、`[MovedFrom]` 42 型の引数が一致。検査のスクリプトは実装のレビューの S1-3 で境界を直した） |

`verified_native_toolkit_commit` は `a7684deeea419ecaa5b7c7f7c8a0cf4e447a0f73`（直しの後の V-S4-2 のとき。`git -C ../native-toolkit status --porcelain -- dist/1.13.0/android` は空）。

## 3. 振る舞いの変化（利用者に見えるもの）

- 1.x の AAR を同梱しない。`android.unity.*`、`android.library.*` のクラスを直接使っていたコードは動かない
- `AndroidShareJsonBuilder`、`AndroidClipboardJsonBuilder` が無い
- Android の 37 型の名前が変わった（2.1 の表）。42 型の名前空間は段 3-0 で `Runtime.Android.<Feature>` に移っている
- これらはどれも利用者のコードが壊れる変更で、コミットの件名に `!` を付けた（S4-1、S4-2、S4-3）

## 4. V-S4-4 の結果（`[MovedFrom]` の効く範囲）

Unity 6000.4.2f1 の batchmode、`-buildTarget Android`、作業ツリーの外の使い捨てのプロジェクト。旧版は `git archive 1.12.0`、新版は `git archive 97f98c6`。manifest はリポジトリのものに `file:` のパッケージを足した。

| 段 | 何を | 結果 |
|---|---|---|
| 1（旧版） | `[SerializeReference]` で 3 つの Payload（Clipboard、Notification、Share）を旧名で保存 | 保存できた（アセットの型は `Runtime.<Feature>` の旧名） |
| 2a（新版、`-accept-apiupdate`） | 37 型だけのスクリプト | (b) 旧名の `CS0246` が残る。Updater は動かない |
| 2b | 名前を残した型を式で使う（`AndroidClipboardManager.Instance`） | (a) `CS0103` が残る。Updater は動かない |
| 2c | 同じ型を型の位置で使う | (a) `CS0246` が残る。Updater は動かない |
| 2 | ガード付きのスクリプト | (c) Editor では変わらない |
| 3（新版） | 旧名を参照しないスクリプトで読み直す | (d) 3 つとも `Runtime.Android.<Feature>.Android*Payload` として、段 1 と同じ値で読めた |
| - | 作業ツリーとパッケージのコピー | (e) 作業ツリーの `git status` は同じ。パッケージのコピーの既存のファイルはすべて同じで、Unity が `Plugins/Windows/VERSION.txt.meta` を足した（5 章） |

- **Updater が動かない理由**: Updater の名前の候補（`Library/Bee/artifacts/mvdfrm`）は 275 ファイルで、どれも `Unity`・`UnityEngine`・`UnityEditor` で始まるアセンブリのもの。`NativeToolkit.Runtime` は入らず、利用者の `Assembly-CSharp.dll.mvfrm` にもこのパッケージの名前が無い。この版の Unity は、このパッケージの `[MovedFrom]` を、スクリプトを書き換える候補にしない
- **結論**: 移行の節は設計 2.3 の既定のまま書く（42 型とも `using` を手で直す。37 型は名前も手で直す）。「旧名で `[SerializeReference]` に保存したデータは、新しい型に自動で移る」を足す
- **前提**: パッケージは `file:` で参照した。利用者が入れる git URL（`Library/PackageCache` の書き換えられないパッケージ）では確かめていない。どちらもソースからコンパイルされるので同じになる見込みで、移行の節は「手で直す」側に倒れる（実装のレビューの S1-4）
- 段 2a で 2b の `CS0103` が出なかったのは、宣言のエラー（2a の `CS0246`）があるとメソッドの本体のエラーが出ないためと見ている

## 5. 直さない残件と、設計からの逸脱

| ID | 中身 | 扱い |
|---|---|---|
| 実装レビュー X1-1 | V-S4-4 の (e) で、パッケージのコピーに Unity が `Plugins/Windows/VERSION.txt.meta` を足した | 既存のファイルは変わっていない。このファイルはリポジトリで `.gitignore:93` により除外され、タグ 1.12.0 にも無い（段 4 の前からある Windows の扱い）。書き換えられない UPM パッケージでは「meta が無い」の警告になりうるので、Windows の側の課題として別に見る |
| 実装レビュー X1-2 | V-S4-4 の段 1 のログに shadergraph の `CS0246`（`GUID`）が 2 件 | リポジトリの manifest から写した URP・shadergraph の側のエラーで、パッケージと段 1 のスクリプトはコンパイルでき、`-executeMethod` が走った。V-S4-4 の判定には関わらないので、最小の manifest でのやり直しはしない |
| 実装レビュー S1-3 の 2・3 | 3.4 の検査は `.cs`・`.md` と Runtime・Tests・agent-rules だけを見る。検査 2 は設計の表を写したものと照らし、タグ 1.12.0 と直接は照らさない | 実装のレビューで、リポジトリ全体の grep（0 件）と、タグ 1.12.0 との独自の照合（42 型が一致）が済んでいる |
| 実装レビュー S1-5 | `os-prefix-violations/README.md` の「公開 81 型・13%」を数え直していない（設計 2.1 からの逸脱） | 81 の数え方が記録に無く、今の数え方（namespace の直下の public 型 209）とは合わない。元の文は 2026-09-03 の記録として残し、日付を付けた追記で改名の結果と残り 9 件を書いた |
| 設計 8 章 | X5-1、X5-6、S5-6、S5-8、S5-9、S5-10（b〜d）、X7-1〜X7-3、S7-1〜S7-3 | 設計 8 章の扱いのまま。S4-4 と S4-5 で扱う |

## 6. 検証の中で起きたこと

- **S4-2 の 1 回目の V-S4-2 が止まった**: 実機の Android の実行で、Unity がプレイヤーにつないだ直後に接続が切れた。テストは実機で最後まで走り（UI ドライバーは正常な実行と同じ 269 の操作）、結果が Editor に届かず、Editor は 600 秒でタイムアウトした後も終わらなかった。スクリプトを止め、UI ドライバーが設定を戻したこと、プレイヤーが残っていないこと、Unity が残したファイルを戻したことを確かめ、やり直して通った。原因は分かっていない。同じことがまた起きたら調べる
- **このときスクリプトが止まらなかった**: TERM の trap が後始末の後も先に進んだ。実装のレビューの直し（`5c3c68a`）で、シグナルで終わり、副作用も戻すようにした
- 見張り（Monitor）の判定の誤り（`find -newermt` が bfs で効かない、走り始めに前回のログの時刻を見る）で、止まったという誤報が 3 回出た。どれも実際には進んでいた

## 7. S4-4（マニュアルの版 1.13.0）

- **起点**: `manual/1.12.0` をそのまま `manual/1.13.0` に写してコミットした（`498cc5b`）。push はしていない（利用者の指示。`write-manual` の手順の push より優先）
- **index（3 言語、`11d2245`）**: 版と Git URL を 1.13.0 にした。冒頭の紹介文を、Android はビルドが依存を足す形に直した。「Android requirements」（IL2CPP、ARM64、Gradle の設定は要らない）と「Changes in 1.13.0 (Android)」（各ページの移行の節への案内）を足した。Android のサンプルの「Configure Gradle Project」の手順と画像を消した
- **4 機能のページ（3 言語、`0c2edb2`、`cd74b3b`、`7cbf7d5`、`325dbf5`）**: 1 ページ 1 本のサブエージェントに分けた（3 言語を 1 本が持つ）。共通の文（コードの直し方、全機能に共通の破壊的変更）は 3 言語で先に書き、そのまま使わせた。各ページの `## Android` の最初の小節に移行の節を置き、目次に足した。本文の `using` と型名を直し、1.12.0 の記述のうち今のコードと合わないものを直した（Share の `CancelPendingCallbacks`・エラーの文言・Chooser アクションの条件、Clipboard の URI の scheme・エラーコードの原因、Notification の `<service>`・リソースの探し方など）。Windows の 1.12.0 の節の「Android の型の名前空間は変わらない」を直した
  - メインのエージェントの直し: Dialog から `[SerializeReference]` の箇条を消した（Dialog には改名した型も保存される型も無い）。Notification の「背面で設定の画面を開く」の 1.12.0 の列を、設計 5.3 の推測から、実機で測った値（別のスレッドから呼ぶと `{op} could not be started.`）に直した
- **README・`package.json`（`b8a049e`）**: 版、Git URL、冒頭の紹介文、Android の行（IL2CPP、ARM64）、移行の案内。`package.json` は `description` だけ（版は S4-5）。README の案内では、3.4 の検査語を避けて「Android の Gradle 設定のウィンドウ」と書いた
- **`Documentation~`（`0ad05dd`）**: Android のコードブロックの旧い `using`（言語ごとに 6）、冒頭の紹介文、Gradle の手順を直した。版の見出し（1.0.0）は設計の範囲の外で、触っていない
- **公開（`4a9efb3`）**: `scripts/publish_docs.sh 1.13.0`
- **コミットのずれ**: `Documentation~` の画像の削除を先に `git rm` していたため、それが `11d2245` に入り、`0ad05dd` が `4a9efb3` の後になった。中身はすべてコミットされている。利用者の判断で作り直さなかった

### 7.1 検査

| 検査 | 結果 |
|---|---|
| 設計 3.4 の S4-4 の行（`manual/1.13.0`、`Documentation~`、README の 21 ファイル） | 0 件（旧名、消したビルダー、旧い手順、1.x のクラス名、`UNITY_ANDROID` のブロックの旧い `using`。許す節は、段を問わず `1.13.0` の移行の見出しから同じ段以上の次の見出しまで） |
| 8 章の X7-3（3 つの文） | `manual/1.12.0` で各 3 件、`manual/1.13.0` で各 0 件 |
| 8 章の X7-2（移行の節のチェックリスト、4 ページ × 3 言語） | 0 件（改名の表が設計 2.1 の組をすべて持つ、共通の文がそのまま入っている、要件へのリンク、消したビルダーの説明） |
| `verify_manual.sh 1.13.0` | 止める項目 0、警告 0（画像、サンプルとの整合、アンカー、版の参照、文体、言語の差、docs の同期） |
| API の docs（`build-docs.sh`） | 警告 0、エラー 0。`api/` に新しい 37 型があり、旧名は 0 |

### 7.2 設計 8 章の残件の扱い

| ID | 扱った結果 |
|---|---|
| X5-1 | 3.4 の検査で、許す節では検査語をすべて除いた |
| S5-6 | 1.x のクラス名の式に否定先読み（`(?!notification\.ACTION_)`）を入れた |
| S5-8、X7-3 | 9 か所を直し、3 つの文の件数で確かめた |
| X7-2、S7-2 | 7.1 のチェックリスト。「自動で直る」とは書かず、V-S4-4 の結果（`using` も型名も手で直す、保存したデータは移る）だけを書いた |
| S7-3 | S4-4 の確かめは 7.1 の検査で行い、実装のレビューは回していない |
| S5-9 | S4-5 でリリースノートを書くときに扱う |

## 8. S4-5（リリース、2026-10-18）

- **native-toolkit のタグ**: native-toolkit のセッションから、タグ `1.13.0`（`b507c42c`、main）を打ったと連絡を受けた。`a7684dee` からタグまで、`dist/1.13.0/android/*.aar` と `m2/**/*.module` の差分は 0 行
- **native-toolkit のマニュアルの変更への対応**（`a8a7af8`）: native-toolkit は、Unity の使い方を unity-native-plugin のマニュアルだけに書くことにし、自分の manual 1.13.0 から Android の Unity の記載を消した（native-toolkit の `a9b1e329`）。unity-native-plugin のマニュアルはそれを参照していなかった。こちらに無かった次を、index の「Android の要件」（3 言語）と README のインストールの条件に足した
  - Unity 6000.0.61f1、6000.3.1f1、6000.4.1f1 以上（AndroidX の依存に Android Gradle Plugin 8.9.1 が要る）
  - Minimum API Level 31
  - App Startup（`androidx.startup.InitializationProvider`）を外すと、すべての呼び出しが失敗する（`AndroidNativeToolkitCApi` が `NOT_INITIALIZED` を返す）。その場合は最初の呼び出しの前に `NativeToolkitCApi.init` を呼ぶ
  - `package.json` の `unityRelease`（`6000.0` の `0f1`）は変えていない。上げると iOS・Windows・macOS だけを使う利用者も入れられなくなるので、マニュアルの記載にとどめた
- **リリース前チェックリスト**（`agent-rules/workflows/release/workflow.md` のステップ 3）: `package.json` の版のほかはすべて ✅
  - Android AAR: タグがある。2 本とも md5 がタグの `dist/1.13.0/android` と一致（`9e068bc9…`、`c706a8a6…`）。`verified_native_toolkit_commit` からタグまで差分なし
  - Windows DLL: md5 がタグ `1.12.0` と一致、`source` はタグを指す
  - `manual/1.13.0` の 3 言語、`docs/1.13.0`、`verify_manual.sh` の停止項目 0、`package.json` の `description`・`keywords` の鮮度
  - `package.json` の版は 1.12.0 だったので、1.13.0 にした（`b3fc87f`）
- **リリースノート**: ステップ 4 の形で、Breaking Changes を先頭に置いた（出す条件の両方に当たる: 英語のマニュアルに `Changes in 1.13.0` の見出しがあり、件名に `!` のコミットがある）。各機能のページの `#changes-in-1130` と index の `#android-requirements` へ、タグの `docs/1.13.0` の絶対 URL でリンクした（設計 8 章の S5-9）。全文は PR #31 の本文にある
- **PR**: `feature/UNT-13` → develop の PR #31（https://github.com/kimjh4941/unity-native-plugin/pull/31）。`MERGEABLE`、CI のチェックは無い
- **残りの手順**（release の手順のステップ 5〜7）: PR #31 を merge commit でマージ（`--delete-branch`）→ develop → main の PR を作って merge commit でマージ → main でタグ `1.13.0` を打って push → GitHub Release（本文は PR #31 と同じリリースノート）。push とマージは、利用者に確かめてから行う
- **このとき記録したもの**: 公開型を改名するときの `[MovedFrom]` の決まりを `agent-rules/coding-rules/common.md` の「既知の逸脱」の下に足した（iOS・macOS の改名で使う）。Windows の `VERSION.txt.meta` の件を `artifact/topics/windows-version-meta/` の課題にした（5 章の X1-1 の行）
