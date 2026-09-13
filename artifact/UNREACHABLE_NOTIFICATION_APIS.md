# サンプルから到達できない通知 API 3 件（未確認の非対応）

- 記録日: 2026-09-09
- 分類: Windows Notification 固有。ただし**根拠が未検証**という点が本質
- 発見経緯: `check_design_consistency.py` を Windows で動くようにした結果、
  通知サンプル計画 v2 が挙げるボタン名が擬似コードにしか無いと報告された。
  追ったところ、ボタン自体が実装後に取り外されていた
- 対応方針: **別タスク。** 判断に実機確認が要るため、記録だけ先に残す
- 進捗: 未着手

---

## 何が起きているか

`WindowsNotificationManager` は次の 3 API を public で公開しているが、
**サンプルシーンから到達する手段が無い。**

| API | サンプルのボタン |
|---|---|
| `SetBadge(int)` | 2026-06-13 に削除 |
| `RemoveNotificationById(uint)` | 同上 |
| `GetAllNotifications(...)` | 同上（`GetAllNotificationsCompleted` の購読も削除） |

削除は `e29968e`「feat(sample): overhaul Windows notification sample for unpackaged apps」。
理由として **`Remove unsupported APIs for unpackaged apps`** と述べている。

## なぜ未着手のまま置くか

**「unpackaged では非対応」の根拠がコミットメッセージ 1 行しかない。**

- 機能設計 v2 の 8 章「要検証事項」は `getAllNotifications` のバッファ不足を挙げるが、
  **3 API が unpackaged で動かないという結論には到達していない**
- 実装結果にも実機確認記録にも、確かめた形跡が無い
- Unity の Windows スタンドアロンが常に unpackaged であること自体は設計 v2 に記載があり、
  前提とは整合する。**整合することと、確かめたことは別**

したがって XML コメントに「unpackaged では非対応」と書くと、
**未確認の事実を仕様として固定してしまう。**

## 現状の危うさ

| 場所 | 状態 |
|---|---|
| `WindowsNotificationManager` | 3 API を公開したまま。非対応の注記が無い |
| マニュアル | 3 API を記載していない。**結果として**矛盾していないだけ |
| サンプル計画 v2 / 実装結果 v1 | 2026-09-09 に経緯を追記済み |

利用者は XML コメントだけを見て `SetBadge` を呼べる。
動かない場合、ライブラリの不具合と区別が付かない。

## 案

| # | 内容 | コスト |
|---|---|---|
| 1 | **実機で 3 API を叩いて可否を確定させる。** 結果に応じて XML コメントか実装を直す | 実機確認 1 回。**推奨** |
| 2 | サンプルにボタンを戻し、失敗することを含めて見せる | 実装 + 実機 |
| 3 | XML コメントに「未確認」と書く | 小。ただし利用者への回答にならない |

**1 を先に。** 非対応が確定すれば 3 の文面が「非対応」に変わり、
対応していれば 2 でサンプルに戻す判断になる。どちらに転んでも 1 が要る。

## 参照

- 経緯: `artifact/results/notification/2026-06-06-windows-notification-implement-sample-scene-result-v1.md` 6 章
- 計画: `artifact/designs/notification/2026-06-06-windows-notification-sample-scene-design-v2.md` 5.2 の注記
- 削除コミット: `e29968e`（2026-06-13）
