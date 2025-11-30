#nullable enable

#if UNITY_STANDALONE_OSX
namespace JonghyunKim.NativeToolkit.Runtime.Dialog
{
    using UnityEngine;
    using System.Runtime.InteropServices;
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;
    using JonghyunKim.NativeToolkit.Runtime.Common;

    /// <summary>
    /// Wrapper class for dialog button configuration in JSON serialization.
    /// Contains an array of DialogButton objects for complex dialog setups.
    /// </summary>
    public class DialogButtonsWrapper
    {
        public DialogButton[]? buttons;
    }

    /// <summary>
    /// Represents a single button configuration for macOS native dialogs.
    /// Defines button appearance, behavior, and keyboard shortcuts.
    /// </summary>
    public class DialogButton
    {
        public string title;
        public bool isDefault;
        public string? keyEquivalent;

        public DialogButton(string title, bool isDefault = false, string? keyEquivalent = null)
        {
            this.title = title;
            this.isDefault = isDefault;
            if (isDefault)
            {
                this.keyEquivalent = "\r"; // Enter key
            }
            else
            {
                this.keyEquivalent = keyEquivalent;
            }
        }
    }

    /// <summary>
    /// Configuration options for macOS native dialog appearance and behavior.
    /// Controls alert style, suppression button, and other dialog-specific settings.
    /// </summary>
    public class DialogOptions
    {
        public enum AlertStyle
        {
            Informational,
            Warning,
            Critical
        }

        public AlertStyle alertStyle;
        public bool showsHelp;
        public bool showsSuppressionButton;
        public string? suppressionButtonTitle;
        public IconConfiguration? icon;

        public DialogOptions(AlertStyle alertStyle, bool showsHelp = false, bool showsSuppressionButton = false, string? suppressionButtonTitle = null, IconConfiguration? icon = null)
        {
            this.alertStyle = alertStyle;
            this.showsHelp = showsHelp;
            this.showsSuppressionButton = showsSuppressionButton;
            this.suppressionButtonTitle = suppressionButtonTitle;
            this.icon = icon;
        }
    }

    /// <summary>
    /// Singleton manager for macOS native dialog operations using Unity's native plugin interface.
    /// Provides a Unity-friendly API for showing various types of macOS native dialogs including
    /// alert dialogs, file selection dialogs, folder selection dialogs, and save dialogs.
    /// Uses P/Invoke to communicate with Objective-C native code and event-driven callbacks for results.
    /// </summary>
    public class MacDialogManager : MonoBehaviour
    {
        private static MacDialogManager? _instance;

