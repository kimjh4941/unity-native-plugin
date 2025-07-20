#if UNITY_IOS
using UnityEngine;

public class IosDialogPlugin : MonoBehaviour
{
    void Start()
    {
        // Get an instance of IosDialogManager and register events
        IosDialogManager.Instance.DialogResult += (result, isSuccess, errorMessage) =>
        {
            Debug.Log($"DialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        IosDialogManager.Instance.ConfirmDialogResult += (result, isSuccess, errorMessage) =>
        {
            Debug.Log($"ConfirmDialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        IosDialogManager.Instance.DestructiveDialogResult += (result, isSuccess, errorMessage) =>
        {
            Debug.Log($"DestructiveDialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        IosDialogManager.Instance.ActionSheetResult += (result, isSuccess, errorMessage) =>
        {
            Debug.Log($"ActionSheetResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        IosDialogManager.Instance.TextInputDialogResult += (buttonPressed, inputText, isSuccess, errorMessage) =>
        {
            Debug.Log($"TextInputDialogResult buttonPressed: {buttonPressed}, inputText: {inputText}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        IosDialogManager.Instance.LoginDialogResult += (buttonPressed, username, password, isSuccess, errorMessage) =>
        {
            Debug.Log($"LoginDialogResult buttonPressed: {buttonPressed}, username: {username}, password: {password}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
    }

    void OnDestroy()
    {
        // Unregister events to prevent memory leaks
        if (IosDialogManager.Instance != null)
        {
            IosDialogManager.Instance.DialogResult -= (result, isSuccess, errorMessage) =>
            {
                Debug.Log($"DialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
            };
            IosDialogManager.Instance.ConfirmDialogResult -= (result, isSuccess, errorMessage) =>
            {
                Debug.Log($"ConfirmDialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
            };
            IosDialogManager.Instance.DestructiveDialogResult -= (result, isSuccess, errorMessage) =>
            {
                Debug.Log($"DestructiveDialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
            };
            IosDialogManager.Instance.ActionSheetResult -= (result, isSuccess, errorMessage) =>
            {
                Debug.Log($"ActionSheetResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
            };
            IosDialogManager.Instance.TextInputDialogResult -= (buttonPressed, inputText, isSuccess, errorMessage) =>
            {
                Debug.Log($"TextInputDialogResult buttonPressed: {buttonPressed}, inputText: {inputText}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
            };
            IosDialogManager.Instance.LoginDialogResult -= (buttonPressed, username, password, isSuccess, errorMessage) =>
            {
                Debug.Log($"LoginDialogResult buttonPressed: {buttonPressed}, username: {username}, password: {password}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
            };
        }
    }

    /// <summary>
    /// Show basic alert dialog
    /// </summary>
    public void ShowDialog()
    {
        IosDialogManager.Instance.ShowDialog("Hello from Unity", "This is a native iOS dialog!", "OK");
    }

    /// <summary>
    /// Show confirmation dialog with OK/Cancel buttons
    /// </summary>
    public void ShowConfirmDialog()
    {
        IosDialogManager.Instance.ShowConfirmDialog("Confirm Action", "Are you sure you want to proceed?", "Yes", "No");
    }

    /// <summary>
    /// Show destructive dialog for delete operations
    /// </summary>
    public void ShowDestructiveDialog()
    {
        IosDialogManager.Instance.ShowDestructiveDialog("Delete File", "This action cannot be undone. Are you sure?", "Delete", "Cancel");
    }

    /// <summary>
    /// Show action sheet with multiple options
    /// </summary>
    public void ShowActionSheet()
    {
        string[] options = { "Camera", "Photo Library", "Documents" };
        IosDialogManager.Instance.ShowActionSheet("Select Source", "Choose where to get the file from", options, "Cancel");
    }

    /// <summary>
    /// Show text input dialog
    /// </summary>
    public void ShowTextInputDialog()
    {
        IosDialogManager.Instance.ShowTextInputDialog("Enter Name", "Please enter your name", "Your name here", "OK", "Cancel", false);
    }

    /// <summary>
    /// Show login dialog with username and password fields
    /// </summary>
    public void ShowLoginDialog()
    {
        IosDialogManager.Instance.ShowLoginDialog("Login Required", "Please enter your credentials", "Username", "Password", "Login", "Cancel", false);
    }
}
#endif
