# 実機確認は済んでいるのに、結果文書が「未実施」のまま

- 記録日: 2026-09-10
- 分類: 横断課題（**4 機能 × 4 プラットフォームのうち 9 文書**。当初は clipboard だけと見ていた）
- 発見経緯: Windows の M-22（IL2CPP 再実施）の必要性を検討する過程で、
  「`[MonoPInvokeCallback]` は一度も実行されていない」と誤って判断した。
  実際には iOS / Android で実機確認が済んでおり、**記録だけが古かった**
- 対応方針: **別タスク。** 実施日・端末・消化項目を知る人からの情報が要る
- 進捗: 該当 9 文書に訂正の注記を入れただけ。**詳細記録は未作成**

---

## 何が起きているか

iOS / Android の clipboard は実機確認が済んでいる。しかし**結果文書は
「実機環境がないため未実施」と書いたままになっている。**

| 文書 | 本文の記述 |
|---|---|
| `2026-08-15-ios-clipboard-implementation-feature-result-v3.md` | `M-1〜M-24（実機手動確認）｜実機 iOS 18 以降が必要。未実施` |
| `2026-08-22-ios-clipboard-implement-sample-scene-result-v3.md` | S-10〜S-32 はすべて未実施 |
| `2026-07-26-android-clipboard-implementation-feature-result-v3.md` | `実機での手動確認（計画 7.3、全18項目）: 実機環境がないため未実施` |
| `2026-07-26-android-clipboard-implement-sample-scene-result-v2.md` | 5.2 実機確認（未実施） |

2026-09-10 に、この 4 文書の冒頭へ訂正の注記を入れた。**本文は直していない。**
実施日・端末・OS バージョン・消化した項目・残った失敗が分からないため、
埋めると別の種類の誤りになる。

## 実施済みである根拠

`manual/1.10.0/images/` に**実機でしか撮れないスクリーンショット**がある。

| プラットフォーム | clipboard のスクリーンショット |
|---|---|
| Android | `Example_AndroidClipboardManager_*.png` 11 枚以上 |
| iOS | `Example_IosClipboardManager_*.png` |
| Windows | **0 枚**（Windows は本文どおり 2026-09-09 に実施） |

## なぜこれが効くか

**「未実施」の記録は、後続の判断をまるごと狂わせる。**

`[MonoPInvokeCallback]` は AOT（IL2CPP）でのみ必要な属性で、JIT（Mono）では
無くても動く。Windows / macOS は Mono、iOS / Android は IL2CPP。
記録上 iOS / Android が未実施だったため、**「この属性は一度も実行されていない」**
という結論になり、Windows を IL2CPP に切り替えてブロック A〜C を全部押し直す、
という重い作業が正当化されかけた。

実際には属性は AOT 上で発火済みで、Windows の IL2CPP 検証は**代理検証**にすぎない。
（訂正は `2026-09-09-windows-clipboard-verify-manual-result-v1.md` の 6 節 / 7 節、
`2026-09-08-windows-clipboard-implementation-feature-result-v6.md` の R-3）

## 埋めるのに要る情報

Windows の実機確認結果 v1 と同じ粒度で書くなら、iOS / Android それぞれについて:

1. 実施日
2. 端末と OS バージョン
3. ~~スクリプティングバックエンド~~ **解決済み（下記「バックエンドは確定した」）。聞き取り不要**
4. 消化した項目（計画の M-1〜M-24 / 7.3 の 18 項目を全部か、一部か）
5. 失敗・未実施が残っているか

**分からない項目は「記録なし」と書く。** 推測で埋めない。

## バックエンドは確定した（2026-09-10）

当初は「Android が Mono か IL2CPP か」を聞き取り事項に挙げていたが、
**Unity のエディタ API に直接聞いて確定した。** 新規プロジェクト（6000.4.2f1）で
`PlayerSettings.GetScriptingBackend` を全プラットフォーム分そのまま出力した結果:

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
Android が Mono だった可能性は消えた。

## 案

| # | 内容 | コスト |
|---|---|---|
| 1 | 情報を持つ人から聞き取り、Windows と同じ形の `verify-manual-result` を起こす | 聞き取り + 執筆 |
| 2 | 実機で再実施して記録を取り直す | 実機 2 台 + 手動確認 |
| 3 | 注記のみで運用する（現状） | 済み。**ただし本文は誤ったまま** |

**1 を推奨。** 2 は確実だが、既に通った確認をもう一度やることになる。

## 洗い出し結果（2026-09-10）

`manual/1.10.0/images/<platform>/<feature>/` の `Example_*` スクリーンショット
（**サンプル画面を実機で撮ったもの**）の有無と、各機能の最新結果文書の記述を
突き合わせた。

| プラットフォーム / 機能 | 実機の証跡 | 文書の記述 | 判定 |
|---|---|---|---|
| android / clipboard | 15 枚 | まるごと未実施 | **ずれ。注記済み** |
| ios / clipboard | 23 枚 | まるごと未実施 | **ずれ。注記済み** |
| ios / share | 10 枚 | まるごと未実施 | **ずれ。注記済み** |
| macos / notification | 8 枚 | まるごと未実施 | **ずれ。注記済み** |
| windows / notification | 4 枚 | まるごと未実施 | **ずれ。注記済み** |
| macos / clipboard | 8 枚 | **実機実施と明記** | 正しい |
| windows / clipboard | 0 枚 | 実機実施と明記（2026-09-09） | 正しい（画像は未撮影） |
| macos / share | 9 枚 | **個別 2 項目のみ**「実機が必要」 | **要確認** |
| android / share | 12 枚 | **個別 1 項目のみ**「実機検証が必要」 | **要確認** |
| 全 4 プラットフォーム / dialog | 7 〜 8 枚 | **結果文書が存在しない** | 別問題 |

**まるごと未実施と書いていた 5 機能 9 文書**に訂正の注記を入れた。本文は直していない。

### 要確認の 2 件

`macos-share` と `android-share` は「全部未実施」ではなく、**特定の項目だけ**
実機が要ると書いている（ピッカーの `mouseDown` 要件、同一登録への複数回タップ）。
実機確認が済んでいても、**その項目だけ残っている可能性がある。**
注記は入れていない。**知っている人に聞くのが確実。**

### dialog に結果文書が無い

4 プラットフォームすべてに dialog のスクリーンショットがあるが、
`artifact/results/` に dialog の結果文書が 1 本も無い。
実装が artifact ワークフローより前だった可能性が高い。**別問題として扱う。**

## 参照

- 訂正を入れた 4 文書（上表）
- Windows 側の訂正: `2026-09-09-windows-clipboard-verify-manual-result-v1.md` 6 / 7 節
- 実機で撮られた画像: `manual/1.10.0/images/{android,ios}/clipboard/`
