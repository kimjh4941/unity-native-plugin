#nullable enable

#if UNITY_IOS
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Dialog
{
    using UnityEngine;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System;
#if !UNITY_EDITOR
    using AOT;
#endif
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
    using Kind = IosDialogCApi.Kind;

    /// <summary>
    /// Singleton manager for iOS native dialog operations.
    /// Provides a Unity-friendly API for showing various types of iOS native dialogs including
    /// basic alerts, confirmation dialogs, destructive alerts, action sheets, text input, and login dialogs.
    /// Results arrive through the events.
    /// </summary>
    /// <remarks>
    /// Calls native-toolkit's C ABI 2.0.0 (<c>Dialog.h</c>; artifact/ios/dialog, C ABI design v2). A call
    /// the C# checks, or the library, reject raises its event once inside the call, with every value
    /// null. An accepted call raises its event once on the Update after the dialog is answered (the
    /// completion arrives on the main thread and is queued on <see cref="UnityMainThreadDispatcher"/>, as
    /// in 1.x). A dialog shown while another is up is shown over it. In the Editor the native library
    /// is not there, and every call fails inside the call (with a warning).
    /// <para>
    /// A subscriber's exception is logged ("&lt;event&gt; callback error: …") and does not leave, also for
    /// the failures raised inside the call. In 1.x, an exception thrown by a subscriber to such a failure
    /// reached the caller of Show*.
    /// </para>
    /// </remarks>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Dialog")]
    public class IosDialogManager : MonoBehaviour
    {
        private static IosDialogManager? _instance;

        /// <summary>
        /// Singleton instance property for IosDialogManager.
        /// Creates a new instance if none exists and ensures it persists across scene loads.
        /// </summary>
        public static IosDialogManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log("Creating new instance of IosDialogManager");
                    GameObject singletonObject = new GameObject("IosDialogManager");
                    _instance = singletonObject.AddComponent<IosDialogManager>();
                    DontDestroyOnLoad(singletonObject);
                }
                return _instance;
            }
        }

        // Event handlers for receiving dialog results (includes success status and error messages)
        public event Action<string?, bool, string?>? DialogResult; // buttonText, isSuccess, errorMessage
        public event Action<string?, bool, string?>? ConfirmDialogResult; // buttonText, isSuccess, errorMessage
        public event Action<string?, bool, string?>? DestructiveDialogResult; // buttonText, isSuccess, errorMessage
        public event Action<string?, bool, string?>? ActionSheetResult; // buttonText, isSuccess, errorMessage
        public event Action<string?, string?, bool, string?>? TextInputDialogResult; // buttonText, inputText, isSuccess, errorMessage
        public event Action<string?, string?, string?, bool, string?>? LoginDialogResult; // buttonText, username, password, isSuccess, errorMessage

        /// <summary>
        /// Initialize the singleton instance and ensure persistence across scene changes.
        /// </summary>
        private void Awake()
        {
            Debug.Log("IosDialogManager Awake");
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // The callback types of the 1.x bridge. Not used since native-toolkit 2.0.0; kept so that code
        // naming them still compiles (C ABI design v2, D-13: whether to remove them is decided in stage 4).
        /// <summary>
        /// Callback signature for basic alert dialog results.
        /// </summary>
        /// <param name="buttonText">Pressed button text (may be null on failure)</param>
        /// <param name="isSuccess">True if native dialog completed successfully</param>
        /// <param name="errorMessage">Error details if <paramref name="isSuccess"/> is false</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DialogCallback(string? buttonText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for confirmation dialog (OK / Cancel) results.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ConfirmDialogCallback(string? buttonText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for destructive confirmation dialog results.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DestructiveDialogCallback(string? buttonText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for action sheet selection results.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ActionSheetCallback(string? buttonText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for text input dialog results.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void TextInputDialogCallback(string? buttonText, string? inputText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for login dialog results (username + password).
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LoginDialogCallback(string? buttonText, string? username, string? password, bool isSuccess, string? errorMessage);

#if !UNITY_EDITOR
        // Kept for the life of the process, so the function pointer handed to the C ABI stays valid.
        // Internal for the player tests, which call the C ABI and the completion with it.
        private static readonly IosDialogCApi.ResultCallback s_resultCallback = OnResult;
        internal static readonly IntPtr s_resultCallbackPointer = Marshal.GetFunctionPointerForDelegate(s_resultCallback);
#endif

        // Public dialog API

        /// <summary>
        /// Shows a basic alert dialog with a single button.
        /// </summary>
        public void ShowDialog(string title, string message, string? buttonText = "OK")
        {
            Debug.Log($"ShowDialog called with title: {title}, message: {message}, buttonText: {buttonText}");
            if (RejectedInCSharp(Kind.Alert, IosDialogCApi.CheckTitleAndMessage(title, message))) return;

            string button = buttonText ?? "OK";
            ShowNative(Kind.Alert, (allocated, callback) =>
            {
                var request = IosDialogCApi.BuildAlert(title, message, button, allocated);
                return IosDialogCApi.ntk_dialog_show_alert_async(ref request, callback, UserData(Kind.Alert), IntPtr.Zero, IntPtr.Zero);
            });
        }

        /// <summary>
        /// Shows a confirmation dialog with confirm / cancel buttons.
        /// </summary>
        public void ShowConfirmDialog(string title, string message, string? confirmButtonText = "OK", string? cancelButtonText = "Cancel")
        {
            Debug.Log($"ShowConfirmDialog called with title: {title}, message: {message}, confirmButtonText: {confirmButtonText}, cancelButtonText: {cancelButtonText}");
            if (RejectedInCSharp(Kind.Confirm, IosDialogCApi.CheckTitleAndMessage(title, message))) return;

            string confirm = confirmButtonText ?? "OK";
            string cancel = cancelButtonText ?? "Cancel";
            ShowNative(Kind.Confirm, (allocated, callback) =>
            {
                var request = IosDialogCApi.BuildConfirm(title, message, confirm, cancel, allocated);
                return IosDialogCApi.ntk_dialog_show_confirm_async(ref request, callback, UserData(Kind.Confirm), IntPtr.Zero, IntPtr.Zero);
            });
        }

        /// <summary>
        /// Shows a destructive confirmation dialog (e.g. Delete confirmation).
        /// </summary>
        public void ShowDestructiveDialog(string title, string message, string? destructiveButtonText = "Delete", string? cancelButtonText = "Cancel")
        {
            Debug.Log($"ShowDestructiveDialog called with title: {title}, message: {message}, destructiveButtonText: {destructiveButtonText}, cancelButtonText: {cancelButtonText}");
            if (RejectedInCSharp(Kind.Destructive, IosDialogCApi.CheckTitleAndMessage(title, message))) return;

            string destructive = destructiveButtonText ?? "Delete";
            string cancel = cancelButtonText ?? "Cancel";
            ShowNative(Kind.Destructive, (allocated, callback) =>
            {
                var request = IosDialogCApi.BuildDestructive(title, message, destructive, cancel, allocated);
                return IosDialogCApi.ntk_dialog_show_destructive_async(ref request, callback, UserData(Kind.Destructive), IntPtr.Zero, IntPtr.Zero);
            });
        }

        /// <summary>
        /// Shows an action sheet with multiple selectable options.
        /// </summary>
        public void ShowActionSheet(string title, string message, string[] options, string? cancelButtonText = "Cancel")
        {
            var optionsList = options == null ? "[]" : $"[{string.Join(", ", options)}]";
            Debug.Log($"ShowActionSheet called with title: {title}, message: {message}, options: {optionsList}, cancelButtonText: {cancelButtonText}");
            if (RejectedInCSharp(Kind.ActionSheet, IosDialogCApi.CheckTitleAndMessage(title, message) ?? IosDialogCApi.CheckOptions(options))) return;

            string cancel = cancelButtonText ?? "Cancel";
            string[] chosen = options!;
            ShowNative(Kind.ActionSheet, (allocated, callback) =>
            {
                var request = IosDialogCApi.BuildActionSheet(title, message, chosen, cancel, allocated);
                return IosDialogCApi.ntk_dialog_show_action_sheet_async(ref request, callback, UserData(Kind.ActionSheet), IntPtr.Zero, IntPtr.Zero);
            });
        }

        /// <summary>
        /// Shows a text input dialog with a single text field.
        /// </summary>
        /// <param name="enableConfirmWhenEmpty">If true the confirm button remains enabled when input is empty (default: false)</param>
        public void ShowTextInputDialog(string title, string message, string? placeholder = "", string? confirmButtonText = "OK", string? cancelButtonText = "Cancel", bool enableConfirmWhenEmpty = false)
        {
            Debug.Log($"ShowTextInputDialog called with title: {title}, message: {message}, placeholder: {placeholder}, confirmButtonText: {confirmButtonText}, cancelButtonText: {cancelButtonText}, enableConfirmWhenEmpty: {enableConfirmWhenEmpty}");
            if (RejectedInCSharp(Kind.TextInput, IosDialogCApi.CheckTitleAndMessage(title, message))) return;

            string hint = placeholder ?? "";
            string confirm = confirmButtonText ?? "OK";
            string cancel = cancelButtonText ?? "Cancel";
            ShowNative(Kind.TextInput, (allocated, callback) =>
            {
                var request = IosDialogCApi.BuildTextInput(title, message, hint, confirm, cancel, enableConfirmWhenEmpty, allocated);
                return IosDialogCApi.ntk_dialog_show_text_input_async(ref request, callback, UserData(Kind.TextInput), IntPtr.Zero, IntPtr.Zero);
            });
        }

        /// <summary>
        /// Shows a login dialog requesting username and password.
        /// </summary>
        /// <param name="enableLoginWhenEmpty">If true the login button remains enabled when fields are empty (default: false)</param>
        public void ShowLoginDialog(string title, string message, string? usernamePlaceholder = "Username", string? passwordPlaceholder = "Password", string? loginButtonText = "Login", string? cancelButtonText = "Cancel", bool enableLoginWhenEmpty = false)
        {
            Debug.Log($"ShowLoginDialog called with title: {title}, message: {message}, usernamePlaceholder: {usernamePlaceholder}, passwordPlaceholder: {passwordPlaceholder}, loginButtonText: {loginButtonText}, cancelButtonText: {cancelButtonText}, enableLoginWhenEmpty: {enableLoginWhenEmpty}");
            if (RejectedInCSharp(Kind.Login, IosDialogCApi.CheckTitleAndMessage(title, message))) return;

            string usernameHint = usernamePlaceholder ?? "Username";
            string passwordHint = passwordPlaceholder ?? "Password";
            string login = loginButtonText ?? "Login";
            string cancel = cancelButtonText ?? "Cancel";
            ShowNative(Kind.Login, (allocated, callback) =>
            {
                var request = IosDialogCApi.BuildLogin(title, message, usernameHint, passwordHint, login, cancel, enableLoginWhenEmpty, allocated);
                return IosDialogCApi.ntk_dialog_show_login_async(ref request, callback, UserData(Kind.Login), IntPtr.Zero, IntPtr.Zero);
            });
        }

        // ── Calling the C ABI ──────────────────────────────────────────────────

        private static IntPtr UserData(Kind kind) => new IntPtr((int)kind);

        /// <summary>A C# check failed: log it as 1.x did and raise the event once inside the call.</summary>
        private bool RejectedInCSharp(Kind kind, string? error)
        {
            if (error == null) return false;
            Debug.LogError(error);
            Raise(new IosDialogCApi.Answer(kind, IosDialogCApi.ErrorUnknown), error);
            return true;
        }

        /// <summary>
        /// Calls the library (D-6, D-2): when it cannot be used, or it rejects the request, the event is
        /// raised once inside the call. The request's strings are freed when the call returns (the
        /// library copies them during the call).
        /// </summary>
        private void ShowNative(Kind kind, Func<List<IntPtr>, IntPtr, int> call)
        {
            if (!IosNativeToolkitCApi.IsAvailable)
            {
                string unavailable = IosDialogCApi.NativeUnavailableMessage(kind);
                Debug.LogWarning(unavailable);
                Raise(new IosDialogCApi.Answer(kind, IosDialogCApi.ErrorNotInitialized), unavailable);
                return;
            }
#if !UNITY_EDITOR
            var allocated = new List<IntPtr>();
            int error;
            try
            {
                error = call(allocated, s_resultCallbackPointer);
            }
            catch (Exception ex)
            {
                // 1.x's and Android's wording for an exception on the way in.
                Debug.LogError($"{IosDialogCApi.MethodName(kind)} error: {ex.Message}");
                Raise(new IosDialogCApi.Answer(kind, IosDialogCApi.ErrorUnknown), $"Internal error: {ex.Message}");
                return;
            }
            finally
            {
                IosNativeToolkitCApi.FreeAll(allocated);
            }
            if (error != IosDialogCApi.ErrorNone)
            {
                // The C# checks should have caught anything the library rejects: a wrong request.
                Debug.LogError($"{IosDialogCApi.MethodName(kind)} was rejected: {IosDialogCApi.ErrorName(error)}");
                Raise(new IosDialogCApi.Answer(kind, error), IosDialogCApi.EntryFailureMessage(kind, error));
            }
#endif
        }

#if !UNITY_EDITOR
        /// <summary>
        /// The completion of every dialog (on the main thread). Reads the answer and frees the handle
        /// here, then raises the kind's event on the next Update (D-7). No exception leaves.
        /// </summary>
        [MonoPInvokeCallback(typeof(IosDialogCApi.ResultCallback))]
        internal static void OnResult(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr result)
        {
            try
            {
                var kind = (Kind)userData.ToInt32();
                IosDialogCApi.Answer answer;
                try
                {
                    answer = IosDialogCApi.ReadAnswer(kind, error, result);
                }
                catch (Exception ex)
                {
                    // An accepted call still completes once: as UNKNOWN when its answer cannot be read.
                    Debug.LogError($"IosDialogManager could not read a {IosDialogCApi.MethodName(kind)} answer: {ex.Message}");
                    answer = new IosDialogCApi.Answer(kind, IosDialogCApi.ErrorUnknown);
                }
                UnityMainThreadDispatcher.Instance.Enqueue(() => Instance.Raise(answer));
            }
            catch (Exception ex)
            {
                Debug.LogError($"IosDialogManager completion error: {ex.Message}");
            }
            finally
            {
                if (result != IntPtr.Zero)
                    IosDialogCApi.ntk_dialog_result_free(result);
            }
        }
#endif

        /// <summary>
        /// Raises the event of an answer's kind. A subscriber's exception is logged with 1.x's message
        /// and does not leave (D-7): it would stop the dispatcher's Update and hold back the rest of
        /// its queue. <paramref name="failure"/> replaces the completion's message for a call that
        /// failed before it was accepted (every value is then null).
        /// </summary>
        internal void Raise(IosDialogCApi.Answer answer, string? failure = null)
        {
            try
            {
                switch (answer.Kind)
                {
                    case Kind.TextInput:
                    {
                        var a = failure != null ? (null, null, false, failure) : IosDialogCApi.ToTextInputArgs(answer);
                        TextInputDialogResult?.Invoke(a.Item1, a.Item2, a.Item3, a.Item4);
                        break;
                    }
                    case Kind.Login:
                    {
                        var a = failure != null ? (null, null, null, false, failure) : IosDialogCApi.ToLoginArgs(answer);
                        LoginDialogResult?.Invoke(a.Item1, a.Item2, a.Item3, a.Item4, a.Item5);
                        break;
                    }
                    case Kind.Alert:
                    case Kind.Confirm:
                    case Kind.Destructive:
                    case Kind.ActionSheet:
                    {
                        var a = failure != null ? (null, false, failure) : IosDialogCApi.ToButtonArgs(answer);
                        ButtonEvent(answer.Kind)?.Invoke(a.Item1, a.Item2, a.Item3);
                        break;
                    }
                    default:
                        Debug.LogError($"IosDialogManager got an answer of an unknown kind {(int)answer.Kind}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{IosDialogCApi.EventName(answer.Kind)} callback error: {ex.Message}");
            }
        }

        private Action<string?, bool, string?>? ButtonEvent(Kind kind) => kind switch
        {
            Kind.Alert => DialogResult,
            Kind.Confirm => ConfirmDialogResult,
            Kind.Destructive => DestructiveDialogResult,
            Kind.ActionSheet => ActionSheetResult,
            _ => null,
        };
    }
}
#endif
