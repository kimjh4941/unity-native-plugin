# レビュー結果

- 日付: 2026-09-05
- 対象: `feature/UNT-10` / commit `06449fa`
- サンプルシーン計画: `artifact/designs/clipboard/2026-09-05-macos-clipboard-sample-scene-design-v3.md`
- 実装結果: `artifact/results/clipboard/2026-09-05-macos-clipboard-implement-sample-scene-result-v2.md`
- 機能名: clipboard / プラットフォーム: macOS / 種別: サンプルシーン実装
- **レビュー実施: Codex（別モデル）。2 巡目**
- 前回レビュー: `artifact/reviews/clipboard/2026-09-05-macos-clipboard-implement-sample-scene-review-v1.md`（Claude サブエージェント。A 区分 0 / medium 5 / B 1 / low 7）
- 所要: 7 分 16 秒
- コード・ドキュメントの変更なし（読み取り専用）
- Codex session ID: `01a06f5c-3e9b-7d62-ab40-7b09be8bd90e`

## 本レビューの位置づけ

ワークフローの止める基準は、A 区分 0 に加えて **「レビュアーを替えて 1 回通していること」** を求めている。1 巡目は Claude のサブエージェント、本巡は Codex で、その条件を満たすためのラウンドである。

**結果として A 区分が 2 件出たため、停止条件は満たされていない。**

## サーキットブレーカーの作動（本体側の注記）

**H-1 は「鮮度判定の限定不足」の 3 回目である。**

| 巡 | レビュアー | 指摘 |
| --- | --- | --- |
| 設計レビュー | Codex | R-3: `changeCount` によるゲートが無い |
| 実装レビュー 1 巡目 | Claude サブエージェント | M-1: アンカーが scope で修飾されていない |
| 実装レビュー 2 巡目 | Codex | **H-1: `Read` 発行時の scope を捕捉していない** |

ワークフローのサーキットブレーカーは「**同じ種類の指摘が 3 回続いたら、個別の修正ではなく、それを生む仕組みを直す**」と定めている。**H-1 は個別修正の対象としない。**

生んでいる仕組みは **`_activeScope` が可変フィールドで、非同期コールバックの中から読めてしまうこと**である。値を足す方向の修正（R-3 で `changeCount`、M-1 で `_lastWrittenScope`）を 2 回重ねたが、読む場所が可変フィールドである限り同じ穴が開き続ける。

---

### レビュー概要

- 対象: `feature/UNT-10` / commit `06449fa`
- 対象計画: `artifact/designs/clipboard/2026-09-05-macos-clipboard-sample-scene-design-v3.md`
- 対象結果: `artifact/results/clipboard/2026-09-05-macos-clipboard-implement-sample-scene-result-v2.md`
- 対象プラットフォーム: macOS Standalone Player
- コード／ドキュメントは変更していない。
- 本レビューではテストを再実行していない。実装結果に記録された EditMode 562件、PlayMode 116件の成功を前提に、静的レビューを実施した。

1巡目の主要修正のうち、teardown guard、interval probe の `NonOwningToken` 処理、計測前のハッシュ除外、deferred stop は反映されている。一方、M-1 の鮮度判定には別の可変 scope 経路が残り、B-1 も名前の照合は直ったが handler 対応までは固定されていない。

### 重大な問題（high）

#### H-1. 1巡目 M-1 の修正が不十分で、`Read` 中の scope 切替により再び `fresh=true` を誤判定できる

- [MacClipboardManagerExampleController.cs:773](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:773)
- [MacClipboardManagerExampleController.cs:783](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:783)
- [MacClipboardManagerExampleController.cs:403](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:403)

`Read(_activeScope, ...)` の呼び出し時にはその時点の scope が Manager に渡るが、callback 内の `SameScope` は、呼び出し対象ではなく完了時点の可変フィールド `_activeScope` と比較している。

次の経路で誤判定できる。

1. General へ Copy し、アンカーが `General / changeCount=5`
2. Named へ切り替え、Named に対して Read を発行
3. Read 完了前に General へ戻す
4. Named の Read が偶然 `changeCount=5` を返す
5. callback は「アンカー General == 現在の active General」かつ changeCount 一致として `fresh=true` にする

