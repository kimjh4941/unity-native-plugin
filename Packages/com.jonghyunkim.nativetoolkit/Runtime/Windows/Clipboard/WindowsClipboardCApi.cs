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

    }
}
#endif
