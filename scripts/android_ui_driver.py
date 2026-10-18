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
SHADE_PACKAGE = "com.android.systemui"
PERMISSION_PACKAGES = ("com.google.android.permissioncontroller", "com.android.permissioncontroller")
SETTINGS_PACKAGE = "com.android.settings"
ROW_ID = "com.android.systemui:id/expandableNotificationRow"
SHADE_CLOSE_TIMEOUT = 10.0
DISMISS_TRIES = 3
DISMISS_WAIT = 3.0
SCREEN_BACK_TRIES = 4
# The longest a screen command takes to answer, on a device whose every adb call times out (design
# 3.5; pinned by the doctests below): its own limit, watching for a dialog or screen not yet seen,
# collapsing the shade and checking, Back, stopping the permission controller, bringing this app back
# (once), six deletes past their deadline, a second of waits, and writing the answer.
# AndroidUiDriver.ScreenAnswerTimeoutSeconds waits longer.
SCREEN_ANSWER_LIMIT = (SHEET_TIMEOUT + UNSEEN_WATCH + SHADE_CLOSE_TIMEOUT + SHEET_CLOSE_TIMEOUT + SHEET_STOP_TIMEOUT
                       + FRONT_TIMEOUT + 6 * DELETE_TIMEOUT + 1 + RESULT_TIMEOUT)
# The commands that run inside screen_command, arguments and all. AndroidUiDriver.cs keeps the same
# list (s_screenCommands) to wait longer for them: change both together.
SCREEN_COMMANDS = frozenset({"notification-present", "notification-tap", "notification-action",
                             "notification-dismiss", "permission-answer", "close-screen"})
# The screen commands that watch for what the test opened when they fail before seeing it.
WATCHED_SCREENS = frozenset({"permission-answer", "close-screen"})
# The only reasons a screen command answers with (design 3.5); anything else becomes "error".
SCREEN_REASONS = SHEET_REASONS | {"shade-did-not-open", "dialog-did-not-open", "screen-did-not-open",
                                  "still-present", "bad-arg", "not-stopped", "ambiguous"}
READY_TIMEOUT = 5.0
# Each screen read gets a name of its own (design 3.6): a read the host gave up on may still be
# written on the device after its delete, and must not pass for a later one.
DUMP_PREFIX = "/data/local/tmp/ntk-ui-dump-"
DUMP_GLOB = DUMP_PREFIX + "*.xml"
RUN_ID = re.compile(r"^[0-9a-f]{32}$")
USER_ID = re.compile(r"^[0-9]{1,5}$")
PERMISSION = re.compile(r"^android\.permission\.[A-Z_]{1,64}$")
APP_OP = re.compile(r"^[A-Z_]{1,64}$")
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


def shows_package(root, packages):
    """Whether a dumped screen has a node of one of packages.

    >>> shows_package(ET.fromstring('<hierarchy><node package="com.android.systemui"/></hierarchy>'), (SHADE_PACKAGE,))
    True
    """
    return any(n.get("package") in packages for n in root.iter("node"))


def private_screen(root):
    """Whether a dumped screen must never be kept: it shows the share sheet (other apps' share
    targets, the user's contacts) or anything of the system UI, whose notification shade lists
    other apps' notifications (design 3.4, 3.6). Judged by package, not by the shade's own ids.

    >>> private_screen(ET.fromstring('<hierarchy><node package="com.android.systemui"/></hierarchy>'))
    True
    >>> private_screen(ET.fromstring('<hierarchy><node package="com.android.intentresolver"/></hierarchy>'))
    True
    >>> private_screen(ET.fromstring('<hierarchy><node package="com.example"/></hierarchy>'))
    False
    """
    return shows_package(root, (SHEET_PACKAGE, SHADE_PACKAGE))


def shade_title(root, title):
    """The shade's title node of the notification titled title, or None."""
    return find(root, {"package": SHADE_PACKAGE, "resource-id": "android:id/title", "text": title}, 0)


def labelled(root, label):
    """The system UI nodes whose text or content description is label."""
    return [n for n in root.iter("node")
            if n.get("package") == SHADE_PACKAGE and label in (n.get("text"), n.get("content-desc"))]


def notification_row(root, node):
    """The notification row that holds node (its nearest expandableNotificationRow ancestor), or None.

    >>> root = ET.fromstring('<hierarchy><node resource-id="com.android.systemui:id/expandableNotificationRow" '
    ...                      'bounds="[0,0][9,9]"><node><node text="T"/></node></node></hierarchy>')
    >>> notification_row(root, root[0][0][0]).get("resource-id") == ROW_ID
    True
    >>> notification_row(root, root) is None
    True
    """
    parents = {child: parent for parent in root.iter() for child in parent}
    while node is not None:
        if node.get("resource-id") == ROW_ID:
            return node
        node = parents.get(node)
    return None


def stopped_in(text, package):
    """Whether every activity record of package in `dumpsys activity activities` reports STOPPED.

    >>> stopped_in('''  * Hist  #0: ActivityRecord{1 u0 com.example/.Main t1}
    ...       state=STOPPED''', "com.example")
    True
    >>> stopped_in('''  * Hist  #0: ActivityRecord{1 u0 com.example/.Main t1}
    ...       state=PAUSED''', "com.example")
    False
    """
    states = []
    current = None
    for line in text.splitlines():
        if "ActivityRecord{" in line:
            current = package in line
        elif current:
            match = re.search(r"\bstate=([A-Z_]+)", line)
            if match:
                states.append(match.group(1))
                current = False
    return bool(states) and all(state == "STOPPED" for state in states)


