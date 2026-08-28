<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# PROVENANCE — this is an INSTALLED COPY, not a source

🔴 **GLPNET DOES NOT OWN `/yx-bootmig`. Do not edit `SKILL.md` in this repo.**

| field | value |
|---|---|
| **specification owner** | `D:/BSTDEV/research/yngenios/specs/008-yx-bootmig-base/{BRIEF.md,DESIGN.md}` |
| **copied from** | `D:/BSTDEV/research/olamnit/.claude/skills/yx-bootmig/SKILL.md` |
| **source sha256** | `1b0ad3976139958ce0217dc1d5dfc019b3001e6248990e13c549271c85e3bcce` |
| **installed** | 2026-08-28T02:20Z · lane `gavriella` · host `GAVRIELLA` · engineer-instructed |
| **relationship** | **byte-identical copy.** Verified at install. |
| glpnet's role in the programme | **source repo only** (never a target) — verified root `D:/BSTDEV/research/GLP/GLPNET`, 7,959 tracked files |

## Why byte-identical, and why a sidecar instead of an edit

The skill is now vendored in **three** places (yngenios spec → olamnit install → this install).
`Q-YXBOOTMIG-04`, open in the owning lane's set `Q-YXBOOTMIG-20260828T0110Z`, is a ruling about
**exactly this failure mode**: `tools/bkquestion` is vendored in 5 repos on this host with **four
different `bkquestion.py` hashes** — "all four expose the same 5 verbs, so they are dialects, not
forks", which is the polite description of a standard nobody can compare.

A third copy that drifts would make that worse. So this copy is installed **verbatim** and every
local finding lives **outside** `SKILL.md`, here. Drift is therefore detectable in one command:

```
sha256sum .claude/skills/yx-bootmig/SKILL.md D:/BSTDEV/research/olamnit/.claude/skills/yx-bootmig/SKILL.md
```

**If they differ, someone edited a copy. Fix the owner, then re-copy — never patch this file.**

## 🔴🔴 THREE OF THE FOUR HARD PRECONDITIONS ARE STALE — measured 2026-08-28T02:35Z

**The installed `SKILL.md` refuses on grounds that no longer hold.** Its four "REFUSE, never
default" preconditions were re-measured against the owner spec and this disk. **Three are false.**
🔴 **I repeated two of them myself** in this file's first revision and in coop
`20260828T0215Z` — **both are withdrawn here.**

| # | precondition as written | measured 2026-08-28 | verdict |
|---|---|---|---|
| 1 | "P0 — L3/L4 are UNDEFINED … ruling `R-L4` blocks" | `LATTICE.md` **Amendment 1.1, "Legacy vocabulary map"** is explicitly **total over terms found in the estates**: **L3 IS a ring** (device + device-version shims); **"L4" is declared NOT A RING**, with a **named disposition** — legacy shorthand for per-deployment packaging, covered by `DEC-PUBLISH-1` (one package per platform target) | **FALSE — P0 is satisfied.** A membership rule per layer plus a named disposition for what fits neither is *exactly* what P0 demanded, and it has been in the spec since 2026-08-03 |
| 2 | "Epic `bootstrap-migration` does not exist" | present in the yngenios roadmap export `gavriella__yngenios__20260828T005824Z.json` — `entity_kind=epic`, `guid 01M0YTBK42W6MY72S4YTGZKVA1`, `resolved_slot=epic-bootstrap-migration`, description matching this programme | **FALSE — it exists** (8 epics total) |
| 3 | "Three of the four targets are absent from this host" | all four present, all git repos, verified by content: `yngenios` 1,221 · `yngenios-windows` 5,077 · `yngenios-linux` 483 · `yngenios-app` 589 tracked files | **FALSE — and already RETRACTED in the owner BRIEF** on 2026-08-28 (M3: *"An evidence line that does not reproduce its own finding is not evidence"*) |
| 4 | "An undelineated source is REFUSED (FR-2)" | no P3 scope manifest exists for any source | ✅ **STANDS — this is the one real gate** |

**Consequence:** `P4 NOT-DISCHARGEABLE-HERE` was wrong. The blocker is **not** missing targets or an
undefined lattice — it is that **P3 has never been run**, and P3 needs the **P2 cross-repo relation**
that does not exist yet. **Refuter for this whole block:** if `D:\yngenios\*` are clones that a peer
host does not have, my target census is host-local and the fleet claim needs a second vantage.

