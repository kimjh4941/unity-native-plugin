# 実装レビュー結果 v3（Codex による独立レビュー）

## 基本情報

- 日付: 2026-09-07
- 機能名: clipboard / 対象プラットフォーム: Windows
- レビュアー: **Codex CLI 0.153.4（`gpt-5.6-sol`、reasoning effort: medium）**
  止める基準の「レビュアーを替えて 1 回通す」を満たすため、実装者とは別のモデルに依頼した
- レビュー対象: `git diff dad06ad~1..HEAD -- Packages/`（レビュー v2 への修正 5 コミット）
- 実装計画: `artifact/features/clipboard/designs/2026-09-05-windows-clipboard-design-v8.md`
- 前回レビュー: `artifact/features/clipboard/reviews/2026-09-07-windows-clipboard-implementation-feature-review-v2.md`
- 実行方法: `codex exec -s read-only`（作業ツリーへの書き込みを禁止して実行）

---

## レビュー概要

対象は `feature/UNT-11` の `dad06ad~1..HEAD -- Packages/`、Runtime 5 ファイル・テスト 6 ファイルです。指定どおり読み取り専用で確認し、Unity／テスト実行、ファイル作成・編集は行っていません。`git diff --check` は問題ありませんでした。

結論は **要修正（重大）** です。A 区分は **5 種**あります。

## A. 振る舞いを変える

### A-1. 非同期化したキャンセル登録の破棄中に、再利用された requestId の別要求をキャンセルできる

対象: [WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2359)、[WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2511)

1. **事実**

   `ReleaseCancellationRegistration` は登録をスレッドプールで遅延 `Dispose()` します。一方、登録済み callback が dispatcher に積んだ処理は、元要求の ticket/generation を確認せず、`requestId` が現在レジストリに存在するかだけを確認しています。

2. **再現シナリオ**

   - 要求 A（native id = 63）が完了し、配送時に cancellation registration の破棄がスレッドプールへ委譲される。
   - pool 側の `Dispose()` が始まる前に A の token をキャンセルすると、古い callback が `requestId = 63` のキャンセル処理を dispatcher に enqueue する。
   - dispatcher 実行前に要求 B が開始され、native が 63 を再利用する。
   - 2511 行目は 63 が「存在する」ことしか確認しないため、A のキャンセル要求が B に対して `CancelRequest(63)` を実行する。
   - B が意図せず `Canceled` になり、A の token が別の利用者の要求を変更する。

3. **根拠**

   設計 7.6.5 は、メインスレッドでキャンセルを実行する際に「**ticket と generation が現在も有効か確認**」することを明記しています。非同期 `Dispose()` 自体は IL2CPP 上の問題を確認できませんでしたが、この寿命の延長が新しい競合窓を作っています。

### A-2. `ShutdownWithDrain` のコルーチン起動失敗・中断で `s_drainRunning` が永久に残り、終了不能になる

対象: [WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:821)、[WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:828)、[WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2820)

1. **事実**

   `ShutdownWithDrain` は `s_drainRunning = true` を立ててから `StartCoroutine` を呼びますが、例外、null 戻り値、コルーチン中断に対するロールバックがありません。フラグと waiters を片付けるのは `DrainRoutine` の末尾だけです。

2. **再現シナリオ**

   - Manager の GameObject を非アクティブにして `ShutdownWithDrain(callback)` を呼ぶ。
   - `StartCoroutine` が起動できず例外または null になっても、`s_drainRunning` は true、callback は未配送のまま残る。
   - その後終了要求が来ると `OnWantsToQuit` は 2820 行目で「既存 drain に合流した」と判断して false を返す。
   - 実際には走っている coroutine がないため `ResumeQuit` は永久に呼ばれず、アプリを終了できない。
   - 同様に、drain 中に `StopAllCoroutines()` または Manager 破棄が起きると、waiter は完了しない。`OnDestroy` の単発 shutdown は drain の末尾処理を代行していません。

3. **根拠**

   設計 7.4 は成功・タイムアウト・終端失敗のいずれでも quit を通し、アプリを終了不能にしないことを要求しています。`ShutdownWithDrain` は最終結果を event/callback に配送する契約です。

これは前回 v2 **A-9 の部分的な未解消**です。`OnWantsToQuit` 自身の起動失敗は処理されましたが、先に失敗した通常 drain へ合流する経路が残っています。

### A-3. quit が通常 drain に合流すると、shutdown の最終 callback/event を enqueue した直後に終了する

対象: [WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2738)、[WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2743)、[WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2748)

1. **事実**

   drain の最終結果は `Deliver` により dispatcher へ enqueue されるだけです。その直後、quit が合流していれば `ResumeQuit` → `Application.Quit()` が実行されます。shutdown waiter は非同期要求レジストリには登録されていないため、`OnDestroy` のドレインでも救済されません。

