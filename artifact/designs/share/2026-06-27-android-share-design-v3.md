# Android Share 実装計画 v3 — Custom Chooser Action コールバック

## 基本情報

- 日付: 2026-06-27
- 機能名: share
- 対象プラットフォーム: Android
- 出力言語: 日本語
- 位置づけ: 既存 Android Share Bridge（design v2 / 実装済み）への**差分追加**。Custom Chooser Action（API 34+）タップを Unity へ返す経路を C# Bridge / Manager に配線する
- 前提: native-toolkit 側で chooser action コールバックと動的 receiver 登録が実装済み（本計画は Unity 側の追従）
- 後続工程: review-document → implement-feature → design-sample-scene（サンプルは別スキル）

---

## 1. native-toolkit 確認結果（unity_android_plugin / android_library）

### 1.1 追加された公開 API（`android.unity.share.UnityAndroidShareManager`）

| メンバ | シグネチャ | 備考 |
|--------|-----------|------|
| `setShareChooserActionListener` | `fun setShareChooserActionListener(listener: ShareChooserActionListener)` | メインスレッドでリスナー設定 |
| `clearShareChooserActionListener` | `fun clearShareChooserActionListener()` | リスナー解除 + 現在の動的 receiver を unregister |
| `ShareChooserActionListener`（interface） | `fun onChooserAction(actionId: String)` | タップされたアクションの `intentAction` 文字列。**メインスレッド**で配信。実装例外は native 側で catch されログのみ |

