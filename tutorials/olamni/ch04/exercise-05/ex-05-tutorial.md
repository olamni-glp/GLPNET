# Exercise 5 — Stream Operators

Welcome to chapter 4, exercise 5. This exercise covers four stream-operator Programs from book §4.2 — `distribute/3` (broadcast), `distribute_indexed/3` (tagged routing), `observer/3` (non-consuming), and `adder/4` (ripple-carry on bit streams).

## Q5 amendment notice

Per Clarifications Q5 spec amendment (recorded during /speckit-implement), this exercise's `distribute_indexed/3` has a 2-character `?` addition in two head positions (one per clause) compared to book p 33–34's printed form. The book's printed clauses have an "untouched output" variable (Out2 in clause 1, Out1 in clause 2) appearing as a writer in BOTH head and body recursive call — strict SRSW violation. The fix per Formal 4.1 (head reader produces to caller's writer): change the head's bare-writer position to `Var?` (head reader); keep the body as bare writer (paired with head's reader, filled by recursion). Pedagogical content (tagged stream routing on `send(1,X)`/`send(2,X)`) is preserved unchanged. The full Q5 reasoning is in `specs/005-tutorial-ch04/spec.md`.

This is the third book-internal SRSW inconsistency surfaced by ch01–ch04 implementation (after ch02 Q3a `append_and_sum/4`→`/3` and ch03 Q4 `lookup/3` Key→Key?). A separate audit of the book's code blocks for similar issues is recommended.

## Before you start

You should have completed ex-04 (in the §4.2 group). Read book §4.2's "Stream Distribution" + "Stream Observers" + "Ripple-Carry Adder" subsections (book pp 33–34). Re-read **Formal 4.3** (Which Guards Enable Multiple Reader Occurrences, pp 35–36) — `ground(X?)` guards on distribute + observer permit replicating X across both output streams.

## What's in the file

`ch-04-ex-05-stream-operators.glp` — 25 clauses byte-exact from book pp 33–34, plus duplicated logic gates / half_adder / full_adder from ex-02 per FR-010 self-containment (because adder/4 calls full_adder which calls half_adder which calls gates):

- `distribute/3` (2 clauses) — broadcast: replicate input to both outputs
- `distribute_indexed/3` (3 clauses, Q5-amended) — tagged routing
- `observer/3` (2 clauses) — non-consuming spy
- `adder/4` (2 clauses) — ripple-carry on bit streams
- 14 logic gate clauses (and/or/not/xor) duplicated from ex-02
- `half_adder/4` + `full_adder/5` duplicated from ex-02

## The exercise

### Step 1 — Load the file

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
olamni/tutorial/ch04/exercise-05/ch-04-ex-05-stream-operators.glp
```

Expected: `✓ Loaded:`. Cross-check trace **Phase A**.

### Step 2 — Run the primary goal: ripple-carry adder

```
adder([1,0,1], [1,1,0], 0, R).
```

Expected: `R = [0, 0, 0, 1]`. Adds two 3-bit numbers (LSB-first) with initial carry 0; result is a 4-bit number with the top bit being the final carry-out. Cross-check trace **Phase B**.

### Step 3 — Run the three inspection goals

#### Inspection 1 — distribute (broadcast)

```
distribute([a,b,c], Out1, Out2).
```

Expected: `Out1 = Out2 = [a, b, c]`. Cross-check trace **Phase C**.

#### Inspection 2 — observer (non-consuming)

```
observer([1,2,3], Out1, Out2).
```

Expected: `Out1 = Out2 = [1, 2, 3]`. Same structural pattern as distribute but with a different pedagogical role. Cross-check trace **Phase D**.

#### Inspection 3 — distribute_indexed (tagged routing)

```
distribute_indexed([send(1,a), send(2,b), send(1,c)], Out1, Out2).
```

Expected: `Out1 = [a, c]` (the send(1,*) tags) and `Out2 = [b]` (the send(2,*) tag). Cross-check trace **Phase E**.

### Step 4 — Cross-check against the trace

Open `ex-05-repl-trace.md`. Match line-for-line modulo banner.

## What you've learned

By the end of this exercise you have seen:

1. **Broadcast distribute** — replicate one input stream across multiple output streams using `ground(X?)` guards to permit multi-reader replication.
2. **Tagged routing distribute_indexed** — dispatch on struct tags (`send(N,X)`) to route values to numbered output streams. The Q5 amendment demonstrates the spec-amendment-during-implement pattern (now the third instance after ch02 Q3a + ch03 Q4).
3. **Non-consuming observer** — same structural shape as distribute but pedagogically distinct: observer is for audit-tap patterns, distribute is for fan-out.
4. **Ripple-carry adder** — a stream-based version of full-adder composition. Bit streams threaded through full_adder calls with carry propagation. Demonstrates that compound-circuit techniques (ex-02) extend cleanly to stream input.
5. **Self-containment at 25 clauses** — ex-05's `.glp` is the largest file in ch04 so far, due to the inline duplication of all gates + half_adder + full_adder per FR-010. Each exercise dir is a standalone REPL load.

ex-06 (next, last in §4.2 group) covers buffered communication (sliding window buffer `bb`) + objects/monitors (`counter/1` + `accumulator/1` with multiple clients).
