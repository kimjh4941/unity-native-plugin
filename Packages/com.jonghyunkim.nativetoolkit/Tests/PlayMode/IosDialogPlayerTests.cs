#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Dialog;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: an action sheet without options is rejected before any alert is shown. 1.x checks
    /// the arguments in C#; every path that reaches the native side shows an alert, which is left to
    /// the UI automation (scripts/test_ios_ui.sh).
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosDialogPlayerTests
    {
        [Test]
        public void ActionSheetWithoutOptionsIsRejected()
        {
            string? error = null;
            bool? success = null;
            void OnResult(string? button, bool ok, string? message) { success = ok; error = message; }
            var manager = IosDialogManager.Instance;
            manager.ActionSheetResult += OnResult;
            try
            {
                LogAssert.Expect(LogType.Error, "Options cannot be null or empty.");
                manager.ShowActionSheet("NTK", "player test", new string[0]);
            }
            finally
            {
                manager.ActionSheetResult -= OnResult;
            }
            Assert.AreEqual(false, success);
            Assert.AreEqual("Options cannot be null or empty.", error);
        }
    }
}
#endif
#endif
