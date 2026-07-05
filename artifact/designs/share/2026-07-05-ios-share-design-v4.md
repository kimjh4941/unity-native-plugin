# iOS Share 機能 実装計画書 (v4)

- 対象機能: share（システム共有シート / `UIActivityViewController`）
- 対象プラットフォーム: iOS（最小 iOS 18）
- 作成日: 2026-07-05
- 対象範囲: `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/` の C# Bridge / Manager / Payload / JsonBuilder / Result と EditMode テスト
- 対象外: サンプルアプリ（ExampleController・UXML/USS・サンプルシーン）は `design-sample-scene` で別途設計
- v2 変更点: レビュー指摘（`2026-07-05-ios-share-design-review.md`）を反映。非 iOS 時の callback 挙動、nullable/null 要素の入力契約、Manager dispatch の EditMode 検証方針を確定。
- v3 変更点: **platform guard 戦略を「クラスは `UNITY_IOS || UNITY_EDITOR` で常時存在させ、native 呼び出しだけ `UNITY_IOS && !UNITY_EDITOR` に閉じる」へ変更**（再レビュー高優先度 #2 の代替案を採用）。これにより Editor から `IosShareManager` を全ビルドターゲットで参照可能にし、Manager 本体（入力ガード・非 iOS Failure・dispatch 順序）を EditMode で直接検証できるようにした。
- v4 変更点: 再レビュー（`...-review-v2.md`）を反映。(1) `Share` 手順の `json` 宣言前参照バグを修正、(2) `InvokeInOrder` の internal 可視化に必要な `Runtime/AssemblyInfo.cs`（`InternalsVisibleTo`）を変更ファイルに追加し §2.2 を訂正、(3) null item は defensive-only と明記（型は `IosShareItem[]` のまま）、(4) 実装順序に v3 追加テストを反映、(5) nm 期待値から固定アドレスを除去（第 9 章に集約）。

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
- アセンブリ定義（v4 訂正）:
  - 新規ファイルは既存 `Runtime/Share/` 配下に置くため、**asmdef ファイル（`NativeToolkit.Runtime.asmdef`）自体の変更は不要**。
  - ただし `IosShareManager.InvokeInOrder` を `internal` にしてテスト（`NativeToolkit.Runtime.Tests`）から
    直接検証するには、**Runtime アセンブリに `[assembly: InternalsVisibleTo("NativeToolkit.Runtime.Tests")]` 属性が必要**。
    これは asmdef のフィールドではなく C# 属性なので、新規 `Runtime/AssemblyInfo.cs` を追加して付与する（4.1 参照）。
  - テスト側 `NativeToolkit.Runtime.Tests.asmdef` は既に `NativeToolkit.Runtime` を参照し `includePlatforms: [Editor]`
    のため、追加参照設定は不要。

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
| `Runtime/Share/IosShareManager.cs` | MonoBehaviour Singleton。共通イベント + per-call callback + dispatch シーム。クラスは `#if UNITY_IOS \|\| UNITY_EDITOR`、native P/Invoke（`DllImport shareContent` + `MonoPInvokeCallback`）のみ `#if UNITY_IOS && !UNITY_EDITOR`（v3） |
| `Runtime/AssemblyInfo.cs` | `[assembly: InternalsVisibleTo("NativeToolkit.Runtime.Tests")]` を付与し、`InvokeInOrder`（internal）を EditMode テストから検証可能にする（v4 追加）。既存 AssemblyInfo は無いため新規作成 |

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
  # 期待: `T _shareContent` の行が 1 件返ること（先頭アドレスはビルドごとに変わるため一致判定に使わない）
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
- **`items` の null 要素契約（v4 確定 = defensive-only）**:
  - 通常の利用契約では `items` に `null` 要素を入れない（型は `IosShareItem[]` のまま。`IosShareItem?[]`
    にはしない。public API を nullable 配列にして常用させる意図はないため）。
  - ただし Unity serialization / 外部生成データ由来の不正状態に対する defensive 対応として、
    serializer は null 要素を除外する（5.3 参照）。
  - テストでは `null!` を用いて「不正状態 → 除外される」ことを検証する（7.1 参照）。
