# テスト方針（`testing.md`）の策定と、層 2b / 3 の未導入

- 記録日: 2026-07-26
- 分類: 横断課題（Clipboard / Notification / Share と 4 プラットフォームすべてに共通）
- 発見経緯: 機能ごとにテストの当て方が食い違っていたため、層モデルとツール選定を
  `agent-rules/coding-rules/testing.md` に一本化した。その草案を 4 巡レビューした記録がここにある
- 対応方針: **方針の策定は完了。層の導入は機能開発の中で段階的に進める。**
  層 2b / 3 は OS 境界ツールの選定まで済んでいるが、どの機能でも着手していない
- 進捗: **一部対応。** 方針は確定済み、適用は層 0〜2a まで

---

## ここに何があるか

| ディレクトリ | 中身 |
|---|---|
| `reviews/` | `agent-rules/coding-rules/testing.md` のレビュー v1〜v4 |

方針そのものは `agent-rules/coding-rules/testing.md` にある。**こちらが正本。**
この README とレビュー記録は、その文書がどう決まったかの経緯にあたる。

## レビュー 4 巡で何が変わったか

| 版 | 主な指摘 |
|---|---|
| v1 | 層モデルと適用状況が混在していた。規範とロードマップを分離 |
| v2 | 外部ツール情報に一次資料と確認日が無かった |
| v3 | OS 境界ツールの選定基準が Appium を含む表と整合していなかった |
| v4 | macOS / iOS を「一次資料未確認」としていたが、手元の Xcode 26.3 で確認できた |

v4 の指摘は反映済み（`testing.md` 5 節の確認済み表に macOS 15.4+ の行がある）。

## 残っている未導入分

`testing.md` 7 節「適用状況」（2026-09-26 時点）より:

| 層 | 状況 |
|---|---|
| 0. Player ビルド | Windows のみ。Android / iOS / macOS は未整備 |
| 1. EditMode | 部分的 |
| 2a. PlayMode（Editor 内） | 部分的。Clipboard 4 種と Share（iOS / macOS） |
| **2b. PlayMode（Player 上）** | **Windows のみ、16 本**（`WindowsClipboardPlayerTests`、既定の実行は 15 本） |
| **3. OS 境界** | **Windows のみ、最小限**（クリップボードの最後の 1 件を外から読む） |

**この表の更新は `testing.md` 側で行う。** ここに写しを置いているのは状態の要約のためで、
実装が進んだときに両方を直す義務を増やさないよう、数値の正本は `testing.md` に置く。

## サンプル UI の全自動化（2026-09-23 追記）

native-toolkit は `windows/WindowsLibraryExampleUITest` でサンプルアプリの UI を
FlaUI.UIA3 + MSTest により自動化しており、`[TestMethod]` が 89 本ある。
Page Object（`Pages/ClipboardPage.cs` ほか）＋ `Infra/` の抽象で組まれている。
同じことがこちらでできるかを調べた。

### 確認した事実（一次資料、2026-09-23）

| 主張 | 結論 | 根拠 |
|---|---|---|
| Unity は Windows で UI Automation を公開するか | **する** | Unity 6000.4 Accessibility module。role が `IRawElementProviderSimple` の `ControlType`（ボタンなら `UIA_ButtonControlTypeId`）に変換される |
| 対応プラットフォーム | Android（TalkBack）/ iOS（VoiceOver）/ **Windows（Narrator）** / macOS（VoiceOver） | 同上。デスクトップ対応は 6000.3 で追加 |
| 階層は自動で作られるか | **作られない** | 「It does not derive the accessibility hierarchy from your scene or UI automatically」「It has no awareness of the scene or UI hierarchy」 |

本プロジェクトは Unity 6000.4.2f1（`scripts/verify_unity_windows.sh`）なので、機能は存在する。

### したがって

**「Unity の UI Toolkit は UIA から見えないので FlaUI 方式は不可能」は誤りである。**
`testing.md` の AltTester 節と同じ構図で、**技術的な不可能ではなく、コストと置き場の問題**になる。

