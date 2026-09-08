#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Example controller driving <see cref="WindowsClipboardManager"/> through every public entry
/// point it has.
/// </summary>
/// <remarks>
/// <para>
/// This screen is the harness for the manual verification pass, not a demo. Twenty-eight of the
/// thirty-three public methods have never run against the native layer, and neither code review
/// nor an Editor test can reach them, so pressing the buttons here is the only way any of them
/// gets executed at all.
/// </para>
/// <para>
/// No handler is platform-guarded. In the Editor the Manager answers PlatformUnavailable before it
/// looks at its own state, so the rejections are observable there too; an inner guard would trade
/// that away for a fixed string.
/// </para>
/// <para>
/// Every line carries a sequence number and a kind, so the ordering rules the design promises can
/// be read off the log: that a result is delivered outside the caller's stack, that the common
/// event precedes the per-call callback, and that an accepted request completes exactly once.
/// </para>
/// <para>
/// Clipboard content never reaches the screen or the log. Round trips are reported as a length or
/// a size plus a match flag. Error messages are shown: every failure detail in this package is a
/// fixed literal with no path from clipboard content into it.
/// </para>
/// </remarks>
public class WindowsClipboardManagerExampleController : MonoBehaviour
{
    private const string LogTag = "WindowsClipboardManagerExampleController";

    private const string CfUnicodeText = "CF_UNICODETEXT";
    private const string CfText = "CF_TEXT";
    private const string CfDib = "CF_DIB";
    private const string HtmlFormat = "HTML Format";

    /// <summary>Seconds the delayed call waits, long enough to bring another window forward.</summary>
    private const float DelayedCallSeconds = 5f;

    private const string UnknownHistoryItemId = "nativetoolkit-sample-no-such-item";

    // ── Deferred rendering ───────────────────────────────────────────────────

    /// <summary>
    /// How many times a deferred provider has been asked for bytes.
    /// </summary>
    /// <remarks>
    /// Static and touched only through <see cref="Interlocked"/>. A provider runs on whichever
    /// thread the native window pump is on, and it also runs while the application is shutting
    /// down, so it must not reach a MonoBehaviour, a VisualElement or any other Unity API. The
    /// screen reads this in Update instead.
    /// </remarks>
    private static int s_renderCount;

    [SerializeField] private UIDocument? uiDocument;

    // ── Screen state ─────────────────────────────────────────────────────────

    private Label? _resultLabel;
    private ScrollView? _resultScrollView;
    private Label? _statusLabel;
    private Label? _stateLabel;
    private TextField? _customFormatField;
    private TextField? _historyItemIdField;

    private readonly StringBuilder _resultLog = new();
    private int _resultSequence;

    private WindowsClipboardSampleState _state = WindowsClipboardSampleState.Unknown;

    // Accepted asynchronous requests that have not completed. Exactly-once is judged by watching
    // this settle back to zero once the operator stops pressing buttons.
    private int _pending;
    private int _eventCount;
    private int _changedCount;
    private int _lastChangedSequence;
    private int _shownRenderCount;

    // Anchors for the round-trip checks. Without them a read cannot tell this app's own write from
    // whatever another application put on the clipboard a moment earlier.
    private ulong _lastTextHash;
    private ulong _lastHtmlHash;
    private ulong _lastImageHash;
    private ulong _lastCustomHash;
    private IReadOnlyList<string> _lastFilePaths = Array.Empty<string>();

    private uint _lastRequestId;

    // Restored in OnDisable. The player stops stepping when it loses focus unless this is on, and
    // the not-foreground check needs frames to keep running while another window is in front.
    private bool _previousRunInBackground;

    private Coroutine? _delayedCall;

    // Captured in Awake, which Unity runs on the player loop thread. Reading "the thread that
    // asked" at the point of the check would answer true from anywhere, which is the one thing a
    // worker-thread observation must not do.
    private int _mainThreadId = -1;

    /// <summary>
    /// Every button this controller binds, paired with its handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only list of button names. Binding, unbinding and
    /// <c>WindowsClipboardSampleSceneWiringTests</c> all read it, so a typo cannot hide in one copy
    /// while another stays correct. A name that does not resolve leaves a button that silently does
    /// nothing, which on a verification harness reads as a check that was performed when it never
    /// ran.
    /// </para>
    /// <para>
    /// Exposed as internal so the wiring test can read it from an inactive instance, which needs no
    /// UIDocument and creates no Manager.
    /// </para>
    /// </remarks>
    internal (string Name, Action Handler)[] Bindings => new (string, Action)[]
    {
        ("HomeButton", OnHomeClicked),

        ("InitializeButton", OnInitializeClicked),
        ("TryShutdownButton", OnTryShutdownClicked),
        ("ShutdownWithDrainButton", OnShutdownWithDrainClicked),
        ("CanShutdownNowButton", OnCanShutdownNowClicked),
        ("ShutdownWhileDisabledButton", OnShutdownWhileDisabledClicked),
        ("ForceInitializeWhileDrainingButton", OnForceInitializeWhileDrainingClicked),
        ("QuitButton", OnQuitClicked),

        ("CopyPlainTextButton", OnCopyPlainTextClicked),
        ("CopyPlainTextEmptyButton", OnCopyPlainTextEmptyClicked),
        ("CopyLargeTextButton", OnCopyLargeTextClicked),
        ("CopyHtmlButton", OnCopyHtmlClicked),
        ("CopyFilesButton", OnCopyFilesClicked),
        ("CopyImageButton", OnCopyImageClicked),
        ("CopyCustomFormatButton", OnCopyCustomFormatClicked),
        ("CopyMultipleFormatsButton", OnCopyMultipleFormatsClicked),
        ("CopyMultipleFormatsWithImageButton", OnCopyMultipleFormatsWithImageClicked),
        ("CopyMultipleFormatsAnsiButton", OnCopyMultipleFormatsAnsiClicked),
        ("CopyMultipleFormatsDuplicateButton", OnCopyMultipleFormatsDuplicateClicked),

        ("CopySensitiveButton", OnCopySensitiveClicked),
        ("CopyExcludeHistoryButton", OnCopyExcludeHistoryClicked),
        ("CopyExcludeRoamingButton", OnCopyExcludeRoamingClicked),

        ("PastePlainTextButton", OnPastePlainTextClicked),
        ("PastePlainTextAfterClearButton", OnPastePlainTextAfterClearClicked),
        ("PasteHtmlButton", OnPasteHtmlClicked),
        ("PasteHtmlTextOnlyButton", OnPasteHtmlTextOnlyClicked),
        ("PasteFilesButton", OnPasteFilesClicked),
        ("PasteImageButton", OnPasteImageClicked),
        ("PasteCustomFormatButton", OnPasteCustomFormatClicked),

        ("HasFormatButton", OnHasFormatClicked),
        ("GetFormatsButton", OnGetFormatsClicked),
        ("GetPreferredFormatButton", OnGetPreferredFormatClicked),
        ("ClearButton", OnClearClicked),

        ("ReserveDeferredFormatsButton", OnReserveDeferredFormatsClicked),
        ("RecoverDeferredStateButton", OnRecoverDeferredStateClicked),

        ("GetHistoryAvailabilityButton", OnGetHistoryAvailabilityClicked),
        ("GetHistoryButton", OnGetHistoryClicked),
        ("RestoreLastButton", OnRestoreLastClicked),
        ("DeleteLastButton", OnDeleteLastClicked),
        ("ClearUnpinnedButton", OnClearUnpinnedClicked),
        ("CancelLastButton", OnCancelLastClicked),
        ("RestoreTwiceInOneFrameButton", OnRestoreTwiceInOneFrameClicked),
        ("IssueAndCancelInOneFrameButton", OnIssueAndCancelInOneFrameClicked),
        ("RequestAndImmediateShutdownButton", OnRequestAndImmediateShutdownClicked),

        ("GetHistoryAwaitButton", OnGetHistoryAwaitClicked),
        ("GetHistoryAwaitCancelButton", OnGetHistoryAwaitCancelClicked),
        ("GetAvailabilityAwaitButton", OnGetAvailabilityAwaitClicked),
        ("RestoreAwaitButton", OnRestoreAwaitClicked),
        ("DeleteAwaitButton", OnDeleteAwaitClicked),
        ("ClearUnpinnedAwaitButton", OnClearUnpinnedAwaitClicked),

        ("EnableHistoryEventsButton", OnEnableHistoryEventsClicked),
        ("DisableHistoryEventsButton", OnDisableHistoryEventsClicked),
        ("ResetEventCountersButton", OnResetEventCountersClicked),

        ("CopyFromWorkerThreadButton", OnCopyFromWorkerThreadClicked),
        ("GetHistoryFromWorkerThreadButton", OnGetHistoryFromWorkerThreadClicked),
        ("DelayedHistoryCallButton", OnDelayedHistoryCallClicked),

        ("ErrCopyPlainTextNullButton", OnErrCopyPlainTextNullClicked),
        ("ErrCopyFilesEmptyButton", OnErrCopyFilesEmptyClicked),
        ("ErrCopyCustomFormatBlankNameButton", OnErrCopyCustomFormatBlankNameClicked),
        ("ErrCopyMultipleFormatsEmptyButton", OnErrCopyMultipleFormatsEmptyClicked),
        ("ErrRestoreBlankIdButton", OnErrRestoreBlankIdClicked),
        ("ErrRestoreUnknownIdButton", OnErrRestoreUnknownIdClicked),
        ("ErrCancelUnknownIdButton", OnErrCancelUnknownIdClicked),
        ("ErrCopyAfterShutdownButton", OnErrCopyAfterShutdownClicked),

        ("CreateTempFilesButton", OnCreateTempFilesClicked),
        ("DeleteTempFilesButton", OnDeleteTempFilesClicked),
    };

