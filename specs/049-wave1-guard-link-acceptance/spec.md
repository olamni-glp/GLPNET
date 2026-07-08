# Feature Specification: Wave 1 Consolidated — GLP Policy-Guard + HTTP3/QUIC-WS Link Full Acceptance

**Feature Branch**: `049-wave1-guard-link-acceptance`
**Created**: 2026-07-08
**Status**: Draft
**Input**: User description: "Wave 1 consolidated: GLP policy-guard + HTTP3/QUIC-WS link full acceptance. TWO consolidated deliverables with full traceability to roadmap features glp-policy-guard and http3-quic-ws-link-full-acceptance. (A) GLP policy-guard (§1.14-gated): implement the native GLP guard satisfiable(Policy?, Reachable?) with three-valued Success/Suspend/Fail semantics as an alternative policy evaluator for crdtmsg mesh routing, per the proposal at programs/crdtmsg/policy-guard-proposal.glp (feature 041 T053/FR-014/contract C24 follow-up); the Suspend arm forbids the silent default-fallback; the shipped C# PolicyMatcher (contract C23) remains the production fallback regardless. HARD GATE: no guard implementation/compile/run before Gabi's express DISCIPLINE §1.14 approval of the concrete guard signature+semantics — spec/clarify are the approval vehicle. (B) HTTP3/QUIC-WS link full acceptance (036 deferred items): Erlang/Gleam Profile C via quicer NIF, two-host LAN end-to-end acceptance (this host + gavri), and marathon durability verification — completing the deferred acceptance criteria of feature 036."

## Consolidation Traceability *(wave mandate)*

This feature is **Wave 1 of the 2026-07 consolidated roadmap sweep** (epic `epic-roadmap-sweep-2026-07-consolidated-waves`, roadmap feature `wave-1-consolidated-glp-policy-guard-http3-quic-ws-link-full-acceptance`). It consolidates two original roadmap features, which MUST be advanced to shipped/closed on the roadmap when this wave ships and closes:

| Original roadmap feature | Deliverable here | Origin |
|---|---|---|
| `glp-policy-guard` | Deliverable A (US1) | Feature 041 T053 / FR-014 / contract C24 follow-up |
| `http3-quic-ws-link-full-acceptance` | Deliverable B (US2–US4) | Feature 036 deferred tasks T032, T040, T003+T036 |

## Clarifications

### Session 2026-07-08

- Q: §1.14 ruling on the `satisfiable/2` guard form — (a) defined guard unfolded by the partial evaluator, (b) new system guard primitive, revise, or reject? → A: **APPROVED, staged: form (a) first, then EVOLVE into form (b), verifying (a) ≡ (b) equivalence.** Both forms are expressly approved under §1.14 with the proposed signature `satisfiable(Policy?, Reachable?)` and three-valued semantics; implementation order is (a) → green → (b), with a mandatory equivalence verification showing identical three-valued outcomes across the shared decision-vector set and the four worked examples under both forms.
- Q: Ship gate — which user stories are hard ship-gates vs deferrable-with-evidence? → A: **Hard-gate everything (Option B): the wave does not ship until ALL FOUR user stories pass.** Environment blockers (toolchain, host availability) are recorded with evidence and block the ship; only an express re-ruling by the engineer (Gabi) can re-scope. No unilateral deferrals.
- Q: **T003 realization addendum (2026-07-08)** — the §8 defined-guard mechanism named by the form-(a) ruling is compile-time-only (research R1); which concrete realization carries form (a)? → A: **(a1) APPROVED, then evolve to form (b)** (Gabi, verbatim "1 a1 yes then a3"): form (a) is realized by the (a1) compiler extension — a declared guard procedure with multiple guarded clauses compiling to runtime three-valued goal evaluation (suspension on unbound readers) — preserving the staged shape; the wave then proceeds to the form-(b) system guard primitive (the (a3) endpoint: additive analyzer guard-table entry + three-valued evaluation in the runner) with the mandatory (a) ≡ (b) equivalence verification unchanged. The v05 (empty-targets) and v12 (excluded-target-with-unbound-reachability) vector outcomes remain to be ruled before `vectors.json` is created (T005) — asked back to the language authority, recorded here when answered.
- Q: Profile C native QUIC sourcing on this host (prebuilt vs MSVC build vs run on gavri)? → A: **Delegate US2 + US3 to the gavri host as a sub-feature worktree task feeding back early into this repo.** A self-contained task prompt is produced as a feature artifact for the engineer to post in the gavri session; the gavri session works on its own branch off `049-wave1-guard-link-acceptance`, provisions the BEAM/quicer stack there, executes Profile C in-process and the two-host LAN run (gavri ↔ this host), and pushes evidence artifacts back under this feature's directory for early integration.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Native GLP routing guard behind the language-authority gate (Priority: P1)

