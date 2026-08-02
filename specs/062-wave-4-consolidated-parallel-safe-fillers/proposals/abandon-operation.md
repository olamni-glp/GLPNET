<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# §1.14 proposal — abandon-operation

**State:** `sourced → drafted` ← (awaiting operator-approved at T028 STOP-gate)
**Item slug:** wave-4 / US5 / §1.14 item 1 — `abandon-operation`

---

## 1. Item

`abandon-operation` — a named, documented GLP operation by which a process **abandons an
input it no longer wants**: it captures the input at a head position without reading it,
raising no suspension, leaving the captured term unreferenced (GC-eligible).

## 2. Motivation

The language already lets a process drop an input via an **anonymous writer** (`_`, or a
named `_Foo`) at a head position where a reader would otherwise sit (typed-glp-manual §9.1).
This is used in practice — e.g. `client(Id, [stop|_], [bye(Id?)])` in
`programs/tests/typed/multi_client_control.glp` (feature 062 US4), and the `_` discards in
`programs/tests/typed/struct_demo.glp`. The gap this item closes is **not a missing
mechanism** — it is that the operation is undocumented as a first-class, named language
feature and unpinned by a dedicated regression. This proposal documents the exact semantics
and pins them.

## 3. Exact semantics

An **anonymous writer** at a head argument (or nested) position where the caller supplies a
term:

- **HEAD phase (tentative).** The position is a *writer* with no paired reader in the clause.
  Head unification **binds** it (writer-`asgn`: it captures the incoming term into σ̂w). It
  raises **no suspension** — contrast a *reader* `X?` at the same slot, which suspends until
  its paired writer binds. This is the "writers bind, readers suspend" rule.
- **GUARD / BODY phase.** The captured value is referenced **nowhere** (no paired reader
  exists), so after the clause commits it is unreachable → GC-eligible. No GUARD or BODY
  effect.
- **SRSW.** Anonymous writers are the sanctioned SRSW exception: a writer *may* appear with
  no paired reader precisely so a value assigned to it is discarded (manual §9.1). Only
  anonymous **writers** are permitted in clause positions; `_?` (anonymous reader) is not.

Net: `abandon` = place an anonymous **writer** where a reader would sit; it
captures-and-drops the input rather than reading it. There is **no dedicated opcode** — it is
the ordinary writable-`asgn` head-bind plus non-reference.

## 4. Authoritative source

- **FCP** (EShapiro2/FCP, `Savannah/Logix/EMULATOR/`): `fcp.h` (`WrtTag`/`RoTag` cells),
  `unify.c` (writable var meeting a term → `asgn` bind+trail; read-only var that would take a
  value → `sus_tbl_add` + return `False`, i.e. suspend), `macros.h` (`asgn`), `emulate.c`
  (writer/reader cells allocated adjacently). Confirmed: FCP has **no** primitive named
  "abandon"; the only machine service in that neighbourhood is GC (`kernels.c` case 9). Full
  sourcing in `proposals/_fcp-sourcing-notes.md` (T005). DISCIPLINE §1.13.
- **Local:** `docs/typed-glp-manual.md` §9.1 (anonymous writer = fresh writer, no paired
  reader, value discarded).

## 5. Type-system impact

**None.** The SRSW checker already accepts anonymous writers via the `_`-prefix exemption
(manual §9). Verified: `multi_client_control.glp` (`[stop|_]`) and `struct_demo.glp` (`_`
positions) type-check clean under the current checker.

## 6. Runtime impact

**No structural change** (operator ruling, T028, 2026-07-30: *"ONLY adding integration
adaptation with wider enriched capabilities, but no structural change per se"*). The
writable-`asgn` head-bind path plus non-reference already realizes abandon in
`glp_runtime/lib/bytecode/runner.dart`; the core mechanism, opcodes, and cell model are
**untouched**. Any adaptation is **additive integration glue** so the existing abandon
semantics compose with the wider feature set — never a change to `_ClauseVar` /
`_TentativeStruct` / fallback branches (those are **not removed and not restructured**;
Constitution IV-b trivially satisfied).

**Verification (2026-07-30, this session):**
- `client(c1, [stop|_], [bye(c1)])` reduces inside `control_demo(X)` → `X` bound to a closed
  6-element reply list, `→ succeeds` (REPL suite A31, 538/538). The `_` abandons the input
  stream tail; no suspension.
- `get_age(person(_, age(Age), _), Age?)` returns `Age = thirty` — the two `_` writers
  abandon Name and City.

## 7. Test plan

- **Positive (REPL, Section A):** a clause abandoning a stream tail runs to a bound result
  (extend the A-section with a focused abandon case beyond A31's end-to-end coverage).
- **Negative (REPL, Section C):** a **reader** `X?` in the same slot with no writer anywhere
  **suspends** (the contrast that proves the writer-vs-reader distinction), and `_?`
  (anonymous reader) is rejected.
- **Dart unit (`glp_runtime/test/`):** assert an anonymous-writer head position produces
  **no** entry in the suspension set `si`/`U` after HEAD, and that a reader-in-slot does.

## 8. Approval reference

Operator approval recorded 2026-07-29 (clarify session; `specs/062-.../` clarify commit
`8d70218b`). Gate ② (§1.14 language-authority) approved by the operator 2026-07-30.

---

## STOP-gate note (T028) — RESOLVED for this item

**Finding:** `abandon-operation` is **not a new primitive** — it is existing GLP
(anonymous-writer discard), verified working (§6).

**Operator ruling (2026-07-30):** the §1.14 item *"ONLY adds integration adaptation with
wider enriched capabilities, but no structural change per se."* Accepted.

**US5 deliverable for this item (T029 scope):**
1. Document the exact semantics as the named `abandon-operation` (this proposal).
2. Pin with positive + negative regression tests (§7) — REPL + Dart unit.
3. **Integration adaptation to the wider runtime family** — the concrete form (per the
   companion Item-2 ruling, 2026-07-30) is to ensure the abandon (anonymous-writer discard)
   semantics are faithfully present in the C# `out/csharp/` engine (via codeconv Dart→C#)
   **and** the Gleam port (fork resolved 2026-07-30: both targets), verified for **parity
   against the Dart reference** (writer binds + no suspension + drop). **No structural change
   to the Dart core.**

Any request to change the *structure* of the Dart mechanism (new opcode, altered cell model,
restructured `_TentativeStruct`/`_ClauseVar`) is **out of scope** per this ruling → STOP and
re-confirm before touching core code (Bug/Language protocol).
