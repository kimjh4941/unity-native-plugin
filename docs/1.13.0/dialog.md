# Dialog

Language:

- English (this page)
- 日本語: [dialog.ja.md](dialog.ja.md)
- 한국어: [dialog.ko.md](dialog.ko.md)

← [Back to manual top](index.md)

---

## Table of Contents

- [Android](#android)
  - [Changes in 1.13.0](#changes-in-1130)
  - [AndroidDialogManager](#androiddialogmanager)
- [iOS](#ios)
  - [IosDialogManager](#iosdialogmanager)
- [Windows](#windows)
  - [Changes in 1.12.0](#changes-in-1120)
  - [WindowsDialogManager](#windowsdialogmanager)
- [macOS](#macos)
  - [MacDialogManager](#macdialogmanager)

---

## Android

### Changes in 1.13.0

Since 1.13.0 the Android dialogs run on native-toolkit's C ABI 2.0.0 (`libntk.so`). The methods, the events, their parameters and the default values are unchanged. What differs from 1.12.0:

#### Updating your code

- Replace `using JonghyunKim.NativeToolkit.Runtime.Dialog;` with `using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;` in your Android code. Keep the old line in iOS and macOS code: their types did not move.
- No Dialog type was renamed, so the `using` line above is the only edit your code needs. Unity does not do this for you: the API Updater does not rewrite references to this package's types, so a script that still uses the 1.12.0 names fails to compile. Code inside `#if UNITY_ANDROID && !UNITY_EDITOR` (as in the examples on this page) is not compiled in the Editor, so the errors appear only when you build the Android player.

#### Breaking changes shared by every Android feature

- **The 1.x native libraries are no longer bundled.** `android-native-toolkit-1.3.0.aar` and `unity-android-native-toolkit-1.3.0.aar` were removed. The package ships `android-native-toolkit-2.0.0.aar` and `android-native-toolkit-capi-2.0.0.aar` (native-toolkit's C ABI 2.0.0). Code that called their classes (`android.unity.*`, `android.library.*`) directly no longer works.
- **"Configure Gradle Project" is gone.** The menu Tools > Native Toolkit > Android > Configure Gradle Project and its window were removed. The build now adds the runtime dependencies to `unityLibrary/build.gradle` itself (7 libraries, including `kotlin-stdlib` 2.2.21 and `kotlinx-coroutines-core` 1.9.0) and no longer applies the Kotlin Gradle plugin. If you use your own Gradle templates or EDM4U, check them for conflicting versions.
- **64-bit and IL2CPP only.** A build that also targets ARMv7 (32-bit) stops with a message, and Mono is not supported. See [Android requirements](index.md#android-requirements).

#### Differences in the dialogs

| Case | 1.12.0 | 1.13.0 |
| --- | --- | --- |
| A single or multiple choice dialog closed by Back or an outside tap | `checkedItem` / `checkedItems` held the selection at that moment | Both are `null`. `buttonText = "Cancel"`, `isSuccess = true` and `errorMessage = ""` are unchanged |
| `checkedItem` below -1, or not less than the number of items | The dialog showed with nothing selected, and the positive button returned the value as passed | No dialog; the event reports `"checkedItem is out of range."` at once, in the Editor too. `-1` (nothing selected) still works |
| `checkedItems` longer than the items | Returned at the length passed | Cut to the number of items |
| `checkedItems` shorter than the items | Not supported (expected to throw on the Android main thread and end the app) | Padded with `false` |
| A `null` item in `singleChoiceItems` / `multiChoiceItems` | Passed to Android, where it could end the app. The Editor let it through with a warning only | No dialog; the event reports `"singleChoiceItems cannot contain null."` / `"multiChoiceItems cannot contain null."` at once, in the Editor too |
| U+0000 inside a string | Passed through | The string ends there |
| Unpaired surrogates in strings | Passed through | Replaced with U+FFFD, both in the strings you pass and in the strings that come back |
| Called while the app is not in the foreground | `"Failed to <method>: <exception message>"` | `"Failed to <method>: the app is not in the foreground."` |
| The system closed the dialog (its host or Activity ended) | No event | A failure: `"Failed to <method>: the dialog was closed by the system."` |
| The foreground Activity is not a `FragmentActivity` | `"Failed to cast context to FragmentActivity"` | The dialog shows in a transparent host Activity. Unity's Activity is a `FragmentActivity`, so this rarely applies |
| Error messages | Included the exception text | The fixed texts below |
| Logs | Failed checks were logged as errors; inputs, user names and passwords were logged as they are | Failed checks are logged as warnings; inputs, user names and passwords are logged by length only. The sample scene shows the password as its length too |
| `Initialize()` | Connected to the native bridge | Only logs whether the native library can be used. You do not need to call it |

The `errorMessage` texts are below. `<method>` is the same name as in 1.12.0: `showDialog`, `showConfirmDialog`, `showSingleChoiceItemDialog`, `showMultiChoiceItemDialog`, `showTextInputDialog` or `showLoginDialog`. "At once" means the event is raised before the `Show*` method returns; "later" means after it returns.

| Cause | When | `errorMessage` |
| --- | --- | --- |
| An empty title, message or item list (unchanged) | At once | `"Title cannot be null or empty."` / `"Message cannot be null or empty."` / `"singleChoiceItems cannot be null or empty."` / `"multiChoiceItems cannot be null or empty."` |
| A `null` item | At once | `"singleChoiceItems cannot contain null."` / `"multiChoiceItems cannot contain null."` |
| `checkedItem` out of range | At once | `"checkedItem is out of range."` |
| The native library cannot be used (1.12.0: `"pluginInstance is null"`) | At once | `"Failed to <method>: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only)."` |
| The native library is not initialized yet | At once | `"Failed to <method>: the native library is not initialized yet."` |
| The library rejected the request (a defect in this package) | At once | `"Failed to <method>: the request was rejected (INVALID_PARAMETER)."` (or `NOT_SUPPORTED`) |
| The library refused the call for another reason | At once | `"Failed to <method>: <name>."` (`<name>` is the error's name, such as `OUT_OF_MEMORY` or `UNKNOWN`) |
| The app is not in the foreground | Later | `"Failed to <method>: the app is not in the foreground."` |
| The dialog host could not be started | Later | `"Failed to <method>: the dialog host could not be started."` |
| The dialog could not be shown | Later | `"Failed to <method>: the dialog could not be shown."` |
| The system closed the dialog | Later | `"Failed to <method>: the dialog was closed by the system."` |
| Any other failure after the call was accepted | Later | `"Failed to <method>: <name>."` |
| An exception in C# (unchanged) | At once | `"Internal error: <exception message>"` |

### AndroidDialogManager

#### ShowDialog - Basic dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidDialogManager.Instance.DialogResult += OnDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
// Title (required).
string title = "Hello from Unity";
// Message (required).
string message = "This is a native Android dialog!";
// Button text (defaults to "OK").
string buttonText = "OK";
// Allow dismiss on touch outside (defaults to true).
bool cancelableOnTouchOutside = false;
// Allow cancel via back key, etc. (defaults to true).
bool cancelable = false;
AndroidDialogManager.Instance.ShowDialog(
  title,
  message,
  buttonText,
  cancelableOnTouchOutside,
  cancelable
);
#endif
```

<p align="center">
  <img src="images/android/dialog/Example_AndroidDialogManager_ShowDialog.png" alt="Example_AndroidDialogManager_ShowDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnDialogResult(
  string? buttonText,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowConfirmDialog - Confirmation dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidDialogManager.Instance.ConfirmDialogResult += OnConfirmDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
// Title (required).
string title = "Confirmation";
// Message (required).
string message = "Do you want to proceed with this action?";
// Negative button text (defaults to "No").
string negativeButtonText = "No";
// Positive button text (defaults to "Yes").
string positiveButtonText = "Yes";
// Allow dismiss on touch outside (defaults to true).
bool cancelableOnTouchOutside = false;
// Allow cancel via back key, etc. (defaults to true).
bool cancelable = false;
AndroidDialogManager.Instance.ShowConfirmDialog(
  title,
  message,
  negativeButtonText,
  positiveButtonText,
  cancelableOnTouchOutside,
  cancelable
);
#endif
```

<p align="center">
  <img src="images/android/dialog/Example_AndroidDialogManager_ShowConfirmDialog.png" alt="Example_AndroidDialogManager_ShowConfirmDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnConfirmDialogResult(
  string? buttonText,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowSingleChoiceDialog - Single choice dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidDialogManager.Instance.SingleChoiceItemDialogResult += OnSingleChoiceItemDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
// Title (required).
string title = "Please select one";
// Options (required).
string[] singleChoiceItems = { "Option 1", "Option 2", "Option 3" };
// Default selected index (defaults to 0).
int checkedItem = 0;
// Negative button text (defaults to "Cancel").
string negativeButtonText = "Cancel";
// Positive button text (defaults to "OK").
string positiveButtonText = "OK";
// Allow dismiss on touch outside (defaults to true).
bool cancelableOnTouchOutside = false;
// Allow cancel via back key, etc. (defaults to true).
bool cancelable = false;
AndroidDialogManager.Instance.ShowSingleChoiceItemDialog(
  title,
  singleChoiceItems,
  checkedItem,
  negativeButtonText,
  positiveButtonText,
  cancelableOnTouchOutside,
  cancelable
);
#endif
```

<p align="center">
  <img src="images/android/dialog/Example_AndroidDialogManager_ShowSingleChoiceItemDialog.png" alt="Example_AndroidDialogManager_ShowSingleChoiceItemDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// checkedItem: Selected index. Null on error, on the negative button, when nothing is selected, or when the dialog is closed by Back or an outside tap.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnSingleChoiceItemDialogResult(
  string? buttonText,
  int? checkedItem,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowMultiChoiceDialog - Multi choice dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidDialogManager.Instance.MultiChoiceItemDialogResult += OnMultiChoiceItemDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
// Title (required).
string title = "Multiple Selection";
// Options (required).
string[] multiChoiceItems = { "Item 1", "Item 2", "Item 3", "Item 4" };
// Default selection states (defaults to all false).
bool[] checkedItems = { false, true, false, true };
// Negative button text (defaults to "Cancel").
string negativeButtonText = "Cancel";
// Positive button text (defaults to "OK").
string positiveButtonText = "OK";
// Allow dismiss on touch outside (defaults to true).
bool cancelableOnTouchOutside = false;
// Allow cancel via back key, etc. (defaults to true).
bool cancelable = false;
AndroidDialogManager.Instance.ShowMultiChoiceItemDialog(
  title,
  multiChoiceItems,
  checkedItems,
  negativeButtonText,
  positiveButtonText,
  cancelableOnTouchOutside,
  cancelable
);
#endif
```

<p align="center">
  <img src="images/android/dialog/Example_AndroidDialogManager_ShowMultiChoiceItemDialog.png" alt="Example_AndroidDialogManager_ShowMultiChoiceItemDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// checkedItems: Selection states. Null on error, on the negative button, or when the dialog is closed by Back or an outside tap.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnMultiChoiceItemDialogResult(
  string? buttonText,
  bool[]? checkedItems,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowInputDialog - Text input dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidDialogManager.Instance.TextInputDialogResult += OnTextInputDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
// Title (required).
string title = "Text Input";
// Message (required).
string message = "Please enter your name";
// Placeholder (defaults to empty).
string placeholder = "Enter here...";
// Negative button text (defaults to "Cancel").
string negativeButtonText = "Cancel";
// Positive button text (defaults to "OK").
string positiveButtonText = "OK";
// Enable positive when empty (defaults to false).
bool enablePositiveButtonWhenEmpty = false;
// Allow dismiss on touch outside (defaults to true).
bool cancelableOnTouchOutside = false;
// Allow cancel via back key, etc. (defaults to true).
bool cancelable = false;
AndroidDialogManager.Instance.ShowTextInputDialog(
  title,
  message,
  placeholder,
  negativeButtonText,
  positiveButtonText,
  enablePositiveButtonWhenEmpty,
  cancelableOnTouchOutside,
  cancelable
);
#endif
```

<p align="center">
  <img src="images/android/dialog/Example_AndroidDialogManager_ShowTextInputDialog.png" alt="Example_AndroidDialogManager_ShowTextInputDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// inputText: Entered text. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnTextInputDialogResult(
  string? buttonText,
  string? inputText,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowLoginDialog - Login dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidDialogManager.Instance.LoginDialogResult += OnLoginDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
// Title (required).
string title = "Login";
// Message (required).
string message = "Please enter your credentials";
// Username hint (defaults to "Username").
string usernameHint = "Username";
// Password hint (defaults to "Password").
string passwordHint = "Password";
// Negative button text (defaults to "Cancel").
string negativeButtonText = "Cancel";
// Positive button text (defaults to "Login").
string positiveButtonText = "Login";
// Enable positive when empty (defaults to false).
bool enablePositiveButtonWhenEmpty = false;
// Allow dismiss on touch outside (defaults to true).
bool cancelableOnTouchOutside = false;
// Allow cancel via back key, etc. (defaults to true).
bool cancelable = false;
AndroidDialogManager.Instance.ShowLoginDialog(
  title,
  message,
  usernameHint,
  passwordHint,
  negativeButtonText,
  positiveButtonText,
  enablePositiveButtonWhenEmpty,
  cancelableOnTouchOutside,
  cancelable
);
#endif
```

<p align="center">
  <img src="images/android/dialog/Example_AndroidDialogManager_ShowLoginDialog.png" alt="Example_AndroidDialogManager_ShowLoginDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// username: Entered username. Null on error.
// password: Entered password. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnLoginDialogResult(
  string? buttonText,
  string? username,
  string? password,
  bool isSuccess,
  string? errorMessage
)
```

---

## iOS

### IosDialogManager

#### ShowDialog - Basic dialog

- Import the namespace.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
IosDialogManager.Instance.DialogResult += OnDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
// Title (required).
string title = "Hello from Unity";
// Message (required).
string message = "This is a native iOS dialog!";
// Button text (defaults to "OK").
string buttonText = "OK";
IosDialogManager.Instance.ShowDialog(
  title,
  message,
  buttonText
);
#endif
```

<p align="center">
  <img src="images/ios/dialog/Example_IosDialogManager_ShowDialog.png" alt="Example_IosDialogManager_ShowDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnDialogResult(
  string? buttonText,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowConfirmDialog - Confirmation dialog

- Import the namespace.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
IosDialogManager.Instance.ConfirmDialogResult += OnConfirmDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
// Title (required).
string title = "Confirm Action";
// Message (required).
string message = "Are you sure you want to proceed?";
// Confirm button text (defaults to "OK").
string confirmButtonText = "Yes";
// Cancel button text (defaults to "Cancel").
string cancelButtonText = "No";
IosDialogManager.Instance.ShowConfirmDialog(
  title,
  message,
  confirmButtonText,
  cancelButtonText
);
#endif
```

<p align="center">
  <img src="images/ios/dialog/Example_IosDialogManager_ShowConfirmDialog.png" alt="Example_IosDialogManager_ShowConfirmDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnConfirmDialogResult(
  string? buttonText,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowDestructiveDialog - Destructive dialog

- Import the namespace.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
IosDialogManager.Instance.DestructiveDialogResult += OnDestructiveDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
// Title (required).
string title = "Delete File";
// Message (required).
string message = "This action cannot be undone. Are you sure?";
// Destructive button text (defaults to "Delete").
string destructiveButtonText = "Delete";
// Cancel button text (defaults to "Cancel").
string cancelButtonText = "Cancel";
IosDialogManager.Instance.ShowDestructiveDialog(
  title,
  message,
  destructiveButtonText,
  cancelButtonText
);
#endif
```

<p align="center">
  <img src="images/ios/dialog/Example_IosDialogManager_ShowDestructiveDialog.png" alt="Example_IosDialogManager_ShowDestructiveDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnDestructiveDialogResult(
  string? buttonText,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowActionSheet - Action sheet

- Import the namespace.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
IosDialogManager.Instance.ActionSheetResult += OnActionSheetResult;
#endif
```

- Show the action sheet.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
// Title (required).
string title = "Select Source";
// Message (required).
string message = "Choose where to get the file from";
// Options (required).
string[] options = { "Camera", "Photo Library", "Documents" };
// Cancel button text (defaults to "Cancel").
string cancelButtonText = "Cancel";
IosDialogManager.Instance.ShowActionSheet(
  title,
  message,
  options,
  cancelButtonText
);
#endif
```

<p align="center">
  <img src="images/ios/dialog/Example_IosDialogManager_ShowActionSheet.png" alt="Example_IosDialogManager_ShowActionSheet" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnActionSheetResult(
  string? buttonText,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowTextInputDialog - Text input dialog

- Import the namespace.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
IosDialogManager.Instance.TextInputDialogResult += OnTextInputDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
// Title (required).
string title = "Enter Name";
// Message (required).
string message = "Please enter your name";
// Placeholder (defaults to empty).
string placeholder = "Your name here";
// Confirm button text (defaults to "OK").
string confirmButtonText = "OK";
// Cancel button text (defaults to "Cancel").
string cancelButtonText = "Cancel";
// Enable confirm when empty (defaults to false).
bool enableConfirmWhenEmpty = false;
IosDialogManager.Instance.ShowTextInputDialog(
  title,
  message,
  placeholder,
  confirmButtonText,
  cancelButtonText,
  enableConfirmWhenEmpty
);
#endif
```

<p align="center">
  <img src="images/ios/dialog/Example_IosDialogManager_ShowTextInputDialog.png" alt="Example_IosDialogManager_ShowTextInputDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// inputText: Entered text. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnTextInputDialogResult(
  string? buttonText,
  string? inputText,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowLoginDialog - Login dialog

- Import the namespace.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
IosDialogManager.Instance.LoginDialogResult += OnLoginDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: iOS (Player) only. Avoid native calls in the Editor.
#if UNITY_IOS && !UNITY_EDITOR
// Title (required).
string title = "Login Required";
// Message (required).
string message = "Please enter your credentials";
// Username placeholder (defaults to "Username").
string usernamePlaceholder = "Username";
// Password placeholder (defaults to "Password").
string passwordPlaceholder = "Password";
// Login button text (defaults to "Login").
string loginButtonText = "Login";
// Cancel button text (defaults to "Cancel").
string cancelButtonText = "Cancel";
// Enable login when empty (defaults to false).
bool enableLoginWhenEmpty = false;
IosDialogManager.Instance.ShowLoginDialog(
  title,
  message,
  usernamePlaceholder,
  passwordPlaceholder,
  loginButtonText,
  cancelButtonText,
  enableLoginWhenEmpty
);
#endif
```

<p align="center">
  <img src="images/ios/dialog/Example_IosDialogManager_ShowLoginDialog.png" alt="Example_IosDialogManager_ShowLoginDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// username: Entered username. Null on error.
// password: Entered password. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnLoginDialogResult(
  string? buttonText,
  string? username,
  string? password,
  bool isSuccess,
  string? errorMessage
)
```

---

## Windows

### Changes in 1.12.0

Since 1.12.0 the Windows dialogs run on the native-toolkit C ABI 2.0.0 (`windows-native-toolkit-capi-2.0.0.dll`). The methods, the events and their parameters are unchanged. What differs from 1.11.0:

- **Namespace.** `WindowsDialogManager`, `Win32MessageBox` and `WindowsDialogErrorCodes` are in `JonghyunKim.NativeToolkit.Runtime.Windows.Dialog`. Replace `using JonghyunKim.NativeToolkit.Runtime.Dialog;` in Windows code. The iOS and macOS types keep their namespace; the Android types moved in 1.13.0 (see [Changes in 1.13.0](#changes-in-1130)).
- **The Editor.** The Windows types also compile in the Editor. A call there shows nothing and reports `-5` through the event.
- **Reserved error codes.** Besides the OS value, `errorCode` can be one of the values below. Compare with the constants rather than checking the sign: an OS `HRESULT` is negative too.

| Constant (`WindowsDialogErrorCodes`) | Value | Meaning |
| --- | --- | --- |
| `Cancelled` | -1 | The user cancelled a file or folder dialog (`isCancelled = true`, `isSuccess = true`) |
| `InvalidArgument` | -2 | Refused before showing anything: an empty title or message, an unsupported flag, a filter without a pattern |
| `Unknown` | -3 | A failure that cannot be described more precisely |
| `NativeUnavailable` | -4 | The native library is missing, lacks a function, cannot be loaded, or is not C ABI 2.x |
| `PlatformUnavailable` | -5 | Not a Windows player (for example the Editor). No dialog is shown |

| Case | 1.11.0 | 1.12.0 |
| --- | --- | --- |
| `bufferSize` | Used as the buffer; a longer result failed | Ignored. One path can be up to 1023 characters and a multiple selection up to 32768 characters in total; beyond that the OS error code is returned |
| Empty title or message | An empty dialog, and up to three events | No dialog; one event with `-2` |
| `MB_SYSTEMMODAL`, `MB_TASKMODAL`, `MB_RIGHT`, `MB_RTLREADING` | Passed to Windows | No dialog; `-2`. Supported: the 7 button sets, 4 icons, 4 default buttons, `MB_APPLMODAL`, `MB_TOPMOST` and `MB_HELP` (0x4000) |
| A filter pair without a pattern | Passed to Windows | `-2` |
| Empty entries in a pattern (`"*.txt;;*.log"`) | Passed to Windows | Collapsed to `"*.txt;*.log"` |
| A filter with no pairs (`""`) | An empty filter | One "All files" pair |
| `ShowSaveFileDialog` with `defExt = ""` | Windows appended the selected filter's extension (`new` becomes `new.txt`) | The name is returned as typed (`new` stays `new`). Pass `defExt` to get an extension; the default `"txt"` gives `new.txt` |
| `ShowFolderDialog` with `title = ""` | An empty title | The system title |
| Multiple files selected at a drive root | `C:\\a.txt` | `C:\a.txt` |
| The message box returns 0 with no OS error | `(0, true, null)` | `(0, false, -3)` |
| The native library is missing or cannot be loaded | An exception at the call | No exception; the event reports `-4` |
| Any other exception in C# | Thrown to the caller | No exception; the event reports `-3` |
| Unpaired surrogates in strings | Passed through | Replaced with U+FFFD |

### WindowsDialogManager

#### ShowDialog - Basic dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsDialogManager.Instance.AlertDialogResult += OnAlertDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
// Title (required).
string title = "Native Windows Dialog";
// Message (required).
string message = "This is a native Windows dialog!";
// Buttons: OK + Cancel (defaults to MB_OK).
uint buttons = Win32MessageBox.MB_OKCANCEL;
// Icon: information (defaults to MB_ICONINFORMATION).
uint icon = Win32MessageBox.MB_ICONINFORMATION;
// Default button: second (defaults to MB_DEFBUTTON1).
uint defbutton = Win32MessageBox.MB_DEFBUTTON2;
// Options: application modal (defaults to MB_APPLMODAL).
uint options = Win32MessageBox.MB_APPLMODAL;
WindowsDialogManager.Instance.ShowDialog(
  title,
  message,
  buttons,
  icon,
  defbutton,
  options
);
#endif
```

<p align="center">
  <img src="images/windows/dialog/Example_WindowsDialogManager_ShowDialog.png" alt="Example_WindowsDialogManager_ShowDialog" width="300" />
</p>
- Receive results via events.

```csharp
// result: Pressed button identifier. Null on error.
// isSuccess: True on success.
// errorCode: Error code (null on success).

private void OnAlertDialogResult(
  int? result,
  bool isSuccess,
  int? errorCode
)
```

#### ShowFileDialog - File picker dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsDialogManager.Instance.FileDialogResult += OnFileDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
// Buffer size (defaults to 1024).
uint bufferSize = 1024;
// Filter (defaults to "All Files\0*.*\0\0").
string filter = "All Files\0*.*\0\0";
WindowsDialogManager.Instance.ShowFileDialog(
  bufferSize,
  filter
);
#endif
```

<p align="center">
  <img src="images/windows/dialog/Example_WindowsDialogManager_ShowFileDialog.png" alt="Example_WindowsDialogManager_ShowFileDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// filePath: Selected path. Null if cancelled or failed.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorCode: Error code (null on success).

private void OnFileDialogResult(
  string? filePath,
  bool isCancelled,
  bool isSuccess,
  int? errorCode
)
```

#### ShowMultiFileDialog - Multi-file picker dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsDialogManager.Instance.MultiFileDialogResult += OnMultiFileDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
// Buffer size (defaults to 4096).
uint bufferSize = 4096;
// Filter (defaults to "All Files\0*.*\0\0").
string filter = "All Files\0*.*\0\0";
WindowsDialogManager.Instance.ShowMultiFileDialog(
  bufferSize,
  filter
);
#endif
```

<p align="center">
  <img src="images/windows/dialog/Example_WindowsDialogManager_ShowMultiFileDialog.png" alt="Example_WindowsDialogManager_ShowMultiFileDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// filePaths: Selected file paths (ArrayList). Null if cancelled or failed.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorCode: Error code (null on success).

private void OnMultiFileDialogResult(
  ArrayList? filePaths,
  bool isCancelled,
  bool isSuccess,
  int? errorCode
)
```

#### ShowFolderDialog - Folder picker dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsDialogManager.Instance.FolderDialogResult += OnFolderDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
// Buffer size (defaults to 1024).
uint bufferSize = 1024;
// Title (defaults to "Select Folder").
string title = "Select Folder";
WindowsDialogManager.Instance.ShowFolderDialog(
  bufferSize,
  title
);
#endif
```

<p align="center">
  <img src="images/windows/dialog/Example_WindowsDialogManager_ShowFolderDialog.png" alt="Example_WindowsDialogManager_ShowFolderDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// folderPath: Selected path. Null if cancelled or failed.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorCode: Error code (null on success).

private void OnFolderDialogResult(
  string? folderPath,
  bool isCancelled,
  bool isSuccess,
  int? errorCode
)
```

#### ShowMultiFolderDialog - Multi-folder picker dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsDialogManager.Instance.MultiFolderDialogResult += OnMultiFolderDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
// Buffer size (defaults to 4096).
uint bufferSize = 4096;
// Title (defaults to "Select Folders").
string title = "Select Folders";
WindowsDialogManager.Instance.ShowMultiFolderDialog(
  bufferSize,
  title
);
#endif
```

<p align="center">
  <img src="images/windows/dialog/Example_WindowsDialogManager_ShowMultiFolderDialog.png" alt="Example_WindowsDialogManager_ShowMultiFolderDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// folderPaths: Selected folder paths (ArrayList). Null if cancelled or failed.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorCode: Error code (null on success).

private void OnMultiFolderDialogResult(
  ArrayList? folderPaths,
  bool isCancelled,
  bool isSuccess,
  int? errorCode
)
```

#### ShowSaveFileDialog - Save file dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
#endif
```

- Register events.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
WindowsDialogManager.Instance.SaveFileDialogResult += OnSaveFileDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
// Buffer size (defaults to 1024).
uint bufferSize = 1024;
// Filter (defaults to "All Files\0*.*\0\0").
string filter = "All Files\0*.*\0\0";
// Default extension (defaults to "txt").
string defExt = "txt";
WindowsDialogManager.Instance.ShowSaveFileDialog(
  bufferSize,
  filter,
  defExt
);
#endif
```

<p align="center">
  <img src="images/windows/dialog/Example_WindowsDialogManager_ShowSaveFileDialog.png" alt="Example_WindowsDialogManager_ShowSaveFileDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// filePath: Saved file path. Null if cancelled or failed.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorCode: Error code (null on success).

private void OnSaveFileDialogResult(
  string? filePath,
  bool isCancelled,
  bool isSuccess,
  int? errorCode
)
```

---

## macOS

### MacDialogManager

#### ShowDialog - Basic dialog

- Import the namespace.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
MacDialogManager.Instance.AlertDialogResult += OnAlertDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// Title (required).
string title = "Hello from Unity";
// Message (optional).
string message = "This is a native macOS dialog!";
// Buttons (at least one required).
DialogButton[] buttons = {
  // title: Button title (required).
  // isDefault: True to mark default button (only one allowed).
  // keyEquivalent: Optional shortcut. Default button uses Enter.
  new DialogButton(title: "OK", isDefault: true),
  new DialogButton(title: "Cancel", keyEquivalent: ""),
  new DialogButton(title: "Delete", keyEquivalent: "d")
};
// Options (required).
DialogOptions options = new DialogOptions(
  // alertStyle: Dialog style (required).
  alertStyle: DialogOptions.AlertStyle.Informational,
  // showsHelp: Show help button.
  showsHelp: true,
  // showsSuppressionButton: Show suppression checkbox.
  showsSuppressionButton: true,
  // suppressionButtonTitle: Optional title. Uses OS default when omitted.
  suppressionButtonTitle: "Don't show this again",
  // icon: Icon configuration.
  icon: new IconConfiguration(
    // Examples for each icon type are shown below.
    ...
  )
);

// Example: system symbol icon.
DialogOptions options = new DialogOptions(
  ...
  icon: new IconConfiguration(
    // type: System symbol icon (required).
    type: IconConfiguration.IconType.SystemSymbol,
    // value: Symbol name (required).
    value: "info.square.fill",
    // mode: Rendering mode (required).
    mode: IconConfiguration.RenderingMode.Palette,
    // colors: 1-3 colors for Palette, 1 color for Hierarchical.
    colors: new List<string> { "white", "systemblue", "systemblue" },
    // size: Point size (ignored by dialogs).
    size: 64f,
    // weight: Symbol weight.
    weight: IconConfiguration.Weight.Regular,
    // scale: Symbol scale (ignored by dialogs).
    scale: IconConfiguration.Scale.Medium
  )
);

// Example: file path icon.
DialogOptions options = new DialogOptions(
  ...
  icon: new IconConfiguration(
    // type: File path icon (required).
    type: IconConfiguration.IconType.FilePath,
    // value: File path (required).
    value: "/Users/user/Downloads/test.png"
  )
);

// Example: named image icon.
DialogOptions options = new DialogOptions(
  ...
  icon: new IconConfiguration(
    // type: Named image icon (required).
    type: IconConfiguration.IconType.NamedImage,
    // value: Image name (required).
    value: "test-image"
  )
);

// Example: app icon.
DialogOptions options = new DialogOptions(
  ...
  icon: new IconConfiguration(
    // type: App icon (required).
    type: IconConfiguration.IconType.AppIcon
  )
);

// Example: system image icon.
DialogOptions options = new DialogOptions(
  ...
  icon: new IconConfiguration(
    // type: System image icon (required).
    type: IconConfiguration.IconType.SystemImage,
    // value: System image name (required).
    value: "cautionName"
  )
);

MacDialogManager.Instance.ShowDialog(
  title,
  message,
  buttons,
  options
);
#endif
```

<p align="center">
  <img src="images/mac/dialog/Example_MacDialogManager_ShowDialog.png" alt="Example_MacDialogManager_ShowDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonTitle: Pressed button title. Null on error.
// buttonIndex: Pressed button index.
// suppressionState: Suppression checkbox state.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnAlertDialogResult(
  string? buttonTitle,
  int buttonIndex,
  bool suppressionState,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowFileDialog - File picker dialog

- Import the namespace.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
MacDialogManager.Instance.FileDialogResult += OnFileDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// Title (required).
string title = "Select a file";
// Message (optional).
string message = "Please select a file to open.";
// Allowed file extensions (defaults to OS values).
string[] allowedContentTypes = { "txt", "png" };
// Initial directory (defaults to OS value).
string? directoryPath = null;
MacDialogManager.Instance.ShowFileDialog(
  title,
  message,
  allowedContentTypes,
  directoryPath
);
#endif
```

<p align="center">
  <img src="images/mac/dialog/Example_MacDialogManager_ShowFileDialog.png" alt="Example_MacDialogManager_ShowFileDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// filePaths: Selected file paths. Null on error.
// fileCount: Returned file count (0 when cancelled).
// directoryURL: Directory URL used for selection. Null on error.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnFileDialogResult(
  string[]? filePaths,
  int fileCount,
  string? directoryURL,
  bool isCancelled,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowMultiFileDialog - Multi-file picker dialog

- Import the namespace.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
MacDialogManager.Instance.MultiFileDialogResult += OnMultiFileDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// Title (required).
string title = "Select files";
// Message (optional).
string message = "Please select files to open.";
// Allowed file extensions (defaults to OS values).
string[] allowedContentTypes = { "txt", "png" };
// Initial directory (defaults to OS value).
string? directoryPath = null;
MacDialogManager.Instance.ShowMultiFileDialog(
  title,
  message,
  allowedContentTypes,
  directoryPath
);
#endif
```

<p align="center">
  <img src="images/mac/dialog/Example_MacDialogManager_ShowMultiFileDialog.png" alt="Example_MacDialogManager_ShowMultiFileDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// filePaths: Selected file paths. Null on error.
// fileCount: Returned file count (0 when cancelled).
// directoryURL: Directory URL used for selection. Null on error.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnMultiFileDialogResult(
  string[]? filePaths,
  int fileCount,
  string? directoryURL,
  bool isCancelled,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowFolderDialog - Folder picker dialog

- Import the namespace.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
MacDialogManager.Instance.FolderDialogResult += OnFolderDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// Title (required).
string title = "Select a folder";
// Message (optional).
string message = "Please select a folder to open.";
// Initial directory (defaults to OS value).
string? directoryPath = null;
MacDialogManager.Instance.ShowFolderDialog(
  title,
  message,
  directoryPath
);
#endif
```

<p align="center">
  <img src="images/mac/dialog/Example_MacDialogManager_ShowFolderDialog.png" alt="Example_MacDialogManager_ShowFolderDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// folderPaths: Selected folder paths. Null on error.
// folderCount: Returned folder count (0 when cancelled).
// directoryURL: Directory URL used for selection. Null on error.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnFolderDialogResult(
  string[]? folderPaths,
  int folderCount,
  string? directoryURL,
  bool isCancelled,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowMultiFolderDialog - Multi-folder picker dialog

- Import the namespace.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
MacDialogManager.Instance.MultiFolderDialogResult += OnMultiFolderDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// Title (required).
string title = "Select folders";
// Message (optional).
string message = "Please select folders to open.";
// Initial directory (defaults to OS value).
string? directoryPath = null;
MacDialogManager.Instance.ShowMultiFolderDialog(
  title,
  message,
  directoryPath
);
#endif
```

<p align="center">
  <img src="images/mac/dialog/Example_MacDialogManager_ShowMultiFolderDialog.png" alt="Example_MacDialogManager_ShowMultiFolderDialog" width="1000" />
</p>
- Receive results via events.

```csharp
// folderPaths: Selected folder paths. Null on error.
// folderCount: Returned folder count (0 when cancelled).
// directoryURL: Directory URL used for selection. Null on error.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnMultiFolderDialogResult(
  string[]? folderPaths,
  int folderCount,
  string? directoryURL,
  bool isCancelled,
  bool isSuccess,
  string? errorMessage
)
```

#### ShowSaveFileDialog - Save file dialog

- Import the namespace.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif
```

- Register events.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
MacDialogManager.Instance.SaveFileDialogResult += OnSaveFileDialogResult;
#endif
```

- Show the dialog.

```csharp
// Guard: macOS (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
// Title (required).
string title = "Save File";
// Message (optional).
string message = "Choose a destination";
// Default file name (defaults to OS value).
string defaultFileName = "default";
// Allowed file extensions (defaults to OS values).
string[] allowedContentTypes = { "txt" };
// Initial directory (defaults to OS value).
string? directoryPath = null;
MacDialogManager.Instance.ShowSaveFileDialog(
  title,
  message,
  defaultFileName,
  allowedContentTypes,
  directoryPath
);
#endif
```

<p align="center">
  <img src="images/mac/dialog/Example_MacDialogManager_ShowSaveFileDialog.png" alt="Example_MacDialogManager_ShowSaveFileDialog" width="600" />
</p>
- Receive results via events.

```csharp
// filePath: Saved file path. Null if cancelled or failed.
// fileCount: Returned path count (1 on success, 0 when cancelled).
// directoryURL: Directory URL used for saving. Null on error.
// isCancelled: True if the user cancelled.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnSaveFileDialogResult(
  string? filePath,
  int fileCount,
  string? directoryURL,
  bool isCancelled,
  bool isSuccess,
  string? errorMessage
)
```
