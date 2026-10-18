# 実装レビュー結果

- 日付: 2026-10-12
- 機能名: share
- プラットフォーム: android
- レビュー対象: ブランチ `feature/UNT-13` の未コミットの差分（実装レビュー v1 の指摘を直した後）と、`Tests/PlayMode/AndroidShare*.cs`
- 実装計画: `artifact/android/share/designs/2026-10-11-android-share-design-v6.md`
- 実装結果: `artifact/android/share/results/2026-10-12-android-share-implementation-feature-result-v1.md`（このレビューの後に v2）
- 前回: `artifact/android/share/reviews/2026-10-12-android-share-implementation-feature-review-v1.md`
- レビュアー: サブエージェント（Claude）と Codex を並行で回した。どちらも読むだけ。1 章はサブエージェント、2 章は Codex の出力（どちらも手を加えていない）

---

## 1. サブエージェント（Claude）


読むだけで行った。リポジトリのファイルは変えていない。adb、実機、Unity は使っていない。対象は未コミットの差分、新しいファイル 3 本、実機テスト 3 本。1 回目の指摘を確かめた後、直しで入った所を中心に新しい問題を探した。

### 1. 1 回目の指摘は直ったか

| 指摘 | 判定 | 根拠（file:line） |
|---|---|---|
| X:A-1（最初の `Instance` を別のスレッドから取る） | 直った（文書で前提にする形） | `AndroidShareManager.cs:30-32`（クラスの remarks）、`:125-130`（`Instance`）、`common.md:84`、実装結果 3.4 の「`Instance` の最初の取得」の行。2 回目の取得からは別のスレッドでも動く。`_instance == null` は、生きている MonoBehaviour なら `m_CachedPtr` を読むだけ。`EnsureCreated` は UnityMain の外では何もしない（`AndroidNativeCallbackQueue.cs:91-95`）。AS-35・53 で実測済み |
| X:B-1（coordinator の `_pending` の可視性） | 直った | `ShareChooserActionCallbackCoordinator.cs:16-17`。`volatile` にした。`Fire` は `:57` で読んだ値を閉じ込める。`Clear` と `Fire` が重なったときにどちらの値になるかは、K-1 で許した範囲 |
| X:B-2（K-1 のテストに per-call が無い） | 直った | `AndroidShareManagerEditorTests.cs:408-420`。`Fire` が per-call を dispatch の中で後から読むように壊すと、`Clear()` の後なので `perCall` が出ず、テストが落ちる |
| S:B-1（共通イベント → per-call の順） | 直った | `AndroidShareManagerEditorTests.cs:135-150`。すぐ決まる結果と完了の両方で順を見ている。`Deliver`（`AndroidShareManager.cs:515-516`）の 2 行を入れ替えると落ちる |
| S:B-2（壊した後の状態の片付け） | 直った | `AndroidShareManagerEditorTests.cs:239-257`。作り直した Manager で `OnDoneCore` と `OnSelectionCore` を呼び、前の Manager の callback が出ないことを照らす。`AndroidShareManager.cs:182` か `:183` を消すと落ちる |
| S:C-1（null 引数のテストの穴） | 直った | `AndroidShareManagerEditorTests.cs:261-293`。どの操作にも callback を渡し、8 操作の完了、選択、Chooser Action で何も出ないことを照らす。`SharePaths` で null の確かめの前に枠を入れる版でも落ちる |
| S:C-2（`EnsureListeners` の例外） | 直った（新しい C-2 あり） | `AndroidShareManager.cs:855-865` |
| S:C-4 / X:C-1（アイコンのログ） | 一部だけ | コードは直った。`:357` で Base64 を解いた後のバイト数を出し、検査に通った後なので `icon!` も安全（`AndroidShareCApi.cs:197-205`）。ただし実装結果 1.1 の J-S10 の行は「アイコンは文字数だけ」のまま（実装結果 :39）→ 新しい C-1 |
| S:C-5（common.md のログの項） | 直った | `common.md:85` |
| S:C-6 / X:C-2（K-1 の書き写し） | 直った（実装結果の側） | 実装結果 3.4 の「結果が Manager を壊す前後に来る（J-S4）」の行。設計 5.3 は次の版で直す（S:C-10 と同じ扱い） |
| S:C-7（XML の remarks） | 直った | `AndroidShareManager.cs:33-35`。`artifact/` のパスは、Dialog に合わせて残すと判断した |
| S:C-8（testing.md の言い過ぎ） | 直った | `testing.md:229` |
| S:C-9（実機テストの参照） | 直った（反射で版を見分ける形は持ち越し） | 3 ファイルとも参照を UI テスト計画 v6 にした |
| S:C-3、S:C-10 | 持ち越し（扱いは妥当） | どちらも 1 回目の反映表に書いてある |

### 2. 新しい指摘

#### A（必ず直す）

なし。調べたが A にしなかったものは次のとおり。

