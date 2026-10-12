#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Share
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using JonghyunKim.NativeToolkit.Runtime.Android.Common;

    /// <summary>
    /// The bridge to native-toolkit's C ABI for sharing (<c>Share.h</c> in libntk.so): the structs,
    /// the <c>extern</c> declarations, and the pure conversions and checks between the manager's
    /// public API and the C ABI.
    /// </summary>
    /// <remarks>
    /// The design is artifact/android/share/designs/2026-10-11-android-share-design-v6.md (4.1 to 4.4).
    /// The conversions compile in the Editor too, so EditMode tests reach them. The <c>extern</c>s are
    /// compiled into an Android player only.
    /// </remarks>
    internal static class AndroidShareCApi
    {
        // ntk_share_error (Share.h).
        internal const int ErrorNone = 0;
        internal const int ErrorInvalidParameter = 1;
        internal const int ErrorNotInitialized = 2;
        internal const int ErrorNotSupported = 3;
        internal const int ErrorUnknown = 4;
        internal const int ErrorOutOfMemory = 5;
        internal const int ErrorNotForeground = 6;
        internal const int ErrorEmptyContent = 7;
        internal const int ErrorNoShareTarget = 8;
        internal const int ErrorFileNotFound = 9;
        internal const int ErrorIllegalFileAccess = 10;
        internal const int ErrorInvalidMimeType = 11;
        internal const int ErrorDirectShareRegistrationFailed = 12;
        internal const int ErrorEmptyIdList = 13;
        internal const int ErrorEmptyFileList = 14;
        internal const int ErrorInvalidIcon = 15;
        internal const int ErrorInvalidChooserAction = 16;

        /// <summary>The chooser action 1.x did not report when tapped (design v6, 4.2.1).</summary>
        internal const string SendAction = "android.intent.action.SEND";

        /// <summary>How many accepted selection requests are kept for the cancel (J-S13).</summary>
        internal const int MaxSelectionIds = 16;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void DoneCallback(IntPtr userData, int error, uint systemCode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ChooserActionCallback(IntPtr userData, IntPtr actionId);

        /// <summary>The selection event. The request ID is 64-bit (<c>uint64_t</c>).</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SelectionCallback(IntPtr userData, ulong requestId, IntPtr packageName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ReleaseCallback(IntPtr userData);

        /// <summary><c>ntk_share_text_content</c>: 56 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct TextContent
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Text;
            public IntPtr Title;
            public IntPtr Subject;
            public IntPtr MimeType;
            public IntPtr PreviewTitle;
            public IntPtr PreviewThumbnailPath;
        }

        /// <summary><c>ntk_share_chooser_action</c>: 32 bytes, no struct_size.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ChooserAction
        {
            public IntPtr Id;
            public IntPtr Label;
            public IntPtr Icon;
            public UIntPtr IconSize;
        }

        /// <summary><c>ntk_share_direct_target</c>: 48 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct DirectTarget
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Id;
            public IntPtr Label;
            public IntPtr Category;
            public IntPtr Icon;
            public UIntPtr IconSize;
        }

        /// <summary>
        /// What one call needs to word its result: the operation (an <c>AndroidShareManager.Operation*</c>
        /// value), and the paths, MIME type and Direct Share ID the messages name (design v6, J-S1).
        /// </summary>
        internal sealed class ShareCall
        {
            internal ShareCall(string operation, string[]? paths = null, string? mimeType = null, string? directTargetId = null)
            {
                Operation = operation;
                Paths = paths ?? Array.Empty<string>();
                MimeType = mimeType;
                DirectTargetId = directTargetId;
            }

            internal string Operation { get; }
            internal string[] Paths { get; }
            internal string? MimeType { get; }
            internal string? DirectTargetId { get; }
        }

        /// <summary>A chooser action as passed to the C ABI (4.2.1).</summary>
        internal readonly struct NormalizedAction
        {
            internal NormalizedAction(string id, string label, byte[] icon)
            {
                Id = id;
                Label = label;
                Icon = icon;
            }

            internal string Id { get; }
            internal string Label { get; }
            internal byte[] Icon { get; }
        }

        // ── Blank and absent (J-S17) ──────────────────────────────────────────

        /// <summary>
        /// Whether a required value is missing, as Kotlin's <c>isBlank</c> saw it in 1.x's parser: null,
        /// empty, or only Unicode space separators (Zs, Zl, Zp), U+0009 to U+000D and U+001C to U+001F.
        /// </summary>
        internal static bool IsBlank(string? value)
        {
            if (value == null) return true;
            foreach (char c in value)
            {
                if (!IsKotlinWhitespace(c)) return false;
            }
            return true;
        }

        /// <summary>Whether an optional value is left out: blank, or what C#'s IsNullOrWhiteSpace leaves out (1.x did both).</summary>
        internal static bool IsAbsent(string? value) => IsBlank(value) || string.IsNullOrWhiteSpace(value);

        private static bool IsKotlinWhitespace(char c)
        {
            if ((c >= '\u0009' && c <= '\u000D') || (c >= '\u001C' && c <= '\u001F')) return true;
            UnicodeCategory category = char.GetUnicodeCategory(c);
            return category == UnicodeCategory.SpaceSeparator
                || category == UnicodeCategory.LineSeparator
                || category == UnicodeCategory.ParagraphSeparator;
        }

        // ── Checks before the native call (4.2), with 1.x's messages ──────────

        internal static string? CheckText(string operation, string? text) =>
            IsBlank(text) ? $"Failed to {operation}: text is required" : null;

        internal static string? CheckPath(string operation, string? path) =>
            IsBlank(path) ? $"Failed to {operation}: filePath is required" : null;

        internal static string? CheckPaths(string operation, string[] paths)
        {
            if (paths.Length == 0) return $"Failed to {operation}: filePaths must not be empty";
            foreach (string? path in paths)
            {
                if (path == null) return $"Failed to {operation}: filePaths must not contain null";
            }
            return null;
        }

        internal static string? CheckIds(string[] ids)
        {
            const string operation = AndroidShareManager.OperationRemoveDirectShareTargets;
            if (ids.Length == 0) return $"Failed to {operation}: ids must not be empty";
            foreach (string? id in ids)
            {
                if (id == null) return $"Failed to {operation}: ids must not contain null";
            }
            return null;
        }

        /// <summary>
        /// Checks a Direct Share target in 1.x's order (id, label, iconBase64, then the icon's Base64)
        /// and hands back the decoded icon.
        /// </summary>
        internal static string? CheckDirectTarget(DirectShareTargetPayload payload, out byte[]? icon)
        {
            const string operation = AndroidShareManager.OperationRegisterDirectShareTarget;
            icon = null;
            if (IsBlank(payload.id)) return $"Failed to {operation}: id is required";
            if (IsBlank(payload.label)) return $"Failed to {operation}: label is required";
            if (IsBlank(payload.iconBase64)) return $"Failed to {operation}: iconBase64 is required";
            icon = DecodeIcon(payload.iconBase64);
            return icon == null ? $"Invalid icon data for Direct Share target: {payload.id}" : null;
        }

        /// <summary>Base64 with the surrounding white space removed, or null when it does not read (5.3).</summary>
        internal static byte[]? DecodeIcon(string? base64)
        {
            if (base64 == null) return null;
            try
            {
                return Convert.FromBase64String(base64.Trim());
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the bytes start like an image Android can read: PNG, JPEG, WebP, GIF, BMP, ICO, or
        /// HEIF / AVIF (<c>ftyp</c> from the fourth byte). Close to 1.x skipping what BitmapFactory
        /// could not read; an image that starts right but is broken fails the whole share (5.3).
        /// </summary>
        internal static bool LooksLikeImage(byte[] bytes)
        {
            if (StartsWith(bytes, 0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return true;
            if (StartsWith(bytes, 0, 0xFF, 0xD8, 0xFF)) return true;
            if (StartsWith(bytes, 0, (byte)'R', (byte)'I', (byte)'F', (byte)'F') && StartsWith(bytes, 8, (byte)'W', (byte)'E', (byte)'B', (byte)'P')) return true;
            if (StartsWith(bytes, 0, (byte)'G', (byte)'I', (byte)'F', (byte)'8')) return true;
            if (StartsWith(bytes, 0, (byte)'B', (byte)'M')) return true;
            if (StartsWith(bytes, 0, 0x00, 0x00, 0x01, 0x00)) return true;
            return StartsWith(bytes, 4, (byte)'f', (byte)'t', (byte)'y', (byte)'p');
        }

        private static bool StartsWith(byte[] bytes, int offset, params byte[] signature)
        {
            if (bytes.Length < offset + signature.Length) return false;
            for (int i = 0; i < signature.Length; i++)
            {
                if (bytes[offset + i] != signature[i]) return false;
            }
            return true;
        }

        /// <summary>A string as the C ABI receives it: cut at U+0000, lone surrogates as U+FFFD.</summary>
        internal static string AsPassed(string value)
        {
            int nul = value.IndexOf('\0');
            string cut = nul >= 0 ? value.Substring(0, nul) : value;
            return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(cut));
        }

        /// <summary>
        /// The chooser actions to pass, in order (4.2.1): drops those with a blank label, icon or
        /// intentAction, an icon that is not Base64 or does not start like an image, and a repeated
        /// intentAction (compared as passed). Keeps android.intent.action.SEND, which shows but is not
        /// reported when tapped. The elements must not be null (checked before).
        /// </summary>
        internal static List<NormalizedAction> NormalizeActions(ChooserActionPayload[]? actions)
        {
            var normalized = new List<NormalizedAction>();
            if (actions == null) return normalized;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ChooserActionPayload action in actions)
            {
                if (IsBlank(action.label) || IsBlank(action.iconBase64) || IsAbsent(action.intentAction)) continue;
                byte[]? icon = DecodeIcon(action.iconBase64);
                if (icon == null || !LooksLikeImage(icon)) continue;
                string id = AsPassed(action.intentAction!);
                if (id.Length == 0 || !seen.Add(id)) continue;
                normalized.Add(new NormalizedAction(id, action.label, icon));
            }
            return normalized;
        }

        /// <summary>Whether a tapped chooser action is reported (1.x did not report SEND).</summary>
        internal static bool IsNotifiedActionId(string actionId) => actionId != SendAction;

        // ── Building the structs ──────────────────────────────────────────────

        internal static IntPtr AllocOptional(string? value, List<IntPtr> allocated) =>
            IsAbsent(value) ? IntPtr.Zero : AndroidNativeToolkitCApi.AllocUtf8(value, allocated);

        internal static TextContent BuildTextContent(ShareTextPayload payload, List<IntPtr> allocated) => new()
        {
            StructSize = (uint)Marshal.SizeOf<TextContent>(),
            Reserved0 = 0,
            Text = AndroidNativeToolkitCApi.AllocUtf8(payload.text, allocated),
            Title = AllocOptional(payload.title, allocated),
            Subject = AllocOptional(payload.subject, allocated),
            MimeType = AllocOptional(payload.mimeType, allocated),
            PreviewTitle = AllocOptional(payload.previewTitle, allocated),
            PreviewThumbnailPath = AllocOptional(payload.previewThumbnailPath, allocated),
        };

        /// <summary>The actions as one block of <c>ntk_share_chooser_action</c>; never NULL, even for none.</summary>
        internal static IntPtr BuildActions(List<NormalizedAction> actions, List<IntPtr> allocated)
        {
            int size = Marshal.SizeOf<ChooserAction>();
            IntPtr block = AndroidNativeToolkitCApi.Alloc(size * Math.Max(actions.Count, 1), allocated);
            for (int i = 0; i < actions.Count; i++)
            {
                var action = new ChooserAction
                {
                    Id = AndroidNativeToolkitCApi.AllocUtf8(actions[i].Id, allocated),
                    Label = AndroidNativeToolkitCApi.AllocUtf8(actions[i].Label, allocated),
                    Icon = AllocBytes(actions[i].Icon, allocated),
                    IconSize = new UIntPtr((uint)actions[i].Icon.Length),
                };
                Marshal.StructureToPtr(action, block + i * size, false);
            }
            return block;
        }

        internal static DirectTarget BuildDirectTarget(DirectShareTargetPayload payload, byte[] icon, List<IntPtr> allocated) => new()
        {
            StructSize = (uint)Marshal.SizeOf<DirectTarget>(),
            Reserved0 = 0,
            Id = AndroidNativeToolkitCApi.AllocUtf8(payload.id, allocated),
            Label = AndroidNativeToolkitCApi.AllocUtf8(payload.label, allocated),
            Category = AllocOptional(payload.category, allocated),
            Icon = AllocBytes(icon, allocated),
            IconSize = new UIntPtr((uint)icon.Length),
        };

        /// <summary>A <c>const uint8_t*</c> copy of the bytes, recorded for FreeAll. Never NULL.</summary>
        internal static IntPtr AllocBytes(byte[] bytes, List<IntPtr> allocated)
        {
            IntPtr pointer = AndroidNativeToolkitCApi.Alloc(Math.Max(bytes.Length, 1), allocated);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        // ── Messages (4.4) ────────────────────────────────────────────────────

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
            ErrorNoShareTarget => "NO_SHARE_TARGET",
            ErrorFileNotFound => "FILE_NOT_FOUND",
            ErrorIllegalFileAccess => "ILLEGAL_FILE_ACCESS",
            ErrorInvalidMimeType => "INVALID_MIME_TYPE",
            ErrorDirectShareRegistrationFailed => "DIRECT_SHARE_REGISTRATION_FAILED",
            ErrorEmptyIdList => "EMPTY_ID_LIST",
            ErrorEmptyFileList => "EMPTY_FILE_LIST",
            ErrorInvalidIcon => "INVALID_ICON",
            ErrorInvalidChooserAction => "INVALID_CHOOSER_ACTION",
            _ => error.ToString(CultureInfo.InvariantCulture),
        };

        /// <summary>Whether an entry rejection means the C# side built a wrong request (logged as an error).</summary>
        internal static bool IsRequestDefect(int error) =>
            error == ErrorInvalidParameter || error == ErrorNotSupported || error == ErrorInvalidChooserAction;

        /// <summary>
        /// The error message of a failed call (4.4): <paramref name="entry"/> for a rejection when the
        /// library was called, false for a completion or a Direct Share result.
        /// <paramref name="exists"/> tells which path is missing; it reads the file system by default.
        /// </summary>
        internal static string FailureMessage(ShareCall call, int error, bool entry, Func<string, bool>? exists = null)
        {
            string op = call.Operation;
            switch (error)
            {
                case ErrorNotInitialized:
                    return $"Failed to {op}: the native library is not initialized yet.";
                case ErrorInvalidParameter:
                case ErrorNotSupported:
                    return $"Failed to {op}: the request was rejected ({ErrorName(error)}).";
                case ErrorInvalidChooserAction:
                    return entry
                        ? $"Failed to {op}: the request was rejected ({ErrorName(error)})."
                        : $"Failed to {AndroidShareManager.OperationShareText}: a chooser action icon could not be read as an image.";
                case ErrorEmptyContent:
                    return $"Failed to {op}: text is required";
                case ErrorEmptyFileList:
                    return $"Failed to {op}: filePaths must not be empty";
                case ErrorEmptyIdList:
                    return $"Failed to {AndroidShareManager.OperationRemoveDirectShareTargets}: ids must not be empty";
                case ErrorNoShareTarget:
                    return "No app available to handle this share request.";
                case ErrorFileNotFound:
                    return $"File not found: {(call.Paths.Length == 1 ? call.Paths[0] : MissingPath(call.Paths, exists ?? Exists))}";
                case ErrorIllegalFileAccess:
                    return $"File cannot be shared: {string.Join(", ", call.Paths)}. Ensure the file is in a supported directory.";
                case ErrorInvalidMimeType:
                    return $"Invalid MIME type: {call.MimeType}";
                case ErrorDirectShareRegistrationFailed:
                    return "Failed to register Direct Share target: the system did not accept the shortcut.";
                case ErrorInvalidIcon:
                    return $"Invalid icon data for Direct Share target: {call.DirectTargetId}";
                case ErrorNotForeground:
                    return $"Failed to {op}: the app is not in the foreground.";
                default:
                    return $"Failed to {op}: {ErrorName(error)}.";
            }
        }

        /// <summary>The message when libntk.so cannot be used (32-bit, Mono, another major version).</summary>
        internal static string NativeUnavailableMessage(string operation) =>
            $"Failed to {operation}: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).";

        /// <summary>
        /// The first path that is neither a file nor a directory; when every path exists, all of them
        /// joined by ", " (J-S9). Only for the message: the C ABI does not say which path it was.
        /// </summary>
        internal static string MissingPath(string[] paths, Func<string, bool> exists)
        {
            foreach (string path in paths)
            {
                if (!exists(path)) return path;
            }
            return string.Join(", ", paths);
        }

        private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

        // ── The selection requests to cancel (J-S13) ──────────────────────────

        /// <summary>Adds an accepted selection request, keeping the newest <see cref="MaxSelectionIds"/>.</summary>
        internal static void AddSelectionId(List<ulong> ids, ulong id)
        {
            ids.Add(id);
            while (ids.Count > MaxSelectionIds) ids.RemoveAt(0);
        }

        /// <summary>Takes every kept request, oldest first, and empties the list.</summary>
        internal static ulong[] TakeSelectionIds(List<ulong> ids)
        {
            ulong[] taken = ids.ToArray();
            ids.Clear();
            return taken;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_text(ref TextContent content, IntPtr actions, UIntPtr actionCount, IntPtr callback, IntPtr userData, IntPtr release);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_image(IntPtr path, IntPtr mimeType, IntPtr callback, IntPtr userData, IntPtr release);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_images(IntPtr paths, UIntPtr count, IntPtr callback, IntPtr userData, IntPtr release);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_file(IntPtr path, IntPtr callback, IntPtr userData, IntPtr release);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_files(IntPtr paths, UIntPtr count, IntPtr callback, IntPtr userData, IntPtr release);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_register_direct_target(ref DirectTarget target);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_remove_direct_targets(IntPtr ids, UIntPtr count);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_text_for_selection(ref TextContent content, IntPtr callback, IntPtr userData, IntPtr release, out ulong requestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_cancel_selection(ulong requestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_add_chooser_action_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_share_add_selection_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_share_listener_remove(IntPtr listener);
#endif
    }
}
#endif
