#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The lines between the iOS player tests and the UI driver (artifact/topics/ios-c-abi-2, UI driver design
    /// v3, chapter 3): building requests and reading answers. No Unity API and no platform guard, so the
    /// Editor's PlayMode run can check it (<c>IosUiDriverProtocolTests</c>); <c>IosUiDriver</c> sends them.
    /// </summary>
    internal static class IosUiDriverProtocol
    {
        /// <summary>A line is at most this many UTF-8 bytes, newline excluded (design 3.1).</summary>
        internal const int MaxLineBytes = 4096;

        /// <summary>The driver's answer to <see cref="Bye"/>.</summary>
        internal const string ByeOk = "BYE ok";

        /// <summary>The wait for a command whose own limit is 10 s (design 3.3).</summary>
        internal const float AnswerTimeoutSeconds = 30f;

        /// <summary>The wait for a screen command, whose own limit is 60 s (design 3.3).</summary>
        internal const float ScreenAnswerTimeoutSeconds = 120f;

        /// <summary>
        /// The Test Framework's limit for a test that waits for a screen command (design 3.3; its default 180 s is
        /// too short: up to 170 s for the test and 120 s for the teardown's WaitForPending. Review v1, R-5).
        /// </summary>
        internal const int ScreenTestTimeoutMilliseconds = 300000;

        /// <summary>The screen commands: 60 s on the driver's side, and they close what they opened on failure.</summary>
        internal static readonly HashSet<string> ScreenCommands = new()
        {
            "sheet-close", "sheet-absent", "permission-answer", "settings-close", "banner-tap",
        };

        /// <summary>Commands whose first argument is a span in milliseconds that adds to the wait.</summary>
        internal static readonly HashSet<string> AbsentCommands = new()
        {
            "alert-absent", "sheet-absent",
        };

        // \A and \z, not ^ and $: .NET's $ also matches before a final "\n", which would let a value with a
        // trailing newline split a line (implementation review v1, Codex C-4).
        private static readonly Regex s_command = new(@"\A[a-z]+(-[a-z]+)*\z", RegexOptions.CultureInvariant);
        private static readonly Regex s_argument = new(@"\A[A-Za-z0-9+/=-]+\z", RegexOptions.CultureInvariant);
        private static readonly Regex s_run = new(@"\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant);
        private static readonly Regex s_reason = new(@"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);

        /// <summary>A new run id: 32 lower-case hex digits.</summary>
        internal static string NewRun() => Guid.NewGuid().ToString("N");

        /// <summary>A string argument: Base64 of its UTF-8 bytes.</summary>
        internal static string Base64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        internal static string Hello(string run, string bundleId)
        {
            RequireRun(run);
            if (string.IsNullOrEmpty(bundleId) || bundleId.IndexOfAny(new[] { ' ', '\n', '\r' }) >= 0)
                throw new ArgumentException($"Not a bundle id: '{bundleId}'.", nameof(bundleId));
            return Limit($"HELLO {run} {bundleId}");
        }

        internal static string Bye(string run)
        {
            RequireRun(run);
            return $"BYE {run}";
        }

        /// <summary>Whether <paramref name="line"/> is the driver's READY for <paramref name="run"/>.</summary>
        internal static bool IsReady(string line, string run) => line == $"READY {run}";

        /// <summary>
        /// A request line. The command is lower-case words joined by '-'; each argument is either raw
        /// (letters, digits and '-') or Base64 (<see cref="Base64"/>), never empty.
        /// </summary>
        internal static string Request(int number, string command, IReadOnlyList<string> args)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number), "Request numbers start at 1.");
            if (!s_command.IsMatch(command)) throw new ArgumentException($"Not a command: '{command}'.", nameof(command));
            var line = new StringBuilder($"{number} {command}");
            foreach (string arg in args)
            {
                if (!s_argument.IsMatch(arg))
                    throw new ArgumentException($"An argument of {command} is empty or not raw/Base64: '{arg}'. Use Base64() for text.");
                line.Append(' ').Append(arg);
            }
            return Limit(line.ToString());
        }

        /// <summary>
        /// Reads an answer line: "&lt;number&gt; ok", "&lt;number&gt; ok &lt;value&gt;" or "&lt;number&gt; fail &lt;reason&gt;".
        /// <paramref name="answer"/> is the part after the number ("ok", "ok value", "fail reason").
        /// False for anything else, including a reason outside the fixed words' alphabet.
        /// </summary>
        internal static bool TryParseAnswer(string line, out int number, out string answer)
        {
            number = 0;
            answer = "";
            int space = line.IndexOf(' ');
            if (space <= 0 || !int.TryParse(line.Substring(0, space), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out number) || number < 1)
                return false;
            string rest = line.Substring(space + 1);
            if (rest == "ok" || (rest.StartsWith("ok ", StringComparison.Ordinal) && rest.Length > 3))
            {
                answer = rest;
                return true;
            }
            if (rest.StartsWith("fail ", StringComparison.Ordinal) && s_reason.IsMatch(rest.Substring(5)))
            {
                answer = rest;
                return true;
            }
            number = 0;
            return false;
        }

        /// <summary>Whether an answer counts as success: "ok" or "ok &lt;value&gt;" only.</summary>
        internal static bool IsSuccess(string answer) =>
            answer == "ok" || answer.StartsWith("ok ", StringComparison.Ordinal);

        /// <summary>The value of "ok &lt;value&gt;", or "".</summary>
        internal static string ValueOf(string answer) =>
            answer.StartsWith("ok ", StringComparison.Ordinal) ? answer.Substring(3) : "";

        /// <summary>
        /// How long the test waits for the answer to <paramref name="command"/> (design 3.3): 30 s, 120 s for a
        /// screen command, plus the span for an "-absent" command.
        /// </summary>
        internal static float AnswerTimeoutFor(string command, IReadOnlyList<string> args)
        {
            float wait = ScreenCommands.Contains(command) ? ScreenAnswerTimeoutSeconds : AnswerTimeoutSeconds;
            if (AbsentCommands.Contains(command) && args.Count > 0 &&
                int.TryParse(args[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int ms))
            {
                wait += ms / 1000f;
            }
            return wait;
        }

        private static void RequireRun(string run)
        {
            if (!s_run.IsMatch(run)) throw new ArgumentException($"Not a run id: '{run}'.", nameof(run));
        }

        private static string Limit(string line)
        {
            if (Encoding.UTF8.GetByteCount(line) > MaxLineBytes)
                throw new ArgumentException($"A UI driver line is limited to {MaxLineBytes} bytes.");
            return line;
        }
    }
}
