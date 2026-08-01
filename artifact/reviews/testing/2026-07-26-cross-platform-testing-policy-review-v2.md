# レビュー結果

- 日付: 2026-07-26
- 対象ファイル: `agent-rules/coding-rules/testing.md`
- 機能名: testing
- プラットフォーム: cross-platform
- 前回レビュー: `artifact/reviews/testing/2026-07-26-cross-platform-testing-policy-review-v1.md`

---

## 強み

- 前回の高優先度指摘だったコンパイルガード分類は、全 12 Manager を A/B 群に分ける形へ修正され、実装と一致している。
- 層 2b と層 3について、共有できるデバイス基盤と別保守になるテストハーネスを明確に分離した。
- EditMode の責務を純粋ロジックへ限定し、`common.md` 側も同じ契約へ更新されている。
- Player テストに IL2CPP、architecture、結果回収などの実行契約が追加され、「Player 上で通った」ことと「IL2CPP を検証した」ことが区別されている。
- AppiumIME の復元、Apple の pasteboard privacy、Windows UI automation の現行方針が反映されている。
- UI 操作 helper、非同期待機、機微情報、外部ツールの鮮度管理が追加され、前回の不足項目が未定義事項として追跡可能になった。

## 改善点

### 高優先度

- なし。

### 中優先度

#### 1. EditMode の適用状況で Dialog を「実装済み」とする根拠がない

- セクション: `7. 適用状況`
- severity: medium
- 問題点:
  - `testing.md:266` は EditMode を `dialog / notification / share / clipboard` すべて実装済みとしている。
  - `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/` と `Tests/PlayMode/` には Dialog を対象とする NUnit / Unity Test Framework テストが存在しない。
  - Notification も Android / macOS / Windows の一部ロジックは検証されているが、iOS を含む全プラットフォームが実装済みとは読めない。
  - 1節の新しい定義では純粋ロジックが存在しない機能は「実装済み」ではなく「対象なし」になりうるため、機能名だけの集約では適用状況を正確に表現できない。
- 改善提案:
  - 層 1 の状況を `部分的` に変更し、機能・プラットフォーム・検証対象を列挙する。
  - Dialog に層 1 対象の純粋ロジックがないなら `N/A` と明記し、未実装と区別する。
  - 「テストファイルが1つ存在する」と「対象契約を網羅した」を区別する完了条件を定義する。

#### 2. AltTester が UI Toolkit を探索できないという採用判断の根拠が古い

- セクション: `4. 採用しない選択肢 > AltTester`
- severity: medium
- 問題点:
  - `testing.md:205-216` は AltTester の探索対象を GameObject / Component に限定し、UI Toolkit の `VisualElement` は見えないため採用動機がないとしている。
  - AltTester は 2.2 で UI Toolkit 対応を追加し、現行 2.3 系では UI Toolkit 要素の探索・操作と Tap の対応が案内されている。
  - UI Toolkit 対応は non-GPL の Pro / Enterprise 向けという制約があるが、「将来対応されたら再評価」ではなく、すでに利用可能な有償機能である。
  - 不採用という結論自体は維持できるものの、現状の技術的不可能性を根拠にはできない。
- 改善提案:
  - 「UI Toolkit を探索できない」という説明を削除する。
  - 不採用理由を、追加SDKによるPlayerのinstrumentation、外部サーバー、ライセンス費用、CI依存、Unity Test Frameworkで現在の要求を満たせることへ置き換える。
  - AltTester 2.2以降のnon-GPL版ならUI Toolkit対応済みであることを代替案として記録する。

#### 3. `NSPasteboard.AccessBehavior` の導入バージョンと適用範囲が不正確

- セクション: `2. OS 境界層のプラットフォーム別ツール > macOS`
- severity: medium
- 問題点:
  - `testing.md:116,145-149` は `NSPasteboard.AccessBehavior` を macOS 15+ とし、最小対応バージョンがmacOS 15なので全対象バージョンに該当するとしている。
  - Xcode 26.3付属のmacOS 26.2 SDKにある `NSPasteboard.h` では、`NSPasteboardAccessBehavior` と `accessBehavior` は `API_AVAILABLE(macos(15.4))` である。
  - 最小バージョンがmacOS 15.0を意味するなら、15.0〜15.3は同じAPI契約に含められず、「全対象バージョン」という結論は成立しない。
