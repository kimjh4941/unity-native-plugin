# Android の下の層が C ABI 2.0.0（`libntk.so`）に置き換わり、Bridge を P/Invoke に書き直す

- 記録日: 2026-10-11
- 分類: 横断課題（移行）。**機能追加ではない。** clipboard / dialog / notification / share の 4 機能と、Android のビルド処理にまたがる
- 発見経緯: native-toolkit 側（`feature/NTKIT-17`、`49b99bc4`）のセッション native-toolkit-1d から、Android 2.0.0 で Unity 向けのブリッジ（`unity-android-native-toolkit-*.aar`）を廃し、汎用の C ABI（`android-native-toolkit-capi-2.0.0.aar`、`libntk.so`）にした旨の申し送りを受けた（段階 2c）
- 対応方針: 各 Manager の公開 API を保ち、下の層（`AndroidJavaObject` / `AndroidJavaProxy` と JSON）だけを P/Invoke に置き換える。進め方は [windows-c-abi-2](../windows-c-abi-2/README.md) に倣う。**ただし Windows の P/Invoke の書き方（`user_data` を NULL、32 ビットの要求の ID、借りるハンドル）は写さない**（申し送り 4 章。下の 3 章）
- 進捗: **進行中。** 基準の記録とスパイク（ビルド処理、共通部、実機での初期化の確認）から始める
- **2c の成功が、native-toolkit 1.13.0（Android 2.0.0）のリリースの条件**。native-toolkit は 2c が終わるまで 1.13.0 を出さない
- 対象: `Runtime/{Clipboard,Dialog,Notification,Share}/Android*.cs`、`Editor/Build/`（`PreBuildProcessor`、`PostBuildProcessor`、`Tools/Android/AndroidGradlePatcher`）、`Plugins/Android/`、対応するテストとサンプル
- ブランチ: `feature/UNT-13`
- 相手側: native-toolkit `artifact/topics/android-c-abi/`。問い合わせと結果の報告はセッション native-toolkit-1d へ

---

## 1. 何が変わるか

| | 1.x（今） | 2.0.0 |
|---|---|---|
| 呼ぶもの | `android.unity.*` の Kotlin（`AndroidJavaClass` / `AndroidJavaObject`）。69 メソッド | `libntk.so` の `ntk_*`（155 関数。`DllImport("ntk")`） |
| 結果 | `AndroidJavaProxy` のリスナー、JSON の文字列 | C の関数ポインタ（`[MonoPInvokeCallback]`）、構造体、**所有権ごと渡すハンドル** |
| リスナー | `set*Listener` / `clear*Listener`（差し替え） | 足す・外す（`_add_*_listener` / `_listener_remove`）。差し替えは C# が保つ |
| AAR | `android-native-toolkit-1.3.0` と `unity-android-native-toolkit-1.3.0` | `android-native-toolkit-2.0.0` と `android-native-toolkit-capi-2.0.0`（両方要る） |
| Kotlin | `PostBuildProcessor` が KGP 2.0.21 と stdlib 2.0.21 を当てる | KGP は要らない。実行時の依存 7 つ（stdlib 2.2.21 ほか）を `unityLibrary` に足す |
| ABI | AAR は Java のみ | `arm64-v8a` と `x86_64` のみ。`armeabi-v7a` では `DllNotFoundException` |
| minSdk | Player Settings 25（Build Profile が 31 で上書き） | 31 以上が必須 |

## 2. 参照

native-toolkit のパス（このマシン上。`feature/NTKIT-17`、`49b99bc4` の時点）。

| 見るもの | 場所 |
|---|---|
| **こちらへの申し送り** | `artifact/topics/android-c-abi/results/2026-10-10-android-c-abi-stage2c-handoff.md` |
| ブリッジの 69 メソッドと C ABI の対応（「Unity の包みが保つこと」の列） | `artifact/topics/android-c-abi/designs/2026-10-05-android-c-abi-c-abi-design-part2.md` 8.3 |
| 包みに渡す事柄 | 同 16 章 |
| スレッド、寿命、`release` | `designs/2026-10-04-android-c-abi-c-abi-design.md`（第 1 部）1.3、1.4、5.7、5.8 |
| 構造体の配置の期待値 | `android/android_library_capi` のテスト `StructLayoutTest.cpp` |
| 配布物 | `dist/1.13.0/android/`（AAR 2 つ、`include/NativeToolkitC/`、`m2/`） |
| 各機能の C の実例 | `manual/1.13.0/` の各章の「### C ABI」 |

