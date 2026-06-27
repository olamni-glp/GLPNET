#!/usr/bin/env bash
# Refinement self-check (spec-007 FR-007/SC-004). Thin wrapper around the
# module CLI so it works the moment the wheel is installed.
# Resolve the project root from THIS script's location (an installer may
# invoke it from an arbitrary cwd). Exit: 0 ready, 0 degraded, 2 DB down.
set -euo pipefail

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  echo "Usage: ./refine-selfcheck.sh   # prints 'refine: ready' | 'refine: degraded (...)'"
  exit 0
fi

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
while [[ -n "$dir" && ! -d "$dir/.specify" ]]; do
  parent="$(dirname "$dir")"
  [[ "$parent" == "$dir" ]] && { dir=""; break; }
  dir="$parent"
done
[[ -n "$dir" ]] && export BUILDKIT_PROJECT_ROOT="$dir"

python -m buildkit_cli.refine selfcheck
