#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Clipboard
{
    using System;
    using System.Collections.Generic;
    using JonghyunKim.NativeToolkit.Runtime.Android.Common;
    using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
    using System.Runtime.InteropServices;
    using AOT;
#endif

    /// <summary>
    /// Singleton manager for Android native clipboard operations.
    ///
    /// Read/HasClip/GetDescription are synchronous and return their result directly. Copy/Clear/
    /// StopObserving report their results through the <see cref="ClipboardOperationCompleted"/> event
    /// and an optional per-call callback. StartObserving reports no result at all (see its own
    /// documentation).
    /// </summary>
    /// <remarks>
    /// Calls native-toolkit's C ABI (libntk.so, <c>ntk_clipboard_*</c>) since 2.0.0; 1.x called a
    /// Kotlin bridge through AndroidJavaObject with JSON. The public API is unchanged. Behaviour that
    /// changed is listed in artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md,
    /// 5.3; in short:
    /// <list type="bullet">
    /// <item>Copy and Clear run on the calling thread, inside the call: the clipboard has changed when
    /// the method returns. Their results still arrive later, on Unity's main thread.</item>
    /// <item>Observing starts later, on the Android main thread, with no guarantee of when: a copy made
    /// before that (for example right after <see cref="StartObserving"/>) may not raise
    /// <see cref="ClipboardChanged"/>.</item>
    /// <item>The methods may be called from any thread (1.x refused other threads).
    /// <see cref="Instance"/> creates a GameObject the first time, which Unity allows on its main thread
    /// only: get it there first.</item>
    /// <item>A null payload or text list throws ArgumentNullException (1.x: NullReferenceException); a
    /// null string is passed as "" (1.x passed the string "null"). A string is cut at its first
    /// U+0000, and an unpaired surrogate becomes U+FFFD.</item>
    /// <item>Observing is shared by the whole process: native-toolkit's Kotlin API in the same app
    /// observes through the same observer, so stopping it here stops it there too.</item>
    /// <item>Several error messages are fixed texts; Android requires IL2CPP and ARM64 (or x86_64).</item>
    /// </list>
    /// Every operation result, failures found before the native call included, arrives on Unity's
    /// main thread, first through the shared event and then the per-call callback; for a call made on
    /// Unity's main thread, after the method has returned.
    /// </remarks>
    public class AndroidClipboardManager : MonoBehaviour
    {
        private const string LogTag = "AndroidClipboardManager";

        /// <summary>Native operation name for copying plain text.</summary>
        public const string OperationCopyPlainText = "copyPlainText";

        /// <summary>Native operation name for copying HTML text.</summary>
        public const string OperationCopyHtmlText = "copyHtmlText";

        /// <summary>Native operation name for copying a URI.</summary>
        public const string OperationCopyUri = "copyUri";

        /// <summary>Native operation name for copying multiple plain-text items.</summary>
        public const string OperationCopyMultipleText = "copyMultipleText";

        /// <summary>Native operation name for clearing the clipboard.</summary>
        public const string OperationClear = "clear";

        /// <summary>Native operation name for stopping clipboard change observation.</summary>
        public const string OperationStopObserving = "stopObserving";

        // C#-only names: these report no operation result, so they are used in messages and logs only.
        private const string OperationStartObserving = "startObserving";
        private const string OperationRead = "read";
        private const string OperationHasClip = "hasClip";
        private const string OperationGetDescription = "getDescription";

        private static AndroidClipboardManager? _instance;

        // Whether Unity started observing (design J-C10). The change callback drops changes while it
        // is false: observing is shared by the process, and 1.x promised no change before
        // StartObserving. Read on the Android main thread without a lock.
        private static volatile bool s_observing;

        // Counts the stops. A change carries the count of when it was queued, and is dropped when a
        // stop came after it, even if observing started again before it was delivered.
        private static volatile int s_generation;

        // Serializes the starts and stops: the flag, the count and the native start or stop change
        // together, so calls from two threads end in the state of the last one. The native start and
        // stop only post to the Android main thread and never wait, so they are called inside it. The
        // change callback never takes it.
        private static readonly object s_observeLock = new object();

        // The change listener is registered once per process and never removed (J-C9). The lock is
        // for the registration only; the change callback never takes it.
        private static readonly object s_listenerLock = new object();
        private static bool s_listenerAdded;

        // Test seams for the registration, the native start and the native stop (design 7.2). Null:
        // the library.
        private static Func<int>? s_addListenerForTests;
        private static Func<int>? s_startObservingForTests;
        private static Func<int>? s_stopObservingForTests;

        /// <summary>
        /// Raised when a copy, clear, or stopObserving operation completes, on both success and
        /// failure. Always invoked before the per-call callback.
        /// </summary>
        public event Action<ClipboardOperationResult>? ClipboardOperationCompleted;

        /// <summary>
        /// Raised when the clipboard content changes while observing. Never raised before
        /// <see cref="StartObserving"/> is called.
        /// </summary>
        public event Action? ClipboardChanged;

        /// <summary>
        /// Singleton instance. Creates a new <see cref="AndroidClipboardManager"/> GameObject if
        /// none exists and ensures it persists across scene loads. The first call must be made on
        /// Unity's main thread.
        /// </summary>
        public static AndroidClipboardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance");
                    GameObject singletonObject = new GameObject("AndroidClipboardManager");
                    _instance = singletonObject.AddComponent<AndroidClipboardManager>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Resets the static state when the app or the Editor's play mode starts. With the Editor's
        /// domain reload turned off, statics would otherwise keep the last play session's values.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnStart() => ResetStaticStateForTests();

        private void Awake()
        {
            Debug.Log($"[{LogTag}][{nameof(Awake)}]");
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Created here, on the main thread: a native callback must never create Unity objects.
            AndroidNativeCallbackQueue.EnsureCreated();
            Initialize();
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            // A second manager destroyed in its Awake must not stop the live one's observing (J-C12).
            if (_instance != this) return;

            try
            {
                StopIfObserving();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{LogTag}][{nameof(OnDestroy)}] stopping the observing failed: {e.Message}");
            }
            _instance = null;
        }

        /// <summary>
        /// Registers the clipboard change listener with the native library, once per process. Called
        /// from <c>Awake</c>; calling it again is harmless. No-op on non-Android platforms.
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.Log($"[{LogTag}] Not running on Android. Skipping initialization.");
                return;
            }
            if (!NativeAvailable(nameof(Initialize))) return;

            EnsureChangeListener();
        }

        // ---- Async: copy / clear / stopObserving ----

        /// <summary>
        /// Copies plain text to the clipboard. A blank <see cref="CopyPlainTextPayload.text"/> is
        /// accepted and succeeds; it is not treated as an error.
        /// </summary>
        /// <param name="payload">Plain text content and optional metadata to copy.</param>
        /// <param name="onResult">Optional per-call callback invoked with the result. The global
        /// <see cref="ClipboardOperationCompleted"/> event always fires first, regardless of this
        /// parameter.</param>
        /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
        public void CopyPlainText(CopyPlainTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            AndroidNativeCallbackQueue.EnsureCreated();
            // Log lengths and flags only: clipboard content may hold passwords or tokens.
            // This intentionally deviates from the "log every parameter" rule in csharp.md.
            Debug.Log($"[{LogTag}][{nameof(CopyPlainText)}] textLength: {payload.text?.Length ?? 0}, " +
                      $"hasLabel: {!string.IsNullOrWhiteSpace(payload.label)}, isSensitive: {payload.isSensitive}, " +
                      $"hasCallback: {onResult != null}");
            if (!CanCall(OperationCopyPlainText, onResult)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Invoke(OperationCopyPlainText, onResult, null, allocated =>
            {
                IntPtr text = AndroidNativeToolkitCApi.AllocUtf8(AndroidClipboardCApi.AsPassed(payload.text), allocated);
                var options = AndroidClipboardCApi.BuildOptions(payload.label, payload.isSensitive, allocated);
                return AndroidClipboardCApi.ntk_clipboard_copy_text(text, ref options);
            });
#endif
        }

        /// <summary>
        /// Copies HTML text to the clipboard. A blank <see cref="CopyHtmlTextPayload.htmlText"/>
        /// fails with "Clipboard content is empty. Please provide text or HTML."
        /// </summary>
        /// <param name="payload">HTML text content and optional metadata to copy.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
        public void CopyHtmlText(CopyHtmlTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CopyHtmlText)}] plainTextLength: {payload.plainText?.Length ?? 0}, " +
                      $"htmlTextLength: {payload.htmlText?.Length ?? 0}, hasLabel: {!string.IsNullOrWhiteSpace(payload.label)}, " +
                      $"isSensitive: {payload.isSensitive}, hasCallback: {onResult != null}");
            if (!CanCall(OperationCopyHtmlText, onResult)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Invoke(OperationCopyHtmlText, onResult, null, allocated =>
            {
                IntPtr html = AndroidNativeToolkitCApi.AllocUtf8(AndroidClipboardCApi.AsPassed(payload.htmlText), allocated);
                IntPtr plain = AndroidNativeToolkitCApi.AllocUtf8(AndroidClipboardCApi.AsPassed(payload.plainText), allocated);
                var options = AndroidClipboardCApi.BuildOptions(payload.label, payload.isSensitive, allocated);
                return AndroidClipboardCApi.ntk_clipboard_copy_html(html, plain, ref options);
            });
#endif
        }

        /// <summary>
        /// Copies a URI (content:// or file:// scheme, including image/file references) to the
        /// clipboard. A blank URI or another scheme fails with "Invalid URI: &lt;uri&gt;".
        /// </summary>
        /// <param name="payload">URI content and optional metadata to copy.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
        public void CopyUri(CopyUriPayload payload, Action<ClipboardOperationResult>? onResult = null)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CopyUri)}] hasUri: {!string.IsNullOrEmpty(payload.uri)}, " +
                      $"hasLabel: {!string.IsNullOrWhiteSpace(payload.label)}, isSensitive: {payload.isSensitive}, " +
                      $"hasCallback: {onResult != null}");
            if (!CanCall(OperationCopyUri, onResult)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Invoke(OperationCopyUri, onResult, payload.uri, allocated =>
            {
                IntPtr uri = AndroidNativeToolkitCApi.AllocUtf8(AndroidClipboardCApi.AsPassed(payload.uri), allocated);
                var options = AndroidClipboardCApi.BuildOptions(payload.label, payload.isSensitive, allocated);
                return AndroidClipboardCApi.ntk_clipboard_copy_uri(uri, ref options);
            });
#endif
        }

        /// <summary>
        /// Copies multiple plain-text items (same form) to the clipboard. Individual empty strings
        /// inside <see cref="CopyMultipleTextPayload.texts"/> are accepted; only an empty array fails.
        /// </summary>
        /// <param name="payload">Text items and optional metadata to copy.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException"><paramref name="payload"/> or its texts is null.</exception>
        public void CopyMultipleText(CopyMultipleTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.texts == null) throw new ArgumentNullException(nameof(payload), "payload.texts is null.");
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CopyMultipleText)}] itemCount: {payload.texts.Length}, " +
                      $"hasLabel: {!string.IsNullOrWhiteSpace(payload.label)}, isSensitive: {payload.isSensitive}, " +
                      $"hasCallback: {onResult != null}");
            if (!CanCall(OperationCopyMultipleText, onResult)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Invoke(OperationCopyMultipleText, onResult, null, allocated =>
            {
                // Inside Invoke's try: a failed allocation is reported as a result (design 5.3).
                string[] texts = AndroidClipboardCApi.AsPassed(payload.texts);
                IntPtr array = AndroidNativeToolkitCApi.AllocStringArray(texts, allocated);
                var options = AndroidClipboardCApi.BuildOptions(payload.label, payload.isSensitive, allocated);
                return AndroidClipboardCApi.ntk_clipboard_copy_texts(array, new UIntPtr((uint)texts.Length), ref options);
            });
#endif
        }

        /// <summary>
        /// Clears the clipboard.
        /// </summary>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void Clear(Action<ClipboardOperationResult>? onResult = null)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(Clear)}] hasCallback: {onResult != null}");
            if (!CanCall(OperationClear, onResult)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Invoke(OperationClear, onResult, null, _ => AndroidClipboardCApi.ntk_clipboard_clear());
#endif
        }

        /// <summary>
        /// Stops observing clipboard changes. Success means the stop was handed to the Android main
        /// thread; no change is raised after this call either way, including one already on its way
        /// to Unity's main thread. Called from another thread, a change already being raised on Unity's
        /// main thread may still reach its subscribers after this returns. Observing is shared by the
        /// process: this stops it for
        /// native-toolkit's Kotlin API in the same app too, even when Unity did not start it.
        /// </summary>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void StopObserving(Action<ClipboardOperationResult>? onResult = null)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(StopObserving)}] hasCallback: {onResult != null}");
            LowerObserving();
            if (!CanCall(OperationStopObserving, onResult)) return;
            Invoke(OperationStopObserving, onResult, null, _ =>
            {
                lock (s_observeLock)
                {
                    LowerObserving();
                    return StopNative();
                }
            });
        }

        // ---- Sync: read / hasClip / getDescription ----

        /// <summary>
        /// Reads the current clipboard content. Synchronous: does not raise
        /// <see cref="ClipboardOperationCompleted"/>. May be called from any thread. Android lets an
        /// app read the clipboard only while it has the input focus; without it, the clipboard reads
        /// as <see cref="ClipboardReadStatus.Empty"/>.
        /// </summary>
        /// <returns>The clipboard content, an empty result, or a failure with an error code.</returns>
        public ClipboardReadResult Read()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(Read)}]");
            if (!CanRead(OperationRead))
                return ClipboardReadResult.Failed(AndroidClipboardCApi.BridgeUnavailableErrorCode, AndroidClipboardCApi.NotStartedMessage(OperationRead));