A GLP crdtmsg mesh developer wants the per-hop routing admission decision expressed natively in GLP: a guard that tests whether a routing policy is satisfiable given the currently-reachable authenticated peers, with three-valued semantics — satisfiable succeeds, unknown reachability suspends (never a silent fallback), unsatisfiable fails loudly. Because this is a language extension (a new guard surface), the language design authority (Gabi, DISCIPLINE §1.14) must first rule on the concrete signature and semantics proposed in `programs/crdtmsg/policy-guard-proposal.glp`; only a recorded express approval unlocks any implementation, compilation, or execution of guard code. The shipped C# PolicyMatcher (contract C23) remains the production routing evaluator regardless of the ruling.

**Why this priority**: Highest-ranked promoted roadmap item; closes the last open thread from feature 041; the Suspend arm eliminates the silent-default-fallback defect class (037) at the language level.

**Independent Test**: Can be fully tested by loading the approved guard in the REPL and reproducing the four worked-example outcomes from the proposal (Success / Fail / Fail / Suspend), independent of Deliverable B.

**Acceptance Scenarios**:

1. **Given** the proposal at `programs/crdtmsg/policy-guard-proposal.glp`, **When** the §1.14 review is held during clarification, **Then** a ruling (approve form (a) defined-guard, approve form (b) system-guard, revise, or reject) is durably recorded in this spec's Clarifications before any guard code exists.
2. **Given** an approved signature and a policy with a reachable, non-excluded target, **When** the guard is evaluated, **Then** it succeeds and the message is delivered/forwarded.
3. **Given** an approved signature and a ground policy with no reachable target (or a target that is excluded), **When** the guard is evaluated, **Then** it fails loudly and only an explicit otherwise-clause records the drop — never a silent drop.
4. **Given** an approved signature and an unbound reachable-set, **When** the guard is evaluated, **Then** the goal suspends until reachability is known — no default fallback fires.
5. **Given** the form-(a) guard is implemented and its suite is green, **When** the guard is evolved into form (b) (system guard primitive), **Then** both forms produce identical three-valued outcomes on the shared vector set and all four worked examples, verified by suite runs under each form.

---

### User Story 2 - In-process QUIC on the full BEAM (036 Profile C) (Priority: P2)

A distributed-GLP developer running the Erlang/Gleam runtime wants the QUIC+WS channel-link to run **in-process** on the full BEAM via a native QUIC library (quicer NIF), instead of delegating to a side-process host, completing deferred 036 task T032.

**Why this priority**: Profile C was the only 036 runtime profile never proven; it unblocks BEAM-native distributed GLP without a companion process.

**Independent Test**: Run the 036 conformance demo with the BEAM client connecting in-process to the shipped QUIC host and observe the same pass criteria as the Profile A baseline.

**Acceptance Scenarios**:

