# レビュー結果

- 日付: 2026-09-27
- 対象ファイル: artifact/windows/notification/designs/2026-09-27-windows-notification-design-v3.md
- 機能名: notification
- プラットフォーム: Windows
- レビュー: 3 本を並行で行い、指摘を重ねて整理した。サブエージェント 2 本（観点を分担: ネイティブ境界 / 公開契約・テスト・自動化の前提）と Codex（全観点）。どれも読み取りのみ。ネイティブ境界の担当は、PowerShell 5.1 から `Windows.Data.Json` を実際に動かして 1.x の Stringify / TryParse の挙動を測った
- 出典の略記: [N] = ネイティブ境界、[T] = 契約・テスト、[X] = Codex

---

## 強み

- 構造体 2 つ（56 / 56）の全フィールドの位置、`extern` の型・`ref` / `out`・`size_t` → `UIntPtr`・Cdecl・コールバックの `(IntPtr, IntPtr)` はヘッダーと一致 [N][X]
- エラーコード 1〜8 をそのまま使える根拠（`WindowsNotificationApi.cpp:84-90`、`NotificationCApi.cpp:39-43`）は正しい [N]
- 1.x の判定の順を意図して保っている（`SetBadge` の -6 を初期化より先、Show で manager の有無を JSON より先）[T]
- J-3 で、W-01 が「2 回目の Initialize でネイティブを呼ばない」ことの検証になる（呼べば 2.0.0 は `NOT_SUPPORTED`）[T]
- W-04 の前提（`CustomActivator` と `LocalServer32`）が、2.0.0 の書き込み（`WindowsClassicActivator.cpp:394-431`）とテストの判定（`:138-145`）で噛み合う [T]
- `textBoxes` の NULL と `""`、audio、`crop`、`duration`、`scenario` の対応は、2.0.0 のビルダーと 1.x の両方と一致 [N]
- runtime の遅延 shutdown、`manager_free` → `runtime_free` の順、MTA の扱い、close 後の活性化を static delegate と NULL の release で受ける設計、dispatcher をメインスレッドで取っておく修正は、実装と合う [N][X]
- 版のついた文書を上書きしない（UI テスト計画は v2 を作る）[T]

## 改善点

### 高優先度

**A1（この通り実装すると契約が変わり、しかもコンパイルもテストも通る。直して再レビュー）**

1. **活性化の購読者の例外が、キューに積んだ処理から漏れる** [T]（4.5）
   - 1.x は、積んだラムダの中でも try/catch して `LogError` していた（`WindowsNotificationManager.cs:490-497`）
   - 4.5 の try/catch は OS スレッドの受け口だけを囲むので、購読者の例外が `UnityMainThreadDispatcher.Update`（`:59-62`）の外へ抜け、同じフレームに積まれたほかの処理が遅れる
   - 修正: 積むラムダも try/catch + `LogError` と書く
2. **`scheduledTimeUnixMs` の範囲外が 7 になる差分が 5.4 に無い** [T][X]。2.0.0 は時刻の変換に失敗すると `INVALID_PARAMETER`（`NotificationContentCApi.cpp:368-375`）。1.x は検査しなかった（`1.11.0:...WindowsNotificationManager.cpp:900-910`）
3. **「通知が無効なら 2」の順が崩れる** [T][X][N]
   - 1.x の Show / Schedule は、設定の確認を JSON の解析より先に行っていた（`1.11.0:...cpp:823-845,871-897`）
   - v3 は C# で先に JSON を読み、setter も先に呼ぶ。そのため、無効なときに、構文の誤り（3）、必須キーの欠落・型違い（7）、timestamp の範囲外（7）が 2 より先に出る。5.4 は構文の誤りしか挙げていない
   - 修正: 一般化して 5.4 に書くか、JSON を読む前に `get_setting` を呼んで 1.x の順を保つ
4. **`ShowNotification(null)` の扱いが決まっていない** [T]。1.x は `hstring{nullptr}` を渡していた（`1.11.0:...cpp:836`）。3 と決めて層 1 で固定する
5. **初期化に失敗すると bootstrap が下りる** [T]
   - `manager_create` が失敗したら `runtime_free` するので、`MddBootstrapShutdown` が走る（`NotificationRuntime.cpp:64-70`）
   - よくある流れは「`Initialize()` を既定の引数で呼んで必ず 7 → 正しい引数でやり直す」で、この 2 回目で同じプロセスの bootstrap を初期化し直す。1.x は bootstrap を残していた
   - 修正: runtime のハンドルは `OnDestroy` まで持ち続け、持っていれば `runtime_initialize` を飛ばす
