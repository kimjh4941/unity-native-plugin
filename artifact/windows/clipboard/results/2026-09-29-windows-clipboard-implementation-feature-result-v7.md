# 実装結果レポート v7（Windows Clipboard の C ABI 2.0.0 への移行）

## 基本情報

- 日付: 2026-09-29
- 機能名: clipboard
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-12
- 計画ファイル: `artifact/windows/clipboard/designs/2026-09-27-windows-clipboard-design-v12.md`
- 親課題: `artifact/topics/windows-c-abi-2/README.md`
- 前の実装結果: `2026-09-08-windows-clipboard-implementation-feature-result-v6.md`（1.x での実装。上書きしない）
- 移行前の基準: `50fe7bb`

## 0. 状態サマリー

設計 v12 の 5.6 の手順 1〜8 をすべて終えた。これで Windows の 3 機能（Dialog、Notification、Clipboard）が C ABI 2.0.0 に移り、1.x の DLL は同梱物から消えた。

| 手順 | 内容 | コミット | 状態 |
|---|---|---|---|
| 1 | 1.x のまま、7.3 の「1.x で記録」のテストを足す | `afee02c` | 済み。1.x で Player 53/53 |
| 2 | Bridge の変換の関数と層 1（`WindowsClipboardJsonParserTests` から移すものを含む） | `716147c` | 済み |
| 3 | Bridge のネイティブの宣言と呼び出し | `c218191` | 済み。EditMode 1195/1195、PlayMode 209/209、Player ビルド |
| 4 | Manager の置き換え、Dispatch のテストの整理、層 2a | `dd31275` | 済み。EditMode 1146/1146、PlayMode 210/210、Player ビルド |
| 5 | 層 2b: 今の Player テスト、S-2 の全ボタンと押した回ごとの期待値（J-12 を除いて不変）、M-19 | `0670868` | 済み。Player 93/93、作り直し 1/1、M-19 と S-2 合格 |
| 6 | 1.x の追加のピンを消す | `05195a5` | 済み。Player に入る DLL は `windows-native-toolkit-capi-2.0.0.dll` だけ（md5 が dist と一致） |
| 7 | `verify_unity_windows.sh --il2cpp` と、IL2CPP の Player で 3 機能の層 2b を 1 回（J-10） | `6dc71d4`、`722ec41` | 済み。Mono と IL2CPP の両方で Player 93/93（5 章） |
| 8 | 実装結果ファイル（この文書）、`testing.md`、親課題 README | この文書と同じコミット | 済み |

## 1. 実装サマリー

### 1.1 計画ファイル由来の実装

