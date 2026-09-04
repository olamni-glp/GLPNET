<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: QUIC federation transport for the ynet oracle

**Input**: Design documents from `/specs/102-quic-federation-transport/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

**Tests**: REQUIRED and non-optional here. The spec's success criteria are written as measurements
(SC-001..SC-013) and SC-007 explicitly demands a **positive and a negative control** for every
reported state. A test suite without negative controls does not satisfy this feature.

**Organization**: grouped by user story. The order below is **dependency order, not priority order**
— User Story 3 (term ordering) is P1 *and* a hard precondition of the first merge, so it ships first
even though User Story 1 is the MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency)
- Every task names exact file paths and the FR/SC it discharges.

## Baseline (do this before T001)

- Full C# suite green: `dotnet test csharp/GlpRuntime.sln` — recorded baseline **190/190** on
  Gavriella 2026-09-04. Re-run and re-record before the first change; any task that reduces this
  count is a stop-and-report, not a "known failure".

---

## Phase 1: Setup

- [ ] **T001** Create `csharp/glp_crdtmsg/federation/` and `csharp/glp_crdtmsg.tests/federation/`.
      No new package reference — the feature is BCL + in-repo only (plan Technical Context).
- [ ] **T002** [P] Create `csharp/ynet_federation/YnetFederation.csproj` (`net11.0`, console,
      references `glp_crdtmsg` and `ynet_transport`) and add it to `csharp/GlpRuntime.sln`.
- [ ] **T003** [P] Add SPDX headers to every new file, matching the repo convention.

**Checkpoint**: solution builds with the new empty project; suite still 190/190.

---

## Phase 2: Foundational (blocking — no user story may start until this is done)

- [ ] **T004** `federation/TermSpace.cs` — `TermSpace(Id, SpaceKind)`, `SpaceKind{Live,Legacy,Unknown}`,
      classification of an operation's space against the configured live epoch. **FR-026, FR-027**,
      data-model I-5..I-8.
- [ ] **T005** `federation/FederationTerm.cs` — `Term(SpaceId, EraCounter, HostId)` and the
      three-valued `TermOrder Compare(Term,Term)`. **No constructor takes a bare number.**
      **FR-013, FR-014**, contract C1/C2.
- [ ] **T006** [P] `federation/FederationOp.cs` — the op envelope (`OpId` as `Dot`, `Origin`, `Kind`,
      optional `Term`, `Deps`, `PredHash`, `Body`) with canonical JSON (de)serialisation matching
      contract W3. Reuses `Crdt.Dot` and `Crdt.HashChain` **unchanged**. **FR-009**, I-13..I-16.
      **The type exposes no removal operation at all** (I-16).
- [ ] **T007** [P] `federation/NodeIdentityStore.cs` — mint-once / load-thereafter persistence of the
      node key at `identity_path`; derives the X509 cert for `QuicLinkTransport` from the **same**
      key so `SpkiPin == NodeId` (research R3). Owner-only file permissions; key never logged.
      **FR-007**, I-4, contract G4.
- [ ] **T008** `federation/FederationConfig.cs` — load / defaults / validate / read-back-effective.
      Defaults `enabled=false`, `peers=[]`. Validation refusals exactly per contract G3, each naming
      the field and the reason. **FR-002, FR-003, FR-004, FR-026**, I-29..I-32.
- [ ] **T009** `federation/PeerSet.cs` — pins keyed by `NodeId`, endpoints as a **list**;
      `PinMismatch` / `Unreachable` / `NotInPeerSet` as **distinct** conditions. **FR-006, FR-007,
      FR-008**, I-20..I-23.
- [ ] **T010** `federation/FederationFold.cs` — union-by-id fold over `VersionVector.Contains`,
      append-only, with deterministic canonical serialisation so two folds can be compared
      **byte-for-byte**. **FR-010, FR-011, FR-012**, contract W6.

**Checkpoint**: foundation compiles; suite still 190/190; no behaviour is reachable yet by design.

---

## Phase 3: User Story 3 — a merge cannot be poisoned by a stale or fabricated term (P1)

*The only irreversible part of the feature. Fully testable offline, with no network.*

- [ ] **T011** `federation/RetirementOp.cs` — `Retire(targetOpId, reason)` producing an ordinary
      `FederationOp` with `Kind="retire"`, `IntoSpace=Legacy`. Idempotent. **FR-017, FR-029**,
      contract C6.
- [ ] **T012** `federation/MergeGate.cs` — `MergeVerdict CanMerge(PeerCapabilities)`; refuses when
      **either** side is not term-space aware, with a **specific** reason string. **FR-018**,
      contract C7.
- [ ] **T013** Wire the epoch counter so it advances **only** on a leadership event; assert by
      construction that no clock, tick, or timer touches `EraCounter`. **FR-015**, contract C3.
- [ ] **T014** [P] `tests/federation/TermOrderingTests.cs` — **SC-005 negative control**: a synthetic
      op in space `"foreign"` carrying `long.MaxValue` MUST NOT beat a live-space op carrying `1`.
      Plus: `Incomparable` is returned as a **third value**, not `false`.
- [ ] **T015** [P] `tests/federation/TermOrderingTests.cs` — **SC-012, both halves in one test**:
      after retirement the target op is **still present** in the log **and** is excluded from the
      ordering decision. Splitting these into two tests permits one to be dropped; do not.
- [ ] **T016** [P] `tests/federation/TermOrderingTests.cs` — **SC-013**: mint a second epoch; assert
      every prior-epoch op is still readable and correctly attributed.
- [ ] **T017** [P] `tests/federation/TermOrderingTests.cs` — **FR-015**: advance a fake clock by
      7 days with no leadership event; assert `EraCounter` is unchanged.
- [ ] **T018** [P] `tests/federation/TermOrderingTests.cs` — **FR-018 gate is load-bearing**: a peer
      advertising no term-space capability is refused. Deleting `MergeGate` must make this test fail.
- [ ] **T018a** [P] `tests/federation/TermOrderingTests.cs` — **FR-016, FR-031, SC-015** *(added by
      the analyze pass, finding C1)*: an op from an **unrecognised** space, one from the **legacy**
      space, and one carrying **no term** produce **three different** reported results. The test must
      fail if any two are collapsed. Without this, `Unknown` and `Legacy` could render identically —
      the same two-states-one-output defect SC-007 exists to forbid, one layer down.

**Checkpoint**: the 🛑 STOP ORDER of ruling `Q-GLPNETG27-03` is now liftable — the fold is
term-space aware and the fossil has an additive remedy. **US3 is independently shippable.**

---

## Phase 4: User Story 2 — an operator can tell whether federation is actually working (P1)

*Testable with no second machine.*

- [ ] **T019** `federation/FederationStatus.cs` — the four `Tri` states plus `SameMachine` and
      `PolicyRefused`. **No aggregate `IsFederated` boolean, and none may be added.** **FR-019**,
      contract S1.
- [ ] **T020** Each state is set **only** by its own measurement per contract S2's table; no state is
      inferred from an earlier one. **FR-020**.
- [ ] **T021** Unmeasurable ⇒ `Unknown`, rendered as the literal word with a reason, never as blank
      or `no`. **FR-021**, contract S3/S7.
- [ ] **T022** `federation/PolicyRefusal.cs` — catch `FileLoadException` HRESULT `0x800711C7` and
      surface `PolicyRefusal("Smart App Control", 0x800711C7, …)` as a **distinct named** startup
      failure. **FR-023**, contract S5, research R7.
- [ ] **T023** `SameMachine` detection by participant address family and host binding (**not** by
      nodeId — two nodeIds on one machine are still two nodeIds). **FR-022**, contract S4.
- [ ] **T024** [P] `tests/federation/StatusSurfaceTests.cs` — **SC-007**: for each of the four states,
      a positive control **and** a negative control, asserting the two produce **different** reported
      results. Identical output in both directions is a failing test.
- [ ] **T025** [P] `tests/federation/StatusSurfaceTests.cs` — **SC-010**: remove the ability to
      measure a state; assert `Unknown`, and assert it is **not** `No`.
- [ ] **T026** [P] `tests/federation/StatusSurfaceTests.cs` — **FR-022**: a same-machine crossing sets
      `OpReceivedFromPeer=Yes` **and** `SameMachine=true`, and the rendered output does not claim
      cross-host federation.

**Checkpoint**: the surface cannot report a false green. **US2 is independently shippable.**

---

## Phase 5: User Story 4 — a reachable listener is not an open one (P2)

- [ ] **T027** Admission path: mutual verification completes **before** any board data is exchanged;
      empty peer set admits nobody. **FR-005, FR-006**, contract W2.
- [ ] **T028** [P] `tests/federation/AdmissionTests.cs` — **SC-004 negative control**: dial with an
      unpinned identity; assert the connection is refused **and that zero bytes of board data
      crossed**. Asserting only "refused" does not test FR-006.
- [ ] **T029** [P] `tests/federation/AdmissionTests.cs` — **SC-006**: a peer configured with two
      endpoints (`192.168.0.136` and `.129`) counts as **one** participant.
- [ ] **T030** [P] `tests/federation/AdmissionTests.cs` — **FR-008**: a pin mismatch reports
      `PinMismatch`, distinguishable from `Unreachable` and from a generic transport error.

**Checkpoint**: the port can be opened safely. **US4 is independently shippable.**

---

## Phase 6: User Story 1 — a lane on one host sees a claim made on another host (P1) 🎯 MVP

- [ ] **T031** `federation/FederationService.cs` — bind (`FR-001`, refuse a loopback bind while
      enabled), dial by **literal IPv4** (`FR-003`), and report a name-resolution failure as
      `NameResolutionFailed`, never as a transport failure.
- [ ] **T032** Durability order: **append locally, then ship** — **FR-030** (contract W4). Never the
      reverse. *(FR-030 was promoted from contract-only to a spec requirement by the analyze pass,
      finding U1: a load-bearing data-safety rule may not live only in the plan layer.)*
- [ ] **T033** Push-on-append leg. **FR-028**, 5 s steady-state target.
- [ ] **T034** Pull backstop every 60 s, exchanging **version vectors first** and transferring only
      the ops the peer lacks (reusing `VersionVector.Join`/`Contains`). Shipping the whole log is a
      broadcast storm, not a backstop. **FR-028**.
- [ ] **T035** Degradation: peer unreachable ⇒ `Degraded(local-only)` reported **explicitly**, local
      oracle unchanged, never reported as success. **FR-004**, contract W7.
- [ ] **T036** [P] `tests/federation/FoldConvergenceTests.cs` — **SC-002**: ship the same op **twice**;
      assert the fold contains it **once**.
- [ ] **T037** [P] `tests/federation/FoldConvergenceTests.cs` — **SC-003**: fold op-set `S` in order
      `p` and in reversed order `p'`; assert the two serialised folds are **byte-equal**. A custom
      "equivalent" comparer would hide the bug being tested.
