# iOS Share 機能 実装計画書 (v2)

- 対象機能: share（システム共有シート / `UIActivityViewController`）
- 対象プラットフォーム: iOS（最小 iOS 18）
- 作成日: 2026-07-05
- 対象範囲: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/` の C# Bridge / Manager / Payload / JsonBuilder / Result と EditMode テスト
- 対象外: サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は `design-sample-scene` で別途設計
- v2 変更点: レビュー指摘（`2026-07-05-ios-share-design-review.md`）を反映。platform guard 方針、非 iOS 時の callback 挙動、nullable/null 要素の入力契約、Manager dispatch の EditMode 検証方針を確定（第 9 章に反映内容を集約）

---

## 0. 前提条件・要検証（最重要）

### [解消済] 同梱 xcframework の Share Bridge 対応

- 当初、同梱バイナリ（`unity-ios-native-toolkit-1.1.0.xcframework`）には `_shareContent`
  シンボルが未反映であり、iOS ビルドでリンクエラーとなる BLOCKER だった。
- **2026-07-05 に `unity-ios-native-toolkit-1.2.0.xcframework` へ更新済み**。
  - `nm -gU .../ios-arm64/UnityIosPlugin.framework/UnityIosPlugin` に
    `T _shareContent` が存在することを確認済み。
  - 併せて `ios-native-toolkit-1.2.0.xcframework`（IosLibrary 側）も更新済み。
  - 旧 1.1.0 の xcframework は iOS Plugins 配下から除去済み。
- したがって C# 側の `[DllImport("__Internal")] shareContent(...)` はリンク可能。前提条件はクリア。

### その他 要検証

- Info.plist 追加要否: 特定 Activity（例: 写真保存 `NSPhotoLibraryAddUsageDescription` 等）を使う場合、
  利用アプリ側の Info.plist に用途文言が必要になる可能性がある。共有シート自体の提示には不要だが、
  画像・ファイル共有先アプリによっては OS 側で権限を要求する。設計上は native 側の責務だが、
  実機確認項目として残す。

---

## 1. native-toolkit 確認結果（ネイティブ側・再実装しない）

参照: `/Users/jonghyunkim/Desktop/native-toolkit/ios/UnityIosPlugin/UnityIosPlugin/Share/`
および `.../IosLibrary/IosLibrary/Share/`

### 1.1 公開 C ABI（`UnityIosShareManagerBridge.h`）

- コールバック型:
  ```c
  typedef void (*ShareCallback)(bool isSuccess,
                                bool completed,
                                const char* activityType,
                                const char* errorMessage);
  ```
- 関数:
  ```c
  void shareContent(const char* contentJson, ShareCallback callback);
  ```
- コールバック契約（ヘッダ明記）:
  - コールバックは常にメインスレッドで発火する（無効 JSON パスを含む全経路）。
  - `isSuccess`: 共有シートを提示できたか（false = エラー）。
  - `completed`: ユーザーが Activity を完了したか（false = キャンセル）。
    - **ユーザーキャンセルはエラーではない**: `isSuccess=true, completed=false, activityType=NULL`。
  - `activityType`: 選択された Activity の raw identifier。キャンセル/不明時は `NULL`。
  - `errorMessage`: `isSuccess=false` のときのみ非 NULL。
  - `callback` は NULL 可（結果不要の場合）。
  - ポインタはコールバック内でのみ有効。即座に managed string へコピーすること
    （IL2CPP marshal が自動でコピーするため C# 側の追加対応は不要）。

### 1.2 contentJson スキーマ（`UnityIosShareJsonParser.swift`）

- `items` (Array, 必須): 各要素 `{ "type": String, "value": String }`
  - `type`: `"text"` | `"url"` | `"image"` | `"file"`
  - 未知の `type` / `value` 欠落の要素は**無視（エラーにならない）**。JSON 構文エラーのみ parse 失敗。
- `subject` (String, 任意): Mail 等で使用。
- `previewTitle` (String, 任意): 共有シートヘッダのプレビュータイトル。
- `excludedActivityTypes` (Array<String>, 任意): 除外する Activity の raw identifier
  （例: `"com.apple.UIKit.activity.PostToFacebook"`）。
- URL の妥当性は parser では検査せず、Data 層で `ShareError.invalidURL` として表面化。

### 1.3 ドメインエラー（`ShareError.swift` → errorMessage 文言）

| ケース | errorMessage |
| ------ | ------------ |
| `noValidItems` | `No shareable items were provided.` |
| `invalidURL(value)` | `Invalid URL: {value}.` |
| `imageLoadFailed(path)` | `Failed to load image at path: {path}.` |
| `fileNotFound(path)` | `File not found at path: {path}.` |
| `noRootViewController` | `No root view controller available to present the share sheet.` |
| `presentationFailed(error)` | `Failed to present the share sheet: {desc}.` |
| `unknown(error)` | `An unknown error occurred: {desc}.` |
| （parser 層）JSON 構文エラー | `Invalid share content JSON.` |

### 1.4 呼び出し方針（C# 側）

- iOS のため `[DllImport("__Internal")]` で C 関数 `shareContent` を呼び出す。
- コールバックは `[UnmanagedFunctionPointer(Cdecl)]` delegate + `[MonoPInvokeCallback]` static メソッドで受ける。
- Share は persistent 登録型のコールバックを持たない（`shareContent` 呼び出しごとに callback を渡す方式）。
  そのため OnDestroy での native コールバック解除は不要。

---

## 2. 既存 C# 実装 確認結果（このプロジェクト側）

### 2.1 参照した既存パターン

- `Runtime/Notification/IosNotificationManager.cs`:
  iOS の `DllImport` + `UnmanagedFunctionPointer` delegate + `MonoPInvokeCallback` static +
  per-operation static delegate 保持（GC 防止）+ per-call callback（last-registered wins）+
  `UnityMainThreadDispatcher` 転送。**本実装の主リファレンス**。
- `Runtime/Dialog/IosDialogManager.cs`:
  入力バリデーション → 即時 Failure、`try/catch` で native 例外を Failure に変換するパターン。
- `Runtime/Share/AndroidShareManager.cs`:
  Share 機能の Android 版。共通イベント + per-call callback、`ShareOperationResult` の設計、
  dispatch 順序（**共通イベント → 個別 callback**）。
- `Runtime/Share/AndroidShareJsonBuilder.cs`:
  外部 JSON ライブラリ非依存の手書きシリアライザ。**iOS 版でも同方式を踏襲**。
- `Runtime/Notification/IosNotificationResult.cs`:
  iOS 固有 result struct を `#if UNITY_IOS || UNITY_EDITOR` で囲み EditMode テスト可能にするパターン。

