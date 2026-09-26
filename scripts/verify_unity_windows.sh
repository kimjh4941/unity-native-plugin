#!/usr/bin/env bash
#
# Runs every gate a Windows change has to pass, in one command.
#
# It exists because two of the three used to be run alone. EditMode and PlayMode both run with
# UNITY_EDITOR defined, so neither of them ever compiles the code behind
# `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` - the P/Invoke declarations, the callbacks, the
# apartment probe. A rename once left a stale `nameof` in there and three review rounds reported
# "0 compile errors" over a player build that did not compile at all.
#
# The build steps mutate the working tree: PreBuildProcessor places the native DLL named by
# Plugins/Windows/VERSION.txt, and Unity may re-serialize settings and assets (render pipeline
# settings, the Windows build profile - sometimes only their line endings). The player-test
# step is a development build, so it also swaps unity-windows-native-toolkit.dll for
# unity-windows-native-toolkit-debug.dll. None of this belongs in a commit, so the script puts it
# back at the end: it restores or removes what the run changed under those locations, and leaves
# alone any file that already had changes before the run, so uncommitted work there is never
# thrown away. --keep-changes skips the cleanup when the build output itself is what you want.
#
# The player-test step runs PlayMode tests on a Windows player (layer 2b), the only place the
# P/Invoke path actually executes, and then reads the system clipboard from outside Unity (a first
# layer 3 check). It needs an interactive desktop session: the clipboard and the player window do not
# work from a service account or a headless agent. --skip-player-tests leaves it out.
#
# Player tests in the Destructive category change what the developer keeps in clipboard history
# (Clear Unpinned wipes every unpinned item), so they are left out unless --include-destructive is
# given. Use it on a machine whose clipboard history nobody needs.
#
# Usage:
#   scripts/verify_unity_windows.sh [--skip-build] [--skip-player-tests] [--include-destructive] [--keep-changes]
#
# Environment:
#   UNITY_EXE   Path to Unity.exe. Defaults to the 6000.4.2f1 install used by this project.
#   OUT_DIR     Where to put logs, test results and the player build. Defaults to a temp dir.

set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EXE="${UNITY_EXE:-/d/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe}"
OUT_DIR="${OUT_DIR:-${TMPDIR:-/tmp}/unity-windows-verify}"
SKIP_BUILD=0
SKIP_PLAYER_TESTS=0
KEEP_CHANGES=0
INCLUDE_DESTRUCTIVE=0

for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=1 ;;
    --skip-player-tests) SKIP_PLAYER_TESTS=1 ;;
    --include-destructive) INCLUDE_DESTRUCTIVE=1 ;;
    --keep-changes) KEEP_CHANGES=1 ;;
    *) echo "unknown argument: $arg" >&2; exit 2 ;;
  esac
done

if [ ! -x "$UNITY_EXE" ]; then
  echo "Unity not found at: $UNITY_EXE" >&2
  echo "Set UNITY_EXE to your Unity.exe and run again." >&2
  exit 2
fi

mkdir -p "$OUT_DIR"
failures=0

# Where Unity and PreBuildProcessor are known to write during a run. Cleanup looks nowhere else, so
# a file edited elsewhere while the run is going (it takes a quarter of an hour) is not touched.
SIDE_EFFECT_PATHS=(
  "Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows"
  "Assets/Settings"
  "Assets/TextMesh Pro"
  "ProjectSettings"
  # Regenerated when a test file is added; Unity only reorders its project list.
  "unity-native-plugin.slnx"
)

# Prints "<XY>\t<path>" for every change under SIDE_EFFECT_PATHS. -z keeps paths with spaces and
# "&" unquoted; a rename or copy carries its old path as an extra entry, which is read and dropped.
changed_paths() {
  git -C "$PROJECT_DIR" status --porcelain=v1 -z --untracked-files=all -- "${SIDE_EFFECT_PATHS[@]}" |
    while IFS= read -r -d '' entry; do
      local status="${entry:0:2}" path="${entry:3}"
      case "$status" in R*|C*) IFS= read -r -d '' _ ;; esac
      printf '%s\t%s\n' "$status" "$path"
    done
}

# What was already changed before the run. Cleanup leaves these alone.
CHANGED_BEFORE="$(changed_paths)"

