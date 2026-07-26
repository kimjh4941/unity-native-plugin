#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using UnityEngine;
using UnityEngine.UIElements;

public class AndroidClipboardManagerExampleController : MonoBehaviour
{
    private const string LogTag = "AndroidClipboardManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private const string ShareFileProviderAuthoritySuffix = ".native_toolkit.share.fileprovider";

    private Label? _resultLabel;
    private string _pendingOperationTitle = string.Empty;
    private int _changeCount;

    private Button? _homeButton;
    private Button? _copyPlainTextButton;
    private Button? _copyEmptyPlainTextButton;
    private Button? _copyHtmlTextButton;
    private Button? _copyHtmlEmptyPlainTextButton;
    private Button? _copyUriButton;
    private Button? _copyMultipleTextButton;
    private Button? _copySensitiveTextButton;
    private Button? _copyInviteCodeButton;
    private Button? _pasteCodeButton;
    private Button? _copyScreenshotButton;
    private Button? _readClipboardButton;
    private Button? _hasClipButton;
    private Button? _getDescriptionButton;
    private Button? _clearClipboardButton;
    private Button? _startObservingButton;
    private Button? _stopObservingButton;
    private Button? _copyEmptyHtmlButton;
    private Button? _copyEmptyItemsButton;
    private Button? _copyBlankUriButton;
    private Button? _copyHttpUriButton;

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
        AndroidClipboardManager.Instance.ClipboardOperationCompleted += OnClipboardOperationCompleted;
        AndroidClipboardManager.Instance.ClipboardChanged += OnClipboardChanged;
#endif
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidClipboardManager.Instance.ClipboardOperationCompleted -= OnClipboardOperationCompleted;
        AndroidClipboardManager.Instance.ClipboardChanged -= OnClipboardChanged;
        AndroidClipboardManager.Instance.StopObserving();
#endif
    }

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_copyPlainTextButton != null) _copyPlainTextButton.clicked -= OnCopyPlainTextClicked;
        if (_copyEmptyPlainTextButton != null) _copyEmptyPlainTextButton.clicked -= OnCopyEmptyPlainTextClicked;
        if (_copyHtmlTextButton != null) _copyHtmlTextButton.clicked -= OnCopyHtmlTextClicked;
        if (_copyHtmlEmptyPlainTextButton != null) _copyHtmlEmptyPlainTextButton.clicked -= OnCopyHtmlEmptyPlainTextClicked;
        if (_copyUriButton != null) _copyUriButton.clicked -= OnCopyUriClicked;
        if (_copyMultipleTextButton != null) _copyMultipleTextButton.clicked -= OnCopyMultipleTextClicked;
        if (_copySensitiveTextButton != null) _copySensitiveTextButton.clicked -= OnCopySensitiveTextClicked;
        if (_copyInviteCodeButton != null) _copyInviteCodeButton.clicked -= OnCopyInviteCodeClicked;
        if (_pasteCodeButton != null) _pasteCodeButton.clicked -= OnPasteCodeClicked;
        if (_copyScreenshotButton != null) _copyScreenshotButton.clicked -= OnCopyScreenshotClicked;
        if (_readClipboardButton != null) _readClipboardButton.clicked -= OnReadClipboardClicked;
        if (_hasClipButton != null) _hasClipButton.clicked -= OnHasClipClicked;
        if (_getDescriptionButton != null) _getDescriptionButton.clicked -= OnGetDescriptionClicked;
        if (_clearClipboardButton != null) _clearClipboardButton.clicked -= OnClearClipboardClicked;
        if (_startObservingButton != null) _startObservingButton.clicked -= OnStartObservingClicked;
        if (_stopObservingButton != null) _stopObservingButton.clicked -= OnStopObservingClicked;
        if (_copyEmptyHtmlButton != null) _copyEmptyHtmlButton.clicked -= OnCopyEmptyHtmlClicked;
        if (_copyEmptyItemsButton != null) _copyEmptyItemsButton.clicked -= OnCopyEmptyItemsClicked;
        if (_copyBlankUriButton != null) _copyBlankUriButton.clicked -= OnCopyBlankUriClicked;
        if (_copyHttpUriButton != null) _copyHttpUriButton.clicked -= OnCopyHttpUriClicked;
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
        _copyPlainTextButton = root.Q<Button>("CopyPlainTextButton");
        _copyEmptyPlainTextButton = root.Q<Button>("CopyEmptyPlainTextButton");
        _copyHtmlTextButton = root.Q<Button>("CopyHtmlTextButton");
        _copyHtmlEmptyPlainTextButton = root.Q<Button>("CopyHtmlEmptyPlainTextButton");
        _copyUriButton = root.Q<Button>("CopyUriButton");
        _copyMultipleTextButton = root.Q<Button>("CopyMultipleTextButton");
        _copySensitiveTextButton = root.Q<Button>("CopySensitiveTextButton");
        _copyInviteCodeButton = root.Q<Button>("CopyInviteCodeButton");
        _pasteCodeButton = root.Q<Button>("PasteCodeButton");
        _copyScreenshotButton = root.Q<Button>("CopyScreenshotButton");
        _readClipboardButton = root.Q<Button>("ReadClipboardButton");
        _hasClipButton = root.Q<Button>("HasClipButton");
        _getDescriptionButton = root.Q<Button>("GetDescriptionButton");
        _clearClipboardButton = root.Q<Button>("ClearClipboardButton");
        _startObservingButton = root.Q<Button>("StartObservingButton");
        _stopObservingButton = root.Q<Button>("StopObservingButton");
        _copyEmptyHtmlButton = root.Q<Button>("CopyEmptyHtmlButton");
        _copyEmptyItemsButton = root.Q<Button>("CopyEmptyItemsButton");
        _copyBlankUriButton = root.Q<Button>("CopyBlankUriButton");
        _copyHttpUriButton = root.Q<Button>("CopyHttpUriButton");

        if (_homeButton != null) _homeButton.clicked += OnHomeClicked;
        if (_copyPlainTextButton != null) _copyPlainTextButton.clicked += OnCopyPlainTextClicked;
        if (_copyEmptyPlainTextButton != null) _copyEmptyPlainTextButton.clicked += OnCopyEmptyPlainTextClicked;
        if (_copyHtmlTextButton != null) _copyHtmlTextButton.clicked += OnCopyHtmlTextClicked;
        if (_copyHtmlEmptyPlainTextButton != null) _copyHtmlEmptyPlainTextButton.clicked += OnCopyHtmlEmptyPlainTextClicked;
        if (_copyUriButton != null) _copyUriButton.clicked += OnCopyUriClicked;
        if (_copyMultipleTextButton != null) _copyMultipleTextButton.clicked += OnCopyMultipleTextClicked;
        if (_copySensitiveTextButton != null) _copySensitiveTextButton.clicked += OnCopySensitiveTextClicked;
        if (_copyInviteCodeButton != null) _copyInviteCodeButton.clicked += OnCopyInviteCodeClicked;
        if (_pasteCodeButton != null) _pasteCodeButton.clicked += OnPasteCodeClicked;
        if (_copyScreenshotButton != null) _copyScreenshotButton.clicked += OnCopyScreenshotClicked;
        if (_readClipboardButton != null) _readClipboardButton.clicked += OnReadClipboardClicked;
        if (_hasClipButton != null) _hasClipButton.clicked += OnHasClipClicked;
        if (_getDescriptionButton != null) _getDescriptionButton.clicked += OnGetDescriptionClicked;
        if (_clearClipboardButton != null) _clearClipboardButton.clicked += OnClearClipboardClicked;
        if (_startObservingButton != null) _startObservingButton.clicked += OnStartObservingClicked;
        if (_stopObservingButton != null) _stopObservingButton.clicked += OnStopObservingClicked;
        if (_copyEmptyHtmlButton != null) _copyEmptyHtmlButton.clicked += OnCopyEmptyHtmlClicked;
        if (_copyEmptyItemsButton != null) _copyEmptyItemsButton.clicked += OnCopyEmptyItemsClicked;
        if (_copyBlankUriButton != null) _copyBlankUriButton.clicked += OnCopyBlankUriClicked;
        if (_copyHttpUriButton != null) _copyHttpUriButton.clicked += OnCopyHttpUriClicked;
    }

    private void OnHomeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHomeClicked)}]");
        if (uiDocument != null)
        {
            NativeToolkitSampleNavigator.ShowTopMenu(uiDocument);
        }
    }

    // ---- Copy ----

    private void OnCopyPlainTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyPlainTextClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy plain text");
        AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
        {
            text = "Hello from Unity Native Toolkit",
            label = "sample"
        });
