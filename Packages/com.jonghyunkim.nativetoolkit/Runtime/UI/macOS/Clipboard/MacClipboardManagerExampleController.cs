#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

/// <summary>
/// Example controller demonstrating the macOS pasteboard via <see cref="MacClipboardManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// This screen is the harness for the manual verification pass, not a decorative demo. Every
/// button either drives one of those checks or covers a public factory no other button reaches.
/// </para>
/// <para>
/// Every call captures its own <see cref="MacClipboardSampleResultContext"/> and reports through
/// that call's per-call callback. The Manager serializes only same-operation calls, so Read and
/// Snapshot genuinely overlap and a single pending-marker field would mislabel completions. The
/// common events are used for shape-only logging; only ClipboardChanged, which belongs to no
/// call, updates the screen from an event.
/// </para>
/// <para>
/// No handler is platform-guarded: in the Editor the Manager rejects every operation with 9002,
/// which is exactly what this screen is meant to show.
/// </para>
/// <para>
/// Clipboard content, base64 payloads, detected values, pasteboard names and native error
/// messages are never shown or logged. Results are reduced to counts, lengths, flags and error
/// codes. The native message is excluded deliberately: it embeds pasteboard names and uniform
/// type identifiers, and which of its cases are dynamic is a native implementation detail.
/// </para>
/// </remarks>
public class MacClipboardManagerExampleController : MonoBehaviour
{
    private const string LogTag = "MacClipboardManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    // ── Fixtures ─────────────────────────────────────────────────────────────

    private const string PlainTextBody = "Hello macOS clipboard";
    private const string UnicodeBody = "こんにちは \U0001F680 \U0001F9D1‍\U0001F4BB テスト";
    private const string HtmlMarkup = "<b>Hello</b>";
    private const string HtmlPlainFallback = "Hello";
    private const string SampleUrl = "https://unity.com";
    private const string CustomTypeIdentifier = "com.jonghyunkim.nativetoolkit.example.custom";
    private const string InvalidTypeIdentifier = "abc";
    private const string FixedScopeName = "com.jonghyunkim.nativetoolkit.example.sample";
    private const string DetectionFixture =
        "Order 12345 from https://example.com/store, contact support@example.com or +1 (408) 555-0134, " +
        "ship to 1 Infinite Loop, Cupertino, CA 95014, meeting on March 3, 2027 at 10:00, " +
        "flight AA100, total $42.50, tracking 1Z999AA10123456784";

    // Above the 10 MiB single-item threshold that puts the write on the lazy data provider path,
    // and below the 32 MiB request limit. Written as plain text so a receiving app can actually
    // paste it: with a custom identifier the paste would fail for reasons unrelated to laziness.
    private const int LargeItemBytes = 12 * 1024 * 1024;

    // Above MacClipboardLimits.MaxRequestBytes, so the C# guard rejects it with 9007.
    private const int OversizeBytes = 33 * 1024 * 1024;

    private const byte FixtureFillByte = (byte)'A';

    private static readonly MacClipboardDetectionPattern[] AllDetectionPatterns =
    {
        MacClipboardDetectionPattern.ProbableWebUrl,
        MacClipboardDetectionPattern.ProbableWebSearch,
        MacClipboardDetectionPattern.Number,
        MacClipboardDetectionPattern.Links,
        MacClipboardDetectionPattern.EmailAddresses,
        MacClipboardDetectionPattern.PhoneNumbers,
        MacClipboardDetectionPattern.PostalAddresses,
        MacClipboardDetectionPattern.CalendarEvents,
        MacClipboardDetectionPattern.FlightNumbers,
        MacClipboardDetectionPattern.MoneyAmounts,
        MacClipboardDetectionPattern.ShipmentTrackingNumbers,
    };

    // Manual check 17 asks for each of these; they run one after another because the observation
    // calls share a single-flight key and cannot overlap.
    private static readonly double[] InvalidIntervals = { 0d, 61d, -1d, double.NaN };

    // ── State ────────────────────────────────────────────────────────────────

    private Label? _resultLabel;
    private ScrollView? _resultScrollView;
    private Label? _statusLabel;

    private readonly StringBuilder _resultLog = new();
    private int _resultSequence;

    private MacPasteboardScope _activeScope = MacPasteboardScope.General;
    private MacPasteboardScope? _lastRemovedScope;
    private MacPasteboardScope? _observedScope;
    private MacPasteboardOwnership? _lastOwnership;
    private MacPasteboardOwnership? _staleOwnership;

    private MacClipboardSampleObservationState _observation;

    // Set in OnDisable so the sequential interval probe does not keep issuing native calls into a
    // screen that is already gone.
    private bool _isTornDown;
    // Ordered, not a dictionary: manual check 16 reads the replaced registration sitting at zero
    // next to the new one, so issue order is part of what is being judged.
    private readonly List<KeyValuePair<string, int>> _registrationCounts = new();
    private int _observedEventCount;

    // Freshness anchors for manual checks 4 and 25. Without the change count, another app copying
    // between our Copy and our Read would be judged as our own content. The scope is recorded too:
    // change counts are per pasteboard, so comparing one pasteboard's count against another's can
    // match by coincidence and silently judge someone else's content as ours.
    private MacPasteboardScope? _lastWrittenScope;
    private long? _lastWrittenChangeCount;
    private int _lastWrittenTypeCount;
    private bool _lastWriteWasSingleItem;
    private string? _lastWrittenType;
    private ulong _lastWrittenPayloadHash;

    private readonly SortedSet<int> _reachedCodes = new();


