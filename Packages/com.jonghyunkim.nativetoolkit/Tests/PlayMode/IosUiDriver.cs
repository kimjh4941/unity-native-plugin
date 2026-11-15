#nullable enable

// iOS player only, like the tests that use it.
#if UNITY_IOS && !UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(JonghyunKim.NativeToolkit.Tests.IosUiDriverRunCallback))]

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The test side of the iOS UI driver (artifact/topics/ios-c-abi-2, UI driver design v2): an XCUITest test
    /// method that scripts/verify_unity_ios.sh starts next to the player and that presses the system screens
    /// (alerts, the share sheet, the notification permission, Settings) the tests ask for. The iOS counterpart
    /// of <c>AndroidUiDriver</c>, with the same names: <see cref="Do"/>, <see cref="Send"/>, <see cref="Wait"/>,
    /// <see cref="WaitForPending"/>.
    /// </summary>
    /// <remarks>
    /// The driver listens on 127.0.0.1; the script passes its port as NTK_DRIVER_PORT (launch arguments do not
    /// reach the iOS player). Lines are built and read by <see cref="IosUiDriverProtocol"/>. Without the port, or
    /// when nothing answers within 5 s, every request ignores the test (a run from the Editor's Test Runner);
    /// the script fails a run in which a test of <see cref="Category"/> was skipped. The socket is read once a
    /// frame; while Unity is paused (a permission prompt) answers wait in the socket. When the connection drops
    /// (the OS may close it while the player is suspended), the next wait connects again with the same run and
    /// sends the unanswered requests again; the driver answers a number it already handled from memory.
    /// </remarks>
    internal static class IosUiDriver
    {
        /// <summary>The NUnit category of every test that needs the driver.</summary>
        internal const string Category = "IosUiDriver";

        /// <summary>Tests that go through the sample scene (ignored where the player has none).</summary>
        internal const string SampleScreenCategory = "IosSampleScreen";

        /// <summary>Tests the device runs leave out (design v2, chapter 7).</summary>
        internal const string SimulatorOnlyCategory = "IosSimulatorOnly";

        /// <summary>The Notification sample tests, run in their own reinstalled player run (design v2, 4.5).</summary>
        internal const string NotificationSampleCategory = "IosNotificationSample";

        private const float ConnectTimeoutSeconds = 5f;
        private const string LogTag = "[NTK-IOS-UI-DRIVER]";

        private static string? s_run;
        private static bool? s_ready;
        private static bool s_lost;
        private static int s_next;
        private static TcpClient? s_client;
        private static NetworkStream? s_stream;
        private static readonly StringBuilder s_received = new();

        // Sent and not yet waited for: number -> (line, what the test asked for, for messages).
        private static readonly SortedDictionary<int, (string Line, string Request)> s_pending = new();
        private static readonly Dictionary<int, string> s_answers = new();

        /// <summary>The answer of the last request waited for ("ok", "ok &lt;value&gt;").</summary>
        internal static string LastAnswer { get; private set; } = "";

        /// <summary>The value of "ok &lt;value&gt;", or "".</summary>
        internal static string LastValue => IosUiDriverProtocol.ValueOf(LastAnswer);

        /// <summary>The number of the last request <see cref="Send"/> sent.</summary>
        internal static int LastSent { get; private set; }

        /// <summary>The number of requests sent and not yet waited for: 0 after every test.</summary>
        internal static int PendingCount => s_pending.Count;

        /// <summary>A string argument (Base64 of UTF-8).</summary>
        internal static string Text(string value) => IosUiDriverProtocol.Base64(value);

        /// <summary>Sends a request and waits for its answer (design v2, 3.3 for the waits).</summary>
        internal static IEnumerator Do(string command, params string[] args)
        {
            yield return Send(command, args);
            yield return Wait(LastSent, IosUiDriverProtocol.AnswerTimeoutFor(command, args));
        }

        /// <summary>
        /// Sends a request without waiting for its answer: send first, then open the system screen, then
        /// <see cref="Wait"/> (design v2, D-6). Every request sent must be waited for, on failure paths too
        /// (<see cref="WaitForPending"/>). Ignores the test when no driver is running.
        /// </summary>
        internal static IEnumerator Send(string command, params string[] args)
        {
            yield return EnsureReady();
            if (s_ready != true)
                Assert.Ignore("No UI driver answered. Run the player tests with scripts/verify_unity_ios.sh, which starts it.");
            if (s_lost)
                Assert.Fail("The UI driver connection was lost and could not be made again.");

            int number = ++s_next;
            string line = IosUiDriverProtocol.Request(number, command, args);
            s_pending[number] = (line, $"{command} {string.Join(" ", args)}".TrimEnd());
            LastSent = number;
            if (!TryWrite(line))
                yield return Reconnect();
        }

        /// <summary>Waits for the answer of a request sent earlier; fails on "fail" or no answer in time.</summary>
        internal static IEnumerator Wait(int number, float timeoutSeconds)
        {
            string request = s_pending.TryGetValue(number, out var sent) ? sent.Request : $"request {number}";
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!s_answers.ContainsKey(number))
            {
                if (!ReadAvailable())
                {
                    yield return Reconnect();
                    if (s_lost)
                    {
                        s_pending.Remove(number);
                        Assert.Fail($"The UI driver connection was lost while waiting for {request}.");
                    }
                    continue;
                }
                if (s_answers.ContainsKey(number)) break;
                if (Time.realtimeSinceStartup > deadline)
                {
                    s_pending.Remove(number);
                    Assert.Fail($"The UI driver did not answer {request} within {timeoutSeconds}s.");
                }
                yield return null;
            }
            s_pending.Remove(number);
            LastAnswer = s_answers[number];
            s_answers.Remove(number);
            if (!IosUiDriverProtocol.IsSuccess(LastAnswer))
                Assert.Fail($"The UI driver could not {request}: {LastAnswer}");
        }

        /// <summary>
        /// Waits for every request sent and not yet waited for. A failed or missing answer does not stop it:
        /// it collects the rest first, then fails with every failure. For teardowns.
        /// </summary>
        internal static IEnumerator WaitForPending()
        {
            var numbers = new List<int>(s_pending.Keys);
            var failures = new List<string>();
            foreach (int number in numbers)
            {
                IEnumerator wait = Wait(number, IosUiDriverProtocol.ScreenAnswerTimeoutSeconds);
                while (true)
                {
                    try
                    {
                        if (!wait.MoveNext()) break;
                    }
                    catch (Exception e) when (e is not IgnoreException)
                    {
                        failures.Add(e.Message);
                        break;
                    }
                    yield return wait.Current;
                }
            }
            if (failures.Count > 0)
                Assert.Fail(string.Join("\n", failures));
        }

        /// <summary>
        /// Says BYE at the end of the run and waits for "BYE ok" (design v2, 4.2). Blocks the main thread for
        /// at most a few seconds. Connects first when no test used the driver; does nothing without a driver.
        /// </summary>
        internal static void SayBye()
        {
            string? port = Environment.GetEnvironmentVariable("NTK_DRIVER_PORT");
            if (string.IsNullOrEmpty(port)) return;
            try
            {
                if (s_stream == null || s_lost)
                {
                    s_run ??= IosUiDriverProtocol.NewRun();
                    if (!ConnectBlocking(port)) return;
                }
                s_stream!.ReadTimeout = 5000;
                WriteLine(IosUiDriverProtocol.Bye(s_run!));
                string? answer = ReadLineBlocking(TimeSpan.FromSeconds(5));
                Debug.Log($"{LogTag} bye answer={(answer == IosUiDriverProtocol.ByeOk ? "ok" : "none")}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} bye failed: {e.GetType().Name}");
            }
            finally
            {
                Close();
            }
        }

        private static IEnumerator EnsureReady()
        {
            if (s_ready.HasValue) yield break;
            string? port = Environment.GetEnvironmentVariable("NTK_DRIVER_PORT");
            if (string.IsNullOrEmpty(port) || !int.TryParse(port, out int portNumber))
            {
                s_ready = false;
                yield break;
            }
            s_run = IosUiDriverProtocol.NewRun();
            yield return Connect(portNumber);
            s_ready = s_stream != null;
            Debug.Log($"{LogTag} ready={s_ready}");
        }

        /// <summary>Connects and says HELLO; leaves <see cref="s_stream"/> null when that fails within 5 s.</summary>
        private static IEnumerator Connect(int port)
        {
            Close();
            var client = new TcpClient();
            Task connecting = client.ConnectAsync("127.0.0.1", port);
            float deadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;
            while (!connecting.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            if (!connecting.IsCompleted || connecting.IsFaulted)
            {
                client.Close();
                yield break;
            }
            s_client = client;
            s_stream = client.GetStream();
            if (!TryWrite(IosUiDriverProtocol.Hello(s_run!, Application.identifier)))
            {
                Close();
                yield break;
            }
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!ReadAvailable()) break;
                if (TakeLine(out string? line))
                {
                    if (IosUiDriverProtocol.IsReady(line!, s_run!)) yield break;
                    break;
                }
                yield return null;
            }
            Close();
        }

        /// <summary>Connects again with the same run and sends every unanswered request again.</summary>
        private static IEnumerator Reconnect()
        {
            if (s_lost) yield break;
            string? port = Environment.GetEnvironmentVariable("NTK_DRIVER_PORT");
            if (port == null || !int.TryParse(port, out int portNumber))
            {
                s_lost = true;
                yield break;
            }
            Debug.Log($"{LogTag} connection lost; connecting again");
            yield return Connect(portNumber);
            if (s_stream == null)
            {
                s_lost = true;
                yield break;
            }
            foreach (var pending in s_pending)
            {
                if (s_answers.ContainsKey(pending.Key)) continue;
                if (!TryWrite(pending.Value.Line))
                {
                    s_lost = true;
                    yield break;
                }
            }
        }

        /// <summary>Reads what has arrived and files the answers. False when the connection is gone.</summary>
        private static bool ReadAvailable()
        {
            if (s_client == null || s_stream == null) return false;
            try
            {
                Socket socket = s_client.Client;
                // A readable socket with nothing to read is a closed one.
                if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0) return false;
                var buffer = new byte[4096];
                while (socket.Available > 0)
                {
                    int n = s_stream.Read(buffer, 0, buffer.Length);
                    if (n <= 0) return false;
                    s_received.Append(Encoding.UTF8.GetString(buffer, 0, n));
                }
                while (s_ready == true && TakeLine(out string? line))
                {
                    if (IosUiDriverProtocol.TryParseAnswer(line!, out int number, out string answer))
                        s_answers[number] = answer;
                    else
                        Debug.LogWarning($"{LogTag} ignored a line that is not an answer");
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException)
            {
                return false;
            }
        }

        private static bool TakeLine(out string? line)
        {
            string text = s_received.ToString();
            int end = text.IndexOf('\n');
            if (end < 0)
            {
                line = null;
                return false;
            }
            line = text.Substring(0, end).TrimEnd('\r');
            s_received.Remove(0, end + 1);
            return true;
        }

        private static bool TryWrite(string line)
        {
            try
            {
                WriteLine(line);
                return true;
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException || e is NullReferenceException)
            {
                return false;
            }
        }

        private static void WriteLine(string line)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
            s_stream!.Write(bytes, 0, bytes.Length);
            s_stream.Flush();
        }

        private static bool ConnectBlocking(string port)
        {
            Close();
            var client = new TcpClient();
            if (!int.TryParse(port, out int portNumber) || !client.ConnectAsync("127.0.0.1", portNumber).Wait(TimeSpan.FromSeconds(ConnectTimeoutSeconds)))
            {
                client.Close();
                return false;
            }
            s_client = client;
            s_stream = client.GetStream();
            WriteLine(IosUiDriverProtocol.Hello(s_run!, Application.identifier));
            string? ready = ReadLineBlocking(TimeSpan.FromSeconds(ConnectTimeoutSeconds));
            return ready != null && IosUiDriverProtocol.IsReady(ready, s_run!);
        }

        private static string? ReadLineBlocking(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            var buffer = new byte[4096];
            while (DateTime.UtcNow < deadline)
            {
                if (TakeLine(out string? line)) return line;
                if (s_client!.Client.Poll(100_000, SelectMode.SelectRead))
                {
                    int n = s_stream!.Read(buffer, 0, buffer.Length);
                    if (n <= 0) return null;
                    s_received.Append(Encoding.UTF8.GetString(buffer, 0, n));
                }
            }
            return null;
        }

        private static void Close()
        {
            try { s_stream?.Dispose(); } catch (Exception) { }
            try { s_client?.Close(); } catch (Exception) { }
            s_stream = null;
            s_client = null;
            s_received.Clear();
        }
    }

    /// <summary>Says BYE to the UI driver when the run ends (design v2, 4.2 and 4.3).</summary>
    [Preserve]
    public sealed class IosUiDriverRunCallback : ITestRunCallback
    {
        public void RunStarted(ITest testsToRun) { }

        public void RunFinished(ITestResult testResults) => IosUiDriver.SayBye();

        public void TestStarted(ITest test) { }

        public void TestFinished(ITestResult result) { }
    }
}
#endif
