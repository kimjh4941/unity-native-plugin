#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Share;
using static JonghyunKim.NativeToolkit.Tests.AndroidShareTestSupport;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests of the Android Share manager that open no share sheet: the calls that fail
    /// before it, and the Direct Share targets. Test IDs (AS-21 to AS-36) and expectations are in
    /// artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v5.md, 2.1.
    /// </summary>
    /// <remarks>
    /// No UI driver is used, so this class has no category and runs without one. A regression that
    /// opens the share sheet here would leave Unity paused until the test run's own limit (plan 4).
    /// The rows whose 1.x behaviour differs check the 1.x value on the 1.x bridge (<see cref="OnCApi"/>),
    /// as recorded on 2026-10-12.
    /// </remarks>
    public sealed class AndroidShareManagerPlayerTests
    {
        private const string Missing = "/data/local/tmp/ntk-none.txt";
        private const string Outside = "/system/etc/hosts";

        private AndroidShareEvents _events = new();
        private int _ops;
        private readonly List<string> _cleanupIds = new();

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.Android)
                Assert.Ignore("Device-only.");
            _events.Unsubscribe();
            _events = new AndroidShareEvents();
            _events.Subscribe();
            _ops = 0;
            _cleanupIds.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var failures = new List<string>();

            // Targets this test registered are always removed; that result is in the test's count.
            if (_cleanupIds.Count > 0)
            {
                try
                {
                    Manager.RemoveDirectShareTargets(new RemoveDirectShareTargetsPayload { ids = _cleanupIds.ToArray() });
                }
                catch (Exception e)
                {
                    failures.Add($"removing {string.Join(", ", _cleanupIds)}: {e.Message}");
                }
                _cleanupIds.Clear();
            }

            float deadline = Time.realtimeSinceStartup + TearDownTimeoutSeconds;
            while (!_events.Reached(_ops, 0, 0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            string? mismatch = _events.Mismatch(_ops, 0, 0);
            if (mismatch != null) failures.Add(mismatch);
            string? offThread = _events.OffMainThread();
            if (offThread != null) failures.Add(offThread);

            if (OnCApi)
            {
                deadline = Time.realtimeSinceStartup + TearDownTimeoutSeconds;
                while (PendingCallCount() != 0 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (PendingCallCount() != 0)
                    failures.Add($"{PendingCallCount()} call(s) still waiting for their completion");
            }

            _events.Unsubscribe();
            if (failures.Count > 0) Assert.Fail(string.Join("\n", failures));
        }

        // ── 2.1 Calls that fail before the share sheet ───────────────────────────

        [UnityTest]
        public IEnumerator AS21_ShareFile_Missing() => Fails(
            () => Manager.ShareFile(new ShareFilePayload { filePath = Missing }),
            $"Op|shareFile|False|File not found: {Missing}");

        [UnityTest]
        public IEnumerator AS22_ShareFile_OutsideTheProvider() => Fails(
            () => Manager.ShareFile(new ShareFilePayload { filePath = Outside }),
            $"Op|shareFile|False|File cannot be shared: {Outside}. Ensure the file is in a supported directory.");

        [UnityTest]
        public IEnumerator AS23_ShareFiles_OneMissing() => Fails(
            () => Manager.ShareFiles(new ShareFilesPayload { filePaths = new[] { ExistingFile(), Missing } }),
            $"Op|shareFiles|False|File not found: {Missing}");

        [UnityTest]
        public IEnumerator AS24_ShareFiles_OneOutsideTheProvider()
        {
            string existing = ExistingFile();
            // Known difference (design v6 5.3): the C ABI names every path, 1.x the one that failed.
            string paths = OnCApi ? $"{existing}, {Outside}" : Outside;
            return Fails(() => Manager.ShareFiles(new ShareFilesPayload { filePaths = new[] { existing, Outside } }),
                $"Op|shareFiles|False|File cannot be shared: {paths}. Ensure the file is in a supported directory.");
        }

        [UnityTest]
        public IEnumerator AS25_ShareText_Empty() => Fails(
            () => Manager.ShareText(new ShareTextPayload { text = "" }),
            "Op|shareText|False|Failed to shareText: text is required");

        [UnityTest]
        public IEnumerator AS26_ShareText_Blank() => Fails(
            () => Manager.ShareText(new ShareTextPayload { text = "   " }),
            "Op|shareText|False|Failed to shareText: text is required");

        [UnityTest]
        public IEnumerator AS27_ShareFile_EmptyPath() => Fails(
            () => Manager.ShareFile(new ShareFilePayload { filePath = "" }),
            "Op|shareFile|False|Failed to shareFile: filePath is required");

        [UnityTest]
        public IEnumerator AS28_ShareFiles_None() => Fails(
            () => Manager.ShareFiles(new ShareFilesPayload { filePaths = new string[0] }),
            "Op|shareFiles|False|Failed to shareFiles: filePaths must not be empty");

        /// <summary>
        /// AS-29: a null path. Known difference: 1.x took it as the path "null" (recorded 2026-10-12;
        /// design v6 5.3 had it to check).
        /// </summary>
        [UnityTest]
        public IEnumerator AS29_ShareFiles_NullPath() => Fails(
            () => Manager.ShareFiles(new ShareFilesPayload { filePaths = new[] { ExistingFile(), null! } }),
            OnCApi
                ? "Op|shareFiles|False|Failed to shareFiles: filePaths must not contain null"
                : "Op|shareFiles|False|File not found: null");

        [UnityTest]
        public IEnumerator AS30_Register_EmptyId() => Fails(
            () => Manager.RegisterDirectShareTarget(new DirectShareTargetPayload { id = "", label = "L", iconBase64 = IconBase64() }),
            "Op|registerDirectShareTarget|False|Failed to registerDirectShareTarget: id is required");

        [UnityTest]
        public IEnumerator AS31_Register_IconNotBase64() => Fails(
            () => Manager.RegisterDirectShareTarget(new DirectShareTargetPayload { id = "ntk_as31", label = "L", iconBase64 = "!!!" }),
            "Op|registerDirectShareTarget|False|Invalid icon data for Direct Share target: ntk_as31");

        [UnityTest]
        public IEnumerator AS32_Register_IconNotAnImage() => Fails(
            () => Manager.RegisterDirectShareTarget(new DirectShareTargetPayload
            {
                id = "ntk_as32",
                label = "L",
                iconBase64 = Convert.ToBase64String(Encoding.ASCII.GetBytes("abc")),
            }),
            "Op|registerDirectShareTarget|False|Invalid icon data for Direct Share target: ntk_as32");

        [UnityTest]
        public IEnumerator AS33_Remove_NoIds() => Fails(
            () => Manager.RemoveDirectShareTargets(new RemoveDirectShareTargetsPayload { ids = new string[0] }),
            "Op|removeDirectShareTargets|False|Failed to removeDirectShareTargets: ids must not be empty");

        // ── Direct Share targets ─────────────────────────────────────────────────

        /// <summary>AS-34: one success each, none of them before the call returns (J-S2).</summary>
        [UnityTest]
        public IEnumerator AS34_RegisterThenRemove()
        {
            _cleanupIds.Add("ntk_as34");
            _ops = 3;
            Manager.RegisterDirectShareTarget(new DirectShareTargetPayload { id = "ntk_as34", label = "NTK AS34", iconBase64 = IconBase64() });
            Assert.IsEmpty(_events.Ops, "a result arrived before RegisterDirectShareTarget returned");
            yield return ExpectOp("Op|registerDirectShareTarget|True|null");
            Manager.RemoveDirectShareTargets(new RemoveDirectShareTargetsPayload { ids = new[] { "ntk_as34" } });
            Assert.AreEqual(1, _events.Ops.Count, "a result arrived before RemoveDirectShareTargets returned");
            yield return ExpectOp("Op|removeDirectShareTargets|True|null");
        }

        /// <summary>
        /// AS-35: from another thread (J-S16). The C ABI takes the call. 1.x could not: its manager
        /// read currentActivity through JNI on that thread, logged an error and failed the call
        /// (recorded 2026-10-12; a known difference).
        /// </summary>
        [UnityTest]
        public IEnumerator AS35_RegisterFromAnotherThread()
        {
            _ = Manager;
            _cleanupIds.Add("ntk_as35");
            string icon = IconBase64();
            if (!OnCApi) ExpectOffThreadErrors();
            Exception? thrown = null;
            var thread = new Thread(() =>
            {
                try
                {
                    Manager.RegisterDirectShareTarget(new DirectShareTargetPayload { id = "ntk_as35", label = "NTK AS35", iconBase64 = icon });
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });
            thread.Start();
            thread.Join();
            Assert.IsNull(thrown, thrown?.ToString());
            _ops = 2;  // the registration and the cleanup's removal
            yield return ExpectOp(OnCApi
                ? "Op|registerDirectShareTarget|True|null"
                : "Op|registerDirectShareTarget|False|registerDirectShareTarget could not be started.");
            // The teardown checks that the result was raised on Unity's main thread.
        }

        /// <summary>AS-36: a null payload throws to the caller, and nothing is reported.</summary>
        [Test]
        public void AS36_ShareFile_NullPayload()
        {
            // Known difference (design v6 5.3, J-S7): 1.x threw NullReferenceException.
            Type expected = OnCApi ? typeof(ArgumentNullException) : typeof(NullReferenceException);
            Exception? thrown = null;
            try
            {
                Manager.ShareFile(null!);
            }
            catch (Exception e)
            {
                thrown = e;
            }
            Assert.IsNotNull(thrown, "no exception");
            Assert.AreEqual(expected, thrown!.GetType(), thrown.ToString());
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static AndroidShareManager Manager => AndroidShareManager.Instance;

        /// <summary>Makes a call that reports one result, and counts it for the teardown.</summary>
        private void Request(Action call)
        {
            call();
            _ops++;
        }

        private IEnumerator Fails(Action call, string expected)
        {
            Request(call);
            yield return ExpectOp(expected);
        }

        private IEnumerator ExpectOp(string expected)
        {
            yield return Eventually(() => _events.Ops.Contains(expected));
            Assert.That(_events.Ops, Does.Contain(expected), _events.Describe());
        }

        private static IEnumerator Eventually(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + EventTimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>A file the share FileProvider exposes (files/).</summary>
        private static string ExistingFile()
        {
            string path = Path.Combine(Application.persistentDataPath, "ntk-share-test.txt");
            File.WriteAllText(path, "native toolkit share test");
            return path;
        }

        /// <summary>The two errors 1.x logged when called from another thread (AS-35, AS-52, AS-53).</summary>
        internal static void ExpectOffThreadErrors()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[AndroidShareManager\] Failed to get currentActivity"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[AndroidShareManager\] \w+ failed: currentActivity is null\."));
        }

    }
}
#endif
#endif
