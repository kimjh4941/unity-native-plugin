# artifact/ の見取り図

設計・検討文書の置き場。**機能か、横断かで分ける。**

| ディレクトリ | 対象 | 作り方 |
|---|---|---|
| `features/<feature>/` | OS 機能の開発（clipboard / notification / share） | `agent-rules/workflows/` のワークフローが出力する |
| `topics/<topic>/` | 機能単位でない課題（横断・移行・検証債務など） | 手動で作成する。ワークフローは使わない |

**どちらか迷ったら、影響が 1 機能に閉じるかで決める。**
閉じるなら `features/`、複数機能や全プラットフォームにまたがるなら `topics/`。

---

## features/

ワークフローが出力する文書の置き場。

| ディレクトリ | 中身 | 命名 |
|---|---|---|
| `designs/` | 機能設計・サンプルシーン計画。`design-feature` / `design-sample-scene` が出力 | `YYYY-MM-DD-<os>-<feature>[-sample-scene]-design-vN.md` |
| `plans/` | 旧形式の実装計画（macOS Notification のみ） | `YYYY-MM-DD-<os>-<feature>-...-plan-vN.md` |
| `results/` | 実装結果・実機確認結果。`implement-*` / 手動確認が出力 | `YYYY-MM-DD-<os>-<feature>-<工程>-result-vN.md` |
| `reviews/` | 設計・実装のレビュー記録。`review-*` が出力 | `YYYY-MM-DD-<os>-<feature>-<対象>-review-vN.md` |
| `results/logs/` | 実機確認の生ログ。結果文書の主張の裏付け | `YYYY-MM-DD-<os>-<feature>-verify-manual-sessionN.log` |
| `issues/` | **その機能に閉じる未対応課題。** 手動で作成する | `<slug>.md`（kebab-case） |

**`vN` は上書きしない。** 同名があれば増やす。
古い版を消さないのは、レビューで何が指摘されて何が変わったかを追うため。

**OS ごとにディレクトリを分けない。** `<feature>` は機能名のみで、OS はファイル名で区別する。

### 機能ごとの未対応課題

| 課題 | 機能 | 進捗 |
|---|---|---|
| [unreachable-notification-apis](features/notification/issues/unreachable-notification-apis.md) | notification | 未着手。通知の 3 API がサンプルから到達できず、非対応の根拠が未検証 |

---

## topics/

機能単位でない課題の置き場。

- 各トピックは `topics/<topic>/README.md` を必ず持つ。課題の内容・状態・対応方針はここに書く
- `plans/` `designs/` `results/` `reviews/` は必要になったものだけ作る
- ディレクトリ名は kebab-case

### トピック一覧

1 行 1 トピック。詳細は各 README に書き、ここには要約だけを置く。

| トピック | 分類 | 状態 | 最終更新 | 概要 |
|---|---|---|---|---|
| [os-prefix-violations](topics/os-prefix-violations/README.md) | 命名規則 | 一部対応 | 2026-09-03 | OS 接頭辞ルールの逸脱 11 件（Runtime）。改名は `public` の破壊的変更。案 0 完了、案 1〜3 未着手 |
| [event-subscriber-isolation](topics/event-subscriber-isolation/README.md) | 契約 | 一部対応 | 2026-09-08 | 共通イベントの購読者が互いから隔離されていない。1 人の例外が後続を止める。**Windows 完了**、Android / iOS / macOS 未着手 |
| [device-verification-records](topics/device-verification-records/README.md) | ドキュメント | 一部対応 | 2026-09-10 | 実機確認済みなのに結果文書が「未実施」のまま。**9 文書**。注記のみ入れた段階 |
| [cross-platform-testing](topics/cross-platform-testing/README.md) | テスト債務 | 一部対応 | 2026-07-26 | テスト方針（`testing.md`）の策定記録。層 2b / 3 は未導入 |
| [windows-c-abi-2](topics/windows-c-abi-2/README.md) | 移行 | 企画中 | 2026-09-23 | Windows の C ABI が 1.x から 2.0.0 に置き換わる。P/Invoke 47 本と JSON 依存層の書き直し。native-toolkit のマージ待ち |

状態の語彙: 未着手 / 企画中 / 設計済 / 進行中 / 一部対応 / 完了

**トピックを新設したら、この表に 1 行足すこと。**
かつて課題ファイルが `artifact/` 直下に置かれていた頃は、関連する規則や設計書からしか
参照されておらず、その領域を触る人以外には存在が見えていなかった。

### 課題文書の体裁

`topics/<topic>/README.md` と `features/<feature>/issues/<slug>.md` に共通:

```
# <一行で問題そのもの>

- 記録日: YYYY-MM-DD
- 分類: 横断課題 / <機能> 固有
- 発見経緯: どの作業のどの局面で出たか
- 対応方針: なぜ今やらないか
- 進捗: 未着手 / 一部完了（どこまで）
```

### ここに書かないもの

| 書かないもの | 書く場所 |
|---|---|
| 機能開発のタスク | `features/` の設計書とチケット |
| 作業ごとの進捗 | 各トピックの README、または `results/` |
| 件数・診断数などの実測値 | 各トピックの README（生成ファイルがあればそちら） |

---

## 設計書の機械照合

設計書は保存直後に照合をかける（`design-feature` / `design-sample-scene` の必須ステップ）。

```
python3 scripts/check_design_consistency.py <対象ファイル>
```

**Windows で `python3` が Microsoft Store のエイリアスに解決されることがある。**
コードを実行せず終了するため、検査が走らないまま成功に見える。
`python3 -c "print(1)"` が `1` を出さない環境では `python` を使う。

- `FAIL` が 1 件でもある間は次の工程へ進まない
- `SKIP` は合格ではない。「その検査の対象が文書に存在しない」意味であり、
  本来あるべき節が欠けていないかを確認する
