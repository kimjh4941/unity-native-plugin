#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Share;
using static JonghyunKim.NativeToolkit.Tests.IosPlayerTestSupport;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: the Share calls that fail without a sheet (Share UI test plan v3, section 2,
    /// artifact/ios/share/designs/2026-11-14-ios-share-ui-test-plan-v3.md). They pin the 1.x results so
    /// the 2.0.0 switch (stage 3) can be compared against them. SH-20 to SH-22 are checked in C#; SH-23 to
    /// SH-25 reach the native parser, which in 1.x drops the bad items and fails with no items left.
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosSharePlayerTests
    {
        private const string NoItems = "No shareable items were provided.";

        /// <summary>
        /// Shares and expects one failure: the ShareCompleted event first and then the per-call callback,
        /// each exactly once and with the same result (SH-22's contract, also on the paths that reach the
        /// native side: SH-23 to SH-25).
        /// </summary>
        private static IEnumerator ShareAndExpect(IosShareContentPayload? payload, string error)
        {
            IosShareResult? result = null;
            var order = new List<string>();
            IosShareResult? eventResult = null;
            Action<IosShareResult> onEvent = r => { order.Add("event"); eventResult = r; };
            IosShareManager.Instance.ShareCompleted += onEvent;
            try
            {
                IosShareManager.Instance.Share(payload, r => { order.Add("callback"); result = r; });
                yield return WaitFor(() => result != null);
                yield return WaitFor(() => false, 1f);
            }
            finally
            {
                IosShareManager.Instance.ShareCompleted -= onEvent;
            }
            Assert.IsNotNull(result, "no share result within the timeout");
            CollectionAssert.AreEqual(new[] { "event", "callback" }, order);
            Assert.IsFalse(result!.Value.IsSuccess);
            Assert.IsFalse(result.Value.Completed);
            Assert.IsNull(result.Value.ActivityType);
            Assert.AreEqual(error, result.Value.ErrorMessage);
            Assert.AreEqual(error, eventResult!.Value.ErrorMessage);
        }

        /// <summary>SH-20 (the stage 2 test ShareWithoutItemsFailsWithoutASheet).</summary>
        [UnityTest]
        public IEnumerator SH20_ShareWithoutItemsFailsWithoutASheet() =>
            ShareAndExpect(new IosShareContentPayload(), NoItems);

        [UnityTest]
        public IEnumerator SH21_NullPayloadFails() => ShareAndExpect(null, NoItems);

        /// <summary>SH-22: the event comes before the per-call callback, each exactly once.</summary>
        [UnityTest]
        public IEnumerator SH22_EventComesBeforeCallbackOnce()
        {
            var order = new List<string>();
            Action<IosShareResult> onEvent = _ => order.Add("event");
            IosShareManager.Instance.ShareCompleted += onEvent;
            try
            {
                IosShareManager.Instance.Share(new IosShareContentPayload(), _ => order.Add("callback"));
                yield return WaitFor(() => order.Count >= 2);
                yield return WaitFor(() => false, 1f);
                CollectionAssert.AreEqual(new[] { "event", "callback" }, order);
            }
            finally
            {
                IosShareManager.Instance.ShareCompleted -= onEvent;
            }
        }

        // SH-23 to SH-25 reach the native parser: compiled out after the 3-S switch until Share moves to
        // the C ABI (artifact/topics/ios-c-abi-2, 3-S design v3, 1.7).
#if !NTK_IOS_CABI_SWITCHED || NTK_IOS_CABI_2_SHARE
        [UnityTest]
        public IEnumerator SH23_UnknownTypeOnlyFails() =>
            ShareAndExpect(new IosShareContentPayload { items = new[] { new IosShareItem { type = "unknown", value = "x" } } }, NoItems);

        [UnityTest]
        public IEnumerator SH24_NullItemOnlyFails() =>
            ShareAndExpect(new IosShareContentPayload { items = new IosShareItem[] { null! } }, NoItems);

        [UnityTest]
        public IEnumerator SH25_NullTextValueOnlyFails() =>
            ShareAndExpect(new IosShareContentPayload { items = new[] { new IosShareItem { type = "text", value = null! } } }, NoItems);
#endif

#if NTK_IOS_CABI_SWITCHED && !NTK_IOS_CABI_2_SHARE
        /// <summary>
        /// 3-S (3-S design v3, 1.6): until Share moves to the C ABI, a share that passes the C# checks fails
        /// with "iOS share is only available on an iOS device." without a sheet. Nothing comes inside the
        /// call; after the next Update the event and then the callback, once each; nothing more within a second.
        /// </summary>
        [UnityTest]
        public IEnumerator UnavailableUntilMoved()
        {
            const string error = "iOS share is only available on an iOS device.";
            var seen = new List<(string source, IosShareResult result)>();
            Action<IosShareResult> onEvent = r => seen.Add(("event", r));
            IosShareManager.Instance.ShareCompleted += onEvent;
            try
            {
                IosShareManager.Instance.Share(
                    new IosShareContentPayload { items = new[] { new IosShareItem { type = "text", value = "ntk player" } } },
                    r => seen.Add(("callback", r)));
                Assert.AreEqual(0, seen.Count, "a result came inside the call");

                yield return null;
                Assert.AreEqual(2, seen.Count, "not one event and one callback after the next Update");
                CollectionAssert.AreEqual(new[] { "event", "callback" }, new[] { seen[0].source, seen[1].source });
                foreach (var (source, r) in seen)
                {
                    Assert.IsFalse(r.IsSuccess, source);
                    Assert.IsFalse(r.Completed, source);
                    Assert.IsNull(r.ActivityType, source);
                    Assert.AreEqual(error, r.ErrorMessage, source);
                }

                yield return WaitFor(() => false, 1f);
                Assert.AreEqual(2, seen.Count, "a result came later");
            }
            finally
            {
                IosShareManager.Instance.ShareCompleted -= onEvent;
            }
        }
#endif
    }
}
#endif
#endif
