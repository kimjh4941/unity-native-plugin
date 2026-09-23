# Windows Clipboard スパイク検証結果 v1

- 日付: 2026-09-05
- 対象: `artifact/features/clipboard/designs/2026-09-05-windows-clipboard-design-v3.md` の V-1 / V-2 / V-3
- 目的: Unity Windows Player 上で「所有 UI スレッド = Unity メインスレッド」という設計前提が成立するかの検証
- 種別: 使い捨てスパイク（実装ではない）

## 実行環境

| 項目 | 値 |
|---|---|
| Unity | 6000.4.2f1 |
| OS | Windows 11 (10.0.26200) |
| Scripting Backend | Mono（Standalone 既定） |
| ビルド種別 | Development Build（`BuildOptions.Development`） |
| ネイティブ DLL | `windows-native-toolkit-1.2.0.dll` → `unity-windows-native-toolkit-debug.dll`（`PreBuildProcessor` が dist 1.11.0 から配置） |
| クリップボード履歴 | 有効（`historyEnabled: true` / `roamingEnabled: false`） |

スパイク用コード（検証後に削除）:

- `Assets/Scripts/Runtime/Spike/WindowsClipboardApartmentProbe.cs`
- `Assets/Editor/WindowsClipboardSpikeBuild.cs`

## 結果ログ（抜粋）

```
[SPIKE] ===== BEGIN ===== unity=6000.4.2f1 threadId=1
[SPIKE] V1 CoGetApartmentType hr=0x00000000 aptType=MAINSTA(3) qualifier=0
[SPIKE] V1 RESULT isSta=True comOwnership=None
[SPIKE] V1 initClipboardManager pError=0 (NONE)
[SPIKE] V2 getClipboardHistoryAvailability requestId=1 pError=0 (NONE)
[SPIKE] V2 CALLBACK request=1 error=0 (NONE) onMainThread=True json={"historyEnabled":true,"roamingEnabled":false}
[SPIKE] PHASE A start t=5.05 count=0 - waiting for an EXTERNAL write (up to 20s)
[SPIKE] V2 CALLBACK ClipboardChanged #1 t=5.25 onMainThread=True
[SPIKE] V2 CALLBACK ClipboardChanged #2 t=5.33 onMainThread=True
[SPIKE] V2 CALLBACK ClipboardChanged #3 t=5.46 onMainThread=True
[SPIKE] PHASE A RESULT delta=3 (expected >= 1)
[SPIKE] PHASE B start t=7.25 count=3 - self copy, options=NONE
[SPIKE] PHASE B RESULT delta=0 (0 = self-write suppressed)
[SPIKE] SYNC pastePlainText(size) needed=29 pError=7 (BUFFER_TOO_SMALL)
[SPIKE] SYNC pastePlainText(read) written=29 pError=0 match=True
[SPIKE] PHASE C start t=13.28 count=3 - self copy, options=EXCLUDE_HISTORY
[SPIKE] PHASE C RESULT delta=0
[SPIKE] V2 SUMMARY totalChanged=3 deltaB=0 deltaC=0
[SPIKE] SHUTDOWN attempt=1 completed=True pError=0 (NONE)
[SPIKE] ===== END =====
```

時刻の対応（Player ログには絶対時刻が無いため実測で補正）:

- プロセス開始: 14:25:16.78
- 外部書き込み（PowerShell `Set-Clipboard`）: 14:25:33.08
- ログ最終書き込み: 14:25:45.26（`===== END =====` は `t≈17.4`）
- → **エンジン `t=0` ≈ 14:25:27.9（起動オーバーヘッド約 11.1 秒）**
- → 外部書き込みは `t≈5.18`。コールバック 3 件（`t=5.25 / 5.33 / 5.46`）は**この外部書き込みによるもの**と確定

## 判定

| # | 項目 | 結果 | 内容 |
|---|---|---|---|
| V-1 | Unity メインスレッドの COM アパートメント | **合格** | `CoGetApartmentType` = `APTTYPE_MAINSTA`(3)。**初期化済み STA**。`CoInitializeEx` フォールバックは不要（`comOwnership=None`）。`initClipboardManager` は `pError=0` |
| V-2 | メッセージポンプの配送 | **合格** | 非同期リクエストが受付（`requestId=1`）→ WinRT continuation → **Unity メインスレッド**で完了。`ClipboardChanged` も同様にメインスレッドで到達。Player のポンプがネイティブの隠しウィンドウのメッセージを配送している |
| V-3 | 終了時ドレイン | **部分合格** | 予約なしの状態で `uninitClipboardManager` は **1 回目で TRUE**。予約あり（`reserveDeferredFormats`）での終了は未検証 |
| — | 2 回呼び出しバッファ規約 | **合格** | 1 回目 `needed=29` + `BUFFER_TOO_SMALL`(7)、2 回目 `written=29` + `NONE`、内容一致 |
| — | self-write 抑止 | **合格** | 自プロセスの `copyPlainText` では通知 0 件。`options=NONE`（履歴に載る）でも `EXCLUDE_HISTORY` でも 0 件。設計 2.2 の記述は正しい |

