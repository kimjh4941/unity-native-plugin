# Windows の C ABI が 2.0.0 に置き換わり、P/Invoke 層を書き直す

- 記録日: 2026-09-23
- 分類: 横断課題（移行）。**機能追加ではない。** clipboard / notification / dialog の 3 機能にまたがる
- 発見経緯: native-toolkit 側（`feature/NTKIT-16`）から、1.x の C ABI を削除し 2.0.0 に置き換えた旨の申し送りを受けた
- 対応方針: **native-toolkit が develop にマージされ 1.12.0 が出るまで、実装は始めない。** 待つ間に、
  設計と**検証手段**を用意する。検証手段は層 2b / 層 3 の自動テストで、
  [cross-platform-testing](../cross-platform-testing/README.md) 側で先に立てる（下記）
- 進捗: **完了（2026-09-29）。3 機能とも移行済み。** 47 本の対応は確定（5 章）。未決（6 章）はすべて解決。
  **Dialog**（2026-09-27、`3cf4b1b`〜`1b9a9ee`。結果は `artifact/windows/dialog/results/2026-09-29-windows-dialog-implementation-feature-result-v3.md`）、
  **Notification**（2026-09-27。結果は `artifact/windows/notification/results/2026-09-29-windows-notification-implementation-feature-result-v2.md`）、
  **Clipboard**（2026-09-27〜29、`afee02c`〜`722ec41`。結果は `artifact/windows/clipboard/results/2026-09-29-windows-clipboard-implementation-feature-result-v7.md`）。
  1.x の DLL は同梱物から消えた。3 機能の Player テストは Mono と IL2CPP の両方で通る（2026-09-29、本体 93 / 93、作り直し 1 / 1）
- その後（2026-09-30〜10-03）:
  - native-toolkit が、パッケージ化していないアプリのコールドスタートで `onInvoked` が 2 回（中身の無い 1 回と本物）呼ばれる不具合を直した（`3a9c2c20`）。あわせて、Clipboard の close の説明を直した（`af72b409`。`BUSY` は実行中の履歴の要求でも起き、待つにはオーナーのスレッドでメッセージを処理する。`WRONG_THREAD` 以外の失敗は再試行してよい）。こちらからの指摘が元で、記録は native-toolkit の `artifact/topics/windows-architecture/results/2026-09-30-windows-clipboard-close-busy-finding.md`
  - 直した DLL（md5 `41ac440bf583e7e209c98a034904e008`）で、2026-10-03 に Mono と IL2CPP の両方で本体 93 / 93、作り直し 1 / 1、M-19 と層 3（Mono）、サンプルの照合が通った。2026-09-30 の 1 回目は PC の負荷とプロジェクトのロックで落ちた（DLL とは無関係。`artifact/windows/clipboard/issues/shutdown-drain-budget-under-load.md`、cross-platform-testing の「検証を流すときの前提」）
  - native-toolkit 1.12.0 がリリースされ（タグ `1.12.0`、`dd327122`）、同じ DLL を同梱した（`43f03f7`。VERSION.txt の `source` はタグを指す）
  - マニュアル 1.12.0 を書いて公開した（`38d4414`、`272f0ba`。Windows の 3 機能に「Changes in 1.12.0」の節）
- 残作業（2026-10-03 更新）:
  - 済み（2026-10-03）: 1.12.0 をリリースした。PR #29（feature/UNT-12 → develop）と #30（develop → main）をマージコミットで入れ、タグ `1.12.0`（`d49a52fa`）と GitHub Release を作った
  - リポジトリの外: 利用者の個人のスキル `C:\Users\User\.claude\skills\commit-msg`（日本語で作る別物）が、repo の `.claude/skills/commit-msg` より優先して呼ばれる。native-toolkit 側でも同じ。名前を変えるか消すかはユーザーの判断で、2026-10-03 時点では据え置き
- **移行前の基準: `50fe7bb`**（`feature/UNT-12`）。1.x の同梱 DLL で、Clipboard / Dialog / Notification のサンプルの UI 自動テストが通る最後のコミット。移行のあと「前は通っていたか」を確かめるときは、ここでテストを流す
- 2.5（Windows Player ビルドが 2.0.0 の DLL を勝手に掴む）は**対応済み**（`0948942`）。VERSION.txt のピンは移行の中で書き換えた（2.5「移行時にやること」）

- 対象: `Runtime/Windows/Clipboard/Windows*.cs`、`Runtime/Windows/Notification/Windows*.cs`、`Runtime/Windows/Dialog/WindowsDialogManager.cs`、`Plugins/Windows/`、対応するテストとサンプル
- チケット: 未採番
- 相手側: native-toolkit `artifact/topics/windows-architecture/`（分類: 横断課題（アーキテクチャ））

---

## 1. 何が起きているか

1.x の C ABI は**削除された**。互換レイヤーは無く、2.0.0 は別物である。

| | 1.x | 2.0.0 |
|---|---|---|
| 関数 | 52 | `ntk_*` に改名。**105**（Common 11 / Dialog 6 / Notification 48 / Clipboard 40） |
| 文字列 | `wchar_t*`。呼び出し側のバッファを 2 回呼んで採寸 | UTF-8。不透明ハンドルから借りる |
| 構造化データ | JSON 文字列 | 型付き構造体（`struct_size` で版管理）とリストハンドル + index アクセサ |
| エラー | `DWORD* pError` の出力引数 | 戻り値。OS の生値は `ntk_last_system_code()` |
| 解放 | プロセス横断の状態を持つ free 関数 | ハンドルごとの `_free` |