6. **JSON の `null` の扱いが決まっていない** [N]
   - 1.x では、`"title": null` は `HasKey` が真になり、`GetNamedString` の例外で 5 になった（`1.11.0:...cpp:540-541`、catch は `:853-857`）
   - 雛形の `MacClipboardJsonReader.GetMemberOrNull` は、キーが無いときと null を同じ扱いにする（`:123-130`）。そのまま写すと成功に変わる
   - 公開ビルダーも、`Args` の値が null なら `null` を書く（`WindowsNotificationJsonBuilder.cs:119,166-168`）
   - 修正: 既知のキーの値が `null` なら「型の違い」として 7
7. **1.x が読んでいたサブキーが表に名指しされていない** [N]
   - `comboBoxes[].items`（省略可、`1.11.0:...cpp:712-719`）、`progress.title` / `valueStr` / `status`（`:651-654,802-809`）が表に無い
   - 公開ビルダーはこれらを出さないので、層 1 の往復テストではキー名の取り違え（例: C ABI に合わせて `valueString`）に気付けない
   - 修正: サブキーをすべて、必須か省略可かと一緒に表に書く
8. **GetAll の「1.x と同じ文字列」の規則が無い** [N][T]
   - 1.x の GetAll は `JsonArray.Stringify()` の出力そのもの（`1.11.0:...cpp:251-262`）
   - 非パッケージでは 8 になるので、層 2b では比べられない
   - 実測した規則:
     - キーは挿入順、空白なし
     - `\"` `\\` `\b` `\f` `\n` `\r` `\t` は短い形
     - そのほかの U+0000〜001F は **大文字**の `\u001F`（公開ビルダーは小文字 `x4`、`WindowsNotificationJsonBuilder.cs:227`）
     - `/`・DEL・非 ASCII・U+2028 はエスケープしない
     - id は整数の数字列
   - 修正: この規則を 4.4 に書き、層 1 の期待値の出典にする
9. **引数が 2 つ以上のボタンでは、活性化の JSON テキストが 1.x と変わりうる** [N]
   - 1.x は `args` を `JsonObject` の列挙順で `AddArgument` していた（`1.11.0:...cpp:689-690`）。列挙順は文書の順ではない（実測: `zeta,b,action,a,id` → `b,id,zeta,action,a`）
   - v3 は文書の順で渡すので、`raw_arguments` のキーの順が変わりうる。W-04 は `action` / `open` を含むかだけを見るので通る
   - 修正: 5.4 に書き、4.5 の「1.x と同じ JSON テキスト」を「ボタンの引数が 1 つのとき」に限る

### 中優先度

**A2（実装やテストで必ず表に出る）**

1. **Manager の呼び出し箇所のガード** [T]（5.1、3 章）
   - `extern` は Editor で消えるので、Manager が Editor でコンパイルできない
   - 修正: 前例 `WindowsClipboardManager.cs:823` のとおり、Manager の呼び出し箇所を `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` で囲み、`#else` では何もせずに戻す。`Application.platform` の確認はその内側に残す

**B（検証手段の穴）**

1. **E-1 と E-2 に実在のテストが無い** [T][N]
   - 修正: 層 2b に次の流れを 1 本足す（1.x でも通る）
     1. `DestroyImmediate(Instance.gameObject)`
     2. 新しい `Instance` で Show → 1、`GetNotificationSetting` → `Unknown`
     3. `Initialize(false, "", "")` → 7
     4. サンプルと同じ引数で `Initialize` → 成功
     5. Show → 成功し、通知センターに出る
   - これで J-3 の「新しい Manager は初回として扱う」、J-9、同じプロセスでの bootstrap の再初期化（移行後だけの新しい経路）もまとめて確かめられる
2. **どのテストからも呼ばれない `extern` がある** [T][X]
   - 対象: `list_*`、`open_settings`、`runtime_free`、`manager_free`
   - 非パッケージでは GetAll が 8 なので、list 系には届かない
   - 修正: `ntk_last_system_code` の結び付けテストを広げ、NULL を渡しても安全な関数（NULL は `INVALID_PARAMETER` を返す、free は何もしない）を `IntPtr.Zero` で呼び、例外が出ないことを確かめる。list の実際の読み取りはコードレビューで押さえる
3. **`FireResult` の形を書き、テストする** [T]
   - 1.x は、per-call callback が例外を投げると `NotificationOperationCompleted` が出ない（1 つの try、`:470-478`）
   - 5.5 の「1 回ずつ出す」は、try を 2 つに分ける実装とも読める
   - 修正: 「形はそのまま」と書き、層 2b に `LogAssert.Expect(Error)` のテストを足す
