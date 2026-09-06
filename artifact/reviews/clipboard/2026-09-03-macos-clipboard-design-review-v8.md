# レビュー結果

- 日付: 2026-09-03
- 対象ファイル: artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md
- 前版レビュー: artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v7.md
- 機能名: clipboard / プラットフォーム: macOS / 種別: 実装計画書
- レビュー実施: **Codex（別モデル）**。段 3 着手前の**監視領域に絞った**ラウンド
- 対象範囲: §1.6 の監視契約 / §5.6.5（active-pending 分離）/ §5.6.8 の監視部分（teardown 競合・D-16）/ §5.6.10 の監視シーム / §7.2 の `(4)` 項目 / §12 の段分割判断
- 範囲外: Builder / Parser / 結果型 / payload 型 / 段 1・段 2 の実装済み部分
- 機械照合: `scripts/check_design_consistency.py` は全項目 OK（Codex 自身も実行して確認）。ただし C-1 の旧段番号は同スクリプトの検査対象外
- ネイティブ照合: Codex が native-toolkit の Swift 実装を直接読み、計画書が主張する契約 4 件を行番号付きで裏取りした
- Codex session ID: `01a0671e-e9ca-7451-9773-4d153038c859`

---

## 結論

A1 / A2 の設計欠陥は見つかりませんでした。active / pending の 2 スロット方式と D-16 は、Swift 正本の実行モデルに照らして成立しています。

ただし、実装着手前に直すべき B が 2 件、段再編の C が 1 件あります。特に D-16 のテストカウンタは、現状の記述だと壊れた実装を通す可能性があります。したがって最終ゲートは「B 2件と C 1件を計画書で明確化後に通過」が妥当です。

分類集計:

- A1: 0
- A2: 0
- B: 2
- C: 1

## 強み

- Swift ファサードの Start / Stop が独立した非構造化 `Task { @MainActor in ... }` を作る点を正しく捉えています。[UnityMacClipboardManager.swift:379](/Users/jonghyunkim/Desktop/native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift:379)、[同:415](/Users/jonghyunkim/Desktop/native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift:415)
- IT-09 の根拠は正確です。interval 検証と scope の解決が `stop()` より前にあり、それ以降に通常の失敗点はありません。[ClipboardChangeMonitor.swift:68](/Users/jonghyunkim/Desktop/native-toolkit/mac/MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift:68)
- 変更イベントの encode 失敗が黙殺されるという記述もソースと一致します。[UnityMacClipboardManager.swift:394](/Users/jonghyunkim/Desktop/native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift:394)
- `checkForegroundChange` と監視が同じ scope 別 tracker を共有し、併用を避けるべきという説明も正確です。[MacClipboardManager.swift:520](/Users/jonghyunkim/Desktop/native-toolkit/mac/MacLibrary/MacLibrary/Clipboard/Manager/MacClipboardManager.swift:520)、[ClipboardChangeTracker.swift:18](/Users/jonghyunkim/Desktop/native-toolkit/mac/MacLibrary/MacLibrary/Clipboard/Application/UseCase/ClipboardChangeTracker.swift:18)
- `scripts/check_design_consistency.py` は対象文書に対して全項目通過しました。ただし、下記の旧段番号は同スクリプトの検査対象外です。

## 改善点

### B-1: `TeardownStopReissueCountForTests` の計数定義が曖昧で、壊れた D-16 実装を通しうる

- 分類: B
- 該当箇所: §5.6.10、§7.2
- 引用:
  - 「`TeardownStopReissueCountForTests` — D-16 の再発行が起きた回数」
  - 同時に「`StopObservingForTeardown` の呼び出し自体を数えて観測する」
  - 「OnDestroy のあとに Start の成功完了を注入すると、`clipboardStopObserving` が再発行されること」

根拠: [計画書:1441](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1441)、[同:1704](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1704)

問題となる筋道:

1. `OnDestroy` 自体が最初に `StopObservingForTeardown` を呼びます。
2. カウンタを「同ヘルパの全呼び出し回数」として実装すると、Start の遅延完了前から値は 1 です。
3. D-16 の再発行を実装し忘れていても、テストが単に `count == 1` を確認すれば通ります。
4. 一方、名前と前半の説明は「初回 teardown Stop を除いた再発行回数」とも読めます。

