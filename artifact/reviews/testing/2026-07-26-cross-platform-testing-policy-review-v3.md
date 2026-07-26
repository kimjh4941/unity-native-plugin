# レビュー結果

- 日付: 2026-07-26
- 対象ファイル: `agent-rules/coding-rules/testing.md`
- 機能名: testing
- プラットフォーム: cross-platform
- 前回レビュー: `artifact/reviews/testing/2026-07-26-cross-platform-testing-policy-review-v2.md`

---

## 強み

- 前回指摘した層 1 の適用状況は、機能・プラットフォーム別の表と `実装済み / 未実装 / N/A / 対象外` の定義へ置き換えられた。
- Dialog と iOS Notification の状態が区別され、次に埋めるべき欠落が明確になっている。
- AltTester の UI Toolkit 対応可能性を認めたうえで、instrumentation、ライセンス、CI依存、層2bとの機能重複を不採用理由としており、判断の筋が通っている。
- macOS 15.0〜15.3と15.4以降が分離され、`accessBehavior` のavailabilityと未実測範囲が明記された。
- build targetの復元ルールは、永続workspaceと使い捨てCIで適切に分けられた。
- 機微情報を扱うアサーションについて、値を直接出力しない比較方法まで具体化された。
- 前回の中優先度指摘4件、低優先度指摘2件はいずれも内容上は反映されている。

## 改善点

### 高優先度

- なし。

### 中優先度

#### 1. 外部仕様の確認ルールと現在の検証状態が矛盾している

- セクション: `5. 外部ツール情報の鮮度管理`
- severity: medium
- 問題点:
  - `testing.md:257-258` は、外部仕様を本文へ追加・更新した時点で「対象バージョン・一次資料・確認日」を併記すると定めている。
  - しかし `testing.md:270-274` では、Appium、iOS pasteboard privacy、`NSPasteboard.accessBehavior`、Windows UI automation、AltTesterの5項目がすべて「一次資料未確認」「確認日: 未」のままである。
  - これらは2節・4節のツール選定と制約を支える現在の主張であり、単なる将来候補ではない。
  - `testing.md:276-277` の「着手前に確認」「不可逆な判断に使わない」という留保は慎重だが、直前に定めた「追加・更新時に確認する」ルールを満たしていない。
- 改善提案:
  - 現在の5項目を一次資料で確認し、資料URL、対象バージョン、確認日を表へ記録する。
  - 確認できない項目は本文でも断定せず、選定根拠から外すか「仮説」と明記する。
  - 「追加時の初回確認」と「層3着手時の再確認」を別の列または別表で管理する。
- 確認可能な一次資料例:
  - Android clipboard制約: Android Developers
  - AppiumIME方式: `appium/io.appium.settings`公式README
  - iOS pasteboard privacy: Apple `UIPasteboard` / WWDC22
  - macOS availability: Xcode SDK `NSPasteboard.h`
  - Windows後継ツール: Microsoft Learn
  - AltTester UI Toolkit対応: AltTester公式リリース・SDKドキュメント

### 低優先度

#### 1. DialogをN/Aとする説明が`Win32MessageBox`の役割を誤っている

- セクション: `7. 適用状況`
- severity: low
- 問題点:
  - `testing.md:346-348` はDialogを「Managerと`Win32MessageBox`（P/Invokeラッパ）のみ」と説明している。
  - 実際の`Win32MessageBox.cs`はWin32 MessageBox用の定数を公開するstatic classであり、`DllImport`は`WindowsDialogManager.cs`側にある。
  - 定数値だけを複製テストする価値は低いため、DialogをN/Aとする結論は維持できるが、現在の根拠説明は事実と異なる。
- 改善提案:
  - `Win32MessageBox`を「Win32定数定義」と表現する。
  - DialogをN/Aとする理由を「層1で検証すべき振る舞いを持つ純粋ロジックがない」に変更する。
  - 定数の値をOS仕様に照合する必要がある場合は、単体テストではなく実装レビューまたは静的確認の対象とする。

#### 2. 「各OSの公式機構を直接使う」という表現が実際の採用候補と一致しない

- セクション: `2. OS 境界層のプラットフォーム別ツール`
- severity: low
- 問題点:
  - `testing.md:110` は各OSの公式機構を「直接使う」としている。
  - AndroidとWindowsの第一候補にはAppiumが含まれ、公式OS機構を直接呼ぶ構成だけではない。
  - 実際の方針はAppium一本化を避け、各プラットフォームに適したnative backendまたは薄いwrapperを選ぶことである。
- 改善提案:
  - 「各プラットフォームの制約に適したnative backendまたは公式APIを選ぶ」など、表の内容と一致する表現へ変更する。

#### 3. digest比較を機微情報の安全策として使う場合の条件が不足している

- セクション: `6. 機微情報の取り扱い`
- severity: low
- 問題点:
  - `testing.md:303` は実データのdigest比較を候補にしている。
  - OTPや短い招待コードなど低エントロピー値のdigestは総当たりで元値を推測できるため、digestをログへ出せば常に安全とは限らない。
  - 比較処理内だけで使い、expected / actual digestを失敗ログへ出さなければリスクを抑えられる。
- 改善提案:
  - digest自体もログへ出力しないカスタム制約を使う。
  - 低エントロピー値にはランダムsalt付きMAC、または長さ・形式・一致の真偽だけを返す比較を優先する。
  - 実データをテストへ持ち込まないことを最優先ルールとして明記する。

## 不足項目

- 外部仕様5項目の一次資料URL、対象バージョン、初回確認日。
- 初回確認日と層3着手時の再確認日を区別する管理方法。
- macOS 15.0〜15.3のpasteboard privacy実測結果。
- 層1の契約網羅性を判定する完了条件。

## 総合評価

**軽微な修正を条件に承認可能。**

前回レビューの指摘は実質的に解消され、層モデル、コンパイルガード、実行契約、カバレッジ状況、機微情報の扱いまで一貫したテスト方針になった。残る中優先度の問題は、外部仕様の鮮度管理ルールを本文自身が満たしていない点である。

一次資料表を現在の確認結果で埋めれば、方針として承認できる。Dialogの表現、OS機構の表現、digestの注意点は判断を変えるものではないが、正本として長期運用する前に直すことを推奨する。

### 確認資料

- Android 10 clipboard restrictions: https://developer.android.com/about/versions/10/privacy/changes
- Appium Settings clipboard behavior: https://github.com/appium/io.appium.settings
- Apple `UIPasteboard`: https://developer.apple.com/documentation/uikit/uipasteboard
- Apple WWDC22 privacy: https://developer.apple.com/videos/play/wwdc2022/10096/
- Apple `NSPasteboard.AccessBehavior`: https://developer.apple.com/documentation/appkit/nspasteboard/accessbehavior-swift.enum
- Microsoft Windows UI testing: https://learn.microsoft.com/en-us/windows/apps/develop/testing/
- AltTester UI Toolkit support: https://alttester.com/alttester-2-2-release-ui-toolkit-support-for-unreal-engine/