def resumed_activity(text):
    """The resumed activity in `dumpsys activity activities` as (component, action), the action
    being its record's intent action or "-" when it shows none; None when no activity is resumed.
    Android 16 writes the record's intent as "Intent { act=... }" (Pixel 6a, 2026-10-12). A screen
    Settings forwards to its SPA activity has none: Settings starts that record itself, without one.

    >>> resumed_activity('''    topResumedActivity=ActivityRecord{108471437 u0 com.android.settings/.Settings$AppNotificationSettingsActivity t490}
    ...     * Hist  #1: ActivityRecord{108471437 u0 com.android.settings/.Settings$AppNotificationSettingsActivity t490}
    ...       packageName=com.android.settings processName=com.android.settings
    ...       Intent { act=android.settings.APP_NOTIFICATION_SETTINGS flg=0x10000000 cmp=com.android.settings/.Settings$AppNotificationSettingsActivity (has extras) }
    ...     * Hist  #0: ActivityRecord{1b u0 com.example/.Main t8}
    ...       Intent { act=android.intent.action.MAIN cmp=com.example/.Main }''')
    ('com.android.settings/.Settings$AppNotificationSettingsActivity', 'android.settings.APP_NOTIFICATION_SETTINGS')
    >>> resumed_activity('''    topResumedActivity=ActivityRecord{240219641 u0 com.android.settings/.spa.SpaActivity t490}
    ...     * Hist  #3: ActivityRecord{240219641 u0 com.android.settings/.spa.SpaActivity t490}
    ...       Intent { xflg=0x4 cmp=com.android.settings/.spa.SpaActivity (has extras) }''')
    ('com.android.settings/.spa.SpaActivity', '-')
    >>> resumed_activity('''  topResumedActivity=ActivityRecord{3a u0 com.example/.Main t9}
    ...     * Hist  #0: ActivityRecord{3a u0 com.example/.Main t9}
    ...       intent={act=android.intent.action.MAIN}''')
    ('com.example/.Main', 'android.intent.action.MAIN')
    >>> resumed_activity("  topResumedActivity=ActivityRecord{3a u0 com.android.settings/.Settings$AppDetails t9}")
    ('com.android.settings/.Settings$AppDetails', '-')
    >>> resumed_activity("nothing") is None
    True
    """
    match = (re.search(r"topResumedActivity=ActivityRecord\{(\S+) u\d+ (\S+)", text)
             or re.search(r"ResumedActivity: ActivityRecord\{(\S+) u\d+ (\S+)", text))
    if not match:
        return None
    record, component = match.groups()
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if f"ActivityRecord{{{record} " in line and "Resumed" not in line:
            for following in lines[i + 1:]:
                if "ActivityRecord{" in following:
                    break
                action = re.search(r"(?:intent=\{|Intent \{ )act=([^\s}]+)", following)
                if action:
                    return component, action.group(1)
            break
    return component, "-"


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