- **2 層（Bridge + Manager）と共通部。** `WindowsClipboardCApi`（Bridge。純粋な変換は `#if` の外、`extern` と呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR`）と、Dialog で作った共通部 `WindowsNativeToolkitCApi`（DLL 名、版の判定、UTF-8、`Common.h` の関数）
- **公開 API は変えない。** メソッド 33 本・イベント 12 個・`Operation*` 定数 27 個（J-2）・結果型・エラーコードの値・`WindowsClipboardJsonBuilder`・ペイロードの型・呼べるスレッド・届き方。例外は名前空間の移動（Q-4 の例外、3.4）
- **J-1** 履歴の `Timestamp` は FILETIME のティックのまま。Unix ミリ秒から C# で変換し、0 は 0、溢れる値も 0
- **J-3** base64 は 1.x のネイティブと同じ規則で C# が検証・復号し、不正なら `InvalidParameter`
- **J-4** Editor では今と同じくネイティブを呼ばず `PlatformUnavailable`
- **J-5** 終了はセッションが無ければ成功。close が成功したら free。成功しないうちは free しない（放棄しない）
- **J-6** `Initialize` の最初に `(ntk_version() >> 16) == 2`。違う・DLL が無い・関数が無い・形式が違うは `BridgeUnavailable`（1001）で、セッションは作らない
- **J-7** コールバックの受け口は static の delegate（`[MonoPInvokeCallback]`）、`user_data` / `release` は `NULL`
- **J-8** 遅延描画は 1 段にし、cache を無くした
- **J-9** `LogError` の箇所は今のまま。結果として返す失敗に新しい `LogError` は足していない
- **J-11** `EnsureStaApartment` は今のまま。Clipboard と Notification をどちらもメインスレッドで使う
- **J-12** S-2 の `ForceInitializeWhileDraining` は、この押下の `ShuttingDown` の行と次の押下の任意の 1 行だけ期待値を変えた（実際の結果は 3.4）
- **J-13** API ごとの例外の捕まえ方は今のまま、`BadImageFormatException` だけ足した
- 読み出しは 1 回の呼び出しで取り（2 回呼び出しと `BufferTooSmall` の再試行は無い）、`FORMAT_UNAVAILABLE` / `EMPTY` だけを空の成功にする。ほかの失敗のコードは失敗のまま
- 履歴の完了は、受け口の中でハンドルから値を写し終える。写す途中の例外でも、結果を必ず 1 回届ける（E-20）
- `WindowsClipboardJsonParser` とそのテストを消した。公開の `WindowsClipboardJsonBuilder` は残し、「2.0.0 からは内部では使わない」と XML コメントに書いた

### 1.2 実装時の追加判断

- **`set_history_handlers` の解除。** `NULL` の構造体を渡すため、同じ関数を指す 2 つ目の `extern`（`ntk_clipboard_set_history_handlers_none`、`EntryPoint` が同じ）を置いた
- **読み出しの失敗のログ。** 1.x の「sizing failed」は採寸が無くなったので「read failed」にした。`LogError` の箇所は同じ（J-9）。層 2a の期待値の文言も合わせた
- **テスト用のフックの形（設計 5.1）。** `InjectCompletionForTests(id, err, items?)`、`InjectAvailabilityForTests`、`HistoryConversionForTests`、`ReadTextForTests(Func<(code, text)>)`、`RenderForTests(name, setTarget)`
- **J-12 の任意の行に `frame=3` を書かない。** 設計 5.1 は次の押下「Initialize」に `?initClipboardManager OK frame=3` を足すとしていた。チェッカーは必須の行を先に使うので、`frame=3` の行がこの押下の自分の行より先に来ると、自分の行が余って落ちる（実際に落ちた）。フレームを名指ししない `?initClipboardManager OK` にした
- **IL2CPP でテストの補助のプロセスを起動する部品。** IL2CPP の Player は `System.Diagnostics.Process` の `Start` と `MainModule` に対応していない。最初の IL2CPP の実行（2026-09-27）では、PowerShell を起動するテストと Player の exe のパスを読むテスト、計 39 本がテストの本題の前に落ちた。`Tests/PlayMode/WindowsTestProcess.cs`（`CreateProcessW` とパイプ、`GetModuleFileNameW`、`K32EnumProcesses`）で置き換えた。Dialog の閉じ役、Notification の通知センターの操作、Clipboard の「別のプロセスから読む」の 2 か所が使う
- **`--il2cpp` の範囲。** Player テスト（本体と作り直し）と S-2 のチェッカー。M-19 と層 3 は Mono だけ。チェッカーに `--without-block` を足し、IL2CPP では quit のブロックを「流していない」と報告する（Quit のボタンは未自動化として渡す）
- **設計 v12 の誤り。** 4.3 の「`GetFormats` の 0 件は `Success`（`IsEmpty = false`）」は誤り。0 件の成功は `IsEmpty = true` で、1.x の `[]` と同じ（`ToListResult_NoEntries_IsASuccess_AsThe1xEmptyArrayWas`）。設計の「1.x と同じ」という意図のほうに合わせた。版の付いた設計書は上書きしない

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Clipboard/WindowsClipboardCApi.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardCApiTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsTestProcess.cs`

### 2.2 既存変更