参照: [UnityAndroidShareManager.kt#L57-L134](/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityAndroidShareManager.kt#L57-L134)

### 1.2 chooser action 登録の挙動

- `shareText(context, shareJson)` 実行時に `chooserActions` から `actionId` を正規化し、動的 BroadcastReceiver を登録する（[UnityAndroidShareManager.kt#L143-L169](/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityAndroidShareManager.kt#L143-L169)）
- **`shareWithCallback` は chooserActions を登録しない**（[UnityAndroidShareManager.kt#L271-L302](/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/UnityAndroidShareManager.kt#L271-L302)）。chooser action コールバックは **`shareText` 経由でのみ発火**する
- `normalizeActionIds`: 空文字 / 既定 `android.intent.action.SEND` を除外し distinct 化（[ShareChooserActionInputs.kt#L15-L25](/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/ShareChooserActionInputs.kt#L15-L25)）。**コールバックを受けるには各アクションに独自の非 SEND・非空 `intentAction` が必要**
- 動的 receiver（[ShareChooserActionReceiverRegistry.kt#L43-L65](/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin/src/main/java/android/unity/share/ShareChooserActionReceiverRegistry.kt#L43-L65)）:
  - **API 34（UPSIDE_DOWN_CAKE）未満は no-op**（token 0 を返す）
  - `ContextCompat.RECEIVER_NOT_EXPORTED` で登録 → **ホストアプリの manifest 宣言は不要**
  - 世代トークンで前回登録を置き換え。share 起動失敗時はその登録のみ解除
- `actionId` は `onChooserAction` にそのまま渡される（タップ識別は `intentAction` 単位）

### 1.3 既存（v2 で配線済み）の listener

- `ShareOperationListener.onShareOperation(operation, isSuccessful, errorMessage)`
- `ShareOperationListener.onShareResult(operation, selectedPackageName)`
- 本計画はこれらに `ShareChooserActionListener` を**追加**する（既存 listener はそのまま）

### 1.4 AAR 同梱状況（要対応・依存）

- `Packages/com.jonghyunkim.nativetoolkit/Plugins/Android/` の現行 AAR は chooser action コールバック実装を含まない旧版
- 本機能の実機動作には **chooser action 対応版 AAR（`unity_android_plugin` + `android_library`）への差し替えが前提**（別タスク・依存指摘スコープ）
- manifest / FileProvider 追加は不要（receiver は動的登録のため）

---

## 2. 既存 C# 実装確認結果（`Packages/com.jonghyunkim.nativetoolkit/Runtime/`）

### 2.1 既存 Share 実装（v2）

- `Share/AndroidShareManager.cs`: `#if UNITY_ANDROID` Singleton。`ShareOperationListenerProxy : AndroidJavaProxy` で `onShareOperation` / `onShareResult` を受信。`Initialize` で `setShareOperationListener`、`OnDestroy` で `clearShareOperationListener`
- 共通イベント `ShareOperationCompleted` / `ShareCallbackReceived` + per-call callback パターン（`_pendingOperationCallbacks` / `_pendingShareSelectedCallback`、last-registered wins）
- `FireOperationResult` / `FireCallbackResult` で `UnityMainThreadDispatcher.Enqueue` 経由・共通イベント→個別 callback の順で dispatch
- `Share/ShareOperationResult.cs` / `Share/ShareCallbackResult.cs`: `readonly struct`、プラットフォームガードなし
- `Share/AndroidSharePayloads.cs`: `ChooserActionPayload`（`label` / `iconBase64` / `intentAction?`）を含む。**変更不要**
- `Share/AndroidShareJsonBuilder.cs`: `chooserActions` を既にシリアライズ。**変更不要**

### 2.2 流用する共通パターン

- `Common/UnityMainThreadDispatcher`: コールバックのメインスレッド転送
- `AndroidJavaProxy` 派生 inner class による listener 受信（既存 `ShareOperationListenerProxy` と同方式）
- 結果型は `readonly struct` で公開（`ShareOperationResult` / `ShareCallbackResult` に倣う）

---

## 3. 実装対象 API 一覧

### 3.1 C# → native 呼び出し（追加）

| 呼び出し | 対象 native メソッド | タイミング |
|---------|--------------------|-----------|
| `pluginInstance.Call("setShareChooserActionListener", chooserActionListener)` | `setShareChooserActionListener` | `Initialize`（`setShareOperationListener` の直後） |
| `pluginInstance.Call("clearShareChooserActionListener")` | `clearShareChooserActionListener` | `OnDestroy`（`clearShareOperationListener` と併せて） |

### 3.2 native → C# コールバック（追加）

| native interface | メソッド | C# 受信 |
|------------------|---------|---------|
| `UnityAndroidShareManager$ShareChooserActionListener` | `onChooserAction(String actionId)` | `ShareChooserActionListenerProxy.onChooserAction(string actionId)` |

---

## 4. 変更ファイル一覧

### 4.1 新規作成

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionResult.cs`
  - `readonly struct`、プラットフォームガードなし、`ActionId`（タップされた `intentAction`、null は `string.Empty` に正規化）を保持
- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/ShareChooserActionCallbackCoordinator.cs`
  - プラットフォームガードなし。共通イベント + per-call callback の調整（順序・last-registered wins・null クリア・例外安全）を担い EditMode テスト可能にする
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/ShareChooserActionCallbackCoordinatorTests.cs`
  - 同期 dispatch を注入し、発火順序・last-registered wins・null クリア・例外時継続を検証

### 4.2 既存変更

- `Packages/com.jonghyunkim.nativetoolkit/Runtime/Share/AndroidShareManager.cs`
  - `ShareChooserActionListenerProxy` inner class 追加
  - `ShareChooserActionCallbackCoordinator` を保持し、共通イベント `ShareChooserActionTapped` を中継
  - `ShareText` に per-call `onChooserAction` 引数追加（coordinator へ register、null 含む）
  - `Initialize`（try/catch degrade）/ `OnDestroy`（listener 解除 → Dispose → coordinator Clear の順）の配線追加
  - `ShareWithCallback`: chooserActions 非空時に警告ログ
  - `ShareText`: chooserActions が 5 件超のとき警告ログ（切り詰めはしない）
  - `FireChooserAction` は coordinator へ委譲
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/ShareResultTests.cs`
  - `ShareChooserActionResult` の生成・値保持・null 正規化テストを追加
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/AndroidShareJsonBuilderTests.cs`
  - chooserActions の callback 条件テスト（`intentAction` 含む/省略、複数 action の順序維持）を補完（不足分のみ追加）
- `Packages/com.jonghyunkim.nativetoolkit/Tests/Runtime/NativeToolkit.Runtime.Tests.asmdef`
  - 既存参照に `NativeToolkit.Runtime` を含むため追加不要（`ShareChooserActionCallbackCoordinator` は public のため `InternalsVisibleTo` 不要）

### 4.3 非変更（対象だが変更しない）

- `Runtime/Share/AndroidSharePayloads.cs`（`ChooserActionPayload.intentAction` で対応済み）
- `Runtime/Share/AndroidShareJsonBuilder.cs`（chooserActions シリアライズ済み）
- `Runtime/Share/ShareOperationResult.cs` / `ShareCallbackResult.cs`
- `Runtime/Common/UnityMainThreadDispatcher.cs`（流用のみ）
- AAR（`Plugins/Android/*.aar`）: chooser action 対応版差し替えは別タスク（依存）

---

## 5. 実装詳細（implement-feature ステップ3で行う内容）

### 5.1 `ShareChooserActionResult`（新規 struct）

```csharp
#nullable enable
namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    public readonly struct ShareChooserActionResult
    {
        public string ActionId { get; }            // tapped intentAction string (never null)
        public ShareChooserActionResult(string actionId)
            => ActionId = actionId ?? string.Empty; // normalize null to empty
    }
}
```

- プラットフォームガードなし（EditMode テスト可能）
- 既存 `ShareCallbackResult` と同方針
- **不変条件**: `ActionId` は null を取らない。コンストラクタで null を `string.Empty` に正規化する（native は非空 intentAction のみ登録するが、public struct として任意入力を安全に扱う）。`ShareResultTests` に null/empty 正規化テストを追加する

### 5.2 `AndroidShareManager` 差分

#### イベント / フィールド

```csharp
/// <summary>Occurs when the user taps a custom chooser action in the Sharesheet (API 34+).
/// Always fired before the per-call onChooserAction callback.</summary>
public event Action<ShareChooserActionResult>? ShareChooserActionTapped;

private ShareChooserActionListenerProxy? chooserActionListener;
private Action<ShareChooserActionResult>? _pendingChooserActionCallback;
```

#### callback 提供方針（既存パターン準拠）

- **共通イベント** `ShareChooserActionTapped`（`event Action<ShareChooserActionResult>`）: chooser action タップ時に常に発火
- **個別 callback**: `ShareText` に `Action<ShareChooserActionResult>? onChooserAction = null` を追加（chooser action は native 上 `shareText` でのみ登録されるため、per-call hook は `ShareText` に限定）
  - `_pendingChooserActionCallback` に保持し last-registered wins（次の `ShareText` 呼び出しで置き換え）
  - **発火後にクリアしない**: 同一登録から複数回タップされ得るため、次の `ShareText` 置き換え or `OnDestroy` までコールバックを保持する（`ShareWithCallback` の `onSelected`＝一回性とは挙動が異なる点を明記）
  - **null も登録値として扱う（クリア方針を明確化）**: `ShareText` が `onChooserAction == null`（第3引数省略含む）で呼ばれた場合は `_pendingChooserActionCallback = null` に更新し、前回の callback を破棄する。これにより「直近の `ShareText` の per-call callback のみが有効」という last-registered wins を一貫させ、シート未タップ/キャンセル後に古い callback が残らないようにする。共通イベント `ShareChooserActionTapped` は per-call の有無に関係なく常に発火する
- **dispatch 順序**: 共通イベント → 個別 callback（`FireOperationResult` と同順）

更新後の `ShareText` シグネチャ:

```csharp
/// <summary>
/// Shares plain text via the Android Sharesheet.
/// Custom chooser actions (API 34+) are registered only for this ShareText call; a tap is
/// reported via <see cref="ShareChooserActionTapped"/> and <paramref name="onChooserAction"/>.
/// Chooser actions passed to <see cref="ShareWithCallback"/> are NOT registered for callbacks.
/// To receive a callback, each chooser action must use a unique, non-blank intentAction
/// other than android.intent.action.SEND. No callback fires if the action is never tapped,
/// or on API levels below 34.
/// </summary>
public void ShareText(
    ShareTextPayload payload,
    Action<ShareOperationResult>? onResult = null,
    Action<ShareChooserActionResult>? onChooserAction = null)
```

- 常に `_pendingChooserActionCallback = onChooserAction`（null 含む）を設定してから `CallOperation(OperationShareText, json, onResult)` を呼ぶ
- 既存呼び出し（第3引数省略）との後方互換を維持（省略時は per-call callback なし＝null 登録）

`ShareWithCallback` の取り扱い（中1 反映）:

- native 仕様上 `ShareWithCallback` の `chooserActions` は callback 登録されない。XML コメントにその旨を明記する
- `ShareWithCallback` 呼び出し時に `payload.chooserActions` が非空の場合、`Debug.LogWarning` で「chooser actions are ignored by ShareWithCallback; use ShareText to receive chooser action callbacks」を出力し、誤用を早期検知する

#### Initialize / OnDestroy（degrade 安全策含む）

- `Initialize`: `setShareOperationListener` の直後に、**未対応（旧版）AAR でも既存 share 機能を壊さないよう try/catch でガード**する（高2 の非依存部分の反映）。`setShareChooserActionListener` が native method 不存在で失敗しても、警告ログのみ出して share 自体は継続する。
  ```csharp
  chooserActionListener ??= new ShareChooserActionListenerProxy(this);
  try
  {
      pluginInstance.Call("setShareChooserActionListener", chooserActionListener);
  }
  catch (Exception ex)
  {
      // Older AAR without chooser-action support: degrade gracefully, keep share working.
      Debug.LogWarning($"[{LogTag}] setShareChooserActionListener unavailable (AAR may be outdated): {ex.Message}");
  }
  ```
  - AAR 差し替え自体は依存タスク（本計画スコープ外）。ここでは「未対応 AAR を検出しても既存機能を degrade させない」ことだけを設計責務とする
- `OnDestroy`: 順序を明確化する（不足項目反映）
  1. `ClearShareOperationListener()`
  2. `ClearShareChooserActionListener()`（`pluginInstance.Call("clearShareChooserActionListener")` を try/catch でログ）
  3. `pluginInstance?.Dispose()` → `pluginInstance = null`
  4. `_pendingChooserActionCallback = null`（既存の `_pendingOperationCallbacks.Clear()` / `_pendingShareSelectedCallback = null` と併せて）
  - listener 解除（1〜2）は必ず `Dispose()`（3）より前に行う

#### テスト可能境界: callback 調整を非ガードのクラスへ切り出す（高1 反映）

- 課題: `AndroidShareManager` は `#if UNITY_ANDROID` ガードのみで Editor 非コンパイルのため、Manager 本体を直接 EditMode テストできない（`InternalsVisibleTo` だけでは不可）。
- 方針: callback 転送の中核ロジック（共通イベント発火 → per-call 発火の順序、last-registered wins、null クリア、例外安全）を **プラットフォームガードなしの小クラス `ShareChooserActionCallbackCoordinator` へ切り出す**。Manager は薄い委譲のみ行う。これにより核ロジックを EditMode で検証できる。

新規クラス（ガードなし、`Runtime/Share/`）:

```csharp
#nullable enable
namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    using System;

    /// <summary>
    /// Coordinates chooser-action callback delivery: global event then per-call callback,
    /// last-registered wins, exception-safe. Platform-agnostic for EditMode testing.
    /// </summary>
    public sealed class ShareChooserActionCallbackCoordinator
    {
        private readonly Action<Action> _dispatch;   // injectable; real = UnityMainThreadDispatcher
        private Action<ShareChooserActionResult>? _pending;

        public event Action<ShareChooserActionResult>? ChooserActionTapped;

        public ShareChooserActionCallbackCoordinator(Action<Action> dispatch) => _dispatch = dispatch;

        /// <summary>Registers (replaces) the per-call callback. null clears it (last-registered wins).</summary>
        public void Register(Action<ShareChooserActionResult>? onChooserAction) => _pending = onChooserAction;

        /// <summary>Clears the per-call callback (e.g., on teardown).</summary>
        public void Clear() => _pending = null;

        /// <summary>Fires global event then per-call callback through the dispatcher. Never throws.</summary>
        public void Fire(ShareChooserActionResult result)
        {
            var cb = _pending; // not cleared: same registration may receive multiple taps
            _dispatch(() =>
            {
                try { ChooserActionTapped?.Invoke(result); cb?.Invoke(result); }
                catch (Exception) { /* logged by caller-provided dispatch wrapper or here */ }
            });
        }
    }
}
```

- Manager 側:
  - `_chooserCoordinator = new ShareChooserActionCallbackCoordinator(a => UnityMainThreadDispatcher.Instance.Enqueue(a));`
  - 公開イベント `ShareChooserActionTapped` は coordinator の event を中継（`add`/`remove` で委譲）または coordinator の event を直接公開
  - `ShareText` の per-call: `_chooserCoordinator.Register(onChooserAction)`（null 含む）
  - proxy 受信: `FireChooserAction` は `_chooserCoordinator.Fire(result)` を呼ぶだけ
  - `OnDestroy`: `_chooserCoordinator.Clear()`
- EditMode テストは同期 dispatch（`a => a()`）を注入し、発火順序・last-registered wins・null クリア・例外時の継続を検証する（テスト方針 7 参照）
- native は既にメインスレッドで `onChooserAction` を配信するが、Manager 内の他経路と一貫させるため dispatch を経由する

#### proxy inner class（IL2CPP 制約）

```csharp
private sealed class ShareChooserActionListenerProxy : AndroidJavaProxy
{
    private readonly AndroidShareManager owner;
    public ShareChooserActionListenerProxy(AndroidShareManager owner)
        : base("android.unity.share.UnityAndroidShareManager$ShareChooserActionListener")
    { this.owner = owner; }