ヘッダーの宣言は 114 あるが、うち 9 本は関数ではなく**コールバックの typedef**（`ntk_release_fn`、`ntk_notification_invoked_fn`、Clipboard の 7 本）。`105 + 9 = 114`。

**この repo は今のところ壊れていない。** 1.x の DLL を同梱して 1.11.0 として動いているので、急ぐ理由は無い。
（2026-09-23 時点の記述。2026-09-29 に 3 機能とも 2.0.0 に移り、1.x の DLL は同梱物から消えた）

## 2. 影響範囲

### 2.1 P/Invoke 宣言

| ファイル | P/Invoke | 行数 | 内訳 |
|---|---|---|---|
| `Runtime/Windows/Clipboard/WindowsClipboardManager.cs` | 27 | 3,984 | リネームのみ 3 / 振る舞い変化 24 |
| `Runtime/Windows/Notification/WindowsNotificationManager.cs` | 14 | 507 | リネームのみ 6 / 振る舞い変化 8 |
| `Runtime/Windows/Dialog/WindowsDialogManager.cs` | 6 | 567 | リネームのみ 0 / 振る舞い変化 6 |

**47 本のうち 38 本で振る舞いが変わる。** 対応は 4 章。

**訂正（2026-09-29）:** 初めは「リネームのみ 4 / 振る舞い変化 23」「37 本」と書いていた（native-toolkit の設計 8.3 の分け方）。`getPreferredClipboardFormat` は呼び出し側のバッファから `ntk_string` のハンドルに変わるので、リネームだけではない（Clipboard 設計 v12 の 1.1 の OP-20）。5 章もこれに合わせた

旧 52 関数のうち呼んでいなかった 5 本は `DLog` / `DFLog` / `DFLLog` / `ToWString` / `ConcatWStrings` の Common ヘルパーで、決定 E-6 により対応先なく廃止された。**移植するものは無い。**

### 2.2 JSON 廃止で消えるもの（確定）

ABI から JSON が消えるため、JSON を前提に作った層が丸ごと不要になる。**設計 8.3 と行単位で突き合わせ済み。**

| ファイル | 置き換わる先 |
|---|---|
| `Runtime/Windows/Clipboard/WindowsClipboardJsonParser.cs` | 履歴はハンドル + index（`ntk_clipboard_history_count` / `_item_id` / `_item_text` / `_item_content_type_count` / `_item_content_type_at` / `_item_timestamp_unix_ms`）。書式一覧と `pasteFiles` は `ntk_string_list`。履歴の可用性は `int32` フラグ 2 つ |
| `Runtime/Windows/Clipboard/WindowsClipboardJsonBuilder.cs` | `copyFiles` は `const char* const*` + 個数。`copyMultipleFormats` はビルダー（`ntk_clipboard_items_create` / `_add_text` / `_add_html` / `_add_bytes` / `_free`） |
| `Runtime/Windows/Notification/WindowsNotificationJsonBuilder.cs` | `ntk_notification_content_create` + セッター 22 本 + `_free` |
| `Tests/Runtime/WindowsClipboardJsonParserTests.cs` | 対象が消えるので削除 |
| `Tests/Runtime/WindowsClipboardJsonBuilderTests.cs` | 同上 |

**あわせて消えるもの（当初見落としていた）:**

- **base64 ヘルパー。** 1.x は `copyMultipleFormats` のバイト列を base64 にして JSON へ埋めていた。2.0.0 のビルダーは**生バイトを取る**ので、この経路の base64 も不要になる
- **`getAllNotifications` の「バッファ不足で切り詰め」ケース。** リストハンドル（`ntk_notification_list_count` / `_id_at` / `_tag_at` / `_group_at`）になり、truncation そのものが起こらなくなる

**残る JSON は 1 箇所だけ。** `ntk_notification_activation_raw_arguments` が OS から来た引数文字列をそのまま返す逃げ道として残る。構造化した値は `ntk_notification_activation_value_count` / `_key_at` / `_value_at` で、押されたボタンの引数とすべてのテキスト・コンボ入力を id で束ねて返す。**raw を利用者に公開しないなら、JSON は完全に消せる。**

### 2.3 差し替えるバイナリ

| 1.x（移行前） | 2.0.0（2026-09-27 に移行済み） |
|---|---|
| `Plugins/Windows/unity-windows-native-toolkit.dll`（削除した） | `Plugins/Windows/windows-native-toolkit-capi-2.0.0.dll`（**dist の名前のまま置く**。DLL 自身の名前は `NativeToolkitC.dll`） |
| `Plugins/Windows/Microsoft.WindowsAppRuntime.Bootstrap.dll` | **変更なし。** 隣に置く要件も同じ |

x64 のみ。最初のネイティブ呼び出しの前に、`ntk_version()` の major（`>> 16`）が 2 であることを 1 回確かめる
（完全一致ではなく major。2.0.1 などの互換な更新で止めないため。`Runtime/Windows/Common/WindowsNativeToolkitCApi.cs`、2026-09-27 に Dialog で実装）。

**配置名は dist の名前のままにする**（2026-09-26 決定）。C# からは P/Invoke でしか読まないので、
DLL 自身の名前（`NativeToolkitC.dll`）に合わせる必要はない。DLL の名前は、Windows の全機能の共通部
`WindowsNativeToolkitCApi.DllName` の 1 か所に `"windows-native-toolkit-capi-2.0.0.dll"` と置く
（名前に `.` を含むので、ローダーに拡張子を補わせず `.dll` まで書く）。`#if DEVELOPMENT_BUILD` による
2 つの名前の切り替えはなくなる。