- `Runtime/Windows/Clipboard/WindowsClipboardManager.cs`（1.x の `DllImport` 27 本、2 回呼び出し、2 段の描画と cache、履歴の JSON を置き換え）
- `Runtime/Windows/Clipboard/WindowsClipboardErrorCode.cs`、`WindowsClipboardHistoryItem.cs`、`WindowsClipboardStringListResult.cs`、`WindowsClipboardPayloads.cs`、`WindowsClipboardJsonBuilder.cs`（XML コメント）
- `Plugins/Windows/VERSION.txt`、`Editor/Build/PreBuildProcessor.cs`（追加のピンを消した）
- `Tests/Runtime/WindowsClipboardManagerDispatchTests.cs`（2.3）、`WindowsClipboardResultTests.cs`
- `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs`（フック、E-20、E-22、文言）
- `Tests/PlayMode/WindowsClipboardPlayerTests.cs`、`WindowsClipboardSampleScreenDriver.cs`、`WindowsDialogSamplePlayerTests.cs`、`WindowsNotificationSamplePlayerTests.cs`
- `scripts/windows_clipboard_sample_expected.py`（J-12）、`scripts/verify_unity_windows.sh`（`--il2cpp`）、`scripts/check_windows_clipboard_sample_log.py`（`--without-block`）
- `agent-rules/coding-rules/common.md`、`agent-rules/coding-rules/testing.md`、`artifact/topics/windows-c-abi-2/README.md`、`artifact/README.md`

### 2.3 削除

- `Runtime/Windows/Clipboard/WindowsClipboardJsonParser.cs`、`Tests/Runtime/WindowsClipboardJsonParserTests.cs`
- `Plugins/Windows/unity-windows-native-toolkit.dll`（1.x）

### 2.4 Dispatch のテストで消したもの・移したもの（設計 5.1）

`WindowsClipboardManagerDispatchTests` は 66 本から 46 本（`TestCase` を数えると 60）になった。消したのは 26 本、足したのは 6 本。

| 1.x のテスト | 扱い | 今の置き場所 |
|---|---|---|
| `ClassifyFirstRead_TheseCodesMeanAnEmptyClipboard` | 移した | `ReadText_EmptyAndFormatUnavailable_AreAnEmptyClipboard`、`WindowsClipboardCApiTests.ClassifyRead_OnlyEmptyAndFormatUnavailableAreEmptySuccesses`、`ToResults_EmptyAndFormatUnavailable_AreEmptySuccesses` |
| `ClassifyFirstRead_AFailureWithZeroSizeIsNeverNormalizedToEmpty`、`ClassifySecondRead_OtherCodesFailBeforeTheBufferIsTouched`、`Read_AFailureOnTheSecondCallIsReportedWithoutTouchingTheBuffer`、`Read_AFailureOnTheFirstCallNeverAllocatesAtAll` | 移した（「失敗のコードを空の成功にしない」） | `ReadText_AFailureIsNeverNormalizedToEmpty`、`WindowsClipboardCApiTests.ToResults_OtherFailures_StayFailures` |
| `Read_ReturnsTheTextTheSecondCallWrote` | 移した | `ReadText_APasteWithContentIsUnaffected`、`ReadText_CutsAtTheFirstNul` |
| `Read_AnEmptyStringIsAValueRatherThanAnEmptyClipboard` | 移した | `ReadText_AnEmptyPasteIsAnEmptyStringAndNotAnEmptyClipboard`（既存）、`WindowsClipboardCApiTests.ToTextResult_AnEmptyStringIsAValue_ExceptForThePreferredFormat` |
| `Read_ByteApi_ReturnsExactlyWhatWasWritten` | 移した | `WindowsClipboardCApiTests.ToBytesResult_CarriesTheBytes` |
| `Render_AThrowingProviderIsContainedAndReportsNoSize` | 移した（大きさの部分を除く） | `Render_AThrowingProviderIsContained`、`WindowsClipboardCApiTests.Render_AProviderThatThrows_IsUnknown` |
| `ClassifyFirstRead_NoneWithASizeNeedsABuffer`、`ClassifyFirstRead_NoneWithoutASizeIsEmpty`、`ClassifyFirstRead_BufferTooSmallWithASizeNeedsABuffer`、`ClassifyFirstRead_ZeroBytesIsAnEmptySuccessOnlyForTheByteApis`、`ClassifySecondRead_NoneMeansTheBufferMayBeRead`、`ClassifySecondRead_TheClipboardChangingBetweenCallsIsAnEmptySuccess`、`ClassifySecondRead_BufferTooSmallMeansTheContentGrew`、`Read_WhenTheContentGrowsBetweenTheCalls_SizesItAgain`、`Read_ThatKeepsResizingGivesUpRatherThanLoopingForever`、`Read_ASizeTooLargeToAllocateIsRefusedRatherThanTruncated` | 消した | 2 回呼び出し（採寸してから読む）そのもの。2.0.0 では 1 回の呼び出しで取るので、対応する仕組みが無い |
| `Render_FirstPhase_ReportsTheSizeAndAsksToBeCalledAgain`、`Render_SecondPhase_WritesTheBytesAndRepeatsTheSameSize`、`Render_SecondPhase_UsesTheCachedBytesEvenWhenTheProviderWouldChange`、`Render_SecondPhaseWithoutACachedPayloadFailsInsteadOfRegenerating`、`Render_ATooSmallBufferReportsTheRealSize`、`Render_AProviderGenerationChangeTakesTheCacheWithIt`、`Reservation_OnFailure_KeepsTheCachedBytesAsWellAsTheProviders` | 消した | 2 段の描画と cache そのもの（J-8）。予約の失敗で provider を残す規則は `Reservation_OnFailure_KeepsThePreviousProvidersUntouched`（既存）に残る |

