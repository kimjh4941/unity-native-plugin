# テスト戦略

このファイルはテストの**層モデルとツール選定**を定義する。
「修正後に既存テストを確認する」といった TDD の規律は `./common.md` を参照すること。

---

## 1. テスト層モデル

Unity プラグインは「Unity 内部の挙動」と「OS 側の実際の状態」の 2 つを検証する必要があり、
両者は必要な実行環境もツールも異なる。次の 4 層に分けて考える。

| 層 | 検証対象 | 実行環境 | デバイス要否 | 前例 |
|---|---|---|---|---|
| **1. EditMode** | ネイティブ非依存の純粋ロジック | Unity Editor | 不要 | あり（多数） |
| **2a. PlayMode（Editor 内）** | プレイヤーループが必要な経路、非実機フォールバック | Unity Editor | 不要 | あり（iOS / mac Share） |
| **2b. PlayMode（Player 上）** | UI → Manager → ネイティブ → callback の全経路 | 実機 / エミュレータ | **必要** | なし |
| **3. OS 境界** | クリップボード実内容、共有シート、ネイティブダイアログ | 実機 + 外部ハーネス | **必要** | なし |

### 層 2b と層 3 のコスト関係

**デバイス調達・接続・署名・配布の基盤は共有できるが、テストハーネスは別コストである。**

| 要素 | 層 2b | 層 3 | 共有可否 |
|---|---|---|---|
| 実機 / エミュレータ、接続、署名 | 要 | 要 | **共有できる** |
| テストハーネス | Unity Test Framework（Player テスト） | Appium / XCTest / PowerShell | **別実装・別保守** |

したがって**層 2b を先行導入する意味がある**。層 3 の外部ハーネスを持たなくても、
層 2b だけで JNI / P/Invoke / IL2CPP の検証価値が得られる。

導入順序は「基盤が共有されるか」ではなく、**各層が検出する障害の種類と、追加で必要になる依存**で判断する。

### 層 1: EditMode

**対象:** ネイティブ呼び出しから分離された純粋ロジック。

- JSON builder / parser、結果型の不変条件、dispatch helper（例: `InvokeInOrder`）
- UXML と Controller の name 不一致を検出する wiring test（`*SampleSceneWiringTests`）
- `InternalsVisibleTo("NativeToolkit.Runtime.Tests")` により `internal` 型も直接検証できる

**Manager 本体の扱い:** Manager の `Awake` / `Initialize` は `AndroidJavaObject` / `DllImport` に触れるため、
**Manager インスタンスを生成するテストは層 1 では書かない**。
Manager の dispatch 順序などを検証したい場合は、`internal static` の純粋関数として切り出して層 1 で検証する
（clipboard の `AndroidClipboardManager.InvokeInOrder` がこの形）。
Manager 全体の初期化・イベント購読を通す検証は層 2a 以降で行う。

### 層 2a: PlayMode（Editor 内）

`UnityMainThreadDispatcher` は実際のプレイヤーループ（`Update`）がないとキューを flush しないため、
**EditMode では通らない経路**がある。この層はそこを埋める。

検証できるもの:

- dispatcher 経由で共通 event → per-call callback の順序が保たれること
- 非実機プラットフォームでの失敗経路（`"{operation} could not be started."` 等）
- イベントが発火しない契約（例: `StartObserving` は event を発火しない）

検証できないもの: JNI / P/Invoke の実際の解決、IL2CPP でのマーシャリング。これらは層 2b。

**実行前提:** 対象 Manager が現在の active build target でコンパイルされている必要がある（3 節参照）。

### 層 2b: PlayMode（Player 上）

サンプルシーンの UI Toolkit ボタンをテストコードから操作し、Player 上で実行する。
テストが Unity プロセス内で動くため、**外部ドライバによるオブジェクト探索が不要**になる
（外部ドライバを採らない理由は 4 節）。

この層でしか捕まえられないもの:

- JNI のメソッド解決失敗（引数 0 個呼び出し等）
- IL2CPP での `AndroidJavaProxy` / `MonoPInvokeCallback` の動作
- ネイティブ側から返る実際のエラー文言

#### 実行契約（必須）

**IL2CPP 固有の不具合を捕捉したい場合、IL2CPP で実行することを明示的に保証しなければならない。**
`-buildTarget Android` は active build target を選ぶだけで、Scripting Backend を指定しない。

