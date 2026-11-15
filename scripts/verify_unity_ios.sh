#!/usr/bin/env bash
#
# Runs the gates an iOS change has to pass, in one command: EditMode, the PlayMode tests inside the
# Editor, then the iOS player tests on the simulator - the only place the P/Invoke path into the iOS
# frameworks executes (artifact/topics/ios-c-abi-2, UI automation design v4, part (A); the stage 2
# spike in the topic README, 6.4).
#
# The player run never opens Simulator.app or Xcode:
#
# - Unity exports the test player and stops there. Assets/NativeToolkitIosPlayerTests/Editor turns
#   the run into "export only" when the Editor gets -ntkIosPlayerSdk, switches the player to the
#   Simulator SDK, arm64 and its own bundle id, and restores those Player Settings afterwards.
#   -doNotReportTestResultsBackToEditor goes on the Editor's command line: the Editor reads it when it
#   writes the test scene. The export log must say remote-sender-count=0.
# - xcodebuild builds the export for the simulator, unsigned; simctl installs and launches it.
# - Launch arguments do not reach the iOS player, so the run id goes in the environment
#   (SIMCTL_CHILD_NTK_RUN). Assets/NativeToolkitIosPlayerTests/Runtime writes the NUnit result to the
#   app container; the player keeps running after the tests, so the script stops it once the result
#   is there, and judges it with scripts/ios_player_results.py: at least one IosPlayer test ran, none
#   failed, none was skipped.
# - The player is uninstalled before the run (the Notification test expects a fresh install) and at
#   the end. A simulator the script booted is shut down at the end.
#
# Every long step has a time limit and a limit on how long its log may stay unchanged, so a step
# that hangs fails instead of waiting forever.
#
# Which build profile is active matters, as on Android (scripts/verify_unity_android.sh): the script
# always passes -activeBuildProfile. The default is the "iOS Profile", whose build steps copy the
# XCFrameworks pinned in Plugins/iOS/VERSION.txt from the sibling native-toolkit dist
# (NATIVE_TOOLKIT_DIST_ROOT overrides it). --as-package-user runs everything with the "Test iOS
# Profile" instead: no NATIVETOOLKIT_ENABLE_BUILD_STEPS, so nothing is copied from the dist and the
# build uses the XCFrameworks committed in Plugins/iOS, which Unity links and embeds by their .meta,
# as in a project that installs the package (artifact/topics/ios-c-abi-2, 3-S design v3, 1.7; before
# the 2.0.0 switch it was refused: the 1.x XCFrameworks were embedded by the build steps only).
#
# A device run needs an approval, the same as the UI tests (scripts/test_ios_ui.sh; native-toolkit's
# agent-rules/device-testing.md): --device --dry-run prints what happens on the device and an approval
# ID without touching it; --device --approved <ID> runs the player tests only (no Editor runs) on the
# one paired iPhone. The approval works once, is checked again with the run's marker before every
# step that touches the device, and the player is removed at the end. The device's identifiers and the
# team are never printed; logs and results go to a temporary folder deleted at the end. On the device
# the run id goes in the launch environment (devicectl --environment-variables) and the result is
# copied out of the app container (devicectl device copy from).
#
# Unity re-serializes some files during a run and leaves the test scene in Assets when the export
# ends the Editor early; the iOS build steps drop the executable bit of the XCFramework binaries they
# copy. The script restores what a run changed under those paths and leaves alone anything that
# already had changes before the run, also when the run is stopped by a signal. --keep-changes skips
# that.
#
# Usage:
#   scripts/verify_unity_ios.sh [--skip-editmode] [--keep-changes] [--as-package-user]
#   scripts/verify_unity_ios.sh --device --dry-run
#   scripts/verify_unity_ios.sh --device --approved <ID> [--as-package-user]
#
#   --skip-editmode skips both Editor runs (EditMode and the Editor's PlayMode) and runs the player
#   tests only.
#
# Environment:
#   UNITY_APP    Path to Unity.app. Defaults to the 6000.4.2f1 install used by this project.
#   XCODE_APP    Path to Xcode.app used for the player build. Defaults to /Applications/Xcode26.3.app.
#   SIM_NAME     Simulator device name. Defaults to "iPhone 16".
#   SIM_RUNTIME  Simulator runtime. Defaults to "iOS 18.3" (closest to the minimum iOS, 18.0).
#   OUT_DIR      Where to put logs and test results. Defaults to a temp dir.

