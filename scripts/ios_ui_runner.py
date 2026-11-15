#!/usr/bin/env python3
r"""Helpers of scripts/test_ios_ui.sh: the test list, the device approval, and the result check.

artifact/topics/ios-c-abi-2, UI automation design v4, 4.6 (shaped after native-toolkit's
scripts/ios_ui_runner.py).

Subcommands (paths are relative to the repository root, which is the first argument):

  tests <repo> [--device | --keep-installed]
      Leaves out the tests of the classes in notMovedYet (features not moved to the C ABI 2.0.0 yet;
      3-S design v3, 1.7). With --device, also leaves out the tests listed as simulatorOnly and those
      of keepInstalled (they run only in the keep-installed run). With --keep-installed, prints
      keepInstalled in its order, and stops when one of them is not moved yet.
      Prints the UI tests, one "Class/method" per line, from Tools~/IosUiTests/DeviceRequirements.json.
      The entry PlayerTests/IosPlayer stands for the player tests of scripts/verify_unity_ios.sh, which
      need the same approval on a device; it is not a UI test and is not printed.
  approve <repo> <test>...
      For a device run: checks the guarded trees (below) are committed, writes an approval record
      outside the repository, prints what will happen on the device and a random approval ID.
  check-approval <repo> <id> <test>...
      Fails unless the approval exists, is unused, names exactly these tests, and the guarded trees
      still match it (HEAD's tree for each, and nothing uncommitted).
  use-approval <repo> <id> <test>...
      check-approval, then marks the approval used by this run (an approval works once) and prints
      the run's marker, a random value only this run knows.
  guard <repo> <id> <marker> <test>...
      Run before every step that touches the device: fails unless the approval was used by the run
      with this marker and the tests, HEAD and the guarded trees still match it.
  not-moved <repo> <test>...
      Prints the given tests whose class is in notMovedYet; exits 0 when there is one, else 1.
  run-lock <lock file>
      Holds the run lock (fcntl) for the parent process: prints "locked" or "busy" on one line, then,
      when locked, keeps the lock until the parent ends or sends SIGUSR1. TERM, HUP and INT are ignored,
      so the lock outlives a signal to the group until the parent's cleanup is done.
  time-limit <repo> <test>
      Prints the test's time limit in seconds: timeLimits in DeviceRequirements.json, or 600.
  results <xcresult tests JSON file> <test>...
      Fails unless the run's test cases are exactly these tests and all passed. Prints names and
      results only (the JSON also holds the device's identifier).

approve, check-approval, use-approval and guard take --keep-installed anywhere among their arguments:
the keep-installed run (keep-installed design v4: the harness installed once, the user allows it in a
Focus mode, the keepInstalled tests run in their order without reinstalling). Its approval is recorded
with keepInstalled true and covers that kind of run only, with the tests in that exact order; an
approval without it never covers a keep-installed run.

The guarded trees are what decides what runs on the device: the package, the harness, the XCUITest
project, the build settings, and the runners.

    >>> sample = {"testNodes": [{"nodeType": "Test Plan", "children": [{"nodeType": "UI test bundle", "children": [
    ...   {"nodeType": "Test Suite", "children": [
    ...     {"nodeType": "Test Case", "nodeIdentifier": "DialogUITests/test_confirmOk()", "result": "Passed"}]}]}]}]}
    >>> judge_results(sample, ["DialogUITests/test_confirmOk"])
    (True, ['DialogUITests/test_confirmOk: Passed'])
    >>> judge_results(sample, ["DialogUITests/test_confirmOk", "ShareUITests/test_dismiss"])
    (False, ['DialogUITests/test_confirmOk: Passed', 'ShareUITests/test_dismiss: did not run'])
    >>> sample["testNodes"][0]["children"][0]["children"][0]["children"][0]["result"] = "Failed"
    >>> judge_results(sample, ["DialogUITests/test_confirmOk"])
    (False, ['DialogUITests/test_confirmOk: Failed'])
    >>> judge_results({"testNodes": []}, ["DialogUITests/test_confirmOk"])
    (False, ['DialogUITests/test_confirmOk: did not run'])

    A test that wraps a known issue in XCTExpectFailure reports "Expected Failure", and passes:

    >>> sample["testNodes"][0]["children"][0]["children"][0]["children"][0]["result"] = "Expected Failure"
    >>> judge_results(sample, ["DialogUITests/test_confirmOk"])
    (True, ['DialogUITests/test_confirmOk: Expected Failure'])

    A test case nobody asked for fails the run too:

    >>> extra = {"testNodes": [{"nodeType": "Test Case", "nodeIdentifier": "X/test_a()", "result": "Passed"},
    ...                        {"nodeType": "Test Case", "nodeIdentifier": "X/test_b()", "result": "Passed"}]}
    >>> judge_results(extra, ["X/test_a"])
    (False, ['X/test_a: Passed', 'X/test_b: Passed (not asked for)'])

    Approval records are compared field by field. Before use, the record must be unused:

    >>> record = {"tests": ["A/t"], "head": "h", "trees": {"p": "t"}, "used": False}
    >>> approval_problems(record, ["A/t"], "h", {"p": "t"}, [])
    []
    >>> approval_problems(dict(record, used=True, run="m"), ["A/t"], "h", {"p": "t"}, [])
    ['the approval was already used']
    >>> approval_problems(record, ["A/t", "B/t"], "h2", {"p": "u"}, ["p"])
    ['the tests differ from the approved list', 'HEAD moved since the approval', 'p changed since the approval', 'uncommitted changes in: p']

    The player tests (scripts/verify_unity_ios.sh) and the UI tests (scripts/test_ios_ui.sh) install
    different apps, so one approval never covers both:

    >>> mixed_kinds(["PlayerTests/IosPlayer", "DialogUITests/test_confirmOk"])
    True
    >>> mixed_kinds(["PlayerTests/IosPlayer"]), mixed_kinds(["DialogUITests/test_confirmOk"])
    (False, False)

    After use, the run that used it (and only that run) passes the guard:

    >>> used = dict(record, used=True, run="m")
    >>> approval_problems(used, ["A/t"], "h", {"p": "t"}, [], marker="m")
    []
    >>> approval_problems(used, ["A/t"], "h", {"p": "t"}, [], marker="other")
    ['the approval was used by another run']
    >>> approval_problems(record, ["A/t"], "h", {"p": "t"}, [], marker="m")
    ['the approval was not used by this run']

    The kind of run must match the approval, both ways, and a keep-installed run keeps the order:

    >>> approval_problems(record, ["A/t"], "h", {"p": "t"}, [], keep=True)
    ['the approval is not for a keep-installed run']
    >>> keep = dict(record, tests=["A/t", "B/t"], keepInstalled=True)
    >>> approval_problems(keep, ["A/t", "B/t"], "h", {"p": "t"}, [])
    ['the approval is for a keep-installed run']
    >>> approval_problems(keep, ["A/t", "B/t"], "h", {"p": "t"}, [], keep=True)
    []
    >>> approval_problems(keep, ["B/t", "A/t"], "h", {"p": "t"}, [], keep=True)
    ['the tests differ from the approved list']

    keepInstalled must name tests, none of them simulator only, none twice, and every category must be
    known:

    >>> check_requirements({"tests": {"A/t": ["focus"], "B/t": ["systemui"]}, "simulatorOnly": [], "keepInstalled": ["A/t"]})
    []
    >>> check_requirements({"tests": {"A/t": ["focus"]}, "simulatorOnly": ["A/t"], "keepInstalled": ["A/t", "A/t", "C/t"]})
    ['keepInstalled names tests that do not exist: C/t', 'keepInstalled names a test twice', 'keepInstalled and simulatorOnly overlap: A/t']
    >>> check_requirements({"tests": {"A/t": ["nosuch"]}})
    ['unknown categories: nosuch']

    notMovedYet names test classes (the part before the slash) that exist:

    >>> check_requirements({"tests": {"A/t": ["focus"], "B/t": ["systemui"]}, "notMovedYet": ["B"]})
    []
    >>> check_requirements({"tests": {"A/t": ["focus"]}, "notMovedYet": ["A", "A/t", "C"]})
    ['notMovedYet names test classes that do not exist: A/t, C']
    >>> not_moved({"notMovedYet": ["B"]}, ["A/t", "B/t", "B/u"])
    ['B/t', 'B/u']

    One approval is used once, also by two runs at the same moment (the lock around the read and the write):

    >>> import tempfile, threading
    >>> folder = tempfile.mkdtemp()
    >>> path = os.path.join(folder, "a.json")
    >>> with open(path, "w") as f: json.dump(dict(record), f)
    >>> results = []
    >>> threads = [threading.Thread(target=lambda: results.append(consume(path, lambda r: approval_problems(r, ["A/t"], "h", {"p": "t"}, [])))) for _ in range(8)]
    >>> for th in threads: th.start()
    >>> for th in threads: th.join()
    >>> sum(1 for marker, problems in results if marker), sum(1 for marker, problems in results if problems)
    (1, 7)
"""

