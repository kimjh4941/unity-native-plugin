# 実装結果レポート v1（Windows Notification の C ABI 2.0.0 への移行）

## 基本情報

- 日付: 2026-09-27
- 機能名: notification
- 対象プラットフォーム: Windows
- ブランチ: feature/UNT-12
- 計画ファイル: `artifact/windows/notification/designs/2026-09-27-windows-notification-design-v8.md`
- UI テスト計画: `artifact/windows/notification/designs/2026-09-27-windows-notification-ui-test-plan-v2.md`
- 親課題: `artifact/topics/windows-c-abi-2/README.md`
- 前の実装結果: `2026-06-06-windows-notification-implementation-feature-result-v1.md`（1.x での新規実装。上書きしない）

## 0. 状態サマリー

設計 v8 の 5.6 の手順 1〜5 と 7 を終えた。手順 6（IL2CPP）は Clipboard の移行のあとに行う。

| 手順 | 内容 | コミット | 状態 |
|---|---|---|---|
| 1a | JSON リーダー、Bridge の変換の関数、層 1 | `398833d` | 済み |
| 1b | 1.x のまま、層 2b の記録テストと作り直しの流れ（検証スクリプトの別の実行を含む） | `7296266` | 済み。1.x で 68/69（W-04 はスキップ）、作り直し 1/1 |
| 2 | Bridge のネイティブの宣言と呼び出し | この文書と同じ一連のコミット | 済み |
| 3 | Manager の置き換え、層 2a | 同上 | 済み |
| 4 | 層 2b: W-01〜W-10 と 1b の記録テストが期待値を変えずに通る、W-04 の `[Ignore]` を外す、W-11、移行後だけのテスト | 同上 | 済み |
| 5 | Dialog の D-09〜D-12 と Clipboard の Player テストを流し直す（STA / MTA） | — | 済み（5 章。Clipboard の 1 本は既知の不安定なもの） |
| 6 | 3 機能の移行後に IL2CPP の Player で層 2b を 1 回流す（J-10） | — | 未（Clipboard 待ち） |
| 7 | 実装結果ファイル（この文書） | 同上 | 済み |

## 1. 実装サマリー

### 1.1 計画ファイル由来の実装

- **2 層（Bridge + Manager）と共通部。** `WindowsNotificationCApi`（Bridge。純粋な変換は `#if` の外、`extern` と呼び出しは `UNITY_STANDALONE_WIN && !UNITY_EDITOR`）、
  `WindowsNotificationJsonReader`、共通部 `WindowsNativeToolkitCApi`（Dialog で作ったもの）
- **J-1** 公開の JSON は残し、C# で 1.x の読み方（`ValidatePayload` / `BuildFromJson` / `BuildPayload` が読むキーと型）で読んで、builder の呼び出しの並びにする。呼び出しの順も 1.x に合わせた
- **J-2** `timestamp` は秒のまま。0 方向に切り捨てて 1000 倍する
- **J-3** Manager があれば 2 回目の `Initialize` はネイティブを呼ばずに成功
- **J-4** Editor では何もしない。Manager のネイティブの呼び出しはすべて `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` の中で、`Application.platform` の確認もその内側
- **J-5** `audio.src` は読まない（issue `audio-src-is-ignored.md` を作った）
- **J-6** `Initialize` の最初に共通部の版の確認。違う・DLL が無いなら `-4`
- **J-7** 活性化の受け口は static の delegate、`user_data` / `release` は `NULL`、dispatcher は `Awake` で取っておく
- **J-8** runtime を読むかは `GetCurrentPackageFullName` の結果（15700 / 122 / そのほか）で決める
- **J-9** runtime は `OnDestroy` まで持つ。manager の作成に失敗しても解放しない。`OnDestroy` で manager → runtime の順に解放
- **J-11** Show / Schedule は 1 → `get_setting` で 2 → JSON（3 / 7）→ builder → ネイティブの順
- 結果の出し方は 1.x の `FireResult` の形のまま（per-call と event を 1 つの try、per-call が投げると event は出ない）。GetAll は `GetAllNotificationsCompleted` だけ
- ログ: 結果として返す失敗は `LogWarning`。`LogError` は 1.x と同じ 4 種類（bootstrap の失敗、`FireResult` の例外、GetAll の per-call とイベントの例外、活性化の受け口と積んだ処理の例外）
- `WindowsNotificationResult` に `-4` の文言、ペイロードの型の XML コメント（`Timestamp` は秒、`Expiration` は秒、`Audio.Src` は効かない）