def sheet_reason(error, words=SHEET_REASONS):
    """The fixed word a share sheet command (or, with SCREEN_REASONS, a screen command) answers with
    for error (design 3.4, 3.5).

    >>> sheet_reason(Failure("not-found"))
    'not-found'
    >>> sheet_reason(Failure("adb shell input tap 1 2 exited 1: Alice"))
    'error'
    >>> sheet_reason(subprocess.TimeoutExpired("adb", 5))
    'timeout'
    >>> sheet_reason(ValueError("Alice"))
    'error'
    >>> sheet_reason(Failure("ambiguous")), sheet_reason(Failure("ambiguous"), SCREEN_REASONS)
    ('error', 'ambiguous')
    """
    if isinstance(error, subprocess.TimeoutExpired):
        return "timeout"
    if isinstance(error, Failure) and str(error) in words:
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
    def __init__(self, device, state_file=None, dump_dir=None, clock=time.monotonic, sleep=time.sleep, package=None):
        self.device = device
        self.now = clock
        self.sleep = sleep
        self.warn = lambda text: print(f"ui-driver: {text}", file=sys.stderr)
        self.expected_package = package  # --package: a HELLO for any other package is ignored
        self.dumps = 0  # screen reads started; each gets its own file on the device
        self.dump_left = False  # a screen read may still be on the device: deleting it failed
        self.seen_sheet = False  # the current share sheet command has read a screen with the sheet
        self.seen_screen = False  # the current screen command has seen the dialog or screen it is for
        self.front_failed = False  # the current screen command already tried to bring this app back
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
        return self.device.shell(f"run-as {shlex.quote(self.package)} --user {shlex.quote(self.user)} sh -c {quoted}",
                                 timeout=timeout)

    def write_result(self, name, text):
        directory = f"files/ntk-ui/{self.run_id}"
        self.as_app(f"echo {shlex.quote(text)} > {directory}/.{name}.tmp && mv {directory}/.{name}.tmp {directory}/{name}",
                    timeout=RESULT_TIMEOUT)

    # -- requests ----------------------------------------------------------------------------

    def hello(self, run_id, package, user):
        # Any app on the device can write a Unity-tagged logcat line, and these values go into
        # shell command lines: a HELLO that is not the test player's, in the form it sends, is
        # dropped (design 3.6). Its values are not printed either.
        if not (RUN_ID.match(run_id) and package == self.expected_package and USER_ID.match(user)):
            self.warn("ignored a HELLO that is not the test player's")
            return
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
            elif name in SCREEN_COMMANDS:
                value = self.screen_command(name, lambda deadline: handler(deadline, *args))
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
        Each read is written to a file of its own on the device and deleted right after, whether it
        could be read or not (design 3.4, 3.6). A screen that shows the share sheet or the system UI,
        or one that cannot be read and so might, is never kept: they list other apps' share targets,
        the user's contacts, and other apps' notifications."""
        self.last_dump = None
        first = self.limit(deadline, 15)  # past the deadline nothing starts, so nothing to delete
        self.dumps += 1
        path = f"{DUMP_PREFIX}{self.dumps}.xml"
        try:
            out = self.device.shell(f"uiautomator dump {path}", check=False, timeout=first)
            if "dumped to" not in out:
                raise Failure("uiautomator dump failed")
            text = self.device.run("exec-out", "cat", path, timeout=self.limit(deadline, 30))
            root = ET.fromstring(text)
        except Exception as error:  # noqa: BLE001 - whatever stopped the read, the copy may be private
            self.forget_dump()
            if isinstance(error, subprocess.TimeoutExpired):
                raise Failure("timeout") from None
            if isinstance(error, ET.ParseError):
                raise Failure("bad-dump") from None
            raise
        self.forget_dump()
        if shows_sheet(root):
            self.seen_sheet = True
        if not private_screen(root):
            self.last_dump = text
        return root

    def forget_dump(self):
        """Deletes the screen reads on the device. A failure is remembered: the command's end and the
        driver's own end delete them again, and the driver's exit status reports reads it could not
        delete (design 3.4)."""
        try:
            self.device.shell(f"rm -f {DUMP_GLOB}", timeout=DELETE_TIMEOUT)
            self.dump_left = False
        except Exception:  # noqa: BLE001
            self.dump_left = True

    def remove_all_dumps(self):
        """At the driver's end: deletes every screen read on the device, whether or not a delete
        failed before (a read the host gave up on may have been written after its delete, V-17).
        Nothing when no screen was read, so a run that read none is not failed by an adb server Unity
        has stopped. Waits for adb first: Unity stops its server at the end of its run. True when
        they are gone (design 3.4, 3.6)."""
        if self.dumps == 0:
            return True
        try:
            self.device.run("wait-for-device", timeout=30)
        except Exception:  # noqa: BLE001
            pass
        self.forget_dump()
        return not self.dump_left

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
        return stopped_in(self.shell("dumpsys activity activities"), self.package)

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

    def do_grant(self, permission):
        """pm grant: does not stop the app (design 3.1). A revoke would, so there is none."""
        if not PERMISSION.match(permission):
            raise Failure("bad-arg")
        self.shell(f"pm grant --user {shlex.quote(self.user)} {shlex.quote(self.package)} {permission}")

    def do_appop_allow(self, operation):
        """appops set ... allow: does not stop the app (design 3.1). deny would, so there is none."""
        if not APP_OP.match(operation):
            raise Failure("bad-arg")
        self.shell(f"appops set --user {shlex.quote(self.user)} {shlex.quote(self.package)} {operation} allow")

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
            f"cmd package resolve-activity --brief -c android.intent.category.LAUNCHER {shlex.quote(self.package)}",
            timeout=self.limit(deadline, 30)).split()
        component = resolved[-1] if resolved else ""
        if "/" not in component:
            raise Failure("no launcher activity")
        self.device.shell(f"am start -n {shlex.quote(component)}", timeout=self.limit(deadline, 30))

    # -- the notification shade, the permission dialog, settings screens (design 3.5, 3.6) --------
    #
    # Like the share sheet commands: each has SHEET_TIMEOUT for its work and, when it fails, closes
    # what it opened or the test opened, brings this app back, and answers with a fixed word, within
    # SCREEN_ANSWER_LIMIT. No screen of the system UI is ever kept.

    def screen_command(self, name, work):
        """Runs work(deadline) as a screen command. Whatever it raises: watches for the dialog or
        screen the test opened if it was not seen yet, collapses the shade (always), closes a
        permission dialog or settings screen in front, brings this app back (unless the command
        already tried and failed), and answers with a fixed word."""
        deadline = self.now() + SHEET_TIMEOUT
        self.seen_screen = False
        self.front_failed = False
        try:
            return work(deadline)
        except Exception as error:  # noqa: BLE001 - every failure must close what is open
            word = sheet_reason(error, SCREEN_REASONS)
            if word == "error":
                self.warn(f"a screen command stopped on {type(error).__name__}")
            if name in WATCHED_SCREENS and not self.seen_screen and word not in ("dialog-did-not-open", "screen-did-not-open"):
                self.watch_for_screen(name)
            if not self.close_screens() and word not in ("did-not-close", "not-in-front"):
                self.warn(f"{word}, and a screen did not close")
                word = "did-not-close"
            if not self.front_failed and not self.bring_front() and word != "did-not-close":
                self.warn(f"{word}, and the player did not come back to the front")
                word = "not-in-front"
            raise Failure(word) from None
        finally:
            self.last_dump = None
            self.forget_dump()

    def watch_for_screen(self, name):
        """Waits up to UNSEEN_WATCH for the permission dialog (permission-answer) or a settings
        screen (close-screen) the test opened with the request. Never raises."""
        until = self.now() + UNSEEN_WATCH
        while self.now() < until:
            try:
                if name == "permission-answer":
                    if shows_package(self.dump(until), PERMISSION_PACKAGES):
                        return
                elif self.front_package(min(until, self.now() + FRONT_READ_TIMEOUT)) == SETTINGS_PACKAGE:
                    return
            except Exception:  # noqa: BLE001
                pass
            self.sleep(0.5)

    def close_screens(self):
        """After a failure. Collapses the shade, unconditionally: it does no harm, and judging whether
        it is open could be wrong. Presses Back while a permission dialog or settings screen is in
        front, reading before each press so that a Back never reaches the player. Stops the
        permission controller if its dialog stays. Each step has its own limit. True when the shade
        is gone and no permission dialog is left (a settings screen left is bring_front's to cover).
        Never raises."""
        shade_gone = self.collapse_shade(self.now() + SHADE_CLOSE_TIMEOUT)
        deadline = self.now() + SHEET_CLOSE_TIMEOUT
        top = "unknown"
        try:
            for presses in range(SCREEN_BACK_TRIES + 1):
                top = self.front_package(deadline)
                if top not in PERMISSION_PACKAGES + (SETTINGS_PACKAGE,) or presses == SCREEN_BACK_TRIES:
                    break
                self.device.shell("input keyevent KEYCODE_BACK", timeout=self.limit(deadline, 30))
                self.sleep(1)
        except Exception:  # noqa: BLE001 - unknown: go on to stopping the dialog
            top = "unknown"
        dialog_gone = True
        if top in PERMISSION_PACKAGES or top == "unknown":
            dialog_gone = self.stop_permission_dialog()
        return shade_gone and dialog_gone

    def stop_permission_dialog(self):
        """Stops the permission controller, when Back does not close its dialog. True when no
        permission dialog is in front. Never raises."""
        deadline = self.now() + SHEET_STOP_TIMEOUT
        try:
            for package in PERMISSION_PACKAGES:
                self.device.shell(f"am force-stop {package}", timeout=self.limit(deadline, 30))
            while self.front_package(deadline) in PERMISSION_PACKAGES:
                self.sleep(0.5)
            return True
        except Exception:  # noqa: BLE001
            return False

    def collapse_shade(self, until):
        """Collapses the shade and reads until no system UI shows. True when it is gone. Never raises."""
        try:
            self.device.shell("cmd statusbar collapse", timeout=self.limit(until, 30))
            while True:
                try:
                    if not shows_package(self.dump(until), (SHADE_PACKAGE,)):
                        return True
                except Failure:
                    pass
                if self.now() >= until:
                    return False
                self.sleep(0.5)
        except Exception:  # noqa: BLE001
            return False

    def close_shade(self, deadline):
        if not self.collapse_shade(min(deadline, self.now() + SHADE_CLOSE_TIMEOUT)):
            raise Failure("did-not-close")

    def wait_screen(self, deadline, found, missing):
        """Reads the screen for up to COMMAND_TIMEOUT (within the deadline) until found(root) gives
        something; returns (root, what it gave). A read that fails is read again; what found raises
        is not caught."""
        until = min(deadline, self.now() + COMMAND_TIMEOUT)
        while True:
            try:
                root = self.dump(until)
            except Failure:
                root = None
            if root is not None:
                value = found(root)
                if value is not None:
                    return root, value
            if self.now() >= until:
                raise Failure("timeout" if until >= deadline else missing)
            self.sleep(0.5)

    def open_shade(self, deadline):
        self.device.shell("cmd statusbar expand-notifications", timeout=self.limit(deadline, 30))
        return self.wait_screen(deadline, lambda root: True if shows_package(root, (SHADE_PACKAGE,)) else None,
                                "shade-did-not-open")[0]

    def tap_node(self, node, deadline):
        left, top, right, bottom = bounds(node)
        self.device.shell(f"input tap {(left + right) // 2} {(top + bottom) // 2}", timeout=self.limit(deadline, 30))

    def send_home(self, deadline):
        """front's first step: HOME, then wait for this app to stop, so that a tap that does not open
        the app cannot pass for one that does (design 3.5; native-toolkit's NotificationEventTest.cpp)."""
        self.device.shell("input keyevent KEYCODE_HOME", timeout=self.limit(deadline, 30))
        until = min(deadline, self.now() + COMMAND_TIMEOUT)
        while True:
            try:
                text = self.device.shell("dumpsys activity activities",
                                         timeout=self.limit(min(until, self.now() + FRONT_READ_TIMEOUT), 30))
                if stopped_in(text, self.package):
                    return
            except (Failure, subprocess.TimeoutExpired):
                pass
            if self.now() >= until:
                raise Failure("timeout" if until >= deadline else "not-stopped")
            self.sleep(0.2)

    def came_front(self, deadline):
        """Whether this app comes to the front within COMMAND_TIMEOUT (and the deadline). Never raises."""
        until = min(deadline, self.now() + COMMAND_TIMEOUT)
        while True:
            try:
                if self.front_package(min(until, self.now() + FRONT_READ_TIMEOUT)) == self.package:
                    return True
            except (Failure, subprocess.TimeoutExpired):
                pass
            if self.now() >= until:
                return False
            self.sleep(0.5)

    def after_tap(self, deadline, front):
        """stay: collapses the shade; ok. front: "front" when this app came to the front, else
        "not-front" once it is brought back; when it cannot be, not-in-front, and the failure path
        does not try again (that try is the one counted in SCREEN_ANSWER_LIMIT)."""
        if front == "stay":
            self.close_shade(deadline)
            return None
        came = self.came_front(deadline)
        self.close_shade(deadline)
        if came:
            return "front"
        if not self.bring_front():
            self.front_failed = True
            raise Failure("not-in-front")
        return "not-front"

    @staticmethod
    def front_argument(front):
        if front not in ("front", "stay"):
            raise Failure("bad-arg")

    def do_notification_present(self, deadline, title):
        label = decode(title)
        self.open_shade(deadline)
        self.wait_screen(deadline, lambda root: shade_title(root, label), "not-found")
        self.close_shade(deadline)

    def do_notification_tap(self, deadline, title, front):
        label = decode(title)
        self.front_argument(front)
        if front == "front":
            self.send_home(deadline)
        self.open_shade(deadline)
        _, node = self.wait_screen(deadline, lambda root: shade_title(root, label), "not-found")
        self.tap_node(node, deadline)
        return self.after_tap(deadline, front)

    def do_notification_action(self, deadline, title, button, front):
        """The button inside the notification's row; failing that, the row expanded and the button
        looked for in the whole shade, as native-toolkit's NotificationShade.kt does (expanding may
        replace the row's content). Then there must be exactly one: another app's notification may
        have a button of the same text (ambiguous)."""
        label, text = decode(title), decode(button)
        self.front_argument(front)
        if front == "front":
            self.send_home(deadline)
        self.open_shade(deadline)
        root, node = self.wait_screen(deadline, lambda root: shade_title(root, label), "not-found")
        row = notification_row(root, node)
        if row is None:
            raise Failure("not-found")
        inside = [n for n in row.iter("node") if text in (n.get("text"), n.get("content-desc"))]
        if inside:
            target = inside[0]
        else:
            expand = find(row, {"resource-id": "android:id/expand_button"}, 0)
            if expand is None:
                raise Failure("not-found")
            self.tap_node(expand, deadline)

            def pick(root):
                if shade_title(root, label) is None:
                    return None
                matches = labelled(root, text)
                if len(matches) > 1:
                    raise Failure("ambiguous")
                return matches[0] if matches else None

            _, target = self.wait_screen(deadline, pick, "not-found")
        self.tap_node(target, deadline)
        return self.after_tap(deadline, front)

    def do_notification_dismiss(self, deadline, title):
        """A fast swipe across the row, from 1/10 to 9/10 of the width (a slow one may not dismiss;
        native-toolkit's UiDriver.kt), up to DISMISS_TRIES times, the row found again each time."""
        label = decode(title)
        width, _ = self.screen_size(deadline)
        self.open_shade(deadline)
        root, node = self.wait_screen(deadline, lambda root: shade_title(root, label), "not-found")
        for _ in range(DISMISS_TRIES):
            _, top, _, bottom = bounds(notification_row(root, node) or node)
            y = (top + bottom) // 2
            self.device.shell(f"input swipe {width // 10} {y} {width * 9 // 10} {y} 100", timeout=self.limit(deadline, 30))
            seen = self.still_shown(deadline, label)
            if seen is None:
                self.close_shade(deadline)
                return
            root, node = seen
        raise Failure("still-present")

    def still_shown(self, deadline, label):
        """None once a read shows no notification titled label within DISMISS_WAIT; else the last
        screen that showed it and its title node."""
        until = min(deadline, self.now() + DISMISS_WAIT)
        seen = None
        while True:
            try:
                root = self.dump(until)
                node = shade_title(root, label)
                if node is None:
                    return None
                seen = (root, node)
            except Failure:
                pass
            if self.now() >= until:
                if seen is None:
                    raise Failure("timeout")
                return seen
            self.sleep(0.5)

    def do_permission_answer(self, deadline, choice):
        if choice not in ("allow", "deny"):
            raise Failure("bad-arg")
        suffix = f":id/permission_{choice}_button"

        def dialog(root):
            if not shows_package(root, PERMISSION_PACKAGES):
                return None
            self.seen_screen = True
            return True

        def button(root):
            return next((n for n in root.iter("node") if n.get("package") in PERMISSION_PACKAGES
                         and n.get("resource-id", "").endswith(suffix)), None)

        self.wait_screen(deadline, dialog, "dialog-did-not-open")
        _, node = self.wait_screen(deadline, button, "not-found")
        self.tap_node(node, deadline)
        if not self.came_front(deadline):
            raise Failure("still-open")

    def do_close_screen(self, deadline, package):
        """ok "<component> <action>" of the settings screen in front, once two reads 0.5 s apart
        agree (a trampoline activity is not taken for the screen), after closing it: Back, read
        before each press, then `front`; the player's launcher activity is singleTask, so starting it
        clears what was stacked on it (V-25)."""
        if decode(package) != SETTINGS_PACKAGE:
            raise Failure("bad-arg")
        until = min(deadline, self.now() + COMMAND_TIMEOUT)
        previous = None
        while True:
            try:
                current = resumed_activity(self.device.shell(
                    "dumpsys activity activities", timeout=self.limit(min(until, self.now() + FRONT_READ_TIMEOUT), 30)))
            except (Failure, subprocess.TimeoutExpired):
                current = None
            if current is not None and current[0].startswith(SETTINGS_PACKAGE + "/"):
                self.seen_screen = True
                if current == previous:
                    break
                previous = current
            else:
                previous = None
            if self.now() >= until:
                raise Failure("timeout" if until >= deadline else "screen-did-not-open")
            self.sleep(0.5)
        for _ in range(SCREEN_BACK_TRIES):
            if self.front_package(deadline) != SETTINGS_PACKAGE:
                break
            self.device.shell("input keyevent KEYCODE_BACK", timeout=self.limit(deadline, 30))
            self.sleep(1)
        if self.front_package(deadline) == SETTINGS_PACKAGE:
            self.do_front(deadline)
            self.sleep(1)
            if self.front_package(deadline) == SETTINGS_PACKAGE:
                raise Failure("did-not-close")
        if self.front_package(deadline) != self.package:
            self.do_front(deadline)
            if not self.came_front(deadline):
                raise Failure("not-in-front")
        return f"{current[0]} {current[1]}"


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
                 stuck_rm=False, bad_at=(), open_at=None, front_sticks=False, front=PLAYER, flaky=(), slow_once=None,
                 rm_fails=()):
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
        self.rm_fails, self.rms = set(rm_fails), 0  # the deletes (1, 2, ...) that fail
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
        if command.startswith("rm -f"):
            self.rms += 1
            if self.stuck_rm or self.rms in self.rm_fails:
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
        if command == f"rm -f {DUMP_GLOB}":
            self.file = None
        elif command.startswith(f"uiautomator dump {DUMP_PREFIX}"):
            if self.closing is not None:
                self.closing -= 1
                if self.closing < 0:
                    self.sheet, self.front, self.closing = False, self.chosen, None
            self.file = self.screen()
            self.reads += 1
            if self.bad_reads or self.reads in self.bad_at:
                self.bad_reads = max(0, self.bad_reads - 1)
                self.file = self.file[:-20]
            return f"UI hierchary dumped to: {command.split()[-1]}"
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
    return on(ScriptedDevice(labels, **options))


def on(device):
    """A Driver on device, as after a HELLO."""
    driver = Driver(device, clock=device.now, sleep=device.sleep, package=ScriptedDevice.PLAYER)
    driver.run_id, driver.package, driver.user = "run", ScriptedDevice.PLAYER, "0"
    driver.warn = lambda text: None
    return device, driver


class ScreenDevice(ScriptedDevice):
    """A stand-in device for the screen commands' doctests (design 3.5): a notification shade that
    expand-notifications opens and collapse closes, notifications to tap, act on, expand and swipe
    away, a permission dialog and a settings screen that Back closes, and an activity stack whose
    bottom is the player: HOME stops it, and `am start` brings it back to the top, clearing what was
    stacked on it, as its singleTask launcher activity does.

    A notification is a dict: title; buttons (shown in its row); more (shown once expanded, outside
    the row when replaces is set); swipes (to dismiss it, default 1); launches (whether a tap on its
    body opens the app, default True); launching (the buttons that open the app)."""

    LAUNCHER = ("com.example.launcher", "com.example.launcher/.Home", "android.intent.action.MAIN")
    DIALOG = (PERMISSION_PACKAGES[0], f"{PERMISSION_PACKAGES[0]}/.GrantPermissionsActivity",
              "android.content.pm.action.REQUEST_PERMISSIONS")

    def __init__(self, notifications=(), dialog=False, dialog_at=None, settings=None, trampoline=0, shade_opens=True,
                 shade_sticks=False, home_stops=True, dialog_backs=1, dialog_stoppable=True, settings_backs=1,
                 **options):
        super().__init__([], sheet=False, **options)
        self.notifications = [dict(n) for n in notifications]
        self.shade, self.shade_opens, self.shade_sticks = False, shade_opens, shade_sticks
        self.home_stops, self.player_stopped = home_stops, False
        self.dialog_backs, self.dialog_stoppable, self.settings_backs = dialog_backs, dialog_stoppable, settings_backs
        self.expanded = set()
        self.stack = [(self.PLAYER, f"{self.PLAYER}/.Main", "android.intent.action.MAIN")]
        if dialog:
            self.stack.append(self.DIALOG)
        if settings:
            self.stack.append((SETTINGS_PACKAGE,) + tuple(settings))
        self.trampoline = trampoline  # reads of dumpsys that still show a bridging activity
        self.dialog_at = dialog_at  # the time a permission dialog the test opened shows, for one that comes late
        self.events = []  # what the app and the system saw: taps, actions, dismissals, answers
        self.collapses = 0
        self.targets = {}

    def top(self):
        return self.stack[-1][0]

    def screen(self):
        self.targets = {}
        if self.shade:
            nodes = []
            for i, n in enumerate(self.notifications):
                y = 200 + 300 * i
                title = (f'<node package="{SHADE_PACKAGE}" resource-id="android:id/title" text="{n["title"]}" '
                         f'bounds="[40,{y + 20}][800,{y + 80}]"/>')
                self.targets[(40, y + 20, 800, y + 80)] = ("body", n["title"])
                expanded = n["title"] in self.expanded
                shown = list(n.get("buttons", ())) + (list(n.get("more", ())) if expanded else [])
                moved = expanded and n.get("replaces")
                parts = [title]
                if n.get("more") and not expanded:
                    parts.append(f'<node package="{SHADE_PACKAGE}" resource-id="android:id/expand_button" '
                                 f'bounds="[900,{y + 20}][1000,{y + 80}]"/>')
                    self.targets[(900, y + 20, 1000, y + 80)] = ("expand", n["title"])
                loose = []
                for j, label in enumerate(shown):
                    box = (100 + 200 * j, 2000 + 60 * i, 280 + 200 * j, 2050 + 60 * i) if moved else \
                          (100 + 200 * j, y + 200, 280 + 200 * j, y + 260)
                    (loose if moved else parts).append(
                        f'<node package="{SHADE_PACKAGE}" text="{label}" bounds="[{box[0]},{box[1]}][{box[2]},{box[3]}]"/>')
                    self.targets[box] = ("action", n["title"], label)
                nodes.append(f'<node package="{SHADE_PACKAGE}" resource-id="{ROW_ID}" bounds="[0,{y}][1080,{y + 280}]">'
                             + "".join(parts) + "</node>" + "".join(loose))
            return f'<hierarchy><node package="{SHADE_PACKAGE}" bounds="[0,0][1080,2400]">{"".join(nodes)}</node></hierarchy>'
        package = self.top()
        if package in PERMISSION_PACKAGES:
            self.targets[(100, 1500, 500, 1600)] = ("allow",)
            self.targets[(100, 1700, 500, 1800)] = ("deny",)
            return (f'<hierarchy><node package="{package}" bounds="[0,1000][1080,2000]">'
                    f'<node package="{package}" resource-id="com.android.permissioncontroller:id/permission_allow_button" '
                    f'bounds="[100,1500][500,1600]"/>'
                    f'<node package="{package}" resource-id="com.android.permissioncontroller:id/permission_deny_button" '
                    f'bounds="[100,1700][500,1800]"/></node></hierarchy>')
        return f'<hierarchy><node package="{package}" text="screen" bounds="[0,0][1080,2400]"/></hierarchy>'

    def bring_player(self):
        while self.top() != self.PLAYER:
            self.stack.pop()
        self.player_stopped = False

    def tap(self, x, y):
        self.screen()
        hits = [(r - l) * (b - t) for (l, t, r, b) in self.targets if l <= x <= r and t <= y <= b]
        target = next((v for (l, t, r, b), v in self.targets.items()
                       if l <= x <= r and t <= y <= b and (r - l) * (b - t) == min(hits)), None) if hits else None
        if target is None:
            return
        notification = next((n for n in self.notifications if len(target) > 1 and n["title"] == target[1]), None)
        if target[0] == "expand":
            self.expanded.add(target[1])
            return
        self.events.append(target)
        if target[0] in ("allow", "deny"):
            self.stack.pop()
            return
        if target[0] == "body":
            self.shade = False
            self.notifications.remove(notification)
            if notification.get("launches", True):
                self.bring_player()
        elif target[2] in notification.get("launching", ()):
            self.shade = False
            self.bring_player()

    def activities(self):
        lines = [f"  topResumedActivity=ActivityRecord{{{len(self.stack)} u0 {self.stack[-1][1]} t1}}"]
        if self.trampoline and self.top() == SETTINGS_PACKAGE:
            self.trampoline -= 1
            lines = [f"  topResumedActivity=ActivityRecord{{99 u0 {SETTINGS_PACKAGE}/.Trampoline t1}}"]
        for i in range(len(self.stack) - 1, -1, -1):
            package, component, action = self.stack[i]
            if package == self.PLAYER and self.player_stopped:
                state = "STOPPED"
            else:
                state = "RESUMED" if i == len(self.stack) - 1 else "PAUSED"
            lines += [f"    * Hist  #{i}: ActivityRecord{{{i + 1} u0 {component} t1}}",
                      f"        intent={{act={action} cmp={component}}}", f"        state={state}"]
        return "\n".join(lines)

    def shell(self, command, check=True, timeout=30):
        self.take(command, timeout)
        if self.dialog_at is not None and self.time >= self.dialog_at:
            self.stack.append(self.DIALOG)
            self.dialog_at = None
        if command.startswith("run-as"):
            script = shlex.split(command)[-1]
            if script.startswith("echo "):
                self.answer = shlex.split(script)[1]
        elif command.startswith(f"uiautomator dump {DUMP_PREFIX}"):
            self.file = self.screen()
            return f"UI hierchary dumped to: {command.split()[-1]}"
        elif command == f"rm -f {DUMP_GLOB}":
            self.file = None
        elif command == "cmd statusbar expand-notifications":
            self.shade = self.shade_opens
        elif command == "cmd statusbar collapse":
            self.collapses += 1
            self.shade = self.shade and self.shade_sticks
        elif command == "input keyevent KEYCODE_HOME":
            if self.top() != self.LAUNCHER[0]:
                self.stack.append(self.LAUNCHER)
            self.player_stopped = self.home_stops
        elif command == "input keyevent KEYCODE_BACK":
            self.backs += 1
            if self.top() in PERMISSION_PACKAGES:
                self.dialog_backs -= 1
                if self.dialog_backs <= 0:
                    self.stack.pop()
            elif self.top() == SETTINGS_PACKAGE:
                self.settings_backs -= 1
                if self.settings_backs <= 0:
                    self.stack.pop()
            elif self.top() == self.PLAYER and not self.shade:
                self.events.append(("back",))  # a Back that reached the player
        elif command.startswith("am force-stop "):
            self.stopped += 1
            if self.top() == command.split()[-1] and self.dialog_stoppable:
                self.stack.pop()
        elif command.startswith("am start -n"):
            if not self.front_sticks:
                self.bring_player()
        elif command.startswith("input tap"):
            self.taps += 1
            x, y = (int(v) for v in command.split()[2:4])
            self.tap(x, y)
        elif command.startswith("input swipe") and self.shade:
            y = int(command.split()[3])
            for i, n in enumerate(self.notifications):
                if 200 + 300 * i <= y <= 480 + 300 * i:
                    n["swipes"] = n.get("swipes", 1) - 1
                    if n["swipes"] <= 0:
                        self.notifications.remove(n)
                        self.events.append(("dismiss", n["title"]))
                    break
        elif command == "dumpsys activity activities":
            return self.activities()
        elif command.startswith("cmd package resolve-activity"):
            return f"priority=0\n{self.PLAYER}/.Main"
        elif command == "wm size":
            return "Physical size: 1080x2400"
        return ""


def screens(**options):
    """A ScreenDevice and a Driver on it, as after a HELLO."""
    return on(ScreenDevice(**options))


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
>>> device.file, driver.last_dump == device.screen()
(None, True)

Each read is written under a name of its own, and deleted right after (design 3.6):

>>> [c.split()[-1] for c in device.calls if c.startswith("uiautomator")], device.calls[-1]
(['/data/local/tmp/ntk-ui-dump-1.xml'], 'rm -f /data/local/tmp/ntk-ui-dump-*.xml')
>>> _ = driver.dump()
>>> [c.split()[-1] for c in device.calls if c.startswith("uiautomator")]
['/data/local/tmp/ntk-ui-dump-1.xml', '/data/local/tmp/ntk-ui-dump-2.xml']

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

>>> device, driver = scripted(["Save"], late_reads=5)
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

A command deletes the reads once more at its end, for the last read's delete that failed:

>>> device, driver = scripted(["Save"], rm_fails=[3])
>>> answer(driver, "close-sheet"), device.rms, driver.dump_left
('ok', 4, False)
>>> device, driver = screens(notifications=[{"title": "T1"}], rm_fails=[3])
>>> answer(driver, "notification-present", encode("T1")), device.rms, driver.dump_left
('ok', 4, False)

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
""",

"screen commands": """
The screen commands' promises (design 3.5, 3.6), on a ScreenDevice.

notification-present opens the shade, finds the title, and collapses it. Nothing read is kept:

>>> device, driver = screens(notifications=[{"title": "T1"}])
>>> answer(driver, "notification-present", encode("T1")), device.shade, device.file, driver.last_dump
('ok', False, None, None)

A title that is not there fails with a fixed word, and the shade is collapsed:

>>> device, driver = screens(notifications=[{"title": "Other app: Alice"}])
>>> answer(driver, "notification-present", encode("T1")), device.shade
('fail not-found', False)

A shade that does not open:

>>> answer(screens(shade_opens=False)[1], "notification-present", encode("T1"))
'fail shade-did-not-open'

notification-tap stay: the body is tapped, and the shade collapsed:

>>> device, driver = screens(notifications=[{"title": "T1"}])
>>> answer(driver, "notification-tap", encode("T1"), "stay"), device.events, device.shade
('ok', [('body', 'T1')], False)

front: HOME first, and the app must stop before the shade opens; then "front" when the tap opened
the app, "not-front" (the app brought back) when it did not:

>>> device, driver = screens(notifications=[{"title": "T1"}])
>>> answer(driver, "notification-tap", encode("T1"), "front"), device.top() == device.PLAYER
('ok front', True)
>>> device.calls.index("input keyevent KEYCODE_HOME") < device.calls.index("cmd statusbar expand-notifications")
True
>>> device, driver = screens(notifications=[{"title": "T1", "launches": False}])
>>> answer(driver, "notification-tap", encode("T1"), "front"), device.top() == device.PLAYER, device.events
('ok not-front', True, [('body', 'T1')])

An app that does not stop on HOME, a front argument out of range, and a player that cannot be
brought back (tried once, not again on the failure path):

>>> device, driver = screens(notifications=[{"title": "T1"}], home_stops=False)
>>> answer(driver, "notification-tap", encode("T1"), "front"), device.top() == device.PLAYER, device.events
('fail not-stopped', True, [])
>>> device, driver = screens(notifications=[{"title": "T1"}])
>>> answer(driver, "notification-tap", encode("T1"), "later"), device.collapses, device.events
('fail bad-arg', 1, [])
>>> device, driver = screens(notifications=[{"title": "T1", "launches": False}], front_sticks=True)
>>> answer(driver, "notification-tap", encode("T1"), "front"), sum(c.startswith("am start") for c in device.calls)
('fail not-in-front', 1)

notification-action: a button in the row; one that shows once the row is expanded, even when
expanding moves it out of the row; and one that another notification has too (ambiguous):

>>> device, driver = screens(notifications=[{"title": "T1", "buttons": ["Later-1"]}])
>>> answer(driver, "notification-action", encode("T1"), encode("Later-1"), "stay"), device.events, device.shade
('ok', [('action', 'T1', 'Later-1')], False)
>>> device, driver = screens(notifications=[{"title": "T1", "more": ["Play-1"], "replaces": True, "launching": ["Play-1"]}])
>>> answer(driver, "notification-action", encode("T1"), encode("Play-1"), "front"), device.events
('ok front', [('action', 'T1', 'Play-1')])
>>> device, driver = screens(notifications=[{"title": "T1", "more": ["Go"], "replaces": True},
...                                         {"title": "Other", "buttons": ["Go"]}])
>>> answer(driver, "notification-action", encode("T1"), encode("Go"), "stay"), device.events
('fail ambiguous', [])
>>> answer(screens(notifications=[{"title": "T1"}])[1], "notification-action", encode("T1"), encode("Go"), "stay")
'fail not-found'
>>> answer(screens(notifications=[{"title": "T1"}])[1], "notification-action", encode("T1"), encode("Go"), "now")
'fail bad-arg'

notification-dismiss: a fast swipe across the row, from 1/10 to 9/10 of the width, up to three:

>>> device, driver = screens(notifications=[{"title": "T1"}])
>>> answer(driver, "notification-dismiss", encode("T1")), device.events
('ok', [('dismiss', 'T1')])
>>> [c for c in device.calls if c.startswith("input swipe")]
['input swipe 108 340 972 340 100']
>>> device, driver = screens(notifications=[{"title": "T1", "swipes": 2}])
>>> answer(driver, "notification-dismiss", encode("T1")), device.events
('ok', [('dismiss', 'T1')])
>>> device, driver = screens(notifications=[{"title": "T1", "swipes": 99}])
>>> answer(driver, "notification-dismiss", encode("T1")), sum(c.startswith("input swipe") for c in device.calls), device.shade
('fail still-present', 3, False)

permission-answer: the dialog's button, then the app in front; a bad answer still closes the dialog
the test opened (Back, then stopping the permission controller):

>>> device, driver = screens(dialog=True)
>>> answer(driver, "permission-answer", "deny"), device.events, device.top() == device.PLAYER
('ok', [('deny',)], True)
>>> device, driver = screens(dialog=True)
>>> answer(driver, "permission-answer", "maybe"), device.events, device.top() == device.PLAYER
('fail bad-arg', [], True)
>>> device, driver = screens(dialog=True, dialog_backs=99)
>>> answer(driver, "permission-answer", "maybe"), device.backs, device.stopped, device.top() == device.PLAYER
('fail bad-arg', 4, 2, True)
>>> answer(screens()[1], "permission-answer", "allow")
'fail dialog-did-not-open'

A dialog the test opened that comes after a bad argument was found is still waited for and closed:

>>> device, driver = screens(dialog_at=8.0)
>>> answer(driver, "permission-answer", "maybe")
'fail bad-arg'
>>> device.sleep(10)
>>> _ = device.shell("true")
>>> device.top() == device.PLAYER
True

A Back is pressed only on a dialog or settings screen in front, never on the player:

>>> device, driver = screens(notifications=[{"title": "T1"}])
>>> answer(driver, "notification-present", encode("Nothing")), device.backs, device.events
('fail not-found', 0, [])

close-screen: the settings screen's activity and action, read twice alike (a trampoline is not
taken for it), then closed; a name with $ reaches the test whole:

>>> device, driver = screens(settings=("com.android.settings/.spa.SpaActivity", "android.settings.APP_NOTIFICATION_SETTINGS"))
>>> answer(driver, "close-screen", encode(SETTINGS_PACKAGE)), device.top() == device.PLAYER
('ok com.android.settings/.spa.SpaActivity android.settings.APP_NOTIFICATION_SETTINGS', True)
>>> device, driver = screens(settings=("com.android.settings/.Settings$AppDetails", "-"), trampoline=1)
>>> answer(driver, "close-screen", encode(SETTINGS_PACKAGE))
'ok com.android.settings/.Settings$AppDetails -'

Back that does not close it falls back on bringing the player (singleTask) up; a screen that stays
even so, another package, and no screen at all:

>>> device, driver = screens(settings=("com.android.settings/.Settings", "-"), settings_backs=99)
>>> answer(driver, "close-screen", encode(SETTINGS_PACKAGE)).split()[0], device.backs, device.top() == device.PLAYER
('ok', 4, True)
>>> device, driver = screens(settings=("com.android.settings/.Settings", "-"), settings_backs=99, front_sticks=True)
>>> answer(driver, "close-screen", encode(SETTINGS_PACKAGE))
'fail did-not-close'
>>> device, driver = screens(settings=("com.android.settings/.Settings", "-"))
>>> answer(driver, "close-screen", encode("com.example")), device.top() == device.PLAYER
('fail bad-arg', True)
>>> answer(screens()[1], "close-screen", encode(SETTINGS_PACKAGE))
'fail screen-did-not-open'

