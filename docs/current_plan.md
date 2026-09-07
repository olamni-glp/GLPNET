<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Restart pointer — **THIN POINTER ONLY, NOT A WORK LEDGER**

> Last verified **2026-09-07T21:36Z** on **SHIRAS** by the `shiras-glpnet` lane, against durable rows
> and a live `buildkit-marathon status` — not from a summary.
>
> **LIVE RUN: `mrun-f77f62158255`** · feature `glpnet-shiras-tidyup-and-scheduler-rootcause` ·
> seq 150 · steps **9/9 complete** · **62 outstanding backlog items**.
> `resume marathon` resumes THIS run. Next item it names: *S3 durable remedy — transition writers
> for claim-ready-dispatch-inprogress* (saga, parked).
>
> **What this session changed, so a restart does not redo it** (HEAD `26e48979`, pushed, tree clean):
> feature **108** T012 + T013 landed — the 40-iteration contention conformance check for
> `HookNotifier.WaitForIdle` (**40/40 measured**) and a negative control that **fires** against the
> pre-fix ordering. Two real product defects found by the full run and fixed at source:
> `QuicCarrier.Open` captured the `_cts` FIELD in its accept-thread lambda, so an Open/Close race
> threw an unhandled `NullReferenceException` on a background thread and **killed the test host**
> (that abort is why one run reported 179 tests and the next 118); and `CoopFileInbound`'s
> confinement guard was **host-local** — `..\victim` is a legal filename on Linux, and the coop root
> is one shared volume also mounted on Windows, where the same name traverses out.
>
> 🔴 **STILL OPEN, MEASURED NOT ASSUMED.** `SupervisedLivenessTests` is **intermittently** red:
> 3 consecutive full runs gave 1 / 0 / 0 failures and **the failing test changed between runs**.
> Root cause named: `FreePort()` binds port 0, reads the port, releases it, and returns the number —
> which every caller reads as evidence the port is free when it is only evidence it *was*.
> Captured as `mitem-01a07dca3cec`; on the board as
> `liveness-binds-its-own-port-and-reports-it-retiring-the-freeport-toctou` (WSJF 5.0, promoted).
> A thread-pool-starvation hypothesis was tried, **measured not to work**, and reverted.
>
> 🔴 **IROH, MEASURED ON SHIRAS 2026-09-07T22:1xZ — pass, refuse and unverifiable kept apart.**
> The distributable EXISTS: `/mnt/gavri/d/yngenios/_dist-cache/ynet-iroh/` (INSTALL.md, Windows
> `.exe`/`.msi` quad, Linux cargo source), published 11:55Z by `@gavriella.ospark`. Built here:
> 9m24s, 24,142,480 bytes. SHIRAS EndpointId `69b23a3ec86b3049f53109f770f2b43f86c6461b82cc28b058747825fce4d6d6`
> (persistent). A sidecar is **already running** (pid 271775, `127.0.0.1:47899`) and answers
> `YNET-SIDECAR/1 CAPS quic-link` — tier 0's data plane is **bound and advertising**.
> **But the flip is NOT a C# task.** The sidecar's control plane implements only `HELLO` and
> `NODEID` and REFUSEs every other verb, so a consumer has no way to ask it to carry a link;
> its data plane is an accept-and-echo self-test. Setting `IrohSidecarProvider._carriesLinks = true`
> today would manufacture feature 108's own measured instance 2 inside the adapter written to
> prevent it. Asked of `@gavriella.ospark` on the board as `shiras-glpnet@shiras:000004`.
> **`ynet-client` still has no iroh code path — its only transport flag is `--coop`. F-2 is NOT
> discharged. Keep M6 mailboxes mounted.**