### 2.2 再利用・非再利用の判断

- 既存 `ShareOperationResult` / `ShareCallbackResult` / `ShareChooserActionResult` は
  **Android 形状**（`(Operation, IsSuccess, ErrorMessage)` 等）であり、iOS の
  `(isSuccess, completed, activityType, errorMessage)` とは形状が異なる。
  → iOS 専用の `IosShareResult` を新規作成する（`IosNotificationResult` と同じ流儀）。
- `UnityMainThreadDispatcher` はそのまま再利用（変更なし）。
- namespace は `JonghyunKim.NativeToolkit.Runtime.Share` を踏襲。
- アセンブリ定義: 新規ファイルは既存 `Runtime/Share/` 配下に置くため、既存 Runtime asmdef の対象に含まれる。asmdef の変更は不要。

---

## 3. 実装対象 API 一覧

### 3.1 ネイティブ（再掲・再実装しない）

- C 関数: `shareContent(const char* contentJson, ShareCallback callback)`
- コールバック: `ShareCallback(bool isSuccess, bool completed, const char* activityType, const char* errorMessage)`

### 3.2 C# 公開 API（新規）

- `IosShareManager.Instance`（Singleton）
- `event Action<IosShareResult>? ShareCompleted`
- `void Share(IosShareContentPayload payload, Action<IosShareResult>? onResult = null)`
- （任意・薄いラッパ、後述）`void ShareText(string text, ...)` / `void ShareUrl(string url, ...)`

---

## 4. 変更ファイル一覧

`.meta` は Unity 自動生成のため記載しない。パスは `Packages/com.jonghyunkim.nativetoolkit/` 起点。

### 4.1 新規作成（Runtime）

