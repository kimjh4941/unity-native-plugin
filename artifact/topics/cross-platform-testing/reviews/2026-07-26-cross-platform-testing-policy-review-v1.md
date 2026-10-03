# レビュー結果

- 日付: 2026-07-26
- 対象ファイル: `agent-rules/coding-rules/testing.md`
- 機能名: testing
- プラットフォーム: cross-platform

---

## 強み

- Unity プロセス内で完結する検証と OS 外部状態の検証を分離しており、テスト失敗時の責任範囲を切り分けやすい。
- EditMode / Editor PlayMode / Player 上の PlayMode / OS 境界という段階的な導入方針は、現状の規模と未導入領域を踏まえた現実的な構成になっている。
- Android 10 以降のクリップボード制約と、copy 系だけはアプリ自身の `Read()` で検証できるという限定条件を明記している。
- 「適用状況」を規範部分と分離し、ロードマップとして更新対象であることを明示している。
- UI Toolkit を使う現在のサンプル構成に対して、Unity プロセス内のテストを優先する判断には合理性がある。

## 改善点

### 高優先度

#### 1. 「Android だけがコンパイルガード非対称」という前提が実装と一致しない

- セクション: `3. Android Manager のコンパイルガード非対称`
- severity: high
- 問題点:
  - `testing.md:98-106` は Android だけが `|| UNITY_EDITOR` を持たないと断定している。
  - 実装では `IosNotificationManager`、`MacNotificationManager`、Android/iOS/macOS/Windows の各 Dialog Manager などもプラットフォームシンボルだけで囲まれている。
  - 表にある `MacShareManager`、`IosShareManager`、`WindowsNotificationManager` だけを全 Manager の代表として一般化しているため、iOS・macOS・Windows の EditMode / Editor PlayMode テストでも active build target の切り替えが必要なケースを見落とす。
  - この誤認を正本ルールにすると、設計レビューと CI ジョブ分割の判断が Android に偏る。
- 改善提案:
  - 見出しを「Manager ごとのコンパイルガード差異」に変更する。
  - 全 Manager を列挙するか、少なくとも `UNITY_EDITOR` 対応群と active build target 依存群に分類する。
  - 「型が Editor に存在するか」ではなく、「現在の active build target で型がコンパイルされるか」と表現する。
  - CI の target 切り替え要否は Android 固有ルールではなく、対象 Manager のガードから決定するルールにする。

### 中優先度

#### 1. Appium のクリップボード取得方式と副作用の説明が現行実装に合っていない

- セクション: `2. OS 境界層のプラットフォーム別ツール`
- severity: medium
- 問題点:
  - `testing.md:84-87` は Appium が `io.appium.settings` を前面に出して読み取ると説明している。
  - 現行の Appium Settings ドキュメントは、Android Q 以降では `io.appium.settings/.AppiumIME` を既定 IME に切り替えてから broadcast で取得する手順を示している。
  - したがって主な副作用は対象アプリのバックグラウンド化ではなく、既定キーボードの変更、IME 状態の復元、端末全体への影響である。現状の記述では、避けるべきテストと cleanup 手順を誤る。
- 改善提案:
  - Appium / UiAutomator2 driver / `io.appium.settings` の対象バージョンを固定して記載する。
  - `getClipboard` の前提を「AppiumIME を一時的に既定 IME に設定する」に更新する。
  - `finally` 相当で元の IME を復元すること、復元失敗をテスト失敗として扱うことを運用契約に追加する。
  - Appium の実装変更を考慮し、この説明に確認日と一次資料リンクを付ける。

#### 2. 層 2b と層 3 のコストを同一視する記述が、後段の方針と矛盾する

- セクション: `1. テスト層モデル`
- severity: medium
- 問題点:
  - `testing.md:20` は同じデバイス基盤が必要なので片方だけ導入してもコストは下がらないと断定している。
  - 一方、`testing.md:89-92` は Android の copy 系検証を層 2b 内に閉じれば Appium を不要にできると説明している。
  - 端末、署名、配布経路などは共有できるが、Unity Test Framework の Player テストと Appium / XCTest / PowerShell の外部ハーネスは別の依存・実装・保守コストを持つ。
- 改善提案:
  - 「デバイス調達・接続基盤は共有できるが、テストハーネスは別」と分けて記述する。
  - 層 2b を先行導入すると、OS 境界ツールなしでも JNI / P/Invoke / IL2CPP の検証価値を得られることを明記する。
  - 導入順序の根拠を共有コストではなく、各層が検出する障害と追加依存で比較する。

#### 3. 「iOS / macOS / Windows は制限なし」という断定が強すぎる

- セクション: `2. OS 境界層のプラットフォーム別ツール`
- severity: medium
- 問題点:
  - `testing.md:72-79` は iOS / macOS / Windows のクリップボードを一律に「制限なし」としている。
  - iOS の一般 pasteboard は、他アプリ由来データの取得時にユーザー通知や pasteboard privacy の影響を受ける。
  - 現行 macOS には General pasteboard のプログラムアクセスに対する access behavior があり、許可状態によって ask / allow / deny が変わる。
  - API を呼べることと、無人 CI で常に無操作・無許可で読めることは別である。
- 改善提案:
  - 「OS API から直接アクセス可能」に表現を弱め、ユーザー許可・通知・実行セッション・端末ポリシーを制約欄に分離する。
  - OS バージョン別の許可ダイアログ処理、初期状態、cleanup をテスト前提として定義する。
  - 実機と Simulator、対話セッションと CI セッションを区別する。

#### 4. Windows の推奨 UI 自動化ツールが古い

