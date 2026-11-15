# iOS の下の層が C ABI 2.0.0（`IosLibraryCApi`）に置き換わり、ブリッジを P/Invoke に書き直す

- 記録日: 2026-11-14
- 分類: 横断課題（移行）。**機能追加ではない。** clipboard / dialog / notification / share の 4 機能と、iOS のビルドの処理にまたがる
- 発見経緯: native-toolkit の段階 2c の申し送り（`artifact/topics/ios-c-abi/results/2026-11-07-ios-c-abi-stage2c-handoff.md`、2026-11-14 改訂）。iOS 2.0.0 で Unity 向けのブリッジ（`unity-ios-native-toolkit-*.xcframework`、`UnityIosPlugin`）を廃し、汎用の C ABI（`ios-native-toolkit-capi-2.0.0.xcframework`、`IosLibraryCApi.framework`）にした。配布物は native-toolkit の `dist/1.14.0/ios`（`3aeeaf3c`）
- 対応方針: 各 Manager の公開 API を保ち、下の層（ブリッジの 43 関数と JSON）だけを C ABI（`ntk_*`、134 関数）の P/Invoke に置き換える。進め方は [android-c-abi-2](../android-c-abi-2/README.md) に倣うが、**4 機能を一度に切り替える**（1.x と 2.0.0 の `IosLibrary.framework` は同じ名前で、同じアプリに入れられない。native-toolkit の README D-12、D-20）
- 進捗: **段 1（スパイク）まで済み、段 2 の仕組みを作り、シミュレーターで (A) Player のテストと (B) UI の自動化が通った（2026-11-14）。段 2 の残りは実機。** 段 0（移す前の基準）は済み（6.1）。段 1 の結果で I-1 を決めた（Embed は `PluginImporter`、口は `.mm` だけ。6.3）。進め方は設計書 [designs/2026-11-14-ios-c-abi-2c-plan-design-v4.md](designs/2026-11-14-ios-c-abi-2c-plan-design-v4.md)（v1〜v3 とレビュー 2 回の後、利用者の判断を書き込んだ版）。**Android の移行とほぼ同じ方針**。レビュー v1 で見つかった、利用者の環境で壊れていたもの 3 件は先に直した（6.2）
- **2c の成功が、native-toolkit の次のリリース（iOS 2.0.0）の条件**。native-toolkit は 2c が終わるまで出さない
- 対象: `Runtime/{Clipboard,Dialog,Notification,Share}/Ios*.cs`、`Runtime/UI/iOS/`、`Editor/Build/`（`PreBuildProcessor`、`PostBuildProcessor`、`Tools/iOS/`）、`Editor/iOS/`、`Plugins/iOS/`、対応するテストとサンプル
- ブランチ: `feature/UNT-14`（利用者の指示。1a の下調べと、dist/1.14.0 の前の固定 `95905d1` と同じブランチ）
- 相手側: native-toolkit `artifact/topics/ios-c-abi/`。問い合わせと結果の報告は native-toolkit のセッションへ
- 前段: [ios-c-abi-handoff](../ios-c-abi-handoff/README.md)（1a の Unity と実機での下調べ、dist/1.14.0 の前に入れた固定）

---

## 1. 何が変わるか

申し送りの 1 章の要約。

| | 1.x（今） | 2.0.0 |
|---|---|---|
| 呼ぶもの | ブリッジの 43 関数（`DllImport("__Internal")`。Clipboard 15、Dialog 6、Notification 21、Share 1。`[DllImport(` の行を数えた） | `ntk_*`（134 関数。同じく `__Internal`） |
| 結果 | `const char*`（JSON か文言）。`user_data` は無い | C の関数ポインタ（`[MonoPInvokeCallback]`）、構造体、**所有権ごと渡すハンドル**。登録ごとに `user_data` と `release` |
| JSON | C# とブリッジの間はすべて JSON | 残るのは Notification の公開 API、userInfo、`ntk_clipboard_detect_values_async` の結果の 3 つ |
| リスナー | 差し替え（static の上書き） | 足す・外す。差し替えは包みが保つ |
| 通知の delegate | `IosNotificationManager.Initialize()`（`Awake`）が `notificationSetup` を呼ぶ | `.mm` が `didFinishLaunching` で `ntk_notification_install` を呼ぶ |
| xcframework | `ios-native-toolkit-1.3.0` と `unity-ios-native-toolkit-1.3.0` | `ios-native-toolkit-2.0.0` と `ios-native-toolkit-capi-2.0.0`（両方を Embed & Sign。`-debug` は無い） |
| ヘッダー | 無い | 公開ヘッダー（C99）。C# の宣言はこれから書いて照らす |

## 2. 参照

native-toolkit のパス（このマシン上。`feature/NTKIT-18`）。

| 見るもの | 場所 |
|---|---|
| **こちらへの申し送り** | `artifact/topics/ios-c-abi/results/2026-11-07-ios-c-abi-stage2c-handoff.md` |
| ブリッジの 43 関数と C ABI の対応（「Unity の包みが保つこと」の列） | `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md` 8.3 |
| 包みに渡す事柄 | 同 16 章（申し送りの 4 章に書き直しがある） |
| スレッド、寿命、`release`、通知の delegate | `designs/2026-10-31-ios-c-abi-c-abi-design.md`（第 1 部）1.3、1.4、5.7、5.12 |
| 構造体の配置の期待値 | `ios/IosLibraryCApiTests/StructLayoutTests.m` |
| 配布物 | `dist/1.14.0/ios/`（xcframework 2 つ、`include/NativeToolkitC/` の 5 つ） |
| C から呼ぶ手本 | `ios/IosLibraryCApiSmoke/` |
| Unity と実機での下調べの結果 | このリポジトリの `artifact/topics/ios-c-abi-handoff/results/` |

## 3. Android と違うところ（写してはいけないもの）

申し送り 4 章と 1a の結果の要点。各機能の設計書はこれを前提にする。