## 3. Windows と違うところ（写してはいけないもの）

申し送り 4 章の要点。各機能の設計書はこれを前提にする。

| # | Windows | Android |
|---|---|---|
| A-1 | `user_data` は NULL、出力の要求の ID を表で照合 | **登録ごとに別の `user_data`**。呼ぶ前に表へ入れる。UnityMain から呼ぶと、関数が戻る前に完了が来うる |
| A-2 | 要求の ID は 32 ビット | **64 ビット（`ulong`）** |
| A-3 | コールバックの引数は借りるポインタ | **所有権ごと渡される**。コールバックの中で写して `_free` する |
| A-4 | `release` を期限付きで待つ | **どのスレッドでも待たない**（UnityMain と Android の main が互いに待って止まる） |
| A-5 | コールバックは OS のスレッド | **Android の main（UI スレッド）**。UnityMain ではない |
| A-6 | 宣言は Windows のもの | `Common.h` だけ共通。機能の関数・列挙・構造体は Android 用に別に宣言する（同名で値や引数が違うものがある） |

## 4. 決定

申し送り 5 章の 7 件。推奨どおりに採る（2026-10-11）。理由が申し送りと同じものは書かない。

| # | 事柄 | 決定 |
|---|---|---|
| J-1 | 依存を足す口 | `IPostGenerateGradleAndroidProject`。`NATIVETOOLKIT_ENABLE_BUILD_STEPS` の外（`NativeToolkit.Editor`）に置き、UPM の利用者のビルドでも動く。足す先は `unityLibrary/build.gradle`、何度流しても 1 回 |
| J-2 | 依存の版 | C# に定数で持ち、dist の `m2/` の `.module` と照らす EditMode のテストを置く（dist が無い環境ではテストを飛ばす） |
| J-3 | AAR の版 | `Plugins/Android/VERSION.txt` に 2 つの AAR の完全なファイル名を書き、`PreBuildProcessor` はそれを読む（Windows と同じ） |
| J-4 | 名前空間とフォルダ | `Runtime/Android/<Feature>/`、`JonghyunKim.NativeToolkit.Runtime.Android.<Feature>`。共通部は `Runtime/Android/Common/AndroidNativeToolkitCApi.cs` |
| J-5 | 32 ビット | **対象外**（J-10。2026-10-11 に改めた）。32 ビットの ABI（ARMv7）を含む Android のビルドは、Gradle の前に `AndroidArchitectureCheck` が分かりやすいメッセージで止める（6.4）。`DllNotFoundException` を「ネイティブが使えない」に写す処理は、念のための防御として残すが、テストはしない。初めは「32 ビットでは `DllNotFoundException` を『使えない』に写し、Manager は失敗を返す」としていたが、capi の AAR の Prefab のためにビルドの段で止まると分かり、改めた |
| J-6 | `NOT_FOREGROUND` と AP-16 | 対応表 8.3 の列のとおりに保ち、保てないものは既知の差分と移行ガイドに書く。機能ごとの設計書で 1 つずつ決める |
| J-7 | JSON の型 | 機能ごとの設計書で決める。`AndroidClipboardJsonParser` は `internal`、`AndroidClipboardJsonBuilder` / `AndroidShareJsonBuilder` / `AndroidNotificationJsonBuilder` は `public` |
| J-8 | フォルダの移し方 | Android の 4 機能の型（`Android*` の 11 と、接頭辞の無い Android 専用の結果型 10。計 21 ファイル）を、Dialog の実装の前の 1 コミットでまとめて `Runtime/Android/<Feature>/` に移す。名前は変えない（接頭辞の改名は os-prefix-violations の範囲）。1 つの OS の中で形を混ぜないため（Dialog 設計 v3 の J-D11） |
| J-9 | C ABI の完了の配送 | 受け口は `Runtime/Android/Common/AndroidNativeCallbackQueue` に入れるだけにし、イベントは取り出した時点の `Instance` に出す（旧の dispatcher の経路と同じ振る舞い）。`UnityMainThreadDispatcher` は使わない（`dispatcher-runs-under-lock`）。4 機能とも同じ（Dialog 設計 v3 の J-D2）。**Share の例外**（Share 設計 v6 の J-S4）: 取り出した時点で生きている Manager（`_instance`）に出し、いなければ捨てる（`Instance` を作り直さない。1.x は Manager を壊すとブリッジのリスナーを外し、その後の結果を捨てた）。受け口が per-call の枠を取り出す（J-S5）。`agent-rules/coding-rules/common.md` の「Share の例外」。**Clipboard も同じ**（Clipboard 設計 v4 の J-C3）: 結果と変更を、取り出した時点で生きている Manager に出し、いなければ捨てる。C の関数が同期なので、per-call は呼び出しの時点で閉じ込める（`common.md` の「Clipboard の形」） |
| J-10 | Android の対応の範囲 | **IL2CPP + ARM64（と x86_64）。32 ビット（ARMv7）と Mono は対象外**（2026-10-11、利用者と相談）。Google Play は 64 ビットを必須にしており、Unity の Android の Mono は ARMv7 しか組めない。32 ビットを含むビルドは止める（J-5、6.4）。移行ガイドとマニュアルに書く |
| J-11 | 1.x の振る舞いをどこまで保つか | **ふつうの使い方を保つ**（2026-10-11、利用者と相談。Share の設計レビューで「1.x と同じ」と書いた所が 1.12.0 のコードの細部と違う指摘が 2 回続いたため）。保つのは、公開 API と、UnityMain から 1 回ずつ正しい入力で呼んだときの結果・時点・文言、Editor の結果。複数のスレッドからの同時の呼び出し、Manager を壊す直前・直後の結果、変わった入力（重複した ID、ふつうでない空白、パディングの無い Base64 など）は、いちばん単純な形にして既知の差分に書く。設計で「1.x と同じ」と書く行には、タグ `1.12.0` のコードの位置を必ず付ける。Clipboard と Notification にも同じ方針を使う。**Clipboard の例外**（Clipboard 設計 v3 の J-C11。2026-10-12、利用者の判断）: `StartObserving` は監視を Android の main で後から始めるので、その前に呼んだコピー（同じ流れで続けて呼んだものなど）の変更は `ClipboardChanged` に届かないことがある。1.x は開始とコピーを同じ main に順に積んだので届いた。順を保つには native-toolkit の内部の順序に頼る仕組みが要るので保たず、既知の差分と XML コメントに書く。**Notification の例外**（Notification 設計 v4 の J-N6、J-N7。2026-10-12、利用者の判断）: 通知の権限が無いとき・アプリの通知がオフのときの表示は、1.x の「成功（通知は出ない）」を保たず失敗にする。引けないリソースの名前は、1.x の代わりの値（アプリのアイコン・省略・Default style）を保たず失敗にする（native-toolkit の申し送りは「包みが保つ」だったが、C# からはどの名前が引けなかったかが分からず、1.x と同じ結果を作れないため） |

