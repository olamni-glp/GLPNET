# Contract — Routing, Policy, Experimental GLP Guard

Traces: FR-012/013/014/015/016; SC-007/009.

## C20. Transport of record (FR-016)
- **Invariant**: delivery over the shipped `glp_quick_host` (MsQuic QUIC + RFC 6455 WS over one bidi stream, SPKI pin) behind a link-transport seam. AtomVM/WASM delegate QUIC to a native side-process (Profile A); never assume in-runtime QUIC.

## C21. @name loud-fail addressing (FR-012)
- **Invariant**: a directed @name resolves against the authenticated peer set and delivers to that peer ONLY; an unknown name → **reported error, never silent default-fallback** (SC-007). This is a language-level invariant for every addressing form (born from the 037 silent-fallback defect).

## C22. Dedup (FR-015)
- **Invariant**: `msg_id` (end-to-end) + per-link `seq` (FIFO) suppress duplicates; idempotent apply at the store boundary.

## C23. Fixed routing policy — MVP-CORE (FR-013)
- **Invariant**: `{targets (must-reach), waypoints (ordered), excludes}` evaluated per hop by a pure matcher; an **unsatisfiable policy fails loud** (consistent with C21). DROP taxonomy is logged-never-silent (no-route / malformed / send-failed-per-dest / over-capacity).

## C24. Experimental GLP policy guard — PROPOSE-FIRST, NOT shipped (FR-014)
- **Status**: **design + proposal ONLY** in this feature. The guard is an *alternative policy evaluator* expressed as an experimental GLP guard surface.
- **GATE (Constitution IV-a / DISCIPLINE §1.14)**: the concrete guard signature/semantics MUST be owner-approved before ANY implementation task runs. Approval-in-principle (E6) is **not** approval of the concrete language change.
- **Deliverable here**: `programs/crdtmsg/policy-guard-proposal.glp` (proposed typed signature + intended three-valued semantics + worked example) + a design note. No guard code is compiled/executed until §1.14 approval is recorded.
- **Fallback**: C23's fixed-field matcher is the shipped MVP behavior regardless of guard approval.
