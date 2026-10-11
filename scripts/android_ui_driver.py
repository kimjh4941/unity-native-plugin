#!/usr/bin/env python3
"""Presses and reads Android UI for device PlayMode tests, on their request.

The Unity tests run inside an Android player and cannot reach the OS UI (a dialog, the share sheet,
the notification shade). This helper runs on the host while the tests run. It reads requests the
tests write to logcat, carries them out with adb (`uiautomator dump`, `input`, `dumpsys`,
`settings`, `am`), and writes each result into the app's files directory through `run-as`, where the
test waits for it. The protocol and the reasons behind it are in
artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-design-v4.md.

Requests (one logcat line each, tag Unity):

    NTK-UI-HELLO|<run>|<package>|<user>
    NTK-UI|<run>|<number>|<command>|<arg>...

Results: files/ntk-ui/<run>/<number> holds "ok", "ok <value>" or "fail <reason>".

It changes device settings while it runs (animations off, screen kept on, autofill off, orientation
when asked) and restores them when it stops, on SIGTERM, SIGINT and SIGHUP too. The values it
replaced are also written to a state file on the host; if a run stopped before restoring them, the
next start restores them first.

Autofill is turned off because a login dialog's password field makes Google's autofill offer to save
the password; that prompt then takes the focus, and `uiautomator dump`, which reads the focused
window, sees only it for the rest of the run (2026-10-11).

Exit status: 0 fine; 1 a setting could not be restored or `background` could not bring the app back;
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
    def __init__(self, device, state_file=None, dump_dir=None):
        self.device = device
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
        time.sleep(1)
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

    def as_app(self, script):
        quoted = shlex.quote(script)
        return self.device.shell(f"run-as {self.package} --user {self.user} sh -c {quoted}")

    def write_result(self, name, text):
        directory = f"files/ntk-ui/{self.run_id}"
        self.as_app(f"echo {shlex.quote(text)} > {directory}/.{name}.tmp && mv {directory}/.{name}.tmp {directory}/{name}")

    # -- requests ----------------------------------------------------------------------------

    def hello(self, run_id, package, user):
        self.hellos += 1
        self.run_id, self.package, self.user = run_id, package, user
        started = time.monotonic()
        try:
            self.as_app(f"rm -rf files/ntk-ui && mkdir -p files/ntk-ui/{run_id}")
            self.write_result("probe", "probe")
            while self.as_app(f"ls files/ntk-ui/{run_id}").split().count("probe"):
                if time.monotonic() - started > READY_TIMEOUT:
                    raise Failure("the test did not take the probe")
                time.sleep(0.2)
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
            value = handler(*args)
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
            print(f"ui-driver: could not write result {number}: {error}", file=sys.stderr)
        if deferred is not None:
            deferred.after()

    # -- reading the screen ----------------------------------------------------------------------

    def dump(self):
        self.device.shell(f"rm -f {DUMP_PATH}", check=False)
        out = self.device.shell(f"uiautomator dump {DUMP_PATH}", check=False, timeout=15)
        if "dumped to" not in out:
            raise Failure("uiautomator dump failed")
        self.last_dump = self.device.run("exec-out", "cat", DUMP_PATH)
        return ET.fromstring(self.last_dump)

    def wait_for(self, selector, present=True):
        attributes, index = parse_selector(selector)
        deadline = time.monotonic() + COMMAND_TIMEOUT
        while True:
            try:
                root = self.dump()
                node = find(root, attributes, index)
                if (node is not None) == present:
                    return root, node
            except Failure:
                pass
            if time.monotonic() > deadline:
                self.keep_last_dump()
                raise Failure("timeout waiting for " + ("" if present else "the end of ") + selector)
            time.sleep(0.5)

    def keep_last_dump(self):
        """Keeps the screen as last read, for finding out what was in the way."""
        if self.dump_dir and self.last_dump:
            os.makedirs(self.dump_dir, exist_ok=True)
            path = os.path.join(self.dump_dir, f"timeout-{self.commands}.xml")
            with open(path, "w", encoding="utf-8") as out:
                out.write(self.last_dump)
            print(f"ui-driver: kept the last screen in {path}", flush=True)

    # -- commands ---------------------------------------------------------------------------------

    def do_wait(self, selector):
        self.wait_for(selector)

    def do_wait_gone(self, selector):
        self.wait_for(selector, present=False)

    def do_tap(self, selector):
        _, node = self.wait_for(selector)
        if node.get("enabled") != "true":
            raise Failure("disabled")
        left, top, right, bottom = bounds(node)
        self.device.shell(f"input tap {(left + right) // 2} {(top + bottom) // 2}")

    def do_tap_outside(self, selector):
        root, node = self.wait_for(selector)
        point = outside_point(root, node)
        if point is None:
            raise Failure("no-outside")
        self.device.shell(f"input tap {point[0]} {point[1]}")

    def do_type(self, selector, text):
        if not TYPE_TEXT.match(text):
            raise Failure("bad-text")
        self.do_tap(selector)
        self.device.shell(f"input text {text}")

    def keyboard_shown(self):
        return "mInputShown=true" in self.device.shell("dumpsys input_method")

    def do_back(self, selector):
        self.wait_for(selector)
        if self.keyboard_shown():
            self.device.shell("input keyevent KEYCODE_BACK")
            deadline = time.monotonic() + COMMAND_TIMEOUT
            while self.keyboard_shown():
                if time.monotonic() > deadline:
                    raise Failure("the keyboard did not close")
                time.sleep(0.2)
        self.device.shell("input keyevent KEYCODE_BACK")

    def do_expect(self, selector, attribute, value):
        _, node = self.wait_for(selector)
        expected = decode(value)
        actual = node.get(attribute)
        if actual != expected:
            raise Failure(f"{attribute} is {actual!r}, not {expected!r}")

    def do_count(self, selector):
        attributes, _ = parse_selector(selector)
        return count(self.dump(), attributes)

    def do_rotation(self):
        return self.dump().get("rotation")

    def do_rotate(self, value):
        if value not in ("0", "1", "2", "3"):
            raise Failure("bad rotation")
        for key in ROTATION_SETTINGS:
            self.save("system", key)
        self.device.shell("settings put system accelerometer_rotation 0")
        self.device.shell(f"settings put system user_rotation {value}")
        deadline = time.monotonic() + COMMAND_TIMEOUT
        while self.dump().get("rotation") != value:
            if time.monotonic() > deadline:
                raise Failure("the display did not turn")
            time.sleep(0.5)

    def app_stopped(self):
        """Whether every activity record of the package reports STOPPED."""
        text = self.device.shell("dumpsys activity activities")
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
        self.device.shell("input keyevent KEYCODE_HOME")
        deadline = time.monotonic() + COMMAND_TIMEOUT
        while not self.app_stopped():
            if time.monotonic() > deadline:
                raise Failure("the app did not stop")
            time.sleep(0.2)
        return _Deferred(lambda: self.bring_back(delay))

    def bring_back(self, delay):
        time.sleep(delay / 1000)
        try:
            self.do_front()
        except Exception as error:  # noqa: BLE001
            print(f"ui-driver: could not bring the app back: {error}", file=sys.stderr)
            self.exit_code = max(self.exit_code, 1)

    def do_home(self):
        self.device.shell("input keyevent KEYCODE_HOME")

    def do_front(self):
        resolved = self.device.shell(
            f"cmd package resolve-activity --brief -c android.intent.category.LAUNCHER {self.package}").split()
        component = resolved[-1] if resolved else ""
        if "/" not in component:
            raise Failure("no launcher activity")
        self.device.shell(f"am start -n {component}")


class _Deferred:
    """A result that is written first, with work that runs after it (background's return trip)."""

    def __init__(self, after):
        self.after = after


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
        driver.restore()
        print(f"ui-driver: hello={driver.hellos} ready={driver.readies} commands={driver.commands} "
              f"exit={driver.exit_code}", flush=True)
    sys.exit(driver.exit_code)


if __name__ == "__main__":
    main()
