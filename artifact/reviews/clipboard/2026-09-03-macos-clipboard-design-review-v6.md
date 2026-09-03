# レビュー結果

- 日付: 2026-09-03
- 対象ファイル: artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md
- 前版レビュー: artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v5.md
- 機能名: clipboard
- プラットフォーム: macOS
- ドキュメント種別: 実装計画書
- レビュー実施: **Codex（別モデル）**。v1〜v5 は Claude 側サブエージェントが担当
  - `review-document` の止める基準「レビュアーを替えて 1 回通していること」を満たすためのラウンド
- 指摘区分: A1（実装時に気づけない）/ A2（実装時に気づける）/ B（検証手段）/ C（記述整合）
- 機械照合: `python3 scripts/check_design_consistency.py` は 17 項目すべて OK

---

## 結論

v6 は M5-1 を正しく反映しています。M5-2 の「成功した StartObserving で無条件解放すると、その場で登録を失う」という診断も正しいです。

ただし、M5-2 の修正を iOS と同じゲートにしたことで、macOS ネイティブ固有の「再 Start 失敗時は従来の監視を継続する」という契約との不整合が残っています。加えて、新規の A1 を2件検出しました。

| 分類 | 件数 |
| --- | ---: |
| A1: 実装前に計画修正が必要 | 3 |
| A2: 実装・テストで必ず検出される | 0 |
| B: テスト／DoD不足 | 1 |
| C: 文書整合性のみ | 0 |

## 重点確認事項

### M5-1 — 正しい

[v6:1212](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1212) の「複数操作が共有する結果型は `result.Operation` からスロットを引く」という一般化と、`MacClipboardOwnershipResult` を `TakeOwnershipCallback` に追加する方針は正しいです。

既存 iOS 実装も、共有結果型について [`result.Operation` から callback を選択](Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardManager.cs:705)し、copy / append を別スロットとして扱っています。macOS では結果型の分割が異なるため、専用の `TakeOwnershipCallback` を設けるのが妥当です。

### M5-2 — 診断は正しいが、修正条件は不完全

[v6:1315](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1315) の診断は正しいです。成功した Start の完了時に無条件で `ReleaseChangeRegistrationIfOwned()` を呼ぶと、`G <= G` が成立して新登録が消えます。

しかし、[`!isSuccess || operation == StopObserving`](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1308) は、macOS の再 Start 失敗時には正しくありません。詳細は A1-1 です。

## 新規指摘

### A1-1: 再 Start 失敗時、ネイティブ監視は継続するのに managed の旧 callback が失われる

該当箇所:

- [Start 前に `s_onChanged` を上書きする処理](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1245)
- [Start 失敗時の解放ゲート](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1303)
- [「再 Start で前回登録を置換する」契約](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1020)

根拠:

ネイティブは interval 検証と scope 解決を、既存監視を止める前に行います。

- [interval の事前検証](native-toolkit:mac/MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift:72)
- [scope の事前解決](native-toolkit:mac/MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift:78)
- [`stop()` はその後](native-toolkit:mac/MacLibrary/MacLibrary/Clipboard/Presentation/ClipboardChangeMonitor.swift:80)
- [公開契約も既存監視を維持すると明記](native-toolkit:mac/MacLibrary/MacLibrary/Clipboard/Manager/MacClipboardManager.swift:500)
- [ネイティブテスト IT-09 も明示的に保証](native-toolkit:mac/MacLibrary/MacLibraryTests/Clipboard/Presentation/ClipboardChangeMonitorTests.swift:38)

一方、v6 はネイティブ成功前に旧 `s_onChanged` を新 callback で上書きし、失敗完了時には新登録を解放します。したがって失敗後は次の状態になります。

| ケース | ネイティブ状態 | v6 の managed 状態 |
| --- | --- | --- |
| 初回 Start 失敗 | 未監視 | callback なし。正しい |
| 再 Start 失敗 | **旧監視を継続** | **旧 callback も新 callback もなし** |
| 再 Start 処理中の旧監視イベント | 旧監視から配送可能 | 成功前なのに新 callback へ届きうる |
| Stop 成功 | 停止 | callback なし。正しい |
| P/Invoke 前の Stop 失敗 | 旧監視を継続 | callback を失う可能性 |

コンパイルも現在の `start → stop → start` テストも通り得ますが、実行時の API 契約が崩れるため A1 です。

修正案:

active と pending を分離してください。

- 成功済み登録: `s_onChanged` / active generation
- Start 中の候補: `s_pendingOnChanged` / pending generation
- Start 成功時だけ pending を active に昇格
- Start 失敗時は pending のみ破棄し、active を維持
- Stop 成功時だけ active を破棄
- P/Invoke 呼び出し自体の失敗では、ネイティブ状態が変化していないため active を維持