足したもの: `Render_HandsTheProvidersBytesToTheTargetOnce`、`Render_ATargetFailureIsReturnedAsItIs`、`Render_AThrowingProviderIsContained`、`ReadText_EmptyAndFormatUnavailable_AreAnEmptyClipboard`、`ReadText_AFailureIsNeverNormalizedToEmpty`、`ReadText_CutsAtTheFirstNul`。provider が無い・null・空は `WindowsClipboardCApiTests` の `Render_WithoutAProvider_IsInvalidParameter`、`Render_NullOrEmptyBytes_AreInvalidData_AndReachNoTarget`。

`WindowsClipboardJsonParserTests`（25 本）から移したもの: id が無い項目を捨てる、text が無い、contentTypes が無い、時刻が 0（`ToHistoryItem_*`）、`ToUtcTime` の範囲外（`ToUtcTime_OutOfRangeValueReturnsNullInsteadOfThrowing`）。`ToUtcTime` の正しい値は `ToHistoryItem_CarriesEveryField` が見る。ほかは JSON の読み方そのものなので消した。

## 3. エラー契約反映

### 3.1 エラーケース実装反映（設計 v12 の 6 章）

| # | 状況 | 検証 |
|---|---|---|
| E-1 | メインスレッド以外から呼ぶ | 2b（S-7）合格 |
| E-2 | 初期化の前・終了のあと | 2b（ブロック D）合格 |
| E-3 | Editor | 2a 合格 |
| E-4 | DLL が無い・major が 2 でない | 1（`FromLifecycleException`、版判定）。実物は起こせない |
| E-5 | STA でない | 1（既存）。実物は起こせない |
| E-6 | 引数の誤り | 2b（ブロック D）合格 |
| E-7 | 不正な base64 | 1、2b（4 通りと、空白を含む正しい base64）合格 |
| E-8 | ネイティブが引数を拒む | 2b（ブロック D）合格 |
| E-9 | ほかのプロセスが握っている（`Busy`） | 起こせない |
| E-10 | 空のクリップボード・形式が無い | 1、2b 合格 |
| E-11 | 履歴が無効 | 2b（ブロック C2）合格 |
| E-12 | 前面でない | 2b（ブロック B）合格 |
| E-13 | 削除された項目 | 2b（ブロック D）合格 |
| E-14 | 要求のキャンセル | 2b 合格 |
| E-15 | close が `Busy` / `PartialState` | 2a（注入）。実物の `Busy` は S-2 の `ForceInitializeWhileDraining` で出た（3.4） |
| E-16 | drain が上限に達する | 2a。実物は起こせない |
| E-17 | 予約の失敗 | 1（既存） |
| E-18 | provider が null・空・例外、`render_target_set` の失敗 | 1 |
| E-19 | 破棄のあとの `Initialize` | 2a 合格 |
| E-20 | 履歴を写す途中の例外 | 1、2a（`HistoryConversionForTests`）合格 |
| E-21 | セッションが無いときの終了 | 1（`ShouldSkipNativeClose`）。置き場所はコードレビュー。Player でセッションの無い Manager を破棄する経路はテストから作りにくい（設計 8.1）。セッションが無いときの `CanShutdownNow` が真であること（別の規則）は 2b で確かめた |
| E-22 | close の中で待機中の要求が `Canceled` で届く | 2a（注入）、2b（`TryShutdown_WithARequestWaiting_DeliversItOnce`、S-2 の `RequestAndImmediateShutdown`）合格 |