## 5. 進め方

| 段 | 中身 | 状態 |
|---|---|---|
| 0 | 移行前の基準を取る（EditMode、Android 実機の PlayMode） | 完了（6.1） |
| 1 | スパイク: VERSION.txt と `PreBuildProcessor`、Gradle の依存、KGP の削除、minSdk 31、`AndroidNativeToolkitCApi`、実機で `ntk_android_is_initialized() == 1`（申し送り 4.1 の「同じ実体」）。旧ブリッジと 2.0.0 の AAR が 1 つの APK に同居できるか | 完了（6.2。`34acb2d`） |
| 2 | Android の Player テストを流すスクリプト（`scripts/verify_unity_android.sh`）。IL2CPP の ARM64 と、パッケージの利用者の条件 | 作成済み（`34acb2d`）。どちらも PASS。32 ビットと Mono の実行は持たない（J-10、6.3、6.4） |
| 3-0 | Android の 4 機能の型 21 ファイルを `Runtime/Android/<Feature>/` に移す（J-8）。振る舞いは変えない | 完了（`44f206d`。EditMode と実機が基準と同じに通った） |
| 3 | 機能ごとに: 設計 → レビュー → UI テスト計画 → **1.x のまま UI テストを流して記録** → 移行の実装 → 同じテストを流し、1.x と同じか既知の差分かを確かめる → 実装のレビュー → 実装結果。順は Dialog → Share → Clipboard → Notification。UI の補助は Dialog の前半で作る | Dialog: 設計 v4、UI テスト計画 v1、UI の補助の設計 v5。利用者の確認を経て実装に入った（2026-10-11）。**UI の補助を作り、1.x で 29 件を記録した（6.5）**。**C ABI への移行を実装し、実機で 49/49（6.6）**。コミット済み（`0513bb4`、`7f7ec32`）。Share: 設計 v6（`artifact/android/share/designs/2026-10-11-android-share-design-v6.md`）。設計レビューを 6 回回し、v6 で A1 が 0（2 回目の後に保つ範囲を J-11 に決めた）。UI テスト計画 v1〜v5 と UI の補助の設計 v6〜v11（レビューを 5 回回し、打ち切りに達した。5 回目の直しは doctest と実機で確かめた。利用者の判断でコミット: `c427a9c`、`5729578`）。**1.x のまま 45/45 を記録した（UI テスト計画 v6 の 5 章。2026-10-12）**。1.x は別のスレッドからの呼び出しを受け付けなかった（設計 v6 J-S16 の「1.x と同じ」は誤り。既知の差分として次の版に足す。持ち越し K-5）。**C ABI への移行を実装した（2026-10-12）**: 層 1 の `AndroidShareCApiTests`、層 2a の `AndroidShareManagerEditorTests` 20 本、実機で Share 45/45（と、共通部を変えたので Dialog 36/36）。実装のレビューを 2 回回し、2 回目で両方の A が 0（実装結果 v2）。Clipboard: 設計 v1〜v3（`artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v3.md`）。設計レビューを 2 回回し、2 回目で両方の A1 が 0。監視の開始の直後の変更を J-11 の例外にした（利用者の判断）。OS の画面を出さないので UI の補助と UI テスト計画は作らず、設計の 7.3 の表を使う。**1.x のまま 37/37 を記録した（設計 v4 の 7.3。2026-10-12）**。1.x は別のスレッドからの呼び出しを受け付けず、文字列の null を `"null"` として扱った。コピーの直後の `Read` は 1.x でも 10 回すべてで新しい本文を読んだ。**C ABI への移行を実装した（2026-10-12）**: 層 1 の `AndroidClipboardCApiTests`、層 2a の `AndroidClipboardManagerEditorTests` 30 本、実機で Clipboard 40/40。通しの検証は PASS。実装のレビューを 2 回回した（実装結果 v3）。IL2CPP では `DllImportAttribute` のライブラリ名を反射で読めなかった（AC-33 から外した）。Notification: 設計 v1〜v4（`artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md`。設計レビュー 3 回）、UI テスト計画 v1〜v4 と UI の補助の設計 v12〜v15（レビュー 3 回。3 回目で A1 が 0。3 回目は Codex が利用上限で使えず、利用者の指示でサブエージェントだけで判定した）。**UI の補助をシェードと画面の命令に広げ、今の実機の UI テストが退行しないことを確かめた（6.7）** |
| 4 | 旧ブリッジの AAR と JSON の層を消す。マニュアル。リリース。**マニュアルの Android の動作環境に「ARM64（と x86_64）だけ、IL2CPP だけ」と書く**（J-10。native-toolkit の manual/1.13.0/index の 32 ビットの項目と合わせる。native-toolkit-1d の勧め、2026-10-11）。移行ガイドには、名前空間、既知の差分、「Configure Gradle Project」の削除、32 ビットのビルドが止まることを書く。**出す版は 1.13.0。4 機能の移行の後、リリースの前に、接頭辞の無い Runtime の 11 型の一括改名（`artifact/topics/os-prefix-violations` の案 1。Android の 10 型と macOS の `IconConfiguration` 1 型）を 1 つのタスクとして行い、移行ガイドに書く**（2026-10-12、利用者と相談） | 未着手 |

