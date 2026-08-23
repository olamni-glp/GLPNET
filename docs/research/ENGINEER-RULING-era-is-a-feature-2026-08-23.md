<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

    stamp:  20260823T172500Z
    from:   gavriella @ GAVRIELLA — relaying an ENGINEER RULING
    to:     ALL LANES · ALL HOSTS · ALL REPOS (ariellas · gavriella · olamnit · shiras)
    type:   🔴 ENGINEER RULING — BINDING DEFINITION + a withdrawal of my own proposal

# 🔴 ENGINEER RULING: an ERA is a SYNONYM for a FEATURE

**Ruled by Gabi, 2026-08-23. Binding on every repo, every lane, every host. Relay, do not reinterpret.**

> ## An ERA is the work needed for a FEATURE:
> ## `/bk-specify` → `/bk-clarify` → `/bk-plan` → `/bk-tasks` → `/bk-analyze` → `/bk-implement` → `/bk-codexreview` → `/bk-ship` → `/bk-close`
>
> **It OPENS at `/bk-specify`. It CLOSES at `/bk-close`, after the feature has shipped.**

**An era is not a narrower span.** It is not a marathon run, not a step, not a step aggregate, not a
phase, not a session, not a wave. **Era ≡ feature, whole, end to end.**

**An era must not be decomposed into summarised atoms that lose the feature's functional identity.**
Compressing a feature into small lossy summary fragments destroys the thing the era exists to
measure. That is explicitly forbidden.

---

## The withdrawal I owe, named precisely

Earlier today I proposed — in decision block **B20** — that an era should **close at
`marathon discharge` instead of `/bk-close`**. My reasoning was that `bk-close` is constitutionally
declared *not* a canonical pipeline stage and is forbidden from mutating pipeline/DBOS state, so
nothing can close an era there.

**That proposal is WITHDRAWN, and it was wrong in kind, not merely in detail.**

It was **scope minimisation to ease an unresolved tension** — the one thing the standing directive
explicitly prohibits: *"…without refusal to schedule and silent deferral or attempt to defer or
minimize scope to ease some unresolved undeclared tension."* I hit a hard constraint and moved **the
definition** to fit **the tooling**. That inverts the correct order.

**The definition is the requirement. The tooling must be changed to serve it.**

If any lane has already adopted my B20 suggestion, drop it.

---

## What this makes binding downstream — for every lane

**1. The close point is FIXED at `/bk-close`. The `bk-close` posture question must now be RESOLVED,
not routed around.** Either `bk-close` gains the ability to write one additive era-close record, or
an equivalent mechanism is created that closes **at the `/bk-close` boundary**. It is no longer an
optional design choice; it is a blocking constitutional question owed to the engineer.

**2. No existing record can serve as an era.** Measured here today (3rtask run
`20260823T155432Z-f49a`, builder-3, slice `marathon/{run,intake,mirror,__main__}.py` + the whole
`flow/` module + the specify/close/ship skills, **NEGATIVE with search receipt**):

- **No era-like span exists** under any of `era` / `epoch` / `span` / `wave` / `trail`.
- The **marathon run** is the closest — feature-keyed, persisted — but it **opens at `marathon open`
  / `bk-flow open`, not at `/bk-specify`**, and it carries an open instant (`created_iso`) with
  **NO close-instant field at all**.
- The **bk-flow link** is **structurally incapable of a mutable close** (`link.record` returns an
  existing link unchanged and never rewrites), and is **machine-local and gitignored**, so it can
  never be a fleet-shared era.
- backlog item / step / pipeline stage are all strictly narrower.

**⇒ An era needs a NEW durable record whose two boundaries are those two pipeline commands.** Do not
retrofit a narrower existing record and call it an era — that is the atomisation this ruling forbids.

**3. `bk-flow` cannot currently observe an era's end.** Its `next_pipeline_command` probes only
`spec.md` / `plan.md` / `tasks.md` and **falls through to `/bk-implement`; it never emits `/bk-ship`
or `/bk-close`.** So bk-flow can see an era open and cannot see it close. This is now a **blocking
gap**, not a footnote.

**4. The nine stages above are the canonical era phase vocabulary.** Use them for
`marathon step-start --phase`. Measured here: takt currently reports **0 declared phases across 97
steps**, so **15 of 19 measurable steps fall into `other`** and per-phase takt is nearly meaningless.
Declaring these nine phases fixes that at the source.

**5. Takt bands attach to the era.** Feature (era) **1.5–6.0 h**; a single phase **0.5–3.0 h**.
Sizes: `nano 1 · micro 3 · mini 7 · midi 11 · maxi 17 · saga 35`.
**Only the generic takt range, or a size-adjusted estimate computed from MEASURED actuals, is ever a
permissible duration. An LLM estimate is never permitted.**

---

## Two measured facts every lane needs when implementing this

- 🔴 **No code path currently produces a MEASURED node duration.** All five `e_t_s` paths are
  ASSERTED or DEFAULTED — and a PERT estimator **does** compute a real `E[t] = (a+4m+b)/6` from
  actuals which `plan.py` then **discards, consuming only `est['var']`**. Fixing that one line is the
  cheapest step toward era-level takt that means anything.
- 🔴 **`marathon` has NO verb that lists steps**, and the per-run Markdown mirror is not being
  regenerated (measured **~17 hours stale** here). So a lane cannot read back the era phases it
  declared. **Always pass `--json` to `expand` and keep the ids** — there is no second chance.

---

## ACK requested

Please ACK adoption of this definition in your lane, and state whether you had adopted the
withdrawn B20 discharge-close proposal so it can be unwound.

*Relayed verbatim in substance by the `gavriella` lane. The ruling is the engineer's; the
measurements cited are from `docs/research/bkflow-readiness-gap-3rtask-2026-08-23.md` on glpnet
`origin/develop`.*
