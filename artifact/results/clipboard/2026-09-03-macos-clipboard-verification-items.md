# macOS Clipboard 要検証事項の棚卸し（V-1 〜 V-13）

- 日付: 2026-09-03
- 対象: `artifact/designs/clipboard/2026-09-03-macos-clipboard-design-v11.md` 8 章
- 目的: 計画書 10 章「段 3 完了時の追加条件」の **「V-1 〜 V-13 のすべてに結論または継続課題としての記載がある」** を満たす
- 実装状況: 段 1 〜 3 完了（EditMode 517 / PlayMode 116、いずれも失敗 0）

## 判定の定義

| 判定 | 意味 |
| --- | --- |
| **クローズ** | 結論が出ており、これ以上の作業は不要（前提が変わらない限り） |
| **部分クローズ** | 自動テストで確認できる範囲は終わり、実機分だけが残っている |
| **継続課題** | 実機・実測が必要で未着手。**本ドキュメントが継続課題としての記載である** |

**実機作業（7.5 の手動確認 32 項目）は未実施である。** そのため多くが継続課題として残る。これは想定どおりで、計画書 8 章の前文が「実機・実測が必要なものだけを残した」と述べているとおりである。

## 一覧

| # | 項目 | 判定 | 根拠と残作業 |
| --- | --- | --- | --- |
| V-1 | xcframework とソースの版対応 | **クローズ** | 2026-09-03 に実測。ビルド時刻（13:51）が最終 clipboard コミット（12:05）より後、未コミットの Swift 変更なし、バイナリ内に想定文字列 15 種（エラーメッセージ 5・JSON キー 7・複数形パターン名 3）を確認。詳細は計画書 0.2。**ネイティブを再ビルドしたら再確認する** |
| V-2 | 文字列マーシャリング | **継続課題** | C# 側は確認済み: Builder が非 ASCII を `\uXXXX` にせず生の UTF-8 で出力することを `MacClipboardJsonBuilderTests`（日本語・絵文字）で、Reader がサロゲートペアを往復することを `MacClipboardJsonReaderTests` で検証済み。**未確認なのは P/Invoke 境界そのもの。** Editor では P/Invoke がコンパイルされないため原理的にここでは検証できない。7.5 の実機確認で埋める |
| V-3 | サイズ上限（32 MiB × 2） | **継続課題** | 暫定値のまま。実装は上限を `MacClipboardLimits` の定数 1 箇所に集約し、`MaxRequestBytesOverrideForTests` で差し替えられるようにしてあるので、**実測値が出たら定数の変更だけで済む**。所要時間・ピークメモリの実測が必要 |
| V-4 | コールバックのスレッド | **継続課題** | 実機のみ（7.5 #28）。ただし**実装はこの前提に依存していない**: ネイティブがどのスレッドで呼んでも結果は `UnityMainThreadDispatcher.Enqueue` を通るため、V-4 が偽でも配送は壊れない。壊れるのは `s_inFlight` と per-call スロットのロックなし前提の方なので、そこを確認する |
| V-5 | App Sandbox | **継続課題** | 実機のみ。サンドボックス有効時に named / unique pasteboard を作成・解放できるか |
| V-6 | `NSInteger` / `BOOL` のマーシャリング実挙動 | **継続課題** | 宣言は D-2 のとおり実装済み（`long` + `[MarshalAs(UnmanagedType.I1)]`、3 delegate 型すべて）。**検証対象は宣言ではなく arm64 / x86_64 の IL2CPP ビルドでの実挙動**であり、実機のみ |
| V-7 | `localOnly` の効果 | **継続課題** | 実機のみ。XML コメントへの引き写しは段 2 で完了済み（`Copy` の `options` に「Universal Clipboard への効果は実機未検証」と明記） |
| V-8 | 監視のアクティブ／非アクティブ挙動 | **継続課題** | 実機のみ。バックグラウンド移行時のポーリング停止と復帰 |
| V-9 | `detectValues` の許可 UI | **継続課題** | 実機のみ。通知の有無・条件・1514 の再現性 |
| V-10 | lazy data provider の実挙動 | **継続課題** | 実機のみ（7.5 #21）。「10 MiB 超の単一 item を Copy 後に Player 終了 → 貼り付け不可」が実際に起きるか。**この挙動は段 2 で `Copy` の XML コメントに引き写し済み**なので、実測で否定されたらコメントの方を直す |
| V-11 | Domain Reload / Scene Reload 双方無効の構成 | **継続課題（ただし現構成では未発動）** | 下記参照 |
| V-12 | single-flight の粒度 | **継続課題** | 実運用での実測が必要。**自動テストで確認済みなのは「異なる操作は互いに干渉しない」ことだけ**（`EveryOperation_UsesItsOwnSingleFlightKey` が 13 操作で別キーであることを確認）。読み取り系の連打で 9001 が頻発するかは実機と実アプリでしか分からない。緩和は非破壊的変更なので後追いで足せる |
| V-13 | 世代カウンタ撤去の妥当性 | **部分クローズ** | 下記参照 |

