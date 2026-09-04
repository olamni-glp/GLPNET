<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART — ariellas · glpnet · 2026-09-04 · rev12

**Supersedes rev11 (2026-09-03).** Resume with: **`resume marathon`**

```
HOST     ARIELLAS        LANE  glpnet        REPO  D:\BSTDEV\research\glp\GLPNET
BRANCH   develop (pushed, clean)
MARATHON mrun-f5ef56dba3c1  feature glpnet-full-completion-programme
         seq 356 · steps 42/135 · outstanding 166 · next W11 (BLOCKED, see §4)
ROADMAP  21 epics · 124 features · 30 NOT-CLOSED · export 20260904T074828Z
BOARD    %LOCALAPPDATA%\yngenios\ynet\mbox\ariellas.glpnet.48cedd.jsonl  (ops -> glpnet:000041)
```

🔴 **`--feature` is MANDATORY.** A bare `resume` reads `.specify/feature.json` (→ `specs/085-…`) and
falsely prints *"no active marathon run"*. rev11's own header shows that false negative.

🔴 **`docs/current_plan.md` IS NOT THIS HOST'S POINTER.** It is the **gavriella** lane's file and names
`mrun-20d9230f767b` / `078-verification-receipts` — a run that **does not exist in ARIELLAS's catalog**.
The marathon catalog is per-machine. This file is the ariellas pointer. I did not edit theirs.

---

## 1 · THE ONE-LINER

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

---

## 2 · WHAT THIS SESSION DID (2026-09-04, sessions 17–18)

Responded to the engineer's QHSM/QMSM virtual-terminal directive wave. **Wrote nothing outside GLPNET.**

| # | outcome |
|---|---|
| 1 | **S4 (carrier / data plane) ACCEPTED and CLAIMED** from yngcor's 0646Z seam map — it is `glpnet:000029` re-anchored |
| 2 | **GPL-3.0 licence blocker found** under the whole route (§5) |
| 3 | **S4 RESCOPED in public** — the carrier already exists in a 6th place I had not counted (§5) |
| 4 | **S1 premise CONFIRMED** for yngcor — no `ISpawn`; but a near-miss exists (§5) |
| 5 | **Leader election answered with a substrate measurement** — Raft cannot run here yet (§5) |
| 6 | **Self-reported an op I emitted under yngcor's identity** — not deleted (§3) |
| 7 | Roadmap import ×3 inboxes, reconcile, dedupe, export; BK-STD-1 not-closed table (§6) |
| 8 | **9 engineer questions filed**: `Q-GLPNETA18-01..04`, `Q-GLPNETA19-01..04`, `Q-GLPNETA20-01` |

Ledger ops emitted: **`glpnet:000034` … `glpnet:000041`** (8).
Broadcast docs: `docs/fleet/BROADCAST-ariellas-glpnet-20260904T0700Z-*.md` and `…T0800Z-*.md`.

---

## 3 · 🔴 MY OWN DEFECT — READ THIS BEFORE RUNNING ANY FLEET SCRIPT

At **07:36:37Z** I ran `scripts/fleet/ynet-witness.py` **with the yngenios repo as CWD**. It derives its
emitting identity from **that repo's config, not the caller**, and appended op `6f959bf9406f9aac` into
**`ariellas.yngcor.2f5a32.jsonl`** — I emitted **as yngcor**.

**The op is NOT deleted.** Removal is indistinguishable from suppression, which is the one manipulation
this board cannot detect. Disclosed as `glpnet:000037`.

🔴 **Do not re-run `ynet-witness.py` until it takes an explicit `--as`/`--agent-id` and refuses on
mismatch.** Filed to `yngwin` as S5 scope.

---

## 4 · WHY THE ERA IS NOT "COMPLETE" — AND IT IS NOT A DEFERRAL

The engineer instructed *RUN CURRENT ERA TO FULL COMPLETION, NO DEFERRALS*. **W11 cannot be run by this
lane or by Gabi.** It is gated on discharge item **J2 — a §1.14 LANGUAGE-AUTHORITY ruling reserved to
Udi** (occurs-check violation = `UnifyFail` runtime, or `CompileError` static). CLAUDE.md *Language
Authority* and DISCIPLINE §1.14 both reserve it. **This is the only block in the programme Gabi cannot
clear.** Raised as `Q-GLPNETA19-01`.

**Unblocked and available now** (all carry `PREREQ W10`, complete):

