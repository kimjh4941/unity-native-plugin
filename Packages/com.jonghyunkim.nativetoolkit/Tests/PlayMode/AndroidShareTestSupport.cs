#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using JonghyunKim.NativeToolkit.Runtime.Android.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// What the Android Share device tests share: the events a test saw, when the player came back
    /// from the share sheet, the icons they pass, and the steps that keep a teardown going. The test
    /// plan is artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v6.md.
    /// </summary>
    internal static class AndroidShareTestSupport
    {
        internal const float EventTimeoutSeconds = 10f;
        internal const float TearDownTimeoutSeconds = 5f;

        /// <summary>
        /// Whether the manager runs on native-toolkit's C ABI: its test seam is there. The fixtures check
        /// it first, so a seam that is gone or renamed fails them rather than passing in silence.
        /// </summary>
        internal static bool OnCApi => PendingCallCountProperty != null;

        private static PropertyInfo? PendingCallCountProperty => typeof(AndroidShareManager).GetProperty(
            "PendingCallCountForTests", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>The manager's calls still waiting for their completion.</summary>
        internal static int? PendingCallCount() => (int?)PendingCallCountProperty?.GetValue(null);

        /// <summary>A 64x64 PNG, as Base64: an icon every Android version can read.</summary>
        internal static string IconBase64()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(30, 120, 200, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            string base64 = Convert.ToBase64String(texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            return base64;
        }

        /// <summary>The PNG signature and nothing an image reader can use (AS-46).</summary>
        internal static string BrokenPngBase64() => Convert.ToBase64String(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 });

        /// <summary>The player's own name, as the share sheet lists it (V-12).</summary>
        internal static string AppLabel()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
            using var info = activity.Call<AndroidJavaObject>("getApplicationInfo");
            using var label = packageManager.Call<AndroidJavaObject>("getApplicationLabel", info);
            return label.Call<string>("toString");
        }

        internal static string AppPackage() => Application.identifier;

        /// <summary>
        /// Runs a step, and the steps it yields, catching what any of them throws into
        /// <paramref name="errors"/>. The Test Framework flattens nested steps itself and, when one
        /// throws, stops without resuming or disposing the method that yielded it; walking the nesting
        /// here keeps the exception inside, so the caller goes on (as AndroidDialogPlayerTests does).
        /// </summary>
        internal static IEnumerator Safely(IEnumerator step, List<Exception> errors)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(step);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool moved;
                Exception? error = null;
                try
                {
                    moved = top.MoveNext();
                }
                catch (Exception e)
                {
                    error = e;
                    moved = false;
                }
                if (error != null)
                {
                    errors.Add(error);
                    yield break;
                }
                if (!moved)
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                    stack.Push(nested);
                else
                    yield return top.Current;
            }
        }
    }

    /// <summary>
    /// The manager's three events as a test saw them: operation results (<c>Op</c>), selected apps
    /// (<c>Selected</c>) and chooser actions (<c>Action</c>), each as a string, with the thread each
    /// was raised on. The teardown checks each kind against the test's expected count (plan 3).
    /// </summary>
    internal sealed class AndroidShareEvents
    {
        internal readonly List<string> Ops = new();
        internal readonly List<string> Selected = new();
        internal readonly List<string> Actions = new();
        private readonly List<int> _threads = new();
        private readonly int _mainThread = Thread.CurrentThread.ManagedThreadId;
        private bool _subscribed;

        /// <summary>When the last event arrived (Time.realtimeSinceStartup), or null.</summary>
        internal float? LastEventAt { get; private set; }

        internal void Subscribe()
        {
            if (_subscribed) return;
            var manager = AndroidShareManager.Instance;
            manager.ShareOperationCompleted += OnOperation;
            manager.ShareCallbackReceived += OnSelected;
            manager.ShareChooserActionTapped += OnAction;
            _subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (!_subscribed) return;
            var manager = AndroidShareManager.Instance;
            manager.ShareOperationCompleted -= OnOperation;
            manager.ShareCallbackReceived -= OnSelected;
            manager.ShareChooserActionTapped -= OnAction;
            _subscribed = false;
        }

        private void Note(List<string> list, string entry)
        {
            _threads.Add(Thread.CurrentThread.ManagedThreadId);
            list.Add(entry);
            LastEventAt = Time.realtimeSinceStartup;
        }

        private void OnOperation(AndroidShareOperationResult r) =>
            Note(Ops, $"Op|{r.Operation}|{r.IsSuccess}|{r.ErrorMessage ?? "null"}");

        private void OnSelected(AndroidShareCallbackResult r) => Note(Selected, $"Selected|{r.SelectedPackageName ?? "null"}");

        private void OnAction(AndroidShareChooserActionResult r) => Note(Actions, $"Action|{r.ActionId}");

        internal bool Reached(int ops, int selected, int actions) =>
            Ops.Count >= ops && Selected.Count >= selected && Actions.Count >= actions;

        internal string? Mismatch(int ops, int selected, int actions) =>
            Ops.Count == ops && Selected.Count == selected && Actions.Count == actions
                ? null
                : $"expected {ops} / {selected} / {actions} (Op / Selected / Action), saw {Ops.Count} / {Selected.Count} / {Actions.Count}: {Describe()}";

        internal string? OffMainThread() => _threads.All(id => id == _mainThread)
            ? null
            : $"an event was raised off Unity's main thread (threads {string.Join(", ", _threads)}, main {_mainThread})";

        internal string Describe() => string.Join(" | ", Ops.Concat(Selected).Concat(Actions));
    }

    /// <summary>
    /// Notes when the player is paused and resumed. The share sheet pauses Unity while it shows
    /// (V-11); a selection or chooser action broadcast may arrive in the frames after the player
    /// resumes, so a check that one does not come starts from the resume (plan 3).
    /// </summary>
    internal sealed class AndroidSharePauseRecorder : MonoBehaviour
    {
        internal float? ResumedAt { get; private set; }
        internal int Pauses { get; private set; }

        internal static AndroidSharePauseRecorder Create() =>
            new GameObject("NtkSharePauseRecorder").AddComponent<AndroidSharePauseRecorder>();

        private void OnApplicationPause(bool paused)
        {
            if (paused) Pauses++;
            else ResumedAt = Time.realtimeSinceStartup;
        }
    }
}
#endif
#endif