FlaUI 方式を採るなら、サンプル画面ごとに `AccessibilityNode` の階層を手で組み、
UI Toolkit の要素と同期させ続ける実装が要る。そのコードは**テストではなくサンプル側**、
つまり UPM で配布される `Runtime/UI/` に入る。

- 得: native-toolkit の 89 テストの作り方をほぼそのまま持ち込める。サンプルがスクリーンリーダー対応になる（それ自体の価値はある）
- 損: 配布物に恒久的な保守対象が増える。画面を直すたびに階層も直す

層 2b（Player 上の PlayMode テスト）なら、プロセス内で `UQuery` から `VisualElement` を
直接掴めるため、**この階層はまったく要らない**。`testing.md` 4 節が AltTester を
不採用にした第 1 の理由（「層 2b で要求を満たせる」）がそのまま当てはまる。

### 未確認（決定的）

- **スクリーンリーダーが起動していないとき、UIA プロバイダが応答するか。**
  `AssistiveSupport.isScreenReaderEnabled` と `activeHierarchy` の関係が公開ドキュメントに書かれていない。
  無人 CI では Narrator が動いていないため、**ここが否なら FlaUI 方式は CI で成立しない**。
  確かめるには階層を実装した Player が要るので、FlaUI 方式を検討する段になってから実験する

### 現時点の判断

**層 2b を本命とする。** FlaUI 方式は「サンプルにアクセシビリティ階層を実装する」という
別の意思決定とセットであり、上の未確認事項も未解決のため、今は採らない。

**自動化は [windows-c-abi-2](../windows-c-abi-2/README.md) の後ではなく、前提として先に立てる。**
当初は「移行の後」と順番付けしたが、これは誤りだった。移行では 47 本中 37 本で振る舞いが変わり、
うち 4 件（S-1〜S-4）は**コンパイルが通る**変更である。タイムスタンプが 1000 倍ずれても
画面上はもっともらしい数字に見えるため、人がサンプルを触って見つけられるものではない。
**自動テストは移行の後で入れるものではなく、移行を検証する手段そのものである。**

「1.x 向けに書くと作り直しになる」という当初の懸念は、分けて考えるべきだった。

| | ABI に依存するか |
|---|---|
| ハーネス（Player 起動・UI 駆動・コールバック待ち・OS 側の読み取り） | **しない** |
| アサーション（ミリ秒か秒か、`CANCELED` か `-1` か） | **する**（37 行） |

高い方は今作れて、そのまま使える。したがって順番は次のとおり。

1. ハーネスを 1.x のまま作り、既存の手動確認項目を移す
2. 1.x で緑にして baseline を取る
3. 移行時、設計 8.3 の 37 行に対応するアサーションだけを意図的に書き換える。
   **それ以外が赤くなったら退行**

3 が肝である。「設計どおり変わった」と「壊れた」を機械的に分けられる。
移行の後にテストを書くと、赤いところから始まるのでこの区別がつかない。

### 自動化の対象は既存の手動確認項目

**新規に観点を作らない。** 正本は
`artifact/features/clipboard/results/2026-09-09-windows-clipboard-verify-manual-result-v1.md`。

| ブロック | 項目数 | 要る層 |
|---|---|---|
| A 履歴 ON・フォアグラウンド | 18 | 大半が層 3 |
| B 非フォアグラウンド | 1 | 層 2b + 外部ウィンドウ |
| C 履歴 OFF | 5 | 層 2b + 層 3 |
| D 異常系 | 8 | **層 2b で完結** |
| 9 章 Await | 6 | **層 2b で完結** |
| S 観点 | 9 | 層 2b（S-2 / S-4 / S-8 は既にログ解析で半自動） |

**47 項目。うち 23 項目（D・9 章・S）は層 2b だけで閉じ、24 項目が層 3 を要求する。**

`testing.md` 1 節は「層 2b を先行導入する意味がある」とするが、
**Windows Clipboard では事情が違う。** 既存項目の過半が層 3 を要求するため、
層 3 のハーネスを後回しにすると自動化が半分で止まる。

未実施として残っている項目も同じ方向を指している。

