#!/usr/bin/env bash
set -euo pipefail
lab="${RUNNER_TEMP:?}/dc-wsus-lab"
nohup sudo -E bash -c '
  /usr/bin/python3 -u .github/lab/windows-lab.py "$1"
  echo "$?" > "$1/guest-exit"
' bash "$lab" > "$lab/guest.log" 2>&1 < /dev/null &