#if UNITY_ANDROID && !UNITY_EDITOR
            ClipboardReadResult result;
            try
            {
                int code = AndroidClipboardCApi.ntk_clipboard_read(out IntPtr content);
                if (code != AndroidClipboardCApi.ErrorNone)
                {
                    LogFailure(OperationRead, code);
                    var (errorCode, message) = AndroidClipboardCApi.ReadFailure(OperationRead, code);
                    result = ClipboardReadResult.Failed(errorCode, message);
                }
                else if (content == IntPtr.Zero)
                {
                    result = ClipboardReadResult.Empty();
                }
                else
                {
                    result = ClipboardReadResult.FromContents(
                        ReadAndFree(content, AndroidClipboardCApi.ReadContent, handle => AndroidClipboardCApi.ntk_clipboard_content_free(handle)));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] {OperationRead} error: {ex.Message}");
                result = ClipboardReadResult.Failed(AndroidClipboardCApi.BridgeUnavailableErrorCode, ex.Message);
            }
            Debug.Log($"[{LogTag}][{nameof(Read)}] status: {result.Status}, errorCode: {result.ErrorCode}");
            return result;
#else
            return ClipboardReadResult.Failed(AndroidClipboardCApi.BridgeUnavailableErrorCode, AndroidClipboardCApi.NotStartedMessage(OperationRead));
