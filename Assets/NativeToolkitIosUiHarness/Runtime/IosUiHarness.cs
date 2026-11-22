#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>
    /// The UI harness: an iOS app that runs one scenario's rows when XCUITest presses them
    /// (artifact/topics/ios-c-abi-2, UI automation design v4, part (B), 4.1-4.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built only by IosUiHarnessBuild (the assembly needs NTK_UI_HARNESS, which that build passes as an
    /// extra define), so the sample app and the test players never contain it. It starts itself after the
    /// first scene loads; the scene does not reference it.
    /// </para>
    /// <para>
    /// The scenario comes from the environment variable NTK_SCENARIO (launch arguments do not reach the
    /// iOS player; stage 2 spike, V-4). A launch without it - a cold start from a notification - reads the
    /// scenario the previous launch saved, and says so in its first result line (source=file).
    /// </para>
    /// <para>
    /// Rows and results are UIKit views over the Unity view (NtkUiHarnessOverlay.mm), so XCUITest can
    /// find them: a button row.&lt;id&gt;.run per row, and a label result.line.&lt;n&gt; per result,
    /// "[&lt;id&gt;@&lt;state&gt;] &lt;value&gt;". Values hold only what the scenario chose and counts or
    /// kinds of results - never other apps' data.
    /// </para>
    /// </remarks>
    public static class IosUiHarness
    {
        private delegate void RowCallback(IntPtr rowId);

        [DllImport("__Internal")] private static extern void ntkh_overlay_init(RowCallback callback);
        [DllImport("__Internal")] private static extern void ntkh_overlay_add_row(string rowId);
        [DllImport("__Internal")] private static extern void ntkh_overlay_append(string rowId, string state, string value);
        [DllImport("__Internal")] private static extern void ntkh_overlay_after(double seconds, string rowId);
        [DllImport("__Internal")] private static extern IntPtr ntkh_activation_state();
        [DllImport("__Internal")] private static extern int ntkh_presenting();
        [DllImport("__Internal")] private static extern void ntkh_overlay_on_background(string rowId);

        private static readonly Dictionary<string, Action> Rows = new();

        private static readonly Dictionary<string, Action> Scenarios = new()
        {
            ["dialog"] = DialogScenario.Register,
            ["share"] = ShareScenario.Register,
            ["notification"] = NotificationScenario.Register,
            ["clipboard"] = ClipboardScenario.Register,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            ntkh_overlay_init(OnRow);
            try
            {
                string scenario = ReadLaunchShape(out string source);
                if (!Scenarios.TryGetValue(scenario, out Action? register))
                {
                    Append("launch", "fail", $"unknown scenario source={source}");
                    return;
                }
                Append("launch", "ok", $"scenario={scenario} source={source}");
                register();
            }
            catch (Exception e)
            {
                Append("launch", "fail", e.GetType().Name);
            }
        }

        /// <summary>Adds a row the test can press.</summary>
        public static void Row(string id, Action action)
        {
            Rows[id] = action;
            ntkh_overlay_add_row(id);
        }

        /// <summary>
        /// Runs a registered row after the delay, timed natively: Unity pauses its player loop while the app
        /// is not active, so a C# delay would not run then (Dialog UI test plan v3, ID-17). Register the
        /// delayed row with <see cref="Hidden"/>.
        /// </summary>
        public static void After(double seconds, string rowId) => ntkh_overlay_after(seconds, rowId);

        /// <summary>Registers a row that has no button (run by <see cref="After"/>).</summary>
        public static void Hidden(string id, Action action) => Rows[id] = action;

        /// <summary>
        /// Runs a registered row once, when the app next enters the background, inside a background task
        /// (Share UI test plan v3, SH-18). Register the row with <see cref="Hidden"/>.
        /// </summary>
        public static void OnBackground(string rowId) => ntkh_overlay_on_background(rowId);

        /// <summary>Whether the Unity view controller is presenting another screen (an alert, a sheet).</summary>
        public static bool Presenting() => ntkh_presenting() != 0;

        /// <summary>The activation state of the scene that shows the harness (foregroundActive, foregroundInactive, ...).</summary>
        public static string ActivationState() => Marshal.PtrToStringUTF8(ntkh_activation_state()) ?? "unknown";

        /// <summary>
        /// A value for a result line: null as &lt;null&gt;, the empty string as "", anything else as is
        /// (Dialog UI test plan v3: the lines tell null from empty).
        /// </summary>
        public static string Value(string? value) => value == null ? "<null>" : value.Length == 0 ? "\"\"" : value;

        /// <summary>Adds a result line. Never throws: it may run inside a native callback.</summary>
        public static void Append(string id, string state, string value)
        {
            try
            {
                Debug.Log($"[NTK-UI-HARNESS] [{id}@{state}] {value}");
                ntkh_overlay_append(id, state, value);
            }
            catch (Exception)
            {
                // An exception must not reach native code (artifact/topics/ios-c-abi-2 README, B-7).
            }
        }

        [MonoPInvokeCallback(typeof(RowCallback))]
        private static void OnRow(IntPtr rowIdPointer)
        {
            string id = "?";
            try
            {
                id = Marshal.PtrToStringUTF8(rowIdPointer) ?? "?";
                Append(id, "pending", "run");
                Rows[id]();
            }
            catch (Exception e)
            {
                Append(id, "fail", e.GetType().Name);
            }
        }

        private static string ReadLaunchShape(out string source)
        {
            string path = Path.Combine(Application.persistentDataPath, "ntk-ui-harness", "launch.txt");
            string? fromEnvironment = Environment.GetEnvironmentVariable("NTK_SCENARIO");
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, fromEnvironment);
                source = "env";
                return fromEnvironment;
            }
            if (File.Exists(path))
            {
                source = "file";
                return File.ReadAllText(path).Trim();
            }
            source = "none";
            return "none";
        }
    }
}
