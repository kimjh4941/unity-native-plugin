# Clipboard Feature

Language:

- English (this page)
- 日本語: [clipboard.ja.md](clipboard.ja.md)
- 한국어: [clipboard.ko.md](clipboard.ko.md)

← [Back to manual top](index.md)

---

## Table of Contents

- [Android](#android)
  - [Setup](#setup)
  - [Copy Plain Text](#copy-plain-text)
  - [Copy Plain Text (Empty, Allowed)](#copy-plain-text-empty-allowed)
  - [Copy HTML Text](#copy-html-text)
  - [Copy HTML Text with Empty Plain Text Fallback](#copy-html-text-with-empty-plain-text-fallback)
  - [Copy URI](#copy-uri)
  - [Copy Multiple Text](#copy-multiple-text)
  - [Copy Sensitive Text](#copy-sensitive-text)
  - [Game Use Cases](#game-use-cases)
    - [Copy Invite Code](#copy-invite-code)
    - [Paste Code from Clipboard](#paste-code-from-clipboard)
    - [Copy Screenshot](#copy-screenshot)
  - [Read Clipboard](#read-clipboard)
  - [Has Clip](#has-clip)
  - [Get Description](#get-description)
  - [Clear Clipboard](#clear-clipboard)
  - [Start / Stop Observing](#start--stop-observing)
  - [Receive Events](#receive-events)
  - [Error Handling](#error-handling)
- [iOS](#ios)
  - [Setup](#setup-1)
  - [Pasteboard Scopes](#pasteboard-scopes)
  - [Copy Plain Text](#copy-plain-text-1)
  - [Copy HTML Text](#copy-html-text-1)
  - [Copy URL](#copy-url)
  - [Copy Image File](#copy-image-file)
  - [Copy Image Data](#copy-image-data)
  - [Copy Color](#copy-color)
  - [Copy Custom Data](#copy-custom-data)
  - [Copy Multiple Text](#copy-multiple-text-1)
  - [Copy Multi Representation](#copy-multi-representation)
  - [Copy Options: Local Only and Expiration](#copy-options-local-only-and-expiration)
  - [Append](#append)
  - [Read](#read)
  - [Read Data](#read-data)
  - [Snapshot](#snapshot)
  - [Load Item](#load-item)
  - [Cancel Loads](#cancel-loads)
  - [Detect Patterns](#detect-patterns)
  - [Detect Values](#detect-values)
  - [Observe Changes](#observe-changes)
  - [Check Foreground Change](#check-foreground-change)
  - [Clear](#clear)
  - [Concurrency and Busy Rejection](#concurrency-and-busy-rejection)
  - [Receive Events](#receive-events-1)
  - [Error Handling](#error-handling-1)
- [macOS](#macos)
  - [Setup](#setup-2)
  - [Pasteboard Scopes](#pasteboard-scopes-1)
  - [Copy Plain Text](#copy-plain-text-2)
  - [Copy HTML Text](#copy-html-text-2)
  - [Copy URL](#copy-url-1)
  - [Copy Custom Data](#copy-custom-data-1)
  - [Copy Multiple Items](#copy-multiple-items)
  - [Copy Multi Representation](#copy-multi-representation-1)
  - [Copy Options: Local Only](#copy-options-local-only)
  - [Append](#append-1)
  - [Read](#read-1)
  - [Read Data](#read-data-1)
  - [Snapshot](#snapshot-1)
  - [Detect Patterns](#detect-patterns-1)
  - [Detect Values](#detect-values-1)
  - [Detect Metadata](#detect-metadata)
  - [Access Behavior](#access-behavior)
  - [Observe Changes](#observe-changes-1)
  - [Check Foreground Change](#check-foreground-change-1)
  - [Clear](#clear-1)
  - [Size Limits](#size-limits)
  - [App Sandbox](#app-sandbox)
  - [Receive Events](#receive-events-2)
  - [Error Handling](#error-handling-2)

---

## Android

Clipboard support targets Android and iOS. There is no Windows or macOS implementation of this feature. The two platforms have separate managers and separate APIs; see [iOS](#ios) for the iOS side.

### Setup

#### Import the namespace

`AndroidClipboardManager` compiles only when the Android build target is selected.

```csharp
// Guard: Android only. AndroidClipboardManager does not exist on other build targets.
#if UNITY_ANDROID
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
#endif
```

Call sites in your own scripts (for example a MonoBehaviour that also runs in the Editor) should additionally exclude the Editor at the call site, since the native bridge only exists on an Android device:

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = "Hello" });
#endif
```

#### Synchronous vs. asynchronous APIs

- `Read`, `HasClip`, and `GetDescription` are synchronous: they return their result directly and never raise `ClipboardOperationCompleted`.
- `CopyPlainText`, `CopyHtmlText`, `CopyUri`, `CopyMultipleText`, `Clear`, and `StopObserving` are asynchronous: they report their result through the `ClipboardOperationCompleted` event, followed by an optional per-call callback.
- `StartObserving` reports no result at all, on success or failure. See [Start / Stop Observing](#start--stop-observing).

#### content:// URIs (required for Copy URI and Copy Screenshot)

The clipboard API takes a URI string but has no built-in way to build one. Use the FileProvider bundled in the native-toolkit AAR (the same FileProvider used by the Share feature). It is declared in the AAR manifest as `${applicationId}.native_toolkit.share.fileprovider`; no additional manifest entry is required in your app.

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

> **Note:** The FileProvider authority is built from `Application.identifier`. If your Gradle template applies an `applicationIdSuffix`, verify the authority matches the merged manifest; otherwise `getUriForFile` throws `IllegalArgumentException`.

---

### Copy Plain Text

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

### Copy Plain Text (Empty, Allowed)

A blank `text` value is explicitly accepted by the native layer and does not fail, unlike `CopyHtmlText`'s `htmlText`.

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

### Copy HTML Text

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

### Copy HTML Text with Empty Plain Text Fallback

`plainText` may be blank; only a blank `htmlText` fails (see [Error Handling](#error-handling)).

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

### Copy URI

Copies a `content://` URI, for example a reference to an image or file. See [content:// URIs](#content-uris-required-for-copy-uri-and-copy-screenshot) above for how to build the URI string.

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

> **Note:** Whether the receiving app can read the pasted `content://` URI depends on the device and the target app; a plain-text receiver will not resolve it.

---

### Copy Multiple Text

Copies multiple plain-text items as one clip. Individual empty strings inside `texts` are accepted; only an empty array fails (see [Error Handling](#error-handling)).

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

### Copy Sensitive Text

Set `isSensitive` to request preview suppression in the system clipboard UI. This hint only takes effect on Android 13 (API 33) or later; on earlier versions the clip is copied normally without suppression.

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

### Game Use Cases

Typical in-game clipboard flows: sharing an invite code, letting a player paste a code they received, and copying a screenshot to share elsewhere.

#### Copy Invite Code

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

#### Paste Code from Clipboard

Reads the clipboard synchronously and extracts the first item's plain text. This does not fall back to a coerced/best-effort text: a clip that holds only a `content://` URI (such as one produced by [Copy Screenshot](#copy-screenshot)) reports that no text item was found, rather than displaying the URI as if it were a pasted code.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
var result = AndroidClipboardManager.Instance.Read();
switch (result.Status)
{
    case ClipboardReadStatus.HasContent:
        string? code = ExtractFirstText(result.Contents!);
        // code is null when the clip holds no plain-text item (for example a URI-only clip).
        break;
    case ClipboardReadStatus.Empty:
        // Clipboard is empty. This is a normal outcome, not a failure.
        break;
    default:
        // result.ErrorCode / result.ErrorMessage describe the failure.
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

> **Security note:** Never log the pasted value; it may be a coupon code or other sensitive data. Log only `result.Status` and `result.ErrorCode`.

#### Copy Screenshot

Captures the current frame and copies it as a `content://` URI. `ScreenCapture.CaptureScreenshotAsTexture` requires the frame to be fully rendered, so the capture must run inside a coroutine after `WaitForEndOfFrame`. Always destroy the captured `Texture2D` once its PNG bytes have been written, or it leaks.

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

> **Note:** Whether the receiving app can read the pasted screenshot depends on the device and the target app, as with any `content://` clip.

---

### Read Clipboard

Synchronous. Returns clip content, an empty result, or a failure — an empty clipboard is a normal outcome, distinct from a failed read.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
var result = AndroidClipboardManager.Instance.Read();
switch (result.Status)
{
    case ClipboardReadStatus.HasContent:
        ClipContents contents = result.Contents!;
        // contents.Label, contents.MimeTypes, contents.Items (Text / HtmlText / Uri / CoercedText per item)
        break;
    case ClipboardReadStatus.Empty:
        // Normal outcome, not a failure.
        break;
    default:
        // result.ErrorCode / result.ErrorMessage describe the failure.
        break;
}
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_ReadClipboard.png" alt="Example_AndroidClipboardManager_ReadClipboard" width="400" />
</p>

> **Security note:** Clipboard content may hold passwords or tokens. Log only `result.Status` and `result.ErrorCode`; never log the clip content itself.

---

### Has Clip

Synchronous. Returns `false` both when the clipboard is genuinely empty and when the check itself could not be performed; the two cases are indistinguishable from C#.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
bool hasClip = AndroidClipboardManager.Instance.HasClip();
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_HasClip.png" alt="Example_AndroidClipboardManager_HasClip" width="400" />
</p>

---

### Get Description

Synchronous. Reads clipboard metadata (label, MIME types, styled-text flag, classification status) without touching the clip body.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
var result = AndroidClipboardManager.Instance.GetDescription();
switch (result.Status)
{
    case ClipboardReadStatus.HasContent:
        ClipDescriptionInfo info = result.Description!;
        // info.Label, info.MimeTypes, info.IsStyledText, info.ClassificationStatus (null below API 31)
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

### Clear Clipboard

Asynchronous; reports through `ClipboardOperationCompleted`.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidClipboardManager.Instance.Clear();
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_ClearClipboard.png" alt="Example_AndroidClipboardManager_ClearClipboard" width="400" />
</p>

---

### Start / Stop Observing

`StartObserving` reports no result at all, on either success or failure — do not display it as a success. Clipboard changes while observing are delivered through the `ClipboardChanged` event (see [Receive Events](#receive-events)). A second call while already observing is a no-op on the native side. Observation is only reliable while the app is in the foreground (an Android 10+ platform restriction).

`StopObserving` is asynchronous like the other operations and reports through `ClipboardOperationCompleted`. Call it in `OnDisable` so observation does not continue after your screen is hidden.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// Start: no result is reported. Change the clipboard afterward to verify via ClipboardChanged.
AndroidClipboardManager.Instance.StartObserving();

// Stop: reports through ClipboardOperationCompleted, like Clear.
AndroidClipboardManager.Instance.StopObserving();
#endif
```

<p align="center">
    <img src="images/android/clipboard/Example_AndroidClipboardManager_StartObserving.png" alt="Example_AndroidClipboardManager_StartObserving" width="400" />
</p>

---

### Receive Events

Subscribe to events on `OnEnable` and unsubscribe on `OnDisable`. Also call `StopObserving` on `OnDisable` so a hidden screen does not keep observing.

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

`ClipboardOperationCompleted` always fires before any per-call callback passed to `CopyPlainText`, `CopyHtmlText`, `CopyUri`, `CopyMultipleText`, `Clear`, or `StopObserving`. If both a subscriber to the event and a per-call callback throw, each exception is caught independently — one throwing does not prevent the other from being invoked.

---

### Error Handling

All asynchronous operations report success or failure via `ClipboardOperationCompleted`. `ErrorMessage` is non-null only when `IsSuccess` is `false`.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// Blank HTML text fails: CLIPBOARD_EMPTY_CONTENT.
// ErrorMessage: "Clipboard content is empty. Please provide text or HTML."
AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload
{
    plainText = "Hello",
    htmlText = ""
});

// Empty items array fails: CLIPBOARD_EMPTY_ITEMS.
// ErrorMessage: "No items provided for clipboard copy."
AndroidClipboardManager.Instance.CopyMultipleText(new CopyMultipleTextPayload
{
    texts = Array.Empty<string>()
});

// Blank URI fails: CLIPBOARD_INVALID_URI.
AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
{
    uri = ""
});

// http:// scheme is rejected; only content:// URIs are supported: CLIPBOARD_INVALID_URI.
// ErrorMessage starts with "Invalid URI:".
AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
{
    uri = "http://example.com/x"
});
#endif
```

The full set of stable error codes reported through `ErrorCode`:

| Error Code | Meaning |
| --- | --- |
| `CLIPBOARD_EMPTY_CONTENT` | `CopyHtmlText` was called with a blank `htmlText`. |
| `CLIPBOARD_EMPTY_ITEMS` | `CopyMultipleText` was called with an empty `texts` array. |
| `CLIPBOARD_INVALID_URI` | `CopyUri` was called with a blank, malformed, or non-`content://` URI. |
| `CLIPBOARD_READ_NOT_ALLOWED` | The native layer refused a read (for example, a focus/permission restriction). |
| `CLIPBOARD_SECURITY` | The native layer denied the operation for a security reason. |
| `CLIPBOARD_UNAVAILABLE` | The system `ClipboardManager` could not be obtained. |
| `CLIPBOARD_UNKNOWN` | An unrecognized failure; also used for parse failures on the Unity side. |

`Read` and `GetDescription` can additionally fail with `CLIPBOARD_BRIDGE_UNAVAILABLE` when the native bridge itself could not be reached (not running on Android, plugin not initialized, or no current Activity) — this is a Unity-side error code, not one reported by the native layer.

---

## iOS

### Setup

#### Import the namespace

`IosClipboardManager` compiles whenever the iOS build target is selected, including in the Editor. Calling it in the Editor does not crash: every operation returns an immediate `CLIPBOARD_BRIDGE_UNAVAILABLE` failure without touching the native bridge, so the same code can stay in a scene that also runs in the Editor.

```csharp
#if UNITY_IOS || UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
#endif
```

The native layer targets iOS 18 or later.

#### Every operation is asynchronous

There is no synchronous API on iOS. Each call takes an optional per-call callback and also raises an event that fires for every call of that kind.

| Method | Callback result | Event |
| --- | --- | --- |
| `Copy`, `Append`, `Clear`, `RemovePasteboard`, `CancelLoads`, `StartObserving`, `StopObserving` | `IosClipboardOperationResult` | `ClipboardOperationCompleted` |
| `Read` | `IosClipboardReadResult` | `ReadCompleted` |
| `ReadData` | `IosClipboardReadDataResult` | `ReadDataCompleted` |
| `GetSnapshot` | `IosClipboardSnapshotResult` | `SnapshotCompleted` |
| `CreatePasteboard` | `IosPasteboardScopeResult` | `PasteboardCreated` |
| `DetectPatterns` | `IosClipboardDetectedPatternsResult` | `PatternsDetected` |
| `DetectValues` | `IosClipboardDetectedValuesResult` | `ValuesDetected` |
| `LoadItem` | `IosClipboardLoadedItemResult` | `ItemLoaded` |
| `CheckForegroundChange` | `IosClipboardForegroundChangeResult` | `ForegroundChangeChecked` |

The events carry no way to tell which call they belong to. Use the per-call callback whenever a result must be matched to a specific request, and the events only for logging or shared UI. Every result exposes `IsSuccess` and, on failure, `Error` (an `IosClipboardErrorInfo` with `Code`, `Message`, and optional `Domain` / `NativeCode`).

#### Main thread only

Every public API must be called from the Unity main thread. A call from any other thread is rejected with `CLIPBOARD_MAIN_THREAD_REQUIRED` and never reaches the native layer.

#### One call per operation at a time

Calls are serialized per operation: a second `LoadItem` issued while the first is still running is rejected with `CLIPBOARD_BUSY`, while a `Read` and a `GetSnapshot` do run concurrently. See [Concurrency and Busy Rejection](#concurrency-and-busy-rejection).

#### Manager lifetime

`IosClipboardManager.Instance` creates the manager on first access, and the native layer is initialized on the first call. Destroying and recreating the manager during a run is not supported: once it has been destroyed, `IosClipboardManager.IsTerminated` is `true` and every API returns `CLIPBOARD_MANAGER_DESTROYED`.

---

### Pasteboard Scopes

Every operation runs against a scope. Passing `null` (the default for the `scope` parameter) means the general pasteboard.

| Scope | Factory | Notes |
| --- | --- | --- |
| General | `IosPasteboardScope.General` | The system pasteboard shared with other apps. |
| Named | `IosPasteboardScope.Named(name)` | An app-defined pasteboard. Create it once with `CreatePasteboard`. |
| Unique | `IosPasteboardScope.Unique(name)` | A pasteboard whose name the system generates; obtain the name from `CreatePasteboard`. |

`Named` and `Unique` throw `ArgumentException` when the name is blank, so validate user input before constructing a scope.

```csharp
#if UNITY_IOS || UNITY_EDITOR
private IosPasteboardScope _scope = IosPasteboardScope.General;

// Create a named pasteboard and keep it as the active scope.
IosClipboardManager.Instance.CreatePasteboard(
    IosPasteboardCreationRequest.Named("com.jonghyunkim.nativetoolkit.example.sample"),
    result =>
    {
        if (!result.IsSuccess || result.Scope == null)
        {
            Debug.LogError($"CreatePasteboard failed: {result.Error?.Code}");
            return;
        }

        _scope = result.Scope;
    });

// A system-named pasteboard: the name is only known from the result.
IosClipboardManager.Instance.CreatePasteboard(
    IosPasteboardCreationRequest.Unique,
    result => _scope = result.IsSuccess && result.Scope != null ? result.Scope : _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CreateNamedPasteboard.png" alt="Example_IosClipboardManager_CreateNamedPasteboard" width="400" />
</p>

A named or unique pasteboard is removed with `RemovePasteboard`. The general pasteboard cannot be removed; that call fails with `CLIPBOARD_CANNOT_REMOVE_GENERAL`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.RemovePasteboard(_scope, result =>
{
    if (result.IsSuccess)
    {
        _scope = IosPasteboardScope.General;
    }
});
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_RemoveActivePasteboard.png" alt="Example_IosClipboardManager_RemoveActivePasteboard" width="400" />
</p>

> **Note:** Reading a scope that was removed does not fail; the pasteboard is recreated empty. Treat an empty read as "gone", not as an error.

---

### Copy Plain Text

`Copy` replaces the whole pasteboard content. An empty string is accepted and clears the text item rather than failing.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.Copy(
    IosClipboardContent.PlainText("Hello 日本語 \U0001F680 テスト"),
    _scope,
    options: null,
    onResult: result =>
    {
        if (!result.IsSuccess)
        {
            Debug.LogError($"Copy failed: {result.Error?.Code}");
        }
    });
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyPlainText.png" alt="Example_IosClipboardManager_CopyPlainText" width="400" />
</p>

---

### Copy HTML Text

Writes a plain-text representation and an HTML representation as one item.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.Copy(
    IosClipboardContent.HtmlText("Hello", "<b>Hello</b>"),
    _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyHtmlText.png" alt="Example_IosClipboardManager_CopyHtmlText" width="400" />
</p>

---

### Copy URL

The string must be a valid URL; otherwise the native layer returns `CLIPBOARD_INVALID_URL`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.Copy(IosClipboardContent.Url("https://unity.com"), _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyUrl.png" alt="Example_IosClipboardManager_CopyUrl" width="400" />
</p>

---

### Copy Image File

Copies an image by path. Unlike Android, no FileProvider is involved: any readable path works. A missing path fails with `CLIPBOARD_FILE_NOT_FOUND`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
string path = Path.Combine(Application.persistentDataPath, "ios_clipboard_sample_image.png");
File.WriteAllBytes(path, pngBytes);

IosClipboardManager.Instance.Copy(IosClipboardContent.ImageFile(path), _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyImageFile.png" alt="Example_IosClipboardManager_CopyImageFile" width="400" />
</p>

---

### Copy Image Data

Copies encoded image bytes together with their uniform type identifier.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.Copy(
    IosClipboardContent.ImageData(pngBytes, "public.png"),
    _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyImageData.png" alt="Example_IosClipboardManager_CopyImageData" width="400" />
</p>

> **Note:** `LoadItem` with `IosClipboardLoadRequest.Image` re-encodes the image, so the byte count it returns does not match the bytes that were copied. Use [Read Data](#read-data) when the exact bytes matter.

---

### Copy Color

Components must be finite; `IosClipboardContent.Color` throws `ArgumentException` for `NaN` or infinity before any call is made. Finite values outside `0.0...1.0` do reach the native layer and fail there with `CLIPBOARD_INVALID_COLOR`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.Copy(IosClipboardContent.Color(0.2, 0.4, 0.8, 1.0), _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyColor.png" alt="Example_IosClipboardManager_CopyColor" width="400" />
</p>

> **Note:** A copied color is not visible in a plain-text receiver. Confirm it with [Snapshot](#snapshot), whose `HasColors` becomes `true`.

---

### Copy Custom Data

Copies raw bytes under any valid uniform type identifier. An invalid identifier fails with `CLIPBOARD_INVALID_TYPE`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
byte[] payload = new byte[64];
payload.AsSpan().Fill(0x41);

IosClipboardManager.Instance.Copy(
    IosClipboardContent.CustomData(payload, "public.data"),
    _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyCustomData.png" alt="Example_IosClipboardManager_CopyCustomData" width="400" />
</p>

---

### Copy Multiple Text

Writes one text item per array element. Individual elements may be empty, but an empty array fails with `CLIPBOARD_EMPTY_ITEMS`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.Copy(
    IosClipboardContent.MultipleText(new[] { "First", string.Empty, "Third" }),
    _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyMultipleText.png" alt="Example_IosClipboardManager_CopyMultipleText" width="400" />
</p>

---

### Copy Multi Representation

Writes a single item that carries several representations, so a receiving app can pick the type it understands. An empty dictionary fails with `CLIPBOARD_EMPTY_ITEMS`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
var representations = new Dictionary<string, byte[]>
{
    { "public.utf8-plain-text", Encoding.UTF8.GetBytes("LOCALONLY-0001") },
    { "com.jonghyunkim.nativetoolkit.example.custom", payload }
};

IosClipboardManager.Instance.Copy(
    IosClipboardContent.MultiRepresentation(representations),
    _scope);
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyMultiRepresentation.png" alt="Example_IosClipboardManager_CopyMultiRepresentation" width="400" />
</p>

---

### Copy Options: Local Only and Expiration

`IosClipboardCopyOptions` applies to `Copy` only; `Append` has no options parameter.

- `LocalOnly` asks the system not to hand the content to nearby devices via Universal Clipboard.
- `ExpirationDate` must be in the future, otherwise the native layer returns `CLIPBOARD_INVALID_EXPIRATION`.
- `IosClipboardCopyOptions.PrivacyPreservingDefault` is `localOnly: true` with no expiration.

```csharp
#if UNITY_IOS || UNITY_EDITOR
// Keep the content on this device only.
IosClipboardManager.Instance.Copy(
    IosClipboardContent.PlainText("LOCALONLY-0001"),
    _scope,
    IosClipboardCopyOptions.Create(localOnly: true));

// Drop the content after 30 seconds.
IosClipboardManager.Instance.Copy(
    IosClipboardContent.PlainText("Hello 日本語 \U0001F680 テスト"),
    _scope,
    IosClipboardCopyOptions.Create(localOnly: false, DateTime.UtcNow.AddSeconds(30)));
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CopyExpiring.png" alt="Example_IosClipboardManager_CopyExpiring" width="400" />
</p>

> **Note:** Whether `localOnly` actually suppresses transfer to a nearby device cannot be confirmed from a single device. Treat it as a request to the system, not as a guarantee this package verifies.

---

### Append

`Append` adds an item instead of replacing the content. It takes no options.

```csharp
#if UNITY_IOS || UNITY_EDITOR
// A unique suffix per call, so the appended items can be told apart on Read.
string marker = "APPENDED-MARKER-" + Guid.NewGuid().ToString("N").Substring(0, 8);

IosClipboardManager.Instance.Append(
    IosClipboardContent.PlainText(marker),
    _scope,
    result => Debug.Log($"Append: {result.IsSuccess}"));
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_AppendPlainText.png" alt="Example_IosClipboardManager_AppendPlainText" width="400" />
</p>

---

### Read

Reads every item with its type identifiers and, where available, the text and URL representations.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.Read(_scope, result =>
{
    if (!result.IsSuccess)
    {
        Debug.LogError($"Read failed: {result.Error?.Code}");
        return;
    }

    foreach (IosClipboardItem item in result.Items)
    {
        // item.TypeIdentifiers, item.Text, item.UrlString, item.ImageDataUtType
        Debug.Log($"types: {item.TypeIdentifiers.Count}, hasText: {item.Text != null}");
    }
});
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_Read.png" alt="Example_IosClipboardManager_Read" width="400" />
</p>

> **Note:** On the general pasteboard, iOS shows the system paste-permission prompt the first time an app reads content it did not write. `Read`, `ReadData`, `LoadItem`, and the detection APIs read content and can trigger it; `GetSnapshot` does not, because it only reports which types are present.

---

### Read Data

Returns the raw bytes stored under one uniform type identifier. When no item carries that type the call still succeeds with `HasData == false`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.ReadData("public.png", _scope, result =>
{
    if (!result.IsSuccess)
    {
        Debug.LogError($"ReadData failed: {result.Error?.Code}");
        return;
    }

    if (!result.HasData)
    {
        Debug.Log("No item carries public.png");
        return;
    }

    byte[] data = result.Data!;
    Debug.Log($"utType: {result.UtType}, bytes: {result.ByteCount}");
});
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_ReadDataPng.png" alt="Example_IosClipboardManager_ReadDataPng" width="400" />
</p>

> **Note:** A payload whose decoded size exceeds 64 MiB is rejected with `CLIPBOARD_CONTENT_TOO_LARGE` before any buffer is allocated.

---

### Snapshot

Reports what the pasteboard holds without reading the content: item count, the type identifiers, and the `HasStrings` / `HasUrls` / `HasImages` / `HasColors` flags. Passing `matchingTypes` additionally fills `MatchingItemIndexes` with the indexes of the items that carry one of those types; without it, `MatchingItemIndexes` is `null`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.GetSnapshot(_scope, matchingTypes: null, result =>
{
    if (!result.IsSuccess || result.Snapshot == null) return;

    IosClipboardSnapshot snapshot = result.Snapshot;
    Debug.Log($"items: {snapshot.NumberOfItems}, strings: {snapshot.HasStrings}, " +
              $"urls: {snapshot.HasUrls}, images: {snapshot.HasImages}, colors: {snapshot.HasColors}");
});

// Restrict the query to specific types.
IosClipboardManager.Instance.GetSnapshot(
    _scope,
    new[] { "public.utf8-plain-text", "public.png" },
    result =>
    {
        IReadOnlyList<int>? matching = result.Snapshot?.MatchingItemIndexes;
        Debug.Log($"matching items: {matching?.Count ?? 0}");
    });
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_Snapshot.png" alt="Example_IosClipboardManager_Snapshot" width="400" />
</p>

---

### Load Item

`LoadItem` asks the system to materialize one item as text, a URL, an image, or a file. It is the only API that can hand back a file on disk.

| Request | Result `Kind` | Populated members |
| --- | --- | --- |
| `IosClipboardLoadRequest.Text` | `Text` | `Text` |
| `IosClipboardLoadRequest.Url` | `Url` | `UrlString` |
| `IosClipboardLoadRequest.Image` | `ImageData` | `Data`, `UtType` |
| `IosClipboardLoadRequest.File(utType)` | `File` | `Path`, `UtType` |

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.Text, _scope, result =>
{
    if (!result.IsSuccess || result.Item == null)
    {
        Debug.LogError($"LoadItem failed: {result.Error?.Code}");
        return;
    }

    Debug.Log($"kind: {result.Item.Kind}, textLength: {result.Item.Text?.Length ?? 0}");
});
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_LoadText.png" alt="Example_IosClipboardManager_LoadText" width="400" />
</p>

**The file returned by a `File` request is owned by the caller.** The native layer copies the item into a per-request temporary directory and never deletes it, so the app must delete that directory once it is done.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.File("public.data"), _scope, result =>
{
    if (!result.IsSuccess || result.Item?.Path == null) return;

    string path = result.Item.Path;
    try
    {
        long size = new FileInfo(path).Length;
        Debug.Log($"loaded file size: {size}");
    }
    finally
    {
        // Delete the request directory the native layer created for this call.
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
});
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_LoadFile.png" alt="Example_IosClipboardManager_LoadFile" width="400" />
</p>

---

### Cancel Loads

`CancelLoads` aborts the loads that are still running. A cancelled load reports `CLIPBOARD_CANCELLED`, which is a normal outcome and not an error to alert on.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.Image, _scope, result =>
{
    if (!result.IsSuccess && result.Error?.Code == "CLIPBOARD_CANCELLED")
    {
        Debug.Log("The load was cancelled on purpose.");
        return;
    }
});

IosClipboardManager.Instance.CancelLoads();
#endif
```

---

### Detect Patterns

Reports which of the requested patterns occur in the pasteboard text, without returning the values themselves. An empty pattern array fails with `CLIPBOARD_EMPTY_PATTERNS`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
private static readonly IosClipboardDetectionPattern[] AllPatterns =
{
    IosClipboardDetectionPattern.ProbableWebUrl,
    IosClipboardDetectionPattern.ProbableWebSearch,
    IosClipboardDetectionPattern.Number,
    IosClipboardDetectionPattern.Link,
    IosClipboardDetectionPattern.EmailAddress,
    IosClipboardDetectionPattern.PhoneNumber,
    IosClipboardDetectionPattern.PostalAddress,
    IosClipboardDetectionPattern.CalendarEvent,
    IosClipboardDetectionPattern.FlightNumber,
    IosClipboardDetectionPattern.MoneyAmount,
    IosClipboardDetectionPattern.ShipmentTrackingNumber
};

IosClipboardManager.Instance.DetectPatterns(AllPatterns, _scope, result =>
{
    if (!result.IsSuccess) return;

    foreach (IosClipboardDetectionPattern pattern in result.Patterns)
    {
        Debug.Log($"detected: {pattern}");
    }
});
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_DetectPatterns.png" alt="Example_IosClipboardManager_DetectPatterns" width="400" />
</p>

> **Note:** `Number` and `ProbableWebSearch` are reported only when the whole text is a number or a search term. They do not appear for text that merely contains a number alongside other patterns.

---

### Detect Values

Returns the detected values themselves, grouped by category.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.DetectValues(AllPatterns, _scope, result =>
{
    if (!result.IsSuccess || result.Values == null) return;

    IosClipboardDetectedValues values = result.Values;
    Debug.Log($"patterns: {values.DetectedPatterns.Count}, emails: {values.EmailAddresses.Count}, " +
              $"phones: {values.PhoneNumbers.Count}, addresses: {values.PostalAddresses.Count}, " +
              $"events: {values.CalendarEvents.Count}, flights: {values.FlightNumbers.Count}, " +
              $"money: {values.MoneyAmounts.Count}, shipments: {values.ShipmentTrackingNumbers.Count}, " +
              $"links: {values.Links.Count}");
});
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_DetectValues.png" alt="Example_IosClipboardManager_DetectValues" width="400" />
</p>

> **Note:** Detected values are user content. Log counts, not values.

---

### Observe Changes

`StartObserving` subscribes to the system change notification for a scope and reports the outcome through its own callback. Changes then arrive on `ClipboardChanged`.

`StartObserving` and `StopObserving` share one single-flight key, so only one of them can be in flight; calling the other one meanwhile is rejected with `CLIPBOARD_BUSY`. Calling `StartObserving` again while already observing replaces the current observation, because the native layer stops the previous one first. Observing a named pasteboard that does not exist fails with `CLIPBOARD_UNAVAILABLE`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.StartObserving(
    _scope,
    onChanged: null,
    onStarted: result =>
    {
        if (!result.IsSuccess)
        {
            Debug.LogError($"StartObserving failed: {result.Error?.Code}");
        }
    });

// Stop when the screen goes away.
IosClipboardManager.Instance.StopObserving(result => Debug.Log($"StopObserving: {result.IsSuccess}"));
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_StartObserving.png" alt="Example_IosClipboardManager_StartObserving" width="400" />
</p>

> **Note:** A failed `StartObserving` leaves the app not observing. The native layer stops the previous observation before it tries to start the new one, so there is nothing left running to stop.

---

### Check Foreground Change

The system only posts change notifications while the app is in the foreground. A copy made by another app while this app is in the background never reaches `ClipboardChanged`. `CheckForegroundChange` compares the pasteboard change count and reports whether the content changed since the last check, so call it when the app returns to the foreground.

```csharp
#if UNITY_IOS || UNITY_EDITOR
private void OnApplicationPause(bool paused)
{
    if (paused) return;

    IosClipboardManager.Instance.CheckForegroundChange(_scope, result =>
    {
        if (result.IsSuccess && result.Changed)
        {
            Debug.Log("The clipboard changed while the app was in the background.");
        }
    });
}
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_CheckForegroundChange.png" alt="Example_IosClipboardManager_CheckForegroundChange" width="400" />
</p>

---

### Clear

Removes every item from the scope.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosClipboardManager.Instance.Clear(_scope, result => Debug.Log($"Clear: {result.IsSuccess}"));
#endif
```

<p align="center">
    <img src="images/ios/clipboard/Example_IosClipboardManager_ClearActiveScope.png" alt="Example_IosClipboardManager_ClearActiveScope" width="400" />
</p>

---

### Concurrency and Busy Rejection

Only calls of the *same* operation are serialized. A second call of an operation that is still running is rejected immediately with `CLIPBOARD_BUSY`; the first call is unaffected and still completes normally. Different operations run concurrently, so results can arrive out of order.

```csharp
#if UNITY_IOS || UNITY_EDITOR
// The second LoadItem is rejected with CLIPBOARD_BUSY while the first is still running.
IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.Text, _scope, first =>
    Debug.Log($"first: {first.IsSuccess}"));

IosClipboardManager.Instance.LoadItem(IosClipboardLoadRequest.Text, _scope, second =>
    Debug.Log($"second: {second.Error?.Code}"));  // CLIPBOARD_BUSY
#endif
```

Because a callback cannot be identified from the result alone, keep the identity of the call on the closure that issues it when several calls can be in flight:

```csharp
#if UNITY_IOS || UNITY_EDITOR
int sequence = ++_sequence;
IosClipboardManager.Instance.Read(_scope, result =>
    Debug.Log($"#{sequence} read: {result.IsSuccess}"));
#endif
```

---

### Receive Events

Subscribe in `OnEnable` and unsubscribe in `OnDisable`. Also stop observing in `OnDisable` so a hidden screen does not keep receiving changes.

```csharp
private void OnEnable()
{
#if UNITY_IOS || UNITY_EDITOR
    var manager = IosClipboardManager.Instance;
    manager.ClipboardOperationCompleted += OnClipboardOperationCompleted;
    manager.ClipboardChanged += OnClipboardChanged;
#endif
}

private void OnDisable()
{
#if UNITY_IOS || UNITY_EDITOR
    var manager = IosClipboardManager.Instance;
    manager.StopObserving();
    manager.ClipboardOperationCompleted -= OnClipboardOperationCompleted;
    manager.ClipboardChanged -= OnClipboardChanged;
#endif
}

#if UNITY_IOS || UNITY_EDITOR
private void OnClipboardOperationCompleted(IosClipboardOperationResult result)
{
    Debug.Log($"[event] {result.Operation}: {result.IsSuccess}, code: {result.Error?.Code}");
}

private void OnClipboardChanged(IosClipboardChangeEvent changeEvent)
{
    // Kind: Changed, ChangedDetectedOnForeground, Removed, or Unknown.
    Debug.Log($"[event] {changeEvent.Kind}, added: {changeEvent.TypesAdded.Count}, " +
              $"removed: {changeEvent.TypesRemoved.Count}");
}
#endif
```

`IosClipboardManager.OperationCopy`, `OperationAppend`, `OperationClear`, `OperationRemovePasteboard`, `OperationCancelLoads`, `OperationStartObserving`, and `OperationStopObserving` are the constants that appear in `IosClipboardOperationResult.Operation`.

> **Note:** For a change this app makes itself, the change notification can arrive before the copy's own callback. If both write to the same UI element, the event can overwrite the result of the copy that caused it.

---

### Error Handling

Every failure carries an `IosClipboardErrorInfo`. `Code` is stable and safe to branch on; `Message` is for logs. `Domain` and `NativeCode` are present only for failures the system reported.

```csharp
#if UNITY_IOS || UNITY_EDITOR
// Empty items array: CLIPBOARD_EMPTY_ITEMS.
IosClipboardManager.Instance.Copy(
    IosClipboardContent.MultipleText(Array.Empty<string>()),
    _scope,
    options: null,
    onResult: result => Debug.Log(result.Error?.Code));

// Missing file: CLIPBOARD_FILE_NOT_FOUND.
IosClipboardManager.Instance.Copy(
    IosClipboardContent.ImageFile("/nonexistent/ios-clipboard-missing.png"), _scope);

// Malformed uniform type identifier: CLIPBOARD_INVALID_TYPE.
IosClipboardManager.Instance.Copy(
    IosClipboardContent.CustomData(payload, "not a uti"), _scope);

// Malformed URL: CLIPBOARD_INVALID_URL.
IosClipboardManager.Instance.Copy(IosClipboardContent.Url("not a valid url"), _scope);

// Finite but outside 0.0...1.0: CLIPBOARD_INVALID_COLOR.
IosClipboardManager.Instance.Copy(IosClipboardContent.Color(1.5, 0.0, 0.0, 1.0), _scope);

// The general pasteboard cannot be removed: CLIPBOARD_CANNOT_REMOVE_GENERAL.
IosClipboardManager.Instance.RemovePasteboard(IosPasteboardScope.General);

// Empty pattern array: CLIPBOARD_EMPTY_PATTERNS, returned before the native layer is reached.
IosClipboardManager.Instance.DetectPatterns(
    Array.Empty<IosClipboardDetectionPattern>(), _scope);
#endif
```

Codes reported by the native layer:

| Error Code | Meaning |
| --- | --- |
| `CLIPBOARD_EMPTY_CONTENT` | Empty text, data, or representation value. |
| `CLIPBOARD_EMPTY_ITEMS` | An empty `texts` array or an empty representation dictionary. |
| `CLIPBOARD_EMPTY_PATTERNS` | No detection patterns were specified. |
| `CLIPBOARD_INVALID_URL` | The URL string could not be parsed. |
| `CLIPBOARD_INVALID_TYPE` | A `utType` or representation key is not a valid uniform type identifier. |
| `CLIPBOARD_INVALID_NAME` | The pasteboard name is not usable. |
| `CLIPBOARD_INVALID_COLOR` | A color component is outside `0.0...1.0`. |
| `CLIPBOARD_INVALID_IMAGE_DATA` | The image data could not be decoded. |
| `CLIPBOARD_INVALID_EXPIRATION` | `ExpirationDate` is not in the future. |
| `CLIPBOARD_INVALID_REQUEST` | The request itself was rejected. The individual reason is not reported. |
| `CLIPBOARD_CONTENT_TOO_LARGE` | The content exceeds the configured size limit (64 MiB). |
| `CLIPBOARD_FILE_NOT_FOUND` | The image file path does not exist. |
| `CLIPBOARD_IMAGE_LOAD_FAILED` | The image could not be read. |
| `CLIPBOARD_IMAGE_ENCODE_FAILED` | The pasted image could not be encoded. |
| `CLIPBOARD_UNAVAILABLE` | The requested pasteboard does not exist. |
| `CLIPBOARD_CANNOT_REMOVE_GENERAL` | `RemovePasteboard` was called with the general scope. |
| `CLIPBOARD_NO_MATCHING_ITEM` | No item matches the requested type. |
| `CLIPBOARD_LOAD_FAILED` | The item could not be loaded. |
| `CLIPBOARD_UNEXPECTED_TYPE` | The item could not be converted to the requested type. |
| `CLIPBOARD_FILE_COPY_FAILED` | The pasted file could not be copied to the temporary directory. |
| `CLIPBOARD_CANCELLED` | The load was cancelled. This is a normal outcome, not a failure to alert on. |
| `CLIPBOARD_TIMED_OUT` | Detection, an item load, or image coding exceeded its time limit. |
| `CLIPBOARD_DETECTION_FAILED` | Pattern detection failed in the system. |
| `CLIPBOARD_UNKNOWN` | An unclassified system error. |

Codes produced on the Unity side, before or instead of a native call:

| Error Code | Meaning |
| --- | --- |
| `CLIPBOARD_BUSY` | The same operation is already in progress. |
| `CLIPBOARD_BRIDGE_UNAVAILABLE` | Not running on an iOS device (this includes the Editor), or the bridge call itself threw. |
| `CLIPBOARD_MAIN_THREAD_REQUIRED` | The API was called from a thread other than the Unity main thread. |
| `CLIPBOARD_MANAGER_DESTROYED` | The manager was destroyed; it cannot be recreated during the run. |
| `CLIPBOARD_INVALID_REQUEST` | A required argument was `null`. |
| `CLIPBOARD_EMPTY_PATTERNS` | An empty pattern array, rejected before the native call. |
| `CLIPBOARD_CONTENT_TOO_LARGE` | The decoded size of a returned payload exceeds 64 MiB. |
| `CLIPBOARD_UNKNOWN` | The native response was empty or could not be parsed. |

Some inputs never reach a result at all: they throw from the factory that builds them. Validate user input before constructing these.

| Factory | Exception |
| --- | --- |
| `IosPasteboardScope.Named` / `Unique`, `IosPasteboardCreationRequest.Named` | `ArgumentException` when the name is blank |
| `IosClipboardContent.Color` | `ArgumentException` when a component is `NaN` or infinity |
| Every other `IosClipboardContent` factory, `IosClipboardLoadRequest.File` | `ArgumentNullException` for a `null` argument |

## macOS

### Setup

#### Import the namespace

`MacClipboardManager` compiles whenever the macOS standalone build target is selected, including in the Editor. Calling it in the Editor does not crash: every operation returns an immediate `BridgeUnavailable` (9002) failure without touching the native bridge, so the same code can stay in a scene that also runs in the Editor.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
#endif
```

The native layer targets macOS 15 or later. Three operations need macOS 15.4; see [Detect Patterns](#detect-patterns-1).

#### Every operation is asynchronous

There is no synchronous API on macOS. Each call takes an optional per-call callback and also raises an event that fires for every call of that kind.

| Method | Callback result | Event |
| --- | --- | --- |
| `Copy`, `Append` | `MacClipboardOwnershipResult` | `OwnershipChanged` |
| `Read` | `MacClipboardReadResult` | `ReadCompleted` |
| `ReadData` | `MacClipboardReadDataResult` | `ReadDataCompleted` |
| `Snapshot` | `MacClipboardSnapshotResult` | `SnapshotCompleted` |
| `Clear` | `MacClipboardChangeCountResult` | `ClearCompleted` |
| `CreatePasteboard` | `MacPasteboardScopeResult` | `PasteboardCreated` |
| `RemovePasteboard`, `StartObserving`, `StopObserving` | `MacClipboardOperationResult` | `ClipboardOperationCompleted` |
| `DetectPatterns` | `MacClipboardDetectedPatternsResult` | `PatternsDetected` |
| `DetectValues` | `MacClipboardDetectedValuesResult` | `ValuesDetected` |
| `DetectMetadata` | `MacClipboardDetectedMetadataResult` | `MetadataDetected` |
| `GetAccessBehavior` | `MacClipboardAccessBehaviorResult` | `AccessBehaviorChecked` |
| `CheckForegroundChange` | `MacClipboardForegroundChangeResult` | `ForegroundChangeChecked` |
| (observation) | `MacClipboardChangeEvent` | `ClipboardChanged` |

The events carry no way to tell which call they belong to. Use the per-call callback whenever a result must be matched to a specific request, and the events only for logging or shared UI. Every result exposes `IsSuccess` and, on failure, `Error` (a `MacClipboardErrorInfo` with an `int Code` and a `string Message`).

#### Main thread only

Every public API must be called from the Unity main thread. A call from any other thread is rejected with `MainThreadRequired` (9003) and never reaches the native layer. Callbacks and events are always delivered on the main thread, so Unity APIs can be used inside them directly.

#### One call per operation at a time

Calls are serialized per operation: a second `Read` issued while the first is still running is rejected with `Busy` (9001), while a `Read` and a `Snapshot` do run concurrently. `StartObserving` and `StopObserving` share a single key, so one cannot start while the other is pending.

#### Manager lifetime

`MacClipboardManager.Instance` creates the manager on first access, and the native layer is initialized on the first call. Destroying and recreating the manager during a run is not supported: once it has been destroyed, `MacClipboardManager.IsTerminated` is `true` and every API returns `ManagerDestroyed` (9004).

---

### Pasteboard Scopes

Every operation runs against a scope. Passing `null` (the default for the `scope` parameter) means the general pasteboard.

| Scope | Factory | Notes |
| --- | --- | --- |
| General | `MacPasteboardScope.General` | The system pasteboard shared with other apps. |
| Named | `MacPasteboardScope.Named(name)` | An app-defined pasteboard. Create it once with `CreatePasteboard`. |
| Unique | `MacPasteboardScope.Unique(name)` | A pasteboard whose name the system generates; obtain the name from `CreatePasteboard`. |

`Named` and `Unique` throw `ArgumentException` when the name is blank or whitespace only. The native layer would accept such a name and operate on an unintended pasteboard, so this check exists only in C#: validate user input before constructing a scope.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
private MacPasteboardScope _scope = MacPasteboardScope.General;

// Create a named pasteboard and keep it as the active scope.
MacClipboardManager.Instance.CreatePasteboard(
    MacPasteboardCreationRequest.Named("com.jonghyunkim.nativetoolkit.example.sample"),
    result =>
    {
        if (!result.IsSuccess || result.Scope == null)
        {
            Debug.LogError($"CreatePasteboard failed: {result.Error?.Code}");
            return;
        }

        _scope = result.Scope;
    });

// A system-named pasteboard: the name is only known from the result.
MacClipboardManager.Instance.CreatePasteboard(
    MacPasteboardCreationRequest.Unique,
    result => _scope = result.IsSuccess && result.Scope != null ? result.Scope : _scope);
#endif
```

<p align="center">
    <img src="images/mac/clipboard/Example_MacClipboardManager_CreateNamedPasteboard.png" alt="Example_MacClipboardManager_CreateNamedPasteboard" width="800" />
</p>

A named or unique pasteboard is removed with `RemovePasteboard`.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.RemovePasteboard(_scope, result =>
{
    if (result.IsSuccess)
    {
        _scope = MacPasteboardScope.General;
    }
});
#endif
```

> **Note:** `RemovePasteboard` discards the contents rather than the name. Reading a removed scope succeeds and returns no items, so treat an empty read as "gone" rather than waiting for an error.

The standard pasteboards cannot be removed. `general`, `font`, `ruler`, `find` and `drag` all fail with `CannotReleaseStandardPasteboard` (1508), and the check is by name: passing one of those five names as a `Unique` scope fails the same way.

Named and unique pasteboards outlive the process on the pasteboard server. Remove a unique pasteboard explicitly when it is no longer needed, and do not place confidential data on a named one.

---

### Copy Plain Text

`Copy` replaces the whole pasteboard content and returns a `MacPasteboardOwnership` that [Append](#append-1) needs.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
private MacPasteboardOwnership? _ownership;

MacClipboardManager.Instance.Copy(
    MacClipboardContent.PlainText("Hello macOS clipboard"),
    _scope,
    options: null,
    onResult: result =>
    {
        if (!result.IsSuccess)
        {
            Debug.LogError($"Copy failed: {result.Error?.Code}");
            return;
        }

        _ownership = result.Ownership;
    });
#endif
```

---

### Copy HTML Text

One item can carry several representations. `Html` writes `public.html` and, when a fallback is given, `public.utf8-plain-text` in the same item, so an app that cannot take HTML still gets readable text.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.Copy(
    MacClipboardContent.Single(MacClipboardContentItem.Html("<b>Hello</b>", "Hello")),
    _scope,
    options: null,
    onResult: result => _ownership = result.IsSuccess ? result.Ownership : _ownership);
#endif
```

---

### Copy URL

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.Copy(
    MacClipboardContent.Single(MacClipboardContentItem.Url("https://unity.com")),
    _scope,
    options: null,
    onResult: result => _ownership = result.IsSuccess ? result.Ownership : _ownership);
#endif
```

---

### Copy Custom Data

Raw bytes under an app-defined uniform type identifier. Other apps will not recognise the type, which is the point: use it to move data between your own scenes or processes.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
byte[] payload = System.Text.Encoding.UTF8.GetBytes("{\"level\":12}");

MacClipboardManager.Instance.Copy(
    MacClipboardContent.Single(MacClipboardContentItem.Data(
        "com.jonghyunkim.nativetoolkit.example.custom", payload)),
    _scope,
    options: null,
    onResult: result => _ownership = result.IsSuccess ? result.Ownership : _ownership);
#endif
```

---

### Copy Multiple Items

`Multiple` writes several items in order. What a receiving app does with them is its own decision: a rich text view reads all of them, while a single-line field usually takes the first.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.Copy(
    MacClipboardContent.Multiple(new[]
    {
        MacClipboardContentItem.PlainText("Hello macOS clipboard"),
        MacClipboardContentItem.Url("https://unity.com"),
    }),
    _scope,
    options: null,
    onResult: result => _ownership = result.IsSuccess ? result.Ownership : _ownership);
#endif
```

---

### Copy Multi Representation

Several representations of the same thing in one item. The receiving app picks the type it prefers.

`MacClipboardContentItem` has no public constructor. `FromRepresentations` is the general factory: it takes a type-to-bytes map and puts every entry in one item, which is how you combine types the named factories do not cover.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
var representations = new Dictionary<string, byte[]>
{
    [MacClipboardTypes.PlainText] = System.Text.Encoding.UTF8.GetBytes("Hello"),
    [MacClipboardTypes.Html] = System.Text.Encoding.UTF8.GetBytes("<b>Hello</b>"),
};

MacClipboardManager.Instance.Copy(
    MacClipboardContent.Single(MacClipboardContentItem.FromRepresentations(representations)),
    _scope,
    options: null,
    onResult: result => _ownership = result.IsSuccess ? result.Ownership : _ownership);
#endif
```

---

### Copy Options: Local Only

`MacClipboardCopyOptions` controls whether the write is offered to the user's other Apple devices through Universal Clipboard.

| Option | Meaning |
| --- | --- |
| `MacClipboardCopyOptions.PrivacyPreservingDefault` | `localOnly: true`. The write stays on this Mac. |
| `MacClipboardCopyOptions.Create(false)` | The write is offered to nearby Apple devices signed in to the same Apple Account. |
| `null` | The system default. |

Both behaviours are confirmed on hardware: with `localOnly: false` the text appears on a second device, and with `localOnly: true` that device keeps whatever it had.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Keep an access token on this Mac only.
MacClipboardManager.Instance.Copy(
    MacClipboardContent.PlainText("Hello macOS clipboard (localOnly true, local)"),
    _scope,
    MacClipboardCopyOptions.PrivacyPreservingDefault,
    result => _ownership = result.IsSuccess ? result.Ownership : _ownership);

// Let an invite code reach the user's iPhone.
MacClipboardManager.Instance.Copy(
    MacClipboardContent.PlainText("Hello macOS clipboard (localOnly false, shared)"),
    _scope,
    MacClipboardCopyOptions.Create(false),
    result => _ownership = result.IsSuccess ? result.Ownership : _ownership);
#endif
```

---

### Append

`Append` adds items without clearing what is already there, and it needs the `MacPasteboardOwnership` returned by the preceding `Copy`.

Two properties of `Append` differ from the other write path and matter in practice:

- **Ownership is enforced.** If another app copies in between, the append fails with `OwnershipLost` (1511) instead of being silently ignored.
- **A successful append leaves the change count untouched**, so the same ownership stays valid for the next append.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
if (_ownership != null)
{
    MacClipboardManager.Instance.Append(
        MacClipboardContent.PlainText("Hello macOS clipboard"),
        _ownership,
        result =>
        {
            if (!result.IsSuccess)
            {
                // 1511: another app took the pasteboard. Copy again to regain ownership.
                Debug.LogError($"Append failed: {result.Error?.Code}");
                return;
            }

            _ownership = result.Ownership;
        });
}
#endif
```

---

### Read

`Read` returns every item with all of its representations, plus the change count at the time of the read.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.Read(_scope, result =>
{
    if (!result.IsSuccess || result.Contents == null)
    {
        Debug.LogError($"Read failed: {result.Error?.Code}");
        return;
    }

    MacClipboardReadContents contents = result.Contents;
    Debug.Log($"items: {contents.Items.Count}, changeCount: {contents.ChangeCount}");

    foreach (MacClipboardItem item in contents.Items)
    {
        if (item.Representations.TryGetValue(MacClipboardTypes.PlainText, out byte[] bytes))
        {
            string text = System.Text.Encoding.UTF8.GetString(bytes);
            // Use the text.
        }
    }
});
#endif
```

<p align="center">
    <img src="images/mac/clipboard/Example_MacClipboardManager_Read.png" alt="Example_MacClipboardManager_Read" width="800" />
</p>

> **Note:** What comes back is not a mirror of what was written. An app that copies rich text declares extra flavors of its own, so a single item can carry `public.rtf`, `public.utf8-plain-text` and `public.utf16-external-plain-text` at once. Ask for the type you need and ignore the rest; never branch on the number of representations or on an exact match with what you wrote.

---

### Read Data

`ReadData` returns the bytes for one type. A type that is absent, and a type identifier that is not valid at all, are both a **success with `Data == null`** rather than a failure, so check `Data`, not `IsSuccess`.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.ReadData(MacClipboardTypes.PlainText, _scope, result =>
{
    if (!result.IsSuccess)
    {
        Debug.LogError($"ReadData failed: {result.Error?.Code}");
        return;
    }

    if (result.Data == null)
    {
        // The pasteboard holds no plain text. This is not an error.
        return;
    }

    string text = System.Text.Encoding.UTF8.GetString(result.Data);
});
#endif
```

<p align="center">
    <img src="images/mac/clipboard/Example_MacClipboardManager_ReadData.png" alt="Example_MacClipboardManager_ReadData" width="800" />
</p>

An empty `utType` is different: it is rejected by the native layer with `ContractViolation` (1302).

---

### Snapshot

`Snapshot` reports which types are present without reading any payload. Use it to decide whether a paste is worth doing before pulling the bytes.

`matchingTypes` does not filter the reported types; it only fills `MatchingItemIndexes` with the items that carry at least one of them.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.Snapshot(
    new[] { MacClipboardTypes.PlainText, MacClipboardTypes.Html },
    _scope,
    result =>
    {
        if (!result.IsSuccess || result.Snapshot == null)
        {
            Debug.LogError($"Snapshot failed: {result.Error?.Code}");
            return;
        }

        MacClipboardSnapshot snapshot = result.Snapshot;
        Debug.Log($"items: {snapshot.ItemTypes.Count}, " +
                  $"matching: {snapshot.MatchingItemIndexes.Count}, " +
                  $"changeCount: {snapshot.ChangeCount}");
    });
#endif
```

<p align="center">
    <img src="images/mac/clipboard/Example_MacClipboardManager_Snapshot.png" alt="Example_MacClipboardManager_Snapshot" width="800" />
</p>

Passing an empty array is not "no filter": it fails with `EmptyTypeFilter` (1512). Pass `null` for no filter.

> **Note:** Skipping the payload is an optimisation, not a privacy guarantee. Neither `Snapshot` nor the detection APIs promise that the user is never notified about clipboard access.

---

### Detect Patterns

`DetectPatterns` reports which kinds of content the pasteboard holds, without returning the values.

**These three APIs need macOS 15.4.** `DetectPatterns`, `DetectValues` and `DetectMetadata` all fail with `DetectionUnavailable` (1513) below that version.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.DetectPatterns(
    new[]
    {
        MacClipboardDetectionPattern.ProbableWebUrl,
        MacClipboardDetectionPattern.Links,
        MacClipboardDetectionPattern.EmailAddresses,
        MacClipboardDetectionPattern.PhoneNumbers,
    },
    _scope,
    result =>
    {
        if (!result.IsSuccess)
        {
            // 1513 on macOS earlier than 15.4.
            Debug.LogError($"DetectPatterns failed: {result.Error?.Code}");
            return;
        }

        foreach (MacClipboardDetectionPattern pattern in result.Patterns)
        {
            Debug.Log($"matched: {pattern}");
        }
    });
#endif
```

<p align="center">
    <img src="images/mac/clipboard/Example_MacClipboardManager_DetectPatterns.png" alt="Example_MacClipboardManager_DetectPatterns" width="800" />
</p>

The result is the subset of the requested patterns that matched. An empty collection is rejected with `EmptyDetectionPatterns` (1503).

`ProbableWebUrl`, `ProbableWebSearch` and `Number` classify the content as a whole, while the rest find items inside a longer text. A paragraph that contains a URL matches `Links` but does not match `Number`, even when it also contains digits.

---

### Detect Values

`DetectValues` returns the detected values themselves.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.DetectValues(
    new[] { MacClipboardDetectionPattern.Links, MacClipboardDetectionPattern.EmailAddresses },
    _scope,
    result =>
    {
        if (!result.IsSuccess || result.Values == null)
        {
            Debug.LogError($"DetectValues failed: {result.Error?.Code}");
            return;
        }

        MacClipboardDetectedValues values = result.Values;
        Debug.Log($"links: {values.Links.Count}, emails: {values.EmailAddresses.Count}");

        foreach (MacClipboardDetectedLink link in values.Links)
        {
            Debug.Log($"url: {link.Url}");
        }
    });
#endif
```

<p align="center">
    <img src="images/mac/clipboard/Example_MacClipboardManager_DetectValues.png" alt="Example_MacClipboardManager_DetectValues" width="800" />
</p>

Reading values can require the user's permission. When it is refused, the call fails with `DetectionDenied` (1514). No prompt appeared on the tested machines, whose access behaviour was `AlwaysAllow`, so handle 1514 defensively rather than assuming it cannot happen.

---

### Detect Metadata

`DetectMetadata` reports the content type without reading the payload.

**Plain text fails with `DetectionFailed` (1515).** The native layer cannot distinguish "there is nothing to report" from "the report could not be produced", so a plain text pasteboard, which is the common case, always takes the failure path. Treat 1515 as "no metadata available" rather than as an error worth surfacing.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.DetectMetadata(_scope, result =>
{
    if (!result.IsSuccess || result.Metadata == null)
    {
        // 1515 for plain text, 1513 below macOS 15.4.
        return;
    }

    Debug.Log($"contentType: {result.Metadata.ContentTypeIdentifier}");
});
#endif
```

---

### Access Behavior

`GetAccessBehavior` reports how the system treats this app's reads of another app's clipboard.

| Value | Meaning |
| --- | --- |
| `Default` | The system default. |
| `Ask` | The user is prompted. |
| `AlwaysAllow` | Reads proceed without a prompt. |
| `AlwaysDeny` | Reads are refused. |
| `Unavailable` | macOS is earlier than 15.4. This is a success, not a failure. |

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.GetAccessBehavior(_scope, result =>
{
    if (!result.IsSuccess)
    {
        Debug.LogError($"GetAccessBehavior failed: {result.Error?.Code}");
        return;
    }

    if (result.Behavior == MacClipboardAccessBehavior.AlwaysDeny)
    {
        // Hide the paste button rather than letting it fail.
    }
});
#endif
```

<p align="center">
    <img src="images/mac/clipboard/Example_MacClipboardManager_GetAccessBehavior.png" alt="Example_MacClipboardManager_GetAccessBehavior" width="800" />
</p>

---

### Observe Changes

`StartObserving` polls the pasteboard and raises `onChanged` (and the `ClipboardChanged` event) whenever the change count moves.

`intervalSeconds` must satisfy `0 < interval <= 60`; anything else, including `NaN`, fails with `InvalidConfiguration` (1523). The default is 0.5 seconds.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.StartObserving(
    _scope,
    intervalSeconds: 0.5,
    onChanged: change =>
    {
        Debug.Log($"clipboard changed: {change.ChangeCount}");
    },
    onStarted: result =>
    {
        if (!result.IsSuccess)
        {
            Debug.LogError($"StartObserving failed: {result.Error?.Code}");
        }
    });
#endif
```

Calling `StartObserving` again replaces the registration rather than adding a second one; the previous `onChanged` stops being called. `StopObserving` is idempotent.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.StopObserving(result =>
{
    // Safe to call even when nothing is being observed.
});
#endif
```

Two behaviours are specific to macOS and both are confirmed on hardware:

- **Polling stops while the app is not frontmost and catches up when it returns.** A change made in another app is reported when the user comes back, not at the moment it happens.
- **The catch-up is not collapsed.** Three changes made while the app was in the background arrive as three events in change count order, so a clipboard history feature does not lose entries.

> **Note:** A failed restart leaves the previous observation running. `StartObserving` validates the interval and resolves the scope before touching the existing observation, so a call that fails with 1523 has not stopped anything.

---

### Check Foreground Change

`CheckForegroundChange` answers "has the pasteboard changed since I last asked" without running a poll. The first call after a change returns `true`, and the next returns `false`.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.CheckForegroundChange(_scope, result =>
{
    if (result.IsSuccess && result.Changed)
    {
        // Refresh the paste button.
    }
});
#endif
```

<p align="center">
    <img src="images/mac/clipboard/Example_MacClipboardManager_CheckForegroundChange.png" alt="Example_MacClipboardManager_CheckForegroundChange" width="800" />
</p>

> **Do not combine this with observation.** The two share the same baseline change count. While `StartObserving` is running its polling updates that baseline first, so `CheckForegroundChange` returns `false` almost every time. Pick one: observation for a screen that stays open, `CheckForegroundChange` for a check on demand.

---

### Clear

`Clear` empties the scope and returns the new change count.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.Clear(_scope, result =>
{
    if (result.IsSuccess)
    {
        Debug.Log($"cleared, changeCount: {result.ChangeCount}");
    }
});
#endif
```

---

### Size Limits

Both directions are capped at 32 MiB.

| Limit | Constant | Failure |
| --- | --- | --- |
| Write | `MacClipboardLimits.MaxRequestBytes` | `RequestTooLarge` (9007), raised in C# before the native call |
| Read, per representation | `MacClipboardLimits.MaxResponseBytesPerRepresentation` | `ResponseParseFailed` (9006) |

The write limit is the sum of every representation of every item. The read limit applies to each representation on its own, so many small representations can total more than 32 MiB and still be read.

**The read limit applies to content another app put there, which your app does not control.** A pasteboard holding a very large image or text makes `Read` fail with 9006, which is the same code a genuinely malformed response produces. Treat 9006 on `Read` as "this pasteboard is not usable" rather than as a bug, and fall back to `Snapshot`, which does not read payloads.

#### Large single items are written lazily

A **single** item larger than 10 MiB takes a different path in the native layer: the pasteboard is given the types, and the bytes are supplied only when a reader asks for them.

- **A successful `Copy` therefore does not mean the paste will work.** If the process exits before anything reads the data, the bytes are gone.
- **Once anything has pasted it, the bytes are materialised and survive the process.**
- The trigger is "one item **and** more than 10 MiB". Several items take the normal path regardless of total size.

If a large payload must survive the app closing, split it across two items.

---

### App Sandbox

The clipboard needs no entitlement beyond `com.apple.security.app-sandbox` itself. With App Sandbox enabled, copying, reading, and creating and removing named and unique pasteboards all work, so a Mac App Store build loses no operation.

One unrelated trap is worth knowing: **a sandboxed player cannot write outside its container.** Passing `-logFile /tmp/player.log` makes the player exit at startup with `Unable to open log file, exiting.` Use a path under `~/Library/Containers/<bundle id>/Data/` instead. The same applies to save files and any other output your game writes by absolute path.

---

### Receive Events

Every operation raises an event in addition to its callback. Events are useful for logging and shared UI; they cannot be matched to a specific call.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
private void OnEnable()
{
    MacClipboardManager.Instance.OwnershipChanged += OnOwnershipChanged;
    MacClipboardManager.Instance.ReadCompleted += OnReadCompleted;
    MacClipboardManager.Instance.ClipboardChanged += OnClipboardChanged;
}

private void OnDisable()
{
    MacClipboardManager.Instance.OwnershipChanged -= OnOwnershipChanged;
    MacClipboardManager.Instance.ReadCompleted -= OnReadCompleted;
    MacClipboardManager.Instance.ClipboardChanged -= OnClipboardChanged;
}

private void OnOwnershipChanged(MacClipboardOwnershipResult result)
{
    Debug.Log($"{result.Operation}: {result.IsSuccess}");
}

private void OnReadCompleted(MacClipboardReadResult result) { }

private void OnClipboardChanged(MacClipboardChangeEvent change)
{
    Debug.Log($"changeCount: {change.ChangeCount}");
}
#endif
```

Unsubscribe in `OnDisable`. The manager outlives individual scenes, so a subscription left behind keeps a destroyed object alive.

---

### Error Handling

Every failure comes back as a result, not an exception. The only exception is `MacPasteboardScope.Named("")` and its siblings, which throw `ArgumentException` for a blank name.

| Code | Constant | When |
| --- | --- | --- |
| 1301 | `ParseFailed` | The native layer could not parse the request. |
| 1302 | `ContractViolation` | A required native argument was empty, such as an empty `utType`. |
| 1501 | `EmptyContent` | The content had no items. |
| 1502 | `EmptyRepresentations` | An item had no representations. |
| 1503 | `EmptyDetectionPatterns` | An empty pattern collection was passed. |
| 1504 | `InvalidTypeIdentifier` | The uniform type identifier was rejected. |
| 1505 | `InvalidPasteboardName` | The pasteboard name was rejected. |
| 1506 | `ContentTooLarge` | The native layer refused the payload size. |
| 1507 | `PasteboardUnavailable` | The pasteboard could not be read. |
| 1508 | `CannotReleaseStandardPasteboard` | `general`, `font`, `ruler`, `find` or `drag` was passed to `RemovePasteboard`. |
| 1509 | `WriteRejected` | The write was refused. |
| 1510 | `AppendRejected` | The append was refused. |
| 1511 | `OwnershipLost` | Another app took the pasteboard before the append. |
| 1512 | `EmptyTypeFilter` | An empty array was passed to `Snapshot`. |
| 1513 | `DetectionUnavailable` | macOS is earlier than 15.4. |
| 1514 | `DetectionDenied` | The user refused the read. |
| 1515 | `DetectionFailed` | Detection produced nothing, including plain text metadata. |
| 1523 | `InvalidConfiguration` | The observation interval was outside `0 < interval <= 60`. |
| 1599 | `Unknown` | Unclassified native failure. |
| 9001 | `Busy` | The same operation is already running. |
| 9002 | `BridgeUnavailable` | Called in the Editor or on another platform. |
| 9003 | `MainThreadRequired` | Called off the Unity main thread. |
| 9004 | `ManagerDestroyed` | The manager was destroyed. |
| 9005 | `InvalidRequest` | A required argument was null. |
| 9006 | `ResponseParseFailed` | The response could not be parsed, including a representation over 32 MiB. |
| 9007 | `RequestTooLarge` | The payload exceeded 32 MiB. |

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacClipboardManager.Instance.Read(_scope, result =>
{
    if (result.IsSuccess)
    {
        return;
    }

    switch (result.Error?.Code)
    {
        case MacClipboardErrorCodes.ResponseParseFailed:
            // Includes a pasteboard too large to read. Fall back to Snapshot.
            break;
        case MacClipboardErrorCodes.Busy:
            // A Read is already running; ignore this one.
            break;
        default:
            Debug.LogError($"Read failed: {result.Error?.Code}");
            break;
    }
});
#endif
```

> **Note:** `Error.Message` is built by the native layer and can contain a pasteboard name. Log the `Code` and your own wording rather than the raw message when the text could reach a user-visible surface.