- 各 public 型・public helper には英語 XML ドキュメントコメントを必ず付ける（`csharp.md` 準拠）。
  型が増えた場合はファイル分割を検討（v1 は同居で可）。

### 5.3 `IosShareJsonBuilder`（static）

- `AndroidShareJsonBuilder` の手書きシリアライザ（`StringBuilder` + エスケープ）を踏襲。
- `static string BuildShareContentJson(IosShareContentPayload payload)`:
  - `items`: 各 `IosShareItem` を `{ "type": type, "value": value }` として配列化（必須）。
  - `subject` / `previewTitle`: 非空のときのみ追加（`AddIfNotNullOrWhiteSpace` 相当）。
  - `excludedActivityTypes`: null/空でなければ String 配列として追加。
  - 文字列エスケープ（`"` `\\` 制御文字 `\uXXXX`）は Android 版のロジックを再利用。
- **builder の入力契約（v2 確定 / v4 補足）**:
  - `items` 内の `null` 要素は defensive-only の扱い（5.2 参照）。native の「無効要素は無視」方針に
    合わせ、**serializer 側で除外**する（例外にはしない）。除外は出力配列からの skip とし、順序は保持する。
  - `type` 値は検証せずそのまま出力する（未知 type は native 側が無視）。
  - `value` は空文字でもそのまま出力する（有効性判断は native 側）。
  - この「null 要素除外」「空 value 素通し」を EditMode テストで固定する（7.1 参照）。

### 5.4 `IosShareManager`（MonoBehaviour Singleton）

- **platform guard 戦略（v3 確定）**: クラス全体は `#if UNITY_IOS || UNITY_EDITOR` で囲み、
  **全ビルドターゲットの Editor で型が存在する**ようにする。native への P/Invoke だけを
  `#if UNITY_IOS && !UNITY_EDITOR` に閉じる。
  - 理由:
    - Editor サンプル UI / EditMode テストがビルドターゲットに依存せず `IosShareManager` を
      参照できる（v2 の「クラス全体 `#if UNITY_IOS`」だと非 iOS ターゲットの Editor で型が消え、
      参照側の実装が分岐する問題を解消。再レビュー高優先度 #2 の代替案を採用）。
    - Manager 本体（入力ガード・非 iOS Failure・dispatch 順序）を EditMode で直接検証できる（7.1 参照）。
  - guard 内訳:
    - クラス宣言・イベント・Singleton・`Share`・`FireResult`・`InvokeInOrder` 等: `#if UNITY_IOS || UNITY_EDITOR`。
    - `[DllImport("__Internal")] shareContent(...)` の extern 宣言と、`shareContent(...)` の実呼び出し・
      `s_shareDelegate` / `OnShareResult`（`[MonoPInvokeCallback]`）: `#if UNITY_IOS && !UNITY_EDITOR`。
    - Editor（`UNITY_EDITOR`）や実機 iOS 以外では native シンボルを参照しないため、Mono/Editor で
      リンク不能にならない。
  - サンプル UI からの参照境界も同様（`#if UNITY_IOS || UNITY_EDITOR` 内で型参照可。native 実行は実機のみ）。
    詳細な UI 側 guard はサンプルシーン設計で確定する。
- `private const string LogTag = "IosShareManager";`
- `public const string OperationShare = "share";`（result のログ・識別用）
- Singleton: `Instance`（GameObject 生成 + `DontDestroyOnLoad`）、`Awake`（`_instance` 設定、
  `DontDestroyOnLoad`、`_ = UnityMainThreadDispatcher.Instance`）。`IosNotificationManager` と同型。
- イベント:
  - `public event Action<IosShareResult>? ShareCompleted;`（成功・失敗いずれも常に発火）
