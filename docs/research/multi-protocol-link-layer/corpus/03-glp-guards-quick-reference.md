---
title: "GLP Guards Quick Reference"
authors: "glpnet project (GLP runtime team); formal basis attributed to U. Shapiro et al. (main_GLP_to_Dart formal spec)"
year: "2026"
source_url: "file:///D:/bstdev/research/glp/glpnet/docs/guards-reference.md"
retrieved: 2026-06-06
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: GLP Guards Quick Reference"
precedence_class: glp-current
access: full-text
---

# GLP Guards Quick Reference — Extraction

## Provenance & precedence note

This is the **authoritative local current-implementation spec** (`docs/guards-reference.md`,
"Last Updated: 2026-03-06") for the track-G guard set the multi-protocol link layer needs.
Per the research thread's SOURCE PRECEDENCE rule, local `docs/` GLP specs are the **highest
authority** (precedence_class `glp-current`) and are NOT overridden by Shapiro's papers or by
earlier concurrent-logic (FCP/CP/Logix) papers. The doc itself cites
`main_GLP_to_Dart (1).tex` ("Formal specification") and `glp-bytecode-v216-complete.md`
(guard instruction specs) as its formal/normative backing. No external fetch was required or
performed — the designated authoritative source is local and was read in full.

This file is a faithful extraction. For any guard implementation question, the source file at
`D:/bstdev/research/glp/glpnet/docs/guards-reference.md` is canonical.

---

## 1. Core model (load-bearing for B2 distributed unification)

**Three-valued guard semantics (verbatim):**

> "Guards are pure tests with **three-valued semantics** (success/suspend/fail) that appear
> before the `|` separator in GLP clauses. Guards are **patient**—they suspend on unbound
> variables rather than fail."

- **Success**: "Guard condition definitively true → continue to next guard or body"
- **Suspend**: "Unbound variables present, success possible → add to suspension set Si"
- **Fail**: "Guard condition definitively false → try next clause"

> "**Key Property**: Guards never have side effects and execute during HEAD/GUARDS phase
> (before commit)."

Syntax: `Head :- Guard1, Guard2, ... | Body.`

**Why guard arguments are readers (`X?`), not writers (load-bearing for the link layer):**

| Argument Type | If Unbound | Behavior |
|---|---|---|
| Reader (`X?`) | Suspend | Wait for paired writer to provide value |
| Writer (`X`) | Fail | No paired reader to wait for |

> "Using a reader enables patient synchronization: the clause waits until data arrives before
> testing it. Using a writer would cause immediate failure on unbound variables, defeating the
> purpose of concurrent synchronization."

**Implication for the distributed link layer:** when the shared logic variable is split across
two REPL instances, the reader side must preserve *suspend-on-unbound* semantics. A guard
testing a value that lives on the remote (writer) side must SUSPEND, not fail, until the
remote binding propagates. This is the fidelity constraint B2 must satisfy.

---

## 2. WxW rule (Writer-to-Writer matching FAILS) — load-bearing

Verbatim:

> "GLP term matching fails on writer-to-writer to ensure no readers are abandoned:
> - If writers X and Y were to match, their readers X? and Y? would have no writer to provide values
> - Runtime must FAIL immediately on writer-to-writer term matching attempts
> - This is NOT a suspension case - it's a definitive failure"