| # | Android | iOS |
|---|---|---|
| B-1 | ライブラリ名 `"ntk"` | `"__Internal"`（UnityFramework にリンクした `ntk_*` を引く） |
| B-2 | コールバックは Android の main（UnityMain ではない）。列が必須 | **Unity と同じ main**。完了は関数が戻った後に来る（1a の U-1）。列は必須ではない |
| B-3 | 初期化の確かめ `ntk_android_is_initialized` | 無い。代わりに `.mm` の `ntk_notification_install` |
| B-4 | 1.x と 2.0.0 の AAR は 1 つの APK に同居できた（Android の 6.2） | **同居できない**。4 機能を一度に切り替える |
| B-5 | 32 ビット・Mono は対象外 | iOS は IL2CPP と 64 ビットだけ（もともと）。最小 iOS は 18.0 |
| B-6 | エラーの値・構造体の配置 | Android と同じ名前でも値や配置が違うものがある（申し送り 4.3）。期待値は iOS の `StructLayoutTests.m` から取る |
| B-7 | コールバックの例外 | 投げるとアプリが落ちる（1a の U-9。`Il2CppExceptionWrapper` で `std::terminate`）。**コールバックの本体は必ず try / catch** |

## 4. 決定

設計書 v4 の I-1〜I-17（2026-11-14）。要点だけを写す。理由と細部は設計書。

| # | 事柄 | 決定 |
|---|---|---|
| I-1 | xcframework を入れる口 | `NativeToolkit.Editor` の `IPostprocessBuildWithReport`（`Editor/iOS/IosXcodeProjectSetup.cs`）。利用者のビルドでも動く。道は `Path.GetFullPath("Packages/...")`。Xcode の API は `#if UNITY_IOS`。冪等。**段 1 で決めた（6.3）: リンクは Unity が、Embed は xcframework の `.meta` の `AddToEmbeddedBinaries` で Unity がする。口が足すのは `.mm` だけ（`UserNotifications.framework` は要らない）。メニュー「Configure Xcode Project」は段 4 で消す** |
| I-2 | `.mm` と入れない設定 | `.mm` は `Editor/iOS/NativeSource~/` に持ち、I-1 の口が UnityFramework に足す。入れない設定は定義シンボル `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL`（Editor のコンパイルの `#if` で読む） |
| I-3 | 入れ直し | 公開の `Initialize()` を入れ直しの口にする（`Awake` は受け手の登録だけ）。通知パッケージの `AuthorizationRequest` の後に呼ぶとマニュアルに書く |
| I-4 | 版の固定 | `Plugins/iOS/VERSION.txt`（`95905d1`）。3-S で 1.14.0 と 2.0.0 の 2 つに |
| I-5・I-6 | 写し方・保つ範囲 | Android の J-6・J-11 と同じ（ふつうの使い方を保つ） |
| I-7 | 完了の配送 | 受けて積み、次の `Update` で出す（Android の J-9 と同じ。1.x と同じ時点）。出るのは来た順 |
| I-8 | 公開の JSON Builder | `IosClipboardJsonBuilder`・`IosShareJsonBuilder` は段 4 で消す。`IosNotificationJsonBuilder` は残す |
| I-9 | 内部の JSON | 機能ごとの設計で決める |
| I-10 | フォルダと 5 型 | 3-0 で `Runtime/iOS/<Feature>/` に移す（`[MovedFrom(true, …)]`）。**接頭辞の無い 5 型は案 2**（段 4 で iOS 用の新しい型を作って替える。旧い型は macOS 用に残す。iOS の利用者には破壊的変更。利用者の判断） |
| I-11 | 切り替えの順 | **案 D**: 3-S で先に切り替え、移していない機能は「使えない」に落とし（Dialog と Notification には仮の経路）、1 つずつ移す（利用者の判断） |
| I-12 | 1.x の記録と UI の自動化 | **段 2 で、ほぼ自動の UI テストを作る**（Unity のシナリオの口 + XCUITest）。1.x の記録は 3-S の前に（利用者の判断） |
| I-13 | Player のテストの仕組み | `scripts/verify_unity_ios.sh`。Simulator.app を開かない。シミュレーターは Simulator SDK と arm64 |
| I-14 | 共通部 | `Runtime/iOS/Common/IosNativeToolkitCApi.cs`。major が 2 でなければ「使えない」 |
| I-15 | 宣言の照合 | 宣言と構造体を Editor でも組み、名前をヘッダーと照らす（数も確かめる） |
| I-16 | 最小 iOS | 18.0。3-S で Player Settings を直し、低ければビルドを止める確かめを置く |
| I-17 | 利用者の形での確かめ | 段 1・3-S・4 の終わりに、git の URL で入れた形で確かめる |

## 5. 進め方

設計書 v4 の 3 章。