#else
        SetResult("Android device only. Run this sample on Android to copy plain text.");
#endif
    }

    private void OnCopyEmptyPlainTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyEmptyPlainTextClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy empty plain text");
        AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
        {
            text = ""
        });
#else
        SetResult("Android device only. Run this sample on Android to copy an empty plain text.");
#endif
    }

    private void OnCopyHtmlTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyHtmlTextClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy HTML text");
        AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload
        {
            plainText = "Hello",
            htmlText = "<b>Hello</b>"
        });
#else
        SetResult("Android device only. Run this sample on Android to copy HTML text.");
#endif
    }

    private void OnCopyHtmlEmptyPlainTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyHtmlEmptyPlainTextClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy HTML text (empty plain text)");
        AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload
        {
            plainText = "",
            htmlText = "<b>Html only</b>"
        });
#else
        SetResult("Android device only. Run this sample on Android to copy HTML text with an empty plain text fallback.");
#endif
    }

    private void OnCopyUriClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyUriClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        string uri;
        try
        {
            uri = CreateSampleContentUri("clipboard_sample.txt");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnCopyUriClicked)}] File preparation failed: {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            return;
        }

        SetPendingOperation("Copy URI");
        AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
        {
            uri = uri
        });
#else
        SetResult("Android device only. Run this sample on Android to copy a URI.");
