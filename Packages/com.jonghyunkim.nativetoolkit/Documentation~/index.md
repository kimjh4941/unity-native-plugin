# Native Toolkit (Unity 6)

Cross‑platform native dialogs and editor utilities with localization.

Version: 1.0.0

# Getting Started

## Installation

- Unity Package Manager → Add package from disk (embedded) or Git URL.
- Requirements:
  - Unity 6+
  - Dependencies: Localization, Addressables, Input System

## Quick Setup

1. Import package.
2. (Optional) Import Samples from Package Manager.
3. call a dialog API:

```csharp
using Jonghyunkim.NativeToolkit;

#if UNITY_ANDROID
AndroidDialogManager.Instance.ShowDialog("Title", "Message", "OK");
#endif
```

# Samples

Import from Package Manager → Samples

Contents:

- NativeToolkitExampleScene.unity
- Minimal controllers demonstrating dialogs

## Editor Window

- Tools → Native Toolkit → Example

# API Overview

## Dialog Managers

- AndroidDialogManager
- IosDialogManager
- MacDialogManager
- WindowsDialogManager

### Common Patterns

- Threading: callbacks may arrive off main thread → dispatch to main thread if needed.
- Memory/Interop: managed strings are copied; dispose native resources per platform guidance.

### AndroidDialogManager (example)

```csharp
AndroidDialogManager.Instance.ShowDialog(
  title: "Confirm",
  message: "Proceed?",
  buttonText: "OK"
);
```

See XML docs in source for detailed parameters and callback contracts.

# Troubleshooting

## Localization shows empty

- Ensure SelectedLocale is set and `InitializationOperation.IsDone`.
- Verify keys exist in tables and the correct table name is used.

## Editor scripts not compiling

- Confirm Editor scripts are under Editor/ and in `NativeToolkit.Editor` asmdef.
- Ensure dependencies in asmdef: Unity.Localization, Unity.Localization.Editor, Unity.Addressables, Unity.ResourceManager.

## Android/iOS build errors

- Check native Plugins import settings (platform filters).
- Clean Addressables build if resource GUID/layout changed.
