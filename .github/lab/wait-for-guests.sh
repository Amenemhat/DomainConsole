#!/usr/bin/env bash
set -eu
lab="${RUNNER_TEMP:?}/dc-wsus-lab"
milestone="$1"
deadline=$((SECONDS + 60 * $2))
safe_tail(){
  python3 - "$lab/guest.log" "$1" <<'PYTAIL'
import pathlib, sys
lines=pathlib.Path(sys.argv[1]).read_text(errors="replace").splitlines()
masks=[s[len("::add-mask::"):] for s in lines if s.startswith("::add-mask::")]
for line in [s for s in lines if not s.startswith("::add-mask::")][-int(sys.argv[2]):]:
    for mask in masks:
        line=line.replace(mask,"***")
    print(line)
PYTAIL
}
next_report=$((SECONDS + 90))
while [[ $SECONDS -lt $deadline ]]; do
  if [[ $SECONDS -ge $next_report && -s "$lab/guest.log" ]]; then safe_tail 5; next_report=$((SECONDS + 90)); fi
  if [[ -f "$lab/$milestone.ready" ]]; then echo "$milestone ready"; exit 0; fi
  if [[ -f "$lab/guest-exit" ]]; then
    # Never emit the add-mask command containing the temporary domain password.
    safe_tail 90
    result=$(cat "$lab/guest-exit")
    if [[ "$milestone" == guest-exit ]]; then exit "$result"; fi
    echo "Guest worker stopped before milestone $milestone" >&2; exit 1
  fi
  sleep 10
done
safe_tail 30
echo "Timed out waiting for $milestone" >&2
exit 1