- delegate / DllImport（**native 参照部は `#if UNITY_IOS && !UNITY_EDITOR`**）:
  ```csharp
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void ShareCallback(bool isSuccess, bool completed, string? activityType, string? errorMessage);

#if UNITY_IOS && !UNITY_EDITOR
  [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
  private static extern void shareContent(string contentJson, ShareCallback callback);

  // native 関数ポインタの GC 回収防止（実機のみ保持）
  private static readonly ShareCallback s_shareDelegate = OnShareResult;
#endif
  ```
  - `ShareCallback` delegate 型自体はガード外（`UNITY_IOS || UNITY_EDITOR`）に置いてもよいが、
    実際に native へ渡す `s_shareDelegate` と extern は実機ガード内に閉じる。
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
  4. **native 呼び出し / 非 iOS・Editor ガード（即時 Failure に統一。v4 で json 生成位置を確定）**:
     - native 実呼び出しは `#if UNITY_IOS && !UNITY_EDITOR` に閉じ、Editor / 非 iOS ビルドでは
       `#else` 経路で即時 Failure を返す。**`json` は実機ガードブロック内で `try` 直前に生成する**
       （Editor 経路では json を生成しない）。ランタイムでも `Application.platform` をガードする:
       ```csharp
       #if UNITY_IOS && !UNITY_EDITOR
           if (Application.platform != RuntimePlatform.IPhonePlayer)
           {
               FireResult(IosShareResult.Failure("iOS share is only available on an iOS device."));
               return;
           }
           string json = IosShareJsonBuilder.BuildShareContentJson(payload);
           try { shareContent(json, s_shareDelegate); }
           catch (Exception ex) { FireResult(IosShareResult.Failure($"Internal error: {ex.Message}")); }
       #else
           FireResult(IosShareResult.Failure("iOS share is only available on an iOS device."));
       #endif
       ```
     - 理由: 本 API は単一 result callback 形状のため、非 iOS で無応答にすると呼び出し側が永続的に
       結果待ちになる。`IosNotificationManager`（多操作・event 中心）とは要件が異なるため、
       Share では「必ず結果を返す」方針を採る。`#else` 経路は Editor で有効なので
       EditMode で直接テスト可能（7.1 参照）。
     - `json` を実機ガード内で宣言することで、`#else` 経路の未使用変数警告や、v3 で残っていた
       「`json` 宣言前参照」バグを回避する。
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

- **テスト可能性（v2 確定・v3 で guard 整理）**: dispatch の中核ロジック（「共通 → 個別」の順序、
  `s_onShare` のスナップショット + クリア、例外時の握りつぶし）を、Unity ライフサイクル・native に
  依存しない `internal static` の純粋ヘルパ `InvokeInOrder` に切り出し、EditMode から直接検証できるようにする。
  - `OnShareResult`（`[MonoPInvokeCallback]`・native callback）は `#if UNITY_IOS && !UNITY_EDITOR` 内に置く。
  - `FireResult` / `InvokeInOrder` / `s_onShare` は `#if UNITY_IOS || UNITY_EDITOR`（常時コンパイル）側に置き、
    Editor 経路（入力ガード・非 iOS Failure）と共有する。
  - テストアセンブリには `[assembly: InternalsVisibleTo("...Tests")]` を付与
    （Runtime asmdef 側で Internals 可視を設定。既存構成に合わせて実装時に確定）。
  - `FireResult` は「callback スナップショット + main-thread へ enqueue」の薄いラッパに留める。

```csharp
#if UNITY_IOS && !UNITY_EDITOR
[MonoPInvokeCallback(typeof(ShareCallback))]
private static void OnShareResult(bool isSuccess, bool completed, string? activityType, string? errorMessage)
{
    var result = isSuccess
        ? IosShareResult.Success(completed, activityType)
        : IosShareResult.Failure(errorMessage);
    FireResult(result);
}
#endif

// 以降は #if UNITY_IOS || UNITY_EDITOR 側（Editor 経路と共有）
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
4. `IosShareManager`（上記すべてに依存）+ `Runtime/AssemblyInfo.cs`（`InternalsVisibleTo`）
5. テスト:
   - `IosShareJsonBuilderTests`（EditMode）
   - `ShareResultTests` への `IosShareResult` テスト追加（EditMode）
   - `IosShareManagerDispatchTests`（EditMode。`InvokeInOrder` 検証）
   - 必要に応じて Manager 統合の `UnityTest`（PlayMode。入力ガード・非 iOS Failure・last-registered wins）

### 5.7 IL2CPP / AOT 制約

- コールバック `OnShareResult` は `static` かつ `[MonoPInvokeCallback(typeof(ShareCallback))]` を付与し、
  `#if UNITY_IOS && !UNITY_EDITOR`（実機 IL2CPP のみ）に置く。
