<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# §1.14 proposal — nested-structure-head-matching

**State:** `sourced → drafted` ← (awaiting operator-approved at T028 STOP-gate)
**Item slug:** wave-4 / US5 / §1.14 item 2 — `nested-structure-head-matching`

---

## 1. Item

`nested-structure-head-matching` — the compositional HEAD-phase matching and construction of
structures nested to **arbitrary depth** (struct-in-struct, struct-in-list), in both READ
(decompose an incoming term) and WRITE (build a tentative term) mode.

## 2. Motivation

Correct nested-structure head matching is load-bearing across the typed corpus (bond agent,
signalling server, social-graph agents). The gap this item closes is **not a missing
capability** — it is that the arbitrary-depth compositional rule is not captured as a named,
pinned language feature with a dedicated regression. This proposal documents the exact
semantics and pins them.

## 3. Exact semantics

**Mode determination (typed-glp-manual §2A — the compositional flip rule).** Each head
variable's form (writer `X` / reader `X?`) is fixed by: start from the argument's declared
mode (`Type?` → ↓ consume, `Type` → ↑ produce); each `?` in the type path **flips** the mode
(↓↔↑, two flips cancel); at a ↓ position use a **writer** (captures the incoming value), at a
↑ position use a **reader** (a hole to be filled). *"Composes recursively at any depth within
a structure in the head"* (§2A.4).

**Runtime (docs/glp-runtime-spec.txt — Head Processing).** HEAD instructions are
**pure/tentative**: they build σ̂w with **no heap mutation**. Nested structures are traversed
via a saved-state stack of `(S, mode, currentStructure)`; `push`/`pop` save/restore into
`clauseVars[...]`. `UnifyStructure` at a nested position either enters a nested `StructTerm`
(READ mode — decompose the incoming term) or builds a nested `_TentativeStruct` (WRITE mode —
skeleton for the produced term). A functor/arity **mismatch soft-fails**: σ̂w is discarded
and matching proceeds to the next clause.

**Three-phase / SRSW.** HEAD phase only; GUARD/BODY unaffected. Nesting depth does not change
SRSW — each variable still occurs at most once as reader and once as writer regardless of the
depth at which it appears.

## 4. Authoritative source

- `docs/typed-glp-manual.md` §2A (compositional mode/flip rule, worked signalling-server
  example) and §4 (Channel convention).
- `docs/glp-runtime-spec.txt` — "Head Processing", "Push/Pop nested-structure state",
  "Soft-Fail". Full sourcing in `proposals/_fcp-sourcing-notes.md` (T005).

## 5. Type-system impact

**None.** The type checker already applies the §2A flip rule at arbitrary depth. Verified:
`struct_demo.glp` and `depth_test.glp` (nesting to triple depth) type-check clean.

## 6. Runtime impact

**No structural change.** Nested-structure HEAD matching is already implemented in
`glp_runtime/lib/bytecode/runner.dart` via an arbitrary-depth parent-context stack
(declared ~line 191; WRITE builds nested `_TentativeStruct` ~1000–1006; READ enters nested
`StructTerm` ~960–980; functor/arity mismatch soft-fails). `_TentativeStruct` / `_ClauseVar`
are the WRITE-mode skeleton + tentative-var cells — to be **extended, never removed**
(Constitution IV-b); this proposal removes and restructures nothing. Consistent with the
operator's Item-1 ruling (2026-07-30): integration adaptation only, no structural change.

**Verification (2026-07-30, this session):**
| Goal | Result | Exercises |
|---|---|---|
| `make_person(alice, thirty, seattle, P)` | `P = person(alice, age(thirty), city(seattle))` | WRITE nested build |
| `build_person(P), get_age(P?, A)` | `A = thirty` | READ match through doubly-nested `age(Age)` |
| `build_person(P), get_city(P?, C)` | `C = seattle` | READ nested extract |
| `tree3(x, T)` | `T = node(node(leaf(x), leaf(a)), leaf(b))` | **triple**-nested WRITE |
| `bin_nest(q, R)` | `R = outer(inner(q, b), c)` | nested WRITE |

## 7. Test plan

- **Positive (REPL, Section A):** a nested READ extract + a nested WRITE build reduce to the
  documented bound results (focused cases over `struct_demo.glp` / `depth_test.glp`).
- **Negative (REPL, Section C):** a functor/arity mismatch at a nested head position
  **soft-fails** to the next clause (or fails when no clause matches) — pinning the soft-fail
  contract.
- **Dart unit (`glp_runtime/test/`):** assert a nested WRITE builds the correct
  `_TentativeStruct` skeleton and that a nested-position mismatch discards σ̂w.

## 8. Approval reference

Operator approval recorded 2026-07-29 (clarify session; commit `8d70218b`). Gate ② approved
2026-07-30.

---

## STOP-gate note (T028) — RESOLVED for this item

**Finding:** `nested-structure-head-matching` is **already fully implemented** in the Dart
runner (arbitrary depth, READ + WRITE), verified above.

**Operator ruling (2026-07-30):** *"YES — so we must codeconv the code / reimplement the code
in C# and/or Gleam."* Accepted.

**US5 deliverable for this item (T030 scope):** the Dart reference is **unchanged**
(no structural change); the §1.14 work is to **reimplement/port the nested-structure
HEAD-matching semantics into the wider runtime family** — the C# `out/csharp/` engine (via
codeconv Dart→C#) and/or the Gleam port — verified for **parity against the Dart reference**
(same READ/WRITE nested results, same soft-fail contract). This is the concrete form of the
Item-1 "integration adaptation with wider enriched capabilities."

Fork RESOLVED by operator (Gabi) 2026-07-30: **both** targets — C# `out/csharp/` **and**
Gleam. Any request to *restructure* the Dart nested-matching internals remains out of scope
→ STOP and re-confirm (Bug/Language protocol).