import fcntl
import json
import os
import secrets
import signal
import subprocess
import sys
import time

REQUIREMENTS = "Tools~/IosUiTests/DeviceRequirements.json"
GUARDED = [
    "Packages/com.jonghyunkim.nativetoolkit",
    "Assets/NativeToolkitIosUiHarness",
    "Assets/NativeToolkitIosPlayerTests",
    "Tools~/IosUiTests",
    "Assets/Settings/Build Profiles",
    "ProjectSettings",
    "scripts/test_ios_ui.sh",
    "scripts/ios_ui_runner.py",
    "scripts/verify_unity_ios.sh",
    "scripts/ios_player_results.py",
    "scripts/ios_script_common.sh",
    "Packages/manifest.json",
    "Packages/packages-lock.json",
    "Assets/Editor",
    "Assets/Plugins",
]
APPROVALS = os.path.expanduser("~/Library/Application Support/unity-native-plugin/test_ios-approvals")
PLAYER_TESTS = "PlayerTests/IosPlayer"
DEFAULT_TIME_LIMIT = 600
# xcresulttool's results that pass: "Expected Failure" is a test whose known issue is wrapped in
# XCTExpectFailure (ShareUITests.test_SH13; it fails once the issue is fixed, as strict).
PASSING_RESULTS = ("Passed", "Expected Failure")
UI_APPS = [
    ("com.jonghyunkim.nativetoolkit.uiharness", "the UI harness (Unity)"),
    ("com.jonghyunkim.nativetoolkit.uitesthost", "the empty host of the UI tests"),
    ("com.jonghyunkim.nativetoolkit.uitests.xctrunner", "the UI test runner Xcode installs"),
]
PLAYER_APPS = [("com.jonghyunkim.nativetoolkit.playertests", "the iOS player tests (Unity)")]
CATEGORIES = {
    "systemui": "presses system alerts and the springboard (the notification permission, a banner)",
    "sharesheet": "opens the share sheet and closes it without choosing an activity",
    "clipboard": "writes and reads a named pasteboard only (never the general one)",
    "reinstall": "removes the app and installs it again before the run",
    "appdialog": "presses the harness's own alerts and action sheets (not the springboard); may type a test value into a text field with the keyboard",
"focus": "the harness is installed once and stays: the run waits until the user lets only NTK UI Harness through a Focus mode, then runs the tests in order without reinstalling",
    "longpress": "long-presses the harness's notification banner and presses an action only after the expanded notification shows that notification",
    "playertests": "runs the iOS player tests: Share calls that fail without a sheet, Clipboard on named pasteboards of the player only (never the general one), Dialog calls rejected before an alert, and Notification calls without ever asking for the permission (requests, a badge and categories of the player; nothing is shown without the permission, and the player's requests are removed with the app)",
}


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True, text=True).stdout