        /// <summary>
        /// Singleton instance property for MacDialogManager.
        /// Creates a new instance if none exists and ensures it persists across scene loads.
        /// </summary>
        public static MacDialogManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log("Creating new instance of MacDialogManager");
                    GameObject singletonObject = new GameObject("MacDialogManager");
                    _instance = singletonObject.AddComponent<MacDialogManager>();
                    DontDestroyOnLoad(singletonObject);
                }
                return _instance;
            }
        }
        /// <summary>
        /// Raised when an alert dialog (standard macOS NSAlert) completes.
        /// </summary>
        /// <remarks>
        /// Parameters:
        /// buttonTitle = title of the pressed button (may be null if failure),
        /// buttonIndex = zero-based index of pressed button in original provided array ( -1 on error ),
        /// suppressionButtonState = state of the optional suppression checkbox (true if checked),
        /// helpButtonPressed = true if the help button was pressed (if present),
        /// isSuccess = true if the native call succeeded, false if an error occurred prior to completion,
        /// errorMessage = error description when <c>isSuccess == false</c> otherwise null.
        /// </remarks>
        public event Action<string?, int, bool, bool, bool, string?>? AlertDialogResult;

        /// <summary>
        /// Raised when a single-selection file open panel closes.
        /// </summary>
        /// <remarks>
        /// Parameters:
        /// filePaths = array containing the selected file path (null if none),
        /// fileCount = number of returned paths ( -1 on error ),
        /// directoryURL = directory path the panel was opened in, may be null,
        /// isCancelled = true if the user cancelled, false otherwise,
        /// isSuccess = false only if a native interop error occurred,
        /// errorMessage = populated only when <c>isSuccess == false</c>.
        /// </remarks>
        public event Action<string[]?, int, string?, bool, bool, string?>? FileDialogResult;

        /// <summary>
        /// Raised when a multi-file selection open panel closes.
        /// </summary>
        /// <remarks>See <see cref="FileDialogResult"/> for parameter semantics; filePaths can contain multiple entries.</remarks>
        public event Action<string[]?, int, string?, bool, bool, string?>? MultiFileDialogResult;

        /// <summary>
        /// Raised when a single folder selection panel closes.
        /// </summary>
        /// <remarks>Parameters mirror <see cref="FileDialogResult"/> but represent folders instead of files.</remarks>
        public event Action<string[]?, int, string?, bool, bool, string?>? FolderDialogResult;

        /// <summary>
        /// Raised when a multi-folder selection panel closes.
        /// </summary>
        /// <remarks>Parameters mirror <see cref="MultiFileDialogResult"/> but represent folders instead of files.</remarks>
        public event Action<string[]?, int, string?, bool, bool, string?>? MultiFolderDialogResult;

        /// <summary>
        /// Raised when a save panel closes.
        /// </summary>
        /// <remarks>
        /// filePath = resulting saved file path (null on cancel/error),
        /// fileCount = always 1 when successful ( -1 on error ),
        /// directoryURL = directory path containing the saved file (may be null),
        /// isCancelled = true if the user cancelled,
        /// isSuccess = false only if a native interop error occurred,
        /// errorMessage = populated only when <c>isSuccess == false</c>.
        /// </remarks>
        public event Action<string?, int, string?, bool, bool, string?>? SaveFileDialogResult;

        /// <summary>
        /// JSON serialization settings for dialog configurations.
        /// </summary>
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter(new LowercaseNamingStrategy(), false) }
        };

        /// <summary>
        /// Custom naming strategy to convert enum names to lowercase during JSON serialization.
        /// </summary>
        private sealed class LowercaseNamingStrategy : NamingStrategy
        {
            protected override string ResolvePropertyName(string name)
                => name?.ToLowerInvariant() ?? string.Empty;
        }

        /// <summary>
        /// Awake lifecycle method to enforce singleton pattern.
        /// </summary>
        private void Awake()
        {
            Debug.Log("Awake");
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

        /// <summary>
        /// Validates the provided DialogOptions for correctness before showing a dialog.
        /// </summary>
        /// <param name="options">The dialog options to validate.</param>
        /// <returns>True if the options are valid; otherwise, false.</returns>
        private bool ValidateDialogOptions(DialogOptions options)
        {
            if (options == null)
            {
                Debug.LogError("No options provided for the dialog.");
                AlertDialogResult?.Invoke(null, -1, false, false, false, "No options provided for the dialog.");
                return false;
            }

            if (options.icon != null)
            {
                switch (options.icon.type)
                {
                    case IconConfiguration.IconType.SystemSymbol:
                        if (string.IsNullOrEmpty(options.icon.value))
                        {
                            Debug.LogError("IconConfiguration of type SystemSymbol requires a non-empty value.");
                            AlertDialogResult?.Invoke(null, -1, false, false, false, "IconConfiguration of type SystemSymbol requires a non-empty value.");
                            return false;
                        }

                        if (options.icon.mode == null)
                        {
                            Debug.LogError("IconConfiguration of type SystemSymbol requires a rendering mode.");
                            AlertDialogResult?.Invoke(null, -1, false, false, false, "IconConfiguration of type SystemSymbol requires a rendering mode.");
                            return false;
                        }

                        switch (options.icon.mode)
                        {
                            case IconConfiguration.RenderingMode.Monochrome:
                                break;
                            case IconConfiguration.RenderingMode.Hierarchical:
                                break;
                            case IconConfiguration.RenderingMode.Palette:
                                if (options.icon.colors.Count == 0)
                                {
                                    Debug.LogError("IconConfiguration of type SystemSymbol with Palette mode requires at least one color.");
                                    AlertDialogResult?.Invoke(null, -1, false, false, false, "IconConfiguration of type SystemSymbol with Palette mode requires at least one color.");
                                    return false;
                                }
                                if (options.icon.colors.Count > 3)
                                {
                                    Debug.LogError("IconConfiguration of type SystemSymbol with Palette mode requires at most three colors.");
                                    AlertDialogResult?.Invoke(null, -1, false, false, false, "IconConfiguration of type SystemSymbol with Palette mode requires at most three colors.");
                                    return false;
                                }
                                break;
                            case IconConfiguration.RenderingMode.Multicolor:
                                break;
                        }
                        break;
                    case IconConfiguration.IconType.FilePath:
                        if (string.IsNullOrEmpty(options.icon.value))
                        {
                            Debug.LogError("IconConfiguration of type FilePath requires a non-empty value.");
                            AlertDialogResult?.Invoke(null, -1, false, false, false, "IconConfiguration of type FilePath requires a non-empty value.");
                            return false;
                        }
                        break;
                    case IconConfiguration.IconType.NamedImage:
                        if (string.IsNullOrEmpty(options.icon.value))
                        {
                            Debug.LogError("IconConfiguration of type NamedImage requires a non-empty value.");
                            AlertDialogResult?.Invoke(null, -1, false, false, false, "IconConfiguration of type NamedImage requires a non-empty value.");
                            return false;
                        }
                        break;
                    case IconConfiguration.IconType.AppIcon:
                        break;
                    case IconConfiguration.IconType.SystemImage:
                        if (string.IsNullOrEmpty(options.icon.value))
                        {
                            Debug.LogError("IconConfiguration of type SystemImage requires a non-empty value.");
                            AlertDialogResult?.Invoke(null, -1, false, false, false, "IconConfiguration of type SystemImage requires a non-empty value.");
                            return false;
                        }
                        break;
                }
            }
            return true;
        }

        // Native Objective-C callback delegate type definitions
        /// <summary>
        /// Callback signature for alert dialogs.
        /// </summary>
        /// <param name="buttonTitle">Title of the button pressed.</param>
        /// <param name="buttonIndex">Zero-based index of the pressed button; -1 if an error occurred.</param>
        /// <param name="suppressionButtonState">State of suppression checkbox (true if checked).</param>
        /// <param name="helpButtonPressed">True if the help button was pressed (if present).</param>
        /// <param name="isSuccess">True if the dialog completed successfully; false on internal/native failure.</param>
        /// <param name="errorMessage">Error description when <c>isSuccess == false</c>; otherwise empty or null.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DialogCallback(string buttonTitle, int buttonIndex, bool suppressionButtonState, bool helpButtonPressed, bool isSuccess, string errorMessage);

        /// <summary>
        /// Callback signature for single file selection dialogs.
        /// </summary>
        /// <param name="filePaths">Pointer to an array (UTF-8 string pointers) of selected file paths.</param>
        /// <param name="fileCount">Number of selected files.</param>
        /// <param name="directoryURL">Directory in which selection occurred.</param>
        /// <param name="isCancelled">True if the user cancelled the panel.</param>
        /// <param name="isSuccess">False only when a native failure occurred.</param>
        /// <param name="errorMessage">Error message when <c>isSuccess == false</c>.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FileDialogCallback(IntPtr filePaths, int fileCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

        /// <summary>
        /// Callback signature for multi-file selection dialogs.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MultiFileDialogCallback(IntPtr filePaths, int fileCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

        /// <summary>
        /// Callback signature for single folder selection dialogs.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FolderDialogCallback(IntPtr folderPaths, int folderCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

        /// <summary>
        /// Callback signature for multi-folder selection dialogs.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MultiFolderDialogCallback(IntPtr folderPaths, int folderCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

        /// <summary>
        /// Callback signature for save file dialogs.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SaveFileDialogCallback(string filePath, int fileCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

        // P/Invoke declarations bridging to Objective-C implementation inside the native macOS plugin.
        /// <summary>
        /// Displays a native macOS alert dialog.
        /// </summary>
        /// <param name="title">Window title of the alert.</param>
        /// <param name="message">Primary informative text.</param>
        /// <param name="buttonsJson">JSON describing button layout (<see cref="DialogButtonsWrapper"/>).</param>
        /// <param name="optionsJson">JSON describing alert options (<see cref="DialogOptions"/>).</param>
        /// <param name="callback">Callback fired when the alert completes.</param>
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void showDialog(string title, string? message, string buttonsJson, string optionsJson, DialogCallback callback);

        /// <summary>
        /// Displays a native open file panel (single file selection).
        /// </summary>
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void showFileDialog(string title, string? message, IntPtr allowedContentTypes, int contentTypesCount, string? directoryPath, FileDialogCallback callback);

        /// <summary>
        /// Displays a native open file panel (multiple file selection).
        /// </summary>
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void showMultiFileDialog(string title, string? message, IntPtr allowedContentTypes, int contentTypesCount, string? directoryPath, MultiFileDialogCallback callback);

        /// <summary>
        /// Displays a native folder selection panel (single folder).
        /// </summary>
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void showFolderDialog(string title, string? message, string? directoryPath, FolderDialogCallback callback);

        /// <summary>
        /// Displays a native folder selection panel (multiple folders).
        /// </summary>
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void showMultiFolderDialog(string title, string? message, string? directoryPath, MultiFolderDialogCallback callback);

        /// <summary>
        /// Displays a native save file panel.
        /// </summary>
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void showSaveFileDialog(string title, string? message, string? defaultFileName, IntPtr allowedContentTypes, int contentTypesCount, string? directoryPath, SaveFileDialogCallback callback);

        /// <summary>
        /// Shows a native alert dialog using macOS NSAlert with custom buttons and options.
        /// </summary>
        /// <param name="title">Dialog title (required).</param>
        /// <param name="message">Informative message displayed in the body of the alert.</param>
        /// <param name="buttons">Array of buttons to display in order (must not be null or empty).</param>
        /// <param name="options">Additional configuration such as alert style and suppression button.</param>
        /// <remarks>
        /// Execution: The native dialog is requested on the main thread via <see cref="UnityMainThreadDispatcher"/>.
        /// Result: Completion is surfaced through <see cref="AlertDialogResult"/>. Errors pre-empt invocation with <c>isSuccess=false</c>.
        /// </remarks>
        public void ShowDialog(string title, string? message, DialogButton[] buttons, DialogOptions options)
        {
            Debug.Log($"ShowDialog called with title: {title}, message: {message} buttons: {buttons.Length}, options: {options}");
            // Ensure title is provided
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                AlertDialogResult?.Invoke(null, -1, false, false, false, "Title cannot be null or empty.");
                return;
            }

            // Ensure buttons are provided
            if (buttons == null || buttons.Length == 0)
            {
                Debug.LogError("No buttons provided for the dialog.");
                AlertDialogResult?.Invoke(null, -1, false, false, false, "No buttons provided for the dialog.");
                return;
            }

            // Ensure only one default button is specified
            int defaultButtonCount = 0;
            foreach (var button in buttons)
            {
                if (button.isDefault)
                {
                    defaultButtonCount++;
                }
            }
            if (defaultButtonCount > 1)
            {
                Debug.LogError("Multiple default buttons provided for the dialog.");
                AlertDialogResult?.Invoke(null, -1, false, false, false, "Multiple default buttons provided for the dialog.");
                return;
            }

            ValidateDialogOptions(options);

            // Convert buttons and options to JSON for native consumption
            DialogButtonsWrapper wrapper = new DialogButtonsWrapper { buttons = buttons };
            string buttonsJson = JsonConvert.SerializeObject(wrapper, JsonSettings);
            string optionsJson = JsonConvert.SerializeObject(options, JsonSettings);
            Debug.Log($"ShowDialog title: {title}, message: {message}, buttonsJson: {buttonsJson}, optionsJson: {optionsJson}");

            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                DialogCallback dialogCallback = (buttonTitle, buttonIndex, suppressionButtonState, helpButtonPressed, isSuccess, errorMessage) =>
                {
                    UnityMainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        try
                        {
                            AlertDialogResult?.Invoke(buttonTitle, buttonIndex, suppressionButtonState, helpButtonPressed, isSuccess, errorMessage);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"AlertDialogResult callback error: {ex.Message}");
                        }
                    });
                };
                showDialog(title, message, buttonsJson, optionsJson, dialogCallback);
            });
        }

        /// <summary>
        /// Shows a native open file panel allowing a single file selection.
        /// </summary>
        /// <param name="title">Panel title (required).</param>
        /// <param name="message">Optional message displayed under the title.</param>
        /// <param name="allowedContentTypes">Optional UTI / content type filters (UTF-8 marshalled to native). Pass null for no filtering.</param>
        /// <param name="directoryPath">Optional initial directory path.</param>
        /// <remarks>
        /// Memory: Each content type string is marshalled manually to unmanaged UTF-8 and freed after the callback returns.
        /// Result event: <see cref="FileDialogResult"/>.
        /// </remarks>
        public void ShowFileDialog(string title, string? message = null, string[]? allowedContentTypes = null, string? directoryPath = null)
        {
            var allowedContentTypesList = allowedContentTypes != null ? string.Join(", ", allowedContentTypes) : "null";
            Debug.Log($"ShowFileDialog called with title: {title}, message: {message}, allowedContentTypes: {allowedContentTypesList}, directoryPath: {directoryPath}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                FileDialogResult?.Invoke(null, -1, null, false, false, "Title cannot be null or empty.");
                return;
            }

            IntPtr contentTypesPtr = IntPtr.Zero;
            IntPtr[]? stringPointers = null;
            int contentTypesCount = allowedContentTypes?.Length ?? 0;

            try
            {
                if (contentTypesCount > 0)
                {
                    stringPointers = new IntPtr[contentTypesCount];
                    for (int i = 0; i < contentTypesCount; i++)
                    {
                        if (allowedContentTypes == null || allowedContentTypes[i] == null)
                        {
                            throw new ArgumentNullException($"allowedContentTypes[{i}] is null.");
                        }
                        byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(allowedContentTypes[i]);
                        stringPointers[i] = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                        Marshal.Copy(utf8Bytes, 0, stringPointers[i], utf8Bytes.Length);
                        Marshal.WriteByte(stringPointers[i], utf8Bytes.Length, 0); // Null terminator
                    }
                    contentTypesPtr = Marshal.AllocHGlobal(IntPtr.Size * contentTypesCount);
                    Marshal.Copy(stringPointers, 0, contentTypesPtr, contentTypesCount);
                }

                FileDialogCallback fileDialogCallback = (filePathsPtr, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
                {
                    // Convert unmanaged pointer array to managed string[] immediately
                    string[]? filePaths = null;
                    if (fileCount > 0)
                    {
                        filePaths = new string[fileCount];
                    }
                    if (filePathsPtr != IntPtr.Zero && filePaths != null && fileCount > 0)
                    {
                        IntPtr[] ptrArray = new IntPtr[fileCount];
                        Marshal.Copy(filePathsPtr, ptrArray, 0, fileCount);
                        for (int i = 0; i < fileCount; i++)
                        {
                            filePaths[i] = Marshal.PtrToStringUTF8(ptrArray[i]);
                        }
                    }

                    // Queue managed array for invocation on Unity main thread
                    UnityMainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        try
                        {
                            FileDialogResult?.Invoke(filePaths, fileCount, directoryURL, isCancelled, isSuccess, errorMessage);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"FileDialogResult callback error: {ex.Message}");
                        }
                        finally
                        {
                            // IMPORTANT: Free unmanaged memory allocated for arguments here
                            if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                            if (stringPointers != null)
                            {
                                foreach (var ptr in stringPointers)
                                {
                                    if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                                }
                            }
                            Debug.Log("Memory for allowedContentTypes freed in FileDialog callback.");
                        }
                    });
                };

                showFileDialog(title, message, contentTypesPtr, contentTypesCount, directoryPath, fileDialogCallback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ShowFileDialog error: {ex.Message}");
                // Ensure allocated unmanaged memory is freed even when an error occurs
                if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                if (stringPointers != null)
                {
                    foreach (var ptr in stringPointers)
                    {
                        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                    }
                }
                FileDialogResult?.Invoke(null, -1, null, false, false, $"Internal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a native open file panel allowing multiple file selections.
        /// </summary>
        /// <inheritdoc cref="ShowFileDialog"/>
        /// <remarks>Result event: <see cref="MultiFileDialogResult"/>.</remarks>
        public void ShowMultiFileDialog(string title, string? message = null, string[]? allowedContentTypes = null, string? directoryPath = null)
        {
            var allowedContentTypesList = allowedContentTypes != null ? string.Join(", ", allowedContentTypes) : "null";
            Debug.Log($"ShowMultiFileDialog called with title: {title}, message: {message}, allowedContentTypes: {allowedContentTypesList}, directoryPath: {directoryPath}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                MultiFileDialogResult?.Invoke(null, -1, null, false, false, "Title cannot be null or empty.");
                return;
            }

            IntPtr contentTypesPtr = IntPtr.Zero;
            IntPtr[]? stringPointers = null;
            int contentTypesCount = allowedContentTypes?.Length ?? 0;

            try
            {
                if (contentTypesCount > 0)
                {
                    stringPointers = new IntPtr[contentTypesCount];
                    for (int i = 0; i < contentTypesCount; i++)
                    {
                        if (allowedContentTypes == null || allowedContentTypes[i] == null)
                        {
                            throw new ArgumentNullException($"allowedContentTypes[{i}] is null.");
                        }
                        byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(allowedContentTypes[i]);
                        stringPointers[i] = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                        Marshal.Copy(utf8Bytes, 0, stringPointers[i], utf8Bytes.Length);
                        Marshal.WriteByte(stringPointers[i], utf8Bytes.Length, 0); // Null terminator
                    }
                    contentTypesPtr = Marshal.AllocHGlobal(IntPtr.Size * contentTypesCount);
                    Marshal.Copy(stringPointers, 0, contentTypesPtr, contentTypesCount);
                }

                MultiFileDialogCallback multiFileDialogCallback = (filePathsPtr, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
                {
                    // Convert unmanaged pointer array to managed string[] immediately
                    string[]? filePaths = null;
                    if (fileCount > 0)
                    {
                        filePaths = new string[fileCount];
                    }
                    if (filePathsPtr != IntPtr.Zero && filePaths != null && fileCount > 0)
                    {
                        IntPtr[] ptrArray = new IntPtr[fileCount];
                        Marshal.Copy(filePathsPtr, ptrArray, 0, fileCount);
                        for (int i = 0; i < fileCount; i++)
                        {
                            filePaths[i] = Marshal.PtrToStringUTF8(ptrArray[i]);
                        }
                    }

                    // Queue managed array for invocation on Unity main thread
                    UnityMainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        try
                        {
                            MultiFileDialogResult?.Invoke(filePaths, fileCount, directoryURL, isCancelled, isSuccess, errorMessage);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"MultiFileDialogResult callback error: {ex.Message}");
                        }
                        finally
                        {
                            // IMPORTANT: Free unmanaged memory allocated for arguments here
                            if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                            if (stringPointers != null)
                            {
                                foreach (var ptr in stringPointers)
                                {
                                    if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                                }
                            }
                            Debug.Log("Memory for allowedContentTypes freed in MultiFileDialog callback.");
                        }
                    });
                };

                showMultiFileDialog(title, message, contentTypesPtr, contentTypesCount, directoryPath, multiFileDialogCallback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ShowMultiFileDialog error: {ex.Message}");
                // Ensure allocated unmanaged memory is freed even when an error occurs
                if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                if (stringPointers != null)
                {
                    foreach (var ptr in stringPointers)
                    {
                        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                    }
                }
                MultiFileDialogResult?.Invoke(null, -1, null, false, false, $"Internal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a native folder selection panel allowing the user to pick a single folder.
        /// </summary>
        /// <param name="title">Panel title (required).</param>
        /// <param name="message">Optional informative message.</param>
        /// <param name="directoryPath">Optional initial directory path.</param>
        /// <remarks>Result event: <see cref="FolderDialogResult"/>.</remarks>
        public void ShowFolderDialog(string title, string? message = null, string? directoryPath = null)
        {
            Debug.Log($"ShowFolderDialog called with title: {title}, message: {message}, directoryPath: {directoryPath}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                FolderDialogResult?.Invoke(null, -1, null, false, false, "Title cannot be null or empty.");
                return;
            }

            try
            {
                FolderDialogCallback folderDialogCallback = (folderPathsPtr, folderCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
                {
                    // Convert unmanaged pointer array to managed string[] immediately
                    string[]? folderPaths = null;
                    if (folderCount > 0)
                    {
                        folderPaths = new string[folderCount];
                    }
                    if (folderPathsPtr != IntPtr.Zero && folderPaths != null && folderCount > 0)
                    {
                        IntPtr[] ptrArray = new IntPtr[folderCount];
                        Marshal.Copy(folderPathsPtr, ptrArray, 0, folderCount);

                        for (int i = 0; i < folderCount; i++)
                        {
                            folderPaths[i] = Marshal.PtrToStringUTF8(ptrArray[i]);
                        }
                    }

                    // Queue managed array for invocation on Unity main thread
                    UnityMainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        try
                        {
                            FolderDialogResult?.Invoke(folderPaths, folderCount, directoryURL, isCancelled, isSuccess, errorMessage);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"FolderDialogResult callback error: {ex.Message}");
                        }
                    });
                };

                showFolderDialog(title, message, directoryPath, folderDialogCallback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ShowFolderDialog error: {ex.Message}");
                FolderDialogResult?.Invoke(null, -1, null, false, false, $"Internal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a native folder selection panel allowing the user to pick multiple folders.
        /// </summary>
        /// <inheritdoc cref="ShowFolderDialog"/>
        /// <remarks>Result event: <see cref="MultiFolderDialogResult"/>.</remarks>
        public void ShowMultiFolderDialog(string title, string? message = null, string? directoryPath = null)
        {
            Debug.Log($"ShowMultiFolderDialog called with title: {title}, message: {message}, directoryPath: {directoryPath}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                MultiFolderDialogResult?.Invoke(null, -1, null, false, false, "Title cannot be null or empty.");
                return;
            }

            try
            {
                MultiFolderDialogCallback multiFolderDialogCallback = (folderPathsPtr, folderCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
                {
                    // Convert unmanaged pointer array to managed string[] immediately
                    string[]? folderPaths = null;
                    if (folderCount > 0)
                    {
                        folderPaths = new string[folderCount];
                    }
                    if (folderPathsPtr != IntPtr.Zero && folderPaths != null && folderCount > 0)
                    {
                        IntPtr[] ptrArray = new IntPtr[folderCount];
                        Marshal.Copy(folderPathsPtr, ptrArray, 0, folderCount);

                        for (int i = 0; i < folderCount; i++)
                        {
                            folderPaths[i] = Marshal.PtrToStringUTF8(ptrArray[i]);
                        }
                    }

                    // Queue managed array for invocation on Unity main thread
                    UnityMainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        try
                        {
                            MultiFolderDialogResult?.Invoke(folderPaths, folderCount, directoryURL, isCancelled, isSuccess, errorMessage);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"MultiFolderDialogResult callback error: {ex.Message}");
                        }
                    });
                };

                showMultiFolderDialog(title, message, directoryPath, multiFolderDialogCallback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ShowMultiFolderDialog error: {ex.Message}");
                MultiFolderDialogResult?.Invoke(null, -1, null, false, false, $"Internal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a native save file panel allowing the user to pick a location and optionally a file name.
        /// </summary>
        /// <param name="title">Panel title (required).</param>
        /// <param name="message">Optional informative message.</param>
        /// <param name="defaultFileName">Optional default file name pre-filled in the panel.</param>
        /// <param name="allowedContentTypes">Optional content type (UTI) filters; marshalled manually to unmanaged memory.</param>
        /// <param name="directoryPath">Optional initial directory path.</param>
        /// <remarks>
        /// Memory: Unmanaged buffers for content type filters are always released in the completion callback or error handler.
        /// Result event: <see cref="SaveFileDialogResult"/>.
        /// </remarks>
        public void ShowSaveFileDialog(string title, string? message = null, string? defaultFileName = null, string[]? allowedContentTypes = null, string? directoryPath = null)
        {
            var allowedContentTypesList = allowedContentTypes != null ? string.Join(", ", allowedContentTypes) : "null";
            Debug.Log($"ShowSaveFileDialog called with title: {title}, message: {message}, defaultFileName: {defaultFileName}, allowedContentTypes: {allowedContentTypesList}, directoryPath: {directoryPath}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                SaveFileDialogResult?.Invoke(null, -1, null, false, false, "Title cannot be null or empty.");
                return;
            }

            IntPtr contentTypesPtr = IntPtr.Zero;
            IntPtr[]? stringPointers = null;
            int contentTypesCount = allowedContentTypes?.Length ?? 0;

            try
            {
                if (contentTypesCount > 0)
                {
                    stringPointers = new IntPtr[contentTypesCount];
                    for (int i = 0; i < contentTypesCount; i++)
                    {
                        if (allowedContentTypes == null || allowedContentTypes[i] == null)
                        {
                            throw new ArgumentNullException($"allowedContentTypes[{i}] is null.");
                        }
                        byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(allowedContentTypes[i]);
                        stringPointers[i] = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                        Marshal.Copy(utf8Bytes, 0, stringPointers[i], utf8Bytes.Length);
                        Marshal.WriteByte(stringPointers[i], utf8Bytes.Length, 0); // Null terminator
                    }
                    contentTypesPtr = Marshal.AllocHGlobal(IntPtr.Size * contentTypesCount);
                    Marshal.Copy(stringPointers, 0, contentTypesPtr, contentTypesCount);
                }

                SaveFileDialogCallback saveFileDialogCallback = (filePath, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
                {
                    // The native callback may arrive on a non-Unity thread; dispatch to the Unity main thread for event invocation & memory cleanup.
                    UnityMainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        try
                        {
                            SaveFileDialogResult?.Invoke(filePath, fileCount, directoryURL, isCancelled, isSuccess, errorMessage);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"SaveFileDialogResult callback error: {ex.Message}");
                        }
                        finally
                        {
                            // IMPORTANT: Free unmanaged memory allocated for arguments here
                            if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                            if (stringPointers != null)
                            {
                                foreach (var ptr in stringPointers)
                                {
                                    if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                                }
                            }
                            Debug.Log("Memory for allowedContentTypes freed in SaveFileDialog callback.");
                        }
                    });
                };

                showSaveFileDialog(title, message, defaultFileName, contentTypesPtr, contentTypesCount, directoryPath, saveFileDialogCallback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ShowSaveFileDialog error: {ex.Message}");
                // Ensure allocated unmanaged memory is freed even when an error occurs
                if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                if (stringPointers != null)
                {
                    foreach (var ptr in stringPointers)
                    {
                        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                    }
                }
                SaveFileDialogResult?.Invoke(null, -1, null, false, false, $"Internal error: {ex.Message}");
            }
        }
    }
}
#endif
