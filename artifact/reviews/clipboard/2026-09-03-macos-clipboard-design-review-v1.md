# レビュー結果

- 日付: 2026-09-03
- 対象ファイル: artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v1.md
- 機能名: clipboard
- プラットフォーム: macOS
- ドキュメント種別: 実装計画書
- レビュー実施: サブエージェント 3 本（並列）
  - A: ネイティブ仕様との整合（一次ソース逐条照合）
  - B: 既存 C# 実装・コーディングルールとの整合
  - C: 実装可能性・抜け漏れ・リスク
- 各指摘の末尾の `[A]` / `[B]` / `[C]` は担当エージェントを示す

---

## 強み

- 1.1 の 15 関数（名前・引数順・コールバック型・成功時 JSON）が実ヘッダと完全一致 `[A][B]`
- 1.3 の型分類（入力4・共用3・出力9・イベント1 = 17）と各スキーマ形、`PatternsJson` / `MatchingTypesJson` のトップレベル配列、`ReadDataJson` / `DetectedValuesJson` / `DetectedMetadataJson` の明示 null、ソート済み出力、ISO8601 UTC がすべて正本と一致 `[A]`
- 1.4 の必須・省略ルール全項目が Swift ファサードの guard と一致 `[A]`
- 1.5 のエラーコード表 22 行すべてが `ClipboardError.swift` / `BridgeError.swift` と完全一致、抜けなし `[A]`
- 1.6 末尾の `detectPatterns` / `detectValues` の guard 束ね指摘（patterns 不正でも scope のエラーとして分類される）が完全に正しい `[A]`
- `errorCode` を `long` で宣言する判断がヘッダの `NSInteger` と一致。`MacNotificationManager` の `int` との差異の指摘も正確 `[B]`
- `clipboardStartObserving` の `callback` / `onChange` の引数順が iOS と逆だが macOS ヘッダのとおりで正しい `[B]`
- xcframework への 15 関数エクスポート済みを `nm -gU` で実測確認しており、差し替え不要という判断は正しい `[A][B]`
- ガードチェーン 5 段の順序が `IosClipboardManager` の実装順と完全一致 `[B]`
- クラスガード（A 群）、`Mac*` 命名、Builder が public / Parser が internal、`DefaultObservationInterval = 0.5` がいずれも既存前例・ネイティブと一致 `[B]`
- workflow.md の必須項目（API 一覧・変更ファイル分類・実装詳細・callback 提供方針・3 契約・実装順序・層別エラー一覧・テスト方針・1 章と 2 章の分離・絵文字不使用）をすべて満たしている `[C]`
- `InternalsVisibleTo` は両テストアセンブリに既存で、asmdef 追加は不要であることを確認済み `[B][C]`

---

## 改善点

### 高優先度

#### エラーコード表の誤り（6.3）

- **1507 は Read と Snapshot にしか到達しない** `[A]`
  - `ClipboardRepositoryImpl.swift:141-143`（read）と `:164-166`（snapshot）以外は `pasteboardItems` を触らない
  - 他 11 行から 1507 を削除する
- **1505 は C ABI 経由では死にコード** `[A]`
  - `PasteboardResolver.swift:44-47` は空名でのみ 1505 を throw するが、`UnityMacClipboardJsonParser.swift:271/274/324` が空名を先に弾き 1301/1302 になる
  - 6.3 から 1505 を全削除し、「C ABI 経由では到達しない」と注記する
- **ReadData は 1504 を返さない** `[A]`
  - `ClipboardRepositoryImpl.swift:148-153` — `readData` は UTI を検証せず `pasteboard.data(forType:)` を呼ぶだけ。UTI 検証は copy/append 経路の `ClipboardContentValidator` からしか呼ばれない
  - 不正 UTI は**成功 + `data: null`** になる。ReadData の到達コードを 1599 のみにし、6.4 に「不正 UTI も成功 + `Data == null`（C# 側で UTI 誤りを検出できない）」を追加する
- **DetectMetadata に 1524 が欠落** `[A]`
  - `ClipboardRepositoryImpl.swift:233-244` — detectMetadata も同じ `Self.detectionError(from:)` を通る。detection 3 操作の 1514/1515/1524 は同一集合

#### 1.5 の注記が 1524 を誤分類 `[A]`

- 計画書は「1521 / 1522 / 1524 は paste ボタン経路」としているが、`ClipboardError.swift:17-18` は「cancelled covers only the detection APIs」と明記。paste ボタン経路は 1521/1522 のみ（`ClipboardPasteLoader.swift:165,192,195`）
- しかも 6.3 は自分で detect 系に 1524 を挙げており自己矛盾している
- 注記を「1521 / 1522 は paste ボタン経路（C ABI 未公開）。1524 は detection の cancel 経路」に修正する