def load_requirements(repo):
    with open(os.path.join(repo, REQUIREMENTS), encoding="utf-8") as f:
        data = json.load(f)
    problems = check_requirements(data)
    if problems:
        raise SystemExit(REQUIREMENTS + ": " + "; ".join(problems))
    return data


def check_requirements(data):
    """What is wrong with DeviceRequirements.json (empty: nothing)."""
    problems = []
    tests = data["tests"]
    unknown_categories = sorted({c for cats in tests.values() for c in cats} - set(CATEGORIES))
    if unknown_categories:
        problems.append("unknown categories: " + ", ".join(unknown_categories))
    keep = data.get("keepInstalled", [])
    missing = [t for t in keep if t not in tests]
    if missing:
        problems.append("keepInstalled names tests that do not exist: " + ", ".join(missing))
    if len(set(keep)) != len(keep):
        problems.append("keepInstalled names a test twice")
    limits = data.get("timeLimits", {})
    bad_limits = sorted(t for t, s in limits.items() if t not in tests or not isinstance(s, int) or s < 60)
    if bad_limits:
        problems.append("timeLimits names unknown tests or limits under 60 s: " + ", ".join(bad_limits))
    overlap = sorted(set(keep) & set(data.get("simulatorOnly", [])))
    if overlap:
        problems.append("keepInstalled and simulatorOnly overlap: " + ", ".join(overlap))
    classes = {t.split("/", 1)[0] for t in tests}
    bad_classes = [c for c in data.get("notMovedYet", []) if c not in classes or c == PLAYER_TESTS.split("/")[0]]
    if bad_classes:
        problems.append("notMovedYet names test classes that do not exist: " + ", ".join(bad_classes))
    return problems


