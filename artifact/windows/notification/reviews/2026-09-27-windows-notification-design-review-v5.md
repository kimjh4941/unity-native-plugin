# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/windows/notification/designs/2026-09-27-windows-notification-design-v5.md
- 前回: artifact/windows/notification/reviews/2026-09-27-windows-notification-design-review-v4.md
- 機能名: notification
- プラットフォーム: Windows
- レビュー: v4 との差分に絞り、2 本で行った。サブエージェント 1 本（ネイティブ境界と契約・テストの両方。1.x の `Windows.Data.Json` を実測）と Codex。どれも読み取りのみ
- 出典の略記: [S] = サブエージェント、[X] = Codex

---

## v4 の指摘の扱い

| v4 | 判定 | 理由 |
|---|---|---|
| A1-1 `null` の payload | 解消 | [S][X] |
| A1-2 設定の取得の例外 5 → 2 | 解消 | [S][X] |
| A1-3 読まないキー | 一部 | `loop` の記述が誤り、`null` の規則と矛盾（新 A1-1）[S][X] |
| A1-4 数値と深さ | 一部 | 小数表記で 0 に丸まる値（新 A1-2）、深さの数え方（C）[S] |
| A1-5 runtime を呼ぶかの判断 | 一部 | 15700 は正しい。判定の API のほかの戻り値が未定義（新 A1-3）[S][X] |
| B-1 設定の事前確認の位置 | 解消 | [S][X] |
| B-2 作り直しのテストの隔離 | 未解消 | `[Order]` はクラスに付けられず、「最後に流す」にもならない（新 B-1）[S][X] |
| B-3 ログの重さ | 一部 | `LogError` の箇所の数え漏れ（新 B-2）[S] |
| B-4、B-5 | 解消 | [S][X] |
| C | 解消 | [S][X] |

## 強み

- `null` の payload の記述は、cppwinrt の `base.h:3028-3030` と 1.x の `cpp:836,885` に一致 [S]
- 設定の取得の例外の 5 → 2 は、1.x の `cpp:827,853-857` と 2.0.0 の `WindowsNotificationManager.cpp:800-812`、`WindowsNotificationApi.cpp:317-320` で裏付けた [S]
- `Destructive` と `--include-destructive` の扱いは、`scripts/verify_unity_windows.sh:221,226` と一致 [S]
- `SetBadge` の判定の順は 1.x のネイティブと一致 [S]

## 改善点

### 高優先度

**A1（この通り実装すると契約が変わり、しかもコンパイルもテストも通る。直して再レビュー）**

1. **`audio.loop` は、1.x では `type` にかかわらず読まれていた** [S][X]
   - 1.x の `ValidatePayload` は、`type` にかかわらず `audio.loop` を `GetNamedBoolean` で読み、`duration` が long でなければ 7 を返す（`1.11.0:...cpp:494-501`）。`ApplyAudio` でも、`loop` は uri の分岐より前で読む（`:765`）
   - v5 のとおりに作ると、`{"audio":{"type":"mute","loop":true}}`（`duration` なし）が、1.x の 7 から成功に変わる
   - v5 の「どのキーも `null` なら 7」と「読まないキーは型を見ない」も矛盾している [X]
   - 修正:
     - 規則を「1.x がどこかで読むキー（`ValidatePayload` を含む）だけ型を見る。`null` も同じ」にする
     - `audio.loop` はどの `type` でも読んで `set_audio` に渡す（2.0.0 の検証は kind を問わず `loop` を見る、`WindowsNotificationValidation.h:49`。ビルダーは mute のとき `loop` を捨てる、`WindowsNotificationBuilder.h:14`）
     - 層 1 に mute の組み合わせを足す
2. **小数表記で 0 に丸まる値** [S]
   - 1.x のパーサー（実測）は、`0.`＋0 が 325〜400 個＋`1` を例外にせず 0 として通す。v5 の規則では 3 になる
   - 1.x の判定は一貫しておらず、正確に写すのは難しい
   - 修正: 5.4 に既知の差分として書き、層 1 に入れる
3. **パッケージの判定の API が、想定外の値を返したときの扱い** [X]
   - v5 は `APPMODEL_ERROR_NO_PACKAGE` だけを決めている。NULL のバッファで呼ぶと、パッケージのプロセスでは `ERROR_INSUFFICIENT_BUFFER` が返る（Microsoft Learn、GetCurrentPackageFullName）
   - 修正: `NO_PACKAGE` / `ERROR_INSUFFICIENT_BUFFER` / そのほか、の 3 つに分け、そのほかは `Initialize` の結果を 5 にする。判定を純粋な関数にして層 1 で試す

### 中優先度

- A2: なし

**B（検証手段の穴）**

1. **作り直しのテストを「最後に流す」手段** [S][X]
   - `com.unity.ext.nunit` 2.0.5 の `OrderAttribute` はメソッドにしか付けられない（`ValidOn=Method`）。クラスに付けると CS0592
   - メソッドに付けても、UTF は `Order` の付いた子を**先に**流す（`CompositeWorkItem.cs:237-273`）
   - 修正: `QuitsThePlayer` と同じく専用のカテゴリにし、スクリプトで別の Player の実行に分ける（`verify_unity_windows.sh:313` と同じ形）。プロセスごと分かれるので、順番に頼らない
2. **1.x が `LogError` を出す箇所の数え漏れ** [S]。GetAll の per-call とイベントの catch（`WindowsNotificationManager.cs:400`）が抜けている。4 箇所にする

### 低優先度

**C（記述の整合）**

- 深さは「最上位を含めてコンテナ 512 個まで通り、513 個で 3」と書き、object だけを 512 段入れ子にして通るテストを足す（実測）[S]
- 5.4 の DLL が無いときの行: 「ほかのメソッドは 1」だが、`SetBadge(-7)` は判定の順で 7 になる [S]

## 不足項目

- なし

## 総合評価

新しい A1 は 3 件で、どれも JSON リーダーと判定の関数の書き方の直し。設計の骨組みは変わらない。
B-1 はそのまま実装するとコンパイルエラーになる。

**v6 で直し、その部分だけ再確認する。**
