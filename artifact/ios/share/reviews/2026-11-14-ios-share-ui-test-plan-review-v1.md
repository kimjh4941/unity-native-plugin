# iOS Share UI 自動テスト計画 v1 のレビュー v1

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-share-ui-test-plan-v1.md](../designs/2026-11-14-ios-share-ui-test-plan-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

指摘は 8 件です。いちばん重いのは SH-13（同じフレームで 2 回呼ぶ）の結果の取り方で、A1 にしました。次が P2-15 の「黙って捨てる」経路のテストが抜けている点（B・中心契約）です。ファイルは編集していません。

### R-1 [A2] 一次ソース: SH-9・SH-10 の 1.x の予想と、`2.0.0` のエラーの種類が両方違う
- **根拠**:
  - タグ `1.13.0` の `ShareRepositoryImpl.swift` では、`makeURL` が不正な URL を捨てずに `ShareError.invalidURL(string)` を投げます。文言は `"Invalid URL: \(value)."` です。
  - native-toolkit の `testS14_shareInvalidURLFails` も `Invalid URL: not a valid url.` で通しています。
  - `2.0.0` の `ShareMapping.error(for:)` は `.invalidURL` を `NTK_SHARE_ERROR_INVALID_URI`(17) に写します。`INVALID_PARAMETER` ではありません。
  - SH-01 の「不正な項目を捨てず」は、未知の `type` のことです（part2 の 6.4「今のブリッジは未知の `type` を黙って捨てた」）。不正な URL の話ではありません。
- **壊れ方**: 1.x の正しい行は SH-9 も SH-10 も `[share@fail] completed=False activity=<null> error=Invalid URL: not a url.` で、シートは出ません。SH-10 は振る舞いの差分ではなく、文言とコードだけの差です。SH-9 の `2.0.0` の列も違うコードを期待しています。どちらも流せば食い違いは見えますが、「1.x は捨てていた」という誤った前提が移行ガイドに入るおそれがあります。
- **直し案**: 1.x の予想を上の行に直します。`2.0.0` は「`INVALID_URI`。文言に URL を含まない。シートは出ない（SH-10 も同じ）」にします。

### R-2 [B・中心契約] 網羅: P2-15 の本当の「黙って捨てる」経路が 1 つも無い
- **根拠**: タグの時点で、次の 4 つが黙って落ちます。
  - **未知の type**: `UnityIosShareJsonParser` が `compactMap` で捨てます。`IosShareItem.type` は public なので C# から作れます。
  - **value が null**: C# が `null` のまま書き、パーサーが `as? String` に失敗して捨てます。
  - **items の null 要素**: C# の長さの確かめは通り、`IosShareJsonBuilder` が除き、ネイティブで `noValidItems` になります。
  - **excludedActivityTypes の null 要素**: `as? [String] ?? []` のため、**除外の一覧がまるごと消えて** Copy が出ます。
  - `2.0.0` の `ShareMapping` では、kind の範囲外と value の NULL は `INVALID_PARAMETER`、要素数 0 は `EMPTY_CONTENT`、文字列の配列の NULL 要素も拒否です。
- **壊れ方**: 段 3 の目的は 1.x と `2.0.0` の差を見つけることですが、P2-15 の破壊的変更そのものを記録しないまま終わります。計画を流しても気づけません。
- **直し案**: 次を足します。
  - UI で: 「文字列＋未知の type」（1.x はシートに文字列だけ）と「除外に null を混ぜる」（1.x は Copy が出る。シミュレーターだけ）。
  - Player のテストで: 「未知の type だけ」「`[null]`」「value が null だけ」（1.x はネイティブから `No shareable items were provided.`）。

### R-3 [A1] 一次ソース/記録の形: SH-13 で、呼び出しごとのコールバックが 1 本失われるのを記録できない
- **根拠**:
  - `IosShareManager.cs` は `s_onShare` が後勝ちで（"last-registered callback wins"）、最初に届いた結果が 2 つ目のコールバックを取って消します。
  - タグの `ShareSheetPresenter` は、最上位の画面を辿って `isBeingPresented` か `presentedViewController != nil` なら `presentationFailed(PresentationUnavailableError)` を投げます。そのため 2 つ目は `Failed to present the share sheet: The root view controller is not in a stable state to present the share sheet..`（ピリオドが 2 つ）で失敗する見込みです。Dialog の ID-14a（2 つとも出た）とは仕組みが違います。
  - 失敗が先に届いて 2 つ目のコールバックを取るので、シートを閉じた結果はイベントにしか来ません。
- **壊れ方**: 計画は結果の行をイベントから書くのかコールバックから書くのかを決めていません。
  - 今の `ShareScenario` のようにコールバックから書くと、行は 1 本だけになり、閉じた結果が無いように見えます。
  - Dialog のようにイベントから書くと 2 本になり、「1 回目のコールバックが 0 回」という 1.x の欠陥が隠れます。`2.0.0` は呼び出しごとにちょうど 1 回なので、「同じ」と誤って比べることになります。
- **直し案**: SH-13 だけは `[share1@cb]`・`[share2@cb]`・`[share@event]` を別の行 id で書き、それぞれ数えます。`Share.h` は「複数の共有のコールバックの順は約束しない」としているので、順ではなく集合で比べます。1.x の予想は上の行に（全文一致の教訓に合わせ、`..` も含めて）書きます。

### R-4 [A2] 実現性: SH-7 の「Copy が無い」が、偽って通りうる
- **根拠**: 「閉じる」は活動の一覧より先に出ます。native-toolkit の S12 は `UIActivityContentView` が出てから固定で 2 秒待ち（U-14）、`copyCount == 0` で確かめています。
- **壊れ方**: 一覧が描かれる前に確かめると、除外が効いていなくても通ります。
- **直し案**: S12 と同じ待ち方にします。同じ実行の SH-6 で同じ問い合わせが Copy を見つけることを、対照として条件にします。

### R-5 [A2] 実現性: SH-6・SH-8 に要る SystemUI の部品がハーネスに無い
- **根拠**: `Tools~/IosUiTests/NtkUiTests/SystemUI.swift` にあるのは `shareSheet` と `closeShareSheet` だけです。
  - native-toolkit の U-13 は、Copy を `.element` で取り、2 件以上当たれば押さないと決めています（私物のショートカットと名前が重なるため）。
  - iOS 18 のヘッダーの題には識別子が無く、ナビゲーションバーの子として読みます（`ShareSheet.observeHeaderTitle`。S5 は当初「読めるか未確認」でした）。
  - iOS 26.2 では閉じるのが `PopoverDismissRegion` です（U-16）。
- **直し案**: これら 3 つの部品を移すことを計画に書きます。シミュレーターの版を iOS 18 に固定するか、版で分けます。

### R-6 [A2] 記録の形: `activity=<生の値>` がハーネスの決まりに反する
- **根拠**: `IosUiHarness.cs` の決まりは「Values hold only what the scenario chose ... never other apps' data」で、今のパイロットは `none`/`set` しか書きません。実機で誤って押すと、他社の拡張の識別子（入っているアプリが分かる）が記録に残ります。
- **直し案**: `com.apple.UIKit.activity.` で始まるものだけ値をそのまま書き、ほかは `<other>`、null は `<null>` にします。

### R-7 [C] 記録の形: 道の伏せ方と、ログの写し
- シミュレーターの `persistentDataPath` には、Mac の利用者名とシミュレーターの UDID が入ります。Append と Debug.Log の前に `<data>` へ置き換えることを明記します。
- タグの bridge.m と Swift の `Log.d`・`Log.e` は、`contentJson`（パスを含む）とエラーの文言を端末のログに出します。端末のログを記録に写さないことを書き添えます。

### R-8 [C] 網羅: DeviceRequirements の区分と、SH-6 の書き込み
- `DeviceRequirements.json` の `clipboard` は「named pasteboard only」ですが、SH-6 は一般のクリップボードに書きます。about の文を直し、SH-6 に `simulatorOnly` を付けます。
- Simulator.app が閉じていることの確かめ（`pgrep`）はスクリプトにあるので、それを前提として書きます。

### 正しかった予想
次はタグの `1.13.0` と合っていました。
- キャンセル: `completed=False activity=<null>`
- SH-11・SH-12 の文言
- SH-15 の `No root view controller available to present the share sheet.`
- SH-15 の `2.0.0` の「前面で非活性でも出す」（`PresenterSelection.foreground`）
- SH-22 の順（イベントが先、コールバックが後）

**件数**: A1 1 / A2 4 / B 1（中心契約） / C 2

## 2. Codex

- **B — SH-9・SH-10：不正URLに対する1.x予想が誤り。** 計画:29–30は、不正URLを捨てると予想しています。しかし1.13.0は全項目を`try map`し、不正URLで処理全体を失敗させます（1.13.0:ShareRepositoryImpl.swift:44,76–91）。**修正:** 両方とも「シートなし、失敗」に変更。`not a url`ならエラーは`Invalid URL: not a url.`です（1.13.0:ShareError.swift:36–37）。実測で発見できるためA1には該当しません。
- **B — SH-9・SH-10：実際に項目を捨てるケースが未検証。** 1.13.0が捨てるのは、例えば未知の`type`です（1.13.0:UnityIosShareJsonParser.swift:36–45）。現在のURLケースでは、SH-01が規定する「破棄から`INVALID_PARAMETER`への変更」を検証できません（C ABI設計:578）。**修正:** 未知の`type`単独と、有効な文字列＋未知の`type`を追加。1.xは前者が項目なしの失敗、後者が文字列だけの共有になります（1.13.0:ShareUseCases.swift:24–27）。
- **B — SH-13：結果の本数だけでは個別コールバックの配送差分を見逃す。** 計画:33は本数を数えますが、現行C#は共有スロットを後の呼び出しで上書きし、最初の結果で消去します（IosShareManager.cs:107,148–153）。C ABIには要求を照合する`user_data`があります（Share.h:29–33）。**修正:** 呼出IDと通知元（イベント／個別callback）を記録し、A・Bそれぞれへの配送回数と結果を比較する。
- **B — SH-14・SH-15：背景からの呼び出し差分が未検証。** 計画:34–35は、表示後の背景移行と`foregroundInactive`時の呼び出しです。背景から呼ぶ場合、1.13.0は`foregroundActive`の探索に失敗します（1.13.0:ShareSheetPresenter.swift:48–50,98–102）。C ABIは`NOT_FOREGROUND`で完了する契約です（Share.h:98–100）。**修正:** 呼出時の状態が`background`であることを確認するケースを追加し、復帰後に失敗結果と通知回数を比較する。

（注）分類の要点: A1・A2 に該当する欠陥は報告されていない。上記はすべて B（中継したサブエージェントの注）。

## 3. 判断

利用者の指示（「判断は推奨案でいい」）に従い、止める基準（A1 と中心契約の B だけを直す）で決めた。

- **A1（R-3、Codex の SH-13 と同じ件）**: SH-13 は、呼び出しごとのコールバックを `[share1@cb]`・`[share2@cb]`、イベントを `[share@event]` として別の行 id で書き、順ではなく集合で数える。1.x の予想は「2 つ目は表示に失敗、コールバックは 1 本失われる」を全文で書く
- **中心契約の B**:
  - R-2・Codex の 2 つ目（黙って捨てる経路）: UI で「文字列＋未知の type」と「除外に null」（シミュレーターだけ）、Player で「未知の type だけ」「`[null]`」「value が null だけ」を足す
  - R-1・Codex の 1 つ目（不正な URL の予想）: 1.x は捨てずに `Invalid URL: …` で失敗、`2.0.0` は `INVALID_URI` に直す（A2 だが、誤った前提が移行ガイドに入るのを防ぐため、予想の行の直しで済むので直す）
  - Codex の 4 つ目（背景からの呼び出し）: シミュレーターだけの項目として足す（`state=background` を確かめる）
  - R-6（記録のプライバシー）: `activity` は `com.apple.UIKit.activity.` で始まるものだけそのまま書き、ほかは `<other>`
- **直さない（v2 の 6 章に理由つきで書く）**: R-4・R-5（A2。実装で native-toolkit の部品と待ち方を写す）、R-7・R-8（C）
