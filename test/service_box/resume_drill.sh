#!/usr/bin/env bash
# =============================================================================
# test/service_box/resume_drill.sh — 064 US1 restart drill (T005).
#
# Three scenarios from specs/064-durable-listener-service-box/spec.md US1:
#   1. register + (re)launch + peer-connect with ZERO operator keystrokes,
#      twice in a row (each launch IS a restart) — SC-001: accepting within
#      10 s of launch (the peer dials at T+6 s and must succeed).
#   2. registration whose program file is missing → named diagnostic, REPL
#      still usable (prompt reached, :quit honored).
#   3. no registration file → normalized startup transcript byte-identical to
#      the pre-feature capture test/service_box/baseline_startup.txt (SC-005).
#
# Needs: built C# REPL + DOTNET_ROOT + glpquick-cert/ (QUIC trust material).
# The registration file glpservice/resume.json is OWNED by this drill for its
# duration; any pre-existing one is preserved and restored.
# =============================================================================
set -u
# Drills own a fresh file WAL; a shared pglite journal must never be touched by tests.
unset COLAB_PG_CONN
SB_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CSREPL="$SB_ROOT/out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe"
REG="$SB_ROOT/glpservice/resume.json"
WAL="$SB_ROOT/glpservice/wal"
BASELINE="$SB_ROOT/test/service_box/baseline_startup.txt"
PORT=9207
PASS=0; FAIL=0
check() { if [ "$2" -eq 0 ]; then echo "  PASS: $1"; PASS=$((PASS+1)); else echo "  FAIL: $1"; FAIL=$((FAIL+1)); fi }
# Drop the per-build banner lines AND case-fold the self.glp path line: NTFS reports the
# canonical drive/dir case, which differs with the invoking shell's cwd spelling.
normalize() {
    grep -v "^Build:" | grep -v "^Compiled:" | grep -v "^Working directory:" \
        | sed 's/^\(Loaded root self.glp from: \).*$/\1<path>/'
}

[ -f "$CSREPL" ] || { echo "ERROR: C# REPL not built at $CSREPL"; exit 1; }

# Preserve any operator registration. The WAL is removed on exit: the host appends
# every drill message durably (OnDelivered installs whenever the WAL opens), and a
# later real service with replay:true must never replay drill garbage as history.
SAVED=""
if [ -f "$REG" ]; then SAVED="$REG.drill-saved"; mv "$REG" "$SAVED"; fi
restore() { rm -f "$REG"; rm -rf "$WAL"; if [ -n "$SAVED" ] && [ -f "$SAVED" ]; then mv "$SAVED" "$REG"; fi }
trap restore EXIT

echo "=== 064 US1 resume drill ==="

# --- Scenario 3: no registration → transcript identical (SC-005) -------------
printf ':quit\n' | timeout 60 "$CSREPL" 2>&1 | normalize > /tmp/resume_drill_s3.txt
normalize < "$BASELINE" > /tmp/resume_drill_baseline.txt
diff -q /tmp/resume_drill_baseline.txt /tmp/resume_drill_s3.txt >/dev/null 2>&1
check "S3: no-registration startup transcript identical to baseline" $?

# --- Scenario 2: missing program → named diagnostic, REPL usable -------------
cat > "$REG" <<EOF
{ "version": 1, "program": "glpservice/no-such-program.glp", "goal": "x(Y).", "enabled": true, "replay": false }
EOF
OUT2=$(printf ':quit\n' | timeout 60 "$CSREPL" 2>&1)
echo "$OUT2" | grep -q "resume: program load failed:.*File not found"
check "S2: missing program yields named diagnostic" $?
echo "$OUT2" | grep -q "Goodbye!"
check "S2: REPL remains usable (reached prompt, honored :quit)" $?

# --- Scenario 1: register listener; two consecutive launches; peer connects --
# The registration is written before each launch and REMOVED once the service has
# armed: the peer runs the same exe, and discovery is by walk-up from the exe dir,
# so a still-present registration would make the peer try to bind the same endpoint
# (a real peer host has its own registration, or none).
for launch in 1 2; do
    cat > "$REG" <<EOF
{ "version": 1, "program": "test/service_box/drill_listener.glp", "goal": "drill_recv(\\"127.0.0.1\\", $PORT, Got).", "enabled": true, "replay": false }
EOF
    ( printf ':quit\n' | timeout 90 "$CSREPL" > /tmp/resume_drill_rx.$launch.out 2>&1 ) &
    RXPID=$!
    sleep 6   # SC-001 window: peer dials at T+6s < 10s
    rm -f "$REG"
    ( cd "$SB_ROOT" && printf 'load ./programs/tests/quic/quic_chat_onebind.glp\nsend("127.0.0.1", %s, drill, box, "restart-%s", R).\n:quit\n' "$PORT" "$launch" \
        | timeout 60 "$CSREPL" ) > /tmp/resume_drill_tx.$launch.out 2>&1
    wait "$RXPID"
    grep -q "resume: arming" /tmp/resume_drill_rx.$launch.out
    check "S1 launch $launch: service auto-armed (zero keystrokes)" $?
    grep -q "restart-$launch" /tmp/resume_drill_rx.$launch.out
    check "S1 launch $launch: peer message received (SC-001 window)" $?
done

echo "======================================"
echo "resume drill: PASS=$PASS FAIL=$FAIL"
echo "======================================"
exit "$FAIL"
