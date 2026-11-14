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
KEEP_INSTALLED=0
KEEP_FLAG=()
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
    --keep-installed) KEEP_INSTALLED=1; KEEP_FLAG=(--keep-installed) ;;
    --approved) APPROVAL="${2:-}"; shift ;;
    --approved=*) APPROVAL="${1#--approved=}" ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

if [ "$KEEP_INSTALLED" -eq 1 ]; then
  if [ -n "$TESTS_ARG" ]; then
    echo "--keep-installed runs exactly keepInstalled of Tools~/IosUiTests/DeviceRequirements.json; --tests is not taken." >&2
    exit 2
  fi
  KNOWN_TESTS="$(python3 -I "$RUNNER" tests "$PROJECT_DIR" --keep-installed)" || exit 2
elif [ "$DEVICE_MODE" -eq 1 ]; then
  KNOWN_TESTS="$(python3 -I "$RUNNER" tests "$PROJECT_DIR" --device)"
else
  KNOWN_TESTS="$(python3 -I "$RUNNER" tests "$PROJECT_DIR")"
fi
if [ -n "$TESTS_ARG" ]; then
  IFS=',' read -r -a TESTS <<< "$TESTS_ARG"
  for t in "${TESTS[@]}"; do
    if ! grep -qxF "$t" <<< "$KNOWN_TESTS"; then
      if python3 -I "$RUNNER" not-moved "$PROJECT_DIR" "$t" >/dev/null; then
        echo "Not moved yet to the C ABI 2.0.0, so not run: $t (Tools~/IosUiTests/DeviceRequirements.json, notMovedYet)" >&2
      elif [ "$DEVICE_MODE" -eq 1 ] && python3 -I "$RUNNER" tests "$PROJECT_DIR" | grep -qxF "$t"; then
        echo "Simulator only, never run on a device: $t (Tools~/IosUiTests/DeviceRequirements.json, simulatorOnly)" >&2
      else
        echo "Not a UI test: $t (the UI tests are listed in Tools~/IosUiTests/DeviceRequirements.json)" >&2
      fi
      exit 2
    fi
  done
else
  TESTS=()
  # An empty list adds no empty name (here-string of "" would give one empty line).
  if [ -n "$KNOWN_TESTS" ]; then
    while IFS= read -r t; do TESTS+=("$t"); done <<< "$KNOWN_TESTS"
  else
    echo "No UI test can run: every UI test class is in notMovedYet (Tools~/IosUiTests/DeviceRequirements.json) or left out of this kind of run." >&2
    exit 2
  fi
fi

if [ "${#TESTS[@]}" -eq 0 ]; then
  echo "No UI tests to run." >&2
  exit 2
fi

