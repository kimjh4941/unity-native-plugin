# macOS Share 実装計画書 (v1)

- 対象機能: share
- 対象プラットフォーム: macOS (Standalone)
- 作成日: 2026-07-12
- 対象範囲: Bridge / Manager / Payload / JsonBuilder / Result / テスト
- 対象外: ExampleController・UXML/USS・サンプルシーン（`design-sample-scene` スキルで別途設計）

---

## 0. 前提と現状

- native-toolkit 側の macOS Share 実装は完成済み。同梱バンドル `Plugins/macOS/unity-mac-native-toolkit-1.2.0.xcframework` に `UnityMacShareManager` / C Bridge が含まれることを確認済み。
- 本プロジェクトの C# 側は iOS / Android の Share のみ実装済みで、**macOS Share は未実装**。本計画は macOS 版 C# 層の新規追加。
- 既存の Share 実装のうち、最も新しく testable な **iOS Share パターン（`IosShareManager`）を macOS 版の下敷き**とする。

---

## 1. native-toolkit 確認結果（macOS）

参照: `native-toolkit/mac/UnityMacPlugin/UnityMacPlugin/Share/` および背後の `MacLibrary/Share/`

### 1.1 公開 C 関数（C# が P/Invoke する対象）

`UnityMacShareManagerBridge.h` / `.m`

| 関数 | シグネチャ | 用途 |
| --- | --- | --- |
| `shareContent` | `void shareContent(const char* contentJson, ShareCallback callback)` | 共有サービスピッカー（NSSharingServicePicker）を提示 |
| `shareViaService` | `void shareViaService(const char* serviceName, const char* contentJson, ShareCallback callback)` | 指定サービスを直接実行（ピッカー UI なし） |

### 1.2 コールバック型

```c
typedef void (*ShareCallback)(bool isSuccess,
                              bool completed,
                              const char* serviceName,
                              const char* errorMessage);
```

- `isSuccess`: 提示/実行できたか。`false` はエラー（無効 JSON・アンカー無し等）のみ。
- `completed`: ユーザーがサービスを完了したか。`false` はユーザーキャンセル（**エラーではない**）。
- `serviceName`: 選択されたサービスの表示名（`NSSharingService.title`）。キャンセル/不明/失敗時は NULL。
- `errorMessage`: `isSuccess == false` のときのみ非 NULL。それ以外は NULL。
- **スレッド**: 呼び出しは任意スレッド可。**コールバックは常にメインスレッド**で配信される。
- **ポインタ寿命**: 生ポインタをコールバック後に保持しない（既定マーシャリングで即 `string` にコピーされる想定）。

### 1.3 定数（サービス名）

- `serviceName` は raw な `NSSharingService.Name` 値（例: `"com.apple.share.Mail.compose"`）。ネイティブ側に固定の公開定数は無い。C# 側では任意文字列として受け渡す（既知値を参考用に定数化する程度）。

### 1.4 JSON スキーマ（`UnityMacShareJsonParser`）

C# → native に渡す `contentJson` のキー:

| キー | 型 | 必須 | 備考 |
| --- | --- | --- | --- |
| `items` | Array | 必須 | 各要素 `{ "type": String, "value": String }` |
| `recipients` | Array<String> | 任意 | Mail 等の宛先 |
| `subject` | String | 任意 | Mail 等の件名 |
| `excludedServiceTitles` | Array<String> | 任意 | `NSSharingService.title` に対するベストエフォート除外 |

- `type` の許容値: `"text"` / `"url"` / `"image"` / `"file"`。未知 `type`・`value` 欠落エントリは**無視**（JSON 構文エラーのみ parse 失敗）。
- **iOS スキーマとの相違（重要）**: iOS は `previewTitle` / `excludedActivityTypes` を使うが、macOS は **`recipients` / `subject` / `excludedServiceTitles`**。iOS の Payload/JsonBuilder を流用せず macOS 専用に作る。

### 1.5 エラー仕様（MacLibrary `ShareError`）

use case / repository 層のドメインエラー。C Bridge には `errorMessage`（文字列）のみ伝播し、`errorCode` は Bridge の `ShareCallback` に**露出しない**（C# は文言のみ受領）。

