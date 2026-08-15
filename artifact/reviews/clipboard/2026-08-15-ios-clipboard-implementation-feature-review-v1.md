# iOS Clipboard 実装レビュー v1

## レビュー概要

- 日付: 2026-08-15
- 機能名: clipboard
- 対象プラットフォーム: iOS
- ブランチ: `feature/UNT-9`
- レビュー対象差分: `develop...HEAD` は空のため、ユーザー承認のもとローカル未コミット差分（未追跡ファイルを含む）を対象
- 実装計画: `artifact/designs/clipboard/2026-08-15-ios-clipboard-design-v5.md`
- 実装結果: `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v1.md`
- 総合評価: 要修正（重大）

15 操作の Manager + P/Invoke Bridge、`[MarshalAs(UnmanagedType.I1)]`、single-flight、lifetime tombstone、JSON 型、EditMode / PlayMode テストの基本構造は実装されている。1.3.0 の device binaryには `clipboardCopy` から `clipboardCheckForegroundChange` までの15シンボルが存在することも `nm` で確認した。

一方、XCFramework の Unity import metadata が完成しておらず、必須の iOS build / link smoke test も未実施である。また、malformed JSON 契約と、single-flight / lifetime / observation の最重要回帰テストが計画どおり固定されていない。

## 重大な問題（high）

### 1. 1.3.0 XCFramework の `.meta` が PluginImporter 設定を持たず、iOS import / link が確定していない

- 対象:
  - `Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/ios-native-toolkit-1.3.0.xcframework.meta:1`
  - `Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/unity-ios-native-toolkit-1.3.0.xcframework.meta:1`
  - `Packages/com.jonghyunkim.nativetoolkit/Editor/Build/PreBuildProcessor.cs:700`
  - `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v1.md:245`
- 新しい `.meta` は `fileFormatVersion` と `guid` の2行だけで、旧 1.2.0 の `.meta` に存在した `PluginImporter`、`Any.enabled: 0`、`iOS.enabled: 1` がない。
- 既存の `ConfigureIosXcframeworkImporter` は `AssetImporter.GetAtPath(...) as PluginImporter` を前提にしている。現在の `.meta` ではその前提を成果物から確認できない。
- 実装結果自身も DoD 2 の M-25 を `×` としている。既存の `Build/iOS` は 2026-07-05 の 1.2.0 参照のままであり、1.3.0 の export / link 証跡にはならない。
- この状態では DoD 1「Unity に import」、DoD 2「iOS build / link smoke test」を達成済みと判定できない。

修正方針:

- Unity の正規 import / `ConfigureIosXcframeworkImporter` 経路で両 `.meta` を再生成し、iOS のみ有効な `PluginImporter` 設定を保存する。
- 1.3.0 を参照する Unity iOS export を新規生成し、Xcode link smoke test（M-25）を実行する。
- `project.pbxproj` が両 1.3.0 XCFramework を Frameworks / Embed Frameworks に含むことも結果レポートへ記録する。

## 改善提案（medium）

### 1. 最小 JSON reader が不正な escape・制御文字・number 文法を受理し、E-2 / B-6 契約を破る

- 対象:
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonReader.cs:542`
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonReader.cs:571`
  - `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosClipboardJsonReaderTests.cs:26`
- `ParseString` は `\` を見つけると後続1文字を無条件に飛ばすため、JSON に存在しない `\q`、不完全な `\u12`、文字列中の raw 制御文字を受理する。`Unescape` は未知 escape を通常文字へ変換するため、必須 string が成功値として扱われうる。
- `ParseNumber` は先頭 `+`、任意位置の `+` / `-` / `.` / `e` を許すため、JSON number 文法より広い入力を parse 成功にする。
- 設計 5.5.1 の E-2 は「JSON として parse 不能」を B-6 に倒す契約であり、reader のクラスコメントも malformed document を拒否するとしている。

修正方針:

- string scan 時に許可 escape、`\u` の4桁16進、raw U+0000〜U+001F を検証する。
- number を `-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?` 相当の状態遷移で検証する。
- `\q`、不完全 `\u`、raw newline、`+1`、`01`、`1.`、`1e` を malformed test に追加する。

### 2. single-flight / lifetime / observation のテストが production の重要経路を固定していない

- 対象:
  - `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/IosClipboardManagerDispatchTests.cs:152`
  - `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardManagerIntegrationTests.cs:299`
  - `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/IosClipboardManagerIntegrationTests.cs:328`
  - `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v1.md:214`
- out-of-order テストはローカル変数の slot と `HashSet` の状態モデルだけを操作し、production の `DispatchRejectedResult`、per-call static slot、`s_inFlight` を通らない。このため、設計レビューで問題になった「busy を通常 Fire 経路へ流して pending callback と marker を奪う」回帰を検出できない。
- observation generation の `ReleaseChangeRegistrationIfOwned` と全完了経路での `s_pendingObservationGeneration` clear を直接検証するテストがない。
- `AfterDestroy_EveryOperationIsRejected` は名前に反して `Clear` / `Read` の2操作しか確認していない。
- `AfterDestroy_PendingCallbacksAndInFlightStateAreCleared` の `Read` は Editor では B-1 で pending 所有前に拒否される。さらに callback 内の `Assert.Fail` は production の `InvokeInOrder` に catch され、`LogAssert.ignoreFailingMessages = true` によってテスト失敗にならないため、現在のテストは callback が実際に届いても pass しうる。
- 設計 7.2 / DoD 7・14〜16・22・23 が要求する rejected 不変条件、late callback、reset 全状態の固定には不足している。

修正方針:

- native 呼び出しと callback 到着を制御できる Editor-only seam、または production と同じ state transition helper を追加する。
- B-0 / B-3 / B-1 / B-9 rejected が pending slot / in-flight を変更しないこと、1本目の結果が1本目へ届くこと、拒否後も3本目が busy になることを production helper 経由で検証する。
- observation generation、全15操作の B-11、late result / change callback discard、`ResetCore` の全 mutable static を検証する。
- callback 内の assertion を production が catch するテストでは、callback count / captured result を外側で assertion する。

### 3. main-thread dispatch 経路も plain reference null 判定を使い、破棄済み dispatcher を見分けられない

- 対象:
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardManager.cs:517`
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardManager.cs:705`
- plain reference の `(object?)dispatcher == null` が必要なのは off-thread の B-9 経路だけである。`DispatchRejectedResult` と通常 `Dispatch` は main thread 上で呼ばれるため、設計 5.6.1 の所有表どおり Unity の `dispatcher == null` で破棄済み wrapper も検出できる。
- 現状では dispatcher GameObject が外部から破棄された場合、managed wrapper は非 null のまま `Enqueue` できても `Update` が再実行されず、結果が永久に配送されない可能性がある。

修正方針:

- main-thread の `DispatchRejectedResult` / `Dispatch` は Unity null 判定を使用する。
- off-thread の `DispatchOffThreadRejection` だけ plain reference 判定を維持する。
- dispatcher 破棄時の挙動を PlayMode テストで固定する。

### 4. snapshot の配列型不一致を空配列として成功に変換している

- 対象: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardJsonParser.cs:143`
- `allTypeIdentifiers` の要素が配列でなくても `ReadStringList` が空配列へ変換し、必須 schema の破損を成功にする。
- `matchingItemIndexes` が非 null の scalar / object でも `AsArray()` が空を返すため、「未指定」の null ではなく「指定したが該当なし」の空配列に変換される。
- 前者は E-11、後者は null / empty を区別する公開契約、および実装結果 A-7 の「任意フィールドの型不一致は null 扱い」と整合しない。

修正方針:

- `allTypeIdentifiers` の各要素が array であることを必須検証する。
- `matchingItemIndexes` は absent / null のみ null、array のみ値として受理し、その他の型を null に寄せるか B-6 とする方針を統一する。
- nested type mismatch と optional type mismatch のテストを追加する。

## 軽微な指摘（low）