| 段 | 中身 | 状態 |
|---|---|---|
| 0 | 移す前の基準（EditMode、Editor の PlayMode） | 完了（6.1） |
| 1 | スパイク（写しで）: 2.0.0 を入れる口と `.mm`、Unity の扱い、定義の効き方、最小 iOS、Xcode の版、git の URL の形。シミュレーターと実機で `ntk_version` と起動と `.mm` | 完了（6.3） |
| 2 | `verify_unity_ios.sh`、Editor 専用の PlayMode のテストを分ける、1.x の Player の基準、**UI の自動化の仕組み**（設計 → レビュー → 実装） | 完了。仕組みの設計は [designs/2026-11-14-ios-ui-automation-design-v4.md](designs/2026-11-14-ios-ui-automation-design-v4.md)（レビュー 3 回、v3 で A1 が 0 になって止めた）。スパイク（V-1〜V-8）はシミュレーターで済んだ（6.4。起動の引数は届かず、環境変数で渡す）。**(A) Player のテストを実装した**（[results/2026-11-14-ios-stage2a-player-tests-result-v1.md](results/2026-11-14-ios-stage2a-player-tests-result-v1.md)。`scripts/verify_unity_ios.sh`。1.x の Player の基準は 4 件成功）。**(B) UI の自動化も実装した**（[results/2026-11-14-ios-stage2b-ui-automation-result-v1.md](results/2026-11-14-ios-stage2b-ui-automation-result-v1.md)。`scripts/test_ios_ui.sh`。1.x のまま、シミュレーターで 4 件成功）。**実機（iPhone XS / iOS 18.7.2）でも (A) と (B) を流した**（6.6）。残っていた Dialog の ID-4 の実機での流し直しは、段 3 の Dialog の実機の回で済んだ（[artifact/ios/dialog/results/2026-11-15-ios-dialog-cabi-implementation-result-v1.md](../../ios/dialog/results/2026-11-15-ios-dialog-cabi-implementation-result-v1.md) の V-4。ID 1〜11 が成功）。**完了** |
| 3-0 | 4 機能の型を `Runtime/iOS/<Feature>/` に移す（5 型は動かさない） | 完了（6.5） |
| 3-R | 機能ごとの確かめる項目の表 → レビュー → 1.x のまま実機で記録（4 機能） | 完了。Dialog の表は決まった（[artifact/ios/dialog/designs/2026-11-14-ios-dialog-ui-test-plan-v3.md](../../ios/dialog/designs/2026-11-14-ios-dialog-ui-test-plan-v3.md)。レビュー 2 回で A1 が 0）。表の項目をハーネスと XCUITest に足し、**シミュレーターで 1.x を記録した**（[artifact/ios/dialog/results/2026-11-14-ios-dialog-1x-record-result-v1.md](../../ios/dialog/results/2026-11-14-ios-dialog-1x-record-result-v1.md)。18 件。ID-14a は予想と違い、2 つとも出た）。実機の記録は許可を得てから。**4 機能の表がすべて決まった**: Share（[v3](../../ios/share/designs/2026-11-14-ios-share-ui-test-plan-v3.md)）、Clipboard（[v4](../../ios/clipboard/designs/2026-11-14-ios-clipboard-ui-test-plan-v4.md)。レビュー 3 回）、Notification（[v4](../../ios/notification/designs/2026-11-14-ios-notification-ui-test-plan-v4.md)。レビュー 3 回）。**Share は 1.x をシミュレーターで記録した**（[artifact/ios/share/results/2026-11-14-ios-share-1x-record-result-v1.md](../../ios/share/results/2026-11-14-ios-share-1x-record-result-v1.md)。UI 18 件と Player 6 件。SH-13 は予想と違い、1 つの呼び出しの結果が失われる）。**Clipboard は Player のテスト 35 件を記録した**（[artifact/ios/clipboard/results/2026-11-14-ios-clipboard-1x-record-result-v1.md](../../ios/clipboard/results/2026-11-14-ios-clipboard-1x-record-result-v1.md)。検出は URL の項目に効かない、1 回の Copy で変化が 2 回）。Dialog の ID-20〜22 も Player で記録した。Clipboard の UI のテスト（CB-1〜5）も記録した。実装のレビュー（[reviews/2026-11-14-ios-stage3r-tests-implementation-review-v1.md](reviews/2026-11-14-ios-stage3r-tests-implementation-review-v1.md)）の後に直し、**実機（iPhone XS / iOS 18.7.2）でも Player の 46 件と UI の 12 件（Share 11、Clipboard の CB-5）が成功した**（Player のテストは development でないビルドにした）。**Notification も 1.x をシミュレーターで記録した**（[artifact/ios/notification/results/2026-11-15-ios-notification-1x-record-result-v1.md](../../ios/notification/results/2026-11-15-ios-notification-1x-record-result-v1.md)。Player 14 件、UI 22 件。`SetBadgeCount(-1)` は 1.x でも失敗、非活性のときの許可の要求は結果が来ない、category の読み書きは揺れる）。実機の Notification のための「入れ直さない」実行は設計が決まった（[designs/2026-11-15-ios-ui-keep-installed-design-v4.md](designs/2026-11-15-ios-ui-keep-installed-design-v4.md)。レビュー 3 回）。その 4 章の、今のランナーにもある 2 つの穴（終わりの戻しが人の編集まで戻す、後片付けの途中で止まるとアプリが残る）は両方のランナーで直した。「入れ直さない」実行も実装し、シミュレーターで NU-1・4・5・7・11 が通った（[results/2026-11-15-ios-ui-keep-installed-result-v1.md](results/2026-11-15-ios-ui-keep-installed-result-v1.md)）。両方のランナーは実行の lock を取る。Notification の実装のレビュー（[artifact/ios/notification/reviews/2026-11-15-ios-notification-ui-tests-implementation-review-v1.md](../../ios/notification/reviews/2026-11-15-ios-notification-ui-tests-implementation-review-v1.md)）の後、1.x の実測を全文で照らし、2c を待つ 2.0.0 の期待は「期待を書くこと」で落ちる形にした。1.x の汎用の失敗の文は端末の言語で訳される（実機で分かった）。**実機の `--keep-installed` の回で、NU-1・4・5・7 が成功した**（NU-11 は 1 回目に時間の上限で止まり、直した 2 回目は準備の待ちが切れた。NU-11 の実機の記録は取れていないが、アプリの中の一覧を読むだけなので、推奨案として 3-S に進む）。**段 3-R はこれで終わり** |
| 3-S | 切り替え（2.0.0、ブリッジの宣言を消す、「使えない」に落とす） | 完了。設計 [v3](designs/2026-11-15-ios-c-abi-3s-switch-design-v3.md)（レビュー 3 回）。2.0.0 の XCFramework は Unity が `.meta` でリンクと Embed をし、口（`IosXcodeProjectSetup`）は照らすだけ、通知の delegate は `NtkNotificationInstaller.mm` が起動時に入れる。4 機能は「使えない」に落とした（Dialog と Notification は仮の経路）。設計 v3 の確かめ 10 項目をシミュレーターで確かめ、**実機でも Player のテスト 14 件が成功した**（[results/2026-11-15-ios-c-abi-3s-switch-result-v1.md](results/2026-11-15-ios-c-abi-3s-switch-result-v1.md)）。git の URL で入れた形もシミュレーターで起動した（実機は段 4 の I-17 で）。移していない機能の UI のテストは `notMovedYet` で流さない |
| 3 | 機能ごとに: 写し方の設計 → レビュー → 実装 → 実機で 1.x と比べる → 実装のレビュー。Dialog → Share → Clipboard → Notification。**`2.0.0` に移した後のテストは、シミュレーターで全件成功してから実機で流す**（誤った移行が私物の端末に届かないように。段 3-R の実装のレビュー v1 の R-11）。テストの 1.x と 2.0.0 の期待は、Player では機能ごとの定義（Notification は `NTK_IOS_CABI_2_NOTIFICATION`）で、UI ではハーネスの最初の行 `[abi@event] 1|2` で分ける。**定義はその機能を移す段で入れる**（3-S では入れない。仮の経路に 2.0.0 の期待を当てないため）。2c を待つ期待は、2.0.0 の側で「期待を書くこと」と落ちる形にしてあるので、写し方の設計で書く | 進行中。**Dialog は完了**（設計 [v2](../../ios/dialog/designs/2026-11-15-ios-dialog-cabi-design-v2.md)、レビュー 1 回で A1 が 0。実装の記録 [results](../../ios/dialog/results/2026-11-15-ios-dialog-cabi-implementation-result-v1.md)。シミュレーターと実機で UI テストと Player のテストが成功。既知の差分は前面で非活性のときにも出ること〈ID-17〉と失敗の文言。実装のレビューで、補助のメソッドの名前 `Start` が Unity のメッセージとみなされる不具合を見つけて直した）。**Share も完了**（設計 [v3](../../ios/share/designs/2026-11-15-ios-share-cabi-design-v3.md)、レビュー 2 回。実装の記録 [results](../../ios/share/results/2026-11-15-ios-share-cabi-implementation-result-v1.md)。呼び出しごとの受け手の表、項目の整えは 1.x と同じ。**同じフレームで 2 回呼ぶと 1 つの呼び出しの完了が来ない**のは 2.0.0 でも同じで、`Share.h` の約束と食い違う native-toolkit の不具合。native のログで、UIKit に断られた 2 つ目のシートが解放されず `ReleaseWatch` が働かないことを確かめ、native-toolkit のセッションに申し送った〈2026-11-15〉）。**Clipboard も完了**（設計 [v5](../../ios/clipboard/designs/2026-11-15-ios-clipboard-cabi-design-v5.md)。レビュー 3 回で止まらず、v4・v5 はレビューしていない〈4 回目は回さない。利用者の判断〉。実装の記録 [results](../../ios/clipboard/results/2026-11-15-ios-clipboard-cabi-implementation-result-v1.md)。1.x の単一の実行を保ち、呼び出しの中で決まった結果はディスパッチャーの GameObject の常駐の部品が後のフレームに渡す。CP-1〜35 は 1.x と同じ値で通り、既知の差分は `Domain`・`NativeCode` が null、ログ、U+0000）。**Notification も完了し、段 3 は終わった**（設計 [v5](../../ios/notification/designs/2026-11-15-ios-notification-cabi-design-v5.md)。レビュー 3 回で 2 人とも A1 が 0、v4・v5 はレビューしていない。実装の記録 [results](../../ios/notification/results/2026-11-15-ios-notification-cabi-implementation-result-v1.md)。公開の JSON を 1.x と同じ規則で C# が読み、呼び出しごとに結果が届く。2c で待っていた期待をすべて書いた。実機でも Player 74 件と `--keep-installed` の 5 件が成功。NU-10〈非活性中の許可の要求は結果が来ない〉は、native のログで調べ直すと、テストがコントロールセンターを閉じられずアプリが非活性のまま〈Unity のループが止まる〉だったことが分かり、テストを直して結果が 1 本届くことを確かめた。報告はしない） |
| 3-E | 署名し直しの確かめ（Validate App は Personal Team ではしない） | 完了（[results/2026-11-15-ios-c-abi-3e-resign-result-v1.md](results/2026-11-15-ios-c-abi-3e-resign-result-v1.md)）。UI のハーネスを Release でアーカイブし、署名なしで配る 2 つの framework が、アプリと同じ開発用の証明書・同じ team で署名し直されたことを `codesign` で確かめた（`--verify --deep --strict` も通った）。配布用の証明書と Validate App は Program の登録が要るので残る |
| 4 | JSON Builder 2 つの削除、5 型の iOS 用の新しい型、メニュー、マニュアル、リリース 1.14.0。iOS のサンプルの画面の自動 UI テスト（Player のテストの中でサンプルのシーンのボタンを C# から押す。Android と同じ形。システムの画面は今のハーネス）。iOS 26 のシミュレーターの回の前に、共有のシートの閉じ方（`PopoverDismissRegion`）と見出しの題の読み方（`LP.CaptionBar.TopCaption`）を `SystemUI` に写す（native-toolkit の `ShareSheet.swift`） | 計画（[designs/2026-11-15-ios-c-abi-stage4-plan-v1.md](designs/2026-11-15-ios-c-abi-stage4-plan-v1.md)）。**サンプルの UI テストは Android と同じ深さにする**（利用者の判断、2026-11-15）: Android の `android_ui_driver.py` にあたる iOS の手伝い役（XCUITest が依頼を待ち、システムの画面を押す）を最初に作る。**手伝い役とサンプルの画面のテストはできた**（設計 [v3](designs/2026-11-15-ios-ui-driver-design-v3.md)、レビュー 2 回で A1 が 0。実装の記録 [results](results/2026-11-15-ios-ui-driver-implementation-result-v1.md)。実装のレビュー [v1](reviews/2026-11-16-ios-ui-driver-implementation-review-v1.md) を直した）。127.0.0.1 の TCP で Player のテストの依頼を受け、アラート・共有のシート・通知の許可・設定の画面を押す。Dialog 11・Share 15・Clipboard 2・Notification 7（入れ直した別の回）。**シミュレーターでも実機（iPhone XS）でもすべて成功**（`44349ce`）。スパイクで、iOS のサンプルの画面がセーフエリアに合わせていないことが分かった（利用者の判断を待つ。配置の確かめのテストはそれまでゲートに入れない） |

