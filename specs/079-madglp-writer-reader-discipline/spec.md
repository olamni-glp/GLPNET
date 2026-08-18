<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: madGLP writer-reader address-discipline closure (N/N+1 audit + residuals)

**Feature Branch**: `079-madglp-writer-reader-discipline`
**Created**: 2026-08-14
**Status**: Draft
**Input**: User description: "madGLP writer-reader address-discipline closure (N/N+1 audit + residuals) … remove the last convention-dependent fallback beside the authoritative heap cross-pointer mechanism. NOT a §1.14 language change — an implementation audit of heap addressing; the FCP cross-pointer architecture is authoritative and unchanged."

## Context

The madGLP heap allocates a variable as a writer/reader **pair**, and (per FCP) links the two ends
with a **bidirectional cross-pointer** — the writer cell records its reader's address and vice versa.
That cross-pointer is the **authoritative** way to get one end from the other. Historically the code
also relied on a weaker **N/N+1 allocation convention** ("the reader is always at `writerAddr + 1`"),
which was the root cause of the address-confusion defect class (Issues 1/2/5/6, all fixed). Those fixes
left three **residuals** where the convention, or a stale description of it, still lingers beside the
authoritative mechanism:

1. **`heap_fcp.dart` `pairedReaderAddr()` retains a `writerAddr + 1` fallback.** It tries the
   authoritative `readerForWriter()` cross-pointer first, but on `null` it *guesses* `writerAddr + 1`
   instead of failing loud. If the cross-pointer is ever absent, the guess silently reintroduces the
   exact convention-dependence the defect class came from.
2. **The `three_agent_pipeline_boot` false-positive residual** (globalise/send, per
   `docs/bug-send-globalise-localise.md`) is **unverified** — a known madGLP test hazard that may be a
   stale false positive rather than a live defect.
3. **`GlobalSendSpawn.readerAddr` is mis-described.** Its doc comment (`mad_helpers.dart:61-64`) says
   "Address of the reader to watch (the ? end)", but the field actually carries an **onBind writer
   key**. The name/description contradict the value — a latent trap for the next maintainer.

This feature is an **audit-first, behaviour-preserving** closure: when the cross-pointers are intact
(the normal, correct state) nothing observable changes; the point is to remove the last silent fallback
so a *broken* cross-pointer fails loudly instead of being masked, and to retire the stale descriptions
and the test hazard.

🔴 **Scope guard**: This is **not** a §1.14 language change — the GLP language, its guards, and the FCP
cross-pointer architecture are authoritative and unchanged. It touches the **core Dart file**
`heap_fcp.dart`; per the maGLP constraints the change is audit-first and behaviour-preserving, and the
diff to core will be surfaced explicitly before it lands.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The last convention-dependent fallback is removed; a broken cross-pointer fails loud (Priority: P1)

`pairedReaderAddr()` no longer guesses `writerAddr + 1`. When the authoritative cross-pointer resolves
the reader, behaviour is unchanged; when it does **not**, the code fails loudly (a diagnostic) rather
than silently returning a convention-derived address.

**Why this priority**: This is the structural fix — removing the fallback is what prevents recurrence of
the address-confusion defect class. A silent guess is exactly what let those bugs hide.

**Independent Test**: With cross-pointers intact, every existing madGLP test passes unchanged (the
authoritative path already returns before the fallback). A fault-injection that removes a cross-pointer
now produces a loud diagnostic at the call site instead of a `writerAddr + 1` guess.

**Acceptance Scenarios**:

1. **Given** a writer whose reader cross-pointer is present, **When** `pairedReaderAddr()` is called,
   **Then** it returns the authoritative reader address — identical to today.
2. **Given** a writer whose reader cross-pointer is absent, **When** `pairedReaderAddr()` is called,
   **Then** it raises a loud, diagnosable error rather than returning `writerAddr + 1`.

### User Story 2 - The three_agent_pipeline_boot false-positive is verified and retired (Priority: P2)

The `globalise/send` residual flagged in `docs/bug-send-globalise-localise.md` is investigated to a
verdict: either it is a genuine live defect (then it is filed with a repro), or it is a stale false
positive (then the test hazard is retired and the doc updated to say so).

**Why this priority**: A test hazard that is neither fixed nor cleared is a standing source of noise
and mis-triage. Closing it either way removes the ambiguity.

**Independent Test**: The `three_agent_pipeline_boot` scenario runs to a deterministic, documented
outcome; the multiagent suite shows no unexplained false positive attributable to it.

**Acceptance Scenarios**:

1. **Given** the `three_agent_pipeline_boot` scenario, **When** it is run under the audit, **Then** its
   outcome is deterministic and its status in `docs/bug-send-globalise-localise.md` reads
   "verified live defect (repro filed)" **or** "false positive, retired" — never "unverified".