名前に版が入るので、**2.0.1 に上げるたびに `DllName` の書き換えが要る**（1 か所）。
その代わり、C# と DLL の版がずれたときは起動時に `DllNotFoundException` で止まり、
別の版が黙って読み込まれることはない（2.5 で起きたのはまさにそれだった）。

### 2.4 波及先

サンプルとテストは本トピックの設計対象に含めるが、**サンプルシーンの作り直しが要る場合は別に切り出す**（Q-4）。

- `Runtime/UI/Windows/Clipboard/`（ExampleController・Fixtures・SampleResult）
- `Runtime/UI/Windows/Dialog/`、`Runtime/UI/Windows/Notification/` の ExampleController
- `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs`
- `Tests/Runtime/WindowsClipboard*Tests.cs`（7 本）、`Tests/Runtime/WindowsNotificationTests.cs`
- `Runtime/Windows/Dialog/Win32MessageBox.cs`、`Editor/UI/NativeToolkitEditorWindow.cs`

### 2.5 着手前にすでに壊れていた（対応済み）

> **対応済み（2026-09-26、`0948942`）。** 案 B を実装した。下の「対応」を参照。
> 以下は発見時の記録として残す。

**移行を始めたかどうかと無関係に、この repo で Windows Player をビルドすると 2.0.0 の DLL が入っていた。**
2026-09-23 に `-testPlatform StandaloneWindows64` を走らせて実測した。

#### 影響するのはこの repo だけ（2026-09-23 訂正）

ビルド処理は無条件には走らない。`Editor/Build/NativeToolkit.BuildProcessors.Editor.asmdef` に
次の制約があり、**define が無ければアセンブリ自体がコンパイルされず、`PreBuildProcessor` は存在しない。**

```
"defineConstraints": [ "NATIVETOOLKIT_ENABLE_BUILD_STEPS" ],
```

**このリポジトリは `ProjectSettings/ProjectSettings.asset` で定義している**（`Standalone` /
`Android` / `iPhone` の 3 つ）。だから今回のビルドで走った。

| 誰 | 走るか |
|---|---|
| **この repo での開発・CI** | **走る。** define が設定済み |
| UPM でパッケージを入れた利用者 | 走らない。define を自分で設定しない限りアセンブリが無い |
| 仮に利用者が define を設定した場合 | `../native-toolkit/dist` が存在しないので `LogError` を出して copy をスキップする |

したがってこれは**配布物の不具合ではなく、この repo の開発・CI 経路の問題**である。
コミット `87eb4f5` のメッセージに「Anyone building a Windows player hits this」と書いたが、
これは範囲を広げすぎている。正しくは「この repo で Windows Player をビルドする人」。

`Editor/Build/PreBuildProcessor.cs` の `FindLatestVersionInDist()` は、
兄弟リポジトリの `../native-toolkit/dist` にあるディレクトリ名を semver として比較し、
**最も高いものを選ぶ**。native-toolkit が `dist/1.12.0/` を置いた時点で、それが選ばれる。

```
[Build] Using native-toolkit version: 1.12.0
[Build][Windows] No -debug.dll published for prefix=windows-native-toolkit-;
                 falling back to the release DLL: windows-native-toolkit-capi-2.0.0.dll
[Build][Windows] Deleted old DLL: ...\Plugins\Windows\unity-windows-native-toolkit.dll
[Build][Windows] Copied windows-native-toolkit-capi-2.0.0.dll -> unity-windows-native-toolkit-debug.dll
```

コピーされた DLL の MD5 は `dist/1.12.0/windows/windows-native-toolkit-capi-2.0.0.dll` と一致した。
**コミット済みの 1.x DLL は削除される。** P/Invoke は 1.x のままなので、
このビルドを実行すれば全関数が entry point 不在になる。

接頭辞 `windows-native-toolkit-` は `windows-native-toolkit-capi-2.0.0.dll` にも一致してしまう。
**`-capi-` の付く方と付かない方を区別していない**（2.3 の注記と同じ罠）。

| 版 | `windows/` の中身 |
|---|---|
| `dist/1.11.0` | `windows-native-toolkit-1.2.0.dll`（1.x。**これが正しい**） |
| `dist/1.12.0` | `windows-native-toolkit-capi-2.0.0.dll`（2.0.0） |

#### 対処の選択肢

| 案 | 内容 | 評価 |
|---|---|---|
| A | `NATIVE_TOOLKIT_DIST_ROOT` で旧 dist を指す | 環境変数の override は `PreBuildProcessor` が既に持っている。ただし `dist` ルートの差し替えであって**版の固定ではない** |
| B | 参照する版をリポジトリ側で固定できるようにする | 「最も高い版」という暗黙の選択をやめる。移行後は 1.12.0 に上げるだけで済む |
| C | 移行が終わるまで native-toolkit の `dist/1.12.0/` を置かない | こちらでは決められない |
| D | 移行までの間、`ProjectSettings` から `Standalone` の `NATIVETOOLKIT_ENABLE_BUILD_STEPS` を外す | コード変更ゼロで即座に止まる。ただし `Standalone` は **macOS も含む**ので macOS の copy も止まる。戻し忘れやすい |

**推奨は B。** A は各自の環境に依存し、CI や別の作業者では再発する。
「最も高い版を黙って選ぶ」仕組み自体が、今回のように相手側の都合で挙動が変わる原因である。

