# Windows Clipboard 実装計画書 v1

- 作成日: 2026-09-05
- 対象機能: clipboard
- 対象プラットフォーム: Windows（Windows 11 以降）
- 対象リポジトリ: unity-native-plugin（C# 層のみ）
- 対象外: サンプルアプリ（ExampleController / UXML / USS / サンプルシーン）。`design-sample-scene` で別途設計する

---

## 1. 概要

native-toolkit の `WindowsLibrary.dll` はすでに Clipboard の `extern "C"` C Bridge（26 関数）を公開している。
本計画は、その C API を Unity C# から P/Invoke する **Bridge + Manager 層のみ**を対象とする。
ネイティブ側の再実装は行わない。

前提となる重要事実（ステップ3 の確認結果、詳細は 2 章）:

- **同梱 DLL は Clipboard 対応版へ差し替え済み**（2026-09-05 確認）。`Packages/.../Plugins/Windows/unity-windows-native-toolkit.dll` は
  `windows-native-toolkit-1.2.0.dll`（457,728 bytes）で、Clipboard の 27 export がすべて含まれている。
  残作業は `VERSION.txt` の `source:` 行のみ（2.7）。
- Clipboard は **OS の WinRT（`Windows.ApplicationModel.DataTransfer`）のみ**を使い、Windows App SDK に依存しない。
  したがって Clipboard 単体利用では `initWinAppSdk` の呼び出しは不要。
- ネイティブ側は **所有 UI スレッド（`initClipboardManager` を呼んだスレッド）** を採用する契約であり、
  そのスレッドは **STA 初期化済み** かつ **メッセージポンプを回し続ける**必要がある。
  Unity の Windows Player メインスレッドがこの条件（特に STA）を満たすかは **要検証**（10 章 V-1）。

---

## 2. native-toolkit 確認結果（ネイティブ側の既存実装）

参照: `C:\Users\User\Desktop\native-toolkit\windows\WindowsLibrary`
（`WindowsClipboardManager.h` / `WindowsClipboardManager.cpp` / `WindowsClipboardHistoryCoordinator.h` /
`WindowsClipboardHistoryWinRt.cpp` / `WindowsLibrary.def`）
補助資料: `native-toolkit/artifact/designs/clipboard/2026-07-28-windows-clipboard-design-v2.md`

### 2.1 公開関数一覧（`WindowsLibrary.def` の EXPORTS）

