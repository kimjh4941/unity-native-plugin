#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
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
    /// artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v4.md.
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
        private const int MaxLineBytes = 1000;

        private static string? s_run;
        private static string? s_directory;
        private static bool? s_ready;
        private static int s_next;

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
        /// fails it when the driver answers "fail" or does not answer.
        /// </summary>
        internal static IEnumerator Do(string command, params string[] args)
        {
            yield return EnsureReady();
            if (s_ready != true)
                Assert.Ignore("No UI driver answered. Run the device tests with scripts/verify_unity_android.sh, which starts it.");

            int number = ++s_next;
            string line = $"NTK-UI|{s_run}|{number}|{command}" + (args.Length > 0 ? "|" + string.Join("|", args) : "");
            if (Encoding.UTF8.GetByteCount(line) > MaxLineBytes)
                throw new ArgumentException($"A UI driver request is limited to {MaxLineBytes} bytes: {line}");
            Debug.Log(line);

            string path = Path.Combine(s_directory!, number.ToString());
            float deadline = Time.realtimeSinceStartup + AnswerTimeoutSeconds;
            while (!File.Exists(path))
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"The UI driver did not answer {command} within {AnswerTimeoutSeconds}s.");
                yield return null;
            }
            LastAnswer = File.ReadAllText(path).Trim();
            File.Delete(path);
            if (!LastAnswer.StartsWith("ok", StringComparison.Ordinal))
                Assert.Fail($"The UI driver could not {command} {string.Join(" ", args)}: {LastAnswer}");
        }

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
