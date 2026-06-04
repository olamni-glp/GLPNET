# Exercise 3 — §3.2 guard negation via lookup/3

Welcome to chapter 3, exercise 3. This exercise completes the §3.2 guard-curriculum by introducing the third species: **guard negation** via the `~(...)` form on a negatable built-in guard. ex-01 used built-in guards (`>`, `ground`); ex-02 introduced defined guards (`channel/1`); ex-03 introduces the `~(...)` negation form on the equality-test guard `=?=`.

## Before you start

You should have completed exercise-02 first (the chapter signpost confirms ex-02 approval before unblocking ex-03). Read book §3.2 again, paying particular attention to the "Guard Negation" subsection (book p 22) and the "SRSW Rules for Defined Guards" table on book p 24. The table is the authoritative reference for which guards are negatable: `=?=` and type guards are negatable; arithmetic comparisons (`<`, `>`, `=:=`, etc.) and `otherwise` are not; defined guards (like ex-02's `channel/1`) are NOT negatable either.

## What's new in ex-03

Compared to ex-02:

- ex-02 demonstrated `channel/1` as a defined guard at `process/2` clause 1's guard site, with `otherwise` as the fallback.
- ex-03 demonstrates `~(=?=)` at `lookup/3` clause 2's guard site, with the SAME operator `=?=` used positively at clause 1's guard site. The two clauses together show the same operator in both forms — the cleanest possible negation pedagogy.

The book's `lookup/3` is the canonical §3.2 negation example. Clause 1 returns the value when the search Key matches the head's K (positive `=?=`); clause 2 recurses on the tail when Key does NOT match (`~(=?=)`).

## Q4 amendment notice

The `.glp` file in this exercise differs from book p 22's byte-exact form by **a single character** — clause 2's body recursive call uses `Key?` (with `?`, the reader) rather than `Key` (without `?`, the writer per PDF). The amendment is justified by book Formal 4.2 (p 31) "SRSW in Continuation Calls — the recursive call passes readers, not writers" and was forced by an empirical halt at first-implementation: the byte-exact form failed under strict SRSW because `Key` would be a SECOND writer occurrence of the same variable. The amendment preserves the pedagogical content (positive vs negated `=?=` on the same operator) and the locked primary goal binding `V = 2` unchanged.

This is the kind of book-internal-consistency amendment that the spec's plan-then-act discipline + Constitution Principle II ("No Workarounds") + the ch02 Q3a precedent enable: an empirical halt, a precisely-reasoned spec amendment via Clarifications Q4, then implementation re-verifies. The full reasoning is in `specs/004-tutorial-ch03/spec.md` Clarifications Q4.

## The exercise

Stand-alone: load just `ch-03-ex-03-guard-negation.glp`; no Program 3.1 or ex-02 procedures needed.

### Step 1 — Open the REPL

```bash
./glp_runtime/glp_repl.exe
```

Or via the kernel snapshot:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-03 file

```
olamni/tutorial/ch03/exercise-03/ch-03-ex-03-guard-negation.glp
```

You should see `✓ Loaded:` followed by the path. `lookup/3`'s two clauses are now in the REPL's procedure table. Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal

```
lookup(b, [(a,1),(b,2),(c,3)], V).
```

Expected: `V = 2` and `→ succeeds`. The recursion descends in two steps:

1. First call `lookup(b, [(a,1),(b,2),(c,3)], V)`. Clause 1's guard `b =?= a` evaluates to fail (b ≠ a). Clause 2's guard `~(b =?= a)` succeeds (the negation of fail is success). Recursive call: `lookup(b?, [(b,2),(c,3)]?, V)` — note `Key?` in the body per the Q4 amendment.
2. Second call `lookup(b, [(b,2),(c,3)], V)`. Clause 1's guard `b =?= b` succeeds. Head pattern matches `(b,2)`. V binds to 2 via the writer/reader pair `V/V?`. Body `true` succeeds.

Cross-check: **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — first element matches (clause 1 only)

```
lookup(a, [(a,1),(b,2),(c,3)], V).
```

Expected: `V = 1`. Clause 1 fires immediately on the first element (`a =?= a` succeeds). Clause 2 (the negated branch) is NOT exercised — this isolates positive `=?=` behaviour. Cross-check: **Phase C**.

#### Inspection 2 — last element matches (deepest descent)

```
lookup(c, [(a,1),(b,2),(c,3)], V).
```

Expected: `V = 3`. Recursion descends in three steps via clause 2 (negated guard succeeds for both `c≠a` and `c≠b`); fourth step matches via clause 1 (`c=?=c`). The longest descent in the trace — the negated branch's recursion is the engine of search. Cross-check: **Phase D**.

#### Inspection 3 — key not found (deterministic failure)

```
lookup(z, [(a,1),(b,2),(c,3)], V).
```

Expected: `V = <unbound>` and `→ failed`. Recursion descends three times via clause 2 (`z` doesn't match any of `a`, `b`, `c`), eventually arriving at `lookup(z, [], V)`. Both clause heads require a non-empty list pattern; neither matches `[]`; the procedure fails. The input was fully ground so the empty residue is also ground — no suspension is possible.

**Important**: `→ failed` is the expected outcome for this specific goal, not a bug. If your runtime produces `→ suspended` instead, that would be a Principle II halt-and-report situation per the spec edge case for ground-input no-match cases. The deterministic-fails contract is documented in spec FR-014 + the Edge Case "Goal that suspends rather than succeeds" (M1 patch). Cross-check: **Phase E**.

### Step 5 — Cross-check against the trace

Open `ex-03-repl-trace.md` in this directory. Match each phase line-for-line modulo banner / wallclock.

## What you've learned

By the end of this exercise (and the chapter) you have seen:

1. **Guard negation** in action. The `~(...)` form turns a positive guard into its negation: it succeeds when the inner guard fails, fails when the inner guard succeeds, and suspends when the inner guard suspends. ex-03's clause 1 uses `=?=` positively; clause 2 uses `~(=?=)` on the same operator.
2. **Negation is restricted to negatable built-in guards**. Per book §3.2 + the SRSW Rules table on p 24: `=?=` and type guards are negatable; arithmetic comparisons (`<`, `>`, `=:=`, etc.) and `otherwise` are not; **defined guards (like ex-02's `channel/1`) are NOT negatable either**. `~(channel(X?))` would be ill-formed in GLP. The chapter-3 curriculum deliberately puts negation last and applies it to a built-in (`=?=`) precisely to demonstrate this restriction.
3. **Deterministic termination on ground no-match**. When the input is fully ground and the recursion runs past the end, GLP's procedure-failure semantics fire (no clause matches → `→ failed`), not suspension. Suspension only happens when an unbound reader could later become bound.
4. **The §3.2 guard curriculum complete**. ex-01 (built-in: `>`, `ground`) → ex-02 (defined: `channel/1` via `process/2`) → ex-03 (negation: `~(=?=)` via `lookup/3`). All three species are now concrete.
5. **Spec-locked decisions can need amendment at empirical-implement time**. The Q4 amendment is the chapter-3 instance of the workflow's spec-amendment-during-implement pattern (predecessor: ch02's Q3a). When a runtime-load failure surfaces a strict-SRSW issue with the byte-exact PDF form, the amendment path is documented (Clarifications entry preserving audit trail) rather than silent substitution. The amendment is principled — book-internal consistency (Formal 4.2) justifies the change — not arbitrary.

You have completed Chapter 3 of the tutorial. The next chapter (ch4 "Basic Concurrent Programming") covers the producer/consumer / merge-tree / distributor / objects-monitor families that the §4.2 import in ex-01 hinted at; chapters 5+ introduce types and modes and the rest of the tutorial set's content.
