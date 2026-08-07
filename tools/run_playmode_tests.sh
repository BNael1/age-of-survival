#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity}"

RESULTS_DIR="$PROJECT_ROOT/TestResults"
RESULTS_FILE="$RESULTS_DIR/playmode-results.xml"
LOG_FILE="$RESULTS_DIR/playmode.log"
RETRY_LOG_FILE="$RESULTS_DIR/playmode-retryable-crash.log"

# LOT7HA3_PLAYMODE_RETRY
MAX_ATTEMPTS=2
RETRYABLE_EXIT_CODE=134
RETRYABLE_CRASH_SIGNATURE='Requested file descriptor exceeds maximum number of files allowed to be open at a time.'
PROJECT_LOCK_FILE="$PROJECT_ROOT/Temp/UnityLockfile"

SCENE_TEMPLATE_SETTINGS="$PROJECT_ROOT/ProjectSettings/SceneTemplateSettings.json"
SCENE_TEMPLATE_SETTINGS_EXISTED=false

if [[ -e "$SCENE_TEMPLATE_SETTINGS" ]]; then
  SCENE_TEMPLATE_SETTINGS_EXISTED=true
fi

cleanup_generated_scene_template_settings() {
  if [[ "$SCENE_TEMPLATE_SETTINGS_EXISTED" == false \
        && -e "$SCENE_TEMPLATE_SETTINGS" ]]; then
    rm -f -- "$SCENE_TEMPLATE_SETTINGS"
  fi
}

trap cleanup_generated_scene_template_settings EXIT

if [[ ! -x "$UNITY_EDITOR" ]]; then
  printf 'Unity Editor introuvable ou non exécutable : %s\n' "$UNITY_EDITOR" >&2
  exit 2
fi

mkdir -p "$RESULTS_DIR"

clear_stale_project_lock() {
  if [[ ! -e "$PROJECT_LOCK_FILE" ]]; then
    return 0
  fi

  local holders
  holders="$(lsof -t "$PROJECT_LOCK_FILE" 2>/dev/null || true)"
  if [[ -n "$holders" ]]; then
    printf 'Le verrou Unity est encore détenu par : %s\n' "$holders" >&2
    return 1
  fi

  rm -f -- "$PROJECT_LOCK_FILE"
}

run_unity_once() {
  rm -f "$RESULTS_FILE" "$LOG_FILE"

  set +e
  "$UNITY_EDITOR" \
    -batchmode \
    -projectPath "$PROJECT_ROOT" \
    -runTests \
    -testPlatform playmode \
    -testResults "$RESULTS_FILE" \
    -logFile "$LOG_FILE"
  UNITY_EXIT_CODE=$?
  set -e
}

is_retryable_unity_crash() {
  [[ $UNITY_EXIT_CODE -eq $RETRYABLE_EXIT_CODE ]] \
    && [[ -s "$LOG_FILE" ]] \
    && grep -Fq "$RETRYABLE_CRASH_SIGNATURE" "$LOG_FILE"
}

clear_stale_project_lock || exit 5
rm -f "$RETRY_LOG_FILE"

attempt=1
while true; do
  run_unity_once

  if [[ $UNITY_EXIT_CODE -eq 0 ]]; then
    break
  fi

  if [[ $attempt -lt $MAX_ATTEMPTS ]] && is_retryable_unity_crash; then
    cp "$LOG_FILE" "$RETRY_LOG_FILE"
    printf 'Crash Unity natif reconnu au lancement PlayMode.\n' >&2
    printf 'Une unique relance propre va être effectuée.\n' >&2
    printf 'Premier journal conservé : %s\n' "$RETRY_LOG_FILE" >&2
    clear_stale_project_lock || exit 5
    attempt=$((attempt + 1))
    sleep 1
    continue
  fi

  printf 'Unity a quitté avec le code %s.\n' "$UNITY_EXIT_CODE" >&2
  printf 'Journal : %s\n' "$LOG_FILE" >&2
  if is_retryable_unity_crash; then
    printf 'La signature Unity native a persisté après la relance unique.\n' >&2
  fi
  exit "$UNITY_EXIT_CODE"
done

if [[ ! -s "$RESULTS_FILE" ]]; then
  printf 'Unity n’a pas produit le fichier de résultats attendu : %s\n' "$RESULTS_FILE" >&2
  exit 3
fi

python3 - "$RESULTS_FILE" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
print(f"result: {root.attrib.get('result')}")
print(f"total: {root.attrib.get('total', '0')}")
print(f"passed: {root.attrib.get('passed', '0')}")
print(f"failed: {root.attrib.get('failed', '0')}")
print(f"skipped: {root.attrib.get('skipped', '0')}")
if root.attrib.get("result") != "Passed" or int(root.attrib.get("failed", "0")) != 0:
    raise SystemExit(4)
PY

printf 'Résultats : %s\n' "$RESULTS_FILE"
printf 'Journal : %s\n' "$LOG_FILE"
