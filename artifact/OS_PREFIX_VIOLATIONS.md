# OS 接頭辞ルールの逸脱（Runtime 11 件。Tests 2 件は解消済み）

- 記録日: 2026-09-03
- 更新: 2026-09-03 案 0（Tests 2 件）を実施。**残るのは Runtime 11 件のみ。**
- 分類: 横断課題（Clipboard 固有ではない。Clipboard / Notification / Share / Common の 4 領域にまたがる）
- 発見経緯: macOS Clipboard の設計レビュー中に、`IosClipboardJsonReader` を改名すべきかを検討して判明
- 検査手段: 下記「検査コマンド」
- 対応方針: **macOS Clipboard の実装には含めない。別途対応。**
- 進捗: 案 0 **完了**（下記）／案 1〜3 は未着手

---

# 何が問題か

`agent-rules/coding-rules/common.md`「命名: OS 接頭辞と、共通ファイルを作らない方針」は次を定めている。

- `Runtime/<Feature>/` と `Tests/` は**プラットフォーム単位で管理し、共通ファイルを作らない**
- ファイル名・型名に `Android` / `Ios` / `Mac` / `Windows` の接頭辞を付ける
- **例外は `Runtime/Common/` のみ**。意図的に共通とした横断インフラの置き場所

**接頭辞なしは「`Common/` の横断インフラである」ことを意味する**のが目的である。しかし発見時、機能ディレクトリと `Tests/` に接頭辞なしのファイルが 13 件あり、いずれも単一プラットフォーム専用だった。**うち Tests の 2 件は解消済みで、残るのは Runtime の 11 件である。**

`Common/` 配下で正しく共通なのは `UnityMainThreadDispatcher` の 1 つだけで、同じ `Common/` にある `IconConfiguration` は macOS 専用である。

---

# 内訳（2026-09-03 実測）

## Android 専用なのに接頭辞がない（10 件）

| ファイル | 型の種別 | 導入 |
|---|---|---|
| `Clipboard/ClipboardOperationResult.cs` | `public readonly struct` | 2026-08-01 `feature/UNT-8 (#21)` |
| `Clipboard/ClipboardReadResult.cs` | `public enum` + `ClipItem` / `ClipContents` | 2026-08-01 `feature/UNT-8 (#21)` |
| `Clipboard/ClipboardDescriptionResult.cs` | `public sealed class` | 2026-08-01 `feature/UNT-8 (#21)` |
| `Notification/NotificationResult.cs` | `public readonly struct` | 2026-04-18 `feat(notification): add action button tap callbacks` |
| `Notification/NotificationActionResult.cs` | `public readonly struct` | 同上 |
| `Notification/NotificationReceivedResult.cs` | `public readonly struct` | 同上 |
| `Share/ShareOperationResult.cs` | `public readonly struct` | 2026-07-04 `feature/UNT-5 (#14)` |
| `Share/ShareCallbackResult.cs` | `public readonly struct` | 同上 |
| `Share/ShareChooserActionResult.cs` | `public readonly struct` | 同上 |
| `Share/ShareChooserActionCallbackCoordinator.cs` | `public sealed class` | 同上 |

## macOS 専用なのに接頭辞がなく、しかも `Common/` にある（1 件）

| ファイル | 型の種別 | 導入 |
|---|---|---|
| `Common/IconConfiguration.cs` | `public class` | 2025-11-16 `[mac] feat(mac): enhance macOS dialog system` |

利用箇所は `Dialog/MacDialogManager.cs` と `UI/macOS/Dialog/MacDialogManagerExampleController.cs` のみ。
**名前と置き場所の両方が実態と食い違っている。** `Common/` にあるため共有ユーティリティに見えるが、macOS Dialog 専用である。

## Tests 配下（2 件）— **解消済み（2026-09-03。案 0）**

`Tests/` のファイル名・クラス名にも接頭辞を付ける規則の対象。発見時は 28 ファイル中 26 件が準拠しており、逸脱は次の 2 件だった。

| ファイル | テスト対象 | 実際の所属 | 対応 |
|---|---|---|---|
| `Tests/Runtime/ShareChooserActionCallbackCoordinatorTests.cs` | `ShareChooserActionCallbackCoordinator` | Android | `AndroidShareChooserActionCallbackCoordinatorTests.cs` へ改名 |
| `Tests/Runtime/ShareResultTests.cs` | `ShareOperationResult` ほか。**`IosShareResult` への言及も混在** | Android（一部 iOS） | `AndroidShareResultTests.cs` ＋ `IosShareResultTests.cs` へ分割 |

`ShareResultTests.cs` は「1 つのテストファイルが複数プラットフォームの型を扱わない」という規則にも抵触していた。改名と同時に Android 8 件 / iOS 6 件へ分割し、両方の規則に適合させた。

なお `Tests/` はコンパイルガードを持たないファイルが多い（テストアセンブリが `includePlatforms: ["Editor"]` のため）。**ガードの有無は所属の判断材料にならない**点は Runtime と同じ。

