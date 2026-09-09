# artifact/ の見取り図

成果物の置き場。**この索引は「直下の課題ファイル」を見つけるためにある。**
`designs/` や `results/` はワークフローが日付とバージョンで命名するので迷わないが、
直下のファイルは誰も探しに来ないまま残るため、ここに一覧を置く。

---

## 直下: 未対応として切り出した課題

**どれも「今の作業に含めない」と判断したものが残っている。** 着手前に進捗欄を見ること。

| ファイル | 内容 | 進捗 |
|---|---|---|
| [OS_PREFIX_VIOLATIONS.md](OS_PREFIX_VIOLATIONS.md) | OS 接頭辞ルールの逸脱 11 件（Runtime）。改名は `public` の破壊的変更 | 案 0 完了。案 1〜3 未着手 |
| [EVENT_SUBSCRIBER_ISOLATION.md](EVENT_SUBSCRIBER_ISOLATION.md) | 共通イベントの購読者が互いから隔離されていない。1 人の例外が後続を止める | **Windows 完了**。Android / iOS / macOS 未着手 |
| [UNREACHABLE_NOTIFICATION_APIS.md](UNREACHABLE_NOTIFICATION_APIS.md) | 通知の 3 API がサンプルから到達できない。非対応の根拠が未検証 | 未着手 |

**課題ファイルを新設したら、この表に 1 行足すこと。**
既存 2 本は関連する規則や設計書からしか参照されておらず、
その領域を触る人以外には存在が見えていなかった。

課題ファイルの体裁（既存 3 本に共通）:

```
# <一行で問題そのもの>

- 記録日: YYYY-MM-DD
- 分類: 横断課題 / <機能> 固有
- 発見経緯: どの作業のどの局面で出たか
- 対応方針: なぜ今やらないか
- 進捗: 未着手 / 一部完了（どこまで）
```

---

## サブディレクトリ

| ディレクトリ | 中身 | 命名 |
|---|---|---|
| `designs/` | 機能設計・サンプルシーン計画。`design-feature` / `design-sample-scene` が出力 | `YYYY-MM-DD-<os>-<feature>[-sample-scene]-design-vN.md` |
| `plans/` | 旧形式の実装計画（macOS Notification のみ） | `YYYY-MM-DD-<os>-<feature>-...-plan-vN.md` |
| `results/` | 実装結果・実機確認結果。`implement-*` / 手動確認が出力 | `YYYY-MM-DD-<os>-<feature>-<工程>-result-vN.md` |
| `reviews/` | 設計・実装のレビュー記録。`review-*` が出力 | `YYYY-MM-DD-<os>-<feature>-<対象>-review-vN.md` |
| `results/*/logs/` | 実機確認の生ログ。結果文書の主張の裏付け | `YYYY-MM-DD-<os>-<feature>-verify-manual-sessionN.log` |

**`vN` は上書きしない。** 同名があれば増やす。
古い版を消さないのは、レビューで何が指摘されて何が変わったかを追うため。

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
