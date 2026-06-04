# Exercise 01 — LP / GLP append contrast

**Source**: *The Art of Grassroots Logic Programming* (Shapiro, 2025). Classical LP append: Chapter 2, §2.1, p 10 (Example 2.1). GLP append: Chapter 4, §4.2 ("List Reversal — Naive Reverse"), pp 31–32. The cross-chapter import is the only one in this tutorial; chapter 2 is mostly theoretical, so we pull forward the smallest GLP exemplar from chapter 4 to make §2.2's LP→GLP transition observable.

**Files in this folder**:
- `ch-02-ex-01-classical-append-LP-only.glp` — classical LP `append/3` byte-exact from p 10. **Intentionally rejected by the SRSW analyser**; the rejection is the demonstration.
- `ch-02-ex-01-glp-append.glp` — GLP `append/3` byte-exact from pp 31–32. Accepted by the SRSW analyser; runs the primary demo goal and the three inspection goals.
- `ex-01-tutorial.md` — this file (step-by-step guide).
- `ex-01-repl-trace.md` — known-good capture of the REPL session this tutorial walks through.

## Before you start

Read §2.1 (Logic Programs — transition systems, syntax, MGU, operational semantics, pp 9–12) and §2.2 (Linear Logic, pp 12–14) in the book, paying special attention to **Formal 2.1: Linear Equality Assertions** on p 14. The "No contraction" row says a variable cannot be duplicated; this exercise makes that abstract rule observable at the REPL.

## Building the REPL

You only need to do this once per checkout (or after a Dart SDK upgrade):

```bash
dart compile exe glp_runtime/bin/glp_repl.dart -o glp_runtime/glp_repl.exe
```

This produces a single `.exe` (Windows) or unsuffixed binary (Linux/macOS) at `glp_runtime/glp_repl.exe`. The Dart SDK requirement is `^3.9.4`. If the build was already done for chapter 1 you can reuse the binary.

## The exercise

Open the REPL:

```bash
./glp_runtime/glp_repl.exe
```

You should see a banner ending with `Loaded root self.glp`.

### Step 1 — Watch the SRSW analyser reject the classical LP file

Type the path of the LP-only file:

```
GLP> olamni/tutorial/ch02/exercise-01/ch-02-ex-01-classical-append-LP-only.glp
```

You should see `Error loading …: SRSW violations found:` followed by a list of violations naming every variable in `append/3` that breaks the SRSW rule. The analyser stops the load — no `✓ Loaded` line appears.

This is the chapter's pedagogical core. Each violation says "Writer variable X occurs 2 times" or "Variable X has no reader". That is exactly Formal 2.1's "No contraction" (each variable used exactly once) and "Paired reader/writer required" (each writer must have one reader) made concrete on a real file. The classical LP version of `append/3` violates both rules and the runtime catches it at load time, before any goal can run.

The error is not a defect of this tutorial — it is what we wanted to see. Now we load the GLP version of the same predicate and watch it pass.

### Step 2 — Load the GLP file

Type the path of the GLP file:

```
GLP> olamni/tutorial/ch02/exercise-01/ch-02-ex-01-glp-append.glp
```

You should see `✓ Loaded: …`. The same `append/3`, but with `?` reader annotations (`Ys?`, `X?`, `Xs?`, `Zs?`) that mark each occurrence as either a writer or a reader. SRSW analysis, partial evaluation, type checking, and compilation all pass.

Compare the two files side by side. The classical LP version writes `[X|Zs]` (no `?`); the GLP version writes `[X?|Zs?]` (both readers). The recursive call in classical LP is `append(Xs, Ys, Zs)` (no readers); in GLP it is `append(Xs?, Ys?, Zs)` (Xs and Ys are readers, Zs is a writer). The structural shape is identical; the annotations are the difference.

### Step 3 — Run the primary demo goal

```
GLP> append([1,2,3], [a,b,c], Zs).
```

You should see `Zs = [1, 2, 3, a, b, c]` followed by `→ succeeds`. The first list's elements precede the second list's elements in the output. The recursive clause walks `[1,2,3]` down to `[]`; the base clause then forwards `[a,b,c]` through the writer/reader pair `Ys`/`Ys?`.

This binding is locked in the spec. If your REPL produces something different, either your `.glp` is corrupted or the runtime is misbehaving — file an issue rather than silently move on.

### Step 4 — Inspection goal 1: empty first list

```
GLP> append([], [a,b,c], Zs).
```

You should get `Zs = [a, b, c]`. The base clause matches on the first call; no recursion happens. This is the simplest case — the second list is forwarded verbatim through the writer/reader pair.

### Step 5 — Inspection goal 2: empty second list

```
GLP> append([1,2,3], [], Zs).
```

You should get `Zs = [1, 2, 3]`. The recursive clause walks `[1,2,3]` to `[]`; the base clause then forwards an empty `Ys` (which is `[]`) into the output. The result is the first list, unchanged.

### Step 6 — Inspection goal 3: both lists empty (base case alone)

```
GLP> append([], [], Zs).
```

You should get `Zs = []`. Only the base clause matches both lists' shapes. This is the protocol's termination condition — without it, recursion would never bottom out.

### Closing

```
:quit
```

The REPL says `Goodbye!` and exits.

## Cross-check against the captured trace

After you've run all six steps, open `ex-01-repl-trace.md` and compare your terminal output line by line against the captured trace there. The Phase A SRSW-violation list should match byte-for-byte; the Phase B–F bindings should match exactly (modulo the build/timestamp banner). If something differs, write down what — divergence is interesting and likely points at either a build issue on your machine or a real change in the runtime since this trace was captured.

## What you've learned

By reading §2.1 + §2.2 + Formal 2.1 and running these six steps you've seen:

1. **The SRSW analyser is real and catches contraction at load time.** The classical LP `append/3` is not a hypothetical bad example — the runtime actually rejects it with a precise list of violations, and you watched it happen.
2. **The `?` reader annotations are the LP→GLP transition concretely.** Same predicate, same recursion, opposite outcomes. The annotations are not stylistic — they are how each variable's writer and reader role is declared.
3. **GLP append composes via writer/reader pairing.** The recursive clause hands `Xs?` (reader) to the recursive call, which writes the result into `Zs` (writer); the caller's `Zs?` reader then sees the result. This is point-to-point communication via paired writer/reader, which is what Formal 2.1's "Linear implication A ⊸ B" row formalises.
4. **The base case matters.** `append([], Ys, Ys?)` is what makes the recursion terminate. Without it, the recursive clause would run forever.

The same `append/3` reappears in chapter 4 inside larger programs (`reverse/2`, `merge/3`'s helpers). Recognising it across those contexts — and recognising the writer/reader pairing pattern — is the start of reading GLP fluently.

## Next: variants and amplifications

After exercise-01 is approved, exercise-02 introduces GLP arithmetic via the `:=` operator (a procedure that appends two number lists and concurrently sums the result). Exercise-03 adds the system clock (`now/1`) and ground-term output (`'_output'/1`) on top, demonstrating that the same SRSW discipline that governs lists and numbers also governs side-effecting kernels. Each exercise is gated behind explicit approval of its predecessor.
