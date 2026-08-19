<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 8e3f21a7-5c94-4d16-b2e8-31f7a9c05d42
-->

# Phase 0 research — Verification receipts and loud failure

**Feature**: `078-verification-receipts` · **Date**: 2026-08-18

No `NEEDS CLARIFICATION` markers remain in the plan's Technical Context. The six that existed were
closed **in the spec** at clarify (Clarifications → Session 2026-08-18) rather than here, because a
requirement-level ambiguity resolved in a research document is a spec that still reads ambiguous.
What follows is the design research the plan defers to, each entry recording what was rejected.

---

## R1 — Receipt storage: files, not the catalog

**Decision.** Receipts are JSON files at `.specify/receipts/<area>/<run-id>.json`. The verdict carries
a pointer to that path.

**Rationale.** Two of the six declared areas cannot use anything richer. `test/run_all_tests.sh` is
POSIX bash and the COOP protocol is a filesystem convention across three hosts; neither can hold a
database handle. A file is the only medium every declared area can write and every consumer can read.

**Alternatives rejected.**

- *Rows in the PGLite catalog.* Queryable and durable, and it would join to existing observability.
  Rejected on this feature's own evidence: the catalog is the component with the fleet's worst
  measured silent-failure record — marathon `capture` failed eight consecutive times and **exited 0
  every time**, and the lock has twice reported a holder PID that was already dead. A mechanism whose
  purpose is to prove a check ran must not depend on the component most likely to fail silently.
  Constitution VI-b is satisfied by avoidance, and the avoidance is deliberate.
- *Inline in the verdict payload only.* No path convention to maintain, nothing to lose on disk.
  Rejected because several checks in the declared areas emit only human-readable text today; making
  the receipt inline-only would force a structured-output migration as a precondition, converting an
  additive feature into a breaking one. Permitted as an **additive** upgrade per area (FR-022).

## R2 — Run-id derivation

**Decision.** `run-id = <UTC timestamp to the second>-<8 hex chars of a per-run nonce>`, generated
once at check start and threaded through. Bash gets it from `date -u +%Y%m%dT%H%M%SZ` plus
`$RANDOM`-derived hex; Python from `datetime` plus `secrets`.

**Rationale.** Must be (a) unique per run so FR-VI-a's write-once-per-`(area, run-id)` holds, (b)
sortable so a reader finds the newest receipt without parsing, and (c) derivable in bash without new
tooling. A timestamp alone fails (a) — two sections of the same suite can start within one second.

**Alternatives rejected.** A monotonic counter (needs shared state the harness has none of); a
content hash (not knowable until the check finishes, but the id is needed at start so a crashed run
still has an addressable receipt — the crash case in Edge Cases).

## R3 — The `Section I` collision must be fixed before per-section receipts exist

**Measured 2026-08-18**: `test/run_all_tests.sh` declares `Section I` at **line 1653**
(`self.glp Procedure Tests`) and again at **line 2219** (`Cross-runtime Gleam × C# link suite (US5)`).

**Decision.** Per-section receipts are keyed on **`(letter, slugified-title)`**, not the letter alone.

**Rationale.** Keying on the letter would make the two sections collide and one receipt would silently
overwrite the other — the precise failure mode this feature exists to prevent, manufactured by the
feature itself. Keying on the pair is collision-proof immediately and requires no rename, so no
handover, COOP post or ledger that refers to "Section I cross-runtime" goes stale.

**Alternatives rejected.** Renaming the cross-runtime section to a free letter (cleanest identity, but
it stales every existing reference and is cosmetic relative to the receipt requirement — it remains a
worthwhile follow-up chore, carried as register block 06); renumbering all sections (maximum churn,
least gain).

## R4 — Where the 13 witnessed instances live

Mapping the spec's evidence table onto the six declared areas, so US4's retrofit is a checklist and
not a search. **Counts are the plan's basis for sequencing US4.**

