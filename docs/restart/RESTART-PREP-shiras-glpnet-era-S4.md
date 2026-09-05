<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras-glpnet — era S4 — 2026-09-05T16:15Z

**Resume with exactly: `resume marathon`.**

    run          mrun-f77f62158255 [open]
    feature      glpnet-shiras-tidyup-and-scheduler-rootcause
    branch       develop @ 73b28ba9 · clean · 0 ahead · 0 behind
    restart-safe YES (measured via scripts/marathon_sitrep.py)
    M6           MET — daemon live, doctor MET, 0 pending alerts, origin high-water 12

---

## What this session delivered (all measured, none asserted)

**The P0 of 15:20Z is fixed, and the fix is proven live.** `send` and `run` no longer exclude each
other: a lane running the kernel-managed receiver M6 mandates can send again.

| evidence | value |
|---|---|
| suite | **93/93 green** (85 baseline re-measured FIRST, then 8 new) |
| live send, receiver **active** | `sent (stamped by the running receiver, seq=12)` exit 0 |
| receiver's own log | `spool drained 1788623494966-…send seq=12 outcome=Sent` |
| frame delivered | `…/shiras%2Fshiras-yngraw~468ac1021e48/inbox/1788623495477.0.….frame` |
| branch | qhstate `095-m6-send-spool` @ `fdb823c9` |

**Design:** `OriginLock` and FR-015 are unchanged in substance — still exactly **one stamper per
origin**. The running receiver already *was* the single writer, so `send` now hands work **to** it
through a ticket spool instead of competing with it. Two atomic filesystem operations; no socket, no
port, no new daemon.

Also fixed: `scripts/ynet-m6-run.sh` spliced its default flags **ahead of positional arguments**, so
`ack <id>` became `ack --lane …` and every ack failed with `no such alert: --lane`.

## 🔴 The one thing blocking the fleet — R-C, for @shiras-qhstate

**The branch is already in the qhstate repo's object store on this machine.** No push, no fetch:

    cd /mnt/biwin/D_DRIVE/BSTDEV/research/qhstate
    git merge 095-m6-send-spool
    dotnet build -c Release Csharp/yngenios/YngeniOS.Ynet.Client.Cli/YngeniOS.Ynet.Client.Cli.csproj

It was authored in a **worktree**, so that lane's uncommitted WIP was never touched. Until it merges,
**this lane's own systemd service runs the unpatched build** and must stop-send-start to publish —
which is how the two YNET broadcasts in this session were sent. That is the honest status.

## Engineer rulings this session — `.specify/questions/Q-glpnetshiras-20260905T1610Z.json` (validated)

- **R-C** — @shiras-qhstate merges the P0 fix from its own object store. This lane deploying a
  patched build for itself was **explicitly refused**: a binary nobody else has is the divergence
  R-B ended.
- **R-D** — the **20:00Z** subroot cutover **stands**, and **`yng-broker`/`yng-guardian` owns the
  window**. shiras-glpnet's coordination proposal is **withdrawn**. Comply with the broker's call.
- **R-E** — **`m6-send-spool-hardening` is the mandatory next era**, ahead of the board's rank-24 next.

## ⇒ NEXT ERA (R-E): `m6-send-spool-hardening` — WSJF 6.50 · RICE 1068.75 · promoted

Prerequisite: **R-C merge above.** Then, in order:

1. **`doctor` must SURFACE spool depth, rejected tickets and drain age.** It reported `MET` while
   sending was impossible — that blindness is *why the P0 hid*, and it will hide the next one.
2. An operator verb to **list / replay / discard** spool tickets.
3. A **bound on spool depth** plus back-pressure.
4. **Compact the crash-window ticket index** alongside journal compaction (it currently grows with
   the append-only journal).
5. **Prove it on all four hosts**, not just SHIRAS — the lesson of SC-003 is that "fixed" measured in
   one place is not fixed.

Run the full pipeline: `/bk-specify → clarify → plan → tasks → analyze → implement → codexreview →
ship → close`.

## Residual finding — check this on YOUR board too

**`pbft-leader-election`, `qhsm-virtual-terminals` and `iroh-quic-transport` are `promoted` with NO
SCORE.** A WSJF-descending board sorts an unscored feature to the bottom, so three features the
directives name as today's critical must-haves are **invisible to every ranking the fleet uses** —
`buildkit-roadmap next` here returns a rank-24 environment-contract feature instead. **Score them
before trusting any board's `next`.** Board state: **51 not-closed · 39 with no spec · 5 unscored.**

## Open / not done, stated plainly

- `buildkit-marathon capture` for the next era **did not land**: the machine registry was held by a
  **live** peer process (PID 95651) across 61 attempts. Not killed — it may be a peer's test run.
  **Re-run the capture on resume**; this document is the durable record until then.
- The qhstate push is still refused by this host's guard. R-C routes around it; the guard itself is
  untouched.
- `yng-broker` liveness is **unmeasured** on every host. R-D assigns it the 20:00Z window; if it
  cannot take it, the fleet needs a different answer fast.

## Restart procedure

Nothing to stop. Tree clean, pushed, M6 daemon healthy and alert queue empty.

    resume marathon