    // Resolved in InitializeUI and keyed by the same names, so unbinding cannot drift from binding.
    private readonly Dictionary<string, (Button Button, Action Handler)> _boundButtons = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        Debug.Log($"[{LogTag}][{nameof(Awake)}] mainThreadId: {_mainThreadId}");
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

        // Not a ProjectSettings change: that would follow every other sample around. The
        // not-foreground check needs Update to keep running while another window is in front, and
        // without this the player freezes the moment focus leaves and the call lands on return.
        _previousRunInBackground = Application.runInBackground;
        Application.runInBackground = true;

        WindowsClipboardManager manager = WindowsClipboardManager.Instance;
        manager.ClipboardOperationCompleted += OnClipboardOperationCompletedEvent;
        manager.FlagChecked += OnFlagCheckedEvent;
        manager.ClipboardChanged += OnClipboardChangedEvent;
        manager.TextReadCompleted += OnTextReadCompletedEvent;
        manager.StringListReadCompleted += OnStringListReadCompletedEvent;
        manager.BytesReadCompleted += OnBytesReadCompletedEvent;
        manager.FormatPresenceChecked += OnFormatPresenceCheckedEvent;
        manager.HistoryReadCompleted += OnHistoryReadCompletedEvent;
        manager.HistoryAvailabilityChecked += OnHistoryAvailabilityCheckedEvent;
        manager.HistoryChanged += OnHistoryChangedEvent;
        manager.HistoryEnabledChanged += OnHistoryEnabledChangedEvent;
        manager.RoamingEnabledChanged += OnRoamingEnabledChangedEvent;
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");

        Application.runInBackground = _previousRunInBackground;

        if (_delayedCall != null)
        {
            StopCoroutine(_delayedCall);
            _delayedCall = null;
        }

