#!/usr/bin/env bash
# glp_gleam/smoke.sh — local WSL gate for the glp_gleam subtree (feature 033, FR-007).
#
# Loudly checks the pinned toolchain, then builds + tests the Erlang/BEAM target.
# Exit 0 iff toolchain OK AND build green AND tests green; non-zero otherwise (SC-005).
# Run under WSL Ubuntu from anywhere:  bash glp_gleam/smoke.sh
# Erlang/BEAM target ONLY — never the JavaScript or AtomVM target (out of scope for F3).
#
# This is a peer gate in the repo's local-gate convention (alongside test/run_all_tests.sh,
# codeconv pytest, buildkit preflight). It is intentionally NOT embedded in
# test/run_all_tests.sh — that suite is the Windows-native dart REPL suite; Gleam needs WSL
# (research.md R-003).
set -euo pipefail

REQ_GLEAM="1.17.0"
MIN_OTP="25"          # Erlang/OTP release FLOOR (not an equality pin).
                      # 2026-08-06: the former `REQ_OTP="25"` exact-equality check was the sole
                      # reason this gate was WSL-only. Verified on Windows OTP 29: `gleam test`
                      # builds and passes 625/625 with zero failures. The subtree depends on no
                      # OTP-25-specific behaviour (gleam.toml pins no OTP release; gleam_stdlib /
                      # gleam_erlang / gleeunit all carry open ranges). The AtomVM constraint in
                      # gleam.toml excludes the gleam_otp PACKAGE (proc_lib), which is unrelated
                      # to the OTP release number. A floor keeps the real requirement (>= 25)
                      # without manufacturing a platform gate.

# Resolve our own directory and cd into the subtree, so cwd does not matter.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

fail() {
  echo "" >&2
  echo "FAIL: $1" >&2
  echo "  Required toolchain: Gleam ${REQ_GLEAM} · Erlang/OTP release >= ${MIN_OTP} (Windows or WSL)" >&2
  exit 1
}

echo "== glp_gleam smoke gate (Erlang/BEAM) =="

# 1. Toolchain check (loud) — never silently pass against an unexpected toolchain.
command -v gleam >/dev/null 2>&1 || fail "gleam not found on PATH."
command -v erl   >/dev/null 2>&1 || fail "erl (Erlang/OTP) not found on PATH."

gleam_ver="$(gleam --version | awk '{print $NF}')"
[ "$gleam_ver" = "$REQ_GLEAM" ] || fail "Gleam ${gleam_ver} found, require ${REQ_GLEAM}."

otp_rel="$(erl -eval 'io:format("~s",[erlang:system_info(otp_release)]),halt().' -noshell)"
case "$otp_rel" in
  ''|*[!0-9]*) fail "Erlang/OTP release '${otp_rel}' is not a plain integer; cannot compare to floor ${MIN_OTP}." ;;
esac
[ "$otp_rel" -ge "$MIN_OTP" ] || fail "Erlang/OTP release ${otp_rel} found, require >= ${MIN_OTP}."

echo "  toolchain OK: Gleam ${gleam_ver} · OTP ${otp_rel}"

# 2. Build (Erlang/BEAM only).
echo "  building (gleam build --target erlang) ..."
gleam build --target erlang || fail "gleam build failed."

# 3. Test (Erlang/BEAM only).
echo "  testing (gleam test --target erlang) ..."
gleam test --target erlang || fail "gleam test failed."

# 4. PASS.
echo ""
echo "PASS: glp_gleam builds + tests green on Erlang/BEAM (Gleam ${gleam_ver} · OTP ${otp_rel})."
exit 0
