# iOS の C ABI 移行の段 3-S（切り替え）の設計 v2 のレビュー v2

- 記録日: 2026-11-15
- 対象: [../designs/2026-11-15-ios-c-abi-3s-switch-design-v2.md](../designs/2026-11-15-ios-c-abi-3s-switch-design-v2.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。2 回目なので、v2 で変えた所と 1 回目の指摘の直りだけを見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く
- 1 章は Unity の Test Framework のソース（`Library/PackageCache/com.unity.test-framework@76560ee600cb/` の `TestCommandBuilder.cs`・`PlayerLauncher.cs`・`PlayerLauncherBuildOptions.cs`）を引いている。実在を確かめた。`scripts/check_design_consistency.py` はそこを見ないので「無い」と数える

## 1. サブエージェント

設計書 v2 の 2 回目のレビュー（差分だけ）です。ファイルは編集していません。行番号は v2 の行です。

### R-1 [A2] 観点: C# の確かめのテストを「テストごとの定義」で残す（R-12 の直し）が、Clipboard では fixture の SetUp と TearDown で落ちる
- **根拠**
  - v2 の 66 行は、定義をテストごとにかけ、CP-20 などの C# の確かめのテストは 3-S の後も流すとしている。
  - しかし `IosClipboardPlayerTests` の `[UnitySetUp]`（`IosClipboardPlayerTests.cs:110-120`）は、ネイティブに届く `CreatePasteboard` を呼び、`created.V.IsSuccess` を Assert する。`[UnityTearDown]`（123-146）も `RemovePasteboard` を呼び、成功か `CLIPBOARD_UNAVAILABLE` のときだけ通す。
  - Test Framework 1.6 は、`[Test]` にも `EnumerableSetUpTearDownCommand`（UnitySetUp と UnityTearDown）をかける（`Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEngine.TestRunner/NUnitExtensions/Runner/TestCommandBuilder.cs:107`）。plain の `[Test]` である CP-20（452 行）でも SetUp が走る。
- **壊れ方**
  - 3-S の後、`CreatePasteboard` は `CLIPBOARD_BRIDGE_UNAVAILABLE` で返る。そのため CP-20 は SetUp の Assert で落ちる。
  - 同じ fixture に Clipboard の `UnavailableUntilMoved` を足すと、それも落ちる。
  - テストの頭にだけ `#if` をかけても、SetUp と TearDown は外れない。V-3 で気づける。
- **直し案**
  - 1.7 に置き場所を書く。CP-20 と Clipboard の `UnavailableUntilMoved` は、SetUp を持たない別の fixture（`[Category(IosPlayer)]`）に移す。
  - または、今の fixture をクラスごと `#if !NTK_IOS_CABI_SWITCHED || NTK_IOS_CABI_2_CLIPBOARD` で囲む。
  - あわせて、Share の SH-22（空の payload で、C# の中で終わる）も「残す」の一覧に名前で挙げる。

### R-2 [B] 観点: `notMovedYet` が既定の一覧にしか効かず、keep-installed の回と `--tests` の扱いが決まっていない
- **根拠**
  - v2 の 69 行は「`ios_ui_runner.py tests` は既定でそれを出さない」とだけ書いている。
  - `ios_ui_runner.py:349-352` の `tests --keep-installed` は `keepInstalled` をそのまま出す。この一覧はすべて `NotificationUITests` で、承認（`approve --keep-installed`）も `keepInstalled` と完全に一致することを求める（386-388）。
  - `test_ios_ui.sh:90-111` は、`--tests` の名前を `tests` の出力と照らす。そこに無い名前は「Not a UI test」で止める。
- **壊れ方**
  - 3-S の後も `test_ios_ui.sh --device --keep-installed` は、移していない Notification の UI テストを実機で流す。実機の回は、シミュレーターで全件成功してから流す決まりだが、その抜け道になる。
  - 4 つとも `notMovedYet` に入るので、既定の実行は「No UI tests to run.」で終わる（exit 2）。
  - `--tests` で名前を指定しても「Not a UI test」という誤った文言で拒まれ、移していない機能の UI テストを意図して流す道も無い。
- **直し案**
  - `notMovedYet` が効く範囲を書く。
    - 既定と `--device` の一覧から外す。
    - `--keep-installed` は、`keepInstalled` と `notMovedYet` が重なれば、理由の文言で止める。
    - `--tests` で名前を指定したときは「not moved yet」の文言で拒む（シミュレーターでだけ許すかも決める）。
  - `check_requirements` で `notMovedYet` のクラス名が実在するかを確かめ、doctest を足す。

### R-3 [B] 観点: 3-S で入れるハーネスの変更が、どの V でも組まれない
- **根拠**
  - v2 の 65 行と 69 行は、ハーネスに `NTK_IOS_CABI_SWITCHED` を足し、Dialog・Share・Clipboard のシナリオに `[abi@event]` の行を足すとしている。
  - ハーネスのアセンブリは `NTK_UI_HARNESS` の定義の制約付き（`NativeToolkit.IosUiHarness.asmdef:8`）なので、Editor のコンパイルには入らない。
  - R-2 のとおり、3-S の後は `test_ios_ui.sh` で流せる UI テストが無く、V-1〜V-9 にハーネスの書き出しも無い。
- **壊れ方**: ハーネスの C# の誤りや、2.0.0 の framework でのリンクの誤りが、段 3 の Dialog まで見つからない。
- **直し案**: V にハーネスの書き出しだけの確かめを足す（`IosUiHarnessBuild.Export` とシミュレーター向けの xcodebuild）。または、ハーネスへの変更を Dialog の段に回す。

### R-4 [B] 観点: `.mm` の確かめのテスト（R-7 の直し）の置き場所と条件が無く、確かめの native が無いビルドでリンクが止まりうる
- **根拠**
  - v2 の 68 行は、確かめの native を `IosPlayerBuildSetup` が足すとしている。しかし、それを `DllImport("__Internal")` で呼ぶテストを、どの asmdef にどの `#if` で置くかが無い。
  - `IosPlayerBuildSetup.ModifyOptions` が動くのは `-ntkIosPlayerSdk` があるときだけ（55-57 行）。`ModifyOptions` は書き出しの前に走るので、出力のプロジェクトに足す時点（`IPostprocessBuildWithReport` か `Cleanup`）も書かれていない。
  - IL2CPP は `__Internal` を静的リンクの記号として参照する。
- **壊れ方**
  - ほかの iOS の Player テストに倣って、パッケージの `Tests/PlayMode` に `#if UNITY_IOS && !UNITY_EDITOR` だけで置いたとする。その場合、Test Runner の窓からの iOS の Player のテストや、`testables` に入れた利用者のビルドは、Undefined symbol で Xcode のリンクが止まる。
  - V はすべて `-ntkIosPlayerSdk` 付きで流すので、気づけない。
- **直し案**
  - 呼ぶ側のテストは `Assets/NativeToolkitIosPlayerTests/Runtime`（iOS だけの TestAssemblies の asmdef）に置く。または `#if NTK_IOS_CABI_SWITCHED` で囲む。
  - 確かめの native を足すのは、その定義を足すのと同じ条件（`s_Active`）にし、足す時点を書く。

### R-5 [B] 観点: 「宣言は Editor でも組む」の決まり（1.4 の 43 行）が `!UNITY_EDITOR` の外というだけで、今の Dialog と Notification のファイルの形では照合が Android・Windows で崩れる
- **根拠**
  - `IosDialogManager.cs:3` と `IosNotificationManager.cs:3` は、ファイルごと `#if UNITY_IOS` で囲まれている。Clipboard と Share は `UNITY_IOS || UNITY_EDITOR`。
  - `IosCApiDeclarationTests` は `NativeToolkit.Runtime.Tests`（Editor の EditMode）にあり、`verify_unity_android.sh` と `verify_unity_windows.sh` の EditMode でも組まれる（レビュー v1 の R-3）。そこでは `UNITY_IOS` が定義されない。
- **壊れ方**: 段 3 で `ntk_dialog_*` を `IosDialogManager.cs` に宣言して期待の集合に足すと、次のようになる。
  - iOS の verify では通る。
  - Android と Windows の EditMode では、宣言が組まれないので集合が合わず落ちる。
  - これを避けようとして期待をプラットフォームで分けると、照合が弱まる。
- **直し案**
  - 決まりを「宣言は `#if UNITY_IOS || UNITY_EDITOR` のファイル（`IosNativeToolkitCApi` か、機能ごとの CApi のクラス）に置き、`!UNITY_EDITOR` の外に置く」と書く。
  - V-1 に、iOS 以外の EditMode（`verify_unity_android.sh` か Windows）でも `IosCApiDeclarationTests` が流れて通ることを足す。

### R-6 [C] 観点: 照合で集める名前空間が、完全一致か前方一致かが読めない
- **根拠**
  - v2 の 42 行は「名前空間 `JonghyunKim.NativeToolkit.Runtime.iOS` の型」としている。
  - しかし `IosNativeToolkitCApi` は `Runtime/iOS/Common/` に置く。Android の同じ役のクラスは `Runtime.Android.Common` にある（`AndroidNativeToolkitCApi.cs:4`）。
- **直し案**: 「`NativeToolkit.Runtime` の中で、名前空間が `JonghyunKim.NativeToolkit.Runtime.iOS` か、`…Runtime.iOS.` で始まる型」と書く（完全一致だと 0 件になり、そこで落ちる）。

### R-7 [C] 観点: 最小 iOS を `System.Version` で比べる（R-9 の直し）とき、`"18"` のような値で例外になる
- **根拠**: `System.Version.Parse("18")` は、マイナー版が無いので例外を投げる。Player Settings の欄には手で `18` と入れられる。
- **直し案**: 読み込めない値や、メジャー版だけの値の扱い（`.0` を補うか、文言を出して止めるか）を書き、EditMode のテストに `"18"`、`""`、`"17.6.1"` を足す。

### R-8 [C] 観点: `.mm` の「1a と同じ形」（R-2 の直し）は、1a の雛形をそのまま写すとも読める
- **根拠**
  - 1a の雛形（`NtkHandoff2Installer.mm`）は、`UserNotifications` を import して `UNUserNotificationCenter` を直に使う（delegate のログ）。`kUnityWillFinishLaunchingWithOptions` と `didBecomeActive` の経路も持つ。
  - 親の I-2 は「ログの仕掛けと `kUnityWillFinishLaunching` の経路を外したもの」としている。README の S-7 で `UserNotifications` が要らないと言えるのは、`.mm` がそれを直に使わないときだけ（F-4）。
- **直し案**: 「1a から delegate のログ・`kUnityWillFinishLaunchingWithOptions`・`didBecomeActive` を外し、`UserNotifications` を import しない」と書く。

### 確かめて問題が無かった点（指摘なし）
- **extraScriptingDefines が Player のテストのアセンブリに効くか**: 効く。
  - Test Framework 1.6 の `PlayerLauncher` は、`buildProfile` が null の `BuildPlayerWithProfileOptions` を作る。そのため `ShouldBuildWithProfile()` は false になり、modifier を通した `BuildPlayerOptions` で `BuildPipeline.BuildPlayer` を呼ぶ（`PlayerLauncher.cs:124-141, 343-358`、`PlayerLauncherBuildOptions.cs:121-124`）。
  - `-activeBuildProfile` 付きで同じ形の `NTK_UI_HARNESS` が効いたという前例もある（README 191 行、`test_ios_ui.sh:270`）。
- **Append の冪等**: S-3 で確かめ済みの形と合っている。外す処理（参照とファイル）も、V-6 で確かめる形になっている。
- **「使えない」の表の回数・順・時点**: 今のコードと合っている。
  - Clipboard は `TryStartOperation` の 3（引数）→ 4（プラットフォーム）→ `DispatchRejectedResult` の順で、共通のイベントが先、次の `Update`。
  - Share は `FireResult` で、イベントが先、次の `Update`。
  - Dialog は C# の確かめと同じく、呼び出しの中で出す。
  - Notification は 1.x の `cb` → `NotificationOperationCompleted` の順（`IosNotificationManager.cs:484-485`）で、イベントがあるのは `IosNotificationResult` の 6 つだけ。
- **宣言を Editor でも組む形**: `#if UNITY_IOS || UNITY_EDITOR` で宣言を外に置き、呼ぶのを iOS の Player だけにする形は、IL2CPP の `__Internal` でも Editor でも問題ない。
- **ヘッダーの道**: `NTK_CALL ntk_version(` は `Common.h:40` にある。framework の中に symlink は無い。
- **最小 iOS**: `Test iOS Profile` も最小 iOS は 18.0 なので、V-3 の `--as-package-user` が最小 iOS の確かめで止まることはない。

### 件数
- A1: 0
- A2: 1（R-1）
- B: 4（R-2、R-3、R-4、R-5）
- C: 3（R-6、R-7、R-8）
## 2. Codex

- **A2／R2-1：CP20 を残すための fixture 対応が不足しています。**
  設計 v2:66 はテスト単位で guard し、CP20 を残します。しかし既存の Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardPlayerTests.cs:118 の SetUp は `CreatePasteboard` の成功を要求し、同:139 の TearDown も削除成功を要求します。未移行期間は unavailable になるため、メソッドだけの guard では失敗します。V-3 で検出されるので A2 です。
  修正：未移行期間は fixture のネイティブ準備・終了処理も分岐するか、CP20 と unavailable テストを別 fixture に分けてください。

- **B／R2-2：`notMovedYet` による対象 0 件の処理・検証が不足しています。**
  設計 v2:69 の全 4 クラス除外後は UI 一覧が空になります。しかし scripts/test_ios_ui.sh:115 は空文字を配列の 1 要素にし、直後の 0 件チェックを通過します。その結果、同:352 に空のテスト名が渡ります。V-1〜V-9 には、この状態の検証がありません。
  修正：空行を配列に追加せず、対象 0 件ならビルド前に明示的に終了させ、その条件を検証項目に追加してください。

Codex の補足：機械照合は成功しました。ファイル変更、Unity／Xcode の実行は行っていません。他の確認項目（extraScriptingDefines、.mm Append、Editor 側宣言、unavailable 表）では指摘は出ていません。

件数：A1＝0、A2＝1、B＝1、C＝0。
## 3. 判断

**2 人とも A1 が 0 になったので、レビューを止める。** 止めた後の直しとして、次を v3 に入れる（v3 はレビューに出さない）。

- サブエージェントの R-1、Codex の R2-1: Clipboard の今の fixture はクラスごと `#if !NTK_IOS_CABI_SWITCHED || NTK_IOS_CABI_2_CLIPBOARD` で囲み、CP-20 と Clipboard の `UnavailableUntilMoved` は SetUp を持たない別の fixture に置く。残す C# の確かめのテストに Share の SH-22 も名前で挙げる
- R-2、Codex の R2-2: `notMovedYet` の効く範囲（既定・`--device` の一覧から外す、`--keep-installed` は重なれば理由の文言で止める、`--tests` で名指しすれば「not moved yet」で拒む、`check_requirements` で実在を確かめる）。一覧が空なら、ビルドの前に理由の文言で止める（空の名前を配列に入れない）
- R-3: V にハーネスの書き出しだけの確かめ（`IosUiHarnessBuild.Export` とシミュレーター向けの xcodebuild）を足す
- R-4: `.mm` の確かめのテストは `Assets/NativeToolkitIosPlayerTests/Runtime`（iOS だけの asmdef）に置き、確かめの native は定義を足すのと同じ条件（`-ntkIosPlayerSdk` のとき）で、書き出した後（`IPostprocessBuildWithReport`）に足す
- R-5: 宣言は `#if UNITY_IOS || UNITY_EDITOR` のファイル（`IosNativeToolkitCApi` か、機能ごとの CApi のクラス）の `!UNITY_EDITOR` の外に置く。V-1 に iOS 以外の EditMode でも照合が通ることを足す
- R-6: 集める名前空間は `JonghyunKim.NativeToolkit.Runtime.iOS` か、それで始まるもの
- R-7: 最小 iOS の値の読み方（メジャーだけなら `.0` を補う、読めなければ文言で止める）と、そのテストの値
- R-8: `.mm` は 1a から delegate のログ・`kUnityWillFinishLaunchingWithOptions`・`didBecomeActive` を外し、`UserNotifications` を import しない