- 改善提案:
  - 表と本文を `macOS 15.4+` に修正する。
  - macOS 15.0〜15.3と15.4以降を分け、実際のpasteboard privacy挙動を対象OSで確認する。
  - テストコードから `accessBehavior` を参照する場合はavailability checkを必須とする。

#### 4. 鮮度管理ルールが現在記載している外部仕様には適用されていない

- セクション: `5. 外部ツール情報の鮮度管理`
- severity: medium
- 問題点:
  - `testing.md:234-242` は一次資料URLと最終確認日を「層 3 に着手する際」に要求している。
  - 一方、2節と4節ではすでにAppium、Apple、Microsoft、AltTesterの現行仕様を根拠に採否と制約を断定しているが、本文に出典・確認日・対象バージョンがない。
  - 今回のmacOSとAltTesterの誤りは、着手時まで確認を延期すると正本内の古い判断が残ることを示している。
- 改善提案:
  - 外部仕様を本文へ追加・更新した時点で、一次資料URL、確認日、対象バージョンを併記するルールへ変更する。
  - 2節と4節の現在の主張にも確認資料を追加する。
  - 層 3 着手時の再確認は、初回確認とは別の再検証ゲートとして残す。

### 低優先度

#### 1. build target の復元ルールがローカル作業とCIを区別していない

- セクション: `3. Manager ごとのコンパイルガード差異`
- severity: low
- 問題点:
  - `testing.md:193-194` はバッチ実行後に必ず元のtargetへ戻すとしている。
  - ローカルのdirty worktreeでは妥当だが、使い捨てCI workspaceでは不要な再importになりうる。
  - `.slnx` に差分が生じるという説明も、Unityバージョンやsolution再生成条件に依存する。
- 改善提案:
  - ローカル実行では元targetへ復元し、使い捨てCIでは不要と分ける。
  - 復元理由を特定ファイルではなく、開発workspaceの生成物・設定差分を残さないこととして表現する。

#### 2. 機微情報をアサーション出力へ残さない具体策が不足している

- セクション: `6. 機微情報の取り扱い`
- severity: low
- 問題点:
  - NUnitの等値アサーションは失敗時にexpected / actualを自動表示するため、「本文を出さない」という禁止だけでは実装者が安全な比較方法を判断できない。
  - サンプル値だけを使えるテストでは問題ないが、他アプリ由来データを扱う層 3では実値が失敗ログに出る可能性がある。
- 改善提案:
  - 実値を直接比較せず、長さ、型、事前計算したdigest、またはredacted matcherで検証する規約を追加する。
  - サンプル値のみを扱うテストと実データを扱いうるテストでログ規約を分ける。

## 不足項目

- 層 1 の「実装済み / 部分的 / N/A」を判定する完了条件。
- 現在の機能・プラットフォーム別テストカバレッジ表。
- 外部ツール・OS APIごとの一次資料、確認日、対象バージョン。
- AltTesterを技術面・ライセンス面・運用面で不採用とする比較結果。
- macOS 15.0〜15.3と15.4以降のpasteboard privacy実測結果。

## 総合評価

**修正推奨。**

前回の高優先度指摘と主要な構造上の不足は解消され、テスト戦略としてかなり実用的になった。特にコンパイルガード、IL2CPP実行契約、層2bと層3のコスト分離は妥当である。

残る問題は、適用状況と外部仕様の事実精度である。DialogのEditMode実装状況、AltTesterのUI Toolkit対応、macOS 15.4のavailabilityを修正すれば、現時点の採用判断とロードマップを信頼できる状態になる。未定義のCI詳細を着手時に確定する方針は許容できる。

### 確認資料

- Apple `NSPasteboard.AccessBehavior`: https://developer.apple.com/documentation/appkit/nspasteboard/accessbehavior-swift.enum
- Xcode 26.3 / macOS 26.2 SDK `NSPasteboard.h`: `NSPasteboardAccessBehavior` および `accessBehavior` は `API_AVAILABLE(macos(15.4))`
- AltTester 2.2 UI Toolkit support: https://alttester.com/alttester-2-2-release-ui-toolkit-support-for-unreal-engine/
- AltTester Unity SDK 2.3.2 documentation: https://alttester.com/docs/sdk/latest/home.html
- AltTester UI Toolkit examples: https://alttester.com/docs/sdk/2.2.4/pages/examples.html