| ケース | code(参考) | errorMessage |
| --- | --- | --- |
| noValidItems | 1401 | `No shareable items were provided.` |
| invalidURL | 1402 | `Invalid URL: {value}.` |
| imageLoadFailed | 1403 | `Failed to load image at path: {path}.` |
| fileNotFound | 1404 | `File not found at path: {path}.` |
| noAnchorView | 1405 | `No key window available to anchor the sharing picker.` |
| serviceUnavailable | 1406 | `Sharing service unavailable: {name}.` |
| presentationFailed | 1407 | `Failed to share: {desc}.` |
| alreadyInProgress | 1408 | `A share operation is already in progress.` |
| unknown | 1499 | `An unknown share error occurred: {desc}.` |

parser 層の失敗（無効 JSON）: `Invalid share content JSON.`

### 1.6 mouseDown 要件（要検証）

- `shareContent`（ピッカー）は `NSSharingServicePicker.show(...)` の仕様上、**ユーザー起点アクション（mouseDown コンテキスト）から呼ぶ必要**がある。ネイティブ実装は `Task { @MainActor }` ホップを挟むため mouseDown コンテキスト保証が崩れる可能性があり、**実クリックでの動作は未検証（best-effort）**とネイティブ doc に明記されている。
- **`shareViaService`（直接実行）が verified-safe path**。信頼性が必要な場面はこちらを推奨。
- 本計画では両 API を提供しつつ、ピッカー動作は手動確認項目（要検証）とする。

---

## 2. 既存 C# 実装の確認結果

参照: `Packages/com.jonghyunkim.nativetoolkit/Runtime/`

- **Manager パターン（`IosShareManager`）**: `MonoBehaviour` Singleton、`#if UNITY_IOS || UNITY_EDITOR` でクラスをコンパイル（Editor から参照/テスト可能）、native P/Invoke と `[MonoPInvokeCallback]` は `#if UNITY_IOS && !UNITY_EDITOR`。共通イベント `ShareCompleted` + per-call `onResult`、`InvokeInOrder`（共通→個別、例外 swallow）を `internal static` で公開しテスト可能。GC 防止のため static readonly delegate を保持。
- **macOS 既存 Manager（`MacNotificationManager` / `MacDialogManager`）**: `#if UNITY_STANDALONE_OSX`、`[DllImport("__Internal")]` + `[MonoPInvokeCallback]`、`UnityMainThreadDispatcher` 経由でメインスレッド転送。→ macOS の P/Invoke ライブラリ名・callback 方式はこれに準拠。
- **Result（`IosShareResult` / `MacNotificationResult`）**: `readonly struct`、`Success` / `Failure` ファクトリ、`Failure` は `ErrorMessage` 非 null を正規化。`MacNotificationResult` は `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`。
- **JsonBuilder（`IosShareJsonBuilder`）**: 外部 JSON ライブラリを使わない手書きシリアライザ。ガードなし（EditMode テスト可能）。
- **Payloads（`IosSharePayloads`）**: `[Serializable]` の Item + ContentPayload、`Text/Url/Image/File` ファクトリ。ガードなし。
- `AssemblyInfo.cs`: `InternalsVisibleTo("NativeToolkit.Runtime.Tests")` 設定済み → `internal` の `InvokeInOrder` をテストから参照可能。
- **重複実装しないもの**: 共通 `ShareOperationResult` / `ShareCallbackResult` / `ShareChooserActionResult` / `ShareChooserActionCallbackCoordinator` は Android chooser 用。macOS では使用しない。`UnityMainThreadDispatcher` は再利用。

### 2.1 設計方針（iOS パターン採用の理由）

- macOS の native Share コールバック契約 `(isSuccess, completed, serviceName, errorMessage)` は iOS Share `(isSuccess, completed, activityType, errorMessage)` とほぼ同型。
- iOS パターンは `InvokeInOrder` を切り出し EditMode で dispatch 順序を検証できる（notification パターンは Editor 非コンパイルで dispatch テスト不可）。
- よって **クラスガードは `#if UNITY_STANDALONE_OSX || UNITY_EDITOR`**、native P/Invoke・`[MonoPInvokeCallback]` は `#if UNITY_STANDALONE_OSX && !UNITY_EDITOR` とする。

