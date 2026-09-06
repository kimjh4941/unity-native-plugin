# 実装結果レポート

## 基本情報

- 日付: 2026-09-05
- 機能名: clipboard
- 対象プラットフォーム: macOS
- ブランチ: `feature/UNT-10`
- 実装計画: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v13.md`
- **対象範囲: 7.5 の手動確認 32 項目（実機実施）**
- 前版: `2026-09-03-macos-clipboard-implementation-feature-result-v4.md`（段 3）
- サンプル計画: `artifact/designs/clipboard/2026-09-05-macos-clipboard-sample-scene-design-v3.md`

## 0. 位置づけ

**P/Invoke 境界の初回実行である。** Editor では P/Invoke がコンパイルされないため、EditMode 573 / PlayMode 116 のテストはすべてその手前で止まっていた。本項目でブリッジ層が初めて実機で動いた。

**32 項目すべてを実施した**（#29 は 2026-09-06 に追加実施）。 実装の欠陥を 1 件発見し、設計書の記述の誤り・不足を 4 件、サンプルの欠陥を 1 件見つけた。

---

## 1. 実施環境

| 項目 | 値 |
|---|---|
| OS | **macOS 26.3**（Darwin 25.3.0）、arm64 |
| 実行形態 | Unity が生成した Xcode プロジェクトを Xcode でビルド・Run |
| bundle ID | `com.jonghyunkim.nativetoolkit.mac` |
| ログ | `-logFile /tmp/mac-clipboard-run.log`（3,434 行） |
| サンプル | `MacClipboardManagerExampleController`（43 ボタン） |

**15.4 未満の分岐は実施できない。** 26.3 は `#available(macOS 15.4, *)` の内側を通るため、1513 と `AccessBehavior = Unavailable` には到達しない。

---

## 2. 実施結果

| # | 項目 | 結果 | 備考 |
|---|---|---|---|
| 1 | `Copy` → 他アプリで貼り付け | ○ | `OwnershipChanged` が `operation: copy` |
| 2 | `Copy` 直後の `Append` | ○ | changeCount 18 のまま不変 |
| 3 | 他アプリコピー後の `Append` | ○ | **1511** |
| 4 | `Read` の型 | ○ | **§5 の訂正あり** |
| 5 | `ReadData`（不在 UTI / 不正 UTI） | ○ | 両方とも成功 + データ無し |
| 6 | `Snapshot`（フィルタ有無） | ○ | items/totalTypes 不変、matching 2 → 1 |
| 7 | `Snapshot`（空配列） | ○ | **1512** `emptyTypeFilter` |
| 8 | `Clear` → `Read` | ○ | items=0、鮮度アンカーも無効化 |
| 9 | unique の生成〜解放 | ○ | **解放後は `OK items=0`。1507 ではない** |
| 10 | `RemovePasteboard(General)` | ○ | **1508** |
| 11 | `DetectPatterns` | ○ | 要求 11 → 一致 9。102 ms |
| 12 | `DetectValues` | ○ | `#11` と完全一致。**ダイアログ出ず** |
| 13 | `DetectMetadata`（plain text） | ○ | **1515** `detectionFailed` |
| 14 | `GetAccessBehavior` | ○ | **`AlwaysAllow`** |
| 15 | 監視開始 → 他アプリでコピー | ○ | **前面復帰時に配信** |
| 16 | 監視の再開始（別 `onChanged`） | ○ | 旧登録は凍結、二重購読なし |
| 17 | 間隔 0 / 61 / 負値 / NaN | ○ | **4 つとも 1523** |
| 17b | `DetectPatterns` に空コレクション | ○ | **1503** |
| 17c | `ReadData` に空 utType | ○ | **1302** `contractViolation` |
| 17d | `Named(" ")` | ○ | **`ArgumentException`**（結果ではない） |
| 18 | 停止後にコピー | ○ | イベント 0 件 |
| 19 | 監視なしの `CheckForegroundChange` | ○ | 初回 True、2 回目 False |
| 20 | 監視中の `CheckForegroundChange` | ○ | 両方 False |
| 21 | 12 MiB → Player 終了 → 貼り付け | ○ | **§5 の限定条件あり** |
| 22 | 12 MiB → 起動中に貼り付け | ○ | 532 ms。貼り付け側が重い |
| 23 | 32 MiB 超の `Copy` | ○ | **9007**（C# 側で拒否） |
| 24 | 他アプリが 50 MiB 超 → `Read` | ○ | **9006。設計どおりの上限（§4.1）** |
| 25 | 日本語・絵文字・サロゲートペア | ○ | `roundTrip=match` |
| 26 | Universal Clipboard | ○ | **両方向とも確認**（§5 にサンプルの欠陥） |
| 27 | ログ確認 | ○ | **漏洩 0 件** |
| 28 | コールバックのスレッド | ○ | `callbackOnMainThread: true (samples=1)` |
| 29 | App Sandbox 有効ビルド | ○ | **全操作が動作。追加 entitlement 不要**（§4.3） |

