#!/usr/bin/env python3
r"""Helpers of scripts/test_ios_ui.sh: the test list, the device approval, and the result check.

artifact/topics/ios-c-abi-2, UI automation design v4, 4.6 (shaped after native-toolkit's
scripts/ios_ui_runner.py).

Subcommands (paths are relative to the repository root, which is the first argument):

  tests <repo>
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
  results <xcresult tests JSON file> <test>...
      Fails unless the run's test cases are exactly these tests and all passed. Prints names and
      results only (the JSON also holds the device's identifier).

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
"""

import json
import os
import secrets
import subprocess
import sys

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
    "playertests": "runs the iOS player tests (a named pasteboard round trip; reads the notification permission without asking)",
}


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True, text=True).stdout


def requirements(repo):
    with open(os.path.join(repo, REQUIREMENTS), encoding="utf-8") as f:
        return json.load(f)["tests"]


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


def approval_problems(record, tests, head, trees, dirty, marker=None):
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
    if sorted(record.get("tests", [])) != sorted(tests):
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
        ok &= result == "Passed"
        lines.append(f"{test}: {result}")
    for name in sorted(set(found) - set(tests)):
        ok = False
        lines.append(f"{name}: {found[name]} (not asked for)")
    return ok, lines


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
        print("\n".join(t for t in requirements(rest[0]) if t != PLAYER_TESTS))
        return 0
    if command == "results":
        with open(rest[0], encoding="utf-8") as f:
            ok, lines = judge_results(json.load(f), rest[1:])
        print("\n".join("  " + line for line in lines))
        return 0 if ok else 1
    repo = rest[0]
    tests = {"approve": rest[1:], "guard": rest[3:]}.get(command, rest[2:])
    known = requirements(repo)
    unknown = [t for t in tests if t not in known]
    if not tests or unknown:
        print(f"unknown or missing tests: {unknown or 'none given'}", file=sys.stderr)
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
            json.dump({"tests": tests, "head": head, "trees": trees, "used": False}, f, indent=1)
        print(f"Device run at {head[:12]}. On the device:")
        for test in tests:
            print(f"  {test}: " + "; ".join(CATEGORIES[c] for c in known[test]))
        print("Apps installed, and removed at the end:")
        apps = (PLAYER_APPS if PLAYER_TESTS in tests else []) + (UI_APPS if set(tests) - {PLAYER_TESTS} else [])
        for bundle_id, what in apps:
            print(f"  {bundle_id} ({what})")
        print(f"Approval ID: {approval_id}")
        return 0
    path = record_path(rest[1])
    if not os.path.exists(path):
        print("no such approval", file=sys.stderr)
        return 1
    with open(path, encoding="utf-8") as f:
        record = json.load(f)
    marker = rest[2] if command == "guard" else None
    problems = approval_problems(record, tests, head, trees, dirty, marker=marker)
    if problems:
        print("the approval does not cover this run: " + "; ".join(problems), file=sys.stderr)
        return 1
    if command == "use-approval":
        marker = secrets.token_hex(16)
        record["used"] = True
        record["run"] = marker
        with open(path, "w", encoding="utf-8") as f:
            json.dump(record, f, indent=1)
        print(marker)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
