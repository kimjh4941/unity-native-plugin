# iOS Clipboard 自動テスト計画 v3 のレビュー v3

- 記録日: 2026-11-14
- 対象: [../designs/2026-11-14-ios-clipboard-ui-test-plan-v3.md](../designs/2026-11-14-ios-clipboard-ui-test-plan-v3.md)
- レビュアー: サブエージェント（general-purpose）と Codex（`codex:codex-rescue`）の 2 本を並行で回した。3 回目なので、v3 で変えた所と 2 回目の指摘の直りだけを見た（利用者が不在の間に、推奨案で進めた）
- 1 章・2 章はレビュアーの出力をそのまま写した。こちらの判断は 3 章にだけ書く

## 1. サブエージェント

iOS Clipboard 自動テスト計画 v3 の 3 回目のレビューです（差分だけ）。見たのは v3 の 7 章に挙がった変更と、2 回目の指摘が直ったかだけです。ファイルは編集していません。

### 裏を取った結果、合っていたもの（指摘なし）

- **CP-12（R-2）**: 1.x の実装と合っています。
  - `typeIdentifiers` は `pasteboard.types` で、宣言のコメントは "Representation types of the first item"（ClipboardSnapshot.swift:16-19、ClipboardRepositoryImpl.swift:95）。
  - `allTypeIdentifiers` は `types(forItemSet: nil)`（同 :96）。
  - MultipleText の項目は plain text だけ、Url の項目は `public.url` だけを持つ（ClipboardMappers.swift の makeItems）。
  - C# 側にも `NumberOfItems`、`TypeIdentifiers`、`AllTypeIdentifiers`、`MatchingItemIndexes` があります（IosClipboardSnapshotResult.cs:33-46）。
- **CP-31 の 1.x の結論**: LoadItem を先に呼ぶなら、v3 の予想（取り消しがちょうど 1 回、成功は 0 回）は正しいです（呼ぶ順は R-1 を参照）。
  - C# の `InvokeNative` は同期で呼ぶ（IosClipboardManager.cs:1459-1468）。ブリッジも同期で Swift に渡す（Bridge.m:134-153）。
  - Swift 側は、読み込みも取り消しも `Task { @MainActor }` で main の列に積む（UnityIosClipboardManager.swift:257-274）。
  - 読み込みの Task は、最初の区間で同期に `requests` へ登録する。`withCheckedThrowingContinuation` の本体は同期で動く（IosClipboardManager.swift:295-304、ClipboardItemLoaderImpl.swift:55-58）。
  - provider の完了は必ず `Task { @MainActor }` で後に回る（ClipboardProviderLoadExecutor.swift:173）。
  - このため、同じフレームで先に積まれた取り消しの Task が必ず先に `cancelAll` を走らせます。文言 `The clipboard load was cancelled.` も ClipboardError.swift:121 と一致します。
- **CB-5 の `dlsym` の受け手（`2.0.0`）**: 監視が止まった後は 0 回になります。
  - `ntk_clipboard_stop_observing_async` は "Stops observing for everyone in the process"（Clipboard.h:279）。
  - `listener_remove` を呼んでも監視は続きます（:291）。
  - 変化は 1 つの受け手から全登録へ配られ、保たれません（part2 5.4）。
  - 包みが受け手を外すだけで停止しない、または `NULL` で停止を断られる、という退行は `.mm` の受け手が 1 回以上受けるので捕まります。
  - 2.0.0 の `IosLibraryCApi` は動的 framework で、`_ntk_clipboard_add_change_listener` などを `T` で書き出しています（nm で確認）。`RTLD_DEFAULT` で引けます。
- **CB-5 の 1.x 側**: `none` になり、リンクの誤りにもなりません。
  - 今の `ios-native-toolkit-1.3.0` と `unity-ios-native-toolkit-1.3.0` の両方に `ntk_clipboard` の記号は 0 個です。`dlsym` は NULL を返します。
  - `.mm` は UnityFramework の target に入ります（IosUiHarnessBuild.cs:98-104）。`dlsym` だけで引くので、未定義の記号は残りません。
- **R-3、R-4**: 直っています。
  - `IsTerminated` は public static です（IosClipboardManager.cs:127）。1.x の停止は Task 1 つで済むので、0.5 秒で足ります。
  - 変化のイベントを捨てるときの文言は `Discarded a late startObserving callback`（:1505、:1599）。1.x の OnDestroy は停止を null で呼ぶので、停止の警告は出ません。
- **R-5、R-6、R-7**: 直りは判断のとおりです。

### R-1 [A2] 観点: CP-31 の根拠の向きが逆で、呼ぶ順が手順に無い
- **根拠**
  - v3:68 は「1.x は取り消しの Task が読み込みの Task より先に main の列に積まれ、必ず取り消す」と書いています。レビュー v2 の R-1 が言ったのは「取り消しの Task は、読み込みの Task が**動き出す前に**もう積まれている」です。実際に積まれる順は C# が呼んだ順そのものです（InvokeNative は同期、Bridge.m:134-153、UnityIosClipboardManager.swift:257-274）。
  - 「先に積まれた方が先に動く」なので、必ず取り消せるのは LoadItem → CancelLoads の順のときだけです。
  - 手順の列は「同じフレームで `LoadItem(Text)` と `CancelLoads`」で、順を決めていません。