- `s_shareDelegate` を static フィールドで永続保持（GC 防止）。同じく実機ガード内。
- `extern shareContent` と `s_shareDelegate` を実機ガードに閉じることで、Editor（Mono）で
  `__Internal` シンボル未解決にならない。
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
| 非 iOS プラットフォーム / Editor | `IosShareResult.Failure("iOS share is only available on an iOS device.")`（native 呼び出しなし、即時 Failure）。**v2 で確定**: early-return による無応答は行わない。**v3**: `#else`（`!UNITY_IOS \|\| UNITY_EDITOR`）経路として実装し EditMode で検証可能 |
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
- `IosShareManagerDispatchTests`（新規・v2 追加 / v3 で対象拡大。`InvokeInOrder` シームを直接検証。native 非依存）:
  - **dispatch 順序**: 共通イベント → 個別 callback の順で 1 回ずつ呼ばれる
    （呼び出し順を記録して検証）。
  - 個別 callback のみ / 共通のみ / 両方 null の各パターンで例外が出ない。
  - 個別 callback 内で例外が発生しても共通イベントは既に呼ばれており、例外は握りつぶされる
    （`LogAssert` で `Debug.LogError` を許容）。
  - 補足: `s_onShare` のスナップショット + クリア（last-registered wins / 連続呼び出しで
    前の callback が二重発火しない）は、`InvokeInOrder` に callback を渡す設計により
    純粋関数として検証可能。
  - **v3 で追加可能になった検証**: `IosShareManager` 型が全ビルドターゲットの Editor で存在するため、
    Manager の入力ガード（`payload==null` / `items` 空 → `"No shareable items were provided."`）と
    非 iOS Failure 経路（`#else` → `"iOS share is only available on an iOS device."`）を、
    `UnityMainThreadDispatcher` を pump できる文脈（下記 PlayMode / `UnityTest`）で Manager 経由で検証する。

### 7.2 PlayMode（`UnityTest` コルーチン）

- v3 では、`IosShareManager` が Editor でコンパイルされるため、native を呼ばない範囲で
  Manager 統合を検証できる（推奨）:
  - Editor で `Share(payload, onResult)` を呼ぶと `ShareCompleted` と `onResult` の両方に
    即時 Failure（`"iOS share is only available on an iOS device."`）が 1 回ずつ届く。
  - `payload == null` / `items` 空で `"No shareable items were provided."` の Failure が届く。
  - 連続呼び出しで `s_onShare` が last-registered wins となり、前の `onResult` が二重発火しない。
  - いずれも `UnityMainThreadDispatcher` を pump するため `UnityTest`（コルーチン）で 1 フレーム進める。

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
- v3: `IosShareManager` のクラスは `#if UNITY_IOS || UNITY_EDITOR`、native P/Invoke のみ
  `#if UNITY_IOS && !UNITY_EDITOR`。既存 `IosNotificationManager`（クラス全体 `#if UNITY_IOS`）とは
  guard 方針が異なる点に注意（本 Share は Editor 参照・EditMode 検証を優先して二分割する）。

---

## 9. レビュー指摘の反映（v1 → v2 → v3 → v4）

対象レビュー:
- `artifact/reviews/share/2026-07-05-ios-share-design-review.md`（v1 対象）→ 下表 #1〜#7
- `artifact/reviews/share/2026-07-05-ios-share-design-review-v2.md`（v3 対象）→ 下表 #8〜#12

