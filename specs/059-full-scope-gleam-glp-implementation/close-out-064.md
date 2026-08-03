<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# 059 acceptance-sweep close-out — recorded under feature 064 (T031)

**Date**: 2026-08-03 · **Branch**: `064-post-wave-gap-closure` · **Recorded by**: 064 US4 T031 per FR-008 ("the remaining 059 acceptance tasks MUST be discharged with recorded evidence or explicit recorded deferrals").

**Governing rulings applied**: 064 clarify Q2 (2026-08-03) — Gleam peers join QUIC-WS meshes via the C# bridge; the native BEAM QUIC-WS leaf is a **gated deferral**. 064 clarify Q3 — FE/BE split + embeddability are BUILDS inside 064, not bookkeeping. The 2026-07-27 `rule-quic-sideprocess-relay` Disposition-2 ruling (escalation-register.md) stands: nothing may depend on `gleam_quic/src/glpq_ffi.erl` until its smoke test passes — and nothing now does (the bridge route does not touch it).

**Evidence anchors**: `specs/064-post-wave-gap-closure/baseline.md` (fresh-session suite table: REPL 381/381 A/B/C, C# 360 total across 5 suites at close checkpoints, Gleam 618, parity corpus 206/206 at 100% in-scope agreement, cross-runtime Section I 18/18 fleet record @ v2026.08.03.1 / 12/18 on this host under the recorded OTP-29 deviation) · CHANGELOG v2026.08.03.1 (feature 060 wave-3 chain) · v2026.08.02.1 (062) · v2026.07.31.2 (063) · `docs/research/fullscope-gleam/phase2-verify/` verdicts + rulings.md · `docs/research/fullscope-gleam/frozen-interface-register.md` (unmodified since commit 0009a7f7).

**Counts**: 39 open tasks swept → **9 DISCHARGED-BY-064 · 7 DISCHARGED-EARLIER · 23 DEFER** (every deferral explicit below; zero silent exits).

---

## 1. Sweep table (task → disposition → evidence)

| Task | WP | Disposition | Evidence / reason |
|---|---|---|---|
| T060 | close-acceptance-acceptance-sweep-and-polish | **DEFER — flag for lead** | The named artifacts (`run_link_tests_cross_gleam.sh` 16/16, `specs/050-full-gleam-combined/acceptance.md`) were never built. Substance superseded: the 060 Section I cross-runtime suite (18 scenarios, both directions, wired into `test/run_all_tests.sh`) exceeds the 16/16 TCP capstone, and this document is the SC sweep. Lead should ratify the supersession or order the named rig. |
| T061 | close-body-kernel-now-send | **DEFER (partial)** | `_send/3` delivered at the mad layer (060; `glp_gleam/src/glp/mad/mad_kernels.gleam:55,71` per the Dart body_kernels contract). `_now` still unregistered (`kernels.gleam:24`; `mad_kernels.gleam:51` "later"). |
| T062 | close-bytecode-bytecode-instruction-set | **DISCHARGED-BY-064** | Lint closed by 064 T034 (v2.16 operand-arity + HEAD/GUARD/BODY placement checks, `lint_test`, commit 75c6b48e); instruction-set + mode-conversion verify-DELIVERED (opcodes_test mnemonic totality; WxW via `writer_mgu_adversarial_test`/`heap_test`/`unify_test`); lint disposition ratified 060 T013. |
| T063 | close-bytecode-runner-missing-opcodes | **DEFER (partial)** | UnifyConstant implemented per the G4-normative Dart reference (`runner.gleam:1343`, Dart runner.dart:1660 anchor); Requeue/Allocate/Deallocate still fall to the `Unimplemented` catch-all (`runner.gleam:452`) and no committed golden opcode pin found. Recorded deferred-as-unused by the reference compiler path. |
| T064 | close-codec-compiled-il-on-the-wire | **DISCHARGED-EARLIER** | Verify b3-c1-011: 3 DELIVERED (TLV term codec byte-parity, result-envelope codec, builder) + 3 ABSENT-by-design; "no close WP is activated". G5 ruled `compiled-il-on-the-wire` out-of-scope post-feature follow-on — which 064 US3 then delivered on the C# side (LOAD_IL/RUN_GOAL_IL, compiler-free execute path, corpus equivalence 12/12). |
| T065 | close-compiler-antlr-shared-grammar-spike | **DEFER (evidence residual) — flag for lead** | Substance delivered by 060: project static linker (Section F oracle plays 1–7 green), dynamic dispatch B1–B3 (Section L oracle; `dispatch_test` runs the verbatim `dynamic_dispatch/` sources), `_copy/2` kernel (`kernels.gleam:201`) unblocking the metainterpreter; strict gate + compile-mode verify-DELIVERED; ANTLR spike G5-superseded. Residual: no recorded fresh run of `tracing_meta.glp` (and the stale `modules/` corpus is parity-rejected on both sides by design). |
| T066 | close-distribution-engine-sessions | **DEFER** | Distributed unification undelivered: no `dist_unify.gleam`, no `RemoteVarRef` runtime; 064 US1 T005–T007 open; `specs/064-post-wave-gap-closure/contracts/dist-unify.md` written but unimplemented. T057 adversarial dist-deref suite still unbuilt. |
| T067 | close-embed-embeddability-service-box | **DEFER** | No ratified service-box requirements contract; the store-kernel scope call (store_put/store_get kernels vs host-owned log) remains an OPEN escalated engineer decision (rulings.md "Still-open"). |
| T068 | close-embeddability-host-api | **DISCHARGED-BY-064** | T030 `glp_embed.gleam` load/run/observe surface (G3-A) + `glp_embed_host_test.gleam` drives the engine with no repl imports (commit 791a3c9e; gleam 597→618). |
| T069 | close-engine-engine-composition-root | **DEFER (partial)** | Transport injection delivered (060 T007/T008 `with_transports`/`transport_for` + 2 tests; `engine.gleam:408,422`); output-capture + envelope verify-DELIVERED. Residual: the host **kernel**-injection seam (kernel registered from the host, never referenced by the engine) is absent. |
| T070 | close-febe-embedded-switch-role-framing | **DISCHARGED-BY-064** | The requirements-level handoff is superseded by the clarify-Q3 engineer ruling to BUILD, plus `contracts/febe-split.md`. Per-detail_id dispositions in §3 below; the un-built detail_ids (snapshot, restore-resume, Gleam-side multi-client control program) carry to the T091/T093 deferrals. |
| T071 | close-guard-kernel-wait-guards | **DEFER (test residual) — flag for lead** | `wait`/`wait_until` implemented outcome-equivalently (`runner.gleam:2668–2674`: bound number succeeds, unbound suspends upstream, non-number fails — Dart-outcome-equivalent in the clock-free pure engine). Residual: the acceptance's dedicated suspend-then-reactivate + failure gleeunit cases in `guards_test.gleam` do not exist (small test-add). |
| T072 | close-guards-guard-defined | **DISCHARGED-EARLIER** | Verify b3-c1-002: both detail_ids DELIVERED (guard_defs side table + three-valued eval; compile-time purity enforcement) with full three-runtime parity — close never activated. |
| T073 | close-langsurface-channel-convention | **DISCHARGED-BY-064** | Verify b3-c1-003: all 5 detail_ids DELIVERED. The one owed fix (F1 param_arity panic, ruled 2026-07-23 to a shared type-checker-robustness close) landed as 064 T035 (typed StagedError + regression test, commit 75c6b48e). |
| T074 | close-link-inbound-pump | **DISCHARGED-EARLIER** | 060 US4 L1–L4: pump (parse-reassemble-order rules 2/4/5), establish (verify-before-act, either role), registry, egress (window+sequence+frames), capability gate, fault lattice; 12 tests over loopback+TCP; K1/K6/K7 kernels wired into the engine; Section I cross-runtime scenarios 18/18 fleet record. Non-ground crossing = T066 deferral. |
| T075 | close-link-layer-fault-decoration | **DISCHARGED-EARLIER** | 060 fault lattice + bounded-silence ≤30s per the amended contract; `primitives_test` observes `PumpFault`/`link_faults` arriving as data. |
| T076 | close-link-layer-glp-primitives | **DISCHARGED-EARLIER** | 060 link primitives per the owner-amended contract + engine-wired link kernels; ground-term round-trips over loopback+TCP (`primitives_test`) + Section I both-direction scenarios. The 025-contract non-ground tail is the T066 deferral. |
| T077 | close-link-layer-sequence-dedup | **DISCHARGED-EARLIER** | 060 US4 L1 reliability state machines ported per amended contract (`link_sequencer`, `inbound_ordering`, `frame_reassembler`, `send_window`) with the egress/pump integration carrying the sequence/dedup/reassembly rules. |
| T078 | close-module-system-runtime-rpc | **DISCHARGED-EARLIER** | 060 dispatch B1–B3: Distribute/Transmit executed (data-threaded channel sends + RemoteSpawn), scheduler module registry, embedded `serve/2`; Section L oracle L1–L3 green; `dispatch_test` end-to-end module-qualified calls. |
| T079 | close-multiagent-multiagent-boot-loader | **DEFER (partial) — flag for lead** | The madGLP layer IS delivered: 060 T039 boot loader + `mad/` suites (globalize/localize byte-identical to Dart, global writers table, MadEngine, `mad_predicates.glp` load, cold-call flow test) + 064 T033 `:boot` live-verified two-agent play. Residual (SC-007): the named reference plays `play_alice_bob.glp`/`play_cold_call_test.glp` do not run — the single-source Gleam boot loader does not model the per-isolate PROJECT loading the full Dart plays use (`boot_command_test.gleam:11–13`). |
| T080 | close-parity-differential-harness | **DISCHARGED-BY-064** | One-command rig committed (`test/parity/run_gleam_corpus.sh` + README); fresh-session 206/206 at 100% in-scope agreement (064 baseline.md); the T051 HALT/ESCALATE drift finding root-caused and fixed in 060 (CRLF-corrupted goldens; CR-tolerant parse + LF pin). |
| T081 | close-platform-atomvm-compatibility-by-construction | **DISCHARGED-BY-064** | Verify DELIVERED-by-construction (no-OTP deps policy); fresh WSL build+test transcript at 064 T001 (gleam 569 incl `deps_policy_test`, 618 at close); AtomVM gated probe retained (T021 record). |
| T082 | close-process-baseline-program-dossier | **DEFER (bookkeeping residual)** | Substance done: roadmap waves 2/4/5 advanced→closed with receipts (063 T030; commits 1996ff5b, 14c28169 per engineer directive); `engine-instances-scaling-research` G5-ruled out-of-scope. Residual: the named per-detail_id reconciliation table was never committed. |
| T083 | close-proofs-proof-dist-deref-convergence | **DEFER** | PI:14 remains discharged (sorry-free Lean + suite). PI:17 undischarged as expected — scaffold only; depends on the deferred dist-unify chain (050 T057/T058 unchecked; T066 deferral). |
| T084 | close-quic-client-inprocess-tests | **DEFER (gated)** | Native/in-process QUIC line. Profile-C quicer NIF is environment-blocked (verify: WSL build-hook failure, classified environment not code-absence). Under the 064 Q2 bridge ruling the native line is a recorded gated deferral. |
| T085 | close-quic-transport-leaf | **DEFER (gated)** | The native BEAM QUIC transport leaf is exactly the 064 Q2 recorded gated deferral (`specs/064-post-wave-gap-closure/DEFERRALS.md` per T038). Blocked behind T098 by the Disposition-2 ruling in any case. |
| T086 | close-quicws-link-completion-live-repl-bridge | **DEFER (gated; capability met by bridge)** | The WP wording demands the **Gleam** QUIC-WS transport completed (RFC 6455 framing Gleam-side, Profile-C runtime) — the native leaf → deferred per Q2. The user-facing capability (Gleam peers join QUIC-WS meshes) IS delivered by the 064 T012 bridge: `glp_quick_host` BridgeAcceptor + `bridge_client.gleam` dial helper (commit c5644c28; glp_link 171, gleam 588). |
| T088 | close-runtime-arithmetic-expression | **DEFER (partial)** | Closed: `_copy/2` (060, `kernels.gleam:201`), heap-copy/stream/arithmetic rows verify-DELIVERED. Open verify residuals: `suspension-abandonment` ABSENT; `_now`(/`_send` standalone) registration; the `[WARN] Unknown guard predicate` line; the `:=` RHS binary-minus parser defect. |
| T089 | close-transports-multi-accept-transport-extension | **DEFER (partial)** | Multi-accept closed by 064 T010/T011 (`multi_accept.gleam` + suite, N concurrent inbound, exit_on_close/D-9 norms, commit f88ff5e1); frame-hardening verify-DELIVERED; ZMQ leaf present per the ZMQ mandatory ruling. Open: quiescence oracle (064 T008–T009 unbuilt) and the all-gating identical-outcomes matrix incl. the QUIC-WS leg. |
| T090 | close-wireproto-crdt-convergence | **DEFER (sign-off gate)** | Verify §3 recommends all five items host-side (message-envelope carrying an explicit interop obligation) but the close "must not start until that table has engineer sign-off" — no sign-off recorded. 063's ms_message QUIC-leg drill (100/100 exactly-once) evidences the C#-hosted side only. |
| T091 | build-fe-be-process-split | **DEFER (partial) — flag for lead** | BUILT and green at 064's own contract: `glp/be/server.gleam` + `glp/fe/client.gleam` over the ported split-protocol wire codec, two-OS-process smoke, regression-corpus equality, cross-runtime FE/BE smoke both directions (T026–T029, gleam 618). FINAL-plan residuals: FE kill-restart e2e, engine-state snapshot/restore (BE refuses `:snapshot` loudly — `febe_split_test.gleam:141` "no store on this BE"), and the two-client GLP control program on the Gleam side. |
| T092 | build-yngenios-embeddability | **DEFER (partial)** | G3-A embeddability surface delivered (064 T030 glp_embed + host test = the compiling boundary proof). Absent: ratified service-box contract, the four-service spec-056 fabric wiring with yngenios suites green, object-PUT across the spine, engineer sign-off; store-kernel scope call open. SC-008 deferred. |
| T093 | accept-febe-embeddability | **DEFER** | Blocked on the T091 residuals (kill-restart, snapshot/restore) + T092 (contract + sign-off). The delivered portions are accepted implicitly via the 064 checkpoints (T026–T030 suites green). |
| T095 | SC-001..SC-009 evidence rows + zero open escalations | **DEFER (partial)** | Per-SC rows recorded in §2 below; but SC-006/007/008 are deferred and the store-kernel scope call is still an open escalated decision, so "zero open" cannot be claimed. |
| T096 | pinned suites green + grow-only, register unmodified | **DISCHARGED-BY-064** | baseline.md: gleam 463-freeze→618, REPL 381/381, C# link 147→171 (+4 more suites), corpus 206/206; `frozen-interface-register.md` untouched since its creating commit 0009a7f7. |
| T097 | marathon discharge gate + ship via GitFlow | **DEFER (ships with 064)** | The 064 ship (T041) is the GitFlow vehicle; this document is the WP-level disposition record. Marathon `mrun-8bda036d9e9b` discharge to be confirmed at 064 /bk-close. |
| T098 | close-quic-sideprocess-relay-smoketest | **DEFER (gated)** | The ruling-mandated smoke test must exercise `glpq_ffi.erl` (native Profile-A relay) — the 064 Q2 bridge route does not admit substitution. Disposition 2 stands satisfied in its protective intent: **no Wave-4 deliverable depends on `glpq_ffi.erl`** (the bridge uses the C# QUIC-WS endpoint directly). Deferral rides with the native-leaf deferral (T085). |

## 2. SC sweep (059 spec.md success criteria)

| SC | Verdict | Evidence |
|---|---|---|
| SC-001 delivered capabilities stay green, suites never shrink | **MET** | Grow-only across 060→064: gleam 463→508→618, REPL 381, C# link 147→171; every 064 checkpoint zero-regression (baseline.md rule); frozen-interface register unmodified (commit 0009a7f7 sole touch). |
| SC-002 97 unconfirmed-gap capabilities have committed verify verdicts | **MET** | 21 verify WPs (T039–T059) all committed under `docs/research/fullscope-gleam/phase2-verify/` with runnable evidence; wave-2 phase fully checked 2026-07-27. |
| SC-003 154-detail_id coverage union reaches terminal disposition, zero silent exits | **PARTIALLY MET** | Majority closed-to-parity/delivered-confirmed/ruled (G5 + ZMQ + QUIC rulings recorded); the remainder are the **explicit** deferrals in §1/§4 — no silent exits, but not yet terminal. |
| SC-004 corpus parity identical to the Dart oracle, fresh-session re-verifiable | **MET** | 206/206, 100% in-scope agreement, fresh session (064 baseline.md); one-command rig `test/parity/run_gleam_corpus.sh`; three-way differential AGREE (060). |
| SC-005 FE/BE e2e (kill-restart, snapshot/restore, two clients) | **PARTIALLY MET** | Two-OS-process split green with regression-corpus equality + cross-runtime smoke (064 T026–T029). Kill-restart, snapshot/restore, and the Gleam two-client control program legs deferred (T091). |
| SC-006 Gleam mesh acceptance (quic_mesh equivalent, C# peer) | **DEFERRED (bridge substrate delivered)** | 064 T012 bridge lets Gleam peers join QUIC-WS meshes via the C# endpoint (Q2 ruling); the quic_mesh.glp-equivalent acceptance run with the Gleam instance as mesh controller has not been executed; native leaf gated-deferred. |
| SC-007 reference multiagent plays pass on the Gleam instance | **PARTIALLY MET** | madGLP layer + `:boot` two-agent play green (060 T039 + 064 T033); the named reference plays deferred — boot loader lacks per-isolate project loading (T079). |
| SC-008 four spec-056 services on the embedded Gleam engine + object-PUT + sign-off | **DEFERRED** | glp_embed boundary surface only (T092); no fabric wiring, no sign-off; store-kernel scope call open. |
| SC-009 zero unresolved escalation-register entries | **PARTIALLY MET** | The register's sole entry (`quic-sideprocess-relay`) is RESOLVED by ruling; its enforcing smoke test is deferred with nothing depending on the relay (protective intent intact). Still open elsewhere: the store-kernel scope call (rulings.md "Still-open"). |

## 3. T070 detail_id dispositions (the eight FE/BE rows)

| detail_id | disposition |
|---|---|
| `repl-engine-split-binary-wire-mvp` | built-in-064 (Gleam split over the ported split-protocol wire codec; C# reference MVP delivered 061) |
| `engine-review-dossier` | design-confirmed (026 dossier, verify-DELIVERED) |
| `premise-reconciliation-compiler-location` | decision-taken (026 US2: source text on the wire for MVP; C#-side IL factor-out delivered 064 US3) |
| `embedded-switch-role-framing` | built-in-064 (BE/FE role split; embedded switch via glp_embed) |
| `multi-client-control-program` | built-in-064 on the C# side (US2 multi-client serve, A31 merge; GLP-merge recorded partial, §1.14-gated); Gleam side deferred (T091) |
| `engine-state-snapshot-persistence` | **deferred** (BE refuses `:snapshot` loudly; no store) |
| `liveness-crash-restart-host` | **deferred** (no kill-restart e2e) |
| `restore-and-resume-link-reestablish` | **deferred** (depends on snapshot + dist chain) |

## 4. Explicit deferral list (the 23 unchecked tasks, grouped)

1. **Distributed-semantics chain**: T066 (dist-unify sessions), T083 (PI:17 proof), plus the 064 US1 open tasks it mirrors (dist-unify, quiescence) → T088/T089 quiescence rows. Largest true residual.
2. **Native QUIC line (Q2 gated deferral)**: T084, T085, T086, T098 — native BEAM QUIC-WS leaf + glpq_ffi.erl smoke; capability meanwhile served by the 064 bridge (T012). Recorded also in `specs/064-post-wave-gap-closure/DEFERRALS.md` (064 T038).
3. **FE/BE completion**: T091 residuals (kill-restart, snapshot/restore, Gleam two-client control program), T093 accept.
4. **Yngenios wiring**: T092 (four-service fabric + contract + sign-off), T067 (service-box contract; store-kernel scope call open — engineer decision), SC-008.
5. **Multiagent plays**: T079 (per-isolate project loading for the named reference plays; SC-007).
6. **Small engine residuals**: T061 (`_now`), T063 (Requeue/Allocate/Deallocate + UnifyConstant golden pin), T069 (host kernel-injection seam), T071 (wait-guard test cases), T088 (abandonment op, [WARN] line, `:=` RHS parser defect).
7. **Evidence/bookkeeping residuals**: T060 (capstone-rig supersession to ratify), T065 (tracing_meta fresh-run record), T082 (reconciliation table), T090 (scope-table sign-off), T095 (SC terminality), T097 (ships with 064 T041).

## 5. Flagged for the lead (ambiguous wording — not guessed)

- **T060**: named artifacts never built; Section I (18/18) + this document exceed/replace them in substance. Ratify supersession or order the named rig.
- **T091/T093**: 064 Q3 ruled "BUILD the FE/BE split" and 064's own contract was met, but the 059 FINAL-plan acceptance is richer (kill-restart, snapshot/restore, two-client). Left unchecked; confirm whether the 064 build contract supersedes the FINAL-plan e2e or the residual legs stay on the roadmap.
- **T079 / SC-007**: "reference multiagent plays" — the delivered single-source boot loader cannot load the per-isolate projects the full plays need; needs a scope ruling (equivalence statement vs. project-loading port).
- **T098 vs Q2**: the Disposition-2 smoke names `glpq_ffi.erl` explicitly; the bridge route bypasses rather than satisfies it. Recorded as gated deferral with the protective condition (no dependency on the relay) intact.
- **T065/T071**: pure test/evidence adds (tracing_meta run; wait-guard cases) — cheap closes if wanted before 064 ship.