set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_APP="${UNITY_APP:-/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app}"
UNITY_EXE="$UNITY_APP/Contents/MacOS/Unity"
XCODE_APP="${XCODE_APP:-/Applications/Xcode26.3.app}"
XCODEBUILD="$XCODE_APP/Contents/Developer/usr/bin/xcodebuild"
SIM_NAME="${SIM_NAME:-iPhone 16}"
SIM_RUNTIME="${SIM_RUNTIME:-iOS 18.3}"
OUT_DIR="${OUT_DIR:-${TMPDIR:-/tmp}/unity-ios-verify}"
PROFILE="Assets/Settings/Build Profiles/iOS Profile.asset"
PLAYER_ID="com.jonghyunkim.nativetoolkit.playertests"
CATEGORY="IosPlayer"
# The UI driver (artifact/topics/ios-c-abi-2, UI driver design v3): an XCUITest test method next to the
# player that presses the system screens the tests ask for over 127.0.0.1. Its runner installs these two.
UITESTS_DIR="$PROJECT_DIR/Tools~/IosUiTests"
DRIVER_APPS=("com.jonghyunkim.nativetoolkit.uitesthost" "com.jonghyunkim.nativetoolkit.uitests.xctrunner")
DRIVER_TEST="DriverUITests/test_drive"
PLAYER_LIMIT=600
DRIVER_PID=""
DRIVER_PORT=""
DRIVER_XCTESTRUN=""
ALLOW_SKIP=()
SKIP_EDITMODE=0
KEEP_CHANGES=0
# shellcheck source=scripts/ios_script_common.sh
source "$PROJECT_DIR/scripts/ios_script_common.sh"

DEVICE_MODE=0
DRY_RUN=0
APPROVAL=""
RUNNER="$PROJECT_DIR/scripts/ios_ui_runner.py"
APPROVAL_TESTS=("PlayerTests/IosPlayer")
while [ $# -gt 0 ]; do
  case "$1" in
    --skip-editmode) SKIP_EDITMODE=1 ;;
    --keep-changes) KEEP_CHANGES=1 ;;
    --as-package-user)
      PROFILE="Assets/Settings/Build Profiles/Test iOS Profile.asset"
      # That profile's player has no sample scene: the sample-screen tests ignore themselves there.
      ALLOW_SKIP=(--allow-skip IosSampleScreen) ;;
    --device) DEVICE_MODE=1 ;;
    --dry-run) DRY_RUN=1 ;;
    --approved) APPROVAL="${2:-}"; shift ;;
    --approved=*) APPROVAL="${1#--approved=}" ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

if [ "$DEVICE_MODE" -eq 1 ]; then
  if [ "$DRY_RUN" -eq 1 ]; then
    python3 -I "$RUNNER" approve "$PROJECT_DIR" "${APPROVAL_TESTS[@]}"
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
  python3 -I "$RUNNER" check-approval "$PROJECT_DIR" "$APPROVAL" "${APPROVAL_TESTS[@]}" || exit 1
  SKIP_EDITMODE=1
elif [ "$DRY_RUN" -eq 1 ] || [ -n "$APPROVAL" ]; then
  echo "--dry-run and --approved are for device runs (--device)." >&2
  exit 2
fi

if [ ! -x "$UNITY_EXE" ]; then
  echo "Unity not found at: $UNITY_EXE" >&2
  echo "Set UNITY_APP to your Unity.app and run again." >&2
  exit 2