### 3.2 イベントの返却仕様

1.x と同じ。同期 API は dispatcher 経由、非同期 API は受付 → 完了 → 台帳 → 配送。要求 1 件にちょうど 1 回（E-20、E-22 を含む）。

### 3.3 success 時契約

`IsSuccess == true` のとき `ErrorCode == None`（既存の層 1）。

### 3.4 既知の差分（マニュアルへ引き継ぐ）

設計 v12 の 5.4 の表を引き継ぎ、実機で確かめたものに印を付けた。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 名前空間とフォルダ | `JonghyunKim.NativeToolkit.Runtime.Clipboard`、`Runtime/Clipboard/` | `JonghyunKim.NativeToolkit.Runtime.Windows.Clipboard`、`Runtime/Windows/Clipboard/`。**利用者は `using` を書き換える**（型の名前と中身は同じ）。Android / iOS / macOS の型は今の名前空間のまま |
| 履歴の `Timestamp` の精度 | 100 ナノ秒 | ミリ秒（下の桁は 0）。値の意味（FILETIME のティック）は同じ。Get History の時刻が今から 10 分以内であることを 2b で確認 |
| 読み出しの `BufferTooSmall` | 大きさが呼び出しの間に変わり続けると返った | 返らない |
| 履歴・一覧・可用性の `ResultParseFailed` | JSON が壊れていれば返った | 返らない。履歴を写す途中の例外は `OutOfMemory` / `Unknown` |
| 対になっていないサロゲートを含む文字列 | UTF-16 のまま | 入力は `Encoding.UTF8` で、出力はネイティブで U+FFFD |
| `CopyMultipleFormats` で誤りが 2 つ以上あるとき | 要素を 1 件ずつ見て、最初の誤りを返した | 全部の base64 を先に検証し、次に flags、それから要素。**結果が変わって見えるのは、前の要素が `InvalidData` のときか flags が不正なときだけ。** 例: DIB として不正な要素の後ろに不正な base64 があると、1.x は `InvalidData`、移行後は `InvalidParameter`（確認済み） |
| 待っている要求があるときの終了の 1 回目（J-12） | 必ず「まだ終わっていない」で、Draining の期間ができた。その間の `Initialize` は `ShuttingDown` | 待っている要求を呼び出しの中で `Canceled` として配送し、1 回目で終わりうる。**実機では両方の経路を見た**: 履歴の可用性の要求を待たせた `TryShutdown` は 1 回目で `completed = true`（1.x は `false`）。S-2 の `ForceInitializeWhileDraining`（履歴の取得の WinRT が走っている）では、close が `Busy` を 2 回返して Draining になり、その間の `Initialize` は 1.x と同じく `ShuttingDown`（Mono と IL2CPP の両方、2026-09-29） |
| DLL の形式が違う（`BadImageFormatException`） | API によって `Unknown` か、呼び出し側に例外 | `BridgeUnavailable` |