| 種別 | 関数 | シグネチャ（C） | 同期/非同期 | スレッド要件 |
|---|---|---|---|---|
| ライフサイクル | `initClipboardManager` | `void(ClipboardChangedCallback, DWORD* pError)` | 同期 | 呼び出しスレッドを所有 UI スレッドとして採用。STA 必須 |
| ライフサイクル | `setClipboardHistoryCallbacks` | `void(ClipboardHistoryChangedCallback, ClipboardFlagChangedCallback, ClipboardFlagChangedCallback, DWORD*)` | 同期 | **所有 UI スレッド限定**。コールバック内から呼ぶのは禁止 |
| ライフサイクル | `uninitClipboardManager` | `BOOL(DWORD* pError)` | 同期（複数回呼ぶ前提） | **所有 UI スレッド限定**。コールバック内から呼ぶのは禁止 |
| ライフサイクル | `canDestroyClipboardManager` | `BOOL(DWORD* pError)` | 同期（非ブロッキング状態問い合わせ） | 制限なし |
| 書き込み | `copyPlainText` | `void(const wchar_t* text, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyHtml` | `void(const wchar_t* htmlFragment, const wchar_t* plainText, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyFiles` | `void(const wchar_t* pathsJson, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyImage` | `void(const BYTE* dib, DWORD dibSize, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyCustomFormat` | `void(const wchar_t* formatName, const BYTE* data, DWORD size, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 書き込み | `copyMultipleFormats` | `void(const wchar_t* itemsJson, DWORD options, DWORD*)` | 同期 | 任意スレッド |
| 読み出し | `pastePlainText` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 読み出し | `pasteHtml` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 読み出し | `pasteFiles` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)`（JSON 配列文字列） | 同期（2 回呼び出し） | 任意スレッド |
| 読み出し | `pasteImage` | `DWORD(BYTE* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 読み出し | `pasteCustomFormat` | `DWORD(const wchar_t* formatName, BYTE* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 内容確認 | `hasClipboardFormat` | `BOOL(const wchar_t* formatName, DWORD*)` | 同期 | 任意スレッド |
| 内容確認 | `getClipboardFormats` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)`（JSON 配列文字列） | 同期（2 回呼び出し） | 任意スレッド |
| 内容確認 | `getPreferredClipboardFormat` | `DWORD(wchar_t* buffer, DWORD buffer_size, DWORD*)` | 同期（2 回呼び出し） | 任意スレッド |
| 消去 | `clearClipboard` | `void(DWORD*)` | 同期 | 任意スレッド |
| 遅延配置 | `reserveDeferredFormats` | `void(const wchar_t* formatNamesJson, ClipboardRenderCallback, void* context, DWORD*)` | 同期 | **所有 UI スレッド限定** |
| 遅延配置 | `recoverDeferredState` | `void(DWORD*)` | 同期 | **所有 UI スレッド限定** |
| 履歴 | `getClipboardHistory` | `uint32_t(ClipboardRequestCallback, DWORD*)` | 非同期（requestId 返却） | 任意スレッド |
| 履歴 | `restoreHistoryItem` | `uint32_t(const wchar_t* itemId, ClipboardRequestCallback, DWORD*)` | 非同期 | 任意スレッド |
| 履歴 | `deleteHistoryItem` | `uint32_t(const wchar_t* itemId, ClipboardRequestCallback, DWORD*)` | 非同期 | 任意スレッド |
| 履歴 | `clearUnpinnedHistory` | `uint32_t(ClipboardRequestCallback, DWORD*)` | 非同期 | 任意スレッド |
| 履歴 | `getClipboardHistoryAvailability` | `uint32_t(ClipboardRequestCallback, DWORD*)` | 非同期 | 任意スレッド |
| 履歴 | `cancelClipboardRequest` | `BOOL(uint32_t requestId, DWORD*)` | 同期（キャンセル投入のみ） | 任意スレッド |

`getNotificationSetting` 等の Notification 系 export は本計画の対象外。

### 2.2 コールバック型

| 型 | シグネチャ | 実行スレッド | 契約 |
|---|---|---|---|
| `ClipboardChangedCallback` | `void(void)` | 所有 UI スレッド | クリップボード内容変更時。自プロセスの書き込みは通知されない（native 側で self-write 抑止） |
| `ClipboardHistoryChangedCallback` | `void(void)` | 所有 UI スレッド | **新規項目追加時のみ**。削除 / ClearHistory では発火しない |
| `ClipboardFlagChangedCallback` | `void(BOOL enabled)` | 所有 UI スレッド | 履歴 / ローミング設定変更。**信頼できない**（プロセス中 1 回しか発火しない、履歴無効時の登録では発火しないことを native 側が実測済み） |
| `ClipboardRequestCallback` | `void(uint32_t requestId, DWORD error, const wchar_t* json)` | 所有 UI スレッド | 受付済みリクエストごとに**ちょうど 1 回**。`json` は**コールバック中のみ有効**（呼び出し側でコピー必須） |
| `ClipboardRenderCallback` | `DWORD(const wchar_t* formatName, void* context, BYTE* buffer, DWORD buffer_size, DWORD* pRequiredSize)` | 所有 UI スレッド（`WM_RENDERFORMAT` 処理中） | クリップボード API を呼ばない / ブロックしない / 例外を出さない。1 回目は `buffer == nullptr` でサイズ問い合わせ、2 回目に書き込み |

- **完了コールバックは inline / reentrant に呼ばれない**（必ず `PostMessage` 経由）。
- コールバック内から `uninitClipboardManager` / `setClipboardHistoryCallbacks` を呼ぶことは禁止。

### 2.3 定数

エラーコード（`CLIPBOARD_ERROR_*`、0 = 成功、非成功 1〜19）:

| 値 | 定数 | 意味（ネイティブ設計書の英語メッセージ） |
|---|---|---|
| 0 | `NONE` | 成功 |
| 1 | `INVALID_PARAMETER` | `Invalid parameter` |
| 2 | `NOT_INITIALIZED` | `Clipboard manager is not initialized` |
| 3 | `BUSY` | `Clipboard is held by another process` |
| 4 | `EMPTY` | `Clipboard is empty` |
| 5 | `FORMAT_UNAVAILABLE` | `Requested format is not available` |
| 6 | `INVALID_DATA` | `Clipboard data failed validation` |
| 7 | `BUFFER_TOO_SMALL` | `Buffer too small; required size returned` |
| 8 | `OUT_OF_MEMORY` | `Out of memory` |
| 9 | `ACCESS_DENIED` | `Access to clipboard history is denied` |
| 10 | `HISTORY_DISABLED` | `Clipboard history is disabled` |
| 11 | `ITEM_DELETED` | `History item was already deleted` |
| 12 | `MONITOR_REGISTER_FAILED` | `Failed to register or unregister the clipboard listener` |
| 13 | `PARTIAL_STATE` | `Rollback failed; clipboard may hold partial content` |
| 14 | `WRONG_THREAD` | `API must be called from the owner UI thread` |
| 15 | `CANCELED` | `Request was canceled` |
| 16 | `NOT_SUPPORTED` | `Operation is not supported on Windows` |
| 17 | `NOT_FOREGROUND` | `App is not in the foreground` |
| 18 | `WRONG_APARTMENT` | `Calling thread is not an initialized STA` |
| 19 | `UNKNOWN` | `Unexpected failure (raw code logged)` |

書き込みオプション（ビットフラグ）:

| 値 | 定数 | 意味 |
|---|---|---|
| `0x0` | `CLIPBOARD_WRITE_OPTION_NONE` | 既定 |
| `0x1` | `CLIPBOARD_WRITE_OPTION_EXCLUDE_HISTORY` | クリップボード履歴に含めない |
| `0x2` | `CLIPBOARD_WRITE_OPTION_EXCLUDE_ROAMING` | クラウド同期しない |
| `0x3` | `CLIPBOARD_WRITE_OPTION_SENSITIVE` | 上記両方 |

### 2.4 JSON スキーマ（ネイティブ境界）

| 用途 | 方向 | スキーマ |
|---|---|---|
| `copyFiles` | C# → native | `["C:\\a.txt","C:\\b.png"]` |
| `pasteFiles` | native → C# | 同上 |
| `getClipboardFormats` | native → C# | 形式名の JSON 配列。名前解決できない形式は `0x%04X` 表記（例 `"0xC0F1"`） |
| `copyMultipleFormats` | C# → native | `[{"format":"CF_UNICODETEXT","text":"..."},{"format":"HTML Format","html":"..."},{"format":"CF_DIB","base64":"..."}]`。richest first。1 要素につきペイロードキーは 1 つのみ。重複 `format` と型不一致は配置前に `INVALID_PARAMETER` |
| `reserveDeferredFormats` | C# → native | `["CF_UNICODETEXT","MyApp Format"]` |
| `getClipboardHistory` | native → C# | `[{"id":"...","text":"..."\|null,"contentTypes":["Text","Bitmap"],"timestamp":"<int64 の 10 進文字列>"}]`。`timestamp` は 1601 基点の 100ns FILETIME ティック（double 精度で失われるため文字列） |
| `getClipboardHistoryAvailability` | native → C# | `{"historyEnabled":true,"roamingEnabled":false}` |

### 2.5 受付（acceptance）契約 — 非同期 API

| 段階 | 失敗内容 | 通知 | コールバック回数 |
|---|---|---|---|
| 受付成立前 | 引数不正 / 未初期化 / gate クローズ / `PostMessage` 失敗 | 戻り値の requestId `0` + `pError` | **0 回** |
| 受付成立後 | フォアグラウンド不成立 / 履歴無効 / WinRT 失敗 / 成功 / キャンセル / `uninit` ドレイン | コールバック | **ちょうど 1 回** |

- `NOT_FOREGROUND` は受付後にコールバックで返る（同期戻り値では返らない）。
- `cancelClipboardRequest` が `FALSE` を返しても、すでに配送中の完了は取り消されない。

### 2.6 終了（shutdown）契約

- `uninitClipboardManager` は **TRUE を返すまで繰り返し呼ぶ**。FALSE の間はメッセージポンプを回し続ける必要がある。
- **プロセス終了前に必ず呼ぶ。** `reserveDeferredFormats` で予約した形式は `WM_RENDERALLFORMATS`（所有ウィンドウ破棄時のみ OS が送る）で実体化されるため、
  呼ばずに終了すると予約形式はクリップボードから失われる。
- `canDestroyClipboardManager` は「次の uninit が成功する」保証ではなく状態問い合わせのみ。

### 2.7 同梱 DLL の状況

| 項目 | 現状（2026-09-05 確認） | 必要な対応 |
|---|---|---|
| `Packages/.../Plugins/Windows/unity-windows-native-toolkit.dll` | **差し替え済み**。`windows-native-toolkit-1.2.0.dll` 相当（457,728 bytes）。Clipboard の 27 export をすべて確認 | なし |
| `Packages/.../Plugins/Windows/VERSION.txt` | バージョン行は `1.2.0` に更新済み。ただし `source:` 行が `dist/1.4.0/.../windows-native-toolkit-1.1.0.dll` のまま**古い** | `source:` 行を `dist/1.11.0/windows/windows-native-toolkit-1.2.0.dll` へ更新する |
| `unity-windows-native-toolkit.dll.meta` | Win64 のみ enabled、Editor は disabled。ファイル名維持のため GUID も維持されている | 変更不要 |
| `Microsoft.WindowsAppRuntime.Bootstrap.dll` | 同梱済み | 変更不要（Clipboard は WinAppSDK 非依存） |
| debug ビルド（`unity-windows-native-toolkit-debug.dll`） | **存在しない**。`native-toolkit/dist` 配下のどのバージョンにも debug 版は無い | 5.1 の `DLL_NAME` 方針を参照（`DEVELOPMENT_BUILD` で debug 名に切り替えない） |

---

## 3. 既存 C# 実装の確認結果

参照: `Packages/com.jonghyunkim.nativetoolkit/Runtime/`

### 3.1 Windows の P/Invoke パターン（`Notification/WindowsNotificationManager.cs`）

踏襲する点:

- コンパイルガード `#if UNITY_STANDALONE_WIN || UNITY_EDITOR`（Editor でもコンパイルされる A 群）
- `[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]` + `[MarshalAs(UnmanagedType.LPWStr)] string`、`out int pError`
- コールバック delegate は `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`、実体は `[MonoPInvokeCallback]` の **static** メソッド
- delegate の GC ルート: `private static readonly XxxCallback s_persistentDelegate = OnXxx;`
- 2 回呼び出しバッファ: `Marshal.AllocHGlobal(size * 2)` → `Marshal.PtrToStringUni` → `finally` で `FreeHGlobal`
- Singleton（`Instance` プロパティ + `Awake` での `_instance` 確定 + `DontDestroyOnLoad` + `_ = UnityMainThreadDispatcher.Instance`）
- 結果型 `WindowsNotificationResult`（`Operation` / `IsSuccess` / `ErrorCode` / `ErrorMessage`、`ErrorCodeToMessage` で文字列化）
- `OnDestroy` で native の uninit を呼ぶ

