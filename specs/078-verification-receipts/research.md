<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 0 Research: Verification receipts and loud failure

All NEEDS CLARIFICATION items are resolved below. The six spec-level clarifications and
three plan-level forks were **engineer-ratified 2026-08-18** (see spec.md Clarifications and
plan.md Summary); this file records the remaining design decisions and the alternatives each beat.

## D1 — Receipt representation: language-neutral JSON schema (RATIFIED)

- **Decision**: The contract is a **versioned JSON schema** defining the sidecar file. A
  **Python reference emitter/validator** serves buildkit, `codeconv` and roadmap-sync directly;
  the bash test-harness and markdown coop-protocol get **thin emitters** that write the same JSON.
  Consumers **validate against the schema**, not against a library binding.
- **Rationale**: The 6 areas are heterogeneous (Python, bash, Dart, markdown). Only a
  language-neutral wire format serves all of them from one contract, and it lets the conformance
  fixture validate *any* emitter's output structurally.
- **Alternatives rejected**: a Python-library-as-contract (forces bash/markdown areas onto a
  Python shell-out dependency, narrows portability); a bespoke text format (no free validator,
  fails FR-004's machine-readability without hand-written parsers).

## D2 — Addressing: sidecar file + verdict pointer (RATIFIED, FR-022)

- **Decision**: The receipt is written to a **conventional, documented path derived from
  area + run**, and the verdict carries a **pointer** to it. Inline emission inside a structured
  verdict is a permitted *additive* per-area upgrade, never a replacement.
- **Path scheme (design)**: `<receipts-root>/<area>/<run-id>/<check-id>.receipt.json`, where
  `<receipts-root>` is a documented per-repo location and `<run-id>` is the invoking run's id
  (marathon run id, CI run, or a synthesized timestamp id when standalone). A run's receipts are
  thus co-located and enumerable — which the per-run expected-set (FR-023) reconciles against.
- **Rationale**: "no receipt" becomes a determinate file-absence condition (FR-008), not a
  judgement; works for checks that emit only human text.
- **Alternatives rejected**: inline-in-payload only (forces every check onto a structured
  channel); catalog rows (depends on the fleet's worst silent-failure component — self-defeating).

## D3 — Ownership & version binding: buildkit owns, glpnet binds by version (RATIFIED, FR-024)

- **Decision**: The schema + conformance fixture are authored **once in buildkit**; glpnet
  **pins a version** and never copies the runtime artifact. `codeconv/src/codeconv/receipts/bind.py`
  resolves the pinned schema from the installed buildkit distribution.
- **Version-pin mechanism (research)**: buildkit already versions its distributed artifacts and
  installs under versioned dirs via `bk-deploy` (`<home>/versions/<version>/`). The receipt schema
  ships inside that distribution; `bind.py` reads the schema from the *active installed buildkit
  version* and records that version string into every receipt (`contract_version`), so a
  version skew between emitter and consumer is itself visible and reconcilable.
- **Open coordination dependency (surfaced, not silently assumed)**: the buildkit companion
  change (schema + fixture + 3rtask/codexreview adoptions) is delivered by the buildkit/gavriella
  lane. glpnet MVP can proceed against a **draft/pinned** schema version; full SC-002 across all 6
  areas closes only once the buildkit side lands. This is tracked as a COOP-coordinated cross-repo
  edge, consistent with the marathon's parked scheduler/cross-host item.
- **Alternatives rejected**: define-in-glpnet-then-port (the copy-divergence FR-024 exists to stop).

## D4 — Outcome classification & the non-collapse rule (FR-006/007, US2)

- **Decision**: Exactly one of `PASS | EMPTY | UNREAD | UNSEARCHABLE | FAIL`. Only PASS and
  EMPTY are successful. UNREAD/UNSEARCHABLE MUST NOT be reported as, aggregated into, or rendered
  like success. Aggregates propagate the *worst* constituent (FR-009): any child UNREAD/UNSEARCHABLE
  forbids a clean parent.
- **Distinguishing rule (the load-bearing part)**: EMPTY requires proof the target was resolved
  **and examined in full** (examined-count == true-total, both recorded — FR-010); UNREAD is
  target-resolved-but-examined < total; UNSEARCHABLE is target-not-resolvable-at-all.
- **Rationale**: instances 4, 5, 7, 8, 10 are all EMPTY/UNREAD/UNSEARCHABLE collapses; the
  reconcilable counts (D5) are what make the EMPTY-vs-UNREAD boundary machine-checkable rather than
  self-asserted.

## D5 — Bounded-but-honest receipts (RATIFIED, FR-005)

- **Decision**: Enumerations of examined/skipped items are capped at a **declared maximum**;
  the **true totals are always recorded**; a **byte backstop** caps any single field; a receipt
  that truncated **says so and by how much**.
- **Rationale**: FR-010 reconciliation needs the true totals; a flat byte cap would discard them
  (one requirement defeating another). Bounded ≠ dishonest.
- **Alternatives rejected**: plain whole-receipt byte cap (breaks reconciliation on large targets).

## D6 — Override: reuse guardian informed-consent shape (RATIFIED, FR-012)

- **Decision**: Overriding a refusal reuses the **established informed-consent mechanism**
  (briefing + explicit acknowledgement + rationale + **scope** + **mandatory expiry**); no
  indefinite override; never applies beyond recorded scope; remains visible in the receipt.
- **Research**: the hardening charter (E13) already fixes the informed-consent override to
  `bk-guardian`'s shape; `override.py` binds to that surface rather than introducing a second one.
- **Rationale**: a second override mechanism would itself be a place for a silent pass to hide.
- **Alternatives rejected**: per-invocation-only (so painful engineers route around it);
  a new bespoke mechanism (duplication + new hiding place).

## D7 — Absence-is-an-error, applied twice, one rule (FR-020 + FR-023)

- **Decision**: Two "silent absence" holes share **one** rule. An **area** absent from the
  per-repo adoption manifest is an error (FR-020). A **run** with no declared expected-check set is
  an error (FR-023). Both refuse rather than default to a pass.
- **Rationale**: makes "a check that silently stopped existing" as loud as "an area that silently
  never adopted"; SC-002's denominator is pinned to FR-017's enumeration (FR-021), so an empty
  declaration set fails FR-020 first rather than trivially satisfying SC-002.
- **Alternatives rejected**: derive-expected-from-last-run (a ratchet that only loosens).

## D8 — Ship-gate: MVP mechanism on a reference check (RATIFIED)

- **Decision**: First SHIP-TOKEN ships US1+US2+US3 proven against a **purpose-built reference
  check**; US4 retrofits the 6 real areas incrementally.
- **Rationale**: the 5 downstream features need the *mechanism*, not the retrofits ("their
  acceptance suites are only trustworthy once a check cannot pass without running"). SC-001 (13/13)
  and SC-002 (100% of areas) close over the US4 increments, reported via the adoption manifest.
- **Alternatives rejected**: all-6-areas-in-one-increment (slowest to unblock downstream, stuck-PR
  risk); named-subset-first (larger first increment for no downstream benefit over mechanism-only).

## US4 site inventory (for later increments — grounded, not yet designed)

Confirmed on disk so the retrofit increments have real anchors:

- **codeconv build gate** — `codeconv/src/codeconv/tools/codegen/buildgate.py`: a clean compile
  ⇒ `pass`; a compile with **no test summary** still passes on compile alone (instance 6 / CD-03).
  Retrofit: a zero-tests-ran build is **EMPTY-qualified or UNREAD**, never a silent behavioural PASS.
- **test-harness** — `test/run_all_tests.sh` (`set -e`, Sections A–Q): skip-guards that render an
  unsupported-platform link as passed-by-skip (instances 5 / RT-24/28/29/16). Retrofit: skipped
  items counted with reason; verdict qualified, never a clean pass on their behalf.
- **roadmap-sync** — `reconcile` issuing an unqualified in-sync verdict without consulting `link`'s
  honest "no spec dirs matched" (instance 13 / RS-11, RS-35/36). Retrofit: aggregate cannot be clean
  while a constituent step reported non-success (FR-009).
- **coop-protocol** — poll/cursor defects silently skipping unread mail (instance 8). Retrofit: an
  unread mailbox is UNREAD, never EMPTY.
- **buildkit-3rtask / buildkit-codexreview** — companion buildkit change (findings-block omission =
  instance 2; mandatory-reading false-zero = instance 1). Delivered by the buildkit lane, pinned here.