**変わらないもの（公開 API の上で）:** エラーコードの値（0〜19 と 1000 以降）、`Operation*` の値、`GetPreferredFormat` の `""` が `Empty`、`GetFormats` の 0 件が `IsEmpty = true` の成功、不正な base64 が `InvalidParameter`、`CanShutdownNow` がセッションの無いときに真、`ShutdownWithDrain` のあとに `Initialize` し直せること。base64、`CanShutdownNow`、作り直しは 1.x で記録して移行後に同じ値を確かめた（2b）。`GetPreferredFormat` と `GetFormats` は層 1 で確かめた。

## 4. ビルド結果

- 実行コマンド: `bash scripts/verify_unity_windows.sh --include-destructive`（手順 5、6）、`--skip-build --include-destructive` と `--skip-build --include-destructive --il2cpp`（手順 7）
- 結果: SUCCESS。Player ビルドは手順 3〜6 の各段で通した（手順 2 は変換の関数と層 1 だけで、ビルドの記録は無い）。IL2CPP の Player はテストの実行ごとにビルドされる（約 20 分）
- 補足: `!UNITY_EDITOR` 側のコード（Bridge の `extern` と呼び出し、Manager の Player の経路、`WindowsTestProcess`）は Player ビルドでだけコンパイルされる

## 5. テスト結果

- 結果サマリー（`windows-native-toolkit-capi-2.0.0.dll` で、2026-09-29）:

| 層 | Mono | IL2CPP |
|---|---|---|
| EditMode | 1146 / 1146（Clipboard の Windows 分: CApi 81、Dispatch 60、ビルダー 15、ペイロード 9、要求の台帳 17、結果 26、サンプルの fixture 14、サンプルの結果 27、配線 19） | — |
| PlayMode（Editor） | 210 / 210（`WindowsClipboardManagerIntegrationTests` 66） | — |
| Player 本体 | 93 / 93 | 93 / 93 |
| うち Clipboard | `WindowsClipboardPlayerTests` 27、サンプルの画面 2、全ボタン 5 | 同じ |
| うち Dialog、Notification | Dialog 26、Notification 33（すべて合格） | Dialog 26、Notification 33（すべて合格。Notification の W-04 / W-11 のボタンのクリックを含む） |
| 作り直し（別の Player の実行） | 1 / 1 | 1 / 1 |
| M-19、層 3 | 合格 | 流さない（Mono だけ） |
| S-2（S-2 / S-4 / S-8 / 押した回ごとの期待値） | 失敗 0（6 ブロック） | 失敗 0（quit のブロックは流さない） |

- IL2CPP の経過:
  - 2026-09-27: 54 / 93。落ちたのはすべて、テストが PowerShell を起動する箇所と Player の exe のパスを読む箇所（IL2CPP の `Process` が対応していない）。本題に届いた Clipboard 26 / 27、S-2 のブロック A / B / C2 / D、`extern` の結び付けは通った
  - 2026-09-28: `WindowsTestProcess` に替えて 92 / 93。落ちた 1 本は `DeleteHistoryItemAsync_RemovesTheItem`（下）
  - 2026-09-29: 93 / 93
- 不安定なもの（移行による退行ではないと見ている）:
  - `DeleteHistoryItemAsync_RemovesTheItem`: 「削除した ID は消えたが、同じ文字の項目が履歴に 1 つある」。1.x（2026-09-26）、Notification の移行の実行（2026-09-27）、IL2CPP（2026-09-28）で 1 回ずつ。流し直すと通る
  - `BlockB_DelayedHistoryCallFromBehind`: 手順 6 の実行で、Player を最小化しても前面を奪えず、テスト自身の前提の確認で落ちた。以後の実行ではすべて通った
