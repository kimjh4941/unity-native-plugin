# レビュー結果

- 日付: 2026-10-12
- 対象ファイル: artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v2.md
- 機能名: clipboard
- プラットフォーム: android
- レビュアー: サブエージェント（ラウンド 2。前のラウンドとは別のインスタンス）と Codex を並行で回した。どちらも v1 との差分を中心に、読むだけ。1 章はサブエージェントの出力、2 章は Codex の出力（どちらも手を加えていない）
- A1: 両方とも 0 件。サブエージェントは R2-1 を「判断待ち」（利用者が J-11 の例外として受け入れれば C、保つなら A1）とした

---

## 1. サブエージェント（Claude）

### 範囲

- 対象は `/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v2.md`。読むだけで、どのファイルも変えていない。
- 進め方
  - `diff` で v1 → v2 の変更（129 行）を見た。
  - レビュー v1 の 19 件（R1-1〜R1-12、X1-1〜X1-7）について、直ったかを判定した。
  - v2 の全文（488 行）を一次資料と照らした。
- 照らした一次資料
  - 規則: `agent-rules/workflows/review-document/workflow.md`（手順 4・8）、`common.md`、`testing.md`、親課題の README（J-9、J-11、A-4）
  - 今の C#: Manager、`AndroidNativeToolkitCApi.cs`、`AndroidShareManager.cs`、`AndroidClipboardManagerIntegrationTests.cs`、`WindowsClipboardPlayerTests.cs`（`extern` の結び付けの前例）
  - native-toolkit（2.0.0）: `Clipboard.h`、`Clipboard.cpp`、`ClipboardBridge.kt`、`Errors.kt`、`MainPoster.kt`、`MainTasks.kt`、`Registry.cpp`、`ClipboardObserver.kt`、`ClipboardChangeMonitor.kt`、`ClipboardTest.cpp`、C ABI の設計の第 1 部 491 行と第 2 部 AP-17
  - 1.x（タグ 1.12.0）: `UnityAndroidClipboardManager.kt`、`ClipboardRepositoryImpl.kt`
- 機械照合 `python3 scripts/check_design_consistency.py` は全項目が OK か SKIP だった。
- v2 で新しく引いた行番号は、すべて一次資料どおりだった。
  - `ClipboardBridge.kt:161-171,40-52`、`Errors.kt:26-32`、`ClipboardChangeMonitor.kt:37-52`
  - `ClipboardTest.cpp:311-312`、`OldBridge:262-265,297-301,303-311,363-365`、`Mgr:173,188,202,217,295-299`

### v1 の指摘の判定表

| ID | 判定 | 根拠・残り |
|---|---|---|
| R1-1 / X1-1 | **部分** | 次は直った: 5.3 と J-C11 に差分を書いた。AC-21 を 21a〜c に分けた。URI で「ちょうど 1 回」を確かめる。<br>残り: 保たない理由「C# からは待つしかない」が正確でない。5.3 の「同じフレーム」と「0.5 秒待てば届く」は、保証していないことを約束している。J-11 の例外として記録していない（R2-1、R2-2） |
| R1-2 / X1-3 | 解決 | 5.4 の 326 行と 7.1 の `AsPassed` の要素のテスト |
| R1-3 | 解決 | 4.4 の 227〜229 行。ただし同じ種類の穴が非同期の `READ_NOT_ALLOWED` に残る（R2-10） |
| R1-4 | 解決 | AC-31。手順の細部は A2（R2-5） |
| R1-5 | 解決 | AC-32 |
| R1-6 | 解決 | AC-17・AC-18 に `Read` を足し、AC-26 に `plainText` を足した |
| R1-7 / X1-7 | 解決 | 5.3 の logcat の行。1.x の `ClipboardRepositoryImpl.kt:63-74` と 2.0.0 の `AndroidClipboardManager.kt:41,51,61,71` に合う |
| R1-8 | 解決 | 1.1 の 50 行、4.4、5.3 |
| R1-9 | 解決 | 5.3 の監視の共有の行 |
| R1-10 | 解決 | J-C16 で `(int)state + 1` にした。5.1 の行は「未確認を 0」のままだが、矛盾はしない |
| R1-11 | 解決 | 5.1 の Mgr の行と、`ClipboardReadResult.cs` の行 |
| R1-12 | **部分** | `HasClip` の `LogWarning` と F-7 の言い回しは直った。common.md に「null の要素は `""` にする」と一言書く件は、5.1 の common.md の行に無い（R2-11） |
| X1-2 | 解決 | 5.3 の 290 行。同期の読み取りでのメモリ不足の差が抜けている（R2-9、C） |
| X1-4 | 解決（要検証あり） | AC-33。IL2CPP で `DllImportAttribute` を反射で読めるか分からない。`internal static extern` の照合が落ちている（R2-6） |
| X1-5 | **部分** | 二重登録は、実機の AC-21a（ちょうど 1 回）で見つけられる。<br>残り: 7.2 の記述が自分と食い違う。`Initialize` / `StartObserving` が `EnsureChangeListener` を呼ぶこと（配線）と、失敗の後の再試行は、どの層でも確かめていない（R2-3） |
| X1-6 | 解決 | 7.2 の `OnDestroy` の 4 つの組と、`StopIfObserving` |

