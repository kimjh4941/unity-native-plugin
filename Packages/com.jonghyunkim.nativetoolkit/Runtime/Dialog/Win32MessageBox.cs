#nullable enable

namespace JonghyunKim.NativeToolkit.Runtime.Dialog
{
    /// <summary>
    /// Static class containing Win32 MessageBox constants for comprehensive native dialog configuration.
    /// Provides constants for button types, icon styles, default button selection, and modal behavior.
    /// Used by WindowsDialogManager for native Win32 API MessageBox calls.
    /// </summary>
    public static class Win32MessageBox
    {
        // Button type constants
        public const uint MB_OK = 0x00000000;
        public const uint MB_OKCANCEL = 0x00000001;
        public const uint MB_ABORTRETRYIGNORE = 0x00000002;
        public const uint MB_YESNOCANCEL = 0x00000003;
        public const uint MB_YESNO = 0x00000004;
        public const uint MB_RETRYCANCEL = 0x00000005;
        public const uint MB_CANCELTRYCONTINUE = 0x00000006;

        // Icon type constants
        public const uint MB_ICONHAND = 0x00000010;
        public const uint MB_ICONQUESTION = 0x00000020;
        public const uint MB_ICONEXCLAMATION = 0x00000030;
        public const uint MB_ICONWARNING = 0x00000030;
        public const uint MB_ICONASTERISK = 0x00000040;
        public const uint MB_ICONINFORMATION = 0x00000040;

        // Default button constants
        public const uint MB_DEFBUTTON1 = 0x00000000;
        public const uint MB_DEFBUTTON2 = 0x00000100;
        public const uint MB_DEFBUTTON3 = 0x00000200;
        public const uint MB_DEFBUTTON4 = 0x00000300;

        // Modal behavior and display options
        public const uint MB_APPLMODAL = 0x00000000;
        public const uint MB_SYSTEMMODAL = 0x00001000;
        public const uint MB_TASKMODAL = 0x00002000;
        public const uint MB_TOPMOST = 0x00040000;
        public const uint MB_RIGHT = 0x00080000;
        public const uint MB_RTLREADING = 0x00100000;
    }
}
