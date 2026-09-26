"""What each press of the automated Windows Clipboard sample run should report.

The runs are WindowsClipboardSampleRunPlayerTests' blocks A and D: presses 1-55 of the
manual session 1 and presses 10-40 of session 2, in the same order. The expectations are
the manual verification's (artifact/features/clipboard/results/
2026-09-09-windows-clipboard-verify-manual-result-v1.md, section 1), written per press so
that check_windows_clipboard_sample_log.py can say which M item a run broke.

Each press is (button, label, patterns). The label is the M or S item, or the section of
the result the press belongs to; None where the result names none. Each pattern describes
one outcome line the press must produce:

    "operation STATUS [Code] [key=value ...]"

STATUS is OK, NG or local (a [local] line, which carries none). Code is required after NG.
The key=value pairs must be on the line; others on it are not looked at. Alternatives are
separated by " | ", and one line matching any of them satisfies the pattern. A pattern
starting with "?" may match any number of lines, none included. A line no pattern matches
fails the press, so an unexpected NG is never passed over.

Values that depend on the machine or on what history already held are left out: counts,
ages, the ANSI code page. History counts are compared with each other instead (M-14).

Where the manual record disagrees with these, the sample was fixed after it was recorded
(6b161d5, 2026-09-09), and the record shows the old behaviour:

- block A press 48, Copy Files: the old sample dropped its temp file list whenever another
  format was copied, so it answered "no temp files" instead of copying
- block D press 12, Force Initialize While Draining: the old sample never reached the
  ShuttingDown guard; it now issues a GetHistory first, so the drain outlives a frame
"""

