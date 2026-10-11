#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Dialog
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JonghyunKim.NativeToolkit.Runtime.Android.Common;

    /// <summary>
    /// The bridge to native-toolkit's C ABI for dialogs (<c>Dialog.h</c> in libntk.so): the request
    /// structs, the <c>extern</c> declarations, and the pure conversions between the manager's public
    /// API and the C ABI.
    /// </summary>
    /// <remarks>
    /// Declared for Android on its own: the Windows C ABI has functions of the same names with other
    /// arguments, values and layouts (artifact/topics/android-c-abi-2, A-6).
    /// <para>
    /// The conversions compile in the Editor too, so EditMode tests reach them. The <c>extern</c>s and
    /// the reading of a result handle are compiled into an Android player only.
    /// </para>
    /// </remarks>
    internal static class AndroidDialogCApi
    {
        /// <summary>
        /// Which dialog a completion belongs to. Passed as <c>user_data</c>: the manager's events are per
        /// kind and carry no per-call state, so the kind is all a completion needs (design v4, J-D1).
        /// Zero is not used, so <c>user_data</c> is never NULL.
        /// </summary>
        internal enum Kind
        {
            Alert = 1,
            Confirm = 2,
            SingleChoice = 3,
            MultiChoice = 4,
            TextInput = 5,
            Login = 6,
        }

        // ntk_dialog_error (Dialog.h). Not the Windows values: CANCELED is 6 here, 2 on Windows.
        internal const int ErrorNone = 0;
        internal const int ErrorInvalidParameter = 1;
        internal const int ErrorNotInitialized = 2;
        internal const int ErrorNotSupported = 3;
        internal const int ErrorUnknown = 4;
        internal const int ErrorOutOfMemory = 5;
        internal const int ErrorCanceled = 6;
        internal const int ErrorCanceledBySystem = 7;
        internal const int ErrorNotForeground = 8;
        internal const int ErrorHostStartFailed = 9;
        internal const int ErrorShowFailed = 10;

        // ntk_dialog_answer: only DISMISSED is checked. For BUTTON (0) the event reports the label in
        // button_text, so which button it was (ntk_dialog_button, ntk_dialog_result_button) is not read.
        internal const int AnswerDismissed = 1;

        /// <summary>The label 1.x reported for a dialog closed by Back or an outside tap.</summary>
        internal const string DismissedButtonText = "Cancel";

        /// <summary>
        /// The completion callback: <c>ntk_dialog_result_fn</c>. The request ID is 64-bit
        /// (<c>uint64_t</c>), unlike the 32-bit ID of the Windows C ABI.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ResultCallback(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr result);

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct AlertRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr ButtonText;
            public int NotCancelable;
            public int NotCancelableOnTouchOutside;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ConfirmRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr NegativeText;
            public IntPtr PositiveText;
            public int NotCancelable;
            public int NotCancelableOnTouchOutside;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct SingleChoiceRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Items;
            public UIntPtr ItemCount;
            public int CheckedIndex;
            public int NotCancelable;
            public int NotCancelableOnTouchOutside;
            public uint Reserved1;
            public IntPtr NegativeText;
            public IntPtr PositiveText;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct MultiChoiceRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Items;
            public UIntPtr ItemCount;
            public IntPtr Checked;
            public IntPtr NegativeText;
            public IntPtr PositiveText;
            public int NotCancelable;
            public int NotCancelableOnTouchOutside;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct TextInputRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr Hint;
            public IntPtr NegativeText;
            public IntPtr PositiveText;
            public int EnablePositiveWhenEmpty;
            public int NotCancelable;
            public int NotCancelableOnTouchOutside;
            public uint Reserved1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct LoginRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public IntPtr UsernameHint;
            public IntPtr PasswordHint;
            public IntPtr NegativeText;
            public IntPtr PositiveText;
            public int EnablePositiveWhenEmpty;
            public int NotCancelable;
            public int NotCancelableOnTouchOutside;
            public uint Reserved1;
        }

        /// <summary>
        /// A completion copied out of the native result handle on the Android main thread, so the
        /// handle can be freed there and only managed values travel to Unity's main thread.
        /// </summary>
        internal sealed class Answer
        {
            internal Kind Kind { get; }
            internal int Error { get; }
            internal bool IsDismissed { get; }
            internal string? ButtonText { get; }
            internal int CheckedIndex { get; }
            internal bool[]? Checked { get; }
            internal string? Text { get; }
            internal string? Username { get; }
            internal string? Password { get; }

            internal Answer(Kind kind, int error, bool isDismissed = false, string? buttonText = null, int checkedIndex = -1,
                bool[]? @checked = null, string? text = null, string? username = null, string? password = null)
            {
                Kind = kind;
                Error = error;
                IsDismissed = isDismissed;
                ButtonText = buttonText;
                CheckedIndex = checkedIndex;
                Checked = @checked;
                Text = text;
                Username = username;
                Password = password;
            }
        }

        /// <summary>The 1.x bridge method each kind replaces; error messages keep its "Failed to &lt;method&gt;:" form.</summary>
        internal static string MethodName(Kind kind) => kind switch
        {
            Kind.Alert => "showDialog",
            Kind.Confirm => "showConfirmDialog",
            Kind.SingleChoice => "showSingleChoiceItemDialog",
            Kind.MultiChoice => "showMultiChoiceItemDialog",
            Kind.TextInput => "showTextInputDialog",
            Kind.Login => "showLoginDialog",
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
            ErrorHostStartFailed => "HOST_START_FAILED",
            ErrorShowFailed => "SHOW_FAILED",
            _ => error.ToString(),
        };

        /// <summary>The error message for a call the library rejected before accepting it (design v4, 4.4).</summary>
        internal static string EntryFailureMessage(Kind kind, int error)
        {
            string reason = error switch
            {
                ErrorNotInitialized => "the native library is not initialized yet.",
                ErrorInvalidParameter or ErrorNotSupported => $"the request was rejected ({ErrorName(error)}).",
                _ => $"{ErrorName(error)}.",
            };
            return $"Failed to {MethodName(kind)}: {reason}";
        }

        /// <summary>Whether an entry rejection means the C# side built a wrong request (logged as an error).</summary>
        internal static bool IsRequestDefect(int error) => error == ErrorInvalidParameter || error == ErrorNotSupported;

        /// <summary>The error message for an accepted call that completed with an error (design v4, 4.4).</summary>
        internal static string CompletionFailureMessage(Kind kind, int error)
        {
            string reason = error switch
            {
                ErrorNotForeground => "the app is not in the foreground.",
                ErrorHostStartFailed => "the dialog host could not be started.",
                ErrorShowFailed => "the dialog could not be shown.",
                ErrorCanceledBySystem => "the dialog was closed by the system.",
                ErrorCanceled => "the dialog was canceled.",
                _ => $"{ErrorName(error)}.",
            };
            return $"Failed to {MethodName(kind)}: {reason}";
        }

        /// <summary>The error message when libntk.so cannot be used (32-bit, Mono, another major version).</summary>
        internal static string NativeUnavailableMessage(Kind kind) =>
            $"Failed to {MethodName(kind)}: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).";

        // The checks the manager makes before calling the library, in its order and with 1.x's
        // messages; the last two are new (design v4, J-D3 and J-D12).

        internal const string TitleRequired = "Title cannot be null or empty.";
        internal const string MessageRequired = "Message cannot be null or empty.";
        internal const string CheckedItemOutOfRange = "checkedItem is out of range.";

        /// <summary>The message for an empty title or message, or <see langword="null"/>. Pass checkMessage false for the choice dialogs, which have no message.</summary>
        internal static string? CheckTitleAndMessage(string? title, string? message, bool checkMessage)
        {
            if (string.IsNullOrEmpty(title)) return TitleRequired;
            if (checkMessage && string.IsNullOrEmpty(message)) return MessageRequired;
            return null;
        }

        /// <summary>The message for a missing, empty or null-holding item list, or <see langword="null"/>.</summary>
        internal static string? CheckItems(string[]? items, string parameterName)
        {
            if (items == null || items.Length == 0) return $"{parameterName} cannot be null or empty.";
            foreach (string? item in items)
            {
                if (item == null) return $"{parameterName} cannot contain null.";
            }
            return null;
        }

        /// <summary>Whether a single-choice <c>checkedItem</c> is one the C ABI accepts: -1 (none) to the last index.</summary>
        internal static bool IsCheckedItemInRange(int checkedItem, int itemCount) => checkedItem >= -1 && checkedItem < itemCount;

        /// <summary>
        /// The checked states padded with <see langword="false"/> or cut to the number of items: the C
        /// ABI reads exactly one value per item and cannot tell how long the array is (design v4, J-D4).
        /// </summary>
        internal static bool[] FitChecked(bool[]? checkedItems, int itemCount)
        {
            var fitted = new bool[itemCount];
            if (checkedItems != null)
                Array.Copy(checkedItems, fitted, Math.Min(checkedItems.Length, itemCount));
            return fitted;
        }

        internal static int ToFlag(bool value) => value ? 1 : 0;

        /// <summary>The C ABI names its cancel flags the other way round: <c>not_cancelable</c>.</summary>
        internal static int ToNotFlag(bool value) => value ? 0 : 1;

        internal static AlertRequest BuildAlert(string title, string message, string buttonText,
            bool cancelableOnTouchOutside, bool cancelable, List<IntPtr> allocated) => new AlertRequest
        {
            StructSize = (uint)Marshal.SizeOf<AlertRequest>(),
            Title = AndroidNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = AndroidNativeToolkitCApi.AllocUtf8(message, allocated),
            ButtonText = AndroidNativeToolkitCApi.AllocUtf8(buttonText, allocated),
            NotCancelable = ToNotFlag(cancelable),
            NotCancelableOnTouchOutside = ToNotFlag(cancelableOnTouchOutside),
        };

        internal static ConfirmRequest BuildConfirm(string title, string message, string negativeText, string positiveText,
            bool cancelableOnTouchOutside, bool cancelable, List<IntPtr> allocated) => new ConfirmRequest
        {
            StructSize = (uint)Marshal.SizeOf<ConfirmRequest>(),
            Title = AndroidNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = AndroidNativeToolkitCApi.AllocUtf8(message, allocated),
            NegativeText = AndroidNativeToolkitCApi.AllocUtf8(negativeText, allocated),
            PositiveText = AndroidNativeToolkitCApi.AllocUtf8(positiveText, allocated),
            NotCancelable = ToNotFlag(cancelable),
            NotCancelableOnTouchOutside = ToNotFlag(cancelableOnTouchOutside),
        };

        internal static SingleChoiceRequest BuildSingleChoice(string title, string[] items, int checkedItem,
            string negativeText, string positiveText, bool cancelableOnTouchOutside, bool cancelable, List<IntPtr> allocated) => new SingleChoiceRequest
        {
            StructSize = (uint)Marshal.SizeOf<SingleChoiceRequest>(),
            Title = AndroidNativeToolkitCApi.AllocUtf8(title, allocated),
            Items = AllocStringArray(items, allocated),
            ItemCount = new UIntPtr((uint)items.Length),
            CheckedIndex = checkedItem,
            NotCancelable = ToNotFlag(cancelable),
            NotCancelableOnTouchOutside = ToNotFlag(cancelableOnTouchOutside),
            NegativeText = AndroidNativeToolkitCApi.AllocUtf8(negativeText, allocated),
            PositiveText = AndroidNativeToolkitCApi.AllocUtf8(positiveText, allocated),
        };

        internal static MultiChoiceRequest BuildMultiChoice(string title, string[] items, bool[]? checkedItems,
            string negativeText, string positiveText, bool cancelableOnTouchOutside, bool cancelable, List<IntPtr> allocated) => new MultiChoiceRequest
        {
            StructSize = (uint)Marshal.SizeOf<MultiChoiceRequest>(),
            Title = AndroidNativeToolkitCApi.AllocUtf8(title, allocated),
            Items = AllocStringArray(items, allocated),
            ItemCount = new UIntPtr((uint)items.Length),
            Checked = AllocInt32Array(FitChecked(checkedItems, items.Length), allocated),
            NegativeText = AndroidNativeToolkitCApi.AllocUtf8(negativeText, allocated),
            PositiveText = AndroidNativeToolkitCApi.AllocUtf8(positiveText, allocated),
            NotCancelable = ToNotFlag(cancelable),
            NotCancelableOnTouchOutside = ToNotFlag(cancelableOnTouchOutside),
        };

        internal static TextInputRequest BuildTextInput(string title, string message, string hint, string negativeText,
            string positiveText, bool enablePositiveWhenEmpty, bool cancelableOnTouchOutside, bool cancelable, List<IntPtr> allocated) => new TextInputRequest
        {
            StructSize = (uint)Marshal.SizeOf<TextInputRequest>(),
            Title = AndroidNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = AndroidNativeToolkitCApi.AllocUtf8(message, allocated),
            Hint = AndroidNativeToolkitCApi.AllocUtf8(hint, allocated),
            NegativeText = AndroidNativeToolkitCApi.AllocUtf8(negativeText, allocated),
            PositiveText = AndroidNativeToolkitCApi.AllocUtf8(positiveText, allocated),
            EnablePositiveWhenEmpty = ToFlag(enablePositiveWhenEmpty),
            NotCancelable = ToNotFlag(cancelable),
            NotCancelableOnTouchOutside = ToNotFlag(cancelableOnTouchOutside),
        };

        internal static LoginRequest BuildLogin(string title, string message, string usernameHint, string passwordHint,
            string negativeText, string positiveText, bool enablePositiveWhenEmpty, bool cancelableOnTouchOutside, bool cancelable,
            List<IntPtr> allocated) => new LoginRequest
        {
            StructSize = (uint)Marshal.SizeOf<LoginRequest>(),
            Title = AndroidNativeToolkitCApi.AllocUtf8(title, allocated),
            Message = AndroidNativeToolkitCApi.AllocUtf8(message, allocated),
            UsernameHint = AndroidNativeToolkitCApi.AllocUtf8(usernameHint, allocated),
            PasswordHint = AndroidNativeToolkitCApi.AllocUtf8(passwordHint, allocated),
            NegativeText = AndroidNativeToolkitCApi.AllocUtf8(negativeText, allocated),
            PositiveText = AndroidNativeToolkitCApi.AllocUtf8(positiveText, allocated),
            EnablePositiveWhenEmpty = ToFlag(enablePositiveWhenEmpty),
            NotCancelable = ToNotFlag(cancelable),
            NotCancelableOnTouchOutside = ToNotFlag(cancelableOnTouchOutside),
        };

        /// <summary>A <c>const char* const*</c>: each item as NUL-terminated UTF-8, then the array of pointers.</summary>
        internal static IntPtr AllocStringArray(string[] items, List<IntPtr> allocated)
        {
            IntPtr array = AndroidNativeToolkitCApi.Alloc(IntPtr.Size * Math.Max(items.Length, 1), allocated);
            for (int i = 0; i < items.Length; i++)
                Marshal.WriteIntPtr(array, i * IntPtr.Size, AndroidNativeToolkitCApi.AllocUtf8(items[i], allocated));
            return array;
        }

        /// <summary>A <c>const int32_t*</c> of 1 for true and 0 for false.</summary>
        internal static IntPtr AllocInt32Array(bool[] values, List<IntPtr> allocated)
        {
            IntPtr array = AndroidNativeToolkitCApi.Alloc(sizeof(int) * Math.Max(values.Length, 1), allocated);
            for (int i = 0; i < values.Length; i++)
                Marshal.WriteInt32(array, i * sizeof(int), ToFlag(values[i]));
            return array;
        }

        // The event arguments of an answer (design v4, 4.3). A failure leaves every value null.

        internal static (string? buttonText, bool isSuccessful, string? errorMessage) ToButtonArgs(Answer answer)
        {
            if (answer.Error != ErrorNone)
                return (null, false, CompletionFailureMessage(answer.Kind, answer.Error));
            if (answer.IsDismissed)
                return (DismissedButtonText, true, "");
            return (answer.ButtonText, true, null);
        }

        internal static (string? buttonText, int? checkedItem, bool isSuccessful, string? errorMessage) ToSingleChoiceArgs(Answer answer)
        {
            var (buttonText, isSuccessful, errorMessage) = ToButtonArgs(answer);
            int? checkedItem = isSuccessful && !answer.IsDismissed && answer.CheckedIndex >= 0 ? answer.CheckedIndex : (int?)null;
            return (buttonText, checkedItem, isSuccessful, errorMessage);
        }

        internal static (string? buttonText, bool[]? checkedItems, bool isSuccessful, string? errorMessage) ToMultiChoiceArgs(Answer answer)
        {
            var (buttonText, isSuccessful, errorMessage) = ToButtonArgs(answer);
            bool[]? checkedItems = isSuccessful && !answer.IsDismissed ? answer.Checked : null;
            return (buttonText, checkedItems, isSuccessful, errorMessage);
        }

        internal static (string? buttonText, string? inputText, bool isSuccessful, string? errorMessage) ToTextInputArgs(Answer answer)
        {
            var (buttonText, isSuccessful, errorMessage) = ToButtonArgs(answer);
            string? inputText = !isSuccessful ? null : answer.IsDismissed ? "" : answer.Text;
            return (buttonText, inputText, isSuccessful, errorMessage);
        }

        internal static (string? buttonText, string? username, string? password, bool isSuccessful, string? errorMessage) ToLoginArgs(Answer answer)
        {
            var (buttonText, isSuccessful, errorMessage) = ToButtonArgs(answer);
            if (!isSuccessful) return (buttonText, null, null, false, errorMessage);
            if (answer.IsDismissed) return (buttonText, "", "", true, errorMessage);
            return (buttonText, answer.Username, answer.Password, true, errorMessage);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Copies a completion out of its result handle. Does not free the handle: the caller does, in
        /// a finally. <paramref name="result"/> is NULL unless <paramref name="error"/> is NONE.
        /// </summary>
        internal static Answer Read(Kind kind, int error, IntPtr result)
        {
            if (error != ErrorNone || result == IntPtr.Zero)
                return new Answer(kind, error == ErrorNone ? ErrorUnknown : error);

            // DISMISSED reports button 0 (POSITIVE) and no text, so the answer is read first.
            if (ntk_dialog_result_answer(result) == AnswerDismissed)
                return new Answer(kind, ErrorNone, isDismissed: true);

            string? buttonText = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_dialog_result_button_text(result, out UIntPtr buttonSize), buttonSize);
            switch (kind)
            {
                case Kind.SingleChoice:
                    return new Answer(kind, ErrorNone, buttonText: buttonText, checkedIndex: ntk_dialog_result_checked_index(result));
                case Kind.MultiChoice:
                {
                    ulong count = ntk_dialog_result_checked_count(result).ToUInt64();
                    bool[]? states = null;
                    if (count > 0)
                    {
                        states = new bool[count];
                        for (ulong i = 0; i < count; i++)
                            states[i] = ntk_dialog_result_checked_at(result, new UIntPtr(i)) != 0;
                    }
                    return new Answer(kind, ErrorNone, buttonText: buttonText, @checked: states);
                }
                case Kind.TextInput:
                    return new Answer(kind, ErrorNone, buttonText: buttonText, text: AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_dialog_result_text(result, out UIntPtr textSize), textSize));
                case Kind.Login:
                    return new Answer(kind, ErrorNone, buttonText: buttonText,
                        username: AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_dialog_result_username(result, out UIntPtr usernameSize), usernameSize),
                        password: AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_dialog_result_password(result, out UIntPtr passwordSize), passwordSize));
                default:
                    return new Answer(kind, ErrorNone, buttonText: buttonText);
            }
        }

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_alert_async(ref AlertRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_confirm_async(ref ConfirmRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_single_choice_async(ref SingleChoiceRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_multi_choice_async(ref MultiChoiceRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_text_input_async(ref TextInputRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_login_async(ref LoginRequest request, IntPtr callback, IntPtr userData, IntPtr release, IntPtr outRequestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_cancel(ulong requestId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_result_answer(IntPtr result);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_dialog_result_button_text(IntPtr result, out UIntPtr outSize);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_result_checked_index(IntPtr result);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_dialog_result_checked_count(IntPtr result);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_result_checked_at(IntPtr result, UIntPtr index);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_dialog_result_text(IntPtr result, out UIntPtr outSize);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_dialog_result_username(IntPtr result, out UIntPtr outSize);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_dialog_result_password(IntPtr result, out UIntPtr outSize);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_dialog_result_free(IntPtr result);
#endif
    }
}
#endif
