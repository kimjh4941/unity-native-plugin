# Share Feature

Language:

- English (this page)
- 日本語: [share.ja.md](share.ja.md)
- 한국어: [share.ko.md](share.ko.md)

← [Back to manual top](index.md)

---

## Table of Contents

- [Android](#android)
  - [Changes in 1.13.0](#changes-in-1130)
  - [Setup](#setup)
  - [Share Text](#share-text)
  - [Share URL](#share-url)
  - [Share with Custom Chooser Actions (API 34+)](#share-with-custom-chooser-actions-api-34)
  - [Share with Subject and Title](#share-with-subject-and-title)
  - [Share with Rich Preview](#share-with-rich-preview)
  - [Share Image](#share-image)
  - [Share Multiple Images](#share-multiple-images)
  - [Share File](#share-file)
  - [Share Multiple Files](#share-multiple-files)
  - [Direct Share Target](#direct-share-target)
  - [Share with Callback](#share-with-callback)
  - [Cancel Pending Callback](#cancel-pending-callback)
  - [Receive Events](#receive-events)
  - [Error Handling](#error-handling)
- [iOS](#ios)
  - [Setup](#setup-1)
  - [Share Text](#share-text-1)
  - [Share URL](#share-url-1)
  - [Share URL with Preview](#share-url-with-preview)
  - [Share Image](#share-image-1)
  - [Share Multiple Images](#share-multiple-images-1)
  - [Share File](#share-file-1)
  - [Share Multiple Files](#share-multiple-files-1)
  - [Share Text and URL](#share-text-and-url)
  - [Share with Subject](#share-with-subject)
  - [Share Excluding Activities](#share-excluding-activities)
  - [Receive results](#receive-results)
  - [Error Handling](#error-handling-1)
- [macOS](#macos)
  - [Setup](#setup-2)
  - [Share Text](#share-text-2)
  - [Share URL](#share-url-2)
  - [Share Image](#share-image-2)
  - [Share File](#share-file-2)
  - [Share Multiple Images](#share-multiple-images-2)
  - [Share Multiple Files](#share-multiple-files-2)
  - [Share Text and URL](#share-text-and-url-1)
  - [Share Excluding Services](#share-excluding-services)
  - [Share via a Named Service (Mail)](#share-via-a-named-service-mail)
  - [Receive results](#receive-results-1)
  - [Error Handling](#error-handling-2)

---

## Android

### Changes in 1.13.0

Since 1.13.0 Android Share runs on native-toolkit's C ABI 2.0.0 instead of the 1.x Kotlin bridge. The methods and events of `AndroidShareManager`, the payload fields and the result properties are unchanged, and a share with valid input opens the same Sharesheet. The FileProvider and the Direct Share manifest setup described below are also unchanged. What you need to change, and what differs from 1.12.0:

#### Updating your code

- Replace `using JonghyunKim.NativeToolkit.Runtime.Share;` with `using JonghyunKim.NativeToolkit.Runtime.Android.Share;` in your Android code. Keep the old line in iOS and macOS code: their types did not move.
- Rename the types listed below in your code. Unity does not do this for you: the API Updater does not rewrite references to this package's types, so a script that still uses the 1.12.0 names fails to compile. Code inside `#if UNITY_ANDROID && !UNITY_EDITOR` (as in the examples on this page) is not compiled in the Editor, so the errors appear only when you build the Android player.
- Data you saved with `[SerializeReference]` under a 1.12.0 name loads as the new type with its values: each renamed type records its 1.12.0 name with `[MovedFrom]`.

Every Android Share type moved from `JonghyunKim.NativeToolkit.Runtime.Share` to `JonghyunKim.NativeToolkit.Runtime.Android.Share`. `AndroidShareManager` keeps its name; the other types were renamed:

| 1.12.0 | 1.13.0 |
| --- | --- |
| `ShareOperationResult` | `AndroidShareOperationResult` |
| `ShareCallbackResult` | `AndroidShareCallbackResult` |
| `ShareChooserActionResult` | `AndroidShareChooserActionResult` |
| `ShareChooserActionCallbackCoordinator` | `AndroidShareChooserActionCallbackCoordinator` |
| `ChooserActionPayload` | `AndroidChooserActionPayload` |
| `ShareTextPayload` | `AndroidShareTextPayload` |
| `ShareImagePayload` | `AndroidShareImagePayload` |
| `ShareImagesPayload` | `AndroidShareImagesPayload` |
| `ShareFilePayload` | `AndroidShareFilePayload` |
| `ShareFilesPayload` | `AndroidShareFilesPayload` |
| `DirectShareTargetPayload` | `AndroidDirectShareTargetPayload` |
| `RemoveDirectShareTargetsPayload` | `AndroidRemoveDirectShareTargetsPayload` |

#### Breaking changes shared by every Android feature

- **The 1.x native libraries are no longer bundled.** `android-native-toolkit-1.3.0.aar` and `unity-android-native-toolkit-1.3.0.aar` were removed. The package ships `android-native-toolkit-2.0.0.aar` and `android-native-toolkit-capi-2.0.0.aar` (native-toolkit's C ABI 2.0.0). Code that called their classes (`android.unity.*`, `android.library.*`) directly no longer works.
- **"Configure Gradle Project" is gone.** The menu Tools > Native Toolkit > Android > Configure Gradle Project and its window were removed. The build now adds the runtime dependencies to `unityLibrary/build.gradle` itself (7 libraries, including `kotlin-stdlib` 2.2.21 and `kotlinx-coroutines-core` 1.9.0) and no longer applies the Kotlin Gradle plugin. If you use your own Gradle templates or EDM4U, check them for conflicting versions.
- **64-bit and IL2CPP only.** A build that also targets ARMv7 (32-bit) stops with a message, and Mono is not supported. See [Android requirements](index.md#android-requirements).

#### Other changes in Share

- **`AndroidShareJsonBuilder` was removed.** In 1.12.0 `AndroidShareManager` used it to turn the payloads into JSON for the Kotlin bridge. The manager now passes the payloads to the native library itself, so nothing uses that JSON any more. Pass the payload objects (`AndroidShareTextPayload` and the others) to the `AndroidShareManager` methods, as the examples on this page do. There is no replacement for the JSON strings.
- **Known differences.** The cases below behave differently from 1.12.0. Most of them involve invalid input, other threads, or calls made close together:

| Case | 1.12.0 | 1.13.0 |
| --- | --- | --- |
| Calling a method from a thread other than Unity's main thread | Refused: two error lines in the log and a failure with `<op> could not be started.` | Accepted. The results arrive on Unity's main thread. Get `AndroidShareManager.Instance` on Unity's main thread first: the first access creates a GameObject |
| Sharing while the app is in the background | Not measured | Fails with `Failed to <op>: the app is not in the foreground.` |
| `RegisterDirectShareTarget` and `RemoveDirectShareTargets` | Ran asynchronously on Android's main thread | Run synchronously on the calling thread, so the call takes longer by the icon decoding and the system call. The result still arrives later through the event |
| Order of results from different calls | The order of the calls | A result decided at the call (a failed check, Direct Share, the success of `CancelPendingShareCallback`) can arrive before the result of an earlier share. For example, `CancelPendingShareCallback` called in the same frame as `ShareWithCallback` can report its success before `onStarted` |
| The per-call callback of a result decided at the call | The completion of an earlier call of the same operation could take a later call's callback | The result goes to the callback passed with its own call |
| `CancelPendingShareCallback` while the user is picking an app | Success was reported after the wait was removed | Success is reported when the cancellation is queued, before the wait is removed. An app selected at the same moment can still be reported after it |
| A call made inside a share event handler or callback whose result is decided in C# (every call in the Editor, or a failed check on the device) | The result arrived in the same frame | The result arrives in a later frame |
| Several threads calling at once | Handled in order on Android's main thread | The per-call callbacks, the receiver of chooser action taps and the cancellation may not follow the order of the calls |
| A chooser action icon that cannot be read as an image | The action was skipped | The action is skipped if its first bytes do not match an image format. If they match but the image is broken, the whole share fails with `Failed to shareText: a chooser action icon could not be read as an image.`, also on API 33 and lower |
| A chooser action icon in a format other than PNG, JPEG, WebP, GIF, BMP, ICO, HEIF or AVIF (for example WBMP) | Shown | The action is skipped |
| Chooser actions with the same `intentAction` | All shown; each tap reported the same ID | Only the first one is shown |
| Chooser actions of the previous `ShareText` | Stopped when the next `ShareText` reached the native side, or when the manager was destroyed | Stop when the next `ShareText` succeeds. If the next `ShareText` fails, or after the manager is created again, the previous actions can still be tapped and are reported to the current `onChooserAction` |
| An app selected in an older Sharesheet | Reported (a bug) | Not reported |
| Results that arrive around the destruction of `AndroidShareManager` | Results queued before the destruction went to the destroyed manager; later ones were dropped | A result goes to the manager alive when it is dispatched (the new one if the manager was created again), or is dropped. A chooser action tap still goes once to the subscribers and the callback of the manager that was alive when it was tapped |
| The error message for several files (`ShareImages`, `ShareFiles`) | The path that caused the error | `File not found:` names the first missing path; `File cannot be shared:` lists all the paths |
| A Direct Share target the system does not accept | `Failed to register Direct Share target: <reason>` | `Failed to register Direct Share target: the system did not accept the shortcut.` |
| A security restriction or another exception on the native side | A message with the exception's text, such as `Security restriction while executing <op>: ...` | A fixed message, such as `Failed to <op>: UNKNOWN.` |
| A null payload, file list, ID list or chooser action | `NullReferenceException` | `ArgumentNullException`, and nothing is registered |
| A null element in `filePaths` or `ids` | Treated as the path `null` (`File not found: null`) | Fails with `Failed to <op>: filePaths must not contain null` (or `ids must not contain null`) |
| A null required string (`text`, `filePath`, `id` and so on), or a null `label`, `iconBase64` or `intentAction` in a chooser action | Not measured | Fails like an empty value (for example `Failed to <op>: text is required`); a chooser action with a null value is dropped |
| Base64 icons (`iconBase64`) | Decoded with Android's `Base64.DEFAULT` | Must be standard Base64 with padding; white space around it is ignored. The output of `Convert.ToBase64String` works |
| U+0000 in a string | Passed as is | The string is cut there (a `text` that starts with U+0000 fails with `text is required`) |
| Unpaired surrogates in a string | Passed as is | Replaced with U+FFFD |
| The Editor with a build target other than Android | `AndroidShareManager` did not exist | It exists; every call fails with `<op> could not be started.` |
| Logs | The whole JSON, including the text and the Base64 icons | The text and `subject` are logged by length only |
| `Initialize()` | Got the bridge and registered the listeners | Registers the listeners only; calling it again is harmless |

### Setup

#### Import the namespace

```csharp
// Guard: Android (Player) only. Prevents native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Android.Share;
#endif
```

#### AndroidManifest.xml (required for Direct Share)

To allow your app to appear as a Direct Share target in the Android share sheet, add the following to your Android Library Project manifest (e.g., `Assets/Plugins/Android/<your-app>.androidlib/AndroidManifest.xml`). This is only required if you use `RegisterDirectShareTarget`.

```xml
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
    package="com.example.your.androidlib">
    <application>
        <activity android:name="com.unity3d.player.UnityPlayerGameActivity" android:exported="true">
            <intent-filter>
                <action android:name="android.intent.action.SEND" />
                <category android:name="android.intent.category.DEFAULT" />
                <data android:mimeType="*/*" />
            </intent-filter>
            <meta-data
                android:name="android.app.shortcuts"
                android:resource="@xml/shortcuts" />
        </activity>
    </application>
</manifest>
```

Add `res/xml/shortcuts.xml` to the same Android Library Project:

```xml
<?xml version="1.0" encoding="utf-8"?>
<shortcuts xmlns:android="http://schemas.android.com/apk/res/android">
    <share-target android:targetClass="com.unity3d.player.UnityPlayerGameActivity">
        <data android:mimeType="*/*" />
        <category android:name="android.shortcut.conversation" />
    </share-target>
</shortcuts>
```

#### File paths for sharing images and files

Files passed to `ShareImage`, `ShareImages`, `ShareFile`, and `ShareFiles` must be located in a directory covered by the native FileProvider. Use `Application.persistentDataPath` (maps to the external files directory, which is declared as `<external-files-path>` in the native FileProvider config). Do not use `Application.temporaryCachePath` (external cache directory), as it is not covered.

```csharp
string path = Path.Combine(Application.persistentDataPath, "my_image.png");
```

---

### Share Text

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "Hello from Unity! This is a plain text share sample."
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareText.png" alt="Example_AndroidShareManager_ShareText" width="400" />
</p>

---

### Share URL

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "https://unity.com",
    mimeType = "text/plain"
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareUrl.png" alt="Example_AndroidShareManager_ShareUrl" width="400" />
</p>

---

### Share with Custom Chooser Actions (API 34+)

Custom chooser actions appear as additional buttons in the Android share chooser dialog. Requires Android 14 (API 34) or later. On lower API levels, the share proceeds without custom actions.

Each action needs a `label`, an `iconBase64` (standard Base64 of an image such as PNG or JPEG) and an `intentAction`; an action that lacks one of them is not shown. Give each action a unique `intentAction` other than `android.intent.action.SEND`: only the first of several actions with the same value is shown, and a tap on a `SEND` action is not reported. Tap events are delivered via `ShareChooserActionTapped` (global event) or the `onChooserAction` parameter of `ShareText` (per-call callback).

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// Subscribe to the global event to receive chooser action taps.
AndroidShareManager.Instance.ShareChooserActionTapped += result =>
{
    Debug.Log($"Chooser action tapped: {result.ActionId}");
};

AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "Sharing with custom chooser actions (Android 14 / API 34+ only).",
    chooserActions = new[]
    {
        new AndroidChooserActionPayload
        {
            label = "Save",
            iconBase64 = iconBase64, // base64-encoded PNG/JPEG
            intentAction = "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_SAVE"
        },
        new AndroidChooserActionPayload
        {
            label = "Open",
            iconBase64 = iconBase64,
            intentAction = "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_OPEN"
        }
    }
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareCustomAction.png" alt="Example_AndroidShareManager_ShareCustomAction" width="400" />
</p>

> **Note:** Android shows at most 5 custom chooser actions. Passing more than 5 logs a warning, and the extra actions may not be shown. `ShareWithCallback` does not support chooser actions; use `ShareText` instead.

---

### Share with Subject and Title

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "This is a share sample with subject and title from Unity.",
    title = "Unity Share Sample",
    subject = "Sample Subject",
    mimeType = "text/plain"
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareWithSubjectTitle.png" alt="Example_AndroidShareManager_ShareWithSubjectTitle" width="400" />
</p>

---

### Share with Rich Preview

Displays a preview title and thumbnail image in the chooser. The thumbnail path must point to a file in a FileProvider-accessible directory; otherwise the chooser opens without the thumbnail.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string thumbnailPath = Path.Combine(Application.persistentDataPath, "share_preview_thumbnail.png");
// Write a PNG file to thumbnailPath before calling ShareText.

AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "Check out this rich preview share from Unity!",
    previewTitle = "Unity Rich Preview Sample",
    previewThumbnailPath = thumbnailPath
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareRichPreview.png" alt="Example_AndroidShareManager_ShareRichPreview" width="400" />
</p>

---

### Share Image

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string imagePath = Path.Combine(Application.persistentDataPath, "share_sample_image.png");
// Write a PNG file to imagePath before calling ShareImage.

AndroidShareManager.Instance.ShareImage(new AndroidShareImagePayload
{
    filePath = imagePath,
    mimeType = "image/png"
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareImage.png" alt="Example_AndroidShareManager_ShareImage" width="400" />
</p>

---

### Share Multiple Images

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string imagePath1 = Path.Combine(Application.persistentDataPath, "share_sample_image_1.png");
string imagePath2 = Path.Combine(Application.persistentDataPath, "share_sample_image_2.png");
// Write PNG files to the paths before calling ShareImages.

AndroidShareManager.Instance.ShareImages(new AndroidShareImagesPayload
{
    filePaths = new[] { imagePath1, imagePath2 }
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareImages.png" alt="Example_AndroidShareManager_ShareImages" width="400" />
</p>

---

### Share File

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string filePath = Path.Combine(Application.persistentDataPath, "share_sample_file.txt");
File.WriteAllText(filePath, "This is a sample text file shared from Unity Native Toolkit.");

AndroidShareManager.Instance.ShareFile(new AndroidShareFilePayload
{
    filePath = filePath
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareFile.png" alt="Example_AndroidShareManager_ShareFile" width="400" />
</p>

---

### Share Multiple Files

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string filePath1 = Path.Combine(Application.persistentDataPath, "share_sample_file_1.txt");
string filePath2 = Path.Combine(Application.persistentDataPath, "share_sample_file_2.txt");
File.WriteAllText(filePath1, "Sample file 1 from Unity Native Toolkit.");
File.WriteAllText(filePath2, "Sample file 2 from Unity Native Toolkit.");

AndroidShareManager.Instance.ShareFiles(new AndroidShareFilesPayload
{
    filePaths = new[] { filePath1, filePath2 }
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareFiles.png" alt="Example_AndroidShareManager_ShareFiles" width="400" />
</p>

---

### Direct Share Target

Register a shortcut that appears in the Direct Share row of the Android share sheet. Requires the manifest and `shortcuts.xml` setup described in [Setup](#setup). The shortcut may not appear immediately after registration due to OS-level caching.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.RegisterDirectShareTarget(new AndroidDirectShareTargetPayload
{
    id = "native_toolkit_sample_target",
    label = "Unity Sample Target",
    iconBase64 = iconBase64 // base64-encoded PNG/JPEG, keep small (64x64 recommended)
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_RegisterDirectShareTarget.png" alt="Example_AndroidShareManager_RegisterDirectShareTarget" width="400" />
</p>

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.RemoveDirectShareTargets(new AndroidRemoveDirectShareTargetsPayload
{
    ids = new[] { "native_toolkit_sample_target" }
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_RemoveDirectShareTargets.png" alt="Example_AndroidShareManager_RemoveDirectShareTargets" width="400" />
</p>

> **Note:** The icon bitmap is passed via Android Binder. Keep it small (64×64 pixels recommended) to avoid exceeding the Binder transaction size limit.

---

### Share with Callback

Shares text and receives a callback when the user selects an app. `onStarted` fires when the chooser launches (success or failure). `onSelected` fires when the user picks an app (not fired if the user cancels, copies, or edits).

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareWithCallback(
    new AndroidShareTextPayload
    {
        text = "Share with callback sample from Unity. Select an app to receive the selection result."
    },
    onStarted: result =>
    {
        string status = result.IsSuccess ? "Success" : "Failed";
        Debug.Log($"[onStarted] ShareWithCallback: {status}");
    },
    onSelected: result =>
    {
        string pkg = result.SelectedPackageName ?? "(unknown)";
        Debug.Log($"[onSelected] Selected: {pkg}");
    });
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareWithCallback.png" alt="Example_AndroidShareManager_ShareWithCallback" width="400" />
</p>

> **Note:** `chooserActions` are not supported for `ShareWithCallback`. Use `ShareText` with `chooserActions` instead.

---

### Cancel Pending Callback

Cancels the wait for the app selection started by `ShareWithCallback`. Call this if you no longer want to receive the `onSelected` callback — for example, after the share sheet is dismissed without a selection. The manager does not call it for you: call it in `OnDisable`, as in [Receive Events](#receive-events), to prevent stale callbacks after screen transitions.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.CancelPendingShareCallback();
#endif
```

---

### Receive Events

Subscribe to events on `OnEnable` and unsubscribe on `OnDisable` to avoid stale references after screen transitions. Every result arrives on Unity's main thread, first through the event and then through the per-call callback.

```csharp
private void OnEnable()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidShareManager.Instance.ShareOperationCompleted += OnShareOperationCompleted;
    AndroidShareManager.Instance.ShareCallbackReceived += OnShareCallbackReceived;
    AndroidShareManager.Instance.ShareChooserActionTapped += OnShareChooserActionTapped;
#endif
}

private void OnDisable()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidShareManager.Instance.ShareOperationCompleted -= OnShareOperationCompleted;
    AndroidShareManager.Instance.ShareCallbackReceived -= OnShareCallbackReceived;
    AndroidShareManager.Instance.ShareChooserActionTapped -= OnShareChooserActionTapped;
    AndroidShareManager.Instance.CancelPendingShareCallback();
#endif
}

#if UNITY_ANDROID && !UNITY_EDITOR
private void OnShareOperationCompleted(AndroidShareOperationResult result)
{
    string status = result.IsSuccess ? "Success" : "Failed";
    Debug.Log($"[event] {result.Operation}: {status}");
    if (!string.IsNullOrEmpty(result.ErrorMessage))
        Debug.LogError(result.ErrorMessage);
}

private void OnShareCallbackReceived(AndroidShareCallbackResult result)
{
    string pkg = result.SelectedPackageName ?? "(unknown)";
    Debug.Log($"[event] ShareCallback: Selected {pkg}");
}

private void OnShareChooserActionTapped(AndroidShareChooserActionResult result)
{
    Debug.Log($"[event] ChooserAction tapped: {result.ActionId}");
}
#endif
```

---

### Error Handling

All share operations report success or failure via `ShareOperationCompleted` (and the per-call callback). The `ErrorMessage` field contains a description when `IsSuccess` is `false`. A null payload, file list, ID list or chooser action is not reported this way: the method throws `ArgumentNullException`.

Files passed to share APIs must exist and be located in a FileProvider-accessible directory. A path that does not exist fails with `File not found: <path>`, and a file outside those directories fails with `File cannot be shared: <path>. Ensure the file is in a supported directory.`

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// This will trigger an error via ShareOperationCompleted (IsSuccess = false)
// ErrorMessage: "File not found: /invalid/path/that/does/not/exist/sample.txt"
AndroidShareManager.Instance.ShareFile(new AndroidShareFilePayload
{
    filePath = "/invalid/path/that/does/not/exist/sample.txt"
});
#endif
```

---

## iOS

### Setup

#### Import the namespace

`IosShareManager` compiles whenever the iOS build target is selected, including in the Editor. Calling `Share` in the Editor or on a non-iOS device does not crash; it returns an immediate failure result instead (see [Error Handling](#error-handling-1)).

```csharp
#if UNITY_IOS || UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Share;
#endif
```

#### File paths for sharing images and files

Unlike Android, iOS has no FileProvider restriction. Any file under `Application.persistentDataPath` can be shared directly.

```csharp
string path = Path.Combine(Application.persistentDataPath, "my_image.png");
```

---

### Share Text

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Text("Shared from Unity Native Toolkit") }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareText.png" alt="Example_IosShareManager_ShareText" width="400" />
</p>

---

### Share URL

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Url("https://unity.com") }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareUrl.png" alt="Example_IosShareManager_ShareUrl" width="400" />
</p>

---

### Share URL with Preview

`previewTitle` sets the title shown in the share sheet's content preview area.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Url("https://unity.com") },
    previewTitle = "Unity"
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareUrlPreview.png" alt="Example_IosShareManager_ShareUrlPreview" width="400" />
</p>

---

### Share Image

```csharp
#if UNITY_IOS || UNITY_EDITOR
string imagePath = Path.Combine(Application.persistentDataPath, "share_sample_image.png");
// Write a PNG file to imagePath before calling Share.

IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Image(imagePath) }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareImage.png" alt="Example_IosShareManager_ShareImage" width="400" />
</p>

---

### Share Multiple Images

```csharp
#if UNITY_IOS || UNITY_EDITOR
string imagePath1 = Path.Combine(Application.persistentDataPath, "share_sample_image_1.png");
string imagePath2 = Path.Combine(Application.persistentDataPath, "share_sample_image_2.png");
// Write PNG files to the paths before calling Share.

IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Image(imagePath1), IosShareItem.Image(imagePath2) }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareImages.png" alt="Example_IosShareManager_ShareImages" width="400" />
</p>

---

### Share File

```csharp
#if UNITY_IOS || UNITY_EDITOR
string filePath = Path.Combine(Application.persistentDataPath, "share_sample_file.txt");
File.WriteAllText(filePath, "This is a sample text file shared from Unity Native Toolkit.");

IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.File(filePath) }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareFile.png" alt="Example_IosShareManager_ShareFile" width="400" />
</p>

---

### Share Multiple Files

```csharp
#if UNITY_IOS || UNITY_EDITOR
string filePath1 = Path.Combine(Application.persistentDataPath, "share_sample_file_1.txt");
string filePath2 = Path.Combine(Application.persistentDataPath, "share_sample_file_2.txt");
File.WriteAllText(filePath1, "Sample file 1 from Unity Native Toolkit.");
File.WriteAllText(filePath2, "Sample file 2 from Unity Native Toolkit.");

IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.File(filePath1), IosShareItem.File(filePath2) }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareFiles.png" alt="Example_IosShareManager_ShareFiles" width="400" />
</p>

---

### Share Text and URL

Multiple items of different types can be shared together in a single `Share` call.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Text("Check this out"), IosShareItem.Url("https://unity.com") }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareMultiple.png" alt="Example_IosShareManager_ShareMultiple" width="400" />
</p>

---

### Share with Subject

`subject` is used by Mail and similar activities as the message subject line.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Text("Body text") },
    subject = "Sample Subject"
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareWithSubject.png" alt="Example_IosShareManager_ShareWithSubject" width="400" />
</p>

---

### Share Excluding Activities

`excludedActivityTypes` hides the specified activity types (raw identifiers) from the share sheet.

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Url("https://unity.com") },
    excludedActivityTypes = new[]
    {
        "com.apple.UIKit.activity.CopyToPasteboard",
        "com.apple.UIKit.activity.PostToFacebook"
    }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareExcludingActivities.png" alt="Example_IosShareManager_ShareExcludingActivities" width="400" />
</p>

> **Note:** Whether a given activity type actually appears in the share sheet also depends on the device and installed apps. Verify with `com.apple.UIKit.activity.CopyToPasteboard` (Copy) as the primary check, since third-party activities such as Facebook may or may not be installed on the test device.

---

### Receive results

Subscribe to `ShareCompleted` on `OnEnable` and unsubscribe on `OnDisable` to avoid stale references after screen transitions. `ShareCompleted` always fires before the optional per-call callback passed to `Share`.

```csharp
private void OnEnable()
{
#if UNITY_IOS || UNITY_EDITOR
    IosShareManager.Instance.ShareCompleted += OnShareCompleted;
#endif
}

private void OnDisable()
{
#if UNITY_IOS || UNITY_EDITOR
    IosShareManager.Instance.ShareCompleted -= OnShareCompleted;
#endif
}

#if UNITY_IOS || UNITY_EDITOR
private void OnShareCompleted(IosShareResult result)
{
    if (!result.IsSuccess)
    {
        Debug.LogError($"Share failed: {result.ErrorMessage}");
        return;
    }

    if (result.Completed)
    {
        Debug.Log($"Share completed: activityType={result.ActivityType}");
    }
    else
    {
        Debug.Log("Share cancelled by the user.");
    }
}
#endif
```

> **Note:** User cancellation is not an error: `IsSuccess` is `true` and `Completed` is `false`, with `ActivityType` set to `null`.

---

### Error Handling

All `Share` calls report their outcome via `ShareCompleted` (and the optional per-call callback passed to `Share`). The `ErrorMessage` field is guaranteed to be non-null whenever `IsSuccess` is `false`.

```csharp
#if UNITY_IOS || UNITY_EDITOR
// No items: fails immediately without presenting the share sheet.
// ErrorMessage: "No shareable items were provided."
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = Array.Empty<IosShareItem>()
});

// Invalid URL string: fails with a native validation error.
// ErrorMessage: "Invalid URL: not a valid url."
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Url("not a valid url") }
});

// Missing file: fails when the native layer cannot find the file.
// ErrorMessage: "File not found at path: /nonexistent/share-missing.txt."
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.File("/nonexistent/share-missing.txt") }
});

// Missing image: fails when the native layer cannot load the image.
// ErrorMessage: "Failed to load image at path: /nonexistent/share-missing.png."
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Image("/nonexistent/share-missing.png") }
});
#endif
```

---

## macOS

macOS sharing is provided through `MacShareManager`. It exposes two entry points: `Share` presents the system sharing service picker (`NSSharingServicePicker`), and `ShareViaService` performs a single named service directly without showing the picker.

> **Note:** `Share` (the picker) must be invoked from a user-initiated action, such as a button click, since `NSSharingServicePicker.show(...)` requires a `mouseDown` event context. `ShareViaService` does not have this requirement and is the more reliable path when you need a deterministic result (for example, always sharing via Mail).

### Setup

#### Import the namespace

`MacShareManager` compiles whenever the macOS Standalone build target is selected, including in the Editor. Calling `Share` or `ShareViaService` in the Editor or on a non-macOS player does not crash; it returns an immediate failure result instead (see [Error Handling](#error-handling-2)).

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Share;
#endif
```

#### File paths for sharing images and files

macOS has no FileProvider-style restriction. Any file under `Application.persistentDataPath` can be shared directly.

```csharp
string path = Path.Combine(Application.persistentDataPath, "my_image.png");
```

---

### Share Text

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Text("Shared from Unity Native Toolkit") }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareText.png" alt="Example_MacShareManager_ShareText" width="800" />
</p>

---

### Share URL

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Url("https://unity.com") }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareUrl.png" alt="Example_MacShareManager_ShareUrl" width="800" />
</p>

---

### Share Image

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
string imagePath = Path.Combine(Application.persistentDataPath, "share_sample_image.png");
// Write a PNG file to imagePath before calling Share.

MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Image(imagePath) }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareImage.png" alt="Example_MacShareManager_ShareImage" width="800" />
</p>

---

### Share File

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
string filePath = Path.Combine(Application.persistentDataPath, "share_sample_file.txt");
File.WriteAllText(filePath, "This is a sample text file shared from Unity Native Toolkit.");

MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.File(filePath) }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareFile.png" alt="Example_MacShareManager_ShareFile" width="800" />
</p>

---

### Share Multiple Images

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
string imagePath1 = Path.Combine(Application.persistentDataPath, "share_sample_image_1.png");
string imagePath2 = Path.Combine(Application.persistentDataPath, "share_sample_image_2.png");
// Write PNG files to the paths before calling Share.

MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Image(imagePath1), MacShareItem.Image(imagePath2) }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareImages.png" alt="Example_MacShareManager_ShareImages" width="800" />
</p>

---

### Share Multiple Files

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
string filePath1 = Path.Combine(Application.persistentDataPath, "share_sample_file_1.txt");
string filePath2 = Path.Combine(Application.persistentDataPath, "share_sample_file_2.txt");
File.WriteAllText(filePath1, "Sample file 1 from Unity Native Toolkit.");
File.WriteAllText(filePath2, "Sample file 2 from Unity Native Toolkit.");

MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.File(filePath1), MacShareItem.File(filePath2) }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareFiles.png" alt="Example_MacShareManager_ShareFiles" width="800" />
</p>

---

### Share Text and URL

Multiple items of different types can be shared together in a single `Share` call.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Text("Check this out"), MacShareItem.Url("https://unity.com") }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareTextAndUrl.png" alt="Example_MacShareManager_ShareTextAndUrl" width="800" />
</p>

---

### Share Excluding Services

`excludedServiceTitles` hides services whose display title matches one of the given strings (best-effort match against `NSSharingService.title`).

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Url("https://unity.com") },
    excludedServiceTitles = new[] { "Add to Reading List" }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareExcludingServices.png" alt="Example_MacShareManager_ShareExcludingServices" width="800" />
</p>

---

### Share via a Named Service (Mail)

`ShareViaService` performs a single named sharing service directly, without presenting the picker. `recipients` and `subject` are applied when the target service supports them (for example, Mail). Use `MacShareServiceNames` for known raw service identifiers.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.ShareViaService(MacShareServiceNames.MailCompose, new MacShareContentPayload
{
    items = new[] { MacShareItem.Text("Body text") },
    recipients = new[] { "test@example.com" },
    subject = "Sample Subject"
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareViaMail.png" alt="Example_MacShareManager_ShareViaMail" width="800" />
</p>

> **Note:** `MacShareServiceNames` holds well-known raw `NSSharingService.Name` identifiers such as `MailCompose`. These are input identifiers for `ShareViaService`, not the display names returned in `MacShareResult.ServiceName`.

---

### Receive results

Subscribe to `ShareCompleted` on `OnEnable` and unsubscribe on `OnDisable` to avoid stale references after screen transitions. `ShareCompleted` fires for both `Share` and `ShareViaService`, and it always fires before the optional per-call callback passed to either method.

```csharp
private void OnEnable()
{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
    MacShareManager.Instance.ShareCompleted += OnShareCompleted;
#endif
}

private void OnDisable()
{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
    MacShareManager.Instance.ShareCompleted -= OnShareCompleted;
#endif
}

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
private void OnShareCompleted(MacShareResult result)
{
    if (!result.IsSuccess)
    {
        Debug.LogError($"Share failed: {result.ErrorMessage}");
        return;
    }

    if (result.Completed)
    {
        Debug.Log($"Share completed: service={result.ServiceName}");
    }
    else
    {
        Debug.Log("Share cancelled by the user.");
    }
}
#endif
```

> **Note:** User cancellation is not an error: `IsSuccess` is `true` and `Completed` is `false`, with `ServiceName` set to `null`.

---

### Error Handling

All `Share` and `ShareViaService` calls report their outcome via `ShareCompleted` (and the optional per-call callback). The `ErrorMessage` field is guaranteed to be non-null whenever `IsSuccess` is `false`.

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// No items: fails immediately without presenting the picker.
// ErrorMessage: "No shareable items were provided."
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = Array.Empty<MacShareItem>()
});

// Invalid URL string: fails with a native validation error.
// ErrorMessage: "Invalid URL: not a valid url."
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Url("not a valid url") }
});

// Missing file: fails when the native layer cannot find the file.
// ErrorMessage: "File not found at path: /nonexistent/share-missing.txt."
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.File("/nonexistent/share-missing.txt") }
});

// Missing image: fails when the native layer cannot load the image.
// ErrorMessage: "Failed to load image at path: /nonexistent/share-missing.png."
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Image("/nonexistent/share-missing.png") }
});

// Unknown service: fails when the named service does not exist or cannot perform.
// ErrorMessage: "Sharing service unavailable: invalid.service."
MacShareManager.Instance.ShareViaService("invalid.service", new MacShareContentPayload
{
    items = new[] { MacShareItem.Text("Body text") }
});
#endif
```
