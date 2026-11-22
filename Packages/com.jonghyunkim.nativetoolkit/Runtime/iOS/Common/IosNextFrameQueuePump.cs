#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Common
{
    using UnityEngine;

    /// <summary>
    /// Hands the items of <see cref="IosNextFrameQueue"/> to the main-thread dispatcher once their frame has passed.
    /// It sits on the dispatcher's GameObject.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class IosNextFrameQueuePump : MonoBehaviour
    {
        // No entry log: it runs every frame and has nothing to do in most (csharp.md); HandOver logs when it hands
        // items over.
        private void Update() => IosNextFrameQueue.HandOver(Time.frameCount);
    }
}
#endif
