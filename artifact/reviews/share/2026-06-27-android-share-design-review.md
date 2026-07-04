# レビュー結果

- 日付: 2026-06-27
- 対象ファイル: `/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/share/2026-06-27-android-share-design-v3.md`
- 機能名: share
- 対象 OS: Android

---

## 強み

- native-toolkit 側の追加 API、`shareText` 限定の chooser action 登録、API 34+ 制約、動的 receiver の前提が整理されており、Unity 側で何を接続すべきかが分かりやすい。
- 既存 v2 の C# 実装（`ShareOperationCompleted` / `ShareCallbackReceived`、per-call callback、`UnityMainThreadDispatcher`）を踏襲する方針になっていて、既存 API 互換性への配慮がある。
- `ShareChooserActionResult` をプラットフォームガードなしにして EditMode テスト可能にする方針は、既存 `ShareOperationResult` / `ShareCallbackResult` と整合している。
- `AndroidJavaProxy` の IL2CPP 制約（public method / interface 名一致）に触れており、実装時の事故を避けやすい。
- サンプルシーンを別スキルに切り出すスコープ整理は妥当。

## 改善点

### 高優先度

1. セクション 7「テスト方針」
   - 問題: unity-native-plugin の `agent-rules/coding-rules/common.md` は「Manager の初期化・イベント購読・コールバック転送は EditMode テストで検証する」としているが、本設計は `AndroidShareManager` 本体を `#if UNITY_ANDROID` + `AndroidJavaObject` 依存のため EditMode 不可として手動確認に寄せている。
   - 影響: `Initialize` で `setShareChooserActionListener` が呼ばれること、`onChooserAction` が `ShareChooserActionTapped` と per-call callback に正しい順序で転送されること、`OnDestroy` で clear と callback null 化が行われることが自動テストされない。
   - 提案: native 呼び出し部分を薄い internal adapter/interface に切り出す、または `FireChooserAction` / proxy dispatch を `internal` + `InternalsVisibleTo` などで検証可能にし、少なくとも callback 転送・順序・last-registered wins・OnDestroy cleanup を EditMode テストに含める。

2. セクション 1.4 / 4.3「AAR 同梱状況」
   - 問題: chooser action 対応版 AAR への差し替えが「別タスク・依存指摘スコープ」扱いで、変更ファイル一覧・実装順序・DoD に入っていない。
   - 影響: C# 側だけ実装しても現行 AAR が旧版なら `setShareChooserActionListener` 呼び出し時に native method 不存在で失敗し、実機動作しない。`Initialize` の listener 登録失敗時の扱いも設計されていない。
   - 提案: 本 feature の実機動作条件として AAR 差し替えを実装タスクまたは明示的な前提チェックに昇格する。少なくとも `Initialize` で `setShareChooserActionListener` 呼び出しを try/catch し、未対応 AAR の場合に既存 share 機能を壊さない方針を記載する。

### 中優先度

1. セクション 5.2「ShareText シグネチャ」
   - 問題: `ShareText` に第3引数を追加する方針は後方互換だが、`ShareWithCallback` には chooser action callback を付けない理由が API 表だけでは伝わりにくい。`ShareTextPayload` 自体は `ShareWithCallback` でも使われるため、利用者が `ShareWithCallback(payloadWithChooserActions)` でも chooser action callback を期待しやすい。
   - 提案: public API コメントに「chooser action callback は `ShareText` のみ。`ShareWithCallback` の chooserActions は native 側で callback 登録されない」ことを明記し、必要なら `ShareWithCallback` 呼び出し時に chooserActions が含まれている場合の警告ログ方針を追加する。

2. セクション 5.2「_pendingChooserActionCallback」
   - 問題: per-call callback を発火後にクリアしない設計は複数タップ対応として理解できるが、共有シートをキャンセルした場合や action 未タップのまま別操作を行う場合、古い callback が次回 `ShareText` まで残る。
   - 提案: 「次の `ShareText` / `OnDestroy` まで保持」という契約に加えて、`ShareText` が `onChooserAction == null` で呼ばれた場合に `_pendingChooserActionCallback = null` へ更新するか、古い callback を残すのかを明文化する。通常は last-registered wins の一貫性から、null も登録値として扱い古い callback を消す方が安全。

3. セクション 6「エラーケース一覧」
   - 問題: native 側の proxy 実装例外を「native 側で catch」としているが、C# の `FireChooserAction` 内で `UnityMainThreadDispatcher.Instance.Enqueue` 取得や result 生成が例外を投げた場合の扱いは未定義。
   - 提案: proxy method は極力薄くしつつ、`FireChooserAction` 側でログと安全な破棄を行うか、既存 Manager と同様に例外は Unity 側ログで観測する方針を記載する。

4. セクション 4.1 / 5.1「ShareChooserActionResult」
   - 問題: `ActionId` が null/empty の場合の不変条件がない。native parser は非空 intentAction のみ callback 登録する前提だが、C# public struct としては任意文字列を受け取れる。
   - 提案: 既存 Result 型の方針に合わせ、constructor で null を `string.Empty` に正規化するのか、`ArgumentNullException` にするのかを明記し、`ShareResultTests` に null/empty の扱いを追加する。

5. セクション 7「テスト方針」
   - 問題: `AndroidShareJsonBuilder` は既存 chooserActions シリアライズテストで担保としているが、callback 用の実用条件（独自 `intentAction` 必須、SEND/空は callback 不発）に対する C# 側のテスト追加がない。
   - 提案: builder が `intentAction` を含めるケース、未指定では含めないケース、複数 action の JSON を維持するケースを明示的にテスト対象として残す。既存テストがある場合も、今回の callback 契約の根拠としてテスト名を列挙する。

### 低優先度

1. セクション 1.1 の参照リンク
   - 問題: `UnityAndroidShareManager.kt:57-134` のような表示と `#L143-L169` 付きリンクが混在している。
   - 提案: Markdown リンク形式を統一し、行番号つきリンクに寄せるとレビュー・実装時に追いやすい。

2. セクション 6「要検証事項」
   - 問題: API 34+ 表示数上限が要検証になっているが、Android 仕様の最大 5 件前提と入力側制約をどう扱うかが設計に落ちていない。
   - 提案: Unity 側では最大 5 件を推奨/警告にするだけなのか、JSON builder/Manager で切り詰めるのかを明記する。

## 不足項目

- Definition of Done がない。少なくとも「listener 登録/解除」「callback 転送順序」「per-call callback の置換/クリア」「未対応 AAR 時の安全性」「AAR 差し替えまたは前提確認」「EditMode/実機確認」の完了条件を列挙するべき。
- 実装タスクに AAR 差し替え、または未対応 AAR を検出した場合の degrade 方針がない。
- `OnDestroy` で `clearShareChooserActionListener` を呼ぶ順序と、`pluginInstance.Dispose()` との順序がタスク化されていない。
- public XML コメントに入れるべき契約（API 34+ 限定、`ShareText` 限定、`ShareWithCallback` では chooser action callback 不可、callback は未タップ時に発火しない）が明文化されていない。
- 手動確認はあるが、Unity Test Runner で実行する EditMode テストの具体ケースが不足している。

## 総合評価

設計の方向性は良いが、現状のままだと実装後に「C# は配線したが AAR が未対応」「コールバック転送の肝が手動確認のみ」という状態になりやすい。特に、このリポジトリの共通ルールは Manager の初期化・イベント購読・コールバック転送を EditMode テスト対象としているため、テスト可能な境界を少し作ってから実装に進むのがよい。AAR 差し替え/未対応時の安全策と DoD も追加すれば、実装可能性はかなり高い。
