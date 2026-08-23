<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# T17 — classification of every non-active local branch, host `Ariellas`

**Measured** 2026-08-23 against `origin/develop` @ `b9fd9dac`, after
`git fetch origin --prune --tags`. **Marathon step** T17
(`mstep-01a02f5e-f2a5-71c2-ac02-346f7f4c15c5`), run `mrun-f5ef56dba3c1`.

## Method (supersedes T04's, which was unsound)

For each of the 46 local branches except the active one (`085-onrestart-fleet-resume`):

1. `merge-base --is-ancestor <sha> origin/develop` → contained in the integration branch?
2. `for-each-ref --contains <sha> refs/remotes refs/tags` → preserved **anywhere** on origin?
3. `rev-list --left-right --count origin/develop...<sha>` → ahead / behind.

Step 2 tests **branches AND tags**. A check over `refs/remotes/origin/*` alone reports false
"uncontained" results — proven on `058-s4-policy-service`, which survives *only* via its archive
tag. That error was made and caught during the T15 survey the same day.

## Result — 45 branches, and **NOTHING requires preservation first**

| Verdict | Count | Meaning |
|---|---|---|
| **RETIRE** | **39** | 0 ahead of `origin/develop`; fully contained. Deleting loses nothing |
| **RETIRE-LOCAL** | **6** | ahead of develop, but every head is preserved on origin |
| **PRESERVE FIRST** | **0** | — |

**There is no MERGE row and no RE-DERIVE row.** Every branch that is ahead of `develop` already
has its head reachable from an origin ref, so no local branch is the sole custodian of any work.

### The 6 that are ahead — each with the ref that preserves it

| Branch | head | ahead | preserved by |
|---|---|---|---|
| `030-phase8-polish` | `363fba46` | 9 | `origin/backup/030-phase8-polish` + `archive/backup__030-phase8-polish-20260820` |
| `050-full-gleam-combined` | `10f02f7d` | 48 | `origin/050-full-gleam-combined` + `archive/050-full-gleam-combined-20260820` |
| `064-durable-walfix` | `d0187c9f` | 1 | **`archive/064-durable-walfix-20260822` (tag only — its branch is gone)** |
| `067b-qr-link-continuation` | `abe9aec5` | 27 | `origin/067b-qr-link-continuation` + archive tag |
| `083-glptutorial-corpus-goldens` | `d4e4598f` | 2 | `origin/083-glptutorial-corpus-goldens` |
| `upgrade/buildkit-migration-20260627T220138Z` | `96d0ce8e` | 1 | `origin/backup/...` + archive tag |

`064-durable-walfix` is the one to note: it is **another lane's** unpushed commit, and it survives
**only** because A01 pushed an archive tag for it. Its branch no longer exists on origin. Do not
push it; do not delete the tag.

## What this settles, and what it does not

**Settled — safety.** The long-standing "39 contained clone-1 local heads" question is closed, and
the answer extends to all 45: deletion is provably lossless. Combined with the T15 finding that
clone-2's 6 heads are also all contained, **no local branch or clone on this host holds unique
work.**

**Not settled — ownership.** Two lanes both claim ref-deletion scope (recorded block 3). This
document establishes that deletion is *safe*; it does not establish *who* may perform it. No
branch was deleted.

**Not settled — the 083 exception.** `083-glptutorial-corpus-goldens` is 2 ahead and is this
lane's own in-progress feature (board WP `in-progress`). It must not be retired — it is live work.