4. **GetAll と GetNotificationSetting が出すイベント** [T]
   - GetAll は `GetAllNotificationsCompleted` だけを出し、`NotificationOperationCompleted` は出さない（`:393-401`）。GetNotificationSetting はイベントを出さない
   - 修正: 5.5 に書き、7.3 で確かめる
5. **確かめないものを 8.1 に挙げる** [T][X][N]
   - GetAll の成功時の JSON（非パッケージでは届かない）
   - J-8（パッケージアプリ）
   - コールドスタートの活性化（native の調査記録も「試していない」、`2026-09-27-windows-unpackaged-activation-finding.md` 3 章）。1.4 と 5.5 の「1 回届く」は言い切らない
6. **7.3 の「全キー」の fixture を決める** [T]
   - 1 つの JSON に全部は入らない（`audio.type` は 1 値）
   - 画像には `StreamingAssets/app-icon.png` の `file:///` を使う。`expiration` は 60 秒以上にする
   - `alarm` / `incomingCall` / `urgent` は応答不可（DND）を越えてバナーが残り、前面の窓を奪いうる（要検証）
   - 修正: UI テスト計画 v2 に payload の中身を書く
7. **5.6 の手順 1 の記録の対象を 7.3 とそろえる** [T]。構文の誤りで 3、ボタン 6 個で 7、B-1 の流れも 1.x で通る
8. **dispatcher が破棄されたとき** [T]。`s_dispatcher` が古い参照のまま残る。受け口で Unity の null（破棄済み）を見て、ログを出して捨てる
9. **通知の設定の事前確認** [X]。SetUp か実行スクリプトで設定を確かめ、無効なら直し方を示してすぐ失敗させる
10. **JSON リーダーと 1.x のパーサーのずれ** [N]
    - `{"a":1e400}`: 1.x の `TryParse` は例外（0x83750008）を投げ、5 になっていた（実測）。v3 では 3 になるので 5.4 に書く
    - 重複したキー: 後の値が勝つ（実測）。リーダーの仕様と 7.1 に書く

### 低優先度

**C（記述の整合）**

- 4.1 の `extern` の本数: 「31 本」ではなく 46 本（ビルダー 22、ほか 24）[N][X]
- 1.2 の引用: `WindowsNotificationValidation.h:481-505` ではなく `:46-70`（このファイルは 102 行）[N][X]
- E-1 の「新しい Manager は作れない」は誤り。`Instance` は作り直せる [T]
- J-6 の「以後 -4」と 5.5 が合わない。manager が無いので、Initialize 以外は 1 を返す [T]
- E-11 を層 1 で試すには、判定を純粋な関数に切り出す（testing.md :80）[T]
- 5.1 に足すもの [T]
  - `artifact/README.md` の課題一覧（:37）
  - 実装結果ファイル
  - Player テストのクラスコメント（`:24` の v1 参照）
  - `GetAllNotifications` の XML コメント「retries with a larger buffer」（`:377`）
  - `WindowsNotificationResult.ErrorCode` の XML コメント（`:17`。-4 を足す）
- `timestamp` の切り捨ては 0 方向（`(long)` キャスト。1.x は `static_cast<time_t>`、`:609`）と書く。負の値で意味が分かれる [T][N]
- `UpdateNotificationProgress` の tag / group が null のときは `""`（1.x と同じ）と書く [T]
- 5.4 のサロゲートの行に、GetAll の tag / group も U+FFFD になることを足す（`Utf8.cpp:34`）[N]

## 不足項目

- なし（上の B-5 に含めた）

## 総合評価

移行の骨格は正しい。
- 公開 API を保つ方針、1.x の JSON を C# で読む設計、コールバックの扱い、ランタイムの寿命は、ネイティブの実装と合っている。
- 構造体と `extern` の型にも誤りは無い。

一方で **A1 が 9 件**ある（3 本の重なりを除いた数）。どれも「1.x の振る舞いのうち、表や差分の一覧に書かれていないもの」で、テストでは見つからない。
- 購読者の例外
- 予約時刻の範囲
- 無効のときの判定の順
- `null` の扱い（`ShowNotification(null)` と JSON の `null`）
- 初期化の失敗で bootstrap が下りる
- サブキー
- GetAll の文字列
- ボタンの引数の順

A2 はガードの 1 件。

**このまま実装に入ることは勧めない。** A1 を直した v4 で再レビューする。