- **M-20b**（`ShutdownTimeout` の観測）: 文書自身が「外部プロセスにクリップボードを
  握らせ続ける道具立て（層 3）」と書いている
- **D-9**（予約失敗時の世代管理）: 「サンプルから起こす手段が無い」= 外部から干渉する必要がある

**層 3 のハーネスは、既存項目の置き換えだけでなくカバレッジの拡大でもある。**

### 外部アプリは観測手段であって仕様ではない

手動項目をそのまま写さない。人がメモ帳や Word を開いていたのは中身を覗くためで、
仕様は「OS のクリップボードが、その形式でその値を保持していること」である。

| 手動項目 | 人がやっていたこと | 自動化で必要なこと |
|---|---|---|
| M-2 Copy Plain Text → メモ帳 | メモ帳に貼って目視 | `Get-Clipboard` で中身を確認 |
| M-3 メモ帳でコピー → Paste | メモ帳でコピー | `Set-Clipboard` で事前に仕込む |
| M-6 Copy Image → ペイント | ペイントに貼って目視 | クリップボードの DIB を読んで突き合わせ |
| M-9 Sensitive → Win+V に出ない | Win+V を開いて目視 | 履歴 API で不在を確認 |

例外は **M-7**（メモ帳は平文、Word は HTML を選択）のように、
消費者側の形式選択そのものが観測対象の項目。ここは形式を指定して取得することで近似する。

### 合図が要るのは少数

多くは前後で足りる（harness が仕込む → Player 上のテストが動く → harness が確認する）。
途中の割り込みが要るのは次の 2 種類だけの見込み。

- **M-10**: 外部コピーの検知。テスト実行中に外から書き換える必要がある
- **M-19**: 予約したまま Quit → **終了後**に貼れること。プレイヤーが死んだ後の検証

### 層 2b は「引数 1 つ」ではなかった（2026-09-23 実測）

Unity Test Framework は `-testPlatform StandaloneWindows64` で
PlayMode テストを Player 上で実行できる（一次資料: Unity 6000.4 マニュアル
"Run Play mode tests in a Player"、2026-09-23 確認）。
`scripts/verify_unity_windows.sh` は既に `-runTests -testPlatform` を使っており、
今は `PlayMode`（Editor 内）を渡しているだけである。

```
現在:  -runTests -testPlatform PlayMode              <- Editor 内
層 2b: -runTests -testPlatform StandaloneWindows64   <- Player 上
```

**これを実際に走らせたところ、Player ビルドが失敗した。** 引数を変えるだけでは動かない。

```
399 errors, すべて CS0117
error CS0117: 'WindowsClipboardManager' does not contain a definition for 'DrainForTests'
error CS0117: 'WindowsClipboardManager' does not contain a definition for 'InjectCompletionForTests'
...
397 件が Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs
```

原因は `Runtime/Clipboard/WindowsClipboardManager.cs:466` の `#if UNITY_EDITOR`。
**69 個の `*ForTests` フックがすべて Editor 限定**で、Player ビルドでは存在しない。
それを使う 65 本の PlayMode 統合テストがコンパイルできない。

| 事前の見立て | 実測 |
|---|---|
| 引数 1 つで既存 193 本が Player 上でどうなるか分かる | **ビルドが通らず、1 本も実行に至らない** |

既存の PlayMode テストは「Editor 内で動く」前提で、Manager の内部状態に
`ForTests` フックで直接触る設計になっている。

当初は「フックのガードを広げる（案 A）」か「フックを使わない形で別に書く（案 B）」かの
二択として整理したが、**この二択は前提が誤っていた**（2026-09-26）。

#### 65 本は、Player 上で動かすためのテストではない

統合テストのクラスコメントに、前提が書いてある。

> The native boundary is compiled out in the Editor, so every operation that gets past the
> guard reports PlatformUnavailable. That is what makes the rejection paths and the delivery
> contract testable here without a Windows player.