D は今日すぐビルドを回す必要があるときの応急手当てとしては成立する。
ただし恒久策にはしない。**止めた状態を正常だと思い込むと、移行後に copy が走らないまま
1.x の DLL を配り続けることになる。**

#### 対応（2026-09-26、`0948942`）

**案 B を実装した。** Windows は `FindLatestVersionInDist()` を呼ばず、
`Plugins/Windows/VERSION.txt` のピンを読む。VERSION.txt はもともと
「dist 1.11.0 の `windows-native-toolkit-1.2.0.dll`」と正しい答えを文章で書いていたが、
ビルドはそれを読んでいなかった。

| キー | 意味 |
|---|---|
| `dist_version` | コピー元の dist フォルダ（必須） |
| `release_dll` | release ビルドで使う成果物（必須） |
| `debug_dll` | development ビルドで使う成果物。空なら `release_dll` |
| `install_as` | `Plugins/Windows` に置く名前。空なら dist の名前のまま |
| `install_as_debug` | development ビルドで置く名前。空なら `install_as`、それも空なら dist の名前 |

- ファイルが無い、必須キーが無い、宣言した成果物が dist に無い、のいずれでも
  `BuildFailedException` でビルドを止める。以前は `LogError` を出して続行していた
- 古い DLL の削除は `unity-windows-native-toolkit*.dll` と `windows-native-toolkit*.dll` の両方を対象にする。
  ピンを切り替えたときに新旧 2 本が Player に入らないようにするため
- 推測していた `FindDllNameInDist` / `SelectDllName` は削除した。Android / iOS / macOS は従来どおり dist を走査する

**確かめたこと**（`dist/1.12.0` が置かれた状態で、Release ビルド 2 回）:

| ピン | 置かれた DLL | Player 内の md5 |
|---|---|---|
| 1.11.0（`install_as: unity-windows-native-toolkit.dll`） | `unity-windows-native-toolkit.dll`（git 上の変更なし） | `37a460f8...`（1.x） |
| 1.12.0（`install_as` は空。一時的に書き換えて戻した） | `windows-native-toolkit-capi-2.0.0.dll`。1.x の DLL と `.meta` は削除 | `03645af6...`（2.0.0、1 本だけ） |

**development ビルドも確かめた**（2026-09-26、テスト用 Player のビルド）。
`install_as_debug` から `unity-windows-native-toolkit-debug.dll` という名前が選ばれ、
中身は 1.x（md5 `37a460f8...`）だった。09-23 に同じ操作をしたときは 2.0.0 が入っていた。

**確かめていないこと:** 止まる経路。宣言した成果物が dist に無いとき、Unity が実際にビルドを止めるか
（コードを読んだだけ）。移行作業で次にビルドするときに合わせて確かめる。

#### 移行時にやること

**1 本で一括に切り替えず、移行の間は 2 本置く**（Dialog 設計 v6 の J-6。2026-09-27、`3cf4b1b`）。
3 機能を 1 つずつ移すため、移していない機能は 1.x の DLL を読み続ける。

1. 済み: VERSION.txt の主のピンを 1.12.0（`windows-native-toolkit-capi-2.0.0.dll`、`install_as` は空）にし、
   1.x を追加のピン（`extra_dist_version` / `extra_dll` / `extra_install_as` / `extra_install_as_debug`）に移した。
   PreBuildProcessor は両方のコピー元を削除より前に解決し、両方の名前を削除から除き、両方に importer を当てる
2. 済み（2026-09-27〜29）: 機能ごとに、Manager を `WindowsNativeToolkitCApi` と機能の Bridge 経由に書き換えた（Dialog、Notification、Clipboard の順）
3. 済み（2026-09-27、Clipboard の手順 6）: 追加のピンのキーと PreBuildProcessor の処理を消し、1.x の DLL を削除し、VERSION.txt のコメントを直した

これで 2 本置きの期間は終わった。VERSION.txt のピンは `windows-native-toolkit-capi-2.0.0.dll` の 1 本だけで、Player に入る DLL もそれだけ（md5 が dist と一致）

#### 併せて壊れているもの

`Editor/Build/PostBuildProcessor.cs` は development ビルドのとき、
絶対パスで PDB を探してコピーする。

```
C:\Users\User\Desktop\native-toolkit\windows\WindowsLibraryExample\x64\Debug\WindowsLibraryExample\AppX\WindowsLibrary-Debug.pdb
```

**このパスは現在解決しない。** 名前も 1.x のもの（`WindowsLibrary-Debug.pdb`）で、
stage 5 の構成変更より前の前提。ビルドは止まらず `LogError` が出るだけだが、
移行時に一緒に直す対象。

**対応済み（2026-09-27、`3cf4b1b`）。** dist 1.12.0 に PDB は無いので、コピーの処理を消した（Dialog 設計 v6 の J-11）。

## 3. 宣言の置換では済まないもの

**ここが本トピックの中身である。** 以下はいずれも今のコードに概念自体が無い。

### 3.1 ドメインリロード（最優先）

Unity Editor はネイティブ DLL を下ろさない。DLL の中のセッションとマネージャーはドメインリロードとプレイモードの出入りを越えて生き残る一方、C# の static に置いたハンドルは消え、登録した関数ポインタは下ろされたドメインを指す。

