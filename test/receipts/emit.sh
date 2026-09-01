# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
#
# The bash-side receipt emitter (T042, FR-024).
#
# WHY A SECOND EMITTER AT ALL. The receipt contract is owned by buildkit and has
# two independent implementations on purpose: the Python one in
# `codeconv/src/codeconv/receipts/` and this one. Two emitters that must agree
# byte-for-byte on the same vectors is what keeps either of them honest — a
# single implementation is its own oracle, and its bugs are invisible.
#
# WHAT IT WRITES. Exactly the same document as `codeconv.receipts.emit`:
#
#   <root>/<area>/<run-id>/<check-id>.receipt.json
#
# with the same keys, the same five-valued classification, and the same
# derived-not-declared rule: the CALLER NEVER STATES THE OUTCOME. The caller
# states what it resolved and what it examined; the outcome is computed. A
# caller that could write "PASS" directly is a caller that can lie, which is the
# whole defect this feature closes.
#
# INVOCATION (source it; every function is prefixed `receipt_`):
#
#   . test/receipts/emit.sh
#   receipt_start   <check-id> <area> <target-kind> <target-identity>
#   receipt_examined <item>            # once per item actually examined
#   receipt_total   <n>                # the denominator; omit ⇒ "unknown" ⇒ UNREAD
#   receipt_skip    <item> <reason>    # recorded as a skip, NEVER as a pass
#   receipt_unresolved <reason>        # the target could not be resolved
#   receipt_problem <text>             # a real finding
#   receipt_emit    <run-id> <root>    # writes the file; echoes its path
#
# 🔴 SHELL CONTRACT. `set -o pipefail` is NOT set here, because sourcing a file
# must not silently change the caller's shell options. The measured defect
# (2026-08-31) is that `cmd 2>&1 | tail` returns tail's 0 and masks a refusal in
# bash — so `receipt_emit` writes its diagnostics to stderr AND returns a
# non-zero status the caller can test directly, rather than relying on output.

_RCPT_CHECK_ID=""
_RCPT_AREA=""
_RCPT_TARGET_KIND=""
_RCPT_TARGET_IDENTITY=""
_RCPT_TARGET_REQUESTED=""
_RCPT_RESOLVED=1
_RCPT_UNRESOLVED_REASON=""
_RCPT_EXAMINED=""
_RCPT_EXAMINED_N=0
_RCPT_TOTAL="unknown"
_RCPT_SKIPPED=""
_RCPT_SKIPPED_N=0
_RCPT_PROBLEMS=0
_RCPT_TRUNC_ENUM=false
_RCPT_TRUNC_DROPPED=0
_RCPT_BYTE_CAPPED=""
_RCPT_OVERRIDE=""

# Contract constants. These MUST track codeconv/src/codeconv/receipts/bind.py;
# the parity vectors in assert.sh fail loudly if they drift.
RECEIPT_CONTRACT_VERSION="buildkit-draft-0"
RECEIPT_MAX_ENUM=100
RECEIPT_MAX_FIELD_BYTES=4096

# Byte-backstop ONE field, recording that it was capped -- parity with the Python
# emitter's `_cap_field`. Without this the bash emitter stored an over-long value
# whole and recorded no truncation, so two emitters disagreed on the one contract
# boundary no parity vector exercised (adversarial review 2026-09-01,
# `enforce-the-field-size-cap-in-the-bash-emitter`).
_rcpt_cap_field() {
  _capped_value="$1"
  _n=$(printf '%s' "$1" | wc -c)
  if [ "$_n" -gt "$RECEIPT_MAX_FIELD_BYTES" ]; then
    _capped_value=$(printf '%s' "$1" | cut -c1-"$RECEIPT_MAX_FIELD_BYTES")
    _RCPT_BYTE_CAPPED="${_RCPT_BYTE_CAPPED}${_RCPT_BYTE_CAPPED:+$'\n'}$(printf '%.24s' "$1")…"
  fi
}