### 1.2 実装時の追加判断

- **JSON リーダーの数値。** 構文を読む段階で `double` にし、∞ になるもの、および仮数に 0 でない数字があるのに 0 になるものを 3 にした。Mono の `double.TryParse` は桁あふれを失敗として返すので、失敗も同じく 3
- **重複したキーの位置。** 後の値が勝ち、位置は最初に出たところ（ボタンの `args` の並びに効く）
- **ボタンとコンボの index の突き合わせ。** 並びは要素ごとに期待する index を持ち、ネイティブが返した index と違えば 5 にする（全部成功している限り起きない）
- **`Deliver` の形。** Show / Schedule の判定の順（J-11）と JSON の読み取りを 1 つの private の関数にまとめ、ネイティブの show / schedule は `out` 付きの delegate で渡した
- **CS0067。** Editor では `NotificationInvoked` と `GetAllNotificationsCompleted` を一度も出さない（J-4）ので、この 2 つのイベントだけ `#pragma warning disable CS0067` で抑えた
- **通知センターの操作の切り出し。** 作り直しの fixture でも使うので、`WindowsNotificationCenter`（同じファイルの internal クラス）にした
- **設定の事前確認（設計 8.2）** は手順 4 ではなく 1b で入れた（1.x でも害が無く、早く効く）
- **dispatcher の生死の確認。** ネイティブのスレッドで Unity の `==` を使う。MonoBehaviour に対してはネイティブの参照（cached pointer）を読むだけなので、そのスレッドでも安全

## 2. 変更ファイル

### 2.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Notification/WindowsNotificationJsonReader.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Notification/WindowsNotificationCApi.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsNotificationCApiTests.cs`（リーダー、変換、Bridge の 3 クラス）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsNotificationManagerEditorTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsNotificationRecreatePlayerTests.cs`
- `artifact/windows/notification/designs/2026-09-27-windows-notification-ui-test-plan-v2.md`
- `artifact/windows/notification/issues/audio-src-is-ignored.md`

