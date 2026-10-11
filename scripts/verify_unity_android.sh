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
# --mono builds the test player with Mono instead of IL2CPP (Player Settings use IL2CPP), through
# the Test Framework's settings file. Unity's Mono on Android builds ARMv7 (32-bit) only, and
# native-toolkit ships libntk.so for arm64-v8a and x86_64 only, so a Mono player cannot load the
# C ABI at all. The Mono run therefore checks the other path: that every manager reports the C ABI
# as unavailable instead of throwing DllNotFoundException at the caller (J-5 in
# artifact/topics/android-c-abi-2). The device has to run 32-bit apps (ro.product.cpu.abilist32).
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
#   scripts/verify_unity_android.sh [--skip-editmode] [--mono] [--as-package-user] [--keep-changes]
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
MONO=0
AS_PACKAGE_USER=0
KEEP_CHANGES=0

for arg in "$@"; do
  case "$arg" in
    --skip-editmode) SKIP_EDITMODE=1 ;;
    --mono) MONO=1 ;;
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

echo "== Unity Android verification =="
echo "project: $PROJECT_DIR"
echo "output:  $OUT_DIR"
echo "device:  $(adb shell getprop ro.product.model | tr -d '\r') (Android $(adb shell getprop ro.build.version.release | tr -d '\r'))"
echo

backend_args=()
backend_label="il2cpp"
if [ "$MONO" -eq 1 ]; then
  backend_label="mono"
  settings="$OUT_DIR/mono-test-settings.json"
  printf '{\n  "scriptingBackend": "Mono2x",\n  "architecture": "ARMv7"\n}\n' > "$settings"
  backend_args=(-testSettingsFile "$settings")
  if [ -z "$(adb shell getprop ro.product.cpu.abilist32 | tr -d '\r')" ]; then
    echo "--mono needs a device that runs 32-bit apps; this one does not (ro.product.cpu.abilist32 is empty)." >&2
    exit 2
  fi
fi

if [ "$SKIP_EDITMODE" -eq 0 ]; then
  echo "-- EditMode (Android™ Profile: the build-step assembly is compiled) --"
  run_tests EditMode EditMode "$PROFILE"
  echo
fi

echo "-- device PlayMode ($backend_label, Android™ Profile: AARs copied from dist) --"
run_tests "Android-$backend_label" Android "$PROFILE" ${backend_args[@]+"${backend_args[@]}"}
echo

if [ "$AS_PACKAGE_USER" -eq 1 ]; then
  echo "-- device PlayMode ($backend_label, Test Android™ Profile: as a package user) --"
  run_tests "Android-$backend_label-package-user" Android "$TEST_PROFILE" ${backend_args[@]+"${backend_args[@]}"}
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
