# Curator report — Full-scope Gleam GLP delivered-vs-gaps inventory (Phase 1)

Run `20260719T130005Z-782b` (research, review-only) · frozen method `method-20260719T130005Z-782b` · marathon `mrun-8bda036d9e9b` · anchor feature `full-scope-gleam-glp-implementation`. Durable deliverable: `docs/research/fullscope-gleam/gap-inventory-2026-07-19.md` (deterministically generated from the recorded claims — no summarization drift).

## Who claimed, who confirmed

- **builder-1** (roadmap snapshot + specs/ + docs/handover/, blind): 76 design-promise claims (b1-c1-001..076), coverage manifest honest about 380 non-Gleam spec files swept only via roadmap rows.
- **builder-2** (glp_gleam + gleam_quic code, blind): 57 claims (b2-c1-001..057) incl. explicit absent-after-sweep testimony for multiagent-runtime, mesh-ring, fe-be-process-split (as code), link-reliability-sequencing; 100% corpus sweep (49 test modules, all src areas).
- **builder-3** (Dart/C# runtimes + normative docs + programs/, blind): 73 reference-capability claims (b3-c1-001..073); breadth-only on the 1183-file programs corpus, C# engine source noted outside slice.
- **Critic** (codex, cross-provider, non-blind): 201 CONFIRM / 5 ESCALATE / 0 REFUTE over the 206 merge rows; all 40 near-miss key pairs judged distinct capabilities.
- **Merge**: deterministic set-ops by detail_id (206 claims → 154 capabilities), verdicts on three axes (delivery / promise / parity) per frozen E8.

## Headline synthesis (every number traceable to the inventory sections)

- **44 capabilities DELIVERED in Gleam** (17 corroborated across corpora + 27 code-singletons kept visible): the full compiler pipeline (parse→SRSW→PE→typecheck→codegen-v2.16), type checker with byte-identical Dart error parity, three-phase execution + writer-MGU (PI:14 discharged), suspension scheduler with wake-dedup, term/result-envelope codecs at byte parity, FrameCodec+CRC32, loopback+TCP transports, scripted REPL, engine-as-value facade, 206/206 M1 corpus parity.
- **9 PARTIAL** with named missing parts — the load-bearing ones: bytecode-runner (Requeue/Allocate/Deallocate/Distribute/Transmit unimplemented faults), guard-kernel (wait/wait_until timer guards missing), body-kernel (_now/_send unregistered), module-system (parse+codegen yes, runtime RPC no), quic-transport (client-only Profile C, no transport leaf, zero in-corpus tests), link-layer (seam delivered; GLP-facing primitives absent), embeddability-api (in-process engine value yes; host-embedding/yngenios surface no), distribution-protocol (wire layers yes; engine-to-engine sessions no).
- **2 RESOLVED/code-only GAPS** + **97 UNCONFIRMED-GAPS** — the unconfirmed set is dominated by (a) b3 reference capabilities with no Gleam counterpart claim (multiagent isolate model, module static-linking/dynamic-dispatch, system-predicate registry, external I/O bridge, fairness budget, C# QUIC/WS/mesh/crdtmsg/schema stack, program corpus execution) and (b) b1 designed-unstarted promises (FE/BE process-split MVP chain, snapshot/persistence, liveness/restart, restore-and-resume, multi-accept, compiled-IL-on-the-wire). Cycle-2 verification of these against code was the planned residual.
- **5 OPEN ESCALATES (2 clusters, ENGINEER to resolve)**: `multiagent-runtime` and `mesh-ring` — b3 says reference-delivered, b2 says absent-after-sweep, b1 says deferred-to-link-features. Mechanically a conflict; the ruling needed is whether they are in-scope gaps of the full-scope feature (recommended framing for Phase 2) or deferred scope.
- **yngenios embeddability**: gap-by-definition confirmed — b1-c1-051 found only a promoted-but-unspecified roadmap row (durable-listener-service-box) and b1-c1-052 a design-level QHSM/YngeniOS dossier (036 P7); b2-c1-050 found no host-embedding API in code.

## Verdict and residuals

`budget_stop` after cycle 1 (574k/600k; min-cycles 2 not reached — convergence not claimable). Named residuals, none silent: cycle-2 self-verification pass (E7), 40 distinct-judged near-miss pairs preserved for re-check, per-builder unswept areas listed in the inventory's Open-items section. Singletons remain visible per SC-001 — never averaged away.

---
## Run footer

- run: `20260719T130005Z-782b`  verdict: **review_only**  cycles: 1
- critic: codex
- terminal review: skipped — research task type - /bk-codexreview terminal review not applicable (code runs only)