def not_moved(data, tests):
    """The tests whose class is in notMovedYet (a feature not moved to the C ABI 2.0.0 yet)."""
    classes = set(data.get("notMovedYet", []))
    return [t for t in tests if t.split("/", 1)[0] in classes]


def requirements(repo):
    return load_requirements(repo)["tests"]


def keep_installed(repo):
    return load_requirements(repo).get("keepInstalled", [])


def simulator_only(repo):
    """The simulatorOnly tests; every name must be a test, or a typo would let one reach a device."""
    with open(os.path.join(repo, REQUIREMENTS), encoding="utf-8") as f:
        data = json.load(f)
    names = set(data.get("simulatorOnly", []))
    unknown = sorted(names - set(data["tests"]))
    if unknown:
        raise SystemExit("simulatorOnly names tests that do not exist: " + ", ".join(unknown))
    return names


def tree_state(repo):
    """HEAD, HEAD's tree id for each guarded path, and the guarded paths with uncommitted changes."""
    head = git(repo, "rev-parse", "HEAD").strip()
    trees = {}
    for path in GUARDED:
        try:
            trees[path] = git(repo, "rev-parse", f"HEAD:{path}").strip()
        except subprocess.CalledProcessError:
            trees[path] = "missing"
    dirty = [p for p in GUARDED if git(repo, "status", "--porcelain=v1", "--untracked-files=all", "--", p).strip()]
    return head, trees, dirty


def approval_problems(record, tests, head, trees, dirty, marker=None, keep=False):
    """What keeps an approval record from covering this run (empty: it covers it).

    Without a marker the record must be unused; with one, it must have been used by that run.
    """
    problems = []
    if marker is None:
        if record.get("used"):
            problems.append("the approval was already used")
    elif not record.get("used"):
        problems.append("the approval was not used by this run")
    elif record.get("run") != marker:
        problems.append("the approval was used by another run")
    if keep and not record.get("keepInstalled"):
        problems.append("the approval is not for a keep-installed run")
    elif not keep and record.get("keepInstalled"):
        problems.append("the approval is for a keep-installed run")
    if (record.get("tests", []) != tests) if keep else (sorted(record.get("tests", [])) != sorted(tests)):
        problems.append("the tests differ from the approved list")
    if record.get("head") != head:
        problems.append("HEAD moved since the approval")
    for path, tree in trees.items():
        if record.get("trees", {}).get(path) != tree:
            problems.append(f"{path} changed since the approval")
    if dirty:
        problems.append("uncommitted changes in: " + ", ".join(dirty))
    return problems


