# Unity Native Toolkit (Unity 6)

[English](index.md) | [Korean](index.ko.md) | [Japanese](index.ja.md)

- A toolkit that provides native features for Unity 6+.
- The package includes native plugins and sample scenes for Android/iOS/Windows/macOS, and exposes native features such as dialogs, notifications, and sharing via singleton APIs per platform.
- Editor windows help integrate native libraries and Gradle/Xcode settings, streamlining post-build project setup.

# Version

## 1.8.0

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

- Notification features
  - Immediate notifications
  - Scheduled notifications
  - Progress foreground service
  - Notification actions
  - Full-screen notifications
  - Custom view notifications

- Share features
  - Share text / URL
  - Share image / multiple images
  - Share file / multiple files
  - Custom Chooser actions (API 34+)
  - Rich preview
  - Direct Share target registration
  - Share with callback
  - Cancel pending callback

- Clipboard features
  - Copy plain text / HTML text / URI / multiple text
  - Sensitive-content preview suppression (Android 13+)
  - Read clipboard content / check for a clip / get metadata without touching the clip body
  - Observe clipboard changes
  - Game use cases: invite codes, pasting a code, copying a screenshot

## iOS

- Dialog features
  - Basic dialog
  - Confirmation dialog
  - Destructive dialog
  - Action sheet
  - Text input dialog
  - Login dialog
- Notification features
  - Request permission / check authorization status / open notification settings
  - Immediate notifications (including attachment sample)
  - Scheduled notifications (time interval / calendar / location)
  - Update / cancel / remove delivered / fetch state
  - Badge count management
  - Category registration / actions / text input actions
- Share features
  - Share text / URL / URL with preview
  - Share image / multiple images
  - Share file / multiple files
  - Share text and URL together
  - Share with subject
  - Exclude specific activities from the share sheet

## Windows

- Dialog features
  - Basic dialog
  - File picker dialog
  - Multi-file picker dialog
  - Folder picker dialog
  - Multi-folder picker dialog
  - Save file dialog

- Notification features
  - Immediate notifications
  - Scheduled notifications
  - Cancel scheduled notifications
  - Progress-bar notifications
  - Remove notifications by tag or remove all
  - Query notification permission setting
  - Open system notification settings
  - Notification activation events (including cold-start activation)

## macOS

- Dialog features
  - Basic dialog
  - File picker dialog
  - Multi-file picker dialog
  - Folder picker dialog
  - Multi-folder picker dialog
  - Save file dialog

- Notification features
  - Request permission / check authorization status / open system notification settings
  - Immediate notifications
  - Scheduled notifications (time interval / calendar)
  - Update / cancel / remove delivered notifications
  - Fetch scheduled and delivered notification lists
  - Badge count management
  - Category registration / actions / text input actions

- Share features
  - Share text / URL
  - Share image / multiple images
  - Share file / multiple files
  - Share text and URL together
  - Share excluding services
  - Share via a named service (e.g. Mail)

# Getting Started

## Installation

- Open Unity 6.
- Window -> Package Manager.
- Select "install from Git URL...".
- Enter the Git URL for this package:
  - https://github.com/jonghyunkim/unity-native-plugin.git?path=/Packages/com.jonghyunkim.nativetoolkit#1.8.0
- Click "install".
- Requirements:
  - Unity 6+
  - Dependencies: Localization, Addressables, Input System

## Samples

- Open Unity 6.
- Window -> Package Manager.
- Unity Package Manager -> Native Toolkit -> Samples -> Import.
- Tools -> Native Toolkit -> Sample.
  <p align="center">
    <img src="images/editor/NativeToolkitSample.png" alt="NativeToolkitSample" width="720" />
  </p>

- Android sample
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
  - The sample UI appears in the Game view.
  - From Build Profiles, run "Windows Profile" -> Build.
  - Run "Unity NativeToolkit.exe" from the build output folder.

- macOS sample
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

- [Dialog](dialog.md)
- [Notification](notification.md)
- [Share](share.md)
- [Clipboard](clipboard.md)
