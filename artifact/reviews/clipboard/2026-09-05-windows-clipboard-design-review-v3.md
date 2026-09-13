# レビュー結果

- 日付: 2026-09-07
- 対象ファイル: `artifact/designs/clipboard/2026-09-05-windows-clipboard-design-v5.md`
- 機能名: clipboard
- プラットフォーム: Windows
- 結果: 要修正（A1 が 3 領域に残存）
- レビュー観点: native-toolkit API、P/Invoke、Manager + Bridge、変更ファイル、エラー契約、スレッド・メモリ契約、EditMode / PlayMode / 実機テスト、IL2CPP、コンパイルガード、P1～P5

---

## 強み

- 27 個のネイティブ API と C# 公開 API の対応が整理され、P/Invoke、callback、文字列・バイトバッファのマーシャリング方針も具体化されている
- v2 の高優先度指摘だった空判定、完了済み・未配送結果、予約 provider の寿命を設計へ取り込み、回帰テスト案まで追加している
- Windows Player の STA とメッセージポンプを実機スパイクで確認し、未検証事項と確認済み事項を分離している
- 新規 Runtime 9 ファイルとテスト 7 ファイルは、ファイル名・型名とも `Windows` 接頭辞を持つ。他プラットフォームの変更や `Runtime/Common` への機能ロジック追加もなく、P1～P5 の方針に適合している
- クラスガードとネイティブ境界ガードを分離し、Editor で公開 API とテストをコンパイルしつつ、Player に不要なプラットフォームコードを入れない構造が明記されている
- IL2CPP 向けに、static callback、`MonoPInvokeCallback`、delegate の GC ルート、例外を ABI 境界へ漏らさない規則が揃っている

## 改善点

### 高優先度

1. **[A1][7.4 / 7.5 / 7.10] shutdown の内部呼び出し経路と状態遷移が成立していない**
   - 状態表では `ShutdownWithDrain` の開始時に `Draining` へ移り、`_initialized = false` にする。一方、7.5 は public API の事前チェックとして「破棄済み → 初期化済み」を要求する。この状態で `ShutdownWithDrain` が public `TryShutdown` を呼ぶと、ネイティブへ到達する前に `NotInitializedByHost` で拒否され得る
   - `OnDestroy` は tombstone をネイティブ呼び出しより先に立てた後、`TryShutdown` を呼ぶ計画である。public `TryShutdown` が通常の破棄済みチェックを通るなら `ManagerDestroyed` で拒否され、最後のネイティブ shutdown が実行されない
   - 状態機械には `ShutdownWithDrain` からの遷移だけがあり、利用者が public `TryShutdown` を直接呼んで `completed == false` または `true` を得た場合の状態がない。ネイティブ `Uninit` は最初の呼び出しから lifecycle gate を閉じ得るため、C# 側を `Running` のままにすると以後の操作が静かに不整合になる
   - 改善案: 通常の public 事前チェックから分離した `TryShutdownCore` を設け、`ShutdownWithDrain` と `OnDestroy` はこれを呼ぶ。public `TryShutdown` 自身も最初の呼び出しで `Draining` へ遷移し、完了・未完了・終端失敗ごとの状態を定義する。`OnDestroy` は tombstone 設定後でも core を実行できる構造にする

2. **[A1][2.8 D-9 / 7.7] `UNKNOWN` の失敗地点を C# から識別できず、provider 世代表を実装できない**
   - 7.7 は `UNKNOWN`(19) を「`EmptyClipboard` 失敗」と「配置失敗後の rollback 成功」に分け、前者では旧世代維持、後者では旧・新世代をマージするとしている。しかし C ABI が返す情報は同じ `pError = 19` だけで、C# から失敗地点を判定する手段がない
   - 同じ format 名が旧・新世代に存在する場合、誤って新 provider を優先すると、native に残った旧 renderer が異なる内容を生成する。コンパイルも通常の成功テストも通るため、実機で予約形式が要求されるまで発覚しない
   - `InjectReserveResultForTests(int pError)` も失敗地点を表現できないため、現在のテスト seam では表の分岐を検証できない
   - 改善案: すべての `UNKNOWN` で旧世代と旧 cache を保持するなど、C# が観測可能な値だけで決まる安全な規則に統一する。rollback 成功時に残る余分な参照は次の予約成功または shutdown 完了まで保持してよい。失敗地点別の処理が必要なら、ネイティブ API に mutation 状態を返す追加情報が必要になる

3. **[A1][7.3] fallback COM 初期化後にネイティブ初期化が失敗した場合の解放経路がない**
   - `CoInitializeEx` が `S_OK` または `S_FALSE` を返した後、`initClipboardManager` は `OutOfMemory`、`Unknown`、`MonitorRegisterFailed` などで失敗し得る。この場合、ネイティブ側は HWND と coordinator を早期 cleanup するが、C# 側は `_initialized = false` のため通常の `TryShutdown` を実行できない
   - 現在の表は COM 参照を「`TryShutdown` が completed になった後」にだけ解放するため、初期化失敗時に本層が取得した COM 参照が残る
   - 改善案: `initClipboardManager` 失敗時は、ネイティブが未初期化へロールバック済みであることを前提に、その場で本層所有の COM 参照を同一スレッドから解放する。P/Invoke 例外時を含む ownership 遷移を表へ追加し、成功、ネイティブ失敗、Bridge 例外の各ケースをテストする