## 6. 記録

### 6.1 移行前の基準（2026-11-14、`95905d1`）

**移行前の基準: `95905d1`**（`feature/UNT-14`。リリース 1.13.0 の後に、1a の記録と iOS の xcframework の固定を足したもの。iOS の振る舞いは 1.13.0 と同じ）。

| 何を | 結果 | 条件 |
|---|---|---|
| EditMode | 1419 件中 1418 件が通過。**iOS の 234 件はすべて通過**（1.x の 212 件と、`95905d1` で足した `IosXcframeworkPinTests` 22 件） | macOS、Unity 6000.4.2f1、`-buildTarget iOS`。通らなかった 1 件は `WindowsClipboardSampleFixtureTests.TemporaryFilePathsAreAbsoluteAndUseBackslashes` の skip（Windows の Editor だけで確かめるテスト） |
| Editor の PlayMode | 300 件すべて通過。**iOS の 36 件**（`IosClipboardManagerIntegrationTests` 31、`IosShareManagerIntegrationTests` 5）を含む | 同上 |

iOS の EditMode の内訳（1.x の 212 件）:

| ファイル | 件数 |
|---|---|
| `IosClipboardJsonParserTests` | 46 |
| `IosClipboardJsonReaderTests` | 34 |
| `IosClipboardJsonBuilderTests` | 30 |
| `IosClipboardSampleStateTests` | 29 |
| `IosClipboardResultTests` | 21 |
| `IosClipboardManagerDispatchTests` | 17 |
| `IosShareJsonBuilderTests` | 16 |
| `IosClipboardSampleSceneWiringTests` | 7 |
| `IosShareManagerDispatchTests` | 6 |
| `IosShareResultTests` | 6 |