## 正しく共有型（1 件・対応不要）

| ファイル | 利用者 |
|---|---|
| `Common/UnityMainThreadDispatcher.cs` | Android / iOS / macOS / Windows |

---

# なぜ混入したか

**最初に実装したプラットフォームが接頭辞を付けなかったため。**

導入時期を見ると、いずれも「その機能の最初の実装」で入っている。

| 機能 | 最初の実装 | そのとき他プラットフォームは |
|---|---|---|
| Clipboard | Android（2026-08-01） | 存在しない |
| Notification | Android（2026-04-18） | 存在しない |
| Share | Android（2026-07-04） | 存在しない |
| Dialog の `IconConfiguration` | macOS（2025-11-16） | 存在しない |

**2 番目のプラットフォームを追加したときに接頭辞を付け直す手順がなかった**ため、後発の iOS / macOS / Windows だけが接頭辞を持ち、先発だけが持たない状態になった。

`IosClipboardOperationResult.cs` の doc コメントに「Distinct from the Android `ClipboardOperationResult`」と書かれているとおり、**実装者は不整合を認識したうえで、後発側に接頭辞を付けることで回避していた**。

---

# これが実際に引き起こした誤り

macOS Clipboard 設計書の v1〜v6 で、`IosClipboardJsonReader` を `ClipboardJsonReader` に改名する根拠として次を書いていた。

> 本パッケージは共有型を接頭辞なしで置く方針（`ClipboardOperationResult` / `ClipboardReadResult`）

**この方針は存在しない。** 挙げた 2 つは Android 専用型である。

さらに v2 のレビューがこの根拠を「成立する」と確認していたが、その確認は**プラットフォームガードの有無**を見たもので、**実際の利用箇所**を見ていなかった。ガードが無いことは共有であることを意味しない。

**設計レビュー 5 ラウンドを通過した誤り**であり、`coding-rules/common.md` に「ガードの有無で判断してはならない」を明記した理由でもある。

---

# 対応案

## 影響範囲

Runtime の 11 件はすべて `public` 型であり、**改名は破壊的変更**になる。パッケージの公開型は 81 個で、その 13% にあたる。

Tests の 2 件は**パッケージ利用者から見えない**ため、改名は破壊的変更にならない。**Runtime とは独立して先に直せる**（実施済み。案 0 参照）。

利用者は次の 3 者。

1. パッケージ内部の Manager / Parser
2. パッケージ内部の ExampleController（`Runtime/UI/`）
3. **パッケージ利用者のアプリコード**（把握できない）

## 案 0: Tests の 2 件を先に直す（Runtime とは独立）— **完了（2026-09-03）**

Tests は公開 API ではないので、いつでも安全に改名できる。

| 現在 | 改名後 | 状態 |
|---|---|---|
| `ShareChooserActionCallbackCoordinatorTests.cs` | `AndroidShareChooserActionCallbackCoordinatorTests.cs` | 完了 |
| `ShareResultTests.cs` | `AndroidShareResultTests.cs`（`IosShareResult` を扱う分は `IosShareResultTests.cs` へ分離） | 完了 |

`git mv` で `.cs` と `.cs.meta` を対で移動し、クラス名も揃えた。Runtime の改名方針が決まるのを待つ必要はなかった。

**実施内容**

- 2 ファイル 4 件（`.cs` と `.cs.meta`）を `git mv`。GUID を保持している
- `ShareResultTests` の 14 件を **Android 8 件 / iOS 6 件**に分割した。「1 つのテストファイルが複数プラットフォームの型を扱わない」という規則にも同時に適合した
- 新規 `IosShareResultTests.cs` には `#if UNITY_IOS || UNITY_EDITOR` を付けた。**テスト対象の `IosShareResult.cs` が同じガードを持つため**（`common.md`「テストのコンパイルガードは対象型に合わせる」）
  - なお既存の `IosShareJsonBuilderTests.cs` はガードを持たない。テストアセンブリが `includePlatforms: ["Editor"]` でありコンパイルは通るが、規則には沿っていない。**本課題の範囲外**として残した
- 2 ファイルとも Android 型のみを扱うためガードは付けていない（対象の `ShareOperationResult` ほかが無ガードのため）
- EditMode **517 件 pass / 失敗 0**。件数は改名前と同じ（14 件を 8 + 6 に分けただけ）
- 検査コマンド（後述）の Tests 用が**何も出力しない**ことを確認した

## 案 1: Runtime の一括改名（推奨）

`Android` / `Mac` 接頭辞を付け、`IconConfiguration` は `Dialog/` へ移す。

| 現在 | 改名後 | 移動先 |
|---|---|---|
| `ClipboardOperationResult` | `AndroidClipboardOperationResult` | 変更なし |
| `ClipboardReadResult` / `ClipItem` / `ClipContents` | `AndroidClipboardReadResult` / `AndroidClipItem` / `AndroidClipContents` | 変更なし |
| `ClipboardDescriptionResult` | `AndroidClipboardDescriptionResult` | 変更なし |
| `NotificationResult` ほか 3 件 | `AndroidNotification*` | 変更なし |
| `ShareOperationResult` ほか 4 件 | `AndroidShare*` | 変更なし |
| `IconConfiguration` | `MacIconConfiguration` | `Common/` → `Dialog/` |

