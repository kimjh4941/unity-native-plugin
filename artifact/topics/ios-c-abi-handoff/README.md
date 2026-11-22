# native-toolkit の iOS C ABI の雛形を、本物の Unity で確かめる（段階 1a の申し送り）

- 記録日: 2026-10-31
- 分類: 移行の事前調査。iOS
- 発見経緯: native-toolkit の iOS C ABI（2.0.0）の設計書の第 1 部 10 章（U-1〜U-10）。native-toolkit では Unity のプロジェクトを組まない取り決めのため、Unity と実機での確かめをこのリポジトリが受け持つ。依頼は native-toolkit のセッション（native-toolkit-75）から、利用者の決定で 2 回に分けて届いた
- 対応方針: 雛形は捨てる前提の試作。このパッケージの本体を C ABI に移すのは段階 2c で、ここでは本体のコードに触れない。試すプロジェクトはどのリポジトリにも入れない（`/Users/jonghyunkim/Desktop/ntk-ios-cabi-probe`）。C# とビルドの手順の写しを `probe/` に置く
- 進捗: **1 回目・2 回目とも完了**（U-1〜U-10）。native-toolkit に要約を送った。試すアプリは利用者の承認を得て端末から消した（2026-10-31）。native-toolkit からの追加の依頼が無ければ、このトピックで残る作業は無い

## 依頼の文書（native-toolkit 側）

- 1 回目: `native-toolkit/artifact/topics/ios-c-abi/results/2026-10-31-ios-c-abi-unity-handoff-1.md`
- 2 回目: `native-toolkit/artifact/topics/ios-c-abi/results/2026-10-31-ios-c-abi-unity-handoff-2.md`
- 実機の決まり: `native-toolkit/agent-rules/device-testing.md`（実機は利用者の私物。段階ごとに 4 項目を示して許可を得る）

## 結果

| 回 | 結果 | 状態 |
|---|---|---|
| 1 回目 | [results/2026-10-31-ios-c-abi-handoff-1-results.md](results/2026-10-31-ios-c-abi-handoff-1-results.md) | 完了。native-toolkit に要約を送った |
| 2 回目 | [results/2026-10-31-ios-c-abi-handoff-2-results.md](results/2026-10-31-ios-c-abi-handoff-2-results.md) | 完了。native-toolkit に要約を送った |

## dist/1.14.0 の前に入れた 3 つ（2026-11-14）

native-toolkit-d9 の依頼（native-toolkit の `artifact/topics/ios-c-abi/README.md` 5 章の「2b の前に unity-native-plugin に頼むこと」）。利用者の判断で **iOS だけ**にし、macOS は外した。

- `Plugins/iOS/VERSION.txt` で、dist の版と xcframework の完全な名前を固定した（今は 1.13.0 の 1.3.0 の 2 つ）。読むのは `Editor/iOS/IosXcframeworkPin.cs`
- `PreBuildProcessor`・`PostBuildProcessor`・`IosFrameworkPatcher` は、固定した名前だけを完全一致で使う。無ければ別のものを選ばずに止まる。一覧が空でも止まる
- development ビルドは、`<名前>-debug.xcframework` があればそれを、無ければ release を使う（capi には `-debug` が無い）。メニューの `IosFrameworkPatcher` は、`Plugins/iOS` にある方（release、無ければ `-debug`）を使う
- **前からの食い違いを直した**: `PostBuildProcessor` は development かどうかを Build Settings のチェックボックス（`EditorUserBuildSettings.development`）で見ていて、`BuildOptions.Development` で組むと（スクリプト、Test Runner）`PreBuildProcessor` と判断が分かれた。dist に `-debug` が無かったので表に出ていなかった。今は `PreBuildProcessor.IsDevelopmentBuildInProgress` を読む。**macOS には同じ食い違いが残っている**（範囲の外）
- 確かめ: EditMode 1419 件中 1418 件成功（1 件は Windows だけのテストの skip）。プロジェクトの写しで、2.0.0 の名前の試しの dist（capi が隣にあり、capi に `-debug` が無い）を使って iOS を書き出し、release・development・メニューのどれでも正しい 2 つがリンクされ Embed & Sign の印が付くことを見た。3 か所の呼び出しは定義付きのアセンブリにあり EditMode のテストから呼べないので、この書き出しは手で確かめる項目
- 残したこと: `IosXcframeworkPin` のメソッドの頭の `Debug.Log`（`csharp.md` の規則。Android・Windows の読み方も持たない）、`VERSION.txt` を読む処理が `PreBuildProcessor`（Android・Windows）と `IosXcframeworkPin` の 2 つあること、`PreBuildProcessor` が xcframework を消すときに `.meta` を消さないこと（前から）

## 2c（本体の移行）に持ち越す知見

- IL2CPP のコールバックから例外が出ると、アプリは `std::terminate` で落ちる（U-9）。本番の包みのコールバックは、本体をすべて try / catch で囲む
- 完了は関数が戻った後に main で来るが、同じ `Time.frameCount` の間に来ることがある（U-1）。テストで「次のフレーム以降」を前提にしない
- 通知の `.mm` を UnityFramework に入れるなら、`UserNotifications.framework` をリンクに足す
- Unity の通知パッケージ（2.5.0）は前の delegate へ転送せず、`kUnityWillFinishLaunchingWithOptions` と C# の `AuthorizationRequest` のたびに delegate を上書きする。ライブラリが後に入れば両方が届くが、ホストが起動の後にパッケージの許可の API を呼ぶと、ライブラリの通知は失われる（U-4）
- 背景のアクションで背景起動したときは C# が初期化されず、イベントは次に前面に来て受け手を登録したときに届く（U-6）
- ASan で確かめるときは、xcframework とアプリを同じ Xcode で組む
- 今の `PreBuildProcessor` / `PostBuildProcessor` / `IosFrameworkPatcher` は `ios-native-toolkit-` の前方一致で探すので、`ios-native-toolkit-capi-*` にも当たる（native-toolkit の README の 5 章の依頼。2b の前に直す）
