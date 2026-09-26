#!/usr/bin/env python3
"""Check a Windows Clipboard sample run against S-2, S-4 and S-8.

These three manual-verification points are the ones nobody can judge while
operating the screen: they are properties of the whole run rather than of any
one button press. The sample writes enough into Player.log to decide them
afterwards, so they are decided here instead of being left as "not done".

For the automated runs (windows-clipboard-sample-run-<block>.log) it also checks
each press's outcome against windows_clipboard_sample_expected.py, the manual
verification's results written per press, and names the M item a run broke.
S-2 alone passes a run in which every button answered NG.

Scope: the Windows Clipboard sample only. The [call] / [accept] / [done] line
format lives in WindowsClipboardSampleResult.cs and no other sample controller
writes it, so pointing this at another feature's log reports nothing rather
than reporting a pass.

Usage:
    python3 scripts/check_windows_clipboard_sample_log.py [--not-automated A,B,...]
        [--test-results RESULTS.xml] [--player-log NAME=PATH] [log ...]

--not-automated names buttons a partial automated run is known not to press
(WindowsClipboardSampleRunPlayerTests.NotYetAutomated). S-2 then reports PART
for them instead of passing, and fails if any of them was pressed after all.

--player-log NAME=PATH reads a run from a test player's own Player.log, for a
run that reports nothing to the editor (WindowsClipboardSampleQuitPlayerTests
quits the player). It keeps what the test logged from its start on, drops the
stack trace a development player writes under every message, saves the rest
beside PATH as windows-clipboard-sample-run-NAME.log and checks it with the others.

--test-results reads the runs WindowsClipboardSampleRunPlayerTests wrote into
its test output, bracketed by "[SampleRun] begin NAME" / "[SampleRun] end NAME",
from a Test Framework result file. Each run is saved beside that file as
windows-clipboard-sample-run-NAME.log and checked with any logs given.

With no log and no --test-results it reads
artifact/features/clipboard/results/logs/*.log.

On Windows `python3` may resolve to a Microsoft Store app execution alias,
which runs nothing and exits 49 - the checks then look like they passed when
none of them ran. Where `python3 -c "print(1)"` prints nothing, use `python`.

Exit status is 1 when any check fails.
"""

import glob
import io
import re
import sys
import xml.etree.ElementTree as ET
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
LOGS = REPO / "artifact" / "features" / "clipboard" / "results" / "logs"

CLICK = re.compile(r"\]\[(On[A-Za-z]+Clicked)\]\s*$")
ENTRY = re.compile(r"#(\d+) \[(call|accept|done|local)\] (\S+)")
ACCEPT = re.compile(r"#(\d+) \[accept\] (\S+)")
DONE = re.compile(r"#(\d+) \[done\] (\S+) call=#(\d+)")
BINDING = re.compile(r'\("([A-Za-z0-9]+)",\s*(On[A-Za-z]+)\)')
BUTTON_TEXT = re.compile(r'name="([A-Za-z0-9]+)"[^>]*text="([^"]*)"')
CONST = re.compile(r'internal const string ([A-Za-z0-9_]+) = "([^"]*)";')
BLEW_UP = re.compile(r"Exception|Stack trace|NullReference|LogError", re.I)

# WindowsClipboardSampleRunPlayerTests.RunLogBegin / RunLogEnd.
RUN_BEGIN = "[SampleRun] begin "
RUN_END = "[SampleRun] end "
RUN_LOG_PREFIX = "windows-clipboard-sample-run-"

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
        self.partial = []

    def ok(self, name, detail=""):
        print("  OK   " + name + (": " + detail if detail else ""))

    def part(self, name, detail):
        """Held back from OK on purpose: the check holds for what ran, and what did not run is named."""
        self.partial.append(name)
        print("  PART " + name + ": " + detail)

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


def extract_runs(results):
    """Saves each bracketed run in a result file's test cases beside it; returns the paths.

    Only test-case elements are read: a suite's output repeats its cases' output.
    """
    saved = []
    for case in ET.parse(str(results)).getroot().iter("test-case"):
        output = case.find("output")
        if output is None or not output.text:
            continue
        name, lines = None, []
        for line in output.text.splitlines():
            if name is None:
                if line.startswith(RUN_BEGIN):
                    name, lines = line[len(RUN_BEGIN):].strip(), []
            elif line == RUN_END + name:
                path = Path(results).parent / (RUN_LOG_PREFIX + name + ".log")
                path.write_bytes(("\n".join(lines) + "\n").encode("utf-8"))
                saved.append(str(path))
                name = None
            else:
                lines.append(line)
    return saved


# The first line a player test writes (WindowsClipboardSampleScreenDriver.LogFocus). What comes
# before it is the player starting up, which names the player's own path under Temp.
TEST_START = "[PlayerTests][focus] "
# A managed frame of the stack trace a development player writes under each message, e.g.
# "UnityEngine.Debug:Log (object)" or "Foo:Bar () (at ./Packages/x.cs:12)". A message such as
# "NullReferenceException: ..." has a space after its colon and is kept.
STACK_FRAME = re.compile(r"^\S+:\S+ \(.*\)( \(at .*\))?\s*$")


