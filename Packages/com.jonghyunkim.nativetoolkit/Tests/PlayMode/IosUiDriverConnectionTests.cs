#nullable enable

// iOS player only, like the driver it reaches.
#if UNITY_IOS && !UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The UI driver answers at all (artifact/topics/ios-c-abi-2, UI driver design v3, chapter 6): one round
    /// trip that needs no sample scene, so every player run, the Test iOS Profile's too, has a test of the
    /// category that scripts/verify_unity_ios.sh requires.
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    [Category(IosUiDriver.Category)]
    public sealed class IosUiDriverConnectionTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return IosUiDriver.WaitForPending();
        }

        [UnityTest]
        public IEnumerator Front_IsAnsweredOk()
        {
            yield return IosUiDriver.Do("front");
            Assert.AreEqual("ok", IosUiDriver.LastAnswer);
            Assert.AreEqual(0, IosUiDriver.PendingCount);
        }
    }
}
#endif
