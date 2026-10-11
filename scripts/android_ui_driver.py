#!/usr/bin/env python3
"""Presses and reads Android UI for device PlayMode tests, on their request.

The Unity tests run inside an Android player and cannot reach the OS UI (a dialog, the share sheet,
the notification shade). This helper runs on the host while the tests run. It reads requests the
tests write to logcat, carries them out with adb (`uiautomator dump`, `input`, `dumpsys`,
`settings`, `am`), and writes each result into the app's files directory through `run-as`, where the
test waits for it. The protocol and the reasons behind it are in
artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v11.md.

Requests (one logcat line each, tag Unity):

    NTK-UI-HELLO|<run>|<package>|<user>
    NTK-UI|<run>|<number>|<command>|<arg>...

Results: files/ntk-ui/<run>/<number> holds "ok", "ok <value>" or "fail <reason>".

It changes device settings while it runs (animations off, screen kept on, autofill off, orientation
when asked) and restores them when it stops, on SIGTERM, SIGINT and SIGHUP too. The values it
replaced are also written to a state file on the host; if a run stopped before restoring them, the
next start restores them first.

The share sheet commands (tap-in-sheet, absent-in-sheet, present-in-sheet, close-sheet, no-sheet)
each do their work within a 60-second limit; when they fail, they close the sheet (Back, then
stopping the sheet's process) and bring the app back to the front, each with its own limit, so they
give up within SHEET_ANSWER_LIMIT of taking the request (an answer that cannot be written then sets
exit status 1). They answer with fixed words only, and never keep or log a
screen that shows the sheet: it lists other apps' share targets and the user's contacts.

Autofill is turned off because a login dialog's password field makes Google's autofill offer to save
the password; that prompt then takes the focus, and `uiautomator dump`, which reads the focused
window, sees only it for the rest of the run (2026-10-11).

Exit status: 0 fine; 1 a setting could not be restored, `background` could not bring the app back, a
screen read could not be deleted from the device, or an answer could not be written;
2 the device could not be prepared (locked with a PIN); 3 a HELLO got no READY.

Usage: scripts/android_ui_driver.py [--serial SERIAL] [--state-file PATH] [--dump-dir DIR]
"""

import argparse
import base64
import json
import os
import re
import shlex
import signal
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

COMMAND_TIMEOUT = 10.0
SHEET_TIMEOUT = 60.0
SHEET_CLOSE_TIMEOUT = 15.0
SHEET_STOP_TIMEOUT = 10.0
SHEET_PACKAGE = "com.android.intentresolver"
SHEET_SWIPES = 8
SHEET_TAP_TRIES = 3
SHEET_BACK_TRIES = 4
FRONT_TIMEOUT = 25.0
FRONT_READ_TIMEOUT = 3.0
DELETE_TIMEOUT = 5.0
RESULT_TIMEOUT = 10.0
UNSEEN_WATCH = 10.0
# The longest a share sheet command takes to answer, on a device whose every adb call times out
# (design 3.3; pinned by the doctests below). AndroidUiDriver.SheetAnswerTimeoutSeconds waits longer.
SHEET_ANSWER_LIMIT = (SHEET_TIMEOUT + UNSEEN_WATCH + SHEET_CLOSE_TIMEOUT + SHEET_STOP_TIMEOUT + FRONT_TIMEOUT
                      + 4 * DELETE_TIMEOUT + 1 + RESULT_TIMEOUT)
# The commands that run inside sheet_command, arguments and all. AndroidUiDriver.cs keeps the same
# list (s_sheetCommands) to wait longer for them: change both together.
SHEET_COMMANDS = frozenset({"tap-in-sheet", "absent-in-sheet", "present-in-sheet", "close-sheet", "no-sheet"})
# The only reasons a share sheet command answers with (design 3.4); anything else becomes "error".
SHEET_REASONS = frozenset({
    "timeout", "bad-dump", "no-screen-size", "sheet-did-not-open", "sheet-closed", "not-found",
    "anchor-not-found", "present", "still-open", "did-not-close", "wrong-app", "not-in-front", "sheet-opened",
    "bad-span",
})
READY_TIMEOUT = 5.0
DUMP_PATH = "/data/local/tmp/ntk-ui-dump.xml"
MIN_OUTSIDE_GAP = 48
TYPE_TEXT = re.compile(r"^[a-z0-9]{1,64}$")
REASON = re.compile(r"[^A-Za-z0-9-]")

# (namespace, key) -> the value used during a run; None deletes the setting.
RUN_SETTINGS = {
    ("global", "stay_on_while_plugged_in"): "7",
    ("global", "window_animation_scale"): "0",
    ("global", "transition_animation_scale"): "0",
    ("global", "animator_duration_scale"): "0",
    ("secure", "autofill_service"): None,
}
ROTATION_SETTINGS = ("accelerometer_rotation", "user_rotation")


class Failure(Exception):
    """A command failed; the message becomes the result's reason."""


def reason(text):
    """The reason as the result file carries it: letters, digits and '-' only.

    >>> reason("not found: android:id/button1")
    'not-found--android-id-button1'
    """
    return REASON.sub("-", text)


def decode(value):
    """A Base64 UTF-8 argument.

    >>> decode("44OA44Kk44Ki44Ot44KwLfCgrrc=")
    'ダイアログ-𠮷'
    """
    return base64.b64decode(value).decode("utf-8")


def encode(text):
    """The Base64 UTF-8 argument for text, as the tests send it.

    >>> encode("ダイアログ-𠮷")
    '44OA44Kk44Ki44Ot44KwLfCgrrc='
    """
    return base64.b64encode(text.encode("utf-8")).decode("ascii")


def parse_selector(text):
    """A selector: attribute=Base64 value pairs joined by '&'; index is plain digits.

    >>> parse_selector("resource-id=YW5kcm9pZDppZC9idXR0b24x")
    ({'resource-id': 'android:id/button1'}, 0)
    >>> parse_selector("class=YW5kcm9pZC53aWRnZXQuRWRpdFRleHQ=&index=1")
    ({'class': 'android.widget.EditText'}, 1)
    """
    attributes, index = {}, 0
    for part in text.split("&"):
        key, _, value = part.partition("=")
        if key == "index":
            index = int(value)
        elif key in ("resource-id", "text", "class", "package"):
            attributes[key] = decode(value)
        else:
            raise Failure(f"bad selector key {key}")
    return attributes, index


def parse_request(line):
    """('hello', run, package, user) or ('command', run, number, command, args), or None.

    >>> parse_request("NTK-UI-HELLO|ab12|com.example|0")
    ('hello', 'ab12', 'com.example', '0')
    >>> parse_request("NTK-UI|ab12|7|tap|resource-id=YQ==")
    ('command', 'ab12', 7, 'tap', ['resource-id=YQ=='])
    >>> parse_request("something else") is None
    True
    """
    line = line.strip()
    if line.startswith("NTK-UI-HELLO|"):
        parts = line.split("|")
        if len(parts) == 4:
            return ("hello", parts[1], parts[2], parts[3])
        return None
    if line.startswith("NTK-UI|"):
        parts = line.split("|")
        if len(parts) >= 4 and parts[2].isdigit():
            return ("command", parts[1], int(parts[2]), parts[3], parts[4:])
    return None


def bounds(node):
    """(left, top, right, bottom) of a dumped node.

    >>> bounds(ET.fromstring('<node bounds="[10,20][30,40]"/>'))
    (10, 20, 30, 40)
    """
    numbers = [int(n) for n in re.findall(r"-?\d+", node.get("bounds", ""))]
    if len(numbers) != 4:
        raise Failure("node without bounds")
    return tuple(numbers)