改める点（本設計で明示的に変える）:

- `WindowsNotificationManager` は非 Windows Player で **何も返さず無言で return** している（per-call callback も event も発火しない）。
  Clipboard では iOS / Android と同様に**失敗結果を返して event と callback を必ず発火**させる（`Awaitable` のハング防止のため必須）。
- `WindowsNotificationManager` の DLL 名定数は `DEVELOPMENT_BUILD` 時に `unity-windows-native-toolkit-debug` へ切り替えるが、**その DLL は配布物に存在しない**（2.7）。
  Clipboard ではこの分岐を持たず、常に `unity-windows-native-toolkit` を参照する（5.1）。

### 3.2 Clipboard の Manager パターン（`Clipboard/IosClipboardManager.cs` / `AndroidClipboardManager.cs`）

踏襲する点:

- 共通 event（`event Action<TResult>?`）+ 任意の per-call callback（`Action<TResult>? onResult = null`）の二本立て
- dispatch 順序は **共通 event → 個別 callback**（`InvokeInOrder`）。dispatch helper は `internal static` の純粋関数として切り出し、EditMode で検証
- 拒否時（プラットフォーム外 / 破棄済み / 引数不正）も結果を必ず配送する
- 操作名の `public const string Operation*` 定数
- `UnityMainThreadDispatcher` 経由の配送（`Dispatch` helper で dispatcher の null チェック込み）
- **機微情報のログ規約**: クリップボード内容（パスワード等を含み得る）を扱うメソッドでは
  csharp.md の「全 public / internal メソッドの先頭でログ」を意図的に外し、内容を出さないサマリのみ記録する。
  JsonBuilder / JsonParser も同様（`AndroidClipboardJsonBuilder` / `AndroidClipboardJsonParser` に前例あり）
- JSON は `XxxJsonBuilder`（手書きシリアライザ）と `XxxJsonParser`（`JsonUtility` + DTO）で担う

再利用しない点:

- `ClipboardOperationResult` / `ClipboardReadResult` / `ClipboardDescriptionResult` は Android の ClipData 概念（label / mimeTypes / items）に強く結びついており、Windows の形式概念と一致しない。**Windows 専用の結果型を新規追加する**
- `IosClipboardJsonReader`（汎用 JSON リーダ）は `#if UNITY_IOS || UNITY_EDITOR` ガード内にあり、`UNITY_STANDALONE_WIN` ではコンパイルされない。
  共有化のリファクタは iOS 実装とそのテストへ波及するため本計画では行わず、Windows 側は `JsonUtility` ベースの軽量パーサを新設する（Android と同じ方式）

### 3.3 テスト構成

- `Tests/Runtime`（asmdef は `includePlatforms: [Editor]`）= EditMode。`InternalsVisibleTo("NativeToolkit.Runtime.Tests")` により internal 型を検証可能
- `Tests/PlayMode` = Editor 内 PlayMode。dispatcher の flush が必要な経路と非実機フォールバックを検証
- Manager インスタンスを生成するテストは EditMode に置かない（`Awake` / `Initialize` が DllImport に触れるため）

---

## 4. 実装制約（common.md / csharp.md からの固定事項）

- Bridge 層で同期・非同期を変換しない。ネイティブ同期 API は C# も同期メソッド（戻り値を返す）、非同期 API は callback 版を必須とし、
  多重呼び出しガードが成立する場合のみ `Awaitable<T>` 版を併設する
- ネイティブコールバックは `UnityMainThreadDispatcher.Instance.Enqueue` 経由で配送する（例外は 7.7 の遅延レンダリング provider のみ）
- Singleton は `Instance` プロパティ + `Awake` の二重生成防止 + `DontDestroyOnLoad`
- `#if UNITY_STANDALONE_WIN || UNITY_EDITOR` でクラス全体を囲む
- 全 public / internal メソッド先頭に `Debug.Log($"[{LogTag}][{nameof(X)}] ...")`（機微情報を扱うメソッドは 3.2 の規約に従い内容を出さない）
- public 型・メソッドに XML ドキュメントコメント、コメントとユーザー向け文言は英語
- `.meta` ファイルは作成しない
- 最小サポート: Unity 6 / Windows 11

---

## 5. 実装対象 API 一覧（native → C# 対応）

### 5.1 `[DllImport]` 宣言（`WindowsClipboardManager` 内 private static extern）

