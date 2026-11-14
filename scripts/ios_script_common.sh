# Shared by scripts/verify_unity_ios.sh and scripts/test_ios_ui.sh (sourced, not run).
#
# - run_limited: runs a long step with a time limit and a limit on how long a watched file (Unity's
#   -logFile, or the step's output) may stay unchanged, so a step that hangs fails instead of waiting
#   forever. A signal that ends the script stops the running step and everything it started.
# - restore_side_effects: puts back what a run changed under SIDE_EFFECT_PATHS (Unity re-serializes
#   settings and leaves its test scene behind; the iOS build steps drop the executable bit of the
#   XCFramework binaries they copy), leaving alone anything that had changes before the run.
#
# The caller sets PROJECT_DIR and KEEP_CHANGES, calls record_side_effects_baseline once before the
# first step, wraps each Unity step in begin_unity_step / end_unity_step, and calls stop_running_step
# and then restore_after_interrupted_unity_step from its EXIT trap.

SIDE_EFFECT_PATHS=(
  "Assets/Settings"
  "ProjectSettings"
  "unity-native-plugin.slnx"
  ".vscode/settings.json"
  "Assets/InitTestScene*"
  "Assets/Resources/PerformanceTestRun*"
  "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS"
)

changed_paths() {
  git -C "$PROJECT_DIR" status --porcelain=v1 -z --untracked-files=all -- "${SIDE_EFFECT_PATHS[@]}" |
    while IFS= read -r -d '' entry; do
      local status="${entry:0:2}" path="${entry:3}"
      case "$status" in R*|C*) IFS= read -r -d '' _ ;; esac
      printf '%s\t%s\n' "$status" "$path"
    done
}

# The iOS build steps delete and copy the XCFrameworks in Plugins/iOS, so changes made there before a
# run could not be told apart from the run's own, nor restored. Exits when there are any.
require_clean_ios_plugins() {
  if [ -n "$(git -C "$PROJECT_DIR" status --porcelain=v1 --untracked-files=all -- Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS)" ]; then
    echo "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS has uncommitted changes. Commit or stash them first:" >&2
    echo "the build steps replace those files during the run." >&2
    exit 2
  fi
}

CHANGED_BEFORE=""
record_side_effects_baseline() {
  CHANGED_BEFORE="$(changed_paths)"
}

# A Unity step is marked while it runs and until its changes are put back: begin_unity_step before it,
# end_unity_step after it (which restores). The end of a run and the EXIT trap restore only while a step is
# marked (stopped in the middle), so files a person edits while a run waits or tests are not put back
# (keep-installed design v4, section 4; Codex KI-01).
UNITY_STEP_ACTIVE=0
begin_unity_step() {
  UNITY_STEP_ACTIVE=1
}
end_unity_step() {
  restore_side_effects
  UNITY_STEP_ACTIVE=0
}
restore_after_interrupted_unity_step() {
  [ "$UNITY_STEP_ACTIVE" -eq 1 ] || return 0
  restore_side_effects
  UNITY_STEP_ACTIVE=0
}

# Puts back what the Unity steps changed since the baseline. Called through end_unity_step and
# restore_after_interrupted_unity_step.
restore_side_effects() {
  [ "${KEEP_CHANGES:-0}" -eq 1 ] && return
  while IFS=$'\t' read -r status path; do
    [ -z "$path" ] && continue
    if grep -qxF "$status"$'\t'"$path" <<< "$CHANGED_BEFORE"; then
      continue
    fi
    case "$status" in
      '??') rm -rf "${PROJECT_DIR:?}/$path" ;;
      *) git -C "$PROJECT_DIR" checkout -- "$path" ;;
    esac
  done <<< "$(changed_paths)"
}

# kill_tree <signal> <pid>: the process and everything it started (xcodebuild runs clang, Unity its
# helpers), children first.
kill_tree() {
  local signal="$1" pid="$2" child
  for child in $(pgrep -P "$pid" 2>/dev/null); do
    kill_tree "$signal" "$child"
  done
  kill "-$signal" "$pid" 2>/dev/null
}

# stop_tree <pid>: TERM, then KILL if it has not ended after 10 seconds.
stop_tree() {
  local pid="$1" waited=0
  kill_tree TERM "$pid"
  while kill -0 "$pid" 2>/dev/null && [ "$waited" -lt 10 ]; do
    sleep 1; waited=$((waited + 1))
  done
  kill -0 "$pid" 2>/dev/null && kill_tree KILL "$pid"
  wait "$pid" 2>/dev/null
}