- `AssemblyReloadEvents.beforeAssemblyReload`、`EditorApplication.playModeStateChanged`（`ExitingPlayMode`）、`Application.quitting` で後始末する
- Clipboard: **オーナースレッドから** `ntk_clipboard_session_close` → `ntk_clipboard_session_free`。`BUSY` なら原因が終わるのを待って再試行する。`BUSY` になるのは、ほかのスレッドの同期の読み書きだけでなく、**実行中の履歴の要求**と、回復の最中にクリップボードを握られているとき（2026-09-29 訂正。Clipboard 設計 v12 の 1.4。S-2 の `ForceInitializeWhileDraining` では、履歴の取得が走っている間の close が実際に `BUSY` を返した）。**ただし移行した Clipboard（設計 v12）も Editor でネイティブを読まない**（J-4）ので、ドメインリロードの後片付けは要らない
- Notification: `ntk_notification_manager_close` → `_free` の後、**すべての登録の `release` を期限付きで待つ**。
  **ただし移行した Notification（設計 v8）は Editor でネイティブを読まない**（J-4。DLL は importer で Editor 無効、Manager の呼び出しは `!UNITY_EDITOR`）ので、
  ドメインリロードの後片付けは要らない。`release` も使わない（`NULL`。受け口は static で、登録ごとの状態を持たない）
- **close が成功しないまま `_free` すると、その Editor では以後セッションを作れない**（放棄）
- 活性化コールバックは受け取ったものをキューに積むだけにする。メインスレッドへ同期で戻ると、close を待つメインスレッドと相互に待って止まる

### 3.2 STA / MTA の分離

`ntk_notification_manager_create` は**呼び出しスレッドを MTA にしようとする**。クリップボードのオーナーは STA でメッセージループを回し続ける必要がある。

**訂正（2026-09-29）:** 初めは「同じスレッドを両方のオーナーにできない」と書いていたが、**先に STA で初期化すれば兼ねられる。**MTA にする試みは `RPC_E_CHANGED_MODE` で失敗し、スレッドは STA のままで、`manager_create` はそれを受け入れる。Unity のメインスレッドは既に STA（MAINSTA、実測）なので、移行後は Clipboard と Notification をどちらもメインスレッドで使っている（Clipboard 設計 v12 の J-11、Notification 設計 v8 の 1.5）。3 機能の Player テストを同じ Player で流して確かめた

### 3.3 マーシャリングの落とし穴

- **文字列を返す関数に `[return: MarshalAs(UnmanagedType.LPUTF8Str)]` を付けない。** マーシャラーが戻りポインタを `CoTaskMemFree` で解放してヒープを壊す。`IntPtr` で受けて自分で複製する。引数側の `LPUTF8Str` は可
- 借りたポインタ（`ntk_string_data` など）はそのハンドルを `_free` するまで有効。コールバックが受け取ったハンドルは**そのコールバックから戻るまで**
- **登録ごとに `GCHandle` を 1 つ**作り、`release` で `Free` する。同じ `user_data` の値で登録し直すときも別の所有権を渡す。`release` は登録が失敗したときも必ず 1 回呼ばれる（呼び出しスレッドで、戻る前に）。
  **訂正（2026-09-29、Clipboard 設計 v12 の 1.5）:** Clipboard の予約（`reserve_deferred`）の失敗のうち `PARTIAL_STATE` では登録が残るので、そのときは呼ばれない。予約の `release` はその後、次の予約・このセッションの書き込み・ほかのプロセスの消去・recover の成功・close の成功・free のどれかで 1 回呼ばれ、書き込みが契機のときは呼び出しスレッドではなくオーナーで呼ばれうる。なお移行した 3 機能は `user_data` / `release` を使っていない（`NULL`。受け口は static で、登録ごとの状態を持たない）
- コールバックは `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`（`NTK_CALL` は `__cdecl`）
- 構造体は 0 で埋め、`struct_size` に **`Marshal.SizeOf<T>()`**（`sizeof` ではない）。`reserved` は 0 のまま
- ハンドルは `IntPtr.Zero` で初期化した変数に受け、`finally` で `!= IntPtr.Zero` のときだけ対応する `_free` に渡す。
  **同期で受け取ってその場で解放するハンドルは `SafeHandle` にしない**（2026-09-27 に見直し。
  `agent-rules/coding-rules/common.md`「Unity Bridge パターン > Windows」）。寿命が呼び出しをまたぐもの
  （Clipboard のセッション、Notification のマネージャー）の扱いは、それぞれの設計書で決める

### 3.4 意味が反転する引数

0 が「既定」になった結果、C++ API に対して**意味が反転している**フラグが 2 つある。移行時に取り違えると静かに壊れる。

- `allow_missing_file`
- `skip_overwrite_prompt`

## 4. 静かに壊れる 4 件

**コンパイルエラーにならない変更。** 型が合ってしまうため、テストで捕まえないと実行時まで抜ける。native-toolkit 側が「Unity への移植で最も刺さる」と名指しした 4 件。

| # | 変更 | 症状 |
|---|---|---|
| S-1 | 通知のタイムスタンプが **Unix 秒 → Unix ミリ秒** | 値は通るが時刻が 1000 倍ずれる |
| S-2 | 履歴のタイムスタンプが **WinRT ticks の十進文字列 → `int64` Unix ミリ秒**（E-14） | 文字列パースを残すと壊れ、型を合わせても基準が違う |
| S-3 | **2 回目の `init` が成功ではなく `NOT_SUPPORTED`** | リロード時に再 init する実装がすべて破綻する。ハンドラ差し替えは `ntk_notification_manager_set_invoked_handler` を使う |
| S-4 | `reserveDeferredFormats` の `release` が `user_data` の解放を持つ | **エラー戻り時に自分で解放すると二重解放。** 失敗しても `release` は必ず呼ばれる（`PARTIAL_STATE` は除く。登録が残り、後で 1 回呼ばれる。3.3 の訂正） |