```csharp
[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void initClipboardManager(ClipboardChangedCallback? onChanged, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void setClipboardHistoryCallbacks(
    ClipboardHistoryChangedCallback? onHistoryChanged,
    ClipboardFlagChangedCallback? onHistoryEnabledChanged,
    ClipboardFlagChangedCallback? onRoamingEnabledChanged,
    out int pError);

[DllImport(DLL_NAME)] [return: MarshalAs(UnmanagedType.Bool)]
private static extern bool uninitClipboardManager(out int pError);

[DllImport(DLL_NAME)] [return: MarshalAs(UnmanagedType.Bool)]
private static extern bool canDestroyClipboardManager(out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void copyPlainText([MarshalAs(UnmanagedType.LPWStr)] string text, uint options, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint pastePlainText(IntPtr buffer, uint bufferSize, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void copyHtml([MarshalAs(UnmanagedType.LPWStr)] string htmlFragment,
                                    [MarshalAs(UnmanagedType.LPWStr)] string? plainText, uint options, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint pasteHtml(IntPtr buffer, uint bufferSize, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void copyFiles([MarshalAs(UnmanagedType.LPWStr)] string pathsJson, uint options, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint pasteFiles(IntPtr buffer, uint bufferSize, out int pError);

[DllImport(DLL_NAME)]
private static extern void copyImage(byte[] dib, uint dibSize, uint options, out int pError);

[DllImport(DLL_NAME)]
private static extern uint pasteImage(IntPtr buffer, uint bufferSize, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void copyCustomFormat([MarshalAs(UnmanagedType.LPWStr)] string formatName,
                                            byte[] data, uint size, uint options, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint pasteCustomFormat([MarshalAs(UnmanagedType.LPWStr)] string formatName,
                                             IntPtr buffer, uint bufferSize, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void copyMultipleFormats([MarshalAs(UnmanagedType.LPWStr)] string itemsJson, uint options, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
private static extern bool hasClipboardFormat([MarshalAs(UnmanagedType.LPWStr)] string formatName, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint getClipboardFormats(IntPtr buffer, uint bufferSize, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint getPreferredClipboardFormat(IntPtr buffer, uint bufferSize, out int pError);

[DllImport(DLL_NAME)]
private static extern void clearClipboard(out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern void reserveDeferredFormats([MarshalAs(UnmanagedType.LPWStr)] string formatNamesJson,
                                                  ClipboardRenderCallback provider, IntPtr context, out int pError);

[DllImport(DLL_NAME)]
private static extern void recoverDeferredState(out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint getClipboardHistory(ClipboardRequestCallback cb, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint restoreHistoryItem([MarshalAs(UnmanagedType.LPWStr)] string itemId,
                                              ClipboardRequestCallback cb, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint deleteHistoryItem([MarshalAs(UnmanagedType.LPWStr)] string itemId,
                                             ClipboardRequestCallback cb, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint clearUnpinnedHistory(ClipboardRequestCallback cb, out int pError);

[DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
private static extern uint getClipboardHistoryAvailability(ClipboardRequestCallback cb, out int pError);

[DllImport(DLL_NAME)] [return: MarshalAs(UnmanagedType.Bool)]
private static extern bool cancelClipboardRequest(uint requestId, out int pError);
```

補足:

- `DLL_NAME` は `private const string DLL_NAME = "unity-windows-native-toolkit";` の**単一定数**とする。
  `WindowsNotificationManager` にある `DEVELOPMENT_BUILD` 時の `-debug` 分岐は採らない。debug 版 DLL は配布物に存在せず（2.7）、
  development build のサンプル実行時に `DllNotFoundException` → `BridgeUnavailable` となり、全 API が使えなくなるため
- 出力バッファは `IntPtr`（`Marshal.AllocHGlobal`）で受ける。`wchar_t` 系の戻り値は **wchar_t 数**（終端含む）、`BYTE` 系は **バイト数**
- `copyImage` / `copyCustomFormat` の入力は `byte[]` を直接マーシャリング（ピン留めは CLR が行う）
- COM アパートメント確認・初期化のために `ole32.dll` の `CoGetApartmentType` / `CoInitializeEx` を追加で P/Invoke する（7.3）

### 5.2 delegate 型（IL2CPP / AOT 安全）

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClipboardChangedCallback();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClipboardHistoryChangedCallback();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClipboardFlagChangedCallback([MarshalAs(UnmanagedType.Bool)] bool enabled);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ClipboardRequestCallback(uint requestId, int error, [MarshalAs(UnmanagedType.LPWStr)] string? json);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint ClipboardRenderCallback([MarshalAs(UnmanagedType.LPWStr)] string formatName, IntPtr context, IntPtr buffer, uint bufferSize, out uint requiredSize);
```

- `json` を `string` でマーシャリングすると CLR が**呼び出し中にコピー**するため、「コールバック中のみ有効」というネイティブ契約を満たす。`null` は `null` として届く
- すべての実体は `static` + `[MonoPInvokeCallback(typeof(...))]`。例外を C ABI 境界へ漏らさないよう全体を `try/catch` で包む

### 5.3 公開 API 対応表

| ネイティブ | C# 公開 API | 形 |
|---|---|---|
| `initClipboardManager` | `WindowsClipboardResult Initialize(bool enableChangeEvents = true, Action<WindowsClipboardResult>? onResult = null)` | 同期 |
| `setClipboardHistoryCallbacks` | `WindowsClipboardResult SetHistoryEventsEnabled(bool enabled, Action<WindowsClipboardResult>? onResult = null)` | 同期 |
| `uninitClipboardManager` / `canDestroyClipboardManager` | `void Shutdown(Action<WindowsClipboardResult>? onResult = null)` / `bool CanShutdownNow()` | 非同期（フレーム跨ぎのリトライ）/ 同期 |
| `copyPlainText` | `WindowsClipboardResult CopyPlainText(string text, WindowsClipboardWriteOptions options = None, Action<WindowsClipboardResult>? onResult = null)` | 同期 |
| `copyHtml` | `WindowsClipboardResult CopyHtml(string htmlFragment, string? plainText = null, ...)` | 同期 |
| `copyFiles` | `WindowsClipboardResult CopyFiles(IReadOnlyList<string> paths, ...)` | 同期 |
| `copyImage` | `WindowsClipboardResult CopyImage(byte[] dib, ...)` | 同期 |
| `copyCustomFormat` | `WindowsClipboardResult CopyCustomFormat(string formatName, byte[] data, ...)` | 同期 |
| `copyMultipleFormats` | `WindowsClipboardResult CopyMultipleFormats(IReadOnlyList<WindowsClipboardFormatPayload> items, ...)` | 同期 |
| `pastePlainText` | `WindowsClipboardTextResult PastePlainText(Action<WindowsClipboardTextResult>? onResult = null)` | 同期 |
| `pasteHtml` | `WindowsClipboardTextResult PasteHtml(...)` | 同期 |
| `pasteFiles` | `WindowsClipboardStringListResult PasteFiles(...)` | 同期 |
| `pasteImage` | `WindowsClipboardBytesResult PasteImage(...)` | 同期 |
| `pasteCustomFormat` | `WindowsClipboardBytesResult PasteCustomFormat(string formatName, ...)` | 同期 |
| `hasClipboardFormat` | `WindowsClipboardFormatPresenceResult HasFormat(string formatName, ...)` | 同期 |
| `getClipboardFormats` | `WindowsClipboardStringListResult GetFormats(...)` | 同期 |
| `getPreferredClipboardFormat` | `WindowsClipboardTextResult GetPreferredFormat(...)` | 同期 |
| `clearClipboard` | `WindowsClipboardResult Clear(...)` | 同期 |
| `reserveDeferredFormats` | `WindowsClipboardResult ReserveDeferredFormats(IReadOnlyDictionary<string, Func<byte[]>> providers, ...)` | 同期 |
| `recoverDeferredState` | `WindowsClipboardResult RecoverDeferredState(...)` | 同期 |
| `getClipboardHistory` | `uint GetHistory(Action<WindowsClipboardHistoryResult>? onResult = null)` / `Awaitable<WindowsClipboardHistoryResult> GetHistoryAsync()` | 非同期 |
| `restoreHistoryItem` | `uint RestoreHistoryItem(string itemId, Action<WindowsClipboardResult>? onResult = null)` / `RestoreHistoryItemAsync` | 非同期 |
| `deleteHistoryItem` | `uint DeleteHistoryItem(string itemId, ...)` / `DeleteHistoryItemAsync` | 非同期 |
| `clearUnpinnedHistory` | `uint ClearUnpinnedHistory(...)` / `ClearUnpinnedHistoryAsync` | 非同期 |
| `getClipboardHistoryAvailability` | `uint GetHistoryAvailability(Action<WindowsClipboardAvailabilityResult>? onResult = null)` / `GetHistoryAvailabilityAsync` | 非同期 |
| `cancelClipboardRequest` | `bool CancelRequest(uint requestId)` | 同期 |

---

## 6. 変更ファイル一覧

### 6.1 新規作成（Runtime）

| ファイル | 内容 |
|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardManager.cs` | Singleton Manager。P/Invoke 宣言、ライフサイクル、同期 API、非同期リクエスト表、イベント配送、遅延レンダリング |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardErrorCode.cs` | `WindowsClipboardErrorCode` enum（native 0〜19 + C# 側 1000 番台）と `ToMessage()` |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardResult.cs` | `WindowsClipboardResult`（void 操作の結果型） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardReadResults.cs` | `WindowsClipboardTextResult` / `WindowsClipboardStringListResult` / `WindowsClipboardBytesResult` / `WindowsClipboardFormatPresenceResult` |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardHistoryResults.cs` | `WindowsClipboardHistoryItem` / `WindowsClipboardHistoryResult` / `WindowsClipboardAvailabilityResult` |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardPayloads.cs` | `WindowsClipboardWriteOptions`（`[Flags]`）、`WindowsClipboardFormatPayload`（format + text / html / base64 の排他） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardJsonBuilder.cs` | `BuildPathsJson` / `BuildMultiFormatItemsJson` / `BuildFormatNamesJson`（public static、手書きシリアライザ） |
| `Packages/com.jonghyunkim.nativetoolkit/Runtime/Clipboard/WindowsClipboardJsonParser.cs` | `TryParseStringArray` / `TryParseHistoryItems` / `TryParseAvailability`（internal static、`JsonUtility` + DTO） |

