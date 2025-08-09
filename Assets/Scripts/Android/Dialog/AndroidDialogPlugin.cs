#if UNITY_ANDROID
using UnityEngine;

public class AndroidDialogPlugin : MonoBehaviour
{
    void Start()
    {
        // Get an instance of AndroidDialogManager and register events
        AndroidDialogManager.Instance.DialogResult += (buttonText, isSuccessful, errorMessage) =>
        {
            Debug.Log($"DialogResult result: {buttonText}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
        };
        AndroidDialogManager.Instance.ConfirmDialogResult += (buttonText, isSuccessful, errorMessage) =>
        {
            Debug.Log($"ConfirmDialogResult result: {buttonText}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
        };
        AndroidDialogManager.Instance.SingleChoiceItemDialogResult += (buttonText, checkedItem, isSuccessful, errorMessage) =>
        {
            Debug.Log($"SingleChoiceItemDialogResult buttonPressed: {buttonText}, checkedItem: {checkedItem}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
        };
        AndroidDialogManager.Instance.MultiChoiceItemDialogResult += (buttonText, checkedItems, isSuccessful, errorMessage) =>
        {
            Debug.Log($"MultiChoiceItemDialogResult buttonPressed: {buttonText}, checkedItems: {string.Join(",", checkedItems)}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
        };
        AndroidDialogManager.Instance.TextInputDialogResult += (buttonText, inputText, isSuccessful, errorMessage) =>
        {
            Debug.Log($"TextInputDialogResult buttonPressed: {buttonText}, inputText: {inputText}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
        };
        AndroidDialogManager.Instance.LoginDialogResult += (buttonText, username, password, isSuccessful, errorMessage) =>
        {
            Debug.Log($"LoginDialogResult buttonPressed: {buttonText}, username: {username}, password: {password}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
        };
    }

    void OnDestroy()
    {
        // Unregister events to prevent memory leaks
        if (AndroidDialogManager.Instance != null)
        {
            AndroidDialogManager.Instance.DialogResult -= (buttonText, isSuccessful, errorMessage) =>
            {
                Debug.Log($"DialogResult result: {buttonText}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
            };
            AndroidDialogManager.Instance.ConfirmDialogResult -= (buttonText, isSuccessful, errorMessage) =>
            {
                Debug.Log($"ConfirmDialogResult result: {buttonText}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
            };
            AndroidDialogManager.Instance.SingleChoiceItemDialogResult -= (buttonText, checkedItem, isSuccessful, errorMessage) =>
            {
                Debug.Log($"SingleChoiceItemDialogResult buttonPressed: {buttonText}, checkedItem: {checkedItem}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
            };
            AndroidDialogManager.Instance.MultiChoiceItemDialogResult -= (buttonText, checkedItems, isSuccessful, errorMessage) =>
            {
                Debug.Log($"MultiChoiceItemDialogResult buttonPressed: {buttonText}, checkedItems: {string.Join(",", checkedItems)}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
            };
            AndroidDialogManager.Instance.TextInputDialogResult -= (buttonText, inputText, isSuccessful, errorMessage) =>
            {
                Debug.Log($"TextInputDialogResult buttonPressed: {buttonText}, inputText: {inputText}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
            };
            AndroidDialogManager.Instance.LoginDialogResult -= (buttonText, username, password, isSuccessful, errorMessage) =>
            {
                Debug.Log($"LoginDialogResult buttonPressed: {buttonText}, username: {username}, password: {password}, isSuccess: {isSuccessful}, errorMessage: {errorMessage}");
            };
        }
    }

    /// <summary>
    /// Show basic alert dialog
    /// </summary>
    public void ShowDialog()
    {
        Debug.Log("ShowDialog called");
        AndroidDialogManager.Instance.ShowDialog(
            "Hello from Unity",
            "This is a native Android dialog!",
            "OK", // Button text
            false, // Cancelable on touch outside
            false  // Cancelable
        );
    }

    /// <summary>
    /// Show confirmation dialog
    /// </summary>
    public void ShowConfirmDialog()
    {
        Debug.Log("ShowConfirmDialog called");
        AndroidDialogManager.Instance.ShowConfirmDialog(
            "Confirmation",
            "Do you want to proceed with this action?",
            "No",
            "Yes",
            false, // Cancelable on touch outside
            false  // Cancelable
        );
    }

    /// <summary>
    /// Show single choice dialog
    /// </summary>
    public void ShowSingleChoiceItemDialog()
    {
        Debug.Log("ShowSingleChoiceItemDialog called");
        string[] options = { "Option 1", "Option 2", "Option 3" };
        AndroidDialogManager.Instance.ShowSingleChoiceItemDialog(
            "Please select one",
            options,
            0, // Default selection
            "Cancel",
            "OK",
            false, // Cancelable on touch outside
            false  // Cancelable
        );
    }

    /// <summary>
    /// Show multi choice dialog
    /// </summary>
    public void ShowMultiChoiceItemDialog()
    {
        Debug.Log("ShowMultiChoiceItemDialog called");
        string[] items = { "Item 1", "Item 2", "Item 3", "Item 4" };
        bool[] checkedItems = { false, true, false, true }; // Default selection state
        AndroidDialogManager.Instance.ShowMultiChoiceItemDialog(
            "Multiple Selection",
            items,
            checkedItems,
            "Cancel",
            "OK",
            false, // Cancelable on touch outside
            false  // Cancelable
        );
    }

    /// <summary>
    /// Show text input dialog
    /// </summary>
    public void ShowTextInputDialog()
    {
        Debug.Log("ShowTextInputDialog called");
        AndroidDialogManager.Instance.ShowTextInputDialog(
            "Text Input",
            "Please enter your name",
            "Enter here...", // hint
            "Cancel",
            "OK",
            true,  // OK button disabled when empty
            false, // Cancelable on touch outside
            false  // Cancelable
        );
    }

    /// <summary>
    /// Show login dialog
    /// </summary>
    public void ShowLoginDialog()
    {
        Debug.Log("ShowLoginDialog called");
        AndroidDialogManager.Instance.ShowLoginDialog(
            "Login",
            "Please enter your credentials",
            "Username",
            "Password",
            "Cancel",
            "Login",
            true,  // Login button disabled when empty
            false, // Cancelable on touch outside
            false  // Cancelable
        );
    }
}
#endif