fi
if [ ! -x "$XCODEBUILD" ]; then
  echo "xcodebuild not found at: $XCODEBUILD (set XCODE_APP)" >&2
  exit 2
fi
export DEVELOPER_DIR="$XCODE_APP/Contents/Developer"
# Simulator.app syncs the simulator's pasteboard with the Mac's (native-toolkit agent-rules/device-testing.md).
if pgrep -x Simulator >/dev/null; then
  echo "Simulator.app is open. Quit it and run again (the script uses simctl only)." >&2
  exit 2
fi

# The run lock comes before anything this run would undo: if another run holds it, this one stops
# without its cleanup or restore (keep-installed design v4, 1.2 step 1).
take_run_lock || exit 1
require_clean_ios_plugins
SIM_ID=""
DEVICE_ID=""
DEVICE_UDID=""
TEAM=""
RUN_MARKER=""
if [ "$DEVICE_MODE" -eq 1 ]; then
  # Logs and results of a device run may name the device: a folder deleted at the end.
  OUT_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ntk-ios-player-device.XXXXXX")"
  # Until the full EXIT trap below, an interrupted run still deletes the folder (it can hold the
  # device list with its identifiers).
  trap 'rm -rf "$OUT_DIR"; release_run_lock' EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  trap 'exit 129' HUP
  if ! select_device "$OUT_DIR" || ! select_team; then
    rm -rf "$OUT_DIR"
    exit 2
  fi
else
  SIM_ID="$(find_simulator "$SIM_NAME" "$SIM_RUNTIME")"
  if [ -z "$SIM_ID" ]; then
    echo "Expected exactly one available simulator named '$SIM_NAME' on '$SIM_RUNTIME' (set SIM_NAME / SIM_RUNTIME)." >&2
    exit 2
  fi
fi
mkdir -p "$OUT_DIR"
failures=0
record_side_effects_baseline

report_tests() {
  local label="$1" xml="$2" exit_code="$3"
  if [ ! -f "$xml" ]; then
    echo "  $label: NO RESULT FILE (Unity exited $exit_code; see the log)"
    return 1
  fi
  local counts
  counts="$(grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' "$xml" | head -1)"
  echo "  $label: ${counts:-unreadable}"
  if [ "$exit_code" -ne 0 ]; then
    echo "  $label: Unity exited $exit_code"
    return 1
  fi
  case "$counts" in
    *'total="0" '*|*'passed="0" '*) echo "  $label: no test passed"; return 1 ;;
    *'failed="0" '*) return 0 ;;
    *) return 1 ;;
  esac
}

# run_editor_tests <label> <platform>
run_editor_tests() {
  local label="$1" platform="$2"
  local xml="$OUT_DIR/$label.xml" log="$OUT_DIR/$label.log"
  rm -f "$xml" "$log"
  # -runTests and -quit cannot be combined: Unity would exit before the run finishes.
  run_limited "$label" 3600 900 "$log" "$OUT_DIR/$label.console.log" \
    "$UNITY_EXE" -batchmode -projectPath "$PROJECT_DIR" -activeBuildProfile "$PROFILE" \
    -runTests -testPlatform "$platform" -testResults "$xml" -logFile "$log"
  local code=$?
  report_tests "$label" "$xml" "$code" || failures=$((failures + 1))
  grep -n "error CS" "$log" 2>/dev/null | sort -u -t: -k2 | head -5
}