## 6. 記録

### 6.1 移行前の基準（2026-10-11、`48d7927`）

**移行前の基準: `48d7927`**（`feature/UNT-13` の起点。develop と同じ）。移行のあと「前は通っていたか」を確かめるときは、ここで流す。

| 何を | 結果 | 条件 |
|---|---|---|
| EditMode | 1154 件中 1153 件が通過。Android の 116 件はすべて通過 | macOS、`-buildTarget Android`。落ちた 1 件は `WindowsClipboardSampleFixtureTests.TemporaryFilePathsAreAbsoluteAndUseBackslashes`（macOS ではパスの区切りが `/` になる。移行とは関係ない） |
| Android の PlayMode（実機） | **11 / 11**（`AndroidClipboardManagerIntegrationTests`） | Pixel 6a（Android 16）、IL2CPP、`-activeBuildProfile "Assets/Settings/Build Profiles/Test Android™ Profile.asset"`、`NATIVE_TOOLKIT_DIST_ROOT` に dist/1.12.0 だけが見えるフォルダを指した（下の 2 点のため） |

基準を取る途中で分かったこと:

- **dist/1.13.0 が置かれた時点で、この repo の Android のビルドは壊れていた。** `PreBuildProcessor` は dist の最新の版（1.13.0）を選び、前方一致 `android-native-toolkit-` で `android-native-toolkit-capi-2.0.0.aar` を拾い、ブリッジの AAR が無いので `LogError` を 2 件出す。Unity はこの 2 件で `Error building Player: 2 errors` としてビルドを止める。Windows の 2.5 と同じ罠（J-3 で直す）
- **CLI のテスト（`-runTests -testPlatform Android`）は Build Profile を使わず Player Settings を使う。** Player Settings の minSdk は 25 で、AAR の minSdk 31 と manifest の merge で衝突してビルドが失敗する（申し送り 3.4）。`-activeBuildProfile` で Test Android™ Profile を指せば通る。Player Settings も 31 にそろえる（スパイクで行う）
- システムの adb（34.0.1、`~/Library/Android/sdk`）と Unity 同梱の adb（36.0.0）は互いのデーモンを落とし合う。Unity を流す間は Unity 同梱の adb を `PATH` の先に置く

