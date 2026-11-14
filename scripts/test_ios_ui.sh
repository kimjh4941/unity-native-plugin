#!/usr/bin/env bash
#
# Runs the iOS UI tests: XCUITest presses the system screens (alerts, the share sheet, the notification
# permission and banners) that the UI harness opens through the package's public API
# (artifact/topics/ios-c-abi-2, UI automation design v4, part (B); the stage 2 spike in the topic
# README, 6.4).
#
# - Unity exports the harness (Assets/NativeToolkitIosUiHarness) with its own bundle id, and
#   xcodebuild builds it. The XCUITest project (Tools~/IosUiTests) is built for testing.
# - Before every test the harness is removed and installed again (the Notification test needs a fresh
#   install; a previous test's state must not carry over). Each test runs on its own with
#   test-without-building -only-testing, and its result bundle must hold exactly that test, passed
#   (scripts/ios_ui_runner.py results).
# - Simulator.app and Xcode are never opened. A simulator the script booted is shut down at the end,
#   and the apps it installed are removed.
# - Every long step has a time limit and a limit on how long its log may stay unchanged; a signal
#   stops the running step with its child processes; the files Unity rewrites are restored
#   (scripts/ios_script_common.sh).
#
# A device run needs an approval (native-toolkit's agent-rules/device-testing.md: every device run is
# approved by the user, once):
#
#   scripts/test_ios_ui.sh --device --dry-run [--tests ...]
#       Does not touch the device. Requires the guarded trees to be committed, prints what will happen
#       on the device (the tests, what each does, the apps installed and removed) and an approval ID.
#   scripts/test_ios_ui.sh --device --approved <ID> [--tests ...]   (the same --tests)
#       Runs on the one paired iPhone. The approval works once: the run uses it before building and
#       gets a marker only it knows; before every step that touches the device, the approval must have
#       been used by this run and HEAD and the guarded trees must still match it (ios_ui_runner.py
#       guard). The end of the run removes the apps it installed without that check: they are the
#       three bundle ids the approval lists as "installed, and removed at the end", and leaving them
#       would leave the user's device with something not approved to stay. Nothing is removed when the
#       run never installed anything. The device's identifiers and the team are never printed: build
#       errors of a device run are counted, not shown. Logs and result bundles (which hold screenshots
#       of the device) go to a temporary folder deleted at the end; only the pass/fail lines are
#       printed. Signing is automatic without -allowProvisioningUpdates, with the one team of the
#       unexpired provisioning profiles (the team's existing wildcard profile; stage 1, S-9).
#
# Usage:
#   scripts/test_ios_ui.sh [--tests Class/test_name,...] [--keep-changes]
#   scripts/test_ios_ui.sh --device --dry-run [--tests ...]
#   scripts/test_ios_ui.sh --device --approved <ID> [--tests ...]
#
# Environment:
#   UNITY_APP    Path to Unity.app. Defaults to the 6000.4.2f1 install used by this project.
#   XCODE_APP    Path to Xcode.app. Defaults to /Applications/Xcode26.3.app.
#   SIM_NAME     Simulator device name. Defaults to "iPhone 16".
#   SIM_RUNTIME  Simulator runtime. Defaults to "iOS 18.3".
#   OUT_DIR      Where to put logs and results of a simulator run. Defaults to a temp dir.

set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_APP="${UNITY_APP:-/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app}"
UNITY_EXE="$UNITY_APP/Contents/MacOS/Unity"
XCODE_APP="${XCODE_APP:-/Applications/Xcode26.3.app}"
XCODEBUILD="$XCODE_APP/Contents/Developer/usr/bin/xcodebuild"
SIM_NAME="${SIM_NAME:-iPhone 16}"
SIM_RUNTIME="${SIM_RUNTIME:-iOS 18.3}"
PROFILE="Assets/Settings/Build Profiles/iOS Profile.asset"
UITESTS_DIR="$PROJECT_DIR/Tools~/IosUiTests"
RUNNER="$PROJECT_DIR/scripts/ios_ui_runner.py"
HARNESS_ID="com.jonghyunkim.nativetoolkit.uiharness"
INSTALLED_IDS=("$HARNESS_ID" "com.jonghyunkim.nativetoolkit.uitesthost" "com.jonghyunkim.nativetoolkit.uitests.xctrunner")
KEEP_CHANGES=0
DEVICE_MODE=0
DRY_RUN=0
APPROVAL=""
TESTS_ARG=""
# shellcheck source=scripts/ios_script_common.sh
source "$PROJECT_DIR/scripts/ios_script_common.sh"