if [ "$DEVICE_MODE" -eq 1 ]; then
  if [ "$DRY_RUN" -eq 1 ]; then
    python3 -I "$RUNNER" approve "$PROJECT_DIR" "${KEEP_FLAG[@]}" "${TESTS[@]}"
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
  python3 -I "$RUNNER" check-approval "$PROJECT_DIR" "$APPROVAL" "${KEEP_FLAG[@]}" "${TESTS[@]}" || exit 1
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
# The run lock comes before anything this run would undo: if another run holds it, this one stops
# without its cleanup or restore (keep-installed design v4, 1.2 step 1).
take_run_lock || exit 1
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
  trap 'rm -rf "$OUT_DIR"; release_run_lock' EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  trap 'exit 129' HUP
  if ! select_device "$OUT_DIR" || ! select_team; then
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
  if ! run_lock_held; then
    echo "  stopped: the run lock is no longer held"
    return 1
  fi
  [ "$DEVICE_MODE" -eq 1 ] || return 0
  python3 -I "$RUNNER" guard "$PROJECT_DIR" "$APPROVAL" "$RUN_MARKER" "${KEEP_FLAG[@]}" "${TESTS[@]}" >/dev/null 2>"$OUT_DIR/guard.err" || {
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
READY_FILE=""
DEVICE_TOUCHED=0
CLEANED_UP=0
# Removes what the run installed. On a device it reads back that each app is gone and returns 1 when
# one is not; the main flow does this before printing the result, so a run that leaves an app behind
# fails. The EXIT trap does it again only when the main flow did not get there.
# CLEANED_UP is set only once every app is removed (and, on a device, read back as gone): a signal in the
# middle leaves it unset, so the EXIT trap removes the rest (keep-installed design v4, section 4; Codex KI-02).
clean_up_apps() {
  if [ "$DEVICE_MODE" -eq 1 ]; then
    if [ "$DEVICE_TOUCHED" -eq 1 ]; then
      remove_from_device "${INSTALLED_IDS[@]}" || return 1
    fi
    CLEANED_UP=1
    return 0
  fi
  for id in "${INSTALLED_IDS[@]}"; do remove_app "$id"; done
  CLEANED_UP=1
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
  restore_after_interrupted_unity_step
  [ -n "$READY_FILE" ] && rm -f "$READY_FILE" "$READY_FILE.cancel"
  if [ "$DEVICE_MODE" -eq 1 ]; then
    rm -rf "$OUT_DIR"
  fi
  release_run_lock
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
  begin_unity_step
  run_limited export 1800 600 "$export_log" "$OUT_DIR/harness-export.console.log" \
    "$UNITY_EXE" -batchmode -nographics -projectPath "$PROJECT_DIR" -activeBuildProfile "$PROFILE" -buildTarget iOS \
    -executeMethod JonghyunKim.NativeToolkit.IosUiHarness.Editor.IosUiHarnessBuild.Export \
    -ntkHarnessSdk "$SDK" -ntkHarnessOut "$export_dir" -logFile "$export_log"
  local code=$?
  end_unity_step
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

# install_harness <log> <label>: puts the harness on the device or the simulator.
install_harness() {
  if [ "$DEVICE_MODE" -eq 1 ]; then
    xcrun devicectl device install app --device "$DEVICE_ID" "$HARNESS_APP" > "$1" 2>&1 || { echo "  $2: install failed"; return 1; }
  else
    xcrun simctl install "$SIM_ID" "$HARNESS_APP" > "$1" 2>&1 || { echo "  $2: install failed"; return 1; }
  fi
}

# run_one <Class/test>: reinstall the harness, run the test, judge its result bundle.
run_one() {
  local test="$1" name="${1//\//-}"
  mkdir -p "$OUT_DIR/results"
  rm -rf "$OUT_DIR/results/$name.xcresult" "$OUT_DIR/results/$name.log"
  device_guard || return 1
  DEVICE_TOUCHED=$DEVICE_MODE
  remove_app "$HARNESS_ID"
  install_harness "$OUT_DIR/results/$name.log" "$test" || return 1
  run_test "$test"
}

# run_test <Class/test> [keep]: runs the test on what is installed and judges its result bundle. With
# "keep", the test learns it runs in a keep-installed run (NTK_KEEP_INSTALLED=1 through Xcode's
# TEST_RUNNER_ prefix); otherwise both names are removed from the environment.
run_test() {
  local test="$1" name="${1//\//-}"
  local bundle="$OUT_DIR/results/$name.xcresult" log="$OUT_DIR/results/$name.log"
  local environment=(env -u TEST_RUNNER_NTK_KEEP_INSTALLED -u NTK_KEEP_INSTALLED)
  [ "${2:-}" = "keep" ] && environment=(env -u NTK_KEEP_INSTALLED TEST_RUNNER_NTK_KEEP_INSTALLED=1)
  mkdir -p "$OUT_DIR/results"
  rm -rf "$bundle"
  device_guard || return 1
  local limit
  limit="$(python3 -I "$RUNNER" time-limit "$PROJECT_DIR" "$test")" || limit=600
  run_limited "$test" "$limit" 300 "$log" "$log" \
    "${environment[@]}" "$XCODEBUILD" test-without-building -xctestrun "$XCTESTRUN" -destination "$DESTINATION" \
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

# device_connected: the development connection still works (read only; after a long wait the tunnel can
# be gone and a test would hang). The fields to read are decided on the device's output; for now the
# command must succeed within 60 s.
device_connected() {
  [ "$DEVICE_MODE" -eq 1 ] || return 0
  run_limited connection 60 60 "$OUT_DIR/connection.log" "$OUT_DIR/connection.log" \
    xcrun devicectl device info details --device "$DEVICE_ID" --json-output "$OUT_DIR/details.json" >/dev/null || {
    rm -f "$OUT_DIR/details.json"
    echo "  stopped: the device's development connection did not answer"
    return 1
  }
  rm -f "$OUT_DIR/details.json"
}

# not_run <reason> <test>...: reports the tests a stopped keep-installed run did not run, each a failure.
not_run() {
  local reason="$1" t
  shift
  for t in "$@"; do
    echo "  $t: did not run ($reason)"
    failures=$((failures + 1))
  done
}

# wait_until_ready: the keep-installed run waits (up to 30 minutes) for the readiness marker, a regular
# file named by a random value of this run; <marker>.cancel stops the wait. The Mac stays awake meanwhile.
wait_until_ready() {
  local folder="$HOME/Library/Application Support/unity-native-plugin/test_ios-ready" waited=0
  mkdir -p "$folder"
  READY_FILE="$folder/$(python3 -I -c 'import secrets; print(secrets.token_hex(8))')"
  rm -f "$READY_FILE" "$READY_FILE.cancel"
  caffeinate -i -w $$ &
  echo "  waiting for the user: let only NTK UI Harness through a Focus mode (no people, no time-sensitive"
  echo "  notifications from others), keep the iPhone unlocked; then create the file:"
  echo "    $READY_FILE"
  echo "  (or $READY_FILE.cancel to stop)"
  while [ "$waited" -lt 1800 ]; do
    if [ -e "$READY_FILE.cancel" ]; then
      echo "  cancelled"
      return 1
    fi
    if [ -e "$READY_FILE" ] || [ -L "$READY_FILE" ]; then
      if [ -f "$READY_FILE" ] && [ ! -L "$READY_FILE" ]; then
        echo "  ready after ${waited} s"
        return 0
      fi
      echo "  the ready marker is not a regular file; not accepted"
      return 1
    fi
    sleep 15
    waited=$((waited + 15))
    echo "  waiting ($waited s of 1800 s)"
  done
  echo "  not ready after 30 minutes"
  return 1
}

# run_keep_installed: the keep-installed run (keep-installed design v4, 1.2 steps 4 to 8).
run_keep_installed() {
  local first="${TESTS[0]}" rest=("${TESTS[@]:1}") i
  device_guard || { not_run "the guard stopped the run" "${TESTS[@]}"; return; }
  DEVICE_TOUCHED=$DEVICE_MODE
  if [ "$DEVICE_MODE" -eq 1 ]; then
    if ! remove_from_device "${INSTALLED_IDS[@]}"; then
      not_run "a harness of an earlier run could not be confirmed removed" "${TESTS[@]}"
      return
    fi
  else
    for i in "${INSTALLED_IDS[@]}"; do remove_app "$i"; done
  fi
  device_guard || { not_run "the guard stopped the run" "${TESTS[@]}"; return; }
  mkdir -p "$OUT_DIR/results"
  install_harness "$OUT_DIR/results/install.log" "the harness" || { not_run "the install failed" "${TESTS[@]}"; return; }
  device_connected || { not_run "no development connection" "${TESTS[@]}"; return; }
  if ! run_test "$first"; then
    failures=$((failures + 1))
    not_run "an earlier test failed" "${rest[@]}"
    return
  fi
  if ! wait_until_ready; then
    not_run "not ready" "${rest[@]}"
    return
  fi
  if ! device_guard || ! device_connected; then
    not_run "the guard or the connection stopped the run" "${rest[@]}"
    return
  fi
  for i in "${!rest[@]}"; do
    if ! run_test "${rest[$i]}" keep; then
      failures=$((failures + 1))
      not_run "an earlier test failed" "${rest[@]:$((i + 1))}"
      return
    fi
  done
}

failures=0
if [ "$DEVICE_MODE" -eq 1 ]; then
  RUN_MARKER="$(python3 -I "$RUNNER" use-approval "$PROJECT_DIR" "$APPROVAL" "${KEEP_FLAG[@]}" "${TESTS[@]}")" || exit 1
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
    if [ "$KEEP_INSTALLED" -eq 1 ]; then
      run_keep_installed
    else
      for test in "${TESTS[@]}"; do
        run_one "$test" || failures=$((failures + 1))
      done
    fi
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
