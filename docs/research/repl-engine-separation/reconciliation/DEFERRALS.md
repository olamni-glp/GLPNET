# Deferral Register — `engine-separation` epic

**Purpose:** every decision the epic deliberately *defers* is recorded here with an explicit **revisit-anchor** (the stage/seed at which it MUST be reconsidered) and a **follow-up action**, so no deferral is silently forgotten. Companion: ratified decisions in [`DECISIONS-LOG.md`](DECISIONS-LOG.md); evidence in `DECISIONS-FOR-OWNER.md` + the per-seed memos.

## 🔁 Pickup protocol (how a deferral gets re-surfaced)

1. **Each anchor seed's roadmap note carries a `PRE-SPECIFY` pointer** to this file → `buildkit-roadmap brief <id>` shows it the moment the seed enters `/buildkit-specify`. Action every DEF anchored at that seed before writing its spec.
2. **MVP-gate review** (immediately after #6 `repl-engine-process-split-mvp` ships): re-read **Anchor A** + re-scan this whole register for anything newly unblocked.
3. **Status** column: `open` until actioned; set to `done (→ <feature/PR>)` when resolved. Never delete a row — closure is part of the trail.

---

## Anchor A — Post-MVP review (after #6 ships)
| ID | Deferred | Why deferred | Follow-up action | Source | Status |
|---|---|---|---|---|---|
| DEF-A1 | Dart-mirror **byte-parity** for the result codec (R4) | MVP ships C#-only; don't gate the first split on a second runtime | Specify the result codec to FR-060/061 byte-parity; add the Dart mirror + golden-file test | R4; §2.5; §12r7; memo #5 | open |
| DEF-A2 | **Multi-client / multi-accept** (#10, then #13) | one engine/one client is the MVP; N-clients needs the deferred multi-accept loop | Specify #10 (multi-accept) → #13 (GLP control program) | §4.2/§4.5; memo #10/#13 | open |
| DEF-A3 | **Full Promela/SPIN model of the complete wire protocol / result envelope** (R14: #1a ships only a minimal-handshake spike) | the full envelope/protocol is not designed until #5/#6 | Model the complete front↔back protocol in Promela; check deadlock-freedom + named liveness with SPIN (or an armoury alternative per R15) | R14; #1a spec FR-081; §2.3 | open |

## Anchor B — Before #4 (IL codec spike) `/buildkit-specify`
| ID | Deferred | Why deferred | Follow-up action | Source | Status |
|---|---|---|---|---|---|
| DEF-B1 | Start of **formal Lean proofs** (R11) | proofs are off the MVP critical path | Instantiate the round-trip-identity proof obligation in Lean 4 (Rocq alt for full bisimulation) | R11; memo #4 | open — *partly de-risked: R13 added a minimal real-Lean validation spike in #1a; the FULL proof suite still starts here* |
| DEF-B2 | Confirm the **MLIR Typed-Datalog-IR citation** | `2502.06854` was mis-attributed; candidate = LingoDB (VLDB 2022, Jungmair et al.) | Pin/confirm the citation during the spike | brief §6; memo #4/#16 | open |
| DEF-B3 | Codec-scope forks: "identical IL" def (byte vs exec-equiv), raw vs `CombinedProgram`, obsolete v1 opcodes | not needed until the spike | Resolve U-E1/U-E2/U-E3 at spec | memo #4/#12 (U-E1-3) | open |

## Anchor C — Before #5 (result codec) `/buildkit-specify`
| ID | Deferred | Why deferred | Follow-up action | Source | Status |
|---|---|---|---|---|---|
| DEF-C1 | **Full envelope field set** (R6: MVP is ground-only) | MVP only needs ground results | Extend the envelope to the full §2.3 set (suspended detail, ModuleTerm) | R6; §2.3; memo #5 | open |
| DEF-C2 | **Full unbound-var round-trip** (R2: MVP is display-only) — `VarRef`/`MutualRefTerm`/`ModuleTerm` + `BlockingReaders` | remote resume/inspection not in MVP | Design the net-new unbound + suspended-goal round-trip encoding (also needed by #9, #11) | R2; §10.3; memo #5 | open |
| DEF-C3 | Output layout final (U-W4) + format-version byte (U-W5) | settle with the codec | Decide at #5 spec | memo #5 (U-W4/W5) | open |

## Anchor D — Before #7 (persistence) `/buildkit-specify`
| ID | Deferred | Why deferred | Follow-up action | Source | Status |
|---|---|---|---|---|---|
| DEF-D1 | **#7 scope expansion** — `_waitReaders`, `GlpEngine._goalId`, `InfrastructureGoalIds`, `GlpChannels` | dossier scope line omitted them | Add to the snapshot blob (omission loses timers/collides goal-ids/breaks routing) | memo #7 | open |
| DEF-D2 | Persistence forks U-P1–U-P7 (timer re-arm; quiescence def; address-stability; blob format; resume trigger; egress ordering; kill semantics) | settle with the persistence design | Resolve the U-P set at #7 spec | DECISIONS-FOR-OWNER §4 (U-P1-7) | open |

## Anchor E — Before #9 (restore-and-resume) `/buildkit-specify`
| ID | Deferred | Why deferred | Follow-up action | Source | Status |
|---|---|---|---|---|---|
| DEF-E1 | **`RewireHandle`** (net-new ~30 lines) | `WireEstablishedLink` aborts on pre-bound cells (`LinkEstablish.cs:38-43`) — the post-restore state | Specify a rewire path that adopts restored heap addrs | memo #9 | open |
| DEF-E2 | **Verbatim-address snapshot** constraint on #7 (else external refs break) | cheapest correctness path; stable-logical-id layer is the alternative | Mandate verbatim Cells restore in #7; revisit if a logical-id layer is needed | §12r5; memo #9 | open |

## Anchor F — Before #8 (liveness) `/buildkit-specify`
| ID | Deferred | Why deferred | Follow-up action | Source | Status |
|---|---|---|---|---|---|
| DEF-F1 | **Self-prove liveness GLP goal** | requires a **NEW system predicate = LANGUAGE-AUTHORITY gate** (Gabi approval required before any implementation) | Propose the predicate to Gabi (language authority) BEFORE specifying it; MVP liveness = host timer only | memo #8; CLAUDE.md §1.14 | open |
| DEF-F2 | Unrecoverable-state taxonomy; platform (Windows-only MVP); FR-057 placement | settle with the host design | Resolve at #8 spec | memo #8 | open |

## Anchor G — Before #11 (compiled-IL + factor-out-compiler) `/buildkit-specify`
| ID | Deferred | Why deferred | Follow-up action | Source | Status |
|---|---|---|---|---|---|
| DEF-G1 | **Add #5 to #11 `depends_on`; carry `BytecodeProgram`+`VariableMap` on the request frame** (D4) | ModuleTerm-in-result needs the result codec; VariableMap must cross once the compiler moves | Correct the dep edge + spec the request frame | D4; memo #11 | open |
| DEF-G2 | **ModuleTerm-in-binding round-trip** (excluded from #6 MVP) | full bidirectional IL-on-wire is post-MVP | Round-trip ModuleTerm via #4's IL codec | §2.4; memo #11 | open |
| DEF-G3 | Compiler-relocation forks U-C1–U-C4 (engine contract; `self.glp` under relocation; conjunction wrapping; VariableMap crossing) | settle with the refactor | Resolve U-C set at #11 spec | DECISIONS-FOR-OWNER §4 (U-C1-4) | open |

## Anchor H — Experiments (#12 / #14 / #15 / #16)
| ID | Deferred | Why deferred | Follow-up action | Source | Status |
|---|---|---|---|---|---|
| DEF-H1 | #12 **two-phase split** (grammar-verifier early dep #1a; production-parser dep #4+#11) + add #4 dep + define "identical IL" | grammar-as-verifier value is available early; byte-identity needs #4 | Split/sequence #12 at spec; drop C++ target (defer to #14) | D5; memo #12 | open — *partly de-risked: R13 added a minimal real-MLIR round-trip validation spike in #1a; the PRODUCTION MLIR infra still starts here/#4* |
| DEF-H2 | #14 **C++ scope** = executor-only (dep #4,#12); footprint target; **explicit infeasibility verdict** | narrows the spike; gates #15 viability | Define footprint number + scope at #14 spec | D6; memo #14 | open |
| DEF-H3 | #15 definitions: "one atomic reduction chain"; in-process N-engines vs OS-process-per-instance | drive safe-preempt + shared-static mechanism | Resolve U-E5/U-E6 + give the FOLLOW-UP half an output gate | memo #15 | open |
| DEF-H4 | #16 **LLVM deepen/spike hibernated** on `blocked_on #14`; reassign exploration links to #1a; **close #16 as a research deliverable** (low GEPA/DSPy applicability) | research, not iterate-to-threshold | Close #16 at specify (reports + spike-ownership table + LingoDB fix); hibernate LLVM stage | memo #16 | open |

---

### Cross-cutting deferrals (no single anchor — revisit at the named gate)
| ID | Deferred | Revisit-anchor | Follow-up | Status |
|---|---|---|---|---|
| DEF-X1 | Binding **depth-truncation bound = 32** (R12) | when cycles/large terms appear (≥ #2 post-MVP) | Revisit bound + the Lean proof scope (U-M3) | open |
| DEF-X2 | **Engine→client `ModuleTerm`/IL-on-wire** (whole compiled-IL direction) | #11 + #4 complete | Full bidirectional IL codec | open |
| DEF-X3 | The §10 dossier open-forks not yet owner-decided (output streaming vs terminal §10.2; store source-of-truth-for-code §10.8; in-flight-request replay §10.9) | their owning seed (#5/#7/#9) spec | Owner-decide at the relevant anchor | open |