| ファイル | 役割 |
| ------- | ---- |
| `Runtime/Share/IosShareResult.cs` | iOS 共有結果 struct（`IsSuccess/Completed/ActivityType/ErrorMessage`）。`#if UNITY_IOS \|\| UNITY_EDITOR` |
| `Runtime/Share/IosSharePayloads.cs` | `IosShareItem`（type/value + 生成ヘルパ）、`IosShareContentPayload`（items/subject/previewTitle/excludedActivityTypes）。ガードなし（EditMode テスト可能） |
| `Runtime/Share/IosShareJsonBuilder.cs` | `IosShareContentPayload` → contentJson を手書きシリアライズ。ガードなし（EditMode テスト可能） |
| `Runtime/Share/IosShareManager.cs` | MonoBehaviour Singleton。`DllImport shareContent` + `MonoPInvokeCallback` + 共通イベント + per-call callback。`#if UNITY_IOS` |

### 4.2 新規作成（Tests）

| ファイル | 役割 |
| ------- | ---- |
| `Tests/Runtime/IosShareJsonBuilderTests.cs` | JsonBuilder の EditMode テスト（`AndroidShareJsonBuilderTests` に準拠）。null 要素除外・空 value も検証 |
| `Tests/Runtime/IosShareManagerDispatchTests.cs` | Manager の `InvokeInOrder` シームの EditMode テスト（dispatch 順序・例外握りつぶし）。`#if UNITY_IOS \|\| UNITY_EDITOR` |

### 4.3 既存変更（Tests）

| ファイル | 変更内容 |
| ------- | ------- |
| `Tests/Runtime/ShareResultTests.cs` | `IosShareResult` のファクトリ/不変条件テストを追加（`IosShareResult` は `UNITY_EDITOR` で利用可） |

### 4.4 非変更（参照・再利用のみ）

- `Runtime/Common/UnityMainThreadDispatcher.cs`
- `Runtime/Share/ShareOperationResult.cs` / `ShareCallbackResult.cs` / `ShareChooserActionResult.cs`（Android 用、iOS では不使用）

### 4.5 別途対応（対応済み・参考）

- `Plugins/iOS/unity-ios-native-toolkit-1.2.0.xcframework` へ更新済み（Share Bridge 反映済み、第 0 章参照）。本計画書の C# 実装対象外。
- 確認コマンド（実装結果でも再現できるよう記録）:
  ```sh
  nm -gU \
    Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/unity-ios-native-toolkit-1.2.0.xcframework/ios-arm64/UnityIosPlugin.framework/UnityIosPlugin \
    | grep " T _shareContent"
  # 期待: 00000000000065c0 T _shareContent
  ```

---

## 5. 実装詳細

### 5.1 `IosShareResult`（struct）

- `#if UNITY_IOS || UNITY_EDITOR` で囲む（EditMode テスト可能にする）。
- プロパティ:
  - `bool IsSuccess` — 共有シートを提示できたか。
  - `bool Completed` — Activity を完了したか（false = キャンセル）。
  - `string? ActivityType` — 選択 Activity の raw id。キャンセル/不明/失敗時 null。
  - `string? ErrorMessage` — `IsSuccess=false` のときのみ非 null。
- ファクトリ:
  - `static IosShareResult Success(bool completed, string? activityType)` → `(true, completed, activityType, null)`
  - `static IosShareResult Failure(string? error)` → `(false, false, null, error)`
- 不変条件: `IsSuccess=true ⇒ ErrorMessage=null` / `IsSuccess=false ⇒ Completed=false, ActivityType=null`。

### 5.2 `IosSharePayloads`

- `[Serializable] sealed class IosShareItem`
  - `public string type = "text";`（`"text"|"url"|"image"|"file"`）
  - `public string value = string.Empty;`
  - 生成ヘルパ（可読性のため。任意だが推奨）:
    - `static IosShareItem Text(string value)`
    - `static IosShareItem Url(string value)`
    - `static IosShareItem Image(string path)`
    - `static IosShareItem File(string path)`
- `[Serializable] sealed class IosShareContentPayload`
  - `public IosShareItem[] items = Array.Empty<IosShareItem>();`（必須・非空）
  - `public string? subject;`
  - `public string? previewTitle;`
  - `public string[]? excludedActivityTypes;`
- 各 public 型・public helper には英語 XML ドキュメントコメントを必ず付ける（`csharp.md` 準拠）。
  型が増えた場合はファイル分割を検討（v1 は同居で可）。

### 5.3 `IosShareJsonBuilder`（static）