| # | 優先度 | 指摘 | 最終確定方針 | 反映箇所 |
| - | ----- | ---- | ----------- | ------- |
| 1 | high | 非 iOS 時に callback/event 未発火で無応答になる | **即時 Failure に統一**（`"iOS share is only available on an iOS device."`）。共通 + 個別の両方が必ず発火。EditMode/PlayMode で検証 | 5.4 手順4 / 6.3 / 7.1 / 7.2 |
| 2 | high | `IosShareManager` の platform guard 境界が未確定 | **【v3 で方針変更】** クラスは `#if UNITY_IOS \|\| UNITY_EDITOR` で全ターゲットの Editor に存在。native P/Invoke（extern/`MonoPInvokeCallback`/`s_shareDelegate`/実呼び出し）のみ `#if UNITY_IOS && !UNITY_EDITOR` に閉じる。レビュー提案の代替案を採用 | 5.4 冒頭 / 4.1 / 5.7 |
| 3 | medium | `payload` が nullable でない | シグネチャを `IosShareContentPayload? payload` に変更。先頭 `Debug.Log` は null 安全に出力 | 5.4 手順1・シグネチャ |
| 4 | medium | builder の null 要素・空 value の扱いが未定義 | null 要素は serializer 側で除外（native 互換、例外にしない）。空 value は素通し。テストで固定 | 5.3 入力契約 / 7.1 |
| 5 | medium | Manager dispatch の EditMode 検証が不足 | dispatch 中核を `internal static InvokeInOrder` に切り出し `InternalsVisibleTo` で EditMode 検証。**v3**: Manager 型が Editor に存在するため入力ガード・非 iOS Failure も Manager 経由で検証可能 | 5.4 dispatch / 4.2 / 7.1 / 7.2 |
| 6 | low | public 型の XML コメント明示 | 各 public 型・helper に英語 XML コメント必須を明記 | 5.2 |
| 7 | low | xcframework の nm 確認コマンド省略 | 確認コマンドと期待出力を記録 | 4.5 |
| 8 | high | `Share` 手順で `json` が宣言前参照（コンパイル不可） | **【v4】** `json` 生成を実機ガード `#if UNITY_IOS && !UNITY_EDITOR` 内の `try` 直前に移動。Editor 経路では生成しない | 5.4 手順4 |
| 9 | medium | `InvokeInOrder`（internal）検証に必要な asmdef/friend 設定と §2.2「変更不要」が矛盾 | **【v4】** 機構は asmdef ではなく `[assembly: InternalsVisibleTo("NativeToolkit.Runtime.Tests")]`。新規 `Runtime/AssemblyInfo.cs` を変更ファイルに追加し §2.2 を訂正 | 2.2 / 4.1 |
| 10 | medium | `items` の null 要素が public API 契約か defensive かが曖昧 | **【v4】** defensive-only と確定（型は `IosShareItem[]` のまま）。通常契約は non-null、テストは `null!` で除外を検証 | 5.2 / 5.3 / 7.1 |
| 11 | low | 実装順序に v3 追加テストが未反映 | **【v4】** 5.6 に `IosShareManagerDispatchTests` / Manager 統合 `UnityTest` / `AssemblyInfo.cs` を追加 | 5.6 |
| 12 | low | nm 期待値に可変の固定アドレスが含まれる | **【v4】** 期待値を `T _shareContent` の存在確認のみに変更（アドレスは一致判定に使わない） | 4.5 |

- v2 → v3 の主変更: #2 の platform guard 戦略を「クラス全体 `#if UNITY_IOS`」から
  「クラス `UNITY_IOS || UNITY_EDITOR` + native 部のみ `UNITY_IOS && !UNITY_EDITOR`」へ変更。
- v3 → v4 の主変更: #8（json 宣言前参照バグ）と #9（InternalsVisibleTo の正確な機構）を修正。
  いずれも実装時のコンパイルエラーに直結する項目。
- 不足項目（両レビューの「不足項目」節）はすべて確定済み。