コードもそのとおりで、Editor 内では `Initialize` もネイティブ呼び出しのラッパーもすべて
`#else` 側に入り、`PlatformUnavailable` を返す。65 本は、ネイティブが存在しない状態で
完了イベントを `InjectCompletionForTests` などで注入し、C# 側の受け渡しの約束を確かめている。

Player ではネイティブが実際に動くため、案 A でコンパイルを通しても
「すべて `PlatformUnavailable` になる」という前提が崩れる。注入したイベントと本物のコールバックが混ざり、
テストとして意味を持たなくなる。

| | 既存の 65 本 | 層 2b に必要なテスト |
|---|---|---|
| 検証する対象 | C# 側の受け渡しの約束（ネイティブなし） | 本物のネイティブを通した動作 |
| 動く場所 | Editor（層 2a） | Player |
| 捕まえられるもの | 受け渡しの順序、拒否の経路 | S-3 / S-4、マーシャリング |

検証する対象が違うので、案 B の懸念だった「二重管理」にはならない。

#### 採った対応

1. 統合テストのファイルのガードを `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` から
   **`#if UNITY_EDITOR` に狭める**。ファイルの前提が成り立つ場所に合わせるだけで、
   フックは `UNITY_EDITOR` のまま、配布物には何も増えない
2. 層 2b のテストは**別ファイルに新しく書く**。フックが必要になったら
   **`UNITY_INCLUDE_TESTS`** で囲む（下記）

#### 結果（2026-09-26）

1 の変更を入れて `-testPlatform StandaloneWindows64` をもう一度回した。

| | 09-23 | 09-26 |
|---|---|---|
| テスト用 Player のビルド | 399 件の `CS0117` で失敗 | **成功**（`CS0117` 0 件） |
| Player の起動 | 至らず | **起動した**。`Player.log` に D3D11 のデバイス作成、入力の初期化、Editor との接続、正常終了が残っている |
| 結果 | 結果ファイルなし、終了コード 3 | 結果ファイルあり、終了コード 0。**実行されたテストは 0 本** |

0 本なのは、Windows の Player に入るテストが今は 1 本もないため。他のプラットフォームの統合テストはそれぞれのプラットフォームのガードで除外され、
Windows Clipboard の 65 本は 1 で Editor 専用にした。

**これで層 2b の流れ（ビルド → 起動 → 結果の返却 → 終了）が初めて最後まで通った。** 中身はまだ空である。
次は、本物のネイティブを通すテストを別ファイルに 1 本書き、この流れの上で実際に動くことを確かめる。

#### 最初の 1 本（2026-09-26）

`Tests/PlayMode/WindowsClipboardPlayerTests.cs` を追加した。`#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` で囲み、
フックは使わない。`Initialize` → `CopyPlainText` → `PastePlainText` → `ShutdownWithDrain` の流れを、
戻り値と、dispatcher 経由で後のフレームに届くコールバックの両方で確かめる。

| 確かめたこと | 結果 |
|---|---|
| テスト用 Player でのコンパイル | 通った（`CS0117` 0 件） |
| テストの実行 | **1 本実行、1 本成功**（0.36 秒） |
| Player の中から P/Invoke が通るか | 通った。`PastePlainText` の戻り値が見本値と一致 |
| dispatcher 経由のコールバックがテスト用 Player で届くか | 届いた（5 秒以内） |
| テスト用 Player のメインスレッドがクリップボードの持ち主になれるか | なれた。`Player.log` に `apartment: MainSta` |
| OS のクリップボードに実際に書かれたか | **書かれた。** テストの後、Unity の外から PowerShell の `Get-Clipboard` で見本値を読めた |

最後の行は層 3 の小さな先取りである。`Copy` → `Paste` の往復は両側が同じように壊れていても通るので、
外から読んで初めて「書けた」と言える。

#### `verify_unity_windows.sh` に組み込んだ（2026-09-26）

Player テストと層 3 の読み取りを、`scripts/verify_unity_windows.sh` の手順として追加した（`--skip-player-tests` で外せる）。

