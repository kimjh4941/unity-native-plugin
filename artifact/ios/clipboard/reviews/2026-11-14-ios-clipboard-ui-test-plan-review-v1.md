# iOS Clipboard 自動テスト計画 v1 のレビュー v1

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-clipboard-ui-test-plan-v1.md](../designs/2026-11-14-ios-clipboard-ui-test-plan-v1.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。1 回目なので全体を見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

A1 は 0 件です。1.x の予想の誤りは 4 件ありますが、どれも流せば分かるので A2 にしました。2.0.0 の中心の変換（エラーの文字列の写し、CL-12、CL-13/14、NULL のコールバック）に項目が無い点を B にしました。

### R-1 [A2] 一次ソース: C# から届かない CL を「確かめる」としている
- **根拠**: `IosPasteboardCreationRequest.Named("   ")` は `ArgumentException` を同期で投げます（`IosClipboardPayloads.cs:120-125`）。このため CP-20 はネイティブの `CLIPBOARD_INVALID_NAME`（`CreatePasteboardUseCase`）まで届きません。CP-10 も同じで、`Append` に options の引数は無く、`BuildAppendJson` は `options` キーを出さないので CL-02 には届きません。CL-08 の scope の省略は、C# の `scope must not be null` で止まります。
- **壊れ方**: CP-20 は 1.x も 2.0.0 も throw して「同じ」になり、CL-07 を確かめたことにされてしまいます。CP-10 の 2.0.0 の列は検証できません。
- **直し案**: CP-20 の 1.x の予想を「`ArgumentException`（同期）」に直します。CL-02・07・08（と、enum で止まる CL-09 の未知の値）は「C# から届かない、確かめない」として 4 章に移します。

### R-2 [A2] 一次ソース: CP-22 は期限を試せない
- **根拠**: `ClipboardContentValidator.validateExpirationDate`（タグ `1.13.0` の 73〜76 行）は過去の日時を `CLIPBOARD_INVALID_EXPIRATION` で断ります。作ったばかりの名前付きの pasteboard なので、Copy が失敗しても Read は 0 項目です。
- **壊れ方**: Copy の成否を照らさないと、「期限切れで消えた」という誤った記録になります。
- **直し案**: 過去の日時の項目は「Copy が `CLIPBOARD_INVALID_EXPIRATION` で失敗する」に直し、エラーの写しの項目にします。期限そのものは、数秒先の日時を指定して待ってから Read する別の項目にします。

### R-3 [A2] 一次ソース: CP-12 の予想が違う
- **根拠**: `ClipboardRepositoryImpl.snapshot`（82〜99 行）で、`matchingTypes` が効くのは `MatchingItemIndexes` だけです。`TypeIdentifiers` には全部の型が入ります。また Copy は項目を置き換えるので、「2 種類を Copy」では項目が 1 つしか残りません。
- **直し案**: MultipleText と Url を Append するなどして 2 項目にします。予想は「`MatchingItemIndexes` が url の項目の添字だけ」に直します。

### R-4 [B 中心契約] 網羅: エラーの写し（11.3 の逆引き）と D-13 がほぼ空振りする
- **根拠**: CP-24 の対象のうち、ネイティブまで届くのは CP-21 だけです（CP-20 は R-1、CP-23 の `CLIPBOARD_BUSY` は C# の層）。1.x の操作のコールバック `(bool, code, message)` には details がありません。details が付くのは `providerLoadFailed`・`fileCopyFailed`・`detectionFailed`・`unknown` だけです（`ClipboardError.diagnosticDetail`）。
- **壊れ方**: D-13 の「既知の差分」は、どの項目でも観測されません。
- **直し案**: ネイティブに届く失敗を足します。
  - 消した pasteboard に Read → `CLIPBOARD_UNAVAILABLE`
  - テキストに `LoadItem(Image)` → `NO_MATCHING_ITEM`
  - `Color(1.5,…)` → `INVALID_COLOR`
  - CustomData の UT 型をドットの無い名前にする → `INVALID_TYPE`
  - 壊れた PNG → `INVALID_IMAGE_DATA`
  - 過去の期限 → `INVALID_EXPIRATION`（R-2）
  - 前書きで名指しされた `INVALID_URL` は `validateURL` を読んでから値を選ぶ

  それぞれ `Code` と `Message` を全文で照らします。D-13 は CB-3 で `Domain`・`NativeCode` を記録して確かめます。あわせて表記を実物に合わせます（`CLIPBOARD_INVALID_NAME`、`CLIPBOARD_CANNOT_REMOVE_GENERAL`、`Details` ではなく `Domain`/`NativeCode`）。

### R-5 [B] 網羅: CL-12（読み込み中の CancelLoads）が無い
- **根拠**: CP-16 は読み込みが無いときだけです。1.x のブリッジは async の `loadItem` を await するので（`UnityIosClipboardManager.swift:259`）、CancelLoads の完了が先に来て、読み込みの `CLIPBOARD_CANCELLED` が後に来ます。CL-12 は 2.0.0 ではこの順序を約束しないと書いており、包みが ID ごとに取り消す形に書き直す箇所です。
- **直し案**: 同じフレームで `LoadItem(Text)` と `CancelLoads` を呼ぶ項目を足します。数えるのは、`CLIPBOARD_CANCELLED` がちょうど 1 回、CancelLoads の成功が 1 回、その順序（順序は記録だけにする）です。

### R-6 [B] 網羅: 監視の置き換え・停止の後・Removed のイベント
- **直し案**: 次の項目を足します。
  - `StartObserving` を 2 回呼び、後の `onChanged` だけに届くこと（CL-13「前の登録を外す」）
  - Stop の後に Copy して、2 秒間 0 回（CL-14、Receiver の `discardUndelivered`）
  - Start と Stop が同じキー `observation` を使うので、Start の直後に Stop を呼ぶと `CLIPBOARD_BUSY`
  - 監視中の RemovePasteboard で `Kind=Removed` が来るか

### R-7 [B] 網羅: NULL のコールバック（前書き）に項目が無い
- **根拠**: 1.x の `OnDestroy` は `clipboardStopObserving(null)`・`clipboardCancelLoads(null)` を呼びます（`IosClipboardManager.cs:407,417`）。2.0.0 では NULL のコールバックは `INVALID_PARAMETER` で、止める処理が行われません。
- **直し案**: 破棄の後に Copy してイベントが 0 回であることを確かめる項目を足します。`s_isTerminated` はプロセスが終わるまで戻らないので、Player の最後に流すか、別の起動にすると計画に書きます。

### R-8 [A2] 実現性・一次ソース: CB-2・CB-3 の予想と時間
- **CB-2**: `Clipboard.h` には、Read で貼り付けを拒んだときは「失敗ではなく、型も文字も無い 1 項目」とあり、main も止まります。結果の行に型の数を入れないと区別できません（`items=1 types=0 text=<null>`）。また Read では、11.3 の #24 の `LOAD_FAILED` の経路を通りません。native-toolkit の CB2 と同じく `LoadItem(Text)` の拒否を足します。
- **CB-3**: 1.13.0 の `ClipboardTimeouts.detection` は 5 秒で、答えるまでの時間も含みます。XCUITest が遅いと、`TIMED_OUT` を「拒否の結果」として記録してしまいます。5 秒以内に押すこと、および `TIMED_OUT` なら失敗にすることを書きます。

### R-9 [B] 実現性: scope の省略は一般の pasteboard になる
- **根拠**: どの API も `scope = null` は general です。実機で 1 か所でも scope を書き忘れると、私物の内容を読みます。device-testing.md では、クリップボードを読むテストは毎回の許可が要ります。
- **直し案**: 必ず scope を取る補助の関数だけで呼ぶこと、`finally` の RemovePasteboard を待つこと、待ち時間を過ぎた操作が残って次のテストを BUSY にしないこと（テストごとに in-flight が空かを照らす）を、3 章に書きます。

### R-10 [C] 実現性: CP-17 の「ちょうど 1 回」
- **根拠**: ネイティブは `object: pasteboard`（`UIPasteboard(name:create:false)` で別に解決したもの）で通知を受けます。名前付きの pasteboard で通知が来るかは流して決めます。
- **直し案**: Dialog v3 と同じく 2〜3 秒の窓を置き、`ClipboardChanged` と `onChanged` を別々に数え、`Kind`・`TypesAdded` を記録します。0 回だったときの記録のしかたも先に決めておきます。

### R-11 [C] 一次ソース: 細かな予想
- CP-2: 書いているのは `UTType.plainText` で、`public.plain-text` です。`public.utf8-plain-text` ではありません。
- CP-15: Url の成功は null にならず、失敗なら `NO_MATCHING_ITEM` の見込みです（`firstMatchingProvider`）。
- CP-18: 1 回目は確実に false です（tracker の初期化。ヘッダーにも「first check gives 0」）。
- CP-19: 「消せる」を、消した後の Read の結果で確かめます。
- CP-23: 共通のイベントは 2 回来ます。

### R-12 [C] 記録の形（プライバシー）
大きな問題はありません。CB の行で長さだけを書くのは良い形です。足すものは次の 3 つです。
- 「ペーストを許可」を押す前に、出どころがランナーかを照らします（native-toolkit の `pasteAlertNamesKnownSource`）。
- `DeviceRequirements.json` の `clipboard` の説明（「名前付きだけ」）は、CB の項目には合わないので直します。
- CP-19 の unique の名前は記録に書きません。

**件数**: A1 0、A2 4（R-1、R-2、R-3、R-8）、B 4（R-4 は中心契約、R-5、R-6、R-7、R-9 のうち R-4 を含めて計 5）、C 3（R-10、R-11、R-12）

訂正: B は R-4（中心契約）、R-5、R-6、R-7、R-9 の 5 件です。合計 12 件です。

## 2. Codex

- **B／CP-12** — 「URL の型だけが入る」は誤りです。`matchingTypes` が絞るのは `matchingItemIndexes` だけで、型一覧・項目数は全体を返します。**修正:** 異なる型の2項目を用意し、全体の型一覧と URL 項目のインデックスを別々に比較する。根拠: native-toolkit **タグ `1.13.0`**、`ios/IosLibrary/IosLibrary/Clipboard/Data/Repository/ClipboardRepositoryImpl.swift:82–98`。

- **B／CP-20** — `Named("   ")` は C# の生成時に `ArgumentException` を投げ、ネイティブの `INVALID_NAME` 完了には到達しません。**修正:** 公開 API のテストは例外の検証に変更し、ネイティブの空白名検証は別のブリッジ用テストに分ける。根拠: [IosClipboardPayloads.cs:120–126](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardPayloads.cs:120)。

- **B／CP-22** — 過去の期限はコピー前に拒否されます。「期限切れで0項目」の予想では、期限拒否と内容消去を取り違えます。**修正:** 先に既知の内容を書き、Copy が `CLIPBOARD_INVALID_EXPIRATION` で失敗し、元の内容が残ることを比較する。根拠: native-toolkit **タグ `1.13.0`**、`ios/IosLibrary/IosLibrary/Clipboard/Application/UseCase/CopyContentUseCase.swift:32–35`、同ディレクトリの `ClipboardContentValidator.swift:73–77`、`ios/IosLibrary/IosLibrary/Clipboard/Domain/Model/ClipboardError.swift:58`。

- **B／CP-10** — 「options を持つ Append の拒否」は、この公開 API では実行できません。`Append` に options 引数がありません。**修正:** CP-10 は通常の追加結果を比較し、JSON の options キー拒否を確認するならブリッジ用テストとして明示する。根拠: [IosClipboardManager.cs:880–883](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardManager.cs:880)、native-toolkit `artifact/topics/ios-c-abi/designs/2026-11-03-ios-c-abi-c-abi-design-part2.md:537`（CL-02）。

- **B／CP-16** — 未完了ロードがないため、2.0.0 で必要になる要求 ID の保存・個別取消を検証できません。**修正:** 完了を保留できる fixture を用意し、未完了 `LoadItem` に対する取消と、取消結果の1回だけの配送を比較する。取消完了とロード完了の順序は固定しない。根拠: native-toolkit **タグ `1.13.0`**、`ios/IosLibrary/IosLibrary/Clipboard/Data/Repository/ClipboardItemLoaderImpl.swift:83–113`、上記 C ABI 設計書 `:547`（CL-12）、`dist/1.14.0/ios/include/NativeToolkitC/Clipboard.h:271–273`。

- **B／CP-17** — Start が1回だけなので、旧リスナーを外し忘れる移行不具合を検出できません。1.x は再 Start 時に以前の監視を停止しますが、C ABI は複数リスナーを登録できます。**修正:** 開始完了後に再 Start し、旧登録への配送がなく、新登録へ1回届くことを検証する。根拠: native-toolkit **タグ `1.13.0`**、`ios/IosLibrary/IosLibrary/Clipboard/IosClipboardManager.swift:319–330`、上記 C ABI 設計書 `:548`（CL-13）。

- **B／CP-24** — 指定された CP-20／21／23 では、D-13 の診断情報消失を比較できません。ネイティブの名前・General 削除エラーには診断情報がなく、C# の Busy エラー生成にも domain/code はありません。**修正:** CB-3 などの検出失敗で `Domain`／`NativeCode` も記録し、その消失を比較する。根拠: native-toolkit **タグ `1.13.0`**、`ios/IosLibrary/IosLibrary/Clipboard/Domain/Model/ClipboardError.swift:131–139`、`ios/IosLibrary/IosLibrary/Clipboard/Data/Repository/ClipboardRepositoryImpl.swift:135–139`、[IosClipboardErrorInfo.cs:55–65](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/iOS/Clipboard/IosClipboardErrorInfo.cs:55)、上記 C ABI 設計書 `:530`。

## 3. 判断

利用者の指示（「判断は推奨案でいい」）に従い、止める基準（A1 と中心契約の B だけを直す）で決めた。A1 は 2 本とも 0 件。

- **中心契約の B（直す）**:
  - R-4・Codex の CP-24（エラーの写しと D-13）: ネイティブまで届く失敗の項目を足す（`CLIPBOARD_UNAVAILABLE`、`NO_MATCHING_ITEM`、`INVALID_COLOR`、`INVALID_TYPE`、`INVALID_IMAGE_DATA`、`INVALID_EXPIRATION`、`INVALID_URL`）。`Code` と `Message` を全文で照らす。`Domain`・`NativeCode` は CB-3（検出の失敗）で記録する。表記を実物に合わせる
  - R-5・Codex の CP-16（CL-12）: 読み込み中の `CancelLoads` を足す。順序は記録だけにする
  - R-6・Codex の CP-17（CL-13・14）: 監視の置き換え、停止の後、Start の直後の Stop、監視中の RemovePasteboard を足す
  - R-7（NULL のコールバック）: 破棄の後にイベントが 0 回の項目を、別の起動で流すものとして足す
- **A2 だが、予想の行を直すだけで済むので直す**（誤った前提が移行ガイドに入るのを防ぐ。Share v1 と同じ扱い）: R-1・Codex の CP-20 と CP-10（C# から届かない CL を 4 章へ）、R-2・Codex の CP-22（過去の期限は Copy の失敗。期限そのものは別の項目）、R-3・Codex の CP-12（`MatchingItemIndexes`）、R-8（CB-2 の行の形、CB-3 の 5 秒）、R-11（細かな予想）
- **B（実機の安全）**: R-9 は 3 章の約束に書く（scope を必ず取る補助の関数、`finally` の後始末、テストごとの in-flight の確かめ）
- **C**: R-10（窓を置いて別々に数える）と R-12（出どころの確かめ、`clipboard` の区分の説明、unique の名前を書かない）は、手間が小さいので取り込む
