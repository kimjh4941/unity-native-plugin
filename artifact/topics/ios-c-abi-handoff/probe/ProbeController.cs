// The probe scene: buttons for each U-n step, a frame counter that keeps moving, and the log.
// NTK_PROBE_AUTO (environment variable, simulator only) runs a step list without taps:
//   round1: U-1 (1)-(3) and U-8 (3), cancelling the alerts instead of tapping them
//   u9:     U-9, cancelling the alert so the throwing callback runs
//   notif:  hand-off 2 wiring (permission, schedule)
using System;
using System.Collections;
using UnityEngine;
#if NTK_UNITY_NOTIFICATIONS
using Unity.Notifications.iOS;
#endif

public class ProbeController : MonoBehaviour
{
    const string U8ModeKey = "ntk.u8.mode";   // 0 none, 1 Start(), 2 BeforeSceneLoad; consumed at launch
    const int U8None = 0, U8Start = 1, U8BeforeSceneLoad = 2;

    static int u8ModeThisLaunch;
    float tickUntil;
    float nextTick;
    Vector2 scroll;
#if NTK_HANDOFF2
    IntPtr listener;
    int focusCount;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeSceneLoad()
    {
        var env = Environment.GetEnvironmentVariable("NTK_PROBE_U8");
        u8ModeThisLaunch = env != null ? int.Parse(env) : PlayerPrefs.GetInt(U8ModeKey, U8None);
        PlayerPrefs.SetInt(U8ModeKey, U8None);
        PlayerPrefs.Save();
        Probe.Log($"BeforeSceneLoad u8-mode={u8ModeThisLaunch} frame={Time.frameCount} t={Time.realtimeSinceStartup:F3}");
        if (u8ModeThisLaunch == U8BeforeSceneLoad) ShowAlert("U-8 BeforeSceneLoad", "Tap Yes", 82);
    }

    void Start()
    {
        Probe.Log($"Start frame={Time.frameCount} t={Time.realtimeSinceStartup:F3}");
#if NTK_HANDOFF2
        int r = NtkProbe.ntk_probe_notification_add_listener(NtkProbe.NotificationEvent, new IntPtr(30), NtkProbe.Release, out listener);
        Probe.Log($"add_listener -> {r} listener={(listener == IntPtr.Zero ? "null" : "set")} frame={Time.frameCount}");
#endif
#if NTK_UNITY_NOTIFICATIONS
        iOSNotificationCenter.OnNotificationReceived += n => Probe.Log($"pkg received id={n.Identifier} focused={Application.isFocused}");
#endif
        if (u8ModeThisLaunch == U8Start) ShowAlert("U-8 Start", "Tap Yes", 81);
        var auto = Environment.GetEnvironmentVariable("NTK_PROBE_AUTO");
        if (!string.IsNullOrEmpty(auto)) StartCoroutine(Auto(auto));
    }

    void Update()
    {
        if (Time.realtimeSinceStartup < tickUntil && Time.realtimeSinceStartup >= nextTick)
        {
            nextTick = Time.realtimeSinceStartup + 1f;
            Probe.Log($"tick frame={Time.frameCount}");
        }
    }

#if NTK_HANDOFF2
    void OnApplicationFocus(bool focus)
    {
        Probe.Log($"focus={focus} frame={Time.frameCount}");
        if (focus) StartCoroutine(ReadLaunchResponse(++focusCount));
    }

    IEnumerator ReadLaunchResponse(int count)
    {
        yield return new WaitForSecondsRealtime(1f);
        int r = NtkProbe.ntk_probe_notification_get_launch_response(out var id, out var action);
        Probe.Log($"get_launch_response(focus #{count}) -> {r} id={NtkProbe.TakeString(id) ?? "null"} action={NtkProbe.TakeString(action) ?? "null"}");
        NtkProbe.NtkProbeLogDelegate(NtkProbe.Utf8($"focus #{count}"));
#if NTK_UNITY_NOTIFICATIONS
        var last = iOSNotificationCenter.GetLastRespondedNotification();
        Probe.Log($"pkg last-responded(focus #{count}) id={last?.Identifier ?? "null"} action={iOSNotificationCenter.GetLastRespondedNotificationAction() ?? "null"}");
#endif
    }
#endif

#if NTK_UNITY_NOTIFICATIONS
    static int pkgCount;

