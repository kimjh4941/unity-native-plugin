#!/usr/bin/env python3
"""Check a Windows Clipboard sample run against S-2, S-4 and S-8.

These three manual-verification points are the ones nobody can judge while
operating the screen: they are properties of the whole run rather than of any
one button press. The sample writes enough into Player.log to decide them
afterwards, so they are decided here instead of being left as "not done".

Scope: the Windows Clipboard sample only. The [call] / [accept] / [done] line
format lives in WindowsClipboardSampleResult.cs and no other sample controller
writes it, so pointing this at another feature's log reports nothing rather
than reporting a pass.

Usage:
    python3 scripts/check_windows_clipboard_sample_log.py [log ...]

With no argument it reads artifact/results/clipboard/logs/*.log.

On Windows `python3` may resolve to a Microsoft Store app execution alias,
which runs nothing and exits 49 - the checks then look like they passed when
none of them ran. Where `python3 -c "print(1)"` prints nothing, use `python`.

Exit status is 1 when any check fails.
"""

import glob
import io
import re
import sys
from collections import OrderedDict
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

REPO = Path(__file__).resolve().parent.parent
PACKAGE = REPO / "Packages" / "com.jonghyunkim.nativetoolkit"
UI = PACKAGE / "Runtime" / "UI" / "Windows" / "Clipboard"
UXML = (PACKAGE / "Runtime" / "Resources" / "UI" / "Windows" / "Clipboard"
        / "WindowsClipboardManagerExample.uxml")
CONTROLLER = UI / "WindowsClipboardManagerExampleController.cs"
FIXTURES = UI / "WindowsClipboardSampleFixtures.cs"
LOGS = REPO / "artifact" / "results" / "clipboard" / "logs"

CLICK = re.compile(r"\]\[(On[A-Za-z]+Clicked)\]\s*$")
ENTRY = re.compile(r"#(\d+) \[(call|accept|done|local)\] (\S+)")
ACCEPT = re.compile(r"#(\d+) \[accept\] (\S+)")
DONE = re.compile(r"#(\d+) \[done\] (\S+) call=#(\d+)")
BINDING = re.compile(r'\("([A-Za-z0-9]+)",\s*(On[A-Za-z]+)\)')
BUTTON_TEXT = re.compile(r'name="([A-Za-z0-9]+)"[^>]*text="([^"]*)"')
CONST = re.compile(r'internal const string ([A-Za-z0-9_]+) = "([^"]*)";')
BLEW_UP = re.compile(r"Exception|Stack trace|NullReference|LogError", re.I)

# The screen is entered from the top menu, whose button belongs to another controller.
FOREIGN_CLICK = "OnClipboardClicked"

# Names holding a clipboard format or the log tag rather than clipboard content.
# A format name is metadata the sample is meant to show, and some button labels
# carry one, so searching for them would report the screen's own wording.
NOT_CONTENT = {"LogTag", "CustomFormatDefaultName", "UnknownCustomFormatName",
               "DuplicateFormatName"}

# Where a temporary file would give itself away even if its name did not.
PATH_MARKERS = ["AppData", "\\Temp\\", "/Temp/", ".tmp"]


class Report:
    def __init__(self):
        self.failures = []

    def ok(self, name, detail=""):
        print("  OK   " + name + (": " + detail if detail else ""))

    def skip(self, name, why):
        print("  SKIP " + name + ": " + why)

    def check(self, condition, name, ok_detail="", fail_detail=""):
        if condition:
            self.ok(name, ok_detail)
        else:
            self.failures.append(name)
            print("  FAIL " + name + ": " + fail_detail)


def read(path):
    return io.open(str(path), encoding="utf-8", errors="replace").read()


def per_click(paths):
    """{handler: {(kind, operation): count}}, in the order the buttons were pressed."""
    seen = OrderedDict()
    for path in paths:
        handler = None
        for line in read(path).splitlines():
            click = CLICK.search(line)
            if click:
                handler = click.group(1)
                seen.setdefault(handler, OrderedDict())
                continue
            if handler is None:
                continue
            entry = ENTRY.search(line)
            if entry:
                key = (entry.group(2), entry.group(3))
                seen[handler][key] = seen[handler].get(key, 0) + 1
    return seen


