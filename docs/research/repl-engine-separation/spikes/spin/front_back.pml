/*
 * front_back.pml — MINIMAL front<->back request/response handshake.
 *
 * Feature 027 (refinement-verification-framework), US5 / HANDSHAKE-1 (research.md §3),
 * FR-080 (runnable Promela/SPIN validation spike). Target: SPIN 6.5.1 (tool-versions.txt).
 *
 * SCOPE — MINIMAL ONLY (FR-081 / DEF-A3): exactly ONE request and ONE response, two
 * proctypes, a depth-1 channel. The FULL wire-protocol / result-envelope model is OUT
 * of scope here and deferred to the wire-protocol seeds #5/#6 (DEF-A3, FR-081). This file
 * is the smallest model that can still exhibit a deadlock or a lost-progress
 * counterexample, so it genuinely tests the methodology (research.md §3 rationale).
 *
 * Ready for the T024 chain:  spin -a front_back.pml  ->  gcc -O2 pan.c  ->  ./pan
 * (safety: ./pan ;  liveness: spin -a -DLTL ... or ./pan -a after the ltl claim).
 *
 * Cross-links (relative): ./RESULT.md (acceptance evidence, SC-011),
 * ./run.sh / ./run.ps1 (reproduction), ./tool-versions.txt (real-tool pins),
 * ../../../../specs/027-refinement-verification-framework/contracts/spin-spike.contract.md.
 */

/* The two message kinds on the wire — one request, one response. */
mtype = { REQUEST, RESPONSE };

/* Depth-1 channels: front -> back carries the request; back -> front carries the
 * response. Depth 1 (not 0 rendezvous) lets the model expose a genuine in-flight
 * message without ballooning the state space (research.md §3: keep channel depth 0-1).
 *
 * SAFETY PROPERTY "no_unspecified_receptions": exclusive send/receive ownership.
 *   xs / xr assertions declare that req2back is sent only by `front` and received only
 *   by `back` (and resp2front only by `back` / `front`). SPIN flags any out-of-spec
 *   send/receive (an unspecified reception) as an error. */
chan req2back  = [1] of { mtype };
chan resp2front = [1] of { mtype };

/* Observation bits for the named progress/liveness property (see ltl below). */
bool req_sent  = false;   /* front has sent the request           */
bool resp_seen = false;   /* front has received the response      */

/*
 * front: sends one REQUEST, then awaits exactly one RESPONSE.
 * The `end_front` label is an ACCEPTING end-state: reaching it with empty channels is a
 * valid termination, NOT a deadlock.
 *
 * SAFETY PROPERTY "deadlock_freedom" (no invalid end states): every proctype either
 *   reaches an `end_*` label or is at a statement that can still execute. SPIN's default
 *   invalid-end-state check (./pan) fails on any reachable state where a process is
 *   blocked at a non-`end` statement with nothing runnable.
 */
active proctype front() {
    /* SAFETY PROPERTY "no_unspecified_receptions" — exclusive channel ownership.
     * `front` is the sole sender on req2back and the sole receiver on resp2front.
     * xr/xs must be declared by the owning proctype; SPIN then flags any send/receive
     * on these channels by another process as an error (an unspecified reception). */
    xs req2back;
    xr resp2front;
    req_sent = true;
    req2back ! REQUEST;            /* send the one request                       */
    resp2front ? RESPONSE;         /* await the one response (blocks until back)  */
    resp_seen = true;
    /* progress label: a request was answered — liveness witness (see ltl). */
progress_front:
    skip;
end_front:
    skip
}

/*
 * back: awaits exactly one REQUEST, then sends exactly one RESPONSE.
 * `end_back` is the accepting end-state for the responder.
 */
active proctype back() {
    /* SAFETY PROPERTY "no_unspecified_receptions" — exclusive channel ownership.
     * `back` is the sole receiver on req2back and the sole sender on resp2front. */
    xr req2back;
    xs resp2front;
    req2back ? REQUEST;            /* await the one request                       */
    resp2front ! RESPONSE;         /* send the one response                       */
end_back:
    skip
}

/*
 * NAMED PROGRESS / LIVENESS PROPERTY "request_eventually_answered":
 *   Globally, once the request has been sent it is eventually followed by the response.
 *   This is the FR-080 progress obligation. Verify with:  spin -a front_back.pml  then
 *   compile + run ./pan (the named claim is compiled in; run with -a for acceptance/
 *   liveness checking). A violation yields a counterexample trace per FR-080.
 *
 * The channel-ownership (xs/xr) assertions for "no_unspecified_receptions" are declared
 * inside the owning proctypes above, so they are checked under the same `spin -a` run.
 */
ltl request_eventually_answered { [] (req_sent -> <> resp_seen) }
