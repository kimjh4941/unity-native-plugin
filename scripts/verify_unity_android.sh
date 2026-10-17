#!/usr/bin/env bash
#
# Runs the gates an Android change has to pass, in one command: EditMode, the PlayMode tests inside
# the Editor (layer 2a), then the PlayMode tests on a connected Android device (layer 2b), the only
# place the P/Invoke path into libntk.so executes.
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
# dialogs, the share sheet and the notification shade the tests ask for (artifact/topics/android-c-
# abi-2/designs/2026-10-12-android-ui-driver-design-v15.md). It is told the test player's package and
# ignores requests from any other. A device run fails when the driver exits non-zero (a setting it
# could not restore, a HELLO it could not answer, a screen read it could not delete), when a test of
# the AndroidUiDriver category was skipped, or when no such test ran at all. The Test Android™
# Profile's player has no sample scenes, so in its run the tests that go through the sample screens
# (category AndroidSampleScreen) are left out of that check, and only they. The driver changes device
# settings while it runs and restores them when it stops; a trap stops it even when this script is
# interrupted.
#
# The Editor ships its own adb (36.x), and an older adb on PATH (the Android SDK's platform-tools)
# kills its server and starts its own: whichever runs second disconnects the other. The script puts
# Unity's adb first on PATH, and uses it for its own checks.
#
# The sample app (the Android™ Profile's own build, installed for manual checks) has the same name as
# the test player, and the share sheet lists apps by name only: a test that picks this app in the
# sheet could pick the sample instead. So the device runs uninstall the sample app first, without
# asking, and uninstall the test player when they end, so a manual check that installs the sample
# afterwards finds it alone (2026-10-12). Each device run also starts from a fresh install of the test
# player: the Notification tests use the state a fresh install has (notification permission not yet
# asked, exact alarms not allowed), so the player is uninstalled before each run, and by the trap.
# The Notification retention test (category AndroidNotificationRetention) needs a process where the
# notification manager was never created, so it has a device run of its own, after the main one; the
# other device runs leave it out (-testCategory "!AndroidNotificationRetention").
#
# Unity re-serializes some assets during a run (render pipeline settings, the solution file). The
# script restores what a run changed under those locations and leaves alone anything that already
# had changes before the run. --keep-changes skips that.
#
# Usage:
#   scripts/verify_unity_android.sh [--skip-editmode] [--as-package-user] [--keep-changes]
#                                   [--pull-apk=<dir>]
#
#   --skip-editmode skips both Editor runs (EditMode and the Editor's PlayMode) and runs the device
#   tests only.
#   --pull-apk=<dir> copies the test player's APKs off the device after each device run, before the
#   player is uninstalled, unpacks them under <dir>/<run>/, and prints how many classes and manifest
#   names of the 1.x bridge (android.unity.*, android.library.*) they hold. The counts are printed,
#   not judged: the check made while the 1.x AARs were still shipped expects them above zero, the one
#   after they were removed expects zero (artifact/topics/android-c-abi-2, stage 4 design v8,
#   V-S4-1). A run whose APKs cannot be pulled, or that has no APK or no dex, fails. <dir> must not
#   exist or must be empty, so a count never comes from an earlier run.
#
# Environment:
#   UNITY_APP   Path to Unity.app. Defaults to the 6000.4.2f1 install used by this project.
#   OUT_DIR     Where to put logs and test results. Defaults to a temp dir.

set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_APP="${UNITY_APP:-/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app}"
UNITY_EXE="$UNITY_APP/Contents/MacOS/Unity"
ANDROID_PLAYER_DIR="$(cd "$UNITY_APP/.." && pwd)/PlaybackEngines/AndroidPlayer"
UNITY_ADB_DIR="$ANDROID_PLAYER_DIR/SDK/platform-tools"
DEXDUMP="$ANDROID_PLAYER_DIR/SDK/build-tools/36.0.0/dexdump"
APKANALYZER="$ANDROID_PLAYER_DIR/SDK/cmdline-tools/16.0/bin/apkanalyzer"
OUT_DIR="${OUT_DIR:-${TMPDIR:-/tmp}/unity-android-verify}"
PROFILE="Assets/Settings/Build Profiles/Android™ Profile.asset"
TEST_PROFILE="Assets/Settings/Build Profiles/Test Android™ Profile.asset"
SKIP_EDITMODE=0
AS_PACKAGE_USER=0
KEEP_CHANGES=0
PULL_APK_DIR=""

