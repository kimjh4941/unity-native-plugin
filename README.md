# Unity Native Toolkit (Unity 6)

- A toolkit that provides native features for Unity 6+.
- The package includes native plugins and sample scenes for Android/iOS/Windows/macOS, and exposes native features such as dialogs, notifications, and sharing via singleton APIs per platform.
- Editor windows help integrate native libraries and Gradle/Xcode settings, streamlining post-build setup.

Other languages:

- Korean: [README.ko.md](README.ko.md)
- Japanese: [README.ja.md](README.ja.md)

## Version

- 1.6.0

## Supported OS Versions

- Android 12+
- iOS 18+
- Windows 11+
- macOS 15+

## Features

### Android

- Dialog features
  - Basic dialog
  - Confirmation dialog
  - Single choice dialog
  - Multi choice dialog
  - Text input dialog
  - Login dialog
- Notification features
  - Standard notifications (show / update / cancel)
  - Scheduled notifications (schedule / status / cancel)
  - Action notifications
  - Full-screen notifications
  - DecoratedCustomView notifications
  - Progress notifications (Foreground Service)
- Share features
  - Share text / URL
  - Share image / multiple images
  - Share file / multiple files
  - Custom Chooser actions (API 34+)
  - Rich preview
  - Direct Share target registration
  - Share with callback
  - Cancel pending callback

### iOS

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

### Windows

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

### macOS

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

## Planned Features

- Clipboard integration

## Installation

- Open Unity 6.
- Window -> Package Manager.
- Select "install from Git URL...".
- Enter the Git URL for this package:
  - https://github.com/jonghyunkim/unity-native-plugin.git?path=/Packages/com.jonghyunkim.nativetoolkit#1.6.0
- Click "install".
- Requirements:
  - Unity 6+
  - Dependencies: Localization, Addressables, Input System

## Samples

- Open Unity 6.
- Window -> Package Manager.
- Unity Package Manager -> Native Toolkit -> Samples -> Import.
- Tools -> Native Toolkit -> Example.

## Detailed Documentation

- See the package documentation:
  - [English documentation](docs/latest/index.md)
  - [Korean documentation](docs/latest/index.ko.md)
  - [Japanese documentation](docs/latest/index.ja.md)

## Native Toolkit

- Native Toolkit is a unified toolkit for platform-native features.
- It includes native plugins and samples for Android / iOS / Windows / macOS, exposing native features such as dialogs, notifications, and sharing via singleton APIs per platform.
- Repository: https://github.com/kimjh4941/native-toolkit

## License

Apache License 2.0. See `LICENSE`.
