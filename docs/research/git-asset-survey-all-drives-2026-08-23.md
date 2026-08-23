<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Git-asset survey — ALL local drives, host `Ariellas`

**Measured**: 2026-08-23. **Marathon step**: T15 (`mstep-01a02e95-50c5-7661-ac98-8b7aa7a7a115`).
Supersedes the D:-only survey and the C:-only follow-up.

## Scope

Local fixed drives **C:, D:, E:** scanned for `.git` directories matching `glpnet`.

**Network drives were deliberately excluded** and this is not an omission: `G:`
(`\192.168.0.129\Olamnit_D`), `H:` and `I:` (`\192.168.0.108\GAVRI_D`) and `J:`
(`\192.168.0.170\Shiras_Share`) are **other hosts' storage**. A host-local asset survey has
no standing over them, and the lane-ownership rule forbids acting on a peer's assets. Each
peer must survey its own.

## Findings

| Path | Drive | Kind | Verdict |
|---|---|---|---|
| `D:\BSTDEV\research\glp\GLPNET` | D: | the working repo | in use |
| `D:\BSTDEV\glp\GLPNET` | D: | clone-2 | **fully preserved — see below** |
| `D:\...\GLPNET\gleam_quic\profile_c\_build\...\quicer\msquic` | D: | **NEW** — vendored upstream | not an asset |
| `C:\...\3a631f2e\scratchpad\restore\050` | C: | bundle-restore clone | verified safe |
| `C:\...\94a409ef\scratchpad\jkmv-sandbox\clone` | C: | gutted shell, 0 objects | verified safe |
| E:\ | E: | empty WD external drive | no repos |

The `msquic` checkout is `github.com/microsoft/msquic` at tag `v2.3.8`, sitting under a
**gitignored** `_build/` path (`gleam_quic/profile_c/.gitignore:3:_build/`). It is a
regenerable build artifact holding no glpnet work. It was invisible to both prior surveys and
is harmless — recorded so the next survey does not re-raise it as a finding.

## Clone-2 carries NO unique unpreserved work — all 6 heads contained

| Head | SHA | Preserved by |
|---|---|---|
| `050-glp-native-quic-link` | `d7481ce8` | an origin ref |
| `051-ynet-transport` | `b3b6c2bf` | `origin/051-ynet-transport` + `archive/051-ynet-transport-20260820` |
| `058-s4-policy-service` | `d45c40fa` | **`archive/058-s4-policy-service-20260820`** (its branch was deleted) |
| `develop` | `b2ece375` | an origin ref |
| `main` | `57fa2066` | **`origin` tag `v2026.07.13.1`, whose peeled target IS `57fa2066`** |
| `release/v2026.07.13.1` | `c7b05070` | an origin ref |

Plus a local 70 MB bundle at
`D:/BSTDEV/evidence/glpnet-tidyup-20260820/clone2/clone2-main-local-only.bundle`.

**Retiring clone-2 is safe on containment grounds.** Lane ownership remains a separate open
question and is not settled by this survey.

## Two corrections to the W05 preservation index

The index at `D:/BSTDEV/evidence/glpnet-tidyup-20260820/clone2/INDEX.txt` (2026-08-20) is
wrong in two ways, both traceable to it being generated **inside clone-2**, whose
remote-tracking refs were 840 commits stale:

1. It records `058-s4-policy-service d45c40fa` as matching `origin/058-s4-policy-service`.
   **That branch no longer exists** — it was among the 127 refs the peer deleted on
   2026-08-21. The commit survives only because W04 had already pushed an archive tag. The
   preserve-then-delete ordering did its job; the index is simply now out of date.
2. It records clone-2's main as carrying "2 commits never pushed to `origin/main`". True as
   literally worded, and **materially misleading**: both are reachable from the pushed release
   tag `v2026.07.13.1`. They were pushed — as a tag, not on a branch.

**Rule this yields**: containment must be tested against **branches *and* tags**, from a clone
with **fresh** remote-tracking refs. A containment check that looks only at
`refs/remotes/origin/*` reports false "uncontained" results — this survey made that exact
error before catching it.
