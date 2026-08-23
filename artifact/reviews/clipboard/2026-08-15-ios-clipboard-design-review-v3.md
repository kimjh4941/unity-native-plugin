# レビュー結果

- 日付: 2026-08-15
- 対象ファイル: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v3.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- v2 の busy 経路問題を `DispatchRejectedResult` で分離し、pending callback と in-flight marker を所有する前後の境界を7段階で明確にした。
- `Awaitable` を初期実装から外し、追加に必要な lifecycle coordinator とテストを将来要件として切り出した判断は妥当である。
- main-thread 契約、B-9、captured dispatcher を導入し、`UnityMainThreadDispatcher.Instance` を callback dispatch 中に評価しない構造へ改善した。
- base64 の宣言 `byteCount` を信用せず、割り当て前上限確認とデコード後の一致確認を分離した。
- Start/Stop が同じ native subscription を変更することを shared single-flight key で表現した。
- rejected 経路の4不変条件と、それを破るケースを直接再現するテスト方針が明確である。

## 改善点

### 高優先度

#### 5.6.1 / 5.6.5 Manager 再生成後に到着する旧 native callback

- 問題点: `OnDestroy` は static pending slots と `s_inFlight` をクリアし、直後の `Instance` 再取得と新規操作を許している。旧 Manager の native 呼び出しは多くがキャンセル不能なので、その callback が新 Manager の同一操作開始後に到着すると、共通の static callback が新しい pending slot を取得し、新呼び出しの callback と in-flight marker を奪う。現テストは「Destroy後の後着」と「Destroy後の再取得」を個別に確認するだけで、この組み合わせによる誤配送を検出しない。observation generation も callback ABI に generation/context がないため、この lifetime 境界を単独では識別できない。
- severity: high
- 改善提案: 最も単純なのは、アプリ実行中の明示的な Manager 破棄・再生成を非対応とし、`OnDestroy` 後は新規 native 操作を拒否する lifetime 契約にすること。再生成を必須にするなら、旧 callback を全て受信するまで同一操作を再開不可にする tombstone 状態、または native ABI への request/lifetime ID 追加が必要である。「旧 A 開始 → Destroy → 新 Manager → 新 A 開始 → 旧 A callback → 新 A callback」のテストを追加する。

### 中優先度

#### 5.8.1 main-thread 違反の rejected 経路

- 問題点: background thread を安全に拒否する方針だが、`DispatchRejectedResult` の呼び出し前に `_instance?.ReadCompleted` 等の event delegate を取得し、例では `Debug.LogWarning` も直接呼んでいる。文書自身が `Debug.Log` と Manager state を main-thread 限定としているため、main-thread 検証に失敗した経路がその前提を破っている。event の購読解除と off-thread snapshot が競合する可能性もある。
- severity: medium
- 改善提案: B-9 専用経路では off-thread で触れてよいものを `s_dispatcher` と拒否呼び出し自身の callback に限定する。dispatcher へ enqueue した closure 内で common event を snapshot し、ログも main thread 上で行う。main-thread検証を各 public method の通常ログより前に置くことを明記する。

#### 5.5.1 / 5.9.2 base64 の上限境界

- 問題点: `(base64文字数 / 4) * 3` は padding を考慮しない最大値であり、raw size がちょうど64MiBの場合は最大値が64MiBを1〜2 byte超える。そのため合法な上限値を E-14 で誤拒否しうる。
- severity: medium
- 改善提案: canonical base64 の長さと末尾 padding（`=` / `==`）から正確な decoded length を overflow-safe に算出してから比較する。不正な長さ・padding は B-8、`limit - 1`／`limit`／`limit + 1` は境界テストで固定する。

#### 5.6.4 observation generation の callback への受け渡し

- 問題点: Start入口のローカル `generation` と Stop入口の `generationAtStopCall` を使う規則はあるが、contextを持たない static `OnStartObservingResult` / `OnStopObservingResult` がどの値を参照するかが定義されていない。`ReleaseChangeRegistrationIfOwned()` の引数・保持場所も示されておらず、実装者によって世代判定が変わりうる。
- severity: medium
- 改善提案: shared observation key により pending control call が1件だけであることを利用し、`s_pendingObservationOperation` と `s_pendingObservationGeneration` の設定・取得・クリア順を明示する。ただし Manager lifetime をまたぐ旧 callback 問題はこの世代値だけでは解決できないため、高優先度項目と分けて扱う。

### 低優先度

#### 5.6.1 static dispatcher の破棄時状態

- 問題点: `s_dispatcher` を `Awake` で設定する一方、`OnDestroy` でどう扱うかが未記載である。dispatcher 自体は `DontDestroyOnLoad` だが、domain reload／test teardown時の古い参照について契約がない。
- severity: low
- 改善提案: dispatcher の所有者は `UnityMainThreadDispatcher` であり Manager は非所有参照を保持すること、Manager再生成時に必ず再キャプチャすること、参照先が破棄済みなら dispatchを行わないことを明記する。

## 不足項目

- Manager lifetime をまたいで後着する native callback と新規操作を分離する契約。
- B-9 rejected 経路で main-thread 到達前に触れてよい state の限定。
- padding を考慮した正確で overflow-safe な base64 decoded-length 計算。
- observation callback が参照する pending generation の具体的な保持・解放方法。

## 総合評価

v2 レビューの指摘は適切に反映され、単一 Manager lifetime 内の single-flight／rejected 契約は実装可能な水準まで具体化された。一方、`Destroy → 再生成 → 旧 callback 後着` では新しい pending call への誤配送が残るため、再生成を許す現在の lifecycle 契約は実装前に修正が必要である。B-9 の完全な off-thread 安全性、base64 上限境界、observation generation の保持方法を補完すれば実装へ進める。総合評価は「要修正（高優先度 1 件）」とする。
