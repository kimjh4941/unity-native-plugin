# レビュー結果

- 日付: 2026-08-15
- 対象ファイル: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v1.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- native Bridge の 15 関数、3 種のコールバック、リクエスト／レスポンス JSON、24 エラーコードを広く整理しており、Unity 側の実装範囲を追跡しやすい。
- iOS の callback API を同期化せず、`DllImport("__Internal")` と `MonoPInvokeCallback` を使う方針はリポジトリ規約に適合している。
- Manager + Bridge の依存方向、コンパイルガード、delegate の静的保持、main-thread dispatch、native 文字列の寿命を明示している。
- Android の既存公開型を変更せず、iOS 固有型を分離する判断には後方互換性上の根拠がある。
- parser/use case/C# Bridge のエラーを層別に整理し、`ReadData` の `data:null` など正常系との境界も明確にしている。
- EditMode、Editor PlayMode、実機手動確認の責務分担が具体的で、IL2CPP／ARM64／実機 iOS 18 以降という確認条件も記載されている。
- クリップボード本文、base64、検出値、pasteboard 名をログへ出さない方針が一貫している。

## 改善点

### 高優先度

#### 3.2 `[DllImport]` 宣言 / 9.1 `bool` のマーシャリング幅

- 問題点: native callback の第 1 引数は C の `bool`（1 byte）だが、計画は C# の素の `bool` を採用し、誤判定時だけ修正する方針になっている。既存 Manager の実績は、この新しい ABI 宣言が正しいことの根拠にはならない。ABI 不一致を既知のまま実装すると、IL2CPP 実機で成功／失敗判定が不定になる。
- severity: high
- 改善提案: `ClipboardOperationCallback` の `isSuccess` に `[MarshalAs(UnmanagedType.I1)]` を最初から付与し、C ヘッダの `bool` と幅を一致させる。必要なら native 側を `uint8_t` に固定する案も記録する。成功と失敗の両方を IL2CPP/ARM64 実機で確認する項目を追加する。

#### 5.6.2 / 5.6.6 同一操作の多重呼び出し

- 問題点: 操作ごとに単一の static callback スロットを使うため、同じ操作 A、B を並行実行すると、A の native 結果が B の per-call callback に渡り、その後の B の結果には callback が残らない。これは単に「1 回目の callback が呼ばれない」だけでなく、`Read`、`ReadData`、`LoadItem` 等で別リクエストのデータを B の結果として誤配送する契約になる。現行 PlayMode 案は Editor の即時失敗を連続実行するだけなので、この競合を再現できない。
- severity: high
- 改善提案: native API に request ID/context を追加して呼び出し単位で対応付けるか、Unity 側で同一操作を single-flight とし、後続呼び出しへ専用の busy 失敗結果を即時返す。現行方式を維持する場合でも「最新 callback が最新呼び出しの結果を受け取る保証はない」と公開契約へ明記し、制御可能な callback seam を使った逆順／遅延完了テストを追加する。

### 中優先度

#### 2.4 / 5.5 / 9.2 `moneyAmounts[].amount` の型

- 問題点: 要検証として `Decimal` または文字列の可能性を挙げているが、参照先の `ClipboardMoneyAmount.amount` は `Double` と確定している。`decimal? Amount` と `RawAmount` の二重公開は native モデルとずれ、不要な API 複雑性を生む。
- severity: medium
- 改善提案: 公開型を `double Amount` とし、JSON number を必須として解析する。将来のスキーマ変更に備えるなら、未知型を成功へ丸めず parse failure とする契約と互換性方針を別途明記する。

#### 3.3〜3.4 / 4.1 公開結果型の仕様

- 問題点: 結果型名と一部の特徴は列挙されているが、多くの public プロパティ、nullability、collection の空／null 契約、成功／失敗 factory、`ErrorCode`／`ErrorMessage`／details の配置が一覧化されていない。実装者が公開 API を補完する余地が大きく、テストの「不変条件」も型ごとの期待を確定できない。
- severity: medium
- 改善提案: すべての public payload/result/event 型について、プロパティ名・型・nullability・生成規則を表で定義する。成功時の data 欠落、失敗時の data、配列キー欠落を許容するかも型ごとに決める。

#### 5.5 malformed response / change event の契約

- 問題点: `ok == true/false` の経路はあるが、`ok` 欠落・型不一致、成功時の `data` 欠落／型不一致、必須フィールド欠落時の扱いが未定義である。また封筒なしの change event が null／壊れた JSON／scope 不正の場合に、`Unknown` を発火するのか破棄するのか、エラーをどう通知するのかがない。既定値で成功を合成すると Bridge 破損を正常結果として隠す可能性がある。
- severity: medium
- 改善提案: envelope と各 data の必須フィールド検証表を追加し、構造不正は B-6 相当の失敗に統一する。change event は「破棄して redacted error log」または専用 parse-error event のどちらかに固定し、テストを追加する。