- **アイコンのログを動かした所**: `icon!.Length` が null で落ちる道は無い。`CheckDirectTarget` が null（失敗なし）を返すのは、`DecodeIcon` が null でない値を返したときだけ。
- **`EnsureListeners` の `try/catch`**: `lock` は `lock` 文が外すので、例外で残らない。登録できた方の印だけが立ち、もう片方は次の呼び出しでやり直す。J-S6 のとおり。
- **`volatile` の `Action<T>?`**: 参照型なので C# と IL2CPP のどちらでも有効。振る舞いは変わらない。
- **足したテスト**: 4 本とも、狙ったミューテーションで落ちることをコードを追って確かめた。いつも通ってしまうテストは無い。

#### B（直すべき）

**B-1 直したコードのうち、実機でしか動かない所が実機で流れていない**
- 1 回目の直しで、`#if UNITY_ANDROID && !UNITY_EDITOR` の中か Android の上でしか通らない所が変わった。
  - `EnsureListeners` の `try/catch`（`AndroidShareManager.cs:855-865`）
  - アイコンのログの位置と中身（`:357`。Editor では `OnDevice` が先に返すので届かない）
  - coordinator の `volatile`（IL2CPP でのコンパイル）
- 反映表が確かめたのは層 1（153/153）と層 2a（36/36）だけ。層 2a ではこれらの行に届かない。実装結果 5 章の実機 81/81 は直す前の結果。
- 壊れる見込みは低い。それでも J-S10 のログ（AS-31 など Direct Share の経路）と IL2CPP のビルドは、実機でしか確かめられない。
- 今走っている実機の実行が直した後のコードなら、その結果を実装結果 5 章に書けば済む。

#### C（軽微）

- **C-1 実装結果が直しに追いついていない**
  - 1.1 の J-S10 が「アイコンは文字数だけ」のまま（実装結果 :39）。コード、設計 4.4、`common.md:85` は「バイト数」。
  - 2.2 の `ShareChooserActionCallbackCoordinator.cs` が「XML コメントだけ」のまま。実際は `volatile` も足した。
  - 1.1 の J-S16 に `Instance` の前提が無い（3.4 には書いてある）。
- **C-2 `EnsureListeners` のログの重さ**（`AndroidShareManager.cs:863`）
  - 捕まえた例外を `LogWarning` にしている。`common.md:87` は「`LogError` は……捕まえた例外」と決めている。同じファイルのほかの `catch`（`:549`、`:773`、`:816`、`:845`、受け口）はどれも `LogError` で、`{e}`（スタックトレース付き）を出す。
  - J-S6 の「失敗したら `LogWarning`」は、戻り値が失敗のとき（`:876`、`:883`）の話として読める。そう読めば、例外は `LogError($"... {e}")` に揃えるのが規則どおり。
- **C-3 テストの本数が古い**
  - 層 2a の `AndroidShareManagerEditorTests` は今 20 本（`[UnityTest]` を数えた）。
  - 次の 3 か所が 19 本のまま: `testing.md:377`、実装結果 5 章（「19」「35/35」）、`artifact/topics/android-c-abi-2/README.md` の段 3 の行。
  - 1 回目のレビューの反映表は 36/36 と書いている。
- **C-4 XML の remarks が、別のスレッドから呼んだときに成り立たない**（`AndroidShareManager.cs:39-40`）
  - 「Every result … arrives after the method has returned」とある。これは UnityMain から呼んだときだけ正しい。
  - 別のスレッドから呼ぶと、UnityMain の取り出し口が先に回り、そのスレッドで呼んだメソッドが戻る前に結果が出ることがある。`PostImmediateResult` の後の `FreeAll` やログの間が、その例。
  - 「UnityMain から呼んだときは」と範囲を付けるとよい。振る舞いは J-S16 の範囲内なので、文言だけの問題。
- **C-5 作業ツリーにテスト実行のゴミがある**
  - 対象: `Assets/InitTestScene…unity(.meta)`、`Assets/Resources/PerformanceTestRun*.json(.meta)`、`Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` の 2 行の削除、`unity-native-plugin.slnx` の並び替え。
  - どれも今走っている実行が作ったものに見える。コミットに入れないように気をつける。

### 3. まとめ

- 1 回目の指摘 15 件
  - 直った: 12 件（X:A-1 は文書で前提にする形）
  - 一部だけ: 1 件（S:C-4 / X:C-1。実装結果 1.1 が古い）
  - 持ち越し（扱いは妥当）: 2 件（S:C-3、S:C-10）
- 1 回目の直しが新しい欠陥を入れた所は無かった。気になったのは、実機で確かめていないこと（B-1）と、文書が古いこと（C-1、C-3）。
- 件数: A 0 件 / B 1 件 / C 5 件

A は 0 件

## 2. Codex

### X 指摘の解消状況