def check_s2(paths, rep):
    """Every button was pressed, nothing threw, and the operations are readable."""
    if not (UXML.is_file() and CONTROLLER.is_file()):
        rep.skip("S-2 button coverage", "the sample sources are not where they were")
        return

    label = dict(BUTTON_TEXT.findall(read(UXML)))
    button_of = dict((handler, button)
                     for button, handler in BINDING.findall(read(CONTROLLER)))
    pressed = per_click(paths)

    missing = sorted(h for h in button_of if h not in pressed)
    rep.check(not missing, "S-2 every button appears in the log",
              "%d buttons, all pressed" % len(button_of),
              "never pressed: %s" % missing[:8])

    unknown = sorted(h for h in pressed if h not in button_of and h != FOREIGN_CLICK)
    rep.check(not unknown, "S-2 every click belongs to a bound button",
              "no stray handlers", "not in the bindings: %s" % unknown[:8])

    threw = ["%s:%d" % (Path(p).name, n)
             for p in paths
             for n, line in enumerate(read(p).splitlines(), 1) if BLEW_UP.search(line)]
    rep.check(not threw, "S-2 no exception or error line", "none", "%s" % threw[:5])

    # Whether an operation belongs to the button that produced it is a reading, not
    # a comparison: "Try Shutdown" correctly reports uninitClipboardManager, and a
    # name-similarity test calls that a mismatch. The pairs are printed so the
    # reading can be done, and done again the same way after a later run.
    print("")
    print("  S-2 button label -> operations the Manager reported")
    for handler, entries in pressed.items():
        if handler == FOREIGN_CLICK:
            continue
        button = button_of.get(handler)
        if button is None:
            continue
        ops = [op for (kind, op) in entries if kind != "done"]
        print("    %-44s %s" % (label.get(button, "(no text)"),
                                ", ".join(ops) if ops else "(none)"))
    print("")


def content_strings():
    """The fixture literals that must never reach the log, with their names."""
    wanted = []
    for name, value in CONST.findall(read(FIXTURES)):
        if name in NOT_CONTENT:
            continue
        # A literal such as "한글 clipboard \U0001F680" carries an escape this
        # reader would have to decode; its plain runs identify it on their own.
        for run in re.split(r"\\[A-Za-z]", value):
            if len(run.strip()) >= 4:
                wanted.append((name, run.strip()))
    return wanted


def check_s4(paths, rep):
    """No clipboard content and no file path reached the log."""
    if not FIXTURES.is_file():
        rep.skip("S-4 no content in the log", "the fixtures file is not where it was")
        return

    wanted = content_strings() + [("path marker", m) for m in PATH_MARKERS]
    leaked = []
    for path in paths:
        text = read(path)
        for name, needle in wanted:
            if needle in text:
                leaked.append("%s: %s (%r)" % (Path(path).name, name, needle))
    rep.check(not leaked, "S-4 no content in the log",
              "%d strings searched, none present" % len(wanted), "%s" % leaked[:5])


def check_s8(paths, rep):
    """Every accept has exactly one done, naming the same operation."""
    total = 0
    problems = []
    for path in paths:
        accepted, done = OrderedDict(), OrderedDict()
        for line in read(path).splitlines():
            accept = ACCEPT.search(line)
            if accept:
                accepted.setdefault(int(accept.group(1)), accept.group(2))
                continue
            finish = DONE.search(line)
            if finish:
                done.setdefault(int(finish.group(3)), []).append(finish.group(2))
        total += len(accepted)
        name = Path(path).name
        for call in accepted:
            if call not in done:
                problems.append("%s: #%d accepted, never done" % (name, call))
        for call, ops in done.items():
            if call not in accepted:
                problems.append("%s: #%d done without an accept" % (name, call))
            elif len(ops) > 1:
                problems.append("%s: #%d done %d times" % (name, call, len(ops)))
            elif ops[0] != accepted[call]:
                problems.append("%s: #%d accepted %s, done %s"
                                % (name, call, accepted[call], ops[0]))

    rep.check(not problems, "S-8 accept and done pair one to one",
              "%d requests, none outstanding" % total, "%s" % problems[:5])
    print("  NOTE S-8 the on-screen outstanding counter is not written to the log;")
    print("       what is checked here is the pairing, not the display.")


def main(argv):
    paths = argv[1:] or sorted(glob.glob(str(LOGS / "*.log")))
    paths = [p for p in paths if Path(p).is_file()]
    if not paths:
        print("error: no log files to read", file=sys.stderr)
        return 2

    print("== %d log(s)" % len(paths))
    for path in paths:
        print("   " + Path(path).name)
    print("")

    rep = Report()
    check_s2(paths, rep)
    check_s4(paths, rep)
    check_s8(paths, rep)
    print("")
    print("failures: %d" % len(rep.failures)
          + (" (%s)" % ", ".join(rep.failures) if rep.failures else ""))
    return 1 if rep.failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
