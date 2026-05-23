#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents a macOS notification category that groups related action buttons.
    /// macOS uses "id" key (not "identifier" as iOS does).
    /// </summary>
    [Serializable]
    public sealed class MacNotificationCategoryPayload
    {
        /// <summary>Unique identifier for the category. Must match the categoryIdentifier in content.</summary>
        public string id = string.Empty;

        /// <summary>Action buttons associated with this category.</summary>
        public MacNotificationActionPayload[] actions = Array.Empty<MacNotificationActionPayload>();
    }

    /// <summary>
    /// Represents an action button in a macOS notification category.
    /// </summary>
    [Serializable]
    public sealed class MacNotificationActionPayload
    {
        /// <summary>Unique identifier for the action.</summary>
        public string id = string.Empty;

        /// <summary>Display title shown on the action button.</summary>
        public string title = string.Empty;

        /// <summary>Whether tapping the action brings the app to the foreground.</summary>
        public bool isForeground = false;

        /// <summary>Whether this action presents a text input field.</summary>
        public bool isTextInput = false;

        /// <summary>Placeholder text shown in the text input field. Used only when isTextInput is true.</summary>
        public string? textInputPlaceholder;
    }
}
#endif
