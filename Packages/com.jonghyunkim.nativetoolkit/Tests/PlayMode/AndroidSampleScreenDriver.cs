#nullable enable

// Android player only, like the tests that use it.
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Works the Android sample screens from a player test: loads the sample scene, finds and presses
    /// its buttons, and reads what the screen shows. The Android counterpart of
    /// <c>WindowsClipboardSampleScreenDriver</c>, kept separate by the per-platform rule
    /// (agent-rules/coding-rules/common.md).
    /// <para>
    /// The sample scene is loaded additively and unloaded afterwards, so the test runner's scene is
    /// never replaced. The Test Framework puts every scene of the active build profile into the
    /// player it builds; the Android™ Profile lists the sample scene, the Test Android™ Profile does
    /// not, so <see cref="SampleSceneIsInThePlayer"/> tells a test whether it can run.
    /// </para>
    /// <para>
    /// Unity's UI is drawn by the engine and is not in the OS view tree, so the sample's buttons are
    /// pressed from C# (a NavigationSubmitEvent reaches the same handler as a tap). The OS dialogs
    /// they open are pressed by the host-side UI driver (<see cref="AndroidUiDriver"/>).
    /// </para>
    /// </summary>
    internal static class AndroidSampleScreenDriver
    {
        internal const string SampleScene = "NativeToolkitExampleScene";
        internal const float TimeoutSeconds = 10f;

        /// <summary>The first load of the sample scene in a run is far slower than the later ones.</summary>
        internal const float SceneLoadTimeoutSeconds = 30f;

        internal static bool SampleSceneIsInThePlayer() => Application.CanStreamedLevelBeLoaded(SampleScene);

        internal static IEnumerator LoadAtTopMenu(string topMenuButton)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SampleScene, LoadSceneMode.Additive);
            yield return WaitFor(() => load.isDone, "the sample scene to load", SceneLoadTimeoutSeconds);
            yield return WaitFor(() => FindButton(topMenuButton) != null, "the top menu");
        }

        internal static IEnumerator Open(string topMenuButton, string screenButton)
        {
            Press(FindButton(topMenuButton)!);
            yield return WaitFor(() => FindButton(screenButton) != null, $"the screen with {screenButton}");
        }

        internal static IEnumerator Unload()
        {
            Scene scene = SceneManager.GetSceneByName(SampleScene);
            if (!scene.isLoaded) yield break;
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            yield return WaitFor(() => unload.isDone, "the sample scene to unload");
        }

        /// <summary>The sample's UIDocument: the one that lives in the sample scene.</summary>
        internal static UIDocument? SampleDocument()
        {
            Scene scene = SceneManager.GetSceneByName(SampleScene);
            if (!scene.isLoaded) return null;
            return UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude)
                .FirstOrDefault(document => document.gameObject.scene == scene);
        }

        internal static Button? FindButton(string name) => SampleDocument()?.rootVisualElement?.Q<Button>(name);

        internal static string? LabelText(string name) => SampleDocument()?.rootVisualElement?.Q<Label>(name)?.text;

        internal static void Press(Button button)
        {
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        /// <summary>
        /// Polls every frame until <paramref name="condition"/> holds or the timeout passes, and
        /// reports which, for the caller to assert on.
        /// </summary>
        internal static IEnumerator Eventually(Func<bool> condition, Action<bool> met, float timeoutSeconds = TimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    met(false);
                    yield break;
                }
                yield return null;
            }
            met(true);
        }

        internal static IEnumerator WaitFor(Func<bool> condition, string what, float timeoutSeconds = TimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timed out after {timeoutSeconds}s waiting for {what}.");
                yield return null;
            }
        }
    }
}
#endif
