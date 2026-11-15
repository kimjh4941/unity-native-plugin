#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Share
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;

    /// <summary>
    /// The bridge to native-toolkit's C ABI for the iOS share sheet (<c>Share.h</c> in IosLibraryCApi): the
    /// structs, the <c>extern</c> declarations, and the pure conversions between <see cref="IosShareManager"/>'s
    /// public API and the C ABI (artifact/ios/share, C ABI design v2).
    /// </summary>
    /// <remarks>
    /// The declarations compile in the Editor too, so the EditMode check compares them with the headers;
    /// they are called on an iOS player only. Reading a result handle is compiled into an iOS player only.
    /// </remarks>
    internal static class IosShareCApi
    {
        // ntk_share_item_kind (Share.h).
        internal const int KindText = 0;
        internal const int KindUrl = 1;
        internal const int KindImageFile = 2;
        internal const int KindFile = 3;

        // ntk_share_error (Share.h). 8 and 10-16 are not used; there is no CANCELED.
        internal const int ErrorNone = 0;
        internal const int ErrorInvalidParameter = 1;
        internal const int ErrorNotInitialized = 2;
        internal const int ErrorNotSupported = 3;
        internal const int ErrorUnknown = 4;
        internal const int ErrorOutOfMemory = 5;
        internal const int ErrorNotForeground = 6;
        internal const int ErrorEmptyContent = 7;
        internal const int ErrorFileNotFound = 9;
        internal const int ErrorInvalidUri = 17;
        internal const int ErrorImageLoadFailed = 18;
        internal const int ErrorShowFailed = 19;
        internal const int ErrorCanceledBySystem = 20;
        internal const int ErrorActivityFailed = 21;

        /// <summary><c>ntk_share_result_fn</c>: unlike Dialog's, it carries no request ID (design v2, D-9).</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ResultCallback(IntPtr userData, int error, uint systemCode, IntPtr result);

        /// <summary><c>ntk_share_item</c> (pack 8, 24 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ShareItem
        {
            public uint StructSize;
            public uint Reserved0;
            public int Kind;
            public uint Reserved1;
            public IntPtr Value;
        }

        /// <summary><c>ntk_share_request</c> (pack 8, 56 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ShareRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Items;
            public UIntPtr ItemCount;
            public IntPtr Subject;
            public IntPtr PreviewTitle;
            public IntPtr ExcludedActivityTypes;
            public UIntPtr ExcludedActivityTypeCount;
        }

        // The C ABI (D-9). callback is a function pointer from Marshal.GetFunctionPointerForDelegate;
        // release is always NULL (D-1).

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern int ntk_share_content_async(ref ShareRequest request, IntPtr callback, IntPtr userData, IntPtr release);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern int ntk_share_result_completed(IntPtr result);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern IntPtr ntk_share_result_activity_type(IntPtr result, out UIntPtr size);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern void ntk_share_result_free(IntPtr result);

        /// <summary>1.x's message for a payload with nothing to share.</summary>
        internal const string NoItems = "No shareable items were provided.";

        /// <summary>D-6: the C ABI cannot be used (the Editor, or another major version linked). 1.x's Editor message.</summary>
        internal const string Unavailable = "iOS share is only available on an iOS device.";

        /// <summary>One item as the C ABI takes it.</summary>
        internal readonly struct Item
        {
            internal readonly int Kind;
            internal readonly string Value;

            internal Item(int kind, string value)
            {
                Kind = kind;
                Value = value;
            }
        }

        /// <summary>What <see cref="Prepare"/> made of a payload: the items to pass, and what was left out (for the warnings).</summary>
        internal sealed class Prepared
        {
            internal List<Item> Items { get; } = new();
            internal string? Subject { get; set; }
            internal string? PreviewTitle { get; set; }
            internal string[]? ExcludedActivityTypes { get; set; }
            internal int DroppedItems { get; set; }
            internal List<string> DroppedTypes { get; } = new();
            internal bool DroppedExcludedList { get; set; }
            internal bool HasNul { get; set; }
        }

        /// <summary>The C ABI kind of a 1.x <c>type</c>, or -1. Case-sensitive, as 1.x's Swift switch was.</summary>
        internal static int KindOf(string? type) => type switch
        {
            "text" => KindText,
            "url" => KindUrl,
            "image" => KindImageFile,
            "file" => KindFile,
            _ => -1,
        };

        /// <summary>
        /// The payload as 1.x passed it on (D-2 to D-5): null items, unknown types and null values are left
        /// out (1.x's builder and Swift parser dropped them); an excluded list holding a null is dropped whole
        /// (1.x's parser could not read it); a blank subject or preview title is left out (1.x's builder).
        /// </summary>
        internal static Prepared Prepare(IosShareContentPayload payload)
        {
            var prepared = new Prepared();
            foreach (IosShareItem? item in payload.items ?? Array.Empty<IosShareItem>())
            {
                if (item == null)
                {
                    prepared.DroppedItems++;
                    prepared.DroppedTypes.Add("<null>");
                    continue;
                }
                int kind = KindOf(item.type);
                if (kind < 0 || item.value == null)
                {
                    prepared.DroppedItems++;
                    prepared.DroppedTypes.Add(item.type ?? "<null>");
                    continue;
                }
                prepared.Items.Add(new Item(kind, item.value));
                prepared.HasNul |= HasNul(item.value);
            }

            prepared.Subject = string.IsNullOrWhiteSpace(payload.subject) ? null : payload.subject;
            prepared.PreviewTitle = string.IsNullOrWhiteSpace(payload.previewTitle) ? null : payload.previewTitle;
            prepared.HasNul |= HasNul(prepared.Subject) || HasNul(prepared.PreviewTitle);

            string[]? excluded = payload.excludedActivityTypes;
            if (excluded != null && excluded.Length > 0)
            {
                if (Array.Exists(excluded, e => e == null))
                {
                    prepared.DroppedExcludedList = true;
                }
                else
                {
                    prepared.ExcludedActivityTypes = excluded;
                    prepared.HasNul |= Array.Exists(excluded, HasNul);
                }
            }
            return prepared;
        }

        /// <summary>A value the C string ends early in (D-5: the library reads up to the first U+0000).</summary>
        internal static bool HasNul(string? value) => value != null && value.IndexOf('\0') >= 0;

        /// <summary>The warnings of <see cref="Prepare"/>, without values or paths (D-2, D-3, D-5).</summary>
        internal static IEnumerable<string> Warnings(Prepared prepared)
        {
            if (prepared.DroppedItems > 0)
                yield return $"Share: left out {prepared.DroppedItems} item(s) of type {string.Join(", ", prepared.DroppedTypes)} (null, unknown type or null value), as 1.x did.";
            if (prepared.DroppedExcludedList)
                yield return "Share: excludedActivityTypes holds null, so the whole list is ignored, as 1.x did.";
            if (prepared.HasNul)
                yield return "Share: a value holds U+0000; the native side reads it only up to that character.";
        }

        /// <summary>
        /// The request (D-10): the items as one block of <c>ntk_share_item</c>s, every string as UTF-8. All of it
        /// is recorded in <paramref name="allocated"/>; the library copies it during the call.
        /// </summary>
        internal static ShareRequest BuildRequest(Prepared prepared, List<IntPtr> allocated)
        {
            int itemSize = Marshal.SizeOf<ShareItem>();
            IntPtr items = IosNativeToolkitCApi.Alloc(itemSize * Math.Max(prepared.Items.Count, 1), allocated);
            for (int i = 0; i < prepared.Items.Count; i++)
            {
                var item = new ShareItem
                {
                    StructSize = (uint)itemSize,
                    Kind = prepared.Items[i].Kind,
                    Value = IosNativeToolkitCApi.AllocUtf8(prepared.Items[i].Value, allocated),
                };
                Marshal.StructureToPtr(item, IntPtr.Add(items, i * itemSize), false);
            }

            string[]? excluded = prepared.ExcludedActivityTypes;
            return new ShareRequest
            {
                StructSize = (uint)Marshal.SizeOf<ShareRequest>(),
                Items = items,
                ItemCount = new UIntPtr((uint)prepared.Items.Count),
                Subject = IosNativeToolkitCApi.AllocUtf8(prepared.Subject, allocated),
                PreviewTitle = IosNativeToolkitCApi.AllocUtf8(prepared.PreviewTitle, allocated),
                ExcludedActivityTypes = excluded == null ? IntPtr.Zero : IosNativeToolkitCApi.AllocStringArray(excluded, allocated),
                ExcludedActivityTypeCount = new UIntPtr((uint)(excluded?.Length ?? 0)),
            };
        }

        /// <summary>The <c>ntk_share_error</c> name of a value, or the number when it is not one.</summary>
        internal static string ErrorName(int error) => error switch
        {
            ErrorNone => "NONE",
            ErrorInvalidParameter => "INVALID_PARAMETER",
            ErrorNotInitialized => "NOT_INITIALIZED",
            ErrorNotSupported => "NOT_SUPPORTED",
            ErrorUnknown => "UNKNOWN",
            ErrorOutOfMemory => "OUT_OF_MEMORY",
            ErrorNotForeground => "NOT_FOREGROUND",
            ErrorEmptyContent => "EMPTY_CONTENT",
            ErrorFileNotFound => "FILE_NOT_FOUND",
            ErrorInvalidUri => "INVALID_URI",
            ErrorImageLoadFailed => "IMAGE_LOAD_FAILED",
            ErrorShowFailed => "SHOW_FAILED",
            ErrorCanceledBySystem => "CANCELED_BY_SYSTEM",
            ErrorActivityFailed => "ACTIVITY_FAILED",
            _ => error.ToString(),
        };

        /// <summary>D-6: the library rejected the request (no completion comes).</summary>
        internal static string EntryFailureMessage(int error) => $"Failed to share: the request was rejected ({ErrorName(error)}).";

        /// <summary>D-8: 1.x's messages without the input URL, path or OS reason.</summary>
        internal static string CompletionFailureMessage(int error) => error switch
        {
            ErrorInvalidUri => "Invalid URL.",
            ErrorImageLoadFailed => "Failed to load image.",
            ErrorFileNotFound => "File not found.",
            ErrorNotForeground => "No root view controller available to present the share sheet.",
            ErrorShowFailed => "Failed to present the share sheet.",
            ErrorActivityFailed => "Failed to present the share sheet: the share activity failed.",
            ErrorCanceledBySystem => "The share sheet was dismissed without a result.",
            _ => $"Failed to share: {ErrorName(error)}.",
        };

        /// <summary>The result of a completion (D-7, D-8).</summary>
        internal static IosShareResult ToResult(int error, bool completed, string? activityType) =>
            error == ErrorNone ? IosShareResult.Success(completed, activityType) : IosShareResult.Failure(CompletionFailureMessage(error));

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>Reads a completion. The caller frees the handle. A NONE without a handle is UNKNOWN.</summary>
        internal static IosShareResult ReadResult(int error, IntPtr result)
        {
            if (error != ErrorNone) return ToResult(error, false, null);
            if (result == IntPtr.Zero) return ToResult(ErrorUnknown, false, null);
            bool completed = ntk_share_result_completed(result) != 0;
            IntPtr type = ntk_share_result_activity_type(result, out UIntPtr size);
            return ToResult(ErrorNone, completed, IosNativeToolkitCApi.ReadUtf8OrNull(type, size));
        }
#endif
    }
}
#endif
