# Native Toolkit (Unity 6)

Native Toolkit is a Unity 6+ package that provides native dialog APIs for Android, iOS, Windows, and macOS.
It also includes editor utilities to help apply native project settings after export.

## Version

- 1.0.0

## Features

- Cross-platform dialog APIs
  - Android: basic/confirm/single-choice/multi-choice/input/login dialogs
  - iOS: basic/confirm/destructive/action-sheet/input/login dialogs
  - Windows: basic/file/folder/save dialogs
  - macOS: basic/file/folder/save dialogs
- Editor workflows
  - Android: configure Gradle project and Kotlin dependencies
  - iOS: add/embed XCFrameworks in exported Xcode project
  - macOS: add XCFramework in exported Xcode project
- Sample scene available via Package Manager Samples

## Requirements

- Unity 6+
- Dependencies
  - `com.unity.localization`
  - `com.unity.addressables`
  - `com.unity.inputsystem`

## Installation (UPM Git URL)

Use Unity Package Manager > `Install package from git URL...`

```text
https://github.com/jonghyunkim/unity-native-plugin.git?path=/Packages/com.jonghyunkim.nativetoolkit#1.0.0
```

## Quick Start

1. Install this package from Git URL.
2. Import Samples from Package Manager (`Native Toolkit Example`).
3. Open `Tools > Native Toolkit > Example`.
4. Build/export per platform and run the matching configure tool from `Tools > Native Toolkit`.

## Documentation

- English: `Documentation~/index.md`
- Japanese: `Documentation~/index.ja.md`
- Korean: `Documentation~/index.ko.md`

## License

Apache-2.0