### 2.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Notification/WindowsNotificationManager.cs`（1.x の `DllImport` と GetAll のバッファ処理を削除）
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Windows/Notification/WindowsNotificationResult.cs`、`WindowsNotificationPayloads.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsNotificationTests.cs`（コード 8 と `-4`）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsNotificationSamplePlayerTests.cs`
- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/VERSION.txt`（コメントだけ）
- `scripts/verify_unity_windows.sh`（作り直しのテストを本体から除き、`--include-destructive` のとき別の Player の実行で流す）
- `agent-rules/coding-rules/testing.md`、`artifact/topics/windows-c-abi-2/README.md`、`artifact/README.md`、`artifact/windows/notification/issues/unreachable-notification-apis.md`

### 2.3 非変更（対象だが未変更）

- `WindowsNotificationJsonBuilder.cs`、`Runtime/Common/UnityMainThreadDispatcher.cs`、`Runtime/UI/Windows/Notification/*`、`Runtime/Resources/UI/Windows/Notification/*`: 公開 API・共通部品・サンプルは変えない
- 設計書 v8: 版の付いた文書は上書きしない

## 3. エラー契約反映

### 3.1 エラーケース実装反映（設計 v8 の 6 章）

| # | 状況 | 実装 | 検証 |
|---|---|---|---|
| E-1 | 初期化の前に操作 | `Failure(1)`、設定は `Unknown` | 2b（作り直しの流れ）合格 |
| E-2 | 非パッケージで表示名かアイコンが空 | ネイティブが 7 | 2b（作り直しの流れ）合格 |
| E-3 | bootstrap の失敗 | その errorCode（5）、`LogError` に system code | 起こせない。コードレビュー |
| E-4 | 2 回目の `Initialize` | `Success`、ネイティブを呼ばない | 2b（W-01。SetUp で初期化済み）、W-11 合格 |
| E-5 | 通知が無効 | 2（JSON より先） | 1（`ShowGate`）。実機では起こさない |
| E-6 | JSON の構文の誤り、`null` の payload | 3 | 1、2b 合格 |
| E-7 | 必須キーの欠落・型の違い・`null` | 7 | 1、2b（7 通り）合格 |
| E-8 | 検証の誤り（ボタン 6 個、ループ音で long でない） | ネイティブが 7 | 2b 合格 |
| E-9 | 進捗の通知が無い | 4 | 2b（W-08）合格 |
| E-10 | 非パッケージで SetBadge / RemoveById / GetAll | 8 | 2b 合格（1.x でも 8） |
| E-11 | `SetBadge` の値が -6 より小さい | 7（初期化の確認より先） | 1、2b 合格。Editor では何も出さない（2a） |
| E-12 | DLL が無い・major が 2 でない | `-4` | 1（版判定、例外の変換）。実物は起こせない |
| E-13 | WinRT の失敗 | 5 | 起こせない |
| E-14 | C# 側の予期しない例外 | 5（読み込みの失敗は `-4`）、結果は 1 回 | 1（例外の変換） |
| E-15 | Editor で呼ぶ | 何もしない | 2a（5 本）合格 |
| E-16 | close の後に届く活性化 | dispatcher に積み、`_instance` が無ければ捨てる | 起こせない。コードレビュー |
| E-17 | 予約時刻が範囲外 | ネイティブが 7 | 2b（2 通り）合格 |
| E-18 | per-call callback が例外 | `LogError`、event は出ない | 2b（2 本）合格 |

### 3.2 イベントの返却仕様

- per-call → `NotificationOperationCompleted` の順に 1 回ずつ（GetAll は per-call → `GetAllNotificationsCompleted`）。同じ呼び出しの中で出る
- `NotificationInvoked` だけ dispatcher 経由（次の `Update` 以降）。引数は `raw_arguments`（1.x と同じ作り方の JSON テキスト）

### 3.3 success 時契約

- `IsSuccess == true` のとき `ErrorCode == 0`、`ErrorMessage == null`（`WindowsNotificationResult.Success`。層 1 の既存テスト）

### 3.4 既知の差分（マニュアルへ引き継ぐ）

設計 v8 の 5.4 の表をそのまま引き継ぐ。実機で確かめたものに印を付けた。

| 差分 | 1.x | 2.0.0 移行後 |
|---|---|---|
| 名前空間とフォルダ | `JonghyunKim.NativeToolkit.Runtime.Notification`、`Runtime/Notification/` | `JonghyunKim.NativeToolkit.Runtime.Windows.Notification`、`Runtime/Windows/Notification/`。利用者は `using` を書き換える |
| **ボタンのクリック（非パッケージ、アプリが動いている）** | 届かない | **`NotificationInvoked` が来る（W-04 / W-11 で確認）** |
| レジストリ | `CLSID\{clsid}\LocalServer32`、`AppUserModelId\<displayName>` の `DisplayName` / `IconUri`、ショートカット | 加えて `CustomActivator` を書く。**表示名が同じアプリどうしはクリックを奪い合う** |
| JSON の必須キーが無い・型が違う・値が `null` | 5 | 7（確認済み） |
| JSON の数値が `double` に収まらない（`1e400`、`1e-400`） | 5 | 3 |
| JSON の入れ子がコンテナ 513 個以上 | 5 | 3 |
| JSON の小数表記で 0 に丸まる値 | 0 として成功 | 3 |
| `null` の payload（初期化済みで有効なとき） | プロセスが落ちた | 3（確認済み） |
| Show / Schedule で設定の取得が例外になる（主にパッケージ、未検証） | 5 | 2 |
| 文字列の中の NUL | NUL の後ろも渡した | NUL の手前で切れる |
| `timestamp` が範囲外（±922,337,203,685 秒を超える） | `time_t` への変換のまま | 7（確認済み） |
| `expiration` が `long` に収まらない | 値が決まらなかった | 7 |
| `scheduledTimeUnixMs` が範囲外（±922,337,203,685,477 を超える） | 検査せずに渡した | 7（確認済み） |
| 引数が 2 つ以上のボタンの活性化の JSON | `args` を `JsonObject` の列挙順で渡した | 文書の順で渡す。キーと値の組は同じ |
| `GetAllNotifications` の結果が長い | 黙って切り詰めて成功 | 切り詰めない（1M wchar を超えると 5）。非パッケージでは 8 のまま |
| `UpdateNotificationProgress` の `valueStr` / `status` が `null` | その項目を変えない | `""` にする |
| DLL が無い・関数が無い・形式が違う | どのメソッドでも例外 | `Initialize` は `-4`、ほかは 1（設定は `Unknown`、`SetBadge(-7)` は 7） |
| そのほかの C# 側の例外 | 呼び出し側に出た | 5 |
| `OnDestroy` の後 | bootstrap を shutdown しない | `MddBootstrapShutdown` を 1 回。新しい Manager の `Initialize` で初期化し直す（作り直しの流れで確認） |
| パッケージのプロセスの `Initialize` | `initWinAppSdk` を呼んだ | 呼ばない（未検証） |
| 対になっていないサロゲート | UTF-16 のまま | U+FFFD |
| close の後に届く活性化 | 1 回走りえた | 同じ。Manager が無ければ捨てる |

**`-4` の意味:** `Initialize` だけが返す。ネイティブのライブラリが使えない（DLL が無い、関数が無い、読み込める形式でない、major 版が 2 でない）。文言は `Native library unavailable`。
このとき Manager は作られないので、以後の操作は 1。

**変わらないもの（公開 API の上で）:** 2 回目の `Initialize` は成功（ネイティブの 2.0.0 は `NOT_SUPPORTED` だが、Manager が呼ばない）、`timestamp` は秒、非パッケージで SetBadge / RemoveById / GetAll は 8（issue `unreachable-notification-apis.md`）。

## 4. ビルド結果

- 実行コマンド: `bash scripts/verify_unity_windows.sh --include-destructive`（Editor の EditMode / PlayMode、Win64 の Player ビルドと Player テスト、作り直しの別の実行、M-19）
- 結果: SUCCESS（Player テストの 1 本を除く。5 章）
- 補足: `!UNITY_EDITOR` 側のコード（Bridge の `extern` と呼び出し、Manager の Player の経路）は Player ビルドでだけコンパイルされる。層 0 で通した

## 5. テスト結果

- 結果サマリー（`windows-native-toolkit-capi-2.0.0.dll` で、2026-09-27）:
  - EditMode: 1114 / 1114（うち Notification の Windows 分: リーダー 31、変換 95、Bridge 65、結果 11、ビルダー 12）
  - PlayMode: 209 / 209（うち `WindowsNotificationManagerEditorTests` 5）
  - Player: 81 / 82。`WindowsNotificationSamplePlayerTests` 33 / 33、`WindowsDialogSamplePlayerTests` 26 / 26、Clipboard 22 / 23
  - 作り直し（別の Player）: 1 / 1
  - M-19、Clipboard の全ボタンのログ照合: 合格
- 失敗: `WindowsClipboardPlayerTests.DeleteHistoryItemAsync_RemovesTheItem`（Clipboard は 1.x のまま）
  - 「削除した ID は消えたが、同じ文字の項目が履歴に 1 つある」。テストのコメントに、2026-09-26 にも 1 回同じ形で落ちたと記録がある
  - Notification の変更はこの経路（Clipboard の 1.x の DLL、履歴の API）を通らない。**移行による退行ではないと見ているが、流し直しで確かめていない**
- 手順 5（STA / MTA）: `manager_create` はメインスレッドで MTA を試みるが、Unity のメインスレッドは既に STA なのでそのまま STA で動く（設計 v8 1.5）。
  Dialog の D-09〜D-12（フォルダ選択、STA 前提）は 26 本すべて合格、Clipboard の STA のセッションも上の 1 本を除いて合格

### 5.1 テスト詳細

| テスト観点 | テストファイル | 結果 | 備考 |
|---|---|---|---|
| JSON リーダー | `Tests/Runtime/WindowsNotificationCApiTests.cs`（`WindowsNotificationJsonReaderTests`） | ○ | 構文、数値、深さ 512、重複キー、「無い」と `null` |
| JSON → builder の呼び出し | 同上（`WindowsNotificationContentPlanTests`） | ○ | 全キーとサブキー、1.x が読まないキー、型の違い、範囲外、公開ビルダーとの往復 |
| 構造体・判定・変換 | 同上（`WindowsNotificationCApiTests`） | ○ | 構造体 2 つの位置、J-11、J-8、GetAll の JSON のエスケープ、活性化の例外 |
| Editor で何もしない | `Tests/PlayMode/WindowsNotificationManagerEditorTests.cs` | ○ | 5 本 |
| W-01〜W-11 | `Tests/PlayMode/WindowsNotificationSamplePlayerTests.cs` | ○ | W-04 / W-11 が初めて通った |
| 1.x で記録した 10 本 | 同上 | ○ | 期待値を変えずに通った |
| 移行後だけ・`extern` | 同上 | ○ | 12 本 |
| 作り直しの流れ | `Tests/PlayMode/WindowsNotificationRecreatePlayerTests.cs` | ○ | 別の Player の実行 |

### 5.2 未実施ケース詳細

| テスト観点 | 未実施理由 |
|---|---|
| GetAll の成功時の JSON と list の読み取り | 非パッケージでは 8。組み立ては層 1、読み取りはコードレビュー |
| パッケージのプロセス（J-8）、設定の取得の例外 | テストの Player は非パッケージ。MSIX の検証手順が無い |
| コールドスタートの活性化 | テストから起動の仕方を作れない |
| 実際の DLL 欠落・版違い（E-12） | Player から DLL を抜く仕組みが無い。判定と変換は層 1 |
| IL2CPP（J-10） | 3 機能の移行待ち。それまで層 2b の結果は Mono での保証 |

## 6. Definition of Done

- 判定基準: ○ 実装・コード・テスト確認の範囲では OK / △ 一部 OK だが追加確認が必要 / × 未達 / - 対象外
- ○ 公開 API（メソッド 12 本・イベント 3 本・定数・型・JSON の形と単位・判定の順）を変えずに C ABI 2.0.0 へ移した（名前空間の変更は Q-4 の例外として承認済み）
- ○ W-01〜W-10 と 1.x で記録したテストが、期待値を変えずに Player で通る
- ○ W-04 / W-11（ボタンのクリック）が通る
- ○ J-1〜J-9、J-11 を実装し、層 1 / 2a / 2b で確かめた
- △ Clipboard の `DeleteHistoryItemAsync_RemovesTheItem` が 1 回落ちた（既知の不安定さと見ている。5 章）
- △ J-10（IL2CPP）: Clipboard の移行後に流す

## 7. 残作業

- 手順 6（IL2CPP）
- Clipboard の移行のあとで、追加のピン（1.x）を消す（親課題 README「移行時にやること」の 3）
- `audio-src-is-ignored.md`（J-5）は次の版の設計で扱う
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
