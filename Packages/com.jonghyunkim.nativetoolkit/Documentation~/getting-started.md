# Getting Started

## Installation

- Unity Package Manager → Add package from disk (embedded) or Git URL.
- Requirements:
  - Unity 6+
  - Dependencies: Localization, Addressables, Input System

## Quick Setup

1. Import package.
2. (Optional) Import Samples from Package Manager.
3. Open the sample scene or call a dialog API:

```csharp
using Jonghyunkim.NativeToolkit;

#if UNITY_ANDROID
AndroidDialogManager.Instance.ShowDialog("Title", "Message", "OK");
#endif
```

## Editor Window

- Tools → Native Toolkit → Example
