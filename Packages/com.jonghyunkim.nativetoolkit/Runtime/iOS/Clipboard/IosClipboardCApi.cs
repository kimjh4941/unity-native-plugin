#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;

    /// <summary>
    /// The bridge to native-toolkit's C ABI for the iOS clipboard (<c>Clipboard.h</c> in IosLibraryCApi): the
    /// structs, the <c>extern</c> declarations, and the pure conversions between <see cref="IosClipboardManager"/>'s
    /// public API and the C ABI (artifact/ios/clipboard, C ABI design).
    /// </summary>
    /// <remarks>
    /// The declarations compile in the Editor too, so the EditMode check compares them with the headers; they
    /// are called on an iOS player only. Reading the result handles is compiled into an iOS player only.
    /// </remarks>
    internal static class IosClipboardCApi
    {
        /// <summary>Which operation a completion belongs to, passed as <c>user_data</c> (design D-2). Zero is not used.</summary>
        internal enum Op
        {
            Copy = 1,
            Append = 2,
            Clear = 3,
            RemovePasteboard = 4,
            StartObserving = 5,
            StopObserving = 6,
            Read = 7,
            ReadData = 8,
            GetSnapshot = 9,
            CreatePasteboard = 10,
            DetectPatterns = 11,
            DetectValues = 12,
            LoadItem = 13,
            CheckForegroundChange = 14,
        }

        // ntk_clipboard_error (Clipboard.h). 10 and 11 are not used.
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
        internal const int ErrorEmptyPatterns = 12;
        internal const int ErrorInvalidType = 13;
        internal const int ErrorInvalidName = 14;
        internal const int ErrorInvalidColor = 15;
        internal const int ErrorInvalidImageData = 16;
        internal const int ErrorInvalidExpiration = 17;
        internal const int ErrorContentTooLarge = 18;
        internal const int ErrorFileNotFound = 19;
        internal const int ErrorImageLoadFailed = 20;
        internal const int ErrorImageEncodeFailed = 21;
        internal const int ErrorCannotRemoveGeneral = 22;
        internal const int ErrorNoMatchingItem = 23;
        internal const int ErrorLoadFailed = 24;
        internal const int ErrorUnexpectedType = 25;
        internal const int ErrorFileCopyFailed = 26;
        internal const int ErrorCanceled = 27;
        internal const int ErrorTimedOut = 28;
        internal const int ErrorDetectionFailed = 29;

        // ntk_clipboard_scope_kind, ntk_clipboard_content_kind, ntk_clipboard_item_kind, ntk_clipboard_change_event_kind.
        internal const int ScopeGeneral = 0;
        internal const int ScopeNamed = 1;
        internal const int ScopeUnique = 2;
        internal const int ItemText = 0;
        internal const int ItemUrl = 1;
        internal const int ItemImage = 2;
        internal const int ItemFile = 3;
        internal const int ChangeChanged = 0;
        internal const int ChangeRemoved = 1;

        // The callbacks of Clipboard.h (all on the main thread).
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void DoneCallback(IntPtr userData, int error, uint systemCode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void HandleCallback(IntPtr userData, int error, uint systemCode, IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void PatternsCallback(IntPtr userData, int error, uint systemCode, uint patterns);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void LoadCallback(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr item);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ForegroundChangeCallback(IntPtr userData, int error, uint systemCode, int changed);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ChangeCallback(IntPtr userData, IntPtr change);
        /// <summary><c>ntk_release_fn</c> (Common.h): the listener's registration was dropped.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ReleaseCallback(IntPtr userData);

        /// <summary><c>ntk_clipboard_scope</c> (pack 8, 24 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Scope
        {
            public uint StructSize;
            public uint Reserved0;
            public int Kind;
            public uint Reserved1;
            public IntPtr Name;
        }

        /// <summary><c>ntk_clipboard_representation</c> (pack 8, 32 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Representation
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr UtType;
            public IntPtr Data;
            public UIntPtr DataSize;
        }

        /// <summary><c>ntk_clipboard_write_content</c> (pack 8, 136 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct WriteContent
        {
            public uint StructSize;
            public uint Reserved0;
            public int Kind;
            public uint Reserved1;
            public IntPtr Text;
            public IntPtr Html;
            public IntPtr Url;
            public IntPtr Path;
            public IntPtr Data;
            public UIntPtr DataSize;
            public IntPtr UtType;
            public double Red;
            public double Green;
            public double Blue;
            public double Alpha;
            public IntPtr Texts;
            public UIntPtr TextCount;
            public IntPtr Representations;
            public UIntPtr RepresentationCount;
        }

        /// <summary><c>ntk_clipboard_copy_options</c> (pack 8, 24 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct CopyOptions
        {
            public uint StructSize;
            public uint Reserved0;
            public int NotLocalOnly;
            public int HasExpiration;
            public double ExpirationUnixSeconds;
        }

        // ── The C ABI (only the functions the manager uses). Pointers to structs are IntPtr (NULL is allowed
        // for scope and options); callbacks are function pointers; release is always NULL. ──

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_copy_async(IntPtr content, IntPtr options, IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_append_async(IntPtr content, IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_read_async(IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_read_data_async(IntPtr utType, IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_get_snapshot_async(IntPtr matchingTypes, UIntPtr matchingTypeCount, IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_clear_async(IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_create_pasteboard_async(int kind, IntPtr name, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_remove_pasteboard_async(IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_detect_patterns_async(uint patterns, IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_detect_values_async(uint patterns, IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_load_item_async(int kind, IntPtr utType, IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release, out ulong requestId);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_cancel_load(ulong requestId);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_start_observing_async(IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_stop_observing_async(IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_check_foreground_change_async(IntPtr scope, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_add_change_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_clipboard_listener_remove(IntPtr listener);

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_clipboard_read_result_item_count(IntPtr result);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_clipboard_read_result_item_type_count(IntPtr result, UIntPtr item);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_read_result_item_type_at(IntPtr result, UIntPtr item, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_read_result_item_text_at(IntPtr result, UIntPtr item, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_read_result_item_url_at(IntPtr result, UIntPtr item, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_read_result_item_image_type_at(IntPtr result, UIntPtr item, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_clipboard_read_result_free(IntPtr result);

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_snapshot_has_strings(IntPtr snapshot);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_snapshot_has_urls(IntPtr snapshot);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_snapshot_has_images(IntPtr snapshot);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_snapshot_has_colors(IntPtr snapshot);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_clipboard_snapshot_number_of_items(IntPtr snapshot);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_clipboard_snapshot_type_count(IntPtr snapshot);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_snapshot_type_at(IntPtr snapshot, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_clipboard_snapshot_item_type_count(IntPtr snapshot, UIntPtr item);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_snapshot_item_type_at(IntPtr snapshot, UIntPtr item, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_snapshot_has_matching(IntPtr snapshot);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_clipboard_snapshot_matching_count(IntPtr snapshot);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern long ntk_clipboard_snapshot_matching_at(IntPtr snapshot, UIntPtr index);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_clipboard_snapshot_free(IntPtr snapshot);

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_loaded_item_kind(IntPtr item);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_loaded_item_text(IntPtr item, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_loaded_item_url(IntPtr item, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_loaded_item_image_data(IntPtr item, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_loaded_item_image_type(IntPtr item, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_loaded_item_file_path(IntPtr item, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_clipboard_loaded_item_free(IntPtr item);

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_change_kind(IntPtr change);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_clipboard_change_scope_kind(IntPtr change);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_change_scope_name(IntPtr change, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_clipboard_change_types_added_count(IntPtr change);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_change_types_added_at(IntPtr change, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_clipboard_change_types_removed_count(IntPtr change);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_clipboard_change_types_removed_at(IntPtr change, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_clipboard_change_free(IntPtr change);

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_string_data(IntPtr s);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_string_size(IntPtr s);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_string_free(IntPtr s);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_bytes_data(IntPtr b);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_bytes_size(IntPtr b);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_bytes_free(IntPtr b);

        // ── Errors (design D-3) ─────────────────────────────────────────────

        /// <summary>
        /// The 1.x code of a C ABI error: <c>CLIPBOARD_&lt;name&gt;</c>, with 1.x's three different names
        /// (native-toolkit part 2, 11.3, read backwards). An unknown value is <c>CLIPBOARD_UNKNOWN</c>.
        /// </summary>
        internal static string ErrorCode(int error) => error switch
        {
            ErrorInvalidParameter => "CLIPBOARD_INVALID_REQUEST",
            ErrorNotSupported => "CLIPBOARD_NOT_SUPPORTED",
            ErrorOutOfMemory => "CLIPBOARD_OUT_OF_MEMORY",
            ErrorNotInitialized => "CLIPBOARD_NOT_INITIALIZED",
            ErrorEmptyContent => "CLIPBOARD_EMPTY_CONTENT",
            ErrorEmptyItems => "CLIPBOARD_EMPTY_ITEMS",
            ErrorInvalidUri => "CLIPBOARD_INVALID_URL",
            ErrorUnavailable => "CLIPBOARD_UNAVAILABLE",
            ErrorEmptyPatterns => "CLIPBOARD_EMPTY_PATTERNS",
            ErrorInvalidType => "CLIPBOARD_INVALID_TYPE",
            ErrorInvalidName => "CLIPBOARD_INVALID_NAME",
            ErrorInvalidColor => "CLIPBOARD_INVALID_COLOR",
            ErrorInvalidImageData => "CLIPBOARD_INVALID_IMAGE_DATA",
            ErrorInvalidExpiration => "CLIPBOARD_INVALID_EXPIRATION",
            ErrorContentTooLarge => "CLIPBOARD_CONTENT_TOO_LARGE",
            ErrorFileNotFound => "CLIPBOARD_FILE_NOT_FOUND",
            ErrorImageLoadFailed => "CLIPBOARD_IMAGE_LOAD_FAILED",
            ErrorImageEncodeFailed => "CLIPBOARD_IMAGE_ENCODE_FAILED",
            ErrorCannotRemoveGeneral => "CLIPBOARD_CANNOT_REMOVE_GENERAL",
            ErrorNoMatchingItem => "CLIPBOARD_NO_MATCHING_ITEM",
            ErrorLoadFailed => "CLIPBOARD_LOAD_FAILED",
            ErrorUnexpectedType => "CLIPBOARD_UNEXPECTED_TYPE",
            ErrorFileCopyFailed => "CLIPBOARD_FILE_COPY_FAILED",
            ErrorCanceled => "CLIPBOARD_CANCELLED",
            ErrorTimedOut => "CLIPBOARD_TIMED_OUT",
            ErrorDetectionFailed => "CLIPBOARD_DETECTION_FAILED",
            _ => IosClipboardErrorInfo.UnknownErrorCode,
        };

        /// <summary>
        /// The library's fixed message for a C ABI error (native-toolkit <c>ClipboardError.swift</c>,
        /// <c>errorDescription</c>; the C ABI carries the number only). The entry-only errors get messages of
        /// their own; an unknown value gets <see cref="IosClipboardErrorInfo.UnknownErrorMessage"/>.
        /// </summary>
        internal static string ErrorMessage(int error) => error switch
        {
            ErrorEmptyContent => "Clipboard content is empty. Please provide text or HTML.",
            ErrorEmptyItems => "No items provided for clipboard copy.",
            ErrorEmptyPatterns => "No detection patterns were specified.",
            ErrorInvalidUri => "The URL is invalid.",
            ErrorInvalidType => "The uniform type identifier is invalid.",
            ErrorInvalidName => "The pasteboard name is invalid.",
            ErrorInvalidColor => "Color components must be finite and within 0.0...1.0.",
            ErrorInvalidImageData => "The provided image data could not be decoded.",
            ErrorInvalidExpiration => "expirationDate must be in the future.",
            ErrorInvalidParameter => "The request is invalid.",
            ErrorContentTooLarge => "The clipboard content exceeds the configured size limit.",
            ErrorFileNotFound => "The requested file was not found.",
            ErrorImageLoadFailed => "Failed to load the image.",
            ErrorImageEncodeFailed => "Failed to encode the pasted image.",
            ErrorUnavailable => "The requested pasteboard is unavailable.",
            ErrorCannotRemoveGeneral => "The general pasteboard cannot be removed.",
            ErrorNoMatchingItem => "No clipboard item matches the requested type.",
            ErrorLoadFailed => "Failed to load the clipboard item.",
            ErrorUnexpectedType => "The clipboard item could not be converted to the requested type.",
            ErrorFileCopyFailed => "Failed to copy the pasted file.",
            ErrorCanceled => "The clipboard load was cancelled.",
            ErrorTimedOut => "The clipboard operation timed out.",
            ErrorDetectionFailed => "Pattern detection failed.",
            ErrorNotSupported => "The request is not supported by this version of the native library.",
            _ => IosClipboardErrorInfo.UnknownErrorMessage,
        };

        internal static IosClipboardErrorInfo ToErrorInfo(int error) => IosClipboardErrorInfo.Create(ErrorCode(error), ErrorMessage(error));

        // ── Detection patterns ────────────────────────────────────────────

        /// <summary>
        /// <c>ntk_clipboard_patterns</c>: the pattern of bit n is at index n (design D-8 (4)). A table, not a
        /// shift, so a value outside the enum is not taken for another pattern.
        /// </summary>
        private static readonly IosClipboardDetectionPattern[] s_patternOfBit =
        {
            IosClipboardDetectionPattern.ProbableWebUrl,
            IosClipboardDetectionPattern.ProbableWebSearch,
            IosClipboardDetectionPattern.Number,
            IosClipboardDetectionPattern.Link,
            IosClipboardDetectionPattern.EmailAddress,
            IosClipboardDetectionPattern.PhoneNumber,
            IosClipboardDetectionPattern.PostalAddress,
            IosClipboardDetectionPattern.CalendarEvent,
            IosClipboardDetectionPattern.FlightNumber,
            IosClipboardDetectionPattern.MoneyAmount,
            IosClipboardDetectionPattern.ShipmentTrackingNumber,
        };

        /// <summary>The bit set of the patterns. False when one of them is not in the table.</summary>
        internal static bool TryPatternBits(IosClipboardDetectionPattern[] patterns, out uint bits)
        {
            bits = 0;
            foreach (IosClipboardDetectionPattern pattern in patterns)
            {
                int bit = Array.IndexOf(s_patternOfBit, pattern);
                if (bit < 0) return false;
                bits |= 1u << bit;
            }
            return true;
        }

        /// <summary>The patterns of a bit set, from the lowest bit. Unknown bits are left out.</summary>
        internal static IReadOnlyList<IosClipboardDetectionPattern> PatternsOf(uint bits)
        {
            var patterns = new List<IosClipboardDetectionPattern>();
            for (int bit = 0; bit < s_patternOfBit.Length; bit++)
            {
                if ((bits & (1u << bit)) != 0) patterns.Add(s_patternOfBit[bit]);
            }
            return patterns;
        }

        // ── U+0000 (design D-8 (5)). A C string ends at U+0000. ──

        internal static bool HasNul(string? value) => value != null && value.IndexOf('\0') >= 0;

        /// <summary>A scope whose name holds U+0000 (it would name another pasteboard).</summary>
        internal static bool ScopeHasNul(IosPasteboardScope? scope) => scope != null && HasNul(scope.Name);

        /// <summary>
        /// The type identifiers (ImageData, CustomData, representations) and the image path of content that
        /// hold U+0000: rejected, as names are. A path cut at U+0000 could name another, existing file, and
        /// Copy puts it where other apps can read it (design v3, D-8 (5)).
        /// </summary>
        internal static bool ContentTypeHasNul(IosClipboardContent content)
        {
            if (HasNul(content.UtType) || HasNul(content.Path)) return true;
            if (content.Representations != null)
            {
                foreach (string key in content.Representations.Keys)
                {
                    if (HasNul(key)) return true;
                }
            }
            return false;
        }

        /// <summary>Values of content that hold U+0000: passed on, cut at the U+0000, with a warning.</summary>
        internal static bool ContentValueHasNul(IosClipboardContent content) =>
            HasNul(content.Text) || HasNul(content.Plain) || HasNul(content.Html) || HasNul(content.UrlString) ||
            (content.Texts != null && Array.Exists(content.Texts, HasNul));

        /// <summary>The warning for a value cut at U+0000. It names no value.</summary>
        internal const string NulValueWarning =
            "A clipboard value contains U+0000; the native library receives it up to the U+0000.";

        // ── Building the requests (design D-8). Everything is recorded in `allocated`; the library copies it
        // during the call. ──

        internal static IntPtr AllocStruct<T>(T value, List<IntPtr> allocated) where T : struct
        {
            IntPtr pointer = IosNativeToolkitCApi.Alloc(Marshal.SizeOf<T>(), allocated);
            Marshal.StructureToPtr(value, pointer, false);
            return pointer;
        }

        internal static IntPtr AllocBytes(byte[] bytes, List<IntPtr> allocated)
        {
            IntPtr pointer = IosNativeToolkitCApi.Alloc(Math.Max(bytes.Length, 1), allocated);
            if (bytes.Length > 0) Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        /// <summary>A scope, or NULL for the general pasteboard (and for no scope).</summary>
        internal static IntPtr BuildScope(IosPasteboardScope? scope, List<IntPtr> allocated)
        {
            if (scope == null || scope.Kind == IosPasteboardScopeKind.General) return IntPtr.Zero;
            return AllocStruct(new Scope
            {
                StructSize = (uint)Marshal.SizeOf<Scope>(),
                Kind = scope.Kind == IosPasteboardScopeKind.Named ? ScopeNamed : ScopeUnique,
                Name = IosNativeToolkitCApi.AllocUtf8(scope.Name, allocated),
            }, allocated);
        }

        /// <summary>A scope that may not be NULL (remove): the general pasteboard is passed as GENERAL.</summary>
        internal static IntPtr BuildRequiredScope(IosPasteboardScope scope, List<IntPtr> allocated)
        {
            if (scope.Kind != IosPasteboardScopeKind.General) return BuildScope(scope, allocated);
            return AllocStruct(new Scope { StructSize = (uint)Marshal.SizeOf<Scope>(), Kind = ScopeGeneral }, allocated);
        }

        internal static WriteContent BuildContentStruct(IosClipboardContent content, List<IntPtr> allocated)
        {
            var c = new WriteContent { StructSize = (uint)Marshal.SizeOf<WriteContent>(), Kind = (int)content.Kind };
            switch (content.Kind)
            {
                case IosClipboardContentKind.PlainText:
                    c.Text = IosNativeToolkitCApi.AllocUtf8(content.Text, allocated);
                    break;
                case IosClipboardContentKind.HtmlText:
                    c.Text = IosNativeToolkitCApi.AllocUtf8(content.Plain, allocated);
                    c.Html = IosNativeToolkitCApi.AllocUtf8(content.Html, allocated);
                    break;
                case IosClipboardContentKind.Url:
                    c.Url = IosNativeToolkitCApi.AllocUtf8(content.UrlString, allocated);
                    break;
                case IosClipboardContentKind.ImageFile:
                    c.Path = IosNativeToolkitCApi.AllocUtf8(content.Path, allocated);
                    break;
                case IosClipboardContentKind.ImageData:
                case IosClipboardContentKind.CustomData:
                    byte[] data = content.Data ?? Array.Empty<byte>();
                    c.Data = AllocBytes(data, allocated);
                    c.DataSize = new UIntPtr((uint)data.Length);
                    c.UtType = IosNativeToolkitCApi.AllocUtf8(content.UtType, allocated);
                    break;
                case IosClipboardContentKind.Color:
                    c.Red = content.Red;
                    c.Green = content.Green;
                    c.Blue = content.Blue;
                    c.Alpha = content.Alpha;
                    break;
                case IosClipboardContentKind.MultipleText:
                    string[] texts = content.Texts ?? Array.Empty<string>();
                    c.Texts = IosNativeToolkitCApi.AllocStringArray(texts, allocated);
                    c.TextCount = new UIntPtr((uint)texts.Length);
                    break;
                case IosClipboardContentKind.MultiRepresentation:
                    var representations = new List<KeyValuePair<string, byte[]>>(content.Representations ?? new Dictionary<string, byte[]>());
                    int size = Marshal.SizeOf<Representation>();
                    IntPtr array = IosNativeToolkitCApi.Alloc(size * Math.Max(representations.Count, 1), allocated);
                    for (int i = 0; i < representations.Count; i++)
                    {
                        byte[] bytes = representations[i].Value ?? Array.Empty<byte>();
                        Marshal.StructureToPtr(new Representation
                        {
                            StructSize = (uint)size,
                            UtType = IosNativeToolkitCApi.AllocUtf8(representations[i].Key, allocated),
                            Data = AllocBytes(bytes, allocated),
                            DataSize = new UIntPtr((uint)bytes.Length),
                        }, IntPtr.Add(array, i * size), false);
                    }
                    c.Representations = array;
                    c.RepresentationCount = new UIntPtr((uint)representations.Count);
                    break;
            }
            return c;
        }

        internal static IntPtr BuildContent(IosClipboardContent content, List<IntPtr> allocated) =>
            AllocStruct(BuildContentStruct(content, allocated), allocated);

        /// <summary>The options of Copy: NULL for none (local only, no expiration, as the zero-filled struct).</summary>
        internal static CopyOptions BuildOptionsStruct(IosClipboardCopyOptions options) => new CopyOptions
        {
            StructSize = (uint)Marshal.SizeOf<CopyOptions>(),
            NotLocalOnly = options.LocalOnly ? 0 : 1,
            HasExpiration = options.ExpirationDate.HasValue ? 1 : 0,
            ExpirationUnixSeconds = options.ExpirationDate.HasValue ? UnixSeconds(options.ExpirationDate.Value) : 0,
        };

        /// <summary>
        /// Whole Unix seconds of a date, the fraction dropped toward the past, as 1.x sent
        /// <c>yyyy-MM-ddTHH:mm:ssZ</c> (design D-8 (2)). Counted in ticks, so a whole second stays whole.
        /// </summary>
        internal static double UnixSeconds(DateTime date)
        {
            long ticks = date.ToUniversalTime().Ticks - DateTime.UnixEpoch.Ticks;
            long seconds = ticks / TimeSpan.TicksPerSecond;
            if (ticks < 0 && ticks % TimeSpan.TicksPerSecond != 0) seconds--;
            return seconds;
        }

        internal static IntPtr BuildOptions(IosClipboardCopyOptions? options, List<IntPtr> allocated) =>
            options == null ? IntPtr.Zero : AllocStruct(BuildOptionsStruct(options), allocated);

        /// <summary>The <c>ntk_clipboard_item_kind</c> of a load request.</summary>
        internal static int ItemKindOf(IosClipboardLoadRequestKind kind) => kind switch
        {
            IosClipboardLoadRequestKind.Text => ItemText,
            IosClipboardLoadRequestKind.Url => ItemUrl,
            IosClipboardLoadRequestKind.Image => ItemImage,
            _ => ItemFile,
        };

        // ── Results (design D-9, table 2.1). Pure: the player readers below fetch the values from the handles,
        // these build the public results, so EditMode can check them (D-13). ──

        /// <summary>The largest ReadData that is passed on: 1.x's limit (64 MiB). Larger is CONTENT_TOO_LARGE.</summary>
        internal const long MaxReadDataBytes = IosClipboardJsonParser.MaxDataByteCount;

        /// <summary>A completion of copy, append, clear, remove, start or stop observing.</summary>
        internal static IosClipboardOperationResult OperationResultOf(string operation, int error) =>
            error == ErrorNone
                ? IosClipboardOperationResult.Success(operation)
                : IosClipboardOperationResult.Failure(operation, ToErrorInfo(error));

        /// <summary>The error of a completion that reported NONE without the handle it promised (D-9 (2)).</summary>
        internal static IosClipboardErrorInfo MissingResult => ToErrorInfo(ErrorUnknown);

        /// <summary>
        /// ReadData: no bytes is a success with no data; more than <see cref="MaxReadDataBytes"/> is 1.x's
        /// CONTENT_TOO_LARGE, without copying; otherwise the bytes, with the type that was asked for (the C ABI
        /// does not return it).
        /// </summary>
        internal static IosClipboardReadDataResult ReadDataResultOf(string utType, bool hasData, ulong size, Func<byte[]> copy)
        {
            if (!hasData) return IosClipboardReadDataResult.NoData();
            if (size > (ulong)MaxReadDataBytes) return IosClipboardReadDataResult.Failure(ToErrorInfo(ErrorContentTooLarge));
            return IosClipboardReadDataResult.Success(utType, copy());
        }

        /// <summary>
        /// A snapshot. <paramref name="allTypes"/> has one row per item (<c>number_of_items</c>). The matching
        /// indexes are null when no matching was asked for; negative values (out of range) are left out.
        /// </summary>
        internal static IosClipboardSnapshot SnapshotOf(
            bool hasStrings, bool hasUrls, bool hasImages, bool hasColors, int numberOfItems,
            IReadOnlyList<string> types, IReadOnlyList<IReadOnlyList<string>> allTypes,
            bool hasMatching, IReadOnlyList<long> matching)
        {
            List<int>? indexes = null;
            if (hasMatching)
            {
                indexes = new List<int>(matching.Count);
                foreach (long index in matching)
                {
                    if (index >= 0 && index <= int.MaxValue) indexes.Add((int)index);
                }
            }
            return new IosClipboardSnapshot(hasStrings, hasUrls, hasImages, hasColors, numberOfItems, types, allTypes, indexes);
        }

        /// <summary>The created pasteboard: the kind from the request, the name from the C ABI.</summary>
        internal static IosPasteboardScope CreatedScopeOf(IosPasteboardCreationRequestKind kind, string name) =>
            kind == IosPasteboardCreationRequestKind.Named ? IosPasteboardScope.Named(name) : IosPasteboardScope.Unique(name);

        /// <summary>A loaded item by its <c>ntk_clipboard_item_kind</c>; -1 and unknown kinds are an unknown item.</summary>
        internal static IosClipboardLoadedItem LoadedItemOf(int kind, string? text, string? url, byte[]? imageData, string? imageType, string? path) =>
            kind switch
            {
                ItemText => IosClipboardLoadedItem.FromText(text ?? ""),
                ItemUrl => IosClipboardLoadedItem.FromUrl(url ?? ""),
                ItemImage => IosClipboardLoadedItem.FromImageData(imageData ?? Array.Empty<byte>(), imageType ?? ""),
                ItemFile => IosClipboardLoadedItem.FromFile(path ?? ""),
                _ => IosClipboardLoadedItem.UnknownKind(),
            };

        /// <summary>The scope of a change: GENERAL, or NAMED/UNIQUE with a name; anything else (-1) is null.</summary>
        internal static IosPasteboardScope? ScopeOf(int kind, string? name) => kind switch
        {
            ScopeGeneral => IosPasteboardScope.General,
            ScopeNamed when name != null => IosPasteboardScope.Named(name),
            ScopeUnique when name != null => IosPasteboardScope.Unique(name),
            _ => null,
        };

        internal static IosClipboardChangeEventKind ChangeKindOf(int kind) => kind switch
        {
            ChangeChanged => IosClipboardChangeEventKind.Changed,
            ChangeRemoved => IosClipboardChangeEventKind.Removed,
            _ => IosClipboardChangeEventKind.Unknown,
        };

        internal static IosClipboardChangeEvent ChangeOf(int kind, int scopeKind, string? scopeName,
            IReadOnlyList<string> added, IReadOnlyList<string> removed) =>
            new(ChangeKindOf(kind), ScopeOf(scopeKind, scopeName), added, removed);

#if UNITY_IOS && !UNITY_EDITOR
        // ── Reading the handles (Player only). The caller frees the handle. ──

        private static string? Utf8(IntPtr data, UIntPtr size) => IosNativeToolkitCApi.ReadUtf8OrNull(data, size);

        private static int Count(UIntPtr value) => value.ToUInt64() > int.MaxValue ? int.MaxValue : (int)value.ToUInt64();

        private delegate IntPtr StringAt(UIntPtr index, out UIntPtr size);

        private static List<string> Strings(int count, StringAt at)
        {
            var list = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                string? value = Utf8(at(new UIntPtr((uint)i), out UIntPtr size), size);
                if (value != null) list.Add(value);
            }
            return list;
        }

        internal static IosClipboardReadResult ReadReadResult(IntPtr result)
        {
            int count = Count(ntk_clipboard_read_result_item_count(result));
            var items = new List<IosClipboardItem>(count);
            for (int i = 0; i < count; i++)
            {
                var item = new UIntPtr((uint)i);
                List<string> types = Strings(
                    Count(ntk_clipboard_read_result_item_type_count(result, item)),
                    (UIntPtr index, out UIntPtr size) => ntk_clipboard_read_result_item_type_at(result, item, index, out size));
                items.Add(new IosClipboardItem(
                    types,
                    Utf8(ntk_clipboard_read_result_item_text_at(result, item, out UIntPtr textSize), textSize),
                    Utf8(ntk_clipboard_read_result_item_url_at(result, item, out UIntPtr urlSize), urlSize),
                    Utf8(ntk_clipboard_read_result_item_image_type_at(result, item, out UIntPtr imageSize), imageSize)));
            }
            return IosClipboardReadResult.Success(count, items);
        }

        /// <summary>ReadData from its <c>ntk_bytes</c> (NULL: no data).</summary>
        internal static IosClipboardReadDataResult ReadReadData(IntPtr bytes, string utType)
        {
            ulong size = bytes == IntPtr.Zero ? 0 : ntk_bytes_size(bytes).ToUInt64();
            return ReadDataResultOf(utType, bytes != IntPtr.Zero, size, () =>
            {
                var copy = new byte[(int)size];
                if (size > 0) Marshal.Copy(ntk_bytes_data(bytes), copy, 0, (int)size);
                return copy;
            });
        }

        internal static string ReadString(IntPtr s) => Utf8(ntk_string_data(s), ntk_string_size(s)) ?? "";

        internal static IosClipboardSnapshot ReadSnapshot(IntPtr snapshot)
        {
            List<string> types = Strings(
                Count(ntk_clipboard_snapshot_type_count(snapshot)),
                (UIntPtr index, out UIntPtr size) => ntk_clipboard_snapshot_type_at(snapshot, index, out size));

            int numberOfItems = Count(ntk_clipboard_snapshot_number_of_items(snapshot));
            var all = new List<IReadOnlyList<string>>(numberOfItems);
            for (int i = 0; i < numberOfItems; i++)
            {
                var item = new UIntPtr((uint)i);
                all.Add(Strings(
                    Count(ntk_clipboard_snapshot_item_type_count(snapshot, item)),
                    (UIntPtr index, out UIntPtr size) => ntk_clipboard_snapshot_item_type_at(snapshot, item, index, out size)));
            }

            bool hasMatching = ntk_clipboard_snapshot_has_matching(snapshot) != 0;
            var matching = new List<long>();
            if (hasMatching)
            {
                int matchingCount = Count(ntk_clipboard_snapshot_matching_count(snapshot));
                for (int m = 0; m < matchingCount; m++)
                    matching.Add(ntk_clipboard_snapshot_matching_at(snapshot, new UIntPtr((uint)m)));
            }

            return SnapshotOf(
                ntk_clipboard_snapshot_has_strings(snapshot) != 0,
                ntk_clipboard_snapshot_has_urls(snapshot) != 0,
                ntk_clipboard_snapshot_has_images(snapshot) != 0,
                ntk_clipboard_snapshot_has_colors(snapshot) != 0,
                numberOfItems, types, all, hasMatching, matching);
        }

        internal static IosClipboardLoadedItem ReadLoadedItem(IntPtr item)
        {
            int kind = ntk_clipboard_loaded_item_kind(item);
            string? text = null, url = null, imageType = null, path = null;
            byte[]? image = null;
            switch (kind)
            {
                case ItemText:
                    text = Utf8(ntk_clipboard_loaded_item_text(item, out UIntPtr textSize), textSize);
                    break;
                case ItemUrl:
                    url = Utf8(ntk_clipboard_loaded_item_url(item, out UIntPtr urlSize), urlSize);
                    break;
                case ItemImage:
                {
                    IntPtr data = ntk_clipboard_loaded_item_image_data(item, out UIntPtr dataSize);
                    int size = Count(dataSize);
                    image = new byte[size];
                    if (size > 0 && data != IntPtr.Zero) Marshal.Copy(data, image, 0, size);
                    imageType = Utf8(ntk_clipboard_loaded_item_image_type(item, out UIntPtr typeSize), typeSize);
                    break;
                }
                case ItemFile:
                    path = Utf8(ntk_clipboard_loaded_item_file_path(item, out UIntPtr pathSize), pathSize);
                    break;
            }
            return LoadedItemOf(kind, text, url, image, imageType, path);
        }

        internal static IosClipboardChangeEvent ReadChange(IntPtr change)
        {
            List<string> added = Strings(
                Count(ntk_clipboard_change_types_added_count(change)),
                (UIntPtr index, out UIntPtr size) => ntk_clipboard_change_types_added_at(change, index, out size));
            List<string> removed = Strings(
                Count(ntk_clipboard_change_types_removed_count(change)),
                (UIntPtr index, out UIntPtr size) => ntk_clipboard_change_types_removed_at(change, index, out size));
            return ChangeOf(
                ntk_clipboard_change_kind(change),
                ntk_clipboard_change_scope_kind(change),
                Utf8(ntk_clipboard_change_scope_name(change, out UIntPtr nameSize), nameSize),
                added,
                removed);
        }
#endif
    }
}
#endif
