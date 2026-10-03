# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/windows/notification/designs/2026-09-27-windows-notification-design-v4.md
- 前回: artifact/windows/notification/reviews/2026-09-27-windows-notification-design-review-v3.md
- 機能名: notification
- プラットフォーム: Windows
- レビュー: v3 との差分に絞り、3 本を並行で行った。サブエージェント 2 本（ネイティブ境界 / 公開契約・テスト・自動化の前提）と Codex。どれも読み取りのみ。ネイティブ境界の担当は、1.x の JSON パーサー（`Windows.Data.Json`）と cppwinrt の `hstring` の振る舞いを実際に動かして測った
- 出典の略記: [N] = ネイティブ境界、[T] = 契約・テスト、[X] = Codex

---

## v3 の指摘の扱い

| v3 | 判定 | 理由 |
|---|---|---|
| A1-1 活性化の購読者の例外 | 解消 | 4.5 の 3。テストは新 B-4 [T][X] |
| A1-2 予約時刻の範囲 | 解消 | 5.4、E-17。`ToTime` の検査（`NotificationContentCApi.cpp:374`）より J-11 の 2 が先 [N][T] |
| A1-3 無効なら 2 | 一部 | 非パッケージは 1.x と同じ（-1 → 2、`0x80070490` → 0 は 1.x も同じ、`WindowsClassicActivator.cpp:709-712`）。取得が例外になる場合だけ差が残る（新 A1-2）[N][T] |
| A1-4 `ShowNotification(null)` | **前提が誤り** | 新 A1-1 [N][T] |
| A1-5 runtime の寿命（J-9） | 解消 | `manager_create` が失敗しても `ManagerOpened` は呼ばれない（`NotificationCApi.cpp:137-141`）。OnDestroy の shutdown は 1 回だけ [N][T] |
| A1-6 JSON の `null` | 解消 | 配列の要素の `null` は C [N] |
| A1-7 サブキー | 解消 | 1.x の全キーと一致。1.x が読まないときの扱いは新 A1-3 [N] |
| A1-8 GetAll の文字列 | 解消 | 再測で 4.4 の規則どおり [N] |
| A1-9 ボタンの引数の順 | 解消 | [N][T] |
| A2-1 ガード | 解消 | [T] |
| B | 解消（B-7、B-9、B-10 は一部） | 新 A1-1、B-1、A1-4 [N][T][X] |
| C | 解消 | `extern` 46 本を数え直して一致、型も一致 [N][T] |

## 強み

- J-11 と J-9 はネイティブの側から見て妥当。参照カウント、shutdown 待ち、manager の数に問題が無い [N]
- 7.3 の NULL で安全な `extern` の一覧は、ソースで全部確かめた。クラッシュも副作用も無い（`last_system_code` が 0 になるだけ）[N][T]
- 作り直しの流れは 1.x で通る（`Uninit` の `CoRevokeClassObject`、再 Init の 7、Show の 1、設定の -1）。サンプルの購読も、シーンをテストごとに読み直すので壊れない [T]
- per-call callback の例外のテストは、1.x の `FireResult` が 1 つの try なので 1.x でも通る [T]
- 新しい A1 / A2 は Codex からは無い [X]

## 改善点

### 高優先度

**A1（この通り実装すると契約が変わり、しかもコンパイルもテストも通る。直して再レビュー）**

1. **`null` の payload は、1.x ではプロセスが落ちていた** [N][T]
   - 1.x は `TryParse(hstring{ jsonPayload })`（`1.11.0:...cpp:836,885`）。cppwinrt の `hstring(wchar_t const*)` は `std::wstring_view` を作る（`base.h:3028-3030`）ので、NULL を渡すと `wcslen` で `0xC0000005` になる（実測）。落ちるのは、初期化済みで通知が有効なときだけ
   - v4 の 2 章と 4.2 の「空文字として 3」は誤り。7.3 の「`null` で 3（1.x で記録）」を 1.x で流すと、テスト用の Player が落ちる
   - 修正: 5.4 に「1.x はプロセスが落ちた → 3」を書き、テストは「移行後だけ」に移す
2. **設定の取得が例外になると、1.x は 5、v4 は 2** [N][T]
   - 1.x の Show / Schedule は `Setting()` の例外を `hresult_error` の catch で受けて 5 にしていた（`1.11.0:...cpp:827,853-857`。パッケージの `PackagedBackend::Setting` は例外を捕まえない、`:265-276`）
   - 2.0.0 は `catch(...)` で -1 にし（`WindowsNotificationManager.cpp:800-812`）、`HRESULT_FAILURE` にする（`WindowsNotificationApi.cpp:317-320`）。J-11 ではそれが 2 になる
   - 修正: 5.4 に書く（主にパッケージ。未検証）
3. **1.x は、実際に読んだキーの型しか見ていなかった** [N]
   - `audio.type = "mute"` なら uri・event・loop を読まない（`1.11.0:...cpp:757-762`）。`"uri"` なら event を読まない（`:769-778`）
   - そのため `{"audio":{"type":"mute","event":5}}` は 1.x で成功する。v4 の表のとおりに実装すると 7 になる
   - 修正: 「1.x がその分岐で読まないキーは、型も見ない」と書き、層 1 に足す