### 1. 実装結果が「復元済み」とする solution 差分が残っている

- 対象:
  - `unity-native-plugin.slnx:4`
  - `artifact/results/clipboard/2026-08-15-ios-clipboard-implementation-feature-result-v1.md:104`
- `NativeToolkit.Runtime.Tests.csproj` の行移動が未コミット差分として残っている。機能には影響しないが、結果レポートの「差分なし」と不一致である。
- 不要なら復元し、残すなら変更ファイル一覧へ記録する。

### 2. `csharp.md` のメソッド先頭ログ規則に対する例外説明が公開 factory 全体では不足している

- 対象例:
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardPayloads.cs:46`
  - `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/IosClipboardOperationResult.cs:30`
- Builder / Parser / Manager には機微値を出さない明示的な逸脱説明がある一方、payload / result の public factory はログなしで、同じ説明がない。
- 値そのものをログへ追加するべきではない。shape-only logging を行うか、セキュリティ優先でログ対象外とする理由をクラスコメントへ明記する。
- 由来: `agent-rules/coding-rules/csharp.md` の「public / internal メソッドの先頭ログ」規則。

## 実装計画整合性チェック

- Manager + Bridge パターン準拠: ○
- 変更ファイル一覧との一致: △（`AssemblyInfo.cs` の意図的逸脱、`slnx` の未記録差分）
- テスト方針の網羅性: ×（single-flight / lifetime / observation の必須状態遷移が未固定）
- エラーケース全実装: △（malformed JSON の一部を受理）
- 返却仕様との整合: △（snapshot の型不一致を成功値へ変換）

## プロジェクトルール適合チェック

- `common.md` 準拠: △（Manager + Bridge と dispatcher dispatch は準拠するが、重要 dispatch 状態のテスト分離が不足）
- `csharp.md` 準拠: △（XML コメントは概ね準拠。公開 factory のログ例外説明が不足）
- Bridge 実装品質（スレッド安全性・メモリ管理）: △（ABI、callback static 化、base64 上限は良好。dispatcher 生存判定と実機 link が未確定）
- 既存 API 互換性: ○（既存 Android API の変更なし。`AssemblyInfo.cs` は friend assembly の追加のみ）

## テストカバレッジ

カバー済み:

- JSON builder 13 種、scope / options / content の主要形状
- envelope E-1〜E-16 の代表ケース、base64 padding と上限境界
- result 型の success / failure payload 不変条件
- `InvokeInOrder` の順序と例外分離
- Editor での15操作 B-1、B-3 / B-4、B-9 の基本経路
- cleanup helper の stop / cancel / managedCleanup 例外分離
- 1.3.0 device binary の15個の clipboard C symbol（静的確認）

不足:

- malformed escape / raw control character /厳密な JSON number 文法
- snapshot nested array / optional field の型不一致
- production rejected dispatch の不変条件1〜4
- observation generation の所有権と全 clear 経路
- 全15操作の B-11
- pending callback / in-flight / generation を含む `ResetCore` の完全確認
- late native result / change callback の discard を含む複合 lifetime シナリオ
- dispatcher 破棄時の挙動
- B-2 の到達確認
- M-1〜M-25 の実機確認、特に 1.3.0 の iOS build / link（M-25）と ABI 成否両系統（M-21）

実装結果では EditMode 345 / 345、PlayMode 28 passed・11 skipped・0 failed と報告されている。本レビューでは Unity Test Runner は再実行していない。補助的に `dotnet build --no-restore` を試したが、`Temp/obj/.../project.assets.json` が存在しないためビルド開始前に停止しており、コード失敗とは判定していない。

## 総合評価

要修正（重大）。

Manager / Bridge の中核実装は概ね設計に沿っているが、1.3.0 XCFramework の Unity import と link を保証する成果物・検証が欠けているため、現状では iOS 機能として受け入れられない。まず PluginImporter metadata と M-25 を解消し、その後に JSON strictness と single-flight / lifetime / observation の production 経路テストを追加する必要がある。