---

## 3. 実装対象 API 一覧

### 3.1 P/Invoke（`#if UNITY_STANDALONE_OSX && !UNITY_EDITOR`）

```csharp
[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void shareContent(string contentJson, ShareCallback callback);

[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
private static extern void shareViaService(string serviceName, string contentJson, ShareCallback callback);
```

### 3.2 delegate

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void ShareCallback(bool isSuccess, bool completed, string? serviceName, string? errorMessage);
```

---

## 4. 変更ファイル一覧

`.meta` は記載しない。ExampleController/UXML/USS/サンプルシーンは対象外。

### 4.1 新規作成（Runtime）

| パス | 内容 |
| --- | --- |
| `Runtime/Share/MacSharePayloads.cs` | `MacShareItem`（Text/Url/Image/File ファクトリ）+ `MacShareContentPayload`（items / recipients / subject / excludedServiceTitles）。ガードなし |
| `Runtime/Share/MacShareJsonBuilder.cs` | 手書きシリアライザ。`items` / `recipients` / `subject` / `excludedServiceTitles` を macOS スキーマで生成。`IosShareJsonBuilder` の Append 系ロジックを踏襲。ガードなし |
| `Runtime/Share/MacShareResult.cs` | `readonly struct`（`Operation` / `IsSuccess` / `Completed` / `ServiceName` / `ErrorMessage`）+ `Success` / `Failure`。`#if UNITY_STANDALONE_OSX || UNITY_EDITOR` |
| `Runtime/Share/MacShareManager.cs` | `MonoBehaviour` Singleton。`Share` / `ShareViaService` を公開。`#if UNITY_STANDALONE_OSX || UNITY_EDITOR`（native は `&& !UNITY_EDITOR`） |

### 4.2 新規作成（Tests）

| パス | 内容 |
| --- | --- |
| `Tests/Runtime/MacShareJsonBuilderTests.cs` | JSON 生成の exact 検証（各 item type / recipients / subject / excludedServiceTitles / エスケープ / null 除外 / 空 optional 省略） |
| `Tests/Runtime/MacShareResultTests.cs` | `Success` / `Failure` ファクトリ、`ErrorMessage` 非 null 正規化、`ServiceName` 保持 |
| `Tests/Runtime/MacShareManagerDispatchTests.cs` | `InvokeInOrder`（共通→個別の順序・両 null・片方例外時の swallow と共通先行発火） |

### 4.3 非変更（参照のみ）

- `Runtime/Common/UnityMainThreadDispatcher.cs`（再利用）
- `Runtime/Share/Ios*` / `Android*` / 共通 `Share*`（変更なし）
- `Runtime/AssemblyInfo.cs`（`InternalsVisibleTo` 既存で追加不要）
- 同梱バンドル `Plugins/macOS/unity-mac-native-toolkit-1.2.0.xcframework`（変更なし）

---

## 5. 実装詳細

### 5.1 `MacShareContentPayload` / `MacShareItem`

```csharp
[Serializable] public sealed class MacShareItem
{
    public string type = "text";
    public string value = string.Empty;
    public static MacShareItem Text(string value)  => new() { type = "text",  value = value };
    public static MacShareItem Url(string value)   => new() { type = "url",   value = value };
    public static MacShareItem Image(string path)  => new() { type = "image", value = path };
    public static MacShareItem File(string path)   => new() { type = "file",  value = path };
}

[Serializable] public sealed class MacShareContentPayload
{
    public MacShareItem[] items = Array.Empty<MacShareItem>();   // 必須・非空
    public string[]? recipients;             // 任意
    public string? subject;                  // 任意
    public string[]? excludedServiceTitles;  // 任意
}
```

### 5.2 `MacShareJsonBuilder`