1. 見本値をテストのソース（`WindowsClipboardPlayerTests.cs` の `SampleText`）から読む。両者がずれないようにするため
2. **実行前に、毎回違う番兵の値をクリップボードに置く。** 前の実行が残した見本値で合格にならないようにするため
3. `-testPlatform StandaloneWindows64` で Player テストを回す。**1 本も実行されなければ失敗にする**
   （実行 0 本でも失敗件数は 0 なので、従来の判定では合格になってしまう）
4. 実行後に PowerShell の `Get-Clipboard` で読み、見本値なら合格、番兵のままなら「Player が何も書かなかった」、それ以外なら「別の何かが最後に書いた」として失敗にする

制約: 確かめられるのは**最後に書かれた 1 件だけ**である。Player テストでクリップボードに書くのが
`WindowsClipboardPlayerTests` だけという前提に立っている。項目ごとに確かめるには、Player とスクリプトの間の合図が要る（M-10 / M-19 と同じ）。

`--skip-build` を付けてスクリプト全体を回した結果:

| 手順 | 結果 |
|---|---|
| EditMode | 809 / 809 |
| PlayMode（Editor 内） | 181 / 181。うち `WindowsClipboardManagerIntegrationTests` は **65 / 65**（ガードを `UNITY_EDITOR` に狭めた後も Editor では全件実行されている） |
| Player テスト | 1 / 1 |
| OS のクリップボード | 見本値を読めた |

**このテストが 1.x での基準（baseline）になる。** 2.0.0 に移行したとき、最初に通すべきはこの 1 本である。

#### `UNITY_INCLUDE_TESTS` はテスト用 Player にだけ入る

手元に残っていた 3 種類のビルドについて、`NativeToolkit.Runtime` をコンパイルしたときの引数
（`Library/Bee/artifacts/<dag>/NativeToolkit.Runtime.rsp`）を確認した（2026-09-26）。

| ビルド | `UNITY_EDITOR` | `UNITY_INCLUDE_TESTS` | `DEVELOPMENT_BUILD` |
|---|---|---|---|
| Editor（`1900b0aE.dag`） | 定義 | **定義** | なし |
| 通常の Player、Release（`1900b0aP.dag`） | なし | **なし** | なし |
| テスト用 Player（`1900b0aPDevDbg.dag`、`-testPlatform StandaloneWindows64`） | なし | **定義** | 定義 |

`#if UNITY_EDITOR || UNITY_INCLUDE_TESTS` で囲んだコードは、テスト用 Player には入り、
**配布する Player には入らない**。

**未確認:** テストを含めない development ビルドでの扱い（手元にビルドが残っていなかった）。

#### 同じ形のガードが他のプラットフォームにもある

`IosClipboardManagerIntegrationTests.cs`（`#if UNITY_IOS || UNITY_EDITOR`）、
`MacClipboardManagerIntegrationTests.cs`（`#if UNITY_STANDALONE_OSX || UNITY_EDITOR`）、
Share の 2 本も同じ形をしている。それぞれの Player でテスト用ビルドが同じように失敗するかは確かめていない。

### CI 上の制約

クリップボードと通知は**対話的なデスクトップセッション**を要求する。
`-nographics` のヘッドレスや、デスクトップを持たないサービスアカウントでは動かない。
既存の `scripts/check_windows_clipboard_sample_log.py` がログ事後解析なのも、おそらく同じ事情。

**これは FlaUI 方式でも同じ制約**なので、層 2b を選ぶ理由にも反証にもならない。

#### 手動確認ブロック D（異常系）を移した（2026-09-26）

手動確認結果（`2026-09-09-windows-clipboard-verify-manual-result-v1.md`）のブロック D、8 項目を
`WindowsClipboardPlayerTests` に移した。値はサンプル画面と同じものを使う（`nativetoolkit-sample-no-such-item`、`uint.MaxValue`）。

