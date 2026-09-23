# 実機確認は済んでいるのに、結果文書が「未実施」のまま

- 記録日: 2026-09-10
- 分類: 横断課題（**3 機能 × 3 プラットフォーム、計 9 文書**）
- 発見経緯: Windows の M-22（IL2CPP 再実施）の必要性を検討する過程で、
  「`[MonoPInvokeCallback]` は一度も実行されていない」と**誤って判断した**。
  実際には iOS / Android で実機確認が済んでおり、記録だけが古かった。
  その後、他機能にも同じずれがあるかを洗い出して 9 文書に広がった
- 対応方針: **別タスク。** 実施日・端末・消化項目を知る人からの情報が要る
- 進捗: 該当 9 文書の冒頭に訂正の注記を入れた。**本文と詳細記録は未着手**

---

## 1. 何が起きているか

サンプル画面を実機で動かした証跡（スクリーンショット）があるにもかかわらず、
結果文書が**「実機環境が無いため未実施」と書いたまま**になっている。

| # | 文書 | 本文の記述 | 証跡 |
|---|---|---|---|
| 1 | `results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v3.md` | `M-1〜M-24（実機手動確認）｜実機 iOS 18 以降が必要。未実施` | 23 枚 |
| 2 | `results/clipboard/2026-08-22-ios-clipboard-implement-sample-scene-result-v3.md` | S-10〜S-32 はすべて未実施 | 23 枚 |
| 3 | `results/clipboard/2026-07-26-android-clipboard-implementation-feature-result-v3.md` | `実機での手動確認（計画 7.3、全18項目）: 実機環境がないため未実施` | 15 枚 |
| 4 | `results/clipboard/2026-07-26-android-clipboard-implement-sample-scene-result-v2.md` | `5.2 実機確認（未実施、理由: 実機/エミュレータ環境が本セッションに無い）` | 15 枚 |
| 5 | `results/share/2026-07-05-ios-share-implementation-feature-result-v3.md` | `手動確認（実機 iOS 18+、計画書 §7.3）: 未実施` | 10 枚 |
| 6 | `results/share/2026-07-05-ios-share-implement-sample-scene-result-v2.md` | `上記手動確認観点はすべて未実施` | 10 枚 |
| 7 | `results/macos-notification/2026-05-16-macos-notification-implementation-result-v1.md` | `全手動確認項目｜macOS 実機・Standalone ビルドが必要` | 8 枚 |
| 8 | `results/notification/2026-06-06-windows-notification-implementation-feature-result-v1.md` | `手動確認（実機）全件｜native DLL + Windows 実機が必要` | 4 枚 |
| 9 | `results/notification/2026-06-06-windows-notification-implement-sample-scene-result-v1.md` | `全手動確認｜Windows 実機 + native DLL が必要` | 4 枚 |

**9 文書とも冒頭に訂正の注記を入れた。本文は直していない。**
実施日・端末・OS バージョン・消化した項目・残った失敗が分からないまま埋めると、
**別の種類の誤りになる**ため。

## 2. 実施済みである根拠

`manual/1.10.0/images/<platform>/<feature>/` に
`Example_<Platform><Feature>Manager_*.png` という命名の画像がある。
**サンプル画面を実機で動かさないと撮れない。**

例:

```
Example_IosShareManager_ShareFile.png
Example_WindowsNotificationManager_ShowProgressNotification.png
Example_AndroidClipboardManager_GetDescription.png
```

## 3. 洗い出し結果（2026-09-10）

証跡の有無と、各機能の**最新**結果文書の記述を突き合わせた。

| プラットフォーム / 機能 | 証跡 | 文書の記述 | 判定 |
|---|---|---|---|
| android / clipboard | 15 枚 | まるごと未実施 | **ずれ。注記済み** |
| ios / clipboard | 23 枚 | まるごと未実施 | **ずれ。注記済み** |
| ios / share | 10 枚 | まるごと未実施 | **ずれ。注記済み** |
| macos / notification | 8 枚 | まるごと未実施 | **ずれ。注記済み** |
| windows / notification | 4 枚 | まるごと未実施 | **ずれ。注記済み** |
| macos / clipboard | 8 枚 | **実機実施と明記** | 正しい |
| windows / clipboard | 0 枚 | 実機実施と明記（2026-09-09） | 正しい（画像は未撮影） |
| macos / share | 9 枚 | **個別 2 項目のみ**「実機が必要」 | **要確認**（4 節） |
| android / share | 12 枚 | **個別 1 項目のみ**「実機検証が必要」 | **要確認**（4 節） |
| 全 4 プラットフォーム / dialog | 7 〜 8 枚 | **結果文書が存在しない** | 別問題（5 節） |

