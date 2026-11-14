#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.iOS.Common;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: the part of native-toolkit's C ABI every iOS feature shares (artifact/topics/ios-c-abi-2,
    /// 3-S design v3, 1.7). <c>__Internal</c> reaches the C ABI Unity linked and embedded, and it is 2.0.0.
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosCApiVersionPlayerTests
    {
        [Test]
        public void VersionIsTwo()
        {
            Assert.AreEqual(0x020000u, IosNativeToolkitCApi.ntk_version(), "ntk_version()");
            Assert.IsTrue(IosNativeToolkitCApi.IsAvailable, "IsAvailable");
        }
    }
}
#endif
#endif
