# Unity6 C# Coding Rules

> **Sync rule:** This file is the single source of truth.
> Whenever this file is updated, apply the **exact same changes** to
> `.github/instructions/csharp-coding.instructions.md` (keep everything below the frontmatter `---` identical).

## Logging (Debug.Log)

Add `Debug.Log` as the **first line** of every target method, including all parameters.

**Format:**

```csharp
Debug.Log($"[{LogTag}][{nameof(MethodName)}] param1: {param1}, param2: {param2}");
```

Use `Debug.LogError` for errors:

```csharp
Debug.LogError($"[{LogTag}][{nameof(MethodName)}] param1: {param1}, param2: {param2}");
```

**Target:**

- `override` methods
- `public` / `internal` methods
- `operator` overloads
- `MonoBehaviour` event functions (`Awake`, `Start`, `Update`, `OnEnable`, `OnDisable`, etc.)
- Local functions (inside notification / permission / platform bridge code)

**Exclude:**

- `class` / `struct` / `enum` / `interface` declarations
- Private lightweight utilities (simple extension/helper methods)
- Pure UI rendering utilities (called every frame in large volume)
- Locations that already have an equivalent log (do not add duplicates)

**Tag definition:**

```csharp
private const string LogTag = "ClassName";
```

**using:**

```csharp
using UnityEngine;
```

**Example:**

```csharp
private const string LogTag = "SampleClass";

public override void OnApplicationPause(bool pauseStatus)
{
    Debug.Log($"[{LogTag}][{nameof(OnApplicationPause)}] pauseStatus: {pauseStatus}");
    // Existing logic...
}

public Result Send(NotificationCommand command)
{
    Debug.Log($"[{LogTag}][{nameof(Send)}] command: {command}");
    return repository.Send(command);
}
```

---

## XML Documentation Comments (C#)

Add XML doc comments to all `public` methods, classes, interfaces, and properties.
Write them together with the code (not after the fact).

**Required for:**

- `public` methods
- `public class` / `interface` / `struct` / `record` / `enum`
- `public` properties (non-obvious ones)

**Exclude:**

- `private` / `internal` methods
- `override` methods (inherit parent's comment)
- Trivial getters / setters

**Format:**

```csharp
/// <summary>
/// Deletes the channel by id.
/// </summary>
/// <param name="channelId">Target channel id.</param>
/// <returns>Operation result containing an exception on failure.</returns>
public Result DeleteChannel(string channelId)
```

**Rules:**

- Write all comment text (XML comments, line comments, block comments) in English
- Write all user-facing message text (UI text, statusText, Toast, Dialog text) in English
- Keep the first line of `<summary>` as a concise overview
- Document all parameters in `<param>` without omission
- Include `<returns>` only when the return value is non-obvious
- Include `<exception>` when an exception is explicitly thrown