- Dialog と Notification の EditMode・PlayMode のテストは無い
- iOS の Player（シミュレーター・実機）でのテストは無い。1.x の実機での振る舞いの記録は、段 0 の残り（設計書 v1 の 3 章）

### 6.2 git の URL で入れた利用者の環境で壊れていたもの（2026-11-14）

設計書 v1 のレビュー（Codex とサブエージェント）の指摘を、使い捨てのプロジェクトに `git+file:///<このリポジトリ>?path=Packages/com.jonghyunkim.nativetoolkit#<コミット>` で入れて確かめた。このリポジトリの中では、パッケージが `Packages/` に埋め込まれていて、どちらも起きない。

| # | 何が | どの版で | 直し |
|---|---|---|---|
| U-1 | `NativeToolkit.Editor` のアセンブリが組まれない。asmdef の `"optionalUnityReferences": ["TestAssemblies"]` を Unity は `UNITY_INCLUDE_TESTS` の条件にし（UnityCsReference 6000.4 の `CustomScriptAssembly.cs`）、テストの対象でないパッケージでは組まない。中の `AndroidGradleDependencies`（Gradle の依存の追加）と `AndroidArchitectureCheck` が利用者の Android のビルドで動かない見込み（実機では確かめていない） | **1.13.0 から**（タグ 1.13.0 で `NativeToolkit.Editor.dll` が無いことを確かめた） | その行を消した。アセンブリは NUnit もテストの API も使っていない |
| U-2 | `95905d1` で `NativeToolkit.BuildTools.Editor` が `NativeToolkit.Editor` を参照したので、U-1 のため `IosFrameworkPatcher` が `CS0246` で止まり、利用者のプロジェクトがスクリプトのエラーになる | `95905d1`（未 push、未リリース） | U-1 の直しで解けた |
| U-3 | メニュー「Configure Xcode Project」（iOS）が、xcframework を `<プロジェクト>/Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS` で探す。git の URL の利用者ではパッケージは `Library/PackageCache/...` にあり、その道は無い | **1.x から**（タグ 1.13.0 の `IosFrameworkPatcher.cs:37`。macOS の `MacFrameworkPatcher.cs:37` も同じ） | iOS だけ `Path.GetFullPath("Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS")`（Unity が実際の場所に解く）にした。**macOS は範囲の外で残っている** |

- 直した後の確かめ: このリポジトリの EditMode 1419 件中 1418 件成功（1 件は Windows だけのテストの skip）。git の URL で入れた使い捨てのプロジェクトで、iOS と Android のどちらでも 3 つのアセンブリが組まれ、エラーは 0 件。iOS を書き出してメニューの処理（`IosFrameworkPatcher.Apply`）を呼ぶと、`Library/PackageCache` の 2 つの xcframework を足し、Embed & Sign の印が付いた
- **1.13.1 は出さず、1.14.0（2c の後のリリース）で直す**（2026-11-14、利用者の判断）。1.14.0 のリリースの前に、git の URL で入れた形で Android と iOS を実機で通す（実機は許可を得て）

### 6.3 段 1（スパイク）の結果（2026-11-14）

コミットしない写しで行った。このリポジトリの写しを git のリポジトリにして使い捨てのローカルのコミット（push しない）を 2 つ作り、使い捨ての Unity のプロジェクトに `git+file:///<写し>?path=Packages/com.jonghyunkim.nativetoolkit#<コミット>` で入れた（I-17 の形。パッケージは `Library/PackageCache` に置かれる）。

