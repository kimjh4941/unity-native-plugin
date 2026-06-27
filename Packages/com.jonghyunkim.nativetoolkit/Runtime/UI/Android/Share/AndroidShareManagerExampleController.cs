#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.IO;
using JonghyunKim.NativeToolkit.Runtime.Share;
using UnityEngine;
using UnityEngine.UIElements;

public class AndroidShareManagerExampleController : MonoBehaviour
{
    private const string LogTag = "AndroidShareManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private const string SampleDirectShareTargetId = "native_toolkit_sample_target";

    private const string SampleChooserActionIdSave =
        "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_SAVE";

    private const string SampleChooserActionIdOpen =
        "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_OPEN";

    private Label? _resultLabel;
    private Label? _callbackLabel;
    private Label? _chooserActionLabel;

    private Button? _homeButton;
    private Button? _shareTextButton;
    private Button? _shareUrlButton;
    private Button? _shareCustomActionButton;
    private Button? _shareWithSubjectTitleButton;
    private Button? _shareRichPreviewButton;
    private Button? _shareImageButton;
    private Button? _shareImagesButton;
    private Button? _shareFileButton;
    private Button? _shareFilesButton;
    private Button? _registerDirectShareTargetButton;
    private Button? _removeDirectShareTargetButton;
    private Button? _shareWithCallbackButton;
    private Button? _cancelPendingCallbackButton;
    private Button? _shareInvalidFileButton;

    private void Awake()
    {
        Debug.Log($"[{LogTag}][{nameof(Awake)}]");
    }

    private void Start()
    {
        Debug.Log($"[{LogTag}][{nameof(Start)}]");
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError($"[{LogTag}][{nameof(Start)}] UIDocument component not found.");
            return;
        }

        InitializeUI();
    }

    private void OnEnable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnEnable)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidShareManager.Instance.ShareOperationCompleted += OnShareOperationCompleted;
        AndroidShareManager.Instance.ShareCallbackReceived += OnShareCallbackReceived;
        AndroidShareManager.Instance.ShareChooserActionTapped += OnShareChooserActionTapped;
#endif
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidShareManager.Instance.ShareOperationCompleted -= OnShareOperationCompleted;
        AndroidShareManager.Instance.ShareCallbackReceived -= OnShareCallbackReceived;
        AndroidShareManager.Instance.ShareChooserActionTapped -= OnShareChooserActionTapped;
        AndroidShareManager.Instance.CancelPendingShareCallback();
