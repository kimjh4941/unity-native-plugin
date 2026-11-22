#!/bin/bash
# I-17: the package as a user installs it, from this repository's git URL at HEAD, in throwaway Unity
# projects (artifact/topics/ios-c-abi-2/designs/2026-11-22-ios-c-abi-i17-git-url-check-design-v3.md).
#
# The basic project has the package only (no define, no build profile: the user's form). It checks
# G-1 the assemblies and compile errors, G-2 the iOS export and the notification installer, G-3 a launch on
# the simulator, G-5 a Release archive re-signed by the development certificate (3-E's rest; no device),
# G-6a the Android build with the Gradle step, G-6b the Android architecture check, and last G-4 the
# notification opt-out define set in Player Settings (in its own Unity launch, then exported by another).
# The coexistence project adds Unity's Mobile Notifications 2.5.0 and checks, on the simulator only,
# M-1 the delegate after launch, M-2 the delegate after its AuthorizationRequest (provisional: no prompt),
# M-3 the delegate after IosNotificationManager.Initialize() and the library's log line. It never goes to
# a device: Mobile Notifications asks for the permission at launch by default.
#
# Usage:
#   scripts/verify_unity_ios_git_url.sh                         (the simulator: G-1 to G-6, M-1 to M-3)
#   scripts/verify_unity_ios_git_url.sh --device --dry-run      (prints what happens on the device and an approval ID)
#   scripts/verify_unity_ios_git_url.sh --device --approved <ID> (G-7: the basic project's app on the one paired iPhone)
#
# A device run follows native-toolkit's agent-rules/device-testing.md as scripts/verify_unity_ios.sh does: the
# approval (GitUrlApp/IosLaunch in scripts/ios_ui_runner.py) works once and is checked again before every step
# that touches the device; the app is removed before the install and at the end; nothing that names the
# device, the team or a certificate is printed; the logs and the result go to a temporary folder deleted at
# the end. The device gets only the basic project's app (no Mobile Notifications: it would ask for the
# permission at launch), launched with I17_STEPS=launch.
#
# The projects live in a temporary folder and are removed at the end; nothing is written to this
# repository. The package's tree must have no uncommitted changes (the git URL takes HEAD). The archive's
# logs are deleted at the end of the step, and only the start of `Authority=` (up to ':') and a short hash
# of the team are shown, never a certificate name or the team.
#
# Environment:
#   UNITY_APP, XCODE_APP, SIM_NAME (iPhone 16), SIM_RUNTIME (iOS 18.3)

set -u
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=scripts/ios_script_common.sh
source "$PROJECT_DIR/scripts/ios_script_common.sh"

UNITY_APP="${UNITY_APP:-/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app}"
UNITY_EXE="$UNITY_APP/Contents/MacOS/Unity"
XCODE_APP="${XCODE_APP:-/Applications/Xcode26.3.app}"
XCODEBUILD="$XCODE_APP/Contents/Developer/usr/bin/xcodebuild"
SIM_NAME="${SIM_NAME:-iPhone 16}"
SIM_RUNTIME="${SIM_RUNTIME:-iOS 18.3}"
BUNDLE_ID="com.jonghyunkim.nativetoolkit.giturlapp"
MOBILE_NOTIFICATIONS="2.5.0"
PROXY="IosLibrary.NotificationDelegateProxy"
KEEP_CHANGES=1
SIM_BOOTED_BY_US=0
SIM_ID=""
RUNNER="$PROJECT_DIR/scripts/ios_ui_runner.py"
APPROVAL_TESTS=("GitUrlApp/IosLaunch")
DEVICE_MODE=0
DRY_RUN=0
APPROVAL=""
RUN_MARKER=""
DEVICE_TOUCHED=0
APP=""
RUN_ID="$(uuidgen | tr -d - | tr 'A-F' 'a-f')"

while [ $# -gt 0 ]; do
  case "$1" in
    --device) DEVICE_MODE=1 ;;
    --dry-run) DRY_RUN=1 ;;
    --approved) APPROVAL="${2:-}"; shift ;;
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
    echo "A device run needs --dry-run first, then --approved <ID> once the user has approved it." >&2
    exit 2
  fi
  python3 -I "$RUNNER" check-approval "$PROJECT_DIR" "$APPROVAL" "${APPROVAL_TESTS[@]}" || exit 1
elif [ "$DRY_RUN" -eq 1 ] || [ -n "$APPROVAL" ]; then
  echo "--dry-run and --approved are for device runs (--device)." >&2
  exit 2