- 写しのコミット A: `dist/1.14.0/ios` の 2 つの xcframework（`.meta` は iOS だけ有効、それ以外の設定は無し）、`VERSION.txt` を 1.14.0 に、`Editor/iOS/IosXcodeProjectSetup.cs`（I-1 の口。リンクと Embed が無ければ足す、`UserNotifications.framework` と `.mm` を足す。前後の数をログに出す）、`Editor/iOS/NativeSource~/IosNotificationInstaller.mm`（`didFinishLaunching:` で `ntk_notification_install()` を呼び、戻り値を `NSLog` する）
- 写しのコミット B: A に、2 つの xcframework の `.meta` の iOS の設定 `AddToEmbeddedBinaries: true` を足したもの
- 利用者のプロジェクトの C#（`Start`）が `ntk_version()` を呼んで出す
- Unity 6000.4.2f1。シミュレーターは iPhone 16 / iOS 18.3（Simulator SDK、arm64）。Simulator.app は開かず、`xcodebuild` と `simctl` だけで入れて起動した

| # | 確かめたこと | 結果 |
|---|---|---|
| S-1 | Unity は `Plugins/iOS` の xcframework を書き出したプロジェクトに写してリンクするか（I-1 (a)） | **する。** A で、口が動く前に、2 つとも `Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS/` にあり、UnityFramework のリンクに 1 件ずつ入っている。Embed は 0 件 |
| S-2 | `PluginImporter` の設定で Embed までできるか（I-1 (b)） | **できる。** B で、口が動く前に、アプリの Embed Frameworks に 2 つとも入っている（`ATTRIBUTES = (CodeSignOnCopy, RemoveHeadersOnCopy, )`。口が `PBXProject.AddFileToEmbedFrameworks` で足したものと同じ）。口は二重に足さなかった。シミュレーターで起動し、S-6 と同じ結果 |
| S-3 | 冪等（同じ所への Append の書き出しを 2 回） | A・B とも、2 回の後もリンク 2、Embed 2、`.mm` 1、`UserNotifications.framework` 1 のまま。A の差分は Unity がグループの並びを入れ替えた 2 行だけ |
| S-4 | 入れない設定（I-2）。Player Settings の定義 | `NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL` を Player Settings（iOS）に入れると、`.mm` も `UserNotifications.framework` も足されず、`.mm` のファイルも写されない。Embed は 2 つとも入る |
| S-5 | 入れない設定。Build Profile の上書き | Player Settings に無く Profile の Scripting Defines に入れると、効いた（`.mm` が外れる）。**Player Settings にあり Profile で空にしても、効いたまま**（`.mm` が外れる）。Profile の Scripting Defines は Player Settings の定義に足すもので、消せないと読める。その回の Player のアセンブリ（`NativeToolkit.Runtime`）のコンパイルにも定義が入っていて、Editor の `#if` とビルドの定義は一致した。Profile が Player Settings ごと上書きする形（このリポジトリの `iOS Profile` の形）は確かめていない |
| S-6 | シミュレーターで起動（A、Xcode 26.3 と 26.6 で組んだもの） | どちらも `ntk_version() = 0x020000`、`ntk_notification_install -> 0`（NONE） |
| S-7 | `UserNotifications.framework` は要るか | **要らない。** A のプロジェクトから外して 26.3 で組み、シミュレーターで S-6 と同じ結果。2.0.0 の 2 つの framework は自分で `UserNotifications` をリンクしている（`otool -L`）。1a で要ったのは、1a の `.mm` が `UNUserNotificationCenter` を直に使っていたから（F-4） |
| S-8 | 最小 iOS（I-16） | 2 つの framework は `minos 18.0`（`vtool`）。Player Settings の最小を 17.0 にしても Unity は止めずに書き出し、Xcode は「18.0 向けの dylib をリンクしている」の警告 2 行だけで組めた。iPhone 15 / iOS 17.4 のシミュレーターで起動すると、**約 1 秒で自分から終わる**（runningboard の `voluntary`、終了コード 0）。dyld のエラー、クラッシュの記録、Unity と C# のログはどれも無い。原因（2.0.0、Unity、Xcode 26 の SDK と 17.4 の組み合わせ）は切り分けていない |
| S-9 | 実機（iPhone XS）。B を Xcode 26.3 で、Team の既存のワイルドカード profile で自動の署名（`-allowProvisioningUpdates` なし） | アーカイブは成功。アプリの `Frameworks/` に `IosLibrary.framework` と `IosLibraryCApi.framework` が入り、アプリと 3 つの framework は同じ Team で署名され、`codesign --verify --deep --strict` が通る。iPhone XS（iOS 18.7.2）に `devicectl` で入れて起動し、`ntk_notification_install -> 0`（NONE）と `ntk_version() = 0x020000` が出た。その後アプリを消し、端末のアプリの一覧に無いことを確かめた（利用者の許可を得て。端末から取ったログは書き写した後に消した） |

段 1 で分かった、ほかのこと:

- git の URL で入れると、`Plugins/Windows/VERSION.txt has no meta file, but it's in an immutable folder. The asset will be ignored.` が出て、ビルドの報告でエラー 1 件に数えられる（ビルドは成功）。[windows-version-meta](../windows-version-meta/README.md) の「警告が出るかは要確認」の答え
- 手動の署名（`CODE_SIGN_STYLE=Manual` と `iOS Team Provisioning Profile: *`）は、1a と同じく「Xcode managed」の profile と組めずに失敗した

段 1 の結果から決めること（設計書 v4 の I-1 の 97 行目の条件どおり）:

- **I-1: Embed は `PluginImporter` の `AddToEmbeddedBinaries` で行う**（S-2）。口（`IosXcodeProjectSetup`）が足すのは `.mm` だけ。`UserNotifications.framework` は足さない（S-7）。口は、Embed とリンクが入っているかを確かめ、無ければ分かりやすい文言で止める。メニュー「Configure Xcode Project」は、Unity がリンクと Embed をするので要らなくなる（段 4 で消し、移行ガイドに書く）
- **I-2: そのまま**（S-4・S-5）。Build Profile の Scripting Defines は足すだけなので、Player Settings に入れた定義を Profile で外すことはできない、とマニュアルに書く
- **I-16: そのまま**（S-8）。18.0 未満では黙って終わるので、ビルドで止める確かめは要る