SIM_BOOTED_BY_US=0
DEVICE_TOUCHED=0
SIM_INSTALLED=0
CLEANED_UP=0
# Removes the player. On a device it reads back that it is gone and returns 1 when it is not; the
# main flow does this before printing the result, so a run that leaves the player behind fails. Only
# what this run installed, as the approval lists it (see scripts/test_ios_ui.sh). The EXIT trap does it
# again only when the main flow did not get there.
# CLEANED_UP is set only once the player is confirmed gone: a signal in the middle leaves it unset, so
# the EXIT trap tries again (keep-installed design v4, section 4; Codex KI-02).
clean_up_player() {
  if [ "$DEVICE_MODE" -eq 1 ]; then
    if [ "$DEVICE_TOUCHED" -eq 1 ]; then
      local id failed=0
      for id in "$PLAYER_ID" "${DRIVER_APPS[@]}"; do
        remove_from_device "$id" || failed=1
      done
      [ "$failed" -eq 0 ] || return 1
    fi
    CLEANED_UP=1
    return 0
  fi
  # Nothing to remove when this run never installed the player (for example, its build failed); a
  # player left by an earlier run is removed before the install. Once installed, the player must be
  # confirmed gone: a failed query is a failure, not "not installed".
  if [ "$SIM_INSTALLED" -eq 1 ]; then
    local id listed
    for id in "$PLAYER_ID" "${DRIVER_APPS[@]}"; do
      xcrun simctl terminate "$SIM_ID" "$id" >/dev/null 2>&1
      xcrun simctl uninstall "$SIM_ID" "$id" >/dev/null 2>&1
    done
    listed=$(xcrun simctl listapps "$SIM_ID" 2>/dev/null) || return 1
    for id in "$PLAYER_ID" "${DRIVER_APPS[@]}"; do
      grep -qF "$id" <<<"$listed" && return 1
    done
  fi
  CLEANED_UP=1
}
on_exit() {
  trap '' INT TERM HUP
  # Unity or xcodebuild may still be running (a signal stopped the script mid-step). Stop them
  # before restoring files they could write again.
  stop_running_step
  stop_driver
  if [ "$CLEANED_UP" -eq 0 ]; then
    clean_up_player || echo "WARNING: the player of this run may still be on the device"
  fi
  if [ "$SIM_BOOTED_BY_US" -eq 1 ]; then
    xcrun simctl shutdown "$SIM_ID" >/dev/null 2>&1
  fi
  restore_after_interrupted_unity_step
  if [ "$DEVICE_MODE" -eq 1 ]; then
    rm -rf "$OUT_DIR"
  fi
  release_run_lock
}

# Before every step that touches the device: this run used the approval, and it still covers the run.
device_guard() {
  if ! run_lock_held; then
    echo "  stopped: the run lock is no longer held"
    return 1
  fi
  python3 -I "$RUNNER" guard "$PROJECT_DIR" "$APPROVAL" "$RUN_MARKER" "${APPROVAL_TESTS[@]}" >/dev/null 2>"$OUT_DIR/guard.err" || {
    echo "  stopped: $(cat "$OUT_DIR/guard.err")"
    return 1
  }
}
trap on_exit EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP

if [ "$SKIP_EDITMODE" -eq 0 ]; then
  # The two Editor runs are one Unity step: nothing is put back between them.
  begin_unity_step
  echo "EditMode:"
  run_editor_tests editmode EditMode
  echo "PlayMode (Editor):"
  run_editor_tests playmode-editor PlayMode
  end_unity_step
fi

# build_driver <destination> <signing...>: builds NtkUiTests for testing; sets DRIVER_XCTESTRUN.
build_driver() {
  local destination="$1" log="$OUT_DIR/uitests-build.log"
  shift
  rm -rf "$OUT_DIR/uitests-dd" "$log"
  run_limited uitests 1200 600 "$log" "$log" \
    "$XCODEBUILD" -project "$UITESTS_DIR/NtkUiTests.xcodeproj" -scheme NtkUiTests \
    -destination "$destination" -derivedDataPath "$OUT_DIR/uitests-dd" "$@" build-for-testing
  if ! grep -q '\*\* TEST BUILD SUCCEEDED \*\*' "$log"; then
    show_build_errors "$log" "$DEVICE_MODE"
    return 1
  fi
  local runs
  runs="$(ls "$OUT_DIR"/uitests-dd/Build/Products/*.xctestrun 2>/dev/null)"
  if [ "$(printf '%s\n' "$runs" | grep -c .)" != "1" ]; then
    echo "  driver build: expected exactly one .xctestrun"
    return 1
  fi
  DRIVER_XCTESTRUN="$runs"
  echo "  driver build: ok"
}