receipt_start() {
  _RCPT_CHECK_ID="$1"
  _RCPT_AREA="$2"
  _RCPT_TARGET_KIND="$3"
  _RCPT_TARGET_IDENTITY="$4"
  _RCPT_TARGET_REQUESTED="${5:-}"
  _RCPT_RESOLVED=1
  _RCPT_UNRESOLVED_REASON=""
  _RCPT_EXAMINED=""
  _RCPT_EXAMINED_N=0
  _RCPT_TOTAL="unknown"
  _RCPT_SKIPPED=""
  _RCPT_SKIPPED_N=0
  _RCPT_PROBLEMS=0
  _RCPT_TRUNC_ENUM=false
  _RCPT_TRUNC_DROPPED=0
  _RCPT_BYTE_CAPPED=""
  _RCPT_OVERRIDE=""
}

# JSON string escaping. Deliberately explicit rather than delegating to a tool
# that may not be installed: a receipt that cannot be written because `jq` is
# absent is a check that silently did not report.
_rcpt_json_escape() {
  printf '%s' "$1" | LC_ALL=C sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' \
      -e 's/\t/\\t/g' -e 's/\r/\\r/g' | tr -d '\000' | LC_ALL=C sed -e ':a' -e 'N' -e '$!ba' -e 's/\n/\\n/g'
}

receipt_examined() {
  _RCPT_EXAMINED_N=$(( _RCPT_EXAMINED_N + 1 ))
  if [ "$_RCPT_EXAMINED_N" -le "$RECEIPT_MAX_ENUM" ]; then
    _rcpt_cap_field "$1"
    _RCPT_EXAMINED="${_RCPT_EXAMINED}${_RCPT_EXAMINED:+$'\n'}$_capped_value"
  else
    _RCPT_TRUNC_ENUM=true
    _RCPT_TRUNC_DROPPED=$(( _RCPT_TRUNC_DROPPED + 1 ))
  fi
}

receipt_total() { _RCPT_TOTAL="$1"; }

receipt_skip() {
  _RCPT_SKIPPED_N=$(( _RCPT_SKIPPED_N + 1 ))
  if [ "$_RCPT_SKIPPED_N" -le "$RECEIPT_MAX_ENUM" ]; then
    _RCPT_SKIPPED="${_RCPT_SKIPPED}${_RCPT_SKIPPED:+$'\n'}$1"$'\t'"$2"
  else
    _RCPT_TRUNC_ENUM=true
    _RCPT_TRUNC_DROPPED=$(( _RCPT_TRUNC_DROPPED + 1 ))
  fi
}

receipt_unresolved() { _RCPT_RESOLVED=0; _RCPT_UNRESOLVED_REASON="$1"; }

# An override does NOT change the outcome and does NOT remove the receipt — it
# rides ALONG with it and stays visible (FR-012). Acknowledgement and expiry are
# mandatory: an override with neither is indistinguishable from a check that was
# quietly turned off.
#   receipt_override <area> <check> <reason> <briefing> <rationale> <expiry>
receipt_override() {
  _RCPT_OVERRIDE="{\"area\":\"$(_rcpt_json_escape "$1")\",\"check\":\"$(_rcpt_json_escape "$2")\",\"reason\":\"$(_rcpt_json_escape "$3")\",\"briefing\":\"$(_rcpt_json_escape "$4")\",\"rationale\":\"$(_rcpt_json_escape "$5")\",\"acknowledged\":true,\"expiry\":\"$(_rcpt_json_escape "$6")\"}"
}

receipt_problem() { _RCPT_PROBLEMS=$(( _RCPT_PROBLEMS + 1 )); }

# The classification, mirroring codeconv.receipts.receipt.classify EXACTLY.
# Order matters and is asserted by the parity vectors:
#   unresolved                    -> UNSEARCHABLE
#   total unknown                 -> UNREAD
#   examined < total              -> UNREAD
#   problems                      -> FAIL
#   examined == total == 0        -> EMPTY
#   otherwise                     -> PASS
receipt_classify() {
  if [ "$_RCPT_RESOLVED" -eq 0 ]; then echo "UNSEARCHABLE"; return; fi
  if [ "$_RCPT_TOTAL" = "unknown" ]; then echo "UNREAD"; return; fi
  if [ "$_RCPT_EXAMINED_N" -lt "$_RCPT_TOTAL" ]; then echo "UNREAD"; return; fi
  if [ "$_RCPT_PROBLEMS" -gt 0 ]; then echo "FAIL"; return; fi
  if [ "$_RCPT_EXAMINED_N" -eq 0 ] && [ "$_RCPT_TOTAL" -eq 0 ]; then echo "EMPTY"; return; fi
  echo "PASS"
}