#### append 成功後の changeCount は変わらない（7.4 #2 / 1.6） `[A]`

- `ClipboardRepositoryImpl.swift:98-105` — append は `prepareForNewContents` を呼ばず、成功時は**受け取った ownership をそのまま返す**（コメント: "A successful append leaves the change count untouched, so the caller keeps the same proof of ownership"）
- 手動確認 #2 の期待値「`changeCount` が更新される」が実装と逆
- 期待を「`changeCount` は変わらず、同じ ownership を次の Append にそのまま使える」に修正し、1.6 の append の項にも明記する

#### `BOOL isSuccess` に `[MarshalAs(UnmanagedType.I1)]` が必要（3.2 / 1.2 / 8 章 #2） `[B][C]`

- `IosClipboardManager.cs:193-200` は既にこの問題を認識して `I1` で幅を固定済み。コメントに「C# marshals a bare bool as a 4-byte Win32 BOOL by default, so the width is pinned explicitly」とある
- ObjC の `BOOL` は arm64 で `_Bool` / x86_64 で `signed char` の 1 バイト。既定の 4 バイト読みではレジスタ上位ビットの残骸を「非 0 = true」と読み、**失敗を成功と誤判定しうる**
- iOS v5 は「既存 `IosShareManager` が素の `bool` で動いている事実は根拠にならない（上位ビットが偶然ゼロの場合と区別できない）」と明示的に否定している。macOS 版でその結論を捨てる根拠が書かれていない
- `errorCode` は「正しさを優先」として `long` を選びながら BOOL だけ既存踏襲という判断の非対称も問題
- 3.2 を `[MarshalAs(UnmanagedType.I1)] bool isSuccess` に確定させ、8 章 #2 を削除する。`MacShareManager.cs:58` の未修正は別課題として残す

#### EditMode で 5 段のガードチェーンを検証するのは層モデル違反で実装不能（7.1 / 2.3） `[B]`

- `testing.md` 1 節・`common.md` ともに「Manager インスタンスを生成するテストは層 1 では書かない」と規定
- `TryStartOperation` は `private static` で到達経路は instance メソッドのみ。拒否結果は `s_dispatcher`（`Update` で flush）を必ず経由するため、EditMode では flush されない
- 既存も PlayMode のみ（`IosClipboardManagerIntegrationTests.cs:55`）。EditMode の `IosClipboardManagerDispatchTests.cs:14-19` は「No Manager instance is created here」と明記し純粋関数しか触らない
- 7.1 から当該箇条を削除して 7.2 へ移し、2.3 の「EditMode から駆動するためのシーム」も「PlayMode から駆動」に訂正する

#### Manager lifetime 契約が丸ごと欠落（5.6 / 2.3） `[C]`

- iOS v5 5.6.1「Manager lifetime 契約」に相当する節が無い
- tombstone と `managedCleanup` での `_instance = null` を同時採用しているため、破棄後に `Instance` を触ると**新しい GameObject が生成されるが tombstone は解除されず、全 API が永久に 9004 を返す死んだ Manager** になる。仕様か不具合か判断できない
- `public static bool IsTerminated` を公開するかも未定義（2.3 で名前だけ挙がるが公開 API 一覧にも 6.1 にも無い）
- 誤配送シナリオ、`Awake` での `Debug.LogError`、tombstone を production から解除できないことを明記する

#### 「共通イベントは常に発火」が破棄後の拒否経路で成立しない（5.6） `[C]`

- 「拒否経路（破棄済み含む）でも共通イベントは発火する」と書きながら、`OnDestroy` の `managedCleanup` で `_instance = null` にする
- iOS の `DispatchRejectedResult` は `_instance?.XxxCompleted` を渡す形なので、null 化後の 9004 拒否では**共通イベントが発火しない**
- 取得元を `this.` にする / `_instance = null` をやめる / 契約側を訂正する、のどれを採るか計画書で決める

#### in-flight マーカー取得後・P/Invoke 前の例外でマーカーが永久リーク（5.6） `[C]`

- 段階 5（`s_inFlight.Add`）と実際の P/Invoke の間に `BuildContentJson` の base64 化（最大 32 MiB → 約 43 MB の managed string）が挟まる
- ここで `OutOfMemoryException` が出ると in-flight が解放されず、**そのプロセスの残り全体でその操作が恒久的に Busy(9001)** になる。計画書は `InvokeNative` の中しか例外を想定していない
- 「JSON 構築を in-flight 取得より前に行う」か「取得〜呼び出しの全区間を try/catch で囲み `EndOperation` してから 9002 を返す」を明記する。あわせて iOS v5 5.6.3 のような段階表を入れる

