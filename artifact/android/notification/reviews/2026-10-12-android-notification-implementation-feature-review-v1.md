# 実装レビュー結果 v1

- 日付: 2026-10-12
- レビュー対象: ブランチ `feature/UNT-13` の未コミットの差分（Android Notification の C ABI 2.0.0 への移行。設計 5.5 の手順 4〜8）
- 実装計画: `artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md`
- 実装結果: `artifact/android/notification/results/2026-10-12-android-notification-implementation-feature-result-v1.md`
- 対象プラットフォーム: Android
- レビュアー: サブエージェント（ラウンド 1）だけ。読むだけ。1 章はその出力（文言には手を加えていない。見出しの段を 1 つ下げた）
- Codex: 並行で回したが、利用上限のエラー（"You've hit your usage limit"）で失敗し、指摘は出なかった。利用者の指示（2026-10-12）で、このラウンドはサブエージェントだけで判定する
- 総合評価: 要修正（軽微）。A は 0 件（B 3 件、C 7 件。どれも low）

---

## 1. サブエージェント（Claude）


### 結論
**総合評価: 要修正（軽微）。A の件数は 0 件。**
- 振る舞いを変える欠陥は見つからなかった。
- 指摘は B が 3 件、C が 7 件で、どれも low。

確かめたことは次のとおり。
- **署名:** Bridge の extern 75 本を `Notification.h` と 1 本ずつ照らした。型・順・out/ref、size_t は `UIntPtr`、int64 は `long`、uint32 は `uint` で、すべて一致した。
- **NULL の渡し方:** Builders.cpp と照らした。setter に NULL を渡すかどうかの判断に問題は無い。
- **1.x の規則:** マッピングを 1.x の Kotlin パーサーとブリッジの補正と照らした。違いは、どれも設計の 5.3（既知の差分）か J-N6 に載っている。
- **ハンドルとメモリ:** content・channel・イベントのハンドルを、成功・失敗・例外の 3 つの経路で追った。どの経路でも 1 回だけ解放され、確保したメモリは `FreeAll` で解放される。
- **設定の画面:** 受け付けた後の失敗でコールバックが来ないことを `Accept.h:30-36` で確かめた。pending の数え方（呼ぶ前に増やし、入口で拒まれたら減らす）は正しい。
- **リスナーと受け口:** 登録は種類ごとの印で 1 回だけ。受け口は try/catch で囲み、イベントは `ReadAndFree` で 1 回だけ解放する。結果はすべて取り出し口を通り、呼び出しの後に、その時点で生きている Manager に出る。

### 指摘一覧

#### B（検証手段の穴）

**I1-1 / B / low — 列挙の値がヘッダーの値に固定されていない**
- 場所: `AndroidNotificationCApi.cs:43-54`、`AndroidNotificationCApiTests.cs`
- 問題: Kind / Target / Result / Tap の定数（0/1/2）を固定するテストが無い。
  - `ActionIdOf_EachKind` も `Settings_*` も `Api.Kind*` などの定数そのものを使うので、値を入れ替えても層 1 と層 2a は通る。
  - エラー値 0〜15 は `ErrorName_EveryValue` が整数で固定しているので問題ない。
- 根拠: `Notification.h:41-72`。実機の AN-32〜35、AN-40〜42 でしか気づけない。
- 提案: 層 1 に `Assert.AreEqual(1, Api.KindAction)` のような固定を 1 本足す。

**I1-2 / B / low — 入口で拒まれたときの pending の減算を確かめるテストが無い**
- 場所: `AndroidNotificationManager.cs:683-703`（`OpenSettings` の入口の拒否と例外の経路）、`OnSettingsCore:944`
- 問題: `PendingCallCountForTests` の増減を層 2a で見ていない。実機のテストはこの値が 0 になるのを待つが、入口で拒まれる経路（NOT_INITIALIZED など）は実機で起こせない。この経路の `Decrement` を消しても、どのテストも落ちない。
- 根拠: EditorTests の `Settings_*` は `OnSettingsCore` を直接呼ぶだけ。
- 提案: 減算を純粋な形に切り出して数えるか、最低限 `OnSettingsCore` の前後の値を確かめる。