1. **Given** a provisioned native QUIC library on this host, **When** the BEAM client executes the 036 conformance flow in-process, **Then** connection, TLS pin verification, and full-duplex exchange all pass with results equal to the Profile A baseline.
2. **Given** the native library cannot be provisioned (toolchain unavailable), **When** the provisioning path is exhausted, **Then** the blocked state is recorded with evidence (what was attempted, what is missing), the ship gate stays closed, and the blocker is escalated to the engineer — never a silent skip or unilateral deferral.

---

### User Story 3 - Two-host LAN end-to-end acceptance (036 T040) (Priority: P3)

A distributed-GLP developer wants the QUIC+WS link proven between **two physical hosts** on a real LAN (this host + gavri), not just multiple NICs on one machine — completing deferred 036 task T040.

**Why this priority**: The link's purpose is cross-host distribution; single-host multi-NIC runs left a real-network gap in 036's acceptance.

**Independent Test**: Execute the 036 quickstart with the server on one host and client(s) on the other; verify the full-duplex and mesh criteria from 036 pass across the wire.

**Acceptance Scenarios**:

1. **Given** both hosts share the generated certificate material per the 036 trust model, **When** the quickstart conformance flow runs across the LAN, **Then** connect, pin-verify, full-duplex, and ≥4-client mesh criteria all pass.
2. **Given** the second host is temporarily unavailable, **When** the run is attempted, **Then** the attempt and blocker are recorded, the item is rescheduled, and the ship gate stays closed — the wave neither silently claims acceptance nor ships around the gap.

---

### User Story 4 - Marathon durability verification (036 T003+T036) (Priority: P4)

An engineer using the marathon harness wants proof that a real persisted marathon run survives a process kill and resumes exactly from durable state — completing the 036 deferred durability verification (the originally-cited run was never created).

**Why this priority**: The sweep itself depends on marathon durability; verifying it on a real run closes the 036 deferral and de-risks every subsequent wave.

**Independent Test**: Create/use a persisted marathon run, kill the owning process mid-flight, resume, and verify position derives purely from durable rows.

**Acceptance Scenarios**:

1. **Given** a marathon run with checkpointed steps, **When** the owning process is killed and a fresh session resumes the run, **Then** the reported position matches the durable rows exactly, completed checkpoints are not re-executed, and no recorded state is lost.
2. **Given** a checkpoint was written durable-first but its scoped commit did not land, **When** the run is resumed, **Then** the re-drive completes the commit without duplicating the step.

---

### Edge Cases

- Form-(a) and form-(b) outcomes diverge on any vector during the equivalence verification: treated as a defect in form (b) (form (a), already green, is the reference), reported via the bug protocol before any fix.
- Guard suspends indefinitely because reachability is never bound: this is correct specified behavior (bounded by REPL step limits), not a defect; tests must distinguish it from a hang.
- Parity divergence between the approved guard and the shipped C# matcher on any shared vector: treated as a defect in the guard (the shipped matcher is the reference), reported via the bug protocol before any fix.
- Native QUIC library version drifts from the shipped host's QUIC dialect: conformance flow must fail loudly on version mismatch, not degrade.
- Two-host clock/cert path differences (Windows ↔ gavri): certificate distribution follows the 036 trust model exactly; any deviation is recorded, never worked around ad hoc.
- Empty-targets policy (`targets = []`) and excluded-target-with-unbound-reachability: FR-002's "some target is reachable" reading fails an empty-targets policy while the shipped C# matcher delivers vacuously (parity conflict with FR-005); and when a target is excluded (unsatisfiable regardless of reachability) while the reachable-set is unbound, FR-003 (suspend) and FR-004 (fail loud) intersect without a stated precedence. Both outcomes are OPEN questions fixed by the language authority at the T003 §1.14 realization checkpoint (decision vectors v05 / v12) BEFORE the shared vector file is created — recorded here, not guessed.

## Requirements *(mandatory)*

### Functional Requirements

**Deliverable A — GLP policy-guard (§1.14-gated)**