#### レスポンス側の巨大 payload に防御が無い（5.3 / 5.4 / 8 章 #8） `[C]`

- 守られているのは送信側だけ。他アプリが 100 MiB の representation をコピーした状態で `Read` を呼ぶと、ネイティブは約 133 MB の base64 を含む JSON を返し、マーシャリングで**約 266 MB の UTF-16 string** が生成される
- `maxDecodedLength` はその**後**に効くので、上限を掛けても確保済みメモリは減らない
- (a) `maxDecodedLength` の具体値を決める、(b) マーシャル後の string サイズは制御できないことを制約として明記、(c) 8 章に「レスポンス実効上限とピークメモリの実測」を追加、の 3 点を入れる

#### 公開結果型の中身が未定義で実装不能（5.5 / 4.1） `[C]`

- 5.5 は擬似コードで `TPayload? Value` と書くが、実際は 11 個の別々の非ジェネリック型であり、各型のプロパティ・型・nullability が一切定義されていない
- 未定義: `MacClipboardReadItem`、`MacClipboardSnapshot` の 3 プロパティ、`MacClipboardDetectedValues` の 12 フィールド、`MacClipboardLabeledValue` / `MacClipboardPostalAddress` / `MacClipboardCalendarEvent` / `MacClipboardShipmentTracking` / `MacClipboardFlightNumber` / `MacClipboardMoneyAmount` / `MacClipboardDetectedLink` の全体
- **`MacClipboardDetectedMetadata` は 5.4 のパーサ表に出てくるだけで、4.1 のファイル一覧にも 5.1 の型一覧にも存在しない**
- iOS v5 3.5 相当の型仕様表を追加する。最低限「コレクションは常に非 null（失敗時は空）」「欠落と空を区別する必要があるものだけ nullable にし理由を書く」「Success / Failure ファクトリの正規化規則」を決める

#### 事前検証と「二重検証しない」原則の矛盾（5.1 / 5.6 / 6.1） `[C][B]`

- 5.1 は「ネイティブが検証する値（**サイズ超過**、不正 UTI、空 item）は C# で再検証しない」と断言する一方、5.6 は `MaxRequestBytes`(32 MiB) で C# が先に落とす。同一文書内で同一項目について逆の方針 `[C]`
- 表 7 行中 4 行（`utType` 空 / `RemovePasteboard(General)` / `intervalSeconds` / サイズ）が原則を破っている `[C]`
- とくに `RemovePasteboard(General)` は「メッセージはネイティブと同文言にする」としているため、**同一の Message が 9005 と 1508 の 2 コードで返り、コードでもメッセージでも分岐できない** `[C]`
- しかも実際の文言は一致していない。1.5 の 1508 は `Standard pasteboard cannot be released: {name}.`、6.1 の 9005 例は `The general pasteboard cannot be released.` `[B]`
- 解決の方向が 2 案ある。計画書で決めること
  - 案 1 `[B]`: 事前検証を外して 1508 / 1523 をそのまま返す
  - 案 2 `[C]`: 事前検証を残し、必ず 9005 とし**ネイティブと異なる文言**にして由来を判別可能にする。「同文言にする」の記述を削除し、6 章に対応表（9005 ↔ 通していれば返ったはずのネイティブコード）を追加する
  - なお `DetectPatterns` / `DetectValues` の空 patterns だけは 1.6 に固有の根拠（エラーコードから引数を特定できない）があるため現状維持でよい `[B]`

#### NULL コールバック方針の自己矛盾（1.2 / 3.1） `[C][B]`

- 1.2「C# 側は常に非 NULL の delegate を渡す設計とし、この分岐に依存しない」と 3.1「`clipboardStopObserving` のみ teardown 時に `null` を渡すため nullable」が直接矛盾
- 宣言自体は妥当（`UnityMacClipboardManagerBridge.m:238-245` が `if (callback)` で処理、前例も `IosClipboardManager.cs:251` / `MacNotificationManager.cs:130`）
- 1.2 を「通常経路では常に非 NULL。例外は `OnDestroy` の `StopObservingForTeardown` のみで、破棄後は結果を配送できないため NULL を渡す」に書き換える

---

### 中優先度

#### ネイティブ仕様の記述精度 `[A]`