def find(root, attributes, index):
    """The index-th node, top to bottom, whose attributes all match.

    >>> root = ET.fromstring('<hierarchy><node class="E" bounds="[0,50][10,60]"/>'
    ...                      '<node class="E" bounds="[0,10][10,20]"/></hierarchy>')
    >>> bounds(find(root, {"class": "E"}, 0))
    (0, 10, 10, 20)
    >>> find(root, {"class": "X"}, 0) is None
    True
    """
    matches = [n for n in root.iter("node") if all(n.get(k) == v for k, v in attributes.items())]
    matches.sort(key=lambda n: (bounds(n)[1], bounds(n)[0]))
    return matches[index] if index < len(matches) else None


def count(root, attributes):
    """The number of nodes whose attributes all match."""
    return sum(1 for n in root.iter("node") if all(n.get(k) == v for k, v in attributes.items()))


def outside_point(root, node):
    """The point to tap outside the dialog that holds node, or None when there is no room.

    `uiautomator dump` reads the focused window, so for a dialog the outermost node is the dialog's
    own window, not the screen. That window is the reference: the tap goes halfway between the top
    of the screen and the top of the window, at the window's horizontal centre.

    >>> root = ET.fromstring(
    ...     '<hierarchy><node bounds="[28,1009][1052,1522]">'
    ...     '<node bounds="[70,1051][1010,1480]"><node bounds="[120,1060][880,1100]" text="T"/></node>'
    ...     '</node></hierarchy>')
    >>> outside_point(root, find(root, {"text": "T"}, 0))
    (540, 504)
    >>> tight = ET.fromstring(
    ...     '<hierarchy><node bounds="[0,40][1000,1400]"><node bounds="[0,50][1000,90]" text="T"/></node></hierarchy>')
    >>> outside_point(tight, find(tight, {"text": "T"}, 0)) is None
    True
    """
    window = None
    for top in root:
        if any(n is node for n in top.iter()):
            window = top
            break
    if window is None:
        raise Failure("the node is not in the dump")
    left, top, right, bottom = bounds(window)
    if top < MIN_OUTSIDE_GAP:
        return None
    return ((left + right) // 2, top // 2)


def shows_sheet(root):
    """Whether a dumped screen shows the share sheet.

    >>> shows_sheet(ET.fromstring('<hierarchy><node package="com.android.intentresolver"/></hierarchy>'))
    True
    >>> shows_sheet(ET.fromstring('<hierarchy><node package="com.example"/></hierarchy>'))
    False
    """
    return any(n.get("package") == SHEET_PACKAGE for n in root.iter("node"))


def find_in_sheet(root, text):
    """The share sheet node whose text is exactly text (case-sensitive), or None.

    >>> root = ET.fromstring('<hierarchy>'
    ...     '<node package="com.android.intentresolver" text="Save" bounds="[0,900][100,1000]"/>'
    ...     '<node package="com.example" text="Save" bounds="[0,10][100,20]"/></hierarchy>')
    >>> bounds(find_in_sheet(root, "Save"))
    (0, 900, 100, 1000)
    >>> find_in_sheet(root, "save") is None
    True
    """
    return find(root, {"package": SHEET_PACKAGE, "text": text}, 0)


def remaining(deadline, cap, now=None):
    """The time an adb call may take: what is left before the deadline, at most cap. Past the
    deadline nothing is left, and the command stops with "timeout" (design 3.3).

    >>> remaining(100.0, 30, now=95.0)
    5.0
    >>> remaining(100.0, 3, now=50.0)
    3
    >>> try:
    ...     remaining(100.0, 30, now=100.0)
    ... except Failure as error:
    ...     print(error)
    timeout
    """
    now = time.monotonic() if now is None else now
    left = deadline - now
    if left <= 0:
        raise Failure("timeout")
    return min(cap, left)


def sheet_reason(error):
    """The fixed word a share sheet command answers with for error (design 3.4).

    >>> sheet_reason(Failure("not-found"))
    'not-found'
    >>> sheet_reason(Failure("adb shell input tap 1 2 exited 1: Alice"))
    'error'
    >>> sheet_reason(subprocess.TimeoutExpired("adb", 5))
    'timeout'
    >>> sheet_reason(ValueError("Alice"))
    'error'
    """
    if isinstance(error, subprocess.TimeoutExpired):
        return "timeout"
    if isinstance(error, Failure) and str(error) in SHEET_REASONS:
        return str(error)
    return "error"


def resumed_package(text):
    """The package of the resumed activity in `dumpsys activity activities`, or None.

    >>> resumed_package("  topResumedActivity=ActivityRecord{3481 u0 com.example/com.example.Main t37}")
    'com.example'
    >>> resumed_package("  ResumedActivity: ActivityRecord{3481 u0 com.example/.Main t37}")
    'com.example'
    >>> resumed_package("nothing") is None
    True
    """
    match = (re.search(r"topResumedActivity=ActivityRecord\{\S+ u\d+ ([^/\s]+)/", text)
             or re.search(r"ResumedActivity: ActivityRecord\{\S+ u\d+ ([^/\s]+)/", text))
    return match.group(1) if match else None


def restore_command(key, namespace, value):
    """The settings command that puts a saved value back; an absent setting is deleted.

    >>> restore_command("user_rotation", "system", None)
    ['settings', 'delete', 'system', 'user_rotation']
    >>> restore_command("animator_duration_scale", "global", "1.0")
    ['settings', 'put', 'global', 'animator_duration_scale', '1.0']
    """
    if value is None:
        return ["settings", "delete", namespace, key]
    return ["settings", "put", namespace, key, value]


class Device:
    def __init__(self, serial):
        self.prefix = ["adb"] + (["-s", serial] if serial else [])

    def run(self, *args, check=True, timeout=30):
        result = subprocess.run(self.prefix + list(args), capture_output=True, text=True, timeout=timeout)
        if check and result.returncode != 0:
            raise Failure(f"adb {' '.join(args)} exited {result.returncode}: {result.stderr.strip()}")
        return result.stdout

    def shell(self, command, check=True, timeout=30):
        """Runs one device shell command line. adb joins its arguments with spaces, so the line is
        passed as a single argument and quoted here, never split by adb."""
        return self.run("shell", command, check=check, timeout=timeout)

    def setting(self, namespace, key):
        value = self.shell(f"settings get {namespace} {key}").strip()
        return None if value in ("", "null") else value


class Driver:
    def __init__(self, device, state_file=None, dump_dir=None, clock=time.monotonic, sleep=time.sleep):
        self.device = device
        self.now = clock
        self.sleep = sleep
        self.warn = lambda text: print(f"ui-driver: {text}", file=sys.stderr)
        self.dump_left = False  # a screen read may still be on the device: deleting it failed
        self.seen_sheet = False  # the current share sheet command has read a screen with the sheet
        self.deadline = None  # the deadline of the command being run, other than a share sheet one
        self.state_file = state_file
        self.dump_dir = dump_dir
        self.last_dump = None
        self.saved = {}
        self.run_id = None
        self.package = None
        self.user = None
        self.hellos = 0
        self.readies = 0
        self.commands = 0
        self.exit_code = 0

    # -- device preparation -------------------------------------------------------------

    def prepare(self):
        self.device.shell("input keyevent KEYCODE_WAKEUP")
        self.device.shell("wm dismiss-keyguard", check=False)
        self.sleep(1)
        window = self.device.shell("dumpsys window")
        if re.search(r"mDreamingLockscreen=true|isKeyguardShowing=true|mShowingLockscreen=true", window):
            print("ui-driver: the device is locked (a PIN?); unlock it and run again", file=sys.stderr)
            sys.exit(2)
        for (namespace, key), value in RUN_SETTINGS.items():
            self.save(namespace, key)
            if value is None:
                self.device.shell(f"settings delete {namespace} {key}")
            else:
                self.device.shell(f"settings put {namespace} {key} {value}")

    def save(self, namespace, key):
        if (namespace, key) not in self.saved:
            self.saved[(namespace, key)] = self.device.setting(namespace, key)
            print(f"ui-driver: saved {namespace} {key}={self.saved[(namespace, key)]}", flush=True)
            self.write_state()

    def write_state(self):
        if self.state_file:
            with open(self.state_file, "w", encoding="utf-8") as out:
                json.dump([[n, k, v] for (n, k), v in self.saved.items()], out)

    def restore_previous(self):
        """Restores what a run that stopped early left in the state file."""
        if not self.state_file or not os.path.exists(self.state_file):
            return
        with open(self.state_file, encoding="utf-8") as source:
            for namespace, key, value in json.load(source):
                self.saved[(namespace, key)] = value
        print(f"ui-driver: restoring {len(self.saved)} settings a previous run left changed", flush=True)
        self.restore()
        self.saved = {}

    def restore(self):
        failed = False
        for (namespace, key), value in self.saved.items():
            try:
                self.device.shell(" ".join(restore_command(key, namespace, value)))
                if self.device.setting(namespace, key) != value:
                    raise Failure(f"{namespace} {key} did not return to {value}")
            except Exception as error:  # noqa: BLE001 - report every setting that stays changed
                print(f"ui-driver: could not restore {namespace} {key}: {error}", file=sys.stderr)
                self.exit_code = max(self.exit_code, 1)
                failed = True
        if not failed and self.state_file and os.path.exists(self.state_file):
            os.remove(self.state_file)

    # -- result files ---------------------------------------------------------------------

    def as_app(self, script, timeout=30):
        quoted = shlex.quote(script)
        return self.device.shell(f"run-as {self.package} --user {self.user} sh -c {quoted}", timeout=timeout)

    def write_result(self, name, text):
        directory = f"files/ntk-ui/{self.run_id}"
        self.as_app(f"echo {shlex.quote(text)} > {directory}/.{name}.tmp && mv {directory}/.{name}.tmp {directory}/{name}",
                    timeout=RESULT_TIMEOUT)

    # -- requests ----------------------------------------------------------------------------

    def hello(self, run_id, package, user):
        self.hellos += 1
        self.run_id, self.package, self.user = run_id, package, user
        started = self.now()
        try:
            self.as_app(f"rm -rf files/ntk-ui && mkdir -p files/ntk-ui/{run_id}")
            self.write_result("probe", "probe")
            while self.as_app(f"ls files/ntk-ui/{run_id}").split().count("probe"):
                if self.now() - started > READY_TIMEOUT:
                    raise Failure("the test did not take the probe")
                self.sleep(0.2)
            self.write_result("ready", "ready")
            self.readies += 1
        except Exception as error:  # noqa: BLE001 - any failure means no READY
            print(f"ui-driver: HELLO {run_id} got no READY: {error}", file=sys.stderr)
            self.exit_code = max(self.exit_code, 3)

    def command(self, number, name, args):
        """Runs a command and writes its result. A _Deferred result is written as "ok" first and
        its follow-up work (background's return trip) runs after, so the test is not held up."""
        self.commands += 1
        deferred = None
        try:
            handler = getattr(self, "do_" + name.replace("-", "_"), None)
            if handler is None:
                raise Failure(f"unknown command {name}")
            if name in SHEET_COMMANDS:
                # Reading the arguments too: the test opened the sheet as it sent the request, so a
                # bad argument must close it like any other failure (design 3.3).
                value = self.sheet_command(lambda deadline: handler(deadline, *args))
            else:
                self.deadline = self.now() + COMMAND_TIMEOUT
                try:
                    value = handler(*args)
                finally:
                    self.deadline = None
            if isinstance(value, _Deferred):
                deferred, value = value, None
            text = "ok" if value is None else f"ok {value}"
        except Failure as error:
            text = "fail " + reason(str(error))
        except Exception as error:  # noqa: BLE001 - the test must hear about any failure
            text = "fail " + reason(f"{type(error).__name__} {error}")
        try:
            self.write_result(str(number), text)
        except Exception as error:  # noqa: BLE001
            # The test fails when the answer does not come; the exit status fails the gate too.
            self.warn(f"could not write result {number}: {type(error).__name__}")
            self.exit_code = max(self.exit_code, 1)
        if deferred is not None:
            deferred.after()

    # -- reading the screen ----------------------------------------------------------------------

    def limit(self, deadline, seconds):
        """An adb call's timeout: seconds, or with a deadline only what is left of it (design 3.3)."""
        return seconds if deadline is None else remaining(deadline, seconds, self.now())

    def dump(self, deadline=None):
        """Reads the screen. With a deadline, each adb call may take only the time left (design 3.3).
        A screen that shows the share sheet, or one that cannot be read and so might, is never kept,
        and its copy on the device is deleted at once (design 3.4): it lists other apps' share
        targets and the user's contacts."""
        self.last_dump = None
        first = self.limit(deadline, 30)  # past the deadline nothing starts, so nothing to delete
        try:
            self.device.shell(f"rm -f {DUMP_PATH}", check=False, timeout=first)
            out = self.device.shell(f"uiautomator dump {DUMP_PATH}", check=False, timeout=self.limit(deadline, 15))
            if "dumped to" not in out:
                raise Failure("uiautomator dump failed")
            text = self.device.run("exec-out", "cat", DUMP_PATH, timeout=self.limit(deadline, 30))
            root = ET.fromstring(text)
        except Exception as error:  # noqa: BLE001 - whatever stopped the read, the copy may be the sheet
            self.forget_dump()
            if isinstance(error, subprocess.TimeoutExpired):
                raise Failure("timeout") from None
            if isinstance(error, ET.ParseError):
                raise Failure("bad-dump") from None
            raise
        if shows_sheet(root):
            self.seen_sheet = True
            self.forget_dump()
        else:
            self.last_dump = text
        return root

    def forget_dump(self):
        """Deletes the screen read on the device. A failure is remembered: the share sheet command's
        end and the driver's own end delete it again, and the driver's exit status reports a screen
        it could not delete (design 3.4)."""
        try:
            self.device.shell(f"rm -f {DUMP_PATH}", timeout=DELETE_TIMEOUT)
            self.dump_left = False
        except Exception:  # noqa: BLE001
            self.dump_left = True

    def wait_for(self, selector, present=True):
        """Reads the screen until the selector matches (or, with present=False, stops matching),
        within the command's COMMAND_TIMEOUT. On a timeout, keeps the last screen it could read
        (never one with the share sheet) for finding out what was in the way."""
        attributes, index = parse_selector(selector)
        deadline = self.command_deadline()
        screen = None
        while True:
            try:
                root = self.dump(deadline)
                screen = self.last_dump or screen
                node = find(root, attributes, index)
                if (node is not None) == present:
                    return root, node
            except Failure:
                pass
            if self.now() >= deadline:
                self.keep_last_dump(screen)
                raise Failure("timeout waiting for " + ("" if present else "the end of ") + selector)
            self.sleep(0.5)

    def command_deadline(self):
        """The deadline of the command being run: COMMAND_TIMEOUT from when it was taken (design 6)."""
        return self.deadline if self.deadline is not None else self.now() + COMMAND_TIMEOUT

    def shell(self, command):
        """A device shell command within the command's deadline."""
        return self.device.shell(command, timeout=self.limit(self.command_deadline(), 30))

    def keep_last_dump(self, screen):
        """Keeps a screen, for finding out what was in the way."""
        if self.dump_dir and screen:
            os.makedirs(self.dump_dir, exist_ok=True)
            path = os.path.join(self.dump_dir, f"timeout-{self.commands}.xml")
            with open(path, "w", encoding="utf-8") as out:
                out.write(screen)
            self.warn(f"kept the last screen in {path}")

    # -- commands ---------------------------------------------------------------------------------
    #
    # Each command other than the share sheet ones finishes within COMMAND_TIMEOUT of being taken:
    # every adb call it makes gets the time left (design 6).

    def do_wait(self, selector):
        self.wait_for(selector)

    def do_wait_gone(self, selector):
        self.wait_for(selector, present=False)

    def do_tap(self, selector):
        _, node = self.wait_for(selector)
        if node.get("enabled") != "true":
            raise Failure("disabled")
        left, top, right, bottom = bounds(node)
        self.shell(f"input tap {(left + right) // 2} {(top + bottom) // 2}")

    def do_tap_outside(self, selector):
        root, node = self.wait_for(selector)
        point = outside_point(root, node)
        if point is None:
            raise Failure("no-outside")
        self.shell(f"input tap {point[0]} {point[1]}")

    def do_type(self, selector, text):
        if not TYPE_TEXT.match(text):
            raise Failure("bad-text")
        self.do_tap(selector)
        self.shell(f"input text {text}")

    def keyboard_shown(self):
        return "mInputShown=true" in self.shell("dumpsys input_method")

    def do_back(self, selector):
        self.wait_for(selector)
        if self.keyboard_shown():
            self.shell("input keyevent KEYCODE_BACK")
            while self.keyboard_shown():
                if self.now() >= self.command_deadline():
                    raise Failure("the keyboard did not close")
                self.sleep(0.2)
        self.shell("input keyevent KEYCODE_BACK")

    def do_expect(self, selector, attribute, value):
        root, node = self.wait_for(selector)
        expected = decode(value)
        actual = node.get(attribute)
        if actual != expected:
            if shows_sheet(root):
                raise Failure("mismatch")  # what the share sheet shows never goes into an answer
            raise Failure(f"{attribute} is {actual!r}, not {expected!r}")

    def do_count(self, selector):
        attributes, _ = parse_selector(selector)
        return count(self.dump(self.command_deadline()), attributes)

    def do_rotation(self):
        return self.dump(self.command_deadline()).get("rotation")

    def do_rotate(self, value):
        if value not in ("0", "1", "2", "3"):
            raise Failure("bad rotation")
        for key in ROTATION_SETTINGS:
            self.save("system", key)
        self.shell("settings put system accelerometer_rotation 0")
        self.shell(f"settings put system user_rotation {value}")
        while self.dump(self.command_deadline()).get("rotation") != value:
            if self.now() >= self.command_deadline():
                raise Failure("the display did not turn")
            self.sleep(0.5)

    def app_stopped(self):
        """Whether every activity record of the package reports STOPPED."""
        text = self.shell("dumpsys activity activities")
        states = []
        current = None
        for line in text.splitlines():
            if "ActivityRecord{" in line:
                current = self.package in line
            elif current:
                match = re.search(r"\bstate=([A-Z_]+)", line)
                if match:
                    states.append(match.group(1))
                    current = False
        return bool(states) and all(state == "STOPPED" for state in states)

    def do_background(self, milliseconds):
        delay = int(milliseconds)
        if not 100 <= delay <= 5000:
            raise Failure("bad delay")
        self.shell("input keyevent KEYCODE_HOME")
        while not self.app_stopped():
            if self.now() >= self.command_deadline():
                raise Failure("the app did not stop")
            self.sleep(0.2)
        return _Deferred(lambda: self.bring_back(delay))

    def bring_back(self, delay):
        self.sleep(delay / 1000)
        if not self.bring_front(quiet=True):
            self.warn("could not bring the app back")
            self.exit_code = max(self.exit_code, 1)

    def do_home(self):
        self.shell("input keyevent KEYCODE_HOME")

    # -- the share sheet (design 3.1, 3.3, 3.4) ---------------------------------------------------
    #
    # Each command finishes on its own within SHEET_TIMEOUT, closes the sheet when it fails, and
    # answers with fixed words only: a reason never carries what the sheet showed.

    def sheet_command(self, work):
        """Runs work(deadline) as a share sheet command. Whatever it raises, the sheet is closed
        (after watching for one not yet seen) and this app is brought back to the front before the
        answer, even when closing failed; the answer is a fixed word; and no screen read on the way
        is kept, on the host or on the device."""
        deadline = self.now() + SHEET_TIMEOUT
        self.seen_sheet = False
        try:
            return work(deadline)
        except Exception as error:  # noqa: BLE001 - every failure must close the sheet
            word = sheet_reason(error)
            if word == "error":
                self.warn(f"a share sheet command stopped on {type(error).__name__}")
            if not self.seen_sheet and word != "sheet-did-not-open":
                # Failed before the sheet was seen (a bad argument, say): the test opened it in the
                # same frame as the request, so it may still be coming. Watch for it, then close.
                self.watch_for_sheet()
            if word != "did-not-close" and not self.close_sheet():
                self.warn(f"{word}, and the share sheet did not close")
                word = "did-not-close"
            if not self.bring_front() and word != "did-not-close":
                self.warn(f"{word}, and the player did not come back to the front")
                word = "not-in-front"
            raise Failure(word) from None
        finally:
            self.last_dump = None
            self.forget_dump()

    def watch_for_sheet(self):
        """Waits up to UNSEEN_WATCH for the sheet to show. Never raises."""
        until = self.now() + UNSEEN_WATCH
        while self.now() < until:
            try:
                if shows_sheet(self.dump(until)):
                    return
            except Exception:  # noqa: BLE001
                pass
            self.sleep(0.5)

    def read(self, deadline):
        """The screen, read again after a failed read until the deadline (design 6)."""
        while True:
            try:
                return self.dump(deadline)
            except Failure:
                if self.now() >= deadline:
                    raise Failure("timeout") from None
            self.sleep(1)

    def screen_size(self, deadline):
        out = self.device.shell("wm size", timeout=self.limit(deadline, 30))
        sizes = re.findall(r"(\d+)x(\d+)", out)
        if not sizes:
            raise Failure("no-screen-size")
        width, height = sizes[-1]  # an override size, when set, comes last
        return int(width), int(height)

    def wait_sheet(self, deadline):
        """The screen once the sheet shows."""
        until = min(deadline, self.now() + COMMAND_TIMEOUT)
        while True:
            try:
                root = self.dump(until)
                if shows_sheet(root):
                    return root
            except Failure:
                pass
            if self.now() >= until:
                raise Failure("timeout" if until >= deadline else "sheet-did-not-open")
            self.sleep(0.5)

    def wait_in_sheet(self, label, deadline, missing):
        """The screen once the sheet shows label without swiping, read again for up to
        COMMAND_TIMEOUT after the sheet shows, while it fills in."""
        root = self.wait_sheet(deadline)
        until = min(deadline, self.now() + COMMAND_TIMEOUT)
        while find_in_sheet(root, label) is None:
            if self.now() >= until:
                raise Failure("timeout" if until >= deadline else missing)
            self.sleep(0.5)
            try:
                root = self.read(until)
            except Failure:
                raise Failure("timeout" if until >= deadline else missing) from None
        return root

    def wait_sheet_gone(self, until):
        """True once a read shows no sheet; False if it still shows at until."""
        while self.now() < until:
            try:
                if not shows_sheet(self.dump(until)):
                    return True
            except Failure:
                pass
            self.sleep(0.5)
        return False

    def search_sheet(self, root, label, deadline):
        """The sheet node with the label, swiping up to look further (native-toolkit's UiDriver.kt:219-228:
        the first swipe opens the sheet fully, the later ones scroll its list). root is looked at
        before any swipe."""
        width, height = self.screen_size(deadline)
        for swipe in range(SHEET_SWIPES + 1):
            node = find_in_sheet(root, label)
            if node is not None:
                return node
            if not shows_sheet(root):
                raise Failure("sheet-closed")
            if swipe == SHEET_SWIPES:
                break
            x = width // 2
            self.device.shell(f"input swipe {x} {height * 3 // 4} {x} {height // 4} 300", timeout=self.limit(deadline, 30))
            self.sleep(0.5)
            root = self.read(deadline)
        raise Failure("not-found")

    def close_sheet(self):
        """Closes the sheet if it shows, with its own limits. True when it is gone. Never raises.

        Back first, up to SHEET_BACK_TRIES times: a sheet swiped fully open may take one Back to fold
        and another to close. If Back does not close it, the sheet's process is stopped (V-16)."""
        deadline = self.now() + SHEET_CLOSE_TIMEOUT
        try:
            for _ in range(SHEET_BACK_TRIES):
                if not shows_sheet(self.read(deadline)):
                    return True
                self.device.shell("input keyevent KEYCODE_BACK", timeout=self.limit(deadline, 30))
                self.sleep(1)
            if not shows_sheet(self.read(deadline)):
                return True
        except Exception:  # noqa: BLE001 - any failure here goes on to stopping the sheet
            pass
        return self.stop_sheet()

    def stop_sheet(self):
        """Stops the sheet's process, the last resort when Back does not close it. True when it is
        gone. Never raises."""
        deadline = self.now() + SHEET_STOP_TIMEOUT
        self.warn("Back did not close the share sheet; stopping it")
        try:
            self.device.shell(f"am force-stop {SHEET_PACKAGE}", timeout=self.limit(deadline, 30))
            return self.wait_sheet_gone(deadline)
        except Exception:  # noqa: BLE001
            return False

    def close_or_fail(self):
        if not self.close_sheet():
            raise Failure("did-not-close")

    def front_package(self, deadline):
        return resumed_package(self.device.shell("dumpsys activity activities", timeout=self.limit(deadline, 30)))

    def check_front(self, deadline):
        """After a tap in the sheet, waits for this app to come to the front. When another app does
        (one with the same name as the test player, say), brings this app back and fails, instead
        of leaving the player stopped behind it (V-12, the test plan's precondition)."""
        until = min(deadline, self.now() + COMMAND_TIMEOUT)
        while True:
            try:
                # Each read gets at most FRONT_READ_TIMEOUT, so one that hangs leaves time to read again.
                if self.front_package(min(until, self.now() + FRONT_READ_TIMEOUT)) == self.package:
                    return
            except (Failure, subprocess.TimeoutExpired):
                pass
            if self.now() >= until:
                # sheet_command brings this app back either way
                raise Failure("timeout" if until >= deadline else "wrong-app")
            self.sleep(0.5)

    def bring_front(self, quiet=False):
        """After a failure, makes sure this app is in front, bringing it back if not, with its own
        limit as closing the sheet has: the player must not stay stopped behind another app. True
        when it is in front. Never raises."""
        deadline = self.now() + FRONT_TIMEOUT
        try:
            if self.front_package(deadline) == self.package:
                return True
            if not quiet:
                self.warn("another app is in front; bringing the player back")
            self.do_front(deadline)
            while self.front_package(deadline) != self.package:
                self.sleep(0.5)
            return True
        except Exception:  # noqa: BLE001
            return False

    def do_tap_in_sheet(self, deadline, text):
        label = decode(text)
        root = self.wait_sheet(deadline)
        for attempt in range(SHEET_TAP_TRIES):
            if attempt > 0 and not shows_sheet(root):
                break  # the last tap closed it after all (native-toolkit's UiDriver.kt pick)
            left, top, right, bottom = bounds(self.search_sheet(root, label, deadline))
            self.device.shell(f"input tap {(left + right) // 2} {(top + bottom) // 2}", timeout=self.limit(deadline, 30))
            if self.wait_sheet_gone(min(deadline, self.now() + COMMAND_TIMEOUT)):
                break
            root = self.read(deadline)  # the node went stale under the tap: look again
        else:
            raise Failure("still-open")
        self.check_front(deadline)

    def do_absent_in_sheet(self, deadline, absent_text, anchor_text):
        """ok when the sheet shows anchor and, looked through with swipes, not absent. The anchor
        shows the sheet has filled in: without it, an empty sheet would pass."""
        absent, anchor = decode(absent_text), decode(anchor_text)
        root = self.wait_in_sheet(anchor, deadline, "anchor-not-found")
        try:
            self.search_sheet(root, absent, deadline)
        except Failure as error:
            if str(error) != "not-found":
                raise
        else:
            raise Failure("present")
        self.close_or_fail()

    def do_present_in_sheet(self, deadline, text):
        self.wait_in_sheet(decode(text), deadline, "not-found")
        self.close_or_fail()

    def do_close_sheet(self, deadline):
        self.wait_sheet(deadline)
        self.close_or_fail()

    def do_no_sheet(self, deadline, milliseconds):
        """ok when the sheet does not show for the given time; when it does, closes it and fails.
        For a call that must not open the sheet: without this, a sheet opened by mistake would keep
        Unity paused until the test run's own limit. Only screens actually read count: a read that
        fails is read again, and the last read is at or after the end of the time."""
        span = int(milliseconds)
        if not 1000 <= span <= 30000:
            raise Failure("bad-span")
        until = min(deadline, self.now() + span / 1000)
        # A sheet that opens stays open (Unity is paused and nothing else closes it), so a read
        # after the time still sees one that opened while reads were failing. Reads may fail for
        # at most COMMAND_TIMEOUT past the time; then the answer is timeout, never ok.
        reads_until = min(deadline, until + COMMAND_TIMEOUT)
        while True:
            if shows_sheet(self.read(reads_until)):
                raise Failure("sheet-opened")
            if self.now() >= until:
                return
            self.sleep(0.5)

    def do_front(self, deadline=None):
        deadline = self.command_deadline() if deadline is None else deadline
        resolved = self.device.shell(
            f"cmd package resolve-activity --brief -c android.intent.category.LAUNCHER {self.package}",
            timeout=self.limit(deadline, 30)).split()
        component = resolved[-1] if resolved else ""
        if "/" not in component:
            raise Failure("no launcher activity")
        self.device.shell(f"am start -n {component}", timeout=self.limit(deadline, 30))


class _Deferred:
    """A result that is written first, with work that runs after it (background's return trip)."""

    def __init__(self, after):
        self.after = after


class ScriptedDevice:
    """A stand-in device for the doctests below: a share sheet that Back, a tap or stopping its
    process closes, and a clock that every adb call moves on by `seconds`, so the time limits can be
    checked without a device."""

    PLAYER = "com.example.player"

    def __init__(self, labels, sheet=True, seconds=0.5, backs_to_close=1, stoppable=True,
                 chosen=PLAYER, bad_reads=0, failing=None, broken=None, late_reads=0, slow=None,
                 stuck_rm=False, bad_at=(), open_at=None, front_sticks=False, front=PLAYER, flaky=(), slow_once=None):
        self.labels, self.sheet, self.seconds = list(labels), sheet, seconds
        self.backs_to_close, self.stoppable, self.chosen = backs_to_close, stoppable, chosen
        self.bad_reads, self.failing, self.broken = bad_reads, failing, broken
        self.late_reads, self.slow, self.stuck_rm = late_reads, slow or {}, stuck_rm
        self.bad_at, self.reads = set(bad_at), 0  # the reads (1, 2, ...) that come back broken
        self.open_at = open_at  # the time the sheet shows, for a sheet that comes late
        self.front_sticks = front_sticks  # am start does not bring the player back
        self.flaky = list(flaky)  # command prefixes that fail once each
        self.slow_once = dict(slow_once or {})  # command prefix -> seconds, for one call only
        self.calls = []  # every adb call, for the doctests that look at what was sent
        if open_at is not None:
            self.sheet = False
        self.closing = None  # reads left before a tapped sheet goes
        self.front = front
        self.time = 0.0
        self.file = None
        self.answer = None
        self.backs = self.swipes = self.stopped = self.taps = 0

    def now(self):
        return self.time

    def sleep(self, seconds):
        self.time += seconds

    def take(self, command, timeout):
        if self.open_at is not None and self.time >= self.open_at:
            self.sheet, self.open_at = True, None
        self.calls.append(command)
        seconds = next((v for k, v in self.slow.items() if command.startswith(k)), self.seconds)
        for prefix in list(self.slow_once):
            if command.startswith(prefix):
                seconds = self.slow_once.pop(prefix)
        if timeout < seconds:
            self.time += timeout
            raise subprocess.TimeoutExpired(command, timeout)
        self.time += seconds
        if self.failing and command.startswith(self.failing):
            raise Failure(f"adb shell {command} exited 1: Alice")
        if self.broken and command.startswith(self.broken):
            raise ValueError("Alice")
        for prefix in self.flaky:
            if command.startswith(prefix):
                self.flaky.remove(prefix)
                raise Failure(f"adb shell {command} exited 1")
        if self.stuck_rm and command.startswith("rm -f"):
            raise Failure("adb shell rm exited 1")

    def screen(self):
        if not self.sheet:
            return f'<hierarchy><node package="{self.front}" text="player" enabled="true" bounds="[0,0][1080,2400]"/></hierarchy>'
        nodes = "".join(f'<node package="{SHEET_PACKAGE}" text="{label}" bounds="[0,{100 * i + 100}][1080,{100 * i + 180}]"/>'
                        for i, label in enumerate(self.labels))
        return f"<hierarchy>{nodes}</hierarchy>"

    def shell(self, command, check=True, timeout=30):
        self.take(command, timeout)
        if command.startswith("run-as"):  # the answer for the test
            script = shlex.split(command)[-1]
            if script.startswith("echo "):
                self.answer = shlex.split(script)[1]
            return ""
        if command == f"rm -f {DUMP_PATH}":
            self.file = None
        elif command == f"uiautomator dump {DUMP_PATH}":
            if self.closing is not None:
                self.closing -= 1
                if self.closing < 0:
                    self.sheet, self.front, self.closing = False, self.chosen, None
            self.file = self.screen()
            self.reads += 1
            if self.bad_reads or self.reads in self.bad_at:
                self.bad_reads = max(0, self.bad_reads - 1)
                self.file = self.file[:-20]
            return f"UI hierchary dumped to: {DUMP_PATH}"
        elif command == "wm size":
            return "Physical size: 1080x2400"
        elif command.startswith("input swipe"):
            self.swipes += 1
        elif command == "input keyevent KEYCODE_BACK":
            self.backs += 1
            self.sheet = self.sheet and self.backs < self.backs_to_close
        elif command == f"am force-stop {SHEET_PACKAGE}":
            self.stopped += 1
            self.sheet = self.sheet and not self.stoppable
        elif command.startswith("input tap") and self.sheet:
            self.taps += 1
            if self.late_reads:
                self.closing = self.late_reads
            else:
                self.sheet, self.front = False, self.chosen
        elif command == "dumpsys activity activities":
            return f"  topResumedActivity=ActivityRecord{{1 u0 {self.front}/.Main t1}}"
        elif command.startswith("cmd package resolve-activity"):
            return f"priority=0\n{self.PLAYER}/.Main"
        elif command.startswith("am start -n") and not self.front_sticks:
            self.front = self.PLAYER
        return ""

    def run(self, *args, check=True, timeout=30):
        self.take(" ".join(args), timeout)
        return self.file or ""


def scripted(labels, **options):
    """A ScriptedDevice and a Driver on it, as after a HELLO."""
    device = ScriptedDevice(labels, **options)
    driver = Driver(device, clock=device.now, sleep=device.sleep)
    driver.run_id, driver.package, driver.user = "run", ScriptedDevice.PLAYER, "0"
    driver.warn = lambda text: None
    return device, driver


def answer(driver, name, *args):
    """Runs one request as the logcat loop does and returns the answer written for the test."""
    driver.command(1, name, list(args))
    return driver.device.answer


__test__ = {"share sheet commands": """
The share sheet commands' promises (design 3.3, 3.4), on a ScriptedDevice.

A tap that closes the sheet and brings the player to the front. Nothing read is kept:

>>> device, driver = scripted(["Save", "Copy"])
>>> answer(driver, "tap-in-sheet", encode("Copy"))
'ok'
>>> device.sheet, device.front == device.PLAYER, device.file, driver.last_dump
(False, True, None, None)

A label that is not there: the answer is a fixed word, and the sheet is closed:

>>> device, driver = scripted(["Save"])
>>> answer(driver, "present-in-sheet", encode("Alice"))
'fail not-found'
>>> device.sheet, device.file, driver.last_dump
(False, None, None)

absent-in-sheet passes only when its anchor shows: an empty or unfinished sheet does not pass:

>>> answer(scripted(["Save"])[1], "absent-in-sheet", encode("Drop"), encode("Keep"))
'fail anchor-not-found'
>>> answer(scripted(["Keep", "Drop"])[1], "absent-in-sheet", encode("Drop"), encode("Keep"))
'fail present'
>>> device, driver = scripted(["Keep"])
>>> answer(driver, "absent-in-sheet", encode("Drop"), encode("Keep"))
'ok'
>>> device.swipes, device.sheet
(8, False)

A sheet swiped open that takes two Backs to close, and one that Back does not close at all:

>>> device, driver = scripted(["Save"], backs_to_close=2)
>>> answer(driver, "close-sheet"), device.backs, device.stopped
('ok', 2, 0)
>>> device, driver = scripted(["Save"], backs_to_close=99)
>>> answer(driver, "close-sheet"), device.backs, device.stopped
('ok', 4, 1)

A sheet nothing closes is reported, and closing it is not tried twice:

>>> device, driver = scripted(["Save"], backs_to_close=99, stoppable=False)
>>> answer(driver, "present-in-sheet", encode("Save")), device.stopped
('fail did-not-close', 1)

A tap that hands the share to another app brings the player back and fails:

>>> device, driver = scripted(["Save"], chosen="com.other")
>>> answer(driver, "tap-in-sheet", encode("Save")), device.front == device.PLAYER
('fail wrong-app', True)

What adb says never reaches the answer, and a sheet left by any failure is closed:

>>> device, driver = scripted(["Save"], failing="input swipe")
>>> answer(driver, "absent-in-sheet", encode("Drop"), encode("Save")), device.sheet
('fail error', False)
>>> device, driver = scripted(["Alice"])
>>> answer(driver, "expect", "package=" + encode(SHEET_PACKAGE), "text", encode("Bob"))
'fail mismatch'

A read of the sheet is not kept, on the host or on the device, even before the command ends; a
read of another screen is kept for finding out what was in the way of a wait:

>>> device, driver = scripted(["Alice"])
>>> _ = driver.dump()
>>> device.file, driver.last_dump
(None, None)
>>> device, driver = scripted([], sheet=False)
>>> _ = driver.dump()
>>> device.file == driver.last_dump == device.screen()
True

A screen that cannot be read is not kept either:

>>> device, driver = scripted(["Alice"], bad_reads=1)
>>> try:
...     driver.dump()
... except Failure as error:
...     print(error)
bad-dump
>>> device.file, driver.last_dump
(None, None)

no-sheet: ok while no sheet shows; a sheet that opens is closed and fails the request:

>>> answer(scripted([], sheet=False)[1], "no-sheet", "3000")
'ok'
>>> device, driver = scripted(["Save"])
>>> answer(driver, "no-sheet", "3000"), device.sheet
('fail sheet-opened', False)

A bad argument fails like anything else: the sheet the test opened with the request is closed,
and the answer is a fixed word:

>>> device, driver = scripted(["Keep"])
>>> answer(driver, "absent-in-sheet", encode("Drop")), device.sheet
('fail error', False)
>>> device, driver = scripted(["Keep"])
>>> answer(driver, "tap-in-sheet", "QQ"), device.sheet
('fail error', False)
>>> device, driver = scripted(["Keep"])
>>> answer(driver, "no-sheet", "500"), device.sheet
('fail bad-span', False)

So does an unexpected exception, in the command or while closing:

>>> device, driver = scripted(["Save"], broken="input swipe")
>>> answer(driver, "absent-in-sheet", encode("Drop"), encode("Save")), device.sheet
('fail error', False)
>>> device, driver = scripted(["Save"], broken="input keyevent")
>>> answer(driver, "present-in-sheet", encode("Alice")), device.sheet, device.stopped
('fail not-found', False, 1)
>>> device, driver = scripted(["Save"], chosen="com.other", broken="dumpsys")
>>> answer(driver, "tap-in-sheet", encode("Save"))
'fail not-in-front'

A bad argument found before the sheet has come: the sheet the test opened in the same frame is
waited for, then closed:

>>> device, driver = scripted(["Keep"], open_at=2.0)
>>> answer(driver, "absent-in-sheet", encode("Drop")), device.sheet
('fail error', False)
>>> device, driver = scripted(["Keep"], open_at=2.0)
>>> answer(driver, "no-sheet", "500"), device.sheet
('fail bad-span', False)

no-sheet watches for the whole time: a sheet that comes late is still caught and closed:

>>> device, driver = scripted(["Save"], open_at=2.5)
>>> answer(driver, "no-sheet", "5000"), device.sheet
('fail sheet-opened', False)

A read that fails once is read again; reads that keep failing never make no-sheet pass:

>>> answer(scripted(["Save"], bad_reads=1)[1], "present-in-sheet", encode("Save"))
'ok'
>>> answer(scripted(["Keep"], bad_at=[2, 3])[1], "absent-in-sheet", encode("Drop"), encode("Keep"))
'ok'
>>> answer(scripted(["Save"], bad_reads=3)[1], "no-sheet", "3000") == "ok"
False
>>> answer(scripted([], sheet=False, bad_reads=99)[1], "no-sheet", "3000") == "ok"
False

The other commands finish within COMMAND_TIMEOUT of being taken (10 seconds), the adb calls after
the read included, and a wait that times out keeps the last screen it could read:

>>> COMMAND_TIMEOUT
10.0
>>> for name, args, slow in (("tap", ["text=" + encode("player")], {"input tap": 40}),
...                          ("count", ["text=" + encode("player")], {"exec-out": 40}),
...                          ("rotation", [], {"exec-out": 40}),
...                          ("background", ["1000"], {"dumpsys": 40})):
...     device, driver = scripted([], sheet=False, slow=slow)
...     print(name, answer(driver, name, *args).split()[0], device.time <= COMMAND_TIMEOUT + DELETE_TIMEOUT + 1)
tap fail True
count fail True
rotation fail True
background fail True
>>> import tempfile
>>> with tempfile.TemporaryDirectory() as folder:
...     device, driver = scripted([], sheet=False, seconds=0.7)
...     driver.dump_dir = folder
...     print(answer(driver, "wait", "text=" + encode("Nothing")).split()[0], len(os.listdir(folder)))
fail 1

A read called past its deadline starts nothing, so deletes nothing:

>>> device, driver = scripted([], sheet=False)
>>> try:
...     driver.dump(device.time)
... except Failure as error:
...     print(error, device.calls)
timeout []

The watch for a sheet not yet seen gives up within UNSEEN_WATCH, even when a read would take longer:

>>> device, driver = scripted([], sheet=False, slow={"exec-out": 40})
>>> driver.watch_for_sheet()
>>> device.time <= UNSEEN_WATCH + DELETE_TIMEOUT + 1
True

A read that fails in any way deletes the copy on the device at once:

>>> device, driver = scripted(["Alice"], failing="exec-out")
>>> try:
...     driver.dump()
... except Failure:
...     pass
>>> device.file is None
True

The waits of 10 seconds (for the sheet, for a label in it, for this app in front, and the other
commands' waits) read within those 10 seconds, not within the command's 60, even when one read
would take longer:

>>> def within_ten(call):
...     try:
...         call()
...     except Failure as error:
...         print(error, end=" ")
...     return device.time <= COMMAND_TIMEOUT + DELETE_TIMEOUT + 2
>>> device, driver = scripted([], sheet=False, slow={"exec-out": 40})
>>> within_ten(lambda: driver.wait_sheet(100.0))
sheet-did-not-open True
>>> device, driver = scripted(["Save"], bad_at=range(2, 99))
>>> within_ten(lambda: driver.wait_in_sheet("Alice", 100.0, "not-found"))
not-found True
>>> device, driver = scripted([], sheet=False, front="com.other", slow={"dumpsys": 20})
>>> within_ten(lambda: driver.check_front(100.0)), device.time <= COMMAND_TIMEOUT + 0.5
wrong-app (True, True)
>>> device, driver = scripted([], sheet=False, flaky=["dumpsys"])
>>> driver.check_front(100.0) is None
True
>>> device, driver = scripted([], sheet=False, slow_once={"dumpsys": 20})
>>> driver.check_front(100.0) is None, device.time <= FRONT_READ_TIMEOUT + 2
(True, True)
>>> device, driver = scripted([], sheet=False, slow={"exec-out": 40})
>>> within_ten(lambda: driver.do_wait("text=" + encode("Nothing")))
timeout waiting for text=Tm90aGluZw== True

no-sheet reads again for at most COMMAND_TIMEOUT past its time, then gives up:

>>> device, driver = scripted([], sheet=False, bad_reads=999)
>>> started = device.time
>>> _ = answer(driver, "no-sheet", "3000")
>>> device.time - started <= 3 + COMMAND_TIMEOUT + SHEET_CLOSE_TIMEOUT + SHEET_STOP_TIMEOUT + FRONT_TIMEOUT
True

The watch for a sheet not yet seen lasts UNSEEN_WATCH, and starts afresh with each command:

>>> device, driver = scripted(["Keep"], open_at=8.0)
>>> answer(driver, "absent-in-sheet", encode("Drop")), device.sheet
('fail error', False)
>>> device, driver = scripted(["Keep"])
>>> answer(driver, "close-sheet")
'ok'
>>> device.open_at = device.time + 2
>>> answer(driver, "absent-in-sheet", encode("Drop")), device.sheet
('fail error', False)

background's return trip brings the player back within FRONT_TIMEOUT, and reports one that does not
come:

>>> device, driver = scripted([], sheet=False, seconds=1000, front="com.other")
>>> driver.bring_back(3000)
>>> device.time <= 3 + FRONT_TIMEOUT, driver.exit_code
(True, 1)
>>> device, driver = scripted([], sheet=False, front="com.launcher", front_sticks=True)
>>> driver.bring_back(3000)
>>> driver.exit_code
1
>>> device, driver = scripted([], sheet=False, front="com.launcher")
>>> driver.bring_back(3000)
>>> device.front == device.PLAYER, driver.exit_code
(True, 0)

A tap whose sheet goes only after the wait for it: the next look finds it gone, and it is not
tapped again:

>>> device, driver = scripted(["Save"], late_reads=4)
>>> answer(driver, "tap-in-sheet", encode("Save")), device.taps
('ok', 1)

Another app in front with the command's time running out before the check ends: the answer is a
failure, and the player is still brought back. One that does not come back is reported:

>>> device, driver = scripted(["Save"], chosen="com.other", seconds=5)
>>> answer(driver, "tap-in-sheet", encode("Save")) != "ok", device.front == device.PLAYER
(True, True)
>>> device, driver = scripted(["Save"], chosen="com.other", front_sticks=True)
>>> answer(driver, "tap-in-sheet", encode("Save"))
'fail not-in-front'

A sheet nothing closes still has the player brought back to the front:

>>> device, driver = scripted(["Save"], backs_to_close=99, stoppable=False, front="com.other")
>>> answer(driver, "present-in-sheet", encode("Alice")), device.front == device.PLAYER
('fail did-not-close', True)


A screen read that times out is deleted from the device, in any command; one that cannot be
deleted is remembered, for the driver's exit status:

>>> device, driver = scripted([], sheet=False, slow={"exec-out": 40})
>>> try:
...     driver.dump()
... except Failure as error:
...     print(error)
timeout
>>> device.file is None, driver.dump_left
(True, False)
>>> device, driver = scripted(["Save"], stuck_rm=True)
>>> _ = answer(driver, "close-sheet")
>>> driver.dump_left
True

The limits (design 3.3). The work has SHEET_TIMEOUT; after a failure, watching for a sheet not yet
seen, closing it (Back, then stopping its process) and bringing the player back each have their own
limit; deletes after reads that time out take DELETE_TIMEOUT each, and writing the answer
RESULT_TIMEOUT. AndroidUiDriver.SheetAnswerTimeoutSeconds (180 s) waits longer than the sum:

>>> SHEET_TIMEOUT, UNSEEN_WATCH, SHEET_CLOSE_TIMEOUT, SHEET_STOP_TIMEOUT, FRONT_TIMEOUT, DELETE_TIMEOUT, RESULT_TIMEOUT
(60.0, 10.0, 15.0, 10.0, 25.0, 5.0, 10.0)
>>> SHEET_ANSWER_LIMIT
151.0

On a device whose every adb call times out, each command gives up within that. No answer can be
written there, which the driver's exit status reports:

>>> for request in (["close-sheet"], ["tap-in-sheet", encode("Save")], ["no-sheet", "3000"],
...                 ["absent-in-sheet", encode("Drop")]):
...     device, driver = scripted(["Save"], seconds=1000, backs_to_close=99, stoppable=False, chosen="com.other")
...     print(request[0], answer(driver, *request), device.time <= SHEET_ANSWER_LIMIT, driver.exit_code)
close-sheet None True 1
tap-in-sheet None True 1
no-sheet None True 1
absent-in-sheet None True 1

Each step after the work gives up at its own limit, not later:

>>> device, driver = scripted([], sheet=False, seconds=1000, front="com.other")
>>> driver.bring_front(), device.time == FRONT_TIMEOUT
(False, True)
>>> device, driver = scripted([], sheet=False, seconds=1000)
>>> try:
...     driver.write_result("1", "ok")
... except subprocess.TimeoutExpired:
...     print(device.time == RESULT_TIMEOUT)
True

On a device so slow that every adb call takes 7 seconds, the answers come within it too:

>>> device, driver = scripted(["Keep"], seconds=7)
>>> answer(driver, "absent-in-sheet", encode("Drop"), encode("Keep")) != "ok", device.time <= SHEET_ANSWER_LIMIT
(True, True)
>>> device, driver = scripted(["Save"], seconds=7, chosen="com.other")
>>> answer(driver, "tap-in-sheet", encode("Save")) != "ok", device.time <= SHEET_ANSWER_LIMIT
(True, True)
"""}


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--serial")
    parser.add_argument("--state-file", help="where the replaced settings are kept until restored")
    parser.add_argument("--dump-dir", help="where the screen is kept when a wait times out")
    options = parser.parse_args()

    device = Device(options.serial)
    driver = Driver(device, options.state_file, options.dump_dir)

    def stop(signum, _frame):
        raise KeyboardInterrupt(signum)

    for signum in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
        signal.signal(signum, stop)

    logcat = None
    try:
        driver.restore_previous()
        driver.prepare()
        device.run("logcat", "-c", check=False)
        since = device.shell("date '+%m-%d %H:%M:%S.000'").strip()
        logcat = subprocess.Popen(device.prefix + ["logcat", "-v", "raw", "-s", "Unity:I", "-T", since],
                                  stdout=subprocess.PIPE, text=True, errors="replace")
        print("ui-driver: ready", flush=True)
        for line in logcat.stdout:
            request = parse_request(line)
            if request is None:
                continue
            if request[0] == "hello":
                driver.hello(*request[1:])
            elif request[1] == driver.run_id:
                _, _, number, name, args = request
                driver.command(number, name, args)
    except KeyboardInterrupt:
        pass
    finally:
        # The restore must run to the end: a signal now (the script stopping us while adb is
        # restarting) must not cut it short. Unity kills the adb server when its test run ends,
        # which ends logcat and brings us here before the script's SIGTERM arrives.
        for signum in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
            signal.signal(signum, signal.SIG_IGN)
        if logcat is not None:
            logcat.terminate()
        if driver.dump_left:
            # An earlier delete failed, so the copy may hold the share sheet: one more try, with
            # room for adb to come back (Unity stops the adb server at the end of its run).
            try:
                device.run("wait-for-device", timeout=30)
            except Exception:  # noqa: BLE001
                pass
            driver.forget_dump()
        if driver.dump_left:
            print(f"ui-driver: could not delete {DUMP_PATH} on the device; it may hold the share sheet",
                  file=sys.stderr)
            driver.exit_code = max(driver.exit_code, 1)
        driver.restore()
        print(f"ui-driver: hello={driver.hellos} ready={driver.readies} commands={driver.commands} "
              f"exit={driver.exit_code}", flush=True)
    sys.exit(driver.exit_code)


if __name__ == "__main__":
    main()
