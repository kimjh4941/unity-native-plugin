# Windows の C ABI が 2.0.0 に置き換わり、P/Invoke 層を書き直す

- 記録日: 2026-09-23
- 分類: 横断課題（移行）。**機能追加ではない。** clipboard / notification / dialog の 3 機能にまたがる
- 発見経緯: native-toolkit 側（`feature/NTKIT-16`）から、1.x の C ABI を削除し 2.0.0 に置き換えた旨の申し送りを受けた
- 対応方針: **native-toolkit が develop にマージされ 1.12.0 が出るまで、実装は始めない。** 待つ間に設計を固める
- 進捗: **企画中。** この README のみ

- 対象: `Runtime/Clipboard/Windows*.cs`、`Runtime/Notification/Windows*.cs`、`Runtime/Dialog/WindowsDialogManager.cs`、`Plugins/Windows/`、対応するテストとサンプル
- チケット: 未採番
- 相手側: native-toolkit `artifact/topics/windows-architecture/`（分類: 横断課題（アーキテクチャ））

---

## 1. 何が起きているか

1.x の C ABI は**削除された**。互換レイヤーは無く、2.0.0 は別物である。

| | 1.x | 2.0.0 |
|---|---|---|
| 関数 | 52 | `ntk_*` に改名。ヘッダー 4 本で 114 宣言 |
| 文字列 | `wchar_t*`。呼び出し側のバッファを 2 回呼んで採寸 | UTF-8。不透明ハンドルから借りる |
| 構造化データ | JSON 文字列 | 型付き構造体（`struct_size` で版管理）とリストハンドル + index アクセサ |
| エラー | `DWORD* pError` の出力引数 | 戻り値。OS の生値は `ntk_last_system_code()` |
| 解放 | プロセス横断の状態を持つ free 関数 | ハンドルごとの `_free` |

**この repo は今のところ壊れていない。** 1.x の DLL を同梱して 1.11.0 として動いているので、急ぐ理由は無い。

## 2. 影響範囲

### 2.1 P/Invoke 宣言

| ファイル | P/Invoke | 行数 |
|---|---|---|
| `Runtime/Clipboard/WindowsClipboardManager.cs` | 27 | 3,984 |
| `Runtime/Notification/WindowsNotificationManager.cs` | 14 | 507 |
| `Runtime/Dialog/WindowsDialogManager.cs` | 6 | 567 |

宣言は 47 本だが、**書き直しは宣言だけでは終わらない**（3 章）。

### 2.2 JSON 廃止で不要・要改修になる可能性があるもの

ABI から JSON が消えるため、JSON を前提に作った層がそのまま残らない。

| ファイル | 見込み |
|---|---|
| `Runtime/Clipboard/WindowsClipboardJsonParser.cs` | 履歴・一覧の受け取りがリストハンドルになるため不要の見込み |
| `Runtime/Clipboard/WindowsClipboardJsonBuilder.cs` | 引数が構造体になるため不要の見込み |
| `Runtime/Notification/WindowsNotificationJsonBuilder.cs` | 通知内容がビルダーハンドルになるため不要の見込み |
| `Tests/Runtime/WindowsClipboardJsonParserTests.cs` | 対象が消えれば削除 |
| `Tests/Runtime/WindowsClipboardJsonBuilderTests.cs` | 同上 |

**「不要の見込み」は要検証。** 対応表（設計書 8.3）と突き合わせて確定させる。

唯一 JSON の形で残るのは `ntk_notification_activation_raw_arguments` で、これは逃げ道として保持されたもの。構造化した値は `ntk_notification_activation_value_count` / `_key_at` / `_value_at` から取る。

### 2.3 差し替えるバイナリ

| 今 | 2.0.0 |
|---|---|
| `Plugins/Windows/unity-windows-native-toolkit.dll` | `NativeToolkitC.dll`（配布物は `dist/1.12.0/windows/windows-native-toolkit-capi-2.0.0.dll`） |
| `Plugins/Windows/Microsoft.WindowsAppRuntime.Bootstrap.dll` | **変更なし。** 隣に置く要件も同じ |

x64 のみ。`ntk_version()` が `NTK_VERSION`（`0x020000`）と一致することを起動時に 1 回確かめる。

### 2.4 波及先

サンプルとテストは本トピックの設計対象に含めるが、**サンプルシーンの作り直しが要る場合は別に切り出す。**

- `Runtime/UI/Windows/Clipboard/`（ExampleController・Fixtures・SampleResult）
- `Runtime/UI/Windows/Dialog/`、`Runtime/UI/Windows/Notification/` の ExampleController
- `Tests/PlayMode/WindowsClipboardManagerIntegrationTests.cs`
- `Tests/Runtime/WindowsClipboard*Tests.cs`（7 本）、`Tests/Runtime/WindowsNotificationTests.cs`
- `Runtime/Dialog/Win32MessageBox.cs`、`Editor/UI/NativeToolkitEditorWindow.cs`

## 3. 宣言の置換では済まないもの

**ここが本トピックの中身である。** 以下はいずれも今のコードに概念自体が無い。

### 3.1 ドメインリロード（最優先）