#endif
    }

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_shareTextButton != null) _shareTextButton.clicked -= OnShareTextClicked;
        if (_shareUrlButton != null) _shareUrlButton.clicked -= OnShareUrlClicked;
        if (_shareCustomActionButton != null) _shareCustomActionButton.clicked -= OnShareCustomActionClicked;
        if (_shareWithSubjectTitleButton != null) _shareWithSubjectTitleButton.clicked -= OnShareWithSubjectTitleClicked;
        if (_shareRichPreviewButton != null) _shareRichPreviewButton.clicked -= OnShareRichPreviewClicked;
        if (_shareImageButton != null) _shareImageButton.clicked -= OnShareImageClicked;
        if (_shareImagesButton != null) _shareImagesButton.clicked -= OnShareImagesClicked;
        if (_shareFileButton != null) _shareFileButton.clicked -= OnShareFileClicked;
        if (_shareFilesButton != null) _shareFilesButton.clicked -= OnShareFilesClicked;
        if (_registerDirectShareTargetButton != null) _registerDirectShareTargetButton.clicked -= OnRegisterDirectShareTargetClicked;
        if (_removeDirectShareTargetButton != null) _removeDirectShareTargetButton.clicked -= OnRemoveDirectShareTargetClicked;
        if (_shareWithCallbackButton != null) _shareWithCallbackButton.clicked -= OnShareWithCallbackClicked;
        if (_cancelPendingCallbackButton != null) _cancelPendingCallbackButton.clicked -= OnCancelPendingCallbackClicked;
        if (_shareInvalidFileButton != null) _shareInvalidFileButton.clicked -= OnShareInvalidFileClicked;
    }

    private void InitializeUI()
    {
        Debug.Log($"[{LogTag}][{nameof(InitializeUI)}]");
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError($"[{LogTag}][{nameof(InitializeUI)}] rootVisualElement is null.");
            return;
        }

        _resultLabel = root.Q<Label>("ResultTextBlock");
        _callbackLabel = root.Q<Label>("CallbackTextBlock");
        _chooserActionLabel = root.Q<Label>("ChooserActionTextBlock");

        _homeButton = root.Q<Button>("HomeButton");
        _shareTextButton = root.Q<Button>("ShareTextButton");
        _shareUrlButton = root.Q<Button>("ShareUrlButton");
        _shareCustomActionButton = root.Q<Button>("ShareCustomActionButton");
        _shareWithSubjectTitleButton = root.Q<Button>("ShareWithSubjectTitleButton");
        _shareRichPreviewButton = root.Q<Button>("ShareRichPreviewButton");
        _shareImageButton = root.Q<Button>("ShareImageButton");
        _shareImagesButton = root.Q<Button>("ShareImagesButton");
        _shareFileButton = root.Q<Button>("ShareFileButton");
        _shareFilesButton = root.Q<Button>("ShareFilesButton");
        _registerDirectShareTargetButton = root.Q<Button>("RegisterDirectShareTargetButton");
        _removeDirectShareTargetButton = root.Q<Button>("RemoveDirectShareTargetButton");
        _shareWithCallbackButton = root.Q<Button>("ShareWithCallbackButton");
        _cancelPendingCallbackButton = root.Q<Button>("CancelPendingCallbackButton");
        _shareInvalidFileButton = root.Q<Button>("ShareInvalidFileButton");

        if (_homeButton != null) _homeButton.clicked += OnHomeClicked;
        if (_shareTextButton != null) _shareTextButton.clicked += OnShareTextClicked;
        if (_shareUrlButton != null) _shareUrlButton.clicked += OnShareUrlClicked;
        if (_shareCustomActionButton != null) _shareCustomActionButton.clicked += OnShareCustomActionClicked;
        if (_shareWithSubjectTitleButton != null) _shareWithSubjectTitleButton.clicked += OnShareWithSubjectTitleClicked;
        if (_shareRichPreviewButton != null) _shareRichPreviewButton.clicked += OnShareRichPreviewClicked;
        if (_shareImageButton != null) _shareImageButton.clicked += OnShareImageClicked;
        if (_shareImagesButton != null) _shareImagesButton.clicked += OnShareImagesClicked;
        if (_shareFileButton != null) _shareFileButton.clicked += OnShareFileClicked;
        if (_shareFilesButton != null) _shareFilesButton.clicked += OnShareFilesClicked;
        if (_registerDirectShareTargetButton != null) _registerDirectShareTargetButton.clicked += OnRegisterDirectShareTargetClicked;
        if (_removeDirectShareTargetButton != null) _removeDirectShareTargetButton.clicked += OnRemoveDirectShareTargetClicked;
        if (_shareWithCallbackButton != null) _shareWithCallbackButton.clicked += OnShareWithCallbackClicked;
        if (_cancelPendingCallbackButton != null) _cancelPendingCallbackButton.clicked += OnCancelPendingCallbackClicked;
        if (_shareInvalidFileButton != null) _shareInvalidFileButton.clicked += OnShareInvalidFileClicked;
    }

    private void OnHomeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHomeClicked)}]");
        if (uiDocument != null)
        {
            NativeToolkitSampleNavigator.ShowTopMenu(uiDocument);
        }
    }

    private void OnShareTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareTextClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requested: Share plain text");
        AndroidShareManager.Instance.ShareText(new ShareTextPayload
        {
            text = "Hello from Unity! This is a plain text share sample."
        });
#else
        SetResult("Android device only. Run this sample on Android to share text.");
#endif
    }

    private void OnShareUrlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareUrlClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requested: Share URL");
        AndroidShareManager.Instance.ShareText(new ShareTextPayload
        {
            text = "https://unity.com",
            mimeType = "text/plain"
        });
#else
        SetResult("Android device only. Run this sample on Android to share a URL.");
#endif
    }

    private void OnShareCustomActionClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareCustomActionClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requested: Share text with custom chooser action");
        if (_chooserActionLabel != null)
        {
            _chooserActionLabel.text = "Waiting for custom action tap...";
        }

        string iconBase64;
        try
        {
            iconBase64 = CreateSampleIconBase64();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnShareCustomActionClicked)}] Icon creation failed: {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            return;
        }

        AndroidShareManager.Instance.ShareText(new ShareTextPayload
        {
            text = "Sharing with custom chooser actions (Android 14 / API 34+ only).",
            chooserActions = new[]
            {
                new ChooserActionPayload
                {
                    label = "Save",
                    iconBase64 = iconBase64,
                    intentAction = SampleChooserActionIdSave
                },
                new ChooserActionPayload
                {
                    label = "Open",
                    iconBase64 = iconBase64,
                    intentAction = SampleChooserActionIdOpen
                }
            }
        });
#else
        SetResult("Android device only. Run this sample on Android 14 (API 34) or later to use custom chooser actions.");
#endif
    }

    private void OnShareWithSubjectTitleClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareWithSubjectTitleClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requested: Share with subject and title");
        AndroidShareManager.Instance.ShareText(new ShareTextPayload
        {
            text = "This is a share sample with subject and title from Unity.",
            title = "Unity Share Sample",
            subject = "Sample Subject",
            mimeType = "text/plain"
        });