    /// <summary>
    /// Every button this controller binds, paired with its handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only list of button names. Binding, unbinding and
    /// <c>MacClipboardSampleSceneWiringTests</c> all read it, so a typo cannot hide in one copy
    /// while another stays correct.
    /// </para>
    /// <para>
    /// The earlier shape kept a field per button plus a separate array, and a wrong name in a
    /// bind call left every test green while the button silently stopped working: the lookup
    /// failed, one error was logged, and the screen carried on. On a screen whose purpose is
    /// driving a manual pass, that reads as a check that was performed when it never ran.
    /// </para>
    /// <para>
    /// Exposed as internal so the wiring test can read it from an inactive instance, which needs
    /// no UIDocument and starts no Manager.
    /// </para>
    /// </remarks>
    internal (string Name, Action Handler)[] Bindings => new (string, Action)[]
    {
            ("HomeButton", OnHomeClicked),
            ("UseGeneralButton", OnUseGeneralClicked),
            ("UseFixedNamedScopeButton", OnUseFixedNamedScopeClicked),
            ("CreateNamedPasteboardButton", OnCreateNamedPasteboardClicked),
            ("CreateUniquePasteboardButton", OnCreateUniquePasteboardClicked),
            ("RemoveActivePasteboardButton", OnRemoveActivePasteboardClicked),
            ("ProbeRemovedScopeButton", OnProbeRemovedScopeClicked),
            ("CopyPlainTextButton", OnCopyPlainTextClicked),
            ("CopyHtmlButton", OnCopyHtmlClicked),
            ("CopyUrlButton", OnCopyUrlClicked),
            ("CopyCustomDataButton", OnCopyCustomDataClicked),
            ("CopyMultipleItemsButton", OnCopyMultipleItemsClicked),
            ("CopyMultiRepresentationButton", OnCopyMultiRepresentationClicked),
            ("CopyDetectionFixtureButton", OnCopyDetectionFixtureClicked),
            ("CopyUnicodeButton", OnCopyUnicodeClicked),
            ("CopyLargeSingleItemButton", OnCopyLargeSingleItemClicked),
            ("CopyLocalOnlyTrueButton", OnCopyLocalOnlyTrueClicked),
            ("CopyLocalOnlyFalseButton", OnCopyLocalOnlyFalseClicked),
            ("AppendWithLastOwnershipButton", OnAppendWithLastOwnershipClicked),
            ("AppendWithStaleOwnershipButton", OnAppendWithStaleOwnershipClicked),
            ("ReadButton", OnReadClicked),
            ("ReadDataPlainTextButton", OnReadDataPlainTextClicked),
            ("ReadDataMissingTypeButton", OnReadDataMissingTypeClicked),
            ("ReadDataInvalidTypeButton", OnReadDataInvalidTypeClicked),
            ("SnapshotButton", OnSnapshotClicked),
            ("SnapshotMatchingButton", OnSnapshotMatchingClicked),
            ("DetectPatternsButton", OnDetectPatternsClicked),
            ("DetectValuesButton", OnDetectValuesClicked),
            ("DetectMetadataButton", OnDetectMetadataClicked),
            ("GetAccessBehaviorButton", OnGetAccessBehaviorClicked),
            ("StartObservingButton", OnStartObservingClicked),
            ("RestartObservingButton", OnRestartObservingClicked),
            ("StopObservingButton", OnStopObservingClicked),
            ("CheckForegroundChangeButton", OnCheckForegroundChangeClicked),
            ("ClearActiveScopeButton", OnClearActiveScopeClicked),
            ("ErrRemoveGeneralButton", OnErrRemoveGeneralClicked),
            ("ErrSnapshotEmptyFilterButton", OnErrSnapshotEmptyFilterClicked),
            ("ErrDetectEmptyPatternsButton", OnErrDetectEmptyPatternsClicked),
            ("ErrReadDataEmptyUtTypeButton", OnErrReadDataEmptyUtTypeClicked),
            ("ErrObservingIntervalMatrixButton", OnErrObservingIntervalMatrixClicked),
            ("ErrCopyOversizeButton", OnErrCopyOversizeClicked),
            ("ErrBlankScopeNameButton", OnErrBlankScopeNameClicked),
            ("ResetReachedCodesButton", OnResetReachedCodesClicked),
    };

    // Buttons resolved in InitializeUI, keyed by the same names, so unbinding cannot drift from
    // binding and no per-button field can be left dangling.
    private readonly Dictionary<string, (Button Button, Action Handler)> _boundButtons = new();

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
        MacClipboardManager manager = MacClipboardManager.Instance;
        manager.ClipboardOperationCompleted += OnClipboardOperationCompletedEvent;
        manager.OwnershipChanged += OnOwnershipChangedEvent;
        manager.ReadCompleted += OnReadCompletedEvent;
        manager.ReadDataCompleted += OnReadDataCompletedEvent;
        manager.SnapshotCompleted += OnSnapshotCompletedEvent;
        manager.ClearCompleted += OnClearCompletedEvent;
        manager.PasteboardCreated += OnPasteboardCreatedEvent;
        manager.PatternsDetected += OnPatternsDetectedEvent;
        manager.ValuesDetected += OnValuesDetectedEvent;
        manager.MetadataDetected += OnMetadataDetectedEvent;
        manager.AccessBehaviorChecked += OnAccessBehaviorCheckedEvent;
        manager.ForegroundChangeChecked += OnForegroundChangeCheckedEvent;
        manager.ClipboardChanged += OnClipboardChangedEvent;
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");

        // Stop first, unsubscribe second. Per-call callbacks are not events, so the stop result
        // still arrives after the events are gone; this order keeps the teardown decision next to
        // the state it reads.
        _isTornDown = true;
        _observation.RequestStop();
        if (_observation.ShouldIssueStopNow())
        {
            IssueStopObserving("observe.stop.teardown");
        }

