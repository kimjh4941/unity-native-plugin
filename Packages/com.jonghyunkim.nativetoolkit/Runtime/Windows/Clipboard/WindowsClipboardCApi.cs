#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Windows.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JonghyunKim.NativeToolkit.Runtime.Windows.Common;

    /// <summary>
    /// The bridge between <see cref="WindowsClipboardManager"/> and native-toolkit's C ABI
    /// (<c>NativeToolkitC/Clipboard.h</c>, version 2.0.0).
    /// </summary>
    /// <remarks>
    /// Two halves, as agent-rules/coding-rules/common.md ("Unity Bridge パターン > Windows") lays
    /// out. The conversions are pure functions compiled everywhere, so EditMode tests reach them:
    /// the base64 decoder 1.x used, the history timestamp, how a read's code and value become a
    /// result, the history item rules, the deferred render, and the exceptions. The
    /// <c>DllImport</c>s are compiled only into a Windows player.
    /// Design: artifact/features/clipboard/designs/2026-09-27-windows-clipboard-design-v12.md.
    /// <para>
    /// No logs here: the values are clipboard content, which this feature never writes to the log
    /// (design v8). The manager logs what it did with them.
    /// </para>
    /// </remarks>
    internal static class WindowsClipboardCApi
    {
        /// <summary><c>ntk_clipboard_session_options</c>, 24 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct SessionOptions
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr OnClipboardChanged;
            public IntPtr UserData;
        }

        /// <summary><c>ntk_clipboard_history_handlers</c>, 40 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct HistoryHandlers
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr OnHistoryChanged;
            public IntPtr OnHistoryEnabledChanged;
            public IntPtr OnRoamingEnabledChanged;
            public IntPtr UserData;
        }

        /// <summary>The session options: the change listener, or none, and no <c>user_data</c> (design v12 J-7).</summary>
        internal static SessionOptions BuildSessionOptions(IntPtr onClipboardChanged) => new SessionOptions
        {
            StructSize = (uint)Marshal.SizeOf<SessionOptions>(),
            OnClipboardChanged = onClipboardChanged,
        };

        /// <summary>The three history listeners and no <c>user_data</c>.</summary>
        internal static HistoryHandlers BuildHistoryHandlers(IntPtr onHistoryChanged, IntPtr onHistoryEnabledChanged,
            IntPtr onRoamingEnabledChanged) => new HistoryHandlers
        {
            StructSize = (uint)Marshal.SizeOf<HistoryHandlers>(),
            OnHistoryChanged = onHistoryChanged,
            OnHistoryEnabledChanged = onHistoryEnabledChanged,
            OnRoamingEnabledChanged = onRoamingEnabledChanged,
        };

        // ── base64 (design v12 J-3) ──────────────────────────────────────────────

        /// <summary>
        /// Decodes base64 by the rules of 1.x's native <c>Base64Decode</c>
        /// (native-toolkit <c>1.11.0:windows/WindowsLibrary/WindowsClipboardFormats.cpp:443-487</c>),
        /// which <see cref="Convert.FromBase64String"/> does not follow:
        /// <list type="bullet">
        /// <item>only carriage return, line feed, space and tab are skipped; any other character
        /// outside the standard alphabet (<c>A-Z a-z 0-9 + /</c>) is refused, <c>\v</c>, <c>\f</c>,
        /// NBSP and non-ASCII included;</item>
        /// <item>what is left must be a non-empty multiple of four;</item>
        /// <item><c>=</c> may stand only in the last two places, and a third-place <c>=</c> needs a
        /// fourth-place one;</item>
        /// <item>the unused bits of the last group are not checked;</item>
        /// <item>a result of no bytes is refused.</item>
        /// </list>
        /// </summary>
        /// <returns>False for anything the rules refuse; the caller reports InvalidParameter.</returns>
        internal static bool TryDecodeBase64(string? text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (text == null) return false;

            var symbols = new List<char>(text.Length);
            foreach (char c in text)
            {
                if (c == '\r' || c == '\n' || c == ' ' || c == '\t') continue;
                symbols.Add(c);
            }
            if (symbols.Count == 0 || symbols.Count % 4 != 0) return false;

            var decoded = new List<byte>(symbols.Count / 4 * 3);
            for (int group = 0; group < symbols.Count; group += 4)
            {
                bool last = group + 4 == symbols.Count;
                int a = SymbolValue(symbols[group]);
                int b = SymbolValue(symbols[group + 1]);
                if (a < 0 || b < 0) return false;

                char third = symbols[group + 2];
                char fourth = symbols[group + 3];
                if (third == '=')
                {
                    if (!last || fourth != '=') return false;
                    decoded.Add((byte)((a << 2) | (b >> 4)));
                    continue;
                }

                int c3 = SymbolValue(third);
                if (c3 < 0) return false;
                if (fourth == '=')
                {
                    if (!last) return false;
                    decoded.Add((byte)((a << 2) | (b >> 4)));
                    decoded.Add((byte)(((b & 0xF) << 4) | (c3 >> 2)));
                    continue;
                }

                int d = SymbolValue(fourth);
                if (d < 0) return false;
                decoded.Add((byte)((a << 2) | (b >> 4)));
                decoded.Add((byte)(((b & 0xF) << 4) | (c3 >> 2)));
                decoded.Add((byte)(((c3 & 0x3) << 6) | d));
            }

            if (decoded.Count == 0) return false;
            bytes = decoded.ToArray();
            return true;
        }

        private static int SymbolValue(char c)
        {
            if (c >= 'A' && c <= 'Z') return c - 'A';
            if (c >= 'a' && c <= 'z') return c - 'a' + 26;
            if (c >= '0' && c <= '9') return c - '0' + 52;
            if (c == '+') return 62;
            if (c == '/') return 63;
            return -1;
        }

        // ── History timestamp (design v12 J-1) ───────────────────────────────────

        /// <summary>Milliseconds from 1601-01-01 (the FILETIME epoch) to 1970-01-01 (the Unix epoch).</summary>
        internal const long FileTimeEpochToUnixEpochMilliseconds = 11_644_473_600_000;

        /// <summary>
        /// The public <see cref="WindowsClipboardHistoryItem.Timestamp"/> (100 ns FILETIME ticks, as
        /// 1.x reported) from the C ABI's Unix milliseconds. 0 stays 0, which is also what the
        /// native side reports for a capture time it does not have; an overflow is 0 as well.
        /// </summary>
        internal static long ToFileTimeTicks(long unixMilliseconds)
        {
            if (unixMilliseconds == 0) return 0;
            try
            {
                return checked((unixMilliseconds + FileTimeEpochToUnixEpochMilliseconds) * 10_000);
            }
            catch (OverflowException)
            {
                return 0;
            }
        }

        // ── Read results ─────────────────────────────────────────────────────────

        /// <summary>What a read's code means for its result.</summary>
        internal enum ReadClass
        {
            /// <summary>The call succeeded and its value is the result.</summary>
            Value,

            /// <summary>FORMAT_UNAVAILABLE or EMPTY: a success with nothing in it, as 1.x reported.</summary>
            EmptySuccess,

            /// <summary>Any other code: a failure with that code, never an empty success.</summary>
            Failure,
        }

        internal static ReadClass ClassifyRead(WindowsClipboardErrorCode code) => code switch
        {
            WindowsClipboardErrorCode.None => ReadClass.Value,
            WindowsClipboardErrorCode.Empty => ReadClass.EmptySuccess,
            WindowsClipboardErrorCode.FormatUnavailable => ReadClass.EmptySuccess,
            _ => ReadClass.Failure,
        };

        /// <summary>
        /// The text up to its first NUL, as 1.x's <c>PtrToStringUni</c> read it. The native side
        /// hands back a length, so a NUL inside would otherwise survive.
        /// </summary>
        internal static string CutAtFirstNul(string text)
        {
            int nul = text.IndexOf('\0');
            return nul < 0 ? text : text.Substring(0, nul);
        }

        /// <summary>
        /// A text read's result. <paramref name="emptyTextIsEmpty"/> is for GetPreferredFormat,
        /// whose empty string means nothing matched; for the pastes an empty string is a value.
        /// </summary>
        internal static WindowsClipboardTextResult ToTextResult(string operation, WindowsClipboardErrorCode code,
            string? text, bool emptyTextIsEmpty)
        {
            switch (ClassifyRead(code))
            {
                case ReadClass.EmptySuccess:
                    return WindowsClipboardTextResult.Empty(operation);
                case ReadClass.Failure:
                    return WindowsClipboardTextResult.Failure(operation, code);
            }
            string value = CutAtFirstNul(text ?? string.Empty);
            return emptyTextIsEmpty && value.Length == 0
                ? WindowsClipboardTextResult.Empty(operation)
                : WindowsClipboardTextResult.Success(operation, value);
        }

        /// <summary>A list read's result. No entries is a success, as 1.x's <c>[]</c> was.</summary>
        internal static WindowsClipboardStringListResult ToListResult(string operation, WindowsClipboardErrorCode code,
            IReadOnlyList<string>? values)
        {
            switch (ClassifyRead(code))
            {
                case ReadClass.EmptySuccess:
                    return WindowsClipboardStringListResult.Empty(operation);
                case ReadClass.Failure:
                    return WindowsClipboardStringListResult.Failure(operation, code);
            }
            return WindowsClipboardStringListResult.Success(operation, values ?? Array.Empty<string>());
        }

        internal static WindowsClipboardBytesResult ToBytesResult(string operation, WindowsClipboardErrorCode code, byte[]? data)
        {
            switch (ClassifyRead(code))
            {
                case ReadClass.EmptySuccess:
                    return WindowsClipboardBytesResult.Empty(operation);
                case ReadClass.Failure:
                    return WindowsClipboardBytesResult.Failure(operation, code);
            }
            return WindowsClipboardBytesResult.Success(operation, data ?? Array.Empty<byte>());
        }

        // ── History items ────────────────────────────────────────────────────────

        /// <summary>
        /// One history entry as the public type holds it, or null for an entry without an id, which
        /// cannot be restored or deleted (1.x dropped it too). Text that is absent or empty is null.
        /// </summary>
        internal static WindowsClipboardHistoryItem? ToHistoryItem(string? id, string? text,
            IReadOnlyList<string>? contentTypes, long unixMilliseconds)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return new WindowsClipboardHistoryItem(id!, string.IsNullOrEmpty(text) ? null : text, contentTypes,
                ToFileTimeTicks(unixMilliseconds));
        }

        // ── Deferred rendering (design v12 J-8) ──────────────────────────────────

        /// <summary>
        /// Renders one reserved format in the one step the C ABI asks for: calls the provider once
        /// and hands its bytes to <paramref name="setTarget"/> (<c>ntk_clipboard_render_target_set</c>),
        /// whose code is returned as it is.
        /// </summary>
        /// <param name="failure">Why nothing was rendered, for the manager's log; null when it was.</param>
        internal static WindowsClipboardErrorCode Render(Func<byte[]>? provider,
            Func<byte[], WindowsClipboardErrorCode> setTarget, out string? failure)
        {
            failure = null;
            if (provider == null)
            {
                failure = "no provider";
                return WindowsClipboardErrorCode.InvalidParameter;
            }

            byte[]? produced;
            try
            {
                produced = provider();
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name;
                return WindowsClipboardErrorCode.Unknown;
            }
            if (produced == null || produced.Length == 0)
            {
                // A zero-length payload cannot be placed.
                failure = "produced no bytes";
                return WindowsClipboardErrorCode.InvalidData;
            }

            try
            {
                WindowsClipboardErrorCode code = setTarget(produced);
                if (code != WindowsClipboardErrorCode.None) failure = $"render_target_set: {code}";
                return code;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name;
                return WindowsClipboardErrorCode.Unknown;
            }
        }

        // ── Codes ────────────────────────────────────────────────────────────────

        /// <summary>Whether an exception says the native library could not be loaded or bound.</summary>
        internal static bool IsBridgeFailure(Exception exception) => WindowsNativeToolkitCApi.IsNativeUnavailable(exception);

        /// <summary>
        /// The code for an exception from Initialize, a history request, or a shutdown attempt:
        /// BridgeUnavailable when the library could not be used, else Unknown.
        /// </summary>
        internal static WindowsClipboardErrorCode FromLifecycleException(Exception exception) =>
            IsBridgeFailure(exception) ? WindowsClipboardErrorCode.BridgeUnavailable : WindowsClipboardErrorCode.Unknown;

        /// <summary>The code for the native library's state: None when it can be used.</summary>
        internal static WindowsClipboardErrorCode StatusFor(WindowsNativeToolkitCApi.NativeState state) => state switch
        {
            WindowsNativeToolkitCApi.NativeState.Available => WindowsClipboardErrorCode.None,
            WindowsNativeToolkitCApi.NativeState.PlatformUnavailable => WindowsClipboardErrorCode.PlatformUnavailable,
            _ => WindowsClipboardErrorCode.BridgeUnavailable,
        };

        /// <summary>
        /// Whether a shutdown attempt has nothing native to close: no session was ever created, or
        /// the last one was closed and freed. 1.x answered such an uninit with success (design v12 J-5).
        /// </summary>
        internal static bool ShouldSkipNativeClose(IntPtr session) => session == IntPtr.Zero;

        // ── Callbacks ────────────────────────────────────────────────────────────
        // The types are declared everywhere so the manager can name them in both compilations; only
        // the player hands them to the native side. Every receiver runs on the owner thread (the
        // Unity main thread), and none may let an exception back into the native library.

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ChangedCallback(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void FlagChangedCallback(IntPtr userData, int enabled);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int RenderCallback(IntPtr userData, IntPtr formatName, IntPtr target);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void HistoryCallback(IntPtr userData, uint requestId, int error, uint systemCode, IntPtr history);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void CompletionCallback(IntPtr userData, uint requestId, int error, uint systemCode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AvailabilityCallback(IntPtr userData, uint requestId, int error, uint systemCode,
            int historyEnabled, int roamingEnabled);

        /// <summary>One entry of <c>CopyMultipleFormats</c>, its base64 already decoded.</summary>
        internal readonly struct MultipleItem
        {
            internal MultipleItem(WindowsClipboardPayloadKind kind, string format, string? text, byte[]? bytes)
            {
                Kind = kind;
                Format = format;
                Text = text;
                Bytes = bytes;
            }

            internal WindowsClipboardPayloadKind Kind { get; }
            internal string Format { get; }
            internal string? Text { get; }
            internal byte[]? Bytes { get; }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // ── Native calls (Windows player only) ───────────────────────────────────
        // Each wrapper copies strings in as NUL-terminated UTF-8 and frees them, and copies every
        // output handle out before freeing it, the frees in finally blocks.

        internal static int CreateSession(IntPtr onClipboardChanged, out IntPtr session)
        {
            session = IntPtr.Zero;
            SessionOptions options = BuildSessionOptions(onClipboardChanged);
            return ntk_clipboard_session_create(ref options, out session);
        }

        internal static int Close(IntPtr session) => ntk_clipboard_session_close(session);

        internal static bool CanClose(IntPtr session) => ntk_clipboard_session_can_close(session) != 0;

        internal static void Free(IntPtr session) => ntk_clipboard_session_free(session);

        /// <summary>Installs the three history listeners, or removes them when <paramref name="enabled"/> is false.</summary>
        internal static int SetHistoryHandlers(IntPtr session, bool enabled, IntPtr onHistoryChanged,
            IntPtr onHistoryEnabledChanged, IntPtr onRoamingEnabledChanged)
        {
            if (!enabled) return ntk_clipboard_set_history_handlers_none(session, IntPtr.Zero);
            HistoryHandlers handlers = BuildHistoryHandlers(onHistoryChanged, onHistoryEnabledChanged, onRoamingEnabledChanged);
            return ntk_clipboard_set_history_handlers(session, ref handlers);
        }

        internal static int CopyText(IntPtr session, string text, uint flags) =>
            WithUtf8(new[] { text }, p => ntk_clipboard_copy_text(session, p[0], flags));

        internal static int CopyHtml(IntPtr session, string fragment, string? plainText, uint flags) =>
            WithUtf8(new[] { fragment, plainText }, p => ntk_clipboard_copy_html(session, p[0], p[1], flags));

        internal static int CopyFiles(IntPtr session, IReadOnlyList<string> paths, uint flags) =>
            WithUtf8Array(paths, (array, count) => ntk_clipboard_copy_files(session, array, count, flags));

        internal static int CopyDib(IntPtr session, byte[] dib, uint flags) =>
            ntk_clipboard_copy_dib(session, dib, new UIntPtr((uint)dib.Length), flags);

        internal static int CopyCustom(IntPtr session, string formatName, byte[] data, uint flags) =>
            WithUtf8(new[] { formatName }, p => ntk_clipboard_copy_custom(session, p[0], data, new UIntPtr((uint)data.Length), flags));

        /// <summary>Builds the items and places them in one clipboard write. The builder is freed whatever happens.</summary>
        internal static int CopyMultiple(IntPtr session, IReadOnlyList<MultipleItem> entries, uint flags)
        {
            var allocated = new List<IntPtr>();
            IntPtr items = IntPtr.Zero;
            try
            {
                int error = ntk_clipboard_items_create(out items);
                for (int i = 0; error == 0 && i < entries.Count; i++)
                {
                    MultipleItem entry = entries[i];
                    IntPtr format = WindowsNativeToolkitCApi.AllocUtf8(entry.Format, allocated);
                    error = entry.Kind switch
                    {
                        WindowsClipboardPayloadKind.Text => ntk_clipboard_items_add_text(items, format, WindowsNativeToolkitCApi.AllocUtf8(entry.Text ?? string.Empty, allocated)),
                        WindowsClipboardPayloadKind.Html => ntk_clipboard_items_add_html(items, format, WindowsNativeToolkitCApi.AllocUtf8(entry.Text ?? string.Empty, allocated)),
                        _ => ntk_clipboard_items_add_bytes(items, format, entry.Bytes ?? Array.Empty<byte>(), new UIntPtr((uint)(entry.Bytes?.Length ?? 0))),
                    };
                }
                return error == 0 ? ntk_clipboard_copy_multiple(session, items, flags) : error;
            }
            finally
            {
                try
                {
                    if (items != IntPtr.Zero) ntk_clipboard_items_free(items);
                }
                finally
                {
                    WindowsNativeToolkitCApi.FreeAll(allocated);
                }
            }
        }

        internal static int Clear(IntPtr session) => ntk_clipboard_clear(session);

        internal static int PasteText(IntPtr session, out string? text) =>
            ReadString(out text, (out IntPtr handle) => ntk_clipboard_paste_text(session, out handle));

        internal static int PasteHtml(IntPtr session, out string? html) =>
            ReadString(out html, (out IntPtr handle) => ntk_clipboard_paste_html(session, out handle));

        internal static int GetPreferredFormat(IntPtr session, out string? format) =>
            ReadString(out format, (out IntPtr handle) => ntk_clipboard_get_preferred_format(session, out handle));

        internal static int PasteFiles(IntPtr session, out List<string>? paths) =>
            ReadList(out paths, (out IntPtr handle) => ntk_clipboard_paste_files(session, out handle));

        internal static int GetFormats(IntPtr session, out List<string>? formats) =>
            ReadList(out formats, (out IntPtr handle) => ntk_clipboard_get_formats(session, out handle));

        internal static int PasteDib(IntPtr session, out byte[]? dib) =>
            ReadBytes(out dib, (out IntPtr handle) => ntk_clipboard_paste_dib(session, out handle));

        internal static int PasteCustom(IntPtr session, string formatName, out byte[]? data)
        {
            byte[]? read = null;
            int error = WithUtf8(new[] { formatName },
                p => ReadBytes(out read, (out IntPtr handle) => ntk_clipboard_paste_custom(session, p[0], out handle)));
            data = read;
            return error;
        }

        internal static int HasFormat(IntPtr session, string formatName, out bool present)
        {
            int flag = 0;
            int error = WithUtf8(new[] { formatName }, p => ntk_clipboard_has_format(session, p[0], out flag));
            present = error == 0 && flag != 0;
            return error;
        }

        internal static int ReserveDeferred(IntPtr session, IReadOnlyList<string> formats, IntPtr render) =>
            WithUtf8Array(formats, (array, count) =>
                ntk_clipboard_reserve_deferred(session, array, count, render, IntPtr.Zero, IntPtr.Zero));

        internal static int RecoverDeferredState(IntPtr session) => ntk_clipboard_recover_deferred_state(session);

        internal static WindowsClipboardErrorCode RenderTargetSet(IntPtr target, byte[] data) =>
            (WindowsClipboardErrorCode)ntk_clipboard_render_target_set(target, data, new UIntPtr((uint)data.Length));

        internal static int GetHistory(IntPtr session, IntPtr callback, out uint requestId) =>
            ntk_clipboard_get_history(session, callback, IntPtr.Zero, out requestId);

        internal static int RestoreHistoryItem(IntPtr session, string itemId, IntPtr callback, out uint requestId)
        {
            uint id = 0;
            int error = WithUtf8(new[] { itemId }, p => ntk_clipboard_restore_history_item(session, p[0], callback, IntPtr.Zero, out id));
            requestId = id;
            return error;
        }

        internal static int DeleteHistoryItem(IntPtr session, string itemId, IntPtr callback, out uint requestId)
        {
            uint id = 0;
            int error = WithUtf8(new[] { itemId }, p => ntk_clipboard_delete_history_item(session, p[0], callback, IntPtr.Zero, out id));
            requestId = id;
            return error;
        }

        internal static int ClearUnpinnedHistory(IntPtr session, IntPtr callback, out uint requestId) =>
            ntk_clipboard_clear_unpinned_history(session, callback, IntPtr.Zero, out requestId);

        internal static int GetHistoryAvailability(IntPtr session, IntPtr callback, out uint requestId) =>
            ntk_clipboard_get_history_availability(session, callback, IntPtr.Zero, out requestId);

        internal static int CancelRequest(IntPtr session, uint requestId) => ntk_clipboard_cancel_request(session, requestId);

        /// <summary>
        /// Copies the history handed to a completion into the public items, while the handle is
        /// alive. Entries without an id are left out and counted.
        /// </summary>
        internal static List<WindowsClipboardHistoryItem> ReadHistory(IntPtr history, out int dropped)
        {
            dropped = 0;
            ulong count = ntk_clipboard_history_count(history).ToUInt64();
            var items = new List<WindowsClipboardHistoryItem>((int)Math.Min(count, 1024));
            for (ulong i = 0; i < count; i++)
            {
                var index = new UIntPtr(i);
                string? id = ReadSized(ntk_clipboard_history_item_id(history, index, out UIntPtr idSize), idSize);
                string? text = ReadSized(ntk_clipboard_history_item_text(history, index, out UIntPtr textSize), textSize);
                ulong typeCount = ntk_clipboard_history_item_content_type_count(history, index).ToUInt64();
                var types = new List<string>((int)Math.Min(typeCount, 64));
                for (ulong t = 0; t < typeCount; t++)
                {
                    string? type = ReadSized(ntk_clipboard_history_item_content_type_at(history, index, new UIntPtr(t), out UIntPtr typeSize), typeSize);
                    if (type != null) types.Add(type);
                }
                long unixMs = ntk_clipboard_history_item_timestamp_unix_ms(history, index);

                WindowsClipboardHistoryItem? item = ToHistoryItem(id, text, types, unixMs);
                if (item == null) dropped++;
                else items.Add(item);
            }
            return items;
        }

        /// <summary>A NUL-terminated UTF-8 string the native side lends for the call (a format name).</summary>
        internal static string ReadUtf8Z(IntPtr data)
        {
            if (data == IntPtr.Zero) return string.Empty;
            int length = 0;
            while (Marshal.ReadByte(data, length) != 0) length++;
            return WindowsNativeToolkitCApi.ReadUtf8(data, length);
        }

        private static string? ReadSized(IntPtr data, UIntPtr size) =>
            data == IntPtr.Zero ? null : WindowsNativeToolkitCApi.ReadUtf8(data, WindowsNativeToolkitCApi.ToByteCount(size));

        private delegate int HandleCall(out IntPtr handle);

        private static int ReadString(out string? value, HandleCall call)
        {
            value = null;
            IntPtr handle = IntPtr.Zero;
            try
            {
                int error = call(out handle);
                if (error == 0) value = WindowsNativeToolkitCApi.ReadString(handle);
                return error;
            }
            finally
            {
                if (handle != IntPtr.Zero) WindowsNativeToolkitCApi.ntk_string_free(handle);
            }
        }

        private static int ReadList(out List<string>? values, HandleCall call)
        {
            values = null;
            IntPtr handle = IntPtr.Zero;
            try
            {
                int error = call(out handle);
                if (error == 0) values = WindowsNativeToolkitCApi.ReadStringList(handle);
                return error;
            }
            finally
            {
                if (handle != IntPtr.Zero) WindowsNativeToolkitCApi.ntk_string_list_free(handle);
            }
        }

        private static int ReadBytes(out byte[]? data, HandleCall call)
        {
            data = null;
            IntPtr handle = IntPtr.Zero;
            try
            {
                int error = call(out handle);
                if (error == 0)
                {
                    int size = WindowsNativeToolkitCApi.ToByteCount(WindowsNativeToolkitCApi.ntk_bytes_size(handle));
                    var copy = new byte[size];
                    if (size > 0) Marshal.Copy(WindowsNativeToolkitCApi.ntk_bytes_data(handle), copy, 0, size);
                    data = copy;
                }
                return error;
            }
            finally
            {
                if (handle != IntPtr.Zero) WindowsNativeToolkitCApi.ntk_bytes_free(handle);
            }
        }

        /// <summary>Copies the strings as UTF-8 (null stays NULL), runs the call, and frees them.</summary>
        private static int WithUtf8(string?[] values, Func<IntPtr[], int> call)
        {
            var allocated = new List<IntPtr>();
            try
            {
                var pointers = new IntPtr[values.Length];
                for (int i = 0; i < values.Length; i++) pointers[i] = WindowsNativeToolkitCApi.AllocUtf8(values[i], allocated);
                return call(pointers);
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
        }

        /// <summary>Lays the strings out as a <c>const char* const*</c> array of UTF-8, runs the call, and frees them.</summary>
        private static int WithUtf8Array(IReadOnlyList<string> values, Func<IntPtr, UIntPtr, int> call)
        {
            var allocated = new List<IntPtr>();
            try
            {
                IntPtr array = Marshal.AllocHGlobal(IntPtr.Size * Math.Max(values.Count, 1));
                allocated.Add(array);
                for (int i = 0; i < values.Count; i++)
                    Marshal.WriteIntPtr(array, i * IntPtr.Size, WindowsNativeToolkitCApi.AllocUtf8(values[i], allocated));
                return call(array, new UIntPtr((uint)values.Count));
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
        }

        // Session
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_session_create(ref SessionOptions options, out IntPtr session);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_session_close(IntPtr session);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_session_can_close(IntPtr session);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_clipboard_session_free(IntPtr session);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_set_history_handlers(IntPtr session, ref HistoryHandlers handlers);

        /// <summary>The same function, for removing the handlers with NULL.</summary>
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ntk_clipboard_set_history_handlers")]
        internal static extern int ntk_clipboard_set_history_handlers_none(IntPtr session, IntPtr handlers);

        // Read and write
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_text(IntPtr session, IntPtr text, uint flags);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_paste_text(IntPtr session, out IntPtr text);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_html(IntPtr session, IntPtr fragment, IntPtr plainText, uint flags);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_paste_html(IntPtr session, out IntPtr html);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_files(IntPtr session, IntPtr paths, UIntPtr count, uint flags);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_paste_files(IntPtr session, out IntPtr paths);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_dib(IntPtr session, byte[] dib, UIntPtr size, uint flags);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_paste_dib(IntPtr session, out IntPtr dib);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_custom(IntPtr session, IntPtr formatName, byte[] data, UIntPtr size, uint flags);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_paste_custom(IntPtr session, IntPtr formatName, out IntPtr data);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_copy_multiple(IntPtr session, IntPtr items, uint flags);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_has_format(IntPtr session, IntPtr formatName, out int present);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_get_formats(IntPtr session, out IntPtr formats);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_get_preferred_format(IntPtr session, out IntPtr format);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_clear(IntPtr session);

        // Multi-format builder
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_items_create(out IntPtr items);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_items_add_text(IntPtr items, IntPtr formatName, IntPtr text);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_items_add_html(IntPtr items, IntPtr formatName, IntPtr html);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_items_add_bytes(IntPtr items, IntPtr formatName, byte[] data, UIntPtr size);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_clipboard_items_free(IntPtr items);

        // Deferred rendering
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_reserve_deferred(IntPtr session, IntPtr formats, UIntPtr count,
            IntPtr provider, IntPtr userData, IntPtr release);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_recover_deferred_state(IntPtr session);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_render_target_set(IntPtr target, byte[] data, UIntPtr size);

        // History requests
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_get_history(IntPtr session, IntPtr callback, IntPtr userData, out uint requestId);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_restore_history_item(IntPtr session, IntPtr itemId, IntPtr callback, IntPtr userData, out uint requestId);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_delete_history_item(IntPtr session, IntPtr itemId, IntPtr callback, IntPtr userData, out uint requestId);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_clear_unpinned_history(IntPtr session, IntPtr callback, IntPtr userData, out uint requestId);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_get_history_availability(IntPtr session, IntPtr callback, IntPtr userData, out uint requestId);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_clipboard_cancel_request(IntPtr session, uint requestId);

        // History (valid during the completion only)
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_clipboard_history_count(IntPtr history);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_history_item_id(IntPtr history, UIntPtr index, out UIntPtr size);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_history_item_text(IntPtr history, UIntPtr index, out UIntPtr size);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_clipboard_history_item_content_type_count(IntPtr history, UIntPtr index);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_clipboard_history_item_content_type_at(IntPtr history, UIntPtr index, UIntPtr typeIndex, out UIntPtr size);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long ntk_clipboard_history_item_timestamp_unix_ms(IntPtr history, UIntPtr index);
#endif
    }
}
#endif