- セクション: `2. OS 境界層のプラットフォーム別ツール`
- severity: medium
- 問題点:
  - `testing.md:74` は WinAppDriver を第一候補としている。
  - Microsoft の現行ガイドは WinAppDriver が active development ではないとし、Appium の Windows Application Driver plugin を後継として推奨している。
  - 新規導入方針として保守停止中のサーバーを直接選ぶと、将来の Windows / SDK 更新で詰まりやすい。
- 改善提案:
  - 第一候補を Appium + `appium-windows-driver` に更新する。
  - WinAppDriver は下位互換の実行基盤として使われる可能性がある旨を注記し、採用バージョンを固定する。
  - クリップボードだけを検証する場合は PowerShell、UI 操作が必要な場合だけ UI Automation driver、という選択条件を明確にする。

#### 5. 層 2b が IL2CPP を検証するための実行契約が不足している

- セクション: `1. テスト層モデル` / `5. 適用状況`
- severity: medium
- 問題点:
  - `testing.md:53-57` は層 2b で IL2CPP の proxy / callback 動作を捕捉できるとしているが、Scripting Backend、architecture、Player 上での実行、test result 回収方法を要求していない。
  - Android Player テストを Mono で実行した場合、文書が期待する IL2CPP 固有の保証は得られない。
  - `-buildTarget Android` は active build target を選ぶだけで、IL2CPP 実行を保証しない。
- 改善提案:
  - 層 2b の必須構成に `Scripting Backend = IL2CPP`、対象 architecture、Development Build の扱いを定義する。
  - Editor 内 PlayMode と Player 上 PlayMode の実行コマンド、端末選択、タイムアウト、XML 結果回収、異常終了時の扱いを記載する。
  - 高速な Mono smoke test と IL2CPP 契約テストを分ける場合は、それぞれの保証範囲を明示する。

#### 6. `common.md` の EditMode 必須方針との境界が曖昧

- セクション: `1. テスト層モデル`
- severity: medium
- 問題点:
  - `common.md` は Manager の初期化・イベント購読・コールバック転送を EditMode で検証するとしている。
  - `testing.md:24-25` は `AndroidJavaObject` / `DllImport` に触れないものを EditMode 対象とし、実 JNI / P/Invoke 解決は層 2b としている。
  - Manager の `Awake` / `Initialize` がネイティブ境界に触れる実装では、どこまでを EditMode で必須とするかが一意に決まらない。
- 改善提案:
  - EditMode 必須範囲を「ネイティブ呼び出しから分離した coordinator / mapper / dispatch helper」に限定するか、Bridge の差し替え方針を定義する。
  - `common.md` 側も同じ表現に更新し、Manager 自体の初期化を必須とする場合は mock seam と期待テストを明記する。

### 低優先度

#### 1. サンプル UI の操作例が実行可能な仕様になっていない

- セクション: `1. テスト層モデル > 層 2b`
- severity: low
- 問題点:
  - `SendEvent(...)` は概念例に留まり、使用イベント、scene のロード方法、`UIDocument` の取得、非同期 callback の待機条件が示されていない。
  - `Button.clicked` を確実に通す共通 helper がないと、各テストが異なる擬似クリック方法を採用し、UI 経路を通したつもりで Controller を通していない可能性がある。
- 改善提案:
  - 共通の UI 操作 helper と scene fixture の方針を定義する。
  - 固定フレーム待ちではなく、結果 event / timeout を使う待機規約を追加する。
  - コード片を擬似コードと明記するか、コンパイル可能な最小例に置き換える。

## 不足項目

- 各プラットフォーム・各機能について、どの層を必須とするかを示すテストマトリクス。
- 層 2b の Scripting Backend、architecture、端末 OS 最小・最大バージョン、Player build 条件。
- CI での実行コマンド、端末選択、タイムアウト、再試行、テスト結果・ログの回収方法。
- クリップボード、既定 IME、許可状態、通知、共有シートをテスト前後に復元する cleanup 規約。
- クリップボード内容や通知本文などの機微情報を CI ログ・スクリーンショット・テスト成果物へ残さない規約。
- 外部ツールの採用バージョン、一次資料、最終確認日、見直し周期。
- Emulator / Simulator で許容する保証と、物理端末でのみ合格とする保証の区別。
- flaky test の隔離・再実行・恒久対応基準。

## 総合評価

**要修正。**

層モデルと段階的導入の方向性は妥当で、テスト戦略の正本として育てる価値がある。一方、Manager のコンパイルガード分類が実装と一致せず、CI と設計レビューの対象範囲を誤らせるため、まず修正が必要である。

加えて、Appium、Apple の pasteboard privacy、Windows UI automation の説明には現行仕様との差がある。外部ツールや OS 制約は変化するため、断定を避け、対象バージョン・確認日・一次資料を併記する形式が望ましい。層 2b については IL2CPP を保証する具体的な実行契約まで追加すれば、概念方針から実際に運用できる基準へ引き上げられる。

### 確認資料

- Unity Test Framework: https://docs.unity3d.com/ja/current/Manual/testing-editortestsrunner.html
- Android 10 privacy changes: https://developer.android.com/about/versions/10/privacy/changes
- Appium Settings clipboard behavior: https://www.npmjs.com/package/io.appium.settings
- Appium UiAutomator2 clipboard management: https://github.com/appium/appium-uiautomator2-driver
- Apple `UIPasteboard`: https://developer.apple.com/documentation/uikit/uipasteboard
- Apple `NSPasteboard.AccessBehavior`: https://developer.apple.com/documentation/appkit/nspasteboard/accessbehavior-swift.enum
- Microsoft Windows UI testing guidance: https://learn.microsoft.com/en-us/windows/apps/develop/testing/