def extract_player_log(name, path):
    """Saves the test's part of a Player.log, messages only, beside it; returns the saved path."""
    lines = read(path).splitlines()
    start = next((i for i, line in enumerate(lines) if line.startswith(TEST_START)), 0)
    kept = [line for line in lines[start:]
            if not STACK_FRAME.match(line) and not line.startswith("(Filename: ")]
    saved = Path(path).parent / (RUN_LOG_PREFIX + name + ".log")
    saved.write_bytes(("\n".join(kept) + "\n").encode("utf-8"))
    return str(saved)


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


def check_s2(paths, rep, not_automated=()):
    """Every button was pressed, nothing threw, and the operations are readable.

    not_automated names buttons (as in OnXClicked, without On / Clicked) that a partial run is
    known not to press - the automated run leaves out the blocks that need the OS in another
    state. They are reported as PART rather than passed, and a name on that list that was in
    fact pressed is a failure, so the list cannot quietly go stale.
    """
    if not (UXML.is_file() and CONTROLLER.is_file()):
        rep.skip("S-2 button coverage", "the sample sources are not where they were")
        return

    label = dict(BUTTON_TEXT.findall(read(UXML)))
    button_of = dict((handler, button)
                     for button, handler in BINDING.findall(read(CONTROLLER)))
    pressed = per_click(paths)

    declared = set("On%sClicked" % name for name in not_automated)
    if declared:
        unknown_declared = sorted(declared - set(button_of))
        rep.check(not unknown_declared, "S-2 not-automated list names bound buttons",
                  "%d named" % len(declared), "not in the bindings: %s" % unknown_declared[:8])

    missing = sorted(h for h in button_of if h not in pressed)
    unexpected = [h for h in missing if h not in declared]
    stale = sorted(h for h in declared if h in pressed)
    rep.check(not unexpected, "S-2 every button appears in the log",
              "%d buttons, all pressed" % len(button_of) if not missing
              else "all but the %d listed as not automated" % len(missing),
              "never pressed: %s" % unexpected[:8])
    if declared:
        rep.check(not stale, "S-2 not-automated list is current",
                  "none of them was pressed", "listed as not automated but pressed: %s" % stale[:8])
    if missing and not unexpected:
        rep.part("S-2 coverage",
                 "%d of %d buttons pressed; not automated yet: %s"
                 % (len(button_of) - len(missing), len(button_of), missing))

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


OUTCOME = re.compile(r"#\d+ \[(call|done|local)\] (\S+)(.*)$")
KEY_VALUE = re.compile(r"(\w+)=(\S+)")


def outcome_of(line):
    """(operation, status, code, {key: value}) for a [call] / [done] / [local] line, else None."""
    found = OUTCOME.search(line)
    if not found:
        return None
    kind, operation, rest = found.groups()
    words = rest.split()
    if kind == "local":
        status = "local"
    else:
        status = next((w for w in words if w in ("OK", "NG")), None)
    fields = dict(KEY_VALUE.findall(rest))
    return operation, status, fields.get("code"), fields


class Pattern:
    """One expected.BLOCKS pattern: alternatives separated by " | ", optional when it starts with "?"."""

    def __init__(self, text):
        self.text = text.lstrip("?")
        self.optional = text.startswith("?")
        self.alternatives = [self._one(part) for part in self.text.split(" | ")]

    @staticmethod
    def _one(text):
        words = text.split()
        operation, status, rest = words[0], words[1], words[2:]
        code = None
        if status == "NG":
            code, rest = rest[0], rest[1:]
        return operation, status, code, dict(w.split("=", 1) for w in rest)

    def matches(self, outcome):
        got_operation, got_status, got_code, got_fields = outcome
        return any(operation == got_operation and status == got_status
                   and (code is None or code == got_code)
                   and all(got_fields.get(k) == v for k, v in fields.items())
                   for operation, status, code, fields in self.alternatives)

    def keys(self):
        return {k for _, _, _, fields in self.alternatives for k in fields}


def presses_of(path):
    """[(button, [outcome lines])] in press order; lines before the first press are dropped."""
    presses = []
    for line in read(path).splitlines():
        click = CLICK.search(line)
        if click:
            presses.append((click.group(1)[len("On"):-len("Clicked")], []))
        elif presses and outcome_of(line):
            presses[-1][1].append(line)
    return presses


def describe(line, keys):
    """The line's operation, status, code, and the fields named in <keys>."""
    operation, status, code, fields = outcome_of(line)
    parts = [operation, status] + ([code] if status == "NG" else [])
    parts += ["%s=%s" % (k, fields[k]) for k in sorted(keys) if k in fields]
    return " ".join(parts)


