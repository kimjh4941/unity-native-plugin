// Logs to the screen, to Debug.Log (tagged [NTK-PROBE]) and to a file under persistentDataPath, so that
// logs written while the app is in the background or right after a cold start can be read later.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

public static class Probe
{
    const int MaxScreenLines = 40;

    static int mainThreadId;
    static string filePath;
    static readonly List<string> lines = new List<string>();

    public static IReadOnlyList<string> Lines => lines;

    public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThreadId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
        filePath = Path.Combine(Application.persistentDataPath, "probe-log.txt");
        Log($"process start pid={System.Diagnostics.Process.GetCurrentProcess().Id}");
    }

    public static void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        Debug.Log("[NTK-PROBE] " + line);
        lock (lines)
        {
            lines.Add(line);
            if (lines.Count > MaxScreenLines) lines.RemoveAt(0);
        }
        try { File.AppendAllText(filePath, line + "\n"); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void ClearFile()
    {
        try { File.Delete(filePath); }
        catch (Exception e) { Debug.LogException(e); }
        lock (lines) lines.Clear();
    }
}
