#nullable enable

namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Represents a custom chooser action tap delivered by the Android Sharesheet (API 34+).
    /// This type is platform-agnostic and can be used in EditMode tests without Android dependencies.
    /// </summary>
    public readonly struct ShareChooserActionResult
    {
        /// <summary>
        /// Gets the intentAction string of the tapped chooser action. Never null; a null value from
        /// the native layer is normalized to <see cref="string.Empty"/>.
        /// </summary>
        public string ActionId { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ShareChooserActionResult"/>.
        /// </summary>
        /// <param name="actionId">
        /// The intentAction string of the tapped chooser action. Null is normalized to
        /// <see cref="string.Empty"/> so callers can always treat <see cref="ActionId"/> as
        /// non-null.
        /// </param>
        public ShareChooserActionResult(string actionId)
            => ActionId = actionId ?? string.Empty;
    }
}
