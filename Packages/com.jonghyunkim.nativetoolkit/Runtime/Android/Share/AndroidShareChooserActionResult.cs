#nullable enable

using UnityEngine.Scripting.APIUpdating;

namespace JonghyunKim.NativeToolkit.Runtime.Android.Share
{
    /// <summary>
    /// Represents a custom chooser action tap delivered by the Android Sharesheet (API 34+).
    /// This type is platform-agnostic and can be used in EditMode tests without Android dependencies.
    /// </summary>
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Share", sourceClassName: "ShareChooserActionResult")]
    public readonly struct AndroidShareChooserActionResult
    {
        /// <summary>
        /// Gets the intentAction string of the tapped chooser action. Never null; a null value from
        /// the native layer is normalized to <see cref="string.Empty"/>.
        /// </summary>
        public string ActionId { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="AndroidShareChooserActionResult"/>.
        /// </summary>
        /// <param name="actionId">
        /// The intentAction string of the tapped chooser action. Null is normalized to
        /// <see cref="string.Empty"/> so callers can always treat <see cref="ActionId"/> as
        /// non-null.
        /// </param>
        public AndroidShareChooserActionResult(string actionId)
            => ActionId = actionId ?? string.Empty;
    }
}