        MacClipboardManager manager = MacClipboardManager.Instance;
        manager.ClipboardOperationCompleted -= OnClipboardOperationCompletedEvent;
        manager.OwnershipChanged -= OnOwnershipChangedEvent;
        manager.ReadCompleted -= OnReadCompletedEvent;
        manager.ReadDataCompleted -= OnReadDataCompletedEvent;
        manager.SnapshotCompleted -= OnSnapshotCompletedEvent;
        manager.ClearCompleted -= OnClearCompletedEvent;
        manager.PasteboardCreated -= OnPasteboardCreatedEvent;
        manager.PatternsDetected -= OnPatternsDetectedEvent;
        manager.ValuesDetected -= OnValuesDetectedEvent;
        manager.MetadataDetected -= OnMetadataDetectedEvent;
        manager.AccessBehaviorChecked -= OnAccessBehaviorCheckedEvent;
        manager.ForegroundChangeChecked -= OnForegroundChangeCheckedEvent;
        manager.ClipboardChanged -= OnClipboardChangedEvent;
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

        foreach ((string name, Action handler) in Bindings)
        {
            Bind(root, name, handler);
        }

        RefreshStatus();
        RefreshInteractivity();
    }

    private void Bind(VisualElement root, string name, Action handler)
    {
        var button = root.Q<Button>(name);
        if (button == null)
        {
            // Loud on purpose: the alternative is a button that does nothing on a screen whose
            // whole job is proving that operations work.
            Debug.LogError($"[{LogTag}][{nameof(Bind)}] Button not found: {name}");
            return;
        }

        button.clicked += handler;
        _boundButtons[name] = (button, handler);
    }

    // ── Result plumbing ──────────────────────────────────────────────────────

    /// <summary>Opens a result line against the active scope.</summary>
    private (MacClipboardSampleResultContext Context, long StartedAt) Begin(string marker) =>
        Begin(marker, _activeScope);

    /// <summary>
    /// Opens a result line and returns the identity the completion must quote back.
    /// </summary>
    /// <param name="marker">Short label for the result line.</param>
    /// <param name="target">
    /// Pasteboard this call targets. Captured now so the completion never reads
    /// <c>_activeScope</c>, which the scope buttons can change while the call is in flight.
    /// </param>
    private (MacClipboardSampleResultContext Context, long StartedAt) Begin(
        string marker, MacPasteboardScope target)
    {
        var context = new MacClipboardSampleResultContext(++_resultSequence, marker, target);
        AppendResult(MacClipboardSampleResult.FormatRunning(context));
        Debug.Log($"[{LogTag}] issue #{context.Sequence} {marker} scope: {ScopeKindOf(target)}");
        return (context, Stopwatch.GetTimestamp());
    }

    /// <summary>Closes a successful result line.</summary>
    private void Succeed(in MacClipboardSampleResultContext context, long startedAt, string payload)
    {
        string withTiming = Append(payload, $"elapsedMs={ElapsedMs(startedAt)}");
        AppendResult(MacClipboardSampleResult.FormatSuccess(context, withTiming));
        Debug.Log($"[{LogTag}] done #{context.Sequence} {context.Marker} ok");
    }

    /// <summary>Closes a failed result line and records the code for the reached-code counter.</summary>
    private void Fail(in MacClipboardSampleResultContext context, MacClipboardErrorInfo? error)
    {
        if (error == null)
        {
            // A failed result always carries an error; a null one would mean the Manager broke its
            // own contract, so it is surfaced rather than silently formatted as a success.
            AppendResult(MacClipboardSampleResult.FormatLocal(context, "missingError"));
            Debug.LogError($"[{LogTag}] #{context.Sequence} {context.Marker} failed with no error detail");
            return;
        }

        MacClipboardErrorInfo info = error.Value;
        _reachedCodes.Add(info.Code);
        AppendResult(MacClipboardSampleResult.FormatFailure(context, info));
        Debug.Log($"[{LogTag}] done #{context.Sequence} {context.Marker} errorCode: {info.Code}");
        RefreshStatus();
    }

    /// <summary>Closes a line for a rejection this screen made before reaching the Manager.</summary>
    private void Local(in MacClipboardSampleResultContext context, string detail)
    {
        AppendResult(MacClipboardSampleResult.FormatLocal(context, detail));
        Debug.LogWarning($"[{LogTag}] #{context.Sequence} {context.Marker} local: {detail}");
    }

    private static long ElapsedMs(long startedAt) =>
        (Stopwatch.GetTimestamp() - startedAt) * 1000 / Stopwatch.Frequency;

    private static string Append(string left, string right) =>
        string.IsNullOrEmpty(left) ? right : $"{left} {right}";

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
        _statusLabel.text = MacClipboardSampleResult.FormatStatus(
            _activeScope,
            _observedScope,
            _observation.IsObserving,
            _observation.ControlPending,
            _observedEventCount,
            _reachedCodes,
            _registrationCounts);
    }

    /// <summary>Counts one event against a registration and returns its new total.</summary>
    private int IncrementRegistration(string registration)
    {
        for (int i = 0; i < _registrationCounts.Count; i++)
        {
            if (_registrationCounts[i].Key != registration) continue;
            int next = _registrationCounts[i].Value + 1;
            _registrationCounts[i] = new KeyValuePair<string, int>(registration, next);
            return next;
        }

        // Reached only if a registration fires without having been recorded, which would mean the
        // start path skipped its own bookkeeping.
        Debug.LogError($"[{LogTag}][{nameof(IncrementRegistration)}] Unknown registration: {registration}");
        return 0;
    }

    /// <summary>
    /// Disables the buttons whose preconditions do not hold, so a rejection that is merely a
    /// sequencing mistake does not get mistaken for a native contract.
    /// </summary>
    private void RefreshInteractivity()
    {
        SetEnabled("UseGeneralButton", _observation.CanChangeScope);
        SetEnabled("UseFixedNamedScopeButton", _observation.CanChangeScope);
        SetEnabled("CreateNamedPasteboardButton", _observation.CanChangeScope);
        SetEnabled("CreateUniquePasteboardButton", _observation.CanChangeScope);
        SetEnabled("RemoveActivePasteboardButton", _observation.CanChangeScope);
        SetEnabled("StartObservingButton", _observation.CanStartObserving);
        SetEnabled("RestartObservingButton", _observation.CanRestartObserving);
        SetEnabled("StopObservingButton", _observation.CanStopObserving);
        SetEnabled("ProbeRemovedScopeButton", _lastRemovedScope != null);
        SetEnabled("AppendWithLastOwnershipButton", _lastOwnership != null);
        SetEnabled("AppendWithStaleOwnershipButton", _staleOwnership != null);
    }

    private void SetEnabled(string name, bool enabled)
    {
        if (_boundButtons.TryGetValue(name, out (Button Button, Action Handler) bound))
        {
            bound.Button.SetEnabled(enabled);
        }
    }

    private static string ScopeKindOf(MacPasteboardScope scope) => scope.Kind.ToString();

    /// <summary>
    /// FNV-1a over the written bytes. Only the comparison result is ever shown; the hash itself is
    /// never displayed, so a non-cryptographic digest is enough and avoids an allocation per call.
    /// </summary>
    private static ulong HashOf(byte[] bytes)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private static byte[] FilledBytes(int count)
    {
        var bytes = new byte[count];
        for (int i = 0; i < count; i++) bytes[i] = FixtureFillByte;
        return bytes;
    }

    /// <summary>Remembers what was written so checks 4 and 25 can tell our content from another app's.</summary>
    private void RememberWrite(MacClipboardContent content, string? primaryType, byte[]? primaryBytes)
    {
        _lastWriteWasSingleItem = content.Items.Count == 1;
        _lastWrittenTypeCount = content.Items.Count == 1 ? content.Items[0].Representations.Count : 0;
        _lastWrittenType = primaryType;
        _lastWrittenPayloadHash = primaryBytes == null ? 0UL : HashOf(primaryBytes);
    }

    // ── Common events (shape-only logging; the screen is driven by per-call callbacks) ──

    private void OnClipboardOperationCompletedEvent(MacClipboardOperationResult result) =>
        LogEvent(nameof(MacClipboardManager.ClipboardOperationCompleted), result.IsSuccess, result.Error, result.Operation);

    private void OnOwnershipChangedEvent(MacClipboardOwnershipResult result) =>
        LogEvent(nameof(MacClipboardManager.OwnershipChanged), result.IsSuccess, result.Error, result.Operation);

    private void OnReadCompletedEvent(MacClipboardReadResult result) =>
        LogEvent(nameof(MacClipboardManager.ReadCompleted), result.IsSuccess, result.Error);

    private void OnReadDataCompletedEvent(MacClipboardReadDataResult result) =>
        LogEvent(nameof(MacClipboardManager.ReadDataCompleted), result.IsSuccess, result.Error);

    private void OnSnapshotCompletedEvent(MacClipboardSnapshotResult result) =>
        LogEvent(nameof(MacClipboardManager.SnapshotCompleted), result.IsSuccess, result.Error);

    private void OnClearCompletedEvent(MacClipboardChangeCountResult result) =>
        LogEvent(nameof(MacClipboardManager.ClearCompleted), result.IsSuccess, result.Error);

    private void OnPasteboardCreatedEvent(MacPasteboardScopeResult result) =>
        LogEvent(nameof(MacClipboardManager.PasteboardCreated), result.IsSuccess, result.Error);

    private void OnPatternsDetectedEvent(MacClipboardDetectedPatternsResult result) =>
        LogEvent(nameof(MacClipboardManager.PatternsDetected), result.IsSuccess, result.Error);

    private void OnValuesDetectedEvent(MacClipboardDetectedValuesResult result) =>
        LogEvent(nameof(MacClipboardManager.ValuesDetected), result.IsSuccess, result.Error);

    private void OnMetadataDetectedEvent(MacClipboardDetectedMetadataResult result) =>
        LogEvent(nameof(MacClipboardManager.MetadataDetected), result.IsSuccess, result.Error);

    private void OnAccessBehaviorCheckedEvent(MacClipboardAccessBehaviorResult result) =>
        LogEvent(nameof(MacClipboardManager.AccessBehaviorChecked), result.IsSuccess, result.Error);

    private void OnForegroundChangeCheckedEvent(MacClipboardForegroundChangeResult result) =>
        LogEvent(nameof(MacClipboardManager.ForegroundChangeChecked), result.IsSuccess, result.Error);

    private static void LogEvent(
        string eventName, bool isSuccess, MacClipboardErrorInfo? error, string? operation = null)
    {
        string op = operation == null ? string.Empty : $" operation: {operation},";
        string code = error == null ? string.Empty : $" errorCode: {error.Value.Code},";
        Debug.Log($"[{LogTag}][event] {eventName}:{op}{code} isSuccess: {isSuccess}");
    }

    /// <summary>
    /// The only event that drives the screen. A change notification belongs to no call, so there
    /// is no per-call callback that could carry it.
    /// </summary>
    private void OnClipboardChangedEvent(MacClipboardChangeEvent changeEvent)
    {
        _observedEventCount++;
        Debug.Log($"[{LogTag}][event] ClipboardChanged: scopeKind: {ScopeKindOf(changeEvent.Scope)}, " +
                  $"changeCount: {changeEvent.ChangeCount}, total: {_observedEventCount}");
        AppendResult($"* changed scopeKind={ScopeKindOf(changeEvent.Scope)} " +
                     $"changeCount={changeEvent.ChangeCount} events={_observedEventCount}");
        RefreshStatus();
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void OnHomeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHomeClicked)}]");
        if (uiDocument == null) return;
        NativeToolkitSampleNavigator.ShowTopMenu(uiDocument);
    }

    private void OnResetReachedCodesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnResetReachedCodesClicked)}]");
        _reachedCodes.Clear();
        RefreshStatus();
    }

    // ── Scope ────────────────────────────────────────────────────────────────

    private void OnUseGeneralClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnUseGeneralClicked)}]");
        (MacClipboardSampleResultContext context, long _) = Begin("scope.useGeneral");
        _activeScope = MacPasteboardScope.General;
        AppendResult(MacClipboardSampleResult.FormatSuccess(
            context, $"scope={MacClipboardSampleResult.FormatScopeLabel(_activeScope)}"));
        RefreshStatus();
    }

    private void OnUseFixedNamedScopeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnUseFixedNamedScopeClicked)}]");
        (MacClipboardSampleResultContext context, long _) = Begin("scope.useFixedNamed");

        // No CreatePasteboard first: naming a pasteboard that was never created is how manual
        // check 9 reaches the unavailable path without removing anything.
        _activeScope = MacPasteboardScope.Named(FixedScopeName);
        AppendResult(MacClipboardSampleResult.FormatSuccess(
            context, $"scope={MacClipboardSampleResult.FormatScopeLabel(_activeScope)}"));
        RefreshStatus();
    }

    private void OnCreateNamedPasteboardClicked() =>
        CreatePasteboard("scope.createNamed", MacPasteboardCreationRequest.Named(FixedScopeName));

    private void OnCreateUniquePasteboardClicked() =>
        CreatePasteboard("scope.createUnique", MacPasteboardCreationRequest.Unique);

    private void CreatePasteboard(string marker, MacPasteboardCreationRequest request)
    {
        (MacClipboardSampleResultContext context, long startedAt) = Begin(marker);
        MacClipboardManager.Instance.CreatePasteboard(request, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            _activeScope = result.Scope!;
            Succeed(context, startedAt, $"scope={MacClipboardSampleResult.FormatScopeLabel(_activeScope)}");
            RefreshStatus();
            RefreshInteractivity();
        });
    }

    private void OnRemoveActivePasteboardClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveActivePasteboardClicked)}]");
        (MacClipboardSampleResultContext context, long startedAt) = Begin("scope.removeActive");
        MacPasteboardScope removed = context.Scope;

        MacClipboardManager.Instance.RemovePasteboard(removed, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            // Kept so the probe below has something that is known to be gone.
            _lastRemovedScope = removed;
            _activeScope = MacPasteboardScope.General;
            Succeed(context, startedAt, $"removed={MacClipboardSampleResult.FormatScopeLabel(removed)}");
            RefreshStatus();
            RefreshInteractivity();
        });
    }

    private void OnProbeRemovedScopeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnProbeRemovedScopeClicked)}]");
        if (_lastRemovedScope == null)
        {
            (MacClipboardSampleResultContext rejected, long _) = Begin("scope.probeRemoved");
            Local(rejected, "noRemovedScope");
            return;
        }

        (MacClipboardSampleResultContext context, long startedAt) =
            Begin("scope.probeRemoved", _lastRemovedScope);

        // Expected to fail with 1507, whose native message names the pasteboard. The failure line
        // shows the code and a token instead, which is what makes that safe to display.
        MacClipboardManager.Instance.Read(context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }
            Succeed(context, startedAt, $"items={result.Contents!.Items.Count}");
        });
    }

    // ── Copy ─────────────────────────────────────────────────────────────────

    private void OnCopyPlainTextClicked() =>
        Copy("copy.plainText", MacClipboardContent.PlainText(PlainTextBody),
            MacClipboardTypes.PlainText, Encoding.UTF8.GetBytes(PlainTextBody));

    private void OnCopyHtmlClicked() =>
        Copy("copy.html",
            MacClipboardContent.Single(MacClipboardContentItem.Html(HtmlMarkup, HtmlPlainFallback)),
            MacClipboardTypes.Html, Encoding.UTF8.GetBytes(HtmlMarkup));

    private void OnCopyUrlClicked() =>
        Copy("copy.url", MacClipboardContent.Single(MacClipboardContentItem.Url(SampleUrl)),
            MacClipboardTypes.Url, Encoding.UTF8.GetBytes(SampleUrl));

    private void OnCopyCustomDataClicked()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(PlainTextBody);
        Copy("copy.customData",
            MacClipboardContent.Single(MacClipboardContentItem.Data(CustomTypeIdentifier, bytes)),
            CustomTypeIdentifier, bytes);
    }

    private void OnCopyMultipleItemsClicked() =>
        Copy("copy.multipleItems", MacClipboardContent.Multiple(new[]
        {
            MacClipboardContentItem.PlainText(PlainTextBody),
            MacClipboardContentItem.Url(SampleUrl),
        }), null, null);

    private void OnCopyMultiRepresentationClicked()
    {
        var representations = new Dictionary<string, byte[]>
        {
            [MacClipboardTypes.PlainText] = Encoding.UTF8.GetBytes(HtmlPlainFallback),
            [MacClipboardTypes.Html] = Encoding.UTF8.GetBytes(HtmlMarkup),
        };
        Copy("copy.multiRepresentation",
            MacClipboardContent.Single(MacClipboardContentItem.FromRepresentations(representations)),
            MacClipboardTypes.PlainText, Encoding.UTF8.GetBytes(HtmlPlainFallback));
    }

    private void OnCopyDetectionFixtureClicked() =>
        Copy("copy.detectionFixture", MacClipboardContent.PlainText(DetectionFixture),
            MacClipboardTypes.PlainText, Encoding.UTF8.GetBytes(DetectionFixture));

    private void OnCopyUnicodeClicked() =>
        Copy("copy.unicode", MacClipboardContent.PlainText(UnicodeBody),
            MacClipboardTypes.PlainText, Encoding.UTF8.GetBytes(UnicodeBody));

    private void OnCopyLargeSingleItemClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCopyLargeSingleItemClicked)}] totalBytes: {LargeItemBytes}");

        // Built here rather than held in a field: a resident 12 MiB buffer would distort the peak
        // memory that manual check 24 measures.
        byte[] bytes = FilledBytes(LargeItemBytes);
        Copy("copy.largeSingleItem",
            MacClipboardContent.Single(MacClipboardContentItem.Data(MacClipboardTypes.PlainText, bytes)),
            MacClipboardTypes.PlainText, bytes);
    }

    private void OnCopyLocalOnlyTrueClicked() =>
        Copy("copy.localOnlyTrue", MacClipboardContent.PlainText(PlainTextBody),
            MacClipboardTypes.PlainText, Encoding.UTF8.GetBytes(PlainTextBody),
            MacClipboardCopyOptions.PrivacyPreservingDefault);

    private void OnCopyLocalOnlyFalseClicked() =>
        Copy("copy.localOnlyFalse", MacClipboardContent.PlainText(PlainTextBody),
            MacClipboardTypes.PlainText, Encoding.UTF8.GetBytes(PlainTextBody),
            MacClipboardCopyOptions.Create(false));

    private void OnErrCopyOversizeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrCopyOversizeClicked)}] totalBytes: {OversizeBytes}");
        byte[] bytes = FilledBytes(OversizeBytes);
        Copy("err.copyOversize",
            MacClipboardContent.Single(MacClipboardContentItem.Data(MacClipboardTypes.PlainText, bytes)),
            MacClipboardTypes.PlainText, bytes);
    }

    private void Copy(
        string marker,
        MacClipboardContent content,
        string? primaryType,
        byte[]? primaryBytes,
        MacClipboardCopyOptions? options = null)
    {
        (MacClipboardSampleResultContext context, long startedAt) = Begin(marker);
        int itemCount = content.Items.Count;

        MacClipboardManager.Instance.Copy(content, context.Scope, options, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            MacPasteboardOwnership ownership = result.Ownership!;
            _lastOwnership = ownership;
            _staleOwnership ??= ownership;
            _lastWrittenScope = ownership.Scope;
            _lastWrittenChangeCount = ownership.ChangeCount;

            // Closed before RememberWrite: hashing a 12 MiB fixture takes long enough to distort
            // the elapsed time that manual checks 22 and 24 record.
            Succeed(context, startedAt, $"itemCount={itemCount} changeCount={ownership.ChangeCount}");
            RememberWrite(content, primaryType, primaryBytes);
            RefreshInteractivity();
        });
    }

    // ── Append ───────────────────────────────────────────────────────────────

    private void OnAppendWithLastOwnershipClicked() =>
        Append("append.lastOwnership", _lastOwnership);

    private void OnAppendWithStaleOwnershipClicked() =>
        Append("append.staleOwnership", _staleOwnership);

    private void Append(string marker, MacPasteboardOwnership? ownership)
    {
        (MacClipboardSampleResultContext context, long startedAt) = Begin(marker);
        if (ownership == null)
        {
            Local(context, "noOwnership");
            return;
        }

        MacClipboardManager.Instance.Append(
            MacClipboardContent.PlainText(PlainTextBody), ownership, result =>
            {
                if (!result.IsSuccess) { Fail(context, result.Error); return; }

                // A successful append leaves the change count untouched, so the same ownership
                // stays valid for the next one.
                _lastOwnership = result.Ownership;
                Succeed(context, startedAt, $"changeCount={result.Ownership!.ChangeCount}");
                RefreshInteractivity();
            });
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    private void OnReadClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnReadClicked)}]");
        (MacClipboardSampleResultContext context, long startedAt) = Begin("read");

        MacClipboardManager.Instance.Read(context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            MacClipboardReadContents contents = result.Contents!;

            // Judged against context.Scope, the pasteboard this read was issued against. Reading
            // _activeScope here would compare with whichever scope the screen shows now, which the
            // scope buttons can have changed while the read was in flight.
            bool fresh = MacClipboardSampleResult.IsFresh(
                _lastWrittenScope, _lastWrittenChangeCount, context.Scope, contents.ChangeCount);
            int readTypes = contents.Items.Count > 0 ? contents.Items[0].Representations.Count : 0;

            bool sameTypeFound = false;
            bool hashMatches = false;
            if (fresh && _lastWrittenType != null && contents.Items.Count > 0
                && contents.Items[0].Representations.TryGetValue(_lastWrittenType, out byte[]? readBytes))
            {
                sameTypeFound = true;
                hashMatches = HashOf(readBytes) == _lastWrittenPayloadHash;
            }

            string derived = MacClipboardSampleResult.FormatDerived(
                fresh, _lastWriteWasSingleItem, _lastWrittenTypeCount, readTypes);
            string roundTrip = MacClipboardSampleResult.FormatRoundTrip(fresh, sameTypeFound, hashMatches);

            Succeed(context, startedAt,
                $"items={contents.Items.Count} changeCount={contents.ChangeCount} " +
                $"writtenTypes={_lastWrittenTypeCount} readTypes={readTypes} " +
                $"derived={derived} roundTrip={roundTrip}");
        });
    }

    private void OnReadDataPlainTextClicked() => ReadData("read.data.plainText", MacClipboardTypes.PlainText);

    private void OnReadDataMissingTypeClicked() => ReadData("read.data.missingType", MacClipboardTypes.Png);

    private void OnReadDataInvalidTypeClicked() => ReadData("read.data.invalidType", InvalidTypeIdentifier);

    private void OnErrReadDataEmptyUtTypeClicked() => ReadData("err.readDataEmptyUtType", string.Empty);

    private void ReadData(string marker, string utType)
    {
        (MacClipboardSampleResultContext context, long startedAt) = Begin(marker);
        MacClipboardManager.Instance.ReadData(utType, context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            // A type that is absent, and a type identifier that is not valid at all, are both a
            // success with no bytes. Only the length is shown.
            int length = result.Data?.Length ?? 0;
            Succeed(context, startedAt, $"hasData={result.Data != null} dataLength={length}");
        });
    }

    private void OnSnapshotClicked() => Snapshot("snapshot", null);

    private void OnSnapshotMatchingClicked() =>
        Snapshot("snapshot.matching", new[] { MacClipboardTypes.PlainText, MacClipboardTypes.Html });

    private void OnErrSnapshotEmptyFilterClicked() => Snapshot("err.snapshotEmptyFilter", Array.Empty<string>());

    private void Snapshot(string marker, IReadOnlyList<string>? matchingTypes)
    {
        (MacClipboardSampleResultContext context, long startedAt) = Begin(marker);
        MacClipboardManager.Instance.Snapshot(matchingTypes, context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            MacClipboardSnapshot snapshot = result.Snapshot!;
            int totalTypes = 0;
            foreach (IReadOnlyList<string> perItem in snapshot.ItemTypes) totalTypes += perItem.Count;

            // Type names are not shown: on a pasteboard written by another app they can be that
            // app's private identifiers.
            Succeed(context, startedAt,
                $"items={snapshot.ItemTypes.Count} totalTypes={totalTypes} " +
                $"matching={snapshot.MatchingItemIndexes.Count} changeCount={snapshot.ChangeCount}");
        });
    }

    // ── Detect ───────────────────────────────────────────────────────────────

    private void OnDetectPatternsClicked() => DetectPatterns("detect.patterns", AllDetectionPatterns);

    private void OnErrDetectEmptyPatternsClicked() =>
        DetectPatterns("err.detectEmptyPatterns", Array.Empty<MacClipboardDetectionPattern>());

    private void DetectPatterns(string marker, IReadOnlyCollection<MacClipboardDetectionPattern> patterns)
    {
        (MacClipboardSampleResultContext context, long startedAt) = Begin(marker);
        MacClipboardManager.Instance.DetectPatterns(patterns, context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            // Pattern kinds are enum names, not clipboard content, so the count alone would hide
            // which ones matched without adding safety.
            Succeed(context, startedAt,
                $"matched={result.Patterns.Count} kinds={string.Join(",", result.Patterns)}");
        });
    }

    private void OnDetectValuesClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDetectValuesClicked)}]");
        (MacClipboardSampleResultContext context, long startedAt) = Begin("detect.values");

        MacClipboardManager.Instance.DetectValues(AllDetectionPatterns, context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            MacClipboardDetectedValues values = result.Values!;

            // Per-category counts only. The detected values are clipboard content.
            Succeed(context, startedAt,
                $"links={values.Links.Count} phones={values.PhoneNumbers.Count} " +
                $"emails={values.EmailAddresses.Count} addresses={values.PostalAddresses.Count} " +
                $"events={values.CalendarEvents.Count} flights={values.FlightNumbers.Count} " +
                $"money={values.MoneyAmounts.Count} shipments={values.ShipmentTrackingNumbers.Count} " +
                $"hasWebUrl={values.ProbableWebUrl != null} hasNumber={values.Number != null}");
        });
    }

    private void OnDetectMetadataClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDetectMetadataClicked)}]");
        (MacClipboardSampleResultContext context, long startedAt) = Begin("detect.metadata");

        // Expected to fail with 1515 on plain text: the native layer cannot tell "nothing to
        // report" from "could not report", so that failure is the documented outcome.
        MacClipboardManager.Instance.DetectMetadata(context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            MacClipboardDetectedMetadata metadata = result.Metadata!;
            Succeed(context, startedAt,
                $"types={metadata.MetadataTypes.Count} " +
                $"hasContentType={metadata.ContentTypeIdentifier != null}");
        });
    }

    private void OnGetAccessBehaviorClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetAccessBehaviorClicked)}]");
        (MacClipboardSampleResultContext context, long startedAt) = Begin("accessBehavior");

        MacClipboardManager.Instance.GetAccessBehavior(context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            // A closed enum, so showing the value cannot leak anything. Unavailable is the answer
            // below macOS 15.4, and Unknown means the native layer gained a case this build does
            // not know about.
            Succeed(context, startedAt, $"behavior={result.Behavior}");
        });
    }

    // ── Observe ──────────────────────────────────────────────────────────────

    private void OnStartObservingClicked() => IssueStartObserving("observe.start", MacClipboardLimits.DefaultObservationInterval);

    private void OnRestartObservingClicked() => IssueStartObserving("observe.restart", MacClipboardLimits.DefaultObservationInterval);

    private void IssueStartObserving(string marker, double intervalSeconds)
    {
        (MacClipboardSampleResultContext context, long startedAt) = Begin(marker);
        int owner = _observation.BeginStart();
        MacPasteboardScope target = context.Scope;

        // A registration marker per start, so a replaced callback that keeps firing is visible
        // rather than being folded into one total.
        string registration = $"{marker}#{context.Sequence}";
        _registrationCounts.Add(new KeyValuePair<string, int>(registration, 0));

        RefreshStatus();
        RefreshInteractivity();

        MacClipboardManager.Instance.StartObserving(
            context.Scope,
            intervalSeconds,
            _ =>
            {
                int count = IncrementRegistration(registration);
                AppendResult($"* onChanged {registration} count={count}");
                RefreshStatus();
            },
            result =>
            {
                bool owned = _observation.CompleteStart(owner, result.IsSuccess);
                if (owned && result.IsSuccess)
                {
                    _observedScope = target;
                }

                if (result.IsSuccess)
                {
                    Succeed(context, startedAt, $"registration={registration}");
                }
                else
                {
                    Fail(context, result.Error);
                }

                if (owned) AfterControlCompletion();
            });
    }

    private void OnStopObservingClicked() => IssueStopObserving("observe.stop");

    private void IssueStopObserving(string marker)
    {
        (MacClipboardSampleResultContext context, long startedAt) = Begin(marker);
        int owner = _observation.BeginStop();

        RefreshStatus();
        RefreshInteractivity();

        MacClipboardManager.Instance.StopObserving(result =>
        {
            bool owned = _observation.CompleteStop(owner, result.IsSuccess);
            if (owned && result.IsSuccess)
            {
                _observedScope = null;
            }

            if (result.IsSuccess)
            {
                Succeed(context, startedAt, string.Empty);
            }
            else
            {
                Fail(context, result.Error);
            }

            if (owned) AfterControlCompletion();
        });
    }

    /// <summary>
    /// Runs after a control call this screen owned has completed.
    /// <para>
    /// The deferred stop is decided on whether anything is still being observed, not on whether
    /// the completion succeeded. On macOS a failed restart leaves the previous observation
    /// running, so keying this off success would walk away from a live poller.
    /// </para>
    /// </summary>
    private void AfterControlCompletion()
    {
        RefreshStatus();
        RefreshInteractivity();

        if (_observation.TakeDeferredStop())
        {
            IssueStopObserving("observe.stop.deferred");
        }
    }

    private void OnCheckForegroundChangeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCheckForegroundChangeClicked)}]");
        (MacClipboardSampleResultContext context, long startedAt) = Begin("foregroundChange");

        // Shares a per-scope tracker with observation, so while the same scope is observed this
        // reports no change almost always. That is the documented interaction, not a bug.
        MacClipboardManager.Instance.CheckForegroundChange(context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }
            Succeed(context, startedAt,
                $"changed={result.Changed} observing={_observation.IsObserving}");
        });
    }

    private void OnErrObservingIntervalMatrixClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrObservingIntervalMatrixClicked)}] values: {InvalidIntervals.Length}");

        // Sequential, not parallel: the observation calls share one single-flight key, so issuing
        // them together would make all but the first fail with 9001 instead of 1523.
        StartIntervalProbe(0);
    }

    private void StartIntervalProbe(int index)
    {
        if (index >= InvalidIntervals.Length || _isTornDown) return;

        double interval = InvalidIntervals[index];
        (MacClipboardSampleResultContext context, long startedAt) = Begin($"err.interval[{interval}]");
        int owner = _observation.BeginStart();

        if (owner == MacClipboardSampleObservationState.NonOwningToken)
        {
            // Another control call is pending, so this one would come back as 9001 rather than the
            // 1523 the check is looking for. Reporting it as busy keeps that distinction visible
            // instead of recording a rejection that means something else.
            Local(context, "observationBusy");
            return;
        }

        MacClipboardManager.Instance.StartObserving(context.Scope, interval, null, result =>
        {
            bool owned = _observation.CompleteStart(owner, result.IsSuccess);

            if (result.IsSuccess)
            {
                Succeed(context, startedAt, $"interval={interval} unexpectedlyAccepted=true");
            }
            else
            {
                Fail(context, result.Error);
            }

            if (owned) AfterControlCompletion();
            StartIntervalProbe(index + 1);
        });
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    private void OnClearActiveScopeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnClearActiveScopeClicked)}]");
        (MacClipboardSampleResultContext context, long startedAt) = Begin("clear");

        MacClipboardManager.Instance.Clear(context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }

            // The pasteboard no longer holds our write, so the freshness anchors are stale.
            _lastWrittenScope = null;
            _lastWrittenChangeCount = null;
            Succeed(context, startedAt, $"changeCount={result.ChangeCount}");
        });
    }

    // ── Errors ───────────────────────────────────────────────────────────────

    private void OnErrRemoveGeneralClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrRemoveGeneralClicked)}]");
        (MacClipboardSampleResultContext context, long startedAt) =
            Begin("err.removeGeneral", MacPasteboardScope.General);

        // Rejected natively with 1508. The C# layer does not pre-check it, so this really does
        // reach the native contract.
        MacClipboardManager.Instance.RemovePasteboard(context.Scope, result =>
        {
            if (!result.IsSuccess) { Fail(context, result.Error); return; }
            Succeed(context, startedAt, "unexpectedlyRemoved=true");
        });
    }

    private void OnErrBlankScopeNameClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnErrBlankScopeNameClicked)}]");
        (MacClipboardSampleResultContext context, long _) = Begin("err.blankScopeName");

        // The one button that drives a C# exception rather than a native contract. The native
        // parser lets a blank name through, so this factory is the only guard, and manual check
        // 17d asks to see it throw. The message is not quoted: it names the parameter.
        try
        {
            MacPasteboardScope unused = MacPasteboardScope.Named(" ");
            Local(context, $"unexpectedlyAccepted:{MacClipboardSampleResult.FormatScopeLabel(unused)}");
        }
        catch (ArgumentException exception)
        {
            Local(context, MacClipboardSampleResult.DescribeException(exception));
        }
    }
}
#endif