#endif
    }

    private void OnCopyMultipleTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyMultipleTextClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy multiple text");
        AndroidClipboardManager.Instance.CopyMultipleText(new CopyMultipleTextPayload
        {
            texts = new[] { "First", "", "Third" }
        });
#else
        SetResult("Android device only. Run this sample on Android to copy multiple text items.");
#endif
    }

    // ---- Copy - Sensitive ----

    private void OnCopySensitiveTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopySensitiveTextClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy sensitive text");
        AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
        {
            text = "P@ssw0rd-sample",
            isSensitive = true
        });
#else
        SetResult("Android device only. Run this sample on Android to copy sensitive text.");
#endif
    }

    // ---- Game Use Cases ----

    private void OnCopyInviteCodeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyInviteCodeClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy invite code");
        AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
        {
            text = "NTK-7F3A-92QX",
            label = "invite code"
        });
#else
        SetResult("Android device only. Run this sample on Android to copy an invite code.");
#endif
    }

    private void OnPasteCodeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnPasteCodeClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        var result = AndroidClipboardManager.Instance.Read();
        // Log status only: the pasted value may be a coupon code or other sensitive data.
        Debug.Log($"[{LogTag}][{nameof(OnPasteCodeClicked)}] status: {result.Status}, errorCode: {result.ErrorCode}");
        switch (result.Status)
        {
            case ClipboardReadStatus.HasContent:
                string? code = ExtractFirstText(result.Contents!);
                SetResult(
                    code != null ? $"Success: Pasted code: {code}" : "Info: Clipboard has no text item",
                    logMessage: code == null);
                break;
            case ClipboardReadStatus.Empty:
                SetResult("Info: Clipboard is empty (normal)");
                break;
            default:
                SetResult($"Failed: {result.ErrorCode}: {result.ErrorMessage}");
                break;
        }
#else
        SetResult("Android device only. Run this sample on Android to paste a code.");
#endif
    }

    private void OnCopyScreenshotClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyScreenshotClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Info: Capturing screenshot...");
        StartCoroutine(CaptureAndCopyScreenshot());
#else
        SetResult("Android device only. Run this sample on Android to copy a screenshot.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator CaptureAndCopyScreenshot()
    {
        Debug.Log($"[{LogTag}][{nameof(CaptureAndCopyScreenshot)}]");
        // ScreenCapture requires the frame to be fully rendered before reading it back.
        yield return new WaitForEndOfFrame();

        string uri;
        Texture2D? screenshot = null;
        try
        {
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] png = screenshot.EncodeToPNG();
            string path = Path.Combine(Application.persistentDataPath, "clipboard_screenshot.png");
            File.WriteAllBytes(path, png);
            uri = CreateContentUri(path);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(CaptureAndCopyScreenshot)}] {ex.Message}");
            SetResult($"File preparation failed: {ex.Message}");
            yield break;
        }
        finally
        {
            if (screenshot != null) Destroy(screenshot);
        }

        SetPendingOperation("Copy screenshot");
        AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
        {
            uri = uri,
            label = "screenshot"
        });
    }
