#!/usr/bin/env bash
set -euo pipefail
lab="${RUNNER_TEMP:?}/dc-wsus-lab"
attempt="$1"
path='.github/tests/domain-scenario'
previous=''
if ((attempt>1)); then previous=$(cat "$lab/scenario-$((attempt-1)).signature"); fi
until_time=$((SECONDS+45*60))
while :; do
  git fetch --quiet --no-tags origin refs/heads/lab/0.57-wsus
  tree=$(git rev-parse "FETCH_HEAD:$path")
  candidate=$(git show FETCH_HEAD:docs/testing/candidate-0.57.sha)
  [[ "$candidate" =~ ^[a-f0-9]{40}$ ]]
  signature="$tree $candidate"
  if [[ "$signature" != "$previous" ]]; then break; fi
  if ((SECONDS>=until_time)); then echo "No corrected scenario was supplied"; exit 1; fi
  sleep 40
done
mkdir -p "$lab/scenario-$attempt"
git archive "$tree" | tar -x -C "$lab/scenario-$attempt"
if [[ "$(git -C _app rev-parse HEAD)" != "$candidate" ]]; then
  git fetch --quiet --no-tags origin refs/heads/main
  git merge-base --is-ancestor "$candidate" FETCH_HEAD
  git -C _app fetch --quiet --no-tags "$PWD" "$candidate"
  git -C _app checkout --quiet --detach "$candidate"
  bash .github/lab/fetch-package.sh
fi
echo "$signature" > "$lab/scenario-$attempt.signature"
echo "$tree" > "$lab/scenario-$attempt.tree"
echo "Scenario revision $tree"
bash .github/lab/wait-for-guests.sh "attempt-$attempt" 85
python3 - "$lab/attempt-$attempt.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]))
print("Scenario passed" if r['passed'] else r['error'])
raise SystemExit(0 if r['passed'] else 1)
PY