### 新しい指摘

#### 判断待ち（A1 の候補。利用者の判断で区分が決まる）

**R2-1 / 判断待ち / medium: 監視を始めた直後の変更を保たない理由が誤っている。親課題の J-11（ふつうの使い方を保つ）の例外として記録されていない**

- 該当
  - 5.2 の J-C11（274 行）、5.3 の 289 行、5.1 の README の行（256 行）
  - 基本情報の「保つ範囲」（11 行）
- 根拠
  - 「`StartObserving` を呼び、同じ流れでコピーする」は、UnityMain から 1 回ずつ正しい入力で呼ぶふつうの使い方に当たる。J-11 はこれを保つとしている（親課題 README の 70 行。設計書 11 行も同じ）。v2 自身も「保たずに」と書いているので、J-11 の例外になる。
  - しかし 5.1 の README の行には、J-9 の届け先と段 3 の状態しか無い。Share が J-9 の例外を README に記録したのと同じ扱いになっていない。
  - 理由の「C# から順を保つには Android の main の完了を待つしかない」は正確でない。待たずに順を知る手段がある。
    - native-toolkit は、main で走らせる仕事を 1 つの Handler にすべて積み、積んだ順に走らせる（`MainPoster.kt:9-13`、C ABI の設計の第 1 部 491 行、第 2 部 AP-17）。
    - `ntk_clipboard_start_observing` の後に、仮のリスナーを `add` してすぐ `remove` する。すると、その `release` は main で、監視を始める処理より後に届く（`Clipboard.h:5-7`、`Registry.cpp:135-152` の `PostRemove`）。
    - C# は、この `release` を「監視が始まった」柵として使える。その間に呼ばれたコピーは取り出し口で後に回せば、1.x の順を待たずに再現できる。
  - **要検証**: この順序は native-toolkit の内部の設計にある性質で、`Clipboard.h` の約束ではない。
- 提案（どちらかを利用者が決める）
  - (a) 差分として受け入れる
    - J-11 の例外として、親課題の README（J-11 の行）と 5.1 の README の行に書く。
    - J-C11 の理由を「待たずに順を保つ手段（`release` の柵）はあるが、native-toolkit の内部の順序に頼り、コピーを後に回す仕組みが要るので採らない」に直す。
    - こう決めれば、この件は C に下がる。
  - (b) 保つ
    - 上の柵を J-C11 に入れる。
    - AC-21b を 1.x と同じ「ちょうど 1 回」で落とすテストにする。
  - 影響の小ささ（サンプルは開始とコピーが別のボタン）と、仕組みの複雑さを比べると、(a) を勧める。ただし、実装に入る前に決めておく必要がある。

#### A1（実装フェーズで気づけない）

- 確定したものは 0 件。

#### A2（実装フェーズで気づける）

- **R2-4 / A2 / low: テスト用の継ぎ目を戻す範囲が決まっていない**
  - 5.1 の 247 行は、`ResetStaticStateForTests` が戻すものを「監視の印」としか書いていない。
  - 7.2 の「失敗の後、次の呼び出しで再試行」は、登録済みの印と差し替えた関数が、テストごとに戻っている前提で成り立つ。戻らなければ、テストの順で結果が変わる。
  - Editor での、登録する関数の既定も書いていない（F-10 の既定だけが「何もしない」と書いてある）。
  - 提案
    - Reset は、差し替えた関数と登録済みの印も戻すと書く。Player では `SubsystemRegistration` が登録より前に走るので害は無い。
    - 層 2b からは Reset を呼ばないと書く。呼ぶと登録が 2 つになり、AC-21a が 2 回になる。
    - `OnDestroy` の中で F-10 が投げた例外は、握ってログにすると書く。1.x は `LogWarning` だった（`Mgr:545-559`）。