#endif

    // ---- Read / Inspect ----

    private void OnReadClipboardClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnReadClipboardClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        var result = AndroidClipboardManager.Instance.Read();
        // Log status and error code only: clipboard content may hold passwords or tokens.
        Debug.Log($"[{LogTag}][{nameof(OnReadClipboardClicked)}] status: {result.Status}, errorCode: {result.ErrorCode}");
        switch (result.Status)
        {
            case ClipboardReadStatus.HasContent:
                SetResult($"Success: Read: {FormatContents(result.Contents!)}", logMessage: false);
                break;
            case ClipboardReadStatus.Empty:
                SetResult("Info: Clipboard is empty (normal)");
                break;
            default:
                SetResult($"Failed: {result.ErrorCode}: {result.ErrorMessage}");
                break;
        }
#else
        SetResult("Android device only. Run this sample on Android to read the clipboard.");
#endif
    }

    private void OnHasClipClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHasClipClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        bool hasClip = AndroidClipboardManager.Instance.HasClip();
        SetResult($"Success: hasClip = {hasClip}");
#else
        SetResult("Android device only. Run this sample on Android to check for clip content.");
#endif
    }

    private void OnGetDescriptionClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetDescriptionClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        var result = AndroidClipboardManager.Instance.GetDescription();
        // Log status and error code only: metadata may still reveal content shape.
        Debug.Log($"[{LogTag}][{nameof(OnGetDescriptionClicked)}] status: {result.Status}, errorCode: {result.ErrorCode}");
        switch (result.Status)
        {
            case ClipboardReadStatus.HasContent:
                SetResult($"Success: {FormatDescription(result.Description!)}", logMessage: false);
                break;
            case ClipboardReadStatus.Empty:
                SetResult("Info: Clipboard is empty (normal)");
                break;
            default:
                SetResult($"Failed: {result.ErrorCode}: {result.ErrorMessage}");
                break;
        }
#else
        SetResult("Android device only. Run this sample on Android to get clipboard metadata.");
#endif
    }

    // ---- Clear ----

    private void OnClearClipboardClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnClearClipboardClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Clear clipboard");
        AndroidClipboardManager.Instance.Clear();
#else
        SetResult("Android device only. Run this sample on Android to clear the clipboard.");
#endif
    }

    // ---- Observe ----

    private void OnStartObservingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnStartObservingClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        // startObserving reports no result: this cannot be shown as a success.
        AndroidClipboardManager.Instance.StartObserving();
        SetResult("Info: startObserving requested. Change the clipboard to verify.");
#else
        SetResult("Android device only. Run this sample on Android to start observing the clipboard.");
#endif
    }

    private void OnStopObservingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnStopObservingClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Stop observing");
        AndroidClipboardManager.Instance.StopObserving();
#else
        SetResult("Android device only. Run this sample on Android to stop observing the clipboard.");
#endif
    }

    // ---- Error Cases ----

    private void OnCopyEmptyHtmlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyEmptyHtmlClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy HTML (empty)");
        AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload
        {
            plainText = "Hello",
            htmlText = ""
        });
#else
        SetResult("Android device only. Run this sample on Android to test the empty HTML error case.");
#endif
    }

    private void OnCopyEmptyItemsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyEmptyItemsClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy multiple (empty list)");
        AndroidClipboardManager.Instance.CopyMultipleText(new CopyMultipleTextPayload
        {
            texts = Array.Empty<string>()
        });
#else
        SetResult("Android device only. Run this sample on Android to test the empty item list error case.");
#endif
    }

    private void OnCopyBlankUriClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyBlankUriClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy URI (blank)");
        AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
        {
            uri = ""
        });
#else
        SetResult("Android device only. Run this sample on Android to test the blank URI error case.");
#endif
    }

    private void OnCopyHttpUriClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyHttpUriClicked)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        SetPendingOperation("Copy URI (http scheme)");
        AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload
        {
            uri = "http://example.com/x"
        });
#else
        SetResult("Android device only. Run this sample on Android to test the http-scheme URI error case.");
