<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart — adopting the evidence-signal invariant in your lane

**The invariant.** A signal a caller treats as evidence must not be observable in a state that
reports completion before the work it reports has completed — and must not report completion for
work that does not survive the next restart.

This is the **complement** of feature 078, not an extension of it. 078 governs signals that state a
verdict. This governs signals that state none but are read as evidence anyway. Do not re-open 078.

---

## 1. Find your surfaces (5 minutes)

    python scripts/evidence_signal_audit.py

The first run has an empty manifest, so every scan hit lands in `scan_only` and the audit exits **3**.
That is correct: the tool is telling you it found evidence-bearing signals nobody has declared.

Ask of each hit, in this order:

1. **Does any caller read it as grounds to proceed?** If no, it is not in scope (FR-002). If yes,
   it is, regardless of what its docstring says.
2. **Which class is it?** A wait or idle predicate → FR-004. An exit status or emptiness → FR-007.
   Anything whose completion must survive a restart → FR-012. A surface can be in more than one.

## 2. Declare them

Add an entry per surface to `.specify/evidence-signals/manifest.json`. Start honest:

```json
{
  "id": "hook-notifier-wait-for-idle",
  "path": "csharp/ynet_transport/HookNotifier.cs",
  "symbol": "WaitForIdle",
  "kind": "wait",
  "consumers": ["csharp/ynet_transport.tests", "csharp/ynet_client"],
  "governed_by": ["FR-004"],
  "conformance_check": null,
  "negative_control": null,
  "iterations": 40,
  "contention": "concurrent enqueue during drain",
  "owner": "olamnit-glpnet",
  "disposition": "owned",
  "notes": "spec.md instance 1"
}
```

`conformance_check: null` classifies it **unproven**. That is the point — an undeclared surface is
invisible, a declared-but-unproven one is a visible piece of work. Never write a check path you have
not written.

Re-run the audit. It now exits **1** (unproven present) instead of **3** (undeclared present). That
is progress, and it is measurable progress.

## 3. Prove one

Write the check **and** its negative control together:

- **Wait class (FR-004)** — drive the signal under contention for **40** iterations; the caller must
  observe a correct result on all 40. Then show the harness **fails** against the pre-fix behaviour.
  Without that demonstration the 40 is arbitrary and the check may simply be incapable of failing.
- **Exit-status class (FR-007)** — inject a *did-not-run* and a *refused* condition; both must be
  classified non-success **and named**. Assert on content only the completed work could produce.
  Never on size, presence, or elapsed time (FR-010) — a 116 KB transcript containing no review
  passes every size check ever written.
- **Durability class (FR-012)** — observe, restart the reporting component, re-observe. The two
  observations must agree.

Fill in `conformance_check` and `negative_control`. Re-run. The surface is now **conforming**, and it
will go back to non-conforming by itself the day the property regresses.

## 4. When you find a defect you do not own

**Report it; do not fix it in their tree.** Set `disposition: "disclosed"` and `owner` to the owning
lane, add a conformance test that **fails** and names the defect, and publish the measurement.
Patching another lane's canonical component is how this fleet got three rival M6 clients in one
morning and five rival elections in one day.

## 5. What "adopted" means

Adoption is declared in feature 078's existing per-area adoption manifest, and one declaration covers
both features (FR-006a). Once your area is adopted, a consumer reading a non-conforming signal
**refuses** it. The only way past a refusal is 078's informed-consent override — briefing,
acknowledgement, rationale, declared scope, and a **mandatory expiry**. An override with no expiry is
rejected when it is recorded, not when it is relied on (FR-006c).