### 手動確認では捕まらない

**この 4 件は、人がサンプルを触って見つけられるものではない。** タイムスタンプが 1000 倍ずれても、
画面上はもっともらしい数字に見える。したがって**自動テストは移行の後で入れるものではなく、
移行を検証する手段そのものである**（[cross-platform-testing](../cross-platform-testing/README.md)）。

| 項目 | 捕まえる層 | 理由 |
|---|---|---|
| S-1 / S-2 タイムスタンプ | 層 2b | 境界値を固定する |
| S-3 2 回目の `init` | **層 2b のみ** | Player 上でしか再現しない。Editor 内では出ない |
| S-4 `release` の二重解放 | **層 2b のみ** | Player 上。IL2CPP で顕在化する |
| 文字列マーシャリングの破損 | **層 3 のみ** | `Copy` → `Paste` の往復は**両側が同じように壊れていても通る**。実際のクリップボードを外から読むしかない |

最後の行が効く。今回は UTF-16 から UTF-8 への変更なので、自前の往復テストでは原理的に取り切れない。

## 5. 47 本の対応

設計 8.3 より。**リネームのみ 9 本、振る舞いが変わる 38 本**（2026-09-29 訂正。`getPreferredClipboardFormat` を 5.1 から 5.2 に移した。2.1 の訂正）。

### 5.1 リネームのみ（9）

| 旧 | 新 |
|---|---|
| `canDestroyClipboardManager` | `ntk_clipboard_session_can_close` |
| `clearClipboard` | `ntk_clipboard_clear` |
| `recoverDeferredState` | `ntk_clipboard_recover_deferred_state` |
| `cancelScheduledNotification` | `ntk_notification_cancel_scheduled` |
| `removeNotificationById` | `ntk_notification_remove_by_id` |
| `removeNotificationsByTag` | `ntk_notification_remove_by_tag` |
| `removeAllNotifications` | `ntk_notification_remove_all` |
| `openNotificationSettings` | `ntk_notification_open_settings` |
| `setBadge` | `ntk_notification_set_badge` |

### 5.2 Clipboard・振る舞いが変わる（24）

| 旧 | 新 | 変わること |
|---|---|---|
| `initClipboardManager` | `ntk_clipboard_session_create` | 同じスレッドからの 2 回目は成功ではなく `NOT_SUPPORTED`（S-3） |
| `uninitClipboardManager` | `ntk_clipboard_session_close` | `BOOL` + `pError` ではなくエラーのみ。成功後に `_free`。要求が飛んでいてもポンプせずに閉じる（E-19）。待っている要求は close の中で `CANCELED` として配送する。移行した Manager は、`BUSY` / `PARTIAL_STATE` / `CANCELED` / `MONITOR_REGISTER_FAILED` をどれも「まだ終わっていない」として drain で再試行する（Clipboard 設計 v12 の 4.7。2026-09-29 訂正: 初めは「再試行は `BUSY` のときだけ」と書いていた）。`BUSY` はほかのスレッドの同期呼び出し、実行中の履歴の要求、回復の最中にクリップボードを握られているとき（3.1） |
| `copyPlainText` | `ntk_clipboard_copy_text` | 未知のオプションビットは `INVALID_PARAMETER`。`SENSITIVE` は値 3 のまま |
| `copyHtml` | `ntk_clipboard_copy_html` | 同上 |
| `copyImage` | `ntk_clipboard_copy_dib` | 運ぶもの（DIB）に合わせて改名。未知ビットの扱いは同上 |
| `copyFiles` | `ntk_clipboard_copy_files` | 文字列配列。JSON 配列ではない |
| `copyCustomFormat` | `ntk_clipboard_copy_custom` | 未知ビットの扱いは同上 |
| `copyMultipleFormats` | `ntk_clipboard_copy_multiple` | ビルダーに**生バイト**。base64 入り JSON ではない |
| `pastePlainText` | `ntk_clipboard_paste_text` | 採寸パスが消え、1 回の呼び出しで済む |
| `pasteHtml` | `ntk_clipboard_paste_html` | 同上 |
| `pasteImage` | `ntk_clipboard_paste_dib` | 同上 |
| `pasteFiles` | `ntk_clipboard_paste_files` | リストハンドル。JSON ではない |
| `pasteCustomFormat` | `ntk_clipboard_paste_custom` | `ntk_bytes` のハンドル（採寸パスなし）。2026-09-29 訂正: 初めは上の行と「同上」（リストハンドル）と書いていた |
| `hasClipboardFormat` | `ntk_clipboard_has_format` | 結果は戻り値ではなく出力引数 |
| `getClipboardFormats` | `ntk_clipboard_get_formats` | リストハンドル。JSON ではない |
| `getPreferredClipboardFormat` | `ntk_clipboard_get_preferred_format` | 呼び出し側のバッファではなく `ntk_string` のハンドル（2026-09-29 に 5.1 から移した） |
| `getClipboardHistory` | `ntk_clipboard_get_history` | 完了が履歴ハンドルを運ぶ（**そのコールバックの間だけ有効**）。**タイムスタンプが変わる（S-2）。** `NULL` コールバックは `INVALID_PARAMETER` |
| `getClipboardHistoryAvailability` | `ntk_clipboard_get_history_availability` | 完了が bool 2 つを運ぶ。JSON ではない |
| `deleteHistoryItem` | `ntk_clipboard_delete_history_item` | 完了がエラーとシステムコードを運ぶ（`restoreHistoryItem` と同じ `ntk_clipboard_completion_fn`）。2026-09-29 訂正: 初めは上の行と「同上」（bool 2 つ）と書いていた |
| `restoreHistoryItem` | `ntk_clipboard_restore_history_item` | 完了がシステムコードを運ぶ |
| `clearUnpinnedHistory` | `ntk_clipboard_clear_unpinned_history` | 同上 |
| `setClipboardHistoryCallbacks` | `ntk_clipboard_set_history_handlers` | 3 つのコールバックが 1 構造体 + `user_data` に。解除は `NULL` か 3 つとも `NULL`。**失敗時は古いハンドラが残り、新しい `user_data` は登録されない**（E-20） |
| `reserveDeferredFormats` | `ntk_clipboard_reserve_deferred` | provider が「採寸してから書く」2 段階ではなく 1 段階に。`void* context` が `user_data` + `release` に。**エラー戻り時に自分で解放しない（S-4）** |
| `cancelClipboardRequest` | `ntk_clipboard_cancel_request` | `BOOL` + `pError` ではなくエラーのみ |