- **R2-5 / A2 / low: AC-31 の手順の細部**
  - `Destroy` はフレームの終わりまで遅れる。同じフレームの `Instance` は、壊れかけの Manager を返す。1 フレーム待つ必要がある。
  - SetUp と TearDown は、最初の `Instance` に購読して、`Instance` から外す（`AndroidClipboardManagerIntegrationTests.cs:53,69-80`）。作り直した後の Manager には購読していないので、後片付けで待つ結果が届かず、時間切れで落ちる。
  - AC-32 の `ntk_clipboard_stop_observing()` は `finally` で呼ぶ。そうしないと、失敗したときに共有の監視が残る。
  - どれもテストが落ちるので気づける。実装結果のチェックリストに書く。
- **R2-10 / A2 / low: 非同期の `READ_NOT_ALLOWED` の文言が決まっていない**
  - 4.4 の 231 行は「（起きない）」のままで、文言が未定義。
  - 7.1 は「6 つの操作の、0〜11 の全値」を固めるので、実装の時点で決まる。
  - 1.x の `errorInfoOf`（`OldBridge:330-331`）と同じ文言にするのが自然。

#### B（検証手段）

- **R2-3 / B / low: 7.2 のリスナーの登録のテストが自分と食い違う。配線を確かめていない**
  - 7.2 の 414 行は「`Initialize()` を 2 回、`StartObserving` を 2 回呼んでも登録は 1 回。失敗すると次の `Initialize()` でもう一度呼ばれる」と書いている。
  - しかし Editor では、どちらも登録より前で戻る（J-C9「Editor では登録しない」、`Mgr:130-134`、5.4 の 342 行）。書いてあるとおりに作ると、登録は 0 回になる。同じ行の末尾は「判断の関数を直接呼ぶ」としている。
  - 直接呼ぶ形では、`EnsureChangeListener` そのものしか確かめられない。
    - 確かめられないもの: `Initialize` / `StartObserving` がそれを呼ぶこと、失敗の後に再試行すること。
    - 実機でも、`StartObserving` の中の登録は、`Awake` で登録済みなので通らない。
  - 提案
    - テストの記述を「`EnsureChangeListener` を 2 回で 1 回。失敗の後の次の呼び出しで再試行」に直す。
    - 配線は E-16 と同じくコードレビューで押さえる、と明記する。
- **R2-6 / B / low（要検証）: AC-33 の反射の照合**
  - 層 2b に置く判断そのものは妥当。`DllImport` は `UNITY_ANDROID && !UNITY_EDITOR` に置く規則があり、Windows の前例（`WindowsClipboardPlayerTests.cs:530-554`）も Player に置いている。
  - **要検証**: `DllImportAttribute` は擬似的なカスタム属性。IL2CPP の Player で `GetCustomAttribute` が null を返すおそれがある。null で黙って通らないように、null なら失敗させるか、`Inconclusive` にする。
  - 期待の表は 4.1 の写しで、ヘッダーとは照らしていない。4.1 そのものはヘッダーと一致していることを確かめた。
  - X1-4 が求めた `internal static extern` が落ちている。
  - 案: 層 1 で `AndroidClipboardCApi.cs` の文面と `Clipboard.h` を照らす。native-toolkit の `check_c_abi_contract_android.py` と同じ形で、Editor で、ヘッダーそのものと比べられる。

#### C（記述の整合）

- **R2-2 / C / medium: 監視の開始の差分の書き方が、保証していないことを約束している。J-C11 と 5.3 で言い方が違う**
  - J-C11 は「同じ流れのすぐ後」、5.3 は「同じフレーム」「待ってから（0.5 秒）なら届く」と書いている。
  - 競合の相手は Android の main の looper で、フレームの区切りとは関係しない（`ClipboardBridge.kt:161-171`）。次のフレームでも、0.5 秒後でも、届く保証は無い。
  - この文が XML コメントと移行ガイドにそのまま写ると、利用者への約束になる。
  - 提案: 「Android の main が積んだ処理を走らせるまで（時間の保証は無い）」に揃える。0.5 秒は AC-21a の余裕にすぎない、と書く。