# start_driver <destination>: starts the driver in the background and waits for the port it announces
# (design v3, 4.1). Its whole limit is the player's plus 120 s; no stall limit (it prints nothing while it
# waits). Sets DRIVER_PID and DRIVER_PORT.
start_driver() {
  local destination="$1" log="$OUT_DIR/driver.log" device=0 waited=0
  [ "$DEVICE_MODE" -eq 1 ] && device=1
  rm -rf "$log" "$OUT_DIR/driver.xcresult" "$OUT_DIR/driver.json"
  env TEST_RUNNER_NTK_DRIVER_BUNDLE="$PLAYER_ID" TEST_RUNNER_NTK_DRIVER_LIMIT=$((PLAYER_LIMIT + 120)) \
    TEST_RUNNER_NTK_DRIVER_DEVICE="$device" \
    "$XCODEBUILD" test-without-building -xctestrun "$DRIVER_XCTESTRUN" -destination "$destination" \
    -derivedDataPath "$OUT_DIR/driver-dd" -only-testing:"NtkUiTests/$DRIVER_TEST" -parallel-testing-enabled NO \
    -test-timeouts-enabled NO -collect-test-diagnostics never -resultBundlePath "$OUT_DIR/driver.xcresult" > "$log" 2>&1 &
  DRIVER_PID=$!
  while [ "$waited" -lt 120 ] && kill -0 "$DRIVER_PID" 2>/dev/null; do
    DRIVER_PORT="$(grep -o '\[NTK-UI-DRIVER\] listening [0-9]*' "$log" | head -1 | grep -o '[0-9]*$')"
    [ -n "$DRIVER_PORT" ] && break
    sleep 1; waited=$((waited + 1))
  done
  if [ -z "$DRIVER_PORT" ]; then
    echo "  driver: did not start listening within ${waited}s (see $log)"
    stop_driver
    return 1
  fi
  echo "  driver: listening after ${waited}s"
}

# stop_driver: stops the driver when it still runs (a run that ended without .done, or a signal).
stop_driver() {
  if [ -n "$DRIVER_PID" ] && kill -0 "$DRIVER_PID" 2>/dev/null; then
    stop_tree "$DRIVER_PID"
  fi
  DRIVER_PID=""
}

# finish_driver <player had .done: 0|1>: after .done the driver ends on the player's BYE; wait 60 s for
# it, then judge its one test (design v3, 4.1 and 4.3). Without .done, stop it first and fail.
finish_driver() {
  local had_done="$1" waited=0
  if [ "$had_done" -eq 1 ]; then
    while [ "$waited" -lt 60 ] && kill -0 "$DRIVER_PID" 2>/dev/null; do
      sleep 1; waited=$((waited + 1))
    done
  fi
  if [ -n "$DRIVER_PID" ] && kill -0 "$DRIVER_PID" 2>/dev/null; then
    stop_driver
    echo "  driver: stopped (no BYE$([ "$had_done" -eq 1 ] && echo " within 60 s of the result"))"
    return 1
  fi
  wait "$DRIVER_PID" 2>/dev/null
  DRIVER_PID=""
  if [ ! -d "$OUT_DIR/driver.xcresult" ]; then
    echo "  driver: no result bundle (see $OUT_DIR/driver.log)"
    return 1
  fi
  xcrun xcresulttool get test-results tests --path "$OUT_DIR/driver.xcresult" > "$OUT_DIR/driver.json" 2>/dev/null
  echo "  driver:"
  python3 -I "$RUNNER" results "$OUT_DIR/driver.json" "$DRIVER_TEST"
}

