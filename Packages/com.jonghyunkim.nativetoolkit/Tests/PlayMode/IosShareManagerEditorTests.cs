#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Share;
using Api = JonghyunKim.NativeToolkit.Runtime.iOS.Share.IosShareCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// <see cref="IosShareManager"/>'s own handling of a share around the native call (artifact/ios/share, C ABI
    /// design v3, D-1, D-6, D-13), with a stand-in for the native call: a rejection or an exception takes the
    /// call out of the table and gives its callback one failure on the next Update; an accepted call stays
    /// until its completion; a completion after the table was cleared reaches no callback. Also the Editor
    /// path for items 1.x would have left out. Editor only: in an iOS player these would sit outside the
    /// IosPlayer category and break the run's case count.
    /// </summary>
    public sealed class IosShareManagerEditorTests
    {
        private readonly List<string> _seen = new();
        private Action<IosShareResult>? _onEvent;

        [SetUp]
        public void SetUp()
        {
            _seen.Clear();
            _onEvent = r => _seen.Add($"event {Describe(r)}");
            IosShareManager.Instance.ShareCompleted += _onEvent;
        }

        [TearDown]
        public void TearDown() => IosShareManager.Instance.ShareCompleted -= _onEvent;

        private static string Describe(IosShareResult r) =>
            r.IsSuccess ? $"ok completed={r.Completed} activity={r.ActivityType ?? "<null>"}" : $"fail {r.ErrorMessage}";

        private static Api.Prepared Text() => Api.Prepare(new IosShareContentPayload { items = new[] { IosShareItem.Text("a") } });

        [UnityTest]
        public IEnumerator Editor_UnknownTypeOnly_IsStillUnavailable()
        {
            // D-6: the Editor check comes before the items are passed on, as in 1.x.
            IosShareManager.Instance.Share(new IosShareContentPayload { items = new[] { new IosShareItem { type = "unknown", value = "x" } } },
                r => _seen.Add($"callback {Describe(r)}"));
            yield return null;
            CollectionAssert.AreEqual(new[] { $"event fail {Api.Unavailable}", $"callback fail {Api.Unavailable}" }, _seen);
        }

        [UnityTest]
        public IEnumerator Rejected_TakesTheCallOut_AndFailsOnceOnTheNextUpdate()
        {
            long other = IosShareManager.s_pending.Add(_ => _seen.Add("other"));
            int before = IosShareManager.s_pending.Count;
            LogAssert.Expect(LogType.Error, "[IosShareManager][Share] rejected: INVALID_PARAMETER");

            IosShareManager.Begin(Text(), r => _seen.Add($"callback {Describe(r)}"),
                (ref Api.ShareRequest request, IntPtr userData) => Api.ErrorInvalidParameter);
            Assert.AreEqual(before, IosShareManager.s_pending.Count, "the rejected call is still recorded");
            CollectionAssert.IsEmpty(_seen, "a result came inside the call");

            yield return null;
            string message = "fail Failed to share: the request was rejected (INVALID_PARAMETER).";
            CollectionAssert.AreEqual(new[] { $"event {message}", $"callback {message}" }, _seen);
            Assert.IsTrue(IosShareManager.s_pending.TryTake(other, out _), "another pending call was lost");
        }

        [UnityTest]
        public IEnumerator Exception_TakesTheCallOut_AndReportsAnInternalError()
        {
            int before = IosShareManager.s_pending.Count;
            LogAssert.Expect(LogType.Error, "[IosShareManager][Share] boom");

            IosShareManager.Begin(Text(), r => _seen.Add($"callback {Describe(r)}"),
                (ref Api.ShareRequest request, IntPtr userData) => throw new InvalidOperationException("boom"));
            Assert.AreEqual(before, IosShareManager.s_pending.Count);

            yield return null;
            CollectionAssert.AreEqual(new[] { "event fail Internal error: boom", "callback fail Internal error: boom" }, _seen);
        }

        [UnityTest]
        public IEnumerator Accepted_WaitsForItsCompletion_EachCallGetsItsOwn()
        {
            var ids = new List<long>();
            IosShareManager.NativeShare accept = (ref Api.ShareRequest request, IntPtr userData) =>
            {
                Assert.AreEqual(56u, request.StructSize);
                ids.Add(userData.ToInt64());
                return Api.ErrorNone;
            };
            IosShareManager.Begin(Text(), r => _seen.Add($"first {Describe(r)}"), accept);
            IosShareManager.Begin(Text(), r => _seen.Add($"second {Describe(r)}"), accept);
            yield return null;
            CollectionAssert.IsEmpty(_seen, "an accepted call completed by itself");

            // Completed in the other order (Share.h promises no order).
            IosShareManager.Complete(ids[1], Api.ToResult(Api.ErrorShowFailed, false, null));
            IosShareManager.Complete(ids[0], Api.ToResult(Api.ErrorNone, false, null));
            CollectionAssert.IsEmpty(_seen, "raised inside the completion");
            yield return null;
            CollectionAssert.AreEqual(new[]
            {
                "event fail Failed to present the share sheet.", "second fail Failed to present the share sheet.",
                "event ok completed=False activity=<null>", "first ok completed=False activity=<null>",
            }, _seen);
        }

        [UnityTest]
        public IEnumerator CompletionBeforeTheCallReturns_ReachesItsCall()
        {
            // Share.h: called off the main thread, the completion may come before the call returns. The
            // call is recorded before the native call, so it still reaches its callback, once.
            int before = IosShareManager.s_pending.Count;
            IosShareManager.Begin(Text(), r => _seen.Add($"callback {Describe(r)}"), (ref Api.ShareRequest request, IntPtr userData) =>
            {
                IosShareManager.Complete(userData.ToInt64(), Api.ToResult(Api.ErrorNone, true, "x.y"));
                return Api.ErrorNone;
            });
            Assert.AreEqual(before, IosShareManager.s_pending.Count, "the completed call is still recorded");
            yield return null;
            CollectionAssert.AreEqual(new[] { "event ok completed=True activity=x.y", "callback ok completed=True activity=x.y" }, _seen);
        }

        [UnityTest]
        public IEnumerator DestroyingTheManager_ForgetsItsCalls_AndALateCompletionCreatesNoManager()
        {
            var ids = new List<long>();
            IosShareManager.Begin(Text(), r => _seen.Add("callback"), (ref Api.ShareRequest request, IntPtr userData) =>
            {
                ids.Add(userData.ToInt64());
                return Api.ErrorNone;
            });
            IosShareManager.Instance.ShareCompleted -= _onEvent;
            UnityEngine.Object.Destroy(IosShareManager.Instance.gameObject);
            yield return null;

            Assert.IsFalse(IosShareManager.s_pending.TryTake(ids[0], out _), "OnDestroy did not clear the table");
            IosShareManager.Complete(ids[0], Api.ToResult(Api.ErrorNone, false, null));
            yield return null;
            CollectionAssert.IsEmpty(_seen, "a late completion reached the destroyed manager's call");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<IosShareManager>(FindObjectsSortMode.None).Length,
                "a late completion created a manager");
        }
    }
}
#endif
