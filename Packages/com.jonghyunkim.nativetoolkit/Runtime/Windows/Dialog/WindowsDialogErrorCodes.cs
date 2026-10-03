#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Windows.Dialog
{
    /// <summary>
    /// The error codes <see cref="WindowsDialogManager"/> reserves for itself in its events'
    /// <c>errorCode</c>.
    /// <para>
    /// Every other value is the operating system's own code for the failure, passed on unchanged:
    /// a <c>CommDlgExtendedError</c> value, an <c>HRESULT</c>, or a <c>GetLastError</c> value.
    /// An <c>HRESULT</c> failure has its top bit set and so reads as a negative number. <b>Tell the
    /// two apart by comparing with these constants, not by the sign.</b> The operating system never
    /// reports -1 to -5 here: the native library turns the file dialogs' cancel (<c>0xFFFFFFFF</c>)
    /// into <see cref="Cancelled"/> before anything else, and <c>0xFFFFFFFB</c> to <c>0xFFFFFFFE</c>
    /// are not defined <c>HRESULT</c>s.
    /// </para>
    /// </summary>
    public static class WindowsDialogErrorCodes
    {
        /// <summary>The user cancelled a file or folder dialog. Reported with <c>isCancelled = true</c> and <c>isSuccess = true</c>.</summary>
        public const int Cancelled = -1;

        /// <summary>
        /// An argument was refused before any dialog was shown: an empty title or message, a
        /// <see cref="Win32MessageBox"/> flag the native library has no equivalent for, or a filter
        /// string whose name has no pattern. Also reported if the native library rejects the request.
        /// </summary>
        public const int InvalidArgument = -2;

        /// <summary>The native library or this layer failed in a way it could not describe further.</summary>
        public const int Unknown = -3;

        /// <summary>
        /// The native library could not be used: the DLL is missing, lacks a function, is not a
        /// loadable image, or reports a major version these bindings do not speak.
        /// </summary>
        public const int NativeUnavailable = -4;

        /// <summary>Not running in a Windows player (for example, in the Unity Editor). No dialog is shown.</summary>
        public const int PlatformUnavailable = -5;
    }
}
#endif
