<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: YNET frame field parity across planes

**Feature**: `110-ynet-frame-field-parity` · **Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md)

Legend: `[ ]` open · `[x]` done · **FR/SC** = the requirement each task discharges.

## Phase 0 — baseline (Test Protocol: baseline before changing)

- [x] **T-001** Record the pre-change suite result for `ynet_client.tests` (numbers, not "green"). — *Test Protocol*
- [x] **T-002** Capture the two construction sites verbatim as the SC-006 before-image. — **SC-006**

## Phase 1 — the seam (no behaviour change)

- [x] **T-010** Add `internal YnetFrame BuildFrame(string signal, string body)` to `CoopFileOutbound`; move the `new YnetFrame { … }` and the sequence increment into it **verbatim**; `Send` calls it. — **FR-001**
- [x] **T-011** Add `internal YnetFrame BuildFrame(YnetMessage message)` to `QuicOutbound`; same treatment. — **FR-001**
- [x] **T-012** Ensure the test assembly can see internals (`InternalsVisibleTo`), adding it only if absent. — **FR-001**
- [x] **T-013** Re-run the suite; result MUST equal T-001 exactly. A seam that moves a number is not a seam. — *Test Protocol*

## Phase 2 — rulings as data

- [x] **T-020** Add `FrameFieldOutcome` (`MustAgree` / `MayDiverge`) and `FrameFieldRuling` (field, outcome, ruling id, rationale). — **FR-014**
- [x] **T-021** Populate the ruling table for all six envelope fields per plan §2, each carrying its Q-110-xx id. — **FR-008, FR-014**

## Phase 3 — the classifier

- [x] **T-030** Implement per-field classification returning `Agrees` / `DivergesRuled(ruling, both values)` / `DivergesUnruled(both values)`. — **FR-002, FR-004**
- [x] **T-031** Enumerate `YnetFrame`'s public properties by reflection; never a hand-maintained list; an unknown field that differs is `DivergesUnruled`. — **FR-007**
- [x] **T-032** Compare `Sequence` by **basis**, not absolute value (counters are per-carrier). — **FR-009**

## Phase 4 — the tests

- [x] **T-040** Non-empty guard as its own `[Fact]`, running before any equality is asserted. — **FR-003**
- [x] **T-041** Construct both carriers with stub identities and a temp COOP root; produce the constructed-frame pair for one identical logical send. — **FR-001**
- [x] **T-042** Assert every field is `Agrees` or `DivergesRuled`; a `DivergesUnruled` fails and names the field and both values. — **FR-005**
- [x] **T-043** Assert ruled divergences are **reported with both values** even though they pass — visible, not suppressed. — **FR-006**
- [x] **T-044** Assert the check reports all four measured divergences before the `Sequence` fix, and exactly three after (`Sequence` becomes `Agrees`). — **SC-002, SC-005**
- [x] **T-045** 🔴 **Non-tautological negative control**: force one field to differ on a real constructed pair, drive the **real classifier**, assert `DivergesUnruled` naming that field. Must NOT compare two literals. — **FR-010, SC-003**
- [x] **T-046** Negative control for an unknown field: a field the ruling table does not cover, differing, fails and names itself. — **SC-004**

## Phase 5 — the one authorised value change

- [x] **T-050** `CoopFileCarrier.cs:186`: `Interlocked.Increment(ref _sequence) - 1` → `Interlocked.Increment(ref _sequence)`. — **FR-013, Q-110-02**
- [x] **T-051** Assert frame filenames remain unique across a burst of sends (uniqueness is the GUID's job — assert it, do not assume it). — **FR-013**
- [x] **T-052** Re-run the suite; `Sequence` now classifies `Agrees`. — **SC-002**

## Phase 6 — record and close

- [x] **T-060** Correct the era-107 `FrameParityTests` **documentation** to state it tests serializer agreement, not carrier parity. Keep the tests — they test a real, different property. — **FR-012**
- [x] **T-061** Record the executed negative-control transcript in the spec dir (evidence, not a claim). — **SC-003**
- [x] **T-062** Diff both construction sites against the T-002 before-image; only the `Sequence` token may differ. — **SC-006**
- [x] **T-063** Full suite green with numbers stated; commit; push. — *Test Protocol*

## Out of scope — other lanes' allocations, named so they stay disjoint

iroh sidecar (`@gavriella.ospark` era 043) · iroh carrier (`@gavriella.qhstate`) · QUIC listener
(`@shiras.glpnet`) · election commit phase (`@shiras.ospark`) · coordinator tiers
(`@shiras.yngcor`) · deployment ledger (`@gavriella.buildkit`).