**32 項目すべて実施。**

---

## 3. 計測値

| 操作 | 所要 |
|---|---|
| 小さな `Copy` / `Read` / `Snapshot` | 3〜11 ms |
| `GetAccessBehavior` | 12〜14 ms |
| `DetectValues`（2 回目以降） | 13〜33 ms |
| `DetectPatterns`（初回） | **102 ms** |
| `Read` 24 MiB | **289 ms** |
| `Read` 32 MiB | **342 ms** |
| `Copy` 12 MiB | **532 ms** |

**失敗も 1 秒未満で返る。** 9006 になる巨大な読み出しでもフリーズしない。

---

## 4. 実装に関する発見

### 4.1 受信側の 32 MiB 上限は設計どおり機能する。ただし iOS と乖離している

**他アプリが 32 MiB を超える representation を置くと `Read` が 9006（`ResponseParseFailed`）で失敗する。上限そのものは設計どおりの動作であり、欠陥ではない。**

| データ | 上限との関係 | 結果 |
|---|---|---|
| 24 MiB | 下回る | **OK**（289 ms） |
| **32 MiB** | **ちょうど等しい** | **OK**（342 ms） |
| **36 MiB** | 超過 | **NG 9006** |
| 40 / 47 / 48 / 60 MiB | 超過 | NG 9006 |

`MacClipboardLimits.MaxResponseBytesPerRepresentation = 32 MiB`（`MacClipboardConstants.cs:92`）を `MacClipboardJsonParser` が base64 デコード時に検査し、`MacClipboardJsonReader.cs:280` の `decodedLength > maxDecodedLength` で `TooLarge` を返す。計画書 v13 の 903 行が、この `TooLarge` を 9006 にすると明示している。比較が厳密超過（`>`）であるため **ちょうど 32 MiB は通る。** 実測の 32 MiB OK / 36 MiB NG はこの境界と完全に一致する。

**実測が示したこと:**

- **上限が実機で設計どおりに効いている。** 定数による決定的な判定であり、メモリ状況に依存しない
- **失敗が安全である。** 1 秒未満で結果として返り、クラッシュもフリーズもしない
- **上限は representation ごとで、合計ではない。** 本測定は 1 item × 1 representation だったため両者が一致した

#### iOS との乖離（対応が必要な論点）

| | iOS | macOS |
|---|---|---|
| 上限値 | **64 MiB**（`IosClipboardJsonParser.MaxDataByteCount`） | **32 MiB** |
| 超過時のコード | **`CLIPBOARD_CONTENT_TOO_LARGE`**（専用） | **9006**（解析失敗と同一） |
| メッセージ | `The clipboard content exceeds the configured size limit.` | `The native result could not be parsed.` |

**iOS は「大きすぎる」を専用コードで分離済みである**（`IosClipboardJsonParser.cs:33 / 45 / 831-833`）。macOS だけが解析失敗に集約しているため、**開発者は「大きすぎた」のか「応答が壊れていた」のかを区別できない。** 9006 は「ブリッジのバグ」「壊れた JSON」を疑わせるが、実際の原因は他アプリが置いた内容の大きさであり、アプリ側から制御できない。

**したがってこれは新しい設計提案ではなく、先行実装への追随である。** 対応案:

1. `MacClipboardErrorCodes` に「応答が大きすぎる」専用コードを追加する
2. `MacClipboardJsonParser` の `TooLarge` 分岐だけを新コードに割り当て、`Malformed` は 9006 のまま残す
3. 上限値 32 MiB を維持するか iOS の 64 MiB に揃えるかを別途判断する（送信側 `MaxRequestBytes` が 32 MiB なので、**維持すれば送受対称**になる）

**計画書の改訂（903 行・5.2.2・エラーコード表）を伴うため、`design-feature` からの着手になる。**

### 4.2 実装の欠陥: `PostBuildProcessor.cs` の macOS 経路が framework を embed していなかった

**修正済み（コミット `c6e9ec4`）。**

dyld が `@rpath/UnityMacPlugin.framework` を解決できずアプリが起動しなかった。iOS 経路は embed と runpath の設定を行うが、**macOS 経路は link のみで embed も runpath 設定も無かった**。

macOS のバンドル構造は iOS と異なり、runpath は `@executable_path/../Frameworks` である（iOS の `/Frameworks` ではない）。

**macOS の Share / Notification / Dialog も同じ経路を通るため、これらすべてに影響していた既存の不具合である。**


### 4.3 App Sandbox 有効下でも全操作が動作する（V-5 クローズ）

