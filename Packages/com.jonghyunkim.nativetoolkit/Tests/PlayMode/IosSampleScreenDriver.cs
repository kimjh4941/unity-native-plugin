#nullable enable

// iOS player only, like the tests that use it.
#if UNITY_IOS && !UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Works the iOS sample screens from a player test (artifact/topics/ios-c-abi-2, UI driver design v3): loads
    /// the sample scene, presses its buttons, reads what the screen shows, and checks the layout. The iOS
    /// counterpart of <c>AndroidSampleScreenDriver</c>, kept separate by the per-platform rule.
    /// </summary>
    /// <remarks>
    /// The sample scene is loaded additively and unloaded afterwards. The Test Framework puts every scene of
    /// the active build profile into the player; the iOS Profile lists the sample scene, the Test iOS Profile
    /// does not (<see cref="SampleSceneIsInThePlayer"/>). Buttons are pressed with a NavigationSubmitEvent (the
    /// handler a tap reaches); the system screens they open are pressed by the UI driver (<see cref="IosUiDriver"/>).
    /// On a device, the Clipboard screen is guarded (design v3, chapter 7): the buttons that reach the general
    /// pasteboard are refused by name, and while the screen's scope reads "general" only the buttons that switch
    /// to a named pasteboard are let through.
    /// </remarks>
    internal static class IosSampleScreenDriver
    {
        internal const string SampleScene = "NativeToolkitExampleScene";
        internal const float TimeoutSeconds = 10f;

        /// <summary>The first load of the sample scene in a run is far slower than the later ones.</summary>
        internal const float SceneLoadTimeoutSeconds = 30f;

        internal const string TopMenuDialog = "DialogFeatureButton";
        internal const string TopMenuNotification = "NotificationFeatureButton";
        internal const string TopMenuShare = "ShareFeatureButton";
        internal const string TopMenuClipboard = "ClipboardFeatureButton";

        /// <summary>The Clipboard screen's status line ("Scope: … | Observing: … | Events: …").</summary>
        internal const string ClipboardStatusLabel = "StatusTextBlock";

        // Design v3, chapter 7. Refused by name on a device: these reach the general pasteboard whatever the
        // screen's scope (ErrRemoveGeneral removes the general one directly).
        private static readonly HashSet<string> s_generalButtons = new() { "UseGeneralButton", "ErrRemoveGeneralButton" };

        // Let through on a device while the scope is general: they switch to a named pasteboard.
        private static readonly HashSet<string> s_switchButtons = new()
        {
            "UseFixedNamedScopeButton", "CreateNamedPasteboardButton", "CreateUniquePasteboardButton",
        };

        internal static bool SampleSceneIsInThePlayer() => Application.CanStreamedLevelBeLoaded(SampleScene);

        /// <summary>
        /// Whether the player runs on a device. A simulator gives its apps SIMULATOR_UDID; without it the
        /// player counts as on a device, so the device guards hold when in doubt.
        /// </summary>
        internal static bool OnDevice => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATOR_UDID"));

        internal static IEnumerator LoadAtTopMenu(string topMenuButton)
        {
            if (!SampleSceneIsInThePlayer())
                Assert.Ignore("The sample scene is not in this player (the Test iOS Profile lists no sample scene).");
            AsyncOperation load = SceneManager.LoadSceneAsync(SampleScene, LoadSceneMode.Additive);
            yield return WaitFor(() => load.isDone, "the sample scene to load", SceneLoadTimeoutSeconds);
            yield return WaitFor(() => FindButton(topMenuButton) != null, "the top menu");
        }

        internal static IEnumerator Open(string topMenuButton, string screenButton)
        {
            Press(FindButton(topMenuButton) ?? throw new AssertionException($"No {topMenuButton} on the top menu."));
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
            return UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(document => document.gameObject.scene == scene);
        }

        internal static Button? FindButton(string name) => SampleDocument()?.rootVisualElement?.Q<Button>(name);

        internal static string? LabelText(string name) => SampleDocument()?.rootVisualElement?.Q<Label>(name)?.text;

        /// <summary>Presses a button of the sample screen, after the device guard of the Clipboard screen.</summary>
        internal static void Press(Button button)
        {
            GuardGeneralPasteboard(button);
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        /// <summary>Presses the named button of the sample screen.</summary>
        internal static void Press(string name) =>
            Press(FindButton(name) ?? throw new AssertionException($"No {name} on the sample screen."));

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

        private static void GuardGeneralPasteboard(Button button)
        {
            if (!OnDevice) return;
            if (s_generalButtons.Contains(button.name))
                Assert.Fail($"{button.name} reaches the general pasteboard; a device run never presses it (UI driver design v3, chapter 7).");
            if (FindButton("UseGeneralButton") == null) return; // not the Clipboard screen
            // A missing status line fails by name rather than reading as "general" (implementation review v1,
            // R-1: a wrong label name made every press after the switch fail on the device).
            string? status = SampleDocument()?.rootVisualElement?.Q<Label>(ClipboardStatusLabel)?.text;
            if (status == null)
                Assert.Fail($"The Clipboard screen has no {ClipboardStatusLabel}; the device guard cannot read the scope.");
            bool general = status!.IndexOf("Scope: general", StringComparison.Ordinal) >= 0;
            if (general && !s_switchButtons.Contains(button.name))
                Assert.Fail($"{button.name} would act on the general pasteboard; switch to a named one first (UI driver design v3, chapter 7).");
        }
    }
}
#endif
