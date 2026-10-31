// P/Invoke declarations for the stage 1a probe xcframeworks (hand-off 1 and 2), following section 3 of
// native-toolkit's hand-off documents. Throwaway code: not the production wrapper (that is stage 2c).
using System;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using UnityEngine;

public static class NtkProbe
{
    const string Lib = "__Internal";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void ReleaseFn(IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void TextFn(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr text);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void DialogDoneFn(IntPtr userData, ulong requestId, int error, uint systemCode, int button);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void ShareDoneFn(IntPtr userData, int error, uint systemCode, int completed, IntPtr activityType);

    [DllImport(Lib)] public static extern uint ntk_version();
    [DllImport(Lib)] public static extern uint ntk_last_system_code();
    [DllImport(Lib)] public static extern IntPtr ntk_string_data(IntPtr s);
    [DllImport(Lib)] public static extern UIntPtr ntk_string_size(IntPtr s);
    [DllImport(Lib)] public static extern void ntk_string_free(IntPtr s);
    [DllImport(Lib)] public static extern int ntk_probe_clipboard_read_text_async(TextFn callback, IntPtr userData, ReleaseFn release, out ulong requestId);
    [DllImport(Lib)] public static extern int ntk_probe_dialog_show_alert(byte[] title, byte[] message, byte[] positiveText, byte[] negativeText,
                                                                         DialogDoneFn callback, IntPtr userData, ReleaseFn release, out ulong requestId);
    [DllImport(Lib)] public static extern int ntk_probe_dialog_cancel(ulong requestId);
    [DllImport(Lib)] public static extern int ntk_probe_share_text(byte[] text, ShareDoneFn callback, IntPtr userData, ReleaseFn release);

    /// NUL-terminated UTF-8; null stays NULL.
    public static byte[] Utf8(string s) => s == null ? null : Encoding.UTF8.GetBytes(s + "\0");

    public static string TakeString(IntPtr handle)   // reads and frees an owned ntk_string*
    {
        if (handle == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(ntk_string_data(handle)); }
        finally { ntk_string_free(handle); }
    }

    // True only while a probe function is on the stack, so a callback can tell whether it came after the return.
    public static bool InCall;

    // The delegates are static fields so that IL2CPP keeps them; the bodies are static methods.
    public static readonly ReleaseFn Release = OnRelease;
    public static readonly TextFn Text = OnText;
    public static readonly DialogDoneFn DialogDone = OnDialogDone;
    public static readonly ShareDoneFn ShareDone = OnShareDone;
    public static readonly DialogDoneFn DialogDoneThrowing = OnDialogDoneThrowing;

    static string Where() => $"main={(Probe.IsMainThread ? "yes" : "no")} after-return={(InCall ? "no" : "yes")} frame={Time.frameCount}";

    [MonoPInvokeCallback(typeof(ReleaseFn))]
    static void OnRelease(IntPtr userData)
    {
        try { Probe.Log($"release tag={userData.ToInt64()} {Where()}"); }
        catch (Exception e) { Debug.LogException(e); }
    }

    [MonoPInvokeCallback(typeof(TextFn))]
    static void OnText(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr text)
    {
        try { Probe.Log($"text tag={userData.ToInt64()} id={requestId} error={error} system={systemCode} text={TakeString(text) ?? "null"} {Where()}"); }
        catch (Exception e) { Debug.LogException(e); }
    }

    [MonoPInvokeCallback(typeof(DialogDoneFn))]
    static void OnDialogDone(IntPtr userData, ulong requestId, int error, uint systemCode, int button)
    {
        try { Probe.Log($"dialog tag={userData.ToInt64()} id={requestId} error={error} system={systemCode} button={button} {Where()}"); }
        catch (Exception e) { Debug.LogException(e); }
    }

    [MonoPInvokeCallback(typeof(ShareDoneFn))]
    static void OnShareDone(IntPtr userData, int error, uint systemCode, int completed, IntPtr activityType)
    {
        // The activity type can name a personal app, so only whether one was given is logged.
        try { var activity = TakeString(activityType); Probe.Log($"share tag={userData.ToInt64()} error={error} system={systemCode} completed={completed} activity={(activity == null ? "null" : "set")} {Where()}"); }
        catch (Exception e) { Debug.LogException(e); }
    }

    // U-9: deliberately without try / catch.
    [MonoPInvokeCallback(typeof(DialogDoneFn))]
    static void OnDialogDoneThrowing(IntPtr userData, ulong requestId, int error, uint systemCode, int button)
    {
        Probe.Log($"dialog-throwing tag={userData.ToInt64()} id={requestId} error={error} button={button} {Where()} -> throwing");
        throw new InvalidOperationException("U-9");
    }

#if NTK_HANDOFF2
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NotificationEventFn(IntPtr userData, int kind, IntPtr identifier, IntPtr action, IntPtr userText, int isLaunchResponse);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NotificationDoneFn(IntPtr userData, int error, uint systemCode, int granted);

    [DllImport(Lib)] public static extern int ntk_probe_notification_add_listener(NotificationEventFn callback, IntPtr userData, ReleaseFn release, out IntPtr listener);
    [DllImport(Lib)] public static extern void ntk_probe_notification_listener_remove(IntPtr listener);
    [DllImport(Lib)] public static extern int ntk_probe_notification_get_launch_response(out IntPtr identifier, out IntPtr action);
    [DllImport(Lib)] public static extern int ntk_probe_notification_request_permission(NotificationDoneFn callback, IntPtr userData, ReleaseFn release);
    [DllImport(Lib)] public static extern int ntk_probe_notification_schedule_marked(uint seconds, byte[] identifier, byte[] title,
                                                                                     NotificationDoneFn callback, IntPtr userData, ReleaseFn release);

    // From the probe's NtkProbeLog.mm, not from the xcframework.
    [DllImport(Lib)] public static extern void NtkProbeLogDelegate(byte[] point);

    public static readonly NotificationEventFn NotificationEvent = OnNotificationEvent;
    public static readonly NotificationDoneFn NotificationDone = OnNotificationDone;

    [MonoPInvokeCallback(typeof(NotificationEventFn))]
    static void OnNotificationEvent(IntPtr userData, int kind, IntPtr identifier, IntPtr action, IntPtr userText, int isLaunchResponse)
    {
        try
        {
            // The reply text is what the user typed, so only its length is logged.
            Probe.Log($"event kind={kind} id={TakeString(identifier) ?? "null"} action={TakeString(action) ?? "null"} " +
                      $"text-length={(TakeString(userText)?.Length ?? -1)} launch={isLaunchResponse} focused={Application.isFocused} {Where()}");
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    [MonoPInvokeCallback(typeof(NotificationDoneFn))]
    static void OnNotificationDone(IntPtr userData, int error, uint systemCode, int granted)
    {
        try { Probe.Log($"done tag={userData.ToInt64()} error={error} system={systemCode} granted={granted} {Where()}"); }
        catch (Exception e) { Debug.LogException(e); }
    }
#endif
}
