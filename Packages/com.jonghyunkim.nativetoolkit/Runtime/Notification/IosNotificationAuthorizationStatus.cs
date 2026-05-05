#nullable enable

#if UNITY_IOS
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the iOS notification authorization status returned by UNUserNotificationCenter.
    /// </summary>
    public enum IosNotificationAuthorizationStatus
    {
        /// <summary>The user has explicitly authorized the app to send notifications.</summary>
        Authorized,

        /// <summary>The user has explicitly denied the app from sending notifications.</summary>
        Denied,

        /// <summary>The user has not yet been asked for notification authorization.</summary>
        NotDetermined,

        /// <summary>The app is authorized to post non-interruptive notifications provisionally.</summary>
        Provisional,

        /// <summary>The app is authorized to send ephemeral notifications (no recording in Notification Center).</summary>
        Ephemeral,

        /// <summary>The authorization status could not be determined.</summary>
        Unknown
    }

    /// <summary>
    /// Parses native authorization status strings into <see cref="IosNotificationAuthorizationStatus"/> values.
    /// </summary>
    public static class IosNotificationAuthorizationStatusParser
    {
        /// <summary>
        /// Converts a native status string to the corresponding enum value.
        /// </summary>
        /// <param name="status">Native status string from the iOS bridge (e.g. "authorized", "denied").</param>
        /// <returns>The matching <see cref="IosNotificationAuthorizationStatus"/> value.</returns>
        public static IosNotificationAuthorizationStatus Parse(string? status) =>
            status switch
            {
                "authorized"    => IosNotificationAuthorizationStatus.Authorized,
                "denied"        => IosNotificationAuthorizationStatus.Denied,
                "notDetermined" => IosNotificationAuthorizationStatus.NotDetermined,
                "provisional"   => IosNotificationAuthorizationStatus.Provisional,
                "ephemeral"     => IosNotificationAuthorizationStatus.Ephemeral,
                _               => IosNotificationAuthorizationStatus.Unknown
            };
    }
}
#endif