- **壊れ方**: 根拠の文を字のとおりに読み、CancelLoads を先に呼んだ場合です。
  - 1.x では、取り消しの Task が空の `requests` に対して先に動き、その後に読み込みが登録されて成功します。
  - 予想（成功は 0 回、成功はテストの失敗）と食い違い、1.x の記録の段で落ちます。原因が「計画の根拠の誤り」だと分かりにくくなります。
- **直し案**
  - 手順を「同じフレームで `LoadItem(Text)` を呼び、**その直後に** `CancelLoads` を呼ぶ（この順）」にする。
  - 根拠を「読み込みの Task が先に積まれ、最初の区間で `requests` に登録する。取り消しの Task はその Task が動く前に積まれているので、完了より先に `cancelAll` が走る」に直す。

### R-2 [B] 観点: CB-5 で、2.0.0 の `native-listener=none` が「1.x と同じ」として通ってしまう
- **根拠**
  - v3:10 の判定は「1.x と同じか、既知の差分か」です。1.x の記録は `native-listener=none` です（v3:95）。
  - 2.0.0 の列は `native-listener=0` ですが、2.0.0 で `none` が出たときに失敗にするとは書いていません。
  - `.mm` の引き方も書かれていません。たとえば次のようなものです。
    - `RTLD_DEFAULT` で引くこと（`dlopen` した UnityFramework の handle や `RTLD_SELF` では、別の framework にある記号は引けない）
    - 記号の名前に先頭の `_` を付けないこと
    - 型を自分で宣言すること（1.x には `NativeToolkitC/Clipboard.h` が無いので、`#include` すると 1.x で組めない）
- **壊れ方**: 2.0.0 で `.mm` の引き方を誤り、`dlsym` が NULL を返したとします。行は `native-listener=none` になり、1.x の記録と同じなので通ります。
  - この場合は、v3 が足した「ネイティブの監視が止まったことの証明」がされないままです。
  - 特に、包みがリスナーを外すだけで停止を落とす退行（Codex の CB-5 の懸念そのもの）は、`late-change=0` だけで通ります。
- **直し案**
  - CB-5 に「`.mm` は `dlsym(RTLD_DEFAULT, "ntk_clipboard_add_change_listener")` で引く。型は自分で宣言する（ヘッダーは読み込まない）。`ntk_clipboard_change_free` も同じように引いて受けたものを解放する」と書く。
  - 別に ABI の目印を引き、行に `abi=<ntk_version の値|none>` として出す。
  - 判定は「`abi` が在るのに `native-listener=none` なら失敗」にする。または「2.0.0 では `none` は 1.x と同じとはみなさず失敗」と明記する。

### R-3 [C] 観点: 3 章の未完了の数え方から、例外の直の呼び出しが漏れる
- **根拠**
  - v3:106 は、`CancelLoads`、`StopObserving`、`CreatePasteboard(Unique)`、CP-21 の `RemovePasteboard(General)` を「manager を直に呼んでよい例外」にしました。
  - v3:107 は「未完了は、補助の関数が呼んだ数と完了の数を自分で数えて見る」としています。
  - 直に呼んだ操作は補助の関数を通らないので、数えられません。
- **壊れ方**: たとえば CP-33 や CP-34 の `StopObserving` の完了が来なかった場合です。キー `observation` が残り、次のテストの `StartObserving` が `CLIPBOARD_BUSY` になります。数えに入っていないので、原因を元のテストに帰すという 3 章の狙いが外れます。
- **直し案**: 例外を「scope を取らない操作は、scope の引数を持たない補助の関数を通す（数えに入れる）」と言い換える。「manager を直に呼ばない」の規則は、scope を取る操作にだけかける。

**件数**: A1 0 件、A2 1 件（R-1）、B 1 件（R-2）、C 1 件（R-3）。合計 3 件。
## 2. Codex

v2→v3 の差分と第2回指摘への対応を確認しました。

- **CP-31**：同一フレームで間に yield を挟まず連続呼出しする条件では、取消必須の予想は妥当です。要求登録後、provider の完了は別の MainActor Task に配送されます（タグ `1.13.0` の `ClipboardItemLoaderImpl.swift:55`、`ClipboardProviderLoadExecutor.swift:173`）。説明の「読み込みの Task」は、この完了配送を指すと読めます。
- **CB-5**：独立リスナーでネイティブ監視停止を観測する修正は妥当です。`Clipboard.h:279` の停止は全体に効き、`:291` の登録解除とは区別できます。シンボルも2.0.0では公開、1.xでは不在を確認でき、関数ポインター経由の `dlsym` 呼出しなら直接リンク参照は不要です。
- **CP-12・CP-29**：新しい予想と固定入力は一次ソースに一致します。ほかの第2回指摘への修正も適切です。

機械照合は失敗0件。iOSテストは実行していません。ファイルは変更していません。

指摘なし。

## 3. 判断

**2 人とも A1 が 0 になったので、レビューを止める。** 止めた後の直しとして、次を v4 に入れる（Share の計画 v3 と同じ扱い。v4 はレビューに出さない）。

- R-1（A2）: CP-31 の手順を「`LoadItem(Text)` を呼び、その直後に `CancelLoads`（この順）」にし、根拠の文を直す
- R-2（B）: CB-5 の `.mm` の引き方（`dlsym(RTLD_DEFAULT, …)`、型は自分で宣言、受けたものの解放）と、`abi` の目印を行に出し、`abi` が在るのに `native-listener=none` なら失敗にする
- R-3（C）: scope を取らない操作も、scope の引数を持たない補助の関数を通して数えに入れる