実施日 2026-09-06。`Build/Mac/Mac.xcodeproj` に `ENABLE_APP_SANDBOX = YES` を加えて再ビルドし、署名に `com.apple.security.app-sandbox` が入っていることと `~/Library/Containers/com.jonghyunkim.nativetoolkit.mac/` が生成されることを確認したうえで実行した。

| 操作 | scope | 結果 |
|---|---|---|
| `Copy`（plain text） | General | **ok** |
| `Read` | General | **ok** |
| `CreatePasteboard(Named)` | General | **ok** |
| `CreatePasteboard(Unique)` | **Named** | **ok** |
| `RemovePasteboard` | **Unique** | **ok** |

**失敗 0 件、サンドボックス由来の拒否 0 件。** pasteboard server はプロセス外にあるが App Sandbox はこれを遮断しない。**追加の entitlement は不要で、`com.apple.security.app-sandbox` だけで 15 操作すべてが使える。** Mac App Store 向けビルドでも機能は制限されない。

`CreatePasteboard(Unique)` が `scope: Named` から成功しているため、**named を作った後に unique も作れる**ことまで確認できている。

#### 付随: `-logFile` はコンテナ外を指定できない

サンドボックス下で `-logFile /tmp/...` を渡すと、Unity は次を出して**起動せずに終了する**。

```
Unable to open log file, exiting. File: /tmp/mac-clipboard-run.log
```

コンテナ内（`~/Library/Containers/<bundle id>/Data/`）なら書ける。clipboard とは無関係だが、**サンドボックス配布時にコンテナ外へのファイル出力を前提とした実装がすべて破綻することの実例**であり、マニュアルに残す価値がある。

#### `ENABLE_APP_SANDBOX` はパッケージ側に入れない

**この設定は次の Unity ビルドで消える。** Unity が Xcode プロジェクトを生成し直すためである。

**それでも `PostBuildProcessor.cs` には追加しない。** 同ファイルはパッケージ内にあり define ゲートも無いため、**このパッケージを入れた全利用者の macOS ビルドが無条件にサンドボックス化される。** サンドボックスの要否は配布経路が決めるもので（Mac App Store は必須、Steam は通常無効）、ライブラリが決めてよい事柄ではない。framework の embed（§4.2）が「入れないと動かない」のに対し、こちらは「勝手に入れると利用者のアプリを壊す」性質の設定である。

**#29 は 1 リリースにつき 1 回の手動確認として扱う。**

## 5. 設計書・サンプルの訂正

### 5.1 `§1.6` 型の派生 — 機序の記述が誤り

現在の記述:

> 読み出しは書き込みの鏡ではない。**pasteboard が型を派生させる**ため、RTF で書いても plain text として読める

**結論は正しいが機序が違う。** 型を増やしているのは **書き込み側（AppKit を使うリッチテキストアプリ）** であり、読み出し時の派生ではない。

| 書き込み元 | writtenTypes | readTypes |
|---|---|---|
| 自ライブラリ `Copy Html` | 2 | **2** |
| 自ライブラリ `Copy Url` | 1 | **1** |
| 他アプリの plain text | — | **1** |
| **他アプリのリッチテキスト** | — | **`rtf` / `utf8` / `utf16-external` の 3 型** |

最後の 1 行はネイティブ側の実測（`native-toolkit` の `2026-09-03-macos-clipboard-verify-manual-result-v1.md` A-2）による。本作業でも、テキスト欄からコピーしただけで `utf8` と `utf16-external` の 2 型が載ることを確認した。

**利用者への警告（型の完全一致を前提にするな）はそのまま有効。** 機序の記述だけを直す。

### 5.2 `§1.6` lazy data provider — 限定条件が抜けている

現在の記述:

> Copy 成功 ≠ 貼り付け可能。Copy 後に Player を終了すると貼り付けられない

**実測では条件付きでしか成立しない。**

| 終了前に読まれたか | 終了後の貼り付け |
|---|---|
| **一度でも貼り付けた** | **できる** |
| **一度も読まれなかった** | **できない** |

一度読まれるとバイトが実体化し、プロセス終了後も pasteboard サーバー側に残る。**危険なのは「コピーしたので安心してアプリを閉じた」場合だけ**であり、実用上の意味が変わる。

### 5.3 `§1.6` pasteboard の解放 — 解放後の挙動が未記載

**解放済みの unique pasteboard 名を読むと `OK items=0` が返る。** エラーにならない。

`RemovePasteboard` は「pasteboard を消滅させる」のではなく **「内容を破棄する」**。名前は生き続け、空として読める。この結果、**1507 に到達する経路が現状の操作には存在しない。**

### 5.4 `§1.6` 監視 — 復帰時の配信単位が未記載

**非アクティブ中の変更は畳み込まれず、変更ごとに個別に配信される。**

