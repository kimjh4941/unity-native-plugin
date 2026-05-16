#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the macOS notification authorization status returned by UNUserNotificationCenter.
    /// </summary>
    public enum MacNotificationAuthorizationStatus
    {
        /// <summary>The user has explicitly authorized the app to send notifications.</summary>
        Authorized,

        /// <summary>The user has explicitly denied the app from sending notifications.</summary>
        Denied,

        /// <summary>The user has not yet been asked for notification authorization.</summary>
        NotDetermined,

        /// <summary>The app is authorized to post non-interruptive notifications provisionally.</summary>
        Provisional,

        /// <summary>The authorization status could not be determined or is not supported.</summary>
        Unsupported
    }

    /// <summary>
    /// Parses native authorization status strings into <see cref="MacNotificationAuthorizationStatus"/> values.
    /// </summary>
    public static class MacNotificationAuthorizationStatusParser
    {
        /// <summary>
        /// Extracts the status string from the authorization status JSON and converts it to the enum.
        /// Expects JSON shape: { "status": "authorized" }.
        /// </summary>
        /// <param name="json">JSON string from the macOS bridge.</param>
        /// <returns>The matching enum value, or <see cref="MacNotificationAuthorizationStatus.Unsupported"/> for unknown values.</returns>
        public static MacNotificationAuthorizationStatus ParseJson(string? json)
        {
            if (string.IsNullOrEmpty(json)) return MacNotificationAuthorizationStatus.Unsupported;

            int statusIndex = json.IndexOf("\"status\"", System.StringComparison.Ordinal);
            if (statusIndex < 0) return MacNotificationAuthorizationStatus.Unsupported;

            int colonIndex = json.IndexOf(':', statusIndex);
            if (colonIndex < 0) return MacNotificationAuthorizationStatus.Unsupported;

            int quoteStart = json.IndexOf('"', colonIndex + 1);
            if (quoteStart < 0) return MacNotificationAuthorizationStatus.Unsupported;

            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return MacNotificationAuthorizationStatus.Unsupported;

            string status = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            return Parse(status);
        }

        /// <summary>
        /// Converts a native status string to the corresponding enum value.
        /// </summary>
        public static MacNotificationAuthorizationStatus Parse(string? status) =>
            status switch
            {
                "authorized"    => MacNotificationAuthorizationStatus.Authorized,
                "denied"        => MacNotificationAuthorizationStatus.Denied,
                "notDetermined" => MacNotificationAuthorizationStatus.NotDetermined,
                "provisional"   => MacNotificationAuthorizationStatus.Provisional,
                "unsupported"   => MacNotificationAuthorizationStatus.Unsupported,
                _               => MacNotificationAuthorizationStatus.Unsupported
            };
    }
}
#endif