- **`ScopeJson.name` は general では出力されない**（5.4）
  - `UnityMacClipboardJsonParser.swift:44-49` は独自 `encode` を持たず、Swift の合成エンコードは Optional に `encodeIfPresent` を使う。general の出力は `{"kind":"general"}` で `name` キーが存在しない
  - 5.4 の「キー欠落はスキーマ不一致として扱う」規約に例外を明記する。明示的 null で出るのは `ReadDataJson` / `DetectedValuesJson` / `DetectedMetadataJson` のみ。`OwnershipJson` / `ScopeResultJson` / `ChangeEventJson` の入れ子 scope も同様
- **`CheckForegroundChange` の「初回 true」は監視していない場合に限る**（1.6 / 6.4 / 7.4 #19）
  - `ClipboardChangeMonitor.swift:85` が start 時に `tracker.hasChanged` で初期値を記録するため、同一 scope で `StartObserving` が先に走っていると初回でも false
  - `MacClipboardManager.swift:520-528` に「Use this instead of observation, not alongside it」と明記あり
- **lazy data provider 経路の記述が無い**（1.6 / 5.6）
  - `ClipboardContentValidator.swift:34-38`（単一 item かつ 10 MiB 超）、`ClipboardRepositoryImpl.swift:108-134`（`setDataProvider` で型だけ宣言し bytes は遅延供給）
  - C# が想定する 32 MiB までの copy は**必ずこの経路に入る**。pasteboard には型だけが載り、実バイトは読み手が要求した時点でプロセスが生きていないと供給されない。**Copy 成功 ≠ 貼り付け可能**
  - 1.6 に項を追加し、7.4 #20 に「Copy 後に Unity を終了してから他アプリで貼り付ける」を足す
- **`detectMetadata` も macOS 15.4 未満で 1513**（1.6 の記載漏れ。6.3 は正しい）
  - `ClipboardRepositoryImpl.swift:235-237` が同じ `#available(macOS 15.4, *)` ガードを通る
- **`OptionsJson` の `localOnly` はキー省略可**（1.3）
  - `UnityMacClipboardJsonParser.swift:30`（`Bool?`）、`:309`（`?? true`）。`{}` も有効。`{"localOnly": true|false}?`（キー省略時 true）と書く
- **エンコード失敗由来の 1599 は値返却操作に限る**（6.3）
  - `UnityMacClipboardManager.swift:271-280`（removePasteboard）、`:379-407`（startObserving）は `encode` を通らない void 経路

#### C# 契約・実装方針 `[B]`

- **Editor 専用の完了シーム群が無い**（5.6 / 4.1 / 7.2）
  - `IosClipboardManager.cs:1656-1704` の `CompleteOperationForTests` / `CompleteObservationControlForTests` / `CompleteReadForTests` / `CompleteSnapshotForTests` / `DeliverChangeEventForTests` / `IsInFlightForTests` / `HasChangeRegistrationForTests` / `PendingObservationGenerationForTests` / `HasAnyPendingCallbackForTests` / `InFlightCountForTests`
  - Editor では P/Invoke がコンパイルされないため、`BridgeAvailableOverrideForTests` で開始した操作は永久に pending のまま。**シームだけ持たせても単体では使い道がない**
  - あわせて「`[MonoPInvokeCallback]` 本体は narrow guard 内、実処理は `HandleXxxCallback` として guard 外に切り出す」構成を明記する（`IosClipboardManager.cs:1508-1654` と同形）。これが無いとシームがコンパイルできない
- **`(int)` narrow の契約が実装方針と矛盾**（3.2 / 5.2）
  - unchecked な `(int)` は上位ビットを捨てて折り返すため（`0x1_0000_0001L` → `1`）、「値域外は 1599 相当として扱う」経路が存在しない
  - `Create` 内で `code < int.MinValue || code > int.MaxValue` を明示判定し、外れた場合のみ 1599 に落とすと書き換える
- **検出パターンの rawValue 対応表が無く、置き場所も既存と逆**（5.1 / 5.3 / 7.1）
  - `IosClipboardPayloads.cs:462-509` は `internal static class IosClipboardDetectionPatternExtensions` を **Payloads 側**に置いている
  - ネイティブ正本 `ClipboardDetection.swift:10-20` は macOS が複数形（`phoneNumbers` など）で **iOS の単数形と異なる**。表が無いと iOS からコピーする事故が起きる
  - 11 件の完全な対応表を 5.1 に載せ、iOS との差を注記。置き場所を `MacClipboardPayloads.cs` に揃える。`MacClipboardAccessBehavior` の rawValue（`ClipboardDetection.swift:33-39`）も同様
- **`MacClipboardItem` の命名が iOS と逆の意味**（5.1 / 4.1）
  - `IosClipboardReadResult.cs:19` の `IosClipboardItem` は**読み出し結果**の型。入力側は `IosClipboardContent`
  - 入力側を `MacClipboardContentItem`、出力側を `MacClipboardItem` に改名する