**Link-layer relevance:** a distributed unification scheme must never let two *writer* endpoints
on different instances be matched/merged — that would orphan both sides' readers. The link
primitive must designate exactly one writer-node and one reader-node per link (consistent with
the design framing's "which side of each link: writer-node vs reader-node").

---

## 3. Guard negation `~G` — load-bearing for track-G

Verbatim semantics:

> "`~G` succeeds iff G fails. Suspension behavior follows from the standard guard definition
> (a guard suspends if there exists an assignment to its readers that makes it succeed)."

Restrictions (verbatim):

> "- Only atomic built-in guards can be negated
> - Defined guards (unit clauses) cannot be negated
> - Compound guards cannot be negated (no `~(A, B)`)
> - Double negation `~~G` is syntactically forbidden (formally equivalent to G, but forbidden in syntax)"

**Negatable guards:** `ground/1`, `known/1`, `unknown/1`, `integer/1`, `number/1`, `string/1`,
`constant/1`, `compound/1`, `list/1`, `module/1`, `is_mutual_ref/1`, `no_readers/1`, and the
ground-equality test `X =?= Y` (i.e. `~(X =?= Y)`).

**Non-negatable guards** (and the doc's stated reasons):
- `<`, `>`, `=<`, `>=` and `=:=`, `=\=` — "Type error on non-numeric operands"
- `otherwise` — "Special clause-ordering semantics"
- `wait`, `wait_until` — "Time-based control flow"

**Design rationale (verbatim):**

> "In GLP, guards have **input-only variables** - they test but don't bind. This makes success
> and failure symmetric definitive outcomes. Neither produces bindings, both are final
> decisions. This symmetry enables clean negation semantics where `~G` simply inverts the
> success/fail outcome while preserving suspension behavior."

---

## 4. The full guard set (track-G inventory)

### Instantiation / type guards

| Guard | Success | Suspend | Fail | Implies ground? (SRSW relax) |
|---|---|---|---|---|
| `known(X?)` | X bound to constant OR compound (`constant(X) ∨ compound(X)`) | X unbound reader | X unbound writer | **No** |
| `unknown(X?)` | X is unbound (reader or writer) | (does **not** suspend) | X bound to any value | No |
| `constant(X?)` | number or string | unbound reader | bound compound | **Yes** |
| `compound(X?)` | `f(T₁..Tₙ)`, n>0 (lists are compound) | unbound reader | bound constant | **No** |
| `ground(X?)` | no unbound vars anywhere | contains unbound readers | contains unbound writers | **Yes** |
| `integer(X?)` | integer | unbound reader | non-integer (incl. floats) | **Yes** |
| `number(X?)` | int or double | unbound reader | non-number | **Yes** |
| `string(X?)` | string constant (not `[]`/nil) | unbound reader | number/compound/empty list | **Yes** |
| `list(X?)` | `[]` or `[H\|T]` (top-level cons; not necessarily proper) | unbound reader | non-list | **No** |
| `module(X?)` | bound to `ModuleTerm` | unbound reader | any other value | **Yes** |
| `is_mutual_ref(X?)` | bound to `MutualRefTerm` | unbound reader | any other value | No |
| `no_readers(X?)` | term has NO readers (ground and/or writers only) | term contains any readers | **never fails** | No |

Notes (verbatim, load-bearing):

- `known`: "**Logical Definition**: `known(X)` ≡ `constant(X) ∨ compound(X)`." And: "`known(f(Y))`
  succeeds even if Y is unbound, because the structure f(Y) itself is a compound term.
  `ground(f(Y))` would suspend waiting for Y to be bound."
- `unknown`: "**Logical Definition**: `unknown(X)` ≡ `~known(X)`." And: "Unlike most guards,
  `unknown(X?)` does NOT suspend — it either succeeds (unbound) or fails (bound)."
- `ground` circular terms: "`ground(X?)` succeeds if the term contains no unbound variables on
  any branch. The cycle itself does not make a term non-ground."
- `no_readers` (verbatim): "This guard **never fails**—it either succeeds (no readers) or
  suspends (has readers)." Distinct from `ground`: "`no_readers(X?)` succeeds if X contains no
  readers but may contain writers." Examples: `no_readers(f(Y))` with Y a writer → **succeeds**;
  `no_readers(f(Y?))` with Y? an unbound reader → **suspends**; `ground(f(Y))` with Y a writer
  → **fails**. (Use case: validating terms before external/UI output.)

### Equality guard

**`X =?= Y` (ground equality test)** — three-valued truth table (verbatim):

| X | Y | Result |
|---|---|---|
| ground | ground, X = Y | succeed |
| ground | ground, X ≠ Y | fail |
| unbound reader | any | suspend |
| any | unbound reader | suspend |
| unbound writer | any | fail |
| any | unbound writer | fail |

> "Why not multiple head writers: GLP maintains the SO invariant via SRSW syntactic restriction
> (one writer per variable). Instead of implicit equality via multiple head occurrences, use
> `=?=` for explicit, visible equality testing."

### Arithmetic comparison guards

**`X < Y`, `X =< Y`, `X > Y`, `X >= Y`** (note: Prolog/GLP uses `=<`, not `<=`):
- Success: both bound to numbers AND condition holds
- Suspend: either operand is an unbound reader
- Fail: both bound to numbers AND condition false

**`X =:= Y` (arithmetic equality):** Success = both bound and numerically equal; Suspend = either
operand unbound reader; Fail = both bound and not equal.

**`X =\= Y` (arithmetic inequality):** verbatim — "The arithmetic inequality guard `=\=` is
**redundant** once guard negation (`~`) is implemented. It becomes equivalent to `~(X =:= Y)`.
Use `~(X? =:= Y?)` for arithmetic inequality."

> Note (when comparisons succeed, both operands are ground): "Arithmetic comparison guards
> suspend if operands are unbound and only succeed if both operands are bound to numbers.
> Therefore, when they succeed, both operands are guaranteed to be ground."

### Control / clause-ordering guard

**`otherwise`:** Success = "All previous clauses for this procedure definitively failed";
Fail = "At least one previous clause suspended (may still succeed)." Verbatim Q&A: "`otherwise`
only succeeds when all previous attempts **definitively failed**, not when they're waiting for
data."

### Time guards

- **`wait(Duration)` (ms):** Duration ≤ 0 → succeed immediately; > 0 → suspend, start timer,
  resume on fire; non-number → fail; unbound reader → suspend. "Non-Negatable": control-flow,
  not a pure test.
- **`wait_until(Timestamp)`:** Success = `now ≥ Timestamp`; Suspend = `now < Timestamp` (timer
  for remaining); non-number → fail; unbound reader → suspend. Mechanism (both): allocate a
  reader/writer pair, start a Dart timer, suspend on the reader; on fire, bind the writer →
  reactivate via the ROQ.

---

## 5. SRSW relaxation rule (load-bearing — the "ground guard" exception)

Verbatim rule:

> "Per the formal definition, variables occur as reader/writer pairs with exactly one of each.
> The ONLY exception: when guards guarantee groundness, multiple occurrences of both the writer
> and reader are permitted because ground terms contain no unbound writers."

> "When a guard ensures a variable is ground (contains no unbound variables), both the writer
> and its paired reader may appear **multiple times** in the clause without violating SRSW."

**Why it works (verbatim):** "Ground terms contain no unbound writers. Multiple occurrences of a
ground variable's writer and reader do not create single-writer violations because there's no
exposed writer that could be bound multiple times."

**Guard reader counting (distinct from the relaxation):** "Guards take reader arguments. These
reader occurrences count toward satisfying the SRSW syntactic restriction that each writer has a
paired reader." E.g. `check(X) :- known(X?) | true.` is valid (writer X in head, reader X? in
guard = one writer + one reader).

**Which guards imply groundness / allow multiple occurrences:**

- **Yes (ground-certified):** `ground/1`, `constant/1`, `integer/1`, `number/1`, `string/1`,
  `module/1`, and ALL arithmetic comparisons `< =< > >= =:= =\=` (both operands, when they succeed).
- **No:** `compound/1`, `known/1`, `no_readers/1`, `otherwise`, `unknown/1`, `is_mutual_ref/1`,
  `list/1`.

**Critical difference (verbatim):** "`known(X)` tests if X is bound to either a constant or
compound term ... but does not require groundness. A compound term like `f(Y)` is known but not
ground if Y is unbound." Hence `bad_example(X,Y1,Y2) :- known(X?) | send(X?,Y1), send(X?,Y2).`
is an SRSW **violation** (X? could be `f(Z)` with Z unbound).

**Compiler/SRSW-analyzer requirements (verbatim list):**
1. "Track guards in HEAD/GUARDS phase"
2. "Recognize guards that imply groundness: Type guards `ground/1`, `integer/1`, `number/1`,
   `string/1`, `constant/1`; Arithmetic comparisons `<`, `=<`, `>`, `>=`, `=:=`, `=\=`"
3. "Mark variable as 'ground-certified' for this clause; Allow multiple occurrences of both
   writer and reader"
4. "For variables without such guards: Enforce strict single-occurrence constraint"

---

## 6. What may appear in guard position

Verbatim validation rule:

> "Any procedure called in guard position that is NOT a builtin guard MUST be a
> single-unit-clause procedure. If a user-defined procedure has multiple clauses, or has guards
> or body goals, calling it in guard position is a compile-time error."

Two categories the partial evaluator accepts:
1. **Builtin guards** — implemented in the Dart runtime with NO GLP clauses (type guards,
   comparison guards, `=?=`); kept as-is by the PE.
2. **Single-unit-clause procedures** — user-defined, exactly one clause, no guards, no body;
   unfolded at compile time by the PE. (Have NO special status; require a `procedure`
   declaration + SRSW compliance.) Example from prelude:
   `new_channel(ch(Xs?, Ys), ch(Ys?, Xs)).` — callable in guard (PE-unfolded) or body
   (runtime) position. Pure pattern guards use anonymous vars: `channel(ch(_, _)).`

---

## 7. Guards vs system predicates (`execute`) — critical distinction

| Aspect | Guards | System Predicates (via `execute`) |
|---|---|---|
| Semantics | Three-valued (success/suspend/fail) | Two-valued (success/abort) |
| Unbound input | Suspend (patient) | Abort (impatient) |
| Syntax | `Head :- Guard \| Body` | `execute('name', [Args])` |
| Phase | HEAD/GUARDS (before commit) | BODY (after commit) |
| Side effects | Never | May have (I/O, mutations) |

Safe pattern (verbatim intent): always gate an `execute` with a guard that ensures
preconditions — e.g. `safe_double(X,Y) :- number(X?) | execute('evaluate', [X? * 2, Y]).`

---

## 8. References cited by the source doc

- `SPEC_GUIDE.md` — guards vs execute overview
- `glp-bytecode-v216-complete.md` — complete guard instruction specifications (NORMATIVE)
- `parser-spec.md` — parser implementation for guard expressions
- `main_GLP_to_Dart (1).tex` — formal specification

---

## 9. Synthesis for the link-layer thread (B2 fidelity yardstick)

The distributed link primitives must preserve, end-to-end across instances:
1. **Three-valued reads** — a reader on instance A testing a value whose writer is on instance B
   must SUSPEND (not fail) until B's binding arrives; reactivation must be driven by binding
   propagation (the remote analogue of the ROQ writer-bind → reactivate path used by `wait`).
2. **WxW failure** — no link may match two writer endpoints; each link is exactly one
   writer-node ↔ one reader-node (matches the design's per-instance role designation).
3. **Ground-certified SRSW relaxation** — if a value is ground, it may be read multiply; the
   link layer can safely broadcast/replicate ground values across N instances (the
   `ground(X?)` / `constant` / numeric-comparison family), but must NOT relax SRSW for
   `known`/`compound`/`list`/`no_readers` results (may still contain unbound writers/readers).
4. **`no_readers`** is the natural gate for sending a term across a transport: a term safe for
   external output contains no readers (the doc's stated use case is exactly "external output,
   e.g. to a UI/Dart"). This is directly reusable as the serialization-safety guard for the
   link layer.
5. **Guard negation + `=?=`** give the testable equality/disequality core; the track-G arithmetic
   and ordering guards are all present and three-valued, so the standalone `comparison-guards`
   feature is fully subsumed by this reference.