| 項目 | 要求 |
|---|---|
| Scripting Backend | **IL2CPP**（Mono で実行した場合、IL2CPP 固有の保証は得られない） |
| Target architecture | 対象を明示（例: ARM64） |
| Build 種別 | Player テストのため Development Build を使う場合はその旨を記録 |
| 結果回収 | XML 結果の回収方法、異常終了・タイムアウト時の扱いを定義 |
| 端末選択 | 実機 / エミュレータの別、OS バージョン範囲 |

Mono での高速 smoke test と IL2CPP での契約テストを分ける場合は、**それぞれの保証範囲を明示する**。
「Player 上で通った」だけでは IL2CPP を検証したことにならない。

#### UI 操作の規約

UI Toolkit の `Button.clicked` は `Clickable` マニピュレータがポインタイベント列に反応して発火する。
**擬似クリック方法がテストごとにばらつくと、UI 経路を通したつもりで Controller を通していない事故が起きる。**

- 共通の UI 操作 helper を 1 つ用意し、全テストがそれを使う（各テストが独自の擬似クリックを実装しない）
- scene fixture（シーンのロード、`UIDocument` の取得、Navigator 経由の画面遷移）も共通化する
- 非同期 callback の待機は**固定フレーム待ちにしない**。結果 event の受信または timeout で待つ

具体的なイベント種別・helper の実装方針は層 2b 着手時に確定する（7 節「未定義事項」）。

### 層 3: OS 境界

Unity プロセスの外側の状態を検証する。ツールはプラットフォームごとに異なる（2 節）。

---

## 2. OS 境界層のプラットフォーム別ツール

**クロスプラットフォームの統一ツール（Appium 一本化）は採らない。**
各プラットフォームの制約に適した native backend または公式 API を選ぶ
（結果として Android / Windows では Appium を薄い wrapper として使い、iOS / macOS では OS API を直接叩く）。

| プラットフォーム | UI 操作ツール | クリップボード読み書き | 制約 |
|---|---|---|---|
| **Android** | Appium + UiAutomator2、または Espresso / UiAutomator instrumentation | Appium: `mobile: getClipboard` / `setClipboard` | Android 10+ のフォアグラウンド制限。既定 IME の変更を伴う（下記） |
| **iOS** | XCTest（XCUITest） | `UIPasteboard.general` に**テストプロセスから API 経由でアクセス可能** | 他アプリ由来データの読み取りは pasteboard privacy の影響を受ける（下記） |
| **macOS** | XCTest、ネイティブダイアログ操作は `osascript`（System Events） | `NSPasteboard.general` に**API 経由でアクセス可能** | macOS 15.4+ は `NSPasteboard.AccessBehavior` による許可状態に依存（下記） |
| **Windows** | Appium + `appium-windows-driver`（Microsoft の現行推奨）。UI 操作が不要ならツール不要 | PowerShell の `Get-Clipboard` / `Set-Clipboard` | 現状、特筆すべき許可制約は確認していない |

**「API を呼べること」と「無人 CI で常に無操作・無許可で読めること」は別である。**
以下の制約は、実機 / Simulator、対話セッション / CI セッションで挙動が変わりうる。

### Android: 既定 IME の変更を伴う

Android 10+ (API 29+) は、**フォーカスのあるアプリまたは既定 IME 以外**からのクリップボードアクセスを禁止する
（Android 公式ドキュメントおよび native 側 `ClipboardChangeMonitor` の KDoc、5 節で出典確認済み）。

Appium はこの制限を回避するため、`io.appium.settings/.AppiumIME` を**既定 IME に設定して**
クリップボードを取得する方式を採る（`adb shell ime set io.appium.settings/.AppiumIME`）。
したがって主な副作用は、

- **端末全体の既定キーボードが変更される**
- IME 状態の復元が必要で、復元漏れは後続テストや端末状態に波及する

運用契約:

- `finally` 相当で**必ず元の IME を復元する**
- **復元失敗はテスト失敗として扱う**（握り潰さない）
- 採用する Appium / UiAutomator2 driver / `io.appium.settings` のバージョンを固定する

### iOS: pasteboard privacy

自アプリが書いた pasteboard の読み取りは問題ないが、**他アプリ由来のデータを読む場合**は
システムの貼り付け許可（ユーザー操作）が介在しうる。
「他アプリでコピー → 自アプリで読む」形のテストは、無人 CI で無条件に通る前提を置かないこと。