| 手動確認 | どこで弾かれるか | 結果 |
|---|---|---|
| Copy Plain Text (null) | C# | 成功（`InvalidArgument`） |
| Copy Files (empty) | C# | 成功（`InvalidArgument`） |
| Copy Custom Format (blank name) | C# | 成功（`InvalidArgument`） |
| Copy Multiple Formats (empty) | C# | 成功（`InvalidArgument`） |
| Restore (blank id) | C#（受け付ける前に拒否） | 成功（requestId=0、`InvalidArgument`） |
| Restore (unknown id) | ネイティブ | 成功（requestId≠0、`ItemDeleted`） |
| Cancel (unknown id) | ネイティブ | 成功（`InvalidParameter`） |
| Copy After Shutdown | C# | 成功（`NotInitializedByHost`、2 回目の shutdown も完了） |

- 1〜4 のエラーメッセージは、Editor 側のテストでは一度も確かめられていなかった。Editor では初期化が通らず、
  引数の検査より手前で弾かれるため。Player に移して初めて自動で確かめられるようになった
- **Restore (unknown id) はクリップボード履歴（Win+V）が有効であることが前提**。無効なら OS は `HistoryDisabled` を返す。
  テストは冒頭で製品の API（`GetHistoryAvailability`）で確かめ、無効なら理由を付けてスキップする。
  スクリプトも実行前にレジストリを見て知らせ、結果の表示にスキップ件数を出す（スキップを合格と見誤らないため）
- 8 本はどれもクリップボードに書き込まない。テストは名前の順に走り、往復テストの後に 2 本走ったが、
  層 3 の確認は見本値を読めた。この前提が実際に確かめられた
- テストごとに初期化とシャットダウンを繰り返しても、1.x では再初期化が毎回成功した。
  2.0.0 では S-3（2 回目の初期化が `NOT_SUPPORTED`）が関わるので、移行後に最初に見る箇所になる

`--skip-build` での実行結果: EditMode 809/809、PlayMode 181/181、Player **9/9**（スキップ 0）、クリップボードの確認も合格。

#### 手動確認 9 章（履歴の Await）を移した（2026-09-26）

9 章の 6 項目を `WindowsClipboardPlayerTests` に移した。

| 手動確認 | テスト | 実開発機の履歴への影響 |
|---|---|---|
| Get Availability (Await) | `GetHistoryAvailabilityAsync_Answers` | なし |
| Get History (Await + Cancel) | `GetHistoryAsync_AlreadyCancelled_...`（S-9。サンプルと同じく取り消し済みのトークンで呼ぶ） | なし |
| Get History (Await) | `GetHistoryAsync_FindsWhatWasJustCopied_...` | 自分で足した項目を最後に消す |
| Restore (Await) | `RestoreHistoryItemAsync_PutsAnOlderItemBackOnTheClipboard` | 同上 |
| Delete (Await) | `DeleteHistoryItemAsync_RemovesTheItem` | 同上 |
| Clear Unpinned (Await) | `ClearUnpinnedHistoryAsync_RemovesUnpinnedItems` | **ピン留めしていない履歴がすべて消える** |

- **履歴は実際の開発機のもの**（Win+V、上限 25 件）。テストが項目を足すたびに開発者の項目が押し出されるので、
  テストは毎回違う目印の値だけを扱い、足した項目は最後に自分で消す。最後に見本値を書き戻す処理（`[TearDown]`）は
  `Sensitive`（履歴にもクラウドにも残さない）で書く
- **Clear Unpinned は他の項目も消すので、`Destructive` カテゴリにした。** スクリプトは既定で
  `-testCategory "!Destructive"` を渡して除外し、`--include-destructive` のときだけ含める。
  カテゴリ名はテストのソースから読む。**このテストはまだ一度も実行していない**（実行すると開発機の履歴が消えるため）
- **S-2 の網を張った。** Get History のテストは、見つけた項目の時刻が今から 10 分以内かを確かめる。
  1.x のタイムスタンプは FILETIME、2.0.0 は Unix ミリ秒で、変換を直し忘れると 1601 年 1 月になる

#### 実行して分かったこと（2026-09-26）