fi

for tool in "$UNITY_EXE" "$XCODEBUILD"; do
  [ -x "$tool" ] || { echo "Not found: $tool (set UNITY_APP / XCODE_APP)" >&2; exit 2; }
done
# simctl and devicectl from the same Xcode as the builds.
export DEVELOPER_DIR="$XCODE_APP/Contents/Developer"

if [ -n "$(git -C "$PROJECT_DIR" status --porcelain=v1 --untracked-files=all -- Packages/com.jonghyunkim.nativetoolkit)" ]; then
  echo "Packages/com.jonghyunkim.nativetoolkit has uncommitted changes: the git URL installs HEAD, without them." >&2
  exit 2
fi
HEAD="$(git -C "$PROJECT_DIR" rev-parse HEAD)"
if [ "$DEVICE_MODE" -eq 0 ]; then
  if pgrep -x Simulator >/dev/null; then
    echo "Simulator.app is open; close it first (the run uses the simulator headless)." >&2
    exit 2
  fi
  SIM_ID="$(find_simulator "$SIM_NAME" "$SIM_RUNTIME")"
  if [ -z "$SIM_ID" ]; then
    echo "Expected exactly one available simulator named '$SIM_NAME' on '$SIM_RUNTIME' (set SIM_NAME / SIM_RUNTIME)." >&2
    exit 2
  fi
fi
take_run_lock || exit 2

