#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
#
# The C# suites, split the way the engineer ruled on 2026-09-08:
#   the DEFAULT gate excludes category=disclosure; a separate NAMED step runs the disclosure set
#   and REPORTS its verdict without gating.
#
# 🔴 WHY THE SPLIT EXISTS, so nobody "tidies" it away.
#   Feature 108 T025 carries two tests that are RED BY DESIGN. They disclose a defect in
#   YngeniOS.Ynet.Client, which Q-glpnetshiras-50 rules canonical and this lane may not patch.
#   Deleting them would delete the disclosure; leaving them in the gate would make this suite
#   permanently red, and a permanently red suite is how the NEXT real regression goes unnoticed.
#   So: the gate means "something broke here", and the disclosure step means "the thing we already
#   told @ariellas-qhstate about is still true". Both are printed. Neither is hidden.
#
#   When @ariellas-qhstate lands the merge-by-message_id replay fix, the disclosure step turns
#   GREEN with no edit here — that is their acceptance gate.
#
# Exit: 0 only if the GATED suites are green. The disclosure step never changes the exit code;
#       it is reported, and its count is stated, never folded into the gated total.

set -uo pipefail
cd "$(dirname "$0")/.." || exit 2

DOTNET=(env -u LD_LIBRARY_PATH dotnet)
GATED=0

run_gated() {
  local proj="$1" name="$2"
  echo "--- GATED: $name"
  "${DOTNET[@]}" test "$proj" -c Release --filter "category!=disclosure" 2>&1 \
    | grep -E "^(Passed!|Failed!)|^  Failed " || true
  local rc=${PIPESTATUS[0]}
  [ "$rc" -ne 0 ] && GATED=1
  return 0
}

echo "=== C# suites — $(date -u +%Y-%m-%dT%H:%M:%SZ) host=$(hostname) ==="
run_gated csharp/ynet_transport.tests/YnetTransport.Tests.csproj "ynet_transport"
run_gated csharp/ynet_client.tests/YnetClient.Tests.csproj       "ynet_client"

echo
echo "--- REPORTED, NOT GATED: the 108 T025 disclosure set (expected RED until the upstream fix)"
"${DOTNET[@]}" test csharp/ynet_client.tests/YnetClient.Tests.csproj -c Release \
  --filter "category=disclosure" 2>&1 \
  | grep -E "^(Passed!|Failed!)|^  (Failed|Passed) " || true
echo "    ^ RED here is the disclosure working. GREEN here means the upstream replay fix has landed"
echo "      — when that happens, tell @ariellas-qhstate and retire specs/108 T025's red note."

echo
if [ "$GATED" -eq 0 ]; then echo "GATED SUITES: GREEN"; else echo "GATED SUITES: FAILED"; fi
exit "$GATED"
