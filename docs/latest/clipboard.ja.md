# クリップボード機能

言語:

- [English](clipboard.md)
- 日本語（このページ）
- [한국어](clipboard.ko.md)

← [マニュアルトップへ戻る](index.ja.md)

---

## 目次

- [Android](#android)
  - [セットアップ](#セットアップ)
  - [プレーンテキストのコピー](#プレーンテキストのコピー)
  - [プレーンテキストのコピー（空文字、許容）](#プレーンテキストのコピー空文字許容)
  - [HTMLテキストのコピー](#htmlテキストのコピー)
  - [プレーンテキストが空のHTMLコピー](#プレーンテキストが空のhtmlコピー)
  - [URIのコピー](#uriのコピー)
  - [複数テキストのコピー](#複数テキストのコピー)
  - [機微テキストのコピー](#機微テキストのコピー)
  - [ゲームでの利用例](#ゲームでの利用例)
    - [招待コードのコピー](#招待コードのコピー)
    - [クリップボードからコードを貼り付け](#クリップボードからコードを貼り付け)
    - [スクリーンショットのコピー](#スクリーンショットのコピー)
  - [クリップボードの読み取り](#クリップボードの読み取り)
  - [クリップの有無判定](#クリップの有無判定)
  - [メタデータの取得](#メタデータの取得)
  - [クリップボードのクリア](#クリップボードのクリア)
  - [監視の開始・停止](#監視の開始停止)
  - [イベントの受信](#イベントの受信)
  - [エラーハンドリング](#エラーハンドリング)

---

## Android

クリップボード機能は Android のみを対象としています。iOS・Windows・macOS向けの実装はありません。

### セットアップ

#### 名前空間のインポート

`AndroidClipboardManager` は Android ビルドターゲットを選択している場合にのみコンパイルされます。

```csharp
// ガード: Android専用。AndroidClipboardManagerは他のビルドターゲットには存在しません。
#if UNITY_ANDROID
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
#endif
```

自作スクリプト側の呼び出し箇所（例えばエディター上でも動作する MonoBehaviour）では、ネイティブブリッジが実機上にしか存在しないため、呼び出し時点でさらにエディターを除外してください。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = "Hello" });
#endif
```

#### 同期APIと非同期API

- `Read` / `HasClip` / `GetDescription` は同期APIで、結果をその場で返し、`ClipboardOperationCompleted` を発火しません。
- `CopyPlainText` / `CopyHtmlText` / `CopyUri` / `CopyMultipleText` / `Clear` / `StopObserving` は非同期APIで、結果を `ClipboardOperationCompleted` イベント経由で通知したあと、任意の呼び出し単位のコールバックを呼び出します。
- `StartObserving` は成功・失敗を問わず結果を一切通知しません。詳細は[監視の開始・停止](#監視の開始停止)を参照してください。

#### content:// URI（Copy URI・Copy Screenshotに必要）

クリップボードAPIはURI文字列を受け取りますが、それを組み立てる仕組みは持っていません。native-toolkitのAARに同梱されているFileProvider（Share機能が使用するものと同一）を利用してください。AARのマニフェストに `${applicationId}.native_toolkit.share.fileprovider` として宣言済みのため、アプリ側でのマニフェスト追記は不要です。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
private static string CreateContentUri(string path)
{
    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    using var file = new AndroidJavaObject("java.io.File", path);
    string authority = $"{Application.identifier}.native_toolkit.share.fileprovider";
    using var fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider");
    using var uri = fileProvider.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file);
    return uri.Call<string>("toString");
}
#endif
```

> **注意:** FileProviderのauthorityは `Application.identifier` から組み立てられます。Gradleテンプレートで `applicationIdSuffix` を設定している場合はマージ後のマニフェストと一致するか確認してください。一致しないと `getUriForFile` が `IllegalArgumentException` を投げます。

---

### プレーンテキストのコピー

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
{
    text = "Hello from Unity Native Toolkit",
    label = "sample"
});
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopyPlainText.png" alt="Example_AndroidClipboardManager_CopyPlainText" width="400" />
</p>

---

### プレーンテキストのコピー（空文字、許容）

`text` が空文字の場合、ネイティブ層はこれを明示的に許容し失敗しません。`CopyHtmlText` の `htmlText` とは異なる挙動です。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
{
    text = ""
});
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopyEmptyPlainText.png" alt="Example_AndroidClipboardManager_CopyEmptyPlainText" width="400" />
</p>

---

### HTMLテキストのコピー

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload
{
    plainText = "Hello",
    htmlText = "<b>Hello</b>"
});
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopyHtmlText.png" alt="Example_AndroidClipboardManager_CopyHtmlText" width="400" />
</p>

---

### プレーンテキストが空のHTMLコピー

`plainText` は空文字を許容しますが、`htmlText` が空文字の場合のみ失敗します（[エラーハンドリング](#エラーハンドリング)参照）。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload
{
    plainText = "",
    htmlText = "<b>Html only</b>"
});
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopyHtmlEmptyPlainText.png" alt="Example_AndroidClipboardManager_CopyHtmlEmptyPlainText" width="400" />
</p>

---

### URIのコピー

`content://` URI（画像やファイルへの参照など）をコピーします。URI文字列の組み立て方は前述の[content:// URI](#content-uriコピー-uricopy-screenshotに必要)を参照してください。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string path = Path.Combine(Application.persistentDataPath, "clipboard_sample.txt");
File.WriteAllText(path, "Clipboard sample file content");
string uri = CreateContentUri(path);

AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
{
    uri = uri
});
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopyUri.png" alt="Example_AndroidClipboardManager_CopyUri" width="400" />
</p>

> **注意:** 貼り付け先アプリがこの `content://` URIを読み取れるかは端末と対象アプリに依存します。プレーンテキストのみを受け付けるアプリでは解決できません。

---

### 複数テキストのコピー

複数のプレーンテキストを1つのクリップとしてコピーします。`texts` 配列内の個々の空文字は許容されますが、配列自体が空の場合のみ失敗します（[エラーハンドリング](#エラーハンドリング)参照）。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyMultipleText(new CopyMultipleTextPayload
{
    texts = new[] { "First", "", "Third" }
});
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopyMultipleText.png" alt="Example_AndroidClipboardManager_CopyMultipleText" width="400" />
</p>

---

### 機微テキストのコピー

`isSensitive` を設定すると、システムクリップボードUIでのプレビュー抑制を要求できます。このヒントはAndroid 13（API 33）以降でのみ効果があり、それより前のバージョンでは抑制なしで通常どおりコピーされます。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
{
    text = "P@ssw0rd-sample",
    isSensitive = true
});
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopySensitiveText.png" alt="Example_AndroidClipboardManager_CopySensitiveText" width="400" />
</p>

---

### ゲームでの利用例

ゲームでよくあるクリップボードの使い方として、招待コードの共有、受け取ったコードの貼り付け、スクリーンショットのコピーを紹介します。

#### 招待コードのコピー

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
{
    text = "NTK-7F3A-92QX",
    label = "invite code"
});
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopyInviteCode.png" alt="Example_AndroidClipboardManager_CopyInviteCode" width="400" />
</p>

#### クリップボードからコードを貼り付け

クリップボードを同期的に読み取り、先頭のアイテムのプレーンテキストを抽出します。ベストエフォートのフォールバック値は使いません。`content://` URIのみを保持するクリップ（[スクリーンショットのコピー](#スクリーンショットのコピー)で作成したものなど）は、URIをコードとして誤って表示するのではなく、「テキストアイテムが見つからない」という結果になります。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
var result = AndroidClipboardManager.Instance.Read();
switch (result.Status)
{
    case ClipboardReadStatus.HasContent:
        string? code = ExtractFirstText(result.Contents!);
        // クリップにプレーンテキストのアイテムが無い場合（URIのみのクリップ等）はnullになる
        break;
    case ClipboardReadStatus.Empty:
        // クリップボードが空。これは失敗ではなく正常な結果
        break;
    default:
        // result.ErrorCode / result.ErrorMessage が失敗内容を表す
        break;
}

static string? ExtractFirstText(ClipContents contents)
{
    foreach (var item in contents.Items)
    {
        if (!string.IsNullOrEmpty(item.Text)) return item.Text;
    }
    return null;
}
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_PasteCode.png" alt="Example_AndroidClipboardManager_PasteCode" width="400" />
</p>

> **セキュリティ上の注意:** 貼り付けた値は絶対にログへ出力しないでください。クーポンコードなどの機微情報である可能性があります。ログには `result.Status` と `result.ErrorCode` のみ出力してください。

#### スクリーンショットのコピー

現在のフレームをキャプチャし、`content://` URIとしてコピーします。`ScreenCapture.CaptureScreenshotAsTexture` はフレームの描画完了を必要とするため、キャプチャは `WaitForEndOfFrame` の後、コルーチン内で実行する必要があります。PNGバイト列への書き出しが終わった `Texture2D` は必ず破棄してください。破棄しないとリークします。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
private IEnumerator CaptureAndCopyScreenshot()
{
    yield return new WaitForEndOfFrame();

    string uri;
    Texture2D? screenshot = null;
    try
    {
        screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] png = screenshot.EncodeToPNG();
        string path = Path.Combine(Application.persistentDataPath, "clipboard_screenshot.png");
        File.WriteAllBytes(path, png);
        uri = CreateContentUri(path);
    }
    finally
    {
        if (screenshot != null) Destroy(screenshot);
    }

    AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
    {
        uri = uri,
        label = "screenshot"
    });
}
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_CopyScreenshot.png" alt="Example_AndroidClipboardManager_CopyScreenshot" width="400" />
</p>

> **注意:** 貼り付けたスクリーンショットを受け取り側アプリが読み取れるかは、他の `content://` クリップと同様に端末と対象アプリに依存します。

---

### クリップボードの読み取り

同期API。クリップの内容、空の結果、または失敗のいずれかを返します。空のクリップボードは失敗ではなく正常な結果として扱われます。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
var result = AndroidClipboardManager.Instance.Read();
switch (result.Status)
{
    case ClipboardReadStatus.HasContent:
        ClipContents contents = result.Contents!;
        // contents.Label, contents.MimeTypes, contents.Items（各アイテムのText / HtmlText / Uri / CoercedText）
        break;
    case ClipboardReadStatus.Empty:
        // 正常な結果であり失敗ではない
        break;
    default:
        // result.ErrorCode / result.ErrorMessage が失敗内容を表す
        break;
}
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_ReadClipboard.png" alt="Example_AndroidClipboardManager_ReadClipboard" width="400" />
</p>

> **セキュリティ上の注意:** クリップボードの内容にはパスワードやトークンが含まれる可能性があります。ログには `result.Status` と `result.ErrorCode` のみを出力し、クリップ本文自体は絶対にログへ出力しないでください。

---

### クリップの有無判定

同期API。クリップボードが本当に空の場合と、判定自体が実行できなかった場合の両方で `false` を返します。C#側からはこの2つを区別できません。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
bool hasClip = AndroidClipboardManager.Instance.HasClip();
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_HasClip.png" alt="Example_AndroidClipboardManager_HasClip" width="400" />
</p>

---

### メタデータの取得

同期API。クリップ本文には触れずに、ラベル・MIMEタイプ・スタイル付きテキストかどうか・分類ステータスといったメタデータのみを読み取ります。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
var result = AndroidClipboardManager.Instance.GetDescription();
switch (result.Status)
{
    case ClipboardReadStatus.HasContent:
        ClipDescriptionInfo info = result.Description!;
        // info.Label, info.MimeTypes, info.IsStyledText, info.ClassificationStatus（API 31未満ではnull）
        break;
    case ClipboardReadStatus.Empty:
        break;
    default:
        break;
}
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_GetDescription.png" alt="Example_AndroidClipboardManager_GetDescription" width="400" />
</p>

---

### クリップボードのクリア

非同期API。`ClipboardOperationCompleted` 経由で結果が通知されます。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.Clear();
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_ClearClipboard.png" alt="Example_AndroidClipboardManager_ClearClipboard" width="400" />
</p>

---

### 監視の開始・停止

`StartObserving` は成功・失敗を問わず結果を一切通知しません。成功として表示しないでください。監視中のクリップボード変化は `ClipboardChanged` イベントで通知されます（[イベントの受信](#イベントの受信)参照）。すでに監視中の状態で再度呼び出した場合、ネイティブ側では何も起きません。監視はアプリがフォアグラウンドにある間のみ有効です（Android 10以降のプラットフォーム制約）。

`StopObserving` は他の操作と同様に非同期APIで、`ClipboardOperationCompleted` 経由で結果が通知されます。画面が非表示になった後も監視が続かないよう、`OnDisable` で呼び出してください。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// 開始: 結果は一切通知されない。クリップボードを変更してClipboardChangedで動作を確認する
AndroidClipboardManager.Instance.StartObserving();

// 停止: Clearと同様にClipboardOperationCompleted経由で通知される
AndroidClipboardManager.Instance.StopObserving();
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_StartObserving.png" alt="Example_AndroidClipboardManager_StartObserving" width="400" />
</p>

---

### イベントの受信

`OnEnable` でイベントを購読し、`OnDisable` で解除してください。`OnDisable` では `StopObserving` も呼び出し、非表示になった画面が監視を続けないようにします。

```csharp
private void OnEnable()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidClipboardManager.Instance.ClipboardOperationCompleted += OnClipboardOperationCompleted;
    AndroidClipboardManager.Instance.ClipboardChanged += OnClipboardChanged;
#endif
}

private void OnDisable()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidClipboardManager.Instance.ClipboardOperationCompleted -= OnClipboardOperationCompleted;
    AndroidClipboardManager.Instance.ClipboardChanged -= OnClipboardChanged;
    AndroidClipboardManager.Instance.StopObserving();
#endif
}

#if UNITY_ANDROID && !UNITY_EDITOR
private void OnClipboardOperationCompleted(ClipboardOperationResult result)
{
    string status = result.IsSuccess ? "Success" : "Failed";
    Debug.Log($"[event] {result.Operation}: {status}");
    if (!string.IsNullOrEmpty(result.ErrorMessage))
        Debug.LogError(result.ErrorMessage);
}

private void OnClipboardChanged()
{
    Debug.Log("[event] Clipboard changed");
}
#endif
```

`ClipboardOperationCompleted` は、`CopyPlainText` / `CopyHtmlText` / `CopyUri` / `CopyMultipleText` / `Clear` / `StopObserving` に渡した呼び出し単位のコールバックより常に先に発火します。イベント側のコールバックと呼び出し単位のコールバックの両方が例外を投げた場合でも、それぞれ独立して捕捉されるため、片方の例外がもう片方の呼び出しを妨げることはありません。

---

### エラーハンドリング

すべての非同期操作は `ClipboardOperationCompleted` を通じて成功・失敗を通知します。`ErrorMessage` は `IsSuccess` が `false` の場合のみ非nullになります。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// HTMLテキストが空の場合は失敗する: CLIPBOARD_EMPTY_CONTENT
// ErrorMessage: "Clipboard content is empty. Please provide text or HTML."
AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload
{
    plainText = "Hello",
    htmlText = ""
});

// アイテム配列が空の場合は失敗する: CLIPBOARD_EMPTY_ITEMS
// ErrorMessage: "No items provided for clipboard copy."
AndroidClipboardManager.Instance.CopyMultipleText(new CopyMultipleTextPayload
{
    texts = Array.Empty<string>()
});

// URIが空文字の場合は失敗する: CLIPBOARD_INVALID_URI
AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
{
    uri = ""
});

// http:// スキームは拒否される。content:// URIのみサポート: CLIPBOARD_INVALID_URI
// ErrorMessageは "Invalid URI:" で始まる
AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
{
    uri = "http://example.com/x"
});
#endif
```

`ErrorCode` として通知される安定したエラーコード一覧:

| エラーコード | 意味 |
| --- | --- |
| `CLIPBOARD_EMPTY_CONTENT` | `CopyHtmlText` が空文字の `htmlText` で呼び出された |
| `CLIPBOARD_EMPTY_ITEMS` | `CopyMultipleText` が空の `texts` 配列で呼び出された |
| `CLIPBOARD_INVALID_URI` | `CopyUri` が空文字・不正な形式・`content://` 以外のURIで呼び出された |
| `CLIPBOARD_READ_NOT_ALLOWED` | ネイティブ層が読み取りを拒否した（フォーカス・権限制約など） |
| `CLIPBOARD_SECURITY` | ネイティブ層がセキュリティ上の理由で操作を拒否した |
| `CLIPBOARD_UNAVAILABLE` | システムの `ClipboardManager` を取得できなかった |
| `CLIPBOARD_UNKNOWN` | 分類できない失敗。Unity側でのパース失敗にも使われる |

`Read` と `GetDescription` は、ネイティブブリッジ自体に到達できなかった場合（Android上で実行されていない、プラグイン未初期化、currentActivityが取得できない等）、追加で `CLIPBOARD_BRIDGE_UNAVAILABLE` を返すことがあります。これはネイティブ層が返すものではなく、Unity側のエラーコードです。
