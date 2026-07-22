# Full-scope Gleam GLP — Engineer Gate Rulings

**Date**: 2026-07-19 · **Ruled by**: Gabi (engineer/owner) · **Recorded by**: Claude session (marathon `mrun-8bda036d9e9b`) · **Feature**: `full-scope-gleam-glp-implementation` · **Plan run**: `20260719T134320Z-544f` (Phase-2 outline plan, NON-FINAL per frozen-method E9)

These are the five engineer gates named in `feature-outline-plan-2026-07-19.md` (E9 disposition, escalation register, out-of-scope proposals). Rulings were given by Gabi in-session on 2026-07-19; the pipeline-start directive (`/bk-marathon` → specify→clarify→plan→tasks→analyze→implement→codexreview→ship→close) confirms proceed. Any wording correction by the engineer amends this file; nothing here may be silently reinterpreted.

---

## G1 — E9 disposition: RESUME (no waiver)

**Ruling**: Resume cycle 2 of plan run `20260719T134320Z-544f` from persisted state in `.specify/3rtask/runs/20260719T134320Z-544f/` with a fresh budget. The Phase-2 outline plan remains NON-FINAL until the cycle-2 repair completes: re-adjudicate the 10 BLOCKED WPs with full (untruncated) statements, author the 3 dangling-dependency WPs (`freeze-body-kernel-interface`, `freeze-module-system-interface`, `verify-module-system-scope-chain`), and repair genuine dependency defects. No waiver is recorded.

The three run-hygiene proposals (`open-items-cycle2-residual`, `open-items-merge-candidates`, `open-items-unswept-areas`) fold into this resume (see G5).

## G2 — multiagent-runtime escalation: IN-SCOPE (satisfies `rule-multiagent-runtime-escalation`)

**Ruling**: In-scope — port `glp_runtime/lib/multiagent/` to the Gleam instance. Priority per engineer wording: **mandatory, imperative, critical, and urgent**. Wave assignment: the cycle-2 repair must author the multiagent verify/close/build WPs accordingly (the previously-BLOCKED `close-multiagent-multiagent-boot-loader` re-enters with this ruling as input); reference multiagent plays at `programs/multiagent/` are in the full-scope parity acceptance. The `_send`/`_now` messaging-kernel scope in `close-body-kernel-now-send` inherits this ruling.

## G3 — mesh-ring escalation: IN-SCOPE, yngenios-controller framing (satisfies `rule-mesh-ring-escalation`)

**Ruling** (decoded from engineer phone input, twice played back, confirmed by proceed directive; correctable by amendment): Mesh/ring user-story parity is **in-scope** and priority-tiered the same as G2 (**critical, urgent, mandatory, imperative**). The QUIC mesh is the **critical Gleam GLP controller** of the yngenios services fabric:

- Acceptance target: Gleam equivalent of `programs/tests/quic/quic_mesh.glp` passing (mirroring `QuicMeshTests.cs`), close in wave 3, multi-peer acceptance breadth in wave 5.
- C# QUIC endpoints (glp_quick_host-style, per the shipped 050 `glp-native-quic-link` seam) may join as mesh peers.
- The yngenios kernel and mailbox service and all other yngenios services — the frozen spec-056 four-service architecture (mailbox / storage S1 / network S2 / kv S3 / spine) implemented at `D:\bstdev\research\yngenios-003` — run in the yngenios services containers, wired into that mesh.
- The GLP-solidified parts migrate out of the emerging/solidifying yngenios kernel into GLP proper (S4 kernel remains language-authority-gated to glpnet per yngenios design `70`).
- This makes G3 and `build-yngenios-embeddability` one architecture: the mesh-controller role feeds the wave-4 embeddability build.

### G3-A — Engineer directive (2026-07-19, mid-session): delivery frame is the yngenios architecture

