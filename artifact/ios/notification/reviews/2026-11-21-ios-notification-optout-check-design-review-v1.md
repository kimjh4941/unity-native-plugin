# iOS Notification の opt-out の確かめの設計 v1 のレビュー v1

- 記録日: 2026-11-21
- 対象: [../designs/2026-11-21-ios-notification-optout-check-design-v1.md](../designs/2026-11-21-ios-notification-optout-check-design-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）を並行で
- 1 章と 2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

## 指摘一覧（設計 v1: artifact/ios/notification/designs/2026-11-21-ios-notification-optout-check-design-v1.md）

**R1-1**
- 区分: A2
- 観点: 実現性
- 場所: 2.3（Probe アセンブリに置く）、3 章（変えるファイル）
- 指摘: テストが使う `IosNotificationManager.Instance` は、Probe アセンブリから見えません。Probe の asmdef は `"references": []` です。`NativeToolkit.Runtime` は `autoReferenced: true` ですが、これが効くのは Assembly-CSharp だけで、asmdef からは明示の参照が要ります。Editor ではテストが `#if` で消えるのでコンパイルが通ります。そのため、エラーは skip-install の回の Player のスクリプトのビルドで初めて出ます。
- 根拠: Assets/NativeToolkitIosPlayerTests/Probe/NativeToolkit.IosPlayerTests.Probe.asmdef。Packages/com.jonghyunkim.nativetoolkit/Runtime/NativeToolkit.Runtime.asmdef。参照を明示している前例は Assets/NativeToolkitIosUiHarness/Runtime/*.asmdef の `"references": ["NativeToolkit.Runtime"]`。
- 提案: 3 章に「Probe asmdef に `NativeToolkit.Runtime` の参照を足す」を加えます。Probe は全プラットフォームで組まれるので、Android・Windows・macOS のゲートでも組まれることを書き添えます。

**R1-2**
- 区分: A2
- 観点: 正しさ／実現性
- 場所: 2.3（C ABI を直接呼ぶ関数名）
- 指摘: `ntk_notification_remove_interaction_listener` という関数はありません。受け手を外すのは `ntk_notification_listener_remove(ntk_notification_listener*)` で、戻り値は void です。`__Internal` の DllImport は Xcode のリンクで未定義シンボルになります。
- 根拠: Notification.h:300-314。native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Notification/NotificationEntry.swift:242。
- 提案: 名前を `ntk_notification_listener_remove` に直し、戻り値は見ません。

**R1-3**
- 区分: A2
- 観点: 正しさ
- 場所: 1 章 OO-2・OO-3、2.3
- 指摘: ライブラリは、callback か out が NULL かを、delegate を入れたかより先に確かめます。NULL なら `INVALID_PARAMETER` を返し、`NOT_INITIALIZED` にはなりません。設計は渡す callback を決めていません。簡便に `IntPtr.Zero` を渡すと、OO-2 は「NOT_INITIALIZED ではない」で落ちます。IL2CPP では、callback は `[MonoPInvokeCallback]` を付けた static メソッドの関数ポインターにする必要があり、その delegate を生かしておく必要もあります。
- 根拠: native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Notification/NotificationReceiver.swift:487-501（`guard let callback, let outListener` の後に `isDelegateInstalled`）。Notification.h:33（"A NULL callback is rejected"）。
- 提案: 2.3 に「非 NULL の static callback（MonoPInvokeCallback、delegate は static field で保持）、release は NULL」と書きます。

**R1-4**
- 区分: B
- 観点: 正しさ（検証の穴）
- 場所: 2.2（OO-1 の確かめ方）
- 指摘: 「installer のファイルが無く、pbxproj が指さない」だけでは、「define が Editor のフックに届いた」と「`IosXcodeProjectSetup` 自体が走らなかった」を区別できません。フックが走らなくても、XCFramework は .meta で link・embed されるのでビルドは通ります。OO-2 の「proxy ではない」も両方で成り立ちます。さらに、`export_dir` は毎回 `rm -rf` されるので、ファイルが無いことは自明です。
- 根拠: IosXcodeProjectSetup.cs:63-67、93-94（`installer entries: N (skip: True|False)` のログ）。verify_unity_ios.sh:441（rm -rf）。
- 提案: OO-1 の合格条件に、書き出しのログの `[NativeToolkit][iOS][Setup] ... installer entries: 0 (skip: True)` を加えます。既定の回の逆の確かめにも `(skip: False)` と `entries: 1` を加えます。

**R1-5**
- 区分: B（要確認）
- 観点: 正しさ
- 場所: 2.1（「Test iOS Profile の写しで、違いは m_ScriptingDefines と名前・GUID だけ」）
- 指摘: 6000.4 の `iOS Profile.asset` には `m_HasScriptingDefines: 0` が書かれていますが、`Test iOS Profile.asset` にはこのフィールドがありません。6000.4 の UnityEditor.CoreModule.dll には `m_HasScriptingDefines`、`get/set_hasScriptingDefines`、`hasScriptingDefinesBeenModified` があります。手で写して `m_ScriptingDefines` だけを埋めると、このフラグが偽のままで define が使われない可能性があります。その場合 OO-1 は落ち、6 章の判断で「マニュアルが誤り」と誤って結論づけることになります。フラグの意味は逆コンパイルしておらず、未確認です。
- 根拠: Assets/Settings/Build Profiles/iOS Profile.asset:26。Test iOS Profile.asset:25。Unity 6000.4.2f1 の Resources/Scripting/Managed/UnityEngine/UnityEditor.CoreModule.dll の文字列。
- 提案: プロファイルは Build Profiles ウィンドウで複製し、Scripting Defines を UI で足して、Unity が書いた YAML をコミットします。6 章に「OO-1 が落ちたら、マニュアルを直す前にプロファイルの直列化（フラグ）を確かめる」を加えます。

**R1-6**
- 区分: B
- 観点: 正しさ
- 場所: 1 章 OO-3（「受け手が登録される」）
- 指摘: OO-3 で見ているのは、テスト自身の `add_interaction_listener` が NONE を返すことだけです。これはライブラリが受け付ける状態になったことの証明で、Manager の受け手（`s_interactionListener`）が `Initialize()` で登録されたことの証明ではありません。Manager が登録に失敗しても `LogWarning` が出るだけで、テストは落ちません（テストを落とすのはエラーと例外だけです）。
- 根拠: IosNotificationManager.cs:165-180、683-692。
- 提案: `Initialize()` の間だけ `Application.logMessageReceived` を拾い、「the interaction listener was not registered」の警告が出ないことを OO-3 の合格条件に足します。Awake の警告は LogAssert.Expect で受けているので、それと対になります。

**R1-7**
- 区分: B
- 観点: 実現性／既定のゲートへの影響
- 場所: 2.2（`--skip-install` の選択肢）
- 指摘: 他の選択肢との組み合わせが決まっていません。
  - `--as-package-user` も `PROFILE` を上書きするので、引数の順で結果のプロファイルが変わります。
  - `--category` と併用すると、`RUN_CATEGORY` の取り合いになります。
  - 「Notification のサンプルの回は流さない」は、631 行の条件（`PACKAGE_USER -eq 0 && -z EXPLORE_CATEGORY`）に新しいフラグを足さないと守られません。
- 根拠: verify_unity_ios.sh:114-120、130-137、629-639。
- 提案: `--skip-install` と `--as-package-user`／`--category`／`--device` の併用を断ることを明記します。サンプルの回とロックの条件に新しいフラグを足すことも書きます。

**R1-8**
- 区分: B
- 観点: 既定のゲートへの影響
- 場所: 2.2（既定の回の逆の確かめ）、5 章
- 指摘:
  - 逆の確かめをどの回で行うかが曖昧です。`run_player_tests` に置くと、`--as-package-user`、`--category`、device の回、Notification のサンプルの回にも効きます。設計は「選択肢なし」とだけ書いています。
  - 新しいプロファイルと script の変更は device 承認が守る木（`Assets/Settings/Build Profiles`、`Assets/NativeToolkitIosPlayerTests`、`scripts/verify_unity_ios.sh`）に入ります。コミット前は device の回の approve が通らず、既存の承認は使えなくなります。
  - 5 章の DoD に device の回と `--as-package-user` の回がありません。
- 根拠: verify_unity_ios.sh:436-545（全回が通る関数）。scripts/ios_ui_runner.py:159-175、271-279。
- 提案: 逆の確かめは skip-install 以外のすべての回（package-user と device を含む）で行うと決めます。5 章に `--as-package-user` の回を足し、device の回はコミットの後に取り直すと書きます。

**R1-9**
- 区分: B
- 観点: 抜け
- 場所: 1 章（マニュアルの主張 1）、6 章
- 指摘: マニュアルの該当段落の主張のうち、次の 2 つが範囲にも「確かめないこと」にも書かれていません。
  - (a) 「Player Settings の Scripting Define Symbols に足す」経路と、Note の「プロファイルの define は Player Settings に加わる」。この repo のプロファイルは Player Settings を上書きしていて、`scriptingDefineSymbols: iPhone:` を空にして共有の define を置き換えています。そのため、上書きのあるプロファイルが有効なときに Player Settings 側へ足しても効かない可能性があります。記録には「Test * が有効だと Editor でも BuildProcessors が外れる」とあります。
  - (b) 「earlier build が足した Xcode プロジェクトからは外す」（Remove の経路）。書き出し先は毎回消されるので、この経路は通りません。
- 根拠: manual/1.14.0/notification.md の Notification delegate 節と Note。Test iOS Profile.asset:851-852。iOS Profile.asset:863-864。commit 40b7f7e。IosXcodeProjectSetup.cs:78-81。
- 提案: (a) と (b) を「確かめないこと」に理由付きで明記するか、マニュアルの要確認として残します。(a) については、上書きのあるプロファイルでは Player Settings 側の define が置き換えられうることを 6 章の判断材料に加えます。

**R1-10**
- 区分: C
- 観点: 正しさ（根拠の書き方）
- 場所: 2.2 末段（「Editor のアセンブリも組み直すはず」）
- 指摘: Unity 6000.4 の文書に根拠があるので、「はず」は文書の引用に置き換えられます。文書には次のとおりあります。
  - build profile の symbols は "apply for all Editor and Player code when the given build profile is active"
  - `-activeBuildProfile` は "the specified build profile and its custom scripting symbols applicable from startup"
  - プロファイルの Scripting Defines は "additive"
  - "When the Editor runs in batch mode, there's no mechanism to trigger recompilation"

  OO-1 は文書どおり動くかの実測として残します。
- 根拠: https://docs.unity3d.com/6000.4/Documentation/Manual/custom-scripting-symbols.html 、https://docs.unity3d.com/6000.4/Documentation/Manual/build-profiles-reference.html
- 提案: 2.2 に URL と引用を書きます。

**R1-11**
- 区分: C
- 観点: 実現性（表現）
- 場所: 2.2（「手伝い役（UI ドライバー）は要らない」）
- 指摘: `--category` の回でも、ドライバーはビルド・起動され、`finish_driver` の判定を受けます。外れるのは `--require IosUiDriver` だけです。「要らない」と書くと、起動も省くと読めます。
- 根拠: verify_unity_ios.sh:496-497、520、530-531、540-541。
- 提案: 「手伝い役は起動するが、そのテストの実行は要求しない（`--category` と同じ）」と書き換えます。

## 区分ごとの件数
- A1: 0
- A2: 3（R1-1、R1-2、R1-3）
- B: 6（R1-4 〜 R1-9）
- C: 2（R1-10、R1-11）

## 2. Codex

1. [P1] テストアセンブリから Manager を参照できません。
   設計 2.3節（design-v1.md:57）の追加先 Assets/NativeToolkitIosPlayerTests/Probe/NativeToolkit.IosPlayerTests.Probe.asmdef は references が空です（asmdef:3）。IosNotificationManager を使う新規テストはコンパイルできません。Unity の asmdef 参照仕様でも明示参照が必要です（https://docs.unity3d.com/6000.4/Documentation/Manual/assembly-definitions-referencing.html）。
   修正: Probe の asmdef に NativeToolkit.Runtime への参照を追加し、変更一覧に含める。

2. [P1] リスナー解除の C ABI 関数名が誤っています。
   設計 design-v1.md:62 の ntk_notification_remove_interaction_listener は存在しません。実際の宣言は Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-capi-2.0.0.xcframework/ios-arm64_x86_64-simulator/IosLibraryCApi.framework/Headers/NativeToolkitC/Notification.h:314 の void ntk_notification_listener_remove(ntk_notification_listener*) です。記載どおりに呼ぶと未定義シンボルになります。
   修正: ntk_notification_listener_remove を戻り値 void、引数は登録時に得たハンドルで宣言・呼び出す。

3. [P2] OO-3 は Manager 自身のリスナー登録成功を証明しません。
   設計 design-v1.md:26 は OO-3 で Manager の登録を確認済みとしていますが、確認するのはテスト自身の登録です。ネイティブ側は delegate のインストール状態を確認して、その呼び出しのリスナーを追加するだけです（/Users/jonghyunkim/Desktop/native-toolkit/ios/IosLibraryCApi/IosLibraryCApi/Notification/NotificationReceiver.swift:493）。そのため Initialize() の RegisterListeners() 呼び出し（Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Notification/IosNotificationManager.cs:179）が欠落しても OO-3・OO-4 は合格します。
   修正: Manager の登録ハンドル（IosNotificationManager.cs:686）が、Initialize 前はゼロ、初回後は非ゼロ、2回目後も同一になる検証を追加する。

補足（Codex の呼び出し役の注記）: Codex の出力は上記 3 件のみ。プロファイルの Scripting Defines が Editor アセンブリと player スクリプトに届くか、verify_unity_ios.sh の追加オプションが ios_ui_runner.py に合うか・既定ゲートや実機承認を壊さないか、の 2 点は調べたが欠陥として挙げていない。ios_ui_runner.py を読むコマンドは 1 回失敗しているので、この 2 点は「問題なしと確認済み」とは言えない。

## 3. 判断

- A1 は 2 人とも 0（Codex の 3 件は、1・2 がサブエージェントの R1-1・R1-2 と同じ A2、3 が R1-6 と同じ B）。決まり（A1 が 0 で止める）により、レビューはこの 1 回で止める
- 設計に直し込む（中心の契約の B）:
  - R1-4: OO-1 の合格条件に、書き出しのログの `installer entries: 0 (skip: True)` を加える。既定の回の逆の確かめは `(skip: False)` と `entries: 1`
  - R1-5: プロファイルは手で YAML を写さず、Unity の API（バッチモードで `BuildProfile` を複製し、`scriptingDefines` を設定して保存）で作る。OO-1 が落ちたら、マニュアルを直す前にプロファイルの直列化を確かめる
  - R1-6 と Codex 3: OO-3 で、`Initialize()` の間に Manager の「the interaction listener was not registered」の警告が出ないことを見る（`Application.logMessageReceived`）。Manager の internal のハンドルは、テストのアセンブリ（Assets）から見えないので使わない
- 設計に残件として書く（網羅系の B）: R1-7（選択肢の併用は断る）、R1-8（逆の確かめの範囲、承認の取り直し、`--as-package-user` の回）、R1-9（Player Settings の経路と、Remove の経路は確かめない）
- 実装のチェックリストへ（A2）: R1-1（Probe の asmdef に `NativeToolkit.Runtime`）、R1-2（`ntk_notification_listener_remove`、void）、R1-3（非 NULL の static callback、`MonoPInvokeCallback`）
- C は直さない（R1-10 の文書の引用は、v2 で根拠として 1 行だけ足す。R1-11 は実装で書き分ける）