OUT_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ntk-git-url.XXXXXX")"
on_exit() {
  trap '' INT TERM HUP
  stop_running_step
  if [ "$DEVICE_MODE" -eq 1 ]; then
    # Only when the run stopped before its own cleanup: the app this run installed is removed without
    # the guard, as scripts/verify_unity_ios.sh does (removing it is part of what was approved).
    if [ "$DEVICE_TOUCHED" -eq 1 ] && ! remove_from_device "$BUNDLE_ID"; then
      echo "WARNING: the app of this run may still be on the device"
    fi
    rm -rf "$OUT_DIR"
    release_run_lock
    return
  fi
  if [ -n "$SIM_ID" ]; then
    xcrun simctl terminate "$SIM_ID" "$BUNDLE_ID" >/dev/null 2>&1
    xcrun simctl uninstall "$SIM_ID" "$BUNDLE_ID" >/dev/null 2>&1
    [ "$SIM_BOOTED_BY_US" -eq 1 ] && xcrun simctl shutdown "$SIM_ID" >/dev/null 2>&1
  fi
  rm -rf "$OUT_DIR/archive"
  rm -rf "$OUT_DIR"/*/project
  release_run_lock
}
trap on_exit EXIT
trap 'exit 130' INT TERM HUP

failures=0
fail() { echo "  $1"; failures=$((failures + 1)); }

# write_project <dir> <with Mobile Notifications 0|1>: the scripts and the manifest of a throwaway project.
write_project() {
  local dir="$1" with_mn="$2"
  mkdir -p "$dir/Assets/Scripts" "$dir/Assets/Plugins/iOS" "$dir/Assets/Editor"
  python3 -I - "$dir/Packages/manifest.json" "$PROJECT_DIR" "$HEAD" "$with_mn" "$MOBILE_NOTIFICATIONS" <<'EOF'
import json, sys
path, repo, head, with_mn, mn = sys.argv[1:6]
with open(path, encoding="utf-8") as f:
    manifest = json.load(f)
manifest["dependencies"]["com.jonghyunkim.nativetoolkit"] = f"git+file://{repo}?path=Packages/com.jonghyunkim.nativetoolkit#{head}"
if with_mn == "1":
    manifest["dependencies"]["com.unity.mobile.notifications"] = mn
with open(path, "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2)
EOF
  cat > "$dir/Assets/Plugins/iOS/I17Probe.mm" <<'EOF'
// The class name of the notification center's delegate (as the player tests' probe reads it).
#import <Foundation/Foundation.h>
#import <objc/message.h>
#include <string.h>
extern "C" char* i17_delegate_class(void)
{
    Class centerClass = NSClassFromString(@"UNUserNotificationCenter");
    if (centerClass == Nil) return NULL;
    id center = ((id (*)(id, SEL))objc_msgSend)(centerClass, NSSelectorFromString(@"currentNotificationCenter"));
    if (center == nil) return NULL;
    id delegate = ((id (*)(id, SEL))objc_msgSend)(center, NSSelectorFromString(@"delegate"));
    if (delegate == nil) return NULL;
    return strdup(NSStringFromClass([delegate class]).UTF8String);
}
EOF
  cat > "$dir/Assets/Scripts/I17Probe.cs" <<'EOF'
#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;
#endif

// I-17: writes what it finds, one step a line, then "done" (design v3, 3.1). Only fixed words, numbers and
// class names. I17_STEPS is "launch" or "launch,coexist".
public sealed class I17Probe : MonoBehaviour
{
    public static Func<IEnumerator>? CoexistSteps;
    private static string s_path = "";

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string? i17_delegate_class();
    [DllImport("__Internal")] private static extern uint ntk_version();
    public static string DelegateClass() => i17_delegate_class() ?? "<none>";
#else
    public static string DelegateClass() => "<editor>";
#endif

    public static void Write(string line) => File.AppendAllText(s_path, line + "\n");

    private IEnumerator Start()
    {
        s_path = Path.Combine(Application.persistentDataPath, "i17-result.txt");
        File.WriteAllText(s_path, "");
        Write("started");
#if UNITY_IOS && !UNITY_EDITOR
        Write($"run={Environment.GetEnvironmentVariable("I17_RUN") ?? "<none>"}");
        Write($"version=0x{ntk_version():x6}");
        Write($"delegate={DelegateClass()}");
        string steps = Environment.GetEnvironmentVariable("I17_STEPS") ?? "launch";
        if (steps.Contains("coexist"))
        {
            if (CoexistSteps == null)
            {
                Write("coexist=missing");
            }
            else
            {
                yield return CoexistSteps();
                IosNotificationManager.Instance.Initialize();
                Write($"m3 delegate={DelegateClass()}");
            }
        }
#endif
        Write("done");
        yield break;
    }
}
EOF
  cat > "$dir/Assets/Editor/I17Build.cs" <<'EOF'
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batchmode entry points of the I-17 check (design v3, 3.1). One Unity launch, one build target.
public static class I17Build
{
    const string ScenePath = "Assets/I17.unity";
    const string BundleId = "com.jonghyunkim.nativetoolkit.giturlapp";

    static string Arg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"{name} is required");
    }

    static void Configure()
    {
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("I17Probe").AddComponent<I17Probe>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
        PlayerSettings.iOS.targetOSVersionString = "18.0";
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel31;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
    }

    static void Finish(BuildReport report)
    {
        Debug.Log($"[I17] build result: {report.summary.result}");
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // -i17Sdk simulator|device -i17Out <dir>
    public static void ExportIos()
    {
        Configure();
        bool simulator = Arg("-i17Sdk") == "simulator";
        PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
        if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
        Finish(BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, target = BuildTarget.iOS, locationPathName = Arg("-i17Out"),
        }));
    }

    // -i17Arch arm64|arm64+armv7 -i17Out <apk>
    public static void BuildAndroid()
    {
        Configure();
        PlayerSettings.Android.targetArchitectures = Arg("-i17Arch") == "arm64"
            ? AndroidArchitecture.ARM64 : AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        Finish(BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, target = BuildTarget.Android, locationPathName = Arg("-i17Out"),
        }));
    }

    // G-4: only sets the define; another launch exports, after the Editor assemblies are compiled with it.
    public static void SetSkipInstallDefine()
    {
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, "NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL");
        AssetDatabase.SaveAssets();
        Debug.Log("[I17] define set");
        EditorApplication.Exit(0);
    }
}
EOF
  if [ "$with_mn" = "1" ]; then
    cat > "$dir/Assets/Scripts/I17CoexistProbe.cs" <<'EOF'
using System.Collections;
using Unity.Notifications.iOS;
using UnityEngine;

// M-2: Mobile Notifications' own request, provisional and without remote registration, so no prompt
// shows (design v3, 2). Its delegate is read one second later, without waiting for the request.
public static class I17CoexistProbe
{
    [RuntimeInitializeOnLoadMethod]
    static void Hook() => I17Probe.CoexistSteps = Steps;

    static IEnumerator Steps()
    {
        var request = new AuthorizationRequest(
            AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound | AuthorizationOption.Provisional, false);
        yield return new WaitForSeconds(1);
        I17Probe.Write($"m2 delegate={I17Probe.DelegateClass()}");
        request.Dispose();
    }
}
EOF
    cat > "$dir/Assets/Editor/I17CoexistSettings.cs" <<'EOF'
using Unity.Notifications;
using UnityEditor;
using UnityEngine;

// Mobile Notifications asks for the permission at launch by default: off (design v3, 2; review v1 R1-1).
public static class I17CoexistSettings
{
    public static void TurnOffLaunchRequest()
    {
        NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;
        AssetDatabase.SaveAssets();
        Debug.Log($"[I17] request on launch: {NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch}");
        EditorApplication.Exit(0);
    }
}
EOF
  fi
}

# unity <label> <project> <build target> <log> <args...>: one Unity launch, limited.
unity() {
  local label="$1" project="$2" target="$3" log="$4"
  shift 4
  run_limited "$label" 2400 900 "$log" "$log.console" \
    "$UNITY_EXE" -batchmode -nographics -projectPath "$project" -buildTarget "$target" -logFile "$log" "$@"
  local code=$?
  local errors
  errors="$(grep -c 'error CS' "$log" 2>/dev/null)"
  if [ "${errors:-0}" != "0" ]; then
    echo "  $label: $errors compile error line(s)"
    grep 'error CS' "$log" | sort -u | head -5
    return 1
  fi
  return $code
}

# create <name> <with Mobile Notifications>: a new project from Unity's default template, then the files.
create() {
  local name="$1" with_mn="$2" dir="$OUT_DIR/$1/project"
  mkdir -p "$OUT_DIR/$name"
  run_limited "$name create" 1200 600 "$OUT_DIR/$name/create.log" "$OUT_DIR/$name/create.log.console" \
    "$UNITY_EXE" -batchmode -nographics -createProject "$dir" -logFile "$OUT_DIR/$name/create.log" -quit || return 1
  write_project "$dir" "$with_mn"
}

# installer_line <export log>: the setup hook's line.
installer_line() {
  grep -o 'linked and embedded: .*installer entries: [0-9]* (skip: [A-Za-z]*)' "$1" | tail -1
}

# simulator_app <name> <export dir>: builds the export for the simulator, unsigned; sets APP to the .app.
# Not run in a command substitution, so run_limited's RUNNING_PID stays visible to the EXIT trap.
simulator_app() {
  local name="$1" export_dir="$2" log="$OUT_DIR/$1/build.log" derived="$OUT_DIR/$1/dd"
  run_limited "$name build" 2400 600 "$log" "$log" \
    "$XCODEBUILD" -project "$export_dir/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
    -sdk iphonesimulator -destination "platform=iOS Simulator,id=$SIM_ID" -derivedDataPath "$derived" \
    CODE_SIGN_IDENTITY=- CODE_SIGN_STYLE=Manual DEVELOPMENT_TEAM= PROVISIONING_PROFILE_SPECIFIER= build
  APP=""
  if ! grep -q '\*\* BUILD SUCCEEDED \*\*' "$log"; then
    show_build_errors "$log" 0
    return 1
  fi
  local apps
  apps="$(ls -d "$derived"/Build/Products/*/*.app 2>/dev/null)"
  [ "$(printf '%s\n' "$apps" | grep -c .)" = "1" ] || { echo "  $name build: expected one .app"; return 1; }
  require_bundle_id "$apps" "$BUNDLE_ID" || return 1
  APP="$apps"
}

