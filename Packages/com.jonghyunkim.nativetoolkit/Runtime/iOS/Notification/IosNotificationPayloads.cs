#nullable enable

using System;

namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    /// <summary>
    /// Represents an iOS notification category that groups related action buttons.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification")]
    public sealed class IosNotificationCategoryPayload
    {
        /// <summary>Unique identifier for the category. Must match the <c>categoryIdentifier</c> in content.</summary>
        public string identifier = string.Empty;

        /// <summary>Standard action buttons associated with this category.</summary>
        public IosNotificationActionPayload[] actions = Array.Empty<IosNotificationActionPayload>();

        /// <summary>Text input action buttons associated with this category.</summary>
        public IosNotificationTextInputActionPayload[] textInputActions = Array.Empty<IosNotificationTextInputActionPayload>();

        /// <summary>
        /// Category behavior options. Valid values: "customDismissAction", "allowInCarPlay",
        /// "hiddenPreviewsShowTitle", "allowAnnouncement".
        /// </summary>
        public string[] options = Array.Empty<string>();
    }

    /// <summary>
    /// Represents a standard action button in an iOS notification category.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification")]
    public sealed class IosNotificationActionPayload
    {
        /// <summary>Unique identifier for the action.</summary>
        public string identifier = string.Empty;

        /// <summary>Display title shown on the action button.</summary>
        public string title = string.Empty;

        /// <summary>Optional SF Symbol name used as the action button icon (iOS 15+).</summary>
        public string? sfSymbolName;

        /// <summary>
        /// Action behavior options. Valid values: "authenticationRequired", "destructive", "foreground".
        /// </summary>
        public string[] options = Array.Empty<string>();
    }

    /// <summary>
    /// Represents a text input action button in an iOS notification category.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification")]
    public sealed class IosNotificationTextInputActionPayload
    {
        /// <summary>Unique identifier for the action.</summary>
        public string identifier = string.Empty;

        /// <summary>Display title shown on the action button.</summary>
        public string title = string.Empty;

        /// <summary>Text displayed on the send button of the inline text input field.</summary>
        public string buttonTitle = string.Empty;

        /// <summary>Placeholder text shown in the text input field before the user types.</summary>
        public string textInputPlaceholder = string.Empty;
    }
}
