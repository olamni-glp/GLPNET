<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# EXECUTED REVERSION — `goal-term-acceptance-dart-csharp`

**Feature 109 · US1 · T058 · FR-007 · SC-002 · FR-023**
**Host:** OLAMNIT · **Date:** 2026-09-06 · **Branch:** `109-differential-acceptance-gate`

---

## Why this file exists

FR-007: *"The harness MUST be proven a real detector by **executing** a reversion of a known fix
and confirming the criterion reports MEASURED-DIVERGE. An unfalsifiable 100% scores zero."*

The criterion carries an **in-band** negative control that runs on every invocation — it perturbs
the C# transcript by deleting the refusal line and requires the comparator to diverge. That proves
the **comparator** discriminates. It does **not** prove that the runtimes, the capture, the
normalisation and the comparison work as a chain, because it never asks the runtime to behave
differently.

So the real fix was reverted **in the real source**, rebuilt, and measured. What follows is what
was run and what came back — not a description of what would happen.

---

## What was reverted

`out/csharp/lib/engine/glp_engine.cs`, **both** improper-tail sites (`_BuildListTerm` and
`_BuildListTermForConj`). The shipped code refuses:

```csharp
throw new GoalTermError(
    "list tail is neither a list nor a variable: " +
    $"{GoalTermDescribe.Describe(tail)} — the goal was not run. " +
    "A list must end in [] or a variable.");
```

replaced with the **actual defect measured on 2026-09-04** — silent coercion of an improper tail
to nil, so `[send(1,a)|foo]` returns exactly what `[send(1,a)|[]]` returns:

```csharp
tailTerm = new RtConstTerm(null);   // REVERTED FOR 109 T058 -- the 2026-09-04 defect
```

Then: `dotnet build out/csharp/glp_repl/glp_repl.csproj -c Debug` → `0 Error(s)`.

---

## Step 1 — with the defect present

`python scripts/differential_gate.py` → **exit 1**

```
declared 1 | AGREE 0 | DIVERGE 1 | NOT-MEASURED 0

[MEASURED-DIVERGE] goal-term-acceptance-dart-csharp
    normalisation control strip-prompt: ok
    normalisation control keep-result-lines: ok
    negative control: ok -- perturbing 'csharp' produced MEASURED-DIVERGE as required
    reason: normalised transcripts differ
    divergence:
      --- dart
      +++ csharp
      @@ -5,8 +5,8 @@
       → succeeds
       Z = some(send(2, b))
       → succeeds
      -→ failed
      -Error: list tail is neither a list nor a variable: foo — the goal was not run. A list must end in [] or a variable.
      +Y = some(send(1, a))
      +→ succeeds
       → failed
       Error: anonymous reader `_?` is not a valid term in a list element — ...
       Y = some(send(1, a))
```

The divergence is not a generic mismatch: it shows C# **answering** `Y = some(send(1, a))` and
`→ succeeds` where Dart refuses. That is the wrong answer itself, printed. A per-runtime pattern
assertion would have passed on both sides here — C# printed a plausible binding, and nothing in
its own transcript looks wrong. Only the comparison against the other runtime shows it.

---

## Step 2 — with the fix restored

The file was restored byte-for-byte from a pre-edit copy (`md5 51480e53d4b0c91b841f631413e4f78f`,
verified equal, `git diff` empty) and rebuilt.

`python scripts/differential_gate.py` → **exit 0**

```
declared 1 | AGREE 1 | DIVERGE 0 | NOT-MEASURED 0

[MEASURED-AGREE] goal-term-acceptance-dart-csharp
    normalisation control strip-prompt: ok
    normalisation control keep-result-lines: ok
    negative control: ok -- perturbing 'csharp' produced MEASURED-DIVERGE as required
```

---

## Result

| | defect present | fix restored |
|---|---|---|
| outcome | `MEASURED-DIVERGE` | `MEASURED-AGREE` |
| exit code | 1 | 0 |

The harness distinguishes the failing case from the passing case **end to end** — runtime, capture,
normalisation and comparison. SC-002 is discharged by execution, not by assertion.

**FR-008 still applies to the right-hand column.** MEASURED-AGREE is a statement that the two
participants agreed. Two runtimes broken identically would also agree, and this reversion does not
and cannot show otherwise.

---

## A defect found by running this, and fixed here

The first attempt at Step 1 did not measure anything: the gate reported

```
participant 'csharp' was not started: binary is NOT NEWER than its source
```

**immediately after a successful build.** Investigated rather than worked around:

`glp_repl.exe` is the .NET **apphost stub**. An incremental build does **not** rewrite it when only
a referenced library's method bodies change. Measured on this host:

```
1788728564  out/csharp/glp_repl/bin/Debug/net11.0/glp_repl.exe          <- 22:02, the PREVIOUS build
1788728564  out/csharp/glp_repl/bin/Debug/net11.0/glp_repl.dll          <- 22:02, also not rewritten
1788729435  out/csharp/glp_repl/bin/Debug/net11.0/glp_runtime_net.dll   <- 22:17, THIS build
1788729415  out/csharp/lib/engine/glp_engine.cs                         <- 22:16, the edit
```

`glp_repl.dll` is not rewritten either: the edit changed a method body, so the reference assembly
was unchanged and MSBuild correctly skipped the dependent project.

**`test/run_all_tests.sh` has the same defect.** Its freshness gate (added under 078 T047) stats
`$GLPREPL_EXE` alone, so after *any* C# source edit and rebuild it declares the binary stale and
marks Sections I, T, U and V-18..V-23 `unsearchable` — the four sections the brief warns are
silently suppressed by staleness are suppressed by the *check for* staleness instead. It errs
safe (it never calls a stale binary fresh), which is why it survived: the failure is invisible
unless someone edits C# and expects those sections to run.

Fixed in both places by dating the build from the **newest file in the output directory** rather
than from a launcher stub the build does not touch. Every file in that directory is build output —
nothing there is written at run time — so its newest mtime is an honest build timestamp. Pinned by
suite check `V-26`, which fails if the gate ever goes back to statting a single artefact that a
successful incremental build leaves untouched.
