# CLAUDE.md — unity-native-toolkit コーディングガイドライン

## Unity6 C# コーディングルール

### ログ（Debug.Log）

対象メソッドの先頭1行目に、全パラメータを含む `Debug.Log` を必ず入れる。

**フォーマット:**

```csharp
Debug.Log($"[{nameof(MethodName)}] param1: {param1}, param2: {param2}");
```

エラーは `Debug.LogError` を使ってログ出力する。

```csharp
Debug.LogError($"[{nameof(MethodName)}] param1: {param1}, param2: {param2}");
```

**対象:**

- `override` メソッド
- `public` / `internal` メソッド
- `operator` オーバーロード
- `MonoBehaviour` のイベント関数（`Awake`, `Start`, `Update`, `OnEnable`, `OnDisable` など）
- ローカル関数（通知・権限・プラットフォーム連携コード内）

**除外:**

- `class` / `struct` / `enum` / `interface` 宣言
- private な軽量ユーティリティ（単純な extension/helper）
- 純粋 UI 描画ユーティリティ（毎フレーム大量呼び出しされる箇所）
- 既に同等ログがある箇所（重複追加しない）

**Tag 定義:**

```csharp
private const string LogTag = "ClassName";
```

```csharp
Debug.Log($"[{LogTag}][{nameof(MethodName)}] param: {param}");
```

**using:**

```csharp
using UnityEngine;
```

**例:**

```csharp
public override void OnApplicationPause(bool pauseStatus)
{
    Debug.Log($"[{nameof(OnApplicationPause)}] pauseStatus: {pauseStatus}");
    // Existing logic...
}

public Result Send(NotificationCommand command)
{
    Debug.Log($"[{nameof(Send)}] command: {command}");
    return repository.Send(command);
}
```

---

### XML ドキュメントコメント（C#）

`public` なメソッド・クラス・インターフェース・プロパティには XML ドキュメントコメントを付ける。  
コードと同時に書く（後からまとめて書くと設計意図を忘れるため）。

**対象（必須）:**

- `public` メソッド
- `public class` / `interface` / `struct` / `record` / `enum`
- `public` プロパティ（非自明なもの）

**除外:**

- `private` / `internal` メソッド
- `override` メソッド（親のコメントを継承する場合）
- 自明な getter / setter

**フォーマット:**

```csharp
/// <summary>
/// Deletes the channel by id.
/// </summary>
/// <param name="channelId">Target channel id.</param>
/// <returns>Operation result containing an exception on failure.</returns>
public Result DeleteChannel(string channelId)
```

**ルール:**

- コメント本文（XML コメント、行コメント、ブロックコメント）は英語で記述する
- ユーザー向けメッセージ文言（UIテキスト、statusText、Toast、Dialog文言）は英語で記述する
- `<summary>` の1行目は簡潔な概要にする
- `<param>` は非自明なパラメータのみ記載（名前から明らかなものは省略可）
- `<returns>` は戻り値が非自明な場合のみ記載
- `<exception>` は明示的に送出する例外がある場合に記載