    // Must be public so IL2CPP can resolve the call from Java.
    // Method name and signature must exactly match the native interface.
    public void onChooserAction(string actionId)
        => owner.FireChooserAction(new ShareChooserActionResult(actionId));
}
```

- proxy メソッドは `public` 非 static、メソッド名 `onChooserAction` / 引数 `string` を native interface に完全一致
- `[MonoPInvokeCallback]` は不要（`AndroidJavaProxy` 方式）

### 5.3 スレッド / メモリ / エラー契約

- **スレッド**: コールバックは `UnityMainThreadDispatcher.Instance.Enqueue` 経由でメインスレッドに転送
- **メモリ**: `_pendingChooserActionCallback` は `OnDestroy` で null 化。動的 receiver の解除は native（`clearShareChooserActionListener` / 次 share の世代置換）が担当
- **エラー契約**: chooser action コールバック自体は成否を持たない通知（`actionId` のみ）。share 起動の成否は従来どおり `ShareOperationCompleted`（`ShareText` の `OperationShareText`）で受ける

### 5.4 実装順序

1. `ShareChooserActionResult`（struct、null 正規化）
2. `ShareChooserActionCallbackCoordinator`（ガードなし、注入可能 dispatch）
3. `AndroidShareManager`: proxy → coordinator 保持/イベント中継 → `ShareText` 引数（coordinator register）→ `ShareWithCallback` 警告ログ → Initialize（try/catch degrade）/ OnDestroy（listener 解除 → Dispose → coordinator Clear）配線 → `FireChooserAction` 委譲
4. テスト: `ShareChooserActionCallbackCoordinatorTests`（順序・last-registered wins・null クリア・例外継続）、`ShareResultTests`（struct null 正規化）、`AndroidShareJsonBuilderTests`（chooserActions の callback 条件、5.5 / 7 参照）

---

## 6. エラーケース一覧と返却仕様（層別）

| 層 | 条件 | 挙動 |
|----|------|------|
| parser | `chooserActions` の `label` / `iconBase64` が空 | 当該アクションを native 側で skip（コールバック対象外） |
| normalize | `intentAction` が空 / `android.intent.action.SEND` | コールバック識別子から除外（タップしても `onChooserAction` は発火しない）。設計上は独自 `intentAction` を要求 |
| registry | API 34 未満 | receiver 登録 no-op（chooser action 自体が表示されない）。`onChooserAction` 不発 |
| registry | 登録失敗（例外） | native でログのみ。share 自体は継続。`onChooserAction` 不発 |
| C# Bridge | 非 Android / `pluginInstance == null` / activity 取得失敗 | `ShareText` 自体が `Failure(OperationShareText, ...)`（既存）。chooser action は登録されない |
| C# Bridge | proxy 実装内例外 | native 側で catch されログのみ（receiver スレッドへ伝播しない） |

chooser action 件数の方針（低2 反映）:

- Android の `EXTRA_CHOOSER_CUSTOM_ACTIONS` は最大 5 件想定。Unity 側では **切り詰め（truncate）は行わず**、5 件超過時に `Debug.LogWarning` で「Android shows at most 5 custom chooser actions; extra actions may be ignored」を出すに留める（入力はそのまま native へ渡す）。JsonBuilder は件数制御しない（責務を持たせない）
- 表示数の最終挙動は OS / OEM 依存のため要検証項目に残す

要検証事項:

- chooser action が **`shareText` のみ**で有効である native 挙動の確認（`shareWithCallback` の chooserActions は無視される）
- API 34+ 実機での表示数上限（システム Sharesheet が複数アクションのうち先頭数件のみ表示する場合がある）
- 同一登録からの複数回タップ時に `onChooserAction` が複数回届くか（per-call callback を非クリアにする設計の妥当性）

---

## 7. テスト方針

### 7.1 EditMode（Unity Test Runner、ネイティブ非依存）

| 対象 | 具体ケース |
|------|-----------|
| `ShareChooserActionCallbackCoordinator` | (1) `Fire` で共通イベント → per-call の順に発火する / (2) per-call 未登録（null）でも共通イベントは発火する / (3) `Register` の last-registered wins（後の登録のみ発火）/ (4) `Register(null)` で前回 callback がクリアされる / (5) 同期 dispatch 注入で複数回 `Fire` → per-call が複数回発火（非クリア）/ (6) 共通イベント購読側が例外を投げても dispatch が継続し per-call も呼ばれる |
| `ShareChooserActionResult` | (7) `ActionId` を保持する / (8) null 入力が `string.Empty` に正規化される（`ShareResultTests` に追加） |
| `AndroidShareJsonBuilder` | (9) `intentAction` 指定時に JSON へ含む / (10) `intentAction` 未指定時は省略する / (11) 複数 chooserActions の配列順序を維持する（中5 反映。既存テストがあれば本契約の根拠としてテスト名を列挙、不足分を追加） |

- coordinator はガードなしクラスのため、注入 dispatch（`a => a()`）で上記をメインスレッド非依存に検証できる

### 7.2 手動確認（実機）

| 条件 | 内容 |
|------|------|
| API 34+ / 対応 AAR | `ShareText` に独自 `intentAction` 付き chooserActions を渡し、Sharesheet にアクション表示 → タップで `ShareChooserActionTapped` / per-call `onChooserAction` が発火 |
| API 34+ | `ShareWithCallback(payloadWithChooserActions)` では chooser action が callback 登録されず、警告ログが出ること |
| リスナー解除 | 画面破棄（`OnDestroy` → `clearShareChooserActionListener`）後にタップしてもコールバックが届かないこと |
| 旧版 AAR（未対応） | `Initialize` の try/catch で degrade し、既存 share 機能（text/image/file/callback）が壊れないこと |
| API 33 以下 | chooser action が表示されず、share 自体は正常に動作すること |

- `AndroidShareManager` 本体（`#if UNITY_ANDROID` + `AndroidJavaObject` 依存）の listener 登録・proxy 受信は EditMode 不可。核となる callback 転送ロジックは 7.1 の coordinator テストで担保し、Manager は委譲のみとする