### 6.2 スパイク（2026-10-11）

**結果: 申し送り 4.1 の「同じ実体」を確かめた。** Unity の `DllImport("ntk")` で読んだ `libntk.so` は、androidx.startup の `NtkInitializer` が初期化したものと同じで、`ntk_android_is_initialized()` が 0 以外を返す。Unity の側で初期化のために呼ぶものは無い。

| 何を | 結果 |
|---|---|
| 実機の PlayMode（IL2CPP、Android™ Profile） | **13 / 13**。`AndroidNativeToolkitCApiPlayerTests` の 2 件（`ntk_version()` の major が 2、`ntk_android_is_initialized()` が 0 以外）と、旧ブリッジの `AndroidClipboardManagerIntegrationTests` 11 件 |
| 同居 | **できる。** 2.0.0 の 2 つ（`com.jonghyunkim.nativetoolkit.*`）と 1.3.0 の 2 つ（`android.library.*` / `android.unity.*`）を 1 つの APK に入れてビルドでき、旧ブリッジも kotlin-stdlib 2.2.21 の上で動く。パッケージ名が違う（native-toolkit の D-15）ので、クラスがぶつからない。**Windows の J-6 と同じく、機能を 1 つずつ移す**（VERSION.txt の `extra_*` で 1.3.0 を置く） |
| EditMode | 1161 件中 1160 件が通過（落ちたのは基準と同じ 1 件）。`AndroidGradleDependenciesTests` 7 件を足した（書き換えが 1 回だけであること、古い版の置き換え、CRLF、`.module` との照合） |

入れたもの:

- `Plugins/Android/VERSION.txt`（J-3）と `PreBuildProcessor` の `AndroidPin`。ピンが解決しなければ `BuildFailedException`。ピンに無い AAR は消す。読み方の解析は Windows と共通の関数に出した
- `Editor/Android/AndroidGradleDependencies.cs`（J-1、J-2）。`NativeToolkit.Editor` に置き、define の外で動く。受け口（`IPostGenerateGradleAndroidProject`）は Android のビルドの支援が要るので `#if UNITY_ANDROID`
- `PostBuildProcessor` の KGP の適用を削除。手動のメニュー「Tools/Native Toolkit/Android/Configure Gradle Project」（`AndroidGradlePatcher`、`AndroidConfigureProjectWindow`）も削除。書き出し（Export）にも `IPostGenerateGradleAndroidProject` が走るので要らない。**マニュアルの Android のサンプルの手順（`Documentation~/index.md` の「Configure Gradle Project」）を直す**（段 4）
- Player Settings の minSdk を 25 から 31 に
- `Runtime/Android/Common/AndroidNativeToolkitCApi.cs`（J-4）。`Common.h` と `ntk_android_is_initialized`
- `scripts/verify_unity_android.sh`

途中で踏んだこと:

