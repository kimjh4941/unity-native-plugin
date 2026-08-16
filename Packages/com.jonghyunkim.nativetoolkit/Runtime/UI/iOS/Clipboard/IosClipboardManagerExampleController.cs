#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Example controller demonstrating the iOS pasteboard via <see cref="IosClipboardManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every call captures its own <see cref="IosClipboardSampleResultContext"/> and reports through
/// that call's per-call callback. The Manager only serializes same-operation calls, so different
/// operations genuinely overlap and a single "pending marker" field would mislabel completions.
/// The common events are used for shape-only logging; only ClipboardChanged - which belongs to no
/// call - updates the screen.
/// </para>
/// <para>
/// No handler is platform-guarded: in the Editor the Manager rejects every operation with
/// CLIPBOARD_BRIDGE_UNAVAILABLE, which is exactly what this screen is meant to show.
/// </para>
/// <para>
/// Clipboard content, base64 payloads, detected values, pasteboard names and temporary file paths
/// are never shown or logged; results are reduced to counts, lengths and kinds.
/// </para>
/// </remarks>
public class IosClipboardManagerExampleController : MonoBehaviour
{
    private const string LogTag = "IosClipboardManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    // ── Fixtures ─────────────────────────────────────────────────────────────
    // Bodies have deliberately different lengths so a device can tell them apart from textLen
    // alone, without ever displaying the text itself.

    private const string PlainTextBody = "Hello 日本語 \U0001F680 テスト";
    private const string LocalOnlyBody = "LOCALONLY-0001";
    private const string AppendMarkerPrefix = "APPENDED-MARKER-";
    private const string HtmlPlainText = "Hello";
    private const string HtmlMarkup = "<b>Hello</b>";
    private const string SampleUrl = "https://unity.com";
    private const string InvalidUrl = "not a valid url";
    private const string MissingImagePath = "/nonexistent/ios-clipboard-missing.png";
    private const string SampleImageFileName = "ios_clipboard_sample_image.png";
    private const string PngTypeIdentifier = "public.png";
    private const string PlainTextTypeIdentifier = "public.utf8-plain-text";
    private const string DataTypeIdentifier = "public.data";
    private const string InvalidTypeIdentifier = "not a uti";
    private const string CustomTypeIdentifier = "com.jonghyunkim.nativetoolkit.example.custom";
    private const string FixedName = "com.jonghyunkim.nativetoolkit.example.sample";
    private const string NumberFixture = "42";
    private const string SearchFixture = "swift concurrency";
    private const string DetectionFixture =
        "Order 12345 from https://example.com/store, contact support@example.com or +1 (408) 555-0134, " +
        "ship to 1 Infinite Loop, Cupertino, CA 95014, meeting on March 3, 2027 at 10:00, " +
        "flight AA100, total $42.50, tracking 1Z999AA10123456784";

    private const int DeviceBaselineLength = 31;
    private const int AppendMarkerSuffixLength = 8;
    private const int FileFixtureByteCount = 64;
    private const byte FileFixtureByte = 0x41;

    // Large-image fixture. A fixed local PRNG keeps the encoded size stable across runs so peak
    // memory can be compared; UnityEngine.Random would mutate global state shared with other samples.
    private const int LargeImageSide = 1024;
    private const uint LargeImageSeed = 0x5EEDC10B;
    private const int LargeImageMinBytes = 3 * 1024 * 1024;
    private const int LargeImageMaxBytes = 5 * 1024 * 1024;

    private const int ExpirationSeconds = 30;

    private static readonly IosClipboardDetectionPattern[] AllDetectionPatterns =
    {
        IosClipboardDetectionPattern.ProbableWebUrl,
        IosClipboardDetectionPattern.ProbableWebSearch,
        IosClipboardDetectionPattern.Number,
        IosClipboardDetectionPattern.Link,
        IosClipboardDetectionPattern.EmailAddress,
        IosClipboardDetectionPattern.PhoneNumber,
        IosClipboardDetectionPattern.PostalAddress,
        IosClipboardDetectionPattern.CalendarEvent,
        IosClipboardDetectionPattern.FlightNumber,
        IosClipboardDetectionPattern.MoneyAmount,
        IosClipboardDetectionPattern.ShipmentTrackingNumber
    };

    private static readonly string[] SnapshotMatchingTypes = { PlainTextTypeIdentifier, PngTypeIdentifier };

    // ── State ────────────────────────────────────────────────────────────────

    private Label? _resultLabel;
    private ScrollView? _resultScrollView;
    private Label? _statusLabel;

    private IosPasteboardScope _activeScope = IosPasteboardScope.General;
    private IosPasteboardScope? _lastRemovedScope;
    private IosPasteboardScope? _observedScope;
    private IosClipboardSampleObservationState _observation;
    private int _observedEventCount;
    private int _resultSequence;

    private Button? _homeButton;
    private Button? _useGeneralButton;
    private Button? _createNamedPasteboardButton;
    private Button? _useFixedNamedScopeButton;
    private Button? _createUniquePasteboardButton;
    private Button? _removeActivePasteboardButton;
    private Button? _probeRemovedScopeButton;
    private Button? _copyPlainTextButton;
    private Button? _copyEmptyPlainTextButton;
    private Button? _copyHtmlTextButton;
    private Button? _copyUrlButton;
    private Button? _copyImageFileButton;
    private Button? _copyImageDataButton;
    private Button? _copyColorButton;
    private Button? _copyCustomDataButton;
    private Button? _copyMultipleTextButton;
    private Button? _copyMultiRepresentationButton;
    private Button? _copyDetectionFixtureButton;
    private Button? _copyLocalOnlyTrueButton;
    private Button? _copyLocalOnlyFalseButton;
    private Button? _copyDeviceBaselineButton;
    private Button? _copyExpiringButton;
    private Button? _appendPlainTextButton;
    private Button? _appendUrlButton;
    private Button? _readButton;
    private Button? _readDataPngButton;
    private Button? _snapshotButton;
    private Button? _snapshotMatchingButton;
    private Button? _loadTextButton;
    private Button? _loadUrlButton;
    private Button? _loadImageButton;
    private Button? _loadFileButton;
    private Button? _loadFileCustomButton;
    private Button? _cancelLoadsButton;
    private Button? _copyNumberFixtureButton;
    private Button? _copySearchFixtureButton;
    private Button? _detectPatternsButton;
    private Button? _detectValuesButton;
    private Button? _startObservingButton;
    private Button? _restartObservingButton;
    private Button? _stopObservingButton;
    private Button? _checkForegroundChangeButton;
    private Button? _clearActiveScopeButton;
    private Button? _busyLoadItemTwiceButton;
    private Button? _seedAndCancelLoadButton;
    private Button? _busyStartObservingTwiceButton;
    private Button? _copyLargeImageDataButton;
    private Button? _errCopyMultipleEmptyButton;
    private Button? _errCopyMultiRepEmptyButton;
    private Button? _errCopyImageFileMissingButton;
    private Button? _errCopyInvalidUtiButton;
    private Button? _errCopyInvalidUrlButton;
    private Button? _errCopyColorOutOfRangeButton;
    private Button? _errReadDataInvalidUtiButton;
    private Button? _errRemoveGeneralButton;
    private Button? _errObserveMissingNamedButton;
    private Button? _errDetectEmptyPatternsButton;

    // ── Lifecycle ────────────────────────────────────────────────────────────

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
        var manager = IosClipboardManager.Instance;
        manager.ClipboardOperationCompleted += OnClipboardOperationCompletedEvent;
        manager.ReadCompleted += OnReadCompletedEvent;
        manager.ReadDataCompleted += OnReadDataCompletedEvent;
        manager.SnapshotCompleted += OnSnapshotCompletedEvent;
        manager.PasteboardCreated += OnPasteboardCreatedEvent;
        manager.PatternsDetected += OnPatternsDetectedEvent;
        manager.ValuesDetected += OnValuesDetectedEvent;
        manager.ItemLoaded += OnItemLoadedEvent;
        manager.ForegroundChangeChecked += OnForegroundChangeCheckedEvent;
        manager.ClipboardChanged += OnClipboardChangedEvent;
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");

        // Stop first, unsubscribe second. Per-call callbacks are not events, so the stop result
        // still arrives after the events are gone; issuing the stop after unsubscribing would be
        // fine too, but this order keeps the teardown decision next to the state it reads.
        _observation.RequestStop();
        if (_observation.ShouldIssueStopNow())
        {
            IssueStopObserving("observe.stop.teardown");
        }