while [ $# -gt 0 ]; do
  case "$1" in
    --tests) TESTS_ARG="${2:-}"; shift ;;
    --tests=*) TESTS_ARG="${1#--tests=}" ;;
    --keep-changes) KEEP_CHANGES=1 ;;
    --device) DEVICE_MODE=1 ;;
    --dry-run) DRY_RUN=1 ;;
    --approved) APPROVAL="${2:-}"; shift ;;
    --approved=*) APPROVAL="${1#--approved=}" ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

if [ "$DEVICE_MODE" -eq 1 ]; then
  KNOWN_TESTS="$(python3 -I "$RUNNER" tests "$PROJECT_DIR" --device)"
else
  KNOWN_TESTS="$(python3 -I "$RUNNER" tests "$PROJECT_DIR")"
fi
if [ -n "$TESTS_ARG" ]; then
  IFS=',' read -r -a TESTS <<< "$TESTS_ARG"
  for t in "${TESTS[@]}"; do
    if ! grep -qxF "$t" <<< "$KNOWN_TESTS"; then
      if [ "$DEVICE_MODE" -eq 1 ] && python3 -I "$RUNNER" tests "$PROJECT_DIR" | grep -qxF "$t"; then
        echo "Simulator only, never run on a device: $t (Tools~/IosUiTests/DeviceRequirements.json, simulatorOnly)" >&2
      else
        echo "Not a UI test: $t (the UI tests are listed in Tools~/IosUiTests/DeviceRequirements.json)" >&2
      fi
      exit 2
    fi
  done
else
  TESTS=()
  while IFS= read -r t; do TESTS+=("$t"); done <<< "$KNOWN_TESTS"
fi

if [ "${#TESTS[@]}" -eq 0 ]; then
  echo "No UI tests to run." >&2
  exit 2
fi

if [ "$DEVICE_MODE" -eq 1 ]; then
  if [ "$DRY_RUN" -eq 1 ]; then
    python3 -I "$RUNNER" approve "$PROJECT_DIR" "${TESTS[@]}"
    exit $?
  fi
  if [ -z "$APPROVAL" ]; then
    echo "A device run needs --dry-run first, then --approved <ID> once the user has approved the list." >&2
    exit 2
  fi
  if [ "$KEEP_CHANGES" -eq 1 ]; then
    echo "--keep-changes cannot be used with a device run: the guarded trees are checked again before every device step." >&2
    exit 2
  fi
  python3 -I "$RUNNER" check-approval "$PROJECT_DIR" "$APPROVAL" "${TESTS[@]}" || exit 1
elif [ "$DRY_RUN" -eq 1 ] || [ -n "$APPROVAL" ]; then
  echo "--dry-run and --approved are for device runs (--device)." >&2
  exit 2
fi

if [ ! -x "$UNITY_EXE" ]; then
  echo "Unity not found at: $UNITY_EXE (set UNITY_APP)" >&2
  exit 2
fi
if [ ! -x "$XCODEBUILD" ]; then
  echo "xcodebuild not found at: $XCODEBUILD (set XCODE_APP)" >&2
  exit 2
fi
export DEVELOPER_DIR="$XCODE_APP/Contents/Developer"
if pgrep -x Simulator >/dev/null; then
  echo "Simulator.app is open. Quit it and run again (the script uses simctl only)." >&2
  exit 2
fi
require_clean_ios_plugins

DEVICE_ID=""
DEVICE_UDID=""
SIM_ID=""
TEAM=""
RUN_MARKER=""
if [ "$DEVICE_MODE" -eq 1 ]; then
  # Logs and result bundles of a device run hold the device's screens: a folder deleted at the end.
  OUT_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ntk-ios-ui-device.XXXXXX")"
  # Until the full EXIT trap below, an interrupted run still deletes the folder (it can hold the
  # device list with its identifiers).
  trap 'rm -rf "$OUT_DIR"' EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  trap 'exit 129' HUP
  if ! select_device "$OUT_DIR" || ! select_team; then
    rm -rf "$OUT_DIR"
    exit 2
  fi
else
  OUT_DIR="${OUT_DIR:-${TMPDIR:-/tmp}/unity-ios-ui-test}"
  SIM_ID="$(find_simulator "$SIM_NAME" "$SIM_RUNTIME")"
  if [ -z "$SIM_ID" ]; then
    echo "Expected exactly one available simulator named '$SIM_NAME' on '$SIM_RUNTIME' (set SIM_NAME / SIM_RUNTIME)." >&2
    exit 2
  fi
fi
mkdir -p "$OUT_DIR"
record_side_effects_baseline

# Before every step that touches the device: this run used the approval, and it still covers the run.
device_guard() {
  [ "$DEVICE_MODE" -eq 1 ] || return 0
  python3 -I "$RUNNER" guard "$PROJECT_DIR" "$APPROVAL" "$RUN_MARKER" "${TESTS[@]}" >/dev/null 2>"$OUT_DIR/guard.err" || {
    echo "  stopped: $(cat "$OUT_DIR/guard.err")"
    return 1
  }
}