### 6.2 新規作成（テスト）

| ファイル | 層 | 内容 |
|---|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardResultTests.cs` | EditMode | 結果型の不変条件（成功時 `ErrorMessage == null` 等）、エラーコード → メッセージ対応 |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardJsonBuilderTests.cs` | EditMode | パス配列 / 複数形式 / 予約形式の JSON 生成、エスケープ、排他キー違反の検出 |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardJsonParserTests.cs` | EditMode | 履歴配列（`text` null、`contentTypes` 省略、`timestamp` 巨大値）、可用性オブジェクト、壊れた JSON |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/WindowsClipboardManagerDispatchTests.cs` | EditMode | `InvokeInOrder`（共通 → 個別）、リクエスト表の登録 / 取り出し / 重複完了防止、バッファ再試行判定 |
| `Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs` | PlayMode（Editor 内） | 非 Windows Player 時の拒否経路、dispatcher 経由の順序、`Awaitable` が拒否時にも完了すること、`Shutdown` のリトライ完了 |

### 6.3 既存変更

| ファイル | 変更内容 | 状態 |
|---|---|---|
| `Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/unity-windows-native-toolkit.dll` | `dist/1.11.0/windows/windows-native-toolkit-1.2.0.dll` へ差し替え（ファイル名・meta・GUID は維持） | **対応済み**（2026-09-05） |
| `Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/VERSION.txt` | `source:` 行を `dist/1.11.0/windows/windows-native-toolkit-1.2.0.dll` へ更新（バージョン行 `1.2.0` は更新済み） | 未対応 |

### 6.4 非変更（明示）

- `Runtime/Clipboard/` の Android / iOS 実装、`ClipboardOperationResult` / `ClipboardReadResult` / `ClipboardDescriptionResult`
- `Runtime/Notification/Windows*`、`Runtime/Dialog/*`、`Runtime/Common/*`
- `Runtime/NativeToolkit.Runtime.asmdef`、`Tests/*/*.asmdef`（新規 asmdef は追加しない）
- `Runtime/UI/**`、`Samples~/**`、シーン、UXML / USS（`design-sample-scene` の対象）
- `package.json`（バージョン更新はリリースワークフローの責務）

---

## 7. 実装詳細

### 7.1 クラス構成とイベント

```csharp
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
public class WindowsClipboardManager : MonoBehaviour
{
    private const string LogTag = "WindowsClipboardManager";

    public static WindowsClipboardManager Instance { get; }   // 既存 Singleton パターン

    // 操作名定数（結果型の Operation に入る。全 API 分を定義する）
    public const string OperationInitialize       = "initClipboardManager";
    public const string OperationShutdown         = "uninitClipboardManager";
    public const string OperationSetHistoryEvents = "setClipboardHistoryCallbacks";
    public const string OperationCopyPlainText    = "copyPlainText";
    // ...

    // 共通イベント（常に発火）
    public event Action<WindowsClipboardResult>? ClipboardOperationCompleted;          // void 系すべて
    public event Action<WindowsClipboardTextResult>? TextReadCompleted;                // PastePlainText / PasteHtml / GetPreferredFormat
    public event Action<WindowsClipboardStringListResult>? StringListReadCompleted;    // PasteFiles / GetFormats
    public event Action<WindowsClipboardBytesResult>? BytesReadCompleted;              // PasteImage / PasteCustomFormat
    public event Action<WindowsClipboardFormatPresenceResult>? FormatPresenceChecked;  // HasFormat
    public event Action<WindowsClipboardHistoryResult>? HistoryReadCompleted;          // GetHistory
    public event Action<WindowsClipboardAvailabilityResult>? HistoryAvailabilityChecked;

    // 通知イベント（ネイティブ発）
    public event Action? ClipboardChanged;
    public event Action? HistoryChanged;
    public event Action<bool>? HistoryEnabledChanged;   // 信頼できない旨を XML コメントに明記
    public event Action<bool>? RoamingEnabledChanged;   // 同上
}
#endif
```

**callback 提供方針**（common.md / 既存 Manager 準拠）:

| 種別 | 形 | 発火 |
|---|---|---|
| 共通イベント | `event Action<TResult>?` | 常に発火（拒否・失敗・成功のすべて） |
| 個別 callback | 各メソッドの `Action<TResult>? onResult = null` 引数 | 任意。未指定でも共通イベントは発火 |
| dispatch 順序 | **共通イベント → 個別 callback**（`InvokeInOrder` を `internal static` として切り出し EditMode で検証） | |

- 同期 API は**戻り値でも結果を返す**（common.md「同期ネイティブ API は同期 C# API」）。イベント / callback は戻る前に同一スレッドで発火する（順序: 共通 → 個別 → return）
- 非同期 API（履歴 5 種）は **requestId をキーにしたリクエスト表**で per-call callback を保持するため、iOS の「last-registered wins」問題が発生しない。したがって `Awaitable<T>` 版を全 5 種に併設する
- `Awaitable` 版は `AwaitableCompletionSource<T>` で callback 版を包む薄いラッパーとし、ネイティブ呼び出しロジックを重複させない
- イベントを同一スタックで発火するため、購読者が Manager を再入呼び出しできる。ネイティブのコールバック内再入禁止（`uninit` / `setClipboardHistoryCallbacks`）とは別経路である旨をコメントに残す