- **`.meta` の記述が不正確**（4.2）
  - `.cs` だけを rename すると旧 `.meta` が孤児化し、Unity が**新しい GUID** で再生成する。「再生成される」という記述は GUID が変わる事実を隠している
  - 5.7 手順 1 に `git mv` で `.cs` と `.cs.meta` を対で移すと明記する（新規作成ではなく移動なので `common.md` の `.meta` ルールに抵触しない）

#### Manager 設計 `[C]`

- **世代管理の存在理由が自分の single-flight と矛盾**（5.6）
  - 「古い stop の完了が新しい start の登録を消さない」と書くが、`ObservationControlKey` を共有し同時 1 件と宣言している以上、その状態は Busy(9001) で拒否されて発生し得ない
  - 世代の本来の役割は iOS v5 5.6.4 のとおり「**context を受け取れない static コールバックへ、どの登録に責任を持つかを渡すこと**」
  - あわせて状態遷移表が欠落（Start で `s_onChangedGeneration = ++s_observingGeneration` / **Stop は世代を進めず** `s_pendingObservationGeneration = s_observingGeneration` / 完了時に 0 へ / `ClearAllPendingCallbacks` で 3 変数とも 0）。`s_onChangedGeneration != 0` のガードも未記載。これが無いと判定式だけでは実装が確定しない
- **teardown の `StopObserving` が single-flight を迂回する点が未記載**（5.6）
  - ガードチェーンを通らずネイティブを直接叩くため、`ObservationControlKey` が in-flight の可能性がある。安全と考えられるがその論証が無い
  - 「tombstone が先に立っており新規操作が入らないため安全」と明記し、`ClearAllPendingCallbacks` と世代リセットが `stop` の**後**に走る順序も書く
- **P/Invoke 例外経路が「拒否経路」と同列に書かれている**（5.6 / 6.1）
  - 「拒否経路は in-flight に触れない」は正しいが、直後の「例外は in-flight を解放して 9002」は**通常経路**の話で性質が正反対。6.1 で両方の 9002 が並んでおり、同じ dispatch 関数を使う危険がある
  - 「段階 1〜5 の失敗 = `DispatchRejectedResult`（触れない）／段階 7 の例外 = 通常経路 `FireXxxResult`（`EndOperation` する）」と分離して書く
- **`EndOperation` と dispatch の順序が未定義**（5.6 / 7.1）
  - iOS v5 は「`EndOperation` は dispatch より**前**（購読者が callback 内から同じ操作を再呼び出しできるように）」を明示的な契約にしている
  - 順序を契約として書き、7.1 に「コールバック内からの同一操作の再実行が Busy にならない」を追加する
- **`const char*` ↔ `string` の文字エンコーディングが未記載**（3.1 / 3.2 / 8 章）
  - `CharSet` も `MarshalAs(UnmanagedType.LPUTF8Str)` も言及が無い。`errorMessage` には `{name}` / `{value}` が埋め込まれ、`probableWebSearch` / `postalAddresses` には非 ASCII が普通に入る
  - Builder が非 ASCII を `\uXXXX` エスケープするのか生 UTF-8 で出すのかも未決（7.1 の「エスケープ: 非 ASCII」の期待値が読み取れない）
  - 方針を確定し、8 章に「日本語・絵文字・サロゲートペアの往復確認」を追加、化けた場合の対処（`LPUTF8Str`）を代替案として書く
- **`s_dispatcher` の所有・有効性・リセット規約が無い**（5.6）
  - iOS v5 には所有者・`Awake` での再キャプチャ・`OnDestroy` で null にしない・**off-thread では Unity の `==` オーバーロードではなく `(object)s_dispatcher == null` のプレーン参照判定を使う**・`SubsystemRegistration` でのリセット、という表がある
  - ガードチェーン段階 1 が「ログを含むすべてを dispatcher の closure 内で行う」と書いている以上、off-thread の null 判定は必ず踏む経路。Unity のオーバーロードを使うと別スレッドからネイティブオブジェクトに触れる
  - あわせて iOS 9.9 の「Reload Domain / Scene Reload をともに無効化した構成で `Awake` が再実行されず dispatch 経路が死ぬ可能性」を 8 章に追加する
- **namespace が計画書のどこにも書かれていない**（5 章全体）
  - iOS v5 は 3.1 で明示。16 個の新規公開型を作るのに記載が無い。workflow.md ステップ 4 も namespace の把握を要求している
  - 5 章冒頭に `JonghyunKim.NativeToolkit.Runtime.Clipboard` と明記する
