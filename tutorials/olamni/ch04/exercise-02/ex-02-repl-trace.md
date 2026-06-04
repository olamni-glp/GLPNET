# Exercise 2 — REPL trace

This trace is the verbatim output of an actual GLP REPL session run on this Windows host on 2026-04-30. It demonstrates the §4.1 compound-circuit Programs (`nand/3`, `half_adder/4`, `full_adder/5`) loaded into a single file alongside the duplicated logic gates from ex-01. The four goals exercise NAND + a half-adder + two full-adder configurations to cover the full-adder's truth-table corners.

## Phase A — Load ex-02 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-02/ch-04-ex-02-compound-circuits.glp
```

The 17 clauses (4 + 4 + 2 + 4 logic gates duplicated from ex-01 + 1 nand + 1 half_adder + 1 full_adder) are now in the REPL's procedure table. The duplicated gates are needed because half_adder/full_adder/nand call them in their bodies; per FR-010 self-containment, this file does NOT load ex-01's `.glp` separately.

## Phase B — Primary demo goal: full-adder with carry-out

```glp
GLP> S = 0
C = 1
→ succeeds
```

Goal: `full_adder(1, 1, 0, S, C).` — adds three bits (1+1+0 = binary 10). Sum-bit S = 0; carry-out C = 1. The full_adder body composes `half_adder(A?,B?,S1,C1)` + `half_adder(S1?,Cin?,Sum,C2)` + `or(C1?,C2?,Cout)`. Each reader appears only once in the body so no `ground` guards are needed (per Formal 4.3).

## Phase C — Inspection goal 1: NAND

```glp
GLP> X = 0
→ succeeds
```

Goal: `nand(1, 1, X).` — NAND inverts AND. `and(1,1,W)` binds `W = 1`; `not(W?,Z)` reads `W?` and binds `Z = 0`; the head's writer-pair X? consumes `Z` and produces `X = 0`. Per Formal 4.1 (Produces and Consumes Parameters): Z? in the head is a "produces" parameter; A, B in the head are "consumes" parameters.

## Phase D — Inspection goal 2: half-adder

```glp
GLP> S = 1
C = 0
→ succeeds
```

Goal: `half_adder(1, 0, S, C).` — adds two bits (1+0 = 1, no carry). Sum = XOR(1,0) = 1; Carry = AND(1,0) = 0. The `ground(A?), ground(B?) |` guards on the half_adder clause check that A and B are fully bound before the body executes — necessary because A? and B? each appear twice in the body (two reader occurrences require ground guards per Formal 4.3).

## Phase E — Inspection goal 3: full-adder all ones

```glp
GLP> S = 1
C = 1
→ succeeds
```

Goal: `full_adder(1, 1, 1, S, C).` — adds three bits (1+1+1 = binary 11). Sum-bit S = 1; carry-out C = 1. Maximum-output corner: both half-adders produce Sum=0+1=0+0; carries are 1+1; OR(1,1)=1. (Following the body's chain: half_adder(1,1,S1,C1) gives S1=0, C1=1; half_adder(0,1,Sum,C2) gives Sum=1, C2=0; or(1,0,Cout) gives Cout=1.)

---

The four goals exercise nand/3 (Phase C), half_adder/4 (Phase D), and full_adder/5 (Phases B + E). Across the 4-goal session, all the ex-02-introduced clauses fire at least once: nand's clause-with-body in Phase C; half_adder's `ground`-guarded clause in Phase D + indirectly through full_adder in Phases B + E; full_adder's compound-circuit clause in Phases B + E. The §4.1 progression from unit clauses (ex-01) to clauses-with-bodies + multi-reader-ground-guards + compound circuits (ex-02) is now complete. ex-03 (the §4.2 group entry-point, gated behind §4.1 group approval) introduces streams.