**`macos / clipboard` が反例として重要。** 同じ時期の同じ工程で、こちらは
「7.5 の手動確認 32 項目（実機実施）」と正しく書けている。
**書けない仕組みなのではなく、工程で確実に踏まれていない。**

## 4. 要確認の 2 件（注記を入れていない）

`macos / share` と `android / share` は「全部未実施」ではなく、
**特定の項目だけ**実機が要ると書いている。

| 文書 | 残っている項目 |
|---|---|
| `results/share/2026-07-12-macos-share-implementation-feature-result-v1.md` | ピッカー実機確認（`mouseDown` 要件）／`ShareViaService` の定数実行確認 |
| `results/share/2026-06-27-android-share-implement-sample-scene-result-v1.md` | 同一登録からの複数回タップ挙動（per-call 非クリア実装） |

**実機確認が済んでいても、この項目だけ残っている可能性がある。**
スクリーンショットは別の操作のものかもしれない。断定できないため注記は入れていない。
**知っている人に聞くのが確実。**

## 5. dialog に結果文書が無い（別問題）

4 プラットフォームすべてに dialog のスクリーンショットがある
（android 7 / ios 8 / mac 8 / windows 8）が、`artifact/features/<feature>/results/` に
dialog の結果文書が**1 本も無い**。実装が artifact ワークフローより前だった可能性が高い。
**本課題とは別に扱う。**

## 6. なぜこれが効くか

**「未実施」の記録は、後続の判断をまるごと狂わせる。**

`[MonoPInvokeCallback]` は AOT（IL2CPP）でのみ必要な属性で、JIT（Mono）では
無くても動く。記録上 iOS / Android が未実施だったため、
**「この属性は一度も実行されていない」**という結論になり、
Windows を IL2CPP に切り替えてブロック A〜C を全部押し直す、という重い作業が
正当化されかけた。

実際には属性は AOT 上で発火済みで、Windows の IL2CPP 検証は**代理検証**にすぎない。

訂正の反映先:

- `results/clipboard/2026-09-09-windows-clipboard-verify-manual-result-v1.md` の 6 / 7 / 10 節
- `results/clipboard/2026-09-08-windows-clipboard-implementation-feature-result-v6.md` の R-3

## 7. バックエンドは確定した

当初は「Android が Mono か IL2CPP か」を聞き取り事項に挙げていたが、
**Unity のエディタ API に直接聞いて確定した。** 新規プロジェクト（6000.4.2f1）で
`PlayerSettings.GetScriptingBackend` を出力した結果:

```
Android    = IL2CPP
Standalone = Mono2x
iPhone     = IL2CPP
WebGL      = IL2CPP
```

裏付け: 新規プロジェクトの `AndroidTargetArchitectures` は `2`（ARM64 のみ）。
**ARM64 は Mono では作れない**ため、既定が IL2CPP でなければ辻褄が合わない。
なお同梱テンプレート `3d-cross-platform-17.0.14` は `1`（ARMv7）のままで、
**テンプレートを根拠にすると誤る**。

本プロジェクトの `scriptingBackend: Android: 1` は**既定の追認**であり、逸脱ではない。
**したがって `[MonoPInvokeCallback]` は iOS と Android の実機確認で AOT 上を通っている。**

## 8. 埋めるのに要る情報

Windows の実機確認結果 v1 と同じ粒度で書くなら、機能ごとに:

1. 実施日
2. 端末と OS バージョン
3. ~~スクリプティングバックエンド~~ **7 節で解決済み。聞き取り不要**
4. 消化した項目（各計画の手動確認項目を全部か、一部か）
5. 失敗・未実施が残っているか

**分からない項目は「記録なし」と書く。推測で埋めない。**

## 9. 案

| # | 内容 | コスト |
|---|---|---|
| 1 | 情報を持つ人から聞き取り、Windows と同じ形の結果文書を起こす | 聞き取り + 執筆 |
| 2 | 実機で再実施して記録を取り直す | 実機 3 種 + 手動確認 |
| 3 | 注記のみで運用する（現状） | 済み。**ただし本文は誤ったまま** |

**1 を推奨。** 2 は確実だが、既に通った確認をもう一度やることになる。

## 10. 再発防止（未着手）

証跡と記述の突き合わせは**機械的にできる**。今回は手で行った。

- `manual/*/images/<platform>/<feature>/Example_*` の有無
- 各機能の最新結果文書に「実機 … 未実施 / 環境が無い」の記述があるか

両方成立していれば、ずれの候補。`scripts/` に置けば
`review-implementation-*` の工程で毎回踏める。**入れるかは未決定。**

## 11. 参照

- 注記を入れた 9 文書（1 節の表）
- 実機で撮られた画像: `manual/1.10.0/images/{android,ios,mac,windows}/`
- Windows 側の訂正: `results/clipboard/2026-09-09-windows-clipboard-verify-manual-result-v1.md`