| declared area | instances | note |
|---|---|---|
| test harness | 5, 6, 7, 9 + Section U's stale-binary case | largest single source; also the only bash area |
| roadmap-sync | 4, 13 | instance 13 was found *while writing this spec*, by running the tools it governs |
| buildkit-3rtask | 3 | `brief`/`record-output` silently no-op on an existing role input |
| buildkit-codexreview | 1, 2 | 2 is the 5-of-5 omitted-findings-block case |
| COOP protocol | 8 | four separate poll/cursor defects, one hid a peer ACK for 14.5 h |
| codeconv build gate | 6 | compile-only, so a behaviourally-wrong generated file promotes |
| *(cross-cutting)* | 10, 11, 12 | retired root; `outstanding: 0` against a refusing gate; a guard that passed on the failing case |

**Consequence for sequencing.** The test harness carries the most instances *and* is the runtime that
cannot reuse the Python emitter, so it is the honest first adopter: if the contract cannot be emitted
from bash, that must surface before five Python areas are built against it.

## R5 — Bounding without defeating reconciliation

**Decision.** Cap **enumerations** (examined/skipped item lists) at a declared maximum; always record
the **true totals**; add a per-field byte backstop; a receipt that truncated says so and by how much.

**Rationale.** FR-005 and FR-010 pull against each other — one demands bounded receipts, the other
demands counts reconcilable against the target's true size. A naive byte cap satisfies FR-005 by
discarding exactly what FR-010 needs. Bounding the list while keeping the count satisfies both.

**Evidence this is not hypothetical.** While building the pre-install guard on 2026-08-18 a real
holder's command line ran to ~1,500 characters and buried the two fields identifying it. The byte
backstop exists for that case; the enumeration cap exists for large targets.

## R6 — Two manifests, one rule

**Decision.** The adoption manifest (FR-019) and the expected-checks manifest (FR-023) are separate
documents that share **one rule**: *absence of a declaration is an error — never a pass, never
"declared not-applicable".*

**Rationale.** They answer different questions — *does FR-008 bind here?* versus *what was this run
supposed to contain?* — but they fail the same way, and the fleet has now watched that exact vacuity
appear twice in one specification (FR-008, then FR-013). One shared rule means one mechanism to build,
one to audit, and no second place for a silent pass to hide.

**Alternatives rejected.** A single combined manifest (conflates area-level adoption with run-level
expectation, and would force a run to re-declare adoption); deriving expected-checks from the previous
successful run (a ratchet that only ever loosens — a check that vanished two runs ago becomes
permanently "not expected").

## R7 — Conformance is demonstrated, not asserted

**Decision.** The contract ships with a fixture that both repositories run; the fixture's output is
itself a receipt, validated against `receipt.schema.json`.

**Rationale.** FR-024 gives buildkit sole authority, which is only safe if glpnet can *prove* its
emitter conforms rather than trusting a version pin. Making the proof a receipt closes the loop: the
mechanism is verified by the invariant it defines, satisfying FR-016's spirit at the contract layer.

**Alternatives rejected.** Version pin alone (the fleet has measured that pins can be entirely
decorative — 30 deploy targets stamped with versions that had no bearing on the executing code);
prose conformance criteria (unenforceable, and two conforming implementations could disagree with
nobody finding out).

## R8 — Line endings are part of the contract

**Decision.** Receipts and manifests are written with `\n` and are pinned `text eol=lf` in
`.gitattributes` wherever they are tracked.

**Rationale.** Learned the same day, twice over: 242 tracked signed roadmap exports were exposed to
CRLF normalisation that would break signature verification (fixed at `d9d1e648`), and `.gitattributes`
already records 059 T051, where a CRLF checkout of `corpus.list` made all 44 runtime goldens report
MISSING. A byte-compared evidence artifact with unpinned line endings is a receipt that can fail
verification for reasons having nothing to do with the check it describes.

---

## Resolved — no open unknowns

All Technical Context fields are concrete. The three questions the plan flagged for research (run-id
derivation, the `Section I` collision, instance-to-area mapping) are answered in R2, R3 and R4
respectively. Phase 1 may proceed.
