# レビュー結果

- 日付: 2026-08-16
- 対象ファイル: `artifact/designs/clipboard/2026-08-15-ios-clipboard-sample-scene-design-v1.md`
- 機能名: clipboard
- プラットフォーム: iOS

---

## 強み

- 実装結果 v3 の15操作、10イベント、B-1、single-flight、tombstone、エラー型を正確に抽出している。
- native の11セクションを比較し、Bridge 非公開の Paste Control を除外した根拠が明確である。
- active scope、observation、イベント数を状態行に分離した判断は、iOS clipboard 固有の操作モデルを理解しやすくする。
- 本文・base64・検出値・pasteboard 名をログへ出さず、画面にも長さ・件数だけを出す規約が一貫している。
- `CLIPBOARD_BUSY` を専用セクションで見せる判断は、Unity Bridge 固有の重要制約を利用者へ伝えるうえで有効である。
- 51ボタンは多いが、M-1〜M-24の検証ハーネスを兼ねる画面としてセクション分割されているため、数だけを理由に削る必要はない。
- Navigator / TopMenu / Resources / wiring test の変更範囲と、シーンを変更しない理由が具体的である。

## 改善点

### 高優先度

#### 1. 単一の `_pendingMarker` と共通イベントだけでは、並行操作の結果を正しい呼び出しへ対応付けられない

- セクション: 3.2、6.1、6.3、6.4
- 対象行: 165、396、435〜475
- Manager の single-flight は「同一操作」だけを直列化し、`Read` と `GetSnapshot`、`LoadItem` と `CancelLoads` など異なる操作の並行実行を許す。
- 現計画では各クリックが `_pendingMarker` を上書きし、結果は共通イベントから最後の marker を読む。先に開始した操作が後から完了すると、別操作の marker で表示される。
- `CancelDuringLoadButton` は設計上必ず2操作を並行させるため、`LoadItem` と `CancelLoads` のどちらか、または両方が誤った marker / sequence になる。busy rejection も進行中呼び出しとは別の結果なので、同一 operation の単一 slot では区別できない。
- Create / Remove の完了時に active scope を変更する処理も、開始時の scope を捕捉しなければ、完了までに利用者が選んだ新しい scope を上書きしうる。
- native サンプルは各 async closure に marker を閉じ込めており、この誤対応を起こさない。

改善提案:

- `BeginResult(marker)` が immutable な `{ sequence, marker }` context を返し、各 Manager 呼び出しの **per-call callback がその context を capture** して完了表示する方式へ変更する。
- 共通結果イベントは安全な shape-only ログ用途に限定し、UI result / scope mutation は per-call callback の所有物とする。`ClipboardChanged` だけは継続イベントとして画面と status を更新する。
- Create / Remove は開始時の対象 scope も callback に capture し、Remove 成功時は「現在も同じ scope が active の場合だけ General へ戻す」など所有条件を明記する。
- busy 2本呼び出しでは callback context を2個作り、拒否された2本目と完了する1本目が別の sequence / marker で表示されることをテストする。

### 中優先度

#### 1. `OnDisable` の無条件 `StopObserving()` は Start pending 中に busy となり、画面離脱後も監視が残りうる

- セクション: 6.2
- 対象行: 424〜431
- Start / Stop は共有 single-flight key を使う。Start callback 待ちの間に画面を離れると、`OnDisable` の Stop は `CLIPBOARD_BUSY` で拒否され、その後 Start が成功しても再度 Stop する経路がない。
- 先に10イベントを解除するため、Stop の拒否や遅い Start 成功を Controller は観測できない。
- 観察中に scope ボタンを操作できるかも未定義で、status の active scope と実際に観察している scope がずれる可能性がある。native サンプルは observation 中の Scope セクションを無効化している。

改善提案:

- `_observationControlPending`、`_isObserving`、`_stopAfterStart` を持つ teardown 状態遷移を定義する。
- Start の per-call callback は、画面離脱要求後に成功した場合、UIへ触れず `StopObserving` を開始する。
- Stop pending 中の重複 Stop を避け、scope 変更と `ErrObserveMissingNamedButton` の可否を observation 状態に連動させる。
- Start pending → OnDisable → Start success → Stop success を固定する Controller/helper テストを追加する。

#### 2. Editor の TopMenu 導線と Editor 手動確認が両立していない

- セクション: 4.1、5.2、6.7、7.1
- 対象行: 189、350前後、507、517〜521
- S-2 は Editor で Clipboard ボタンを押すとダイアログだけを出すとしている。一方、S-4 は Editor 上の sample screen で操作して B-1 を表示し、実装順序6は Editor で TopMenu → Clipboard の遷移を確認するとしている。
- 現在の `TopMenuExampleController` と同じ Editor ダイアログ方式のままでは sample screen に到達できず、S-3〜S-5と V-1を実行できない。

改善提案:

