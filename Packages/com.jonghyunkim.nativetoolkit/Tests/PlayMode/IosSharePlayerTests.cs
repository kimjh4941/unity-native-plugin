#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Share;
using static JonghyunKim.NativeToolkit.Tests.IosPlayerTestSupport;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: Share without items fails without opening the share sheet. In 1.x the check is in C#;
    /// the test pins the observable result so the 2.0.0 switch (stage 3-S) can be compared against it.
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosSharePlayerTests
    {
        [UnityTest]
        public IEnumerator ShareWithoutItemsFailsWithoutASheet()
        {
            IosShareResult? result = null;
            IosShareManager.Instance.Share(new IosShareContentPayload(), r => result = r);
            yield return WaitFor(() => result != null);
            Assert.IsNotNull(result, "no share result within the timeout");
            Assert.IsFalse(result!.Value.IsSuccess);
            Assert.IsFalse(result.Value.Completed);
            Assert.IsNotNull(result.Value.ErrorMessage);
        }
    }
}
#endif
#endif
