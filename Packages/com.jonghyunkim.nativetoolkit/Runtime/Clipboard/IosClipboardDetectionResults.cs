#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>A detected value with an optional label (for example an email address or phone number).</summary>
    public sealed class IosClipboardLabeledValue
    {
        /// <summary>The detected value. Never null.</summary>
        public string Value { get; }

        /// <summary>Optional label describing the value, or <c>null</c>.</summary>
        public string? Label { get; }

        internal IosClipboardLabeledValue(string value, string? label)
        {
            Value = value;
            Label = label;
        }
    }

    /// <summary>A detected postal address. Every component is optional.</summary>
    public sealed class IosClipboardPostalAddress
    {
        /// <summary>Street, or <c>null</c>.</summary>
        public string? Street { get; }

        /// <summary>City, or <c>null</c>.</summary>
        public string? City { get; }

        /// <summary>State, or <c>null</c>.</summary>
        public string? State { get; }

        /// <summary>Postal code, or <c>null</c>.</summary>
        public string? PostalCode { get; }

        /// <summary>Country, or <c>null</c>.</summary>
        public string? Country { get; }

        internal IosClipboardPostalAddress(
            string? street,
            string? city,
            string? state,
            string? postalCode,
            string? country)
        {
            Street = street;
            City = city;
            State = state;
            PostalCode = postalCode;
            Country = country;
        }
    }

    /// <summary>A detected calendar event.</summary>
    public sealed class IosClipboardCalendarEvent
    {
        /// <summary>Start date, or <c>null</c> when absent or unparsable.</summary>
        public DateTimeOffset? StartDate { get; }

        /// <summary>End date, or <c>null</c> when absent or unparsable.</summary>
        public DateTimeOffset? EndDate { get; }

        /// <summary>Start time zone identifier, or <c>null</c>.</summary>
        public string? StartTimeZone { get; }

        /// <summary>End time zone identifier, or <c>null</c>.</summary>
        public string? EndTimeZone { get; }

        /// <summary>Whether the event spans whole days.</summary>
        public bool IsAllDay { get; }

        internal IosClipboardCalendarEvent(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? startTimeZone,
            string? endTimeZone,
            bool isAllDay)
        {
            StartDate = startDate;
            EndDate = endDate;
            StartTimeZone = startTimeZone;
            EndTimeZone = endTimeZone;
            IsAllDay = isAllDay;
        }
    }

    /// <summary>A detected flight number.</summary>
    public sealed class IosClipboardFlightNumber
    {
        /// <summary>Airline code. Never null.</summary>
        public string Airline { get; }

        /// <summary>Flight number. Never null.</summary>
        public string FlightNumber { get; }

        internal IosClipboardFlightNumber(string airline, string flightNumber)
        {
            Airline = airline;
            FlightNumber = flightNumber;
        }
    }

    /// <summary>A detected money amount.</summary>
    public sealed class IosClipboardMoneyAmount
    {
        /// <summary>Amount. The native model types this as a double, so it is always a JSON number.</summary>
        public double Amount { get; }

        /// <summary>Currency code. Never null.</summary>
        public string Currency { get; }

        internal IosClipboardMoneyAmount(double amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }
    }

    /// <summary>A detected shipment tracking number.</summary>
    public sealed class IosClipboardShipmentTracking
    {
        /// <summary>Carrier name. Never null.</summary>
        public string Carrier { get; }

        /// <summary>Tracking number. Never null.</summary>
        public string TrackingNumber { get; }

        internal IosClipboardShipmentTracking(string carrier, string trackingNumber)
        {
            Carrier = carrier;
            TrackingNumber = trackingNumber;
        }
    }

    /// <summary>
    /// Full set of values the data detection system identified on the pasteboard.
    /// Every collection is non-null; an absent or malformed entry is skipped rather than failing
    /// the whole result, because detection is inherently best-effort.
    /// </summary>
    public sealed class IosClipboardDetectedValues
    {
        /// <summary>Patterns that were detected. Never null.</summary>
        public IReadOnlyList<IosClipboardDetectionPattern> DetectedPatterns { get; }

        /// <summary>Probable web URL, or <c>null</c> when the pattern was not detected.</summary>
        public string? ProbableWebUrl { get; }

        /// <summary>Probable web search term, or <c>null</c> when the pattern was not detected.</summary>
        public string? ProbableWebSearch { get; }

        /// <summary>Detected number, or <c>null</c> when the pattern was not detected.</summary>
        public double? Number { get; }

        /// <summary>Detected links. Never null.</summary>
        public IReadOnlyList<string> Links { get; }

        /// <summary>Detected email addresses. Never null.</summary>
        public IReadOnlyList<IosClipboardLabeledValue> EmailAddresses { get; }

        /// <summary>Detected phone numbers. Never null.</summary>
        public IReadOnlyList<IosClipboardLabeledValue> PhoneNumbers { get; }

        /// <summary>Detected postal addresses. Never null.</summary>
        public IReadOnlyList<IosClipboardPostalAddress> PostalAddresses { get; }

        /// <summary>Detected calendar events. Never null.</summary>
        public IReadOnlyList<IosClipboardCalendarEvent> CalendarEvents { get; }

        /// <summary>Detected flight numbers. Never null.</summary>
        public IReadOnlyList<IosClipboardFlightNumber> FlightNumbers { get; }

        /// <summary>Detected money amounts. Never null.</summary>
        public IReadOnlyList<IosClipboardMoneyAmount> MoneyAmounts { get; }

        /// <summary>Detected shipment tracking numbers. Never null.</summary>
        public IReadOnlyList<IosClipboardShipmentTracking> ShipmentTrackingNumbers { get; }

        internal IosClipboardDetectedValues(
            IReadOnlyList<IosClipboardDetectionPattern> detectedPatterns,
            string? probableWebUrl,
            string? probableWebSearch,
            double? number,
            IReadOnlyList<string> links,
            IReadOnlyList<IosClipboardLabeledValue> emailAddresses,
            IReadOnlyList<IosClipboardLabeledValue> phoneNumbers,
            IReadOnlyList<IosClipboardPostalAddress> postalAddresses,
            IReadOnlyList<IosClipboardCalendarEvent> calendarEvents,
            IReadOnlyList<IosClipboardFlightNumber> flightNumbers,
            IReadOnlyList<IosClipboardMoneyAmount> moneyAmounts,
            IReadOnlyList<IosClipboardShipmentTracking> shipmentTrackingNumbers)
        {
            DetectedPatterns = detectedPatterns;
            ProbableWebUrl = probableWebUrl;
            ProbableWebSearch = probableWebSearch;
            Number = number;
            Links = links;
            EmailAddresses = emailAddresses;
            PhoneNumbers = phoneNumbers;
            PostalAddresses = postalAddresses;
            CalendarEvents = calendarEvents;
            FlightNumbers = flightNumbers;
            MoneyAmounts = moneyAmounts;
            ShipmentTrackingNumbers = shipmentTrackingNumbers;
        }
    }

    /// <summary>
    /// Result of <see cref="IosClipboardManager.DetectPatterns"/>: which patterns are present,
    /// without reading their matched values.
    /// </summary>
    public readonly struct IosClipboardDetectedPatternsResult
    {
        /// <summary>Whether detection succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>
        /// Detected patterns. Never null; empty on failure. Raw values this version does not know
        /// are skipped rather than failing the result, so a newer native layer stays compatible.
        /// </summary>
        public IReadOnlyList<IosClipboardDetectionPattern> Patterns { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="patterns">Patterns reported by the native layer.</param>
        /// <returns>A successful <see cref="IosClipboardDetectedPatternsResult"/>.</returns>
        internal static IosClipboardDetectedPatternsResult Success(IReadOnlyList<IosClipboardDetectionPattern> patterns) =>
            new(true, null, patterns);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>A failed <see cref="IosClipboardDetectedPatternsResult"/>.</returns>
        internal static IosClipboardDetectedPatternsResult Failure(string? errorCode, string? errorMessage) =>
            Failure(IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed result from an already-built error info.
        /// </summary>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosClipboardDetectedPatternsResult"/>.</returns>
        internal static IosClipboardDetectedPatternsResult Failure(IosClipboardErrorInfo error) =>
            new(false, error, Array.Empty<IosClipboardDetectionPattern>());

        private IosClipboardDetectedPatternsResult(
            bool isSuccess,
            IosClipboardErrorInfo? error,
            IReadOnlyList<IosClipboardDetectionPattern> patterns)
        {
            IsSuccess = isSuccess;
            Error = error;
            Patterns = patterns;
        }
    }

    /// <summary>
    /// Result of <see cref="IosClipboardManager.DetectValues"/>: detected patterns together with
    /// their matched values.
    /// </summary>
    public readonly struct IosClipboardDetectedValuesResult
    {
        /// <summary>Whether detection succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>Detected values. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public IosClipboardDetectedValues? Values { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="values">Detected values.</param>
        /// <returns>A successful <see cref="IosClipboardDetectedValuesResult"/>.</returns>
        internal static IosClipboardDetectedValuesResult Success(IosClipboardDetectedValues values) =>
            new(true, null, values);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>A failed <see cref="IosClipboardDetectedValuesResult"/>.</returns>
        internal static IosClipboardDetectedValuesResult Failure(string? errorCode, string? errorMessage) =>
            Failure(IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed result from an already-built error info.
        /// </summary>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosClipboardDetectedValuesResult"/>.</returns>
        internal static IosClipboardDetectedValuesResult Failure(IosClipboardErrorInfo error) =>
            new(false, error, null);

        private IosClipboardDetectedValuesResult(
            bool isSuccess,
            IosClipboardErrorInfo? error,
            IosClipboardDetectedValues? values)
        {
            IsSuccess = isSuccess;
            Error = error;
            Values = values;
        }
    }
}
#endif
