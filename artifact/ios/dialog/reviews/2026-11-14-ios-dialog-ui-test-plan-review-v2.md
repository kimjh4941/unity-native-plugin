# iOS Dialog UI 自動テスト計画 v2 のレビュー v2（差分のレビュー）

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-dialog-ui-test-plan-v2.md](../designs/2026-11-14-ios-dialog-ui-test-plan-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。2 回目なので差分のレビュー
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

### 判定表（レビュー v1 で直すと決めた項目）

| 項目 | 判定 | 一言 |
|---|---|---|
| A1（null と空文字、全文一致） | 直った | 23 行で書き分けと全文一致を定めた。ID-10・13 の 1.x 予想は 1.13.0 タグの `UnityIosDialogManager.swift:233・280` と合う |
| R-2 / Codex 2（2 つ続けたとき） | 一部 | ID-14a はよい。ID-14b はこのままでは自動化できない（R2-1） |
| R-3（ちょうど 1 回） | 直った | 文言の食い違いが小さく残る（R2-4） |
| R-4（ログインはシミュレーターだけ） | 直った | ID-12・13・16 |
| R-5（シミュレーターだけの印） | 直った | 3 章に 12・13・15・16・17 を挙げ、ランナーに印を足す作業も書いた |
| R-6 の一部（ログインの反転の真の側） | 直った | ID-16 の `username="" password-length=0` は 1.13.0 の L404-409 と合う（空の UITextField の text は `""`） |
| Codex 3（foregroundInactive） | 一部 | ID-17 を足したが、Unity の止まり方のせいで場面が作れない（R2-2）。根拠に挙げたファイルも違う（R2-3） |

### 指摘

#### R2-1 [B 中心契約] ID-14b: 1 つ目のアラートが出ている間はハーネスの行を押せない
- **根拠**: ハーネスの行は Unity のルートビューの子ビューで、アラートの下にある（`NtkUiHarnessOverlay.mm:27-30`）。アラートはモーダルなので、下にあるボタンは押せない。`HarnessUITestCase.run()` は `button.tap()` を呼ぶだけ（L58-63）。
- **壊れ方**: 押せない要素への tap は、失敗になるか、何も起きない。2 つ目が呼ばれないので、1.x の予想（両方 1 回ずつ）を確かめられない。
- **直し案**: 1 つの行の中で、1 つ目を呼び、決まった秒数の後に C# 側で 2 つ目を呼ぶ（アラートは非活性にしないので Unity は止まらない）。XCUITest は `OK2` が出るのを待つ。

#### R2-2 [B 中心契約] ID-17: Control Center を開くと Unity が止まり、3 秒の遅れが進まない
- **根拠**: Unity 6000.4.2f1 の Trampoline `UnityAppController.mm:606-648`。`applicationWillResignActive` で `UnityPause(1)` を呼ぶ。Control Center を開くとこれが呼ばれる。ハーネスの行は C# のラムダ（`DialogScenario.cs:14`）。
- **壊れ方**: 遅れをコルーチンなどの C# で書くと、`ShowDialog` が呼ばれるのは Control Center を閉じて活性に戻った後になる。すると 1.x も `2.0.0` も普通に表示して「同じ」になる。結果、「既知の差分の候補」が消えたまま、5 章の 1.x の列が書き換えられうる。A1 にしないのは、閉じた後に遅れてアラートが出るので、tearDown や待ち時間のずれから流せば気づきやすいため。
- **直し案**: 遅れはネイティブ側で作る（overlay の `dispatch_after` で行のコールバックを呼ぶ）と書く。呼んだ時点の `activationState` を結果の行に出し、`foregroundInactive` だったことを確かめる（native-toolkit の D22 の `@foregroundInactive` と同じ考え）。

#### R2-3 [A2] ID-17: 1.x の根拠のファイルが出荷物と違い、「0 回」の予想が成り立たない
- **根拠**: Unity が積んでいるのは `dist/1.13.0`（`Plugins/iOS/VERSION.txt`）。1.13.0 タグには `ForegroundLocator.swift` が無い。1.x の探し方は 1.13.0 タグの `IosDialogManager.swift:147-153`（`foregroundActive` だけを探す）。見つからなければ L92-95 で `completion(false, "Failed to get root view controller")` を返す。ブリッジはこれをそのまま渡す（`UnityIosDialogManager.swift:76-80`）。
- **壊れ方**: 予想は「ちょうど 1 回 `[dialog@fail] error=Failed to get root view controller`」の 1 通りで、0 回にはならない。今の根拠のままでは、`2.0.0` 側と比べるときに読み違えるおそれがある。
- **直し案**: 根拠を 1.13.0 タグの行に替え、予想を上の 1 行にする。

#### R2-4 [C] 「その行 id の結果の行がちょうど 1 本」が ID-14a・14b と食い違う
- 23 行は全部の ID で 1 本とし、例外を「0 回の場面」に限っている。しかし ID-14a の `2.0.0` と ID-14b は 2 本になる。23 行に「ID-14 は表の本数」と足す。

### 新しい予想のうち、一次ソースと合っていたもの
- ID-10・13・16 は上の判定表のとおり。
- ID-14a の 1.x（2 つ目は表示中の 1 つ目の上に出そうとして断られ、0 回）は、1.13.0 の L91・L155-158・L136 と合う。
- ID-17 の `2.0.0`（`DialogReceiver.swift:45` の `.foreground` が `foregroundInactive` も受け付ける。`ForegroundLocator.swift:52-61`）も合う。
- Control Center をシミュレーターで座標で開く操作は、native-toolkit の `Springboard.openControlCenter` に実績がある。自動化はできる。
- `Dialog.h` の 167〜172 行（確定でないときは NULL）と 30 行（exactly once）の引用は正しい。

### 6 章の確認
3 章で直さないと決めた R-6 の残り、R-7、R-8、R-9、R-10 が全部、理由と扱う場所つきで載っている。漏れは無い。R-8 の根拠（`UnityIosDialogManagerBridge.m:223` でパスワードを平文でログに出す）も確かめた。

**件数**: A1 0 件、A2 1 件、B 2 件（どちらも中心契約）、C 1 件。

## 2. Codex

前回指摘の判定：

1. 修正済み。ID-10／13は null と空文字を区別し、全文一致で比較する。キャンセル値も一次ソースと一致する（v2:23、UnityIosDialogManager.swift:233、同:280）。
2. 一部修正。ID-14a／14bへの分割は済んだが、予測と操作手順に残件がある（v2:40、下記）。
3. 項目追加は修正済み。ID-17が非活性中の要求を扱う。受付条件の差はソースと一致するが、期待値と成立条件に残件がある（v2:44、ForegroundLocator.swift:40、同:62）。

変更行の指摘（A1に該当する確証付き指摘はなし）：

- B：ID-14a は第二要求「0回」の予測が指定ソースと不一致。UIKitが拒否すると、PresentationProcedure.swift:244 が `.presentationFailed` で閉じる。旧APIも AlertDialogPresenter.swift:315 で失敗を報告し、UnityIosDialogManager.swift:114 から通知する。修正：指定ソースでは第一要求の成功1回＋第二要求の失敗1回と予測し、移行後の成功2回との差を状態込みで比較する。回数検証で発覚するため A1 ではない。
- B：ID-14b は第一アラート表示後に背後の行を押せない。行は Unity の root view 内にある（NtkUiHarnessOverlay.mm:27）。アラートは別の ViewController としてモーダル表示される（PresentationProcedure.swift:319）。修正：一つの行から両要求を予約し、第一の表示完了後に第二をプログラムで呼ぶ。
- B：ID-17 は非活性成立の確認がなく、「失敗か0回」も広すぎる。指定ソースでは検索失敗を AlertDialogPresenter.swift:313 が通知する。参考実装は呼出時の scene 状態を記録し、テストでも `foregroundInactive` を照合している（CApiRow.swift:85、CApiRoundTripUITests.swift:537）。修正：非活性中に処理された証拠を記録・検証し、1.x は失敗1回、移行後は応答成功1回を期待する。

## 3. 判断

- **ラウンド 2 で、2 人とも A1 が 0 になった。レビューを止める**（差分のレビュー、レビュアーを替えて通した、直さない残件は計画の 6 章に理由つきで書いてある）
- ID-14a の 1.x の予想は、2 人で食い違った。Codex は今の native-toolkit のライブラリ（1b の後）を根拠にし、サブエージェントは出荷物の 1.13.0 のタグを根拠にした。この repo が積むのは dist 1.13.0（`Plugins/iOS/VERSION.txt`）なので、**サブエージェントに従い「2 つ目は 0 回の見込み」のまま**にする。実測が違えば 5 章で直す
- 止めた後の v3 で直すもの（新しいレビューには回さない）:
  - R2-1・Codex の 2 つ目（中心契約。ID-14b）: 1 つの行の中で 1 つ目を呼び、1 秒後に C# 側で 2 つ目を呼ぶ
  - R2-2・Codex の 3 つ目（中心契約。ID-17）: 遅れはネイティブ側（重ね表示の `dispatch_after`）で作り、呼んだ時点の `activationState` を結果の行に出して確かめる
  - R2-3（A2 だが予想の 1 行の直しで済む）: ID-17 の 1.x の根拠を 1.13.0 のタグの行に替え、予想を「ちょうど 1 回 `[dialog@fail] error=Failed to get root view controller`」にする
  - R2-4（C）: 「ちょうど 1 本」の例外に ID-14 を足す