- **xcframework バイナリと参照ソースの版対応が未検証**（0 節 / 1 節 / 4.5）
  - JSON スキーマ・エラーコード・既定値はすべて**ソース**から読み取っているが、Unity がリンクするのは同梱バイナリ 1.3.0。`nm -gU` で確認したのは**シンボルの存在だけ**
  - ソースが先行しているとスキーマが食い違い、全パーサが 9006 で落ちる
  - 0 節に「参照ソースの commit / タグと xcframework 1.3.0 のビルド元が一致することを確認する」を追加し、未確認なら 8 章へ
- **`MaxRequestBytes` が実質の公開上限になることが契約として書かれていない**（5.3 / 5.6 / 8 章 #8）
  - C# が 32 MiB で先に落とすため利用者から見える上限は 32 MiB で、1506 はほぼ到達しない。マニュアル・XML コメントに誤った上限を書く原因になる
- **8 章に「決定事項」が混ざっている**（#4 / #11 / #12）
  - いずれも実機検証を要さず、この計画書が決めるべき設計判断。とくに #11 は 5.6 本文で既に「2 引数版を新設する」と決めており重複・矛盾
  - #4 は改名を推奨として確定（iOS 側 42 箇所の機械的置換のコストより、`Ios*` に macOS が依存する構造を残す代償が大きい）。#11 は削除。#12 は「決定事項・非対応範囲」として 9 章へ移す
- **監視イベントの terminated ガードと PlayMode のテスト隔離が未記載**（5.6 / 7.2）
  - `DiscardIfTerminated` が `ClipboardChangeCallback` にも必要な点が書かれていない。teardown で stop を呼んでも既に main queue に載ったイベントが 1 回届きうる
  - 7.2 に破棄系テストがあるのに `[TearDown]` での `ResetForTests()` 呼び出しが無い。欠くと Domain Reload 無効設定でこの 1 件が以降の全 PlayMode テストを 9004 にする
- **observation 共有キーの Busy メッセージが嘘をつく**（5.6 / 6.1）
  - `StartObserving` が進行中の `StopObserving` に阻まれると「startObserving is already in progress」と返る。専用文言を用意する

---

### 低優先度

#### 記述・数値の不整合

- 4.1 のファイル数は数えると **16 件**（本文の 17 件と不一致）。5.7 の内訳（結果型 11 + Payloads + ErrorInfo + Builder + Parser + Manager = 16）とは整合 `[C]`
- 4.1 の「C# ブリッジ 9001-**9005**」が 5.2 / 6.1 の 9006（`ResponseParseFailed`）と不一致。9001-9006 に統一する `[C][B]`
- 5.2 の `MacClipboardErrorInfo.UnknownErrorCode = 1599` と `MacClipboardErrorCodes` の 1599 が二重定義。片方を他方の参照にする `[B]`
- 出力型の個数表記が不統一。4.1 / 7.1 は「10 種」だが 5.4 のパーサ表は 12 メソッド（出力専用 9 + 共用 2 + イベント 1）。12 に統一する `[C][B]`
- `MacClipboardDetectedMetadata` が 4.1 の型一覧から漏れている `[C][B]`
- 4.2 の置換件数「Runtime 2 箇所」は実測 3 箇所（`IosClipboardJsonParser.cs` 2 + クラス宣言 1）。Tests 42 箇所は一致 `[B]`
- 3.1 の「camelCase のまま宣言する（`MacNotificationManager` の既存方針と同じ）」は不正確。`MacNotificationManager` は `NotificationShow` などの **PascalCase**。既存方針は「ネイティブシンボルに一致させる」。文言を直す `[C]`

#### スキーマ・テストの補足 `[A]`

- 1.3 の `DetectedValuesJson` は入れ子型のスキーマが未記載。`PhoneNumber.label` / `EmailAddress.label` / `PostalAddress` の 5 フィールド / `CalendarEvent` の日付・タイムゾーンも**明示的 null**で出る（`UnityMacClipboardJsonParser.swift:100-150`）。一方 `Link` / `ShipmentTracking` / `FlightNumber` / `MoneyAmount` は全フィールド非 optional。7.1 の Parser テストで網羅するなら 1.3 に入れ子スキーマを書き足す
- 出力 JSON は `encoder.outputFormatting = [.sortedKeys]`（`:356`）でキー順がアルファベット順に安定する。テストのゴールデン文字列の前提にできる
- `snapshot` の guard は scope → matchingTypes の順（`UnityMacClipboardManager.swift:214-217`）。引数ごとに `argumentError()` を分けているので分類自体は正しい
- 1508 の対象は general だけでなく `font` / `ruler` / `find` / `drag` も含む（`PasteboardResolver.swift:21-27`）。5.6 の事前検証は `Kind == General` のみを弾く設計なので、標準名を `Named` で渡すとネイティブ 1508 が返る。5.6 の理由書きが網羅していないように読める

