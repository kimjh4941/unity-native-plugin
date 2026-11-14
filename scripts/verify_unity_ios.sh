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
# (NATIVE_TOOLKIT_DIST_ROOT overrides it). --as-package-user would use the "Test iOS Profile" (no
# build steps), but with the 1.x XCFrameworks a build without the build steps does not embed them,
# so the player cannot start (review v2 of the plan, N-9; spike 6.4): it is refused until the 2.0.0
# switch (stage 3-S).
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
#   scripts/verify_unity_ios.sh [--skip-editmode] [--keep-changes]
#   scripts/verify_unity_ios.sh --device --dry-run
#   scripts/verify_unity_ios.sh --device --approved <ID>
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
      echo "--as-package-user is not available yet: with the 1.x XCFrameworks a build without the build" >&2
      echo "steps does not embed them (artifact/topics/ios-c-abi-2, README 6.4). It comes with stage 3-S." >&2
      exit 2 ;;
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
      remove_from_device "$PLAYER_ID" || return 1
    fi
    CLEANED_UP=1
    return 0
  fi
  # Nothing to remove when this run never installed the player (for example, its build failed); a
  # player left by an earlier run is removed before the install. Once installed, the player must be
  # confirmed gone: a failed query is a failure, not "not installed".
  if [ "$SIM_INSTALLED" -eq 1 ]; then
    xcrun simctl terminate "$SIM_ID" "$PLAYER_ID" >/dev/null 2>&1
    xcrun simctl uninstall "$SIM_ID" "$PLAYER_ID" >/dev/null 2>&1
    local listed
    listed=$(xcrun simctl listapps "$SIM_ID" 2>/dev/null) || return 1
    grep -qF "$PLAYER_ID" <<<"$listed" && return 1
  fi
  CLEANED_UP=1
}
on_exit() {
  trap '' INT TERM HUP
  # Unity or xcodebuild may still be running (a signal stopped the script mid-step). Stop them
  # before restoring files they could write again.
  stop_running_step
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
    run_on_device "$apps"
    return $?
  fi

  if ! xcrun simctl list devices | grep -F "($SIM_ID) (Booted)" >/dev/null; then
    SIM_BOOTED_BY_US=1
  fi
  run_limited boot 300 300 "$OUT_DIR/ios-player-boot.log" "$OUT_DIR/ios-player-boot.log" \
    xcrun simctl bootstatus "$SIM_ID" -b || return 1
  local app="$apps"
  xcrun simctl terminate "$SIM_ID" "$PLAYER_ID" >/dev/null 2>&1
  xcrun simctl uninstall "$SIM_ID" "$PLAYER_ID" >/dev/null 2>&1
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
  SIMCTL_CHILD_NTK_RUN="$run" xcrun simctl launch --console-pty "$SIM_ID" "$PLAYER_ID" > "$OUT_DIR/ios-player-run.log" 2>&1 &
  local launcher=$! waited=0
  # Stop waiting when the player has ended (it crashed: a finished run keeps it running).
  while [ ! -f "$done_file" ] && [ "$waited" -lt 600 ] && kill -0 "$launcher" 2>/dev/null; do
    sleep 2; waited=$((waited + 2))
  done
  xcrun simctl terminate "$SIM_ID" "$PLAYER_ID" >/dev/null 2>&1
  kill "$launcher" 2>/dev/null; wait "$launcher" 2>/dev/null
  if [ ! -f "$done_file" ] || [ ! -f "$result" ]; then
    echo "  run: no result after ${waited}s (the player ended, or 600s passed; see $OUT_DIR/ios-player-run.log)"
    return 1
  fi
  cp "$result" "$OUT_DIR/ios-player.xml"
  echo -n "  run: "
  python3 -I "$PROJECT_DIR/scripts/ios_player_results.py" "$OUT_DIR/ios-player.xml" "$CATEGORY"
}

# run_on_device <app>: install, launch with the run id, copy the result out, stop, judge.
run_on_device() {
  local app="$1" run pid waited=0 code
  local results="$OUT_DIR/device-results"
  mkdir -p "$results"
  device_guard || return 1
  DEVICE_TOUCHED=1
  run_limited "remove before install" 120 120 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
    xcrun devicectl device uninstall app --device "$DEVICE_ID" "$PLAYER_ID" >/dev/null
  device_guard || return 1
  run_limited install 300 300 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
    xcrun devicectl device install app --device "$DEVICE_ID" "$app" >/dev/null || { echo "  install: failed"; return 1; }
  device_guard || return 1
  run="$(uuidgen | tr -d - | tr 'A-F' 'a-f')"
  run_limited launch 120 120 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
    xcrun devicectl device process launch --device "$DEVICE_ID" --environment-variables "{\"NTK_RUN\": \"$run\"}" \
    --json-output "$OUT_DIR/launch.json" "$PLAYER_ID" >/dev/null || { echo "  launch: failed"; return 1; }
  pid="$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["result"]["process"]["processIdentifier"])' "$OUT_DIR/launch.json" 2>/dev/null)"
  # Poll for the .done marker (the player keeps running after the tests), at most 600 seconds of real
  # time. Every copy is a device step, so the approval is checked again before each.
  local start now copied=0
  start=$(date +%s)
  while :; do
    now=$(date +%s); waited=$((now - start))
    [ "$waited" -ge 600 ] && break
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
  python3 -I "$PROJECT_DIR/scripts/ios_player_results.py" "$results/$run.xml" "$CATEGORY"
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