# The same invariants as codeconv.receipts.receipt.validate. A receipt that
# breaks one is NOT written: an impossible receipt on disk is worse than none,
# because a consumer would read it.
receipt_validate() {
  outcome="$1"
  if [ "$_RCPT_EXAMINED_N" -lt 0 ] || [ "$_RCPT_SKIPPED_N" -lt 0 ]; then
    echo "receipt $_RCPT_CHECK_ID: negative count — impossible (FR-010)" >&2; return 1
  fi
  if [ "$_RCPT_TOTAL" != "unknown" ]; then
    if [ "$_RCPT_TOTAL" -lt 0 ]; then
      echo "receipt $_RCPT_CHECK_ID: negative total — impossible (FR-010)" >&2; return 1
    fi
    if [ "$_RCPT_EXAMINED_N" -gt "$_RCPT_TOTAL" ]; then
      echo "receipt $_RCPT_CHECK_ID: examined $_RCPT_EXAMINED_N > total $_RCPT_TOTAL (FR-010)" >&2; return 1
    fi
    if [ $(( _RCPT_EXAMINED_N + _RCPT_SKIPPED_N )) -gt "$_RCPT_TOTAL" ]; then
      echo "receipt $_RCPT_CHECK_ID: examined+skipped exceeds total (FR-010)" >&2; return 1
    fi
  fi
  if [ "$outcome" = "PASS" ]; then
    if [ "$_RCPT_RESOLVED" -eq 0 ] || [ "$_RCPT_TOTAL" = "unknown" ] \
       || [ "$_RCPT_EXAMINED_N" -ne "$_RCPT_TOTAL" ] || [ "$_RCPT_EXAMINED_N" -le 0 ]; then
      echo "receipt $_RCPT_CHECK_ID: PASS is earned, not assumed (FR-006/007)" >&2; return 1
    fi
  fi
  if [ "$outcome" = "EMPTY" ]; then
    if [ "$_RCPT_RESOLVED" -eq 0 ] || [ "$_RCPT_TOTAL" != "0" ] || [ "$_RCPT_EXAMINED_N" -ne 0 ]; then
      echo "receipt $_RCPT_CHECK_ID: EMPTY requires a resolved target at 0/0 (FR-006)" >&2; return 1
    fi
  fi
  if [ "$outcome" = "UNSEARCHABLE" ] && [ -z "$_RCPT_UNRESOLVED_REASON" ]; then
    echo "receipt $_RCPT_CHECK_ID: UNSEARCHABLE requires a reason" >&2; return 1
  fi
  if [ "$_RCPT_TRUNC_ENUM" = true ] && [ "$_RCPT_TRUNC_DROPPED" -le 0 ]; then
    echo "receipt $_RCPT_CHECK_ID: truncated without recording how many dropped" >&2; return 1
  fi
  return 0
}

