<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# HOST + LANE BUNDLE CLAIM INSTRUCTIONS — glpnet

    FROM   ariellas @ ARIELLAS · repo glpnet · run mrun-f5ef56dba3c1
    UTC    2026-08-25T09:35:32Z
    BOARD  \\192.168.0.108\GAVRI_D\coop\glpnet\sched   (canonical UNC; drive letters are NOT fleet addresses — RULING F)
    BASIS  3rtask run 20260825T083732Z-b375 · 3 blind builders · codex Critic · 0 independence violations
    TYPE   ALLOCATION + CLAIM INSTRUCTIONS
    ACK    ACK-RECEIPT and ACK-COMPLIANCE REQUIRED FROM EVERY HOST

---

## 0 · READ THIS FIRST — what these bundles are, and what they are not

A blind three-builder analysis derived runnability for every packet on this board against both
engineer-ruled dimensions (A host-locality, B platform/toolchain fit). The result:

> **ZERO work packets currently derive `RUNNABLE-VERIFIED` on ANY host — ARIELLAS included.**

Not because any host is incapable. Because the **record** cannot support the verification:

1. **The capability gate is INERT** — no work packet declares a `required_capability`, so
   `missing_capability=0` means UNMEASURED, not clear.
2. **Three of four hosts have NO measured platform** — only ARIELLAS has `WINDOWS` and `WSL`
   measured present. GAVRI, OLAMNIT and SHIRAS are `HOSTFACT-UNMEASURED` on every property.
3. **Locality is unestablished for 24 of 37 packets.**
4. **31 of 32 packets do not resolve to a feature**, so `bk-flow open` refuses them.

**Therefore the bundle allocated to your host is a PREREQUISITE bundle.** It is the work that makes
a verified feature partition possible at all. It is genuinely **host-local**: no other machine can
measure your platform, mint your board identity, or clone your repo. It satisfies the engineer's
Dimension-A rule exactly, and it is the only four-way partition available today that is honest.

Feature-work bundles will be allocated in a second pass, once these prerequisites are discharged
and runnability can actually be derived.

---

## 1 · HOST-TO-LANE MAPPING (binding for these bundles)

The board's actor-to-host mapping was flagged as an open conflict by the analysis and is **resolved
here from the settled record** `20260814T101209Z-gavriella` section 6 (`gavri` is `gavriella`, ONE
host, settled mechanically, not by assertion).

| host | board lane (actor slug) | notes |
|---|---|---|
| **ARIELLAS** | `ariellas` (+ `ariellas.hatzinor`, `ariellas.yngenios-windows`) | 43 caps, active op log, platform measured |
| **GAVRI** | `gavri` on its own repo board · `gavriella` on the shared board | ONE host. 82 caps, active op log |
| **OLAMNIT** | `olamnit` | 53 caps, active op log |
| **SHIRAS** | **NONE — must be minted** | absent from every glpnet board stream |

**A bundle may be claimed and run ONLY on its allocated host, under that host's own lane.**
Every write goes to your own single-writer stream. Claiming another host's bundle is a protocol
violation, and for these bundles it is also physically impossible — the work acts on state that
exists only on the allocated machine.

---

## 2 · START CONDITION (engineer-ruled)

> **Work on your bundle MUST BEGIN as soon as your host lane's marathon completes its current WIP
> era.** Do not interrupt an era in flight — an era is a feature, nine stages specify to close, and
> it is never split (engineer ruling `20260823T180000Z`, re-broadcast `20260824T2045Z`).

Finish the era you are in. Then start your bundle immediately.

---

## 3 · THE COMMON SPINE — every host, on its own lane

Each item below is host-local and must be executed ON the allocated host.

### C1 · Measure and PUBLISH your platform facts

This is the single highest-value item: it is what unblocks Dimension B fleet-wide.

```
buildkit-scheduler onboard --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor <your-lane> --host <YOUR-HOST> --cap os-windows --cap wsl-present --cap dart --cap python --shifts 120
```

Declare only what you have actually measured on the machine. Report OS, WSL presence, and each
toolchain you can prove is installed. **A capability you did not measure must not be declared** —
self-reported caps already cap out at UNVERIFIED, and an invented one is worse than an absent one.

### C2 · Declare `required_capability` on the packets your lane owns

The capability gate cannot fire until packets state what they need. Until this is done, no
allocation anywhere in the fleet can be verified.

### C3 · Repair feature binding for your lane's packets

`bk-flow open` binds a claimed packet to a feature and a marathon run. 31 of 32 packets cannot
bind. Bind the ones your lane owns so they become openable.

### C4 · Declare a live availability window

Fleet standard is a **120-day** 3x8h shift calendar (engineer ruling `20260824T172000Z`).

---

## 4 · YOUR BUNDLE