通常の Read は observation state に入らないため、Read pending 中も scope ボタンは有効である。結果として、他 pasteboard の内容に `derived` / `roundTrip` の判定を付け得る。

これは**1巡目 M-1 と同じ誤判定クラスの反映漏れ**である。`_lastWrittenScope` を追加した方向は正しいが、Read 発行時に `MacPasteboardScope target = _activeScope` を捕捉し、Manager 呼び出しと `SameScope` の双方で `target` を使う必要がある。

また、現行テストは Controller 内の `fresh` 算出を通さず、フォーマッタへ渡された `fresh` の真偽だけを検証しているため、この回帰を検出しない。

#### H-2. 7.5 #16 の「旧 callback が0回」を画面から一意に判定できない

- [MacClipboardManagerExampleController.cs:946](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:946)
- [MacClipboardManagerExampleController.cs:957](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:957)
- [MacClipboardManagerExampleController.cs:970](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:970)

計画 §6.6 は #16 の判定値を `obs#1=0 obs#2=1` と定義している。しかし実装では:

- 登録時に辞書へ `0` を保存
- callback が来た登録だけ `count=1` と表示
- Start 成功行は新しい registration 名だけ表示

となっており、旧登録の `0` は一度も画面に出ない。「旧 callback が正しく置換されて0回」なのか、「旧登録の計数／表示自体が機能していない」のかを区別できない。

これは**1巡目では指摘されていない新規問題**で、実機確認 #16 の期待結果が表示されない A 区分に当たる。

### 改善提案（medium）

#### M-1（B区分）. B-1 の名前照合は直ったが、button name と handler の対応はテスト対象外

- [MacClipboardManagerExampleController.cs:146](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:146)
- [MacClipboardSampleSceneWiringTests.cs:44](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/MacClipboardSampleSceneWiringTests.cs:44)
- [MacClipboardSampleSceneWiringTests.cs:96](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/MacClipboardSampleSceneWiringTests.cs:96)

結論を分けると次のとおり。

- Controller の名前を1文字変えたときに落ちる: **修正済み**
- 非アクティブ GameObject に AddComponent する方式: **問題なし**。`Awake` / `OnEnable` は走らず Manager を生成しない
- `Bindings` を唯一の名前リストにしたこと: **有効**
- handler を別の既存 handler に差し替えた場合: **検出できない**

テストは `Bindings` の `Action` を `_` として捨て、名前しか読んでいない。例えば `DetectValuesButton` を `OnDetectMetadataClicked` に結び替えても、43件の名前・一意性・UXML照合はすべて通る。

これは元の B-1 の単純な反映漏れではなく、**修正後の構造に残る別の検証穴**である。現在の43組は目視上正しいが、中心導線の変異耐性は handler 対応まで閉じていない。

#### M-2. 7.5 #1 / #2 の共通イベント発火を画面から確認できない

- [MacClipboardManagerExampleController.cs:470](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:470)
- [MacClipboardManagerExampleController.cs:506](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:506)

#1 / #2 は `OwnershipChanged` の発火と `Operation == "copy"` / `"append"` も期待結果に含む。共通イベントは `Debug.Log` にしか出ず、画面の成功行は per-call callback の成功だけを示す。

したがって画面だけでは「操作は成功したが共通イベントが欠落した」を判別できない。これは**1巡目では指摘されていない新規問題**。Player.log を正式な判定先とするなら、その例外を手順に明記する必要がある。

#### M-3. 7.5 #15 の「非アクティブ中は停止し、復帰時に追いつく」を画面だけでは時系列判定できない

- [MacClipboardManagerExampleController.cs:518](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:518)

画面には changeCount と累積 event 数が出るが、イベント受信時刻、foreground/background 状態、復帰マーカーがない。復帰後に見える1件が「background 中に受信済み」か「foreground 復帰時に追いついた」のかを区別できない。