**1. 今のクリップボードの中身が履歴から除外されていると、Restore は「成功」を返すのに中身を差し替えない。**
最初の版のテストは、復元の前に見本値を `Sensitive` で書いていた。その状態で復元すると、成功が返るのに、
アプリの中からもアプリの外（別プロセスの `Get-Clipboard`）からも、中身は見本値のままだった。
復元の前に書く値を「履歴に残る 2 つ目の目印」に変えると（native-toolkit の同じテストと同じ手順）、中からも外からも復元した値になった。
手動確認（M-14、9 章）は成功の戻り値と件数しか見ておらず、中身が差し替わるかは今回初めて確かめた。

**Windows 側の挙動として確定した（2026-09-26）。** native-toolkit に報告し、向こうが 2.0.0 の現在のコードで再現した。
3 通りを試し、`Sensitive` と **`ExcludeHistory` だけ** のどちらでも失敗、普通に書いた値なら成功。
**原因は履歴の除外だけで、クラウドの除外（`ExcludeRoaming`）は関係ない。**
Windows の `SetHistoryItemAsContentStatus` は成功・アクセス拒否・削除済みの 3 値しかなく、「差し替えを断った」ことを表す値がない。
ライブラリは受け取った結果をそのまま返しており、1.x と 2.0.0 で処理は同じ。
向こうが測ったのは C++ の API（`Session::RestoreHistoryItem`）で、C API の入口そのものではない（C API は処理を渡すだけ）。

native-toolkit は**検出せずドキュメントに書く**と決め、`feature/NTKIT-16` の `67217afb` で次の 4 か所に同じ注意書きを入れた。
C++ API のヘッダー、C API のヘッダー（`ntk_clipboard_restore_history_item`）、`manual/1.12.0/clipboard.md` の復元の節（3 言語）、
再現記録 `artifact/topics/windows-architecture/results/2026-09-26-windows-clipboard-history-restore-finding.md`。
趣旨は「成功は Windows が要求を受け付けたという意味で、クリップボードがその項目になったという意味ではない。
差し替えられる側が excludeFromHistory で書かれていると中身は変わらない。確実に知りたければ読み戻すか GetClipboardSequenceNumber を比べる」。

- 利用者から見ると、パスワードを `Sensitive` でコピーした直後に履歴から復元すると、成功と言われるのに戻らない
- **この挙動そのものを確かめるテストは作らない。** native-toolkit も再現用のテストは残さなかった。
  Windows の挙動が変われば失敗するだけで、このライブラリについては何も分からないため
- **残作業: このリポジトリの Unity 向けマニュアルにも同じ注意書きを入れる。** Unity の利用者は native-toolkit のヘッダーを読まない。
  入れる場所は `clipboard.md` の Windows の章、`#### Restore, Delete, Clear` の節（1.11.0 では 2586 行目）。
  マニュアルは版ごとに公開しているので、次の版を作るときに入れる

**2. 履歴の API は、テスト用 Player のウィンドウが前面にないと動かない（`NotForeground`）。**
1 回、履歴のテストが「履歴が無効」としてスキップされた。原因は補助関数の誤りで、問い合わせの失敗まで「無効」と扱っていた。
問い合わせの失敗はエラーコード付きの失敗として報告するよう直した。スキップされた回の失敗理由は残っていないが、
M-13（非フォアグラウンド）と同じ制約である可能性が高い。**無人で実行する間は、その PC を操作しないことが前提になる**

**3. テストの中から、別プロセスで OS のクリップボードを読める。**
Restore のテストは、テストの中から PowerShell を起動して `Get-Clipboard` で読む。
これで、**スクリプトの最後に 1 回だけ確かめる層 3 の制約（最後の 1 件しか見られない）が、項目ごとに解ける**。
出力は UTF-8 に固定した。この PC は既定がもともと UTF-8（コードページ 65001）で、日本語も固定の有無にかかわらず正しく読めた。
既定が 932 の PC で固定が効くかは確かめていない

`--skip-build` での実行結果: EditMode 809/809、PlayMode 181/181、Player **14/14**（スキップ 0）、クリップボードの確認も合格。

#### S 観点に着手した（2026-09-26）

S 観点は 3 段階に分けて移す。