remove_app() {
  if [ "$DEVICE_MODE" -eq 1 ]; then
    run_limited "remove $1" 120 120 "$OUT_DIR/remove.log" "$OUT_DIR/remove.log" \
      xcrun devicectl device uninstall app --device "$DEVICE_ID" "$1" >/dev/null
  else
    xcrun simctl terminate "$SIM_ID" "$1" >/dev/null 2>&1
    xcrun simctl uninstall "$SIM_ID" "$1" >/dev/null 2>&1
  fi
}

SIM_BOOTED_BY_US=0
DEVICE_TOUCHED=0
CLEANED_UP=0
# Removes what the run installed. On a device it reads back that each app is gone and returns 1 when
# one is not; the main flow does this before printing the result, so a run that leaves an app behind
# fails. The EXIT trap does it again only when the main flow did not get there.
clean_up_apps() {
  CLEANED_UP=1
  if [ "$DEVICE_MODE" -eq 1 ]; then
    [ "$DEVICE_TOUCHED" -eq 1 ] || return 0
    remove_from_device "${INSTALLED_IDS[@]}"
    return $?
  fi
  for id in "${INSTALLED_IDS[@]}"; do remove_app "$id"; done
}
on_exit() {
  trap '' INT TERM HUP
  stop_running_step
  if [ "$CLEANED_UP" -eq 0 ]; then
    clean_up_apps || echo "WARNING: an app of this run may still be on the device"
  fi
  if [ "$SIM_BOOTED_BY_US" -eq 1 ]; then
    xcrun simctl shutdown "$SIM_ID" >/dev/null 2>&1
  fi
  restore_side_effects
  if [ "$DEVICE_MODE" -eq 1 ]; then
    rm -rf "$OUT_DIR"
  fi
}
trap on_exit EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP

if [ "$DEVICE_MODE" -eq 1 ]; then
  SDK=device
  SIGNING=(DEVELOPMENT_TEAM="$TEAM")
  DESTINATION="platform=iOS,id=$DEVICE_UDID"
  GENERIC="generic/platform=iOS"
else
  SDK=simulator
  SIGNING=(CODE_SIGN_IDENTITY=- CODE_SIGN_STYLE=Manual DEVELOPMENT_TEAM= PROVISIONING_PROFILE_SPECIFIER=)
  DESTINATION="platform=iOS Simulator,id=$SIM_ID"
  GENERIC="generic/platform=iOS Simulator"
fi

build_all() {
  local export_dir="$OUT_DIR/harness-xcode" export_log="$OUT_DIR/harness-export.log"
  rm -rf "$export_dir" "$export_log" "$OUT_DIR/harness-dd" "$OUT_DIR/uitests-dd"
  echo "Harness export ($SDK):"
  run_limited export 1800 600 "$export_log" "$OUT_DIR/harness-export.console.log" \
    "$UNITY_EXE" -batchmode -nographics -projectPath "$PROJECT_DIR" -activeBuildProfile "$PROFILE" -buildTarget iOS \
    -executeMethod JonghyunKim.NativeToolkit.IosUiHarness.Editor.IosUiHarnessBuild.Export \
    -ntkHarnessSdk "$SDK" -ntkHarnessOut "$export_dir" -logFile "$export_log"
  local code=$?
  restore_side_effects
  if [ "$code" -ne 0 ] || ! grep -q '\[NTK-UI-HARNESS-BUILD\] result=Succeeded' "$export_log"; then
    echo "  export: failed (Unity exited $code; see $export_log)"
    grep -n "error CS" "$export_log" 2>/dev/null | sort -u -t: -k2 | head -5
    return 1
  fi
  echo "  export: ok"

  echo "Harness build:"
  run_limited build 2400 600 "$OUT_DIR/harness-build.log" "$OUT_DIR/harness-build.log" \
    "$XCODEBUILD" -project "$export_dir/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
    -destination "$GENERIC" -derivedDataPath "$OUT_DIR/harness-dd" "${SIGNING[@]}" build
  if ! grep -q '\*\* BUILD SUCCEEDED \*\*' "$OUT_DIR/harness-build.log"; then
    show_build_errors "$OUT_DIR/harness-build.log" "$DEVICE_MODE"
    return 1
  fi
  local apps
  apps="$(ls -d "$OUT_DIR"/harness-dd/Build/Products/*/*.app 2>/dev/null)"
  if [ "$(printf '%s\n' "$apps" | grep -c .)" != "1" ]; then
    echo "  build: expected exactly one .app, found: ${apps:-none}"
    return 1
  fi
  HARNESS_APP="$apps"
  require_bundle_id "$HARNESS_APP" "$HARNESS_ID" || return 1
  echo "  build: ok"

  echo "UI tests build:"
  run_limited uitests 1200 600 "$OUT_DIR/uitests-build.log" "$OUT_DIR/uitests-build.log" \
    "$XCODEBUILD" -project "$UITESTS_DIR/NtkUiTests.xcodeproj" -scheme NtkUiTests \
    -destination "$GENERIC" -derivedDataPath "$OUT_DIR/uitests-dd" "${SIGNING[@]}" build-for-testing
  if ! grep -q '\*\* TEST BUILD SUCCEEDED \*\*' "$OUT_DIR/uitests-build.log"; then
    show_build_errors "$OUT_DIR/uitests-build.log" "$DEVICE_MODE"
    return 1
  fi
  local runs
  runs="$(ls "$OUT_DIR"/uitests-dd/Build/Products/*.xctestrun 2>/dev/null)"
  if [ "$(printf '%s\n' "$runs" | grep -c .)" != "1" ]; then
    echo "  build: expected exactly one .xctestrun, found: ${runs:-none}"
    return 1
  fi
  XCTESTRUN="$runs"
  echo "  build: ok"
}

