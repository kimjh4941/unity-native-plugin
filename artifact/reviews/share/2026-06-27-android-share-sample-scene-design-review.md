# レビュー結果

- 日付: 2026-06-27
- 対象ファイル: `/Users/jonghyunkim/Desktop/unity-native-plugin/artifact/designs/share/2026-06-27-android-share-sample-scene-design-v2.md`
- 機能名: share
- 対象 OS: Android

---

## 強み

- v1 からの差分が Custom Chooser Action デモ追加に絞られており、既存サンプル構成を崩さない方針が明確。
- native-toolkit サンプルアプリとの差分（Unity 版は manifest receiver ではなく AAR 内 dynamic receiver）を明記しており、実装時の混同を避けやすい。
- `ResultTextBlock` / `CallbackTextBlock` / `ChooserActionTextBlock` を分け、global event と per-call callback の発火経路を UI 上で識別する方針は良い。
- API 34+ 限定、旧版 AAR degrade、AAR 差し替え依存を手動確認観点に含めている。
- 既存 `NativeToolkitSampleNavigator` / TopMenu / Notification サンプル構成を踏襲する方針が整理されている。

## 改善点

### 高優先度

1. セクション 0 / 1.1 / 1.4
   - 問題: 本設計は chooser action callback 実装を「実装完了」として扱っているが、`artifact/reviews/share/2026-06-27-android-share-implementation-feature-review-v1.md` では `ShareChooserActionCallbackCoordinator.Fire` の例外継続契約に重大な実装バグがあり、総合評価は「要修正（重大）」になっている。
   - 影響: サンプルシーン実装へ進むと、`ShareChooserActionTapped` と per-call `onChooserAction` の両発火を確認するデモが、未修正の中核 callback 実装に依存する。レビュー済みの未解決不具合をサンプル側で既成事実化してしまう。
   - 提案: 前提に「implementation-feature-review-v1 の high 指摘修正後に実装する」を追加し、DoD/手動確認にも「機能実装レビューの重大指摘が解消済み」を入れる。未修正のまま進めるなら、サンプルは Custom Action デモを一時無効化する。

2. セクション 5.2 / 6.2
   - 問題: `OnDisable` 時に per-call `onChooserAction` を明示クリアしない方針になっている。`ShareText(..., onChooserAction: OnCustomChooserActionTapped)` で Manager に controller インスタンスメソッドを保持させるため、画面離脱後に Sharesheet 上の custom action がタップされると、無効化/破棄済み controller の callback が呼ばれ得る。
   - 影響: `ChooserActionTextBlock` が null/破棄済みの状態で更新される、古い controller 参照が残る、次回 Share まで stale callback が保持される、というライフサイクル不具合につながる。
   - 提案: サンプルで per-call callback を使うなら、機能側に `ClearShareChooserActionCallback` 相当の C# API を追加して `OnDisable` で呼ぶ設計にする。機能 API を増やさないなら、サンプルは per-call callback を渡さず `ShareChooserActionTapped` の global event のみで表示するか、callback 内で `isActiveAndEnabled` と label null を確認するだけでなく stale 参照が残るリスクを明記する。

### 中優先度

1. セクション 4.1 / 5.3
   - 問題: 「`.meta` ファイルは Unity が自動生成するため記載しない」「`.meta` は作成しない」とあるが、既存 package の UXML/USS/C# は `.meta` とセットで管理されている。直近の機能実装レビューでも新規 `.cs` の `.meta` 不足が指摘されている。
   - 影響: Unity が後から生成した GUID が環境依存になり、package 配布や asset 参照が不安定になる可能性がある。
   - 提案: 変更ファイル一覧に新規 C# / UXML / USS / folder の `.meta` を含める。手動作成ではなく Unity 生成後にコミットする、という表現にする。