- [ ] **T038** [P] `tests/federation/FoldConvergenceTests.cs` — **SC-011**: append while the link is
      down, restore the link, assert presence within **120 s**. Deleting the pull backstop must make
      this test fail.
- [ ] **T039** [P] `tests/federation/FoldConvergenceTests.cs` — **FR-030, SC-014** (contract W4):
      kill between local append and push; assert the op survives locally and is delivered by the
      backstop.
- [ ] **T040** `tests/federation/CrossHostAcceptanceTests.cs` — **SC-001**. Reads peer configuration;
      **with no peer present it SKIPS LOUDLY**, reporting *peer absent — SC-001 UNMEASURED*, and
      **never passes by default**. An unmeasured criterion reported green is exactly FR-021's
      prohibition and the reason for ruling `Q-GLPNETG28-02`.

**Checkpoint**: everything except SC-001 is measurable on this host alone.

---

## Phase 7: Operator surface, evidence, and the peer ask

- [ ] **T041** `csharp/ynet_federation/Program.cs` — verbs `status`, `config show|set|add-peer`,
      `identity init`, `epoch mint`, `serve`, `post`, `retire`, `revert --all`. Invoked via
      `dotnet run` (the signed host). **FR-002, FR-019**, contract G2.
- [ ] **T042** Change-recording: every enabling change appends its **reversal** to
      `changes.jsonl`; `revert --all` replays them in reverse. **FR-025**, contract G5.