# run_on_simulator <name> <app> <steps> <console log>: installs, launches with I17_STEPS, this run's I17_RUN
# and the library's info log on the console, waits for "done" (60 s), and copies the result file to
# $OUT_DIR/<name>/result.txt. Not run in a command substitution (SIM_BOOTED_BY_US and RUNNING_PID).
run_on_simulator() {
  local name="$1" app="$2" steps="$3" console="$4"
  if ! xcrun simctl list devices | grep -F "($SIM_ID) (Booted)" >/dev/null; then SIM_BOOTED_BY_US=1; fi
  run_limited "$name boot" 300 300 "$OUT_DIR/$name/boot.log" "$OUT_DIR/$name/boot.log" xcrun simctl bootstatus "$SIM_ID" -b || return 1
  xcrun simctl terminate "$SIM_ID" "$BUNDLE_ID" >/dev/null 2>&1
  xcrun simctl uninstall "$SIM_ID" "$BUNDLE_ID" >/dev/null 2>&1
  if xcrun simctl listapps "$SIM_ID" 2>/dev/null | grep -qF "$BUNDLE_ID"; then
    echo "  $name: an earlier app could not be removed"
    return 1
  fi
  rm -f "$OUT_DIR/$name/result.txt"
  xcrun simctl install "$SIM_ID" "$app" || { echo "  $name install: failed"; return 1; }
  local data result waited=0
  data="$(xcrun simctl get_app_container "$SIM_ID" "$BUNDLE_ID" data)" || return 1
  result="$data/Documents/i17-result.txt"
  SIMCTL_CHILD_I17_STEPS="$steps" SIMCTL_CHILD_I17_RUN="$RUN_ID" SIMCTL_CHILD_OS_ACTIVITY_DT_MODE=YES \
    xcrun simctl launch --console-pty "$SIM_ID" "$BUNDLE_ID" > "$console" 2>&1 &
  local launcher=$!
  while ! grep -qx done "$result" 2>/dev/null && [ "$waited" -lt 60 ]; do
    sleep 1; waited=$((waited + 1))
  done
  sleep 1
  xcrun simctl terminate "$SIM_ID" "$BUNDLE_ID" >/dev/null 2>&1
  kill "$launcher" 2>/dev/null
  wait "$launcher" 2>/dev/null
  cp "$result" "$OUT_DIR/$name/result.txt" 2>/dev/null
  xcrun simctl uninstall "$SIM_ID" "$BUNDLE_ID" >/dev/null 2>&1
}