No read with the system UI is kept, even when a wait for the player's screen times out:

>>> import tempfile
>>> with tempfile.TemporaryDirectory() as folder:
...     device, driver = screens(notifications=[{"title": "T1"}])
...     driver.dump_dir = folder
...     device.shade = True
...     print(answer(driver, "wait", "text=" + encode("Nothing")).split()[0], os.listdir(folder))
fail []

grant and appop-allow take checked arguments only:

>>> device, driver = screens()
>>> answer(driver, "grant", "android.permission.POST_NOTIFICATIONS"), device.calls[0]
('ok', 'pm grant --user 0 com.example.player android.permission.POST_NOTIFICATIONS')
>>> answer(driver, "grant", "android.permission.X;reboot"), answer(driver, "appop-allow", "SCHEDULE_EXACT_ALARM")
('fail bad-arg', 'ok')
>>> answer(driver, "appop-allow", "a b")
'fail bad-arg'

A HELLO that is not the test player's, in the form it sends, is dropped: another app can write the
same logcat line, and the values go into shell commands:

>>> device, driver = screens()
>>> for hello in (("0" * 32, "com.other", "0"), ("x;reboot", device.PLAYER, "0"), ("0" * 32, device.PLAYER, "0 0"),
...               ("0" * 31, device.PLAYER, "0")):
...     driver.hello(*hello)
>>> driver.hellos, device.calls
(0, [])
>>> driver.hello("0" * 32, device.PLAYER, "10")
>>> driver.hellos, driver.run_id == "0" * 32
(1, True)