#### 既存前例との細部整合 `[B]`

- delegate **型**は既存では narrow guard の**外**に置かれている（`MacShareManager.cs:57-58`、`IosClipboardManager.cs:193-205`）。3.2 に「delegate 型は guard 外、`static readonly` フィールドと `[MonoPInvokeCallback]` 本体は guard 内」と 1 行足す
- `InvokeInOrder` の try/catch 粒度は `IosClipboardManager.cs:591-611` と一致するが、2.2 で前例に挙げた `MacShareManager.cs:236-249` は**両方を 1 つの try/catch で包む**（共通イベントが投げると per-call が呼ばれない）。2.2 に「Mac Share ではなく iOS Clipboard 側の分離 try/catch を採る」と注記する
- `MacClipboardReadContents` の入れ子は `IosClipboardReadResult.cs:50-62` のフラット構成と逆（`IosClipboardSnapshotResult.cs:71-80` の入れ子は前例どおり）。揃えるか理由を書く
- `MacClipboardCopyOptions.Default` は iOS の `PrivacyPreservingDefault`（`IosClipboardPayloads.cs:352`）と比べ、「localOnly が既定で true」という意図が名前から失われる
- 逸脱コメントの形式は既存で使い分けられている。Manager / Builder は**クラスの XML `<summary>` 内 `<para>`**（`IosClipboardManager.cs:44-48`、`IosClipboardJsonBuilder.cs:22-25`）、結果型は **`#if` 直後のファイル先頭 `//` コメント**（`IosClipboardErrorInfo.cs:3-7`）。5.6 も同じ使い分けで書き直す
- `ClipboardJsonReader` のクラス doc に iOS 固有記述がある（`IosClipboardJsonReader.cs:417-431` の「`loadItem` is polymorphic on `kind`」など）。改名時に文面のプラットフォーム中立化も必要。4.2 の「機械的な識別子置換のみ」に追記する
- テストファイルのコンパイルガードが 4.3 / 7 章に未記載。前例は `MacShareManagerIntegrationTests.cs:3` が `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`。新規 5 ファイルにも明記する
- `Runtime/AssemblyInfo.cs` は変更不要だが、既存コメントが `IosClipboardManager.ResetForTests` を名指ししているため `MacClipboardManager.ResetForTests` 追加時に更新対象になりうる。4.5 に「確認済み・変更不要」と書く
- 5.6 の `Awake` 記述に `else if (_instance != this) { Destroy(gameObject); return; }` の重複インスタンス破棄分岐が無い（`common.md` Singleton 節、`MacShareManager.cs:79-92`）

#### その他 `[C]`

- 7.1 に `MacClipboardPayloads` の検証テストが無い。`MacPasteboardScope.Named("")` の `ArgumentException`、`MacClipboardItem.PlainText` の UTF-8 エンコードは EditMode で検証できる
- `StartObserving` を 2 回呼ぶと 1 回目の `onChanged` が黙って差し替わる。1.6 に「ネイティブは再開する」とあるだけで、**C# 側の登録が置き換わる**ことが契約として書かれていない
- workflow.md は個別 callback を「last-registered wins」と規定しているが、本計画は single-flight により後勝ちが発生しない設計（iOS v5 と同じ、正しい逸脱）。逸脱であることを 5.6 に 1 行明示する
- 1.2 / スレッド契約の「macOS Standalone では AppKit のメインスレッド = Unity のメインスレッド」は断定形だが実測記録が無い。7.4 に「コールバックのスレッド ID が Unity メインスレッドと一致すること」を足すと安い
- `Awaitable` 見送りの根拠として「パッケージ全体でまだ前例が無い（`Runtime/` に `*Async` 実装 0 件）ため Clipboard だけ先行させない」を 1 行足すと `common.md` との関係が明確になる
- 7 章に「テストはサンプル値のみを用い、実クリップボード内容を持ち込まない（`testing.md` 6 節）」を 1 行書く

---

## 不足項目

