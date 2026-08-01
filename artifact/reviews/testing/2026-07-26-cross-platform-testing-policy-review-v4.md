# レビュー結果

- 日付: 2026-07-26
- 対象ファイル: `agent-rules/coding-rules/testing.md`
- 機能名: testing
- プラットフォーム: cross-platform
- 前回レビュー: `artifact/reviews/testing/2026-07-26-cross-platform-testing-policy-review-v3.md`

---

## 強み

- 外部仕様の初回確認と層 3 着手時の再確認が別管理になり、確認済み項目には一次資料と確認日が追加された。
- Android の clipboard 制約、AppiumIME、Windows UI automation、AltTester UI Toolkit 対応は、本文の主張と一次資料が対応している。
- Dialog の N/A 判定は `Win32MessageBox` の実際の役割に合わせて修正され、N/A の理由も「検証すべき振る舞いを持つ純粋ロジックがない」と明確になった。
- OS 境界ツールの選定方針は「native backend または公式 API」に修正され、Appium を含む表と整合した。
- digest 比較には低エントロピー値のリスク、ログ禁止、代替手段が追加された。
- 前回レビュー v3 の中優先度指摘と低優先度指摘は、意図した方針としてはすべて反映されている。

## 改善点

### 高優先度

- なし。

### 中優先度

#### 1. macOS / iOS を「一次資料未確認」とする検証状況が現在の環境と一致しない

- セクション: `5. 外部ツール情報の鮮度管理 > 未確認`
- severity: medium
- 問題点:
  - `testing.md:297` はローカルの最新 SDK を `MacOSX14.4.sdk` とし、macOS 15.x SDK を所持していないため `NSPasteboard.accessBehavior` の導入バージョンを確認できないとしている。
  - 現在選択されている Developer Directory は `/Applications/Xcode26.3.app/Contents/Developer` であり、`xcrun --sdk macosx --show-sdk-path` は `MacOSX26.2.sdk` を返す。
  - 同 SDK の `AppKit.framework/Headers/NSPasteboard.h` では、`NSPasteboardAccessBehavior` と `accessBehavior` の両方が `API_AVAILABLE(macos(15.4))` と明記されている。
  - `testing.md:298` はAppleドキュメントを取得できないためiOS pasteboard privacyを未確認としているが、Appleの `UIPasteboard` ドキュメントとWWDC22「What's new in privacy」は取得可能である。WWDC22では、iOS 16で他アプリが書いたpasteboard itemへアクセスすると、ユーザー意図が確認できない場合にmodal promptを表示する契約が説明されている。
  - 未確認扱い自体は安全側だが、「確認できない」という環境認識が誤っており、5節の初回確認ルールを不要に未達のままにしている。
- 改善提案:
  - macOSの行を確認済み表へ移し、対象を `macOS 15.4+`、根拠をXcode 26.3 / MacOSX26.2 SDKの`NSPasteboard.h`、初回確認日を2026-07-26とする。
  - 2節のmacOS表・見出し・本文を`macOS 15.4+`へ戻し、15.0〜15.3ではAPIを利用できないことを明記する。
  - iOSの行を確認済み表へ移し、Apple `UIPasteboard` とWWDC22を一次資料として記録する。
  - 実機での許可UIやCI挙動は引き続き「層3再確認: 要」とし、API契約の初回確認と実測を区別する。

### 低優先度

#### 1. AppiumIMEの確認対象バージョンが固定されていない

- セクション: `5. 外部ツール情報の鮮度管理`
- severity: low
- 問題点:
  - `testing.md:289` はAppium公式リポジトリを一次資料としているが、確認したtag、release、commitが記録されていない。
  - 本文自身が`testing.md:140`でAppium / UiAutomator2 driver / `io.appium.settings`のバージョン固定を要求している。
  - 実導入前に固定する方針は妥当だが、初回確認の再現性を持たせるには確認時点のバージョンまたはcommitも必要である。
- 改善提案:
  - 初回確認に使用した`io.appium.settings`のreleaseまたはcommitを記録する。
  - 実導入時に採用バージョンが異なる場合は、層3再確認欄で再検証する。

#### 2. AltTester節に解消済みの「要確認事項」への参照が残っている

- セクション: `4. 採用しない選択肢 > AltTester`
- severity: low
- 問題点:
  - `testing.md:232-238` はAltTesterのUI Toolkit対応を一次資料で確認済みとしている。
  - 一方、`testing.md:240-242` はAltTesterが`VisualElement`をどう扱うかを「上記の要確認事項」としているが、上段に要確認事項はなく、探索・操作可能であることも確認済みである。
- 改善提案:
  - `testing.md:240-242`を削除するか、「内部実装方式は採用判断に影響しない」に置き換える。

## 不足項目

- macOS 15.4 availabilityの確認済み記録。
- iOS 16以降のpasteboard privacy API契約の確認済み記録。
- `io.appium.settings`の初回確認に使用したversionまたはcommit。
- macOS 15.0〜15.3および対象iOSでの実機挙動。これは層3着手時の実測項目として残してよい。

## 総合評価

**修正推奨。**

テスト戦略の構造、採用判断、セキュリティ上の規約、現状カバレッジは十分に整理されており、前回の指摘もほぼ解消されている。残る中優先度の問題は、確認可能なApple仕様を環境誤認によって未確認扱いにしている点だけである。

macOS SDKヘッダとAppleのiOS一次資料を確認済み表へ反映すれば、方針文書として承認できる。残る低優先度2件は長期運用時の追跡性と文章整合性に関するもので、採用判断そのものは変えない。

### 確認資料

- Apple `UIPasteboard`: https://developer.apple.com/documentation/uikit/uipasteboard
- Apple WWDC22 What's new in privacy: https://developer.apple.com/videos/play/wwdc2022/10096/
- Apple `NSPasteboard.AccessBehavior`: https://developer.apple.com/documentation/appkit/nspasteboard/accessbehavior-swift.enum
- Xcode 26.3 / MacOSX26.2 SDK `NSPasteboard.h`: `NSPasteboardAccessBehavior` および `accessBehavior` は `API_AVAILABLE(macos(15.4))`
- Appium Settings: https://github.com/appium/io.appium.settings