- `BuildShareContentJson(MacShareContentPayload)` を公開。
- `items` は常に出力（null エントリは防御的に除外）。`recipients` / `excludedServiceTitles` は null/空なら省略、`subject` は null/空白なら省略。
- 文字列エスケープ・`StringBuilder` による構築は `IosShareJsonBuilder` の private ヘルパ（`AppendValue` / `AppendObject` / `AppendArray` / `AppendEscapedString`）と同一方針で新規実装（共有化はしない＝既存 iOS 実装を変更しない）。

### 5.3 `MacShareResult`

```csharp
public readonly struct MacShareResult
{
    public string Operation { get; }       // "share" | "shareViaService"
    public bool IsSuccess { get; }
    public bool Completed { get; }         // false = ユーザーキャンセル（エラーではない）
    public string? ServiceName { get; }    // 選択サービス表示名 / 失敗・キャンセル時 null
    public string? ErrorMessage { get; }   // IsSuccess=false のとき非 null 保証

    public static MacShareResult Success(string operation, bool completed, string? serviceName);
    public static MacShareResult Failure(string operation, string? error); // null/空は "Unknown error." に正規化
}
```

### 5.4 `MacShareManager`

- `LogTag = "MacShareManager"`。定数 `OperationShare = "share"`, `OperationShareViaService = "shareViaService"`。
- Singleton は既存 macOS Manager と同一パターン（`Instance` getter + `Awake` + `OnDestroy`、`Awake` で `UnityMainThreadDispatcher.Instance` を先行生成）。

#### callback 提供方針（既存 Manager パターン準拠）

- 共通イベント: `public event Action<MacShareResult>? ShareCompleted` — `Share` / `ShareViaService` いずれの完了でも**常に発火**。
- 個別 callback: `Action<MacShareResult>? onResult = null` — 各操作メソッドの引数。任意。未指定でも共通イベントは発火。
- 個別 callback は `IosNotificationManager` / `IosShareManager` の per-call 方式に準拠。同一操作の連続呼び出しは **last-registered wins**。
- **dispatch 順序**: 共通イベント → 個別 callback（`InvokeInOrder(result, common, perCall)` で共通を先に発火し、両者を try/catch で包み例外を swallow）。iOS と同順。
- 2 操作の混線を避けるため per-call フィールドを操作ごとに分離（`s_onShare` / `s_onShareViaService`）、それぞれ専用の static delegate（`s_shareDelegate` / `s_shareViaServiceDelegate`）を持つ。

```csharp
public void Share(MacShareContentPayload? payload, Action<MacShareResult>? onResult = null);
public void ShareViaService(string serviceName, MacShareContentPayload? payload, Action<MacShareResult>? onResult = null);
```

- 処理フロー（`Share` 例）:
  1. 先頭 `Debug.Log`（全パラメータ）。
  2. per-call callback を保存。
  3. payload / items 検証 → NG なら即 `Failure` を dispatch（native 呼び出しなし）。
  4. `Application.platform != RuntimePlatform.OSXPlayer` → 即 `Failure`。
  5. `MacShareJsonBuilder.BuildShareContentJson` → `try { shareContent(json, s_shareDelegate); } catch { Failure }`。
- `ShareViaService` は加えて `serviceName` の null/空検証を行う。

#### スレッド契約

- native はメインスレッドで callback するが、Unity API 安全のため `[MonoPInvokeCallback]` static メソッド内で `UnityMainThreadDispatcher.Instance.Enqueue` により発火（既存 macOS Manager と同一）。

#### メモリ契約

- static callback delegate は `static readonly` フィールドで永続保持し GC を防止（`s_shareDelegate` / `s_shareViaServiceDelegate`）。
- 文字列は既定マーシャリング（`const char*` → `string`）で即コピーされ、手動 `Marshal` 解放は不要（Dialog の `IntPtr` 配列渡しのような手動確保・解放はなし）。

#### IL2CPP / AOT 制約

- callback は `static` かつ `[MonoPInvokeCallback(typeof(ShareCallback))]` を付与。
- delegate は `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`。
- （本 macOS 実装は `AndroidJavaProxy` を使わないため proxy 関連制約は非該当。）

### 5.5 実装順序（依存順）