2. **再現シナリオ**

   - `ShutdownWithDrain(callback)` を開始し、native が一度以上 `Busy` を返す。
   - drain 中に終了要求が来て `OnWantsToQuit` が既存 drain に合流する。
   - coroutine の最終フレームで callback/event が enqueue された後、同じ継続内で `Application.Quit()` が呼ばれる。
   - dispatcher の `Update` はそのフレームでは既に終了しているため、次の `Update` が来なければ callback/event は一度も発火しない。

3. **根拠**

   設計 5.3 は `ShutdownWithDrain` が最終結果を共通 event と per-call callback に配送すると定め、設計 8.4 は callback の未配送を許していません。

### A-4. 複数の drain waiter とイベント購読者が multicast 一括呼び出しされ、先頭の例外で後続が未配送になる

対象: [WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:821)、[WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2943)

1. **事実**

   `ShutdownWithDrain` の複数 callback は `s_drainWaiters += onResult` で一つの multicast delegate に集約されます。`InvokeInOrder` は `common?.Invoke` と `perCall?.Invoke` をそれぞれ一度しか呼ばず、invocation list の各要素を隔離していません。

2. **再現シナリオ**

   - 同じ drain に callback A、B を登録する。
   - A が例外を投げる。
   - catch は multicast 呼び出し全体の外側なので B は一度も呼ばれない。
   - 同様に、`ClipboardOperationCompleted` の第1購読者が投げると第2購読者以降は呼ばれない。

3. **根拠**

   per-call callback を渡した各呼び出しは、その最終結果を受け取る契約です。前回 v2 A-14 が指摘した購読者隔離も、そのまま残っています。横断課題であることは Windows 側で契約違反が存在しない理由にはなりません。

これは前回 v2 **A-14 の未解消**です。さらに今回の `s_drainWaiters` 導入で、問題がイベント購読者だけでなく複数の per-call callback にも拡大しています。

### A-5. 可用性 JSON の必須キーがネスト内にあっても、トップレベルのキーとして受理される

対象: [WindowsClipboardJsonParser.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardJsonParser.cs:164)、[WindowsClipboardJsonParser.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardJsonParser.cs:203)

1. **事実**

   `ContainsKey` は JSON を構文解析せず、文字列中の `"key"` の直後に空白と `:` があるかだけを調べます。キーの階層は確認しません。

2. **再現シナリオ**

   次の有効な JSON を渡します。

   ```json
   {"meta":{"historyEnabled":true},"roamingEnabled":false}
   ```

   形状検査は両キーがあるとして通ります。しかし `JsonUtility` がトップレベルの `historyEnabled` を見つけられず false を補うため、`TryParseAvailability` は成功として `historyEnabled=false`、`roamingEnabled=false` を返します。壊れた native payload が「履歴無効」という正常結果に化けます。

3. **根拠**

   設計 2.4 は両フラグを可用性オブジェクトのトップレベル必須キーとし、設計 8.4 は壊れた結果を成功に正規化しないことを要求しています。

## B. 検証手段を変える

### B-1. キャンセル競合テストが ticket/generation を検証していない（中心契約）

対象: [WindowsClipboardManagerIntegrationTests.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:375)

1. **事実**

   ワーカースレッドからのキャンセル、登録の破棄回数は検証されていますが、「古い token callback を enqueue →元要求を配送→同一 native id を再利用→enqueue 済み callback 実行」の順序がありません。

2. **再現シナリオ**

   ticket/generation の照合を native id の存在確認だけに壊しても、現在の全テストは通ります。実装は実際にその壊れた状態です。

3. **根拠**

   設計 7.6.5、9.2 の「キャンセルと完了の競合」「ticket と generation の確認」。

### B-2. drain の起動失敗・中断が未検証（中心契約）

対象: [WindowsClipboardManagerIntegrationTests.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:212)

1. **事実**

   drain テストはすべて正常に開始された coroutine が末尾まで進む前提です。非アクティブ GameObject、`StopAllCoroutines`、drain 中の Manager 破棄がありません。

2. **再現シナリオ**

   `StartCoroutine` 後に末尾の `s_drainRunning = false` と waiter 配送へ到達しない実装でも、全テストが通ります。

3. **根拠**

   設計 7.4、9.2 の quit 再開・最終結果配送。

### B-3. `QuitActionForTests` が「次の Update が存在しない」実機条件を再現しない（中心契約）

対象: [WindowsClipboardManagerIntegrationTests.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:176)、[WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2857)

1. **事実**

   Editor seam は `Application.Quit()` の代わりに counter を増やして return するため、その後も Update が継続します。また、通常 drain に quit が途中合流するテストがありません。

2. **再現シナリオ**

   最終 callback を dispatcher に enqueue した直後に quit する現在の実装でも、Editor では次フレームに dispatcher が動くためテストが通ります。Player でのみ callback が失われます。

3. **根拠**

   設計 5.3、8.4、9.2。シームは本番の「quit 後は Update が保証されない」という条件を再現できていません。

### B-4. multicast のテストが「共通 event 全体対 per-call」しか見ていない（中心契約）

対象: [WindowsClipboardManagerDispatchTests.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardManagerDispatchTests.cs:58)