### 6.4 段 2 のスパイクの結果（2026-11-14）

[設計書 v4](designs/2026-11-14-ios-ui-automation-design-v4.md) の 7 章の V-1〜V-8 を、コミットしない写し（このリポジトリの作業木を写し、Library は APFS の複製）で確かめた。Unity 6000.4.2f1、Test Framework 1.6、Xcode 26.3、iPhone 16 / iOS 18.3 のシミュレーター。Simulator.app は開かず、終わった後にシミュレーターを止めた。ビルドの処理（`iOS Profile`）は `NATIVE_TOOLKIT_DIST_ROOT` で native-toolkit の dist を読ませた。実機は使っていない。

| # | 結果 | 分かったこと |
|---|---|---|
| V-1 | **通った** | 公式の「split build and run」の形（`-runTests -testPlatform iOS` に、アセンブリ単位の `[assembly: TestPlayerBuildModifier]` と `[assembly: PostBuildCleanup]`）で、`AutoRunPlayer`・`ConnectToHost`・`WaitForPlayerConnection` を外し、出力先を替えると、Xcode のプロジェクトに書き出すだけで終わる。Simulator.app も Xcode も開かない。`PlayerLauncherTestRunSettings`（internal）は使わない。cleanup の中で `EditorApplication.Exit` を呼ぶと、Test Framework がテストのシーン（`Assets/InitTestScene*.unity`）を消す前に終わり、ファイルが残る（3.4 の戻しが拾う） |
| V-2 | **通った（1 の方法）** | modifier の中で `PlayerSettings.iOS.sdkVersion` と `simulatorSdkArchitecture` を替えると、`iOS Profile`・`Test iOS Profile` のどちらが Player Settings を上書きしていても、書き出したプロジェクトは `SUPPORTED_PLATFORMS = iphonesimulator`、`ARCHS = arm64` になった。cleanup で戻した後、`ProjectSettings` と Build Profiles に差分は残らない |
| V-3 | **通った** | bundle id も modifier（とハーネスの書き出し）の中の `PlayerSettings.SetApplicationIdentifier` で替わる（`com.jonghyunkim.nativetoolkit.playertests`・`...uiharness`）。書き出した後に `PBXProject` で替える必要は無い。表示名は `PlistDocument` で替えた |
| V-4 | **起動の引数は届かない。環境変数は届く** | iOS の Player の `Environment.GetCommandLineArgs()` は空（`simctl launch` の引数も、XCUITest の `launchArguments` も）。`simctl` の `SIMCTL_CHILD_<名前>` と XCUITest の `launchEnvironment` で渡した環境変数は、`Environment.GetEnvironmentVariable` で読めた。**(A) の run と (B) のシナリオは環境変数（`NTK_RUN`・`NTK_SCENARIO`）で渡す** |
| V-5 | **通った** | Player で `TestRunCallback` が呼ばれ、`persistentDataPath`（`Documents/`）に書いた XML と `done` を `simctl get_app_container ... data` で読めた。書き出し中のテストのシーンの `RemoteTestResultSender` は 0（同じシーンの `PlayModeRunnerCallback` は 1 で、読めていることの対照）。Player は**テストの後も動き続ける**（レビュー v1 の R-2 のとおり）ので、ランナーが `simctl terminate` する。XML の根は `test-suite`（R-8） |
| V-6 | **通った** | `UnityGetGLViewController().view` に重ねた `UIScrollView` の `UIButton`（`row.<id>.run`）と `UILabel`（`result.line.<n>`）は XCUITest から identifier で見え、押すと C# の `[MonoPInvokeCallback]` に届く |
| V-7 | **シミュレーターで通った**（実機は段 2 の 4） | 手で書いた最小の Xcode のプロジェクト（空のホストと UI テストの 2 つのターゲット、共有のスキーム）が `build-for-testing` と `test-without-building -only-testing:` で組めて、先に `simctl install` したハーネスを bundle id で起動して押せた |
| V-8 | **通った** | 1.x のまま、`iOS Profile` で書き出したハーネスで、4 つのシナリオがすべて通った: Dialog（アラートの OK → `[confirm@ok] OK`）、Share（シートを見つけて閉じる → `completed=False`）、Notification（springboard の許可のダイアログで許可 → 5 秒後の通知を予約 → ホームに戻してバナーを押す → `[tap@event] ... DefaultActionIdentifier`）、Clipboard（名前付きの pasteboard の往復 → `items=1 match=True`） |

スパイクで分かった、ほかのこと:

- **今の iOS の PlayMode のテスト 2 つ（Clipboard 31 件、Share 5 件）は、iOS の Player のコンパイルで止まる**（Editor だけの口 `ResetForTests` など）。設計書 3.3 のとおり `#if UNITY_EDITOR` にすると組めた
- asmdef に `optionalUnityReferences: ["TestAssemblies"]` と、Test Runner のアセンブリの明示の参照を両方書くと「duplicate references」でコンパイルが止まる。既存の PlayMode の asmdef と同じく、明示の参照は書かない
- ハーネスの定義（`NTK_UI_HARNESS`）は `BuildPlayerOptions.extraScriptingDefines` で渡すと、定義の制約つきの asmdef が Player に入った（レビュー v2 の R2-4 の代わりの案が通った）。コントローラーは `[RuntimeInitializeOnLoadMethod]` で作り、シーンから参照しない
- `iOS Profile` のビルドの処理（`PreBuildProcessor`）は dist から xcframework を写すとき、実行の印を落とす（`100755` → `100644`。中身は同じ）。git の作業木が汚れるので、4.6 の照らし直しで止まる。写すときに mode を保つか、3.4 の戻しの範囲に `Plugins/iOS` を足す
- Unity は実行の中で `.vscode/settings.json` とプロジェクト名の `.slnx` も書き換える（3.4 の戻しの範囲に足す）
- `Test iOS Profile` の Player Settings の bundle id は `unity.ios.plugin.UnityIosPluginExample`。1.x の `Test iOS Profile` の書き出しには Embed が無い（レビュー v2 の N-9 のとおり）
- 時間（Xcode 26.3、2 つの組み立てを並べたとき）: Unity の書き出し 1〜2 分、Player の組み立て 4〜7 分、UI テスト 1 件 10〜25 秒