#### 5.9 大容量レスポンスのメモリ契約

- 問題点: request 側の base64 膨張は記載されているが、`ReadData`／`LoadItem(Image)` の最大 64 MiB データを、native JSON文字列、managed string、JSON tree の string、decoded `byte[]` として重複保持する response 側のピークメモリが未評価である。手書き DOM reader は 85 MiB 前後の base64 をさらに複製しうる。
- severity: medium
- 改善提案: response parsing の一時割り当てと保持期間を契約に追加する。可能なら base64 値の余分なコピーを避ける設計、サイズ上限の事前確認、または大容量用途を `LoadItem(File)` へ誘導する明確な制限を定義し、数 MiB／上限近傍の実機メモリ確認を追加する。

#### 4. 変更ファイル一覧 / 0. XCFramework

- 問題点: 計画は 1.3.0 XCFramework への差し替えと新しい `.meta` を DoD に含める一方、変更一覧では C#／テスト以外の新規・削除ファイルを分類していない。現 worktree には 1.2.0 の削除と 1.3.0 の追加が存在するため、「既存変更なし」だけでは実装成果の境界が曖昧になる。
- severity: medium
- 改善提案: 1.2.0 XCFramework と root `.meta` の削除、1.3.0 XCFramework と Unity 生成 `.meta` の追加を「実装前に完了済みの前提変更」または「今回の成果物」に明確に分類する。エージェントが `.meta` を手書きしない規約と、Unity が生成した `.meta` を最終成果へ含めることを分けて記載する。

#### 5.2 `IosClipboardCopyOptions` の既定値

- 問題点: `readonly struct` の既定を `LocalOnly = true` と説明しているが、`default(IosClipboardCopyOptions)` はフィールドがゼロ初期化され `false` になる。nullable `options == null` の native 既定とは異なるため、利用者が `default` を渡した場合の privacy 動作が説明と逆になる。
- severity: medium
- 改善提案: `sealed class`／factory を使って不正な既定値を作れなくするか、struct の `default` は `LocalOnly == false` であることを公開契約へ明記し、`PrivacyPreservingDefault` など明示的な生成 API を用意する。null、省略、明示 true/false、`default` の builder test を追加する。

### 低優先度

#### 0. XCFramework のリンク保証

- 問題点: binary に 15 symbol が存在することから「リンク時エラーは発生しない」と断定しているが、plugin import metadata、target membership、framework embedding/link 設定の誤りでも解決失敗は起こりうる。
- severity: low
- 改善提案: 「symbol 欠落によるリンクエラーは除外できた」に表現を限定し、Unity import 後の iOS build/link smoke test を DoD に追加する。

#### 4.2 / 7.1 JSON builder のテスト件数

- 問題点: 4.2 は「15 リクエスト形状」、5.4 と 7.1 は requestJson を持つ 13 操作としており件数が不一致である。
- severity: low
- 改善提案: 「13 builder 出力 + requestJson を持たない 2 操作」に統一する。

#### 5.6.4 observation callback の後始末

- 問題点: `s_onChanged` は `StopObserving` 成功時にだけクリアするとしており、`StartObserving` の Editor failure、Bridge exception、native start failure 後には保持される。通常はイベントが来なくても、不要な参照保持と状態の曖昧さが残る。
- severity: low
- 改善提案: start failure 時にも、その呼び出しが登録した callback であることを確認してからスロットをクリアする。再入可能性を含む状態遷移テストを追加する。

## 不足項目

- C `bool` と C# `bool` の ABI を一致させる確定仕様。
- 同一操作を並行実行した場合の結果相関保証、または single-flight/busy 契約。
- 全 public result/payload/event 型の完全なプロパティ・nullability・不変条件一覧。
- malformed envelope、malformed success data、malformed change event の処理規則。
- 大容量 native response を解析するときの managed peak memory と実機確認条件。
- XCFramework 1.2.0 → 1.3.0 差し替えを含む変更ファイル分類。

## 総合評価

native API の調査範囲、エラー表、スレッド／メモリ／IL2CPP への言及、テスト層の分担は非常に充実している。一方、現状のまま実装すると callback ABI の不一致と同一操作の結果誤配送が起こりうるため、実装着手前の修正が必要である。さらに native で確定済みの money amount 型、公開結果モデル、壊れた JSON の扱い、大容量 response のメモリ契約、XCFramework の成果物境界を確定すれば、実装者間の解釈差を大きく減らせる。総合評価は「要修正（高優先度 2 件）」とする。