- **FR-001**: The §1.14 ruling on the concrete guard signature `satisfiable(Policy?, Reachable?)` and its three-valued semantics MUST be obtained from the language design authority and durably recorded (in this spec's Clarifications) BEFORE any guard implementation, compilation, or execution. **RULING RECORDED 2026-07-08 (Clarifications, Session 2026-07-08): APPROVED staged — form (a) defined guard first, then evolve into form (b) system guard primitive, with mandatory (a) ≡ (b) equivalence verification.** Implementation is unlocked in that staged order only.
- **FR-001a**: After the form-(a) guard is implemented and green, the guard MUST be evolved into form (b) — a system guard primitive — and BOTH forms MUST produce identical three-valued outcomes (Success/Suspend/Fail) across the shared decision-vector set and the four proposal worked-examples. Any divergence is a defect reported via the bug protocol before any fix.
- **FR-002**: The guard (both forms) MUST succeed exactly when some target in the policy is reachable AND no target is excluded, per the proposal's reading.
- **FR-003**: The guard (both forms) MUST suspend while the reachable-set (or a needed prefix) is an unbound reader — no default fallback may fire on unknown reachability.
- **FR-004**: The guard (both forms) MUST fail loudly when the policy is ground and unsatisfiable; a drop may only occur through an explicit otherwise-clause consistent with the 041 logged-never-silent drop taxonomy (C23).
- **FR-005**: Guard decisions (both forms) MUST agree 100% with the shipped C# PolicyMatcher (contract C23) on a shared decision-vector set covering deliver and no-route outcomes; the Suspend arm is additional guard-only behavior.
- **FR-006**: The shipped C# PolicyMatcher MUST remain the production routing evaluator and MUST NOT be modified by this feature, regardless of the ruling.
- **FR-007**: Regression tests for all four worked-example outcomes MUST be added to the unified test suite as typed programs with procedure declarations, exercised under form (a) and re-exercised after the form-(b) evolution.
- **FR-008**: If the form-(b) evolution hits an unforeseen kernel-level blocker, the ship gate REMAINS CLOSED: the blocker is recorded with evidence and escalated to the engineer (Gabi); only an express re-ruling can re-scope the wave. No unilateral deferral, no silent omission. (The original reject/defer arm was resolved by the recorded approval; the ship gate is all-four-user-stories per Clarifications.)

**Deliverable B — 036 link full acceptance**

- **FR-009**: The BEAM runtime MUST complete the 036 conformance flow (connect, pin-verify, full-duplex) using an in-process native QUIC transport (Profile C), with pass criteria equal to the recorded Profile A baseline.
- **FR-010**: The Profile C provisioning path (native library acquisition/build) MUST be documented and reproducible; if provisioning fails, the blocked state MUST be recorded with evidence and escalated — the ship gate stays closed until the blocker is resolved or the engineer (Gabi) expressly re-scopes. Never a silent skip, never a unilateral deferral.
- **FR-011**: The 036 quickstart conformance flow (including the ≥4-client mesh criterion) MUST pass with endpoints on two physical LAN hosts (this host + gavri), using the 036 certificate trust model unchanged.
- **FR-012**: Marathon durability MUST be verified on a real persisted run: kill the owning process mid-run, resume in a fresh session, and confirm position derives purely from durable rows with no lost or duplicated work (including the durable-first/commit re-drive path).
- **FR-013**: All acceptance evidence (commands, outputs, host identities, pass/fail per criterion) MUST be captured in the feature's verification artifacts and referenced from the tasks.

**Wave mandate**

- **FR-014**: When this wave ships and closes, the original roadmap features `glp-policy-guard` and `http3-quic-ws-link-full-acceptance` MUST be advanced to shipped/closed with traceability to this feature recorded.
- **FR-015**: The four lower-severity codexreview findings carried into this feature by the 036 follow-up brief MUST be fixed with regression coverage: #3 mesh duplicate `endpoint_id` eviction guard, #5 demo harness AttributeError on handshake timeout, #6 latent pre-readiness stdout-pipe hang, #7 gleam relay >1 MiB line-mode misroute (length-framed read).
- **FR-016**: A self-contained gavri task prompt MUST be produced as a feature artifact (`gavri-task-prompt.md`) delegating US2 + US3 to a session on the gavri host: own branch off this feature branch (push-only-own-branch), environment discovery, Profile C provisioning + in-process conformance, the two-host LAN run paired with this host, and evidence committed under `specs/049-wave1-guard-link-acceptance/evidence/gavri/` — pushed early and continuously so results feed back into this repo before the sub-task completes.

### Key Entities

- **Routing Policy**: `{targets, waypoints, excludes}` over authenticated peer names (never certificates/pins) — mirrors the shipped C# RoutingPolicy.
- **Reachable Set**: the currently-reachable authenticated peers at a hop; may be unknown (unbound) at evaluation time.
- **§1.14 Ruling**: the language authority's recorded decision on the guard signature/semantics (approve-a / approve-b / revise / reject), with date and scope.
- **Acceptance Evidence**: per-criterion record of the Profile C, two-host, and marathon-durability runs (host, command, output, verdict).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A §1.14 ruling is durably recorded before any guard code exists; an audit of the feature history shows zero guard implementation/compile/run events preceding it.
- **SC-002**: All four proposal worked-examples reproduce their specified outcomes (Success, Fail, Fail, Suspend) in the REPL, verified by suite runs — under form (a), and again under form (b) after the evolution.
- **SC-003**: 100% decision parity between the guard and the shipped C# matcher across the shared vector set; zero silent fallbacks or silent drops in any test.
- **SC-009**: The (a) ≡ (b) equivalence verification shows identical three-valued outcomes for 100% of the shared vector set across both guard forms.
- **SC-004**: Repo baselines stay green: unified REPL suite at its pre-change baseline and all shipped C# suites unchanged-or-better.
- **SC-005**: Profile C conformance passes in-process on the full BEAM with results equal to the Profile A baseline (hard ship-gate).
- **SC-010**: The wave ships only when ALL FOUR user stories pass their acceptance scenarios — zero deferred ship-gate items at release.
- **SC-006**: The two-host LAN run passes every 036 quickstart criterion (including ≥4-client mesh) across two physical hosts.
- **SC-007**: A killed marathon run resumes with 100% of checkpointed state intact and zero duplicated step executions.
- **SC-008**: At wave close, both original roadmap features show shipped/closed with traceability to this feature.

## Assumptions

- The E6 approval-in-principle from feature 041 stands; only the concrete signature/semantics ruling is outstanding.
- The proposal file `programs/crdtmsg/policy-guard-proposal.glp` is the authoritative design artifact for the guard; no competing design exists.
- The gavri host is available on the LAN within this feature's window and can run one side of the 036 quickstart.
- US2 + US3 execute primarily on the gavri host per the recorded delegation; a native QUIC library for the BEAM (quicer NIF or equivalent) is obtainable on gavri (its toolchain is expected to build quicer where this host's MSVC absence blocked it in 036). This host runs the server side of the two-host pair and integrates the fed-back evidence.
- The gavri session has clone/push access to `github.com/olamni-glp/GLPNET` and can run Claude Code against a worktree of this repo.
- The 036 shipped artifacts (C# QUIC host, certificates workflow, conformance demo) are the unchanged substrate for Deliverable B; no protocol redesign is in scope.
- The marathon harness (buildkit-marathon, deploy-home catalog) is the durability substrate to verify; this feature verifies rather than modifies it.
- Multi-runtime expansion beyond Profile C (e.g. WASM Profile A hardening, live glp_repl bridge) is OUT of scope — it belongs to `http3-quic-ws-link-completion` (Wave 5).
