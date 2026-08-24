<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# SAFE-RESTART PREP — `mrun-f5ef56dba3c1` · ariellas lane · glpnet · 2026-08-24T07:00Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.

---

## 1 · Where the run is (objective, from durable rows — not from a summary)

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme
seq 297 · steps 40/91 complete · outstanding items 144 · open (in_progress)
```

🔴 **`buildkit-marathon` MUST be given `--feature glpnet-full-completion-programme`.**
The bare command resolves `.specify/feature.json` (which points at `085-onrestart-fleet-resume`)
and **falsely reports "no active marathon run"**. This is the single trap most likely to send a
fresh session into the wrong work.

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

## 2 · What this session changed (all durable, none in scrollback)

| what | where |
|---|---|
| **T16 discharged** — 2 C: scratchpad clones deleted, ~103 MB reclaimed | marathon checkpoint, full preservation evidence |
| **T18 discharged, premise corrected** — bk-flow + bk-proof *installed*, not authored; verified live | commit `88174d1b` |
| **083 FR-002 RULED (b)** record-the-rejection; FR-009 in scope; B02 unblocked | `specs/083-glptutorial-corpus-goldens/spec.md` |
| roadmap round 47: reconcile→sync→import→reconcile→dedupe→export→commit→push | commit `dcb6e465` |
| ruled table renderer adopted **with its defect recorded** | `scripts/roadmap_open_table.py` |
| 4 engineer rulings 2026-08-24 | `mitem-01a03293-9be7-75be-bab7-81e13f489003` |
| `roadmap link` silent-no-op bug | `mitem-01a03262-6239-74be-9c3a-16eef18d663c` |
| bk-flow install-not-author finding | `mitem-01a03262-84fb-7770-966b-dd3b86a402cd` |
| DuckLake takt gap raised as a feature | roadmap `takt-and-token-persistence-to-ducklake` (captured, midi, low risk) |

**Commits**: `dcb6e465`, `88174d1b` — both pushed to `origin/091-bkstd1-round42`. **Tree clean.**

**COOP written** (live root `I:\coop\`, NOT the stale in-repo `COOP/`):
`20260824T062100Z-...ACK-SWEEP...` (glpnet) · `20260824T062100Z-...FLEET-BROADCAST-ERA-IS-A-FEATURE...` (fleet root) ·
`20260824T065500Z-...RELEASE-HELD...` (to gavriella) · ACK-LEDGER row appended.

## 3 · The four engineer rulings now in force (2026-08-24)

1. **083 FR-002 = (b) record the rejection.** Book text stays byte-exact; the golden records the
   rejection. FR-009 **in scope**. B02 unblocked. B10 confirmed.
2. **Release HELD** until gavriella confirms quiescence. Do **not** cut `v2026.08.24.1` before an
   ACK arrives on the `RELEASE-HELD` message.
3. **`roadmap link` defect: FILE, do not patch.** Buildkit lane owns the code. **T20 stays blocked** —
   do not attempt the 14 links through the CLI, and do **not** write `spec_path` directly.
4. **DuckLake takt: SPEC it, do not hand-patch.** Until it ships, every takt report must carry the
   explicit **not-lake-sourced** caveat.

## 4 · WHAT'S NEXT — ranked, with blockers named

| rank | step | size | state | blocked-by |
|---:|---|---|---|---|
| 1 | **B02** — 083 `/bk-plan` | midi 11 | ✅ **UNBLOCKED** (FR-002 ruled) | — |
| 2 | B03–B08 — 083 tasks→analyze→implement→codexreview→ship→close | mixed | follows B02 | B02 |
| 3 | B10 — report the book-§4.3.1 guard finding to Udi | nano 1 | ✅ unblocked | — |
| 4 | Release `v2026.08.24.1` | micro 3 | **gated** | gavriella quiescence ACK |
| 5 | T19 — ERA tag in marathon | midi 11 | **held** | PREREQ T11 (takt emission) |
| 6 | T20 — link 14 spec dirs | mini 7 | **held** | `link` CLI defect (buildkit lane) |
| 7 | W11 — resolve 080 | mini 7 | **gated** | Udi §1.14 ruling, discharge item J2 |
| 8 | W18 — Gleam cluster | mini 7 | **gated** | two contradictory recorded reads (N12 vs C1) |

**B02 is the next action for a fresh session.** The marathon's own `next:` still points at **W11,
which is engineer-gated** — the `next` field does not account for gating, so follow this table, not
`next`. (This is measurement trap #1 in `docs/SITREP-FORMAT.md`.)

## 5 · 🔴 Standing hazards — read before acting

1. **gavriella is LIVE in this repo.** We already collided on roadmap round 47. Check
   `origin/develop` and the coop root before any shared-resource write.
2. **This branch is 32 behind `origin/develop`.** Rebase or merge before any wide-scope work; the
   round-47 export here predates gavriella's import legs.
3. **"STUCK lock" is a FALSE POSITIVE.** Twice this session the registry named a live process as a
   stuck holder (`python -m pytest tests/roadmap tests/marathon`, 576 s CPU). **Verify liveness with
   PowerShell `Get-Process`, sampling CPU twice. Git-Bash `ps -p` cannot see native Windows PIDs.
   Never kill a holder.**
4. **The ruled table renderer under-reports.** `roadmap_open_table.py` gives **23**; the signed-export
   `heads` fold gives **24**. Always cross-check against the fold.
5. **This repo is NOT a registered deploy target.** `D:\BSTDEV\research\glp\GLPNET` has no `pin.json`
   under the deploy home (the *stale* clone `D:\BSTDEV\glp\GLPNET` is the registered one). Hence
   `engine resolution degraded: pin mirror absent` on every command. **Not fixed — deploying would
   pin an engine version mid-marathon and needs an engineer decision.**
6. **3 dangling `spec_path` pointers**: `specs/067-qr-link-provisioning`, `specs/066-wave6-consolidation`,
   and `guards-reference.md#comparison-guards` (a markdown anchor recorded as a spec directory).
7. **Never fetch tags with force** — `v2026.06.10.1` reports "would clobber existing tag".

## 6 · Restart readiness checklist

- [x] Working tree clean
- [x] All work committed **and pushed** (`88174d1b`)
- [x] All findings durable in marathon items, not scrollback
- [x] Engineer rulings recorded in the spec **and** the marathon
- [x] COOP ACKs written to the live root; ledger row appended
- [x] Resume verified: `buildkit-marathon resume --feature glpnet-full-completion-programme`
- [x] Next action identified and unblocked (**B02**)

**READY FOR RESTART.**

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-24T07:00:00Z`