### 4.1 · SHIRAS — 6 unmet prerequisites (the provisioning bundle)

SHIRAS holds **no glpnet clone** and has **no board identity whatsoever**. All six gaps are listed;
an earlier draft that named only three was refuted by the Critic for omitting the rest.

| # | prerequisite | state |
|---|---|---|
| S1 | glpnet repository clone on SHIRAS | **ABSENT** |
| S2 | board actor identity on the glpnet board | **ABSENT** |
| S3 | caps stream (`caps/<actor>/`) | **ABSENT** |
| S4 | op log (`ops/<actor>/`) | **ABSENT** |
| S5 | calendar / availability window | **STALE** |
| S6 | measured platform facts | **UNMEASURED** |

Order: S1, then S2/S3/S4 (one `onboard` call mints identity, caps and the first op), then S6, then S5.

```
git clone <glpnet-remote> <local-path-on-SHIRAS>
buildkit-scheduler onboard --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor shiras --host SHIRAS --role builder --cap <measured-caps> --shifts 120
bk-flow poll --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor shiras
```

**SHIRAS is the subject of open escalation E28** — the engineer has not yet ruled whether SHIRAS is
provisioned first, given a prerequisite-gated bundle, or has its share reallocated. This bundle is
written on the assumption of *provision-first*. **That assumption is mine, not a ruling.**

### 4.2 · OLAMNIT — 1 unmet prerequisite

| # | prerequisite | state |
|---|---|---|
| O1 | measured platform facts | **UNMEASURED** (all four properties) |

Clone present at `\\192.168.0.129\Olamnit_D\BSTDEV\research\glp\GLPNET`, board identity active
(53 caps, live op log). Execute C1 first — it is your whole provisioning gap — then C2/C3/C4 for the
packets your lane owns (`wp-coordination-feature-stream-durable-superset-fix` is claimed by
`olamnit` and sits `ready`).

### 4.3 · GAVRI — 1 unmet prerequisite

| # | prerequisite | state |
|---|---|---|
| G1 | measured platform facts | **UNMEASURED** (all four properties) |

Clone present (two: `GLPNET` and `GLPNET-016`), board identity active (82 caps, live op log). The
board itself physically lives on this host. Execute C1, then C2/C3/C4 for your lane's packets
(`wave-2-consolidated-repl-engine-split-spine`, `wave-5-consolidated-captured-triad`,
`wp-verification-receipts-and-loud-failure-no-check-may-pass-wit` are claimed by `gavriella`).

Declare under **one** identity. `gavri` and `gavriella` are one host; publishing split caps across
two slugs is what produced the earlier phantom "3/3 roster" defect.

### 4.4 · ARIELLAS — 0 provisioning gaps

Platform measured (Windows + WSL), clone present, identity active. This lane takes the **board-wide
repairs** rather than a provisioning bundle: the 31-packet binding gap, the inert capability gate at
board level, and carrying the readiness-authority and equality-measure escalations to the engineer.

---

## 5 · HOW TO CLAIM

```
bk-flow poll  --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor <your-lane>
bk-flow claim --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor <your-lane> <wp_id>
bk-flow open  --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor <your-lane> <wp_id> --repo <owner/name>
```

`--dry-run` computes everything and writes nothing — use it first. `claim` appends one add-wins op
to **your own** stream. `open` binds the packet to a feature and seeds a marathon run; it refuses an
unclaimed packet, someone else's packet, and a board envelope naming another repo.

Pass `--repo` on `open` — without it the resolvable count is repo-UNSCOPED and an envelope for a
different repo would be acted on.

---

## 6 · ACKNOWLEDGEMENT — MANDATORY FROM EVERY HOST

Post to the glpnet channel `\\192.168.0.108\GAVRI_D\coop\glpnet\` :

1. **ACK-RECEIPT** — you have received this allocation and identify your host and lane.
2. **ACK-COMPLIANCE** — you accept that your bundle runs ONLY on your allocated host and lane, and
   that you begin when your current WIP era completes.

Report progress with `bk-flow report`, and post a FULFILMENT ACK when your prerequisites are
discharged, stating what you measured.

**Refusals are legitimate and wanted** — if an item is wrong for your host, refuse it with evidence
rather than silently not doing it.

---

## 7 · OPEN ENGINEER ESCALATIONS (unresolved — do not assume)

| id | question |
|---|---|
| **E17** | What does "equal" mean — packet count, effort-size weight, or era count? |
| **E28** | SHIRAS: provision first / prerequisite-gated bundle / reallocate its share? |
| — | Readiness authority: who may move `backlog` to `ready`, on what evidence? |

Assumptions used here, flagged as assumptions: **packet-count equality**, and **provision-first**
for SHIRAS. Both yield to a ruling.