### macOS: `NSPasteboard.AccessBehavior`（macOS 15.4+）

`NSPasteboardAccessBehavior` / `accessBehavior` は **`API_AVAILABLE(macos(15.4))`** であり、
プログラムからの general pasteboard アクセスに ask / allow / deny の許可状態が適用される。

Xcode 26.3 付属の `MacOSX26.2.sdk` にある `NSPasteboard.h` で availability を確認済み
（5 節参照）。

`common.md` の Minimum Versions は macOS 15 以降だが、15.0〜15.3 ではこの API を利用できない。

- テストコードから `accessBehavior` を参照する場合は **availability check を必須**とする
- OS バージョン別の許可ダイアログ処理、初期状態、cleanup をテスト前提として定義する
- macOS 15.0〜15.3 と 15.4 以降の実挙動は対象 OS で実測して確定させる（7 節「未定義事項」）

### Android では層 3 を狭くできる

アプリ自身がフォアグラウンドにいる限り、テストから `Read()` を呼んでクリップボードを読める。
`CopyPlainText` の結果検証に `Read()` を使えば Appium 不要で層 2b 内に閉じる
（`Read()` 自体のテストには使えないが、copy 系の検証には有効）。

Appium が本当に必要なのは、他アプリ起点の操作、貼り付け先での実際の見え方、機微フラグのプレビュー抑制確認に限られる。

---

## 3. Manager ごとのコンパイルガード差異

**判定基準は「Editor に型が存在するか」ではなく「現在の active build target で型がコンパイルされるか」。**

Manager のプラットフォームガードは統一されておらず、`|| UNITY_EDITOR` を持つものと持たないものが混在する。

### A. `|| UNITY_EDITOR` あり（どの build target でもコンパイルされる）

| Manager | ガード |
|---|---|
| `IosShareManager` | `#if UNITY_IOS \|\| UNITY_EDITOR` |
| `MacShareManager` | `#if UNITY_STANDALONE_OSX \|\| UNITY_EDITOR` |
| `WindowsNotificationManager` | `#if UNITY_STANDALONE_WIN \|\| UNITY_EDITOR` |

### B. `|| UNITY_EDITOR` なし（該当 build target でのみコンパイルされる）

| Manager | ガード |
|---|---|
| `AndroidShareManager` / `AndroidClipboardManager` / `AndroidDialogManager` / `AndroidNotificationManager` | `#if UNITY_ANDROID` |
| `IosDialogManager` / `IosNotificationManager` | `#if UNITY_IOS` |
| `MacDialogManager` / `MacNotificationManager` | `#if UNITY_STANDALONE_OSX` |
| `WindowsDialogManager` | `#if UNITY_STANDALONE_WIN` |

**Android 固有の問題ではない。** 12 個中 9 個が B 群であり、iOS / macOS / Windows でも
Dialog と（Windows を除く）Notification は build target 切り替えが必要になる。

### ルール

- **層 1 / 層 2a のテスト対象 Manager が B 群なら、対応する build target へ切り替えないとテストが存在しない扱いになる**
- CI の target 切り替え要否は、Android 固有ルールではなく**対象 Manager のガードから決定する**
- バッチ実行時は `-buildTarget <target>` を明示する
- **終了後の復元は実行環境で分ける**
  - ローカル / 永続 workspace: **元の target へ戻す**（開発 workspace に生成物・設定差分を残さないため）
  - 使い捨て CI workspace: 復元不要（破棄されるため、再 import のコストだけが増える）
- 既存 PlayMode テストが iOS / mac Share のみなのは、両者が A 群である（= 追加設定なしで Editor から到達できる）ことと整合する

---

## 4. 採用しない選択肢

### AltTester（旧 AltUnityTester）

**採用しない。ただし「技術的に不可能だから」ではない。**

不採用の理由は次の 3 点であり、いずれも AltTester の機能有無に依存しない。

1. **層 2b で要求を満たせる。** Unity プロセス内で動く PlayMode テストなら、
   外部からのオブジェクト探索そのものが不要になる。追加の SDK も外部サーバーも要らない
2. **Player への instrumentation が必要になる。** AltTester SDK をビルドに組み込む必要があり、
   本リポジトリは UPM パッケージとして配布されるため、`Runtime` アセンブリに入れると
   利用者の依存関係になる。組み込むならサンプル / テスト専用プロジェクトに限定する制約が付く