- **iOS v5 相当の型仕様表**（各結果型のプロパティ・型・nullability・コレクション非 null 規約・ファクトリの正規化規則） `[C]`
- **iOS v5 5.6.1 相当の Manager lifetime 契約表** `[C]`
- **iOS v5 5.6.4 相当の監視の世代管理・状態遷移表** `[C]`
- **iOS v5 5.6.3 相当の段階表**（段階 6 = per-call スロット格納、段階 7 = native 呼び出し、段階 8 = コールバック到着） `[C]`
- **iOS v5 8 章相当の Definition of Done**。implement-feature の完了判定が曖昧になる `[C]`
- **iOS v5 5.10 相当の IL2CPP 制約の節**（`[MonoPInvokeCallback]` は static、例外をネイティブ境界へ漏らさない、AOT 制約）。現状は 5.6 に 1 行あるのみ `[C]`
- **`#if UNITY_EDITOR` の完了シーム群と `HandleXxxCallback` 分離構成** `[B]`
- **検出パターン 11 件と AccessBehavior 5 件の rawValue 対応表** `[B]`
- **namespace の明記** `[C]`
- **`DetectedValuesJson` の入れ子型スキーマ** `[A]`
- **iOS v5 5.6.3 末尾の「採用しなかった案」表**（FIFO キュー / last-registered wins / ネイティブへの request ID 追加）と 9.8「ABI への request / lifetime ID 追加が恒久解」の記録。single-flight と tombstone がいずれも ABI 制約への回避策であることを残す `[C]`

---

## 総合評価

**現状のまま implement-feature に進むべきではない。少なくとも Manager コアに着手する前に、高優先度の指摘を計画書へ反映すること。** `[C]`

- **調査部分（1 章）の精度は高い。** 15 関数のシグネチャ、コールバック仕様、17 型の JSON スキーマ、必須・省略ルール、エラー表 22 行、`detectPatterns` の guard 束ね指摘はいずれも一次ソースと一致し、この計画書の中核的価値になっている `[A]`
- **既存パターンの選択も妥当。** `MacShareManager` の macOS P/Invoke 形式 + `IosClipboardManager` の single-flight / tombstone / 世代管理という合成、ガードチェーン 5 段の順序、A 群ガード、命名規約はすべて既存前例と一致している `[B]`
- **問題は 2 つに集中している。**
  1. **エラーコード表の事実誤り**（1507 / 1505 / 1504 / 1524 / append の changeCount）。ソースを読めば判明する範囲で 6 件の誤りがある `[A]`
  2. **設計密度の不足**。iOS v5 の 1,978 行に対し本計画書は 992 行で、結果型の仕様欠落・lifetime 契約の欠落・世代管理の状態遷移表の欠落は、その密度不足が形になったもの。件数（16 + 5 ファイル）ではなく**未決事項の量**がリスク `[C]`
- **マーシャリングの判断に一貫性が無い。** `errorCode` は「正しさを優先」して `long` にしながら、`BOOL` は誤りと分かっている宣言を「既存踏襲」で採り実機確認送りにしている。iOS Clipboard は既に `[MarshalAs(UnmanagedType.I1)]` で解決済みであり、前例を採るなら Clipboard 機能の直近の前例を採るべき `[B][C]`
- **テスト方針に実装不能な項目がある。** 7.1 の「EditMode で 5 段のガードチェーンを検証」は層モデル違反で必ず破綻する `[B]`

### 推奨する分割 `[C]`

Manager コアにリスクが集中しているため、1 回の implement-feature で通さず 4 段に分けることを推奨する。

| 段 | 範囲 | 単独マージ可否の根拠 |
| --- | --- | --- |
| 1 | `ClipboardJsonReader.cs` への改名 + ガード拡張、`IosClipboardJsonParser.cs` と iOS テストの置換 | 振る舞いの変更ゼロ。既存 iOS テストの全 pass が完了条件 |
| 2 | Payloads / ErrorInfo / 結果型 11 / Builder / Parser + EditMode 3 テスト | ネイティブに一切依存しない。型仕様をここで確定できる |
| 3 | Manager 本体（Singleton / lifecycle / tombstone / dispatcher / ガードチェーン / dispatch）+ Copy / Append / Read / ReadData / Clear + Dispatch テスト + PlayMode テスト | 設計上のリスクがすべてこの段に集中する。小さくすることに最大の価値がある |
| 4 | 残り 10 操作 + 監視の世代管理 | 3 で確立したパターンの反復。監視は新機構なので必要なら 4a / 4b に分割 |

段 2 と段 3 の間に一度レビューを挟むこと。**段 3 に着手する前に高優先度の指摘をすべて反映する。**

### 要検証（本レビューで確定できなかった事項）

- xcframework 1.3.0 のビルド元が参照したソースツリーと一致するか `[C]`
- `const char*` の非 ASCII 往復（`LPUTF8Str` の要否） `[C]`
- レスポンス側のピークメモリ実測値 `[C]`
- `MaxRequestBytes` の適正値 `[C]`
