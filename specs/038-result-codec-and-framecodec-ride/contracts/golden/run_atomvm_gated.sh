#!/usr/bin/env bash
# T039 (float 0x03) / T040 (64-bit-int edge) — run the REAL Gleam term codec on AtomVM
# (a BEAM-alternative WASM VM) under Node.js, no browser and no Linux distro, and verify
# byte-identity + round-trip for the gated corpus. See ATOMVM.md for the one-time
# node-target rebuild of AtomVM.mjs (AVM_EMSCRIPTEN_ENV=node → -sNODERAWFS/-sENVIRONMENT=node).
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
GLEAM="${GLEAM:-/c/Users/smbuser/AppData/Local/Microsoft/WinGet/Packages/Gleam.Gleam_Microsoft.Winget.Source_8wekyb3d8bbwe/gleam.exe}"
ERLANG_BIN="${ERLANG_BIN:-/c/Program Files/Erlang OTP/bin}"
AVM_MJS="${AVM_MJS:-/c/Users/smbuser/toolchain/AtomVM/src/platforms/emscripten/build/src/AtomVM.mjs}"
NODE="${NODE:-/c/Program Files/nodejs/node.exe}"

if [ ! -f "$AVM_MJS" ]; then
  echo "AtomVM.mjs not found at $AVM_MJS — build the node target first (see ATOMVM.md)."; exit 2
fi

cd "$REPO/glp_gleam"
EB=build/dev/erlang
# Fail hard if the Gleam build errors (codex P2): a failing `gleam build` exits non-zero,
# so a stale-beam false-pass is unreachable — the gate stops here instead of running old
# artifacts. Beam-existence + Node-exit are checked below as belt-and-braces.
if ! PATH="$ERLANG_BIN:$PATH" "$GLEAM" build --target erlang >/dev/null; then
  echo "ATOMVM GATED: FAIL — gleam build --target erlang failed"; exit 1
fi

beams=( "$EB/glp_gleam/ebin/atomvm_gated_probe.beam" "$EB/glp_gleam/ebin/glp@codec@term_codec.beam" )
for m in gleam@int gleam@bit_array gleam@result gleam@order gleam@list gleam@bool gleam@option gleam_stdlib; do
  beams+=( "$(find "$EB" -name "$m.beam" | head -1)" )
done
for b in "${beams[@]}"; do
  if [ ! -f "$b" ]; then echo "ATOMVM GATED: FAIL — missing beam after build: $b"; exit 1; fi
done
abs=(); for b in "${beams[@]}"; do abs+=( "$(cd "$(dirname "$b")" && pwd)/$(basename "$b")" ); done

# NB: this AtomVM (emscripten) build exits non-zero even on a successful run (its default
# init-module probe fails first), so the process exit code is NOT a usable success signal.
# The authoritative signal is the OUTPUT content asserted below: a crashing/wrong probe
# cannot emit the expected byte lines + 3 round-trip `true`s, so the checks still FAIL loud.
out="$("$NODE" "$AVM_MJS" "${abs[@]}" 2>&1)"
echo "$out" | grep -vE "streaming|fallback|prepare wasm|pthread|Downloading|Failed load module: init"

check() { echo "$out" | grep -qF "$1" || { echo "MISSING expected: $1"; return 1; }; }
rc=0
check '<<2,255,255,255,255,255,255,255,127>>' || rc=1   # T040 int64 max
check '<<2,0,0,0,0,0,0,0,128>>'               || rc=1   # T040 int64 min (two's-complement LE)
check '<<3,24,45,68,84,251,33,9,64>>'         || rc=1   # T039 float Pi (IEEE-754 LE)
[ "$(echo "$out" | grep -cx true)" = "3" ] || { echo "round-trip trues != 3"; rc=1; }

echo
if [ "$rc" = 0 ]; then
  echo "ATOMVM GATED: PASS — float 0x03 + int64 edges byte-identical + round-trip on AtomVM"
else
  echo "ATOMVM GATED: FAIL"
fi
exit "$rc"