    static void SchedulePackage()
    {
        var id = $"u4-pkg-{++pkgCount}";
        iOSNotificationCenter.ScheduleNotification(new iOSNotification
        {
            Identifier = id,
            Title = "U-4 package",
            Body = id,
            ShowInForeground = true,
            ForegroundPresentationOption = PresentationOption.Banner | PresentationOption.List | PresentationOption.Sound,
            Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = TimeSpan.FromSeconds(8), Repeats = false },
        });
        Probe.Log($"pkg schedule {id} in 8s");
    }

    // Not in the hand-off steps: the package assigns its delegate again on every authorization request.
    static IEnumerator PackageAuthorization()
    {
        using (var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false))
        {
            while (!request.IsFinished) yield return null;
            Probe.Log($"pkg authorization granted={request.Granted}");
        }
        NtkProbe.NtkProbeLogDelegate(NtkProbe.Utf8("after pkg authorization"));
    }
#endif

    void StartTicks(float seconds) { tickUntil = Time.realtimeSinceStartup + seconds; nextTick = 0f; }

    static ulong ShowAlert(string title, string message, long tag, NtkProbe.DialogDoneFn callback = null)
    {
        NtkProbe.InCall = true;
        int r = NtkProbe.ntk_probe_dialog_show_alert(NtkProbe.Utf8(title), NtkProbe.Utf8(message), NtkProbe.Utf8("Yes"), NtkProbe.Utf8("No"),
                                                    callback ?? NtkProbe.DialogDone, new IntPtr(tag), NtkProbe.Release, out var id);
        NtkProbe.InCall = false;
        Probe.Log($"show_alert '{title}' tag={tag} -> {r} id={id} frame={Time.frameCount}");
        return id;
    }

    static void Version() => Probe.Log($"ntk_version = 0x{NtkProbe.ntk_version():X6} ({NtkProbe.ntk_version()})");

    static void Read()
    {
        NtkProbe.InCall = true;
        int r = NtkProbe.ntk_probe_clipboard_read_text_async(NtkProbe.Text, new IntPtr(1), NtkProbe.Release, out var id);
        NtkProbe.InCall = false;
        Probe.Log($"read_text_async tag=1 -> {r} id={id} frame={Time.frameCount}");
    }

    static void Share()
    {
        NtkProbe.InCall = true;
        int r = NtkProbe.ntk_probe_share_text(NtkProbe.Utf8("U-5 test text"), NtkProbe.ShareDone, new IntPtr(5), NtkProbe.Release);
        NtkProbe.InCall = false;
        Probe.Log($"share_text tag=5 -> {r} frame={Time.frameCount}");
    }

    static void TwoInOneFrame()
    {
        ShowAlert("first", "U-8 (3)", 831);
        ShowAlert("second", "U-8 (3)", 832);
    }

    static void Cancel(ulong id)
    {
        int r = NtkProbe.ntk_probe_dialog_cancel(id);
        Probe.Log($"dialog_cancel id={id} -> {r} frame={Time.frameCount}");
    }

#if NTK_HANDOFF2
    static void Schedule(uint seconds, string id, string title)
    {
        int r = NtkProbe.ntk_probe_notification_schedule_marked(seconds, NtkProbe.Utf8(id), NtkProbe.Utf8(title),
                                                                 NtkProbe.NotificationDone, new IntPtr(40), NtkProbe.Release);
        Probe.Log($"schedule_marked {id} in {seconds}s -> {r}");
    }
