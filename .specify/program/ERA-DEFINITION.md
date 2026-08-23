<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# ⚖️ ENGINEER RULING — **AN ERA IS A FEATURE.** Authoritative definition. ALL LANES · ALL HOSTS · ALL REPOS.

> glpnet adopts this VERBATIM (not paraphrased — a shorter form would be the very compression it
> forbids). Source: ENGINEER 2026-08-23, relayed by gavriella (mstack `mrun-7532bd26ae63`,
> 2026-08-23T17:20Z); canonical peer copy crucible `.specify/program/ERA-DEFINITION.md`
> (697ba70 + 773396c). BINDING — not a proposal, not open for lane-local reinterpretation.

---

## 1 · ⚖️ THE DEFINITION

> ### **An `era` is a synonym for a FEATURE.**
>
> ### An era is **the work needed for a feature**, from
> ### **`/bk-specify` → `/bk-clarify` → `/bk-plan` → `/bk-tasks` → `/bk-analyze` → `/bk-implement` → `/bk-codexreview` → `/bk-ship` → `/bk-close`.**

**The era opens at `/bk-specify`. The era closes at `/bk-close`.** Everything the feature required
in between **is** the era. The nine stages are **constitutive of** the era, not metadata attached
to it.

## 2 · ⛔ WHAT IS FORBIDDEN

The engineer's ruling is explicit that the following are **forbidden**, and names them
**performance theater**:

- ⛔ **Decomposing a feature into small down-summaries.** An era is not a digest of a feature.
- ⛔ **Max-loss compression of a feature's work into "atoms".** The work is the era. Compressing it
  away destroys the thing being measured.
- ⛔ **Hyper-distorted fragments carrying no functionality identity.** An era must retain the
  feature's full functional identity — what it *does* — not just its endpoints.
- ⛔ **Reducing an era to a pair of bracket events.** Two timestamps are not an era. That is a
  measurement of an era's *duration*, and mistaking the measurement for the thing is the error.

**An era is not a tag. It is not a label. It is not a summary. It is the feature and all the work
the feature required.**

## 4 · ✅ WHAT THE MARATHON MUST ACTUALLY CARRY

An era is a **first-class record of a feature's whole workstream**. At minimum it must hold, and
must not discard:

| must carry | why |
|---|---|
| the feature's **functional identity** — what it does, in its own terms | §2 forbids fragments with no functionality id |
| **every one of the nine stages**, each individually recorded | the stages are constitutive; a missing stage is a missing part of the era |
| **per-stage work**, not just per-stage timestamps | the era is the *work needed*, not when it started |
| the **open** at `/bk-specify` and the **close** at `/bk-close` | the era's boundaries |
| **linkage to the feature's artefacts** — spec, plan, tasks, analysis, review, ship | so the era resolves to real work, not a stub |

**Duration and takt are DERIVED FROM the era. They are not the era.** Measuring an era yields takt;
measuring is not defining.

**Close discipline:** an era must not be closable unless the feature has actually shipped, so the
bracket cannot be faked.

## 5 · 📋 STATUS IN THE TOOLING — this is not yet buildable

`buildkit-marathon` exposes: `open resume status position doctor discharge capture expand park
sequence resolve defer backlog step-start checkpoint trace gate discharge-item override takt
takt-target version`. **There is no `era` verb and no `era` field.** This is new surface in the
buildkit package, owned by the **buildkit lane** under the 2026-08-23T12:05Z ruling. It cannot be
approximated by a local naming convention — a convention enforces nothing, and an unenforced era
can silently lie.

## 6 · 📋 REQUIRED OF EVERY LANE

1. **Adopt §1 as the definition of `era`.** Immediately, in every repo, on every host.
2. **Discard any lane-local era shape** that reduced a feature to bracket events / metrics / tags.
3. **Do not compress, summarise, atomise or bracket-reduce a feature and call the result an era.**
4. **ACK receipt** on your channel so the fleet has a record of who is working to the correct
   definition.

---

**ERA = FEATURE. `/bk-specify` opens it. `/bk-close` closes it. Everything the feature needed in
between is the era.**

*glpnet adoption recorded by olamnit, 2026-08-23. The `era` verb/field itself is buildkit-lane
surface (§5) — glpnet consumes it when shipped; it is NOT approximated here by convention.*
