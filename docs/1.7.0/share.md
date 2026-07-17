# Share Feature

Language:

- English (this page)
- 日本語: [share.ja.md](share.ja.md)
- 한국어: [share.ko.md](share.ko.md)

← [Back to manual top](index.md)

---

## Table of Contents

- [Android](#android)
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

### Setup

#### Import the namespace

```csharp
// Guard: Android (Player) only. Prevents native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Share;
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
AndroidShareManager.Instance.ShareText(new ShareTextPayload
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
AndroidShareManager.Instance.ShareText(new ShareTextPayload
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

Custom chooser actions appear as additional buttons in the Android share chooser dialog. Requires Android 14 (API 34) or later and an AAR that supports chooser actions. On lower API levels, the share proceeds without custom actions.

Each action must specify a unique `intentAction` string (other than `android.intent.action.SEND`) to receive tap callbacks. Tap events are delivered via `ShareChooserActionTapped` (global event) or the `onChooserAction` parameter of `ShareText` (per-call callback).

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// Subscribe to the global event to receive chooser action taps.
AndroidShareManager.Instance.ShareChooserActionTapped += result =>
{
    Debug.Log($"Chooser action tapped: {result.ActionId}");
};

AndroidShareManager.Instance.ShareText(new ShareTextPayload
{
    text = "Sharing with custom chooser actions (Android 14 / API 34+ only).",
    chooserActions = new[]
    {
        new ChooserActionPayload
        {
            label = "Save",
            iconBase64 = iconBase64, // base64-encoded PNG/JPEG
            intentAction = "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_SAVE"
        },
        new ChooserActionPayload
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

> **Note:** `chooserActions` has a maximum of 5 entries. Exceeding this limit is logged as a warning and the extra entries are ignored by the native layer. `ShareWithCallback` does not support chooser actions; use `ShareText` instead.

---

### Share with Subject and Title

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareText(new ShareTextPayload
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

Displays a preview title and thumbnail image in the chooser. The thumbnail path must point to a file in a FileProvider-accessible directory.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string thumbnailPath = Path.Combine(Application.persistentDataPath, "share_preview_thumbnail.png");
// Write a PNG file to thumbnailPath before calling ShareText.

AndroidShareManager.Instance.ShareText(new ShareTextPayload
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

AndroidShareManager.Instance.ShareImage(new ShareImagePayload
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

AndroidShareManager.Instance.ShareImages(new ShareImagesPayload
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

AndroidShareManager.Instance.ShareFile(new ShareFilePayload
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

AndroidShareManager.Instance.ShareFiles(new ShareFilesPayload
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
AndroidShareManager.Instance.RegisterDirectShareTarget(new DirectShareTargetPayload
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
AndroidShareManager.Instance.RemoveDirectShareTargets(new RemoveDirectShareTargetsPayload
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
    new ShareTextPayload
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

Cancels the BroadcastReceiver that waits for the share selection result. Call this if you no longer want to receive the `onSelected` callback — for example, after the share sheet is dismissed without a selection. `CancelPendingShareCallback` is called automatically in `OnDisable` to prevent stale callbacks after screen transitions.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.CancelPendingShareCallback();
#endif
```

---

### Receive Events

Subscribe to events on `OnEnable` and unsubscribe on `OnDisable` to avoid stale references after screen transitions.

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
private void OnShareOperationCompleted(ShareOperationResult result)
{
    string status = result.IsSuccess ? "Success" : "Failed";
    Debug.Log($"[event] {result.Operation}: {status}");
    if (!string.IsNullOrEmpty(result.ErrorMessage))
        Debug.LogError(result.ErrorMessage);
}

private void OnShareCallbackReceived(ShareCallbackResult result)
{
    string pkg = result.SelectedPackageName ?? "(unknown)";
    Debug.Log($"[event] ShareCallback: Selected {pkg}");
}

private void OnShareChooserActionTapped(ShareChooserActionResult result)
{
    Debug.Log($"[event] ChooserAction tapped: {result.ActionId}");
}
#endif
```

---

### Error Handling

All share operations report success or failure via `ShareOperationCompleted`. The `ErrorMessage` field contains a description when `IsSuccess` is `false`.

Files passed to share APIs must exist and be located in a FileProvider-accessible directory. Passing an invalid path results in an `IllegalFileAccess` error reported through `ShareOperationCompleted`.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// This will trigger an error via ShareOperationCompleted (IsSuccess = false)
AndroidShareManager.Instance.ShareFile(new ShareFilePayload
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
    <img src="images/mac/share/Example_MacShareManager_ShareText.png" alt="Example_MacShareManager_ShareText" width="400" />
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
    <img src="images/mac/share/Example_MacShareManager_ShareUrl.png" alt="Example_MacShareManager_ShareUrl" width="400" />
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
    <img src="images/mac/share/Example_MacShareManager_ShareImage.png" alt="Example_MacShareManager_ShareImage" width="400" />
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
    <img src="images/mac/share/Example_MacShareManager_ShareFile.png" alt="Example_MacShareManager_ShareFile" width="400" />
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
    <img src="images/mac/share/Example_MacShareManager_ShareImages.png" alt="Example_MacShareManager_ShareImages" width="400" />
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
    <img src="images/mac/share/Example_MacShareManager_ShareFiles.png" alt="Example_MacShareManager_ShareFiles" width="400" />
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
    <img src="images/mac/share/Example_MacShareManager_ShareTextAndUrl.png" alt="Example_MacShareManager_ShareTextAndUrl" width="400" />
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
    <img src="images/mac/share/Example_MacShareManager_ShareExcludingServices.png" alt="Example_MacShareManager_ShareExcludingServices" width="400" />
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
    <img src="images/mac/share/Example_MacShareManager_ShareViaMail.png" alt="Example_MacShareManager_ShareViaMail" width="400" />
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
