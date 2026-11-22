# iOS の通知の 5 型の設計 v1 のレビュー v1

- 記録日: 2026-11-21
- 対象: [../designs/2026-11-21-ios-notification-payloads-design-v1.md](../designs/2026-11-21-ios-notification-payloads-design-v1.md)
- レビュアー: サブエージェント（general-purpose）だけ。**Codex（`codex:codex-rescue`）は使えなかった**（`Codex CLI is not installed or is missing required runtime support`）。決まりどおり、待たずにサブエージェントの結果だけで判断した
- 1 章はレビュアーの出力をそのまま写した。こちらの判断は 2 章にだけ書く

## 1. サブエージェント

設計書 v1（`artifact/ios/notification/designs/2026-11-21-ios-notification-payloads-design-v1.md`）のレビュー、ラウンド 1（全体）

件数: A1 0 / A2 0 / B 1 / C 4

**確かめて問題が無かったこと**
- **JSON が同じになるか**: `IosNotificationJsonBuilder` は 5 型の全項目を読んでいます。
  - content は 13 項目（`IosNotificationJsonBuilder.cs:31-45`）、attachment は `:135-141`、time interval は `:60-61`、calendar は `:77-85`、location は `:100-105`。
  - このファイルには `#if` が無く、Editor でも組まれます。写すときに項目を 1 つ落とすと、Editor のコンパイルで止まります。
  - D-2（同じ項目・同じ既定値）を守れば、出る JSON は同じです。Manager の読み手（`IosNotificationRequests.cs:98-223`）はキーしか見ないので、ここも変わりません。
- **旧い型の使い手**: パッケージ・Assets・Samples~・Documentation~ を全部 grep しました。iOS 側の使い手は 3 章の 4 ファイルだけで、漏れはありません。
  - サンプル: `IosNotificationManagerExampleController.cs:261-567`
  - Player のテスト: `IosNotificationPlayerTests.cs:59, 380`
  - ハーネス: `NotificationScenario.cs:149`
  - 旧い型の値はどれも Builder に渡されます。直し忘れは CS1503 になり、黙って通ることはありません。
- **Editor では組まれないコードの確かめ**:
  - サンプルの `#if UNITY_IOS && !UNITY_EDITOR` の中と、`IosNotificationPlayerTests` の 380 行は V-2 で組まれます。380 行は `#if NTK_IOS_CABI_2_NOTIFICATION` の中ですが、`IosPlayerBuildSetup.cs:56` の Defines にこの印があります。
  - ハーネスの asmdef は `includePlatforms: ["iOS"]` と `NTK_UI_HARNESS` の組なので、V-3 の `test_ios_ui.sh` で組まれます（`IosUiHarnessBuild.cs:82`）。
- **名前**: 新しい 5 つの名前と同じ型はまだありません。サンプルは global 名前空間で、2 つの `using` を並べても旧い名前と新しい名前はぶつかりません。
- **IL2CPP と JsonUtility**: どちらも影響はありません。Builder は反射も JsonUtility も使わず、項目を直接読んでいます。
- **macOS**: macOS の Builder・サンプル・テストは旧い型をそのまま使い、差分は入りません。
- **overload を残さない判断（D-4）**: I-10 の決定と、macOS の改名でもう一度壊れることの両方から、妥当だと思います。

---

**S-1 [B] [テスト] V-1 の「直す前に記録した文字列」をどう作るかの手順が無い**
- 根拠:
  - 設計書 4 章 V-1。
  - `IosNotificationJsonBuilder` のテストは今は 1 本もありません（`Tests/Runtime/` を見て確認。`testing.md:409` にも「テストは無い」とあります）。
  - 新しいテストは、型の差し替えと同じ変更（3 章）で足す計画です。
- 壊れ方:
  - 旧い型のまま Builder を一度も走らせなければ、期待の文字列は差し替えた後のコードを読んで手で書くことになります。それでは「JSON が同じ」を確かめたことになりません。
  - 実害は小さいです。全項目が Builder から読まれるので、項目を落とせばコンパイルで止まります。
  - それでも、次の 3 つは今の計画のテストでは捕まらない可能性があります。
    - 既定値の写し違い（例: `radius = 100.0`、`notifyOnEntry = true`、`attachments` の初期化漏れによる NRE）
    - 型の写し違い（例: `double?` を `float?` にすると `AppendValue` の default 分岐に落ち、カルチャ依存の文字列として引用符つきで出る。`IosNotificationJsonBuilder.cs:248-249`）
    - `[Serializable]` の付け忘れ（どのテストにも現れない）
