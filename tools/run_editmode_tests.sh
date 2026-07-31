#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity}"

RESULTS_DIR="$PROJECT_ROOT/TestResults"
RESULTS_FILE="$RESULTS_DIR/editmode-results.xml"
LOG_FILE="$RESULTS_DIR/editmode.log"

if [[ ! -x "$UNITY_EDITOR" ]]; then
  printf 'Unity Editor introuvable ou non exécutable : %s\n' "$UNITY_EDITOR" >&2
  exit 2
fi

mkdir -p "$RESULTS_DIR"
rm -f "$RESULTS_FILE" "$LOG_FILE"

set +e
"$UNITY_EDITOR" \
  -batchmode \
  -nographics \
  -projectPath "$PROJECT_ROOT" \
  -runTests \
  -testPlatform editmode \
  -testResults "$RESULTS_FILE" \
  -logFile "$LOG_FILE"
UNITY_EXIT_CODE=$?
set -e

if [[ $UNITY_EXIT_CODE -ne 0 ]]; then
  printf 'Unity a quitté avec le code %s.\n' "$UNITY_EXIT_CODE" >&2
  printf 'Journal : %s\n' "$LOG_FILE" >&2
  exit "$UNITY_EXIT_CODE"
fi

if [[ ! -s "$RESULTS_FILE" ]]; then
  printf 'Unity n’a pas produit le fichier de résultats attendu : %s\n' "$RESULTS_FILE" >&2
  printf 'Journal : %s\n' "$LOG_FILE" >&2
  exit 3
fi

python3 - "$RESULTS_FILE" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()

result = root.attrib.get("result")
total = int(root.attrib.get("total", "0"))
passed = int(root.attrib.get("passed", "0"))
failed = int(root.attrib.get("failed", "0"))
skipped = int(root.attrib.get("skipped", "0"))

print(f"result: {result}")
print(f"total: {total}")
print(f"passed: {passed}")
print(f"failed: {failed}")
print(f"skipped: {skipped}")

if result != "Passed" or failed != 0:
    raise SystemExit(4)
PY

printf 'Résultats : %s\n' "$RESULTS_FILE"
printf 'Journal : %s\n' "$LOG_FILE"
