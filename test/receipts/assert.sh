#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
#
# Harness-side receipt assertions + the 7/7 cross-emitter parity run (T043, T044).
#
# WHAT THIS PROVES. `codeconv/tests/faultinj/conformance.py` drives the PYTHON
# emitter through seven declared cases. This drives the BASH emitter through the
# same seven with the same inputs and asserts the two documents are identical
# once the two fields that are legitimately allowed to differ — `ran_at` and
# `verdict_pointer` — are pinned or normalised. That is what FR-024 means by
# "two emitters kept honest": neither is its own oracle.
#
# 🔴 `set -o pipefail` IS set here, deliberately, and this is the reason.
# Measured 2026-08-31 on this host: in bash, `failing_cmd 2>&1 | tail -1` exits 0
# — tail's status, not the command's — which is how a refusal gets read as a
# pass. This harness pipes, so without pipefail it would be a live instance of
# the very class 078 exists to close. PowerShell is immune ($LASTEXITCODE
# survives the pipe); an invocation-hygiene rule is a property of the SHELL.
set -u -o pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$HERE/emit.sh"

PY="${CODECONV_PYTHON:-$REPO/codeconv/.venv/Scripts/python.exe}"
[ -x "$PY" ] || PY="${CODECONV_PYTHON:-python}"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
BASH_ROOT="$TMP/bash"
PY_ROOT="$TMP/py"
RUN_ID="parity"
RAN_AT="2026-09-01T00:00:00+00:00"

PASSED=0
FAILED=0
EXERCISED=()

ok()   { PASSED=$((PASSED+1)); printf '  ok   %s\n' "$1"; }
fail() { FAILED=$((FAILED+1)); printf '  FAIL %s\n' "$1" >&2; }

# ---------------------------------------------------------------------------
# assert_outcome <expected> <label>  — the bash emitter's own classification
# ---------------------------------------------------------------------------
assert_outcome() {
  local expected="$1" label="$2" got
  got="$(receipt_classify)"
  if [ "$got" = "$expected" ]; then ok "$label -> $expected"; else
    fail "$label: classified $got, expected $expected"
  fi
}

# ---------------------------------------------------------------------------
# assert_refused <label>  — the emitter MUST refuse to write an impossible receipt
# ---------------------------------------------------------------------------
assert_refused() {
  local label="$1" rc
  receipt_emit "$RUN_ID" "$BASH_ROOT" "$RAN_AT" >/dev/null 2>&1
  rc=$?
  if [ "$rc" -ne 0 ]; then ok "$label refused (rc=$rc)"; else
    fail "$label was WRITTEN — an impossible receipt must be refused (FR-010)"
  fi
}

# ---------------------------------------------------------------------------
# parity <case> — same inputs to both emitters, then compare the documents
# ---------------------------------------------------------------------------
parity() {
  local case="$1" check_id="$2"
  local a="$BASH_ROOT/reference/$RUN_ID/$check_id.receipt.json"
  local b="$PY_ROOT/reference/$RUN_ID/$check_id.receipt.json"
  if [ ! -f "$a" ]; then fail "$case: bash emitter wrote no receipt"; return; fi
  if [ ! -f "$b" ]; then fail "$case: python emitter wrote no receipt"; return; fi
  if "$PY" "$HERE/parity_compare.py" "$a" "$b"; then
    ok "$case parity"
    EXERCISED+=("$case")
  else
    fail "$case parity"
  fi
}

echo "== bash emitter: classification =="

receipt_start conformance.pass reference path t
receipt_examined a; receipt_examined b; receipt_examined c
receipt_examined d; receipt_examined e
receipt_total 5
assert_outcome PASS "PASS case"
receipt_emit "$RUN_ID" "$BASH_ROOT" "$RAN_AT" >/dev/null

receipt_start conformance.empty reference path t
receipt_total 0
assert_outcome EMPTY "EMPTY case"
receipt_emit "$RUN_ID" "$BASH_ROOT" "$RAN_AT" >/dev/null

receipt_start conformance.unread reference path t
receipt_examined a
receipt_total 3
assert_outcome UNREAD "UNREAD case"
receipt_emit "$RUN_ID" "$BASH_ROOT" "$RAN_AT" >/dev/null

receipt_start conformance.unsearchable reference path t
receipt_unresolved "target absent"
assert_outcome UNSEARCHABLE "UNSEARCHABLE case"
receipt_emit "$RUN_ID" "$BASH_ROOT" "$RAN_AT" >/dev/null

receipt_start conformance.fail reference path t
for i in 1 2 3 4 5; do receipt_examined "item-$i"; done
receipt_total 5
receipt_problem "a problem"
assert_outcome FAIL "FAIL case"
receipt_emit "$RUN_ID" "$BASH_ROOT" "$RAN_AT" >/dev/null