これも**新規問題**。Player.log の時刻等を併用するなら外部判定項目として明記すべきで、画面単独を要件とするなら観測値が不足している。

### 軽微な指摘（low）

なし。

1巡目の L-1〜L-7 は実装結果 v2 に理由付きで記録されており、本レビューでは重複指摘しない。

### 重点確認事項

#### 1. `Error.Message` の画面・ログ流出

**なし。**

- 失敗画面は code とローカル `reason` のみ
- Controller の失敗ログは code のみ
- 13共通イベントも code のみ
- `Local` に渡る値は固定 token、例外型名、または scope kind/name length
- Manager の native callback は raw `errorMessage` を結果へ保持するが、それ自体をログしない

Manager の `ex.Message` ログは JSON構築、subscriber例外、P/Invoke例外等の managed exception 経路であり、ネイティブから返る `MacClipboardErrorInfo.Message` の出力経路ではない。

#### 2. `Read` / `Snapshot` が返した型名

**なし。**

- Read: item数、先頭itemの型数、changeCount、判定値だけ
- Snapshot: item数、全型数、matching件数、changeCountだけ
- ReadData: data有無と長さだけ
- Metadata: 型数と content type 有無だけ

他アプリ由来の UTI 名を画面／ログへ展開する経路は確認されなかった。

#### 3. deferred stop

**意図どおり。1巡目からの回帰なし。**

- [MacClipboardSampleObservationState.cs:145](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardSampleObservationState.cs:145)
- [MacClipboardManagerExampleController.cs:1020](/Users/jonghyunkim/Desktop/unity-native-plugin/Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/macOS/Clipboard/MacClipboardManagerExampleController.cs:1020)

`TakeDeferredStop` は制御完了の成否を受け取らず、完了後の `IsObserving` を返す。`AfterControlCompletion` は成功・失敗双方の所有 callback から呼ばれる。失敗した再 Start で旧監視が残る経路も stop 発行対象になる。

deferred stop 自体の失敗を再試行しない点は、実装結果 v2 に理由付きで記録・テスト化済み。

#### 4. 32項目の画面判定

凡例: ○=画面で一意、△=仕様上の外部確認またはログ併用、×=現実装では判定不足。

| 7.5 | 判定 | 確認結果 |
|---|---|---|
| 1 | △ | Copy成功は画面、貼付と `OwnershipChanged.Operation` は外部アプリ／ログ |
| 2 | △ | 成功・同一changeCount・再Appendは画面、event operationはログ |
| 3 | ○ | 1511を画面表示 |
| 4 | △ | `CopyHtml → Read` なら `derived` を判定可能。正本の「他アプリがコピー」は判定不能として既に記録済み |
| 5 | ○ | missing/invalidの双方で `hasData=false dataLength=0` |
| 6 | ○ | `totalTypes` と `matching` の件数を表示 |
| 7 | ○ | 1512 |
| 8 | ○ | Clear後のReadで `items=0` |
| 9 | ○ | create/copy/read/remove/probeを結果履歴で追跡可能 |
| 10 | ○ | 1508 |
| 11 | ○ | matched数とenum kinds、または1513 |
| 12 | △ | 値のカテゴリ別件数／1514は画面。許可ダイアログ自体はOS側 |
| 13 | ○ | 1515または1513 |
| 14 | ○ | access behavior enumを表示 |
| 15 | × | イベント受信時期がなく、background停止／復帰時追随を区別不能（M-3） |
| 16 | × | 旧registrationの0回が表示されない（H-2） |
| 17 | ○ | 4 intervalを個別markerで実行し1523を表示 |
| 17b | ○ | 1503 |
| 17c | ○ | 1302 |
| 17d | ○ | `ArgumentException` |
| 18 | ○ | stop後に累積event数が増えないことで判定可能 |
| 19 | ○ | `changed=true/false` |
| 20 | ○ | `changed` と `observing` を同じ行に表示 |
| 21 | △ | Copy結果は画面、Player終了後の貼付は外部アプリ |
| 22 | △ | `elapsedMs` は画面、貼付・フレーム落ちは外部観測 |
| 23 | ○ | 9007 |
| 24 | △ | `elapsedMs` は画面、ピークメモリはProfiler/Activity Monitor |
| 25 | ○ | `roundTrip=match/differ/n/a` |
| 26 | △ | 別Appleデバイスでの確認が必要 |
| 27 | △ | 項目自体がConsole/Player.log確認 |
| 28 | △ | v13どおりManagerのPlayer.log診断。サンプル画面の責務外 |
| 29 | △ | Sandboxビルド確認は署名／OS確認を併用。API成否は画面 |

