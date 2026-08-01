# Android Clipboard 実装レビュー v1

## レビュー概要

- 日付: 2026-07-26
- 機能名: clipboard
- 対象プラットフォーム: Android
- レビュー対象計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md`
- レビュー対象結果: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v1.md`
- レビュー対象差分: `develop...HEAD` に差分が無かったため、ユーザー確認のうえローカル未コミット差分を対象にレビュー
- 総合判定: 修正が必要

設計 v4 は同期 API と非同期 callback API の切り分け、StartObserving の非通知契約、JSON DTO 方針、実機未確認項目を明確にしており、実装の大枠はこの方針に沿っている。一方で、callback 例外分離の実装に設計違反があり、また結果レポートと実際の作業ツリーに不整合が残っている。

## 重大な問題（high）

### 1. 共通 event の例外で per-call callback が呼ばれない

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardManager.cs:492`
- 関連テスト: `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidClipboardManagerDispatchTests.cs:70`
- 関連計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md:1035`

`InvokeInOrder` は `common?.Invoke(result)` と `perCall?.Invoke(result)` を同じ `try` ブロック内で実行しているため、`ClipboardOperationCompleted` の購読者が例外を投げると、その後の per-call callback が実行されない。

設計 v4 は「共通 event → per-call callback の順」と「どちらかが例外を投げても他方の呼び出しと全体の完了が阻害されないこと」をテスト観点として要求している。しかし現状のテストは per-call 側が例外を投げるケースだけを確認しており、共通 event 側が例外を投げた場合に per-call callback が保持されることを検証していない。

このままだと、グローバル購読者側のバグが個別呼び出し元の完了通知を奪うため、コピー/クリア/監視停止 API の利用者が結果を受け取れない可能性がある。

推奨修正:

- `common` と `perCall` を別々の `try/catch` で囲み、片方の例外がもう片方の実行を止めないようにする。
- `InvokeInOrder_CommonThrows_PerCallStillInvokedAndExceptionSwallowed` 相当の EditMode テストを追加する。

## 改善提案（medium）

### 1. 実装結果レポートと作業ツリーの変更内容が一致していない

- 対象: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v1.md:22`
- 対象: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v1.md:47`
- 対象: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v1.md:56`
- 関連計画: `artifact/designs/clipboard/2026-07-26-android-clipboard-design-v4.md:185`

結果レポートは「AAR 差し替えは行っていない」「既存変更なし」「`Plugins/Android/*.aar` は差し替えなし」と記載しているが、作業ツリーには以下の差分がある。

- `Plugins/Android/*-1.2.0.aar` と `.meta` の削除
- `Plugins/Android/*-1.3.0.aar` と `.meta` の追加
- `agent-rules/coding-rules/common.md` の変更

設計 v4 は 1.3.0 AAR が同梱済みで差し替え不要としており、DoD でも既存 Runtime / Tests への意図的変更なしを掲げている。実際に AAR 差し替えが必要だった可能性はあるが、その場合は設計または結果レポートへ正しく反映する必要がある。

推奨修正:

- AAR 差し替えが意図した変更なら、結果レポートの変更ファイル・DoD・非変更欄を更新する。
- `agent-rules/coding-rules/common.md` の変更が今回の機能実装に不要なら、実装差分から外す。必要なら別作業として明示する。

### 2. `common.md` のルール変更が実装計画の対象外

- 対象: `agent-rules/coding-rules/common.md:119`
- 関連結果: `artifact/results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v1.md:47`

`agent-rules/coding-rules/common.md` に同期/非同期 API と Awaitable 併設ルールが追加されているが、clipboard 実装結果レポートでは既存変更なしとされており、このルール変更はレビュー対象計画にも含まれていない。

プロジェクトルール自体の変更は後続実装全体へ影響するため、feature 実装差分に混ざるとレビュー範囲と責任境界が曖昧になる。

推奨修正:

- 今回の clipboard 実装に不要であれば差分から外す。
- 採用するなら、設計/結果に「ルール変更を含む」と明記し、`.github/instructions/csharp-coding.instructions.md` との同期要否も確認する。

## 軽微な指摘（low）

### 1. public/internal メソッドの先頭ログルールに未対応の箇所がある

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardJsonBuilder.cs:21`
- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/AndroidClipboardJsonParser.cs:61`
- 関連ルール: `agent-rules/coding-rules/csharp.md:7`

`AndroidClipboardJsonBuilder` の public static メソッドと `AndroidClipboardJsonParser` の internal static メソッドは、C# ルール上は先頭行 `Debug.Log` の対象に見える。一方で clipboard は秘匿値を扱うため、生成 JSON や本文をログに出さない設計判断は妥当。

推奨修正:

- ルールを厳密適用するなら、本文や JSON を出さず、長さ・件数・フラグだけをログに出す。
- あえてログを省略するなら、clipboard の秘匿情報保護を理由にした例外として設計/結果へ明記する。

## 実装計画整合性チェック

- 同期 3 メソッド（`Read` / `HasClip` / `GetDescription`）を戻り値 API とし、event / callback を使わない方針は計画どおり。
- 非同期 6 メソッドを共通 event + per-call callback とする構成は計画どおり。
- `StartObserving` に callback を持たせず、失敗時はログのみで return する方針は計画どおり。
- JSON DTO を `[Serializable]` + public フィールドにする方針は計画どおり。
- `copyPlainText` の blank を C# 側で独自バリデーションしない方針は計画どおり。
- ただし callback 例外分離は計画 7.1 のテスト観点を満たしていない。
- AAR 差し替えなし/既存変更なしという計画・結果と、実際の作業ツリーが一致していない。

## プロジェクトルール適合チェック

- Manager + Bridge パターン、`#if UNITY_ANDROID` ガード、`AndroidJavaObject` / `AndroidJavaProxy`、`UnityMainThreadDispatcher` 経由の dispatch は既存 Android Manager 方針に沿っている。
- XML コメントと実装コード内コメント/ログ文言は概ね英語で統一されている。
- クリップボード本文や生成 JSON をログに出さない方針は、秘匿情報保護の観点で妥当。
- public/internal メソッドの先頭ログルールについては、Builder/Parser で未対応または例外扱いが未文書化。
- `agent-rules/coding-rules/common.md` の変更は実装計画外であり、実装レビュー対象に混在させるべきか整理が必要。

## テストカバレッジ

結果レポート上は macOS EditMode 197/197、Android ビルドターゲット EditMode 204/204 が成功している。ただし本レビューではテスト再実行はしていない。

カバレッジは JSON builder/parser と dispatch helper を中心に十分厚いが、重大指摘のとおり `common` 側が例外を投げた場合の per-call callback 継続テストが欠けている。実機手動確認は結果レポートどおり未実施であり、特に `stopObserving` の 0 引数 JNI 解決、foreground 制限下の read/observe 挙動、実際の clipboard payload の相互運用は次工程前に確認が必要。

## 総合評価

実装の方向性は設計 v4 とよく揃っており、同期/非同期境界や native JSON 仕様の反映は概ね良好。ただし、callback 例外分離の不備は API 利用者の完了通知を失わせるため、承認前に修正が必要。

加えて、結果レポートと実際の作業ツリーの不一致は、後続レビューやコミット時の事故につながる。AAR 差し替えとルールファイル変更の扱いを明確化したうえで、結果レポートも実態に合わせることを推奨する。