receipt_emit() {
  run_id="$1"
  root="$2"
  ran_at="${3:-$(date -u +%Y-%m-%dT%H:%M:%S+00:00)}"

  case "$_RCPT_AREA$run_id$_RCPT_CHECK_ID" in
    */*|*\\*) echo "receipt: path separator in area/run/check id — refused (FR-022)" >&2; return 64 ;;
  esac
  if [ -z "$_RCPT_AREA" ] || [ -z "$run_id" ] || [ -z "$_RCPT_CHECK_ID" ]; then
    echo "receipt: empty area/run/check id — refused (FR-022)" >&2; return 64
  fi
  # '.' and '..' carry no separator but still escape the root: area='..' writes
  # the receipt one level ABOVE the receipts root. The Python `_safe_component`
  # refuses them explicitly; this did not, so the two emitters disagreed on
  # containment (adversarial review 2026-09-01,
  # `reject-dot-path-components-in-the-bash-emitter`).
  for _comp in "$_RCPT_AREA" "$run_id" "$_RCPT_CHECK_ID"; do
    case "$_comp" in
      .|..) echo "receipt: '$_comp' is not a usable path component — refused (FR-022)" >&2; return 64 ;;
    esac
  done

  outcome="$(receipt_classify)"
  if ! receipt_validate "$outcome"; then return 65; fi

  dir="$root/$_RCPT_AREA/$run_id"
  mkdir -p "$dir" || return 66
  out="$dir/$_RCPT_CHECK_ID.receipt.json"

  examined_json=""
  if [ -n "$_RCPT_EXAMINED" ]; then
    while IFS= read -r item; do
      [ -z "$item" ] && continue
      examined_json="${examined_json}${examined_json:+,}\"$(_rcpt_json_escape "$item")\""
    done <<EOF
$_RCPT_EXAMINED
EOF
  fi

  skipped_json=""
  if [ -n "$_RCPT_SKIPPED" ]; then
    while IFS=$'\t' read -r item reason; do
      [ -z "$item" ] && continue
      skipped_json="${skipped_json}${skipped_json:+,}{\"item\":\"$(_rcpt_json_escape "$item")\",\"reason\":\"$(_rcpt_json_escape "$reason")\"}"
    done <<EOF
$_RCPT_SKIPPED
EOF
  fi

  if [ "$_RCPT_TOTAL" = "unknown" ]; then total_json='"unknown"'; else total_json="$_RCPT_TOTAL"; fi
  if [ "$_RCPT_RESOLVED" -eq 1 ]; then resolved_json=true; else resolved_json=false; fi

  target_json="{\"kind\":\"$(_rcpt_json_escape "$_RCPT_TARGET_KIND")\",\"identity\":\"$(_rcpt_json_escape "$_RCPT_TARGET_IDENTITY")\",\"resolved\":$resolved_json"
  if [ -n "$_RCPT_TARGET_REQUESTED" ] && [ "$_RCPT_TARGET_REQUESTED" != "$_RCPT_TARGET_IDENTITY" ]; then
    target_json="$target_json,\"requested\":\"$(_rcpt_json_escape "$_RCPT_TARGET_REQUESTED")\""
  fi
  if [ -n "$_RCPT_UNRESOLVED_REASON" ]; then
    target_json="$target_json,\"unresolved_reason\":\"$(_rcpt_json_escape "$_RCPT_UNRESOLVED_REASON")\""
  fi
  target_json="$target_json}"

  byte_capped_json=""
  if [ -n "$_RCPT_BYTE_CAPPED" ]; then
    while IFS= read -r _bc; do
      [ -z "$_bc" ] && continue
      byte_capped_json="${byte_capped_json}${byte_capped_json:+,}\"$(_rcpt_json_escape "$_bc")\""
    done <<EOFBC
$_RCPT_BYTE_CAPPED
EOFBC
  fi

  override_tail=""
  if [ -n "$_RCPT_OVERRIDE" ]; then override_tail=",
  \"override\": $_RCPT_OVERRIDE"; fi

  cat > "$out" <<EOF
{
  "schema_version": "$RECEIPT_CONTRACT_VERSION",
  "contract_version": "$RECEIPT_CONTRACT_VERSION",
  "check_id": "$(_rcpt_json_escape "$_RCPT_CHECK_ID")",
  "area": "$(_rcpt_json_escape "$_RCPT_AREA")",
  "run_id": "$(_rcpt_json_escape "$run_id")",
  "resolved_target": $target_json,
  "outcome": "$outcome",
  "examined_count": $_RCPT_EXAMINED_N,
  "total_count": $total_json,
  "skipped": [$skipped_json],
  "skipped_total": $_RCPT_SKIPPED_N,
  "examined": [$examined_json],
  "truncated": {"enumerations": $_RCPT_TRUNC_ENUM, "dropped": $_RCPT_TRUNC_DROPPED, "byte_capped": [$byte_capped_json]},
  "ran_at": "$ran_at",
  "verdict_pointer": "$(_rcpt_json_escape "$out")"$override_tail
}
EOF
EOF_STATUS=$?
  # The heredoc redirection above can fail (read-only dir, full disk). Returning
  # 0 regardless made the emitter report a receipt path for a receipt that was
  # never written -- a check claiming evidence it does not have (adversarial
  # review 2026-09-01, `propagate-bash-receipt-write-failures`).
  if [ "$EOF_STATUS" -ne 0 ] || [ ! -s "$out" ]; then
    echo "receipt $_RCPT_CHECK_ID: FAILED to write $out (status $EOF_STATUS)" >&2
    return 67
  fi
  printf '%s\n' "$out"
  return 0
}
