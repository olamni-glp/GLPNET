/*
 * wire_protocol.pml — FULL client<->engine wire-protocol model for the 061
 * REPL/engine split (T015; FR-040, discharging R14/DEF-A3).
 *
 * Extends spikes/spin/front_back.pml (the 027 HANDSHAKE-1 minimal spike) to the
 * complete protocol of specs/061-wave-2-consolidated-repl-engine-split-spine/
 * contracts/wire-protocol.md:
 *
 *   - all six request kinds  : LOAD_SOURCE RUN_GOAL SNAPSHOT STATUS SHUTDOWN PING
 *     (+ BAD, a malformed/unknown-kind frame — wire rule 3)
 *   - all five response kinds: RESULT ACK DEFERRED PROTOCOL_ERROR ENGINE_BUSY
 *   - wire rule 2: every request gets exactly one terminal response, in order
 *     (single client, single engine, depth-1 channels)
 *   - wire rule 3: malformed/unknown kind -> PROTOCOL_ERROR, engine keeps serving
 *   - wire rule 4: during restore only STATUS/PING are served; ENGINE_BUSY for
 *     the rest; restore completes nondeterministically (FR-030)
 *   - wire rule 5: SNAPSHOT on a busy engine -> DEFERRED, then executes at the
 *     next quiescence (modelled by the `pending` bit + the quiescence branch)
 *   - wire rule 6: SHUTDOWN -> final snapshot (subsumes any pending one), ACK,
 *     engine exits 0
 *
 * Crash/restore-consistency transitions are deliberately NOT here — they are the
 * TLA+ model's property class (models/tla/, FR-040); UPPAAL owns the timed
 * supervision bounds (models/uppaal/). This model checks the untimed wire
 * protocol between one live client and one live engine.
 *
 * NAMED PROPERTIES (checked by run.sh over real SPIN):
 *   deadlock_freedom                       — no invalid end states (run 2)
 *   no_unspecified_receptions              — xs/xr channel ownership (run 2)
 *   request_eventually_answered            — [] (awaiting -> <> !awaiting)  (run 1, fairness)
 *   deferred_snapshot_eventually_completes — [] (pending  -> <> !pending)   (run 1, fairness)
 *
 * Cross-links: ./RESULT.md (verdicts), ./run.sh + ./run.ps1 (reproduction),
 * ./tool-versions.txt (real-tool pins), ../../spikes/spin/ (the seed spike).
 */

/* Request kinds (0x01-0x06 in WireProtocol.cs) + BAD (an unknown kind byte). */
mtype = { LOAD, RUN, SNAP, STAT, SHUT, PING, BAD,
          RESULT, ACK, DEFERRED, PERR, BUSY };

/* Depth-1 channels — one request in flight, one response in flight (wire rule 2:
 * the single client awaits each terminal response before its next request). */
chan req  = [1] of { mtype };
chan resp = [1] of { mtype };

/* Observation bits for the liveness claims. */
bool awaiting = false;   /* client has an unanswered request               */
bool pending  = false;   /* a DEFERRED snapshot is parked (wire rule 5)    */

/* Bound on client requests — keeps the statespace finite; the protocol itself
 * is unbounded-session (the engine loop has no counter). */
#define MAXREQ 5

/*
 * client — the thin REPL client (R7): sends one request at a time, each
 * nondeterministically any of the seven kinds, and awaits exactly one terminal
 * response. Stops after a SHUTDOWN ACK or when its request budget is spent.
 */
active proctype client() {
    xs req;
    xr resp;
    mtype r;
    byte n = 0;
    bool shutdown_acked = false;

    do
    :: n < MAXREQ && !shutdown_acked ->
        if
        :: req ! LOAD
        :: req ! RUN
        :: req ! SNAP
        :: req ! STAT
        :: req ! PING
        :: req ! BAD
        :: req ! SHUT; shutdown_acked = true   /* tentatively; confirmed below */
        fi;
        awaiting = true;
        resp ? r;                 /* exactly one terminal response (wire rule 2) */
        awaiting = false;
        if
        :: shutdown_acked && r == BUSY -> shutdown_acked = false  /* engine was restoring; SHUT refused */
        :: else -> skip
        fi;
        n++
    :: else -> break
    od;
end_client:
    skip
}

/*
 * engine — the engine host. Starts either empty (serving) or --from-snapshot
 * (restoring). In `restoring` only STATUS/PING are answered (wire rule 4);
 * restore completion is a nondeterministic internal step (FR-030). In serving
 * mode every request kind gets its one terminal response; a non-quiescent
 * SNAPSHOT parks as `pending` and completes at the next quiescence step.
 */
active proctype engine() {
    xr req;
    xs resp;
    bool restoring;
    mtype k;

    if                              /* start empty or from a snapshot */
    :: restoring = true
    :: restoring = false
    fi;

    /* The ONE receive sits at this end-labelled do (an idle serving engine
     * blocks here — a valid end state, not a false deadlock), which also keeps
     * the xr assertion valid (no channel polls — SPIN's xr/xs restriction).
     * The if below is exhaustive over (kind × restoring), and the response
     * send never blocks: the single client drains resp before its next send. */
end_engine:
    do
    :: req ? k ->
        if
        /* served in EVERY state (wire rules 4/7; malformed is malformed anywhere) */
        :: k == STAT -> resp ! ACK
        :: k == PING -> resp ! ACK
        :: k == BAD  -> resp ! PERR

        /* restoring: everything else is ENGINE_BUSY (wire rule 4) */
        :: restoring && k == LOAD -> resp ! BUSY
        :: restoring && k == RUN  -> resp ! BUSY
        :: restoring && k == SNAP -> resp ! BUSY
        :: restoring && k == SHUT -> resp ! BUSY

        /* serving */
        :: !restoring && k == LOAD ->
            if
            :: resp ! ACK            /* load ok                                 */
            :: resp ! RESULT         /* compile/type error as structured result */
            fi                       /* (FR-006: either way the engine serves)  */
        :: !restoring && k == RUN -> resp ! RESULT
        :: !restoring && k == SNAP ->
            if
            :: resp ! ACK                       /* quiescent: snapshot written  */
            :: resp ! DEFERRED; pending = true  /* busy: parked (wire rule 5)   */
            fi
        :: !restoring && k == SHUT ->
            pending = false;         /* final snapshot subsumes a parked one    */
            resp ! ACK;              /* wire rule 6: ACK with final seq, exit 0 */
            goto engine_down
        fi
    :: restoring -> restoring = false            /* restore completes (FR-030) */
    :: !restoring && pending -> pending = false  /* quiescence: parked snapshot runs */
    od;
engine_down:
    skip
}

/*
 * LIVENESS (run 1, fairness enabled):
 *   request_eventually_answered — the FR-040 progress obligation: an in-flight
 *   request is always eventually answered by its one terminal response.
 *   deferred_snapshot_eventually_completes — wire rule 5's promise: a DEFERRED
 *   snapshot is eventually executed (at quiescence, or subsumed by SHUTDOWN's
 *   final snapshot).
 */
ltl request_eventually_answered { [] (awaiting -> <> !awaiting) }
ltl deferred_snapshot_eventually_completes { [] (pending -> <> !pending) }
