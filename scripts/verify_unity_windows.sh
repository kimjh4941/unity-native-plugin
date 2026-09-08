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
# The build step mutates the working tree: PreBuildProcessor places the native DLL from dist
# (design 6.3 - not a commit target), and Unity may re-serialize render pipeline settings. Neither
# belongs in a commit, so check `git status` afterwards and restore them.
#
# Usage:
#   scripts/verify_unity_windows.sh [--skip-build]
#
# Environment:
#   UNITY_EXE   Path to Unity.exe. Defaults to the 6000.4.2f1 install used by this project.
#   OUT_DIR     Where to put logs, test results and the player build. Defaults to a temp dir.

set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EXE="${UNITY_EXE:-/d/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe}"
OUT_DIR="${OUT_DIR:-${TMPDIR:-/tmp}/unity-windows-verify}"
SKIP_BUILD=0

for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=1 ;;
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
if [ "$failures" -eq 0 ]; then
  echo "all gates passed"
  exit 0
fi
echo "$failures gate(s) failed"
exit 1
