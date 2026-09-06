# レビュー結果

- 日付: 2026-09-03
- 対象ファイル: artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md
- 前版レビュー: artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v6.md
- 機能名: clipboard / プラットフォーム: macOS / 種別: 実装計画書
- レビュー実施: **Codex（別モデル）**。v6 で自身が出した A1 3 件 + B 1 件の反映確認に絞ったラウンド
- 機械照合: `scripts/check_design_consistency.py` は 17 項目すべて OK（Codex 自身も実行して確認）

---

## 結論

v6 の4指摘は概ね反映されていますが、teardown と進行中 Start の競合に新規 A1 があります。段2以降への着手は不可です。

| 前回指摘 | 判定 |
|---|---|
| A1-1 active / pending 分離 | **反映されたが新たな問題あり** |
| A1-2 `changeCount` の `long` 化 | **反映済み** |
| A1-3 `default(T)` の定義 | **反映済み** |
| B-1 PlayMode 回帰条件 | **反映済み** |

## A1（重大な設計欠陥）

### 新規 A1: Start pending 中の teardown で、破棄後にネイティブ監視が再開し得る

通常ライフサイクルの active/pending 遷移は正しいです。ネイティブの interval 検証→scope 解決→stop の順序とも一致しています。

- [ClipboardChangeMonitor.swift:72](native-toolkit:mac/MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift:72)
- [ClipboardChangeMonitor.swift:78](native-toolkit:mac/MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift:78)
- [ClipboardChangeMonitor.swift:80](native-toolkit:mac/MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift:80)
- [IT-09:38](native-toolkit:mac/MacLibrary/MacLibraryTests/Clipboard/Presentation/ClipboardChangeMonitorTests.swift:38)
- [v7:1321](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:1321)

ただし、[v7:1378](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:1378) の「Start 応答待ちでも teardown の直接 Stop は安全」は成立しません。

実際の経路は次のとおりです。

1. Start は独立した `Task { @MainActor }` を投入して直ちに戻る（[UnityMacClipboardManager.swift:392](native-toolkit:mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift:392)）。
2. その実行前に `OnDestroy` が Stop を呼ぶと、Stop も別の `Task { @MainActor }` を投入する（[同:417](native-toolkit:mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift:417)）。
3. MainActor は排他実行を保証しますが投入順の実行は保証しません。[Swift公式仕様](https://github.com/swiftlang/swift-evolution/blob/main/proposals/0392-custom-actor-executors.md)
4. Stop→Start の順に実行されると、Stop 後に Start が監視を開始する。Start 完了は tombstone により捨てられ、再 Stop されないため、破棄後もネイティブ poller が残る。

tombstone は managed 配送を捨てるだけで、ネイティブ監視の再開を防ぎません。コンパイル・現在のテストとも通り得るため A1 です。

修正条件は、少なくとも「teardown 時に Start が pending なら、遅延 Start 完了後に Stop を再発行する」等の順序保証を設計し、その回帰テストを追加することです。

## A2（軽微な設計欠陥）

なし。

## B（記述不備・検証不足）

- **B-1: teardown 競合テストがない。** [7.2:1655](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:1655) は通常の active/pending 遷移だけで、Start pending 中の `OnDestroy` と Stop/Start 逆順実行を検証しません。Stop 完了失敗時の active 維持と、破棄時に active/pending が双方消えることも明示テストが必要です。

- **B-2: 撤去済みシーム名が残存。** [2.3:356](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:356) に `PendingObservationGenerationForTests` が残っています。5.6.10・12章の `HasPendingChangeRegistrationForTests` と不一致です。

- **B-3: `long` 化後の型表記が不一致。** [5.5.1:874](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:874) が値型を「`int` / `bool` / enum」としたままです。`ChangeCount` は `long` に修正済みです。

- **B-4: V-13 が段4 DoDから漏れている。** [V-13:1735](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:1735) を追加した一方、段4条件は [v7:1783](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:1783) で「V-1〜V-12」のままです。

## その他の確認結果

- `changeCount` はスキーマ、parser、公開型、テストで `long` に統一済みです。[TryGetInt64 方針:851](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:851)
- `matchingItemIndexes` はコレクション添字なので `int` 維持で妥当です。ネイティブも配列の index を返しています。[Swift:84](native-toolkit:mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardJsonParser.swift:84)
- `default(T)` の未初期化値化と factory 値限定の不変条件、テスト方針は整合しています。[v7:882](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v7.md:882)
- 通常運用ではネイティブ完了は各呼び出し1回で、共有 single-flight により世代カウンタ撤去は成立します。問題は single-flight を迂回する teardown です。
- 機械照合は17項目すべて成功しましたが、上記の意味的競合と残存名は検出対象外でした。

## 着手可否

**不可。**

新規 A1 の teardown 順序保証を設計へ追加し、Start pending 中の破棄を回帰テストへ固定した後に再確認が必要です。

設計・コード・レビュー文書は編集していません。[review-document スキル](.codex/skills/review-document/SKILL.md)の一次情報照合と実装可能性基準を適用しました。