**Directive (verbatim intent)**: "It is CRITICAL that the Full-scope Gleam GLP implementation is complete **inside the yngenios architecture**." The yngenios architecture (frozen spec-056 fabric + containers + the G3 mesh-controller role) is the **delivery frame of the whole feature**, not an optional integration target. `/bk-specify` and the cycle-2 plan repair must scope the feature accordingly. NOTE: the engineer's sentence was cut off after "with" — a completion clause is pending and will be amended here when received.

## G4 — UnifyConstant ground-struct-literal divergence: REFERENCE IS NORMATIVE (satisfies `rule-bytecode-runner-unifyconstant-divergence`)

**Ruling**: Parity governs ("we still must maintain parity"). The reference v2.16 ground-struct-literal behavior is normative; the current Gleam emission must conform to it. The golden-parity pin in `close-bytecode-runner-missing-opcodes` pins the **reference** behavior.

## G5 — Out-of-scope proposals: ALL 8 ACCEPTED AS PROPOSED

**Ruling**: Accept-as-proposed for each. These pre-satisfy the wave-2 rule-request WPs that cite this file:

| detail_id | disposition | satisfies WP |
|---|---|---|
| `antlr-shared-grammar-spike` | out-of-scope: superseded (R1 absorption; Glp.g4 = dossier follow-on) | `rule-request-compiler-antlr-shared-grammar-spike` |
| `compiled-il-on-the-wire` | out-of-scope: post-feature follow-on (026 reconciliation; source text on wire for MVP) | `rule-request-codec-compiled-il-on-the-wire` |
| `engine-instances-scaling-research` | out-of-scope: post-feature follow-on | `rule-request-process-engine-instances-scaling-research` |
| `mesh-full-mesh-native-quic` | out-of-scope: duplicate-of promoted `glp-native-quic-link` (note: does NOT dilute G3 — the Gleam mesh-controller scope of G3 stands) | `rule-request-quicws-mesh-full-mesh-native-quic` |
| `zmq-comm-base` | out-of-scope: external-dependency / post-feature follow-on (governing reason: contract-excluded — Gleam transport contract is loopback/TCP/QUIC only) | `rule-request-transports-zmq-comm-base` |
| `open-items-cycle2-residual` | run hygiene → folded into G1 cycle-2 resume | — |
| `open-items-merge-candidates` | run hygiene → 40 DISTINCT verdicts re-checked in cycle 2 | — |
| `open-items-unswept-areas` | run hygiene → folded into cycle-2 sweep | — |

---

## F1 (wave-2 verify finding) — param-arity panic: fix lands under a SHARED type-checker-robustness close

**Date**: 2026-07-23 · **Ruled by**: Gabi · **Satisfies**: the engineer scope-Q raised in `verify-langsurface-channel-convention.md` Finding F1.

**Ruling**: The F1 fix lands under a **shared type-checker-robustness close**, NOT under `close-langsurface-channel-convention`. F1: `program_dfa.gleam:580` raises an uncaught `panic as "UnknownTypeError: …"` (the Dart `states[…]!` ported to `panic`) that crashes the Gleam REPL on `param_arity_mismatch.glp`, where Dart/C# surface a graceful `Error loading …` diagnostic. Rationale: the defect is in shared type-checker infrastructure (program_dfa automaton build) — it fires for any unknown type reaching automaton construction, not the parameterized-type logic that merely surfaced it. The fix threads the error back as a returned/catchable `StagedError(TypeCheckStage, …)` (mirroring the Dart exception-caught-by-loader path). `close-langsurface-channel-convention` (b3-c1-028) is therefore NOT the owner of F1; its typed-corpus Dart-parity acceptance passes once the shared close lands the fix.

---

## Still-open (not gated on /bk-specify, carried forward)

- `rule-request-link-quic-relay` (wave-2 WP): drift-control disposition for the untested Profile-A QUIC relay — freeze-by-file-pin vs minimal smoke test. Decision pending; carried inside the plan, due before any wave-4 WP depends on the relay.
- `close-embed-embeddability-service-box` scope call: store_put/store_get kernels vs host-owned log — escalated to Gabi at that WP, per plan.
