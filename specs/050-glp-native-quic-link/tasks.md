---

description: "Task list for feature 050 — GLP-Native True-QUIC Link"
---

# Tasks: GLP-Native True-QUIC Link — Genuine GLP Over the Wire

**Input**: Design documents from `specs/050-glp-native-quic-link/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED (tests-first). The spec's per-story "Independent Test" sections, the contract "Tests" sections, and Constitution VII (test-gated) make tests mandatory here — xUnit for host-side, the REPL suite (`test/run_all_tests.sh`) for GLP-level, plus a manual two-host acceptance run.

**Organization**: grouped by user story (P1→P5). Each story is an independently demonstrable slice.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no dependency on an incomplete task)
- **[Story]**: US1..US5 (user-story phases only)

## Discipline banners (apply to every task)

- 🔴 **FR-019 / Constitution IV-a**: reuse the 025 kernels + GLP wrappers UNCHANGED; `"quic"` is data. NO new GLP kernel/guard/system-predicate/primitive. If one seems necessary → STOP and propose-first; never a bespoke evaluator or shadow layer.
- 🔴 **Constitution III (SRSW)**: every `.glp` file is SRSW-clean; zero `skipSRSW`.
- 🔴 **Constitution VII**: baseline green before change; re-test after; commit only 050 files by name.
- 🔴 The single genuine QUIC transport driven is `csharp/glp_link/transports/QuicTransport.cs` (in-process) — NOT `csharp/glp_crdtmsg/route/QuicLinkTransport.cs` (side-process; research D-4).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: known-good baseline + environment readiness.

- [X] T001 Baseline: run `dotnet test csharp/glp_link.tests` + `dotnet test csharp/glp_crdtmsg.tests` and `bash test/run_all_tests.sh`; confirm green (REPL baseline 524/525, 1 pre-existing AOT-smoke fail), then commit a baseline checkpoint (scoped to no changes — record the SHA).
- [X] T002 [P] Probe `QuicTransport.IsSupported` on this host (a throwaway `dotnet` probe or an xUnit skip-guard) and confirm `glpquick-cert/{glpquick.pfx,glpquick.fingerprint}` load: `X509CertificateLoader.LoadPkcs12` succeeds and `QuicTransport.SpkiPin(cert)` equals the fingerprint file. Record findings in `specs/050-glp-native-quic-link/research.md` (D-5 note).
- [X] T003 [P] Create `programs/tests/quic/` and confirm the reused wrappers `server_listener`/`client_connector`/`link_send`/`link_close`/`link_monitor` exist in `programs/self.glp` (no edits) — record the line anchors.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: prerequisites shared by every story. ⚠️ No user-story work begins until this phase is complete.

- [X] T004 Reconciliation (READ-ONLY, no code): study `csharp/glp_crdtmsg/route/QuicLinkTransport.cs` + `route/LinkTransport.cs` vs `csharp/glp_link/transports/QuicTransport.cs`; write into `research.md` D-4 exactly how the crdtmsg router relates to the 025 link path, confirm 050 drives the in-process `glp_link` leaf, and flag any risk of double-wiring. If the router already carries a reusable crdtmsg envelope surface, note it for T017.
- [X] T005 Implement the cert/pin loader helper (composition-root scope): load `glpquick-cert/glpquick.pfx` → `X509Certificate2` (with private key, for mutual presentation) and `glpquick-cert/glpquick.fingerprint` → expected SPKI pin string; **fail-closed** (loud startup error) on missing/unreadable material — never a degraded no-pin mode. Place in the REPL shim area or a small `glp_link` helper consumed at the root.

**Checkpoint**: foundation ready — user stories can begin (in priority order or parallel).

---

## Phase 3: User Story 1 - A GLP goal establishes a genuine QUIC link and one bind crosses the wire (Priority: P1) 🎯 MVP

**Goal**: register `QuicTransport` into the REPL `LinkRuntime`; a GLP goal opens a genuine QUIC+WS link and one writer→reader bind crosses the real wire, reactivating a suspended reader exactly once. (Uses the existing default payload codec — crdtmsg is US2.)

**Independent Test**: on two LAN hosts (or a hermetic real-QUIC endpoint), a GLP `server_listener`/`client_connector` over a `"quic"` `link_id` completes a QUIC handshake (verifiable, not loopback/TCP), establishes `ch(In, Out?)`, and one bind reactivates a suspended reader exactly once; assert no TCP/loopback fallback.

### Tests for User Story 1 ⚠️ (write first, must fail before impl)

- [X] T006 [P] [US1] xUnit: registering `QuicTransport` → `Transports.Select(LinkScheme.Quic)` returns it; `Select` on an unregistered scheme throws — `csharp/glp_link.tests/QuicRegistrationTests.cs`.
- [X] T007 [P] [US1] xUnit: real QUIC link established via the kernel path; a writer→reader bind crosses; the reader suspends until the value arrives and reactivates **exactly once** — `csharp/glp_link.tests/QuicLinkOneBindTests.cs` (skip-guarded on `QuicTransport.IsSupported`).
- [X] T008 [P] [US1] xUnit: `IsSupported == false` path → loud fault (no TCP/loopback fallback, FR-002) — `csharp/glp_link.tests/QuicLinkOneBindTests.cs`.

### Implementation for User Story 1

- [X] T009 [US1] Register `new QuicTransport(cert, pin)` in `out/csharp/glp_repl/Program.cs` (composition root, after tcp/loopback) per `contracts/transport-registration.md`; use the T005 loader. (Depends on T005.)
- [X] T010 [US1] Confirm the kernels reach the quic leaf UNCHANGED — trace `LinkTerms.ParseLinkId` → `LinkScheme.Of("quic")` → `TransportRegistry.Select` in `LinkSetupKernel`/`LinkListenKernel`; assert no kernel/wrapper edit was needed (FR-001/FR-019).
- [X] T011 [US1] Author `programs/tests/quic/quic_one_bind.glp` — role-parameterized listener/connector opening one `"quic"` link and crossing one bind; SRSW-clean, `procedure`-declared.
- [X] T012 [US1] Add a REPL regression to `test/run_all_tests.sh` that loads `quic_one_bind.glp` and asserts the one-bind reactivation (against a hermetic real-QUIC endpoint where available; otherwise gate on host QUIC support).

**Checkpoint**: MVP — a GLP goal brings up one genuine QUIC link and a bind crosses it. STOP and VALIDATE.

---

## Phase 4: User Story 2 - Messages on the wire are 041 crdtmsg envelopes (Priority: P2)

**Goal**: the `"quic"` link's wire payload is a well-formed 041 crdtmsg envelope (incl. the rich-text CRDT), decoded losslessly with loud-fail on malformed input — via a host-side `IPayloadCodec` seam, kernels unchanged.

**Independent Test**: send a crdtmsg message (incl. a rich-text edit op) over the link; peer decodes losslessly incl. unknown-ignorable sections; malformed inputs (bad version, unknown must-understand tag, truncation, trailing bytes) rejected loud-fail; zero ad-hoc-string payloads on the wire.

### Tests for User Story 2 ⚠️ (write first, must fail before impl)

- [ ] T013 [P] [US2] xUnit: crdtmsg round-trip over a real quic link incl. one rich-text edit op; lossless incl. unknown-ignorable sections (using `csharp/glp_crdtmsg.tests` `SampleMessages.All()` incl. `"rich"`) — `csharp/glp_link.tests/CrdtMsgOnLinkTests.cs`.
- [ ] T014 [P] [US2] xUnit: malformed inputs (bad codec version byte, unknown must-understand tag, truncation, trailing bytes) rejected loud-fail — `csharp/glp_link.tests/CrdtMsgOnLinkTests.cs`.
- [ ] T015 [P] [US2] xUnit: the L5 payload observed on the wire is a crdtmsg envelope (SC-002), zero ad-hoc strings — `csharp/glp_link.tests/CrdtMsgOnLinkTests.cs`.

### Implementation for User Story 2

- [X] T016 [US2] Define `IPayloadCodec` (term-in/bytes-out, host-side, below GLP) in `csharp/glp_link/seam/IPayloadCodec.cs`; default impl preserves current `PayloadSerializer` behaviour byte-for-byte.
- [ ] T017 [US2] Implement `CrdtMsgPayloadCodec` over `GlpRuntime.CrdtMsg.MessageCodec` in `csharp/glp_crdtmsg/` (which already references `glp_link`), injected at the composition root — the reference-cycle resolution in research D-1. Carries the rich-text model (FR-006).
- [ ] T018 [US2] Wire per-link codec selection in `csharp/glp_link/primitives/LinkEstablish.cs`: the quic link gets `CrdtMsgPayloadCodec`; loopback/tcp keep the default. (Depends on T016, T017.)
- [X] T019 [US2] Edit `csharp/glp_link/primitives/LinkEgress.cs` `ShipGround` (line ~36) to encode via the link's `IPayloadCodec` instead of the hard-coded `PayloadSerializer`.
- [X] T020 [US2] Edit `csharp/glp_link/primitives/LinkPump.cs` inbound decode to use the link's `IPayloadCodec` before extending the `In` stream.
- [ ] T021 [US2] Confirm the 025 `FrameCodec` (length+CRC+seq) still wraps the crdtmsg bytes unchanged (FR-016 reliability preserved); resolve research D-1 SC-002 reading (L5-payload-is-envelope) and record it in `research.md`.

**Checkpoint**: US1 + US2 — genuine QUIC carrying crdtmsg envelopes.

---

## Phase 5: User Story 3 - Macaroons gate link establishment and maintenance (Priority: P3)

**Goal**: establishment and gated actions present a static macaroon in the envelope capability slot and `Macaroon.Verify()` it before acting; refusals fail closed, recorded, never a crash.

**Independent Test**: open with a valid macaroon (succeeds); absent/tampered/expired (fails closed, refusal recorded, no crash); mid-session gated action with invalid capability (refused + recorded, run graceful).

### Tests for User Story 3 ⚠️ (write first, must fail before impl)

- [ ] T022 [P] [US3] xUnit: valid macaroon, all caveats satisfied → establishment proceeds — `csharp/glp_link.tests/MacaroonGateTests.cs`.
- [ ] T023 [P] [US3] xUnit: absent/tampered/expired/unsatisfiable/un-understood → fail closed, refusal recorded (`ProvenanceOutcome.Refused`), zero crashes — `csharp/glp_link.tests/MacaroonGateTests.cs`.
- [ ] T024 [P] [US3] xUnit: gated action mid-session with an invalid capability → verify-before-act refuses + records, run stays graceful — `csharp/glp_link.tests/MacaroonGateTests.cs`.

### Implementation for User Story 3

- [ ] T025 [US3] Resolve research D-2 (capability-on-wire surface: extend binary to v2 — 041-coordinated, propose-first — vs. JSON-surface stopgap) and record the decision; carry the macaroon in the crdtmsg capability slot (`header/CapabilitySlot.cs`, section `0x20`) accordingly.
- [ ] T026 [US3] Gate `LinkEstablish` on `Macaroon.Verify(rootKey, context, understoodKeys)` BEFORE wiring the link; on failure record via `cap/Provenance.cs` and surface a distinct refusal outcome (no crash, no silent drop).
- [ ] T027 [US3] Re-verify capability on gated actions during an established session (maintenance path); same fail-closed + record semantics.
- [ ] T028 [US3] Load the static-macaroon root key out-of-band alongside the cert (beacon static-macaroon model); fail-closed if absent.

**Checkpoint**: US1–US3 — genuine QUIC + crdtmsg + capability control.

---

## Phase 6: User Story 4 - The full cross-host mesh + performance + security + reliability test runs as GLP goals (Priority: P4)

**Goal**: a GLP program stands up the all-pairs 5-endpoint / 10-full-duplex-link mesh across the two hosts as GLP goals and drives mesh + performance + security/cyber + reliability.

**Independent Test**: every link opened by a GLP goal (no external harness); mesh reaches the concurrency floor; perf targets met; each security scenario yields the expected recorded outcome; each reliability property holds.

### Tests for User Story 4 ⚠️ (write first, must fail before impl)

- [ ] T029 [P] [US4] xUnit: multi-accept mesh via `QuicTransport.CreateListenerAsync`/`QuicListenerHandle` — N isolated client links from one UDP port, one link's fault never touches a sibling — `csharp/glp_link.tests/QuicMeshTests.cs`.
- [ ] T030 [P] [US4] xUnit: reliability — duplicate suppression (`msg_id` + per-link `seq`), exactly-once remote reader reactivation, fault reporting via the monitor stream — `csharp/glp_link.tests/QuicReliabilityTests.cs`.
- [ ] T031 [P] [US4] xUnit: cyber — rogue/non-pinned peer rejected (pin-mismatch handshake fail), tampered signed block rejected (`csharp/glp_crdtmsg/sig/Seals.cs`), zero false accepts — `csharp/glp_link.tests/QuicCyberTests.cs`.

### Implementation for User Story 4

- [ ] T032 [US4] Author `programs/tests/quic/quic_mesh.glp` — role-parameterized; opens all peer-pair links as GLP goals (C(5,2)=10 full-duplex, one `ch(In,Out?)` per pair); SRSW-clean, `procedure`-declared.
- [ ] T033 [US4] Add the performance harness inside the GLP program: message round-trip latency + sustained throughput (provisional SC-005 targets — median < 50 ms, ≥ 1000 msgs, zero loss; confirm per research D-3 before treating as a firm gate).
- [ ] T034 [US4] Security/cyber scenarios as GLP goals: capability refusal, whole- and sub-content tamper detection, cert-pin enforcement against a rogue peer — each a recorded outcome.
- [ ] T035 [US4] Reliability scenarios as GLP goals: duplicate suppression, exactly-once reactivation, fault reporting on the 025 monitor stream.
- [ ] T036 [US4] Interop-readiness (FR-013a): delivered endpoints stand up listeners/acceptors honoring mutual-pin QUIC + macaroon + crdtmsg so the 3 pre-built MAUI C# apps can join the 10-link mesh (per `contracts/mesh-test-harness.md`); do NOT build/modify the MAUI apps.

**Checkpoint**: US1–US4 — the headline cross-host demonstration, driven entirely by GLP.

---

## Phase 7: User Story 5 - The run concludes with graceful termination (Priority: P5)

**Goal**: drain in-flight → clean `link_close` on every link → orderly teardown of listeners/connectors/streams/QUIC connections; zero crashes; immediate re-run needs no manual cleanup.

**Independent Test**: complete a run, trigger termination; every link drains + closes cleanly, resources released, process exits with no error, immediate re-run succeeds.

### Tests for User Story 5 ⚠️ (write first, must fail before impl)

- [ ] T037 [P] [US5] xUnit: graceful close — drain in-flight, `link_close` each link, `RecvBytesAsync` returns null on close, teardown with no crash — `csharp/glp_link.tests/QuicTeardownTests.cs`.
- [ ] T038 [P] [US5] xUnit: re-run after teardown re-establishes with no leftover listeners/connections (port released) — `csharp/glp_link.tests/QuicTeardownTests.cs`.
- [ ] T039 [P] [US5] xUnit: peer disappears mid-drain → fault reported via monitor stream, teardown still completes gracefully — `csharp/glp_link.tests/QuicTeardownTests.cs`.

### Implementation for User Story 5

- [ ] T040 [US5] Add the termination sequence to `programs/tests/quic/quic_mesh.glp`: drain in-flight, `link_close` on every link, ordered teardown — via the existing `link_close` kernel (no new kernel).
- [ ] T041 [US5] Verify resource release through `csharp/glp_link/reliability/LinkReclaimer.cs` (listeners/connectors/streams/QUIC connections); confirm an immediate re-run is clean (FR-018).

**Checkpoint**: all five stories independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T042 [P] Update `docs/known-issues.md` with any limitation surfaced (e.g. QUIC-unsupported hosts, capability-surface stopgap) and point `specs/050-*/quickstart.md` from the feature docs.
- [ ] T043 Run the full `quickstart.md` two-host acceptance (Olamnit 192.168.0.136 + gavri 192.168.0.108); record SC-001..SC-008 results (SC-005 against confirmed or provisional targets).
- [ ] T044 Re-run `dotnet test csharp/glp_link.tests` + `csharp/glp_crdtmsg.tests` + `bash test/run_all_tests.sh`; confirm green; commit 050 files by name.
- [ ] T045 [P] FR-019 audit: confirm zero new GLP kernels/primitives were introduced (kernels/wrappers diff clean) and `grep -c skipSRSW` over the new `.glp` files is 0.

---

## Dependencies & Execution Order

### Phase dependencies
- **Setup (P1)**: no deps.
- **Foundational (P2)**: after Setup — BLOCKS all stories (T005 cert loader gates US1 registration; T004 reconciliation gates the codec/mesh work).
- **US1 (P3)**: after Foundational — the MVP, no dependency on other stories.
- **US2 (P4)**: after Foundational; builds on US1's established link but the codec seam is independently testable over a quic loopback link.
- **US3 (P5)**: after US2 (the macaroon rides in the crdtmsg capability slot).
- **US4 (P6)**: after US1–US3 (integration slice).
- **US5 (P7)**: after US4 (terminates the mesh the US4 program builds).

### Within each story
- Tests (T006–T008, T013–T015, T022–T024, T029–T031, T037–T039) written and FAILING before implementation.
- Seam/model before services; C# host-side before the `.glp` program that drives it.

### Parallel opportunities
- Setup: T002, T003 parallel.
- All per-story test tasks marked [P] run in parallel within their phase.
- US2's T019/T020 touch different files (LinkEgress vs LinkPump) — parallel after T016/T017/T018.

## Parallel Example: User Story 1

```text
# Tests first (parallel):
T006 QuicRegistrationTests.cs
T007 QuicLinkOneBindTests.cs (one-bind)
T008 QuicLinkOneBindTests.cs (no-fallback)   # same file as T007 → sequence, not [P] within the file
# Then impl: T009 (register) → T010 (verify kernels) → T011 (.glp) → T012 (REPL regression)
```

## Implementation Strategy

- **MVP** = Setup + Foundational + US1. STOP and validate one genuine QUIC bind crossing the wire before proceeding.
- **Incremental**: US2 (crdtmsg on wire) → US3 (macaroon gate) → US4 (full mesh/perf/security/reliability) → US5 (graceful termination). Each adds value without breaking prior stories.
- **Residual clarifications** (research D-1 SC-002 reading, D-2 capability surface, D-3 perf targets) are resolved inside T021/T025/T033 respectively — expect `/bk-analyze` to flag them; resolve there or in the "apply top remediations" pass.

## Notes
- [P] = different files, no incomplete-task dependency.
- Reuse kernels/wrappers unchanged; the single genuine QUIC leaf is `glp_link/QuicTransport.cs` (in-process).
- Commit after each task or logical group, 050 files by name only.