| 指摘 | 状況 | 根拠 |
|---|---|---|
| X:A-1 | 実装・公開契約は解消済み。設計書への反映は未完 | クラス XML と `Instance` XML に「最初は UnityMain で取得」と明記されています（AndroidShareManager.cs:30、:125）。設計 v6 の J-S16 は無条件に「どのスレッドから呼んでもよい」のままです（C-1）。 |
| X:B-1 | 実装修正済み、検証は不足 | `_pending` が `volatile` になり、`Fire` は dispatch 前に一度だけ読み取っています（ShareChooserActionCallbackCoordinator.cs:17、:55）。スレッド間 publication のテストはありません（B-1）。 |
| X:B-2 | 解消済み | K-1 テストが per-call を登録し、Manager 破棄後の到達を検証しています（AndroidShareManagerEditorTests.cs:409）。 |
| X:C-1 | 実装は解消済み | Direct Share のログは Base64 文字数を出しません。デコード成功後の `icon.Length` をバイト数として出しています（AndroidShareManager.cs:353、:357）。実装結果に古い記述が残っています（C-2）。 |
| X:C-2 | 解消済み | 実装結果 3.4 に、Chooser Action は受け口時点の Manager と per-call に閉じ込め、破棄後も一度配送する例外が明記されています（実装結果:128）。 |

### 新しい指摘

#### A
なし。通常の利用経路で再現するクラッシュ、リーク、コールバック欠落、C ABI 契約違反の具体的な経路は見つかりませんでした。`EnsureListeners` の新しい `try/catch` は例外を C# 呼び出し元へ漏らしません。登録フラグが偽のままなので次回再試行できます（AndroidShareManager.cs:854）。

#### B-1: `volatile _pending` の修正を検出できるテストがない
- 場所は ShareChooserActionCallbackCoordinator.cs:17 と AndroidShareManagerEditorTests.cs:381 です。
- 追加した層 2a の Chooser Action テストは、`ShareText` と `OnChooserActionCore` を同じ UnityMain 上で逐次実行します。
- 既存の coordinator テストも同期 dispatcher `a => a()` だけです（AndroidShareChooserActionCallbackCoordinatorTests.cs:15）。
- そのため `_pending` から `volatile` を外しても、現在のテストは落ちません。
- 少なくとも reflection で `IsVolatile` の required modifier を固定すれば、修正の消失を決定的に検出できます。

#### C-1: 設計 v6 に `Instance` の UnityMain 前提が未反映
設計 v6:434 の J-S16 を、X:A-1 の契約修正に合わせて書き戻す必要があります。

#### C-2: 実装結果のアイコンログ単位が古い
実装結果:45 が「文字数」のままです。実装と設計は「デコード後のバイト数」です。

#### C-3: 実装結果の層 2a 件数が修正前のまま
- 実装結果:147 は 19 件・35/35 のままです。
- 現在は EditorTests が 20 メソッド、Dialog が 16 メソッドで合計 36 です。
- 第1回レビューの反映欄は 36/36 なので、結果文書側の更新漏れです。

#### C-4: Share 設計外の URP 設定変更が差分に混在
- Assets/Settings/UniversalRenderPipelineGlobalSettings.asset:81 で、`m_RuntimeSettings` から SSAO の resource 参照 2 件が削除されています。
- 設計と実装結果の変更ファイル一覧にありません。
- Share 移行との関係はファイルから確認できず、描画への影響は仮説です。
- 差分から除外するか、理由を記録すべきです。

### 総合評価
要修正（軽微）です。

- Manager + Bridge、P/Invoke ガード、構造体配置、`ulong` request ID、delegate lifetime、`user_data` と release、ネイティブ文字列の解放は設計と整合しています。
- 第1回の A は、公開契約に前提を追加したことで解消されています。
- 新しい A はありません。
- Unity、adb、テストは実行していません。ファイルも変更していません。

A は 0 件

## 3. 統合（止める基準の判定）

| 分類 | サブエージェント（S:） | Codex（X:） |
|---|---|---|
| A | 0 | 0 |
| B | 1（S:B-1） | 1（X:B-1） |
| C | 5（S:C-1〜S:C-5） | 4（X:C-1〜X:C-4） |

**サブエージェントも Codex も A が 0 件。止める基準を満たしたので、実装のレビューを止める。** 両方とも、1 回目の直しが新しい欠陥を入れていないことを確かめた。

### 反映（2026-10-12）

| 指摘 | 直したところ |
|---|---|
| S:B-1 | 直した後のコードで、実機の Share の 45 件を流した（45/45、補助の終了コード 0）。Direct Share のアイコンのログが、実機でバイト数で出ることも確かめた（`icon: (200 bytes)` など） |
| X:B-1 | `ShareChooserActionCallbackCoordinatorTests` に、per-call の欄が `volatile` であることを反射で固める 1 本を足した |
| S:C-1、X:C-2、X:C-3、S:C-3 | 実装結果 v2 に、ログの単位、`volatile`、`Instance` の前提、層 2a の本数（20 本、36/36）を書いた。`testing.md` と親課題の README の本数も直した |
| S:C-2 | リスナーの登録の例外を `LogError`（`{e}`）にした。失敗の戻り値は今までどおり `LogWarning` |
| S:C-4 | XML コメントを「UnityMain から呼んだときは、結果は呼び出しの後」に直した |
| X:C-1 | Share 設計 v7 の J-S16 に `Instance` の前提を書いた（K-4、K-5、実装との違いと一緒に） |
| X:C-4、S:C-5 | URP の設定、`.slnx`、テストの一時ファイルは、テストの実行が作ったもの。元に戻した。コミットに入れない |