- `AndroidShareJsonBuilder` の手書きシリアライザ（`StringBuilder` + エスケープ）を踏襲。
- `static string BuildShareContentJson(IosShareContentPayload payload)`:
  - `items`: 各 `IosShareItem` を `{ "type": type, "value": value }` として配列化（必須）。
  - `subject` / `previewTitle`: 非空のときのみ追加（`AddIfNotNullOrWhiteSpace` 相当）。
  - `excludedActivityTypes`: null/空でなければ String 配列として追加。
  - 文字列エスケープ（`"` `\\` 制御文字 `\uXXXX`）は Android 版のロジックを再利用。
- **builder の入力契約（v2 確定）**:
  - `items` 内の `null` 要素は native の「無効要素は無視」方針に合わせ、**serializer 側で除外**する
    （例外にはしない）。除外は出力配列からの skip とし、順序は保持する。
  - `type` 値は検証せずそのまま出力する（未知 type は native 側が無視）。
  - `value` は空文字でもそのまま出力する（有効性判断は native 側）。
  - この「null 要素除外」「空 value 素通し」を EditMode テストで固定する（7.1 参照）。

### 5.4 `IosShareManager`（MonoBehaviour Singleton）

- クラス全体を `#if UNITY_IOS` で囲む（`IosNotificationManager` / `IosDialogManager` と同一方針）。
  - **platform guard 方針（v2 確定）**: `UNITY_IOS` は「ビルドターゲット = iOS」のとき Editor でも定義される。
    したがって iOS ビルドターゲット選択中は Editor でも `IosShareManager` 型が存在する。
    Editor Play（`Application.platform != IPhonePlayer`）での native 呼び出し可否は、後述の
    ランタイムガードで制御する。
  - サンプル UI / Editor 疎通テストからの参照は `#if UNITY_IOS` の内側で行う
    （`#if UNITY_IOS && !UNITY_EDITOR` にはしない。Editor での型参照・疎通テストを可能に保つため）。
    これは `IosNotificationManagerExampleController` と同じ参照境界。
- `private const string LogTag = "IosShareManager";`
- `public const string OperationShare = "share";`（result のログ・識別用）
- Singleton: `Instance`（GameObject 生成 + `DontDestroyOnLoad`）、`Awake`（`_instance` 設定、
  `DontDestroyOnLoad`、`_ = UnityMainThreadDispatcher.Instance`）。`IosNotificationManager` と同型。
- イベント:
  - `public event Action<IosShareResult>? ShareCompleted;`（成功・失敗いずれも常に発火）
- delegate / DllImport:
  ```csharp
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void ShareCallback(bool isSuccess, bool completed, string? activityType, string? errorMessage);

  [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
  private static extern void shareContent(string contentJson, ShareCallback callback);
  ```
- static delegate 保持（GC 防止）:
  ```csharp
  private static readonly ShareCallback s_shareDelegate = OnShareResult;
  ```
- per-call callback（last-registered wins。`IosNotificationManager` に準拠）:
  ```csharp
  private static Action<IosShareResult>? s_onShare;
  ```
- 公開メソッド `Share(IosShareContentPayload? payload, Action<IosShareResult>? onResult = null)`:
  - **シグネチャ（v2 確定）**: `payload` は nullable（`IosShareContentPayload?`）とし、defensive に null を扱う。
    nullable 有効環境で API 契約と実装を一致させる。
  1. 先頭で `Debug.Log`（全パラメータを **null 安全** に出力）。
     - 例: `payload?.items?.Length ?? 0`、`onResult != null` を出力し、ガード前に payload の
       プロパティへ直接アクセスしない。
  2. `s_onShare = onResult;`（以降どの分岐でも結果が必ず返るよう、先に登録する）
  3. 入力ガード（`IosDialogManager` 流儀。いずれも native を呼ばず即時 `FireResult`）:
     - `payload == null` または `payload.items == null || payload.items.Length == 0`
       → `FireResult(IosShareResult.Failure("No shareable items were provided."))` して return。
       （native の `noValidItems` 文言と一致させ、無駄な native 往復を避ける）
  4. **非 iOS / Editor ガード（v2 確定 = 即時 Failure に統一）**:
     - `if (Application.platform != RuntimePlatform.IPhonePlayer)` のとき、native をスキップし
       `FireResult(IosShareResult.Failure("iOS share is only available on an iOS device."))` して return。
     - 理由: 本 API は単一 result callback 形状のため、非 iOS で early-return して無応答にすると
       呼び出し側が永続的に結果待ちになる。`IosNotificationManager`（多操作・event 中心）とは
       利用体験の要件が異なるため、Share では「必ず結果を返す」方針を採る。
       この分岐は EditMode でテスト可能（7.1 参照）。
  5. `string json = IosShareJsonBuilder.BuildShareContentJson(payload);`
  6. `try { shareContent(json, s_shareDelegate); } catch (Exception ex) { FireResult(IosShareResult.Failure($"Internal error: {ex.Message}")); }`
