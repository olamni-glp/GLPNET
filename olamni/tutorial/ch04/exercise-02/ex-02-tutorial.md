# Exercise 2 — Compound Circuits

Welcome to chapter 4, exercise 2. This exercise builds on ex-01's logic gates (`and/3`, `or/3`, `not/2`, `xor/3`) by composing them into compound circuits via clauses-with-bodies — `nand/3`, `half_adder/4`, and `full_adder/5`. The §4.1 group is now complete after this exercise; the §4.2 group (streams) opens once the project owner approves both ex-01 and ex-02 as a group.

## Before you start

You should have completed ex-01 first (the §4.1 group's status block confirms ex-01 is approved before ex-02 unlocks). Read book §4.1's "Clauses with Bodies" + "Guards for Multiple Reader Occurrences" + "Compound Circuits" subsections (book pp 29–30). Also read **Formal 4.1** (Produces and Consumes Parameters, p 29) and **Formal 4.3** (Which Guards Enable Multiple Reader Occurrences, pp 35–36) — both are referenced in this file's header.

## What's new in ex-02

Compared to ex-01:

- ex-01 had only **unit clauses** (no body). ex-02 introduces **clauses with bodies** — the body of a clause is a sequence of goals that must succeed for the clause to apply.
- ex-01 had only constants in head positions. ex-02 has **writers + readers** in the head, and uses **`ground` guards** on multi-reader clauses (the half_adder).
- ex-01 had no composition. ex-02 composes the gates from ex-01 into circuits via body goals.

The Formal 4.1 box on book p 29 names the parameter-mode convention: a head **reader** is a "produces" parameter (the body's paired writer fills it); a head **writer** is a "consumes" parameter (the body's paired reader observes it). Look at `nand(A,B,Z?)` — the `Z?` reader in the head is a produces parameter; A and B (writers) are consumes parameters. The body `:- and(A?,B?,W), not(W?,Z).` reads A? and B? (consuming the head's bound A and B), produces a fresh writer W via `and/3`'s output, and finally produces Z via `not/2`'s output.

## What's in this file

`ch-04-ex-02-compound-circuits.glp` contains:

- **Logic gates duplicated inline from ex-01** (14 unit clauses): `and/3` + `or/3` + `not/2` + `xor/3`. Per FR-010 self-containment, ex-02 does NOT load ex-01's `.glp` as a dependency; the gates are duplicated byte-exact.
- **`nand/3`** (1 clause with body) — first GLP demonstration of a clause-with-body composing two prior procedures.
- **`half_adder/4`** (1 clause with body) — uses `ground(A?), ground(B?) |` guards because A? and B? each appear twice in the body. Per Formal 4.3 + book p 30 prose: ground guards permit multi-reader-occurrence clauses by ensuring inputs are fully bound before the body executes.
- **`full_adder/5`** (1 clause with body) — composes two `half_adder/4` calls + one `or/3` call. Each reader appears only once in the body so NO ground guards are needed.

## The exercise

### Step 1 — Open the REPL

If your REPL session from ex-01 is still open, you can reuse it (but you'd need to clear the procedure table — easier to just restart). Otherwise:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-02 file

```
olamni/tutorial/ch04/exercise-02/ch-04-ex-02-compound-circuits.glp
```

You should see `✓ Loaded:`. All 17 clauses are now in the REPL's procedure table. Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal: full-adder with carry-out

```
full_adder(1, 1, 0, S, C).
```

Expected: `S = 0` and `C = 1`. The full_adder adds 1+1+0 = binary 10 (decimal 2). Sum-bit is 0; carry-out is 1. Cross-check: **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — NAND

```
nand(1, 1, X).
```

Expected: `X = 0`. NAND inverts AND. AND(1,1) = 1; NOT(1) = 0. Cross-check: **Phase C**.

#### Inspection 2 — half-adder

```
half_adder(1, 0, S, C).
```

Expected: `S = 1`, `C = 0`. XOR(1,0) = 1; AND(1,0) = 0. The `ground(A?), ground(B?)` guards check that A=1 and B=0 are fully bound before the body executes. Cross-check: **Phase D**.

#### Inspection 3 — full-adder all ones

```
full_adder(1, 1, 1, S, C).
```

Expected: `S = 1`, `C = 1`. Adds 1+1+1 = binary 11 (decimal 3). Cross-check: **Phase E**.

### Step 5 — Cross-check against the trace

Open `ex-02-repl-trace.md`. Match each phase line-for-line modulo banner / wallclock.

## What you've learned

By the end of this exercise you have seen:

1. **Clauses with bodies** — the simplest such clause is `nand(A,B,Z?) :- and(A?,B?,W), not(W?,Z).` Two body goals, sequential. The clause's reader/writer modes are governed by Formal 4.1: the head's writers and readers determine the parameter modes, and the body's writers/readers complete the SRSW pairs.
2. **`ground` guards on multi-reader clauses** — per Formal 4.3, when a reader variable appears multiple times in the body, the clause needs a guard that ensures the variable is fully bound before the body executes. `ground/1` is the canonical such guard. The half_adder's `ground(A?), ground(B?)` guards demonstrate this.
3. **Compound-circuit composition** — full_adder composes two half_adder calls + one or-gate. The wiring is purely declarative: the output writers of one sub-call become the input readers of the next.
4. **No-guards-needed when readers appear once** — full_adder's body has `S1, C1, S2, C2, A?, B?, Cin?, Sum, Cout` — each reader appears only once. No ground guards are needed.

The §4.1 group is now complete. ex-03 (the §4.2 group entry-point) introduces streams: `producer/2`, `consumer/3`, naive `reverse/2`, and accumulator `reverse/2`. ex-03 also reclaims `producer/2` + `consumer/3` as their NATIVE chapter-4 home (after they appeared in ch03 ex-01 as a cross-chapter forward import).
