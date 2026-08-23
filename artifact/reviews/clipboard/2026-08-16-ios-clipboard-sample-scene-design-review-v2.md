# レビュー結果

- 日付: 2026-08-16
- 対象ファイル: `artifact/designs/clipboard/2026-08-16-ios-clipboard-sample-scene-design-v2.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- v1 の最重要問題だった単一 `_pendingMarker` を廃止し、immutable な `ResultContext` を per-call callback に capture する構造へ変更したことで、異種操作の逆順完了を正しく扱える。
- 共通結果イベントを shape-only ログへ限定し、UI と scope mutation の所有者を per-call callback にした境界が明確である。
- observation teardown を純粋な状態機械へ切り出し、Start pending 中の画面離脱で Stop を即時発行しない方針は、共有 single-flight key と整合している。
- Editor 導線、M-4 の識別可能な fixture、M-14 の seed、M-22 の大容量 fixture、file cleanup、C# validation の確定文言まで具体化され、v1 の曖昧さの多くが解消された。
- `IosClipboardSampleResult` と `IosClipboardSampleObservationState` を EditMode で検証する方針は、Controller を層1で生成しない `testing.md` の制約に沿っている。
- 53ボタンは多いが、検証ハーネスとして各ボタンの目的が追跡可能であり、数自体は問題ではない。

## 改善点

### 高優先度

#### 1. Busy Start の拒否 callback と進行中 Start の状態所有者が区別されていない

- セクション: 4.3、4.7、6.5、7.2
- 対象行: 284、365〜381、627〜670、784〜785
- `BusyStartObservingTwiceButton` は `StartObserving` を2回連続で呼び、2本目は `CLIPBOARD_BUSY` で即時 callback される。
- 両 callback が同じ `_observation.CompleteStart(result.IsSuccess)` を通る実装では、2本目の失敗が `ControlPending = false` にしてしまい、1本目がまだ native pending なのに状態機械は「未観測」に戻る。
- その時点で画面を離れると `RequestStop()` は deferred stop を記録できず、後から1本目が成功して observation が画面外に残る。v1 で修正対象だった teardown 漏れが Busy デモ経由で再発する。
- per-call の `ResultContext` は表示を対応付けるだけで、observation control state の所有権までは表していない。

改善提案:

- observation control に owner token / generation を導入し、`CompleteStart(owner, result)` / `CompleteStop(owner, result)` は現在の owner と一致する callback だけが状態を変更できるようにする。
- 最小構成なら Busy デモの1本目だけを状態所有者とし、拒否される2本目の callback は結果表示だけを行い、`IosClipboardSampleObservationState` へ触れないことを明記する。
- `Start #1 pending → Start #2 busy → OnDisable → Start #1 success → Stop success` を純粋 helper または Controller seam のテストに追加する。
- 離脱後 callback の順序は「状態遷移と必須 cleanup を先に実行し、UI 更新だけを `this == null || !isActiveAndEnabled` で抑止」と固定する。ガードで callback 全体を早期 return すると deferred Stop が発行されない。

### 中優先度

#### 1. M-16 の「2回目も成功する Start replacement」を実行できる導線がない

- セクション: 4.7、7.2
- 対象行: 374〜381、784〜785
- S-25a は pending 中の2本目を busy にするため、native の registration replacement / generation gate を検証しない。
- S-25b は `Stop Observing → Start Observing` なので、旧 registration を明示停止してから新規登録している。実装計画 v5 の M-16 が求める「成功した Start の後に、Stop を挟まずもう一度 Start し、通知が重複しないこと」とは異なる。
- `StartObservingButton` は観測中 disabled のため、現 UI から2回目の成功 Start を発行できない。

改善提案:

- `Restart Observing` 専用ボタンを追加するか、観測中だけ有効な replacement 導線を定義する。
- 手順を「Start #1 成功 → Stop を挟まず Start #2 成功 → 外部変更1回 → Events が1だけ増える」とする。
- `onChanged` と共通 `ClipboardChanged` の両方で event count を増やすと二重計上になるため、サンプルは `onChanged: null` を渡し、共通イベントだけを UI/status 更新元にすることを明記する。

#### 2. `CopyCustomDataButton` の UTI が fixture 表内で矛盾している

- セクション: 4.5、7.2
- 対象行: 334、341、781
- `FileFixturePayload` は `CopyCustomDataButton` が `public.data` で copy すると記載されている一方、同じボタンの `CustomTypeIdentifier` は `com.jonghyunkim.nativetoolkit.example.custom` と記載されている。
- `Load File (public.data)` が64バイトを取得するには、copy 側 representation の UTI も `public.data` でなければならない。実装者が `CustomTypeIdentifier` に従うと S-22 が成立しない。

改善提案:

