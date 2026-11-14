#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Notification;
using static JonghyunKim.NativeToolkit.Tests.IosPlayerTestSupport;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: reads the notification authorization through the native bridge without asking.
    /// scripts/verify_unity_ios.sh installs the player fresh each run, so the answer is NotDetermined.
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosNotificationPlayerTests
    {
        [UnityTest]
        public IEnumerator AuthorizationIsNotDeterminedOnAFreshInstall()
        {
            IosNotificationAuthorizationStatus? status = null;
            IosNotificationManager.Instance.GetAuthorizationStatus(s => status = s);
            yield return WaitFor(() => status != null);
            Assert.IsNotNull(status, "no authorization status within the timeout");
            Assert.AreEqual(IosNotificationAuthorizationStatus.NotDetermined, status);
        }
    }
}
#endif
#endif
