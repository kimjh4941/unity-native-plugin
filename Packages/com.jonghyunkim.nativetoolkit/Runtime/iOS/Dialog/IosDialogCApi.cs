#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Dialog
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;

    /// <summary>
    /// The bridge to native-toolkit's C ABI for iOS dialogs (<c>Dialog.h</c> in IosLibraryCApi): the
    /// request structs, the <c>extern</c> declarations, and the pure conversions between
    /// <see cref="IosDialogManager"/>'s public API and the C ABI (artifact/ios/dialog, C ABI design v2).
    /// </summary>
    /// <remarks>
    /// The declarations compile in the Editor too, so the EditMode check compares them with the
    /// headers (3-S design v3, 1.3); they are called on an iOS player only. Reading a result handle is
    /// compiled into an iOS player only.
    /// </remarks>
    internal static class IosDialogCApi
    {
        /// <summary>
        /// Which dialog a completion belongs to, passed as <c>user_data</c>: the manager's events are per
        /// kind and carry no per-call state (design v2, D-1). Zero is not used.
        /// </summary>
        internal enum Kind
        {
            Alert = 1,
            Confirm = 2,
            Destructive = 3,
            ActionSheet = 4,
            TextInput = 5,
            Login = 6,
        }

        // ntk_dialog_error (Dialog.h).
        internal const int ErrorNone = 0;
        internal const int ErrorInvalidParameter = 1;
        internal const int ErrorNotInitialized = 2;
        internal const int ErrorNotSupported = 3;
        internal const int ErrorUnknown = 4;
        internal const int ErrorOutOfMemory = 5;
        internal const int ErrorCanceled = 6;
        internal const int ErrorCanceledBySystem = 7;
        internal const int ErrorNotForeground = 8;
        internal const int ErrorShowFailed = 10;

        /// <summary><c>ntk_dialog_result_fn</c>.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ResultCallback(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr result);

        // The request structs of Dialog.h (#pragma pack 8). Sizes 32/40/40/48/56/64; the EditMode tests
        // check the size and every field's offset.

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct AlertRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr ButtonText;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ConfirmRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr ConfirmText;
            public IntPtr CancelText;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct DestructiveRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr DestructiveText;
            public IntPtr CancelText;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ActionSheetRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr Options;
            public UIntPtr OptionCount;
            public IntPtr CancelText;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct TextInputRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr Placeholder;
            public IntPtr ConfirmText;
            public IntPtr CancelText;
            public int ConfirmDisabledWhenEmpty;
            public uint Reserved1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct LoginRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr UsernamePlaceholder;
            public IntPtr PasswordPlaceholder;
            public IntPtr LoginText;
            public IntPtr CancelText;
            public int LoginDisabledWhenEmpty;
            public uint Reserved1;
        }

        // The C ABI (D-12: only the functions the manager uses). callback is a function pointer from
        // Marshal.GetFunctionPointerForDelegate; release and out_request_id are always NULL (D-1, D-10).

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern int ntk_dialog_show_alert_async(ref AlertRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern int ntk_dialog_show_confirm_async(ref ConfirmRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern int ntk_dialog_show_destructive_async(ref DestructiveRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern int ntk_dialog_show_action_sheet_async(ref ActionSheetRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern int ntk_dialog_show_text_input_async(ref TextInputRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern int ntk_dialog_show_login_async(ref LoginRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern IntPtr ntk_dialog_result_button_text(IntPtr result, out UIntPtr size);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern IntPtr ntk_dialog_result_text(IntPtr result, out UIntPtr size);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern IntPtr ntk_dialog_result_username(IntPtr result, out UIntPtr size);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern IntPtr ntk_dialog_result_password(IntPtr result, out UIntPtr size);

        [DllImport(IosNativeToolkitCApi.LibraryName)]
        internal static extern void ntk_dialog_result_free(IntPtr result);

        /// <summary>
        /// A completion read out of the native result handle in the callback, so the handle is freed
        /// there and only managed values travel to the next Update (D-7).
        /// </summary>
        internal sealed class Answer
        {
            internal Kind Kind { get; }
            internal int Error { get; }
            internal string? ButtonText { get; }
            internal string? Text { get; }
            internal string? Username { get; }
            internal string? Password { get; }

            internal Answer(Kind kind, int error, string? buttonText = null, string? text = null, string? username = null, string? password = null)
            {
                Kind = kind;
                Error = error;
                ButtonText = buttonText;
                Text = text;
                Username = username;
                Password = password;
            }
        }

        /// <summary>The 1.x native function each kind replaces; messages keep the "Failed to &lt;function&gt;:" form (D-6).</summary>
        internal static string MethodName(Kind kind) => kind switch
        {
            Kind.Alert => "showDialog",
            Kind.Confirm => "showConfirmDialog",
            Kind.Destructive => "showDestructiveDialog",
            Kind.ActionSheet => "showActionSheet",
            Kind.TextInput => "showTextInputDialog",
            Kind.Login => "showLoginDialog",
            _ => kind.ToString(),
        };

        /// <summary>The event each kind raises, as 1.x named it in its "callback error" log.</summary>
        internal static string EventName(Kind kind) => kind switch
        {
            Kind.Alert => "DialogResult",
            Kind.Confirm => "ConfirmDialogResult",
            Kind.Destructive => "DestructiveDialogResult",
            Kind.ActionSheet => "ActionSheetResult",
            Kind.TextInput => "TextInputDialogResult",
            Kind.Login => "LoginDialogResult",
            _ => kind.ToString(),
        };

        /// <summary>The <c>ntk_dialog_error</c> name of a value, or the number when it is not one.</summary>
        internal static string ErrorName(int error) => error switch
        {
            ErrorNone => "NONE",
            ErrorInvalidParameter => "INVALID_PARAMETER",
            ErrorNotInitialized => "NOT_INITIALIZED",
            ErrorNotSupported => "NOT_SUPPORTED",
            ErrorUnknown => "UNKNOWN",
            ErrorOutOfMemory => "OUT_OF_MEMORY",
            ErrorCanceled => "CANCELED",
            ErrorCanceledBySystem => "CANCELED_BY_SYSTEM",
            ErrorNotForeground => "NOT_FOREGROUND",
            ErrorShowFailed => "SHOW_FAILED",
            _ => error.ToString(),
        };

        /// <summary>D-6 (a): the C ABI cannot be used (the Editor, or another major version linked).</summary>
        internal static string NativeUnavailableMessage(Kind kind) =>
            $"Failed to {MethodName(kind)}: the native library is not available.";

        /// <summary>D-6 (b): the C ABI rejected the request (no completion comes).</summary>
        internal static string EntryFailureMessage(Kind kind, int error) =>
            $"Failed to {MethodName(kind)}: the request was rejected ({ErrorName(error)}).";

        /// <summary>D-9: an accepted request that completed with an error.</summary>
        internal static string CompletionFailureMessage(Kind kind, int error)
        {
            string reason = error switch
            {
                ErrorNotForeground => "the app is not in the foreground.",
                ErrorShowFailed => "the dialog could not be shown.",
                ErrorCanceledBySystem => "the dialog was closed by the system.",
                ErrorCanceled => "the dialog was canceled.",
                _ => $"{ErrorName(error)}.",
            };
            return $"Failed to {MethodName(kind)}: {reason}";
        }

        // The checks the manager makes before calling the library, with 1.x's messages; the last is
        // new (D-4).
        internal const string TitleRequired = "Title cannot be null or empty.";
        internal const string MessageRequired = "Message cannot be null or empty.";
        internal const string OptionsRequired = "Options cannot be null or empty.";
        internal const string OptionsContainNull = "Options cannot contain null.";

        /// <summary>The message for an empty title or message, or <see langword="null"/> (1.x's order).</summary>
        internal static string? CheckTitleAndMessage(string? title, string? message)
        {
            if (string.IsNullOrEmpty(title)) return TitleRequired;
            if (string.IsNullOrEmpty(message)) return MessageRequired;
            return null;
        }

        /// <summary>The message for a missing, empty or null-holding options array, or <see langword="null"/>.</summary>
        internal static string? CheckOptions(string[]? options)
        {
            if (options == null || options.Length == 0) return OptionsRequired;
            foreach (string? option in options)
            {
                if (option == null) return OptionsContainNull;
            }
            return null;
        }

        /// <summary>The C ABI names the empty-input flag the other way round (DL-05, D-5).</summary>
        internal static int DisabledWhenEmpty(bool enableWhenEmpty) => enableWhenEmpty ? 0 : 1;

        // The requests (D-3: the caller has already filled 1.x's defaults, so no text is NULL).

        internal static AlertRequest BuildAlert(string title, string message, string buttonText, List<IntPtr> allocated) => new AlertRequest
        {
            StructSize = (uint)Marshal.SizeOf<AlertRequest>(),
            Title = IosNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = IosNativeToolkitCApi.AllocUtf8(message, allocated),
            ButtonText = IosNativeToolkitCApi.AllocUtf8(buttonText, allocated),
        };

        internal static ConfirmRequest BuildConfirm(string title, string message, string confirmText, string cancelText, List<IntPtr> allocated) => new ConfirmRequest
        {
            StructSize = (uint)Marshal.SizeOf<ConfirmRequest>(),
            Title = IosNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = IosNativeToolkitCApi.AllocUtf8(message, allocated),
            ConfirmText = IosNativeToolkitCApi.AllocUtf8(confirmText, allocated),
            CancelText = IosNativeToolkitCApi.AllocUtf8(cancelText, allocated),
        };

        internal static DestructiveRequest BuildDestructive(string title, string message, string destructiveText, string cancelText, List<IntPtr> allocated) => new DestructiveRequest
        {
            StructSize = (uint)Marshal.SizeOf<DestructiveRequest>(),
            Title = IosNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = IosNativeToolkitCApi.AllocUtf8(message, allocated),
            DestructiveText = IosNativeToolkitCApi.AllocUtf8(destructiveText, allocated),
            CancelText = IosNativeToolkitCApi.AllocUtf8(cancelText, allocated),
        };

        internal static ActionSheetRequest BuildActionSheet(string title, string message, string[] options, string cancelText, List<IntPtr> allocated) => new ActionSheetRequest
        {
            StructSize = (uint)Marshal.SizeOf<ActionSheetRequest>(),
            Title = IosNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = IosNativeToolkitCApi.AllocUtf8(message, allocated),
            Options = IosNativeToolkitCApi.AllocStringArray(options, allocated),
            OptionCount = new UIntPtr((uint)options.Length),
            CancelText = IosNativeToolkitCApi.AllocUtf8(cancelText, allocated),
        };

        internal static TextInputRequest BuildTextInput(string title, string message, string placeholder, string confirmText, string cancelText,
            bool enableConfirmWhenEmpty, List<IntPtr> allocated) => new TextInputRequest
        {
            StructSize = (uint)Marshal.SizeOf<TextInputRequest>(),
            Title = IosNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = IosNativeToolkitCApi.AllocUtf8(message, allocated),
            Placeholder = IosNativeToolkitCApi.AllocUtf8(placeholder, allocated),
            ConfirmText = IosNativeToolkitCApi.AllocUtf8(confirmText, allocated),
            CancelText = IosNativeToolkitCApi.AllocUtf8(cancelText, allocated),
            ConfirmDisabledWhenEmpty = DisabledWhenEmpty(enableConfirmWhenEmpty),
        };

        internal static LoginRequest BuildLogin(string title, string message, string usernamePlaceholder, string passwordPlaceholder,
            string loginText, string cancelText, bool enableLoginWhenEmpty, List<IntPtr> allocated) => new LoginRequest
        {
            StructSize = (uint)Marshal.SizeOf<LoginRequest>(),
            Title = IosNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = IosNativeToolkitCApi.AllocUtf8(message, allocated),
            UsernamePlaceholder = IosNativeToolkitCApi.AllocUtf8(usernamePlaceholder, allocated),
            PasswordPlaceholder = IosNativeToolkitCApi.AllocUtf8(passwordPlaceholder, allocated),
            LoginText = IosNativeToolkitCApi.AllocUtf8(loginText, allocated),
            CancelText = IosNativeToolkitCApi.AllocUtf8(cancelText, allocated),
            LoginDisabledWhenEmpty = DisabledWhenEmpty(enableLoginWhenEmpty),
        };

        // The event arguments of an answer (D-8, D-9). A failure leaves every value null.

        internal static (string? buttonText, bool isSuccess, string? errorMessage) ToButtonArgs(Answer answer) =>
            answer.Error == ErrorNone
                ? (answer.ButtonText, true, null)
                : (null, false, CompletionFailureMessage(answer.Kind, answer.Error));

        internal static (string? buttonText, string? inputText, bool isSuccess, string? errorMessage) ToTextInputArgs(Answer answer) =>
            answer.Error == ErrorNone
                ? (answer.ButtonText, answer.Text, true, null)
                : (null, null, false, CompletionFailureMessage(answer.Kind, answer.Error));

        internal static (string? buttonText, string? username, string? password, bool isSuccess, string? errorMessage) ToLoginArgs(Answer answer) =>
            answer.Error == ErrorNone
                ? (answer.ButtonText, answer.Username, answer.Password, true, null)
                : (null, null, null, false, CompletionFailureMessage(answer.Kind, answer.Error));

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// Reads a completion into an <see cref="Answer"/>. The caller frees the handle. Only the readers
        /// the kind has a value for are called; the button kind and option index are not public (D-8).
        /// </summary>
        internal static Answer ReadAnswer(Kind kind, int error, IntPtr result)
        {
            if (error != ErrorNone || result == IntPtr.Zero)
                return new Answer(kind, error == ErrorNone ? ErrorUnknown : error);

            string? buttonText = Read(ntk_dialog_result_button_text(result, out UIntPtr buttonSize), buttonSize);
            switch (kind)
            {
                case Kind.TextInput:
                    return new Answer(kind, error, buttonText, text: Read(ntk_dialog_result_text(result, out UIntPtr textSize), textSize));
                case Kind.Login:
                    string? username = Read(ntk_dialog_result_username(result, out UIntPtr usernameSize), usernameSize);
                    string? password = Read(ntk_dialog_result_password(result, out UIntPtr passwordSize), passwordSize);
                    return new Answer(kind, error, buttonText, username: username, password: password);
                default:
                    return new Answer(kind, error, buttonText);
            }
        }

        private static string? Read(IntPtr data, UIntPtr size) => IosNativeToolkitCApi.ReadUtf8OrNull(data, size);
#endif
    }
}
#endif