- M-13 の file fixture は `public.data` に統一し、未使用になる `CustomTypeIdentifier` を削除する。
- 独自 UTI もデモするなら別 representation / 別ボタンに分離し、`Load File` の request UTI と対応を表で固定する。

#### 3. M-23 の引き継ぎ案は、元の P/Invoke / IL2CPP 実機メモリ契約を検証しない

- セクション: 7.4
- 対象行: 806〜815
- 実装計画 v5 の M-23 は、実機 IL2CPP/ARM64 で上限近傍の `LoadItem(Image)` を通し、native JSON、`DllImport` による UTF-16 string、parser、decoded `byte[]` を含むピークと OOM の有無を測る項目である。
- response JSON を直接組み立てる EditMode / PlayMode ベンチは parser と decoded-length 判定は測れるが、native callback と `DllImport` string marshaling を迂回する。v5 5.9.2 が残存リスクとしている約170MBの UTF-16 marshalling を評価できない。
- したがってこのベンチは M-23 の補助測定にはなるが、M-23 の引き継ぎ先にはならない。

改善提案:

- parser ベンチを「事前の補助測定」と位置付け直す。
- M-23 本体は、実機 native fixture / test seam から上限近傍の `LoadItem(Image)` 応答を返し、Unity の P/Invoke callback まで通す別 artifact へ引き継ぐ。
- fixture の生成コストを測定から分離するため、native 側で事前生成またはファイルから読み込み、計測区間を load 開始直前から callback 解放後までに固定する。

#### 4. 画面破棄後 callback の共通方針が UI ガードだけでは不足する

- セクション: 4.6、6.4.1、6.4.3、6.5
- 対象行: 345〜363、558〜625、668〜670
- Navigator は Controller を `Destroy` するため、任意の operation callback が画面離脱後に届きうる。
- UI を触らないことだけでなく、処理ごとに「画面外でも必須」「画面外では禁止」を分ける必要がある。例えば observation の deferred Stop と `LoadItem(File)` の caller-owned directory cleanup は画面外でも必須だが、seed Copy 成功後に新しい Load + Cancel を開始することや active scope を変更することは不要である。
- `LoadItem(File)` も size 取得が例外になった場合に directory cleanup まで飛ぶ書き方では、caller-owned resource が残る。

改善提案:

- callback の順序を `必須 state/ownership cleanup → active guard → UI/state mutation・後続デモ操作` として表にする。
- File result は size 取得と directory 削除を分離し、size 取得失敗でも削除を `finally` 相当で試みる。`fileSize=-1, cleanup=<ok|failed>` など全経路の表示契約を決める。
- Seed And Cancel は seed 成功時点で Controller が inactive / destroyed なら後続 Load + Cancel を開始しない。

### 低優先度

#### 1. S-15 の `Copy Plain Text` の長さ注記が fixture と一致しない

- セクション: 4.5、7.2
- 対象行: 330、774
- `PlainTextBody` は日本語と UTF-16 surrogate pair を含み、S-15 記載の「14文字」と一致しない。14文字なのは `LocalOnlyBody` である。

改善提案:

- S-15 から「14文字」を削除するか、実際の `PlainTextBody` の Unicode scalar / UTF-16 length のどちらを指すかを明記する。M-6 は `items=2` が主眼なので、長さ注記自体を外すのが簡潔である。

#### 2. `LargePng` のランダム生成条件を再現可能にする

- セクション: 4.5
- 対象行: 339
- ランダム seed と生成 API が未指定だと、PNG サイズと実行時メモリが run ごとに変わり、M-22 の比較が不安定になる。Unity の global random state を変更すると他のサンプルにも影響しうる。

改善提案:

- 固定 seed のローカル PRNG で pixel bytes を生成し、生成後の PNG byte length を結果へ表示する。
- 目標範囲（例: 3〜5MiB）を外れた場合は計測を開始せず fixture failure と表示する。

## 不足項目

- Busy Start の rejected callback が observation state を所有しないことを固定する owner 契約と競合テスト。
- Stop を挟まない2回の成功 Start による registration replacement / generation gate の実機導線。
- 非 active / destroyed Controller に届く callback の、必須 cleanup・禁止する後続操作・UI抑止の共通順序。
- M-23 の native → P/Invoke → IL2CPP callback を含む実機計測への具体的な引き継ぎ先。

## 総合評価

要修正。

v1 の result correlation、Editor 導線、fixture、resource ownership は大幅に改善され、サンプル計画としての完成度は高くなった。一方、Busy Start の拒否 callback が進行中 Start の状態を解除できる設計のままでは、画面離脱時に observation が残る高優先度リスクがある。また、現在の enabled 契約と手動手順では、M-16 の「Stop を挟まない2回の成功 Start」を実行できない。

上記の observation ownership を確定し、UTI の矛盾、callback lifetime、M-23 の測定境界を修正すれば、実装へ引き継げる状態になる。53ボタンという規模は引き続き許容範囲である。