# Prints the counts from an NUnit result file, or says why it cannot.
report_tests() {
  local label="$1" xml="$2" exit_code="$3"
  if [ ! -f "$xml" ]; then
    echo "  $label: NO RESULT FILE (Unity exited $exit_code; see the log)"
    return 1
  fi
  local counts
  # skipped is shown because a case whose precondition is not met is skipped, not failed, and a
  # run that skipped its only interesting case must not read like a clean pass.
  counts="$(grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' "$xml" | head -1)"
  echo "  $label: ${counts:-unreadable}"
  case "$counts" in
    *'failed="0" '*) return 0 ;;
    *) return 1 ;;
  esac
}

run_tests() {
  local platform="$1"
  local xml="$OUT_DIR/${platform}.xml"
  local log="$OUT_DIR/${platform}.log"
  rm -f "$xml" "$log"
  # -runTests and -quit cannot be combined: Unity would exit before the run finishes and write
  # no result file at all.
  "$UNITY_EXE" -batchmode -nographics -projectPath "$PROJECT_DIR" \
    -runTests -testPlatform "$platform" -buildTarget Win64 \
    -testResults "$xml" -logFile "$log" >/dev/null 2>&1
  local code=$?
  report_tests "$platform" "$xml" "$code" || failures=$((failures + 1))
  grep -n "error CS" "$log" | sort -u -t: -k2 | head -5
}

echo "== Unity Windows verification =="
echo "project: $PROJECT_DIR"
echo "output:  $OUT_DIR"
echo

echo "-- tests --"
run_tests EditMode
run_tests PlayMode
echo

if [ "$SKIP_BUILD" -eq 1 ]; then
  echo "-- player build: skipped --"
else
  echo "-- player build --"
  # The point of this step: it is the only one that compiles the native branch.
  BUILD_DIR="$OUT_DIR/player"
  BUILD_LOG="$OUT_DIR/player.log"
  rm -rf "$BUILD_DIR"; mkdir -p "$BUILD_DIR"
  "$UNITY_EXE" -batchmode -nographics -quit -projectPath "$PROJECT_DIR" \
    -buildWindows64Player "$BUILD_DIR/NativeToolkit.exe" -logFile "$BUILD_LOG" >/dev/null 2>&1
  build_code=$?
  if [ "$build_code" -eq 0 ] && [ -f "$BUILD_DIR/NativeToolkit.exe" ]; then
    echo "  Win64 player: built"
  else
    echo "  Win64 player: FAILED (exit $build_code)"
    grep -n "error CS" "$BUILD_LOG" | sort -u -t: -k2 | head -10
    failures=$((failures + 1))
  fi
fi

echo
if [ "$SKIP_PLAYER_TESTS" -eq 1 ]; then
  echo "-- player tests: skipped --"
