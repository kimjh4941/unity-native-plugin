#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System;
using System.IO;
using JonghyunKim.NativeToolkit.Runtime.Share;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Example controller demonstrating the iOS system share sheet via <see cref="IosShareManager"/>.
/// </summary>
public class IosShareManagerExampleController : MonoBehaviour
{
    private const string LogTag = "IosShareManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private const string SampleUrl = "https://unity.com";
    private const string SampleInvalidUrl = "not a valid url";
    private const string MissingFilePath = "/nonexistent/share-missing.txt";
    private const string MissingImagePath = "/nonexistent/share-missing.png";
    private const string ExcludedActivityCopyToPasteboard = "com.apple.UIKit.activity.CopyToPasteboard";
    private const string ExcludedActivityPostToFacebook = "com.apple.UIKit.activity.PostToFacebook";

    private Label? _resultLabel;
    private string _pendingOperationTitle = string.Empty;

    private Button? _homeButton;
    private Button? _shareTextButton;
    private Button? _shareUrlButton;
    private Button? _shareUrlPreviewButton;
    private Button? _shareImageButton;
    private Button? _shareImagesButton;
    private Button? _shareFileButton;
    private Button? _shareFilesButton;
    private Button? _shareMultipleButton;
    private Button? _shareWithSubjectButton;
    private Button? _shareExcludingActivitiesButton;
    private Button? _shareEmptyButton;
    private Button? _shareInvalidUrlButton;
    private Button? _shareMissingFileButton;
    private Button? _shareMissingImageButton;

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
        IosShareManager.Instance.ShareCompleted += OnShareCompleted;
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");
        IosShareManager.Instance.ShareCompleted -= OnShareCompleted;
    }

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_shareTextButton != null) _shareTextButton.clicked -= OnShareTextClicked;
        if (_shareUrlButton != null) _shareUrlButton.clicked -= OnShareUrlClicked;
        if (_shareUrlPreviewButton != null) _shareUrlPreviewButton.clicked -= OnShareUrlPreviewClicked;
        if (_shareImageButton != null) _shareImageButton.clicked -= OnShareImageClicked;
        if (_shareImagesButton != null) _shareImagesButton.clicked -= OnShareImagesClicked;
        if (_shareFileButton != null) _shareFileButton.clicked -= OnShareFileClicked;
        if (_shareFilesButton != null) _shareFilesButton.clicked -= OnShareFilesClicked;
        if (_shareMultipleButton != null) _shareMultipleButton.clicked -= OnShareMultipleClicked;
        if (_shareWithSubjectButton != null) _shareWithSubjectButton.clicked -= OnShareWithSubjectClicked;
        if (_shareExcludingActivitiesButton != null) _shareExcludingActivitiesButton.clicked -= OnShareExcludingActivitiesClicked;
        if (_shareEmptyButton != null) _shareEmptyButton.clicked -= OnShareEmptyClicked;
        if (_shareInvalidUrlButton != null) _shareInvalidUrlButton.clicked -= OnShareInvalidUrlClicked;
        if (_shareMissingFileButton != null) _shareMissingFileButton.clicked -= OnShareMissingFileClicked;
        if (_shareMissingImageButton != null) _shareMissingImageButton.clicked -= OnShareMissingImageClicked;
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

        _homeButton = root.Q<Button>("HomeButton");
        _shareTextButton = root.Q<Button>("ShareTextButton");
        _shareUrlButton = root.Q<Button>("ShareUrlButton");
        _shareUrlPreviewButton = root.Q<Button>("ShareUrlPreviewButton");
        _shareImageButton = root.Q<Button>("ShareImageButton");
        _shareImagesButton = root.Q<Button>("ShareImagesButton");
        _shareFileButton = root.Q<Button>("ShareFileButton");
        _shareFilesButton = root.Q<Button>("ShareFilesButton");
        _shareMultipleButton = root.Q<Button>("ShareMultipleButton");
        _shareWithSubjectButton = root.Q<Button>("ShareWithSubjectButton");
        _shareExcludingActivitiesButton = root.Q<Button>("ShareExcludingActivitiesButton");
        _shareEmptyButton = root.Q<Button>("ShareEmptyButton");
        _shareInvalidUrlButton = root.Q<Button>("ShareInvalidUrlButton");
        _shareMissingFileButton = root.Q<Button>("ShareMissingFileButton");
        _shareMissingImageButton = root.Q<Button>("ShareMissingImageButton");

        if (_homeButton != null) _homeButton.clicked += OnHomeClicked;
        if (_shareTextButton != null) _shareTextButton.clicked += OnShareTextClicked;
        if (_shareUrlButton != null) _shareUrlButton.clicked += OnShareUrlClicked;
        if (_shareUrlPreviewButton != null) _shareUrlPreviewButton.clicked += OnShareUrlPreviewClicked;
        if (_shareImageButton != null) _shareImageButton.clicked += OnShareImageClicked;
        if (_shareImagesButton != null) _shareImagesButton.clicked += OnShareImagesClicked;
        if (_shareFileButton != null) _shareFileButton.clicked += OnShareFileClicked;
        if (_shareFilesButton != null) _shareFilesButton.clicked += OnShareFilesClicked;
        if (_shareMultipleButton != null) _shareMultipleButton.clicked += OnShareMultipleClicked;
        if (_shareWithSubjectButton != null) _shareWithSubjectButton.clicked += OnShareWithSubjectClicked;
        if (_shareExcludingActivitiesButton != null) _shareExcludingActivitiesButton.clicked += OnShareExcludingActivitiesClicked;
        if (_shareEmptyButton != null) _shareEmptyButton.clicked += OnShareEmptyClicked;
        if (_shareInvalidUrlButton != null) _shareInvalidUrlButton.clicked += OnShareInvalidUrlClicked;
        if (_shareMissingFileButton != null) _shareMissingFileButton.clicked += OnShareMissingFileClicked;
        if (_shareMissingImageButton != null) _shareMissingImageButton.clicked += OnShareMissingImageClicked;
    }

    // ── Button Handlers ──────────────────────────────────────────────────────

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
        SetPendingOperation("Share Text");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Text("Shared from Unity Native Toolkit") }
        });
    }

    private void OnShareUrlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareUrlClicked)}]");
        SetPendingOperation("Share URL");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Url(SampleUrl) }
        });
    }

    private void OnShareUrlPreviewClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareUrlPreviewClicked)}]");
        SetPendingOperation("Share URL with Preview");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Url(SampleUrl) },
            previewTitle = "Unity"
        });
    }

    private void OnShareImageClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareImageClicked)}]");
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

        SetPendingOperation("Share Image");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Image(imagePath) }
        });
    }

    private void OnShareImagesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareImagesClicked)}]");
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

        SetPendingOperation("Share Multiple Images");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Image(imagePath1), IosShareItem.Image(imagePath2) }
        });
    }

    private void OnShareFileClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareFileClicked)}]");
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

        SetPendingOperation("Share File");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.File(filePath) }
        });
    }

    private void OnShareFilesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareFilesClicked)}]");
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

        SetPendingOperation("Share Multiple Files");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.File(filePath1), IosShareItem.File(filePath2) }
        });
    }

    private void OnShareMultipleClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareMultipleClicked)}]");
        SetPendingOperation("Share Text + URL");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Text("Check this out"), IosShareItem.Url(SampleUrl) }
        });
    }

    private void OnShareWithSubjectClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareWithSubjectClicked)}]");
        SetPendingOperation("Share with Subject");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Text("Body text") },
            subject = "Sample Subject"
        });
    }

    private void OnShareExcludingActivitiesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareExcludingActivitiesClicked)}]");
        SetPendingOperation("Share Excluding Activities");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Url(SampleUrl) },
            excludedActivityTypes = new[] { ExcludedActivityCopyToPasteboard, ExcludedActivityPostToFacebook }
        });
    }

    private void OnShareEmptyClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareEmptyClicked)}]");
        SetPendingOperation("Share Empty (error)");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = Array.Empty<IosShareItem>()
        });
    }

    private void OnShareInvalidUrlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareInvalidUrlClicked)}]");
        SetPendingOperation("Share Invalid URL");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Url(SampleInvalidUrl) }
        });
    }

    private void OnShareMissingFileClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareMissingFileClicked)}]");
        SetPendingOperation("Share Missing File");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.File(MissingFilePath) }
        });
    }

    private void OnShareMissingImageClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareMissingImageClicked)}]");
        SetPendingOperation("Share Missing Image");
        IosShareManager.Instance.Share(new IosShareContentPayload
        {
            items = new[] { IosShareItem.Image(MissingImagePath) }
        });
    }

    // ── Event Handlers ───────────────────────────────────────────────────────

    private void OnShareCompleted(IosShareResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareCompleted)}] isSuccess: {result.IsSuccess}, completed: {result.Completed}, activityType: {result.ActivityType}, errorMessage: {result.ErrorMessage}");
        string title = !string.IsNullOrEmpty(_pendingOperationTitle) ? _pendingOperationTitle : "Share";
        _pendingOperationTitle = string.Empty;

        if (!result.IsSuccess)
        {
            SetResult($"[{title}] Error: {result.ErrorMessage}");
            return;
        }

        string detail = result.Completed
            ? $"[{title}] completed=true, activityType={result.ActivityType ?? "nil"}"
            : $"[{title}] completed=false (cancelled)";
        SetResult(detail);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetPendingOperation(string title)
    {
        _pendingOperationTitle = title;
        SetResult($"Requested: {title}");
    }

    private string CreateSampleImage(string fileName)
    {
        Debug.Log($"[{LogTag}][{nameof(CreateSampleImage)}] fileName: {fileName}");
        var source = Resources.Load<Texture2D>("Images/share_sample_image");
        if (source == null)
            throw new InvalidOperationException("share_sample_image not found in Resources.");

        // EncodeToPNG requires a readable texture. Use RenderTexture blit to read
        // pixels without requiring Read/Write enabled in the import settings.
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        try
        {
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            byte[] pngBytes = readable.EncodeToPNG();
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(path, pngBytes);
            return path;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(readable);
        }
    }

    private static string CreateSampleTextFile(string fileName, string content)
    {
        Debug.Log($"[{LogTag}][{nameof(CreateSampleTextFile)}] fileName: {fileName}, content: {content}");
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, content);
        return path;
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