#endif
        }

        /// <summary>
        /// Returns whether the clipboard currently holds data. Synchronous: does not raise
        /// <see cref="ClipboardOperationCompleted"/>. May be called from any thread.
        /// </summary>
        /// <returns><c>true</c> if the clipboard holds data. Returns <c>false</c> if the check could
        /// not be performed; this is indistinguishable from an empty clipboard.</returns>
        public bool HasClip()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(HasClip)}]");
            if (!CanRead(OperationHasClip))
            {
                Debug.LogWarning($"[{LogTag}] {OperationHasClip} could not be started.");
                return false;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                int code = AndroidClipboardCApi.ntk_clipboard_has_clip(out int hasClip);
                if (code == AndroidClipboardCApi.ErrorNone) return hasClip != 0;
                LogFailure(OperationHasClip, code);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{LogTag}] {OperationHasClip} error: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Reads clipboard metadata without touching the clip body. Synchronous: does not raise
        /// <see cref="ClipboardOperationCompleted"/>. May be called from any thread.
        /// </summary>
        /// <returns>The clipboard metadata, an empty result, or a failure with an error code.</returns>
        public ClipboardDescriptionResult GetDescription()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(GetDescription)}]");
            if (!CanRead(OperationGetDescription))
                return ClipboardDescriptionResult.Failed(AndroidClipboardCApi.BridgeUnavailableErrorCode, AndroidClipboardCApi.NotStartedMessage(OperationGetDescription));
