<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# BK-STD-4 — The release bar, two-tier

**Status:** ACTIVE. Established by ruling `Q-GLPNETA16-04` (2026-09-01, ariellas/glpnet),
option `write-both`.

**Scope: FLEET-WIDE** — every lane, every repo, every host. Declared explicitly, per
`BK-STD-3` rule 4.

---

## 1 · Why this exists

`BK-STD-3` settled *precedence* between colliding rulings ("same subject, latest wins") but
deliberately did not settle *the release bar itself*. Three lanes each ruled a different bar
on 2026-08-31 and all three readings were defensible, because **they answer two different
questions that were never separated**:

- *What may a tag legitimately contain?* → the **content** question.
- *When is a tag worth cutting at all?* → the **significance** question.

Collapsing to a single bar throws one of those away. This standard keeps both by binding each
to a **release tier**.

## 2 · The rule

| tier | bar | qualifies when |
|---|---|---|
| **PATCH** (`vYYYY.MM.DD.N`, same-day increment or a fix-only cut) | **CONTENT BAR** | at least one `feat:` or `fix:` commit on `develop` since the last tag, **and** the code delta has passed a `/bk-codexreview` with no unresolved HIGH |
| **MINOR / FEATURE** (a cut announced as delivering a feature) | **FEATURE BAR** | at least one roadmap feature has reached `shipped` — completed, fully implemented **and** codex-reviewed |

**An engineer directive overrides the bar for a named cut**, and when it does, the receipt must
say so (§4). A directive does **not** silently re-tier a release.

**Neither tier may be met by docs/chore/merge commits alone.** A `develop` carrying only
`docs:`, `chore:` and merge commits qualifies for **no** tier.

## 3 · What this supersedes

Per `BK-STD-3` rule 2, this standard names every ruling it displaces:

| superseded ruling | set_id | lane | decided | its bar |
|---|---|---|---|---|
| `Q-hardening-03` | `Q-hardening-20260831T1320Z` | olamnit | 2026-08-31T13:08Z | patch-level qualifies |
| `Q-GLPNETA13-01` | `Q-GLPNETA13-20260831T1250Z` | ariellas | 2026-08-31T13:44Z | feature-level bar |
| `Q-GLPNETS13-03` | `Q-GLPNETS13-20260831T1310Z` | gavriella | 2026-08-31T17:56Z | content bar, directive overrides |

Each is **superseded, not deleted**. Each keeps its record and gains a `superseded_by` object
naming `Q-GLPNETA16-04`.

**How each survives inside this standard**, so nothing defensible was discarded:

- `Q-hardening-03`'s *patch-level qualifies* is the PATCH tier, now with a review condition.
- `Q-GLPNETA13-01`'s *feature bar* is the MINOR tier — it was always the right answer to the
  significance question and the wrong answer to the content question.
- `Q-GLPNETS13-03`'s *directive overrides* survives verbatim as the override clause in §2.

## 4 · Retrospective effect — stated explicitly, per `BK-STD-3` rule 5

**This standard is NOT retrospective.** Tags cut before 2026-09-01T16:00Z stand as cut, on
whichever bar their lane was following in good faith. In particular `v2026.09.01.1`, `.3` and
`.4` were cut on the content bar with zero features shipped; under this standard they are
**PATCH-tier releases and legitimate as such** — they were never MINOR cuts.

## 5 · The receipt requirement

**Every release receipt and every sitrep release line MUST name its tier and its evidence.**
A receipt that says only "released" is not comparable across hosts, which is the failure that
produced three bars in one day.

```
RELEASE <tag>  tier=PATCH|MINOR
  content bar   N feat/fix since <prev-tag>   [list]
  feature bar   N features shipped            [list, or "n/a for PATCH"]
  review        <codexreview run id> · <verdict> · unresolved HIGH = N
  directive     none | "<engineer instruction, verbatim>"
```

## 6 · Why a two-tier rule and not a single bar

Recorded so the next lane does not re-litigate it:

A single content bar makes every reviewed patch releasable, which is right — but it also lets
a release channel accumulate tags that deliver nothing a user would notice, and it gives the
fleet no way to say "this one is the feature". A single feature bar makes every tag meaningful
— but it strands reviewed fixes indefinitely, which is exactly what happened here: a clean,
codex-reviewed `fix(onrestart)` sat unreleasable behind features that were not close to
shipping.

**The tier is the missing word.** Both bars were correct; neither was complete.

---

*Authored by `ariellas` / `glpnet` under `Q-GLPNETA16-04`, 2026-09-01. Conforms to `BK-STD-3`
rules 1–5. Circulated for fleet adoption; a lane that disagrees should raise the collision
under `BK-STD-3` rule 2 rather than execute a fourth bar.*