```
ClipboardChanged: changeCount: 30, total: 1
ClipboardChanged: changeCount: 31, total: 2
```

29 を基準に開始し、30 と 31 の 2 変更が復帰時に 2 イベントとして届いた。**クリップボード履歴のような用途でも取りこぼさない。**

### 5.5 `§1.2` 受信側のサイズ制約が未記載

受信側の上限（`MaxResponseBytesPerRepresentation`）は 5.2.2 と 903 行に書かれているが、**1.6 の「ネイティブ側の制約」一覧には受信側の記述が無い。** 利用者から見て「他アプリが大きなデータを置くと読めない」ことが制約一覧から読み取れないため、1.6 に 1 項目追加する。

### 5.6 サンプルの欠陥 — `#26` が単独で判定できない

`CopyLocalOnlyTrue` と `CopyLocalOnlyFalse` が**同じ `PlainTextBody` を書く**ため、伝播が止まった場合と同じ文字列が再伝播した場合を区別できない。

サンプル計画 v3 のボタン採用基準 3（「駆動できる = 期待結果を観測できる」）を満たしていない。

本作業では、別デバイス側で先に独自の文字列をコピーしておく回避策で判定した。

**修正済み（コミット `1c5fb39`）。** 2 つの fixture を貼り付け先で判別できる別文字列に分け、ソース検査のガードテストを追加した。ガードが機能することは 2 通りの変異（2 つのリテラルを同一に戻す / ハンドラを共有 fixture に戻す）で確認している。**以後は回避策なしで #26 を判定できる。**

---

## 6. 到達不能なエラーコード

| コード | 理由 |
|---|---|
| **1507** | 解放済み pasteboard が `OK` を返すため経路が無い（§5.3） |
| **1513** | macOS 15.4 未満の機体が必要。26.3 では到達しない |
| **1514** | **拒否の導線が存在しない** |

**1514 は 2 経路で確認した。** システム設定（ネイティブ側が実施）に加え、本作業で `tccutil reset Pasteboard com.jonghyunkim.nativetoolkit.mac` を実行したが、`GetAccessBehavior` は `AlwaysAllow` のまま変化せず、ダイアログも出なかった。

ネイティブ側は 1514 の判別条件（`NSCocoaErrorDomain` + `NSUserCancelledError`）が当たるか未確認としており、**外れていれば静かに 1515 が返る**と記録している。本作業はその宿題を解消できなかったが、**`tccutil` 経路も使えないという情報を追加した。**

---

## 7. ネイティブ側結果との突き合わせ

`native-toolkit` の `2026-09-03-macos-clipboard-verify-manual-result-v1.md` と照合した。

| 観点 | ネイティブ側 | 本作業 |
|---|---|---|
| `AccessBehavior` | `alwaysAllow` | **一致** |
| プライバシー表示 | 出ない（26.3 / 15.4.1） | **一致** |
| `DetectMetadata` | 1515 | **一致** |
| `localOnly` の抑止 | 実機で確認 | **一致** |
| 型が無いときの `ReadData` | 成功 + データ無し | **一致** |
| 型の派生 | RTF で `utf16-external` が増える | **本作業の誤った結論を訂正**（§5.1） |
| **32 MiB を超える読み出し** | **記載なし**（ブリッジ層固有） | **上限は設計どおり。ただし iOS と乖離**（§4.1） |

**独立した 2 つの実装層で同じ結果が出ている**ことが、ブリッジ層が値を歪めていないことの証拠になる。

---

## 8. 未実施

| 項目 | 理由 |
|---|---|
| **1513**（15.4 未満） | 該当する機体が無い |
| **1514**（拒否） | 導線が存在しない（§6） |

---

## 9. 次のステップ

**対応済み:**

1. `PostBuildProcessor.cs` の修正（§4.2）— コミット `c6e9ec4`
2. 設計書の訂正（§5.1〜5.5）— `2026-09-05-macos-clipboard-design-v14.md`
3. サンプルの `#26` fixture 分離（§5.6）— コミット `1c5fb39`

**未対応:**

5. **`TooLarge` と `Malformed` の分離**（§4.1 / 計画書 v14 の V-14）。**リリース後に別計画で扱う。** あわせて上限値を 32 MiB のまま維持するか iOS の 64 MiB に揃えるかを判断する
6. `write-manual` → `release`

**マニュアルに書くべき実機由来の挙動:**

- 10 MiB 超の単一 item は、**一度も貼り付けられないままアプリが終了するとデータが失われる**
- **`Read` の型は書いた型と一致しない**ことがある。件数や完全一致で判定しない
- 他アプリが **32 MiB を超える** representation を置くと **`Read` が 9006 で失敗する**
- 監視は**アプリが非アクティブの間は止まり、前面復帰時にまとめて配信される**
- **監視と `CheckForegroundChange` は併用しない**