### 5.3 Notification・振る舞いが変わる（8）

| 旧 | 新 | 変わること |
|---|---|---|
| `initWinAppSdk` | `ntk_notification_runtime_initialize` | ハンドルの解放で、全マネージャーが閉じた時点で `MddBootstrapShutdown` を 1 回呼ぶ。**1.x は一度も shutdown していなかった** |
| `initNotificationManager` | `ntk_notification_manager_create` | 2 回目はハンドラ差し替えではなく `NOT_SUPPORTED`（S-3）。差し替えは `ntk_notification_manager_set_invoked_handler`。`release` で `user_data` の終わりが分かる（E-12） |
| `uninitNotificationManager` | `ntk_notification_manager_close` / `_free` | 戻った後に、飛んでいた活性化が 1 件だけ走ることがある。`user_data` の解放は `release` の**後** |
| `showNotification` | `ntk_notification_show` | ビルダー。JSON ではない。`INVALID_PAYLOAD` は起こり得なくなる。**タイムスタンプが秒からミリ秒へ（S-1）。** 範囲外の scenario / audio kind / logo crop は既定値へ落とさず `INVALID_PARAMETER`。ボタンラベルや入力 id の欠落は `HRESULT_FAILURE` ではなく `INVALID_PARAMETER` |
| `scheduleNotification` | `ntk_notification_schedule` | `showNotification` と同じ。予定時刻は Unix ミリ秒のままだが、`system_clock` の範囲（前後およそ 29,000 年）を超えると `INVALID_PARAMETER` |
| `updateNotificationProgress` | `ntk_notification_update_progress` | 引数 6 個が 1 構造体に |
| `getAllNotifications` | `ntk_notification_get_all` | リストハンドル。**切り詰めが起こらなくなる** |
| `getNotificationSetting` | `ntk_notification_get_setting` | `-1` ではなくエラー。閉じたマネージャーは `NOT_INITIALIZED`、`NULL` ハンドルは `INVALID_PARAMETER` |

**通知のボタンが、パッケージ化しないアプリに届くようになる（2026-09-27 追記）。** 1.x も 2.0.0 も、
`HKCU\Software\Classes\AppUserModelId\<AUMID>\CustomActivator` を書いていなかった。Windows 11 はこの値が無いと、動いているアプリにクリックを届けない。
native-toolkit が `c9f4071b` で直し、dist 1.12.0 に入っている（原因と確かめ方は `artifact/windows/notification/designs/2026-09-27-windows-notification-ui-test-plan-v1.md` 2 章）。
**移行したら、`WindowsNotificationSamplePlayerTests` の W-04（`ShowNotification_OpenInTheCenter_ComesBackAsInvoked`）の `[Ignore]` を外し、W-11 を書く。**
移行の確認はこの 2 本が通ることを含める。表示名（Unity では `Application.productName`）が同じアプリどうしは、クリックを奪い合う。
**1.x には修正版を出さない**（2026-09-27 決定）。移行までは、同梱の 1.x でこの不具合が残る

> **移行済み（2026-09-27）。** W-04 の `[Ignore]` を外し、W-11 を書いた。どちらも 2.0.0 で通った（UI テスト計画 v2）。
> 上の表の違いのうち、公開 API では変わらないもの: **2 回目の `Initialize` は、Manager があればネイティブを呼ばずに成功**（J-3。S-3 は公開 API に出ない）、
> **公開の JSON の `timestamp` は秒のまま**（J-2。C# で 1000 倍する。S-1 は公開 API に出ない）、GetNotificationSetting の失敗は今までどおり `Unknown`。
> 公開 API で変わった振る舞いは、Notification 設計 v8 の 5.4 と実装結果 v1 の「既知の差分」にある

### 5.4 Dialog・振る舞いが変わる（6）

> **移行済み（2026-09-27）。** 下の違いは Manager が公開 API の形に戻すので、公開 API で見える変化は限られる
> （キャンセルは `-1` のまま、複数ファイルは 1.x もフルパス、上書き確認は常に出す、ファイル系の title と owner は使わない）。
> 公開 API で変わった振る舞いは、Dialog 設計 v6 の 5.3 と実装結果 v2 の「既知の差分」にある。