集計: **○ 19 / △ 11 / × 2**。

#### 5. イベント、closure、interval probe

- Managerイベント13本の購読・解除: **○**。13/13で対称、漏れ・重複なし
- `Button.clicked`: **○**。同じ `_boundButtons` から解除するため構造的に対称
- closure: **△**。通常の context、startedAt、registration、owner、target は呼び出しローカルだが、Readだけ完了時の可変 `_activeScope` を読む問題がある（H-1）
- `StartIntervalProbe` の停止性: **○**
  - 4件で終了
  - callback後に次を発行する逐次処理
  - teardown時は `_isTornDown` で停止
  - `NonOwningToken` 時はManagerへ発行せず `observationBusy` を表示
  - shared single-flight keyとの整合あり

#### 6. Manager実シグネチャとの一致

**15操作すべて一致。**

引数順、nullable scope、options、per-call callback、`StartObserving` の `scope / interval / onChanged / onStarted`、`StopObserving` を実定義と照合し、齟齬は確認されなかった。

### 計画整合性チェック

| 観点 | 判定 | 補足 |
|---|---|---|
| ボタン一覧の実装網羅性 | ○ | 43件、重複なし |
| UXML name と Controller の一致 | ○ | `Bindings` とUXMLが一致 |
| API 呼び出し仕様の一致 | ○ | 15操作すべて一致 |
| 期待結果の判定可能性 | × | #15 / #16が画面から一意に判定できない |
| 変更ファイル一覧との一致 | △ | 機能ファイル9件は一致。コミットには生成`.meta`とworkflow artifact 3件も含まれる |
| 1巡目 B-1 の修正 | △ | name変異は検出。handler対応の変異は未検出 |
| 1巡目 M-1 の修正 | × | scopeを保持したが、Read発行時のtargetを捕捉していない |

### プロジェクトルール適合チェック

| 観点 | 判定 | 補足 |
|---|---|---|
| `common.md` 準拠 | ○ | Mac接頭辞、macOS UI配置、他OS実装の変更なし、Commonにはナビゲーションのみ |
| `csharp.md` 準拠 | ○ | コメント／UI文言は英語。純粋formatter/stateのログ省略理由あり |
| ライフサイクル管理 | ○ | event 13/13、button bind/unbind対称、deferred stop正常 |
| コンパイルガードの網羅性 | ○ | Runtimeは `UNITY_STANDALONE_OSX \|\| UNITY_EDITOR` |
| 権限ガードの網羅性 | ○ | Unity側の事前権限ガード対象なし。OS拒否を1514として表示 |
| ナビゲーション統合 | ○ | TopMenu導線、Navigator追加、controller除去を確認 |
| 既存 API 互換性 | ○ | 既存分岐を壊す変更なし |

### 総合評価

**要修正（重大）**

- A区分相当: **2件**
  - H-1: scope切替競合による `fresh` 誤判定
  - H-2: #16 の期待結果が画面に出ない
- B区分: **1件**
  - button名の照合は直ったが、handler対応の変異は検出できない
- 1巡目 M-1 は**不十分な反映**、B-1 は**主要部分は解決したが別の穴が残存**
- `Error.Message`／返却UTIの漏洩、deferred stop、13イベント対称性、interval probe、Managerシグネチャには問題なし

現状はワークフローの「Aが0」の停止条件を満たしていない。

Codex session ID: 01a06f5c-3e9b-7d62-ab40-7b09be8bd90e
Resume in Codex: codex resume 01a06f5c-3e9b-7d62-ab40-7b09be8bd90e
