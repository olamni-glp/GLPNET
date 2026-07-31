# Contract — US1: QUIC+WS link completion (live REPL bridge, mesh fix, re-verify)

Authoritative sources: the 2026-07-02 036 fidelity-audit findings (carried in
the roadmap profile of `http3-quic-ws-link-completion`) + the spec-025
link-message interface. This contract binds acceptance, not implementation.

## C1 — Live REPL bridge (`--repl`)

- `glp_quick --server --repl <path-to-glp_repl>` and
  `glp_quick --client --repl <path-to-glp_repl>` MUST each start the named
  REPL as a child process and bridge its stdio over the established link via
  the existing link-message envelopes (one line in ⇒ one message; one result
  block ⇒ one message). No new wire format.
- An operator at either end MUST be able to run a goal that the OTHER end's
  REPL evaluates, observing its result locally (the 036 "run GLP over the
  link" promise, now real).
- REPL child death MUST surface as an explicit link-visible fault, never a
  silent stall (bounded-silence rules apply).

## C2 — Mesh dup-id regression (the recorded defect)

- Scenario `mesh_dup_id`: three instances A, B, C; C announces the same id as
  a LIVE B. REQUIRED behaviour: the incumbent B's routing entry survives (no
  eviction/hijack by the newcomer); traffic to that id keeps flowing to B;
  the rejection/refresh is visible in the mesh's own output.
- The scenario MUST fail against the audited defect behaviour and pass
  against the completed implementation (a true regression witness). If the
  current in-tree guard already passes, the scenario STILL ships — it is the
  proof the audit finding stays closed.

## C3 — In-tree build + full re-verify

- The C# host library the 9 skipped integration tests load MUST build from
  the current tree via the standard `dotnet build` path; the integration
  suite MUST run with 0 skips attributable to the missing dll.
- The prototype's full demo suite (single-host + mesh scenarios) MUST re-run
  with one explicit verdict per scenario; the summary MUST reproduce or
  supersede the audited 18/104 claim with a current, reproducible count.

## C4 — Documentation correction

- The stack-profile description MUST read: the relay profile relays
  (no QUIC termination); the reference stack terminates QUIC. One place,
  referenced elsewhere (single source of truth).
