#nullable enable

#if NTK_IOS_PLAYER_PROBE && !UNITY_EDITOR
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.IosPlayerTests
{
    /// <summary>
    /// iOS player: the package's NtkNotificationInstaller.mm made native-toolkit's proxy the notification
    /// center's delegate at launch (artifact/topics/ios-c-abi-2, 3-S design v3, 1.7; V-3 and V-4). The
    /// delegate's class is read by NativeSource~/NtkPlayerTestsProbe.mm, which only a run of
    /// scripts/verify_unity_ios.sh adds (NTK_IOS_PLAYER_PROBE). Here, not in the package's tests: a build
    /// without the probe would not link.
    /// </summary>
    [Category("IosPlayer")]
    public class IosNotificationDelegateProbeTests
    {
        private const string Proxy = "IosLibraryCApi.NotificationDelegateProxy";

        [DllImport("__Internal")] private static extern string? ntkpt_notification_delegate_class();

        [Test]
        public void InstallerSetTheDelegate()
        {
            string? name = ntkpt_notification_delegate_class();
            Debug.Log($"[NTK-IOS-PLAYER] notification delegate class: {name ?? "<none>"}");
#if NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL
            Assert.AreNotEqual(Proxy, name, "the installer ran although NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL is set");
#else
            Assert.AreEqual(Proxy, name);
#endif
        }
    }
}
#endif