# expect_lines <result> <line>...: every line is in the result, which ends with done.
expect_lines() {
  local result="$1" line ok=0
  shift
  for line in "$@" done; do
    grep -qxF "$line" <<< "$result" || { echo "    missing: $line"; ok=1; }
  done
  return $ok
}

echo "Package: git URL at $(git -C "$PROJECT_DIR" rev-parse --short HEAD)"

# ── G-7: the device ───────────────────────────────────────────────────────────
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

run_on_device() {
  echo "Device (G-7):"
  # The paired iPhone first (a listing only), so a run without one fails before the 10-minute build.
  select_device "$OUT_DIR" || return 1
  B="$OUT_DIR/basic/project"
  create basic 0 || { echo "  create: failed"; return 1; }
  unity "export (device)" "$B" iOS "$OUT_DIR/basic/export-device.log" \
    -executeMethod I17Build.ExportIos -i17Sdk device -i17Out "$OUT_DIR/basic/xcode-device" || { echo "  export: failed"; return 1; }
  select_team || return 1
  run_limited build 2400 600 "$OUT_DIR/basic/build.log" "$OUT_DIR/basic/build.log" \
    "$XCODEBUILD" -project "$OUT_DIR/basic/xcode-device/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
    -destination "generic/platform=iOS" -derivedDataPath "$OUT_DIR/basic/dd" \
    CODE_SIGN_STYLE=Automatic PROVISIONING_PROFILE_SPECIFIER= DEVELOPMENT_TEAM="$TEAM" build
  TEAM=""
  if ! grep -q '\*\* BUILD SUCCEEDED \*\*' "$OUT_DIR/basic/build.log"; then
    show_build_errors "$OUT_DIR/basic/build.log" 1
    return 1
  fi
  local app
  app="$(ls -d "$OUT_DIR"/basic/dd/Build/Products/*/*.app 2>/dev/null)"
  [ "$(printf '%s\n' "$app" | grep -c .)" = "1" ] || { echo "  build: expected one .app"; return 1; }
  require_bundle_id "$app" "$BUNDLE_ID" || return 1
  echo "  build: ok"
  RUN_MARKER="$(python3 -I "$RUNNER" use-approval "$PROJECT_DIR" "$APPROVAL" "${APPROVAL_TESTS[@]}")" || return 1
  [ -n "$RUN_MARKER" ] || return 1
  device_guard || return 1
  run_limited connection 60 60 "$OUT_DIR/connection.log" "$OUT_DIR/connection.log" \
    xcrun devicectl device info details --device "$DEVICE_ID" --json-output "$OUT_DIR/details.json" >/dev/null \
    || { rm -f "$OUT_DIR/details.json"; echo "  stopped: the device's development connection did not answer"; return 1; }
  rm -f "$OUT_DIR/details.json"
  DEVICE_TOUCHED=1
  device_guard || return 1
  remove_from_device "$BUNDLE_ID" || { echo "  stopped: an earlier app could not be confirmed removed"; return 1; }
  device_guard || return 1
  run_limited install 300 300 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
    xcrun devicectl device install app --device "$DEVICE_ID" "$app" >/dev/null || { echo "  install: failed"; return 1; }
  device_guard || return 1
  run_limited launch 120 120 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
    xcrun devicectl device process launch --device "$DEVICE_ID" --environment-variables "{\"I17_STEPS\": \"launch\", \"I17_RUN\": \"$RUN_ID\"}" \
    --json-output "$OUT_DIR/launch.json" "$BUNDLE_ID" >/dev/null || { echo "  launch: failed"; return 1; }
  local pid start waited=0 result="$OUT_DIR/i17-result.txt"
  pid="$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["result"]["process"]["processIdentifier"])' "$OUT_DIR/launch.json" 2>/dev/null)"
  rm -f "$OUT_DIR/launch.json"
  # The result is copied until it ends with "done", at most 120 s; every copy is a device step.
  start=$(date +%s)
  while [ "$waited" -lt 120 ]; do
    device_guard || break
    rm -f "$result"
    run_limited copy 60 60 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
      xcrun devicectl device copy from --device "$DEVICE_ID" --domain-type appDataContainer \
      --domain-identifier "$BUNDLE_ID" --source "Documents/i17-result.txt" --destination "$result" >/dev/null
    grep -qx done "$result" 2>/dev/null && break
    sleep 5
    waited=$(( $(date +%s) - start ))
  done
  if [ -n "$pid" ] && device_guard; then
    run_limited terminate 60 60 "$OUT_DIR/device.log" "$OUT_DIR/device.log" \
      xcrun devicectl device process terminate --device "$DEVICE_ID" --pid "$pid" >/dev/null
  fi
  local lines
  lines="$(cat "$result" 2>/dev/null)"
  printf '%s\n' "$lines" | sed 's/^/    /'
  if expect_lines "$lines" started "run=$RUN_ID" version=0x020000 "delegate=$PROXY"; then
    echo "  G-7 device launch: ok"
  else
    echo "  G-7 device launch: failed"
    return 1
  fi
}

