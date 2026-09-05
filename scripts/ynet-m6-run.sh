#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
#
# M6 adoption launcher for the shiras/glpnet lane.
#
# THIS IS CONFIGURATION, NOT A CLIENT. The QHSM/QMSM YNET receiver client is
# YngeniOS.Ynet.Client, shipped by ariellas.qhstate as feature 093 and adopted here per its
# 2026-09-05T12:50Z broadcast ("do not author a rival client"). This file supplies the three
# things that broadcast says are lane-specific -- lane id, node id, carrier root -- and nothing
# else. If you find yourself adding behaviour here, you are writing the second client the fleet
# has twice been told not to write.
set -euo pipefail

LANE="${YNET_LANE:-shiras-glpnet}"
NODE="${YNET_NODE:-shiras}"
COOP="${YNET_COOP:-/mnt/gavri/d/coop}"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI="${YNET_CLIENT_DLL:-/mnt/biwin/D_DRIVE/BSTDEV/research/qhstate/Csharp/yngenios/YngeniOS.Ynet.Client.Cli/bin/Release/net11.0/ynet-client.dll}"

if [[ ! -f "$CLI" ]]; then
  echo "ynet-m6: client not built at $CLI" >&2
  echo "  build it:  dotnet build -c Release $(dirname "$(dirname "$(dirname "$CLI")")")" >&2
  exit 1
fi

cd "$REPO"

# The defaults are APPENDED, and only when the caller has not already supplied them.
#
# They used to be spliced in immediately after the subcommand, which put them ahead of any
# positional argument -- so `ynet-m6-run.sh ack <id> --lane L` became
# `ynet-client ack --lane L --node N --coop C <id> --lane L`, the CLI read `--lane` as the alert id,
# and every ack failed with "no such alert: --lane". Appending also means an explicitly passed
# --lane/--node/--coop wins over this lane's default rather than being silently overridden.
SUB="${1:-run}"
shift || true
ARGS=("$@")

supplied() {
  local flag="$1" a
  for a in "${ARGS[@]+"${ARGS[@]}"}"; do
    [[ "$a" == "$flag" ]] && return 0
  done
  return 1
}

supplied --lane || ARGS+=(--lane "$LANE")
supplied --node || ARGS+=(--node "$NODE")
supplied --coop || ARGS+=(--coop "$COOP")

exec dotnet "$CLI" "$SUB" "${ARGS[@]+"${ARGS[@]}"}"