        var manager = IosClipboardManager.Instance;
        manager.ClipboardOperationCompleted -= OnClipboardOperationCompletedEvent;
        manager.ReadCompleted -= OnReadCompletedEvent;
        manager.ReadDataCompleted -= OnReadDataCompletedEvent;
        manager.SnapshotCompleted -= OnSnapshotCompletedEvent;
        manager.PasteboardCreated -= OnPasteboardCreatedEvent;
        manager.PatternsDetected -= OnPatternsDetectedEvent;
        manager.ValuesDetected -= OnValuesDetectedEvent;
        manager.ItemLoaded -= OnItemLoadedEvent;
        manager.ForegroundChangeChecked -= OnForegroundChangeCheckedEvent;
        manager.ClipboardChanged -= OnClipboardChangedEvent;
    }

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_useGeneralButton != null) _useGeneralButton.clicked -= OnUseGeneralClicked;
        if (_createNamedPasteboardButton != null) _createNamedPasteboardButton.clicked -= OnCreateNamedPasteboardClicked;
        if (_useFixedNamedScopeButton != null) _useFixedNamedScopeButton.clicked -= OnUseFixedNamedScopeClicked;
        if (_createUniquePasteboardButton != null) _createUniquePasteboardButton.clicked -= OnCreateUniquePasteboardClicked;
        if (_removeActivePasteboardButton != null) _removeActivePasteboardButton.clicked -= OnRemoveActivePasteboardClicked;
        if (_probeRemovedScopeButton != null) _probeRemovedScopeButton.clicked -= OnProbeRemovedScopeClicked;
        if (_copyPlainTextButton != null) _copyPlainTextButton.clicked -= OnCopyPlainTextClicked;
        if (_copyEmptyPlainTextButton != null) _copyEmptyPlainTextButton.clicked -= OnCopyEmptyPlainTextClicked;
        if (_copyHtmlTextButton != null) _copyHtmlTextButton.clicked -= OnCopyHtmlTextClicked;
        if (_copyUrlButton != null) _copyUrlButton.clicked -= OnCopyUrlClicked;
        if (_copyImageFileButton != null) _copyImageFileButton.clicked -= OnCopyImageFileClicked;
        if (_copyImageDataButton != null) _copyImageDataButton.clicked -= OnCopyImageDataClicked;
        if (_copyColorButton != null) _copyColorButton.clicked -= OnCopyColorClicked;
        if (_copyCustomDataButton != null) _copyCustomDataButton.clicked -= OnCopyCustomDataClicked;
        if (_copyMultipleTextButton != null) _copyMultipleTextButton.clicked -= OnCopyMultipleTextClicked;
        if (_copyMultiRepresentationButton != null) _copyMultiRepresentationButton.clicked -= OnCopyMultiRepresentationClicked;
        if (_copyDetectionFixtureButton != null) _copyDetectionFixtureButton.clicked -= OnCopyDetectionFixtureClicked;
        if (_copyLocalOnlyTrueButton != null) _copyLocalOnlyTrueButton.clicked -= OnCopyLocalOnlyTrueClicked;
        if (_copyLocalOnlyFalseButton != null) _copyLocalOnlyFalseButton.clicked -= OnCopyLocalOnlyFalseClicked;
        if (_copyDeviceBaselineButton != null) _copyDeviceBaselineButton.clicked -= OnCopyDeviceBaselineClicked;
        if (_copyExpiringButton != null) _copyExpiringButton.clicked -= OnCopyExpiringClicked;
        if (_appendPlainTextButton != null) _appendPlainTextButton.clicked -= OnAppendPlainTextClicked;
        if (_appendUrlButton != null) _appendUrlButton.clicked -= OnAppendUrlClicked;
        if (_readButton != null) _readButton.clicked -= OnReadClicked;
        if (_readDataPngButton != null) _readDataPngButton.clicked -= OnReadDataPngClicked;
        if (_snapshotButton != null) _snapshotButton.clicked -= OnSnapshotClicked;
        if (_snapshotMatchingButton != null) _snapshotMatchingButton.clicked -= OnSnapshotMatchingClicked;
        if (_loadTextButton != null) _loadTextButton.clicked -= OnLoadTextClicked;
        if (_loadUrlButton != null) _loadUrlButton.clicked -= OnLoadUrlClicked;
        if (_loadImageButton != null) _loadImageButton.clicked -= OnLoadImageClicked;
        if (_loadFileButton != null) _loadFileButton.clicked -= OnLoadFileClicked;
        if (_loadFileCustomButton != null) _loadFileCustomButton.clicked -= OnLoadFileCustomClicked;
        if (_cancelLoadsButton != null) _cancelLoadsButton.clicked -= OnCancelLoadsClicked;
        if (_copyNumberFixtureButton != null) _copyNumberFixtureButton.clicked -= OnCopyNumberFixtureClicked;
        if (_copySearchFixtureButton != null) _copySearchFixtureButton.clicked -= OnCopySearchFixtureClicked;
        if (_detectPatternsButton != null) _detectPatternsButton.clicked -= OnDetectPatternsClicked;
        if (_detectValuesButton != null) _detectValuesButton.clicked -= OnDetectValuesClicked;
        if (_startObservingButton != null) _startObservingButton.clicked -= OnStartObservingClicked;
        if (_restartObservingButton != null) _restartObservingButton.clicked -= OnRestartObservingClicked;
        if (_stopObservingButton != null) _stopObservingButton.clicked -= OnStopObservingClicked;
        if (_checkForegroundChangeButton != null) _checkForegroundChangeButton.clicked -= OnCheckForegroundChangeClicked;
        if (_clearActiveScopeButton != null) _clearActiveScopeButton.clicked -= OnClearActiveScopeClicked;
        if (_busyLoadItemTwiceButton != null) _busyLoadItemTwiceButton.clicked -= OnBusyLoadItemTwiceClicked;
        if (_seedAndCancelLoadButton != null) _seedAndCancelLoadButton.clicked -= OnSeedAndCancelLoadClicked;
        if (_busyStartObservingTwiceButton != null) _busyStartObservingTwiceButton.clicked -= OnBusyStartObservingTwiceClicked;
        if (_copyLargeImageDataButton != null) _copyLargeImageDataButton.clicked -= OnCopyLargeImageDataClicked;
        if (_errCopyMultipleEmptyButton != null) _errCopyMultipleEmptyButton.clicked -= OnErrCopyMultipleEmptyClicked;
        if (_errCopyMultiRepEmptyButton != null) _errCopyMultiRepEmptyButton.clicked -= OnErrCopyMultiRepEmptyClicked;
        if (_errCopyImageFileMissingButton != null) _errCopyImageFileMissingButton.clicked -= OnErrCopyImageFileMissingClicked;
        if (_errCopyInvalidUtiButton != null) _errCopyInvalidUtiButton.clicked -= OnErrCopyInvalidUtiClicked;
        if (_errCopyInvalidUrlButton != null) _errCopyInvalidUrlButton.clicked -= OnErrCopyInvalidUrlClicked;
        if (_errCopyColorOutOfRangeButton != null) _errCopyColorOutOfRangeButton.clicked -= OnErrCopyColorOutOfRangeClicked;
        if (_errReadDataInvalidUtiButton != null) _errReadDataInvalidUtiButton.clicked -= OnErrReadDataInvalidUtiClicked;
        if (_errRemoveGeneralButton != null) _errRemoveGeneralButton.clicked -= OnErrRemoveGeneralClicked;
        if (_errObserveMissingNamedButton != null) _errObserveMissingNamedButton.clicked -= OnErrObserveMissingNamedClicked;
        if (_errDetectEmptyPatternsButton != null) _errDetectEmptyPatternsButton.clicked -= OnErrDetectEmptyPatternsClicked;
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
        _resultScrollView = root.Q<ScrollView>("ResultScrollView");
        _statusLabel = root.Q<Label>("StatusTextBlock");

        _homeButton = root.Q<Button>("HomeButton");
        _useGeneralButton = root.Q<Button>("UseGeneralButton");
        _createNamedPasteboardButton = root.Q<Button>("CreateNamedPasteboardButton");
        _useFixedNamedScopeButton = root.Q<Button>("UseFixedNamedScopeButton");
        _createUniquePasteboardButton = root.Q<Button>("CreateUniquePasteboardButton");
        _removeActivePasteboardButton = root.Q<Button>("RemoveActivePasteboardButton");
        _probeRemovedScopeButton = root.Q<Button>("ProbeRemovedScopeButton");
        _copyPlainTextButton = root.Q<Button>("CopyPlainTextButton");
        _copyEmptyPlainTextButton = root.Q<Button>("CopyEmptyPlainTextButton");
        _copyHtmlTextButton = root.Q<Button>("CopyHtmlTextButton");
        _copyUrlButton = root.Q<Button>("CopyUrlButton");
        _copyImageFileButton = root.Q<Button>("CopyImageFileButton");
        _copyImageDataButton = root.Q<Button>("CopyImageDataButton");
        _copyColorButton = root.Q<Button>("CopyColorButton");
        _copyCustomDataButton = root.Q<Button>("CopyCustomDataButton");
        _copyMultipleTextButton = root.Q<Button>("CopyMultipleTextButton");
        _copyMultiRepresentationButton = root.Q<Button>("CopyMultiRepresentationButton");
        _copyDetectionFixtureButton = root.Q<Button>("CopyDetectionFixtureButton");
        _copyLocalOnlyTrueButton = root.Q<Button>("CopyLocalOnlyTrueButton");
        _copyLocalOnlyFalseButton = root.Q<Button>("CopyLocalOnlyFalseButton");
        _copyDeviceBaselineButton = root.Q<Button>("CopyDeviceBaselineButton");
        _copyExpiringButton = root.Q<Button>("CopyExpiringButton");
        _appendPlainTextButton = root.Q<Button>("AppendPlainTextButton");
        _appendUrlButton = root.Q<Button>("AppendUrlButton");
        _readButton = root.Q<Button>("ReadButton");
        _readDataPngButton = root.Q<Button>("ReadDataPngButton");
        _snapshotButton = root.Q<Button>("SnapshotButton");
        _snapshotMatchingButton = root.Q<Button>("SnapshotMatchingButton");
        _loadTextButton = root.Q<Button>("LoadTextButton");
        _loadUrlButton = root.Q<Button>("LoadUrlButton");
        _loadImageButton = root.Q<Button>("LoadImageButton");
        _loadFileButton = root.Q<Button>("LoadFileButton");
        _loadFileCustomButton = root.Q<Button>("LoadFileCustomButton");
        _cancelLoadsButton = root.Q<Button>("CancelLoadsButton");
        _copyNumberFixtureButton = root.Q<Button>("CopyNumberFixtureButton");
        _copySearchFixtureButton = root.Q<Button>("CopySearchFixtureButton");
        _detectPatternsButton = root.Q<Button>("DetectPatternsButton");
        _detectValuesButton = root.Q<Button>("DetectValuesButton");
        _startObservingButton = root.Q<Button>("StartObservingButton");
        _restartObservingButton = root.Q<Button>("RestartObservingButton");
        _stopObservingButton = root.Q<Button>("StopObservingButton");
        _checkForegroundChangeButton = root.Q<Button>("CheckForegroundChangeButton");
        _clearActiveScopeButton = root.Q<Button>("ClearActiveScopeButton");
        _busyLoadItemTwiceButton = root.Q<Button>("BusyLoadItemTwiceButton");
        _seedAndCancelLoadButton = root.Q<Button>("SeedAndCancelLoadButton");
        _busyStartObservingTwiceButton = root.Q<Button>("BusyStartObservingTwiceButton");
        _copyLargeImageDataButton = root.Q<Button>("CopyLargeImageDataButton");
        _errCopyMultipleEmptyButton = root.Q<Button>("ErrCopyMultipleEmptyButton");
        _errCopyMultiRepEmptyButton = root.Q<Button>("ErrCopyMultiRepEmptyButton");
        _errCopyImageFileMissingButton = root.Q<Button>("ErrCopyImageFileMissingButton");
        _errCopyInvalidUtiButton = root.Q<Button>("ErrCopyInvalidUtiButton");
        _errCopyInvalidUrlButton = root.Q<Button>("ErrCopyInvalidUrlButton");
        _errCopyColorOutOfRangeButton = root.Q<Button>("ErrCopyColorOutOfRangeButton");
        _errReadDataInvalidUtiButton = root.Q<Button>("ErrReadDataInvalidUtiButton");
        _errRemoveGeneralButton = root.Q<Button>("ErrRemoveGeneralButton");
        _errObserveMissingNamedButton = root.Q<Button>("ErrObserveMissingNamedButton");
        _errDetectEmptyPatternsButton = root.Q<Button>("ErrDetectEmptyPatternsButton");

        if (_homeButton != null) _homeButton.clicked += OnHomeClicked;
        if (_useGeneralButton != null) _useGeneralButton.clicked += OnUseGeneralClicked;
        if (_createNamedPasteboardButton != null) _createNamedPasteboardButton.clicked += OnCreateNamedPasteboardClicked;
        if (_useFixedNamedScopeButton != null) _useFixedNamedScopeButton.clicked += OnUseFixedNamedScopeClicked;
        if (_createUniquePasteboardButton != null) _createUniquePasteboardButton.clicked += OnCreateUniquePasteboardClicked;
        if (_removeActivePasteboardButton != null) _removeActivePasteboardButton.clicked += OnRemoveActivePasteboardClicked;
        if (_probeRemovedScopeButton != null) _probeRemovedScopeButton.clicked += OnProbeRemovedScopeClicked;
        if (_copyPlainTextButton != null) _copyPlainTextButton.clicked += OnCopyPlainTextClicked;
        if (_copyEmptyPlainTextButton != null) _copyEmptyPlainTextButton.clicked += OnCopyEmptyPlainTextClicked;
        if (_copyHtmlTextButton != null) _copyHtmlTextButton.clicked += OnCopyHtmlTextClicked;
        if (_copyUrlButton != null) _copyUrlButton.clicked += OnCopyUrlClicked;
        if (_copyImageFileButton != null) _copyImageFileButton.clicked += OnCopyImageFileClicked;
        if (_copyImageDataButton != null) _copyImageDataButton.clicked += OnCopyImageDataClicked;
        if (_copyColorButton != null) _copyColorButton.clicked += OnCopyColorClicked;
        if (_copyCustomDataButton != null) _copyCustomDataButton.clicked += OnCopyCustomDataClicked;
        if (_copyMultipleTextButton != null) _copyMultipleTextButton.clicked += OnCopyMultipleTextClicked;
        if (_copyMultiRepresentationButton != null) _copyMultiRepresentationButton.clicked += OnCopyMultiRepresentationClicked;
        if (_copyDetectionFixtureButton != null) _copyDetectionFixtureButton.clicked += OnCopyDetectionFixtureClicked;
        if (_copyLocalOnlyTrueButton != null) _copyLocalOnlyTrueButton.clicked += OnCopyLocalOnlyTrueClicked;
        if (_copyLocalOnlyFalseButton != null) _copyLocalOnlyFalseButton.clicked += OnCopyLocalOnlyFalseClicked;
        if (_copyDeviceBaselineButton != null) _copyDeviceBaselineButton.clicked += OnCopyDeviceBaselineClicked;
        if (_copyExpiringButton != null) _copyExpiringButton.clicked += OnCopyExpiringClicked;
        if (_appendPlainTextButton != null) _appendPlainTextButton.clicked += OnAppendPlainTextClicked;
        if (_appendUrlButton != null) _appendUrlButton.clicked += OnAppendUrlClicked;
        if (_readButton != null) _readButton.clicked += OnReadClicked;
        if (_readDataPngButton != null) _readDataPngButton.clicked += OnReadDataPngClicked;
        if (_snapshotButton != null) _snapshotButton.clicked += OnSnapshotClicked;
        if (_snapshotMatchingButton != null) _snapshotMatchingButton.clicked += OnSnapshotMatchingClicked;
        if (_loadTextButton != null) _loadTextButton.clicked += OnLoadTextClicked;
        if (_loadUrlButton != null) _loadUrlButton.clicked += OnLoadUrlClicked;
        if (_loadImageButton != null) _loadImageButton.clicked += OnLoadImageClicked;
        if (_loadFileButton != null) _loadFileButton.clicked += OnLoadFileClicked;
        if (_loadFileCustomButton != null) _loadFileCustomButton.clicked += OnLoadFileCustomClicked;
        if (_cancelLoadsButton != null) _cancelLoadsButton.clicked += OnCancelLoadsClicked;
        if (_copyNumberFixtureButton != null) _copyNumberFixtureButton.clicked += OnCopyNumberFixtureClicked;
        if (_copySearchFixtureButton != null) _copySearchFixtureButton.clicked += OnCopySearchFixtureClicked;
        if (_detectPatternsButton != null) _detectPatternsButton.clicked += OnDetectPatternsClicked;
        if (_detectValuesButton != null) _detectValuesButton.clicked += OnDetectValuesClicked;
        if (_startObservingButton != null) _startObservingButton.clicked += OnStartObservingClicked;
        if (_restartObservingButton != null) _restartObservingButton.clicked += OnRestartObservingClicked;
        if (_stopObservingButton != null) _stopObservingButton.clicked += OnStopObservingClicked;
        if (_checkForegroundChangeButton != null) _checkForegroundChangeButton.clicked += OnCheckForegroundChangeClicked;
        if (_clearActiveScopeButton != null) _clearActiveScopeButton.clicked += OnClearActiveScopeClicked;
        if (_busyLoadItemTwiceButton != null) _busyLoadItemTwiceButton.clicked += OnBusyLoadItemTwiceClicked;
        if (_seedAndCancelLoadButton != null) _seedAndCancelLoadButton.clicked += OnSeedAndCancelLoadClicked;
        if (_busyStartObservingTwiceButton != null) _busyStartObservingTwiceButton.clicked += OnBusyStartObservingTwiceClicked;
        if (_copyLargeImageDataButton != null) _copyLargeImageDataButton.clicked += OnCopyLargeImageDataClicked;
        if (_errCopyMultipleEmptyButton != null) _errCopyMultipleEmptyButton.clicked += OnErrCopyMultipleEmptyClicked;
        if (_errCopyMultiRepEmptyButton != null) _errCopyMultiRepEmptyButton.clicked += OnErrCopyMultiRepEmptyClicked;
        if (_errCopyImageFileMissingButton != null) _errCopyImageFileMissingButton.clicked += OnErrCopyImageFileMissingClicked;
        if (_errCopyInvalidUtiButton != null) _errCopyInvalidUtiButton.clicked += OnErrCopyInvalidUtiClicked;
        if (_errCopyInvalidUrlButton != null) _errCopyInvalidUrlButton.clicked += OnErrCopyInvalidUrlClicked;
        if (_errCopyColorOutOfRangeButton != null) _errCopyColorOutOfRangeButton.clicked += OnErrCopyColorOutOfRangeClicked;
        if (_errReadDataInvalidUtiButton != null) _errReadDataInvalidUtiButton.clicked += OnErrReadDataInvalidUtiClicked;
        if (_errRemoveGeneralButton != null) _errRemoveGeneralButton.clicked += OnErrRemoveGeneralClicked;
        if (_errObserveMissingNamedButton != null) _errObserveMissingNamedButton.clicked += OnErrObserveMissingNamedClicked;
        if (_errDetectEmptyPatternsButton != null) _errDetectEmptyPatternsButton.clicked += OnErrDetectEmptyPatternsClicked;

        UpdateStatus();
        UpdateEnabledStates();
    }

    // ── Scope ────────────────────────────────────────────────────────────────

    private void OnHomeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHomeClicked)}]");
        if (uiDocument != null)
        {
            NativeToolkitSampleNavigator.ShowTopMenu(uiDocument);
        }
    }

    private void OnUseGeneralClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnUseGeneralClicked)}]");
        var context = BeginResult("scope.useGeneral");
        _activeScope = IosPasteboardScope.General;
        SetResult(IosClipboardSampleResult.FormatSuccess(context, "scope=general"));
        UpdateStatus();
    }

    private void OnUseFixedNamedScopeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnUseFixedNamedScopeClicked)}]");
        var context = BeginResult("scope.useFixedNamed");
        _activeScope = IosPasteboardScope.Named(FixedName);
        SetResult(IosClipboardSampleResult.FormatSuccess(
            context, $"scope={IosClipboardSampleResult.FormatScopeLabel(_activeScope)}"));
        UpdateStatus();
    }

    private void OnCreateNamedPasteboardClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCreateNamedPasteboardClicked)}]");
        IssueCreatePasteboard("scope.createNamed", IosPasteboardCreationRequest.Named(FixedName));
    }

    private void OnCreateUniquePasteboardClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCreateUniquePasteboardClicked)}]");
        IssueCreatePasteboard("scope.createUnique", IosPasteboardCreationRequest.Unique);
    }

    private void IssueCreatePasteboard(string marker, IosPasteboardCreationRequest request)
    {
        IosPasteboardScope scopeAtCall = _activeScope;
        var context = BeginResult(marker);
        IosClipboardManager.Instance.CreatePasteboard(request, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(IssueCreatePasteboard)}] seq: {context.Sequence}, " +
                      $"marker: {context.Marker}, isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
            if (!IsScreenAlive()) return;
            if (!result.IsSuccess || result.Scope == null)
            {
                SetResult(IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
                return;
            }

            // Only adopt the new scope when this call still owns the active one: the user may have
            // switched scopes while the creation was in flight.
            if (ReferenceEquals(_activeScope, scopeAtCall))
            {
                _activeScope = result.Scope;
            }
            SetResult(IosClipboardSampleResult.FormatSuccess(
                context, $"scope={IosClipboardSampleResult.FormatScopeLabel(result.Scope)}"));
            UpdateStatus();
        });
    }

    private void OnRemoveActivePasteboardClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveActivePasteboardClicked)}]");
        IosPasteboardScope target = _activeScope;
        var context = BeginResult("scope.remove");
        IosClipboardManager.Instance.RemovePasteboard(target, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(OnRemoveActivePasteboardClicked)}] seq: {context.Sequence}, " +
                      $"marker: {context.Marker}, isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
            if (!IsScreenAlive()) return;
            if (!result.IsSuccess)
            {
                SetResult(IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
                return;
            }

            _lastRemovedScope = target;
            if (ReferenceEquals(_activeScope, target))
            {
                _activeScope = IosPasteboardScope.General;
            }
            SetResult(IosClipboardSampleResult.FormatSuccess(
                context, $"removed={IosClipboardSampleResult.FormatScopeLabel(target)}"));
            UpdateStatus();
        });
    }

    private void OnProbeRemovedScopeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnProbeRemovedScopeClicked)}] hasLastRemoved: {_lastRemovedScope != null}");
        var context = BeginResult("scope.probeRemoved");
        if (_lastRemovedScope == null)
        {
            // The only precondition this screen checks itself: without a removed scope there is
            // nothing to probe, and calling Read on the active scope would prove nothing.
            SetResult(IosClipboardSampleResult.FormatLocal(context, "no removed scope yet"));
            return;
        }

        IssueRead(context, _lastRemovedScope);
    }

    // ── Copy ─────────────────────────────────────────────────────────────────

    private void OnCopyPlainTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyPlainTextClicked)}] scope: {IosClipboardSampleResult.FormatScopeLabel(_activeScope)}");
        IssueCopy("copy.plainText", IosClipboardContent.PlainText(PlainTextBody));
    }

    private void OnCopyEmptyPlainTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyEmptyPlainTextClicked)}]");
        IssueCopy("copy.emptyPlainText", IosClipboardContent.PlainText(string.Empty));
    }

    private void OnCopyHtmlTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyHtmlTextClicked)}]");
        IssueCopy("copy.htmlText", IosClipboardContent.HtmlText(HtmlPlainText, HtmlMarkup));
    }

    private void OnCopyUrlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyUrlClicked)}]");
        IssueCopy("copy.url", IosClipboardContent.Url(SampleUrl));
    }

    private void OnCopyImageFileClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyImageFileClicked)}]");
        string path;
        try
        {
            path = WriteSampleImageFile();
        }
        catch (Exception ex)
        {
            // Type only: file exception messages embed the path they failed on.
            Debug.LogError($"[{LogTag}][{nameof(OnCopyImageFileClicked)}] image file preparation failed: " +
                           $"{IosClipboardSampleResult.DescribeException(ex)}");
            var failedContext = BeginResult("copy.imageFile");
            SetResult(IosClipboardSampleResult.FormatLocal(failedContext, "fixture=write-failed"));
            return;
        }

        IssueCopy("copy.imageFile", IosClipboardContent.ImageFile(path));
    }

    private void OnCopyImageDataClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyImageDataClicked)}]");
        IssueCopy("copy.imageData", IosClipboardContent.ImageData(CreateSmallPng(), PngTypeIdentifier));
    }

    private void OnCopyColorClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyColorClicked)}]");
        IssueCopy("copy.color", IosClipboardContent.Color(0.2, 0.4, 0.8, 1.0));
    }

    private void OnCopyCustomDataClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyCustomDataClicked)}]");
        // public.data, not the custom UTI: LoadFileButton loads this representation back and
        // asserts its 64-byte size. The custom UTI is demonstrated by CopyMultiRepresentation.
        IssueCopy("copy.customData", IosClipboardContent.CustomData(CreateFileFixturePayload(), DataTypeIdentifier));
    }

    private void OnCopyMultipleTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyMultipleTextClicked)}]");
        IssueCopy("copy.multipleText", IosClipboardContent.MultipleText(new[] { "First", string.Empty, "Third" }));
    }

    private void OnCopyMultiRepresentationClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyMultiRepresentationClicked)}]");
        var representations = new Dictionary<string, byte[]>
        {
            { PlainTextTypeIdentifier, Encoding.UTF8.GetBytes(LocalOnlyBody) },
            { CustomTypeIdentifier, CreateFileFixturePayload() }
        };
        IssueCopy("copy.multiRepresentation", IosClipboardContent.MultiRepresentation(representations));
    }

    private void OnCopyDetectionFixtureClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyDetectionFixtureClicked)}]");
        IssueCopy("copy.detectionFixture", IosClipboardContent.PlainText(DetectionFixture));
    }

    private void OnCopyNumberFixtureClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyNumberFixtureClicked)}]");
        IssueCopy("copy.numberFixture", IosClipboardContent.PlainText(NumberFixture));
    }

    private void OnCopySearchFixtureClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopySearchFixtureClicked)}]");
        IssueCopy("copy.searchFixture", IosClipboardContent.PlainText(SearchFixture));
    }

    private void OnCopyLargeImageDataClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyLargeImageDataClicked)}]");
        byte[] png;
        try
        {
            png = CreateLargePng();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{LogTag}][{nameof(OnCopyLargeImageDataClicked)}] fixture generation failed: {ex.Message}");
            SetResult(IosClipboardSampleResult.FormatLocal(BeginResult("copy.largeImage"), "fixture=generation-failed"));
            return;
        }

        if (png.Length < LargeImageMinBytes || png.Length > LargeImageMaxBytes)
        {
            // Out of the target band the memory comparison is not meaningful, so do not measure.
            Debug.LogWarning($"[{LogTag}][{nameof(OnCopyLargeImageDataClicked)}] fixture out of range: {png.Length}");
            SetResult(IosClipboardSampleResult.FormatLocal(
                BeginResult("copy.largeImage"), $"fixture=out-of-range bytes={png.Length}"));
            return;
        }

        var context = BeginResult("copy.largeImage");
        IssueCopy(context, IosClipboardContent.ImageData(png, PngTypeIdentifier), options: null, $"bytes={png.Length}");
    }

    // ── Copy Options ─────────────────────────────────────────────────────────

    private void OnCopyLocalOnlyTrueClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyLocalOnlyTrueClicked)}]");
        IssueCopy(
            BeginResult("copy.localOnlyTrue"),
            IosClipboardContent.PlainText(LocalOnlyBody),
            IosClipboardCopyOptions.Create(localOnly: true),
            payload: string.Empty);
    }

    private void OnCopyLocalOnlyFalseClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyLocalOnlyFalseClicked)}]");
        IssueCopy(
            BeginResult("copy.localOnlyFalse"),
            IosClipboardContent.PlainText(LocalOnlyBody),
            IosClipboardCopyOptions.Create(localOnly: false),
            payload: string.Empty);
    }

    private void OnCopyDeviceBaselineClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyDeviceBaselineClicked)}]");
        IssueCopy("copy.deviceBaseline", IosClipboardContent.PlainText(new string('B', DeviceBaselineLength)));
    }

    private void OnCopyExpiringClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyExpiringClicked)}]");
        IssueCopy(
            BeginResult("copy.expiring"),
            IosClipboardContent.PlainText(PlainTextBody),
            IosClipboardCopyOptions.Create(localOnly: false, DateTime.UtcNow.AddSeconds(ExpirationSeconds)),
            payload: $"expiresInSec={ExpirationSeconds}");
    }

    // ── Append ───────────────────────────────────────────────────────────────

    private void OnAppendPlainTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnAppendPlainTextClicked)}]");
        string marker = AppendMarkerPrefix + Guid.NewGuid().ToString("N").Substring(0, AppendMarkerSuffixLength);
        IssueAppend("append.plainText", IosClipboardContent.PlainText(marker));
    }

    private void OnAppendUrlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnAppendUrlClicked)}]");
        IssueAppend("append.url", IosClipboardContent.Url(SampleUrl));
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    private void OnReadClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnReadClicked)}]");
        IssueRead(BeginResult("read"), _activeScope);
    }

    private void IssueRead(IosClipboardSampleResultContext context, IosPasteboardScope scope)
    {
        IosClipboardManager.Instance.Read(scope, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(IssueRead)}] seq: {context.Sequence}, marker: {context.Marker}, " +
                      $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
            if (!IsScreenAlive()) return;
            if (!result.IsSuccess)
            {
                SetResult(IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
                return;
            }

            int firstItemTypes = result.Items.Count > 0 ? result.Items[0].TypeIdentifiers.Count : 0;
            string textLen = "-";
            foreach (var item in result.Items)
            {
                if (item.Text != null)
                {
                    textLen = item.Text.Length.ToString();
                    break;
                }
            }

            SetResult(IosClipboardSampleResult.FormatSuccess(
                context, $"items={result.NumberOfItems} firstItemTypes={firstItemTypes} textLen={textLen}"));
        });
    }

    private void OnReadDataPngClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnReadDataPngClicked)}]");
        IssueReadData("read.dataPng", PngTypeIdentifier);
    }

    private void OnErrReadDataInvalidUtiClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrReadDataInvalidUtiClicked)}]");
        IssueReadData("err.readDataInvalidUti", InvalidTypeIdentifier);
    }

    private void IssueReadData(string marker, string utType)
    {
        var context = BeginResult(marker);
        IosClipboardManager.Instance.ReadData(utType, _activeScope, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(IssueReadData)}] seq: {context.Sequence}, marker: {context.Marker}, " +
                      $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
            if (!IsScreenAlive()) return;
            if (!result.IsSuccess)
            {
                SetResult(IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
                return;
            }

            SetResult(IosClipboardSampleResult.FormatSuccess(
                context, $"hasData={result.HasData} utType={result.UtType ?? "-"} bytes={result.ByteCount}"));
        });
    }

    private void OnSnapshotClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSnapshotClicked)}]");
        IssueSnapshot("snapshot", matchingTypes: null);
    }

    private void OnSnapshotMatchingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSnapshotMatchingClicked)}]");
        IssueSnapshot("snapshot.matching", SnapshotMatchingTypes);
    }

    private void IssueSnapshot(string marker, string[]? matchingTypes)
    {
        var context = BeginResult(marker);
        IosClipboardManager.Instance.GetSnapshot(_activeScope, matchingTypes, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(IssueSnapshot)}] seq: {context.Sequence}, marker: {context.Marker}, " +
                      $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
            if (!IsScreenAlive()) return;
            if (!result.IsSuccess || result.Snapshot == null)
            {
                SetResult(IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
                return;
            }

            var snapshot = result.Snapshot;
            string matching = snapshot.MatchingItemIndexes == null
                ? "null"
                : snapshot.MatchingItemIndexes.Count.ToString();
            SetResult(IosClipboardSampleResult.FormatSuccess(
                context,
                $"items={snapshot.NumberOfItems} strings={snapshot.HasStrings} urls={snapshot.HasUrls} " +
                $"images={snapshot.HasImages} colors={snapshot.HasColors} matching={matching}"));
        });
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    private void OnLoadTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnLoadTextClicked)}]");
        IssueLoadItem("load.text", IosClipboardLoadRequest.Text);
    }

    private void OnLoadUrlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnLoadUrlClicked)}]");
        IssueLoadItem("load.url", IosClipboardLoadRequest.Url);
    }

    private void OnLoadImageClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnLoadImageClicked)}]");
        IssueLoadItem("load.image", IosClipboardLoadRequest.Image);
    }

    private void OnLoadFileClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnLoadFileClicked)}]");
        IssueLoadItem("load.file", IosClipboardLoadRequest.File(DataTypeIdentifier));
    }

    private void OnLoadFileCustomClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnLoadFileCustomClicked)}]");
        IssueLoadItem("load.fileCustom", IosClipboardLoadRequest.File(CustomTypeIdentifier));
    }

    private void OnCancelLoadsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelLoadsClicked)}]");
        var context = BeginResult("load.cancel");
        IosClipboardManager.Instance.CancelLoads(result => CompleteOperation(context, result, string.Empty));
    }

    private void IssueLoadItem(string marker, IosClipboardLoadRequest request)
    {
        IssueLoadItem(BeginResult(marker), request);
    }

    private void IssueLoadItem(IosClipboardSampleResultContext context, IosClipboardLoadRequest request)
    {
        IosClipboardManager.Instance.LoadItem(request, _activeScope, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(IssueLoadItem)}] seq: {context.Sequence}, marker: {context.Marker}, " +
                      $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");

            // Mandatory even after the screen is gone: the returned file is caller-owned, so the
            // request directory must be deleted whether or not anything is left to display.
            string? payload = null;
            if (result.IsSuccess && result.Item != null)
            {
                payload = DescribeLoadedItem(result.Item);
            }

            if (!IsScreenAlive()) return;
            if (!result.IsSuccess || result.Item == null)
            {
                SetResult(IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
                return;
            }

            SetResult(IosClipboardSampleResult.FormatSuccess(context, payload!));
        });
    }

    /// <remarks>
    /// Also performs the caller-owned cleanup for file results, which is why it is not a pure
    /// formatter: the size read and the directory delete are independent, so a failed size read
    /// still deletes.
    /// </remarks>
    private string DescribeLoadedItem(IosClipboardLoadedItem item)
    {
        switch (item.Kind)
        {
            case IosClipboardLoadedItemKind.Text:
                return $"kind={item.Kind} textLen={item.Text?.Length ?? 0}";
            case IosClipboardLoadedItemKind.Url:
                return $"kind={item.Kind} urlLen={item.UrlString?.Length ?? 0}";
            case IosClipboardLoadedItemKind.ImageData:
                return $"kind={item.Kind} bytes={item.Data?.Length ?? 0} utType={item.UtType ?? "-"}";
            case IosClipboardLoadedItemKind.File:
                return $"kind={item.Kind} {ConsumeLoadedFile(item.Path)}";
            default:
                return $"kind={item.Kind}";
        }
    }

    // Reads the size and then deletes the request directory the native layer created for this call.
    // The path itself is never shown or logged: it contains a temporary directory name.
    private string ConsumeLoadedFile(string? path)
    {
        long size = -1;
        if (path == null)
        {
            return IosClipboardSampleResult.FormatFileOutcome(size, cleanupSucceeded: false);
        }

        try
        {
            size = new FileInfo(path).Length;
        }
        catch (Exception ex)
        {
            // Type only: FileNotFoundException and friends put the path in their message, and the
            // returned path is a temporary directory this sample must not disclose.
            Debug.LogWarning($"[{LogTag}][{nameof(ConsumeLoadedFile)}] size read failed: " +
                             $"{IosClipboardSampleResult.DescribeException(ex)}");
        }

        bool cleaned = TryDeleteRequestDirectory(path);
        return IosClipboardSampleResult.FormatFileOutcome(size, cleaned);
    }

    private bool TryDeleteRequestDirectory(string path)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) return false;
            Directory.Delete(directory, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            // Type only: the message would contain the directory being deleted.
            Debug.LogWarning($"[{LogTag}][{nameof(TryDeleteRequestDirectory)}] cleanup failed: " +
                             $"{IosClipboardSampleResult.DescribeException(ex)}");
            return false;
        }
    }

    // ── Detect ───────────────────────────────────────────────────────────────

    private void OnDetectPatternsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDetectPatternsClicked)}]");
        IssueDetectPatterns("detect.patterns", AllDetectionPatterns);
    }

    private void OnErrDetectEmptyPatternsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrDetectEmptyPatternsClicked)}]");
        IssueDetectPatterns("err.detectEmptyPatterns", Array.Empty<IosClipboardDetectionPattern>());
    }

    private void IssueDetectPatterns(string marker, IosClipboardDetectionPattern[] patterns)
    {
        var context = BeginResult(marker);
        IosClipboardManager.Instance.DetectPatterns(patterns, _activeScope, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(IssueDetectPatterns)}] seq: {context.Sequence}, marker: {context.Marker}, " +
                      $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
            if (!IsScreenAlive()) return;
            if (!result.IsSuccess)
            {
                SetResult(IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
                return;
            }

            SetResult(IosClipboardSampleResult.FormatSuccess(context, $"patterns={result.Patterns.Count}"));
        });
    }

    private void OnDetectValuesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDetectValuesClicked)}]");
        var context = BeginResult("detect.values");
        IosClipboardManager.Instance.DetectValues(AllDetectionPatterns, _activeScope, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(OnDetectValuesClicked)}] seq: {context.Sequence}, marker: {context.Marker}, " +
                      $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
            if (!IsScreenAlive()) return;
            if (!result.IsSuccess || result.Values == null)
            {
                SetResult(IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
                return;
            }

            // Counts only: detected values are user content and must never be shown or logged.
            var values = result.Values;
            SetResult(IosClipboardSampleResult.FormatSuccess(
                context,
                $"patterns={values.DetectedPatterns.Count} emails={values.EmailAddresses.Count} " +
                $"phones={values.PhoneNumbers.Count} addresses={values.PostalAddresses.Count} " +
                $"events={values.CalendarEvents.Count} flights={values.FlightNumbers.Count} " +
                $"money={values.MoneyAmounts.Count} shipments={values.ShipmentTrackingNumbers.Count} " +
                $"links={values.Links.Count}"));
        });
    }

    // ── Observe ──────────────────────────────────────────────────────────────

    private void OnStartObservingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnStartObservingClicked)}]");
        IssueStartObserving(IosClipboardSampleObservationRequests.Start(ref _observation, _activeScope));
    }

    private void OnRestartObservingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRestartObservingClicked)}]");
        IssueStartObserving(IosClipboardSampleObservationRequests.Restart(ref _observation, _activeScope));
    }

    private void OnBusyStartObservingTwiceClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnBusyStartObservingTwiceClicked)}]");
        var (first, second) = IosClipboardSampleObservationRequests.BusyPair(ref _observation, _activeScope);
        IssueStartObserving(first);
        IssueStartObserving(second);
    }

    private void OnErrObserveMissingNamedClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrObserveMissingNamedClicked)}]");
        IssueStartObserving(IosClipboardSampleObservationRequests.MissingNamed(ref _observation));
    }

    /// <remarks>
    /// The scope travels in the request rather than being read from <c>_activeScope</c> here, so the
    /// error button can aim at a pasteboard that does not exist and the status line describes the
    /// scope this call asked for even when a CreatePasteboard completes while the start is pending.
    /// </remarks>
    private void IssueStartObserving(IosClipboardSampleStartRequest request)
    {
        var context = BeginResult(request.Marker);
        IosClipboardManager.Instance.StartObserving(
            request.TargetScope,
            onChanged: null,
            onStarted: result => CompleteStartObserving(context, request.Owner, request.TargetScope, result));

        // BeginStart already moved the state to pending, so the screen must show "starting" and
        // disable the scope and observation buttons now rather than waiting for the callback.
        RefreshObservationUI();
    }

    private void OnStopObservingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnStopObservingClicked)}]");
        IssueStopObserving("observe.stop");
    }

    /// <remarks>
    /// Also used for the deferred stop a start completing after teardown must issue, so it must not
    /// assume the screen is alive; <see cref="BeginResult"/> already skips the UI in that case.
    /// </remarks>
    private void IssueStopObserving(string marker)
    {
        var context = BeginResult(marker);
        int owner = _observation.BeginStop();
        IosClipboardManager.Instance.StopObserving(result => CompleteStopObserving(context, owner, result));
        RefreshObservationUI();
    }

    // Recomputes status and enabled state from the current observation state. Safe to call both at
    // issue time and from a completion, including when the completion arrived synchronously.
    private void RefreshObservationUI()
    {
        if (!IsScreenAlive()) return;
        UpdateStatus();
        UpdateEnabledStates();
    }

    private void CompleteStartObserving(
        IosClipboardSampleResultContext context,
        int owner,
        IosPasteboardScope targetScope,
        IosClipboardOperationResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(CompleteStartObserving)}] seq: {context.Sequence}, " +
                  $"marker: {context.Marker}, owner: {owner}, " +
                  $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");

        // Mandatory: runs even after the screen is gone, otherwise a start that completes after
        // teardown leaves the native observation running with nothing left to stop it.
        bool owned = _observation.CompleteStart(owner, result.IsSuccess);
        if (owned)
        {
            // The scope captured when this call was issued - not _activeScope, which may have been
            // replaced by a CreatePasteboard that completed while this start was pending.
            _observedScope = result.IsSuccess ? targetScope : null;

            if (result.IsSuccess && _observation.StopRequestedAfterStart && _observation.ShouldIssueStopNow())
            {
                IssueStopObserving("observe.stop.deferred");
            }
        }

        if (!IsScreenAlive()) return;
        SetResult(result.IsSuccess
            ? IosClipboardSampleResult.FormatSuccess(context, $"owned={owned}")
            : IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
        UpdateStatus();
        UpdateEnabledStates();
    }

    private void CompleteStopObserving(
        IosClipboardSampleResultContext context, int owner, IosClipboardOperationResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(CompleteStopObserving)}] seq: {context.Sequence}, " +
                  $"marker: {context.Marker}, owner: {owner}, " +
                  $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");

        // CompleteStop consumes the deferred request in both outcomes, so no stop is ever re-issued
        // from here. Native stopObserving cannot fail; a failure here is a managed rejection that a
        // retry would only repeat.
        bool owned = _observation.CompleteStop(owner, result.IsSuccess);
        if (owned && result.IsSuccess)
        {
            _observedScope = null;
        }
        if (owned && !result.IsSuccess)
        {
            Debug.LogWarning($"[{LogTag}][{nameof(CompleteStopObserving)}] stop rejected: {result.Error?.Code}");
        }

        if (!IsScreenAlive()) return;
        SetResult(result.IsSuccess
            ? IosClipboardSampleResult.FormatSuccess(context, $"owned={owned}")
            : IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
        UpdateStatus();
        UpdateEnabledStates();
    }

    private void OnCheckForegroundChangeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCheckForegroundChangeClicked)}]");
        var context = BeginResult("observe.checkForegroundChange");
        IosClipboardManager.Instance.CheckForegroundChange(_activeScope, result =>
        {
            Debug.Log($"[{LogTag}][{nameof(OnCheckForegroundChangeClicked)}] seq: {context.Sequence}, " +
                      $"marker: {context.Marker}, isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
            if (!IsScreenAlive()) return;
            SetResult(result.IsSuccess
                ? IosClipboardSampleResult.FormatSuccess(context, $"changed={result.Changed}")
                : IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
        });
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    private void OnClearActiveScopeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnClearActiveScopeClicked)}]");
        var context = BeginResult("clear");
        IosClipboardManager.Instance.Clear(_activeScope, result => CompleteOperation(context, result, string.Empty));
    }

    // ── Busy / concurrency demos ─────────────────────────────────────────────

    private void OnBusyLoadItemTwiceClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnBusyLoadItemTwiceClicked)}]");
        // Two contexts: the second call is rejected immediately with CLIPBOARD_BUSY while the first
        // is still running, so both must be able to report under their own sequence and marker.
        IssueLoadItem(BeginResult("busy.loadItem#1"), IosClipboardLoadRequest.Text);
        IssueLoadItem(BeginResult("busy.loadItem#2"), IosClipboardLoadRequest.Text);
    }

    private void OnSeedAndCancelLoadClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSeedAndCancelLoadClicked)}]");
        var seed = BeginResult("cancel.seedCopy");
        IosClipboardManager.Instance.Copy(
            IosClipboardContent.ImageData(CreateSmallPng(), PngTypeIdentifier),
            _activeScope,
            options: null,
            onResult: copyResult =>
            {
                Debug.Log($"[{LogTag}][{nameof(OnSeedAndCancelLoadClicked)}] seq: {seed.Sequence}, " +
                          $"marker: {seed.Marker}, isSuccess: {copyResult.IsSuccess}, errorCode: {copyResult.Error?.Code}");
                if (IsScreenAlive())
                {
                    SetResult(copyResult.IsSuccess
                        ? IosClipboardSampleResult.FormatSuccess(seed, string.Empty)
                        : IosClipboardSampleResult.FormatFailure(seed, RequireError(copyResult.Error)));
                }

                // Nothing to load, or the screen is gone: do not start new demo work off-screen.
                if (!copyResult.IsSuccess || !IsScreenAlive()) return;

                IssueLoadItem(BeginResult("cancel.loadImage"), IosClipboardLoadRequest.Image);

                var cancel = BeginResult("cancel.cancelLoads");
                IosClipboardManager.Instance.CancelLoads(r => CompleteOperation(cancel, r, string.Empty));
            });
    }

    // ── Error cases ──────────────────────────────────────────────────────────

    private void OnErrCopyMultipleEmptyClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyMultipleEmptyClicked)}]");
        IssueCopy("err.copyMultipleEmpty", IosClipboardContent.MultipleText(Array.Empty<string>()));
    }

    private void OnErrCopyMultiRepEmptyClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyMultiRepEmptyClicked)}]");
        IssueCopy("err.copyMultiRepEmpty", IosClipboardContent.MultiRepresentation(new Dictionary<string, byte[]>()));
    }

    private void OnErrCopyImageFileMissingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyImageFileMissingClicked)}]");
        IssueCopy("err.copyImageFileMissing", IosClipboardContent.ImageFile(MissingImagePath));
    }

    private void OnErrCopyInvalidUtiClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyInvalidUtiClicked)}]");
        IssueCopy("err.copyInvalidUti", IosClipboardContent.CustomData(CreateFileFixturePayload(), InvalidTypeIdentifier));
    }

    private void OnErrCopyInvalidUrlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyInvalidUrlClicked)}]");
        IssueCopy("err.copyInvalidUrl", IosClipboardContent.Url(InvalidUrl));
    }

    private void OnErrCopyColorOutOfRangeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyColorOutOfRangeClicked)}]");
        // Finite but outside 0.0-1.0: this reaches the native layer instead of throwing in C#.
        IssueCopy("err.copyColorOutOfRange", IosClipboardContent.Color(1.5, 0.0, 0.0, 1.0));
    }

    private void OnErrRemoveGeneralClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrRemoveGeneralClicked)}]");
        var context = BeginResult("err.removeGeneral");
        IosClipboardManager.Instance.RemovePasteboard(
            IosPasteboardScope.General, result => CompleteOperation(context, result, string.Empty));
    }

    // ── Common event handlers (shape-only logging) ───────────────────────────
    // These fire for every call and carry no way to tell which call they belong to, so they never
    // touch the result line or the scope state. ClipboardChanged is the exception: it belongs to no
    // call at all, so it is the single source of the event count and the only event that updates UI.

    private void OnClipboardOperationCompletedEvent(IosClipboardOperationResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnClipboardOperationCompletedEvent)}] operation: {result.Operation}, " +
                  $"isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
    }

    private void OnReadCompletedEvent(IosClipboardReadResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnReadCompletedEvent)}] isSuccess: {result.IsSuccess}, " +
                  $"items: {result.NumberOfItems}, errorCode: {result.Error?.Code}");
    }

    private void OnReadDataCompletedEvent(IosClipboardReadDataResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnReadDataCompletedEvent)}] isSuccess: {result.IsSuccess}, " +
                  $"hasData: {result.HasData}, bytes: {result.ByteCount}, errorCode: {result.Error?.Code}");
    }

    private void OnSnapshotCompletedEvent(IosClipboardSnapshotResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnSnapshotCompletedEvent)}] isSuccess: {result.IsSuccess}, " +
                  $"items: {result.Snapshot?.NumberOfItems}, errorCode: {result.Error?.Code}");
    }

    private void OnPasteboardCreatedEvent(IosPasteboardScopeResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnPasteboardCreatedEvent)}] isSuccess: {result.IsSuccess}, " +
                  $"scopeKind: {result.Scope?.Kind}, errorCode: {result.Error?.Code}");
    }

    private void OnPatternsDetectedEvent(IosClipboardDetectedPatternsResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnPatternsDetectedEvent)}] isSuccess: {result.IsSuccess}, " +
                  $"patterns: {result.Patterns.Count}, errorCode: {result.Error?.Code}");
    }

    private void OnValuesDetectedEvent(IosClipboardDetectedValuesResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnValuesDetectedEvent)}] isSuccess: {result.IsSuccess}, " +
                  $"patterns: {result.Values?.DetectedPatterns.Count}, errorCode: {result.Error?.Code}");
    }

    private void OnItemLoadedEvent(IosClipboardLoadedItemResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnItemLoadedEvent)}] isSuccess: {result.IsSuccess}, " +
                  $"kind: {result.Item?.Kind}, errorCode: {result.Error?.Code}");
    }

    private void OnForegroundChangeCheckedEvent(IosClipboardForegroundChangeResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnForegroundChangeCheckedEvent)}] isSuccess: {result.IsSuccess}, " +
                  $"changed: {result.Changed}, errorCode: {result.Error?.Code}");
    }

    private void OnClipboardChangedEvent(IosClipboardChangeEvent changeEvent)
    {
        _observedEventCount++;
        Debug.Log($"[{LogTag}][{nameof(OnClipboardChangedEvent)}] kind: {changeEvent.Kind}, " +
                  $"added: {changeEvent.TypesAdded.Count}, removed: {changeEvent.TypesRemoved.Count}, " +
                  $"events: {_observedEventCount}");
        if (!IsScreenAlive()) return;
        _resultSequence++;
        var context = new IosClipboardSampleResultContext(_resultSequence, "event.changed");
        SetResult(IosClipboardSampleResult.FormatSuccess(
            context,
            $"kind={changeEvent.Kind} added={changeEvent.TypesAdded.Count} removed={changeEvent.TypesRemoved.Count} " +
            $"scope={IosClipboardSampleResult.FormatScopeLabel(changeEvent.Scope)}"));
        UpdateStatus();
    }

    // ── Call helpers ─────────────────────────────────────────────────────────

    private void IssueCopy(string marker, IosClipboardContent content) =>
        IssueCopy(BeginResult(marker), content, options: null, payload: string.Empty);

    private void IssueCopy(
        IosClipboardSampleResultContext context,
        IosClipboardContent content,
        IosClipboardCopyOptions? options,
        string payload)
    {
        IosClipboardManager.Instance.Copy(
            content, _activeScope, options, result => CompleteOperation(context, result, payload));
    }

    private void IssueAppend(string marker, IosClipboardContent content)
    {
        var context = BeginResult(marker);
        IosClipboardManager.Instance.Append(
            content, _activeScope, result => CompleteOperation(context, result, string.Empty));
    }

    private void CompleteOperation(
        IosClipboardSampleResultContext context, IosClipboardOperationResult result, string payload)
    {
        Debug.Log($"[{LogTag}][{nameof(CompleteOperation)}] seq: {context.Sequence}, marker: {context.Marker}, " +
                  $"operation: {result.Operation}, isSuccess: {result.IsSuccess}, errorCode: {result.Error?.Code}");
        if (!IsScreenAlive()) return;
        SetResult(result.IsSuccess
            ? IosClipboardSampleResult.FormatSuccess(context, payload)
            : IosClipboardSampleResult.FormatFailure(context, RequireError(result.Error)));
    }

    /// <remarks>
    /// Allocates the identity of a new call. Safe to call after the screen is gone: the sequence and
    /// marker are still allocated for the log, but no UI is touched.
    /// </remarks>
    private IosClipboardSampleResultContext BeginResult(string marker)
    {
        _resultSequence++;
        var context = new IosClipboardSampleResultContext(_resultSequence, marker);
        Debug.Log($"[{LogTag}][{nameof(BeginResult)}] seq: {context.Sequence}, marker: {context.Marker}");
        if (IsScreenAlive())
        {
            SetResult(IosClipboardSampleResult.FormatRunning(context));
        }
        return context;
    }

    private bool IsScreenAlive() => this != null && isActiveAndEnabled;

    // A failed result always carries an error; this keeps the call sites free of null checks and
    // still shows something meaningful if the Manager ever changes that contract.
    private static IosClipboardErrorInfo RequireError(IosClipboardErrorInfo? error) =>
        error ?? IosClipboardErrorInfo.Create(
            IosClipboardErrorInfo.UnknownErrorCode, IosClipboardErrorInfo.UnknownErrorMessage);

    // Results are already reduced to counts and lengths, so they are safe to log; this matches the
    // Debug.Log the callers emit and keeps the on-screen line as the single formatting path.
    private void SetResult(string message)
    {
        if (_resultLabel != null)
        {
            _resultLabel.text = message;
        }

        // The result area is fixed height and scrolls. Without this, a new result inherits the
        // previous scroll position and its first lines would be hidden above the viewport.
        if (_resultScrollView != null)
        {
            _resultScrollView.scrollOffset = Vector2.zero;
        }
    }

    private void UpdateStatus()
    {
        if (_statusLabel == null) return;
        _statusLabel.text = IosClipboardSampleResult.FormatStatus(
            _activeScope, _observedScope, _observation.IsObserving, _observation.ControlPending, _observedEventCount);
    }

    private void UpdateEnabledStates()
    {
        bool canChangeScope = _observation.CanChangeScope;
        SetEnabled(_useGeneralButton, canChangeScope);
        SetEnabled(_createNamedPasteboardButton, canChangeScope);
        SetEnabled(_useFixedNamedScopeButton, canChangeScope);
        SetEnabled(_createUniquePasteboardButton, canChangeScope);
        SetEnabled(_removeActivePasteboardButton, canChangeScope);
        SetEnabled(_probeRemovedScopeButton, canChangeScope);
        SetEnabled(_startObservingButton, _observation.CanStartObserving);
        SetEnabled(_restartObservingButton, _observation.CanRestartObserving);
        SetEnabled(_stopObservingButton, _observation.CanStopObserving);
        SetEnabled(_errObserveMissingNamedButton, _observation.CanStartObserving);
        SetEnabled(_busyStartObservingTwiceButton, _observation.CanStartObserving);
    }

    private static void SetEnabled(Button? button, bool enabled)
    {
        if (button != null)
        {
            button.SetEnabled(enabled);
        }
    }

    // ── Fixture helpers ──────────────────────────────────────────────────────

    private static byte[] CreateFileFixturePayload()
    {
        var payload = new byte[FileFixtureByteCount];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = FileFixtureByte;
        }
        return payload;
    }

    private static byte[] CreateSmallPng()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
        try
        {
            texture.SetPixel(0, 0, new Color(0.2f, 0.4f, 0.8f, 1.0f));
            texture.Apply();
            return texture.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.Destroy(texture);
        }
    }

    // Noise from a fixed local PRNG: PNG compression cannot shrink it, so the encoded size lands in
    // the multi-MiB band and stays identical across runs. UnityEngine.Random is deliberately not
    // used because it would move global random state shared with the rest of the samples.
    private static byte[] CreateLargePng()
    {
        var texture = new Texture2D(LargeImageSide, LargeImageSide, TextureFormat.RGBA32, mipChain: false);
        try
        {
            var pixels = new Color32[LargeImageSide * LargeImageSide];
            uint state = LargeImageSeed;
            for (int i = 0; i < pixels.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                pixels[i] = new Color32(
                    (byte)(state & 0xFF),
                    (byte)((state >> 8) & 0xFF),
                    (byte)((state >> 16) & 0xFF),
                    byte.MaxValue);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.Destroy(texture);
        }
    }

    // Fixed file name: the file is overwritten on every click so repeated runs do not accumulate.
    private static string WriteSampleImageFile()
    {
        string path = Path.Combine(Application.persistentDataPath, SampleImageFileName);
        File.WriteAllBytes(path, CreateSmallPng());
        return path;
    }
}
#endif
