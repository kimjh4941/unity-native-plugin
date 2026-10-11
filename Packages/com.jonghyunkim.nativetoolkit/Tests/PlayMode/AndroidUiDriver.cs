#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The test side of the host-side UI driver (scripts/android_ui_driver.py): asks it to press or
    /// read the OS UI, and waits for its answer.
    /// </summary>
    /// <remarks>
    /// A request is one logcat line (Debug.Log, tag Unity). The driver writes the answer into this
    /// app's files directory through run-as, one file per request, by renaming a finished temporary
    /// file so a half-written answer is never read. The protocol is in
    /// artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v11.md.
    /// <para>
    /// When no driver answers the HELLO (the tests were started from the Editor's Test Runner, say),
    /// every request ignores the test instead of failing it. Once the driver has answered, a missing
    /// answer fails the test. scripts/verify_unity_android.sh always starts the driver and fails a run
    /// in which a test of the <see cref="Category"/> was skipped.
    /// </para>
    /// </remarks>
    internal static class AndroidUiDriver
    {
        /// <summary>The NUnit category of every test that needs the driver.</summary>
        internal const string Category = "AndroidUiDriver";

        private const float ReadyTimeoutSeconds = 5f;
        private const float AnswerTimeoutSeconds = 20f;

        /// <summary>
        /// How long a share sheet command may take to answer. On a device whose adb calls all time
        /// out, the driver gives up within 151 s of taking the request (its SHEET_ANSWER_LIMIT,
        /// pinned by its doctest): its own limit (60 s), watching for a sheet not yet seen (10 s),
        /// closing the sheet (25 s), bringing this app back to the front (25 s), deleting what it
        /// read (20 s), one second of waits, and writing the answer (10 s) (design 3.3). Ordinarily
        /// a few seconds. A request still waiting behind an earlier one is not counted in that.
        /// </summary>
        internal const float SheetAnswerTimeoutSeconds = 180f;

        // The driver's share sheet commands (design 3.1), which take longer than the others. The
        // driver keeps the same list (SHEET_COMMANDS in scripts/android_ui_driver.py): change both.
        private static readonly HashSet<string> s_sheetCommands = new()
        {
            "tap-in-sheet", "absent-in-sheet", "present-in-sheet", "close-sheet", "no-sheet",
        };
        private const int MaxLineBytes = 1000;

        private static string? s_run;
        private static string? s_directory;
        private static bool? s_ready;
        private static int s_next;

        // Requests sent and not yet waited for: the driver answers in order, so each must be collected.
        private static readonly Dictionary<int, string> s_pending = new();

        /// <summary>The answer of the last request: "ok", "ok &lt;value&gt;" or "fail &lt;reason&gt;".</summary>
        internal static string LastAnswer { get; private set; } = "";

        /// <summary>The value of the last "ok &lt;value&gt;" answer, or "".</summary>
        internal static string LastValue => LastAnswer.StartsWith("ok ", StringComparison.Ordinal) ? LastAnswer.Substring(3) : "";

        // Selectors (design 3.1): attribute=Base64 value, joined by '&'; index is plain digits.

        internal static string Res(string resourceId, int index = 0) => Selector("resource-id", resourceId, index);
        internal static string Text(string text, int index = 0) => Selector("text", text, index);
        internal static string Class(string className, int index = 0) => Selector("class", className, index);

        internal static string Selector(string attribute, string value, int index = 0)
        {
            string selector = $"{attribute}={Base64(value)}";
            return index == 0 ? selector : $"{selector}&index={index}";
        }

        internal static string Base64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        /// <summary>
        /// Sends a request and waits for its answer. Ignores the test when no driver is running;
        /// fails it when the driver answers "fail" or does not answer. A share sheet command gets
        /// <see cref="SheetAnswerTimeoutSeconds"/>, the others 20 s.
        /// </summary>
        internal static IEnumerator Do(string command, params string[] args)
        {
            yield return Send(command, args);
            yield return Wait(LastSent, s_sheetCommands.Contains(command) ? SheetAnswerTimeoutSeconds : AnswerTimeoutSeconds);
        }

        /// <summary>The number of the last request <see cref="Send"/> sent.</summary>
        internal static int LastSent { get; private set; }

        /// <summary>
        /// Sends a request without waiting for its answer (design 5): for a share sheet, which pauses
        /// Unity while it shows, the test sends first, opens the sheet in the same frame, and then
        /// waits. The wait runs again once the sheet closes and Unity resumes; its limit counts from
        /// when it began. Every request sent must be waited for, on failure paths too
        /// (<see cref="WaitForPending"/>). Ignores the test when no driver is running.
        /// </summary>
        internal static IEnumerator Send(string command, params string[] args)
        {
            yield return EnsureReady();
            if (s_ready != true)
                Assert.Ignore("No UI driver answered. Run the device tests with scripts/verify_unity_android.sh, which starts it.");

            int number = ++s_next;
            string line = $"NTK-UI|{s_run}|{number}|{command}" + (args.Length > 0 ? "|" + string.Join("|", args) : "");
            if (Encoding.UTF8.GetByteCount(line) > MaxLineBytes)
                throw new ArgumentException($"A UI driver request is limited to {MaxLineBytes} bytes: {line}");
            s_pending[number] = $"{command} {string.Join(" ", args)}";
            Debug.Log(line);
            LastSent = number;
        }

        /// <summary>
        /// Waits for the answer of a request sent earlier. Fails the test when the driver answers
        /// "fail" or does not answer in time.
        /// </summary>
        internal static IEnumerator Wait(int number, float timeoutSeconds)
        {
            string request = s_pending.TryGetValue(number, out string? sent) ? sent : $"request {number}";
            string path = Path.Combine(s_directory!, number.ToString());
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!File.Exists(path))
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    s_pending.Remove(number);
                    Assert.Fail($"The UI driver did not answer {request} within {timeoutSeconds}s.");
                }
                yield return null;
            }
            s_pending.Remove(number);
            LastAnswer = File.ReadAllText(path).Trim();
            File.Delete(path);
            if (LastAnswer != "ok" && !LastAnswer.StartsWith("ok ", StringComparison.Ordinal))
                Assert.Fail($"The UI driver could not {request}: {LastAnswer}");
        }

        /// <summary>
        /// Waits for every request sent and not yet waited for, so that a failed test does not leave
        /// answers that a later request would be held up behind. A failed or missing answer does not
        /// stop it: it collects the rest first, then fails with every failure. For teardowns: run it
        /// through a wrapper that keeps what it throws.
        /// </summary>
        internal static IEnumerator WaitForPending()
        {
            var numbers = new List<int>(s_pending.Keys);
            numbers.Sort();
            var failures = new List<string>();
            foreach (int number in numbers)
            {
                IEnumerator wait = Wait(number, SheetAnswerTimeoutSeconds);
                while (true)
                {
                    try
                    {
                        if (!wait.MoveNext()) break;
                    }
                    catch (Exception e) when (e is not IgnoreException)
                    {
                        // A failed answer, or one that could not be read: collect the rest first.
                        failures.Add(e.Message);
                        break;
                    }
                    yield return wait.Current;
                }
            }
            if (failures.Count > 0)
                Assert.Fail(string.Join("\n", failures));
        }

        /// <summary>The number of requests sent and not yet waited for: 0 after every test.</summary>
        internal static int PendingCount => s_pending.Count;

        /// <summary>
        /// Waits on the calling thread, without Unity's API, for the answer file of a request sent
        /// earlier. For a test whose main thread is paused while the answer arrives (background).
        /// </summary>
        internal static bool WaitForAnswerFile(int number, TimeSpan timeout, System.Threading.CancellationToken cancel = default)
        {
            string path = Path.Combine(s_directory!, number.ToString());
            DateTime deadline = DateTime.UtcNow + timeout;
            while (!File.Exists(path))
            {
                if (DateTime.UtcNow > deadline || cancel.IsCancellationRequested) return false;
                System.Threading.Thread.Sleep(50);
            }
            return true;
        }

        /// <summary>The number the next request will get. A background request reads it before sending.</summary>
        internal static int NextNumber => s_next + 1;

        private static IEnumerator EnsureReady()
        {
            if (s_ready.HasValue) yield break;

            s_run = Guid.NewGuid().ToString("N");
            string files;
            int user;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var filesDir = activity.Call<AndroidJavaObject>("getFilesDir"))
            using (var process = new AndroidJavaClass("android.os.Process"))
            {
                files = filesDir.Call<string>("getAbsolutePath");
                user = process.CallStatic<int>("myUid") / 100000;
                Debug.Log($"NTK-UI-HELLO|{s_run}|{activity.Call<string>("getPackageName")}|{user}");
            }
            s_directory = Path.Combine(files, "ntk-ui", s_run);

            // The driver writes a probe first; reading and deleting it shows the driver that files it
            // writes reach this process. It then writes "ready".
            string probe = Path.Combine(s_directory, "probe");
            string ready = Path.Combine(s_directory, "ready");
            float deadline = Time.realtimeSinceStartup + ReadyTimeoutSeconds;
            while (!File.Exists(ready))
            {
                if (File.Exists(probe))
                {
                    File.ReadAllText(probe);
                    File.Delete(probe);
                    deadline = Time.realtimeSinceStartup + ReadyTimeoutSeconds;
                }
                if (Time.realtimeSinceStartup > deadline)
                {
                    s_ready = false;
                    yield break;
                }
                yield return null;
            }
            File.Delete(ready);
            s_ready = true;
        }
    }
}
#endif
#endif