- （任意）薄い便宜ラッパ:
  - `ShareText(string text, string? subject = null, Action<IosShareResult>? onResult = null)`
  - `ShareUrl(string url, string? subject = null, Action<IosShareResult>? onResult = null)`
  - いずれも `IosShareContentPayload` を組んで `Share(...)` へ委譲。実装簡素化のため v1 では
    `Share(payload, onResult)` を主 API とし、便宜ラッパは実装時に要否判断（サンプル設計に合わせる）。

#### callback 提供方針（共通イベント + 個別 callback）

- 共通イベント `ShareCompleted`（`event Action<IosShareResult>`）: 常に発火。横断ハンドリング用。
- 個別 callback `onResult`（`Action<IosShareResult>? = null`）: 任意。未指定でも共通イベントは発火。
- last-registered wins: 同一 `Share` 連続呼び出しでは最後に登録した `onResult` が有効
  （`s_onShare` を上書き）。`IosNotificationManager` の per-call 方式に準拠。
- **dispatch 順序: 共通イベント → 個別 callback**（`AndroidShareManager` および design-feature 規定に一致）。
  - 注: `IosNotificationManager` は歴史的に「個別 → 共通」の順だが、本 Share 機能は Android 版と
    挙動を揃えるため「共通 → 個別」で統一する。設計上の意図として明記。

#### 結果 dispatch（`OnShareResult` / `FireResult` / テストシーム）

- **テスト可能性（v2 確定）**: dispatch の中核ロジック（「共通 → 個別」の順序、`s_onShare` の
  スナップショット + クリア、例外時の握りつぶし）を、Unity ライフサイクルに依存しない
  `internal static` の純粋ヘルパ `InvokeInOrder` に切り出し、EditMode から直接検証できるようにする。
  - テストアセンブリには `[assembly: InternalsVisibleTo("...Tests")]` を付与
    （Runtime asmdef 側で Internals 可視を設定。既存構成に合わせて実装時に確定）。
  - `FireResult` は「callback スナップショット + main-thread へ enqueue」の薄いラッパに留める。

```csharp
[MonoPInvokeCallback(typeof(ShareCallback))]
private static void OnShareResult(bool isSuccess, bool completed, string? activityType, string? errorMessage)
{
    var result = isSuccess
        ? IosShareResult.Success(completed, activityType)
        : IosShareResult.Failure(errorMessage);
    FireResult(result);
}

private static void FireResult(IosShareResult result)
{
    var cb = s_onShare;      // スナップショット（連続呼び出し対策）
    s_onShare = null;
    var common = _instance?.ShareCompleted;
    UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, common, cb));
}

// EditMode から native / MonoBehaviour 非依存で検証できる純粋ヘルパ。
// 順序（共通 → 個別）と例外握りつぶしをここに集約する。
internal static void InvokeInOrder(
    IosShareResult result,
    Action<IosShareResult>? common,
    Action<IosShareResult>? perCall)
{
    try
    {
        common?.Invoke(result); // 共通
        perCall?.Invoke(result); // 個別
    }
    catch (Exception ex)
    {
        Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] {ex.Message}");
    }
}
```

- native 契約上コールバックはメインスレッドで来るが、既存 iOS Manager と同様に
  `UnityMainThreadDispatcher` を必ず経由し、スレッド契約を統一する。

### 5.5 スレッド契約 / メモリ契約 / エラー契約

- スレッド契約:
  - native コールバックはメインスレッド保証（Bridge ヘッダ）だが、`UnityMainThreadDispatcher.Instance.Enqueue`
    経由で Unity API 呼び出しを行う（既存 iOS 実装と統一）。
  - `s_shareDelegate` を static readonly で保持し、native 関数ポインタが GC 回収されないようにする。
- メモリ契約:
  - `contentJson`（string）は IL2CPP marshal が `const char*` へ自動変換。手動 `AllocHGlobal` は不要
    （ActionSheet のような string[] 渡しではないため）。
  - コールバックの `const char*`（activityType/errorMessage）は IL2CPP が managed string へ自動コピー。
    ポインタを保持しない。