#endif

    IEnumerator Auto(string mode)
    {
        Probe.Log($"auto {mode} begin");
        yield return new WaitForSecondsRealtime(2f);
        switch (mode)
        {
            case "round1":
                Version();
                Read();
                yield return new WaitForSecondsRealtime(1f);
                var id = ShowAlert("U-1", "Tap Yes", 2);
                yield return new WaitForSecondsRealtime(3f);
                Cancel(id);
                yield return new WaitForSecondsRealtime(1f);
                var first = ShowAlert("first", "U-8 (3)", 831);
                var second = ShowAlert("second", "U-8 (3)", 832);
                yield return new WaitForSecondsRealtime(3f);
                Cancel(second);
                Cancel(first);
                yield return new WaitForSecondsRealtime(1f);
                Share();
                StartTicks(6f);
                break;
            case "u9":
                var throwing = ShowAlert("U-9", "Tap Yes", 9, NtkProbe.DialogDoneThrowing);
                yield return new WaitForSecondsRealtime(2f);
                Cancel(throwing);
                yield return new WaitForSecondsRealtime(2f);
                Probe.Log("u9: still running after the throwing callback");
                var again = ShowAlert("U-9 again", "Tap Yes", 91);
                yield return new WaitForSecondsRealtime(2f);
                Cancel(again);
                break;
#if NTK_HANDOFF2
            case "notif":
                int r = NtkProbe.ntk_probe_notification_request_permission(NtkProbe.NotificationDone, new IntPtr(41), NtkProbe.Release);
                Probe.Log($"request_permission -> {r}");
                yield return new WaitForSecondsRealtime(3f);
                Schedule(2, "auto-a", "Auto A");
                break;
#endif
        }
        yield return new WaitForSecondsRealtime(8f);
        Probe.Log($"auto {mode} end");
    }

    void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.width / 400f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        // 400 units span the screen width, so one unit is close to one point on an iPhone; 52 units keep
        // each button above the 44 pt touch target.
        GUI.skin.button.fixedHeight = 52f;
        GUI.skin.button.fontSize = 17;
        GUI.skin.button.margin = new RectOffset(4, 4, 6, 6);

        GUILayout.BeginArea(new Rect(8, 48, width - 16, height - 96));
        // The moving bar shows whether rendering keeps going while an alert or a sheet is up.
        GUILayout.Label($"frame {Time.frameCount}   {DateTime.Now:HH:mm:ss.fff}");
        GUI.Box(new Rect(Mathf.Repeat(Time.realtimeSinceStartup * 120f, width - 40f), 22, 24, 6), GUIContent.none);
        GUILayout.Space(12);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("U-1 version")) Version();
        if (GUILayout.Button("U-1 read")) Read();
        if (GUILayout.Button("U-1 dialog")) ShowAlert("U-1", "Tap Yes", 2);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("U-5 dialog")) { StartTicks(20f); ShowAlert("U-5", "Wait 5 s, then tap No", 51); }
        if (GUILayout.Button("U-5 share")) { StartTicks(20f); Share(); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("U-8 two")) TwoInOneFrame();
        if (GUILayout.Button("U-8 next: Start")) { PlayerPrefs.SetInt(U8ModeKey, U8Start); PlayerPrefs.Save(); Probe.Log("next launch: U-8 Start"); }
        if (GUILayout.Button("U-8 next: BSL")) { PlayerPrefs.SetInt(U8ModeKey, U8BeforeSceneLoad); PlayerPrefs.Save(); Probe.Log("next launch: U-8 BeforeSceneLoad"); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("U-9 throwing")) ShowAlert("U-9", "Tap Yes", 9, NtkProbe.DialogDoneThrowing);
        if (GUILayout.Button("Clear log")) Probe.ClearFile();
        GUILayout.EndHorizontal();

#if NTK_HANDOFF2
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Permission"))
        {
            int r = NtkProbe.ntk_probe_notification_request_permission(NtkProbe.NotificationDone, new IntPtr(41), NtkProbe.Release);
            Probe.Log($"request_permission -> {r}");
        }
        if (GUILayout.Button("U-3 cold 10s")) Schedule(10, "u3-cold", "U-3 cold");
        if (GUILayout.Button("U-6 8s")) Schedule(8, "u6-a", "U-6 A");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        foreach (uint s in new uint[] { 1, 2, 3, 5 })
            if (GUILayout.Button($"U-7 {s}s")) Schedule(s, $"u7-{s}s", $"U-7 {s}s");
        GUILayout.EndHorizontal();
#endif

#if NTK_UNITY_NOTIFICATIONS
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("U-4 pkg 8s")) SchedulePackage();
        if (GUILayout.Button("U-4 lib 8s")) Schedule(8, $"u4-lib-{++pkgCount}", "U-4 library");
        if (GUILayout.Button("Pkg auth")) StartCoroutine(PackageAuthorization());
        GUILayout.EndHorizontal();
#endif

        scroll = GUILayout.BeginScrollView(scroll);
        lock (Probe.Lines)
            foreach (var line in Probe.Lines) GUILayout.Label(line);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