## 🔴 A READER DEFECT THAT MAKES EPICS VANISH — candidate root cause for `Q-YXBOOTMIG-03`

Reading the same signed export two ways:

```
key on 'kind'         ->  {MISSING: 8, feature: 99}     ZERO epics
key on 'entity_kind'  ->  {epic: 8,    feature: 99}     EIGHT epics
```

Epic heads carry **`entity_kind`**, not `kind`. **Any reader keying on `kind` silently reports zero
epics** — the house defect class exactly: a present thing reads as an absent one, exit 0. I hit it
myself in my first probe this session and nearly published "0 epics".

`Q-YXBOOTMIG-03` (open) has the two standard renderers disagreeing on one catalog —
`roadmap_open_table.py` **4 epics** vs `bk_report_v1.py` **7**. **Stated as a candidate, not a
finding:** the magnitudes differ (4-vs-7, not 0-vs-8), so this is *a* field-keying divergence of the
same class, not proven to be *that* one. **Refuter:** point both renderers at this export and show
they agree on the epic count — that kills it.

## 🔴🔴 P2 ANALYSED 2026-08-28T02:55Z — **THE SKILL MISDIAGNOSES ITS OWN BINDING CONSTRAINT**

P2 as written says: *"extend the callgraph node key beyond `{repo}:{rel}` so edges cross repos."*
**Read the code: the node key is not the defect, and extending it would change nothing.**

```python
# yx/src/yx_distill/tools/callgraph/workflow.py
by_key[f"{repo}:{rel}"] = aid                    # ← ALREADY repo-qualified. Correct as-is.
...
to_aid = by_key.get(f"{repo}:{ref}")             # ← THE DEFECT: `repo` is the CITING artifact's
if to_aid is not None and to_aid != aid:         #    own repo, so an edge can only ever land
    pairs.append((aid, to_aid))                  #    inside it. A miss is dropped — no counter.
```

**The blindness is TWO layers deep, and the upper one is fatal:**

| layer | site | what happens |
|---|---|---|
| **1 — ingest** | `survey/parse.py:97 resolve_references` + `survey/workflow.py:177` | `index`/`stem_index` are built **"in-repo … over INCLUDED files"**, inside a per-`repo_id` loop. A token naming another repo matches nothing and is **dropped at parse time**. Its own docstring: *"Unresolved (external) tokens are dropped — `referenced` holds **in-repo edges only**"* |
| **2 — edge resolution** | `callgraph/workflow.py:43` | even a surviving cross-repo ref cannot resolve, and the miss is discarded by `if to_aid is not None` with **no counter, no warning, exit 0** |

🔴 **So the substrate DESTROYS the cross-repo evidence at ingest — one layer before any graph
computation runs.** `yx callgraph compute` cannot distinguish *"no cross-repo edges exist"* from
*"every cross-repo edge was discarded"*: same exit code, same silence, **no denominator**. That is
this programme's declared house defect class — *a check that cannot fail is worse than none* —
sitting inside the programme's **own primary instrument**.

⚠️ **AND THE OBVIOUS FIX IS UNSAFE.** The module-ish fallback resolves tokens by **lowercased path
stem**. Union `stem_index` across repos and `mailbox` in `qhstate`, `olamnit` and `yngenios` all
collide — and the `bootstrap-migration` epic's own description records **three divergent, NOT
byte-equivalent C# implementations of the kernel**. Naive cross-repo stem matching would therefore
**mint false edges between different implementations of the same-named thing**, which is exactly
**FR-8** (*same identity + different semantics ⇒ ESCALATE with both provenances*). **Ambiguity must
escalate, never silently resolve.**

### M1 REUSE-vs-REBUILD — answered with evidence: **REUSE**

`survey → artifacts → edges` is sound and already carries `repo_id` end-to-end. What needs
re-specifying is **one function and one lookup**: `resolve_references` must **retain** unresolved
tokens with provenance instead of dropping them, and resolution must search across repos **with an
explicit ambiguity outcome**. That is a spec change, not a second tool — and duplicating `yx` is the
defect M1 warns about.

**Refuter for this whole block:** run `yx survey` over two repos and show a `referenced` entry
holding a path that belongs to the *other* repo. If one exists, my ingest-drop claim is wrong.

**This lane did not build it.** `yx` lives in the yngenios repo; this skill is advisory and never
writes across repos. Delivered as a design finding for the owner.

## 🔴 KNOWN DEFECTS IN THE INSTALLED TEXT — measured from this lane 2026-08-28, filed for the owner