4. **数値と深さの扱いが 1.x とずれる** [N]
   - 1.x のパーサー（実測）:
     - `1e400` も `1e-400` も例外（`0x83750008`）
     - 入れ子は 511 段まで通り、512 段で例外（`0x8007050C`）
   - 雛形の `MacClipboardJsonReader` は、数値の範囲を読むときまで調べない（`:170-178,719-780`）ので、未知のキーの `1e400` が成功になる。深さの上限も `MaxDepth = 64`（`:476`）
   - 修正:
     - 構文を読む段階で全部の数値を変換する。±∞、または 0 でない値が 0 に丸められたら 3
     - 深さの上限は 1.x に合わせる
5. **非パッケージのプロセスで `Initialize(isPackaged: true)` を呼ぶと、bootstrap を呼ばない** [N]
   - 1.x は `isPackaged` にかかわらず、先に `initWinAppSdk` を呼んでいた（`WindowsNotificationManager.cs:229-236`）
   - v4 は引数で決めるので、bootstrap の無い状態で `AppNotificationManager` に触る
   - 修正: runtime を呼ぶかは、実際にパッケージのプロセスか（`GetCurrentPackageFullName`）で決める

### 中優先度

- A2: なし（[N] の A2-1 は上の A1-1 に含めた）

**B（検証手段の穴）**

1. **設定の事前確認は、SetUp の `Initialize` が成功した直後に置く** [T]。manager が無いと `GetNotificationSetting` は `Unknown` なので、前に置くと最初のテストが必ず落ちる
2. **作り直しの流れが失敗すると、後のテストが全部落ちる** [T]
   - 2.0.0 の bootstrap の shutdown のあとの再初期化は、native のテストがフックで代替していて、実物では試されていない（`NotificationRuntime.cpp:85`）
   - 失敗すると Manager が未初期化のまま残り、以後の SetUp の `Initialize` も同じ理由で失敗し続ける
   - 修正: 別の fixture にして最後に流す
3. **結果として返す失敗のログの重さ** [T]
   - 想定していない `LogError` があるとテストが落ちる
   - 1.x が `LogError` を出すのは、bootstrap の失敗（`WindowsNotificationManager.cs:233`）、`FireResult` の例外（`:477`）、活性化の受け口（`:496,502`）だけ
   - v4 の「HRESULT をログに出す」「例外の型を出す」、`get_setting` の失敗を `LogError` にすると、7 / 3 / 8 / 1 を期待するテストが移行後だけ落ちる
   - 修正: 「結果として返す失敗は `Debug.Log` か `LogWarning`」と書く
4. **判定の順の関数と、活性化の購読者の例外を層 1 で試す** [X]
   - manager の有無・`get_setting` の結果・設定の値から「JSON へ進む / 1 / 2」を決める処理を純粋な関数にし、組み合わせを層 1 で固定する
   - 活性化のイベントを出す処理を `internal static` の関数にし、購読者が投げても `LogError` になって外へ漏れないことを層 1 で確かめる
5. **`SetBadge(-7)` の判定の位置** [T]
   - 順を「プラットフォーム → -6 → manager」と書く。`#if` の外に置くと Editor で 7 が出て、J-4 が崩れる
   - 7.2 で `SetBadge(-7)` も呼ぶ

### 低優先度

**C（記述の整合）**

- DLL が無いとき、1.x はどのメソッドでも `DllNotFoundException` を投げていた。v4 では `Initialize` 以外は 1 か `Unknown` になる。5.4 の行を全メソッドに広げる [T]
- 5.1 に足すもの [T]
  - `Initialize` の XML コメント「iconUri は任意」（`:222`）。1.x でも 2.0.0 でも、無ければ 7
  - 実装結果ファイルの名前を、既存の `...-implementation-feature-result-v1.md` の形に合わせる
  - `artifact/README.md` の unreachable の行の進捗
- `expiration` が `long` に収まらないときの扱いを書く（C# の `(long)` は、範囲外だと値が決まらない）[N]
- 埋め込みの NUL（`\u0000`）は、NUL 終端の UTF-8 で切れる。5.4 に足す [N]
- 5.4 の「±約 922 兆 ms を超える秒」は「±922,337,203,685 秒」と書く [N]
- 配列の要素が `null`（`"buttons":[null]`）のときも 7 と書く [N]

## 不足項目

- なし

## 総合評価

v3 の A1 はほぼ解消し、J-11 と J-9 はネイティブの側から見て妥当。

新しい **A1 は 5 件**。どれも 1.x を実測して分かった、1.x の振る舞いと v4 の記述のずれ。
- `null` の payload（1.x では Player が落ちる。記録の手順そのものを止める）
- 設定の取得が例外になったとき
- 1.x が読まないキーの型
- 数値と深さ
- `isPackaged` と bootstrap

直し方はどれも決まっている。

**v5 で直し、その部分だけ再確認する。**