### 7.2 スレッド契約

| 対象 | 契約 |
|---|---|
| 全 public API | **Unity メインスレッド限定**。他スレッドからの呼び出しは `MainThreadRequired` で即時失敗を返す |
| 所有 UI スレッド | `Initialize` を呼んだ Unity メインスレッドをネイティブの所有 UI スレッドとして採用する。Player のメッセージポンプがネイティブの隠しウィンドウのメッセージも処理する前提 |
| ネイティブコールバック | 所有 UI スレッド（= Unity メインスレッド）に届くが、Player ループ内のポンプ処理中に届く。イベント配送は `UnityMainThreadDispatcher.Enqueue` を通し、次の `Update` で発火させる |
| 遅延レンダリング provider | **例外**。`WM_RENDERFORMAT` 処理中に同期的に値を返す必要があるため dispatcher を使わない（7.7） |

ネイティブの同期 API は任意スレッド可だが、C# 層はイベント配送とライフサイクル管理を単純化するためメインスレッド限定とする（設計判断）。

### 7.3 初期化（`Initialize`）

1. メインスレッド確認 → 失敗時 `MainThreadRequired`
2. `Application.platform != RuntimePlatform.WindowsPlayer` → `PlatformUnavailable` を返し、イベント / callback を発火（Editor では DLL の meta が Editor 無効のため必ずここで止まる）
3. アパートメント確認: `CoGetApartmentType` が STA / MAINSTA 以外なら `CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED)` を 1 回だけ試行する
   - `RPC_E_CHANGED_MODE (0x80010106)` の場合は MTA 確定のため `WrongApartment` を返して終了（回復不能）
   - 成功した場合も `CoUninitialize` は呼ばない（Unity メインスレッドの COM 状態を壊さないため）
   - **要検証（V-1）**: Unity Windows Player のメインスレッドが既定で STA か
4. `initClipboardManager(enableChangeEvents ? s_changedDelegate : null, out pError)`
5. `pError == 0` で `_initialized = true`。同一スレッドからの再呼び出しは冪等成功（ネイティブ契約）
6. 結果を返す（共通イベント → 個別 callback → return）

`Awake` では `_ = UnityMainThreadDispatcher.Instance;` のみ行い、**自動初期化はしない**（アプリ側が明示的に `Initialize` を呼ぶ）。

### 7.4 終了（`Shutdown`）

- `uninitClipboardManager` は TRUE を返すまで**フレームを跨いで**リトライする（メッセージポンプを回す必要があるため）
- 実装: コルーチン（`yield return null`）で最大 N フレーム（既定 60）または T 秒（既定 2 秒）までリトライ。超過時は `ShutdownTimeout` として失敗結果を返す
- **プロセス終了前の保証**: `Application.wantsToQuit` に購読し、未完了なら `false` を返してドレイン用コルーチンを開始し、完了後に `Application.Quit()` を再実行する
  - 予約済み遅延形式は所有ウィンドウ破棄時にのみ実体化されるため、この経路がないとクリップボードから消える（2.6）
- `OnDestroy` は最後の砦として 1 回だけ `uninitClipboardManager` を呼ぶ（best-effort、戻り値をログに残す）
- ドレインにより未完了リクエストは `CANCELED` でコールバックされる → リクエスト表が完了処理を行い、`Awaitable` も必ず完了する

### 7.5 同期 API の実装方針

- 書き込み系: 引数検証（null / 空配列 / `byte[]` 長 0）→ ネイティブ呼び出し → 結果生成 → 配送 → return
- 読み出し系は **2 回呼び出しバッファ規約**:
  1. `buffer = IntPtr.Zero, bufferSize = 0` で呼び、必要サイズ（wchar_t 数 or バイト数）を得る。`pError` は `BUFFER_TOO_SMALL` になる
  2. 必要サイズが 0 かつ `pError` が `NONE` なら空値として成功（`getPreferredClipboardFormat` が該当し得る。V-4）
  3. `Marshal.AllocHGlobal`（文字列は `size * 2` バイト）→ 再呼び出し → `Marshal.PtrToStringUni` / `Marshal.Copy` → `finally` で `FreeHGlobal`
  4. 2 回目も `BUFFER_TOO_SMALL` の場合（間にクリップボードが変わった）は最大 2 回まで再試行し、超過は失敗として返す
- `EMPTY` / `FORMAT_UNAVAILABLE` は「空のクリップボード」という正常系として扱えるよう、読み出し結果型に明示の `IsEmpty` を持たせる
- クリップボード内容そのものはログに出さない。ログはサイズ・形式名・エラーコードのみ

### 7.6 非同期（履歴）API の実装方針

```csharp
private static readonly ClipboardRequestCallback s_requestDelegate = OnRequestCompleted;  // GC ルート
private static readonly Dictionary<uint, PendingRequest> s_pending = new();               // メインスレッドのみが触る
private readonly struct PendingRequest { public string Operation; public Action<int, string?> Complete; }
```

- 呼び出し手順: 引数検証 → `getClipboardHistory(s_requestDelegate, out pError)` → `requestId != 0` なら `s_pending[requestId]` に登録して return
- **登録タイミングの安全性**: 完了は必ず `PostMessage` 経由で所有 UI スレッド（= 現在のスレッド）に届くため、呼び出しから return するまでの間にコールバックが走ることはない。よって「呼び出し後に登録」で競合しない
- `requestId == 0`（受付前失敗）: `pError` を用いた失敗結果を**即時に配送**する。`pError == 0` の場合は `RequestRejected` として扱う。これにより `Awaitable` が未完了のまま残らない
- `OnRequestCompleted(requestId, error, json)`: `s_pending` から取り出し（見つからない場合は警告ログのみ）、JSON をパースし、`UnityMainThreadDispatcher.Enqueue` で配送
- `CancelRequest(requestId)`: `cancelClipboardRequest` を呼ぶ。`false` でも完了は届き得るため、リクエスト表からは削除しない
- 同一操作の並行呼び出しは requestId で区別されるため許可する（in-flight ガードは設けない）。ただし `RestoreHistoryItem` のようなクリップボードを変更する操作の同時実行はアプリ側の意図と衝突し得る旨を XML コメントで注意喚起する

### 7.7 遅延レンダリング（`ReserveDeferredFormats`）

```csharp
public WindowsClipboardResult ReserveDeferredFormats(
    IReadOnlyDictionary<string, Func<byte[]>> providers,
    WindowsClipboardWriteOptions options = WindowsClipboardWriteOptions.None,
    Action<WindowsClipboardResult>? onResult = null);
```