At the driver's end every read is deleted, whatever was remembered; nothing is done when nothing
was read; one that cannot be deleted is reported:

>>> device, driver = screens()
>>> driver.remove_all_dumps(), device.calls
(True, [])
>>> _ = driver.dump()
>>> driver.dump_left = False
>>> before = len(device.calls)
>>> driver.remove_all_dumps(), device.calls[before:]
(True, ['wait-for-device', 'rm -f /data/local/tmp/ntk-ui-dump-*.xml'])
>>> device, driver = screens(stuck_rm=True)
>>> _ = driver.dump()
>>> driver.remove_all_dumps()
False

The limits (design 3.5): SCREEN_ANSWER_LIMIT, and on a device whose every adb call times out, each
screen command gives up within it (no answer can be written there, which the exit status reports):

>>> SHADE_CLOSE_TIMEOUT, SCREEN_ANSWER_LIMIT
(10.0, 171.0)
>>> for request in (["notification-present", encode("T1")], ["notification-tap", encode("T1"), "front"],
...                 ["notification-action", encode("T1"), encode("Go"), "front"],
...                 ["notification-dismiss", encode("T1")], ["permission-answer", "allow"],
...                 ["close-screen", encode(SETTINGS_PACKAGE)]):
...     device, driver = screens(notifications=[{"title": "T1"}], dialog=True, seconds=1000)
...     print(request[0], answer(driver, *request), device.time <= SCREEN_ANSWER_LIMIT, driver.exit_code)
notification-present None True 1
notification-tap None True 1
notification-action None True 1
notification-dismiss None True 1
permission-answer None True 1
close-screen None True 1

