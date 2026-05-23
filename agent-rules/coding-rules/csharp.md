# Unity6 C# コーディングルール

> **同期ルール:** このファイルが唯一の正本（Single Source of Truth）。
> このファイルを更新したら、**まったく同じ内容**を
> `.github/instructions/csharp-coding.instructions.md` にも反映すること（front matter `---` 以下を同一に保つ）。

## ログ（Debug.Log）

全メソッドの先頭1行目に、全パラメータを含む `Debug.Log` を必ず入れる。

**フォーマット:**

```csharp
Debug.Log($"[{LogTag}][{nameof(MethodName)}] param1: {param1}, param2: {param2}");
```

エラーは `Debug.LogError` を使ってログ出力する。

```csharp
Debug.LogError($"[{LogTag}][{nameof(MethodName)}] param1: {param1}, param2: {param2}");
```

**対象:**

- `override` メソッド
- `public` / `internal` メソッド
- `operator` オーバーロード
- `MonoBehaviour` イベント関数（`Awake`, `Start`, `Update`, `OnEnable`, `OnDisable` など）
- ローカル関数（通知機能 / 権限 / プラットフォーム Bridge コード内）

**除外:**

- `class` / `struct` / `enum` / `interface` 宣言
- private の軽量ユーティリティ（シンプルな extension / helper メソッド）
- 純粋 UI レンダリングユーティリティ（毎フレーム大量に呼ばれるもの）
- 既にログがある箇所（重複追加しない）

**TAG の定義:**

```csharp
private const string LogTag = "ClassName";
```

**using:**

```csharp
using UnityEngine;
```

**例:**

```csharp
private const string LogTag = "SampleClass";

public override void OnApplicationPause(bool pauseStatus)
{
    Debug.Log($"[{LogTag}][{nameof(OnApplicationPause)}] pauseStatus: {pauseStatus}");
    // 既存処理...
}

public Result Send(NotificationCommand command)
{
    Debug.Log($"[{LogTag}][{nameof(Send)}] command: {command}");
    return repository.Send(command);
}
```

---

## XML ドキュメントコメント（C#）

`public` なメソッド・クラス・インターフェース・プロパティには XML ドキュメントコメントを付ける。
コードと同時に書く（後からまとめて書かない）。

**対象（必須）:**

- `public` メソッド
- `public class` / `interface` / `struct` / `record` / `enum`
- `public` プロパティ（非自明なもの）

**除外:**

- `private` / `internal` メソッド
- `override` メソッド（親のコメントを継承するため不要）
- 自明な getter / setter

**フォーマット:**

```csharp
/// <summary>
/// チャンネルを ID で削除する。
/// </summary>
/// <param name="channelId">削除対象のチャンネル ID。</param>
/// <returns>失敗時に例外を含む操作結果。</returns>
public Result DeleteChannel(string channelId)
```

**ルール:**

- コメント本文（XML コメント、行コメント、ブロックコメント）は英語で記述する
- ユーザー向けメッセージ文言（UI テキスト、statusText、Toast、Dialog 文言）は英語で記述する
- `<summary>` の1行目は簡潔な概要にする
- `<param>` は全パラメータを省略せず記載する
- `<returns>` は戻り値が非自明な場合のみ記載する
- `<exception>` は例外を明示的にスローする場合に記載する
