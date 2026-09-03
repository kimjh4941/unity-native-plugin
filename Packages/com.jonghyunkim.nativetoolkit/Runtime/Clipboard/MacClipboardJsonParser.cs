#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Turns the macOS clipboard bridge responses into the public result payloads.
    /// <para>
    /// Every method returns <c>false</c> rather than throwing. A response the native layer reported
    /// as a success but that cannot be read is a failure to the caller
    /// (<see cref="MacClipboardErrorCodes.ResponseParseFailed"/>): reading through a broken
    /// response would hand back a half-built value that looks legitimate.
    /// </para>
    /// <para>
    /// Two shapes of "absent" are distinguished, because the native side uses both deliberately:
    /// an explicit <c>null</c> means "asked for and not found", while a missing key means the
    /// response does not match the schema. The one exception is <c>scope.name</c>, which Swift's
    /// synthesised encoder omits for the general pasteboard.
    /// </para>
    /// <para>
    /// Intentional deviation from the "log every internal method" rule in csharp.md: these methods
    /// receive clipboard content, which may hold passwords or tokens. Nothing is logged here.
    /// </para>
    /// </summary>
    internal static class MacClipboardJsonParser
    {
        /// <summary>Parses <c>OwnershipJson</c>, returned by copy and append.</summary>
        internal static bool TryParseOwnership(string? json, out MacPasteboardOwnership? ownership)
        {
            ownership = null;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            if (root == null) { return false; }

            if (!TryReadScopeMember(root, "scope", out MacPasteboardScope? scope)) { return false; }
            if (!TryReadInt64(root, "changeCount", out long changeCount)) { return false; }

            ownership = new MacPasteboardOwnership(scope!, changeCount);
            return true;
        }

        /// <summary>Parses <c>ScopeResultJson</c>, returned by createPasteboard.</summary>
        internal static bool TryParseScopeResult(string? json, out MacPasteboardScope? scope)
        {
            scope = null;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            return root != null && TryReadScopeMember(root, "scope", out scope);
        }

        /// <summary>Parses <c>ReadResultJson</c>.</summary>
        internal static bool TryParseReadResult(string? json, out MacClipboardReadContents? contents)
        {
            contents = null;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            if (root == null) { return false; }
            if (!TryReadInt64(root, "changeCount", out long changeCount)) { return false; }
            if (!root.TryGetMember("items", out MacJsonValue items) || items.Kind != MacJsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<MacClipboardItem>();
            foreach (MacJsonValue item in items.AsArray())
            {
                if (!item.TryGetMember("representations", out MacJsonValue reps)
                    || reps.Kind != MacJsonValueKind.Object)
                {
                    return false;
                }

                var map = new Dictionary<string, byte[]>();
                foreach (string key in reps.MemberNames())
                {
                    MacJsonValue? encoded = reps.GetMemberOrNull(key);
                    if (encoded == null) { return false; }
                    MacJsonBase64Status status = encoded.TryGetBase64Bytes(
                        MacClipboardLimits.MaxResponseBytesPerRepresentation, out byte[]? bytes);
                    if (status != MacJsonBase64Status.Success || bytes == null) { return false; }
                    map[key] = bytes;
                }
                parsed.Add(new MacClipboardItem(map));
            }

            contents = new MacClipboardReadContents(changeCount, parsed);
            return true;
        }

        /// <summary>
        /// Parses <c>ReadDataJson</c>. A <c>data</c> member written as an explicit null is a
        /// success with no bytes, which is why the payload is reported separately from the result.
        /// </summary>
        internal static bool TryParseReadData(string? json, out byte[]? data)
        {
            data = null;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            // The key is always written, so its absence is a schema mismatch rather than "no data".
            if (root == null || !root.TryGetMember("data", out MacJsonValue value)) { return false; }
            if (value.IsNull) { return true; }

            MacJsonBase64Status status = value.TryGetBase64Bytes(
                MacClipboardLimits.MaxResponseBytesPerRepresentation, out byte[]? bytes);
            if (status != MacJsonBase64Status.Success || bytes == null) { return false; }

            data = bytes;
            return true;
        }

        /// <summary>Parses <c>SnapshotJson</c>.</summary>
        internal static bool TryParseSnapshot(string? json, out MacClipboardSnapshot? snapshot)
        {
            snapshot = null;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            if (root == null) { return false; }
            if (!TryReadInt64(root, "changeCount", out long changeCount)) { return false; }

            if (!root.TryGetMember("itemTypes", out MacJsonValue itemTypes)
                || itemTypes.Kind != MacJsonValueKind.Array)
            {
                return false;
            }
            var types = new List<IReadOnlyList<string>>();
            foreach (MacJsonValue perItem in itemTypes.AsArray())
            {
                if (perItem.Kind != MacJsonValueKind.Array) { return false; }
                var one = new List<string>();
                foreach (MacJsonValue t in perItem.AsArray())
                {
                    if (!t.TryGetString(out string s)) { return false; }
                    one.Add(s);
                }
                types.Add(one);
            }

            if (!root.TryGetMember("matchingItemIndexes", out MacJsonValue indexes)
                || indexes.Kind != MacJsonValueKind.Array)
            {
                return false;
            }
            var matching = new List<int>();
            foreach (MacJsonValue i in indexes.AsArray())
            {
                // int, not long: these index a managed collection.
                if (!i.TryGetInt(out int index)) { return false; }
                matching.Add(index);
            }

            snapshot = new MacClipboardSnapshot(changeCount, types, matching);
            return true;
        }

        /// <summary>Parses <c>ChangeCountJson</c>, returned by clear.</summary>
        internal static bool TryParseChangeCount(string? json, out long changeCount)
        {
            changeCount = 0;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            return root != null && TryReadInt64(root, "changeCount", out changeCount);
        }

        /// <summary>Parses <c>BoolJson</c>, returned by checkForegroundChange.</summary>
        internal static bool TryParseBool(string? json, out bool value)
        {
            value = false;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            return root != null
                && root.TryGetMember("value", out MacJsonValue member)
                && member.TryGetBool(out value);
        }

        /// <summary>Parses <c>AccessBehaviorJson</c>.</summary>
        internal static bool TryParseAccessBehavior(string? json, out MacClipboardAccessBehavior behavior)
        {
            behavior = MacClipboardAccessBehavior.Unknown;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            if (root == null
                || !root.TryGetMember("value", out MacJsonValue member)
                || !member.TryGetString(out string raw))
            {
                return false;
            }
            // A value this package does not know maps to Unknown rather than failing: the native
            // layer may gain a case first, and an access behaviour is advisory.
            behavior = MacClipboardDetectionPatternExtensions.ParseAccessBehavior(raw);
            return true;
        }

        /// <summary>
        /// Parses <c>PatternsJson</c>, which is a bare top-level array rather than an object.
        /// Unknown pattern names are skipped so a newer native layer does not break the read.
        /// </summary>
        internal static bool TryParsePatterns(
            string? json, out IReadOnlyList<MacClipboardDetectionPattern> patterns)
        {
            patterns = Array.Empty<MacClipboardDetectionPattern>();
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            if (root == null || root.Kind != MacJsonValueKind.Array) { return false; }

            var parsed = new List<MacClipboardDetectionPattern>();
            foreach (MacJsonValue value in root.AsArray())
            {
                if (!value.TryGetString(out string raw)) { return false; }
                if (MacClipboardDetectionPatternExtensions.TryParsePattern(
                        raw, out MacClipboardDetectionPattern pattern))
                {
                    parsed.Add(pattern);
                }
            }
            patterns = parsed;
            return true;
        }

        /// <summary>Parses <c>DetectedMetadataJson</c>.</summary>
        internal static bool TryParseDetectedMetadata(string? json, out MacClipboardDetectedMetadata? metadata)
        {
            metadata = null;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            if (root == null
                || !root.TryGetMember("metadataTypes", out MacJsonValue types)
                || types.Kind != MacJsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<MacClipboardMetadataType>();
            foreach (MacJsonValue value in types.AsArray())
            {
                if (!value.TryGetString(out string raw)) { return false; }
                if (MacClipboardDetectionPatternExtensions.TryParseMetadataType(
                        raw, out MacClipboardMetadataType type))
                {
                    parsed.Add(type);
                }
            }

            // Written as an explicit null when absent, so the key must be present.
            if (!root.TryGetMember("contentTypeIdentifier", out MacJsonValue contentType)) { return false; }
            string? identifier = null;
            if (!contentType.IsNull && !contentType.TryGetString(out identifier!)) { return false; }

            metadata = new MacClipboardDetectedMetadata(parsed, contentType.IsNull ? null : identifier);
            return true;
        }

        /// <summary>Parses <c>DetectedValuesJson</c>.</summary>
        internal static bool TryParseDetectedValues(string? json, out MacClipboardDetectedValues? values)
        {
            values = null;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            if (root == null) { return false; }

            if (!root.TryGetMember("patterns", out MacJsonValue patternsValue)
                || patternsValue.Kind != MacJsonValueKind.Array)
            {
                return false;
            }
            var patterns = new List<MacClipboardDetectionPattern>();
            foreach (MacJsonValue value in patternsValue.AsArray())
            {
                if (!value.TryGetString(out string raw)) { return false; }
                if (MacClipboardDetectionPatternExtensions.TryParsePattern(
                        raw, out MacClipboardDetectionPattern pattern))
                {
                    patterns.Add(pattern);
                }
            }

            if (!TryReadNullableString(root, "probableWebURL", out string? probableWebUrl)) { return false; }
            if (!TryReadNullableString(root, "probableWebSearch", out string? probableWebSearch)) { return false; }

            if (!root.TryGetMember("number", out MacJsonValue numberValue)) { return false; }
            double? number = null;
            if (!numberValue.IsNull)
            {
                if (!numberValue.TryGetDouble(out double parsedNumber)) { return false; }
                number = parsedNumber;
            }

            var links = new List<MacClipboardDetectedLink>();
            if (!TryReadArray(root, "links", links, static (v, list) =>
            {
                if (!TryReadString(v, "matchedString", out string matched)) { return false; }
                if (!TryReadString(v, "url", out string url)) { return false; }
                list.Add(new MacClipboardDetectedLink(matched, url));
                return true;
            }))
            {
                return false;
            }

            var phoneNumbers = new List<MacClipboardLabeledValue>();
            if (!TryReadArray(root, "phoneNumbers", phoneNumbers, static (v, list) =>
                TryReadLabeledValue(v, "phoneNumber", list)))
            {
                return false;
            }

            var emailAddresses = new List<MacClipboardLabeledValue>();
            if (!TryReadArray(root, "emailAddresses", emailAddresses, static (v, list) =>
                TryReadLabeledValue(v, "emailAddress", list)))
            {
                return false;
            }

            var postalAddresses = new List<MacClipboardPostalAddress>();
            if (!TryReadArray(root, "postalAddresses", postalAddresses, static (v, list) =>
            {
                if (!TryReadString(v, "matchedString", out string matched)) { return false; }
                if (!TryReadNullableString(v, "street", out string? street)) { return false; }
                if (!TryReadNullableString(v, "city", out string? city)) { return false; }
                if (!TryReadNullableString(v, "state", out string? state)) { return false; }
                if (!TryReadNullableString(v, "postalCode", out string? postalCode)) { return false; }
                if (!TryReadNullableString(v, "country", out string? country)) { return false; }
                list.Add(new MacClipboardPostalAddress(matched, street, city, state, postalCode, country));
                return true;
            }))
            {
                return false;
            }

            var calendarEvents = new List<MacClipboardCalendarEvent>();
            if (!TryReadArray(root, "calendarEvents", calendarEvents, static (v, list) =>
            {
                if (!TryReadString(v, "matchedString", out string matched)) { return false; }
                if (!v.TryGetMember("isAllDay", out MacJsonValue allDayValue)
                    || !allDayValue.TryGetBool(out bool isAllDay))
                {
                    return false;
                }
                if (!TryReadNullableDate(v, "startDate", out DateTimeOffset? start)) { return false; }
                if (!TryReadNullableString(v, "startTimeZoneIdentifier", out string? startZone)) { return false; }
                if (!TryReadNullableDate(v, "endDate", out DateTimeOffset? end)) { return false; }
                if (!TryReadNullableString(v, "endTimeZoneIdentifier", out string? endZone)) { return false; }
                list.Add(new MacClipboardCalendarEvent(matched, isAllDay, start, startZone, end, endZone));
                return true;
            }))
            {
                return false;
            }

            var shipments = new List<MacClipboardShipmentTracking>();
            if (!TryReadArray(root, "shipmentTrackingNumbers", shipments, static (v, list) =>
            {
                if (!TryReadString(v, "matchedString", out string matched)) { return false; }
                if (!TryReadString(v, "carrier", out string carrier)) { return false; }
                if (!TryReadString(v, "trackingNumber", out string tracking)) { return false; }
                list.Add(new MacClipboardShipmentTracking(matched, carrier, tracking));
                return true;
            }))
            {
                return false;
            }

            var flights = new List<MacClipboardFlightNumber>();
            if (!TryReadArray(root, "flightNumbers", flights, static (v, list) =>
            {
                if (!TryReadString(v, "matchedString", out string matched)) { return false; }
                if (!TryReadString(v, "airline", out string airline)) { return false; }
                if (!TryReadString(v, "flightNumber", out string flight)) { return false; }
                list.Add(new MacClipboardFlightNumber(matched, airline, flight));
                return true;
            }))
            {
                return false;
            }

            var money = new List<MacClipboardMoneyAmount>();
            if (!TryReadArray(root, "moneyAmounts", money, static (v, list) =>
            {
                if (!TryReadString(v, "matchedString", out string matched)) { return false; }
                if (!TryReadString(v, "currencyCode", out string currency)) { return false; }
                if (!v.TryGetMember("amount", out MacJsonValue amountValue)
                    || !amountValue.TryGetDouble(out double amount))
                {
                    return false;
                }
                list.Add(new MacClipboardMoneyAmount(matched, currency, amount));
                return true;
            }))
            {
                return false;
            }

            values = new MacClipboardDetectedValues(
                patterns, probableWebUrl, probableWebSearch, number, links, phoneNumbers, emailAddresses,
                postalAddresses, calendarEvents, shipments, flights, money);
            return true;
        }

        /// <summary>Parses <c>ChangeEventJson</c>, delivered while observation is running.</summary>
        internal static bool TryParseChangeEvent(string? json, out MacClipboardChangeEvent? changeEvent)
        {
            changeEvent = null;
            MacJsonValue? root = MacClipboardJsonReader.Parse(json);
            if (root == null) { return false; }
            if (!TryReadScopeMember(root, "scope", out MacPasteboardScope? scope)) { return false; }
            if (!TryReadInt64(root, "changeCount", out long changeCount)) { return false; }

            changeEvent = new MacClipboardChangeEvent(scope!, changeCount);
            return true;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static bool TryReadScopeMember(MacJsonValue parent, string key, out MacPasteboardScope? scope)
        {
            scope = null;
            if (!parent.TryGetMember(key, out MacJsonValue value)
                || value.Kind != MacJsonValueKind.Object
                || !TryReadString(value, "kind", out string kind))
            {
                return false;
            }

            // The general pasteboard has no name, and Swift's synthesised encoder omits the key
            // rather than writing null. This is the one place a missing key is not a mismatch.
            if (kind == "general")
            {
                scope = MacPasteboardScope.General;
                return true;
            }

            if (!TryReadString(value, "name", out string name) || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }
            scope = kind switch
            {
                "named" => MacPasteboardScope.Named(name),
                "unique" => MacPasteboardScope.Unique(name),
                _ => null,
            };
            return scope != null;
        }

        private static bool TryReadInt64(MacJsonValue parent, string key, out long value)
        {
            value = 0;
            // TryGetInt64, not TryGetInt: every native changeCount is a Swift Int, so a value past
            // int.MaxValue is a normal response rather than a malformed one.
            return parent.TryGetMember(key, out MacJsonValue member) && member.TryGetInt64(out value);
        }

        private static bool TryReadString(MacJsonValue parent, string key, out string value)
        {
            value = string.Empty;
            return parent.TryGetMember(key, out MacJsonValue member) && member.TryGetString(out value);
        }

        // The key must be present: these are written as explicit nulls, so a missing one means the
        // response does not match the schema.
        private static bool TryReadNullableString(MacJsonValue parent, string key, out string? value)
        {
            value = null;
            if (!parent.TryGetMember(key, out MacJsonValue member)) { return false; }
            if (member.IsNull) { return true; }
            if (!member.TryGetString(out string parsed)) { return false; }
            value = parsed;
            return true;
        }

        private static bool TryReadNullableDate(MacJsonValue parent, string key, out DateTimeOffset? value)
        {
            value = null;
            if (!parent.TryGetMember(key, out MacJsonValue member)) { return false; }
            if (member.IsNull) { return true; }
            if (!member.TryGetString(out string text)) { return false; }

            // A date that cannot be read leaves the field null rather than failing the whole
            // event: the rest of the match is still usable, and the format is the detector's.
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                value = parsed;
            }
            return true;
        }

        private static bool TryReadArray<T>(
            MacJsonValue parent, string key, List<T> destination, Func<MacJsonValue, List<T>, bool> readOne)
        {
            if (!parent.TryGetMember(key, out MacJsonValue member)
                || member.Kind != MacJsonValueKind.Array)
            {
                return false;
            }
            foreach (MacJsonValue element in member.AsArray())
            {
                if (!readOne(element, destination)) { return false; }
            }
            return true;
        }

        private static bool TryReadLabeledValue(MacJsonValue value, string valueKey, List<MacClipboardLabeledValue> list)
        {
            if (!TryReadString(value, "matchedString", out string matched)) { return false; }
            if (!TryReadString(value, valueKey, out string parsed)) { return false; }
            if (!TryReadNullableString(value, "label", out string? label)) { return false; }
            list.Add(new MacClipboardLabeledValue(matched, parsed, label));
            return true;
        }
    }
}
#endif