and on one where every call takes 7 seconds:

>>> for request in (["notification-tap", encode("T1"), "front"], ["notification-dismiss", encode("T1")],
...                 ["permission-answer", "maybe"], ["close-screen", encode(SETTINGS_PACKAGE)]):
...     device, driver = screens(notifications=[{"title": "T1", "swipes": 99, "launches": False}], dialog=True,
...                              dialog_backs=99, seconds=7)
...     print(request[0], end=" ")
...     _ = answer(driver, *request)
...     print(device.time <= SCREEN_ANSWER_LIMIT)
notification-tap True
notification-dismiss True
permission-answer True
close-screen True
"""}


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--serial")
    parser.add_argument("--state-file", help="where the replaced settings are kept until restored")
    parser.add_argument("--dump-dir", help="where the screen is kept when a wait times out")
    parser.add_argument("--package", required=True,
                        help="the test player's package: a HELLO for any other is ignored (design 3.6)")
    options = parser.parse_args()

    device = Device(options.serial)
    driver = Driver(device, options.state_file, options.dump_dir, package=options.package)

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
        if not driver.remove_all_dumps():
            print(f"ui-driver: could not delete {DUMP_GLOB} on the device; they may hold the share sheet or "
                  "the notification shade", file=sys.stderr)
            driver.exit_code = max(driver.exit_code, 1)
        driver.restore()
        print(f"ui-driver: hello={driver.hellos} ready={driver.readies} commands={driver.commands} "
              f"exit={driver.exit_code}", flush=True)
    sys.exit(driver.exit_code)


if __name__ == "__main__":
    main()