- **`Test Android™ Profile` を有効にすると、ビルド処理のアセンブリがコンパイルされない**（define が空。メモリ `project_build_define_profiles` のとおり、パッケージの利用者を再現するためにわざと空）。`-activeBuildProfile` は Library に残り、後の実行にも効く。1 回目の実機の実行はこれで AAR が写らず、`DllNotFoundException` になった。**同じ理由で、Test の Profile のまま EditMode を流すと、ビルド処理のコンパイルエラーを見逃す**（実際に 1 件見逃した）。スクリプトは毎回 `-activeBuildProfile` を渡し、既定は Android™ Profile にした
- 利用者のビルドでは、`Plugins/Android` にコミットされた AAR がそのまま使われる。**2.0.0 の AAR はコミットする**（Windows の DLL と同じ）

### 6.3 Mono は ARMv7 になる（2026-10-11）

`--mono` で実機の PlayMode を流すと「Target architecture not specified」でビルドが止まった。Unity の Android の Mono は ARM64 を扱わず ARMv7 だけを組むので、Player Settings の ARM64 だけの指定と合わない。`libntk.so` は arm64-v8a と x86_64 だけなので、**Mono の Player では C ABI を一切使えない**（親課題の J-5 の経路に必ず入る）。

- スクリプトの `--mono` は、テストの設定ファイルで `"architecture": "ARMv7"` も渡し、32 ビットで「使えない」を確かめる実行にした。端末が 32 ビットのアプリを動かせること（`ro.product.cpu.abilist32`。Pixel 6a は可）が要る
- **その後（同日）、`--mono` を `--armv7`（IL2CPP + ARMv7）に置き換えた**（J-10）。Android の Mono は出荷に使えない（Google Play は 64 ビットが必須で、Mono は ARMv7 しか組めない）。現実に 32 ビットの経路に入るのは、利用者が ARMv7 も含めた IL2CPP のビルドを 32 ビットで動かしたときなので、その条件で確かめる
- `AndroidNativeToolkitCApiPlayerTests` を 64 ビットと 32 ビットで分けた（32 ビットでは `EnsureNativeAvailable()` が `NativeUnavailable` で、例外が出ないこと）
- 移行ガイドに「Android は IL2CPP と ARM64 が要る」と書く（段 4）

### 6.4 ARMv7 を含めると、ビルドが Gradle で止まる（2026-10-11）

`--armv7`（IL2CPP + ARMv7）で実機の PlayMode を流すと、Player のビルドが Gradle で失敗した。

```
Execution failed for task ':unityLibrary:configureCMakeDebug[armeabi-v7a]'.
> [CXX1210] .../unityLibrary/src/main/cpp/CMakeLists.txt debug|armeabi-v7a : No compatible library found [//ntk/ntk]
```

- capi の AAR は、C/C++ の利用者向けに Prefab（`prefab/modules/ntk/`。AGP が CMake の `find_package` に渡す形式）を持ち、その `libntk.so` は arm64-v8a と x86_64 だけ
- Unity が書き出す `unityLibrary` は、GameActivity のために `android.buildFeatures.prefab true`。Unity は `ntk` の Prefab を使わない（`jni/<ABI>/libntk.so` を `DllImport` で読むだけ）が、AGP は依存の Prefab を ABI ごとに調べ、armeabi-v7a で見つからずに止まる
- **申し送り 3.1 の「32 ビットでも落ちず、`DllNotFoundException` を『使えない』と扱える」は、Unity では成り立たない。** ビルドの段で止まるので、実行時の経路に届かない。ARMv7 と ARM64 の両方を出荷している利用者は、2.0.0 に上げるとビルドできなくなる

扱い（利用者と相談。J-5、J-10）:

- 32 ビットは対象外にする
- `Editor/Android/AndroidArchitectureCheck.cs`（`IPreprocessBuildWithReport`、define の外）が、Target Architectures に ARM64 と x86_64 以外があれば、Gradle の前に `BuildFailedException` で止め、Player Settings の直し方を出す。EditMode のテスト（`AndroidArchitectureCheckTests`）で組み合わせを固めた
- スクリプトの `--armv7` と、Player テストの 32 ビットのケースは消した
- native-toolkit-1d には、直しの依頼ではなく報告として伝える（Prefab を有効にした Gradle の利用者にも同じことが起きうるので、native-toolkit のマニュアルに書くかの判断材料）
- native-toolkit の返事（同日、`feature/NTKIT-17` の `a7684dee`）: manual/1.13.0/index（3 言語）の 32 ビットの項目に、Prefab が有効で 32 ビット向けに組むモジュールは `ntk::ntk` を使わなくても CXX1210 で止まること、Unity の `unityLibrary` がこれに当たることを書いた。申し送り 3.1 に訂正、5 章の表の 32 ビットの行にこちらの決定を書いた。C ABI の直しは無い