BLOCKS = {
    "blockA": [
        ("Clipboard", "S-1", []),
        ("Initialize", "M-1", ["initClipboardManager OK"]),
        ("CopyImage", "M-6", ["copyImage OK size=296"]),
        ("PasteImage", "M-6", ["pasteImage OK empty=False match=match"]),
        ("CreateTempFiles", None, ["fixtures.createTempFiles local created=2"]),
        ("CopyFiles", "M-5", ["copyFiles OK count=2"]),
        ("PasteFiles", "M-5", ["pasteFiles OK empty=False match=match"]),
        ("CopyMultipleFormats", "M-7", ["copyMultipleFormats OK count=2"]),
        ("CopyMultipleFormatsWithImage", "M-7", ["copyMultipleFormats OK dibSize=296"]),
        ("CopyMultipleFormatsAnsi", "M-8", ["copyMultipleFormats OK count=2"]),
        ("CopyMultipleFormatsDuplicate", None, ["copyMultipleFormats NG InvalidParameter duplicate=true"]),
        ("CopyPlainText", "M-2", ["copyPlainText OK"]),
        ("Clear", None, ["clearClipboard OK"]),
        # M-3 proper pastes what Notepad copied; the run has nothing outside to copy from.
        ("PastePlainText", "M-3", ["pastePlainText OK empty=True"]),
        ("CopyHtml", "M-4", ["copyHtml OK htmlLength=31"]),
        ("PasteHtml", "M-4", ["pasteHtml OK empty=False length=31 match=match"]),
        ("CopyLargeText", "M-21", ["copyPlainText OK length=1048576"]),
        ("PastePlainText", "M-21", ["pastePlainText OK length=1048576 match=match"]),
        ("CopySensitive", "M-9", ["copyPlainText OK"]),
        ("CopyExcludeHistory", "M-9", ["copyPlainText OK"]),
        ("CopyExcludeRoaming", None, ["copyPlainText OK"]),
        ("GetHistoryAvailability", None, ["getClipboardHistoryAvailability OK history=True"]),
        ("GetHistory", "M-11", ["getClipboardHistory OK empty=False newestMatch=match"]),
        ("RestoreLast", "M-14", ["restoreHistoryItem OK"]),
        ("DeleteLast", "M-14", ["deleteHistoryItem OK"]),
        ("GetHistory", "M-14", ["getClipboardHistory OK empty=False"]),
        ("RestoreTwiceInOneFrame", "M-15", ["restoreHistoryItem NG OperationBusy", "restoreHistoryItem OK"]),
        ("IssueAndCancelInOneFrame", "M-16", ["cancelClipboardRequest OK", "getClipboardHistory NG Canceled"]),
        ("RestoreLast", "M-24", ["restoreHistoryItem OK"]),
        ("ResetEventCounters", None, []),
        ("EnableHistoryEvents", "M-10", ["setClipboardHistoryCallbacks OK enabled=True"]),
        ("CopyMultipleFormats", "M-10", ["copyMultipleFormats OK count=2"]),
        ("GetHistoryAwait", "9", ["getClipboardHistory OK empty=False"]),
        ("GetHistoryAwaitCancel", "S-9", ["getClipboardHistory NG Canceled"]),
        ("GetAvailabilityAwait", "9", ["getClipboardHistoryAvailability OK history=True"]),
        ("RestoreAwait", "9", ["restoreHistoryItem OK"]),
        ("DeleteAwait", "9", ["deleteHistoryItem OK"]),
        ("ClearUnpinnedAwait", "9", ["clearUnpinnedHistory OK"]),
        ("CopyCustomFormat", None, ["copyCustomFormat OK"]),
        ("PasteCustomFormat", None, ["pasteCustomFormat OK empty=False match=match"]),
        ("PasteCustomFormatUnknown", None, ["pasteCustomFormat OK empty=True"]),
        ("GetFormats", None, ["getClipboardFormats OK empty=False"]),
        ("GetPreferredFormat", "8.8", ["getPreferredClipboardFormat OK empty=True"]),
        ("HasFormat", None, ["hasClipboardFormat OK hasFormat=False"]),
        ("Clear", None, ["clearClipboard OK"]),
        ("CopyMultipleFormats", None, ["copyMultipleFormats OK count=2"]),
        ("GetPreferredFormat", "8.8", ["getPreferredClipboardFormat OK format=CF_UNICODETEXT"]),
        ("CopyFiles", None, ["copyFiles OK count=2"]),
        ("GetFormats", None, ["getClipboardFormats OK empty=False"]),
        ("GetPreferredFormat", "8.8", ["getPreferredClipboardFormat OK format=CF_HDROP"]),
        ("CopyImage", None, ["copyImage OK size=296"]),
        ("GetPreferredFormat", "8.8", ["getPreferredClipboardFormat OK format=CF_DIB"]),
        ("CreateTempFiles", None, ["fixtures.createTempFiles local created=2"]),
        ("CopyFiles", None, ["copyFiles OK count=2"]),
        ("GetPreferredFormat", "8.8", ["getPreferredClipboardFormat OK format=CF_HDROP"]),
    ],
    "blockD": [
        ("Clipboard", "S-1", []),
        ("Initialize", "M-1", ["initClipboardManager OK"]),
        ("ErrCopyPlainTextNull", "D", ["copyPlainText NG InvalidArgument"]),
        ("ErrCopyFilesEmpty", "D", ["copyFiles NG InvalidArgument"]),
        ("ErrCopyCustomFormatBlankName", "D", ["copyCustomFormat NG InvalidArgument"]),
        ("ErrCopyMultipleFormatsEmpty", "D", ["copyMultipleFormats NG InvalidArgument"]),
        ("ErrRestoreBlankId", "D", ["restoreHistoryItem NG InvalidArgument"]),
        ("ErrRestoreUnknownId", "D", ["restoreHistoryItem NG ItemDeleted"]),
        ("ErrCancelUnknownId", "D", ["cancelClipboardRequest NG InvalidParameter"]),
        ("ErrCopyAfterShutdown", "D", ["uninitClipboardManager OK completed=True",
                                       "copyPlainText NG NotInitializedByHost"]),
        ("Initialize", None, ["initClipboardManager OK"]),
        # Retried once a frame up to three times; the frames before the drain starts succeed.
        ("ForceInitializeWhileDraining", None, ["?initClipboardManager OK",
                                               "getClipboardHistory NG Canceled",
                                               "initClipboardManager NG ShuttingDown",
                                               "uninitClipboardManager OK"]),
        ("Initialize", None, ["initClipboardManager OK"]),
        ("CopyFromWorkerThread", "S-7", ["copyPlainText NG MainThreadRequired callbackOnMainThread=True"]),
        ("GetHistoryFromWorkerThread", "S-7", ["getClipboardHistory NG MainThreadRequired callbackOnMainThread=True"]),
        ("CopyPlainTextEmpty", None, ["copyPlainText OK length=0"]),
        ("PasteHtmlTextOnly", None, ["copyHtml OK plainText=null", "pasteHtml OK empty=False match=match"]),
        # The last request finished long ago, so there is nothing left to cancel.
        ("CancelLast", None, ["cancelClipboardRequest NG InvalidParameter"]),
        ("ClearUnpinned", None, ["clearUnpinnedHistory OK"]),
        ("PastePlainTextAfterClear", None, ["clearClipboard OK", "pastePlainText OK empty=True"]),
        ("DeleteTempFiles", None, ["fixtures.deleteTempFiles local removed=2"]),
        # 4.1 is about the request being answered exactly once, not dropped. Usually the drain
        # answers it Canceled; once (2026-09-26) the history came back first, which is as good.
        ("RequestAndImmediateShutdown", "4.1", ["getClipboardHistory NG Canceled | getClipboardHistory OK",
                                                "uninitClipboardManager OK"]),
        ("Initialize", None, ["initClipboardManager OK"]),
        ("TryShutdown", None, ["uninitClipboardManager OK completed=True"]),
        ("Initialize", None, ["initClipboardManager OK"]),
        ("ShutdownWithDrain", None, ["uninitClipboardManager OK"]),
        ("Home", "S-5", []),
        ("Clipboard", "S-5", []),
        ("ResetEventCounters", None, []),
        ("Home", "S-5", []),
        ("Clipboard", "S-5", []),
        ("Initialize", None, ["initClipboardManager OK"]),
        ("ResetEventCounters", None, []),
    ],
}

# M-14: Delete Last takes one item out, so the Get History after it (press 26) finds one
# fewer than the one before Restore Last (press 23). Restoring moves an item, not adds one.
HISTORY_COUNT_DROPS = {"blockA": [(23, 26, 1)]}