2. セクション 6.2
   - 問題: `OnEnable` で Manager event 購読、`Start` で UI 初期化の順にしているが、Unity ライフサイクル上は通常 `OnEnable` が `Start` より先に走る。Manager 初期化時や保留 callback のタイミング次第で、label 初期化前にイベントが届く可能性がある。
   - 影響: `_resultLabel` / `_callbackLabel` / `_chooserActionLabel` が null のまま event handler が動くと、結果表示が落ちるか欠落する。
   - 提案: event handler は label null 安全にする、または `Awake`/`OnEnable` 前に UI を確実に取得できる構成へ寄せる。最低限、`OnEnable` 時に UI 未初期化なら購読だけして handler は null 安全に表示を遅延/破棄する方針を記載する。

3. セクション 6.2 / 7
   - 問題: Custom Action は「1〜2 件」と書かれているが、手動確認観点は単一 ActionId の両経路表示のみ。複数 action の識別確認がない。
   - 影響: `intentAction` ごとに異なる `ActionId` が届くこと、複数ボタン表示時の UI 表示が壊れないことを確認できない。
   - 提案: 実装は 2 件に固定し、`SampleChooserActionIdSave` / `SampleChooserActionIdOpen` など別 action を使う。手動確認に「各 action で異なる ActionId が表示される」を追加する。

4. セクション 7
   - 問題: サンプルシーンの PlayMode/EditMode 自動テスト方針がない。workflow はサンプル実装でも画面導線・ボタン名・Controller 接続を確認する観点を求めているが、すべて手動確認になっている。
   - 影響: UXML の button name と Controller の `root.Q<Button>` 名ズレ、TopMenu 導線漏れ、Resources path 間違いが実装後まで検出されにくい。
   - 提案: 少なくとも UXML/USS の Resources path、必須 element name、TopMenu button name、Navigator の `ShowAndroidShare` path をチェックする軽量 EditMode テスト、または実装レビュー時の静的確認チェックリストを追加する。

5. セクション 7
   - 問題: API 34+ / API 33 以下 / 旧版 AAR の確認はあるが、feature 実装レビューで指摘された「新規 `.meta`」「payload コメント更新」などの前提修正完了確認がない。
   - 提案: サンプル実装前提チェックとして「implementation-feature-review-v1 の high/medium が解消済み」「Unity Test Runner が通過済み」を追加する。

### 低優先度

1. セクション 2.1 / 5.1
   - 問題: 既存 Notification Controller は `Start`/`OnDestroy` 購読だが、本設計は workflow に合わせて `OnEnable`/`OnDisable` へ変える。理由は書かれているが、既存 controller との差分が実装時に迷いやすい。
   - 提案: `OnEnable`/`OnDisable` に寄せる対象は Manager events のみ、button clicked は `Start`/`OnDestroy` と明記している点をさらにタスク分解にも反映する。

2. セクション 3.3
   - 問題: UI 文言は英語方針だが、例示の一部が省略形 `Android device only. …` のまま。
   - 提案: 実装詳細に実際の UI 文言を完全な英語で列挙すると、実装者が迷わない。

## 不足項目

- Definition of Done がない。サンプルシーン設計として、TopMenu 導線、Resources load、各 button click、event 購読解除、chooser action の global/per-call 表示、API 33 以下、旧版 AAR、`.meta` 生成/コミット、実装レビュー high 解消済みを DoD に含めるべき。
- 実装タスク分解がない。UXML/USS、Controller、TopMenu、Navigator、手動確認、`.meta` 確認をタスク単位に分けると実装しやすい。
- `ChooserActionTextBlock` の初期表示/クリア条件がない。`ShareCustomActionButton` 押下時に前回の action 表示を消すのか、履歴として残すのかを明記するべき。
- 画面離脱後に Sharesheet が残っている場合の chooser action callback の扱いが未定義。
- 複数 custom action を本当に実装するか、単一 action に絞るかの決定が曖昧。

## 総合評価

Custom Chooser Action デモを追加する方向性は妥当で、UI 表示の分離も良い。ただし、前提にしている機能実装がまだ「要修正（重大）」であり、さらに sample controller の per-call callback が画面離脱後も Manager に残り得る設計になっている。ここはサンプル実装に入る前に直した方がよい。特に per-call callback を安全に解除できないなら、サンプルでは global event のみを使う判断も現実的。