| 旧 | 新 | 変わること |
|---|---|---|
| `showAlertDialog` | `ntk_dialog_show_alert` | `MB_*` の 4 グループが列挙型になり、他の `MB_*` は渡せない（E-2）。結果は `IDOK` 等ではなく `ntk_dialog_alert_result`。**owner が効くようになった**（E-13） |
| `showFileDialog` | `ntk_dialog_show_open_file` | キャンセルは `*pError == -1` ではなく `CANCELED`。失敗は生の OS 値ではなく `SYSTEM_ERROR` + `ntk_last_system_code()`。フィルタは NUL 区切りの 1 本ではなく name/pattern の配列。**title / `allow_missing_file` / owner が効くようになった**。パス長は終端込み 1024、超過は `SYSTEM_ERROR`（1.x は呼び出し側がバッファを選んでいた） |
| `showMultiFileDialog` | `ntk_dialog_show_open_files` | フォルダ + 名前の並びではなく**フルパス**。フォルダは件数に数えない。全体で 32768 文字 |
| `showFolderDialog` | `ntk_dialog_show_pick_folder` | キャンセルは `TRUE` + `pError == -1` ではなく `CANCELED`。終端込み 1024 |
| `showMultiFolderDialog` | `ntk_dialog_show_pick_folders` | キャンセルは 0 件や `-1` ではなく `CANCELED`。**「成功したが未選択」と「キャンセル」がようやく区別できる**。全体で 32768 |
| `showSaveFileDialog` | `ntk_dialog_show_save_file` | `skip_overwrite_prompt` で上書き確認を止められる（1.x は常に確認した）。**title / owner が効くようになった**。終端込み 1024 |

## 6. 未確認・未決

| # | 内容 | 状態 |
|---|---|---|
| Q-1 | 関数の数 | **解決（2026-09-23）。** 105。`.def` が 105 行で、これがリンカの export。114 は typedef 9 本を含めた数 |
| Q-2 | JSON 依存層の廃止範囲 | **解決（2026-09-23）。** 5 ファイルすべて廃止。base64 ヘルパーと truncation 処理も道連れ。残る JSON は `raw_arguments` のみ（2.2） |
| Q-3 | 47 本の対応と未使用 5 本 | **解決（2026-09-23）。** 5 章。未使用 5 本は E-6 で廃止、移植不要 |
| Q-4 | サンプルシーンの作り直しが要るか。要るなら `design-sample-scene` へ別途切り出す | **解決（2026-09-27）。作り直さない。** Manager の公開 API を保ち、中の P/Invoke 層だけを 2.0.0 に置き換える。サンプルの UI 自動テストがそのまま移行の検証になる。2.0.0 で意味が変わる戻り値（ダイアログのキャンセルなど）を公開 API にどう出すかは、設計書で 1 つずつ決める。**例外（2026-09-27）: Windows の型の名前空間を `JonghyunKim.NativeToolkit.Runtime.<機能>` から `JonghyunKim.NativeToolkit.Runtime.Windows.<機能>` に変え、ファイルを `Runtime/Windows/<機能>/` に移す。**利用者がまだいないため、ここで揃える（`agent-rules/coding-rules/common.md`「命名」）。Android / iOS / macOS は、それぞれの OS の対応のときに移す |
| Q-5 | NuGet の `NativeToolkit.CApi 2.0.0` を使うか、DLL を直接同梱するか | **解決（2026-09-27）。DLL を直接同梱する。** Unity は NuGet をそのまま使えない。今と同じく PreBuildProcessor が dist からコピーし、`windows-native-toolkit-capi-2.0.0.dll` を dist の名前のまま使う（`Plugins/Windows/VERSION.txt` のコメント） |
| Q-6 | チケット番号（`UNT-13` 以降）を採番する | **解決（2026-09-27）。新しく採番しない。** UI 自動化と同じ `feature/UNT-12` で続ける。PR は大きくなるが、コミットは内容ごとに分かれている |

6 件とも解決。

## 7. 参照

native-toolkit のパス（このマシン上）。**`feature/NTKIT-16` は develop 未マージ、1.12.0 未リリース**（2026-09-23 時点、HEAD `72d83fce`）。

| 見るもの | 場所 |
|---|---|
| こちらへの申し送り | `artifact/topics/windows-architecture/results/2026-09-23-windows-architecture-stage5-result.md` 2 章 |
| 52 関数の対応表（5 章の出典） | `artifact/topics/windows-architecture/designs/2026-09-21-windows-architecture-c-abi-design.md` 8.3 |
| スレッドと寿命の契約 | 同 1.3（Unity 固有は 1.3.4、STA/MTA は 1.3.3） |
| 受け渡しの 3 方式 | 同 1.2 |
| 21 の決定（E-1〜E-21） | 同 4.2 |
| 公開ヘッダー | `windows/WindowsLibraryCApi/include/NativeToolkitC/{Common,Dialog,Notification,Clipboard}.h` |
| **export の正本** | `windows/WindowsLibraryCApi/WindowsLibraryCApi.def`（105 行） |
| 整合検査 | `python scripts/check_c_abi_contract.py`。`.def`・ヘッダー・設計 8.2・付録 A の 4 つを相互照合する。ツールチェーン不要 |
| 全 105 関数の C 実例 | `manual/1.12.0/{dialog,notification,clipboard}.md` の「C ABI」節。コンパイル検査済み |
| 配布物 | `dist/1.12.0/windows/`（`-capi-` が C ABI。`-capi-` の付かない方は C++ の静的ライブラリで、**こちらではない**） |