1. **S-7**（Manager を直接呼ぶ。UI 不要）: **完了**。`CopyPlainText_FromAWorkerThread_IsRefusedAndAnsweredOnTheMainThread`。
   サンプルと同じく Manager はメインスレッドで取得し、別スレッドから `CopyPlainText` を呼ぶ。
   戻り値が `MainThreadRequired`、別スレッドで例外が出ない、コールバックがメインスレッドで届く、の 3 点を確かめる
2. **UI を操作する仕組み**: サンプルのシーンをテスト用 Player で読み込み、トップメニューから Clipboard 画面へボタンで移る（S-1）。
   行き来を 2 回して `OnDisable` → `OnDestroy` が対になることを確かめる（S-5 / S-6）。
   テスト用 Player にはビルド設定のシーンがすべて入る（`PlayerLauncher` が `EditorBuildSettings.scenes` を足す）ので、サンプルのシーンはそのまま読める
3. **全ボタンを押す**（S-2）。終わったら Player のログを `scripts/check_windows_clipboard_sample_log.py` に渡して S-2 / S-4 / S-8 を判定する。
   ボタン 67 個のうち、`QuitButton`（Player が終了する）、`ClearUnpinned` 系 2 個（開発機の履歴が消える）、
   `RestoreLast` / `DeleteLast` / `RestoreAwait` / `DeleteAwait`（直前に取った履歴の先頭、つまり開発者の項目を戻す・消す）は通常の実行から外す

S-3 と S-9 は、D と 9 章のテストで実質的に確かめている。

#### ファイアウォールのダイアログ（2026-09-26 対応）

テスト用 Player は Editor に結果を返すために待ち受ける。Windows は、規則のないプログラムが
待ち受けを始めると「アクセスを許可しますか」というダイアログを出す。Test Framework の既定では
テスト用 Player が毎回 `Temp/UnityTempFile-<毎回違う値>/PlayerWithTests/PlayerWithTests.exe` に作られるため、
**Windows からは毎回別のプログラムに見え、実行のたびにダイアログで止まっていた**。
許可しても次の実行には効かず、規則だけが溜まった（3 回の実行で 6 件、すべてパブリックで許可）。

対応:

- `verify_unity_windows.sh` の Player テストに **`-buildPlayerPath`** を付け、出力先を
  `Temp/NativeToolkitTestPlayer/PlayerWithTests/PlayerWithTests.exe` に固定した。
  Test Framework のコマンドライン引数で、出力先を変えるだけで、ビルドのみのモード（`buildOnly`）には
  ならない（`SettingsBuilder.cs` と `PlayerLauncher.cs` で確認）。コードは不要だった
- そのパスに対して**受信ブロック**の規則を全プロファイルに 1 つ作る（管理者で 1 回だけ）。
  規則があればダイアログは出ない。ブロックにしたのは、Player は同じ PC の Editor に届けば足り、
  ネットワーク上の他の機器から接続される必要がないため
- スクリプトは実行前に規則の有無を確かめ、無ければ作成コマンドを表示する

```powershell
New-NetFirewallRule -DisplayName 'NativeToolkit test player' -Direction Inbound -Action Block -Profile Any -Program '<プロジェクト>\Temp\NativeToolkitTestPlayer\PlayerWithTests\PlayerWithTests.exe'
```

**確かめたこと:** 規則を作った後に `--skip-build` でスクリプトを回し、EditMode 809/809、PlayMode 181/181、
Player 1/1、クリップボードの確認も合格。**受信ブロックの規則があっても、結果は Editor に届いた**
（同じ PC の中の通信はファイアウォールで止まらない）。実行の前後でファイアウォール規則の数は変わらず、
実行中に新しい規則は作られていない。

規則はプロジェクトの場所ごとに要る。別の PC や別の場所に clone した場合は、スクリプトの表示に従って作る。

### 100% にはならない

native-toolkit も `ComputerUse/CU-01-hero-image.md` で
「通知に出ている画像が `StoreLogo.png` かどうかは UI Automation では分からない」として
computer use に切り出している。こちらでも、貼り付け先アプリでの見え方、
機微フラグのプレビュー抑制、通知の見た目は層 3 でも取り切れない。