These are **not** applied to `SKILL.md` above. They are published in the coop channel at
`20260828T0215Z-gavriella-glpnet-CORRECTION-…` and are for the yngenios/olamnit owner to apply.
**Read them before you act on the corresponding sections.**

### 1 · REFUTED — *"`GLPNET` (capitals) is a DIFFERENT directory"* (§ Root resolution)

One directory, not two. NTFS is case-insensitive and git normalises the lowercase alias:

```
stat -c %i D:/BSTDEV/research/GLP/glpnet  ->  281474976874354
stat -c %i D:/BSTDEV/research/GLP/GLPNET  ->  281474976874354      SAME INODE
git -C D:/BSTDEV/research/GLP/glpnet rev-parse --show-toplevel
                                          ->  D:/BSTDEV/research/GLP/GLPNET
```

The canonical spelling is **`GLPNET`**. The 2026-08-25 case-collision incident was real but is not
this pair on this filesystem. *(A genuinely distinct sibling `GLPNET-016` does exist and holds
4,127 untracked files from a halted deletion — that one deserves the warning.)*

### 2 · CORRECTED — the decoy test does not discriminate (§ Root resolution, FR-9)

The text says `qhstate-Yngenios` is "not a git repo" and offers `git rev-parse` as verification.
**`git rev-parse` PASSES on both decoys**, because it walks up to a repo at the parent:

```
git -C D:/BSTDEV/research/qhstate-Yngenios  rev-parse --show-toplevel -> D:/BSTDEV/research
git -C D:/BSTDEV/research/olamnit-assistant rev-parse --show-toplevel -> D:/BSTDEV/research
```

`D:/BSTDEV/research` is itself a git repo. **FR-9 must compare the resolved toplevel to the
requested path and refuse when they differ** — asking "does git answer?" is not a verification.

### 3 · A2.2 CONFIRMED, with a magnitude (§ Scope delineation)

The path-name proxy undercounts this repo by **444×** — quoted here so no one uses the 6.0%
figure as a scope statement:

| instrument over GLPNET's 7,959 tracked files | hits |
|---|---:|
| `yngenios` in the **path** (the proxy) | **1** |
| `yngenios` in the **content** | **444** |
| `ynet` in the path / content | 50 / 341 |

Neither number is a delineation — content-mentions over-count too. Classification is **P3, gated**,
and is deliberately not pre-empted here.

### 4 · Tracked counts have moved on every source except buildkit

`qhstate 4,034` (was 3,983) · `olamnit 3,056` (was 2,970) · `glpnet 7,959` (was 7,896) ·
`buildkit 5,736` (unchanged) · `yngenios 1,697`. The table in `SKILL.md` is a snapshot, not a
constant — **re-measure before quoting it.**

## Gate state from THIS lane, at install time

| phase | state (re-measured 2026-08-28T02:35Z) |
|---|---|
| P0 define L3/L4 | ✅ **SATISFIED, not blocked** — `LATTICE.md` Amendment 1.1 maps the legacy L0–L4 vocabulary totally onto the five rings; L3 is a ring, L4 is explicitly not one and has a named disposition (`DEC-PUBLISH-1`) |
| P1 resolve + verify roots | ✅ ran 02:15Z — **no unreadable roots on this disk**; 4/4 targets and 5/5 sources verified by content |
| P2 cross-repo relation | ⚠️ **ANALYSED 02:55Z — and the skill misdiagnoses it.** The node key is already repo-qualified and correct; the blindness is (1) unresolved tokens **dropped at ingest** and (2) edge lookup bound to the citing repo, missing silently. See the P2 block above. **Not built — the tool is in another repo.** |
| P3 scope delineation | **GATED (correctly)** — `R-A3.1`/`R-A3.2`; an undelineated source is REFUSED (FR-2). **Cannot be derived without P2**, and must not be faked from the path proxy (444× undercount, above) |
| P4 migrate | **BLOCKED ON P3 ONLY** — *not* on absent targets; all four are present and readable here |
| P5 verify + report | n/a |

**Four rulings are UNDECIDED** in the owning lane's set `Q-YXBOOTMIG-20260828T0110Z`
(Era shape · BK release · Renderer · bkquestion). By that set's own words nothing downstream is
safe to specify until Q-01 is answered. **This lane has no standing on them and must not answer
them.** The epic `bootstrap-migration` still does not exist on any roadmap.

— `gavriella` · `glpnet` · `GLPNET` · run `mrun-20d9230f767b` · 2026-08-28T02:20Z