| step | work |
|---|---|
| **W12** | decide the 067 vs 067b qr-link survivor (067: 10 open tasks/8 conflicts · 067b: 0/12) |
| **W13** | land or re-derive `051-ynet-transport` (5 conflicts, 23 open tasks, draft PR 111) |
| **W14** | land or re-derive `066-wave6-consolidation` (9 conflicts, 18 open tasks) |
| **W15** | land or retire `058-s4-policy-service` (1 ahead, 11 conflicts; second clone's branch) |
| **W16/W17** | re-derive vs abandon `016`/`017`, `030-phase8-polish` |
| **W19** | delete the 129 provably-contained origin refs (W03 restore-verify + W04 tags done) |
| ~~W18~~ | **ESCALATED** — Gleam cluster; marathon holds two contradictory reads (N12 vs C1) |

---

## 5 · THE FIVE MEASURED FINDINGS — DO NOT RE-DERIVE

1. **GPL-3.0 under L0.** `yngenios` `LICENSE` = MIT, but `l0/ports.win32` + `l0/ports.posix` are QP/C
   (Quantum Leaps) `GPL-3.0-or-later OR LicenseRef-QL-commercial`, `state: admitted`, origin root
   `D:\BSTDEV\research\qhstate`. The **four** MIT-stamped C# `QHsm.cs` copies call themselves *"a
   faithful C# port of QP/C qep_hsm.c"*. **Zero** repo-wide mentions of GPL/Quantum Leaps outside the
   port trees. Link/build inclusion **NOT measured** — not asserting contamination. `Q-GLPNETA18-01`.
2. **The carrier exists in 6 places; the best is the one nobody counted.**
   `prototype/src/Yng.Shared/Ring/` — `RingLayout.cs` 168, `SlotStateMachine.cs` 273,
   `MappingInterop.cs` 342. Byte-precise: magic `'YNGR'`, 8 slots × 32 MiB, DataOffset `0x2000`,
   256 MiB ring file, normative `contracts/ring-layout.md`. **S4 is CHOOSE + CONSOLIDATE + HARDEN,
   not build.**
3. **S1 premise holds.** `grep ISpawn prototype/src` = 0 hits. The near-miss: `Yng.Broker.SpawnEngine`
   (480 lines, **running**) spawns **Docker containers via npipe**, not Windows processes — **and
   Docker is NOT running** (`127.0.0.1:2375` refused). Process green, capability red.
4. **Raft/Paxos/ZAB/PBFT cannot run on this transport.** Board is host-local (16 mailboxes, 954 ops,
   all ARIELLAS). **SHIRAS has no mount here.** 🔴 **`H:` and `I:` are the SAME UNC**
   (`\\192.168.0.108\GAVRI_D`) → drive-letter peer enumeration gives **GAVRI two votes** = split-brain
   generator. Use the **shipped R10 single-writer lease**
   (`buildkit_cli/scheduler/engine/daemon/confirm.py:338,409,414` — *"refusing rather than becoming a
   second writer"*). **Federation is a prerequisite of election.** `Q-GLPNETA19-02/03`.
5. **Two auth models, two mailboxes.** Ed25519 (yngcor S6) vs macaroons (`Macaroon.cs` 155,
   `CapabilityToken.cs` 44); file-CRDT JSONL board vs `PgWireClient.cs` 295. **Board of record never
   ruled.** `Q-GLPNETA20-01`.

---

## 6 · ROADMAP + FEDERATION STATE

```
import  I:\coop\glpnet\roadmap-sync\inbox  (294 entries) -> 3 publishers REFUSED, unknown key
import  G:\coop\glpnet\roadmap-sync\inbox  (194 entries) -> 133 already-applied, 2 REFUSED
import  D:\coop\glpnet\roadmap-sync\inbox  ( 36 entries) -> local dead-drop (recorded defect)
reconcile  75/124 features carry NO spec_path; 4 pipeline records cannot move a roadmap state
dedupe     123 live scanned, 0 duplicate groups
export     21 epics / 124 features / 4062 journal lines -> ariellas__glpnet__20260904T074828Z.json
table      30 NOT-CLOSED = 2 analyzed · 1 captured · 3 implemented · 19 promoted · 5 specified
```

🔴 **Cross-host roadmap convergence is ZERO.** The channel is mounted and populated and imports nothing,
because every peer publisher key is untrusted. **Do NOT pass `--allow-untrusted`** — recorded ruling
**N7**: fix at the producer. `Q-GLPNETA19-04`.

---

## 7 · OPEN ENGINEER QUESTIONS — 9, ALL VALIDATED AGAINST `tools/bkquestion`

| set | ids | subject |
|---|---|---|
| `Q-GLPNETA18-20260904T0700Z` | 01–04 | GPL under L0 · write pen · three CLIs · cross-host ACK |
| `Q-GLPNETA19-20260904T0800Z` | 01–04 | no-deferrals/J2 · quorum first · lease vs Raft · trust gate |
| `Q-GLPNETA20-20260904T0800Z` | 01 | board of record (blocks S6 hardening) |

---

## 8 · WHAT'S NEXT, IN ORDER

1. **Engineer rules `Q-GLPNETA19-01`** — then either W12 starts immediately or J2 goes to Udi.
2. **W12 → W17, W19** — the unblocked marathon steps above.
3. **S4 spec** — only after the carrier choice in finding 2 is ruled.
4. **Collect approvals 3 and 4** on `glpnet:000029`/S4 from `qhstate` and `YNGLIN`, **four different
   METHODS** (crucible's rule). `mstack` and `YNGCOR` already approved.
5. **Off-host eras stay UNDELIVERED** until an ACK returns — GAVRI, OLAMNIT, SHIRAS cannot see this
   board; `lejepa` has no mailbox at all.

---

## 9 · REBOOT

`BK-OnRestart` fires **mstack's** launcher, *not* glpnet's copy:

```
pwsh -File "D:\BSTDEV\tools\mstack\scripts\fleet\post-reboot-restart.ps1" -WaitForMounts -Layout Tabs
```

⚠ yngcor records the task as **Disabled on ARIELLAS** (their §7) — **verify before trusting it**; if
disabled, the launch is by hand after logon. Verify a fix **only** with
`-DryRun -WaitForMounts -AllowUnconfirmedResume` — a plain `-DryRun` omits `-WaitForMounts` and never
exercises the path that failed on 2026-08-28 (`LastTaskResult=6`, zero lanes).

Lanes: `ospark · tefl · ulpanit(hatzinor) · olamnit · buildkit · qhstate · crucible · glpnet · lejepa ·
mstack · yngraw · yngwin · ynglin · yngapp · yngcor`.
⚠ **Never register a yngenios lane without `-Name`** — the leaf default collides and silently drops a lane.

---

**rev12 · `ariellas.glpnet` · 2026-09-04T08:00Z · resume with `resume marathon`**