# run_one <Class/test>: reinstall the harness, run the test, judge its result bundle.
run_one() {
  local test="$1" name="${1//\//-}"
  local bundle="$OUT_DIR/results/$name.xcresult" log="$OUT_DIR/results/$name.log"
  mkdir -p "$OUT_DIR/results"
  rm -rf "$bundle" "$log"
  device_guard || return 1
  DEVICE_TOUCHED=$DEVICE_MODE
  remove_app "$HARNESS_ID"
  if [ "$DEVICE_MODE" -eq 1 ]; then
    xcrun devicectl device install app --device "$DEVICE_ID" "$HARNESS_APP" > "$log" 2>&1 || { echo "  $test: install failed"; return 1; }
  else
    xcrun simctl install "$SIM_ID" "$HARNESS_APP" > "$log" 2>&1 || { echo "  $test: install failed"; return 1; }
  fi
  device_guard || return 1
  run_limited "$test" 600 300 "$log" "$log" \
    "$XCODEBUILD" test-without-building -xctestrun "$XCTESTRUN" -destination "$DESTINATION" \
    -derivedDataPath "$OUT_DIR/test-dd" -only-testing:"NtkUiTests/$test" -parallel-testing-enabled NO \
    -collect-test-diagnostics never -resultBundlePath "$bundle"
  local code=$?
  if [ ! -d "$bundle" ]; then
    echo "  $test: no result bundle (xcodebuild exited $code)"
    return 1
  fi
  xcrun xcresulttool get test-results tests --path "$bundle" > "$OUT_DIR/results/$name.json" 2>/dev/null
  python3 -I "$RUNNER" results "$OUT_DIR/results/$name.json" "$test" || return 1
  if [ "$code" -ne 0 ]; then
    echo "  $test: xcodebuild exited $code"
    return 1
  fi
}

failures=0
if [ "$DEVICE_MODE" -eq 1 ]; then
  RUN_MARKER="$(python3 -I "$RUNNER" use-approval "$PROJECT_DIR" "$APPROVAL" "${TESTS[@]}")" || exit 1
  [ -n "$RUN_MARKER" ] || exit 1
fi
if build_all; then
  echo "UI tests ($([ "$DEVICE_MODE" -eq 1 ] && echo "device" || echo "$SIM_NAME, $SIM_RUNTIME")):"
  if [ "$DEVICE_MODE" -eq 0 ]; then
    if ! xcrun simctl list devices | grep -F "($SIM_ID) (Booted)" >/dev/null; then
      SIM_BOOTED_BY_US=1
    fi
    run_limited boot 300 300 "$OUT_DIR/boot.log" "$OUT_DIR/boot.log" xcrun simctl bootstatus "$SIM_ID" -b || failures=$((failures + 1))
  fi
  if [ "$failures" -eq 0 ]; then
    for test in "${TESTS[@]}"; do
      run_one "$test" || failures=$((failures + 1))
    done
  fi
else
  failures=$((failures + 1))
fi

if ! clean_up_apps; then
  echo "  cleanup: an app of this run could not be confirmed removed"
  failures=$((failures + 1))
fi
if [ "$DEVICE_MODE" -eq 0 ]; then
  echo "Logs and results: $OUT_DIR"
fi
if [ "$failures" -ne 0 ]; then
  echo "FAILED: $failures step(s) did not pass"
  exit 1
fi
echo "PASSED"