## 新たに判明した事項（設計へ反映が必要）

### F-1: `ClipboardChanged` は 1 回の外部書き込みで複数回発火する

PowerShell の `Set-Clipboard` 1 回に対して **3 回**発火した。複数フォーマットの配置やクリップボード履歴サービスの再書き込みで
`GetClipboardSequenceNumber` が複数回進むため。ネイティブの抑止はシーケンス番号単位であり、これは仕様どおりの挙動。

- **`ClipboardChanged` はユーザー操作 1 回と 1:1 ではない。** 購読側は冪等に扱うか debounce する必要がある
- サンプルシーンで「変更回数」を表示すると実際のコピー回数と一致しない

### F-2: Windows の DLL 名はビルド構成でリネームされる（設計 v3 の 5.1 は誤り）

`Packages/com.jonghyunkim.nativetoolkit/Editor/Build/PreBuildProcessor.cs` の `CopyWindowsLibraries` が、ビルドのたびに
`native-toolkit/dist/<version>/windows` から DLL をコピーし、**構成に応じてリネーム**する。

```
[Build][Windows] Copying libraries from dist (config=Debug, version=1.11.0)
[Build][Windows] No -debug.dll published for prefix=windows-native-toolkit-; falling back to the release DLL: windows-native-toolkit-1.2.0.dll
[Build][Windows] Copied windows-native-toolkit-1.2.0.dll → unity-windows-native-toolkit-debug.dll
```

| ビルド構成 | 配置される名前 |
|---|---|
| Development（`config=Debug`） | `unity-windows-native-toolkit-debug.dll` |
| Release | `unity-windows-native-toolkit.dll` |

dist に `-debug.dll` が無くてもリリース DLL を debug 名で配置するため、**`DEVELOPMENT_BUILD` による DLL 名分岐は必須**。
本スパイクは最初この分岐を持たずに実装したため `DllNotFoundException` で失敗した（実証済み）。

- 設計 v3 の 5.1「単一 DLL 名とする」は撤回する
- review-v1 の V-6（「debug 版 DLL は存在しないので分岐不要」）も撤回する。dist の中身だけを見た誤った結論だった
- 既存 `WindowsNotificationManager` の `DEVELOPMENT_BUILD` 分岐は**正しい実装**であり、是正候補ではない

### F-3: `Plugins/Windows` の DLL は手動管理ではない

DLL はビルド時に `PreBuildProcessor` が dist から自動配置し、`ConfigureWindowsPluginImporter` が meta も設定する。
`VERSION.txt` は自動更新されない情報ファイル。設計 v3 の 2.7 / 6.3（手動差し替えと VERSION.txt 更新を作業項目とする）は前提を修正する。

## 副作用（検証ビルドによる作業ツリーの変化）

| ファイル | 変化 | 対応 |
|---|---|---|
| `Plugins/Windows/unity-windows-native-toolkit.dll` | 削除され `-debug.dll` が生成された | リリースビルドで元に戻る。コミット前に要確認 |
| `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` | ビルドにより変更 | 内容を確認して破棄可 |
| `Assets/Scripts/Runtime/Spike/`、`Assets/Editor/` | スパイク用に追加 | 検証完了後に削除 |

## 残る未検証項目

- **V-3（予約ありの終了）**: `reserveDeferredFormats` 実行後にアプリを終了し、`WM_RENDERALLFORMATS` で provider が呼ばれて形式が実体化されるか。
  `uninit` 内の `DestroyWindow` から provider が同期再入する経路（設計 2.8 の D-7）も未実証
- **IL2CPP**: 本スパイクは Mono。`MonoPInvokeCallback` の実挙動は IL2CPP ビルドで別途確認する
- 非フォアグラウンド時の履歴 API（`NOT_FOREGROUND`）、履歴無効時の挙動
