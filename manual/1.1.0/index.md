# Unity Native Toolkit (Unity 6)

[English](index.md) | [Korean](index.ko.md) | [Japanese](index.ja.md)

- A toolkit that provides native features for Unity 6+.
- The package includes native plugins and sample scenes for Android/iOS/Windows/macOS, and exposes dialog operations via singleton APIs per platform.
- Editor windows help integrate native libraries and Gradle/Xcode settings, streamlining post-build project setup.

# Version

## 1.0.0

# Supported OS Versions

- Android 12+
- iOS 18+
- Windows 11+
- macOS 15+

# Features

## Android

- Dialog features
  - Basic dialog
  - Confirmation dialog
  - Single choice dialog
  - Multi choice dialog
  - Text input dialog
  - Login dialog

## iOS

- Dialog features
  - Basic dialog
  - Confirmation dialog
  - Destructive dialog
  - Action sheet
  - Text input dialog
  - Login dialog

## Windows

- Dialog features
  - Basic dialog
  - File picker dialog
  - Multi-file picker dialog
  - Folder picker dialog
  - Multi-folder picker dialog
  - Save file dialog

## macOS

- Dialog features
  - Basic dialog
  - File picker dialog
  - Multi-file picker dialog
  - Folder picker dialog
  - Multi-folder picker dialog
  - Save file dialog

## Planned Features

- Share
- Clipboard integration
- Notifications

# Getting Started

## Installation

- Open Unity 6.
- Window -> Package Manager.
- Select "install from Git URL...".
- Enter the Git URL for this package:
  - https://github.com/jonghyunkim/unity-native-plugin.git?path=/Packages/com.jonghyunkim.nativetoolkit#1.0.0
- Click "install".
- Requirements:
  - Unity 6+
  - Dependencies: Localization, Addressables, Input System

## Samples

- Open Unity 6.
- Window -> Package Manager.
- Unity Package Manager -> Native Toolkit -> Samples -> Import.
- Tools -> Native Toolkit -> Example.
  <p align="center">
    <img src="images/editor/NativeToolkitEditorWindow.png" alt="NativeToolkit Editor Window" width="720" />
  </p>

- Android sample
  - Select Android - Dialog - AndroidDialogManager.cs.
  - Click the "Open" button.
  - The sample UI appears in the Game view.
  - From Build Profiles, run "Android Profile" -> Export.
  - Tools -> Native Toolkit -> Android -> Configure Gradle Project.
  <p align="center">
    <img src="images/editor/ConfigureGradleProject.png" alt="ConfigureGradleProject" width="720" />
  </p>

  - Click "Browse" and select the exported Android project.
  - Click "Run: Add Kotlin Dependencies" to add Kotlin libraries.
  - Install the sample app from Android Studio.
    - <a href="https://developer.android.com/studio" target="_blank" rel="noopener noreferrer">Reference</a>

- iOS sample
  - Select iOS - Dialog - IosDialogManager.cs.
  - Click the "Open" button.
  - The sample UI appears in the Game view.
  - From Build Profiles, run "iOS Profile" -> Build.
  - Tools -> Native Toolkit -> iOS -> Configure Xcode Project.
  <p align="center">
    <img src="images/editor/IosConfigureXcodeProject.png" alt="IosConfigureXcodeProject" width="720" />
  </p>

  - Click "Browse" and select the built iOS project.
  - Click "Run: Add/Embed iOS XCFrameworks" to add NativeToolkit libraries.
  - Install the sample app from Xcode.
    - <a href="https://developer.apple.com/xcode" target="_blank" rel="noopener noreferrer">Reference</a>

- Windows sample
  - Select Windows - Dialog - WindowsDialogManager.cs.
  - Click the "Open" button.
  - The sample UI appears in the Game view.
  - From Build Profiles, run "Windows Profile" -> Build.
  - Run "Unity NativeToolkit.exe" from the build output folder.

- macOS sample
  - Select macOS - Dialog - MacDialogManager.cs.
  - Click the "Open" button.
  - The sample UI appears in the Game view.
  - From Build Profiles, run "macOS Profile" -> Build.
  - Tools -> Native Toolkit -> macOS -> Configure Xcode Project.
  <p align="center">
    <img src="images/editor/MacConfigureXcodeProject.png" alt="MacConfigureXcodeProject" width="720" />
  </p>

  - Click "Browse" and select the built macOS project.
  - Click "Run: Add UnityMacNativeToolkit.xcframework" to add NativeToolkit libraries.
  - Install the sample app from Xcode.
    - <a href="https://developer.apple.com/xcode" target="_blank" rel="noopener noreferrer">Reference</a>

# API Usage

## Dialogs

## AndroidDialogManager

### ShowDialog - Basic dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/android/Example_AndroidDialogManager_ShowDialog.png" alt="Example_AndroidDialogManager_ShowDialog" width="400" />
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

### ShowConfirmDialog - Confirmation dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/android/Example_AndroidDialogManager_ShowConfirmDialog.png" alt="Example_AndroidDialogManager_ShowConfirmDialog" width="400" />
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

