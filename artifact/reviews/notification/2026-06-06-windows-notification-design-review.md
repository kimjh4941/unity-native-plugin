# レビュー結果

- 日付: 2026-06-06
- 対象ファイル: artifact/designs/notification/2026-06-06-windows-notification-design-v1.md
- 機能名: notification
- プラットフォーム: Windows

---

## 強み

- native-toolkit 公開関数 13 件・エラーコード・setBadge グリフ値・JSON ペイロードスキーマを表形式で整理しており、API 確認結果の粒度が高い
- DllImport 宣言で `CharSet.Unicode` と `[MarshalAs(UnmanagedType.LPWStr)]` を一貫して適用しており、Windows ワイド文字列マーシャリングの方針が妥当
- `getAllNotifications` のバッファ出力型に対し `AllocHGlobal / PtrToStringUni / try-finally FreeHGlobal` というメモリ契約を明示している
- コールバックがバックグラウンドスレッド由来である前提で `UnityMainThreadDispatcher.Instance.Enqueue` 経由の転送契約を明記しており既存パターンと整合
- 要検証事項（unpackaged/CLSID、管理者権限での Show サイレント失敗、バッファサイズ不足）を断定せず明記している

## 改善点

### 高優先度

**[3. 変更ファイル一覧 / Manager パターン準拠性]**
- 問題: 2章で「per-call Action<T> の2段構成」に言及しているのに、4章の実装詳細に Operation 定数・per-call onResult 引数・s_xxxDelegate（GC防止）が落ちていない。既存 Mac Manager の必須構成要素が実装設計として欠落している
- 改善: 各 public API の `per-call Action<WindowsNotificationResult>? onResult` 引数、Operation 定数一覧、`WindowsNotificationResult.Success/Failure` ファクトリの設計を実装詳細に明記する

**[5. エラーケース一覧と返却仕様]**
- 問題: `getAllNotifications` でバッファ不足が起きた場合の挙動（pError に何が返るか、切り詰めか失敗か）が API 確認結果・エラーケース一覧の双方で未定義
- 改善: バッファ不足時の native 返却仕様を確認し、不足時の再呼び出し（バッファ拡張リトライ）戦略をエラーケースとして追加する

**[7. テスト方針]**
- 問題: EditMode テストで JsonBuilder のバリデーション制約（buttons 最大 5 件・audio.loop 制約・args/invokeUri 排他制約）の検証が含まれていない。バリデーションをどの層（JsonBuilder か native か）で担保するかも未記載
- 改善: バリデーション制約の担当層を JsonBuilder と定め、各制約違反に対する EditMode テストケースを追加する

### 中優先度

**[4. 実装詳細 / DllImport 宣言]**
- 問題: `initNotificationManager` の `bool isPackaged` に `[MarshalAs(UnmanagedType.Bool)]` の明示がない。`getNotificationSetting` 戻り値・setBadge グリフ値の C# enum 化方針が示されていない
- 改善: `[MarshalAs(UnmanagedType.Bool)]` を明示。戻り値・グリフ値を C# enum として定義する方針を追加する

**[3. 変更ファイル一覧]**
- 問題: `NotificationInvoked` で argsJson を string のまま渡す設計の根拠が不明確。ExampleController に伴う UXML/USS/シーンファイルの要否が変更ファイル一覧に含まれていない
- 改善: argsJson を構造化型で渡すか string のまま渡すかを明示的に判断して記載。UXML/USS/シーンファイルの新規要否も分類する

**[5. エラーケース一覧]**
- 問題: `getNotificationSetting` が int を同期返却し Result 契約に乗らない特例であることが表の注記だけでは不明確
- 改善: Result を返さず enum を同期返却する特例 API であると明記し、他の out pError 系 API との契約差分を注記する

**[6. メモリ契約]**
- 問題: `AllocHGlobal((int)bufferSize * 2)` の `bufferSize` が wchar_t 単位かバイト単位かが確定していない
- 改善: native 側 bufferSize の単位（文字数 or バイト数）を確認し、計算式の根拠を契約として明記する

### 低優先度

なし

## 不足項目

- IL2CPP / AOT 制約への言及（`[MonoPInvokeCallback]` 属性・static delegate GC 防止）が 2章の事実記述のみで 4章の実装設計に落ちていない
- `#if UNITY_STANDALONE_WIN` を全新規ファイルに適用する旨が変更ファイル一覧・実装詳細に明示されていない
- `Application.platform != RuntimePlatform.WindowsPlayer` 時の early return 方針が未記載
- `DLL_NAME` の `DEVELOPMENT_BUILD` 切り替えを `#if` でどう実装するかの具体策がない
- `OnDestroy` で `uninitNotificationManager()` を呼ぶ際の二重解放・未初期化状態の安全性検討
- Operation 定数の一覧定義
- `WindowsNotificationResult.Success/Failure` ファクトリの具体定義
- errorCode（int 数値）から ErrorMessage（人間可読文字列）へのマッピング方針
- XML ドキュメントコメント・全 public メソッド先頭の `Debug.Log` 付与という `csharp.md` 必須ルールへの準拠言及

## 総合評価

native-toolkit の API 確認（1章）は関数・エラーコード・列挙値・JSON スキーマまで網羅されており調査品質は高水準。DllImport マーシャリングとメモリ/スレッド契約も概ね妥当。最大の弱点は「既存 Manager パターンを 2章で把握しているのに 4章の実装詳細へ落とし込めていない」点で、Operation 定数・per-call onResult・プラットフォームガード・errorCode→ErrorMessage マッピングといった必須構成要素が欠落している。総じて「調査フェーズは合格、設計フェーズが未成熟」であり、high 指摘 3件を解消したうえで実装着手することを推奨する。
