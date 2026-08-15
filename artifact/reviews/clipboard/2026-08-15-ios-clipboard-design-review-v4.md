# レビュー結果

- 日付: 2026-08-15
- 対象ファイル: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v4.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- Manager lifetime を tombstone で一方向に終了させ、native ABI に識別子がない条件でも旧 callback と新規操作が共存しない構造にした。
- 全 native callback の入口で terminated を確認し、JSON parse や pending slot 操作より先に後着結果を破棄する契約が明確である。
- B-9 専用の off-thread rejection を分離し、common event とログを main thread 上で評価するよう改善した。
- base64 decoded length は padding を考慮した exact calculation に修正され、64MiB 境界の検算とテストが追加された。
- observation control の pending generation の保持・解放順が具体化された。
- tombstone、B-11、後着 callback、再取得を組み合わせた複合テストを定義している。

## 改善点

### 高優先度

#### 5.6.1 `OnDestroy` の tombstone 設定順序と例外安全性

- 問題点: 擬似コードは `clipboardStopObserving(null)` と `clipboardCancelLoads(null)` を呼んだ後で `s_isTerminated = true` を設定する。いずれかの P/Invoke が `DllNotFoundException`、`EntryPointNotFoundException` 等を投げると、tombstone、pending clear、`_instance = null` に到達しない。終了処理が部分適用となり、再生成拒否と後着 callback 破棄という中心的な安全保証が成立しない。
- severity: high
- 改善提案: `_instance == this` を確認した直後、native cleanup より先に `s_isTerminated = true` を設定する。managed cleanup と `_instance = null` は `finally` で必ず実行する。native の Stop／Cancel はそれぞれ独立した try/catch にし、一方の失敗で他方を省略せず、例外を Unity lifecycle callback の外へ出さない。順序と各 cleanup 例外のテスト seam を追加する。

### 中優先度

#### 5.6.1 / 7.2 PlayMode テスト間の tombstone 分離

- 問題点: tombstone は同一 Play Mode session 中に永続するため、Manager を破棄するテストを1件実行すると、その後の integration tests はすべて B-11 になる。テスト順序への依存が生じる。また「Play Mode終了でstaticが自然に初期化される」は domain reload 無効設定では成立しない。
- severity: medium
- 改善提案: production の同一 lifetime では解除不能という契約を維持したまま、Editor test assembly からのみ使える明示的な reset seam、または破棄系テストを独立 Player run に隔離する方針を定義する。`RuntimeInitializeOnLoadMethod(SubsystemRegistration)` で新しい Play session 開始時に static state を初期化すれば、domain reload 無効時にも対応できる。旧 callback が残る実機 runtime では同一 session 中に reset しない。

#### 3.3 / 5.8.1 `Instance` getter の thread 契約

- 問題点: B-9 は instance method の先頭で判定されるが、一般的な呼び方 `IosClipboardManager.Instance.Read(...)` では先に `Instance` getter が評価される。未生成状態で background thread から呼ぶと getter が GameObject を作成し、B-9 に到達する前に Unity API の thread 制約へ違反する。
- severity: medium
- 改善提案: `Instance` getter 自体も Unity main thread 限定であることを public XML 契約へ明記する。B-9 が安全に返せるのは既存 instance 参照を background thread から呼んだ場合だけであることを明確にするか、static entry point を別途設けない限り「全 off-thread 呼び出しを B-9 で拒否」とは断定しない。

### 低優先度

#### 7.2 tombstone 無効化の比較実装

- 問題点: 「tombstone を無効化した比較実装」が production Manager に切替可能な escape hatch として入ると、禁止した lifecycle を再び有効化できてしまう。
- severity: low
- 改善提案: 誤配送の再現はテスト内の純粋な状態モデルまたは test-only helper で行い、production code に tombstone 無効化フラグを追加しないことを明記する。本実装には正の安全性テストだけを適用すればよい。

## 不足項目

- native cleanup が例外を投げても tombstone と managed cleanup が必ず完了する `OnDestroy` 契約。
- 同一 Play Mode session のテスト間で tombstone を安全に分離する方法。
- `Instance` getter を含む正確な main-thread API 契約。
- tombstone 比較試験を production code から隔離する規則。

## 総合評価

v3 の指摘はすべて実質的に反映され、通常経路・rejected 経路・lifetime 境界の設計はかなり完成度が高い。ただし tombstone が native cleanup より後に設定される現在の `OnDestroy` は例外時に中心的保証を失うため、実装前に順序と `finally` 契約を修正する必要がある。テスト分離と `Instance` getter の thread 契約も補完すれば実装へ進める。総合評価は「要修正（高優先度 1 件）」とする。
