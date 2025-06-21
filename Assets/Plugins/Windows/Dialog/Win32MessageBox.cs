// Win32 MessageBox定数（主要なものを網羅）
public static class Win32MessageBox
{
    // ボタン種別
    public const uint MB_OK = 0x00000000;
    public const uint MB_OKCANCEL = 0x00000001;
    public const uint MB_ABORTRETRYIGNORE = 0x00000002;
    public const uint MB_YESNOCANCEL = 0x00000003;
    public const uint MB_YESNO = 0x00000004;
    public const uint MB_RETRYCANCEL = 0x00000005;
    public const uint MB_CANCELTRYCONTINUE = 0x00000006;

    // アイコン種別
    public const uint MB_ICONHAND = 0x00000010;
    public const uint MB_ICONQUESTION = 0x00000020;
    public const uint MB_ICONEXCLAMATION = 0x00000030;
    public const uint MB_ICONWARNING = 0x00000030;
    public const uint MB_ICONASTERISK = 0x00000040;
    public const uint MB_ICONINFORMATION = 0x00000040;

    // デフォルトボタン
    public const uint MB_DEFBUTTON1 = 0x00000000;
    public const uint MB_DEFBUTTON2 = 0x00000100;
    public const uint MB_DEFBUTTON3 = 0x00000200;
    public const uint MB_DEFBUTTON4 = 0x00000300;

    // その他オプション
    public const uint MB_APPLMODAL = 0x00000000;
    public const uint MB_SYSTEMMODAL = 0x00001000;
    public const uint MB_TASKMODAL = 0x00002000;
    public const uint MB_TOPMOST = 0x00040000;
    public const uint MB_RIGHT = 0x00080000;
    public const uint MB_RTLREADING = 0x00100000;
}