- 実行中の注意: テスト中にクリップボードを外から書くと、S-2 の期待値が崩れる（2026-09-28 に確認用の操作で起きた。ブロック A の `GetPreferredFormat` / `HasFormat`）。`artifact/topics/cross-platform-testing/README.md` の「テスト中に PC に触らない」と同じ扱い

### 5.1 テスト詳細

| テスト観点 | テストファイル | 結果 | 備考 |
|---|---|---|---|
| 構造体の大きさと位置、base64、Timestamp、読み出しの結果、履歴の項目、描画、例外と状態の変換、`ShouldSkipNativeClose` | `Tests/Runtime/WindowsClipboardCApiTests.cs` | ○ | 81 |
| 読み出しと描画の規則（新しい仕組み） | `Tests/Runtime/WindowsClipboardManagerDispatchTests.cs` | ○ | 2.4 |
| 拒否・配送・drain・quit・E-20・E-22 | `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs` | ○ | 66 |
| 1.x で記録した 10 本 | `Tests/PlayMode/WindowsClipboardPlayerTests.cs` | ○ | 3.4 の 2 本（DIB と base64、`TryShutdown`）を除き、期待値を変えずに通った |
| `extern` の結び付け | 同上（`TheExternsThatTakeNull_Bind`） | ○ | Mono と IL2CPP |
| S-2 の全ボタン 67 と押した回ごとの期待値 | `WindowsClipboardSampleRunPlayerTests.cs`、`scripts/check_windows_clipboard_sample_log.py` | ○ | J-12 の 2 か所だけ変えた |
| M-19 | `WindowsClipboardSampleQuitPlayerTests.cs` | ○ | Mono |

### 5.2 未実施ケース詳細

| テスト観点 | 未実施理由 |
|---|---|
| 実際の DLL 欠落・版違い（E-4） | Player から DLL を抜く仕組みが無い。判定と変換は層 1 |
| `Busy` を返すほかのプロセス（E-9）、drain の上限（E-16） | 起こせない |
| close の中の `WM_RENDERALLFORMATS` で provider が呼ばれること | M-19 の期待値で間接的に見る（設計 8.1） |
| IL2CPP での M-19 と層 3 | Mono だけで流す（設計 5.1。IL2CPP で差が出るのはマーシャリングとコールバックで、Player テストと S-2 が通す） |

## 6. Definition of Done

- 判定基準: ○ 実装・コード・テスト確認の範囲では OK / △ 一部 OK だが追加確認が必要 / × 未達 / - 対象外
- ○ 公開 API を変えずに C ABI 2.0.0 へ移した（名前空間の変更は Q-4 の例外として承認済み）
- ○ 今の Player テスト、S-2 の全ボタンと押した回ごとの期待値（J-12 の 2 か所を除く）、M-19 が通る
- ○ 1.x で記録したテストが、3.4 に書いた 2 本を除いて同じ値で通る
- ○ J-1〜J-13 を実装し、層 1 / 2a / 2b で確かめた
- ○ 1.x の追加のピンと DLL を消した
- ○ J-10: 3 機能の層 2b が IL2CPP の Player で通った
- △ `DeleteHistoryItemAsync_RemovesTheItem` がまれに落ちる（Windows の履歴の振る舞いと見ている。5 章）

## 7. 残作業

- Dialog と Notification の実装結果に IL2CPP の結果を足す（それぞれ新しい版で）
- マニュアル: 次の版の write-manual でこの文書の 3.4 を読む

## 8. ステップ 9 実行確認

- 提示文:
  - 「この実装結果を採用して、次工程へ進めますか？」
- 選択肢:
  - 実行する: この実装結果を採用して次工程へ進む
  - 修正する: 指摘内容を反映して再実装
  - キャンセル: ここまでの修正差分は保持したまま、終了
- ユーザー回答:
  - 未回答
