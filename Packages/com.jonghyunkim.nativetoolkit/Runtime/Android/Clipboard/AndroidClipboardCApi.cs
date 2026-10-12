#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JonghyunKim.NativeToolkit.Runtime.Android.Common;

    /// <summary>
    /// The bridge to native-toolkit's C ABI for the clipboard (<c>Clipboard.h</c> in libntk.so): the
    /// options struct, the <c>extern</c> declarations, and the pure conversions between the manager's
    /// public API and the C ABI.
    /// </summary>
    /// <remarks>
    /// The design is artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md (4.1
    /// to 4.4). C# checks no input: the library runs the same Kotlin checks as 1.x, and this class
    /// turns its error values into 1.x's messages and codes. The conversions compile in the Editor
    /// too, so EditMode tests reach them. The <c>extern</c>s are compiled into an Android player only.
    /// </remarks>
    internal static class AndroidClipboardCApi
    {
        // ntk_clipboard_error (Clipboard.h).
        internal const int ErrorNone = 0;
        internal const int ErrorInvalidParameter = 1;
        internal const int ErrorNotInitialized = 2;
        internal const int ErrorNotSupported = 3;
        internal const int ErrorUnknown = 4;
        internal const int ErrorOutOfMemory = 5;
        internal const int ErrorEmptyContent = 6;
        internal const int ErrorEmptyItems = 7;
        internal const int ErrorInvalidUri = 8;
        internal const int ErrorUnavailable = 9;
        internal const int ErrorReadNotAllowed = 10;
        internal const int ErrorSecurity = 11;

        /// <summary>The read results' code when the library could not be called (1.x's own code).</summary>
        internal const string BridgeUnavailableErrorCode = "CLIPBOARD_BRIDGE_UNAVAILABLE";

        internal const string UnknownErrorCode = "CLIPBOARD_UNKNOWN";

        // 1.x's messages (tag 1.12.0, UnityAndroidClipboardManager.kt errorInfoOf).
        internal const string EmptyContentMessage = "Clipboard content is empty. Please provide text or HTML.";
        internal const string EmptyItemsMessage = "No items provided for clipboard copy.";
        internal const string UnavailableMessage = "Clipboard service is unavailable.";
        internal const string ReadNotAllowedMessage = "Clipboard read is not allowed. The app must be in the foreground.";

        /// <summary>
        /// 1.x added the exception's message after the colon; the C ABI does not return it (design
        /// 5.3).
        /// </summary>
        internal const string SecurityMessage = "Security restriction while accessing clipboard.";

        /// <summary>The change event: <c>void (*)(void* user_data)</c>, on the Android main thread.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ChangeCallback(IntPtr userData);

        /// <summary><c>ntk_clipboard_copy_options</c>: 24 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct CopyOptions
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Label;
            public int Sensitive;
            public uint Reserved1;
        }

        /// <summary>One clip item's four values as the library returned them, before normalizing.</summary>
        internal readonly struct RawItem
        {
            internal RawItem(string? text, string? html, string? uri, string? coerced)
            {
                Text = text;
                Html = html;
                Uri = uri;
                Coerced = coerced;
            }

            internal string? Text { get; }
            internal string? Html { get; }
            internal string? Uri { get; }
            internal string? Coerced { get; }
        }

        // ── Arguments (4.2) ───────────────────────────────────────────────────

        /// <summary>A null string passes as "" (design J-C7): the C ABI refuses NULL text.</summary>
        internal static string AsPassed(string? value) => value ?? "";

        /// <summary>The list with every null element passed as "" (J-C7), in the same order.</summary>
        internal static string[] AsPassed(string[] values)
        {
            var passed = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                passed[i] = AsPassed(values[i]);
            return passed;
        }

        /// <summary>
        /// The copy options. A null or blank label is no label (NULL), as 1.x left it out of the JSON
        /// (<c>AndroidClipboardJsonBuilder</c>); the library then labels the clip "".
        /// </summary>
        internal static CopyOptions BuildOptions(string? label, bool sensitive, List<IntPtr> allocated) => new()
        {
            StructSize = (uint)Marshal.SizeOf<CopyOptions>(),
            Reserved0 = 0,
            Label = string.IsNullOrWhiteSpace(label) ? IntPtr.Zero : AndroidNativeToolkitCApi.AllocUtf8(label, allocated),
            Sensitive = sensitive ? 1 : 0,
            Reserved1 = 0,
        };

        // ── Errors (4.4) ──────────────────────────────────────────────────────

        internal static string ErrorName(int error) => error switch
        {
            ErrorNone => "NONE",
            ErrorInvalidParameter => "INVALID_PARAMETER",
            ErrorNotInitialized => "NOT_INITIALIZED",
            ErrorNotSupported => "NOT_SUPPORTED",
            ErrorUnknown => "UNKNOWN",
            ErrorOutOfMemory => "OUT_OF_MEMORY",
            ErrorEmptyContent => "EMPTY_CONTENT",
            ErrorEmptyItems => "EMPTY_ITEMS",
            ErrorInvalidUri => "INVALID_URI",
            ErrorUnavailable => "UNAVAILABLE",
            ErrorReadNotAllowed => "READ_NOT_ALLOWED",
            ErrorSecurity => "SECURITY",
            _ => $"ERROR_{error}",
        };

        /// <summary>
        /// The values that mean C# built the request wrongly, logged as errors. INVALID_PARAMETER can
        /// also come from a Kotlin IllegalArgumentException, which the clipboard path does not throw.
        /// </summary>
        internal static bool IsRequestDefect(int error) => error == ErrorInvalidParameter || error == ErrorNotSupported;

        /// <summary>1.x's message when the call could not be made at all.</summary>
        internal static string NotStartedMessage(string operation) => $"{operation} could not be started.";

        /// <summary>
        /// A failed copy, clear or stopObserving as 1.x worded it. <paramref name="uri"/> is the URI
        /// the caller passed (null is ""), which 1.x put in the INVALID_URI message.
        /// </summary>
        internal static string OperationFailureMessage(string operation, int error, string? uri = null) => error switch
        {
            ErrorNotInitialized => NotStartedMessage(operation),
            ErrorEmptyContent => EmptyContentMessage,
            ErrorEmptyItems => EmptyItemsMessage,
            ErrorInvalidUri => $"Invalid URI: {uri ?? ""}",
            ErrorUnavailable => UnavailableMessage,
            ErrorReadNotAllowed => ReadNotAllowedMessage,
            ErrorSecurity => SecurityMessage,
            _ => $"Failed: {ErrorName(error)}",
        };

        /// <summary>
        /// A failed read or getDescription: 1.x's code and message. The copy-only values (6 to 8) are
        /// not expected here and are reported as unexpected values.
        /// </summary>
        internal static (string Code, string Message) ReadFailure(string operation, int error) => error switch
        {
            ErrorNotInitialized => (BridgeUnavailableErrorCode, NotStartedMessage(operation)),
            ErrorUnavailable => ("CLIPBOARD_UNAVAILABLE", UnavailableMessage),
            ErrorReadNotAllowed => ("CLIPBOARD_READ_NOT_ALLOWED", ReadNotAllowedMessage),
            ErrorSecurity => ("CLIPBOARD_SECURITY", SecurityMessage),
            _ => (UnknownErrorCode, $"Failed: {ErrorName(error)}"),
        };

        // ── Read results (4.3, J-C4) ──────────────────────────────────────────

        /// <summary>1.x never returned an empty string: "" and a missing value both become null.</summary>
        internal static string? Normalize(string? value) => string.IsNullOrEmpty(value) ? null : value;

        /// <summary>The content a read returned, normalized as 1.x's parser did.</summary>
        internal static ClipContents ToClipContents(string? label, IReadOnlyList<string> mimeTypes, IReadOnlyList<RawItem> items)
        {
            var clipItems = new List<ClipItem>(items.Count);
            foreach (RawItem item in items)
                clipItems.Add(new ClipItem(Normalize(item.Text), Normalize(item.Html), Normalize(item.Uri), Normalize(item.Coerced)));
            return new ClipContents(Normalize(label), mimeTypes, clipItems);
        }

        /// <summary>
        /// The description a read returned. The library reports no classification as -1; 1.x's parser
        /// read a missing one as 0. Neither is a CLASSIFICATION_* value (1 to 3), so both are null.
        /// </summary>
        internal static ClipDescriptionInfo ToDescriptionInfo(string? label, IReadOnlyList<string> mimeTypes, int styled, int classification) =>
            new(Normalize(label), mimeTypes, styled != 0, classification == -1 || classification == 0 ? null : classification);

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Reads every value out of an <c>ntk_clipboard_content</c>. Does not free it.</summary>
        internal static ClipContents ReadContent(IntPtr content)
        {
            string? label = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_clipboard_content_label(content, out UIntPtr labelSize), labelSize);
            var mimeTypes = new List<string>();
            ulong mimeCount = ntk_clipboard_content_mime_type_count(content).ToUInt64();
            for (ulong i = 0; i < mimeCount; i++)
            {
                IntPtr data = ntk_clipboard_content_mime_type_at(content, new UIntPtr(i), out UIntPtr size);
                mimeTypes.Add(AndroidNativeToolkitCApi.ReadUtf8OrNull(data, size) ?? "");
            }
            var items = new List<RawItem>();
            ulong itemCount = ntk_clipboard_content_item_count(content).ToUInt64();
            for (ulong i = 0; i < itemCount; i++)
            {
                var index = new UIntPtr(i);
                string? text = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_clipboard_content_item_text_at(content, index, out UIntPtr textSize), textSize);
                string? html = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_clipboard_content_item_html_at(content, index, out UIntPtr htmlSize), htmlSize);
                string? uri = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_clipboard_content_item_uri_at(content, index, out UIntPtr uriSize), uriSize);
                string? coerced = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_clipboard_content_item_coerced_text_at(content, index, out UIntPtr coercedSize), coercedSize);
                items.Add(new RawItem(text, html, uri, coerced));
            }
            return ToClipContents(label, mimeTypes, items);
        }

        /// <summary>Reads every value out of an <c>ntk_clipboard_description</c>. Does not free it.</summary>
        internal static ClipDescriptionInfo ReadDescription(IntPtr description)
        {
            string? label = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_clipboard_description_label(description, out UIntPtr labelSize), labelSize);
            var mimeTypes = new List<string>();
            ulong mimeCount = ntk_clipboard_description_mime_type_count(description).ToUInt64();
            for (ulong i = 0; i < mimeCount; i++)
            {
                IntPtr data = ntk_clipboard_description_mime_type_at(description, new UIntPtr(i), out UIntPtr size);
                mimeTypes.Add(AndroidNativeToolkitCApi.ReadUtf8OrNull(data, size) ?? "");
            }
            return ToDescriptionInfo(
                label,
                mimeTypes,
                ntk_clipboard_description_is_styled_text(description),
                ntk_clipboard_description_classification_status(description));
        }

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_text(IntPtr text, ref CopyOptions options);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_html(IntPtr html, IntPtr plainText, ref CopyOptions options);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_uri(IntPtr uri, ref CopyOptions options);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_texts(IntPtr texts, UIntPtr count, ref CopyOptions options);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_clear();

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_read(out IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_has_clip(out int hasClip);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_get_description(out IntPtr description);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_start_observing();

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_stop_observing();

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_add_change_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_content_label(IntPtr content, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_clipboard_content_mime_type_count(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_content_mime_type_at(IntPtr content, UIntPtr index, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_clipboard_content_item_count(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_content_item_text_at(IntPtr content, UIntPtr index, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_content_item_html_at(IntPtr content, UIntPtr index, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_content_item_uri_at(IntPtr content, UIntPtr index, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_content_item_coerced_text_at(IntPtr content, UIntPtr index, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_clipboard_content_free(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_description_label(IntPtr description, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_clipboard_description_mime_type_count(IntPtr description);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_description_mime_type_at(IntPtr description, UIntPtr index, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_description_is_styled_text(IntPtr description);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_description_classification_status(IntPtr description);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_clipboard_description_free(IntPtr description);
#endif
    }
}
#endif