確かめたこと（同日。Unity Editor を閉じてから流した。Editor でプロジェクトが開いていると、コマンドラインの Unity はプロジェクトを開けずにすぐ終わる）:

- EditMode: 1168 件中 1167 件が通過、失敗 0（`AndroidArchitectureCheckTests` 7 件を含む）
- ARMv7 を含めたテストのビルド（`-testSettingsFile` で `"architecture": "ARMv7"`）: Gradle の前に `BuildFailedException: [NativeToolkit] Native Toolkit for Android supports ARM64 (and x86_64) only; its native library is not built for ARMv7. ...` で止まった。`CXX1210` は出ない
- テストのビルドが途中で止まると、Test Framework が一時的に変えた設定（スプラッシュ、コードのストリップ、署名）が `Android™ Profile.asset` に残る。`git checkout` で戻した。スクリプトの片付けは `Assets/Settings` を戻すので、スクリプトから流せば残らない

### 6.5 UI の補助と、Dialog の 1.x の記録（2026-10-11）

Dialog 設計 v4 5.5 の手順 2〜3。補助（`scripts/android_ui_driver.py` ほか）を作り、旧ブリッジ（1.x）のまま Dialog の UI テスト 29 件を実機で流して記録した。4 回目に Android™ Profile で 42/42（Clipboard の 11 件、C ABI の共通部の 2 件を含む）、パッケージの利用者の条件で 33 件が合格・9 件が skip（サンプルのシーンが無い。想定どおり）。結果は UI テスト計画 v1 の 5 章と「1.x」の列、補助で直したことは UI の補助の設計 v5。

- 1.x の実測で、推定を確かめた: 長い `checkedItems` は長さのまま返る（AD-22）、`checkedItem = -1` は `null`（AD-23）、範囲外の `checkedItem` でもダイアログは出て、その値が返る（AD-33。移行後は同期の失敗になる既知の差分）
- **Google の自動入力が、テストのパスワードの保存を求める画面を出し、それが残って以後のテストを止めた。** テストの値は「Not now」で閉じて保存させていない。補助は実行の間、自動入力を切る（元の設定は終わりに戻す）
- **補助が後片付けの途中で止まり、端末の設定（アニメーション、充電中の画面の点灯、自動入力）が変わったまま残った**（1・2 回目）。1 回目の後は元の値を記録していなかったので、Android の既定の値に戻した（アニメーションは既定の 1 倍、画面の点灯はオフ）。利用者に、開発者向けオプションで別の値にしていたら直すよう伝えた。2 回目の後は状態ファイルから 7 つとも元の値に戻した。補助は、後片付けの間は合図を無視し、状態ファイルを残すようにした

### 6.6 Dialog の C ABI への移行（2026-10-11）

Dialog 設計 v4 5.5 の手順 4〜9。`AndroidDialogCApi`（Bridge）、共通部の取り出し口 `AndroidNativeCallbackQueue`、`AndroidDialogManager` の書き直し、層 1・2a のテスト、2.0.0 だけの UI テスト 5 件を足した。結果は Dialog の実装結果 v4（`artifact/android/dialog/results/2026-10-11-android-dialog-implementation-feature-result-v4.md`。v1 は移行の直後、v2〜v4 は実装レビュー v1〜v3 の修正の後）。UI テスト計画は v2（AD-36、AD-37 を足した）。

