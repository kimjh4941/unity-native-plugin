#!/usr/bin/env bash
#
# Runs the gates an Android change has to pass, in one command: EditMode, then the PlayMode tests on
# a connected Android device (layer 2b), the only place the P/Invoke path into libntk.so executes.
#
# Which build profile is active matters, and is easy to get wrong from the command line:
#
# - The build steps (PreBuildProcessor, which copies the AARs pinned in Plugins/Android/VERSION.txt)
#   live in an assembly that is compiled only when NATIVETOOLKIT_ENABLE_BUILD_STEPS is defined. The
#   "Android™ Profile" takes the defines from Player Settings, which have it. The "Test Android™
#   Profile" deliberately has none: it reproduces a project that installed the package from UPM, where
#   the AARs committed in Plugins/Android are what ships. With the test profile active, the build-step
#   assembly is not compiled at all, so a compile error in it goes unnoticed.
# - The active profile persists in Library/ between runs. -activeBuildProfile sets it for this run
#   and every later one, so this script always passes it.
#
# So the default runs everything with the Android™ Profile (build steps on: the AARs are copied from
# the sibling native-toolkit dist, and the build-step assembly is compiled). --as-package-user runs
# the device tests again with the Test Android™ Profile, against the AARs the first run left in
# Plugins/Android - what a package user's build sees.
#
# Only 64-bit players are built. native-toolkit ships libntk.so for arm64-v8a and x86_64 only, and
# its AAR's Prefab module stops any build that also targets ARMv7 inside Gradle; the package now
# stops such a build earlier, with a clear message (AndroidArchitectureCheck). 32-bit Android is out
# of scope (artifact/topics/android-c-abi-2, J-10), so there is no 32-bit run. Mono is not run
# either: on Android it builds ARMv7 only.
#
# The device runs start the host-side UI driver (scripts/android_ui_driver.py), which presses the OS
# dialogs the tests ask for (artifact/topics/android-c-abi-2/designs/2026-10-11-android-ui-driver-
# design-v4.md). A device run fails when the driver exits non-zero (a setting it could not restore,
# a HELLO it could not answer), and - with the Android™ Profile, whose player holds the sample scene -
# when a test of the AndroidUiDriver category was skipped, or when no such test ran at all. The driver
# changes device settings while it runs and restores them when it stops; a trap stops it even when
# this script is interrupted.
#
# The Editor ships its own adb (36.x), and an older adb on PATH (the Android SDK's platform-tools)
# kills its server and starts its own: whichever runs second disconnects the other. The script puts
# Unity's adb first on PATH, and uses it for its own checks.
#
# Unity re-serializes some assets during a run (render pipeline settings, the solution file). The
# script restores what a run changed under those locations and leaves alone anything that already
# had changes before the run. --keep-changes skips that.
#
# Usage:
#   scripts/verify_unity_android.sh [--skip-editmode] [--as-package-user] [--keep-changes]
#
# Environment:
#   UNITY_APP   Path to Unity.app. Defaults to the 6000.4.2f1 install used by this project.
#   OUT_DIR     Where to put logs and test results. Defaults to a temp dir.

set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_APP="${UNITY_APP:-/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app}"
UNITY_EXE="$UNITY_APP/Contents/MacOS/Unity"
UNITY_ADB_DIR="$(cd "$UNITY_APP/.." && pwd)/PlaybackEngines/AndroidPlayer/SDK/platform-tools"
OUT_DIR="${OUT_DIR:-${TMPDIR:-/tmp}/unity-android-verify}"
PROFILE="Assets/Settings/Build Profiles/Android™ Profile.asset"
TEST_PROFILE="Assets/Settings/Build Profiles/Test Android™ Profile.asset"
SKIP_EDITMODE=0
AS_PACKAGE_USER=0
KEEP_CHANGES=0

for arg in "$@"; do
  case "$arg" in
    --skip-editmode) SKIP_EDITMODE=1 ;;
    --as-package-user) AS_PACKAGE_USER=1 ;;
    --keep-changes) KEEP_CHANGES=1 ;;
    *) echo "unknown argument: $arg" >&2; exit 2 ;;
  esac
done

if [ ! -x "$UNITY_EXE" ]; then
  echo "Unity not found at: $UNITY_EXE" >&2
  echo "Set UNITY_APP to your Unity.app and run again." >&2
  exit 2
fi
if [ ! -x "$UNITY_ADB_DIR/adb" ]; then
  echo "Unity's adb not found at: $UNITY_ADB_DIR (is Android Build Support installed?)" >&2
  exit 2
fi
export PATH="$UNITY_ADB_DIR:$PATH"

device_count="$(adb devices | awk 'NR > 1 && $2 == "device"' | wc -l | tr -d ' ')"
if [ "$device_count" != "1" ]; then
  echo "Expected exactly one Android device in the 'device' state, found $device_count:" >&2
  adb devices >&2
  exit 2
fi

mkdir -p "$OUT_DIR"
failures=0

SIDE_EFFECT_PATHS=(
  "Assets/Settings"
  "ProjectSettings"
  "unity-native-plugin.slnx"
)

changed_paths() {
  git -C "$PROJECT_DIR" status --porcelain=v1 -z --untracked-files=all -- "${SIDE_EFFECT_PATHS[@]}" |
    while IFS= read -r -d '' entry; do
      local status="${entry:0:2}" path="${entry:3}"
      case "$status" in R*|C*) IFS= read -r -d '' _ ;; esac
      printf '%s\t%s\n' "$status" "$path"
    done
}

CHANGED_BEFORE="$(changed_paths)"