- `providers` のキー（形式名）から `formatNamesJson` を生成し、`context` には `IntPtr.Zero` を渡す（形式名でルーティングするため context 不要）
- `s_renderDelegate`（static readonly、`[MonoPInvokeCallback]`）が全形式の描画を受ける
- 二相呼び出しへの対応:
  1. `buffer == IntPtr.Zero`: `Func<byte[]>` を実行してバイト列を生成し `s_renderCache[formatName]` に保持。`requiredSize` に長さを設定し `BUFFER_TOO_SMALL` を返す
  2. `buffer != IntPtr.Zero`: キャッシュ（無ければその場で生成）から `Marshal.Copy`。`bufferSize` 不足なら `requiredSize` を返して `BUFFER_TOO_SMALL`
- **provider の制約（XML コメントに明記）**: Unity API を呼ばない / クリップボード API を呼ばない / ブロックしない / 例外を投げない。
  provider は `WM_RENDERFORMAT` 処理中に同期実行されるため `UnityMainThreadDispatcher` は使えない（キューは次の `Update` まで流れずデッドロックする）
- provider から例外が出た場合は catch して `Unknown`(19) を返し、`Debug.LogError` に形式名のみ記録する
- キャッシュは次回 `ReserveDeferredFormats` 呼び出し時と `Shutdown` 時にクリアする。クリップボード所有権の喪失を C# から観測できないため、それまでバイト列を保持する（既知の制限としてコメントに残す）
- Windows のクリップボード履歴が有効な場合、履歴サービスが予約直後に全形式を実体化するため「まだ provider が呼ばれていない」を前提にしない（ネイティブ契約）

### 7.8 IL2CPP / マーシャリング制約

- コールバック実体はすべて `private static` + `[MonoPInvokeCallback(typeof(...))]`。インスタンスメソッド・ラムダを直接渡さない
- delegate インスタンスは `static readonly` フィールドで保持し GC を防ぐ。ネイティブが保持する期間（`uninitClipboardManager` が TRUE を返すまで）は必ず生存させる
- コールバック内で例外を C ABI 境界へ漏らさない（全体を `try/catch`）
- `bool` の戻り値・引数は `[MarshalAs(UnmanagedType.Bool)]` を明示する（既定の 2 バイト `VARIANT_BOOL` 化を避ける）
- `AndroidJavaProxy` は使用しない（Windows のため該当なし）

### 7.9 依存関係と実装順序

1. **純粋層**: `WindowsClipboardErrorCode` → 結果型 3 ファイル → `WindowsClipboardPayloads` → `WindowsClipboardJsonBuilder` / `WindowsClipboardJsonParser`（EditMode テストを先に書く）
2. **Manager 骨格**: Singleton / `Awake` / `OnDestroy` / ログ / プラットフォームガード / `InvokeInOrder` / メインスレッド判定
3. **ライフサイクル**: アパートメント処理 → `Initialize` → `Shutdown`（コルーチン + `wantsToQuit`）→ `CanShutdownNow`
4. **同期 API**: 書き込み 6 種 → 読み出し 5 種 → 内容確認 3 種 → `Clear`
5. **非同期 API**: リクエスト表 → 履歴 5 種 → `CancelRequest` → `Awaitable` 版
6. **イベント**: `ClipboardChanged` → `SetHistoryEventsEnabled` と履歴 3 イベント
7. **遅延レンダリング**: `ReserveDeferredFormats` / `RecoverDeferredState`
8. **実機確認**（9.3）と `VERSION.txt` の `source:` 行更新（6.3）

DLL 本体は差し替え済みのため、V-1 / V-2 の実機検証は 3 の時点で即座に実施できる。

---

## 8. エラーケース一覧と返却仕様

### 8.1 ネイティブ層（`pError` として返る 19 種）

2.3 の表を正本とする。C# は `WindowsClipboardErrorCode` enum で 0〜19 を受け、`ToMessage()` で 2.3 の英語メッセージへ変換する。

発生源別の内訳:

| 層 | コード | 主な発生条件 |
|---|---|---|
| ネイティブ引数検証 | 1 `InvalidParameter` | `null` 引数、サイズ 0、JSON 解析失敗、`copyMultipleFormats` の重複 format / ペイロード種別不一致、`reserveDeferredFormats` の空配列・形式名解決失敗 |
| ネイティブ状態 | 2 `NotInitialized` | `initClipboardManager` 前、`uninit` 後、lifecycle gate クローズ中の呼び出し |
| Win32 | 3 `Busy` / 4 `Empty` / 5 `FormatUnavailable` / 8 `OutOfMemory` | `OpenClipboard` リトライ上限、空クリップボード、要求形式なし、`GlobalAlloc` / `GlobalLock` 失敗・`std::bad_alloc` |
| ネイティブ境界検証 | 6 `InvalidData` / 7 `BufferTooSmall` / 13 `PartialState` | 終端 NUL / サイズ / オフセット / DIB 構造の検証失敗、出力バッファ不足、配置失敗後のロールバック失敗 |
| WinRT（履歴） | 9 `AccessDenied` / 10 `HistoryDisabled` / 11 `ItemDeleted` / 17 `NotForeground` | 履歴アクセス拒否、履歴設定オフ、項目削除済み、自プロセスが前面でない（**受付後にコールバックで返る**） |
| スレッド / 監視 | 12 `MonitorRegisterFailed` / 14 `WrongThread` / 18 `WrongApartment` | リスナ登録・解除失敗、UI スレッド限定 API の誤用、`init` 呼び出しスレッドが STA でない |
| ライフサイクル | 15 `Canceled` | `cancelClipboardRequest` または `uninit` ドレインによる失効 |
| その他 | 16 `NotSupported` / 19 `Unknown` | Windows に等価概念がない操作、`DeleteItemFromHistory` / `ClearHistory` の `false` を含む上記以外 |

### 8.2 C# Bridge 層（本実装が生成する。1000 番台）

| 値 | 定数 | 条件 | メッセージ |
|---|---|---|---|
| 1000 | `PlatformUnavailable` | `Application.platform != RuntimePlatform.WindowsPlayer`（Editor 実行を含む） | `{operation} is available only on Windows player builds.` |
| 1001 | `BridgeUnavailable` | `DllNotFoundException` / `EntryPointNotFoundException` | `{operation} could not be started; the native clipboard bridge is unavailable.` |
| 1002 | `MainThreadRequired` | Unity メインスレッド以外からの呼び出し | `{operation} must be called from the Unity main thread.` |
| 1003 | `ManagerDestroyed` | `OnDestroy` 後の呼び出し | `{operation} was rejected because the manager has been destroyed.` |
| 1004 | `NotInitializedByHost` | `Initialize` 未実行 / 失敗のままの呼び出し | `{operation} requires Initialize to succeed first.` |
| 1005 | `InvalidArgument` | C# 側の引数検証失敗（null / 空 / 形式名が空白 / `WindowsClipboardFormatPayload` の排他違反） | `{operation} received an invalid argument: {detail}` |
| 1006 | `ShutdownTimeout` | `Shutdown` のリトライ上限超過 | `Shutdown did not complete within the retry budget.` |
| 1007 | `ResultParseFailed` | ネイティブが返した JSON のパース失敗 | `{operation} returned a payload that could not be parsed.` |
| 1008 | `RequestRejected` | `requestId == 0` かつ `pError == 0` | `{operation} was rejected without a native error code.` |