- 実機（Pixel 6a、IL2CPP ARM64、Android™ Profile）: 47/47。Dialog の AD-01〜AD-34 がすべて合格。パッケージの利用者の条件: 38 件合格・9 件 skip（想定どおり）。実装レビュー 3 回の修正の後の最終回（`verify-fix3`）で、実機 49/49（AD-36、AD-37 を含む）、EditMode 1229/1230、Editor の PlayMode 226/237（skip はどちらも既存のもの）。移行の直後は 1226/1227、219/230 だった
- 1.x の 29 件のうち期待値を変えたのは、Dialog 設計 v4 5.3 の既知の差分に当たるものだけ（閉じたときの選択、範囲外の `checkedItem`、長い `checkedItems`、サンプルのパスワードの表示、検査のログの段階）
- 後ろから呼んだときの `NOT_FOREGROUND`（AD-27）は実機で確実に起こせた。補助が Activity の `STOPPED` を確かめてから、別のスレッドが Bridge を呼ぶ。Unity が一時停止している間も Test Runner の接続は切れない（UI の補助の設計 v5 の V-8）
- C ABI の側の直しは要らなかった（native-toolkit-1d へ戻すものは無い）
- 実装レビューを 3 回（サブエージェントと Codex）回し、3 回目で A が 0 件になった。3 回目では、別のスレッドからの `Show*` が例外を出す回帰（1.x は動いた見込み）も見つかり、直した。製品の側では、確保した後に一覧へ足すまでの間の漏れ、取り出し口が壊された・無効にされたときに完了が無言で止まること、などを直した。テストの側では、後片付けで要求とイベントの数を照らし、Unity Test Framework が入れ子の手順の失敗の後に外側の `finally` を走らせないことに合わせて、片付けが必ず走る形にした。直した所は、わざと壊して落ちることを確かめた（実装結果 v2 の 9 章、v3 の 10 章）
- **Windows にも、確保した後に一覧へ足すまでの間の漏れと同じ形が 3 か所ある**（実装結果 v3 の 7 章）。この段では変えない

### 6.7 UI の補助を Notification に広げる（2026-10-12）

UI の補助の設計 v15 の実装。Notification の設計 5.5 の手順 3（1.x の記録）の前に、補助と検証のスクリプトを広げた。Notification のテストはまだ足していない。

- 補助（`scripts/android_ui_driver.py`）:
  - 画面の命令 6 つ（`notification-present`、`notification-tap`、`notification-action`、`notification-dismiss`、`permission-answer`、`close-screen`）と、`grant`、`appop-allow` を足した
  - HELLO の検査と `--package` の引数を足した。v11 からは、偽の HELLO でシェルに命令を入れられた
  - 読み取りは毎回別の名前の端末のファイルに書き、読んだ直後に消す。終わりにはすべてを消す
  - システムの UI（`com.android.systemui`）の木は残さない
- `doctest` は全件通った。決まりを 1 つずつ壊した版でも確かめた:
  - 新しい決まりの 32 通りは、すべて捕まえた。最初は 4 通りを捕まえられず、模擬の端末と `doctest` を直した。捕まえられなかったのは、終わりの削除、Back を読まずに押す、見ていない画面の見張り、`stay` の後の `collapse`
  - Sharesheet の前の壊し方 49 通りのうち、当てられる 38 通りも、すべて捕まえた（11 通りはコードが変わって当てられない）。1 通りは、読み取り 1 回の adb の呼び出しが 4 回から 3 回に減ったため、遅れて消えるシートの `doctest` が押し直しの経路に入らなくなっていた。遅れを 5 回の読み取りにして直した
- テストの側（`AndroidUiDriver.cs`）: 画面の命令の一覧と、200 秒の待ちを足した
- 検証のスクリプト（`verify_unity_android.sh`）:
  - 補助に `--package` を渡す
  - 実機の実行の前と `trap` で、テストの Player をアンインストールする
  - Test Android™ Profile の実行でも、`AndroidSampleScreen` を除く `AndroidUiDriver` のテストの skip を見る
  - Dialog と Share のサンプル画面のテスト 22 本に、このカテゴリを付けた
- 実機（Pixel 6a、`--as-package-user` 付きの通しの検証）: PASS
  - EditMode 1263/1264（skip 1）、Editor の PlayMode 276/313（skip 37）
  - Android™ Profile の実機 123/123。補助の命令は 178、終了コードは 0。`AndroidUiDriver` のテストは 65 本で、skip は 0
  - Test Android™ Profile の実機 101 件が合格、22 件が skip（サンプル画面のテストだけ）。`AndroidSampleScreen` を除く `AndroidUiDriver` のテストは 43 本で、skip は 0
- 実行の途中で、Unity のログが 5 分以上伸びない時間が 2 回あった。どちらも止まってはいなかった
  - 1 回目は Gradle の中の IL2CPP の C++ のコンパイル（clang が CPU を使っていた）
  - 2 回目は端末の上のテストの実行中で、補助への依頼は増え続けていた（`absent-in-sheet` は 1 件 30 秒あまり）
  - 実機の実行の間、ホストの側の Unity のログは伸びない。止まったかは、補助への依頼が増えているかで見る