### User Story 3 - readerAddr is renamed/re-described to match what it holds (Priority: P2)

`GlobalSendSpawn.readerAddr` and its doc comment are corrected so the name and description match the
value it actually carries (an onBind writer key), eliminating the contradiction.

**Why this priority**: A field whose name lies about its contents is a latent defect generator; this is
cheap to fix and removes a maintenance trap.

**Independent Test**: The field's name and doc comment describe an onBind writer key; a reader of
`mad_helpers.dart` cannot mistake it for "the reader to watch"; all references compile and pass.

**Acceptance Scenarios**:

1. **Given** `GlobalSendSpawn`, **When** a maintainer reads the field and its comment, **Then** both
   describe the onBind writer key it holds, with no residual "reader to watch" wording.

### Edge Cases

- A writer legitimately at the end of the heap where `writerAddr + 1` would be out of bounds — the
  removed fallback must not have been masking such a case; the audit confirms this.
- Anonymous / write-only variables whose reader is never materialised — `pairedReaderAddr()` callers
  must be audited to confirm none depended on the fallback for a reader that structurally does not
  exist.
- The doc header/body inconsistency (Issue-1 header "Open" vs body "Fixed") is corrected as a
  ride-along.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `pairedReaderAddr()` MUST resolve the reader **only** via the authoritative bidirectional
  cross-pointer; the `writerAddr + 1` fallback MUST be removed.
- **FR-002**: When the cross-pointer cannot resolve the reader, the code MUST fail with a loud,
  diagnosable error (naming the writer address) rather than returning a convention-derived address.
- **FR-003**: The change MUST be behaviour-preserving when cross-pointers are intact — the full
  multiagent + REPL suites MUST show no new failures versus the pre-change baseline.
- **FR-004**: Every call site of `pairedReaderAddr()` MUST be audited to confirm none relied on the
  `writerAddr + 1` fallback for correctness (the N/N+1 audit).
- **FR-005**: The `three_agent_pipeline_boot` residual MUST be driven to a documented verdict (live
  defect with repro, or false positive retired) in `docs/bug-send-globalise-localise.md`.
- **FR-006**: `GlobalSendSpawn.readerAddr` and its doc comment MUST be corrected to describe the
  onBind writer key the field holds; all references MUST be updated consistently.
- **FR-007**: The Issue-1 doc header/body status inconsistency ("Open" vs "Fixed") MUST be corrected.
- **FR-008** (process): Core file `heap_fcp.dart` is touched; the change MUST be audit-first and the
  core diff surfaced explicitly (behaviour-preserving with cross-pointers intact) before it lands.
- **FR-009** (scope, ESCALATE E5): The bundled scope (residuals 1–3 + doc fixes) MUST be confirmed
  after inspecting `heap_fcp.dart`; if the inspection reveals a residual is larger than an audit-close
  (e.g. removing the fallback surfaces real dependent callers), that residual MUST be split out and
  reported rather than force-fit into this closure.

### Key Entities

- **Writer/reader pair**: two heap cells for one logical variable; the writer holds a value, the reader
  (`?` end) observes it.
- **Bidirectional cross-pointer**: the authoritative link recording each end's address in the other
  (`readerForWriter()` is its accessor). Source of truth.
- **N/N+1 convention**: the legacy assumption "reader = writer + 1"; the residual being removed.
- **onBind writer key**: the value `GlobalSendSpawn.readerAddr` actually holds (mis-named today).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero `writerAddr + 1` fallbacks remain in the reader-resolution path; a broken
  cross-pointer produces a loud diagnostic (verified by fault-injection).
- **SC-002**: The multiagent + REPL suites pass at the pre-change baseline count (no new failures) —
  behaviour preserved with cross-pointers intact.
- **SC-003**: 100% of `pairedReaderAddr()` call sites audited and confirmed cross-pointer-safe.
- **SC-004**: The `three_agent_pipeline_boot` residual has a documented verdict; the madGLP
  false-positive test hazard is retired or the live defect is filed.
- **SC-005**: `GlobalSendSpawn.readerAddr` name + doc match its onBind-writer-key contents; the
  Issue-1 doc header/body inconsistency is resolved.

## Assumptions

- The FCP bidirectional cross-pointer is populated for every writer/reader pair created through the
  normal allocation path (the fallback is dead code in the correct state — this is what "behaviour-
  preserving" rests on, and FR-004's audit confirms it).
- Scope is the three named residuals + the two doc fixes; the runtime unifier and the language surface
  are out of scope (this is not §1.14).
- Testing is via the existing multiagent Dart suite + the REPL suite (baseline recorded before change),
  per the project Test Protocol; the C# side is unaffected.
- 077's shared-module dedup and 069/077 releases are unrelated to this heap-addressing audit; no
  cross-dependency.