3. **外部サーバー・ライセンス・CI 依存が増える。** 現時点の要求（JNI / IL2CPP の検証）に対して
   得られるものが層 2b と重複する

**AltTester の UI Toolkit 対応について（一次資料で確認済み、5 節）:**

AltTester 2.2 は Unity の UI Toolkit 対応を追加しており、UI Toolkit 要素の探索・クリック・テキスト取得が可能である。
ただしこれは **non-GPL の AltTester Unity SDK に含まれ、Pro / Enterprise ライセンス向け**である。

したがって**「UI Toolkit を探索できない」を不採用の根拠にしてはならない**。
技術的には可能であり、不採用の理由は上記 3 点（層 2b で足りる / instrumentation / ライセンス・依存）にある。

再評価するとすれば、層 2b の UI 操作 helper（1 節）の実装コストが想定を大きく超えた場合に、
ライセンス費用と instrumentation の制約を含めて比較する。

### Appium への一本化

**採用しない。**

Appium は内部で各 OS のネイティブ機構をラップしている。
iOS / macOS / Windows ではクリップボードに OS API 経由で直接アクセスできるため、
XCTest や PowerShell の方が依存が少ない。Appium は Android を第一とし、
Windows は UI 操作が必要な場合に限って使う。

---

## 5. 外部ツール情報の鮮度管理

**外部ツールの挙動・OS の許可仕様は変化する。**
Appium の実装方式、Apple の pasteboard privacy、Windows の UI automation 推奨、AltTester の対応範囲は
いずれも実際に変化してきた領域であり、過去にこのファイルでも誤った断定が発生している。

### ルール

1. **本文へ外部仕様を追加・更新した時点で、対象バージョン・一次資料・確認日を併記する**
   （「着手時に確認する」では、正本の中に古い判断が残り続ける）
2. 層 3 着手時には、初回確認とは別に**再検証ゲート**として全項目を洗い直す
3. 記述と実挙動が食い違った場合、**実挙動を正とし、このファイルを更新する**

### 出典と検証状況

「初回確認」は本文へ記載した時点の確認、「層 3 再確認」は層 3 着手時の再検証ゲート（別管理）。

#### リポジトリ内で検証したもの

| 主張 | 記載箇所 | 検証方法 | 初回確認 | 層 3 再確認 |
|---|---|---|---|---|
| Manager のコンパイルガード A / B 群分類 | 3 節 | 全 12 Manager の `#if` を確認 | 2026-07-26 | 不要 |
| `UIElements.Button` は GameObject ではない | 4 節 | サンプルの UI 実装を確認 | 2026-07-26 | 不要 |
| 層 1 の機能別カバレッジ | 7 節 | `Tests/` 配下の実ファイルを確認 | 2026-07-26 | 都度更新 |
| Dialog に層 1 対象の純粋ロジックが無い | 7 節 | `Runtime/Dialog/` の構成を確認 | 2026-07-26 | 都度更新 |

#### 外部一次資料で確認したもの