- **R2-7 / C / low: AC-21b の期待の扱いが食い違う**
  - 7.3 の 460 行は、AC-21b を「`OnCApi` で分けて両方の期待を書く」行に入れている。
  - 462 行は「値を記録するだけで落とさない」としている。
  - 1.x で「ちょうど 1 回」として落とすのか、記録だけにするのかを決める。
- **R2-8 / C / low: `StartObserving` の C# の例外のログの重さが食い違う（v1 からある）**
  - 4.4 の 238 行は「どの原因でも `LogWarning`」と書いている。
  - しかし 4.4 の 233 行の C# の例外の行と、J-C9 の「例外なら `LogError`」は `LogError`。1.x も `LogError` だった（`Mgr:374-377`）。
- **R2-9 / C / low: 5.3 の 290 行（X1-2）が非同期の操作しか扱っていない**
  - 1.x の同期の読み取り（read / getDescription / hasClip）では、Kotlin の `OutOfMemoryError` が C# まで出た。C# が捕まえて、次のようになった。
    - read / getDescription: `CLIPBOARD_BRIDGE_UNAVAILABLE` / `ex.Message`（`Mgr:265-269,325-329`）
    - hasClip: `LogWarning` と `false`（`Mgr:295-299`）
  - 2.0.0 では `CLIPBOARD_UNKNOWN` / `Failed: OUT_OF_MEMORY` になる（`ClipboardBridge.kt:40-42`）。この差を書き足す。
- **R2-11 / C / low: 直さない残件を、理由とともに文書に書いていない**
  - 止める基準の 3（`workflow.md:159`）が求めている。
  - 該当するもの: R1-12 の 3 つ目（common.md の一言）、ラウンド 1 の「要検証: `Application.Unload()` の後の受け口」、この版の A2・B・C。
  - 短い節を 1 つ足せば足りる。

### 総合評価

- **A1: 0 件（確定）**。判断待ちが 1 件（R2-1）。
  - R2-1 は、利用者が J-11 の例外として受け入れれば C に下がる。保つと決めれば、J-C11 の作り直しが要る A1 になる。
  - どちらに決まるかで実装の形が変わるので、実装の前に決めることを勧める。
- 件数: A2 3 件（R2-4、R2-5、R2-10）、B 2 件（R2-3、R2-6）、C 5 件（R2-2、R2-7、R2-8、R2-9、R2-11）。
- v1 の 19 件のうち 15 件は解決、4 件は部分（R1-1、X1-1、R1-12、X1-5）、未解決は 0 件。
- 移し方の骨格は、v1 のまま一次資料と合っている。新しく引いた行番号もすべて正しい。
- 直した所から新しく生まれた問題は 2 つ。
  - 監視の開始の差分の書き方（R2-1、R2-2）
  - 層 2a の継ぎ目の記述の細部（R2-3、R2-4）
- AC-21a（URI で「ちょうど 1 回」）は、二重登録と監視の開始の両方を 1 本で押さえる、よい形になっている。
- 次の一手: R2-1 の判断を利用者に仰ぎ、R2-2 の文言を揃える。そうすれば、止める基準（A1 が 0、機械照合が通る、残件の明記）に届く。残件の明記には R2-11 の節が要る。

---

## 2. Codex

レビュー結果: 新規 A1 は 0 件。v2 は主要な契約差分を取り込めていますが、A2 が 1 件、B が 1 件残っています。ファイルは変更していません。

### 前回指摘の解消状況