# The command run_limited is running, so the EXIT trap can stop it (a background child does not get
# the terminal's Ctrl-C).
RUNNING_PID=""
stop_running_step() {
  if [ -n "$RUNNING_PID" ]; then
    stop_tree "$RUNNING_PID"
    RUNNING_PID=""
  fi
}

# run_limited <label> <limit seconds> <stall seconds> <watched file> <output log> <command...>
# Runs the command in the background with its output appended to the output log. Stops it, and
# fails with 124, when it runs longer than the limit or the watched file stays unchanged for the stall
# time. Prints progress every 60 seconds. Returns the command's exit code otherwise.
run_limited() {
  local label="$1" limit="$2" stall="$3" watched="$4" log="$5"
  shift 5
  : >> "$log"
  "$@" >> "$log" 2>&1 &
  RUNNING_PID=$!
  local pid=$RUNNING_PID start now size last_size=-1 last_change last_report code
  start=$(date +%s); last_change=$start; last_report=$start
  while kill -0 "$pid" 2>/dev/null; do
    sleep 2
    now=$(date +%s); size=$(stat -f %z "$watched" 2>/dev/null || echo 0)
    if [ "$size" != "$last_size" ]; then last_size=$size; last_change=$now; fi
    if [ $((now - start)) -ge "$limit" ] || [ $((now - last_change)) -ge "$stall" ]; then
      echo "  $label: STOPPED after $((now - start))s (limit ${limit}s; $(basename "$watched") unchanged for $((now - last_change))s, stall limit ${stall}s)"
      stop_tree "$pid"
      RUNNING_PID=""
      return 124
    fi
    if [ $((now - last_report)) -ge 60 ]; then
      echo "  $label: running $((now - start))s"
      last_report=$now
    fi
  done
  wait "$pid"
  code=$?
  RUNNING_PID=""
  return $code
}

# find_simulator <name> <runtime>: prints the UDID of the one available simulator with that name on
# that runtime, or nothing.
find_simulator() {
  xcrun simctl list -j devices available | python3 -I -c '
import json, sys
name, runtime = sys.argv[1], sys.argv[2]
key = "com.apple.CoreSimulator.SimRuntime." + runtime.replace(" ", "-").replace(".", "-")
ids = [d["udid"] for d in json.load(sys.stdin)["devices"].get(key, []) if d["name"] == name]
print(ids[0] if len(ids) == 1 else "")
' "$1" "$2"
}

# select_device <work dir>: sets DEVICE_ID (for devicectl) and DEVICE_UDID (for xcodebuild) to the one
# paired iPhone with Developer Mode on. Prints neither. Returns 1 when there is not exactly one.
select_device() {
  local json="$1/devices.json" ids
  xcrun devicectl list devices --json-output "$json" >/dev/null 2>&1
  ids="$(python3 -I -c '
import json, sys
devices = json.load(open(sys.argv[1]))["result"]["devices"]
found = [(d["identifier"], d.get("hardwareProperties", {}).get("udid", "")) for d in devices
         if d.get("hardwareProperties", {}).get("deviceType") == "iPhone"
         and d.get("connectionProperties", {}).get("pairingState") == "paired"
         and d.get("deviceProperties", {}).get("developerModeStatus") == "enabled"]
print(" ".join(found[0]) if len(found) == 1 and found[0][1] else "")
' "$json" 2>/dev/null)"
  rm -f "$json"
  DEVICE_ID="${ids%% *}"
  DEVICE_UDID="${ids#* }"
  if [ -z "$ids" ] || [ -z "$DEVICE_UDID" ]; then
    echo "Expected exactly one paired iPhone with Developer Mode on." >&2
    return 1
  fi
}

# select_team: sets TEAM to the team of the unexpired Xcode provisioning profiles - exactly one, or the
# signing would be a guess. Prints nothing. Returns 1 otherwise.
select_team() {
  TEAM="$(for profile in ~/Library/Developer/Xcode/UserData/Provisioning\ Profiles/*; do
      security cms -D -i "$profile" 2>/dev/null | python3 -I -c '
import datetime, plistlib, sys
p = plistlib.loads(sys.stdin.buffer.read())
if p["ExpirationDate"].replace(tzinfo=datetime.timezone.utc) > datetime.datetime.now(datetime.timezone.utc):
    print(p["TeamIdentifier"][0])
' 2>/dev/null
    done | sort -u)"
  if [ -z "$TEAM" ] || [ "$(printf '%s\n' "$TEAM" | grep -c .)" != "1" ]; then
    echo "Expected the unexpired Xcode provisioning profiles to belong to exactly one team (sign in to Xcode once)." >&2
    TEAM=""
    return 1
  fi
}

