#!/usr/bin/env bash
set -euo pipefail
lab="${RUNNER_TEMP:?}/dc-wsus-lab"
mkdir -p "$lab"
sha=$(git -C _app rev-parse HEAD)
for attempt in $(seq 1 90); do
  run=$(gh api "repos/$GITHUB_REPOSITORY/actions/workflows/build-windows.yml/runs?branch=main&head_sha=$sha&per_page=20" --jq ".workflow_runs | map(select(.head_sha == \"$sha\" and .head_branch == \"main\")) | first | .id // empty")
  if [[ -n "$run" ]]; then
    state=$(gh api "repos/$GITHUB_REPOSITORY/actions/runs/$run" --jq '.conclusion // .status')
    if [[ "$state" == success ]]; then break; fi
    case "$state" in failure|cancelled|timed_out|action_required) echo "Windows build $run ended with $state" >&2; exit 1;; esac
  fi
  sleep 30
done
test -n "$run" && test "$state" = success
meta=$(gh api "repos/$GITHUB_REPOSITORY/actions/runs/$run/artifacts" --jq '.artifacts[]|select(.name=="DomainConsole-Windows-x64" and (.expired|not))')
artifact=$(jq -er '.id' <<< "$meta")
expected=$(jq -er '.digest|select(startswith("sha256:"))|sub("sha256:";"")' <<< "$meta")
gh api "repos/$GITHUB_REPOSITORY/actions/artifacts/$artifact/zip" > "$lab/package.zip"
echo "$expected  $lab/package.zip" | sha256sum --check --status
unzip -tqq "$lab/package.zip"
jq -n --arg commit "$sha" --arg run "$run" --arg artifact "$artifact" --arg digest "$expected" '{commit:$commit,run:$run,artifact:$artifact,sha256:$digest}' > "$lab/package.part.json"
mv "$lab/package.part.json" "$lab/package.json"
cat "$lab/package.json"
