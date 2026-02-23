# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project aims to follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0]

### Added

- Initial release of **Native Toolkit** for Unity 6+.
- Native dialog APIs for Android / iOS / Windows / macOS.
  - Android: basic/confirm/single-select/multi-select/input/login dialogs.
  - iOS: basic/confirm/destructive/action sheet/input/login dialogs.
  - Windows: basic/file open (single/multi)/folder select (single/multi)/save dialogs.
  - macOS: basic/file open (single/multi)/folder select (single/multi)/save dialogs.
- Sample content (importable via Unity Package Manager Samples) to validate behavior per platform.
- Editor workflows to help integrate native libraries and platform build settings.
  - Android: configure Gradle project and dependencies.
  - iOS: add/embed iOS XCFrameworks into the exported Xcode project.
  - macOS: configure Xcode project and add XCFramework.

### Notes

- Supported OS versions (baseline): Android 12+, iOS 18+, Windows 11+, macOS 15+.