- [ ] **T043** [P] `tests/federation/…` — **SC-009**: apply all three enabling changes, run the
      recorded reversals, assert the host is back to its prior state (config restored, key absent,
      rule absent).
- [ ] **T044** [P] `docs/runbooks/ynet-federation.md` — the operator runbook, derived from
      `quickstart.md`, including the elevated firewall one-liner **and its reversal**. **FR-024,
      SC-008**.
- [ ] **T045** **Execute the fossil retirement**: append the `retire` op for
      `628016928ab854ae` into the legacy space, on the live board, and record it.
      **🔴 Do not delete the op by any other means** — suppression is undetectable on an append-only
      board. **FR-017, FR-029**, ruling `Q-GLPNETG28-04`.
- [ ] **T046** **ACK-required broadcast** to all hosts and lanes: this host's `node_id`, its
      endpoint `192.168.0.108:47890`, the `space_id`, the runbook path, and an explicit request for
      one peer to stand up a listener so **SC-001 can be measured**. Ruling `Q-GLPNETG28-02`.
- [ ] **T047** Evidence pack: record which SCs are **measured**, which are **unmeasured**, and why —
      by name, never by aggregate. SC-001 is `UNMEASURED (no peer listener)` until a peer answers.

---

## Dependencies

```
Setup (T001-T003)
   └─> Foundational (T004-T010)          BLOCKS EVERYTHING
          ├─> US3  (T011-T018)           hard precondition of any merge
          ├─> US2  (T019-T026)           needs T004-T010 only
          ├─> US4  (T027-T030)           needs T009
          └─> US1  (T031-T040)           needs US3 (fold must be space-aware) + US4 (admission)
                 └─> Phase 7 (T041-T047)
```

- **US3 before US1** is not a preference: merging under the older ordering rule is the irreversible
  mistake (spec Dependencies, ruling `Q-GLPNETG27-03`).
- **T045 after T011** — the retirement mechanism must exist and be tested before it is used on the
  live board.
- **T046 after T044** — do not ask a peer to act before the runbook they need exists.

## Parallelisation

Within a phase, `[P]` tasks touch different files and may run together. The four test files
(`TermOrderingTests`, `StatusSurfaceTests`, `AdmissionTests`, `FoldConvergenceTests`) are disjoint and
fully parallel. `T002`/`T003` are parallel with `T001`.

## Definition of done for this era

1. Suite green and **higher than the 190 baseline** by the count of tests added; no test removed.
2. Every SC from SC-002..SC-013 **measured** on this host, each with its negative control.
3. SC-001 either **measured** with a real peer, or reported **UNMEASURED by name** with the
   ACK-required broadcast sent — per ruling `Q-GLPNETG28-02`. Never reported green.
4. The fossil op retired additively and still present in the log.
5. The runbook's reversal executed once and verified (SC-009).