#else
        SetResult("Android device only. Run this sample on Android to share with subject and title.");
#endif
    }

    private void OnShareRichPreviewClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareRichPreviewClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        string thumbnailPath;
        try
        {
            thumbnailPath = CreateSampleImage("share_preview_thumbnail.png");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnShareRichPreviewClicked)}] Image creation failed: {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            return;
        }

        SetResult("Requested: Share with rich preview");
        AndroidShareManager.Instance.ShareText(new ShareTextPayload
        {
            text = "Check out this rich preview share from Unity!",
            previewTitle = "Unity Rich Preview Sample",
            previewThumbnailPath = thumbnailPath
        });
#else
        SetResult("Android device only. Run this sample on Android to share with rich preview.");
#endif
    }

    private void OnShareImageClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareImageClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        string imagePath;
        try
        {
            imagePath = CreateSampleImage("share_sample_image.png");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnShareImageClicked)}] Image creation failed: {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            return;
        }

        if (!File.Exists(imagePath))
        {
            SetResult($"File preparation failed: file not found at {imagePath}");
            return;
        }

        SetResult("Requested: Share image");
        AndroidShareManager.Instance.ShareImage(new ShareImagePayload
        {
            filePath = imagePath,
            mimeType = "image/png"
        });
#else
        SetResult("Android device only. Run this sample on Android to share an image.");
#endif
    }

    private void OnShareImagesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareImagesClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        string imagePath1;
        string imagePath2;
        try
        {
            imagePath1 = CreateSampleImage("share_sample_image_1.png");
            imagePath2 = CreateSampleImage("share_sample_image_2.png");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnShareImagesClicked)}] Image creation failed: {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            return;
        }

        if (!File.Exists(imagePath1) || !File.Exists(imagePath2))
        {
            SetResult("File preparation failed: one or more image files not found.");
            return;
        }

        SetResult("Requested: Share multiple images");
        AndroidShareManager.Instance.ShareImages(new ShareImagesPayload
        {
            filePaths = new[] { imagePath1, imagePath2 }
        });
#else
        SetResult("Android device only. Run this sample on Android to share multiple images.");
#endif
    }

    private void OnShareFileClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareFileClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        string filePath;
        try
        {
            filePath = CreateSampleTextFile("share_sample_file.txt", "This is a sample text file shared from Unity Native Toolkit.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnShareFileClicked)}] File creation failed: {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            return;
        }

        if (!File.Exists(filePath))
        {
            SetResult($"File preparation failed: file not found at {filePath}");
            return;
        }

        SetResult("Requested: Share file");
        AndroidShareManager.Instance.ShareFile(new ShareFilePayload
        {
            filePath = filePath
        });
#else
        SetResult("Android device only. Run this sample on Android to share a file.");
#endif
    }

    private void OnShareFilesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareFilesClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        string filePath1;
        string filePath2;
        try
        {
            filePath1 = CreateSampleTextFile("share_sample_file_1.txt", "Sample file 1 from Unity Native Toolkit.");
            filePath2 = CreateSampleTextFile("share_sample_file_2.txt", "Sample file 2 from Unity Native Toolkit.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnShareFilesClicked)}] File creation failed: {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            return;
        }

        if (!File.Exists(filePath1) || !File.Exists(filePath2))
        {
            SetResult("File preparation failed: one or more files not found.");
            return;
        }

        SetResult("Requested: Share multiple files");
        AndroidShareManager.Instance.ShareFiles(new ShareFilesPayload
        {
            filePaths = new[] { filePath1, filePath2 }
        });
#else
        SetResult("Android device only. Run this sample on Android to share multiple files.");
#endif
    }

    private void OnRegisterDirectShareTargetClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRegisterDirectShareTargetClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        string iconBase64;
        try
        {
            iconBase64 = CreateSampleIconBase64();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnRegisterDirectShareTargetClicked)}] Icon creation failed: {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            return;
        }

        SetResult("Requested: Register Direct Share target");
        AndroidShareManager.Instance.RegisterDirectShareTarget(new DirectShareTargetPayload
        {
            id = SampleDirectShareTargetId,
            label = "Unity Sample Target",
            iconBase64 = iconBase64
        });
#else
        SetResult("Android device only. Run this sample on Android to register a Direct Share target.");
#endif
    }

    private void OnRemoveDirectShareTargetClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveDirectShareTargetClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requested: Remove Direct Share targets");
        AndroidShareManager.Instance.RemoveDirectShareTargets(new RemoveDirectShareTargetsPayload
        {
            ids = new[] { SampleDirectShareTargetId }
        });
#else
        SetResult("Android device only. Run this sample on Android to remove Direct Share targets.");
