#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using UnityEngine;

    /// <summary>
    /// Maps the JSON envelopes returned by the iOS clipboard bridge onto the public result types.
    /// <para>
    /// Structural problems are never smoothed over with default values: a missing <c>ok</c> flag,
    /// an unexpected <c>data</c> shape, or a missing required field all produce a failure result,
    /// so a broken bridge can never be mistaken for a successful empty read.
    /// </para>
    /// <para>
    /// Intentional deviation from the "log every internal method" rule in csharp.md: these methods
    /// receive raw clipboard payloads, which may hold passwords or tokens. Only failure facts are
    /// logged (status, error code, counts) and never the content itself, matching the native
    /// <c>ClipboardRedaction</c> policy.
    /// </para>
    /// </summary>
    internal static class IosClipboardJsonParser
    {
        private const string LogTag = "IosClipboardJsonParser";

        /// <summary>Mirrors the native <c>ClipboardLimits.default.maxCopyByteCount</c> / <c>maxLoadByteCount</c>.</summary>
        internal const long MaxDataByteCount = 64L * 1024L * 1024L;

        internal const string UnknownErrorCode = "CLIPBOARD_UNKNOWN";
        internal const string ContentTooLargeErrorCode = "CLIPBOARD_CONTENT_TOO_LARGE";

        /// <summary>
        /// A load the caller cancelled. Documented as a normal outcome, so it is logged at info
        /// level: reporting it as an error would put a red entry in the console - and a false
        /// positive in any error monitoring - every time CancelLoads does its job.
        /// </summary>
        internal const string CancelledErrorCode = "CLIPBOARD_CANCELLED";

        internal const string NoDataMessage = "Clipboard bridge returned no data.";
        internal const string ParseFailedMessage = "Failed to parse the clipboard response.";
        internal const string DecodeFailedMessage = "Failed to decode the clipboard data.";
        internal const string ContentTooLargeMessage = "The clipboard content exceeds the configured size limit.";

        // ── Public parse entry points ────────────────────────────────────────────

        internal static IosClipboardReadResult ParseReadResult(string? json)
        {
            if (!TryReadEnvelope(json, out JsonValue? data, out IosClipboardErrorInfo error))
            {
                return IosClipboardReadResult.Failure(error);
            }

            if (data == null || data.Kind != JsonValueKind.Object)
            {
                return IosClipboardReadResult.Failure(MalformedResponse());
            }

            if (!TryGetRequiredInt(data, "numberOfItems", out int numberOfItems) ||
                !TryGetRequiredArray(data, "items", out JsonValue itemsArray))
            {
                return IosClipboardReadResult.Failure(MalformedResponse());
            }

            var items = new List<IosClipboardItem>(itemsArray.AsArray().Count);
            foreach (JsonValue element in itemsArray.AsArray())
            {
                if (element.Kind != JsonValueKind.Object)
                {
                    return IosClipboardReadResult.Failure(MalformedResponse());
                }

                items.Add(new IosClipboardItem(
                    ReadStringList(element.GetMemberOrNull("typeIdentifiers")),
                    ReadOptionalString(element, "text"),
                    ReadOptionalString(element, "urlString"),
                    ReadOptionalString(element, "imageDataUTType")));
            }

            return IosClipboardReadResult.Success(numberOfItems, items);
        }

        internal static IosClipboardReadDataResult ParseReadDataResult(string? json)
        {
            if (!TryReadEnvelope(json, out JsonValue? data, out IosClipboardErrorInfo error))
            {
                return IosClipboardReadDataResult.Failure(error);
            }

            // A null or absent data member is the documented "no data for this type" success.
            if (data == null)
            {
                return IosClipboardReadDataResult.NoData();
            }

            if (data.Kind != JsonValueKind.Object)
            {
                return IosClipboardReadDataResult.Failure(MalformedResponse());
            }

            if (!TryGetRequiredString(data, "utType", out string utType) ||
                !TryGetRequiredInt(data, "byteCount", out int byteCount) ||
                !data.TryGetMember("base64", out JsonValue base64Value))
            {
                return IosClipboardReadDataResult.Failure(MalformedResponse());
            }

            if (!TryDecodeBase64(base64Value, out byte[]? bytes, out IosClipboardErrorInfo decodeError))
            {
                return IosClipboardReadDataResult.Failure(decodeError);
            }

            // The declared byteCount is not trusted for sizing (see TryDecodeBase64), but it must
            // agree with what was actually decoded, or the response is inconsistent.
            if (bytes!.Length != byteCount)
            {
                Debug.LogError($"[{LogTag}][{nameof(ParseReadDataResult)}] byteCount mismatch: " +
                               $"declared {byteCount}, decoded {bytes.Length}.");
                return IosClipboardReadDataResult.Failure(MalformedResponse());
            }

            return IosClipboardReadDataResult.Success(utType, bytes);
        }

        internal static IosClipboardSnapshotResult ParseSnapshotResult(string? json)
        {
            if (!TryReadEnvelope(json, out JsonValue? data, out IosClipboardErrorInfo error))
            {
                return IosClipboardSnapshotResult.Failure(error);
            }

            if (data == null || data.Kind != JsonValueKind.Object)
            {
                return IosClipboardSnapshotResult.Failure(MalformedResponse());
            }

            if (!TryGetRequiredBool(data, "hasStrings", out bool hasStrings) ||
                !TryGetRequiredBool(data, "hasURLs", out bool hasUrls) ||
                !TryGetRequiredBool(data, "hasImages", out bool hasImages) ||
                !TryGetRequiredBool(data, "hasColors", out bool hasColors) ||
                !TryGetRequiredInt(data, "numberOfItems", out int numberOfItems) ||
                !TryGetRequiredArray(data, "typeIdentifiers", out JsonValue typeIdentifiers) ||
                !TryGetRequiredArray(data, "allTypeIdentifiers", out JsonValue allTypeIdentifiers))
            {
                return IosClipboardSnapshotResult.Failure(MalformedResponse());
            }

            // Every snapshot array is a required field, so this payload does no element-level
            // skipping: a wrong element type is a structural break, and dropping it would report a
            // shorter, plausible-looking list the bridge never sent. Where skipping IS the right
            // behaviour — optional and best-effort fields elsewhere in this parser — the lenient
            // ReadStringList is used instead; see its own comment for those call sites.
            if (!TryReadStrictStringList(typeIdentifiers, out IReadOnlyList<string> firstItemTypes))
            {
                Debug.LogError($"[{LogTag}][{nameof(ParseSnapshotResult)}] typeIdentifiers holds a non-string element.");
                return IosClipboardSnapshotResult.Failure(MalformedResponse());
            }

            var allTypes = new List<IReadOnlyList<string>>(allTypeIdentifiers.AsArray().Count);
            foreach (JsonValue element in allTypeIdentifiers.AsArray())
            {
                if (element.Kind != JsonValueKind.Array)
                {
                    Debug.LogError($"[{LogTag}][{nameof(ParseSnapshotResult)}] allTypeIdentifiers row is not an array.");
                    return IosClipboardSnapshotResult.Failure(MalformedResponse());
                }

                if (!TryReadStrictStringList(element, out IReadOnlyList<string> rowTypes))
                {
                    Debug.LogError(
                        $"[{LogTag}][{nameof(ParseSnapshotResult)}] allTypeIdentifiers row holds a non-string element.");
                    return IosClipboardSnapshotResult.Failure(MalformedResponse());
                }
                allTypes.Add(rowTypes);
            }

            // Absent or null means "no matchingTypes were requested"; an empty array means they
            // were requested and nothing matched. Because that distinction is part of the public
            // contract, a present-but-non-array value is failed rather than folded into either
            // meaning: silently choosing one would misreport what the pasteboard was asked.
            IReadOnlyList<int>? matchingIndexes = null;
            JsonValue? matching = data.GetMemberOrNull("matchingItemIndexes");
            if (matching != null)
            {
                if (matching.Kind != JsonValueKind.Array)
                {
                    Debug.LogError($"[{LogTag}][{nameof(ParseSnapshotResult)}] matchingItemIndexes is not an array.");
                    return IosClipboardSnapshotResult.Failure(MalformedResponse());
                }

                if (!TryReadStrictIntList(matching, out IReadOnlyList<int> indexes))
                {
                    Debug.LogError(
                        $"[{LogTag}][{nameof(ParseSnapshotResult)}] matchingItemIndexes holds a non-integer element.");
                    return IosClipboardSnapshotResult.Failure(MalformedResponse());
                }
                matchingIndexes = indexes;
            }

            var snapshot = new IosClipboardSnapshot(
                hasStrings,
                hasUrls,
                hasImages,
                hasColors,
                numberOfItems,
                firstItemTypes,
                allTypes,
                matchingIndexes);

            return IosClipboardSnapshotResult.Success(snapshot);
        }

        internal static IosPasteboardScopeResult ParsePasteboardScopeResult(string? json)
        {
            if (!TryReadEnvelope(json, out JsonValue? data, out IosClipboardErrorInfo error))
            {
                return IosPasteboardScopeResult.Failure(error);
            }

            if (data == null || data.Kind != JsonValueKind.Object)
            {
                return IosPasteboardScopeResult.Failure(MalformedResponse());
            }

            IosPasteboardScope? scope = ReadScope(data.GetMemberOrNull("scope"));
            if (scope == null)
            {
                return IosPasteboardScopeResult.Failure(MalformedResponse());
            }

            return IosPasteboardScopeResult.Success(scope);
        }

        internal static IosClipboardDetectedPatternsResult ParseDetectedPatternsResult(string? json)
        {
            if (!TryReadEnvelope(json, out JsonValue? data, out IosClipboardErrorInfo error))
            {
                return IosClipboardDetectedPatternsResult.Failure(error);
            }

            if (data == null || data.Kind != JsonValueKind.Object ||
                !TryGetRequiredArray(data, "patterns", out JsonValue patternsArray))
            {
                return IosClipboardDetectedPatternsResult.Failure(MalformedResponse());
            }

            return IosClipboardDetectedPatternsResult.Success(ReadPatterns(patternsArray));
        }

        internal static IosClipboardDetectedValuesResult ParseDetectedValuesResult(string? json)
        {
            if (!TryReadEnvelope(json, out JsonValue? data, out IosClipboardErrorInfo error))
            {
                return IosClipboardDetectedValuesResult.Failure(error);
            }

            if (data == null || data.Kind != JsonValueKind.Object ||
                !TryGetRequiredArray(data, "detectedPatterns", out JsonValue detectedPatterns))
            {
                return IosClipboardDetectedValuesResult.Failure(MalformedResponse());
            }

            double? number = null;
            JsonValue? numberValue = data.GetMemberOrNull("number");
            if (numberValue != null && numberValue.TryGetDouble(out double parsedNumber))
            {
                number = parsedNumber;
            }

            var values = new IosClipboardDetectedValues(
                ReadPatterns(detectedPatterns),
                ReadOptionalString(data, "probableWebURL"),
                ReadOptionalString(data, "probableWebSearch"),
                number,
                ReadStringList(data.GetMemberOrNull("links")),
                ReadLabeledValues(data.GetMemberOrNull("emailAddresses")),
                ReadLabeledValues(data.GetMemberOrNull("phoneNumbers")),
                ReadPostalAddresses(data.GetMemberOrNull("postalAddresses")),
                ReadCalendarEvents(data.GetMemberOrNull("calendarEvents")),
                ReadFlightNumbers(data.GetMemberOrNull("flightNumbers")),
                ReadMoneyAmounts(data.GetMemberOrNull("moneyAmounts")),
                ReadShipmentTrackingNumbers(data.GetMemberOrNull("shipmentTrackingNumbers")));

            return IosClipboardDetectedValuesResult.Success(values);
        }

        internal static IosClipboardLoadedItemResult ParseLoadedItemResult(string? json)
        {
            if (!TryReadEnvelope(json, out JsonValue? data, out IosClipboardErrorInfo error))
            {
                return IosClipboardLoadedItemResult.Failure(error);
            }

            if (data == null || data.Kind != JsonValueKind.Object ||
                !TryGetRequiredString(data, "kind", out string kind))
            {
                return IosClipboardLoadedItemResult.Failure(MalformedResponse());
            }

            switch (kind)
            {
                case "text":
                    return TryGetRequiredString(data, "text", out string text)
                        ? IosClipboardLoadedItemResult.Success(IosClipboardLoadedItem.FromText(text))
                        : IosClipboardLoadedItemResult.Failure(MalformedResponse());

                case "url":
                    return TryGetRequiredString(data, "urlString", out string urlString)
                        ? IosClipboardLoadedItemResult.Success(IosClipboardLoadedItem.FromUrl(urlString))
                        : IosClipboardLoadedItemResult.Failure(MalformedResponse());

                case "imageData":
                {
                    if (!TryGetRequiredString(data, "utType", out string utType) ||
                        !data.TryGetMember("base64", out JsonValue base64Value))
                    {
                        return IosClipboardLoadedItemResult.Failure(MalformedResponse());
                    }

                    if (!TryDecodeBase64(base64Value, out byte[]? bytes, out IosClipboardErrorInfo decodeError))
                    {
                        return IosClipboardLoadedItemResult.Failure(decodeError);
                    }

                    return IosClipboardLoadedItemResult.Success(
                        IosClipboardLoadedItem.FromImageData(bytes!, utType));
                }

                case "file":
                    return TryGetRequiredString(data, "path", out string path)
                        ? IosClipboardLoadedItemResult.Success(IosClipboardLoadedItem.FromFile(path))
                        : IosClipboardLoadedItemResult.Failure(MalformedResponse());

                default:
                    // The native layer emits "unknown" deliberately, and a future version may add
                    // kinds. Both are successful results carrying an Unknown item.
                    return IosClipboardLoadedItemResult.Success(IosClipboardLoadedItem.UnknownKind());
            }
        }

        internal static IosClipboardForegroundChangeResult ParseForegroundChangeResult(string? json)
        {
            if (!TryReadEnvelope(json, out JsonValue? data, out IosClipboardErrorInfo error))
            {
                return IosClipboardForegroundChangeResult.Failure(error);
            }

            if (data == null || data.Kind != JsonValueKind.Object ||
                !TryGetRequiredBool(data, "changed", out bool changed))
            {
                return IosClipboardForegroundChangeResult.Failure(MalformedResponse());
            }

            return IosClipboardForegroundChangeResult.Success(changed);
        }

        /// <summary>
        /// Parses a change event, which the native layer sends without an envelope.
        /// <para>
        /// Returns <c>null</c> when the payload cannot be parsed or carries no <c>kind</c>: such an
        /// event is dropped rather than surfaced. A parsed-but-unrecognized <c>kind</c> is
        /// delivered as <see cref="IosClipboardChangeEventKind.Unknown"/>, so a broken bridge and a
        /// deliberate native "unknown" stay distinguishable.
        /// </para>
        /// </summary>
        /// <param name="eventJson">Raw event JSON.</param>
        /// <returns>The event, or <c>null</c> when it must be dropped.</returns>
        internal static IosClipboardChangeEvent? ParseChangeEvent(string? eventJson)
        {
            JsonValue? root = IosClipboardJsonReader.Parse(eventJson);
            if (root == null || root.Kind != JsonValueKind.Object)
            {
                Debug.LogError($"[{LogTag}][{nameof(ParseChangeEvent)}] Dropped a change event: unparsable payload.");
                return null;
            }

            if (!TryGetRequiredString(root, "kind", out string kind))
            {
                Debug.LogError($"[{LogTag}][{nameof(ParseChangeEvent)}] Dropped a change event: missing kind.");
                return null;
            }

            IosClipboardChangeEventKind eventKind = kind switch
            {
                "changed" => IosClipboardChangeEventKind.Changed,
                "changedDetectedOnForeground" => IosClipboardChangeEventKind.ChangedDetectedOnForeground,
                "removed" => IosClipboardChangeEventKind.Removed,
                _ => IosClipboardChangeEventKind.Unknown
            };

            // The kind is the actionable part; a missing or malformed scope must not cost the
            // subscriber the whole notification.
            IosPasteboardScope? scope = ReadScope(root.GetMemberOrNull("scope"));

            return new IosClipboardChangeEvent(
                eventKind,
                scope,
                ReadStringList(root.GetMemberOrNull("typesAdded")),
                ReadStringList(root.GetMemberOrNull("typesRemoved")));
        }

        // ── Envelope ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates the <c>{"ok":...}</c> envelope.
        /// </summary>
        /// <param name="json">Raw response.</param>
        /// <param name="data">The <c>data</c> member on success; <c>null</c> when absent or JSON null.</param>
        /// <param name="error">Error info when the envelope reports or constitutes a failure.</param>
        /// <returns><c>true</c> when the envelope reports success.</returns>
        private static bool TryReadEnvelope(string? json, out JsonValue? data, out IosClipboardErrorInfo error)
        {
            data = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError($"[{LogTag}][{nameof(TryReadEnvelope)}] Response was null or blank.");
                error = IosClipboardErrorInfo.Create(UnknownErrorCode, NoDataMessage);
                return false;
            }

            JsonValue? root = IosClipboardJsonReader.Parse(json);
            if (root == null || root.Kind != JsonValueKind.Object)
            {
                error = MalformedResponse();
                return false;
            }

            // A missing or non-boolean ok flag is a structural failure, never an implied success.
            if (!root.TryGetMember("ok", out JsonValue okValue) || !okValue.TryGetBool(out bool ok))
            {
                error = MalformedResponse();
                return false;
            }

            if (!ok)
            {
                error = ReadErrorInfo(root);
                return false;
            }

            error = default;
            data = root.GetMemberOrNull("data");
            return true;
        }

        private static IosClipboardErrorInfo ReadErrorInfo(JsonValue root)
        {
            JsonValue? errorValue = root.GetMemberOrNull("error");
            if (errorValue == null || errorValue.Kind != JsonValueKind.Object)
            {
                Debug.LogError($"[{LogTag}][{nameof(ReadErrorInfo)}] Failure envelope carried no error object.");
                return IosClipboardErrorInfo.Create(UnknownErrorCode, IosClipboardErrorInfo.UnknownErrorMessage);
            }

            string? code = ReadOptionalString(errorValue, "code");
            string? message = ReadOptionalString(errorValue, "message");

            string? domain = null;
            int? nativeCode = null;
            JsonValue? details = errorValue.GetMemberOrNull("details");
            if (details != null && details.Kind == JsonValueKind.Object)
            {
                string? detailDomain = ReadOptionalString(details, "domain");
                JsonValue? detailCode = details.GetMemberOrNull("code");
                if (detailDomain != null && detailCode != null && detailCode.TryGetInt(out int parsedCode))
                {
                    domain = detailDomain;
                    nativeCode = parsedCode;
                }
            }

            string logLine = $"[{LogTag}][{nameof(ReadErrorInfo)}] errorCode: {code ?? UnknownErrorCode}, " +
                             $"hasDetails: {domain != null}";
            if (code == CancelledErrorCode)
            {
                Debug.Log(logLine);
            }
            else
            {
                Debug.LogError(logLine);
            }
            return IosClipboardErrorInfo.Create(code, message, domain, nativeCode);
        }

        private static IosClipboardErrorInfo MalformedResponse()
        {
            Debug.LogError($"[{LogTag}] Malformed clipboard response.");
            return IosClipboardErrorInfo.Create(UnknownErrorCode, ParseFailedMessage);
        }

        // ── Field readers ────────────────────────────────────────────────────────

        private static bool TryGetRequiredString(JsonValue owner, string key, out string value)
        {
            if (owner.TryGetMember(key, out JsonValue member) && member.TryGetString(out value))
            {
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static bool TryGetRequiredInt(JsonValue owner, string key, out int value)
        {
            if (owner.TryGetMember(key, out JsonValue member) && member.TryGetInt(out value))
            {
                return true;
            }
            value = 0;
            return false;
        }

        private static bool TryGetRequiredBool(JsonValue owner, string key, out bool value)
        {
            if (owner.TryGetMember(key, out JsonValue member) && member.TryGetBool(out value))
            {
                return true;
            }
            value = false;
            return false;
        }

        private static bool TryGetRequiredArray(JsonValue owner, string key, out JsonValue value)
        {
            if (owner.TryGetMember(key, out JsonValue member) && member.Kind == JsonValueKind.Array)
            {
                value = member;
                return true;
            }
            value = JsonValue.Null;
            return false;
        }

        // Optional fields stay lenient: an absent, null, or unexpectedly typed value yields null
        // rather than failing the whole response, which keeps a newer native layer compatible.
        private static string? ReadOptionalString(JsonValue owner, string key)
        {
            JsonValue? member = owner.GetMemberOrNull(key);
            return member != null && member.TryGetString(out string value) ? value : null;
        }

        /// <summary>
        /// Reads a string array where every element must be a string.
        /// <para>
        /// Used for the snapshot payload, whose arrays are required fields: unlike the best-effort
        /// detection results, a wrong element type there means the response is malformed and must
        /// fail rather than degrade into a shorter list.
        /// </para>
        /// </summary>
        private static bool TryReadStrictStringList(JsonValue array, out IReadOnlyList<string> values)
        {
            var result = new List<string>(array.AsArray().Count);
            foreach (JsonValue element in array.AsArray())
            {
                if (!element.TryGetString(out string value))
                {
                    values = Array.Empty<string>();
                    return false;
                }
                result.Add(value);
            }
            values = result;
            return true;
        }

        /// <summary>
        /// Reads an integer array where every element must be an integer. See
        /// <see cref="TryReadStrictStringList"/> for why the snapshot payload cannot skip elements.
        /// </summary>
        private static bool TryReadStrictIntList(JsonValue array, out IReadOnlyList<int> values)
        {
            var result = new List<int>(array.AsArray().Count);
            foreach (JsonValue element in array.AsArray())
            {
                if (!element.TryGetInt(out int value))
                {
                    values = Array.Empty<int>();
                    return false;
                }
                result.Add(value);
            }
            values = result;
            return true;
        }

        // Lenient list reader: skips elements that are not strings. Only for optional or
        // best-effort fields (read items' typeIdentifiers, detectValues links, change event types),
        // never for the snapshot payload.
        private static IReadOnlyList<string> ReadStringList(JsonValue? array)
        {
            if (array == null || array.Kind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var values = new List<string>(array.AsArray().Count);
            foreach (JsonValue element in array.AsArray())
            {
                if (element.TryGetString(out string value))
                {
                    values.Add(value);
                }
            }
            return values;
        }

        private static IReadOnlyList<IosClipboardDetectionPattern> ReadPatterns(JsonValue array)
        {
            var patterns = new List<IosClipboardDetectionPattern>(array.AsArray().Count);
            int skipped = 0;
            foreach (JsonValue element in array.AsArray())
            {
                if (element.TryGetString(out string raw) &&
                    IosClipboardDetectionPatternExtensions.TryParse(raw, out IosClipboardDetectionPattern pattern))
                {
                    patterns.Add(pattern);
                    continue;
                }
                skipped++;
            }

            if (skipped > 0)
            {
                Debug.LogWarning($"[{LogTag}][{nameof(ReadPatterns)}] Skipped {skipped} unrecognized pattern(s).");
            }
            return patterns;
        }

        private static IReadOnlyList<IosClipboardLabeledValue> ReadLabeledValues(JsonValue? array)
        {
            var values = new List<IosClipboardLabeledValue>();
            if (array == null)
            {
                return values;
            }

            foreach (JsonValue element in array.AsArray())
            {
                if (element.Kind != JsonValueKind.Object || !TryGetRequiredString(element, "value", out string value))
                {
                    continue; // detection is best-effort: skip the entry, keep the result
                }
                values.Add(new IosClipboardLabeledValue(value, ReadOptionalString(element, "label")));
            }
            return values;
        }

        private static IReadOnlyList<IosClipboardPostalAddress> ReadPostalAddresses(JsonValue? array)
        {
            var values = new List<IosClipboardPostalAddress>();
            if (array == null)
            {
                return values;
            }

            foreach (JsonValue element in array.AsArray())
            {
                if (element.Kind != JsonValueKind.Object)
                {
                    continue;
                }
                values.Add(new IosClipboardPostalAddress(
                    ReadOptionalString(element, "street"),
                    ReadOptionalString(element, "city"),
                    ReadOptionalString(element, "state"),
                    ReadOptionalString(element, "postalCode"),
                    ReadOptionalString(element, "country")));
            }
            return values;
        }

        private static IReadOnlyList<IosClipboardCalendarEvent> ReadCalendarEvents(JsonValue? array)
        {
            var values = new List<IosClipboardCalendarEvent>();
            if (array == null)
            {
                return values;
            }

            foreach (JsonValue element in array.AsArray())
            {
                if (element.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                bool isAllDay = false;
                JsonValue? allDay = element.GetMemberOrNull("isAllDay");
                allDay?.TryGetBool(out isAllDay);

                values.Add(new IosClipboardCalendarEvent(
                    ReadOptionalDate(element, "startDate"),
                    ReadOptionalDate(element, "endDate"),
                    ReadOptionalString(element, "startTimeZone"),
                    ReadOptionalString(element, "endTimeZone"),
                    isAllDay));
            }
            return values;
        }

        private static IReadOnlyList<IosClipboardFlightNumber> ReadFlightNumbers(JsonValue? array)
        {
            var values = new List<IosClipboardFlightNumber>();
            if (array == null)
            {
                return values;
            }

            foreach (JsonValue element in array.AsArray())
            {
                if (element.Kind != JsonValueKind.Object ||
                    !TryGetRequiredString(element, "airline", out string airline) ||
                    !TryGetRequiredString(element, "flightNumber", out string flightNumber))
                {
                    continue;
                }
                values.Add(new IosClipboardFlightNumber(airline, flightNumber));
            }
            return values;
        }

        private static IReadOnlyList<IosClipboardMoneyAmount> ReadMoneyAmounts(JsonValue? array)
        {
            var values = new List<IosClipboardMoneyAmount>();
            if (array == null)
            {
                return values;
            }

            foreach (JsonValue element in array.AsArray())
            {
                if (element.Kind != JsonValueKind.Object ||
                    !TryGetRequiredString(element, "currency", out string currency))
                {
                    continue;
                }

                // The native model types amount as Double, so it always arrives as a JSON number.
                JsonValue? amountValue = element.GetMemberOrNull("amount");
                if (amountValue == null || !amountValue.TryGetDouble(out double amount))
                {
                    continue;
                }
                values.Add(new IosClipboardMoneyAmount(amount, currency));
            }
            return values;
        }

        private static IReadOnlyList<IosClipboardShipmentTracking> ReadShipmentTrackingNumbers(JsonValue? array)
        {
            var values = new List<IosClipboardShipmentTracking>();
            if (array == null)
            {
                return values;
            }

            foreach (JsonValue element in array.AsArray())
            {
                if (element.Kind != JsonValueKind.Object ||
                    !TryGetRequiredString(element, "carrier", out string carrier) ||
                    !TryGetRequiredString(element, "trackingNumber", out string trackingNumber))
                {
                    continue;
                }
                values.Add(new IosClipboardShipmentTracking(carrier, trackingNumber));
            }
            return values;
        }

        private static DateTimeOffset? ReadOptionalDate(JsonValue owner, string key)
        {
            string? raw = ReadOptionalString(owner, key);
            if (raw == null)
            {
                return null;
            }

            return DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset parsed)
                ? parsed
                : null;
        }

        private static IosPasteboardScope? ReadScope(JsonValue? scopeValue)
        {
            if (scopeValue == null || scopeValue.Kind != JsonValueKind.Object ||
                !TryGetRequiredString(scopeValue, "kind", out string kind))
            {
                return null;
            }

            switch (kind)
            {
                case "general":
                    return IosPasteboardScope.General;

                case "named":
                    return TryGetRequiredString(scopeValue, "name", out string namedName)
                        ? IosPasteboardScope.Named(namedName)
                        : null;

                case "unique":
                    return TryGetRequiredString(scopeValue, "name", out string uniqueName)
                        ? IosPasteboardScope.Unique(uniqueName)
                        : null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Decodes a base64 member, checking the size limit from the exact decoded length before
        /// allocating so an oversized payload costs nothing.
        /// </summary>
        private static bool TryDecodeBase64(JsonValue value, out byte[]? bytes, out IosClipboardErrorInfo error)
        {
            JsonBase64Status status = value.TryGetBase64Bytes(MaxDataByteCount, out bytes);
            switch (status)
            {
                case JsonBase64Status.Success:
                    error = default;
                    return true;

                case JsonBase64Status.TooLarge:
                    Debug.LogError($"[{LogTag}][{nameof(TryDecodeBase64)}] Payload exceeds {MaxDataByteCount} bytes.");
                    error = IosClipboardErrorInfo.Create(ContentTooLargeErrorCode, ContentTooLargeMessage);
                    return false;

                default:
                    Debug.LogError($"[{LogTag}][{nameof(TryDecodeBase64)}] base64 decode failed: {status}.");
                    error = IosClipboardErrorInfo.Create(UnknownErrorCode, DecodeFailedMessage);
                    return false;
            }
        }
    }
}
#endif