### 6.5 段 3-0（iOS の型を `Runtime/iOS/<Feature>/` に移す）の結果（2026-11-14）

設計書 v4 の I-10 のとおりに行った（利用者の指示「判断は推奨案でいい」の間に、設計で決まっていた作業として進めた）。

- iOS の 4 機能の 27 ファイルを `.meta` ごと（GUID を保って）`Runtime/iOS/{Clipboard,Dialog,Notification,Share}/` に移し、名前空間を `JonghyunKim.NativeToolkit.Runtime.iOS.<Feature>` にした。公開の型 51 個に `[MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.<Feature>")]` を付けた。型の名前と振る舞いは変えていない
- 接頭辞の無い 5 型（`NotificationContentPayload`、`NotificationAttachmentPayload`、`TimeIntervalTriggerPayload`、`CalendarTriggerPayload`、`LocationTriggerPayload`）は `IosNotificationPayloads.cs` から `Runtime/Notification/NotificationPayloads.cs` に分け、今の名前空間に残した（macOS も使う。段 4 で iOS 用の型を作る）。iOS の通知の型は、この 5 型を使うところで旧い名前空間も `using` する
- `using` を直したのは 26 ファイル（サンプルの iOS のコントローラーと状態、`Assets/Scripts` のサンプルのテスト、EditMode と PlayMode の iOS のテスト、段 2 の UI のハーネス）。macOS のファイルは 5 型だけを使うので、変えていない
- 確かめ（このリポジトリ、`iOS Profile`）: `scripts/verify_unity_ios.sh` で EditMode 1419 件中 1418 件成功（Windows だけの skip 1）、Editor の PlayMode 300 件成功、iOS の Player（IL2CPP まで組み、`#if UNITY_IOS && !UNITY_EDITOR` の中もコンパイルされる）4 件成功。`scripts/test_ios_ui.sh` で UI のテスト 4 件成功。どれも移す前と同じ
- 残り: マニュアル（`manual/`、`docs/`）と `Documentation~` のコード例の `using` は旧い名前空間のまま（段 4 のマニュアルで直す）。移行ガイドに「iOS の型の名前空間が `...Runtime.iOS.<Feature>` に変わった（`[MovedFrom]` つき）」を書く。git の URL で入れた形での確かめは I-17 のとおり 3-S と 4 で行う

### 6.6 段 2 の仕組みの実機での確かめ（2026-11-14 20:24〜20:46）

利用者の許可を得て（`--dry-run` の一覧と承認の ID を見せた）、HEAD `f58c695` で流した。iPhone XS（iOS 18.7.2）、ケーブル、Xcode 26.3。利用者が自動ロックを「なし」、おやすみモード、Handoff を切った状態にした（設定は agent が変えていない）。

| 回 | 承認 | 結果 |
|---|---|---|
| (A) `verify_unity_ios.sh --device` | `PlayerTests/IosPlayer` | **4 件とも成功**（`IosPlayer: 4 ran, 4 passed`） |
| (B) `test_ios_ui.sh --device`（通知のテストを除く 15 件） | UI のテスト 15 件 | **14 件成功、1 件失敗**（Dialog の ID-4。シミュレーターでは成功。下の「ローカルネットワーク」の見込み） |

- 実機で初めて確かめられたこと: 承認の仕組み（使い済みにした後、実行ごとの印で端末の手順の前ごとに照らし直す）、UI テストのランナーの自動の署名（`-allowProvisioningUpdates` なし）、`devicectl device process launch --environment-variables` で run の値が Unity の `Environment.GetEnvironmentVariable` に届くこと、`devicectl device copy from --domain-type appDataContainer` で結果を取り出せること（Release の自動の署名でも読めた）、ハーネス自身のアラートとキーボードの入力、共有のシートの「閉じる」
- 後片付け: 両方の回で入れたアプリ（4 つ）は消え、消えたことを読み直して確かめた。端末のログと結果のバンドルは一時のフォルダごと消した。端末の識別子と Team ID は出していない
- **ローカルネットワークの許可のダイアログ**: (B) で、ハーネスを入れ直すたびに iOS が「ローカルネットワーク」の許可を求めた（利用者が毎回「許可しない」を押した）。ハーネスを development ビルドで組んでいたため、Player が Unity の Editor を同じネットワークで探した。シミュレーターでは出ない。ID-4 の失敗は、このダイアログが初めて出た頃で、原因の見込み（実機の失敗の文言は決まりどおり消したので確定はしていない）。**ハーネスを development ビルドにしない形に直した**。(A) のテストの Player は Test Runner が development にするが、利用者からダイアログの話は無かった
- **通知の実機の確かめは外した**: おやすみモードではハーネスの通知のバナーも出ず、ハーネスはテストのたびに入れ直すので、集中モードの「許可するアプリ」にも入れておけない。段 3-R の Notification の表で、native-toolkit の形（ハーネスを 1 回だけ入れ、UI テストモードの集中モードで許可し、まだ聞いていない状態が要るテストはシミュレーターだけ）に合わせて決める
- **iOS 26 系**（利用者の判断、2026-11-14）: 移行の比べは実機 1 台とシミュレーター 1 台（iPhone 16 / iOS 18.3）で進め、**段 4（リリースの前）に iOS 26 のシミュレーターでも 1 回流す**。iPad のシミュレーターは、あれば足し、無ければマニュアルに「確かめていない」と書く
