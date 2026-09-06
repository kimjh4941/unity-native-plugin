#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these types carry
// values detected in clipboard content, which may hold personal data. They are pure value
// constructors and emit no logs at all. The Manager already logs operation, success and error
// code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>A web link found on the pasteboard.</summary>
    public sealed class MacClipboardDetectedLink
    {
        /// <summary>The text exactly as it appeared on the pasteboard.</summary>
        public string MatchedString { get; }

        /// <summary>Absolute URL string. Not re-encoded or normalised.</summary>
        public string Url { get; }

        internal MacClipboardDetectedLink(string matchedString, string url)
        {
            MatchedString = matchedString;
            Url = url;
        }
    }

    /// <summary>
    /// A detected value that carries an optional label, shared by phone numbers and email
    /// addresses. Both have the same shape in the native payload.
    /// </summary>
    public sealed class MacClipboardLabeledValue
    {
        /// <summary>The text exactly as it appeared on the pasteboard.</summary>
        public string MatchedString { get; }

        /// <summary>The value in the form the detector produced.</summary>
        public string Value { get; }

        /// <summary>Label the detector attached, such as a contact field name. Often absent.</summary>
        public string? Label { get; }

        internal MacClipboardLabeledValue(string matchedString, string value, string? label)
        {
            MatchedString = matchedString;
            Value = value;
            Label = label;
        }
    }

    /// <summary>A postal address found on the pasteboard. Every component is optional.</summary>
    public sealed class MacClipboardPostalAddress
    {
        /// <summary>The text exactly as it appeared on the pasteboard.</summary>
        public string MatchedString { get; }

        /// <summary>Street line, when the address included one.</summary>
        public string? Street { get; }

        /// <summary>City, when the address included one.</summary>
        public string? City { get; }

        /// <summary>State or region, when the address included one.</summary>
        public string? State { get; }

        /// <summary>Postal code, when the address included one.</summary>
        public string? PostalCode { get; }

        /// <summary>Country, when the address included one.</summary>
        public string? Country { get; }

        internal MacClipboardPostalAddress(
            string matchedString, string? street, string? city, string? state, string? postalCode, string? country)
        {
            MatchedString = matchedString;
            Street = street;
            City = city;
            State = state;
            PostalCode = postalCode;
            Country = country;
        }
    }

    /// <summary>A calendar event found on the pasteboard.</summary>
    public sealed class MacClipboardCalendarEvent
    {
        /// <summary>The text exactly as it appeared on the pasteboard.</summary>
        public string MatchedString { get; }

        /// <summary>Whether the event covers whole days rather than a time range.</summary>
        public bool IsAllDay { get; }

        /// <summary>Start instant, when the event had one. Parsed from ISO 8601 in UTC.</summary>
        public DateTimeOffset? StartDate { get; }

        /// <summary>Time zone identifier such as <c>Asia/Tokyo</c>, never a localized name.</summary>
        public string? StartTimeZoneIdentifier { get; }

        /// <summary>End instant, when the event had one.</summary>
        public DateTimeOffset? EndDate { get; }

        /// <summary>Time zone identifier for the end, when the event had one.</summary>
        public string? EndTimeZoneIdentifier { get; }

        internal MacClipboardCalendarEvent(
            string matchedString, bool isAllDay,
            DateTimeOffset? startDate, string? startTimeZoneIdentifier,
            DateTimeOffset? endDate, string? endTimeZoneIdentifier)
        {
            MatchedString = matchedString;
            IsAllDay = isAllDay;
            StartDate = startDate;
            StartTimeZoneIdentifier = startTimeZoneIdentifier;
            EndDate = endDate;
            EndTimeZoneIdentifier = endTimeZoneIdentifier;
        }
    }

    /// <summary>A parcel tracking number found on the pasteboard.</summary>
    public sealed class MacClipboardShipmentTracking
    {
        /// <summary>The text exactly as it appeared on the pasteboard.</summary>
        public string MatchedString { get; }

        /// <summary>Carrier name as reported by the detector.</summary>
        public string Carrier { get; }

        /// <summary>Tracking number as reported by the detector.</summary>
        public string TrackingNumber { get; }

        internal MacClipboardShipmentTracking(string matchedString, string carrier, string trackingNumber)
        {
            MatchedString = matchedString;
            Carrier = carrier;
            TrackingNumber = trackingNumber;
        }
    }

    /// <summary>A flight number found on the pasteboard.</summary>
    public sealed class MacClipboardFlightNumber
    {
        /// <summary>The text exactly as it appeared on the pasteboard.</summary>
        public string MatchedString { get; }

        /// <summary>Airline code as reported by the detector.</summary>
        public string Airline { get; }

        /// <summary>Flight number as reported by the detector.</summary>
        public string FlightNumber { get; }

        internal MacClipboardFlightNumber(string matchedString, string airline, string flightNumber)
        {
            MatchedString = matchedString;
            Airline = airline;
            FlightNumber = flightNumber;
        }
    }

    /// <summary>An amount of money found on the pasteboard.</summary>
    public sealed class MacClipboardMoneyAmount
    {
        /// <summary>The text exactly as it appeared on the pasteboard.</summary>
        public string MatchedString { get; }

        /// <summary>ISO currency code, for example <c>USD</c>.</summary>
        public string CurrencyCode { get; }

        /// <summary>Numeric amount, unformatted.</summary>
        public double Amount { get; }

        internal MacClipboardMoneyAmount(string matchedString, string currencyCode, double amount)
        {
            MatchedString = matchedString;
            CurrencyCode = currencyCode;
            Amount = amount;
        }
    }

    /// <summary>
    /// Everything the detection system found.
    /// <para>
    /// The three scalar properties are nullable because the native layer writes them as explicit
    /// nulls: null means "requested and not found", which is different from "not requested". The
    /// nine collections are never null; an empty one means nothing of that kind was found.
    /// </para>
    /// </summary>
    public sealed class MacClipboardDetectedValues
    {
        /// <summary>Patterns the system actually matched. Never null.</summary>
        public IReadOnlyList<MacClipboardDetectionPattern> Patterns { get; }

        /// <summary>Matched web URL, or <c>null</c> when the pattern did not match.</summary>
        public string? ProbableWebUrl { get; }

        /// <summary>Matched search term, or <c>null</c> when the pattern did not match.</summary>
        public string? ProbableWebSearch { get; }

        /// <summary>Matched number, or <c>null</c> when the pattern did not match.</summary>
        public double? Number { get; }

        /// <summary>Matched links. Never null.</summary>
        public IReadOnlyList<MacClipboardDetectedLink> Links { get; }

        /// <summary>Matched phone numbers. Never null.</summary>
        public IReadOnlyList<MacClipboardLabeledValue> PhoneNumbers { get; }

        /// <summary>Matched email addresses. Never null.</summary>
        public IReadOnlyList<MacClipboardLabeledValue> EmailAddresses { get; }

        /// <summary>Matched postal addresses. Never null.</summary>
        public IReadOnlyList<MacClipboardPostalAddress> PostalAddresses { get; }

        /// <summary>Matched calendar events. Never null.</summary>
        public IReadOnlyList<MacClipboardCalendarEvent> CalendarEvents { get; }

        /// <summary>Matched tracking numbers. Never null.</summary>
        public IReadOnlyList<MacClipboardShipmentTracking> ShipmentTrackingNumbers { get; }

        /// <summary>Matched flight numbers. Never null.</summary>
        public IReadOnlyList<MacClipboardFlightNumber> FlightNumbers { get; }

        /// <summary>Matched money amounts. Never null.</summary>
        public IReadOnlyList<MacClipboardMoneyAmount> MoneyAmounts { get; }

        internal MacClipboardDetectedValues(
            IReadOnlyList<MacClipboardDetectionPattern> patterns,
            string? probableWebUrl,
            string? probableWebSearch,
            double? number,
            IReadOnlyList<MacClipboardDetectedLink> links,
            IReadOnlyList<MacClipboardLabeledValue> phoneNumbers,
            IReadOnlyList<MacClipboardLabeledValue> emailAddresses,
            IReadOnlyList<MacClipboardPostalAddress> postalAddresses,
            IReadOnlyList<MacClipboardCalendarEvent> calendarEvents,
            IReadOnlyList<MacClipboardShipmentTracking> shipmentTrackingNumbers,
            IReadOnlyList<MacClipboardFlightNumber> flightNumbers,
            IReadOnlyList<MacClipboardMoneyAmount> moneyAmounts)
        {
            Patterns = patterns;
            ProbableWebUrl = probableWebUrl;
            ProbableWebSearch = probableWebSearch;
            Number = number;
            Links = links;
            PhoneNumbers = phoneNumbers;
            EmailAddresses = emailAddresses;
            PostalAddresses = postalAddresses;
            CalendarEvents = calendarEvents;
            ShipmentTrackingNumbers = shipmentTrackingNumbers;
            FlightNumbers = flightNumbers;
            MoneyAmounts = moneyAmounts;
        }
    }

    /// <summary>Metadata the detection system reported without reading the contents.</summary>
    public sealed class MacClipboardDetectedMetadata
    {
        /// <summary>Metadata types the system reported. Never null.</summary>
        public IReadOnlyList<MacClipboardMetadataType> MetadataTypes { get; }

        /// <summary>Content type of a file reference, or <c>null</c> when there was none.</summary>
        public string? ContentTypeIdentifier { get; }

        internal MacClipboardDetectedMetadata(
            IReadOnlyList<MacClipboardMetadataType> metadataTypes, string? contentTypeIdentifier)
        {
            MetadataTypes = metadataTypes;
            ContentTypeIdentifier = contentTypeIdentifier;
        }
    }

    /// <summary>
    /// Result of asking which patterns the pasteboard matches, without reading the values.
    /// <para>
    /// <see cref="Patterns"/> is the one collection a result type holds directly, so it is never
    /// null even on failure. Requires macOS 15.4; below that the call fails with
    /// <see cref="MacClipboardErrorCodes.DetectionUnavailable"/>.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardDetectedPatternsResult
    {
        private readonly IReadOnlyList<MacClipboardDetectionPattern>? _patterns;

        /// <summary>Whether the detection succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>Patterns that matched. Never null; empty on failure or when nothing matched.</summary>
        public IReadOnlyList<MacClipboardDetectionPattern> Patterns =>
            _patterns ?? Array.Empty<MacClipboardDetectionPattern>();

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="patterns">Patterns that matched.</param>
        /// <returns>A successful <see cref="MacClipboardDetectedPatternsResult"/>.</returns>
        public static MacClipboardDetectedPatternsResult Success(
            IReadOnlyList<MacClipboardDetectionPattern> patterns) => new(true, null, patterns);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardDetectedPatternsResult"/>.</returns>
        public static MacClipboardDetectedPatternsResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), null);

        private MacClipboardDetectedPatternsResult(
            bool isSuccess, MacClipboardErrorInfo? error, IReadOnlyList<MacClipboardDetectionPattern>? patterns)
        {
            IsSuccess = isSuccess;
            Error = error;
            _patterns = patterns;
        }
    }

    /// <summary>
    /// Result of reading the detected values themselves.
    /// <para>
    /// This reads the contents. The system tells the person using the app on a match and can deny
    /// access, reported as <see cref="MacClipboardErrorCodes.DetectionDenied"/>. Call it from a
    /// user action. Requires macOS 15.4.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardDetectedValuesResult
    {
        /// <summary>Whether the detection succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>Detected values. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public MacClipboardDetectedValues? Values { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="values">Values reported by the detector.</param>
        /// <returns>A successful <see cref="MacClipboardDetectedValuesResult"/>.</returns>
        public static MacClipboardDetectedValuesResult Success(MacClipboardDetectedValues values) =>
            new(true, null, values);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardDetectedValuesResult"/>.</returns>
        public static MacClipboardDetectedValuesResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), null);

        private MacClipboardDetectedValuesResult(
            bool isSuccess, MacClipboardErrorInfo? error, MacClipboardDetectedValues? values)
        {
            IsSuccess = isSuccess;
            Error = error;
            Values = values;
        }
    }

    /// <summary>
    /// Result of reading pasteboard metadata.
    /// <para>
    /// Fails for content the system cannot describe, <b>which includes plain text</b>. "Nothing to
    /// report" and "could not report" are not distinguishable. Requires macOS 15.4.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardDetectedMetadataResult
    {
        /// <summary>Whether the query succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>Metadata. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public MacClipboardDetectedMetadata? Metadata { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="metadata">Metadata reported by the detector.</param>
        /// <returns>A successful <see cref="MacClipboardDetectedMetadataResult"/>.</returns>
        public static MacClipboardDetectedMetadataResult Success(MacClipboardDetectedMetadata metadata) =>
            new(true, null, metadata);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardDetectedMetadataResult"/>.</returns>
        public static MacClipboardDetectedMetadataResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), null);

        private MacClipboardDetectedMetadataResult(
            bool isSuccess, MacClipboardErrorInfo? error, MacClipboardDetectedMetadata? metadata)
        {
            IsSuccess = isSuccess;
            Error = error;
            Metadata = metadata;
        }
    }
}
#endif