### 8.3 不変条件

- `IsSuccess == true` のとき `ErrorCode == None` かつ `ErrorMessage == null`
- `IsSuccess == false` のとき `ErrorMessage != null`
- 読み出し結果は `IsSuccess == true` でも `IsEmpty == true` があり得る（空クリップボードは正常系）
- 受付済み（`requestId != 0`）の非同期リクエストは、成功・失敗・キャンセル・shutdown ドレインのいずれかで**必ず 1 回**結果が配送される
- 拒否（受付前失敗）でも共通イベントと per-call callback は必ず発火する（`Awaitable` のハング防止）

---

## 9. テスト方針

### 9.1 層 1: EditMode（`Tests/Runtime`）

| 対象 | 検証内容 |
|---|---|
| `WindowsClipboardErrorCode` / 結果型 | 0〜19 と 1000 番台のメッセージ対応、成功時 `ErrorMessage == null`、`IsEmpty` の判定 |
| `WindowsClipboardJsonBuilder` | パス配列（バックスラッシュ / 非 ASCII / 引用符のエスケープ）、複数形式（順序保持、排他キー違反の検出）、予約形式 |
| `WindowsClipboardJsonParser` | 履歴配列（`text: null`、`contentTypes` 省略、`timestamp` が `long.MaxValue` 相当）、可用性オブジェクト、壊れた JSON / 空文字 / null |
| `InvokeInOrder` | 共通イベント → 個別 callback の順序、片方 null、購読者の例外が他方の発火を妨げないこと |
| リクエスト表 helper | 登録 / 取り出しで削除されること、未知 ID の完了が無害であること |
| バッファ helper | 必要サイズ 0 の扱い、`BUFFER_TOO_SMALL` の再試行上限 |

Manager インスタンスを生成するテストは書かない（testing.md 層 1 の規約）。

### 9.2 層 2a: PlayMode（Editor 内、`Tests/PlayMode`）

- 非 Windows Player 環境での拒否経路（`PlatformUnavailable`）が **event と per-call callback の両方**に届くこと
- `GetHistoryAsync` 等が拒否時にも必ず完了し、ハングしないこと
- dispatcher の `Update` flush を跨いだ順序保証
- `Shutdown` がリトライ後に完了結果を返すこと（ネイティブ不在環境では拒否結果で完了）

### 9.3 層 2b / 層 3: 実機（Windows 11 Player）

本計画では**テストコードは作成せず、手動確認項目として定義する**（自動化は testing.md の未定義事項に従い別途）。

| # | 確認項目 | 期待 |
|---|---|---|
| M-1 | `Initialize` 成功 | `WRONG_APARTMENT` が返らない（V-1 の実証） |
| M-2 | テキスト copy → メモ帳に貼り付け | 同一内容 |
| M-3 | メモ帳でコピー → `PastePlainText` | 同一内容 |
| M-4 | HTML copy → Word / ブラウザに貼り付け | 書式が保たれる |
| M-5 | ファイル copy → エクスプローラに貼り付け | ファイルがコピーされる |
| M-6 | 画像（DIB）copy → ペイントに貼り付け | 画像が一致 |
| M-7 | `CopyMultipleFormats` | 貼り付け先アプリごとに richest な形式が選ばれる |
| M-8 | `Sensitive` オプションで copy | Win+V の履歴に載らない |
| M-9 | 他アプリでコピー | `ClipboardChanged` が発火。自プロセスの copy では発火しない |
| M-10 | 履歴取得 | Win+V の内容と一致。`timestamp` が妥当 |
| M-11 | 履歴無効時に履歴 API | `HistoryDisabled` がコールバックで返る |
| M-12 | 非フォアグラウンド時に履歴 API | `NotForeground` がコールバックで返る |
| M-13 | `RestoreHistoryItem` / `DeleteHistoryItem` / `ClearUnpinnedHistory` | Win+V 上の状態が変わる。ピン留め項目は残る |
| M-14 | 進行中リクエストの `CancelRequest` | `Canceled` が 1 回だけ返る |
| M-15 | `ReserveDeferredFormats` 後に他アプリで貼り付け | provider が呼ばれ内容が渡る |
| M-16 | 予約状態のままアプリ終了 | `wantsToQuit` ドレインにより予約形式が実体化される |
| M-17 | 大きなテキスト / 画像（バッファ再試行経路） | 2 回呼び出しで正しく取得できる |
| M-18 | IL2CPP ビルドでの全経路 | `MonoPInvokeCallback` が機能し、コールバックが届く |

---

## 10. 未確定・要検証事項

| # | 事項 | 影響 | 対応 |
|---|---|---|---|
| V-1 | Unity Windows Player のメインスレッドが STA として初期化されているか | MTA の場合 `initClipboardManager` が `WRONG_APARTMENT` で失敗し、`CoInitializeEx` も `RPC_E_CHANGED_MODE` で回復不能。その場合は「専用 STA スレッド + 自前メッセージポンプ」を C# 層に持つ代替設計（大幅な追加実装）が必要になる | 実装の早い段階（7.9 の 3）で実機検証する。MTA だった場合は実装を止めて設計を見直す |
| V-2 | Unity Player のメッセージポンプがネイティブの隠しトップレベルウィンドウのメッセージも配送するか | 配送されない場合、履歴コールバックと `WM_RENDERFORMAT` が届かない | V-1 と同じタスクで、`ClipboardChanged` の受信をもって確認する |
| V-3 | `Application.wantsToQuit` 経由のドレインが Player で確実に動作するか（タイムアウト時の扱いを含む） | 予約形式の消失 | M-16 で確認 |
| V-4 | `getPreferredClipboardFormat` が「該当なし」のときの戻り値（必要サイズ 1 / `pError` が `NONE` か） | 空結果の判定分岐 | 実機で確認し、判定を実装に反映する |
| V-5 | Editor 実行時の扱い | 現状 DLL の meta が Editor 無効のため Editor では動作しない。`PlatformUnavailable` を返す設計とする | 変更しない（Editor 対応は本計画のスコープ外） |
| V-6 | ~~`unity-windows-native-toolkit-debug.dll` の有無~~ | — | **解決済み**（2026-09-05）。`native-toolkit/dist` 配下に debug ビルドは存在しない。Clipboard は `DEVELOPMENT_BUILD` 分岐を持たず単一 DLL 名を使う（5.1）。既存の `WindowsNotificationManager` が development build で DLL を解決できない点は本計画の対象外だが、別途の是正候補として記録する |

---

## 11. スコープ外

- サンプルアプリ（`Runtime/UI/Windows/Clipboard/**`、UXML / USS、サンプルシーン、`NativeToolkitSampleNavigator` への導線）→ `design-sample-scene`
- マニュアル / `docs` の更新 → `write-manual`
- `package.json` のバージョン更新・リリース作業 → `release`
- ネイティブ（`native-toolkit`）側の実装変更
- 層 2b / 層 3 のテスト自動化ハーネス構築