- メジャーバージョンで行う。CHANGELOG に破壊的変更として記載する
- `git mv` で `.cs` と `.cs.meta` を対で移動し GUID を保持する
- 移行期間を設けるなら `[Obsolete]` の型エイリアスを 1 バージョン残す

## 案 2: 現状維持 + ルールを「新規のみ適用」に緩める

既存 11 件を逸脱として許容し、新規追加にのみ規則を適用する。

- 破壊的変更を避けられる
- ただし**規則の目的（名前だけで所属が判断できる）が達成されない**。今回のような誤読が再発する
- 採る場合は、`common.md` の「既知の逸脱」一覧を正本として維持し、レビュー時に必ず参照させる

## 案 3: 段階的改名

機能単位で、その機能に手を入れるタイミングに合わせて改名する。

- Clipboard は macOS 追加が終わったら 3 件
- Share / Notification は次の変更時
- 期間中は規則が混在する

---

# 再発防止（実施済み）

`agent-rules/coding-rules/common.md`「命名: OS 接頭辞と、共通ファイルを作らない方針」に次を明記した。

- 機能ディレクトリと `Tests/` に接頭辞なしのファイルを作らないこと。**同じロジックが必要なら共通化せず複製する**
- `Runtime/Common/` だけが例外で、**横断インフラに限る**こと
- **プラットフォームガードの有無で判断してはならない**こと
- 1 つのテストファイルが複数プラットフォームの型を扱わないこと
- 上記のうち **Runtime 11 件**を「既知の逸脱（新規実装で真似しない）」として列挙（Tests 2 件は解消済みとして別表に移した）

`agent-rules/workflows/design-feature/workflow.md` ステップ 6 に、他プラットフォームの既存実装を共有化する案を採らないことを追加した。

**3 つのレビューワークフロー**（`review-document` / `review-implementation-feature` / `review-implementation-sample-scene`）に「プラットフォーム独立性」の検査項目 P1〜P5 を追加し、**違反を A 区分（止めて直す）**と定めた。とくに次の 2 点を明示している。

- 既存型の所属は**ガードではなく実際の利用箇所**で判定する
- **本ファイルの逸脱 11 件を前例として引用しない**

---

# 検査コマンド

接頭辞なしのファイルと、その実際の利用プラットフォームを一覧する。

```bash
cd Packages/com.jonghyunkim.nativetoolkit

# 接頭辞なしの Runtime ファイル（UI 配下と Common 配下を除く）
find Runtime -name "*.cs" -not -path "Runtime/UI/*" -not -path "Runtime/Common/*" \
  | xargs -n1 basename | grep -v "^\(Android\|Ios\|Mac\|Win\)" | sort

# 接頭辞なしのテストファイル
find Tests -name "*.cs" | xargs -n1 basename \
  | grep -v "^\(Android\|Ios\|Mac\|Win\)" | sort

# 各型を実際に使っているプラットフォーム
for t in ClipboardDescriptionResult ClipboardOperationResult ClipboardReadResult \
         IconConfiguration NotificationActionResult NotificationReceivedResult \
         NotificationResult ShareCallbackResult ShareChooserActionCallbackCoordinator \
         ShareChooserActionResult ShareOperationResult UnityMainThreadDispatcher; do
  users=$(grep -rl "\b$t\b" Runtime Tests --include="*.cs" \
    | xargs -n1 basename | grep -v "^$t\.cs$" \
    | grep -o "^\(Android\|Ios\|Mac\|Windows\)" | sort -u | tr '\n' ' ')
  printf "%-42s %s\n" "$t" "${users:-（参照なし）}"
done
```

**判定の注意:** `IosClipboardOperationResult.cs` は doc コメントで Android 型に言及するだけなので、`ClipboardOperationResult` の利用者に iOS が混じって見える。コード上の参照かどうかを確認すること。

---

# 関連

- 規則の正本: `agent-rules/coding-rules/common.md`「命名: OS 接頭辞と、共通ファイルを作らない方針」
- 設計段階での強制: `agent-rules/workflows/design-feature/workflow.md` ステップ 6
- レビューでの強制: `review-document` / `review-implementation-feature` / `review-implementation-sample-scene` の各ステップ 6「プラットフォーム独立性」
- 誤読が発生した設計書: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v1.md` 〜 `v6.md` の 2.4 / 4.2 / D-1
- 誤読を「成立する」と確認したレビュー: `artifact/reviews/clipboard/2026-09-03-macos-clipboard-design-review-v2.md`
- ディレクトリ構成の議論: `Runtime/` は接頭辞で分け、`UI/` だけ `UI/<Platform>/<Feature>/` で分ける（`common.md` に記載）