**I1-3 / B / low — 通知の操作の前のリスナー再登録が未検証**
- 場所: `AndroidNotificationManager.cs:504, 634, 651`
- 問題: J-N12 は「通知を出す・予約する各メソッドの前にもやり直す」と決めている。しかし Editor では `CanCall` で先に戻り、実機では `Awake` が登録を済ませているので、この 3 行を消しても落ちるテストが無い。
  - 実装結果 5.2 にも「壊すと落ちる」の確かめ（ワークフローの止める基準の 2）がまだと書かれている。
- 提案: 確かめを行って結果を実装結果に書く。この経路は、コードレビューで済ませると明記するだけでもよい。

#### C（記述・ルール）

**I1-4 / C / low — 問い合わせの例外のログが Warning**
- 場所: `AndroidNotificationManager.cs:752-756`（`Query` の catch）
- 問題: 捕まえた例外を `LogWarning` で出している。
- 根拠: `common.md:109` は「LogError は…捕まえた例外」、設計 4.5 の行は「C# の例外 → LogError」。J-N11 の Warning はエラー値で失敗したときの話。
- 提案: `LogError` にする。

**I1-5 / C / low — 失敗の警告が 2 回出る**
- 場所: `AndroidNotificationManager.cs:393-397, 508-512, 660-664`、`Run:778`
- 問題: ビルダーが拒むと、`LogBuilderFailure` に続けて `Run` の `LogFailure` も Warning を出す。
- 提案: 片方にまとめる（例えば `LogBuilderFailure` だけにする）。

**I1-6 / C / low — csharp.md の「先頭 1 行目に Debug.Log」を満たしていない**
- 場所:
  - Manager の internal メソッド: `EnsureListeners:863`、`OnInteractionCore:919`、`OnShownCore:932`、`OnSettingsCore:942`、`ScheduleWithFallback:799`
  - マッピングの `ReadNotification/ReadSchedule/ReadChannel`（172/182/210）
- 根拠: csharp.md の対象は public / internal メソッド。Clipboard の前例は `EnsureChangeListener:693` で先頭に出しており、`ReadAndFree:609` は出さない理由をコメントに書いている。
- 提案: 先頭のログ（値は長さや件数だけ）を足すか、出さない理由をコメントに書く。

**I1-7 / C / low — ルールファイルが古いまま・不足している**
- `csharp.md:14` に「Android の 1.x のブリッジを呼ぶ層（まだ移していない機能）は LogError」が残っている。4 機能すべてを移したので、もう該当しない。
- `common.md` の「Notification の形」に、INVALID_PARAMETER を `LogWarning` にする例外（設計 4.5。ビルダーの範囲の検査は利用者の値に返るため）が無い。一般則（`common.md:109`）は「入口の INVALID_PARAMETER は LogError」なので、書かないと矛盾して見える。

**I1-8 / C / low — 実装結果に、設計の変更ファイル一覧からの逸脱が書かれていない**
- 設計 5.1（行 279）は層 2b を `AndroidNotificationPlayerTests.cs` と `AndroidNotificationManagerPlayerTests.cs` の 2 つに分けるとしていた。実際は UI テスト計画 v5 の 51 行の決定どおり 1 つにまとまっている。
- `AndroidNotificationRetentionTests.cs` は設計 5.1 に無い。
- 実装結果 2.1 と「計画との差分」に、この 2 点が書かれていない。

**I1-9 / C / low — XML コメントが少し足りない**
- クラスの remarks（Manager:36-38）の「範囲の外の値」の列挙に、メッセージの負の時刻が抜けている（設計 J-N6 と 5.3 にはある）。
- `CancelAllScheduledNotifications` の XML（544-546）は「Cancels all scheduled notifications.」のまま。ヘッダーは「再起動の後も残す予約だけを取り消す（as in 1.x）」と書いている。`IsNotificationScheduled` と同じ注記を足すとよい。

**I1-10 / C / low — 空白の判定の違いが 5.3 に無い（要確認）**
- 場所: `AndroidNotificationJsonMapping.cs:461-464`
- 問題: 空白を .NET の `string.IsNullOrWhiteSpace` で判定している。1.x は Kotlin の `isNotBlank` で判定した。
  - 記憶による推測で、実際には確かめていない: U+001C〜U+001F は Kotlin では空白で .NET では空白でない。U+0085 はその逆。
