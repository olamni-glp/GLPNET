#!/usr/bin/env bash
# =============================================================================
# test/service_box/history_drill.sh — 064 US2 history drill (T009, SC-002).
#
#   1. Send N messages to the registered service (each is its own one-bind link,
#      the proven quic_chat_onebind shape) — every one is durably appended by the
#      host BEFORE the program observes it (FR-004).
#   2. Restart: the service re-arms and REPLAYS the full history to the program,
#      in receipt order, exactly once — the program echoes each replayed message
#      via '_output' (the drill listener), so the transcript is the assertion.
#   3. Restart again: the replayed history must equal the previous restart's
#      full sequence plus the one live probe it durably appended (replay
#      idempotence — replay reads the log, never re-appends it).
#
# N defaults to 12 (each message is a full QUIC one-bind cycle; SC-002's 100 is
# the release gate — pass N as $1 for the full run).
# =============================================================================
set -u
# Drills own a fresh file WAL; a shared pglite journal must never be touched by tests.
unset COLAB_PG_CONN
SB_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CSREPL="$SB_ROOT/out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe"
REG="$SB_ROOT/glpservice/resume.json"
WAL="$SB_ROOT/glpservice/wal"
N="${1:-12}"
PORT=9209
PASS=0; FAIL=0
check() { if [ "$2" -eq 0 ]; then echo "  PASS: $1"; PASS=$((PASS+1)); else echo "  FAIL: $1"; FAIL=$((FAIL+1)); fi }

[ -f "$CSREPL" ] || { echo "ERROR: C# REPL not built at $CSREPL"; exit 1; }

SAVED=""
if [ -f "$REG" ]; then SAVED="$REG.drill-saved"; mv "$REG" "$SAVED"; fi
# The drill owns a FRESH log: history assertions are about this run only. Any real
# service WAL is moved aside (like the registration) and restored intact on exit;
# the drill's own WAL is removed so replay:true never sees drill garbage later.
SAVED_WAL=""
if [ -d "$WAL" ]; then SAVED_WAL="$WAL.drill-saved"; mv "$WAL" "$SAVED_WAL"; fi
restore() { rm -f "$REG"; rm -rf "$WAL"; if [ -n "$SAVED" ] && [ -f "$SAVED" ]; then mv "$SAVED" "$REG"; fi; if [ -n "$SAVED_WAL" ] && [ -d "$SAVED_WAL" ]; then mv "$SAVED_WAL" "$WAL"; fi }
trap restore EXIT

write_reg() {  # $1 = replay flag
    cat > "$REG" <<EOF
{ "version": 1, "program": "test/service_box/drill_listener.glp", "goal": "drill_recv(\\"127.0.0.1\\", $PORT, Got).", "enabled": true, "replay": $1 }
EOF
}

send_one() {  # $1 = message text (registration must be absent so the peer does not re-arm)
    ( cd "$SB_ROOT" && printf 'load ./programs/tests/quic/quic_chat_onebind.glp\nsend("127.0.0.1", %s, drill, box, "%s", R).\n:quit\n' "$PORT" "$1" \
        | timeout 60 "$CSREPL" ) > /dev/null 2>&1
}

echo "=== 064 US2 history drill (N=$N) ==="

# --- Phase 1: N messages into a fresh log --------------------------------------
for i in $(seq 1 "$N"); do
    write_reg false
    ( printf ':quit\n' | timeout 40 "$CSREPL" > "/tmp/history_fill.$i.out" 2>&1 ) &
    RXPID=$!
    sleep 5
    rm -f "$REG"                 # the peer must not inherit the registration
    send_one "msg-$i"
    wait "$RXPID"
done
grep -c "received(" /tmp/history_fill."$N".out >/dev/null 2>&1
check "fill: final message observed by the service" $?

# --- Phase 2: restart WITH replay → full history, in order, once ---------------
write_reg true
( printf ':quit\n' | timeout 40 "$CSREPL" > /tmp/history_replay1.out 2>&1 ) &
RXPID=$!
sleep 6
rm -f "$REG"
# A peer connects: the link establishes, so the durable history replays into the
# service FIRST (drained before the receive loop starts) and this live message
# lands after it — history-precedes-live on the same link.
send_one "live-probe"
wait "$RXPID" 2>/dev/null
# The FULL token sequence (msg-N and live-probe): a duplicated or interleaved
# live-probe must be visible to the assertion, not filtered out.
grep -oE 'msg-[0-9]+|live-probe' /tmp/history_replay1.out > /tmp/history_replay1.seq
EXPECTED=$(for i in $(seq 1 "$N"); do echo "msg-$i"; done; echo "live-probe")
ACTUAL=$(cat /tmp/history_replay1.seq)
[ "$EXPECTED" = "$ACTUAL" ]
check "replay: all $N messages, receipt order, no duplicates, then one live probe (SC-002)" $?
grep -q "resume: replayed $N message(s)" /tmp/history_replay1.out
check "replay: host reported replaying $N message(s)" $?

# --- Phase 3: restart again, no traffic → byte-identical replay ----------------
write_reg true
( printf ':quit\n' | timeout 40 "$CSREPL" > /tmp/history_replay2.out 2>&1 ) &
RXPID=$!
sleep 6
rm -f "$REG"
# A peer connects: the link establishes, so the durable history replays into the
# service FIRST (drained before the receive loop starts) and this live message
# lands after it — history-precedes-live on the same link.
send_one "live-probe"
wait "$RXPID" 2>/dev/null
# Phase 2's live-probe was durably appended, so this restart's replay is phase 2's
# replay PLUS that one live-probe; this launch's own live-probe then lands after it.
# Full-sequence identity: seq3 == seq2 + ["live-probe"] — deterministic, and a
# duplicated live-probe (invisible to a msg-N-only grep) fails it.
grep -oE 'msg-[0-9]+|live-probe' /tmp/history_replay2.out > /tmp/history_replay2.seq
{ cat /tmp/history_replay1.seq; echo "live-probe"; } > /tmp/history_replay2.expected
diff -q /tmp/history_replay2.expected /tmp/history_replay2.seq >/dev/null 2>&1
check "idempotence: second restart replays identical history plus the appended live probe (SC-002)" $?

echo "======================================"
echo "history drill: PASS=$PASS FAIL=$FAIL"
echo "======================================"
exit "$FAIL"
