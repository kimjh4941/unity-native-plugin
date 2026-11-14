#nullable enable

#if UNITY_IOS && !UNITY_EDITOR && NTK_IOS_PLAYER_PROBE
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
    /// <para>
    /// Its assembly is built for every platform (empty outside an iOS player with the probe): the Test
    /// Framework searches a player only for the test assemblies loaded in the Editor
    /// (RuntimeTestLauncherBase), so an iOS-only assembly's tests never run (3-S simulator run 1). The
    /// Editor counts an assembly as a test assembly only when its compiled code references NUnit
    /// (EditorLoadedTestAssemblyProvider), which an assembly whose code is all compiled out does not, so
    /// <see cref="ProbeTestAssemblyMarker"/> keeps that reference everywhere (3-S simulator run 2).
    /// </para>
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

namespace JonghyunKim.NativeToolkit.IosPlayerTests
{
    /// <summary>
    /// Compiled on every platform so that this assembly references NUnit even where the probe test is
    /// compiled out; otherwise the Editor does not list it and the iOS player never runs the test.
    /// </summary>
    internal static class ProbeTestAssemblyMarker
    {
        internal static readonly System.Type NUnitReference = typeof(NUnit.Framework.TestAttribute);
    }
}