#endif
    }

    // ---- Event handlers ----

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnClipboardOperationCompleted(ClipboardOperationResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnClipboardOperationCompleted)}] operation: {result.Operation}, isSuccess: {result.IsSuccess}");
        string status = result.IsSuccess ? "Success" : "Failed";
        string title = !string.IsNullOrEmpty(_pendingOperationTitle)
            ? _pendingOperationTitle
            : GetOperationTitle(result.Operation);
        _pendingOperationTitle = string.Empty;
        string msg = $"[event] {title}: {status}";
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            msg += $"\nError: {result.ErrorMessage}";
        }
        SetResult(msg);
    }

    private void OnClipboardChanged()
    {
        _changeCount++;
        Debug.Log($"[{LogTag}][{nameof(OnClipboardChanged)}] changeCount: {_changeCount}");
        SetResult($"[event] Clipboard changed ({_changeCount})");
    }

    private static string GetOperationTitle(string operation)
    {
        return operation switch
        {
            AndroidClipboardManager.OperationCopyPlainText => "Copy plain text",
            AndroidClipboardManager.OperationCopyHtmlText => "Copy HTML text",
            AndroidClipboardManager.OperationCopyUri => "Copy URI",
            AndroidClipboardManager.OperationCopyMultipleText => "Copy multiple text",
            AndroidClipboardManager.OperationClear => "Clear clipboard",
            AndroidClipboardManager.OperationStopObserving => "Stop observing",
            _ => operation
        };
    }

    // Returns the first item's plain text. Deliberately does not fall back to CoercedText: the
    // native mapper derives CoercedText as `item.text ?: item.uri` (ClipboardMappers.kt), so for a
    // URI-only clip (for example one produced by CopyScreenshotButton) CoercedText is just the URI
    // string. Falling back to it would make this button report a content:// URI as a "pasted code".
    // Null when the clip holds no text item (for example a URI-only clip). Mirrors how a coupon-code
    // input field would read the clipboard.
    private static string? ExtractFirstText(ClipContents contents)
    {
        foreach (var item in contents.Items)
        {
            if (!string.IsNullOrEmpty(item.Text)) return item.Text;
        }
        return null;
    }

    // Formats clip contents for on-screen display only. Never logged: clipboard content is sensitive.
    private static string FormatContents(ClipContents contents)
    {
        var itemStrings = new System.Text.StringBuilder();
        itemStrings.Append('[');
        for (int i = 0; i < contents.Items.Count; i++)
        {
            if (i > 0) itemStrings.Append(", ");
            var item = contents.Items[i];
            itemStrings.Append(
                $"{{text={item.Text ?? "(null)"}, htmlText={item.HtmlText ?? "(null)"}, " +
                $"uri={item.Uri ?? "(null)"}, coercedText={item.CoercedText ?? "(null)"}}}");
        }
        itemStrings.Append(']');
        return $"label={contents.Label ?? "(null)"}, mimeTypes=[{string.Join(", ", contents.MimeTypes)}], items={itemStrings}";
    }

    // Formats clip description for on-screen display only. Never logged.
    private static string FormatDescription(ClipDescriptionInfo info)
    {
        return $"label={info.Label ?? "(null)"}, mimeTypes=[{string.Join(", ", info.MimeTypes)}], " +
               $"isStyledText={info.IsStyledText}, classificationStatus={(info.ClassificationStatus?.ToString() ?? "(null)")}";
    }
#endif

    // ---- Content URI helpers ----

    private void SetPendingOperation(string title)
    {
        _pendingOperationTitle = title;
        SetResult($"Requested: {title}");
    }

    // The clipboard API takes a URI string but provides no way to build one. Mirror the native
    // sample (ClipboardSampleScreen.prepareSampleUri) and use the FileProvider bundled in the
    // native-toolkit AAR.
    private static string CreateSampleContentUri(string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, "Clipboard sample file content");
        return CreateContentUri(path);
    }

    // Converts an already-written file path into a content:// URI via the bundled FileProvider.
    // Shared by CreateSampleContentUri and the screenshot capture flow.
    private static string CreateContentUri(string path)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var file = new AndroidJavaObject("java.io.File", path);
        string authority = $"{Application.identifier}{ShareFileProviderAuthoritySuffix}";
        using var fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider");
        using var uri = fileProvider.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file);
        return uri.Call<string>("toString");
#else
        return path;
#endif
    }

    // logMessage is false for Read/GetDescription/PasteCode results: those messages embed
    // clipboard content (which may hold passwords or tokens) and must never reach Debug.Log,
    // even though they are shown on screen. This deviates from the base pattern in
    // AndroidShareManagerExampleController.SetResult, which always logs the message.
    private void SetResult(string message, bool logMessage = true)
    {
        if (logMessage)
        {
            Debug.Log($"[{LogTag}][{nameof(SetResult)}] {message}");
        }
        if (_resultLabel != null)
        {
            _resultLabel.text = message;
        }
    }
}
#endif
