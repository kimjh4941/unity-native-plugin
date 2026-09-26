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
# unity-windows-native-toolkit-debug.dll. None of this belongs in a commit, so check `git status`
# afterwards and restore it.
#
# The player-test step runs PlayMode tests on a Windows player (layer 2b), the only place the
# P/Invoke path actually executes, and then reads the system clipboard from outside Unity (a first
# layer 3 check). It needs an interactive desktop session: the clipboard and the player window do not
# work from a service account or a headless agent. --skip-player-tests leaves it out.
#
# Usage:
#   scripts/verify_unity_windows.sh [--skip-build] [--skip-player-tests]
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

for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=1 ;;
    --skip-player-tests) SKIP_PLAYER_TESTS=1 ;;
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

# Prints the counts from an NUnit result file, or says why it cannot.
report_tests() {
  local label="$1" xml="$2" exit_code="$3"
  if [ ! -f "$xml" ]; then
    echo "  $label: NO RESULT FILE (Unity exited $exit_code; see the log)"
    return 1
  fi
  local counts
  counts="$(grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$xml" | head -1)"
  echo "  $label: ${counts:-unreadable}"
  case "$counts" in
    *'failed="0"') return 0 ;;
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

  # No -nographics: the player opens a window and owns the clipboard from its main thread. This
  # step has been run without it; it has not been tried with it.
  "$UNITY_EXE" -batchmode -projectPath "$PROJECT_DIR" \
    -runTests -testPlatform StandaloneWindows64 \
    -testResults "$PT_XML" -logFile "$PT_LOG" >/dev/null 2>&1
  pt_code=$?
  report_tests "StandaloneWindows64" "$PT_XML" "$pt_code" || failures=$((failures + 1))
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
fi

echo
if [ "$failures" -eq 0 ]; then
  echo "all gates passed"
  exit 0
fi
echo "$failures gate(s) failed"
exit 1