if [ "$DEVICE_MODE" -eq 1 ]; then
  status=0
  run_on_device || status=1
  # The app is removed and confirmed gone before the result: a run that leaves it behind fails.
  if [ "$DEVICE_TOUCHED" -eq 1 ]; then
    if device_guard && remove_from_device "$BUNDLE_ID"; then
      DEVICE_TOUCHED=0
      echo "  cleanup: the app is removed"
    else
      echo "  cleanup: the app could not be confirmed removed"
      status=1
    fi
  fi
  [ "$status" -eq 0 ] && echo "PASSED" || echo "FAILED"
  exit "$status"
fi

# ── The basic project ─────────────────────────────────────────────────────────
echo "Basic project:"
B="$OUT_DIR/basic/project"
if ! create basic 0; then
  fail "create: failed (see $OUT_DIR/basic/create.log)"
else
  echo "  create: ok"
  # G-1 and G-2: the iOS export for the simulator.
  if unity "export (simulator)" "$B" iOS "$OUT_DIR/basic/export-sim.log" \
      -executeMethod I17Build.ExportIos -i17Sdk simulator -i17Out "$OUT_DIR/basic/xcode-sim"; then
    present="$(ls "$B/Library/ScriptAssemblies" | sed -n 's/^\(NativeToolkit\.[A-Za-z.]*\)\.dll$/\1/p' | sort | tr '\n' ' ')"
    if [ "$present" = "NativeToolkit.BuildTools.Editor NativeToolkit.Editor NativeToolkit.Runtime " ]; then
      echo "  G-1 assemblies: ok ($present)"
    else
      fail "G-1 assemblies: $present"
    fi
    line="$(installer_line "$OUT_DIR/basic/export-sim.log")"
    pbx="$OUT_DIR/basic/xcode-sim/Unity-iPhone.xcodeproj/project.pbxproj"
    if [ "$line" = "linked and embedded: ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-capi-2.0.0.xcframework; installer entries: 1 (skip: False)" ] \
        && [ -f "$OUT_DIR/basic/xcode-sim/Libraries/com.jonghyunkim.nativetoolkit/NtkNotificationInstaller.mm" ] \
        && grep -q 'NtkNotificationInstaller\.mm in Sources' "$pbx"; then
      echo "  G-2 export: ok"
    else
      fail "G-2 export: ${line:-no setup log line}"
    fi
    # G-3: the launch on the simulator.
    if simulator_app basic "$OUT_DIR/basic/xcode-sim"; then
      run_on_simulator basic "$APP" launch "$OUT_DIR/basic/console.log"
      result="$(cat "$OUT_DIR/basic/result.txt" 2>/dev/null)"
      if expect_lines "$result" started "run=$RUN_ID" version=0x020000 "delegate=$PROXY"; then
        echo "  G-3 simulator launch: ok"
      else
        fail "G-3 simulator launch: failed"
      fi
    else
      fail "G-3 simulator launch: no app"
    fi
  else
    fail "G-1/G-2 export: failed (see $OUT_DIR/basic/export-sim.log)"
  fi

  # G-5: the Release archive, signed by the development certificate. Its logs are removed at once.
  mkdir -p "$OUT_DIR/archive"
  if unity "export (device)" "$B" iOS "$OUT_DIR/archive/export-device.log" \
      -executeMethod I17Build.ExportIos -i17Sdk device -i17Out "$OUT_DIR/archive/xcode-device"; then
    if select_team; then
      run_limited "archive" 2400 600 "$OUT_DIR/archive/archive.log" "$OUT_DIR/archive/archive.log" \
        "$XCODEBUILD" -project "$OUT_DIR/archive/xcode-device/Unity-iPhone.xcodeproj" -scheme Unity-iPhone \
        -configuration Release -destination "generic/platform=iOS" -archivePath "$OUT_DIR/archive/App.xcarchive" \
        -derivedDataPath "$OUT_DIR/archive/dd" \
        CODE_SIGN_STYLE=Automatic PROVISIONING_PROFILE_SPECIFIER= DEVELOPMENT_TEAM="$TEAM" archive
      TEAM=""
      if grep -q '\*\* ARCHIVE SUCCEEDED \*\*' "$OUT_DIR/archive/archive.log"; then
        app="$(ls -d "$OUT_DIR"/archive/App.xcarchive/Products/Applications/*.app 2>/dev/null | head -1)"
        signed=0
        report=""
        for item in "$app" "$app/Frameworks/IosLibrary.framework" "$app/Frameworks/IosLibraryCApi.framework"; do
          authority="$(codesign -dvv "$item" 2>&1 | grep -m1 '^Authority=' | sed 's/^Authority=\([^:]*:\).*/\1/')"
          team="$(codesign -dvv "$item" 2>&1 | grep -m1 '^TeamIdentifier=' | shasum | cut -c1-8)"
          report+="$(basename "$item"): $authority team $team; "
          [ "$authority" = "Apple Development:" ] || signed=1
        done
        teams="$(grep -o 'team [0-9a-f]*' <<< "$report" | sort -u | grep -c .)"
        frameworks="$(ls -d "$app"/Frameworks/*.framework 2>/dev/null | xargs -n1 basename | sort | tr '\n' ' ')"
        versions="$(for f in IosLibrary IosLibraryCApi; do plutil -extract CFBundleShortVersionString raw "$app/Frameworks/$f.framework/Info.plist" 2>/dev/null; done | tr '\n' ' ')"
        echo "  G-5 frameworks: $frameworks(versions: $versions)"
        # The two 2.0.0 frameworks once each (Unity's own frameworks are there too: 3-E saw UnityFramework and UnityRuntime).
        [ "$(tr ' ' '\n' <<< "$frameworks" | grep -cx 'IosLibrary.framework\|IosLibraryCApi.framework')" = "2" ] || signed=1
        [ "$versions" = "2.0.0 2.0.0 " ] || signed=1
        verify="$(codesign --verify --deep --strict --verbose=2 "$app" 2>&1 | tail -2 | tr '\n' ' ')"
        echo "  G-5 archive: $report"
        echo "  G-5 verify: $verify"
        if [ "$signed" -eq 0 ] && [ "$teams" = "1" ] && grep -q 'valid on disk' <<< "$verify" \
            && grep -q 'satisfies its Designated Requirement' <<< "$verify"; then
          echo "  G-5 archive: ok"
        else
          fail "G-5 archive: not as expected"
        fi
      else
        echo "  archive: failed ($(grep -c 'error:' "$OUT_DIR/archive/archive.log") error line(s); not shown)"
        fail "G-5 archive: failed"
      fi
    else
      fail "G-5 archive: no single team"
    fi
  else
    fail "G-5 export: failed"
  fi
  rm -rf "$OUT_DIR/archive"

  # G-6a and G-6b: Android, in launches of their own (the Android build steps compile for Android only).
  if unity "android (arm64)" "$B" Android "$OUT_DIR/basic/android-arm64.log" \
      -executeMethod I17Build.BuildAndroid -i17Arch arm64 -i17Out "$OUT_DIR/basic/app-arm64.apk" \
      && grep -q '\[AndroidGradleDependencies\] .* native-toolkit runtime dependencies in' "$OUT_DIR/basic/android-arm64.log"; then
    echo "  G-6a android build: ok"
  else
    fail "G-6a android build: failed (see $OUT_DIR/basic/android-arm64.log)"
  fi
  unity "android (arm64+armv7)" "$B" Android "$OUT_DIR/basic/android-armv7.log" \
    -executeMethod I17Build.BuildAndroid -i17Arch arm64+armv7 -i17Out "$OUT_DIR/basic/app-armv7.apk"
  if grep -q 'Native Toolkit for Android supports ARM64' "$OUT_DIR/basic/android-armv7.log"; then
    echo "  G-6b architecture check: ok (stopped the ARMv7 build)"
  else
    fail "G-6b architecture check: the ARMv7 build was not stopped by it"
  fi

  # G-4 last: the define stays in ProjectSettings. One launch sets it, another exports.
  if unity "define" "$B" iOS "$OUT_DIR/basic/define.log" -executeMethod I17Build.SetSkipInstallDefine \
      && unity "export (define)" "$B" iOS "$OUT_DIR/basic/export-define.log" \
        -executeMethod I17Build.ExportIos -i17Sdk simulator -i17Out "$OUT_DIR/basic/xcode-define"; then
    line="$(installer_line "$OUT_DIR/basic/export-define.log")"
    if [ "${line##*; }" = "installer entries: 0 (skip: True)" ] \
        && [ ! -e "$OUT_DIR/basic/xcode-define/Libraries/com.jonghyunkim.nativetoolkit/NtkNotificationInstaller.mm" ] \
        && ! grep -q 'NtkNotificationInstaller\.mm' "$OUT_DIR/basic/xcode-define/Unity-iPhone.xcodeproj/project.pbxproj"; then
      echo "  G-4 Player Settings define: ok"
    else
      fail "G-4 Player Settings define: ${line:-no setup log line}"
    fi
  else
    fail "G-4: failed (see $OUT_DIR/basic/define.log, export-define.log)"
  fi
