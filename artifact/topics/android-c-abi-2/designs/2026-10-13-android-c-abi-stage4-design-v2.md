# Android の C ABI 2.0.0 への移行の段 4（旧ブリッジの撤去、改名、文書、1.13.0）v2

## 基本情報

| 項目 | 内容 |
|---|---|
| 親課題 | `artifact/topics/android-c-abi-2/README.md`（5 章の段 4、4 章の J-10〜J-12） |
| 改名の課題 | `artifact/topics/os-prefix-violations/README.md`（案 1。2026-10-12 の決定: 1.13.0 で一括改名、破壊的変更として CHANGELOG と移行ガイドに書く） |
| 前提 | 段 3 が終わった（Dialog・Share・Clipboard・Notification の 4 機能がすべて C ABI を呼ぶ。`cb428dd`）。1.x のブリッジを呼ぶ実行時のコードは無い（2026-10-13 に `Runtime/` を `AndroidJavaObject` / `AndroidJavaProxy` / `android.unity.` で検索して確かめた。残るのは Clipboard のサンプルが FileProvider を引く `AndroidJavaObject` だけで、1.x のブリッジではない） |
| 出す版 | 1.13.0（メジャーにしない。改名とビルダーの削除は破壊的変更として書く） |
| native-toolkit | dist 1.13.0（`android-native-toolkit-2.0.0.aar`、`android-native-toolkit-capi-2.0.0.aar`）。**native-toolkit の 1.13.0 のタグはまだ無い**（2026-10-13。手元の最新は `49b99bc4 build(dist): create dist/1.13.0 ...`） |

## v1 からの主な変更（v2。2026-10-13）

- 4 章の 3 つを利用者が決めた: `ClipboardReadStatus` と `ClipDescriptionInfo` も改名する（Q-1）、リリースのチェックリストに Android の AAR の照合を足す（Q-2）、S4-1〜S4-4 を先に進め、S4-5 だけを native-toolkit の 1.13.0 のタグまで待つ（Q-3）
- v1 の「要確認」3 つを確かめて決めた: `PreBuildProcessor` の Android の `extra_*` の扱いを Windows と同じ形にする（1.1）、ほかの OS のファイルの説明文は変えない（1.2）、`MacIconConfiguration` の名前空間は `JonghyunKim.NativeToolkit.Runtime.Dialog` になる（1.3）

## 1. 何をするか

| # | 中身 | 破壊的変更 |
|---|---|---|
| S4-1 | 旧ブリッジの AAR（1.3.0 の 2 本）を外す | なし（公開 API は変わらない。APK から 1.x のクラスが消える） |
| S4-2 | 使われていない JSON の層を消す（J-12） | あり（`AndroidShareJsonBuilder`、`AndroidClipboardJsonBuilder`） |
| S4-3 | 接頭辞の無い Runtime の型を改名する（os-prefix-violations の案 1） | あり（下の表の 15 型） |
| S4-4 | マニュアル（1.13.0、英・日・韓）、移行ガイド、API の docs、README | なし |
| S4-5 | 1.13.0 のリリース（`agent-rules/workflows/release`） | - |

### 1.1 S4-1: 旧ブリッジの AAR を外す

- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/VERSION.txt` から `extra_dist_version` と `extra_aars` を消す
- `Plugins/Android/` の `android-native-toolkit-1.3.0.aar` と `unity-android-native-toolkit-1.3.0.aar`（と `.meta`）を消す。パッケージの利用者の条件（Test Android™ Profile）では、このフォルダにコミットした AAR がそのまま使われるので、消すのはコミットで行う（`PreBuildProcessor` はビルドのときに、ピンに無い AAR をこのフォルダから消すが、それは Android™ Profile の実行だけ）
- `PreBuildProcessor` の Android のピン（`AndroidPin`）から、追加の組（`ExtraDistVersion`、`ExtraAars`）と、それを写す処理を消す。残った `extra_*` のキーは無視する（v2。Windows と同じ形: `WindowsPin` は 1.x の DLL の追加のピンを最後の機能の移行で外し、残ったキーを無視する。`PreBuildProcessor.cs:100-105`）。今の Android の読み方は `extra_*` を省略でき、片方だけのときにビルドを止める（`PreBuildProcessor.cs:211-227`）。XML コメントの「移行の間の追加の組」の段落（`:186-188`）も Windows と同じ形に直す。ピンの読み方の EditMode のテストがあれば、追加の組のテストを、残ったキーを無視するテストに置き換える
- 2.0.0 の AAR の `LegacyScheduleCleaner`（1.x の予約を起動時に 1 回だけ消す）は 2.0.0 の側にあるので残る（移行ガイドに書く。Notification の J-N19）
- 確かめ: 通しの検証（Android™ Profile と `--as-package-user`）が PASS。APK に 1.x のクラス（`android.unity.*`、`android.library.*`）が入っていないこと（Gradle が出す APK を `unzip` して、`classes*.dex` を `dexdump` か文字列で調べる。V-S4-1）

### 1.2 S4-2: 使われていない JSON の層を消す（J-12。利用者の判断、2026-10-13）

| 消す | 理由 |
|---|---|
| `Runtime/Android/Share/AndroidShareJsonBuilder.cs`、`Tests/Runtime/AndroidShareJsonBuilderTests.cs` | Share の Manager は Payload を受け、C ABI の移行の後はどこからも使われていない |
| `Runtime/Android/Clipboard/AndroidClipboardJsonBuilder.cs`、`Tests/Runtime/AndroidClipboardJsonBuilderTests.cs` | 同上（Clipboard） |

- **残す**: `AndroidNotificationJsonBuilder`。Notification の公開 API は JSON の文字列を受け、このビルダーが利用者がその JSON を組む道具（サンプルも使う）。Notification の設計 v4 の 5.3 の最後の行と J-N17 の「段 4 で消す」は J-12 で置き換えた
- 消した型を名前で書いている Android の XML コメント（`AndroidClipboardCApi.cs:103`）を直す。**ほかの OS のファイル**（`IosShareJsonBuilder.cs:12`、`WindowsClipboardJsonBuilder.cs:18`）は `<c>` の地の文で触れているだけ（リンクではない）ので、ほかの OS のファイルを変えない規則（`common.md` の P2）を優先して残す（v2）
- Payload の型（`Android*Payload`）は Manager の公開 API なので残す

### 1.3 S4-3: 改名（os-prefix-violations の案 1）

| 今 | 改名後 | 置き場所 |
|---|---|---|
| `ClipboardOperationResult` | `AndroidClipboardOperationResult` | `Runtime/Android/Clipboard/`（変えない） |
| `ClipboardReadResult` | `AndroidClipboardReadResult` | 同上 |
| `ClipboardReadStatus`（同じファイルの列挙型。案 1 の表に無い） | `AndroidClipboardReadStatus` | 同上 |
| `ClipItem`、`ClipContents`（同じファイル） | `AndroidClipItem`、`AndroidClipContents` | 同上 |
| `ClipboardDescriptionResult` | `AndroidClipboardDescriptionResult` | 同上 |
| `ClipDescriptionInfo`（同じファイル。案 1 の表に無い） | `AndroidClipDescriptionInfo` | 同上 |
| `NotificationResult`、`NotificationActionResult`、`NotificationReceivedResult` | `AndroidNotificationResult`、`AndroidNotificationActionResult`、`AndroidNotificationReceivedResult` | `Runtime/Android/Notification/` |
| `ShareOperationResult`、`ShareCallbackResult`、`ShareChooserActionResult`、`ShareChooserActionCallbackCoordinator` | `AndroidShareOperationResult`、`AndroidShareCallbackResult`、`AndroidShareChooserActionResult`、`AndroidShareChooserActionCallbackCoordinator` | `Runtime/Android/Share/` |
| `IconConfiguration`（入れ子の列挙型 `IconType`、`RenderingMode`、`Weight`、`Scale` はそのまま中に残る） | `MacIconConfiguration` | `Runtime/Common/` → `Runtime/Dialog/`（`MacDialogManager` と同じフォルダ）。**名前空間も `JonghyunKim.NativeToolkit.Runtime.Common` から `JonghyunKim.NativeToolkit.Runtime.Dialog` に変わる**（`MacDialogManager.cs:4`。v2）。macOS の利用者にも破壊的変更なので、移行ガイドの改名の表に名前空間も書く |

- 案 1 の表に無い 2 つ（`ClipboardReadStatus`、`ClipDescriptionInfo`）も、同じファイルにある Android の Clipboard 専用の public 型なので、同じ規則（`common.md` の「命名: OS 接頭辞」）で改名する（利用者の判断、2026-10-13。Q-1）
- ファイルの名前も型に合わせる。`git mv` で `.cs` と `.cs.meta` を対で動かし、GUID を保つ
- `[Obsolete]` の別名は置かない（C# には型の別名を公開する手段が無く、構造体は継げない。案 1 の「1 版残す」は使わない）。CHANGELOG と移行ガイドに、旧名 → 新名の表を載せる
- 参照の置き換え: Runtime、サンプル（`Runtime/UI/`）、テスト、`agent-rules`（`common.md` の「既知の逸脱」の表を消す、`testing.md` の表）、`os-prefix-violations/README.md`（案 1 を「完了」に）
- 確かめ: 置き換えの漏れはコンパイルで分かる（iOS・macOS・Windows の型にも触れるので、**4 つの build target でコンパイルする**。`IconConfiguration` は macOS だけで、Android の検証のスクリプトでは見えない。V-S4-2）

### 1.4 S4-4: 文書

`agent-rules/workflows/write-manual` と `verify-manual` に従う。

- **マニュアル**（`manual/1.13.0/`、英・日・韓）: Android の動作環境に「ARM64（と x86_64）だけ、IL2CPP だけ」（J-10）。4 機能の既知の差分（各設計の 5.3。Notification は実装結果 v2 の 3.4 も）。Notification の `tools:keep`（J-N6）、`persistAcrossBoot` の予約だけが `IsNotificationScheduled` に見えること
- **移行ガイド**（1.12.0 → 1.13.0）: 改名の表（S4-3）、消したビルダー（S4-2）、名前空間（段 3-0 でフォルダと名前空間を `Runtime/Android/<Feature>/` に移した。J-4、J-8）、既知の差分、「Configure Gradle Project」の削除、32 ビットのビルドが止まること（J-5）、1.x が保存した予約と 1.x が出した通知の扱い（J-N19、Notification の 5.3）、通知の権限が無いときの失敗（J-N7）
- **API の docs**（`docs/1.13.0/`。`Documentation~/build-docs.sh` と `scripts/publish_docs.sh`）
- **README**（英・日・韓）の機能と動作環境
- `package.json` の版、`description` / `keywords`

### 1.5 S4-5: リリース

`agent-rules/workflows/release/workflow.md` に従う。**前提**: native-toolkit の 1.13.0 がタグになっていること（`Plugins/Android/VERSION.txt` の `dist_version: 1.13.0` が指すもの）。S4-1〜S4-4 は先に進め、この段だけをタグまで待つ（利用者の判断、2026-10-13。Q-3）。

- **リリースのチェックリストに Android の AAR の照合の行を足す**（利用者の判断、2026-10-13。Q-2）: `Plugins/Android/VERSION.txt` の `dist_version` を `<dv>`、`aars` の 2 本を `<aar>` として、同梱の `Plugins/Android/<aar>` の md5 が、native-toolkit のタグ `<dv>` の `dist/<dv>/android/<aar>` と一致すること。タグが無ければ ❌（Windows の DLL の行と同じ形）。dist の中の Android の AAR の置き場所（`dist/<dv>/android/` か）は、足すときに native-toolkit の dist で確かめる

## 2. 順とコミット

1. S4-1（AAR）。通しの検証（`--as-package-user` も）。1 コミット
2. S4-2（ビルダー）。通しの検証。1 コミット
3. S4-3（改名）。通しの検証と、4 つの build target のコンパイル。1 コミット（参照の置き換えを分けると、途中のコミットがコンパイルできない）
4. S4-4（文書）。`verify_manual.sh` が停止項目 0 件。文書ごとにコミット
5. S4-5（リリース）。native-toolkit の 1.13.0 のタグの後

各段の実装の後に、実装のレビュー（サブエージェントと Codex）を回す。S4-1〜S4-3 は機械的な変更が多いので、1 回のレビューにまとめてもよい。

## 3. 要検証

| # | 内容 | 確かめる時点 |
|---|---|---|
| V-S4-1 | 1.3.0 の 2 本を外した APK に、1.x のクラス（`android.unity.*`、`android.library.*`）が入っていないこと。2.0.0 の AAR だけで、実機の全テストが通ること | S4-1 |
| V-S4-2 | 改名の後、Android・iOS・macOS・Windows の 4 つの build target で、Runtime とテストとサンプルがコンパイルできること | S4-3 |
| V-S4-3 | `PreBuildProcessor` の Android のピンが、`extra_*` の無い `VERSION.txt` で、2 本だけを置くこと（Android™ Profile の実行の後、`Plugins/Android/` に 2 本と androidlib だけが残る） | S4-1 |

## 4. 利用者の判断（2026-10-13）

| # | 事柄 | 決定 |
|---|---|---|
| Q-1 | 案 1 の表に無い `ClipboardReadStatus` と `ClipDescriptionInfo` も改名するか | 改名する（同じファイルの Android 専用の public 型で、残すと接頭辞の規則の逸脱が 2 つ残る） |
| Q-2 | リリースのチェックリストに、Android の AAR の照合（md5 と native-toolkit のタグ）を足すか | 足す（Windows の DLL と同じ理由。同梱のバイナリとタグのずれを止める） |
| Q-3 | native-toolkit の 1.13.0 のタグを待つか | S4-1〜S4-4 を先に進め、S4-5 だけを待つ |