Unity Editor はネイティブ DLL を下ろさない。DLL の中のセッションとマネージャーはドメインリロードとプレイモードの出入りを越えて生き残る一方、C# の static に置いたハンドルは消え、登録した関数ポインタは下ろされたドメインを指す。

- `AssemblyReloadEvents.beforeAssemblyReload`、`EditorApplication.playModeStateChanged`（`ExitingPlayMode`）、`Application.quitting` で後始末する
- Clipboard: **オーナースレッドから** `ntk_clipboard_session_close` → `ntk_clipboard_session_free`。`BUSY` なら他スレッドの読み書きを終わらせて再試行
- Notification: `ntk_notification_manager_close` → `_free` の後、**すべての登録の `release` を期限付きで待つ**
- **close が成功しないまま `_free` すると、その Editor では以後セッションを作れない**（放棄）
- 活性化コールバックは受け取ったものをキューに積むだけにする。メインスレッドへ同期で戻ると、close を待つメインスレッドと相互に待って止まる

### 3.2 STA / MTA の分離

`ntk_notification_manager_create` は**呼び出しスレッドを MTA にする**。クリップボードのオーナーは STA でメッセージループを回し続ける必要があるため、**同じスレッドを両方のオーナーにできない。** スレッドを分けるか、先に STA で初期化する。

### 3.3 マーシャリングの落とし穴

- **文字列を返す関数に `[return: MarshalAs(UnmanagedType.LPUTF8Str)]` を付けない。** マーシャラーが戻りポインタを `CoTaskMemFree` で解放してヒープを壊す。`IntPtr` で受けて自分で複製する。引数側の `LPUTF8Str` は可
- 借りたポインタ（`ntk_string_data` など）はそのハンドルを `_free` するまで有効。コールバックが受け取ったハンドルは**そのコールバックから戻るまで**
- **登録ごとに `GCHandle` を 1 つ**作り、`release` で `Free` する。同じ `user_data` の値で登録し直すときも別の所有権を渡す。`release` は登録が失敗したときも必ず 1 回呼ばれる（呼び出しスレッドで、戻る前に）
- コールバックは `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`（`NTK_CALL` は `__cdecl`）
- 構造体は 0 で埋め、`struct_size` に **`Marshal.SizeOf<T>()`**（`sizeof` ではない）。`reserved` は 0 のまま
- ハンドルは `SafeHandle` に包み、対応する `_free` で解放する

### 3.4 意味が反転する引数

0 が「既定」になった結果、C++ API に対して**意味が反転している**フラグが 2 つある。移行時に取り違えると静かに壊れる。

- `allow_missing_file`
- `skip_overwrite_prompt`

## 4. 未確認・未決

| # | 内容 | 状態 |
|---|---|---|
| Q-1 | 申し送りは「105 関数」だが、ヘッダー 4 本の宣言数は 114（Common 12 / Dialog 6 / Notification 49 / Clipboard 47）。差の 9 が関数ポインタ型の typedef なのか別の理由かを確定する | 未確認 |
| Q-2 | 2.2 の「不要の見込み」4 ファイル + テスト 2 本を、対応表 8.3 と突き合わせて確定する | 未確認 |
| Q-3 | 現行 47 本の P/Invoke が旧 52 関数のどれに当たるか。使っていない 5 関数があるはず | 未確認 |
| Q-4 | サンプルシーンの作り直しが要るか。要るなら `design-sample-scene` へ別途切り出す | 未決 |
| Q-5 | NuGet の `NativeToolkit.CApi 2.0.0` を使うか、DLL を直接同梱するか。Unity のパッケージ構成と噛み合うかを確かめる | 未決 |
| Q-6 | チケット番号（`UNT-13` 以降）を採番する | 未決 |

**Q-1 〜 Q-3 は native-toolkit セッションに聞けば済む。** 相手は「ABI と文書が食い違っていたらヘッダーで確かめる」と申し出ている。

## 5. 参照

native-toolkit のパス（このマシン上）。**`feature/NTKIT-16` は develop 未マージ、1.12.0 未リリース**（2026-09-23 時点、HEAD `72d83fce`）。

| 見るもの | 場所 |
|---|---|
| こちらへの申し送り | `artifact/topics/windows-architecture/results/2026-09-23-windows-architecture-stage5-result.md` 2 章 |
| 52 関数の対応表 | `artifact/topics/windows-architecture/designs/2026-09-21-windows-architecture-c-abi-design.md` 8.3 |
| スレッドと寿命の契約 | 同 1.3（Unity 固有は 1.3.4、STA/MTA は 1.3.3） |
| 受け渡しの 3 方式 | 同 1.2 |
| 21 の決定（E-1〜E-21） | 同 4.2 |
| 公開ヘッダー | `windows/WindowsLibraryCApi/include/NativeToolkitC/{Common,Dialog,Notification,Clipboard}.h` |
| 全 105 関数の C 実例 | `manual/1.12.0/{dialog,notification,clipboard}.md` の「C ABI」節。`scripts/check_manual_c_examples.py` でコンパイル検査済み |
| 配布物 | `dist/1.12.0/windows/`（`-capi-` が C ABI。`-capi-` の付かない方は C++ の静的ライブラリで、**こちらではない**） |
