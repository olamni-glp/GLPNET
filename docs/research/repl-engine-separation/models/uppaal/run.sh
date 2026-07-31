#!/usr/bin/env bash
# Reproduction — UPPAAL timed model of the 061 supervision loop (T030, FR-040).
# CANONICAL run path: WSL2 (real verifyta; see tool-versions.txt). run.ps1 wraps this.
#
# verifyta runs queries.q against supervision.xml:
#   Q1 A[] not deadlock
#   Q2 Engine.Down --> (Engine.Serving || Supervisor.Stopped)   (no silent death)
#   Q3 A[] (!Engine.Serving && !Supervisor.Stopped imply gdead <= BOUND)  (SC-003)
#   Q4 A[] (Supervisor.Stopped imply crashes >= THRESHOLD)      (FR-023)
# PASS = every property "is satisfied". Exit 0 = PASS, 1 = FAIL, 2 = tool blocked.
#
# LICENSE GATE: UPPAAL 5.x verifyta requires a license key (free for academic
# use, but issuance requires registration — an engineer action). Set
# UPPAAL_KEY to your key; without it this script exits 2 with the blocker
# stated loudly (never a fabricated verdict).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERIFYTA="${VERIFYTA:-$HOME/tools/uppaal-5.0.0-linux64/bin/verifyta}"

if [ ! -x "$VERIFYTA" ]; then
  echo "BLOCKED: verifyta not found at $VERIFYTA (set VERIFYTA=... )" >&2
  exit 2
fi

"$VERIFYTA" --version | head -2

KEY_ARGS=()
if [ -n "${UPPAAL_KEY:-}" ]; then
  KEY_ARGS=(--key "$UPPAAL_KEY")
else
  # Probe: without a key the 5.x verifier refuses with a license error.
  if "$VERIFYTA" -u "$HERE/supervision.xml" <(echo 'A[] not deadlock') 2>&1 | grep -qiE "license|key"; then
    echo "BLOCKED: UPPAAL verifier license key not set (export UPPAAL_KEY=...)." >&2
    echo "Obtain a free academic key via uppaal.org (engineer action), then re-run." >&2
    exit 2
  fi
fi

out="$("$VERIFYTA" "${KEY_ARGS[@]}" -u "$HERE/supervision.xml" "$HERE/queries.q" 2>&1)"; echo "$out"

sat=$(echo "$out" | grep -c "is satisfied" || true)
if [ "$sat" -eq 4 ] && ! echo "$out" | grep -q "NOT satisfied"; then
  echo ""
  echo "RESULT: PASS — deadlock-freedom + no-silent-death + SC-003 bound + taxonomy threshold all hold"
  exit 0
fi
echo ""
echo "RESULT: FAIL — one or more properties not satisfied (see output above)" >&2
exit 1
