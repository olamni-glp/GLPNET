<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# US5 sourcing notes — FCP / GLP semantics (feature 062, T026/T027 prep)

Sourced 2026-07-29 from primary sources (per R-5 / DISCIPLINE §1.13). **Not a §1.14
proposal** — a sourced-fact record. The proposals themselves await the operator's scope
ruling (see the T028 STOP-gate question) before drafting.

## Item 1 — abandon-operation

**Sources**: EShapiro2/FCP (branch `main`, commit `6dda4ee…`), authoritative tree
`Savannah/Logix/EMULATOR/` — `unify.c`, `macros.h`, `fcp.h`, `emulate.c`, `kernels.c`.
Local: `docs/typed-glp-manual.md` §9; `docs/glp-runtime-spec.txt`.

**Key finding — FCP has NO primitive named "abandon."** (Confirmed absent: scanned
`kernels.c`/`unify.c`/`macros.h`/`fcp.h`; the only relevant machine service is GC,
`kernels.c` case 9.) "Abandon" is a GLP-surface notion realized by lower FCP mechanics:

1. **Variable model** — a logical variable is a *writable* cell (`WrtTag`, `fcp.h`) with,
   where a reader is needed, a paired *read-only* cell (`RoTag`) cross-pointing to its
   writable twin (`emulate.c` allocates them adjacently).
2. **Directional unification** (`unify.c`): a *writable* var meeting any term is **bound**
   in place via `asgn` (bind+trail, `macros.h`); a *read-only* var that would take a value
   (meeting a nonvar or another RO var) does **not** bind — it `sus_tbl_add`s and returns
   `False`, i.e. **suspends** until the writable twin binds. ("writers bind, readers
   suspend.")
3. **Abandon = anonymous writer** (`typed-glp-manual.md` §9.1): an anonymous `_` writer is
   "a fresh writer with no paired reader, so that a value assigned to it is discarded." At
   the FCP level it is a fresh `WrtTag` cell at a head position, referenced nowhere in the
   body: head unification binds it via `asgn`, so the process (a) raises **no** suspension
   on that input (contrast the RO suspend path) and (b) leaves the captured structure
   unreferenced → GC-eligible.

**Net**: `abandon` = put an anonymous **writer** where a reader would sit; it
captures-and-drops the input rather than reading it. No dedicated FCP opcode — the ordinary
writable-`asgn` path plus non-reference. **This mechanism already exists in GLP** (anonymous
`_` writers; used e.g. in `programs/tests/typed/multi_client_control.glp` `[stop|_]`, which
type-checks and runs). → the T028 question to the operator: what does the §1.14
"abandon-operation" item add beyond the existing anonymous-writer semantics?

**Gap**: the GC *reclaim* routine (heap.c/freezer.c) was not line-quoted — reclaim is
inferred from (reference-drop + a counted GC), not quoted.

## Item 2 — nested-structure-head-matching

**Sources**: `docs/typed-glp-manual.md` §2A (compositional mode/flip rule) + §4;
`docs/glp-runtime-spec.txt` "Head Processing", "Push/Pop nested-structure state", "Soft-Fail".

- **Mode/matching (manual §2A)**: each head variable's writer/reader form is fixed by a
  compositional flip rule — start from the arg's declared mode (`Type?`→↓, `Type`→↑), each
  `?` in the type path flips; writer at ↓ (captures), reader at ↑ (hole). "Composes
  recursively at any depth within a structure in the head" (§2A.4).
- **Runtime (runtime-spec)**: HEAD instructions are **pure/tentative** (build σ̂w, no heap
  mutation). Nested structures traversed via a saved-state stack of `(S, mode,
  currentStructure)`; `push`/`pop` save/restore into `clauseVars[...]`; `unify_structure`
  enters a nested `StructTerm` (READ) or builds a `_TentativeStruct` (WRITE); a
  functor/arity mismatch **soft-fails** (discards σ̂w) to the next clause. So
  `_TentativeStruct`/`_ClauseVar` are the WRITE-mode skeleton + tentative-var cells — to be
  **extended, not removed** (IV-b).

→ Likely already implemented in the Dart runner; VERIFY behaviour (a nested-structure head
match) directly before drafting the proposal, per the T028 note.