### 中優先度

1. **[A1][7.3] 冪等な `Initialize` に対する `wantsToQuit` の多重購読を防ぐ規則がない**
   - ネイティブ初期化は owner thread からの再呼び出しを成功として返す。設計は `Initialize` 成功時に毎回 `Application.wantsToQuit += OnWantsToQuit` を実行するため、同じ handler が複数登録され、`OnDestroy` の 1 回の解除では購読が残り得る
   - `Initialize` を2回呼ぶケースで購読数が1のままであることを保証し、購読済みフラグまたは解除後再登録を用いる。2回初期化 → destroy / quit の回帰テストを追加する

2. **[B][7.7 / 9.2] 予約呼び出し中の callback と provider 世代切替を検証できない**
   - native は `EmptyClipboard` 後、`renderers_` を新世代へ置き換えてから各 `SetClipboardData` を実行する。設計は managed provider の反映を P/Invoke 成功後としているため、P/Invoke が戻る前に render callback が再入した場合の参照世代が未定義
   - 要検証: 実際にこのタイミングで callback が起こり得るかをネイティブテストで確定する。起こり得る場合は、旧世代を保持しながら呼び出し中だけ新世代を解決できる staging が必要。テスト seam は結果コードの注入だけでなく、ネイティブ呼び出し中に render callback を同期実行できる形にする

3. **[A2][9.1] バッファ helper のテスト記述が修正済みの空判定契約と矛盾する**
   - 9.1 の「必要サイズ 0 は `pError` に関わらず空成功」は、7.5 と同じ節の H1 回帰テストに反する。このままテスト化すれば、正しい実装に対してテストが失敗するため実装段階で発覚する
   - 実装結果ファイルのチェックリストへ「バッファ helper でも先にエラーを分類し、0 + `Busy` / `NotInitialized` / `InvalidData` を成功にしない」と転記する

### 低優先度

1. **[C] 文書内の旧記述と件数を機械照合で修正する**
   - 3.1 は Clipboard が `DEVELOPMENT_BUILD` 分岐を持たないとしているが、2.7 / 5.1 / V-6 は分岐必須としている
   - 2.6 は `uninit` の FALSE 時エラーをすべて未完了としているが、7.4 は `WrongThread` などを終端失敗へ分類している
   - 6.2 の PlayMode ファイル説明は「7 種の共通 event」だが、7.1 / 9.2 は `FlagChecked` を含む 8 種である
   - 7.3 の V-1 は「要検証」と残っているが、10 章では解決済みである
   - 8.2 の `RecoverDeferredState` には、ネイティブ `RecoverFromPartialState` が返し得る `Busy`(3) がない
   - Definition of Done は M-1～M-22 の確認だけを要求し、追加済みの M-23 / M-24 を含まない
   - 12 章は `WindowsNotificationManager` の `-debug` DLL 名を是正対象としているが、V-6 では現在の分岐を正しい実装としている
   - 前版欄が v3 までで止まり、v4 が抜けている

## 不足項目

- public `TryShutdown`、`ShutdownWithDrain`、`OnDestroy` が共通利用する、通常 API の tombstone / initialized ガードを回避した内部 shutdown core と、その状態遷移テスト
- COM ownership の `None` / `Initialized` / `RefCounted` について、ネイティブ初期化成功、ネイティブ初期化失敗、P/Invoke 例外、shutdown 完了を網羅する遷移表とテスト
- `UNKNOWN` を失敗地点へ分解できない現行 ABIを前提とした、観測可能な値だけで決まる provider / cache 保持規則
- `RecoverDeferredState` が `NONE` を返したとき、「partial 状態から回復した」のか「もともと partial ではなかった」のかを managed 側でどう判断し、provider / cache をいつ解放するかの契約。native は非 partial 時に `NONE` を返して既存予約を維持するため、成功コードだけを根拠に消去してはならない
- `Initialize` の冪等呼び出し、`wantsToQuit` 購読、再初期化、shutdown 後の解除を含む lifecycle テスト
- 機械照合の実行環境修正。`scripts/check_design_consistency.py` は、macOS framework の `Versions/Current/_CodeSignature` を走査中に `FileNotFoundError` で停止し、v5 の判定結果を出力できなかった

## 総合評価

v5 は、API 網羅性、P/Invoke、プラットフォーム独立性、空判定、非同期結果の exactly-once 配送、IL2CPP、テスト層の整理について高い水準に達している。前回レビューの主要指摘も、設計意図としてはほぼ反映されている。

ただし、A1 は 3 領域に残る。特に shutdown は `Draining` / tombstone と public API の事前チェックが衝突し、終了時にネイティブへ到達しない実装になり得る。遅延予約は、同じ `UNKNOWN` から異なる失敗地点を判定する前提が ABI 上成立しない。COM fallback はネイティブ初期化失敗時に参照を解放できない。

review-document の停止基準「A1 が 0」を満たしていないため、この版を確定して実装へ進む段階ではない。加えて、必須の機械照合はリポジトリ内の macOS framework 走査エラーで完走していない。上記 A1 と中心契約の B を修正した改善版を作成し、機械照合を通したうえで再レビューすることを推奨する。
