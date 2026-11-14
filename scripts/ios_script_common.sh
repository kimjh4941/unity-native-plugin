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
# first step, and calls stop_running_step from its EXIT trap before restoring.

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

# Safe to call more than once: after a Unity step (so later steps start from a clean tree) and at the
# end, or from the EXIT trap.
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
