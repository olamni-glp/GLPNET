#!/usr/bin/env bash
# Opt-in scheduled refinement trigger (spec-007 FR-018c). OFF by default:
# no-op unless .specify/refine.json sets triggers.scheduled=true.
# Single-flight + fail-safe enforced in the runner. Exit: 0 ran/disabled,
# 1 failure, 2 DB down.
#
# cron invokes this with an arbitrary working directory, so resolve the
# project root from THIS script's location (walk up to the dir containing
# .specify) and pin BUILDKIT_PROJECT_ROOT — otherwise the module resolves
# from cwd and usually no-ops as disabled (or targets the wrong repo).
set -euo pipefail

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  echo "Usage: ./refine-scheduled.sh   # opt-in; off unless triggers.scheduled=true"
  exit 0
fi

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
while [[ -n "$dir" && ! -d "$dir/.specify" ]]; do
  parent="$(dirname "$dir")"
  [[ "$parent" == "$dir" ]] && { dir=""; break; }
  dir="$parent"
done
[[ -n "$dir" ]] && export BUILDKIT_PROJECT_ROOT="$dir"

python -m buildkit_cli.refine scheduled