| ID | 判定 | 根拠 |
|---|---|---|
| X1-1 | 解消 | J-C11、5.3、XML 更新予定、AC-21a/b/c に競合が明記された。待機しない方針も A-4 と整合する |
| X1-2 | 解消 | 5.3 に managed 確保例外と native OOM の差分が追加された |
| X1-3 | 解消 | `CopyMultipleText` で各要素を `AsPassed` に通す手順と層1テストが追加された |
| X1-4 | 解消 | AC-33 で全 `extern` の型・属性を実機反射検査する。`extern` は Editor に存在しないため、層2bへの配置は妥当 |
| X1-5 | 一部解消 | 重複登録は層2aの呼出回数と AC-21a の URI 通知「ちょうど1回」で検出できる。一方、登録失敗後に実際の `Initialize` / `StartObserving` が再試行用ヘルパーを呼ぶ配線は、Editor でヘルパーを直接呼ぶだけでは固定できない |
| X1-6 | 解消 | `OnDestroy` については injected F-10 の回数を live/duplicate × flag true/false で検査するため、呼出欠落を検出できる。ただし公開 `StopObserving()` は X2-2 のとおり別問題 |
| X1-7 | 解消 | logcat 差分が「ラベルは両版で出る。失敗ログだけが異なる」に訂正された |
| R1-1 | 解消 | X1-1 と同じ |
| R1-2 | 解消 | X1-3 と同じ |
| R1-3 | 解消 | `ReadFailure` の 6〜8 が想定外値と同じ結果に定義された |
| R1-4 | 解消 | AC-31 が追加され、Manager 再作成後の監視を実機で検査する |
| R1-5 | 解消 | AC-32 が native 側だけで開始した監視を使い、`s_observing` の門を実機で検査する |
| R1-6 | 解消 | AC-26 に `plainText == null`、AC-17/18 に `Read().Label` が追加された |
| R1-7 | 解消 | X1-7 と同じ |
| R1-8 | 解消 | Kotlin の `IllegalArgumentException` → `INVALID_PARAMETER` が 1.1、4.4、5.3 に明記された |
| R1-9 | 解消 | Kotlin 利用者による停止が Unity の通知も止めることが 5.3 に追加された |
| R1-10 | 解消 | `volatile int` を `(int)state + 1` で符号化すると明記された |
| R1-11 | 解消 | Manager、`Initialize`、`StartObserving`、`StopObserving`、`ClipItem` の XML 更新が変更一覧に入った |
| R1-12 | 一部解消 | `HasClip` の例外ログと F-7 の記述は修正済み。`common.md` の「NULL 配列要素は Manager が先に検査」に対する Clipboard 固有の例外説明は、変更内容に明記されていない |

### v2 の新規指摘

#### X2-1 — A2
- 設計箇所: 5.1 `AndroidClipboardManagerIntegrationTests.cs`、7.3 AC-32(v2 設計書の 246、254、457 行)
- 欠陥: AC-32 は `ntk_clipboard_start_observing()` と `stop_observing()` を直接呼びます。一方、既存テストファイルのガードは `#if UNITY_ANDROID` のままです。Android をビルドターゲットにした Editor でもテストコードはコンパイルされます。しかし `extern` は `UNITY_ANDROID && !UNITY_EDITOR` にしか存在しません。そのため、そのまま実装すると Editor のコンパイルが失敗します。
- 根拠:
  - `/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v2.md:246,254,457`
  - `/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/AndroidClipboardManagerIntegrationTests.cs:3`
  - `/Users/jonghyunkim/Desktop/unity-native-plugin/agent-rules/coding-rules/common.md:75,255-257`
- 修正: AC-32 と native-only helper を `#if UNITY_ANDROID && !UNITY_EDITOR` で囲みます。AC-33 も同じ領域に置くと、実機専用であることが明確になります。反射自体を実機に置く判断は正しいです。

#### X2-2 — B
- 設計箇所: 5.4 `StopObserving`、7.2、7.3 AC-22(v2 設計書の 330、412-414、447 行)
- 欠陥: 新しい `stopObserving` seam が数えるのは `OnDestroy` の F-10 呼出だけです。公開 `StopObserving()` から F-10 を呼び忘れても、先に `s_observing = false` になるため、コールバックは C# 側で捨てられます。AC-22 は通知が無いことしか見ないので成功します。つまり、公開操作での native stop 欠落は検出できません。
- 根拠:
  - 受け口は flag が偽なら捨てる設計: v2 設計書 `:273,344`
  - AC-22 は通知が無いことだけを見る: v2 設計書 `:447`
  - native stop は実際に処理を post する: `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library_capi/src/main/cpp/src/Clipboard/Clipboard.cpp:442-445`
  - system listener の解除箇所: `/Users/jonghyunkim/Desktop/native-toolkit/android/android_library/src/main/java/com/jonghyunkim/nativetoolkit/clipboard/presentation/ClipboardObserver.kt:35-38`、`ClipboardChangeMonitor.kt:57-65`
- 修正: 公開 `StopObserving()` と `OnDestroy` を、同じ injectable な native-stop wrapper に通します。層2a で公開 `StopObserving()` を実行し、F-10 が 1 回呼ばれ、戻り値が結果に反映されることを検査します。Editor の早期 return を越えるための test-only な native-ready seam も必要です。

機械照合 `scripts/check_design_consistency.py` は v2 に対して成功しました。

A1 件数: 0