| 主張 | 記載箇所 | 一次資料 | 初回確認 | 層 3 再確認 |
|---|---|---|---|---|
| Android 10+ は「フォーカスのあるアプリまたは既定 IME」以外の clipboard アクセスを禁止 | 2 節 | [Android 10 privacy changes](https://developer.android.com/about/versions/10/privacy/changes)（"Unless your app is the default input method editor (IME) or is the app that currently has focus, your app cannot access clipboard data on Android 10 or higher."） | 2026-07-26 | 要 |
| Appium は `io.appium.settings/.AppiumIME` を既定 IME に設定して clipboard を取得する | 2 節 | [`io.appium.settings` 7.1.11](https://www.npmjs.com/package/io.appium.settings) の公式 README（Android Q 以降は requester を既定 IME に設定し、`adb shell ime set io.appium.settings/.AppiumIME` を実行） | 2026-07-26 | 要 |
| iOS 16+ は、他アプリが書いた pasteboard item の値へユーザー意図なしでアクセスすると modal prompt を表示する | 2 節 | [Apple `UIPasteboard`](https://developer.apple.com/documentation/uikit/uipasteboard) / [WWDC22: What's new in privacy](https://developer.apple.com/videos/play/wwdc2022/10096/) | 2026-07-26 | 要 |
| `NSPasteboardAccessBehavior` / `accessBehavior` は macOS 15.4+ | 2 節 | Xcode 26.3 / `MacOSX26.2.sdk` の `AppKit.framework/Headers/NSPasteboard.h`（`API_AVAILABLE(macos(15.4))`） / [Apple `NSPasteboard.AccessBehavior`](https://developer.apple.com/documentation/appkit/nspasteboard/accessbehavior-swift.enum) | 2026-07-26 | 要 |
| WinAppDriver は非 active development、`appium-windows-driver` が後継 | 2 節 | [Microsoft Learn: Test apps built with the Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/develop/testing/)（"WinAppDriver ... is no longer under active development. The recommended successor is Appium with the Windows Application Driver plugin (appium-windows-driver)."、ページ更新日 2026-07-22） | 2026-07-26 | 要 |
| AltTester 2.2 が Unity の UI Toolkit 対応を追加、Pro / Enterprise 向けの非 GPL SDK | 4 節 | [AltTester 2.2 release](https://alttester.com/alttester-2-2-release-ui-toolkit-support-for-unreal-engine/)（"The UI Toolkit support is part of the new non-GPL AltTester Unity SDK – that is available for AltTester Pro and AltTester Enterprise."） | 2026-07-26 | 要 |

Apple の API 契約は初回確認済みだが、許可 UI、無人 CI、Simulator / 実機での差は未実測である。
これらは層 3 着手時に再確認し、実挙動を確定する。

---

## 6. 機微情報の取り扱い

clipboard / notification など、**本文が機微情報になりうる機能をテストする場合**、
実装側のログ秘匿（`common.md` / 各機能の設計に準拠）だけでは不十分である。

CI 成果物にも残さないこと。

### テストの分類とログ規約

| 分類 | 扱う値 | ログ規約 |
|---|---|---|
| **サンプル値のみを扱うテスト** | テストが自分で書き込んだ既知の値（例: `NTK-7F3A-92QX`） | 通常のアサーションでよい。値がログに出ても実害がない |
| **実データを扱いうるテスト** | 他アプリ由来のクリップボード内容など（主に層 3） | 下記の redaction 規約に従う |

### 実データを扱うテストの規約

**NUnit の等値アサーション（`Assert.AreEqual` 等）は失敗時に expected / actual を自動出力する。**
「本文を出さない」という禁止だけでは不十分で、比較方法自体を変える必要がある。

- **最優先ルール: 実データをテストへ持ち込まない。** サンプル値で代替できないか先に検討する
- **実値を直接比較しない。** 次のいずれかで検証する（上ほど安全）
  - 長さ（`Assert.AreEqual(expectedLength, actual.Length)`）
  - 型 / 形式（正規表現マッチの**真偽のみ**をアサートし、値もパターンも出力しない）
  - redacted matcher（失敗メッセージに値を含めないカスタム制約）
  - digest 比較（**下記の条件付き**）
- **digest を使う場合の条件:**
  - **digest 自体もログへ出力しない。** OTP・招待コードなど**低エントロピー値の digest は総当たりで元値を復元できる**ため、
    「digest ならログに出してよい」は成立しない
  - 低エントロピー値には**ランダム salt 付き MAC**、または長さ・形式・一致の真偽だけを返す比較を優先する
- 失敗時スクリーンショットを取得する場合、機微値を表示した状態のものを成果物として保存しない
- 実装側のログ秘匿（`common.md` / 各機能の設計）に加えて、**CI 成果物側でも同じ基準を適用する**

---

## 7. 適用状況（2026-09-03 時点）

**この節は現状のロードマップであり、上記の層モデル・ツール選定（1〜6 節）とは性質が異なる。**
実装が進んだら更新すること。

| 層 | 状況 |
|---|---|
| 1. EditMode | **部分的**（下表参照） |
| 2a. PlayMode（Editor 内） | **部分的**。Clipboard（Android / iOS / macOS）と Share（iOS / macOS）|
| 2b. PlayMode（Player 上） | 未着手 |
| 3. OS 境界 | 未着手 |

### 判定の定義

機能名だけで「実装済み」と集約しない。層 1 は純粋ロジックが対象のため、
**そもそも対象が存在しない機能**と**対象があるのにテストが無い機能**を区別する。

| 判定 | 意味 |
|---|---|
| 実装済み | 層 1 対象の純粋ロジックが存在し、テストがある |
| **未実装** | 層 1 対象の純粋ロジックが存在するのに**テストが無い**（= 埋めるべき欠落） |
| **N/A** | 層 1 対象の純粋ロジックがそもそも存在しない（= 欠落ではない） |
| 対象外 | その機能自体がそのプラットフォームに存在しない |

注意: 「テストファイルが 1 つ存在する」ことと「対象契約を網羅した」ことは別である。
下表の「実装済み」は前者のみを意味し、網羅性は保証していない（網羅性の完了条件は本節「未定義事項」）。

### 層 1 の機能・プラットフォーム別状況（2026-09-03、`Tests/` 配下の実ファイルで確認）

| 機能 | Android | iOS | macOS | Windows |
|---|---|---|---|---|
| **Clipboard** | 実装済み（builder / parser / dispatch / wiring） | 実装済み（builder / parser / reader / dispatch / result / wiring） | 実装済み（builder / parser / reader / dispatch / result） | 対象外 |
| **Share** | 実装済み（builder / wiring） | 実装済み（builder / dispatch） | 実装済み（builder / dispatch / result / wiring） | 対象外 |
| **Notification** | 実装済み（builder） | **未実装**（`IosNotificationJsonBuilder` があるがテストが無い） | 実装済み | 実装済み |
| **Dialog** | **N/A** | **N/A** | **N/A** | **N/A** |

- **Dialog は全プラットフォームで N/A。** `Runtime/Dialog/` は各 Manager と `Win32MessageBox`
  （Win32 MessageBox の定数定義のみを持つ static class。`DllImport` は `WindowsDialogManager` 側にある）で
  構成されており、**層 1 で検証すべき振る舞いを持つ純粋ロジックが無い**（JsonBuilder・結果型・
  `internal static` のいずれも存在しない）。定数の値を単体テストで複製しても OS 仕様との整合は保証されないため、
  定数は実装レビューまたは静的確認の対象とする。Dialog の振る舞い検証は層 2a 以降で行う
- **iOS Notification のみが真の欠落。** ここだけは層 1 で埋められる
- **macOS Clipboard に wiring が無いのは欠落ではない。** サンプルシーンをまだ設計していないため、
  `*SampleSceneWiringTests` の対象が存在しない。`design-sample-scene` の完了時に追加する

### 推奨する導入順序

4 プラットフォーム分のデバイス CI を最初から組むのは、本パッケージの規模に対して過剰投資となる。

0. **層 1 の欠落（iOS Notification）を埋める**（最も安く、既存パターンの流用で済む）
1. **層 2a を埋める**（デバイス不要、前例あり、コストほぼゼロ）
   - 特に「event を発火しない契約」は現状どの層でも未検証で、リグレッションを検出できない
   - Dialog は層 1 が N/A のため、**層 2a が最初の自動テストになる**
   - B 群 Manager（3 節）が対象の場合は build target 切り替えが要る点に注意
2. **層 2b を Android に限定して試す**
   - 未解消の要検証項目（JNI の引数 0 個解決、IL2CPP の `AndroidJavaProxy`）はこの層でしか捕まえられない
   - 着手時に「実行契約」（1 節）を確定させる
3. **価値が実証できてから層 3 と他プラットフォームへ展開**

### 未定義事項（着手時に定義する）

現時点で意図的に定義していない。憶測で埋めず、該当層に着手する際に実測に基づいて追加すること。

- 各プラットフォーム・各機能について、**どの層を必須とするか**のテストマトリクス
  （上表は「現状どうなっているか」であり、「どうあるべきか」は未定義）
- 「対象契約を網羅した」と判定する完了条件（テストファイルの存在だけでは不十分、上記「判定の定義」参照）
- CI での実行コマンド、端末選択、タイムアウト、再試行、ログ回収方法
- クリップボード・既定 IME・許可状態・通知をテスト前後に復元する cleanup 規約の具体手順
- Emulator / Simulator で許容する保証と、物理端末でのみ合格とする保証の区別
- flaky test の隔離・再実行・恒久対応基準
- 層 2b の UI 操作 helper / scene fixture の実装方針（1 節）
- macOS 15.0〜15.3 と 15.4 以降の pasteboard privacy 実測結果（2 節・5 節）
- iOS pasteboard privacy の実挙動（許可 UI の有無・条件）の実測結果（2 節・5 節）
- AltTester を技術面・ライセンス面・運用面で比較した結果（4 節、再評価が必要になった場合）