fi

# ── The coexistence project (simulator only) ──────────────────────────────────
echo "Coexistence project (Mobile Notifications $MOBILE_NOTIFICATIONS):"
C="$OUT_DIR/coexist/project"
if ! create coexist 1; then
  fail "create: failed (see $OUT_DIR/coexist/create.log)"
elif ! unity "settings" "$C" iOS "$OUT_DIR/coexist/settings.log" -executeMethod I17CoexistSettings.TurnOffLaunchRequest; then
  fail "settings: failed (see $OUT_DIR/coexist/settings.log)"
elif ! unity "export (simulator)" "$C" iOS "$OUT_DIR/coexist/export-sim.log" \
    -executeMethod I17Build.ExportIos -i17Sdk simulator -i17Out "$OUT_DIR/coexist/xcode-sim"; then
  fail "export: failed (see $OUT_DIR/coexist/export-sim.log)"
else
  plist="$(plutil -extract UnityNotificationRequestAuthorizationOnAppLaunch raw "$OUT_DIR/coexist/xcode-sim/Info.plist" 2>/dev/null)"
  if [ "$plist" != "false" ]; then
    fail "the launch request is not off in Info.plist (${plist:-missing}); not launched"
  elif simulator_app coexist "$OUT_DIR/coexist/xcode-sim"; then
    run_on_simulator coexist "$APP" launch,coexist "$OUT_DIR/coexist/console.log"
    result="$(cat "$OUT_DIR/coexist/result.txt" 2>/dev/null)"
    printf '%s\n' "$result" | sed 's/^/    /'
    expect_lines "$result" "run=$RUN_ID" "delegate=$PROXY" && echo "  M-1 after launch: ok" || fail "M-1 after launch: not the proxy"
    expect_lines "$result" "m2 delegate=UnityNotificationManager" && echo "  M-2 after the request: ok" \
      || fail "M-2 after the request: not UnityNotificationManager"
    if expect_lines "$result" "m3 delegate=$PROXY" \
        && grep -q 'installing the proxy again in front of it' "$OUT_DIR/coexist/console.log" \
        && grep 'installing the proxy again in front of it' "$OUT_DIR/coexist/console.log" | grep -q UnityNotificationManager; then
      echo "  M-3 after Initialize(): ok"
    else
      fail "M-3 after Initialize(): failed (see $OUT_DIR/coexist/console.log)"
    fi
  else
    fail "M: no app"
  fi
fi

echo "Logs: $OUT_DIR"
if [ "$failures" -ne 0 ]; then
  echo "FAILED: $failures check(s) did not pass"
  exit 1
fi
echo "PASSED"