# The iOS player on the simulator or the device. Returns non-zero at the first step that fails.
run_player_tests() {
  local sdk=simulator
  [ "$DEVICE_MODE" -eq 1 ] && sdk=device
  local export_dir="$OUT_DIR/ios-player-xcode" export_log="$OUT_DIR/ios-player-export.log"
  local build_log="$OUT_DIR/ios-player-build.log" derived="$OUT_DIR/ios-player-dd"
  rm -rf "$export_dir" "$export_log" "$build_log" "$derived" "$OUT_DIR/ios-player.xml"

  begin_unity_step
  run_limited export 1800 600 "$export_log" "$OUT_DIR/ios-player-export.console.log" \
    "$UNITY_EXE" -batchmode -projectPath "$PROJECT_DIR" -activeBuildProfile "$PROFILE" \
    -runTests -testPlatform iOS -testCategory "$CATEGORY" -doNotReportTestResultsBackToEditor \
    -ntkIosPlayerSdk "$sdk" -ntkIosPlayerOut "$export_dir" \
    -testResults "$OUT_DIR/ios-player-export-editor.xml" -logFile "$export_log"
  end_unity_step
  grep -n "error CS" "$export_log" 2>/dev/null | sort -u -t: -k2 | head -5
  if ! grep -q '^Build Finished, Result: Success' "$export_log"; then
    echo "  export: Unity did not finish the player build (see $export_log)"
    return 1
  fi
  if ! grep -q '\[NTK-IOS-PLAYER\] remote-sender-count=0 runner-callback-count=[1-9]' "$export_log"; then
    echo "  export: the test scene was not checked, or it still reports to the Editor (see $export_log)"
    return 1
  fi
  if [ ! -d "$export_dir/Unity-iPhone.xcodeproj" ]; then
    echo "  export: no Xcode project at $export_dir"
    return 1
  fi
  echo "  export: ok"

  if [ "$DEVICE_MODE" -eq 1 ]; then
    # Automatic signing without -allowProvisioningUpdates: the team's existing wildcard profile.
    run_limited build 2400 600 "$build_log" "$build_log" \
      "$XCODEBUILD" -project "$export_dir/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
      -destination "generic/platform=iOS" -derivedDataPath "$derived" \
      CODE_SIGN_STYLE=Automatic PROVISIONING_PROFILE_SPECIFIER= DEVELOPMENT_TEAM="$TEAM" build
  else
    run_limited build 2400 600 "$build_log" "$build_log" \
      "$XCODEBUILD" -project "$export_dir/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
      -sdk iphonesimulator -destination "platform=iOS Simulator,id=$SIM_ID" -derivedDataPath "$derived" \
      CODE_SIGN_IDENTITY=- CODE_SIGN_STYLE=Manual DEVELOPMENT_TEAM= PROVISIONING_PROFILE_SPECIFIER= build
  fi
  if ! grep -q '\*\* BUILD SUCCEEDED \*\*' "$build_log"; then
    show_build_errors "$build_log" "$DEVICE_MODE"
    return 1
  fi
  echo "  build: ok"
  local apps
  apps="$(ls -d "$derived"/Build/Products/*/*.app 2>/dev/null)"
  if [ "$(printf '%s\n' "$apps" | grep -c .)" != "1" ]; then
    echo "  build: expected exactly one .app, found: $(printf '%s\n' "$apps" | grep -c .)"
    return 1
  fi
  require_bundle_id "$apps" "$PLAYER_ID" || return 1
  if [ "$DEVICE_MODE" -eq 1 ]; then
    build_driver "generic/platform=iOS" DEVELOPMENT_TEAM="$TEAM" || return 1
    run_on_device "$apps"
    return $?
  fi

  build_driver "generic/platform=iOS Simulator" \
    CODE_SIGN_IDENTITY=- CODE_SIGN_STYLE=Manual DEVELOPMENT_TEAM= PROVISIONING_PROFILE_SPECIFIER= || return 1
  if ! xcrun simctl list devices | grep -F "($SIM_ID) (Booted)" >/dev/null; then
    SIM_BOOTED_BY_US=1
  fi
  run_limited boot 300 300 "$OUT_DIR/ios-player-boot.log" "$OUT_DIR/ios-player-boot.log" \
    xcrun simctl bootstatus "$SIM_ID" -b || return 1
  local app="$apps"
  local id
  for id in "$PLAYER_ID" "${DRIVER_APPS[@]}"; do
    xcrun simctl terminate "$SIM_ID" "$id" >/dev/null 2>&1
    xcrun simctl uninstall "$SIM_ID" "$id" >/dev/null 2>&1
  done
  SIM_INSTALLED=1
  if ! xcrun simctl install "$SIM_ID" "$app"; then
    echo "  install: failed ($app)"
    return 1
  fi

  local run data result done_file
  run="$(uuidgen | tr -d - | tr 'A-F' 'a-f')"
  data="$(xcrun simctl get_app_container "$SIM_ID" "$PLAYER_ID" data)" || return 1
  result="$data/Documents/ntk-player-tests/$run.xml"
  done_file="$data/Documents/ntk-player-tests/$run.done"
  start_driver "platform=iOS Simulator,id=$SIM_ID" || return 1
  SIMCTL_CHILD_NTK_RUN="$run" SIMCTL_CHILD_NTK_DRIVER_PORT="$DRIVER_PORT" \
    xcrun simctl launch --console-pty "$SIM_ID" "$PLAYER_ID" > "$OUT_DIR/ios-player-run.log" 2>&1 &
  local launcher=$! waited=0 had_done=0
  # Stop waiting when the player has ended (it crashed: a finished run keeps it running).
  while [ ! -f "$done_file" ] && [ "$waited" -lt "$PLAYER_LIMIT" ] && kill -0 "$launcher" 2>/dev/null; do
    sleep 2; waited=$((waited + 2))
  done
  [ -f "$done_file" ] && had_done=1
  # The player says BYE at the end of its run; it keeps running until the driver has ended (design v3, 4.3).
  local driver_ok=0
  finish_driver "$had_done" || driver_ok=1
  xcrun simctl terminate "$SIM_ID" "$PLAYER_ID" >/dev/null 2>&1
  kill "$launcher" 2>/dev/null; wait "$launcher" 2>/dev/null
  if [ ! -f "$done_file" ] || [ ! -f "$result" ]; then
    echo "  run: no result after ${waited}s (the player ended, or ${PLAYER_LIMIT}s passed; see $OUT_DIR/ios-player-run.log)"
    return 1
  fi
  cp "$result" "$OUT_DIR/ios-player.xml"
  echo -n "  run: "
  python3 -I "$PROJECT_DIR/scripts/ios_player_results.py" "$OUT_DIR/ios-player.xml" "$CATEGORY" \
    --require IosUiDriver ${ALLOW_SKIP[@]+"${ALLOW_SKIP[@]}"} || return 1
  return "$driver_ok"
}

