# Unity Native Toolkit (Unity 6)

- A toolkit that provides native features for Unity 6+.
- The package includes native plugins and sample scenes for Android/iOS/Windows/macOS, and exposes dialog operations via singleton APIs per platform.
- Editor windows help integrate native libraries and Gradle/Xcode settings, streamlining post-build setup.

Other languages:

- Korean: [README.ko.md](README.ko.md)
- Japanese: [README.ja.md](README.ja.md)

## Version

- 1.0.0

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

### iOS

- Dialog features
  - Basic dialog
  - Confirmation dialog
  - Destructive dialog
  - Action sheet
  - Text input dialog
  - Login dialog

### Windows

- Dialog features
  - Basic dialog
  - File picker dialog
  - Multi-file picker dialog
  - Folder picker dialog
  - Multi-folder picker dialog
  - Save file dialog

### macOS

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

## Detailed Documentation

- See the package documentation:
  - [English documentation](docs/latest/index.md)
  - [Korean documentation](docs/latest/index.ko.md)
  - [Japanese documentation](docs/latest/index.ja.md)

## Native Toolkit

- Native Toolkit is a unified toolkit for platform-native features.
- It includes native plugins and samples for Android / iOS / Windows / macOS, exposing dialog APIs via a singleton interface.
- Repository: https://github.com/kimjh4941/native-toolkit

## License

Apache License 2.0. See `LICENSE`.