#if UNITY_ANDROID && !UNITY_EDITOR
            ClipboardDescriptionResult result;
            try
            {
                int code = AndroidClipboardCApi.ntk_clipboard_get_description(out IntPtr description);
                if (code != AndroidClipboardCApi.ErrorNone)
                {
                    LogFailure(OperationGetDescription, code);
                    var (errorCode, message) = AndroidClipboardCApi.ReadFailure(OperationGetDescription, code);
                    result = ClipboardDescriptionResult.Failed(errorCode, message);
                }
                else if (description == IntPtr.Zero)
                {
                    result = ClipboardDescriptionResult.Empty();
                }
                else
                {
                    result = ClipboardDescriptionResult.FromDescription(
                        ReadAndFree(description, AndroidClipboardCApi.ReadDescription, handle => AndroidClipboardCApi.ntk_clipboard_description_free(handle)));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] {OperationGetDescription} error: {ex.Message}");
                result = ClipboardDescriptionResult.Failed(AndroidClipboardCApi.BridgeUnavailableErrorCode, ex.Message);
            }
            Debug.Log($"[{LogTag}][{nameof(GetDescription)}] status: {result.Status}, errorCode: {result.ErrorCode}");
            return result;
#else
            return ClipboardDescriptionResult.Failed(AndroidClipboardCApi.BridgeUnavailableErrorCode, AndroidClipboardCApi.NotStartedMessage(OperationGetDescription));