段 2 の現行 `OnDestroy` も、段 3 で空実装を同ヘルパへ置換する場所を明確に示しています。[MacClipboardManager.cs:230](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/MacClipboardManager.cs:230)

推奨修正:

次のどちらかに固定してください。

- 推奨: 「全 teardown Stop 発行回数」と定義し、テストを `OnDestroy` 直後は 1、遅延 Start 成功後は 2、遅延 Start 失敗後は 1 と明記する。
- または、D-16 分岐でだけ増える専用の `TeardownStopReissueCountForTests` とし、成功時 `0 → 1`、失敗時 `0` とする。

加えて `ResetForTests()` がこのカウンタを 0 に戻すことを明記してください。

### B-2: pending 中の再 Start に届くイベントの配送先を直接検証するテストがない

- 分類: B
- 該当箇所: §5.6.5、§7.2
- 引用:
  - 「再 Start の処理中に旧監視のイベントが届いても、まだ昇格していない新 callback には流れない」
  - テスト側は「再 Start 失敗のあと」「再 Start 成功時」の配送先だけを記載

根拠: [計画書:1346](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1346)、[同:1696](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1696)

問題となる筋道:

1. 旧 callback A を active にした状態で、callback B を指定して再 Start を開始します。
2. ネイティブ Start 完了をまだ注入していない間に変更イベントを注入します。
3. 正しい配送先は A だけです。
4. 現在のテスト項目は失敗完了後と成功完了後を検査しますが、この pending 中の窓を直接検査しません。
5. 誤って Start 段階 7 で active を B に差し替える実装でも、完了時の巻き戻し処理次第では記載済みの完了後テストを通せます。

Swift 側では native Start 本体と成功 callback の間に `await` がなく、監視切替後に直ちに handler が呼ばれます。[UnityMacClipboardManager.swift:392](/Users/jonghyunkim/Desktop/native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift:392)  
一方、Task が実行を開始するまでは旧監視が生きているため、この pending 中配送は実在する経路です。

推奨修正:

§7.2 の active / pending テストへ次を追加してください。

> 成功済み Start A → 再 Start B を pending のまま保持 → `DeliverChangeEventForTests` を実行し、A だけが発火して B は発火しないこと。その後、B を成功完了させ、次のイベントでは B だけが発火すること。

既存の `DeliverChangeEventForTests` で駆動可能なので、新しいシームは不要です。

### C-1: v10 の 3 段構成に旧「段 4」表記が残り、段 3 の observation DoD が自己矛盾している

- 分類: C
- 該当箇所: §5.6.8、§7.2、§10、§12
- 引用:
  - §5.6.8: 「段 3 では `stop` に空実装」「段 4 で差し替える」
  - §7.2: 「`(3)` / `(4)`」「段 3 の DoD は `(3)` の項目だけ」
  - observation テストはすべて `(4)`
  - §10: 「段 4 のみ」「段 4 完了時」
  - §12: 「3 段に分ける」「監視に関わるものはすべて段 3」

根拠: [計画書:1391](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1391)、[同:1682](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1682)、[同:1696](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1696)、[同:1820](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1820)、[同:1854](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v10.md:1854)

問題となる筋道:

文書を字義どおり読むと、最終の段 3 DoD は `(3)` だけを要求するため、`(4)` の observation テスト、D-16、Stop 差し替えが段 3 の完了条件から外れます。実装段自体は §12 で明確ですが、ゲート判定が不能です。

推奨修正:

- §5.6.8 の「段 3／段 4」を「段 2／段 3」に変更。
- §7.2 の全 `(4)` を `(3)` に変更し、冒頭を現行 3 段に合わせる。
- §10 の「段 4」を「段 3」に変更。
- 変更後、段 3 DoD に observation active/pending、D-16、空 stop の置換が明示的に含まれることを再照合する。

## 「特に疑ってほしい点」6項目の確認結果