# BOUNDED: MAX_ENUM + 7 items. The enumeration caps; the TOTALS survive (FR-005).
receipt_start conformance.bounded reference path t
n=$(( RECEIPT_MAX_ENUM + 7 ))
i=0
while [ "$i" -lt "$n" ]; do receipt_examined "item-$i"; i=$((i+1)); done
receipt_total "$n"
assert_outcome PASS "BOUNDED case"
receipt_emit "$RUN_ID" "$BASH_ROOT" "$RAN_AT" >/dev/null

receipt_start conformance.overridden reference path t
receipt_examined a
receipt_total 1
receipt_override reference conformance.overridden \
  "conformance fixture exercises the recorded-override case" \
  "contract F1 requires the fixture to drive an overridden case" \
  "demonstrates an override remains visible in the emitted receipt" \
  "2099-01-01T00:00:00+00:00"
assert_outcome PASS "OVERRIDDEN case"
receipt_emit "$RUN_ID" "$BASH_ROOT" "$RAN_AT" >/dev/null

echo
echo "== bash emitter: refusals (F3 / FR-010) =="

# A falsified count: 10 examined out of a declared total of 1.
receipt_start conformance.falsified reference path t
i=0; while [ "$i" -lt 10 ]; do receipt_examined "x$i"; i=$((i+1)); done
receipt_total 1
assert_refused "falsified count (examined 10 > total 1)"

# examined + skipped exceeding the total: six outcomes from a five-item target.
receipt_start conformance.oversum reference path t
i=0; while [ "$i" -lt 5 ]; do receipt_examined "x$i"; i=$((i+1)); done
receipt_skip "s1" "unsupported platform"
receipt_total 5
assert_refused "examined+skipped exceeds total"

echo
echo "== instance 5: a skip is NEVER a pass =="
# Instance 5 (RT-24/28/29/16): an unsupported-platform link reported as
# passed-by-skip. Recorded as a skip with a reason, the receipt cannot be PASS.
receipt_start harness.skip-guard test-harness path "test/run_all_tests.sh"
receipt_skip "section-N-link" "unsupported platform: no QUIC transport on this host"
receipt_total 1
got="$(receipt_classify)"
if [ "$got" = "UNREAD" ]; then
  ok "skip-guard: 0 examined / 1 total / 1 skipped -> UNREAD, not passed-by-skip"
  EXERCISED+=("instance-5")
else
  fail "skip-guard classified $got; a passed-by-skip is instance 5 (expected UNREAD)"
fi

echo
echo "== instance 7: corpus scope gated by nothing is UNREAD, not clean =="
# Instance 7 (D8-11/12/14): corpus tools are manual-only, so the suite gates
# corpus scope by nothing at all. A declared corpus of 13 chapters with 0
# examined is UNREAD; reporting it clean is the defect.
receipt_start harness.corpus-scope test-harness item-set "glptutorial corpus chapters"
receipt_total 13
got="$(receipt_classify)"
if [ "$got" = "UNREAD" ]; then
  ok "corpus-scope: 0 examined / 13 declared -> UNREAD, not clean"
  EXERCISED+=("instance-7")
else
  fail "corpus-scope classified $got; gating by nothing must not be clean (expected UNREAD)"
fi

echo
echo "== cross-emitter parity: 7 conformance vectors (T044, FR-024) =="
if ! "$PY" "$HERE/parity_vectors.py" "$PY_ROOT" "$RUN_ID" "$RAN_AT"; then
  fail "python emitter could not produce the parity vectors"
else
  parity PASS         conformance.pass
  parity EMPTY        conformance.empty
  parity UNREAD       conformance.unread
  parity UNSEARCHABLE conformance.unsearchable
  parity FAIL         conformance.fail
  parity BOUNDED      conformance.bounded
  parity OVERRIDDEN   conformance.overridden
fi

echo
echo "== summary =="
PARITY_CASES=0
for c in "${EXERCISED[@]:-}"; do case "$c" in instance-*) ;; *) PARITY_CASES=$((PARITY_CASES+1));; esac; done
echo "  parity vectors exercised: $PARITY_CASES/7"
echo "  assertions: $PASSED passed, $FAILED failed"

# Emit the harness's own receipt. `examined` names the instances this run
# actually demonstrated, so the pytest-side registry can absorb them (only from
# a SUCCESSFUL receipt — a failing harness proves nothing about the injections).
if [ -n "${CODECONV_RECEIPTS_ROOT:-}" ] && [ -n "${CODECONV_RECEIPTS_RUN_ID:-}" ]; then
  receipt_start harness.receipts-parity test-harness path "test/receipts/assert.sh"
  receipt_total 2
  if [ "$FAILED" -eq 0 ] && [ "$PARITY_CASES" -eq 7 ]; then
    receipt_examined "instance:5"
    receipt_examined "instance:7"
  fi
  receipt_emit "$CODECONV_RECEIPTS_RUN_ID" "$CODECONV_RECEIPTS_ROOT" >/dev/null || true
fi

if [ "$FAILED" -ne 0 ] || [ "$PARITY_CASES" -ne 7 ]; then
  echo "  RESULT: FAIL" >&2
  exit 1
fi
echo "  RESULT: 7/7 parity, all assertions pass"
exit 0
