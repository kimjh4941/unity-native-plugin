#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard
{
    using UnityEngine;

    /// <summary>
    /// Hands the clipboard results decided during a call to the main-thread dispatcher once their frame has
    /// passed (artifact/ios/clipboard, C ABI design v5, D-12). It sits on the dispatcher's GameObject, so it
    /// runs whether or not <see cref="IosClipboardManager"/> is enabled, and the results join the dispatcher's
    /// queue behind what is already there.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class IosClipboardLaterPump : MonoBehaviour
    {
        // No entry log: it runs every frame and has nothing to do in most (csharp.md); DeliverLater logs when
        // it hands results over.
        private void Update() => IosClipboardManager.DeliverLater(Time.frameCount);
    }
}
#endif