# show_build_errors <log> <device mode 0|1>: a device build's errors can name the team, certificates
# and the device, so a device run counts them instead of showing them.
show_build_errors() {
  if [ "$2" -eq 1 ]; then
    echo "  build: failed ($(grep -c 'error:' "$1") error line(s); not shown for a device run)"
  else
    echo "  build: failed (see $(basename "$1"))"
    grep -E 'error:' "$1" | sort -u | head -5
  fi
}

# remove_from_device <bundle id>...: removes the apps from DEVICE_ID and reads back that each is gone.
# Returns 1 (and says which) when an app could not be confirmed gone. Uses OUT_DIR for its files.
remove_from_device() {
  local id left status=0
  for id in "$@"; do
    run_limited "remove $id" 120 120 "$OUT_DIR/remove.log" "$OUT_DIR/remove.log" \
      xcrun devicectl device uninstall app --device "$DEVICE_ID" "$id" >/dev/null
    rm -f "$OUT_DIR/apps.json"
    run_limited "check $id" 120 120 "$OUT_DIR/remove.log" "$OUT_DIR/remove.log" \
      xcrun devicectl device info apps --device "$DEVICE_ID" --bundle-id "$id" --json-output "$OUT_DIR/apps.json" >/dev/null
    left="$(python3 -I -c 'import json,sys; print(len(json.load(open(sys.argv[1]))["result"]["apps"]))' "$OUT_DIR/apps.json" 2>/dev/null)"
    case "$left" in
      0) ;;
      '') echo "  could not read whether $id is still on the device"; status=1 ;;
      *) echo "  $id is still on the device"; status=1 ;;
    esac
  done
  return $status
}

# require_bundle_id <.app> <bundle id>: returns 1 unless the app has that bundle id (so cleanup, which
# removes by bundle id, removes what was installed).
require_bundle_id() {
  local actual
  actual="$(/usr/libexec/PlistBuddy -c 'Print CFBundleIdentifier' "$1/Info.plist" 2>/dev/null)"
  if [ "$actual" != "$2" ]; then
    echo "  build: the app's bundle id is '${actual:-unreadable}', expected $2"
    return 1
  fi
}

# The run lock (keep-installed design v4, 1.2 step 1): every run that touches a device or the simulator
# holds it, from right after its arguments are checked until its cleanup is done, so a run never removes
# the harness another run (waiting for the user) installed. A small helper (ios_ui_runner.py run-lock)
# holds an fcntl lock for this shell: it ignores TERM, HUP and INT and ends when this shell ends or
# release_run_lock sends SIGUSR1, so a stale lock never outlives the run.
RUN_LOCK_FILE="$HOME/Library/Application Support/unity-native-plugin/test_ios-run.lock"
RUN_LOCK_PID=""
take_run_lock() {
  local answer out
  out="$(mktemp "${TMPDIR:-/tmp}/ntk-run-lock.XXXXXX")"
  python3 -I "$PROJECT_DIR/scripts/ios_ui_runner.py" run-lock "$RUN_LOCK_FILE" > "$out" 2>&1 &
  RUN_LOCK_PID=$!
  local tries=0
  while [ ! -s "$out" ] && [ "$tries" -lt 50 ] && kill -0 "$RUN_LOCK_PID" 2>/dev/null; do
    sleep 0.2
    tries=$((tries + 1))
  done
  answer="$(head -1 "$out")"
  rm -f "$out"
  if [ "$answer" != "locked" ]; then
    kill "$RUN_LOCK_PID" 2>/dev/null
    RUN_LOCK_PID=""
    echo "Another iOS test run holds the run lock; wait for it to end ($RUN_LOCK_FILE)." >&2
    return 1
  fi
}
# Fails when the lock holder is gone (the run must not go on without the lock).
run_lock_held() {
  [ -n "$RUN_LOCK_PID" ] && kill -0 "$RUN_LOCK_PID" 2>/dev/null
}
# Waits for the holder to end, so the lock is free when this run ends (a run started right after must
# not find it held; the holder checks for the signal once a second).
release_run_lock() {
  if [ -n "$RUN_LOCK_PID" ]; then
    kill -USR1 "$RUN_LOCK_PID" 2>/dev/null
    wait "$RUN_LOCK_PID" 2>/dev/null
  fi
  RUN_LOCK_PID=""
}

