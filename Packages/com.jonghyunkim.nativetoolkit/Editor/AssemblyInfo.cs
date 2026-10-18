using System.Runtime.CompilerServices;

// EditMode tests reach the build helpers that are internal to the Editor assembly
// (AndroidGradleDependencies.Patch and its list).
[assembly: InternalsVisibleTo("NativeToolkit.Runtime.Tests")]