### ShowSingleChoiceDialog - Single choice dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/android/Example_AndroidDialogManager_ShowSingleChoiceItemDialog.png" alt="Example_AndroidDialogManager_ShowSingleChoiceItemDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// checkedItem: Selected index. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnSingleChoiceItemDialogResult(
  string? buttonText,
  int? checkedItem,
  bool isSuccess,
  string? errorMessage
)
```

### ShowMultiChoiceDialog - Multi choice dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/android/Example_AndroidDialogManager_ShowMultiChoiceItemDialog.png" alt="Example_AndroidDialogManager_ShowMultiChoiceItemDialog" width="400" />
</p>
- Receive results via events.

```csharp
// buttonText: The pressed button text. Null on error.
// checkedItems: Selection states. Null on error.
// isSuccess: True on success.
// errorMessage: Error details (null on success).

private void OnMultiChoiceItemDialogResult(
  string? buttonText,
  bool[]? checkedItems,
  bool isSuccess,
  string? errorMessage
)
```

### ShowInputDialog - Text input dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/android/Example_AndroidDialogManager_ShowTextInputDialog.png" alt="Example_AndroidDialogManager_ShowTextInputDialog" width="400" />
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

### ShowLoginDialog - Login dialog

- Import the namespace.

```csharp
// Guard: Android (Player) only. Avoid native calls in the Editor.
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/android/Example_AndroidDialogManager_ShowLoginDialog.png" alt="Example_AndroidDialogManager_ShowLoginDialog" width="400" />
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

## iOSDialogManager

### ShowDialog - Basic dialog

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
  <img src="images/ios/Example_IosDialogManager_ShowDialog.png" alt="Example_IosDialogManager_ShowDialog" width="400" />
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

### ShowConfirmDialog - Confirmation dialog

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
  <img src="images/ios/Example_IosDialogManager_ShowConfirmDialog.png" alt="Example_IosDialogManager_ShowConfirmDialog" width="400" />
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

### ShowDestructiveDialog - Destructive dialog

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
  <img src="images/ios/Example_IosDialogManager_ShowDestructiveDialog.png" alt="Example_IosDialogManager_ShowDestructiveDialog" width="400" />
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

### ShowActionSheet - Action sheet

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
  <img src="images/ios/Example_IosDialogManager_ShowActionSheet.png" alt="Example_IosDialogManager_ShowActionSheet" width="400" />
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

### ShowTextInputDialog - Text input dialog

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
  <img src="images/ios/Example_IosDialogManager_ShowTextInputDialog.png" alt="Example_IosDialogManager_ShowTextInputDialog" width="400" />
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

### ShowLoginDialog - Login dialog

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
  <img src="images/ios/Example_IosDialogManager_ShowLoginDialog.png" alt="Example_IosDialogManager_ShowLoginDialog" width="400" />
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

## WindowsDialogManager

### ShowDialog - Basic dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/windows/Example_WindowsDialogManager_ShowDialog.png" alt="Example_WindowsDialogManager_ShowDialog" width="300" />
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

### ShowFileDialog - File picker dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/windows/Example_WindowsDialogManager_ShowFileDialog.png" alt="Example_WindowsDialogManager_ShowFileDialog" width="1000" />
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

### ShowMultiFileDialog - Multi-file picker dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/windows/Example_WindowsDialogManager_ShowMultiFileDialog.png" alt="Example_WindowsDialogManager_ShowMultiFileDialog" width="1000" />
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

### ShowFolderDialog - Folder picker dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/windows/Example_WindowsDialogManager_ShowFolderDialog.png" alt="Example_WindowsDialogManager_ShowFolderDialog" width="1000" />
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

### ShowMultiFolderDialog - Multi-folder picker dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/windows/Example_WindowsDialogManager_ShowMultiFolderDialog.png" alt="Example_WindowsDialogManager_ShowMultiFolderDialog" width="1000" />
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

### ShowSaveFileDialog - Save file dialog

- Import the namespace.

```csharp
// Guard: Windows (Player) only. Avoid native calls in the Editor.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
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
  <img src="images/windows/Example_WindowsDialogManager_ShowSaveFileDialog.png" alt="Example_WindowsDialogManager_ShowSaveFileDialog" width="1000" />
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

## MacDialogManager

### ShowDialog - Basic dialog

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
  new DialogButton(title: "Cancel", keyEquivalent: "\u001b"),
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
  <img src="images/mac/Example_MacDialogManager_ShowDialog.png" alt="Example_MacDialogManager_ShowDialog" width="400" />
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

### ShowFileDialog - File picker dialog

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
  <img src="images/mac/Example_MacDialogManager_ShowFileDialog.png" alt="Example_MacDialogManager_ShowFileDialog" width="1000" />
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

### ShowMultiFileDialog - Multi-file picker dialog

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
  <img src="images/mac/Example_MacDialogManager_ShowMultiFileDialog.png" alt="Example_MacDialogManager_ShowMultiFileDialog" width="1000" />
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

### ShowFolderDialog - Folder picker dialog

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
  <img src="images/mac/Example_MacDialogManager_ShowFolderDialog.png" alt="Example_MacDialogManager_ShowFolderDialog" width="1000" />
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

### ShowMultiFolderDialog - Multi-folder picker dialog

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
  <img src="images/mac/Example_MacDialogManager_ShowMultiFolderDialog.png" alt="Example_MacDialogManager_ShowMultiFolderDialog" width="1000" />
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

### ShowSaveFileDialog - Save file dialog

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
  <img src="images/mac/Example_MacDialogManager_ShowSaveFileDialog.png" alt="Example_MacDialogManager_ShowSaveFileDialog" width="600" />
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