- Editor の active build target が iOS なら `ShowIosClipboard`、Android なら既存 Android screen へ遷移するなど、Editor route を明示する。
- ダイアログ方式を維持する場合は、別の明示的な Editor 導線を設け、S-2 / S-4 / V-1 の手順をその導線に合わせる。

#### 3. M-1 / M-4 / M-14 / M-16 / M-22 の手動導線が、記載された fixture と操作だけでは成立しない

- セクション: 4.3、4.4、4.6、6.3、7.2
- 対象行: 225〜269、290〜295、311〜319、444〜450、527〜551
- M-1: S-10 は日本語・絵文字を要求するが、Controller 例は英語のみの `"Hello from Unity Native Toolkit"` である。
- M-4: localOnly true / false を別デバイスで判別する既知 baseline と識別可能な fixture がない。`Read` 表示も text length を出さないため、値を表示せずにどの fixture が届いたかを区別できない。native サンプルには長さの異なる device-B baseline がある。
- M-14: `CancelDuringLoadButton` は active scope に画像がある前提を作らないため、load がデータ不在で先に失敗し `CLIPBOARD_CANCELLED` にならない可能性がある。
- M-16: 「即時2回目の B-0」と「1回目完了後に再 Start してイベントが重複しない世代ゲート確認」が1行に混在している。後者の変更イベント発生・件数確認手順がない。
- M-22: fixture は 1x1 PNG だが、S-31 は数 MiB の ImageData 往復を要求する。さらに M-22 本来の Instruments peak memory 記録手順がない。

改善提案:

- fixture と manual step を1対1で対応させる。PlainText は日本語・絵文字を実際に含める。
- M-4 用に異なる既知長の baseline / transferable body を用意し、`Read` に `textLen` 等の非機微な識別情報を追加する。
- Cancel demo は section note で先に画像を copy させるか、ボタン内で seed copy 成功後に load → cancel を開始する。
- M-16 を「pending 中の busy」と「成功後の再 Start → 外部変更1回 → Events が1だけ増える」に分ける。
- M-22 用の数 MiB fixture、生成・破棄方法、Instruments の観測項目を明記する。通常の1x1画像とは分けてもよい。

#### 4. 画像 fixture と `LoadItem(File)` の所有リソース解放が未定義

- セクション: 4.4、4.6
- 対象行: 288〜295、317
- `new Texture2D(...)` を繰り返し生成するが、`Destroy(texture)` / `finally` が計画にない。
- `IosClipboardLoadedItem.Path` は caller-owned temporary file で、公開 XML コメントも caller に削除責任があるとしている。native サンプルも file size を取得後、返却ファイルの親にある request-scoped directory を削除する。現計画は `pathLen` を表示するだけなので、一時ディレクトリが反復実行で蓄積する。

改善提案:

- PNG生成 helper は `try/finally` で `Texture2D` を必ず `Destroy` する。
- File result はパスをログ・画面へ出さず file size を読み、caller-owned request directory を削除する。削除失敗は `cleanup=failed` と安全に表示・ログし、どのディレクトリまで削除してよいかを native 契約に合わせて固定する。

### 低優先度

#### 1. C# 例外ケースを「ボタン化するとクラッシュする」と断定せず、注記の確定条件を決める

- セクション: 4.3、5.5、6.5
- 対象行: 286、V-3、482〜485
- `Named("")` / `Color(NaN, ...)` は try/catch すればサンプルをクラッシュさせずに表示できるため、技術的にボタン化できないわけではない。
- 51ボタンをこれ以上増やさない判断自体は妥当だが、V-3のまま実装へ渡すと注記の有無・文言が実装者判断になる。

改善提案:

- ボタンを追加しない方針を維持するなら、subtitle または Errors section note の確定英語文言と wiring 対象 label を計画で固定する。
- C# validation もデモ対象にする場合だけ、例外を catch して `[local.validation]` として表示する専用ボタンを検討する。

## 不足項目

- M-23（64MiB近傍）の実施主体・fixture供給方法・Instruments観測手順。サンプル外に出すなら、別artifactまたは具体的な手動手順への引き継ぎ先が必要。
- marker / sequence correlation と observation teardown を検証する Controller または純粋 helper テスト。UXML wiring testだけでは今回の主要状態バグを検出できない。
- observation 中の Scope / Start / Stop / error ボタンの enabled 状態と、status がどの scope を観察中と表示するかの契約。
- `LoadItem(File)` の caller-owned temporary directory cleanup と、cleanup失敗時の表示契約。

## 総合評価

要修正。

機能網羅性、セキュアな表示規約、既存画面との構造的整合は高い。一方、単一 `_pendingMarker` と共通イベントだけの設計は、Manager が意図的に許可する異種操作の並行実行で結果を誤対応させるため、サンプルの中心である結果表示の信頼性を損なう。さらに observation teardown、Editor 導線、M-22 fixture、一時ファイル所有権を実装前に確定する必要がある。

51ボタンという規模自体は問題ではない。ボタンを削るより、呼び出し単位の結果 context、section 前提注記、enabled 状態、再現可能な fixture を整える方が優先される。