for arg in "$@"; do
  case "$arg" in
    --skip-editmode) SKIP_EDITMODE=1 ;;
    --as-package-user) AS_PACKAGE_USER=1 ;;
    --keep-changes) KEEP_CHANGES=1 ;;
    --pull-apk=?*) PULL_APK_DIR="${arg#--pull-apk=}" ;;
    --pull-apk|--pull-apk=) echo "--pull-apk takes a directory: --pull-apk=<dir>" >&2; exit 2 ;;
    *) echo "unknown argument: $arg" >&2; exit 2 ;;
  esac
done

if [ -n "$PULL_APK_DIR" ]; then
  if [ -e "$PULL_APK_DIR" ] && [ -n "$(ls -A "$PULL_APK_DIR" 2>/dev/null)" ]; then
    echo "--pull-apk: $PULL_APK_DIR is not empty; give a new or empty directory" >&2
    exit 2
  fi
  for tool in "$DEXDUMP" "$APKANALYZER" "$ANDROID_PLAYER_DIR/OpenJDK/bin/java"; do
    if [ ! -x "$tool" ]; then
      echo "--pull-apk: not found: $tool" >&2
      exit 2
    fi
  done
  mkdir -p "$PULL_APK_DIR" && PULL_APK_DIR="$(cd "$PULL_APK_DIR" && pwd)"
fi

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
TEST_PLAYER_ID="com.UnityTestRunner.UnityTestRunner"

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
# The driver first (it may still be pressing the player's screens), then the player.
trap 'stop_driver >/dev/null 2>&1; adb uninstall "$TEST_PLAYER_ID" >/dev/null 2>&1' EXIT INT TERM HUP

# Prints how many tests of the AndroidUiDriver category ran and were skipped, from a result file,
# leaving out those that also have the category given second (if any).
# A category put on a fixture is written on its test-suite, so a case inherits its suites' categories.
ui_driver_counts() {
  python3 - "$1" "${2:-}" <<'PY'
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
    if element.tag == "test-case" and "AndroidUiDriver" in mine and not (sys.argv[2] and sys.argv[2] in mine):
        total += 1
        if element.get("result") == "Skipped":
            skipped += 1
walk(root, set())
print(total, skipped)
PY
}

# The Notification retention test (AN-01) holds only in a process where nothing has created the
# notification manager, so it runs alone, from a fresh install, and every other device run leaves
# its category out (the Notification UI test plan v5, chapter 2).
RETENTION_CATEGORY="AndroidNotificationRetention"

# pull_apks <label>: copies every APK of the test player off the device into <dir>/<label>/apks,
# unpacks each into its own folder under <dir>/<label>/expanded, and counts the 1.x bridge's classes
# in all their dex files and its names in the base APK's merged manifest. The action IDs the
# Notification still publishes (android.unity.notification.ACTION_*) are values, not classes, and
# are not counted. The names found are written next to the APKs.
pull_apks() {
  local label="$1"
  local dir="$PULL_APK_DIR/$label"
  mkdir -p "$dir/apks" "$dir/expanded"
  local paths
  paths="$(adb shell pm path "$TEST_PLAYER_ID" | tr -d '\r' | sed -n 's/^package://p')"
  if [ -z "$paths" ]; then
    echo "  apk: FAILED: no APK of $TEST_PLAYER_ID on the device"
    failures=$((failures + 1))
    return
  fi
  local apk_count=0 dex_count=0 path name dex dump
  : > "$dir/old-classes.txt"
  : > "$dir/old-manifest-names.txt"
  while IFS= read -r path; do
    name="$(basename "$path" .apk)"
    if ! adb pull "$path" "$dir/apks/$name.apk" >/dev/null 2>&1; then
      echo "  apk: FAILED: could not pull $path"
      failures=$((failures + 1))
      continue
    fi
    apk_count=$((apk_count + 1))
    if ! unzip -q -o "$dir/apks/$name.apk" -d "$dir/expanded/$name"; then
      echo "  apk: FAILED: could not unpack $name.apk"
      failures=$((failures + 1))
      continue
    fi
    for dex in "$dir/expanded/$name"/classes*.dex; do
      [ -f "$dex" ] || continue
      dex_count=$((dex_count + 1))
      if ! dump="$("$DEXDUMP" "$dex" 2>/dev/null)"; then
        echo "  apk: FAILED: dexdump could not read $name/$(basename "$dex")"
        failures=$((failures + 1))
        continue
      fi
      printf '%s\n' "$dump" | grep -oE "Class descriptor +: 'Landroid/(unity|library)/[^']*'" \
        | sed "s/^.*'\(.*\)'\$/\1/" >> "$dir/old-classes.txt"
    done
    if [ "$name" = "base" ]; then
      if JAVA_HOME="$ANDROID_PLAYER_DIR/OpenJDK" "$APKANALYZER" manifest print "$dir/apks/$name.apk" \
          > "$dir/expanded/base-manifest.xml" 2>/dev/null; then
        grep -oE 'android\.(unity|library)\.[A-Za-z0-9_.$]+' "$dir/expanded/base-manifest.xml" \
          | grep -v '^android\.unity\.notification\.ACTION_' >> "$dir/old-manifest-names.txt"
      else
        echo "  apk: FAILED: apkanalyzer could not print the base APK's manifest"
        failures=$((failures + 1))
      fi
    fi
  done <<< "$paths"
  if [ "$apk_count" -eq 0 ] || [ "$dex_count" -eq 0 ]; then
    echo "  apk: FAILED: $apk_count APK, $dex_count dex"
    failures=$((failures + 1))
  fi
  local old_classes old_names
  old_classes="$(wc -l < "$dir/old-classes.txt" | tr -d ' ')"
  old_names="$(wc -l < "$dir/old-manifest-names.txt" | tr -d ' ')"
  echo "  apk: $apk_count APK, $dex_count dex; 1.x classes in the dex: $old_classes," \
    "1.x names in the manifest: $old_names ($dir)"
}