report_tests() {
  local label="$1" xml="$2" exit_code="$3"
  if [ ! -f "$xml" ]; then
    echo "  $label: NO RESULT FILE (Unity exited $exit_code; see the log)"
    return 1
  fi
  local counts
  counts="$(grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' "$xml" | head -1)"
  echo "  $label: ${counts:-unreadable}"
  case "$counts" in
    *'total="0" '*) echo "  $label: no test ran"; return 1 ;;
    *'failed="0" '*) return 0 ;;
    *) return 1 ;;
  esac
}

# run_tests <label> <platform> <profile> [extra Unity arguments...]
run_tests() {
  local label="$1" platform="$2" profile="$3"
  shift 3
  local xml="$OUT_DIR/$label.xml"
  local log="$OUT_DIR/$label.log"
  rm -f "$xml" "$log"
  # -runTests and -quit cannot be combined: Unity would exit before the run finishes.
  "$UNITY_EXE" -batchmode -projectPath "$PROJECT_DIR" -activeBuildProfile "$profile" \
    -runTests -testPlatform "$platform" -testResults "$xml" -logFile "$log" "$@" >/dev/null 2>&1
  local code=$?
  report_tests "$label" "$xml" "$code" || failures=$((failures + 1))
  grep -n "error CS" "$log" | sort -u -t: -k2 | head -5
  grep -n "BuildFailedException\|Error building Player" "$log" | head -3
}

DRIVER="$PROJECT_DIR/scripts/android_ui_driver.py"
DRIVER_PID=""

stop_driver() {
  if [ -n "$DRIVER_PID" ]; then
    kill -TERM "$DRIVER_PID" 2>/dev/null
    wait "$DRIVER_PID"
    local code=$?
    DRIVER_PID=""
    return $code
  fi
  return 0
}
trap 'stop_driver >/dev/null 2>&1' EXIT INT TERM HUP

# Prints how many tests of the AndroidUiDriver category ran and were skipped, from a result file.
# A category put on a fixture is written on its test-suite, so a case inherits its suites' categories.
ui_driver_counts() {
  python3 - "$1" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
def categories(element):
    return {p.get("value") for p in element.findall("./properties/property[@name='Category']")}
total = skipped = 0
def walk(element, inherited):
    global total, skipped
    mine = inherited | categories(element)
    for child in element:
        if child.tag in ("test-suite", "test-case"):
            walk(child, mine)
    if element.tag == "test-case" and "AndroidUiDriver" in mine:
        total += 1
        if element.get("result") == "Skipped":
            skipped += 1
walk(root, set())
print(total, skipped)
PY
}

# run_device_tests <label> <profile> <check ui tests: 1|0>
run_device_tests() {
  local label="$1" profile="$2" check_ui="$3"
  local driver_log="$OUT_DIR/$label-ui-driver.log"
  python3 "$DRIVER" --state-file "$OUT_DIR/ui-driver-settings.json" --dump-dir "$OUT_DIR/$label-ui-dumps" > "$driver_log" 2>&1 &
  DRIVER_PID=$!
  run_tests "$label" Android "$profile"
  stop_driver
  local driver_code=$?
  echo "  ui driver: $(tail -1 "$driver_log") (exit $driver_code)"
  if [ "$driver_code" -ne 0 ]; then
    echo "  ui driver: FAILED; see $driver_log"
    failures=$((failures + 1))
  fi
  local xml="$OUT_DIR/$label.xml"
  if [ "$check_ui" -eq 1 ] && [ -f "$xml" ]; then
    read -r ui_total ui_skipped <<< "$(ui_driver_counts "$xml")"
    echo "  AndroidUiDriver tests: $ui_total, skipped $ui_skipped"
    if [ "$ui_total" -eq 0 ] || [ "$ui_skipped" -ne 0 ]; then
      echo "  AndroidUiDriver tests: FAILED (every one must run, and at least one must exist)"
      failures=$((failures + 1))
    fi
  fi
}

echo "== Unity Android verification =="
echo "project: $PROJECT_DIR"
echo "output:  $OUT_DIR"
echo "device:  $(adb shell getprop ro.product.model | tr -d '\r') (Android $(adb shell getprop ro.build.version.release | tr -d '\r'))"
echo

echo "-- UI driver doctest --"
if python3 -m doctest "$DRIVER"; then
  echo "  doctest: passed"
else
  echo "  doctest: FAILED"
  failures=$((failures + 1))
fi
echo

if [ "$SKIP_EDITMODE" -eq 0 ]; then
  echo "-- EditMode (Android™ Profile: the build-step assembly is compiled) --"
  run_tests EditMode EditMode "$PROFILE"
  echo
fi

echo "-- device PlayMode (IL2CPP, Android™ Profile: AARs copied from dist) --"
run_device_tests Android "$PROFILE" 1
echo

if [ "$AS_PACKAGE_USER" -eq 1 ]; then
  echo "-- device PlayMode (IL2CPP, Test Android™ Profile: as a package user) --"
  run_device_tests Android-package-user "$TEST_PROFILE" 0
  echo
fi

if [ "$KEEP_CHANGES" -eq 0 ]; then
  while IFS=$'\t' read -r status path; do
    [ -z "$path" ] && continue
    if printf '%s\n' "$CHANGED_BEFORE" | grep -qxF "$status"$'\t'"$path"; then
      continue
    fi
    case "$status" in
      '??') rm -rf "${PROJECT_DIR:?}/$path" ;;
      *) git -C "$PROJECT_DIR" checkout -- "$path" ;;
    esac
  done <<< "$(changed_paths)"
fi

if [ "$failures" -eq 0 ]; then
  echo "== PASS =="
else
  echo "== FAIL ($failures) =="
fi
exit "$failures"
