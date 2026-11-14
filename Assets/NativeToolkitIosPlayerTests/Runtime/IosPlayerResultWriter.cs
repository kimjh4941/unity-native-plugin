#nullable enable

using System;
using System.IO;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(JonghyunKim.NativeToolkit.IosPlayerTests.IosPlayerResultWriter))]

namespace JonghyunKim.NativeToolkit.IosPlayerTests
{
    /// <summary>
    /// Writes the NUnit result of an iOS player test run into the app container, where
    /// scripts/verify_unity_ios.sh reads it with simctl (artifact/topics/ios-c-abi-2, UI automation
    /// design v4, 3.1).
    /// </summary>
    /// <remarks>
    /// It writes only when the environment variable NTK_RUN names the run (simctl passes it as
    /// SIMCTL_CHILD_NTK_RUN): launch arguments do not reach Environment.GetCommandLineArgs on the iOS
    /// player (stage 2 spike, V-4). The result goes to Documents/ntk-player-tests/&lt;run&gt;.xml (written to
    /// a temporary name, then renamed), then &lt;run&gt;.done. The root element is the test-suite NUnit
    /// gives for the whole run. The player keeps running afterwards; the script stops it.
    /// </remarks>
    [Preserve]
    public sealed class IosPlayerResultWriter : ITestRunCallback
    {
        private const string LogTag = "[NTK-IOS-PLAYER]";

        public void RunStarted(ITest testsToRun) =>
            Debug.Log($"{LogTag} run-started tests={testsToRun.TestCaseCount} run={RunId() ?? "none"}");

        public void RunFinished(ITestResult result)
        {
            string? run = RunId();
            if (run == null)
                return;
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "ntk-player-tests");
                Directory.CreateDirectory(dir);
                string temporary = Path.Combine(dir, run + ".xml.tmp");
                File.WriteAllText(temporary, result.ToXml(true).OuterXml);
                File.Move(temporary, Path.Combine(dir, run + ".xml"));
                File.WriteAllText(Path.Combine(dir, run + ".done"), result.ResultState.ToString());
                Debug.Log($"{LogTag} run-finished run={run} pass={result.PassCount} fail={result.FailCount} skip={result.SkipCount}");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} could not write the result: {e.GetType().Name}: {e.Message}");
            }
        }

        public void TestStarted(ITest test) { }

        public void TestFinished(ITestResult result) { }

        private static string? RunId()
        {
            string? run = Environment.GetEnvironmentVariable("NTK_RUN");
            if (string.IsNullOrEmpty(run))
                return null;
            foreach (char c in run)
                if (!Uri.IsHexDigit(c))
                    return null;
            return run;
        }
    }
}
