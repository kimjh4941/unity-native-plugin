# レビュー結果

- 日付: 2026-08-15
- 対象ファイル: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- `OnDestroy` は tombstone を最初に設定し、Stop／Cancelを独立した例外境界で実行し、managed cleanupを `finally` で保証する構造になった。
- `SubsystemRegistration` とEditor限定resetにより、domain reload設定とPlayModeテスト順序への依存を除いている。
- `Instance` getterとinstance methodのthread契約を分離し、B-9が保証できる範囲を正確に限定した。
- production codeからtombstoneを無効化する案を撤回し、lifetime安全性を構造的に維持している。
- v1〜v4で扱ったABI、single-flight、rejected dispatch、malformed JSON、memory、observation、lifetimeの各契約がDoDとテストへ追跡可能に反映されている。

## 改善点

### 高優先度

- なし。

### 中優先度

#### 5.6.1 / 7.2 native cleanup の例外注入 seam

- 問題点: `OnDestroy` の擬似コードは直接 P/Invoke を呼ぶ一方、テスト方針では `internal static Action?` フック2個へ切り出すとしており、production／Editorそれぞれでどの呼び出しが実行されるか未確定である。mutable hookをplayer buildへ残すと、同一assembly内のコードがcleanupを差し替えたりnull化できる余地も生まれる。
- severity: medium
- 改善提案: mutableなproduction hookではなく、`internal static void RunDestroyCleanup(Action stop, Action cancel, Action managedCleanup)` のような例外分離helperを作り、device `OnDestroy` は実native actionを渡し、Editorテストはthrowing actionを直接渡す。hook方式を採るなら `#if UNITY_EDITOR` に限定し、`ResetForTests` がhook自体も必ず初期化する契約を明記する。

### 低優先度

#### 5.6.1 `s_dispatcher` 表のdomain reload記述

- 問題点: 新しい節では `SubsystemRegistration` による明示resetを正しく定義した一方、`s_dispatcher` の表には「domain reload / Play Mode終了でreset」と旧説明が残っている。Reload Domain無効時の説明と矛盾する。
- severity: low
- 改善提案: 「`SubsystemRegistration` でPlay session開始時にresetし、`Awake`で再キャプチャする」に統一する。Reload DomainとScene Reloadを両方無効化する構成をサポート範囲に含める場合は、既存Managerの`Awake`が再実行されないケースも別途確認する。

#### 5.6.1 `ResetForTests` のreset範囲

- 問題点: 例では tombstone とpending stateだけをresetするが、cleanup injection seamやobservation generationを含む全test-mutated static stateを確実に戻すことが一覧化されていない。
- severity: low
- 改善提案: `ResetForTests` と `ResetStaticState` が同じ内部reset coreを共有し、`_instance`を除く全static mutable stateとEditor-only hookを初期値へ戻すことをテストで固定する。

## 不足項目

- native cleanup例外注入seamのproduction-safeな具体形。
- `SubsystemRegistration`を正本としたdispatcher reset説明への統一。
- static reset対象の完全な一覧とreset後不変条件。

## 総合評価

これまでの高優先度指摘はすべて解消され、native API、ABI、Manager + Bridge、エラー、thread／memory、IL2CPP、テスト層、lifetimeの各契約は実装可能な水準に達している。残る指摘は主にテストseamの具体化であり、機能設計を変更するものではない。productionへmutable cleanup hookを残さない形に整理することを条件に、実装へ進めてよい。総合評価は「承認（軽微な実装前整理あり）」とする。