- 根拠: 親課題の J-11 は「ふつうでない空白」を既知の差分に書くとしている。
- 提案: 違いを確かめたうえで 5.3 に 1 行足す。

### 実装計画整合性チェック
| 項目 | 判定 |
|---|---|
| Manager + Bridge パターン準拠 | ○ |
| 変更ファイル一覧との一致 | △（I1-8） |
| テスト方針（7.1 / 7.2）の網羅性 | ○（7.1・7.2 の全項目にテストがある） |
| エラーケース E-1〜E-17 の全実装 | ○ |
| 返却仕様（文言・成功時に ErrorMessage が null）との整合 | ○ |

- J-N1〜J-N21 はすべて実装と一致した。
  - J-N8: 不正確な予約は 1 で 1 回だけ呼び、正確な予約だけ 11 のとき 1 で呼び直す。
  - J-N9: user_data は target の値、release は NULL。
  - J-N12: 種類ごとの印。
  - J-N16: JSON を読む前に判定する。
  - J-N18: 0 件の data は null。
  - J-N20: 自分が `_instance` のときだけ null にする。

### プロジェクトルール適合チェック
| 項目 | 判定 |
|---|---|
| common.md 準拠 | △（I1-4、I1-7） |
| csharp.md 準拠 | △（I1-6、I1-7） |
| Bridge 実装品質（スレッド安全性・メモリ管理） | ○ |
| 既存 API 互換性 | ○ |

- 既存 API: 公開の型・メンバー・イベント・定数は変わっていない。`NotificationActionResult` で置き換えたのは internal のコンストラクタだけ。
- ログに値を出さない規則: JSON をそのまま出す箇所は無く、出すのは長さ・件数・有無・ID だけ。
- プラットフォーム独立性:
  - P1: 新しいファイルと型はすべて Android 接頭辞付き。
  - P1b: テストのガードは対象の型のガードと一致。
  - P1c: 置き場所と名前空間は規約どおり。
  - P2: 差分に他の OS のファイルは無い。
  - P4: `Runtime/Common/` への追加は無い。
  - P5: クラスは `UNITY_ANDROID || UNITY_EDITOR`、extern と受け口は `UNITY_ANDROID && !UNITY_EDITOR` の二重構造になっている。

### テストカバレッジ
- **カバーできている観点**
  - 層 1: 構造体 2 つの大きさと全フィールドの位置。コールバックの形。全エラー値の文言を 16 の操作それぞれで確認。`ActionIdOf` と `ShownTag`。予約の options とアクションの写し。
  - マッピング: 1.x の必須の順と文言、既定値、空白・null・型違い、丸め、色の範囲、捨てる要素、style ごとの扱い。`AndroidNotificationJsonBuilder` が書いた JSON（Builder のテストの 3 件とサンプルの各種類）を読み戻して、元の Payload と比べている。
  - 層 2a: Editor の全メソッド、届け先と作り直し、3 つの受け口、`ScheduleWithFallback` の 4 つの場合、リスナーの種類ごとのやり直し、`ReadAndFree`。
  - 層 2b: 43 本と AN-01（保持のテスト）。
- **不足している観点**
  - 列挙の値の固定（I1-1）。
  - 設定の呼び出しの pending の数え方（I1-2）。
  - 操作の前のリスナー再登録（I1-3）。
  - `BuildContent` / `BuildChannel` の setter が全部呼ばれていること・失敗の経路での解放はコードレビュー扱い（設計 E-17 のとおり）。読んで、spec の全フィールドが setter に渡り、失敗と例外の経路で content と channel が解放されることを確かめた。

### 主に参照したファイル
- 実装: `/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/Android/Notification/` の Manager / CApi / JsonMapping / JsonReader と結果の型 3 つ
- テスト: `Tests/Runtime/AndroidNotification{CApi,JsonMapping,JsonReader}Tests.cs`、`Tests/PlayMode/AndroidNotification{ManagerEditor,Retention}Tests.cs`
- native-toolkit: `Notification.h`、`Builders.cpp`、`Notification.cpp:199-237, 547-576`、`Common/Accept.h`、`NotificationBridge.kt:120-360, 545-680`
- 1.x: `UnityNotificationJsonParser.kt`、`UnityAndroidNotificationManager.kt:330-866`（スクラッチパッドの v112）

---

## 2. Codex

利用上限で失敗し、出力なし（上の記載）。
