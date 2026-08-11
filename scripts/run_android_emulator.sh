#!/usr/bin/env bash
set -euo pipefail

repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
data_dir=${XDG_DATA_HOME:-${HOME}/.local/share}
android_root=${REDBOLT_ANDROID_ROOT:-${data_dir}/redbolt-android}
sdk_root=${ANDROID_SDK_ROOT:-${android_root}/sdk}
java_root=${JAVA_HOME:-${android_root}/jdk}
avd_home=${ANDROID_AVD_HOME:-${android_root}/avd}
avd_name=${REDBOLT_ANDROID_AVD:-redbolt-api36}
system_image=${REDBOLT_ANDROID_SYSTEM_IMAGE:-system-images;android-36;google_apis;x86_64}
apk=${REDBOLT_ANDROID_APK:-${repo_dir}/build/obj/managed/android-x64/staging/RedBolt.Sample.Android/Debug/org.redbolt.samples-Signed.apk}
sample=
screenshot=
emulator_window=false
shutdown=false

usage() {
    echo "Usage: $0 [--apk PATH] [--sample ID] [--screenshot PATH] [--window] [--shutdown]"
}

while (($#)); do
    case "$1" in
        --apk)
            apk=$2
            shift 2
            ;;
        --sample)
            sample=$2
            shift 2
            ;;
        --screenshot)
            screenshot=$2
            shift 2
            ;;
        --window)
            emulator_window=true
            shift
            ;;
        --shutdown)
            shutdown=true
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            usage >&2
            exit 2
            ;;
    esac
done

if [[ -n "$sample" && ! "$sample" =~ ^[a-z0-9-]+$ ]]; then
    echo "Invalid sample id: $sample" >&2
    exit 2
fi

adb=${sdk_root}/platform-tools/adb
emulator=${sdk_root}/emulator/emulator
avdmanager=${sdk_root}/cmdline-tools/latest/bin/avdmanager

for tool in "$adb" "$emulator" "$avdmanager"; do
    if [[ ! -x "$tool" ]]; then
        echo "Missing Android tool: $tool" >&2
        exit 1
    fi
done
if [[ ! -f "$apk" ]]; then
    echo "APK not found: $apk" >&2
    exit 1
fi

export ANDROID_AVD_HOME=$avd_home
export ANDROID_SDK_ROOT=$sdk_root
export JAVA_HOME=$java_root
mkdir -p "$avd_home"

if ! "$avdmanager" list avd | grep -Fq "Name: $avd_name"; then
    echo no | "$avdmanager" create avd --force --name "$avd_name" --package "$system_image"
fi

serial=$("$adb" devices | awk '$1 ~ /^emulator-/ && $2 == "device" { print $1; exit }')
emulator_pid=
boot_attempts=180
if ! "$emulator" -accel-check >/dev/null 2>&1; then
    boot_attempts=600
fi
runtime_timeout=${REDBOLT_ANDROID_RUNTIME_TIMEOUT:-180}
if ((boot_attempts > 180)) && [[ -z ${REDBOLT_ANDROID_RUNTIME_TIMEOUT:-} ]]; then
    runtime_timeout=600
fi

android_ready() {
    local boot_completed provisioned framework_path
    boot_completed=$(timeout 10 "$adb" -s "$serial" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r') || return 1
    provisioned=$(timeout 10 "$adb" -s "$serial" shell settings get global device_provisioned 2>/dev/null | tr -d '\r') || return 1
    framework_path=$(timeout 10 "$adb" -s "$serial" shell cmd package path android 2>/dev/null | tr -d '\r') || return 1
    [[ "$boot_completed" == 1 && "$provisioned" =~ ^[01]$ && "$framework_path" == package:* ]]
}

if [[ -z "$serial" ]]; then
    emulator_args=(-avd "$avd_name" -no-boot-anim -no-snapshot -no-metrics -no-audio -gpu software)
    if ((boot_attempts > 180)); then
        echo "KVM acceleration is unavailable; using the slower software CPU emulator."
        emulator_args+=(-accel off)
    fi
    if [[ "$emulator_window" == false ]]; then
        emulator_args+=(-no-window)
    fi
    setsid "$emulator" "${emulator_args[@]}" </dev/null >"${android_root}/emulator.log" 2>&1 &
    emulator_pid=$!
fi

for _ in $(seq 1 "$boot_attempts"); do
    if [[ -n "$emulator_pid" ]] && ! kill -0 "$emulator_pid" 2>/dev/null; then
        echo "Android emulator exited during startup. See ${android_root}/emulator.log" >&2
        tail -n 20 "${android_root}/emulator.log" >&2
        exit 1
    fi
    serial=$("$adb" devices | awk '$1 ~ /^emulator-/ && $2 == "device" { print $1; exit }')
    if [[ -n "$serial" ]] && android_ready; then
        break
    fi
    sleep 2
done

if [[ -z "$serial" ]] || ! android_ready; then
    echo "Android emulator did not finish booting. See ${android_root}/emulator.log" >&2
    exit 1
fi

"$adb" -s "$serial" shell am force-stop org.redbolt.samples || true
"$adb" -s "$serial" install --no-incremental -r "$apk"
"$adb" -s "$serial" logcat -c
"$adb" -s "$serial" shell am force-stop org.redbolt.samples

activity=$("$adb" -s "$serial" shell cmd package resolve-activity --brief \
    -a android.intent.action.MAIN -c android.intent.category.LAUNCHER org.redbolt.samples | tr -d '\r' | tail -n 1)
if [[ -z "$activity" || "$activity" != */* ]]; then
    echo "Unable to resolve the RedBolt launcher activity." >&2
    exit 1
fi

launch_args=(-W -n "$activity")
if [[ -n "$sample" ]]; then
    launch_args+=(--es sample "$sample")
fi
"$adb" -s "$serial" shell am start "${launch_args[@]}"

if [[ -n "$sample" ]]; then
    deadline=$((SECONDS + runtime_timeout))
    while ((SECONDS < deadline)); do
        logs=$(timeout 10 "$adb" -s "$serial" logcat -d -s RedBolt:V AndroidRuntime:E mono-rt:E '*:S' 2>/dev/null) || true
        if grep -Eq 'FATAL EXCEPTION|Unhandled Exception|Sample .* exited with code [^0]' <<<"$logs"; then
            echo "Android sample failed during startup." >&2
            printf '%s\n' "$logs" >&2
            exit 1
        fi
        if grep -Fq 'Verified packaged CoreData asset' <<<"$logs" \
            && grep -Fq 'Dear ImGui UI ready:' <<<"$logs"; then
            break
        fi
        if ! timeout 10 "$adb" -s "$serial" shell pidof org.redbolt.samples >/dev/null 2>&1; then
            echo "Android sample process exited before reaching the runtime gate." >&2
            printf '%s\n' "$logs" >&2
            exit 1
        fi
        sleep 2
    done
    if ((SECONDS >= deadline)); then
        echo "Android sample did not reach the runtime gate within ${runtime_timeout}s." >&2
        printf '%s\n' "$logs" >&2
        exit 1
    fi
    echo "Android runtime gate passed for sample: $sample"
fi

if [[ -n "$screenshot" ]]; then
    mkdir -p "$(dirname "$screenshot")"
    "$adb" -s "$serial" exec-out screencap -p >"$screenshot"
    echo "Screenshot: $screenshot"
fi

"$adb" -s "$serial" logcat -d -s RedBolt:V AndroidRuntime:E mono-rt:E '*:S'

if [[ "$shutdown" == true ]]; then
    "$adb" -s "$serial" emu kill
fi
