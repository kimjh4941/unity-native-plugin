# iOS Dialog の C ABI への写し方の設計 v1 のレビュー v1

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-dialog-cabi-design-v1.md](../designs/2026-11-15-ios-dialog-cabi-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

設計書 `/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/dialog/designs/2026-11-15-ios-dialog-cabi-design-v1.md` の 1 回目のレビューです。ファイルは編集していません。

**先に結論:** A1 に当たる指摘はありません。構造体の大きさ（32/40/40/48/56/64）、F-1〜F-4、F-6〜F-8、DL-05 の反転、D-12 の数（11 + `ntk_version`）、ID 1〜16 の値の対応は、ヘッダー・実装・1.x のコードと合っていました。直すべき中心は、B の 4 件（購読者の例外、3 章に無い差分、iPad、テストの抜け）です。

### R-1 [B] 観点 2・5: 購読者が例外を投げたときの扱いを決めていない
- 根拠
  - 1.x は、出すイベントごとに `try/catch` で囲み、`"DialogResult callback error: {ex.Message}"` などを `LogError` していた（`git show 93e6b8f:…/IosDialogManager.cs` の 152-162、168-178、184-194、200-228、234-244、250-260）。
  - `UnityMainThreadDispatcher.cs:55-63` は、例外が出ると `Update` から抜け、列の残りを次のフレームに回す。
  - 設計の D-7 と 4 章が「例外を外に出さない」と書いているのは、ネイティブのコールバックの側だけ。
- 壊れ方: `Enqueue` するラムダの中で `catch` しないと、利用者の購読者 1 人の例外が `Update` から漏れる。同じフレームに積まれたほかのイベント（Share・Clipboard などの dispatcher 経由のもの）が 1 フレーム遅れ、ログの形も 1.x と変わる。ハーネスの購読者は例外を投げないので、V-3 では捕まらない。
- 直し案
  - D-7 に「Enqueue するラムダの中で、1.x と同じ文言で `catch` して `LogError`」と書く。
  - Android の `Raise` のように、イベントを出す口を internal にし、EditMode で「例外を投げる購読者がいても後続が走る」ことを確かめる。

### R-2 [B] 観点 2・5: 3 章の対応表に、1.x と変わる場面が抜けている
- 根拠
  - (a) 1.x は `presentedViewController` を、閉じかけのものも含めてたどる（`git -C native-toolkit show 37615913:ios/IosLibrary/IosLibrary/Dialog/IosDialogManager.swift` の 85-98）。UIKit が表示を断ると、完了は呼ばれない（`PresentationProcedure.swift:241-243` の MS-01 の注記）。だから 1.x では、閉じかけの画面の上に出すとアラートが出ず、イベントは 0 回。2.0.0 は閉じかけのものを飛ばし（`ForegroundLocator.swift:72-74`）、1 回待つ（`PresentationProcedure.swift:189-214`）。その結果、出るか、`SHOW_FAILED` が 1 回来る。
  - (b) Editor（iOS ターゲット）の経路が違う。1.x は `__Internal` が見つからない例外で、呼び出しの中に `"Internal error: …"` が出た。2.0.0 は D-6(a) の文言になる。
  - (c) ID-17 の行はアラートだけを書いている。1.x のアクションシートは façade が呼び出しの時点で同期で root を探していた（`native-toolkit/ios/UnityIosPlugin/UnityIosPlugin/Dialog/UnityIosDialogManager.swift:177-197`）。非活性で出なかったのは 6 種類とも同じ。
- 壊れ方: (a) は「0 回 → 1 回」の差分なのに、既知の差分として記録されず、利用者への案内（段 4）からも漏れる。5 章にこれを確かめる項目は無い。
- 直し案: 3 章に (a) と (b) の行を足し、ID-17 の行を「6 種類」に広げる。

### R-3 [B] 観点 1・4: D-14 の理由「iPad の試験の環境が無い」は事実と合わない
- 根拠
  - `xcrun simctl list devices available` に iPad のシミュレーターがある（iPad Pro 11-inch (M4) など）。
  - `ProjectSettings/ProjectSettings.asset:12` は `targetDevice: 2`（iPhone と iPad）。
  - native-toolkit の第 2 部 `part2:330` は、iPad Pro 11-inch (M4) / 18.3 で外側のタップを確かめている（TB-5）。
  - 計画 v3 の 70 行は「iPad のシミュレーターで足すかを Dialog の写し方の設計で決める」と、この判断をこの設計に送っている。
  - D-14 の「1.x は `sourceRect` 無し」も不正確。1.x のライブラリは `sourceRect ?? sourceView.bounds`（同 37615913 の 63-65）なので、基点は view 全体の矩形だった。
- 壊れ方: 判断が誤った前提のまま閉じる。DL-04 にある「iPad で選択肢なしで出るおそれ」が 1.x と 2.0.0 でどう違うかを、誰も見ない。
- 直し案: iPad のシミュレーターで ID-7・ID-8 を流すかを決める。popover が XCUITest の `sheets` で取れるかの確かめも含める。流さないなら、正しい理由を書く。1.x の基点の記述を直す。

### R-4 [A2] 観点 3・5: UTF-8 の確保と読み取りの部品を置く場所が決まっていない
- 根拠: Android の `AllocUtf8`・`AllocStringArray`・`FreeAll`・`ReadUtf8OrNull` は `#if UNITY_ANDROID || UNITY_EDITOR` のファイルにある（`Runtime/Android/Common/AndroidNativeToolkitCApi.cs:3,86,121,130,155`）。設計の 4 章は「`List<IntPtr>` に集める」とだけ書き、結果を読む方法（`out_size` を使うか）も書いていない。
- 壊れ方: 手本どおりに Android の部品を呼ぶと、Editor では組めて EditMode も通るが、iOS の IL2CPP の Player だけ組めない。ただし V-2 のビルドで捕まる。
- 直し案: `IosNativeToolkitCApi`（共通部。後の Share・Clipboard・Notification も使う）に iOS 用の部品を置くと決め、EditMode のテストを付ける。選択肢のポインターの配列も同じ list で解放すると書く。

### R-5 [A2] 観点 4: 大きさのテストでは、項目の並びの誤りが捕まらない
- 根拠
  - 大きさが同じまま入れ替わる誤り（`title`/`message`、`username_placeholder`/`password_placeholder` など）は `Marshal.SizeOf` では見えない。
  - `DialogUITests.swift` は題・本文・placeholder を照らしていない（20-153）。
  - `size_t option_count` を `int` にしても、大きさは 48 のまま。
- 壊れ方: 題と本文が入れ替わる、ユーザー名欄に "Password" が出る、といった誤りが、V-1 から V-4 のどれも通ってしまう。
- 直し案: `Marshal.OffsetOf` で各項目の位置を照らすテストを足す。
  - text input: 0/4/8/16/24/32/40/48/52
  - login: 0/4/8/16/24/32/40/48/56/60
  - action sheet: 0/4/8/16/24/32/40

  あわせて、C# の型（`UIntPtr`、`int`、`uint`）を設計に書く。

### R-6 [B] 観点 4: D-6(a) の Editor の経路を確かめる項目が無い
- 根拠: `IosDialogManager` は `#if UNITY_IOS` なので、iOS Profile の Editor でも組まれる。D-6(a) は「Editor もここ」としているが、4 章のテストは CApi の変換だけ、`IosDialogPlayerTests` は `!UNITY_EDITOR`（`Tests/PlayMode/IosDialogPlayerTests.cs:3-4`）。
- 壊れ方: Editor で 6 つの `Show*` の回数・時点・文言が崩れても、5 章のどこでも分からない。
- 直し案: iOS Profile の EditMode か Editor の PlayMode に、6 つとも呼び出しの中で 1 回、値はすべて null、`"Failed to …: the native library is not available."` の全文、を照らすテストを足し、V-1 に入れる。

### R-7 [C] 観点 2: 「選択肢に null の要素」の 1.x の振る舞いの記述が誤っている
- 根拠: 1.x は `Encoding.UTF8.GetBytes(options[i])` が `ArgumentNullException` を投げ、ネイティブに届く前に `catch` する（93e6b8f の 419、429-447）。結果は、呼び出しの中で 1 回 `"Internal error: Value cannot be null…"` が出て、アラートは出ない。
- 壊れ方: 3 章の行と D-4 の「ネイティブに届く」は誤り。差分は文言だけで、時点と回数は同じ。`IosDialogPlayerTests.cs:22` のコメントと、記録の 74 行にも同じ誤りがある。
- 直し案: 1.x の列を「呼び出しの中で 1 回、`Internal error: …`」に直す。

### R-8 [C] 観点 1: F-2 と F-5 の網羅が一次ソースと少しずれている
- 根拠
  - `UNKNOWN` も完了で来うる（`DialogReceiver.swift:42,54`。箱が無い、知らないボタン）。
  - `NOT_SUPPORTED` も入口で返りうる（`StructReader.swift:115-119`）。
  - D-6 と D-9 の「ほか」の扱いで実害は無い。
- 直し案: F-5 を「`UNKNOWN` は通常は来ない」に、F-2 に「`NOT_SUPPORTED`（C# は `Marshal.SizeOf` ちょうどを渡すので来ない）」を足す。

### R-9 [C] 観点 3: D-7 で dispatcher を使う理由と、参照先が無い
- 根拠
  - `agent-rules/coding-rules/common.md:67`（Android は dispatcher を使わない）と `artifact/topics/dispatcher-runs-under-lock/README.md:13`（iOS のメインキューも挙げている）に対して、D-7 は理由を書いていない。
  - D-7 が参照する「F-14」は、この文書に無い（F は 1〜9）。
- 直し案: 「iOS の完了は Unity と同じ主スレッドで来るので、ロックで待たない」と書く。F-14 の参照先を直す。

### R-10 [C] 観点 3・5: 文言と組み立ての細部が曖昧
- `MethodName` の「1.x の関数名」が、C# の `ShowDialog` なのか native の `showDialog` なのか分からない。文言はテストで全文を照らすので、6 つの文字列を書く。
- D-11 で、関数ポインター（静的 delegate と `GetFunctionPointerForDelegate`）と show の呼び出しを、`!UNITY_EDITOR` の内と外のどちらに置くかを明記する。
- 3 章の「外から消された」の出典は DL-02 ではなく DL-01（02〜06 は「同上」）。

### R-11 [C] 観点 4・5: 書いていない変更と確かめ
- 更新が要る箇所
  - `DialogUITests.swift:3-4`（「Expected lines are the 1.x record」）
  - `IosDialogPlayerTests.cs:15-22` のコメント
  - `IosDialogManager` の XML ドキュメント（"Unavailable for now"）
- 仮の経路のテスト `UnavailableUntilMoved`（`IosDialogPlayerTests.cs:133-145`）は、組まれなくなるだけで残る。3-S の 1.6 は「仮の文言は段 3 で消える」としているので、消すかどうかを決める。
- 4 章の文書の道は `agent-rules/coding-rules/common.md`・`testing.md` が正しい。`testing.md:237` の表の行も対象にする。
- 計画 v3 の 4 章にある「サンプルの画面（`IosDialogManagerExampleController`）を移行の後に手で一度見る」が、5 章に無い。
- ID-17 の 2.0.0 の期待では、`if alert.waitForExistence` を `XCTAssertTrue` にする。

件数: A1 0 件、A2 2 件、B 4 件、C 5 件

## 2. Codex

1. **R2-1 — B：外部消去時の完了保証に必要な条件が抜けている**

   **根拠：** [設計書:52](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/dialog/designs/2026-11-15-ios-dialog-cabi-design-v1.md:52) は「失敗が1回」としています。しかし、[Dialog.h:65](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Dialog.h:65) は、window/root の交換後もダイアログが保持される場合、callback が来ないと明記しています。検出処理も [ReleaseWatch.swift:42](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibrary/IosLibrary/Common/Presentation/ReleaseWatch.swift:42) の `deinit` にあります。

   **失敗シナリオ：** ホストが表示済みダイアログを強参照したまま root を交換すると、画面から消えても保持中は完了イベントが来ません。外部消去のテストは設計書:52で対象外です。

   **修正：** 「ボタン無しで消え、ダイアログが解放された場合」と限定し、ホスト保持時の完了非保証を明記してください。

2. **R2-2 — B：同じ総サイズになるメンバー配置の誤りを検出できない**

   **根拠：** [設計書:64](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/dialog/designs/2026-11-15-ios-dialog-cabi-design-v1.md:64) のレイアウト検査は総サイズです。[Dialog.h:75](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64/IosLibraryCApi.framework/Headers/NativeToolkitC/Dialog.h:75) から計算すると、alert は32バイト、`title` は offset 8、`message` は offset 16です。[DialogUITests.swift:20](/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/DialogUITests.swift:20) はボタンと結果行だけを照合しています。

   **検出漏れ例：** C# 構造体の `Title` と `Message` の宣言順を逆にしても32バイトです。Swift はそれぞれを別の表示項目として読むため、題と本文が逆転しますが、既存の UI 検証は通ります（[DialogMapping.swift:27](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Dialog/DialogMapping.swift:27)）。

   **修正：** V-1 に各フィールドの `Marshal.OffsetOf` 検査を追加してください。

3. **R2-3 — B：V-3 のログイン検証はパスワードの内容破損を検出できない**

   **根拠：** [設計書:38](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/dialog/designs/2026-11-15-ios-dialog-cabi-design-v1.md:38) は password の返却を契約にしています。実装も入力値を結果に渡します（[DialogMapping.swift:107](/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Dialog/DialogMapping.swift:107)）。一方、ハーネスは長さしか出力せず（[DialogScenario.cs:27](/Users/jonghyunkim/Desktop/unity-native-plugin/Assets/NativeToolkitIosUiHarness/Runtime/Scenarios/DialogScenario.cs:27)）、ID-12 は `pw1234` の入力に対して `password-length=6` だけを照合します（[DialogUITests.swift:82](/Users/jonghyunkim/Desktop/unity-native-plugin/Tools~/IosUiTests/NtkUiTests/DialogUITests.swift:82)）。

   **検出漏れ例：** ネイティブ結果の読み取りで誤って `"******"` を返しても、長さが同じなので合格します。

   **修正：** ハーネス内で固定 fixture との完全一致を確認し、内容を表示せず `password-match=True` を照合してください。

4. **R2-4 — C：1.x の null 選択肢がネイティブに到達するという説明が誤っている**

   **根拠：** [設計書:34](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/dialog/designs/2026-11-15-ios-dialog-cabi-design-v1.md:34) と [設計書:54](/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/ios/dialog/designs/2026-11-15-ios-dialog-cabi-design-v1.md:54) に対し、指定コミット `93e6b8f` の `Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Dialog/IosDialogManager.cs:419` は、ネイティブ呼び出しの427行より前に `Encoding.UTF8.GetBytes(options[i])` を実行します。null 要素ではここで例外となり、446行のイベントへ進みます。

   **失敗シナリオ：** `new string[] { null }` はネイティブに届かず、C# 内で同期的に `Internal error: …` を返します。

   **修正：** 既知の差分を「C# 内の同期的な例外通知から、明示的な入力検証と固定文言への変更」と訂正してください。

補足：構造体サイズ 32/40/40/48/56/64 バイト（pack 8）は、Codex が独立に再計算した結果、設計書の記載と一致しました。A1 に該当する指摘はありません。

## 3. 判断

**2 人とも A1 が 0 で、止めた。** 止める基準（A1 が 0）を 1 回目で満たした。止めた後の直しは v2 に入れ、レビューには出さない。直すのは中心の約束の B と、安く捕まえられる A2・C だけ。

| 指摘 | 判断 | v2 での直し |
|---|---|---|
| サブの R-1（購読者の例外） | 直す（B） | D-7 に、`Enqueue` するラムダの中で 1.x と同じ文言で `catch` して `LogError` を書く。イベントを出す口を internal にし、EditMode で確かめる |
| サブの R-2（3 章の抜け） | 直す（B） | 3 章に「閉じかけの画面の上」と「Editor」の行を足し、ID-17 の行を 6 種類に |
| サブの R-3（iPad） | 直す（B） | D-14 の理由を直す: この段では流さない。今のランナー（`test_ios_ui.sh`）は機種を 1 つに決めていて、iPad の popover を XCUITest で押す形も無い。段 4 で iPad のシミュレーターの回を足すかを決める。1.x の基点の記述も直す |
| サブの R-4（UTF-8 の部品） | 直す（A2、安い） | `IosNativeToolkitCApi` に iOS の部品（確保・配列・解放・読み取り）を置き、EditMode で確かめる |
| サブの R-5、Codex の R2-2（項目の位置） | 直す（A2/B） | `Marshal.OffsetOf` で全項目の位置を照らすテストを足す。C# の型を書く |
| サブの R-6（Editor の経路） | 直す（B） | iOS Profile の Editor の PlayMode に、6 つとも呼び出しの中で 1 回、全文を照らすテストを足す |
| サブの R-7、Codex の R2-4（null の要素の 1.x） | 直す（C、事実の誤り） | 1.x は呼び出しの中で 1 回 `Internal error: …`。差分は文言だけ |
| サブの R-8（F-2・F-5） | 直す（C、安い） | 書き足す |
| サブの R-9（dispatcher の理由、F-14） | 直す（C） | 理由を書き、参照を 2c 計画 v4 の F-14 に |
| サブの R-10（文言の細部） | 直す（C） | 6 つの関数名を書く。関数ポインターと呼び出しの置き場所を書く。DL-01 に |
| サブの R-11（書いていない変更） | 直す（C） | 4 章に足す。`UnavailableUntilMoved` の Dialog の分は消す。サンプルの画面の手の確かめは実機の回の後に利用者へ頼む項目として 5 章に |
| Codex の R2-1（外から消されたとき） | 直す（B） | 「消えて解放されたとき 1 回。ホストが保持し続ければ来ない（`Dialog.h:65`）」と書く |
| Codex の R2-3（パスワード） | 直す（B） | ハーネスで固定の値と全文が合うかを `password-match=True` として出し、ID-12 で照らす（値そのものは出さない） |
