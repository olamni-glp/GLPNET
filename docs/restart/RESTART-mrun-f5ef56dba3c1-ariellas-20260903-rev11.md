# RESTART — ariellas · glpnet · 2026-09-03 · rev11

**Supersedes rev10 (2026-09-02).** Resume with: **`resume marathon`**

```
HOST     ARIELLAS          LANE  glpnet         REPO  D:\BSTDEV\research\glp\GLPNET
BRANCH   develop @ fa341fd7 (pushed, clean)
FEATURE  .specify/feature.json -> specs/085-onrestart-fleet-resume
MARATHON no active run (status: absent for 085)
BOARD    %LOCALAPPDATA%\yngenios\ynet\mbox\ariellas.glpnet.48cedd.jsonl  (33 ops)
```

---

## 1 · WHAT THIS SESSION WAS

A 15-lane host-contention round on ARIELLAS, conducted over a file-CRDT board at
`%LOCALAPPDATA%\yngenios\ynet\mbox\` (16 mailboxes, 866+ ops). glpnet's role: **transport /
protocol lane**. 11 self-retractions filed — the largest count on the board.

---

## 2 · ENGINEER RULINGS — ALL FOUR RECORDED, TWO ALREADY APPLIED

Recorded via `tools/bkquestion` → `.specify/decisions/engineer-decisions.jsonl`, commit `fa341fd7`.

| id | ruling | state |
|---|---|---|
| `Q-glpnet-01` | Add a push permission rule | **APPLIED + VERIFIED** — two pushes landed |
| `Q-glpnet-02` | Pump → **lejepa** as a 16th era | **NOT DELIVERABLE ON THE BOARD** — lejepa has no live session and no mailbox; must travel in restart artifacts |
| `Q-glpnet-03` | Fix identity before reboot | **APPLIED** — 4 lanes pinned |
| `Q-glpnet-04` | **Both** pool AND second DIMM; DIMM is a declared dependency of mstack's era | recorded |

---

## 3 · THE DIAGNOSIS (measured, and corrected by peers)

| finding | value |
|---|---|
| Host commit vs physical | 33,950MB / 15,450MB = **2.20× oversubscribed** ← the *cause* |
| Transition faults | 80–102k/s (trim→refault storm) |
| CPU | 100%, **idle 0.00%**, privileged 57% > user 50% |
| Cores | **4 physical** (Ryzen 5 3550H), not 8 logical |
| **MsMpEng (Defender)** | **37.6% of host** — invisible to `Get-Process` (protected) |
| Per-lane commit | **~881MB FIXED FLOOR** (n=14, sd 55; 14h age gap moves it 3.4%) |
| D: | **Storage Spaces over a USB SSD**; only C: is NVMe |
| After elastic pool @ 6 hot | 24,728MB = **1.60× — still oversubscribed** |
| Pool + 32GB | **0.75×** — the only combination under 1.0 |

**Load-bearing conclusion:** `--autocompact` is NOT the lever (four independent null results).
**Lane count is.** And the elastic pool alone does not end the fault storm.

---

## 4 · GLPNET ERA CLAIM (`glpnet:000029`) — re-anchored, 2 approvals

**Bounded, fenced, windowed realtime frame plane**, extraction root `D:\BSTDEV\research\glp\glpnet`:

1. **Max frame length cap** — `TcpTransport` guards `len<0` but not `0x7FFFFFFF` → a 2GB alloc request. YNGCOR: *"a one-frame denial of service against the whole desk."* **FIRST.**
2. **Fencing token** — a stale pump must not interleave with its relaunched successor. Routine under a slot pool.
3. **FIFO window sized as a stated requirement** — and it must cover **measured consumer wake granularity**, not just the retransmit horizon (15.6ms unraised vs 1.57ms with `timeBeginPeriod(1)` = 10×).
4. **Bounded channel** (`LoopbackTransport.cs:91-92` is `CreateUnbounded<byte[]>`) — hardening, not headline.
5. All under the existing `ILinkTransport` seam; loopback first, QUIC-ready, identical bytes.

**Approvals: mstack, YNGCOR.** crucible attacked instead (at my request) and their method-diversity
rule now governs: **4 approvals must use 4 different METHODS, not 4 different lanes.** qhstate and
YNGLIN outstanding.

⚠ **`self_contained: false`** on the l0 projection — item 1 carries escaping deps to resolve.

---

## 5 · TRAPS — DO NOT RE-DERIVE

1. **TWO GLPNET REPOS.** `D:/BSTDEV/research/glp/GLPNET` (9f642258, develop) **is the extraction source**. `D:/BSTDEV/glp/GLPNET` (d45c40fa, `058-s4-policy-service`) is a separate repo — **and `LoopbackTransport.cs` exists in BOTH.** Authoring there builds, tests, passes review and **never reaches l0**.
2. **`Get-Process` reports 0.00 CPU for protected processes.** Use `Get-CimInstance Win32_Process` KernelModeTime+UserModeTime deltas.
3. **`Get-Disk` conceals bus type under Storage Spaces.** Use `Get-StoragePool | Get-PhysicalDisk`.
4. **Roadmap `export --json` is cross-project with NULL feature names.** Use `status` (30 of 124 not closed, 21 epics).
5. **`bkquestion` is FORKED at incompatible schemas** — glpnet's rejects `provenance`, qhstate's requires it.
6. **Lamport is unusable for selection here** — my own file had 26 distinct values across 47 ops. **Select by FILE ORDER** (append order under single-writer-per-file).
7. **YNET cannot carry cross-process traffic** — `YnetLink.Dial` throws pending 051 T011; `specs/051` is 0/23. Default routing is **Sealed** and `SealUnavailable` **fails closed**.

---

## 6 · SECURITY — OPEN

- **P0 class (era-088, shiras):** shared-write CRDT → config generator → RCE on every syncing peer. **Audited `oracle.py`: CLEAR today** (both `ps()` call sites literal; `roster()` reads the launcher's `$Repos`, not the board). **Latent by design** — the relaunch-on-crash feature reintroduces it the moment a spawn argument comes from an op. **Invariant to enforce:** *lane identities and paths used for spawn MUST come from the authoritative launcher table, never the board.*
- **Board has NO authentication** (YNGCOR). Attribution-by-filename is a **convention, not an invariant**.
- **Suppression leaves no evidence** (forgery does). **Highest-probability case is SELF-suppression** — removing one's own retractions is cheap, needs no conspiracy, and flatters the executor. **glpnet has the most retractions and therefore the most to gain — tripwire declared; YNGCOR holds the other end.**
- **Mitigation running:** cross-witnessing. Two independent WITNESS r1 ops (glpnet 866 ops/16 files; YNGCOR 849). **First cross-check PASSED: 0 decreases across 8 comparable lanes.**

---

## 7 · WHAT'S NEXT (in order)

1. **Relaunch lejepa** and deliver the pump allocation (`Q-glpnet-02`) — it cannot receive it otherwise.
2. **Collect approvals 3 and 4** on `glpnet:000029` from qhstate and YNGLIN, under crucible's **method-diversity** rule.
3. **Open the marathon era**, then run: `/bk-specify` → `/bk-clarify` → `/bk-plan` → `/bk-tasks` → `/bk-analyze` → `/bk-implement` → `/bk-codexreview` → `/bk-ship` → `/bk-close` → ERA close + tidy.
4. **Gate `/bk-implement` on a drill that fails against a mutant** (crucible's ruling): bounded vs unbounded channel, stalled consumer, measure RSS growth and time-to-observable-drop.
5. **Fit the second 16GB DIMM** — declared dependency of mstack's era.
6. **Read-side aggregation by lane component** — ynglin's `d6129e` (17 ops) is still orphaned; pinning fixed the write side only.

---

## 8 · REBOOT

`BK-OnRestart` (at-logon) executes **mstack's** launcher — *not* glpnet's copy:

```
pwsh -File "D:\BSTDEV\tools\mstack\scripts\fleet\post-reboot-restart.ps1" -WaitForMounts -Layout Tabs
```

15 lanes, tab order load-bearing. All six at-risk identity caches are pinned, so no lane
re-mints. **glpnet's `scripts/onrestart-launch.ps1` is a second, corrected implementation —
convergence is the 085 vehicle and is glpnet's leg.**
