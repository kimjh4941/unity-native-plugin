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

        /// <summary>The label every iOS sample screen shows its results in.</summary>
        internal const string ResultLabel = "ResultTextBlock";

        /// <summary>The least height a sample button may have: the samples' buttons are 46 tall.</summary>
        private const float MinButtonHeight = 44f;

        /// <summary>
        /// The layout problems of the screen shown now (UI driver design v3, chapter 9; sample plan v3, D-5): for
        /// every visible button, scrolled into view and, inside a ScrollView, on into the safe area when the scroll
        /// allows it, whether it lies inside <see cref="Screen.safeArea"/>, inside the ScrollView's viewport, and
        /// whether <c>panel.Pick</c> at its centre reaches it. Also (a) whether the root's padding is the part of
        /// the screen outside the safe area and the root carries the landscape class exactly in landscape, and
        /// (c) whether the result box is inside its header and the safe area and can be seen, with the screen's
        /// result and with a long one. Each problem names the element (the sample's own names) and what is wrong.
        /// </summary>
        internal static IEnumerator CheckLayout(List<string> problems, bool expectResult)
        {
            UIDocument? document = SampleDocument();
            if (document == null)
            {
                problems.Add("no sample document");
                yield break;
            }
            VisualElement root = document.rootVisualElement;
            IPanel panel = root.panel;
            Rect safe = Screen.safeArea;
            // Screen y grows upwards, panel y downwards.
            Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMin, Screen.height - safe.yMax));
            Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMax, Screen.height - safe.yMin));
            Rect safePanel = Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
            CheckPadding(root, panel, safePanel, problems, expectResult);
            foreach (Button button in document.rootVisualElement.Query<Button>().ToList())
            {
                if (button.resolvedStyle.display == DisplayStyle.None || button.resolvedStyle.visibility == Visibility.Hidden)
                    continue;
                ScrollView? scroll = button.GetFirstAncestorOfType<ScrollView>();
                if (scroll != null)
                {
                    scroll.ScrollTo(button);
                    // Only up and down, as a user scrolls: ScrollTo also moves sideways, which brought a button
                    // wider than the viewport into it and hid that it was cut (the top menu, 2026-11-22).
                    scroll.scrollOffset = new Vector2(0f, scroll.scrollOffset.y);
                    yield return null;
                    yield return null;
                    // ScrollTo stops with the button on the edge of the view, which can be outside the safe area
                    // while the scroll could bring it in: move on by what is missing, then look again.
                    Rect seen = button.worldBound;
                    float down = Math.Max(0f, seen.yMax - safePanel.yMax);
                    float up = Math.Max(0f, safePanel.yMin - seen.yMin);
                    if (down > 0f || up > 0f)
                    {
                        scroll.scrollOffset += new Vector2(0f, down - up);
                        yield return null;
                        yield return null;
                    }
                }
                Rect r = button.worldBound;
                if (r.width <= 0f || r.height <= 0f)
                {
                    // Hidden through an ancestor: not on the screen, nothing to check.
                    if (HiddenByAncestor(button)) continue;
                    problems.Add($"{button.name}: shown but has no size [{r.width:F0}x{r.height:F0}]");
                    continue;
                }
                if (r.height < MinButtonHeight)
                {
                    problems.Add($"{button.name}: squashed to {r.height:F0} tall (the samples' buttons are 46)");
                    continue;
                }
                if (scroll != null && !Inside(r, scroll.contentViewport.worldBound))
                {
                    Rect v = scroll.contentViewport.worldBound;
                    problems.Add($"{button.name}: not inside the scroll view's viewport " +
                                 $"[{r.xMin:F0},{r.yMin:F0},{r.xMax:F0},{r.yMax:F0}] vs [{v.xMin:F0},{v.yMin:F0},{v.xMax:F0},{v.yMax:F0}]");
                    continue;
                }
                if (r.xMin < safePanel.xMin - 0.5f || r.xMax > safePanel.xMax + 0.5f ||
                    r.yMin < safePanel.yMin - 0.5f || r.yMax > safePanel.yMax + 0.5f)
                {
                    problems.Add($"{button.name}: outside the safe area " +
                                 $"[{r.xMin:F0},{r.yMin:F0},{r.xMax:F0},{r.yMax:F0}] vs [{safePanel.xMin:F0},{safePanel.yMin:F0},{safePanel.xMax:F0},{safePanel.yMax:F0}]");
                    continue;
                }
                VisualElement? picked = panel.Pick(r.center);
                if (picked == null || (picked != button && !button.Contains(picked)))
                    problems.Add($"{button.name}: covered by {picked?.name ?? picked?.GetType().Name ?? "nothing"}");
            }

            CheckLabels(root, safePanel, problems);

            Label? result = root.Q<Label>(ResultLabel);
            if (result == null)
            {
                if (expectResult) problems.Add($"no {ResultLabel} on a sample screen");
                yield break; // the top menu has no result
            }
            CheckResult(result, panel, safePanel, problems, "");
            // A long result, such as a native error message (sample plan v3, R3-1): the box keeps its fixed height
            // inside its header, the text scrolls inside it, and the button list below stays at least a button tall
            // (the result boxes are fixed since 2026-11-22; before, a long result grew the header).
            string before = result.text;
            result.text = "Long result: " + string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 6)) +
                          "\nSecond line of the result.\nThird line of the result.";
            yield return null;
            yield return null;
            CheckResult(result, panel, safePanel, problems, " (long result)");
            ScrollView? list = root.Query<ScrollView>().ToList().FirstOrDefault(view => !view.Contains(result));
            float listHeight = list?.contentViewport.worldBound.height ?? -1f;
            Debug.Log($"[IosSampleScreenDriver] long result: button list viewport height {listHeight:F0} (orientation {Screen.orientation})");
            // Accepted (R3-2): the list gets shorter while a long result shows, but stays at least a button tall.
            if (listHeight < MinButtonHeight)
                problems.Add($"long result: the button list is {listHeight:F0} tall, less than a button");
            result.text = before;
            yield return null;
        }

        /// <summary>(a): the padding IosSampleSafeArea puts on the root, and its landscape class.</summary>
        private static void CheckPadding(VisualElement root, IPanel panel, Rect safePanel, List<string> problems, bool sampleScreen)
        {
            Vector2 size = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));
            var expected = new[] { safePanel.yMin, size.y - safePanel.yMax, safePanel.xMin, size.x - safePanel.xMax };
            IResolvedStyle style = root.resolvedStyle;
            var actual = new[] { style.paddingTop, style.paddingBottom, style.paddingLeft, style.paddingRight };
            string[] sides = { "top", "bottom", "left", "right" };
            for (int i = 0; i < sides.Length; i++)
            {
                if (Math.Abs(actual[i] - Math.Max(0f, expected[i])) > 1f)
                    problems.Add($"root padding {sides[i]}: {actual[i]:F1}, the safe area needs {Math.Max(0f, expected[i]):F1}");
            }
            // The padding colour (implementation review v1, M1; Codex M1): the four sample screens must declare it,
            // so a missing or misspelt declaration fails instead of falling back to their black background; the top
            // menu declares none and takes its first element's background.
            if (root.childCount == 0)
            {
                problems.Add("root: no screen inside");
            }
            else if (root[0].customStyle.TryGetValue(new CustomStyleProperty<Color>(IosSampleSafeArea.InsetColorProperty), out Color declared))
            {
                if (root.resolvedStyle.backgroundColor != declared)
                    problems.Add($"root: padding colour {root.resolvedStyle.backgroundColor}, the screen declares {declared}");
            }
            else if (sampleScreen)
            {
                problems.Add($"root: the sample screen declares no {IosSampleSafeArea.InsetColorProperty}");
            }
            else if (root.resolvedStyle.backgroundColor != root[0].resolvedStyle.backgroundColor)
            {
                problems.Add($"root: padding colour {root.resolvedStyle.backgroundColor}, the screen's background is {root[0].resolvedStyle.backgroundColor}");
            }
            bool landscape = Screen.width > Screen.height;
            if (root.ClassListContains(IosSampleSafeArea.LandscapeClass) != landscape)
                problems.Add($"root: {IosSampleSafeArea.LandscapeClass} is {(landscape ? "missing" : "set")} in {(landscape ? "landscape" : "portrait")}");
        }

        /// <summary>
        /// (c): the box of the result (the header's child that holds <see cref="ResultLabel"/>) lies inside the
        /// header and the safe area, is at least a line tall, and is what a tap at its centre reaches. On the
        /// Clipboard screen the status line has to stay inside the header too.
        /// </summary>
        private static void CheckResult(Label result, IPanel panel, Rect safePanel, List<string> problems, string when)
        {
            VisualElement box = result;
            VisualElement? header = result.parent;
            while (header != null && !IsHeader(header))
            {
                box = header;
                header = header.parent;
            }
            if (header == null)
            {
                problems.Add($"{ResultLabel}{when}: no header above it");
                return;
            }
            Rect b = box.worldBound;
            Rect h = header.worldBound;
            if (!Inside(b, h))
                problems.Add($"{ResultLabel}{when}: its box [{b.xMin:F0},{b.yMin:F0},{b.xMax:F0},{b.yMax:F0}] is not inside the header [{h.xMin:F0},{h.yMin:F0},{h.xMax:F0},{h.yMax:F0}]");
            if (!Inside(b, safePanel))
                problems.Add($"{ResultLabel}{when}: its box [{b.xMin:F0},{b.yMin:F0},{b.xMax:F0},{b.yMax:F0}] is outside the safe area");
            if (b.height < 20f)
                problems.Add($"{ResultLabel}{when}: its box is {b.height:F0} tall, less than a line");
            VisualElement? picked = panel.Pick(b.center);
            if (picked == null || (picked != box && !box.Contains(picked)))
                problems.Add($"{ResultLabel}{when}: covered by {picked?.name ?? picked?.GetType().Name ?? "nothing"}");
            Label? status = header.Q<Label>(ClipboardStatusLabel);
            if (status != null && !Inside(status.worldBound, h))
                problems.Add($"{ClipboardStatusLabel}{when}: not inside the header");

            // The text itself (implementation review of 2026-11-22): it scrolls in a ScrollView inside the box, is shown,
            // is no wider than that view, and can be scrolled to its end when it is longer than the view.
            ScrollView? textView = result.GetFirstAncestorOfType<ScrollView>();
            if (textView == null || !box.Contains(textView))
            {
                problems.Add($"{ResultLabel}{when}: not in a scroll view inside its box");
                return;
            }
            Rect v = textView.contentViewport.worldBound;
            Rect t = result.worldBound;
            if (t.width <= 0f || t.height <= 0f)
            {
                problems.Add($"{ResultLabel}{when}: its text is not shown [{t.width:F0}x{t.height:F0}]");
                return;
            }
            if (!Inside(v, b))
                problems.Add($"{ResultLabel}{when}: its scroll view [{v.xMin:F0},{v.yMin:F0},{v.xMax:F0},{v.yMax:F0}] is not inside its box");
            if (t.xMin < v.xMin - 0.5f || t.xMax > v.xMax + 0.5f)
                problems.Add($"{ResultLabel}{when}: its text [{t.xMin:F0},{t.xMax:F0}] is wider than its scroll view [{v.xMin:F0},{v.xMax:F0}]");
            float hidden = t.height - v.height;
            if (hidden > 1f && textView.verticalScroller.highValue < hidden - 1f)
                problems.Add($"{ResultLabel}{when}: {hidden:F0} of its text is below the view and it scrolls only {textView.verticalScroller.highValue:F0}");
        }

        /// <summary>The sample screens' header: <c>ios-dialog-header</c>, <c>ios-notif-header</c> and so on.</summary>
        private static bool IsHeader(VisualElement element) =>
            element.GetClasses().Any(name => name.EndsWith("-header", StringComparison.Ordinal));

        /// <summary>
        /// The screen's texts, labels and buttons (the result has its own checks): inside the ScrollView's viewport
        /// from side to side (up and down the scroll can bring them in), or inside the safe area from side to side
        /// outside a ScrollView; the text of one that does not wrap no wider than its box, and that of one that wraps
        /// no taller than its box at its width, or it is cut at the box's edges (the top menu on the iPhone XS and the
        /// 13 mini, 2026-11-22; implementation review of the same day).
        /// </summary>
        private static void CheckLabels(VisualElement root, Rect safePanel, List<string> problems)
        {
            foreach (TextElement label in root.Query<TextElement>().ToList())
            {
                if (label.name == ResultLabel || string.IsNullOrEmpty(label.text)) continue;
                if (label.resolvedStyle.display == DisplayStyle.None || label.resolvedStyle.visibility == Visibility.Hidden) continue;
                Rect r = label.worldBound;
                if (r.width <= 0f || r.height <= 0f)
                {
                    if (HiddenByAncestor(label)) continue;
                    problems.Add($"{LabelName(label)}: shown but has no size");
                    continue;
                }
                ScrollView? scroll = label.GetFirstAncestorOfType<ScrollView>();
                Rect side = scroll != null ? scroll.contentViewport.worldBound : safePanel;
                if (r.xMin < side.xMin - 0.5f || r.xMax > side.xMax + 0.5f)
                    problems.Add($"{LabelName(label)}: wider than the {(scroll != null ? "scroll view's viewport" : "safe area")} [{r.xMin:F0},{r.xMax:F0}] vs [{side.xMin:F0},{side.xMax:F0}]");
                Rect content = label.contentRect;
                if (label.resolvedStyle.whiteSpace == WhiteSpace.NoWrap)
                {
                    Vector2 text = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                    if (text.x > content.width + 1f)
                        problems.Add($"{LabelName(label)}: its text is {text.x:F0} wide in a box {content.width:F0} wide (it does not wrap)");
                }
                else
                {
                    Vector2 text = label.MeasureTextSize(label.text, content.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
                    if (text.y > content.height + 1f)
                        problems.Add($"{LabelName(label)}: its text is {text.y:F0} tall in a box {content.height:F0} tall at its width");
                }
            }
        }

        private static string LabelName(TextElement label) =>
            !string.IsNullOrEmpty(label.name) ? label.name
                : label.GetClasses().FirstOrDefault(c => !c.StartsWith("unity-")) ?? "a label";

        private static bool HiddenByAncestor(VisualElement element)
        {
            for (VisualElement? e = element.parent; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None) return true;
            return false;
        }

        private static bool Inside(Rect inner, Rect outer) =>
            inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f &&
            inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;

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