#endif
        }

        // ---- Observation ----

        /// <summary>
        /// Starts observing clipboard changes. Reports no result. Changes are delivered through
        /// <see cref="ClipboardChanged"/>. A second call while already observing is a no-op on the
        /// native side. Observing starts later, on the Android main thread, with no guarantee of
        /// when: a change made before that, such as a copy right after this call, may not be
        /// reported. Android reports changes only while the app has the input focus. If the native
        /// layer cannot obtain the system ClipboardManager, observation silently does not start;
        /// this cannot be detected from C#.
        /// </summary>
        public void StartObserving()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(StartObserving)}]");

            // startObserving reports no result, so failures are logged and swallowed here. Nothing
            // is raised on ClipboardOperationCompleted or ClipboardChanged.
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning($"[{LogTag}] {OperationStartObserving} can only be called on Android.");
                return;
            }
            if (!NativeAvailable(OperationStartObserving)) return;

            StartObservingCore();
        }

        /// <summary>
        /// The start once the library is known to be usable: registers the change listener if it is
        /// not yet (a registration that failed in <see cref="Initialize"/> is tried again here),
        /// raises the observing flag, and asks the library to observe. When the library does not
        /// accept it, the flag goes back to what it was (J-C10, J-C11).
        /// </summary>
        internal static void StartObservingCore()
        {
            Debug.Log($"[{LogTag}][{nameof(StartObservingCore)}]");
            if (!EnsureChangeListener()) return;

            int code;
            lock (s_observeLock)
            {
                // A second StartObserving that fails must not stop the changes of the first.
                bool wasObserving = s_observing;
                s_observing = true;
                try
                {
                    code = StartNative();
                }
                catch (Exception ex)
                {
                    s_observing = wasObserving;
                    Debug.LogError($"[{LogTag}] {OperationStartObserving} error: {ex.Message}");
                    return;
                }
                if (code != AndroidClipboardCApi.ErrorNone) s_observing = wasObserving;
            }
            if (code != AndroidClipboardCApi.ErrorNone) LogFailure(OperationStartObserving, code);
        }

        // ---- Shared call machinery ----

        /// <summary>
        /// Whether an asynchronous operation can reach the library. When not, posts 1.x's
        /// "&lt;op&gt; could not be started." failure (design 4.4, J-C6).
        /// </summary>
        private static bool CanCall(string operation, Action<ClipboardOperationResult>? onResult)
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning($"[{LogTag}] {operation} can only be called on Android.");
                Post(ClipboardOperationResult.Failure(operation, AndroidClipboardCApi.NotStartedMessage(operation)), onResult);
                return false;
            }
            if (!NativeAvailable(operation))
            {
                Post(ClipboardOperationResult.Failure(operation, AndroidClipboardCApi.NotStartedMessage(operation)), onResult);
                return false;
            }
            return true;
        }

        /// <summary>Whether a synchronous read can reach the library; logs why not.</summary>
        private static bool CanRead(string operation)
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning($"[{LogTag}] {operation} can only be called on Android.");
                return false;
            }
            return NativeAvailable(operation);
        }

        private static bool NativeAvailable(string operation)
        {
            if (AndroidNativeToolkitCApi.EnsureNativeAvailable() == AndroidNativeToolkitCApi.NativeState.Available) return true;
            Debug.LogWarning($"[{LogTag}][{operation}] the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).");
            return false;
        }

        /// <summary>
        /// Runs one synchronous C call, frees what it allocated, and posts its result with the call's
        /// own callback (J-C1, J-C2).
        /// </summary>
        private static void Invoke(string operation, Action<ClipboardOperationResult>? onResult, string? uri, Func<List<IntPtr>, int> call)
        {
            List<IntPtr>? allocated = null;
            ClipboardOperationResult result;
            try
            {
                allocated = new List<IntPtr>();
                int code = call(allocated);
                if (code == AndroidClipboardCApi.ErrorNone)
                {
                    result = ClipboardOperationResult.Success(operation);
                }
                else
                {
                    LogFailure(operation, code);
                    result = ClipboardOperationResult.Failure(operation, AndroidClipboardCApi.OperationFailureMessage(operation, code, uri));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] {operation} error: {ex.Message}");
                result = ClipboardOperationResult.Failure(operation, ex.Message);
            }
            finally
            {
                if (allocated != null) AndroidNativeToolkitCApi.FreeAll(allocated);
            }
            Post(result, onResult);
        }

        /// <summary>
        /// Reads a handle the library returned and frees it exactly once, whether the read succeeds or
        /// throws (the caller owns the handle, <c>Clipboard.h</c>).
        /// </summary>
        internal static T ReadAndFree<T>(IntPtr handle, Func<IntPtr, T> read, Action<IntPtr> free)
        {
            // No entry log: Read and GetDescription log the call and its outcome.
            try
            {
                return read(handle);
            }
            finally
            {
                free(handle);
            }
        }

        /// <summary>
        /// Logs a failure by its error value only: messages may hold the caller's URI (J-C15).
        /// </summary>
        private static void LogFailure(string operation, int code)
        {
            if (AndroidClipboardCApi.IsRequestDefect(code))
                Debug.LogError($"[{LogTag}][{operation}] the request was rejected: {AndroidClipboardCApi.ErrorName(code)}");
            else if (code == AndroidClipboardCApi.ErrorNotInitialized)
                Debug.LogWarning($"[{LogTag}][{operation}] the native library is not initialized yet");
            else
                Debug.LogWarning($"[{LogTag}][{operation}] failed: {AndroidClipboardCApi.ErrorName(code)}");
        }

        private static void Post(ClipboardOperationResult result, Action<ClipboardOperationResult>? onResult) =>
            AndroidNativeCallbackQueue.Post(() => Deliver(result, onResult));

        /// <summary>
        /// Raises a result on Unity's main thread: the shared event, then the per-call callback. Goes
        /// to the manager alive when the queue runs; without one, the result is dropped (J-C3).
        /// </summary>
        private static void Deliver(ClipboardOperationResult result, Action<ClipboardOperationResult>? onResult)
        {
            AndroidClipboardManager? manager = _instance;
            if (manager == null)
            {
                Debug.Log($"[{LogTag}][{nameof(Deliver)}] no manager; dropping the result of {result.Operation}");
                return;
            }
            InvokeInOrder(result, manager.ClipboardOperationCompleted, onResult);
        }

        /// <summary>
        /// Invokes the common event followed by the per-call callback, swallowing any exception
        /// raised by either. Extracted as a pure, Unity-lifecycle-independent helper so dispatch
        /// order and exception handling can be verified directly from EditMode tests.
        /// </summary>
        /// <param name="result">Result to dispatch.</param>
        /// <param name="common">Common event snapshot (invoked first).</param>
        /// <param name="perCall">Per-call callback snapshot (invoked second).</param>
        internal static void InvokeInOrder(
            ClipboardOperationResult result,
            Action<ClipboardOperationResult>? common,
            Action<ClipboardOperationResult>? perCall)
        {
            // Separate try/catch blocks: an exception from `common` must not prevent `perCall`
            // from being invoked, and vice versa.
            try
            {
                common?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] {ex.Message}");
            }

            try
            {
                perCall?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] {ex.Message}");
            }
        }

        // ---- The change listener and the observing flag (J-C9, J-C10, J-C12) ----

        /// <summary>
        /// Registers the change listener if it is not registered yet. A failure is logged and tried
        /// again on the next call. Returns whether the listener is registered.
        /// </summary>
        internal static bool EnsureChangeListener()
        {
            Debug.Log($"[{LogTag}][{nameof(EnsureChangeListener)}]");
            try
            {
                lock (s_listenerLock)
                {
                    if (s_listenerAdded) return true;
                    int code = AddListenerNative();
                    s_listenerAdded = code == AndroidClipboardCApi.ErrorNone;
                    if (!s_listenerAdded)
                        Debug.LogWarning($"[{LogTag}][{nameof(EnsureChangeListener)}] the change listener was not added: {AndroidClipboardCApi.ErrorName(code)}");
                    return s_listenerAdded;
                }
            }
            catch (Exception e)
            {
                // Not expected, so an error; it too is tried again on the next call.
                Debug.LogError($"[{LogTag}][{nameof(EnsureChangeListener)}] {e}");
                return false;
            }
        }

        /// <summary>
        /// Lowers the observing flag and, when Unity had started observing, stops the native
        /// observing (1.x stopped it when the manager was destroyed). Called from <c>OnDestroy</c>.
        /// </summary>
        internal static void StopIfObserving()
        {
            Debug.Log($"[{LogTag}][{nameof(StopIfObserving)}] observing: {s_observing}");
            int code;
            lock (s_observeLock)
            {
                if (!s_observing) return;
                LowerObserving();
                if (s_stopObservingForTests == null
                    && AndroidNativeToolkitCApi.EnsureNativeAvailable() != AndroidNativeToolkitCApi.NativeState.Available)
                    return;

                code = StopNative();
            }
            if (code != AndroidClipboardCApi.ErrorNone)
                Debug.LogWarning($"[{LogTag}][{nameof(StopIfObserving)}] failed: {AndroidClipboardCApi.ErrorName(code)}");
        }

        /// <summary>Lowers the flag and counts the stop. Takes the lock itself (it is reentrant).</summary>
        private static void LowerObserving()
        {
            lock (s_observeLock)
            {
                s_observing = false;
                s_generation++;
            }
        }

        private static int AddListenerNative()
        {
            if (s_addListenerForTests != null) return s_addListenerForTests();
#if UNITY_ANDROID && !UNITY_EDITOR
            return AndroidClipboardCApi.ntk_clipboard_add_change_listener(s_changePointer, IntPtr.Zero, IntPtr.Zero, out IntPtr _);
#else
            return AndroidClipboardCApi.ErrorNone;
#endif
        }

        private static int StartNative()
        {
            if (s_startObservingForTests != null) return s_startObservingForTests();
#if UNITY_ANDROID && !UNITY_EDITOR
            return AndroidClipboardCApi.ntk_clipboard_start_observing();
#else
            return AndroidClipboardCApi.ErrorNone;
#endif
        }

        private static int StopNative()
        {
            if (s_stopObservingForTests != null) return s_stopObservingForTests();
#if UNITY_ANDROID && !UNITY_EDITOR
            return AndroidClipboardCApi.ntk_clipboard_stop_observing();
#else
            return AndroidClipboardCApi.ErrorNone;
#endif
        }

        /// <summary>
        /// The change callback's body, on the Android main thread: drops the change unless Unity is
        /// observing, and otherwise queues it for Unity's main thread. No entry log: it runs on every
        /// clipboard change, and twice for a text copy.
        /// </summary>
        internal static void OnChangedCore()
        {
            // The count first: a stop between the two reads leaves an old count, so the change is
            // dropped when it is delivered.
            int generation = s_generation;
            if (!s_observing) return;
            AndroidNativeCallbackQueue.Post(() => RaiseChanged(generation));
        }

        private static void RaiseChanged(int generation)
        {
            // Looked at again here: a change queued before a stop is not raised after it, even when
            // observing has started again since.
            if (!s_observing || generation != s_generation) return;
            AndroidClipboardManager? manager = _instance;
            if (manager == null)
            {
                Debug.Log($"[{LogTag}][{nameof(RaiseChanged)}] no manager; dropping a clipboard change");
                return;
            }
            try
            {
                manager.ClipboardChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(RaiseChanged)}] {ex.Message}");
            }
        }

        // ---- Test seams (design 7.2) ----

        internal static bool IsObservingForTests => s_observing;

        internal static void SetObservingForTests(bool observing) => s_observing = observing;

        internal static bool ListenerRegisteredForTests
        {
            get
            {
                lock (s_listenerLock) return s_listenerAdded;
            }
        }

        /// <summary>
        /// Replaces the registration, the native start and the native stop with test functions; null
        /// restores them (a call with two arguments restores the start).
        /// </summary>
        internal static void SetNativeForTests(Func<int>? addListener, Func<int>? stopObserving, Func<int>? startObserving = null)
        {
            s_addListenerForTests = addListener;
            s_stopObservingForTests = stopObserving;
            s_startObservingForTests = startObserving;
        }

        /// <summary>
        /// Empties the static state, as at the start of the app (SubsystemRegistration). Not for the
        /// device tests: it forgets the registered listener, so the next call would register a second.
        /// </summary>
        internal static void ResetStaticStateForTests()
        {
            lock (s_observeLock)
            {
                s_observing = false;
                s_generation = 0;
            }
            s_addListenerForTests = null;
            s_startObservingForTests = null;
            s_stopObservingForTests = null;
            lock (s_listenerLock) s_listenerAdded = false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // The change callback, kept alive for the process and handed to the library as a pointer.
        private static readonly AndroidClipboardCApi.ChangeCallback s_change = OnChanged;
        private static readonly IntPtr s_changePointer = Marshal.GetFunctionPointerForDelegate(s_change);

        // On the Android main thread. No exception may reach the library, where it would end the process.
        [MonoPInvokeCallback(typeof(AndroidClipboardCApi.ChangeCallback))]
        private static void OnChanged(IntPtr userData)
        {
            try
            {
                OnChangedCore();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnChanged)}] {e}");
            }
        }
#endif
    }
}
#endif
