#nullable enable

using UnityEngine.Scripting.APIUpdating;

namespace JonghyunKim.NativeToolkit.Runtime.Android.Share
{
    /// <summary>
    /// Represents the app-selection result delivered by <c>shareWithCallback</c> after the user
    /// chooses a target application. This result is only fired when the user actively selects an
    /// app; cancelling the chooser or choosing Copy/Edit does not produce this result.
    /// </summary>
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Share", sourceClassName: "ShareCallbackResult")]
    public readonly struct AndroidShareCallbackResult
    {
        /// <summary>
        /// Gets the native operation name (<c>shareWithCallback</c>).
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Gets the package name of the application the user selected, or <c>null</c> when the
        /// package name could not be determined.
        /// </summary>
        public string? SelectedPackageName { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="AndroidShareCallbackResult"/>.
        /// </summary>
        /// <param name="operation">The native operation name.</param>
        /// <param name="selectedPackageName">The package name of the selected application, or <c>null</c>.</param>
        public AndroidShareCallbackResult(string operation, string? selectedPackageName)
        {
            Operation = operation;
            SelectedPackageName = selectedPackageName;
        }
    }
}