このモデルでは、現在の `ReleaseChangeRegistrationIfOwned` だけでは状態を表現できません。

### A1-2: Swift `Int` の `changeCount` を C# `int` に狭めている

該当箇所:

- [`MacPasteboardOwnership.ChangeCount` が `int`](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:568)
- [`TryParseChangeCount` の出力が `int`](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:818)
- [各公開結果／イベントも `int`](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:878)

根拠:

ネイティブの JSON スキーマは `changeCount: Int` です。

- [OwnershipJson](native-toolkit:mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardJsonParser.swift:51)
- [ReadResultJson](native-toolkit:mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardJsonParser.swift:61)
- [SnapshotJson](native-toolkit:mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardJsonParser.swift:81)
- [ChangeEventJson](native-toolkit:mac/UnityMacPlugin/UnityMacPlugin/Clipboard/UnityMacClipboardJsonParser.swift:225)

64-bit macOS の Swift `Int` は64ビットですが、既存 C# reader の [`TryGetInt`](Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonReader.cs:171) は、[`int` 範囲外を失敗させます](Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonReader.cs:179)。

そのため `changeCount > int.MaxValue` になると、正常なネイティブ応答が 9006 に変換されます。特に ownership は append 前の比較に使われるため、単なる表示値ではありません。

修正案:

- 全 `changeCount` プロパティを `long` に統一
- parser に `TryGetInt64` を追加
- ownership の入力 JSON も `long` を保持
- `int.MaxValue + 1` の ownership/read/snapshot/change-count/event のパーステストを追加
- `matchingItemIndexes` は managed collection の添字なので `int` のままでよい

### A1-3: `readonly struct` の `default` が結果型の不変条件を破る

該当箇所:

- [`IsSuccess == true ⇔ Error == null`、例外なし](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:862)
- [`default(T)` を失敗結果としてテストする方針](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1617)
- [`Operation` は非 nullable な `string`](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:875)

根拠:

`default(MacClipboardOperationResult)` は必ず以下になります。

```text
IsSuccess = false
Error     = null
Operation = null
```

したがって「失敗なら Error は非 null」「Operation は非 null」の両方を破ります。既存 iOS 型にも同じ構造があり、[`Operation` を Never null と記述](Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardOperationResult.cs:21)しながら、実体は自動プロパティを持つ struct です。既存実装を踏襲しても、v6 が宣言する不変条件の証明にはなりません。

修正案は次のどちらかです。

- 結果型を private constructor の `sealed class` にして、Manager が null を返さない契約にする
- struct を維持するなら `default` を「失敗結果」ではなく「未初期化値」と定義し、`IsInitialized` または `Status.Unknown` を追加する。不変条件を factory／Manager 生成値に限定する

現在の「default は失敗だが Error は null」という状態を正規の失敗結果として承認しない方が安全です。

### B-1: M5-1／M5-2 の回帰条件がテスト項目に固定されていない

該当箇所:

- [`OwnershipResult.Operation` の値の区別だけを検証](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1618)
- [監視テストは `start → stop → start` の正常系のみ](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1645)
- [`CompleteOwnershipForTests` 自体には operation がある](artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v6.md:1394)

`Operation` プロパティが copy / append を区別できても、`FireOwnershipResult` が正しいスロットを取得することは保証できません。また、正常な `start → stop → start` では A1-1 の再 Start 失敗を検出できません。

修正案:

PlayMode の必須項目に以下を明記してください。

- Copy 完了で copy callback だけが発火し、copy スロットと in-flight が解放される
- Append 完了で append callback だけが発火し、append スロットと in-flight が解放される
- Start 成功後に `HasChangeRegistrationForTests == true` で、変更注入により `onChanged` が発火する
- 成功済み Start → 再 Start 失敗後も旧 callback が残り、新 callback は発火しない
- 再 Start 成功時に初めて旧 callback から新 callback へ置換される
- Stop 成功では登録を失い、ネイティブ呼び出し開始前の失敗では旧登録を維持する

## 実装着手可否

**現状は着手不可です。** A1-1 はネイティブで明示的に保証・テストされている契約との不一致であり、A1-2 は長期稼働時のデータ幅、A1-3 は公開結果型の基本不変条件に関わります。この3件を計画へ反映し、B-1 の回帰テストを DoD に固定した後であれば着手可能です。

文書構造、章参照、件数、既知の v1〜v5 指摘について新たな C 指摘はありません。ファイル編集は行っていません。`review-document` スキルの一次情報照合・既存指摘の重複排除・実装可能性確認の観点を適用しました。
