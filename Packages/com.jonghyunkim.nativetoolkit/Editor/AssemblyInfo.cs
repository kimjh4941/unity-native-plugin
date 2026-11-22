using System.Runtime.CompilerServices;

// EditMode tests reach the build helpers that are internal to the Editor assembly
// (AndroidGradleDependencies.Patch and its list, IosXcframeworkPin).
[assembly: InternalsVisibleTo("NativeToolkit.Runtime.Tests")]

// The iOS build steps read Plugins/iOS/VERSION.txt through IosXcframeworkPin.
[assembly: InternalsVisibleTo("NativeToolkit.BuildProcessors.Editor")]
[assembly: InternalsVisibleTo("NativeToolkit.BuildTools.Editor")]