- 直し方の案:
  - V-1 に順番を書きます。
    1. 旧い型のままテストを書き、EditMode で緑にする。
    2. Builder とテストの型名だけを替える。期待の文字列には触らない。
    3. もう一度緑にする。
  - D-6 のテストの形を、型ごとに「`new T()` の既定値だけ」と「全項目を既定値以外にする」の 2 本と決めます。
  - `[Serializable]` は `typeof(T).IsSerializable` を 1 行で確かめます。

**S-2 [C] [移行] D-3 の理由が、リポジトリが実測した事実と食い違っている**
- 根拠:
  - D-3 は「Unity の Script Updater が旧名を新名に書き換えると、macOS のコードまで壊す」としています。
  - しかし `agent-rules/coding-rules/common.md:481` は、段 4 で 6000.4.2f1 を実測した結果として、「API Updater は、このパッケージの `[MovedFrom]` を使わない（候補は名前が `Unity` で始まるアセンブリだけ）」と書いています。
  - `[MovedFrom]` が効くのは `[SerializeReference]` のデータだけです（`:482`）。
- 壊れ方: 結論の「付けない」は正しいので、壊れはしません。ただ、移行ガイドや後の macOS の設計がこの理由を写すと、誤った説明が広がります。
- 直し方の案: 理由を次のように書き換えます。
  1. Script Updater は利用者のスクリプトを直さないので、付けても得が無い。
  2. 旧い型が残る間は、`[SerializeReference]` の旧名のデータも旧い型として読まれるので、効く場面が無い（これは私の理解で、実測はしていません）。
  3. macOS の移行で `Mac*` に改名するとき、同じ旧名を `sourceClassName` にする `[MovedFrom]` が要る。いま iOS の型に付けると、1 つの旧名を 2 つの型が名乗ることになる。

**S-3 [C] [正しさ・移行] iOS の 4 ファイルに残る `using JonghyunKim.NativeToolkit.Runtime.Notification;` の扱いが書かれていない**
- 根拠:
  - この `using` があるのは次の 4 か所です。
    - `IosNotificationJsonBuilder.cs:9`
    - `IosNotificationManagerExampleController.cs:8`
    - `IosNotificationPlayerTests.cs:13`
    - `NotificationScenario.cs:8`
  - 差し替えの後は、どれも使われなくなります。
  - `Runtime/Notification/` の中で `#if` が無いのは `NotificationPayloads.cs` だけで、ほかの 8 ファイルは `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`（Manager は `UNITY_STANDALONE_OSX`）です。iOS の Player にこの名前空間があるのは、`NotificationPayloads.cs` があるからです。
- 壊れ方:
  - 今回は壊れません。
  - 後で macOS 用になった 5 型にほかの macOS のファイルと同じ `#if` を付けたり、`Mac*` に移したりすると、残った `using` が iOS の Player でだけ CS0246 になります。Editor では気づけません。
  - iOS の利用者のコードも同じ形で壊れます。
- 直し方の案:
  - D-7 に「4 ファイルからこの `using` を消す」と書きます。
  - 確かめに、`common.md:483` の「旧名が残っていないかを識別子の境界で検査する」を iOS のファイルに当てる grep を入れます。
  - 移行ガイド（D-8）には「この `using` も消す」と書きます。

**S-4 [C] [移行] D-8 の移行ガイドに書くことが足りない**
- 根拠:
  - D-8 には、旧名 → 新名の表と iOS の名前空間の `using` しかありません。
  - 5 型のうち 3 つは macOS と共用でした（`MacNotificationJsonBuilder.cs:21, 41, 56`）。
- 壊れ方: 次の 3 つの利用者が、手順の無いまま残ります。
  - 1 つの payload を iOS と macOS の両方の Builder に渡していた利用者。値を 2 つ作るか、写す処理を書く必要があります。
  - 型を `[SerializeReference]` で持っていた利用者。項目の型を替えると、旧名で保存したデータが新しい型に移りません（`[MovedFrom]` が無いため）。普通の `[SerializeField]` なら項目の名前で読むので値は残ると考えていますが、これは私の理解で、実測はしていません。
  - `#if UNITY_IOS && !UNITY_EDITOR` の中で使っていた利用者。エラーが iOS の書き出しで初めて出ます。