## V-11 の現状（実測）

`ProjectSettings/EditorSettings.asset` を確認した。

```
m_EnterPlayModeOptionsEnabled: 1
m_EnterPlayModeOptions: 0
```

`EnterPlayModeOptions` は `None = 0` / `DisableDomainReload = 1` / `DisableSceneReload = 2` のビットフラグである。**現在の値は 0 で、どちらも無効化されていない。** つまり V-11 が想定する構成は本プロジェクトでは発動していない。

**残る懸念は利用者側が双方を無効にした場合である。** コード上の懸念点は次のとおりで、これは分析であって実測ではない。

- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` の `ResetCore()` が `s_dispatcher` と `s_mainThreadId` を毎回 null / 0 に戻す
- 再捕捉するのは `Awake` だけ
- したがって「`ResetCore` は走るが `Awake` は走らない」構成があれば、dispatch 経路が死ぬ

ただし **Play モード終了時には runtime に生成されたオブジェクトは `DontDestroyOnLoad` のものも含めて破棄される**ため、次回の Play で `Instance` に触れた時点で GameObject が作り直され `Awake` が走る、という筋道も成り立つ。**どちらが正しいかは設定を変えて実測しないと確定できない。**

プロジェクト設定の変更は本作業の範囲外と判断して行っていない。**`m_EnterPlayModeOptions` を 3 にして PlayMode テストを流すだけで確認できる**ので、判断は利用者に委ねる。

## V-13 の現状（部分クローズ）

計画書は「7.2 と 7.5 #15〜#18 で確認する」としている。**7.2 の分は完了した。**

段 3b で実装した active / pending の 2 スロットに対し、**世代カウンタが守っていた不変条件が保たれることを変異テストで確認した。** 実装に既知の欠陥を 4 つ同時に注入し、落ちたのはちょうど対応する 4 テストだけだった。

| 注入した欠陥 | 検出したテスト |
| --- | --- |
| D-16 の再発行を削除 | `LateSuccessfulStart_AfterTeardown_ReissuesTheStop` |
| 段階 7 が pending ではなく active に書く | `PendingRestart_DoesNotDivertEventsFromTheActiveRegistration` |
| 失敗した start が active を破棄する | `FailedRestart_KeepsTheRegistrationItAlreadyHad` |
| 失敗した stop が active を破棄する | `FailedStop_KeepsTheRegistrationBecauseNativeIsStillObserving` |

Codex のレビュー（review-v8）も、Swift 正本を読んだうえで「世代カウンタが必要としていた『複数の observation control callback の所有者識別』が欠落する問題はない。共有 single-flight キーにより managed 上の未完了制御呼び出しは最大 1 件である」と結論している。

**残るのは 7.5 #15〜#18 の実機連続操作のみ。** iOS 版が世代カウンタを残しているのに対し macOS 版が撤去した差は意図的な分岐であり、実機で崩れないことが確認できた時点で完全クローズとする。

## まとめ

| 判定 | 件数 | 内訳 |
| --- | ---: | --- |
| クローズ | 1 | V-1 |
| 部分クローズ | 1 | V-13 |
| 継続課題 | 11 | V-2 〜 V-12 |

**11 件が実機待ちであることは想定どおりである。** 計画書 8 章は設計判断をすべて 9 章に移したうえで「実機・実測が必要なものだけを残した」と宣言しており、自動テストで閉じられるものは既に閉じている。

次の作業は **7.5 の手動確認 32 項目**で、V-2 / V-3 / V-4 / V-5 / V-6 / V-7 / V-8 / V-9 / V-10 / V-13 の 10 件がそこで進む。V-11 は設定変更 1 行、V-12 は実アプリでの利用が必要で、いずれも 7.5 とは別の作業になる。