def mixed_kinds(tests):
    """True when the player tests and UI tests are asked for together."""
    return PLAYER_TESTS in tests and len(set(tests)) > 1


def judge_results(data, tests):
    """(passed, lines) for xcresulttool's 'get test-results tests' JSON and the tests that should have run."""
    found = {}

    def walk(node):
        if node.get("nodeType") == "Test Case":
            name = node.get("nodeIdentifier", "").removesuffix("()")
            found[name] = node.get("result", "unknown")
        for child in node.get("children", []):
            walk(child)

    for node in data.get("testNodes", []):
        walk(node)
    lines, ok = [], True
    for test in tests:
        result = found.get(test, "did not run")
        ok &= result in PASSING_RESULTS
        lines.append(f"{test}: {result}")
    for name in sorted(set(found) - set(tests)):
        ok = False
        lines.append(f"{name}: {found[name]} (not asked for)")
    return ok, lines


def consume(path, problems_of):
    """Uses the approval at path once: under an exclusive lock, reads it, checks it with problems_of and,
    when it covers the run, marks it used by a new marker written through a temporary file and a rename.
    Returns (marker or None, problems)."""
    with open(os.path.join(os.path.dirname(path), ".consume.lock"), "a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        with open(path, encoding="utf-8") as f:
            record = json.load(f)
        problems = problems_of(record)
        if problems:
            return None, problems
        marker = secrets.token_hex(16)
        record["used"] = True
        record["run"] = marker
        temporary = path + ".tmp"
        with open(temporary, "w", encoding="utf-8") as f:
            json.dump(record, f, indent=1)
        os.replace(temporary, path)
        return marker, []


def run_lock(path):
    """The run lock's holder (see the run-lock subcommand). Returns the exit code."""
    parent = os.getppid()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    fd = os.open(path, os.O_RDWR | os.O_CREAT | os.O_CLOEXEC, 0o600)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        print("busy", flush=True)
        return 1
    for sig in (signal.SIGTERM, signal.SIGHUP, signal.SIGINT):
        signal.signal(sig, signal.SIG_IGN)
    released = []
    signal.signal(signal.SIGUSR1, lambda *_: released.append(True))
    print("locked", flush=True)
    while not released and os.getppid() == parent:
        time.sleep(1)
    return 0


def record_path(approval_id):
    if len(approval_id) != 32 or any(c not in "0123456789abcdef" for c in approval_id):
        raise SystemExit("an approval ID is 32 lowercase hex digits")
    return os.path.join(APPROVALS, approval_id + ".json")


def main(argv):
    if len(argv) < 3:
        print(__doc__, file=sys.stderr)
        return 2
    command, rest = argv[1], argv[2:]
    if command == "tests":
        data = load_requirements(rest[0])
        if "--keep-installed" in rest[1:]:
            stopped = not_moved(data, keep_installed(rest[0]))
            if stopped:
                print("the keep-installed run has tests of features not moved to the C ABI 2.0.0 yet (notMovedYet): "
                      + ", ".join(stopped), file=sys.stderr)
                return 2
            print("\n".join(keep_installed(rest[0])))
            return 0
        skip = simulator_only(rest[0]) | set(keep_installed(rest[0])) if "--device" in rest[1:] else set()
        skip |= set(not_moved(data, data["tests"]))
        print("\n".join(t for t in requirements(rest[0]) if t != PLAYER_TESTS and t not in skip))
        return 0
    if command == "not-moved":
        found = not_moved(load_requirements(rest[0]), rest[1:])
        if found:
            print("\n".join(found))
        return 0 if found else 1
    if command == "run-lock":
        return run_lock(rest[0])
    if command == "time-limit":
        print(load_requirements(rest[0]).get("timeLimits", {}).get(rest[1], DEFAULT_TIME_LIMIT))
        return 0
    if command == "results":
        try:
            with open(rest[0], encoding="utf-8") as f:
                data = json.load(f)
        except (OSError, ValueError):
            # A stopped test leaves no result bundle to read (the device run of 2026-11-15, NU-11).
            print("\n".join(f"  {test}: no readable result" for test in rest[1:]))
            return 1
        ok, lines = judge_results(data, rest[1:])
        print("\n".join("  " + line for line in lines))
        return 0 if ok else 1
    keep = "--keep-installed" in rest
    rest = [a for a in rest if a != "--keep-installed"]
    repo = rest[0]
    tests = {"approve": rest[1:], "guard": rest[3:]}.get(command, rest[2:])
    known = requirements(repo)
    unknown = [t for t in tests if t not in known]
    if not tests or unknown:
        print(f"unknown or missing tests: {unknown or 'none given'}", file=sys.stderr)
        return 2
    stopped = not_moved(load_requirements(repo), tests)
    if stopped:
        print("not moved yet to the C ABI 2.0.0 (notMovedYet): " + ", ".join(stopped), file=sys.stderr)
        return 2
    sim_only = sorted(set(tests) & simulator_only(repo))
    if sim_only:
        print("simulator-only tests cannot run on a device: " + ", ".join(sim_only), file=sys.stderr)
        return 2
    keep_list = keep_installed(repo)
    if keep and tests != keep_list:
        print("a keep-installed run runs exactly keepInstalled, in its order: " + ", ".join(keep_list), file=sys.stderr)
        return 2
    if not keep and set(tests) & set(keep_list):
        print("these run only in the keep-installed run (--keep-installed): " + ", ".join(sorted(set(tests) & set(keep_list))), file=sys.stderr)
        return 2
    if mixed_kinds(tests):
        print(f"{PLAYER_TESTS} is approved and run on its own (scripts/verify_unity_ios.sh --device)", file=sys.stderr)
        return 2
    head, trees, dirty = tree_state(repo)
    if command == "approve":
        if dirty:
            print("commit these first (a device run uses committed code only): " + ", ".join(dirty), file=sys.stderr)
            return 1
        approval_id = secrets.token_hex(16)
        os.makedirs(APPROVALS, exist_ok=True)
        with open(record_path(approval_id), "w", encoding="utf-8") as f:
            json.dump({"tests": tests, "head": head, "trees": trees, "used": False, "keepInstalled": keep}, f, indent=1)
        print(f"Device run at {head[:12]}{' (keep-installed: the harness is installed once)' if keep else ''}. On the device:")
        for test in tests:
            print(f"  {test}: " + "; ".join(CATEGORIES[c] for c in known[test]))
        print("Apps installed, and removed at the end:")
        apps = (PLAYER_APPS if PLAYER_TESTS in tests else []) + (UI_APPS if set(tests) - {PLAYER_TESTS} else [])
        for bundle_id, what in apps:
            print(f"  {bundle_id} ({what})")
        if keep:
            print("Before the run (the user): unlock the iPhone, set Auto-Lock to Never, keep it charging, and make sure no")
            print("  scheduled Focus mode starts during the run. After NU-1 the run waits (up to 30 minutes) until the user")
            print("  lets only NTK UI Harness through a Focus mode (no people, no time-sensitive notifications from others).")
            print("  Afterwards, remove NTK UI Harness from that Focus mode. Time: 30 to 40 minutes plus the wait.")
        print(f"Approval ID: {approval_id}")
        return 0
    path = record_path(rest[1])
    if not os.path.exists(path):
        print("no such approval", file=sys.stderr)
        return 1
    marker = rest[2] if command == "guard" else None
    if command == "use-approval":
        new_marker, problems = consume(path, lambda r: approval_problems(r, tests, head, trees, dirty, keep=keep))
    else:
        with open(path, encoding="utf-8") as f:
            record = json.load(f)
        new_marker, problems = None, approval_problems(record, tests, head, trees, dirty, marker=marker, keep=keep)
    if problems:
        print("the approval does not cover this run: " + "; ".join(problems), file=sys.stderr)
        return 1
    if new_marker:
        print(new_marker)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