#endif
    }

    private void OnShareWithCallbackClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareWithCallbackClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requested: Share with callback");
        if (_callbackLabel != null)
        {
            _callbackLabel.text = "Waiting for app selection...";
        }

        AndroidShareManager.Instance.ShareWithCallback(
            new ShareTextPayload
            {
                text = "Share with callback sample from Unity. Select an app to receive the selection result."
            },
            onStarted: result =>
            {
                string status = result.IsSuccess ? "Success" : "Failed";
                string msg = $"[onStarted] ShareWithCallback: {status}";
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    msg += $"\nError: {result.ErrorMessage}";
                }
                SetResult(msg);
            },
            onSelected: result =>
            {
                string pkg = result.SelectedPackageName ?? "(unknown)";
                if (_callbackLabel != null)
                {
                    _callbackLabel.text = $"[onSelected] Selected: {pkg}";
                }
                Debug.Log($"[{LogTag}] onSelected: {pkg}");
            });
#else
        SetResult("Android device only. Run this sample on Android to share with callback.");
#endif
    }

    private void OnCancelPendingCallbackClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelPendingCallbackClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requested: Cancel pending callback");
        AndroidShareManager.Instance.CancelPendingShareCallback();
#else
        SetResult("Android device only. Run this sample on Android to cancel a pending share callback.");
#endif
    }

    private void OnShareInvalidFileClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareInvalidFileClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requested: Share invalid file (error handling demo)");
        AndroidShareManager.Instance.ShareFile(new ShareFilePayload
        {
            filePath = "/invalid/path/that/does/not/exist/sample.txt"
        });
#else
        SetResult("Android device only. Run this sample on Android to test error handling for invalid file paths.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnShareOperationCompleted(ShareOperationResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareOperationCompleted)}] operation: {result.Operation}, isSuccess: {result.IsSuccess}");
        string status = result.IsSuccess ? "Success" : "Failed";
        string msg = $"[event] {GetOperationTitle(result.Operation)}: {status}";
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            msg += $"\nError: {result.ErrorMessage}";
        }
        SetResult(msg);
    }

    private void OnShareCallbackReceived(ShareCallbackResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareCallbackReceived)}] operation: {result.Operation}, package: {result.SelectedPackageName}");
        string pkg = result.SelectedPackageName ?? "(unknown)";
        if (_callbackLabel != null)
        {
            _callbackLabel.text = $"[event] ShareCallback: Selected {pkg}";
        }
    }

    private void OnShareChooserActionTapped(ShareChooserActionResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareChooserActionTapped)}] actionId: {result.ActionId}");
        if (_chooserActionLabel != null)
        {
            _chooserActionLabel.text = $"[event] ChooserAction: {result.ActionId}";
        }
    }

    private static string GetOperationTitle(string operation)
    {
        return operation switch
        {
            AndroidShareManager.OperationShareText => "ShareText",
            AndroidShareManager.OperationShareImage => "ShareImage",
            AndroidShareManager.OperationShareImages => "ShareImages",
            AndroidShareManager.OperationShareFile => "ShareFile",
            AndroidShareManager.OperationShareFiles => "ShareFiles",
            AndroidShareManager.OperationRegisterDirectShareTarget => "RegisterDirectShareTarget",
            AndroidShareManager.OperationRemoveDirectShareTargets => "RemoveDirectShareTargets",
            AndroidShareManager.OperationShareWithCallback => "ShareWithCallback",
            AndroidShareManager.OperationCancelPendingShareCallback => "CancelPendingShareCallback",
            _ => operation
        };
    }
#endif

    private string CreateSampleImage(string fileName)
    {
        var texture = new Texture2D(128, 128);
        try
        {
            var pixels = new Color[128 * 128];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0.12f, 0.53f, 0.90f);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            byte[] pngBytes = texture.EncodeToPNG();
            // Use persistentDataPath (external-files-path) which is covered by the native FileProvider.
            // temporaryCachePath maps to external cache dir which is not in the FileProvider config.
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(path, pngBytes);
            return path;
        }
        finally
        {
            Destroy(texture);
        }
    }

    private static string CreateSampleTextFile(string fileName, string content)
    {
        // Use persistentDataPath (external-files-path) which is covered by the native FileProvider.
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateSampleIconBase64()
    {
        var texture = new Texture2D(64, 64);
        try
        {
            var pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0.12f, 0.53f, 0.90f);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            byte[] pngBytes = texture.EncodeToPNG();
            return Convert.ToBase64String(pngBytes);
        }
        finally
        {
            Destroy(texture);
        }
    }

    private void SetResult(string message)
    {
        Debug.Log($"[{LogTag}][{nameof(SetResult)}] {message}");
        if (_resultLabel != null)
        {
            _resultLabel.text = message;
        }
    }
}
#endif
