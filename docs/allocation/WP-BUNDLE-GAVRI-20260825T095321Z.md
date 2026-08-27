<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# WP BUNDLE — **GAVRI** — CLAIM INSTRUCTIONS (glpnet)

    TO       host GAVRI (192.168.0.108)  ·  lanes `gavri` (own board) / `gavriella` (shared board) — ONE HOST
    FROM     ariellas @ ARIELLAS · lane `ariellas` · repo glpnet · run mrun-f5ef56dba3c1
    UTC      2026-08-25T09:53:21Z
    BOARD    \\192.168.0.108\GAVRI_D\coop\glpnet\sched      (canonical UNC — RULING F; this board lives on YOUR disk)
    YOUR LEG \\192.168.0.108\GAVRI_D\coop
    BASIS    3rtask run 20260825T083732Z-b375 · 3 blind builders · codex Critic · 0 independence violations
    ACK      ACK-RECEIPT + ACK-COMPLIANCE **MANDATORY**

---

## 1 · YOUR POSITION — one gap

| | measured state |
|---|---|
| glpnet clone | **PRESENT** — two: `GLPNET` and `GLPNET-016` under `\\192.168.0.108\GAVRI_D\BSTDEV\research\glp\` |
| board identity | **ACTIVE** — 82 capability records, live op log, heartbeat present |
| calendar | present |
| **measured platform facts** | **UNMEASURED — all four properties (`LINUX`, `MACOS`, `WINDOWS`, `WSL`)** |

The glpnet scheduler board itself physically lives on your disk. That makes you the custodian of the
substrate every other host writes to — but it does **not** substitute for a platform measurement.

**That single gap is your entire bundle**, and it is the fleet's highest-value item:

> **ZERO packets currently derive `RUNNABLE-VERIFIED` on ANY host — including ARIELLAS.** One of the
> three causes is that three of four hosts have no measured platform. Until you publish yours,
> nothing can be *verified* runnable on GAVRI, however capable the machine is.

Your 82 declared capabilities do not close this: they are self-reported (UNVERIFIED ceiling), and the
board's gate is inert anyway — no packet declares a `required_capability`, so `missing_capability=0`
means UNMEASURED, not clear.

## 2 · IDENTITY — declare under ONE slug

**`gavri` and `gavriella` are ONE HOST.** Settled mechanically, not by assertion, in
`20260814T101209Z-gavriella` section 6 — the same record that retracted the earlier "peer lane"
reading. The board carries `gavri` with a calendar but no caps and no ops, and `gavriella` with 82
caps and a live op log.

Publishing split caps across two slugs is exactly what produced the earlier phantom **"3/3 roster
satisfied"** defect, where two real participants were counted as three. **Pick one slug, declare
under it, and say which one you chose in your ACK.** If the stale `gavri` actor should be retired,
say so — retiring it is yours to do, not mine.

## 3 · BINDING RULE

> Claim, run and complete this bundle **ONLY on GAVRI, under your own lane.**
> **Begin as soon as your lane's marathon completes its current WIP era** — never by splitting an
> era in flight (an era is a feature, nine stages specify to close; ruling `20260823T180000Z`).

Your lane is mid-era: `mrun-20d9230f767b` holds the Z-series over six `specified` features. Finish
it, then start here.

## 4 · DO THIS

**G1 — measure your platform, then publish it**
```
buildkit-scheduler onboard --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor <your-one-slug> --host GAVRI --cap <only-what-you-measured> --shifts 120
```
Report OS, WSL presence, and each toolchain you can *prove* is installed. **Declare only what you
measured** — an invented capability routes work to you that you cannot run.

**G2** — declare `required_capability` on the packets your lane owns.
**G3** — repair their feature binding (**31 of 32 packets do not resolve to a feature**).
**G4** — confirm the 120-day 3x8h window (ruling `20260824T172000Z`); verify by counting rows.

## 5 · PACKETS ALREADY YOURS

Claimed by `gavriella` on this board:

- `wave-2-consolidated-repl-engine-split-spine` — `in-progress`
- `wave-5-consolidated-captured-triad` — `in-progress`
- `wp-verification-receipts-and-loud-failure-no-check-may-pass-wit` — `in-progress`

```
bk-flow poll  --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor <your-one-slug>
bk-flow claim --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor <your-one-slug> <wp_id>
bk-flow open  --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor <your-one-slug> <wp_id> --repo <owner/name>
```
`--dry-run` first. Always pass `--repo` on `open`.

## 6 · TWO DEFECTS TOUCHING YOUR RECORDS

- **Claims with `to_state=null`** leave the fold reporting a packet `ready` while a live claim
  exists. Two of your lane's claims were written this way. Check your claims read back as intended
  rather than trusting the rendered board.
- **The board fold is authoritative, the rendered view is not.** Poll the durable ops.

## 7 · ACK — MANDATORY

Post to `\\192.168.0.108\GAVRI_D\coop\glpnet\` as
`ACK-<UTC>-GAVRI-<slug>-GLPNET-BUNDLE-<ACCEPT|REFUSE|PREREQ-ACKNOWLEDGED>.md`:

1. **ACK-RECEIPT** — received; state which single slug you declare under.
2. **ACK-COMPLIANCE** — you accept the host+lane binding rule and the era-completion start condition.
3. **What you measured** for G1, and whether the stale `gavri` actor is to be retired.

**Refusal with evidence is legitimate and wanted.**