def check_outcomes(paths, rep):
    """Each press of an automated run reports what the manual verification expects.

    Only the automated run logs are judged (windows-clipboard-sample-run-<block>.log); the
    manual logs predate two sample fixes and are the record the expectations came from.
    """
    sys.dont_write_bytecode = True  # no __pycache__ left in scripts/
    import windows_clipboard_sample_expected as expected

    runs = {Path(p).stem[len(RUN_LOG_PREFIX):]: p for p in paths
            if Path(p).stem.startswith(RUN_LOG_PREFIX)}
    if not runs:
        rep.skip("outcomes", "no automated run log (%s<block>.log) among the logs" % RUN_LOG_PREFIX)
        return

    for block, table in expected.BLOCKS.items():
        name = "outcomes %s" % block
        if block not in runs:
            rep.check(False, name, fail_detail="no run log for this block")
            continue

        presses = presses_of(runs[block])
        problems = []
        if [b for b, _ in presses] != [b for b, _, _ in table]:
            first = next((i for i, (a, b) in enumerate(zip(presses, table)) if a[0] != b[0]),
                         min(len(presses), len(table)))
            problems.append("the presses are not the expected sequence from press %d (%d pressed, %d expected)"
                            % (first + 1, len(presses), len(table)))
        for number, ((button, lines), (_, label, texts)) in enumerate(zip(presses, table), 1):
            patterns = [Pattern(t) for t in texts]
            unused = [p for p in patterns if not p.optional]
            unexpected = []
            for line in lines:
                outcome = outcome_of(line)
                required = next((p for p in unused if p.matches(outcome)), None)
                if required is not None:
                    unused.remove(required)
                elif not any(p.optional and p.matches(outcome) for p in patterns):
                    unexpected.append(line)
            if unused or unexpected:
                where = "press %d %s%s" % (number, button, " (%s)" % label if label else "")
                keys = set().union(*(p.keys() for p in patterns))
                problems.append("%s: expected %s; got %s"
                                % (where, [p.text for p in unused] or "nothing more",
                                   [describe(l, keys) for l in unexpected] or "nothing else"))

        for before, after, drop in expected.HISTORY_COUNT_DROPS.get(block, []):
            counts = []
            for number in (before, after):
                lines = presses[number - 1][1] if number <= len(presses) else []
                count = next((outcome_of(l)[3].get("count") for l in lines
                              if outcome_of(l)[0] == "getClipboardHistory"), None)
                counts.append(int(count) if count and count.isdigit() else None)
            if None in counts or counts[0] - counts[1] != drop:
                problems.append("M-14: history count %s at press %d, %s at press %d; expected %d fewer"
                                % (counts[0], before, counts[1], after, drop))

        rep.check(not problems, name,
                  "%d presses report what the manual verification expects" % len(table),
                  "%d problem(s): %s" % (len(problems), "; ".join(problems[:8])))


def main(argv):
    args = list(argv[1:])
    not_automated = ()
    if "--not-automated" in args:
        at = args.index("--not-automated")
        if at + 1 >= len(args):
            print("error: --not-automated needs a comma-separated list", file=sys.stderr)
            return 2
        not_automated = tuple(name for name in args[at + 1].split(",") if name)
        del args[at:at + 2]

    extracted = []
    if "--test-results" in args:
        at = args.index("--test-results")
        if at + 1 >= len(args) or not Path(args[at + 1]).is_file():
            print("error: --test-results needs a Test Framework result file", file=sys.stderr)
            return 2
        extracted = extract_runs(args[at + 1])
        if not extracted:
            print("error: no sample run in %s" % args[at + 1], file=sys.stderr)
            return 2
        del args[at:at + 2]

    while "--player-log" in args:
        at = args.index("--player-log")
        name, _, path = (args[at + 1] if at + 1 < len(args) else "").partition("=")
        if not name or not Path(path).is_file():
            print("error: --player-log needs NAME=PATH to a Player.log", file=sys.stderr)
            return 2
        extracted.append(extract_player_log(name, path))
        del args[at:at + 2]

    paths = args + extracted if (args or extracted) else sorted(glob.glob(str(LOGS / "*.log")))
    paths = [p for p in paths if Path(p).is_file()]
    if not paths:
        print("error: no log files to read", file=sys.stderr)
        return 2

    print("== %d log(s)" % len(paths))
    for path in paths:
        print("   " + Path(path).name)
    print("")

    rep = Report()
    check_s2(paths, rep, not_automated)
    check_s4(paths, rep)
    check_s8(paths, rep)
    check_outcomes(paths, rep)
    print("")
    print("failures: %d" % len(rep.failures)
          + (" (%s)" % ", ".join(rep.failures) if rep.failures else ""))
    if rep.partial:
        print("partial: %s - holds for what ran; see PART above for what did not" % ", ".join(rep.partial))
    return 1 if rep.failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