1. teardown Stop と進行中 Start の交差

   指摘: 無し。

   次の全順序を確認しました。

   - Start Task → teardown Stop Task
   - teardown Stop Task → Start Task
   - active がある再 Start
   - 初回 Start
   - interval / scope 失敗
   - P/Invoke 前の拒否・P/Invoke 例外

   Stop→Start の場合でも、Start は実際に監視を開始してから成功 callback を同期的に呼ぶため、その callback から発行する Stop は必ず当該 Start より後になります。Start→Stop の場合は最初の teardown Stop が停止し、再発行は冪等な追加 Stop になります。[UnityMacClipboardManager.swift:392](/Users/jonghyunkim/Desktop/native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift:392)、[同:417](/Users/jonghyunkim/Desktop/native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardManager.swift:417)

2. 成功した Start だけを再発行対象にする判断

   指摘: 無し。

   interval 検証と `readChangeCount(scope)` が `stop()` より前にあります。`stop()` 後の処理には通常の throw 経路がないため、失敗 callback は新監視を開始しておらず、既存監視も残しています。[ClipboardChangeMonitor.swift:72](/Users/jonghyunkim/Desktop/native-toolkit/mac/MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift:72)  
   IT-09 も scope 解決失敗後に旧監視が生きていることを確認しています。[ClipboardChangeMonitorTests.swift:36](/Users/jonghyunkim/Desktop/native-toolkit/mac/MacLibrary/MacLibraryTests/Clipboard/Presentation/ClipboardChangeMonitorTests.swift:36)

3. active / pending 状態遷移表の全域性

   指摘: 無し。

   表の契機に加え、段階 1〜6 の拒否は直前の §5.6.4 で「両スロットに触れない」と定義されています。SubsystemRegistration / `ResetForTests` は `ClearAllPendingCallbacks` に包含されます。ネイティブが scope 読み取り失敗で自律停止する場合は C# callback がなく、managed の登録スロットを変化させる契機ではありません。

   `onChanged == null` の Start では pending の値も null になりますが、進行中操作の所有者は共有 in-flight キーで一意になり、機能上必要な状態は失われません。

4. 変更イベントの配送先が active だけであること

   設計判断への指摘: 無し。テスト設計には B-2 あり。

   Start Task が実行されるまでは旧 native 監視が有効なので active への配送が正しく、native の監視切替と成功 callback の間には suspension point がないため、成功完了処理で active を新 callback に昇格する境界も正しいです。  
   ただし pending 中の配送先を直接テストする項目を追加すべきです。

5. §7.2 の `(4)` テストとシーム粒度

   指摘: B-1、B-2、C-1。

   `CompleteObservationControlForTests`、`DeliverChangeEventForTests`、active / pending の観測シームは基本的に十分です。追加シームは不要です。一方で、D-16 カウンタの定義と期待値を固定する必要があります。また `(4)` は現行の段 3 に修正しない限り DoD から漏れます。

6. 監視を段 3 に置き、段 2 の stop を空実装にする判断

   指摘: 無し。

   実装済み段 2 の `MacClipboardManager` には Start / Stop / change delegate / active registration が存在せず、`OnDestroy` の空 stop と説明が一致しています。[MacClipboardManager.cs:240](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/MacClipboardManager.cs:240)  
   段 2 API から native poller を開始する経路がないため、段 2 単独では停止対象が存在せず無害です。段 3 で `StopObservingForTeardown` へ置換することだけを、C-1 の修正後の DoD で確実にゲートしてください。

## 総合評価

active / pending への置換によって、世代カウンタが必要としていた「複数の observation control callback の所有者識別」が欠落する問題はありません。共有 single-flight キーにより managed 上の未完了制御呼び出しは最大 1 件であり、teardown だけは tombstone と D-16 で別途閉じられています。

実装方針そのものは承認可能です。ただし、B-1、B-2、C-1を計画書へ反映してから段 3 に着手することを推奨します。

`review-document` スキルのレビュー基準を使用しましたが、読み取り専用指定を優先し、レビュー結果ファイルや計画書は変更していません。

Codex session ID: 01a0671e-e9ca-7451-9773-4d153038c859
Resume in Codex: codex resume 01a0671e-e9ca-7451-9773-4d153038c859
