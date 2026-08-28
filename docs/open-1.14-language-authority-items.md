<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->
# Open §1.14 language-authority items — for Udi

**Opened**: 2026-08-22 · **Maintained by**: the glpnet marathon lanes

DISCIPLINE §1.14 reserves the GLP language definition — guards, system predicates, body kernels,
directives, type-system features, primitive types — to Udi's express approval. This file is the
single register of items that are **blocked on that authority**, so they stop being rediscovered
one at a time inside unrelated features. Nothing here is a proposal to change the language; each
entry states what was measured, what is blocked by it, and what the decision is.

---

## L1 — A byte-exact transcription of book §4.3.1 is rejected by the guard rules

**Found in**: feature `083-glptutorial-corpus-goldens`, FR-002 · **Blocks**: 083 plan/implement,
and FR-009's very existence · **Status**: OPEN

The tutorial corpus exercise `tutorials/olamni/ch04/exercise-07/ch-04-ex-07-recursive-numerics.glp`
states *"All clauses byte-exact from the PDF"* and transcribes book §4.3.1 (p 37), which includes:

```prolog
%% lesseq base — 0 ≤ X for any natural X (guarded by natural_number/1).
lesseq(0, X) :- natural_number(X?) | true.
%% lesseq recursive — s(X) ≤ s(Y) iff X ≤ Y.
lesseq(s(X), s(Y)) :- lesseq(X?, Y?).

%% natural_number base — 0 is a natural.
natural_number(0).
%% natural_number recursive — s(X) is natural if X is natural.
natural_number(s(X)) :- natural_number(X?).
```

`natural_number/1` is a **two-clause** procedure whose second clause has a body. Per
`docs/typed-glp-manual.md` §8, a **defined guard must be a single-unit-clause procedure** — one
clause, no guard, no body — which the partial evaluator unfolds at compile time. So
`natural_number(X?)` in guard position is not callable, and **the runtime's rejection of this file
is correct**. The corpus golden that records `✓Loaded` was captured from a stale build and is the
falsehood.

**Why this needs you and not us.** There is **no single-unit-clause formulation of "is a natural
number"** over Peano terms — naturalness is inherently recursive. So the corpus can either keep
the book text and record the rejection, or diverge from the book. Repairing the exercise means
either deleting the guard (changing the program's meaning relative to the book) or introducing new
guard semantics, which is a language change.

**The decision**: is the book's §4.3.1 `lesseq` intended to be valid typed GLP? If yes, the guard
rules need an approved extension (recursive defined guards) — a §1.14 change. If no, the corpus
records the rejection as the golden and the tutorial gains a teaching point.
**The lane's recommendation is "no" — record the rejection**; it needs no language change and
keeps the corpus faithful to the book. But the observation that a byte-exact book program is
rejected is worth your attention in its own right.

---

## L2 — Binding at a consume position may be a `self.glp`-only privilege

**Found in**: feature `076-typechecker-body-atom-moding` (shipped) · **Blocks**: end-to-end
binding delegation for user code · **Status**: OPEN · **Evidence**: codify note
`cn-20260814T094751-7c6f5f1c`, retrospective finding `fnd-1477ca629f`

076 licensed the **caller** end of a binding delegation: a body-atom writer at a declared consume
position, licensed by a head-flipped reader. The approved rule's justification cites `X? = X.`
under `procedure =(_?, _).` as the **callee** end that the checker already models via the
Definition 5.5 flip.

Probed: a **user-written** callee of exactly that shape is **rejected**:

```prolog
procedure assign(_?, _).
assign(X?, X).       %% -> "writer requires up(produce), got down(consume)" on the head
```

The same failure occurs over a concrete type. So `programs/self.glp`'s `=` clause is evidently
**exempt from type checking**, and the callee end is currently expressible only inside `self.glp`.

**Consequence**: a user-defined procedure can *accept* the licensed writer (076 US2/P2 proves
this) but can only *read* it, never *bind* it — end-to-end delegation still works only through
`=`. This was deliberately out of 076's approved scope (the amendment is body-atoms-only and does
not license the symmetric head combination), so it was reported and not fixed.

**The decision**: either license the symmetric head combination too, or state explicitly in the
manual that binding at a consume position is a `self.glp`-only privilege.

---

## L3 — Occurs-check violation: `UnifyFail` or `CompileError`?

**Found in**: feature `080-occurs-checked-substitution` · **Blocks**: merging
`origin/080-occurs-checked-substitution` (only 2 conflicting paths — the *conflict* is trivial,
the *ruling* is not) · **Status**: OPEN, carried since 2026-08-20

077 made a cyclic term raise a **catchable `CompileError`** (occurs-check-violation diagnostic) in
the consolidated `term_traversal.cs`. 080 lands the single **bind-time** occurs-check on that
module. The open question is what a bind-time occurs-check violation *is* semantically: a
**unification failure** (`UnifyFail`, i.e. the clause simply does not match and execution
continues under committed choice) or a **compile/runtime error** (`CompileError`).

This is not an implementation preference — it decides whether occurs-check violation is part of
GLP's three-valued unification (Success | Suspend | Fail) or outside it. That is a language
definition question.

---

## How to use this file

Add an entry when a feature stops on a §1.14 boundary; state the measurement, what it blocks, and
the decision — never a proposed language change. Record Udi's ruling inline and move the entry to
a **Resolved** section rather than deleting it, so the reasoning stays recoverable.