1. `MacShareResult`（他が依存）
2. `MacSharePayloads`
3. `MacShareJsonBuilder`
4. `MacShareManager`
5. テスト（Result → JsonBuilder → Dispatch）

---

## 6. エラーケース一覧と返却仕様（層別）

### 6.1 C# Bridge 層（native 呼び出し前 / 例外）

| 条件 | 返却（`MacShareResult.Failure`） |
| --- | --- |
| `payload == null` / `items == null` / `items.Length == 0` | `No shareable items were provided.` |
| `ShareViaService` で `serviceName` が null/空 | `Sharing service name must not be empty.` |
| 非 macOS プレイヤー（`platform != OSXPlayer`） | `macOS share is only available on a macOS Standalone player.` |
| `shareContent` / `shareViaService` 呼び出しで例外 | `Internal error: {ex.Message}` |

### 6.2 parser 層（native `UnityMacShareJsonParser`）

| 条件 | errorMessage |
| --- | --- |
| JSON 構文エラー | `Invalid share content JSON.` |

### 6.3 use case / repository 層（MacLibrary `ShareError`）

§1.5 の 9 種（`No shareable items were provided.` / `Invalid URL: {value}.` / `Failed to load image at path: {path}.` / `File not found at path: {path}.` / `No key window available to anchor the sharing picker.` / `Sharing service unavailable: {name}.` / `Failed to share: {desc}.` / `A share operation is already in progress.` / `An unknown share error occurred: {desc}.`）。C# は `errorMessage` 文字列としてそのまま `ErrorMessage` に格納。

### 6.4 キャンセル（エラーではない）

- ユーザーキャンセル: `IsSuccess = true`, `Completed = false`, `ServiceName = null`, `ErrorMessage = null`。

---

## 7. テスト方針

### 7.1 EditMode（NUnit / native 呼び出しなし）

- `MacShareJsonBuilderTests`
  - 各 item type の exact JSON。
  - `recipients` / `subject` / `excludedServiceTitles` の出力・省略。
  - 特殊文字エスケープ、null エントリ除外、複数 item の順序保持。
- `MacShareResultTests`
  - `Success(op, completed, serviceName)` の各フィールド。
  - `Failure(op, null/"")` → `ErrorMessage == "Unknown error."`、`IsSuccess == false`。
- `MacShareManagerDispatchTests`
  - `InvokeInOrder` 共通→個別の順序、両 null、片方 null、per-call 例外時に共通が先行発火し例外 swallow（`LogAssert.Expect`）。

### 7.2 PlayMode

- `MacShareManager.Instance` 生成・イベント購読・非 OSX プレイヤーでの即時 `Failure` 経路（`RuntimePlatform` 依存のため Editor では `Failure` 分岐を検証）。

### 7.3 手動確認（実機 macOS 15+ / ネイティブ Bridge 依存）

- `ShareViaService`（verified-safe）: Mail 等で共有完了 → `Completed=true` / `ServiceName` 取得。
- `Share`（ピッカー・**要検証**）: 実クリック（mouseDown コンテキスト）からピッカー提示、選択完了・キャンセルの結果。mouseDown 要件は best-effort のため要検証項目として明記。
- キャンセル時に `IsSuccess=true, Completed=false`。
- 無効 URL / 存在しない画像・ファイルパスで対応する `ErrorMessage` が返ること。

---

## 8. 要検証事項

- ピッカー（`shareContent`）の mouseDown コンテキスト依存（§1.6）: 実機・実クリックでの安定動作は未検証。信頼性が必要な用途は `ShareViaService` を推奨。
- 既定マーシャリングでの `const char*`（NULL 含む）→ `string?` 変換の挙動（NULL → C# `null`）の実機確認。
- 同梱 `unity-mac-native-toolkit-1.2.0.xcframework` に `shareContent` / `shareViaService` の C シンボルが正しくエクスポートされ、`__Internal` から解決されること（実ビルドで確認）。

---

## 9. 出力範囲の明記

- 本計画は Bridge / Manager / Payload / JsonBuilder / Result / テストのみ。
- ExampleController・UXML/USS・サンプルシーンは `design-sample-scene` スキルで別途設計する。