# run_on_device <app>: install, launch with the run id, copy the result out, stop, judge.
run_on_device() {
  local app="$1" run pid waited=0 code
  local results="$OUT_DIR/device-results"
  mkdir -p "$results"
  device_guard || return 1
  DEVICE_TOUCHED=1
  local id
  for id in "$PLAYER_ID" "${DRIVER_APPS[@]}"; do
    device_guard || return 1
    run_limited "remove before install" 120 120 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
      xcrun devicectl device uninstall app --device "$DEVICE_ID" "$id" >/dev/null
  done
  device_guard || return 1
  run_limited install 300 300 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
    xcrun devicectl device install app --device "$DEVICE_ID" "$app" >/dev/null || { echo "  install: failed"; return 1; }
  device_guard || return 1
  start_driver "platform=iOS,id=$DEVICE_UDID" || return 1
  device_guard || { stop_driver; return 1; }
  run="$(uuidgen | tr -d - | tr 'A-F' 'a-f')"
  run_limited launch 120 120 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
    xcrun devicectl device process launch --device "$DEVICE_ID" \
    --environment-variables "{\"NTK_RUN\": \"$run\", \"NTK_DRIVER_PORT\": \"$DRIVER_PORT\"}" \
    --json-output "$OUT_DIR/launch.json" "$PLAYER_ID" >/dev/null || { echo "  launch: failed"; stop_driver; return 1; }
  pid="$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["result"]["process"]["processIdentifier"])' "$OUT_DIR/launch.json" 2>/dev/null)"
  # Poll for the .done marker (the player keeps running after the tests), at most 600 seconds of real
  # time. Every copy is a device step, so the approval is checked again before each.
  local start now copied=0
  start=$(date +%s)
  while :; do
    now=$(date +%s); waited=$((now - start))
    [ "$waited" -ge "$PLAYER_LIMIT" ] && break
    device_guard || break
    rm -f "$results/$run.done"
    if run_limited "copy" 60 60 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
        xcrun devicectl device copy from --device "$DEVICE_ID" --domain-type appDataContainer \
        --domain-identifier "$PLAYER_ID" --source "Documents/ntk-player-tests/$run.done" \
        --destination "$results/$run.done" >/dev/null && [ -s "$results/$run.done" ]; then
      copied=1
      break
    fi
    sleep 5
  done
  if [ "$copied" -eq 1 ] && device_guard; then
    rm -f "$results/$run.xml"
    run_limited "copy" 60 60 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
      xcrun devicectl device copy from --device "$DEVICE_ID" --domain-type appDataContainer \
      --domain-identifier "$PLAYER_ID" --source "Documents/ntk-player-tests/$run.xml" \
      --destination "$results/$run.xml" >/dev/null || rm -f "$results/$run.xml"
  fi
  # The player says BYE at the end of its run; it keeps running until the driver has ended (design v3, 4.3).
  local driver_ok=0
  finish_driver "$copied" || driver_ok=1
  # Stop the player this run launched (the removal at the end would stop it too).
  if [ -n "$pid" ] && device_guard; then
    run_limited terminate 60 60 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
      xcrun devicectl device process terminate --device "$DEVICE_ID" --pid "$pid" >/dev/null
  fi
  if [ ! -s "$results/$run.xml" ]; then
    echo "  run: no result after ${waited}s"
    return 1
  fi
  echo -n "  run: "
  python3 -I "$PROJECT_DIR/scripts/ios_player_results.py" "$results/$run.xml" "$CATEGORY" \
    --require IosUiDriver ${ALLOW_SKIP[@]+"${ALLOW_SKIP[@]}"} || return 1
  return "$driver_ok"
}

if [ "$DEVICE_MODE" -eq 1 ]; then
  RUN_MARKER="$(python3 -I "$RUNNER" use-approval "$PROJECT_DIR" "$APPROVAL" "${APPROVAL_TESTS[@]}")" || exit 1
  [ -n "$RUN_MARKER" ] || exit 1
  echo "iOS player (device):"
else
  echo "iOS player ($SIM_NAME, $SIM_RUNTIME):"
fi
run_player_tests || failures=$((failures + 1))
if ! clean_up_player; then
  echo "  cleanup: the player could not be confirmed removed"
  failures=$((failures + 1))
fi

restore_after_interrupted_unity_step
[ "$DEVICE_MODE" -eq 0 ] && echo "Logs and results: $OUT_DIR"
if [ "$failures" -ne 0 ]; then
  echo "FAILED: $failures run(s) did not pass"
  exit 1
fi
echo "PASSED"