- エラー契約:
  - `IsSuccess=false` のとき `ErrorMessage` 非 null を保証。
  - `IsSuccess=true` のとき `ErrorMessage=null`。キャンセルは `IsSuccess=true, Completed=false`。

### 5.6 依存関係の実装順序

1. `IosShareResult`（他が依存）
2. `IosSharePayloads`
3. `IosShareJsonBuilder`（Payload に依存）
4. `IosShareManager`（上記すべてに依存）
5. テスト（`IosShareJsonBuilderTests`、`ShareResultTests` への追加）

### 5.7 IL2CPP / AOT 制約

- コールバック `OnShareResult` は `static` かつ `[MonoPInvokeCallback(typeof(ShareCallback))]` を付与。
- `s_shareDelegate` を static フィールドで永続保持（GC 防止）。
- `AndroidJavaProxy` は iOS では不使用（`DllImport` 方式）。

---

## 6. エラーケース一覧と返却仕様（層別）

### 6.1 parser 層（native `UnityIosShareJsonParser`）

| 条件 | 返却 |
| ---- | ---- |
| JSON 構文エラー / `items` 不在 | `isSuccess=false`, `errorMessage="Invalid share content JSON."` |
| 未知 `type` / `value` 欠落の要素 | エラーにせず無視（該当要素のみ除外） |

### 6.2 use case / repository 層（native `ShareError`）

| 条件 | errorMessage |
| ---- | ------------ |
| 有効アイテムが 0 件 | `No shareable items were provided.` |
| URL 文字列が不正 | `Invalid URL: {value}.` |
| 画像ロード失敗 | `Failed to load image at path: {path}.` |
| ファイル不存在 | `File not found at path: {path}.` |
| ルート VC 不在 | `No root view controller available to present the share sheet.` |
| 提示失敗 | `Failed to present the share sheet: {desc}.` |
| 不明エラー | `An unknown error occurred: {desc}.` |

### 6.3 C# Bridge 層（`IosShareManager`）

| 条件 | 返却 |
| ---- | ---- |
| `payload` が null / `items` が null または空 | `IosShareResult.Failure("No shareable items were provided.")`（native 呼び出しなし） |
| 非 iOS プラットフォーム（Editor 等） | `IosShareResult.Failure("iOS share is only available on an iOS device.")`（native 呼び出しなし、即時 Failure）。**v2 で確定**: early-return による無応答は行わない |
| `shareContent` 呼び出しで例外 | `IosShareResult.Failure($"Internal error: {ex.Message}")` |

- 補足（非 iOS 時の挙動・v2 確定）: 単一 result callback 形状の API のため、非 iOS では
  **必ず即時 Failure を返す**（共通イベント `ShareCompleted` と個別 `onResult` の両方が発火する）。
  呼び出し側が永続的に結果待ちにならないことを保証する。
  `IosNotificationManager`（多操作・event 中心）とは要件が異なるため、この点だけ挙動を分ける。
  サンプル側の Editor 表示ハンドリングはサンプルシーン設計で扱う。

---

## 7. テスト方針

### 7.1 EditMode（NUnit / ネイティブ呼び出しなし）

