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
