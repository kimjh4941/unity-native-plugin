using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NativeToolkit.Runtime.Tests")]

// PlayMode tests need the Editor-only IosClipboardManager.ResetForTests seam to isolate the
// lifetime tombstone between tests; without it a single destroy test would reject every later one.
[assembly: InternalsVisibleTo("NativeToolkit.Runtime.PlayModeTests")]