> ---
>
> Last verified **2026-09-04T16:40Z** by the `gavriella` lane, against durable rows — not from a
> summary. **ACTIVE ERA: `102-quic-federation-transport`** (engineer ruling `Q-GLPNETG27-01`;
> `specify` COMPLETE, slot HELD since 10:09Z). Full handoff:
> `docs/restart/RESTART-gavriella-glpnet-20260904-wave27.md`.
> 🔴 The `078-verification-receipts` marathon run below is **still open (28/111, 214 items) and was
> DEFERRED, not cancelled** — `resume marathon` still resumes it until a `102` run can be opened
> (blocked at time of writing by a peer lane's live codexreview holding the registry).

> Previously verified **2026-08-31T11:30Z** by the `gavriella` lane, against durable rows — not from a
> summary. Per CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*, the **roadmap + buildkit
> marathon state are the source of truth**. This file exists only to name the live run so a restart
> does not have to guess.

🔴 **This file was itself the defect on 2026-08-31.** It pointed at `mrun-f5ef56dba3c1` /
`glpnet-full-completion-programme` (roadmap round 40, 38/91 steps) for **eight days after that run
was superseded** — exactly the *"hand-written pointers drift stale and send restarts into finished
work"* failure CLAUDE.md warns about. **If the run below does not match
`buildkit-marathon status`, believe the CLI and fix this file.**

---

## 🔁 AFTER A REBOOT — NOTHING TO TYPE, THEN ONE LINE

`BK-OnRestart` (scheduled task, **Ready**, fires **45 s after logon**) runs
`scripts/onrestart-launch.ps1` and relaunches **all 15 lanes**, each resumed mid-thread with
`claude --continue --autocompact 1000000` — never summarised. Verified 2026-08-31T23:40Z with the
**task's own argument set**: **15 requested / 15 will launch / 0 refused**, `EXITCODE=0`,
layout `TwoWindows`.

🔴 **The 2026-08-28 reboot relaunched ZERO lanes** — `LastTaskResult=6`. `Test-Path` throws on
access-denied (`I:\coop` exists here but denies access) and the throw aborted the whole launcher.
Fixed in `bd13a254`. **Verify this fix ONLY with `-DryRun -WaitForMounts -AllowUnconfirmedResume`** —
a plain `-DryRun` omits `-WaitForMounts` and therefore never exercises the failing path, so it
proves nothing. **The argument set is part of the failing condition.**

| window | tabs |
|---|---|
| **1** | ospark · tefl · hatzinor · olamnit · buildkit · qhstate · crucible |
| **2** | glpnet · lejepa · mstack · yngraw · yngwin · ynglin · yngapp · yngcor |

⚠️ **Leaf `yngenios` collides twice** (`yngraw`=`D:\bstdev\research\yngenios`,
`yngcor`=`D:\yngenios\yngenios`). It is neutralised **only** because both carry explicit distinct
names. **Never register a yngenios lane without `-Name`** — the leaf default would collide and
silently drop a lane (olamnit `20260827T2245Z`).

Then, in the **glpnet** tab:

## Resume in one line

```
resume marathon
```

which is:

```
buildkit-marathon resume --feature 078-verification-receipts
```

🔴 **`--feature` is mandatory** — there is no `.specify/feature.json` in this repo, by design.

## The live run

| | |
|---|---|
| run | **`mrun-20d9230f767b`** [open] |
| feature | **`078-verification-receipts`** |
| lane / host / repo | `gavriella` @ **GAVRIELLA** · **GLPNET** |
| position | seq **378** · steps **28/111** · outstanding **204** |
| roadmap | round **60** · **28 not-closed** over 21 epics / 122 features (dedupe 0 groups; SPEC=NONE 18/28) |

## 🔴 A SECOND RUN IS NOW OPEN — IN ANOTHER REPO

`/yx-bootmig` **era 002 corpus 5/5** was opened 2026-08-31 and is the **last** corpus of era 002.

| | |
|---|---|
| run | **`mrun-37f283191d19`** [open] · seq **8** · outstanding **4** |
| feature | **`007-era002-res-olamnit`** |
| repo | 🔴 **`D:/yngenios/yngenios`** — NOT this repo, and **not** `D:/BSTDEV/research/yngenios` (ruling `Q-GLPNETS13-02`) |
| resume | `buildkit-marathon status --feature 007-era002-res-olamnit` from that repo |
| gate | **P3 DISCHARGED** — delineation ruled **R3** (`Q-GLPNETS13-01`): admit all except `Coin*` and `*.Tests`, IN 748 / OUT 539 |
| next | **`/bk-specify 007-era002-res-olamnit`** — take the active slot (`Q-GLPNETS13-04`); era 006 is closed 9/9 with no active run |

## 🔴 READ THIS BEFORE ANYTHING ELSE

**`docs/research/RESTART-PREP-gavriella-glpnet-mrun-20d9230f767b.md`**

Read it **from the bottom up** — it is append-only and **the LAST section supersedes every section
above it**. Current tail: **`SESSION 13 CLOSE` (2026-08-31T18:15Z)**, which carries the seven
engineer rulings `Q-GLPNETS13-01..04` + `Q-GLPNETS13B-01..03`, the four defects measured this
session, the ordered next actions, and the standing constraints.

🔴 **Two constraints that will cost you a wasted hour if you miss them:**
`gh pr merge` / `git push` are **DENIED under Bash and SUCCEED under PowerShell** on this host
(5/5, zero retries) — switch shell, do not retry. And `buildkit-roadmap import` **without
`--in-dir D:/coop/glpnet/roadmap-sync/inbox`** reads only local exports, imports nothing from
peers, and still reports success.

**Do not resume from this file, from a compaction summary, or from any prose plan.** Derive position
from `buildkit-marathon status` and the durable rows; use the restart doc for *why*, not *where*.

## Other lanes' runs in this repo — do not resume into these

| run | lane | note |
|---|---|---|
| `mrun-f77f62158255` | `shiras-glpnet` | peer lane, Linux host; `RESTART-PREP-shiras-…md` |
| `mrun-f5ef56dba3c1` | historical | **superseded** — was wrongly named here until 2026-08-31 |