        WindowsClipboardManager manager = WindowsClipboardManager.Instance;
        manager.ClipboardOperationCompleted -= OnClipboardOperationCompletedEvent;
        manager.FlagChecked -= OnFlagCheckedEvent;
        manager.ClipboardChanged -= OnClipboardChangedEvent;
        manager.TextReadCompleted -= OnTextReadCompletedEvent;
        manager.StringListReadCompleted -= OnStringListReadCompletedEvent;
        manager.BytesReadCompleted -= OnBytesReadCompletedEvent;
        manager.FormatPresenceChecked -= OnFormatPresenceCheckedEvent;
        manager.HistoryReadCompleted -= OnHistoryReadCompletedEvent;
        manager.HistoryAvailabilityChecked -= OnHistoryAvailabilityCheckedEvent;
        manager.HistoryChanged -= OnHistoryChangedEvent;
        manager.HistoryEnabledChanged -= OnHistoryEnabledChangedEvent;
        manager.RoamingEnabledChanged -= OnRoamingEnabledChangedEvent;
    }

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
        foreach ((Button button, Action handler) in _boundButtons.Values)
        {
            button.clicked -= handler;
        }
        _boundButtons.Clear();
    }

    /// <summary>
    /// Publishes the deferred provider's call count.
    /// </summary>
    /// <remarks>
    /// The provider cannot touch the screen itself, so this is where its counter becomes visible.
    /// Only a change redraws: the status line would otherwise be rebuilt every frame for nothing.
    /// </remarks>
    private void Update()
    {
        int rendered = Volatile.Read(ref s_renderCount);
        if (rendered == _shownRenderCount) return;
        _shownRenderCount = rendered;
        RefreshStatus();
    }

    private void InitializeUI()
    {
        Debug.Log($"[{LogTag}][{nameof(InitializeUI)}]");
        VisualElement? root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError($"[{LogTag}][{nameof(InitializeUI)}] rootVisualElement is null.");
            return;
        }

        _resultLabel = root.Q<Label>("ResultTextBlock");
        _resultScrollView = root.Q<ScrollView>("ResultScrollView");
        _statusLabel = root.Q<Label>("StatusTextBlock");
        _stateLabel = root.Q<Label>("StateTextBlock");
        _customFormatField = root.Q<TextField>("CustomFormatNameField");
        _historyItemIdField = root.Q<TextField>("HistoryItemIdField");

        foreach ((string name, Action handler) in Bindings)
        {
            Bind(root, name, handler);
        }

        RefreshStatus();
        RefreshState();
    }

    private void Bind(VisualElement root, string name, Action handler)
    {
        var button = root.Q<Button>(name);
        if (button == null)
        {
            // Loud on purpose: the alternative is a button that does nothing on a screen whose whole
            // job is proving that an operation was executed.
            Debug.LogError($"[{LogTag}][{nameof(Bind)}] Button not found: {name}");
            return;
        }

        button.clicked += handler;
        _boundButtons[name] = (button, handler);
    }

    // ── Result plumbing ──────────────────────────────────────────────────────

    /// <summary>Opens a call and returns the identity its completion quotes back.</summary>
    private WindowsClipboardSampleCall Begin(string marker)
    {
        var call = new WindowsClipboardSampleCall(++_resultSequence, marker);
        Debug.Log($"[{LogTag}] issue #{call.Sequence} {marker}");
        return call;
    }

    /// <summary>
    /// Writes the line for a synchronous return value.
    /// </summary>
    /// <remarks>
    /// The subject is the operation the result carries, never the button's own label. A handler
    /// wired to the wrong Manager call would otherwise still print the name the operator expected.
    /// </remarks>
    private void Call(
        in WindowsClipboardSampleCall call,
        string operation,
        bool isSuccess,
        WindowsClipboardErrorCode code,
        string? message,
        string shape = "")
    {
        AppendResult(WindowsClipboardSampleResult.FormatLine(
            call.Sequence, WindowsClipboardSampleResult.KindCall, operation,
            WindowsClipboardSampleResult.FormatOutcome(isSuccess, code, message, shape)));
        _state = WindowsClipboardSampleResult.Advance(_state, operation, isSuccess, code);
        RefreshState();
    }

    /// <summary>Writes the line for an asynchronous completion and closes its pending slot.</summary>
    private void Done(
        in WindowsClipboardSampleCall call,
        string operation,
        bool isSuccess,
        WindowsClipboardErrorCode code,
        string? message,
        string shape = "")
    {
        _pending--;
        AppendResult(WindowsClipboardSampleResult.FormatLine(
            call.Sequence, WindowsClipboardSampleResult.KindDone, operation,
            WindowsClipboardSampleResult.FormatOutcome(isSuccess, code, message, shape)));
        _state = WindowsClipboardSampleResult.Advance(_state, operation, isSuccess, code);
        RefreshState();
        RefreshStatus();
    }

    /// <summary>Writes the accept line for an asynchronous request and opens its pending slot.</summary>
    private void Accept(in WindowsClipboardSampleCall call, string operation, uint requestId)
    {
        _lastRequestId = requestId;
        _pending++;
        AppendResult(WindowsClipboardSampleResult.FormatAccept(call.Sequence, operation, requestId));
        RefreshStatus();
    }

    /// <summary>
    /// Opens a pending slot for an awaited call.
    /// </summary>
    /// <remarks>
    /// The Awaitable forms return no request id, so there is nothing to put on an accept line. The
    /// slot is still opened, or the pending counter would never balance for half the screen.
    /// </remarks>
    private void AcceptAwaited(in WindowsClipboardSampleCall call, string operation)
    {
        _pending++;
        AppendResult(WindowsClipboardSampleResult.FormatLine(
            call.Sequence, WindowsClipboardSampleResult.KindAccept, operation, "awaited (no requestId)"));
        RefreshStatus();
    }

    private void Local(in WindowsClipboardSampleCall call, string detail)
    {
        AppendResult(WindowsClipboardSampleResult.FormatLocal(call, detail));
        Debug.LogWarning($"[{LogTag}] #{call.Sequence} {call.Marker} local: {detail}");
    }

    private void AppendResult(string line)
    {
        _resultLog.AppendLine(line);
        if (_resultLabel != null)
        {
            _resultLabel.text = _resultLog.ToString();
        }
        _resultScrollView?.schedule.Execute(() =>
        {
            if (_resultScrollView != null)
            {
                _resultScrollView.verticalScroller.value = _resultScrollView.verticalScroller.highValue;
            }
        });
    }

    private void RefreshStatus()
    {
        if (_statusLabel == null) return;
        _statusLabel.text = WindowsClipboardSampleResult.FormatStatus(
            _pending,
            _eventCount,
            _lastChangedSequence,
            _changedCount,
            _shownRenderCount,
            WindowsClipboardSampleFixtures.CultureAnsiCodePage());
    }

    private void RefreshState()
    {
        if (_stateLabel == null) return;
        _stateLabel.text = WindowsClipboardSampleResult.FormatState(
            _state, WindowsClipboardManager.Instance.enabled);
    }

    private string CustomFormatName() =>
        _customFormatField?.value ?? WindowsClipboardSampleFixtures.CustomFormatDefaultName;

    private string HistoryItemId() => _historyItemIdField?.value ?? string.Empty;

    // ── Common events ────────────────────────────────────────────────────────

    private void LogEvent(string eventName, string detail)
    {
        _eventCount++;
        AppendResult(WindowsClipboardSampleResult.FormatLine(
            ++_resultSequence, WindowsClipboardSampleResult.KindEvent, eventName, detail));
        RefreshStatus();
    }

    private static string OutcomeOf(bool isSuccess, WindowsClipboardErrorCode code, string operation) =>
        $"{operation} {(isSuccess ? "OK" : "NG")} code={code}";

    private void OnClipboardOperationCompletedEvent(WindowsClipboardResult result) =>
        LogEvent(nameof(WindowsClipboardManager.ClipboardOperationCompleted),
            OutcomeOf(result.IsSuccess, result.ErrorCode, result.Operation));

    private void OnFlagCheckedEvent(WindowsClipboardFlagResult result) =>
        LogEvent(nameof(WindowsClipboardManager.FlagChecked),
            OutcomeOf(result.IsSuccess, result.ErrorCode, result.Operation) + $" value={result.Value}");

    private void OnTextReadCompletedEvent(WindowsClipboardTextResult result) =>
        LogEvent(nameof(WindowsClipboardManager.TextReadCompleted),
            OutcomeOf(result.IsSuccess, result.ErrorCode, result.Operation) + $" empty={result.IsEmpty}");

    private void OnStringListReadCompletedEvent(WindowsClipboardStringListResult result) =>
        LogEvent(nameof(WindowsClipboardManager.StringListReadCompleted),
            OutcomeOf(result.IsSuccess, result.ErrorCode, result.Operation) + $" count={result.Values.Count}");

    private void OnBytesReadCompletedEvent(WindowsClipboardBytesResult result) =>
        LogEvent(nameof(WindowsClipboardManager.BytesReadCompleted),
            OutcomeOf(result.IsSuccess, result.ErrorCode, result.Operation) + $" size={result.Data.Length}");

    private void OnFormatPresenceCheckedEvent(WindowsClipboardFormatPresenceResult result) =>
        LogEvent(nameof(WindowsClipboardManager.FormatPresenceChecked),
            OutcomeOf(result.IsSuccess, result.ErrorCode, result.Operation) + $" has={result.HasFormat}");

    private void OnHistoryReadCompletedEvent(WindowsClipboardHistoryResult result) =>
        LogEvent(nameof(WindowsClipboardManager.HistoryReadCompleted),
            OutcomeOf(result.IsSuccess, result.ErrorCode, result.Operation) + $" count={result.Items.Count}");

    private void OnHistoryAvailabilityCheckedEvent(WindowsClipboardAvailabilityResult result) =>
        LogEvent(nameof(WindowsClipboardManager.HistoryAvailabilityChecked),
            OutcomeOf(result.IsSuccess, result.ErrorCode, result.Operation) +
            $" history={result.HistoryEnabled} roaming={result.RoamingEnabled}");

    private void OnHistoryChangedEvent() =>
        LogEvent(nameof(WindowsClipboardManager.HistoryChanged), string.Empty);

    /// <remarks>
    /// The native toolkit's own device pass measured these firing once and then never again, and
    /// never at all when the subscription started with history already off. The fault is in the
    /// Windows event source, not in the delivery path, so silence here is the expected result and
    /// must not be reported as a defect. Read the current setting with Get History Availability.
    /// </remarks>
    private void OnHistoryEnabledChangedEvent(bool enabled) =>
        LogEvent(nameof(WindowsClipboardManager.HistoryEnabledChanged), $"enabled={enabled}");

    private void OnRoamingEnabledChangedEvent(bool enabled) =>
        LogEvent(nameof(WindowsClipboardManager.RoamingEnabledChanged), $"enabled={enabled}");

    /// <remarks>
    /// One external copy raises this about three times, so the sequence of the last one is what the
    /// status line carries and the count is kept beside it rather than in front.
    /// </remarks>
    private void OnClipboardChangedEvent()
    {
        _changedCount++;
        _eventCount++;
        _lastChangedSequence = ++_resultSequence;
        AppendResult(WindowsClipboardSampleResult.FormatLine(
            _lastChangedSequence, WindowsClipboardSampleResult.KindEvent,
            nameof(WindowsClipboardManager.ClipboardChanged), $"total={_changedCount}"));
        RefreshStatus();
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void OnHomeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHomeClicked)}]");
        if (uiDocument == null) return;
        NativeToolkitSampleNavigator.ShowTopMenu(uiDocument);
    }

    // ── Lifecycle operations ─────────────────────────────────────────────────

    private void OnInitializeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnInitializeClicked)}]");
        WindowsClipboardSampleCall call = Begin("lifecycle.initialize");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.Initialize();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    /// <remarks>
    /// The only operation that fires neither the common event nor a callback. Its line comes from
    /// the return value alone, and counters standing still afterwards is the contract working.
    /// </remarks>
    private void OnTryShutdownClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnTryShutdownClicked)}]");
        WindowsClipboardSampleCall call = Begin("lifecycle.tryShutdown");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.TryShutdown(out bool completed);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"completed={completed}");
    }

    private void OnShutdownWithDrainClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShutdownWithDrainClicked)}]");
        WindowsClipboardSampleCall call = Begin("lifecycle.shutdownWithDrain");
        AcceptAwaited(call, WindowsClipboardManager.OperationShutdown);
        WindowsClipboardManager.Instance.ShutdownWithDrain(result =>
            Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage));
    }

    /// <remarks>
    /// Turning the Manager component off is what puts the drain on its forced path: Update no
    /// longer runs, so it makes the single attempt that is possible instead of waiting for frames
    /// that will not come. A shutdown that simply completes on that attempt is the normal outcome
    /// on a healthy machine; what this observes is that it did not retry, not that it timed out.
    /// </remarks>
    private void OnShutdownWhileDisabledClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShutdownWhileDisabledClicked)}]");
        WindowsClipboardSampleCall call = Begin("lifecycle.shutdownWhileDisabled");
        WindowsClipboardManager manager = WindowsClipboardManager.Instance;
        manager.enabled = false;
        AcceptAwaited(call, WindowsClipboardManager.OperationShutdown);
        manager.ShutdownWithDrain(result =>
        {
            Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
            manager.enabled = true;
            RefreshState();
        });
        RefreshState();
    }

    private void OnCanShutdownNowClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCanShutdownNowClicked)}]");
        WindowsClipboardSampleCall call = Begin("lifecycle.canShutdownNow");
        WindowsClipboardFlagResult result = WindowsClipboardManager.Instance.CanShutdownNow();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"value={result.Value}");
    }

    /// <remarks>
    /// Both calls go out in one frame, so the initialize lands while the drain is still running and
    /// is refused with ShuttingDown. Splitting them across frames would let the drain finish first
    /// and the re-entry would never happen.
    /// </remarks>
    private void OnForceInitializeWhileDrainingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnForceInitializeWhileDrainingClicked)}]");
        WindowsClipboardSampleCall drain = Begin("lifecycle.forceInit.drain");
        AcceptAwaited(drain, WindowsClipboardManager.OperationShutdown);
        WindowsClipboardManager.Instance.ShutdownWithDrain(result =>
            Done(drain, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage));

        WindowsClipboardSampleCall init = Begin("lifecycle.forceInit.initialize");
        WindowsClipboardResult initResult = WindowsClipboardManager.Instance.Initialize();
        Call(init, initResult.Operation, initResult.IsSuccess, initResult.ErrorCode, initResult.ErrorMessage);
    }

    /// <remarks>
    /// Quitting is how the reserved formats get their last chance to render: the native window is
    /// destroyed on the shutdown path, and that is the only thing that sends WM_RENDERALLFORMATS.
    /// The native toolkit's own sample lost its deferred content here until it added the same hook.
    /// </remarks>
    private void OnQuitClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnQuitClicked)}]");
        WindowsClipboardSampleCall call = Begin("lifecycle.quit");
        Local(call, "quitRequested; the quit drain runs after this line");
        Application.Quit();
    }

    // ── Copy ─────────────────────────────────────────────────────────────────

    private void CopyText(string marker, string body, WindowsClipboardWriteOptions options)
    {
        WindowsClipboardSampleCall call = Begin(marker);
        WindowsClipboardResult result =
            WindowsClipboardManager.Instance.CopyPlainText(body, options);
        if (result.IsSuccess) _lastTextHash = WindowsClipboardSampleFixtures.HashOf(body);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"length={body.Length}");
    }

    private void OnCopyPlainTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyPlainTextClicked)}]");
        CopyText("copy.plainText",
            WindowsClipboardSampleFixtures.PlainText(_resultSequence + 1),
            WindowsClipboardWriteOptions.None);
    }

    private void OnCopyPlainTextEmptyClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyPlainTextEmptyClicked)}]");
        CopyText("copy.plainText.empty", string.Empty, WindowsClipboardWriteOptions.None);
    }

    private void OnCopyLargeTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyLargeTextClicked)}]");
        CopyText("copy.largeText",
            WindowsClipboardSampleFixtures.LargeText(), WindowsClipboardWriteOptions.None);
    }

    private void OnCopyHtmlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyHtmlClicked)}]");
        WindowsClipboardSampleCall call = Begin("copy.html");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CopyHtml(
            WindowsClipboardSampleFixtures.HtmlFragment,
            WindowsClipboardSampleFixtures.HtmlPlainFallback);
        if (result.IsSuccess)
        {
            _lastHtmlHash = WindowsClipboardSampleFixtures.HashOf(WindowsClipboardSampleFixtures.HtmlFragment);
            _lastTextHash = WindowsClipboardSampleFixtures.HashOf(WindowsClipboardSampleFixtures.HtmlPlainFallback);
        }
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"htmlLength={WindowsClipboardSampleFixtures.HtmlFragment.Length}");
    }

    private void OnCopyFilesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyFilesClicked)}]");
        WindowsClipboardSampleCall call = Begin("copy.files");
        if (_lastFilePaths.Count == 0)
        {
            Local(call, "noTempFiles; press Create Temp Files first");
            return;
        }

        WindowsClipboardResult result = WindowsClipboardManager.Instance.CopyFiles(_lastFilePaths);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"count={_lastFilePaths.Count}");
    }

    private void OnCopyImageClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyImageClicked)}]");
        WindowsClipboardSampleCall call = Begin("copy.image");
        byte[] dib = WindowsClipboardSampleFixtures.BuildDib();
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CopyImage(dib);
        if (result.IsSuccess) _lastImageHash = WindowsClipboardSampleFixtures.HashOf(dib);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"size={dib.Length}");
    }

    private void OnCopyCustomFormatClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyCustomFormatClicked)}]");
        WindowsClipboardSampleCall call = Begin("copy.customFormat");
        byte[] data = WindowsClipboardSampleFixtures.CustomBytes();
        WindowsClipboardResult result =
            WindowsClipboardManager.Instance.CopyCustomFormat(CustomFormatName(), data);
        if (result.IsSuccess) _lastCustomHash = WindowsClipboardSampleFixtures.HashOf(data);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"size={data.Length}");
    }

    private void CopyMultiple(
        string marker, IReadOnlyList<WindowsClipboardFormatPayload> items, string shape)
    {
        WindowsClipboardSampleCall call = Begin(marker);
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CopyMultipleFormats(items);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage, shape);
    }

    private void OnCopyMultipleFormatsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyMultipleFormatsClicked)}]");
        string body = WindowsClipboardSampleFixtures.PlainText(_resultSequence + 1);
        _lastTextHash = WindowsClipboardSampleFixtures.HashOf(body);
        _lastHtmlHash = WindowsClipboardSampleFixtures.HashOf(WindowsClipboardSampleFixtures.HtmlFragment);
        CopyMultiple("copy.multiple", new[]
        {
            WindowsClipboardFormatPayload.Text(CfUnicodeText, body),
            WindowsClipboardFormatPayload.Html(HtmlFormat, WindowsClipboardSampleFixtures.HtmlFragment),
        }, "count=2");
    }

    /// <remarks>The only operation that goes through the Bytes payload factory.</remarks>
    private void OnCopyMultipleFormatsWithImageClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyMultipleFormatsWithImageClicked)}]");
        string body = WindowsClipboardSampleFixtures.PlainText(_resultSequence + 1);
        byte[] dib = WindowsClipboardSampleFixtures.BuildDib();
        _lastTextHash = WindowsClipboardSampleFixtures.HashOf(body);
        _lastImageHash = WindowsClipboardSampleFixtures.HashOf(dib);
        CopyMultiple("copy.multiple.withImage", new[]
        {
            WindowsClipboardFormatPayload.Text(CfUnicodeText, body),
            WindowsClipboardFormatPayload.Bytes(CfDib, dib),
        }, $"count=2 dibSize={dib.Length}");
    }

    /// <remarks>
    /// The unicode and the ANSI format carry the same body on purpose: what is being judged is
    /// which of the two comes back intact. The body is chosen so no ANSI code page can encode it,
    /// but a machine whose code page is UTF-8 loses nothing and the check is then unobservable
    /// rather than failed. The status line carries the code page so that is visible up front.
    /// </remarks>
    private void OnCopyMultipleFormatsAnsiClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyMultipleFormatsAnsiClicked)}]");
        _lastTextHash = WindowsClipboardSampleFixtures.HashOf(WindowsClipboardSampleFixtures.AnsiLossyText);
        CopyMultiple("copy.multiple.ansi", new[]
        {
            WindowsClipboardFormatPayload.Text(CfUnicodeText, WindowsClipboardSampleFixtures.AnsiLossyText),
            WindowsClipboardFormatPayload.Text(CfText, WindowsClipboardSampleFixtures.AnsiLossyText),
        }, $"count=2 acp={WindowsClipboardSampleFixtures.CultureAnsiCodePage()}");
    }

    private void OnCopyMultipleFormatsDuplicateClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyMultipleFormatsDuplicateClicked)}]");
        CopyMultiple("copy.multiple.duplicate", new[]
        {
            WindowsClipboardFormatPayload.Text(WindowsClipboardSampleFixtures.DuplicateFormatName, "first"),
            WindowsClipboardFormatPayload.Text(WindowsClipboardSampleFixtures.DuplicateFormatName, "second"),
        }, "count=2 duplicate=true");
    }

    // ── Options ──────────────────────────────────────────────────────────────

    private void OnCopySensitiveClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopySensitiveClicked)}]");
        CopyText("options.sensitive",
            WindowsClipboardSampleFixtures.PlainText(_resultSequence + 1),
            WindowsClipboardWriteOptions.Sensitive);
    }

    private void OnCopyExcludeHistoryClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyExcludeHistoryClicked)}]");
        CopyText("options.excludeHistory",
            WindowsClipboardSampleFixtures.PlainText(_resultSequence + 1),
            WindowsClipboardWriteOptions.ExcludeHistory);
    }

    /// <remarks>
    /// Pressing this proves only that the call is accepted. Whether the flag actually keeps the
    /// content off another machine needs a second Windows device on the same account with sync
    /// enabled, which the native toolkit's device pass could not arrange either.
    /// </remarks>
    private void OnCopyExcludeRoamingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyExcludeRoamingClicked)}]");
        CopyText("options.excludeRoaming",
            WindowsClipboardSampleFixtures.PlainText(_resultSequence + 1),
            WindowsClipboardWriteOptions.ExcludeRoaming);
    }

    // ── Paste ────────────────────────────────────────────────────────────────

    private void PasteText(string marker, ulong expected)
    {
        WindowsClipboardSampleCall call = Begin(marker);
        WindowsClipboardTextResult result = WindowsClipboardManager.Instance.PastePlainText();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.DescribeText(result, expected));
    }

    private void OnPastePlainTextClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnPastePlainTextClicked)}]");
        PasteText("paste.plainText", _lastTextHash);
    }

    /// <remarks>
    /// Success with nothing in it is the correct answer on an empty clipboard. The anchor is
    /// dropped first so the match column reads as not applicable rather than as a mismatch.
    /// </remarks>
    private void OnPastePlainTextAfterClearClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnPastePlainTextAfterClearClicked)}]");
        WindowsClipboardSampleCall clear = Begin("paste.afterClear.clear");
        WindowsClipboardResult cleared = WindowsClipboardManager.Instance.Clear();
        Call(clear, cleared.Operation, cleared.IsSuccess, cleared.ErrorCode, cleared.ErrorMessage);

        _lastTextHash = 0UL;
        PasteText("paste.afterClear.paste", 0UL);
    }

    private void OnPasteHtmlClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnPasteHtmlClicked)}]");
        WindowsClipboardSampleCall call = Begin("paste.html");
        WindowsClipboardTextResult result = WindowsClipboardManager.Instance.PasteHtml();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.DescribeText(result, _lastHtmlHash));
    }

    /// <remarks>
    /// Copies HTML with no plain-text fallback, then reads it back. Nothing here is expected to
    /// fail: the point is that a fragment written without a fallback still returns as a fragment.
    /// </remarks>
    private void OnPasteHtmlTextOnlyClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnPasteHtmlTextOnlyClicked)}]");
        WindowsClipboardSampleCall copy = Begin("paste.htmlTextOnly.copy");
        WindowsClipboardResult copied =
            WindowsClipboardManager.Instance.CopyHtml(WindowsClipboardSampleFixtures.HtmlFragment);
        if (copied.IsSuccess)
        {
            _lastHtmlHash = WindowsClipboardSampleFixtures.HashOf(WindowsClipboardSampleFixtures.HtmlFragment);
        }
        Call(copy, copied.Operation, copied.IsSuccess, copied.ErrorCode, copied.ErrorMessage, "plainText=null");

        WindowsClipboardSampleCall call = Begin("paste.htmlTextOnly.paste");
        WindowsClipboardTextResult result = WindowsClipboardManager.Instance.PasteHtml();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.DescribeText(result, _lastHtmlHash));
    }

    private void OnPasteFilesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnPasteFilesClicked)}]");
        WindowsClipboardSampleCall call = Begin("paste.files");
        WindowsClipboardStringListResult result = WindowsClipboardManager.Instance.PasteFiles();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.Join(
                WindowsClipboardSampleResult.DescribeStringList(result),
                $"match={MatchFiles(result.Values)}"));
    }

    /// <summary>Compares a file read against the paths this screen last wrote.</summary>
    /// <remarks>Paths are compared, never shown: a temporary path still names the machine's user.</remarks>
    private string MatchFiles(IReadOnlyList<string> read)
    {
        if (_lastFilePaths.Count == 0) return WindowsClipboardSampleResult.NotApplicable;
        if (read.Count != _lastFilePaths.Count) return "differ";
        for (int i = 0; i < read.Count; i++)
        {
            if (!string.Equals(read[i], _lastFilePaths[i], StringComparison.OrdinalIgnoreCase))
            {
                return "differ";
            }
        }
        return "match";
    }

    private void OnPasteImageClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnPasteImageClicked)}]");
        WindowsClipboardSampleCall call = Begin("paste.image");
        WindowsClipboardBytesResult result = WindowsClipboardManager.Instance.PasteImage();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.DescribeBytes(result, _lastImageHash));
    }

    private void OnPasteCustomFormatClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnPasteCustomFormatClicked)}]");
        WindowsClipboardSampleCall call = Begin("paste.customFormat");
        WindowsClipboardBytesResult result =
            WindowsClipboardManager.Instance.PasteCustomFormat(CustomFormatName());
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.DescribeBytes(result, _lastCustomHash));
    }

    // ── Inspect ──────────────────────────────────────────────────────────────

    /// <remarks>The success flag and the presence flag are independent; both are shown.</remarks>
    private void OnHasFormatClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHasFormatClicked)}]");
        WindowsClipboardSampleCall call = Begin("inspect.hasFormat");
        WindowsClipboardFormatPresenceResult result =
            WindowsClipboardManager.Instance.HasFormat(CfUnicodeText);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"hasFormat={result.HasFormat}");
    }

    private void OnGetFormatsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetFormatsClicked)}]");
        WindowsClipboardSampleCall call = Begin("inspect.getFormats");
        WindowsClipboardStringListResult result = WindowsClipboardManager.Instance.GetFormats();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.DescribeStringList(result));
    }

    /// <remarks>
    /// The candidate set is fixed and does not include HTML Format, so a clipboard holding only
    /// HTML or only a custom format answers with an empty string. That is a success, not a failure,
    /// and the empty flag is shown so the two cannot be confused.
    /// </remarks>
    private void OnGetPreferredFormatClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetPreferredFormatClicked)}]");
        WindowsClipboardSampleCall call = Begin("inspect.getPreferredFormat");
        WindowsClipboardTextResult result = WindowsClipboardManager.Instance.GetPreferredFormat();
        // The format name is a well-known constant, not clipboard content, so it can be shown.
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"empty={result.IsEmpty} format={(string.IsNullOrEmpty(result.Text) ? "(none)" : result.Text)}");
    }

    /// <remarks>
    /// The one synchronous operation given a per-call callback as well, so the log shows the common
    /// event and the callback landing in that order.
    /// </remarks>
    private void OnClearClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnClearClicked)}]");
        WindowsClipboardSampleCall call = Begin("inspect.clear");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.Clear(callback =>
            AppendResult(WindowsClipboardSampleResult.FormatLine(
                ++_resultSequence, WindowsClipboardSampleResult.KindDone, callback.Operation,
                WindowsClipboardSampleResult.FormatOutcome(
                    callback.IsSuccess, callback.ErrorCode, callback.ErrorMessage, "perCallCallback"))));
        _lastTextHash = 0UL;
        _lastHtmlHash = 0UL;
        _lastImageHash = 0UL;
        _lastCustomHash = 0UL;
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    // ── Deferred rendering ───────────────────────────────────────────────────

    /// <remarks>
    /// The providers capture their bytes by value and touch nothing else. They run on the native
    /// window's thread and they also run while the application is shutting down, so a Unity call
    /// from inside one would be undefined at exactly the moment the check cares about.
    /// <para>
    /// Each format's provider is asked for bytes once. The size phase calls it and caches the
    /// result; the fill phase reuses that cache, because the two sizes have to agree exactly.
    /// </para>
    /// </remarks>
    private void OnReserveDeferredFormatsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnReserveDeferredFormatsClicked)}]");
        WindowsClipboardSampleCall call = Begin("deferred.reserve");

        string body = WindowsClipboardSampleFixtures.PlainText(_resultSequence + 1);
        byte[] textBytes = Encoding.Unicode.GetBytes(body + "\0");
        byte[] dib = WindowsClipboardSampleFixtures.BuildDib();
        _lastTextHash = WindowsClipboardSampleFixtures.HashOf(body);
        _lastImageHash = WindowsClipboardSampleFixtures.HashOf(dib);

        var providers = new Dictionary<string, Func<byte[]>>
        {
            [CfUnicodeText] = () => { Interlocked.Increment(ref s_renderCount); return textBytes; },
            [CfDib] = () => { Interlocked.Increment(ref s_renderCount); return dib; },
        };

        WindowsClipboardResult result =
            WindowsClipboardManager.Instance.ReserveDeferredFormats(providers);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"formats={providers.Count}");
    }

    /// <remarks>
    /// Success does not mean a recovery happened: "nothing was partial" and "the reservation is
    /// already gone" report the same way. Reaching a genuinely partial state needs a Win32 failure
    /// and a failed rollback at the same moment, which no button can arrange.
    /// </remarks>
    private void OnRecoverDeferredStateClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRecoverDeferredStateClicked)}]");
        WindowsClipboardSampleCall call = Begin("deferred.recover");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.RecoverDeferredState();
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            "successDoesNotMeanRecovered");
    }

    // ── History (callback) ───────────────────────────────────────────────────

    private void OnGetHistoryAvailabilityClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetHistoryAvailabilityClicked)}]");
        WindowsClipboardSampleCall call = Begin("history.availability");
        uint requestId = WindowsClipboardManager.Instance.GetHistoryAvailability(result =>
            Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                $"history={result.HistoryEnabled} roaming={result.RoamingEnabled}"));
        Accept(call, WindowsClipboardManager.OperationGetHistoryAvailability, requestId);
    }

    private void OnGetHistoryClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetHistoryClicked)}]");
        WindowsClipboardSampleCall call = Begin("history.get");
        uint requestId = WindowsClipboardManager.Instance.GetHistory(HandleHistory(call));
        Accept(call, WindowsClipboardManager.OperationGetHistory, requestId);
    }

    /// <summary>Shared completion for every history read, whichever form issued it.</summary>
    private Action<WindowsClipboardHistoryResult> HandleHistory(WindowsClipboardSampleCall call) =>
        result =>
        {
            RememberNewestHistoryId(result);
            Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardSampleResult.DescribeHistory(result, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        };

    /// <remarks>
    /// The id is an opaque handle the history service owns, not clipboard content, so it can be
    /// put in the field. The item's text never leaves the result.
    /// </remarks>
    private void RememberNewestHistoryId(in WindowsClipboardHistoryResult result)
    {
        if (!result.IsSuccess || result.Items.Count == 0) return;
        if (_historyItemIdField == null) return;
        _historyItemIdField.value = result.Items[0].Id;
    }

    private void OnRestoreLastClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRestoreLastClicked)}]");
        IssueStatusRequest("history.restore", WindowsClipboardManager.OperationRestoreHistoryItem,
            (manager, callback) => manager.RestoreHistoryItem(HistoryItemId(), callback));
    }

    private void OnDeleteLastClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDeleteLastClicked)}]");
        IssueStatusRequest("history.delete", WindowsClipboardManager.OperationDeleteHistoryItem,
            (manager, callback) => manager.DeleteHistoryItem(HistoryItemId(), callback));
    }

    private void OnClearUnpinnedClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnClearUnpinnedClicked)}]");
        IssueStatusRequest("history.clearUnpinned", WindowsClipboardManager.OperationClearUnpinnedHistory,
            (manager, callback) => manager.ClearUnpinnedHistory(callback));
    }

    /// <summary>Issues one of the history operations that answer with a plain result.</summary>
    private void IssueStatusRequest(
        string marker, string operation, Func<WindowsClipboardManager, Action<WindowsClipboardResult>, uint> issue)
    {
        WindowsClipboardSampleCall call = Begin(marker);
        uint requestId = issue(WindowsClipboardManager.Instance, result =>
            Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage));
        Accept(call, operation, requestId);
    }

    private void OnCancelLastClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelLastClicked)}]");
        WindowsClipboardSampleCall call = Begin("history.cancelLast");
        WindowsClipboardResult result =
            WindowsClipboardManager.Instance.CancelRequest(_lastRequestId);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"targetRequestId={_lastRequestId}");
    }

    /// <remarks>
    /// Two restores in one frame. The second meets the in-flight guard and is refused with
    /// OperationBusy, and the first still gets its own result: refusing the newcomer is what keeps
    /// the earlier promise intact.
    /// </remarks>
    private void OnRestoreTwiceInOneFrameClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRestoreTwiceInOneFrameClicked)}]");
        IssueStatusRequest("history.restoreTwice.first", WindowsClipboardManager.OperationRestoreHistoryItem,
            (manager, callback) => manager.RestoreHistoryItem(HistoryItemId(), callback));
        IssueStatusRequest("history.restoreTwice.second", WindowsClipboardManager.OperationRestoreHistoryItem,
            (manager, callback) => manager.RestoreHistoryItem(HistoryItemId(), callback));
    }

    /// <remarks>
    /// Issue and cancel without a frame in between. Whichever of the two wins, the request
    /// completes exactly once: the pending counter returning to its previous value is the check.
    /// </remarks>
    private void OnIssueAndCancelInOneFrameClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnIssueAndCancelInOneFrameClicked)}]");
        WindowsClipboardSampleCall call = Begin("history.issueAndCancel.issue");
        uint requestId = WindowsClipboardManager.Instance.GetHistory(HandleHistory(call));
        Accept(call, WindowsClipboardManager.OperationGetHistory, requestId);

        WindowsClipboardSampleCall cancel = Begin("history.issueAndCancel.cancel");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CancelRequest(requestId);
        Call(cancel, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"targetRequestId={requestId}");
    }

    /// <remarks>
    /// A request and a shutdown in one frame. The request must still complete, because a shutdown
    /// drains what it finds rather than dropping it; an Awaitable left unfinished here would hang
    /// its caller forever.
    /// </remarks>
    private void OnRequestAndImmediateShutdownClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRequestAndImmediateShutdownClicked)}]");
        WindowsClipboardSampleCall call = Begin("history.requestThenShutdown.request");
        uint requestId = WindowsClipboardManager.Instance.GetHistory(HandleHistory(call));
        Accept(call, WindowsClipboardManager.OperationGetHistory, requestId);

        WindowsClipboardSampleCall drain = Begin("history.requestThenShutdown.shutdown");
        AcceptAwaited(drain, WindowsClipboardManager.OperationShutdown);
        WindowsClipboardManager.Instance.ShutdownWithDrain(result =>
            Done(drain, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage));
    }

    // ── History (Awaitable) ──────────────────────────────────────────────────
    //
    // Cancellation does not throw here. The Awaitable completes with ErrorCode Canceled, following
    // the repository rule that a failed result is never turned into an exception. A try/catch on
    // OperationCanceledException would therefore never run, and the handler would carry on past the
    // await and touch elements that a destroy has already taken away. Every await site below tests
    // for Canceled and returns.

    private async void OnGetHistoryAwaitClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetHistoryAwaitClicked)}]");
        WindowsClipboardSampleCall call = Begin("await.history.get");
        AcceptAwaited(call, WindowsClipboardManager.OperationGetHistory);

        WindowsClipboardHistoryResult result =
            await WindowsClipboardManager.Instance.GetHistoryAsync(destroyCancellationToken);
        if (Cancelled(call, result.Operation, result.ErrorCode, result.ErrorMessage)) return;

        RememberNewestHistoryId(result);
        Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.DescribeHistory(result, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    /// <remarks>
    /// The token is already cancelled when the call is made, so the request never reaches the
    /// native layer and the Awaitable completes with Canceled straight away. Nothing is thrown.
    /// </remarks>
    private async void OnGetHistoryAwaitCancelClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetHistoryAwaitCancelClicked)}]");
        WindowsClipboardSampleCall call = Begin("await.history.getCancelled");
        AcceptAwaited(call, WindowsClipboardManager.OperationGetHistory);

        using var source = new CancellationTokenSource();
        source.Cancel();

        WindowsClipboardHistoryResult result =
            await WindowsClipboardManager.Instance.GetHistoryAsync(source.Token);
        if (Cancelled(call, result.Operation, result.ErrorCode, result.ErrorMessage)) return;

        Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            WindowsClipboardSampleResult.DescribeHistory(result, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    private async void OnGetAvailabilityAwaitClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetAvailabilityAwaitClicked)}]");
        WindowsClipboardSampleCall call = Begin("await.history.availability");
        AcceptAwaited(call, WindowsClipboardManager.OperationGetHistoryAvailability);

        WindowsClipboardAvailabilityResult result =
            await WindowsClipboardManager.Instance.GetHistoryAvailabilityAsync(destroyCancellationToken);
        if (Cancelled(call, result.Operation, result.ErrorCode, result.ErrorMessage)) return;

        Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"history={result.HistoryEnabled} roaming={result.RoamingEnabled}");
    }

    private async void OnRestoreAwaitClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRestoreAwaitClicked)}]");
        WindowsClipboardSampleCall call = Begin("await.history.restore");
        AcceptAwaited(call, WindowsClipboardManager.OperationRestoreHistoryItem);

        WindowsClipboardResult result = await WindowsClipboardManager.Instance
            .RestoreHistoryItemAsync(HistoryItemId(), destroyCancellationToken);
        if (Cancelled(call, result.Operation, result.ErrorCode, result.ErrorMessage)) return;

        Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    private async void OnDeleteAwaitClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDeleteAwaitClicked)}]");
        WindowsClipboardSampleCall call = Begin("await.history.delete");
        AcceptAwaited(call, WindowsClipboardManager.OperationDeleteHistoryItem);

        WindowsClipboardResult result = await WindowsClipboardManager.Instance
            .DeleteHistoryItemAsync(HistoryItemId(), destroyCancellationToken);
        if (Cancelled(call, result.Operation, result.ErrorCode, result.ErrorMessage)) return;

        Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    private async void OnClearUnpinnedAwaitClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnClearUnpinnedAwaitClicked)}]");
        WindowsClipboardSampleCall call = Begin("await.history.clearUnpinned");
        AcceptAwaited(call, WindowsClipboardManager.OperationClearUnpinnedHistory);

        WindowsClipboardResult result = await WindowsClipboardManager.Instance
            .ClearUnpinnedHistoryAsync(destroyCancellationToken);
        if (Cancelled(call, result.Operation, result.ErrorCode, result.ErrorMessage)) return;

        Done(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>
    /// Closes an awaited call that came back cancelled, and says whether it did.
    /// </summary>
    /// <param name="call">Identity of the awaited call.</param>
    /// <param name="operation">Operation name from the result.</param>
    /// <param name="code">Error code from the result.</param>
    /// <param name="message">Error message from the result.</param>
    /// <returns><c>true</c> when the caller must stop here.</returns>
    /// <remarks>
    /// The guard every await site needs. Carrying on after a cancellation means running against a
    /// screen the destroy has already dismantled, and that fails at the element rather than at the
    /// call, which makes it look like a UI bug instead of a missing check.
    /// </remarks>
    private bool Cancelled(
        in WindowsClipboardSampleCall call,
        string operation,
        WindowsClipboardErrorCode code,
        string? message)
    {
        if (code != WindowsClipboardErrorCode.Canceled) return false;
        Done(call, operation, false, code, message, "cancelledWithoutThrowing");
        return true;
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private void SetHistoryEvents(string marker, bool enabled)
    {
        WindowsClipboardSampleCall call = Begin(marker);
        WindowsClipboardResult result =
            WindowsClipboardManager.Instance.SetHistoryEventsEnabled(enabled);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"enabled={enabled}");
    }

    private void OnEnableHistoryEventsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnEnableHistoryEventsClicked)}]");
        SetHistoryEvents("events.enable", true);
    }

    /// <remarks>
    /// Turn this off before changing the Windows history setting. A stop that fails leaves
    /// MonitorRegisterFailed latched, and every later start is refused for the rest of the session.
    /// </remarks>
    private void OnDisableHistoryEventsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisableHistoryEventsClicked)}]");
        SetHistoryEvents("events.disable", false);
    }

    private void OnResetEventCountersClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnResetEventCountersClicked)}]");
        _eventCount = 0;
        _changedCount = 0;
        _lastChangedSequence = 0;
        Interlocked.Exchange(ref s_renderCount, 0);
        _shownRenderCount = 0;
        RefreshStatus();
    }

    // ── Threading ────────────────────────────────────────────────────────────

    /// <remarks>
    /// The Manager is captured here, on the main thread. Its Instance getter creates a GameObject
    /// when none exists, which from a worker thread is a Unity exception rather than a rejection,
    /// and the check would then be observing the wrong failure.
    /// </remarks>
    private void OnCopyFromWorkerThreadClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyFromWorkerThreadClicked)}]");
        WindowsClipboardSampleCall call = Begin("threading.copy");
        WindowsClipboardManager manager = WindowsClipboardManager.Instance;
        string body = WindowsClipboardSampleFixtures.PlainText(_resultSequence + 1);

        AcceptAwaited(call, WindowsClipboardManager.OperationCopyPlainText);
        Task.Run(() => manager.CopyPlainText(
            body,
            WindowsClipboardWriteOptions.None,
            result => Done(
                call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                $"callbackOnMainThread={IsMainThreadNow()}")));
    }

    /// <remarks>
    /// The return value cannot carry the rejection: an asynchronous call answers with a request id,
    /// and a refused one is simply zero. The code arrives on the callback, which is dispatched to
    /// the main thread even when the rejection happened on a worker.
    /// </remarks>
    private void OnGetHistoryFromWorkerThreadClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetHistoryFromWorkerThreadClicked)}]");
        WindowsClipboardSampleCall call = Begin("threading.getHistory");
        WindowsClipboardManager manager = WindowsClipboardManager.Instance;

        AcceptAwaited(call, WindowsClipboardManager.OperationGetHistory);
        Task.Run(() => manager.GetHistory(result => Done(
            call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            $"callbackOnMainThread={IsMainThreadNow()}")));
    }

    private bool IsMainThreadNow() =>
        Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    /// <remarks>
    /// A coroutine rather than a timer on a worker: the call has to be made from the main thread,
    /// and its five seconds have to pass in frames. That is also why the screen turns
    /// runInBackground on, because a player without focus stops producing frames.
    /// </remarks>
    private void OnDelayedHistoryCallClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDelayedHistoryCallClicked)}]");
        WindowsClipboardSampleCall call = Begin("threading.delayed");
        if (_delayedCall != null)
        {
            Local(call, "delayedCallAlreadyPending");
            return;
        }
        Local(call, $"bringAnotherWindowToTheFrontWithin={DelayedCallSeconds}s");
        _delayedCall = StartCoroutine(DelayedHistoryCall());
    }

    private IEnumerator DelayedHistoryCall()
    {
        yield return new WaitForSeconds(DelayedCallSeconds);
        _delayedCall = null;

        WindowsClipboardSampleCall call = Begin("threading.delayed.fire");
        uint requestId = WindowsClipboardManager.Instance.GetHistory(HandleHistory(call));
        Accept(call, WindowsClipboardManager.OperationGetHistory, requestId);
    }

    // ── Errors ───────────────────────────────────────────────────────────────

    private void OnErrCopyPlainTextNullClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyPlainTextNullClicked)}]");
        WindowsClipboardSampleCall call = Begin("error.copyPlainText.null");
        // null! rather than a nullable parameter: the Manager's contract is that null is rejected
        // with InvalidArgument, and that rejection is what this button exists to observe.
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CopyPlainText(null!);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    private void OnErrCopyFilesEmptyClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyFilesEmptyClicked)}]");
        WindowsClipboardSampleCall call = Begin("error.copyFiles.empty");
        WindowsClipboardResult result =
            WindowsClipboardManager.Instance.CopyFiles(Array.Empty<string>());
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    private void OnErrCopyCustomFormatBlankNameClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyCustomFormatBlankNameClicked)}]");
        WindowsClipboardSampleCall call = Begin("error.copyCustomFormat.blankName");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CopyCustomFormat(
            "   ", WindowsClipboardSampleFixtures.CustomBytes());
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    private void OnErrCopyMultipleFormatsEmptyClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyMultipleFormatsEmptyClicked)}]");
        WindowsClipboardSampleCall call = Begin("error.copyMultipleFormats.empty");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CopyMultipleFormats(
            Array.Empty<WindowsClipboardFormatPayload>());
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    private void OnErrRestoreBlankIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrRestoreBlankIdClicked)}]");
        IssueStatusRequest("error.restore.blankId", WindowsClipboardManager.OperationRestoreHistoryItem,
            (manager, callback) => manager.RestoreHistoryItem("   ", callback));
    }

    /// <remarks>
    /// Only blank ids are refused locally, so this one travels all the way to the history service
    /// and comes back on the callback rather than in the accept line.
    /// </remarks>
    private void OnErrRestoreUnknownIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrRestoreUnknownIdClicked)}]");
        IssueStatusRequest("error.restore.unknownId", WindowsClipboardManager.OperationRestoreHistoryItem,
            (manager, callback) => manager.RestoreHistoryItem(UnknownHistoryItemId, callback));
    }

    /// <remarks>
    /// Cancelling an id that never existed is not necessarily an error: only a zero id is refused
    /// locally, and a non-zero one that no longer matches anything can come back as a success.
    /// </remarks>
    private void OnErrCancelUnknownIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCancelUnknownIdClicked)}]");
        WindowsClipboardSampleCall call = Begin("error.cancel.unknownId");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CancelRequest(uint.MaxValue);
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage,
            "targetRequestId=uint.MaxValue");
    }

    /// <remarks>
    /// Press last. The rejection depends on how far the shutdown got: ShuttingDown while a drain is
    /// still running, NotInitializedByHost once it finished, ShutdownFailed if it gave up. After
    /// this the screen needs Initialize again before anything else works.
    /// </remarks>
    private void OnErrCopyAfterShutdownClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyAfterShutdownClicked)}]");
        WindowsClipboardSampleCall shutdown = Begin("error.afterShutdown.shutdown");
        WindowsClipboardResult shutdownResult =
            WindowsClipboardManager.Instance.TryShutdown(out bool completed);
        Call(shutdown, shutdownResult.Operation, shutdownResult.IsSuccess, shutdownResult.ErrorCode,
            shutdownResult.ErrorMessage, $"completed={completed}");

        WindowsClipboardSampleCall call = Begin("error.afterShutdown.copy");
        WindowsClipboardResult result = WindowsClipboardManager.Instance.CopyPlainText(
            WindowsClipboardSampleFixtures.PlainText(_resultSequence));
        Call(call, result.Operation, result.IsSuccess, result.ErrorCode, result.ErrorMessage);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────

    private void OnCreateTempFilesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCreateTempFilesClicked)}]");
        WindowsClipboardSampleCall call = Begin("fixtures.createTempFiles");
        _lastFilePaths = WindowsClipboardSampleFixtures.CreateTempFiles();
        Local(call, $"created={_lastFilePaths.Count}");
    }

    private void OnDeleteTempFilesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDeleteTempFilesClicked)}]");
        WindowsClipboardSampleCall call = Begin("fixtures.deleteTempFiles");
        int removed = WindowsClipboardSampleFixtures.DeleteTempFiles();
        _lastFilePaths = Array.Empty<string>();
        Local(call, $"removed={removed}");
    }
}
#endif