- `IosShareJsonBuilderTests`（新規）:
  - items（text/url/image/file 各 type）が `{ "type", "value" }` で正しく直列化される。
  - `subject` / `previewTitle`: 指定時に含まれ、null/空白時に省略される。
  - `excludedActivityTypes`: 指定時に配列出力、null/空時に省略される。
  - 特殊文字（`"` `\` 改行等）が正しくエスケープされる。
  - items 空配列でもクラッシュせず `"items":[]` を出力する（空判定は Manager 側の責務）。
  - **null 要素除外（v2 追加）**: `items` に `null` 要素が混入しても例外にならず、出力配列から
    除外される（順序保持）。
  - **空 value 素通し（v2 追加）**: `value == ""` の item はそのまま `"value":""` として出力される。
- `ShareResultTests`（追加）:
  - `IosShareResult.Success(completed:true, activityType:"com.apple...")` → `IsSuccess=true, ErrorMessage=null`。
  - `Success(completed:false, null)`（キャンセル相当）→ `IsSuccess=true, Completed=false, ActivityType=null`。
  - `IosShareResult.Failure("...")` → `IsSuccess=false, Completed=false, ActivityType=null, ErrorMessage` 設定。
  - 不変条件（`IsSuccess=true ⇒ ErrorMessage=null`）。
- `IosShareManagerDispatchTests`（新規・v2 追加。`InvokeInOrder` シームを直接検証。native 非依存）:
  - **dispatch 順序**: 共通イベント → 個別 callback の順で 1 回ずつ呼ばれる
    （呼び出し順を記録して検証）。
  - 個別 callback のみ / 共通のみ / 両方 null の各パターンで例外が出ない。
  - 個別 callback 内で例外が発生しても共通イベントは既に呼ばれており、例外は握りつぶされる
    （`LogAssert` で `Debug.LogError` を許容）。
  - 補足: `s_onShare` のスナップショット + クリア（last-registered wins / 連続呼び出しで
    前の callback が二重発火しない）は、`InvokeInOrder` に callback を渡す設計により
    純粋関数として検証可能。`FireResult` 経由のクリア挙動は Manager 統合の PlayMode で確認する。

### 7.2 PlayMode

- v1 では必須としない（native Bridge 依存が大半のため）。
- 任意で以下を検討（native を呼ばない範囲）:
  - 非 iOS/Editor で `Share(payload)` を呼ぶと `ShareCompleted` と `onResult` の両方に
    即時 Failure（`"iOS share is only available on an iOS device."`）が届く。
  - `payload == null` / `items` 空で `"No shareable items were provided."` の Failure が届く。
  - これらは `UnityMainThreadDispatcher` を pump する必要があるため PlayMode（or `UnityTest` コルーチン）で実施。

### 7.3 手動確認（実機 iOS 18+）

- テキスト共有 → 共有シート提示 → アプリ選択で `completed=true`, `activityType` 取得。
- 共有シートをキャンセル → `isSuccess=true, completed=false, activityType=null`。
- 不正ファイルパスで file 共有 → `File not found at path: ...`。
- 不正 URL で url 共有 → `Invalid URL: ...`。
- `excludedActivityTypes` 指定 → 該当 Activity が非表示。
- iPad での提示（popover 提示）に問題がないか（native 側 presenter の責務だが実機確認）。

---

## 8. 補足・留意点

- 本計画書は Runtime 実装のみを対象とする。サンプル UI（`IosShareManagerExampleController` 等）は
  `design-sample-scene` で別途設計する。
- dispatch 順序は Share 機能内（Android/iOS）で「共通 → 個別」に統一する。
- 第 0 章の XCFramework 更新（1.2.0）は対応済みのため、C# 実装に着手可能。

---

## 9. レビュー指摘の反映（v1 → v2）

対象レビュー: `artifact/reviews/share/2026-07-05-ios-share-design-review.md`

| # | 優先度 | 指摘 | v2 での確定方針 | 反映箇所 |
| - | ----- | ---- | -------------- | ------- |
| 1 | high | `IosShareManager` の platform guard と Editor 参照方針が未確定 | `#if UNITY_IOS`（iOS ビルドターゲット時は Editor でも型が存在）。サンプル/Editor 疎通テストは `#if UNITY_IOS` 内側で参照し `!UNITY_EDITOR` は付けない。native 呼び出し可否はランタイムガードで制御 | 5.4 冒頭 |
| 2 | high | 非 iOS 時に callback/event 未発火で無応答になる | **即時 Failure に統一**（`"iOS share is only available on an iOS device."`）。共通 + 個別の両方が必ず発火。テスト追加 | 5.4 手順4 / 6.3 / 7.1 / 7.2 |
| 3 | medium | `payload` が nullable でない | シグネチャを `IosShareContentPayload? payload` に変更。先頭 `Debug.Log` は null 安全に出力 | 5.4 手順1・シグネチャ |
| 4 | medium | builder の null 要素・空 value の扱いが未定義 | null 要素は serializer 側で除外（native 互換、例外にしない）。空 value は素通し。テストで固定 | 5.3 入力契約 / 7.1 |
| 5 | medium | Manager dispatch の EditMode 検証が不足 | dispatch 中核を `internal static InvokeInOrder` に切り出し、`InternalsVisibleTo` で EditMode 検証。順序・例外握りつぶしをテスト | 5.4 dispatch / 4.2 / 7.1 |
| 6 | low | public 型の XML コメント明示 | 各 public 型・helper に英語 XML コメント必須を明記 | 5.2 |
| 7 | low | xcframework の nm 確認コマンド省略 | 確認コマンドと期待出力を記録 | 4.5 |

- 不足項目（レビュー「不足項目」節）はすべて上記で確定済み。