else
  echo "-- player tests (layer 2b) and system clipboard (layer 3) --"
  PT_XML="$OUT_DIR/StandaloneWindows64.xml"
  PT_LOG="$OUT_DIR/StandaloneWindows64.log"
  PT_SOURCE="$PROJECT_DIR/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardPlayerTests.cs"
  rm -f "$PT_XML" "$PT_LOG"

  # The value the test writes is read from the test itself, so the two cannot drift apart.
  expected="$(sed -n 's/.*const string SampleText = "\([^"]*\)".*/\1/p' "$PT_SOURCE" | head -1)"

  # Put a value no run has written on the clipboard first. Otherwise a sample value left there by an
  # earlier run would pass for this one even if this run never wrote anything.
  sentinel="ntk-verify-sentinel-$$-$RANDOM"
  powershell.exe -NoProfile -Command "Set-Clipboard -Value '$sentinel'" >/dev/null 2>&1

  # The test player listens for the editor, and Windows asks about the firewall the first time a
  # program listens without a rule. The Test Framework's default location has a fresh directory per
  # run (Temp/UnityTempFile-<guid>), so every run was a new program and every run stopped on that
  # dialog. -buildPlayerPath pins the executable, so a single rule settles it for good. The rule is a
  # Block: the player only needs to reach the editor on this machine, not the network.
  PT_PLAYER_DIR="$PROJECT_DIR/Temp/NativeToolkitTestPlayer"
  PT_PLAYER_EXE="$(cygpath -w "$PT_PLAYER_DIR/PlayerWithTests/PlayerWithTests.exe")"
  if ! powershell.exe -NoProfile -Command \
      "if (Get-NetFirewallApplicationFilter -Program '$PT_PLAYER_EXE' -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }" \
      >/dev/null 2>&1; then
    echo "  note: no firewall rule for the test player, so Windows will stop this run with a dialog."
    echo "        Once, from an elevated PowerShell:"
    echo "        New-NetFirewallRule -DisplayName 'NativeToolkit test player' -Direction Inbound -Action Block -Profile Any -Program '$PT_PLAYER_EXE'"
  fi

  # The Test Framework does not get its player to quit on a command-line run: at the end it sends
  # the quit message and closes the connection in the same breath, the editor then exits, and the
  # message never arrives. The player stays up on its results screen, announcing itself to editors,
  # one more per run (RemotePlayerTestController.RunFinished). Only players on the pinned path are
  # stopped, so nothing else running on the machine is touched.
  stop_test_players() {
    powershell.exe -NoProfile -Command \
      "Get-Process PlayerWithTests -ErrorAction SilentlyContinue | Where-Object { \$_.Path -eq '$PT_PLAYER_EXE' } | ForEach-Object { Stop-Process -Id \$_.Id -Force; \$_.Id }" \
      2>/dev/null | tr -d '\r'
  }
  leftover="$(stop_test_players)"
  if [ -n "$leftover" ]; then
    echo "  note: stopped $(printf '%s\n' "$leftover" | wc -l) test player(s) left running by an earlier run"
  fi

  # The unknown-id restore case needs clipboard history (Win+V) on; with it off the OS answers
  # HistoryDisabled instead, and the test skips itself rather than fail. Say so up front.
  history="$(powershell.exe -NoProfile -Command "(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Clipboard' -ErrorAction SilentlyContinue).EnableClipboardHistory" 2>/dev/null | tr -d '\r')"
  if [ "$history" != "1" ]; then
    echo "  note: clipboard history (Win+V) is off, so the unknown-id restore case will be skipped."
    echo "        Settings > System > Clipboard > Clipboard history."
  fi

  # Destructive tests are excluded with the Test Framework's "!" category filter. The category name
  # is read from the test, like the sample value, so a rename cannot quietly let them through.
  destructive="$(sed -n 's/.*const string DestructiveCategory = "\([^"]*\)".*/\1/p' "$PT_SOURCE" | head -1)"
  category_args=()
  if [ "$INCLUDE_DESTRUCTIVE" -eq 1 ]; then
    echo "  note: --include-destructive: this run clears the unpinned clipboard history on this machine."
  elif [ -z "$destructive" ]; then
    echo "  StandaloneWindows64: CANNOT RUN (no DestructiveCategory in $PT_SOURCE, so nothing could be excluded)"
    failures=$((failures + 1))
  else
    category_args=(-testCategory "!$destructive")
  fi

  # No -nographics: the player opens a window and owns the clipboard from its main thread. This
  # step has been run without it; it has not been tried with it.
  if [ "$INCLUDE_DESTRUCTIVE" -eq 1 ] || [ -n "$destructive" ]; then
    "$UNITY_EXE" -batchmode -projectPath "$PROJECT_DIR" \
      -runTests -testPlatform StandaloneWindows64 -buildPlayerPath "$PT_PLAYER_DIR" \
      "${category_args[@]}" \
      -testResults "$PT_XML" -logFile "$PT_LOG" >/dev/null 2>&1
  fi
  pt_code=$?
  # The editor has the results by the time it exits, so this player has nothing left to do.
  stop_test_players >/dev/null
  report_tests "StandaloneWindows64" "$PT_XML" "$pt_code" || failures=$((failures + 1))

  # Block C turns clipboard history off and puts it back in its teardown. A player that died in
  # between would leave the developer's history off, so the setting read before the run is put
  # back here too, and a run that needed it counts as failed.
  history_after="$(powershell.exe -NoProfile -Command "(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Clipboard' -ErrorAction SilentlyContinue).EnableClipboardHistory" 2>/dev/null | tr -d '\r')"
  if [ "$history_after" != "$history" ]; then
    if [ -z "$history" ]; then
      powershell.exe -NoProfile -Command "Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Clipboard' -Name EnableClipboardHistory -ErrorAction SilentlyContinue" >/dev/null 2>&1
    else
      powershell.exe -NoProfile -Command "Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Clipboard' -Name EnableClipboardHistory -Value $history -Type DWord" >/dev/null 2>&1
    fi
    echo "  clipboard history setting: RESTORED (the run left it at '${history_after:-absent}', put back to '${history:-absent}')"
    failures=$((failures + 1))
  fi
  grep -n "error CS" "$PT_LOG" | sort -u -t: -k2 | head -5

  # A run in which nothing executed reports failed="0" too. Here that would mean the player tests
  # dropped out of the build, which is exactly what this step exists to notice.
  pt_total="$(grep -o 'total="[0-9]*"' "$PT_XML" 2>/dev/null | head -1 | tr -dc '0-9')"
  if [ -f "$PT_XML" ] && [ "${pt_total:-0}" -eq 0 ]; then
    echo "  StandaloneWindows64: NO TESTS RAN"
    failures=$((failures + 1))
  fi

  # Layer 3: what the OS holds, read from outside Unity. A copy-then-paste round trip inside the
  # player passes even if both directions are broken the same way; this does not. It checks only
  # the last value written, so it relies on WindowsClipboardPlayerTests being the only player test
  # that writes to the clipboard. Checking item by item needs a signal between the player and this
  # script (topics/cross-platform-testing).
  actual="$(powershell.exe -NoProfile -Command "Get-Clipboard -Raw" 2>/dev/null)"
  actual="${actual%$'\r'}"
  if [ -z "$expected" ]; then
    echo "  system clipboard: CANNOT CHECK (no SampleText in $PT_SOURCE)"
    failures=$((failures + 1))
  elif [ "$actual" = "$expected" ]; then
    echo "  system clipboard: holds the sample value the player wrote"
  elif [ "$actual" = "$sentinel" ]; then
    echo "  system clipboard: FAILED (still the sentinel; the player wrote nothing)"
    failures=$((failures + 1))
  else
    echo "  system clipboard: FAILED (neither the sample value nor the sentinel; something else wrote last)"
    failures=$((failures + 1))
  fi

  # S-2 / S-4 / S-8 and each press's outcome over the sample's own log, as the manual run was judged. Only a destructive run
  # presses the sample's buttons (WindowsClipboardSampleRunPlayerTests), so only then is there a
  # log. The test writes it into its output, and the checker takes it from the result file and
  # saves it beside that file; the player's own directory is under Temp, which the editor deletes
  # as it exits. Buttons that only blocks B and C press are passed as not automated, read from
  # the test.
  if [ "$INCLUDE_DESTRUCTIVE" -eq 1 ]; then
    RUN_SOURCE="$PROJECT_DIR/Packages/com.jonghyunkim.nativetoolkit/Tests/PlayMode/WindowsClipboardSampleRunPlayerTests.cs"
    # The constant's value is on the line of its name, or on the next when it is long.
    not_automated="$(awk '/const string NotYetAutomated =/ { if ($0 !~ /"/) getline; print; exit }' "$RUN_SOURCE" \
      | sed -n 's/.*"\([^"]*\)".*/\1/p')"
    if [ -z "$not_automated" ]; then
      echo "  sample run log: CANNOT CHECK (no NotYetAutomated in $RUN_SOURCE)"
      failures=$((failures + 1))
    elif ! grep -q '\[SampleRun\] begin ' "$PT_XML" 2>/dev/null; then
      echo "  sample run log: MISSING (no run from WindowsClipboardSampleRunPlayerTests in the results)"
      failures=$((failures + 1))
    else
      echo "  sample run log (S-2 / S-4 / S-8 / outcomes):"
      python "$PROJECT_DIR/scripts/check_windows_clipboard_sample_log.py" \
        --not-automated "$not_automated" --test-results "$PT_XML" 2>&1 | sed 's/^/    /'
      [ "${PIPESTATUS[0]}" -eq 0 ] || failures=$((failures + 1))
    fi
  fi
fi

echo
if [ "$KEEP_CHANGES" -eq 1 ]; then
  echo "-- cleanup: skipped (--keep-changes); check git status before committing --"
else
  echo "-- cleanup --"
  cleaned=0
  while IFS=$'\t' read -r status path; do
    [ -n "$path" ] || continue
    if printf '%s\n' "$CHANGED_BEFORE" | cut -f2- | grep -Fxq -- "$path"; then
      echo "  left alone (already changed before the run): $path"
      continue
    fi
    if [ "$status" = "??" ]; then
      rm -f -- "$PROJECT_DIR/$path"
    else
      git -C "$PROJECT_DIR" restore -- "$path"
    fi
    cleaned=$((cleaned + 1))
  done <<< "$(changed_paths)"
  echo "  restored or removed $cleaned file(s) the run changed"
fi

echo
if [ "$failures" -eq 0 ]; then
  echo "all gates passed"
  exit 0
fi
echo "$failures gate(s) failed"
exit 1
