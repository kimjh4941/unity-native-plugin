#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Dialog
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;
    using JonghyunKim.NativeToolkit.Runtime.Android.Common;
#if UNITY_ANDROID && !UNITY_EDITOR
    using System.Runtime.InteropServices;
    using AOT;
#endif

    /// <summary>
    /// Singleton manager for Android native dialogs: basic alerts, confirmations, single and multiple
    /// choice lists, text input and login. Results arrive through one event per dialog kind.
    /// </summary>
    /// <remarks>
    /// Calls native-toolkit's C ABI (libntk.so, <c>ntk_dialog_*</c>) since 2.0.0; 1.x called a Kotlin
    /// bridge through AndroidJavaObject. The public API is unchanged. Behaviour that changed is listed
    /// in artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md, 5.3; in short:
    /// <list type="bullet">
    /// <item>A dialog closed by Back or an outside tap no longer reports the selection of a single or
    /// multiple choice dialog: checkedItem / checkedItems are null.</item>
    /// <item>A checkedItem outside -1 to the last index, and a null item, fail at once; checkedItems of
    /// another length than the items is padded with false or cut.</item>
    /// <item>Error messages are fixed texts ("Failed to showDialog: the app is not in the
    /// foreground." and so on), and a dialog closed by the system reports a failure.</item>
    /// <item>Android requires IL2CPP and ARM64 (or x86_64): libntk.so is not built for 32-bit ABIs.</item>
    /// <item>Failures returned as results are logged as warnings, and inputs, user names and
    /// passwords are logged by length only.</item>
    /// </list>
    /// Call the public methods from Unity's main thread. A failure found before the native call is
    /// reported through the event before the method returns; a result of the dialog arrives on a later
    /// frame, on Unity's main thread. A call from another thread also works, as in 1.x: such a failure
    /// is then reported on the calling thread.
    /// <para>
    /// A dialog closed by a button reports (label, …, true, null). A dialog closed by Back or an
    /// outside tap reports ("Cancel", …, true, "") as in 1.x: successful, with an empty errorMessage
    /// rather than null. A failure reports (null, …, false, message).
    /// </para>
    /// </remarks>
    [MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Dialog")]
    public class AndroidDialogManager : MonoBehaviour
    {
        private const string LogTag = nameof(AndroidDialogManager);

        private static AndroidDialogManager? _instance;

        /// <summary>
        /// Singleton instance property for AndroidDialogManager.
        /// Creates a new instance if none exists and ensures it persists across scene loads.
        /// </summary>
        public static AndroidDialogManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}][{nameof(Instance)}] creating the instance");
                    GameObject singletonObject = new GameObject("AndroidDialogManager");
                    _instance = singletonObject.AddComponent<AndroidDialogManager>();
                    DontDestroyOnLoad(singletonObject);
                }
                return _instance;
            }
        }

        /// <summary>Result of <see cref="ShowDialog"/>: (buttonText, isSuccessful, errorMessage).</summary>
        public event Action<string?, bool, string?>? DialogResult;

        /// <summary>Result of <see cref="ShowConfirmDialog"/>: (buttonText, isSuccessful, errorMessage).</summary>
        public event Action<string?, bool, string?>? ConfirmDialogResult;

        /// <summary>Result of <see cref="ShowSingleChoiceItemDialog"/>: (buttonText, checkedItem, isSuccessful, errorMessage).</summary>
        public event Action<string?, int?, bool, string?>? SingleChoiceItemDialogResult;

        /// <summary>Result of <see cref="ShowMultiChoiceItemDialog"/>: (buttonText, checkedItems, isSuccessful, errorMessage).</summary>
        public event Action<string?, bool[]?, bool, string?>? MultiChoiceItemDialogResult;

        /// <summary>Result of <see cref="ShowTextInputDialog"/>: (buttonText, inputText, isSuccessful, errorMessage).</summary>
        public event Action<string?, string?, bool, string?>? TextInputDialogResult;

        /// <summary>Result of <see cref="ShowLoginDialog"/>: (buttonText, username, password, isSuccessful, errorMessage).</summary>
        public event Action<string?, string?, string?, bool, string?>? LoginDialogResult;

        /// <summary>
        /// Initialize the singleton instance and the object that carries native callbacks to the main thread.
        /// </summary>
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
            }
            // Created here, on the main thread: a native callback must never create Unity objects.
            AndroidNativeCallbackQueue.EnsureCreated();

            Initialize();
        }

        /// <summary>
        /// Reports whether the native library can be used. 2.0.0 needs no setup: the library is
        /// initialized at app start. Kept for compatibility; calling it again is harmless.
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.Log("Not running on an Android device. Skipping native plugin initialization.");
                return;
            }
            Debug.Log($"[{LogTag}][{nameof(Initialize)}] native: {AndroidNativeToolkitCApi.EnsureNativeAvailable()}, initialized: {AndroidNativeToolkitCApi.IsInitialized()}");
        }

        /// <summary>
        /// Shows a basic Android alert dialog with a single button.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Dialog message content</param>
        /// <param name="buttonText">Text for the single button (default: "OK")</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowDialog(string title, string message, string? buttonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowDialog)}] title: {title}, message: {message}, buttonText: {buttonText}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            Action<string> fail = error => DialogResult?.Invoke(null, false, error);
            if (Rejected(nameof(ShowDialog), AndroidDialogCApi.CheckTitleAndMessage(title, message, checkMessage: true), fail)) return;

            buttonText ??= "OK";

            if (!OnDevice(nameof(ShowDialog))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            ShowNative(AndroidDialogCApi.Kind.Alert, allocated =>
            {
                AndroidDialogCApi.AlertRequest request = AndroidDialogCApi.BuildAlert(title, message, buttonText, cancelableOnTouchOutside, cancelable, allocated);
                return AndroidDialogCApi.ntk_dialog_show_alert_async(ref request, s_callbackPointer, KindData(AndroidDialogCApi.Kind.Alert), IntPtr.Zero, IntPtr.Zero);
            }, fail);
#endif
        }

        /// <summary>
        /// Shows a confirmation dialog with Yes/No buttons.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Dialog message content</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "No")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "Yes")</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowConfirmDialog(string title, string message, string? negativeButtonText = "No", string? positiveButtonText = "Yes", bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowConfirmDialog)}] title: {title}, message: {message}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            Action<string> fail = error => ConfirmDialogResult?.Invoke(null, false, error);
            if (Rejected(nameof(ShowConfirmDialog), AndroidDialogCApi.CheckTitleAndMessage(title, message, checkMessage: true), fail)) return;

            negativeButtonText ??= "No";
            positiveButtonText ??= "Yes";

            if (!OnDevice(nameof(ShowConfirmDialog))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            ShowNative(AndroidDialogCApi.Kind.Confirm, allocated =>
            {
                AndroidDialogCApi.ConfirmRequest request = AndroidDialogCApi.BuildConfirm(title, message, negativeButtonText, positiveButtonText, cancelableOnTouchOutside, cancelable, allocated);
                return AndroidDialogCApi.ntk_dialog_show_confirm_async(ref request, s_callbackPointer, KindData(AndroidDialogCApi.Kind.Confirm), IntPtr.Zero, IntPtr.Zero);
            }, fail);
#endif
        }

        /// <summary>
        /// Shows a single selection dialog with radio button options.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="singleChoiceItems">Array of items to choose from; no item may be null</param>
        /// <param name="checkedItem">Index of the initially selected item, or -1 for none (default: 0)</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "Cancel")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "OK")</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowSingleChoiceItemDialog(string title, string[] singleChoiceItems, int checkedItem = 0, string? negativeButtonText = "Cancel", string? positiveButtonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            var singleChoiceItemsList = singleChoiceItems != null ? string.Join(", ", singleChoiceItems) : "null";
            Debug.Log($"[{LogTag}][{nameof(ShowSingleChoiceItemDialog)}] title: {title}, singleChoiceItems: {singleChoiceItemsList}, checkedItem: {checkedItem}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            Action<string> fail = error => SingleChoiceItemDialogResult?.Invoke(null, null, false, error);
            if (Rejected(nameof(ShowSingleChoiceItemDialog), AndroidDialogCApi.CheckTitleAndMessage(title, null, checkMessage: false), fail)) return;
            if (Rejected(nameof(ShowSingleChoiceItemDialog), AndroidDialogCApi.CheckItems(singleChoiceItems, nameof(singleChoiceItems)), fail)) return;
            if (Rejected(nameof(ShowSingleChoiceItemDialog), AndroidDialogCApi.IsCheckedItemInRange(checkedItem, singleChoiceItems!.Length) ? null : AndroidDialogCApi.CheckedItemOutOfRange, fail)) return;

            negativeButtonText ??= "Cancel";
            positiveButtonText ??= "OK";

            if (!OnDevice(nameof(ShowSingleChoiceItemDialog))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            ShowNative(AndroidDialogCApi.Kind.SingleChoice, allocated =>
            {
                AndroidDialogCApi.SingleChoiceRequest request = AndroidDialogCApi.BuildSingleChoice(title, singleChoiceItems, checkedItem, negativeButtonText, positiveButtonText, cancelableOnTouchOutside, cancelable, allocated);
                return AndroidDialogCApi.ntk_dialog_show_single_choice_async(ref request, s_callbackPointer, KindData(AndroidDialogCApi.Kind.SingleChoice), IntPtr.Zero, IntPtr.Zero);
            }, fail);
#endif
        }

        /// <summary>
        /// Shows a multi-selection dialog with checkbox options.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="multiChoiceItems">Array of items to choose from; no item may be null</param>
        /// <param name="checkedItems">Initial selection state for each item; padded with false or cut to the number of items</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "Cancel")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "OK")</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowMultiChoiceItemDialog(string title, string[] multiChoiceItems, bool[]? checkedItems = null, string? negativeButtonText = "Cancel", string? positiveButtonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            var multiChoiceItemsList = multiChoiceItems != null ? string.Join(", ", multiChoiceItems) : "null";
            var checkedItemsList = checkedItems != null ? string.Join(", ", checkedItems) : "null";
            Debug.Log($"[{LogTag}][{nameof(ShowMultiChoiceItemDialog)}] title: {title}, multiChoiceItems: {multiChoiceItemsList}, checkedItems: {checkedItemsList}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            Action<string> fail = error => MultiChoiceItemDialogResult?.Invoke(null, null, false, error);
            if (Rejected(nameof(ShowMultiChoiceItemDialog), AndroidDialogCApi.CheckTitleAndMessage(title, null, checkMessage: false), fail)) return;
            if (Rejected(nameof(ShowMultiChoiceItemDialog), AndroidDialogCApi.CheckItems(multiChoiceItems, nameof(multiChoiceItems)), fail)) return;

            negativeButtonText ??= "Cancel";
            positiveButtonText ??= "OK";

            if (!OnDevice(nameof(ShowMultiChoiceItemDialog))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            ShowNative(AndroidDialogCApi.Kind.MultiChoice, allocated =>
            {
                AndroidDialogCApi.MultiChoiceRequest request = AndroidDialogCApi.BuildMultiChoice(title, multiChoiceItems, checkedItems, negativeButtonText, positiveButtonText, cancelableOnTouchOutside, cancelable, allocated);
                return AndroidDialogCApi.ntk_dialog_show_multi_choice_async(ref request, s_callbackPointer, KindData(AndroidDialogCApi.Kind.MultiChoice), IntPtr.Zero, IntPtr.Zero);
            }, fail);
#endif
        }

        /// <summary>
        /// Shows a text input dialog with a single text field.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Dialog message content</param>
        /// <param name="hint">Placeholder text for the input field (default: "")</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "Cancel")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "OK")</param>
        /// <param name="enablePositiveButtonWhenEmpty">Whether positive button is enabled when input is empty (default: false)</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowTextInputDialog(string title, string message, string? hint = "", string? negativeButtonText = "Cancel", string? positiveButtonText = "OK", bool enablePositiveButtonWhenEmpty = false, bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowTextInputDialog)}] title: {title}, message: {message}, hint: {hint}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, enablePositiveButtonWhenEmpty: {enablePositiveButtonWhenEmpty}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            Action<string> fail = error => TextInputDialogResult?.Invoke(null, null, false, error);
            if (Rejected(nameof(ShowTextInputDialog), AndroidDialogCApi.CheckTitleAndMessage(title, message, checkMessage: true), fail)) return;

            hint ??= "";
            negativeButtonText ??= "Cancel";
            positiveButtonText ??= "OK";

            if (!OnDevice(nameof(ShowTextInputDialog))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            ShowNative(AndroidDialogCApi.Kind.TextInput, allocated =>
            {
                AndroidDialogCApi.TextInputRequest request = AndroidDialogCApi.BuildTextInput(title, message, hint, negativeButtonText, positiveButtonText, enablePositiveButtonWhenEmpty, cancelableOnTouchOutside, cancelable, allocated);
                return AndroidDialogCApi.ntk_dialog_show_text_input_async(ref request, s_callbackPointer, KindData(AndroidDialogCApi.Kind.TextInput), IntPtr.Zero, IntPtr.Zero);
            }, fail);
#endif
        }

        /// <summary>
        /// Shows a login dialog with username and password input fields.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Dialog message content</param>
        /// <param name="usernameHint">Placeholder text for the username field (default: "Username")</param>
        /// <param name="passwordHint">Placeholder text for the password field (default: "Password")</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "Cancel")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "Login")</param>
        /// <param name="enablePositiveButtonWhenEmpty">Whether positive button is enabled when inputs are empty (default: false)</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowLoginDialog(string title, string message, string? usernameHint = "Username", string? passwordHint = "Password", string? negativeButtonText = "Cancel", string? positiveButtonText = "Login", bool enablePositiveButtonWhenEmpty = false, bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowLoginDialog)}] title: {title}, message: {message}, usernameHint: {usernameHint}, passwordHint: {passwordHint}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, enablePositiveButtonWhenEmpty: {enablePositiveButtonWhenEmpty}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            Action<string> fail = error => LoginDialogResult?.Invoke(null, null, null, false, error);
            if (Rejected(nameof(ShowLoginDialog), AndroidDialogCApi.CheckTitleAndMessage(title, message, checkMessage: true), fail)) return;

            usernameHint ??= "Username";
            passwordHint ??= "Password";
            negativeButtonText ??= "Cancel";
            positiveButtonText ??= "Login";

            if (!OnDevice(nameof(ShowLoginDialog))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            ShowNative(AndroidDialogCApi.Kind.Login, allocated =>
            {
                AndroidDialogCApi.LoginRequest request = AndroidDialogCApi.BuildLogin(title, message, usernameHint, passwordHint, negativeButtonText, positiveButtonText, enablePositiveButtonWhenEmpty, cancelableOnTouchOutside, cancelable, allocated);
                return AndroidDialogCApi.ntk_dialog_show_login_async(ref request, s_callbackPointer, KindData(AndroidDialogCApi.Kind.Login), IntPtr.Zero, IntPtr.Zero);
            }, fail);
#endif
        }

        /// <summary>
        /// Raises the event for a completed dialog. Runs on Unity's main thread, from the callback queue.
        /// Internal: EditMode and PlayMode tests feed it answers directly.
        /// </summary>
        internal void Raise(AndroidDialogCApi.Answer answer)
        {
            Debug.Log($"[{LogTag}][{nameof(Raise)}] kind: {answer.Kind}, error: {AndroidDialogCApi.ErrorName(answer.Error)}, dismissed: {answer.IsDismissed}");
            if (answer.Error != AndroidDialogCApi.ErrorNone)
                Debug.LogWarning($"[{LogTag}][{nameof(Raise)}] {AndroidDialogCApi.CompletionFailureMessage(answer.Kind, answer.Error)}");

            switch (answer.Kind)
            {
                case AndroidDialogCApi.Kind.Alert:
                {
                    var (button, ok, error) = AndroidDialogCApi.ToButtonArgs(answer);
                    Debug.Log($"[{LogTag}][{nameof(Raise)}] DialogResult buttonText: {Show(button)}, isSuccessful: {ok}, errorMessage: {Show(error)}");
                    DialogResult?.Invoke(button, ok, error);
                    break;
                }
                case AndroidDialogCApi.Kind.Confirm:
                {
                    var (button, ok, error) = AndroidDialogCApi.ToButtonArgs(answer);
                    Debug.Log($"[{LogTag}][{nameof(Raise)}] ConfirmDialogResult buttonText: {Show(button)}, isSuccessful: {ok}, errorMessage: {Show(error)}");
                    ConfirmDialogResult?.Invoke(button, ok, error);
                    break;
                }
                case AndroidDialogCApi.Kind.SingleChoice:
                {
                    var (button, index, ok, error) = AndroidDialogCApi.ToSingleChoiceArgs(answer);
                    Debug.Log($"[{LogTag}][{nameof(Raise)}] SingleChoiceItemDialogResult buttonText: {Show(button)}, checkedItem: {index?.ToString() ?? "null"}, isSuccessful: {ok}, errorMessage: {Show(error)}");
                    SingleChoiceItemDialogResult?.Invoke(button, index, ok, error);
                    break;
                }
                case AndroidDialogCApi.Kind.MultiChoice:
                {
                    var (button, states, ok, error) = AndroidDialogCApi.ToMultiChoiceArgs(answer);
                    Debug.Log($"[{LogTag}][{nameof(Raise)}] MultiChoiceItemDialogResult buttonText: {Show(button)}, checkedItems: {(states != null ? string.Join(", ", states) : "null")}, isSuccessful: {ok}, errorMessage: {Show(error)}");
                    MultiChoiceItemDialogResult?.Invoke(button, states, ok, error);
                    break;
                }
                case AndroidDialogCApi.Kind.TextInput:
                {
                    var (button, text, ok, error) = AndroidDialogCApi.ToTextInputArgs(answer);
                    Debug.Log($"[{LogTag}][{nameof(Raise)}] TextInputDialogResult buttonText: {Show(button)}, inputText: {Length(text)}, isSuccessful: {ok}, errorMessage: {Show(error)}");
                    TextInputDialogResult?.Invoke(button, text, ok, error);
                    break;
                }
                case AndroidDialogCApi.Kind.Login:
                {
                    var (button, user, password, ok, error) = AndroidDialogCApi.ToLoginArgs(answer);
                    Debug.Log($"[{LogTag}][{nameof(Raise)}] LoginDialogResult buttonText: {Show(button)}, username: {Length(user)}, password: {Length(password)}, isSuccessful: {ok}, errorMessage: {Show(error)}");
                    LoginDialogResult?.Invoke(button, user, password, ok, error);
                    break;
                }
                default:
                    Debug.LogError($"[{LogTag}][{nameof(Raise)}] unknown kind: {answer.Kind}");
                    break;
            }
        }

        /// <summary>
        /// Queues the event for a completed dialog. It goes to the instance that exists when the queue
        /// runs, so a disabled manager still hears it and a destroyed one is replaced (as with 1.x's
        /// dispatcher). Called by the completion callback; internal so the Editor tests take the same path.
        /// </summary>
        internal static void PostAnswer(AndroidDialogCApi.Answer answer) =>
            AndroidNativeCallbackQueue.Post(() => Instance.Raise(answer));

        /// <summary>Reports a failed check through the event, before any native call. Returns whether it failed.</summary>
        private static bool Rejected(string method, string? error, Action<string> fail)
        {
            if (error == null) return false;
            Debug.LogWarning($"[{LogTag}][{method}] {error}");
            fail(error);
            return true;
        }

        /// <summary>Outside an Android device the dialog is not shown and no event is raised, as in 1.x.</summary>
        private static bool OnDevice(string method)
        {
            if (Application.platform == RuntimePlatform.Android) return true;
            // The 1.x text, without the tag: callers and tests match it as it is.
            Debug.LogWarning($"{method} can only be called on an Android device.");
            return false;
        }

        private static string Show(string? value) => value ?? "null";

        /// <summary>Inputs, user names and passwords are logged by length only.</summary>
        private static string Length(string? value) => value == null ? "null" : $"({value.Length} characters)";

#if UNITY_ANDROID && !UNITY_EDITOR
        // The completion callback, kept alive for the process and handed to the library as a pointer.
        private static readonly AndroidDialogCApi.ResultCallback s_callback = OnResult;

        /// <summary>The callback as a function pointer. Internal: a player test calls the library with it directly.</summary>
        internal static readonly IntPtr s_callbackPointer = Marshal.GetFunctionPointerForDelegate(s_callback);

        /// <summary>The kind as user_data (design v4, J-D1).</summary>
        internal static IntPtr KindData(AndroidDialogCApi.Kind kind) => new IntPtr((int)kind);

        /// <summary>
        /// Calls the library and reports a failure before acceptance through the event. On acceptance
        /// the result comes later, through <see cref="OnResult"/>; nothing waits for it.
        /// </summary>
        private static void ShowNative(AndroidDialogCApi.Kind kind, Func<List<IntPtr>, int> call, Action<string> fail)
        {
            // Recreated here if code has destroyed it, so the result has a way back.
            AndroidNativeCallbackQueue.EnsureCreated();

            string? error = null;
            if (AndroidNativeToolkitCApi.EnsureNativeAvailable() != AndroidNativeToolkitCApi.NativeState.Available)
            {
                error = AndroidDialogCApi.NativeUnavailableMessage(kind);
                Debug.LogWarning($"[{LogTag}][{nameof(ShowNative)}] {error}");
            }
            else
            {
                var allocated = new List<IntPtr>();
                try
                {
                    try
                    {
                        int code = call(allocated);
                        if (code != AndroidDialogCApi.ErrorNone)
                        {
                            error = AndroidDialogCApi.EntryFailureMessage(kind, code);
                            if (AndroidDialogCApi.IsRequestDefect(code))
                                Debug.LogError($"[{LogTag}][{nameof(ShowNative)}] {error}");
                            else
                                Debug.LogWarning($"[{LogTag}][{nameof(ShowNative)}] {error}");
                        }
                    }
                    finally
                    {
                        AndroidNativeToolkitCApi.FreeAll(allocated);
                    }
                }
                catch (Exception e)
                {
                    error = $"Internal error: {e.Message}";
                    Debug.LogError($"[{LogTag}][{nameof(ShowNative)}] {e}");
                }
            }
            if (error != null) fail(error);
        }

        /// <summary>
        /// The completion callback, on the Android main thread (not Unity's). Copies the result out of
        /// its handle, frees the handle, and queues the event for Unity's main thread. Touches no
        /// manager state and lets no exception reach the library, where it would end the process.
        /// </summary>
        [MonoPInvokeCallback(typeof(AndroidDialogCApi.ResultCallback))]
        private static void OnResult(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr result)
        {
            try
            {
                AndroidDialogCApi.Answer answer;
                try
                {
                    answer = AndroidDialogCApi.Read((AndroidDialogCApi.Kind)userData.ToInt32(), error, result);
                }
                finally
                {
                    if (result != IntPtr.Zero) AndroidDialogCApi.ntk_dialog_result_free(result);
                }
                Debug.Log($"[{LogTag}][{nameof(OnResult)}] kind: {answer.Kind}, requestId: {requestId}, error: {AndroidDialogCApi.ErrorName(error)}, systemCode: {systemCode}");
                PostAnswer(answer);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnResult)}] {e}");
            }
        }
#endif
    }
}
#endif