1. **事実**

   例外テストは共通 event delegate 一つと per-call delegate 一つの隔離だけです。共通 event の複数購読者、同じ drain に参加する複数 callback は検証していません。

2. **再現シナリオ**

   invocation list を一括 `Invoke` して最初の例外で後続を止めても全テストが通ります。現実装がその形です。

3. **根拠**

   前回 v2 A-14、設計 9.1 の配送例外検証、各 per-call callback の最終結果配送契約。

### B-5. 必須キー検査の入力が階層を識別できない（網羅系）

対象: [WindowsClipboardJsonParserTests.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardJsonParserTests.cs:249)

1. **事実**

   追加テストはキー名が「値」にあるケースを検出しますが、同じキー名がネストしたオブジェクトの正規キーとして現れるケースを検証しません。

2. **再現シナリオ**

   `ContainsKey` を現在の単純な `IndexOf` 型検査のままにしても全テストが通ります。`{"meta":{"historyEnabled":true},"roamingEnabled":false}` は成功扱いになります。

3. **根拠**

   設計 2.4、9.1 の可用性オブジェクト／壊れた JSON 検証。

## C. 記述だけを変える

### C-1. teardown を「初回試行だけ」とする設計と、毎試行ドレインする実装・テストが食い違う

対象: [WindowsClipboardManager.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs:2625)、[WindowsClipboardManagerIntegrationTests.cs](C:/Users/User/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs:154)、[windows-clipboard-design-v8.md](C:/Users/User/Desktop/unity-native-plugin/artifact/features/clipboard/designs/2026-09-05-windows-clipboard-design-v8.md:1131)

1. **事実**

   設計 7.4／7.6.3 は teardown drain を `FinishShutdownAttempt` の「初回試行」と定義しています。実装は無条件に毎試行 `DrainRequestRegistry()` を呼び、テストも「EveryShutdownAttemptDrains」を正として固定しています。

2. **再現シナリオ**

   `Draining` に入った後で拒否要求を登録し、2回目の shutdown 結果を注入すると、実装はその要求を同期ドレインします。設計文からは2回目を teardown と判定できません。

3. **根拠**

   設計 7.4 段階1、7.6.3。二重配送は `TryClaim` により防げていますが、正本を実装意図へ合わせる必要があります。

### C-2. 結果 v5 の変更ファイル数が誤っている

対象: [windows-clipboard-implementation-feature-result-v5.md](C:/Users/User/Desktop/unity-native-plugin/artifact/features/clipboard/results/2026-09-07-windows-clipboard-implementation-feature-result-v5.md:169)

1. **事実**

   「テスト 5」と記載されていますが、差分には Runtime テスト5ファイルと PlayModeテスト1ファイル、計6ファイルがあります。

2. **再現シナリオ**

   `git diff --name-status dad06ad~1..HEAD -- Packages/` でテストファイルが6件列挙されます。

3. **根拠**

   設計 6.2、レビュー workflow の変更ファイル一覧の正確性。

### C-3. 結果 v5 の「B 21/21 解消」は現状と一致しない

対象: [windows-clipboard-implementation-feature-result-v5.md](C:/Users/User/Desktop/unity-native-plugin/artifact/features/clipboard/results/2026-09-07-windows-clipboard-implementation-feature-result-v5.md:25)

1. **事実**

   B 区分を全解消としていますが、上記 B-1〜B-5 の検証穴が残っています。

2. **再現シナリオ**

   stale cancellation、drain 起動失敗、quit 合流、複数 waiter、ネストキーの各実装を現在の欠陥状態にしても、既存テストは通ります。

3. **根拠**

   設計 9.1〜9.2およびレビュー workflow の「追加したテストが、壊すと落ちること」。

## 問題なしと判断した観点

- `ClassifyOperationGuard` のプラットフォーム優先は設計 7.5 と一致しています。リポジトリ内に旧 `NotInitializedByHost` を前提にする利用コードやサンプルはありません。
- `ReadRaw` は内側の native guard 外ですが、Manager 全体が `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` なので非 Windows Player には入りません。P5 違反はありません。
- `Marshal.AllocHGlobal`／`FreeHGlobal` は再試行を含め `finally` で釣り合っています。
- shutdown 完了時だけの COM 解放、未完了・終端失敗時の保持、二重解放防止に新しい問題は見つかりませんでした。
- 遅延レンダリング D-1〜D-9、provider/cache 世代管理に今回差分由来の新しい問題は見つかりませんでした。
- P1〜P5、他プラットフォームファイル変更、公開 API シグネチャに問題はありません。
- スレッドプールへの `CancellationTokenRegistration.Dispose()` 委譲自体に明白な IL2CPP 非互換はありません。問題は A-1 の寿命競合です。

## 総合評価

- **総合評価: 要修正（重大）**
- **A 区分: 5 種**
- 前回レビュー v2 の未解消:
  - **A-9: 部分的に未解消**（通常 drain の起動失敗後に quit が永久待機）
  - **A-14: 未解消**（今回、複数 drain waiter にも影響が拡大）

読み取り専用指示に従い、レビュー結果ファイルは保存していません。