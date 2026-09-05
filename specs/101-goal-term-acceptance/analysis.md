<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Analyze — cross-artifact consistency: `spec.md` × `plan.md` × `tasks.md` × the code

**Feature**: `101-goal-term-acceptance` · **Stage**: analyze · **Run**: 2026-09-05
**Method**: read each artifact against the others *and against the measured behaviour of all
three runtimes*. Findings are ordered by consequence, not by artifact.

---

## A1 · 🔴 CRITICAL — `FR-008a` / `SC-003a` rest on a premise that measurement has falsified

| | |
|---|---|
| **Spec says** | *"Gleam's conjunction path is currently deferred"*; FR-008a requires a regression test asserting Gleam **refuses** a conjunctive goal; SC-003a requires **0 silent divergences** on that refusal. |
| **Measured** | **Gleam ACCEPTS conjunctive goals.** `goal_boot.setup_goals` routes every argument through **the same `setup_args`** as the single-goal path; a conjunction containing `_` boots and reports `["Y","Z"]` — identical to Dart and C#. |
| **Consequence** | 🔴 **SC-003a cannot be satisfied without BREAKING Gleam.** A test pinning a refusal would fail against working code, and making it pass would mean deliberately degrading a path that already agrees with the other two runtimes. |

**Corroborating stale artifact**: `goal_boot.gleam`'s own module header still reads *"STILL
DEFERRED, surfaced LOUDLY"*. The header and the spec clarification were written together and
describe a state that no longer holds — **the clarification's bounding of the parity obligation
was reasoning from that header.**

**Recommendation (engineer decision — deliberately NOT taken here)**: retire FR-008a and
SC-003a, and **widen** FR-008/SC-003 to include conjunctive shapes, since all three runtimes now
demonstrably agree on them. **The spec is left unedited pending the ruling**: editing a spec to
match the code is the inversion `DISCIPLINE.md` §1.10 exists to forbid. Tracked as **T024**.

*Nothing downstream is blocked — the shipped behaviour is correct and measured in all three
runtimes. What is open is which sentence in the spec is right.*

---

## A2 · 🔴 HIGH — the plan scoped **one runtime**; the spec's own FR-008 requires **three**

`plan.md` §1 states, in bold: *"**All eight sites are in ONE file:**
`glp_runtime/lib/engine/glp_engine.dart`."* Measured: that is true **of Dart only**. The
identical eight sites existed in `out/csharp/lib/engine/glp_engine.cs`, and four positions in
`glp_gleam/src/glp/engine/goal_boot.gleam`.

**The plan contains no C# task at all**, while FR-008 makes three-runtime agreement a
requirement and SC-003 makes it an acceptance criterion.

🔴 **This is the root cause of the era's central defect.** The plan's file census was correct
for the runtime it examined and was then read as a census of *the work*. C# was implemented only
after this analysis pass measured it — by which time `CLAUDE.md` and `docs/known-issues.md` had
**already stated the fix had landed and named the exact C# line numbers**, and the C# runtime
was still returning a **silent wrong answer**.

**Remediation applied**: C# brought to parity (`d8dbd593`); the plan's one-file claim is
superseded by `tasks.md` Phase 2–4, which enumerate all three runtimes.

---

## A3 · MEDIUM — the spec's "Cross-runtime state today" table is now stale in two cells

| cell | spec says | measured now |
|---|---|---|
| Gleam × anonymous `_` | *"Refuses loudly, named as a deferred shape"* | **Accepts** at all four positions |
| Gleam × conjunction | deferred | **Accepts** (A1) |

The table is a **Measured Baseline** — a snapshot of the state *before* the work — so it is not
wrong as history. It is only misleading if read as current. **Recommendation**: leave it, and
rely on `tasks.md` §0 plus the restart brief to mark it as a baseline rather than a status. No
change made.

---

## A4 · Coverage — every FR and SC traced to a test or an explicit exclusion

| requirement | discharged by | verdict |
|---|---|---|
| FR-001 (4 positions) | V-1..V-4, Gleam ×4, C# ×6 sites | ✅ |
| FR-002 (conjunctions) | V-4, Gleam conjunction test | ✅ |
| FR-003 (no aliasing) | by construction + Gleam positive/negative pair | ✅ |
| FR-004 (no binding) | V-5 + Gleam pair with negative control | ✅ |
| FR-005 (refuse, never alter) | V-6, V-22, Gleam refusal test | ✅ |
| FR-006 (legible refusals) | V-7, V-8, V-16, V-23, Gleam sweep test | ✅ |
| FR-007 (session survives) | V-9 + Gleam heap-untouched test | ✅ |
| FR-008 (3-runtime agreement) | **V-18..V-23 — byte-identical transcripts** | ✅ |
| FR-008a / SC-003a | — | 🔴 **A1: falsified premise** |
| FR-009 (regression per shape) | Section V, 23 checks | ✅ |
| FR-010 / FR-011 (docs, locations) | `af77d284` incl. the correction of record | ✅ |
| FR-012 (no language change) | scope held; ruling `Q-101-02` explicitly bounded to goal terms | ✅ |
| SC-001 (4 shapes → 0 failing) | V-1..V-4 | ✅ |
| SC-002 (0 runtimes answer it) | V-6, V-22, Gleam | ✅ |
| SC-003 (0 divergences) | V-20 byte-identical | ✅ |
| SC-005 (4 claims tested) | V-14, V-15, V-1..V-5, V-6/V-7 | ✅ |
| SC-006 (suites stay green) | 582/580, Gleam 645, C# 0 errors | ✅ |

---

## A5 · METHOD FINDING — the criterion was untestable *by the suite's own shape*, and that is the transferable defect

FR-008/SC-003 required three-runtime agreement. **Nothing in the 566-check suite had ever
started a second runtime.** The criterion was therefore not merely untested — it was
**unmeasurable by construction**, and every green run was consistent with total divergence.

The fix is differential, not additive: run **one** script through **both** implementations and
require **byte-identical** transcripts. Two design details carry the weight:

1. **A non-empty guard runs first.** Two empty transcripts also compare equal, so a diff without
   that guard is a check that passes hardest when everything is broken.
2. **The test was itself tested.** The C# fix was reverted, rebuilt, and re-measured: V-20 fails
   and prints the divergence, V-22 fails, V-23 catches the leaked class name. *A green check
   whose failure mode was never observed is not evidence.*

Codified as roadmap feature `differential-cross-runtime-acceptance-gate` (WSJF 19.5 / RICE
774,000 — highest on the board by ~3×), and ruled the lane's next era (`Q-101-04`).

---

## A6 · Residual risk carried into ship, with the engineer's ruling

| risk | disposition |
|---|---|
| 2 Section T failures | **Attributed, not assumed** (stash → rebuild → re-run → 5/2 identical). Pre-existing, absent `glpquick.pfx`. Shipping **disclosed** under `Q-101-01`. |
| 1 skip (`ms_message` venv) | Explicit skip; the suite's honest-exit guard returns non-zero rather than reporting green. Working as designed. |
| A1 spec defect | Escalated as T024; does not block ship — behaviour is correct and measured in all three runtimes. |

---

## Verdict

**Ship-ready under ruling `Q-101-01`, with one escalation (A1/T024) and one root cause recorded
(A2).** No finding blocks the release; A2's remediation is already in the tree, and A1 is a
question about which sentence of the spec is correct, not about what the code does.