---

## 8. Definition of Done

- `ShareChooserActionResult`（null 正規化）と `ShareChooserActionCallbackCoordinator` を実装
- `AndroidShareManager` に `ShareChooserActionListenerProxy` / 共通イベント `ShareChooserActionTapped` / `ShareText` の per-call `onChooserAction` を配線
- listener 登録（`Initialize`、try/catch degrade）/ 解除（`OnDestroy`、listener 解除 → Dispose → coordinator Clear の順）が実装されている
- per-call callback の置換・null クリア（last-registered wins）が実装されている
- callback 転送順序（共通イベント → per-call）と例外安全が実装されている
- `ShareWithCallback` の chooserActions 非対応が XML コメント + 警告ログで明示されている
- public メンバの XML コメントに契約（API 34+ 限定 / `ShareText` 限定 / `ShareWithCallback` 不可 / 未タップ時は不発）を記載
- EditMode テスト（7.1）が全ケース通過する
- 未対応 AAR でも既存 share 機能が degrade しないことを設計・実装で担保（実機確認は手動）
- （前提・依存スコープ外）chooser action 対応版 AAR への差し替え後に 7.2 の実機確認を行う

## 9. 補足

- native-toolkit 確認結果（セクション1）と既存 C# 実装確認結果（セクション2）を分離して記載した
- 本計画はサンプルアプリ（ExampleController・UXML/USS・シーン）を含まない。Custom Action のサンプル提示は `design-sample-scene` で別途設計する（Bridge 拡張が前提）
- AAR 差し替え（chooser action 対応版）は依存指摘スコープ。実機確認の前提条件として扱う
- 後方互換: `ShareText` の第3引数追加はオプション引数のため既存呼び出しに影響しない