- 直し方の案: この 3 点を段 4 の項目 7 に渡す申し送りとして、D-8 に書き足します。

**S-5 [C] [macOS・記録] `NotificationPayloads.cs` の説明の直しと、関連する規則の記述の範囲が足りない**
- 根拠と直し方の案:
  - **ファイル先頭のコメント**: `NotificationPayloads.cs:7-10` は「iOS と macOS の Manager で共用」「stage 4 で iOS に専用の型」と書いています。D-5 は XML の summary だけを直す計画なので、ここも直す対象に入れます。
    - D-5 で「macOS の通知」と書き直すと、`interruptionLevel`・`relevanceScore`・`attachments` などの説明も macOS 向けに読めます。実際には macOS の Builder はこれらを読みません（`MacNotificationJsonBuilder.cs:21-34`）。そのことを 1 行添えます。
  - **D-5 の理由**: `NotificationAttachmentPayload` は、macOS の Builder は使いませんが、`NotificationContentPayload.attachments` の型です（`NotificationPayloads.cs:55`）。単独では消せないので、理由はこちらにするほうが確かです。
  - **V-4**: macOS のサンプルの `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` の中は、Editor の EditMode では組まれません。「macOS の組み立て」はコメントだけの差分であることで保証する、と書き換えます（例: `git diff` がコメントの行だけであること）。
  - **規則と記録**: 次の 4 つの直し方を 3 章に具体的に書きます。
    - `common.md:471`: パスが今でも古く、5 型はもう `Runtime/Notification/NotificationPayloads.cs` にあります。所属の欄は「macOS」に直します。
    - `common.md:475`: 「iOS と macOS の 9 件」の数え方を直します。
    - `agent-rules/workflows/review-document/workflow.md:46`: `NotificationContentPayload` を「iOS / macOS 専用」と書いています。3 章に挙がっていません。
    - `artifact/topics/os-prefix-violations/README.md:11`: 3 章に挙がっていません。
  - **P5 の確認の記録**: 新しい 5 型は、`IosNotificationPayloads.cs` の今の型と同じく `#if` 無しで全プラットフォームの Player に入ります（Builder も `#if` 無しです）。`review-document/workflow.md` の P5 の確認で問われるので、「今のファイルにそろえて付けない」と D-1 に 1 行書いておきます。

---

**先へ進めるか**: 進めてよいです。A1 は 0 件です。B（S-1）は「JSON が同じ」という中心の約束を確かめる手順なので、V-1 と D-6 に手順を書き足してから実装に入ることを勧めます。C の 4 件は文書に残件として書くか、機械照合に回せば足ります。今回のレビュアーは私 1 人です（Codex は使えませんでした）。

## 2. 判断（こちら）

A1 が 0 件なので、レビューはここで止める（1 回。Codex が使えず、サブエージェントの結果だけで判定した）。止めた後の直しは v2（レビューしない）に入れた。

| 指摘 | 区分 | 判断 | v2 の場所 |
|---|---|---|---|
| S-1 「同じ JSON」の確かめ方 | B（中心） | 受け入れる。旧い型のままテストを書いて緑にし、型の名前だけを替えて緑のまま、の順。型ごとに既定値と全項目の 2 本、`IsSerializable` | D-6、V-1 |
| S-2 `[MovedFrom]` を付けない理由 | C | 受け入れる。リポジトリの実測（API Updater はこのパッケージの `[MovedFrom]` を使わない）に合わせた | D-3 |
| S-3 使われなくなる `using` | C | 受け入れる。4 ファイルから消し、識別子の境界での grep を確かめに入れる | D-7、V-5 |
| S-4 移行ガイドの申し送り | C | 受け入れる。両方の Builder に渡していた人、`[SerializeReference]`、iOS の書き出しで初めて出る誤り | D-8 |
| S-5 macOS の説明と規則の範囲 | C | 受け入れる。ファイルの頭のコメント、macOS の Builder が読まない項目、`NotificationAttachmentPayload` を残す理由、V-4 の書き方、規則と記録の 4 か所、P5 の一行 | D-1、D-5、3 章、V-4 |