# run_device_tests <label> <profile> <category left out of the AndroidUiDriver check, or ""> <-testCategory filter>
run_device_tests() {
  local label="$1" profile="$2" left_out="$3" categories="$4"
  local driver_log="$OUT_DIR/$label-ui-driver.log"
  uninstall_app "$TEST_PLAYER_ID"
  python3 "$DRIVER" --package "$TEST_PLAYER_ID" --state-file "$OUT_DIR/ui-driver-settings.json" \
    --dump-dir "$OUT_DIR/$label-ui-dumps" > "$driver_log" 2>&1 &
  DRIVER_PID=$!
  run_tests "$label" Android "$profile" -testCategory "$categories"
  # The player stays installed until the next run's (or the final) uninstall.
  [ -n "$PULL_APK_DIR" ] && pull_apks "$label"
  stop_driver
  local driver_code=$?
  echo "  ui driver: $(tail -1 "$driver_log") (exit $driver_code)"
  if [ "$driver_code" -ne 0 ]; then
    echo "  ui driver: FAILED; see $driver_log"
    failures=$((failures + 1))
  fi
  local xml="$OUT_DIR/$label.xml"
  if [ -f "$xml" ]; then
    read -r ui_total ui_skipped <<< "$(ui_driver_counts "$xml" "$left_out")"
    echo "  AndroidUiDriver tests${left_out:+ (without $left_out)}: $ui_total, skipped $ui_skipped"
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
if PYTHONDONTWRITEBYTECODE=1 python3 -m doctest "$DRIVER"; then
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
  # Layer 2a: PlayMode tests inside the Editor (the Android managers' Editor paths and the callback
  # queue). The device runs below do not reach them: they are compiled for the Editor only.
  echo "-- PlayMode in the Editor (layer 2a) --"
  run_tests PlayMode PlayMode "$PROFILE"
  echo
fi

# The sample app's package, as the Android™ Profile sets it.
SAMPLE_APP_ID="$(sed -n "s/^.*|     Android: \(com\.[A-Za-z0-9_.]*\)'\$/\1/p" "$PROJECT_DIR/$PROFILE" | head -1)"

# uninstall_app <package>: removes it if installed; says what it did.
uninstall_app() {
  if adb shell pm list packages "$1" | tr -d '\r' | grep -qx "package:$1"; then
    if adb uninstall "$1" >/dev/null 2>&1; then
      echo "  uninstalled $1"
    else
      echo "  could not uninstall $1"
      failures=$((failures + 1))
    fi
  fi
}

echo "-- device apps --"
if [ -n "$SAMPLE_APP_ID" ]; then
  uninstall_app "$SAMPLE_APP_ID"
else
  echo "  the sample app's package was not found in $PROFILE"
  failures=$((failures + 1))
fi
echo

echo "-- device PlayMode (IL2CPP, Android™ Profile: AARs copied from dist) --"
run_device_tests Android "$PROFILE" "" "!$RETENTION_CATEGORY"
echo

echo "-- device PlayMode (IL2CPP, Android™ Profile: the Notification retention test alone) --"
run_device_tests Android-retention "$PROFILE" "" "$RETENTION_CATEGORY"
echo

if [ "$AS_PACKAGE_USER" -eq 1 ]; then
  echo "-- device PlayMode (IL2CPP, Test Android™ Profile: as a package user) --"
  run_device_tests Android-package-user "$TEST_PROFILE" AndroidSampleScreen "!$RETENTION_CATEGORY"
  echo
fi

echo "-- device apps --"
uninstall_app "$TEST_PLAYER_ID"
echo

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
